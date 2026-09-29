---
type: reference
title: "Compatibility policy"
description: "The two contracts this SDK treats separately - the public API surface and the server protocol - and what counts as a breaking change to each."
resource: "https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/public-api-approval.md"
tags: [compatibility, versioning, policy]
---
# Compatibility Policy

This SDK treats compatibility as two separate contracts: server compatibility
for runtime behavior and package API compatibility for released .NET packages.

## Server Compatibility Matrix

| SDK package baseline | Honua Server baseline | Release channel baseline | Admin API major | Admin API base path |
|----------------------|-----------------------|--------------------------|-----------------|---------------------|
| `1.x` | `0.1.0` or newer | `preview` or later | `1` | `/api/v1/admin` |

`Honua.Sdk.Admin` evaluates this matrix through
`HonuaAdminCompatibility.Evaluate()` and `IHonuaAdminClient.CheckCompatibilityAsync()`.
A server is unsupported when it omits compatibility metadata, reports a lower
server version, advertises a lower release channel, changes the control-plane
API major, changes the admin base path, or marks the advertised control-plane
API as deprecated.

Supported release channels, from lowest to highest, are `nightly`, `dev`,
`alpha`, `preview`, `beta`, `rc`, `stable`, and `lts`. The current SDK baseline
requires `preview` or higher.

## 2026.1 Source-Import Support Matrix

ArcGIS service/layer import through `Honua.Sdk.GeoServices` is qualified only
for the supported representations in the
[source-import certification](sdk-certification.md#import-fidelity-for-20261).
Package API availability alone does not establish lossless import support.
The [2026.1 acceptance ruling](https://github.com/honua-io/honua-sdk-dotnet/issues/341#issuecomment-5883909477)
records these explicit limits:

| Representation | Certification cell | 2026.1 support | Reason |
| --- | --- | --- | --- |
| Live attachments | `data.attachments` | **Not supported in 2026.1** | The pinned server's supported publishing API cannot author an attachment-enabled source; `hasAttachments=false`. Attachment list/download APIs and SDK tests do not qualify a live lossless import. |
| Subtypes | `schema.subtypes` | **Not supported in 2026.1** | The supported publishing/field-configuration APIs cannot author layer subtype definitions. Preserving authored subtype JSON in an SDK fixture does not prove a live subtype round-trip. |
| True curves | `geometry.true-curves` | **Not supported in 2026.1** | The pinned source advertises `supportsTrueCurve=false` and cannot author `curvePaths`/`curveRings`. Linearized geometry is not lossless curve preservation. |

These are documented unsupported findings for 2026.1. Source authoring support
and lossless round-trip qualification are deferred to 2026.2 by the ruling;
2026.2 support is not certified here. The recorded source-import receipt remains
41 pass / 0 fail / 3 released, with the three rows retained as `released`, never
counted as passes. The 41-cell result must be rerun on the release candidate
with the rest of the SDK evidence.

## Package API Compatibility Gate

CI validates public package API compatibility by packing the baseline ref and
the current checkout, then comparing the resulting `.nupkg` files with
`Microsoft.DotNet.ApiCompat.Tool`.

The gate runs for:

- Pull requests and pushes in `.github/workflows/ci.yml`.
- Package publish dry runs and tag publishes in
  `.github/workflows/publish-dotnet-sdk.yml`.

Run the same check locally with:

```bash
scripts/validate-api-compat.sh origin/trunk
```

Breaking public API changes should be avoided for the current `1.x` package
line unless the release plan explicitly accepts the break. When the server
compatibility baseline changes, update `HonuaAdminCompatibility`, the
compatibility matrix tests, and this document in the same pull request.
