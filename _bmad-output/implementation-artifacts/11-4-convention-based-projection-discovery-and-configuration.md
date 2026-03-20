# Story 11.4: Convention-Based Projection Discovery & Configuration

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform developer,
I want domain services that expose a `/project` endpoint to be automatically wired for projections,
so that no explicit projection registration is needed beyond existing domain service setup.

## Acceptance Criteria

1. **Given** a domain service registered in `EventStore:DomainServices` that also exposes a `/project` endpoint,
   **When** the system starts,
   **Then** it is automatically wired for projection building via convention-based discovery.

2. **Given** projection configuration,
   **When** `EventStore:Projections:DefaultRefreshIntervalMs` is set,
   **Then** 0 = immediate (fire-and-forget), >0 = polling interval in ms.

3. **Given** per-domain override,
   **When** `EventStore:Projections:Domains:{domain}:RefreshIntervalMs` is configured,
   **Then** it overrides the system default for that domain.

4. **Given** tenant isolation,
   **When** projection updates execute,
   **Then** they are scoped to `tenant:domain:aggregateId`
   **And** no cross-tenant event leakage occurs in triggering or delivery.

## Definition of Done

- All 4 ACs verified against actual code
- Build: `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
- Tier 1 + Tier 2 tests pass, no regressions
- Branch: `feat/story-11-4-convention-based-projection-discovery-and-configuration`

## Tasks / Subtasks

- [x] Task 1: Gate fire-and-forget trigger by refresh interval in `ProjectionUpdateOrchestrator` (AC: 2, 3)
  - [x] Inject `IOptions<ProjectionOptions>` into `ProjectionUpdateOrchestrator` constructor
  - [x] At start of `UpdateProjectionAsync`, check `projectionOptions.Value.GetRefreshIntervalMs(identity.Domain)`
  - [x] If `== 0`: proceed with existing immediate trigger logic
  - [x] If `> 0`: log at Debug level that polling mode is configured (deferred), return without invoking domain service
  - [x] Add LoggerMessage for polling mode deferral (EventId 1117)
  - [x] NOTE: AC 2 is satisfied by configuration gating. The system respects `RefreshIntervalMs > 0` by not firing the immediate trigger. The actual background poller (which would actively poll at this interval) is deferred to a future epic. The hosted service logs a WARNING at startup making this transparent to operators.
- [x] Task 2: Create `ProjectionDiscoveryHostedService` startup validator (AC: 1, 2, 3)
  - [x] Create `src/Hexalith.EventStore.Server/Projections/ProjectionDiscoveryHostedService.cs`
  - [x] Implement `IHostedService` (one-shot startup pattern, like `CommandApiAuthorizationStartupValidator`)
  - [x] In `StartAsync`: read `DomainServiceOptions.Registrations` and `ProjectionOptions`
  - [x] Log startup summary: each registered domain's projection mode (immediate/polling/not-configured)
  - [x] Validate: warn on `ProjectionOptions.Domains` entries that reference domains with no domain service registration
- [x] Task 3: Register hosted service in DI (AC: 1)
  - [x] Add `services.AddHostedService<ProjectionDiscoveryHostedService>()` in `AddEventStoreServer`
- [x] Task 4: Create tests (AC: 1, 2, 3, 4)
  - [x] Test refresh interval gating in `ProjectionUpdateOrchestrator` (immediate fires, polling skips)
  - [x] Test `ProjectionDiscoveryHostedService` startup logging
  - [x] Test tenant isolation (verify orchestrator scopes by tenant identity, no cross-tenant calls)
  - [x] Update existing orchestrator test constructor calls if needed
- [x] Task 5: Full build and test verification
  - [x] `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
  - [x] All Tier 1 tests pass
  - [x] Tier 2 Server tests pass

## Dev Notes

### Architecture Context: Server-Managed Projection Builder (Mode B)

This story adds the **convention-based discovery and configuration gating** -- the fourth layer of Epic 11. The full pipeline is now:

```
Events persisted -> EventPublisher.PublishEventsAsync (pub/sub)
  -> Task.Run fire-and-forget:
     -> ProjectionUpdateOrchestrator.UpdateProjectionAsync(identity)
        -> [NEW] Check ProjectionOptions.GetRefreshIntervalMs(domain)
           -> If 0: proceed with immediate trigger (existing Story 11-3 logic)
           -> If >0: log "polling mode deferred", return
        -> DomainServiceResolver.ResolveAsync (convention: uses existing DomainServices registration)
        -> AggregateActor.GetEventsAsync(0)
        -> Map EventEnvelope[] to ProjectionEventDto[]
        -> DaprClient.InvokeMethodAsync("appId", "project", ProjectionRequest)
        -> EventReplayProjectionActor.UpdateProjectionAsync(ProjectionState)
        -> ETag regenerated -> SignalR broadcast -> UI refreshes
```

**Convention-based discovery** means: every domain service registered in `EventStore:DomainServices` is automatically a projection source. No separate "projection registration" exists. The `DomainServiceResolver` resolution (used for command routing) is reused for projection routing. If a domain service exposes a `/project` endpoint, the projection builder invokes it. If not, the invocation fails gracefully (logged, skipped -- existing behavior from Story 11-3).

**Story 11-1 provided:** `ProjectionEventDto`, `ProjectionRequest`, `ProjectionResponse` contracts and `AggregateActor.GetEventsAsync`.
**Story 11-2 provided:** `EventReplayProjectionActor`, `IProjectionWriteActor`, `ProjectionState` DTO, actor registration.
**Story 11-3 provided:** `IProjectionUpdateOrchestrator`, `ProjectionUpdateOrchestrator`, `NoOpProjectionUpdateOrchestrator`, `ProjectionOptions`, fire-and-forget wiring in `EventPublisher`.
**This story adds:** Refresh interval gating in orchestrator, startup-time projection discovery/logging, configuration validation.
**Story 11-5 builds:** Counter sample `/project` endpoint.

### Task 1: Gate Fire-and-Forget by Refresh Interval

**File:** `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`

**Why gate in the orchestrator (not EventPublisher):** Adding `IOptions<ProjectionOptions>` to `EventPublisher` would change its constructor signature, requiring updates to 7+ test files. The orchestrator is the correct architectural boundary -- it decides whether to proceed with the projection update. The `Task.Run` overhead for polling-mode domains (starts a task that immediately returns) is negligible.

**Change 1:** Add `IOptions<ProjectionOptions> projectionOptions` to constructor:
```csharp
public partial class ProjectionUpdateOrchestrator(
    IActorProxyFactory actorProxyFactory,
    DaprClient daprClient,
    IDomainServiceResolver resolver,
    IOptions<ProjectionOptions> projectionOptions,
    ILogger<ProjectionUpdateOrchestrator> logger) : IProjectionUpdateOrchestrator
```

**Change 2:** At the start of `UpdateProjectionAsync`, after `ArgumentNullException.ThrowIfNull(identity)` and before `try {`, add refresh interval check:
```csharp
int refreshIntervalMs = projectionOptions.Value.GetRefreshIntervalMs(identity.Domain);
if (refreshIntervalMs > 0)
{
    Log.PollingModeDeferred(logger, identity.TenantId, identity.Domain, refreshIntervalMs);
    return;
}
```

**CRITICAL:** This check goes BEFORE the try/catch block -- it is a normal control flow return, not an error. The try/catch wraps the actual projection update logic (fire-and-forget safe), but this early return is intentional configuration-based gating.

**Change 3:** Add required using:
```csharp
using Hexalith.EventStore.Server.Configuration;
using Microsoft.Extensions.Options;
```

**Change 4:** Add new LoggerMessage (EventId 1117, continuing 1110-1116 range):
```csharp
[LoggerMessage(
    EventId = 1117,
    Level = LogLevel.Debug,
    Message = "Projection polling mode configured (RefreshIntervalMs={RefreshIntervalMs}), skipping immediate trigger: TenantId={TenantId}, Domain={Domain}, Stage=PollingModeDeferred")]
public static partial void PollingModeDeferred(ILogger logger, string tenantId, string domain, int refreshIntervalMs);
```

**Note:** Log level is **Debug** (not Information) because this fires on every command for polling-mode domains. Information-level would flood logs in high-throughput scenarios. The startup summary log (Task 2) provides the Information-level visibility.

