# Post-Epic-4 R4-A8: Story Numbering Comments

Status: ready-for-dev

<!-- Source: epic-4-retro-2026-04-26.md R4-A8 -->
<!-- Source: sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md Proposal 8 -->
<!-- Source: sprint-change-proposal-2026-04-28.md Proposal 6 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform maintainer reading Epic 4 recovery and publication code**,
I want stale Story 4.4 / 4.5 source comments normalized to the current Epic 4 story map or neutral feature names,
So that future agents and reviewers do not chase obsolete story numbers while auditing event distribution behavior.

## Story Context

Epic 4 now contains three canonical stories:

- Story 4.1: CloudEvents Publication & Topic Routing.
- Story 4.2: Resilient Publication & Backlog Draining.
- Story 4.3: Per-Aggregate Backpressure.

Older implementation comments still refer to Story 4.4 and Story 4.5 in production code and AppHost DAPR component YAML. The Epic 4 retrospective records this as R4-A8: a low-risk traceability cleanup, not a behavioral change. This story closes only that cleanup. It must not absorb R4-A5 runtime pub/sub proof, R4-A6 drain integrity behavior, or any backlog/security gate.

Current HEAD at story creation: `da59238`.

## Acceptance Criteria

1. **Stale production comments are inventoried before editing.** Run a focused search over `src/` for obsolete Epic 4 references, at minimum:
   - `rg -n "Story 4\\.[45]|4\\.4|4\\.5" src -S`
   - Record the before/after counts in the Dev Agent Record.

2. **Production source/config comments no longer point to obsolete Story 4.4 or Story 4.5.** After implementation, `rg -n "Story 4\\.[45]" src -S` returns no production-source matches. Replace comments with current story references where clear, or with neutral feature language where a single current story number would be misleading.

3. **Behavior is unchanged.** The diff contains only comment/documentation wording changes plus required story bookkeeping. No method bodies, constants, options defaults, DAPR component values, topic names, routes, retry policies, access-control scopes, or test assertions are changed.

4. **Current Epic 4 mapping is explicit where useful.** Drain recovery comments may reference Story 4.2 or the neutral phrase "drain recovery"; publication/topic comments may reference Story 4.1; backpressure comments may reference Story 4.3. Dead-letter comments should prefer neutral feature language such as "dead-letter routing" unless a current story artifact explicitly owns the detail.

5. **Generated artifacts and historical story records are not rewritten.** Do not edit completed story files, retrospectives, planning proposals, or `_bmad-output/process-notes` to erase history. Historical mentions of Story 4.4 / 4.5 remain valid audit trail unless this story's Dev Agent Record needs to cite them.

6. **Tests remain a verification gate, not the primary change.** Do not modify test behavior for this cleanup. Test comments may be updated only when they are stale story-number labels and the edit is comment-only.

7. **Build verification is captured.** Run `dotnet build Hexalith.EventStore.slnx --configuration Release`. If a pre-existing build failure appears, record its exact shape and prove the failure is unrelated to comment-only edits.

8. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and both `last_updated` fields in `sprint-status.yaml` name R4-A8 and the result. At code-review signoff, both become `done`. Do not touch R4-A5 or R4-A6 status rows.

## Scope Boundaries

- Do not change runtime behavior in `AggregateActor`, event publishing, drain retry, dead-letter routing, DAPR component behavior, or AppHost topology.
- Do not broaden DAPR pub/sub scopes, subscriber definitions, resiliency values, or dead-letter topic patterns.
- Do not add or remove tests unless a build/lint rule requires a comment-only adjustment.
- Do not rewrite completed story artifacts or planning artifacts to hide historical numbering.
- Do not perform R4-A5 Tier 3 runtime proof or R4-A6 drain integrity work here.

## Implementation Inventory

| Area | File | Expected use |
|---|---|---|
| Actor orchestration comments | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | Normalize top-level, drain-recovery, dead-letter, and infrastructure-failure story labels without changing code |
| Drain record comments | `src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs` | Replace old drain recovery story label with current Story 4.2 or neutral wording |
| Drain options comments | `src/Hexalith.EventStore.Server/Configuration/EventDrainOptions.cs` | Replace old Story 4.4 wording with current drain-recovery wording |
| Publisher options comments | `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs` | Replace old Story 4.5 wording with neutral dead-letter routing wording |
| Telemetry comments | `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` | Normalize activity/tag summary comments for drain and dead-letter spans |
| Local pub/sub config comments | `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` | Normalize dead-letter and per-subscription comment wording only |
| Local resiliency comments | `src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml` | Normalize drain recovery comment wording only |
| Sample subscription comments | `src/Hexalith.EventStore.AppHost/DaprComponents/subscription-sample-counter.yaml` | Normalize dead-letter topic convention wording only |
| Optional test comment cleanup | `tests/Hexalith.EventStore.Server.Tests/**` | Comment-only updates if obsolete Story 4.4 / 4.5 labels are present and confusing |

## Tasks / Subtasks

