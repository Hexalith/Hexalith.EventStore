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

MUTATION_SWITCH_SOURCE = (
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Story45MutationSwitch.cs"
)

# The exact invariant key set each capture must publish. Deleting an invariant from the emitted
# object fails the packet instead of silently shrinking what the run proves.
RACE_INVARIANTS = frozenset(
    {
        "gate-hold",
        "gate-targeting",
        "intermediate-raw-durability",
        "key-addressability",
        "final-state-sound",
        "conflict-retry-classification",
        "infrastructure-free",
        "state-store-component-identity",
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
# falsify). Pinning the exact set -- read from the receipt's own embedded capture -- binds a
# receipt to the perturbation that produced it. A receipt cannot be satisfied by an environmental
# flake, by a differently-armed run, or by an assertion that failed for another reason.
#
# Two perturbations legitimately falsify more than one invariant, and the sets say so rather than
# hiding it: holding the wrong writer also means the intended writer was not held, and redirecting
# the writer endpoint also destroys the namespace probe and the race classification.
MUTATIONS = {
    "mutation-gate-hold.json": (RACE_METHOD, "gate-hold", {"gate-hold"}),
    "mutation-gate-targeting.json": (
        RACE_METHOD, "gate-targeting", {"gate-hold", "gate-targeting"}),
    "mutation-intermediate-raw-durability.json": (
        RACE_METHOD, "intermediate-raw-durability", {"intermediate-raw-durability"}),
    "mutation-key-addressability.json": (RACE_METHOD, "key-addressability", {"key-addressability"}),
    "mutation-final-state-sound.json": (RACE_METHOD, "final-state-sound", {"final-state-sound"}),
    "mutation-conflict-retry-classification.json": (
        RACE_METHOD, "conflict-retry-classification", {"conflict-retry-classification"}),
    "mutation-infrastructure-free.json": (
        RACE_METHOD, "infrastructure-free", {"infrastructure-free"}),
    "mutation-infrastructure-free-transport.json": (
        RACE_METHOD,
        "infrastructure-free-transport",
        {"key-addressability", "conflict-retry-classification", "infrastructure-free"},
    ),
    "mutation-state-store-component-identity.json": (
        RACE_METHOD, "state-store-component-identity", {"state-store-component-identity"}),
    "mutation-stale-token-proven-stale.json": (
        GENERIC_METHOD, "stale-token-proven-stale", {"stale-token-proven-stale"}),
    "mutation-generic-409-semantics.json": (
        GENERIC_METHOD, "generic-409-semantics", {"generic-409-semantics"}),
    "mutation-retained-generic-value.json": (
        GENERIC_METHOD, "retained-generic-value", {"retained-generic-value"}),
}
POSITIVE_RECEIPTS = {
    "race-test-results.json": (1, 1, 0),
    "generic-etag-test-results.json": (1, 1, 0),
    "classifier-parser-test-results.json": (53, 53, 0),
    "live-sidecar-test-results.json": (105, 105, 0),
    "post-mutation-focused-test-results.json": (2, 2, 0),
    "post-mutation-live-sidecar-test-results.json": (105, 105, 0),
}
# Which committed capture must be reproduced by which receipt's embedded copy. This binds each
# committed artifact to the exact run that produced it.
CAPTURE_BINDINGS = {
    "append-durability-race.json": ("post-mutation-focused-test-results.json", RACE_METHOD),
    "generic-etag-control.json": ("post-mutation-focused-test-results.json", GENERIC_METHOD),
}
# Receipts that ran BEFORE the mutation campaign and therefore embed a superseded capture of the
# same test. They are kept as positive evidence of the focused runs, but they are explicitly not
# the source of the committed captures; the validator holds them to clean-run semantics only.
SUPERSEDED_FOCUSED_RECEIPTS = {
    "race-test-results.json": RACE_METHOD,
    "generic-etag-test-results.json": GENERIC_METHOD,
}

# A real `--no-incremental` build of this solution takes far longer than an up-to-date one
# (measured: ~13-19s versus ~4s). The elapsed floor is a heuristic backstop; the load-bearing check
# is the command line recorded in the log header.
BUILD_ELAPSED_FLOOR_SECONDS = 6.0
MINIMUM_COMPILED_PROJECTS = 45
# These two projects are referenced only transitively and are not members of
# `Hexalith.EventStore.slnx`, so the solution configuration does not flow to them and they emit
# Debug output inside a Release solution build. Their `.csproj` files live under `src/`, which AC6
# freezes byte-for-byte, so this story records the condition rather than fixing it. Any *other*
# Debug output path -- in particular from a sealed source -- fails the packet.
BUILD_LOG_DEBUG_ALLOWLIST = (
    "Hexalith.EventStore.Gateway",
    "Hexalith.EventStore.TestSubscriber",
)

REQUIRED_SOURCE_ROWS = {
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/ActorConcurrencyConflictTests.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceLiveSidecarTests.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceClassifierTests.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityFinalShapeClassifierTests.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/DaprStateErrorParserTests.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/StateStoreComponentCanonicalizationTests.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/Story45MutationSwitchTests.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceControl.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceSession.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveSidecarGlobalPositionAllocator.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceClassifier.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityFinalShapeClassifier.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprStateErrorParser.cs",
    MUTATION_SWITCH_SOURCE,
}


class EvidenceError(AssertionError):
    """A named validation failure, as opposed to an unhandled crash."""


def require(condition: object, message: str) -> None:
    """Fails with a named message instead of letting a bad shape raise a bare traceback."""
    if not condition:
        raise EvidenceError(message)


def load_json(name: str) -> object:
    path = EVIDENCE / name
    require(path.is_file(), f"{name}: expected evidence file is missing")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise EvidenceError(f"{name}: file is not valid JSON ({error})") from error


def get(document: object, path: str, name: str) -> object:
    """Walks a dotted path, failing named at the first missing or non-mapping step."""
    current = document
    for part in path.split("."):
        require(isinstance(current, dict), f"{name}: '{path}' traverses a non-object at '{part}'")
        require(part in current, f"{name}: '{path}' is missing '{part}'")
        current = current[part]
    return current


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def find_workspace() -> Path:
    for candidate in EVIDENCE.parents:
        if (candidate / ".git").exists():
            return candidate
    raise EvidenceError("repository root was not found above evidence directory")


def embedded_capture(receipt: str, method: str) -> dict:
    """Returns the capture the named test wrote to its xUnit output inside a CTRF receipt."""
    tests = get(load_json(receipt), "results.tests", receipt)
    require(isinstance(tests, list), f"{receipt}: results.tests is not a list")
    matches = [
        test for test in tests
        if isinstance(test, dict) and str(test.get("name", "")).endswith(method)
    ]
    require(
        len(matches) == 1,
        f"{receipt}: expected exactly one {method} entry, got {len(matches)}",
    )
    extra = matches[0].get("extra")
    require(isinstance(extra, dict), f"{receipt}: {method} has no extra object")
    output = extra.get("output")
    require(
        isinstance(output, str) and output.strip(),
        f"{receipt}: {method} carries no captured output",
    )
    try:
        capture = json.loads(output)
    except json.JSONDecodeError as error:
        raise EvidenceError(
            f"{receipt}: {method} output is not the capture JSON alone ({error}). "
            "The test must write nothing else to its xUnit output."
        ) from error
    require(isinstance(capture, dict), f"{receipt}: {method} output is not a JSON object")
    return capture


def receipt_summary(name: str) -> dict:
    summary = get(load_json(name), "results.summary", name)
    require(isinstance(summary, dict), f"{name}: results.summary is not an object")
    return summary


def validate_mutation_registry() -> None:
    """Binds the C# perturbation set and the invariant sets to this map, in both directions."""
    workspace = find_workspace()
    source = workspace / MUTATION_SWITCH_SOURCE
    require(source.is_file(), f"{MUTATION_SWITCH_SOURCE} is missing")
    text = source.read_text(encoding="utf-8")
    block = re.search(r"KnownMutations\s*=\s*new HashSet<string>\([^)]*\)\s*\{(.*?)\};", text, re.S)
    require(block is not None, "Story45MutationSwitch.KnownMutations could not be parsed")
    declared = set(re.findall(r'"([a-z0-9-]+)"', block.group(1)))
    armed = {mutation for _, mutation, _ in MUTATIONS.values()}
    require(
        declared == armed,
        "Story45MutationSwitch.KnownMutations and the validator's MUTATIONS map disagree: "
        f"only in C# {sorted(declared - armed)}, only in the validator {sorted(armed - declared)}",
    )

    # Every named invariant must be falsified by at least one pinned perturbation, so an invariant
    # can never be added without an accompanying receipt that proves it has teeth.
    covered: set[str] = set()
    for _, _, expected_false in MUTATIONS.values():
        covered |= set(expected_false)
    uncovered = sorted((set(RACE_INVARIANTS) | set(GENERIC_INVARIANTS)) - covered)
    require(not uncovered, f"invariants with no perturbation pinned to falsify them: {uncovered}")

    unknown = sorted(covered - (set(RACE_INVARIANTS) | set(GENERIC_INVARIANTS)))
    require(not unknown, f"perturbations pinned to unknown invariants: {unknown}")


def validate_receipts() -> None:
    for name, expected in POSITIVE_RECEIPTS.items():
        summary = receipt_summary(name)
        actual = (summary.get("tests"), summary.get("passed"), summary.get("failed"))
        require(actual == expected, f"{name}: expected {expected}, got {actual}")

    mutation_stops: list[int] = []
    for name, (expected_method, expected_mutation, expected_false) in MUTATIONS.items():
        results = get(load_json(name), "results", name)
        summary = results.get("summary", {})
        require(
            (summary.get("tests"), summary.get("passed"), summary.get("failed")) == (1, 0, 1),
            f"{name}: a mutation receipt must be exactly 1 test, 0 passed, 1 failed",
        )
        mutation_stops.append(int(summary.get("stop", 0)))
        tests = results.get("tests", [])
        require(
            len(tests) == 1 and tests[0].get("status") == "failed",
            f"{name}: expected one failed test entry",
        )
        require(
            str(tests[0].get("name", "")).endswith(expected_method),
            f"{name}: failing test is not {expected_method}",
        )
        failure_text = " ".join(
            str(tests[0].get(field, "")) for field in ("message", "trace", "status")
        )
        for invariant in sorted(expected_false):
            require(
                f"[invariant:{invariant}]" in failure_text,
                f"{name}: failure does not attribute to invariant {invariant}",
            )

        capture = embedded_capture(name, expected_method)
        require(
            capture.get("mutationArmed") == expected_mutation,
            f"{name}: capture records perturbation {capture.get('mutationArmed')!r}, "
            f"expected {expected_mutation!r}",
        )
        invariants = capture.get("invariants")
        require(isinstance(invariants, dict), f"{name}: capture has no invariants object")
        expected_keys = RACE_INVARIANTS if expected_method == RACE_METHOD else GENERIC_INVARIANTS
        require(set(invariants) == set(expected_keys), f"{name}: invariant key set drifted")
        require(
            all(isinstance(value, bool) for value in invariants.values()),
            f"{name}: invariant values must be booleans",
        )
        actual_false = {key for key, value in invariants.items() if value is False}
        require(
            actual_false == set(expected_false),
            f"{name}: perturbation falsified {sorted(actual_false)}, "
            f"expected exactly {sorted(expected_false)}",
        )

    # The post-mutation full-suite receipt exists to show the tree is green *after* the campaign.
    # Without this check a pre-mutation copy would satisfy it.
    post_summary = receipt_summary("post-mutation-live-sidecar-test-results.json")
    latest_mutation_stop = max(mutation_stops)
    require(
        int(post_summary.get("start", 0)) >= latest_mutation_stop,
        "post-mutation-live-sidecar-test-results.json started before the mutation campaign "
        f"finished ({post_summary.get('start')} < {latest_mutation_stop})",
    )
    post_focused = receipt_summary("post-mutation-focused-test-results.json")
    require(
        int(post_focused.get("start", 0)) >= latest_mutation_stop,
        "post-mutation-focused-test-results.json started before the mutation campaign finished",
    )

    full_tests = get(load_json("live-sidecar-test-results.json"), "results.tests", "live-sidecar")
    allocator_return_proof = (
        "Hexalith.EventStore.Server.LiveSidecar.Tests.Benchmarking."
        "BenchmarkDatasetBuilderLiveSidecarTests."
        "SeedAsync_ProductionActorReadsSnapshotTailAndAppendsNextEvent"
    )
    require(
        any(
            test.get("name") == allocator_return_proof and test.get("status") == "passed"
            for test in full_tests
        ),
        "production allocator return/persisted-position proof is absent",
    )


def validate_capture_bindings() -> None:
    for capture_name, (receipt, method) in CAPTURE_BINDINGS.items():
        committed = load_json(capture_name)
        embedded = embedded_capture(receipt, method)
        require(
            committed == embedded,
            f"{capture_name} is not the capture recorded by {receipt}:{method}",
        )
        require(
            committed.get("mutationArmed") is None,
            f"{capture_name} came from a perturbed run",
        )

    # The pre-mutation focused receipts embed an earlier capture of the same test. They are valid
    # clean runs, but they are NOT the committed captures; assert both facts so a reader who
    # compares session ids finds the discrepancy already explained rather than unexplained.
    for receipt, method in SUPERSEDED_FOCUSED_RECEIPTS.items():
        earlier = embedded_capture(receipt, method)
        require(earlier.get("mutationArmed") is None, f"{receipt} embeds a perturbed capture")
        invariants = earlier.get("invariants", {})
        require(
            invariants and all(value is True for value in invariants.values()),
            f"{receipt}: embedded capture does not satisfy every invariant",
        )
        committed_name = next(
            name for name, (bound, bound_method) in CAPTURE_BINDINGS.items()
            if bound_method == method
        )
        require(
            earlier != load_json(committed_name),
            f"{receipt} embeds the committed capture; the packet documents it as an earlier run",
        )


def validate_race() -> None:
    race = load_json("append-durability-race.json")
    require(race.get("schemaVersion") == 5, "append-durability-race.json: expected schemaVersion 5")
    provider = get(race, "providerProfile", "race")

    # Provider attribution is observed, not a source literal: the fixture reads the runtime version
    # from the exact daprd binary it launches and the images from the running containers.
    require(provider.get("daprRuntimeObserved") == "1.18.1", "unexpected observed Dapr runtime")
    require(provider.get("stateStoreType") == "state.redis", "unexpected state store type")
    require(provider.get("redisImageObserved") == "docker.io/redis:6", "unexpected Redis image")
    redis_image_id = provider.get("redisImageIdObserved", "")
    require(
        redis_image_id.startswith("sha256:"),
        "redisImageIdObserved must be a local image ID",
    )
    repo_digests = json.loads(provider.get("redisRepoDigestsObserved", "null"))
    require(
        isinstance(repo_digests, list) and any("redis@sha256:" in item for item in repo_digests),
        "redisRepoDigestsObserved must carry at least one pullable repository digest",
    )
    require("appendonly no" in provider.get("redisPersistenceObserved", ""), "unexpected Redis persistence")
    require(
        provider.get("placementImageObserved", "").endswith("daprio/dapr:1.18.2"),
        "unexpected placement image",
    )
    require(
        provider.get("schedulerImageObserved", "").endswith("daprio/dapr:1.18.2"),
        "unexpected scheduler image",
    )
    for field in ("placementImageIdObserved", "schedulerImageIdObserved"):
        require(provider.get(field, "").startswith("sha256:"), f"{field} must be an image ID")

    ports = get(race, "providerProfile.controlPlanePorts", "race")
    require(ports.get("placementProbeOrder") == [50005, 6050], "unexpected placement probe order")
    require(ports.get("schedulerProbeOrder") == [50006, 6060], "unexpected scheduler probe order")
    # The reviewed capture was taken with no port forwarder in place, so the resolved ports are the
    # second candidates. This is what proves the new dual-probe branch actually ran; the old
    # hardcoded predicate could only ever have produced 50005/50006.
    require(
        ports.get("placementResolved") == 6050,
        f"placement resolved to {ports.get('placementResolved')}, expected the 6050 branch this "
        "capture exists to exercise",
    )
    require(ports.get("schedulerResolved") == 6060, "scheduler did not resolve to the 6060 branch")

    canonical = provider.get("stateStoreComponentCanonicalYaml", "")
    hashed = provider.get("stateStoreComponentHashedYaml", "")
    component = provider.get("stateStoreComponentYaml", "")
    require("scopes:" not in canonical, "the hashed component must not carry the per-run scopes list")
    require(hashed == canonical, "the reviewed run must hash the canonical component form")
    require(
        hashlib.sha256(hashed.encode()).hexdigest() == provider.get("stateStoreComponentSha256"),
        "stateStoreComponentSha256 does not match the text it claims to hash",
    )
    require(canonical == component.split("\nscopes:", 1)[0], "canonical form is not the terminal-strip")
    require("name: actorStateStore" in canonical, "component is not an actor state store")
    require('value: "true"' in canonical, "actorStateStore is not enabled")
    require(f"scopes:\n  - {provider.get('appId')}" in component, "component is not scoped to the run app id")
    require(
        str(provider.get("productionAllocatorType", "")).endswith(".DaprGlobalPositionAllocator"),
        "the production allocator type is not the decorated one",
    )
    require("no aggregate identity" in provider.get("allocatorIdentityLimitation", ""),
            "the allocator identity limitation is not disclosed")

    session = get(race, "session", "race")
    require(session.get("targetActorId") == get(race, "aggregate.actorId", "race"), "gate target actor mismatch")
    require(
        session.get("targetMessageId") == get(race, "actorContender.messageId", "race"),
        "gate target message mismatch",
    )
    require(session.get("armCalls") == 1, "expected exactly one arm call")
    require(session.get("allocationAttempts") == 1, "expected exactly one allocation attempt")
    require(session.get("gateInterceptions") == 1, "expected exactly one gate interception")
    require(session.get("retryCount") == 0, "expected zero derived retries")
    for flag in ("actorTaskIncompleteAtGate", "actorTaskIncompleteAfterRaw", "actorTaskIncompleteAfterIntermediate"):
        require(session.get(flag) is True, f"{flag} must be true in the reviewed run")
    # Recorded, explicitly not evidence: the chain is stamped in sequential program order.
    require(session.get("timestampChainIsEvidence") is False, "the timestamp chain must not be claimed as evidence")
    require(session.get("decoyGateOccupantActorId") is None, "the reviewed run must have no decoy occupant")

    require(get(race, "rawContender.httpStatus", "race") == 204, "raw writer was not acknowledged")
    require(get(race, "rawContender.exceptionType", "race") is None, "raw writer hit a transport error")
    require(get(race, "intermediate.attemptedRegardlessOfRawStatus", "race") is True, "gated read was skipped")
    require(get(race, "intermediate.rawEventDurabilityProven", "race") is True, "raw event was not proven durable")
    require(get(race, "intermediate.rawDurabilityProven", "race") is True, "raw write was not proven durable")
    intermediate_event = get(race, "intermediate.event", "race")
    require(isinstance(intermediate_event, dict), "intermediate event is absent")
    require(
        intermediate_event.get("messageId") == get(race, "rawContender.messageId", "race"),
        "the gated read did not return the raw contender",
    )
    require(
        intermediate_event.get("correlationId") == get(race, "rawContender.correlationId", "race"),
        "gated read correlation mismatch",
    )
    require(
        intermediate_event.get("causationId") == get(race, "rawContender.messageId", "race"),
        "gated read causation mismatch",
    )

    final = get(race, "final", "race")
    require(final.get("finalSequenceWithinBounds") is True, "final sequence out of bounds")
    require(final.get("nextEventProbed") is True, "the one-past-the-end probe was skipped")
    require(final.get("finalStateFullyRead") is True, "final state was not fully read")
    require(final.get("tornShapeInjected") is False, "the reviewed run must not inject a torn shape")
    require(final.get("exactContendersOnly") is True, "a foreign writer reached the final stream")
    require(final.get("rawDurableWriteLost") is True, "the observed silent overwrite is absent")
    require(final.get("actorSurvives") is True, "the actor contender did not survive")
    require(final.get("unexpectedNextEventPresent") is False, "an event exists past the metadata sequence")
    require(get(race, "actorContender.result.accepted", "race") is True, "the actor did not accept")
    metadata = final.get("metadata")
    require(isinstance(metadata, dict), "final metadata is absent from the reviewed capture")
    events = final.get("events")
    require(isinstance(events, list) and len(events) == 1, "expected exactly one surviving event")
    require(metadata.get("currentSequence") == 1, "expected final sequence 1")
    final_event = events[0]
    require(
        final_event.get("correlationId") == get(race, "actorContender.messageId", "race"),
        "surviving event is not the actor contender",
    )
    require(
        final_event.get("causationId") == get(race, "actorContender.messageId", "race"),
        "surviving event causation mismatch",
    )
    require(
        final_event.get("messageId") == get(race, "actorContender.survivingEventMessageId", "race"),
        "surviving event message id mismatch",
    )

    # These facts are recorded rather than required by the test, so the reviewed capture is pinned
    # here: the validator asserts what was observed, while the test stays outcome-neutral for any
    # other provider profile.
    key_addressability = get(race, "keyAddressability", "race")
    require(
        key_addressability.get("classification") == "actor-key-absent-from-generic-namespace",
        "unexpected key-addressability classification",
    )
    require(key_addressability.get("compositeActorRedisReadable") is True, "composite key unreadable")
    require(
        key_addressability.get("genericStateKey") == get(race, "aggregate.metadataKey", "race"),
        "the probe did not target the aggregate metadata key",
    )
    require(
        key_addressability.get("genericStateKey")
        in str(key_addressability.get("genericStateProbeUrl", "")).replace("%3A", ":"),
        "the recorded probe URL does not carry the probed key",
    )
    require(
        key_addressability.get("compositeRedisKey")
        == get(race, "aggregate.compositeMetadataRedisKey", "race"),
        "composite Redis key mismatch",
    )
    require(key_addressability.get("compositeRedisMetadata") is not None, "composite metadata absent")
    require(final.get("shapeClassification") == "gapless-1-event-stream", "unexpected final shape")
    require(final.get("metadataEtagState") == "etag-absent", "unexpected metadata ETag state")

    infrastructure = get(race, "infrastructure", "race")
    require(
        infrastructure.get("writerEndpoint") == infrastructure.get("sidecarEndpoint"),
        "the raw writer did not use the live sidecar endpoint",
    )
    require(infrastructure.get("writerEndpointRedirected") is False, "writer endpoint was redirected")
    require(infrastructure.get("sidecarHealthy") is True, "the sidecar was not healthy")
    require(
        str(infrastructure.get("sidecarHealthProbeUrl", "")).startswith(
            str(infrastructure.get("sidecarEndpoint"))),
        "the health probe did not target the live sidecar",
    )

    invariants = get(race, "invariants", "race")
    require(set(invariants) == set(RACE_INVARIANTS), "race invariant key set drifted")
    require(all(value is True for value in invariants.values()), "a race invariant was falsified")
    require(race.get("mutationArmed") is None, "the committed race capture came from a perturbed run")

    observation = get(race, "observation", "race")
    require(
        observation.get("classification") == "same-key-overwrite-raw-durable-write-lost",
        "unexpected race classification",
    )
    require(
        observation.get("classifierSequence") == metadata.get("currentSequence"),
        "the classification was not computed from the observed sequence",
    )
    require(observation.get("isInternallyConsistent") is True, "the observation is internally inconsistent")
    require(observation.get("isInfrastructureFailure") is False, "the run hit an infrastructure failure")
    require(observation.get("invalidOperationExceptionSurfaced") is False, "an InvalidOperationException surfaced")
    require(observation.get("concurrencyConflictSignalled") is False, "a concurrency conflict was signalled")


def validate_generic_control() -> None:
    control = load_json("generic-etag-control.json")
    require(control.get("schemaVersion") == 4, "generic-etag-control.json: expected schemaVersion 4")
    require(control.get("mutationArmed") is None, "the committed control capture came from a perturbed run")
    require(control.get("postUpdateReadKey") == control.get("key"), "the post-update read used a decoy key")
    require(get(control, "interveningUpdate.etagAdvanced", "control") is True, "the ETag did not advance")
    require(get(control, "original.value.writer", "control") == "seed", "seed value mismatch")
    require(get(control, "original.parseExceptionType", "control") is None, "seed body did not parse")
    require(get(control, "interveningUpdate.value.writer", "control") == "first", "first-update value mismatch")
    require(get(control, "interveningUpdate.parseExceptionType", "control") is None, "current body did not parse")
    stale = get(control, "staleReplay", "control")
    require(stale.get("suppliedEtag") == get(control, "original.etag", "control"), "the replay did not use the original token")
    require(stale.get("suppliedEtagWasStale") is True, "the replayed token was not stale")
    require(stale.get("acknowledged") is False, "the stale replay was acknowledged")
    require(stale.get("status") == 409, "the stale replay did not return 409")
    require(stale.get("errorCode") == "ERR_STATE_SAVE", "unexpected Dapr error code")
    require("etag mismatch" in str(stale.get("errorMessage", "")).lower(), "no ETag-mismatch text")
    require(stale.get("parseError") is None, "the error body did not parse cleanly")
    require(control.get("retainedReadKey") == control.get("key"), "the retained read used a decoy key")
    require(control.get("expectedRetainedWriter") == "first", "unexpected retained-value expectation")
    require(control.get("redisRetainedValue") == {"writer": "first", "version": 1}, "unexpected retained value")
    require(
        json.loads(str(control.get("redisRetainedRawJson"))) == control.get("redisRetainedValue"),
        "the raw retained body and the parsed retained value disagree",
    )
    require(control.get("retainedValueMatchesExpected") is True, "the retained value did not match")
    require(control.get("retainedReadExceptionType") is None, "the retained read raised")
    invariants = get(control, "invariants", "control")
    require(set(invariants) == set(GENERIC_INVARIANTS), "generic invariant key set drifted")
    require(all(value is True for value in invariants.values()), "a generic invariant was falsified")


def validate_build_log() -> None:
    build_log = (EVIDENCE / "solution-build.log").read_text(encoding="utf-8")
    require("Build succeeded." in build_log, "the solution build did not succeed")
    require("0 Warning(s)" in build_log, "the solution build reported warnings")
    require("0 Error(s)" in build_log, "the solution build reported errors")

    # An up-to-date build skips CoreCompile, and MSBuild emits no warnings for a compile it never
    # ran -- which would make `0 Warning(s)` vacuous. It still prints a `Project -> path` line for
    # skipped projects, so the project count alone cannot tell the two apart. The load-bearing
    # check is the command line the capture block records in the log header.
    header = build_log.splitlines()[0] if build_log else ""
    require(
        header.startswith("$ dotnet build") and "--no-incremental" in header,
        "solution-build.log does not record a `--no-incremental` build command in its first line",
    )
    require("--configuration Release" in header, "the recorded build command is not a Release build")

    elapsed = re.search(r"Time Elapsed (\d+):(\d+):(\d+)\.(\d+)", build_log)
    require(elapsed is not None, "solution-build.log has no Time Elapsed line")
    seconds = (
        int(elapsed.group(1)) * 3600
        + int(elapsed.group(2)) * 60
        + int(elapsed.group(3))
        + float(f"0.{elapsed.group(4)}")
    )
    require(
        seconds >= BUILD_ELAPSED_FLOOR_SECONDS,
        f"solution build took {seconds}s, below the {BUILD_ELAPSED_FLOOR_SECONDS}s floor that "
        "separates a real compile from an up-to-date no-op",
    )

    # `.+?` rather than `\S+`, and separator normalization, so a Windows capture or a path with a
    # space cannot slip past the Debug rejection or empty the allowlist re-check.
    outputs = [
        (name.strip(), path.strip().replace("\\", "/"))
        for name, path in re.findall(r"^  (.+?) -> (.+)$", build_log, re.MULTILINE)
    ]
    require(
        len(outputs) >= MINIMUM_COMPILED_PROJECTS,
        f"solution-build.log records only {len(outputs)} project outputs; "
        f"expected at least {MINIMUM_COMPILED_PROJECTS}",
    )
    debug_outputs = [name for name, path in outputs if "/bin/Debug/" in path]
    unexpected = sorted(set(debug_outputs) - set(BUILD_LOG_DEBUG_ALLOWLIST))
    require(not unexpected, f"a Release build emitted Debug output paths for {unexpected}")

    # The allowlist may only cover projects that genuinely are not solution members.
    solution = (find_workspace() / "Hexalith.EventStore.slnx").read_text(encoding="utf-8")
    for name in BUILD_LOG_DEBUG_ALLOWLIST:
        require(
            f"{name}.csproj" not in solution,
            f"{name} is a solution member; its Debug output must not be allowlisted",
        )

    live_sidecar = [
        path for name, path in outputs
        if name == "Hexalith.EventStore.Server.LiveSidecar.Tests"
    ]
    require(live_sidecar, "solution-build.log does not record the LiveSidecar test assembly")
    require(
        all("/bin/Release/" in path for path in live_sidecar),
        f"the LiveSidecar test assembly is not a Release output: {live_sidecar}",
    )


def validate_environment_profile() -> None:
    environment = (EVIDENCE / "environment.md").read_text(encoding="utf-8")
    provider = get(load_json("append-durability-race.json"), "providerProfile", "race")
    for field in (
        "stateStoreComponentSha256",
        "daprRuntimeObserved",
        "redisImageIdObserved",
        "placementImageIdObserved",
        "schedulerImageIdObserved",
    ):
        value = str(provider.get(field, ""))
        require(value and value in environment, f"environment.md does not record the observed {field}")
    for digest in json.loads(provider.get("redisRepoDigestsObserved", "[]")):
        require(digest in environment, f"environment.md does not pin the Redis repository digest {digest}")
    require(
        "bb570eb45c2994eaf32da783cc098b3d51d1095b73ec92919863d73d0a9eaafb" in environment,
        "environment.md dropped the Zipkin image pin",
    )
    require("dapr init --runtime-version 1.18.1" in environment, "environment.md dropped the runtime pin")


def validate_redaction() -> None:
    absolute_workspace = re.compile(r"/home/[^/\s]+/projects/hexalith/eventstore")
    for path in EVIDENCE.iterdir():
        if not path.is_file() or path.name == Path(__file__).name:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        require(absolute_workspace.search(text) is None, f"absolute workspace in {path.name}")

    for path in EVIDENCE.glob("*.json"):
        document = json.loads(path.read_text(encoding="utf-8"))
        extra = document.get("results", {}).get("extra") or {}
        if "computer" in extra:
            require(extra["computer"] == "<redacted-machine>", f"{path.name}: machine name not redacted")
        if "user" in extra:
            require(extra["user"] == "<redacted-machine-user>", f"{path.name}: user not redacted")


def validate_source_binding() -> None:
    workspace = find_workspace()
    source_state = (EVIDENCE / "source-state.md").read_text(encoding="utf-8")
    rows = re.findall(r"^\| `([^`]+)` \| `([0-9a-f]{64})` \|$", source_state, re.MULTILINE)
    require(bool(rows), "source-state.md contains no source hash rows")
    bound = {relative for relative, _ in rows}
    missing = sorted(REQUIRED_SOURCE_ROWS - bound)
    require(not missing, f"source-state.md omits evidence-relevant sources: {missing}")
    for relative, expected in rows:
        path = workspace / relative
        require(path.is_file(), f"source-state.md binds a missing path: {relative}")
        require(sha256(path) == expected, f"source drifted since the seal: {relative}")


def validate_manifest() -> None:
    entries: dict[str, str] = {}
    for line in MANIFEST.read_text(encoding="utf-8").splitlines():
        digest, name = line.split("  ", 1)
        require(name != MANIFEST.name, "the manifest must not list itself")
        require(name not in entries, f"the manifest lists {name} twice")
        entries[name] = digest

    expected_files = {path.name for path in EVIDENCE.iterdir() if path.is_file() and path != MANIFEST}
    require(
        set(entries) == expected_files,
        f"manifest and directory disagree on {sorted(set(entries) ^ expected_files)}",
    )
    for name, expected in entries.items():
        require(sha256(EVIDENCE / name) == expected, f"hash mismatch for {name}")


def main() -> None:
    validate_mutation_registry()
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
        f"Story 4.5 evidence valid: {len(MUTATIONS)} perturbations over "
        f"{len(RACE_INVARIANTS) + len(GENERIC_INVARIANTS)} invariants, "
        f"{len(POSITIVE_RECEIPTS)} positive receipts, "
        f"{len(CAPTURE_BINDINGS)} capture-to-receipt bindings, "
        f"{len([path for path in EVIDENCE.iterdir() if path.is_file()]) - 1} hashed files"
    )


if __name__ == "__main__":
    main()
