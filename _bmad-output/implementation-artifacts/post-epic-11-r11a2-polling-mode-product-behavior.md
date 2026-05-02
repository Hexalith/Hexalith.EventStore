# Post-Epic-11 R11-A2: Polling-Mode Product Behavior

Status: done

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

### Review Findings

Triple-layer code review (Blind Hunter / Edge Case Hunter / Acceptance Auditor) on diff `e2315a9..HEAD` covering polling-mode implementation. 18 unique findings after dedup: 3 decision-needed, 15 patch candidates, 3 deferred, 5 dismissed.

**Decision-needed (3) — resolved 2026-05-02:**

- [x] [Review][Decision] D1 — Tracker enumeration cost per tick — **Resolved: defer to scaling story.** The current cost is acceptable for the product scope (no perf SLA in AC); enumeration runs at the smallest configured interval. Recorded as R11A2-DF4 in `deferred-work.md`. To revisit if a real-world deployment exceeds ~1k tracked identities or pairs sub-second polling with multi-domain scopes.
- [x] [Review][Decision] D2 — Polling-registration permanent loss — **Resolved: accept + pinned by test.** AC #3 explicitly states "Registration failure is logged and swallowed... a later event for the same identity may retry registration." Current behavior is spec-compliant: command processing remains fail-open, and the next event for the same aggregate retries `TrackIdentityAsync`. **Round 2 update (R2D1 closure):** The "retry after prior failure" pinning test now exists at `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs::TrackIdentityAsync_RetryAfterPriorFailure_CompletesRegistrationOnSecondCall`. It proves a previously-failed `TrackIdentityAsync` (transient `InvalidOperationException` propagated through the bounded ETag retry loop) leaves no orphan in-memory state, and a subsequent call with the same identity completes scope+identity registration end-to-end. To enable the test, the previously-`private sealed record` persistence types (`ProjectionIdentityIndex`, `ProjectionIdentityScope`, `ProjectionIdentityScopePage`, `ProjectionIdentity`, `ProjectionIdentityPage`) were widened to `internal sealed record` so the test assembly (already covered by `InternalsVisibleTo`) can construct/inspect them — they remain hidden from the public API surface.
- [x] [Review][Decision] D3 — Dead `ReadLastDeliveredSequenceAsync` API — **Resolved: keep with `<remarks>` annotation.** `[Obsolete]` would cascade build failures across server tests under `TreatWarningsAsErrors`. Documentation note added at `IProjectionCheckpointTracker.cs:9-19` warns against introducing new callers without revisiting the R11-A1b full-replay decision. Stronger removal/Obsolete deferred to a future cleanup story when forward-compat intent is confirmed.

**Patch — applied (8 of 15):**

- [x] [Review][Patch] P1 — Race on `_nextDueByDomain` — **Applied.** Changed to `ConcurrentDictionary<string, DateTimeOffset>` with `GetOrAdd` in `IsDomainDue` and indexer-set in the post-loop schedule advancement [`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:24,69-72,91-101`]
- [x] [Review][Patch] P2 — Per-tick starvation — **Applied.** When `MaxIdentitiesPerTick` cap fires, `tickLimited=true` and the post-loop schedule advancement is skipped entirely. The next tick re-discovers the same domains and continues processing the unprocessed tail; duplicate delivery is absorbed by the at-least-once checkpoint contract [`ProjectionPollerService.cs:36,44-46,68-77`]
- [x] [Review][Patch] P3 — `EnumerateTrackedIdentitiesAsync` exception aborts whole tick — **Applied.** Wrapped the `await foreach` in try/catch (OCE rethrows; other exceptions logged via new `EnumerationFailed` event id 1134) so a transient tracker failure does not skip the post-loop schedule advancement [`ProjectionPollerService.cs:38-67,79-82,143-147`]
- [x] [Review][Patch] P6 — `DeliverProjectionAsync` interface footgun — **Applied.** Added `<remarks>` warning that this is an internal seam for `ProjectionPollerService` only and `[EditorBrowsable(EditorBrowsableState.Never)]` so IntelliSense hides the method from immediate-mode callers [`src/Hexalith.EventStore.Server/Projections/IProjectionUpdateOrchestrator.cs:18-37`]
- [x] [Review][Patch] P7 — Silent disable when `DaprClient` missing — **Applied.** DI registration logs a startup `LogWarning` ("DaprClient is not registered; projection polling is disabled... Stage=ProjectionPollerDisabled") before substituting the no-op hosted service, so operators detect the misconfiguration even if discovery later logs "polling mode active" [`src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:74-83`]
- [x] [Review][Patch] P10 — `PeriodicTimer` cosplay — **Applied.** Replaced the disposable-after-first-tick `PeriodicTimer` with `Task.Delay`, which matches the actual semantics (per-call interval, no cross-tick cadence). Added an interval clamp (≤0 → 60s default) and OCE→`false` translation so the existing tick-source contract is preserved. XML doc records the at-least-once drift acceptance [`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:158-179`]
- [x] [Review][Patch] P11 — `TenantId`/`Domain` validation — **Applied.** Added `ArgumentException.ThrowIfNullOrWhiteSpace` for `TenantId`, `Domain`, `AggregateId`, plus an explicit check that `TenantId` and `Domain` do not contain `':'` (key separator) to prevent cross-scope key collisions and namespace injection. Validation throws inside `TrackIdentityAsync`, which the orchestrator already wraps in a fail-open catch [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:128-145`]
- [x] [Review][Patch] P12 — `AggregateId` non-empty validation — **Applied** as part of P11 [`ProjectionCheckpointTracker.cs:131`]
- [x] [Review][Patch] P15 — `PeriodicTimer` interval ≤0 guard — **Applied** as part of P10 (clamp inside `PeriodicProjectionPollerTickSource.WaitForNextTickAsync`) [`ProjectionPollerService.cs:172-174`]

**Patch — final carryover applied (6 of 15):**

- [x] [Review][Patch] P4 — `KeyedSemaphore` robustness gaps — **Applied.** `AcquireRefAsync` now checks cancellation inside the retry loop, yields with `Task.Yield()` after the spin threshold, uses CAS reference increments to prevent `int.MaxValue` overflow wrapping negative, and `Releaser.Dispose` defensively handles `ObjectDisposedException` on release races [`src/Hexalith.EventStore.Server/Projections/KeyedSemaphore.cs`]
- [x] [Review][Patch] P5 — Identity registration safety — **Applied.** Tracker scope/identity page scans now use ETag-guarded reads, duplicate target-page detection recovers orphan page/index writes after page-save/index-save interruption, and retry exhaustion throws so `ProjectionUpdateOrchestrator` logs registration failure instead of a false registered event [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`]
- [x] [Review][Patch] P8 — Tests use `TimeProvider.System` instead of `FakeTimeProvider` — **Applied.** Added `Microsoft.Extensions.TimeProvider.Testing` and switched poller tests to `FakeTimeProvider` [`Directory.Packages.props`, `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj`, `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionPollerServiceTests.cs`]
- [x] [Review][Patch] P9 — `_activeIdentities` cleanup theoretical race — **Closed by inspection.** There is still no async boundary between `_activeIdentities.TryAdd` and the guarded `try/finally`; the existing cleanup path remains covered by no-overlap/failure-retry poller tests and no code change was required [`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs`]
- [x] [Review][Patch] P13 — Domain-casing normalization mismatch — **Closed by pinning upstream guarantee.** Added tracker coverage proving mixed-case `AggregateIdentity` input persists canonical lowercase tenant/domain identity records, matching the contract-level normalization boundary [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs`]
- [x] [Review][Patch] P14 — Missing tests required by AC #11 / Task 6 — **Applied.** Added remaining tests for domain-casing persistence, `IdentityPageSize=100` pagination rollover, and operator log assertions for `PollingWorkRegistered`, `IdentityDeliveryFailed`, `TickLimitReached`, and `EnumerationFailed`; retry-after-failure remained covered by Round 2 [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs`, `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionPollerServiceTests.cs`, `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs`]

