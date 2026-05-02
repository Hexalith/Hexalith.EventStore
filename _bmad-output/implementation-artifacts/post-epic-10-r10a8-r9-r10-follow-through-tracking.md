# Post-Epic-10 R10-A8: R9/R10 Follow-Through Tracking

Status: ready-for-dev

<!-- Source: epic-10-retro-2026-05-01.md R10-A8 -->
<!-- Source: epic-9-retro-2026-04-30.md R9-A1..R9-A8 -->
<!-- Source: sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md Proposal 8 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **Scrum Master / project lead responsible for retro closure discipline**,
I want R9 and R10 retrospective action items reconciled into visible tracking or accepted non-action decisions,
so that query and SignalR confidence work is not lost in retrospective prose or duplicated across overlapping post-epic stories.

## Story Context

Epic 9 and Epic 10 both closed as successful implementation epics, but their retrospectives exposed the same process risk: important confidence-hardening work can remain buried in retrospective tables while later stories move on. R10-A8 exists to make that follow-through visible. It is not another query, SignalR, Redis, or UI proof story. It is a tracking reconciliation story that inspects the current artifact state, maps every R9 and R10 action item to a concrete disposition, and updates the repo's planning/status trail so future agents can find the owning story or accepted decision.

At story creation, the Post-Epic-10 cluster is partly closed and partly still queued: R10-A1 is routed to `post-epic-11-r11a3-apphost-projection-proof` and that story is `done`; R10-A2 and R10-A3 are `done`; R10-A5 is `review`; R10-A6 and R10-A7 are `ready-for-dev`; R10-A8 is the remaining backlog row. Epic 9 action items still need fresh inspection rather than assumption because some may have been partially satisfied by later R11 proof work, planning-artifact edits, documentation updates, or release/governance work.

Current HEAD at story creation: `3bb39b8`.

## Acceptance Criteria

1. **R9/R10 action inventory is complete.** Create a concise follow-through table covering every Epic 9 action item `R9-A1` through `R9-A8` and every Epic 10 action item `R10-A1` through `R10-A8`. The table must include source file, action text, owner, priority, current disposition, evidence link, and next action.

2. **Disposition categories are explicit.** Each item must be classified as exactly one of: `done`, `in-progress`, `ready-for-dev`, `backlog`, `superseded`, `accepted-non-action`, or `needs-new-tracking`. Do not use vague values such as "probably done" or "covered elsewhere".

3. **Current artifact evidence is inspected before closure.** For any item marked `done`, cite the exact story artifact, test artifact, docs change, sprint-status row, retrospective annotation, or commit evidence that proves closure. A story being `done` is sufficient only when its artifact clearly names the retro action or acceptance criteria that close the action.

4. **R10 routing is reconciled without duplication.** Verify and record the current disposition of R10-A1 through R10-A7:
   - R10-A1 is routed to `post-epic-11-r11a3-apphost-projection-proof`; record whether its caveated SignalR/AppHost evidence is sufficient or whether residual follow-up remains.
   - R10-A2 is owned by `post-epic-10-r10a2-redis-backplane-runtime-proof`.
   - R10-A3 is owned by `post-epic-10-r10a3-hub-group-authorization-decision`.
   - R10-A4 was applied directly through planning artifact normalization; verify PRD/epics wording before marking it done.
   - R10-A5, R10-A6, and R10-A7 retain their own story rows; do not absorb their implementation scope.

5. **R9 actions are evaluated against later evidence, not copied from the retro.** Inspect R9-A1 through R9-A8 against current artifacts. In particular, check whether later R11 proof stories satisfy any HTTP ETag, query topology, or query-latency evidence obligations; whether PRD/epics/docs now align on colon separators, per-request tenant, static `ProjectionType`, and `api/v1/queries`; whether Story 8.1 and Story 7.3 status headers still drift from `sprint-status.yaml`; and whether release governance has real recorded evidence.

