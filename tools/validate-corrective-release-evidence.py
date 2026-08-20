#!/usr/bin/env python3
"""Validate retained Story 3.14 evidence and print its canonical identity digest."""

import argparse
import hashlib
import json
import sys
from pathlib import Path

from release_evidence_codec import EvidenceError, load_json_bytes, validate_identity


def _read_manifest(path):
    try:
        manifest = load_json_bytes(path.read_bytes())
        packages = manifest["packages"]
        return [item["id"] for item in packages]
    except (OSError, KeyError, TypeError) as error:
        raise EvidenceError("release package manifest is invalid") from error


def validate(evidence_path, manifest_path):
    """Return the canonical SHA-256 after validating one retained identity file."""
    document = load_json_bytes(evidence_path.read_bytes())
    canonical = validate_identity(document, _read_manifest(manifest_path))
    return hashlib.sha256(canonical).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument("--manifest", type=Path, default=Path("tools/release-packages.json"))
    arguments = parser.parse_args()
    try:
        digest = validate(arguments.evidence, arguments.manifest)
    except (OSError, EvidenceError, json.JSONDecodeError) as error:
        print(f"[corrective-release-evidence] fail: {error}", file=sys.stderr)
        return 1
    print(f"[corrective-release-evidence] pass: sha256:{digest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
