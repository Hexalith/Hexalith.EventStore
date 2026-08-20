#!/usr/bin/env python3
"""Canonical identity codec for a Story 3.14 corrective release packet."""

import hashlib
import json
import re
import xml.etree.ElementTree as element_tree
import zipfile
from pathlib import Path


SCHEMA = "hexalith.eventstore.corrective-release-identity.v1"
CODEC_VERSION = 1
SHA40 = re.compile(r"^[0-9a-f]{40}$", re.ASCII)
SHA256 = re.compile(r"^[0-9a-f]{64}$", re.ASCII)
SEMVER = re.compile(r"^\d+\.\d+\.\d+$", re.ASCII)
PLATFORMS = ("linux/amd64", "linux/arm64")
INDEX_MEDIA_TYPE = "application/vnd.oci.image.index.v1+json"
MANIFEST_MEDIA_TYPE = "application/vnd.oci.image.manifest.v1+json"
CONFIG_MEDIA_TYPE = "application/vnd.oci.image.config.v1+json"
REQUIRED_HELPERS = (
    "publish-containers.sh",
    "oci_registry_validator.py",
    "publication_preflight.py",
    "smoke-container-platforms.sh",
    "smoke_container_platforms.py",
)


class EvidenceError(ValueError):
    """Raised when retained corrective-release evidence is not canonical."""


def _pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise EvidenceError(f"duplicate JSON field: {key}")
        result[key] = value
    return result


def load_json_bytes(value):
    """Load JSON bytes while rejecting duplicate object fields."""
    try:
        document = json.loads(value, object_pairs_hook=_pairs)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise EvidenceError("evidence is not valid UTF-8 JSON") from error
    if not isinstance(document, dict):
        raise EvidenceError("evidence root must be an object")
    return document


def canonical_bytes(value):
    """Encode the one canonical JSON representation used for identity hashing."""
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n").encode(
        "utf-8"
    )


def canonical_sha256(value):
    """Hash canonical JSON bytes."""
    return hashlib.sha256(canonical_bytes(value)).hexdigest()


def select_absent_version(versions, occupied):
    """Select the first absent patch version newer than every observed stable version."""
    parsed = []
    for version in versions:
        if SEMVER.fullmatch(version or "") is None:
            raise EvidenceError("candidate floor contains a non-stable semantic version")
        parsed.append(tuple(int(part) for part in version.split(".")))
    if not parsed:
        raise EvidenceError("candidate floor is empty")
    major, minor, patch = max(parsed)
    occupied_set = set(occupied)
    while True:
        patch += 1
        candidate = f"{major}.{minor}.{patch}"
        if candidate not in occupied_set:
            return candidate


def publication_disposition(version, completed_writes, complete):
    """Classify a publication attempt without making a partial identity reusable."""
    if SEMVER.fullmatch(version or "") is None:
        raise EvidenceError("publication version is invalid")
    if not isinstance(completed_writes, list) or any(
        not isinstance(item, str) or not item.strip() for item in completed_writes
    ):
        raise EvidenceError("completed publication writes are invalid")
    if complete:
        return {
            "version": version,
            "result": "complete",
            "immutable_non_authorizing": False,
            "retry_requires_new_version": False,
            "retry_requires_new_authority": False,
        }
    return {
        "version": version,
        "result": "partial" if completed_writes else "no-write",
        "immutable_non_authorizing": bool(completed_writes),
        "retry_requires_new_version": bool(completed_writes),
        "retry_requires_new_authority": bool(completed_writes),
    }


def _exact_object(value, fields, message):
    if not isinstance(value, dict) or set(value) != set(fields):
        raise EvidenceError(message)
    return value


def _positive_integer(value, message):
    if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
        raise EvidenceError(message)
    return value


def _sha256(value, message):
    if not isinstance(value, str) or SHA256.fullmatch(value) is None:
        raise EvidenceError(message)
    return value


def _digest(value, message):
    if not isinstance(value, str) or not value.startswith("sha256:"):
        raise EvidenceError(message)
    _sha256(value.removeprefix("sha256:"), message)
    return value


