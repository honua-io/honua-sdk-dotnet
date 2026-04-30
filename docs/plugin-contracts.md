# Plugin Contracts

`Honua.Sdk.Abstractions.Plugins` defines the host-neutral plugin contract
surface. It covers manifest metadata, host compatibility, edition gates,
capability flags, permission declarations, safe configuration envelopes, and
non-UI extension point descriptors.

The SDK owns these portable contracts:

- `HonuaPluginManifest` for JSON manifests.
- `HonuaPluginManifestValidator` for shared manifest validation.
- `HonuaPluginCompatibility` for host, SDK, server, and feature-flag gates.
- `HonuaPluginPermissionDeclaration` for auditable permission requests.
- `HonuaPluginConfigurationEnvelope` and `HonuaPluginConfigurationField` for
  safe host-managed configuration.
- `HonuaPluginExtensionPoint` for field validators, calculated fields, data
  transformers, and workflow hooks.

Host repos still own runtime behavior: MAUI assembly loading, React/Vue
registration, map controls, custom renderers, marketplace UX, sandbox runtime
isolation, code signing, and any host-specific plugin execution pipeline.

The shared fixture at `contracts/fixtures/plugin-manifest.v1.json` is intended
for mobile, web, server, and admin tests. Host repos should load the fixture,
validate it with `HonuaPluginManifest.ParseJson(...).Validate()`, and then map
the resulting contract into their own runtime adapter.

Server-side plugin execution and policy enforcement remains tied to
`honua-server#347`.
