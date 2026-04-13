# Story 15.13: Stream Activity Tracker Writer

Status: done

## Definition of Done

- All 6 ACs verified
- Build: `dotnet build Hexalith.EventStore.slnx --configuration Release` with zero warnings
- All 8 new writer Tier 1 tests pass: `dotnet test tests/Hexalith.EventStore.Client.Tests/`
- Reader regression test passes: `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/`
- No regressions in existing Tier 1 tests
- No new analyzer suppressions
- Manual smoke test: run AppHost, increment counter 3x, verify `admin:stream-activity:all` contains 1 entry with `EventCount=3`, verify `/events` page shows events, verify `/streams` page still works

## Story

As a **developer using the Admin UI to investigate event activity**,
I want **the Admin stream activity index (`admin:stream-activity:all`) to be populated whenever commands produce events**,
so that **the Streams page, Events page, and Activity Feed show real data instead of being permanently empty**.

## Acceptance Criteria

1. **Writer creates index entry** -- Given a command that produces events is submitted, When `SubmitCommandHandler` completes routing, Then `DaprStreamActivityTracker.TrackAsync` writes a `StreamSummary` to the DAPR state key `admin:stream-activity:all` with the correct `TenantId`, `Domain`, `AggregateId`, `EventCount`, `LastEventSequence`, `LastActivityUtc`, and `StreamStatus = Active`.

2. **Writer accumulates counters** -- Given a stream that already has an entry in the index, When a new command produces more events, Then the existing entry's `EventCount` is incremented by the new event count and `LastEventSequence` is updated (not replaced).

3. **Writer skips rejected commands** -- Given a command that produces zero events (rejected or idempotent), When `SubmitCommandHandler` processes it, Then `DaprStreamActivityTracker.TrackAsync` returns immediately without writing to the state store.

4. **Writer is advisory (Rule 12)** -- Given the DAPR state store is unavailable, When `DaprStreamActivityTracker.TrackAsync` fails, Then the exception is logged but does NOT propagate -- command processing succeeds normally.

5. **Reader uses single global key** -- Given the Admin Server reads the stream activity index via `DaprStreamQueryService.GetRecentlyActiveStreamsAsync`, When a tenant filter is provided, Then the reader fetches from the single key `admin:stream-activity:all` and filters in memory (not per-tenant keys).

6. **No log flood** -- Given the dashboard polls every 30 seconds, When the stream activity index is empty (no commands submitted yet), Then the log level is Debug (not Warning), with message: "Stream activity index '{IndexKey}' is empty."

## Tasks / Subtasks

- [x] **Task 1: Create `IStreamActivityTracker` interface** (AC: 1, 4)
    - [x] 1.1 Create `src/Hexalith.EventStore.Server/Commands/IStreamActivityTracker.cs`
    - [x] 1.2 Single method: `Task TrackAsync(string tenantId, string domain, string aggregateId, long newEventsAppended, DateTimeOffset timestamp, CancellationToken ct = default)`
    - [x] 1.3 XML doc: "Implementations are advisory -- failures must not block command processing (Rule 12)"
    - [x] 1.4 **Checkpoint**: Build compiles

- [x] **Task 2: Create `DaprStreamActivityTracker` implementation** (AC: 1, 2, 3, 4)
    - [x] 2.1 Create `src/Hexalith.EventStore/Commands/DaprStreamActivityTracker.cs`
    - [x] 2.2 Constructor: `DaprStreamActivityTracker(DaprClient daprClient, IOptions<CommandStatusOptions> options, ILogger<DaprStreamActivityTracker> logger)`
    - [x] 2.3 Constants: `MaxEntries = 1000`, `MaxEtagRetries = 3`, `ActivityIndexKey = "admin:stream-activity:all"`
    - [x] 2.4 `TrackAsync`: early return if `newEventsAppended <= 0` (AC 3). Build `StreamSummary`. Delegate to `TryUpsertActivityIndexAsync`
    - [x] 2.5 `TryUpsertActivityIndexAsync`: ETag retry loop (clone from `DaprCommandActivityTracker.TryUpsertActivityIndexAsync`). Identity match on `(TenantId, Domain, AggregateId)` case-insensitive. On match: accumulate `EventCount += newEventsAppended`, set `LastEventSequence`, update `LastActivityUtc`. On new: insert. Order by `LastActivityUtc` desc, take `MaxEntries`
    - [x] 2.6 `HasSnapshot: false` on new entries, preserve existing value on updates
    - [x] 2.7 `StreamStatus = StreamStatus.Active` on every write. Add code comment: `// TODO: Preserve non-Active StreamStatus when tombstoning is implemented`
    - [x] 2.8 All exceptions caught and logged (Rule 12) -- never propagate
    - [x] 2.9 Reuse `CommandStatusOptions.StateStoreName` for DAPR state store name
    - [x] 2.10 **Checkpoint**: Build compiles with zero warnings

