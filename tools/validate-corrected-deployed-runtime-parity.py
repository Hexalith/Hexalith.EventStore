#!/usr/bin/env python3
"""Validate retained Story 3.15 deployed-runtime parity closure evidence."""

import argparse
import hashlib
import importlib.machinery
import json
import sys
import types
from pathlib import Path


SCHEMA = "hexalith.eventstore.corrected-deployed-runtime-parity.v1"
V3_PACKET_CODEC_SHA256 = "814502bd962e00dfbac243e2443c3709b46bdbb69e197691443a083e283d32a9"
RERUN_TRIGGER = (
    "Rebuild the complete subject and reject all prior receipts after any predecessor, package, OCI, "
    "Production-smoke, inventory, registry, verifier, decision, or receipt-source change."
)
V1_HANDLER_SHA256 = "e8b6538b11d626bb248fa9a0a61b22a2b2d4c39b5e6561f4e334a50a731f6b3d"
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
        "5cb007497466090956eb35a7d5340cfe4c9e07920a1b55dd1801d422509e0f1b",
}
EXPECTED_IMPORT_PATH_FILES = {
    "deployed_runtime_parity_handlers/__init__.py",
    "deployed_runtime_parity_handlers/v1.py",
    "release_evidence_handlers/__init__.py",
    "release_evidence_handlers/v3.py",
}


# The source-only loader below never asks importlib for code, so a timestamp-valid stale .pyc cannot
# stand in for the bytes whose SHA-256 was checked. Keep bytecode writes disabled too: verifier runs
# must not create a second executable representation beside the reviewed sources.
sys.dont_write_bytecode = True


class DispatchError(ValueError):
    """The retained packet does not select a trusted live handler."""


def _verify_dispatch_table():
    """Fail closed when a registered handler module is absent from the pin table.

    Without this, adding an entry to HANDLERS and forgetting IMPORT_PATH_FILE_SHA256 imports that
    module unpinned -- the sibling dispatcher gained the equivalent guard for the same reason.
    """
    registered = {module.replace(".", "/") + ".py" for module in HANDLERS.values()}
    if (
        not registered.issubset(EXPECTED_IMPORT_PATH_FILES)
        or set(IMPORT_PATH_FILE_SHA256) != EXPECTED_IMPORT_PATH_FILES
    ):
        raise DispatchError("trusted live handler configuration is inconsistent")


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
        if key not in HANDLERS:
            raise DispatchError("closure does not select a trusted live handler")
    except (KeyError, TypeError, json.JSONDecodeError) as error:
        raise DispatchError("closure dispatch metadata is invalid") from error
    return document, HANDLERS[key]


def _repository_root():
    return Path(__file__).resolve().parents[1]


def _verify_import_path():
    tools_root = Path(__file__).resolve().parent
    sources = {}
    for relative, expected in sorted(IMPORT_PATH_FILE_SHA256.items()):
        try:
            source = (tools_root / relative).read_bytes()
        except OSError as error:
            raise DispatchError("trusted live handler source is unavailable") from error
        if hashlib.sha256(source).hexdigest() != expected:
            raise DispatchError(
                f"trusted live handler source does not match its pinned SHA-256: {relative}"
            )
        sources[relative] = source
    return sources


def _load_verified_module(module_name, relative, source, *, is_package=False):
    """Compile and execute exactly the verified source bytes, never cached bytecode."""
    path = (Path(__file__).resolve().parent / relative).resolve()
    module = types.ModuleType(module_name)
    module.__file__ = str(path)
    module.__package__ = module_name if is_package else module_name.rpartition(".")[0]
    module.__loader__ = None
    module.__spec__ = importlib.machinery.ModuleSpec(module_name, loader=None, is_package=is_package)
    if is_package:
        module.__path__ = [str(path.parent)]
        module.__spec__.submodule_search_locations = module.__path__
    sys.modules[module_name] = module
    try:
        exec(compile(source, str(path), "exec", dont_inherit=True), module.__dict__)
    except Exception as error:
        sys.modules.pop(module_name, None)
        raise DispatchError("trusted live handler could not be loaded") from error
    return module


