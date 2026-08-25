#!/usr/bin/env python3
"""Fail closed on Story 4.5 receipt semantics, redaction, source binding, and hashes."""

from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path


if not __debug__:  # pragma: no cover - guards `python -O`, which strips every assert below.
    raise SystemExit("validate-evidence.py must run with assertions enabled (do not use -O).")

EVIDENCE = (Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parent)
MANIFEST = EVIDENCE / "evidence-sha256.txt"

RACE_METHOD = "SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome"
GENERIC_METHOD = "MetadataKey_StaleEtagUpdate_IsRejected"

# The exact invariant key set each capture must publish. Deleting an invariant from the emitted
# object now fails the packet instead of silently shrinking what the run proves.
RACE_INVARIANTS = frozenset(
    {
        "gate-hold",
        "gate-targeting",
        "intermediate-raw-durability",
        "key-addressability",
        "final-state-classified",
        "conflict-retry-classification",
        "infrastructure-free",
    }
)
GENERIC_INVARIANTS = frozenset(
    {
        "stale-token-proven-stale",
        "generic-409-semantics",
        "retained-generic-value",
    }
)

# receipt file -> (focused test method, armed perturbation, exact set of invariants it must
# falsify). Pinning the exact set -- read from the receipt's own embedded capture -- is what binds
# a receipt to the perturbation that produced it. A receipt can no longer be satisfied by an
# environmental flake, by a differently-armed run, or by an assertion that failed for another
# reason, and a conjunct that is true by program construction cannot yield a passing receipt.
MUTATIONS = {
    "mutation-gate-hold.json": (RACE_METHOD, "gate-hold", {"gate-hold"}),
    "mutation-gate-targeting.json": (RACE_METHOD, "gate-targeting", {"gate-targeting"}),
    "mutation-intermediate-raw-durability.json": (
        RACE_METHOD, "intermediate-raw-durability", {"intermediate-raw-durability"}),
    "mutation-key-addressability.json": (RACE_METHOD, "key-addressability", {"key-addressability"}),
    "mutation-final-state-classified.json": (
        RACE_METHOD, "final-state-classified", {"final-state-classified"}),
    "mutation-conflict-retry-classification.json": (
        RACE_METHOD, "conflict-retry-classification", {"conflict-retry-classification"}),
    "mutation-infrastructure-free.json": (RACE_METHOD, "infrastructure-free", {"infrastructure-free"}),
    "mutation-generic-409-semantics.json": (
        GENERIC_METHOD, "generic-409-semantics", {"generic-409-semantics"}),
    "mutation-retained-generic-value.json": (
        GENERIC_METHOD, "retained-generic-value", {"retained-generic-value"}),
}
POSITIVE_RECEIPTS = {
    "race-test-results.json": (1, 1, 0),
    "generic-etag-test-results.json": (1, 1, 0),
    "classifier-parser-test-results.json": (28, 28, 0),
    "live-sidecar-test-results.json": (78, 78, 0),
    "post-mutation-focused-test-results.json": (2, 2, 0),
    "post-mutation-live-sidecar-test-results.json": (78, 78, 0),
}
# Which committed capture must be byte-for-byte reproduced by which receipt's embedded copy. This
# binds each committed artifact to the exact run that produced it, closing the capture-to-receipt
# traceability gap that timestamps alone could not.
CAPTURE_BINDINGS = {
    "append-durability-race.json": ("post-mutation-focused-test-results.json", RACE_METHOD),
    "generic-etag-control.json": ("post-mutation-focused-test-results.json", GENERIC_METHOD),
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


def embedded_capture(receipt: str, method: str) -> dict:
    """Returns the capture the named test wrote to its xUnit output inside a CTRF receipt."""
    tests = load_json(receipt)["results"]["tests"]
    matches = [test for test in tests if test["name"].endswith(method)]
    assert len(matches) == 1, f"{receipt}: expected exactly one {method} entry, got {len(matches)}"
    output = matches[0].get("extra", {}).get("output")
    assert output, f"{receipt}: {method} carries no captured output"
    return json.loads(output)


def validate_receipts() -> None:
    for name, expected in POSITIVE_RECEIPTS.items():
        summary = load_json(name)["results"]["summary"]
        actual = (summary["tests"], summary["passed"], summary["failed"])
        assert actual == expected, f"{name}: expected {expected}, got {actual}"

    for name, (expected_method, expected_mutation, expected_false) in MUTATIONS.items():
        results = load_json(name)["results"]
        summary = results["summary"]
        assert (summary["tests"], summary["passed"], summary["failed"]) == (1, 0, 1), name
        tests = results["tests"]
        assert len(tests) == 1 and tests[0]["status"] == "failed", name
        assert tests[0]["name"].endswith(expected_method), name
        failure_text = " ".join(
            str(tests[0].get(field, "")) for field in ("message", "trace", "status")
        )
        for invariant in sorted(expected_false):
            assert f"[invariant:{invariant}]" in failure_text, (
                f"{name}: failure does not attribute to invariant {invariant}"
            )

        capture = embedded_capture(name, expected_method)
        assert capture["mutationArmed"] == expected_mutation, (
            f"{name}: capture records perturbation {capture['mutationArmed']!r}, "
            f"expected {expected_mutation!r}"
        )
        invariants = capture["invariants"]
        expected_keys = RACE_INVARIANTS if expected_method == RACE_METHOD else GENERIC_INVARIANTS
        assert set(invariants) == set(expected_keys), f"{name}: invariant key set drifted"
        assert all(isinstance(value, bool) for value in invariants.values()), name
        actual_false = {key for key, value in invariants.items() if value is False}
        assert actual_false == set(expected_false), (
            f"{name}: perturbation falsified {sorted(actual_false)}, "
            f"expected exactly {sorted(expected_false)}"
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


def validate_capture_bindings() -> None:
    for capture_name, (receipt, method) in CAPTURE_BINDINGS.items():
        committed = load_json(capture_name)
        embedded = embedded_capture(receipt, method)
        assert committed == embedded, (
            f"{capture_name} is not the capture recorded by {receipt}:{method}"
        )
        assert committed["mutationArmed"] is None, f"{capture_name} came from a perturbed run"


def validate_race() -> None:
    race = load_json("append-durability-race.json")
    assert race["schemaVersion"] == 4
    provider = race["providerProfile"]
    # Provider attribution is observed, not a source literal: the fixture reads the runtime version
    # from the exact daprd binary it launches and the image from the running container.
    assert provider["daprRuntimeObserved"] == "1.18.1"
    assert provider["stateStoreType"] == "state.redis"
    assert provider["redisImageObserved"] == "docker.io/redis:6"
    assert provider["redisImageDigestObserved"] == (
        "sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf"
    )
    assert "appendonly no" in provider["redisPersistenceObserved"]
    canonical = provider["stateStoreComponentCanonicalYaml"]
    assert "scopes:" not in canonical, "the hashed component must not carry the per-run scopes list"
    assert hashlib.sha256(canonical.encode()).hexdigest() == provider["stateStoreComponentSha256"]
    component = provider["stateStoreComponentYaml"]
    assert canonical == component.split("\nscopes:", 1)[0]
    assert "name: actorStateStore" in canonical
    assert 'value: "true"' in canonical
    assert f"scopes:\n  - {provider['appId']}" in component
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
    # Recorded, explicitly not evidence: the chain is stamped in sequential program order.
    assert session["timestampChainIsEvidence"] is False

    assert race["rawContender"]["httpStatus"] == 204
    assert race["rawContender"]["exceptionType"] is None
    assert race["intermediate"]["attemptedRegardlessOfRawStatus"] is True
    assert race["intermediate"]["rawEventDurabilityProven"] is True
    assert race["intermediate"]["rawDurabilityProven"] is True
    intermediate_event = race["intermediate"]["event"]
    assert intermediate_event["messageId"] == race["rawContender"]["messageId"]
    assert intermediate_event["correlationId"] == race["rawContender"]["correlationId"]
    assert intermediate_event["causationId"] == race["rawContender"]["messageId"]
    assert race["final"]["finalSequenceWithinBounds"] is True
    assert race["final"]["nextEventProbed"] is True
    assert race["final"]["finalStateFullyRead"] is True
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
    # The race test records these facts instead of requiring them, so the reviewed capture is
    # pinned here: the validator asserts what was observed, while the test stays outcome-neutral
    # for any other provider profile.
    key_addressability = race["keyAddressability"]
    assert key_addressability["classification"] == "actor-key-absent-from-generic-namespace"
    assert key_addressability["compositeActorRedisReadable"] is True
    assert key_addressability["genericStateKey"] == race["aggregate"]["metadataKey"]
    assert key_addressability["genericStateKey"] in key_addressability["genericStateProbeUrl"].replace(
        "%3A", ":")
    assert key_addressability["compositeRedisKey"] == race["aggregate"]["compositeMetadataRedisKey"]
    assert key_addressability["compositeRedisMetadata"] is not None
    assert race["final"]["shapeClassification"] == "gapless-1-event-stream"
    assert race["final"]["metadataEtagState"] == "etag-absent"

    invariants = race["invariants"]
    assert set(invariants) == set(RACE_INVARIANTS), "race invariant key set drifted"
    assert all(value is True for value in invariants.values())
    assert race["mutationArmed"] is None
    observation = race["observation"]
    assert observation["classification"] == "same-key-overwrite-raw-durable-write-lost"
    assert observation["classifierSequence"] == race["final"]["metadata"]["currentSequence"]
    assert observation["isInternallyConsistent"] is True
    assert observation["isInfrastructureFailure"] is False
    assert observation["invalidOperationExceptionSurfaced"] is False
    assert observation["concurrencyConflictSignalled"] is False


def validate_generic_control() -> None:
    control = load_json("generic-etag-control.json")
    assert control["schemaVersion"] == 3
    assert control["mutationArmed"] is None
    assert control["interveningUpdate"]["etagAdvanced"] is True
    assert control["original"]["value"]["writer"] == "seed"
    assert control["original"]["parseExceptionType"] is None
    assert control["interveningUpdate"]["value"]["writer"] == "first"
    assert control["interveningUpdate"]["parseExceptionType"] is None
    stale = control["staleReplay"]
    assert stale["suppliedEtag"] == control["original"]["etag"]
    assert stale["suppliedEtagWasStale"] is True
    assert stale["acknowledged"] is False
    assert stale["status"] == 409
    assert stale["errorCode"] == "ERR_STATE_SAVE"
    assert "etag mismatch" in stale["errorMessage"].lower()
    assert stale["parseError"] is None
    assert control["retainedReadKey"] == control["key"]
    assert control["expectedRetainedWriter"] == "first"
    assert control["redisRetainedValue"] == {"writer": "first", "version": 1}
    assert json.loads(control["redisRetainedRawJson"]) == control["redisRetainedValue"]
    assert control["retainedValueMatchesExpected"] is True
    assert control["retainedReadExceptionType"] is None
    invariants = control["invariants"]
    assert set(invariants) == set(GENERIC_INVARIANTS), "generic invariant key set drifted"
    assert all(value is True for value in invariants.values())


# Two projects are referenced only transitively and are not members of `Hexalith.EventStore.slnx`,
# so the solution configuration does not flow to them and they emit Debug output inside a Release
# solution build. Their `.csproj` files live under `src/`, which AC6 freezes byte-for-byte, so this
# story records the condition rather than fixing it (see the deferred-work ledger). Any *other*
# Debug output path -- in particular from a sealed source -- fails the packet.
BUILD_LOG_DEBUG_ALLOWLIST = (
    "Hexalith.EventStore.Gateway",
    "Hexalith.EventStore.TestSubscriber",
)
MINIMUM_COMPILED_PROJECTS = 45


def validate_build_log() -> None:
    build_log = (EVIDENCE / "solution-build.log").read_text(encoding="utf-8")
    assert "Build succeeded." in build_log
    assert "0 Warning(s)" in build_log
    assert "0 Error(s)" in build_log

    # A skipped (up-to-date) compile emits no warnings, so `0 Warning(s)` would be vacuous under an
    # incremental build. The build block runs `--no-incremental`; require the per-project output
    # lines that only a real compile emits.
    outputs = re.findall(r"^  (\S+) -> (\S+)$", build_log, re.MULTILINE)
    assert len(outputs) >= MINIMUM_COMPILED_PROJECTS, (
        f"solution-build.log records only {len(outputs)} compiled projects; "
        f"expected at least {MINIMUM_COMPILED_PROJECTS} (was the build incremental?)"
    )
    debug_outputs = [name for name, path in outputs if "/bin/Debug/" in path]
    unexpected = sorted(set(debug_outputs) - set(BUILD_LOG_DEBUG_ALLOWLIST))
    assert not unexpected, f"a Release build emitted Debug output paths for {unexpected}"

    # The allowlist may only cover projects that genuinely are not solution members.
    solution = (find_workspace() / "Hexalith.EventStore.slnx").read_text(encoding="utf-8")
    for name in BUILD_LOG_DEBUG_ALLOWLIST:
        assert f"{name}.csproj" not in solution, (
            f"{name} is a solution member; its Debug output must not be allowlisted"
        )

    # The assembly the whole packet was captured from must be a Release build.
    live_sidecar = [
        path for name, path in outputs
        if name == "Hexalith.EventStore.Server.LiveSidecar.Tests"
    ]
    assert live_sidecar, "solution-build.log does not record the LiveSidecar test assembly"
    assert all("/bin/Release/" in path for path in live_sidecar), live_sidecar


def validate_environment_profile() -> None:
    environment = (EVIDENCE / "environment.md").read_text(encoding="utf-8")
    race = load_json("append-durability-race.json")
    provider = race["providerProfile"]
    assert provider["stateStoreComponentSha256"] in environment
    assert provider["daprRuntimeObserved"] in environment
    assert provider["redisImageDigestObserved"] in environment
    for digest in (
        "c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf",
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
    bound = {relative for relative, _ in rows}
    # Every source the capture depends on must be present, so omitting a row cannot loosen the
    # binding. Extra rows are allowed; missing ones are not.
    required = {
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/ActorConcurrencyConflictTests.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceLiveSidecarTests.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceClassifierTests.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/DaprStateErrorParserTests.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceControl.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceSession.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveSidecarGlobalPositionAllocator.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceClassifier.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprStateErrorParser.cs",
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Story45MutationSwitch.cs",
    }
    missing = sorted(required - bound)
    assert not missing, f"source-state.md omits evidence-relevant sources: {missing}"
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
    validate_capture_bindings()
    validate_race()
    validate_generic_control()
    validate_build_log()
    validate_environment_profile()
    validate_redaction()
    validate_source_binding()
    validate_manifest()
    print(
        f"Story 4.5 evidence valid: {len(MUTATIONS)} mutations, "
        f"{len(POSITIVE_RECEIPTS)} positive receipts, "
        f"{len(CAPTURE_BINDINGS)} capture-to-receipt bindings, "
        f"{len([path for path in EVIDENCE.iterdir() if path.is_file()]) - 1} hashed files"
    )


if __name__ == "__main__":
    main()
