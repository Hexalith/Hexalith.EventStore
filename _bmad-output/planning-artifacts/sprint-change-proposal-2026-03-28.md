# Sprint Change Proposal — Convention-Based Message Routing as Default

**Date:** 2026-03-28
**Triggered by:** Jerome (architectural direction)
**Scope:** Minor (direct implementation by dev team)

---

## Section 1: Issue Summary

Message routing in EventStore should be convention-based by default: a domain's messages automatically route to the DAPR service whose AppId matches the domain name (e.g., `tenants` domain -> AppId `tenants`, method `process`). Configuration overrides via static registrations or DAPR config store should remain available for complex scenarios, but zero configuration should be the default for simple implementations.

**Current state:** The convention-based routing already exists as a fallback in `DomainServiceResolver.cs` (lines 114-128). However:
1. `DomainServiceOptions.ConfigStoreName` defaults to `"configstore"`, causing a DAPR config store lookup on every resolution — even when no config store exists. This produces error logs and adds latency before falling through to convention.
2. Documentation (FR22, D7) positions convention as a "fallback" rather than the primary approach.
3. Five unit tests in `DomainServiceResolverTests.cs` expect `null` for unregistered services, contradicting the convention-based fallback that always returns a result.

**Evidence:** `DomainServiceResolver.cs:59-112` — config store lookup runs on every call, catches exceptions, logs errors, then falls through to convention at line 122.

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Status | Impact Level | Detail |
|------|--------|-------------|--------|
| Epic 2 | done | Code fix | `DomainServiceResolver.cs` + `DomainServiceOptions.cs` — make config store opt-in |
| Epic 8 | done | AC wording | Story 8.2 AC mentions "configured via DAPR" — should include convention |
| Epic 11 | done | AC wording | Story 11.4 AC references `EventStore:DomainServices` — should note convention-based discovery works without explicit registration |
| Others | — | None | No impact |

No new epics. No epic reordering. No epic removal.

### Artifact Conflicts

**PRD (prd.md):**
- FR22: Reword from "explicit DAPR configuration or convention-based assembly scanning" to convention-first with optional config override

**Architecture (architecture.md):**
- D7: Update service discovery description — convention is default, config store is opt-in override
- Cross-cutting #8: Acknowledge convention-first behavior
- DAPR Building Block table: Note Configuration building block is optional for domain service routing

