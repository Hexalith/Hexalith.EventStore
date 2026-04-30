# Post-Epic-11 R11-A2: Polling-Mode Product Behavior

Status: ready-for-dev

<!-- Source: epic-11-retro-2026-04-30.md - Action item R11-A2 -->
<!-- Source: epic-12-retro-2026-04-30.md - R12-A5 carry-forward backlog -->
<!-- Source: docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md - Background Poller -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform engineer hardening the server-managed projection builder,
I want `RefreshIntervalMs > 0` to have explicit, working polling behavior instead of silently disabling projection delivery,
so that operators can choose batched projection updates without losing automatic projection freshness or overloading command processing.

## Story Context

Epic 11 shipped the server-managed projection path in immediate mode: after events are persisted, `EventPublisher` triggers `ProjectionUpdateOrchestrator`, which reads aggregate events, calls the domain service `/project` endpoint, writes `ProjectionState` to `EventReplayProjectionActor`, regenerates ETags, and broadcasts projection-change signals.

The current polling-mode behavior is intentionally incomplete. `ProjectionOptions` accepts `DefaultRefreshIntervalMs` and per-domain `RefreshIntervalMs`, but `ProjectionUpdateOrchestrator` logs `PollingModeDeferred` and skips updates when the resolved interval is greater than zero. Epic 11 retro action R11-A2 requires this to be resolved: either implement polling or prevent users from relying on it. This story implements the polling behavior described by the projection-builder design while preserving immediate mode for `RefreshIntervalMs = 0`.

This story is sequenced after R11-A1. Reuse the checkpoint/tracking boundary from `post-epic-11-r11a1-checkpoint-tracked-projection-delivery`; do not create a second projection identity registry if R11-A1 already provides one. If R11-A1 has not landed when development starts, block or re-sequence this story rather than duplicating checkpoint storage.

## Acceptance Criteria

1. **Polling mode has product-defined semantics.** `RefreshIntervalMs = 0` keeps immediate fire-and-forget delivery. `RefreshIntervalMs > 0` means event publication records the affected aggregate as needing projection work and the background poller performs delivery on interval ticks. Negative intervals remain invalid through existing options validation.

2. **A polling service is registered only when projection infrastructure is enabled.** `Hexalith.EventStore.Server` adds an injectable hosted service, for example `ProjectionPollerService`, from `EventStoreServerServiceCollectionExtensions.AddEventStoreServer`. It uses existing `ProjectionOptions`, domain service resolution, actor proxy, checkpoint/tracking, and projection update boundaries; it must not require a new package.

3. **New aggregates are discoverable in polling mode.** When events are published for a domain whose resolved refresh interval is greater than zero, the system records `TenantId`, `Domain`, and `AggregateId` in the existing projection checkpoint/tracker boundary before returning from the fire-and-forget projection path. The record must be idempotent and safe to repeat. A newly registered aggregate with no delivered checkpoint is eligible for polling from sequence 0.

4. **Polling delivery reuses the orchestrator logic.** The poller must call the same delivery path used by immediate mode after R11-A1, or a shared internal collaborator extracted from it. Domain service invocation, event mapping, projection actor writes, checkpoint advancement, ETag regeneration, SignalR broadcasting, fail-open logging, and at-least-once semantics must stay identical between immediate and polling modes.

5. **Polling does not deliver immediately per command.** For `RefreshIntervalMs > 0`, event publication must not call the domain service `/project` endpoint on every command. It may mark the aggregate dirty/tracked. The first projection update is allowed on the next poll tick, and the story must document this operator-visible delay.

6. **The poller respects domain intervals and overrides.** A domain-specific `EventStore:Projections:Domains:{domain}:RefreshIntervalMs` overrides `DefaultRefreshIntervalMs`. Domains with `0` are not polled and still use immediate mode. Domains with `>0` are polled at their resolved interval. Orphaned per-domain config keeps the existing warning behavior from `ProjectionDiscoveryHostedService`.

7. **Polling work is bounded and non-overlapping.** The poller must not start overlapping update attempts for the same `{tenantId, domain, aggregateId}` if a previous attempt is still running. A slow or failing aggregate must not block polling for unrelated aggregate identities longer than necessary. The implementation may process sequentially with clear limits or use bounded concurrency, but unbounded fan-out is not acceptable.

8. **Shutdown and cancellation are graceful.** The hosted service honors the application stopping token, exits promptly, and does not swallow `OperationCanceledException` as an error. Use `BackgroundService` plus `PeriodicTimer` or an equivalent testable abstraction that follows Microsoft hosted-service guidance.

