# Post-Epic-4 R4-A6: Drain Integrity Guard

Status: ready-for-dev

<!-- Source: epic-4-retro-2026-04-26.md R4-A6 -->
<!-- Source: sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md Proposal 6 -->
<!-- Source: sprint-change-proposal-2026-04-28.md Proposal 4 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform maintainer responsible for zero-loss event recovery**,
I want drain recovery to fail visibly when an unpublished event range is incomplete,
So that at-least-once recovery never turns state-store inconsistency into apparent drain success.

## Story Context

Epic 4 delivered persisted CloudEvents publication, tenant-domain topic routing, resilient backlog draining, and per-aggregate backpressure. The retrospective found one remaining data-integrity risk: a drain record can claim a sequence range and event count, but if any event key in that range is missing, the drain must not publish a shorter list and clear the record as if recovery succeeded.

This story closes R4-A6 from the Epic 4 retrospective. It is a narrow guard-pinning story around `DrainUnpublishedEventsAsync`, not a pub/sub runtime proof. R4-A5 owns the running AppHost subscriber proof. R4-A8 owns stale source-comment numbering cleanup. R4-A6 owns only the incomplete-range behavior.

Current HEAD `8ff581f` already contains a stronger implementation than the older Story 4.2 completion note suggested: `LoadPersistedEventsRangeAsync` calls `ReadEventsRangeAsync`, and `ReadEventsRangeAsync` throws `MissingEventException` when any expected event key is absent. `DrainUnpublishedEventsAsync` catches exceptions, increments `UnpublishedEventsRecord.RetryCount`, stores the updated record, saves state, logs a warning, and leaves the reminder active. This story must pin that behavior with explicit tests for missing first, middle, and last events, and add any missing operational signal detail needed to make the failure diagnosable.

## Acceptance Criteria

1. **Drain refuses incomplete ranges before publish.** Given an `UnpublishedEventsRecord` with `StartSequence`, `EndSequence`, and `EventCount`, when any event key in that exact range is missing from actor state, then `DrainUnpublishedEventsAsync` must not call `IEventPublisher.PublishEventsAsync` with a partial event list.

2. **Drain record is preserved and retry state advances.** Given an incomplete persisted range, when the drain reminder fires, then the `drain:{correlationId}` state record remains present, `RetryCount` increments by exactly 1, `LastFailureReason` records the missing sequence failure, and `SaveStateAsync` is called to commit the updated record.

3. **Reminder remains active on integrity failure.** Given the incomplete range failure path, when the handler completes, then the drain reminder is not unregistered. The next reminder tick must be able to retry after the missing state is repaired.

4. **Missing first, middle, and last event positions are covered.** Tests explicitly cover missing `StartSequence`, one middle sequence, and `EndSequence` for a range of at least three events. Each test asserts no publish call, no record removal, retry increment, and a failure reason that identifies the missing sequence.

5. **Successful complete-range drain behavior is unchanged.** Existing happy-path drain behavior remains intact: complete ranges are published in sequence order, the record is removed, pending command count is decremented, state is saved, reminder is unregistered, and advisory status moves to `Completed` or `Rejected` based on `UnpublishedEventsRecord.IsRejection`.

6. **Operational signal is explicit.** The failure path emits a warning-level log and activity error status containing enough context for operators to identify the correlation id, tenant, domain, aggregate id, retry count, and missing sequence. If the existing `MissingEventException` message is the source of sequence detail, tests or code review notes must prove it is preserved in `LastFailureReason` or logging.

7. **No broad state-store access is introduced.** The implementation continues to use `IActorStateManager` and actor-scoped keys. Do not use `DaprClient.QueryStateAsync`, broad state queries, direct Redis scans, or direct `DaprClient.GetStateAsync/SetStateAsync` to inspect actor state.

8. **Existing runtime and sibling scopes remain separate.** Do not add a Tier 3 subscriber proof, do not change DAPR pub/sub component scopes, do not alter AppHost topology, and do not absorb R4-A8 story-number comment cleanup. R4-A5 and R4-A8 stay as their own stories.

9. **Tier 1 and Tier 2 verification is captured.** Run the four Tier 1 unit test projects individually per repository instructions. Run targeted `Hexalith.EventStore.Server.Tests` tests for drain recovery and any changed server tests. If the pre-existing full server-test CA2007 build failure or Docker-dependent infra failures still apply, record the exact shape without weakening this story's targeted test gate.

10. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and both `last_updated` fields in `sprint-status.yaml` name R4-A6 and the result. At code-review signoff, both become `done`. Do not touch R4-A5 or R4-A8 status rows.

## Scope Boundaries

- Do not redesign event persistence, event key layout, CloudEvents metadata, or topic derivation.
- Do not add application-level retry loops around `IEventPublisher`; DAPR resiliency and the actor reminder drain remain the recovery mechanisms.
- Do not create a new global drain registry or admin endpoint.
- Do not change `UnpublishedEventsRecord` shape unless required to satisfy AC #6; prefer preserving the existing record and `IncrementRetry` pattern.
- Do not change source XML comments that only mention old Story 4.4 / 4.5 numbering; R4-A8 owns that cleanup.

