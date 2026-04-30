# Feature Edits

Feature reads and feature writes are separate SDK surfaces. Read/query clients
implement `IHonuaFeatureQueryClient`; edit-capable clients implement
`IHonuaFeatureEditClient`.

## Shared Edit Abstraction

Use `IHonuaFeatureEditClient` from `Honua.Sdk.Abstractions` when application
code should apply edits without depending directly on one protocol package.

```csharp
using Honua.Sdk.Abstractions.Features;

IHonuaFeatureEditClient edits = featureEditClients
    .Single(c => c.ProviderName == "geoservices-featureserver");

var result = await edits.ApplyEditsAsync(new FeatureEditRequest
{
    Source = new FeatureSource { ServiceId = "parks", LayerId = 0 },
    Adds =
    [
        new FeatureEditFeature
        {
            Attributes = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("New Park"),
                ["status"] = JsonSerializer.SerializeToElement("open"),
            },
        }
    ],
    RollbackOnFailure = true,
}, ct);
```

The shared response preserves per-operation add, update, and delete results,
provider object IDs, per-feature errors, top-level batch errors, and a
`Succeeded` helper for whole-batch checks.

## Current Provider Support

| Provider | Shared edit support | Native edit support |
|----------|---------------------|---------------------|
| gRPC | Yes, via `IHonuaFeatureEditClient` | Yes, via `IHonuaGrpcClient.ApplyEditsAsync()` |
| GeoServices FeatureServer | Yes, via `IHonuaFeatureEditClient` | Yes, via `IHonuaFeatureServerEditClient.ApplyEditsAsync()` plus add/update/delete convenience methods |
| WFS | Registered as unsupported via `IHonuaFeatureEditClient` | WFS-T implementation decision tracked by #35 |
| OGC API Features | Yes, via `IHonuaFeatureEditClient` | Yes, via `IHonuaOgcFeaturesEditClient.CreateItemAsync()`/`UpdateItemAsync()`/`DeleteItemAsync()` |
| Admin | Not applicable | Admin has control-plane mutations, not data-plane feature edits |

Select from `IEnumerable<IHonuaFeatureEditClient>` and inspect
`EditCapabilities` before attempting a write. Unsupported feature providers
return `SupportsAdds`, `SupportsUpdates`, and `SupportsDeletes` as `false`,
populate `UnsupportedReason`, and throw `NotSupportedException` from
`ApplyEditsAsync`.

## Shared Attachment Abstraction

Use `IHonuaFeatureAttachmentClient` when application code needs provider-neutral
feature attachment operations. The shared contract supports listing attachment
metadata, downloading attachment streams, adding attachment content, updating
attachment content or metadata, and deleting attachments.

```csharp
using Honua.Sdk.Abstractions.Features;

IHonuaFeatureAttachmentClient attachments = attachmentClients
    .Single(c => c.ProviderName == "geoservices-featureserver");

var listed = await attachments.ListAttachmentsAsync(new FeatureAttachmentListRequest
{
    Source = new FeatureSource { ServiceId = "parks", LayerId = 0 },
    ObjectId = 42,
}, ct);

await using var file = File.OpenRead("photo.jpg");
var addResult = await attachments.AddAttachmentAsync(new FeatureAttachmentAddRequest
{
    Source = new FeatureSource { ServiceId = "parks", LayerId = 0 },
    ObjectId = 42,
    Name = "photo.jpg",
    ContentType = "image/jpeg",
    Content = file,
    Keywords = "field",
}, ct);
```

Dispose downloaded `FeatureAttachmentContent.Content` streams when finished.

| Provider | Shared attachment support | Native attachment support |
|----------|---------------------------|---------------------------|
| gRPC | Registered as unsupported via `IHonuaFeatureAttachmentClient` | Attachment RPCs are not exposed yet |
| GeoServices FeatureServer | Yes, via `IHonuaFeatureAttachmentClient` | Yes, via FeatureServer attachment endpoints |
| WFS | Registered as unsupported via `IHonuaFeatureAttachmentClient` | WFS does not expose attachment operations |
| OGC API Features | Registered as unsupported via `IHonuaFeatureAttachmentClient` | Not defined by this SDK surface |
| Admin | Not applicable | Admin has control-plane mutations, not data-plane feature attachments |

Select from `IEnumerable<IHonuaFeatureAttachmentClient>` and inspect
`AttachmentCapabilities` before attempting attachment operations. Unsupported
providers return all operation flags as `false`, populate `UnsupportedReason`,
and throw `NotSupportedException` from attachment methods.

## gRPC Notes

The gRPC shared adapter maps:

- `FeatureEditRequest.Source.ServiceId` and `LayerId` to the native
  `ApplyEditsRequest`.
- `Adds` and `Updates` to native feature payloads with JSON attributes and Esri
  JSON geometry objects.
- `DeleteObjectIds` and numeric `DeleteIds` to native delete object IDs.
- Native per-feature edit errors to shared `FeatureEditResult.Error` values.

gRPC updates and deletes require numeric feature IDs or object IDs. Non-numeric
IDs fail locally with `ArgumentException` before a remote call.

## GeoServices FeatureServer Notes

The FeatureServer shared adapter maps:

- `FeatureEditRequest.Source.ServiceId` and `LayerId` to
  `/rest/services/{serviceId}/FeatureServer/{layerId}/applyEdits`.
- `Adds` and `Updates` to GeoServices feature JSON payloads with `attributes`
  and optional Esri JSON `geometry`.
- `DeleteObjectIds` and numeric `DeleteIds` to GeoServices `deletes`.
- GeoServices per-feature edit errors to shared `FeatureEditResult.Error`
  values.

Native callers can use `ApplyEditsAsync`, `AddFeaturesAsync`,
`UpdateFeaturesAsync`, and `DeleteFeaturesAsync` on
`IHonuaFeatureServerEditClient`. Use `GetEditCapabilitiesAsync(serviceId, layerId)`
to inspect layer capabilities parsed from FeatureServer layer metadata before
attempting writes.

FeatureServer updates require the layer object ID field in each update
payload. When using the shared abstraction, the SDK injects the object ID field
from layer metadata when `FeatureEditFeature.ObjectId` or a numeric `Id` is
provided.

## OGC API Features Notes

The OGC API Features shared adapter maps:

- `FeatureEditRequest.Source.CollectionId` to
  `/ogc/features/collections/{collectionId}/items`.
- `Adds` to `POST` GeoJSON feature payloads.
- `Updates` to `PUT` GeoJSON feature payloads at
  `/items/{featureId}` using `FeatureEditFeature.Id` or `ObjectId`.
- `DeleteIds` and numeric `DeleteObjectIds` to `DELETE /items/{featureId}`.
- OGC problem details and HTTP errors to shared `FeatureEditResult.Error`
  values.

OGC API Features writes are item-level operations, not server-side edit
batches. Multi-operation shared edit requests must set
`RollbackOnFailure = false`; rollback-on-failure batches are rejected locally.
