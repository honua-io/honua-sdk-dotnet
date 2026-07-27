#!/usr/bin/env python3
"""Generate and validate contracts/sdk-coverage.v1.json (issue #273).

This is the .NET SDK's producer snapshot for the cross-repo capability
matrix: for every canonical capability key this SDK's source actually
implements, it records a coverage status (``covered`` or ``partial``),
a ``sinceVersion`` marker, and entrypoint references (the public
type/method that provides the coverage). Capabilities this SDK does not
touch are omitted entirely -- never padded with an invented ``covered`` or
``partial`` entry.

Consume-never-copy
-------------------
The canonical 110-key capability vocabulary is published by
honua-io/honua-server (honua-io/honua-server#2893) at
``docs/gis/data/capability-keys.v1.json``. This script never redefines that
vocabulary -- it only *references* keys from it (and fails loudly if a
mapped key does not exist in the resolved canonical list). Key resolution
order:

  1. ``KEY_LIST_URL`` env var, if set to an http(s) URL -- fetched at run
     time. This is the one-line swap point once the published artifact
     moves or CI wants to pin a specific ref.
  2. ``contracts/fixtures/capability-keys.fixture.json`` -- a pinned,
     byte-for-byte mirror of the published file, committed in this repo so
     CI and offline runs do not depend on network access. Refresh it by
     re-downloading the canonical URL below; nothing else needs to change.

Canonical URL (also the ``KEY_LIST_URL`` default a caller would set):
  https://raw.githubusercontent.com/honua-io/honua-server/trunk/docs/gis/data/capability-keys.v1.json

Partial-requires-a-note
------------------------
Every entry with ``status: partial`` must carry a non-empty ``note`` that
says, in concrete terms, where SDK coverage stops. This is enforced by
``_validate_entries`` below, not just convention -- a partial entry with no
note fails the build.

Entrypoints-must-exist (source-truth gate)
-------------------------------------------
Every ``entrypoints`` reference must resolve against the actual SDK source
under ``src/``: the referenced type must be *publicly* declared in some
``.cs`` file (real declaration syntax on comment/string-stripped text --
``public [modifiers] class|interface|record|struct|enum Name``), and a
``Type.Member`` reference must additionally match an accessible,
declaration-shaped member in a file declaring that type. Occurrences that
survive only in comments, XML doc prose, or string literals never count,
and a type demoted to ``internal`` stops resolving. This is what makes the
CI drift gate honest about *renames, deletions, and de-publicizing*, not
just stale regeneration: any of those without updating the inventory fails
``--check`` instead of leaving the snapshot claiming coverage that no
longer exists.

Usage
-----
  python3 scripts/generate-sdk-coverage.py            # (re)writes contracts/sdk-coverage.v1.json
  python3 scripts/generate-sdk-coverage.py --check     # drift gate: fails if committed file is stale
  python3 scripts/generate-sdk-coverage.py --summary <path>   # also append a GitHub step summary
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
SRC_ROOT = ROOT / "src"
OUTPUT_PATH = ROOT / "contracts" / "sdk-coverage.v1.json"
FIXTURE_PATH = ROOT / "contracts" / "fixtures" / "capability-keys.fixture.json"
CANONICAL_URL = (
    "https://raw.githubusercontent.com/honua-io/honua-server/trunk/"
    "docs/gis/data/capability-keys.v1.json"
)
TRACKING_ISSUE = "#273"
SOURCE_REPOSITORY = "honua-io/honua-sdk-dotnet"

# This SDK ships as a source preview only: no NuGet package has published yet
# (see honua-site data/sdk-availability.v1.json, productArea "sdk-dotnet",
# availability "Source preview", publishedVersion null). Every coverage entry
# below therefore carries the literal marker "unreleased" rather than a
# fabricated version number -- repo tags such as dotnet-sdk-v1.5.0 are
# pre-publish release-please source tags, not registry releases, and must not
# be used here to imply availability that does not exist yet.
UNRELEASED = "unreleased"

VALID_STATUSES = frozenset({"covered", "partial"})

# ---------------------------------------------------------------------------
# The inventory: canonical capability key -> what this SDK's source actually
# implements. Add an entry only when you can point at a real public type or
# member; remove an entry the moment the mapped surface is deleted or
# renamed. Never add a "none" entry -- an untouched capability is simply
# absent from this list.
# ---------------------------------------------------------------------------
COVERAGE_ENTRIES: list[dict[str, Any]] = [
    {
        "key": "admin.control-plane",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.Admin.IHonuaAdminConnectionsClient",
            "Honua.Sdk.Admin.IHonuaAdminServicesClient",
            "Honua.Sdk.Admin.IHonuaAdminUsersClient",
            "Honua.Sdk.Admin.IHonuaAdminRolesClient",
            "Honua.Sdk.Admin.IHonuaAdminConfigClient",
            "Honua.Sdk.Admin.IHonuaAdminMetadataClient",
        ],
    },
    {
        "key": "ai.spec-apply",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.Spec.IHonuaSpecClient.ApplyAsync",
            "Honua.Sdk.Spec.IHonuaSpecClient.CancelAsync",
        ],
    },
    {
        "key": "ai.spec-artifacts",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.Spec.IHonuaSpecClient.GetArtifactAsync"],
    },
    {
        "key": "discovery.capability-manifest",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.Studio.Capabilities.IHonuaCapabilityManifestClient.GetManifestAsync",
        ],
    },
    {
        "key": "editing.featureserver-edits",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.GeoServices.FeatureServer.IHonuaFeatureServerEditClient.ApplyEditsAsync",
        ],
    },
    {
        "key": "fieldops.forms",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.Field.Forms.FormValidator",
            "Honua.Sdk.Field.Forms.FieldFormSchemaMapper",
            "Honua.Sdk.Field.Forms.CalculatedFieldEvaluator",
            "Honua.Sdk.Field.Records.RecordWorkflow",
            "Honua.Sdk.Field.Records.DuplicateDetector",
        ],
    },
    {
        "key": "fieldops.offline-sync",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.Offline.OfflineSyncEngine",
            "Honua.Sdk.Offline.OfflineDownloadPlanner",
            "Honua.Sdk.Offline.ReplicaSyncClient",
        ],
    },
    {
        "key": "geocoding.batch",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.Admin.Geocoding.IHonuaGeocodingClient.BatchGeocodeAsync",
            "Honua.Sdk.Admin.Geocoding.IHonuaBatchGeocodingClient.BatchGeocodeDetailedAsync",
        ],
    },
    {
        "key": "geocoding.forward",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.Admin.Geocoding.IHonuaGeocodingClient.ForwardGeocodeAsync"],
    },
    {
        "key": "geocoding.reverse",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.Admin.Geocoding.IHonuaGeocodingClient.ReverseGeocodeAsync"],
    },
    {
        "key": "identity.oidc",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.Admin.IHonuaAdminIdentityClient.CreateOidcProviderAsync",
            "Honua.Sdk.Admin.IHonuaAdminIdentityClient.TestOidcProviderAsync",
        ],
    },
    {
        "key": "identity.oidc-multi-provider",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.Admin.IHonuaAdminIdentityClient.ListOidcProvidersAsync",
            "Honua.Sdk.Admin.IHonuaAdminIdentityClient.GetIdentityProvidersAsync",
        ],
    },
    {
        "key": "identity.portal-sharing",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.ConsoleShare.IHonuaConsoleShareClient.UpdateAccessAsync",
            "Honua.Sdk.ConsoleShare.IHonuaConsoleShareClient.CreatePublicLinkAsync",
        ],
    },
    {
        "key": "identity.portal-token",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.ConsoleShare.IHonuaConsoleShareClient.CreateEmbedTokenAsync",
            "Honua.Sdk.ConsoleShare.IHonuaConsoleShareClient.RevokeEmbedTokenAsync",
        ],
    },
    {
        "key": "ops.observability",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.Admin.IHonuaAdminObservabilityClient"],
    },
    {
        "key": "plugin.sdk",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.Abstractions.Plugins.HonuaPluginManifest"],
    },
    {
        "key": "process.ogc-api-processes",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.Processes.IHonuaProcessesClient"],
    },
    {
        "key": "routing.solve",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.GeoServices.Routing.HonuaRoutingClient"],
    },
    {
        "key": "scene.catalog",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.Scenes.HonuaSceneClient.ListScenesAsync"],
    },
    {
        "key": "serve.geoservices-featureserver",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.GeoServices.FeatureServer.HonuaFeatureServerClient"],
    },
    {
        "key": "serve.geoservices-geocodeserver",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.Admin.Geocoding.IHonuaGeocodingClient"],
    },
    {
        "key": "serve.geoservices-geometry-service",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.GeoServices.GeometryServer.HonuaGeometryServerClient"],
    },
    {
        "key": "serve.geoservices-imageserver",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.GeoServices.ImageServer.HonuaImageServerClient",
            "Honua.Sdk.GeoServices.ImageServer.HonuaImageServerRasterDataClient",
        ],
    },
    {
        "key": "serve.ogc-api-features",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.OgcFeatures.HonuaOgcFeaturesClient",
            "Honua.Sdk.OgcFeatures.IHonuaOgcFeaturesEditClient",
            "Honua.Sdk.OgcFeatures.IHonuaOgcFeaturesPatchClient",
        ],
    },
    {
        "key": "serve.ogc-api-records",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.Catalogs.Records.HonuaOgcRecordsClient"],
    },
    {
        "key": "serve.stac",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.Catalogs.Stac.HonuaStacClient"],
    },
    {
        "key": "streaming.feature-subscriptions",
        "status": "covered",
        "entrypoints": [
            "Honua.Sdk.Abstractions.Features.IHonuaFeatureStreamClient.SubscribeAsync",
            "Honua.Sdk.Admin.IHonuaAdminStreamingOperationsClient",
        ],
    },
    {
        "key": "styling.ogc-api-styles",
        "status": "covered",
        "entrypoints": ["Honua.Sdk.OgcFeatures.Styles.IHonuaOgcStylesClient"],
    },
    {
        "key": "alerts.threshold",
        "status": "partial",
        "entrypoints": ["Honua.Sdk.Admin.IHonuaAdminAlertsClient"],
        "note": (
            "Generic alert-rule CRUD (CreateAlertRuleAsync/UpdateAlertRuleAsync) "
            "passes AlertRuleRequest.TriggerType and Channels through as opaque "
            "strings. The SDK ships no typed threshold-specific request/response "
            "shape and does not validate threshold semantics client-side."
        ),
    },
    {
        "key": "alerts.dwell",
        "status": "partial",
        "entrypoints": ["Honua.Sdk.Admin.IHonuaAdminAlertsClient"],
        "note": (
            "Same generic AlertRuleRequest.TriggerType/Channels passthrough as "
            "alerts.threshold; no dwell-duration-specific typed fields or "
            "client-side dwell evaluation ships in this SDK."
        ),
    },
    {
        "key": "alerts.enter-exit",
        "status": "partial",
        "entrypoints": ["Honua.Sdk.Admin.IHonuaAdminAlertsClient"],
        "note": (
            "Alert-zone CRUD (ListAlertZonesAsync/CreateAlertZoneAsync/etc.) plus "
            "generic rule CRUD supports configuring enter/exit zones, but "
            "TriggerType remains an opaque string -- the SDK has no typed "
            "enter-exit request shape or client-side geofence evaluation for "
            "alerting."
        ),
    },
    {
        "key": "analytics.reporting",
        "status": "partial",
        "entrypoints": [
            "Honua.Sdk.Studio.IHonuaStudioReportsClient.GetReportAsync",
            "Honua.Sdk.Studio.IHonuaStudioReportsClient.RenderReportAsync",
        ],
        "note": (
            "Read/render only: retrieves and renders (Markdown/HTML) a structured "
            "analysis report for an already-completed job. The SDK does not submit "
            "or configure the analytics/report-generating job itself -- that goes "
            "through the generic IHonuaProcessesClient/IHonuaProcessGrpcClient job "
            "submission, not a typed analytics wrapper."
        ),
    },
    {
        "key": "enrichment.datasets",
        "status": "partial",
        "entrypoints": ["Honua.Sdk.Abstractions.Data.IHonuaEnrichmentDataClient"],
        "note": (
            "Honua.Sdk.Abstractions ships IHonuaEnrichmentDataClient as a "
            "provider-neutral contract (GetEnrichmentMetadataAsync/EnrichAsync), "
            "but this SDK contains no concrete HTTP-backed implementation of it -- "
            "no HonuaEnrichmentDataClient class exists in src/ yet."
        ),
    },
    {
        "key": "serve.elevation",
        "status": "partial",
        "entrypoints": ["Honua.Sdk.Abstractions.Data.IHonuaElevationDataClient"],
        "note": (
            "Same contract-only situation as enrichment.datasets: "
            "IHonuaElevationDataClient (SampleElevationAsync) is a shipped "
            "abstraction with no concrete implementing class in this SDK yet."
        ),
    },
    {
        "key": "serve.wfs",
        "status": "partial",
        "entrypoints": ["Honua.Sdk.OgcFeatures.Wfs.IHonuaWfsClient"],
        "note": (
            "WFS 2.0 read/query only (GetCapabilitiesAsync, "
            "DescribeFeatureTypeAsync, GetFeaturesAsync, hits-only count, "
            "auto-paging). No WFS-T Transaction (insert/update/delete) support "
            "ships in this SDK."
        ),
    },
    {
        "key": "serve.3d-tiles-scene",
        "status": "partial",
        "entrypoints": ["Honua.Sdk.Scenes.HonuaSceneClient"],
        "note": (
            "The scene client discovers and resolves a scene's 3D Tiles endpoint "
            "URL and capability tag (HonuaSceneCapabilities.ThreeDimensionalTiles); "
            "it does not parse or serve the 3D Tiles/Cesium tileset protocol "
            "itself. Renderer/display code is explicitly out of scope for this SDK "
            "(see AGENTS.md 'Does Not Belong Here')."
        ),
    },
    {
        "key": "serve.i3s-scene",
        "status": "partial",
        "entrypoints": ["Honua.Sdk.Scenes.HonuaSceneClient"],
        "note": (
            "Same as serve.3d-tiles-scene: the scene client resolves the I3S "
            "endpoint URL and capability tag (HonuaSceneCapabilities.I3s) only; "
            "it does not parse or serve the I3S scene-layer protocol itself."
        ),
    },
    {
        "key": "styling.defaults",
        "status": "partial",
        "entrypoints": ["Honua.Sdk.OgcFeatures.Styles.IHonuaOgcStylesClient.ListStylesAsync"],
        "note": (
            "Read-only: the OgcStylesList.Default field surfaces which style is "
            "the server-declared default. The SDK has no call to designate or "
            "change the default style."
        ),
    },
    {
        "key": "import.geoserver",
        "status": "partial",
        "entrypoints": ["Honua.Sdk.Admin.HonuaAdminMigrationClientExtensions.ScanMigrationSourceAsync"],
        "note": (
            "Covers migration-source scanning and inventory/manifest/"
            "parity-evidence/readiness-attestation generation only "
            "(sourceKind: \"geoserver\"), producing "
            "MigrationSourceInventoryArtifact / MigrationManifestArtifact / "
            "MigrationParityEvidenceArtifact / MigrationReadinessAttestation. The "
            "SDK has no apply/cutover call that loads a scanned GeoServer "
            "source's data or config into Honua."
        ),
    },
    {
        "key": "import.geoservices",
        "status": "partial",
        "entrypoints": ["Honua.Sdk.Admin.HonuaAdminMigrationClientExtensions.ScanMigrationSourceAsync"],
        "note": (
            "Same as import.geoserver: scan/manifest/parity-evidence/"
            "readiness-attestation generation only (sourceKind: \"geoservices\"), "
            "no apply/execute call."
        ),
    },
    {
        "key": "process.geoprocessing",
        "status": "partial",
        "entrypoints": [
            "Honua.Sdk.Processes.IHonuaProcessesClient.SubmitJobAsync",
            "Honua.Sdk.Grpc.IHonuaProcessGrpcClient.SubmitJobAsync",
        ],
        "note": (
            "Generic job submission/poll/cancel by processId works against any "
            "registered geoprocessing tool -- this is also the only path this SDK "
            "offers for GP-backed analytics.* operations; it has no typed "
            "per-tool wrappers. IHonuaProcessGrpcClient's own doc comments flag "
            "that its synchronous ExecutePlanAsync/ExecutePlanStreamAsync methods "
            "may return Unimplemented on current Honua Server deployments -- the "
            "reliable path is async submit-then-poll."
        ),
    },
]


def _load_canonical_keys() -> tuple[frozenset[str], str]:
    """Resolves the canonical capability key set and where it came from."""

    url = os.environ.get("KEY_LIST_URL", "").strip()
    if url:
        if not url.lower().startswith(("http://", "https://")):
            raise ValueError(
                f"KEY_LIST_URL is set to {url!r} but is not an http(s) URL. "
                "Unset it to fall back to the pinned fixture, or point it at "
                "the published capability-keys.v1.json."
            )
        with urllib.request.urlopen(url, timeout=30) as response:  # noqa: S310 (explicit http(s) check above)
            payload = json.load(response)
        return _extract_keys(payload), f"KEY_LIST_URL ({url})"

    payload = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))
    return (
        _extract_keys(payload),
        "contracts/fixtures/capability-keys.fixture.json (pinned mirror -- see KEY_LIST_URL)",
    )


def _extract_keys(payload: Any) -> frozenset[str]:
    if isinstance(payload, list):
        return frozenset(payload)
    if isinstance(payload, dict) and isinstance(payload.get("capabilities"), list):
        return frozenset(item["key"] for item in payload["capabilities"] if isinstance(item, dict) and "key" in item)
    if isinstance(payload, dict) and isinstance(payload.get("keys"), list):
        return frozenset(payload["keys"])
    raise ValueError(
        "capability key list must be an array of strings, an object with a "
        "'keys' array, or the canonical {'capabilities': [{'key': ...}, ...]} shape"
    )


def _validate_entries(entries: list[dict[str, Any]], canonical_keys: frozenset[str]) -> None:
    seen: set[str] = set()
    errors: list[str] = []

    for entry in entries:
        key = entry.get("key")
        status = entry.get("status")
        entrypoints = entry.get("entrypoints")
        note = entry.get("note")

        if not isinstance(key, str) or not key:
            errors.append(f"entry missing a string 'key': {entry!r}")
            continue

        if key in seen:
            errors.append(f"duplicate coverage entry for key {key!r}")
        seen.add(key)

        if key not in canonical_keys:
            errors.append(
                f"unknown capability key {key!r} -- not in the resolved canonical "
                "key list. Fix the key, or confirm honua-server#2893's published "
                "list actually contains it before mapping to it."
            )

        if status not in VALID_STATUSES:
            errors.append(
                f"{key!r}: status must be one of {sorted(VALID_STATUSES)}, got {status!r}. "
                "Untouched capabilities must be omitted entirely, never recorded as 'none'."
            )

        if not isinstance(entrypoints, list) or not entrypoints or not all(isinstance(e, str) and e for e in entrypoints):
            errors.append(f"{key!r}: 'entrypoints' must be a non-empty list of non-empty strings")

        if status == "partial" and not (isinstance(note, str) and note.strip()):
            errors.append(f"{key!r}: status is 'partial' but has no non-empty 'note' saying where coverage stops")

        if status == "covered" and note is not None:
            errors.append(f"{key!r}: status is 'covered' but carries a 'note' -- notes are reserved for 'partial'")

    if errors:
        raise ValueError("sdk-coverage.v1.json validation failed:\n" + "\n".join(f"  - {e}" for e in errors))


_NAMESPACE_RE = re.compile(r"^\s*namespace\s+([A-Za-z_][\w.]*)", re.MULTILINE)

# A *real, public* type declaration: the `public` keyword, optional
# additional modifiers, then a type keyword and the type name. Matching is
# done against comment/string-stripped text (see _strip_comments_and_strings)
# so a declaration that only survives inside a comment, XML doc line, or
# string literal never counts, and a type demoted to `internal` (or default
# accessibility) drops out of the index -- exactly the cases where the
# snapshot would otherwise keep advertising a public entrypoint that no
# longer exists. Nested public types and `public partial` declarations
# spread across files all match (each declaring file lands in the index).
_PUBLIC_TYPE_DECL_RE = re.compile(
    r"\bpublic\s+(?:(?:sealed|static|abstract|partial|readonly|ref|unsafe|new)\s+)*"
    r"(?:class|interface|enum|struct|record(?:\s+(?:class|struct))?)\s+([A-Za-z_]\w*)"
)


def _strip_comments_and_strings(text: str) -> str:
    """Blanks out comments and string/char literals from C# source.

    Keeps the gate lexical and stdlib-only while making sure that ``//`` and
    ``///`` line comments, ``/* */`` block comments, ordinary/interpolated
    string literals (with backslash escapes), verbatim ``@"..."`` strings
    (with ``\"\"`` escapes), raw ``\"\"\"...\"\"\"`` strings, and char
    literals can never satisfy a type or member existence check. Newlines are
    preserved so surrounding code structure stays intact.
    """

    out: list[str] = []
    i, n = 0, len(text)
    while i < n:
        ch = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if ch == "/" and nxt == "/":
            end = text.find("\n", i)
            i = n if end == -1 else end  # keep the newline itself
        elif ch == "/" and nxt == "*":
            end = text.find("*/", i + 2)
            stop = n if end == -1 else end + 2
            out.append("".join(c if c == "\n" else " " for c in text[i:stop]))
            i = stop
        elif ch == "@" and nxt == '"':
            j = i + 2
            while j < n:
                if text[j] == '"':
                    if j + 1 < n and text[j + 1] == '"':
                        j += 2
                        continue
                    break
                j += 1
            out.append(" ")
            i = j + 1
        elif ch == '"':
            if text.startswith('"""', i):
                end = text.find('"""', i + 3)
                out.append(" ")
                i = n if end == -1 else end + 3
            else:
                j = i + 1
                while j < n and text[j] != '"':
                    j += 2 if text[j] == "\\" else 1
                out.append(" ")
                i = j + 1
        elif ch == "'":
            j = i + 1
            while j < n and text[j] != "'":
                j += 2 if text[j] == "\\" else 1
            out.append(" ")
            i = j + 1
        else:
            out.append(ch)
            i += 1
    return "".join(out)


def _build_source_type_index(src_root: Path) -> dict[str, list[Path]]:
    """Indexes every *public* type declared under ``src/`` as ``Namespace.TypeName``.

    Deliberately lexical (regex over comment/string-stripped ``.cs`` files,
    no Roslyn): the entrypoint strings in ``COVERAGE_ENTRIES`` are flat
    ``Namespace.Type`` or ``Namespace.Type.Member`` references, so namespace
    declarations plus public type-declaration syntax are enough to resolve
    them, and this keeps the gate runnable in the same dependency-free
    python step CI already uses.
    """

    index: dict[str, list[Path]] = {}
    for path in sorted(src_root.rglob("*.cs")):
        text = _strip_comments_and_strings(path.read_text(encoding="utf-8", errors="replace"))
        namespaces = _NAMESPACE_RE.findall(text)
        type_names = _PUBLIC_TYPE_DECL_RE.findall(text)
        for namespace in namespaces or [""]:
            for type_name in type_names:
                full_name = f"{namespace}.{type_name}" if namespace else type_name
                index.setdefault(full_name, []).append(path)
    return index


# Header tokens that mark a candidate member match as something other than an
# accessible declaration: explicit non-public accessibility, or statement
# keywords that mean the name is being *invoked/consumed* rather than declared.
_MEMBER_REJECT_TOKENS = frozenset({"private", "protected", "return", "await", "throw", "yield"})


def _member_publicly_declared(member: str, stripped_text: str) -> bool:
    """True when ``stripped_text`` plausibly *declares* an accessible ``member``.

    A declaration-shaped occurrence is the member name followed by ``(`` or
    ``<...>(`` (method/ctor), or by ``{``, ``=>``, ``;``, or ``=`` (property,
    field, event, expression-bodied member). The declaration header -- the
    text between the previous ``;``/``{``/``}`` and the name -- must not
    carry ``private``/``protected`` (or ``internal`` without ``public``;
    interface members legitimately carry no modifier at all) and must not
    look like an invocation context. Input must already be comment/string
    stripped, so a renamed method whose old name lingers only in XML doc
    prose, a comment, or a string no longer resolves.
    """

    declaration_re = re.compile(
        rf"(?<![\w.]){re.escape(member)}\s*(?:<[^<>]{{0,120}}>)?\s*(?:\(|\{{|=>|;|=(?!=))"
    )
    for match in declaration_re.finditer(stripped_text):
        start = match.start()
        header_start = max(
            stripped_text.rfind(";", 0, start),
            stripped_text.rfind("{", 0, start),
            stripped_text.rfind("}", 0, start),
        ) + 1
        header = stripped_text[header_start:start]
        tokens = set(re.findall(r"[A-Za-z_]\w*", header))
        if tokens & _MEMBER_REJECT_TOKENS:
            continue
        if "internal" in tokens and "public" not in tokens:
            continue
        trailing = header.rstrip()
        if trailing and trailing[-1] in "=(,&|?:!+-*/":
            continue  # argument/operand position -- an invocation, not a declaration
        return True
    return False


def _validate_entrypoints_against_source(
    entries: list[dict[str, Any]],
    type_index: dict[str, list[Path]],
) -> None:
    """Source-truth gate: every entrypoint must resolve to real SDK source.

    An entrypoint is either ``Namespace.TypeName`` (must be a *publicly*
    declared type) or ``Namespace.TypeName.MemberName`` (the type must be
    publicly declared and the member name must appear as an accessible,
    declaration-shaped member in a file declaring that type -- see
    ``_member_publicly_declared``). A reference that no longer resolves --
    because the surface was deleted, renamed, or demoted from ``public`` --
    fails generation and the CI ``--check`` drift gate.
    """

    errors: list[str] = []
    file_text_cache: dict[Path, str] = {}

    for entry in entries:
        key = entry.get("key", "<missing key>")
        for entrypoint in entry.get("entrypoints", []):
            if not isinstance(entrypoint, str) or not entrypoint:
                continue  # shape errors are _validate_entries' job
            if entrypoint in type_index:
                continue
            parent, _, member = entrypoint.rpartition(".")
            declaring_files = type_index.get(parent, [])
            if declaring_files and member:
                found = False
                for path in declaring_files:
                    if path not in file_text_cache:
                        file_text_cache[path] = _strip_comments_and_strings(
                            path.read_text(encoding="utf-8", errors="replace")
                        )
                    if _member_publicly_declared(member, file_text_cache[path]):
                        found = True
                        break
                if found:
                    continue
                errors.append(
                    f"{key!r}: entrypoint {entrypoint!r} names member {member!r}, but no file "
                    f"declaring {parent!r} declares an accessible member by that name "
                    "(occurrences in comments, XML docs, and strings do not count). The member "
                    "was removed or renamed -- update or drop this coverage entry."
                )
                continue
            errors.append(
                f"{key!r}: entrypoint {entrypoint!r} does not resolve to any type publicly declared "
                "under src/. The surface was removed, renamed, or made non-public -- update or drop "
                "this coverage entry so the snapshot stops claiming coverage that no longer exists."
            )

    if errors:
        raise ValueError(
            "sdk-coverage.v1.json entrypoint source-truth check failed:\n"
            + "\n".join(f"  - {e}" for e in errors)
        )


def _build_document(canonical_keys: frozenset[str], key_list_source: str) -> dict[str, Any]:
    ordered = sorted(COVERAGE_ENTRIES, key=lambda e: e["key"])
    _validate_entries(ordered, canonical_keys)

    coverage = []
    for entry in ordered:
        item: dict[str, Any] = {
            "key": entry["key"],
            "status": entry["status"],
            "sinceVersion": UNRELEASED,
            "entrypoints": list(entry["entrypoints"]),
        }
        if entry["status"] == "partial":
            item["note"] = entry["note"]
        coverage.append(item)

    return {
        "schemaVersion": "1.0.0",
        "generator": "scripts/generate-sdk-coverage.py",
        "trackingIssue": TRACKING_ISSUE,
        "sourceRepository": SOURCE_REPOSITORY,
        "description": (
            "Per-capability coverage snapshot for the cross-repo capability "
            "matrix (honua-io/honua-server#2892). Each entry maps a canonical "
            "capability key (consumed, never copied, from honua-server#2893's "
            "published capability-keys.v1.json) to what this SDK's source "
            "actually implements. Capabilities this SDK does not touch are "
            "absent from 'coverage' -- never padded."
        ),
        "sdkAvailability": {
            "status": "source-preview",
            "publishedVersion": None,
            "note": (
                "Honua.Sdk.* ships as a source preview only; no NuGet package "
                "has published yet (see honua-site data/sdk-availability.v1.json, "
                "productArea \"sdk-dotnet\"). Every coverage entry's "
                "sinceVersion is the literal marker \"unreleased\" -- repo tags "
                "such as dotnet-sdk-v1.5.0 are pre-publish release-please "
                "source tags, not registry releases, and are never used here."
            ),
        },
        "capabilityKeyList": {
            "canonicalUrl": CANONICAL_URL,
            "resolvedFrom": key_list_source,
            "keyCount": len(canonical_keys),
        },
        "coverage": coverage,
    }


def _dumps(document: dict[str, Any]) -> str:
    return json.dumps(document, indent=2, ensure_ascii=False) + "\n"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="Fail if contracts/sdk-coverage.v1.json is missing or stale (drift gate); do not write.",
    )
    parser.add_argument(
        "--summary",
        type=Path,
        help="Optional path (e.g. $GITHUB_STEP_SUMMARY) to append a coverage summary to.",
    )
    args = parser.parse_args(argv)

    try:
        canonical_keys, key_list_source = _load_canonical_keys()
        _validate_entrypoints_against_source(COVERAGE_ENTRIES, _build_source_type_index(SRC_ROOT))
        document = _build_document(canonical_keys, key_list_source)
    except (ValueError, OSError, urllib.error.URLError, json.JSONDecodeError) as error:
        print(f"::error::{error}", file=sys.stderr)
        return 1

    rendered = _dumps(document)
    covered = sum(1 for c in document["coverage"] if c["status"] == "covered")
    partial = sum(1 for c in document["coverage"] if c["status"] == "partial")
    total_keys = document["capabilityKeyList"]["keyCount"]

    summary_line = (
        f"sdk-coverage.v1.json: {covered} covered + {partial} partial "
        f"= {covered + partial}/{total_keys} canonical capability keys touched "
        f"(source: {key_list_source})."
    )
    print(summary_line)

    if args.summary:
        with args.summary.open("a", encoding="utf-8") as fh:
            fh.write(f"\n**SDK coverage snapshot**: {summary_line}\n")

    if args.check:
        if not OUTPUT_PATH.is_file():
            print(f"::error::{OUTPUT_PATH} does not exist. Run without --check to generate it.", file=sys.stderr)
            return 1
        existing = OUTPUT_PATH.read_text(encoding="utf-8")
        if existing != rendered:
            print(
                f"::error::{OUTPUT_PATH} is stale. Run "
                "'python3 scripts/generate-sdk-coverage.py' and commit the result.",
                file=sys.stderr,
            )
            return 1
        print(f"{OUTPUT_PATH} is up to date.")
        return 0

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(rendered, encoding="utf-8")
    print(f"Wrote {OUTPUT_PATH}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
