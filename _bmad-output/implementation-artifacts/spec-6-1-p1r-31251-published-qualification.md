---
title: '6.1-P1R published EventStore 3.125.1 qualification'
type: 'feature'
created: '2026-10-18'
status: 'in-progress'
approved: '2026-10-18'
approval_decision: 'approve-and-continue'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'd55c6f7c11e9bce6f17d490a84c83f1561979d63'
tracking_scope: 'qualification follow-up; no sprint-status mutation'
context:
  - '{project-root}/_bmad-output/specs/spec-6-1-p1r-remediation/qualification-contract.md'
---

<frozen-after-approval reason="human-owned qualification intent and execution selections">

## Intent

**Problem:** Historical 3.115.0 evidence does not qualify the latest EventStore. Fixtures enroll through 3.125.0, and the executor couples candidate and verifier checkouts.

**Approach:** Measure 3.125.1 using finite enrollment, separate verifier/source roots and isolated execution/audit.

## Boundaries & Constraints

**Always:** Select nuget.org Client, Contracts, DomainService, Server and ServiceDefaults 3.125.1, tag `v3.125.1`, source `55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b`. Bind actual signatures, archive hashes, repository metadata, assets/locks and loaded assemblies. Tag Builds is `4629ff2d3bd66376b29a553663c241d3a7b87853`; observe execution Builds separately. Freeze this observed candidate for the run. Retain Dapr 1.18.2, pinned PostgreSQL v1, comparison-only 3.70.1/3.110.0, rollback=null, both Reminder/evolution additions and `p1r-executed-checks-v1`. Use one writer, owned resources and Projects AD-17 drain/freeze/forward recovery. Keep P1R unusable and owner/conformance/support decisions pending unless genuine decisions are supplied.

**Never:** Rewrite historical evidence, replace published bytes, relax exact-tag source checks, substitute current production source, enable arbitrary versions, omit source comparison, invent disposition/acceptance, modify Projects/Builds/pins/sprint/readiness, publish, commit or push. Installed Dapr 1.18.7 supplies no inherited profile evidence. Do not execute dated historical audit entrypoints against new inputs.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Required behavior |
| --- | --- | --- |
| Published candidate | Exact signed family and clean tagged production source | Execute all seventeen scenarios and both additions; retain real outcomes and assertions. |
| Source substitution | Wrong tag, dirty production/build source, missing closure or changed source after a wait | Refuse dependent execution/import; retain diagnostics and cleanup. |
| Finite enrollment | 3.125.1 versus unenrolled 3.126.0 | Enable only the selected candidate; refuse the unenrolled seed before state reset/writer start. |
| Nonpassing evidence | Failed checks, runtime errors, unavailable source lane or missing owner disposition | Preserve nonpassing measurements; strict preparation/validation cannot grant qualification. |
| Interruption | Startup failure, timeout/cancellation and repeated cleanup | Prove owned resource absence and unchanged shared identities; retain every failed attempt. |

</frozen-after-approval>

## Code Map

- `tools/p1r_published_executor.py::{validate_execution_inputs,Executor.observe_inputs,build_consumers,seed,case}`: source/tag checks, package builds, finite snapshot execution.
- `tools/p1r_qualification.py::source_binding`, `tools/p1r_isolation.py`, `tools/p1r_isolation_evidence.py`: closure and private-runner identity/mount boundaries.
- `tools/p1r_published_qualification.py::{validate_packet,finalize_isolation_receipts,bind_lane,bind_recovery,receipt_outcome}` and `tools/p1r_check_witnesses.py`: independent import, strict isolation and snapshot witnesses.
- `tools/p1r-published-consumers/{Directory.Build.props,host/Host.csproj,domain/Domain.csproj}`: explicit capability/evolution/snapshot enrollment. Preserve historical conditions.

## Tasks & Acceptance

**Execution:**

