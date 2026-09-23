---
title: 'Story 4.15 OQ8 Lifecycle Closure v4'
type: 'bugfix'
created: '2026-09-20'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '5ab1d01908acbecba9258667f76f670e4549956b'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 4.15 cannot reach a truthful terminal lifecycle: full validation requires sprint `review` plus spec `done`, and the checked-in Contracts probe preserves that transitional pair forever. Changing only the sprint row to `done` makes the default/final validators and Contracts lane red even when the sealed packet is valid.

**Approach:** Separate evidence validity from lifecycle postconditions. Preserve v3 byte-for-byte as historical evidence, issue a freshly reviewed v4 successor for the changed validator/tests/frozen contract, and use one bounded lifecycle-state record to select exact ready-to-close (`review`/`done`) or closed (`done`/`done`) tracking after complete packet validation.

## Boundaries & Constraints

**Always:** Validate the complete active v4 packet before lifecycle state in default mode. Keep `candidate` = sprint `in-progress`/spec `in-review`, `final` = `review`/`done`, and `closed` = `done`/`done`. Bind the lifecycle record to the active v4 directory, manifest, and review subject; keep that mutable record and sprint tracking outside the sealed v4 subject. Obtain fresh architecture, security, and test approvals over one unchanged v4 subject before activation. Run default, final, historical v1/v2/v3, and full Contracts while ready-to-close; only then transition the record and sprint row together, followed by default, closed, historical, and full-Contracts verification.

**Never:** Modify Story 4.14 evidence or any file under the sealed v3 successor; reuse or fabricate prior approvals; let an isolated lifecycle mode claim evidence approval; bind mutable lifecycle/selector/sprint bytes into v4; change Epic 4 from `in-progress`; grant release, package, registry, deployment, runtime-pin, consumer-migration, external-repository, final-consumer, Folders-closure, or broader readiness authority; alter unrelated deferred-work entries or other frozen Story 4.15 content.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Ready to close | Valid v4; record `ready-to-close`; sprint/spec `review`/`done` | Default and `final` pass | Any record/hash/status mismatch fails closed |
| Closed | Same valid v4; record `closed`; sprint/spec `done`/`done` | Default and `closed` pass | `final` no longer passes after closure |
| Status-only flip | Record ready; sprint `done` | Reject lifecycle mismatch | Name Story 4.15 lifecycle drift |
| Gamed closure | Record/status closed; v4 evidence invalid | Reject evidence before lifecycle success | Preserve bounded evidence diagnostic |
| Historical lineage | v3 files unchanged; validator/tests evolved in v4 | v1/v2/v3 historical modes pass | Missing/drifted predecessor fails closed |

</frozen-after-approval>

## Code Map

