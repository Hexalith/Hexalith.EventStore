#!/usr/bin/env python3
"""Assemble the cycle-free Story 3.15 subject and closure from retained bytes."""

import argparse
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

from deployed_runtime_parity_handlers import v1


def binding(path, relative):
    content = path.read_bytes()
    return {"file": relative, "sha256": hashlib.sha256(content).hexdigest(), "size": len(content)}


def oci_binding(path, relative, media_type):
    result = binding(path, relative)
    result["digest"] = f"sha256:{result['sha256']}"
    result["media_type"] = media_type
    return result


def canonical_write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(v1.canonical_bytes(value))


def repository_root():
    return Path(__file__).resolve().parents[1]


def build_document(packet_root):
    root = repository_root()
    predecessor = v1.predecessor_handler.load_json_bytes(
        (root / v1.PREDECESSOR_IDENTITY_FILE).read_bytes()
    )
    manifest_sha256 = hashlib.sha256((root / v1.MANIFEST_FILE).read_bytes()).hexdigest()

    registry_source_relative = "registry/role-registry-source.json"
    registry_source = binding(packet_root / registry_source_relative, registry_source_relative)
    registry_source_document = v1.load_json_bytes((packet_root / registry_source_relative).read_bytes())
    registry = {
        "authority_source": registry_source,
        "created_at": registry_source_document["created_at"],
        "repository": v1.REPOSITORY,
        "roles": {role: [v1.EXPECTED_IDENTITIES[role]] for role in v1.REQUIRED_ROLES},
        "schema": v1.REGISTRY_SCHEMA,
    }
    registry_relative = "registry/owner-role-registry.json"
    canonical_write(packet_root / registry_relative, registry)

    predecessor_packages = {item["id"]: item for item in predecessor["packages"]}
    package_ids = v1.validate_release_manifest(v1.load_json_bytes((root / v1.MANIFEST_FILE).read_bytes()))
    package_items = []
    for package_id in package_ids:
        try:
            predecessor_package = predecessor_packages[package_id]
        except KeyError as error:
            raise ValueError(
                f"{package_id} is in the release manifest but missing from the predecessor packet"
            ) from error
        relative = f"packages/{package_id}.{v1.VERSION}.nupkg"
        public_binding = binding(packet_root / relative, relative)
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
        manifest = oci_binding(packet_root / manifest_relative, manifest_relative, v1.MANIFEST_MEDIA_TYPE)
        config = oci_binding(packet_root / config_relative, config_relative, v1.CONFIG_MEDIA_TYPE)
        if manifest["digest"] != predecessor_child["manifest"]["digest"]:
            raise ValueError(f"independent {platform} manifest changed")
        if config["digest"] != predecessor_child["config"]["digest"]:
            raise ValueError(f"independent {platform} config changed")
        children.append({"config": config, "manifest": manifest, "platform": platform})
    index_relative = "oci/index.raw"
    index = oci_binding(packet_root / index_relative, index_relative, v1.INDEX_MEDIA_TYPE)
    if index["digest"] != v1.INDEX_DIGEST:
        raise ValueError("independent OCI index changed")

    results_relative = "smokes/smoke-results.json"
    technical_files = {
        item["nuget_org"]["file"] for item in package_items
    }
    technical_files.update((index_relative, results_relative, registry_relative, registry_source_relative))
    for child in children:
        technical_files.update((child["manifest"]["file"], child["config"]["file"]))
    smoke_results = v1.load_json_bytes((packet_root / results_relative).read_bytes())
    technical_files.update(item["log"]["file"] for item in smoke_results["platforms"])
    inventory_text = "".join(
        f"{hashlib.sha256((packet_root / relative).read_bytes()).hexdigest()}  {relative}\n"
        for relative in sorted(technical_files)
    ).encode("utf-8")
    inventory_relative = "technical-sha256.txt"
    (packet_root / inventory_relative).write_bytes(inventory_text)

    handler_binding = binding(root / v1.HANDLER_FILE, v1.HANDLER_FILE)
    verifier_binding = binding(root / v1.VERIFIER_FILE, v1.VERIFIER_FILE)
    predecessor_handler_binding = binding(
        root / v1.PREDECESSOR_HANDLER_FILE, v1.PREDECESSOR_HANDLER_FILE)
    predecessor_package_binding = binding(
        root / v1.PREDECESSOR_PACKAGE_FILE, v1.PREDECESSOR_PACKAGE_FILE)
    existing_subject = packet_root / "subject.json"
    created_at = datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")
    if existing_subject.exists():
        created_at = v1.load_json_bytes(existing_subject.read_bytes())["created_at"]
    document = {
        "acceptances": {"directory": "", "receipts": []},
        "consumer_removal_authorized": False,
        "deployed_runtime_parity": "available",
        "deployment_authorized": False,
        "dispatch": {
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
        "owner_role_registry": binding(packet_root / registry_relative, registry_relative),
        "packages": {"count": 14, "items": package_items, "manifest_sha256": manifest_sha256},
        "predecessor": {
            "identity_file": v1.PREDECESSOR_IDENTITY_FILE,
            "packet_root": v1.PREDECESSOR_PACKET_ROOT,
            "publication_authority_sha256": predecessor["authority"]["authority_record_sha256"],
            "sha256": v1.PREDECESSOR_SHA256,
        },
        "production_smokes": {"results": binding(packet_root / results_relative, results_relative)},
        "publication_authorized": False,
        "repository": v1.REPOSITORY,
        "rerun_trigger": v1.RERUN_TRIGGER,
        "schema": v1.SCHEMA,
        "selected_deployed_identity": v1.INDEX_DIGEST,
        "story_id": "3.15",
        "subject": {"created_at": created_at, "file": "subject.json", "sha256": "0" * 64, "size": 1},
        "technical_inventory": binding(packet_root / inventory_relative, inventory_relative),
    }
    subject = v1._expected_subject(document)  # noqa: SLF001
    subject_bytes = v1.canonical_bytes(subject)
    (packet_root / "subject.json").write_bytes(subject_bytes)
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
            if path.is_file():
                document["acceptances"]["receipts"].append(
                    {"role": role, **binding(path, f"{acceptance_directory}/{role}.json")}
                )
    return document, subject_sha256


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("packet_root", type=Path)
    arguments = parser.parse_args()
    document, subject_sha256 = build_document(arguments.packet_root)
    canonical_write(arguments.packet_root / "closure.json", document)
    print(
        "[corrected-deployed-runtime-parity-assembly] "
        f"subject=sha256:{subject_sha256} receipts={len(document['acceptances']['receipts'])}"
    )


if __name__ == "__main__":
    main()
