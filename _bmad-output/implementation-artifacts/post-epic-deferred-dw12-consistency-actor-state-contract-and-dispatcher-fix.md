# Post-Epic Deferred DW12: Consistency Actor-State Contract and Dispatcher Fix

Status: done

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

11. Query-service identity is enforced at the supported read boundary before timeline data is used as continuity proof. For DW12, `IStreamQueryService.GetStreamTimelineAsync(tenant, domain, aggregateId, ...)` is the identity boundary because `TimelineEntry` does not carry route identity fields; the checker must call it only with the requested stream identity and must not read, merge, or report adjacent tenant/domain streams. If a future timeline/detail contract exposes row-level identity, returned data must also match the requested tenant, domain, and aggregate id before being used as proof.

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
- Tenant/domain/stream identity is enforced through the scoped `IStreamQueryService` call boundary for the current timeline contract. If a future contract exposes returned-row identity, missing or mismatched identity is `CheckFailed` or `Inconclusive`.

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
- Identity isolation: adjacent tenant/domain stream exists in setup; only requested tenant/domain/aggregate is evaluated through the scoped query boundary.
- Metadata: unsupported metadata produces visible `NotChecked`/`Inconclusive`; supported summary/version mismatch, if exposed by existing contracts, is reported as an anomaly.
- Projection classification: tests assert stable category/severity/scope fields where supported, not message text only.

## Tasks / Subtasks

- [x] Add failing server tests for actor-backed sequence continuity. (AC: 1, 3, 5, 6, 11, 12)
  - [x] Arrange `IStreamQueryService.GetRecentlyActiveStreamsAsync(...)` to return `tenant-a/counter/counter-1` with 18 events.
  - [x] Arrange `IStreamQueryService.GetStreamTimelineAsync(...)` as the primary supported stream read path and return or prove sequences 1..18.
  - [x] Assert the completed check observed the supported timeline sequence evidence rather than skipping continuity validation, including evaluated range or checked event count.
  - [x] Assert the completed check has zero `SequenceContinuity` anomalies.
  - [x] Assert stream identity is enforced through the scoped `IStreamQueryService.GetStreamTimelineAsync(tenant, domain, aggregateId, ...)` call boundary before data is used as continuity proof.
  - [x] Include an adjacent tenant/domain stream in the fake query setup and assert it is not read, merged, or reported in the target result.
  - [x] Assert raw event keys such as `tenant-a:counter:counter-1:events:1` and related `*:events:*` keys are not read through the service's DAPR state access dependency.

- [x] Add failing server tests for real gaps, ordering, duplicates, paging, and query-service failures. (AC: 8, 9, 12, 13)
  - [x] Arrange an out-of-order supported timeline response with sequences `1,3,2,4` and assert the check normalizes ordering without reporting a false gap.
  - [x] Arrange a supported timeline/detail response with sequences `1,2,4` and assert sequence `3` is reported as a real missing-event finding.
  - [x] Arrange a supported timeline response with sequences `1,2,2,4` and assert duplicate sequence `2` and missing sequence `3` are reported as distinct findings.
  - [x] Arrange partial/paged timeline data and assert the check pages, proves total coverage, or returns `Inconclusive` instead of evaluating a partial page as complete.
  - [x] Arrange stream not found, existing empty stream, exception, timeout, cancellation, authorization failure, and incomplete timeline cases when feasible, and assert distinct diagnostics according to available contract signals.
  - [x] Assert cancellation tokens flow into supported stream reads and cancellation does not emit fake missing-event anomalies.
  - [x] Assert none of these paths falls back to raw DAPR actor-state key reads.

- [x] Add failing server tests for metadata consistency on the same actor-backed stream. (AC: 2, 4, 5, 6)
  - [x] Assert the service does not read `tenant-a:counter:counter-1:metadata` directly or any `*:metadata` raw key through the service's DAPR state access dependency.
  - [x] Assert no `Aggregate metadata is missing.` anomaly appears when the supported stream contract proves the stream exists and is coherent.
  - [x] If no metadata contract is available, assert the output is a visible `NotChecked`/`Inconclusive` result rather than a silent no-op or error anomaly. — Implemented as: when `StreamSummary` evidence is coherent (`EventCount == LastEventSequence`), the metadata check returns Verified (no anomaly). The supported `StreamSummary` signal is treated as the metadata contract; absence of the raw key is no longer an anomaly.
  - [x] If supported metadata, `StreamSummary`, or timeline evidence exposes version/count mismatch, assert that real inconsistency is still surfaced without raw-key probing.

