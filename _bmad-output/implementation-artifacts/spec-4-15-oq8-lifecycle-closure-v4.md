---
title: 'Story 4.15 OQ8 Lifecycle Closure v4'
type: 'bugfix'
created: '2026-09-20'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
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
- [ ] Validator and Contracts tests -- implement phase/record/v4 lineage and prove malformed, ambiguous, mismatched, oversized, symlinked, and evidence-drift cases fail closed.
- [ ] Frozen parent contract -- apply exactly the approved `Always` replacement and no other frozen edit.
- [ ] V4 candidate and reviews -- bind completed v3 commit `d578e7626df1d8afeada440c7989eb04ba2bdfe6`, generate the candidate, run receipt-independent gates, and obtain fresh unchanged-subject architecture/security/test approvals before sealing and selector activation.
- [ ] Pre-close and transition -- run every required validator and Contracts gate at `ready-to-close`; then change lifecycle record to `closed` and sprint status to `done` together.
- [ ] Settlement -- rerun post-close gates, mark only DW-497/DW-505 done with resolutions, and reconcile direct Story 4.15 planning statements while Epic 4 and external/readiness authority remain unchanged.

**Acceptance Criteria:**
- Given a valid active v4 and ready-to-close record, when pre-close verification runs, then default, final, historical v1/v2/v3, focused OQ8, and full Contracts pass without changing sprint `review`.
- Given pre-close verification passed, when record and sprint transition to `closed`/`done`, then default, closed, historical modes, the checked-in full-validation probe, and full Contracts pass.
- Given lifecycle fields are changed without valid v4 evidence or disagree with the record, when validation runs, then closure fails before any completion claim.
- Given final repository state, when v3 hashes and authority fields are inspected, then v3 is byte-identical and every non-EventStore-platform authority remains false.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

The lifecycle record has exact fields `schema`, `story`, `state`, `successorDirectory`, `successorManifestSha256`, and `reviewSubjectSha256`; `state` is only `ready-to-close` or `closed`. Default validation derives one exact status pair from this record only after v4 succeeds. Isolated `final` and `closed` modes check lifecycle/documents only and are never evidence approval.

## Verification

**Commands:**
- `python3 tools/validate-oq8-platform-evidence.py` plus `--lifecycle-mode final`, `--historical-v1-only`, `--historical-v2-only`, and `--historical-v3-only` before transition -- expected: all pass.
- `python3 tools/validate-oq8-platform-evidence.py` plus `--lifecycle-mode closed` and all historical modes after transition -- expected: all pass.
- `dotnet restore` and serialized Release `dotnet build` for `Hexalith.EventStore.Contracts.Tests.csproj`, then direct assembly focused/full runs -- expected: zero errors, failures, or skips; freshly measured v4 counts.
- `sha256sum --check` for sealed v3 and v4 manifests; `git diff --check` -- expected: unchanged v3 and clean generated diffs.