- [x] **Task 3: Hook into `SubmitCommandHandler`** (AC: 1, 3, 4)
    - [x] 3.1 Add `IStreamActivityTracker? streamActivityTracker` as optional primary-constructor parameter in `SubmitCommandHandler`
    - [x] 3.2 Update existing backward-compatible overload constructors to also pass `null` for the new parameter. The handler already has 4 overloads for `ICommandActivityTracker?` and `IBackpressureTracker?` — extend each to forward `null` for `streamActivityTracker`. Do NOT add new overloads — update existing ones to include the new parameter in their delegation chain
    - [x] 3.3 After the existing command-tracker block (around line 120), add stream-tracker block:
        - Gate on `streamActivityTracker is not null`
        - Gate on `processingResult.Accepted && (finalStatus?.EventCount ?? 0) > 0`
        - Call `streamActivityTracker.TrackAsync(request.Tenant, request.Domain, request.AggregateId, finalStatus.EventCount.Value, finalStatus.Timestamp ?? DateTimeOffset.UtcNow, cancellationToken)`
        - Wrap in try/catch, log warning on failure (new event ID `1105 StreamActivityTrackingFailed`)
    - [x] 3.4 Share the existing `finalStatus` variable -- do NOT add a second `ReadStatusAsync` call
    - [x] 3.5 **Checkpoint**: Build compiles with zero warnings

- [x] **Task 4: Register in DI** (AC: 1)
    - [x] 4.1 In `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs`, after the command-tracker registration (~line 113), add:
        ```csharp
        // Stream activity tracking for admin UI Streams/Events pages (DAPR state store backed).
        // Writer only — the reader lives in DaprStreamQueryService on the Admin.Server side.
        _ = services.AddSingleton<DaprStreamActivityTracker>();
        _ = services.AddSingleton<IStreamActivityTracker>(sp => sp.GetRequiredService<DaprStreamActivityTracker>());
        ```
    - [x] 4.2 **Checkpoint**: Build compiles

- [x] **Task 5: Adapt Admin.Server reader** (AC: 5, 6)
    - [x] 5.1 In `DaprStreamQueryService.GetRecentlyActiveStreamsAsync` (~line 99), change key from `$"admin:stream-activity:{tenantId ?? "all"}"` to constant `"admin:stream-activity:all"`
    - [x] 5.2 Apply tenant filter in memory: `Where(s => s.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase))` when tenantId is not null/whitespace
    - [x] 5.3 Apply domain filter in memory (already exists, keep it)
    - [x] 5.4 Downgrade "index not found" log from Warning to Debug, update message: `"Stream activity index '{IndexKey}' is empty. No commands have produced events yet, or the writer has not run."`
    - [x] 5.5 Update any existing tests in `DaprStreamQueryServiceTests.cs` that assert the old per-tenant key format (`admin:stream-activity:{tenantId}`) to use the new constant key `admin:stream-activity:all`
    - [x] 5.6 **Checkpoint**: Build compiles with zero warnings