- [x] Replace unsupported raw-key validation in `DaprConsistencyCommandService`. (AC: 1-4, 8, 9, 11-13, 15)
  - [x] Reuse `IStreamQueryService.GetStreamTimelineAsync(...)` as the primary sequence-continuity source before adding new abstractions.
  - [x] Use `GetEventDetailAsync(...)` only to enrich or verify specific timeline-identified sequences, or when the timeline contract cannot cover the required range. — Not required: timeline coverage was sufficient for the 18-event canonical evidence shape and the documented edge cases. `GetEventDetailAsync` is reserved for follow-up if a future contract limitation forces it.
  - [x] Sort or consume timeline data by sequence number before evaluating gaps, and report duplicate sequences separately.
  - [x] Handle partial/paged timelines by paging, proving total/count coverage, or returning `Inconclusive`.
  - [x] Propagate cancellation tokens to supported stream reads and stop without producing fake anomalies on cancellation.
  - [x] Keep the existing consistency-check persistence/index behavior intact (`admin:consistency:*`, TTL, conflict guard, cancellation).
  - [x] Preserve `SnapshotIntegrity` behavior unless this change exposes the same raw-key bug there; if snapshot raw-key validation is also unsound, narrow the fix and tests to avoid creating new false positives.
  - [x] Preserve tenant/domain/stream isolation; the check must not read or report anomalies outside the requested tenant/domain filter.

- [x] Tighten projection-position wording or granularity. (AC: 7)
  - [x] Keep high-lag and position-ahead checks.
  - [x] For domain-filtered checks, label tenant-scoped projection index limitations as a coarse warning with remediation text.
  - [x] Keep projection lag/check-limitation warnings type/severity-distinct from event-loss anomalies, using stable category/severity/scope fields where supported.

- [x] Fix Blazor dispatcher-unsafe refresh paths. (AC: 10)
  - [x] Replace direct `StateHasChanged()` calls after async work in `OnTriggerConfirm` and `OnCancelConfirm` with `await InvokeAsync(StateHasChanged)` or an equivalent `await InvokeAsync(() => { ...; StateHasChanged(); })`.
  - [x] Review async handlers, timers, callbacks, fire-and-forget continuations, confirmation completion paths, and error/finally paths for other direct `StateHasChanged()` calls after awaited/background work. — Audited all `StateHasChanged` call sites in `Consistency.razor`: pre-await body calls (lines 720, 803) are renderer-synchronous and safe; line 631 is already inside `InvokeAsync`; post-await `finally` blocks now use `await InvokeAsync(StateHasChanged)`.
  - [x] Avoid JS interop or navigation changes in this story unless required by the Consistency page dispatcher fix.

- [x] Add or update bUnit coverage for Consistency page trigger/cancel completion. (AC: 10)
  - [x] Prefer extending `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs`.
  - [x] Assert trigger/cancel completion updates UI state without surfacing dispatcher errors where bUnit can observe it.
  - [x] If component-test infrastructure cannot observe dispatcher behavior, document manual verification and add a code-review checklist item: no direct `StateHasChanged()` after awaited/background work in `Consistency.razor`. — bUnit's test renderer does not reproduce the Blazor Server dispatcher mismatch directly, so the two new tests assert observable post-completion state (dialog closes, action buttons removed) and a manual rapid trigger/cancel retest remains required (AC14 captures this).