### Task 2: ProjectionDiscoveryHostedService

**File:** `src/Hexalith.EventStore.Server/Projections/ProjectionDiscoveryHostedService.cs`

**Namespace:** `Hexalith.EventStore.Server.Projections`

**Pattern:** Follows `CommandApiAuthorizationStartupValidator` (one-shot `IHostedService`, runs in `StartAsync` and returns).

**Constructor dependencies:**
- `IOptions<DomainServiceOptions> domainServiceOptions` -- for `Registrations` dictionary
- `IOptions<ProjectionOptions> projectionOptions` -- for refresh interval configuration
- `ILogger<ProjectionDiscoveryHostedService> logger` -- structured logging

**Implementation:**

```csharp
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// One-shot startup service that discovers projection-capable domain services
/// from existing <see cref="DomainServiceOptions.Registrations"/> and logs
/// the configured projection mode for each domain.
/// </summary>
/// <remarks>
/// Convention-based discovery: any domain service registered in
/// <c>EventStore:DomainServices:Registrations</c> is automatically
/// a potential projection source. No separate projection registration
/// is needed. The refresh interval from <see cref="ProjectionOptions"/>
/// controls whether projections use immediate (fire-and-forget) or
/// polling mode.
/// <para><b>Fail-fast:</b> Accessing <see cref="ProjectionOptions"/> triggers
/// ValidateOnStart validation. Invalid configuration (negative intervals, empty
/// domain keys) will intentionally crash startup — this is fail-fast by design.</para>
/// </remarks>
public sealed partial class ProjectionDiscoveryHostedService(
    IOptions<DomainServiceOptions> domainServiceOptions,
    IOptions<ProjectionOptions> projectionOptions,
    ILogger<ProjectionDiscoveryHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        DomainServiceOptions dsOptions = domainServiceOptions.Value;
        ProjectionOptions pOptions = projectionOptions.Value;

        // Extract unique domain names from static registrations.
        // Registration keys are "{tenant}:{domain}:{version}" or "{tenant}|{domain}|{version}".
        var discoveredDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in dsOptions.Registrations.Keys)
        {
            string domain = ExtractDomain(key);
            if (!string.IsNullOrWhiteSpace(domain))
            {
                _ = discoveredDomains.Add(domain);
            }
        }

        if (discoveredDomains.Count == 0)
        {
            // Operators: for runtime-resolved domains (DAPR config store), check EventId 1111
            // in ProjectionUpdateOrchestrator logs for "No domain service registered" messages.
            Log.NoRegistrations(logger);
            return Task.CompletedTask;
        }

        // Log projection mode for each discovered domain
        foreach (string domain in discoveredDomains.Order())
        {
            int refreshMs = pOptions.GetRefreshIntervalMs(domain);
            if (refreshMs == 0)
            {
                Log.DomainImmediate(logger, domain);
            }
            else
            {
                Log.DomainPolling(logger, domain, refreshMs);
            }
        }

        // Warn on projection config entries that reference domains without registrations
        foreach (string configuredDomain in pOptions.Domains.Keys)
        {
            if (!discoveredDomains.Contains(configuredDomain))
            {
                Log.OrphanedConfig(logger, configuredDomain);
            }
        }

        Log.DiscoveryComplete(logger, discoveredDomains.Count, pOptions.DefaultRefreshIntervalMs);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Extracts the domain segment from a registration key.
    /// Keys use either colon or pipe separators: "{tenant}:{domain}:{version}" or "{tenant}|{domain}|{version}".
    /// </summary>
    internal static string ExtractDomain(string registrationKey)
    {
        // Try colon separator first, then pipe
        char separator = registrationKey.Contains(':') ? ':' : '|';
        string[] parts = registrationKey.Split(separator);
        return parts.Length >= 2 ? parts[1] : string.Empty;
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1120,
            Level = LogLevel.Information,
            Message = "Projection discovery: no domain service registrations found in EventStore:DomainServices:Registrations. Projections will activate on first domain service resolution at runtime.")]
        public static partial void NoRegistrations(ILogger logger);

        [LoggerMessage(
            EventId = 1121,
            Level = LogLevel.Information,
            Message = "Projection discovery: domain '{Domain}' -> immediate mode (fire-and-forget after persistence)")]
        public static partial void DomainImmediate(ILogger logger, string domain);

        [LoggerMessage(
            EventId = 1122,
            Level = LogLevel.Warning,
            Message = "Projection discovery: domain '{Domain}' -> polling mode (RefreshIntervalMs={RefreshIntervalMs}). WARNING: Background poller not yet implemented; projections for this domain will NOT update automatically until poller is available.")]
        public static partial void DomainPolling(ILogger logger, string domain, int refreshIntervalMs);

        [LoggerMessage(
            EventId = 1123,
            Level = LogLevel.Warning,
            Message = "Projection configuration for domain '{Domain}' has no matching domain service registration in EventStore:DomainServices:Registrations. This configuration entry will have no effect.")]
        public static partial void OrphanedConfig(ILogger logger, string domain);

        [LoggerMessage(
            EventId = 1124,
            Level = LogLevel.Information,
            Message = "Projection discovery complete: {DomainCount} domains discovered. Default refresh interval: {DefaultRefreshIntervalMs}ms (0=immediate).")]
        public static partial void DiscoveryComplete(ILogger logger, int domainCount, int defaultRefreshIntervalMs);
    }
}
```

