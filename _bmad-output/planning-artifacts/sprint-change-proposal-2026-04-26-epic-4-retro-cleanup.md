# Sprint Change Proposal - Epic 4 Retrospective Cleanup and Runtime Verification

**Date:** 2026-04-26
**Author:** Bob (Scrum Master)
**Project Lead:** Jerome
**Trigger:** Epic 4 retrospective (`epic-4-retro-2026-04-26.md`) action items R4-A1..R4-A8
**Mode:** Batch
**Scope Classification:** **Moderate** - backlog reorganization, one bookkeeping correction, story-record reconstruction, and runtime verification follow-up. No PRD redefinition or architecture replan.

---

## 1. Issue Summary

Epic 4 delivered the event distribution backbone: CloudEvents publication, tenant/domain topics, resilient drain recovery, and per-aggregate backpressure. The retrospective found that the implementation direction is sound, but the evidence trail and runtime verification are not strong enough for Epic 5 security work to depend on without cleanup.

| ID | Finding | Evidence | Status |
|---|---|---|---|
| R4-A1 | Story 4.2 file status says `review` while sprint-status says `done` | `_bmad-output/implementation-artifacts/4-2-resilient-publication-and-backlog-draining.md:3`; `sprint-status.yaml` marks `4-2-*` done | Bookkeeping correction applied by this proposal |
| R4-A2 | Story 4.3 is marked done but lacks a full execution record | `4-3-per-aggregate-backpressure.md` contains story + ACs only; implementation/tests exist in `AggregateActor`, `BackpressureTests`, `BackpressureOptionsTests`, and `BackpressureExceptionHandler` | Needs documentation reconstruction story |
| R4-A3 | Replay ULID validation from R3-A1 is still critical before recovery evidence is trusted | Existing `post-epic-3-r3a1-replay-ulid-validation` backlog story | Existing story remains authoritative |
| R4-A4 | Live command-surface verification from R3-A7 is still critical | Existing `post-epic-3-r3a7-live-command-surface-verification` backlog story | Existing story remains authoritative |
| R4-A5 | Full command-to-subscriber pub/sub delivery remains deferred to Tier 3 | Story 4.1 deferred E2E pub/sub integration; Story 4.2 recorded DAPR infrastructure failures | Needs verification story |
| R4-A6 | Drain success can publish fewer events than `UnpublishedEventsRecord.EventCount` if a persisted range has missing events | Story 4.2 completion notes document missing-event range behavior | Needs implementation/test story |
| R4-A7 | R3-A2/R3-A3 must remain routed to existing cleanup stories | Sprint status already maps R3-A2 to post-Epic-1 and R3-A3 to post-Epic-2 | No new story; preserve routing |
| R4-A8 | Source comments still reference old Story 4.4 / 4.5 numbering | Story 4.2 notes XML comments in `UnpublishedEventsRecord.cs` and `EventDrainOptions.cs` | Needs low-risk cleanup story |

---

## 2. Checklist Execution Summary

| Checklist Section | Status | Notes |
|---|---|---|
| 1. Trigger and context | Done | Trigger is Epic 4 retro findings with concrete artifact and code evidence |
| 2. Epic impact | Done | Epic 4 remains complete; Epic 5 security evidence depends on Epic 4 runtime confidence |
| 3. Artifact conflicts | Done | PRD and architecture remain valid; sprint-status, story records, and verification backlog need updates |
| 4. Path forward | Done | Direct adjustment plus targeted follow-up stories is the best path |
| 5. Proposal components | Done | Detailed changes and handoff are below |
| 6. Final review/handoff | Action-needed | Proposal and backlog entries are prepared; implementation still requires dev/QA execution |

---

## 3. Impact Analysis

### Epic Impact

- **Epic 4 (`done`)** remains valid. The delivered pub/sub and backpressure scope is not being rolled back or redefined.
- **Epic 5 (`done` in current sprint-status, but security evidence still depends on runtime proof)** is affected because it relies on tenant/domain topics, DAPR access-control behavior, command-surface health, and runtime verification discipline.
- **Future observability/admin work** is affected by drain integrity and missing runtime evidence because operations tooling will expose drain, pub/sub, and backpressure health.

### Story Impact

New standalone post-Epic-4 cleanup stories:

| New Story Key | Title | Source Action | Priority |
|---|---|---|---|
| `post-epic-4-r4a2-story-4-3-execution-record` | Reconstruct Story 4.3 execution record and final verification evidence | R4-A2 | High |
| `post-epic-4-r4a5-tier3-pubsub-delivery` | Add Tier 3 command-to-subscriber pub/sub delivery and drain recovery coverage | R4-A5 | Medium |
| `post-epic-4-r4a6-drain-integrity-guard` | Add drain integrity guard for incomplete event ranges | R4-A6 | Medium |
| `post-epic-4-r4a8-story-numbering-comments` | Normalize old Epic 4 story numbering comments | R4-A8 | Low |