- [x] `tools/p1r-published-executor.py`, `tools/p1r_published_executor.py`, `tools/p1r_qualification.py` — add explicit tagged-source selection, defaulting legacy calls compatibly. Build Debug/source consumers from that root; independently bind verifier closure and exact immutable candidate production/build closure. Keep selected tag/source equality and reject dirty/substituted candidate source.
- [x] `tools/p1r_isolation.py`, `tools/p1r_isolation_evidence.py`, `tools/p1r_published_qualification.py` — carry both roots/closures through private execution, receipts, finalization and independent validation. Use self-contained ordinary Git checkouts without shared metadata/alternates. Recheck identities after waits and before sealing; preserve legacy validation contracts.
- [x] `tools/p1r-published-consumers/{Directory.Build.props,host/Host.csproj,domain/Domain.csproj}`, `tools/p1r_published_executor.py`, `tools/p1r_check_witnesses.py`, `tools/p1r_published_qualification.py` — enroll exactly 3.125.1 for genuine snapshot/evolution execution and full isolation validation. Preserve historical version constants and measured support rules.
- [x] `tools/tests/{test_p1r_published_executor.py,test_p1r_published_qualification.py,test_p1r_snapshot_registration.py,test_p1r_isolation.py,test_p1r_isolation_hardening.py}` — verify the matrix through real callers, including rehashed root/source substitution, post-wait drift, missing witnesses and unenrolled candidates. Retain earlier enrolled coverage.
- [x] `tools/p1r-published-consumers/README.md` — document finite enrollment, separate roots and pending-decision behavior.
- [x] `_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run/` — retain fresh official observations and selected inputs, then execute canonical observe/run, measure-boundary and independent audit. Preserve literal commands, counts, errors, package proof, persisted recovery and cleanup. Attempt strict prepare/validate with genuine available inputs; retain missing-disposition refusal rather than fabricate a passing packet. Add byte-preservation attributes for new sealed evidence as needed.

**Acceptance Criteria:**

- Given a verifier checkout newer than the candidate, when 3.125.1 runs, then source comparison uses the exact tagged production source and independently validated closures identify both roles.
- Given complete fresh execution, when audited, then every required case and recovery/cleanup receipt is accounted for, all failed assertions remain visible, and execution errors prevent a passing result.
- Given pending owner decisions, when reporting results, then technical evidence and acceptance remain distinct and P1R usability stays false.

## Implementation Notes

- Approval resumed from clean `ce1eba0c459f4a00b2e555668e2e548af81f34f5`.
  Concurrent user work subsequently advanced the shared checkout to
  `fcf78d6cafb6c0c9f52dc350a9a09aff54ded288`. Its pinned-tooling route and
  unrelated commits, documentation, production code and gitlinks are preserved.
  The original planning baseline above remains unchanged.
- The selected fresh qualification runs in immutable separate verifier and
  exact-tag source checkouts. Current tooling also preserves the concurrently
  added optional `tooling`/`runtime_source` route. Their separate provenance is
  retained; this qualification selects the verifier/candidate closure route.
- Parent inspection covered implementation, test and documentation hunks from
  the frozen baseline and the merge against the measured producer. The
  [merged-source suite audit](evidence/6-1-p1r-31251-published-run/reconciliation/verification-ce6d5948928243f8a44978c7d1fef11a/parent-independent-audit.json)
  independently matches all 406 unfiltered, unskipped test executions to their
  literal logs and covers all five frozen matrix rows.
- The [source application receipt](evidence/6-1-p1r-31251-published-run/reconciliation/source-application-0cff688f268642a98eb1bed19d28d1cd.json)
  records all 17 target files matching their preserved pre-write hashes, exact
  verified output bytes, unchanged shared HEAD/index and zero application errors.
  The [owning-checkout audit](evidence/6-1-p1r-31251-published-run/reconciliation/verification-5de2722d6a404bdd982be7787f2869cd/parent-independent-audit.json)
  independently confirms all 406 checks passed again without skips or source
  drift, using its actual paths and initialized dependencies.
- A [separate merged-reader diagnostic](evidence/6-1-p1r-31251-published-run/reconciliation/retained-producer-compatibility-proof.json)
  reads all 19 lanes and both recovery receipts from the completed earlier V3
  run while preserving its three failed restore assertions and original rejected
  audit. This diagnostic supplies no qualification. The selected V4 run and its
  fresh private audit have now completed. A [current merged-reader diagnostic](evidence/6-1-p1r-31251-published-run/reconciliation/retained-producer-diagnostic-07a87c1c6a634dc4bbadbbbe1fca24bf/diagnostic.json)
  independently validates its 21 receipts while preserving every failed check.


- [Final canonical evidence](evidence/6-1-p1r-31251-published-run/final-attempt/canonical-v4-index.json)
  records observe, run and fresh audit exits zero. All 19 lanes and 163 cases
  executed: 4,804 main assertions, 4,795 passed and nine failed. Six failures are
  source snapshot-tail checks after the tenant-B seed's first append was refused;
  three are post-upgrade restore checks. Separate restore is 10/12; cleanup 7/7.
  Reminder passed 149/149 and logical evolution passed 193/193. No production
  cause or repair is claimed. Four later source seed checks were unattempted
  after the real first-append refusal; counts preserve that fact.
- The [parent raw audit](evidence/6-1-p1r-31251-published-run/reconciliation/selected-final-parent-audit-9895165b155e48f2852f55b91c9bea35.json)
  independently matches actual counters, hashes, all 21 receipts, exact tagged
  Debug consumer bytes and private/outer cleanup proofs. Fresh process controls
  passed 38/38. Strict prepare exited two for the missing measured owner
  disposition. Subsequent validation exited two for the incomplete invocation;
  boundary measurement remains pending disposition.
  All four owner decisions and conformance remain pending, with qualification
  and P1R usability false.
