---
title: 'Govern PostgreSQL image identity'
type: 'bugfix'
created: '2026-08-27'
status: ready-for-dev
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

**Approach:** Pin the reviewed multi-platform PostgreSQL 18.4 index as `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`, enforce exact workflow/fixture agreement with deterministic regression tests, and document a rotation procedure that re-verifies the upstream index before coordinated updates. Preserve the Story 4.15 v1 handoff as immutable historical evidence and add a versioned v2 successor that becomes the only active current-source authority for the changed workflow and fixture.

## Boundaries & Constraints

**Always:** Preserve the fixture's fail-closed prerequisite inspection and TCP readiness probe; use the cross-platform index digest rather than the amd64 child manifest; keep the Story 4.14 and Story 4.15 v1 evidence directories, review subjects, receipts, checksum manifests, and `4-8-eventstore-oq8-platform-evidence.yaml` byte-for-byte unchanged; add new governance tests in a new source file because the existing OQ8 workflow guardrail test is itself hash-bound. The additive v2 successor must bind the v1 landed source commit `5e8f175b2ced4715f7c6f765386812cc1001dbb4` and subject SHA-256 `26a0afd67c14befc3d7b5045c13c1532b27663e3409026d6f5d5e8fc5b3b5e6f`, the exact reviewed PostgreSQL index, the before/after identities of the workflow and fixture, the successor validator/tests/documentation, and fresh architecture, security, and test receipts issued after the successor subject is frozen.

**Block If:** The v2 successor is absent, does not link exactly to the v1 commit and subject above, omits either changed source identity or any successor gate input, lacks any fresh content-bound architecture/security/test receipt, rewrites v1 bytes, or attempts to make the historical v1 handoff alone authorize the changed current source.

**Never:** Edit `_bmad-output/implementation-artifacts/deferred-work.md`; rewrite retained Story 4.14/4.15 v1 evidence, review subjects, receipts, checksum manifests, or top-level handoff packet; weaken OQ8 closure validation; treat v1 as current-source authority after either bound source changes; grant release, package, registry, deployment, runtime-pin, consumer-migration, external-repository, Folders-final-closure, or final-consumer authority; use the historical Docker image/config ID as the registry pin; hide the fixture change through generated-source, MSBuild, PATH, or local-tag indirection.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Reviewed image | Workflow and fixture use the reviewed index | Both pull/run the identical digest-pinned image | No error expected |
| Mutable or malformed identity | Tag-only, child digest, missing digest, or invalid SHA-256 | Governance test rejects the identity | Focused deterministic failure identifies the violated contract |
| Drift | Workflow and fixture references differ | Governance test rejects the mismatch | Failure reports both extracted identities |
| Ambiguous workflow | Pull step is missing or has duplicate image pulls | Governance test rejects the workflow | Fail closed instead of selecting one reference |
| Historical v1 handoff | Immutable v1 evidence is valid but current workflow or fixture differs from v1 | Preserve v1 as historical evidence; require the valid v2 successor for current-source authority | V1 alone must not authorize current source |
| Missing or incomplete successor | V2 predecessor link, bound identity, review receipt, or gate input is missing or changed | Reject current-source closure | Name the missing or drifted successor field without falling back to v1 |
| Overstated successor authority | V2 claims authority beyond the existing EventStore source-only boundary | Reject the successor | Preserve every v1 external-authority exclusion |

</intent-contract>

## Code Map

