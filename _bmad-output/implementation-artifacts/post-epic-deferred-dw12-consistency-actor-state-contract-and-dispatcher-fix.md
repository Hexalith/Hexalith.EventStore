# Post-Epic Deferred DW12: Consistency Actor-State Contract and Dispatcher Fix

Status: ready-for-dev

Story key: post-epic-deferred-dw12-consistency-actor-state-contract-and-dispatcher-fix
Context created: 2026-05-20
Context completed: 2026-05-20T15:09:00+02:00
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md`
Source evidence: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`

## Story

As an EventStore operator running consistency checks,
I want the checker to validate streams through supported EventStore/admin contracts,
so that actor-state-backed streams are not reported as missing and Consistency UI refreshes stay safe on the Blazor renderer dispatcher.

## Scope

This story covers:

- CC-4 / Issues 16 and 17 from the 2026-05-20 manual Admin UI retest.
- False-positive `sequencecontinuity` and `metadataconsistency` anomalies for the seeded `tenant-a / counter / counter-1` stream.
- The related Blazor Server dispatcher exception observed in `Consistency.razor`.

This story does not cover:

- DW11 projection detail contract gaps.
- DW13 tenant lifecycle actor timeout root-cause investigation.
- DW14 deferred snapshot, compaction, backup, validation, or export UX policy.
- DW15 TypeCatalog navigation/disconnect hygiene.
- Introducing new persistence contracts, changing actor state layout, migrating stored data, redesigning EventStore stream storage, redesigning projection checkpoints, or adding direct DAPR state access to compensate for missing query APIs.
- Full corrupted DAPR actor-state chaos testing, persisted event repair/migration, cross-projector checkpoint reconciliation, large-stream performance/load testing, new metadata query contract design, or automated browser race coverage for every Blazor timing path.

## Acceptance Criteria

1. Consistency checks no longer report missing events 1..N for streams persisted through the current EventStore aggregate actor pipeline when supported stream reads prove those events exist.

2. `DaprConsistencyCommandService` stops reconstructing and reading raw state-store keys directly for event and metadata validation, including:

   ```text
   {tenant}:{domain}:{aggregateId}:events:{sequence}
   {tenant}:{domain}:{aggregateId}:metadata
   ```

3. Sequence continuity reads through an existing supported read contract. `IStreamQueryService.GetStreamTimelineAsync(...)` is the primary source of truth for continuity; `IStreamQueryService.GetEventDetailAsync(...)` may be used only to enrich or verify a specific sequence already identified from timeline analysis, or when the timeline contract cannot cover the required range. `GetEventDetailAsync(...)` must not become the primary traversal mechanism. If the implementation adds a narrower contract, it must still route through EventStore/admin stream reads, not DAPR state-store key guessing.

4. Metadata consistency does not treat missing raw `{tenant}:{domain}:{aggregateId}:metadata` keys as an anomaly for actor-backed streams. The check must not probe raw metadata keys. The metadata result must be one of `Verified`, `Inconclusive`, or `CheckFailed`; `Missing` or `Inconsistent` may be emitted only when a supported metadata/query contract positively proves that condition. The check must either:
   - validate metadata through a supported EventStore/admin metadata/read contract; or
   - use existing `StreamSummary` evidence (`EventCount`, `LastEventSequence`, `LastActivityUtc`, `HasSnapshot`) plus successful stream reads as the supported consistency signal; or
   - return a visible `NotChecked`/`Inconclusive` coarse warning when no supported metadata contract exists.

5. Regression tests cover the live evidence shape: `tenant-a / counter / counter-1`, `EventCount = 18`, `LastEventSequence = 18`, and supported stream reads returning sequences 1 through 18. Tests must prove the continuity check executed by asserting the evaluated sequence range or checked event count for the target stream, and must not infer completeness only from returned item count. The expected result shape is zero `SequenceContinuity` missing-event anomalies, zero raw-metadata missing anomalies, and only classified projection/check-limitation warnings when applicable.

6. Regression tests prove the old implementation path is gone by asserting `DaprConsistencyCommandService` does not read reconstructed event or metadata raw keys through the service's DAPR state access dependency for `SequenceContinuity` and `MetadataConsistency`, including keys containing `:events:` or `:metadata`. If direct `DaprClient.GetStateAsync<T>` verification is not feasible for all overloads, use an existing wrapper, fake, or spied dependency to prove the same behavior.

