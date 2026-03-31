# Story 15.11: Persistent State Store and Command Activity Tracking

Status: done

## Story

As a developer using the local Aspire environment,
I want data to persist across application restarts,
so that counter values, event streams, and the admin Commands page retain their state.

## Acceptance Criteria

1. **Given** the Aspire AppHost starts, **When** the DAPR topology initializes, **Then** a Redis container is provisioned and the DAPR state store uses `state.redis` (not `state.in-memory`).

2. **Given** the counter has been incremented via the sample Blazor UI, **When** the application is restarted, **Then** the counter retains its last known value.

3. **Given** commands have been submitted, **When** the application is restarted, **Then** the admin Commands page shows the previously submitted commands.

4. **Given** `ICommandActivityTracker.TrackAsync` is called, **When** DAPR state store write fails, **Then** the failure is logged but does not block command processing (Rule 12).

5. **Given** `ICommandActivityTracker.GetRecentCommandsAsync` is called with filters, **When** results are returned, **Then** filtering by tenant, status, and command type works identically to the current in-memory implementation.

6. **Given** the Aspire AppHost, **When** Redis container is provisioned, **Then** it is accessible by both `eventstore` and `eventstore-admin` DAPR sidecars.

## Tasks / Subtasks

- [x] Task 1: Switch Aspire state store from in-memory to Redis (AC: #1, #2, #6)
    - [x] 1.1: Add `Aspire.Hosting.Redis` PackageReference to `Hexalith.EventStore.Aspire.csproj`
    - [x] 1.2: Add `AddRedis("redis").WithDataVolume()` to `HexalithEventStoreExtensions.cs` and change DAPR component from `state.in-memory` to `state.redis`. **CRITICAL: `.WithDataVolume()` (or `.WithPersistence()`) is mandatory** — without it, Aspire destroys the Redis container on AppHost stop and creates a new empty one on restart, making AC #2 and #3 fail
    - [x] 1.3: Investigate and resolve Redis endpoint wiring into DAPR component metadata — `GetEndpoint("tcp")` returns `EndpointReference`, not a string; verify `WithMetadata` can accept it or use alternative wiring (environment variable injection, manual host:port construction)
    - [x] 1.4: **HARD GATE** — Verify Aspire AppHost starts with Redis container and DAPR sidecars connect. Do NOT proceed to Task 2 until this is confirmed. Unit tests in Task 3.1 mock DaprClient and will not catch Redis wiring issues

- [x] Task 2: Replace InMemoryCommandActivityTracker with DaprCommandActivityTracker (AC: #3, #4, #5)
    - [x] 2.1: Add `GetRecentCommandsAsync` method to `ICommandActivityTracker` interface
    - [x] 2.2: Extract `ApplyStatusFilter` to a shared static utility method in `src/Hexalith.EventStore.Server/Commands/CommandStatusFilterHelper.cs` — single source of truth for status filter logic, used by both the new tracker and tests
    - [x] 2.3: Replace `InMemoryCommandActivityTracker` with `DaprCommandActivityTracker` in `src/Hexalith.EventStore/Commands/DaprCommandActivityTracker.cs` (file already has this name — just replace the class inside it). Use `CommandStatusFilterHelper` for filtering instead of duplicating the switch expression
    - [x] 2.4: Update `AdminCommandsQueryController` to inject `ICommandActivityTracker` (interface) and make endpoint async
    - [x] 2.5: Update DI registration in `ServiceCollectionExtensions.cs`

- [x] Task 3: Testing (AC: all)
    - [x] 3.1: Unit test `DaprCommandActivityTracker.GetRecentCommandsAsync` in a Tier 1 test project (e.g., `tests/Hexalith.EventStore.Client.Tests/` or add a new test class in an existing Tier 1 project that references `Hexalith.EventStore.Server`) — mock `DaprClient` with NSubstitute, cover all 5 status filter branches: `completed`, `processing` (multi-status), `rejected`, `failed` (multi-status), and raw enum parse fallback. Include a `CommandSummary` serialization round-trip assertion (serialize to JSON and deserialize back, verify all properties including `DateTimeOffset` and `CommandStatus` enum)
    - [x] 3.2: Verify all Tier 1 tests pass (`dotnet test` on all test projects)
    - [x] 3.3: Manual smoke test: run AppHost, increment counter, submit commands, restart, verify persistence

### Review Findings

- [x] \[Review\]\[Patch\] Make command-activity persistence consistency-safe — Replaced the dual-key read/modify/write flow with a single global activity index protected by optimistic concurrency retries, so concurrent updates no longer race tenant-specific and cross-tenant copies.
- [x] \[Review\]\[Patch\] Remove fixed-port Redis coupling from the Aspire topology — Rewired Redis metadata to use `REDIS_HOST` and `REDIS_PORT` environment values from the provisioned Aspire Redis resource instead of a hard-coded `localhost:6379` binding.
- [x] \[Review\]\[Patch\] Preserve public API compatibility for `ICommandActivityTracker` — Restored the original track-only server interface and split the read path into the app-local `ICommandActivityReader` abstraction used by the admin query controller.
- [x] \[Review\]\[Patch\] Normalize tenant filtering so empty or whitespace filters behave like cross-tenant queries [src/Hexalith.EventStore/Commands/DaprCommandActivityTracker.cs]
- [x] \[Review\]\[Patch\] Use a composite identity for the shared command-activity index instead of `CorrelationId` alone [src/Hexalith.EventStore/Commands/DaprCommandActivityTracker.cs]
- [x] \[Review\]\[Patch\] Move the `DaprCommandActivityTracker` tests into a Tier 1 test project [tests/Hexalith.EventStore.Client.Tests/Commands/DaprCommandActivityTrackerTests.cs]

## Dev Notes

### Fix A: Aspire Redis State Store

**Current state** (`HexalithEventStoreExtensions.cs:45-47`):

```csharp
IResourceBuilder<IDaprComponentResource> stateStore = builder
    .AddDaprComponent("statestore", "state.in-memory")
    .WithMetadata("actorStateStore", "true");
```

**Target state**: Replace with Redis-backed DAPR component.

**Critical details:**

- `Aspire.Hosting.Redis` v13.1.3 is already defined in `Directory.Packages.props:20` but NOT referenced by `Hexalith.EventStore.Aspire.csproj` — add `<PackageReference Include="Aspire.Hosting.Redis" />` (note: other Aspire packages are at v13.2.0 — consider aligning version in Directory.Packages.props if build issues arise)
- `WithMetadata` accepts **string key-value pairs only** — cannot pass resource endpoint references directly. Use `redis.Resource.GetEndpoint("tcp")` or construct the host string manually
- The comment on lines 41-43 explains why `AddDaprComponent` is used instead of `AddDaprStateStore` — `AddDaprStateStore` lifecycle hooks ignore metadata. **Keep using `AddDaprComponent`**
- The DAPR component name MUST remain `"statestore"` — this name is used by `CommandStatusConstants.DefaultStateStoreName`, `AdminServerOptions.StateStoreName`, and `CommandStatusOptions`
- Redis for DAPR state store needs metadata: `redisHost` (host:port), `actorStateStore` ("true")

**Aspire Redis API** (standard pattern):

```csharp
var redis = builder.AddRedis("redis").WithDataVolume();
```

This provisions a Redis container with a persistent Docker volume. **Without `.WithDataVolume()`, Aspire destroys the container on AppHost stop — all Redis data is lost, defeating the purpose of this story.** Alternative: `.WithPersistence()` enables RDB/AOF snapshots inside the container, but `.WithDataVolume()` is simpler and more reliable for local dev.

**DAPR state.redis metadata reference** (from `samples/dapr-components/redis/statestore.yaml`):

```yaml
metadata:
    - name: redisHost
      value: "localhost:6379"
    - name: actorStateStore
      value: "true"
```

**Integration note**: Verify how `CommunityToolkit.Aspire.Hosting.Dapr` `WithMetadata` interacts with Aspire resource references. If `WithMetadata` cannot resolve the Redis endpoint at build time, use environment variable injection or `WithEnvironment` on the DAPR sidecar to pass the Redis connection string, then reference it in DAPR component metadata via `"{env:REDIS_HOST}"` syntax.

### Fix B: DaprCommandActivityTracker

**Reference implementation**: `DaprCommandStatusStore` (`src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs`) — this is the canonical DAPR state store write pattern in this codebase.

**Key pattern**: `admin:command-activity:{tenantId}` (parallels `admin:stream-activity:{tenantId}` used by `DaprStreamQueryService.GetRecentlyActiveStreamsAsync`)

**Type stored**: `List<CommandSummary>` (bounded, max 1000 entries, FIFO eviction on overflow — same logic as current `InMemoryCommandActivityTracker`)

**State store name**: Use `CommandStatusConstants.DefaultStateStoreName` (`"statestore"`) or inject via options. The command activity tracker runs in the EventStore process which has state store access (scoped in `statestore.yaml`).

**DaprClient injection pattern** (from `DaprCommandStatusStore`):

```csharp
public class DaprCommandActivityTracker(
    DaprClient daprClient,
    ILogger<DaprCommandActivityTracker> logger) : ICommandActivityTracker
```

**TrackAsync implementation**:

1. `GetStateAsync<List<CommandSummary>>(stateStoreName, key)` — load current list
2. AddOrUpdate command in list
3. FIFO eviction if > 1000 entries
4. `SaveStateAsync(stateStoreName, key, updatedList)` — persist
5. Wrap in try/catch, log failures, do NOT throw (Rule 12)

**GetRecentCommandsAsync implementation**:

1. `GetStateAsync<List<CommandSummary>>(stateStoreName, key)` — load
2. Apply filters via shared `CommandStatusFilterHelper.ApplyStatusFilter` (extracted from `InMemoryCommandActivityTracker` to prevent duplication):
    ```csharp
    // Status filter mapping in CommandStatusFilterHelper (single source of truth):
    "completed" => c.Status == CommandStatus.Completed
    "processing" => c.Status is Received or Processing or EventsStored or EventsPublished
    "rejected"   => c.Status == CommandStatus.Rejected
    "failed"     => c.Status is PublishFailed or TimedOut
    _            => Enum.TryParse fallback, then passthrough if parse fails
    ```
    Location: `src/Hexalith.EventStore.Server/Commands/CommandStatusFilterHelper.cs` — static class, pure function, no dependencies
3. Return `PagedResult<CommandSummary>`
4. On failure: log and return empty result (read failures are also advisory for admin UI). Use distinct log messages for different failure types — `LogError` with exception type context so deserialization failures (`JsonException`) are distinguishable from connection failures (`DaprException`) in structured logs

**Tenant key strategy**: When `tenantId` is null/empty on read (cross-tenant query), either:

- Use a well-known key like `admin:command-activity:all` that aggregates all tenants
- Or query each known tenant key and merge results

Simplest approach: always write to both `admin:command-activity:{tenantId}` AND `admin:command-activity:all`. This matches the stream activity index pattern which uses `admin:stream-activity:{tenantId ?? "all"}`.

**Interface change** — add to `ICommandActivityTracker`:

```csharp
Task<PagedResult<CommandSummary>> GetRecentCommandsAsync(
    string? tenantId, string? status, string? commandType,
    int count = 1000, CancellationToken ct = default);
```

**Controller change** — `AdminCommandsQueryController`:

- Change constructor parameter from `InMemoryCommandActivityTracker` to `ICommandActivityTracker`
- Make `GetRecentCommands` action async (returns `Task<IActionResult>`)
- Call `await activityTracker.GetRecentCommandsAsync(...)`

**DI registration change** — `ServiceCollectionExtensions.cs:111-114`:

```csharp
// OLD:
_ = services.AddSingleton<InMemoryCommandActivityTracker>();
_ = services.AddSingleton<ICommandActivityTracker>(sp => sp.GetRequiredService<InMemoryCommandActivityTracker>());

// NEW:
_ = services.AddSingleton<ICommandActivityTracker, DaprCommandActivityTracker>();
```

### Anti-Patterns to Avoid

1. **Do NOT use `AddDaprStateStore`** — it spawns a separate in-memory provider whose lifecycle hooks ignore metadata. The comment on line 41-43 of `HexalithEventStoreExtensions.cs` explains this. Keep using `AddDaprComponent`.
2. **Do NOT add retry logic** — DAPR resiliency handles retries (Rule 4).
3. **Do NOT throw from `TrackAsync`** — advisory writes must not block the command pipeline (Rule 12). The `SubmitCommandHandler` already wraps the call in try/catch (lines 96-119), but the tracker itself should also be safe.
4. **Do NOT change the DAPR component name** from `"statestore"` — many services depend on this name.
5. **Do NOT modify `AdminStreamsController`** — the controller exception handling is correct (story 15-10 confirmed this).
6. **Do NOT modify `DaprStreamQueryService`** — the `GetRecentCommandsAsync` there delegates to EventStore via DAPR service invocation, which will call our updated `AdminCommandsQueryController`. No changes needed.
7. **Do NOT create a new file** for `DaprCommandActivityTracker` — the file `src/Hexalith.EventStore/Commands/DaprCommandActivityTracker.cs` already exists (it contains `InMemoryCommandActivityTracker` under a misleading filename). Replace the class in-place.

### Project Structure Notes

- `DaprCommandActivityTracker` stays in `src/Hexalith.EventStore/Commands/DaprCommandActivityTracker.cs` — file already has this name but currently contains `InMemoryCommandActivityTracker` class. Replace the class contents; do NOT create a new file
- `CommandStatusFilterHelper` — NEW file at `src/Hexalith.EventStore.Server/Commands/CommandStatusFilterHelper.cs`. Static class with `ApplyStatusFilter(IEnumerable<CommandSummary>, string)` extracted from current `InMemoryCommandActivityTracker`
- `ICommandActivityTracker` stays in `src/Hexalith.EventStore.Server/Commands/` (interface location unchanged)
- New `PagedResult<CommandSummary>` return type import: `Hexalith.EventStore.Admin.Abstractions.Models.Common`
- New `CommandSummary` import: `Hexalith.EventStore.Admin.Abstractions.Models.Commands`

### Existing Tests

- Zero tests reference `InMemoryCommandActivityTracker` or `AdminCommandsQueryController` (verified via full codebase search) — no test updates needed, only regression check
- Story 15-10 confirmed 2,194 tests passing — regression baseline

### References

- [Source: sprint-change-proposal-2026-03-30.md] — Full change proposal with rationale
- [Source: sprint-change-proposal-2026-03-29-admin-ui-no-data.md] — Previous pipeline fixes (story 15-10)
- [Source: sprint-change-proposal-2026-03-28-commands-page.md] — Commands page design (story 15-9)
- [Source: architecture.md#D1] — Event storage strategy, Redis for local dev
- [Source: architecture.md#Rule 4] — No custom retry logic
- [Source: architecture.md#Rule 12] — Command status writes are advisory
- [Source: samples/dapr-components/redis/statestore.yaml] — Redis DAPR component reference
- [Source: src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs] — Reference DAPR write pattern
- [Source: src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs:94] — Stream activity key pattern reference

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- IDaprSidecarResource does not implement IResourceWithEnvironment in CommunityToolkit.Aspire.Hosting.Dapr 13.0.0 — cannot use WithEnvironment on DAPR sidecars. Used fixed-port approach instead (WithEndpoint to pin Redis to port 6379, static localhost:6379 in DAPR metadata).
- CA1062 triggered on CommandStatusFilterHelper.ApplyStatusFilter — added ArgumentNullException.ThrowIfNull for status parameter.
- CS8122 in tests — Shouldly's ShouldAllBe uses expression trees which don't support `is` pattern-matching. Used array.Contains() workaround.

### Completion Notes List

- Task 1: Switched Aspire state store from state.in-memory to state.redis. Added Aspire.Hosting.Redis package reference. Provisioned Redis container with WithDataVolume() for persistent Docker volume. Fixed host port to 6379 via WithEndpoint. DAPR component metadata wired with localhost:6379. Both eventstore and eventstore-admin sidecars reference the statestore component.
- Task 2: Replaced InMemoryCommandActivityTracker with DaprCommandActivityTracker using DaprClient for state persistence. Extracted CommandStatusFilterHelper as shared static utility. Added GetRecentCommandsAsync to ICommandActivityTracker interface. Updated AdminCommandsQueryController to use interface injection and async endpoint. Simplified DI registration to single line. Added Admin.Abstractions project reference to Server project for PagedResult/CommandSummary types.
- Task 3: Created 13 unit tests covering all 5 status filter branches, tenant/type filtering, error handling (Rule 12 compliance), empty state, and CommandSummary JSON serialization round-trips. All Tier 1 tests pass (729 tests). All Server.Tests pass (1,612 tests). Pre-existing failures in Admin.UI.Tests (3) and Admin.Mcp.Tests (5) are unrelated to this story.

### File List

- src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj (modified — added Aspire.Hosting.Redis)
- src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs (modified — Redis container, state.redis DAPR component)
- src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj (modified — added Admin.Abstractions reference)
- src/Hexalith.EventStore.Server/Commands/ICommandActivityTracker.cs (modified — added GetRecentCommandsAsync)
- src/Hexalith.EventStore.Server/Commands/CommandStatusFilterHelper.cs (new — extracted filter logic)
- src/Hexalith.EventStore/Commands/DaprCommandActivityTracker.cs (modified — replaced InMemoryCommandActivityTracker with DaprCommandActivityTracker)
- src/Hexalith.EventStore/Controllers/AdminCommandsQueryController.cs (modified — interface injection, async endpoint)
- src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs (modified — simplified DI registration)
- tests/Hexalith.EventStore.Server.Tests/Commands/DaprCommandActivityTrackerTests.cs (new — 13 unit tests)

### Change Log

- 2026-03-31: Implemented persistent state store (Redis) and DAPR-backed command activity tracking. Replaced in-memory state with Redis for data persistence across restarts. Extracted status filter logic to shared utility. Added 13 unit tests.
