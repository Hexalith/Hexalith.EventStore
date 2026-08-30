#!/usr/bin/env python3
"""Assemble the cycle-free Story 3.15 subject and closure from retained bytes."""

import argparse
import hashlib
import stat
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

# Set before the handler import: this producer's own digest is a bound decision input, so it must
# not leave a second executable representation of the trusted handler beside the reviewed source.
# The pinned verifier this script runs over its own output remains the authority -- it loads the
# whole trust path from verified source bytes -- but a producer that quietly wrote bytecode would
# make the two runs disagree about what executed.
sys.dont_write_bytecode = True

from deployed_runtime_parity_handlers import v1  # noqa: E402


VERIFIER_TIMEOUT_SECONDS = 120


def _inside(root, path):
    resolved_root = root.resolve(strict=True)
    resolved = path.resolve(strict=False)
    if resolved != resolved_root and resolved_root not in resolved.parents:
        raise ValueError(f"producer path escapes its trusted root: {path}")
    return resolved


def _regular_bytes(path, root):
    _inside(root, path)
    try:
        mode = path.lstat().st_mode
    except OSError as error:
        raise ValueError(f"producer input is unavailable: {path}") from error
    if path.is_symlink() or not stat.S_ISREG(mode):
        raise ValueError(f"producer input is not a regular file: {path}")
    return path.read_bytes()


def _write_bytes(packet_root, path, content):
    _inside(packet_root, path)
    path.parent.mkdir(parents=True, exist_ok=True)
    _inside(packet_root, path.parent)
    if path.parent.is_symlink() or not path.parent.is_dir():
        raise ValueError(f"producer output parent is not a regular directory: {path.parent}")
    if path.is_symlink() or (path.exists() and not stat.S_ISREG(path.lstat().st_mode)):
        raise ValueError(f"producer output is not a regular file: {path}")
    path.write_bytes(content)


def _validate_packet_tree(packet_root):
    if packet_root.is_symlink() or not packet_root.is_dir():
        raise ValueError(f"packet root is not a regular directory: {packet_root}")
    for path in packet_root.rglob("*"):
        if path.is_symlink():
            raise ValueError(f"packet contains a symbolic link: {path}")
        mode = path.lstat().st_mode
        if not stat.S_ISDIR(mode) and not stat.S_ISREG(mode):
            raise ValueError(f"packet contains an unsupported entry: {path}")


def _require_object(value, fields, message):
    if not isinstance(value, dict) or any(field not in value for field in fields):
        raise ValueError(message)
    return value


def _require_list(value, length, message):
    if not isinstance(value, list) or len(value) != length:
        raise ValueError(message)
    return value


def binding(path, relative, trusted_root):
    content = _regular_bytes(path, trusted_root)
    return {"file": relative, "sha256": hashlib.sha256(content).hexdigest(), "size": len(content)}


def oci_binding(path, relative, media_type, trusted_root):
    result = binding(path, relative, trusted_root)
    result["digest"] = f"sha256:{result['sha256']}"
    result["media_type"] = media_type
    return result


def canonical_write(packet_root, path, value):
    _write_bytes(packet_root, path, v1.canonical_bytes(value))


def repository_root():
    return Path(__file__).resolve().parents[1]


def executing_assembler_path(root):
    """Return this script's own resolved path, refusing anything outside the repository.

    Binding ``root / ASSEMBLER_FILE`` bound the pristine repository file rather than the bytes
    actually running, so a copy executed from elsewhere would have written the repository file's
    digest into the closure it produced.
    """
    path = Path(__file__).resolve()
    expected = (root / v1.ASSEMBLER_FILE).resolve()
    if path != expected:
        raise ValueError(
            f"assembler is executing from {path}, not the bound repository path {expected}")
    return path


def verify_handler_provenance(root):
    """Refuse to assemble when the imported handler modules are not the repository files."""
    for module, relative in (
        (v1, v1.HANDLER_FILE),
        (v1.predecessor_handler, v1.PREDECESSOR_HANDLER_FILE),
    ):
        actual = Path(getattr(module, "__file__", "") or "").resolve()
        expected = (root / relative).resolve()
        if actual != expected:
            raise ValueError(f"{relative} was imported from {actual}, not {expected}")