- [Shared-host preservation](evidence/6-1-p1r-31251-published-run/final-attempt/outer-shared-after-canonical-v4.json)
  remains false: final before13/after9, four removed with none added or changed;
  original before5/after9, two removed and six added. Owned-resource absence
  proofs do not establish whole-host preservation. No unrelated containers were
  excluded or intervened on to change these results.
- Concurrent user commits advanced the shared HEAD further to
  `6fccf5f235cebdcb2ae412fc03ee019904e611a7`; their evidence and gitlinks are
  preserved. An additional [SDK regression and repair](evidence/6-1-p1r-31251-published-run/reconciliation/sdk-compatibility-repair-8cf36a7818714a88a130acf05b3e3523.json)
  makes both source routes select the tagged SDK before MSBuild. The frozen V4
  selected route already had this behavior and remains unchanged. Full current
  merged-tooling verification is pending separately from the completed run.

## Spec Change Log

- 2026-10-18 UTC: Completed and verified the five tooling/documentation tasks;
  preserved the frozen intent and concurrent user work.
- 2026-10-19 UTC: Retained the completed fresh execution, audit and all failed
  checks; all six implementation tasks are complete. Qualification remains
  false. Final merged-source verification and review are in progress.

## Review Triage Log

## Verification

- Capture the Aspire baseline before code changes; report blockers separately.
- Run each of the six P1R Python discovery suites from `.github/workflows/p1r-qualification.yml` individually, plus optimized isolation hardening. Run actual Host/Domain/Probe project evaluations for enrolled and unenrolled versions.
- Execute fresh Release/package lanes and distinct Debug/tagged-source comparisons, private failure/cleanup drills, independent audit and strict import attempts. Require zero executor errors for successful execution. Keep owner-disposition refusal explicit.
- Run `git diff --check`; verify preservation of historical and unrelated inputs.

## Retained V4 causal follow-up, 2026-10-20

This is an additive diagnosis and proposal, with no new qualification invocation
or runtime/tooling repair. The [read-only diagnostic](evidence/6-1-p1r-31251-followup-20261020/diagnosis.json)
recounts all 19 lanes / 163 cases and verifies all 21 original receipt hashes:
main **4,795/4,804, nine failed**, separate restore **10/12**, cleanup **7/7**.
The recovery failures repeat the same failed append/restart observations; they
are not two additional runtime incidents. Original results and labels remain
unchanged. Command/audit exits zero establish completed measurement, not passing
qualification. The diagnostic rehashes all five retained published archives and
checks the exact tagged source against its recorded closure. Signature results
remain retained observations; this pass performs no fresh signature verification.

| Class | Evidence and causal finding | What remains unproven |
| --- | --- | --- |
| Verifier fixture defect | Command **4014** changes metadata floor to **5** while preserving `sourceProof.retainedFloor=1`. Decoded SQL hashes to `c03b45fefe2269d240c01e9b2aedf6763b1a63565a3f75b30037e6332f3e0538`, exactly the source/restored/appended/restarted metadata. Tagged `SourceProofCodec.IsValid` requires equal floors; tagged `EventPersister` rejects mismatches with `source_conflict`. Command **4038** refuses the append at `EventsStored`; restart **4055** still reports head 12. This invalid positive fixture explains three main and two recovery failures. | The raw exception message is redacted; the specific guard is a deduction from exact input and tagged code, not a newly captured exception. This does not establish a runtime defect or a successful valid-fixture append. |
| Verifier reporting defect | The failed `fresh-backup-restore-append-restart` case returns `outcome=effect`, `disposition=compatible` despite three failed checks. Its enclosing receipt correctly remains `execution=failed`, `compatibility=unverified`; recovery remains failed/incompatible. | Case labels cannot override failed checks or grant acceptance. No historical label is corrected in place. |
| Environmental seed failure | The source-comparison pair's failing command **3500** uses the **3.110.0 Release seeder**, before the tagged 3.125.1 Debug reader starts. Tenant B commits zero events. Sidecars **3501/3503** report SQLite name-registration `context deadline exceeded`; host **3502** records invocation HTTP 500. The later tagged hydration assertion at sequence zero follows that absent seed. These account for six main failures. Four later seed checks were never attempted. | Registration deadlines support a discovery/environment cause, but do not identify the underlying SQLite/host fault. The unequal seeds do not isolate a 3.125.1 source/package regression. Do not infer that a retry would pass. |
| Shared-host preservation failure | Recomputed final outer delta is **13→9**, four removed, none added/changed; original delta is **5→9**, two removed, six added. The four final removals do not overlap the run's recorded owned-container set. | The retained capture does not identify the actor responsible. Owned cleanup and private audit cannot establish whole-host preservation or excuse the observed removals. |
| Runtime defects | No production defect is isolated by these nine assertions. In particular, rejecting mismatched anchored metadata is the tagged writer's expected guard behavior. | Valid restore-plus-append and source equivalence remain unqualified. Other runtime defects are neither excluded nor repaired by this diagnosis. |