- [x] **Task 6: Tier 1 unit tests** (AC: 1, 2, 3, 4)
    - [x] 6.1 Create `tests/Hexalith.EventStore.Client.Tests/Commands/DaprStreamActivityTrackerTests.cs`
    - [x] 6.2 Test: `TrackAsync_NewStream_InsertsNewSummary` -- empty index -> new entry with correct counters
    - [x] 6.3 Test: `TrackAsync_ExistingStream_AccumulatesEventCountAndSequence` -- existing entry -> cumulative update
    - [x] 6.4 Test: `TrackAsync_ZeroNewEvents_IsNoOp` -- no state store call when `newEventsAppended <= 0`
    - [x] 6.5 Test: `TrackAsync_DifferentAggregates_KeepsBothEntries` -- identity match on `(TenantId, Domain, AggregateId)`
    - [x] 6.6 Test: `TrackAsync_SameAggregateDifferentTenants_KeepsBothEntries` -- multi-tenant isolation
    - [x] 6.7 Test: `TrackAsync_EtagMismatch_RetriesUntilSaveSucceeds` -- ETag retry loop
    - [x] 6.8 Test: `TrackAsync_DaprThrows_SwallowsException` -- Rule 12 compliance
    - [x] 6.9 Test: `TrackAsync_ExceedsMaxEntries_TrimsOldestByLastActivityUtc` -- bounded FIFO cap at 1000 entries
    - [x] 6.10 Follow `DaprCommandActivityTrackerTests.cs` conventions: NSubstitute for `DaprClient`, Shouldly assertions, `SetupGetStateAndEtag`/`SetupTrySave` helper methods
    - [x] 6.11 **Checkpoint**: All 8 new tests pass, all existing Tier 1 tests pass

- [x] **Task 6b: Reader regression test** (AC: 5)
    - [x] 6b.1 In existing `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceTests.cs`, add test: `GetRecentlyActiveStreamsAsync_WithTenantFilter_FiltersFromGlobalKey` -- verify key is always `admin:stream-activity:all` and tenant filtering happens in memory. This prevents accidental revert to per-tenant keys
    - [x] 6b.2 **Checkpoint**: All existing + new reader tests pass

- [ ] **Task 7: Manual smoke test** (AC: 1, 2, 5, 6)
    - [ ] 7.1 Run AppHost, confirm `admin:stream-activity:all` returns HTTP 204 (empty) before any commands
    - [ ] 7.2 Submit 3 `IncrementCounter` commands via sample UI
    - [ ] 7.3 Verify `admin:stream-activity:all` contains 1 `StreamSummary` with `EventCount=3`, `LastEventSequence=3`, `StreamStatus=Active`
    - [ ] 7.4 Navigate to `/events` -- stat cards show `Recent Events > 0`
    - [ ] 7.5 Navigate to `/streams` -- no regression
    - [ ] 7.6 Confirm no Warning-level "index not found" log flood at 30-second interval

## Dev Notes

### Root Cause

The Admin UI `/events` and `/streams` pages read from `admin:stream-activity:all` via `DaprStreamQueryService.GetRecentlyActiveStreamsAsync`. **No code writes to this key.** Story 15.2 built the reader, Story 15.12 built the UI, but both assumed a writer existed. This story closes the gap.

Evidence: `rg "admin:stream-activity" src/**/*.cs` returns exactly 1 match -- the reader at `DaprStreamQueryService.cs:99`.

### Architecture Compliance

- **Rule 4**: No custom retry logic -- ETag retries are optimistic concurrency, not transport retries. DAPR resiliency handles transport failures.
- **Rule 12**: Stream activity writes are advisory -- failures logged but never block command processing.
- **Rule 10**: Register via `Add*` extension methods in `ServiceCollectionExtensions.cs`.
- **State store name**: Reuse `CommandStatusOptions.StateStoreName` (`"statestore"`) -- zero new configuration.
- **No new API endpoints, DTOs, or DAPR components.**

### Reference Implementation: DaprCommandActivityTracker

`DaprStreamActivityTracker` is a structural clone of `DaprCommandActivityTracker` (`src/Hexalith.EventStore/Commands/DaprCommandActivityTracker.cs`). Key pattern elements to replicate:

