# Sprint Change Proposal: Data Not Persisted Across Application Restarts

**Date:** 2026-03-30
**Triggered by:** Post-fix validation of Story 15-10 (admin-ui-data-pipeline-fixes) — data appears during a session but is lost on restart
**Scope Classification:** Minor — Direct implementation by dev team

---

## Section 1: Issue Summary

The Hexalith.EventStore local development environment does not persist any data across application restarts. The counter correctly increments during a session but resets to 0 on restart. The admin Commands page shows commands during a session but resets to empty on restart. Two independent root causes:

1. **DAPR state store configured as `state.in-memory`** in `HexalithEventStoreExtensions.cs:46` — all actor state (events, aggregates, snapshots, command status) is stored in DAPR's in-memory provider and lost when the Aspire AppHost stops
2. **`InMemoryCommandActivityTracker` uses `ConcurrentDictionary`** — admin command activity tracking bypasses DAPR entirely, using in-process memory that doesn't survive restarts

**Evidence:**
- `HexalithEventStoreExtensions.cs:46`: `AddDaprComponent("statestore", "state.in-memory")`
- `DaprCommandActivityTracker.cs:18`: `ConcurrentDictionary<string, CommandSummary> _commands = new()`
- Production DAPR config (`statestore.yaml`) correctly specifies Redis — the Aspire programmatic config overrides it
- Streams page uses persistent DAPR state store (`admin:stream-activity:{tenantId}`); Commands page uses transient `InMemoryCommandActivityTracker` — asymmetric implementation

**Discovery context:** Found during post-implementation validation after Story 15-10 fixed 4 data pipeline bugs (claim type mismatch, silent DAPR failure masking, Keycloak mapping, protocol mapper). Those fixes made data visible during a session; this proposal addresses data persistence across restarts.

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact | Detail |
|------|--------|--------|
| Epic 8 (Aspire Orchestration) | Fix needed | State store must use persistent backend (Redis) for local dev |
| Epic 15 (Admin Web UI) | Fix needed | Command activity tracking must use DAPR state store, not in-memory |
| All other epics | No impact | Unaffected |

### Story Impact

- **Story 8-1** (Aspire AppHost and DAPR topology): The `state.in-memory` configuration was a development shortcut. Needs remediation to use Redis
- **Story 15-9** (Commands page): Acceptance criteria require displaying recent commands — works within a session but fails the implicit persistence expectation. The `InMemoryCommandActivityTracker` backend needs replacement
- No existing stories need modification. A single remediation story covers both fixes

### Artifact Conflicts

| Artifact | Conflict | Action |
|----------|----------|--------|
| PRD | None | FR40 and NFR29 are unaffected — switching from in-memory to Redis is the expected DAPR backend swap |
| Architecture | None | D1 already specifies Redis for local dev. Rule 12 (advisory tracking) maintained |
| UX Design | None | No UI changes needed |
| Epics | None | No story modifications needed |

### Technical Impact

- **Fix A (state store):** 1 file modified (`HexalithEventStoreExtensions.cs`), 1 Redis container added to Aspire topology
- **Fix B (command tracker):** 4 files modified (`ICommandActivityTracker.cs`, `DaprCommandActivityTracker.cs`, `AdminCommandsQueryController.cs`, `ServiceCollectionExtensions.cs`)
- **No API contract changes** — same endpoints, same DTOs
- **No infrastructure changes** beyond adding Redis container to Aspire AppHost
- **CI impact:** Tier 2+ tests already use `dapr init` which provisions Redis — no change needed

---

## Section 3: Recommended Approach

**Selected path:** Direct Adjustment — 2 targeted fixes within existing epic scope.

**Rationale:**
- Both issues are infrastructure configuration bugs, not design flaws
- Fix A is a single-line change (in-memory → Redis) plus Redis container provisioning
- Fix B follows the established stream activity tracking pattern (`admin:stream-activity:{tenantId}`)
- No architectural changes needed — the architecture already expects Redis for local dev
- All patterns are established: Aspire Redis provisioning is standard, DAPR state store access via `DaprClient` is used throughout

**Effort estimate:** Low (single story, ~2 hours implementation)
**Risk level:** Low (no API changes, no schema changes, follows established patterns)
**Timeline impact:** None — fits within current sprint

---

## Section 4: Detailed Change Proposals

### Fix A: Switch Aspire State Store from In-Memory to Redis

**File:** `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`
**Section:** State store configuration (lines 41-47)

**OLD:**
```csharp
// Use AddDaprComponent instead of AddDaprStateStore so that WithMetadata
// actually propagates into the generated YAML. AddDaprStateStore spawns a
// separate in-memory provider process whose lifecycle hook ignores metadata.
// actorStateStore is required for Dapr actor state management.
IResourceBuilder<IDaprComponentResource> stateStore = builder
    .AddDaprComponent("statestore", "state.in-memory")
    .WithMetadata("actorStateStore", "true");
```

