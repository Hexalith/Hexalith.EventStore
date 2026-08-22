"""Trusted Story 3.15 deployed-runtime parity closure verifier."""

from __future__ import annotations

import hashlib
import json
import re
import zipfile
from datetime import datetime
from pathlib import Path

from release_evidence_handlers import v3 as predecessor_handler


SCHEMA = "hexalith.eventstore.corrected-deployed-runtime-parity.v1"
SUBJECT_SCHEMA = "hexalith.eventstore.corrected-deployed-runtime-parity-subject.v1"
REGISTRY_SCHEMA = "hexalith.eventstore.owner-role-registry.v1"
SMOKE_SCHEMA = "hexalith.eventstore.production-smoke-results.v1"
SMOKE_LOG_SCHEMA = "hexalith.eventstore.production-smoke-log.v1"
RECEIPT_SCHEMA = "hexalith.eventstore.deployed-runtime-parity-acceptance.v1"
TEST_ARCHITECT_SOURCE_SCHEMA = "hexalith.eventstore.test-architect-acceptance-source.v1"
CODEC_VERSION = 1
REPOSITORY = "Hexalith/Hexalith.EventStore"
SOURCE_SHA = "f343bb0153e9cdcb8b12ec10153813072f5ad38d"
VERSION = "3.96.2"
TAG = "v3.96.2"
PREDECESSOR_SHA256 = "4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9"
INDEX_DIGEST = "sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3"
PREDECESSOR_IDENTITY_FILE = (
    "_bmad-output/implementation-artifacts/evidence/story-3-14/"
    f"{SOURCE_SHA}/release-identity.json"
)
PREDECESSOR_PACKET_ROOT = (
    "_bmad-output/implementation-artifacts/evidence/story-3-14/" f"{SOURCE_SHA}"
)
MANIFEST_FILE = "tools/release-packages.json"
HANDLER_FILE = "tools/deployed_runtime_parity_handlers/v1.py"
VERIFIER_FILE = "tools/validate-corrected-deployed-runtime-parity.py"
PLATFORMS = ("linux/amd64", "linux/arm64")
REQUIRED_ROLES = ("eventstore-owner", "release-owner", "test-architect")
EXPECTED_IDENTITIES = {
    "eventstore-owner": "github:jpiquot",
    "release-owner": "github:jpiquot",
    "test-architect": "bmad:murat",
}
REQUIRED_LIMITATIONS = (
    "This packet supplies immutable deployed-runtime parity evidence only.",
    "It authorizes no deployment, publication, registry mutation, consumer removal, or predecessor change.",
)
RERUN_TRIGGER = (
    "Rebuild the complete subject and reject all prior receipts after any predecessor, package, OCI, "
    "Production-smoke, inventory, registry, verifier, decision, or receipt-source change."
)
INDEX_MEDIA_TYPE = "application/vnd.oci.image.index.v1+json"
MANIFEST_MEDIA_TYPE = "application/vnd.oci.image.manifest.v1+json"
CONFIG_MEDIA_TYPE = "application/vnd.oci.image.config.v1+json"
SHA256 = re.compile(r"^[0-9a-f]{64}$", re.ASCII)
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$", re.ASCII)
ROLE_MAPPING_LINE = re.compile(r"^- (eventstore-owner|release-owner|test-architect): (\S+)$", re.MULTILINE)
REGISTRY_AUTHORITY_DISCLAIMER_MARKERS = ("authorizes no", "deployment")


class EvidenceError(ValueError):
    """The retained Story 3.15 packet is invalid or non-authorizing."""


def _pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise EvidenceError("evidence contains duplicate JSON fields")
        result[key] = value
    return result


def load_json_bytes(value):
    """Load UTF-8 JSON while rejecting duplicate fields and non-object roots."""
    try:
        result = json.loads(value, object_pairs_hook=_pairs)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise EvidenceError("evidence is not valid UTF-8 JSON") from error
    if not isinstance(result, dict):
        raise EvidenceError("evidence root must be an object")
    return result


def canonical_bytes(value):
    """Return the selected canonical UTF-8 JSON representation."""
    return (
        json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":"), sort_keys=True)
        + "\n"
    ).encode("utf-8")


def canonical_sha256(value):
    """Return the SHA-256 of the selected canonical representation."""
    return hashlib.sha256(canonical_bytes(value)).hexdigest()