1. **Constructor**: `(DaprClient, IOptions<CommandStatusOptions>, ILogger<T>)`
2. **Constants**: `MaxEntries = 1000`, `MaxEtagRetries = 3`, single global key
3. **ETag retry loop**: `GetStateAndETagAsync` -> modify -> `TrySaveStateAsync` -> retry on ETag mismatch
4. **Identity matching**: Dedup by composite key `(TenantId, Domain, AggregateId)` using `StringComparison.OrdinalIgnoreCase`
5. **Bounded FIFO**: Order by timestamp desc, take top 1000
6. **Exception handling**: `OperationCanceledException` rethrown, all others caught and logged

**Key difference from command tracker**: Stream tracker uses cumulative counters (accumulate `EventCount`, update `LastEventSequence`) instead of simple replace.

**EventCount semantics (verified)**: `finalStatus.EventCount` is a **delta** -- the number of events produced by THIS specific command, not the cumulative aggregate total. Set from `domainResult.Events.Count` in `AggregateActor` (lines 391, 438, 473). This confirms that accumulation (`existing.EventCount += newEventsAppended`) is the correct approach.

### SubmitCommandHandler Integration Point

The tracker hooks into `SubmitCommandHandler.Handle` (lines 95-120) right alongside the existing command-tracker block. The `finalStatus` variable already contains:
- `finalStatus.EventCount` -- number of events produced by this command
- `finalStatus.Timestamp` -- when the command completed
- `finalStatus.Status` -- final command status

The stream tracker only fires when:
1. `streamActivityTracker is not null` (optional dependency)
2. `processingResult.Accepted == true` (command was not rejected at routing)
3. `(finalStatus?.EventCount ?? 0) > 0` (command actually produced events)

This ensures rejected/idempotent commands don't pollute the index.

**Domain rejection safety (verified):** `processingResult.Accepted` is `false` for domain rejections (`IRejectionEvent`), even though rejection events ARE persisted and `EventCount > 0`. The `Accepted` gate at (2) correctly excludes them. See `AggregateActor.cs:447`: `bool accepted = !domainResult.IsRejection`.

### StreamSummary Record (Already Exists)

`src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/StreamSummary.cs`:
```csharp
public record StreamSummary(
    string TenantId,        // non-null, non-empty
    string Domain,          // non-null, non-empty
    string AggregateId,     // non-null, non-empty
    long LastEventSequence,
    DateTimeOffset LastActivityUtc,
    long EventCount,
    bool HasSnapshot,
    StreamStatus StreamStatus)
```

**On accumulation** (existing entry found):
- `EventCount += newEventsAppended` (cumulative)
- `LastEventSequence = existing.LastEventSequence + newEventsAppended` (gapless per FR10)
- `LastActivityUtc = timestamp`
- `HasSnapshot` -- preserve existing value (snapshot subsystem may have set it)
- `StreamStatus = StreamStatus.Active`

**On first write** (no existing entry):
- `EventCount = newEventsAppended`
- `LastEventSequence = newEventsAppended`
- `LastActivityUtc = timestamp`
- `HasSnapshot = false`
- `StreamStatus = StreamStatus.Active`

### DaprStreamQueryService Reader Adaptation

Current code at `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs:99`:
```csharp
string indexKey = $"admin:stream-activity:{tenantId ?? "all"}";
```

Change to:
```csharp
const string indexKey = "admin:stream-activity:all";
```

Then add tenant filter in memory (same approach as `DaprCommandActivityTracker.GetRecentCommandsAsync`):
```csharp
if (!string.IsNullOrWhiteSpace(tenantId))
{
    result = result.Where(s => s.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)).ToList();
}
```

Domain filter already exists and stays as-is.

Downgrade log at line ~105-108 from `LogWarning` to `LogDebug` with updated message.

### DI Registration Pattern

