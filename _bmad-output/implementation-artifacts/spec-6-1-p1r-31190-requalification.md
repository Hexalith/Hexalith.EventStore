---
title: '6.1-P1R published-candidate requalification'
type: 'feature'
created: '2026-10-10'
status: 'done'
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

The final execution is recorded in [the 3.119.0 evidence index](evidence/6-1-p1r-31190-published-run/README.md). After the second review, `execution-822c180c8b1942d9bfba2601e2b5202e` reran all 17 canonical scenarios and both adopted additions against the selected PostgreSQL/Dapr profile. It ran on the committed first-review patches plus the second-review patches, with no executor errors and with owned cleanup completed. `packet-677345025f7b42449cbf8ce4afb43813` was prepared only from its receipts; independent validation accepted it as `valid=true` with `technically_qualified=false`, `decisions_complete=false`, and `p1r_usable=false`. The earlier `execution-64d7…`/`packet-7e1d…` pair was sealed before the first-review patches and is superseded; all attempts stay at unique paths.

Registering the fixture audit sink for 3.110.0 turned that direction's former fixture error into a measured, compatible unauthorized-effect refusal. The candidate audit records persist and match. Failure cleanup now passes 115/115. Historical metadata, invalid-evidence, query/projection wire and status incompatibilities remain nonpassing, as do the 3.110.0/3.119.0 stale-fence denial, the 3.70.1 unsupported methods, pre-upgrade containment restore, and logical alias execution. No capable rollback or owner decision was selected; mutation freeze and forward recovery remain in force.

## Spec Change Log