**Deferred (3) — pre-existing or out-of-scope:**

- [x] [Review][Defer] W1 — `ProjectionUpdateOrchestrator.s_projectionLocks` static field shared across xUnit parallel tests [`ProjectionUpdateOrchestrator.cs:41`] — deferred, R11-A1b legacy cleanup, see deferred-work.md R11A1-ReRe-DF16
- [x] [Review][Defer] W2 — `PollOnceAsync_SameIdentityAlreadyRunning_SkipsOverlap` synchronization is timing-dependent — deferred, passes today
- [x] [Review][Defer] W3 — `ProjectionPollerService.ExecuteAsync` does not subscribe to `IOptionsMonitor.OnChange` — interval changes only take effect on next tick boundary — deferred, scope creep

**Dismissed (5):** Events.Length order coupling (sequence-ordered by source); async enumerator dispose (compiler-handled); polling-mode OCE filter (already covered); unlock-duration metric finally (observability nicety, out of scope); DaprClient post-startup loss (fail-open absorbs).

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
- 2026-05-02 baseline Aspire attempt before final hardening initially failed during apphost build with stale `CS0009` metadata/reference errors; after focused builds/tests, `dotnet build Hexalith.EventStore.slnx -p:NuGetAudit=false` succeeded with 0 warnings/0 errors and a second Aspire smoke reported healthy running resources.
- Red-phase final carryover run: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~ProjectionCheckpointTrackerTests|FullyQualifiedName~ProjectionPollerServiceTests|FullyQualifiedName~ProjectionUpdateOrchestratorRefreshIntervalTests" -p:NuGetAudit=false` failed as expected on tracker retry-exhaustion and pagination/page-read assertions before implementation.
- Focused final carryover suite: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~ProjectionCheckpointTrackerTests|FullyQualifiedName~ProjectionPollerServiceTests|FullyQualifiedName~ProjectionUpdateOrchestratorRefreshIntervalTests|FullyQualifiedName~KeyedSemaphoreTests" -p:NuGetAudit=false` => 43/43 passed.
- Broader projection suite: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~Projections|FullyQualifiedName~EventReplayProjectionActorTests" -p:NuGetAudit=false` => 100/100 passed.
- Full server suite: `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj -p:NuGetAudit=false` => 1693/1693 passed.
- Tier 1 units: Client 334/334, Contracts 281/281, Sample 63/63, Testing 78/78 passed.
- Solution build: `dotnet build Hexalith.EventStore.slnx -p:NuGetAudit=false` => succeeded, 0 warnings, 0 errors.
- Closure grep: `rg -n "PollingModeDeferred|Background poller not yet implemented|will NOT update automatically" src tests docs` => no matches.
- P8 audit: `rg -n "TimeProvider\.System" tests\Hexalith.EventStore.Server.Tests\Projections\ProjectionPollerServiceTests.cs` => no matches.
- P14 audit: `rg -n "TrackIdentityAsync_(MixedCaseInput|FullIdentityPage|ExistingScopeScan|IndexRetryExhausted)|PollingRegistration_LogsOperatorEvent|DeliveryFailure_LogsOperatorEvent|TickLimitReached_LogsOperatorEvent|EnumerationFailure_LogsOperatorEvent" tests\Hexalith.EventStore.Server.Tests\Projections` => all expected tests present.

