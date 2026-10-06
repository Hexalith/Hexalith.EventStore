---
title: '6.1-P1R successor qualification'
type: 'bugfix'
created: '2026-10-06'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '785d58fc99ce4c2751c0546ca6368e993c654ed9'
builds_input: 'ba4ca78c3868a4757cb92d912a54c8a237871b54'
context:
  - '{project-root}/_bmad-output/specs/spec-6-1-p1r-remediation/SPEC.md'
---

<frozen-after-approval reason="human-owned scope; package and operational authority remain separately owned">

## Intent

**Problem:** Completed P1R source remediation does not qualify published packages, real database restoration/failure cleanup, assertion totals or owner acceptance.

**Approach:** Build one CLI with prepare/run/validate commands and a small helper. Reuse the canonical scenario inventory and ownership patterns; independently validate retained evidence.

**Approved decisions (2026-10-06):** User said "apply recommended": preparation tooling and real subprocess controls only; existing Redis/Dapr 1.18.4 is local source context, with operational backend selection deferred. Retain approved mutation freeze/forward recovery until a capable published rollback is qualified. Use explicit executed-assertion counters for new checks; Test Architect acceptance remains a published gate. No package/database/container qualification is claimed.

## Boundaries & Constraints

**Always:** Bind current relevant source/configuration inputs, including dirty/untracked files and initialized root-declared dependencies, canonical HEADs and diff hashes. Retain unique invocation receipts, safe diagnostics, assertion counts and failures. Separate process-control evidence from unavailable package/database/actor lanes. Preserve all existing user work.

**Never:** Alter sealed evidence/validators, fabricate authority/counts, rewrite history, discard writes, or modify Projects/Builds/pins/readiness. Six deferrals and P2 denial gates stay separate. Publication/deployment/acceptance requires separate authority.

## I/O & Edge-Case Matrix

| Input/state | Required behavior |
| --- | --- |
| Missing, changed or unauthorized inputs; absent required evidence | Refuse dependent execution; report unavailable/unverified and nonpassing. |
| Failed, skipped, zero/unmeasured assertions or incompatible required case | Retain execution and compatibility separately; cannot qualify. |
| Complete preparation evidence | Verify inventory of seventeen scenarios, seven families and selected Reminder/evolution additions; valid preparation still cannot qualify P1R. |
| Real subprocess startup failure, success, timeout, cancellation, repeated cleanup | Retain command/output receipts and attempted/passed/failed assertion counts; stop exact owned process groups/descendants, preserve shared process sentinels, verify no owned processes remain. |

</frozen-after-approval>

## Code Map

