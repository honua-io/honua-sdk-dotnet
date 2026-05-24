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
  "src/Honua.Sdk.Processes/Honua.Sdk.Processes.csproj|Honua.Sdk.Processes"
  "src/Honua.Sdk.Geometry/Honua.Sdk.Geometry.csproj|Honua.Sdk.Geometry"
  "src/Honua.Sdk.Spec/Honua.Sdk.Spec.csproj|Honua.Sdk.Spec"
  "src/Honua.Sdk.Grpc/Honua.Sdk.Grpc.csproj|Honua.Sdk.Grpc"
  "src/Honua.Sdk.GeoServices/Honua.Sdk.GeoServices.csproj|Honua.Sdk.GeoServices"
  "src/Honua.Sdk.Scenes/Honua.Sdk.Scenes.csproj|Honua.Sdk.Scenes"
  "src/Honua.Sdk.Field/Honua.Sdk.Field.csproj|Honua.Sdk.Field"
  "src/Honua.Sdk.OgcFeatures/Honua.Sdk.OgcFeatures.csproj|Honua.Sdk.OgcFeatures"
  "src/Honua.Sdk.Catalogs/Honua.Sdk.Catalogs.csproj|Honua.Sdk.Catalogs"
  "src/Honua.Sdk.Offline/Honua.Sdk.Offline.csproj|Honua.Sdk.Offline"
  "src/Honua.Sdk/Honua.Sdk.csproj|Honua.Sdk"
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

  # When HONUA_API_COMPAT_ALLOW_BREAKING=true (used for explicit major-version
  # bumps such as the 0.1.x-alpha → 1.0.0 cut), still RUN the check so the
  # diagnostics show up in the workflow log, but downgrade a non-zero exit to
  # a warning instead of failing the gate. This is the SemVer-correct
  # behaviour for an intentional breaking-major release.
  if [[ "${HONUA_API_COMPAT_ALLOW_BREAKING:-false}" == "true" ]]; then
    (
      cd "${ROOT}"
      if ! dotnet tool run apicompat -- "${api_compat_args[@]}"; then
        echo "::warning::API breaking changes detected in ${package_id} but HONUA_API_COMPAT_ALLOW_BREAKING=true; downgrading to warning."
      fi
    )
  else
    (
      cd "${ROOT}"
      dotnet tool run apicompat -- "${api_compat_args[@]}"
    )
  fi
done