## Implementation Inventory

| Area | File | Expected use |
|---|---|---|
| Drain handler | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:640-803` | `DrainUnpublishedEventsAsync`: load record, load range, publish, remove record on success, increment retry on failure |
| Range loader | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1076-1100` | `LoadPersistedEventsRangeAsync`: validates recorded start/end range and calls `ReadEventsRangeAsync` |
| Exact event reads | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1238-1260` | `ReadEventsRangeAsync`: reads each event key and throws `MissingEventException` when a key is absent |
| Missing event exception | `src/Hexalith.EventStore.Server/Events/MissingEventException.cs` | Existing corruption signal with sequence, tenant, domain, aggregate id |
| Drain record | `src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs` | `drain:{correlationId}` key, `drain-unpublished-{correlationId}` reminder, `IncrementRetry` |
| Drain options | `src/Hexalith.EventStore.Server/Configuration/EventDrainOptions.cs` | Timing only; should not need changes |
| Existing drain tests | `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs` | Add integrity guard tests beside current drain success/failure tests |
| Publish-failure tests | `tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs` | Reference only unless resume preparation needs a regression pin |
| Story 4.2 artifact | `_bmad-output/implementation-artifacts/4-2-resilient-publication-and-backlog-draining.md` | Historical source for the gap; do not edit unless dev-story records a discovered documentation correction and the project lead agrees |
| R4-A5 sibling | `_bmad-output/implementation-artifacts/post-epic-4-r4a5-tier3-pubsub-delivery.md` | Runtime proof remains separate and currently ready-for-dev |

## Tasks / Subtasks

- [ ] Task 0: Baseline and current-state confirmation
    - [ ] 0.1 Record current HEAD SHA and confirm this story is still `ready-for-dev`.
    - [ ] 0.2 Read the implementation inventory files before editing.
    - [ ] 0.3 Confirm current drain code path: `LoadPersistedEventsRangeAsync` -> `ReadEventsRangeAsync` -> `MissingEventException` on absent state.
    - [ ] 0.4 Record existing drain test count and targeted test baseline.

- [ ] Task 1: Add explicit missing-range tests (AC: #1, #2, #3, #4)
    - [ ] 1.1 Add a helper in `EventDrainRecoveryTests.cs` that configures a three-event record while omitting one requested sequence.
    - [ ] 1.2 Add missing-first test: sequence `StartSequence` absent.
    - [ ] 1.3 Add missing-middle test: one sequence between start and end absent.
    - [ ] 1.4 Add missing-last test: sequence `EndSequence` absent.
    - [ ] 1.5 In each test, assert `PublishEventsAsync` is not called, `RemoveStateAsync("drain:{correlationId}")` is not called, `UnregisterReminderAsync` is not called, `SetStateAsync` stores `RetryCount + 1`, and `LastFailureReason` identifies the missing sequence.

- [ ] Task 2: Tighten operational signal only if needed (AC: #2, #6)
    - [ ] 2.1 If existing `MissingEventException.Message` already flows to `LastFailureReason` and warning logs, prove it in tests or completion notes.
    - [ ] 2.2 If missing sequence detail is not visible enough, update the catch path in `DrainUnpublishedEventsAsync` to log or tag it without changing the retry semantics.
    - [ ] 2.3 Preserve `OperationCanceledException` propagation.

- [ ] Task 3: Preserve success path and sibling boundaries (AC: #5, #7, #8)
    - [ ] 3.1 Re-run existing happy-path drain tests and ensure complete ranges still remove the record and unregister the reminder.
    - [ ] 3.2 Verify no broad DAPR state access or direct backend scan was added.
    - [ ] 3.3 Verify no AppHost, DAPR component, R4-A5, or R4-A8 files changed.

- [ ] Task 4: Verification and bookkeeping (AC: #9, #10)
    - [ ] 4.1 Run targeted server tests for `EventDrainRecoveryTests` and any changed tests.
    - [ ] 4.2 Run Tier 1 unit test projects individually.
    - [ ] 4.3 Run `dotnet build Hexalith.EventStore.slnx --configuration Release`.
    - [ ] 4.4 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
    - [ ] 4.5 Update only the R4-A6 sprint-status row and `last_updated` values.

## Dev Notes

### Architecture Guardrails

- ADR-P2 says state store persistence is the source of truth and pub/sub is only the distribution mechanism. A drain must never clear recovery work unless it has loaded the complete persisted range recorded by `UnpublishedEventsRecord`.
- D6 topic naming remains `{tenant}.{domain}.events`; R4-A6 should not touch topic derivation.
- `CommandStatus.PublishFailed` represents events stored but not yet published after DAPR retry exhaustion. Drain success may later write advisory `Completed` or `Rejected`; incomplete range failure must keep the command in a recoverable state.
- `MissingEventException` already represents state-store data corruption and carries the missing sequence plus tenant/domain/aggregate identity. Prefer using that signal over inventing a second exception type.
- Actor reminders are the correct mechanism for continued drain attempts. Dapr v1.17 documentation says reminders persist across actor deactivation and failover and continue until explicitly removed or exhausted by policy; this aligns with preserving the reminder on integrity failure.
- DAPR pub/sub uses CloudEvents 1.0 for message format, but this story is below the subscriber layer. Do not add subscriber assertions here.
- Pub/sub topic scopes and Aspire Dapr sidecar configuration are R4-A5 concerns. Dapr's pub/sub scope docs warn that sensitive topics require explicit publishing and subscription scopes; do not relax those scopes for R4-A6.

### Current-Code Intelligence

- The older Story 4.2 completion note says missing events were "simply not returned"; that is not true at current HEAD `8ff581f`. Current `ReadEventsRangeAsync` throws `MissingEventException` when `TryGetStateAsync<EventEnvelope>` returns no value.
- `DrainUnpublishedEventsAsync` catches non-cancellation exceptions, increments retry through `record.IncrementRetry(ex.Message)`, persists the updated record with the same key, saves state, logs warning, and leaves the reminder active.
- Existing tests cover generic publish failure and record preservation, but they do not explicitly exercise missing first/middle/last persisted events. That is the gap this story closes.
- R4-A2b removed `IBackpressureTracker`; actor-level drain success still decrements the persisted pending-command counter in `AggregateActor.cs:707-708`.

### Testing Standards

- Tier 1 projects should be run individually: `Hexalith.EventStore.Client.Tests`, `Hexalith.EventStore.Contracts.Tests`, `Hexalith.EventStore.Sample.Tests`, and `Hexalith.EventStore.Testing.Tests`.
- For changed Tier 2 tests, inspect actor state-manager side effects, not just method return values. The critical assertions are no partial publish, no drain record removal, retry record persisted, and reminder not unregistered.
- Use Shouldly and NSubstitute patterns already present in `EventDrainRecoveryTests.cs`.
- Avoid Docker/AppHost requirements for this story's core gate. Missing-range tests belong in mocked server tests because the behavior is actor-state deterministic.

### Latest Technical Information

- Dapr v1.17 docs describe actor reminders as persistent callbacks that continue across actor deactivation and failover until unregistered; the story relies on not unregistering the reminder after an integrity failure. Source: <https://docs.dapr.io/developing-applications/building-blocks/actors/actors-timers-reminders/>
- Dapr pub/sub docs state Dapr wraps topic messages in CloudEvents 1.0 by default; no raw subscriber behavior is needed in this story. Source: <https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/>
- Dapr pub/sub scope docs warn that protected or sensitive topics should explicitly list allowed publishers and subscribers. R4-A6 does not touch scopes; R4-A5 owns that runtime proof. Source: <https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-scopes/>
- Aspire Dapr integration docs show Dapr sidecars are added through Aspire hosting integration and require Dapr CLI initialization. R4-A6 should not change AppHost sidecars. Source: <https://aspire.dev/integrations/frameworks/dapr/>

### Project Structure Notes

- Expected source edit, if any: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`.
- Expected test edit: `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs`.
- Likely no changes needed in `UnpublishedEventsRecord.cs`, `EventDrainOptions.cs`, AppHost, DAPR components, integration tests, or documentation pages.
- Keep one public type per file and existing feature-folder organization.
- Keep comments sparse; tests should carry most of the behavior documentation.

