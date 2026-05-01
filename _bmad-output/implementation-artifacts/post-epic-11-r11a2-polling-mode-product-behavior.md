# Post-Epic-11 R11-A2: Polling-Mode Product Behavior

Status: review

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

3. **New aggregates are discoverable in polling mode.** When events are published for a domain whose resolved refresh interval is greater than zero, the existing fire-and-forget projection path records `TenantId`, `Domain`, and `AggregateId` in the projection checkpoint/tracker boundary instead of invoking `/project` immediately. The record must be idempotent and safe to repeat. Repeated publication for an already known identity must still leave that identity eligible for later polling; an "already registered" fast path must not accidentally suppress dirty-work visibility. Registration failure is logged and swallowed like other projection-side failures; it must not fail command processing, and a later event for the same identity may retry registration. A newly registered aggregate with no delivered checkpoint is eligible for polling from sequence 0.

4. **Polling delivery reuses the orchestrator logic.** The poller must call the same delivery path used by immediate mode after R11-A1, or a shared internal collaborator extracted from it. The callable polling entry point must bypass only the `RefreshIntervalMs > 0` immediate-mode deferral guard; it must not bypass domain service invocation, event mapping, projection actor writes, checkpoint advancement, ETag regeneration, SignalR broadcasting, fail-open logging, or at-least-once semantics.

5. **Polling does not deliver immediately per command.** For `RefreshIntervalMs > 0`, event publication must not call the domain service `/project` endpoint on every command. It may mark the aggregate dirty/tracked. The first projection update is allowed on the next poll tick, and the story must document this operator-visible delay.

6. **The poller respects domain intervals and overrides.** A domain-specific `EventStore:Projections:Domains:{domain}:RefreshIntervalMs` overrides `DefaultRefreshIntervalMs`. Domains with `0` are not polled and still use immediate mode. Domains with `>0` are polled at their resolved interval, and the scheduling design must not let the shortest configured interval cause every polling domain to run on every tick. Orphaned per-domain config keeps the existing warning behavior from `ProjectionDiscoveryHostedService`.

7. **Polling work is bounded and non-overlapping.** The poller must not start overlapping update attempts for the same `{tenantId, domain, aggregateId}` if a previous attempt is still running within the same process. Multi-instance duplicates remain allowed under the existing at-least-once contract and must be reconciled by checkpoint/concurrency behavior, not by inventing a new distributed lock in this story. A slow or failing aggregate must not block polling for unrelated aggregate identities longer than necessary. The implementation may process sequentially with clear limits or use bounded concurrency, but unbounded fan-out is not acceptable.

8. **Shutdown and cancellation are graceful.** The hosted service honors the application stopping token, exits promptly, and does not swallow `OperationCanceledException` as an error. Use `BackgroundService` plus `PeriodicTimer` or an equivalent testable abstraction that follows Microsoft hosted-service guidance.

9. **Failures remain fail-open.** Polling failures are logged and retried on a later tick. Domain resolver failures, aggregate read failures, HTTP failures, invalid projection responses, projection actor write failures, and checkpoint save failures must not fail command processing or stop the hosted service permanently.

10. **Configuration and logs no longer overpromise.** The existing `PollingModeDeferred` log message is removed or replaced. Startup discovery and runtime logs must say polling is active for configured polling domains. Developer/operator documentation states the interval delay, stale-read behavior, and at-least-once duplicate-delivery expectation.

11. **Tests pin the polling contract.** Unit coverage proves: `0` still routes to immediate delivery; `>0` records/tracks without immediate `/project`; the poller invokes delivery on tick for tracked identities; per-domain override wins; no overlap for the same identity; cancellation exits cleanly; failure classes are logged and retried without stopping the service.

12. **Existing projection behavior still passes.** Existing `ProjectionUpdateOrchestratorTests`, `ProjectionUpdateOrchestratorRefreshIntervalTests`, `ProjectionDiscoveryHostedServiceTests`, `EventReplayProjectionActorTests`, and R11-A1 checkpoint tests remain green. Any test that previously expected polling to skip forever must be updated to assert registration plus deferred poll delivery.

