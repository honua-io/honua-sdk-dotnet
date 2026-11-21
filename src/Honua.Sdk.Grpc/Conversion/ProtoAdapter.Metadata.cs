// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc.Conversion;

internal static partial class ProtoAdapter
{
    public static Proto.DescribeServiceRequest ToProtoDescribeServiceRequest(Models.DescribeServiceRequest request)
    {
        return new Proto.DescribeServiceRequest
        {
            ServiceId = request.ServiceId,
            PageSize = request.PageSize,
            PageToken = request.PageToken ?? string.Empty,
        };
    }

    public static Models.DescribeServiceResponse FromProtoDescribeServiceResponse(Proto.DescribeServiceResponse response)
    {
        return new Models.DescribeServiceResponse
        {
            Service = response.Service is not null ? ConvertServiceDescriptor(response.Service) : null,
            Layers = response.Layers.Select(ConvertLayerSummary).ToList(),
            NextPageToken = response.NextPageToken,
        };
    }

    public static Proto.DescribeLayerRequest ToProtoDescribeLayerRequest(Models.DescribeLayerRequest request)
    {
        return new Proto.DescribeLayerRequest
        {
            ServiceId = request.ServiceId,
            LayerId = request.LayerId,
        };
    }

    public static Models.DescribeLayerResponse FromProtoDescribeLayerResponse(Proto.DescribeLayerResponse response)
    {
        return new Models.DescribeLayerResponse
        {
            Layer = response.Layer is not null ? ConvertLayerDescriptor(response.Layer) : null,
        };
    }

    private static Models.GrpcServiceDescriptor ConvertServiceDescriptor(Proto.ServiceDescriptor descriptor)
    {
        return new Models.GrpcServiceDescriptor
        {
            ServiceId = descriptor.ServiceId,
            Name = descriptor.Name,
            Description = descriptor.Description,
            SpatialReference = descriptor.SpatialReference is not null ? ConvertSpatialReference(descriptor.SpatialReference) : null,
            Extent = descriptor.Extent is not null ? ConvertExtent(descriptor.Extent) : null,
            Capabilities = descriptor.Capabilities is not null ? ConvertCapabilities(descriptor.Capabilities) : null,
            MaxRecordCount = descriptor.MaxRecordCount,
        };
    }

    private static Models.LayerSummary ConvertLayerSummary(Proto.LayerSummary summary)
    {
        return new Models.LayerSummary
        {
            LayerId = summary.LayerId,
            Name = summary.Name,
            Description = summary.Description,
            GeometryType = (Models.GeometryType)summary.GeometryType,
        };
    }

    private static Models.LayerDescriptor ConvertLayerDescriptor(Proto.LayerDescriptor layer)
    {
        return new Models.LayerDescriptor
        {
            ServiceId = layer.ServiceId,
            LayerId = layer.LayerId,
            Name = layer.Name,
            Description = layer.Description,
            GeometryType = (Models.GeometryType)layer.GeometryType,
            SpatialReference = layer.SpatialReference is not null ? ConvertSpatialReference(layer.SpatialReference) : null,
            Extent = layer.Extent is not null ? ConvertExtent(layer.Extent) : null,
            ObjectIdField = layer.ObjectIdField,
            GlobalIdField = layer.GlobalIdField,
            DisplayField = layer.DisplayField,
            Fields = layer.Fields.Select(ConvertLayerField).ToList(),
            Subtypes = layer.Subtypes is not null ? ConvertSubtypes(layer.Subtypes) : null,
            Templates = layer.Templates.Select(ConvertTemplate).ToList(),
            Relationships = layer.Relationships.Select(ConvertRelationship).ToList(),
            Capabilities = layer.Capabilities is not null ? ConvertCapabilities(layer.Capabilities) : null,
            MaxRecordCount = layer.MaxRecordCount,
            HasZ = layer.HasZ,
            HasM = layer.HasM,
            TimeInfo = layer.TimeInfo is not null ? ConvertTimeInfo(layer.TimeInfo) : null,
            EditingInfo = layer.EditingInfo is not null ? ConvertEditingInfo(layer.EditingInfo) : null,
            AttachmentLimits = layer.AttachmentLimits is not null ? ConvertAttachmentLimits(layer.AttachmentLimits) : null,
        };
    }