def validate_release_manifest(document):
    """Return the exact ordered package IDs from the release manifest."""
    return predecessor_handler.validate_release_manifest(document)


def _exact_object(value, fields, message):
    if not isinstance(value, dict) or set(value) != set(fields):
        raise EvidenceError(message)
    return value


def _exact_list(value, length, message):
    if not isinstance(value, list) or len(value) != length:
        raise EvidenceError(message)
    return value


def _sha256(value, message):
    if not isinstance(value, str) or SHA256.fullmatch(value) is None:
        raise EvidenceError(message)
    return value


def _digest(value, message):
    if not isinstance(value, str) or DIGEST.fullmatch(value) is None:
        raise EvidenceError(message)
    return value


def _positive_integer(value, message):
    if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
        raise EvidenceError(message)
    return value


def _binding(value, *, media_type=None):
    fields = {"file", "sha256", "size"}
    if media_type is not None:
        fields.update(("digest", "media_type"))
    binding = _exact_object(value, fields, "file binding schema is invalid")
    if (
        not isinstance(binding["file"], str)
        or not binding["file"]
        or Path(binding["file"]).is_absolute()
        or ".." in Path(binding["file"]).parts
    ):
        raise EvidenceError("file binding path is unsafe")
    _sha256(binding["sha256"], "file binding SHA-256 is invalid")
    _positive_integer(binding["size"], "file binding size is invalid")
    if media_type is not None and (
        binding["digest"] != f"sha256:{binding['sha256']}" or binding["media_type"] != media_type
    ):
        raise EvidenceError("OCI file binding is invalid")
    return binding


def _parse_time(value, message):
    if not isinstance(value, str) or not value.endswith("Z"):
        raise EvidenceError(message)
    try:
        return datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as error:
        raise EvidenceError(message) from error


def _repository_file(repository_root, relative):
    path = Path(relative)
    if path.is_absolute() or ".." in path.parts:
        raise EvidenceError("repository-relative evidence path is unsafe")
    root = repository_root.resolve()
    resolved = (root / path).resolve()
    if resolved != root and root not in resolved.parents:
        raise EvidenceError("repository-relative evidence path escapes the repository")
    return resolved


def _packet_file(packet_root, relative):
    root = packet_root.resolve()
    path = Path(relative)
    if path.is_absolute() or ".." in path.parts:
        raise EvidenceError("packet-relative evidence path is unsafe")
    resolved = (root / path).resolve()
    if resolved != root and root not in resolved.parents:
        raise EvidenceError("packet-relative evidence path escapes the packet")
    return resolved


def _verify_file(packet_root, binding):
    content = _packet_file(packet_root, binding["file"]).read_bytes()
    if len(content) != binding["size"] or hashlib.sha256(content).hexdigest() != binding["sha256"]:
        raise EvidenceError(f"retained file binding mismatch: {binding['file']}")
    return content


def _expected_subject(document):
    return {
        "authority": {
            "owner_role_registry_sha256": document["owner_role_registry"]["sha256"],
            "publication_authority_sha256": document["predecessor"]["publication_authority_sha256"],
        },
        "created_at": document["subject"]["created_at"],
        "decision": {
            "consumer_removal_authorized": False,
            "deployed_runtime_parity": "available",
            "deployment_authorized": False,
            "grants_mutation_authority": False,
            "publication_authorized": False,
            "selected_deployed_identity": INDEX_DIGEST,
        },
        "evidence": {
            "oci_graph_sha256": canonical_sha256(document["oci"]),
            "package_domains_sha256": canonical_sha256(document["packages"]),
            "production_smokes_sha256": document["production_smokes"]["results"]["sha256"],
            "technical_inventory_sha256": document["technical_inventory"]["sha256"],
        },
        "limitations": list(REQUIRED_LIMITATIONS),
        "lineage": {
            "package_manifest_sha256": document["packages"]["manifest_sha256"],
            "predecessor_sha256": PREDECESSOR_SHA256,
            "repository": REPOSITORY,
            "source_sha": SOURCE_SHA,
            "tag": TAG,
            "version": VERSION,
            "workflow": document["lineage"]["workflow"],
        },
        "required_acceptances": list(REQUIRED_ROLES),
        "rerun_trigger": RERUN_TRIGGER,
        "schema": SUBJECT_SCHEMA,
        "verifier": document["dispatch"],
    }


