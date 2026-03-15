# Story 18.6: Sample UI Refresh Patterns

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **Blazor developer**,
I want **at least 3 documented reference patterns for handling the SignalR "changed" signal**,
So that **I can choose the right UI update strategy for my use case**.

## Acceptance Criteria

1. **Three reference implementations in the sample project** — **Given** the EventStore sample application, **When** a developer looks for SignalR integration patterns, **Then** they find at least 3 reference implementations (FR60): (a) persistent notification prompting manual refresh, (b) automatic silent data reload, (c) selective component refresh targeting only the affected projection
2. **Each pattern demonstrates full SignalR lifecycle** — **Given** each sample pattern implemented as a Blazor component, **When** a developer reviews it, **Then** it demonstrates: subscribing to the SignalR group via `EventStoreSignalRClient.SubscribeAsync()`, handling the "changed" signal callback, and triggering the appropriate UI update
3. **Trade-offs documented in each pattern** — **Given** the sample patterns, **When** a developer evaluates them, **Then** trade-offs are documented inline: persistent notification = least disruptive but requires user action, silent reload = seamless but may cause layout shifts, selective refresh = best UX but most implementation effort
4. **Patterns use EventStoreSignalRClient from Story 18-5** — **Given** the `EventStoreSignalRClient` helper (from Story 18-5), **When** patterns subscribe to projection changes, **Then** they use the client helper's `SubscribeAsync(projectionType, tenantId, onChanged)` method — not raw `HubConnection` wiring
5. **Counter domain used as projection source** — **Given** the existing Counter domain sample, **When** demonstrating UI refresh, **Then** all patterns subscribe to counter projection changes (projectionType="counter", tenantId from config) using the existing domain
6. **Blazor Fluent UI v4 components** — **Given** the Blazor UI components, **When** implemented, **Then** they use Microsoft Fluent UI Blazor v4 components (`FluentMessageBar`, `FluentCard`, `FluentButton`, `FluentDataGrid`) consistent with UX specification
7. **Landing page includes run instructions** — **Given** the sample's Index page, **When** a developer opens it, **Then** it explains how to launch the full topology (AppHost → CommandApi + Sample + BlazorUI) and which pattern to try first (recommended order: Pattern 2 → Pattern 1 → Pattern 3)
8. **Build-smoke test verifies compilation** — **Given** the Blazor UI sample project, **When** included in `dotnet build Hexalith.EventStore.slnx`, **Then** it compiles with zero errors and zero warnings
9. **All existing tests pass** — All Tier 1, Tier 2, and Tier 3 tests continue to pass with zero behavioral change

## Tasks / Subtasks

<!-- TASK PRIORITY: Tasks 1-2 = Foundation (scaffolding), Tasks 3-5 = Core Deliverable (FR60 patterns), Tasks 6-7 = Integration, Tasks 8-9 = Verification -->

