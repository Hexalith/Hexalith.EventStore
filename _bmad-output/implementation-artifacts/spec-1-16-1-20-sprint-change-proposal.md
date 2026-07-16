---
title: 'Implement Story 1.16/1.20 Sprint Change Proposal'
type: 'chore'
created: '2026-07-16'
status: 'in-progress'
baseline_commit: '4423e03bef8f2e6f9139a143a3fc42ea8c835dfd'
review_loop_iteration: 1
context:
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-story-1-16-review-and-story-1-20-proof-closure.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The approved Story 1.16/1.20 correction is only partially reflected in the implementation artifacts. Without the remaining AD-11, sequencing, identity, and approval gates, readers could mistake a failed candidate or documentation commit for an approved runtime and authorize consumer migration prematurely.

**Approach:** Apply the approved planning and evidence edits incrementally against the current richer artifacts. Preserve every concurrent fact and fail-closed value, and leave conditional Story 1.16 review closure unapplied until a genuine named review exists for the eventual exact runtime.

## Boundaries & Constraints

**Always:** Preserve the failed candidate `85877902f8d60a466ab90cd8b68b53838863db1c` as non-authorizing evidence; keep Story 1.20 `blocked`, sprint/Epic 1 `in-progress`, `tested_runtime_sha: null`, `documentation_commit_sha: null`, `final_decision: still blocked`, `authorize_consumer_migration: false`, and all nine capability rows `still blocked`; retain Story 1.16 `followup_review_recommended: true`; append rather than overwrite deferred work; reconcile against current repository facts and richer evidence.

**Ask First:** Any proposal to clear Story 1.16's review flag, select a tested runtime, approve package or container identity, authorize migration, change a story/epic status, publish an artifact, edit a submodule, or alter runtime code, tests, pins, topology, or persisted data requires new human authority and the missing durable evidence.

**Never:** Fabricate reviewer, approval, SHA, package hash, container digest, platform, provenance, or verification data; restore proposal-era submodule pointers; replace the current proof packet with the proposal's shorter example; modify Parties or remove rollback infrastructure; weaken or bypass a fail-closed guard.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Approved incremental update | Rich failed-candidate evidence already exists | Add only missing AD-11, execution-order, identity, and blocker context | Preserve current evidence when proposal snippets are stale |
| Missing exact-runtime review | Story 1.16 has no named, dated, durable approval | Keep the flag `true` and do not add a disposition | Keep Story 1.20 blocked |
| Partial proof | Build subsets passed but live-sidecar failed | Retain candidate only as failed evidence | Never populate approval/runtime fields |
| Concurrent artifact drift | Current facts differ from proposal-era audit | Use current facts without erasing historical evidence | Stop if a change would overwrite unknown work |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md` -- current richer proof ledger and all non-authorizing runtime/package/container fields.
- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md` -- Story 1.20 acceptance boundary, blocker state, and execution sequence.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- append-only lifecycle and build/release corrective-work ledger.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- Epic 1 and Story 1.20 status plus blocker commentary.
- `_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md` -- historical filename for active Story 1.16; its follow-up-review flag remains fail closed.
- `_bmad-output/implementation-artifacts/epic-1-context.md` -- workflow-refreshed concurrent context change to preserve.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md` -- incrementally add the dated exact-SHA audit and AD-11 hard blocker, including runtime/documentation identity separation, without duplicating or replacing the existing failed-run evidence; refresh `updated` to the actual edit time.
- [x] `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md` -- expand acceptance items 1 and 5–10, reconcile the stale Auto Run summary with Story 1.19's completed review, and add the approved ten-step closure order while retaining `status: blocked`; make release-owner authority an explicit prerequisite to external container publication and distinguish it from final proof approval.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- append the separately owned AD-11 `open-blocking` corrective item with evidence, consequence, closure conditions, and reopen trigger; preserve existing entries verbatim.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- replace only Story 1.20's explanatory comments with the approved blocker list; retain Story 1.20 and Epic 1 `in-progress`.
- [x] `_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md` -- verify and retain `followup_review_recommended: true`; make no review-disposition edit without all approved fields and evidence.
- [x] `_bmad-output/implementation-artifacts/epic-1-context.md` -- restore the durable Sample/Tenants behavior-preservation and release-hygiene constraints plus the projection-handler adapter/breaking-migration compatibility rule that the refreshed context over-pruned.

**Acceptance Criteria:**
- Given the failed candidate and incomplete identities, when the artifacts are updated, then no field, capability row, story, epic, or comment claims approval, availability, completion, or migration authority.
- Given existing concurrent evidence and deferred items, when approved text is applied, then those contents remain intact and the AD-11 work is additive and separately owned.
- Given Story 1.16 lacks an exact-runtime review, when implementation completes, then its flag remains `true` and the Story 1.20 blockers explicitly name that condition.
- Given the proposal is evidence-only, when the diff is reviewed, then no runtime source, test, pin, package manifest, container, topology, submodule, Parties, or persisted-data artifact changed.
- Given external container publication requires release-owner authority, when the closure order is read, then authority precedes publication and final proof approval remains a later distinct gate.

## Spec Change Log

- Review loop 1: the first review found stale Story 1.19 wording, ambiguous pre-publication authority, an unchanged proof-packet timestamp, an untracked-file whitespace gap, and over-pruned Epic 1 constraints. Tasks and verification now require those corrections, avoiding contradictory blocker narratives, unauthorized publication ordering, weak provenance, incomplete whitespace coverage, and future context-driven compatibility/release regressions. KEEP: preserve the additive AD-11 audit and ledger work, exact-SHA failure evidence, current-fact reconciliation, all existing deferred entries, every fail-closed status/identity/capability guard, Story 1.16's retained flag, and all concurrent lifecycle/submodule changes.

## Design Notes

The approved proposal contains replacement-style examples based on an earlier checkout. Treat them as semantic requirements: merge their missing gates into the current artifacts, whose later exact-SHA failure evidence is authoritative. Current repository identity supersedes proposal-era audit facts, but the failed detached-run evidence remains historical proof. Release-owner authority to perform external publication must exist before the publish step; the later release-owner disposition approves the completed identity/provenance packet.

## Verification

**Commands:**
- `git diff --check` and `git diff --cached --check` -- expected: no whitespace errors or conflict markers in unstaged or staged tracked changes.
- `test -z "$(git diff --no-index --check -- /dev/null _bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md 2>&1)"` -- expected: untracked spec has no whitespace errors.
- `bash scripts/check-deferred-work.sh _bmad-output/implementation-artifacts/deferred-work.md` -- expected: exit 0; pre-existing legacy advisories may remain.
- `rg -c '^- classification: `still blocked`$' _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md` -- expected: `9`.
- `rg -n 'tested_runtime_sha: null|documentation_commit_sha: null|final_decision: still blocked|authorize_consumer_migration: false' _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md` -- expected: all four guards present.
- `rg -n 'followup_review_recommended: true' _bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md` -- expected: fail-closed flag present.
- `git status --short --branch` -- expected: authorized planning/evidence edits remain attributable to this spec; concurrent runtime, test, branch, or submodule work is reported separately and preserved.
