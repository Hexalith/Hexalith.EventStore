---
title: 'Govern PostgreSQL image identity'
type: 'bugfix'
created: '2026-08-27'
status: 'blocked'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '_bmad-output/project-context.md'
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** The live-sidecar workflow and `Oq8PostgresqlFixture` independently name the mutable `postgres:18.4` tag, so their identities can drift and a later tag replacement can change tested bits without a reviewed source change.

**Approach:** Pin the reviewed multi-platform PostgreSQL 18.4 index as `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`, enforce exact workflow/fixture agreement with deterministic regression tests, and document a rotation procedure that re-verifies the upstream index before coordinated updates.

## Boundaries & Constraints

**Always:** Preserve the fixture's fail-closed prerequisite inspection and TCP readiness probe; use the cross-platform index digest rather than the amd64 child manifest; keep historical Story 4.14 evidence bytes unchanged; add new governance tests in a new source file because the existing OQ8 workflow guardrail test is itself hash-bound.

**Block If:** Editing `Oq8PostgresqlFixture.cs` remains forbidden by the approved Story 4.15 source-only handoff unless an authorized successor/reseal contract is supplied, or the owner explicitly accepts invalidating that handoff and its blocking closure test.

**Never:** Edit `_bmad-output/implementation-artifacts/deferred-work.md`; rewrite retained Story 4.14/4.15 evidence, review subjects, receipts, or checksum manifests; weaken OQ8 closure validation; use the historical Docker image/config ID as the registry pin; hide the fixture change through generated-source, MSBuild, PATH, or local-tag indirection.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Reviewed image | Workflow and fixture use the reviewed index | Both pull/run the identical digest-pinned image | No error expected |
| Mutable or malformed identity | Tag-only, child digest, missing digest, or invalid SHA-256 | Governance test rejects the identity | Focused deterministic failure identifies the violated contract |
| Drift | Workflow and fixture references differ | Governance test rejects the mismatch | Failure reports both extracted identities |
| Ambiguous workflow | Pull step is missing or has duplicate image pulls | Governance test rejects the workflow | Fail closed instead of selecting one reference |

</intent-contract>

## Code Map

- `.github/workflows/integration.yml` -- evolved OQ8 orchestration path; its named pull step currently contains `docker pull postgres:18.4` and may safely change to the reviewed digest.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs` -- runtime authority currently declares private `PostgresImage = "postgres:18.4"`; this file is also one of 24 byte-frozen Story 4.15 capability paths.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs` -- new, unbound deterministic guardrail location; extract the named workflow step and fixture constant, assert one exact match, digest shape, and negative mutations.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- read-only evidence; do not add coverage here because Story 4.15 hash-binds it as `workflowGuardrailTests`.
- `tools/validate-oq8-platform-evidence.py` -- read-only closure authority; `EXPECTED_CURRENT_BOUND_PATHS` and `validate_source_state()` intentionally reject fixture drift, while retained artifacts hash-bind this validator.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- read-only blocking test that proves the retained source-only handoff and detects any fixture edit.
- `docs/ci.md` -- unbound operational documentation location for digest discovery, review, coordinated rotation, focused governance validation, and full live-sidecar validation.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- explicitly read-only; orchestration owns resolution recording.

## Tasks & Acceptance

**Execution:**
- `.github/workflows/integration.yml` and `Oq8PostgresqlFixture.cs` -- replace the mutable tag with the reviewed multi-platform index reference after the OQ8 successor/reseal decision is supplied.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs` -- add positive agreement/digest-shape coverage and negative tag-only, mismatch, missing, and duplicate-pull cases.
- `docs/ci.md` -- document registry index inspection, upstream/version/platform review, coordinated literal rotation, and required validation.

**Acceptance Criteria:**
- Given the integration workflow and fixture source, when deterministic governance tests run, then they prove the named pull step and `PostgresImage` contain one identical `postgres@sha256:<64 lowercase hex>` reference.
- Given the reviewed PostgreSQL 18.4 multi-platform index, when the live-sidecar lane runs, then prerequisite inspection, container startup, and captured runtime metadata all use `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`.
- Given the existing Story 4.15 source-only handoff, when repository closure validation runs after implementation, then it remains valid through an authorized successor/reseal rather than historical-evidence mutation or a weakened gate.

## Spec Change Log

## Review Triage Log

## Design Notes

The registry inspection performed on 2026-08-27 returned index digest `sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636` and amd64 child manifest `sha256:4cc13dede823cab4e05290c7fb3350fb4e599ecabd9b07e6706b5d5e8f5bc929`. The index is the correct pin because the fixture is not architecture-specific. Retained evidence's `sha256:3a82...` value is a Docker image/config identity, not a registry manifest digest.

## Verification

**Commands:**
- `docker buildx imagetools inspect postgres:18.4 --format '{{json .Manifest}}'` -- expected: the reviewed index digest and platform manifests are visible before rotation.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false -m:1` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.PostgreSqlImageGovernanceTests -noColor` -- expected: all governance cases pass.
- `actionlint .github/workflows/integration.yml` -- expected: no findings.
- `python3 tools/validate-oq8-platform-evidence.py` -- expected: the approved source-only handoff remains valid after an authorized successor/reseal.
- `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/ --configuration Release -p:UseHexalithProjectReferences=false` -- expected: the complete live-sidecar suite passes with the digest-pinned image already pulled.

## Auto Run Result

Status: blocked
Blocking condition: intent gap — changing the required fixture invalidates the blocking, hash-bound Story 4.15 source-only handoff, while weakening that gate or rewriting retained evidence is forbidden. Supply an authorized successor/reseal contract, or explicitly accept invalidating the handoff and its closure test.
