// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Grpc.Conversion;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Grpc;

/// <summary>
/// gRPC client for the Honua FeatureService.
/// </summary>
public sealed class HonuaGrpcClient : IHonuaGrpcClient, IHonuaFeatureQueryClient, IHonuaFeatureEditClient, IDisposable
{
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

    private readonly Honua.Server.Features.Grpc.Proto.FeatureService.FeatureServiceClient _client;
    private readonly GrpcChannel? _ownedChannel;
    private readonly HonuaGrpcClientOptions _options;
    private readonly Metadata? _metadataOverride;

    /// <summary>
    /// Creates a new gRPC client using the provided options.
    /// </summary>
    /// <param name="options">Configuration options for the client.</param>
    public HonuaGrpcClient(IOptions<HonuaGrpcClientOptions> options)
    {
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

        _ownedChannel = GrpcChannel.ForAddress(address, channelOptions);
        _client = new Honua.Server.Features.Grpc.Proto.FeatureService.FeatureServiceClient(_ownedChannel);
        _options = opts;
    }

    /// <summary>
    /// Creates a new gRPC client using a pre-configured channel.
    /// </summary>
    /// <param name="channel">The gRPC channel to use.</param>
    /// <param name="options">Optional client options for authentication.</param>
    public HonuaGrpcClient(GrpcChannel channel, HonuaGrpcClientOptions? options = null)
    {
        var opts = options ?? new HonuaGrpcClientOptions();
        HonuaGrpcClientOptions.ValidateTimeout(opts.Timeout);
        if (HasCredentials(opts))
        {
            ValidateAuthenticationTransport(opts, ResolveChannelAddress(channel));
        }

        _client = new Honua.Server.Features.Grpc.Proto.FeatureService.FeatureServiceClient(channel);
        _options = opts;
    }

    // For testing - inject the generated client stub directly
    internal HonuaGrpcClient(Honua.Server.Features.Grpc.Proto.FeatureService.FeatureServiceClient client, Metadata? metadata = null)
    {
        _client = client;
        _options = new HonuaGrpcClientOptions();
        _metadataOverride = metadata ?? new Metadata();
    }

    // For testing - inject the generated client stub directly with live options
    internal HonuaGrpcClient(Honua.Server.Features.Grpc.Proto.FeatureService.FeatureServiceClient client, HonuaGrpcClientOptions options)
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
    public async Task<Models.QueryFeaturesResponse> QueryFeaturesAsync(
        Models.QueryFeaturesRequest request, CancellationToken ct = default)
    {
        var protoRequest = ProtoAdapter.ToProtoRequest(request);
        try
        {
            var metadata = await BuildMetadataAsync(ct).ConfigureAwait(false);
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
        var response = await QueryFeaturesAsync(BuildGrpcQuery(request), ct).ConfigureAwait(false);
        return ToFeatureQueryResult(response);
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
        var protoRequest = ProtoAdapter.ToProtoApplyEditsRequest(request);
        try
        {
            var metadata = await BuildMetadataAsync(ct).ConfigureAwait(false);
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
    public async IAsyncEnumerable<Models.FeaturePage> QueryFeaturesStreamAsync(
        Models.QueryFeaturesRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var protoRequest = ProtoAdapter.ToProtoRequest(request);
        var metadata = await BuildMetadataAsync(ct).ConfigureAwait(false);
        var call = _client.QueryFeaturesStream(
            protoRequest,
            metadata,
            deadline: CreateDeadline(),
            cancellationToken: ct);
        try
        {
            while (true)
            {
                Honua.Server.Features.Grpc.Proto.FeaturePage protoPage;
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
                Names = { MethodName.Default },
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

    private async Task<Metadata> BuildMetadataAsync(CancellationToken cancellationToken)
    {
        if (_metadataOverride is not null)
        {
            return _metadataOverride;
        }

        var metadata = new Metadata();
        var apiKey = await ResolveApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(apiKey))
        {
            metadata.Add("x-api-key", apiKey);
        }

        var bearerToken = await ResolveBearerTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(bearerToken))
        {
            metadata.Add("authorization", $"Bearer {bearerToken}");
        }

        if (_options.EnableCompressionNegotiation && !string.IsNullOrWhiteSpace(_options.AcceptedCompressionEncodings))
        {
            metadata.Add("grpc-accept-encoding", _options.AcceptedCompressionEncodings);
        }

        return metadata;
    }

    private DateTime CreateDeadline()
        => DateTime.UtcNow.Add(_options.Timeout);

    private Task<string?> ResolveApiKeyAsync(CancellationToken cancellationToken)
        => _options.ApiKeyProvider is { } provider
            ? provider(cancellationToken)
            : Task.FromResult(_options.ApiKey);

    private Task<string?> ResolveBearerTokenAsync(CancellationToken cancellationToken)
        => _options.BearerTokenProvider is { } provider
            ? provider(cancellationToken)
            : Task.FromResult(_options.BearerToken);

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
        => !string.IsNullOrWhiteSpace(opts.ApiKey) ||
           !string.IsNullOrWhiteSpace(opts.BearerToken) ||
           opts.ApiKeyProvider is not null ||
           opts.BearerTokenProvider is not null;

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

    private static Models.QueryFeaturesRequest BuildGrpcQuery(FeatureQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureSupportedFilterLanguage(request.FilterLanguage);

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
            SpatialFilter = BuildSpatialFilter(request.Bbox),
        };
    }

    private static Models.ApplyEditsRequest BuildGrpcEditRequest(FeatureEditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

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

    private static IReadOnlyList<long> ResolveDeleteObjectIds(FeatureEditRequest request)
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

    private static Models.SpatialFilter? BuildSpatialFilter(FeatureBoundingBox? bbox)
    {
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

    private static Models.SpatialReference? ParseSpatialReference(string? crs)
    {
        if (string.IsNullOrWhiteSpace(crs))
        {
            return null;
        }

        var digits = new string(crs.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var wkid))
        {
            return new Models.SpatialReference { Wkid = wkid };
        }

        return new Models.SpatialReference { Wkt = crs };
    }

    private FeatureQueryResult ToFeatureQueryResult(Models.QueryFeaturesResponse response)
    {
        return new FeatureQueryResult
        {
            ProviderName = ProviderName,
            Features = response.Features.Select(feature => ToFeatureRecord(feature)).ToList(),
            NumberReturned = response.Features.Count,
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