7. Projection-position warnings are either made granular by tenant/domain/projector when that data is available or explicitly labeled as coarse-grained warnings. A tenant/domain-scoped projection warning must not be counted or worded as an unexplained data-loss anomaly. Where the result model supports it, projection diagnostics expose stable category, severity, and scope fields, not message text only.

8. Query-service failure taxonomy is explicit. Stream not found, existing empty stream, query exception, timeout, cancellation, authorization failure, and incomplete/paged timeline without enough continuation or total-count evidence produce distinct diagnostics when the contracts expose enough signal. None of these cases may be converted into "missing events 1..N", silently treated as healthy, or fall back to raw DAPR actor-state key reads for recovery, diagnostics, or logging enrichment.

9. Real sequence gaps reported by the supported stream query contract are still detected. Continuity validation normalizes out-of-order valid timelines before evaluation, detects duplicate sequence numbers separately from missing sequence numbers, and does not probe raw state keys. For example, `1,3,2,4` passes after ordering, while `1,2,2,4` reports duplicate sequence `2` and missing sequence `3` as distinct findings.

10. `Consistency.razor` uses `await InvokeAsync(...)` for UI state changes in async completion/finally paths that can resume outside the Blazor renderer dispatcher. At minimum, fix direct `StateHasChanged()` calls in `OnTriggerConfirm` and `OnCancelConfirm`, and scan async handlers, timers, callbacks, fire-and-forget continuations, confirmation completion paths, and error/finally paths for other post-await/background direct render calls in the same flow. Direct `StateHasChanged()` is acceptable only from renderer-synchronous code paths.

11. Query-service results are identity-checked before they are used as continuity proof. Returned timeline/detail data must match the requested tenant, domain, and aggregate id; mismatches are reported as query contract failures, not valid continuity data. Tests include at least one adjacent stream for another tenant or domain and prove it is not read, merged, or reported in the target result.

12. Continuity handles paging and read timestamps. If the timeline API exposes total count, continuation, paging metadata, or read timestamp, the check must page until complete, honor explicit total/count coverage, or mark continuity as `Inconclusive`; it must not evaluate a partial page as complete. Results identify whether evaluated stream data is current at check time or based on a known snapshot/timeline read timestamp when the contract exposes that signal.

13. Cancellation is propagated to supported stream reads and stops without emitting fake anomalies. Cancellation may produce a canceled/failed/inconclusive diagnostic according to current result semantics, but must not produce false missing-event findings or raw DAPR fallback.

14. Manual retest for Issues 16 and 17 returns no unexplained anomaly cluster for the seeded Counter stream, no raw-metadata missing anomaly, projection/check-limitation warnings are clearly classified if present, and no Blazor dispatcher exception is logged for `Consistency.OnTriggerConfirm` or `Consistency.OnCancelConfirm`. Manual validation includes rapid trigger/cancel interaction during asynchronous completion and checks both browser console and server logs.

15. Architecture decision is explicit: consistency diagnostics depend on public EventStore query contracts, not physical DAPR actor-state layout. Consequence: checks may become `Inconclusive` when supported contracts do not expose enough information, but they must not infer correctness from private storage keys.

## Assumptions and Minimum Path

Hidden assumptions to make explicit during implementation:

- `IStreamQueryService.GetStreamTimelineAsync(...)` can identify stream existence, event sequence numbers, and enough paging/completeness information to evaluate continuity. If the current contract cannot provide that information, report `Inconclusive` rather than inferring missing events.
- Timeline sequence number is the canonical event order for this diagnostic. If returned timeline fields conflict, document which field is authoritative before evaluating continuity.
- Metadata validation is optional unless a supported query contract positively exposes metadata. Absence of a supported metadata contract is not itself a consistency anomaly.
- Projection diagnostics are advisory unless a supported projection contract proves data loss or corruption. Projection lag, unreadable projection position, or coarse projection status is not event-store integrity failure.
- Cancellation means the check did not complete. A canceled scope must not emit healthy, missing-event, or metadata-inconsistent results.
- Tenant/domain/stream identity from query contracts must be present and match the requested stream before timeline data is evaluated. Missing or mismatched identity is `CheckFailed` or `Inconclusive`.

Preferred implementation thread:

