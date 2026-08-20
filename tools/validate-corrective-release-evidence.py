#!/usr/bin/env python3
"""Validate retained Story 3.14 evidence and print its canonical identity digest."""

import argparse
import hashlib
import json
import sys
from pathlib import Path

from release_evidence_codec import (
    CODEC_VERSION,
    SCHEMA,
    EvidenceError,
    load_json_bytes,
    validate_identity,
    validate_packet_files,
)


def _read_manifest(path):
    try:
        manifest = load_json_bytes(path.read_bytes())
        packages = manifest["packages"]
        return [item["id"] for item in packages]
    except (OSError, KeyError, TypeError) as error:
        raise EvidenceError("release package manifest is invalid") from error


def _sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _codec_identity():
    tools = Path(__file__).resolve().parent
    return {
        "schema": SCHEMA,
        "version": CODEC_VERSION,
        "codec_file": "tools/release_evidence_codec.py",
        "codec_sha256": _sha256(tools / "release_evidence_codec.py"),
        "verifier_file": "tools/validate-corrective-release-evidence.py",
        "verifier_sha256": _sha256(tools / "validate-corrective-release-evidence.py"),
    }


def validate(evidence_path, manifest_path, packet_root=None):
    """Return the canonical SHA-256 after validating one retained identity file."""
    evidence_bytes = evidence_path.read_bytes()
    document = load_json_bytes(evidence_bytes)
    canonical = validate_identity(
        document,
        _read_manifest(manifest_path),
        expected_manifest_sha256=_sha256(manifest_path),
        expected_codec=_codec_identity(),
    )
    if evidence_bytes != canonical:
        raise EvidenceError("release identity bytes are not the selected codec's canonical UTF-8 form")
    validate_packet_files(document, packet_root or evidence_path.parent)
    return hashlib.sha256(canonical).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument("--manifest", type=Path, default=Path("tools/release-packages.json"))
    parser.add_argument(
        "--packet-root",
        type=Path,
        help="Root used to resolve retained package, OCI, and smoke evidence paths.",
    )
    arguments = parser.parse_args()
    try:
        digest = validate(arguments.evidence, arguments.manifest, arguments.packet_root)
    except (OSError, EvidenceError, json.JSONDecodeError) as error:
        print(f"[corrective-release-evidence] fail: {error}", file=sys.stderr)
        return 1
    print(f"[corrective-release-evidence] pass: sha256:{digest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