### Completion Notes List

- Implemented active polling semantics for `RefreshIntervalMs > 0`: event publication now registers tracked projection work instead of invoking `/project` immediately.
- Added `ProjectionPollerService` with testable tick source, per-domain due scheduling, same-process non-overlap guard, fail-open retry behavior, and graceful shutdown.
- Extended the R11-A1 checkpoint tracker boundary to track and enumerate canonical `{TenantId, Domain, AggregateId}` polling identities through bounded pages; no parallel registry or actor state-key scan was added.
- Refactored projection delivery so immediate mode and polling mode share the same domain resolution, aggregate event read, `/project`, projection actor write, checkpoint save, ETag/SignalR path, and failure logging.
- Replaced deferred polling logs/docs with active polling semantics, including interval-delayed freshness and at-least-once duplicate-delivery expectations.
- Closed final review carryover: hardened `KeyedSemaphore`, made projection identity registration page reads ETag-guarded, surfaced registration retry exhaustion as fail-open orchestrator warnings, migrated poller tests to `FakeTimeProvider`, and added missing casing/pagination/operator-log tests.

### File List

- `_bmad-output/implementation-artifacts/post-epic-11-r11a2-polling-mode-product-behavior.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `Directory.Packages.props`
- `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md`
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/IProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore.Server/Projections/KeyedSemaphore.cs`
- `src/Hexalith.EventStore.Server/Projections/NoOpProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionDiscoveryHostedService.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionPollerServiceTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs`

### Change Log

- 2026-05-01: Implemented polling-mode product behavior, added focused tests and documentation, validated server/Tier 1 suites and Aspire smoke, moved story to review.
- 2026-05-02: Addressed final review carryover P4/P5/P8/P9/P13/P14, added missing hardening tests, revalidated focused/server/Tier 1/solution build/Aspire smoke, moved story to review.

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

### Review Findings — Round 2 (post-`a61a87a` hardening review, 2026-05-02)

Triple-layer code review (Blind Hunter / Edge Case Hunter / Acceptance Auditor) on diff `a61a87a~1..a61a87a` (the applied-patches commit landing P1, P2, P3, P6, P7, P10, P11, P12, P15 and resolution annotations for D2/D3). 14 unique findings after dedup of 34 raw (12 Blind + 8 Auditor + 14 Edge): 1 decision-needed, 10 patch candidates, 3 deferred, 7 dismissed.

**Decision-needed (1) — resolved 2026-05-02:**

- [x] [Review][Decision] R2D1 — D2 closure circular vs. land P14 test in this branch — **Resolved: option (c) — landed the missing pinning test in this branch.** Added `TrackIdentityAsync_RetryAfterPriorFailure_CompletesRegistrationOnSecondCall` to `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs`. The test injects a transient `InvalidOperationException` into the scope-index `GetStateAndETagAsync` path so the bounded ETag retry loop exhausts and propagates on the first `TrackIdentityAsync` call, then flips the failure flag and verifies the second call completes registration end-to-end (both `ProjectionIdentityScopePage` and `ProjectionIdentityPage` are saved with the test identity). To enable the test the persisted-schema records were widened from `private sealed record` to `internal sealed record` (they remain hidden from the public API surface and were already test-assembly-visible via `InternalsVisibleTo`). 16/16 tracker tests + 92/92 broader projection tests pass. Original D2 (line 108) updated to reference the new pinning test; P14 sub-bullet (b) checked off; sub-bullets (a, c, d) remain open.

**Patch — candidates (10):**

