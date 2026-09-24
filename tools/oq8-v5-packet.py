#!/usr/bin/env python3
"""Prepare or validate Story 4.15 v5 source-only successor packets.

Drafts are receipt-independent and have no authority. Active validation is
available for a future reviewed packet but requires a v5 selector, lifecycle
binding, three fresh receipts, and a clean checkout. This tool never activates
the selector or creates review receipts.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SELECTOR = ROOT / "_bmad-output/implementation-artifacts/4-15-oq8-platform-closure-successor.json"
LIFECYCLE = ROOT / "_bmad-output/implementation-artifacts/4-15-oq8-platform-lifecycle-state.json"
V5_DIRECTORY = ROOT / "_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v5"
V5_RELATIVE_DIRECTORY = V5_DIRECTORY.relative_to(ROOT).as_posix()
V4_SOURCE_COMMIT = "30b279bd841c671932b51fad6bcf78b147079d21"
V5_SELECTION_REASON = "Retain immutable v1-v4 history and activate the reviewed v5 source-only successor for EventStore-owned trusted publishing."
MAX_JSON_BYTES = 131_072
SHA256_PATTERN = re.compile(r"[0-9a-f]{64}\Z")
COMMIT_PATTERN = re.compile(r"[0-9a-f]{40}\Z")
STAMP_PATTERN = re.compile(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z\Z")
REVIEWERS = {
    "architecture": "Winston (System Architect)",
    "security": "Security Reviewer",
    "test": "Murat (Test Architect)",
}
REVIEW_SCOPES = {
    "architecture": "v1-v4 immutable lineage, v5 source identity, EventStore-owned OIDC job, and source-only handoff",
    "security": "OIDC policy boundary, exact-source and destination gates, v5 fail-closed hashes, and non-authority limits",
    "test": "v1-v4 historical validation, full Contracts regression, OIDC workflow checks, and v5 mutation checks",
}
LIMITATIONS = [
    "Historical v1 through v4 evidence remains bound to archived source; it does not approve changed current source bytes.",
    "The v5 successor approves EventStore current-source OQ8 evidence only after one frozen subject, three fresh reviews, a sealed packet, and an exact selector and lifecycle binding.",
    "NuGet policy activation, protected production approval, public package publication, container publication, and package-only consumer proof are separate release gates.",
    "No release, package, registry, deployment, runtime-pin, consumer-migration, external-repository, Folders final-closure, or final-consumer authority is granted.",
    "Repository review receipts are content-bound declarations; the validator cannot independently authenticate the named reviewer's identity.",
    "The full Contracts suite contains the active OQ8 gate and can pass only after the reviewed v5 packet and selector are assembled; the test receipt binds focused pre-review checks, and full Contracts verification remains a required PR CI gate before merge and release.",
]
NO_AUTHORITY = {
    "currentSourceApproved": False,
    "releaseApproved": False,
    "packageAuthority": False,
    "registryAuthority": False,
}
SOURCE_AUTHORITY = {**NO_AUTHORITY, "currentSourceApproved": True}
FOCUSED_VERIFICATION_COMMAND = "bash scripts/verify-oq8-v5-candidate.sh"
POST_REVIEW_PATHS = {
    SELECTOR.relative_to(ROOT).as_posix(),
    LIFECYCLE.relative_to(ROOT).as_posix(),
    f"{V5_RELATIVE_DIRECTORY}/packet.json",
    f"{V5_RELATIVE_DIRECTORY}/closure-sha256.txt",
}


class PacketError(Exception):
    """A bounded, reviewable v5 packet rejection."""


def require(condition: bool, message: str) -> None:
    if not condition:
        raise PacketError(message)


def exact_fields(value: object, fields: set[str], label: str) -> dict:
    require(isinstance(value, dict) and set(value) == fields, f"{label} field set drift")
    return value


def regular_bytes(path: Path, maximum: int = MAX_JSON_BYTES) -> bytes:
    require(not path.is_symlink() and path.is_file(), f"Required regular file is unavailable: {path.name}")
    require(path.stat().st_size <= maximum, f"File exceeds size limit: {path.name}")
    data = path.read_bytes()
    require(len(data) <= maximum, f"File exceeds size limit: {path.name}")
    return data


def load_json(path: Path) -> dict:
    try:
        value = json.loads(regular_bytes(path))
    except (UnicodeError, json.JSONDecodeError) as error:
        raise PacketError(f"Invalid JSON: {path.name}") from error
    require(isinstance(value, dict), f"JSON object required: {path.name}")
    return value


def canonical(value: object) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def digest(value: object) -> str:
    return hashlib.sha256(canonical(value)).hexdigest()


def file_digest(path: Path) -> str:
    return hashlib.sha256(regular_bytes(path)).hexdigest()


def require_sha(value: object, label: str) -> str:
    require(isinstance(value, str) and SHA256_PATTERN.fullmatch(value) is not None, f"{label} must be SHA-256")
    return value


def timestamp(value: object, label: str) -> datetime:
    require(isinstance(value, str) and STAMP_PATTERN.fullmatch(value) is not None, f"{label} timestamp malformed")
    try:
        parsed = datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)
    except ValueError as error:
        raise PacketError(f"{label} timestamp invalid") from error
    require(parsed <= datetime.now(timezone.utc), f"{label} timestamp is in the future")
    return parsed


def current_candidate(*, for_activation: bool) -> dict:
    command = [sys.executable, str(ROOT / "tools/prepare-oq8-v5-candidate.py")]
    if for_activation:
        command.append("--for-v5-activation")
    result = subprocess.run(command, cwd=ROOT, capture_output=True, timeout=180)
    require(result.returncode == 0, "Archived v1-v4 or current v5 source preparation failed")
    require(len(result.stdout) <= MAX_JSON_BYTES, "Prepared v5 source identity exceeds size limit")
    try:
        candidate = json.loads(result.stdout)
    except (UnicodeError, json.JSONDecodeError) as error:
        raise PacketError("Prepared v5 source identity is not JSON") from error
    require(isinstance(candidate, dict), "Prepared v5 source identity must be an object")
    require(candidate.get("historical", {}).get("validation") == {
        "v1": "passed", "v2": "passed", "v3": "passed", "v4": "passed"
    }, "Historical v1-v4 validation is incomplete")
    require(candidate.get("authority") == NO_AUTHORITY, "Prepared v5 input claims authority")
    return candidate


def subject_inputs(candidate: dict) -> dict:
    historical = candidate["historical"]
    source_identity = {
        "schema": "hexalith.eventstore.story-4-15-successor-source-identity/v5",
        "repository": "Hexalith/Hexalith.EventStore",
        "reviewedCommit": candidate["head"],
        "predecessor": {
            "v4SourceCommit": V4_SOURCE_COMMIT,
            "v4Directory": historical["v4Directory"],
            "v4ManifestSha256": historical["v4ManifestSha256"],
            "v4ReviewSubjectSha256": historical["v4ReviewSubjectSha256"],
        },
        "gateInputs": candidate["currentGateInputs"],
        "releaseSourceSha256": candidate["releaseSourceSha256"],
        "packageIds": candidate["packageIds"],
    }
    return {
        "schema": "hexalith.eventstore.story-4-15-successor-review-subject-inputs/v5",
        "sourceIdentity": source_identity,
        "sourceIdentitySha256": digest(source_identity),
        "historicalValidation": historical["validation"],
        "limitations": LIMITATIONS,
        "requiredReviews": [
            {"role": role, "reviewer": REVIEWERS[role], "scope": REVIEW_SCOPES[role]}
            for role in ("architecture", "security", "test")
        ],
    }


def draft_packet(candidate: dict) -> dict:
    inputs = subject_inputs(candidate)
    return {
        "schema": "hexalith.eventstore.story-4-15-successor-packet-draft/v5",
        "status": "draft-unapproved",
        "sourceHead": candidate["head"],
        "workingTreeDirty": candidate["workingTreeDirty"],
        "subjectInputs": inputs,
        "subjectSha256": digest(inputs),
        "frozenAt": None,
        "reviews": {role: "pending" for role in REVIEWERS},
        "authority": NO_AUTHORITY,
    }


def validate_draft(path: Path) -> None:
    require(ROOT not in path.resolve().parents and path.resolve() != ROOT, "Draft packet must be outside repository")
    draft = load_json(path)
    require(draft == draft_packet(current_candidate(for_activation=False)), "V5 draft packet or source identity drift")


def validate_receipts(packet: dict, subject_sha: str, frozen_at: datetime, source_sha: str) -> tuple[dict[str, str], datetime]:
    reviews = packet.get("reviews")
    require(isinstance(reviews, list) and len(reviews) == 3, "V5 requires three fresh review receipts")
    receipt_hashes: dict[str, str] = {}
    latest = frozen_at
    for receipt in reviews:
        require(isinstance(receipt, dict), "V5 review receipt must be an object")
        role = receipt.get("role")
        require(role in REVIEWERS and role not in receipt_hashes, "V5 review roles must be exact and unique")
        fields = {"schema", "role", "reviewer", "scope", "issuedAt", "decision", "subjectSha256", "findings", "authority"}
        if role == "test":
            fields.add("verification")
        exact_fields(receipt, fields, f"V5 {role} review")
        require(receipt["schema"] == "hexalith.eventstore.story-4-15-successor-review-receipt/v5", f"V5 {role} review schema drift")
        require(receipt["reviewer"] == REVIEWERS[role] and receipt["scope"] == REVIEW_SCOPES[role], f"V5 {role} reviewer or scope drift")
        issued_at = timestamp(receipt["issuedAt"], f"V5 {role} receipt")
        require(issued_at > frozen_at, f"V5 {role} review predates subject freeze")
        latest = max(latest, issued_at)
        require(receipt["decision"] == "approved" and receipt["subjectSha256"] == subject_sha, f"V5 {role} review approval or subject drift")
        findings = receipt["findings"]
        require(isinstance(findings, list) and findings and all(isinstance(item, str) and item.strip() for item in findings), f"V5 {role} findings missing")
        require(receipt["authority"] == NO_AUTHORITY, f"V5 {role} receipt claims external authority")
        if role == "test":
            verification = exact_fields(receipt["verification"], {"scope", "command", "passed", "failed", "skipped", "evidenceSha256", "sourceIdentitySha256"}, "V5 focused verification")
            require(verification["scope"] == "pre-review-focused", "V5 verification scope drift")
            require(verification["command"] == FOCUSED_VERIFICATION_COMMAND, "V5 focused verification command drift")
            require(type(verification["passed"]) is int and verification["passed"] > 0, "V5 focused passed count missing")
            require(type(verification["failed"]) is int and verification["failed"] == 0, "V5 focused failures remain")
            require(type(verification["skipped"]) is int and verification["skipped"] >= 0, "V5 focused skipped count invalid")
            require_sha(verification["evidenceSha256"], "V5 focused evidence")
            require(verification["sourceIdentitySha256"] == source_sha, "V5 focused source identity drift")
        receipt_hashes[role] = digest(receipt)
    require(set(receipt_hashes) == set(REVIEWERS), "V5 review roster incomplete")
    return receipt_hashes, latest


def validate_active_snapshot(
    packet: dict,
    candidate: dict,
    selector: dict,
    lifecycle: dict,
    packet_sha: str,
    manifest: str,
) -> None:
    """Validate the same active contract for live files or an isolated fixture."""
    exact_fields(packet, {"schema", "status", "subjectInputs", "subjectSha256", "frozenAt", "reviews", "handoff", "authority"}, "V5 active packet")
    require(packet["schema"] == "hexalith.eventstore.story-4-15-successor-packet/v5" and packet["status"] == "reviewed-source-only", "V5 active packet schema or status drift")
    require(candidate["workingTreeDirty"] is False, "V5 activation requires a clean committed checkout")
    source_identity = packet.get("subjectInputs", {}).get("sourceIdentity", {})
    reviewed_commit = source_identity.get("reviewedCommit") if isinstance(source_identity, dict) else None
    require(isinstance(reviewed_commit, str) and COMMIT_PATTERN.fullmatch(reviewed_commit) is not None, "V5 reviewed commit malformed")
    ancestor = subprocess.run(
        ["git", "--no-replace-objects", "merge-base", "--is-ancestor", reviewed_commit, candidate["head"]],
        cwd=ROOT, capture_output=True, timeout=30,
    )
    require(ancestor.returncode == 0, "V5 reviewed commit is not an ancestor of current source")
    changed = subprocess.run(
        ["git", "--no-replace-objects", "diff", "--no-renames", "--name-only", "-z", reviewed_commit, candidate["head"]],
        cwd=ROOT, capture_output=True, timeout=30,
    )
    require(changed.returncode == 0, "V5 reviewed commit diff unavailable")
    changed_paths = {path.decode("utf-8") for path in changed.stdout.split(b"\0") if path}
    require(changed_paths <= POST_REVIEW_PATHS, "V5 source changed outside reviewed evidence and selector")
    reviewed_candidate = {**candidate, "head": reviewed_commit}
    expected_inputs = subject_inputs(reviewed_candidate)
    require(packet["subjectInputs"] == expected_inputs, "V5 active packet source or historical identity drift")
    subject_sha = digest(expected_inputs)
    require(packet["subjectSha256"] == subject_sha, "V5 active review subject hash drift")
    frozen_at = timestamp(packet["frozenAt"], "V5 subject freeze")
    receipt_hashes, latest_receipt = validate_receipts(packet, subject_sha, frozen_at, expected_inputs["sourceIdentitySha256"])
    handoff = exact_fields(packet["handoff"], {"schema", "assembledAt", "subjectSha256", "reviewReceiptsSha256", "authority"}, "V5 source-only handoff")
    require(handoff["schema"] == "hexalith.eventstore.story-4-15-successor-source-only-handoff/v5", "V5 handoff schema drift")
    require(timestamp(handoff["assembledAt"], "V5 handoff") > latest_receipt, "V5 handoff predates a review receipt")
    require(handoff["subjectSha256"] == subject_sha and handoff["reviewReceiptsSha256"] == receipt_hashes, "V5 handoff subject or receipt drift")
    require(handoff["authority"] == SOURCE_AUTHORITY and packet["authority"] == SOURCE_AUTHORITY, "V5 handoff or packet authority drift")

    require(manifest == f"{packet_sha}  packet.json\n", "V5 closure manifest drift")
    manifest_sha = hashlib.sha256(manifest.encode("utf-8")).hexdigest()
    exact_fields(selector, {"schema", "selectedOn", "reason", "historical", "successor", "authority"}, "V5 selector")
    require(selector["schema"] == "hexalith.eventstore.story-4-15-successor-selection/v4", "V5 selector schema drift")
    selected_on = selector["selectedOn"]
    require(isinstance(selected_on, str) and re.fullmatch(r"\d{4}-\d{2}-\d{2}", selected_on) is not None, "V5 selector selection date drift")
    try:
        selection_date = datetime.strptime(selected_on, "%Y-%m-%d").date()
    except ValueError as error:
        raise PacketError("V5 selector selection date invalid") from error
    require(selection_date <= datetime.now(timezone.utc).date(), "V5 selector selection date is in the future")
    require(selector["reason"] == V5_SELECTION_REASON, "V5 selector reason drift")
    require(selection_date >= timestamp(handoff["assembledAt"], "V5 handoff").date(), "V5 selector predates reviewed handoff")
    archived = subprocess.run(
        ["git", "--no-replace-objects", "show", f"{V4_SOURCE_COMMIT}:{SELECTOR.relative_to(ROOT).as_posix()}"],
        cwd=ROOT,
        capture_output=True,
        timeout=30,
    )
    require(archived.returncode == 0 and len(archived.stdout) <= MAX_JSON_BYTES, "Archived v4 selector unavailable")
    try:
        archived_selector = json.loads(archived.stdout)
    except (UnicodeError, json.JSONDecodeError) as error:
        raise PacketError("Archived v4 selector is invalid") from error
    require(
        selector["historical"] == {**archived_selector["historical"], "v4Successor": archived_selector["successor"]},
        "V5 selector historical v1-v4 lineage drift",
    )
    require(selector["authority"] == {"eventStorePlatformComplete": True, "handoffMode": "source-only", "releaseApproved": False, "foldersFinalClosure": False, "packageAuthority": False, "registryAuthority": False, "deploymentAuthority": False, "runtimePinAuthority": False, "consumerMigrationAuthority": False, "externalRepositoryAuthority": False, "finalConsumerAuthority": False}, "V5 selector authority drift")
    selected = selector["successor"]
    require(selected == {
        "directory": V5_RELATIVE_DIRECTORY,
        "manifestSha256": manifest_sha,
        "files": {"packet.json": packet_sha},
        "sourceIdentitySha256": expected_inputs["sourceIdentitySha256"],
        "reviewSubjectSha256": subject_sha,
        "handoffSha256": digest(handoff),
    }, "V5 selector packet binding drift")
    require(lifecycle == {
        "schema": "hexalith.eventstore.story-4-15-platform-lifecycle-state/v1",
        "story": "4.15",
        "state": "closed",
        "successorDirectory": V5_RELATIVE_DIRECTORY,
        "successorManifestSha256": manifest_sha,
        "reviewSubjectSha256": subject_sha,
    }, "V5 lifecycle binding drift")


def validate_active(path: Path) -> None:
    require(path == V5_DIRECTORY / "packet.json", "Active v5 packet must use the selected repository path")
    components = (
        ROOT / "_bmad-output",
        ROOT / "_bmad-output/implementation-artifacts",
        ROOT / "_bmad-output/implementation-artifacts/evidence",
        ROOT / "_bmad-output/implementation-artifacts/evidence/story-4-15-successors",
        V5_DIRECTORY,
    )
    for component in components:
        require(not component.is_symlink(), "V5 packet path has a symlink component")
    require(V5_DIRECTORY.is_dir(), "V5 packet directory unavailable")
    actual = {item.relative_to(V5_DIRECTORY).as_posix() for item in V5_DIRECTORY.rglob("*")}
    require(actual == {"packet.json", "closure-sha256.txt"}, "V5 packet file set drift")
    validate_active_snapshot(
        load_json(path),
        current_candidate(for_activation=True),
        load_json(SELECTOR),
        load_json(LIFECYCLE),
        file_digest(path),
        regular_bytes(V5_DIRECTORY / "closure-sha256.txt", 512).decode("utf-8"),
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--prepare-draft", type=Path, metavar="OUTPUT")
    mode.add_argument("--validate-draft", type=Path, metavar="DRAFT")
    mode.add_argument("--validate-active", action="store_true")
    args = parser.parse_args()
    try:
        if args.prepare_draft is not None:
            output = args.prepare_draft.resolve()
            require(output != ROOT and ROOT not in output.parents, "Draft packet output must be outside repository")
            packet = draft_packet(current_candidate(for_activation=False))
            with output.open("x", encoding="utf-8") as stream:
                json.dump(packet, stream, indent=2, sort_keys=True)
                stream.write("\n")
            print(output)
        elif args.validate_draft is not None:
            validate_draft(args.validate_draft)
            print("OQ8 v5 subject-input draft validated; inactive and unapproved.")
        else:
            validate_active(V5_DIRECTORY / "packet.json")
            print("OQ8 v5 reviewed source-only packet validated.")
        return 0
    except (PacketError, OSError, subprocess.SubprocessError, UnicodeError, TypeError, KeyError, ValueError) as error:
        print(f"oq8-v5-packet: {str(error).splitlines()[0][:240]}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
