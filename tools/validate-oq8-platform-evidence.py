#!/usr/bin/env python3
"""Validate Story 4.14 capture and Story 4.15 OQ8 platform closure evidence."""

from __future__ import annotations

import argparse
import hashlib
import importlib
import importlib.metadata
import json
import math
import os
import re
import selectors
import subprocess
import sys
import time
from pathlib import Path
from typing import Any

try:
    yaml = importlib.import_module("yaml")
except Exception:
    yaml = None


DEFAULT_ROOT = Path(__file__).resolve().parents[1]
ROOT = DEFAULT_ROOT
GIT_ROOT = DEFAULT_ROOT
GIT_TIMEOUT_SECONDS = 30.0
GIT_OUTPUT_LIMIT_BYTES = 131072
GIT_BLOB_LIMIT_BYTES = 8 * 1024 * 1024
PACKET = ROOT / "_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml"
EVIDENCE = (
    ROOT
    / "_bmad-output/implementation-artifacts/evidence/story-4-14"
    / "e60a3777c581d70b62f67173ccc2372b5b64a425"
)
CLOSURE = (
    ROOT
    / "_bmad-output/implementation-artifacts/evidence/story-4-15"
    / "5e8f175b2ced4715f7c6f765386812cc1001dbb4"
)
SUCCESSOR_SELECTOR = (
    ROOT
    / "_bmad-output/implementation-artifacts/4-15-oq8-platform-closure-successor.json"
)
SUCCESSOR = (
    ROOT
    / "_bmad-output/implementation-artifacts/evidence/story-4-15/successors"
    / "sdk-10.0.400-xunit4-mtp"
)
DESIGN_VERSION = "1.0.0"
DESIGN_SHA256 = "1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8"
BASELINE = "e60a3777c581d70b62f67173ccc2372b5b64a425"
LANDED_SOURCE = "5e8f175b2ced4715f7c6f765386812cc1001dbb4"
LANDED_TREE = "96fdfbba56df41b58889bf7f3b532a64d15314bd"
PRIOR_CLOSURE_COMMIT = "9fbd8cbf2687a2cbb0172a14eaa68f9b276ee105"
PRIOR_VALIDATOR_SHA256 = "9652019053810366ec3a7682490a5b85385880b63bb3bf2ca7023b0a49c18dde"
SUCCESSOR_REVIEW_BASE = "cf320fd907430156d1d82e54f0aa404bdef73704"
PRIOR_PACKET_SHA256 = "ab6931160b1b9574f6f0e8c5698a0982e45978071cc311951d9604d27a5650a4"
PRIOR_CLOSURE_MANIFEST_SHA256 = "da3994dfd687b4ecf7a150b0b8b2d9fa41e54d1ea8d57641163dc6532bfb9e47"
PROFILE = "oq8-postgresql-v1"
POSTGRES_IMAGE = "postgres:18.4"
COMMITTED_DAPR_RUNTIME_VERSION = "1.18.1"
CURRENT_REVIEW_DATE = "2026-08-27"
PINNED_PYYAML_VERSION = "6.0.3"
PINNED_PYYAML_REQUIREMENT = f"PyYAML=={PINNED_PYYAML_VERSION}"
MAX_SPRINT_STATUS_BYTES = 1_048_576
EVIDENCE_DIRECTORY = "_bmad-output/implementation-artifacts/evidence/story-4-14/e60a3777c581d70b62f67173ccc2372b5b64a425"
CLOSURE_DIRECTORY = "_bmad-output/implementation-artifacts/evidence/story-4-15/5e8f175b2ced4715f7c6f765386812cc1001dbb4"
SUCCESSOR_DIRECTORY = "_bmad-output/implementation-artifacts/evidence/story-4-15/successors/sdk-10.0.400-xunit4-mtp"
SUCCESSOR_SELECTOR_PATH = "_bmad-output/implementation-artifacts/4-15-oq8-platform-closure-successor.json"
FOCUSED_METHOD = "Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.IdempotencyAdmissionOq8PostgresqlTests.ProductionMatrix_IndependentProcessesPreserveAuthorityReplayExpiryAndLeakageInvariants"
FOCUSED_TRAITS = {
    "Category": ["LiveSidecar"],
    "Profile": [PROFILE],
}
FOCUSED_LABELS = {
    "Category": "LiveSidecar",
    "Profile": PROFILE,
}
FOCUSED_LEGACY_COMMAND = f"dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method {FOCUSED_METHOD} -noColor -ctrf raw-runner-temp"
FOCUSED_CURRENT_COMMAND = f"dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method {FOCUSED_METHOD} -noColor -result-ctrf raw-runner-temp"
SUPPORT_LEGACY_COMMAND = "dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -method (21 validator-pinned selectors) -noColor -ctrf raw-runner-temp"
SUPPORT_CURRENT_COMMAND = "dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -method (21 validator-pinned selectors) -noColor -result-ctrf raw-runner-temp"
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
EXPECTED_CANDIDATE_FILES = {
    ".github/workflows/integration.yml",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/IdempotencyAdmissionOq8PostgresqlTests.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/AssemblyInfo.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8AdmissionSnapshot.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8BoundaryCounterStartupFilter.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8BoundedLog.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8CommandObservation.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8FileTimeProvider.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8HostingStartup.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlSnapshot.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8ProcessNode.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8RotatedAuthoritySnapshot.cs",
    "tools/validate-oq8-platform-evidence.py",
}
EXPECTED_CAPTURE_PATHS = EXPECTED_CANDIDATE_FILES | EXPECTED_SOURCE_INPUTS
EXPECTED_EVOLVED_PATHS = {
    ".github/workflows/integration.yml",
    "tools/validate-oq8-platform-evidence.py",
}
EXPECTED_CURRENT_BOUND_PATHS = EXPECTED_CAPTURE_PATHS - EXPECTED_EVOLVED_PATHS
REPLACED_PRIOR_BOUND_PATHS = {
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/AssemblyInfo.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs",
}
SUCCESSOR_SOURCE_PATHS = {
    ".github/workflows/ci.yml",
    ".github/workflows/integration.yml",
    "docs/ci.md",
    "global.json",
    "tests/Directory.Build.props",
    "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs",
    "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/AssemblyInfo.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DockerPublishedPortResolver.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DockerPublishedPortResolverTests.cs",
    "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs",
    "tools/validate-oq8-platform-evidence.py",
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
CLOSURE_FILES = {
    "capture-packet-v1.json",
    "closure-crosswalk.json",
    "limitations.json",
    "review-subject.json",
    "reviews/architecture.json",
    "reviews/security.json",
    "reviews/test.json",
    "source-artifact-identity.json",
    "source-only-handoff.json",
    "pre-review-execution.json",
    "validator-sha256.txt",
}
SUCCESSOR_FILES = {
    "review-subject.json",
    "reviews/architecture.json",
    "reviews/security.json",
    "reviews/test.json",
    "source-artifact-identity.json",
    "source-only-handoff.json",
}
REVIEW_ROSTER = {
    "architecture": "Winston (System Architect)",
    "security": "Security Reviewer",
    "test": "Murat (Test Architect)",
}
REVIEW_SCOPES = {
    "architecture": "OQ8 design reference, invariant crosswalk, landed-source identity, architecture boundaries, and source-only handoff",
    "security": "protected-data leakage gates, current-fence evidence, sanitized-state limitation, and external authority exclusions",
    "test": "production/deterministic evidence coverage, commands and counts, negative validation, and matrix coverage",
}
SUCCESSOR_REVIEW_SCOPES = {
    "architecture": "SDK 10.0.400/MTP source rebinding, prior-seal retention, Linux control-plane discovery, and source-only authority boundaries",
    "security": "successor identity integrity, immutable prior evidence, bounded Docker port parsing, dependency probing, and external authority exclusions",
    "test": "xUnit 4 serialization metadata, maintained MTP coverage, focused resolver/production evidence, and fail-closed successor selection",
}
SUCCESSOR_REVIEW_FINDINGS = {
    "architecture": [
        "The successor is additive and selects exact current source bytes without changing or deleting the original Story 4.15 packet, manifest, or artifacts.",
        "The xUnit 4 assembly metadata and Linux-published control-plane port adaptation preserve serialized production evidence behavior under SDK 10.0.400.",
    ],
    "security": [
        "Docker port discovery accepts one consistent published TCP port and fails closed on absent, malformed, or conflicting mappings.",
        "The EventStore-owned NuGet package-cache probing path is bound here without granting package, shared-workflow, release, or external-repository authority.",
    ],
    "test": [
        "Nine focused Docker published-port resolver cases and the production OQ8 case passed with no failures or skips.",
        "The EventStore-owned MTP workflow integration and xUnit ParallelMode.None source are content-bound; external Builds catalog and reusable-workflow authority remain excluded.",
    ],
}
EXTERNAL_AUTHORITY_FIELDS = {
    "releaseApproved",
    "foldersFinalClosure",
    "packageAuthority",
    "registryAuthority",
    "deploymentAuthority",
    "runtimePinAuthority",
    "consumerMigrationAuthority",
    "externalRepositoryAuthority",
    "finalConsumerAuthority",
}
EXPECTED_DOCUMENT_MARKER = "OQ8-SOURCE-ONLY-HANDOFF"
EXPECTED_DOCUMENTS = {
    "docs/concepts/command-lifecycle.md",
    "docs/concepts/architecture-overview.md",
    "docs/reference/command-api.md",
    "docs/guides/configuration-reference.md",
}
EXPECTED_DOCUMENT_HASHES = {
    "docs/concepts/architecture-overview.md": "8eb99ee1053be809e9e0b136d183a9ad3a591f766f82247a2109e344027043db",
    "docs/concepts/command-lifecycle.md": "c82edf5422be21e096afaecef0bd254f28f924fb5557c73b6fc22d9da7334cd0",
    "docs/guides/configuration-reference.md": "e2fde4db539fc2fadfa545394cb81cabc270bd37bba4f3cce2687e25407f8f0d",
    "docs/reference/command-api.md": "6b0bfd403d371c1278ad0e09516cfd995b26e8de98f3c85c9e1a20d68d5cc821",
}
DOCUMENT_REQUIRED_TEXT = (
    "reviewed source-only handoff",
    LANDED_SOURCE,
    "approved Folders design bytes are not tracked here",
    "test-only deterministic-time, intent-adapter, and boundary-counter seams",
    "raw PostgreSQL values and diagnostics were replaced by sanitized structural projections",
    "original dirty candidate capture is independently rebound to the 26-path landed source",
    "no release approval",
    "Folders final closure",
    "package or registry authority",
    "deployment authority",
    "runtime-pin authority",
    "consumer-migration authority",
    "external-repository authority",
    "final-consumer authority",
    "fresh content-bound architecture, security, and test receipts",
    "EventStore platform completion and the source-only handoff are recorded",
    "only while",
    "python3 -m venv .oq8-python",
    ".oq8-python/bin/python -m pip install --requirement requirements-oq8.txt",
    ".oq8-python/bin/python tools/validate-oq8-platform-evidence.py",
)
DOCUMENT_FORBIDDEN_TEXT = (
    "source-only handoff candidate",
    "are required and are not yet recorded",
    "only after those receipts",
)
CLOSURE_TEST_SOURCE = "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs"
PRIOR_ROOT_BINDING_HASHES = {
    ".github/workflows/ci.yml": "6a28bd968ad3c865226e3a0c2bccdd75f520ac5455b887f5b7efdaa3b1c0bcce",
    ".github/workflows/integration.yml": "343163fd164bb49252ad2ec67c7fbc90aa2f3aaecafa4d4d51640ccc39e7b777",
    "requirements-oq8.txt": "0969da99a0bc2a1b71ed50584560f4588a37567ac63af3ddbaf3c4617ca5621a",
    "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs": "4085ae558bad0eed21759a3e4f561e35710f3cd3375705f4041b0089083e83e3",
    "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs": "f30ed72b844b845c04509eb3dddf07ea6cac38571c0998b57687f4b2e3568fe1",
    "tools/validate-oq8-platform-evidence.py": PRIOR_VALIDATOR_SHA256,
}
PRE_REVIEW_EXECUTION = "pre-review-execution.json"
EXPECTED_LIMITATIONS = [
    "The approved Folders design bytes are not tracked in EventStore; only design version 1.0.0 and its approved SHA-256 reference are preserved.",
    "The production-path capture used shipped Release entry assemblies with a test-only hosting startup, deterministic time, trusted intent adapter, boundary counter, and Testing environment disclosure.",
    "Raw PostgreSQL values and raw diagnostics were intentionally excluded; committed evidence contains sanitized structural projections, bounded counts, hashes, and invariant results.",
    "The original capture bound a dirty candidate against e60a3777c581d70b62f67173ccc2372b5b64a425; this closure independently binds the same 26 paths at landed commit 5e8f175b2ced4715f7c6f765386812cc1001dbb4.",
    "This is a source-only EventStore platform handoff. It grants no release, package, registry, deployment, runtime pin, external-repository, consumer-migration, or final-consumer authority.",
    "Folders retains its own final cross-repository decision and must independently verify the referenced design and EventStore source before consumption.",
]
PRE_REVIEW_TEST_DLL = "tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll"
PRE_REVIEW_TEST_CLASS = "Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests"
PRE_REVIEW_COMMAND_RESULTS = [
    {
        "name": "oq8-validator-dependency",
        "command": "python3 -m venv /tmp/hexalith-eventstore-oq8-python && /tmp/hexalith-eventstore-oq8-python/bin/python -m pip install --requirement requirements-oq8.txt && /tmp/hexalith-eventstore-oq8-python/bin/python -c \"import importlib.metadata, pathlib, yaml; distribution = importlib.metadata.distribution('PyYAML'); expected = pathlib.Path(distribution.locate_file('yaml/__init__.py')).resolve(); actual = pathlib.Path(yaml.__file__).resolve(); assert distribution.version == yaml.__version__ == '6.0.3' and actual == expected\"",
        "exitCode": 0,
        "result": "passed",
    },
    {
        "name": "validator-syntax",
        "command": "python3 -m py_compile tools/validate-oq8-platform-evidence.py",
        "exitCode": 0,
        "result": "passed",
    },
    {
        "name": "contracts-build",
        "command": "dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1",
        "exitCode": 0,
        "result": "passed",
        "warnings": 0,
        "errors": 0,
    },
    *[
        {
            "name": name,
            "command": f"dotnet {PRE_REVIEW_TEST_DLL} -method {PRE_REVIEW_TEST_CLASS}.{method} -noColor",
            "exitCode": 0,
            "result": "passed",
            "tests": tests,
            "passed": tests,
            "failed": 0,
            "skipped": 0,
        }
        for name, method, tests in (
            ("candidate-semantic-mutations", "CandidateSemanticMutationsFailClosed", 49),
            ("consumer-bootstrap", "SourceOnlyConsumerBootstrapInstructionsAreExactAndOrdered", 1),
            ("invalid-pyyaml-dependencies", "InvalidPyYamlDependenciesFailClosed", 4),
            ("hostile-duplicate-json", "HostileDuplicateJsonKeyIsBoundedAndRedacted", 1),
            ("candidate-limitations", "CandidateLimitationTextIsExact", 6),
            ("candidate-authority", "CandidateExternalAuthorityFailsClosed", 9),
            ("candidate-lifecycle", "RequiredSprintStatusMustBeUnique", 9),
            ("retired-lifecycle-yaml-shapes", "RetiredSprintStatusYamlShapesFailClosed", 19),
            ("supported-active-lifecycle-yaml", "SupportedActiveSprintStatusYamlPasses", 4),
            ("unsupported-active-lifecycle-yaml", "UnsupportedActiveSprintStatusYamlFailsClosed", 17),
            ("retired-lifecycle-scoping", "RetiredSprintStatusTextOutsideExactDirectEntryPasses", 12),
            ("unsupported-lifecycle-yaml", "UnsupportedSprintStatusYamlFailsClosed", 72),
            ("missing-active-lifecycle", "MissingRequiredSprintStatusFailsClosed", 1),
            ("duplicate-authority-formatting", "DuplicateAuthorityInjectorSupportsJsonFormatting", 4),
            ("final-lifecycle-review", "FinalLifecycleReviewPassesInIsolation", 1),
            ("final-lifecycle-drift", "FinalLifecycleRequiresSprintReview", 3),
            ("changed-deleted-source", "ChangedOrDeletedBoundCapabilityPathFailsClosed", 2),
            ("hidden-index-flags", "HiddenBoundCapabilityPathFailsClosed", 2),
            ("non-descendant-head", "NonDescendantHeadFailsClosed", 1),
            ("replacement-ref-isolation", "ReplacementRefCannotAlterLandedIdentityProof", 1),
            ("invalid-git-root", "InvalidGitRootFailsSafely", 1),
            ("git-timeout", "GitSubprocessTimeoutFailsSafely", 1),
            ("git-output-limit", "GitSubprocessOutputFloodFailsSafely", 1),
            ("redacted-paths", "CandidatePathFailureIsBoundedAndRedacted", 2),
            ("process-timeout", "ProcessHarnessEnforcesTimeoutWithoutRedirectDeadlock", 1),
            ("dual-stream-drain", "ProcessHarnessDrainsLargeStdoutAndStderr", 1),
            ("runtime-mode-identity", "FreshAndCommittedRuntimeModesRemainExact", 1),
            ("fresh-observation-semantics", "FreshObservationSchemaAndSemanticMutationsFailClosed", 9),
            ("pre-review-mode-exclusivity", "PreReviewModeRejectsCaptureAndSupportArguments", 2),
        )
    ],
]
PRE_REVIEW_FINAL_VALIDATION = {
    "command": f"dotnet {PRE_REVIEW_TEST_DLL} -class {PRE_REVIEW_TEST_CLASS} -noColor",
    "status": "not-run-pre-review",
    "reason": "Final-only receipt, handoff, manifest, and packet tests require three real content-bound review receipts and final assembly.",
}
EXPECTED_CAPTURE_COMMAND_RESULTS = {
    "live-sidecar-release-build": {
        "command": "dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --configuration Release -m:1",
        "counts": {"warnings": 0, "errors": 0},
    },
    "focused-production-matrix": {
        "command": FOCUSED_LEGACY_COMMAND,
        "counts": {"passed": 1, "failed": 0, "skipped": 0},
    },
    "explicit-deterministic-support-oracles": {
        "command": SUPPORT_LEGACY_COMMAND,
        "counts": {"methods": 21, "passed": 33, "failed": 0, "skipped": 0},
    },
    "deterministic-support-lane": {
        "command": "dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --no-build -m:1",
        "counts": {"passed": 3103, "failed": 0, "preExistingSkipped": 25},
    },
    "fresh-capture-validator": {
        "command": "python3 tools/validate-oq8-platform-evidence.py --capture-directory runner-temp/oq8-story-4-14 --ctrf runner-temp/oq8-results.json --support-ctrf runner-temp/oq8-support-results.json",
        "counts": {"validationErrors": 0},
    },
    "committed-packet-validator": {
        "command": "python3 tools/validate-oq8-platform-evidence.py",
        "counts": {"validationErrors": 0},
    },
    "solution-release-build": {
        "command": "dotnet build Hexalith.EventStore.slnx --configuration Release -m:1",
        "counts": {"warnings": 0, "errors": 0},
    },
    "diff-whitespace-gate": {
        "command": "git diff --check",
        "counts": {"errors": 0},
    },
}
EXPECTED_CONSUMER_INSTRUCTIONS = {
    "mode": "source-only",
    "installCommand": "python3 -m venv .oq8-python && .oq8-python/bin/python -m pip install --requirement requirements-oq8.txt",
    "verifyCommand": ".oq8-python/bin/python tools/validate-oq8-platform-evidence.py",
    "designBytesRequiredFromFolders": True,
    "sourcePathRule": "Use only the exact landed EventStore commit after the closure validator passes against unchanged capability paths.",
}
EXPECTED_CROSSWALK_EVIDENCE_HASHES = {
    "_bmad-output/implementation-artifacts/4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence.md": "bc19803ebc7e1f1b6b0e0353560d0e6f9c72815b7e0afba35e94d60a1046d904",
    "_bmad-output/implementation-artifacts/spec-4-11-admission-state-machine-and-current-fence-enforcement.md": "a26090a034e60851b2a518fed6c41de7caf1ecff4931147ff954e4fe45310624",
    "_bmad-output/implementation-artifacts/spec-4-12-expiry-compaction-and-tombstone-retention.md": "df48f3bc6dfef608190a640193182e07c514798676abd0f50967207f219e652a",
    "_bmad-output/implementation-artifacts/spec-4-13-legacy-admission-migration-and-fail-closed-reconciliation.md": "7afed1dcb5f0fdf8e7aba12ae3a52c2a4dd13f438d05f85a500e5ee6e0441128",
    "_bmad-output/implementation-artifacts/spec-4-14-oq8-multi-host-production-evidence.md": "4e6956baac6ff79c9032d57e46ce08f6f0217c63f51523e942c5eb62d9439c8e",
    f"{EVIDENCE_DIRECTORY}/deterministic-support.json": "de2f76f2662574595c2db3b23af51bd06a7f1f9d194553e748cb1e2c83a238ae",
    f"{EVIDENCE_DIRECTORY}/environment.json": "2981c6dd2ad81fbde6ece41f2b9dcf1c9f3f26a84d6a7e128a0c50a94e8c7254",
    f"{EVIDENCE_DIRECTORY}/evidence-sha256.txt": "02c3f50778e4b6b2cc2ea422ad00c149884955a113cd6bce9f8c80b24dc1d1fc",
    f"{EVIDENCE_DIRECTORY}/observations.json": "7444f4a696e52bbb49a624f576fad8d577a630ca32b57b0fb6eb35b618e7c701",
}
EXPECTED_CROSSWALK_INVARIANTS = [
    {
        "id": "OQ8-1",
        "name": "trusted-admission",
        "stories": ["4.9"],
        "evidence": [
            "_bmad-output/implementation-artifacts/4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence.md",
            f"{EVIDENCE_DIRECTORY}/deterministic-support.json",
        ],
        "result": "approved",
    },
    {
        "id": "OQ8-2",
        "name": "tenant-key-identity",
        "stories": ["4.9", "4.10"],
        "evidence": [
            "_bmad-output/implementation-artifacts/4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence.md",
            f"{EVIDENCE_DIRECTORY}/observations.json",
        ],
        "result": "approved",
    },
    {
        "id": "OQ8-3",
        "name": "protected-key-handling",
        "stories": ["4.9", "4.14"],
        "evidence": [f"{EVIDENCE_DIRECTORY}/observations.json", f"{EVIDENCE_DIRECTORY}/environment.json"],
        "result": "approved",
    },
    {
        "id": "OQ8-4",
        "name": "atomic-state-and-current-fence",
        "stories": ["4.10", "4.11", "4.14"],
        "evidence": [
            "_bmad-output/implementation-artifacts/spec-4-11-admission-state-machine-and-current-fence-enforcement.md",
            f"{EVIDENCE_DIRECTORY}/deterministic-support.json",
            f"{EVIDENCE_DIRECTORY}/observations.json",
        ],
        "result": "approved",
    },
    {
        "id": "OQ8-5",
        "name": "replay-conflict-and-inclusive-expiry",
        "stories": ["4.11", "4.12", "4.14"],
        "evidence": [
            "_bmad-output/implementation-artifacts/spec-4-12-expiry-compaction-and-tombstone-retention.md",
            f"{EVIDENCE_DIRECTORY}/observations.json",
        ],
        "result": "approved",
    },
    {
        "id": "OQ8-6",
        "name": "retention-and-governed-tombstones",
        "stories": ["4.12", "4.14"],
        "evidence": [
            "_bmad-output/implementation-artifacts/spec-4-12-expiry-compaction-and-tombstone-retention.md",
            f"{EVIDENCE_DIRECTORY}/observations.json",
        ],
        "result": "approved",
    },
    {
        "id": "OQ8-7",
        "name": "fail-closed-recovery-and-migration",
        "stories": ["4.11", "4.13", "4.14"],
        "evidence": [
            "_bmad-output/implementation-artifacts/spec-4-13-legacy-admission-migration-and-fail-closed-reconciliation.md",
            f"{EVIDENCE_DIRECTORY}/deterministic-support.json",
        ],
        "result": "approved",
    },
    {
        "id": "OQ8-8",
        "name": "multi-host-production-evidence",
        "stories": ["4.14"],
        "evidence": [
            "_bmad-output/implementation-artifacts/spec-4-14-oq8-multi-host-production-evidence.md",
            f"{EVIDENCE_DIRECTORY}/evidence-sha256.txt",
        ],
        "result": "approved",
    },
]
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


def display_path(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.name


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except (OSError, UnicodeError):
        fail(f"Cannot read evidence path {display_path(path)}")


def reject_duplicate_json_fields(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    document: dict[str, Any] = {}
    for name, value in pairs:
        require(name not in document, "Duplicate JSON field")
        document[name] = value
    return document


def reject_non_finite_json_constant(value: str) -> Any:
    fail(f"Non-finite JSON constant is forbidden: {value}")


def load_json(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8") as stream:
            return json.load(
                stream,
                object_pairs_hook=reject_duplicate_json_fields,
                parse_constant=reject_non_finite_json_constant,
            )
    except (OSError, UnicodeError, json.JSONDecodeError):
        fail(f"Cannot load JSON evidence {display_path(path)}")


def scan_json_protected_content(value: Any) -> None:
    if isinstance(value, dict):
        for name, nested in value.items():
            scan_json_protected_content(name)
            scan_json_protected_content(nested)
        return
    if isinstance(value, list):
        for nested in value:
            scan_json_protected_content(nested)
        return
    if not isinstance(value, str):
        return
    require(PRIVATE_PATH_RE.search(value) is None, "Candidate JSON contains forbidden private-path content")
    lowered = value.lower()
    require(
        all(term.lower() not in lowered for term in FORBIDDEN_CAPTURE_TERMS),
        "Candidate JSON contains forbidden protected content",
    )


def load_candidate_json(path: Path) -> Any:
    document = load_json(path)
    scan_json_protected_content(document)
    return document


def write_json(path: Path, value: Any) -> None:
    path.mkdir(parents=True, exist_ok=True) if path.suffix == "" else path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(f"{path.suffix}.tmp")
    with temporary.open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, indent=2, sort_keys=False)
        stream.write("\n")
    temporary.replace(path)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(chunk)
    except OSError:
        fail(f"Cannot hash evidence path {display_path(path)}")
    return digest.hexdigest()


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def run_subprocess_bounded(command: list[str], label: str) -> tuple[int, bytes, bytes]:
    try:
        process = subprocess.Popen(
            command,
            cwd=GIT_ROOT,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
    except OSError:
        fail(f"{label} could not start")

    require(process.stdout is not None and process.stderr is not None, f"{label} output capture failed")
    selector = selectors.DefaultSelector()
    output = bytearray()
    errors = bytearray()
    deadline = time.monotonic() + GIT_TIMEOUT_SECONDS
    try:
        for stream, destination in ((process.stdout, output), (process.stderr, errors)):
            os.set_blocking(stream.fileno(), False)
            selector.register(stream, selectors.EVENT_READ, destination)

        while selector.get_map():
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                process.kill()
                process.wait()
                fail(f"{label} timed out")
            for key, _ in selector.select(min(remaining, 0.1)):
                try:
                    chunk = os.read(key.fileobj.fileno(), 8192)
                except BlockingIOError:
                    continue
                if not chunk:
                    selector.unregister(key.fileobj)
                    continue
                key.data.extend(chunk)
                if len(output) + len(errors) > GIT_OUTPUT_LIMIT_BYTES:
                    process.kill()
                    process.wait()
                    fail(f"{label} exceeded output limit")

        remaining = deadline - time.monotonic()
        if remaining <= 0:
            process.kill()
            process.wait()
            fail(f"{label} timed out")
        try:
            return_code = process.wait(timeout=remaining)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
            fail(f"{label} timed out")
    except EvidenceError:
        if process.poll() is None:
            process.kill()
            process.wait()
        raise
    except OSError:
        if process.poll() is None:
            process.kill()
            process.wait()
        fail(f"{label} failed safely")
    finally:
        selector.close()
        process.stdout.close()
        process.stderr.close()

    return return_code, bytes(output), bytes(errors)


def run_git(*arguments: str) -> bytes:
    label = f"Git identity proof for {' '.join(arguments[:2])}"
    return_code, output, _ = run_subprocess_bounded(
        ["git", "--no-replace-objects", *arguments],
        label,
    )
    require(return_code == 0, f"Git identity proof failed for {' '.join(arguments[:2])}")
    return output


def git_diff_is_clean(*arguments: str) -> bool:
    return_code, _, _ = run_subprocess_bounded(
        ["git", "--no-replace-objects", *arguments],
        "Git current-bound-source proof",
    )
    require(return_code in (0, 1), "Git current-bound-source proof failed")
    return return_code == 0


def git_file(revision: str, relative: str) -> bytes:
    require(
        isinstance(relative, str)
        and relative
        and not Path(relative).is_absolute()
        and ".." not in Path(relative).parts,
        "Unsafe Git-bound path",
    )
    return run_git("show", f"{revision}:{relative}")


def sha256_git_file(revision: str, relative: str) -> str:
    require(
        isinstance(relative, str)
        and relative
        and not Path(relative).is_absolute()
        and ".." not in Path(relative).parts,
        "Unsafe Git-bound path",
    )
    label = "Git historical-blob identity proof"
    try:
        process = subprocess.Popen(
            ["git", "--no-replace-objects", "show", f"{revision}:{relative}"],
            cwd=GIT_ROOT,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
    except OSError:
        fail(f"{label} could not start")

    require(process.stdout is not None and process.stderr is not None, f"{label} output capture failed")
    selector = selectors.DefaultSelector()
    digest = hashlib.sha256()
    errors = bytearray()
    blob_size = 0
    deadline = time.monotonic() + GIT_TIMEOUT_SECONDS
    try:
        for stream, kind in ((process.stdout, "blob"), (process.stderr, "error")):
            os.set_blocking(stream.fileno(), False)
            selector.register(stream, selectors.EVENT_READ, kind)
        while selector.get_map():
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                process.kill()
                process.wait()
                fail(f"{label} timed out")
            for key, _ in selector.select(min(remaining, 0.1)):
                try:
                    chunk = os.read(key.fileobj.fileno(), 8192)
                except BlockingIOError:
                    continue
                if not chunk:
                    selector.unregister(key.fileobj)
                    continue
                if key.data == "blob":
                    blob_size += len(chunk)
                    require(blob_size <= GIT_BLOB_LIMIT_BYTES, f"{label} exceeded blob limit")
                    digest.update(chunk)
                else:
                    errors.extend(chunk)
                    require(len(errors) <= GIT_OUTPUT_LIMIT_BYTES, f"{label} exceeded output limit")
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            process.kill()
            process.wait()
            fail(f"{label} timed out")
        try:
            return_code = process.wait(timeout=remaining)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
            fail(f"{label} timed out")
    except EvidenceError:
        if process.poll() is None:
            process.kill()
            process.wait()
        raise
    except OSError:
        if process.poll() is None:
            process.kill()
            process.wait()
        fail(f"{label} failed safely")
    finally:
        selector.close()
        process.stdout.close()
        process.stderr.close()

    require(return_code == 0, f"{label} failed")
    return digest.hexdigest()


def configure_roots(root: Path, git_root: Path, git_timeout_seconds: float = 30.0) -> None:
    global ROOT, GIT_ROOT, GIT_TIMEOUT_SECONDS, PACKET, EVIDENCE, CLOSURE, SUCCESSOR_SELECTOR, SUCCESSOR
    require(0 < git_timeout_seconds <= 30, "Git timeout must be greater than zero and no more than 30 seconds")
    ROOT = root.resolve()
    GIT_ROOT = git_root.resolve()
    GIT_TIMEOUT_SECONDS = git_timeout_seconds
    PACKET = ROOT / "_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml"
    EVIDENCE = (
        ROOT
        / "_bmad-output/implementation-artifacts/evidence/story-4-14"
        / BASELINE
    )
    CLOSURE = (
        ROOT
        / "_bmad-output/implementation-artifacts/evidence/story-4-15"
        / LANDED_SOURCE
    )
    SUCCESSOR_SELECTOR = ROOT / SUCCESSOR_SELECTOR_PATH
    SUCCESSOR = ROOT / SUCCESSOR_DIRECTORY


def require(condition: bool, message: str) -> None:
    if not condition:
        fail(message)


def scan_support_safe(path: Path) -> None:
    text = read_text(path)
    require(not PRIVATE_PATH_RE.search(text), f"Private path found in {path.name}")
    require(not PLACEHOLDER_RE.search(text), f"Placeholder found in {path.name}")
    require(not FORBIDDEN_CLAIM_RE.search(text), f"Closure/release claim found in {path.name}")
    for term in FORBIDDEN_CAPTURE_TERMS:
        require(term.lower() not in text.lower(), f"Protected or secret-like term found in {path.name}")


def require_sha256(value: Any, field: str) -> str:
    require(isinstance(value, str) and SHA256_RE.fullmatch(value) is not None, f"{field} is not SHA-256")
    return value


def require_exact_integer(value: Any, expected: int, field: str) -> None:
    require(type(value) is int, f"{field} must be an exact integer")
    require(value == expected, f"{field} count drift")


def require_nonnegative_integer(value: Any, field: str) -> int:
    require(type(value) is int, f"{field} must be a non-negative integer")
    require(value >= 0, f"{field} must be a non-negative integer")
    return value


def require_exact_fields(value: Any, fields: set[str], label: str) -> dict[str, Any]:
    require(isinstance(value, dict) and set(value) == fields, f"{label} field set drift")
    return value


def validate_observations(path: Path, expected_dapr_runtime_version: str) -> dict[str, Any]:
    require(
        re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", expected_dapr_runtime_version) is not None,
        "Expected Dapr runtime version must be one exact semantic version",
    )
    scan_support_safe(path)
    document = load_json(path)
    require(isinstance(document, dict), "observations.json must be an object")
    require_exact_fields(
        document,
        {"schemaVersion", "captureKind", "capturedOn", "topology", "profile", "runtime", "executionConfiguration", "artifacts", "diagnostics", "observations"},
        "Observation",
    )
    require(document.get("schemaVersion") == 1, "Observation schemaVersion drift")
    require(document.get("captureKind") == "release-entry-binaries-test-seams-sidecar-postgresql", "Observation capture kind drift")
    require(re.fullmatch(r"\d{4}-\d{2}-\d{2}", str(document.get("capturedOn", ""))) is not None, "Capture date missing")

    topology = document.get("topology", {})
    require_exact_fields(topology, {"eventStoreProcessCount", "eventStoreSidecarCount", "sampleProcessCount", "sampleSidecarCount", "independentProcessIdentities"}, "Observation topology")
    for field in ("eventStoreProcessCount", "eventStoreSidecarCount", "sampleProcessCount", "sampleSidecarCount"):
        require_nonnegative_integer(topology.get(field), f"Observation topology {field}")
    require(topology.get("eventStoreProcessCount") == 2, "Two EventStore processes were not observed")
    require(topology.get("eventStoreSidecarCount") == 2, "Two EventStore sidecars were not observed")
    require(topology.get("sampleProcessCount") == 1, "The Sample process was not observed")
    require(topology.get("sampleSidecarCount") == 1, "The Sample sidecar was not observed")
    require(topology.get("independentProcessIdentities") is True, "Process identities were not independent")

    profile = document.get("profile", {})
    require_exact_fields(profile, {"name", "stateStoreType", "stateComponentSha256", "resiliencySha256"}, "Observation profile")
    require(profile.get("name") == PROFILE, "OQ8 profile drift")
    require(profile.get("stateStoreType") == "state.postgresql", "State store is not PostgreSQL")
    require_sha256(profile.get("stateComponentSha256"), "state component identity")
    require_sha256(profile.get("resiliencySha256"), "resiliency identity")

    runtime = document.get("runtime", {})
    require_exact_fields(runtime, {"dotnet", "dapr", "postgresImage", "postgresImageIdentity"}, "Observation runtime")
    require(isinstance(runtime.get("dotnet"), str) and runtime["dotnet"], ".NET runtime identity missing")
    require(runtime.get("dapr") == expected_dapr_runtime_version, "Dapr runtime identity drift")
    require(runtime.get("postgresImage") == POSTGRES_IMAGE, "PostgreSQL image tag drift")
    require(
        isinstance(runtime.get("postgresImageIdentity"), str)
        and re.fullmatch(r"sha256:[0-9a-f]{64}", runtime["postgresImageIdentity"]) is not None,
        "PostgreSQL immutable identity must be an exact sha256 digest",
    )

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
    require_exact_fields(diagnostics, {"streamsScanned", "boundedCharacterLimitPerStream", "forbiddenTermClassesScanned", "postRedactionProtectedMatches", "rawDiagnosticsCommitted", "sanitizedProjectionSha256"}, "Observation diagnostics")
    for field in ("streamsScanned", "boundedCharacterLimitPerStream", "postRedactionProtectedMatches"):
        require_nonnegative_integer(diagnostics.get(field), f"Observation diagnostics {field}")
    require(diagnostics.get("streamsScanned") == 12, "All bounded process diagnostic streams were not scanned")
    require(diagnostics.get("boundedCharacterLimitPerStream") == 32768, "Diagnostic stream bound drift")
    require(diagnostics.get("forbiddenTermClassesScanned") == DIAGNOSTIC_FORBIDDEN_CLASSES, "Diagnostic forbidden-term class coverage drift")
    require(diagnostics.get("postRedactionProtectedMatches") == 0, "Protected diagnostics remain after redaction")
    require(diagnostics.get("rawDiagnosticsCommitted") is False, "Raw diagnostics were committed")
    require_sha256(diagnostics.get("sanitizedProjectionSha256"), "Sanitized diagnostic projection identity")

    observations = document.get("observations", {})
    require(set(observations) == {"writers_failover", "expiry_compaction", "authority_change", "capture"}, "Observation matrix is incomplete")
    writers = observations["writers_failover"]
    require_exact_fields(writers, {"concurrentRequests", "canonicalExecutionIdentities", "durableFencePositive", "sampleExecutions", "ownerStoppedAtTerminalBoundary", "failoverAttempts", "failoverReplayExact", "restartedNodeReplayExact", "conflictStatus", "crossTargetConflictStatus", "nonExecuteAdditionalWork"}, "Writer/failover observation")
    for field in ("concurrentRequests", "canonicalExecutionIdentities", "sampleExecutions", "failoverAttempts", "nonExecuteAdditionalWork"):
        require_nonnegative_integer(writers.get(field), f"Writer/failover observation {field}")
    require(writers.get("concurrentRequests", 0) >= 2, "Concurrent writer count is insufficient")
    require(writers.get("canonicalExecutionIdentities") == 1, "Canonical execution identity count is not one")
    require(writers.get("durableFencePositive") is True, "Durable positive fence was not observed")
    require(writers.get("sampleExecutions") == 1, "Sample execution count is not one")
    require(writers.get("ownerStoppedAtTerminalBoundary") is True, "Known owner failover was not observed")
    require(writers.get("failoverAttempts", 0) >= 1, "Failover request was not observed")
    require(writers.get("failoverReplayExact") is True, "Failover replay content was not exact")
    require(writers.get("restartedNodeReplayExact") is True, "Restart replay was not exact")
    require_exact_integer(writers.get("conflictStatus"), 409, "Writer/failover observation conflictStatus")
    require_exact_integer(writers.get("crossTargetConflictStatus"), 409, "Writer/failover observation crossTargetConflictStatus")
    require(writers.get("nonExecuteAdditionalWork") == 0, "Writer non-execute path performed work")

    expiry = observations["expiry_compaction"]
    require_exact_fields(expiry, {"oneTickBefore", "oneTickBeforeReplayExact", "inclusiveBoundary", "oneTickAfter", "terminalBecameMinimalTombstone", "equivalentAndDifferentReuseShareOutcome", "nonExecuteAdditionalWork"}, "Expiry observation")
    require_nonnegative_integer(expiry.get("nonExecuteAdditionalWork"), "Expiry observation nonExecuteAdditionalWork")
    require_exact_integer(expiry.get("oneTickBefore"), 202, "Expiry observation oneTickBefore")
    require(expiry.get("oneTickBeforeReplayExact") is True, "T-1 replay content was not exact")
    require_exact_integer(expiry.get("inclusiveBoundary"), 409, "Expiry observation inclusiveBoundary")
    require_exact_integer(expiry.get("oneTickAfter"), 409, "Expiry observation oneTickAfter")
    require(expiry.get("terminalBecameMinimalTombstone") is True, "Expiry did not compact atomically")
    require(expiry.get("equivalentAndDifferentReuseShareOutcome") is True, "Expired reuse outcomes diverged")
    require(expiry.get("nonExecuteAdditionalWork") == 0, "Expiry non-execute path performed work")

    authority = observations["authority_change"]
    require_exact_fields(authority, {"rotationReplayExact", "canonicalAuthorityCount", "retiredReaderReplayExact", "legalHoldState", "releasedState", "failClosedStatuses", "sampleExecutions", "nonExecuteAdditionalWork", "deterministicSupportOracles"}, "Authority-change observation")
    for field in ("canonicalAuthorityCount", "sampleExecutions", "nonExecuteAdditionalWork"):
        require_nonnegative_integer(authority.get(field), f"Authority-change observation {field}")
    require(authority.get("rotationReplayExact") is True, "Rotation replay was not exact")
    require(authority.get("canonicalAuthorityCount") == 1, "Rotated canonical authority count is not one")
    require(authority.get("retiredReaderReplayExact") is True, "Retired-reader replay was not exact")
    require(authority.get("legalHoldState") == "LegalHold", "Legal hold was not serialized")
    require(authority.get("releasedState") == "Retaining", "Hold release did not restore retaining state")
    fail_closed_statuses = authority.get("failClosedStatuses")
    require(
        isinstance(fail_closed_statuses, list)
        and all(type(status) is int for status in fail_closed_statuses)
        and fail_closed_statuses == [503, 503],
        "Governance unavailable states did not fail closed",
    )
    require(authority.get("sampleExecutions") == 2, "Authority-change eligible execution count is not two")
    require(authority.get("nonExecuteAdditionalWork") == 0, "Authority non-execute path performed work")
    require(
        authority.get("deterministicSupportOracles") == list(EXPECTED_SUPPORT_METHOD_CASES),
        "Deterministic support oracle identities or order drifted",
    )

    capture = observations["capture"]
    require_exact_fields(capture, {"before", "after", "protectedSentinelMatches", "committedProjectionContainsIdentifiers", "closureClaimed"}, "Capture observation")
    before = capture.get("before", {})
    after = capture.get("after", {})
    capture_snapshot_fields = {"stage", "schemaSha256", "projectionSha256", "totalRows", "admissionRows", "terminalRows", "tombstoneRows", "minimalTombstoneRows", "directoryRows", "lifecycleRows", "aggregateMetadataRows", "aggregateEventRows", "aggregateSequenceTotal", "protectedSentinelMatches"}
    require_exact_fields(before, capture_snapshot_fields, "Before capture snapshot")
    require_exact_fields(after, capture_snapshot_fields, "After capture snapshot")
    capture_snapshot_counters = capture_snapshot_fields - {"stage", "schemaSha256", "projectionSha256"}
    for label, snapshot in (("Before", before), ("After", after)):
        for field in capture_snapshot_counters:
            require_nonnegative_integer(snapshot.get(field), f"{label} capture snapshot {field}")
    require_nonnegative_integer(capture.get("protectedSentinelMatches"), "Capture observation protectedSentinelMatches")
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
    require(
        before.get("protectedSentinelMatches") == 0
        and after.get("protectedSentinelMatches") == 0
        and capture.get("protectedSentinelMatches") == 0,
        "Protected sentinel leakage detected",
    )
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
    require(test.get("labels") == FOCUSED_LABELS, "Focused CTRF xUnit 4 Category/Profile labels drift")
    require(test.get("tags") == ["LiveSidecar"], "Focused CTRF xUnit 4 Category tag drift")
    portable = {
        "schemaVersion": 1,
        "runner": "xUnit.net v3",
        "command": FOCUSED_CURRENT_COMMAND,
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
            "traits": {name: [value] for name, value in test.get("labels", {}).items()},
        },
    }
    validate_focused_document(portable, FOCUSED_CURRENT_COMMAND)
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


def validate_focused_document(document: Any, expected_command: str = FOCUSED_LEGACY_COMMAND) -> dict[str, Any]:
    require(isinstance(document, dict), "test-results.json must be an object")
    require_exact_fields(document, {"schemaVersion", "runner", "command", "summary", "test"}, "Focused result")
    require(document.get("schemaVersion") == 1, "Focused result schemaVersion drift")
    require(document.get("runner") == "xUnit.net v3", "Focused result runner drift")
    require(
        document.get("command") == expected_command,
        "Focused result command identity drift",
    )
    require(
        document.get("summary") == {"tests": 1, "passed": 1, "failed": 0, "skipped": 0},
        "Focused result is not exactly one green case",
    )
    test = document.get("test", {})
    require(isinstance(test, dict), "Focused result test must be an object")
    require_exact_fields(test, {"name", "status", "durationMilliseconds", "traits"}, "Focused result test")
    require(test.get("name") == FOCUSED_METHOD, "Focused result test identity drift")
    require(test.get("status") == "passed", "Focused test status is not passed")
    require(test.get("traits") == FOCUSED_TRAITS, "Focused result Category/Profile traits drift")
    duration = test.get("durationMilliseconds")
    require(
        (type(duration) is int and duration >= 0)
        or (type(duration) is float and math.isfinite(duration) and duration >= 0),
        "Focused result duration must be a finite non-negative number",
    )
    return document


def validate_support_document(document: Any, expected_command: str = SUPPORT_LEGACY_COMMAND) -> dict[str, Any]:
    require(isinstance(document, dict), "deterministic-support.json must be an object")
    require_exact_fields(document, {"schemaVersion", "runner", "command", "selectors", "summary", "methods", "classifications"}, "Deterministic support")
    require(document.get("schemaVersion") == 1, "Deterministic support schemaVersion drift")
    require(document.get("runner") == "xUnit.net v3", "Deterministic support runner drift")
    require(
        document.get("command") == expected_command,
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
        "command": SUPPORT_CURRENT_COMMAND,
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
    validate_support_document(portable, SUPPORT_CURRENT_COMMAND)
    write_json(destination, portable)
    scan_support_safe(destination)
    return portable


def validate_capture(
    capture_directory: Path,
    ctrf_path: Path,
    support_ctrf_path: Path,
    expected_dapr_runtime_version: str,
) -> None:
    require(capture_directory.is_dir(), "Capture directory is missing")
    require(not ctrf_path.is_relative_to(capture_directory), "Raw focused CTRF must remain outside the capture directory")
    require(not support_ctrf_path.is_relative_to(capture_directory), "Raw support CTRF must remain outside the capture directory")
    require(
        {path.name for path in capture_directory.iterdir()} == {"observations.json"},
        "Fresh capture directory contains unexpected pre-existing files",
    )
    observations_path = capture_directory / "observations.json"
    require(observations_path.is_file(), "Capture observations.json is missing")
    validate_observations(observations_path, expected_dapr_runtime_version)
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
    for line in read_text(manifest_path).splitlines():
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


def validate_successor_source_identity() -> dict[str, Any]:
    require(SUCCESSOR_SELECTOR.is_file(), "Story 4.15 successor selector is missing")
    require(SUCCESSOR.is_dir(), "Story 4.15 successor directory is missing")
    selector = load_candidate_json(SUCCESSOR_SELECTOR)
    require(isinstance(selector, dict), "Story 4.15 successor selector must be an object")
    require(
        selector.get("schema") == "hexalith.eventstore.story-4-15-successor-selection/v1",
        "Story 4.15 successor selector schema drift",
    )
    successor = selector.get("successor")
    require(isinstance(successor, dict), "Story 4.15 successor selection is missing")
    require(successor.get("directory") == SUCCESSOR_DIRECTORY, "Story 4.15 successor directory selection drift")

    identity_path = SUCCESSOR / "source-artifact-identity.json"
    require(identity_path.is_file(), "Story 4.15 successor source identity is missing")
    require(
        successor.get("sourceIdentitySha256") == sha256_file(identity_path),
        "Story 4.15 successor source identity selection drift",
    )
    identity = load_candidate_json(identity_path)
    require(isinstance(identity, dict), "Story 4.15 successor source identity must be an object")
    require(
        set(identity)
        == {
            "schema",
            "reviewedOn",
            "repository",
            "reviewedBaseCommit",
            "priorLandedSource",
            "reason",
            "bindingRule",
            "replacedPriorBoundPaths",
            "boundPaths",
            "validation",
        },
        "Story 4.15 successor source identity field set drift",
    )
    require(
        identity.get("schema") == "hexalith.eventstore.story-4-15-successor-source-identity/v1",
        "Story 4.15 successor source identity schema drift",
    )
    require(identity.get("reviewedOn") == "2026-08-29", "Story 4.15 successor review date drift")
    require(identity.get("repository") == "Hexalith/Hexalith.EventStore", "Story 4.15 successor repository drift")
    require(identity.get("reviewedBaseCommit") == SUCCESSOR_REVIEW_BASE, "Story 4.15 successor review base drift")
    require(identity.get("priorLandedSource") == LANDED_SOURCE, "Story 4.15 successor prior source drift")
    require(
        identity.get("reason")
        == "SDK 10.0.400 requires Microsoft.Testing.Platform, xUnit 4 serialization metadata, and Linux-safe OQ8 control-plane discovery.",
        "Story 4.15 successor reason drift",
    )
    require(
        identity.get("bindingRule")
        == "Every listed current worktree source path must exist as a regular file and match its reviewed SHA-256; reviewedBaseCommit is an ancestry base only and does not claim those bound worktree bytes exist in that commit.",
        "Story 4.15 successor source binding rule drift",
    )
    require(
        identity.get("replacedPriorBoundPaths") == sorted(REPLACED_PRIOR_BOUND_PATHS),
        "Story 4.15 successor replaced-path declaration drift",
    )
    bound_paths = identity.get("boundPaths")
    require(
        isinstance(bound_paths, dict) and set(bound_paths) == SUCCESSOR_SOURCE_PATHS,
        "Story 4.15 successor current source path set drift",
    )
    for relative, expected in bound_paths.items():
        path = Path(relative)
        require(
            not path.is_absolute() and ".." not in path.parts and path.as_posix() == relative,
            "Story 4.15 successor source path is unsafe",
        )
        require_sha256(expected, f"Story 4.15 successor source:{relative}")
        source_path = ROOT / path
        require(source_path.is_file() and not source_path.is_symlink(), f"Story 4.15 successor source path is missing: {relative}")
        require(sha256_file(source_path) == expected, f"Story 4.15 successor current source identity drift: {relative}")
    require(
        identity.get("validation")
        == {
            "sdk": "10.0.400",
            "xunitParallelization": "ParallelMode.None",
            "dockerPublishedControlPlanePorts": True,
            "nugetPackageCacheProbing": True,
            "focusedPortResolverTests": {"total": 9, "passed": 9, "failed": 0, "skipped": 0},
            "focusedProductionOq8": {"total": 1, "passed": 1, "failed": 0, "skipped": 0},
        },
        "Story 4.15 successor validation record drift",
    )
    run_git("merge-base", "--is-ancestor", SUCCESSOR_REVIEW_BASE, "HEAD")
    return identity


def validate_successor_manifest() -> dict[str, str]:
    manifest_path = SUCCESSOR / "successor-sha256.txt"
    require(manifest_path.is_file(), "Story 4.15 successor manifest is missing")
    scan_support_safe(manifest_path)
    manifest: dict[str, str] = {}
    lines = read_text(manifest_path).splitlines()
    require(lines == sorted(lines, key=lambda line: line.split("  ", 1)[-1]), "Story 4.15 successor manifest is not path-sorted")
    for line in lines:
        parts = line.split("  ", 1)
        require(len(parts) == 2 and SHA256_RE.fullmatch(parts[0]) is not None, "Malformed Story 4.15 successor manifest line")
        digest, relative = parts
        path = Path(relative)
        require(
            relative not in manifest
            and not path.is_absolute()
            and ".." not in path.parts
            and path.as_posix() == relative,
            "Unsafe or duplicate Story 4.15 successor manifest path",
        )
        manifest[relative] = digest
    require(set(manifest) == SUCCESSOR_FILES, "Story 4.15 successor manifest file set drift")
    for relative, expected in manifest.items():
        artifact = SUCCESSOR / relative
        require(artifact.is_file(), f"Story 4.15 successor artifact missing: {relative}")
        require(not artifact.is_symlink(), f"Story 4.15 successor artifact cannot be a symlink: {relative}")
        require(sha256_file(artifact) == expected, f"Story 4.15 successor checksum mismatch: {relative}")
        scan_support_safe(artifact)
    return manifest


def validate_successor_review_subject(subject: Any, identity: dict[str, Any]) -> str:
    require(isinstance(subject, dict), "Story 4.15 successor review subject must be an object")
    require(
        set(subject)
        == {
            "schema",
            "reviewedOn",
            "reason",
            "priorSeal",
            "sourceIdentity",
            "validation",
            "requiredReviews",
            "authority",
        },
        "Story 4.15 successor review subject field set drift",
    )
    require(
        subject.get("schema") == "hexalith.eventstore.story-4-15-successor-review-subject/v1",
        "Story 4.15 successor review subject schema drift",
    )
    require(subject.get("reviewedOn") == "2026-08-29", "Story 4.15 successor review subject date drift")
    require(subject.get("reason") == identity.get("reason"), "Story 4.15 successor review subject reason drift")
    require(
        subject.get("priorSeal")
        == {
            "packetPath": "_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml",
            "packetSha256": PRIOR_PACKET_SHA256,
            "closureDirectory": CLOSURE_DIRECTORY,
            "closureManifestSha256": PRIOR_CLOSURE_MANIFEST_SHA256,
        },
        "Story 4.15 successor prior-seal binding drift",
    )
    require(
        subject.get("sourceIdentity")
        == {
            "path": "source-artifact-identity.json",
            "sha256": sha256_file(SUCCESSOR / "source-artifact-identity.json"),
            "reviewedBaseCommit": SUCCESSOR_REVIEW_BASE,
            "boundPathCount": len(SUCCESSOR_SOURCE_PATHS),
        },
        "Story 4.15 successor review source identity drift",
    )
    require(subject.get("validation") == identity.get("validation"), "Story 4.15 successor reviewed validation drift")
    require(
        subject.get("requiredReviews")
        == [
            {
                "role": role,
                "reviewer": REVIEW_ROSTER[role],
                "scope": SUCCESSOR_REVIEW_SCOPES[role],
                "status": "required",
            }
            for role in ("architecture", "security", "test")
        ],
        "Story 4.15 successor required review roster or scope drift",
    )
    validate_authority(subject.get("authority"))
    return sha256_file(SUCCESSOR / "review-subject.json")


def validate_successor_reviews(subject_sha256: str) -> dict[str, str]:
    receipts: dict[str, str] = {}
    for role, reviewer in REVIEW_ROSTER.items():
        path = SUCCESSOR / "reviews" / f"{role}.json"
        document = load_candidate_json(path)
        require(isinstance(document, dict), f"Story 4.15 successor {role} review must be an object")
        require(
            set(document)
            == {
                "schema",
                "role",
                "reviewer",
                "reviewedOn",
                "decision",
                "subjectSha256",
                "acceptedScope",
                "findings",
                "authority",
            },
            f"Story 4.15 successor {role} review field set drift",
        )
        require(
            document.get("schema") == "hexalith.eventstore.story-4-15-successor-review-receipt/v1",
            f"Story 4.15 successor {role} review schema drift",
        )
        require(document.get("role") == role, f"Story 4.15 successor {role} review role drift")
        require(document.get("reviewer") == reviewer, f"Story 4.15 successor {role} reviewer identity drift")
        require(document.get("reviewedOn") == "2026-08-29", f"Story 4.15 successor {role} review date drift")
        require(document.get("decision") == "approved", f"Story 4.15 successor {role} review is not approved")
        require(document.get("subjectSha256") == subject_sha256, f"Story 4.15 successor {role} review subject drift")
        require(document.get("acceptedScope") == SUCCESSOR_REVIEW_SCOPES[role], f"Story 4.15 successor {role} review scope drift")
        require(document.get("findings") == SUCCESSOR_REVIEW_FINDINGS[role], f"Story 4.15 successor {role} review findings drift")
        validate_authority(document.get("authority"))
        receipts[role] = sha256_file(path)
    return receipts


def validate_successor_handoff(
    document: Any,
    subject_sha256: str,
    identity_sha256: str,
    receipts: dict[str, str],
) -> str:
    require(isinstance(document, dict), "Story 4.15 successor handoff must be an object")
    require(
        set(document)
        == {
            "schema",
            "story",
            "selectedSuccessorDirectory",
            "reviewedBaseCommit",
            "reviewSubjectSha256",
            "sourceIdentitySha256",
            "reviewReceipts",
            "consumerInstructions",
            "priorSealRetained",
            "authority",
        },
        "Story 4.15 successor handoff field set drift",
    )
    require(
        document.get("schema") == "hexalith.eventstore.story-4-15-successor-source-only-handoff/v1",
        "Story 4.15 successor handoff schema drift",
    )
    require(document.get("story") == "4.15", "Story 4.15 successor handoff story drift")
    require(document.get("selectedSuccessorDirectory") == SUCCESSOR_DIRECTORY, "Story 4.15 successor handoff directory drift")
    require(document.get("reviewedBaseCommit") == SUCCESSOR_REVIEW_BASE, "Story 4.15 successor handoff review base drift")
    require(document.get("reviewSubjectSha256") == subject_sha256, "Story 4.15 successor handoff subject drift")
    require(document.get("sourceIdentitySha256") == identity_sha256, "Story 4.15 successor handoff source identity drift")
    require(document.get("reviewReceipts") == receipts, "Story 4.15 successor handoff receipt set drift")
    require(
        document.get("consumerInstructions")
        == {
            "mode": "source-only",
            "verifyCommand": ".oq8-python/bin/python tools/validate-oq8-platform-evidence.py",
            "sourcePathRule": "Use only current files that match the selected successor source identity after the validator passes.",
            "priorSealRule": "Retain the original Story 4.15 packet, closure manifest, and every sealed artifact byte unchanged.",
        },
        "Story 4.15 successor consumer instructions drift",
    )
    require(document.get("priorSealRetained") is True, "Story 4.15 prior seal retention is not recorded")
    validate_authority(document.get("authority"))
    return sha256_file(SUCCESSOR / "source-only-handoff.json")


def validate_successor_selector(
    selector: Any,
    prior_manifest: dict[str, str],
    successor_manifest: dict[str, str],
    subject_sha256: str,
    identity_sha256: str,
    handoff_sha256: str,
) -> None:
    require(isinstance(selector, dict), "Story 4.15 successor selector must be an object")
    require(
        set(selector) == {"schema", "selectedOn", "reason", "prior", "successor", "authority"},
        "Story 4.15 successor selector field set drift",
    )
    require(
        selector.get("schema") == "hexalith.eventstore.story-4-15-successor-selection/v1",
        "Story 4.15 successor selector schema drift",
    )
    require(selector.get("selectedOn") == "2026-08-29", "Story 4.15 successor selection date drift")
    require(
        selector.get("reason")
        == "SDK 10.0.400 requires Microsoft.Testing.Platform, xUnit 4 serialization metadata, and Linux-safe OQ8 control-plane discovery.",
        "Story 4.15 successor selection reason drift",
    )
    require(
        selector.get("prior")
        == {
            "packetPath": "_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml",
            "packetSha256": PRIOR_PACKET_SHA256,
            "closureDirectory": CLOSURE_DIRECTORY,
            "closureManifestSha256": PRIOR_CLOSURE_MANIFEST_SHA256,
            "closureFiles": prior_manifest,
        },
        "Story 4.15 successor prior selection drift",
    )
    require(
        selector.get("successor")
        == {
            "directory": SUCCESSOR_DIRECTORY,
            "manifestSha256": sha256_file(SUCCESSOR / "successor-sha256.txt"),
            "files": successor_manifest,
            "sourceIdentitySha256": identity_sha256,
            "reviewSubjectSha256": subject_sha256,
            "handoffSha256": handoff_sha256,
        },
        "Story 4.15 successor selection drift",
    )
    require(sha256_file(PACKET) == PRIOR_PACKET_SHA256, "Story 4.15 prior packet byte identity drift")
    require(
        sha256_file(CLOSURE / "closure-sha256.txt") == PRIOR_CLOSURE_MANIFEST_SHA256,
        "Story 4.15 prior closure manifest byte identity drift",
    )
    validate_authority(selector.get("authority"))


def validate_successor_closure(prior_manifest: dict[str, str]) -> None:
    identity = validate_successor_source_identity()
    identity_sha256 = sha256_file(SUCCESSOR / "source-artifact-identity.json")
    successor_manifest = validate_successor_manifest()
    subject_sha256 = validate_successor_review_subject(
        load_candidate_json(SUCCESSOR / "review-subject.json"),
        identity,
    )
    receipts = validate_successor_reviews(subject_sha256)
    handoff_sha256 = validate_successor_handoff(
        load_candidate_json(SUCCESSOR / "source-only-handoff.json"),
        subject_sha256,
        identity_sha256,
        receipts,
    )
    validate_successor_selector(
        load_candidate_json(SUCCESSOR_SELECTOR),
        prior_manifest,
        successor_manifest,
        subject_sha256,
        identity_sha256,
        handoff_sha256,
    )


def validate_source_state(document: dict[str, Any], identity: dict[str, Any]) -> None:
    validate_successor_source_identity()
    require(isinstance(document, dict), "Captured source-state must be an object")
    require(isinstance(identity, dict), "Source identity must be an object")
    require(
        set(document) == {
            "schemaVersion",
            "baselineCommit",
            "dirtySourceCaptured",
            "candidateDiffAlgorithm",
            "candidateDiffSha256",
            "candidateFiles",
            "sourceInputs",
        },
        "Captured source-state field set drift",
    )
    require(document.get("baselineCommit") == BASELINE, "Baseline commit drift")
    candidate_files = document.get("candidateFiles", {})
    source_inputs = document.get("sourceInputs", {})
    require(isinstance(candidate_files, dict) and candidate_files, "Candidate file identities missing")
    require(isinstance(source_inputs, dict) and source_inputs, "Source input identities missing")
    require(set(candidate_files) == EXPECTED_CANDIDATE_FILES, "Pinned candidate-file identity set drift")
    require(set(source_inputs) == EXPECTED_SOURCE_INPUTS, "Pinned source-input identity set drift")
    for collection_name, collection in (("candidateFiles", candidate_files), ("sourceInputs", source_inputs)):
        for relative, expected in collection.items():
            require(isinstance(relative, str) and not Path(relative).is_absolute() and ".." not in Path(relative).parts, f"Unsafe {collection_name} path")
            require_sha256(expected, f"{collection_name}:{relative}")
    lines = [f"{relative}:{candidate_files[relative]}" for relative in sorted(candidate_files)]
    candidate_digest = hashlib.sha256(("\n".join(lines) + "\n").encode("utf-8")).hexdigest()
    require(document.get("candidateDiffSha256") == candidate_digest, "Candidate diff identity drift")
    require(
        document.get("candidateDiffAlgorithm")
        == "sha256(sorted complete changed source/config/tool relative-path:file-sha256 lines)",
        "Candidate diff algorithm drift",
    )
    require(document.get("dirtySourceCaptured") is True, "Dirty-source condition was not recorded")

    landed = identity.get("landedSource", {})
    current = identity.get("currentVerification", {})
    capture_paths = identity.get("captureWorktreePaths", {})
    landed_overrides = identity.get("landedGitByteOverrides", {})
    expected_paths = candidate_files | source_inputs
    require(identity.get("schema") == "hexalith.eventstore.story-4-15-source-artifact-identity/v1", "Source identity schema drift")
    require(
        set(identity) == {
            "schema",
            "repository",
            "landedSource",
            "capturedPathSets",
            "currentVerification",
            "captureWorktreePaths",
            "landedGitByteOverrides",
            "capture",
            "runtimeArtifacts",
        },
        "Source identity field set drift",
    )
    require(identity.get("repository") == "Hexalith/Hexalith.EventStore", "Source repository identity drift")
    require(
        set(landed) == {"commit", "tree", "pathCount", "pathSet"},
        "Landed source field set drift",
    )
    require(landed.get("commit") == LANDED_SOURCE, "Landed source commit drift")
    require(landed.get("tree") == LANDED_TREE, "Landed source tree declaration drift")
    require(landed.get("pathCount") == len(expected_paths) == 26, "Landed source path count drift")
    require(landed.get("pathSet") == "union of Story 4.14 candidateFiles and sourceInputs", "Landed source path-set rule drift")
    require(
        identity.get("capturedPathSets") == {
            "candidateFiles": sorted(EXPECTED_CANDIDATE_FILES),
            "sourceInputs": sorted(EXPECTED_SOURCE_INPUTS),
        },
        "Captured candidate/source path sets drift",
    )
    require(capture_paths == expected_paths, "Capture worktree path/hash set drift")
    require(
        run_git("rev-parse", f"{LANDED_SOURCE}^{{tree}}").decode("ascii").strip() == LANDED_TREE,
        "Landed source Git tree drift",
    )

    evolved = current.get("closureEvolvedPaths", {})
    require(
        evolved == {
            ".github/workflows/integration.yml":
                "The workflow orchestrates evidence capture and may evolve independently; its landed bytes remain historical evidence rather than current capability source.",
            "tools/validate-oq8-platform-evidence.py":
                "Story 4.15 evolves the closure validator; its new bytes are bound by validator-sha256.txt rather than treated as unchanged Story 4.14 capability source.",
        },
        "Closure-evolved path declaration drift",
    )
    require(
        landed_overrides == {
            ".github/workflows/integration.yml": {
                "captureWorktreeSha256": "afb28e703e9b9d51b5144e10c0368cf6ac94ab01cf5e456ef2090a6d444e3205",
                "landedGitSha256": "343163fd164bb49252ad2ec67c7fbc90aa2f3aaecafa4d4d51640ccc39e7b777",
                "reason": "Story 4.14 capture hashed the original integration workflow; landed commit 5e8f175b retains later CI history-fetch and Dapr runtime-pin wiring as historical evidence for the evolved orchestration path.",
            },
            "src/Hexalith.EventStore/Program.cs": {
                "captureWorktreeSha256": "245f79cc04998118da9caec70cdf290d67fb23e71a91100af46c4a019af5be7f",
                "landedGitSha256": "7203089f0035e3a45cf7ccf6c6fffdc120e4b2dc2d9fd53b8031ed87e7ad83e9",
                "reason": "The capture hashed CRLF working-tree bytes while the landed Git blob stores LF bytes under tracked text normalization.",
            },
            "tests/Hexalith.EventStore.Server.Tests/Actors/PublicationRecoveryActivationTests.cs": {
                "captureWorktreeSha256": "7aff4137378e9f2529b1770dd81c6638e8159277b4e92a5653ffc61a65ad2eb4",
                "landedGitSha256": "bfc4c85147df8d468d8ed33a9e00c4c372ce18d2512f07913095190527b15b6d",
                "reason": "Committed publication-recovery tests advanced before landed commit 5e8f175b after Story 4.14 capture; the override bridges capture worktree bytes to the intentional landed Git capability source.",
            },
            "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs": {
                "captureWorktreeSha256": "0da109af79cb0ded1c9e7377c4140a561ef682f1cf23b7c0fbb6284b7401c216",
                "landedGitSha256": "28a89849a864014f4e18ab0bd791c4e905cc662799c8a58fce7e9627762dde9a",
                "reason": "Story 4.5 fixture hardening advanced at landed commit 5e8f175b after Story 4.14 capture; the override bridges capture worktree bytes to the intentional landed Git capability source.",
            },
            "tools/validate-oq8-platform-evidence.py": {
                "captureWorktreeSha256": "0e9a352e7757f452dbc1f41dbd7036d76088f56be732a03c9b48ba4d6ab1c8b1",
                "landedGitSha256": "585e4d8634f6862a99a3431da37634a23d3aac5886a83b76f0bc4a7f0d309726",
                "reason": "Story 4.14 capture hashed the original validator; landed commit 5e8f175b retains the later closure-validator evolution, while current validator bytes remain bound by validator-sha256.txt.",
            },
        },
        "Landed Git-byte override declaration drift",
    )
    capability_paths = set(capture_paths) - set(evolved)
    require(capability_paths == EXPECTED_CURRENT_BOUND_PATHS, "Current bound source path set drift")
    require(
        set(current) == {
            "source",
            "rule",
            "pathCount",
            "boundPaths",
            "headMustDescendFromLandedSource",
            "sourceAuthorityBoundary",
            "closureEvolvedPaths",
            "unboundLaterPathsAllowed",
        },
        "Current source verification field set drift",
    )
    require(current.get("source") == "current HEAD Git tree", "Current source proof mode drift")
    require(
        current.get("rule")
        == "every non-evolved capability path must exist in HEAD, remain byte-equivalent to the landed source, and have no index or semantic working-tree change",
        "Current source proof rule drift",
    )
    require(current.get("pathCount") == len(capability_paths) == 24, "Current capability path count drift")
    require(current.get("boundPaths") == sorted(EXPECTED_CURRENT_BOUND_PATHS), "Current bound source path declaration drift")
    require(current.get("headMustDescendFromLandedSource") is True, "Current source ancestry requirement drift")
    require(
        current.get("sourceAuthorityBoundary")
        == "The exact landed Git tree is the complete source-only authority; current verification is limited to the 24 non-evolved captured capability paths and does not claim that every production path remains unchanged.",
        "Source-only authority boundary drift",
    )
    require(current.get("unboundLaterPathsAllowed") is True, "Later unbound paths are not explicitly allowed")
    run_git("merge-base", "--is-ancestor", LANDED_SOURCE, "HEAD")

    retained_paths = capability_paths - REPLACED_PRIOR_BOUND_PATHS
    require(
        retained_paths | REPLACED_PRIOR_BOUND_PATHS == capability_paths,
        "Story 4.15 successor replaced paths are not prior current-bound paths",
    )
    for relative, capture_expected in capture_paths.items():
        override = landed_overrides.get(relative, {})
        landed_expected = override.get("landedGitSha256", capture_expected)
        if override:
            require(override.get("captureWorktreeSha256") == capture_expected, f"Capture/Git override drift: {relative}")
        require_sha256(landed_expected, f"landedGitPaths:{relative}")
        require(sha256_bytes(git_file(LANDED_SOURCE, relative)) == landed_expected, f"Landed source identity drift: {relative}")
        if relative in retained_paths:
            require(sha256_bytes(git_file("HEAD", relative)) == landed_expected, f"Current bound source identity drift: {relative}")

    bound_arguments = tuple(sorted(retained_paths))
    index_records = run_git("ls-files", "-v", "-z", "--", *bound_arguments).split(b"\0")
    index_flags: dict[str, str] = {}
    for raw_record in index_records:
        if not raw_record:
            continue
        require(len(raw_record) > 2 and raw_record[1:2] == b" ", "Current bound source index record is malformed")
        try:
            flag = raw_record[:1].decode("ascii")
            relative = raw_record[2:].decode("utf-8")
        except UnicodeError:
            fail("Current bound source index record is malformed")
        require(relative not in index_flags, "Current bound source index path is duplicated")
        index_flags[relative] = flag
    require(set(index_flags) == retained_paths, "Current bound source index path set drift")
    for relative, flag in index_flags.items():
        require(flag == "H", f"Current bound source index flags are not normal: {relative}")

    require(
        git_diff_is_clean("diff", "--quiet", "--", *bound_arguments),
        "Current bound source has semantic working-tree changes",
    )
    require(
        git_diff_is_clean("diff", "--cached", "--quiet", "HEAD", "--", *bound_arguments),
        "Current bound source has index changes",
    )

    capture = identity.get("capture", {})
    require_exact_fields(capture, {"packetV1Path", "packetV1Sha256", "evidenceDirectory", "manifestSha256", "artifactCount"}, "Source identity capture")
    require(capture.get("packetV1Path") == "capture-packet-v1.json", "Capture packet snapshot path drift")
    require_sha256(capture.get("packetV1Sha256"), "Capture packet snapshot identity")
    require(capture.get("packetV1Sha256") == sha256_file(CLOSURE / "capture-packet-v1.json"), "Capture packet snapshot drift")
    require(capture.get("evidenceDirectory") == EVIDENCE_DIRECTORY, "Capture evidence directory identity drift")
    require(capture.get("manifestSha256") == sha256_file(EVIDENCE / "evidence-sha256.txt"), "Capture manifest identity drift")
    require(capture.get("artifactCount") == len(REQUIRED_FILES), "Capture artifact count drift")


def validate_closure_manifest() -> dict[str, str]:
    manifest_path = CLOSURE / "closure-sha256.txt"
    require(manifest_path.is_file(), "Closure manifest is missing")
    scan_support_safe(manifest_path)
    manifest: dict[str, str] = {}
    lines = read_text(manifest_path).splitlines()
    require(lines == sorted(lines, key=lambda line: line.split("  ", 1)[-1]), "Closure manifest is not path-sorted")
    for line in lines:
        parts = line.split("  ", 1)
        require(len(parts) == 2 and SHA256_RE.fullmatch(parts[0]) is not None, "Malformed closure manifest line")
        digest, relative = parts
        path = Path(relative)
        require(
            relative not in manifest
            and not path.is_absolute()
            and ".." not in path.parts
            and path.as_posix() == relative,
            "Unsafe or duplicate closure manifest path",
        )
        manifest[relative] = digest
    require(set(manifest) == CLOSURE_FILES, "Closure manifest file set drift")
    for relative, expected in manifest.items():
        path = CLOSURE / relative
        require(path.is_file(), f"Closure artifact missing: {relative}")
        require(not path.is_symlink(), f"Closure artifact cannot be a symlink: {relative}")
        require(sha256_file(path) == expected, f"Closure checksum mismatch: {relative}")
        scan_support_safe(path)

    validate_validator_identity()
    return manifest


def validate_validator_identity() -> str:
    validator_record = read_text(CLOSURE / "validator-sha256.txt").splitlines()
    require(len(validator_record) == 1, "Closure validator identity record is malformed")
    validator_parts = validator_record[0].split("  ", 1)
    require(
        len(validator_parts) == 2
        and validator_parts[1] == "tools/validate-oq8-platform-evidence.py"
        and validator_parts[0] == PRIOR_VALIDATOR_SHA256,
        "Closure validator identity drift",
    )
    require(
        sha256_git_file(PRIOR_CLOSURE_COMMIT, validator_parts[1]) == PRIOR_VALIDATOR_SHA256,
        "Closure validator historical identity drift",
    )
    return validator_parts[0]


def validate_crosswalk(document: dict[str, Any]) -> None:
    require(isinstance(document, dict), "Closure crosswalk must be an object")
    require(
        set(document) == {"schema", "story", "design", "invariants", "evidenceBindings", "storyEvidence", "verification"},
        "Closure crosswalk field set drift",
    )
    require(document.get("schema") == "hexalith.eventstore.story-4-15-closure-crosswalk/v1", "Closure crosswalk schema drift")
    require(document.get("story") == "4.15", "Closure crosswalk story drift")
    require(
        document.get("design") == {
            "version": DESIGN_VERSION,
            "sha256": DESIGN_SHA256,
            "bytesAvailableInEventStore": False,
        },
        "Closure crosswalk design reference drift",
    )
    invariants = document.get("invariants", [])
    require(invariants == EXPECTED_CROSSWALK_INVARIANTS, "Closure invariant-to-story/evidence mapping drift")
    referenced_evidence = {relative for invariant in invariants for relative in invariant["evidence"]}
    require(referenced_evidence == set(EXPECTED_CROSSWALK_EVIDENCE_HASHES), "Closure invariant evidence path set drift")
    evidence_bindings = document.get("evidenceBindings")
    require(evidence_bindings == EXPECTED_CROSSWALK_EVIDENCE_HASHES, "Closure evidence binding set or identity drift")
    for relative, expected in EXPECTED_CROSSWALK_EVIDENCE_HASHES.items():
        path = Path(relative)
        require(not path.is_absolute() and ".." not in path.parts and path.as_posix() == relative, "Unsafe crosswalk evidence path")
        require(sha256_file(ROOT / path) == expected, f"Crosswalk evidence body drift: {relative}")
    require(
        document.get("storyEvidence") == {story: "approved" for story in ("4.9", "4.10", "4.11", "4.12", "4.13", "4.14")},
        "Closure story result crosswalk drift",
    )
    verification = document.get("verification", {})
    expected_verification = {
        "commandsFile": f"{EVIDENCE_DIRECTORY}/commands.json",
        "commandsSha256": "29b488e3779192191340f868a4c3f5be3622af51dfa25a26663bb5f49727bd9c",
        "recordedCommands": 8,
        "successfulCommands": 8,
        "focusedProductionCases": 1,
        "focusedProductionPassed": 1,
        "deterministicMethods": 21,
        "deterministicCases": 33,
        "deterministicPassed": 33,
        "focusedSkipped": 0,
        "deterministicSupportSkipped": 0,
        "broadLanePreExistingSkipped": 25,
    }
    require(isinstance(verification, dict) and set(verification) == set(expected_verification), "Closure verification field set drift")
    for field, expected in expected_verification.items():
        if type(expected) is int:
            require_exact_integer(verification.get(field), expected, f"Closure verification {field}")
        else:
            require(verification.get(field) == expected, f"Closure verification {field} drift")
    require(verification["commandsSha256"] == sha256_file(ROOT / verification["commandsFile"]), "Closure commands identity drift")


def validate_authority(authority: Any) -> None:
    require(isinstance(authority, dict), "Closure authority record is missing")
    require(authority.get("eventStorePlatformComplete") is True, "EventStore platform completion is not recorded")
    require(authority.get("handoffMode") == "source-only", "OQ8 handoff is not source-only")
    require(set(authority) == {"eventStorePlatformComplete", "handoffMode", *EXTERNAL_AUTHORITY_FIELDS}, "Closure authority field set drift")
    for field in EXTERNAL_AUTHORITY_FIELDS:
        require(authority.get(field) is False, f"External authority overstated: {field}")


def validate_limitations(document: Any) -> dict[str, Any]:
    require(isinstance(document, dict), "Closure limitations must be an object")
    require(
        document == {
            "schema": "hexalith.eventstore.story-4-15-limitations/v1",
            "limitations": EXPECTED_LIMITATIONS,
        },
        "Closure limitation text or order drift",
    )
    return document


def validate_review_subject(subject: dict[str, Any], crosswalk: dict[str, Any], identity: dict[str, Any], limitations: dict[str, Any]) -> str:
    require(isinstance(subject, dict), "Review subject must be an object")
    require(
        set(subject) == {
            "schema",
            "createdOn",
            "proposedDecision",
            "design",
            "bindings",
            "identity",
            "reviewedPublicDocs",
            "handoff",
            "limitations",
            "requiredReviews",
            "authority",
        },
        "Review subject field set drift",
    )
    require(subject.get("schema") == "hexalith.eventstore.story-4-15-review-subject/v1", "Review subject schema drift")
    require(subject.get("createdOn") == CURRENT_REVIEW_DATE, "Review subject date drift")
    require(subject.get("proposedDecision") == "eventstore-platform-complete", "Review subject decision drift")
    require(subject.get("design") == crosswalk.get("design"), "Review subject design binding drift")
    bindings = subject.get("bindings", {})
    expected_bindings = {
        "capturePacketV1": ("capture-packet-v1.json", CLOSURE / "capture-packet-v1.json"),
        "closureCrosswalk": ("closure-crosswalk.json", CLOSURE / "closure-crosswalk.json"),
        "sourceArtifactIdentity": ("source-artifact-identity.json", CLOSURE / "source-artifact-identity.json"),
        "limitations": ("limitations.json", CLOSURE / "limitations.json"),
        "closureValidator": ("validator-sha256.txt", CLOSURE / "validator-sha256.txt"),
        "validatorRequirements": ("requirements-oq8.txt", None),
        "ciWorkflow": (".github/workflows/ci.yml", None),
        "integrationWorkflow": (".github/workflows/integration.yml", None),
        "workflowGuardrailTests": (
            "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs",
            None,
        ),
        "closureTests": (CLOSURE_TEST_SOURCE, None),
        "preReviewExecution": (PRE_REVIEW_EXECUTION, CLOSURE / PRE_REVIEW_EXECUTION),
        "captureManifest": (f"{EVIDENCE_DIRECTORY}/evidence-sha256.txt", EVIDENCE / "evidence-sha256.txt"),
    }
    require(set(bindings) == set(expected_bindings), "Review subject binding set drift")
    run_git("merge-base", "--is-ancestor", PRIOR_CLOSURE_COMMIT, "HEAD")
    for name, (relative, path) in expected_bindings.items():
        if relative in PRIOR_ROOT_BINDING_HASHES:
            expected_sha256 = PRIOR_ROOT_BINDING_HASHES[relative]
            require(
                sha256_git_file(PRIOR_CLOSURE_COMMIT, relative) == expected_sha256,
                f"Review subject historical binding drift: {name}",
            )
        else:
            require(path is not None, f"Review subject binding source is missing: {name}")
            expected_sha256 = sha256_file(path)
        require(bindings.get(name) == {"path": relative, "sha256": expected_sha256}, f"Review subject binding drift: {name}")
    require(
        subject.get("identity") == {
            "repository": "Hexalith/Hexalith.EventStore",
            "landedSourceCommit": LANDED_SOURCE,
            "landedSourceTree": LANDED_TREE,
            "boundPathCount": 26,
            "captureArtifactCount": 7,
        },
        "Review subject identity drift",
    )
    for relative, expected in EXPECTED_DOCUMENT_HASHES.items():
        validate_document_semantics(relative)
        require(sha256_file(ROOT / relative) == expected, f"Reviewed public document body drift: {relative}")
    require(subject.get("reviewedPublicDocs") == EXPECTED_DOCUMENT_HASHES, "Review subject public-document binding drift")
    require(
        subject.get("handoff") == {
            "schema": "hexalith.eventstore.story-4-15-source-only-handoff/v1",
            "story": "4.15",
            "landedSourceCommit": LANDED_SOURCE,
            "consumerInstructions": EXPECTED_CONSUMER_INSTRUCTIONS,
        },
        "Review subject handoff semantics drift",
    )
    require(subject.get("limitations") == limitations.get("limitations"), "Review subject limitations drift")
    reviews = subject.get("requiredReviews", [])
    require(
        reviews == [
            {"role": role, "reviewer": REVIEW_ROSTER[role], "scope": REVIEW_SCOPES[role], "status": "required"}
            for role in ("architecture", "security", "test")
        ],
        "Required reviewer roster or scope drift",
    )
    validate_authority(subject.get("authority"))
    return sha256_file(CLOSURE / "review-subject.json")


def validate_reviews(subject_sha256: str, limitations_sha256: str) -> dict[str, str]:
    receipts: dict[str, str] = {}
    for role, reviewer in REVIEW_ROSTER.items():
        path = CLOSURE / "reviews" / f"{role}.json"
        document = load_candidate_json(path)
        require(isinstance(document, dict), f"{role} review must be an object")
        require(
            set(document) == {
                "schema",
                "role",
                "reviewer",
                "reviewedOn",
                "decision",
                "subjectSha256",
                "acceptedScope",
                "acceptedLimitationsSha256",
                "findings",
                "authority",
            },
            f"{role} review field set drift",
        )
        require(document.get("schema") == "hexalith.eventstore.story-4-15-review-receipt/v1", f"{role} review schema drift")
        require(document.get("role") == role, f"{role} review role drift")
        require(document.get("reviewer") == reviewer, f"{role} reviewer identity drift")
        require(document.get("reviewedOn") == CURRENT_REVIEW_DATE, f"{role} review date drift")
        require(document.get("decision") == "approved", f"{role} review is not approved")
        require(document.get("subjectSha256") == subject_sha256, f"{role} review subject drift")
        require(document.get("acceptedScope") == REVIEW_SCOPES[role], f"{role} accepted scope drift")
        require(document.get("acceptedLimitationsSha256") == limitations_sha256, f"{role} limitations acceptance drift")
        findings = document.get("findings", [])
        require(
            isinstance(findings, list)
            and findings
            and all(isinstance(item, str) and item.strip() for item in findings),
            f"{role} review findings missing or blank",
        )
        validate_authority(document.get("authority"))
        receipts[role] = sha256_file(path)
    return receipts


def validate_handoff(
    document: dict[str, Any],
    subject: dict[str, Any],
    subject_sha256: str,
    receipts: dict[str, str],
    limitations_sha256: str,
) -> None:
    require(isinstance(document, dict), "Source-only handoff must be an object")
    require(
        set(document) == {
            "schema",
            "story",
            "landedSourceCommit",
            "reviewSubjectSha256",
            "limitationsSha256",
            "reviewReceipts",
            "consumerInstructions",
            "authority",
        },
        "Source-only handoff field set drift",
    )
    require(document.get("schema") == "hexalith.eventstore.story-4-15-source-only-handoff/v1", "Source-only handoff schema drift")
    require(document.get("story") == "4.15", "Source-only handoff story drift")
    require(document.get("landedSourceCommit") == LANDED_SOURCE, "Source-only handoff commit drift")
    require(document.get("reviewSubjectSha256") == subject_sha256, "Source-only handoff subject drift")
    require(document.get("limitationsSha256") == limitations_sha256, "Source-only handoff limitations drift")
    require(document.get("reviewReceipts") == receipts, "Source-only handoff receipt set drift")
    handoff_subject = subject.get("handoff", {})
    require(document.get("schema") == handoff_subject.get("schema"), "Source-only handoff reviewed schema drift")
    require(document.get("story") == handoff_subject.get("story"), "Source-only handoff reviewed story drift")
    require(document.get("landedSourceCommit") == handoff_subject.get("landedSourceCommit"), "Source-only handoff reviewed commit drift")
    require(document.get("consumerInstructions") == handoff_subject.get("consumerInstructions"), "Consumer instruction set or value drift")
    require(document.get("authority") == subject.get("authority"), "Source-only handoff reviewed authority drift")
    validate_authority(document.get("authority"))


def validate_pyyaml_dependency() -> None:
    dependency_valid = False
    try:
        distribution = importlib.metadata.distribution("PyYAML")
        expected_module = Path(distribution.locate_file("yaml/__init__.py")).resolve(strict=True)
        actual_module = Path(getattr(yaml, "__file__", "")).resolve(strict=True)
        dependency_valid = (
            distribution.version == PINNED_PYYAML_VERSION
            and getattr(yaml, "__version__", None) == PINNED_PYYAML_VERSION
            and actual_module == expected_module
        )
    except Exception:
        dependency_valid = False
    require(
        dependency_valid,
        f"Pinned PyYAML {PINNED_PYYAML_VERSION} dependency is unavailable or untrusted",
    )

    requirement_path = ROOT / "requirements-oq8.txt"
    require(
        read_text(requirement_path).splitlines() == [PINNED_PYYAML_REQUIREMENT],
        "OQ8 validator dependency requirement drift",
    )
    required_workflow_fragments = (
        'python3 -m venv "${RUNNER_TEMP}/oq8-python"',
        '"${RUNNER_TEMP}/oq8-python/bin/python" -m pip install --requirement requirements-oq8.txt',
        'echo "${RUNNER_TEMP}/oq8-python/bin" >> "$GITHUB_PATH"',
    )
    for relative in (".github/workflows/ci.yml", ".github/workflows/integration.yml"):
        workflow = read_text(ROOT / relative)
        require(
            all(fragment in workflow for fragment in required_workflow_fragments),
            f"OQ8 validator dependency bootstrap drift: {relative}",
        )


def parse_development_status(document: str) -> dict[str, str]:
    validate_pyyaml_dependency()
    require(
        len(document.encode("utf-8")) <= MAX_SPRINT_STATUS_BYTES,
        "Sprint-status YAML source exceeds the bounded size limit",
    )
    if document.startswith("\ufeff"):
        document = document[1:]
    require("\ufeff" not in document, "Sprint-status BOM is only permitted at stream start")
    require(
        yaml.reader.Reader.NON_PRINTABLE.search(document) is None,
        "Sprint-status YAML source contains forbidden characters",
    )

    class SprintStatusSafeLoader(yaml.SafeLoader):
        def __init__(self, stream: str) -> None:
            super().__init__(stream)
            self.node_properties: dict[int, tuple[bool, bool]] = {}
            self.aliased_nodes: set[int] = set()

        def compose_node(self, parent: Any, index: Any) -> Any:
            is_alias = self.check_event(yaml.events.AliasEvent)
            event = self.peek_event()
            node = super().compose_node(parent, index)
            if is_alias:
                self.aliased_nodes.add(id(node))
            else:
                self.node_properties[id(node)] = (
                    event.anchor is not None,
                    event.tag is not None,
                )
            return node

    loader = SprintStatusSafeLoader(document)
    try:
        root = loader.get_single_node()
    except yaml.composer.ComposerError as error:
        if error.context == "expected a single document in the stream":
            fail("Sprint-status YAML stream must contain exactly one document")
        fail("Unsupported sprint-status mapping structure")
    except yaml.YAMLError:
        fail("Unsupported sprint-status mapping structure")
    finally:
        loader.dispose()

    mapping_tag = yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG
    scalar_tag = yaml.resolver.BaseResolver.DEFAULT_SCALAR_TAG
    require(
        isinstance(root, yaml.nodes.MappingNode) and root.tag == mapping_tag,
        "Unsupported sprint-status mapping structure",
    )

    def has_properties(node: Any) -> bool:
        anchored, explicitly_tagged = loader.node_properties.get(id(node), (False, False))
        return anchored or explicitly_tagged or id(node) in loader.aliased_nodes

    require(not has_properties(root), "Unsupported sprint-status mapping structure")

    def contains_nested_lifecycle_mapping(node: Any, visited: set[int]) -> bool:
        if id(node) in visited:
            return False
        visited.add(id(node))
        if isinstance(node, yaml.nodes.MappingNode):
            for key_node, value_node in node.value:
                if isinstance(key_node, yaml.nodes.ScalarNode) and key_node.value == "development_status":
                    return True
                if contains_nested_lifecycle_mapping(key_node, visited):
                    return True
                if contains_nested_lifecycle_mapping(value_node, visited):
                    return True
        elif isinstance(node, yaml.nodes.SequenceNode):
            return any(contains_nested_lifecycle_mapping(item, visited) for item in node.value)
        return False

    root_entries: dict[str, tuple[Any, Any]] = {}
    lifecycle_entries: list[tuple[Any, Any]] = []
    for key_node, value_node in root.value:
        require(isinstance(key_node, yaml.nodes.ScalarNode), "Unsupported sprint-status mapping structure")
        key = key_node.value
        require(key_node.tag != "tag:yaml.org,2002:merge", "Sprint-status merge keys are forbidden")
        if key in root_entries:
            if key == "development_status":
                fail("Lifecycle development_status mapping is missing or ambiguous")
            fail("Sprint-status root mapping contains a duplicate key")
        root_entries[key] = (key_node, value_node)
        if key == "development_status":
            lifecycle_entries.append((key_node, value_node))
    for key, (_, value_node) in root_entries.items():
        if key != "development_status":
            require(
                not contains_nested_lifecycle_mapping(value_node, set()),
                "Unsupported sprint-status mapping structure",
            )

    require(len(lifecycle_entries) == 1, "Lifecycle development_status mapping is missing or ambiguous")
    lifecycle_key, lifecycle_mapping = lifecycle_entries[0]
    require(
        lifecycle_key.tag == scalar_tag and not has_properties(lifecycle_key),
        "Unsupported sprint-status mapping structure",
    )
    require(
        isinstance(lifecycle_mapping, yaml.nodes.MappingNode)
        and lifecycle_mapping.tag == mapping_tag
        and lifecycle_mapping.flow_style is False
        and not has_properties(lifecycle_mapping),
        "Unsupported sprint-status mapping structure",
    )

    retired_key = "4-8-durable-admission-evidence-ledger"
    bounded_lifecycle_keys = {
        "epic-4",
        "4-9-trusted-admission-contract-and-protected-identity",
        "4-10-digest-directory-rotation-and-key-retirement",
        "4-11-admission-state-machine-and-current-fence-enforcement",
        "4-12-expiry-compaction-and-tombstone-retention",
        "4-13-legacy-admission-migration-and-fail-closed-reconciliation",
        "4-14-oq8-multi-host-production-evidence",
        "4-15-oq8-platform-closure-and-handoff",
    }
    for key_node, _ in lifecycle_mapping.value:
        if isinstance(key_node, yaml.nodes.ScalarNode) and key_node.value == retired_key:
            fail(f"Retired lifecycle key is forbidden: {retired_key}")
        if isinstance(key_node, yaml.nodes.ScalarNode):
            require(key_node.tag != "tag:yaml.org,2002:merge", "Sprint-status merge keys are forbidden")

    unique_keys: set[str] = set()
    for key_node, _ in lifecycle_mapping.value:
        require(
            isinstance(key_node, yaml.nodes.ScalarNode),
            "Unsupported sprint-status mapping structure",
        )
        key = key_node.value
        if key in unique_keys:
            if key in bounded_lifecycle_keys:
                fail(f"Lifecycle status is missing or ambiguous: {key}")
            fail("Lifecycle status mapping contains a duplicate key")
        unique_keys.add(key)

    entries: dict[str, str] = {}
    for key_node, value_node in lifecycle_mapping.value:
        key = key_node.value
        require(
            key_node.tag == scalar_tag and not has_properties(key_node),
            "Unsupported sprint-status mapping structure",
        )
        require(
            isinstance(value_node, yaml.nodes.ScalarNode)
            and value_node.tag == scalar_tag
            and value_node.style in (None, "'", '"')
            and not has_properties(value_node),
            "Unsupported sprint-status mapping structure",
        )
        entries[key] = value_node.value
    return entries


def require_unique_sprint_status(statuses: dict[str, str], key: str, expected: str) -> None:
    require(key in statuses, f"Lifecycle status is missing or ambiguous: {key}")
    require(statuses[key] == expected, f"Lifecycle status drift: {key}")


def parse_unique_frontmatter_status(path: Path, story: str) -> str:
    text = read_text(path)
    lines = text.splitlines()
    require(lines and lines[0] == "---", f"Malformed Story {story} frontmatter")
    closing = next((index for index, line in enumerate(lines[1:], start=1) if line == "---"), None)
    require(closing is not None, f"Malformed Story {story} frontmatter")
    matches: list[str] = []
    for line in lines[1:closing]:
        match = re.fullmatch(r"status:\s*['\"]?([a-z-]+)['\"]?\s*", line)
        if match is not None:
            matches.append(match.group(1))
    require(len(matches) == 1, f"Story {story} frontmatter status is missing or ambiguous")
    return matches[0]


def validate_document_semantics(relative: str) -> None:
    text = read_text(ROOT / relative)
    require(text.count(EXPECTED_DOCUMENT_MARKER) == 1, f"OQ8 source-only handoff marker is missing or ambiguous: {relative}")
    for required in DOCUMENT_REQUIRED_TEXT:
        require(required in text, f"OQ8 source-only handoff semantics missing from {relative}: {required}")
    for forbidden in DOCUMENT_FORBIDDEN_TEXT:
        require(forbidden not in text, f"Stale OQ8 handoff state remains in {relative}: {forbidden}")


def validate_status_and_documents(*, final: bool) -> None:
    sprint = read_text(ROOT / "_bmad-output/implementation-artifacts/sprint-status.yaml")
    statuses = parse_development_status(sprint)
    expected_statuses = {
        "epic-4": "in-progress",
        "4-9-trusted-admission-contract-and-protected-identity": "done",
        "4-10-digest-directory-rotation-and-key-retirement": "done",
        "4-11-admission-state-machine-and-current-fence-enforcement": "done",
        "4-12-expiry-compaction-and-tombstone-retention": "done",
        "4-13-legacy-admission-migration-and-fail-closed-reconciliation": "done",
        "4-14-oq8-multi-host-production-evidence": "done",
        "4-15-oq8-platform-closure-and-handoff": "review" if final else "in-progress",
    }
    for key, expected in expected_statuses.items():
        require_unique_sprint_status(statuses, key, expected)

    story_specs = {
        "4.11": ROOT / "_bmad-output/implementation-artifacts/spec-4-11-admission-state-machine-and-current-fence-enforcement.md",
        "4.12": ROOT / "_bmad-output/implementation-artifacts/spec-4-12-expiry-compaction-and-tombstone-retention.md",
        "4.13": ROOT / "_bmad-output/implementation-artifacts/spec-4-13-legacy-admission-migration-and-fail-closed-reconciliation.md",
        "4.14": ROOT / "_bmad-output/implementation-artifacts/spec-4-14-oq8-multi-host-production-evidence.md",
        "4.15": ROOT / "_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md",
    }
    for story, path in story_specs.items():
        status = parse_unique_frontmatter_status(path, story)
        expected = "done" if final or story != "4.15" else "in-review"
        require(status == expected, f"Story {story} metadata status drift")

    for relative in EXPECTED_DOCUMENTS:
        validate_document_semantics(relative)


def validate_pre_review_candidate() -> None:
    require(EVIDENCE.is_dir(), "OQ8 evidence directory is missing")
    require(CLOSURE.is_dir(), "Story 4.15 closure directory is missing")
    capture_packet = load_candidate_json(CLOSURE / "capture-packet-v1.json")
    validate_capture_packet(capture_packet)
    crosswalk = load_candidate_json(CLOSURE / "closure-crosswalk.json")
    identity = load_candidate_json(CLOSURE / "source-artifact-identity.json")
    limitations = load_candidate_json(CLOSURE / "limitations.json")
    execution = load_candidate_json(CLOSURE / PRE_REVIEW_EXECUTION)
    subject = load_candidate_json(CLOSURE / "review-subject.json")
    validate_validator_identity()
    validate_crosswalk(crosswalk)
    validate_limitations(limitations)
    validate_pre_review_execution(execution)
    validate_review_subject(subject, crosswalk, identity, limitations)
    validate_successor_source_identity()
    validate_status_and_documents(final=False)


def validate_pre_review_execution(document: Any) -> None:
    require(isinstance(document, dict), "Pre-review execution record must be an object")
    require(
        set(document)
        == {"schema", "executedOn", "scope", "authority", "validator", "testSource", "finalValidation", "commands", "summary"},
        "Pre-review execution field set drift",
    )
    require(document.get("schema") == "hexalith.eventstore.story-4-15-pre-review-execution/v1", "Pre-review execution schema drift")
    require(document.get("executedOn") == CURRENT_REVIEW_DATE, "Pre-review execution date drift")
    require(document.get("scope") == "receipt-independent-isolated-candidate", "Pre-review execution scope drift")
    require(
        document.get("authority") == {
            "reviewReceiptsValidated": False,
            "finalHandoffValidated": False,
            "externalAuthorityClaimed": False,
        },
        "Pre-review execution authority disclosure drift",
    )
    require(
        document.get("validator") == {
            "path": "tools/validate-oq8-platform-evidence.py",
            "sha256": PRIOR_VALIDATOR_SHA256,
        },
        "Pre-review execution validator identity drift",
    )
    require(
        sha256_git_file(PRIOR_CLOSURE_COMMIT, "tools/validate-oq8-platform-evidence.py")
        == PRIOR_VALIDATOR_SHA256,
        "Pre-review execution historical validator identity drift",
    )
    require(
        document.get("testSource") == {
            "path": CLOSURE_TEST_SOURCE,
            "sha256": PRIOR_ROOT_BINDING_HASHES[CLOSURE_TEST_SOURCE],
        },
        "Pre-review execution test-source identity drift",
    )
    require(
        sha256_git_file(PRIOR_CLOSURE_COMMIT, CLOSURE_TEST_SOURCE)
        == PRIOR_ROOT_BINDING_HASHES[CLOSURE_TEST_SOURCE],
        "Pre-review execution historical test-source identity drift",
    )
    require(document.get("finalValidation") == PRE_REVIEW_FINAL_VALIDATION, "Pre-review final-validation disclosure drift")
    commands = document.get("commands")
    require(isinstance(commands, list) and len(commands) == len(PRE_REVIEW_COMMAND_RESULTS), "Pre-review execution command set drift")
    expected_names = [expected["name"] for expected in PRE_REVIEW_COMMAND_RESULTS]
    actual_names = [command.get("name") if isinstance(command, dict) else None for command in commands]
    require(
        len(set(expected_names)) == len(expected_names)
        and actual_names == expected_names
        and len(set(actual_names)) == len(actual_names),
        "Pre-review execution command names must be exact and unique",
    )
    for index, expected in enumerate(PRE_REVIEW_COMMAND_RESULTS):
        command = commands[index]
        require(isinstance(command, dict) and set(command) == set(expected), f"Pre-review execution command field set drift: {index}")
        require(isinstance(expected.get("command"), str) and expected["command"].strip(), f"Pre-review expected command identity missing: {index}")
        if "tests" in expected:
            require(
                type(expected["tests"]) is int
                and expected["tests"] > 0
                and expected.get("passed") == expected["tests"]
                and expected.get("failed") == 0
                and expected.get("skipped") == 0,
                f"Pre-review expected test counts are not meaningful: {index}",
            )
        for field, value in expected.items():
            if type(value) is int:
                require_exact_integer(command.get(field), value, f"Pre-review execution command {index}:{field}")
            else:
                require(command.get(field) == value, f"Pre-review execution command drift: {index}:{field}")
    expected_test_count = sum(expected.get("tests", 0) for expected in PRE_REVIEW_COMMAND_RESULTS)
    expected_summary = {
        "commands": len(PRE_REVIEW_COMMAND_RESULTS),
        "successfulCommands": len(PRE_REVIEW_COMMAND_RESULTS),
        "tests": expected_test_count,
        "passed": expected_test_count,
        "failed": 0,
        "skipped": 0,
    }
    summary = document.get("summary")
    require(isinstance(summary, dict) and set(summary) == set(expected_summary), "Pre-review execution summary field set drift")
    for field, value in expected_summary.items():
        require_exact_integer(summary.get(field), value, f"Pre-review execution summary {field}")


def validate_platform_closure(platform: dict[str, Any]) -> None:
    require(isinstance(platform, dict), "Platform closure must be an object")
    require(CLOSURE.is_dir(), "Story 4.15 closure directory is missing")
    manifest = validate_closure_manifest()
    crosswalk = load_candidate_json(CLOSURE / "closure-crosswalk.json")
    identity = load_candidate_json(CLOSURE / "source-artifact-identity.json")
    limitations = load_candidate_json(CLOSURE / "limitations.json")
    execution = load_candidate_json(CLOSURE / PRE_REVIEW_EXECUTION)
    subject = load_candidate_json(CLOSURE / "review-subject.json")
    validate_crosswalk(crosswalk)
    validate_limitations(limitations)
    validate_pre_review_execution(execution)
    subject_sha256 = validate_review_subject(subject, crosswalk, identity, limitations)
    limitations_sha256 = sha256_file(CLOSURE / "limitations.json")
    receipts = validate_reviews(subject_sha256, limitations_sha256)
    validate_handoff(load_candidate_json(CLOSURE / "source-only-handoff.json"), subject, subject_sha256, receipts, limitations_sha256)
    require(
        set(platform) == {
            "story",
            "status",
            "landedSourceCommit",
            "closureDirectory",
            "closureManifestSha256",
            "closureFiles",
            "reviewSubjectSha256",
            "authority",
        },
        "Platform closure field set drift",
    )
    require(platform.get("story") == "4.15", "Platform closure story drift")
    require(platform.get("status") == "complete", "Platform closure status drift")
    require(platform.get("landedSourceCommit") == LANDED_SOURCE, "Platform closure source drift")
    require(platform.get("closureDirectory") == CLOSURE_DIRECTORY, "Platform closure directory drift")
    require(platform.get("closureManifestSha256") == sha256_file(CLOSURE / "closure-sha256.txt"), "Platform closure manifest drift")
    require(platform.get("closureFiles") == manifest, "Platform closure file identities drift")
    require(platform.get("reviewSubjectSha256") == subject_sha256, "Platform closure subject drift")
    validate_authority(platform.get("authority"))
    validate_successor_closure(manifest)
    validate_status_and_documents(final=True)


def validate_capture_packet(packet: Any) -> None:
    require(isinstance(packet, dict), "Capture packet must be an object")
    require_exact_fields(
        packet,
        {"schemaVersion", "story", "design", "profile", "baselineCommit", "capturedOn", "evidenceDirectory", "evidenceFiles", "manifestSha256", "matrix", "closureClaimed", "releaseApproved", "story415Status"},
        "Capture packet",
    )
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

    observations = validate_observations(EVIDENCE / "observations.json", COMMITTED_DAPR_RUNTIME_VERSION)
    deterministic_support_path = EVIDENCE / "deterministic-support.json"
    deterministic_support = validate_support_document(load_json(deterministic_support_path))
    require(
        observations["observations"]["authority_change"]["deterministicSupportOracles"]
        == deterministic_support["selectors"],
        "Observation and deterministic support oracle identities drifted",
    )
    source_state = load_json(EVIDENCE / "source-state.json")
    identity = load_candidate_json(CLOSURE / "source-artifact-identity.json")
    validate_source_state(source_state, identity)
    environment = load_json(EVIDENCE / "environment.json")
    require_exact_fields(environment, {"schemaVersion", "capturedOn", "runtime", "profile", "executionConfiguration", "artifacts", "limits"}, "Environment")
    require(environment.get("schemaVersion") == 1, "Environment schemaVersion drift")
    require_exact_fields(environment.get("runtime"), {"dotnet", "dapr", "postgresImage", "postgresImageIdentity"}, "Environment runtime")
    require_exact_fields(environment.get("profile"), {"name", "stateStoreType", "stateComponentSha256", "resiliencySha256"}, "Environment profile")
    require_exact_fields(environment.get("executionConfiguration"), {"shippedReleaseEntryAssemblies", "shadowCopiedBeforeLaunch", "environmentName", "testOnlyHostingStartup", "productionConfigurationUntouched", "seams"}, "Environment execution configuration")
    require_exact_fields(environment.get("artifacts"), {"eventStoreSha256", "sampleSha256", "eventStoreRuntimeSetSha256", "sampleRuntimeSetSha256", "hostingStartupSha256", "additionalDepsSha256"}, "Environment runtime artifacts")
    require(identity.get("runtimeArtifacts") == environment.get("artifacts"), "Closure runtime artifact identities drift")
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
    require_exact_fields(limits, {"healthTimeoutSeconds", "nodeReadinessOverallTimeoutSeconds", "actorRuntimeReadinessRequired", "requestTimeoutSeconds", "diagnosticLogCharactersPerStream", "diagnosticStreamsScanned", "forbiddenTermClassesScanned", "rawDiagnosticsCommitted", "postgresqlProjection", "rawPostgresqlValuesCommitted"}, "Environment limits")
    require_exact_integer(limits.get("healthTimeoutSeconds"), 60, "Environment healthTimeoutSeconds")
    require_exact_integer(limits.get("nodeReadinessOverallTimeoutSeconds"), 60, "Environment nodeReadinessOverallTimeoutSeconds")
    require(limits.get("actorRuntimeReadinessRequired") is True, "Environment actor-runtime readiness requirement drift")
    require_exact_integer(limits.get("requestTimeoutSeconds"), 30, "Environment requestTimeoutSeconds")
    require_exact_integer(limits.get("diagnosticLogCharactersPerStream"), 32768, "Environment diagnosticLogCharactersPerStream")
    require_exact_integer(limits.get("diagnosticStreamsScanned"), 12, "Environment diagnosticStreamsScanned")
    require_exact_integer(limits.get("forbiddenTermClassesScanned"), len(DIAGNOSTIC_FORBIDDEN_CLASSES), "Environment forbiddenTermClassesScanned")
    require(limits.get("rawDiagnosticsCommitted") is False, "Environment permits committed raw diagnostics")
    require(
        limits.get("postgresqlProjection")
        == "row counts, state-shape counts, schema hash, projection hash, invariant results",
        "Environment PostgreSQL projection disclosure drift",
    )
    require(limits.get("rawPostgresqlValuesCommitted") is False, "Environment permits committed raw PostgreSQL values")
    test_results = load_json(EVIDENCE / "test-results.json")
    validate_focused_document(test_results)
    commands = load_json(EVIDENCE / "commands.json")
    require_exact_fields(commands, {"schemaVersion", "capturedOn", "commands"}, "Verification commands")
    require(commands.get("schemaVersion") == 1, "Verification commands schemaVersion drift")
    require(isinstance(commands.get("commands"), list), "Verification commands must be a list")
    for index, command in enumerate(commands["commands"]):
        require_exact_fields(command, {"name", "command", "exitCode", "counts"}, f"Verification command {index}")
        require(isinstance(command.get("counts"), dict), f"Verification command {index} counts must be an object")
    require(commands.get("capturedOn") == observations.get("capturedOn"), "Command record capture date crosswalk drift")
    command_names = [item.get("name") for item in commands.get("commands", [])]
    require(
        len(command_names) == len(EXPECTED_CAPTURE_COMMAND_RESULTS)
        and len(set(command_names)) == len(command_names)
        and set(command_names) == set(EXPECTED_CAPTURE_COMMAND_RESULTS),
        "Verification command names must be exact and unique",
    )
    command_records = {item["name"]: item for item in commands["commands"]}
    for name, expected in EXPECTED_CAPTURE_COMMAND_RESULTS.items():
        record = command_records[name]
        require_exact_integer(record.get("exitCode"), 0, f"Verification command {name}:exitCode")
        require(record.get("command") == expected["command"], f"Verification command identity drift: {name}")
        counts = record.get("counts")
        require(isinstance(counts, dict) and set(counts) == set(expected["counts"]), f"Verification command count field set drift: {name}")
        for field, value in expected["counts"].items():
            require_exact_integer(counts.get(field), value, f"Verification command {name}:{field}")
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
    require_exact_fields(reviews, {"schemaVersion", "records", "releaseApproval", "foldersOq8Closure", "story415Status"}, "Capture review records")
    require(reviews.get("schemaVersion") == 1, "Capture review records schemaVersion drift")
    require(
        reviews.get("records") == [
            {"kind": "implementation-verification", "performedOn": "2026-08-10", "result": "passed", "authority": "development-verification-only"},
            {"kind": "external-release-authority", "performed": False, "approval": False, "ownedBy": "Story 4.15 and external authority"},
            {"kind": "production-evidence-review", "performed": False, "approval": False, "ownedBy": "Murat"},
            {"kind": "leakage-fence-review", "performed": False, "approval": False, "ownedBy": "Security Reviewer"},
        ],
        "Capture review record set or field drift",
    )
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


def validate_committed_packet() -> None:
    require(PACKET.is_file(), "OQ8 packet is missing")
    require(EVIDENCE.is_dir(), "OQ8 evidence directory is missing")
    scan_support_safe(PACKET)
    outer_packet = load_json(PACKET)
    require(isinstance(outer_packet, dict), "Closure packet must be an object")
    require(outer_packet.get("schemaVersion") == 2, "Closure packet schemaVersion drift")
    require(set(outer_packet) == {"schemaVersion", "capture", "platformClosure"}, "Closure packet field set drift")
    packet = outer_packet.get("capture", {})
    require(packet == load_candidate_json(CLOSURE / "capture-packet-v1.json"), "Immutable v1 capture packet snapshot drift")
    validate_capture_packet(packet)
    validate_platform_closure(outer_packet.get("platformClosure", {}))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=DEFAULT_ROOT, help="Artifact/document root to validate")
    parser.add_argument("--git-root", type=Path, default=DEFAULT_ROOT, help="Git repository used for immutable source proof")
    parser.add_argument("--git-timeout-seconds", type=float, default=30.0, help="Bound each Git identity subprocess")
    parser.add_argument("--pre-review", action="store_true", help="Validate receipt-independent frozen candidate inputs")
    parser.add_argument("--lifecycle-mode", choices=("final",), help="Validate the exact final lifecycle/document gate in isolation")
    parser.add_argument("--capture-directory", type=Path, help="Validate one fresh opt-in OQ8 capture")
    parser.add_argument("--ctrf", type=Path, help="Raw CTRF input to sanitize for capture upload")
    parser.add_argument("--support-ctrf", type=Path, help="Raw deterministic-support CTRF input to validate and sanitize")
    parser.add_argument("--support-output", type=Path, help="Write one sanitized deterministic-support document")
    parser.add_argument(
        "--expected-runtime-version",
        action="append",
        help="Exact Dapr runtime version required for one fresh capture",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        configure_roots(args.root, args.git_root, args.git_timeout_seconds)
        if args.pre_review:
            require(
                args.lifecycle_mode is None
                and args.capture_directory is None
                and args.ctrf is None
                and args.support_ctrf is None
                and args.support_output is None
                and args.expected_runtime_version is None,
                "Pre-review mode cannot be combined with capture, support, or lifecycle arguments",
            )
        if args.lifecycle_mode is not None:
            require(
                not args.pre_review
                and args.capture_directory is None
                and args.ctrf is None
                and args.support_ctrf is None
                and args.support_output is None
                and args.expected_runtime_version is None,
                "Lifecycle mode cannot be combined with another validation mode",
            )
            validate_status_and_documents(final=True)
            print("OQ8 final lifecycle validation passed.")
        elif (
            args.capture_directory is not None
            or args.ctrf is not None
            or args.expected_runtime_version is not None
        ):
            require(
                args.capture_directory is not None
                and args.ctrf is not None
                and args.support_ctrf is not None
                and args.expected_runtime_version is not None
                and len(args.expected_runtime_version) == 1,
                "Capture mode requires --capture-directory, --ctrf, --support-ctrf, and exactly one --expected-runtime-version",
            )
            require(args.support_output is None, "Capture mode writes deterministic support into the capture directory")
            validate_capture(
                args.capture_directory.resolve(),
                args.ctrf.resolve(),
                args.support_ctrf.resolve(),
                args.expected_runtime_version[0],
            )
            print("OQ8 capture validation passed.")
        elif args.support_ctrf is not None or args.support_output is not None:
            require(args.support_ctrf is not None and args.support_output is not None, "Support mode requires --support-ctrf and --support-output")
            sanitize_support_ctrf(args.support_ctrf.resolve(), args.support_output.resolve())
            print("OQ8 deterministic support validation passed.")
        else:
            if args.pre_review:
                validate_pre_review_candidate()
            else:
                validate_committed_packet()
            print("OQ8 pre-review candidate validation passed." if args.pre_review else "OQ8 platform evidence validation passed.")
        return 0
    except EvidenceError as exception:
        print(f"OQ8 evidence validation failed: {exception}", file=sys.stderr)
        return 1
    except Exception as exception:
        bounded = EvidenceError(f"Unexpected validator failure was safely bounded ({type(exception).__name__})")
        print(f"OQ8 evidence validation failed: {bounded}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