- [x] [Review][Patch] R2P1 — Per-tick cap + skip-advancement + restart-from-zero causes permanent tail starvation (`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:36, 48-52, 87-89`) — All 3 reviewers converge: when `processed >= MaxIdentitiesPerTick=100`, `tickLimited=true` skips `_nextDueByDomain` advancement *and* the next `EnumerateTrackedIdentitiesAsync` call restarts from offset 0. With >100 tracked identities under one interval, ranks 101+ are never reached. The in-code comment claims "next tick re-discovers same domains and continues from where this tick stopped" — the code does not continue; it restarts. Suggested fix: track per-domain processed-vs-enumerated count and advance domains whose entire identity set was processed in this tick, leaving only the truncated domain pinned for re-enumeration; or persist a resume cursor. Severity: HIGH.
- [x] [Review][Patch] R2P2 — Enumeration-failure path doesn't advance schedule when zero identities yielded → every-tick retry storm (`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:38-78, 87-89`) — The new try/catch (P3) was meant to prevent "transient tracker failure does not skip the schedule advancement", but if `MoveNextAsync` throws before yielding any identity, `dueDomains` is empty and the post-loop foreach is a no-op. `_nextDueByDomain` stays seeded at original `now`, so `IsDomainDue` returns true on every minimum-interval tick — the original failure mode P3 was supposed to fix. Suggested fix: track `enumerationFailed` flag distinct from `tickLimited`; on enumeration failure, advance `_nextDueByDomain` for all configured polling domains (or apply a backoff clamp) instead of relying on `dueDomains`. Severity: HIGH.
- [x] [Review][Patch] R2P3 — `WaitForNextTickAsync` swallows `OperationCanceledException` and returns `false`, conflating shutdown with "no more ticks" (`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:185-191`) — AC #8 states the hosted service "does not swallow `OperationCanceledException` as an error". Current code catches OCE without a token-source filter, so any ambient cancellation (e.g. linked CTS timeout) is coerced to the "stop ticking" sentinel that `ExecuteAsync` treats as terminal. Suggested fix: `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }` and let other OCEs propagate. Severity: HIGH.
- [x] [Review][Patch] R2P4 — Inner & outer `catch (OperationCanceledException) { throw; }` blindly rethrow any OCE — non-stopping-token cancellation tears down `BackgroundService` permanently (`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:64-66, 75-76`) — Latent today (`ExecuteAsync` only passes `stoppingToken`), but the seam is callable from tests/future code with arbitrary tokens. A linked-CTS timeout from a future caller would propagate through both rethrow points and exit the BackgroundService for the lifetime of the process. Suggested fix: filter `when (cancellationToken.IsCancellationRequested)` on both rethrows; otherwise log and continue. Severity: HIGH.
- [x] [Review][Patch] R2P5 — `[EditorBrowsable(Never)]` on `IProjectionUpdateOrchestrator.DeliverProjectionAsync` is IntelliSense theater, not enforcement (`src/Hexalith.EventStore.Server/Projections/IProjectionUpdateOrchestrator.cs:35`) — All 3 reviewers converge: same-assembly callers (notably `EventPublisher`, which already takes `IProjectionUpdateOrchestrator`) can still bind to the method and compile under `TreatWarningsAsErrors`. The `<remarks>` block is documentation; the attribute affects external IDE display only. Suggested fix: split the seam onto an internal-only interface (`IProjectionPollerDeliveryGateway`) implemented by the same class, or add a `BannedSymbols.txt` analyzer rule pinning the method to `ProjectionPollerService`. Severity: MEDIUM.
- [x] [Review][Patch] R2P6 — `':'` rejection in `TrackIdentityAsync` is incomplete and partially redundant (`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:128-145`) — Three concerns merged: (a) `AggregateId` is part of `identity.ActorId` used as `_activeIdentities` key but is not `':'`-checked; (b) other reserved chars used by the actor-key scheme (`|`, `.`, control chars) are not blocked; (c) the check is largely redundant with `AggregateIdentity` regex `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`, which already rejects `:` for tenant/domain — the new guard only fires on bypass paths (reflection, deserialization without validator, `with`-clone). Suggested fix: either delegate to a single `AggregateIdentity.AssertValid()` helper that re-runs the regex, or extend the guard to a denylist `[':', '\0', '|', '.']` covering all separator hazards including `AggregateId`. Severity: MEDIUM.
- [x] [Review][Patch] R2P7 — `_nextDueByDomain` post-loop indexer write is last-writer-wins under concurrent ticks (`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:91-101`) — The P1 `ConcurrentDictionary` upgrade made `GetOrAdd` atomic but did not fix the schedule-advancement write at `:94`. If two ticks (A at `t=0`, B at `t=500ms`) both reach the post-loop foreach, A's `nextDue=t0+1000` may overwrite B's `nextDue=t0+1500`, regressing the schedule. The existing `PollOnceAsync_SameIdentityAlreadyRunning_SkipsOverlap` test already runs two concurrently, so this is reachable. Suggested fix: `AddOrUpdate(domain, candidate, (_, existing) => existing >= candidate ? existing : candidate)`. Severity: MEDIUM.
- [x] [Review][Patch] R2P8 — Interval clamp `≤0 → 60s` silently masks misconfig with no log/warn (`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:185-187`) — P15 deliberately clamps to avoid `PeriodicTimer` throwing, but the previous behavior surfaced misconfig loudly. Suggested fix: log a warning (one-shot per process or one-shot per distinct bad value) before coercing; use existing source-generated logger style (`Stage=ProjectionPollingIntervalClamped`). Severity: MEDIUM.
- [x] [Review][Patch] R2P9 — P7 disable-warning uses ad-hoc `LogWarning` extension instead of source-generated `[LoggerMessage]` (`src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:74-79`) — Spec project structure note (line 195) requires "existing logging source-generator style for new structured logs". The current message embeds `Stage=ProjectionPollerDisabled` as a string-interpolated tag inside the message body — greppable by text but not surfaced as a structured `Stage` property the way `ProjectionPollerService.Log` entries are, so Grafana/Seq filters by structured field will miss it. Suggested fix: add a `[LoggerMessage]` entry in a partial logger class colocated with the registration extension. Severity: MEDIUM.
- [x] [Review][Patch] R2P10 — DI factory `log?.LogWarning` silently disappears if `ILogger<>` is not registered (`src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:76-79`) — The whole point of P7 is to alert operators that polling is disabled; if logging happens to be misconfigured the alert vanishes. Suggested fix: resolve via `GetRequiredService<ILoggerFactory>()`, or fall back to `Console.Error.WriteLine` when logging is unavailable so the warning cannot be silenced. Severity: LOW.

**Patch — minor (3, batched):**