def validate_identity(document, expected_package_ids, expected_manifest_sha256, repository_root):
    """Validate closed schemas and return canonical closure bytes."""
    _exact_object(
        document,
        (
            "acceptances",
            "consumer_removal_authorized",
            "deployed_runtime_parity",
            "deployment_authorized",
            "dispatch",
            "grants_mutation_authority",
            "lineage",
            "oci",
            "owner_role_registry",
            "packages",
            "predecessor",
            "publication_authorized",
            "repository",
            "rerun_trigger",
            "schema",
            "selected_deployed_identity",
            "story_id",
            "subject",
            "technical_inventory",
            "production_smokes",
        ),
        "closure schema is invalid",
    )
    if document["schema"] != SCHEMA or document["repository"] != REPOSITORY or document["story_id"] != "3.15":
        raise EvidenceError("closure identity is invalid")

    dispatch = _exact_object(document["dispatch"], ("handler", "schema", "verifier", "version"), "dispatch schema is invalid")
    if dispatch["schema"] != SCHEMA or dispatch["version"] != CODEC_VERSION:
        raise EvidenceError("dispatch identity is invalid")
    handler_binding = _binding(dispatch["handler"])
    verifier_binding = _binding(dispatch["verifier"])
    if handler_binding["file"] != HANDLER_FILE or verifier_binding["file"] != VERIFIER_FILE:
        raise EvidenceError("dispatch files are not trusted")
    for binding in (handler_binding, verifier_binding):
        live = _repository_file(repository_root, binding["file"]).read_bytes()
        if len(live) != binding["size"] or hashlib.sha256(live).hexdigest() != binding["sha256"]:
            raise EvidenceError("dispatch identity does not select the trusted live verifier")

    predecessor = _exact_object(
        document["predecessor"],
        ("identity_file", "packet_root", "publication_authority_sha256", "sha256"),
        "predecessor schema is invalid",
    )
    if (
        predecessor["identity_file"] != PREDECESSOR_IDENTITY_FILE
        or predecessor["packet_root"] != PREDECESSOR_PACKET_ROOT
        or predecessor["sha256"] != PREDECESSOR_SHA256
    ):
        raise EvidenceError("predecessor identity is not the frozen Story 3.14 handoff")
    _sha256(predecessor["publication_authority_sha256"], "publication authority digest is invalid")

    lineage = _exact_object(document["lineage"], ("source_sha", "tag", "version", "workflow"), "lineage schema is invalid")
    workflow = _exact_object(
        lineage["workflow"],
        ("repository", "run_attempt", "run_id", "source_sha", "workflow_file", "workflow_sha"),
        "workflow schema is invalid",
    )
    if lineage != {
        "source_sha": SOURCE_SHA,
        "tag": TAG,
        "version": VERSION,
        "workflow": workflow,
    } or workflow != {
        "repository": REPOSITORY,
        "run_attempt": 1,
        "run_id": 32361958618,
        "source_sha": SOURCE_SHA,
        "workflow_file": ".github/workflows/release.yml",
        "workflow_sha": SOURCE_SHA,
    }:
        raise EvidenceError("lineage does not reproduce the corrective release")

    packages = _exact_object(document["packages"], ("count", "items", "manifest_sha256"), "package-domain schema is invalid")
    items = _exact_list(packages["items"], 14, "exactly 14 package-domain mappings are required")
    if packages["count"] != 14 or packages["manifest_sha256"] != expected_manifest_sha256:
        raise EvidenceError("package manifest identity is invalid")
    if [item.get("id") for item in items if isinstance(item, dict)] != list(expected_package_ids):
        raise EvidenceError("package inventory does not match the release manifest order")
    for item in items:
        _exact_object(item, ("github_release_asset", "id", "nuget_org", "repository_commit", "version"), "package mapping schema is invalid")
        if item["version"] != VERSION or item["repository_commit"] != SOURCE_SHA:
            raise EvidenceError("package mapping lineage is invalid")
        _binding(item["github_release_asset"])
        nuget = _exact_object(
            item["nuget_org"],
            ("download_url", "file", "repository_signature_entry_present", "sha256", "size"),
            "NuGet binding schema is invalid",
        )
        _binding({key: nuget[key] for key in ("file", "sha256", "size")})
        expected_url = (
            "https://api.nuget.org/v3-flatcontainer/"
            f"{item['id'].lower()}/{VERSION}/{item['id'].lower()}.{VERSION}.nupkg"
        )
        if nuget["download_url"] != expected_url or nuget["repository_signature_entry_present"] is not True:
            raise EvidenceError("NuGet.org source identity is invalid")

    oci = _exact_object(document["oci"], ("children", "image", "index"), "OCI schema is invalid")
    if oci["image"] != f"registry.hexalith.com/eventstore@{INDEX_DIGEST}":
        raise EvidenceError("OCI image identity is invalid")
    index_binding = _binding(oci["index"], media_type=INDEX_MEDIA_TYPE)
    if index_binding["digest"] != INDEX_DIGEST:
        raise EvidenceError("OCI index digest is invalid")
    children = _exact_list(oci["children"], 2, "OCI graph must contain exactly two children")
    if [child.get("platform") for child in children if isinstance(child, dict)] != list(PLATFORMS):
        raise EvidenceError("OCI platform set or order is invalid")
    for child in children:
        _exact_object(child, ("config", "manifest", "platform"), "OCI child schema is invalid")
        _binding(child["manifest"], media_type=MANIFEST_MEDIA_TYPE)
        _binding(child["config"], media_type=CONFIG_MEDIA_TYPE)

    smokes = _exact_object(document["production_smokes"], ("results",), "Production smoke schema is invalid")
    _binding(smokes["results"])
    _binding(document["owner_role_registry"])
    _binding(document["technical_inventory"])
    subject = _exact_object(document["subject"], ("created_at", "file", "sha256", "size"), "subject binding schema is invalid")
    _parse_time(subject["created_at"], "subject creation timestamp is invalid")
    _binding({key: subject[key] for key in ("file", "sha256", "size")})
    if subject["file"] != "subject.json":
        raise EvidenceError("subject file identity is invalid")

    acceptances = _exact_object(document["acceptances"], ("directory", "receipts"), "acceptance schema is invalid")
    if acceptances["directory"] != f"acceptances/{subject['sha256']}":
        raise EvidenceError("acceptances are not addressed by the unchanged subject")
    receipts = _exact_list(acceptances["receipts"], 3, "exactly three packet-bound receipts are required")
    if [receipt.get("role") for receipt in receipts if isinstance(receipt, dict)] != list(REQUIRED_ROLES):
        raise EvidenceError("acceptance roles are missing, duplicated, or out of order")
    for receipt in receipts:
        _exact_object(receipt, ("file", "role", "sha256", "size"), "receipt binding schema is invalid")
        _binding({key: receipt[key] for key in ("file", "sha256", "size")})
        if receipt["file"] != f"acceptances/{subject['sha256']}/{receipt['role']}.json":
            raise EvidenceError("receipt path does not match its role and subject")

    if (
        document["deployed_runtime_parity"] != "available"
        or document["selected_deployed_identity"] != INDEX_DIGEST
        or document["deployment_authorized"] is not False
        or document["consumer_removal_authorized"] is not False
        or document["publication_authorized"] is not False
        or document["grants_mutation_authority"] is not False
        or document["rerun_trigger"] != RERUN_TRIGGER
    ):
        raise EvidenceError("closure outcome or non-authority flags are invalid")
    return canonical_bytes(document)