In `ServiceCollectionExtensions.cs` after line ~113 (command tracker registration):
```csharp
// Stream activity tracking for admin UI Streams/Events pages (DAPR state store backed).
// Writer only — the reader lives in DaprStreamQueryService on the Admin.Server side.
_ = services.AddSingleton<DaprStreamActivityTracker>();
_ = services.AddSingleton<IStreamActivityTracker>(sp => sp.GetRequiredService<DaprStreamActivityTracker>());
```

No `IStreamActivityReader` interface needed -- the reader is `DaprStreamQueryService` which reads state directly.

### Anti-Patterns to Avoid

1. **DO NOT** create per-tenant state keys (`admin:stream-activity:{tenantId}`) -- use single global key `admin:stream-activity:all`
2. **DO NOT** add custom retry logic for transport failures -- DAPR resiliency handles those (Rule 4). The ETag retry loop is for optimistic concurrency only
3. **DO NOT** throw from `TrackAsync` -- advisory writes must not block command processing (Rule 12)
4. **DO NOT** add a second `ReadStatusAsync` call in `SubmitCommandHandler` -- share the existing `finalStatus` variable
5. **DO NOT** change the DAPR component name from `"statestore"` -- many services depend on it
6. **DO NOT** create a reader interface (`IStreamActivityReader`) -- the reader stays in `DaprStreamQueryService` on the Admin.Server side
7. **DO NOT** modify `Events.razor` or `Streams.razor` -- this is pure backend plumbing
8. **DO NOT** add any new configuration options -- reuse `CommandStatusOptions.StateStoreName`
9. **DO NOT** replace `EventCount` on update -- accumulate it (`existing.EventCount + newEventsAppended`)
10. **DO NOT** overwrite `HasSnapshot` to `false` on update -- preserve the existing value

### Previous Story Intelligence

**From Story 15.11 (persistent state store)**:
- `DaprCommandActivityTracker` is the canonical pattern. Structural clone is the correct approach.
- Review found a race condition with dual-key read/modify/write -- was fixed with single global key + optimistic concurrency. This story starts with the single-global-key pattern.
- Redis is provisioned via `AddRedis("redis").WithDataVolume()` in `HexalithEventStoreExtensions.cs`.
- `CommandStatusOptions.StateStoreName` resolves to `"statestore"`.

**From Story 15.12 (Events page)**:
- Events page calls `AdminStreamApiClient.GetRecentlyActiveStreamsAsync()` which delegates to `DaprStreamQueryService`.
- Page works correctly -- just needs data flowing into its data source.

**From Story 15.10 (data pipeline fixes)**:
- `DaprStreamQueryService` exception handling was fixed: catch blocks now rethrow (not swallow).
- Controller `IsServiceUnavailable` mapping is correct.

### Existing Test Conventions

Follow `DaprCommandActivityTrackerTests.cs`:
- `NSubstitute` for `DaprClient` mocking
- `Shouldly` for assertions
- Helper methods: `SetupGetStateAndEtag(key, value, etag)`, `SetupTrySave(key, result)`
- Test naming: `MethodName_Condition_ExpectedBehavior`
- Mock `IOptions<CommandStatusOptions>` with `StateStoreName = "test-store"`
- Mock `ILogger<T>` with `NullLogger<T>.Instance` or NSubstitute

### Known Limitations

1. **Index drift after missed writes**: If the tracker exhausts ETag retries or DAPR is unavailable, the index permanently falls behind the real aggregate state. No self-healing mechanism exists. The index is an advisory cache, not a source of truth. A future "index rebuild" story could re-scan aggregates to correct drift.
2. **Redis data loss resets index**: If the Redis container is recreated without a volume, the index resets to empty. The dashboard shows "no data" until new commands flow. This is acceptable for local dev (Redis has `.WithDataVolume()`).
3. **Null finalStatus skips tracking**: If `statusStore.ReadStatusAsync` returns null (status write failed silently in Stage 1), the stream tracker is skipped even though the command may have produced events. This is a pre-existing limitation shared with the command tracker -- both rely on the advisory status being readable.
4. **StreamStatus always Active**: The tracker unconditionally sets `StreamStatus = Active`. A future tombstoning feature will need to check and preserve non-Active status. Documented with a TODO comment in code.

