---
title: '6.1-P1R published EventStore 3.115.0 qualification run'
type: 'feature'
created: '2026-10-07'
status: 'in-review'
approved: '2026-10-07'
approval_decision: 'approve-and-stop'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: '98da5a04e6df33ba026cbaae46d1777acdca7a21'
tracking_scope: 'qualification follow-up; no Projects or sprint-status changes'
context:
  - '{project-root}/_bmad-output/specs/spec-6-1-p1r-remediation/qualification-contract.md'
---

<frozen-after-approval reason="human-owned qualification scope; execution selections accepted on 2026-10-07">

## Intent

**Problem:** The completed harness has no real published-package or operational qualification evidence. Source remediation and packet validity cannot establish P1R compatibility.

**Approach:** Execute the canonical seventeen scenarios/seven families and selected additions against immutable packages; import measured receipts into a fresh validated packet. Use the accepted execution selections below; keep acceptance decisions and conformance pending.

## Boundaries & Constraints

**Always:** Separate execution from compatibility and owner acceptance. Use fresh isolated Release/package consumers, `CI=true`, isolated `NUGET_PACKAGES`, exact assets/lock graphs, actual signatures and physically loaded DLL hashes. Keep canonical version directions and required operations explicit. Bind the actual executor checkout separately from package repository commits and candidate Builds provenance. Retain every failed, unavailable, skipped, incompatible or unmeasured portion as nonpassing. Use one writer, real backup/fresh-database restoration, persisted inventories and repeated owned cleanup. Preserve Projects AD-17 mutation freeze/forward recovery and `p1r_usable=false`.

**Never:** Invent decisions/counts, rewrite sealed evidence, republish/rebuild existing versions, modify Projects/Builds/pins/acceptance/readiness/sprint tracking, initialize nested submodules, commit/push/publish, or substitute self-roundtrips for missing cross-version execution. Runtime application state access remains Dapr/actor-owned; provider backup/provisioning and bounded fixture diagnostics have separate purposes.

## I/O & Edge-Case Matrix

| Input/state | Required behavior |
| --- | --- |
| Retained database | Fresh restore of floor 5/head 12/snapshot 9 reconstructs 12; append 13, stop writer, restart and replay reconstruct 13; preserve prior hashes, snapshot and second tenant. |
| Invalid evidence or incapable historical operation | Measure refusal/effect and unchanged domain inventories; unsupported/incompatible results cannot pass qualification. |

## Settled Execution Selections

The user accepted all six recommendations on 2026-10-07. These authorize execution inputs and Test-owner instrumentation, not the four owners' eventual qualification acceptance.

1. Candidate: nuget.org 3.115.0, tag `v3.115.0`, commit `283b07a52c9c70e1c940164a7011ee8c3ad98b2d`; Client/Contracts/DomainService/Server/ServiceDefaults with actual archive/content hashes and repository commits in the run's `planning-observations.json`.
2. Builds: untagged `4.29.1-22-gba4ca78`, commit `ba4ca78c3868a4757cb92d912a54c8a237871b54`. Keep workspace Builds 4.30.0 unchanged and separate.
3. Rollback: `null`; retain approved Projects AD-17 mutation freeze/forward recovery.
4. Profile: invocation-owned Redis at the observed registry digest indexed in the run README, Dapr runtime 1.18.4; preserve shared containers. PostgreSQL/Dapr 1.18.2 receives no inherited evidence.
5. Test selections: both `reminder-recovery` and `logical-event-evolution`; accepted mechanism `p1r-executed-checks-v1` records every executed Boolean check by stable ID, including failures, and derives counters from those records.
6. Comparisons: narrowly bind actual published 3.70.1 and 3.110.0 inputs/evidence for canonical directions. These are comparison packages only; rollback remains null and their actual incompatibilities remain nonpassing.

</frozen-after-approval>

## Code Map