    private static Models.LayerFieldDescriptor ConvertLayerField(Proto.LayerFieldDescriptor field)
    {
        var hasDefault = field.DefaultValue is not null &&
            field.DefaultValue.ValueCase != Proto.AttributeValue.ValueOneofCase.None;
        return new Models.LayerFieldDescriptor
        {
            Definition = field.Definition is not null ? ConvertField(field.Definition) : new Models.FieldDefinition(),
            Editable = field.HasEditable ? field.Editable : null,
            HasDefaultValue = hasDefault,
            DefaultValue = hasDefault ? ConvertAttribute(field.DefaultValue!) : null,
            Domain = field.Domain is not null ? ConvertFieldDomain(field.Domain) : null,
            Precision = field.HasPrecision ? field.Precision : null,
            Scale = field.HasScale ? field.Scale : null,
        };
    }

    private static Models.FieldDomain ConvertFieldDomain(Proto.FieldDomain domain)
    {
        return new Models.FieldDomain
        {
            Name = domain.Name,
            CodedValues = domain.ConstraintCase == Proto.FieldDomain.ConstraintOneofCase.CodedValues
                ? domain.CodedValues.Values.Select(ConvertCodedValue).ToList()
                : null,
            Range = domain.ConstraintCase == Proto.FieldDomain.ConstraintOneofCase.Range
                ? new Models.RangeDomain
                {
                    Minimum = domain.Range.Minimum is not null ? ConvertAttribute(domain.Range.Minimum) : null,
                    Maximum = domain.Range.Maximum is not null ? ConvertAttribute(domain.Range.Maximum) : null,
                }
                : null,
            MergePolicy = domain.MergePolicy,
            SplitPolicy = domain.SplitPolicy,
        };
    }

    private static Models.DomainCodedValue ConvertCodedValue(Proto.DomainCodedValue coded)
    {
        return new Models.DomainCodedValue
        {
            Code = coded.Code is not null ? ConvertAttribute(coded.Code) : null,
            Name = coded.Name,
            Description = coded.Description,
        };
    }

    private static Models.SubtypeDefinition ConvertSubtypes(Proto.SubtypeDefinition subtypes)
    {
        return new Models.SubtypeDefinition
        {
            Field = subtypes.Field,
            DefaultCode = subtypes.DefaultCode is not null ? ConvertAttribute(subtypes.DefaultCode) : null,
            Subtypes = subtypes.Subtypes.Select(ConvertSubtype).ToList(),
        };
    }

    private static Models.LayerSubtype ConvertSubtype(Proto.LayerSubtype subtype)
    {
        var overrides = new Dictionary<string, Models.SubtypeFieldOverride>(subtype.FieldOverrides.Count);
        foreach (var kvp in subtype.FieldOverrides)
        {
            overrides[kvp.Key] = ConvertSubtypeFieldOverride(kvp.Value);
        }

        return new Models.LayerSubtype
        {
            Code = subtype.Code is not null ? ConvertAttribute(subtype.Code) : null,
            Name = subtype.Name,
            FieldOverrides = overrides,
            Templates = subtype.Templates.Select(ConvertTemplate).ToList(),
        };
    }

    private static Models.SubtypeFieldOverride ConvertSubtypeFieldOverride(Proto.SubtypeFieldOverride fieldOverride)
    {
        var hasDefault = fieldOverride.DefaultValue is not null &&
            fieldOverride.DefaultValue.ValueCase != Proto.AttributeValue.ValueOneofCase.None;
        return new Models.SubtypeFieldOverride
        {
            HasDefaultValue = hasDefault,
            DefaultValue = hasDefault ? ConvertAttribute(fieldOverride.DefaultValue!) : null,
            Domain = fieldOverride.DomainOverrideCase == Proto.SubtypeFieldOverride.DomainOverrideOneofCase.Domain
                ? ConvertFieldDomain(fieldOverride.Domain)
                : null,
            ClearDomain = fieldOverride.DomainOverrideCase == Proto.SubtypeFieldOverride.DomainOverrideOneofCase.ClearDomain &&
                fieldOverride.ClearDomain,
        };
    }

    private static Models.FeatureTemplate ConvertTemplate(Proto.FeatureTemplate template)
    {
        var defaults = new Dictionary<string, object?>(template.DefaultAttributes.Count);
        foreach (var kvp in template.DefaultAttributes)
        {
            defaults[kvp.Key] = ConvertAttribute(kvp.Value);
        }

        return new Models.FeatureTemplate
        {
            Name = template.Name,
            Description = template.Description,
            DefaultAttributes = defaults,
        };
    }

