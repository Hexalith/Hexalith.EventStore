#!/usr/bin/env python3
"""Validate retained Story 3.14 evidence and print its canonical identity digest."""

import sys

# Fail closed before any other import can resolve. Running this file as a script puts its own
# directory on sys.path[0], so a repository-local tools/json.py -- or argparse, hashlib, types,
# pathlib -- executes ahead of every pin, long before validate() gets a chance to sanitize
# sys.path. Re-exec once under isolated mode, which drops the script directory, PYTHONPATH and
# the user site directory from import resolution. os is resolved through sys.modules rather than
# imported, because that import is itself shadowable; the interpreter has already loaded the real
# module during startup.
if __name__ == "__main__" and not sys.flags.isolated:
    _os = sys.modules.get("os")
    if _os is None:
        raise SystemExit(
            "[corrective-release-evidence] fail: cannot establish isolated import resolution")
    _os.execv(
        sys.executable,
        [sys.executable, "-I", _os.path.abspath(__file__), *sys.argv[1:]],
    )

import argparse  # noqa: E402
import hashlib  # noqa: E402
import importlib.machinery  # noqa: E402
import json  # noqa: E402
import os  # noqa: E402
import types  # noqa: E402
from pathlib import Path  # noqa: E402


SCHEMA = "hexalith.eventstore.corrective-release-identity.v1"
V3_PACKET_CODEC_SHA256 = "814502bd962e00dfbac243e2443c3709b46bdbb69e197691443a083e283d32a9"
RERUN_TRIGGER = (
    "Restore the frozen Story 3.14 packet, trusted verifier bytes, and release manifest, then rerun "
    "the complete corrective-release validation command."
)
HANDLERS = {
    (SCHEMA, 3, V3_PACKET_CODEC_SHA256): "release_evidence_handlers.v3",
}
# V3_PACKET_CODEC_SHA256 (above, keyed into HANDLERS) pins the codec/verifier bytes the packet
# retains as evidence -- it says nothing about the executing module. This table pins the actual
# on-disk handler source before import, so an unreviewed edit never executes. Recompute with:
# sha256sum tools/release_evidence_handlers/v3.py
HANDLER_FILE_SHA256 = {
    "release_evidence_handlers.v3": "a421791b4c6176afc8120e4e5c4668cb9703976e6f74659c0525119fc5aca5f4",
}
# Importing a submodule also executes its package initializer, so pinning the leaf alone leaves
# that file free to run unreviewed code. Every file on the import path is pinned and verified
# before the first import, and the paths are resolved from this script rather than through
# importlib.util.find_spec, because find_spec itself imports the parent package. Recompute with:
# sha256sum tools/release_evidence_handlers/__init__.py
HANDLER_PACKAGE_FILE_SHA256 = {
    "release_evidence_handlers": "a33b53f823fa36b822395aee2d01597091b37c26248995c2629b0a9e30c70625",
}


# The handlers are executed from source bytes this script hashes itself (see _load_verified_module),
# so no .pyc is ever consulted for them. This setting is the matching hygiene on the write side:
# it stops a run from leaving behind bytecode that some other, ordinary import path could later
# treat as a second executable representation of a handler.
sys.dont_write_bytecode = True


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
        version = codec["version"]
        if type(version) is not int:
            raise DispatchError("release identity dispatch metadata is invalid")
        key = (document["schema"], version, binding["sha256"])
        if key not in HANDLERS:
            raise DispatchError("release identity does not select a trusted live handler")
    except (KeyError, TypeError, json.JSONDecodeError) as error:
        raise DispatchError("release identity dispatch metadata is invalid") from error
    return document, HANDLERS[key]


def _verify_pinned_source(path, expected):
    # Each failure gets its own sentence: an operator reading a CI log must be able to tell an
    # unpinned file from an unreadable one from an edited one without opening this script.
    if expected is None:
        raise DispatchError("trusted live handler file is not pinned in the dispatch table")
    try:
        source = path.read_bytes()
    except OSError as error:
        raise DispatchError("trusted live handler source could not be read") from error
    if hashlib.sha256(source).hexdigest() != expected:
        raise DispatchError("trusted live handler source does not match its pinned SHA-256")
    return source


def _repository_root():
    return Path(__file__).resolve().parents[1]


def _is_repository_path(value):
    if (
        not isinstance(value, (str, bytes))
        or not value
        or value in ("built-in", "frozen")
        or (isinstance(value, str) and value.startswith("<"))
    ):
        return False
    try:
        # os.fsdecode, not Path(value): Path raises TypeError on bytes, which is not in the caught
        # tuple, so a bytes __file__ produced a raw traceback instead of the fail: line.
        path = Path(os.fsdecode(value)).resolve()
    except (OSError, RuntimeError, TypeError, ValueError):
        return False
    root = _repository_root().resolve()
    return path == root or root in path.parents


def _module_is_repository_local(module):
    if _is_repository_path(getattr(module, "__file__", None)):
        return True
    spec = getattr(module, "__spec__", None)
    return _is_repository_path(getattr(spec, "origin", None))