- `tools/p1r_published_qualification.py` — strict input, independently verified package roles and measured lane binding, including comparison-only archives.
- `tools/p1r_qualification.py` — exact source closure and strictly verified root-declared Builds observation through the owning or enclosing workspace repository.
- `evidence/6-1-p1r-3110/verification/{run_verification.py,host,domain,probe}` under implementation artifacts — read-only patterns; historical coordinates, PostgreSQL topology and literal counters cannot be reused unchanged.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/{Events,Integration,Fixtures}` — persistence, Reminder, evolution and ownership patterns; source tests and logical fixture restore do not qualify published recovery.

## Tasks & Acceptance

**Execution:**

- [x] `tools/p1r_published_qualification.py`, `tools/p1r_published_executor.py`, `tools/tests/test_p1r_published_executor.py` — independently reject contradictions between case checks and retained check witnesses/probe measurements; enforce stable required checks without silently dropping failures. Retain each predicate source file once with its closure hash, so independent validation parses sealed source bytes and does not depend on the later live checkout. Recompute persisted inventory hashes from retained observations, and require runtime configurations for cases that execute runtime commands. Bind rendered configuration paths/content to the actual sidecar commands; wire/provenance-only cases legitimately need none. Negative controls must flip failed metadata checks, invent inventory hashes, remove runtime configurations, and substitute executor source while recomputing outer hashes.
- [x] `tools/p1r_published_executor.py`, `tools/p1r-published-executor.py`, `tools/tests/test_p1r_published_executor.py` — use existing nonce/PID/start-time ownership for synchronous command trees as well as long-running nodes, including detached descendants on timeout/cancellation. Make partial initialization transactional and retain refusal/cleanup evidence. Interrupted Docker creation must recover only exact generated-name/invocation-label container identity even if no cidfile was written; continue repeated cleanup without touching shared resources.
- [x] `tools/p1r_published_executor.py`, `tools/p1r-published-consumers/{host,probe}/`, `tools/tests/test_p1r_published_executor.py` — compare each declared shared source case with its corresponding actual selected-package case and retain normalized semantic outputs/inventory coordinates and measured equivalence or delta. Differences, unavailable comparisons and incomplete shared operations remain nonpassing. Assert replay current sequence, exact expected event sequence sets and snapshot coordinates. After each supported metadata append, restart its actual writer, replay 13 and recheck floor/prior hashes/second tenant; historical incompatibility stays incompatible.
- [x] `tools/p1r-published-consumers/host/QualificationCapabilities.cs`, `tools/p1r_published_executor.py`, `tools/tests/test_p1r_published_executor.py` — classify only the existing expected fence/proof denial as successful refusal and retain unexpected exceptions as failed execution; reject unknown fixture operations. Include nonnull IdentityAdmissionProof in bounded query wire preservation, without implying P2 authentication acceptance. Require both initial and restart Reminder convergence submitted=0 for natural delivery attribution; bound every observation call to the remaining deadline and reject late completion. Preserve the actual no-direct-submission historical result.
- [x] `.github/workflows/p1r-qualification.yml`, `.gitattributes`, `tools/tests/test_p1r_qualification.py`, `tools/tests/test_p1r_published_executor.py` — run executor tests in regular CI; make Builds fallback tests independent of the present checkout and test both nested/workspace observation routes; preserve checksum-bound text as LF and retained binary bytes as binary.
- [x] `evidence/6-1-p1r-31150-published-run/` — after corrected source is final, run a fresh full invocation and prepare/independently validate a new immutable packet, with all real failures retained. Keep every existing sealed execution unchanged and identify its superseding run. Re-run focused suites, Release/Debug comparisons, strict restore and cleanup and negative controls; recompute counts rather than copying prior totals.

- [x] `tools/p1r_qualification.py` and `tools/tests/test_p1r_qualification.py` — support strictly verified workspace-root-declared Builds observation without nested initialization; retain actual source/Builds identity and refuse substitutions.
- [x] `tools/p1r_published_qualification.py`, `tools/p1r-published-qualification.py`, `tools/tests/test_p1r_published_qualification.py` — implement selected comparison policy; independently verify comparison archives/graphs/binaries without making them rollback or granting acceptance.
- [x] `tools/p1r_published_executor.py`, `tools/p1r-published-executor.py`, `tools/p1r-published-consumers/` — create isolated Host/Domain/Probe consumers and orchestrator; execute canonical case inventory and selected additions with measured receipts and persisted state checks.
- [x] `tools/tests/test_p1r_published_executor.py` — test false counts, missing direction, substitutions, interruption and continued cleanup; tooling fixtures confer no operational pass.
- [x] `evidence/6-1-p1r-31150-published-run/` under implementation artifacts — write owner inputs only after selections; retain package and operational receipts; prepare/validate new packet with `--inputs`, evidence directories and receipts; index outstanding owner decisions in README.

**Acceptance Criteria:**

- Given selected inputs, when consumers execute, then every required lane has its real disposition; omissions remain nonpassing.
- Given restore/append/restart, when inventories are queried, then they meet `tools/p1r_qualification_runtime.py`'s exact contract.
- Given startup/failure/timeout/cancellation drills, when repeated cleanup completes, then owned resources are absent and shared resources remain unchanged.
- Given incomplete owner decisions, when independent validation succeeds, then technical qualification is recomputed true or false and decisions/usability remain false.

## Implementation Notes

Review iteration 1 re-derives the owned source after reverting it to the preserved
pre-execution state. The prior implementation's source-only KEEP snapshot is
`/tmp/p1r-review-keep-wiucb_sp`; its manifest identifies the exact owned paths.
Read those ordinary source files as implementation material and restore the
positive behavior below before correcting the listed gaps. Do not load skills
from `references/`. The existing complete execution remains immutable historical
evidence; its counts below describe that execution, not the forthcoming corrected
source closure. Do not rewrite or reseal it.

KEEP: exact accepted package/runtime/comparison selections and refusal of
substitutions; strictly verified workspace-root Builds fallback; independent
archive/signature/graph/physically-loaded-binary identity for all three package
versions; isolated nine Release consumers and distinct three Debug/source
consumers; safe Docker inspection without Env; one writer, real Redis backup to a
fresh database, Dapr-owned application access, second-tenant preservation;
all seventeen scenarios, both selected additions, every actual failed check and
historical incompatibility; honest missing registered logical evolution;
mutation freeze, no capable rollback, pending owners/conformance and false
qualification/usability; original protected files, shared containers, frozen
intent and pre-existing user changes. Existing tests and exact source closure
binding must survive. Preserve safe diagnostic redaction and original seals.

Implemented strictly observed workspace-root Builds fallback, comparison-only
archive/graph/binary policy and the invocation-owned published Host/Domain/Probe
executor. Exact approved selections are refused on substitution. Measured case
checks retain failures and derive every counter; source closure and rendered
configuration bytes are bound independently from package/tag/Builds provenance.
Docker discovery captures only preservation fields and the invocation label.

The fresh [execution index](evidence/6-1-p1r-31150-published-run/execution-325e3c9327df4810941981b5882e3a70/README.md)
records all seventeen scenarios/seven families and both additions: 163 cases,
4,909 checks, 4,819 passed and 90 failed. Actual Release package lanes passed
267/267; distinct Debug/source comparison passed 329/329, strict candidate
restore 12/12, owned cleanup 7/7, natural Reminder delivery 125/125. Pre-upgrade
restoration restarted real old hosts and replayed both tenants, but remains
incompatible containment with RPO-zero false. Registered logical alias/evolution
execution is missing: live registration observations and three failed checks
keep the selected addition incompatible; retained legacy readback does not
substitute for it.

Fresh prepare and separate independent validation exit 0 with `valid=true`,
`technically_qualified=false`, `decisions_complete=false`, `qualified=false`,
`p1r_usable=false`, and no capable rollback. Four owners and same-baseline
conformance remain pending. Source HEAD is `4e4ee8587ee1f6e73f704cd548734e963b2af3e9`;
the exact executor closure also binds current dirty/untracked source bytes.
Earlier trials remain private outside the repository; newly safe derived
artifacts preserve original hashes and exclusions without rewriting original
seals. No Projects/Builds/pins/readiness/acceptance/sprint changes were made.

Review iteration 1 preserved a separate
[failed invocation](evidence/6-1-p1r-31150-published-run/failed-execution-6f89354956c24577ae1404cddb78bde9/README.md).
Its Debug/source restore command 3802 exited 0, but its first owned cleanup
recorded `OSError` with no remaining processes; the second attempt was clean.
That first error remained a refusal, source comparisons were unavailable, and
the invocation was interrupted with repeated final cleanup passing 7/7.
Eight fresh source builds, eighty actual source restores and both descendant
stress trials did not reproduce the generic error. Its original errno was not
established. The narrow diagnostic change preserves the existing error list
and separately retains operation/type/errno and exact owned signal/reap
identity. No error is ignored or reclassified; a regression proves that a clean
second attempt cannot erase the first failure. Focused verification now passes
112 tests. A new complete invocation must bind this final diagnostic closure;
none of the failed invocation's receipts may substitute for fresh execution.

The next [failed invocation](evidence/6-1-p1r-31150-published-run/failed-execution-ad19b44dd4d44796b09e2f62c9388046/README.md)
demonstrated a specific disappearing-process race: inventory command 1528
retained valid JSON followed by `ProcessLookupError` (errno 3), recorded exit
127, and two empty/error-free cleanup attempts. Its final repeated cleanup
passed 7/7. `process_state` now treats only `FileNotFoundError` and
`ProcessLookupError` during procfs stat observation as an absent process.
The regression proves that permission errors and other read failures still
propagate. This demonstrated guard does not establish the first invocation's
unknown errno, and neither failed invocation supplies the final run's receipts.

The [early failed invocation](evidence/6-1-p1r-31150-published-run/failed-execution-90b6f9b1ad3b41feabd670f273d0a027/README.md)
retained an exact `signal-owned` errno 22 for an already exited build root.
`signal_owned` now skips absent/reused identities before `pidfd_open`, while
retaining the kernel-handle/start-identity recheck before signalling. An errno
22 from opening the handle is skipped only if a fresh observation proves that
stored identity is gone/reused. Matching-live errno 22, permissions and other
errors remain refusals. The regression covers disappearance, reuse and the
mandatory final recheck without signalling an unowned reused PID.

The complete [next execution](evidence/6-1-p1r-31150-published-run/execution-b944559fd0f3413696db48bcfc585f52/README.md)
retains 163 cases, 5,522 checks, 5,412 passed and 110 failed, with strict restore
12/12 and cleanup 7/7. Execution exited 0; preparation correctly remains refused
with `JSONDecodeError` because the importer parsed a non-JSON startup GET retry
before a later successful status/readiness response. Its complete execution and
sealed refusal packet remain unchanged. The narrow correction retains every
failed command and binds HTTP observations only to successfully parsed JSON from
an actual exit-0 command. Regressions reject malformed successful responses and
matching JSON from failed commands. A private tooling-only rederivation of every
original case validates against its unchanged retained closure; it confers no
qualification and does not rebind those receipts. Exact closure equality requires
another fresh canonical invocation from the final persistent source.


Review iteration 1 is complete. The [corrected fresh execution](evidence/6-1-p1r-31150-published-run/execution-c497b88cb2e5436ebddb37e33f1fcb64/README.md)
retains all nineteen scenario/addition receipts: 163 cases, 5,522 checks,
5,412 passed and 110 failed. Nine isolated Release consumers independently
pass 267 package checks; three distinct Debug/source consumers execute all
eleven paired cases with 704/704 checks. Strict restore passes 12/12 and repeated
owned cleanup 7/7. Reminder passes 131/131, with initial and restart submitted=0
and natural sequence 13 measured within the finite deadline before callbacks.
Real stale transport and trusted-effect denial-audit-unavailable errors retain
specific bounded diagnostics and after-inventories as nonpassing. The missing
registered logical evolution, wire losses and historical incompatibilities
remain explicit. Rollback, owner/conformance decisions and downstream gates
remain unchanged and qualification/usability remain false.

The [authoritative retained packet](evidence/6-1-p1r-31150-published-run/packet-c497b88cb2e5436ebddb37e33f1fcb64/packet.json)
was prepared directly at its permanent path. [Preparation](evidence/6-1-p1r-31150-published-run/review-iteration1-final-verification/prepare.json)
and [independent validation](evidence/6-1-p1r-31150-published-run/review-iteration1-final-verification/validate.json)
exit 0, recomputing valid=true, package assertions 267 and process controls 38.
The exact final source closure is
`04ca7c61e729ff7154472bd1a6a9fcdc4b4073b940db2aa3f4530abd6f1e6a38`.
Focused verification passes 115 tests; all eleven resealed negative controls
are refused at both import and independent validation with matching reasons.
The already-sealed execution and its relocated packet copy remain unchanged;
that copy's process-control cwd binding correctly refuses validation at the
changed path. The retained-path preparation corrects only the output location,
imports only the successful fresh invocation's unchanged receipts and does not
relax or rebind source. Final review verification retains this exact diagnostic.

## Spec Change Log

- 2026-10-07, review iteration 1: BH1/BH2/BH3 exposed missing independent
  check/inventory/runtime consistency requirements; BH4/EH9 exposed incomplete
  ownership during interrupted commands/container creation; BH8/BH10/BH11
  exposed missing supported-write restart, specific denial classification and
  paired source/package comparison. Amended only non-frozen implementation
  tasks to require those proofs, all related direct corrections and CI
  regressions. Reverted only this run's owned source paths to the pre-execution
  state, preserving a source-only KEEP snapshot and all sealed evidence. Avoid
  accepted contradictory checks, surviving owned descendants, inferred security
  refusal or Scheduler attribution, and unmeasured equivalence. KEEP the positive
implementation behavior enumerated above; all earlier constraints remain.

- 2026-10-07: Completed the approved execution tasks and recorded measured
  results outside the unchanged frozen intent. Qualification and downstream
  acceptance remain pending/nonpassing.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH1 | medium | bad_spec | Measured-check consistency: Flipping four metadata-read failures while retaining contradictory observations is accepted because only counters/hashes are verified. Independent validation needs retained check witnesses and probe-result consistency. |
| BH2 | medium | bad_spec | Inventory binding: The executor derives hashes from observations, but the importer never rederives them; invented equal hashes can make refusal preservation pass. |
| BH3 | medium | bad_spec | Runtime configuration binding: Full-replay operations can omit every configuration because an empty default list is accepted. Runtime command/configuration requirements must be derived from executed operations. |
| BH4 | medium | bad_spec | Command process ownership: run uses subprocess.run while cleanup tracks only launch; timed-out detached descendants were reproduced alive. All executed command trees need the existing exact ownership mechanism. |
| BH5 | low | patch (moot) | Initialization cleanup: Source/Docker discovery can fail after scratch creation while the caller has no worker reference. Transactional initialization must remove owned scratch and retain partial refusal/cleanup. |
| BH6 | false | reject | Projection XML coverage: The adopted finite projection-wire scenario requires both version directions, which the JSON cases execute; only query-wire explicitly requires JSON and DataContract roundtrips. The cited projection row does not require a second transport format. |
| BH7 | medium | patch (moot) | Query proof transport: Dual-principal wire values omit the contract's IdentityAdmissionProof, so dropping that member cannot fail the field-preservation test. A nonnull bounded wire value is required; P2 acceptance remains separate. |
| BH8 | medium | bad_spec | Supported metadata-write restart: Comparison append cases capture floor/head but do not restart and rehydrate the appended writer. The adopted metadata-write family explicitly requires restart proof for supported effects. |
| BH9 | medium | patch (moot) | Replay coordinates: A matching Counter fold and unchanged inventories do not assert the expected current sequence/event set/snapshot coordinate. The full-replay contract independently requires the sequence. |
| BH10 | medium | bad_spec | Expected refusal classification: Every exception satisfies stale/forged/unauthorized refusal; the existing fence/proof paths have distinguishable denial reasons. Infrastructure failures must remain failed execution. |
| BH11 | medium | bad_spec | Paired checkout comparison: Source runs substitute source for one package but never compare the corresponding selected-package observations. The checkout contract requires measured equivalence or actual deltas over the declared shared scope. |
| BH12 | medium | patch (moot) | Evidence byte preservation: The new checksum-bound tree inherits text=auto without eol; autocrlf can change JSON/checksum bytes and invalidate independent validation. Existing sealed evidence trees already use explicit LF/binary rules. |
| EH1 | medium | bad_spec; grouped BH1 | Measured-check consistency: The importer accepts check-to-evidence contradictions, reproducing BH1; the retained commands remain unchanged while failures become pass claims. |
| EH2 | medium | bad_spec; grouped BH4 | Command process ownership: The timeout path does not register command descendants and cleanup cannot target them; the review reproduced and removed its surviving test descendant. |
| EH3 | medium | patch (moot) | Reminder delivery attribution: Restart convergence can submit an overdue effect directly, but the executor ignores submitted. The captured run has submitted=0; future runs must assert that field before claiming natural Scheduler delivery. |
| EH4 | medium | bad_spec; grouped BH3 | Runtime configuration binding: The runtime profile exists on the lane, but required operational configuration records can be removed with a recomputed hash. No upstream guard requires them. |
| EH5 | medium | bad_spec; grouped BH10 | Fence denial classification: The fenced negative control catches Exception without checking the expected stale/invalid-fence denial, so transport or implementation failures can qualify refusal. |
| EH6 | medium | bad_spec; grouped BH10 | Proof denial classification: The unauthorized-effect negative control catches Exception without checking the gateway-proof denial, so unrelated actor failures can qualify refusal. |
| EH7 | low | patch (moot) | Unknown fixture operation: The qualification route accepts any operation string; an unknown value falls through to trusted-effect submission. Explicitly reject names outside the finite fixture operation set. |
| EH8 | medium | patch (moot) | Builds provenance route: source_binding legitimately omits workspace_builds when the repository has initialized Builds, but comparison observation unconditionally indexes that key. Select the already-bound owning Builds repository. |
| EH9 | medium | bad_spec | Container creation ownership: A created container can precede cidfile persistence when Docker create is interrupted; cleanup sees only recorded IDs. Recover ownership by exact generated name plus invocation label before cleanup. |
| EH10 | medium | patch (moot) | Reminder observation deadline: The thirty-second polling loop invokes probes with the default 180-second timeout and accepts sequence 13 without checking the time after the call. Bound each call to remaining time and retain late completion as nonpassing. |
| VG1 | medium | patch (moot) | CI executor coverage: Preverified: P1R CI selects only the two older test modules and omits executor regressions. Add the new suite to the regular job. |
| VG2 | medium | patch (moot) | Fallback regression fixture: Preverified: fallback assertions skip when nested Builds is initialized, including the normal CI configuration. Use a controlled disposable repository fixture so the provenance boundary always runs. |
| VG3 | medium | patch (moot) | Source-substitution regression: Preverified: no measured-receipt test changes executor_source while keeping outer hashes consistent. Add rejection coverage for that exact substitution. |

## Verification

- Focused Python suites; actual Release consumer and distinct Debug/source comparison runs; restore/cleanup drills; fresh published-harness prepare/validate; `git diff --check`.
- Retained preflight: five archives restored/signature-verified. Aspire startup exits 2 for missing nested Tenants; preparation/validate exit 2 for absent nested Builds. Initialize neither.

Historical verification receipts before review iteration 1:

- [92 focused Python tests](evidence/6-1-p1r-31150-published-run/execution-325e3c9327df4810941981b5882e3a70/verification/focused-tests.json): exit 0.
- [Full fresh execution](evidence/6-1-p1r-31150-published-run/execution-325e3c9327df4810941981b5882e3a70/run.json): exit 0, no executor errors; every lane retains its real compatibility disposition.
- [Prepare](evidence/6-1-p1r-31150-published-run/execution-325e3c9327df4810941981b5882e3a70/prepare.json) and [independent validation](evidence/6-1-p1r-31150-published-run/execution-325e3c9327df4810941981b5882e3a70/validate.json): exit 0; packet valid, qualification/usability false.
- [Source/configuration/direction negative controls](evidence/6-1-p1r-31150-published-run/execution-325e3c9327df4810941981b5882e3a70/negative-controls.json): all four substitutions refused at import and independent validation; tooling scope only.
- [Diff check](evidence/6-1-p1r-31150-published-run/execution-325e3c9327df4810941981b5882e3a70/verification/diff-check.json): exit 0.
