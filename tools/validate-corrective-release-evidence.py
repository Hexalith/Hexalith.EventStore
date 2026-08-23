#!/usr/bin/env python3
"""Validate retained Story 3.14 evidence and print its canonical identity digest."""

import argparse
import hashlib
import importlib
import json
import sys
from pathlib import Path


SCHEMA = "hexalith.eventstore.corrective-release-identity.v1"
V3_PACKET_CODEC_SHA256 = "814502bd962e00dfbac243e2443c3709b46bdbb69e197691443a083e283d32a9"
HANDLERS = {
    (SCHEMA, 3, V3_PACKET_CODEC_SHA256): "release_evidence_handlers.v3",
}
# EXPECTED_PACKET_CODEC_SHA256 (above, keyed into HANDLERS) pins the codec/verifier bytes the
# packet retains as evidence -- it says nothing about the executing module. This pins the actual
# on-disk bytes of the imported handler file itself, so an edit to release_evidence_handlers/v3.py
# that is not accompanied by updating this constant fails closed instead of executing unreviewed.
HANDLER_FILE_SHA256 = {
    "release_evidence_handlers.v3": "c768b1efbe1314b2c8d90e523536648b5fd912208ff6d65a81838b3c61857ff0",
}


class DispatchError(ValueError):
    """The retained packet does not select a trusted live handler."""


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise DispatchError("release identity contains duplicate JSON fields")
        result[key] = value
    return result


def _load_dispatch_metadata(evidence_bytes):
    try:
        document = json.loads(evidence_bytes, object_pairs_hook=_unique_object)
        codec = document["codec"]
        binding = codec["codec"]
        key = (document["schema"], codec["version"], binding["sha256"])
    except (KeyError, TypeError, json.JSONDecodeError) as error:
        raise DispatchError("release identity dispatch metadata is invalid") from error
    if key not in HANDLERS:
        raise DispatchError("release identity does not select a trusted live handler")
    return document, HANDLERS[key]


def _load_handler(module_name):
    module = importlib.import_module(module_name)
    expected = HANDLER_FILE_SHA256.get(module_name)
    if expected is None or hashlib.sha256(Path(module.__file__).read_bytes()).hexdigest() != expected:
        raise DispatchError("release identity does not select a trusted live handler")
    return module


def _read_manifest(path, handler):
    try:
        return handler.validate_release_manifest(handler.load_json_bytes(path.read_bytes()))
    except OSError as error:
        raise handler.EvidenceError("release package manifest is invalid") from error


def _sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate(evidence_path, manifest_path, packet_root=None):
    """Return the canonical SHA-256 after validating one retained identity file."""
    evidence_bytes = evidence_path.read_bytes()
    document, module_name = _load_dispatch_metadata(evidence_bytes)
    handler = _load_handler(module_name)
    canonical = handler.validate_identity(
        document,
        _read_manifest(manifest_path, handler),
        expected_manifest_sha256=_sha256(manifest_path),
    )
    if evidence_bytes != canonical:
        raise handler.EvidenceError("release identity bytes are not the selected codec's canonical UTF-8 form")
    handler.validate_packet_files(document, packet_root or evidence_path.parent)
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
    except (OSError, DispatchError, ValueError, json.JSONDecodeError) as error:
        print(f"[corrective-release-evidence] fail: {error}", file=sys.stderr)
        return 1
    print(f"[corrective-release-evidence] pass: sha256:{digest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