### Party Mode Review Findings (Applied)

1. **[Winston/Amelia] EventCount semantics verified** -- `finalStatus.EventCount` is delta (from `domainResult.Events.Count`), confirming accumulation logic is correct. Added explicit documentation in Dev Notes.
2. **[Bob] Constructor overload clarification** -- Task 3.2 updated: extend existing 4 overloads instead of adding new ones. Prevents constructor explosion.
3. **[Murat] Bounded list test added** -- Test 6.9 (`TrackAsync_ExceedsMaxEntries_TrimsOldestByLastActivityUtc`) added to verify 1000-entry FIFO cap. Test count: 7 -> 8.
4. **[Murat] Reader regression test added** -- Task 6b added: verify `DaprStreamQueryService` always reads from global key and filters in memory. Prevents accidental revert to per-tenant keys.

### Advanced Elicitation Findings (Applied)

**Round 1** (Pre-mortem, Self-Consistency, Failure Mode, Red Team, Critique and Refine):

| # | Finding | Resolution |
|---|---------|------------|
| E1 | Domain rejection events might pass the tracking gate | **Non-issue**: `processingResult.Accepted == false` for domain rejections (verified at `AggregateActor.cs:447`). Gate is correct. Added clarifying note in Dev Notes. |
| E2 | `finalStatus == null` silently skips tracking | **Known limitation**: Pre-existing in command tracker. Documented in Known Limitations section. |
| E3 | Existing reader tests may assert old per-tenant key | **Task added**: Task 5.5 -- update existing `DaprStreamQueryServiceTests` for new key constant. |
| E4 | `StreamStatus.Active` overwrites future tombstone status | **TODO added**: Task 2.7 -- code comment for future tombstoning. |
| E5 | No self-healing after missed writes or Redis data loss | **Documented**: Known Limitations section -- advisory cache, not source of truth. |