### References

- [Source: `_bmad-output/implementation-artifacts/epic-4-retro-2026-04-26.md`] - R4-A6 action item: add a drain integrity guard for incomplete event ranges.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md#Proposal-6`] - Acceptance criteria for loaded-count mismatch, record preservation, retry or signal, missing first/middle/last tests, success path unchanged.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-28.md#Proposal-4`] - Follow-through framing: incomplete state must be visible, not converted into apparent success.
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-4-Event-Distribution-and-PubSub`] - Story 4.2 requires all events persisted during an outage to be delivered and no events silently dropped.
- [Source: `_bmad-output/planning-artifacts/prd.md#Reliability`] - NFR22 and NFR24 require zero event loss and no silently dropped events after pub/sub recovery.
- [Source: `_bmad-output/planning-artifacts/architecture.md#ADR-P2-Persist-Then-Publish-Event-Flow`] - State store is source of truth; pub/sub is distribution.
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:640-803`] - Current drain handler behavior.
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1076-1100`] - Current recorded-range loader.
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1238-1260`] - Current exact event read and missing-event throw.
- [Source: `src/Hexalith.EventStore.Server/Events/MissingEventException.cs`] - Existing corruption exception and message shape.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs`] - Existing test patterns to extend.

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
| 2026-05-01 | 0.1 | Created ready-for-dev R4-A6 drain integrity guard story. | Codex automation |

## Verification Status

Story creation only. Runtime, build, and test execution are intentionally deferred to `bmad-dev-story`.