The diagnostic reads frozen verifier text from the V4 receipt and production
files from `55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b`, checking their recorded
hashes. It does not substitute today's executor or production source. Historical
SDK-selection and earlier import failures remain independent retained defects;
they do not explain away this selected V4 result.

### Proposed bounded next work

The [explicit proposal](evidence/6-1-p1r-31251-followup-20261020/proposal.json)
separates verifier preparation from candidate selection. First correct only the
new verifier's positive retained fixture, preserving the existing source
incarnation/head/message/digest and setting both retention coordinates coherently
inside the stopped, owned disposable fixture. Keep mismatched-proof and
invalid-floor negatives as refusals. Add checks before backup and after restore
that bind both floors, snapshot 9, head 12, exact retained event hashes and both
tenants. Correct future case outcome/disposition reporting from actual results;
preserve execution and compatibility as separate dimensions. Never weaken the
published writer's source-proof guard to admit this fixture.

Investigate private discovery registration and capture both seed tenants before
attempting source equivalence. Diagnose locks, registration and writer shutdown
with task-owned controls; no shared host setting changes, silent retries, larger
budgets or omitted checks. Establish an exclusive host window or separate host
that can satisfy the unchanged outer preservation assertion. Keep attribution
unknown unless new evidence identifies it. These are preparation proposals,
not implemented repairs or a new run authorization/selection.

Only after focused verifier controls and valid-fixture preparation pass should
a separately named execution be considered. A 3.125.1 run must retain these same
signed archives, `v3.125.1`, exact source and tagged Builds, with a separately bound
new verifier; use Dapr **1.18.2**, PostgreSQL v1, comparison-only 3.70.1/3.110.0,
both additions and rollback=null. Installed 1.18.7 supplies no replacement
evidence. No runtime repair to 3.125.1 is proposed from V4. If a valid input
isolates a runtime defect requiring source changes, prepare an explicit successor
with new immutable package/tag/source/Builds coordinates; never rebuild 3.125.1
or compare its published bytes with current production source.

### Story 6.4 verified-origin API is a separate qualification target

The tagged 3.125.1 `VerifiedQueryCaller` and `QueryCallerAuthority` lack
`OriginatingContext`; the presence of the older verified-caller feature does not
provide the newer API. The later implementation is retained in commit
`6c56c27bc1a8d63c83f9631f7162cf79fdde0a1e` and the
[separate prerequisite report](6-4-eventstore-query-authority-prerequisite-report.md).
Its 422 focused Debug/source checks are bounded source evidence. They neither
qualify 3.125.1 nor establish immutable published consumption or parent Story 6.4
acceptance.

The proposal therefore leaves the newer package version, tag/source binding,
publication authority and owner decisions **unselected/pending**. Before its
qualification, select and verify a new exact published family containing the
query-binding signing repair, `OriginatingQueryContext`, gateway forwarding,
SDK-verified origin separate from owner-hop identity, and the selected proof
limits. Retain the full P1R envelope and add dedicated packaged origin/signature,
serialization, tampering, scope/audience/resource, current-owner-denial and legacy
compatibility checks. Passing the older P1R query-wire cases cannot establish
this API. Any necessary publication remains separately authorized work.

All four P1R owner decisions and same-baseline conformance remain pending.
Qualification and P1R usability remain **false**; no support/readiness, consumer
pin, Projects/sprint, owner acceptance or maintenance-cutover transition follows.

### Follow-up verification boundary

`python3 .../6-1-p1r-31251-followup-20261020/inspect_v4.py` and its `-O`
invocation reproduce the same diagnostic bytes. The
[closing preservation check](evidence/6-1-p1r-31251-followup-20261020/closing-verification.json)
rechecks all **4,880** historical files (**1,852,650,799 bytes**), protected
production/tool/test inputs, the frozen intent and original wrapper bytes.
Only this appended wrapper text, a scoped byte-preservation attribute and the
new sibling forensic packet were written. No historical entrypoint was executed.

The [initial preservation check](evidence/6-1-p1r-31251-followup-20261020/verification.json)
retains its failed whole-worktree equality result: one of 570 pre-existing dirty
files, `evidence/story-3-21/qualification-2026-10-20/index.json`, changed during
the capture window. The closing record preserves both hashes and leaves that
unrelated file untouched; it does not claim whole-worktree equality. Local diff
and text checks pass. These checks validate this forensic handoff only, with no
new runtime, package-signature, owner or qualification result.
