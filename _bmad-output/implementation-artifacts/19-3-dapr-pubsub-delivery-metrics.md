# Story 19.3: DAPR Pub/Sub Delivery Metrics

Status: done

Size: Medium — Creates new `DaprSubscriptionInfo` and `DaprPubSubOverview` models in Admin.Abstractions, extends `IDaprInfrastructureQueryService` with `GetPubSubOverviewAsync`, adds `GET /api/v1/admin/dapr/pubsub` endpoint to `AdminDaprController`, creates `AdminPubSubApiClient` UI HTTP client, creates `DaprPubSub.razor` dashboard page with pub/sub component cards + subscription grid + topic topology summary + dead-letter integration + observability deep links, adds "Pub/Sub Metrics" button to `DaprComponents.razor`. Creates ~5–7 test classes across 3–4 test projects (~25–30 tests, ~6–8 hours estimated). Extends story 19-1/19-2's DAPR infrastructure foundation.

**Dependency:** Story 19-1 must be complete (`done`). Story 19-2 must be **implemented and merged** (this story reuses the `"DaprSidecar"` named HttpClient registration and `AdminServerOptions.EventStoreDaprHttpEndpoint` configuration that 19-2 adds to the codebase — if 19-2 is only `ready-for-dev`, those DI registrations don't exist yet and this story will fail to compile). Epics 14 and 15 must be complete (both are `done`). Epic 16 story 16-6 (Dead Letter Manager) must be complete for dead-letter integration.

## Definition of Done

- All acceptance criteria verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- New `/dapr/pubsub` page renders with pub/sub component cards, subscription grid, and topic topology
- New REST endpoint returns structured JSON with pub/sub components and subscriptions
- DAPR components page (`/dapr`) includes "Pub/Sub Metrics" button
- All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change
- Subscription grid correctly shows topic routing from EventStore server sidecar
- Dead-letter integration shows count and links to Dead Letter Manager

## Story

As a **platform operator or DBA using the Hexalith EventStore Admin UI**,
I want **a DAPR pub/sub delivery metrics page showing pub/sub component status, active subscriptions with topic routing, topic naming topology, dead-letter volume summary, and deep links to configured observability tools**,
so that **I can monitor the event distribution pipeline health, understand the pub/sub topology and subscription routing, identify delivery failures via dead-letter integration, and quickly navigate to detailed metrics in external observability tools for deeper investigation**.

## Acceptance Criteria

1. **AC1: Pub/sub component status cards** — The page displays `StatCard` and `StatusBadge` components for each registered pub/sub component showing component name, type (e.g., `pubsub.redis`, `pubsub.kafka`), version, and health status. Health is determined by the existing component health probe from story 19-1 (presence in DAPR metadata = Healthy). If multiple pub/sub components are registered (e.g., separate for events and dead-letters), each gets its own card. Capabilities are shown as `FluentBadge` tags.

2. **AC2: Active subscription grid** — A `FluentDataGrid<DaprSubscriptionInfo>` shows all registered subscriptions from the EventStore server's DAPR sidecar metadata. Columns: Topic, Route, PubSub Component, Type (Declarative/Programmatic), Dead Letter Topic. Sorted alphabetically by topic. A `FluentSearch` input filters by topic or route text. Data retrieved via HTTP call to EventStore server's sidecar metadata endpoint (same `EventStoreDaprHttpEndpoint` + `/v1.0/metadata` pattern as story 19-2).

3. **AC3: Reference information (topology + pipeline)** — A collapsible `FluentCard` section (collapsed by default, expandable via click) combines two reference panels: (a) **Topic topology:** Event topic pattern `{tenant}.{domain}.events`, dead-letter topic pattern `deadletter.{tenant}.{domain}.events`, delivery semantics "At-least-once · CloudEvents 1.0 · Subscribers must be idempotent", and the pub/sub component name used for publishing. (b) **Publication pipeline:** Horizontal `FluentBadge` step indicator showing `EventsStored` → `EventsPublished` → `Completed` (success) / `PublishFailed` (failure), with the failure path noting "DAPR retry exhaustion → dead-letter routing → investigate via Dead Letter Manager." Both panels are static documentation, not live data — live metrics are in external observability tools per ADR-P5.

4. **AC4: Dead-letter integration** — The summary stat cards row includes a dead-letter count `StatCard` (severity: green when 0, yellow when > 0, neutral "N/A" when API unavailable). Below the subscription grid, a separate dead-letter management `FluentCard` shows the count with a "Manage Dead Letters" `FluentButton` navigating to `/deadletters`. The stat card shows the number at a glance; the management card provides the action — both are intentional.

5. **AC5: Observability deep-link cards** — Three `FluentCard` link cards for external tools: (a) "Traces" → `AdminServerOptions.TraceUrl`, (b) "Metrics" → `AdminServerOptions.MetricsUrl`, (c) "Logs" → `AdminServerOptions.LogsUrl`. Reuse the existing `ObservabilityLinks` model at `Models/Health/ObservabilityLinks.cs` if applicable. Cards with null URLs render as disabled with "Configure observability URL to enable" guidance.

6. **AC6: REST endpoint** — `GET /api/v1/admin/dapr/pubsub` returns `DaprPubSubOverview` containing pub/sub components, subscriptions, and remote metadata availability flag. Requires `ReadOnly` authorization policy.

7. **AC7: Page routing and navigation** — Route is `/dapr/pubsub`. The DAPR components page (`/dapr`, from story 19-1) includes a `FluentButton` with `Appearance.Outline` labeled "Pub/Sub Metrics" near the sidecar status card section (adjacent to the "Actor Inspector" button from story 19-2). The NavMenu does NOT add a separate "Pub/Sub" link — pub/sub metrics are accessed from the DAPR page. The `DaprPubSub.razor` page includes a `FluentAnchor` back-link to `/dapr`.

8. **AC8: Auto-refresh and loading states** — Pub/sub component and subscription data auto-refreshes on a 30-second `PeriodicTimer` cycle (same pattern as `DaprComponents.razor`). Dead-letter count refreshes on the same cycle. `SkeletonCard` shows during initial load. Manual "Refresh" button triggers immediate data fetch. Stale data and API unavailability show `IssueBanner`.

9. **AC9: Empty states and error handling** — No pub/sub components detected: `EmptyState` with title "No pub/sub components found" and description guiding operator to check DAPR component configuration. Remote metadata unavailable (EventStore sidecar not reachable): `IssueBanner` with "EventStore sidecar unreachable — subscription data unavailable. Configure `AdminServer:EventStoreDaprHttpEndpoint` in appsettings." — local pub/sub component cards still shown from the admin server's own sidecar. No subscriptions found: informational note in subscription section "No active subscriptions registered on the EventStore server sidecar." Deep linking: `?component={pubsubName}` query parameter to highlight a specific pub/sub component on load via `[SupplyParameterFromQuery]`.

## Tasks / Subtasks

- [x] Task 1: Create new models in Admin.Abstractions (AC: #1, #2, #6)
  - [x] 1.1 Create `DaprSubscriptionInfo` record in `Models/Dapr/DaprSubscriptionInfo.cs`
  - [x] 1.2 Create `DaprPubSubOverview` record in `Models/Dapr/DaprPubSubOverview.cs`
- [x] Task 2: Extend service interface and implementation (AC: #1, #2, #6)
  - [x] 2.0 **MANDATORY pre-step:** Verify DAPR SDK version in `Directory.Packages.props`. Check whether `DaprMetadata.Subscriptions` property exists. Story 19-1 noted SDK 1.16.1 did NOT expose subscriptions — verify current version. If available via SDK, use it instead of the HTTP endpoint fallback.
  - [x] 2.1 Add `GetPubSubOverviewAsync(CancellationToken ct)` to `IDaprInfrastructureQueryService`
  - [x] 2.2 Implement `GetPubSubOverviewAsync` in `DaprInfrastructureQueryService`
- [x] Task 3: Add REST endpoint to existing controller (AC: #6)
  - [x] 3.1 Add `GetPubSubOverviewAsync` endpoint to `AdminDaprController`
- [x] Task 4: Create UI API client and dead-letter count endpoint (AC: #4, #6)
  - [x] 4.1 Create `AdminPubSubApiClient` in Admin.UI `Services/AdminPubSubApiClient.cs`
  - [x] 4.2 Register `AdminPubSubApiClient` as scoped in `Program.cs` (after existing API client registrations)
  - [x] 4.3 Add `GetDeadLetterCountAsync()` to `IDeadLetterQueryService` (Admin.Abstractions)
  - [x] 4.4 Implement `GetDeadLetterCountAsync()` in `DaprDeadLetterQueryService` (Admin.Server)
  - [x] 4.5 Add `GET /api/v1/admin/dead-letters/count` endpoint to `AdminDeadLettersController` (Admin.Server)
  - [x] 4.6 Add `GetDeadLetterCountAsync()` to `AdminDeadLetterApiClient` (Admin.UI)
- [x] Task 5: Create pub/sub metrics page (AC: #1, #2, #3, #4, #5, #7, #8, #9)
  - [x] 5.1 Create `DaprPubSub.razor` page in Admin.UI `Pages/`
  - [x] 5.2 Add "Pub/Sub Metrics" button to `DaprComponents.razor` (from story 19-1)
- [x] Task 6: Write tests (all ACs)
  - [x] 6.1 Model tests in Admin.Abstractions.Tests (`Models/Dapr/`)
  - [x] 6.2 Service tests in Admin.Server.Tests (`Services/`)
  - [x] 6.3 Controller tests in Admin.Server.Tests (`Controllers/`)
  - [x] 6.4 Dead-letter count endpoint tests in Admin.Server.Tests
  - [x] 6.5 UI page tests in Admin.UI.Tests (`Pages/`)

## Dev Notes

### Architecture Compliance

This story follows the **exact same architecture** as stories 19-1 and 19-2 and all Epic 14/15/19 patterns:

- **Models:** Immutable C# `record` types with constructor validation (`ArgumentException` / `ArgumentNullException`). Located in `Admin.Abstractions/Models/Dapr/` (same subfolder as story 19-1/19-2 models).
- **Service extension:** Extends the existing `IDaprInfrastructureQueryService` and `DaprInfrastructureQueryService` — do NOT create a separate service interface. Add the new method to the existing interface and implementation. **IMPORTANT:** Adding a method to `IDaprInfrastructureQueryService` requires awareness that NSubstitute mocks in test files from stories 19-1 and 19-2 will inherit the new method. NSubstitute stubs return default values for unconfigured methods, so existing tests should still compile and pass — but verify.
- **Controller extension:** Extends the existing `AdminDaprController` — do NOT create a separate controller. Add new action method to the existing controller.
- **UI API client:** New `AdminPubSubApiClient` in `Admin.UI/Services/`. Uses `IHttpClientFactory` with `"AdminApi"` named client. Virtual async methods for testability. `HandleErrorStatus(response)` pattern from `AdminStreamApiClient`.
- **Page:** New `DaprPubSub.razor` at `/dapr/pubsub`. Implements `IAsyncDisposable`. Self-contained `PeriodicTimer`-based 30-second polling. Injects `AdminPubSubApiClient` and `NavigationManager`.

### Key Data Sources and Retrieval Strategy

#### Pub/Sub Components

Call `DaprClient.GetMetadataAsync()` directly and filter components where `DaprComponentCategoryHelper.FromComponentType(c.Type) == DaprComponentCategory.PubSub`. Do NOT call `GetComponentsAsync()` internally — that method includes health probes with 3-second timeouts per state store component, which would double the probe overhead on each poll cycle. Instead, map the filtered `DaprComponentsMetadata` entries to `DaprComponentDetail` records with `Status = HealthStatus.Healthy` (presence in metadata = healthy, same as 19-1's probe strategy for non-state-store components).

#### Subscription Data from EventStore Server Sidecar

**Same remote sidecar pattern as story 19-2.** Use `IHttpClientFactory` with the `"DaprSidecar"` named client (already registered by story 19-2 in `AddAdminServer()`) to call the EventStore server's DAPR sidecar HTTP metadata endpoint: `GET {EventStoreDaprHttpEndpoint}/v1.0/metadata`.

**IMPORTANT: Base address is set per-call, not on the named client.** The `"DaprSidecar"` named HttpClient only has a timeout configured — the base address must be set from `_options.EventStoreDaprHttpEndpoint` on each call. Follow the exact pattern in `DaprInfrastructureQueryService.GetActorRuntimeInfoAsync()`: create client via `_httpClientFactory.CreateClient("DaprSidecar")`, set `httpClient.BaseAddress = new Uri(_options.EventStoreDaprHttpEndpoint)`, then call `GetAsync("v1.0/metadata")`.

Parse the JSON response and extract the `subscriptions` array:
```json
{
  "subscriptions": [
    {
      "pubsubName": "pubsub",
      "topic": "*.*.events",
      "rules": { "rules": [{ "match": "", "path": "/events/handle" }] },
      "deadLetterTopic": "",
      "type": "DECLARATIVE",
      "metadata": {}
    }
  ]
}
```

**Map to `DaprSubscriptionInfo`:**
- `PubSubName` ← `pubsubName`
- `Topic` ← `topic`
- `Route` ← first `rules.rules[].path` value (the subscription handler endpoint)
- `SubscriptionType` ← `type` ("DECLARATIVE" or "PROGRAMMATIC")
- `DeadLetterTopic` ← `deadLetterTopic` (empty string from JSON → null)

**CRITICAL: Also query the admin server's own sidecar** via `DaprClient.GetMetadataAsync()` for any subscriptions registered by the admin server itself (e.g., SignalR projection-changed subscriptions). Merge subscriptions from both sidecars, deduplicating by topic+route. The `DaprMetadata` may or may not expose subscriptions depending on SDK version (see Task 2.0).

**Shared metadata fetch optimization:** Story 19-2's `GetActorRuntimeInfoAsync` also calls `/v1.0/metadata` on the remote sidecar. If both methods are called on the same polling cycle, that's two HTTP round-trips to the same endpoint. If the implementation can share a single metadata fetch (e.g., a private helper that caches the response for a short window, or a shared method that parses both actors and subscriptions from one response), prefer that. If not, two calls are acceptable for this story — consolidation is a future optimization, not a requirement.

**Fallback strategy:** If `EventStoreDaprHttpEndpoint` is null/not configured, subscriptions will be empty with `IsRemoteMetadataAvailable = false`. The page still shows pub/sub component data from the local sidecar.

#### Dead-Letter Count

The existing dead-letter infrastructure is at:
- **Service interface:** `IDaprDeadLetterQueryService` in `Admin.Abstractions/Services/`
- **Implementation:** `DaprDeadLetterQueryService` in `Admin.Server/Services/`
- **Controller:** `AdminDeadLettersController` in `Admin.Server/Controllers/` — `GET /api/v1/admin/dead-letters` returns paginated `DeadLetterEntry` list
- **UI client:** `AdminDeadLetterApiClient` in `Admin.UI/Services/` — has `GetDeadLettersAsync()` returning `PagedResult<DeadLetterEntry>`

**No dedicated count endpoint exists.** Add one — this is cleaner than abusing the paged list endpoint:

1. Add `GetDeadLetterCountAsync(CancellationToken ct)` returning `int` to `IDaprDeadLetterQueryService` (Admin.Abstractions)
2. Implement in `DaprDeadLetterQueryService` (Admin.Server) — query the state store for dead-letter key count
3. Add `GET /api/v1/admin/dead-letters/count` endpoint to `AdminDeadLettersController` returning `int`
4. Add `GetDeadLetterCountAsync(CancellationToken ct)` to `AdminDeadLetterApiClient` (Admin.UI)

These are Tasks 4.3–4.6. Complete them before starting the UI page.

**Approach:** Inject `AdminDeadLetterApiClient` in the `DaprPubSub.razor` page and call it in parallel with the pub/sub overview API via `Task.WhenAll`. Do NOT add dead-letter count to `DaprPubSubOverview` — keep service responsibilities separate.

#### Observability Deep-Link URLs

`AdminServerOptions` has observability URL properties. The dev agent MUST verify the exact property names by reading `src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs` as the first step of implementing AC5. Expected names: `TraceUrl`, `MetricsUrl`, `LogsUrl`. The existing `ObservabilityLinks` model at `Admin.Abstractions/Models/Health/ObservabilityLinks.cs` aggregates these — reuse it. Check the Health page (story 15-7) for the existing deep-link rendering pattern and follow it exactly.

If the observability URL properties do NOT exist in `AdminServerOptions`, add them as part of Task 5.1 (3 nullable string properties). This is the only acceptable fallback — do NOT use environment variables directly.

The deep-link cards should link to the base URL without appending filters. If a URL is null/empty, the card renders as disabled with "Configure observability URL to enable" guidance.

### Model Definitions

#### DaprSubscriptionInfo

```csharp
public record DaprSubscriptionInfo(
    string PubSubName,
    string Topic,
    string Route,
    string SubscriptionType,
    string? DeadLetterTopic);
```

- `PubSubName`: validated non-null/non-empty (the pub/sub component name this subscription uses)
- `Topic`: validated non-null/non-empty (the topic pattern, may contain wildcards like `*.*.events`)
- `Route`: validated non-null/non-empty (the HTTP path the subscriber handles, e.g., `/events/handle`)
- `SubscriptionType`: "DECLARATIVE" or "PROGRAMMATIC" — validated non-null/non-empty
- `DeadLetterTopic`: null if no dead-letter topic configured (empty string from JSON → convert to null in mapping)

#### DaprPubSubOverview

```csharp
public record DaprPubSubOverview(
    IReadOnlyList<DaprComponentDetail> PubSubComponents,
    IReadOnlyList<DaprSubscriptionInfo> Subscriptions,
    bool IsRemoteMetadataAvailable);
```

- `PubSubComponents`: filtered from DAPR metadata where component type starts with `pubsub.`. Reuses existing `DaprComponentDetail` type — do NOT create a new pub/sub-specific model.
- `Subscriptions`: merged from local sidecar (if SDK supports) and remote EventStore server sidecar
- `IsRemoteMetadataAvailable`: true if subscription data was successfully retrieved from the EventStore server sidecar (same flag pattern as 19-2's `DaprActorRuntimeInfo.IsRemoteMetadataAvailable`)

**Note:** `UniqueTopicCount` is NOT in the model — the UI computes it inline: `overview.Subscriptions.Select(s => s.Topic).Distinct().Count()`. One fewer parameter, one fewer test.

### Controller Endpoint

Add to the existing `AdminDaprController` (from story 19-1). The controller already has `[Route("api/v1/admin/dapr")]` and `[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]`.

```csharp
[HttpGet("pubsub")]
[ProducesResponseType(typeof(DaprPubSubOverview), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
public async Task<IActionResult> GetPubSubOverviewAsync(CancellationToken ct)
```

**Note:** Return type is `Task<IActionResult>` (not `Task<ActionResult<T>>`) — matching all existing endpoints in `AdminDaprController`.

- Return `Ok(overview)` on success — even when `IsRemoteMetadataAvailable = false` (partial data is valid, not an error)
- Return `503 ServiceUnavailable` with `ProblemDetails` only when the local DAPR sidecar is completely unreachable (gRPC/HTTP failure)
- Follow the exact error handling pattern from the existing `GetComponentsAsync` and `GetSidecarInfoAsync` endpoints in the same controller
- Include `[ProducesResponseType]` attributes for 200, 401, 403, 503 matching existing endpoint declarations

### UI API Client: AdminPubSubApiClient

**CRITICAL:** All existing Admin UI API clients use **primary constructors** with both `IHttpClientFactory` and `ILogger<T>` parameters. They create the `HttpClient` per-call (not stored in a field). Follow the exact pattern from `AdminDaprApiClient` or `AdminActorApiClient`:

```csharp
// Admin.UI/Services/AdminPubSubApiClient.cs
public class AdminPubSubApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminPubSubApiClient> logger)
{
    public virtual async Task<DaprPubSubOverview?> GetPubSubOverviewAsync(CancellationToken ct = default)
    {
        using HttpResponseMessage response = await httpClientFactory.CreateClient("AdminApi")
            .GetAsync("api/v1/admin/dapr/pubsub", ct).ConfigureAwait(false);
        HandleErrorStatus(response);
        return await response.Content.ReadFromJsonAsync<DaprPubSubOverview>(ct).ConfigureAwait(false);
    }

    private static void HandleErrorStatus(HttpResponseMessage response)
    {
        // Follow exact pattern from AdminDaprApiClient/AdminDeadLetterApiClient:
        // - 401 → throw UnauthorizedAccessException
        // - 403 → throw ForbiddenAccessException (from Admin.UI.Services.Exceptions)
        // - 503 → throw ServiceUnavailableException (from Admin.UI.Services.Exceptions)
        // - Other non-success → throw HttpRequestException
    }
}
```

Register in `Program.cs` after existing API client registrations (after `AdminActorApiClient` registration):
```csharp
builder.Services.AddScoped<AdminPubSubApiClient>();
```

### UI Page: DaprPubSub.razor

Route: `@page "/dapr/pubsub"`

**Injected dependencies:**
- `AdminPubSubApiClient` — for pub/sub overview data
- `AdminDeadLetterApiClient` — for dead-letter count (existing client from story 16-6)
- `NavigationManager` — for navigation and deep linking
- `ILogger<DaprPubSub>` — for exception logging in PollAsync

**Layout (top to bottom):**

1. **Header:** Page title "DAPR Pub/Sub Delivery Metrics" + `FluentAnchor` back-link to `/dapr`

2. **Summary stat cards row:** Four `StatCard` components:
   - Pub/Sub Components: `overview.PubSubComponents.Count`
   - Active Subscriptions: `overview.Subscriptions.Count` (show "N/A" if `IsRemoteMetadataAvailable == false`)
   - Unique Topics: `overview.Subscriptions.Select(s => s.Topic).Distinct().Count()` (computed inline in UI)
   - Dead Letters: `_deadLetterCount` — Healthy severity (green) when 0, Warning severity (yellow) when > 0, neutral "N/A" when null

3. **Pub/sub component section:** `FluentCard` per pub/sub component showing:
   - Component name and type
   - Version (or "N/A" if empty)
   - Health `StatusBadge`
   - Capabilities as `FluentBadge` tags
   - For single component, render as a prominent card. For multiple, arrange in a responsive grid.

4. **Subscription grid:** `FluentDataGrid<DaprSubscriptionInfo>` with columns:
   - Topic (primary sort, ascending)
   - Route
   - PubSub Component
   - Type (Declarative/Programmatic)
   - Dead Letter Topic (show "None" if null)

   `FluentSearch` filter above the grid for topic/route text filtering. When `IsRemoteMetadataAvailable = false`, show `IssueBanner` above the grid with connectivity guidance. When no subscriptions, show inline informational note.

5. **Topic topology card:** Informational `FluentCard` with styled documentation:
   - Event topic pattern: `{tenant}.{domain}.events`
   - Dead-letter topic pattern: `deadletter.{tenant}.{domain}.events`
   - Delivery semantics: "At-least-once · CloudEvents 1.0 · Subscribers must be idempotent"
   - Pub/sub component name from overview data

6. **Publication pipeline card:** `FluentCard` with horizontal `FluentBadge` step indicator:
   - `EventsStored` (blue) → chevron → `EventsPublished` (blue) → chevron → `Completed` (green)
   - Below the success path: branching arrow → `PublishFailed` (red) → "Dead-letter routing"
   - Each stage has a one-line description below the badge
   - CSS Flexbox layout — no charting library needed

7. **Observability deep-link section:** Three `FluentCard` link cards in a row:
   - "Traces" with trace icon → configured trace URL
   - "Metrics" with chart icon → configured metrics URL
   - "Logs" with document icon → configured logs URL
   - Enabled cards: clickable with `FluentAnchor` external link. Disabled cards: muted text "Configure observability URL to enable"

8. **Dead-letter management card:** `FluentCard` with dead-letter count, severity-colored value, and "Manage Dead Letters" `FluentButton` navigating to `/deadletters`.

**PeriodicTimer lifecycle** (same pattern as `DaprComponents.razor` from story 19-1):
```csharp
private readonly CancellationTokenSource _cts = new();
private DaprPubSubOverview? _overview;
private int? _deadLetterCount;
private bool _isLoading = true;
private string? _error;

protected override async Task OnInitializedAsync()
{
    await LoadDataAsync();
    _ = PollAsync(); // fire-and-forget
}

private async Task LoadDataAsync()
{
    // Fetch pub/sub overview and dead-letter count independently — one failure must not block the other
    Task<DaprPubSubOverview?> overviewTask = Task.Run(async () =>
    {
        try { return await PubSubClient.GetPubSubOverviewAsync(_cts.Token); }
        catch (Exception ex) { Logger.LogWarning(ex, "Failed to fetch pub/sub overview"); return null; }
    });
    Task<int?> deadLetterTask = Task.Run(async () =>
    {
        try { return await DeadLetterClient.GetDeadLetterCountAsync(_cts.Token); }
        catch (Exception ex) { Logger.LogWarning(ex, "Failed to fetch dead-letter count"); return null; }
    });
    await Task.WhenAll(overviewTask, deadLetterTask);
    _overview = overviewTask.Result;
    _deadLetterCount = deadLetterTask.Result;
    _isLoading = false;
}

private async Task PollAsync()
{
    using PeriodicTimer timer = new(TimeSpan.FromSeconds(30));
    try
    {
        while (await timer.WaitForNextTickAsync(_cts.Token))
        {
            await InvokeAsync(async () =>
            {
                try { await LoadDataAsync(); StateHasChanged(); }
                catch (ObjectDisposedException) { }
                catch (Exception ex) { Logger.LogWarning(ex, "Poll cycle failed"); }
            });
        }
    }
    catch (OperationCanceledException) { }
}

public async ValueTask DisposeAsync()
{
    await _cts.CancelAsync();
    _cts.Dispose();
}
```

**Note:** The `PollAsync` sample above demonstrates the resilient pattern from story 19-1 code review (P-9): catch ALL non-cancellation exceptions within the loop body, log them, and continue polling. The `LoadDataAsync` sample demonstrates independent error handling per data source (F2) — a dead-letter API failure does NOT prevent pub/sub data from rendering.

**Deep linking:** `[SupplyParameterFromQuery]` with `string? Component` property. If provided, highlight the matching pub/sub component card on load (e.g., add a CSS class for visual emphasis).

### JSON Parsing for Subscription Metadata

The subscription data from the DAPR HTTP metadata endpoint (`/v1.0/metadata`) needs manual JSON parsing since it's not covered by the DAPR SDK's typed models.

Use `System.Text.Json.JsonDocument` or `JsonSerializer.Deserialize<T>` with a DTO:

```csharp
// Internal DTO for deserialization — NOT a public model
// CRITICAL: Use [JsonPropertyName] attributes to match exact DAPR JSON casing.
// Do NOT rely on PropertyNameCaseInsensitive — explicit attributes are safer.
private sealed record DaprMetadataResponse(
    [property: JsonPropertyName("subscriptions")] IReadOnlyList<DaprSubscriptionResponse>? Subscriptions);

private sealed record DaprSubscriptionResponse(
    [property: JsonPropertyName("pubsubName")] string? PubsubName,
    [property: JsonPropertyName("topic")] string? Topic,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("deadLetterTopic")] string? DeadLetterTopic,
    [property: JsonPropertyName("rules")] DaprSubscriptionRulesResponse? Rules);

private sealed record DaprSubscriptionRulesResponse(
    [property: JsonPropertyName("rules")] IReadOnlyList<DaprSubscriptionRuleResponse>? Rules);

private sealed record DaprSubscriptionRuleResponse(
    [property: JsonPropertyName("path")] string? Path);
```

Map to public `DaprSubscriptionInfo`:
- Route = first `Rules.Rules[].Path` value (if rules exist), otherwise `"/"` as default
- DeadLetterTopic = empty string or whitespace → null

### Integration with Story 19-1

Add a "Pub/Sub Metrics" button to `DaprComponents.razor` (created by story 19-1), near the sidecar status card section, adjacent to the "Actor Inspector" button from story 19-2:
```razor
<FluentButton Appearance="Appearance.Outline"
              OnClick="@(() => NavigationManager.NavigateTo("/dapr/pubsub"))">
    Pub/Sub Metrics
</FluentButton>
```

### Reuse Existing Shared Components

Do NOT create new shared components. Reuse:
- `StatCard` — for summary metrics (component count, subscription count, topic count, dead-letter count)
- `StatusBadge` — for component health status
- `IssueBanner` — for errors, unavailability, and stale data warnings
- `EmptyState` — for no pub/sub components
- `SkeletonCard` — for loading state
- `FluentBadge` — for component capabilities display and pipeline stage badges

### Interface Coexistence

`IDaprInfrastructureQueryService` now has methods for: components (19-1), sidecar info (19-1), actor runtime (19-2), actor state (19-2), and pub/sub overview (this story). This is by design — all DAPR infrastructure queries live in one service. Do NOT split into separate interfaces. If the interface grows too large in future stories, refactoring is a separate concern.

### Project Structure Notes

All new files go in existing projects — no new `.csproj` files:

| File | Project | Path |
|------|---------|------|
| `DaprSubscriptionInfo.cs` | Admin.Abstractions | `Models/Dapr/DaprSubscriptionInfo.cs` |
| `DaprPubSubOverview.cs` | Admin.Abstractions | `Models/Dapr/DaprPubSubOverview.cs` |
| `AdminPubSubApiClient.cs` | Admin.UI | `Services/AdminPubSubApiClient.cs` |
| `DaprPubSub.razor` | Admin.UI | `Pages/DaprPubSub.razor` |

**Modified files:**

| File | Change |
|------|--------|
| `IDaprInfrastructureQueryService.cs` | Add `GetPubSubOverviewAsync` method |
| `DaprInfrastructureQueryService.cs` | Implement `GetPubSubOverviewAsync` (subscription retrieval + component filtering) |
| `AdminDaprController.cs` | Add `GetPubSubOverviewAsync` endpoint |
| `IDaprDeadLetterQueryService.cs` | Add `GetDeadLetterCountAsync` method |
| `DaprDeadLetterQueryService.cs` | Implement `GetDeadLetterCountAsync` |
| `AdminDeadLettersController.cs` | Add `GET /api/v1/admin/dead-letters/count` endpoint |
| `AdminDeadLetterApiClient.cs` | Add `GetDeadLetterCountAsync` method |
| `DaprComponents.razor` | Add "Pub/Sub Metrics" button (next to "Actor Inspector") |
| `Program.cs` (Admin.UI) | Register `AdminPubSubApiClient` |
| `AdminServerOptions.cs` | Verify observability URL properties exist — add if missing |

**Test files:**

| File | Project | Path |
|------|---------|------|
| `DaprSubscriptionInfoTests.cs` | Admin.Abstractions.Tests | `Models/Dapr/` |
| `DaprPubSubOverviewTests.cs` | Admin.Abstractions.Tests | `Models/Dapr/` |
| `DaprPubSubQueryServiceTests.cs` | Admin.Server.Tests | `Services/` |
| `AdminDaprControllerPubSubTests.cs` | Admin.Server.Tests | `Controllers/` |
| `DaprPubSubPageTests.cs` | Admin.UI.Tests | `Pages/` |

### Testing Standards

- **Framework:** xUnit 2.9.3, **Assertions:** Shouldly 4.3.0, **Mocking:** NSubstitute 5.3.0
- Follow existing test patterns from story 19-1 and 19-2 test files
- **Model tests:** Validate constructor argument validation (null/empty strings throw `ArgumentException`). Test `DaprSubscriptionInfo` with empty `DeadLetterTopic` string → should accept (mapping to null is at the service layer, not the model). Test `DaprPubSubOverview` construction with valid component list and subscription list.
- **Dead-letter count tests:** Test `GetDeadLetterCountAsync` service implementation (mock state store, return count). Test controller endpoint returns `int`. Test API client method.
- **Service tests:** Mock `DaprClient` (for `GetMetadataAsync`), `IHttpClientFactory` (for remote metadata fetch with subscription JSON), `IOptions<AdminServerOptions>`, `ILogger<T>` via NSubstitute. Test cases:
  - Happy path: local pub/sub components + remote subscriptions
  - Remote unavailable: `EventStoreDaprHttpEndpoint` is null → empty subscriptions, `IsRemoteMetadataAvailable = false`
  - Remote timeout/error: HTTP call fails → empty subscriptions, `IsRemoteMetadataAvailable = false`, no exception thrown
  - No pub/sub components: metadata has components but none with `pubsub.*` type → empty `PubSubComponents`
  - Subscription JSON parsing: valid JSON with multiple subscriptions, empty subscriptions array, malformed JSON (graceful handling)
  - Subscriptions key absent from JSON response entirely (not empty array, but missing key) → treat as empty list, not error. The DTO uses nullable `IReadOnlyList?` — verify the service handles `null` vs empty list consistently.
  - Empty `deadLetterTopic` in JSON → `null` in model
- **Controller tests:** Mock `IDaprInfrastructureQueryService` and `ILogger<T>`. Test: 200 OK with full data, 200 OK with partial data (remote unavailable), 503 when sidecar unreachable.
- **UI page tests:** Follow patterns from story 19-1's `DaprComponentsPageTests.cs`. Test: deep linking parameter parsing (`?component=pubsub`), empty states (no components, no subscriptions), remote metadata unavailable banner, subscription grid rendering, dead-letter count display.

### Previous Story Intelligence (Stories 19-1 and 19-2)

Key learnings from preceding stories:

- **Story 19-1 (`DaprComponents.razor`):** Established the `PeriodicTimer` pattern for DAPR pages, `DaprInfrastructureQueryService` metadata retrieval, `AdminDaprController` REST pattern, `FluentDataGrid` component grid, `SkeletonCard` → loaded content transition, and `EmptyState` patterns. **Follow exactly.**
- **Story 19-2 (`DaprActors.razor`):** Established the remote sidecar HTTP metadata endpoint pattern (`EventStoreDaprHttpEndpoint` + `/v1.0/metadata`), `"DaprSidecar"` named HttpClient registration in `AddAdminServer()`, `AdminActorApiClient` API client separation, and sub-page navigation from `DaprComponents.razor`. **Follow the same remote metadata approach for subscriptions.**
- **Story 19-1 debug log:** DAPR SDK 1.16.1 did NOT expose `Subscriptions` on `DaprMetadata` — this is why we need the HTTP endpoint fallback for subscription data. Verify current SDK version before coding.
- **Story 19-1 debug log:** `RuntimeVersion` is in `DaprMetadata.Extended["daprRuntimeVersion"]`, not a direct property.
- **Story 19-1 code review fix (P-9):** `PollAsync` must be resilient to exceptions — catch and log within the loop body, do not let the polling loop die.
- **Story 19-1 code review fix (P-1):** Pass cancellation token through to all async calls including health probes.
- **Interface extension impact:** Adding methods to `IDaprInfrastructureQueryService` does not break existing NSubstitute mocks (unconfigured methods return default values), but verify all existing tests pass after the extension.
- **Button placement on DaprComponents.razor:** Story 19-2 added "Actor Inspector" button. Add "Pub/Sub Metrics" adjacent to it (same row or section). Check current layout to ensure buttons don't overflow.

### Git Intelligence

Recent commits established:
- `308690e`: Story 19-1 merged — DAPR component status dashboard foundation
- `e57ec0a`: `feat: add DAPR component status dashboard (story 19-1)` — the implementation commit
- `Admin.Abstractions/Models/Dapr/` subfolder for DAPR-specific models
- `IDaprInfrastructureQueryService` with `GetComponentsAsync` and `GetSidecarInfoAsync`
- `AdminDaprController` REST controller at `api/v1/admin/dapr`
- `"DaprSidecar"` named HttpClient pattern for remote sidecar calls (19-2)
- `AdminServerOptions.EventStoreDaprHttpEndpoint` for cross-sidecar metadata (19-2)
- `PeriodicTimer` page lifecycle pattern in Admin.UI DAPR pages
- NSubstitute mock patterns for `DaprClient.GetMetadataAsync()` and `IHttpClientFactory`

### References

- [Source: src/Hexalith.EventStore.Admin.Abstractions/Services/IDaprInfrastructureQueryService.cs] — Service interface to extend
- [Source: src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs] — Service implementation to extend
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminDaprController.cs] — Controller to extend
- [Source: src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor] — Sister page pattern and button integration point (story 19-1)
- [Source: src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor] — Sister page pattern (story 19-2)
- [Source: src/Hexalith.EventStore.Server/Events/EventPublisher.cs] — Event publication logic, topic derivation, pub/sub component usage
- [Source: src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs] — Dead-letter topic naming and routing pattern
- [Source: src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs] — `PubSubName` and `DeadLetterTopicPrefix` configuration
- [Source: src/Hexalith.EventStore.CommandApi/HealthChecks/DaprPubSubHealthCheck.cs] — Pub/sub health check pattern (metadata component lookup)
- [Source: src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs] — Publication pipeline stages (EventsStored, EventsPublished, Completed, PublishFailed)
- [Source: deploy/dapr/pubsub-kafka.yaml] — DAPR pub/sub component configuration, topic patterns, scoping
- [Source: deploy/dapr/resiliency.yaml] — DAPR resiliency policies for pub/sub (retry, circuit breaker)
- [Source: deploy/dapr/subscription-projection-changed.yaml] — Declarative subscription example
- [Source: src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs] — API client pattern to follow
- [Source: src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor] — Navigation context
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/StatusBadge.razor] — Status display component
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor] — Metrics card component
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/IssueBanner.razor] — Error/warning banner
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/EmptyState.razor] — Empty state component
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/SkeletonCard.razor] — Loading state component
- [Source: src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs] — Options class (may need observability URL properties)
- [Source: _bmad-output/implementation-artifacts/19-1-dapr-component-status-dashboard.md] — Previous story reference
- [Source: _bmad-output/implementation-artifacts/19-2-dapr-actor-inspector.md] — Previous story reference
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 19] — Epic definition (FR75 DAPR portion)
- [Source: _bmad-output/planning-artifacts/architecture.md#D6] — Pub/sub topic naming convention
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-P5] — Deep-link strategy for observability tools
- [Source: _bmad-output/planning-artifacts/prd.md#FR75] — Operational health dashboard requirement
- [Source: _bmad-output/planning-artifacts/prd.md#FR18] — At-least-once delivery guarantee
- [Source: _bmad-output/planning-artifacts/prd.md#NFR5] — Pub/sub delivery latency target (<50ms p99)
- [Source: _bmad-output/planning-artifacts/prd.md#NFR24] — Pub/sub recovery — no events silently dropped

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- DAPR SDK 1.16.1 confirmed: `DaprMetadata.Subscriptions` NOT exposed — HTTP endpoint fallback used for subscription data
- `DaprMetadata` is sealed — service tests must use constructor `new DaprMetadata(id:, actors:, extended:, components:)`, not NSubstitute mocking

### Completion Notes List

- Task 1: Created `DaprSubscriptionInfo` and `DaprPubSubOverview` immutable records with constructor validation following existing model patterns
- Task 2: Verified DAPR SDK 1.16.1 (subscriptions not in SDK). Added `GetPubSubOverviewAsync` to interface and implemented using local `GetMetadataAsync()` for pub/sub components + remote HTTP `/v1.0/metadata` endpoint for subscriptions via "DaprSidecar" named HttpClient
- Task 3: Added `GET /api/v1/admin/dapr/pubsub` endpoint to `AdminDaprController` with ReadOnly policy, matching existing error handling patterns (503/500)
- Task 4: Created `AdminPubSubApiClient` with same pattern as `AdminActorApiClient`. Added `GetDeadLetterCountAsync` to `IDeadLetterQueryService` interface, implemented in `DaprDeadLetterQueryService` (reads "admin:dead-letters:all" index count), added `GET /api/v1/admin/dead-letters/count` endpoint, added client method to `AdminDeadLetterApiClient`
- Task 5: Created `DaprPubSub.razor` page with all 9 ACs: stat cards, component cards, subscription grid with search filter, topic topology reference (collapsible), publication pipeline visualization, observability deep-link cards, dead-letter management card, 30s PeriodicTimer polling, empty/error states, deep linking via `?component=` query parameter. Added "Pub/Sub Metrics" button to `DaprComponents.razor` adjacent to "Actor Inspector"
- Task 6: 43 new tests across 5 test files — all pass. Full regression suite: 1,728 tests pass with zero failures

### Review Findings

- [x] [Review][Decision] **Dead-letter count masks state store failures as "0 dead letters"** — Fixed via Option A: removed catch-all returning 0 in `DaprDeadLetterQueryService`; exceptions now propagate to controller (returns 503) → client returns null → UI shows "N/A"
- [x] [Review][Patch] **Task.Run wrapping I/O-bound async calls in LoadDataAsync** — Fixed: replaced Task.Run with local async functions in Task.WhenAll
- [x] [Review][Patch] **JSON subscriptions ValueKind not checked before EnumerateArray** — Fixed: added `ValueKind == JsonValueKind.Array` guard
- [x] [Review][Patch] **FilteredSubscriptions recomputes LINQ chain on every access** — Fixed: added `_filteredSubscriptionsCache` field with invalidation on data/filter change
- [x] [Review][Patch] **Observability URLs fetched every 30s via full SystemHealthReport** — Fixed: moved to `LoadObservabilityLinksAsync()` called once in OnInitializedAsync
- [x] [Review][Patch] **HTTP endpoint check uses IsNullOrEmpty instead of IsNullOrWhiteSpace** — Fixed
- [x] [Review][Patch] **CSS --accent-fill-rest-rgb custom property may not exist in all Fluent UI themes** — Fixed: replaced with safe `var(--accent-fill-rest)` token
- [x] [Review][Patch] **Missing test: malformed JSON from remote sidecar** — Fixed: added `GetPubSubOverviewAsync_HandlesGracefully_WhenRemoteReturnsMalformedJson` test
- [x] [Review][Patch] **Missing test: empty DeadLetterTopic string acceptance at model level** — Fixed: added `Constructor_WithEmptyDeadLetterTopic_CreatesInstance` test
- [x] [Review][Patch] **Missing test: observability deep-link rendering when URLs configured** — Fixed: added `PubSubPage_RendersObservabilityLinks_WhenUrlsConfigured` test with configured mock
- [x] [Review][Patch] **Auth exceptions silently swallowed in dead-letter client flow** — Skipped: requires global auth error handling strategy (Blazor error boundary); not a standalone fix
- [x] [Review][Defer] **DisposeAsync doesn't await in-flight tasks / no double-disposal guard** — pre-existing pattern from DaprComponents.razor and DaprActors.razor
- [x] [Review][Defer] **DisposeAsync doesn't call GC.SuppressFinalize** — pre-existing pattern across all admin UI pages
- [x] [Review][Defer] **Unconsumed response body on HandleErrorStatus error prevents HTTP/1.1 connection reuse** — pre-existing pattern across all API clients
- [x] [Review][Defer] **Dead-letter state store loads full List<DeadLetterEntry> index for count** — pre-existing architecture from ListDeadLettersAsync; DAPR state store index design

### Change Log

- 2026-03-26: Story 19-3 implementation complete — all 6 tasks done, all ACs satisfied, 43 new tests, zero regressions

### File List

**New files:**
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprSubscriptionInfo.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprPubSubOverview.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminPubSubApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprSubscriptionInfoTests.cs`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprPubSubOverviewTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprPubSubQueryServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDaprControllerPubSubTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDeadLettersControllerCountTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprPubSubPageTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IDaprInfrastructureQueryService.cs` — added `GetPubSubOverviewAsync`
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IDeadLetterQueryService.cs` — added `GetDeadLetterCountAsync`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs` — implemented `GetPubSubOverviewAsync`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprDeadLetterQueryService.cs` — implemented `GetDeadLetterCountAsync`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDaprController.cs` — added pub/sub overview endpoint
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDeadLettersController.cs` — added dead-letter count endpoint
- `src/Hexalith.EventStore.Admin.UI/Services/AdminDeadLetterApiClient.cs` — added `GetDeadLetterCountAsync`
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor` — added "Pub/Sub Metrics" button
- `src/Hexalith.EventStore.Admin.UI/Program.cs` — registered `AdminPubSubApiClient`
