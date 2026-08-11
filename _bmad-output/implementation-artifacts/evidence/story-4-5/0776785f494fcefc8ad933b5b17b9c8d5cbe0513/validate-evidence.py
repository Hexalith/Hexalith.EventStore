#!/usr/bin/env python3
"""Fail closed on Story 4.5 receipt semantics, redaction, source binding, and hashes."""

from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path


EVIDENCE = (Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parent)
MANIFEST = EVIDENCE / "evidence-sha256.txt"
# filename -> (focused test method, invariant tag that must appear in the failure message).
# The invariant tag binds each receipt to the assertion that actually failed, so a receipt can no
# longer be satisfied by any single-test failure that happens to carry the right filename.
MUTATIONS = {
    "mutation-gate-timing.json": (
        "SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome", "gate-timing"),
    "mutation-intermediate-raw-durability.json": (
        "SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome", "intermediate-raw-durability"),
    "mutation-final-state-consistency.json": (
        "SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome", "final-state-consistency"),
    "mutation-conflict-retry-classification.json": (
        "SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome", "conflict-retry-classification"),
    "mutation-key-addressability.json": (
        "SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome", "key-addressability"),
    "mutation-generic-409-semantics.json": (
        "MetadataKey_StaleEtagUpdate_IsRejected", "generic-409-semantics"),
    "mutation-retained-generic-value.json": (
        "MetadataKey_StaleEtagUpdate_IsRejected", "retained-generic-value"),
}
POSITIVE_RECEIPTS = {
    "race-test-results.json": (1, 1, 0),
    "generic-etag-test-results.json": (1, 1, 0),
    "classifier-parser-test-results.json": (28, 28, 0),
    "live-sidecar-test-results.json": (78, 78, 0),
    "post-mutation-focused-test-results.json": (2, 2, 0),
}


def load_json(name: str) -> object:
    return json.loads((EVIDENCE / name).read_text(encoding="utf-8"))


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def find_workspace() -> Path:
    for candidate in EVIDENCE.parents:
        if (candidate / ".git").exists():
            return candidate
    raise AssertionError("repository root was not found above evidence directory")


def validate_receipts() -> None:
    for name, expected in POSITIVE_RECEIPTS.items():
        summary = load_json(name)["results"]["summary"]
        actual = (summary["tests"], summary["passed"], summary["failed"])
        assert actual == expected, f"{name}: expected {expected}, got {actual}"

    for name, (expected_method, expected_invariant) in MUTATIONS.items():
        results = load_json(name)["results"]
        summary = results["summary"]
        assert (summary["tests"], summary["passed"], summary["failed"]) == (1, 0, 1), name
        tests = results["tests"]
        assert len(tests) == 1 and tests[0]["status"] == "failed", name
        assert tests[0]["name"].endswith(expected_method), name
        failure_text = " ".join(
            str(tests[0].get(field, "")) for field in ("message", "trace", "status")
        )
        assert f"[invariant:{expected_invariant}]" in failure_text, (
            f"{name}: failure does not attribute to invariant {expected_invariant}"
        )

    full_tests = load_json("live-sidecar-test-results.json")["results"]["tests"]
    allocator_return_proof = (
        "Hexalith.EventStore.Server.LiveSidecar.Tests.Benchmarking."
        "BenchmarkDatasetBuilderLiveSidecarTests."
        "SeedAsync_ProductionActorReadsSnapshotTailAndAppendsNextEvent"
    )
    assert any(
        test["name"] == allocator_return_proof and test["status"] == "passed"
        for test in full_tests
    ), "production allocator return/persisted-position proof is absent"


def validate_race() -> None:
    race = load_json("append-durability-race.json")
    assert race["schemaVersion"] == 3
    provider = race["providerProfile"]
    assert provider["daprRuntime"] == "1.18.1"
    assert provider["stateStoreType"] == "state.redis"
    assert provider["redisImage"] == "redis:6"
    component = provider["stateStoreComponentYaml"]
    assert hashlib.sha256(component.encode()).hexdigest() == provider["stateStoreComponentSha256"]
    assert "name: actorStateStore" in component
    assert 'value: "true"' in component
    assert provider["productionAllocatorType"].endswith(".DaprGlobalPositionAllocator")
    assert "no aggregate identity" in provider["allocatorIdentityLimitation"]

    session = race["session"]
    assert session["targetActorId"] == race["aggregate"]["actorId"]
    assert session["targetMessageId"] == race["actorContender"]["messageId"]
    assert session["armCalls"] == 1
    assert session["allocationAttempts"] == 1
    assert session["gateInterceptions"] == 1
    assert session["retryCount"] == 0
    assert session["actorTaskIncompleteAtGate"] is True
    assert session["actorTaskIncompleteAfterRaw"] is True
    assert session["actorTaskIncompleteAfterIntermediate"] is True
    assert session["armedAtUtc"] <= session["firstAllocationEnteredAtUtc"] <= session["releasedAtUtc"]

    assert race["rawContender"]["httpStatus"] == 204
    assert race["rawContender"]["exceptionType"] is None
    assert race["intermediate"]["attemptedRegardlessOfRawStatus"] is True
    assert race["intermediate"]["rawDurabilityProven"] is True
    intermediate_event = race["intermediate"]["event"]
    assert intermediate_event["messageId"] == race["rawContender"]["messageId"]
    assert intermediate_event["correlationId"] == race["rawContender"]["correlationId"]
    assert intermediate_event["causationId"] == race["rawContender"]["messageId"]
    assert race["final"]["finalSequenceWithinBounds"] is True
    assert race["final"]["exactContendersOnly"] is True
    assert race["final"]["rawDurableWriteLost"] is True
    assert race["final"]["actorSurvives"] is True
    assert race["final"]["unexpectedNextEventPresent"] is False
    assert race["actorContender"]["result"]["accepted"] is True
    assert len(race["final"]["events"]) == race["final"]["metadata"]["currentSequence"] == 1
    final_event = race["final"]["events"][0]
    assert final_event["correlationId"] == race["actorContender"]["messageId"]
    assert final_event["causationId"] == race["actorContender"]["messageId"]
    assert final_event["messageId"] == race["actorContender"]["survivingEventMessageId"]
    # The race test now records these two facts instead of requiring them, so the reviewed
    # capture is pinned here: the validator asserts what was observed, while the test stays
    # outcome-neutral for any other provider profile.
    assert race["keyAddressability"]["classification"] == "actor-key-absent-from-generic-namespace"
    assert race["keyAddressability"]["compositeActorRedisReadable"] is True
    assert race["final"]["metadataEtagPresent"] is False

    assert all(value is True for value in race["invariants"].values())
    observation = race["observation"]
    assert observation["classification"] == "same-key-overwrite-raw-durable-write-lost"
    assert observation["isInternallyConsistent"] is True
    assert observation["isInfrastructureFailure"] is False
    assert observation["invalidOperationExceptionSurfaced"] is False
    assert observation["concurrencyConflictSignalled"] is False


