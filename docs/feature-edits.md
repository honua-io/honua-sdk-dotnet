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
| WFS | Not yet | WFS-T decision tracked by #35 |
| OGC API Features | Not yet | Transaction/create-update-delete decision tracked by #35 |
| Admin | Not applicable | Admin has control-plane mutations, not data-plane feature edits |

Unsupported read providers are not registered as `IHonuaFeatureEditClient`
implementations. Select from `IEnumerable<IHonuaFeatureEditClient>` or inspect
`EditCapabilities` before attempting a write.

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
