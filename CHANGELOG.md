# Changelog

All notable changes to the Honua .NET SDK will be documented in this file.

## [1.4.0](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v1.3.0...dotnet-sdk-v1.4.0) (2026-06-27)


### Features

* **geometry:** NTS-native feature geometry and client-side reprojection ([#218](https://github.com/honua-io/honua-sdk-dotnet/issues/218)) ([9978a71](https://github.com/honua-io/honua-sdk-dotnet/commit/9978a7102a3e82001042ab7e17b2a27804e0b016))
* **raster:** reach raster client via AddHonua DI and implement IHonuaRasterDataClient ([#220](https://github.com/honua-io/honua-sdk-dotnet/issues/220)) ([2333f3b](https://github.com/honua-io/honua-sdk-dotnet/commit/2333f3bee92bb7e09249096ebf83b742a40dee00))


### Bug Fixes

* pre-release audit S2 correctness fixes (auth, parser, gRPC datetime, docs) ([#229](https://github.com/honua-io/honua-sdk-dotnet/issues/229)) ([34c8d99](https://github.com/honua-io/honua-sdk-dotnet/commit/34c8d99cff05d1ff687d627eed7a53b3e008f716))
* **reliability:** scale resilience timeouts to budget; mark offline pull as full-refresh ([#231](https://github.com/honua-io/honua-sdk-dotnet/issues/231)) ([5961dba](https://github.com/honua-io/honua-sdk-dotnet/commit/5961dba45a5dc7f69251f312a24bd1ba0d0492b7))

## [1.3.0](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v1.2.1...dotnet-sdk-v1.3.0) (2026-06-24)


### Features

* **console-share:** add open-data (DCAT/STAC) SDK client; FeatureServer Multi* edit-projection tests ([#216](https://github.com/honua-io/honua-sdk-dotnet/issues/216)) ([5a96cba](https://github.com/honua-io/honua-sdk-dotnet/commit/5a96cba79373b8802f65a2a427e8fba9782c228f))
* **offline:** scope extractChanges by server generation ([#199](https://github.com/honua-io/honua-sdk-dotnet/issues/199)) ([b73a460](https://github.com/honua-io/honua-sdk-dotnet/commit/b73a460b21bcf98e4515073e4c203ff3e923284a))


### Bug Fixes

* **abstractions:** disable HTTP auto-redirect by default to stop X-API-Key leak ([#215](https://github.com/honua-io/honua-sdk-dotnet/issues/215)) ([130e4ef](https://github.com/honua-io/honua-sdk-dotnet/commit/130e4efef7f82dfc4dce66ced8cb9f3ffd2305a8))
* **abstractions:** resolve FeatureStreamEventBuffer completion/backpressure deadlocks ([#208](https://github.com/honua-io/honua-sdk-dotnet/issues/208)) ([34218ae](https://github.com/honua-io/honua-sdk-dotnet/commit/34218ae82c542ff89c1570425fd91becf5b64cbc)), closes [#201](https://github.com/honua-io/honua-sdk-dotnet/issues/201)
* **resilience:** correct default resilience config that broke clients on first use ([#212](https://github.com/honua-io/honua-sdk-dotnet/issues/212)) ([c97a9a9](https://github.com/honua-io/honua-sdk-dotnet/commit/c97a9a996c7efeacce8ce5201f106324918a8387)), closes [#200](https://github.com/honua-io/honua-sdk-dotnet/issues/200)

## [1.2.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v1.2.0...dotnet-sdk-v1.2.1) (2026-06-09)


### Documentation

* add OpenSSF Scorecard badge ([#193](https://github.com/honua-io/honua-sdk-dotnet/issues/193)) ([f46085f](https://github.com/honua-io/honua-sdk-dotnet/commit/f46085f52dca90261b122235a0caf84637644679))

## [1.2.0](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v1.1.0...dotnet-sdk-v1.2.0) (2026-06-03)


### Features

* **forms:** real expression engine for calculated, constraint, and relevance fields ([#189](https://github.com/honua-io/honua-sdk-dotnet/issues/189)) ([50eb982](https://github.com/honua-io/honua-sdk-dotnet/commit/50eb982e7772cdc066818627d6e1a49d87e16e3c))

## [1.1.0](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v1.0.0...dotnet-sdk-v1.1.0) (2026-06-02)


### Features

* .NET SDK sample app and staging integration test suite (#honua-sdk-dotnet-3) ([1d519b0](https://github.com/honua-io/honua-sdk-dotnet/commit/1d519b0b928bfb5626cc1c9af466d9a07abeaad8))
* .NET SDK: Console Studio package clients and validation responses ([#169](https://github.com/honua-io/honua-sdk-dotnet/issues/169)) ([#170](https://github.com/honua-io/honua-sdk-dotnet/issues/170)) ([28917d8](https://github.com/honua-io/honua-sdk-dotnet/commit/28917d8001dae6e5a1d2ae8cab260f2dfcf98394))
* **abstractions:** migrate provider-neutral request DTOs from honua-mobile ([#152](https://github.com/honua-io/honua-sdk-dotnet/issues/152)) ([890e1ee](https://github.com/honua-io/honua-sdk-dotnet/commit/890e1eefb5b152e7460ec7497f7a57d37d5e823e))
* add .NET compatibility baseline ([#14](https://github.com/honua-io/honua-sdk-dotnet/issues/14)) ([#17](https://github.com/honua-io/honua-sdk-dotnet/issues/17)) ([b71c696](https://github.com/honua-io/honua-sdk-dotnet/commit/b71c696d483c8b322cd46ecf1acf67a4429f7cdf))
* add .NET demo suite scaffold ([2e43860](https://github.com/honua-io/honua-sdk-dotnet/commit/2e4386077ccbffc3d2ed1ad92ebdf85a465a94a9))
* add advanced editing rule contracts ([#123](https://github.com/honua-io/honua-sdk-dotnet/issues/123)) ([c5c3256](https://github.com/honua-io/honua-sdk-dotnet/commit/c5c32567063d5a20fcc22ae1cdaf175796e8a135))
* add catalog discovery client ([#110](https://github.com/honua-io/honua-sdk-dotnet/issues/110)) ([b988c82](https://github.com/honua-io/honua-sdk-dotnet/commit/b988c8212a9ad04a0819c4454f4c932e5947c8ac))
* add feature rest parity knobs ([5d2c54c](https://github.com/honua-io/honua-sdk-dotnet/commit/5d2c54c57047caf723bfbc9b50a923471b27ba1d))
* add feature REST parity knobs ([e2e82da](https://github.com/honua-io/honua-sdk-dotnet/commit/e2e82da0afc0152b9a9c3c811eaea33adfeac16a))
* add feature stream contracts ([#111](https://github.com/honua-io/honua-sdk-dotnet/issues/111)) ([6b6e90a](https://github.com/honua-io/honua-sdk-dotnet/commit/6b6e90a692005f86d869d4c9b4c92a338fd48b2a))
* add field workflow SDK package ([#103](https://github.com/honua-io/honua-sdk-dotnet/issues/103)) ([83f2f35](https://github.com/honua-io/honua-sdk-dotnet/commit/83f2f35e990fc0d54ba71af6c70a6c49779b42dd))
* add host-neutral geofence evaluation ([#118](https://github.com/honua-io/honua-sdk-dotnet/issues/118)) ([f5e95b8](https://github.com/honua-io/honua-sdk-dotnet/commit/f5e95b8f6eec73dc71b1d6c9f71a1e02ed560fbb))
* add host-neutral plugin contracts ([#116](https://github.com/honua-io/honua-sdk-dotnet/issues/116)) ([2513c2c](https://github.com/honua-io/honua-sdk-dotnet/commit/2513c2ce9bd90f59fae6d3d56a8b7832b47dbab1))
* add HTTP retry with resilience handler for Admin and Geocoding clients ([546f221](https://github.com/honua-io/honua-sdk-dotnet/commit/546f22110050cb06fb659cea765e9cc4a5bd3df1))
* add observability and deploy control endpoints to admin client ([6dd0cdf](https://github.com/honua-io/honua-sdk-dotnet/commit/6dd0cdf4785f68e8445b2636452ce876aa1f5a42))
* add observability and deploy control endpoints to admin client ([02c4e04](https://github.com/honua-io/honua-sdk-dotnet/commit/02c4e04782fafa46a7fe0fd0e9c5153495c296be))
* add OGC API Records client ([#148](https://github.com/honua-io/honua-sdk-dotnet/issues/148)) ([59db134](https://github.com/honua-io/honua-sdk-dotnet/commit/59db1346cb793ceeac075a3e8dcb7767a7c0c88a))
* add OGC merge patch edit contracts ([#112](https://github.com/honua-io/honua-sdk-dotnet/issues/112)) ([0594ed3](https://github.com/honua-io/honua-sdk-dotnet/commit/0594ed32574a419c66cad53f2a8df88aaa8fd2ac))
* add planar geometry analysis helpers ([#114](https://github.com/honua-io/honua-sdk-dotnet/issues/114)) ([134f954](https://github.com/honua-io/honua-sdk-dotnet/commit/134f954cae4837e716faf84e85319860571438ae))
* add production auth provider hooks ([f1f6a3a](https://github.com/honua-io/honua-sdk-dotnet/commit/f1f6a3a8f2a149911576df27a3ccc3806ebd187d))
* add production auth provider hooks ([64c3d0f](https://github.com/honua-io/honua-sdk-dotnet/commit/64c3d0f2e857934f202de79596c6aa319931a116))
* add raster elevation enrichment contracts ([#125](https://github.com/honua-io/honua-sdk-dotnet/issues/125)) ([cf3a7e3](https://github.com/honua-io/honua-sdk-dotnet/commit/cf3a7e3ec843e47d4c5a1c5bd196a505837d3a10))
* add scene metadata SDK package ([c9e8459](https://github.com/honua-io/honua-sdk-dotnet/commit/c9e8459ca4e99be75fe78ed43802c8251226e4c2))
* add SDK geometry core ([3a39fdc](https://github.com/honua-io/honua-sdk-dotnet/commit/3a39fdc9c40d1a958bfd451612693d2fcfa920d6))
* add spec workspace SDK contracts ([075cffe](https://github.com/honua-io/honua-sdk-dotnet/commit/075cffe09a5995a4605fbf1d904b11a82013e09f))
* add STAC catalog client ([#150](https://github.com/honua-io/honua-sdk-dotnet/issues/150)) ([223b1ab](https://github.com/honua-io/honua-sdk-dotnet/commit/223b1abdf86ade23efbbf35d54d40deabc8e98d8))
* add styleId-keyed OGC styles client ([#184](https://github.com/honua-io/honua-sdk-dotnet/issues/184)) ([#187](https://github.com/honua-io/honua-sdk-dotnet/issues/187)) ([175ab77](https://github.com/honua-io/honua-sdk-dotnet/commit/175ab77c85cf90f0890a1f392f575ab970da39e1))
* add utility network trace contracts ([#121](https://github.com/honua-io/honua-sdk-dotnet/issues/121)) ([006c34c](https://github.com/honua-io/honua-sdk-dotnet/commit/006c34ce3017c30dfab7bbaeec6f316c6f6c7a53))
* complete admin contract gap coverage ([7cd992d](https://github.com/honua-io/honua-sdk-dotnet/commit/7cd992d83249e568adeaabb42a23c0bff4390f4a))
* complete admin contract gap coverage ([445362e](https://github.com/honua-io/honua-sdk-dotnet/commit/445362ec5db9db9d92a41264226c778fa05230f0))
* complete Phase 0 Epic [#402](https://github.com/honua-io/honua-sdk-dotnet/issues/402) - Mobile parity+innovation spec and contract freeze ([376b3ba](https://github.com/honua-io/honua-sdk-dotnet/commit/376b3ba9c2dc3c2c8453e6c441492526b5afd690))
* Console .NET client contracts for Blazor Web and MAUI hosts ([#166](https://github.com/honua-io/honua-sdk-dotnet/issues/166)) ([62fdcb6](https://github.com/honua-io/honua-sdk-dotnet/commit/62fdcb65e1403e6679a7b9250b4bb3b41a2b09db))
* Demo: admin bootstrap console and gRPC verification flow for .NET SDK ([#23](https://github.com/honua-io/honua-sdk-dotnet/issues/23)) ([19c6771](https://github.com/honua-io/honua-sdk-dotnet/commit/19c67713ba58dbefcaf5e836bc8260ce0dce32ea))
* **dotnet:** add featureserver and ogc features clients ([b6ade48](https://github.com/honua-io/honua-sdk-dotnet/commit/b6ade48cf3e4f9619a534006ad4bb04576d76ad1))
* enhanced gRPC capabilities for mobile SDK integration ([0bb1efa](https://github.com/honua-io/honua-sdk-dotnet/commit/0bb1efa062f4afa4b40e9200144caf26e28c5eb1)), closes [#359](https://github.com/honua-io/honua-sdk-dotnet/issues/359)
* expand geocoding parity ([1f7b449](https://github.com/honua-io/honua-sdk-dotnet/commit/1f7b449e00527f1f5b5a4fe1a64fff9e76df1948))
* expand geocoding parity ([0472d63](https://github.com/honua-io/honua-sdk-dotnet/commit/0472d63367fbba2439236dc4710a1cf317d8e077))
* expose grpc transport converter fixtures ([8397c7c](https://github.com/honua-io/honua-sdk-dotnet/commit/8397c7cf43a90e2362e8afcca79354a415e5bb3a))
* **geometry:** add GeographicBoundingBox WGS84 primitive ([#151](https://github.com/honua-io/honua-sdk-dotnet/issues/151)) ([cd0e066](https://github.com/honua-io/honua-sdk-dotnet/commit/cd0e066af630bf57cab761790d25dc803d0d8ede))
* graduate admin identity and license contracts ([c4dedb9](https://github.com/honua-io/honua-sdk-dotnet/commit/c4dedb97341f1067fd93de8b22d0274cc766c6b2))
* graduate admin identity and license contracts ([c6b4027](https://github.com/honua-io/honua-sdk-dotnet/commit/c6b4027b2722423094778decf697543eda3377f5))
* **grpc,abstractions:** migrate mobile-side transport mappings ([#154](https://github.com/honua-io/honua-sdk-dotnet/issues/154)) ([c94333f](https://github.com/honua-io/honua-sdk-dotnet/commit/c94333f9cd523110eb2f42953d7950613f9a2f69))
* **grpc,geoservices,ogcfeatures,offline:** expose RequestConverters publicly and surface Geometry transitively ([#155](https://github.com/honua-io/honua-sdk-dotnet/issues/155)) ([919077a](https://github.com/honua-io/honua-sdk-dotnet/commit/919077a86caef9bc5a7ee05e2c6b96589f6d835a))
* migrate mobile content from honua-server ([c45fc05](https://github.com/honua-io/honua-sdk-dotnet/commit/c45fc05e09a7ad2aaed8ff6fd5c5641e07f244c2))
* **offline:** add reusable sync core ([1b6fb56](https://github.com/honua-io/honua-sdk-dotnet/commit/1b6fb56f5292662d2c5a81269995d8a9616d0f95))
* **offline:** add reusable sync core ([b8cb509](https://github.com/honua-io/honua-sdk-dotnet/commit/b8cb5092cd0f987ae87f4927bb151dc11fafd29b))
* **offline:** migrate replica sync HTTP client from honua-mobile ([#153](https://github.com/honua-io/honua-sdk-dotnet/issues/153)) ([5c82ab8](https://github.com/honua-io/honua-sdk-dotnet/commit/5c82ab8d4b4258cae07cd591e62d208a9e2845f8))
* SDK publishing, geocoding client, and developer docs ([#7](https://github.com/honua-io/honua-sdk-dotnet/issues/7)) ([b4f6487](https://github.com/honua-io/honua-sdk-dotnet/commit/b4f64875af7472a6d5bbebcd90432043a1356f74))
* sdk(dotnet): add FeatureServer and OGC API Features read/query clients ([#473](https://github.com/honua-io/honua-sdk-dotnet/issues/473)) ([390a50d](https://github.com/honua-io/honua-sdk-dotnet/commit/390a50d5295926c284713f7605c0d6dd657c5113))
* sdk(dotnet): add WFS read/query client ([#474](https://github.com/honua-io/honua-sdk-dotnet/issues/474)) ([95a5cd2](https://github.com/honua-io/honua-sdk-dotnet/commit/95a5cd2ddeabb79d7d665fd8eada614b8832ff26))


### Bug Fixes

* address geometry review feedback ([3c4e250](https://github.com/honua-io/honua-sdk-dotnet/commit/3c4e250ad515b128b20190154f61747da250cb47))
* address repo review findings ([d8771ec](https://github.com/honua-io/honua-sdk-dotnet/commit/d8771ec15a1ac7fef78305f3e35e0208250cd480))
* address repo review findings ([c5288bf](https://github.com/honua-io/honua-sdk-dotnet/commit/c5288bffc718d20a4eb4016d74562829da6a5525))
* align .NET SDK publishing baseline ([9d14096](https://github.com/honua-io/honua-sdk-dotnet/commit/9d1409611aff6fa47bc56bc6222709d995504733))
* correct release please tag separator ([fb81b1e](https://github.com/honua-io/honua-sdk-dotnet/commit/fb81b1e8d78936c0a773bf3cd831e859f5dc7458))
* correct release please tag separator ([06f18ba](https://github.com/honua-io/honua-sdk-dotnet/commit/06f18bac8992cd80130bd296b3bf9e652b0d8930))
* **geoservices:** project GeoJSON Point geometry to FeatureServer {x,y} ([#156](https://github.com/honua-io/honua-sdk-dotnet/issues/156)) ([b9e2d1e](https://github.com/honua-io/honua-sdk-dotnet/commit/b9e2d1e6ae62880bb74731019e353c31404e3d7c))
* harden grpc transport and proto conversion ([1dae822](https://github.com/honua-io/honua-sdk-dotnet/commit/1dae822fbeb8b81a80e4fa6690ff0001213aa978))
* honor zero object id query limit ([c2f08b7](https://github.com/honua-io/honua-sdk-dotnet/commit/c2f08b7f7b39ef733f548f0e140b65634429cf28))
* remove Phase 0 docs from wrong repository ([baaf2e6](https://github.com/honua-io/honua-sdk-dotnet/commit/baaf2e658a23e98a683a20c737bfce224d885c37))


### Documentation

* add source-backed feature map ([555fda9](https://github.com/honua-io/honua-sdk-dotnet/commit/555fda9bbd5add0f6dc135b454516bf2105db107))
* fix incorrect server namespace in install docs ([0350a30](https://github.com/honua-io/honua-sdk-dotnet/commit/0350a305c0ccb07fbe5d1e174bf6381ae11e2222))
* update README with ApplyEdits, retry, streaming examples ([eb26619](https://github.com/honua-io/honua-sdk-dotnet/commit/eb26619e1937ae4368b37ff0e2610475b593a560))

## [Unreleased]

### Added

* `Honua.Sdk.Field`: native repeatable-section support. `FieldRecord.Repeats`
  carries captured rows (`FieldRepeatInstance`) per repeatable section, so repeat
  data is part of the portable record contract and round-trips through
  serialization, sync, and export rather than being flattened by consumers.
  `FormValidator` and `CalculatedFieldEvaluator` now evaluate each row against
  its own values; repeat-row validation errors are reported with field ids of
  the form `sectionId[index].fieldId`. Additive and backward compatible.

## [1.0.0] - 2026-05-21

First stable release of the Honua .NET SDK. Subsequent releases follow standard
SemVer; breaking changes will be gated behind a major bump.

### Public surface

* Twelve packages under one umbrella: install `Honua.Sdk` and call
  `AddHonua(o => o.BaseAddress = ...)` to register every enabled sub-package,
  or depend on the narrower `Honua.Sdk.*` packages directly for tighter
  transitive graphs.
* Every exception type derives from `Honua.Sdk.Abstractions.HonuaException`;
  misconfiguration is surfaced at registration time via
  `HonuaConfigurationException` rather than silently dialing
  `http://localhost`.
* `IHonuaAdminClient` is split into twelve role-segregated sub-interfaces
  (Services, Layers, Connections, Styles, Metadata, Compatibility, Manifest,
  Config, Identity, License, Observability, Deploy) for ISP-clean consumer
  surfaces. The aggregate interface remains as a convenience.
* `IHonuaStacClient` exposes a typed surface plus `IHonuaStacRawClient` with
  raw `JsonDocument` / `HttpResponseMessage` escape hatches for catalog
  extensions the typed surface does not yet model.

### Breaking changes

* REST and gRPC clients require an explicit `BaseAddress`; there is no
  baked-in `localhost` default.
* `HonuaClientOptions.MaxRetryAttempts` throws `ArgumentOutOfRangeException`
  when the value falls outside `[2, 5]`.
* Request DTOs (`QueryFeaturesRequest`, `ApplyEditsRequest`,
  `GetFeaturesRequest`, and roughly twenty Admin request types) are
  `sealed record` with `init`-only properties and `required` modifiers on
  identifying fields. Footgun defaults (`Where = "1=1"`, `OrderBy = ""`,
  `ResultRecordCount = 0`) are gone.
* `IHonuaGrpcClient` no longer extends `IDisposable` (the concrete
  `HonuaGrpcClient` still is). gRPC options expose only `BaseAddress` (`Uri`);
  the legacy `Address` string property has been removed.
* Cancellation parameters are uniformly named `cancellationToken` across
  every public API, matching the .NET Framework Design Guidelines.

### Packaging and build

* Central Package Management via `Directory.Packages.props`; the .NET SDK is
  pinned via `global.json` to `10.0.100` with `allowPrerelease: false`.
* Coverage gate at 30% line / 20% branch (measured coverage is 84% line /
  69% branch).
* NuGet signing required on release tags.
* CycloneDX SBOM emitted per package on release.
* CodeQL and dependency vulnerability audit run on every PR.

### Documentation

* Hosted DocFX API reference at
  <https://honua-io.github.io/honua-sdk-dotnet/>, deployed by
  `.github/workflows/docs.yml`.

## [0.1.17-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.16-alpha.1...dotnet-sdk-v0.1.17-alpha.1) (2026-05-20)


### Features

* **abstractions:** migrate provider-neutral request DTOs from honua-mobile ([#152](https://github.com/honua-io/honua-sdk-dotnet/issues/152)) ([890e1ee](https://github.com/honua-io/honua-sdk-dotnet/commit/890e1eefb5b152e7460ec7497f7a57d37d5e823e))
* add OGC API Records client ([#148](https://github.com/honua-io/honua-sdk-dotnet/issues/148)) ([59db134](https://github.com/honua-io/honua-sdk-dotnet/commit/59db1346cb793ceeac075a3e8dcb7767a7c0c88a))
* add STAC catalog client ([#150](https://github.com/honua-io/honua-sdk-dotnet/issues/150)) ([223b1ab](https://github.com/honua-io/honua-sdk-dotnet/commit/223b1abdf86ade23efbbf35d54d40deabc8e98d8))
* **geometry:** add GeographicBoundingBox WGS84 primitive ([#151](https://github.com/honua-io/honua-sdk-dotnet/issues/151)) ([cd0e066](https://github.com/honua-io/honua-sdk-dotnet/commit/cd0e066af630bf57cab761790d25dc803d0d8ede))
* **grpc,abstractions:** migrate mobile-side transport mappings ([#154](https://github.com/honua-io/honua-sdk-dotnet/issues/154)) ([c94333f](https://github.com/honua-io/honua-sdk-dotnet/commit/c94333f9cd523110eb2f42953d7950613f9a2f69))
* **grpc,geoservices,ogcfeatures,offline:** expose RequestConverters publicly and surface Geometry transitively ([#155](https://github.com/honua-io/honua-sdk-dotnet/issues/155)) ([919077a](https://github.com/honua-io/honua-sdk-dotnet/commit/919077a86caef9bc5a7ee05e2c6b96589f6d835a))
* **offline:** migrate replica sync HTTP client from honua-mobile ([#153](https://github.com/honua-io/honua-sdk-dotnet/issues/153)) ([5c82ab8](https://github.com/honua-io/honua-sdk-dotnet/commit/5c82ab8d4b4258cae07cd591e62d208a9e2845f8))

## [0.1.16-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.15-alpha.1...dotnet-sdk-v0.1.16-alpha.1) (2026-05-07)


### Features

* add .NET demo suite scaffold ([2e43860](https://github.com/honua-io/honua-sdk-dotnet/commit/2e4386077ccbffc3d2ed1ad92ebdf85a465a94a9))


### Documentation

* add source-backed feature map ([555fda9](https://github.com/honua-io/honua-sdk-dotnet/commit/555fda9bbd5add0f6dc135b454516bf2105db107))

## [0.1.15-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.14-alpha.1...dotnet-sdk-v0.1.15-alpha.1) (2026-04-30)


### Features

* add raster elevation enrichment contracts ([#125](https://github.com/honua-io/honua-sdk-dotnet/issues/125)) ([cf3a7e3](https://github.com/honua-io/honua-sdk-dotnet/commit/cf3a7e3ec843e47d4c5a1c5bd196a505837d3a10))

## [0.1.14-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.13-alpha.1...dotnet-sdk-v0.1.14-alpha.1) (2026-04-30)


### Features

* add advanced editing rule contracts ([#123](https://github.com/honua-io/honua-sdk-dotnet/issues/123)) ([c5c3256](https://github.com/honua-io/honua-sdk-dotnet/commit/c5c32567063d5a20fcc22ae1cdaf175796e8a135))

## [0.1.13-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.12-alpha.1...dotnet-sdk-v0.1.13-alpha.1) (2026-04-30)


### Features

* add utility network trace contracts ([#121](https://github.com/honua-io/honua-sdk-dotnet/issues/121)) ([006c34c](https://github.com/honua-io/honua-sdk-dotnet/commit/006c34ce3017c30dfab7bbaeec6f316c6f6c7a53))

## [0.1.12-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.11-alpha.1...dotnet-sdk-v0.1.12-alpha.1) (2026-04-30)


### Features

* add host-neutral geofence evaluation ([#118](https://github.com/honua-io/honua-sdk-dotnet/issues/118)) ([f5e95b8](https://github.com/honua-io/honua-sdk-dotnet/commit/f5e95b8f6eec73dc71b1d6c9f71a1e02ed560fbb))

## [0.1.11-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.10-alpha.1...dotnet-sdk-v0.1.11-alpha.1) (2026-04-30)


### Features

* add host-neutral plugin contracts ([#116](https://github.com/honua-io/honua-sdk-dotnet/issues/116)) ([2513c2c](https://github.com/honua-io/honua-sdk-dotnet/commit/2513c2ce9bd90f59fae6d3d56a8b7832b47dbab1))

## [0.1.10-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.9-alpha.1...dotnet-sdk-v0.1.10-alpha.1) (2026-04-30)


### Features

* add planar geometry analysis helpers ([#114](https://github.com/honua-io/honua-sdk-dotnet/issues/114)) ([134f954](https://github.com/honua-io/honua-sdk-dotnet/commit/134f954cae4837e716faf84e85319860571438ae))

## [0.1.9-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.8-alpha.1...dotnet-sdk-v0.1.9-alpha.1) (2026-04-30)


### Features

* add catalog discovery client ([#110](https://github.com/honua-io/honua-sdk-dotnet/issues/110)) ([b988c82](https://github.com/honua-io/honua-sdk-dotnet/commit/b988c8212a9ad04a0819c4454f4c932e5947c8ac))
* add feature stream contracts ([#111](https://github.com/honua-io/honua-sdk-dotnet/issues/111)) ([6b6e90a](https://github.com/honua-io/honua-sdk-dotnet/commit/6b6e90a692005f86d869d4c9b4c92a338fd48b2a))
* add OGC merge patch edit contracts ([#112](https://github.com/honua-io/honua-sdk-dotnet/issues/112)) ([0594ed3](https://github.com/honua-io/honua-sdk-dotnet/commit/0594ed32574a419c66cad53f2a8df88aaa8fd2ac))
* add production auth provider hooks ([f1f6a3a](https://github.com/honua-io/honua-sdk-dotnet/commit/f1f6a3a8f2a149911576df27a3ccc3806ebd187d))
* add production auth provider hooks ([64c3d0f](https://github.com/honua-io/honua-sdk-dotnet/commit/64c3d0f2e857934f202de79596c6aa319931a116))
* expand geocoding parity ([1f7b449](https://github.com/honua-io/honua-sdk-dotnet/commit/1f7b449e00527f1f5b5a4fe1a64fff9e76df1948))
* expand geocoding parity ([0472d63](https://github.com/honua-io/honua-sdk-dotnet/commit/0472d63367fbba2439236dc4710a1cf317d8e077))

## [0.1.8-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.7-alpha.1...dotnet-sdk-v0.1.8-alpha.1) (2026-04-30)


### Features

* add field workflow SDK package ([#103](https://github.com/honua-io/honua-sdk-dotnet/issues/103)) ([83f2f35](https://github.com/honua-io/honua-sdk-dotnet/commit/83f2f35e990fc0d54ba71af6c70a6c49779b42dd))

## [0.1.7-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.6-alpha.1...dotnet-sdk-v0.1.7-alpha.1) (2026-04-30)


### Features

* add scene metadata SDK package ([c9e8459](https://github.com/honua-io/honua-sdk-dotnet/commit/c9e8459ca4e99be75fe78ed43802c8251226e4c2))

## [0.1.6-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.5-alpha.1...dotnet-sdk-v0.1.6-alpha.1) (2026-04-30)


### Features

* .NET SDK sample app and staging integration test suite (#honua-sdk-dotnet-3) ([1d519b0](https://github.com/honua-io/honua-sdk-dotnet/commit/1d519b0b928bfb5626cc1c9af466d9a07abeaad8))
* add .NET compatibility baseline ([#14](https://github.com/honua-io/honua-sdk-dotnet/issues/14)) ([#17](https://github.com/honua-io/honua-sdk-dotnet/issues/17)) ([b71c696](https://github.com/honua-io/honua-sdk-dotnet/commit/b71c696d483c8b322cd46ecf1acf67a4429f7cdf))
* add feature rest parity knobs ([5d2c54c](https://github.com/honua-io/honua-sdk-dotnet/commit/5d2c54c57047caf723bfbc9b50a923471b27ba1d))
* add feature REST parity knobs ([e2e82da](https://github.com/honua-io/honua-sdk-dotnet/commit/e2e82da0afc0152b9a9c3c811eaea33adfeac16a))
* add HTTP retry with resilience handler for Admin and Geocoding clients ([546f221](https://github.com/honua-io/honua-sdk-dotnet/commit/546f22110050cb06fb659cea765e9cc4a5bd3df1))
* add observability and deploy control endpoints to admin client ([6dd0cdf](https://github.com/honua-io/honua-sdk-dotnet/commit/6dd0cdf4785f68e8445b2636452ce876aa1f5a42))
* add observability and deploy control endpoints to admin client ([02c4e04](https://github.com/honua-io/honua-sdk-dotnet/commit/02c4e04782fafa46a7fe0fd0e9c5153495c296be))
* add SDK geometry core ([3a39fdc](https://github.com/honua-io/honua-sdk-dotnet/commit/3a39fdc9c40d1a958bfd451612693d2fcfa920d6))
* add spec workspace SDK contracts ([075cffe](https://github.com/honua-io/honua-sdk-dotnet/commit/075cffe09a5995a4605fbf1d904b11a82013e09f))
* complete admin contract gap coverage ([7cd992d](https://github.com/honua-io/honua-sdk-dotnet/commit/7cd992d83249e568adeaabb42a23c0bff4390f4a))
* complete admin contract gap coverage ([445362e](https://github.com/honua-io/honua-sdk-dotnet/commit/445362ec5db9db9d92a41264226c778fa05230f0))
* complete Phase 0 Epic [#402](https://github.com/honua-io/honua-sdk-dotnet/issues/402) - Mobile parity+innovation spec and contract freeze ([376b3ba](https://github.com/honua-io/honua-sdk-dotnet/commit/376b3ba9c2dc3c2c8453e6c441492526b5afd690))
* Demo: admin bootstrap console and gRPC verification flow for .NET SDK ([#23](https://github.com/honua-io/honua-sdk-dotnet/issues/23)) ([19c6771](https://github.com/honua-io/honua-sdk-dotnet/commit/19c67713ba58dbefcaf5e836bc8260ce0dce32ea))
* **dotnet:** add featureserver and ogc features clients ([b6ade48](https://github.com/honua-io/honua-sdk-dotnet/commit/b6ade48cf3e4f9619a534006ad4bb04576d76ad1))
* enhanced gRPC capabilities for mobile SDK integration ([0bb1efa](https://github.com/honua-io/honua-sdk-dotnet/commit/0bb1efa062f4afa4b40e9200144caf26e28c5eb1)), closes [#359](https://github.com/honua-io/honua-sdk-dotnet/issues/359)
* expose grpc transport converter fixtures ([8397c7c](https://github.com/honua-io/honua-sdk-dotnet/commit/8397c7cf43a90e2362e8afcca79354a415e5bb3a))
* graduate admin identity and license contracts ([c4dedb9](https://github.com/honua-io/honua-sdk-dotnet/commit/c4dedb97341f1067fd93de8b22d0274cc766c6b2))
* graduate admin identity and license contracts ([c6b4027](https://github.com/honua-io/honua-sdk-dotnet/commit/c6b4027b2722423094778decf697543eda3377f5))
* migrate mobile content from honua-server ([c45fc05](https://github.com/honua-io/honua-sdk-dotnet/commit/c45fc05e09a7ad2aaed8ff6fd5c5641e07f244c2))
* **offline:** add reusable sync core ([1b6fb56](https://github.com/honua-io/honua-sdk-dotnet/commit/1b6fb56f5292662d2c5a81269995d8a9616d0f95))
* **offline:** add reusable sync core ([b8cb509](https://github.com/honua-io/honua-sdk-dotnet/commit/b8cb5092cd0f987ae87f4927bb151dc11fafd29b))
* SDK publishing, geocoding client, and developer docs ([#7](https://github.com/honua-io/honua-sdk-dotnet/issues/7)) ([b4f6487](https://github.com/honua-io/honua-sdk-dotnet/commit/b4f64875af7472a6d5bbebcd90432043a1356f74))
* sdk(dotnet): add FeatureServer and OGC API Features read/query clients ([#473](https://github.com/honua-io/honua-sdk-dotnet/issues/473)) ([390a50d](https://github.com/honua-io/honua-sdk-dotnet/commit/390a50d5295926c284713f7605c0d6dd657c5113))
* sdk(dotnet): add WFS read/query client ([#474](https://github.com/honua-io/honua-sdk-dotnet/issues/474)) ([95a5cd2](https://github.com/honua-io/honua-sdk-dotnet/commit/95a5cd2ddeabb79d7d665fd8eada614b8832ff26))


### Bug Fixes

* address geometry review feedback ([3c4e250](https://github.com/honua-io/honua-sdk-dotnet/commit/3c4e250ad515b128b20190154f61747da250cb47))
* address repo review findings ([d8771ec](https://github.com/honua-io/honua-sdk-dotnet/commit/d8771ec15a1ac7fef78305f3e35e0208250cd480))
* address repo review findings ([c5288bf](https://github.com/honua-io/honua-sdk-dotnet/commit/c5288bffc718d20a4eb4016d74562829da6a5525))
* align .NET SDK publishing baseline ([9d14096](https://github.com/honua-io/honua-sdk-dotnet/commit/9d1409611aff6fa47bc56bc6222709d995504733))
* correct release please tag separator ([fb81b1e](https://github.com/honua-io/honua-sdk-dotnet/commit/fb81b1e8d78936c0a773bf3cd831e859f5dc7458))
* correct release please tag separator ([06f18ba](https://github.com/honua-io/honua-sdk-dotnet/commit/06f18bac8992cd80130bd296b3bf9e652b0d8930))
* harden grpc transport and proto conversion ([1dae822](https://github.com/honua-io/honua-sdk-dotnet/commit/1dae822fbeb8b81a80e4fa6690ff0001213aa978))
* honor zero object id query limit ([c2f08b7](https://github.com/honua-io/honua-sdk-dotnet/commit/c2f08b7f7b39ef733f548f0e140b65634429cf28))
* remove Phase 0 docs from wrong repository ([baaf2e6](https://github.com/honua-io/honua-sdk-dotnet/commit/baaf2e658a23e98a683a20c737bfce224d885c37))


### Documentation

* fix incorrect server namespace in install docs ([0350a30](https://github.com/honua-io/honua-sdk-dotnet/commit/0350a305c0ccb07fbe5d1e174bf6381ae11e2222))
* update README with ApplyEdits, retry, streaming examples ([eb26619](https://github.com/honua-io/honua-sdk-dotnet/commit/eb26619e1937ae4368b37ff0e2610475b593a560))

## [0.1.5-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.4-alpha.1...dotnet-sdk-v0.1.5-alpha.1) (2026-04-30)


### Features

* add routing client contracts ([5fc9ffd](https://github.com/honua-io/honua-sdk-dotnet/commit/5fc9ffd3432016819fd54c1493fe52e184057816))

## [0.1.4-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.3-alpha.1...dotnet-sdk-v0.1.4-alpha.1) (2026-04-30)


### Features

* add SDK geometry core ([3a39fdc](https://github.com/honua-io/honua-sdk-dotnet/commit/3a39fdc9c40d1a958bfd451612693d2fcfa920d6))


### Bug Fixes

* address geometry review feedback ([3c4e250](https://github.com/honua-io/honua-sdk-dotnet/commit/3c4e250ad515b128b20190154f61747da250cb47))

## [0.1.3-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.2-alpha.1...dotnet-sdk-v0.1.3-alpha.1) (2026-04-30)


### Features

* add feature rest parity knobs ([5d2c54c](https://github.com/honua-io/honua-sdk-dotnet/commit/5d2c54c57047caf723bfbc9b50a923471b27ba1d))
* add feature REST parity knobs ([e2e82da](https://github.com/honua-io/honua-sdk-dotnet/commit/e2e82da0afc0152b9a9c3c811eaea33adfeac16a))

## [0.1.2-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.1-alpha.1...dotnet-sdk-v0.1.2-alpha.1) (2026-04-29)


### Features

* complete admin contract gap coverage ([7cd992d](https://github.com/honua-io/honua-sdk-dotnet/commit/7cd992d83249e568adeaabb42a23c0bff4390f4a))
* complete admin contract gap coverage ([445362e](https://github.com/honua-io/honua-sdk-dotnet/commit/445362ec5db9db9d92a41264226c778fa05230f0))

## [0.1.1-alpha.1](https://github.com/honua-io/honua-sdk-dotnet/compare/dotnet-sdk-v0.1.0-alpha.1...dotnet-sdk-v0.1.1-alpha.1) (2026-04-29)


### Features

* .NET SDK sample app and staging integration test suite (#honua-sdk-dotnet-3) ([1d519b0](https://github.com/honua-io/honua-sdk-dotnet/commit/1d519b0b928bfb5626cc1c9af466d9a07abeaad8))
* add .NET compatibility baseline ([#14](https://github.com/honua-io/honua-sdk-dotnet/issues/14)) ([#17](https://github.com/honua-io/honua-sdk-dotnet/issues/17)) ([b71c696](https://github.com/honua-io/honua-sdk-dotnet/commit/b71c696d483c8b322cd46ecf1acf67a4429f7cdf))
* add HTTP retry with resilience handler for Admin and Geocoding clients ([546f221](https://github.com/honua-io/honua-sdk-dotnet/commit/546f22110050cb06fb659cea765e9cc4a5bd3df1))
* add observability and deploy control endpoints to admin client ([6dd0cdf](https://github.com/honua-io/honua-sdk-dotnet/commit/6dd0cdf4785f68e8445b2636452ce876aa1f5a42))
* add observability and deploy control endpoints to admin client ([02c4e04](https://github.com/honua-io/honua-sdk-dotnet/commit/02c4e04782fafa46a7fe0fd0e9c5153495c296be))
* add spec workspace SDK contracts ([075cffe](https://github.com/honua-io/honua-sdk-dotnet/commit/075cffe09a5995a4605fbf1d904b11a82013e09f))
* complete Phase 0 Epic [#402](https://github.com/honua-io/honua-sdk-dotnet/issues/402) - Mobile parity+innovation spec and contract freeze ([376b3ba](https://github.com/honua-io/honua-sdk-dotnet/commit/376b3ba9c2dc3c2c8453e6c441492526b5afd690))
* Demo: admin bootstrap console and gRPC verification flow for .NET SDK ([#23](https://github.com/honua-io/honua-sdk-dotnet/issues/23)) ([19c6771](https://github.com/honua-io/honua-sdk-dotnet/commit/19c67713ba58dbefcaf5e836bc8260ce0dce32ea))
* **dotnet:** add featureserver and ogc features clients ([b6ade48](https://github.com/honua-io/honua-sdk-dotnet/commit/b6ade48cf3e4f9619a534006ad4bb04576d76ad1))
* enhanced gRPC capabilities for mobile SDK integration ([0bb1efa](https://github.com/honua-io/honua-sdk-dotnet/commit/0bb1efa062f4afa4b40e9200144caf26e28c5eb1)), closes [#359](https://github.com/honua-io/honua-sdk-dotnet/issues/359)
* expose grpc transport converter fixtures ([8397c7c](https://github.com/honua-io/honua-sdk-dotnet/commit/8397c7cf43a90e2362e8afcca79354a415e5bb3a))
* graduate admin identity and license contracts ([c4dedb9](https://github.com/honua-io/honua-sdk-dotnet/commit/c4dedb97341f1067fd93de8b22d0274cc766c6b2))
* graduate admin identity and license contracts ([c6b4027](https://github.com/honua-io/honua-sdk-dotnet/commit/c6b4027b2722423094778decf697543eda3377f5))
* migrate mobile content from honua-server ([c45fc05](https://github.com/honua-io/honua-sdk-dotnet/commit/c45fc05e09a7ad2aaed8ff6fd5c5641e07f244c2))
* **offline:** add reusable sync core ([1b6fb56](https://github.com/honua-io/honua-sdk-dotnet/commit/1b6fb56f5292662d2c5a81269995d8a9616d0f95))
* **offline:** add reusable sync core ([b8cb509](https://github.com/honua-io/honua-sdk-dotnet/commit/b8cb5092cd0f987ae87f4927bb151dc11fafd29b))
* SDK publishing, geocoding client, and developer docs ([#7](https://github.com/honua-io/honua-sdk-dotnet/issues/7)) ([b4f6487](https://github.com/honua-io/honua-sdk-dotnet/commit/b4f64875af7472a6d5bbebcd90432043a1356f74))
* sdk(dotnet): add FeatureServer and OGC API Features read/query clients ([#473](https://github.com/honua-io/honua-sdk-dotnet/issues/473)) ([390a50d](https://github.com/honua-io/honua-sdk-dotnet/commit/390a50d5295926c284713f7605c0d6dd657c5113))
* sdk(dotnet): add WFS read/query client ([#474](https://github.com/honua-io/honua-sdk-dotnet/issues/474)) ([95a5cd2](https://github.com/honua-io/honua-sdk-dotnet/commit/95a5cd2ddeabb79d7d665fd8eada614b8832ff26))


### Bug Fixes

* address repo review findings ([d8771ec](https://github.com/honua-io/honua-sdk-dotnet/commit/d8771ec15a1ac7fef78305f3e35e0208250cd480))
* address repo review findings ([c5288bf](https://github.com/honua-io/honua-sdk-dotnet/commit/c5288bffc718d20a4eb4016d74562829da6a5525))
* align .NET SDK publishing baseline ([9d14096](https://github.com/honua-io/honua-sdk-dotnet/commit/9d1409611aff6fa47bc56bc6222709d995504733))
* correct release please tag separator ([fb81b1e](https://github.com/honua-io/honua-sdk-dotnet/commit/fb81b1e8d78936c0a773bf3cd831e859f5dc7458))
* correct release please tag separator ([06f18ba](https://github.com/honua-io/honua-sdk-dotnet/commit/06f18bac8992cd80130bd296b3bf9e652b0d8930))
* harden grpc transport and proto conversion ([1dae822](https://github.com/honua-io/honua-sdk-dotnet/commit/1dae822fbeb8b81a80e4fa6690ff0001213aa978))
* honor zero object id query limit ([c2f08b7](https://github.com/honua-io/honua-sdk-dotnet/commit/c2f08b7f7b39ef733f548f0e140b65634429cf28))
* remove Phase 0 docs from wrong repository ([baaf2e6](https://github.com/honua-io/honua-sdk-dotnet/commit/baaf2e658a23e98a683a20c737bfce224d885c37))


### Documentation

* fix incorrect server namespace in install docs ([0350a30](https://github.com/honua-io/honua-sdk-dotnet/commit/0350a305c0ccb07fbe5d1e174bf6381ae11e2222))
* update README with ApplyEdits, retry, streaming examples ([eb26619](https://github.com/honua-io/honua-sdk-dotnet/commit/eb26619e1937ae4368b37ff0e2610475b593a560))

## [0.1.0-alpha.1] - Unreleased

### Added

- Admin client SDK (`Honua.Sdk.Admin`) for managing services, layers, and configuration
- gRPC client SDK (`Honua.Sdk.Grpc`) for FeatureService queries and edits
- WFS 2.0 read/query client SDK (`Honua.Sdk.Wfs`) for GetCapabilities, GetFeature, DescribeFeatureType
- DI registration extensions (`AddHonuaGrpc`, `AddHonuaAdmin`, `AddHonuaGeocoding`, `AddHonuaWfs`)
- Typed request/response models for feature queries
- Automatic retry with exponential backoff and jitter for gRPC and WFS clients