**CRITICAL: Static registrations only.** At startup, only `DomainServiceOptions.Registrations` (static/config-based registrations) are available. DAPR config store registrations are resolved at runtime and cannot be enumerated at startup. The startup log covers statically configured domains; runtime-resolved domains are covered by the existing orchestrator logging (EventId 1111 "No domain service registered").

**CRITICAL: `ExtractDomain` is `internal static`** for testability. Registration keys use colon (`tenant:domain:version`) or pipe (`tenant|domain|version`) separators. See `DomainServiceResolver` lines 41-50 for the dual-format handling rationale.

### Task 3: Register Hosted Service in DI

**File:** `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`

**Change:** Add one line after the `ProjectionOptions` registration (after line 64):
```csharp
_ = services.AddHostedService<ProjectionDiscoveryHostedService>();
```

**Required using:** Already covered by existing `using Hexalith.EventStore.Server.Projections;` at line 8.

**Required using for `IHostedService`:** Add:
```csharp
using Microsoft.Extensions.Hosting;
```

**Note:** `AddHostedService<T>()` is from `Microsoft.Extensions.Hosting.Abstractions` which is already a transitive dependency via `Microsoft.Extensions.Options`.

Wait -- `AddHostedService` is in `Microsoft.Extensions.Hosting`. Check that `Hexalith.EventStore.Server` has a reference to `Microsoft.Extensions.Hosting.Abstractions`. If not, add it to the `.csproj`.

### Task 4: Tests

**File 1:** `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs`

**Test setup:**
- NSubstitute mocks for `IActorProxyFactory`, `DaprClient`, `IDomainServiceResolver`
- Real `ProjectionOptions` wrapped in `Options.Create(...)`
- `NullLogger<ProjectionUpdateOrchestrator>` for logging

**Tests to create:**

1. `UpdateProjectionAsync_DefaultRefreshIntervalZero_ProceedsWithUpdate` (AC: 2)
   - Create `ProjectionOptions { DefaultRefreshIntervalMs = 0 }`
   - Mock resolver to return valid registration, mock aggregate proxy with events, mock DAPR response
   - Call `UpdateProjectionAsync`
   - Verify `resolver.ResolveAsync` was called (orchestrator proceeded past the refresh interval check)

2. `UpdateProjectionAsync_DefaultRefreshIntervalPositive_SkipsUpdate` (AC: 2)
   - Create `ProjectionOptions { DefaultRefreshIntervalMs = 5000 }`
   - Call `UpdateProjectionAsync`
   - Verify `resolver.ResolveAsync` was NOT called (orchestrator returned early)

3. `UpdateProjectionAsync_PerDomainOverrideZero_ProceedsForThatDomain` (AC: 3)
   - Create `ProjectionOptions { DefaultRefreshIntervalMs = 5000, Domains = { ["counter"] = new { RefreshIntervalMs = 0 } } }`
   - Call with domain "counter"
   - Verify `resolver.ResolveAsync` was called

