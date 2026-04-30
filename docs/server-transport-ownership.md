# Server Transport Ownership

This document tracks the first SDK-side slice for
[`honua-sdk-dotnet#73`](https://github.com/honua-io/honua-sdk-dotnet/issues/73)
and the companion server cleanup
[`honua-server#854`](https://github.com/honua-io/honua-server/issues/854).

Canonical `.proto` definitions stay in
[`geospatial-grpc`](https://github.com/honua-io/geospatial-grpc). This SDK
consumes the generated `Geospatial.Grpc` package and owns reusable .NET clients,
SDK models, and converter fixtures.

## Inventory

| Server file | Classification | SDK replacement or target |
|-------------|----------------|---------------------------|
| `src/Honua.Core/Transport/Clients/GrpcFeatureServiceClient.cs` | Portable feature gRPC client wrapper | Replace consumer-facing use with `Honua.Sdk.Grpc.HonuaGrpcClient`, `IHonuaGrpcClient`, `IHonuaFeatureQueryClient`, and `IHonuaFeatureEditClient`. Keep server-only logging/retry wrappers in server only if they are part of server runtime wiring. |
| `src/Honua.Core/Transport/Clients/IFeatureServiceClient.cs` | Portable feature query/edit abstraction plus DTOs | Replace reusable client contracts with `Honua.Sdk.Abstractions.Features` and protocol-native `Honua.Sdk.Grpc.Models`. Keep server-domain query/edit models in server. |
| `src/Honua.Core/Transport/Clients/IFormServiceClient.cs` | Portable form client and field/form DTO sketch | Align reusable DTOs with `Honua.Sdk.Field`; keep form persistence, validation execution, collaboration runtime, and endpoint behavior in server. |
| `src/Honua.Core/Transport/Converters/AttributeConverter.cs` | Domain-to-protobuf adapter | SDK owns canonical SDK model to protobuf conversion through `HonuaGrpcProtoConverter`; server may keep domain-to-SDK mapping or server-domain adapters. |
| `src/Honua.Core/Transport/Converters/ExtentConverter.cs` | Domain-to-protobuf adapter | SDK owns SDK extent/protobuf parity in `Honua.Sdk.Grpc`; server-specific `FeatureExtent` mapping stays server-owned. |
| `src/Honua.Core/Transport/Converters/FeatureConverter.cs` | Server domain feature/query adapter | Keep server-domain query/edit pipeline mapping in server; use SDK fixtures to verify protocol parity. |
| `src/Honua.Core/Transport/Converters/GeometryConverter.cs` | Server NTS/WKB/protobuf adapter | Keep server WKB/NTS domain mapping in server until SDK geometry/NTS work in `honua-sdk-dotnet#55` provides reusable geometry contracts. |
| `src/Honua.Core/Transport/Converters/SpatialFilterConverter.cs` | Server domain spatial filter adapter | Keep server-domain mapping in server; SDK owns SDK spatial filter to protobuf conversion. |
| `src/Honua.Core/Transport/Converters/SpatialReferenceConverter.cs` | Domain-to-protobuf adapter | SDK owns SDK spatial reference to protobuf conversion; server-specific `Honua.Core.Models.SpatialReference` mapping stays server-owned. |
| `src/Honua.Core/Transport/Converters/StatisticDefinitionConverter.cs` | Server domain statistics adapter | Keep server-domain statistics mapping in server; SDK owns SDK query statistics to protobuf conversion. |
| `src/Honua.Core/Transport/Proto/geospatial/v1/*.proto` | Generated/snapshot protocol material | Remove as canonical source after server consumes `Geospatial.Grpc`; protocol changes start in `geospatial-grpc`. |

## SDK Surface Added

- `Honua.Sdk.Grpc.Conversion.HonuaGrpcProtoConverter` exposes the stable SDK
  model to `geospatial.v1` protobuf conversion boundary without making server
  or mobile copy internal SDK adapter code.
- `tests/Honua.Sdk.Grpc.Tests/Fixtures/Json/*.json` are the first shared
  gRPC contract fixtures for feature queries, feature edits, spatial reference
  handling, datetime attributes, and edit errors.

## Remaining Work

- Replace server-local portable feature client usage with the published
  `Honua.Sdk.Grpc` NuGet package after `honua-server#854` lands on trunk.
- Align server-local form client DTOs with `Honua.Sdk.Field`; do not treat the
  server-local `IFormServiceClient<T>` as the long-term public contract.
- Add server-side fixture tests that parse the same JSON fixtures or equivalent
  protobuf payloads and compare server protocol adapters with the SDK converter
  behavior.