- `tools/validate-oq8-platform-evidence.py` -- replace the final Boolean with explicit phases; validate the bounded lifecycle record; preserve v3 through the completed-v3 Git snapshot; add active-v4 and selector-v3 validation with lifecycle last.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs` -- add closed-phase, lifecycle-record, anti-gaming, historical-v3, and current-repository full-validation coverage; do not alter sealed v3 counts.
- `_bmad-output/implementation-artifacts/evidence/story-4-15-successors/v4/` -- new immutable ten-file successor with v3 predecessor identities, current gate inputs, limitations, execution, one subject, three fresh receipts, handoff, validator record, and manifest.
- `_bmad-output/implementation-artifacts/4-15-oq8-platform-closure-successor.json` -- selector schema v3: v3 becomes historical and v4 becomes active; authority remains unchanged.
- `_bmad-output/implementation-artifacts/4-15-oq8-platform-lifecycle-state.json` -- exact bounded mutable state record; binds v4 manifest and subject without serving as evidence approval.
- `_bmad-output/implementation-artifacts/spec-4-15-oq8-platform-closure-and-handoff.md` -- human-approved replacement of only the final sentence in frozen `Always`; frontmatter remains `done`.
- `_bmad-output/implementation-artifacts/{deferred-work.md,sprint-status.yaml}` and directly related Story 4.15 lifecycle prose in `planning-artifacts/{epics.md,prd.md}` -- settle DW-497/DW-505 and reconcile terminal status without changing other gates or authorities.

## Tasks & Acceptance

**Execution:**
- [x] Validator and Contracts tests -- implement phase/record/v4 lineage and prove malformed, ambiguous, mismatched, oversized, symlinked, and evidence-drift cases fail closed.
- [x] Frozen parent contract -- apply exactly the approved `Always` replacement and no other frozen edit.
- [x] V4 candidate and reviews -- bind completed v3 commit `d578e7626df1d8afeada440c7989eb04ba2bdfe6`, generate the candidate, run receipt-independent gates, and obtain fresh unchanged-subject architecture/security/test approvals before sealing and selector activation.
- [x] Pre-close and transition -- run every required validator and Contracts gate at `ready-to-close`; then change lifecycle record to `closed` and sprint status to `done` together.
- [x] Settlement -- rerun post-close gates, mark only DW-497/DW-505 done with resolutions, and reconcile direct Story 4.15 planning statements while Epic 4 and external/readiness authority remain unchanged.

**Acceptance Criteria:**
- Given a valid active v4 and ready-to-close record, when pre-close verification runs, then default, final, historical v1/v2/v3, focused OQ8, and full Contracts pass without changing sprint `review`.
- Given pre-close verification passed, when record and sprint transition to `closed`/`done`, then default, closed, historical modes, the checked-in full-validation probe, and full Contracts pass.
- Given lifecycle fields are changed without valid v4 evidence or disagree with the record, when validation runs, then closure fails before any completion claim.
- Given final repository state, when v3 hashes and authority fields are inspected, then v3 is byte-identical and every non-EventStore-platform authority remains false.

## Implementation Notes

- Added explicit `candidate`, `final`, and `closed` lifecycle phases plus the bounded six-field lifecycle record. Default validation proves the complete active packet and selector before lifecycle; isolated lifecycle modes validate record/selector metadata and tracking only.
- Preserved v3 against completed commit `d578e7626df1d8afeada440c7989eb04ba2bdfe6` and activated the corrective v4 subject `7e6a393ece18b322a01157a8b60ea37ede7d8d142d10a4fa65f08c7c35b276cf` with manifest `a3cd51c6e789715c95194ea4a3c31614ce2f70dec7c099fd5fb2f48e562065f4`.
- Fresh architecture, security, and test reviews approved the unchanged final subject. Review feedback corrected selector-dependent pre-review provenance, execution chronology, the full-suite count, and lifecycle-only evidence isolation before sealing.
- Transitioned the lifecycle record and sprint row together to `closed` / `done`; Epic 4 remains `in-progress` and every non-EventStore platform authority remains false.

## Spec Change Log

- 2026-09-20: Implemented lifecycle closure v4, sealed fresh evidence, completed pre-close and post-close gates, and reconciled bounded Story 4.15 planning/deferred-work state.
- 2026-09-21: Corrected selector/lifecycle identity coupling, expanded focused fail-closed mutations, added the parent-story supersession note, and resealed fresh unchanged-subject reviews.

## Review Triage Log

- Architecture review initially rejected selector-dependent pre-review claims and later rejected stale execution chronology; both were corrected and all receipt-independent commands rerun before the final subject freeze.
- Test review rejected an inferred full-suite count of 2067; independent discovery established and sealed the exact 2072-case count.
- The first focused run found three test expectation/probe mismatches; corrected tests were rebound and freshly approved before final execution.
- The corrective review findings BH-02, BH-06, BH-10, BH-12 through BH-14, ECH-01, ECH-03, VG-01, and VG-02 were implemented; fresh architecture, security, and test reviews approved one unchanged replacement subject.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH-01 | false | reject | Historical-v3 mode intentionally validates the pinned v3 directory and completed-v3 Git snapshot without consulting the mutable live selector; it explicitly grants no current-source authority. |
| BH-02 | low | patch | Isolated lifecycle validation is intentionally non-authorizing, but matching null or malformed selector/record identities can currently pass; validate identity shape while preserving isolation. |
| BH-03 | false | reject | `final`/`closed` modes explicitly report that evidence was not evaluated; authority and historical-selector approval belong to default validation, which checks both before lifecycle. |
| BH-04 | low | reject | The exact six-field mutable record selects current state, while the performed pre-close/post-close sequence is recorded in verification; adding actor/history/test-run fields would change the approved contract for negligible runtime benefit. |
| BH-05 | low | reject | Post-close execution is workflow verification rather than sealed evidence by design; sealing it would create a circular or successor evidence cycle outside the approved lifecycle record. |
| BH-06 | medium | patch | Default validation validates one selector, then `validate_lifecycle_state` reloads another; a concurrent selector change can mix evidence and lifecycle identities. Reuse the validated selector and expected identities. |
| BH-07 | false | reject | The approved v4 subject binds the executable validator/tests and approved parent contract; the build wrapper remains mutable workflow state and was never an evidence gate input. |
| BH-08 | false | reject | The v4 limitation list is scoped to this source-only successor, not the entire repository backlog; required-CI enforcement remains explicitly external and tracked by DW-521/DW-533. |
| BH-09 | false | reject | Receipts truthfully content-bind named independent reviews without claiming cryptographic identity; authenticated reviewer ownership remains the separate DW-306/DW-532 capability. |
| BH-10 | medium | patch | The parent story's latest completion section still says sprint `review` and reports obsolete 467/2070 results, contradicting the new closed lifecycle. Add a non-frozen v4 supersession note. |
| BH-11 | false | reject | The cited ledger passages are dated append-only historical findings; canonical DW-497 and DW-505 now carry `done` plus v4 resolutions, so rewriting history would corrupt provenance. |
| BH-12 | medium | patch | Existing v4 source-drift tests stop at hash mismatch and do not reach the workflow/fixture PostgreSQL semantic guards; add coherently rebound v4 mutations. |
| BH-13 | medium | patch | The v4 chronology comparisons have only happy-path coverage; execution/freeze/receipt/handoff ordering and future-time mutations can regress undetected. |
| BH-14 | medium | patch | The lifecycle suite covers the approved matrix broadly but misses coupled selector/record drift and basic structural identity cases that exercise the new binding guards. |
| ECH-01 | medium | patch | Same root cause as BH-06: packet and lifecycle can observe different selector reads during one default run. |
| ECH-02 | low | reject | The pathname-replacement race is real but pre-existing in the shared bounded reader, already tracked as DW-454, and an atomic open-beneath rewrite is disproportionate to this single-writer path. |
| ECH-03 | medium | patch | Equality alone lets null or non-SHA manifest/subject values pass when both lifecycle and selector are changed; require exact SHA-256-shaped identities and cover them. |
| VG-01 | medium | patch | Pre-verified gap: no active-v4 mutation falsifies execution/freeze, receipt/freeze, handoff/receipt, or future-time comparisons. |
| VG-02 | medium | patch | Pre-verified gap: legacy handoff mutations never exercise the active-v4 consumer-instruction and external-authority checks. |

## Design Notes

The lifecycle record has exact fields `schema`, `story`, `state`, `successorDirectory`, `successorManifestSha256`, and `reviewSubjectSha256`; `state` is only `ready-to-close` or `closed`. Default validation derives one exact status pair from this record only after v4 succeeds. Isolated `final` and `closed` modes check lifecycle/documents only and are never evidence approval.

## Verification

**Commands:**
- `python3 tools/validate-oq8-platform-evidence.py` plus `--lifecycle-mode final`, `--historical-v1-only`, `--historical-v2-only`, and `--historical-v3-only` before transition -- expected: all pass.
- `python3 tools/validate-oq8-platform-evidence.py` plus `--lifecycle-mode closed` and all historical modes after transition -- expected: all pass.
- `dotnet restore` and serialized Release `dotnet build` for `Hexalith.EventStore.Contracts.Tests.csproj`, then direct assembly focused/full runs -- expected: zero errors, failures, or skips; freshly measured v4 counts.
- `sha256sum --check` for sealed v3 and v4 manifests; `git diff --check` -- expected: unchanged v3 and clean generated diffs.

**Results:**
- Pre-close default, final, and historical v1/v2/v3 validators passed; focused Contracts passed 464/464 and full Contracts passed 2072/2072 with zero failures or skips.
- Post-close default, closed, and historical v1/v2/v3 validators passed; final mode correctly rejected the closed record; full Contracts again passed 2072/2072 with zero failures or skips.
- Corrective verification passed default and closed validation, all historical modes, both sealed manifests, validator-record identity, actionlint, the LiveSidecar Release build and focused production path, the focused Contracts class at 476/476, and the full Contracts assembly at 2084/2084, with zero failures, errors, skips, or not-run cases.