def _file_binding(value, media_type=None):
    fields = {"file", "size", "sha256"}
    if media_type is not None:
        fields.update({"digest", "media_type"})
    value = _exact_object(value, fields, "retained byte binding field set drift")
    if not isinstance(value["file"], str) or not value["file"]:
        raise EvidenceError("retained byte path is invalid")
    _positive_integer(value["size"], "retained byte size is invalid")
    sha256 = _sha256(value["sha256"], "retained byte SHA-256 is invalid")
    if media_type is not None:
        if value["media_type"] != media_type or _digest(value["digest"], "OCI digest is invalid") != (
            "sha256:" + sha256
        ):
            raise EvidenceError("OCI media type or raw-byte digest mismatch")
    return value


def _expected_labels(source_sha, version):
    repository_url = "https://github.com/Hexalith/Hexalith.EventStore"
    return {
        "org.opencontainers.image.source": repository_url,
        "org.opencontainers.image.url": f"{repository_url}/releases/tag/v{version}",
        "org.opencontainers.image.documentation": f"{repository_url}/blob/{source_sha}/README.md",
        "org.opencontainers.image.revision": source_sha,
        "org.opencontainers.image.version": version,
    }


def validate_identity(document, expected_package_ids, expected_manifest_sha256=None, expected_codec=None):
    """Validate and return canonical identity bytes for one complete release."""
    required = {
        "schema",
        "codec",
        "repository",
        "version",
        "tag",
        "source_sha",
        "manifest",
        "workflow",
        "builds",
        "authority",
        "packages",
        "oci",
        "smokes",
        "selects_deployed_identity",
        "grants_mutation_authority",
    }
    if set(document) != required:
        raise EvidenceError("corrective release identity field set drift")
    if document["schema"] != SCHEMA or document["repository"] != "Hexalith/Hexalith.EventStore":
        raise EvidenceError("corrective release repository or schema mismatch")
    version = document["version"]
    source_sha = document["source_sha"]
    if SEMVER.fullmatch(version or "") is None or document["tag"] != f"v{version}":
        raise EvidenceError("release version/tag mismatch")
    if SHA40.fullmatch(source_sha or "") is None:
        raise EvidenceError("source SHA is invalid")

    codec = _exact_object(
        document["codec"],
        {
            "schema",
            "version",
            "codec_file",
            "codec_sha256",
            "verifier_file",
            "verifier_sha256",
        },
        "codec identity field set drift",
    )
    if codec["schema"] != SCHEMA or codec["version"] != CODEC_VERSION:
        raise EvidenceError("codec schema or version mismatch")
    if codec["codec_file"] != "tools/release_evidence_codec.py" or codec["verifier_file"] != (
        "tools/validate-corrective-release-evidence.py"
    ):
        raise EvidenceError("codec file identity mismatch")
    _sha256(codec["codec_sha256"], "codec content digest is invalid")
    _sha256(codec["verifier_sha256"], "verifier content digest is invalid")
    if expected_codec is not None and codec != expected_codec:
        raise EvidenceError("codec or verifier content does not match the selected implementation")

    manifest = _exact_object(
        document["manifest"],
        {"file", "sha256", "package_count"},
        "manifest identity field set drift",
    )
    if manifest["file"] != "tools/release-packages.json":
        raise EvidenceError("release package manifest path mismatch")
    manifest_sha256 = _sha256(manifest["sha256"], "manifest digest is invalid")
    if expected_manifest_sha256 is not None and manifest_sha256 != expected_manifest_sha256:
        raise EvidenceError("release package manifest bytes changed")
    if manifest["package_count"] != len(expected_package_ids):
        raise EvidenceError("release package manifest count mismatch")

    workflow = _exact_object(
        document["workflow"],
        {"repository", "workflow_file", "workflow_sha", "run_id", "run_attempt", "source_sha"},
        "workflow identity field set drift",
    )
    if (
        workflow["repository"] != document["repository"]
        or workflow["workflow_file"] != ".github/workflows/release.yml"
        or workflow["source_sha"] != source_sha
        or SHA40.fullmatch(workflow["workflow_sha"] or "") is None
    ):
        raise EvidenceError("workflow identity mismatch")
    _positive_integer(workflow["run_id"], "workflow run ID is invalid")
    _positive_integer(workflow["run_attempt"], "workflow run attempt is invalid")

    builds = _exact_object(
        document["builds"],
        {"execution_sha", "helpers"},
        "Builds identity field set drift",
    )
    helpers = builds["helpers"]
    if SHA40.fullmatch(builds["execution_sha"] or "") is None or not isinstance(helpers, dict):
        raise EvidenceError("Builds identity is invalid")
    if set(helpers) != set(REQUIRED_HELPERS) or any(
        SHA256.fullmatch(value or "") is None for value in helpers.values()
    ):
        raise EvidenceError("Builds helper hashes are invalid")

    authority = _exact_object(
        document["authority"],
        {
            "owner",
            "authority_url",
            "issue_url",
            "publication_identity_file",
            "publication_identity_sha256",
            "authority_evidence_file",
            "authority_evidence_sha256",
            "consumption_evidence_file",
            "consumption_evidence_sha256",
            "consumed_once",
        },
        "authority identity field set drift",
    )
    if authority["owner"] != "github:jpiquot" or authority["consumed_once"] is not True:
        raise EvidenceError("publication authority is invalid or unconsumed")
    for name in (
        "publication_identity_sha256",
        "authority_evidence_sha256",
        "consumption_evidence_sha256",
    ):
        _sha256(authority[name], "publication authority digest is invalid")
    for name in (
        "publication_identity_file",
        "authority_evidence_file",
        "consumption_evidence_file",
    ):
        if not isinstance(authority[name], str) or not authority[name]:
            raise EvidenceError("publication authority evidence path is invalid")
    if not re.fullmatch(
        r"https://api\.github\.com/repos/Hexalith/Hexalith\.EventStore/issues/comments/[1-9][0-9]*",
        authority["authority_url"] or "",
    ) or not re.fullmatch(
        r"https://api\.github\.com/repos/Hexalith/Hexalith\.EventStore/issues/[1-9][0-9]*",
        authority["issue_url"] or "",
    ):
        raise EvidenceError("publication authority URL is invalid")

    packages = document["packages"]
    if not isinstance(packages, list) or len(packages) != len(expected_package_ids):
        raise EvidenceError("release package inventory count mismatch")
    actual_ids = []
    for item in packages:
        item = _exact_object(
            item,
            {"id", "version", "file", "size", "sha256", "repository_commit"},
            "release package identity field set drift",
        )
        actual_ids.append(item["id"])
        if item["version"] != version or item["repository_commit"] != source_sha:
            raise EvidenceError("release package version or repository commit mismatch")
        _file_binding({name: item[name] for name in ("file", "size", "sha256")})
    if actual_ids != expected_package_ids:
        raise EvidenceError("release package identity or order mismatch")

    oci = _exact_object(document["oci"], {"image", "index", "children"}, "OCI identity field set drift")
    if oci["image"] != f"registry.hexalith.com/eventstore:{version}":
        raise EvidenceError("OCI image identity mismatch")
    index = _file_binding(oci["index"], INDEX_MEDIA_TYPE)
    children = oci["children"]
    if not isinstance(children, list) or len(children) != 2:
        raise EvidenceError("OCI child count mismatch")
    expected_labels = _expected_labels(source_sha, version)
    for child, platform in zip(children, PLATFORMS, strict=True):
        child = _exact_object(
            child,
            {"platform", "manifest", "config", "labels"},
            "OCI child identity field set drift",
        )
        labels = child["labels"]
        if (
            child["platform"] != platform
            or not isinstance(labels, dict)
            or any(not isinstance(value, str) for value in labels.values())
            or any(labels.get(name) != value for name, value in expected_labels.items())
        ):
            raise EvidenceError("OCI child platform or provenance labels mismatch")
        _file_binding(child["manifest"], MANIFEST_MEDIA_TYPE)
        _file_binding(child["config"], CONFIG_MEDIA_TYPE)

    smokes = document["smokes"]
    if not isinstance(smokes, list) or len(smokes) != 2:
        raise EvidenceError("smoke identity count mismatch")
    child_digests = [child["manifest"]["digest"] for child in children]
    for smoke, platform, child_digest in zip(smokes, PLATFORMS, child_digests, strict=True):
        smoke = _exact_object(
            smoke,
            {
                "platform",
                "child_digest",
                "immutable_image",
                "environment",
                "endpoint",
                "timeout_seconds",
                "result",
                "evidence_file",
                "evidence_sha256",
            },
            "smoke identity field set drift",
        )
        if (
            smoke["platform"] != platform
            or smoke["child_digest"] != child_digest
            or smoke["immutable_image"] != f"registry.hexalith.com/eventstore@{child_digest}"
            or smoke["environment"] != "Development"
            or smoke["endpoint"] != "/alive"
            or smoke["result"] != "pass"
        ):
            raise EvidenceError("bounded Development smoke identity mismatch")
        timeout = _positive_integer(smoke["timeout_seconds"], "smoke timeout is invalid")
        if timeout > 300:
            raise EvidenceError("smoke timeout exceeds the bounded limit")
        if not isinstance(smoke["evidence_file"], str) or not smoke["evidence_file"]:
            raise EvidenceError("smoke evidence path is invalid")
        _sha256(smoke["evidence_sha256"], "smoke evidence digest is invalid")

    if index["digest"] == children[0]["manifest"]["digest"]:
        raise EvidenceError("OCI index and child identities are not distinct")
    if document["selects_deployed_identity"] is not False or document["grants_mutation_authority"] is not False:
        raise EvidenceError("Story 3.14 evidence must not select deployment or grant mutation authority")
    return canonical_bytes(document)