4. `UpdateProjectionAsync_PerDomainOverridePositive_SkipsForThatDomain` (AC: 3)
   - Create `ProjectionOptions { DefaultRefreshIntervalMs = 0, Domains = { ["order"] = new { RefreshIntervalMs = 3000 } } }`
   - Call with domain "order"
   - Verify `resolver.ResolveAsync` was NOT called

5. `UpdateProjectionAsync_PerDomainOverridePositive_ProceedsForOtherDomains` (AC: 3)
   - Same options as test 4
   - Call with domain "counter" (not in Domains dict, uses default 0)
   - Verify `resolver.ResolveAsync` was called

6. `UpdateProjectionAsync_TenantIsolation_PassesTenantIdThroughPipeline` (AC: 4)
   - Create orchestrator with `DefaultRefreshIntervalMs = 0`
   - Call with specific `AggregateIdentity { TenantId = "acme", Domain = "counter", AggregateId = "123" }`
   - Verify `resolver.ResolveAsync` called with `tenantId: "acme"`
   - Verify aggregate actor proxy created with ActorId containing "acme"

**File 2:** `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDiscoveryHostedServiceTests.cs`

**Tests to create:**

7. `ExtractDomain_ColonSeparatedKey_ReturnsDomain`
   - Input: `"tenant1:counter:v1"` -> Output: `"counter"`

8. `ExtractDomain_PipeSeparatedKey_ReturnsDomain`
   - Input: `"tenant1|counter|v1"` -> Output: `"counter"`

9. `ExtractDomain_SingleSegment_ReturnsEmpty`
   - Input: `"invalid"` -> Output: `""`

10. `ExtractDomain_FourSegmentKey_ReturnsSecondSegment`
    - Input: `"org:tenant:counter:v1"` -> Output: `"tenant"` (always returns `parts[1]`, the second segment)
    - Documents assumption: key format is exactly `{tenant}:{domain}:{version}` (3 segments)

11. `StartAsync_NoRegistrations_LogsNoRegistrations`
    - Create service with empty `Registrations` dictionary
    - Call `StartAsync`
    - No exceptions thrown (startup validator should never block)

12. `StartAsync_WithRegistrations_LogsProjectionModes`
    - Create service with registrations for "counter" domain
    - Create `ProjectionOptions { DefaultRefreshIntervalMs = 0 }`
    - Call `StartAsync`
    - No exceptions thrown

13. `StartAsync_OrphanedDomainConfig_LogsWarning`
    - Create service with empty registrations
    - Create `ProjectionOptions { Domains = { ["unknown"] = new { RefreshIntervalMs = 5000 } } }`
    - Call `StartAsync`
    - No exceptions thrown (orphaned config produces a warning, not an error)

**Test helper notes:**
- `ProjectionUpdateOrchestrator` constructor now takes 5 params: `IActorProxyFactory`, `DaprClient`, `IDomainServiceResolver`, `IOptions<ProjectionOptions>`, `ILogger`
- Existing 12 tests in `ProjectionUpdateOrchestratorTests.cs` must be updated to include the new `IOptions<ProjectionOptions>` parameter. Use `Options.Create(new ProjectionOptions())` (default `DefaultRefreshIntervalMs = 0`) so all existing tests continue to pass without behavior changes.
- `AggregateIdentity` constructor: `new AggregateIdentity(tenantId, domain, aggregateId)`
- `DomainServiceOptions` has a public `Registrations` dictionary
- `DomainServiceRegistration` takes `(AppId, MethodName, TenantId, Domain, Version)`

### CRITICAL: Scope Boundaries

**This story ONLY creates/modifies:**
- `ProjectionUpdateOrchestrator.cs` -- add `IOptions<ProjectionOptions>` constructor param, add refresh interval gating logic, add LoggerMessage
- `ProjectionDiscoveryHostedService.cs` -- NEW, startup-time discovery and logging
- `ServiceCollectionExtensions.cs` -- add `AddHostedService<ProjectionDiscoveryHostedService>()`
- Test files for new functionality
- Update existing `ProjectionUpdateOrchestratorTests.cs` constructor calls with `IOptions<ProjectionOptions>` param

