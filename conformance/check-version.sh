#!/usr/bin/env bash
#
# Enforce that the pinned conformance fixture version equals the SDK's pinned
# Geospatial.Grpc package version. A fixture set maps 1:1 to a geospatial.v1
# schema release, and the SDK's generated gRPC client is built against that same
# schema version, so the two pins must never drift apart. The conformance CI job
# runs this before fetching fixtures or starting the server.
#
# Usage: conformance/check-version.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

fixture_version="$(tr -d '[:space:]' < "${SCRIPT_DIR}/FIXTURE_VERSION")"

props="${REPO_ROOT}/Directory.Packages.props"
# Extract the centrally managed Geospatial.Grpc package version without an XML parser.
grpc_version="$(
  grep -oE '<PackageVersion Include="Geospatial\.Grpc" Version="[^"]+"' "${props}" \
    | head -n1 \
    | sed -E 's:.* Version="([^"]+)".*:\1:' \
    | tr -d '[:space:]'
)"

if [[ -z "${fixture_version}" ]]; then
  echo "error: conformance/FIXTURE_VERSION is empty" >&2
  exit 1
fi

if [[ -z "${grpc_version}" ]]; then
  echo "error: could not read the Geospatial.Grpc PackageVersion from ${props}" >&2
  exit 1
fi

echo "Conformance fixture version : ${fixture_version}"
echo "Geospatial.Grpc package     : ${grpc_version}"

if [[ "${fixture_version}" != "${grpc_version}" ]]; then
  echo "error: conformance fixture version (${fixture_version}) does not match" >&2
  echo "       the pinned Geospatial.Grpc package version (${grpc_version})." >&2
  echo "       Update conformance/FIXTURE_VERSION and Directory.Packages.props together" >&2
  echo "       so the fixtures and the generated client stay on the same schema." >&2
  exit 1
fi

echo "OK: conformance fixtures and Geospatial.Grpc are pinned to the same schema release."