1. Add failing backend tests for the 18-event healthy stream and raw-state access ban.
2. Move sequence continuity reads to `IStreamQueryService.GetStreamTimelineAsync(...)`.
3. Extract a small internal continuity evaluator isolated from DAPR/query plumbing so sequence normalization, missing detection, duplicate detection, empty stream, partial timeline, and result mapping can be tested directly.
4. Add real gap, duplicate/out-of-order, partial timeline, failure taxonomy, cancellation, missing-stream, empty-stream, and identity-isolation tests.
5. Replace raw metadata probing with supported metadata/summary evidence or a visible `NotChecked`/`Inconclusive` result.
6. Keep projection diagnostics category/severity-distinct from event-store integrity diagnostics.
7. Apply the `Consistency.razor` dispatcher fix only after backend tests pass, then run targeted UI tests and manual trigger/cancel validation.

Avoid these implementation choices:

- Do not replace raw DAPR key reads with actor-state key reconstruction through another abstraction.
- Do not loop over `GetEventDetailAsync(1..N)` as the primary continuity scan.
- Do not treat empty query results as healthy unless the query contract positively identifies an existing empty stream.
- Do not downgrade proven missing or duplicate sequences to `Inconclusive`.
- Do not add new EventStore APIs unless existing public contracts make DW12 impossible; if a new contract is required, stop and document the missing capability instead of inventing storage-level access.

Minimum automation set:

- Valid 18-event stream: timeline covers `1..18`, continuity diagnostic proves evaluated range/count, no missing-event anomaly, and no reconstructed DAPR event/metadata key reads.
- Real gap: supported query timeline returns `1,2,4`; missing sequence `3` is reported as `DataIntegrity`.
- Out-of-order: supported query timeline returns `1,3,2,4`; no false missing sequence is reported.
- Duplicate plus gap: supported query timeline returns `1,2,2,4`; duplicate `2` and missing `3` are separate findings.
- Partial/paged timeline: returned page is incomplete relative to count/latest sequence or continuation metadata; result is `Inconclusive`/`CheckFailed`, never healthy.
- Failure taxonomy: query exception, timeout, cancellation, authorization failure, incomplete result, missing stream, and empty stream are covered where the current contracts make them observable; cancellation token is propagated and no raw DAPR fallback occurs.
- Identity isolation: adjacent tenant/domain stream exists in setup; only requested tenant/domain/aggregate is evaluated, and mismatched query result identity is rejected.
- Metadata: unsupported metadata produces visible `NotChecked`/`Inconclusive`; supported summary/version mismatch, if exposed by existing contracts, is reported as an anomaly.
- Projection classification: tests assert stable category/severity/scope fields where supported, not message text only.

## Tasks / Subtasks

- [ ] Add failing server tests for actor-backed sequence continuity. (AC: 1, 3, 5, 6, 11, 12)
  - [ ] Arrange `IStreamQueryService.GetRecentlyActiveStreamsAsync(...)` to return `tenant-a/counter/counter-1` with 18 events.
  - [ ] Arrange `IStreamQueryService.GetStreamTimelineAsync(...)` as the primary supported stream read path and return or prove sequences 1..18.
  - [ ] Assert the completed check observed the supported timeline sequence evidence rather than skipping continuity validation, including evaluated range or checked event count.
  - [ ] Assert the completed check has zero `SequenceContinuity` anomalies.
  - [ ] Assert returned stream identity matches requested tenant/domain/aggregate id before the data is used as continuity proof.
  - [ ] Include an adjacent tenant/domain stream in the fake query setup and assert it is not read, merged, or reported in the target result.
  - [ ] Assert raw event keys such as `tenant-a:counter:counter-1:events:1` and related `*:events:*` keys are not read through the service's DAPR state access dependency.

- [ ] Add failing server tests for real gaps, ordering, duplicates, paging, and query-service failures. (AC: 8, 9, 12, 13)
  - [ ] Arrange an out-of-order supported timeline response with sequences `1,3,2,4` and assert the check normalizes ordering without reporting a false gap.
  - [ ] Arrange a supported timeline/detail response with sequences `1,2,4` and assert sequence `3` is reported as a real missing-event finding.
  - [ ] Arrange a supported timeline response with sequences `1,2,2,4` and assert duplicate sequence `2` and missing sequence `3` are reported as distinct findings.
  - [ ] Arrange partial/paged timeline data and assert the check pages, proves total coverage, or returns `Inconclusive` instead of evaluating a partial page as complete.
  - [ ] Arrange stream not found, existing empty stream, exception, timeout, cancellation, authorization failure, and incomplete timeline cases when feasible, and assert distinct diagnostics according to available contract signals.
  - [ ] Assert cancellation tokens flow into supported stream reads and cancellation does not emit fake missing-event anomalies.
  - [ ] Assert none of these paths falls back to raw DAPR actor-state key reads.

