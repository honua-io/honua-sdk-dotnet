# Plugin contracts

`Honua.Sdk.Abstractions.Plugins` defines the host-neutral plugin contract
surface shared by the Honua server, admin app, mobile shell, and web shell.
The SDK owns the portable data model and validation rules. The contracts
cover manifest metadata, host compatibility, edition gates, capability
flags, permission declarations, safe configuration envelopes, and non-UI
extension point descriptors. Reach for this namespace when you need to
parse, validate, audit, or generate a Honua plugin manifest from .NET code
without taking a dependency on any specific host runtime.

Host repos still own runtime behavior: MAUI assembly loading, React/Vue
registration, map controls, custom renderers, marketplace UX, sandbox
runtime isolation, code signing, and any host-specific plugin execution
pipeline. Server-side plugin execution and policy enforcement is tracked in
`honua-server#347`.

## Types you'll touch

All types live in `Honua.Sdk.Abstractions.Plugins`
(see [src/Honua.Sdk.Abstractions/Plugins/HonuaPluginContracts.cs](../src/Honua.Sdk.Abstractions/Plugins/HonuaPluginContracts.cs)).

| Type | Role |
|---|---|
| `HonuaPluginManifest` | Top-level manifest record. Exposes `ParseJson`, `ToJson`, and `Validate`. The schema version constant is `HonuaPluginManifest.CurrentSchemaVersion` (`"honua.plugin.v1"`). |
| `HonuaPluginCompatibility` | `SupportedHosts`, `MinSdkVersion`/`MaxSdkVersion`, `MinServerVersion`/`MaxServerVersion`, `RequiredFeatureFlags`. |
| `HonuaPluginPermissionDeclaration` | `Permission`, `Access`, `Required`, `Reason` — the auditable permission request a host surfaces to operators. |
| `HonuaPluginConfigurationEnvelope` | Bounded envelope (`MaxSerializedBytes`, `Fields`, `Defaults`) for safe host-managed configuration. |
| `HonuaPluginConfigurationField` | `Key`, `Type`, `Required`, `Sensitive`, `MaxLength`, `AllowedValues`, `Description`. |
| `HonuaPluginExtensionPoint` | Non-UI extension descriptor: `ExtensionId`, `Type`, `Target`, `Handler`, `ConfigurationKey`, `Order`, `Input`, `Output`. |
| `HonuaPluginDataContract` | `SchemaRef` + `Tags` for the payload of an extension point. |
| `HonuaPluginManifestValidator` | Static `Validate(manifest)` that returns a `HonuaPluginValidationResult`. |
| `HonuaPluginValidationResult` / `HonuaPluginValidationIssue` | `IsValid`, `HasWarnings`, and the issue list with `Code`, `Message`, `Path`, `Severity`. |
| `HonuaPluginHostKinds` | Constants: `Mobile`, `Web`, `Server`, `Admin`, `Cli`, `Worker`. |
| `HonuaPluginEditionGates` | Constants: `Community`, `Pro`, `Enterprise`, `Internal`. |
| `HonuaPluginPermissionAccess` | Constants: `Read`, `Write`, `Invoke`, `Manage`. |
| `HonuaPluginExtensionTypes` | Constants: `FieldValidator`, `CalculatedField`, `DataTransformer`, `WorkflowHook`. |
| `HonuaPluginConfigurationTypes` | Constants: `Text` (`"string"`), `Numeric` (`"number"`), `Bool` (`"boolean"`), `Uri`, `Enum`, `Json`. |
| `HonuaPluginValidationCodes` | Machine-readable issue codes (`UnsupportedSchemaVersion`, `MissingRequiredValue`, `InvalidIdentifier`, `ValueTooLong`, `DuplicateDeclaration`, `UnsupportedHost`, `UnsupportedEditionGate`, `UnsupportedPermissionAccess`, `UnsupportedExtensionType`, `UnsupportedConfigurationType`, `UnsafeConfigurationEnvelope`, `SensitiveDefaultValue`, `UnknownConfigurationKey`). |

## Manifest shape

`HonuaPluginManifest` deserializes from a JSON document with the following
shape. Property names are case-insensitive on read; `ToJson` emits camelCase.

```json
{
  "schemaVersion": "honua.plugin.v1",
  "pluginId": "io.example.parcels.validator",
  "displayName": "Parcel Validator",
  "publisher": "Example, Inc.",
  "version": "1.2.0",
  "description": "Validates parcel polygons against the county overlay.",
  "editionGate": "pro",
  "compatibility": {
    "supportedHosts": ["server", "admin"],
    "minSdkVersion": "0.1.0",
    "minServerVersion": "0.10.0",
    "requiredFeatureFlags": ["spec.apply.v2"]
  },
  "capabilities": ["validation.parcel"],
  "permissions": [
    {
      "permission": "features.parcels",
      "access": "read",
      "required": true,
      "reason": "Read the parcels source to validate proposed edits."
    }
  ],
  "configuration": {
    "maxSerializedBytes": 8192,
    "fields": [
      { "key": "overlayUrl", "type": "uri", "required": true, "maxLength": 512 },
      { "key": "apiKey",     "type": "string", "required": true, "sensitive": true, "maxLength": 256 }
    ],
    "defaults": {}
  },
  "extensions": [
    {
      "extensionId": "parcel.geometry.validator",
      "type": "field-validator",
      "target": "parcels.geometry",
      "handler": "ParcelGeometryValidator",
      "configurationKey": "overlayUrl",
      "order": 100,
      "input":  { "schemaRef": "honua://schemas/feature/geometry", "tags": ["geometry", "polygon"] },
      "output": { "schemaRef": "honua://schemas/validation/result" }
    }
  ],
  "metadata": { "support.email": "ops@example.com" }
}
```