- `.github/workflows/integration.yml` -- evolved OQ8 orchestration path; its named pull step currently contains `docker pull postgres:18.4` and may safely change to the reviewed digest.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs` -- runtime authority currently declares private `PostgresImage = "postgres:18.4"`; this file is also one of 24 byte-frozen Story 4.15 capability paths.
- `_bmad-output/implementation-artifacts/evidence/story-4-15/**` and `_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml` -- immutable v1 predecessor; read and validate, but do not edit or reseal.
- `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/**` -- additive versioned successor location; freeze a v2 subject, predecessor binding, exact changed-source and gate identities, limitations, fresh named receipts, and a closed checksum manifest.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs` -- new, unbound deterministic guardrail location; extract the named workflow step and fixture constant, assert one exact match, digest shape, and negative mutations.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- read-only evidence; do not add coverage here because Story 4.15 hash-binds it as `workflowGuardrailTests`.
- `tools/validate-oq8-platform-evidence.py` -- evolve the active entry point so it still proves v1's immutable historical integrity but requires the v2 successor for current-source closure after the bound workflow/fixture change; it must not silently remove a path from v1 or accept v1 alone.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- evolve the blocking contract to prove v1 remains historically intact, v1 alone cannot authorize the changed current source, and only a complete content-bound v2 successor restores current-source closure.
- `docs/ci.md` -- unbound operational documentation location for digest discovery, review, coordinated rotation, focused governance validation, and full live-sidecar validation.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- explicitly read-only; orchestration owns resolution recording.

## Tasks & Acceptance

**Execution:**
- `.github/workflows/integration.yml` and `Oq8PostgresqlFixture.cs` -- replace the mutable tag with the reviewed multi-platform index reference after the OQ8 successor/reseal decision is supplied.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/PostgreSqlImageGovernanceTests.cs` -- add positive agreement/digest-shape coverage and negative tag-only, mismatch, missing, and duplicate-pull cases.
- `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/**` -- add the immutable v2 successor described above without modifying any v1 artifact; freeze its subject before issuing fresh architecture, security, and test receipts.
- `tools/validate-oq8-platform-evidence.py` and `Oq8PlatformClosureTests.cs` -- replace v1's current-HEAD authority with the fail-closed v2 successor gate while retaining explicit historical v1 integrity validation and negative proof that v1 alone cannot authorize the changed source.
- `docs/ci.md` -- document registry index inspection, upstream/version/platform review, coordinated literal rotation, and required validation.

**Acceptance Criteria:**
- Given the integration workflow and fixture source, when deterministic governance tests run, then they prove the named pull step and `PostgresImage` contain one identical `postgres@sha256:<64 lowercase hex>` reference.
- Given the reviewed PostgreSQL 18.4 multi-platform index, when the live-sidecar lane runs, then prerequisite inspection, container startup, and captured runtime metadata all use `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`.
- Given the existing Story 4.15 source-only handoff, when repository closure validation runs after implementation, then every v1 artifact remains byte-for-byte valid as historical evidence, v1 alone is explicitly non-authorizing for the changed current source, and the complete v2 successor with fresh content-bound architecture/security/test receipts is the only gate that restores current-source closure.
- Given a missing, drifted, partially reviewed, predecessor-mismatched, or authority-overstating v2 successor, when either focused or full closure validation runs, then validation fails closed and never falls back to v1 or silently exempts the changed workflow/fixture.

## Spec Change Log

- 2026-08-28: Owner selected the versioned Story 4.15 successor resolution: preserve v1 byte-for-byte, bind the digest-pinned workflow/fixture and successor gate in v2, require fresh architecture/security/test receipts, and make v2 the only active current-source authority.

## Review Triage Log

## Design Notes

The registry inspection performed on 2026-08-27 returned index digest `sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636` and amd64 child manifest `sha256:4cc13dede823cab4e05290c7fb3350fb4e599ecabd9b07e6706b5d5e8f5bc929`. The index is the correct pin because the fixture is not architecture-specific. Retained evidence's `sha256:3a82...` value is a Docker image/config identity, not a registry manifest digest.

## Verification

**Commands:**
- `docker buildx imagetools inspect postgres:18.4 --format '{{json .Manifest}}'` -- expected: the reviewed index digest and platform manifests are visible before rotation.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false -m:1` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.PostgreSqlImageGovernanceTests -noColor` -- expected: all governance cases pass.
- `actionlint .github/workflows/integration.yml` -- expected: no findings.
- `python3 tools/validate-oq8-platform-evidence.py` -- expected: immutable v1 historical integrity and complete v2 current-source closure both pass; v1 alone cannot authorize the changed source.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.Oq8PlatformClosureTests -noColor` -- expected: v1 preservation, v2 successor, missing/drifted receipt, predecessor mismatch, source drift, and authority-boundary cases all pass.
- `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/ --configuration Release -p:UseHexalithProjectReferences=false` -- expected: the complete live-sidecar suite passes with the digest-pinned image already pulled.

