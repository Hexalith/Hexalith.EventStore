---
title: 'Story 4.14: OQ8 Multi-Host Production Evidence'
type: 'feature'
created: '2026-08-10'
status: 'done'
review_loop_iteration: 0
story_key: '4-14-oq8-multi-host-production-evidence'
baseline_commit: 'e60a3777c581d70b62f67173ccc2372b5b64a425'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The current `oq8-postgresql-v1` fixture runs two hosts inside one xUnit process and proves only one failover case. Missing durability, leakage, and source-bound packet evidence prevents a production claim.

**Approach:** Run EventStore and Sample as independent processes/sidecars over the tracked PostgreSQL profile. Correlate production observations with deterministic tests and emit a validated, support-safe packet.

## Boundaries & Constraints

**Always:** Preserve Stories 4.9-4.13 replay, expiry, fence, migration, and fail-closed invariants. Bind evidence to OQ8 design `1.0.0` / SHA-256 `1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8` plus source/input/artifact/runtime identities, commands/counts, environment, and date. Inspect PostgreSQL transiently; commit only structural projections, hashes, and invariant results after sentinel scanning.

**Ask First:** Production behavior changes; a new project/package/dependency; or changed public contract, profile, schema, or retention.

**Never:** Treat same-process/direct-actor/mock evidence alone as production proof. Never record protected inputs/results, identifiers, secrets, or private paths. Do not publish/push, mutate Folders/submodules, update pins, approve releases, close Epic 4, or claim Folders OQ8 closure; those remain Story 4.15/external authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Writers/failover | Equivalent/different requests; owner stops at durable boundaries | One identity/fence/execution; another node resumes/replays exactly | Non-execute does zero work; unknown reconciles read-only or stays blocked |
| Expiry/compaction | Mutation/commit at T-1, T, T+1 with concurrent retries | Exact retention and atomic live-to-minimal-tombstone transition; never Missing | Equivalent/different expired reuse returns the same terminal outcome |
| Authority change | Interrupted rotation, collision, migration, deletion/hold, retirement | One canonical authority with durable checkpoints/references | Unsafe or unavailable state fails closed |
| Capture | Protected sentinels plus before/after production state | Source-bound packet contains identities, commands/counts, invariants, limits, and review records | Validator rejects leakage, placeholders, drift, omissions, or closure claims |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs:21-527` -- replace same-process hosts with child-process lifecycle, known-owner failover, PostgreSQL snapshots, identity capture, and bounded diagnostics/cleanup.
- `src/Hexalith.EventStore/Program.cs`, `samples/Hexalith.EventStore.Sample/Program.cs`, and `deploy/dapr/{statestore-postgresql,resiliency}.yaml` -- unchanged process/profile inputs; edits require approval.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/IdempotencyAdmissionOq8PostgresqlTests.cs:23-123` -- expand the narrow proof across the matrix.
- `tests/Hexalith.EventStore.Server.Tests/Actors/Idempotency*Tests.cs` -- reuse exact-time/fault oracles; do not duplicate actor logic.
- `_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml`, `evidence/story-4-14/e60a3777c581d70b62f67173ccc2372b5b64a425/`, and `tools/validate-oq8-platform-evidence.py` -- sanitized packet, receipts, manifest, validator, and observations; source-state records the candidate diff hash.
- `.github/workflows/integration.yml:54-85` and `sprint-status.yaml:122-124` -- reproduce/upload capture; set landed Story 4.13 to `done` and lifecycle-track 4.14 without entering release.

## Tasks & Acceptance

**Execution:**
- [x] `Oq8PostgresqlFixture.cs` plus focused support types -- provide two OS-process nodes/sidecars, actual Sample boundary, restart/failover controls, PostgreSQL structural snapshots, counters, diagnostics, and opt-in evidence emission.
- [x] `IdempotencyAdmissionOq8PostgresqlTests.cs` plus existing deterministic tests -- cover the matrix and prove one execution, exact replay, current fences, durable checkpoints/tombstones, and zero work for every non-execute class.
- [x] `4-8-eventstore-oq8-platform-evidence.yaml`, `evidence/story-4-14/`, `tools/validate-oq8-platform-evidence.py`, `.github/workflows/integration.yml`, and `sprint-status.yaml` -- capture/validate/upload sanitized source-bound evidence, repair tracking, and leave Story 4.15 closure unapproved.

