#!/usr/bin/env python3
"""Canonical identity codec for a Story 3.14 corrective release packet."""

import hashlib
import json
import re


SCHEMA = "hexalith.eventstore.corrective-release-identity.v1"
SHA40 = re.compile(r"^[0-9a-f]{40}$", re.ASCII)
SHA256 = re.compile(r"^[0-9a-f]{64}$", re.ASCII)
SEMVER = re.compile(r"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", re.ASCII)
PLATFORMS = ("linux/amd64", "linux/arm64")


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
        if SEMVER.fullmatch(version or "") is None or "-" in version:
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


def validate_identity(document, expected_package_ids):
    """Validate and return canonical identity bytes for one complete release."""
    required = {
        "schema",
        "repository",
        "version",
        "tag",
        "source_sha",
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
    workflow = document["workflow"]
    if (
        not isinstance(workflow, dict)
        or workflow.get("source_sha") != source_sha
        or not isinstance(workflow.get("run_id"), int)
        or workflow.get("run_id", 0) <= 0
        or not isinstance(workflow.get("run_attempt"), int)
        or workflow.get("run_attempt", 0) <= 0
    ):
        raise EvidenceError("workflow identity mismatch")
    builds = document["builds"]
    helpers = builds.get("helpers") if isinstance(builds, dict) else None
    if SHA40.fullmatch(builds.get("execution_sha", "")) is None or not isinstance(helpers, dict):
        raise EvidenceError("Builds identity is invalid")
    if not helpers or any(SHA256.fullmatch(value or "") is None for value in helpers.values()):
        raise EvidenceError("Builds helper hashes are invalid")
    authority = document["authority"]
    if (
        not isinstance(authority, dict)
        or authority.get("owner") != "github:jpiquot"
        or authority.get("consumed_once") is not True
        or any(
            SHA256.fullmatch(authority.get(name, "")) is None
            for name in ("record_sha256", "consumption_sha256")
        )
    ):
        raise EvidenceError("publication authority is invalid or unconsumed")
    packages = document["packages"]
    if not isinstance(packages, list) or len(packages) != len(expected_package_ids):
        raise EvidenceError("release package inventory count mismatch")
    actual_ids = [item.get("id") for item in packages if isinstance(item, dict)]
    if actual_ids != expected_package_ids or any(item.get("version") != version for item in packages):
        raise EvidenceError("release package identity mismatch")
    if any(SHA256.fullmatch(item.get("sha256", "")) is None or item.get("size", 0) <= 0 for item in packages):
        raise EvidenceError("release package byte evidence is invalid")
    oci = document["oci"]
    children = oci.get("children") if isinstance(oci, dict) else None
    if (
        oci.get("image") != f"registry.hexalith.com/eventstore:{version}"
        or SHA256.fullmatch(oci.get("index_digest", "").removeprefix("sha256:")) is None
        or not isinstance(children, list)
        or [child.get("platform") for child in children] != list(PLATFORMS)
    ):
        raise EvidenceError("OCI identity or platform set mismatch")
    expected_labels = {
        "org.opencontainers.image.source": "https://github.com/Hexalith/Hexalith.EventStore",
        "org.opencontainers.image.url": (
            f"https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v{version}"
        ),
        "org.opencontainers.image.documentation": (
            f"https://github.com/Hexalith/Hexalith.EventStore/blob/{source_sha}/README.md"
        ),
        "org.opencontainers.image.revision": source_sha,
        "org.opencontainers.image.version": version,
    }
    if any(child.get("labels") != expected_labels for child in children):
        raise EvidenceError("OCI provenance labels differ across child configs")
    smokes = document["smokes"]
    if (
        not isinstance(smokes, list)
        or [smoke.get("platform") for smoke in smokes] != list(PLATFORMS)
        or any(smoke.get("result") != "pass" for smoke in smokes)
    ):
        raise EvidenceError("both immutable child smokes must pass")
    if document["selects_deployed_identity"] is not False or document["grants_mutation_authority"] is not False:
        raise EvidenceError("Story 3.14 evidence must not select deployment or grant mutation authority")
    return canonical_bytes(document)
