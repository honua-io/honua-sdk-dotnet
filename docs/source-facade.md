# Source Facade

`Honua.Sdk.Abstractions` includes an additive source-oriented facade over the
existing protocol clients. Native clients remain the implementation surface;
`IHonuaSource` gives application code one place to query, drain pages, ask for
feature IDs, apply supported edits, and reach the native client when a
protocol-specific method is needed.

## Core Types

- `SourceDescriptor` is the serializable source identity: `Id`, `Protocol`,
  `Locator`, declared `Capabilities`, optional `Schema`, and `Attribution`.
- `SourceLocator` carries protocol-specific addressing fields such as
  `ServiceId`, `LayerId`, `CollectionId`, and `TypeName`.
- `SourceQuery` is the source-oriented query request. `HonuaSource` maps it to
  the existing `FeatureQueryRequest` and fills `FeatureQueryRequest.Source`
  from the descriptor.
- `IHonuaSource` is the runtime handle with `QueryAsync()`,
  `QueryPagesAsync()`, `QueryAllAsync()`, `QueryObjectIdsAsync()`,
  `GetDescriptorAsync()`, `ApplyEditsAsync()`, and `Protocol<TClient>()`.

```csharp
var source = new HonuaSource(
    new SourceDescriptor
    {
        Id = "parks",
        Protocol = FeatureProtocolIds.GeoServicesFeatureService,
        Locator = new SourceLocator { ServiceId = "parks", LayerId = 0 }
    },
    queryClient,
    editClient,
    nativeClient);

var page = await source.QueryAsync(new SourceQuery
{
    Where = "status = 'open'",
    FilterLanguage = FeatureFilterLanguage.SqlWhere,
    Limit = 25,
});
```

`SourceQuery` also carries the shared high-value query facets: output fields,
order by, offset/count, distinct, count-only, IDs-only, extent-only, bounding
boxes, explicit geometry spatial filters, output CRS, time filters, statistics,
group-by fields, and having
clauses. Adapters map only the facets their backing protocol supports. Unsupported
facets fail with `NotSupportedException` instead of being ignored.

`GetDescriptorAsync()` asks a backing provider for source schema and capability
metadata when the client implements descriptor discovery. GeoServices maps layer
metadata directly; OGC API Features combines collection metadata with
`queryables`; WFS combines `GetCapabilities` with `DescribeFeatureType`; gRPC
uses feature query metadata plus an extent-only query because the current proto
does not expose a dedicated schema RPC.

## Protocol IDs

Use `FeatureProtocolIds` for stable protocol identifiers. The facade accepts
existing provider-name aliases and normalizes them to canonical IDs:

| Canonical ID | Common aliases |
| --- | --- |
| `grpc` | `grpc` |
| `geoservices-feature-service` | `geoservices-featureserver`, `featureserver`, `FeatureServer` |
| `ogc-features` | `ogc-api-features`, `ogcapi-features`, `OgcFeatures` |
| `wfs` | `wfs` |

Call `FeatureProtocolIds.Normalize()` or `FeatureProtocolIds.Matches()` when
persisted descriptors may contain older provider names.

## Capabilities

Use `FeatureCapabilities` for shared capability identifiers. The .NET facade
advertises query, statistics, extent, query-object-IDs, time-filter, spatial
relationship, stream/page iteration, edit, attachment, relationship, and offline
capabilities where the wrapped client or discovered metadata supports them.
`FeatureProtocolCapabilities` contains protocol defaults and helpers for
union/intersection when a caller is building a multi-source view.

`HonuaSource` intersects declared descriptor capabilities with runtime client
capabilities. For example, WFS can still be described as queryable while
`ApplyEditsAsync()` throws a clear `NotSupportedException` until WFS-T is
implemented.

## Native Escape Hatch

The facade does not replace protocol-native clients. Use `Protocol<TClient>()`
for operations outside the shared source surface:

```csharp
var featureServer = source.Protocol<IHonuaFeatureServerClient>(
    FeatureProtocolIds.GeoServicesFeatureServer);

var layerInfo = await featureServer!.GetLayerInfoAsync("parks", 0);
```
