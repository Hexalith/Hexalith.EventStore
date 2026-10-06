---
title: '6.1-P1R successor qualification'
type: 'bugfix'
created: '2026-10-06'
status: 'in-progress'
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

- [ ] `tools/p1r_qualification.py` -- define fixed inventory, strict packet validation, source bindings, explicit counters and owned subprocess execution/cleanup. Preserve receipts on interruption, attempt every cleanup, and fail incomplete observations.
- [ ] `tools/p1r-qualification.py` -- expose `prepare --out DIR`, `run --out DIR` and read-only `validate DIR`. Create fresh directories, refuse existing paths, run only bounded built-in process controls, and retain nonpassing qualification.
- [ ] `tools/tests/test_p1r_qualification.py` -- cover duplicate/missing/tampered inputs, unmeasured/zero counts, nonpassing required lanes, existing-output refusal, and real startup/success/timeout/SIGINT/descendant/repeated-cleanup controls. Counter or receipt errors must fail; tests must inspect actual process end-state.
- [ ] `_bmad-output/implementation-artifacts/evidence/6-1-p1r-qualification/README.md` -- document exact CLI use, index new bound receipts and explicitly pending package/runtime/database/container/owner gates.

**Acceptance Criteria:**

- Given only existing source evidence, when preparation runs, then required missing lanes remain nonpassing and `qualified=false`/`p1r_usable=false` are retained.
- Given altered hashes, omitted/duplicate cases or invented qualification, when validation runs, then packet validation fails with a safe reason.
- Given a required failed/skipped/unmeasured/incompatible lane, when preparation is validated, then its nonpassing disposition remains explicit; valid structure cannot become conformance.
- Given an interrupted owned process invocation, when cleanup repeats, then receipts survive, exact owned descendants disappear and shared process sentinels remain unchanged. These controls do not qualify container/database cleanup.

## Implementation Notes

- Investigation: runtime scope is done; dirty-tree continuation is authorized. Existing source-packet `sha256sum --check --status SHA256SUMS` returned 0; integrity does not qualify packages. Canonical spec/code/old packets were preserved.
- User approved the recommended preparation slice and continuation. The compact scope resolves the prior questions without selecting a published tuple or granting owner acceptance. No runtime code, package versions, Git staging/commits/pushes or external transitions are authorized by this slice.
- Pre-edit Aspire baseline completed by the parent: isolated HTTP start initially exited 2 because unsecured transport was unset; retry with `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` and `--no-build` started successfully. `aspire describe` showed core services healthy and both existing UI projects FailedToStart. `aspire stop --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` succeeded. These baseline observations are separate from Python verification; do not rerun or repair unrelated UI projects.

## Spec Change Log

## Review Triage Log

## Verification

- `python3 -m unittest discover -s tools/tests -p 'test_p1r_qualification.py'` -- positive contract controls and negative/tamper cases pass; real subprocess cleanup is observed.
- Run prepare/run/validate on fresh packets; retain actual control outputs/counts and nonpassing qualification. Independently test tampered copies.
- Observe Aspire before code edits and release only invocation-owned resources. No .NET code changes or broad test rerun are required by this Python slice.