def _safe_packet_file(root, relative):
    if not isinstance(relative, str) or not relative or Path(relative).is_absolute():
        raise EvidenceError("retained evidence path is invalid")
    root = root.resolve()
    path = (root / relative).resolve()
    if not path.is_relative_to(root) or not path.is_file():
        raise EvidenceError("retained evidence path escapes or is unavailable")
    return path


def _verify_bound_file(root, binding):
    path = _safe_packet_file(root, binding["file"])
    content = path.read_bytes()
    if len(content) != binding["size"] or hashlib.sha256(content).hexdigest() != binding["sha256"]:
        raise EvidenceError("retained evidence bytes do not match their binding")
    return content


def _nuspec_identity(package_bytes):
    try:
        with zipfile.ZipFile(Path(package_bytes)) as archive:
            nuspecs = [name for name in archive.namelist() if name.endswith(".nuspec")]
            if len(nuspecs) != 1:
                raise EvidenceError("package does not contain exactly one nuspec")
            root = element_tree.fromstring(archive.read(nuspecs[0]))
    except (OSError, zipfile.BadZipFile, element_tree.ParseError) as error:
        raise EvidenceError("package archive could not be independently inspected") from error
    namespace = {"n": root.tag.partition("}")[0].removeprefix("{")} if root.tag.startswith("{") else {}
    prefix = "n:" if namespace else ""
    metadata = root.find(f"{prefix}metadata", namespace)
    if metadata is None:
        raise EvidenceError("package nuspec metadata is missing")
    package_id = metadata.findtext(f"{prefix}id", namespaces=namespace)
    version = metadata.findtext(f"{prefix}version", namespaces=namespace)
    repository = metadata.find(f"{prefix}repository", namespace)
    commit = repository.get("commit") if repository is not None else None
    return package_id, version, commit