## Permissions and edition gates

Each `HonuaPluginPermissionDeclaration` is a triple of `Permission`
(scope, dot-segmented identifier), `Access` (one of
`HonuaPluginPermissionAccess.Read`/`Write`/`Invoke`/`Manage`), and
`Reason` (human-readable, surfaced in audit logs). `Required = true`
means the plugin will refuse to load if the host denies the request.

The `EditionGate` is a soft gate the SDK validates against
`HonuaPluginEditionGates` (`community`, `pro`, `enterprise`, `internal`).
Hosts decide whether to honour or override the gate; the SDK only ensures
the value is known.

## Validator, transformer, workflow-hook contract

The four extension `Type` values describe what kind of work the plugin
performs. `Target` names the host symbol the extension binds to (for
example, `parcels.geometry` for a field validator) and `Handler` is the
symbolic key the host resolves to actual code.

- `field-validator` — runs against a single field value, returns a
  validation result.
- `calculated-field` — produces a value for a derived field.
- `data-transformer` — transforms an input payload to an output payload,
  using the optional `Input`/`Output` `HonuaPluginDataContract` to describe
  the schemas.
- `workflow-hook` — fires on a host-defined workflow event keyed by
  `Target`.

`HonuaPluginExtensionPoint.Order` is a relative integer hosts use to
compose multiple extensions on the same target. The SDK does not run
extensions; it only validates the descriptor.

## Worked example: parse, validate, inspect

```csharp
using System.IO;
using Honua.Sdk.Abstractions.Plugins;

var json = File.ReadAllText("plugin-manifest.json");
var manifest = HonuaPluginManifest.ParseJson(json);

var result = manifest.Validate();
if (!result.IsValid)
{
    foreach (var issue in result.Issues)
    {
        Console.Error.WriteLine($"[{issue.Severity}] {issue.Code} @ {issue.Path}: {issue.Message}");
    }
    return 1;
}

Console.WriteLine($"Loaded {manifest.PluginId} v{manifest.Version} by {manifest.Publisher}.");
foreach (var extension in manifest.Extensions)
{
    Console.WriteLine($"  - {extension.Type}: {extension.ExtensionId} -> {extension.Handler}");
}

return 0;
```

The shared fixture at `contracts/fixtures/plugin-manifest.v1.json` is
intended for mobile, web, server, and admin tests. Load it, call
`HonuaPluginManifest.ParseJson(...).Validate()`, and map the resulting
contract into your runtime adapter.

## How it composes

- These contracts are the only plugin types in the SDK; everything else
  (sandbox runtime, UI registration, marketplace) lives in the host repos.
- Hosts that load a manifest typically chain: parse with `ParseJson`,
  call `Validate`, persist the manifest, hand the resolved
  `Handler` strings to their own DI container, and execute extensions in
  `Order`.
- `HonuaPluginConfigurationEnvelope` is the only shape the SDK lets a host
  expose to operators without loading plugin code; it is intentionally
  bounded and validated.

## Pitfalls

- `SchemaVersion` must equal `HonuaPluginManifest.CurrentSchemaVersion`
  (`"honua.plugin.v1"`). Anything else yields a
  `HonuaPluginValidationCodes.UnsupportedSchemaVersion` error:
  `"Plugin manifest schema version is not supported."`
- `HonuaPluginManifest.ParseJson` throws `FormatException` for empty,
  malformed, or non-object JSON (`"Plugin manifest JSON is required."`,
  `"Plugin manifest JSON did not contain an object."`,
  `"Plugin manifest JSON was malformed."`). It does not return a
  partially-populated manifest on bad input.
- A configuration default keyed to a `Sensitive` field is rejected with
  `HonuaPluginValidationCodes.SensitiveDefaultValue`:
  `"Sensitive configuration fields must not declare default values."`
- `HonuaPluginExtensionPoint.Handler` must be a safe symbolic reference
  (ASCII letters/digits plus `.`, `-`, `_`, `:`). Anything else fails with
  `InvalidIdentifier`: `"Extension handler is not a safe symbolic reference."`
- `Compatibility.SupportedHosts` cannot be empty. `Permissions[*].Reason`
  is required and surfaced verbatim in operator-visible audits, so keep it
  human-readable.

## See also

- [src/Honua.Sdk.Abstractions/README.md](../src/Honua.Sdk.Abstractions/README.md)
  — package overview.
- [authentication.md](authentication.md) — how hosts authenticate plugin
  manifest uploads.
- [troubleshooting.md](troubleshooting.md) — error-code lookup for
  `HonuaPluginValidationCodes`.
- [architecture.md](architecture.md) — where plugin contracts sit in the
  SDK layering.
- [spec-workspace-contracts.md](spec-workspace-contracts.md) — plugins
  surface in Spec workspaces as extension targets.
