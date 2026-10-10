---
title: '6.1-P1R published-candidate requalification'
type: 'feature'
created: '2026-10-10'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-6-1-p1r-remediation/SPEC.md'
---

<frozen-after-approval reason="human-owned P1R execution scope and exact inputs">

## Intent

**Problem:** EventStore source repairs are done, but the published 3.115.0 run has 110 failed checks and cannot qualify current P1R usability.

**Approach:** Qualify an owner-selected newer published candidate with the existing seventeen-scenario/seven-family matrix and both adopted additions. Repair only demonstrated current defects; retain true failure dispositions, independent validation, and separate owner acceptance.

**Decision (2026-10-10):** Use published EventStore 3.119.0, tag/source `f463442cca19e4199982a23a08bae4a490767d4a`, with Builds execution input `2cf00028bbe563d80d4d12b5fb2054914f14fcb6`. Keep the tag's Builds gitlink `468fdbba04e2d9a27d251298875125b57fa6d836` as distinct package-build provenance.

**Decision (2026-10-10):** Qualify the Projects operational profile: Dapr 1.18.2 with the tracked PostgreSQL v1 actor state store (`state.postgresql`). Use `rollback=null` and the approved Projects AD-17 mutation fence and forward recovery. Do not present Redis/Dapr 1.18.4 results or an unproven older package as this profile's writable recovery proof.

## Boundaries & Constraints

**Always:** Bind tag/source, Builds, archives/signatures, assets, loaded DLLs, runtime, measured checks, persisted inventories, Tenant isolation, and cleanup. Preserve sealed evidence, one writer, and freeze/forward recovery until a capable rollback passes.

**Never:** Reuse old receipts or infer a pass from source tests; rewrite history or sealed evidence; initialize nested submodules; change Projects pins/status/readiness; publish, deploy, or declare P1R usable from this run alone.

## I/O & Edge-Case Matrix

| Case | Expected result |
| --- | --- |
| Supported package | Real package/source directions, restore, append/restart, authority, watermark, and cleanup measured |
| 3.70.1/3.110.0 comparisons | Actual old incompatibilities remain nonpassing; no invented rollback |
| Missing or failed proof | Packet can be valid while technical qualification and usability remain false |
| Floor 5/head 12/snapshot 9, two Tenants | Supported append 13 and replay preserve prior hashes and other Tenant; otherwise fence mutation |

</frozen-after-approval>

## Code Map

- `tools/p1r_published_executor.py`, `tools/p1r-published-executor.py` -- isolated consumers, Redis-only sidecars and RDB backup/restore today; add a narrow PostgreSQL/Dapr 1.18.2 path for this run, preserving measured checks and owned cleanup. No generic backend framework.
- `deploy/dapr/statestore-postgresql.yaml` -- tracked PostgreSQL v1 actor-state component; use its type, actor setting, and scopes as the operational contract.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs` -- reuse the pinned PostgreSQL image, component rendering, two-sidecar lifecycle, structural snapshots, and cleanup pattern; it tests source binaries, not published packages.
- `../../tools/qualification/run_g6_qualification.py` -- Projects G-6 runtime evidence runs Dapr 1.18.2 with the PostgreSQL fixture; do not import its source-level pass as P1R package proof.
- `tools/p1r_published_qualification.py`, `tools/p1r-published-qualification.py` -- strict prepare/validate and independent gate computation.
- `tools/p1r_qualification_runtime.py`, `tools/p1r-published-consumers/` -- persisted inventory contract and actual package host/domain/probe.
- `tools/tests/test_p1r_published_executor.py`, `test_p1r_published_qualification.py` -- focused harness tests.
- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-31150-published-run/` -- immutable nonqualifying comparison; never reseal.
- `../Hexalith.Builds/Props/Directory.Packages.props` -- current 3.119.0 pin; no catalog edit in this run.

## Tasks & Acceptance

**Execution:**
- [ ] `tools/p1r_published_executor.py` -- verify selected inputs; run the PostgreSQL/Dapr 1.18.2 profile in fresh isolated package/source lanes, including a fresh-database restore and replay; execute every canonical case and adopted addition, retaining failed checks and owned cleanup.
- [ ] `tools/p1r-published-consumers/host/QualificationCapabilities.cs` and `tools/p1r-published-consumers/probe/Program.cs` -- exercise loaded assemblies, supported effects, authority refusal, persisted replay, and comparison directions.
- [ ] `tools/p1r_published_qualification.py` -- import only this run's receipts and independently recompute valid, technically qualified, decisions complete, and usable.
- [ ] `tools/tests/test_p1r_published_executor.py` -- pin each newly reproduced defect and preserve existing negative controls.
- [ ] `_bmad-output/implementation-artifacts/evidence/6-1-p1r-31190-published-run/` -- retain exact inputs, PostgreSQL/Dapr component and image identities, package/signature/graph/loaded-binary evidence, inventories, cleanup, failures, `rollback=null`, and pending decisions at unique paths.

**Acceptance Criteria:**
- Given approved exact inputs, when the canonical run executes, then every required case records its measured compatibility and assertion count.
- Given retained history and two Tenants, when the supported writer restores, appends, and restarts, then floor, sequence, old hashes, and the other Tenant remain intact.
- Given any failed required lane or missing owner decision, when independent validation runs, then it reports the limit without advancing Projects usability.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

- Run focused Python executor/validator tests, full `p1r-published-executor.py run`, and separate `p1r-published-qualification.py prepare`/`validate` against unique evidence paths. Require truthful dispositions, independent packet validity, and `git diff --check`.