def validate_packet_files(document, packet_root):
    """Re-derive package, OCI graph/config, and smoke claims from retained bytes."""
    packet_root = Path(packet_root)
    for package in document["packages"]:
        path = _safe_packet_file(packet_root, package["file"])
        content = path.read_bytes()
        if len(content) != package["size"] or hashlib.sha256(content).hexdigest() != package["sha256"]:
            raise EvidenceError("retained package bytes do not match their binding")
        if _nuspec_identity(path) != (
            package["id"],
            package["version"],
            package["repository_commit"],
        ):
            raise EvidenceError("package nuspec identity does not match the release identity")

    authority = document["authority"]
    publication_identity_path = _safe_packet_file(
        packet_root,
        authority["publication_identity_file"],
    )
    publication_identity_bytes = publication_identity_path.read_bytes()
    if hashlib.sha256(publication_identity_bytes).hexdigest() != authority[
        "publication_identity_sha256"
    ]:
        raise EvidenceError("publication identity bytes do not match the authority binding")
    publication_identity = load_json_bytes(publication_identity_bytes)
    run = publication_identity.get("run")
    source = publication_identity.get("source")
    builds = publication_identity.get("builds")
    packages = publication_identity.get("packages")
    if (
        publication_identity.get("schema") != "hexalith.release-publication-preflight.v4"
        or publication_identity.get("repository") != document["repository"]
        or publication_identity.get("version") != document["version"]
        or publication_identity.get("source_sha") != document["source_sha"]
        or publication_identity.get("container_repository")
        != "registry.hexalith.com/eventstore"
        or publication_identity.get("container_repositories")
        != ["registry.hexalith.com/eventstore"]
        or publication_identity.get("platforms") != list(PLATFORMS)
        or publication_identity.get("environment") != "production"
        or not isinstance(source, dict)
        or source.get("branch") != "main"
        or source.get("ref") != "refs/heads/main"
        or source.get("live_sha") != document["source_sha"]
        or source.get("ci_workflow") != "ci.yml"
        or not isinstance(source.get("ci_run"), dict)
        or source["ci_run"].get("head_sha") != document["source_sha"]
        or source["ci_run"].get("head_branch") != "main"
        or source["ci_run"].get("event") != "push"
        or source["ci_run"].get("status") != "completed"
        or source["ci_run"].get("conclusion") != "success"
        or not isinstance(run, dict)
        or str(run.get("id")) != str(document["workflow"]["run_id"])
        or str(run.get("attempt")) != str(document["workflow"]["run_attempt"])
        or run.get("workflow_sha") != document["workflow"]["workflow_sha"]
        or run.get("ref") != "refs/heads/main"
        or not isinstance(builds, dict)
        or builds.get("workflow_sha") != document["builds"]["execution_sha"]
        or builds.get("action_sha") != document["builds"]["execution_sha"]
        or builds.get("files") != document["builds"]["helpers"]
        or not isinstance(packages, dict)
        or packages.get("ids") != [item["id"] for item in document["packages"]]
        or packages.get("manifest_sha256") != document["manifest"]["sha256"]
    ):
        raise EvidenceError("publication identity does not bind the corrective release lineage")

    authority_path = _safe_packet_file(packet_root, authority["authority_evidence_file"])
    authority_bytes = authority_path.read_bytes()
    if hashlib.sha256(authority_bytes).hexdigest() != authority["authority_evidence_sha256"]:
        raise EvidenceError("publication authority evidence bytes changed")
    authority_evidence = load_json_bytes(authority_bytes)
    if (
        set(authority_evidence)
        != {
            "url",
            "comment_id",
            "issue_url",
            "owner",
            "created_at",
            "authorized_at",
            "expires_at",
            "rationale",
            "nonce",
            "identity_sha256",
            "record_sha256",
        }
        or not isinstance(authority_evidence.get("comment_id"), int)
        or authority_evidence["comment_id"] <= 0
        or authority_evidence.get("owner") != authority["owner"]
        or authority_evidence.get("url") != authority["authority_url"]
        or authority_evidence.get("issue_url") != authority["issue_url"]
        or authority_evidence.get("identity_sha256") != authority["publication_identity_sha256"]
        or SHA256.fullmatch(authority_evidence.get("record_sha256", "")) is None
    ):
        raise EvidenceError("publication authority evidence does not bind the selected identity")

    consumption_path = _safe_packet_file(packet_root, authority["consumption_evidence_file"])
    consumption_bytes = consumption_path.read_bytes()
    if hashlib.sha256(consumption_bytes).hexdigest() != authority["consumption_evidence_sha256"]:
        raise EvidenceError("publication authority consumption evidence bytes changed")
    consumption = load_json_bytes(consumption_bytes)
    user = consumption.get("user")
    try:
        consumption_body = load_json_bytes(consumption.get("body", "").encode("utf-8"))
    except (AttributeError, EvidenceError) as error:
        raise EvidenceError("publication authority consumption body is invalid") from error
    if (
        not isinstance(user, dict)
        or user.get("login") != "github-actions[bot]"
        or consumption_body.get("authority_comment_id") != authority_evidence.get("comment_id")
        or consumption_body.get("authority_record_sha256") != authority_evidence.get("record_sha256")
        or consumption_body.get("identity_sha256") != authority["publication_identity_sha256"]
        or str(consumption_body.get("run_id")) != str(document["workflow"]["run_id"])
        or str(consumption_body.get("run_attempt")) != str(document["workflow"]["run_attempt"])
        or consumption_body.get("nonce") != authority_evidence.get("nonce")
    ):
        raise EvidenceError("publication authority consumption does not bind the selected identity")

    oci = document["oci"]
    index_bytes = _verify_bound_file(packet_root, oci["index"])
    index = load_json_bytes(index_bytes)
    descriptors = index.get("manifests")
    if (
        index.get("schemaVersion") != 2
        or index.get("mediaType") != INDEX_MEDIA_TYPE
        or not isinstance(descriptors, list)
        or len(descriptors) != 2
    ):
        raise EvidenceError("retained OCI index does not contain exactly two direct children")
    for child, descriptor in zip(oci["children"], descriptors, strict=True):
        platform = descriptor.get("platform") if isinstance(descriptor, dict) else None
        expected_os, expected_architecture = child["platform"].split("/", 1)
        if (
            not isinstance(platform, dict)
            or set(platform) != {"os", "architecture"}
            or platform.get("os") != expected_os
            or platform.get("architecture") != expected_architecture
            or descriptor.get("digest") != child["manifest"]["digest"]
            or descriptor.get("size") != child["manifest"]["size"]
            or descriptor.get("mediaType") != MANIFEST_MEDIA_TYPE
        ):
            raise EvidenceError("retained OCI child descriptor mismatch")
        manifest_bytes = _verify_bound_file(packet_root, child["manifest"])
        manifest = load_json_bytes(manifest_bytes)
        config_descriptor = manifest.get("config")
        if (
            manifest.get("schemaVersion") != 2
            or manifest.get("mediaType") != MANIFEST_MEDIA_TYPE
            or not isinstance(config_descriptor, dict)
            or config_descriptor.get("digest") != child["config"]["digest"]
            or config_descriptor.get("size") != child["config"]["size"]
            or config_descriptor.get("mediaType") != CONFIG_MEDIA_TYPE
        ):
            raise EvidenceError("retained OCI config descriptor mismatch")
        config_bytes = _verify_bound_file(packet_root, child["config"])
        config = load_json_bytes(config_bytes)
        image_config = config.get("config")
        labels = image_config.get("Labels") if isinstance(image_config, dict) else None
        if (
            config.get("os") != expected_os
            or config.get("architecture") != expected_architecture
            or labels != child["labels"]
        ):
            raise EvidenceError("retained OCI config platform or labels mismatch")

    summaries = {}
    for smoke in document["smokes"]:
        path = _safe_packet_file(packet_root, smoke["evidence_file"])
        content = path.read_bytes()
        if hashlib.sha256(content).hexdigest() != smoke["evidence_sha256"]:
            raise EvidenceError("retained smoke evidence digest mismatch")
        summary = summaries.setdefault(str(path), load_json_bytes(content))
        platforms = summary.get("platforms")
        matching = [
            item
            for item in platforms if isinstance(item, dict) and item.get("platform") == smoke["platform"]
        ] if isinstance(platforms, list) else []
        if (
            summary.get("result") != "pass"
            or summary.get("image_repository") != "registry.hexalith.com/eventstore"
            or summary.get("environment") != "Development"
            or summary.get("endpoint") != "/alive"
            or summary.get("timeout_seconds") != smoke["timeout_seconds"]
            or not isinstance(platforms, list)
            or len(platforms) != 2
            or [item.get("platform") for item in platforms if isinstance(item, dict)]
            != list(PLATFORMS)
            or len(matching) != 1
            or matching[0].get("digest") != smoke["child_digest"]
            or matching[0].get("outcome") != "pass"
            or matching[0].get("cleanup") != "pass"
        ):
            raise EvidenceError("retained bounded Development smoke result mismatch")