def _begin_trusted_import_environment():
    """Isolate verified execution from search-path and preloaded repository shadows."""
    original_path = list(sys.path)
    sys.path[:] = [entry for entry in original_path if not _is_repository_path(entry or str(Path.cwd()))]
    trusted_names = {"release_evidence_handlers", "release_evidence_handlers.v3"}
    displaced = {}
    for name, module in list(sys.modules.items()):
        if name == "__main__":
            continue
        if name in trusted_names or (module is not None and _module_is_repository_local(module)):
            displaced[name] = module
            sys.modules.pop(name, None)
    return original_path, displaced, trusted_names


def _verify_no_repository_import_shadows(trusted_names):
    for name, module in list(sys.modules.items()):
        if name in trusted_names or name == "__main__" or module is None:
            continue
        if _module_is_repository_local(module):
            raise DispatchError("trusted live handler resolved a repository-local import shadow")


def _end_trusted_import_environment(original_path, displaced, trusted_names):
    for name in trusted_names:
        sys.modules.pop(name, None)
    sys.modules.update(displaced)
    sys.path[:] = original_path


def _load_verified_module(module_name, path, source, *, is_package=False):
    """Compile and execute exactly one already verified source file."""
    module = types.ModuleType(module_name)
    module.__file__ = str(path.resolve())
    module.__package__ = module_name if is_package else module_name.rpartition(".")[0]
    module.__loader__ = None
    module.__spec__ = importlib.machinery.ModuleSpec(module_name, loader=None, is_package=is_package)
    if is_package:
        module.__path__ = [str(path.resolve().parent)]
        module.__spec__.submodule_search_locations = module.__path__
    sys.modules[module_name] = module
    try:
        exec(compile(source, str(path.resolve()), "exec", dont_inherit=True), module.__dict__)
    except Exception as error:
        sys.modules.pop(module_name, None)
        raise DispatchError("trusted live handler could not be loaded") from error
    return module


def _load_handler(module_name):
    if set(HANDLERS.values()) != set(HANDLER_FILE_SHA256):
        raise DispatchError("trusted live handler configuration is inconsistent")
    package_name, _, leaf_name = module_name.rpartition(".")
    if not package_name or not leaf_name:
        raise DispatchError("trusted live handler module name is malformed")
    if set(HANDLER_PACKAGE_FILE_SHA256) != {name.rpartition(".")[0] for name in HANDLER_FILE_SHA256}:
        raise DispatchError("trusted live handler configuration is inconsistent")
    # Resolve from this script, never through find_spec: find_spec imports the parent package,
    # which would execute the initializer before it could be verified.
    package_root = Path(__file__).resolve().parent / package_name
    package_path = package_root / "__init__.py"
    handler_path = package_root / (leaf_name + ".py")
    package_source = _verify_pinned_source(
        package_path, HANDLER_PACKAGE_FILE_SHA256.get(package_name))
    handler_source = _verify_pinned_source(
        handler_path, HANDLER_FILE_SHA256.get(module_name))
    package = _load_verified_module(package_name, package_path, package_source, is_package=True)
    module = _load_verified_module(module_name, handler_path, handler_source)
    setattr(package, leaf_name, module)
    # No "was it imported from the verified path?" check here: _load_verified_module sets __file__
    # from that same path and executes only bytes this function hashed, so such a check compares a
    # value to its own origin and can never fail. The pin above is the real guarantee.
    if getattr(module, "EXPECTED_PACKET_CODEC_SHA256", None) != V3_PACKET_CODEC_SHA256:
        raise DispatchError("trusted live handler codec pin is inconsistent")
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
    original_path, displaced, trusted_names = _begin_trusted_import_environment()
    try:
        handler = _load_handler(module_name)
        canonical = handler.validate_identity(
            document,
            _read_manifest(manifest_path, handler),
            expected_manifest_sha256=_sha256(manifest_path),
        )
        if evidence_bytes != canonical:
            raise handler.EvidenceError("release identity bytes are not the selected codec's canonical UTF-8 form")
        handler.validate_packet_files(document, packet_root or evidence_path.parent)
        _verify_no_repository_import_shadows(trusted_names)
        return hashlib.sha256(canonical).hexdigest()
    finally:
        _end_trusted_import_environment(original_path, displaced, trusted_names)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument(
        "--manifest",
        type=Path,
        default=_repository_root() / "tools" / "release-packages.json",
    )
    parser.add_argument(
        "--packet-root",
        type=Path,
        help="Root used to resolve retained package, OCI, and smoke evidence paths.",
    )
    arguments = parser.parse_args()
    try:
        digest = validate(arguments.evidence, arguments.manifest, arguments.packet_root)
    except (OSError, DispatchError, ValueError, json.JSONDecodeError) as error:
        print(
            f"[corrective-release-evidence] fail: {error}; rerun: {RERUN_TRIGGER}",
            file=sys.stderr,
        )
        return 1
    print(f"[corrective-release-evidence] pass: sha256:{digest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