Existing cleanup stories remain authoritative and must not be duplicated:

- `post-epic-3-r3a1-replay-ulid-validation` covers R4-A3.
- `post-epic-3-r3a7-live-command-surface-verification` covers R4-A4.
- `post-epic-1-r1a1-aggregatetype-pipeline` covers R4-A7's AggregateType carry-over concern.
- `post-epic-2-r2a2-commandstatus-isterminal-extension` covers R4-A7's terminal-status carry-over concern.

### Artifact Conflicts

- **PRD:** No change. FR17-FR20, FR29, FR30-FR34, FR67, NFR13, NFR22, NFR24, NFR25, and NFR28 already describe the required behavior.
- **Epics:** No redefinition. Epic 4 and Epic 5 are directionally correct.
- **Architecture:** No replan. D6 topic naming, ADR-P2, D11 Keycloak runtime verification, and Rule 16 already express the needed direction.
- **UX:** No immediate change. Admin UI/operations UX may benefit later from drain/pub-sub health visibility, but this proposal does not change UI scope.
- **Sprint status:** Add post-Epic-4 cleanup keys and update `last_updated`.
- **Story 4.2 file:** Correct `Status: review` -> `Status: done`.

---

## 4. Recommended Approach

**Direct adjustment with a runtime-verification story cluster.**

Rollback is not useful: Epic 4's implementation is coherent and aligned with the PRD/architecture. MVP review is not needed because the requirements remain achievable. The problem is traceability and runtime proof, so the right course is to add targeted follow-up stories and fix the obvious bookkeeping drift.

**Sequence:**

1. Apply this proposal's planning/status edits.
2. Keep R4-A3 and R4-A4 routed to the existing post-Epic-3 stories; execute them before using Epic 5 runtime evidence as security proof.
3. Complete R4-A2 so Story 4.3 has a reliable execution record.
4. Implement R4-A6 before treating drain recovery as a zero-loss proof.
5. Add R4-A5 Tier 3 pub/sub delivery coverage before claiming end-to-end event distribution confidence.
6. Apply R4-A8 as low-risk traceability cleanup when touching nearby files.

**Risk level:** Medium until R4-A3/R4-A4/R4-A5 are resolved, because Epic 5 security claims depend on runtime behavior across the command API, pub/sub topics, DAPR sidecars, and subscriber delivery.

---

## 5. Detailed Change Proposals

### Proposal 1 - R4-A1 Story 4.2 Status Reconciliation

**Problem.** The story file says `Status: review`, while sprint-status marks the story done and the story notes say all 13 ACs were verified.

```text
File: _bmad-output/implementation-artifacts/4-2-resilient-publication-and-backlog-draining.md

OLD:
Status: review

NEW:
Status: done

Rationale:
Sprint status and story completion notes already record the story as complete.
```

### Proposal 2 - `post-epic-4-r4a2-story-4-3-execution-record`

**Problem.** Story 4.3 has implementation and tests in the codebase but lacks the execution record expected by future agents.

```text
Story: 4.3 Per-Aggregate Backpressure
Section: Dev Agent Record / Tasks / File List / Change Log

OLD:
No execution record beyond story and acceptance criteria.

NEW:
Add:
- Task checklist mapping AC #1-#12 to implementation/tests
- Dev Agent Record with model, completion notes, verification summary
- File List including AggregateActor.cs, BackpressureExceededException.cs, CommandProcessingResult.cs,
  SubmitCommandHandler.cs, BackpressureExceptionHandler.cs, BackpressureOptions.cs, and tests
- Change Log entry for Story 4.3 completion
- Explicit final verification status and any unrun test caveats

Rationale:
Story status is already `done`; the missing artifact creates avoidable uncertainty.
```

### Proposal 3 - Existing R4-A3 Routing

**Problem.** Replay still needs the R3-A1 ULID validation fix before replay/recovery behavior can be used as evidence.

```text
Existing Story:
post-epic-3-r3a1-replay-ulid-validation

Instruction:
Keep this story as the authoritative fix for R4-A3. Do not create a duplicate post-Epic-4 story.

Rationale:
The defect was already routed by the Epic 3 corrective proposal.
```

### Proposal 4 - Existing R4-A4 Routing

**Problem.** Live command-surface verification remains required before Epic 5 depends on runtime security evidence.

```text
Existing Story:
post-epic-3-r3a7-live-command-surface-verification

Instruction:
Execute this before using Epic 5 E2E security results as proof of the command pipeline.

Rationale:
Security E2E tests are only meaningful if the command surface is known healthy.
```

### Proposal 5 - `post-epic-4-r4a5-tier3-pubsub-delivery`

**Problem.** Mocked DAPR publication tests prove publisher behavior, but not broker delivery, subscription, sidecar policy, reminder execution, or drain re-publish in a running topology.

