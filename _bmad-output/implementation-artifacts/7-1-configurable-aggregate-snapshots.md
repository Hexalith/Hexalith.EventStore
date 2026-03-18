# Story 7.1: Configurable Aggregate Snapshots

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**BLOCKING: Epics 1-6 must be completed first. All are done.**

Verify these files/classes exist before starting:
- `src/Hexalith.EventStore.Server/Events/ISnapshotManager.cs` — interface with `ShouldCreateSnapshotAsync`, `CreateSnapshotAsync`, `LoadSnapshotAsync`
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` — interval-based creation, advisory (Rule #12), payload protection
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs` — immutable record with SequenceNumber, State, CreatedAt, Domain, AggregateId, TenantId
- `src/Hexalith.EventStore.Server/Configuration/SnapshotOptions.cs` — DefaultInterval=100, DomainIntervals dictionary, Validate() with MinimumInterval=10
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` — snapshot-aware rehydration with parallel reads (MaxConcurrentStateReads=32)
- `src/Hexalith.EventStore.Server/Events/RehydrationResult.cs` — separates SnapshotState from Events, tracks LastSnapshotSequence
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` — Step 3 (snapshot-first rehydration), Step 5b (snapshot creation after event persistence)
- `src/Hexalith.EventStore.Contracts/Commands/DomainServiceCurrentState.cs` — snapshot-aware state payload to domain services
- `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs` — client-side reflection-based state reconstruction
- `src/Hexalith.EventStore.Testing/Fakes/FakeSnapshotManager.cs` — in-memory test double
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs` (24 tests)
- `tests/Hexalith.EventStore.Server.Tests/Events/EventStreamReaderTests.cs` (23 tests)
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotCreationIntegrationTests.cs` (6 tests)
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotRehydrationTests.cs` (4 tests)
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotRecordTests.cs` (3 tests)
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotIntegrationTests.cs` (1 test)

Run `dotnet test tests/Hexalith.EventStore.Server.Tests/` to confirm all existing 61 snapshot-related tests pass before beginning.

## Story

As a **platform developer**,
I want aggregate state snapshots created at configurable intervals with per-tenant-domain overrides,
So that state rehydration remains fast regardless of total event count.

## Acceptance Criteria

1. **Snapshot threshold triggers snapshot creation** — Given an aggregate with events exceeding the snapshot threshold (default: every 100 events), When EventStore processes a command that crosses the threshold, Then a snapshot of the current aggregate state is created (FR13) And the snapshot is stored in DAPR actor state atomically with events (D1).

2. **Per-tenant-domain snapshot interval configuration** — Given a configurable snapshot interval, When configured per tenant-domain pair (e.g., `"acme:payments"` = 50), Then the tenant-domain interval overrides both the domain-level and system default (Rule 15) And snapshot configuration is mandatory — there is always a threshold.

3. **Three-tier interval resolution** — Given interval configuration at three levels, When determining the snapshot interval for a specific aggregate, Then the resolution order is: tenant-domain override > domain override > system default And all intervals must be >= MinimumInterval (10).

4. **Snapshot + tail events produces identical state to full replay** — Given an aggregate with a snapshot and subsequent events, When state is rehydrated, Then reconstruction from snapshot + tail events produces identical state to full replay (FR14) And rehydration time remains bounded regardless of total event count (NFR19).

5. **Snapshot-first rehydration flow** — Given an aggregate with an existing snapshot, When the AggregateActor rehydrates state (Step 3), Then the SnapshotManager loads the snapshot first And EventStreamReader reads only tail events from snapshot.SequenceNumber + 1 And the resulting state is passed to the domain service as DomainServiceCurrentState.

6. **Corrupt snapshot graceful degradation** — Given a snapshot that fails deserialization, When LoadSnapshotAsync encounters the failure, Then the corrupt snapshot is deleted And the system falls back to full event replay And a warning is logged with correlationId (never logging state content per Rule #5).

7. **Advisory snapshot creation** — Given snapshot creation is advisory (Rule #12), When CreateSnapshotAsync fails, Then command processing is NOT blocked And a warning is logged.

8. **SnapshotOptions validation** — Given SnapshotOptions with configured intervals, When Validate() is called, Then all intervals (DefaultInterval, DomainIntervals, TenantDomainIntervals) must be >= MinimumInterval (10) And invalid intervals throw InvalidOperationException.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and audit current implementation (AC: ALL) **— GATE: Complete Task 0 fully before starting Tasks 1-5. The audit identifies the exact scope of changes needed.**
  - [x] 0.1 Run `dotnet test tests/Hexalith.EventStore.Server.Tests/` — all existing tests must pass
  - [x] 0.2 Review `SnapshotManager.cs` — verify interval checking, advisory failure handling, payload protection, structured logging
  - [x] 0.3 Review `SnapshotOptions.cs` — verify DefaultInterval=100, DomainIntervals, MinimumInterval=10, Validate()
  - [x] 0.4 Review `EventStreamReader.cs` — verify snapshot-first rehydration, tail-only reads, parallel loading
  - [x] 0.5 Review `AggregateActor.cs` Steps 3 and 5b — verify snapshot load → rehydrate → persist → snapshot creation flow
  - [x] 0.6 Review `ISnapshotManager.cs` — verify current interface contract
  - [x] 0.7 Review `FakeSnapshotManager.cs` — verify test double alignment with interface
  - [x] 0.8 Grep for all `ShouldCreateSnapshotAsync` call sites across the entire codebase — identify every caller that needs the new `tenantId` parameter. Search for BOTH direct calls AND NSubstitute mock setups (`.ShouldCreateSnapshotAsync(Arg.`). Expected callers: `AggregateActor`, `FakeSnapshotManager`, ~24 `SnapshotManagerTests` direct calls, and potentially `AggregateActorTests` or other integration tests that construct/mock `ISnapshotManager`. **NSubstitute mock setups that don't match the new 4-parameter signature will silently return `default(bool)` = `false` — tests pass but cover nothing.**
  - [x] 0.9 Verify that `SnapshotOptions.Validate()` is called during startup — check `ServiceCollectionExtensions.cs` or DI registration for an explicit `Validate()` call or `ValidateOnStart()` configuration. If not called, this is a gap to fix in this story.
  - [x] 0.10 Create audit report: map each AC to existing implementation, identify all gaps

- [x] Task 1: Add per-tenant-domain interval support to SnapshotOptions (AC: #2, #3, #8)
  - [x] 1.1 Add `TenantDomainIntervals` property to `SnapshotOptions`: `Dictionary<string, int>` with key format `"tenantId:domain"` (lowercase, colon-separated matching AggregateIdentity key convention)
  - [x] 1.2 Update `Validate()` to also validate all `TenantDomainIntervals` entries against MinimumInterval
  - [x] 1.3 If Task 0.9 found that `Validate()` is NOT called at startup, add `ValidateOnStart()` or explicit `Validate()` call in DI registration (`ServiceCollectionExtensions.cs`) to ensure invalid intervals are rejected at startup, not at first command
  - [x] 1.4 Add XML doc comments explaining the three-tier resolution order

- [x] Task 2: Update ISnapshotManager.ShouldCreateSnapshotAsync to accept tenantId (AC: #2, #3)
  - [x] 2.1 Add `string tenantId` parameter to `ISnapshotManager.ShouldCreateSnapshotAsync` signature (before `domain` parameter for consistency with AggregateIdentity property order). **STRING SWAP WARNING:** Both `tenantId` and `domain` are `string` — the compiler will NOT catch callers passing them in the wrong order. Any call site not updated will silently interpret the old `domain` value as `tenantId`. You MUST update ALL call sites atomically in the same commit to prevent silent semantic errors.
  - [x] 2.2 Update `SnapshotManager.ShouldCreateSnapshotAsync` implementation: rename `GetIntervalForDomain` to `GetInterval` and implement three-tier lookup: `TenantDomainIntervals[$"{tenantId}:{domain}"]` → `DomainIntervals[domain]` → `DefaultInterval`. Use `ToLowerInvariant()` on the constructed lookup key defensively (config keys may not match `AggregateIdentity` lowercase normalization)
  - [x] 2.3 Update `SnapshotManager.ShouldCreateSnapshotAsync` parameter validation: add `ArgumentException.ThrowIfNullOrWhiteSpace(tenantId)`
  - [x] 2.4 Update ALL existing `ShouldCreateSnapshotAsync` call sites to pass `tenantId` parameter — this includes ~24 direct calls in `SnapshotManagerTests.cs`, any NSubstitute mock setups (e.g., `snapshotManager.ShouldCreateSnapshotAsync(Arg.Any<string>(), ...)` which need a 4th `Arg.Any<string>()` or they silently stop matching), and any other callers identified in Task 0.8. Use a representative tenant ID like `"test-tenant"` for existing tests that aren't specifically testing tenant-domain overrides

- [x] Task 3: Update all production callers to pass tenantId (AC: #2)
  - [x] 3.1 Update the `ShouldCreateSnapshotAsync` call in AggregateActor Step 5b (currently line ~371): pass `command.TenantId` as the new `tenantId` parameter alongside `command.Domain`
  - [x] 3.2 Verify no other production callers exist beyond AggregateActor (confirm via Task 0.8 grep results)

- [x] Task 4: Update FakeSnapshotManager test double (AC: #2, #3)
  - [x] 4.1 Add `TenantDomainIntervals` property: `Dictionary<string, int>` matching SnapshotOptions
  - [x] 4.2 Update `ShouldCreateSnapshotAsync` to accept `tenantId` parameter and implement three-tier lookup
  - [x] 4.3 Update `ShouldCreateCalls` record type to include `TenantId` field

- [x] Task 5: Add tests for per-tenant-domain interval override (AC: #2, #3, #8)
  - [x] 5.1 Test: ShouldCreateSnapshot with tenant-domain override — uses tenant-domain interval, not domain or default
  - [x] 5.2 Test: ShouldCreateSnapshot with tenant-domain override AND domain override — tenant-domain wins
  - [x] 5.3 Test: ShouldCreateSnapshot without tenant-domain override but with domain override — domain override wins over default
  - [x] 5.4 Test: ShouldCreateSnapshot without any override — uses DefaultInterval
  - [x] 5.5 Test: SnapshotOptions.Validate() rejects TenantDomainIntervals entry below MinimumInterval
  - [x] 5.6 Test: SnapshotOptions.Validate() accepts valid TenantDomainIntervals entries
  - [x] 5.7 Test: ShouldCreateSnapshot with null tenantId — throws ArgumentException
  - [x] 5.8 Test: ShouldCreateSnapshot with empty/whitespace tenantId — throws ArgumentException

- [x] Task 6: Verify existing snapshot + rehydration coverage (AC: #1, #4, #5, #6, #7)
  - [x] 6.1 Verify SnapshotManagerTests cover: interval threshold trigger, advisory failure handling, corrupt snapshot deletion, payload protection
  - [x] 6.2 Verify EventStreamReaderTests cover: snapshot-first flow, tail-only reads, full replay fallback, parallel reads
  - [x] 6.3 Verify SnapshotRehydrationTests cover: snapshot + tail = full replay consistency (FR14)
  - [x] 6.4 Verify SnapshotCreationIntegrationTests cover: atomic commit with events, snapshot at correct intervals
  - [x] 6.5 Identify any gap tests needed and add them
  - [x] 6.6 Verify all structured logging follows Rule #5 (never log state content) and Rule #9 (correlationId)

- [x] Task 7: Verify all tests pass (AC: ALL)
  - [x] 7.1 Run `dotnet test tests/Hexalith.EventStore.Server.Tests/` — confirm no regressions
  - [x] 7.2 Run full Tier 1 test suite (`dotnet test` on Contracts.Tests, Client.Tests, Sample.Tests, Testing.Tests) — confirm no regressions
  - [x] 7.3 Verify all 61+ snapshot-related tests pass plus new tests

## Dev Notes

### Story Context

This is the **first story in Epic 7: Snapshots, Rate Limiting & Performance**. The core snapshot infrastructure was built during the old epic structure (Stories 3.9 and 3.10) and is already comprehensively implemented and tested. This story is primarily a **verification + gap-fix story** — the main new work is adding per-tenant-domain interval configuration (AC #2, #3) which the current implementation lacks.

**What previous work already built (to VERIFY, not replicate):**

1. **SnapshotManager** (`src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`):
   - Interval-based snapshot creation with per-domain overrides
   - Advisory failure handling (Rule #12: failures never block command processing)
   - Payload protection via `IEventPayloadProtectionService`
   - Structured logging with correlationId, never logging state content (Rule #5)
   - Stages snapshot in actor state (caller commits atomically)

2. **SnapshotOptions** (`src/Hexalith.EventStore.Server/Configuration/SnapshotOptions.cs`):
   - `DefaultInterval` = 100 events (Rule #15)
   - `DomainIntervals` dictionary for per-domain overrides
   - `MinimumInterval` = 10 (prevents performance degradation)
   - `Validate()` method rejecting intervals below minimum

3. **EventStreamReader** (`src/Hexalith.EventStore.Server/Events/EventStreamReader.cs`):
   - Snapshot-aware rehydration: loads snapshot first, reads only tail events
   - Parallel reads with `MaxConcurrentStateReads=32` for performance (NFR6)
   - Returns `RehydrationResult` separating snapshot state from events
   - Source-generated `[LoggerMessage]` structured logging

4. **AggregateActor** (`src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`):
   - Step 3: Loads snapshot via `snapshotManager.LoadSnapshotAsync()`, passes to `EventStreamReader.RehydrateAsync(identity, existingSnapshot)`
   - Step 5b: After event persistence, checks `ShouldCreateSnapshotAsync`, creates snapshot if threshold crossed
   - Atomic commit: events + snapshot + checkpoint in single `SaveStateAsync()` (D1)

5. **DomainServiceCurrentState** (`src/Hexalith.EventStore.Contracts/Commands/DomainServiceCurrentState.cs`):
   - Snapshot-aware payload passed to domain services with `SnapshotState`, `Events`, `LastSnapshotSequence`, `CurrentSequence`

6. **Client-side rehydration** (`src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`):
   - Reflection-based state reconstruction from snapshot + tail events
   - Cached Apply method lookup via `ConcurrentDictionary`
   - Handles `DomainServiceCurrentState`, `JsonElement`, and collection inputs

7. **Test infrastructure** (61 tests across 6 files):
   - `FakeSnapshotManager` in-memory test double
   - `SnapshotManagerTests` (24 tests): interval trigger, advisory failure, corrupt snapshot, payload protection
   - `EventStreamReaderTests` (23 tests): snapshot-first, tail-only reads, full replay, parallel reads
   - `SnapshotCreationIntegrationTests` (6 tests): atomic commit, correct intervals
   - `SnapshotRehydrationTests` (4 tests): snapshot + tail = full replay consistency
   - `SnapshotRecordTests` (3 tests): immutable record structure
   - `SnapshotIntegrationTests` (1 test): end-to-end flow

**What this story adds (NEW):**

1. **Per-tenant-domain interval configuration** — `TenantDomainIntervals` dictionary in `SnapshotOptions` with key format `"tenantId:domain"` and three-tier resolution: tenant-domain > domain > default
2. **Updated `ShouldCreateSnapshotAsync` signature** — adds `tenantId` parameter to `ISnapshotManager` interface
3. **Updated `FakeSnapshotManager`** — adds `TenantDomainIntervals` support matching production implementation
4. **New tests** — per-tenant-domain override behavior, three-tier resolution, validation of tenant-domain intervals
5. **Verification** — audit existing 61 tests against all ACs, fix any gaps found

### Architecture Compliance

**FR13:** The system can create aggregate state snapshots at configurable intervals (default: every 100 events). **Covered by:** `SnapshotManager.ShouldCreateSnapshotAsync` + `CreateSnapshotAsync`, triggered from `AggregateActor` Step 5b.

**FR14:** Reconstruction from snapshot + tail events produces identical state to full replay. **Covered by:** `EventStreamReader.RehydrateAsync` with snapshot parameter, `DomainProcessorStateRehydrator`, and `SnapshotRehydrationTests`.

**NFR19:** Event stream growth bounded by snapshot strategy — rehydration time constant. **Covered by:** Snapshot-first flow reads only tail events; `MaxConcurrentStateReads=32` parallel loading.

**Rule #5 (SEC-5):** Never log payload data. All snapshot logging uses identity + metadata only, never state content.

**Rule #12:** Snapshot creation is advisory — failures never block command processing. `CreateSnapshotAsync` catches exceptions and logs warnings.

**Rule #15:** Snapshot configuration is mandatory — every domain must have a threshold. `SnapshotOptions.Validate()` enforces `MinimumInterval >= 10`.

**D1:** Atomic writes — events + snapshot + checkpoint committed in single `SaveStateAsync()` batch.

### DAPR State Store Key Convention

Snapshot storage follows the composite key pattern from `AggregateIdentity.SnapshotKey`:
```
{tenantId}:{domain}:{aggregateId}:snapshot
```
Example: `acme:payments:order-123:snapshot`

### Three-Tier Interval Resolution (NEW)

```
1. TenantDomainIntervals["tenantId:domain"]  →  if found, use this
2. DomainIntervals["domain"]                  →  if found, use this
3. DefaultInterval (100)                       →  always available
```

Example configuration:
```json
{
  "EventStore:Snapshots": {
    "DefaultInterval": 100,
    "DomainIntervals": {
      "payments": 50,
      "audit": 200
    },
    "TenantDomainIntervals": {
      "high-volume-tenant:payments": 25,
      "archive-tenant:audit": 500
    }
  }
}
```

### Critical Design Decisions

- **Key format for TenantDomainIntervals:** Use `"tenantId:domain"` (colon-separated, all lowercase) matching the `AggregateIdentity` key convention. The colon separator is consistent with all other key patterns in the system. **Defensive normalization:** The `GetInterval` method must `ToLowerInvariant()` the constructed lookup key because config values from DAPR config store may not enforce lowercase — `AggregateIdentity` normalizes on construction but config entries are user-authored. **Colon constraint:** Tenant IDs cannot contain colons — `AggregateIdentity` validation enforces this (FR15, FR28) to prevent structural key ambiguity. A key like `"org:team:payments"` would be ambiguous. This constraint is already enforced at the identity layer, not the snapshot layer. **Silent misconfiguration:** Config keys with the wrong separator (e.g., `"acme-payments"` dash instead of `"acme:payments"` colon) will silently fall through to domain/default with no warning. Consider adding a startup validation warning in `Validate()` if any `TenantDomainIntervals` key does not contain exactly one colon — but this is optional for v1.

- **Interface change is acceptable:** Adding `tenantId` to `ISnapshotManager.ShouldCreateSnapshotAsync` is a breaking change to the internal interface. This is acceptable because `ISnapshotManager` is internal to the Server package and not part of any NuGet public API.

- **FakeSnapshotManager must stay in sync:** The test double must implement the same three-tier resolution to prevent test/production divergence.

- **No `IOptions<SnapshotOptions>` hot-reload:** `SnapshotOptions` is loaded once at startup via `IOptions<SnapshotOptions>` (not `IOptionsMonitor`). Dynamic updates require service restart. This is intentional — snapshot interval changes have performance implications and should be deliberate. Per-tenant dynamic configuration via DAPR config store (NFR20) would require `IOptionsMonitor` and is out of scope for this story.

- **Snapshot content is `DomainServiceCurrentState` (DO NOT "optimize" this):** The `currentState` snapshotted in AggregateActor Step 5b is a `DomainServiceCurrentState` wrapper containing `SnapshotState` (the domain's reconstructed state) + `Events` (tail events since last snapshot). This means snapshot size grows linearly between snapshot intervals — at interval 100, the snapshot near the threshold may contain ~99 embedded event envelopes. This is **by design**: the snapshot is a self-contained state package that `DomainProcessorStateRehydrator` knows how to reconstruct from. Do NOT attempt to strip events from the snapshot to "save space" — the client-side rehydrator depends on receiving the full `DomainServiceCurrentState` shape. Large tenant-domain intervals (e.g., 500) will create proportionally larger snapshot state store entries — this is an acceptable trade-off documented in the architecture.

- **Hot-path allocation consideration for `GetInterval`:** `ShouldCreateSnapshotAsync` is called on every command that produces events. The three-tier lookup constructs a `$"{tenantId}:{domain}"` string + `ToLowerInvariant()` per call. For v1 this is acceptable. If profiling shows contention, a future optimization would pre-compute a flattened `Dictionary<(string TenantId, string Domain), int>` in the constructor since `SnapshotOptions` is loaded once at startup. Do NOT pre-optimize in this story — keep the implementation simple and readable.

### Existing Patterns to Follow

**Primary constructor pattern (all classes in this project):**
```csharp
public class SnapshotManager(
    IOptions<SnapshotOptions> options,
    ILogger<SnapshotManager> logger,
    IEventPayloadProtectionService payloadProtectionService) : ISnapshotManager {
```

**Guard clause pattern:**
```csharp
ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
ArgumentException.ThrowIfNullOrWhiteSpace(domain);
```

**Structured logging pattern (Rule #9 — correlationId on all logs, Rule #5 — never log state content):**
```csharp
logger.LogInformation(
    "Snapshot staged: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, ...",
    correlationId, identity.TenantId, identity.Domain, ...);
```

**Test assertion pattern (Shouldly):**
```csharp
result.ShouldBeTrue();
options.DomainIntervals["payments"].ShouldBe(50);
```

**Configuration record pattern:**
```csharp
public record SnapshotOptions {
    public int DefaultInterval { get; init; } = 100;
    public Dictionary<string, int> DomainIntervals { get; init; } = [];
}
```

### Mandatory Coding Conventions

- Primary constructors for all classes (existing convention)
- `ConfigureAwait(false)` on all async calls (CA2007)
- `ArgumentNullException.ThrowIfNull()` / `ArgumentException.ThrowIfNullOrWhiteSpace()` for guard clauses
- File-scoped namespaces (`namespace X.Y.Z;`)
- Allman brace style (new line before opening brace)
- `_camelCase` for private fields
- NSubstitute for mocking, Shouldly for assertions
- `TreatWarningsAsErrors` is enabled — zero warnings allowed
- `sealed` modifier on classes not designed for inheritance
- XML doc comments on all public members

### Project Structure Notes

**Files to modify:**
- `src/Hexalith.EventStore.Server/Configuration/SnapshotOptions.cs` — add `TenantDomainIntervals`, update `Validate()`
- `src/Hexalith.EventStore.Server/Events/ISnapshotManager.cs` — add `tenantId` parameter to `ShouldCreateSnapshotAsync`
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` — update `ShouldCreateSnapshotAsync` and `GetInterval` for three-tier lookup
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` — pass `command.TenantId` to `ShouldCreateSnapshotAsync`
- `src/Hexalith.EventStore.Testing/Fakes/FakeSnapshotManager.cs` — add `TenantDomainIntervals`, update `ShouldCreateSnapshotAsync`
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs` — add per-tenant-domain override tests

**No new files expected** unless audit reveals coverage gaps requiring new test files.

**No new NuGet dependencies needed.**

### Previous Story Intelligence

**From Story 6.3 (Health & Readiness Endpoints) — most recently completed:**
- Verification story pattern: audit existing implementation → map ACs → identify gaps → fix and test
- Added 15 gap-fix tests covering edge cases not found in original implementation
- Fixed `WriteHealthCheckJsonResponse` error handling for non-serializable data
- Pre-existing test failure in `ErrorReferenceEndpointTests` (unrelated, slug 'not-implemented') — ignore if still present
- All Tier 1 tests passed (659 total)

**From Story 6.2 (Structured Logging Verification):**
- `[LoggerMessage]` source-generated methods convention for hot-path loggers (already used in `EventStreamReader`)
- NSubstitute logger mock pattern for log verification

**From Story 6.1 (OpenTelemetry Tracing):**
- `"Hexalith.EventStore"` and `"Hexalith.EventStore.CommandApi"` ActivitySources registered in ServiceDefaults
- Tracing spans wrap state rehydration (relevant to snapshot performance)

### Git Intelligence

Recent commits show Epic 6 completion:
- `2933980` Merge PR #112 — Story 6.3 Health and Readiness Endpoints
- `ad9aa77` feat: Complete Story 6.3 Health and Readiness Endpoints
- `54edca0` Merge PR #111 — Story 6.2 Structured Logging verification
- `5b81788` feat: Complete Story 6.2 Structured Logging verification
- `fc4b532` Merge PR #110 — Story 6.1 OpenTelemetry Tracing verification

Branch pattern: `feat/story-7-1-configurable-aggregate-snapshots`

### Testing Requirements

**Existing test coverage (61 tests, expected to all pass):**

| Test File | Tests | Coverage |
|-----------|-------|----------|
| SnapshotManagerTests | 24 | Interval trigger, domain override, advisory failure, corrupt snapshot deletion, payload protection, logging, guard clauses |
| EventStreamReaderTests | 23 | Snapshot-first, tail-only reads, full replay, parallel reads, missing event handling, metadata validation |
| SnapshotCreationIntegrationTests | 6 | Atomic commit, snapshot at intervals, snapshot overwrites previous |
| SnapshotRehydrationTests | 4 | Snapshot + tail = full replay, tail events match, lastSnapshotSequence flow |
| SnapshotRecordTests | 3 | Immutable record structure |
| SnapshotIntegrationTests | 1 | End-to-end flow |

**New tests to add (Task 5):**
- ShouldCreateSnapshot: tenant-domain override wins over domain override
- ShouldCreateSnapshot: tenant-domain override wins when only tenant-domain exists
- ShouldCreateSnapshot: domain override wins when no tenant-domain override
- ShouldCreateSnapshot: default used when no overrides
- SnapshotOptions.Validate: rejects invalid TenantDomainIntervals entry
- SnapshotOptions.Validate: accepts valid TenantDomainIntervals entries
- ShouldCreateSnapshot: null tenantId throws ArgumentException
- ShouldCreateSnapshot: empty/whitespace tenantId throws ArgumentException

### Definition of Done

- All 8 ACs verified against implementation with zero unmapped criteria
- Per-tenant-domain interval configuration added and tested
- All 61 existing snapshot tests pass with zero regressions
- 8+ new per-tenant-domain and guard clause tests pass
- Full Tier 1 test suite passes
- Story file updated with completion notes and file list

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 7, Story 7.1: Configurable Aggregate Snapshots]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Rule #15 — mandatory snapshot configuration]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1 — atomic state store writes]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure — SnapshotManager.cs, SnapshotOptions.cs]
- [Source: _bmad-output/planning-artifacts/prd.md#FR13 — configurable snapshot intervals]
- [Source: _bmad-output/planning-artifacts/prd.md#FR14 — snapshot + tail = full replay]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR4 — actor activation latency < 50ms]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR19 — bounded rehydration time]
- [Source: src/Hexalith.EventStore.Server/Events/ISnapshotManager.cs]
- [Source: src/Hexalith.EventStore.Server/Events/SnapshotManager.cs]
- [Source: src/Hexalith.EventStore.Server/Configuration/SnapshotOptions.cs]
- [Source: src/Hexalith.EventStore.Server/Events/EventStreamReader.cs]
- [Source: src/Hexalith.EventStore.Server/Events/RehydrationResult.cs]
- [Source: src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs]
- [Source: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#Step 3 and Step 5b]
- [Source: src/Hexalith.EventStore.Contracts/Commands/DomainServiceCurrentState.cs]
- [Source: src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs]
- [Source: src/Hexalith.EventStore.Testing/Fakes/FakeSnapshotManager.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Events/EventStreamReaderTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Events/SnapshotRehydrationTests.cs]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Build: 0 warnings, 0 errors (Release configuration)
- Server.Tests: 1504 passed, 1 pre-existing known failure (ErrorReferenceEndpointTests slug 'not-implemented')
- Tier 1 total: 267 (Contracts) + 293 (Client) + 32 (Sample) + 67 (Testing) = 659 passed
- 8 new per-tenant-domain tests added, all pass

### Completion Notes List

- **Task 0:** Full audit confirmed AC #1, #4, #5, #6, #7 already covered. Identified gaps: AC #2/#3 (no tenant-domain override) and AC #8 (TenantDomainIntervals validation). Validate() already called at startup via ServiceCollectionExtensions.cs:56-59. Identified all ShouldCreateSnapshotAsync call sites including NSubstitute mock setups.
- **Task 1:** Added `TenantDomainIntervals` property to `SnapshotOptions` with XML docs explaining three-tier resolution. Updated `Validate()` to reject entries below MinimumInterval. No need to add ValidateOnStart (already present).
- **Task 2:** Added `tenantId` parameter to `ISnapshotManager.ShouldCreateSnapshotAsync`. Renamed `GetIntervalForDomain` to `GetInterval` with three-tier lookup: tenant-domain > domain > default. Uses `ToLowerInvariant()` on constructed key. Added `ArgumentException.ThrowIfNullOrWhiteSpace(tenantId)` guard. Updated ALL call sites atomically: 12 SnapshotManagerTests, 5 AggregateActorTests (2 NSubstitute setups + 3 assertions), 5 SnapshotCreationIntegrationTests.
- **Task 3:** Updated AggregateActor Step 5b (line 371) to pass `command.TenantId` as new first parameter. No other production callers found.
- **Task 4:** Updated FakeSnapshotManager: added `TenantDomainIntervals` property, updated `ShouldCreateSnapshotAsync` with tenantId parameter and three-tier lookup, updated `ShouldCreateCalls` tuple to include `TenantId` field.
- **Task 5:** Added 8 new tests: tenant-domain override wins, tenant-domain wins over domain, domain override without tenant-domain, default when no overrides, TenantDomainIntervals validation rejection, TenantDomainIntervals validation acceptance, null tenantId throws, empty tenantId throws.
- **Task 6:** Verified existing 61 snapshot tests cover AC #1 (interval trigger), AC #4 (snapshot + tail = full replay), AC #5 (snapshot-first flow), AC #6 (corrupt snapshot degradation), AC #7 (advisory creation). Structured logging follows Rule #5 and #9. No gap tests needed.
- **Task 7:** Full regression suite passes. All 1504 Server.Tests pass (1 pre-existing known failure). All 659 Tier 1 tests pass.

### Change Log

- 2026-03-18: Added per-tenant-domain interval configuration (AC #2, #3, #8). Added tenantId parameter to ISnapshotManager.ShouldCreateSnapshotAsync. Updated all call sites. Added 8 new tests.

### File List

- `src/Hexalith.EventStore.Server/Configuration/SnapshotOptions.cs` — Added TenantDomainIntervals property and validation
- `src/Hexalith.EventStore.Server/Events/ISnapshotManager.cs` — Added tenantId parameter to ShouldCreateSnapshotAsync
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` — Updated ShouldCreateSnapshotAsync with tenantId, renamed GetIntervalForDomain to GetInterval with three-tier lookup
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` — Pass command.TenantId to ShouldCreateSnapshotAsync
- `src/Hexalith.EventStore.Testing/Fakes/FakeSnapshotManager.cs` — Added TenantDomainIntervals, updated ShouldCreateSnapshotAsync signature and lookup
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs` — Updated all call sites with tenantId, added 8 new per-tenant-domain tests
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTests.cs` — Updated 5 ShouldCreateSnapshotAsync call sites with tenantId
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotCreationIntegrationTests.cs` — Updated 5 ShouldCreateSnapshotAsync call sites with tenantId