def _load_handler(module_name, sources):
    """Load the complete four-file trust path from its already verified source bytes."""
    predecessor_package = _load_verified_module(
        "release_evidence_handlers",
        "release_evidence_handlers/__init__.py",
        sources["release_evidence_handlers/__init__.py"],
        is_package=True,
    )
    predecessor_handler = _load_verified_module(
        "release_evidence_handlers.v3",
        "release_evidence_handlers/v3.py",
        sources["release_evidence_handlers/v3.py"],
    )
    predecessor_package.v3 = predecessor_handler
    handler_package = _load_verified_module(
        "deployed_runtime_parity_handlers",
        "deployed_runtime_parity_handlers/__init__.py",
        sources["deployed_runtime_parity_handlers/__init__.py"],
        is_package=True,
    )
    handler = _load_verified_module(
        module_name,
        module_name.replace(".", "/") + ".py",
        sources[module_name.replace(".", "/") + ".py"],
    )
    handler_package.v1 = handler
    if predecessor_handler.EXPECTED_PACKET_CODEC_SHA256 != V3_PACKET_CODEC_SHA256:
        raise DispatchError("trusted predecessor handler codec pin is inconsistent")
    if handler.RERUN_TRIGGER != RERUN_TRIGGER:
        raise DispatchError("trusted live handler rerun trigger is inconsistent")
    return handler, predecessor_handler, handler_package, predecessor_package


def _verify_imported_file(module, relative):
    """Fail closed when the imported module is not the file whose bytes were verified.

    _verify_import_path hashes tools/<relative>, but importlib resolves through sys.path
    independently, so without this the verified file and the executed file can differ.
    """
    expected = (Path(__file__).resolve().parent / relative).resolve()
    actual = Path(getattr(module, "__file__", "") or "").resolve()
    if actual != expected:
        raise DispatchError("trusted live handler was not imported from its verified path")


def validate(evidence_path, manifest_path=None, packet_root=None):
    """Return the canonical subject digest after validating one closure packet."""
    repository_root = _repository_root()
    evidence_bytes = evidence_path.read_bytes()
    document, module_name = _load_dispatch_metadata(evidence_bytes)
    _verify_dispatch_table()
    sources = _verify_import_path()
    handler, predecessor_handler, handler_package, predecessor_package = _load_handler(
        module_name, sources)
    _verify_imported_file(handler, module_name.replace(".", "/") + ".py")
    _verify_imported_file(predecessor_handler, "release_evidence_handlers/v3.py")
    _verify_imported_file(handler_package, "deployed_runtime_parity_handlers/__init__.py")
    _verify_imported_file(predecessor_package, "release_evidence_handlers/__init__.py")
    manifest_path = manifest_path or repository_root / handler.MANIFEST_FILE
    manifest_bytes = manifest_path.read_bytes()
    expected_package_ids = handler.validate_release_manifest(handler.load_json_bytes(manifest_bytes))
    manifest_sha256 = hashlib.sha256(manifest_bytes).hexdigest()
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
        "--manifest",
        type=Path,
        help="Release package manifest; defaults to the trusted handler's repository-relative path.",
    )
    parser.add_argument(
        "--packet-root",
        type=Path,
        help="Root used to resolve retained package, OCI, smoke, registry, subject, and receipt paths.",
    )
    arguments = parser.parse_args()
    try:
        subject_sha256, selected_identity = validate(
            arguments.evidence, arguments.manifest, arguments.packet_root)
    except (OSError, DispatchError, ValueError, json.JSONDecodeError) as error:
        print(
            f"[corrected-deployed-runtime-parity] fail: {error}; rerun: {RERUN_TRIGGER}",
            file=sys.stderr,
        )
        return 1
    print(
        "[corrected-deployed-runtime-parity] pass: "
        f"subject=sha256:{subject_sha256} selected={selected_identity}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
