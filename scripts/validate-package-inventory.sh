#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST="${ROOT}/eng/shipped-packages.txt"
EXPECTED_PACKAGE_COUNT=15
errors=0

fail() {
  echo "::error::$*" >&2
  errors=1
}

if [[ ! -f "${MANIFEST}" ]]; then
  fail "Shipped-package manifest not found: ${MANIFEST}"
  exit 1
fi

mapfile -t entries < <(grep -Ev '^[[:space:]]*(#|$)' "${MANIFEST}")
if [[ ${#entries[@]} -ne ${EXPECTED_PACKAGE_COUNT} ]]; then
  fail "Expected ${EXPECTED_PACKAGE_COUNT} shipped packages, found ${#entries[@]} in eng/shipped-packages.txt."
fi

manifest_projects=()
for entry in "${entries[@]}"; do
  IFS="|" read -r project package_id extra <<< "${entry}"
  if [[ -z "${project}" || -z "${package_id}" || -n "${extra:-}" ]]; then
    fail "Invalid shipped-package entry: ${entry}"
    continue
  fi

  manifest_projects+=("${project}")
  project_path="${ROOT}/${project}"
  if [[ ! -f "${project_path}" ]]; then
    fail "Manifest project does not exist: ${project}"
    continue
  fi

  declared_id="$(sed -n 's:.*<PackageId>\([^<]*\)</PackageId>.*:\1:p' "${project_path}" | head -n 1)"
  if [[ "${declared_id}" != "${package_id}" ]]; then
    fail "${project} declares PackageId '${declared_id}', expected '${package_id}'."
  fi

  package_dir="$(dirname "${project_path}")"
  if [[ ! -f "${package_dir}/README.md" ]]; then
    fail "${package_id} is missing its package README."
  fi
  if [[ ! -f "${package_dir}/PublicAPI.Shipped.txt" ]]; then
    fail "${package_id} is missing PublicAPI.Shipped.txt."
  fi
  if [[ ! -f "${package_dir}/PublicAPI.Unshipped.txt" ]]; then
    fail "${package_id} is missing PublicAPI.Unshipped.txt."
  fi

  grep -Fq "| **${package_id}** |" "${ROOT}/README.md" \
    || fail "README.md package table is missing ${package_id}."
  grep -Fq "| \`${package_id}\` |" "${ROOT}/INSTALL.md" \
    || fail "INSTALL.md package table is missing ${package_id}."
  grep -Fxq "dotnet add package ${package_id}" "${ROOT}/README.md" \
    || fail "README.md install commands are missing ${package_id}."
  grep -Fxq "dotnet add package ${package_id}" "${ROOT}/INSTALL.md" \
    || fail "INSTALL.md install commands are missing ${package_id}."
  grep -Fq "${project}" "${ROOT}/.github/workflows/ci.yml" \
    || fail "CI package matrix is missing ${project}."
done

grep -Fq 'eng/shipped-packages.txt' "${ROOT}/scripts/validate-api-compat.sh" \
  || fail "API compatibility validation must consume eng/shipped-packages.txt."
grep -Fq 'eng/shipped-packages.txt' "${ROOT}/.github/workflows/publish-dotnet-sdk.yml" \
  || fail "Publish workflow must consume eng/shipped-packages.txt."

mapfile -t actual_projects < <(
  find "${ROOT}/src" -mindepth 2 -maxdepth 2 -type f -name 'Honua.Sdk*.csproj' \
    -printf 'src/%P\n' | sort
)
mapfile -t expected_projects < <(printf '%s\n' "${manifest_projects[@]}" | sort)

if ! diff -u \
  <(printf '%s\n' "${expected_projects[@]}") \
  <(printf '%s\n' "${actual_projects[@]}"); then
  fail "eng/shipped-packages.txt does not match the shipped projects under src/."
fi

grep -Fq "map of the 15 packages" "${ROOT}/README.md" \
  || fail "README.md must state the current 15-package inventory."
grep -Fq "15 packages (meta plus 14 sub-packages)" "${ROOT}/docs/README.md" \
  || fail "docs/README.md must state the current package inventory."
grep -Fq "fans out across the 14" "${ROOT}/docs/architecture.md" \
  || fail "docs/architecture.md must state the current 14 leaf-package inventory."

if [[ ${errors} -ne 0 ]]; then
  exit 1
fi

echo "Validated ${#entries[@]} shipped packages across projects, workflows, and install documentation."
