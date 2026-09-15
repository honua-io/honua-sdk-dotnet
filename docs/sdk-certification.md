# SDK protocol certification

The .NET SDK certification lane proves public SDK operations against a real,
deterministically seeded Honua Server. The source-derived ledger at
`contracts/sdk-certification.v1.json` is authoritative for operation-level
completeness. It catalogs every async member declared by a public
`IHonua*Client` interface and fails CI when the public surface changes without a
corresponding regenerated ledger.
For provider-neutral contracts, the ledger also records every concrete client
implementation and binds canonical tests to each implementation. An operation
is exercised only when every concrete implementation has live evidence.

## Compliance tiers

| Tier | Trigger | Required scope | Failure semantics |
| --- | --- | --- | --- |
| PR | Relevant pull request | Bounded gRPC, FeatureServer, and OGC API Features reads | Missing, skipped, or failed required cells fail closed. |
| Nightly | Daily schedule or manual dispatch | Every concrete public client operation | Gaps and skips remain owned defects and fail the certification verdict. |
| Release | Manual release-train dispatch | Nightly scope rerun on the exact candidate | Also requires a digest-addressed image, full source SHA, matching seed revision, fixture revision, and independently frozen release cut. |

Provider-neutral interfaces without a concrete Honua transport are recorded as
`non-addressable`; they are not passes. Concrete methods without an executable
live test are recorded as `gap` and owned by
[honua-sdk-dotnet#31](https://github.com/honua-io/honua-sdk-dotnet/issues/31).
Raster implementation gaps remain owned by
[honua-sdk-dotnet#294](https://github.com/honua-io/honua-sdk-dotnet/issues/294).

## Commands

Regenerate the operation ledger after changing a public client:

```bash
python3 scripts/generate-sdk-certification.py
```

Check that the committed ledger matches source and canonical test calls:

```bash
python3 scripts/generate-sdk-certification.py --check
```

The workflow parses TRX results and publishes normalized cells containing the
surface, operation, SDK version, exact deployment target, scenario facets,
required tier, verdict, tests, and owned disposition. A gap, unowned skip,
missing result, identity mismatch, or observed failure can never be converted
to a passing cell.

## Source-import certification

`certification/geoservices-source-import/` certifies lossless ArcGIS
service/layer import through the installed `Honua.Sdk.GeoServices` package, for
[honua-sdk-dotnet#341](https://github.com/honua-io/honua-sdk-dotnet/issues/341).
Unlike the operation ledger, it treats the digest-pinned candidate as a live
ArcGIS REST *source*. A versioned fixture is published through the supported
admin API, and a consumer restores the SDK from nuget.org by version.

Metadata cells require every member of the source's own JSON to survive the
typed model. Data cells compare decoded values with an oracle written by hand
from the fixture. A negative-control run with a corrupted oracle must fail. See
the [directory README](../certification/geoservices-source-import/README.md)
for the cells, released cells and run instructions.