- 2026-10-10: Implemented the selected published-candidate run and retained its nonqualifying result without changing Projects pins or status.
- 2026-10-10: Second review patched without amending frozen intent or scope:
  - the 3.110.0 fixture audit sink, candidate workload-authority disclosure, exact Dapr runtime match, and backup wording;
  - regression tests for audit matching, the component validator, ownership refusals, and backend substitution.

  The run was repeated on the patched source, and the evidence README was corrected. One recovery-attribution finding was deferred.

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
| R2 Blind 1: `postgres_value` ignores `isbinary` | low; reject | All 1,741 string rows in execution-f3d07's 118 PostgreSQL dumps decode as base64 JSON, and metadata/event/snapshot rows are always JSONB objects, so no fixture row is misdecoded. The fix adds a selected column and branch. |
| R2 Blind 2: zero-row UPDATE/DELETE | false; reject | carried — Blind 8: the only mutation caller iterates keys from a stopped writer's `raw_state`. |
| R2 Blind 3: recovery steps bind wrong command | low; defer | `create-database` binds the last `pg_isready` probe and `backup` binds `docker cp`, but the baseline Redis path already bound the `docker inspect` and `docker cp` rows: a pre-existing `commands[-1]` attribution pattern. |
| R2 Blind 4: candidate workload authority undisclosed | medium; patch | `start_nodes` now applies the private Development JWT to `source` and 3.119.0, yet `source_workload_authority` is set only for `source`, so candidate configuration receipts record `None`. |
| R2 Blind 5: partial Redis migration | false; reject | `validate_execution_inputs` refuses any backend except `state.postgresql` before operations, so no Redis profile reaches mutation; attribute naming harms no caller. |
| R2 Blind 6: Builds commit divergence | low; patch | The frozen decision records both Builds commits but the evidence README omits them; routed with R2 Blind 13c. Error wording and a loud unfetched-tag failure are not defects. |
| R2 Blind 7: hard-coded candidate literals | false; reject | `check_current_source` lets packets validate only against their captured workspace, and every retained run carries its own consumer copy, so current literals never re-validate or rebuild 31150 evidence. |
| R2 Blind 8: template compared to working tree | false; reject | `check_current_source` already binds every tracked file, including the template, so any template edit invalidates the packet before `bind_lane`. Test gaps route under R2 Gap 2. |
| R2 Blind 9: monotonic stamp hides reversals | low; reject | Cross-process ordering violations still fail closed during validation; reversal diagnostics would add persisted cross-process state. |
| R2 Blind 10: audit check only negatively tested | medium; patch | Same root cause as R2 Gap 3. |
| R2 Blind 11: audit sink in actor state store | false; reject | A fixed number of effect operations bounds the rows; they are bookkeeping, excluded from domain comparisons and copied identically by restore; a wrong key prefix yields no audits and fails the check closed. |
| R2 Blind 12: binary evidence treated as text | false; reject | No `.png`, `.p7s`, `.dump` or `.nupkg` file is tracked under the 31190 tree; `*.nupkg` is ignored repository-wide and archives are bound by hash. |
| R2 Blind 13a: "physical" backup wording | low; patch | `tools/p1r-published-consumers/README.md:14` calls the `pg_dump -Fc` logical backup physical. |
| R2 Blind 13b: password in container environment | false; reject | argv carries only `-e POSTGRES_PASSWORD` without a value, evidence uses the bounded inspect format, and retained commands contain zero `POSTGRES_PASSWORD=` occurrences. |
| R2 Blind 13c: evidence README omissions | low; patch | The README lacks pinned identities, Builds divergence, failed direction and seed-check ids, and pending decisions; routed with the R2 Gap 1 rewrite. |
| R2 Blind 13d: Dapr version substring | low; patch | `"1.18.2" in version` also accepts 1.18.20; the recorded output is exactly `1.18.2`, so an exact comparison is a direct correction. |
| R2 Blind 14: nested EditorConfig | low; reject | carried — Blind 2: `root=true` deliberately isolates copied consumer sources; no style failure is demonstrated. |
| R2 Blind 15: `postgres_wait` evidence clutter | low; reject | Probe rows are accurate records and the timeout fails loudly; no caller is harmed. |
| R2 Edge 1: base64 JSON string misdecoded | low; reject | Same root cause and evidence as R2 Blind 1. |
| R2 Edge 2: unguarded `json.loads` in `mutate` | false; reject | Unchanged baseline loop; every tenant-a row in executed dumps decodes to JSON (object, integer or base64 JSON). |
| R2 Edge 3: `isbinary` left set on UPDATE | false; reject | `mutate` writes only metadata, event and snapshot rows, which are JSONB objects in every executed dump. |
| R2 Edge 4: Redis mutation paths | false; reject | Same root cause and evidence as R2 Blind 5. |
| R2 Edge 5: Dapr version substring | low; patch | Same root cause as R2 Blind 13d. |
| R2 Edge 6: polling an exited PostgreSQL container | low; reject | Startup still fails loudly with a timeout; only diagnostic precision is affected. |
| R2 Edge 7: `create-database` binds probe | low; defer | Same root cause as R2 Blind 3. |
| R2 Edge 8: psql stderr merged into stdout | low; reject | Parsed queries are plain SELECTs that emit no notices; any notice fails loudly as an executor error. |
| R2 Edge 9: template from working tree | false; reject | Same root cause and evidence as R2 Blind 8. |
| R2 Edge 10: cross-process clock reversal | low; reject | Same root cause and evidence as R2 Blind 9. |
| R2 Edge 11: 3.110.0 build lacks audit sink | medium; patch | 3.110.0 and 3.119.0 share the `ITrustedEffectAuditSink` contract and both throw `Trusted effect denial audit is unavailable.` without one; the fixture registers it only under `P1R_CANDIDATE`. |
| R2 Edge 12: removed 3.115.0 literals | false; reject | Same root cause and evidence as R2 Blind 7. |
| R2 Edge 13: designated evidence predates committed code | medium; patch | Same root cause as R2 Gap 1. |
| R2 Edge 14: 3.110.0 refusal direction unmeasured | medium; patch | execution-64d7 command 3526 and execution-f3d07 command 3416 record the 3.110.0 audit-unavailable error while 3.119.0 refuses; same root cause as R2 Edge 11. |
| R2 Edge 15: ignored archives cannot be re-verified | false; reject | Archives are bound by SHA-256 and remain retrievable at their exact published versions; the README claim is binding, not retention. |
| R2 Gap 1: final evidence predates committed source | medium; patch | Pre-verified. execution-64d7 and packet-7e1d bind pre-patch executor and fixture hashes, and `validate` at HEAD returns `source/configuration inputs changed`. packet-98591a7b failed on timestamps. execution-4b93 matches HEAD but stopped after 13 receipts with four exited owned containers left behind. |
| R2 Gap 2: PostgreSQL component validator untested | medium; patch | Pre-verified: disabling the hook and its redaction and template clauses leaves every test passing. |
| R2 Gap 3: audit match only tested empty | medium; patch | Pre-verified: dropping `action` and `disposition` from the match leaves the tests passing. |
| R2 Gap 4: ownership refusals untested | medium; patch | Pre-verified: no-op `postgres_inventory` and `postgres_query` ownership guards leave every test passing. |
| R2 Gap 5: backend substitution untested | medium; patch | Pre-verified: the substitution loop never mutates `operational_profile.backend`. |
| R2 Gap other 1: `isbinary` ignored | low; reject | Same root cause and evidence as R2 Blind 1. |
| R2 Gap other 2: README contradicts later runs | medium; patch | Same root cause as R2 Gap 1. |

## Verification

- Run focused Python executor/validator tests, full `p1r-published-executor.py run`, and separate `p1r-published-qualification.py prepare`/`validate` against unique evidence paths. Require truthful dispositions, independent packet validity, and `git diff --check`.
- `PYTHONPATH=tools python -m unittest discover -s tools/tests -p 'test_p1r_published_*.py'`: 84 passed.
- Final full executor: exit 0, all 17 scenarios and two additions recorded, zero executor errors; selected packet `prepare` and `validate`: exit 0, `valid=true` and technical qualification false.
- Four matrix rows checked against the final packet and restore receipt; `git diff --check` passed.
- Second review: `PYTHONPATH=tools python -m unittest discover -s tools/tests -p 'test_p1r_published_*.py'` passed 95 tests, and all 130 `test_p1r_*.py` tests passed. Disabling each of 10 newly covered guards in a scratch copy made its targeted test fail.
- Final full executor `execution-822c180c8b1942d9bfba2601e2b5202e`: exit 0, 21 receipts, zero executor errors, every owned container removed.
- `prepare` and `validate` of `packet-677345025f7b42449cbf8ce4afb43813`: exit 0 at 2026-10-10T12:46:46Z, `valid=true`, technical qualification false.
- After these spec and README updates, `validate` still exits 0. It binds HEAD, gitlinks and bound source/configuration files, so after any commit it reports `source/configuration inputs changed` by design. Validate only against the captured workspace.
