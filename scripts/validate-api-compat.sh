#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
BASE_REF="${1:-${HONUA_API_COMPAT_BASE_REF:-}}"
DEFAULT_BASE_REF="${HONUA_API_COMPAT_DEFAULT_BASE_REF:-origin/trunk}"

if [[ -z "${BASE_REF}" ]]; then
  if git -C "${ROOT}" rev-parse --verify --quiet "${DEFAULT_BASE_REF}^{commit}" >/dev/null; then
    BASE_REF="${DEFAULT_BASE_REF}"
  else
    BASE_REF="HEAD~1"
  fi
fi

if ! git -C "${ROOT}" rev-parse --verify --quiet "${BASE_REF}^{commit}" >/dev/null; then
  if [[ "${HONUA_API_COMPAT_ALLOW_MISSING_BASELINE:-false}" == "true" ]]; then
    echo "::warning::Skipping API compatibility validation because baseline ref '${BASE_REF}' was not found and HONUA_API_COMPAT_ALLOW_MISSING_BASELINE=true."
    exit 0
  fi

  echo "::error::API compatibility baseline ref '${BASE_REF}' was not found. Fetch the baseline or set HONUA_API_COMPAT_ALLOW_MISSING_BASELINE=true for intentional first-run bootstraps."
  exit 1
fi

TEMP_DIR="$(mktemp -d)"
BASE_WORKTREE="${TEMP_DIR}/baseline"
BASE_PACKAGES="${TEMP_DIR}/baseline-packages"
CURRENT_PACKAGES="${TEMP_DIR}/current-packages"

cleanup() {
  git -C "${ROOT}" worktree remove --force "${BASE_WORKTREE}" >/dev/null 2>&1 || true
  rm -rf "${TEMP_DIR}"
}
trap cleanup EXIT

mkdir -p "${BASE_PACKAGES}" "${CURRENT_PACKAGES}"

git -C "${ROOT}" worktree add --detach "${BASE_WORKTREE}" "${BASE_REF}" >/dev/null
dotnet tool restore --tool-manifest "${ROOT}/.config/dotnet-tools.json" >/dev/null

projects=(
  "src/Honua.Sdk.Abstractions/Honua.Sdk.Abstractions.csproj|Honua.Sdk.Abstractions"
  "src/Honua.Sdk.Admin/Honua.Sdk.Admin.csproj|Honua.Sdk.Admin"
  "src/Honua.Sdk.Grpc/Honua.Sdk.Grpc.csproj|Honua.Sdk.Grpc"
  "src/Honua.Sdk.Wfs/Honua.Sdk.Wfs.csproj|Honua.Sdk.Wfs"
  "src/Honua.Sdk.GeoServices/Honua.Sdk.GeoServices.csproj|Honua.Sdk.GeoServices"
  "src/Honua.Sdk.OgcFeatures/Honua.Sdk.OgcFeatures.csproj|Honua.Sdk.OgcFeatures"
  "src/Honua.Sdk.Offline.Abstractions/Honua.Sdk.Offline.Abstractions.csproj|Honua.Sdk.Offline.Abstractions"
  "src/Honua.Sdk.Offline/Honua.Sdk.Offline.csproj|Honua.Sdk.Offline"
)

for entry in "${projects[@]}"; do
  IFS="|" read -r project package_id <<< "${entry}"
  baseline_output="${BASE_PACKAGES}/${package_id}"
  current_output="${CURRENT_PACKAGES}/${package_id}"

  if [[ ! -f "${ROOT}/${project}" ]]; then
    echo "::warning::Skipping ${package_id}; current project '${project}' was not found."
    continue
  fi

  if [[ ! -f "${BASE_WORKTREE}/${project}" ]]; then
    echo "::notice::Skipping ${package_id} API compatibility; package does not exist at baseline ref '${BASE_REF}'."
    continue
  fi

  mkdir -p "${baseline_output}" "${current_output}"

  echo "Packing ${package_id} baseline from ${BASE_REF}..."
  dotnet pack "${BASE_WORKTREE}/${project}" \
    --configuration "${CONFIGURATION}" \
    -o "${baseline_output}" \
    /p:TreatWarningsAsErrors=true \
    /p:ContinuousIntegrationBuild=true

  echo "Packing ${package_id} from current checkout..."
  dotnet pack "${ROOT}/${project}" \
    --configuration "${CONFIGURATION}" \
    -o "${current_output}" \
    /p:TreatWarningsAsErrors=true \
    /p:ContinuousIntegrationBuild=true

  baseline_package="$(find "${baseline_output}" -maxdepth 1 -type f -name "${package_id}.*.nupkg" ! -name "*.symbols.nupkg" | sort | head -n 1)"
  current_package="$(find "${current_output}" -maxdepth 1 -type f -name "${package_id}.*.nupkg" ! -name "*.symbols.nupkg" | sort | head -n 1)"

  if [[ -z "${baseline_package}" || -z "${current_package}" ]]; then
    echo "::error::Could not locate generated NuGet packages for ${package_id}."
    exit 1
  fi

  echo "Validating ${package_id} API compatibility..."
  api_compat_args=(
    "package" "${current_package}"
    "--baseline-package" "${baseline_package}"
    "--run-api-compat"
    "--enable-rule-cannot-change-parameter-name"
  )

  suppression_file="${ROOT}/eng/api-compat/${package_id}.xml"
  if [[ -f "${suppression_file}" ]]; then
    api_compat_args+=("--suppression-file" "${suppression_file}")
  fi

  (
    cd "${ROOT}"
    dotnet tool run apicompat -- "${api_compat_args[@]}"
  )
done