def _nuspec_identity(package_path):
    return predecessor_handler._nuspec_identity(package_path)  # noqa: SLF001


def _validate_predecessor(document, expected_package_ids, expected_manifest_sha256, repository_root):
    identity_path = _repository_file(repository_root, PREDECESSOR_IDENTITY_FILE)
    packet_root = _repository_file(repository_root, PREDECESSOR_PACKET_ROOT)
    identity_bytes = identity_path.read_bytes()
    predecessor = predecessor_handler.load_json_bytes(identity_bytes)
    canonical = predecessor_handler.validate_identity(
        predecessor,
        expected_package_ids,
        expected_manifest_sha256=expected_manifest_sha256,
    )
    if identity_bytes != canonical or hashlib.sha256(canonical).hexdigest() != PREDECESSOR_SHA256:
        raise EvidenceError("frozen Story 3.14 identity digest did not reproduce")
    predecessor_handler.validate_packet_files(predecessor, packet_root)
    if (
        predecessor["source_sha"] != SOURCE_SHA
        or predecessor["version"] != VERSION
        or predecessor["tag"] != TAG
        or predecessor["oci"]["index"]["digest"] != INDEX_DIGEST
        or predecessor["authority"]["authority_record_sha256"]
        != document["predecessor"]["publication_authority_sha256"]
        or predecessor["workflow"] != document["lineage"]["workflow"]
    ):
        raise EvidenceError("predecessor lineage does not match the closure")
    return predecessor


