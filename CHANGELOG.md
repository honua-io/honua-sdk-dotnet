# Changelog

All notable changes to the Honua .NET SDK will be documented in this file.

## [0.1.0-alpha.1] - Unreleased

### Added

- Admin client SDK (`Honua.Sdk.Admin`) for managing services, layers, and configuration
- gRPC client SDK (`Honua.Sdk.Grpc`) for FeatureService queries and edits
- DI registration extensions (`AddHonuaGrpcClient`, `AddHonuaAdminClient`)
- Typed request/response models for feature queries
