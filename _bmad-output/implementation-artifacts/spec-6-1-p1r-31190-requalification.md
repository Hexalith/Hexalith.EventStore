---
title: '6.1-P1R published-candidate requalification'
type: 'feature'
created: '2026-10-10'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '785260fa233880e403bf80bd1bc2f1d53b1f4836'
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
- [x] `tools/p1r_published_executor.py` -- verify selected inputs; run the PostgreSQL/Dapr 1.18.2 profile in fresh isolated package/source lanes, including a fresh-database restore and replay; execute every canonical case and adopted addition, retaining failed checks and owned cleanup.
- [x] `tools/p1r-published-consumers/host/QualificationCapabilities.cs` and `tools/p1r-published-consumers/probe/Program.cs` -- exercise loaded assemblies, supported effects, authority refusal, persisted replay, and comparison directions.
- [x] `tools/p1r_published_qualification.py` -- import only this run's receipts and independently recompute valid, technically qualified, decisions complete, and usable.
- [x] `tools/tests/test_p1r_published_executor.py` -- pin each newly reproduced defect and preserve existing negative controls.
- [x] `_bmad-output/implementation-artifacts/evidence/6-1-p1r-31190-published-run/` -- retain exact inputs, PostgreSQL/Dapr component and image identities, package/signature/graph/loaded-binary evidence, inventories, cleanup, failures, `rollback=null`, and pending decisions at unique paths.

**Acceptance Criteria:**
- Given approved exact inputs, when the canonical run executes, then every required case records its measured compatibility and assertion count.
- Given retained history and two Tenants, when the supported writer restores, appends, and restarts, then floor, sequence, old hashes, and the other Tenant remain intact.
- Given any failed required lane or missing owner decision, when independent validation runs, then it reports the limit without advancing Projects usability.

## Implementation Notes

The final execution is recorded in [the 3.119.0 evidence index](evidence/6-1-p1r-31190-published-run/README.md). All 17 canonical scenarios and both adopted additions ran against the selected PostgreSQL/Dapr profile; the packet retains earlier failed attempts at distinct paths. The final executor reported no errors and completed owned cleanup. Independent preparation and validation accepted the packet structure while reporting `technically_qualified=false`, `decisions_complete=false`, and `p1r_usable=false`. Historical incompatibilities, candidate stale-fence denial, logical alias execution, and four startup-failure seed checks remain nonpassing. No capable rollback or owner decision was selected; mutation freeze and forward recovery remain in force.

## Spec Change Log

- 2026-10-10: Implemented the selected published-candidate run and retained its nonqualifying result without changing Projects pins or status.

## Review Triage Log

| Finding | Verdict and route | Evidence |
| --- | --- | --- |
| Blind 1: packet line endings | medium; patch | `.gitattributes` pins the 31150 packet but not 31190; `git check-attr` reports `text=auto` and no `eol` for the new hash-bound JSON. Checkout conversion can break its byte hashes. |
| Blind 2: nested EditorConfig | low; reject | The new `root=true` hides parent style rules, but it also prevents the parent CRLF editor rule from rewriting copied consumer sources. This isolated fixture has no demonstrated style failure, and a safe correction would duplicate or redesign the rules. |
| Blind 3: database credential in apps | medium; patch | `start_nodes` puts `POSTGRES_CONNECTION_STRING` into the environment passed to both Host and Domain, although it already renders the component in private scratch. Domain's documented no-state-access boundary is weakened. |
| Blind 4: omitted state component | false; reject | `p1r_check_witnesses.py` requires the sidecar's exact `state.yaml` path in its rendered configuration whenever a runtime command is present. The cited validator loop is not the only guard. |
| Blind 5: rendered hash not independently recomputed | low; reject | The private rendered file is deliberately not retained because it contains a credential. Its hash is a producer observation; the validator separately binds the tracked template and actual PostgreSQL runtime checks. Proving the private bytes independently would require a new evidence protocol without changing this run's disposition. |
| Blind 6: inventory image not checked locally | false; reject | The qualification caller passes `self.redis`, which is created by `postgres_container` with the selected immutable image and recorded in `self.containers`; the standalone inventory CLI cannot substitute the selected run's container ID. |
| Blind 7: malformed stream row hidden | medium; patch | `postgres_inventory` labels a `:metadata` or `:snapshot` key as bookkeeping when decoded content is not an object. Domain comparisons exclude bookkeeping, so malformed stream evidence can disappear. |
| Blind 8: SQL zero-row result | false; reject | The only real mutation caller iterates keys from a stopped writer's `raw_state`; no supported concurrent writer can remove those keys between enumeration and update. The proposed missing-key case is not reached in this run. |
| Blind 9: no backup/restore verification | false; reject | The published full run executed fresh PostgreSQL backup, restore, append, restart, and replay; its independently validated candidate restore receipt passed all 12 checks, including two-Tenant and hash preservation. |
| Blind 10: absent component or fabricated hash tests | low; reject | The absent `state.yaml` path is already rejected by the executed witness guard. A fabricated private-render hash is possible, but changing its verification would require the new protocol described in Blind 5. |
| Blind 11: retained gaps lack regression tests | false; reject | The task's defect regression coverage concerns executor defects repaired in this run. Stale-fence and logical-alias failures are measured package limitations, and the startup seed passed on a fresh targeted rerun; none was silently treated as fixed. |
| Blind 12: four failed seed checks | false; reject | The selected packet retains all four failures and independently reports `technically_qualified=false` and `p1r_usable=false`, as the spec requires for failed proof. |
| Edge 1: malformed stream row hidden | medium; patch | A malformed metadata or snapshot value reaches the bookkeeping fallback in `postgres_inventory`; `runtime.validate_rows` accepts bookkeeping and `runtime.domain` excludes it. Same root cause as Blind 7. |
| Edge 2: unrelated rendered hash | low; reject | The validator checks the private-render hash format, not its inaccessible bytes. Actual sidecar and persisted PostgreSQL observations provide the operational evidence; the hash alone cannot qualify this packet. Same root cause as Blind 5. |
| Edge 3: non-string rendered hash | low; patch | `re.fullmatch` receives `None` or a number and raises `TypeError` for a malformed receipt. The CLI fails closed, but direct validator callers lose the intended `InvalidPacket` error; a type guard is a direct fix. |
| Edge 4: obsolete Redis restore branch | low; patch | No current caller passes `restore=` to `container`; the branch still copies an RDB into `/data/dump.rdb` and is dead under the PostgreSQL profile. Direct deletion removes an untested path. |
| Gap 1: persisted trusted-effect audit | medium; patch | `FixtureTrustedEffectAuditSink` is newly registered, but `mixed_case` checks effect/refusal and domain state only; bookkeeping audit rows are excluded. A no-op sink would pass those checks. |
| Gap 2: packet line endings | medium; patch | The 31190 source-binding JSON lacks the 31150 sibling's LF checkout rule, so a `core.autocrlf=true` checkout can invalidate the sealed index. Same root cause as Blind 1. |

## Verification

- Run focused Python executor/validator tests, full `p1r-published-executor.py run`, and separate `p1r-published-qualification.py prepare`/`validate` against unique evidence paths. Require truthful dispositions, independent packet validity, and `git diff --check`.
- `PYTHONPATH=tools python -m unittest discover -s tools/tests -p 'test_p1r_published_*.py'`: 84 passed.
- Final full executor: exit 0, all 17 scenarios and two additions recorded, zero executor errors; selected packet `prepare` and `validate`: exit 0, `valid=true` and technical qualification false.
- Four matrix rows checked against the final packet and restore receipt; `git diff --check` passed.