- [ ] Task 0: Baseline and inventory (AC: #1)
  - [ ] 0.1 Record current HEAD SHA and confirm this story is still `ready-for-dev`.
  - [ ] 0.2 Run `rg -n "Story 4\\.[45]|4\\.4|4\\.5" src -S` and record the matching production files.
  - [ ] 0.3 Classify each match as drain recovery, dead-letter routing, pub/sub topic config, or unrelated numeric text.

- [ ] Task 1: Normalize production comments (AC: #2, #3, #4)
  - [ ] 1.1 Update drain-recovery comments to Story 4.2 or neutral "drain recovery" wording.
  - [ ] 1.2 Update dead-letter comments to neutral "dead-letter routing" wording unless a current story reference is plainly correct.
  - [ ] 1.3 Update pub/sub and subscription YAML comments without changing YAML values, scopes, metadata, or indentation semantics.
  - [ ] 1.4 Update telemetry summary comments only; do not rename activity constants or tag constants.

- [ ] Task 2: Optional stale test-comment cleanup (AC: #3, #6)
  - [ ] 2.1 Search tests for `Story 4.4` / `Story 4.5` labels.
  - [ ] 2.2 If a test comment is only a stale story label, update it to neutral task/feature wording.
  - [ ] 2.3 Do not change assertions, test names, fixtures, test data, or test counts.

- [ ] Task 3: Verification gates (AC: #2, #3, #7)
  - [ ] 3.1 Re-run `rg -n "Story 4\\.[45]" src -S` and confirm zero production-source matches.
  - [ ] 3.2 Review `git diff --check`.
  - [ ] 3.3 Review `git diff --name-only` and confirm only expected comment/story-bookkeeping files changed.
  - [ ] 3.4 Run `dotnet build Hexalith.EventStore.slnx --configuration Release`.

- [ ] Task 4: Story bookkeeping (AC: #8)
  - [ ] 4.1 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
  - [ ] 4.2 Move this story and only this story from `ready-for-dev` to `review` in `sprint-status.yaml`.
  - [ ] 4.3 Leave R4-A5 and R4-A6 status rows unchanged.

## Dev Notes

### Architecture Guardrails

- ADR-P2 keeps the state store as source of truth and pub/sub as distribution. This story must not touch that ordering.
- D6 topic naming remains `{tenant}.{domain}.events`; dead-letter topic comments must not imply a different runtime pattern.
- Rule 4 still applies: no application-level pub/sub retry loops. DAPR resiliency and drain reminders own retries.
- Rule 5 still applies: comments must not encourage logging event payload data.
- Rule 6 still applies: actor state remains accessed through `IActorStateManager`; do not introduce direct `DaprClient` state access while touching nearby comments.
- DAPR access-control and pub/sub scopes are runtime security boundaries. Comment cleanup must not alter component metadata values.

### Current-Code Intelligence

- The source search at story creation found obsolete labels in `AggregateActor.cs`, `UnpublishedEventsRecord.cs`, `EventDrainOptions.cs`, `EventPublisherOptions.cs`, `EventStoreActivitySource.cs`, and three AppHost DAPR component YAML files.
- Story 4.2 is the current owner of persist-then-publish failure handling, `UnpublishedEventsRecord`, drain reminders, and backlog draining.
- Story 4.1 is the current owner of CloudEvents publication and tenant-domain topic routing.
- Story 4.3 is the current owner of per-aggregate backpressure. It also mentions drain-pending records because those records affect pending command count; do not rewrite that behavior.
- Dead-letter routing appears in the codebase as infrastructure-failure handling and pub/sub dead-letter topic configuration, but the current Epic 4 story map has no standalone Story 4.5. Neutral wording is safer than inventing a new current story owner.
- R4-A5 is in `review` at story creation and owns Tier 3 command-to-subscriber delivery proof. R4-A6 is `ready-for-dev` and owns drain incomplete-range guard behavior. R4-A8 is only traceability cleanup.

### Testing Standards

- This is a comment-only source cleanup. The primary verification is diff review, grep gates, and release build.
- If test projects are run beyond build, run the repository's Tier 1 unit projects individually.
- Do not run a solution-level `dotnet test`; repository instructions call out project-level test runs.
- `Hexalith.EventStore.Server.Tests` has a known pre-existing CA2007 warning-as-error build issue in some contexts; record if encountered but do not weaken the R4-A8 comment-only gate.

### Latest Technical Information

No external technical research is needed for this story. It does not change DAPR, Aspire, .NET, CloudEvents, or OpenTelemetry behavior; it only aligns comments with the repository's current story map.

### Project Structure Notes

- Expected source edits are limited to comments in `src/`.
- Expected optional test edits are limited to comments in `tests/Hexalith.EventStore.Server.Tests/`.
- Expected BMAD edits are this story file plus `sprint-status.yaml` during dev handoff.
- Keep all completed story artifacts and retrospective documents as historical records.

### References

- [Source: `_bmad-output/implementation-artifacts/epic-4-retro-2026-04-26.md`] - R4-A8 action item: normalize old Story 4.4 / 4.5 source comments to current Epic 4 story numbers.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md#Proposal-8`] - R4-A8 acceptance criteria: comments reference current story numbers or neutral feature names, no behavior changes, build remains clean.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-28.md#Proposal-6`] - R4-A8 remains low-risk and non-behavioral.
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-4-Event-Distribution-and-PubSub`] - Current Epic 4 story map has Stories 4.1, 4.2, and 4.3 only.
- [Source: `_bmad-output/implementation-artifacts/4-1-cloudevents-publication-and-topic-routing.md`] - Current publication/topic routing owner.
- [Source: `_bmad-output/implementation-artifacts/4-2-resilient-publication-and-backlog-draining.md`] - Current drain recovery owner.
- [Source: `_bmad-output/implementation-artifacts/4-3-per-aggregate-backpressure.md`] - Current backpressure owner.
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`] - Main source comments with stale labels.
- [Source: `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`] - Local pub/sub dead-letter comments with stale labels.

## Dev Agent Record

### Agent Model Used

To be filled by dev agent.

### Debug Log References

To be filled by dev agent.

### Completion Notes List

To be filled by dev agent.

### File List

To be filled by dev agent.

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-01 | 0.1 | Created ready-for-dev R4-A8 story-numbering comment cleanup story. | Codex automation |

## Verification Status

Story creation only. Runtime, build, and test execution are intentionally deferred to `bmad-dev-story`.