**Acceptance Criteria:**
- Given two independent EventStore processes/sidecars sharing `oq8-postgresql-v1`, when concurrency, crash points, restart, expiry, and authority transitions run, then PostgreSQL evidence proves one authority, exactly one eligible execution, exact replay, and zero non-execute work.
- Given protected sentinels, when evidence validation finishes, then the packet is source-bound, leak-free, internally consistent, and makes no closure claim.

## Spec Change Log

- 2026-08-10: Implemented and verified the approved execution tasks without changing the frozen intent.
- 2026-08-10: Applied accepted review patches for exact replay, bounded runtime safety, source binding, and evidence validation.

## Design Notes

Production processes prove transport/restart/PostgreSQL behavior; deterministic tests remain labeled support for exact ticks/failures. Follow Story 4.5 redaction-before-hashing, immutable-capture, and dirty-source conventions.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --configuration Release -m:1` -- zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.IdempotencyAdmissionOq8PostgresqlTests.ProductionMatrix_IndependentProcessesPreserveAuthorityReplayExpiryAndLeakageInvariants -noColor -ctrf /tmp/oq8-results.json` -- the exact OQ8 case passes, no skips.
- Direct xUnit runner with every selector pinned in `deterministic-support.json` -- all 21 methods / 33 cases pass, no skips.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --no-build -m:1` -- deterministic lane passes.
- `python3 tools/validate-oq8-platform-evidence.py` -- packet passes completeness, identity, leakage, and checksum gates.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 && git diff --check` -- repository gate passes.

## Suggested Review Order

**Production proof**

- Start with the end-to-end matrix spanning writers, failover, expiry, authority, and leakage.
  [`IdempotencyAdmissionOq8PostgresqlTests.cs:21`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/IdempotencyAdmissionOq8PostgresqlTests.cs#L21)

- Follow independent process, PostgreSQL, and bounded lifecycle orchestration from fixture initialization.
  [`Oq8PostgresqlFixture.cs:82`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs#L82)

- Inspect stable directory authority derivation across rotation and retained-reader retirement.
  [`Oq8PostgresqlFixture.cs:407`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs#L407)

**Evidence integrity**

- Review support-safe capture construction, runtime disclosure, and launched-artifact identities.
  [`Oq8PostgresqlFixture.cs:1231`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs#L1231)

- Confirm the validator pins the exact live method and deterministic support set.
  [`validate-oq8-platform-evidence.py:29`](../../tools/validate-oq8-platform-evidence.py#L29)

- Verify focused CTRF identity and trait validation fails closed.
  [`validate-oq8-platform-evidence.py:335`](../../tools/validate-oq8-platform-evidence.py#L335)

- Check complete Git-derived source binding and actual profile crosswalks.
  [`validate-oq8-platform-evidence.py:572`](../../tools/validate-oq8-platform-evidence.py#L572)

- Finish with packet matrix, manifest, review-boundary, and no-closure enforcement.
  [`validate-oq8-platform-evidence.py:598`](../../tools/validate-oq8-platform-evidence.py#L598)

- Inspect the compact handoff packet and its immutable evidence identities.
  [`4-8-eventstore-oq8-platform-evidence.yaml:1`](4-8-eventstore-oq8-platform-evidence.yaml#L1)

**Reproduction and support seams**

- Review exact CI capture, support selectors, committed validation, and success-only upload.
  [`integration.yml:77`](../../.github/workflows/integration.yml#L77)

- See the disclosed hosting-startup seam that leaves production sources untouched.
  [`Oq8HostingStartup.cs:10`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8HostingStartup.cs#L10)

- Confirm atomic boundary counting cannot under-report concurrent downstream work.
  [`Oq8BoundaryCounterStartupFilter.cs:10`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8BoundaryCounterStartupFilter.cs#L10)

- Check deterministic time injection uses atomic shared-clock updates.
  [`Oq8FileTimeProvider.cs:6`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8FileTimeProvider.cs#L6)