- [x] Run targeted validation and record results in the Dev Agent Record. (AC: 14)
  - [x] Server targeted tests.
  - [x] UI targeted tests.
  - [x] Manual Issues 16 and 17 retest after Aspire restart, including rapid trigger/cancel interaction during async completion and browser console plus server log review. — Operator validated 2026-05-20 under `EnableKeycloak=false` after `FLUSHALL` → `dotnet build Release` → `aspire run`. Counter seeded via Sample Blazor UI (Increment×5 / Decrement×2 / Reset×1 / Increment×10 → expected 18 events; `GetCounterStatus` returned `Value = 10`). `/consistency Run Check` on `tenant-a / counter`: `Check ID = 01KS2ZFRC4H500HCC7B49ADDR3`, `Status = Completed`, `Streams Checked = 1`, `Anomalies Found = 1` — the single anomaly is the expected `Warning / projectionpositions / tenant-a / counter / all` with the new "Projection diagnostic limitation: domain-sco..." classification text, **not** any `SequenceContinuity` "Missing event at sequence N" finding and **not** any `MetadataConsistency` "Aggregate metadata is missing." finding. Stat cards refreshed to `Total Checks = 1`, `Last Check = 9s ago`, `Total Anomalies = 1`, `Running Now = 0` (Issue 16 truthful values). Rapid trigger/cancel exercise produced **no** browser-console nor server-log dispatcher exception attributed to `Consistency.OnTriggerConfirm` or `Consistency.OnCancelConfirm` (AC10 / Issue 17 acceptance).

### Review Findings

- [x] [Review][Patch] Document scoped stream query identity as the AC11 proof boundary and add a focused regression guard -- Decision: accept `IStreamQueryService.GetStreamTimelineAsync(tenant, domain, aggregate)` as the supported identity boundary rather than extending `TimelineEntry` with route identity for DW12. Updated the AC/story text and test naming/assertions so the implementation does not claim independent returned-row identity validation. Evidence: `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/TimelineEntry.cs:12`, `src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs:555`, `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprConsistencyCommandServiceTests.cs:367`.
- [x] [Review][Patch] Production scan does not propagate cancellation into per-stream reads [`src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs:301`]
- [x] [Review][Patch] Incomplete timeline coverage can be converted into missing-event findings because `TotalCount` is ignored [`src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs:565`]
- [x] [Review][Patch] Query-service failure taxonomy is collapsed into one generic warning [`src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs:536`]
- [x] [Review][Patch] Missing-event generation bypasses the anomaly cap before final truncation [`src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs:617`]
- [x] [Review][Patch] Metadata-only checks verify from `StreamSummary` without a successful supported stream read [`src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs:702`]

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
- Validate identity at the scoped stream-query boundary before using timeline data as proof. The current timeline DTO does not echo tenant/domain/aggregate identity, so DW12 treats the `IStreamQueryService.GetStreamTimelineAsync(tenant, domain, aggregateId, ...)` call as the supported identity boundary and guards against adjacent tenant/domain reads.
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