def build_document(packet_root):
    root = repository_root()
    _validate_packet_tree(packet_root)
    assembler_path = executing_assembler_path(root)
    verify_handler_provenance(root)
    manifest_bytes = _regular_bytes(root / v1.MANIFEST_FILE, root)
    manifest_sha256 = hashlib.sha256(manifest_bytes).hexdigest()
    package_ids = v1.validate_release_manifest(v1.load_json_bytes(manifest_bytes))
    predecessor_bytes = _regular_bytes(root / v1.PREDECESSOR_IDENTITY_FILE, root)
    predecessor = v1.predecessor_handler.load_json_bytes(predecessor_bytes)
    predecessor_canonical = v1.predecessor_handler.validate_identity(
        predecessor,
        package_ids,
        expected_manifest_sha256=manifest_sha256,
    )
    if predecessor_bytes != predecessor_canonical:
        raise ValueError("retained predecessor identity is not canonical")

    registry_source_relative = "registry/role-registry-source.json"
    registry_source_path = packet_root / registry_source_relative
    registry_source_bytes = _regular_bytes(registry_source_path, packet_root)
    registry_source = binding(
        registry_source_path, registry_source_relative, packet_root)
    registry_source_document = v1._github_comment_envelope(  # noqa: SLF001
        registry_source_bytes,
        "retained owner-role authority source structure is invalid",
    )
    registry = {
        "authority_source": registry_source,
        "created_at": registry_source_document["created_at"],
        "repository": v1.REPOSITORY,
        "roles": {role: [v1.EXPECTED_IDENTITIES[role]] for role in v1.REQUIRED_ROLES},
        "schema": v1.REGISTRY_SCHEMA,
    }
    registry_relative = "registry/owner-role-registry.json"
    canonical_write(packet_root, packet_root / registry_relative, registry)

    predecessor_packages = {item["id"]: item for item in predecessor["packages"]}
    package_items = []
    for package_id in package_ids:
        try:
            predecessor_package = predecessor_packages[package_id]
        except KeyError as error:
            raise ValueError(
                f"{package_id} is in the release manifest but missing from the predecessor packet"
            ) from error
        relative = f"packages/{package_id}.{v1.VERSION}.nupkg"
        public_binding = binding(packet_root / relative, relative, packet_root)
        package_items.append(
            {
                "github_release_asset": {
                    "file": predecessor_package["file"],
                    "sha256": predecessor_package["sha256"],
                    "size": predecessor_package["size"],
                },
                "id": package_id,
                "nuget_org": {
                    "download_url": (
                        "https://api.nuget.org/v3-flatcontainer/"
                        f"{package_id.lower()}/{v1.VERSION}/{package_id.lower()}.{v1.VERSION}.nupkg"
                    ),
                    **public_binding,
                    "repository_signature_entry_present": True,
                },
                "repository_commit": v1.SOURCE_SHA,
                "version": v1.VERSION,
            }
        )

    predecessor_children = {child["platform"]: child for child in predecessor["oci"]["children"]}
    children = []
    for platform in v1.PLATFORMS:
        name = platform.replace("/", "-")
        predecessor_child = predecessor_children[platform]
        manifest_relative = f"oci/child-{name}.manifest.raw"
        config_relative = f"oci/child-{name}.config.raw"
        manifest = oci_binding(
            packet_root / manifest_relative,
            manifest_relative,
            v1.MANIFEST_MEDIA_TYPE,
            packet_root,
        )
        config = oci_binding(
            packet_root / config_relative,
            config_relative,
            v1.CONFIG_MEDIA_TYPE,
            packet_root,
        )
        if manifest["digest"] != predecessor_child["manifest"]["digest"]:
            raise ValueError(f"independent {platform} manifest changed")
        if config["digest"] != predecessor_child["config"]["digest"]:
            raise ValueError(f"independent {platform} config changed")
        children.append({"config": config, "manifest": manifest, "platform": platform})
    index_relative = "oci/index.raw"
    index = oci_binding(
        packet_root / index_relative,
        index_relative,
        v1.INDEX_MEDIA_TYPE,
        packet_root,
    )
    if index["digest"] != v1.INDEX_DIGEST:
        raise ValueError("independent OCI index changed")

    results_relative = "smokes/smoke-results.json"
    technical_files = {
        item["nuget_org"]["file"] for item in package_items
    }
    technical_files.update((index_relative, results_relative, registry_relative, registry_source_relative))
    for child in children:
        technical_files.update((child["manifest"]["file"], child["config"]["file"]))
    smoke_results = v1.load_json_bytes(
        _regular_bytes(packet_root / results_relative, packet_root))
    _require_object(
        smoke_results,
        ("exit_code", "platforms", "result"),
        "retained Production smoke results structure is invalid",
    )
    smoke_platforms = _require_list(
        smoke_results["platforms"],
        len(v1.PLATFORMS),
        "retained Production smoke platform structure is invalid",
    )
    for item in smoke_platforms:
        _require_object(
            item,
            ("child_digest", "log", "outcome", "platform"),
            "retained Production smoke platform structure is invalid",
        )
        _require_object(
            item["log"],
            ("file", "sha256", "size"),
            "retained Production smoke log binding is invalid",
        )
    # "deployed_runtime_parity": "available" was written unconditionally, so a packet assembled over
    # a failed smoke run still declared parity and only a separate verifier run could contradict it.
    # Refuse to assemble unless the retained smokes actually passed on both immutable children.
    if smoke_results.get("result") != "pass" or smoke_results.get("exit_code") != 0:
        raise ValueError("retained Production smokes did not pass")
    smoke_children = {item.get("child_digest") for item in smoke_platforms}
    if smoke_children != {child["manifest"]["digest"] for child in children}:
        raise ValueError("retained Production smokes do not cover the selected children")
    if any(item.get("outcome") != "pass" for item in smoke_platforms):
        raise ValueError("a retained Production smoke platform did not pass")
    technical_files.update(item["log"]["file"] for item in smoke_platforms)
    inventory_text = "".join(
        f"{hashlib.sha256(_regular_bytes(packet_root / relative, packet_root)).hexdigest()}  {relative}\n"
        for relative in sorted(technical_files)
    ).encode("utf-8")
    inventory_relative = "technical-sha256.txt"
    _write_bytes(packet_root, packet_root / inventory_relative, inventory_text)

    handler_binding = binding(root / v1.HANDLER_FILE, v1.HANDLER_FILE, root)
    verifier_binding = binding(root / v1.VERIFIER_FILE, v1.VERIFIER_FILE, root)
    predecessor_handler_binding = binding(
        root / v1.PREDECESSOR_HANDLER_FILE, v1.PREDECESSOR_HANDLER_FILE, root)
    predecessor_package_binding = binding(
        root / v1.PREDECESSOR_PACKAGE_FILE, v1.PREDECESSOR_PACKAGE_FILE, root)
    # Both producers are decision inputs even though the verifier never runs them: the capture tool
    # decides what a passing Production smoke means and this assembler decides how the packet is
    # derived. Binding their digests makes any producer edit re-mint the subject.
    capture_binding = binding(root / v1.CAPTURE_FILE, v1.CAPTURE_FILE, root)
    assembler_binding = binding(assembler_path, v1.ASSEMBLER_FILE, root)
    existing_subject = packet_root / "subject.json"
    created_at = datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")
    if existing_subject.exists():
        existing_subject_document = v1.load_json_bytes(
            _regular_bytes(existing_subject, packet_root))
        _require_object(
            existing_subject_document,
            ("created_at",),
            "retained subject structure is invalid",
        )
        v1._parse_time(  # noqa: SLF001
            existing_subject_document["created_at"],
            "retained subject timestamp is invalid",
        )
        created_at = existing_subject_document["created_at"]
    document = {
        "acceptances": {"directory": "", "receipts": []},
        "consumer_removal_authorized": False,
        "deployed_runtime_parity": "available",
        "deployment_authorized": False,
        "dispatch": {
            "assembler": assembler_binding,
            "capture": capture_binding,
            "handler": handler_binding,
            "predecessor_handler": predecessor_handler_binding,
            "predecessor_package": predecessor_package_binding,
            "schema": v1.SCHEMA,
            "verifier": verifier_binding,
            "version": v1.CODEC_VERSION,
        },
        "grants_mutation_authority": False,
        "lineage": {
            "source_sha": v1.SOURCE_SHA,
            "tag": v1.TAG,
            "version": v1.VERSION,
            "workflow": predecessor["workflow"],
        },
        "oci": {
            "children": children,
            "image": f"registry.hexalith.com/eventstore@{v1.INDEX_DIGEST}",
            "index": index,
        },
        "owner_role_registry": binding(
            packet_root / registry_relative, registry_relative, packet_root),
        "packages": {"count": len(package_items), "items": package_items, "manifest_sha256": manifest_sha256},
        "predecessor": {
            "identity_file": v1.PREDECESSOR_IDENTITY_FILE,
            "packet_root": v1.PREDECESSOR_PACKET_ROOT,
            "publication_authority_sha256": predecessor["authority"]["authority_record_sha256"],
            "sha256": v1.PREDECESSOR_SHA256,
        },
        "production_smokes": {
            "results": binding(packet_root / results_relative, results_relative, packet_root)
        },
        "publication_authorized": False,
        "repository": v1.REPOSITORY,
        "rerun_trigger": v1.RERUN_TRIGGER,
        "schema": v1.SCHEMA,
        "selected_deployed_identity": v1.INDEX_DIGEST,
        "story_id": "3.15",
        "subject": {"created_at": created_at, "file": "subject.json", "sha256": "0" * 64, "size": 1},
        "technical_inventory": binding(
            packet_root / inventory_relative, inventory_relative, packet_root),
    }
    subject = v1._expected_subject(document)  # noqa: SLF001
    subject_bytes = v1.canonical_bytes(subject)
    if existing_subject.exists() and _regular_bytes(existing_subject, packet_root) != subject_bytes:
        # Content changed, so the carried-forward timestamp is wrong: re-stamp and recompute.
        created_at = datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")
        document["subject"] = {"created_at": created_at, "file": "subject.json", "sha256": "0" * 64, "size": 1}
        subject = v1._expected_subject(document)  # noqa: SLF001
        subject_bytes = v1.canonical_bytes(subject)
    _write_bytes(packet_root, packet_root / "subject.json", subject_bytes)
    subject_sha256 = hashlib.sha256(subject_bytes).hexdigest()
    document["subject"] = {
        "created_at": created_at,
        "file": "subject.json",
        "sha256": subject_sha256,
        "size": len(subject_bytes),
    }
    acceptance_directory = f"acceptances/{subject_sha256}"
    document["acceptances"]["directory"] = acceptance_directory
    receipt_root = packet_root / acceptance_directory
    if receipt_root.is_dir():
        for role in v1.REQUIRED_ROLES:
            path = receipt_root / f"{role}.json"
            if path.is_file() and not path.is_symlink():
                document["acceptances"]["receipts"].append(
                    {
                        "role": role,
                        **binding(
                            path,
                            f"{acceptance_directory}/{role}.json",
                            packet_root,
                        ),
                    }
                )
    return document, subject_sha256


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("packet_root", type=Path)
    arguments = parser.parse_args()
    try:
        document, subject_sha256 = build_document(arguments.packet_root)
        canonical_write(
            arguments.packet_root,
            arguments.packet_root / "closure.json",
            document,
        )
    except (OSError, TypeError, ValueError) as error:
        print(
            f"[corrected-deployed-runtime-parity-assembly] fail: {error}; "
            f"rerun: {v1.RERUN_TRIGGER}",
            file=sys.stderr,
        )
        return 1
    receipts = len(document["acceptances"]["receipts"])
    # Assemble and verify are one operation: emitting a packet without running the pinned verifier
    # over it is how a rejected packet acquired a success-shaped assembly line and exit 0.
    try:
        verdict = subprocess.run(
            [
                sys.executable,
                str(Path(__file__).resolve().parent / "validate-corrected-deployed-runtime-parity.py"),
                str(arguments.packet_root / "closure.json"),
                "--packet-root",
                str(arguments.packet_root),
            ],
            check=False,
            capture_output=True,
            text=True,
            timeout=VERIFIER_TIMEOUT_SECONDS,
        )
    except (OSError, subprocess.SubprocessError) as error:
        print(
            "[corrected-deployed-runtime-parity-assembly] fail: "
            f"the bounded verifier process could not complete: {error}; "
            f"rerun: {v1.RERUN_TRIGGER}",
            file=sys.stderr,
        )
        return 1
    sys.stderr.write(verdict.stderr)
    print(
        "[corrected-deployed-runtime-parity-assembly] "
        f"subject=sha256:{subject_sha256} receipts={receipts} verifier_exit={verdict.returncode}"
    )
    if verdict.returncode != 0:
        return verdict.returncode
    return 0 if receipts == len(v1.REQUIRED_ROLES) else 1


if __name__ == "__main__":
    sys.exit(main())