**Do NOT create or modify:**
- `EventPublisher.cs` -- no changes needed (gating is in orchestrator)
- `EventPublisher` test files -- no changes needed (EventPublisher constructor unchanged)
- `ProjectionOptions.cs` -- no changes needed (already has `GetRefreshIntervalMs`, `Domains`, `Validate`)
- `DomainServiceOptions.cs` -- no changes needed (already has `Registrations` dict)
- `DomainServiceResolver.cs` -- no changes needed
- `ProjectionPollerService` (background `IHostedService` for polling) -- deferred beyond this epic
- `ProjectionCheckpointTracker` -- deferred beyond this epic
- Counter Sample `/project` endpoint -- that's Story 11-5
- Any projection contracts (ProjectionEventDto, ProjectionRequest, ProjectionResponse) -- no changes
- `AssemblyScanner.cs` -- no changes (server-side discovery uses config, not assembly scanning)
- `NamingConventionEngine.cs` -- no changes

### CRITICAL: Do NOT Break Existing Tests

- `ProjectionUpdateOrchestrator` gains a new required constructor parameter (`IOptions<ProjectionOptions>`). Existing **12 tests** in `ProjectionUpdateOrchestratorTests.cs` must be updated to include the new parameter. Pass `Options.Create(new ProjectionOptions())` -- the default `DefaultRefreshIntervalMs = 0` means all existing tests proceed with the immediate trigger path (unchanged behavior).
- `ServiceCollectionExtensions` changes are additive (new hosted service registration)
- No existing interfaces are modified
- All existing tests (Tier 1: 709, Tier 2: 1564) must continue to pass

### CRITICAL: Microsoft.Extensions.Hosting.Abstractions Dependency

Before creating `ProjectionDiscoveryHostedService`, verify that `Hexalith.EventStore.Server.csproj` has a reference to `Microsoft.Extensions.Hosting.Abstractions` (for `IHostedService` and `AddHostedService<T>`). If not present, add:
```xml
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" />
```
Check `Directory.Packages.props` for version management. The existing `CommandApi` project has this dependency (via `BackgroundService`). The Server project may already have it transitively via DAPR SDK or Aspire dependencies -- verify before adding explicitly.

### Existing Code Patterns to Follow

**IHostedService one-shot pattern** (from `CommandApiAuthorizationStartupValidator.cs`):
```csharp
internal sealed class CommandApiAuthorizationStartupValidator(IServiceScopeFactory scopeFactory) : IHostedService {
    public Task StartAsync(CancellationToken cancellationToken) {
        // Do startup validation
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

**LoggerMessage pattern** (from `ProjectionUpdateOrchestrator.cs`):
```csharp
private static partial class Log
{
    [LoggerMessage(EventId = 1110, Level = LogLevel.Debug, Message = "...")]
    public static partial void MethodName(ILogger logger, ...);
}
```

**Options registration pattern** (from `ServiceCollectionExtensions.cs`):
```csharp
_ = services.AddOptions<ProjectionOptions>()
    .Bind(configuration.GetSection("EventStore:Projections"))
    .Validate(o => { o.Validate(); return true; }, "...")
    .ValidateOnStart();
```

**DI hosted service registration** (from `CommandApi ServiceCollectionExtensions.cs`):
```csharp
_ = services.AddHostedService<DaprRateLimitConfigSync>();
```

**Options.Create for tests** (standard .NET pattern):
```csharp
var options = Options.Create(new ProjectionOptions { DefaultRefreshIntervalMs = 0 });
```

**DomainServiceOptions.Registrations key format:**
- Colon-separated (canonical): `"tenant1:counter:v1"`
- Pipe-separated (config-friendly): `"tenant1|counter|v1"`

### Project Structure Notes

```
src/Hexalith.EventStore.Server/Projections/
  ProjectionUpdateOrchestrator.cs               [MODIFY -- add IOptions<ProjectionOptions> param, add refresh interval gating]
  ProjectionDiscoveryHostedService.cs           [NEW -- startup-time discovery and logging]
  IProjectionUpdateOrchestrator.cs              [EXISTS -- no changes]
  NoOpProjectionUpdateOrchestrator.cs           [EXISTS -- no changes]

src/Hexalith.EventStore.Server/Configuration/
  ServiceCollectionExtensions.cs                [MODIFY -- add AddHostedService<ProjectionDiscoveryHostedService>()]
  ProjectionOptions.cs                          [EXISTS -- no changes]

