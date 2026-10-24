"""Release entitlement proof for licensed FeatureServer edit certification (issue #308).

Mirrors the Python SDK's governed model: pull request and nightly runs use an
explicit Development grant rehearsal that is never release proof; release runs
require a signed license supplied through a governed workflow_dispatch and the
candidate's own license status must validate it. The published .NET SDK does not
expose license mode or identifier, so the server status is observed over HTTP and
the raw JSON is retained next to its SHA-256 as the evidence.
"""
from __future__ import annotations

import hashlib
import json
import re
from collections.abc import Mapping
from datetime import datetime, timezone
from typing import Any

POLICY_REVISION = "honua-pro-featureserver-edits-v1"
EDIT_CAPABILITY = "editing.featureserver-edits"
OBSERVATION_SCHEMA = "honua.sdk-dotnet-entitlement-observation/v1"
CONSUMER_VERIFICATION = "live-server-capability-probe-v1"
LICENSE_STATUS_PATH = "/api/v1/admin/license/"
DEPLOYMENT_TARGET = "local-docker"
_REHEARSAL_EVENTS = {"pull_request", "push", "schedule", "workflow_dispatch", "local"}


def profile(tier: str, event: str, environment: str, license_content: str) -> dict[str, str]:
    if tier == "release":
        if event != "workflow_dispatch":
            raise ValueError("release certification requires an explicit governed dispatch")
        try:
            envelope = json.loads(license_content)
        except (ValueError, TypeError) as exc:
            raise ValueError("HONUA_CERTIFICATION_LICENSE_CONTENT must supply the signed release license") from exc
        if not isinstance(envelope, dict) or not envelope:
            raise ValueError("signed release license envelope is absent")
        # Parsing is input validation only; the candidate must validate the signature.
        return {"mode": "signed", "devGrantEdition": ""}
    if tier not in {"pr", "nightly"} or event not in _REHEARSAL_EVENTS:
        raise ValueError("unknown certification rehearsal profile")
    if environment != "Development":
        raise ValueError("rehearsal entitlement grants require Development")
    return {"mode": "development-grant", "devGrantEdition": "Enterprise"}


def _timestamp(value: object) -> datetime:
    if not isinstance(value, str):
        raise ValueError("timestamp is absent")
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        raise ValueError("timestamp has no timezone")
    return parsed


def canonical_sha256(value: object) -> str:
    return "sha256:" + hashlib.sha256(
        json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    ).hexdigest()


def signed_status(value: object, *, now: datetime) -> str:
    """Validate a candidate license status as a signed non-development entitlement.

    Returns the license fingerprint. A 402, a development grant, a community
    license, an invalid or expired license, or a missing edit entitlement raises.
    """
    data = value.get("data") if isinstance(value, Mapping) and value.get("success") is True else None
    if not isinstance(data, Mapping):
        raise AssertionError("candidate did not return a successful license status")
    identifier = data.get("licenseId")
    if (data.get("mode") != "enabled" or data.get("isValid") is not True or data.get("validationState") != "Valid"
            or not isinstance(identifier, str) or not identifier.strip()
            or identifier.lower() in {"dev-grant", "community"}):
        raise AssertionError("candidate did not validate a signed non-development license")
    try:
        issued = _timestamp(data.get("issuedAt"))
        expires = _timestamp(data["expiresAt"]) if data.get("expiresAt") else None
        if issued > now or (expires is not None and expires <= now):
            raise ValueError("invalid license lifetime")
    except (KeyError, TypeError, AttributeError, ValueError) as exc:
        raise AssertionError("candidate license lifetime is absent, invalid or expired") from exc
    entitlements = data.get("entitlements")
    matching = [row for row in entitlements if isinstance(row, Mapping) and row.get("key") == EDIT_CAPABILITY] \
        if isinstance(entitlements, list) else []
    if len(matching) != 1 or matching[0].get("isActive") is not True:
        raise AssertionError("candidate did not validate the required editing entitlement")
    return "sha256:" + hashlib.sha256(identifier.encode()).hexdigest()


def build_observation(mode: str, license_status: object, checked_at: datetime) -> dict[str, Any]:
    """Build the retained observation. Only a valid signed status is release eligible."""
    checked = checked_at.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")
    observation: dict[str, Any] = {
        "schema": OBSERVATION_SCHEMA, "mode": mode, "release_eligible": False,
        "policy_revision": POLICY_REVISION, "deployment_target": DEPLOYMENT_TARGET,
        "verification": CONSUMER_VERIFICATION, "checked_at": checked,
        "license_status": license_status, "license_status_sha256": canonical_sha256(license_status),
        "license_fingerprint": None,
    }
    if mode == "signed":
        observation["license_fingerprint"] = signed_status(license_status, now=checked_at)
        observation["release_eligible"] = True
    elif mode != "development-grant":
        raise ValueError("unknown entitlement mode")
    return observation


def verified_entitlement(observation: object, *, capability_key: str, started_at: str, completed_at: str) -> dict[str, str]:
    """Return the strict-consumer entitlement assertion, or raise ValueError/AssertionError.

    The observation is re-validated from its retained raw status; its claimed
    eligibility and fingerprint are never trusted.
    """
    if not isinstance(observation, Mapping) or observation.get("schema") != OBSERVATION_SCHEMA:
        raise ValueError("entitlement observation is absent or has the wrong schema")
    if observation.get("mode") != "signed" or observation.get("release_eligible") is not True:
        raise ValueError("entitlement observation is a non-release rehearsal")
    if observation.get("policy_revision") != POLICY_REVISION or observation.get("deployment_target") != DEPLOYMENT_TARGET:
        raise ValueError("entitlement observation is bound to another policy or target")
    status = observation.get("license_status")
    if observation.get("license_status_sha256") != canonical_sha256(status):
        raise ValueError("entitlement observation digest does not match the retained license status")
    checked = _timestamp(observation.get("checked_at"))
    if not _timestamp(started_at) <= checked <= _timestamp(completed_at):
        raise ValueError("entitlement observation was not taken during the certification run")
    fingerprint = signed_status(status, now=checked)
    if observation.get("license_fingerprint") != fingerprint or not re.fullmatch(r"sha256:[0-9a-f]{64}", fingerprint):
        raise ValueError("entitlement observation fingerprint does not match the retained license status")
    return {
        "policy_revision": POLICY_REVISION, "capability_key": capability_key,
        "deployment_target": DEPLOYMENT_TARGET, "verification": CONSUMER_VERIFICATION,
        "status": "active", "checked_at": observation["checked_at"], "license_fingerprint": fingerprint,
    }
