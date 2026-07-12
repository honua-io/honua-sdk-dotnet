#!/usr/bin/env python3
"""Fail when a browser trim log contains a warning outside the reviewed baseline."""

from __future__ import annotations

import re
import sys
from pathlib import Path


WARNING_PATTERN = re.compile(r"\bwarning (IL\d+): (.+)$")

# These diagnostics are emitted from framework or geospatial dependency code.
# The trimmed Playwright smoke exercises Blazor/JS interop and the SDK's NTS
# GeoJSON + ProjNET paths so that preserved runtime behavior is verified.
ALLOWED_WARNING_SITES = {
    ("IL2072", "Microsoft.AspNetCore.Components.CascadingParameterState.FindCascadingParameters"),
    ("IL2072", "Microsoft.AspNetCore.Components.ComponentFactory.PerformPropertyInjection"),
    ("IL2072", "Microsoft.AspNetCore.Components.Reflection.ComponentProperties.SetProperties"),
    ("IL2111", "Microsoft.JSInterop.Infrastructure.DotNetDispatcher.GetCachedMethodInfo"),
    ("IL2065", "Microsoft.JSInterop.Infrastructure.DotNetDispatcher.ScanAssemblyForCallableMethods"),
    ("IL2057", "NetTopologySuite.Index.Quadtree.NodeBase<T>.SynchonizedList.SynchonizedList"),
    ("IL2026", "NetTopologySuite.Features.JsonElementAttributesTable.TryDeserializeElement<T>"),
    ("IL2026", "NetTopologySuite.Features.JsonObjectAttributesTable.TryDeserializeJsonObject<T>"),
    ("IL2026", "NetTopologySuite.Features.JsonObjectAttributesTable.TryGetJsonObjectPropertyValue<T>"),
    ("IL2026", "NetTopologySuite.IO.Converters.StjAttributesTableConverter.Write"),
    ("IL2026", "NetTopologySuite.IO.Converters.StjFeatureCollectionConverter.Read"),
    ("IL2026", "NetTopologySuite.IO.Converters.StjFeatureCollectionConverter.Write"),
    ("IL2026", "NetTopologySuite.IO.Converters.StjFeatureConverter.Read"),
    ("IL2026", "NetTopologySuite.IO.Converters.StjFeatureConverter.Write"),
    ("IL2026", "NetTopologySuite.IO.Converters.Utility.ObjectFromJsonNode"),
    ("IL2026", "NetTopologySuite.IO.Converters.Utility.ObjectToJsonNode"),
    ("IL2070", "ProjNet.CoordinateSystems.Projections.ProjectionsRegistry.CheckConstructor"),
    ("IL2067", "ProjNet.CoordinateSystems.Projections.ProjectionsRegistry.CreateProjection"),
}


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: validate-browser-trim-warnings.py <publish-log>", file=sys.stderr)
        return 2

    log_path = Path(sys.argv[1])
    if not log_path.is_file():
        print(f"error: trim publish log not found: {log_path}", file=sys.stderr)
        return 2

    unexpected: list[str] = []
    reviewed_count = 0
    for line in log_path.read_text(encoding="utf-8", errors="replace").splitlines():
        match = WARNING_PATTERN.search(line)
        if match is None:
            continue

        warning_code, message = match.groups()
        if any(
            warning_code == allowed_code and message.startswith(site + "(")
            for allowed_code, site in ALLOWED_WARNING_SITES
        ):
            reviewed_count += 1
        else:
            unexpected.append(line)

    if unexpected:
        print("Unexpected browser trim warnings were found:", file=sys.stderr)
        for line in unexpected:
            print(f"  {line}", file=sys.stderr)
        return 1

    print(f"Browser trim warning baseline passed ({reviewed_count} reviewed upstream warning(s)).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
