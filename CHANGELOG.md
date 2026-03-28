# Changelog

All notable changes to the Honua .NET SDK will be documented in this file.

## [0.1.0-alpha.1] - Unreleased

### Added

- Admin client SDK (`Honua.Sdk.Admin`) for managing services, layers, and configuration
- gRPC client SDK (`Honua.Sdk.Grpc`) for FeatureService queries and edits
- WFS 2.0 read/query client SDK (`Honua.Sdk.Wfs`) for GetCapabilities, GetFeature, DescribeFeatureType
- DI registration extensions (`AddHonuaGrpc`, `AddHonuaAdmin`, `AddHonuaGeocoding`, `AddHonuaWfs`)
- Typed request/response models for feature queries
- Automatic retry with exponential backoff and jitter for gRPC and WFS clients