- [x] [Review][Patch] R2P11 — `IProjectionPollerTickSource` contract for `false` return is undocumented (`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:168-170`) — `ExecuteAsync` treats any `false` as terminal. A future tick-source impl (e.g. one that wraps `PeriodicTimer`, which returns `false` when disposed) could shut down the poller without a stop request. Suggested fix: add interface XML doc stating `false` MUST mean "host shutdown requested"; or make `ExecuteAsync` re-check `stoppingToken.IsCancellationRequested` before returning. Severity: LOW.
- [x] [Review][Patch] R2P12 — `Task.Delay(TimeSpan.MaxValue)` would throw `ArgumentOutOfRangeException`; only `≤0` is clamped (`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:185-187`) — `PeriodicProjectionPollerTickSource` is `public sealed` and directly callable; no upper-bound clamp. Production path clamps via `GetSmallestPositiveInterval` so this is latent. Suggested fix: clamp to `TimeSpan.FromMilliseconds(uint.MaxValue - 1)` or `TimeSpan.FromHours(24)`. Severity: LOW.
- [x] [Review][Patch] R2P13 — Tick-source docstring incomplete on `default` token semantics (`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:171-178`) — Docstring claims "cancellation returns `false`"; a `default(CancellationToken)` cannot cancel, so the call always waits the full interval. Suggested fix: append "If the supplied token is `default`/none, the call always waits the full interval and returns `true`." Severity: LOW.

**Deferred (3, all pre-existing or out-of-scope):**

- [x] [Review][Defer] R2W1 — `processed++` placement inside `_activeIdentities.TryAdd` guard means already-running identities don't count toward `MaxIdentitiesPerTick` (`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:48-52`) — Not introduced by this diff; recorded as `R11A2-Round2-DF1` in `deferred-work.md`. Pre-existing fairness oddity; can iterate past the cap's intent under heavy overlap. Revisit when the cap rule is reformulated.
- [x] [Review][Defer] R2W2 — `EventId 1121` collision between `ProjectionUpdateOrchestrator` and `ProjectionDiscoveryHostedService` for unrelated logs — Pre-existing collision in the 11xx range, not caused by this diff. New `EventId=1134` is unique. Recorded as `R11A2-Round2-DF2`; project-wide audit needed for the EventId numbering scheme.
- [x] [Review][Defer] R2W3 — `await foreach` dispose-time exception masking — If a future tracker's `IAsyncEnumerator.DisposeAsync` throws (no current trackers do), the dispose exception would replace an inner OCE and be logged as `EnumerationFailed`. Recorded as `R11A2-Round2-DF3`. Edge case; not reachable with the current `EnumerateTrackedIdentitiesAsync` implementation.

**Dismissed (7):** `dueDomains` HashSet concurrency (local variable, not shared across ticks); empty-tenant `ThrowIfNullOrWhiteSpace` regression (already enforced by `AggregateIdentity` regex); `NoOpHostedService.Instance` cross-host singleton (DI registration runs once); `GetOrAdd(domain, now)` eager `now` evaluation (trivial perf, deliberate); `':'` Ordinal vs `OrdinalIgnoreCase` casing asymmetry (benign — `:` is not casing-sensitive); `_activeIdentities` cleanup `ThreadAbortException` race (not relevant on .NET 10); D3 `<remarks>` lacking analyzer guard (spec deliberately accepted annotation as sufficient under `TreatWarningsAsErrors` constraint).

**AC verdict matrix (Acceptance Auditor) — updated 2026-05-02 after Round 2 patches landed:**

| AC | Pre-patch verdict | Post-patch verdict | Evidence |
|----|---------|---------|----------|
| #2 (registered only when enabled) | PARTIAL | PASS | R2P9 (source-gen `ProjectionPollerDisabledLog.PollerDisabled` event 1140), R2P10 (`ILoggerFactory` resolved + `Console.Error` fallback) |
| #3 (discoverable in polling) | PARTIAL | PASS | R2P6 (`AssertNoReservedChars` covers all three identity components, denylist `:`, `\0`, `\|`, `\r`, `\n`); R2D1 retry-after-failure pinning test landed |
| #4 (reuses orchestrator, bypasses only deferral guard) | PARTIAL | PASS | R2P5 — `IProjectionPollerDeliveryGateway` is a separate contract; immediate-mode callers cannot reach `DeliverProjectionAsync` via `IProjectionUpdateOrchestrator` |
| #6 (per-domain intervals respected) | PARTIAL | PASS | R2P1 (truncated-domain skip with full-coverage advancement), R2P7 (`AddOrUpdate` preserves freshest schedule) |
| #7 (bounded, non-overlapping) | PASS | PASS | `ConcurrentDictionary` swap correct; cap-skip + freshness-preserving `AddOrUpdate` |
| #8 (graceful shutdown) | FAIL | PASS | R2P3 (`when (cancellationToken.IsCancellationRequested)` filter on tick-source OCE), R2P4 (same filter on inner+outer poll-loop rethrows) |
| #9 (fail-open) | PARTIAL | PASS | R2P2 (`AdvanceKnownPollingDomains` on enumeration failure prevents retry-storm); existing per-identity log-and-continue preserved |
| #10 (config/logs no longer overpromise) | PASS | PASS | New event ids 1134/1135/1136/1140 all source-generated; no ad-hoc strings |
| #11 (tests pin contract) | FAIL | PARTIAL | R2D1 closure test landed (`TrackIdentityAsync_RetryAfterPriorFailure_CompletesRegistrationOnSecondCall`). Round-1 P14 sub-bullets (a, c, d) remain — domain-casing dedup, pagination boundary, log-message assertions still owed |
| #13 (closure evidence) | PARTIAL | PARTIAL | Spec narrative + 1685/1685 server tests + full-solution build green; broader validation grep still owed when P14 (a, c, d) lands |

