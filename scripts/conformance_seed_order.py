#!/usr/bin/env python3
"""Decide whether a conformance server image is seeded before or after startup.

The historical e3ab87ce image is the only one seeded first. Callers may pass
that image as a tag, so the decision compares resolved repository digests.
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys


DIGEST_REF = re.compile(r"^(.+)@sha256:([0-9a-fA-F]{64})$")
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")


def canonical_digest_ref(image: str) -> str | None:
    """Return repo@sha256:digest when image is already digest-addressed."""
    match = DIGEST_REF.fullmatch(image.strip())
    if match is None:
        return None
    return f"{match.group(1)}@sha256:{match.group(2).lower()}"


def repository_name(image: str) -> str:
    reference = image.strip()
    if "@" in reference:
        return reference.split("@", 1)[0]
    return reference.rsplit(":", 1)[0]


def docker_resolve_digest(image: str) -> str:
    """Resolve a tag with docker buildx. Digest references are not looked up."""
    canonical = canonical_digest_ref(image)
    if canonical is not None:
        return canonical
    completed = subprocess.run(
        ["docker", "buildx", "imagetools", "inspect", image.strip(), "--format", "{{json .}}"],
        check=False,
        capture_output=True,
        text=True,
    )
    if completed.returncode != 0:
        detail = completed.stderr.strip() or completed.stdout.strip()
        raise ValueError(detail or f"could not inspect {image}")
    payload = json.loads(completed.stdout)
    digest = payload.get("manifest", {}).get("digest") if isinstance(payload, dict) else None
    if not isinstance(digest, str) or DIGEST.fullmatch(digest) is None:
        raise ValueError(f"image {image} did not resolve to a sha256 digest")
    return f"{repository_name(image)}@{digest}"


def seed_before_start(image: str, legacy_image: str, resolve_digest) -> bool:
    """True when image and the legacy pin are the same repository digest."""
    return resolve_digest(image) == resolve_digest(legacy_image)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--canonicalize", help="Print the repository digest reference for this image.")
    parser.add_argument("--image", help="Selected honua-server image.")
    parser.add_argument("--legacy", help="Legacy image that must be seeded before startup.")
    args = parser.parse_args(argv)
    try:
        if args.canonicalize:
            if args.image or args.legacy:
                parser.error("--canonicalize cannot be combined with --image or --legacy")
            print(docker_resolve_digest(args.canonicalize))
            return 0
        if not args.image or not args.legacy:
            parser.error("--image and --legacy are required unless --canonicalize is set")
        print("before" if seed_before_start(args.image, args.legacy, docker_resolve_digest) else "after")
    except (ValueError, json.JSONDecodeError, OSError) as error:
        print(f"::error::{error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