def _validate_packages(document, predecessor, packet_root, repository_root):
    predecessor_packages = {item["id"]: item for item in predecessor["packages"]}
    for item in document["packages"]["items"]:
        release_asset = predecessor_packages[item["id"]]
        expected_release_binding = {
            "file": release_asset["file"],
            "sha256": release_asset["sha256"],
            "size": release_asset["size"],
        }
        if item["github_release_asset"] != expected_release_binding:
            raise EvidenceError("GitHub release-asset package domain changed")
        nuget = item["nuget_org"]
        package_path = _packet_file(packet_root, nuget["file"])
        content = _verify_file(packet_root, nuget)
        if nuget["sha256"] == release_asset["sha256"] or content == _repository_file(
            repository_root, f"{PREDECESSOR_PACKET_ROOT}/{release_asset['file']}"
        ).read_bytes():
            raise EvidenceError("NuGet-signed and GitHub release-asset byte domains were conflated")
        try:
            with zipfile.ZipFile(package_path) as archive:
                signature_count = sum(entry.filename == ".signature.p7s" for entry in archive.infolist())
        except (OSError, zipfile.BadZipFile) as error:
            raise EvidenceError("NuGet.org package is not a valid signed archive") from error
        if signature_count != 1 or _nuspec_identity(package_path) != (
            item["id"],
            VERSION,
            "git",
            "https://github.com/Hexalith/Hexalith.EventStore",
            SOURCE_SHA,
        ):
            raise EvidenceError("NuGet.org package signature or nuspec identity is invalid")


def _validate_oci(document, predecessor, packet_root, repository_root):
    index_bytes = _verify_file(packet_root, document["oci"]["index"])
    predecessor_root = _repository_file(repository_root, PREDECESSOR_PACKET_ROOT)
    predecessor_index = predecessor["oci"]["index"]
    if index_bytes != (predecessor_root / predecessor_index["file"]).read_bytes():
        raise EvidenceError("independent raw OCI index does not match the predecessor")
    index = load_json_bytes(index_bytes)
    descriptors = index.get("manifests")
    if (
        index.get("schemaVersion") != 2
        or index.get("mediaType") != INDEX_MEDIA_TYPE
        or not isinstance(descriptors, list)
        or len(descriptors) != 2
    ):
        raise EvidenceError("raw OCI index shape is invalid")
    for child, predecessor_child, descriptor in zip(
        document["oci"]["children"], predecessor["oci"]["children"], descriptors, strict=True
    ):
        if child["platform"] != predecessor_child["platform"]:
            raise EvidenceError("OCI child platform changed from the predecessor")
        expected_os, expected_architecture = child["platform"].split("/", 1)
        platform = descriptor.get("platform") if isinstance(descriptor, dict) else None
        if not isinstance(platform, dict) or platform != {"architecture": expected_architecture, "os": expected_os}:
            raise EvidenceError("OCI index platform descriptor mismatch")
        for name in ("manifest", "config"):
            content = _verify_file(packet_root, child[name])
            predecessor_binding = predecessor_child[name]
            if (
                child[name]["digest"] != predecessor_binding["digest"]
                or content != (predecessor_root / predecessor_binding["file"]).read_bytes()
            ):
                raise EvidenceError(f"independent raw OCI {name} does not match the predecessor")
        manifest = load_json_bytes(_verify_file(packet_root, child["manifest"]))
        config_descriptor = manifest.get("config")
        if not isinstance(config_descriptor, dict) or (
            config_descriptor.get("digest") != child["config"]["digest"]
            or config_descriptor.get("mediaType") != child["config"]["media_type"]
            or config_descriptor.get("size") != child["config"]["size"]
        ):
            raise EvidenceError("OCI config descriptor mismatch")
        config = load_json_bytes(_verify_file(packet_root, child["config"]))
        labels = config.get("config", {}).get("Labels") if isinstance(config.get("config"), dict) else None
        if (
            config.get("os") != expected_os
            or config.get("architecture") != expected_architecture
            or labels != predecessor_child["labels"]
        ):
            raise EvidenceError("OCI config platform or provenance changed")