9. **Failures remain fail-open.** Polling failures are logged and retried on a later tick. Domain resolver failures, aggregate read failures, HTTP failures, invalid projection responses, projection actor write failures, and checkpoint save failures must not fail command processing or stop the hosted service permanently.

10. **Configuration and logs no longer overpromise.** The existing `PollingModeDeferred` log message is removed or replaced. Startup discovery and runtime logs must say polling is active for configured polling domains. Developer/operator documentation states the interval delay, stale-read behavior, and at-least-once duplicate-delivery expectation.

11. **Tests pin the polling contract.** Unit coverage proves: `0` still routes to immediate delivery; `>0` records/tracks without immediate `/project`; the poller invokes delivery on tick for tracked identities; per-domain override wins; no overlap for the same identity; cancellation exits cleanly; failure classes are logged and retried without stopping the service.

12. **Existing projection behavior still passes.** Existing `ProjectionUpdateOrchestratorTests`, `ProjectionUpdateOrchestratorRefreshIntervalTests`, `ProjectionDiscoveryHostedServiceTests`, `EventReplayProjectionActorTests`, and R11-A1 checkpoint tests remain green. Any test that previously expected polling to skip forever must be updated to assert registration plus deferred poll delivery.

## Tasks / Subtasks

- [ ] Task 1: Confirm R11-A1 checkpoint/tracking boundary is available (AC: #3, #4)
  - [ ] Reuse the R11-A1 tracker contract for known projection identities and checkpoint reads/writes.
  - [ ] If the tracker cannot enumerate or register known identities, extend it in one place instead of adding a parallel registry.
  - [ ] Keep identity shape canonical: `TenantId`, `Domain`, `AggregateId`; do not depend on DAPR actor state key internals.

- [ ] Task 2: Refactor projection delivery into a shared path if needed (AC: #4, #9, #12)
  - [ ] Keep immediate mode behavior unchanged for interval `0`.
  - [ ] Extract only enough logic for both immediate and polling callers to share domain resolution, `GetEventsAsync`, `/project`, `UpdateProjectionAsync`, checkpoint save, and logging.
  - [ ] Preserve public projection DTOs and actor interfaces unless R11-A1 already changed an internal server-only boundary.

- [ ] Task 3: Implement polling-mode registration from event publication (AC: #1, #3, #5)
  - [ ] When resolved interval is `>0`, mark the aggregate identity as tracked/dirty instead of invoking `/project` immediately.
  - [ ] Make repeated registration idempotent.
  - [ ] Ensure no projection payload or event body is logged while registering work.

- [ ] Task 4: Add `ProjectionPollerService` or equivalent hosted worker (AC: #2, #6, #7, #8, #9)
  - [ ] Use `ProjectionOptions.GetRefreshIntervalMs(domain)` for interval decisions.
  - [ ] Poll only tracked identities whose resolved interval is greater than zero.
  - [ ] Prevent overlapping delivery attempts for the same identity.
  - [ ] Honor cancellation and do not treat normal shutdown as a failure.
  - [ ] Keep concurrency bounded and observable through structured logs.

- [ ] Task 5: Update discovery and operator logs (AC: #6, #10)
  - [ ] Replace "polling not implemented" startup/runtime wording with active polling semantics.
  - [ ] Log domain, tenant, aggregate identity, interval, and exception type where useful; do not log event payloads or projection state.
  - [ ] Preserve orphaned domain-configuration warnings.

- [ ] Task 6: Expand focused server tests (AC: #1, #3, #5, #6, #7, #8, #9, #11, #12)
  - [ ] Add poller tests with a testable timer/tick abstraction rather than sleeping real intervals.
  - [ ] Add registration tests for `RefreshIntervalMs > 0` and non-registration for immediate domains.
  - [ ] Add no-overlap and failure-retry tests.
  - [ ] Update refresh-interval tests that currently assert permanent skip.

- [ ] Task 7: Update documentation and validation evidence (AC: #10, #12)
  - [ ] Update `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md` or developer docs to describe active polling mode.
  - [ ] Record that polling mode introduces interval-delayed projection freshness and keeps at-least-once semantics.
  - [ ] Run targeted tests and record results in this story's Dev Agent Record.

## Dev Notes

### Existing Implementation To Reuse

- `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs` already models `DefaultRefreshIntervalMs`, per-domain overrides, case-insensitive fallback lookup, and negative-value validation.
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` already resolves refresh mode, maps events to `ProjectionEventDto`, invokes `/project`, writes to `IProjectionWriteActor`, and logs failure classes. R11-A1 should replace the hard-coded `GetEventsAsync(0)` with checkpoint-driven reads.
- `src/Hexalith.EventStore.Server/Projections/ProjectionDiscoveryHostedService.cs` already logs discovered domains and warns about orphaned projection config. Update wording; do not remove the validation signal.
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` already isolates projection updates from command publication through a background task and fail-open error handling.
- `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs` remains the projection write/read actor and owns ETag/SignalR invalidation through existing notification boundaries.

### Implementation Guardrails

- Do not change `ProjectionRequest`, `ProjectionResponse`, `ProjectionEventDto`, `ProjectionState`, `IProjectionWriteActor`, or client query contracts for this story.
- Do not implement a second checkpoint store. R11-A1 owns checkpoint delivery state; R11-A2 may extend the same boundary for tracked identity enumeration if needed.
- Do not use direct DAPR actor state-key scans for aggregate discovery. Polling must use an explicit tracker/registry contract controlled by EventStore.
- Do not use actor timers for this product behavior. The design calls for an `IHostedService` poller, and Dapr actor timers are not retained after actor deactivation. Actor reminders are persistent but would add a different scheduler/control-plane contract than this story needs.
- Do not make SignalR the source of projection truth. Polling updates projection state; SignalR remains an invalidation signal that causes clients to re-query.
- Do not make polling exactly-once. Preserve at-least-once delivery, duplicate tolerance, and stale projection acceptance on failure.

### Suggested File Touches

- `src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs`
- `src/Hexalith.EventStore.Server/Projections/IProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs` or the R11-A1 equivalent tracker interface
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionDiscoveryHostedService.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionPollerServiceTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs`
- `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md`

### Architecture And Version Notes

- Package versions are centrally pinned in `Directory.Packages.props`: Dapr `1.17.7`, Aspire `13.2.2`, .NET extensions `10.x`, xUnit v3, Shouldly, and NSubstitute. Do not add Hangfire, Quartz, or another scheduler for this story.
- Microsoft hosted-service guidance uses `BackgroundService` for long-running workers and shows `PeriodicTimer` for asynchronous timed background tasks. `PeriodicTimer` supports asynchronous ticks but only one `WaitForNextTickAsync` call may be in flight for a timer.
- Dapr actor timers and reminders are useful actor primitives, but timers are not retained after actor deactivation. This story's polling behavior should be service-level and testable without binding product behavior to actor reminder migration or scheduler availability.
- Dapr state APIs support ETags and optimistic concurrency, but R11-A2 should rely on the R11-A1 checkpoint/tracker abstraction rather than directly coding against state-store details.

### Previous Story Intelligence

- Epic 11 retro says polling mode is configured but not implemented; users must not continue seeing "configured" as "working" without active behavior.
- R11-A1 narrows delivery from full replay to checkpoint-based reads. R11-A2 should build on that checkpoint model; polling without checkpoints would regress to repeated full replay.
- Epic 12 proved the sample UI can demonstrate refresh behavior, but UI smoke evidence is not proof that polling works. Polling needs server-side tests and, ideally, a later AppHost proof under R11-A3/R11-A4.
- Lessons ledger L09 applies only if this story touches sample UI evidence. It does not require a Blazor smoke test for this server-side polling story.

### Project Structure Notes

- Keep projection infrastructure under `src/Hexalith.EventStore.Server/Projections/`.
- Keep configuration registration in `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`.
- Keep focused server tests under `tests/Hexalith.EventStore.Server.Tests/Projections/`.
- Use existing logging source-generator style for new structured logs.

## References

- `_bmad-output/implementation-artifacts/epic-11-retro-2026-04-30.md` - R11-A2 action item and polling-mode risk.
- `_bmad-output/implementation-artifacts/epic-12-retro-2026-04-30.md` - R12-A5 carry-forward of R11-A1 through R11-A4.
- `_bmad-output/implementation-artifacts/post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md` - required predecessor and checkpoint boundary.
- `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md` - background poller, checkpoint tracker, error handling, and configuration.
- `_bmad-output/planning-artifacts/epics.md` - Epic 11 server-managed projection builder requirements.
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` - current polling skip and delivery flow.
- `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs` - interval and per-domain override semantics.
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs` - existing tests that pin current deferred behavior.
- Dapr actor timers/reminders docs: https://docs.dapr.io/developing-applications/building-blocks/actors/actors-timers-reminders/
- Dapr state API docs: https://docs.dapr.io/reference/api/state_api/
- Microsoft hosted services docs: https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services
- Microsoft `PeriodicTimer` docs: https://learn.microsoft.com/dotnet/api/system.threading.periodictimer

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

### Completion Notes List

### File List
