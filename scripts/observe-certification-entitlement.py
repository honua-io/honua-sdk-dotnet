#!/usr/bin/env python3
"""Resolve the entitlement profile and retain the candidate's license status (issue #308)."""
from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import certification_entitlement as entitlement  # noqa: E402


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):  # noqa: D401
        return None


def fetch_status(base_url: str, api_key: str) -> object:
    request = urllib.request.Request(base_url.rstrip("/") + entitlement.LICENSE_STATUS_PATH,
                                     headers={"X-API-Key": api_key} if api_key else {})
    with urllib.request.build_opener(_NoRedirect).open(request, timeout=20) as response:
        return json.loads(response.read().decode("utf-8"))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    for name in ("profile", "observe"):
        command = sub.add_parser(name)
        command.add_argument("--tier", required=True, choices=("pr", "nightly", "release"))
        command.add_argument("--event", required=True)
    sub.choices["observe"].add_argument("--mode", required=True, choices=("signed", "disabled", "development-grant"))
    sub.choices["observe"].add_argument("--base-url", required=True)
    sub.choices["observe"].add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == "profile":
            print(json.dumps(entitlement.profile(args.tier, args.event, "Development",
                                                 os.environ.get("HONUA_CERTIFICATION_LICENSE_CONTENT", ""))))
            return 0
        expected = entitlement.profile(args.tier, args.event, "Development",
                                       os.environ.get("HONUA_CERTIFICATION_LICENSE_CONTENT", ""))["mode"]
        if expected != args.mode:
            raise ValueError("entitlement mode does not match the tier profile")
        try:
            status = fetch_status(args.base_url, os.environ.get("HONUA_PROTOCOL_API_KEY", ""))
        except (urllib.error.URLError, ValueError, OSError) as exc:
            if args.mode == "signed":
                raise AssertionError(f"release certification could not read the license status: {exc}") from exc
            status = None  # rehearsal observation is best effort and never release proof
        observation = entitlement.build_observation(args.mode, status, datetime.now(timezone.utc))
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(observation, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print(f"Retained {args.mode} entitlement observation (release_eligible={observation['release_eligible']}).")
        return 0
    except (ValueError, AssertionError) as error:
        print(f"::error::{error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