def _validate_smokes(document, packet_root):
    results_binding = document["production_smokes"]["results"]
    results_bytes = _verify_file(packet_root, results_binding)
    results = load_json_bytes(results_bytes)
    _exact_object(
        results,
        (
            "ended_at",
            "endpoint",
            "environment",
            "exit_code",
            "image_repository",
            "index_digest",
            "platforms",
            "repository",
            "result",
            "schema",
            "started_at",
            "timeout_seconds",
        ),
        "Production smoke result schema is invalid",
    )
    platforms = _exact_list(results["platforms"], 2, "both Production smoke results are required")
    if (
        results["schema"] != SMOKE_SCHEMA
        or results["repository"] != REPOSITORY
        or results["image_repository"] != "registry.hexalith.com/eventstore"
        or results["index_digest"] != INDEX_DIGEST
        or results["environment"] != "Production"
        or results["endpoint"] != "/alive"
        or results["timeout_seconds"] != 180
        or results["exit_code"] != 0
        or results["result"] != "pass"
        or [item.get("platform") for item in platforms if isinstance(item, dict)] != list(PLATFORMS)
    ):
        raise EvidenceError("bounded Production smoke outcome is invalid")
    overall_start = _parse_time(results["started_at"], "Production smoke start is invalid")
    overall_end = _parse_time(results["ended_at"], "Production smoke end is invalid")
    if overall_end < overall_start or (overall_end - overall_start).total_seconds() > 360:
        raise EvidenceError("Production smoke aggregate bound is invalid")
    expected_children = {child["platform"]: child["manifest"]["digest"] for child in document["oci"]["children"]}
    for item in platforms:
        _exact_object(
            item,
            (
                "attempts",
                "child_digest",
                "cleanup",
                "ended_at",
                "exit_code",
                "http_status",
                "log",
                "observed_runtime_platform",
                "outcome",
                "platform",
                "readiness_result",
                "redirect_count",
                "started_at",
            ),
            "Production smoke platform schema is invalid",
        )
        log_binding = _binding(item["log"])
        start = _parse_time(item["started_at"], "Production smoke platform start is invalid")
        end = _parse_time(item["ended_at"], "Production smoke platform end is invalid")
        if (
            item["child_digest"] != expected_children[item["platform"]]
            or item["observed_runtime_platform"] != item["platform"]
            or not isinstance(item["attempts"], int)
            or isinstance(item["attempts"], bool)
            or item["attempts"] <= 0
            or item["http_status"] != 200
            or item["redirect_count"] != 0
            or item["exit_code"] != 0
            or item["readiness_result"] != "pass"
            or item["cleanup"] != "pass"
            or item["outcome"] != "pass"
            or end < start
            or (end - start).total_seconds() > results["timeout_seconds"]
            or start < overall_start
            or end > overall_end
        ):
            raise EvidenceError("Production smoke platform outcome is invalid")
        log_bytes = _verify_file(packet_root, log_binding)
        log = load_json_bytes(log_bytes)
        expected_log = {
            "attempts": item["attempts"],
            "child_digest": item["child_digest"],
            "cleanup": item["cleanup"],
            "ended_at": item["ended_at"],
            "exit_code": item["exit_code"],
            "health_path": "/alive",
            "hosting_environment": "Production",
            "http_status": item["http_status"],
            "observed_runtime_platform": item["observed_runtime_platform"],
            "outcome": item["outcome"],
            "platform": item["platform"],
            "readiness_result": item["readiness_result"],
            "redirect_count": item["redirect_count"],
            "schema": SMOKE_LOG_SCHEMA,
            "started_at": item["started_at"],
        }
        if log != expected_log or log_bytes != canonical_bytes(log):
            raise EvidenceError("Production smoke log does not reproduce its result")


