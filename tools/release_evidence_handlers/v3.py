#!/usr/bin/env python3
"""Canonical identity codec for a Story 3.14 corrective release packet."""

import hashlib
import json
import re
import xml.etree.ElementTree as element_tree
import zipfile
from datetime import datetime, timedelta
from pathlib import Path


SCHEMA = "hexalith.eventstore.corrective-release-identity.v1"
CODEC_VERSION = 3
EXPECTED_PACKET_CODEC_SHA256 = "814502bd962e00dfbac243e2443c3709b46bdbb69e197691443a083e283d32a9"
V3_PUBLICATION_PREFLIGHT_SHA256 = "830af8afb3d2a611d5029133352ecf708511c4e2f4d74aa7e0723420220dfd01"
# The packet retains the exact codec and verifier that produced it, the same way it retains the
# executed Builds helpers. Validation binds those retained bytes rather than whatever happens to
# sit in tools/ today, so a later fix to this file cannot invalidate an already-frozen packet.
# That binding covers the packet's retained copies only -- it says nothing about the bytes of
# this file itself. The dispatcher in validate-corrective-release-evidence.py is what pins this
# module's own SHA-256 (EXPECTED_V3_HANDLER_SHA256) before importing it, so an edit here that is
# not accompanied by updating that constant fails closed rather than silently executing.
RETAINED_CODEC_FILE = "successful/tools/release_evidence_codec.py"
RETAINED_VERIFIER_FILE = "successful/tools/validate-corrective-release-evidence.py"
SHA40 = re.compile(r"^[0-9a-f]{40}$", re.ASCII)
SHA256 = re.compile(r"^[0-9a-f]{64}$", re.ASCII)
SEMVER = re.compile(r"^(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)$", re.ASCII)
NONCE = re.compile(r"^[A-Za-z0-9_-]{20,128}$", re.ASCII)
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
EXPECTED_PACKAGE_COUNT = 14
REPOSITORY = "Hexalith/Hexalith.EventStore"
REPOSITORY_URL = "https://github.com/Hexalith/Hexalith.EventStore"
AUTHORITY_SCHEMA = "hexalith.release-publication-authority.v1"
CONSUMPTION_SCHEMA = "hexalith.release-publication-authority-consumption.v1"
PACKET_MANIFEST_FILE = "packet-sha256.txt"


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


def _load_json_value_bytes(value):
    try:
        return json.loads(value, object_pairs_hook=_pairs)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise EvidenceError("evidence is not valid UTF-8 JSON") from error


def canonical_bytes(value):
    """Encode the one canonical JSON representation used for identity hashing."""
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n").encode(
        "utf-8"
    )


def canonical_sha256(value):
    """Hash canonical JSON bytes."""
    return hashlib.sha256(canonical_bytes(value)).hexdigest()


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
    return {
        "org.opencontainers.image.source": REPOSITORY_URL,
        "org.opencontainers.image.url": f"{REPOSITORY_URL}/releases/tag/v{version}",
        "org.opencontainers.image.documentation": f"{REPOSITORY_URL}/blob/{source_sha}/README.md",
        "org.opencontainers.image.revision": source_sha,
        "org.opencontainers.image.version": version,
    }


def validate_release_manifest(document):
    """Return the exact ordered package IDs from the release manifest."""
    document = _exact_object(document, {"packages"}, "release package manifest field set drift")
    packages = document["packages"]
    if not isinstance(packages, list) or len(packages) != EXPECTED_PACKAGE_COUNT:
        raise EvidenceError("release package manifest must contain exactly 14 packages")
    ids = []
    projects = []
    for item in packages:
        item = _exact_object(item, {"id", "project"}, "release package manifest entry field set drift")
        package_id = item["id"]
        project = item["project"]
        if (
            not isinstance(package_id, str)
            or not package_id
            or package_id != package_id.strip()
            or not isinstance(project, str)
            or not project
            or project != project.strip()
        ):
            raise EvidenceError("release package manifest entry is invalid")
        ids.append(package_id)
        projects.append(project)
    if len(set(ids)) != len(ids) or len({item.casefold() for item in ids}) != len(ids):
        raise EvidenceError("release package manifest IDs must be exactly and case-insensitively unique")
    if len(set(projects)) != len(projects) or len({item.casefold() for item in projects}) != len(projects):
        raise EvidenceError("release package manifest projects must be exactly and case-insensitively unique")
    return ids


