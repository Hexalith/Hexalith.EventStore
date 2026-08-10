#!/usr/bin/env python3
"""Validate or sanitize Story 4.14 OQ8 production evidence using only stdlib."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
PACKET = ROOT / "_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml"
EVIDENCE = (
    ROOT
    / "_bmad-output/implementation-artifacts/evidence/story-4-14"
    / "e60a3777c581d70b62f67173ccc2372b5b64a425"
)
DESIGN_VERSION = "1.0.0"
DESIGN_SHA256 = "1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8"
BASELINE = "e60a3777c581d70b62f67173ccc2372b5b64a425"
PROFILE = "oq8-postgresql-v1"
POSTGRES_IMAGE = "postgres:18.4"
EVIDENCE_DIRECTORY = "_bmad-output/implementation-artifacts/evidence/story-4-14/e60a3777c581d70b62f67173ccc2372b5b64a425"
FOCUSED_METHOD = "Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.IdempotencyAdmissionOq8PostgresqlTests.ProductionMatrix_IndependentProcessesPreserveAuthorityReplayExpiryAndLeakageInvariants"
FOCUSED_TRAITS = {
    "Category": ["LiveSidecar"],
    "Profile": [PROFILE],
}
EXPECTED_SOURCE_INPUTS = {
    "deploy/dapr/resiliency.yaml",
    "deploy/dapr/statestore-postgresql.yaml",
    "samples/Hexalith.EventStore.Sample/Program.cs",
    "src/Hexalith.EventStore/Program.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlCollection.cs",
    "tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyAdmissionActorTests.cs",
    "tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyAdmissionDirectoryActorTests.cs",
    "tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyAdmissionExpiryTests.cs",
    "tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyTenantLifecycleActorTests.cs",
    "tests/Hexalith.EventStore.Server.Tests/Actors/PublicationRecoveryActivationTests.cs",
    "tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerIdempotencyAdmissionTests.cs",
}
REQUIRED_FILES = {
    "commands.json",
    "deterministic-support.json",
    "environment.json",
    "observations.json",
    "review-records.json",
    "source-state.json",
    "test-results.json",
}
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
PRIVATE_PATH_RE = re.compile(r"(?:/home/|/Users/|[A-Za-z]:[\\/]Users[\\/])")
PLACEHOLDER_RE = re.compile(r"(?:\bTBD\b|\bTODO\b|\bUNKNOWN\b|<[^>]+>)", re.IGNORECASE)
FORBIDDEN_CLAIM_RE = re.compile(
    r"(?:OQ8\s+(?:is\s+)?closed|Folders\s+OQ8\s+closure|production[- ]ready|release\s+approved)",
    re.IGNORECASE,
)
FORBIDDEN_CAPTURE_TERMS = (
    "PROTECTED-OQ8-RAW-SENTINEL",
    "Oq8EvidenceOnlySigningKey-AtLeast32Characters",
    "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=",
    "ZmVkY2JhOTg3NjU0MzIxMGZlZGNiYTk4NzY1NDMyMTA=",
    "POSTGRES_PASSWORD",
    "password=",
    "Bearer ",
    "eyJhbGci",
)
EXPECTED_SUPPORT_METHOD_CASES = {
    "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_PendingEquivalent_ReturnsFirstWriterTaskEvidenceWithoutDownstreamWork": 1,
    "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_Conflict_DeniesBeforeAggregateAndAdvisoryStores": 1,
    "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_AdmissionStoreUnavailableFailsClosedBeforeRoute": 1,
    "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_UnverifiableAdmission_ReturnsStableFailClosedOutcome": 3,
    "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_UnknownOutcome_ReconcilesReadOnlyAndFinalizesExactAggregateResult": 1,
    "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_UnknownOutcomeWithoutAuthoritativeResult_RemainsFailClosed": 1,
    "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.Coordinator_UnsafeLegacyInventoryDoesNoLifecycleAdmissionDirectoryOrMigrationWork": 2,
    "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.AdmitAsync_StateStoreUnavailableFailsClosedWithoutReservation": 1,
    "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.AdmitAsync_UnknownSchema_FailsClosedAsCorrupt": 1,
    "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.AdmitAsync_VerificationTagMismatchFailsClosedAsCollision": 1,
    "Hexalith.EventStore.Server.Tests.Actors.IdempotencyTenantLifecycleActorTests.MigrateLegacyAsync_RestartFromEveryDurablePhaseFinishesPinnedTarget": 6,
    "Hexalith.EventStore.Server.Tests.Actors.IdempotencyTenantLifecycleActorTests.MigrateLegacyAsync_UnsafeSourceEvidenceNeverPreparesTarget": 4,
    "Hexalith.EventStore.Server.Tests.Actors.PublicationRecoveryActivationTests.OnActivate_MissingCheckpointPruned_ReleasesTheRecoverableIdempotencyRecord": 1,
    "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_RouteFailure_MarksUnknownOutcomeUnderSameFence": 1,
    "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_WriteAheadFailureAfterFenceMarksUnknownOutcome": 1,
    "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionExpiryTests.ValidateAuthorityAsync_PendingAcceptsOnlyExactExecuteAuthorityWithoutMutation": 1,
    "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionExpiryTests.ValidateAuthorityAsync_UnknownOutcomeAcceptsOnlyExactReconciliationAuthorityWithoutMutation": 1,
    "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_TamperedCapabilityLeavesAdmissionUnchangedBeforeBegin": 1,
    "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_ContextThatLostDurableAuthorityPerformsZeroDownstreamWork": 1,
    "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionDirectoryActorTests.AdvanceAsync_PromotionOrder_KeepsSourceCanonicalUntilDirectoryFlip": 1,
    "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.Coordinator_OrdinaryActivationResponseLossReprovesExactTargetBeforeAdvance": 2,
}
SUPPORT_CLASSIFICATIONS = {
    "pending": [
        "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_PendingEquivalent_ReturnsFirstWriterTaskEvidenceWithoutDownstreamWork",
    ],
    "denied": [
        "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_Conflict_DeniesBeforeAggregateAndAdvisoryStores",
    ],
    "unavailable_or_corrupt": [
        "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_AdmissionStoreUnavailableFailsClosedBeforeRoute",
        "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_UnverifiableAdmission_ReturnsStableFailClosedOutcome",
        "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.AdmitAsync_StateStoreUnavailableFailsClosedWithoutReservation",
        "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.AdmitAsync_UnknownSchema_FailsClosedAsCorrupt",
    ],
    "unsafe_legacy": [
        "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_UnverifiableAdmission_ReturnsStableFailClosedOutcome",
        "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.Coordinator_UnsafeLegacyInventoryDoesNoLifecycleAdmissionDirectoryOrMigrationWork",
        "Hexalith.EventStore.Server.Tests.Actors.IdempotencyTenantLifecycleActorTests.MigrateLegacyAsync_UnsafeSourceEvidenceNeverPreparesTarget",
    ],
    "recoverable_without_checkpoint": [
        "Hexalith.EventStore.Server.Tests.Actors.PublicationRecoveryActivationTests.OnActivate_MissingCheckpointPruned_ReleasesTheRecoverableIdempotencyRecord",
    ],
    "reconciled_unknown_read_only": [
        "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_UnknownOutcome_ReconcilesReadOnlyAndFinalizesExactAggregateResult",
    ],
    "unreconciled_unknown_blocked": [
        "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_UnknownOutcomeWithoutAuthoritativeResult_RemainsFailClosed",
    ],
    "collision": [
        "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_UnverifiableAdmission_ReturnsStableFailClosedOutcome",
        "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.AdmitAsync_VerificationTagMismatchFailsClosedAsCollision",
    ],
    "migration": [
        "Hexalith.EventStore.Server.Tests.Actors.IdempotencyTenantLifecycleActorTests.MigrateLegacyAsync_RestartFromEveryDurablePhaseFinishesPinnedTarget",
        "Hexalith.EventStore.Server.Tests.Actors.IdempotencyTenantLifecycleActorTests.MigrateLegacyAsync_UnsafeSourceEvidenceNeverPreparesTarget",
    ],
}
FAULT_SUPPORT_CLASSIFICATIONS = {
    "route_or_write_ahead_unknown_outcome": [
        "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_RouteFailure_MarksUnknownOutcomeUnderSameFence",
        "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_WriteAheadFailureAfterFenceMarksUnknownOutcome",
    ],
    "durable_pending_or_unknown_authority": [
        "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionExpiryTests.ValidateAuthorityAsync_PendingAcceptsOnlyExactExecuteAuthorityWithoutMutation",
        "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionExpiryTests.ValidateAuthorityAsync_UnknownOutcomeAcceptsOnlyExactReconciliationAuthorityWithoutMutation",
    ],
    "tampered_or_lost_authority_zero_work": [
        "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_TamperedCapabilityLeavesAdmissionUnchangedBeforeBegin",
        "Hexalith.EventStore.Server.Tests.Pipeline.SubmitCommandHandlerIdempotencyAdmissionTests.Handle_ContextThatLostDurableAuthorityPerformsZeroDownstreamWork",
    ],
    "directory_promotion_ordering": [
        "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionDirectoryActorTests.AdvanceAsync_PromotionOrder_KeepsSourceCanonicalUntilDirectoryFlip",
    ],
    "activation_response_loss_reproof": [
        "Hexalith.EventStore.Server.Tests.Actors.IdempotencyAdmissionActorTests.Coordinator_OrdinaryActivationResponseLossReprovesExactTargetBeforeAdvance",
    ],
}
SUPPORT_CASE_TOTAL = sum(EXPECTED_SUPPORT_METHOD_CASES.values())
DIAGNOSTIC_FORBIDDEN_CLASSES = [
    "protected-input",
    "protected-result",
    "request-identifier",
    "test-key-material",
    "bearer-token",
    "database-credential",
    "private-path",
]


class EvidenceError(RuntimeError):
    """Raised when a fail-closed evidence rule is violated."""


def fail(message: str) -> None:
    raise EvidenceError(message)


def load_json(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8") as stream:
            return json.load(stream)
    except (OSError, json.JSONDecodeError) as exception:
        fail(f"Cannot load JSON evidence {path.relative_to(ROOT) if path.is_relative_to(ROOT) else path.name}: {exception}")


def write_json(path: Path, value: Any) -> None:
    path.mkdir(parents=True, exist_ok=True) if path.suffix == "" else path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(f"{path.suffix}.tmp")
    with temporary.open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, indent=2, sort_keys=False)
        stream.write("\n")
    temporary.replace(path)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        fail(message)


def scan_support_safe(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    require(not PRIVATE_PATH_RE.search(text), f"Private path found in {path.name}")
    require(not PLACEHOLDER_RE.search(text), f"Placeholder found in {path.name}")
    require(not FORBIDDEN_CLAIM_RE.search(text), f"Closure/release claim found in {path.name}")
    for term in FORBIDDEN_CAPTURE_TERMS:
        require(term.lower() not in text.lower(), f"Protected or secret-like term found in {path.name}")


def require_sha256(value: Any, field: str) -> str:
    require(isinstance(value, str) and SHA256_RE.fullmatch(value) is not None, f"{field} is not SHA-256")
    return value


def validate_observations(path: Path) -> dict[str, Any]:
    scan_support_safe(path)
    document = load_json(path)
    require(isinstance(document, dict), "observations.json must be an object")
    require(document.get("schemaVersion") == 1, "Observation schemaVersion drift")
    require(document.get("captureKind") == "release-entry-binaries-test-seams-sidecar-postgresql", "Observation capture kind drift")
    require(re.fullmatch(r"\d{4}-\d{2}-\d{2}", str(document.get("capturedOn", ""))) is not None, "Capture date missing")

    topology = document.get("topology", {})
    require(topology.get("eventStoreProcessCount") == 2, "Two EventStore processes were not observed")
    require(topology.get("eventStoreSidecarCount") == 2, "Two EventStore sidecars were not observed")
    require(topology.get("sampleProcessCount") == 1, "The Sample process was not observed")
    require(topology.get("sampleSidecarCount") == 1, "The Sample sidecar was not observed")
    require(topology.get("independentProcessIdentities") is True, "Process identities were not independent")

    profile = document.get("profile", {})
    require(profile.get("name") == PROFILE, "OQ8 profile drift")
    require(profile.get("stateStoreType") == "state.postgresql", "State store is not PostgreSQL")
    require_sha256(profile.get("stateComponentSha256"), "state component identity")
    require_sha256(profile.get("resiliencySha256"), "resiliency identity")

    runtime = document.get("runtime", {})
    require(isinstance(runtime.get("dotnet"), str) and runtime["dotnet"], ".NET runtime identity missing")
    require(runtime.get("dapr") == "1.18.1", "Dapr runtime identity drift")
    require(runtime.get("postgresImage") == POSTGRES_IMAGE, "PostgreSQL image tag drift")
    require(str(runtime.get("postgresImageIdentity", "")).startswith("sha256:"), "PostgreSQL immutable identity missing")

    execution_configuration = document.get("executionConfiguration", {})
    require(
        execution_configuration == {
            "shippedReleaseEntryAssemblies": True,
            "shadowCopiedBeforeLaunch": True,
            "environmentName": "Testing",
            "testOnlyHostingStartup": True,
            "productionConfigurationUntouched": False,
            "seams": ["deterministic-time", "idempotency-intent-adapter", "boundary-counter"],
        },
        "Test-only execution configuration disclosure drift",
    )

    artifacts = document.get("artifacts", {})
    require(set(artifacts) == {
        "eventStoreSha256",
        "sampleSha256",
        "eventStoreRuntimeSetSha256",
        "sampleRuntimeSetSha256",
        "hostingStartupSha256",
        "additionalDepsSha256",
    }, "Runtime artifact identity set drift")
    for name, value in artifacts.items():
        require_sha256(value, f"runtime artifact identity:{name}")

    diagnostics = document.get("diagnostics", {})
    require(diagnostics.get("streamsScanned") == 12, "All bounded process diagnostic streams were not scanned")
    require(diagnostics.get("boundedCharacterLimitPerStream") == 32768, "Diagnostic stream bound drift")
    require(diagnostics.get("forbiddenTermClassesScanned") == DIAGNOSTIC_FORBIDDEN_CLASSES, "Diagnostic forbidden-term class coverage drift")
    require(diagnostics.get("postRedactionProtectedMatches") == 0, "Protected diagnostics remain after redaction")
    require(diagnostics.get("rawDiagnosticsCommitted") is False, "Raw diagnostics were committed")
    require_sha256(diagnostics.get("sanitizedProjectionSha256"), "Sanitized diagnostic projection identity")

    observations = document.get("observations", {})
    require(set(observations) == {"writers_failover", "expiry_compaction", "authority_change", "capture"}, "Observation matrix is incomplete")
    writers = observations["writers_failover"]
    require(writers.get("concurrentRequests", 0) >= 2, "Concurrent writer count is insufficient")
    require(writers.get("canonicalExecutionIdentities") == 1, "Canonical execution identity count is not one")
    require(writers.get("durableFencePositive") is True, "Durable positive fence was not observed")
    require(writers.get("sampleExecutions") == 1, "Sample execution count is not one")
    require(writers.get("ownerStoppedAtTerminalBoundary") is True, "Known owner failover was not observed")
    require(writers.get("failoverAttempts", 0) >= 1, "Failover request was not observed")
    require(writers.get("failoverReplayExact") is True, "Failover replay content was not exact")
    require(writers.get("restartedNodeReplayExact") is True, "Restart replay was not exact")
    require(writers.get("conflictStatus") == 409, "Different-payload conflict was not terminal")
    require(writers.get("crossTargetConflictStatus") == 409, "Different-target conflict was not terminal")
    require(writers.get("nonExecuteAdditionalWork") == 0, "Writer non-execute path performed work")

    expiry = observations["expiry_compaction"]
    require(expiry.get("oneTickBefore") == 202, "T-1 replay was not accepted")
    require(expiry.get("oneTickBeforeReplayExact") is True, "T-1 replay content was not exact")
    require(expiry.get("inclusiveBoundary") == 409, "Inclusive T expiry was not terminal")
    require(expiry.get("oneTickAfter") == 409, "T+1 expiry was not terminal")
    require(expiry.get("terminalBecameMinimalTombstone") is True, "Expiry did not compact atomically")
    require(expiry.get("equivalentAndDifferentReuseShareOutcome") is True, "Expired reuse outcomes diverged")
    require(expiry.get("nonExecuteAdditionalWork") == 0, "Expiry non-execute path performed work")

    authority = observations["authority_change"]
    require(authority.get("rotationReplayExact") is True, "Rotation replay was not exact")
    require(authority.get("canonicalAuthorityCount") == 1, "Rotated canonical authority count is not one")
    require(authority.get("retiredReaderReplayExact") is True, "Retired-reader replay was not exact")
    require(authority.get("legalHoldState") == "LegalHold", "Legal hold was not serialized")
    require(authority.get("releasedState") == "Retaining", "Hold release did not restore retaining state")
    require(authority.get("failClosedStatuses") == [503, 503], "Governance unavailable states did not fail closed")
    require(authority.get("sampleExecutions") == 2, "Authority-change eligible execution count is not two")
    require(authority.get("nonExecuteAdditionalWork") == 0, "Authority non-execute path performed work")
    require(
        authority.get("deterministicSupportOracles") == list(EXPECTED_SUPPORT_METHOD_CASES),
        "Deterministic support oracle identities or order drifted",
    )

    capture = observations["capture"]
    before = capture.get("before", {})
    after = capture.get("after", {})
    require(before.get("stage") == "before" and after.get("stage") == "after", "Before/after snapshot labels drifted")
    require(before.get("schemaSha256") == after.get("schemaSha256"), "PostgreSQL schema changed during capture")
    require_sha256(before.get("schemaSha256"), "PostgreSQL schema identity")
    require_sha256(before.get("projectionSha256"), "Before projection identity")
    require_sha256(after.get("projectionSha256"), "After projection identity")
    require(after.get("aggregateSequenceTotal") == before.get("aggregateSequenceTotal", 0) + 4, "Eligible execution count is not four")
    require(after.get("aggregateMetadataRows") == before.get("aggregateMetadataRows", 0) + 4, "Aggregate metadata row delta is not exactly four")
    require(after.get("aggregateEventRows") == before.get("aggregateEventRows", 0) + 4, "Aggregate event row delta is not exactly four")
    require(after.get("minimalTombstoneRows", 0) >= before.get("minimalTombstoneRows", 0) + 1, "Minimal tombstone delta missing")
    require(after.get("directoryRows", 0) > before.get("directoryRows", 0), "Digest directory state missing")
    require(after.get("lifecycleRows", 0) > before.get("lifecycleRows", 0), "Tenant lifecycle state missing")
    require(after.get("protectedSentinelMatches") == 0 and capture.get("protectedSentinelMatches") == 0, "Protected sentinel leakage detected")
    require(capture.get("committedProjectionContainsIdentifiers") is False, "Committed projection contains identifiers")
    require(capture.get("closureClaimed") is False, "Capture claims closure")
    return document


def sanitize_ctrf(ctrf_path: Path, destination: Path) -> dict[str, Any]:
    ctrf = load_json(ctrf_path)
    results = ctrf.get("results", {}) if isinstance(ctrf, dict) else {}
    summary = results.get("summary", {})
    tests = results.get("tests", [])
    require(summary.get("tests") == 1, "Focused CTRF must contain exactly one test")
    require(summary.get("passed") == 1, "Focused CTRF does not report one pass")
    require(summary.get("failed") == 0, "Focused CTRF reports a failure")
    require(summary.get("skipped") == 0, "Focused CTRF reports a skip")
    require(isinstance(tests, list) and len(tests) == 1, "Focused CTRF test record is incomplete")
    test = tests[0]
    require(test.get("name") == FOCUSED_METHOD, "Focused CTRF test identity drift")
    require(test.get("extra", {}).get("traits") == FOCUSED_TRAITS, "Focused CTRF Category/Profile traits drift")
    portable = {
        "schemaVersion": 1,
        "runner": "xUnit.net v3",
        "command": f"dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method {FOCUSED_METHOD} -noColor -ctrf raw-runner-temp",
        "summary": {
            "tests": 1,
            "passed": 1,
            "failed": 0,
            "skipped": 0,
        },
        "test": {
            "name": test.get("name"),
            "status": test.get("status"),
            "durationMilliseconds": test.get("duration"),
            "traits": test.get("extra", {}).get("traits", {}),
        },
    }
    validate_focused_document(portable)
    write_json(destination, portable)
    scan_support_safe(destination)
    return portable


def expected_support_classifications() -> dict[str, Any]:
    classifications = {
        classification: {
            "commandExecutionWork": 0,
            "methods": methods,
        }
        for classification, methods in SUPPORT_CLASSIFICATIONS.items()
    }
    classifications.update({
        classification: {
            "evidenceRole": "deterministic-fault-or-fence-support",
            "methods": methods,
        }
        for classification, methods in FAULT_SUPPORT_CLASSIFICATIONS.items()
    })
    return classifications


def validate_focused_document(document: Any) -> dict[str, Any]:
    require(isinstance(document, dict), "test-results.json must be an object")
    require(document.get("schemaVersion") == 1, "Focused result schemaVersion drift")
    require(document.get("runner") == "xUnit.net v3", "Focused result runner drift")
    require(
        document.get("command")
        == f"dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method {FOCUSED_METHOD} -noColor -ctrf raw-runner-temp",
        "Focused result command identity drift",
    )
    require(
        document.get("summary") == {"tests": 1, "passed": 1, "failed": 0, "skipped": 0},
        "Focused result is not exactly one green case",
    )
    test = document.get("test", {})
    require(test.get("name") == FOCUSED_METHOD, "Focused result test identity drift")
    require(test.get("status") == "passed", "Focused test status is not passed")
    require(test.get("traits") == FOCUSED_TRAITS, "Focused result Category/Profile traits drift")
    require(isinstance(test.get("durationMilliseconds"), (int, float)), "Focused result duration is missing")
    return document


def validate_support_document(document: Any) -> dict[str, Any]:
    require(isinstance(document, dict), "deterministic-support.json must be an object")
    require(document.get("schemaVersion") == 1, "Deterministic support schemaVersion drift")
    require(document.get("runner") == "xUnit.net v3", "Deterministic support runner drift")
    require(
        document.get("command")
        == "dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -method (21 validator-pinned selectors) -noColor -ctrf raw-runner-temp",
        "Deterministic support command identity drift",
    )
    require(document.get("selectors") == list(EXPECTED_SUPPORT_METHOD_CASES), "Deterministic support selectors drift")
    require(
        document.get("summary") == {"tests": SUPPORT_CASE_TOTAL, "passed": SUPPORT_CASE_TOTAL, "failed": 0, "skipped": 0},
        f"Deterministic support summary is not exactly {SUPPORT_CASE_TOTAL}/{SUPPORT_CASE_TOTAL} green",
    )
    expected_methods = [
        {
            "identity": identity,
            "expectedCases": expected_cases,
            "observedCases": expected_cases,
            "passedCases": expected_cases,
        }
        for identity, expected_cases in EXPECTED_SUPPORT_METHOD_CASES.items()
    ]
    require(document.get("methods") == expected_methods, "Deterministic support method identities or exact case counts drifted")
    require(
        document.get("classifications") == expected_support_classifications(),
        "Deterministic support non-execute classification coverage drifted",
    )
    return document


def sanitize_support_ctrf(ctrf_path: Path, destination: Path) -> dict[str, Any]:
    ctrf = load_json(ctrf_path)
    results = ctrf.get("results", {}) if isinstance(ctrf, dict) else {}
    summary = results.get("summary", {})
    tests = results.get("tests", [])
    require(summary.get("tests") == SUPPORT_CASE_TOTAL, f"Deterministic support CTRF must contain exactly {SUPPORT_CASE_TOTAL} cases")
    require(summary.get("passed") == SUPPORT_CASE_TOTAL, f"Deterministic support CTRF does not report {SUPPORT_CASE_TOTAL} passes")
    require(summary.get("failed") == 0, "Deterministic support CTRF reports a failure")
    require(summary.get("skipped") == 0, "Deterministic support CTRF reports a skip")
    require(isinstance(tests, list) and len(tests) == SUPPORT_CASE_TOTAL, "Deterministic support CTRF records are incomplete")

    observed = {identity: 0 for identity in EXPECTED_SUPPORT_METHOD_CASES}
    passed = {identity: 0 for identity in EXPECTED_SUPPORT_METHOD_CASES}
    for test in tests:
        require(isinstance(test, dict), "Deterministic support CTRF contains an invalid test record")
        name = test.get("name")
        require(isinstance(name, str), "Deterministic support CTRF test identity is missing")
        matches = [identity for identity in EXPECTED_SUPPORT_METHOD_CASES if name == identity or name.startswith(f"{identity}(")]
        require(len(matches) == 1, f"Unexpected or ambiguous deterministic support test: {name}")
        identity = matches[0]
        observed[identity] += 1
        require(test.get("status") == "passed", f"Deterministic support case did not pass: {identity}")
        passed[identity] += 1

    for identity, expected_cases in EXPECTED_SUPPORT_METHOD_CASES.items():
        require(observed[identity] == expected_cases, f"Deterministic support case count drifted: {identity}")
        require(passed[identity] == expected_cases, f"Deterministic support pass count drifted: {identity}")

    portable = {
        "schemaVersion": 1,
        "runner": "xUnit.net v3",
        "command": "dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -method (21 validator-pinned selectors) -noColor -ctrf raw-runner-temp",
        "selectors": list(EXPECTED_SUPPORT_METHOD_CASES),
        "summary": {
            "tests": SUPPORT_CASE_TOTAL,
            "passed": SUPPORT_CASE_TOTAL,
            "failed": 0,
            "skipped": 0,
        },
        "methods": [
            {
                "identity": identity,
                "expectedCases": expected_cases,
                "observedCases": observed[identity],
                "passedCases": passed[identity],
            }
            for identity, expected_cases in EXPECTED_SUPPORT_METHOD_CASES.items()
        ],
        "classifications": expected_support_classifications(),
    }
    validate_support_document(portable)
    write_json(destination, portable)
    scan_support_safe(destination)
    return portable


def validate_capture(capture_directory: Path, ctrf_path: Path, support_ctrf_path: Path) -> None:
    require(capture_directory.is_dir(), "Capture directory is missing")
    require(not ctrf_path.is_relative_to(capture_directory), "Raw focused CTRF must remain outside the capture directory")
    require(not support_ctrf_path.is_relative_to(capture_directory), "Raw support CTRF must remain outside the capture directory")
    require(
        {path.name for path in capture_directory.iterdir()} == {"observations.json"},
        "Fresh capture directory contains unexpected pre-existing files",
    )
    observations_path = capture_directory / "observations.json"
    require(observations_path.is_file(), "Capture observations.json is missing")
    validate_observations(observations_path)
    sanitize_ctrf(ctrf_path, capture_directory / "test-results.json")
    sanitize_support_ctrf(support_ctrf_path, capture_directory / "deterministic-support.json")
    receipt = {
        "schemaVersion": 1,
        "validation": "passed",
        "observationsSha256": sha256_file(observations_path),
        "testResultsSha256": sha256_file(capture_directory / "test-results.json"),
        "deterministicSupportSha256": sha256_file(capture_directory / "deterministic-support.json"),
    }
    write_json(capture_directory / "capture-validation.json", receipt)
    scan_support_safe(capture_directory / "capture-validation.json")
    require(
        {path.name for path in capture_directory.iterdir()}
        == {"observations.json", "test-results.json", "deterministic-support.json", "capture-validation.json"},
        "Sanitized capture output file set drift",
    )


def validate_manifest() -> dict[str, str]:
    manifest_path = EVIDENCE / "evidence-sha256.txt"
    require(manifest_path.is_file(), "Evidence manifest is missing")
    scan_support_safe(manifest_path)
    manifest: dict[str, str] = {}
    for line in manifest_path.read_text(encoding="utf-8").splitlines():
        parts = line.split("  ", 1)
        require(len(parts) == 2 and SHA256_RE.fullmatch(parts[0]) is not None, "Malformed evidence manifest line")
        digest, name = parts
        require("/" not in name and "\\" not in name and name not in manifest, "Unsafe or duplicate manifest name")
        manifest[name] = digest
    require(set(manifest) == REQUIRED_FILES, "Evidence manifest file set is incomplete or contains extras")
    for name, expected in manifest.items():
        path = EVIDENCE / name
        require(path.is_file(), f"Manifest artifact missing: {name}")
        require(sha256_file(path) == expected, f"Evidence checksum mismatch: {name}")
        scan_support_safe(path)
    return manifest


def changed_candidate_files() -> set[str]:
    paths: set[str] = set()
    for arguments in (
        ["diff", "--name-only", "-z", BASELINE, "--"],
        ["ls-files", "--others", "--exclude-standard", "-z"],
    ):
        result = subprocess.run(
            ["git", *arguments],
            cwd=ROOT,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
        require(result.returncode == 0, "Git could not derive the complete candidate source set")
        try:
            discovered = result.stdout.decode("utf-8").split("\0")
        except UnicodeDecodeError:
            fail("Git returned a non-UTF-8 candidate source path")
        paths.update(
            relative
            for relative in discovered
            if relative and not relative.startswith("_bmad-output/")
        )
    return paths


def validate_source_state(document: dict[str, Any]) -> None:
    require(document.get("baselineCommit") == BASELINE, "Baseline commit drift")
    candidate_files = document.get("candidateFiles", {})
    source_inputs = document.get("sourceInputs", {})
    require(isinstance(candidate_files, dict) and candidate_files, "Candidate file identities missing")
    require(isinstance(source_inputs, dict) and source_inputs, "Source input identities missing")
    require(set(candidate_files) == changed_candidate_files(), "Candidate files are not the complete Git-derived changed source/config/tool set")
    require(set(source_inputs) == EXPECTED_SOURCE_INPUTS, "Pinned source-input identity set drift")
    for collection_name, collection in (("candidateFiles", candidate_files), ("sourceInputs", source_inputs)):
        for relative, expected in collection.items():
            require(isinstance(relative, str) and not Path(relative).is_absolute() and ".." not in Path(relative).parts, f"Unsafe {collection_name} path")
            require_sha256(expected, f"{collection_name}:{relative}")
            path = ROOT / relative
            require(path.is_file(), f"Bound source file missing: {relative}")
            require(sha256_file(path) == expected, f"Bound source identity drift: {relative}")
    lines = [f"{relative}:{candidate_files[relative]}" for relative in sorted(candidate_files)]
    candidate_digest = hashlib.sha256(("\n".join(lines) + "\n").encode("utf-8")).hexdigest()
    require(document.get("candidateDiffSha256") == candidate_digest, "Candidate diff identity drift")
    require(
        document.get("candidateDiffAlgorithm")
        == "sha256(sorted complete changed source/config/tool relative-path:file-sha256 lines)",
        "Candidate diff algorithm drift",
    )
    require(document.get("dirtySourceCaptured") is True, "Dirty-source condition was not recorded")


def validate_committed_packet() -> None:
    require(PACKET.is_file(), "OQ8 packet is missing")
    require(EVIDENCE.is_dir(), "OQ8 evidence directory is missing")
    scan_support_safe(PACKET)
    packet = load_json(PACKET)
    require(packet.get("schemaVersion") == 1, "Packet schemaVersion drift")
    require(packet.get("story") == "4.14", "Packet story drift")
    require(packet.get("design") == {"version": DESIGN_VERSION, "sha256": DESIGN_SHA256}, "OQ8 design identity drift")
    require(packet.get("profile") == PROFILE, "Packet profile drift")
    require(packet.get("baselineCommit") == BASELINE, "Packet baseline drift")
    require(packet.get("evidenceDirectory") == EVIDENCE_DIRECTORY, "Packet evidence directory drift")
    require(packet.get("matrix") == {
        "writersFailover": "passed",
        "expiryCompaction": "passed",
        "authorityChange": "passed",
        "deterministicSupport": "passed",
        "diagnosticLeakage": "passed",
        "sanitizedCapture": "passed",
    }, "Packet matrix is not the exact six-entry passed set")
    require(packet.get("closureClaimed") is False, "Packet claims closure")
    require(packet.get("releaseApproved") is False, "Packet claims release approval")
    require(packet.get("story415Status") == "backlog", "Packet advances Story 4.15")

    manifest = validate_manifest()
    evidence_files = packet.get("evidenceFiles", {})
    require(evidence_files == manifest, "Packet evidence identities do not match the manifest")
    require_sha256(packet.get("manifestSha256"), "Manifest identity")
    require(packet["manifestSha256"] == sha256_file(EVIDENCE / "evidence-sha256.txt"), "Packet manifest identity drift")

    observations = validate_observations(EVIDENCE / "observations.json")
    deterministic_support_path = EVIDENCE / "deterministic-support.json"
    deterministic_support = validate_support_document(load_json(deterministic_support_path))
    require(
        observations["observations"]["authority_change"]["deterministicSupportOracles"]
        == deterministic_support["selectors"],
        "Observation and deterministic support oracle identities drifted",
    )
    source_state = load_json(EVIDENCE / "source-state.json")
    validate_source_state(source_state)
    environment = load_json(EVIDENCE / "environment.json")
    require(packet.get("capturedOn") == observations.get("capturedOn"), "Packet capture date crosswalk drift")
    require(environment.get("runtime") == observations.get("runtime"), "Runtime identity crosswalk drift")
    require(environment.get("profile") == observations.get("profile"), "Profile identity crosswalk drift")
    require(environment.get("executionConfiguration") == observations.get("executionConfiguration"), "Execution-configuration disclosure crosswalk drift")
    require(environment.get("artifacts") == observations.get("artifacts"), "Runtime artifact identity crosswalk drift")
    require(environment.get("capturedOn") == observations.get("capturedOn"), "Capture date crosswalk drift")
    state_component_path = ROOT / "deploy/dapr/statestore-postgresql.yaml"
    resiliency_path = ROOT / "deploy/dapr/resiliency.yaml"
    require(
        observations["profile"]["stateComponentSha256"]
        == sha256_file(state_component_path)
        == source_state["sourceInputs"]["deploy/dapr/statestore-postgresql.yaml"],
        "PostgreSQL component identity crosswalk drift",
    )
    require(
        observations["profile"]["resiliencySha256"]
        == sha256_file(resiliency_path)
        == source_state["sourceInputs"]["deploy/dapr/resiliency.yaml"],
        "Resiliency identity crosswalk drift",
    )
    limits = environment.get("limits", {})
    require(limits.get("healthTimeoutSeconds") == 60, "Environment health timeout drift")
    require(limits.get("nodeReadinessOverallTimeoutSeconds") == 60, "Environment overall node-readiness deadline drift")
    require(limits.get("actorRuntimeReadinessRequired") is True, "Environment actor-runtime readiness requirement drift")
    require(limits.get("diagnosticLogCharactersPerStream") == 32768, "Environment diagnostic bound drift")
    require(limits.get("diagnosticStreamsScanned") == 12, "Environment diagnostic stream count drift")
    require(limits.get("forbiddenTermClassesScanned") == len(DIAGNOSTIC_FORBIDDEN_CLASSES), "Environment forbidden-term class count drift")
    require(limits.get("rawDiagnosticsCommitted") is False, "Environment permits committed raw diagnostics")
    test_results = load_json(EVIDENCE / "test-results.json")
    validate_focused_document(test_results)
    commands = load_json(EVIDENCE / "commands.json")
    require(commands.get("capturedOn") == observations.get("capturedOn"), "Command record capture date crosswalk drift")
    require(all(item.get("exitCode") == 0 for item in commands.get("commands", [])), "A recorded verification command failed")
    command_records = {item.get("name"): item for item in commands.get("commands", [])}
    require(set(command_records) == {
        "live-sidecar-release-build",
        "focused-production-matrix",
        "explicit-deterministic-support-oracles",
        "deterministic-support-lane",
        "fresh-capture-validator",
        "committed-packet-validator",
        "solution-release-build",
        "diff-whitespace-gate",
    }, "Verification command record set drift")
    require(
        command_records["focused-production-matrix"].get("command") == test_results.get("command"),
        "Recorded focused method command drift",
    )
    require(
        command_records["explicit-deterministic-support-oracles"].get("command")
        == deterministic_support.get("command"),
        "Recorded deterministic support command drift",
    )
    require(
        command_records["explicit-deterministic-support-oracles"].get("counts")
        == {"methods": len(EXPECTED_SUPPORT_METHOD_CASES), "passed": SUPPORT_CASE_TOTAL, "failed": 0, "skipped": 0},
        "Recorded deterministic support counts drift",
    )
    reviews = load_json(EVIDENCE / "review-records.json")
    require(any(
        record == {
            "kind": "production-evidence-review",
            "performed": False,
            "approval": False,
            "ownedBy": "Murat",
        }
        for record in reviews.get("records", [])
    ), "Pending Murat production-evidence review record is missing")
    require(any(
        record == {
            "kind": "leakage-fence-review",
            "performed": False,
            "approval": False,
            "ownedBy": "Security Reviewer",
        }
        for record in reviews.get("records", [])
    ), "Pending Security Reviewer leakage/fence review record is missing")
    require(reviews.get("releaseApproval") is False, "Review record claims release approval")
    require(reviews.get("foldersOq8Closure") is False, "Review record claims Folders OQ8 closure")
    require(reviews.get("story415Status") == "backlog", "Review record advances Story 4.15")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture-directory", type=Path, help="Validate one fresh opt-in OQ8 capture")
    parser.add_argument("--ctrf", type=Path, help="Raw CTRF input to sanitize for capture upload")
    parser.add_argument("--support-ctrf", type=Path, help="Raw deterministic-support CTRF input to validate and sanitize")
    parser.add_argument("--support-output", type=Path, help="Write one sanitized deterministic-support document")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        if args.capture_directory is not None or args.ctrf is not None:
            require(
                args.capture_directory is not None and args.ctrf is not None and args.support_ctrf is not None,
                "Capture mode requires --capture-directory, --ctrf, and --support-ctrf",
            )
            require(args.support_output is None, "Capture mode writes deterministic support into the capture directory")
            validate_capture(args.capture_directory.resolve(), args.ctrf.resolve(), args.support_ctrf.resolve())
            print("OQ8 capture validation passed.")
        elif args.support_ctrf is not None or args.support_output is not None:
            require(args.support_ctrf is not None and args.support_output is not None, "Support mode requires --support-ctrf and --support-output")
            sanitize_support_ctrf(args.support_ctrf.resolve(), args.support_output.resolve())
            print("OQ8 deterministic support validation passed.")
        else:
            validate_committed_packet()
            print("OQ8 platform evidence validation passed.")
        return 0
    except EvidenceError as exception:
        print(f"OQ8 evidence validation failed: {exception}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