def _validate_registry(document, packet_root):
    """Validate the owner-role registry and its retained GitHub authority source.

    The authority source is reused across stories because it records a role-holder identity fact
    (who currently holds each rostered role), not release-lineage evidence for this specific
    closure -- so a comment dated and scoped to a different story is acceptable here. To keep that
    reuse from silently widening into a release authorization, the comment must itself explicitly
    disclaim deployment authority, and its role-mapping lines must match the required identities
    exactly, with no extra or contradictory role assignment accepted.
    """
    registry_bytes = _verify_file(packet_root, document["owner_role_registry"])
    registry = load_json_bytes(registry_bytes)
    _exact_object(registry, ("authority_source", "created_at", "repository", "roles", "schema"), "owner-role registry schema is invalid")
    source = _binding(registry["authority_source"])
    if (
        registry["schema"] != REGISTRY_SCHEMA
        or registry["repository"] != REPOSITORY
        or registry["roles"] != {role: [EXPECTED_IDENTITIES[role]] for role in REQUIRED_ROLES}
        or registry_bytes != canonical_bytes(registry)
    ):
        raise EvidenceError("owner-role registry is invalid")
    _parse_time(registry["created_at"], "owner-role registry timestamp is invalid")
    source_bytes = _verify_file(packet_root, source)
    source_document = load_json_bytes(source_bytes)
    body = source_document.get("body")
    user = source_document.get("user")
    role_lines = dict(ROLE_MAPPING_LINE.findall(body)) if isinstance(body, str) else {}
    if (
        source_document.get("id") != 5290564372
        or source_document.get("url")
        != "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/5290564372"
        or source_document.get("html_url")
        != "https://github.com/Hexalith/Hexalith.EventStore/issues/324#issuecomment-5290564372"
        or not isinstance(user, dict)
        or user.get("login") != "jpiquot"
        or not isinstance(body, str)
        or role_lines != EXPECTED_IDENTITIES
        or any(marker not in body for marker in REGISTRY_AUTHORITY_DISCLAIMER_MARKERS)
        or registry["created_at"] != source_document.get("created_at")
    ):
        raise EvidenceError("owner-role registry authority source is invalid")


def _validate_inventory(document, packet_root):
    expected = {
        item["nuget_org"]["file"] for item in document["packages"]["items"]
    }
    expected.update((document["oci"]["index"]["file"], document["production_smokes"]["results"]["file"]))
    for child in document["oci"]["children"]:
        expected.update((child["manifest"]["file"], child["config"]["file"]))
    results = load_json_bytes(_verify_file(packet_root, document["production_smokes"]["results"]))
    expected.update(item["log"]["file"] for item in results["platforms"])
    registry = load_json_bytes(_verify_file(packet_root, document["owner_role_registry"]))
    expected.update((document["owner_role_registry"]["file"], registry["authority_source"]["file"]))
    inventory_bytes = _verify_file(packet_root, document["technical_inventory"])
    expected_text = "".join(
        f"{hashlib.sha256(_packet_file(packet_root, relative).read_bytes()).hexdigest()}  {relative}\n"
        for relative in sorted(expected)
    ).encode("utf-8")
    if inventory_bytes != expected_text:
        raise EvidenceError("technical inventory is not closed over the exact retained files")
    closed = expected | {document["technical_inventory"]["file"], document["subject"]["file"], "closure.json"}
    actual = {
        path.relative_to(packet_root).as_posix()
        for path in packet_root.rglob("*")
        if path.is_file() and not path.relative_to(packet_root).as_posix().startswith("acceptances/")
    }
    if actual - closed:
        raise EvidenceError("packet contains files outside the closed technical inventory")


