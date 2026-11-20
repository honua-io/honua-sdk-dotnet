// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Grpc.Core;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Grpc.Conversion;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc;

public sealed partial class HonuaGrpcClient
{
    /// <inheritdoc />
    public Task<Models.DescribeServiceResponse> DescribeServiceAsync(
        Models.DescribeServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var protoRequest = ProtoAdapter.ToProtoDescribeServiceRequest(request);
        return ExecuteUnaryAsync(
            new UnaryRpcSpec<Proto.DescribeServiceResponse, Models.DescribeServiceResponse>(
                "DescribeService",
                (metadata, deadline, token) => _client.DescribeServiceAsync(protoRequest, metadata, deadline, token),
                ProtoAdapter.FromProtoDescribeServiceResponse),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Models.DescribeLayerResponse> DescribeLayerAsync(
        Models.DescribeLayerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var protoRequest = ProtoAdapter.ToProtoDescribeLayerRequest(request);
        return ExecuteUnaryAsync(
            new UnaryRpcSpec<Proto.DescribeLayerResponse, Models.DescribeLayerResponse>(
                "DescribeLayer",
                (metadata, deadline, token) => _client.DescribeLayerAsync(protoRequest, metadata, deadline, token),
                ProtoAdapter.FromProtoDescribeLayerResponse),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SourceDescriptor> GetDescriptorAsync(
        SourceDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var locator = descriptor.Locator;
        if (string.IsNullOrWhiteSpace(locator.ServiceId) || !locator.LayerId.HasValue)
        {
            throw new ArgumentException(
                "gRPC descriptor discovery requires SourceDescriptor.Locator.ServiceId and LayerId.",
                nameof(descriptor));
        }

        try
        {
            var layerResponse = await DescribeLayerAsync(
                new Models.DescribeLayerRequest
                {
                    ServiceId = locator.ServiceId,
                    LayerId = locator.LayerId.Value,
                },
                cancellationToken).ConfigureAwait(false);
            if (layerResponse.Layer is { } layer)
            {
                return descriptor with
                {
                    Capabilities = BuildDiscoveredCapabilities(layer.Capabilities),
                    Schema = BuildSourceSchema(layer),
                };
            }
        }
        catch (HonuaGrpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
        {
        }

        return await GetDescriptorViaQueryFallbackAsync(descriptor, locator, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SourceDescriptor> GetDescriptorViaQueryFallbackAsync(
        SourceDescriptor descriptor,
        SourceLocator locator,
        CancellationToken cancellationToken)
    {
        var schemaResponse = await QueryFeaturesAsync(
            new Models.QueryFeaturesRequest
            {
                ServiceId = locator.ServiceId!,
                LayerId = locator.LayerId!.Value,
                Where = "1=1",
                ReturnGeometry = false,
                ResultRecordCount = 0,
            },
            cancellationToken).ConfigureAwait(false);
        var extentResponse = await QueryFeaturesAsync(
            new Models.QueryFeaturesRequest
            {
                ServiceId = locator.ServiceId!,
                LayerId = locator.LayerId!.Value,
                Where = "1=1",
                ReturnGeometry = false,
                ReturnExtentOnly = true,
            },
            cancellationToken).ConfigureAwait(false);

        return descriptor with
        {
            Capabilities = BuildDiscoveredCapabilities(),
            Schema = BuildSourceSchema(schemaResponse, extentResponse.Extent),
        };
    }

    private static List<string> BuildDiscoveredCapabilities(Models.FeatureServiceCapabilities? capabilities)
    {
        if (capabilities is null)
        {
            return BuildDiscoveredCapabilities();
        }

        var discovered = new HashSet<string>(BuildDiscoveredCapabilities(), StringComparer.Ordinal);
        if (capabilities.QueryRelated)
        {
            discovered.Add(FeatureCapabilities.QueryRelated);
        }

        if (capabilities.ListAttachments || capabilities.UploadAttachments || capabilities.DeleteAttachments)
        {
            discovered.Add(FeatureCapabilities.Attachments);
        }

        return FeatureCapabilities.All.Where(discovered.Contains).ToList();
    }

    private static SourceSchema BuildSourceSchema(Models.LayerDescriptor layer)
    {
        var objectIdField = string.IsNullOrWhiteSpace(layer.ObjectIdField) ? null : layer.ObjectIdField;
        var globalIdField = string.IsNullOrWhiteSpace(layer.GlobalIdField) ? null : layer.GlobalIdField;
        return new SourceSchema
        {
            Fields = layer.Fields.Select(ToSourceField).ToList(),
            PrimaryKey = objectIdField,
            ObjectIdField = objectIdField,
            GlobalIdField = globalIdField,
            GeometryType = ToSourceGeometryType(layer.GeometryType),
            Extent = layer.Extent is not null ? ToFeatureBoundingBox(layer.Extent) : null,
            SpatialReference = FormatSpatialReference(layer.SpatialReference),
            TimeField = string.IsNullOrWhiteSpace(layer.TimeInfo?.StartField) ? null : layer.TimeInfo.StartField,
            TimeInfo = ToSourceTimeInfo(layer.TimeInfo),
            EditCapabilities = ToFeatureEditCapabilities(layer.Capabilities),
            AttachmentCapabilities = ToFeatureAttachmentCapabilities(layer.Capabilities),
        };
    }

    private static SourceField ToSourceField(Models.LayerFieldDescriptor field)
    {
        var def = field.Definition;
        return new SourceField
        {
            Name = def.Name,
            Alias = string.IsNullOrWhiteSpace(def.Alias) ? null : def.Alias,
            Type = def.FieldType.ToString(),
            Nullable = def.Nullable,
            Length = def.Length > 0 ? def.Length : null,
            Editable = field.Editable,
            Required = !def.Nullable,
        };
    }

    private static SourceTimeInfo? ToSourceTimeInfo(Models.LayerTimeInfo? timeInfo)
    {
        if (timeInfo is null)
        {
            return null;
        }

        return new SourceTimeInfo
        {
            StartTimeField = string.IsNullOrWhiteSpace(timeInfo.StartField) ? null : timeInfo.StartField,
            EndTimeField = string.IsNullOrWhiteSpace(timeInfo.EndField) ? null : timeInfo.EndField,
            TimeReference = string.IsNullOrWhiteSpace(timeInfo.TimeZone) ? null : timeInfo.TimeZone,
        };
    }

    private static FeatureEditCapabilities ToFeatureEditCapabilities(Models.FeatureServiceCapabilities? capabilities)
    {
        if (capabilities is null)
        {
            return GrpcEditCapabilities;
        }

        return new FeatureEditCapabilities
        {
            SupportsAdds = capabilities.Create,
            SupportsUpdates = capabilities.Update,
            SupportsDeletes = capabilities.Delete,
            SupportsRollbackOnFailure = capabilities.Create || capabilities.Update || capabilities.Delete,
            NativeSurface = GrpcEditCapabilities.NativeSurface,
        };
    }

    private static FeatureAttachmentCapabilities ToFeatureAttachmentCapabilities(Models.FeatureServiceCapabilities? capabilities)
    {
        if (capabilities is null)
        {
            return GrpcAttachmentCapabilities;
        }

        return new FeatureAttachmentCapabilities
        {
            SupportsList = capabilities.ListAttachments,
            SupportsDownload = false,
            SupportsAdd = capabilities.UploadAttachments,
            SupportsUpdate = capabilities.UploadAttachments,
            SupportsDelete = capabilities.DeleteAttachments,
            NativeSurface = GrpcAttachmentCapabilities.NativeSurface,
        };
    }
}