**NEW:**
```csharp
// Provision a Redis container for persistent state storage during local
// development. DAPR actors, events, and projections are persisted to Redis
// so data survives application restarts.
IResourceBuilder<RedisResource> redis = builder.AddRedis("redis");
IResourceBuilder<IDaprComponentResource> stateStore = builder
    .AddDaprComponent("statestore", "state.redis")
    .WithMetadata("actorStateStore", "true")
    .WithMetadata("redisHost", redis.Resource.GetEndpoint("tcp"));
```

**Justification:** The architecture (D1) and production DAPR config already specify Redis. Using `state.in-memory` was a development shortcut that breaks the developer experience promise (FR40). Redis in Aspire is standard and auto-managed.

**Note:** The exact Aspire Redis wiring API needs verification against `Aspire.Hosting.Redis` and `CommunityToolkit.Aspire.Hosting.Dapr` — the concept is correct but integration syntax may need adjustment.

---

### Fix B: Replace InMemoryCommandActivityTracker with DAPR-Backed Storage

**Files affected:**

#### B1. Add read method to interface

**File:** `src/Hexalith.EventStore.Server/Commands/ICommandActivityTracker.cs`

**OLD:**
```csharp
public interface ICommandActivityTracker
{
    Task TrackAsync(
        string tenantId, string domain, string aggregateId,
        string correlationId, string commandType, CommandStatus status,
        DateTimeOffset timestamp, int? eventCount, string? failureReason,
        CancellationToken ct = default);
}
```

**NEW:**
```csharp
public interface ICommandActivityTracker
{
    Task TrackAsync(
        string tenantId, string domain, string aggregateId,
        string correlationId, string commandType, CommandStatus status,
        DateTimeOffset timestamp, int? eventCount, string? failureReason,
        CancellationToken ct = default);

    Task<PagedResult<CommandSummary>> GetRecentCommandsAsync(
        string? tenantId, string? status, string? commandType,
        int count = 1000, CancellationToken ct = default);
}
```

#### B2. Replace in-memory implementation with DAPR state store

**File:** `src/Hexalith.EventStore/Commands/DaprCommandActivityTracker.cs`

**OLD:** `InMemoryCommandActivityTracker` using `ConcurrentDictionary`

**NEW:** `DaprCommandActivityTracker` using `DaprClient.GetStateAsync/SaveStateAsync` with key pattern `admin:command-activity:{tenantId}`, storing bounded `List<CommandSummary>` (max 1000, FIFO eviction). Follows the same pattern as `admin:stream-activity:{tenantId}` in `DaprStreamQueryService`. `TrackAsync` catches and logs failures without blocking (Rule 12).

#### B3. Update controller to use interface

**File:** `src/Hexalith.EventStore/Controllers/AdminCommandsQueryController.cs`

**OLD:**
```csharp
public class AdminCommandsQueryController(
    InMemoryCommandActivityTracker activityTracker) : ControllerBase
```

**NEW:**
```csharp
public class AdminCommandsQueryController(
    ICommandActivityTracker activityTracker) : ControllerBase
```

Controller calls `activityTracker.GetRecentCommandsAsync(...)` (now async).

#### B4. Update DI registration

**File:** `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs`

**OLD:**
```csharp
_ = services.AddSingleton<InMemoryCommandActivityTracker>();
_ = services.AddSingleton<ICommandActivityTracker>(sp => sp.GetRequiredService<InMemoryCommandActivityTracker>());
```

**NEW:**
```csharp
_ = services.AddSingleton<ICommandActivityTracker, DaprCommandActivityTracker>();
```

**Justification:** Aligns command activity tracking with stream activity tracking — both use DAPR state store. Eliminates the asymmetry that caused Commands page data loss on restart.

---

## Section 5: Implementation Handoff

**Scope:** Minor — direct implementation by dev team.

**Handoff:**

| Role | Responsibility |
|------|---------------|
| Developer | Implement Fix A (Redis state store) and Fix B (DAPR command tracker) |
| Developer | Run Tier 1 tests to verify no regressions |
| Developer | Manual smoke test: run AppHost, increment counter, restart AppHost, verify counter retains value and Commands page retains data |

**Success Criteria:**

1. Counter state persists across application restarts
2. Admin Commands page shows commands after application restart
3. Admin Streams page continues to work (no regression)
4. `dapr init` + Redis container starts automatically with Aspire AppHost
5. Tier 1 tests pass
6. Dev mode auth continues to work

**New Story ID:** `15-11-persistent-state-store-and-command-activity`

**Dependencies:** None — both fixes are self-contained within existing code patterns