6. **No hidden feature work is performed.** If an item still needs implementation, verification, documentation, or release-governance work, create or identify the owning story/status row or record an accepted non-action decision. Do not implement runtime proofs, change product code, rewrite query/SignalR behavior, or run broad infrastructure work inside this tracking story.

7. **Accepted non-action decisions are justified.** Any `accepted-non-action` or `superseded` disposition must include a dated owner/role, rationale, evidence reviewed, residual risk, and revisit trigger. Do not use accepted non-action to hide missing high-priority proof.

8. **Sprint status remains discoverable.** Update `_bmad-output/implementation-artifacts/sprint-status.yaml` comments or rows only as needed to make R9/R10 routing visible. Do not change unrelated story statuses. This story may move only itself through the normal lifecycle during development/review.

9. **Retrospective trail is updated without rewriting history.** Add closure annotations or a follow-through section to the relevant retrospective or tracking artifact while preserving the original retro tables. Do not delete or rewrite the original R9/R10 findings; append dated dispositions so the audit trail remains readable.

10. **A reusable follow-through pattern is captured.** Record the minimum fields future retrospectives should provide before marking follow-through complete. Keep the pattern inside BMAD output artifacts or documentation owned by this repository; do not edit `.claude/skills/`, `_bmad/bmm/`, or tool-submodule skill definitions as part of this story.

11. **Validation is appropriate for a tracking story.** Run markdown/link validation if available. If only BMAD Markdown/YAML artifacts change, no product test is required. If any script or generated-status helper is added, run its focused validation and record the command/result.

12. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and `last_updated` names R10-A8 and the reconciliation result. At code-review signoff, both become `done`.

## Scope Boundaries

- Do not create duplicate stories for work already owned by R10-A2, R10-A3, R10-A5, R10-A6, R10-A7, R11-A3, or R11-A4.
- Do not mark a high-priority proof item done from inference alone. It needs artifact evidence or an explicit accepted decision.
- Do not run full AppHost, Redis, SignalR, query, browser, release, or governance verification unless the reconciliation itself cannot classify an item without a small targeted check.
- Do not initialize nested submodules.
- Do not edit generated preflight JSON audit files.

## Initial Follow-Through Matrix To Verify

| Item | Initial state at story creation | Required verification |
|---|---|---|
| R9-A1 | Possibly covered in part by R11-A4 valid projection round-trip | Verify full HTTP self-routing ETag proof, including valid `If-None-Match` behavior, is explicitly evidenced |
| R9-A2 | Possibly covered in part by R11-A3/R11-A4 | Verify Aspire query-cache topology proof covers cold query, ETag header, warm 304, projection invalidation, and cache re-warm |
| R9-A3 | Possibly covered by R10-A4 direct planning normalization | Verify PRD/epics/docs align on colon separators, per-request tenant, static `ProjectionType`, and `api/v1/queries` |
| R9-A4 | Unknown | Verify Story 8.1 and Story 7.3 file headers match sprint-status or record the owning cleanup |
| R9-A5 | Unknown | Verify semantic-release remote tag state and release governance are recorded |
| R9-A6 | Unknown | Verify `IQueryResponse` compile-time non-null and runtime non-empty enforcement is documented |
| R9-A7 | Still process-risk unless this story closes it | Capture a reusable retro follow-through pattern without editing BMAD skill internals |
| R9-A8 | Possibly adjacent to R10-A6, but query-specific | Verify query latency NFR evidence has its own pattern or accepted routing |
| R10-A1 | Routed to R11-A3, status `done` | Decide whether R11-A3 caveated evidence is enough or whether residual SignalR proof remains |
| R10-A2 | `post-epic-10-r10a2-redis-backplane-runtime-proof`, status `done` | Cite runtime proof artifact and any deferred Redis isolation handoff to R10-A7 |
| R10-A3 | `post-epic-10-r10a3-hub-group-authorization-decision`, status `done` | Cite tenant-aware hub authorization decision/evidence and deferred findings |
| R10-A4 | Direct planning artifact normalization | Verify no stale hyphen-scoped group/actor wording remains in PRD/epics for this scope |
| R10-A5 | `post-epic-10-r10a5-client-reconnect-guidance`, status `review` | Leave ownership with R10-A5 unless review finds missing tracking |
| R10-A6 | `post-epic-10-r10a6-signalr-operational-evidence-pattern`, status `ready-for-dev` | Leave ownership with R10-A6 |
| R10-A7 | `post-epic-10-r10a7-redis-channel-isolation-policy`, status `ready-for-dev` | Leave ownership with R10-A7 |
| R10-A8 | This story | Close the visibility/process gap with dated dispositions |