- [ ] Add failing server tests for metadata consistency on the same actor-backed stream. (AC: 2, 4, 5, 6)
  - [ ] Assert the service does not read `tenant-a:counter:counter-1:metadata` directly or any `*:metadata` raw key through the service's DAPR state access dependency.
  - [ ] Assert no `Aggregate metadata is missing.` anomaly appears when the supported stream contract proves the stream exists and is coherent.
  - [ ] If no metadata contract is available, assert the output is a visible `NotChecked`/`Inconclusive` result rather than a silent no-op or error anomaly.
  - [ ] If supported metadata, `StreamSummary`, or timeline evidence exposes version/count mismatch, assert that real inconsistency is still surfaced without raw-key probing.

- [ ] Replace unsupported raw-key validation in `DaprConsistencyCommandService`. (AC: 1-4, 8, 9, 11-13, 15)
  - [ ] Reuse `IStreamQueryService.GetStreamTimelineAsync(...)` as the primary sequence-continuity source before adding new abstractions.
  - [ ] Use `GetEventDetailAsync(...)` only to enrich or verify specific timeline-identified sequences, or when the timeline contract cannot cover the required range.
  - [ ] Sort or consume timeline data by sequence number before evaluating gaps, and report duplicate sequences separately.
  - [ ] Handle partial/paged timelines by paging, proving total/count coverage, or returning `Inconclusive`.
  - [ ] Propagate cancellation tokens to supported stream reads and stop without producing fake anomalies on cancellation.
  - [ ] Keep the existing consistency-check persistence/index behavior intact (`admin:consistency:*`, TTL, conflict guard, cancellation).
  - [ ] Preserve `SnapshotIntegrity` behavior unless this change exposes the same raw-key bug there; if snapshot raw-key validation is also unsound, narrow the fix and tests to avoid creating new false positives.
  - [ ] Preserve tenant/domain/stream isolation; the check must not read or report anomalies outside the requested tenant/domain filter.

- [ ] Tighten projection-position wording or granularity. (AC: 7)
  - [ ] Keep high-lag and position-ahead checks.
  - [ ] For domain-filtered checks, label tenant-scoped projection index limitations as a coarse warning with remediation text.
  - [ ] Keep projection lag/check-limitation warnings type/severity-distinct from event-loss anomalies, using stable category/severity/scope fields where supported.

- [ ] Fix Blazor dispatcher-unsafe refresh paths. (AC: 10)
  - [ ] Replace direct `StateHasChanged()` calls after async work in `OnTriggerConfirm` and `OnCancelConfirm` with `await InvokeAsync(StateHasChanged)` or an equivalent `await InvokeAsync(() => { ...; StateHasChanged(); })`.
  - [ ] Review async handlers, timers, callbacks, fire-and-forget continuations, confirmation completion paths, and error/finally paths for other direct `StateHasChanged()` calls after awaited/background work.
  - [ ] Avoid JS interop or navigation changes in this story unless required by the Consistency page dispatcher fix.

- [ ] Add or update bUnit coverage for Consistency page trigger/cancel completion. (AC: 10)
  - [ ] Prefer extending `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs`.
  - [ ] Assert trigger/cancel completion updates UI state without surfacing dispatcher errors where bUnit can observe it.
  - [ ] If component-test infrastructure cannot observe dispatcher behavior, document manual verification and add a code-review checklist item: no direct `StateHasChanged()` after awaited/background work in `Consistency.razor`.

- [ ] Run targeted validation and record results in the Dev Agent Record. (AC: 14)
  - [ ] Server targeted tests.
  - [ ] UI targeted tests.
  - [ ] Manual Issues 16 and 17 retest after Aspire restart, including rapid trigger/cancel interaction during async completion and browser console plus server log review.

## Dev Notes

### Current State