13. **Closure evidence rejects the old deferred mode.** Validation must include a grep or equivalent source audit proving the old `PollingModeDeferred` path no longer represents the runtime behavior, plus targeted evidence that `RefreshIntervalMs > 0` reaches registration and poller delivery instead of a permanent skip.

## Tasks / Subtasks

- [x] Task 1: Confirm R11-A1 checkpoint/tracking boundary is available (AC: #3, #4)
  - [x] Reuse the R11-A1 tracker contract for known projection identities and checkpoint reads/writes.
  - [x] If the tracker cannot enumerate or register known identities, extend it in one place instead of adding a parallel registry.
  - [x] Make identity enumeration bounded or pageable enough that a large state store cannot be loaded into memory in one unbounded read.
  - [x] Keep identity shape canonical: `TenantId`, `Domain`, `AggregateId`; do not depend on DAPR actor state key internals.
  - [x] Compile against the actual R11-A1 tracker API before marking this task complete; do not assume method names from the story text if R11-A1 landed with different internal names.

- [x] Task 2: Refactor projection delivery into a shared path if needed (AC: #4, #9, #12)
  - [x] Keep immediate mode behavior unchanged for interval `0`.
  - [x] Extract only enough logic for both immediate and polling callers to share domain resolution, `GetEventsAsync`, `/project`, `UpdateProjectionAsync`, checkpoint save, and logging.
  - [x] Ensure the polling caller does not route through a public method that immediately returns because `RefreshIntervalMs > 0`; use a private/internal delivery method or equivalent collaborator for the actual projection work.
  - [x] Preserve public projection DTOs and actor interfaces unless R11-A1 already changed an internal server-only boundary.

- [x] Task 3: Implement polling-mode registration from event publication (AC: #1, #3, #5)
  - [x] When resolved interval is `>0`, mark the aggregate identity as tracked/dirty instead of invoking `/project` immediately.
  - [x] Make repeated registration idempotent.
  - [x] Add or preserve a dirty-work signal on repeated publication for an already tracked identity, so later events are not hidden by an idempotent no-op.
  - [x] Ensure no projection payload or event body is logged while registering work.

- [x] Task 4: Add `ProjectionPollerService` or equivalent hosted worker (AC: #2, #6, #7, #8, #9)
  - [x] Use `ProjectionOptions.GetRefreshIntervalMs(domain)` for interval decisions.
  - [x] Poll only tracked identities whose resolved interval is greater than zero.
  - [x] Track per-domain due times or equivalent scheduling state so domains with longer intervals are not polled on every shorter-interval tick.
  - [x] Prevent overlapping delivery attempts for the same identity.
  - [x] Honor cancellation and do not treat normal shutdown as a failure.
  - [x] Keep concurrency bounded and observable through structured logs.

- [x] Task 5: Update discovery and operator logs (AC: #6, #10)
  - [x] Replace "polling not implemented" startup/runtime wording with active polling semantics.
  - [x] Log domain, tenant, aggregate identity, interval, and exception type where useful; do not log event payloads or projection state.
  - [x] Preserve orphaned domain-configuration warnings.

- [x] Task 6: Expand focused server tests (AC: #1, #3, #5, #6, #7, #8, #9, #11, #12)
  - [x] Add poller tests with a testable timer/tick abstraction rather than sleeping real intervals.
  - [x] Add registration tests for `RefreshIntervalMs > 0` and non-registration for immediate domains.
  - [x] Add registration-failure tests proving command publication remains fail-open and a later registration attempt can retry the same identity.
  - [x] Add a repeated-publication test proving an already tracked identity remains eligible for polling after new events arrive.
  - [x] Add per-domain interval tests proving a fast polling domain does not force slower polling domains to run on every fast tick.
  - [x] Add validation evidence that `PollingModeDeferred` is removed or no longer reachable for configured polling behavior.
  - [x] Add no-overlap and failure-retry tests.
  - [x] Update refresh-interval tests that currently assert permanent skip.

- [x] Task 7: Update documentation and validation evidence (AC: #10, #12)
  - [x] Update `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md` or developer docs to describe active polling mode.
  - [x] Record that polling mode introduces interval-delayed projection freshness and keeps at-least-once semantics.
  - [x] Run targeted tests and record results in this story's Dev Agent Record.

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
- Do not implement tracked-identity enumeration as a full unbounded state-store scan. If the backing store cannot query identities directly, maintain a dedicated projection identity index through the R11-A1 tracker boundary.
- Do not use actor timers for this product behavior. The design calls for an `IHostedService` poller, and Dapr actor timers are not retained after actor deactivation. Actor reminders are persistent but would add a different scheduler/control-plane contract than this story needs.
- Do not make SignalR the source of projection truth. Polling updates projection state; SignalR remains an invalidation signal that causes clients to re-query.
- Do not make polling exactly-once. Preserve at-least-once delivery, duplicate tolerance, and stale projection acceptance on failure.
- Do not add a cross-instance distributed mutex for polling in this story. If multiple app instances observe the same tracked identity, rely on checkpoint persistence and idempotent projection writes to absorb duplicate attempts.
- Do not let domain-name casing create separate polling identities or interval buckets. Use the same domain normalization and override lookup semantics as `ProjectionOptions`.

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
- L08 applies to this story's preparation history: the dated party-mode review above is separate from this advanced-elicitation pass, and both traces must remain visible for `bmad-dev-story`.

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

GPT-5

### Debug Log References

- Red-phase focused test run initially blocked by Aspire-held DLL locks after the baseline apphost smoke; apphost was stopped and tests were rerun.
- Focused projection suite: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --no-build --filter "FullyQualifiedName~ProjectionUpdateOrchestratorRefreshIntervalTests|FullyQualifiedName~ProjectionPollerServiceTests|FullyQualifiedName~ProjectionCheckpointTrackerTests|FullyQualifiedName~ProjectionDiscoveryHostedServiceTests" -p:NuGetAudit=false` => 34/34 passed.
- Broader projection suite: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --no-build --filter "FullyQualifiedName~Projections|FullyQualifiedName~EventReplayProjectionActorTests" -p:NuGetAudit=false` => 80/80 passed.
- Full server suite: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --no-build -p:NuGetAudit=false` => 1668/1668 passed.
- Tier 1 units: Client 334/334, Contracts 281/281, Sample 63/63, Testing 78/78, SignalR 32/32 passed.
- Closure grep: `rg -n "PollingModeDeferred|Background poller not yet implemented|will NOT update automatically" src tests docs` => no matches.
- Post-change Aspire smoke: `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`; Aspire MCP resource inspection showed app resources, Dapr sidecars, statestore, and pubsub running healthy. Apphost stopped after inspection.

### Completion Notes List

- Implemented active polling semantics for `RefreshIntervalMs > 0`: event publication now registers tracked projection work instead of invoking `/project` immediately.
- Added `ProjectionPollerService` with testable tick source, per-domain due scheduling, same-process non-overlap guard, fail-open retry behavior, and graceful shutdown.
- Extended the R11-A1 checkpoint tracker boundary to track and enumerate canonical `{TenantId, Domain, AggregateId}` polling identities through bounded pages; no parallel registry or actor state-key scan was added.
- Refactored projection delivery so immediate mode and polling mode share the same domain resolution, aggregate event read, `/project`, projection actor write, checkpoint save, ETag/SignalR path, and failure logging.
- Replaced deferred polling logs/docs with active polling semantics, including interval-delayed freshness and at-least-once duplicate-delivery expectations.

### File List

- `_bmad-output/implementation-artifacts/post-epic-11-r11a2-polling-mode-product-behavior.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md`
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/IProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore.Server/Projections/NoOpProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionDiscoveryHostedService.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionPollerServiceTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs`

### Change Log

- 2026-05-01: Implemented polling-mode product behavior, added focused tests and documentation, validated server/Tier 1 suites and Aspire smoke, moved story to review.

## Party-Mode Review

- Date/time: 2026-05-01T10:52:28+02:00
- Selected story key: `post-epic-11-r11a2-polling-mode-product-behavior`
- Command/skill invocation used: `/bmad-party-mode post-epic-11-r11a2-polling-mode-product-behavior; review;`
- Participating BMAD agents: Bob (Scrum Master), Winston (Architect), Amelia (Developer Agent), Murat (Master Test Architect), Paige (Technical Writer), Sally (UX Designer)
- Findings summary:
  - Bob: The story was ready-for-dev, but polling-mode registration needed explicit fail-open semantics so command processing cannot become hostage to tracker writes.
  - Winston: The poller must not call an entry point that immediately returns on `RefreshIntervalMs > 0`; the shared delivery path needs a bypass only for the immediate-mode deferral guard.
  - Amelia: Task sequencing needed to tell the developer where to extract the shared delivery collaborator and how to avoid a second identity registry.
  - Murat: Tests needed explicit coverage for registration failure retry and per-domain interval scheduling, not only no-overlap and cancellation.
  - Paige: Operator documentation remained acceptable but needed the tracked-identity enumeration warning to avoid a hidden state-store scan design.
  - Sally: No direct UI accessibility/localization work is introduced; adopter-experience risk is primarily stale-read expectations, interval delay, and clear logs/docs.
- Changes applied:
  - Clarified AC #3 registration fail-open behavior and retry expectations.
  - Clarified AC #4 so polling bypasses only the interval deferral guard while reusing the projection delivery semantics.
  - Strengthened AC #6 and Task 4 with per-domain interval scheduling expectations.
  - Added Task 1 and implementation guardrails against unbounded tracked-identity scans.
  - Expanded Task 6 with registration-failure retry and per-domain interval tests.
- Findings deferred:
  - No product-scope or architecture-policy decisions deferred. The exact timer abstraction and checkpoint tracker API shape remain implementation details for `bmad-dev-story`, constrained by these clarified acceptance criteria.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-01T13:20:52+02:00
- Selected story key: `post-epic-11-r11a2-polling-mode-product-behavior`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-11-r11a2-polling-mode-product-behavior`
- Batch 1 method names:
  - Self-Consistency Validation
  - Pre-mortem Analysis
  - Failure Mode Analysis
  - Architecture Decision Records
  - Comparative Analysis Matrix
- Reshuffled Batch 2 method names:
  - Red Team vs Blue Team
  - Occam's Razor Application
  - 5 Whys Deep Dive
  - Graph of Thoughts
  - Reverse Engineering
- Findings summary:
  - Self-consistency and pre-mortem review found that idempotent registration could be misread as "do nothing when already known"; AC #3 and Task 3 now require repeated publication to keep an identity eligible for later polling.
  - Failure-mode and architecture review found a hidden distributed-lock decision inside "non-overlapping"; AC #7 and guardrails now constrain non-overlap to the same process and keep cross-instance duplicates under the existing at-least-once/checkpoint contract.
  - Test-design review found the old deferred behavior needed a closure gate, not only new poller tests; AC #13 and Task 6 now require evidence that `PollingModeDeferred` no longer represents configured polling behavior.
  - Red-team and graph review found domain casing and R11-A1 API assumptions as implementation traps; guardrails now require consistent domain normalization and Task 1 requires compiling against the actual landed tracker API.
  - Reverse-engineering review confirmed that the existing product scope is sufficient; no scheduler package, distributed mutex, public DTO change, or UI smoke requirement should be added to close this story.
- Changes applied:
  - Strengthened AC #3 and Task 3 for repeated-publication dirty-work visibility.
  - Clarified AC #7 and implementation guardrails for same-process non-overlap versus multi-instance at-least-once duplicates.
  - Added AC #13 and Task 6 validation evidence for the former `PollingModeDeferred` skip path.
  - Added Task 1 protection against assuming R11-A1 tracker method names before compiling.
  - Added guardrails for domain-name normalization and preserved L08 trace separation.
- Findings deferred:
  - No product-scope or architecture-policy decisions deferred. The exact tracker API extension, timer abstraction, and bounded concurrency shape remain implementation decisions for `bmad-dev-story` inside the clarified constraints.
- Final recommendation: ready-for-dev