## Tasks / Subtasks

- [ ] Task 0: Baseline the reconciliation target (AC: #1, #2, #3)
  - [ ] 0.1 Record current HEAD SHA and confirm this story is still `ready-for-dev`.
  - [ ] 0.2 Load Epic 9 and Epic 10 retrospectives and extract R9-A1..R9-A8 and R10-A1..R10-A8 verbatim.
  - [ ] 0.3 Load `sprint-status.yaml` and all candidate owning story artifacts named in this story.
  - [ ] 0.4 Build the follow-through table with one row per action item and no omitted IDs.

- [ ] Task 1: Reconcile R10 action items (AC: #3, #4, #6, #7)
  - [ ] 1.1 Verify R10-A1 against R11-A3 evidence and caveats.
  - [ ] 1.2 Verify R10-A2, R10-A3, and R10-A5 against their story artifacts and statuses.
  - [ ] 1.3 Verify R10-A4 by searching PRD/epics for stale group/actor wording in the R9/R10 scope.
  - [ ] 1.4 Preserve R10-A6 and R10-A7 as separate ready stories and record that ownership explicitly.

- [ ] Task 2: Reconcile R9 action items (AC: #3, #5, #6, #7)
  - [ ] 2.1 Inspect R11-A3/R11-A4 and query/ETag docs for R9-A1/R9-A2 closure evidence.
  - [ ] 2.2 Inspect planning docs for R9-A3 route, tenant, projection, and separator alignment.
  - [ ] 2.3 Inspect Story 8.1 and Story 7.3 headers for R9-A4 drift.
  - [ ] 2.4 Inspect release notes, tags, workflow docs, and governance artifacts for R9-A5.
  - [ ] 2.5 Inspect query API/docs/story records for R9-A6 and R9-A8.
  - [ ] 2.6 Decide whether R9-A7 is satisfied by this story's reusable follow-through pattern or needs a future process story.

- [ ] Task 3: Persist visible tracking (AC: #8, #9, #10)
  - [ ] 3.1 Add or update a BMAD-owned follow-through tracking section/artifact with dated R9/R10 dispositions.
  - [ ] 3.2 Add sprint-status comments or rows only where necessary to make unresolved items discoverable.
  - [ ] 3.3 Append retrospective closure annotations without deleting original retro findings.
  - [ ] 3.4 Capture the reusable minimum follow-through pattern for future retrospectives.

- [ ] Task 4: Validate and close bookkeeping (AC: #11, #12)
  - [ ] 4.1 Run markdown/link validation if available, or record why it was unavailable.
  - [ ] 4.2 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
  - [ ] 4.3 Move only this story and its sprint-status row to `review` at dev handoff.

## Dev Notes

### Reconciliation Rules

- Treat source retrospectives as historical records. Add dated dispositions; do not rewrite the original lessons.
- Prefer existing story ownership over new rows. R10-A8 should reduce duplicate tracking, not create another unowned backlog queue.
- A closure claim must point to a story artifact, evidence folder, docs change, sprint-status row, or commit. If evidence is missing, classify the item as still needing tracking.
- Use `accepted-non-action` sparingly. It is valid for obsolete or intentionally rejected work, but it must name the owner, rationale, residual risk, and revisit trigger.
- Keep the follow-through table short enough to maintain but specific enough that a later retro can audit each item without searching the whole repository.

### Existing Artifact Intelligence

- Epic 9 retro defines R9-A1 through R9-A8 and explicitly calls out full HTTP ETag proof, Aspire query-cache proof, planning drift, story-status drift, release governance, `IQueryResponse` docs, closure-gate process, and query-latency evidence.
- Epic 10 retro defines R10-A1 through R10-A8 and explicitly routes final SignalR confidence through live topology proof, Redis runtime proof, tenant-aware hub authorization, planning normalization, reconnect docs, operational evidence, Redis isolation policy, and follow-through tracking.
- `sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md` routes R10-A1 to R11-A3, creates R10-A2/R10-A3/R10-A5/R10-A6/R10-A7/R10-A8 rows, and says R10-A4 planning cleanup was applied directly.
- `post-epic-11-r11a3-apphost-projection-proof` is `done` and includes R10-A1 source routing, but it records caveated evidence for projection actor write and SignalR delivery. R10-A8 must classify those caveats honestly.
- `post-epic-11-r11a4-valid-projection-round-trip` is `done` and includes a valid command-to-query integration proof with ETag follow-up. It may help classify R9-A1/R9-A2, but only if its evidence matches the R9 action wording.
- `post-epic-10-r10a2-redis-backplane-runtime-proof` and `post-epic-10-r10a3-hub-group-authorization-decision` are `done`.
- `post-epic-10-r10a5-client-reconnect-guidance` is in `review`; R10-A6 and R10-A7 are `ready-for-dev`.

### Suggested File Touches

- `_bmad-output/implementation-artifacts/post-epic-10-r10a8-r9-r10-follow-through-tracking.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/epic-9-retro-2026-04-30.md`
- `_bmad-output/implementation-artifacts/epic-10-retro-2026-05-01.md`
- Optional BMAD-owned tracking artifact under `_bmad-output/implementation-artifacts/` or `_bmad-output/process-notes/`

### Testing Standards

- Markdown/YAML-only tracking changes do not require product tests.
- If a helper script is introduced to validate follow-through tables, run that script and record the command/result.
- If `sprint-status.yaml` changes, ensure it remains parseable and preserves comments/order.

## References

- `_bmad-output/implementation-artifacts/epic-9-retro-2026-04-30.md` - R9 action items and prior follow-through context.
- `_bmad-output/implementation-artifacts/epic-10-retro-2026-05-01.md` - R10 action items and R10-A8 source.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md` - Post-Epic-10 routing proposal.
- `_bmad-output/implementation-artifacts/post-epic-11-r11a3-apphost-projection-proof.md` - R10-A1 routed AppHost/SignalR proof.
- `_bmad-output/implementation-artifacts/post-epic-11-r11a4-valid-projection-round-trip.md` - valid command-to-query/ETag integration proof.
- `_bmad-output/implementation-artifacts/post-epic-10-r10a2-redis-backplane-runtime-proof.md` - R10-A2 runtime proof.
- `_bmad-output/implementation-artifacts/post-epic-10-r10a3-hub-group-authorization-decision.md` - R10-A3 hub authorization decision.
- `_bmad-output/implementation-artifacts/post-epic-10-r10a5-client-reconnect-guidance.md` - R10-A5 reconnect guidance.
- `_bmad-output/implementation-artifacts/post-epic-10-r10a6-signalr-operational-evidence-pattern.md` - R10-A6 evidence-pattern story.
- `_bmad-output/implementation-artifacts/post-epic-10-r10a7-redis-channel-isolation-policy.md` - R10-A7 Redis isolation story.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` - current status source of truth.

## Dev Agent Record

### Agent Model Used

To be filled by dev agent.

### Reconciliation Table

To be filled by dev agent with R9-A1..R9-A8 and R10-A1..R10-A8 dispositions.

### Debug Log References

To be filled by dev agent.

### Completion Notes List

To be filled by dev agent.

### File List

To be filled by dev agent.

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-02 | 0.1 | Created ready-for-dev R10-A8 R9/R10 follow-through tracking story. | Codex automation |

## Verification Status

Story creation only. R9/R10 reconciliation, tracking artifact updates, and validation are intentionally deferred to `bmad-dev-story`.