- `tools/release_package_contract.py`: existing archive parsing for future package lanes; no new package implementation in this slice.
- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation/`: immutable source/ownership evidence; lacks real failure drills and fresh database restore.
- `_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/run_verification.py`: read-only scenario/count/archive/restore reference; fixed coordinates/checks stay unchanged.

## Tasks & Acceptance

**Execution:**

- [x] `tools/p1r_qualification.py` -- define fixed inventory, strict packet validation, source bindings, explicit counters and owned subprocess execution/cleanup. Preserve receipts on interruption, attempt every cleanup, and fail incomplete observations.
- [x] `tools/p1r-qualification.py` -- expose `prepare --out DIR`, `run --out DIR` and read-only `validate DIR`. Create fresh directories, refuse existing paths, run only bounded built-in process controls, and retain nonpassing qualification.
- [x] `tools/tests/test_p1r_qualification.py` -- cover duplicate/missing/tampered inputs, unmeasured/zero counts, nonpassing required lanes, existing-output refusal, and real startup/success/timeout/SIGINT/descendant/repeated-cleanup controls. Counter or receipt errors must fail; tests must inspect actual process end-state.
- [x] `_bmad-output/implementation-artifacts/evidence/6-1-p1r-qualification/README.md` -- document exact CLI use, index new bound receipts and explicitly pending package/runtime/database/container/owner gates.
- [x] `.github/workflows/p1r-qualification.yml` -- execute the focused Python suite in a separate Linux workflow with the existing pinned checkout action and root-only dependency initialization; preserve sealed CI inputs.

**Acceptance Criteria:**

- Given only existing source evidence, when preparation runs, then required missing lanes remain nonpassing and `qualified=false`/`p1r_usable=false` are retained.
- Given altered hashes, omitted/duplicate cases or invented qualification, when validation runs, then packet validation fails with a safe reason.
- Given a required failed/skipped/unmeasured/incompatible lane, when preparation is validated, then its nonpassing disposition remains explicit; valid structure cannot become conformance.
- Given an interrupted owned process invocation, when cleanup repeats, then receipts survive, exact owned descendants disappear and shared process sentinels remain unchanged. These controls do not qualify container/database cleanup.

## Implementation Notes

- Investigation: runtime scope is done; dirty-tree continuation is authorized. Existing source-packet `sha256sum --check --status SHA256SUMS` returned 0; integrity does not qualify packages. Canonical spec/code/old packets were preserved.
- User approved the recommended preparation slice and continuation. The compact scope resolves the prior questions without selecting a published tuple or granting owner acceptance. No runtime code, package versions, Git staging/commits/pushes or external transitions are authorized by this slice.
- Pre-edit Aspire baseline completed by the parent: isolated HTTP start initially exited 2 because unsecured transport was unset; retry with `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` and `--no-build` started successfully. `aspire describe` showed core services healthy and both existing UI projects FailedToStart. `aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` succeeded. These baseline observations are separate from Python verification; do not rerun or repair unrelated UI projects.
- Parent acceptance audit: inspected authored code/tests and the captured working diff. Corrected a reproduced omission of staged new source files; the regression also covers later unstaged edits. The pre-review focused suite passed 17 tests; pre-review prepare/run/validate receipts exited 0, six real controls passed 38 assertions, and a tampered copy exited 2. Both genuine packet checksum indexes pass. A later parent validation correctly refused source/configuration drift caused by continuing external edits; final evidence will bind stable post-review inputs. All scoped tasks and matrix cases have executed coverage.
- Review closure: all three review layers completed. Eighteen actionable findings were fixed through private process registration/kernel handles, explicit root and sentinel observations, consistent typed outcomes, ordered receipt intervals, strict malformed-input refusals and complete checksum inventories. Two concurrent runtime verification gaps were appended to the deferred-work ledger; no runtime changes were made. The implementation's first expanded test run exposed a test-only inherited-stdout timeout, corrected by redirecting the sleeping child's output; its focused regression then passed. The independent parent suite passed all 29 tests in 22.111 seconds, and `actionlint .github/workflows/p1r-qualification.yml` exited 0. Existing immutable inputs/packets and the approved frozen block were verified unchanged.
- Final collection: fresh prepare/run and independent validation receipts are reserved under `evidence/6-1-p1r-qualification/` with key `0b0a66d921b24ad5a15ba1882f46ff8d`; their command index records actual outcomes, including a deliberate tampered-copy refusal. These collection artifacts are created after this final metadata, without resealing previous observations. Completion means the approved preparation/process slice only; published qualification and all separately owned gates remain pending. User changes remain preserved; no staging, commit or push is performed.

## Spec Change Log

## Review Triage Log

Each finding was judged individually before grouping. Shared causes are patched together; the two runtime verification gaps belong to the concurrent Story 6.6 changes and are deferred because the approved intent excludes runtime edits.

| Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- |
| Blind 1: reused parent/session identity adopts unrelated processes | high | A parent-inventory probe with changed start ticks adopted an unrelated child; discovery compares numeric parent/session values without confirming the recorded live identity. Cleanup could terminate that child. | patch: exact ownership discovery |
| Blind 2: early parent exit loses detached descendants | high | The review's real launch probe left a detached child alive with empty ownership; the discovery graph has no surviving registration after its root exits before observation. This violates the approved interrupted-descendant cleanup requirement. | patch: exact ownership discovery |
| Blind 3: stopped sentinel appears running | medium | A stopped-state probe returned `running=true`; both observations can therefore compare equal after SIGSTOP and falsely establish shared-process preservation. | patch: sentinel observations |
| Blind 4: null sentinel observations accepted | medium | Parent validation of a resealed disposable copy accepted both observations as null with release true; no identity or release end-state is checked. | patch: sentinel observations |
| Blind 5: launched control can omit ownership | medium | A resealed timeout copy with empty ownership/cleanup observations still validated; end-state checks then iterate an empty inventory. | patch: receipt ownership evidence |
| Blind 6: cleanup before-observations can be erased | medium | The validator only checks that supplied before-identities are members of ownership, which accepts empty observations for still-running timed-out descendants. The review reproduced acceptance. | patch: receipt ownership evidence |
| Blind 7: invented string exit status accepted | medium | Parent validation accepted `exit_code="made-up"` for a timeout receipt; outcome validation never checks its type. | patch: receipt outcome consistency |
| Blind 8: cancellation can also claim failed launch | medium | Cancellation checks only its boolean; the review accepted an added launch error despite recorded launched identities and passing assertions. | patch: receipt outcome consistency |
| Blind 9: receipt dates need not match invocation | medium | Timestamp validation checks each interval alone; a 1900 receipt interval can pass within a current invocation because containment and control ordering are absent. | patch: receipt interval binding |
| Blind 10: malformed preparation assertion crashes | medium | Check IDs are accessed before assertion-shape validation; `[null]` raises AttributeError instead of the documented refusal. | patch: malformed evidence shapes |
| Blind 11: nested checksum files are unbound | medium | Both inventories exclude files by basename; the review accepted an added `receipts/SHA256SUMS` outside every hash/allowed-input check. | patch: checksum inventory |
| Edge 1: empty launched ownership accepted | medium | Independently reproduced by the edge reviewer and the parent's copied timeout probe; recorded process end-state is omitted despite passing counters. | patch: receipt ownership evidence |
| Edge 2: null sentinel accepted | medium | Independently reproduced by the edge reviewer and parent; equality of two nulls does not establish shared-process preservation. | patch: sentinel observations |
| Edge 3: sentinel observation failure prevents failure retention | medium | Sentinel packet state is initialized after a potentially failing observation, then unconditionally accessed during release; the review reproduced KeyError with no finish time/checksum index. | patch: sentinel initialization |
| Edge 4: PID/session reuse adopts unrelated child | high | Parent probe and edge tracing both show discovery relies on stale numeric ownership despite changed start time; termination can reach unrelated processes. | patch: exact ownership discovery |
| Edge 5: array packet causes traceback | medium | Parent resealed a disposable preparation copy with an array root; CLI exited 1 with AttributeError rather than structured exit 2. | patch: malformed evidence shapes |
| Edge 6: repeated SIGINT can bypass cleanup | high | The finally block calls timer cancellation/join before masking SIGINT; an interrupt there propagates before either owned cleanup attempt. This directly affects externally interrupted runs. | patch: interruption masking |
| Verification 1: final page metadata refusal lacks a behavioral test | medium | Filed verification shows existing tests refuse before callbacks or on a later page, leaving the final single-page fence unobserved. This is concurrent runtime work, not caused by the approved preparation/process slice. | defer |
| Verification 2: retained-history HTTP composition is mocked away | medium | Filed route-mutation demonstration leaves direct source/client-handler tests passing while the real controller is bypassed. The controller is from the external commit and runtime edits are explicitly excluded. | defer |
| Verification 3: Python regressions absent from CI | medium | The filed search found no checked CI invocation despite 17 passing focused tests; checksum/cleanup regressions could merge unchecked. A small separate workflow avoids changing the sealed CI input. | patch: focused CI invocation |

## Verification

- `python3 -m unittest discover -s tools/tests -p 'test_p1r_qualification.py'` -- positive contract controls and negative/tamper cases pass; real subprocess cleanup is observed.
- `actionlint .github/workflows/p1r-qualification.yml` -- the focused CI workflow passes static validation.
- Run prepare/run/validate on fresh packets; retain actual control outputs/counts and nonpassing qualification. Independently test tampered copies.
- Observe Aspire before code edits and release only invocation-owned resources. No .NET code changes or broad test rerun are required by this Python slice.