- `src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs` already owns consistency command orchestration, check state persistence, check index maintenance, cancellation, background scans, anomaly creation, and projection-position checks.
- The service currently discovers streams through `IStreamQueryService.GetRecentlyActiveStreamsAsync(...)`, then `CheckSequenceContinuityAsync` and `CheckMetadataConsistencyAsync` bypass the supported stream service and call `DaprClient.GetStateAsync` against reconstructed raw keys.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` explicitly documents the correct boundary: event data reads delegate to EventStore because actor state uses a different key namespace that is not accessible through plain `GetStateAsync`.
- `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` already reads aggregate events through `IAggregateActor.GetEventsAsync(...)` using `IActorProxyFactory.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), "AggregateActor")`. Reuse this route through `IStreamQueryService` instead of duplicating actor access in Admin.Server.
- `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs` exposes `GetEventsAsync(long fromSequence)`. Existing controller comments show the method is exclusive on the lower bound, so callers that need sequence N use `fromSequence = N - 1`.
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor` already uses `await InvokeAsync(StateHasChanged)` in `LoadDataAsync` and uses `InvokeAsync` around timer/debounce callbacks, but `OnTriggerConfirm` and `OnCancelConfirm` still call `StateHasChanged()` directly before/after awaited work.

### Implementation Guardrails

- Do not make `DaprConsistencyCommandService` aware of backend-specific actor-state physical keys such as `eventstore||AggregateActor||...`. That Redis/DAPR shape appeared in the manual evidence, but it is not a product contract for Admin.Server code.
- Do not add direct `DaprClient.QueryStateAsync`, raw Redis scans, or actor-state prefix matching as the fix. Project rules forbid bypassing actor isolation for aggregate actor state.
- Do not add raw DAPR state reads for recovery, diagnostics, logging enrichment, or fallback behavior when supported query APIs are unavailable, incomplete, or failing.
- Do not move consistency checking into the Blazor UI. The UI should trigger, list, refresh, and render results only.
- Do not weaken tenant/domain filtering. All stream reads must stay tenant-scoped and domain-filtered according to the request.
- Do not report anomalies for tenants/domains/streams outside the requested consistency scope while probing continuity, metadata, or projection-position state.
- Do not log event payload data or raw protected payloads while adding diagnostics. Use tenant, domain, aggregate, sequence, check id, and check type.
- Keep background scan failures honest: infrastructure read failures may produce a failed check or explicit warning, but must not be hidden as "zero anomalies" unless the supported read contract actually proved stream coherence.
- Preserve command result semantics. `TriggerCheckAsync` starts the background check and returns an `AdminOperationResult`; do not turn it into a synchronous long-running scan.
- Categorize diagnostics by meaning where the model supports it: supported-contract missing or duplicate sequences are data-integrity anomalies; query failures, unsupported metadata, partial timeline coverage, and coarse projection checks are operational warnings, check limitations, or inconclusive diagnostics.

### Preferred Design

Use the existing `IStreamQueryService` dependency as the consistency read boundary:

- `GetRecentlyActiveStreamsAsync(...)` remains the discovery source.
- For sequence continuity, call `GetStreamTimelineAsync(stream.TenantId, stream.Domain, stream.AggregateId, fromSequence: null, toSequence: stream.LastEventSequence, count: ...)` as the primary proof path when the configured count can cover the stream. Call `GetEventDetailAsync(...)` only to enrich or verify a specific sequence already identified from timeline analysis, or if the timeline contract is capped.
- Validate returned timeline/detail identity before using it as proof. The tenant, domain, and aggregate id must match the target stream.
- Treat a successful supported stream read with complete contiguous sequence coverage as continuity proof. Record the evaluated range, checked event count, or equivalent diagnostic evidence so tests can prove the check executed.
- Treat out-of-order valid timelines as order-normalizable, supported stream reads that return non-contiguous sequences as real findings, and duplicate sequences as separate findings from missing sequences.
- Treat supported stream-read failures, missing streams, empty streams, authorization failures, cancellations, and incomplete timeline coverage as distinct diagnostics when the contract exposes enough signal. If paging or total-count evidence is available, page until complete or prove coverage; otherwise mark the result `Inconclusive`.
- For metadata consistency, prefer a supported metadata contract if one already exists. If none exists, use `StreamSummary.EventCount == StreamSummary.LastEventSequence` plus successful reads as the supported signal and avoid the raw metadata anomaly. An explicit `NotChecked`/`Inconclusive` coarse warning is acceptable only when it tells operators the metadata check lacks a supported contract.

### Decision Record

- Decision: consistency diagnostics depend on public EventStore query contracts, not physical DAPR actor-state layout.
- Consequence: checks may become `Inconclusive` when supported contracts do not expose enough information, but they must not infer correctness from private storage keys.
- Rationale: a truthful inconclusive diagnostic is safer than a false corruption alarm or a storage-coupled implementation that breaks actor isolation.

If a new helper is needed, keep it narrow and testable inside Admin.Server, for example a private helper that checks continuity through `IStreamQueryService`. Add a public abstraction only if tests become brittle or the existing interface cannot express the supported read.