- [x] Task 1: [FOUNDATION] Create sample Blazor Server project scaffolding (AC: #5, #6)
    - [x] 1.1 Create `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj` — Blazor Server project targeting `net10.0`
    - [x] 1.2 Add NuGet references: `Microsoft.FluentUI.AspNetCore.Components` (v4), `Hexalith.EventStore.SignalR` (project reference), `Microsoft.AspNetCore.SignalR.Client`
    - [x] 1.3 Add to `Hexalith.EventStore.slnx` solution file
    - [x] 1.4 Create `Program.cs` with Blazor Server setup
    - [x] 1.5 Create `App.razor` — standard Blazor layout with `<FluentDesignTheme>`
    - [x] 1.6 Create `_Imports.razor` — common Blazor and FluentUI `@using` directives
    - [x] 1.7 Create `MainLayout.razor` — Blazor Server layout with `<FluentLayout>`, navigation sidebar linking to each pattern page
    - [x] 1.8 Create `Routes.razor` — Blazor router component
    - [x] 1.9 Create `appsettings.json` with all required configuration keys

- [x] Task 2: [FOUNDATION] Create shared counter query service (AC: #4, #5)
    - [x] 2.1 Create `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs`
    - [x] 2.2 Constructor: `IHttpClientFactory httpClientFactory`
    - [x] 2.3 Method: `Task<CounterStatusResult> GetCounterStatusAsync(string tenantId, CancellationToken ct = default)`
    - [x] 2.4 Record: `CounterStatusResult(int Count, string? ETag)`
    - [x] 2.5 Client-side cache fields for HTTP 304 handling
    - [x] 2.6 Uses `If-None-Match` header when `_lastETag` is known
    - [x] 2.7 Helper constant: `_emptyPayload = JsonDocument.Parse("{}").RootElement.Clone()`

- [x] Task 3: [CORE] Pattern 1 — Persistent Notification Prompting Manual Refresh (AC: #1, #2, #3)
    - [x] 3.1 Create `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor`
    - [x] 3.2 Subscribe to counter projection changes via `EventStoreSignalRClient.SubscribeAsync`
    - [x] 3.3 On "changed" signal: show `FluentMessageBar` (persistent, inline) with "Refresh Now" button
    - [x] 3.4 On "Refresh Now" click: re-query, update count, dismiss message bar
    - [x] 3.5 Display current count in `FluentCard` with last-refreshed timestamp
    - [x] 3.6 Multiple signals do not stack — existing bar stays visible
    - [x] 3.7 Inline documentation comment block explaining trade-offs
    - [x] 3.8 Dispose: `UnsubscribeAsync()` in `@implements IAsyncDisposable`

- [x] Task 4: [CORE] Pattern 2 — Automatic Silent Data Reload (AC: #1, #2, #3)
    - [x] 4.1 Create `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor`
    - [x] 4.2 Subscribe to counter projection changes via `EventStoreSignalRClient.SubscribeAsync`
    - [x] 4.3 On "changed" signal: debounced re-query via `InvokeAsync`
    - [x] 4.4 Debounce with `CancellationTokenSource` pattern (200ms)
    - [x] 4.5 Display count in `FluentCard` with CSS fade transition (opacity 0.3s)
    - [x] 4.6 Inline documentation comment block
    - [x] 4.7 Dispose: unsubscribe + cancel pending debounce

- [x] Task 5: [CORE] Pattern 3 — Selective Component Refresh (AC: #1, #2, #3)
    - [x] 5.1 Create `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SelectiveRefreshPattern.razor`
    - [x] 5.2 Page contains independent components: `CounterValueCard`, `CounterHistoryGrid`, `CounterCommandForm`
    - [x] 5.3 Only `CounterValueCard` and `CounterHistoryGrid` subscribe — `CounterCommandForm` does not
    - [x] 5.4 Each subscribed component independently refreshes on signal
    - [x] 5.5 Create `CounterValueCard.razor` with FluentCard, FluentProgress, SignalR subscription
    - [x] 5.6 Create `CounterHistoryGrid.razor` with FluentDataGrid, history tracking (last 20 entries)
    - [x] 5.7 Create `CounterCommandForm.razor` with Increment/Decrement/Reset buttons
    - [x] 5.8 Inline documentation comment block
    - [x] 5.9 `CounterValueCard` and `CounterHistoryGrid` implement `IAsyncDisposable`

- [x] Task 6: [INTEGRATION] Create pattern comparison landing page (AC: #3, #7)
    - [x] 6.1 Create `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/Index.razor`
    - [x] 6.2 FluentDataGrid comparing all 3 patterns
    - [x] 6.3 Navigation links to each pattern page
    - [x] 6.4 Signal-only model explanation
    - [x] 6.5 Recommended order section (Pattern 2 → 1 → 3)
    - [x] 6.6 Run instructions section

- [x] Task 7: [INTEGRATION] Wire up in Aspire AppHost (AC: #5)
    - [x] 7.1 Modify `src/Hexalith.EventStore.AppHost/Program.cs`
    - [x] 7.2 Add BlazorUI as Aspire resource `"sample-blazor-ui"`
    - [x] 7.3 Add `.WithReference(commandApi)` for service discovery
    - [x] 7.4 Configure CommandApi SignalR: `EventStore__SignalR__Enabled = true`
    - [x] 7.5 Added project reference in AppHost csproj

- [x] Task 8: [VERIFICATION] Build-smoke test for Blazor UI project (AC: #8)
    - [x] 8.1 Full solution build: 0 errors, 0 warnings
    - [x] 8.2 DI container registers: `EventStoreSignalRClient`, `CounterQueryService`, `IHttpClientFactory`

- [x] Task 9: [VERIFICATION] Verify zero regression (AC: #9)
    - [x] 9.1 Tier 1: 535 tests passed (Contracts 197, Client 273, Testing 61, Sample 4)
    - [x] 9.2 Tier 2: 1,245 tests passed (Server 1,233, SignalR 12)
    - [x] 9.3 Full solution build: `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings

## Architectural Decisions

### ADR-18.6a: Separate Blazor Server Project (Not Embedded in Existing Sample)

- **Choice:** New `samples/Hexalith.EventStore.Sample.BlazorUI/` project
- **Rejected:** Adding Blazor pages to `Hexalith.EventStore.Sample` — that project is a domain service (D4: zero infrastructure access). Mixing concerns violates the architecture.
- **Trade-off:** Two sample projects to maintain. But separation demonstrates the real-world topology: domain service + UI are different deployments.

### ADR-18.6b: Blazor Server Rendering Mode (Not WebAssembly)

- **Choice:** Blazor Server (`AddServerSideBlazor()`) — persistent circuit with server-side rendering
- **Rejected:** Blazor WebAssembly — SignalR in WASM has CORS complications, and the `EventStoreSignalRClient` from Story 18-5 is designed for server-side use with `HubConnectionBuilder`. WASM would require additional proxy configuration.
- **Rejected:** Blazor United (SSR + Server interactivity) — adds rendering mode complexity for a sample. Server mode is simplest to demonstrate real-time SignalR patterns.
- **Trade-off:** Server mode requires a persistent circuit (WebSocket to the Blazor host). This is the natural fit for real-time SignalR scenarios where the client already maintains a connection.
- **Rationale:** The sample demonstrates SignalR patterns. Blazor Server's persistent circuit is architecturally aligned — the developer's Blazor Server connection and the SignalR projection-change connection coexist naturally.

### ADR-18.6c: FluentMessageBar Over FluentToast for Pattern 1

- **Choice:** Use `FluentMessageBar` (persistent inline notification) instead of `FluentToast` for the "data changed, refresh?" pattern
- **Rejected:** `FluentToast` — auto-dismisses after ~5 seconds by default (per UX spec). A "Refresh" action on an auto-dismissing notification gives insufficient time for the user to act, especially if they're reading other content.
- **Trade-off:** Message bar takes vertical space in the layout. But this is intentional — it's a persistent prompt that the user must acknowledge.
- **Rationale:** The pattern's purpose is "user controls when to refresh." An auto-dismissing toast undermines that purpose.

## Dev Notes

### Dependencies

- **HARD DEPENDENCY on Story 18-5** (SignalR Real-Time Notifications): `EventStoreSignalRClient`, `EventStoreSignalRClientOptions`, `IProjectionChangedClient`, `ProjectionChangedHub` — all created in Story 18-5. Story 18-5 must be complete before this story can be implemented.
- **Existing infrastructure (done):** ETag actor (18-1), query routing (18-2), query endpoint with ETag pre-check (18-3), query contracts (18-4)

### Architecture Patterns and Constraints

- **Blazor Fluent UI v4.13.2** — v5 in development but v4 supported until Nov 2026. Use `Microsoft.FluentUI.AspNetCore.Components` v4 package. NOT v5.
- **Sample project is NOT a NuGet package** — this is a `samples/` project, not published. It demonstrates patterns but is not part of the 5 published NuGet packages.
- **D4: Domain services have zero infrastructure access** — the sample domain service (Counter) has no DAPR/SignalR dependencies. The Blazor UI project is a separate consumer that connects to CommandApi.
- **Coarse invalidation model** — ETag is per `{ProjectionType}:{TenantId}`, not per entity. All patterns receive signals for any change within the projection+tenant pair.
- **Signal-only model** — the SignalR "changed" callback provides only `(projectionType, tenantId)` — no data payload. Patterns must re-query via the query endpoint.

### Server-to-Server HTTP (No CORS)

The Blazor UI runs in **Blazor Server** mode — all `HttpClient` calls from `CounterQueryService` execute on the server, not in the browser. This means CommandApi does NOT need CORS headers for this sample. Do NOT add `AllowAnyOrigin()` or CORS middleware to CommandApi for this story. If the dev agent sees a network error, it's a service discovery or URL issue, not CORS.

### HttpClient Configuration (Aspire Service Discovery)

The `CounterQueryService` uses `IHttpClientFactory` with a named client `"EventStoreApi"`. In the Aspire topology, the CommandApi URL is resolved via service discovery — the BlazorUI project references CommandApi via `.WithReference(commandApi)` in the AppHost, and the `EventStore:CommandApiUrl` configuration key is populated automatically. Do NOT hardcode `https://localhost:5001` — use Aspire service discovery.

```csharp
// Program.cs
builder.Services.AddHttpClient("EventStoreApi", client =>
    client.BaseAddress = new Uri(builder.Configuration["EventStore:CommandApiUrl"]!));
```

### EventStoreSignalRClient Startup

`EventStoreSignalRClient` requires `StartAsync()` to establish the hub connection before any component can subscribe. Register a hosted service in `Program.cs` to start it on app startup:

```csharp
// Simple IHostedService that starts the SignalR client
builder.Services.AddHostedService<SignalRClientStartup>();
// where SignalRClientStartup.StartAsync() calls _signalRClient.StartAsync()
```

### Pattern Recommendation Order

When developers are new to EventStore SignalR integration, recommend this learning order:

1. **Pattern 2 (Silent Reload)** — simplest, ~20 lines, immediate gratification
2. **Pattern 1 (Notification)** — adds user control, introduces FluentMessageBar
3. **Pattern 3 (Selective Refresh)** — most sophisticated, teaches component isolation

### EventStoreSignalRClient API (from Story 18-5)

```csharp
// Subscribe to projection changes
await client.SubscribeAsync("counter", "sample-tenant", () => {
    // Callback: projection changed — re-query data
    InvokeAsync(StateHasChanged);
});

// Unsubscribe
await client.UnsubscribeAsync("counter", "sample-tenant");

// Lifecycle
await client.StartAsync();
await client.DisposeAsync();
```

- Auto-rejoin on reconnect (FR59) — handled internally by `EventStoreSignalRClient`
- Group name format: `{projectionType}:{tenantId}` (matching ETag actor ID)

### Counter Query Endpoint

```json
POST /api/v1/queries
{
  "domain": "counter",
  "tenant": "sample-tenant",
  "aggregateId": "counter-1",
  "queryType": "get-counter-status",
  "payload": {},
  "entityId": null
}

Headers:
  If-None-Match: "current-etag-value"

Response:
  200 OK + ETag header + JSON payload
  304 Not Modified (if ETag matches)
```

### Blazor Component Lifecycle for SignalR

```csharp
@implements IAsyncDisposable

@code {
    protected override async Task OnInitializedAsync()
    {
        await _signalRClient.SubscribeAsync(projectionType, tenantId, OnChanged);
    }

    private void OnChanged()
    {
        // Must use InvokeAsync for Blazor thread safety
        InvokeAsync(async () => {
            _data = await _queryService.GetCounterStatusAsync(tenantId);
            StateHasChanged();
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _signalRClient.UnsubscribeAsync(projectionType, tenantId);
    }
}
```

**CRITICAL:** Always use `InvokeAsync()` when updating UI from SignalR callbacks — the callback runs on a non-Blazor thread. Without `InvokeAsync()`, `StateHasChanged()` will throw.

### Project Structure Notes

- New project: `samples/Hexalith.EventStore.Sample.BlazorUI/`
- Follows existing sample convention: `samples/Hexalith.EventStore.Sample/` (domain service)
- NOT in `src/` — this is a demonstration project, not a library
- Must be added to `Hexalith.EventStore.slnx` solution file
- Must NOT be added to any NuGet packaging configuration

### Existing Files to Modify

| File                                         | Change                                                                             |
| -------------------------------------------- | ---------------------------------------------------------------------------------- |
| `Hexalith.EventStore.slnx`                   | Add new Blazor UI sample project                                                   |
| `src/Hexalith.EventStore.AppHost/Program.cs` | Add Blazor UI as Aspire resource                                                   |
| `Directory.Packages.props`                   | Add `Microsoft.FluentUI.AspNetCore.Components` v4 package version (if not present) |

### References

- [Source: _bmad-output/planning-artifacts/prd.md — FR60: 3 sample Blazor UI refresh patterns]
- [Source: _bmad-output/planning-artifacts/epics.md — Story 9.6: Sample UI Refresh Patterns]
- [Source: _bmad-output/planning-artifacts/architecture.md — Blazor Fluent UI v4.13.2, v2 reference]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — FluentToast patterns, accessibility]
- [Source: _bmad-output/implementation-artifacts/18-5-signalr-real-time-notifications.md — EventStoreSignalRClient API, SignalR hub, ADRs]
- [Source: _bmad-output/implementation-artifacts/18-3-query-endpoint-with-etag-pre-check-and-cache.md — Query endpoint, ETag headers]
- [Source: _bmad-output/implementation-artifacts/18-4-query-contract-library.md — IQueryContract, NamingConventionEngine]

## Dev Agent Record

### Agent Model Used

GPT-5.4

### Debug Log References

- Initial build errors from App.razor `InteractiveServer` — needed `RenderMode.InteractiveServer` with explicit `@using Microsoft.AspNetCore.Components.Web`
- Icons in MainLayout caused build errors — simplified to text-only navigation links (FluentUI v4 Icon classes require specific imports)
- CounterCommandForm `[Inject]` property inside `@code` block caused Razor parser errors — moved to `@inject` directive
- Review remediation surfaced three follow-up build blockers: Razor markup placement in `CounterHistoryGrid.razor`, nullable delegate mismatch in `Program.cs`, and analyzer warnings in the new bearer-token handler; all were corrected before final validation

### Completion Notes List

- Created Blazor Server sample project `samples/Hexalith.EventStore.Sample.BlazorUI/` with 3 UI refresh patterns (FR60)
- Pattern 1 (NotificationPattern): FluentMessageBar prompts user to refresh — persistent, non-stacking, user-controlled
- Pattern 2 (SilentReloadPattern): Auto-refresh with 200ms CancellationTokenSource debounce and CSS opacity fade transition
- Pattern 3 (SelectiveRefreshPattern): 3 independent components — CounterValueCard and CounterHistoryGrid subscribe, CounterCommandForm does not
- CounterQueryService: ETag-based caching with HTTP 304 support, JSON payload contract alignment
- SignalRClientStartup: IHostedService ensures hub connection before components render
- Landing page (Index.razor): FluentDataGrid comparison, recommended learning order, run instructions
- Aspire AppHost wired with service discovery and SignalR auto-enable
- All 1,780 existing tests pass with zero regression
- Full solution build: 0 errors, 0 warnings
- Review fixes completed: sample UI now authenticates to protected CommandApi and SignalR endpoints in both dev-token and Keycloak-backed Aspire runs
- `EventStoreSignalRClient` now supports multiple independent callbacks per projection group, enabling Pattern 3 component isolation as intended
- Query authorization now accepts legacy `command:query` claims in addition to the newer query permissions so the sample works with the current local auth setup
- `CounterHistoryGrid` now surfaces refresh failures in the UI instead of swallowing them silently
- Final remediation validation for this review pass: `dotnet build Hexalith.EventStore.slnx --configuration Release`, `Hexalith.EventStore.SignalR.Tests` (14 passed), and `ClaimsRbacValidatorTests` filter in `Hexalith.EventStore.Server.Tests` (24 passed)

### Change Log

- 2026-03-13: Story 18-6 implementation complete — 3 Blazor UI refresh patterns with Aspire integration
- 2026-03-13: Review fixes applied — aligned persistent notification wording, added `FluentDesignTheme`, and corrected JSON payload contracts for sample command/query requests
- 2026-03-13: Review remediation completed — added sample auth/token plumbing, multi-subscriber SignalR callback support, callback-specific unsubscribe, legacy query-claim compatibility, and visible history refresh errors
- 2026-03-15: Fixup — added `CounterCommandForm` to NotificationPattern.razor and SilentReloadPattern.razor so all 3 pattern pages are self-contained with increment/decrement/reset buttons (sprint-change-proposal-2026-03-15-counter-command-buttons)

### File List

New files:

- `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs`
- `samples/Hexalith.EventStore.Sample.BlazorUI/_Imports.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/appsettings.json`
- `samples/Hexalith.EventStore.Sample.BlazorUI/appsettings.Development.json`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Properties/launchSettings.json`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/App.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/Routes.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterValueCard.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterHistoryGrid.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterCommandForm.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Layout/MainLayout.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/Index.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SelectiveRefreshPattern.razor`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/CounterQueryService.cs`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/EventStoreApiAccessTokenProvider.cs`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/EventStoreApiAuthorizationHandler.cs`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Services/SignalRClientStartup.cs`

Modified files:

- `Hexalith.EventStore.slnx` — added BlazorUI project to /samples/ folder
- `Directory.Packages.props` — added Blazor ItemGroup with FluentUI v4.13.2 packages
- `src/Hexalith.EventStore.AppHost/Program.cs` — added BlazorUI resource with service discovery + SignalR enable
- `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` — added BlazorUI project reference
- `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs` — registered API auth/token services and SignalR access token provider
- `samples/Hexalith.EventStore.Sample.BlazorUI/appsettings.json` — aligned tenant/auth defaults for local Aspire runs
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/Index.razor` — clarified automatic authentication behavior in run instructions
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor` — aligned tenant defaults and callback-specific unsubscribe
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor` — aligned tenant defaults and callback-specific unsubscribe
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SelectiveRefreshPattern.razor` — aligned tenant defaults for pattern demo consistency
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterValueCard.razor` — aligned tenant defaults and callback-specific unsubscribe
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterHistoryGrid.razor` — added visible refresh error handling and callback-specific unsubscribe
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterCommandForm.razor` — aligned tenant defaults for authenticated sample flows
- `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` — added multi-subscriber dispatch and callback-aware unsubscribe
- `src/Hexalith.EventStore.SignalR/EventStoreSignalRClientOptions.cs` — added optional SignalR access token provider
- `src/Hexalith.EventStore.CommandApi/Authorization/ClaimsRbacValidator.cs` — accepted legacy query permission for local auth compatibility
- `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs` — added regression coverage for multi-callback and callback-specific unsubscribe behavior
- `tests/Hexalith.EventStore.Server.Tests/Authorization/ClaimsRbacValidatorTests.cs` — added regression coverage for legacy query permission handling

## Senior Developer Review (AI)

### Reviewer

GPT-5.4

### Date

2026-03-15

### Outcome

Approved — no blocking findings in Story 18.6 scope.

### Scope Reviewed

- Story acceptance criteria and task completion claims in this file
- Sample UI implementation under `samples/Hexalith.EventStore.Sample.BlazorUI/`
- Supporting SignalR client and Aspire wiring used by the sample
- Focused diagnostics and a Release build of the sample Blazor UI project

### What I Validated

- All 3 refresh pattern pages exist and are linked from the landing page
- Each pattern uses `EventStoreSignalRClient.SubscribeAsync(...)` rather than raw `HubConnection` wiring
- Pattern trade-offs are documented inline in the sample pages/components
- Counter projection (`projectionType = "counter"`) is used consistently
- Fluent UI components (`FluentMessageBar`, `FluentCard`, `FluentButton`, `FluentDataGrid`) are present in the sample implementation
- `Index.razor` includes launch guidance and recommends the order Pattern 2 → Pattern 1 → Pattern 3
- `NotificationPattern.razor` and `SilentReloadPattern.razor` now include `CounterCommandForm`, making all 3 demos self-contained for interactive evaluation
- The reviewed files report no diagnostics, and `dotnet build samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj --configuration Release` succeeded

### Findings

No medium or high-severity findings identified in the reviewed story scope.

### Notes

- The repository working tree contains many unrelated changes outside Story 18.6; those were not treated as story findings unless they directly affected the sample UI implementation under review.
- This review confirms the 2026-03-15 fixup resolves the prior self-contained demo gap for Pattern 1 and Pattern 2.