def validate_generic_control() -> None:
    control = load_json("generic-etag-control.json")
    assert control["schemaVersion"] == 2
    assert control["interveningUpdate"]["etagAdvanced"] is True
    stale = control["staleReplay"]
    assert stale["status"] == 409
    assert stale["errorCode"] == "ERR_STATE_SAVE"
    assert "etag mismatch" in stale["errorMessage"].lower()
    assert stale["parseError"] is None
    assert control["redisRetainedValue"] == {"writer": "first", "version": 1}
    assert control["retainedValueMatchesExpected"] is True
    assert control["retainedReadExceptionType"] is None


def validate_build_log() -> None:
    build_log = (EVIDENCE / "solution-build.log").read_text(encoding="utf-8")
    assert "Build succeeded." in build_log
    assert "0 Warning(s)" in build_log
    assert "0 Error(s)" in build_log


def validate_environment_profile() -> None:
    environment = (EVIDENCE / "environment.md").read_text(encoding="utf-8")
    race = load_json("append-durability-race.json")
    assert race["providerProfile"]["stateStoreComponentSha256"] in environment
    for digest in (
        "c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf",
        "b42eeb03c4300938226b7a5d7a15db5513e69e1d55570967c290d670c7612df2",
        "bb570eb45c2994eaf32da783cc098b3d51d1095b73ec92919863d73d0a9eaafb",
    ):
        assert digest in environment
    assert "dapr init --runtime-version 1.18.1" in environment


def validate_redaction() -> None:
    absolute_workspace = re.compile(r"/home/[^/\s]+/projects/hexalith/eventstore")
    for path in EVIDENCE.iterdir():
        if not path.is_file() or path.name == Path(__file__).name:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        assert absolute_workspace.search(text) is None, f"absolute workspace in {path.name}"

    for path in EVIDENCE.glob("*.json"):
        document = json.loads(path.read_text(encoding="utf-8"))
        extra = document.get("results", {}).get("extra", {})
        if "computer" in extra:
            assert extra["computer"] == "<redacted-machine>", path.name
        if "user" in extra:
            assert extra["user"] == "<redacted-machine-user>", path.name


def validate_source_binding() -> None:
    workspace = find_workspace()
    source_state = (EVIDENCE / "source-state.md").read_text(encoding="utf-8")
    rows = re.findall(r"^\| `([^`]+)` \| `([0-9a-f]{64})` \|$", source_state, re.MULTILINE)
    assert rows, "source-state.md contains no source hash rows"
    for relative, expected in rows:
        path = workspace / relative
        assert path.is_file(), relative
        assert sha256(path) == expected, relative


def validate_manifest() -> None:
    entries: dict[str, str] = {}
    for line in MANIFEST.read_text(encoding="utf-8").splitlines():
        digest, name = line.split("  ", 1)
        assert name != MANIFEST.name
        assert name not in entries
        entries[name] = digest

    expected_files = {path.name for path in EVIDENCE.iterdir() if path.is_file() and path != MANIFEST}
    assert set(entries) == expected_files, (sorted(set(entries) ^ expected_files))
    for name, expected in entries.items():
        assert sha256(EVIDENCE / name) == expected, name


def main() -> None:
    validate_receipts()
    validate_race()
    validate_generic_control()
    validate_build_log()
    validate_environment_profile()
    validate_redaction()
    validate_source_binding()
    validate_manifest()
    print(f"Story 4.5 evidence valid: {len(MUTATIONS)} mutations, {len(POSITIVE_RECEIPTS)} positive receipts, {len(list(EVIDENCE.iterdir())) - 1} hashed files")


if __name__ == "__main__":
    main()
