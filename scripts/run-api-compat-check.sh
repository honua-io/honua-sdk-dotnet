#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 || "$2" != "--" ]]; then
  echo "usage: $0 PACKAGE_ID -- COMMAND [ARG ...]" >&2
  exit 2
fi

package_id="$1"
shift 2

allow_breaking="${HONUA_API_COMPAT_ALLOW_BREAKING:-false}"
approval="${HONUA_API_COMPAT_BREAKING_APPROVAL:-}"

if [[ "${allow_breaking}" == "true" && "${approval}" != "major-version-release" ]]; then
  echo "::error::HONUA_API_COMPAT_ALLOW_BREAKING=true requires HONUA_API_COMPAT_BREAKING_APPROVAL=major-version-release. Use the manual API Compatibility Major Release workflow so the override is auditable."
  exit 2
fi

if [[ "${allow_breaking}" == "true" && "${GITHUB_ACTIONS:-false}" == "true" ]]; then
  if [[ "${GITHUB_EVENT_NAME:-}" != "workflow_dispatch" || "${GITHUB_WORKFLOW:-}" != "API Compatibility Major Release Review" ]]; then
    echo "::error::The breaking-API override is restricted to the manually dispatched API Compatibility Major Release Review workflow."
    exit 2
  fi
fi

set +e
"$@"
status=$?
set -e

if [[ ${status} -eq 0 ]]; then
  exit 0
fi

if [[ "${allow_breaking}" == "true" ]]; then
  echo "::warning::API breaking changes detected in ${package_id}; the explicitly approved major-version release override is active."
  exit 0
fi

echo "::error::API compatibility failed for ${package_id}. Breaking changes require an explicit major-version release review."
exit "${status}"
