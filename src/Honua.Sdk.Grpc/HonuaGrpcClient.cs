// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Honua.Sdk.Abstractions.Authentication;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Grpc.Conversion;
using Microsoft.Extensions.Options;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc;

/// <summary>
/// gRPC client for the Honua FeatureService.
/// </summary>
public sealed class HonuaGrpcClient :
    IHonuaGrpcClient,
    IHonuaFeatureQueryClient,
    IHonuaFeatureEditClient,
    IHonuaFeatureDescriptorClient,
    IHonuaFeatureAttachmentClient,
    IDisposable
{
    private const string FeatureServiceName = "geospatial.v1.FeatureService";
    private const string UnsupportedAttachmentReason = "gRPC FeatureService does not expose attachment RPCs yet.";

    private static readonly JsonSerializerOptions FeatureJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly FeatureEditCapabilities GrpcEditCapabilities = new()
    {
        SupportsAdds = true,
        SupportsUpdates = true,
        SupportsDeletes = true,
        SupportsRollbackOnFailure = true,
        NativeSurface = "grpc FeatureService.ApplyEdits"
    };
    private static readonly FeatureAttachmentCapabilities GrpcAttachmentCapabilities = new()
    {
        NativeSurface = "grpc FeatureService attachments",
        UnsupportedReason = UnsupportedAttachmentReason
    };

    private readonly Proto.FeatureService.FeatureServiceClient _client;
    private readonly GrpcChannel? _ownedChannel;
    private readonly HonuaGrpcClientOptions _options;
    private readonly Metadata? _metadataOverride;

    /// <summary>
    /// Creates a new gRPC client using the provided options.
    /// </summary>
    /// <param name="options">Configuration options for the client.</param>
    public HonuaGrpcClient(IOptions<HonuaGrpcClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var opts = options.Value;
        var address = HonuaGrpcClientOptions.ParseAndValidateAddress(opts.Address);
        HonuaGrpcClientOptions.ValidateTimeout(opts.Timeout);
        ValidateAuthenticationTransport(opts, address);

        var channelOptions = new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials =
                HasCredentials(opts) && HonuaGrpcClientOptions.IsLocalDevelopmentHttp(address),
            ServiceConfig = BuildServiceConfig(opts)
        };
        if (opts.PrimaryHttpMessageHandlerFactory is { } primaryHandlerFactory)
        {
            channelOptions.HttpHandler = primaryHandlerFactory();
        }

        _ownedChannel = GrpcChannel.ForAddress(address, channelOptions);
        _client = new Proto.FeatureService.FeatureServiceClient(_ownedChannel);
        _options = opts;
    }

    /// <summary>
    /// Creates a new gRPC client using a pre-configured channel.
    /// </summary>
    /// <param name="channel">The gRPC channel to use.</param>
    /// <param name="options">Optional client options for authentication.</param>
    public HonuaGrpcClient(GrpcChannel channel, HonuaGrpcClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var opts = options ?? new HonuaGrpcClientOptions();
        HonuaGrpcClientOptions.ValidateTimeout(opts.Timeout);
        if (HasCredentials(opts))
        {
            ValidateAuthenticationTransport(opts, ResolveChannelAddress(channel));
        }

        _client = new Proto.FeatureService.FeatureServiceClient(channel);
        _options = opts;
    }

    // For testing - inject the generated client stub directly
    internal HonuaGrpcClient(Proto.FeatureService.FeatureServiceClient client, Metadata? metadata = null)
    {
        _client = client;
        _options = new HonuaGrpcClientOptions();
        _metadataOverride = metadata ?? new Metadata();
    }

    // For testing - inject the generated client stub directly with live options
    internal HonuaGrpcClient(Proto.FeatureService.FeatureServiceClient client, HonuaGrpcClientOptions options)
    {
        _client = client;
        HonuaGrpcClientOptions.ValidateTimeout(options.Timeout);
        _options = options;
    }

    /// <inheritdoc />
    public string ProviderName => "grpc";

    /// <inheritdoc />
    public FeatureEditCapabilities EditCapabilities => GrpcEditCapabilities;

    /// <inheritdoc />
    public FeatureAttachmentCapabilities AttachmentCapabilities => GrpcAttachmentCapabilities;

    /// <inheritdoc />
    public async Task<SourceDescriptor> GetDescriptorAsync(SourceDescriptor descriptor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var locator = descriptor.Locator;
        if (string.IsNullOrWhiteSpace(locator.ServiceId) || !locator.LayerId.HasValue)
        {
            throw new ArgumentException(
                "gRPC descriptor discovery requires SourceDescriptor.Locator.ServiceId and LayerId.",
                nameof(descriptor));
        }

        var schemaResponse = await QueryFeaturesAsync(
            new Models.QueryFeaturesRequest
            {
                ServiceId = locator.ServiceId,
                LayerId = locator.LayerId.Value,
                Where = "1=1",
                ReturnGeometry = false,
                ResultRecordCount = 0,
            },
            ct).ConfigureAwait(false);
        var extentResponse = await QueryFeaturesAsync(
            new Models.QueryFeaturesRequest
            {
                ServiceId = locator.ServiceId,
                LayerId = locator.LayerId.Value,
                Where = "1=1",
                ReturnGeometry = false,
                ReturnExtentOnly = true,
            },
            ct).ConfigureAwait(false);

        return descriptor with
        {
            Capabilities = BuildDiscoveredCapabilities(),
            Schema = BuildSourceSchema(schemaResponse, extentResponse.Extent),
        };
    }

    /// <inheritdoc />
    public async Task<Models.QueryFeaturesResponse> QueryFeaturesAsync(
        Models.QueryFeaturesRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var protoRequest = ProtoAdapter.ToProtoRequest(request);
        try
        {
            var metadata = await BuildMetadataAsync("QueryFeatures", ct).ConfigureAwait(false);
            var protoResponse = await _client.QueryFeaturesAsync(
                protoRequest,
                metadata,
                deadline: CreateDeadline(),
                cancellationToken: ct);
            return ProtoAdapter.FromProtoResponse(protoResponse);
        }
        catch (RpcException ex)
        {
            throw new HonuaGrpcException(ex.StatusCode, ex.Status.Detail, ex);
        }
    }

    /// <inheritdoc />
    public async Task<FeatureQueryResult> QueryAsync(
        FeatureQueryRequest request, CancellationToken ct = default)
    {
        var grpcRequest = BuildGrpcQuery(request);
        var response = await QueryFeaturesAsync(grpcRequest, ct).ConfigureAwait(false);
        return ToFeatureQueryResult(response, grpcRequest.ReturnCountOnly);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(
        FeatureQueryRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var page in QueryFeaturesStreamAsync(BuildGrpcQuery(request), ct).ConfigureAwait(false))
        {
            yield return ToFeatureQueryResult(page);
        }
    }

    /// <inheritdoc />
    public async Task<Models.ApplyEditsResponse> ApplyEditsAsync(
        Models.ApplyEditsRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var protoRequest = ProtoAdapter.ToProtoApplyEditsRequest(request);
        try
        {
            var metadata = await BuildMetadataAsync("ApplyEdits", ct).ConfigureAwait(false);
            var protoResponse = await _client.ApplyEditsAsync(
                protoRequest,
                metadata,
                deadline: CreateDeadline(),
                cancellationToken: ct);
            return ProtoAdapter.FromProtoApplyEditsResponse(protoResponse);
        }
        catch (RpcException ex)
        {
            throw new HonuaGrpcException(ex.StatusCode, ex.Status.Detail, ex);
        }
    }

    /// <inheritdoc />
    public async Task<FeatureEditResponse> ApplyEditsAsync(
        FeatureEditRequest request, CancellationToken ct = default)
    {
        var response = await ApplyEditsAsync(BuildGrpcEditRequest(request), ct).ConfigureAwait(false);
        return ToFeatureEditResponse(response);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FeatureAttachmentInfo>> ListAttachmentsAsync(
        FeatureAttachmentListRequest request,
        CancellationToken ct = default)
        => ThrowUnsupportedAttachmentAsync<IReadOnlyList<FeatureAttachmentInfo>>(request, ct);

    /// <inheritdoc />
    public Task<FeatureAttachmentContent> DownloadAttachmentAsync(
        FeatureAttachmentDownloadRequest request,
        CancellationToken ct = default)
        => ThrowUnsupportedAttachmentAsync<FeatureAttachmentContent>(request, ct);

    /// <inheritdoc />
    public Task<FeatureAttachmentResult> AddAttachmentAsync(
        FeatureAttachmentAddRequest request,
        CancellationToken ct = default)
        => ThrowUnsupportedAttachmentAsync<FeatureAttachmentResult>(request, ct);

    /// <inheritdoc />
    public Task<FeatureAttachmentResult> UpdateAttachmentAsync(
        FeatureAttachmentUpdateRequest request,
        CancellationToken ct = default)
        => ThrowUnsupportedAttachmentAsync<FeatureAttachmentResult>(request, ct);

    /// <inheritdoc />
    public Task<FeatureAttachmentResult> DeleteAttachmentAsync(
        FeatureAttachmentDeleteRequest request,
        CancellationToken ct = default)
        => ThrowUnsupportedAttachmentAsync<FeatureAttachmentResult>(request, ct);

    /// <inheritdoc />
    public async IAsyncEnumerable<Models.FeaturePage> QueryFeaturesStreamAsync(
        Models.QueryFeaturesRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var protoRequest = ProtoAdapter.ToProtoRequest(request);
        var metadata = await BuildMetadataAsync("QueryFeaturesStream", ct).ConfigureAwait(false);
        var call = _client.QueryFeaturesStream(
            protoRequest,
            metadata,
            deadline: CreateDeadline(),
            cancellationToken: ct);
        try
        {
            while (true)
            {
                Proto.FeaturePage protoPage;
                try
                {
                    if (!await call.ResponseStream.MoveNext(ct).ConfigureAwait(false))
                    {
                        yield break;
                    }

                    protoPage = call.ResponseStream.Current;
                }
                catch (RpcException ex)
                {
                    throw new HonuaGrpcException(ex.StatusCode, ex.Status.Detail, ex);
                }

                var page = ProtoAdapter.FromProtoPage(protoPage);
                yield return page;
                if (protoPage.IsLastPage)
                {
                    yield break;
                }
            }
        }
        finally
        {
            call.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ownedChannel?.Dispose();
    }

    private static ServiceConfig BuildServiceConfig(HonuaGrpcClientOptions opts)
    {
        var serviceConfig = new ServiceConfig();

        if (opts.EnableRetry)
        {
            var maxAttempts = Math.Clamp(opts.MaxRetryAttempts, 2, 5);

            serviceConfig.MethodConfigs.Add(new MethodConfig
            {
                Names =
                {
                    new MethodName { Service = FeatureServiceName, Method = "QueryFeatures" },
                    new MethodName { Service = FeatureServiceName, Method = "QueryFeaturesStream" }
                },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = maxAttempts,
                    InitialBackoff = TimeSpan.FromMilliseconds(500),
                    MaxBackoff = TimeSpan.FromSeconds(5),
                    BackoffMultiplier = 2,
                    RetryableStatusCodes =
                    {
                        StatusCode.Unavailable,
                        StatusCode.Internal
                    }
                }
            });
        }

        return serviceConfig;
    }

    private async Task<Metadata> BuildMetadataAsync(string methodName, CancellationToken cancellationToken)
    {
        if (_metadataOverride is not null)
        {
            return _metadataOverride;
        }

        var metadata = new Metadata();
        var context = HonuaAuthenticationSupport.CreateGrpcRequest(_options, "grpc", methodName);
        var apiKey = await HonuaAuthenticationSupport.ResolveApiKeyAsync(_options, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(apiKey))
        {
            metadata.Add("x-api-key", apiKey);
        }

        var accessToken = await HonuaAuthenticationSupport.ResolveAccessTokenAsync(
            _options,
            context,
            cancellationToken).ConfigureAwait(false);
        if (accessToken is not null && !string.IsNullOrWhiteSpace(accessToken.Token))
        {
            var tokenType = string.IsNullOrWhiteSpace(accessToken.TokenType) ? "Bearer" : accessToken.TokenType;
            metadata.Add("authorization", $"{tokenType} {accessToken.Token}");
        }

        if (_options.EnableCompressionNegotiation && !string.IsNullOrWhiteSpace(_options.AcceptedCompressionEncodings))
        {
            metadata.Add("grpc-accept-encoding", _options.AcceptedCompressionEncodings);
        }

        await HonuaAuthenticationSupport.EmitCredentialAppliedDiagnosticAsync(
            _options,
            context,
            hasApiKey: !string.IsNullOrWhiteSpace(apiKey),
            authorizationScheme: accessToken?.TokenType,
            hasAuthorization: accessToken is not null && !string.IsNullOrWhiteSpace(accessToken.Token),
            cancellationToken).ConfigureAwait(false);

        return metadata;
    }

    private DateTime CreateDeadline()
        => DateTime.UtcNow.Add(_options.Timeout);

    private static void ValidateAuthenticationTransport(HonuaGrpcClientOptions opts, Uri address)
    {
        if (!HasCredentials(opts))
        {
            return;
        }

        if (HonuaGrpcClientOptions.RequiresHttpsForAuthentication(address))
        {
            throw new InvalidOperationException(
                "Refusing to send gRPC credentials over an insecure connection. Use HTTPS, " +
                "or use loopback HTTP only for local development.");
        }
    }

    private static bool HasCredentials(HonuaGrpcClientOptions opts)
        => HonuaAuthenticationSupport.HasCredentialSource(opts);

    private static Uri ResolveChannelAddress(GrpcChannel channel)
    {
        if (Uri.TryCreate(channel.Target, UriKind.Absolute, out var targetAddress) &&
            IsHttpOrHttps(targetAddress))
        {
            return targetAddress;
        }

        // GrpcChannel.Target omits the scheme; Address preserves the original URI.
        var originalAddress = typeof(GrpcChannel)
            .GetProperty("Address", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?
            .GetValue(channel) as Uri;

        if (originalAddress is not null && IsHttpOrHttps(originalAddress))
        {
            return originalAddress;
        }

        throw new InvalidOperationException(
            "Honua gRPC preconfigured channel target must expose an HTTP or HTTPS address when credentials are configured.");
    }

    private static bool IsHttpOrHttps(Uri uri)
        => string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static List<string> BuildDiscoveredCapabilities()
        => FeatureCapabilities.All
            .Where(FeatureProtocolCapabilities.DefaultsFor(FeatureProtocolIds.Grpc).Contains)
            .ToList();

    private static SourceSchema BuildSourceSchema(Models.QueryFeaturesResponse response, Models.Extent? extent)
    {
        var objectIdField = string.IsNullOrWhiteSpace(response.ObjectIdFieldName) ? null : response.ObjectIdFieldName;
        return new SourceSchema
        {
            Fields = response.Fields.Select(ToSourceField).ToList(),
            PrimaryKey = objectIdField,
            ObjectIdField = objectIdField,
            GeometryType = ToSourceGeometryType(response.GeometryType),
            Extent = extent is not null ? ToFeatureBoundingBox(extent) : null,
            SpatialReference = FormatSpatialReference(response.SpatialReference),
            EditCapabilities = GrpcEditCapabilities,
            AttachmentCapabilities = GrpcAttachmentCapabilities,
        };
    }

    private static Task<TResult> ThrowUnsupportedAttachmentAsync<TResult>(object request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        throw new NotSupportedException(UnsupportedAttachmentReason);
    }

    private static SourceField ToSourceField(Models.FieldDefinition field)
        => new()
        {
            Name = field.Name,
            Type = field.FieldType.ToString(),
            Nullable = field.Nullable,
            Length = field.Length > 0 ? field.Length : null,
            Required = !field.Nullable,
        };

    private static FeatureSpatialGeometryType ToSourceGeometryType(Models.GeometryType geometryType)
        => geometryType switch
        {
            Models.GeometryType.Point => FeatureSpatialGeometryType.Point,
            Models.GeometryType.MultiPoint => FeatureSpatialGeometryType.MultiPoint,
            Models.GeometryType.LineString or Models.GeometryType.MultiLineString => FeatureSpatialGeometryType.Polyline,
            Models.GeometryType.Polygon or Models.GeometryType.MultiPolygon => FeatureSpatialGeometryType.Polygon,
            _ => FeatureSpatialGeometryType.Unspecified,
        };

    private static string? FormatSpatialReference(Models.SpatialReference? spatialReference)
        => spatialReference?.Wkid > 0
            ? string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"EPSG:{spatialReference.Wkid}")
            : spatialReference?.Wkt;

    private static Models.QueryFeaturesRequest BuildGrpcQuery(FeatureQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureSupportedFilterLanguage(request.FilterLanguage);
        EnsureSupportedSharedQueryFacets(request);

        if (string.IsNullOrWhiteSpace(request.Source.ServiceId))
        {
            throw new ArgumentException("A service ID is required for gRPC feature queries.", nameof(request));
        }

        if (!request.Source.LayerId.HasValue)
        {
            throw new ArgumentException("A layer ID is required for gRPC feature queries.", nameof(request));
        }

        return new Models.QueryFeaturesRequest
        {
            ServiceId = request.Source.ServiceId,
            LayerId = request.Source.LayerId.Value,
            Where = request.Filter ?? "1=1",
            ObjectIds = ResolveObjectIds(request),
            OutFields = request.OutFields,
            ReturnGeometry = request.ReturnGeometry ?? true,
            OutSr = ParseSpatialReference(request.OutputCrs),
            ResultOffset = request.Offset ?? 0,
            ResultRecordCount = request.Limit ?? 0,
            OrderBy = request.OrderBy ?? string.Empty,
            ReturnDistinct = request.ReturnDistinct ?? false,
            ReturnCountOnly = request.ReturnCountOnly ?? false,
            ReturnIdsOnly = request.ReturnIdsOnly ?? false,
            ReturnExtentOnly = request.ReturnExtentOnly ?? false,
            OutStatistics = BuildGrpcStatistics(request.OutStatistics),
            GroupBy = request.GroupBy,
            SpatialFilter = BuildSpatialFilter(request.SpatialFilter, request.Bbox),
        };
    }

    private static Models.ApplyEditsRequest BuildGrpcEditRequest(FeatureEditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Patches.Count > 0)
        {
            throw new NotSupportedException("gRPC shared edits do not support JSON Merge Patch operations.");
        }

        if (string.IsNullOrWhiteSpace(request.Source.ServiceId))
        {
            throw new ArgumentException("A service ID is required for gRPC feature edits.", nameof(request));
        }

        if (!request.Source.LayerId.HasValue)
        {
            throw new ArgumentException("A layer ID is required for gRPC feature edits.", nameof(request));
        }

        return new Models.ApplyEditsRequest
        {
            ServiceId = request.Source.ServiceId,
            LayerId = request.Source.LayerId.Value,
            Adds = request.Adds.Select(feature => ToGrpcEditFeature(feature, requireObjectId: false)).ToList(),
            Updates = request.Updates.Select(feature => ToGrpcEditFeature(feature, requireObjectId: true)).ToList(),
            Deletes = ResolveDeleteObjectIds(request),
            RollbackOnFailure = request.RollbackOnFailure,
            ForceWrite = request.ForceWrite,
        };
    }

    private static Models.Feature ToGrpcEditFeature(FeatureEditFeature feature, bool requireObjectId)
    {
        ArgumentNullException.ThrowIfNull(feature);

        return new Models.Feature
        {
            Id = ResolveObjectId(feature, requireObjectId),
            Attributes = feature.Attributes.ToDictionary(
                kvp => kvp.Key,
                kvp => ProtoAdapter.UnwrapJsonValue(kvp.Value)),
            Geometry = ToGrpcGeometry(feature.Geometry),
        };
    }

    private static long ResolveObjectId(FeatureEditFeature feature, bool required)
    {
        if (feature.ObjectId.HasValue)
        {
            return feature.ObjectId.Value;
        }

        if (!string.IsNullOrWhiteSpace(feature.Id) &&
            long.TryParse(feature.Id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var objectId))
        {
            return objectId;
        }

        if (required)
        {
            throw new ArgumentException("gRPC feature updates require a numeric feature ID or object ID.");
        }

        return 0;
    }

    private static List<long> ResolveDeleteObjectIds(FeatureEditRequest request)
    {
        var objectIds = new List<long>(request.DeleteObjectIds);
        foreach (var id in request.DeleteIds)
        {
            if (!long.TryParse(id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var objectId))
            {
                throw new ArgumentException("gRPC feature deletes require numeric feature IDs.", nameof(request));
            }

            objectIds.Add(objectId);
        }

        return objectIds;
    }

    private static IReadOnlyDictionary<string, object?>? ToGrpcGeometry(JsonElement? geometry)
    {
        if (!geometry.HasValue)
        {
            return null;
        }

        if (ProtoAdapter.UnwrapJsonValue(geometry.Value) is IReadOnlyDictionary<string, object?> dictionary)
        {
            return dictionary;
        }

        throw new ArgumentException("gRPC feature edit geometry must be a JSON object.");
    }

    private static void EnsureSupportedFilterLanguage(FeatureFilterLanguage language)
    {
        if (language is not FeatureFilterLanguage.ProviderDefault and not FeatureFilterLanguage.SqlWhere)
        {
            throw new NotSupportedException("gRPC feature queries support provider-default or SQL WHERE filters.");
        }
    }

    private static void EnsureSupportedSharedQueryFacets(FeatureQueryRequest request)
    {
        if (request.TimeFilter is not null)
        {
            throw new NotSupportedException("gRPC shared queries do not support provider-neutral time filters until the geospatial proto adds a time query contract.");
        }

        if (!string.IsNullOrWhiteSpace(request.Having))
        {
            throw new NotSupportedException("gRPC shared statistics queries do not support provider-neutral having clauses yet.");
        }
    }

    private static List<Models.StatisticDefinition>? BuildGrpcStatistics(
        IReadOnlyList<FeatureQueryStatistic>? statistics)
    {
        if (statistics is not { Count: > 0 })
        {
            return null;
        }

        return statistics.Select(statistic => new Models.StatisticDefinition
        {
            OnStatisticField = statistic.OnField,
            StatisticType = ToGrpcStatisticType(statistic.StatisticType),
            OutStatisticFieldName = statistic.OutField,
        }).ToList();
    }

    private static Models.StatisticType ToGrpcStatisticType(FeatureStatisticType statisticType) => statisticType switch
    {
        FeatureStatisticType.Count => Models.StatisticType.Count,
        FeatureStatisticType.Sum => Models.StatisticType.Sum,
        FeatureStatisticType.Min => Models.StatisticType.Min,
        FeatureStatisticType.Max => Models.StatisticType.Max,
        FeatureStatisticType.Average => Models.StatisticType.Avg,
        FeatureStatisticType.StandardDeviation => Models.StatisticType.Stddev,
        FeatureStatisticType.Variance => Models.StatisticType.Var,
        _ => throw new ArgumentOutOfRangeException(
            nameof(statisticType),
            statisticType,
            "Feature statistic type must be specified."),
    };

    private static IReadOnlyList<long>? ResolveObjectIds(FeatureQueryRequest request)
    {
        if (request.ObjectIds is { Count: > 0 })
        {
            return request.ObjectIds;
        }

        if (request.FeatureIds is not { Count: > 0 })
        {
            return null;
        }

        var objectIds = new List<long>(request.FeatureIds.Count);
        foreach (var featureId in request.FeatureIds)
        {
            if (!long.TryParse(featureId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var objectId))
            {
                throw new ArgumentException("gRPC feature IDs must be numeric object IDs.", nameof(request));
            }

            objectIds.Add(objectId);
        }

        return objectIds;
    }

    private static Models.SpatialFilter? BuildSpatialFilter(
        FeatureSpatialFilter? spatialFilter,
        FeatureBoundingBox? bbox)
    {
        EnsureSingleSpatialFilter(spatialFilter, bbox);

        if (spatialFilter is not null)
        {
            return new Models.SpatialFilter
            {
                Geometry = BuildSpatialGeometry(spatialFilter),
                SpatialRelationship = ToGrpcSpatialRelationship(spatialFilter.Relationship),
                SpatialReference = ParseSpatialReference(spatialFilter.Crs),
            };
        }

        if (bbox is null)
        {
            return null;
        }

        return new Models.SpatialFilter
        {
            Geometry = new Dictionary<string, object?>
            {
                ["xmin"] = bbox.MinX,
                ["ymin"] = bbox.MinY,
                ["xmax"] = bbox.MaxX,
                ["ymax"] = bbox.MaxY,
            },
            SpatialRelationship = Models.SpatialRelationship.Intersects,
            SpatialReference = ParseSpatialReference(bbox.Crs),
        };
    }

    private static void EnsureSingleSpatialFilter(FeatureSpatialFilter? spatialFilter, FeatureBoundingBox? bbox)
    {
        if (spatialFilter is not null && bbox is not null)
        {
            throw new ArgumentException("Feature query cannot specify both Bbox and SpatialFilter.");
        }
    }

    private static IReadOnlyDictionary<string, object?> BuildSpatialGeometry(FeatureSpatialFilter spatialFilter)
    {
        var geometry = spatialFilter.Geometry;
        if (geometry.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Feature query spatial filter geometry must be a JSON object.", nameof(spatialFilter));
        }

        EnsureSpatialGeometryTypeMatches(spatialFilter);

        if (ProtoAdapter.UnwrapJsonValue(geometry) is IReadOnlyDictionary<string, object?> dictionary)
        {
            return dictionary;
        }

        throw new ArgumentException("Feature query spatial filter geometry must be a JSON object.", nameof(spatialFilter));
    }

    private static void EnsureSpatialGeometryTypeMatches(FeatureSpatialFilter spatialFilter)
    {
        if (spatialFilter.GeometryType is FeatureSpatialGeometryType.Unspecified)
        {
            return;
        }

        var inferred = InferSpatialGeometryType(spatialFilter.Geometry);
        if (inferred != spatialFilter.GeometryType)
        {
            throw new ArgumentException(
                "Feature query spatial filter geometry type does not match the geometry object.",
                nameof(spatialFilter));
        }
    }

    private static FeatureSpatialGeometryType InferSpatialGeometryType(JsonElement geometry)
    {
        if (geometry.TryGetProperty("x", out _) && geometry.TryGetProperty("y", out _))
        {
            return FeatureSpatialGeometryType.Point;
        }

        if (geometry.TryGetProperty("xmin", out _) &&
            geometry.TryGetProperty("ymin", out _) &&
            geometry.TryGetProperty("xmax", out _) &&
            geometry.TryGetProperty("ymax", out _))
        {
            return FeatureSpatialGeometryType.Envelope;
        }

        if (geometry.TryGetProperty("points", out _))
        {
            return FeatureSpatialGeometryType.MultiPoint;
        }

        if (geometry.TryGetProperty("paths", out _))
        {
            return FeatureSpatialGeometryType.Polyline;
        }

        if (geometry.TryGetProperty("rings", out _))
        {
            return FeatureSpatialGeometryType.Polygon;
        }

        throw new ArgumentException(
            "Feature query spatial filter geometry type could not be inferred from the geometry object.",
            nameof(geometry));
    }

    private static Models.SpatialRelationship ToGrpcSpatialRelationship(FeatureSpatialRelationship relationship)
        => relationship switch
        {
            FeatureSpatialRelationship.Unspecified => Models.SpatialRelationship.Intersects,
            FeatureSpatialRelationship.Intersects => Models.SpatialRelationship.Intersects,
            FeatureSpatialRelationship.Within => Models.SpatialRelationship.Within,
            FeatureSpatialRelationship.Contains => Models.SpatialRelationship.Contains,
            FeatureSpatialRelationship.EnvelopeIntersects => Models.SpatialRelationship.EnvelopeIntersects,
            FeatureSpatialRelationship.Crosses => Models.SpatialRelationship.Crosses,
            FeatureSpatialRelationship.Touches => Models.SpatialRelationship.Touches,
            FeatureSpatialRelationship.Overlaps => Models.SpatialRelationship.Overlaps,
            FeatureSpatialRelationship.IndexIntersects => throw new NotSupportedException(
                "gRPC shared spatial filters do not support index-intersects relationships."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(relationship),
                relationship,
                "Feature spatial relationship is not supported by gRPC shared queries."),
        };

    private static Models.SpatialReference? ParseSpatialReference(string? crs)
    {
        if (string.IsNullOrWhiteSpace(crs))
        {
            return null;
        }

        var trimmed = crs.Trim();
        if (IsCrs84(trimmed))
        {
            return new Models.SpatialReference { Wkid = 4326 };
        }

        if (int.TryParse(trimmed, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var bareWkid))
        {
            return new Models.SpatialReference { Wkid = bareWkid };
        }

        var separatorIndex = Math.Max(trimmed.LastIndexOf(':'), trimmed.LastIndexOf('/'));
        if (separatorIndex >= 0 &&
            int.TryParse(
                trimmed[(separatorIndex + 1)..],
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var wkid))
        {
            return new Models.SpatialReference { Wkid = wkid };
        }

        return new Models.SpatialReference { Wkt = trimmed };
    }

    private static bool IsCrs84(string crs) =>
        string.Equals(crs, "CRS84", StringComparison.OrdinalIgnoreCase) ||
        crs.EndsWith("/CRS84", StringComparison.OrdinalIgnoreCase) ||
        crs.EndsWith(":CRS84", StringComparison.OrdinalIgnoreCase);

    private FeatureQueryResult ToFeatureQueryResult(
        Models.QueryFeaturesResponse response,
        bool countRequested = false)
    {
        return new FeatureQueryResult
        {
            ProviderName = ProviderName,
            Features = response.Features.Select(feature => ToFeatureRecord(feature)).ToList(),
            NumberMatched = countRequested || response.Count > 0 ? response.Count : null,
            NumberReturned = response.Features.Count,
            ObjectIds = response.ObjectIds,
            Extent = response.Extent is not null ? ToFeatureBoundingBox(response.Extent) : null,
            HasMoreResults = response.ExceededTransferLimit,
            ObjectIdFieldName = response.ObjectIdFieldName,
        };
    }

    private FeatureQueryResult ToFeatureQueryResult(Models.FeaturePage page)
    {
        return new FeatureQueryResult
        {
            ProviderName = ProviderName,
            Features = page.Features.Select(feature => ToFeatureRecord(feature)).ToList(),
            NumberReturned = page.Features.Count,
            HasMoreResults = !page.IsLastPage,
            ObjectIdFieldName = string.IsNullOrWhiteSpace(page.ObjectIdFieldName) ? null : page.ObjectIdFieldName,
        };
    }

    private static FeatureBoundingBox ToFeatureBoundingBox(Models.Extent extent)
    {
        return new FeatureBoundingBox
        {
            MinX = extent.Xmin,
            MinY = extent.Ymin,
            MaxX = extent.Xmax,
            MaxY = extent.Ymax,
            Crs = extent.SpatialReference?.Wkid > 0
                ? string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"EPSG:{extent.SpatialReference.Wkid}")
                : null,
        };
    }

    private FeatureEditResponse ToFeatureEditResponse(Models.ApplyEditsResponse response)
    {
        return new FeatureEditResponse
        {
            ProviderName = ProviderName,
            AddResults = response.AddResults.Select(ToFeatureEditResult).ToList(),
            UpdateResults = response.UpdateResults.Select(ToFeatureEditResult).ToList(),
            DeleteResults = response.DeleteResults.Select(ToFeatureEditResult).ToList(),
            Error = response.Error is not null ? ToFeatureEditError(response.Error) : null,
        };
    }

    private static FeatureEditResult ToFeatureEditResult(Models.EditResult result)
    {
        var id = result.ObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new FeatureEditResult
        {
            Id = id,
            ObjectId = result.ObjectId,
            Succeeded = result.Success,
            Error = result.Error is not null ? ToFeatureEditError(result.Error) : null,
        };
    }

    private static FeatureEditError ToFeatureEditError(Models.EditError error)
    {
        return new FeatureEditError
        {
            Code = error.Code,
            Message = error.Message,
        };
    }

    private static FeatureRecord ToFeatureRecord(Models.Feature feature)
    {
        JsonElement? geometry = feature.Geometry is not null
            ? JsonSerializer.SerializeToElement(feature.Geometry, FeatureJsonOptions)
            : null;

        return new FeatureRecord
        {
            Id = feature.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Attributes = feature.Attributes.ToDictionary(
                kvp => kvp.Key,
                kvp => ToJsonElement(kvp.Value)),
            Geometry = geometry,
        };
    }

    private static JsonElement ToJsonElement(object? value)
    {
        return value is null
            ? JsonSerializer.SerializeToElement<object?>(null, FeatureJsonOptions)
            : JsonSerializer.SerializeToElement(value, value.GetType(), FeatureJsonOptions);
    }
}