```text
Story: Add Tier 3 pub/sub delivery coverage

Acceptance Criteria:
1. Running Aspire topology can process a command, persist events, publish CloudEvents, and deliver them to a subscriber on `{tenant}.{domain}.events`.
2. The subscriber observes CloudEvents metadata: type, source, id, tenant/domain topic, and correlation ID.
3. A simulated pub/sub outage records `UnpublishedEventsRecord` and leaves command processing accepted.
4. Recovery/drain republishes the same persisted event range and removes the drain record.
5. Test artifacts clearly separate environment limitations from product failures.

Rationale:
FR18, FR20, NFR22, NFR24, and NFR28 require runtime evidence beyond mocked `DaprClient` calls.
```

### Proposal 6 - `post-epic-4-r4a6-drain-integrity-guard`

**Problem.** Story 4.2 notes missing events in a drain range are skipped, which can produce a shorter event list while still allowing a successful publish result.

```text
Story: Add drain integrity guard

Acceptance Criteria:
1. `DrainUnpublishedEventsAsync` compares loaded event count with `UnpublishedEventsRecord.EventCount`.
2. If the loaded count differs, drain does not remove the drain record.
3. The failure path increments retry count or emits an explicit operational signal with correlation ID and missing sequence range.
4. Tests cover missing first, middle, and last event in the range.
5. Existing successful drain behavior remains unchanged when all events are present.

Rationale:
At-least-once delivery should not turn state inconsistency into apparent drain success.
```

### Proposal 7 - R4-A7 Existing Cleanup Routing

**Problem.** AggregateType and terminal-status helper debt remain visible from prior retrospectives.

```text
Existing Stories:
post-epic-1-r1a1-aggregatetype-pipeline
post-epic-2-r2a2-commandstatus-isterminal-extension

Instruction:
Do not duplicate these under post-Epic-4. Preserve the routing comments in sprint-status and execute the existing stories.

Rationale:
Duplicate cleanup stories make ownership less clear.
```

### Proposal 8 - `post-epic-4-r4a8-story-numbering-comments`

**Problem.** Some source comments reference old Story 4.4 / 4.5 numbering, while the current Epic 4 only tracks Stories 4.1-4.3.

```text
Story: Normalize old Epic 4 story numbering comments

Acceptance Criteria:
1. Comments in drain/publication/backpressure source files reference current story numbers or neutral feature names.
2. No behavior changes.
3. Build remains clean.

Rationale:
Traceability matters for agents and reviewers; stale story numbers create avoidable confusion.
```

---

## 6. Sprint Status Changes

Add this section after the existing post-Epic-3 cleanup block:

```yaml
  # Post-Epic-4 Retro Cleanup (sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md)
  # Standalone follow-up stories from Epic 4 retrospective.
  # R4-A1 corrected directly in Story 4.2.
  # R4-A3 is covered by post-epic-3-r3a1-replay-ulid-validation.
  # R4-A4 is covered by post-epic-3-r3a7-live-command-surface-verification.
  # R4-A7 preserves existing routing to post-Epic-1 R1-A1 and post-Epic-2 R2-A2.
  post-epic-4-r4a2-story-4-3-execution-record: backlog
  post-epic-4-r4a5-tier3-pubsub-delivery: backlog
  post-epic-4-r4a6-drain-integrity-guard: backlog
  post-epic-4-r4a8-story-numbering-comments: backlog
```

---

## 7. Implementation Handoff

**Scope:** Moderate.

**Bob / Scrum Master**

- Preserve sprint-status routing and prevent duplicate stories for R4-A3/R4-A4/R4-A7.
- Ensure Story 4.3 execution record is reconstructed before future agents rely on it.

**Dev**

- Implement R4-A6 drain integrity guard.
- Apply R4-A8 comment cleanup.
- Execute R4-A3 through the existing post-Epic-3 story.

**QA**

- Design and run R4-A5 Tier 3 pub/sub delivery/drain recovery coverage.
- Keep environment prerequisites and failures explicit.

**Dev / QA**

- Execute R4-A4 live command-surface verification before Epic 5 security evidence is treated as complete.

**Product / Architecture**

- No PRD or architecture replan needed.
- Reassess only if Tier 3/live verification reveals a structural limitation rather than an implementation or environment issue.

---

## 8. Success Criteria

The course correction is successful when:

1. Story 4.2 status is reconciled.
2. Story 4.3 has a complete execution record.
3. Sprint status carries the post-Epic-4 cleanup cluster.
4. Existing R3/R2/R1 cleanup routing remains intact.
5. Tier 3 pub/sub delivery and drain recovery have explicit runtime evidence.
6. Drain recovery cannot report success when the loaded persisted range is incomplete.
7. Epic 5 security verification is not used as final evidence until command-surface and pub/sub runtime confidence are established.

Bob (Scrum Master): "This is a direct course correction. Epic 4 stands. The next work is evidence, integrity, and runtime proof."