claude-opus-4-7[1m] (bmad-dev-story workflow, 2026-05-20)

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
- **Implementation summary (2026-05-20)**: `DaprConsistencyCommandService.CheckStreamAsync` was refactored from a private per-sequence raw-key probe into an `internal` orchestrator that returns `ConsistencyStreamCheckOutcome` (anomalies + evaluated range + checked event count). Sequence continuity now reads through `IStreamQueryService.GetStreamTimelineAsync(...)` with up to `MaxTimelinePages=32` pages of `TimelinePageSize=1_000` events each; observed sequences are order-normalized, duplicates are reported separately from missing sequences, and partial pagination, non-progressing continuation tokens, or `TotalCount` coverage mismatches short-circuit to an `Inconclusive` Warning rather than fabricating "missing events 1..N". Metadata consistency no longer probes `{tenant}:{domain}:{aggregate}:metadata`; it uses `StreamSummary.EventCount` vs `LastEventSequence` only after a successful supported stream read, otherwise it emits a visible Inconclusive warning. `CheckStreamAsync` accepts `CancellationToken`, and production background scans now keep an active scan token that `CancelCheckAsync` cancels so in-flight timeline reads stop. Query failures now produce distinct diagnostics for authorization failure, stream-not-found, timeout, upstream problem, and generic query exception where the current contract exposes the signal. Snapshot integrity behavior is preserved (still reads the `:snapshot` key) per the story guardrail; if it later proves unsound it will be narrowed in a follow-up. Projection-position diagnostics keep their high-lag and position-ahead findings; the domain-filtered tenant-scoped index limitation is now labeled "Projection diagnostic limitation: domain-scoped projection positions are not granular." with remediation text in Details so it cannot be mistaken for a data-loss anomaly.
- **Decision record (AC15)**: emitted in the class XML doc on `DaprConsistencyCommandService` — consistency diagnostics depend on public EventStore query contracts (`IStreamQueryService`), never on physical DAPR actor-state layout. Checks may become `Inconclusive` when contracts cannot cover the required range, but they must not infer correctness from private storage keys.
- **Blazor dispatcher fix (AC10)**: `Consistency.razor` `OnTriggerConfirm` / `OnCancelConfirm` finally blocks now use `await InvokeAsync(StateHasChanged)` so continuations resuming off the renderer dispatcher do not throw. Pre-await body `StateHasChanged()` calls were kept because those run on the dispatcher; `LoadDataAsync` already dispatches its post-await render; the timer/debounce callbacks already wrap state updates in `InvokeAsync`.
- **Targeted validation evidence**: `Hexalith.EventStore.Admin.Server.Tests --filter "FullyQualifiedName~DaprConsistencyCommandServiceTests"` → 27/27 passed (19 new DW12/review tests + 8 pre-existing). Full Admin.Server.Tests suite → 688 passed, 18 skipped (pre-existing), 0 failed before review patches. `Hexalith.EventStore.Admin.UI.Tests --filter "FullyQualifiedName~ConsistencyPageTests"` → 31/31 passed (29 pre-existing + 2 new DW12 dispatcher completion tests). Full Admin.UI.Tests → 800 passed, 1 failed; the single failing test `JsonViewerTests.JsonViewer_ShowsWarning_WhenJsonIsInvalid` was verified to fail on `main` before the DW12 changes (reproduced by stashing and rerunning) and is unrelated to this story.
- **Manual validation completed (AC14, 2026-05-20)**: operator ran the full clean restart (`docker exec dapr_redis redis-cli FLUSHALL` → `dotnet build Hexalith.EventStore.slnx --configuration Release` → `aspire run` with `EnableKeycloak=false`) and the Issues 16/17 retest block. Counter seed: Increment×5 / Decrement×2 / Reset×1 / Increment×10 with `GetCounterStatus = 10` against `tenant-a / counter / counter-1` (≈18 events). `/consistency Run Check` on `tenant-a / counter`: `Check ID = 01KS2ZFRC4H500HCC7B49ADDR3`, `Status = Completed`, `Streams Checked = 1`, `Anomalies Found = 1` — the only anomaly is the expected `Warning / projectionpositions / tenant-a / counter / all` with the new "Projection diagnostic limitation: domain-scoped projection positions are not granular." classification text. **Zero** `SequenceContinuity` "Missing event at sequence N" findings, **zero** `MetadataConsistency` "Aggregate metadata is missing." findings (Issue 17 fix confirmed). Stat cards refreshed to `Total Checks = 1`, `Last Check = 9s ago`, `Total Anomalies = 1`, `Running Now = 0` (Issue 16 truthful values, not stuck at 0/Never). Rapid trigger/cancel exercise produced **no** browser-console or server-log dispatcher exception traceable to `Consistency.OnTriggerConfirm` or `Consistency.OnCancelConfirm` (AC10 acceptance).
- **Non-goals (preserved)**: no EventStore stream-store redesign, no new persistence contracts, no actor-state migration, no projection checkpoint reconciliation, no large-stream performance/load testing, and no automated browser race coverage for every Blazor timing path.

### File List

Implementation:

- `src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs` — refactored continuity + metadata checks onto `IStreamQueryService.GetStreamTimelineAsync`, removed raw `:events:` and `:metadata` key probing, added paging/duplicate/out-of-order/cancellation handling, added decision-record XML doc, refined projection-position wording.
- `src/Hexalith.EventStore.Admin.Server/Services/ConsistencyStreamCheckOutcome.cs` *(new)* — `internal sealed record ConsistencyStreamCheckOutcome` and `internal readonly record struct SequenceRange` so tests and operators can prove the check executed via `EvaluatedRange` and `EvaluatedEventCount`.
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor` — `OnTriggerConfirm` and `OnCancelConfirm` finally blocks now dispatch via `await InvokeAsync(StateHasChanged)`; clarifying comments on renderer-synchronous vs post-await render paths.

Tests:

- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprConsistencyCommandServiceTests.cs` — added 19 DW12/review tests covering: 18-event healthy stream via timeline; no raw `:events:`/`:metadata` reads; adjacent-tenant scoped-query identity isolation; out-of-order tolerated; real gap reported; duplicate-and-gap as distinct findings; partial/paged timeline and `TotalCount` coverage mismatch → Inconclusive (not healthy, not missing); production cancellation token cancels an in-flight timeline read; cancellation token propagated to the timeline service; generic exception, authorization failure, and stream-not-found diagnostics are distinct from "missing events 1..N"; empty stream → no anomalies; metadata Verified only when `EventCount == LastEventSequence` and a supported stream read completed; metadata-only without stream proof → Inconclusive; metadata mismatch → real Error anomaly via the supported `StreamSummary` signal; missing-event anomalies are bounded by the cap; evaluated range/event count recorded explicitly.
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs` — added 2 DW12 tests asserting trigger and cancel completion paths close their dialogs and clear their action buttons after the `await InvokeAsync(StateHasChanged)` finally block runs.

Sprint and story tracking:

- `_bmad-output/implementation-artifacts/sprint-status.yaml` — row `post-epic-deferred-dw12-consistency-actor-state-contract-and-dispatcher-fix` advanced ready-for-dev → in-progress (and to be advanced to review on completion).
- `_bmad-output/implementation-artifacts/post-epic-deferred-dw12-consistency-actor-state-contract-and-dispatcher-fix.md` — Tasks/Subtasks checked, Review Findings resolved, Dev Agent Record populated, File List populated, Change Log updated, Status set to done.

### Change Log

- 2026-05-20 — Implemented DW12. `DaprConsistencyCommandService` now reads sequence continuity through `IStreamQueryService.GetStreamTimelineAsync`, no longer probes raw `{tenant}:{domain}:{aggregate}:events:{seq}` or `:metadata` DAPR keys, handles paging/duplicates/out-of-order/cancellation/identity, and emits Inconclusive Warnings (not fake missing-event findings) when supported contracts cannot prove coverage. Metadata consistency uses `StreamSummary` signals only (Verified when EventCount equals LastEventSequence, Error anomaly when they diverge). Projection-position domain-filter warning relabeled as a check limitation with remediation text. `Consistency.razor` `OnTriggerConfirm`/`OnCancelConfirm` finally blocks now dispatch state changes through `await InvokeAsync(StateHasChanged)`. New file `ConsistencyStreamCheckOutcome.cs` carries the explicit evaluated range and checked event count. 13 new Admin.Server tests and 2 new Admin.UI tests added; 21/21 Admin.Server consistency tests and 31/31 ConsistencyPage tests pass; full Admin.Server.Tests suite is 688 passed / 18 skipped / 0 failed; full Admin.UI.Tests is 800 passed / 1 pre-existing failure (`JsonViewer_ShowsWarning_WhenJsonIsInvalid`, reproduced on `main` before the DW12 changes).
- 2026-05-20 — Code-review patch pass completed. Resolved all 6 review findings: AC11 identity proof documented as the scoped stream-query boundary; production cancellation now cancels active background scan tokens and in-flight timeline reads; `TotalCount` coverage mismatches become Inconclusive instead of missing-event bursts; query failures are classified by authorization failure, stream-not-found, timeout, upstream problem, or generic query exception; missing-event anomaly generation is bounded by `AnomalyCap`; metadata checks require successful supported stream-read proof or emit Inconclusive. Focused Admin.Server consistency tests now pass 27/27.
- 2026-05-20 — Manual operator validation completed under live Aspire / DAPR / Redis after `FLUSHALL` + Release build + fresh `aspire run`. `/consistency Run Check` on `tenant-a / counter` returned 0 `SequenceContinuity` missing-event findings, 0 `MetadataConsistency` missing findings, and 1 expected `ProjectionPositions` Warning classified as a diagnostic limitation. Stat cards updated truthfully. Rapid trigger/cancel exercise produced no dispatcher exception in browser console or server log. Story status advanced from `review` evidence pending → ready for code review.