**Code:**
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceOptions.cs` — `ConfigStoreName` default from `"configstore"` to `null`
- `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` — Skip config store when `ConfigStoreName` is null/empty
- `tests/.../DomainServiceResolverTests.cs` — Fix 5 stale tests, add convention-specific tests

### Technical Impact

**Zero breaking changes.** Existing deployments that explicitly set `ConfigStoreName` in their configuration continue to work identically. Only deployments relying on the implicit `"configstore"` default would see changed behavior (config store no longer queried by default), which is the desired outcome.

---

## Section 3: Recommended Approach

**Direct Adjustment** — modify existing code and documentation within the current epic structure.

**Rationale:**
- Convention-based routing already works in code; we're promoting it from fallback to default
- The code change is ~15 lines across 2 files + test updates
- No architectural redesign needed
- Aligns with FR42 (zero-config quickstart) which is already a stated goal
- Fixes stale tests that contradict the actual implementation

**Effort:** Low (< 1 story point)
**Risk:** Low (additive change, no breaking paths)
**Timeline impact:** None

---

## Section 4: Detailed Change Proposals

### Change 1: `DomainServiceOptions.cs` — Make ConfigStore opt-in

**File:** `src/Hexalith.EventStore.Server/DomainServices/DomainServiceOptions.cs`
**Section:** ConfigStoreName property

OLD:
```csharp
/// <summary>The DAPR config store name. Default: "configstore".</summary>
public string ConfigStoreName { get; init; } = "configstore";
```

NEW:
```csharp
/// <summary>
/// The DAPR config store name for domain service registration overrides.
/// Default: null (convention-based routing only). Set to a config store name
/// (e.g., "configstore") to enable config store lookups that override conventions.
/// </summary>
public string? ConfigStoreName { get; init; }
```

**Rationale:** Convention-based routing (AppId = domain name, MethodName = "process") is the default. Config store is opt-in for complex routing scenarios (e.g., tenant-specific routing to different services).

---

### Change 2: `DomainServiceResolver.cs` — Skip config store when not configured

**File:** `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs`
**Section:** Config store lookup block (lines 59-112)

OLD:
```csharp
// Try DAPR config store lookup if available.
try {
    logger.LogDebug(
        "Resolving domain service: ConfigKey={ConfigKey}, ConfigStore={ConfigStore}, Version={Version}",
        configKey,
        options.Value.ConfigStoreName,
        version);

    GetConfigurationResponse configResponse = await daprClient
        .GetConfiguration(
            options.Value.ConfigStoreName,
            [configKey],
            cancellationToken: cancellationToken)
        .ConfigureAwait(false);
    // ... (rest of config store block)
```

NEW:
```csharp
// Try DAPR config store lookup only if explicitly configured (opt-in override).
// When ConfigStoreName is null/empty, convention-based routing is used directly.
if (!string.IsNullOrWhiteSpace(options.Value.ConfigStoreName)) {
    try {
        logger.LogDebug(
            "Resolving domain service: ConfigKey={ConfigKey}, ConfigStore={ConfigStore}, Version={Version}",
            configKey,
            options.Value.ConfigStoreName,
            version);

        GetConfigurationResponse configResponse = await daprClient
            .GetConfiguration(
                options.Value.ConfigStoreName,
                [configKey],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        // ... (rest of config store block, unchanged)
    }
    // ... (catch block unchanged)
}
```

**Rationale:** Eliminates unnecessary DAPR config store calls, error logs, and latency in zero-config deployments. Config store lookups only happen when `ConfigStoreName` is explicitly set.

---

### Change 3: `DomainServiceResolverTests.cs` — Fix stale tests, add convention tests

**File:** `tests/.../DomainServiceResolverTests.cs`

**Tests to update (expect convention result instead of null):**
- `ResolveAsync_UnregisteredService_ReturnsNull` → rename to `ResolveAsync_UnregisteredService_ReturnsConventionRouting`, assert `AppId == "orders"`, `MethodName == "process"`
- `ResolveAsync_EmptyConfigValue_ReturnsNull` → same pattern
- `ResolveAsync_JsonNull_ReturnsNull` → same pattern
- `ResolveAsync_NoCache_EachCallQueriesConfigStore` → update `result1.ShouldBeNull()` assertion

**Tests to update (config store no longer queried by default):**
- `ResolveAsync_UsesCorrectConfigKeyWithVersion` → must configure `ConfigStoreName` in options
- `ResolveAsync_NoCaching_GetConfigurationCalledEveryInvocation` → must configure `ConfigStoreName`
- All tests that call `ConfigureConfigStore` and expect it to be used → must set `ConfigStoreName = "configstore"` in options

**New tests to add:**
- `ResolveAsync_NoConfigStore_ReturnsConventionRouting` — verifies AppId=domain, MethodName="process" with default (null) ConfigStoreName
- `ResolveAsync_StaticRegistration_OverridesConvention` — verifies static registration takes precedence over convention
- `ResolveAsync_ConfigStore_OverridesConvention` — verifies config store (when configured) takes precedence
- `ResolveAsync_NullConfigStoreName_SkipsConfigStoreLookup` — verifies DaprClient.GetConfiguration is never called when ConfigStoreName is null

---

### Change 4: PRD FR22 — Convention-first wording

**File:** `_bmad-output/planning-artifacts/prd.md`

OLD:
```
FR22: A domain service developer can register their domain service via explicit DAPR configuration or convention-based assembly scanning
```

NEW:
```
FR22: A domain service developer's domain service is automatically routed by convention (AppId matches the domain name, method "process") with zero configuration. Routing can be overridden via static registrations or DAPR config store for complex scenarios (e.g., per-tenant routing to different services)
```

---

### Change 5: Architecture D7 — Convention-first service discovery

**File:** `_bmad-output/planning-artifacts/architecture.md`

OLD:
```
#### D7: Domain Service Invocation -- DAPR Service Invocation

- **Mechanism:** Actor calls domain service via `DaprClient.InvokeMethodAsync<TRequest, TResponse>`
- **Service discovery:** Domain service endpoint resolved from DAPR config store registration (`tenant:domain:version -> appId + method`)
```

NEW:
```
#### D7: Domain Service Invocation -- DAPR Service Invocation

- **Mechanism:** Actor calls domain service via `DaprClient.InvokeMethodAsync<TRequest, TResponse>`
- **Service discovery:** Convention-based by default — AppId = domain name, MethodName = "process" (zero configuration). Override hierarchy: (1) static registrations (appsettings.json, for local dev/test), (2) DAPR config store (opt-in via `ConfigStoreName`, for per-tenant routing in complex scenarios), (3) convention fallback
```

---

### Change 6: Architecture — DAPR Configuration building block optional

**File:** `_bmad-output/planning-artifacts/architecture.md`

OLD:
```
| Configuration           | Domain service registration + admin tool settings | Tenant + domain + version -> service endpoint mapping; observability tool URLs for admin deep links |
```

NEW:
```
| Configuration           | Admin tool settings + optional domain service routing overrides | Observability tool URLs for admin deep links; domain service routing overrides (opt-in, convention-based routing is the default) |
```

---

### Change 7: Epics — Story 11.4 AC wording

**File:** `_bmad-output/planning-artifacts/epics.md`

OLD (Story 11.4 AC):
```
**Given** a domain service registered in `EventStore:DomainServices` that also exposes a `/project` endpoint,
**When** the system starts,
**Then** it is automatically wired for projection building via convention-based discovery.
```

NEW:
```
**Given** a domain service reachable via convention (AppId = domain name) or explicit registration that also exposes a `/project` endpoint,
**When** the system starts,
**Then** it is automatically wired for projection building via convention-based discovery.
```

---

## Section 5: Implementation Handoff

**Scope classification:** Minor — direct implementation by dev team.

**Handoff:** Development team (Jerome)

**Implementation sequence:**
1. Update `DomainServiceOptions.cs` — make `ConfigStoreName` nullable, default null
2. Update `DomainServiceResolver.cs` — guard config store block with null check
3. Update `DomainServiceResolverTests.cs` — fix stale tests, add convention tests
4. Run Tier 1+2 tests to validate
5. Update PRD FR22 wording
6. Update Architecture D7 + DAPR building block table
7. Update Epics Story 11.4 AC

**Success criteria:**
- `DomainServiceResolver` returns convention-based routing (AppId=domain, MethodName="process") by default without any config store calls
- Config store lookups only happen when `ConfigStoreName` is explicitly set
- All existing tests pass (with updates)
- New convention-specific tests pass
- Zero breaking changes for deployments that explicitly configure `ConfigStoreName`