def _validate_receipts(document, packet_root, subject_document):
    """Validate the three subject-bound acceptance receipts and their durable sources.

    Each receipt's ``durable_source`` is cross-checked against a file retained inside this same
    packet, not fetched live from the GitHub API. That proves internal consistency (the receipt and
    its claimed source agree byte-for-byte) but not independence: whoever can author the receipt can
    also author its retained source file. See spec-3-15's Design Notes for the accepted trade-off.
    """
    subject_hash = document["subject"]["sha256"]
    receipt_root = _packet_file(packet_root, f"acceptances/{subject_hash}")
    sources_root = receipt_root / "sources"
    expected_receipts = {f"{role}.json" for role in REQUIRED_ROLES}
    if (
        not receipt_root.is_dir()
        or not sources_root.is_dir()
        or {path.name for path in receipt_root.iterdir() if path.is_file()} != expected_receipts
        or {path.name for path in sources_root.iterdir() if path.is_file()} != expected_receipts
        or any(path.is_dir() and path.name != "sources" for path in receipt_root.iterdir())
        or any(path.is_dir() for path in sources_root.iterdir())
    ):
        raise EvidenceError("acceptance directory is not closed over exactly three receipts and sources")
    for binding in document["acceptances"]["receipts"]:
        receipt_bytes = _verify_file(packet_root, binding)
        receipt = load_json_bytes(receipt_bytes)
        _exact_object(
            receipt,
            (
                "accepted_at",
                "accepted_limitations",
                "accepted_scope",
                "decision",
                "durable_source",
                "reviewer_identity",
                "role",
                "schema",
                "subject_sha256",
            ),
            "acceptance receipt schema is invalid",
        )
        role = receipt["role"]
        source = _exact_object(receipt["durable_source"], ("file", "kind", "sha256", "size"), "receipt source binding is invalid")
        _binding({key: source[key] for key in ("file", "sha256", "size")})
        if (
            receipt_bytes != canonical_bytes(receipt)
            or receipt["schema"] != RECEIPT_SCHEMA
            or role != binding["role"]
            or receipt["reviewer_identity"] != EXPECTED_IDENTITIES[role]
            or receipt["subject_sha256"] != subject_hash
            or receipt["decision"] != "accepted"
            or receipt["accepted_scope"] != f"Story 3.15 corrected deployed-runtime parity for {subject_hash}"
            or receipt["accepted_limitations"] != list(REQUIRED_LIMITATIONS)
            or source["file"] != f"acceptances/{subject_hash}/sources/{role}.json"
        ):
            raise EvidenceError("acceptance receipt does not bind the unchanged subject and role")
        accepted_at = _parse_time(receipt["accepted_at"], "acceptance timestamp is invalid")
        if accepted_at < _parse_time(subject_document["created_at"], "subject timestamp is invalid"):
            raise EvidenceError("acceptance predates the subject")
        if accepted_at > datetime.now().astimezone():
            raise EvidenceError("acceptance timestamp lies in the future")
        source_bytes = _verify_file(packet_root, source)
        source_document = load_json_bytes(source_bytes)
        if source["kind"] == "github-issue-comment":
            try:
                source_body = load_json_bytes(source_document["body"].encode("utf-8"))
            except (KeyError, AttributeError) as error:
                raise EvidenceError("GitHub acceptance source body is invalid") from error
            expected_source_body = {key: value for key, value in receipt.items() if key != "durable_source"}
            receipt_user = source_document.get("user")
            if (
                not isinstance(receipt_user, dict)
                or receipt_user.get("login") != "jpiquot"
                or receipt_user.get("id") != 6775094
                or source_document.get("author_association") not in ("MEMBER", "OWNER", "COLLABORATOR")
                or source_body != expected_source_body
                or not isinstance(source_document.get("id"), int)
                or not str(source_document.get("url", "")).startswith(
                    "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/comments/"
                )
                or not str(source_document.get("html_url", "")).startswith(
                    "https://github.com/Hexalith/Hexalith.EventStore/issues/"
                )
                or not str(source_document.get("issue_url", "")).startswith(
                    "https://api.github.com/repos/Hexalith/Hexalith.EventStore/issues/"
                )
                or source_document.get("created_at") != receipt["accepted_at"]
                or source_document.get("updated_at") != receipt["accepted_at"]
                or source_document.get("performed_via_github_app") is not None
            ):
                raise EvidenceError("GitHub acceptance source is not authenticated to the rostered owner")
        elif source["kind"] == "bmad-test-architect-record":
            expected_source = {
                "acceptance": {key: value for key, value in receipt.items() if key != "durable_source"},
                "repository": REPOSITORY,
                "schema": TEST_ARCHITECT_SOURCE_SCHEMA,
                "test_architect": "bmad:murat",
            }
            if source_document != expected_source or source_bytes != canonical_bytes(source_document):
                raise EvidenceError("Test Architect acceptance source is invalid")
        else:
            raise EvidenceError("acceptance source kind is not allowlisted")


def validate_packet_files(document, packet_root, expected_package_ids, expected_manifest_sha256, repository_root):
    """Recompute all retained edges without executing any packet-supplied code."""
    packet_root = packet_root.resolve()
    predecessor = _validate_predecessor(document, expected_package_ids, expected_manifest_sha256, repository_root)
    _validate_packages(document, predecessor, packet_root, repository_root)
    _validate_oci(document, predecessor, packet_root, repository_root)
    _validate_smokes(document, packet_root)
    _validate_registry(document, packet_root)
    _validate_inventory(document, packet_root)
    subject_bytes = _verify_file(packet_root, document["subject"])
    subject_document = load_json_bytes(subject_bytes)
    if (
        subject_bytes != canonical_bytes(subject_document)
        or subject_document != _expected_subject(document)
        or document["subject"]["sha256"] != hashlib.sha256(subject_bytes).hexdigest()
    ):
        raise EvidenceError("canonical subject does not bind every decision input")
    _validate_receipts(document, packet_root, subject_document)