    private static Models.RelationshipDescriptor ConvertRelationship(Proto.RelationshipDescriptor relationship)
    {
        return new Models.RelationshipDescriptor
        {
            RelationshipId = relationship.RelationshipId,
            Name = relationship.Name,
            RelatedLayerId = relationship.RelatedLayerId,
            OriginKeyField = relationship.OriginKeyField,
            DestinationKeyField = relationship.DestinationKeyField,
            Cardinality = (Models.RelationshipCardinality)relationship.Cardinality,
            Role = (Models.RelationshipRole)relationship.Role,
            Composite = relationship.HasComposite ? relationship.Composite : null,
            Description = relationship.Description,
        };
    }

    private static Models.FeatureServiceCapabilities ConvertCapabilities(Proto.FeatureCapabilities capabilities)
    {
        return new Models.FeatureServiceCapabilities
        {
            Query = capabilities.Query,
            QueryStream = capabilities.QueryStream,
            Create = capabilities.Create,
            Update = capabilities.Update,
            Delete = capabilities.Delete,
            QueryRelated = capabilities.QueryRelated,
            ApplyServiceEdits = capabilities.ApplyServiceEdits,
            UseGlobalIds = capabilities.UseGlobalIds,
            ListAttachments = capabilities.ListAttachments,
            UploadAttachments = capabilities.UploadAttachments,
            DeleteAttachments = capabilities.DeleteAttachments,
            QueryTime = capabilities.QueryTime,
            QueryStatistics = capabilities.QueryStatistics,
            QueryDistinct = capabilities.QueryDistinct,
            QueryOrderBy = capabilities.QueryOrderBy,
            QueryPagination = capabilities.QueryPagination,
        };
    }

    private static Models.LayerTimeInfo ConvertTimeInfo(Proto.LayerTimeInfo timeInfo)
    {
        return new Models.LayerTimeInfo
        {
            StartField = timeInfo.StartField,
            EndField = timeInfo.EndField,
            TimeZone = timeInfo.TimeZone,
            RespectsDaylightSaving = timeInfo.HasRespectsDaylightSaving ? timeInfo.RespectsDaylightSaving : null,
            ExtentStartUtcMs = timeInfo.HasExtentStartUtcMs ? timeInfo.ExtentStartUtcMs : null,
            ExtentEndUtcMs = timeInfo.HasExtentEndUtcMs ? timeInfo.ExtentEndUtcMs : null,
            DefaultInterval = timeInfo.HasDefaultInterval ? timeInfo.DefaultInterval : null,
            IntervalUnit = (Models.TimeIntervalUnit)timeInfo.IntervalUnit,
        };
    }

    private static Models.LayerEditingInfo ConvertEditingInfo(Proto.LayerEditingInfo editingInfo)
    {
        return new Models.LayerEditingInfo
        {
            CreatorField = editingInfo.CreatorField,
            CreatedAtField = editingInfo.CreatedAtField,
            EditorField = editingInfo.EditorField,
            EditedAtField = editingInfo.EditedAtField,
            EditorTrackingEnabled = editingInfo.HasEditorTrackingEnabled ? editingInfo.EditorTrackingEnabled : null,
            OwnershipAccessControlEnabled = editingInfo.HasOwnershipAccessControlEnabled
                ? editingInfo.OwnershipAccessControlEnabled
                : null,
            AllowUpdateOthers = editingInfo.HasAllowUpdateOthers ? editingInfo.AllowUpdateOthers : null,
            AllowDeleteOthers = editingInfo.HasAllowDeleteOthers ? editingInfo.AllowDeleteOthers : null,
            LastEditUtcMs = editingInfo.HasLastEditUtcMs ? editingInfo.LastEditUtcMs : null,
        };
    }

    private static Models.AttachmentLimits ConvertAttachmentLimits(Proto.AttachmentLimits limits)
    {
        return new Models.AttachmentLimits
        {
            MaxFileBytes = limits.MaxFileBytes,
            MaxAttachmentsPerFeature = limits.MaxAttachmentsPerFeature,
            MaxTotalBytesPerFeature = limits.MaxTotalBytesPerFeature,
            AllowedContentTypes = limits.AllowedContentTypes.ToList(),
            MaxUnaryUploadBytes = limits.MaxUnaryUploadBytes,
            MaxChunkBytes = limits.MaxChunkBytes,
        };
    }
}