**Round 2** (Reverse Engineering, Occam's Razor, Rubber Duck Debugging, Cross-Functional War Room, Chaos Monkey):

| # | Finding | Resolution |
|---|---------|------------|
| E6 | All 3 consumers (Events, Streams, Dashboard) share same backend method | **Confirmed**: Fixing writer + reader covers all pages. No gaps. |
| E7 | Every component serves a purpose -- no simplification possible | **Confirmed**: Story is already lean (2 new files, 3 mods, proven pattern). |
| E8 | Corrupted StreamSummary makes index permanently unreadable | **Accepted**: Very low risk. Pre-existing in command tracker. Recovery: delete key via DAPR API. |
| E9 | Process gap: "verify writer exists when building a reader" | **Retrospective item**: Flag for Epic 15 retrospective -- not a story change. |
| E10 | System resilient under all Chaos Monkey scenarios | **Confirmed**: Redis kill, rolling deploy, multi-instance, burst, corruption -- all handled. |

### Project Structure Notes

| Action | File | Project |
|--------|------|---------|
| NEW | `src/Hexalith.EventStore.Server/Commands/IStreamActivityTracker.cs` | Server |
| NEW | `src/Hexalith.EventStore/Commands/DaprStreamActivityTracker.cs` | EventStore |
| MODIFY | `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` | Server |
| MODIFY | `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` | EventStore |
| MODIFY | `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` | Admin.Server |
| NEW | `tests/Hexalith.EventStore.Client.Tests/Commands/DaprStreamActivityTrackerTests.cs` | Tests (Tier 1) |

Build: `dotnet build Hexalith.EventStore.slnx --configuration Release`
Solution: Use `Hexalith.EventStore.slnx` only, never `.sln`

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-10-events-page-empty.md`] -- Sprint change proposal with full root cause analysis
- [Source: `_bmad-output/planning-artifacts/architecture.md` Rule 4] -- No custom retry logic
- [Source: `_bmad-output/planning-artifacts/architecture.md` Rule 12] -- Advisory status writes
- [Source: `src/Hexalith.EventStore/Commands/DaprCommandActivityTracker.cs`] -- Reference implementation to clone
- [Source: `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`] -- Integration point
- [Source: `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs:99`] -- Reader to adapt
- [Source: `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/StreamSummary.cs`] -- DTO written to state store
- [Source: `tests/Hexalith.EventStore.Client.Tests/Commands/DaprCommandActivityTrackerTests.cs`] -- Test pattern reference
- [Source: `_bmad-output/implementation-artifacts/15-11-persistent-state-store-and-command-activity.md`] -- Previous story (command tracker pattern)
- [Source: `_bmad-output/implementation-artifacts/15-12-events-page-cross-stream-browser.md`] -- Previous story (Events page consumer)
- **Retrospective item (E9):** Epic 15 retro should flag: "verify writer exists when building a reader" as a dependency verification practice. Three stories (15.2, 15.11, 15.12) shipped assuming a writer existed.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Constructor overload ambiguity: resolved by casting `null` to `(IStreamActivityTracker?)null` in all backward-compatible constructor overloads
- `CommandStatusRecord.Timestamp` is non-nullable `DateTimeOffset`: removed unnecessary `?? DateTimeOffset.UtcNow` fallback in stream tracker call
- Hoisted `finalStatus` read before both tracker blocks to share the variable (Task 3.4) and added `StatusReadForTrackingFailed` log (EventId 1106) for the shared read

### Completion Notes List

- Created `IStreamActivityTracker` interface with `TrackAsync` method and Rule 12 XML documentation
- Created `DaprStreamActivityTracker` as structural clone of `DaprCommandActivityTracker` with cumulative counter semantics (`EventCount += newEventsAppended`, `LastEventSequence += newEventsAppended`)
- Integrated stream tracker into `SubmitCommandHandler` with gates: `streamActivityTracker is not null`, `processingResult.Accepted`, `finalStatus?.EventCount > 0`
- Refactored `SubmitCommandHandler` to read `finalStatus` once before both tracker blocks (shared read, no duplicate `ReadStatusAsync`)
- Registered `DaprStreamActivityTracker` + `IStreamActivityTracker` as singletons in DI
- Adapted `DaprStreamQueryService` reader: global key `admin:stream-activity:all`, in-memory tenant filter, log downgraded from Warning to Debug
- Updated existing reader tests to use new global key constant
- Added 8 new writer unit tests and 1 reader regression test
- All Tier 1 tests pass: Contracts (271), Client (321), Sample (62), Testing (67), SignalR (32), Admin.Server (499)
- Full solution build: zero warnings, zero errors
- Task 7 (manual smoke test) left unchecked — requires running AppHost with DAPR

### File List

- NEW: `src/Hexalith.EventStore.Server/Commands/IStreamActivityTracker.cs`
- NEW: `src/Hexalith.EventStore/Commands/DaprStreamActivityTracker.cs`
- MODIFIED: `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`
- MODIFIED: `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs`
- MODIFIED: `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`
- NEW: `tests/Hexalith.EventStore.Client.Tests/Commands/DaprStreamActivityTrackerTests.cs`
- MODIFIED: `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceTests.cs`

### Change Log

- 2026-04-13: Story implemented — stream activity tracker writer, SubmitCommandHandler integration, reader adaptation, 9 new tests (Date: 2026-04-13)
- 2026-04-13: Code review complete — 1 patch, 3 deferred, 9 dismissed (Date: 2026-04-13)

### Review Findings

- [x] [Review][Patch] Domain filter inconsistency: `domain is not null` should use `!string.IsNullOrWhiteSpace(domain)` to match tenant filter pattern [DaprStreamQueryService.cs:116] — fixed
- [x] [Review][Defer] Single global key scalability bottleneck under high concurrency (MaxEtagRetries=3) — deferred, pre-existing architecture decision
- [x] [Review][Defer] Constructor overload proliferation in SubmitCommandHandler — deferred, pre-existing pattern
- [x] [Review][Defer] Writer/reader state store config mismatch (CommandStatusOptions vs AdminServerOptions) — deferred, only diverges if deployment configures different store names