def validate_identity(document, expected_package_ids, expected_manifest_sha256=None):
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
        "packet_manifest",
        "selects_deployed_identity",
        "grants_mutation_authority",
    }
    if set(document) != required:
        raise EvidenceError("corrective release identity field set drift")
    if document["schema"] != SCHEMA or document["repository"] != REPOSITORY:
        raise EvidenceError("corrective release repository or schema mismatch")
    version = document["version"]
    source_sha = document["source_sha"]
    if SEMVER.fullmatch(version or "") is None or document["tag"] != f"v{version}":
        raise EvidenceError("release version/tag mismatch")
    if SHA40.fullmatch(source_sha or "") is None:
        raise EvidenceError("source SHA is invalid")

    codec = _exact_object(
        document["codec"],
        {"schema", "version", "codec", "verifier"},
        "codec identity field set drift",
    )
    if codec["schema"] != SCHEMA:
        raise EvidenceError("codec schema mismatch")
    codec_binding = _file_binding(codec["codec"])
    verifier_binding = _file_binding(codec["verifier"])
    if (
        codec["version"] != CODEC_VERSION
        or codec_binding["sha256"] != EXPECTED_PACKET_CODEC_SHA256
        or codec_binding["file"] != RETAINED_CODEC_FILE
        or verifier_binding["file"] != RETAINED_VERIFIER_FILE
    ):
        raise EvidenceError("codec file identity mismatch")

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
        or workflow["workflow_sha"] != source_sha
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
    execution_sha = builds["execution_sha"]
    if not isinstance(execution_sha, str) or SHA40.fullmatch(execution_sha) is None or not isinstance(helpers, dict):
        raise EvidenceError("Builds identity is invalid")
    if set(helpers) != set(REQUIRED_HELPERS):
        raise EvidenceError("Builds helper hashes are invalid")
    for name, binding in helpers.items():
        binding = _file_binding(binding)
        if binding["file"] != f"successful/builds/{execution_sha}/Github/publish-containers/{name}":
            raise EvidenceError("Builds helper path is not tied to the selected execution commit")

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
            "authority_record_file",
            "authority_record_sha256",
            "comments_snapshot_file",
            "comments_snapshot_sha256",
            "role_evidence_file",
            "role_evidence_sha256",
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
        "authority_record_sha256",
        "comments_snapshot_sha256",
        "role_evidence_sha256",
        "consumption_evidence_sha256",
    ):
        _sha256(authority[name], "publication authority digest is invalid")
    for name in (
        "publication_identity_file",
        "authority_evidence_file",
        "authority_record_file",
        "comments_snapshot_file",
        "role_evidence_file",
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
                "log",
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
        _file_binding(smoke["log"])
    if len({(smoke["evidence_file"], smoke["evidence_sha256"]) for smoke in smokes}) != 1:
        raise EvidenceError("smoke identities do not bind one shared two-platform summary")

    packet_manifest = _file_binding(document["packet_manifest"])
    if packet_manifest["file"] != PACKET_MANIFEST_FILE:
        raise EvidenceError("packet checksum manifest path mismatch")

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
    repository_type = repository.get("type") if repository is not None else None
    repository_url = repository.get("url") if repository is not None else None
    commit = repository.get("commit") if repository is not None else None
    return package_id, version, repository_type, repository_url, commit


def _publisher_canonical_bytes(value):
    return (json.dumps(value, indent=2, sort_keys=True) + "\n").encode("utf-8")


def _parse_timestamp(value, message):
    if not isinstance(value, str):
        raise EvidenceError(message)
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as error:
        raise EvidenceError(message) from error
    if parsed.tzinfo is None:
        raise EvidenceError(message)
    return parsed


def _verify_named_hash(root, relative, expected_hash, message):
    content = _safe_packet_file(root, relative).read_bytes()
    if hashlib.sha256(content).hexdigest() != expected_hash:
        raise EvidenceError(message)
    return content


def repository_issue_html_url(issue_url):
    """Derive the GitHub issue HTML URL from one accepted EventStore API URL."""
    prefix = f"https://api.github.com/repos/{REPOSITORY}/issues/"
    if not isinstance(issue_url, str) or not issue_url.startswith(prefix):
        raise EvidenceError("publication authority issue URL is invalid")
    issue_number = issue_url.removeprefix(prefix)
    if not issue_number.isdigit() or int(issue_number) <= 0:
        raise EvidenceError("publication authority issue URL is invalid")
    return f"https://github.com/{REPOSITORY}/issues/{issue_number}"


def _validate_publication_identity(document, publication_identity):
    publication_identity = _exact_object(
        publication_identity,
        {
            "schema",
            "repository",
            "version",
            "source_sha",
            "source",
            "container_repository",
            "container_repositories",
            "platforms",
            "environment",
            "run",
            "builds",
            "packages",
        },
        "publication identity field set drift",
    )
    source = _exact_object(
        publication_identity["source"],
        {"branch", "ref", "live_sha", "ci_workflow", "ci_run"},
        "publication source proof field set drift",
    )
    ci_run = _exact_object(
        source["ci_run"],
        {"id", "head_sha", "head_branch", "event", "status", "conclusion"},
        "publication CI proof field set drift",
    )
    run = _exact_object(
        publication_identity["run"],
        {"actor", "attempt", "event", "id", "number", "ref", "triggering_actor", "workflow_ref", "workflow_sha"},
        "publication run identity field set drift",
    )
    builds = _exact_object(
        publication_identity["builds"],
        {"workflow_sha", "action_sha", "files"},
        "publication Builds identity field set drift",
    )
    packages = _exact_object(
        publication_identity["packages"],
        {"ids", "normalized_ids", "manifest_sha256"},
        "publication package identity field set drift",
    )
    expected_ids = [item["id"] for item in document["packages"]]
    expected_helper_hashes = {
        name: binding["sha256"] for name, binding in document["builds"]["helpers"].items()
    }
    expected_workflow_ref = f"{REPOSITORY}/.github/workflows/release.yml@refs/heads/main"
    if (
        publication_identity["schema"] != "hexalith.release-publication-preflight.v4"
        or publication_identity["repository"] != document["repository"]
        or publication_identity["version"] != document["version"]
        or publication_identity["source_sha"] != document["source_sha"]
        or publication_identity["container_repository"] != "registry.hexalith.com/eventstore"
        or publication_identity["container_repositories"] != ["registry.hexalith.com/eventstore"]
        or publication_identity["platforms"] != list(PLATFORMS)
        or publication_identity["environment"] != "production"
        or source["branch"] != "main"
        or source["ref"] != "refs/heads/main"
        or source["live_sha"] != document["source_sha"]
        or source["ci_workflow"] != "ci.yml"
        or not isinstance(ci_run["id"], int)
        or isinstance(ci_run["id"], bool)
        or ci_run["id"] <= 0
        or ci_run["head_sha"] != document["source_sha"]
        or ci_run["head_branch"] != "main"
        or ci_run["event"] != "push"
        or ci_run["status"] != "completed"
        or ci_run["conclusion"] != "success"
        or run["id"] != str(document["workflow"]["run_id"])
        or run["attempt"] != str(document["workflow"]["run_attempt"])
        or run["workflow_sha"] != document["source_sha"]
        or run["ref"] != "refs/heads/main"
        or run["actor"] != "jpiquot"
        or run["triggering_actor"] != "jpiquot"
        or run["event"] != "workflow_dispatch"
        or not isinstance(run["number"], str)
        or not run["number"].isdigit()
        or int(run["number"]) <= 0
        or run["workflow_ref"] != expected_workflow_ref
        or builds["workflow_sha"] != document["builds"]["execution_sha"]
        or builds["action_sha"] != document["builds"]["execution_sha"]
        or builds["files"] != expected_helper_hashes
        or packages["ids"] != expected_ids
        or packages["normalized_ids"] != [item.lower() for item in expected_ids]
        or packages["manifest_sha256"] != document["manifest"]["sha256"]
    ):
        raise EvidenceError("publication identity does not bind the corrective release lineage")


def _validate_authority(document, packet_root):
    authority = document["authority"]
    publication_identity_bytes = _verify_named_hash(
        packet_root,
        authority["publication_identity_file"],
        authority["publication_identity_sha256"],
        "publication identity bytes do not match the authority binding",
    )
    publication_identity = load_json_bytes(publication_identity_bytes)
    _validate_publication_identity(document, publication_identity)

    raw_bytes = _verify_named_hash(
        packet_root,
        authority["authority_record_file"],
        authority["authority_record_sha256"],
        "raw GitHub authority comment bytes changed",
    )
    raw = load_json_bytes(raw_bytes)
    try:
        body = load_json_bytes(raw["body"].encode("utf-8"))
    except (KeyError, AttributeError, EvidenceError) as error:
        raise EvidenceError("raw GitHub authority body is invalid") from error
    body = _exact_object(
        body,
        {"schema", "role", "identity_sha256", "rationale", "authorized_at", "expires_at", "nonce"},
        "raw GitHub authority body field set drift",
    )
    comment_id = raw.get("id")
    expected_url = f"https://api.github.com/repos/{REPOSITORY}/issues/comments/{comment_id}"
    expected_html_url = repository_issue_html_url(authority["issue_url"])
    user = raw.get("user")
    if (
        not isinstance(comment_id, int)
        or isinstance(comment_id, bool)
        or comment_id <= 0
        or raw.get("url") != expected_url
        or raw.get("html_url") != f"{expected_html_url}#issuecomment-{comment_id}"
        or raw.get("issue_url") != authority["issue_url"]
        or expected_url != authority["authority_url"]
        or not isinstance(user, dict)
        or user.get("login") != "jpiquot"
        or raw.get("author_association") not in {"OWNER", "MEMBER", "COLLABORATOR", "CONTRIBUTOR"}
        or body["schema"] != AUTHORITY_SCHEMA
        or body["role"] != "release-owner"
        or body["identity_sha256"] != authority["publication_identity_sha256"]
        or not isinstance(body["rationale"], str)
        or not body["rationale"].strip()
        or body["rationale"] != body["rationale"].strip()
        or len(body["rationale"]) > 500
        or NONCE.fullmatch(body["nonce"] if isinstance(body["nonce"], str) else "") is None
    ):
        raise EvidenceError("raw GitHub authority comment does not bind the selected identity")
    created = _parse_timestamp(raw.get("created_at"), "raw GitHub authority creation timestamp is invalid")
    updated = _parse_timestamp(raw.get("updated_at"), "raw GitHub authority update timestamp is invalid")
    authorized = _parse_timestamp(body["authorized_at"], "raw GitHub authority timestamp is invalid")
    expires = _parse_timestamp(body["expires_at"], "raw GitHub authority expiry is invalid")
    if (
        created != updated
        or created >= expires
        or authorized >= expires
        or expires - created > timedelta(hours=24)
        or abs(authorized - created) > timedelta(minutes=5)
    ):
        raise EvidenceError("raw GitHub authority timestamps or validity window are invalid")

    record_hash = hashlib.sha256(_publisher_canonical_bytes(raw)).hexdigest()
    summary_bytes = _verify_named_hash(
        packet_root,
        authority["authority_evidence_file"],
        authority["authority_evidence_sha256"],
        "publication authority evidence bytes changed",
    )
    summary = _exact_object(
        load_json_bytes(summary_bytes),
        {
            "url", "comment_id", "issue_url", "owner", "created_at", "authorized_at",
            "expires_at", "rationale", "nonce", "identity_sha256", "record_sha256",
        },
        "publication authority evidence field set drift",
    )
    expected_summary = {
        "url": raw["url"],
        "comment_id": comment_id,
        "issue_url": raw["issue_url"],
        "owner": authority["owner"],
        "created_at": raw["created_at"],
        "authorized_at": body["authorized_at"],
        "expires_at": body["expires_at"],
        "rationale": body["rationale"],
        "nonce": body["nonce"],
        "identity_sha256": authority["publication_identity_sha256"],
        "record_sha256": record_hash,
    }
    if summary != expected_summary:
        raise EvidenceError("publication authority evidence does not re-derive from the raw record")

    role_bytes = _verify_named_hash(
        packet_root,
        authority["role_evidence_file"],
        authority["role_evidence_sha256"],
        "publication authority role evidence bytes changed",
    )
    role = load_json_bytes(role_bytes)
    expected_role_url = (
        f"https://api.github.com/repos/{REPOSITORY}/collaborators/jpiquot/permission"
    )
    if set(role) == {"schema", "repository", "request_url", "response"}:
        if (
            role["schema"] != "hexalith.github-repository-permission-evidence.v1"
            or role["repository"] != REPOSITORY
            or role["request_url"] != expected_role_url
            or not isinstance(role["response"], dict)
        ):
            raise EvidenceError("publication authority repository role proof is invalid")
        role_response = role["response"]
    else:
        # The historical v3 packet retained GitHub's raw response, which contains no
        # request URL. Preserve it only because the exact executed preflight helper is
        # independently pinned and derives that endpoint from the publication identity's
        # already validated EventStore repository. Future evidence uses the envelope above.
        preflight_binding = document["builds"]["helpers"]["publication_preflight.py"]
        if preflight_binding["sha256"] != V3_PUBLICATION_PREFLIGHT_SHA256:
            raise EvidenceError("historical repository role proof has no trusted request binding")
        role_response = role
    role_user = role_response.get("user")
    if (
        not isinstance(role_user, dict)
        or role_user.get("login") != "jpiquot"
        or role_response.get("permission") not in {"admin", "maintain", "write"}
    ):
        raise EvidenceError("publication authority repository role proof is invalid")

    receipt_bytes = _verify_named_hash(
        packet_root,
        authority["consumption_evidence_file"],
        authority["consumption_evidence_sha256"],
        "publication authority consumption evidence bytes changed",
    )
    receipt = load_json_bytes(receipt_bytes)
    try:
        receipt_body = load_json_bytes(receipt["body"].encode("utf-8"))
    except (KeyError, AttributeError, EvidenceError) as error:
        raise EvidenceError("publication authority consumption body is invalid") from error
    receipt_body = _exact_object(
        receipt_body,
        {
            "schema",
            "authority_comment_id",
            "authority_record_sha256",
            "identity_sha256",
            "run_id",
            "run_attempt",
            "nonce",
        },
        "publication authority consumption body field set drift",
    )
    receipt_id = receipt.get("id")
    receipt_user = receipt.get("user")
    receipt_app = receipt.get("performed_via_github_app")
    receipt_created = _parse_timestamp(receipt.get("created_at"), "consumption creation timestamp is invalid")
    receipt_updated = _parse_timestamp(receipt.get("updated_at"), "consumption update timestamp is invalid")
    expected_receipt_body = {
        "schema": CONSUMPTION_SCHEMA,
        "authority_comment_id": comment_id,
        "authority_record_sha256": record_hash,
        "identity_sha256": authority["publication_identity_sha256"],
        "run_id": str(document["workflow"]["run_id"]),
        "run_attempt": str(document["workflow"]["run_attempt"]),
        "nonce": body["nonce"],
    }
    if (
        not isinstance(receipt_id, int)
        or isinstance(receipt_id, bool)
        or receipt_id <= 0
        or receipt.get("url") != f"https://api.github.com/repos/{REPOSITORY}/issues/comments/{receipt_id}"
        or receipt.get("html_url") != f"{expected_html_url}#issuecomment-{receipt_id}"
        or receipt.get("issue_url") != authority["issue_url"]
        or not isinstance(receipt_user, dict)
        or receipt_user.get("login") != "github-actions[bot]"
        or receipt_user.get("type") != "Bot"
        or not isinstance(receipt_app, dict)
        or receipt_app.get("slug") != "github-actions"
        or receipt_created != receipt_updated
        or receipt_created <= max(created, authorized)
        or receipt_created >= expires
        or receipt_body != expected_receipt_body
    ):
        raise EvidenceError("publication authority consumption does not bind the selected identity")

    snapshot_bytes = _verify_named_hash(
        packet_root,
        authority["comments_snapshot_file"],
        authority["comments_snapshot_sha256"],
        "issue-comment snapshot bytes changed",
    )
    snapshot = _load_json_value_bytes(snapshot_bytes)
    if not isinstance(snapshot, list) or not snapshot:
        raise EvidenceError("issue-comment snapshot is invalid")
    ids = [item.get("id") for item in snapshot if isinstance(item, dict)]
    if len(ids) != len(snapshot) or any(not isinstance(item, int) or isinstance(item, bool) for item in ids):
        raise EvidenceError("issue-comment snapshot comment IDs are invalid")
    if ids != sorted(ids) or len(set(ids)) != len(ids):
        raise EvidenceError("issue-comment snapshot must be ordered and unique")
    if any(item.get("issue_url") != authority["issue_url"] for item in snapshot):
        raise EvidenceError("issue-comment snapshot contains an unrelated issue comment")
    authority_matches = []
    receipt_matches = []
    for comment in snapshot:
        try:
            comment_body = load_json_bytes(comment.get("body", "").encode("utf-8"))
        except (AttributeError, EvidenceError):
            continue
        if (
            comment_body.get("schema") == AUTHORITY_SCHEMA
            and comment_body.get("identity_sha256") == authority["publication_identity_sha256"]
        ):
            authority_matches.append(comment)
        if comment_body.get("authority_comment_id") == comment_id:
            receipt_matches.append(comment)
    snapshot_authority = authority_matches[0] if len(authority_matches) == 1 else None
    authority_fields = ("id", "url", "html_url", "issue_url", "body", "created_at", "updated_at")
    if (
        snapshot_authority is None
        or any(snapshot_authority.get(name) != raw.get(name) for name in authority_fields)
        or not isinstance(snapshot_authority.get("user"), dict)
        or snapshot_authority["user"].get("login") != "jpiquot"
        or receipt_matches != [receipt]
    ):
        raise EvidenceError("issue-comment snapshot does not prove exactly one authority and one receipt")


def _validate_packet_manifest(document, packet_root):
    binding = document["packet_manifest"]
    content = _verify_bound_file(packet_root, binding)
    try:
        text = content.decode("utf-8")
    except UnicodeDecodeError as error:
        raise EvidenceError("packet checksum manifest is not UTF-8") from error
    if not text.endswith("\n"):
        raise EvidenceError("packet checksum manifest is not canonical")
    entries = []
    for line in text.splitlines():
        match = re.fullmatch(r"([0-9a-f]{64})  (.+)", line)
        if match is None:
            raise EvidenceError("packet checksum manifest entry is invalid")
        entries.append((match.group(2), match.group(1)))
    paths = [path for path, _ in entries]
    if (
        paths != sorted(paths)
        or len(set(paths)) != len(paths)
        or len({path.casefold() for path in paths}) != len(paths)
    ):
        raise EvidenceError("packet checksum manifest must be ordered and uniquely named")
    excluded = {"release-identity.json", PACKET_MANIFEST_FILE}
    actual_paths = sorted(
        path.relative_to(packet_root).as_posix()
        for path in packet_root.rglob("*")
        if path.is_file() and path.relative_to(packet_root).as_posix() not in excluded
    )
    if paths != actual_paths:
        raise EvidenceError("packet checksum manifest inventory mismatch")
    for relative, expected_hash in entries:
        content = _safe_packet_file(packet_root, relative).read_bytes()
        if hashlib.sha256(content).hexdigest() != expected_hash:
            raise EvidenceError("packet checksum manifest digest mismatch")


def _validate_smoke_log(content, smoke):
    try:
        lines = content.decode("utf-8").splitlines()
    except UnicodeDecodeError as error:
        raise EvidenceError("retained raw smoke log is not UTF-8") from error
    expected = {
        "platform": smoke["platform"],
        "image": smoke["immutable_image"],
        "cleanup": "pass",
        "outcome": "pass",
    }
    for key, value in expected.items():
        matching = [line for line in lines if line.startswith(f"{key}=")]
        if matching != [f"{key}={value}"]:
            raise EvidenceError("retained raw smoke log identity or outcome mismatch")
    attempts = [line.removeprefix("attempts=") for line in lines if line.startswith("attempts=")]
    if len(attempts) != 1 or not attempts[0].isdigit() or int(attempts[0]) <= 0:
        raise EvidenceError("retained raw smoke log attempts are invalid")
    states = [line for line in lines if line.startswith("container_state=")]
    if not states or any(line != "container_state=running|0" for line in states):
        raise EvidenceError("retained raw smoke log container state is invalid")


def validate_packet_files(document, packet_root):
    """Re-derive the full corrective packet from retained, identity-bound bytes."""
    packet_root = Path(packet_root).resolve()
    _validate_packet_manifest(document, packet_root)

    for name, binding in document["builds"]["helpers"].items():
        content = _verify_bound_file(packet_root, binding)
        if hashlib.sha256(content).hexdigest() != binding["sha256"]:
            raise EvidenceError(f"retained Builds helper changed: {name}")

    for name in ("codec", "verifier"):
        binding = document["codec"][name]
        content = _verify_bound_file(packet_root, binding)
        if hashlib.sha256(content).hexdigest() != binding["sha256"]:
            raise EvidenceError(f"retained {name} implementation changed")

    for package in document["packages"]:
        path = _safe_packet_file(packet_root, package["file"])
        content = path.read_bytes()
        if len(content) != package["size"] or hashlib.sha256(content).hexdigest() != package["sha256"]:
            raise EvidenceError("retained package bytes do not match their binding")
        if _nuspec_identity(path) != (
            package["id"],
            package["version"],
            "git",
            REPOSITORY_URL,
            package["repository_commit"],
        ):
            raise EvidenceError("package nuspec repository identity does not match the release identity")

    _validate_authority(document, packet_root)

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
    summary_bindings = {
        (smoke["evidence_file"], smoke["evidence_sha256"])
        for smoke in document["smokes"]
    }
    if len(summary_bindings) != 1:
        raise EvidenceError("retained smokes do not share one two-platform summary")
    for smoke in document["smokes"]:
        path = _safe_packet_file(packet_root, smoke["evidence_file"])
        content = path.read_bytes()
        if hashlib.sha256(content).hexdigest() != smoke["evidence_sha256"]:
            raise EvidenceError("retained smoke evidence digest mismatch")
        summary = summaries.setdefault(str(path), load_json_bytes(content))
        platforms = summary.get("platforms")
        matching = [
            item for item in platforms if isinstance(item, dict) and item.get("platform") == smoke["platform"]
        ] if isinstance(platforms, list) else []
        if (
            summary.get("result") != "pass"
            or summary.get("image_repository") != "registry.hexalith.com/eventstore"
            or summary.get("environment") != "Development"
            or summary.get("endpoint") != "/alive"
            or summary.get("timeout_seconds") != smoke["timeout_seconds"]
            or not isinstance(platforms, list)
            or len(platforms) != 2
            or [item.get("platform") for item in platforms if isinstance(item, dict)] != list(PLATFORMS)
            or len(matching) != 1
            or matching[0].get("digest") != smoke["child_digest"]
            or matching[0].get("outcome") != "pass"
            or matching[0].get("cleanup") != "pass"
        ):
            raise EvidenceError("retained bounded Development smoke result mismatch")
        _validate_smoke_log(_verify_bound_file(packet_root, smoke["log"]), smoke)