tests/Hexalith.EventStore.Server.Tests/Projections/
  ProjectionUpdateOrchestratorTests.cs          [MODIFY -- update constructor calls with IOptions<ProjectionOptions>]
  ProjectionUpdateOrchestratorRefreshIntervalTests.cs  [NEW -- refresh interval gating tests]
  ProjectionDiscoveryHostedServiceTests.cs      [NEW -- startup discovery tests]
```

### Previous Story Intelligence (Story 11-3)

Key learnings from the previous story:
- **Branch naming:** `feat/story-11-3-immediate-projection-trigger-fire-and-forget`
- **Commit message pattern:** `feat: <description for Story 11-4>`
- **Test count baseline:** Tier 1: 709 passed, Tier 2: 1564 passed (total 2273)
- **Build must pass:** `dotnet build Hexalith.EventStore.slnx --configuration Release` with 0 warnings, 0 errors (TreatWarningsAsErrors = true)
- **ProjectionUpdateOrchestrator** has 4 constructor params before this change (actorProxyFactory, daprClient, resolver, logger) -- will become 5 with IOptions<ProjectionOptions> inserted before logger
- **LoggerMessage EventId range:** 1110-1116 used by `ProjectionUpdateOrchestrator`. This story adds 1117 (polling mode deferred) and 1120-1124 (discovery hosted service)
- **DAPR actor remoting serialization:** `[DataContract]/[DataMember]` annotations were added to `EventEnvelope` in Story 11-3. No further serialization changes needed for 11-4
- **Fail-open hardening:** Story 11-2 added guards for empty ProjectionType/TenantId in `EventReplayProjectionActor`. The orchestrator passes valid values
- **NoOpProjectionUpdateOrchestrator:** Exists as test fallback. Not affected by orchestrator constructor changes since it's a separate class implementing the same interface
- **DaprClient.InvokeMethodAsync<TRequest, TResponse>** is non-virtual and cannot be mocked with NSubstitute. Tests that need to verify the full pipeline path must work around this (same limitation as Story 11-3)

### Git Intelligence

Recent commits (last 5):
- `25b6a4c` Merge PR #126 (Story 11-3 Immediate Projection Trigger)
- `c96503c` feat: Add immediate projection trigger with fire-and-forget background task
- `ed47cd6` Merge PR #125 (Story 11-2 EventReplayProjectionActor)
- `d390695` feat: Implement EventReplayProjectionActor and projection state storage
- `6ba7598` Merge PR #124 (Story 11-1 projection contracts)

Pattern: feature branches merged via PRs, conventional commit messages (`feat: ...`).

### Package/Framework Reference

- .NET 10 SDK `10.0.103` (from `global.json`)
- DAPR SDK `1.17.0` -- `DaprClient.InvokeMethodAsync<TRequest, TResponse>`, `IActorProxyFactory`, `ActorId`
- xUnit `2.9.3`, Shouldly `4.3.0`, NSubstitute `5.3.0`
- `TreatWarningsAsErrors = true` -- any warning is a build failure
- `ConfigureAwait(false)` on all async calls (library code convention)
- File-scoped namespaces, Allman brace style, 4-space indentation
- `Microsoft.Extensions.Options` for `IOptions<T>`, `Options.Create<T>`

### Architecture Decisions

**ADR-1: Gate in orchestrator vs EventPublisher.**
Chosen `ProjectionUpdateOrchestrator` to avoid modifying `EventPublisher` constructor and its 7+ test call sites. The `Task.Run` overhead for polling-mode domains (spawns a task that immediately returns) is negligible. The orchestrator is the correct architectural boundary for projection-related decisions.

**ADR-2: Static registrations only at startup.**
`ProjectionDiscoveryHostedService` only reads `DomainServiceOptions.Registrations` (statically configured). DAPR config store entries (dynamically registered at runtime) cannot be enumerated at startup. Runtime-resolved domains are covered by the existing orchestrator logging (EventId 1111 "No domain service registered for projection update").

**ADR-3: Background poller deferred beyond this epic.**
The design spec describes a `ProjectionPollerService` for `RefreshIntervalMs > 0`, but Story 11-3 explicitly deferred it ("ProjectionPollerService (background IHostedService) -- deferred, not in this epic sprint"). This story implements the configuration gating and logs an informational message for polling-mode domains. The poller itself is a future epic.

**ADR-4: Discovery via DomainServiceOptions, not AssemblyScanner.**
Server-side projection discovery uses `DomainServiceOptions.Registrations` (configuration-based), not `AssemblyScanner` (assembly-scanning). The server does not scan for `EventStoreProjection<T>` types -- it discovers projection-capable domains through their domain service registrations. `AssemblyScanner` is a client-side concern used by `AddEventStore()` in domain service projects.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 11.4] -- Story requirements and acceptance criteria
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 11] -- Epic overview: Server-Managed Projection Builder
- [Source: docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md#Convention-Based Discovery] -- Design spec section 6: convention-based discovery
- [Source: docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md#Configuration] -- Design spec: configuration JSON structure
- [Source: docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md#Security] -- Design spec: tenant isolation requirements
- [Source: src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs] -- Orchestrator to modify (add refresh interval gating)
- [Source: src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs] -- Existing options with GetRefreshIntervalMs, Domains, Validate
- [Source: src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs] -- DI registration point for hosted service
- [Source: src/Hexalith.EventStore.Server/DomainServices/DomainServiceOptions.cs] -- Registrations dictionary for discovery
- [Source: src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs] -- Dual key format (colon/pipe), static + config store resolution
- [Source: src/Hexalith.EventStore.CommandApi/Authorization/CommandApiAuthorizationStartupValidator.cs] -- One-shot IHostedService pattern
- [Source: src/Hexalith.EventStore.Server/Events/EventPublisher.cs] -- Fire-and-forget trigger (lines 125-137, NOT modified in this story)
- [Source: tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs] -- Existing tests to update constructor calls
- [Source: _bmad-output/implementation-artifacts/11-3-immediate-projection-trigger-fire-and-forget.md] -- Previous story (test counts, patterns, conventions)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- No issues encountered. All code compiled cleanly with 0 warnings, 0 errors on first attempt.

### Completion Notes List

- AC1: Convention-based discovery implemented via `ProjectionDiscoveryHostedService` which reads `DomainServiceOptions.Registrations` at startup and logs projection mode for each discovered domain.
- AC2: Refresh interval gating added to `ProjectionUpdateOrchestrator.UpdateProjectionAsync` — `DefaultRefreshIntervalMs == 0` proceeds with immediate trigger, `> 0` logs and returns early (polling mode deferred to future epic).
- AC3: Per-domain override via `ProjectionOptions.Domains` dictionary takes precedence over `DefaultRefreshIntervalMs`. Tested with 5 scenarios covering override combinations.
- AC4: Tenant isolation verified — orchestrator passes `identity.TenantId` through the entire pipeline (resolver, actor proxy creation). Test confirms tenant ID flows through correctly.
- 13 new tests added (6 refresh interval gating + 7 discovery hosted service), all passing.
- 12 existing orchestrator tests updated with new `IOptions<ProjectionOptions>` constructor parameter, all passing.
- Full build: 0 warnings, 0 errors. Tier 1: 709 passed. Tier 2: 1577 passed (1564 existing + 13 new).

### Change Log

- 2026-03-20: Story 11-4 implementation — added refresh interval gating in orchestrator, created ProjectionDiscoveryHostedService, registered in DI, created 13 new tests.

### File List

- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` — added `IOptions<ProjectionOptions>` constructor param, refresh interval gating before try/catch, LoggerMessage EventId 1117
- `src/Hexalith.EventStore.Server/Projections/ProjectionDiscoveryHostedService.cs` — NEW: one-shot IHostedService for startup discovery and logging
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` — added `AddHostedService<ProjectionDiscoveryHostedService>()` and `Microsoft.Extensions.Hosting` using
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs` — updated CreateSut() to include `IOptions<ProjectionOptions>` parameter
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs` — NEW: 6 tests for refresh interval gating (ACs 2, 3, 4)
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDiscoveryHostedServiceTests.cs` — NEW: 7 tests for discovery hosted service (ExtractDomain + StartAsync scenarios)
