# Client Behavior

This page documents the cross-cutting behavior that applies to the Honua SDK
clients: timeouts, retries, errors, pagination, and current endpoint coverage.

## Timeouts and cancellation

Every DI-registered client exposes a `Timeout` option. The default is 100
seconds, matching the .NET `HttpClient` default. The value must be greater than
10 milliseconds and less than 24 hours.

```csharp
builder.Services.AddHonuaGrpc(options =>
{
    options.Address = "https://localhost:5001";
    options.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHonuaWfs(options =>
{
    options.BaseAddress = new Uri("https://localhost:5001");
    options.Timeout = TimeSpan.FromSeconds(30);
});
```

For HTTP clients, `Timeout` is applied to the underlying `HttpClient`. When
automatic retry is enabled, it is also applied to the standard resilience
pipeline as both the total request timeout and the per-attempt timeout. For
gRPC, `Timeout` is converted to a per-call deadline. All public async methods
also accept a `CancellationToken`; use it for caller-driven cancellation.

## Retries

Retries are enabled by default and can be disabled per client with
`EnableRetry = false`. `MaxRetryAttempts` is clamped to the supported range of
2 to 5.

| Client family | Retried failures |
|---------------|------------------|
| gRPC | `Unavailable`, `Internal` |
| Admin, Geocoding, WFS, GeoServices, OGC API Features | HTTP `429`, `502`, `503` |

Authentication failures, validation errors, unsupported operations, parser
failures, and application-level protocol errors are not retried by the SDK.

## Error handling

Each package exposes protocol-specific exception types so callers can catch the
right failure surface without parsing strings.

| Package | Exception | Notes |
|---------|-----------|-------|
| `Honua.Sdk.Admin` | `HonuaAdminApiException` | Non-success HTTP status codes. Includes status code and response body. |
| `Honua.Sdk.Admin` | `HonuaAdminOperationException` | Successful HTTP responses that fail the expected Admin contract, such as null envelopes or compatibility failures. |
| `Honua.Sdk.Wfs` | `HonuaWfsException` | HTTP failures, OGC `ExceptionReport` responses, and content-format mismatches. Includes the OGC exception code when available. |
| `Honua.Sdk.GeoServices` | `HonuaFeatureServerException` | HTTP failures and GeoServices JSON error envelopes, including 200 responses that carry an error payload. |
| `Honua.Sdk.OgcFeatures` | `HonuaOgcFeaturesException` | HTTP failures, JSON contract failures, and rejected cross-origin next-page links. |
| `Honua.Sdk.Grpc` | `HonuaGrpcException` | Wraps `RpcException` and preserves the gRPC status code. |

`ArgumentNullException`, `ArgumentException`, `InvalidOperationException`, and
`NotSupportedException` are used for local input/configuration problems before
or instead of a remote request.

## Pagination

Native clients expose provider-specific pagination helpers, and read clients
also implement `IHonuaFeatureQueryClient.QueryPagesAsync` from
`Honua.Sdk.Abstractions` for provider-neutral paging.

| Client | Pagination behavior |
|--------|---------------------|
| WFS | `GetFeaturesAsyncEnumerable` advances `STARTINDEX`; shared queries advance `FeatureQueryRequest.Offset`. Paging stops when `numberMatched` is reached or a page is empty, with a 100-page safety limit. |
| GeoServices FeatureServer | `QueryPagesAsync` advances `resultOffset` while the server reports `exceededTransferLimit`. |
| OGC API Features | `GetItemsPagesAsync` follows same-origin `rel=next` links. Cross-origin next links are rejected. |
| gRPC | `QueryFeaturesStreamAsync` returns server-streamed pages until `IsLastPage`; `QueryPagesAsync` maps those pages to the shared abstraction. |

## Endpoint coverage

Current typed endpoint coverage is:

| Package | Covered surfaces |
|---------|------------------|
| `Honua.Sdk.Admin` | Service listing/settings/protocols, MapServer/access/time/layer metadata settings, metadata resources and manifests, version/capabilities/compatibility/config, secure connections/encryption, layer publishing/table discovery/styles, observability, migrations, deploy preflight/plans/operations, and geocoding. |
| `Honua.Sdk.Grpc` | Feature query, streaming feature query, and feature edits. |
| `Honua.Sdk.Wfs` | `GetCapabilities`, `DescribeFeatureType`, `GetFeature`, feature count via hits, custom output handlers, and auto-pagination. |
| `Honua.Sdk.GeoServices` | FeatureServer service/layer metadata, query, feature by object ID, count, IDs, extent, statistics, SQL validation, raw query, auto-pagination, layer edit capabilities, and applyEdits/add/update/delete feature edits. |
| `Honua.Sdk.OgcFeatures` | Landing page, conformance, collections, collection details, queryables, items, item by ID, raw item responses, and next-link pagination. |

Shared read queries are available through `IHonuaFeatureQueryClient` for gRPC,
WFS, GeoServices FeatureServer, and OGC API Features. Shared feature edit
capabilities are available through `IHonuaFeatureEditClient`; gRPC and
GeoServices FeatureServer currently advertise write support, while WFS and OGC
API Features report unsupported edit capabilities with a reason.