The smallest safe scope is the consistency diagnostic path plus `Consistency.razor` state marshalling. If existing query contracts cannot support the minimum diagnostics above, document that missing capability before adding any new public API.

### Previous Story Intelligence

DW10 just completed truthful metric zero rendering, State Inspector stale modal reset, and role switcher visible-copy cleanup. Useful lessons:

- Write targeted failing tests before product fixes, then rerun with `-m:1` to avoid transient shared build file locks.
- Admin UI and Admin.Server changes require an Aspire restart before manual browser validation.
- Keep manual retest artifacts precise; this story should update evidence for Issues 16 and 17 only.
- DW10 validation used targeted Admin.Server and Admin.UI test filters first, then broader project tests when warranted.

### Latest Technical Notes

- Microsoft Learn for ASP.NET Core Blazor synchronization context (`view=aspnetcore-10.0`) says components use a synchronization context and external/background-triggered updates should dispatch through `InvokeAsync` before rendering. This supports using `await InvokeAsync(StateHasChanged)` in async/timer-like completion paths.
- Dapr docs for actors/state management (current v1.17.7 docs) state that actor state is stored by the actors runtime in the configured actor state store and that writes should go through Dapr state management or actors APIs. Dapr also documents actor state as stored with a specific scheme in transactional stores; Admin.Server must not hard-code that physical scheme as an EventStore product contract.

## Testing Requirements

Run targeted tests first:

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release --filter "FullyQualifiedName~DaprConsistencyCommandServiceTests" -m:1
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~ConsistencyPageTests" -m:1
```

If shared contracts or EventStore stream endpoints are changed, also run the narrow affected suites:

```powershell
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release --filter "FullyQualifiedName~DaprStreamQueryServiceTests" -m:1
dotnet test tests/Hexalith.EventStore.Server.Tests --configuration Release --filter "FullyQualifiedName~AdminStreamQueryController" -m:1
```

Manual validation after code changes:

```powershell
$env:EnableKeycloak = 'false'
aspire stop --non-interactive --apphost .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
aspire run --project .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
```

Then rerun the Issue 16 and Issue 17 block from `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`.

## Project Structure Notes

Expected implementation files:

- `src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`

Likely test files:

- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprConsistencyCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs`

Only touch these if the chosen supported contract requires it:

- `src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`
- `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`
- related `DaprStreamQueryServiceTests` or EventStore controller tests.

## References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md` section 5.3.
- `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md` section CC-4 / Issues 16 and 17.
- `_bmad-output/project-context.md` rules: DAPR actor state must go through `IActorStateManager`; never bypass actor isolation with direct `DaprClient` for aggregate actor state.
- `_bmad-output/planning-artifacts/architecture.md` rules: use `IActorStateManager` for all actor state operations and never use DaprClient bypass for actor state.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` summary comment: Event data reads delegate to EventStore because actor state uses a different key namespace.
- `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` admin stream endpoints and `IAggregateActor.GetEventsAsync(...)` usage.
- Microsoft Learn: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0
- Dapr Docs: https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/
- Dapr Docs: https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/

## Dev Agent Record

### Agent Model Used

TBD by dev agent.

### Debug Log References

- Manual evidence trace dumps: `_bmad-output/test-artifacts/admin-ui-manual-follow-up-trace-dumps-2026-05-20/`
- Observed Consistency dispatcher trace: `Consistency.OnTriggerConfirm` direct `StateHasChanged()` after async work.

### Completion Notes List

- Story context engine analysis completed.
- Existing handoff was expanded into a ready-for-dev implementation guide.
- Sprint status row should be advanced from `backlog` to `ready-for-dev`.
- Party-mode review hardening applied: timeline-first continuity contract, `NotChecked`/`Inconclusive` metadata behavior, query-failure and real-gap regression expectations, projection warning classification, dispatcher scan scope, tenant isolation guardrail, and explicit non-goal for storage/projection redesign.
- Advanced elicitation hardening applied: duplicate/out-of-order sequence coverage, partial/paged timeline protections, query failure taxonomy, cancellation propagation, identity validation, visible unsupported-metadata diagnostics, stable diagnostic classification, manual rapid trigger/cancel validation, and an explicit query-contract decision record.
- Second elicitation simplification applied: explicit hidden assumptions, preferred implementation thread, implementation choices to avoid, deferred chaos scope, and minimum automation set.

### File List

To be populated by dev agent during implementation.