### Round 2 closure summary (2026-05-02)

- **Decisions resolved:** R2D1 (test landed in branch).
- **Patches applied:** R2P1–R2P13 (all 13). Files touched: `src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs`, `IProjectionUpdateOrchestrator.cs` (interface split), `ProjectionUpdateOrchestrator.cs`, `NoOpProjectionUpdateOrchestrator.cs`, `ProjectionCheckpointTracker.cs` (validation widened + records made `internal`); `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (DI seam registration + source-gen disable warning); `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs` (new pinning test); `tests/.../ProjectionPollerServiceTests.cs` (substitute updated for split interface).
- **Deferred:** R2W1, R2W2, R2W3 — recorded in `deferred-work.md` under Round 2 heading.
- **Test evidence:** 16/16 tracker tests, 92/92 broader projection tests, 1685/1685 server tests, full-solution Release build with `TreatWarningsAsErrors` — all green.
- **Round 1 carryover now closed in final pass:** P4 (KeyedSemaphore robustness), P5 (identity registration ETag-race safety), P8 (FakeTimeProvider migration), P9 (theoretical `_activeIdentities` race closed by inspection), P13 (domain-casing dedup pinned at tracker), P14 (a, c, d tests) — story is ready for review.

### Final carryover closure summary (2026-05-02)

- **Patches applied:** P4, P5, P8, P13, P14; P9 closed by inspection because no async boundary exists between active-identity registration and cleanup `try/finally`.
- **Files touched in final pass:** `Directory.Packages.props`; `src/Hexalith.EventStore.Server/Projections/KeyedSemaphore.cs`; `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`; `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj`; `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs`; `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionPollerServiceTests.cs`; `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs`.
- **Test evidence:** focused final carryover 43/43, broader projection 100/100, full server 1693/1693, Tier 1 units Client 334/334 + Contracts 281/281 + Sample 63/63 + Testing 78/78, solution build 0 warnings/0 errors, post-change Aspire resources healthy.

### Review Findings — Round 3 (final carryover review of `c840f6e`, 2026-05-02)

Triple-layer code review (Blind Hunter / Edge Case Hunter / Acceptance Auditor) on commit `c840f6e` covering the final carryover hardening (P4 KeyedSemaphore, P5 tracker ETag-guarded reads + orphan recovery + retry-exhaustion-throws, P8 FakeTimeProvider migration, P13/P14 a/c/d tests). Acceptance Auditor: **all 10 audited items PASS** (no AC regression). 13 unique patch findings after dedup of 25 raw, 4 deferred, 7 dismissed.

**Patch — candidates (13):**

- [x] [Review][Patch] R3P1 (HIGH) — `TrackIdentityAsync` enters a permanent throw loop when a previous attempt filled a page to *exactly* `IdentityPageSize` then crashed before saving the index [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:265-290, 353-378`] — Pre-loop scan finds nothing, `targetPageNumber = PageCount-1`, page read returns 100 entries, the new identity is not among them so the `existingIdentities.Any(==identity)` orphan-recovery branch is skipped, the `existingIdentities.Length >= IdentityPageSize` branch hits `continue`, state did not change, retry exhausts. New `InvalidOperationException` propagates and the orchestrator logs `PollingWorkRegistrationFailed` for every new aggregate in the affected scope. Fix: when `existingIdentities.Length >= IdentityPageSize` AND `targetPageNumber == index.PageCount - 1`, save a corrected `index with { LastPageCount = existingIdentities.Length }` before the `continue`. Pin with a unit test priming index `(1, 99)` while the page returns 100 entries and asserting registration of a new identity does NOT throw.
- [x] [Review][Patch] R3P2 (MEDIUM) — `Releaser.Dispose` swallows `ObjectDisposedException` after `Semaphore.Release()` but before `owner.ReleaseRef`, leaking the holder permanently [`src/Hexalith.EventStore.Server/Projections/KeyedSemaphore.cs:107-114`] — Both reviewers converge. The diff's own comment says "should not be reachable", yet placing both calls in the same `try` means a future regression that disposes the semaphore between Acquire and Dispose silently skips refcount cleanup. Fix: move `owner.ReleaseRef(key, holder)` into a `finally` block (or invert the ordering so `ReleaseRef` runs first).
- [x] [Review][Patch] R3P3 (MEDIUM) — Orphan-recovery branch can leave `LastPageCount > IdentityPageSize` under concurrent writers [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:274-286, 362-374`] — When orphan recovery saves `recoveredIndex` with `LastPageCount = existingIdentities.Length` and the save returns false (etag conflict), the next retry re-reads the page; if a concurrent writer added another identity in the interim, `existingIdentities.Length` is now larger than the original orphan size and the post-loop write can produce a `LastPageCount` that exceeds `IdentityPageSize`, breaking the page-rollover predicate. Fix: re-derive `recoveredIndex` from a fresh page read on each retry attempt OR clamp `recoveredIndex.LastPageCount` to `IdentityPageSize`.
- [x] [Review][Patch] R3P4 (MEDIUM) — `TrackIdentityAsync_MixedCaseInput_PersistsCanonicalLowercaseScopeAndIdentity` only validates `AggregateIdentity` ctor normalization, not any tracker-layer guarantee [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs:537-609`] — Mixed-case input is normalized inside `AggregateIdentity` BEFORE reaching the tracker, so this test passes regardless of tracker behavior. The P13 closure narrative ("domain-casing dedup at the tracker layer") overstates coverage. Fix: rename to `AggregateIdentity_NormalizesBeforeTracker_PersistsLowercase` and move into `AggregateIdentityTests`, OR add a second tracker-internal test that constructs `ProjectionIdentityScope`/`ProjectionIdentity` directly with mixed case to lock the contract responsibility upstream.
- [x] [Review][Patch] R3P5 (MEDIUM) — `AssertNoReservedChars` only runs inside `TrackIdentityAsync`; `ReadLastDeliveredSequenceAsync` and `SaveDeliveredSequenceAsync` accept the same identity components without the new colon/null guard [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:27-49,53-125,142-144`] — `GetStateKey(identity)` builds `StateKeyPrefix + identity.ActorId` for read/save paths. If a caller bypasses `AggregateIdentity` validation (reflection, custom converter), a poisoned identity with `:` in the AggregateId could collide cross-scope. The R2P6 defense-in-depth claim only covers registration. Fix: lift `AssertNoReservedChars` into `GetStateKey` (or a private `Validate(identity)` helper) and call it from all three entry points; add `ArgumentException` pinning tests for each method.
- [x] [Review][Patch] R3P6 (MEDIUM) — `PollOnceAsync_TickLimitReached_LogsOperatorEvent` does not assert the actual delivered count is bounded by the limit [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionPollerServiceTests.cs:144-160`] — Test enqueues 101 identities and asserts `EventId 1133` fired but never asserts that delivery stopped at 100. A silent regression that lowered or raised the limit would still pass as long as it remained below 101. Fix: assert `orchestrator.Received(MaxIdentitiesPerTick).DeliverProjectionAsync(...)` or read the constant from production and assert exact equality.
- [x] [Review][Patch] R3P7 (LOW) — `TrackIdentityAsync_IndexRetryExhausted_ThrowsSoPublisherCanLogFailure` only exercises the scope-index throw site, not the identity-index throw site [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs:426-465`] — Mock setup makes `TrySaveStateAsync<ProjectionIdentityIndex>` always return false, so the scope-index retry exhausts first and the identity-index retry at `ProjectionCheckpointTracker.cs:161` is never reached. A future regression that drops the throw at line 161 but keeps the throw at line 152 would still pass. Fix: split into two tests — one priming the scope-index save to fail and one priming the identity-index save to fail (after scope-index succeeds) — and assert each `InvalidOperationException` message.
- [x] [Review][Patch] R3P8 (LOW) — Missing pinning test for the new "tracker throws → orchestrator logs `PollingWorkRegistrationFailed` and returns normally" contract [`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:50-55`] — The silent-skip → throw contract change at the tracker is only protected by the orchestrator's existing catch-all. No regression test asserts the orchestrator absorbs the throw and emits event 1121. Fix: add a test in `ProjectionUpdateOrchestratorRefreshIntervalTests` substituting a tracker that throws `InvalidOperationException` and asserting (a) `UpdateProjectionAsync` returns without rethrowing, (b) the operator log includes `EventId 1121 / Stage=ProjectionPollingWorkRegistrationFailed`.
- [x] [Review][Patch] R3P9 (LOW) — `FakeTimeProvider` migration is plumbed but never advanced or read in any new test — pure no-op replacement [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionPollerServiceTests.cs:189`] — `CreateService` defaults `timeProvider` to `new FakeTimeProvider(...)` but every test calls `service.PollOnceAsync(DateTimeOffset.UtcNow)` directly with its own real `now`. `PollOnceAsync` doesn't call `timeProvider.GetUtcNow()` — only `ExecuteAsync` does, and no new test exercises `ExecuteAsync`. The new package dependency provides no behavioral guarantee. Fix: either drive at least one test through `ExecuteAsync` with `FakeTimeProvider.Advance(...)` to actually exercise the seam, OR revert to `TimeProvider.System` and remove the `Microsoft.Extensions.TimeProvider.Testing` package addition.
- [x] [Review][Patch] R3P10 (LOW) — First-boot enumeration-failure produces a tight retry storm because `_nextDueByDomain` was never seeded [`src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs:99-102` and test at `ProjectionPollerServiceTests.cs:159-175`] — `AdvanceKnownPollingDomains` iterates `_nextDueByDomain.Keys.ToArray()`. If `EnumerateTrackedIdentitiesAsync` throws before the first yield, `_nextDueByDomain` is empty and the R2P2 backstop is a no-op. Operator sees event 1134 spam at the smallest configured interval until a tick eventually succeeds. Fix: seed `_nextDueByDomain` from `ProjectionOptions.Domains` at startup (before the first `ExecuteAsync` tick) so `AdvanceKnownPollingDomains` has all configured polling domains regardless of tracker yield.
- [x] [Review][Patch] R3P11 (LOW) — Log message and exception message at `TryAddScopeAsync` retry-exhaustion blame "scope" but the identity-path message is ambiguous [`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:149-160`] — Both throws produce identical `InvalidOperationException` types with subtly different message strings; no `Exception.Data` payload to disambiguate scope vs identity programmatically. Operators triaging a registration failure must read both the log line and the exception. Fix: include `(TenantId, Domain, AggregateId)` in `Exception.Data`, OR introduce a dedicated exception type per path.
- [x] [Review][Patch] R3P12 (LOW) — `TrackIdentityAsync_FullIdentityPage_AppendsToNextPageAndUpdatesIndex` does not assert page-0 is left untouched [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs:677-684`] — A future refactor that accidentally appends the new identity to BOTH page 0 and page 1 would still pass. Fix: add a `daprClient.DidNotReceive().TrySaveStateAsync<ProjectionIdentityPage>(\"...:0\", ...)` assertion to lock the existing page is not rewritten during the rollover.
- [x] [Review][Patch] R3P13 (LOW) — `TestLogger<T>` defined twice (byte-identical) in `ProjectionPollerServiceTests.cs:240-250` and `ProjectionUpdateOrchestratorRefreshIntervalTests.cs:264-275` — Future log-capture tests will copy this a third time. Fix: extract to a shared `TestUtilities/TestLogger.cs` helper inside the test project (internal accessibility).

**Deferred (4) — pre-existing or out-of-scope:**

- [x] [Review][Defer] R3W1 — `KeyedSemaphore.AcquireRefAsync` retry has no max-retry bound; vulnerable to livelock if a poisoned holder is never `TryRemove`'d [`KeyedSemaphore.cs:38-58`] — pre-existing pattern, the disposer's CAS + `Task.Yield()` mitigation already in place make this practically unreachable. Recorded for the next concurrency hardening pass.
- [x] [Review][Defer] R3W2 — `PollOnceAsync` re-entrancy claim relies on `ExecuteAsync` serialization; if `PollOnceAsync` becomes a public/manual trigger, two calls could race — pre-existing internal seam. P9 closure-by-inspection remains correct under current call shapes. Revisit if `PollOnceAsync` is exposed via an admin endpoint.
- [x] [Review][Defer] R3W3 — `_holders.GetOrAdd` callback may allocate `Holder { SemaphoreSlim }` multiple times under contention; losing instances are GC'd but waste allocations on hot keys [`KeyedSemaphore.cs:44`] — pre-existing, microbenchmark concern only.
- [x] [Review][Defer] R3W4 — Tests assert hardcoded EventIds (1117, 1131, 1133, 1134) without symbolic constants; brittle to event-id renumbering — defer to a project-wide event-id constants audit covering the 11xx range collision noted in R2W2.

**Dismissed (7):** B1 (`AcquireRefAsync` cancellation suspect — re-checked, `WaitAsync` catch handles it correctly); B2 (`Task.Yield` missing `ConfigureAwait` — `YieldAwaitable` doesn't accept it, and the project is server-only with no SyncContext); B7 (orphan-recovery wrong-key suspect — verified both branches use the correct index key variable); B10 (`FullIdentityPage` rollover suspect — Acceptance Auditor confirmed implementation matches spec); B14 (`Returns(_ => throw ...)` — idiomatic NSubstitute for one-shot throw); B15 (Packages.props alphabetical ordering — cosmetic, no CPM conflict); E12 (overflow throw leaves empty Holder — requires 2³¹ concurrent acquires on the same key, unreachable).

**AC verdict matrix (Acceptance Auditor) — final pass after `c840f6e`:**

| AC | Round 2 verdict | Round 3 verdict | Notes |
|----|----------------|-----------------|-------|
| #1, #4, #7, #8, #9, #10 | PASS | PASS | No regression; `c840f6e` does not modify the relevant code paths |
| #11 (tests pin contract) | PARTIAL | PASS | All Round 1 P14 sub-bullets (a, c, d) landed in `c840f6e` |
| #13 (closure evidence) | PARTIAL | PASS | Closure greps recorded; PollingModeDeferred / TimeProvider.System audits clean |

### Round 3 closure summary (2026-05-02)

- **Patches applied:** R3P1–R3P13 (all 13). Round 3 deferred items (R3W1–R3W4) recorded in `deferred-work.md`.
- **Production code touched:** `src/Hexalith.EventStore.Server/Projections/KeyedSemaphore.cs` (R3P2 finally block); `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs` (R3P1 page-full orphan recovery, R3P3 LastPageCount clamp, R3P5 ValidateIdentity helper lifted to read/save paths, R3P11 Exception.Data disambiguation); `src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs` (R3P10 _nextDueByDomain seed from configured polling domains).
- **Tests added/updated:** `tests/.../Projections/ProjectionCheckpointTrackerTests.cs` (R3P4 mixed-and-lowercase dedup convergence, R3P7 split scope-vs-identity exhaustion, R3P11 Exception.Data assertions, R3P12 page-0 DidNotReceive); `tests/.../Projections/ProjectionPollerServiceTests.cs` (R3P6 cap-boundary delivery count assertion, R3P9 ExecuteAsync FakeTimeProvider behavioral exercise); `tests/.../Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs` (R3P8 orchestrator catch log-assertion); shared `tests/.../TestUtilities/TestLogger.cs` (R3P13 extracted helper).
- **Test evidence:** focused projection tests 47/47 (KeyedSemaphore + ProjectionCheckpointTracker + ProjectionPollerService + ProjectionUpdateOrchestratorRefreshInterval), broader projection 104/104, full server 1697/1697 (+4 over pre-Round-3 baseline of 1693), solution-wide Release build 0 warnings / 0 errors under `TreatWarningsAsErrors`.
- **R3P1 high-severity verification:** new orphan-recovery branch covers the previously-permanent throw loop. The fix corrects `LastPageCount = IdentityPageSize` when the orphan page already holds a full set, so the next retry computes `targetPageNumber = index.PageCount` (a fresh page) and the standard append path proceeds.
- **Story status:** `done`.
