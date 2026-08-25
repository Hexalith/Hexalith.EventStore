#!/usr/bin/env python3
"""Validate retained Story 3.15 deployed-runtime parity closure evidence."""

import argparse
import hashlib
import importlib
import json
import sys
from pathlib import Path


SCHEMA = "hexalith.eventstore.corrected-deployed-runtime-parity.v1"
V1_HANDLER_SHA256 = "c493cb5870774bef4281566b0b0425e21f9799ce7b6ed21bac4cb18c2c0a3272"
HANDLERS = {
    (SCHEMA, 1, V1_HANDLER_SHA256): "deployed_runtime_parity_handlers.v1",
}
# The closure's own dispatch block re-verifies these bytes, but only from inside v1.py -- by then
# the module body, its package initializer, and everything it imports have already run. Every file
# on the import path is pinned and verified here, before the first import, so an unreviewed edit
# never executes. Paths are resolved from this script rather than importlib.util.find_spec, because
# find_spec imports the parent package. Recompute each with sha256sum.
IMPORT_PATH_FILE_SHA256 = {
    "deployed_runtime_parity_handlers/__init__.py":
        "39efb6d37ba6ff98a791d5af4cdc5e099824f7f210258efd917776c8135c613d",
    "deployed_runtime_parity_handlers/v1.py": V1_HANDLER_SHA256,
    "release_evidence_handlers/__init__.py":
        "a33b53f823fa36b822395aee2d01597091b37c26248995c2629b0a9e30c70625",
    "release_evidence_handlers/v3.py":
        "3f366eee1509f5350806b9277eb514d20987790fff5b248f81155dbb5857d490",
}


class DispatchError(ValueError):
    """The retained packet does not select a trusted live handler."""


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise DispatchError("closure contains duplicate JSON fields")
        result[key] = value
    return result


def _load_dispatch_metadata(evidence_bytes):
    try:
        document = json.loads(evidence_bytes, object_pairs_hook=_unique_object)
        dispatch = document["dispatch"]
        key = (dispatch["schema"], dispatch["version"], dispatch["handler"]["sha256"])
    except (KeyError, TypeError, json.JSONDecodeError) as error:
        raise DispatchError("closure dispatch metadata is invalid") from error
    if key not in HANDLERS:
        raise DispatchError("closure does not select a trusted live handler")
    return document, HANDLERS[key]


def _repository_root():
    return Path(__file__).resolve().parents[1]


def _verify_import_path():
    tools_root = Path(__file__).resolve().parent
    for relative, expected in sorted(IMPORT_PATH_FILE_SHA256.items()):
        try:
            source = (tools_root / relative).read_bytes()
        except OSError as error:
            raise DispatchError("trusted live handler source is unavailable") from error
        if hashlib.sha256(source).hexdigest() != expected:
            raise DispatchError("trusted live handler source does not match its pinned SHA-256")


def validate(evidence_path, packet_root=None):
    """Return the canonical subject digest after validating one closure packet."""
    repository_root = _repository_root()
    manifest_path = repository_root / "tools/release-packages.json"
    evidence_bytes = evidence_path.read_bytes()
    document, module_name = _load_dispatch_metadata(evidence_bytes)
    _verify_import_path()
    handler = importlib.import_module(module_name)
    expected_package_ids = handler.validate_release_manifest(handler.load_json_bytes(manifest_path.read_bytes()))
    manifest_sha256 = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    canonical = handler.validate_identity(
        document,
        expected_package_ids,
        manifest_sha256,
        repository_root,
    )
    if evidence_bytes != canonical:
        raise handler.EvidenceError("closure bytes are not the selected codec's canonical UTF-8 form")
    handler.validate_packet_files(
        document,
        packet_root or evidence_path.parent,
        expected_package_ids,
        manifest_sha256,
        repository_root,
    )
    return document["subject"]["sha256"], document["selected_deployed_identity"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument(
        "--packet-root",
        type=Path,
        help="Root used to resolve retained package, OCI, smoke, registry, subject, and receipt paths.",
    )
    arguments = parser.parse_args()
    try:
        subject_sha256, selected_identity = validate(arguments.evidence, arguments.packet_root)
    except (OSError, DispatchError, ValueError, json.JSONDecodeError) as error:
        print(f"[corrected-deployed-runtime-parity] fail: {error}", file=sys.stderr)
        return 1
    print(
        "[corrected-deployed-runtime-parity] pass: "
        f"subject=sha256:{subject_sha256} selected={selected_identity}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
