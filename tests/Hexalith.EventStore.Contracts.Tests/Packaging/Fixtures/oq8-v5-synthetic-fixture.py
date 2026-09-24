#!/usr/bin/env python3
"""In-memory, non-authorizing fixture for the future v5 active validator path.

The receipt objects below are synthetic test data. No packet, selector, lifecycle,
or review receipt is written to the EventStore repository or to a temp file.
"""

from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import subprocess
from datetime import datetime, timedelta, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
SPEC = importlib.util.spec_from_file_location("oq8_v5_packet", ROOT / "tools/oq8-v5-packet.py")
assert SPEC is not None and SPEC.loader is not None
packet_tool = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(packet_tool)


def stamped(value: datetime) -> str:
    return value.strftime("%Y-%m-%dT%H:%M:%SZ")


def must_reject(packet: dict, candidate: dict, selector: dict, lifecycle: dict, packet_sha: str, manifest: str, expected: str) -> None:
    try:
        packet_tool.validate_active_snapshot(packet, candidate, selector, lifecycle, packet_sha, manifest)
    except packet_tool.PacketError as error:
        if expected not in str(error):
            raise AssertionError(f"Wrong fixture rejection: {error}") from error
    else:
        raise AssertionError(f"Fixture mutation was accepted: {expected}")


def main() -> None:
    candidate = packet_tool.current_candidate(for_activation=False)
    candidate["workingTreeDirty"] = False  # Synthetic clean checkout; no live source claim.
    inputs = packet_tool.subject_inputs(candidate)
    subject_sha = packet_tool.digest(inputs)
    now = datetime.now(timezone.utc).replace(microsecond=0)
    frozen_at = now - timedelta(seconds=20)
    reviews = []
    for index, role in enumerate(("architecture", "security", "test")):
        receipt = {
            "schema": "hexalith.eventstore.story-4-15-successor-review-receipt/v5",
            "role": role,
            "reviewer": packet_tool.REVIEWERS[role],
            "scope": packet_tool.REVIEW_SCOPES[role],
            "issuedAt": stamped(now - timedelta(seconds=15 - index)),
            "decision": "approved",  # Synthetic fixture only; never persisted.
            "subjectSha256": subject_sha,
            "findings": ["SYNTHETIC NON-AUTHORIZING TEST FIXTURE"],
            "authority": packet_tool.NO_AUTHORITY,
        }
        if role == "test":
            receipt["verification"] = {
                "scope": "pre-review-focused",
                "command": packet_tool.FOCUSED_VERIFICATION_COMMAND,
                "passed": 1,
                "failed": 0,
                "skipped": 0,
                "evidenceSha256": "0" * 64,
                "sourceIdentitySha256": inputs["sourceIdentitySha256"],
            }
        reviews.append(receipt)
    receipt_hashes = {receipt["role"]: packet_tool.digest(receipt) for receipt in reviews}
    handoff = {
        "schema": "hexalith.eventstore.story-4-15-successor-source-only-handoff/v5",
        "assembledAt": stamped(now - timedelta(seconds=5)),
        "subjectSha256": subject_sha,
        "reviewReceiptsSha256": receipt_hashes,
        "authority": packet_tool.SOURCE_AUTHORITY,
    }
    packet = {
        "schema": "hexalith.eventstore.story-4-15-successor-packet/v5",
        "status": "reviewed-source-only",
        "subjectInputs": inputs,
        "subjectSha256": subject_sha,
        "frozenAt": stamped(frozen_at),
        "reviews": reviews,
        "handoff": handoff,
        "authority": packet_tool.SOURCE_AUTHORITY,
    }
    packet_sha = hashlib.sha256(json.dumps(packet, sort_keys=True).encode()).hexdigest()
    manifest = f"{packet_sha}  packet.json\n"
    manifest_sha = hashlib.sha256(manifest.encode()).hexdigest()
    selector_result = subprocess.run(
        ["git", "--no-replace-objects", "show", f"{packet_tool.V4_SOURCE_COMMIT}:{packet_tool.SELECTOR.relative_to(ROOT).as_posix()}"],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
        timeout=30,
    )
    archived_selector = json.loads(selector_result.stdout)
    selector = {
        "schema": "hexalith.eventstore.story-4-15-successor-selection/v4",
        "selectedOn": now.date().isoformat(),
        "reason": packet_tool.V5_SELECTION_REASON,
        "historical": {**archived_selector["historical"], "v4Successor": archived_selector["successor"]},
        "successor": {
            "directory": packet_tool.V5_RELATIVE_DIRECTORY,
            "manifestSha256": manifest_sha,
            "files": {"packet.json": packet_sha},
            "sourceIdentitySha256": inputs["sourceIdentitySha256"],
            "reviewSubjectSha256": subject_sha,
            "handoffSha256": packet_tool.digest(handoff),
        },
        "authority": archived_selector["authority"],
    }
    lifecycle = {
        "schema": "hexalith.eventstore.story-4-15-platform-lifecycle-state/v1",
        "story": "4.15",
        "state": "closed",
        "successorDirectory": packet_tool.V5_RELATIVE_DIRECTORY,
        "successorManifestSha256": manifest_sha,
        "reviewSubjectSha256": subject_sha,
    }
    packet_tool.validate_active_snapshot(packet, candidate, selector, lifecycle, packet_sha, manifest)

    missing_receipt = copy.deepcopy(packet)
    missing_receipt["reviews"] = missing_receipt["reviews"][:2]
    must_reject(missing_receipt, candidate, selector, lifecycle, packet_sha, manifest, "three fresh review receipts")
    wrong_selector = copy.deepcopy(selector)
    wrong_selector["successor"]["manifestSha256"] = "0" * 64
    must_reject(packet, candidate, wrong_selector, lifecycle, packet_sha, manifest, "selector packet binding drift")
    print("Synthetic v5 active-path fixture passed; non-authorizing and in-memory only.")


if __name__ == "__main__":
    main()
