# Story 19.1: DAPR Component Status Dashboard

Status: done

Size: Medium-Large — Creates new `DaprComponentDetail` and `DaprSidecarInfo` models in Admin.Abstractions, new `IDaprInfrastructureQueryService` interface, `DaprInfrastructureQueryService` DAPR-backed implementation, new `AdminDaprController` REST controller with 2 endpoints, new `AdminDaprApiClient` UI HTTP client, new `DaprComponents.razor` dedicated dashboard page with sidecar status card + grouped component grid + expandable detail rows, NavMenu link addition, self-contained PeriodicTimer polling in the page, creates ~8–10 test classes across 4 test projects (~50–70 tests, ~10–12 hours estimated). Delivers the foundational DAPR infrastructure visibility page that subsequent stories (19-2 through 19-5) will extend.

**Dependency:** Epics 14 and 15 must be complete (both are `done`). The existing `DaprComponentHealth` model, `DaprHealthQueryService`, `AdminHealthController`, and `Health.razor` page from stories 14-x and 15-7 are the baseline. This story does **not** modify the existing Health page — it creates a new dedicated DAPR infrastructure page alongside it.

## Definition of Done

- All acceptance criteria verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- New `/dapr` page renders with sidecar info and component grid
- New REST endpoints return structured JSON from DAPR metadata API
- NavMenu includes "DAPR" link between "Health" and "Dead Letters"
- All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change
- Component type grouping and filtering works correctly
- Expandable detail rows show component capabilities and version

## Story

As a **platform operator or DBA using the Hexalith EventStore Admin UI**,
I want **a dedicated DAPR infrastructure dashboard showing sidecar status, all registered components with their types, versions, scopes, and per-component health probes**,
so that **I can understand the DAPR runtime topology, quickly identify misconfigured or unhealthy components, and have a foundation for deeper DAPR diagnostics (actor inspection, pub/sub metrics, resiliency policies) in subsequent stories**.

## Acceptance Criteria

1. **AC1: Sidecar status card** — The dashboard displays a card showing DAPR sidecar app ID, runtime version, number of registered components, number of active subscriptions, and number of HTTP endpoints, all retrieved from `DaprClient.GetMetadataAsync()`. (Note: DAPR sidecar ports are not exposed via the metadata API — they are runtime configuration and out of scope.)
2. **AC2: Component grid sorted by category** — All DAPR components are listed in a single `FluentDataGrid` sorted by `DaprComponentCategory` (ascending enum ordinal) then by `ComponentName` (ascending alphabetical). Columns: Category, Component Name, Type (e.g., `state.redis`), Version, Status. The Category column uses the default sort. Note: `FluentDataGrid` does not support native row grouping — sorting by category achieves the visual grouping effect.
3. **AC3: Component type filter** — A `FluentSearch` input filters the component grid by name or type. A `FluentSelect` dropdown filters by component category (All, State Store, Pub/Sub, Binding, etc.).
4. **AC4: Expandable component detail** — Clicking a component row sets a `_selectedComponent` state field and renders a `FluentCard` detail panel below the grid (conditional `@if (_selectedComponent is not null)`). Clicking the same row again or clicking a different row toggles/swaps selection. The detail panel shows the component's capabilities list (as `FluentBadge` tags) and version. (Note: The DAPR metadata API via `DaprComponentsMetadata` exposes `Name`, `Type`, `Version`, and `Capabilities` — it does not expose per-component configuration key-value pairs. `FluentDataGrid` does not support native expandable rows — use the selected-row + detail-panel-below pattern.)
5. **AC5: Per-component health probe** — Each component shows a health status (Healthy/Degraded/Unhealthy) via the existing `StatusBadge` component. State store components are probed via a DAPR state get; pub/sub components show Healthy if present in metadata; other types show Healthy if registered.
6. **AC6: REST endpoints** — `GET /api/v1/admin/dapr/components` returns `IReadOnlyList<DaprComponentDetail>`. `GET /api/v1/admin/dapr/sidecar` returns `DaprSidecarInfo`. Both require `ReadOnly` authorization policy.
7. **AC7: Auto-refresh** — The page uses its own `PeriodicTimer`-based 30-second polling loop (do NOT extend `DashboardRefreshService` — that would require modifying `DashboardData` and impacting `Index.razor` and `Health.razor` consumers). A manual "Refresh" button triggers an immediate data fetch. Stale data and API unavailability show `IssueBanner`.
8. **AC8: Navigation** — A "DAPR" nav link with `Icons.Regular.Size20.PlugDisconnected` icon appears in `NavMenu.razor` between "Health" and "Dead Letters". Route is `/dapr`.
9. **AC9: Empty state** — When no DAPR components are detected (sidecar not available), an `EmptyState` component renders with title "No DAPR components detected" and a description guiding the operator to check sidecar connectivity.
10. **AC10: Deep linking** — The page supports URL query parameter `?type=state` to pre-filter by component category on load. URL values use lowercase shortened names mapped to enum values: `state` → StateStore, `pubsub` → PubSub, `binding` → Binding, `configuration` → Configuration, `lock` → Lock, `secretstore` → SecretStore, `middleware` → Middleware. Use `[SupplyParameterFromQuery]` with a `string? Type` property and a `private static readonly Dictionary<string, DaprComponentCategory>` for the mapping. Unknown values are ignored (show all).

## Tasks / Subtasks

- [x] Task 1: Create new models in Admin.Abstractions (AC: #1, #2, #4, #6)
  - [x] 1.1 Create `DaprComponentDetail` record in `Models/Dapr/DaprComponentDetail.cs`
  - [x] 1.2 Create `DaprSidecarInfo` record in `Models/Dapr/DaprSidecarInfo.cs`
  - [x] 1.3 Create `DaprComponentCategory` enum in `Models/Dapr/DaprComponentCategory.cs`
  - [x] 1.4 Create `DaprComponentCategoryHelper` static class in `Models/Dapr/DaprComponentCategoryHelper.cs`
- [x] Task 2: Create service interface and implementation (AC: #1, #2, #5, #6)
  - [x] 2.1 Create `IDaprInfrastructureQueryService` in `Services/IDaprInfrastructureQueryService.cs`
  - [x] 2.2 Create `DaprInfrastructureQueryService` in Admin.Server `Services/DaprInfrastructureQueryService.cs`
  - [x] 2.3 Register `IDaprInfrastructureQueryService` → `DaprInfrastructureQueryService` in `AddAdminServer()` method in `src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs` (line ~138, after existing service registrations)
- [x] Task 3: Create REST controller (AC: #6)
  - [x] 3.1 Create `AdminDaprController` in Admin.Server `Controllers/AdminDaprController.cs`
- [x] Task 4: Create UI API client (AC: #6, #7)
  - [x] 4.1 Create `AdminDaprApiClient` in Admin.UI `Services/AdminDaprApiClient.cs`
  - [x] 4.2 Register `AdminDaprApiClient` as scoped in `src/Hexalith.EventStore.Admin.UI/Program.cs` (line ~43, after existing API client registrations)
- [x] Task 5: Create DAPR dashboard page (AC: #1, #2, #3, #4, #7, #8, #9, #10)
  - [x] 5.1 Create `DaprComponents.razor` page in Admin.UI `Pages/`
  - [x] 5.2 Add NavMenu link
- [x] Task 6: Write tests (all ACs)
  - [x] 6.1 Model tests in Admin.Abstractions.Tests
  - [x] 6.2 Service tests in Admin.Server.Tests
  - [x] 6.3 Controller tests in Admin.Server.Tests
  - [x] 6.4 UI page tests in Admin.UI.Tests

## Dev Notes

### Architecture Compliance

This story follows the **exact same architecture** as the existing Health dashboard (story 15-7) and all Epic 14/15 patterns:

- **Models:** Immutable C# `record` types with constructor validation (`ArgumentException` / `ArgumentNullException`). Located in `Admin.Abstractions/Models/Dapr/` subfolder (new subfolder, parallel to `Models/Health/`).
- **Service interface:** Located in `Admin.Abstractions/Services/`. Async methods returning model types, `CancellationToken ct = default` parameter.
- **DAPR service:** Located in `Admin.Server/Services/`. Uses `DaprClient` injected via constructor. Uses `IOptions<AdminServerOptions>` for config. Uses `IAdminAuthContext` for JWT forwarding. Uses `ILogger<T>` for structured logging.
- **Controller:** Located in `Admin.Server/Controllers/`. Allman braces, `[ApiController]`, `[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]`, `[Route("api/v1/admin/dapr")]`, `[Tags("Admin - DAPR")]`. Uses `ProblemDetails` for errors. Follows `AdminHealthController` pattern exactly.
- **UI API client:** Located in `Admin.UI/Services/`. Uses `IHttpClientFactory` with `"AdminApi"` named client. Virtual async methods for testability. `HandleErrorStatus(response)` pattern from `AdminStreamApiClient`.
- **Page:** Located in `Admin.UI/Pages/`. `@page "/dapr"`. Injects `AdminDaprApiClient` and `NavigationManager`. Implements `IAsyncDisposable`. Manages its own `PeriodicTimer`-based 30-second polling loop (do NOT touch `DashboardRefreshService` or `DashboardData` — those serve `Index.razor` and `Health.razor` and adding DAPR data would be a cross-cutting change). Uses `FluentDataGrid`, `StatCard`, `StatusBadge`, `IssueBanner`, `EmptyState`, `SkeletonCard` shared components.

**PeriodicTimer lifecycle pattern** (must follow exactly):
```csharp
private readonly CancellationTokenSource _cts = new();
private bool _disposed;

protected override async Task OnInitializedAsync()
{
    await LoadDataAsync();
    _ = PollAsync(); // fire-and-forget
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
            });
        }
    }
    catch (OperationCanceledException) { }
}

public async ValueTask DisposeAsync()
{
    _disposed = true;
    await _cts.CancelAsync();
    _cts.Dispose();
}
```

### Key DAPR API: GetMetadataAsync()

The `DaprClient.GetMetadataAsync()` call returns a `DaprMetadata` object containing:
- `Id` — DAPR app ID
- `RuntimeVersion` — DAPR runtime version string
- `Components` — `IReadOnlyList<DaprComponentsMetadata>` with `Name`, `Type`, `Version`, `Capabilities`
- `Subscriptions` — active pub/sub subscriptions
- `HttpEndpoints` — registered HTTP endpoints
- `ActorRuntime` — actor runtime info (used in story 19-2)

This is the **sole data source** for this story. Do NOT call the DAPR HTTP metadata API directly — use only `DaprClient.GetMetadataAsync()`.

**SDK verification required:** Before coding, verify that `DaprComponentsMetadata` in DAPR SDK 1.17.0 exposes a `Capabilities` property via IntelliSense or SDK source. If the property is absent in this SDK version, populate `DaprComponentDetail.Capabilities` with an empty list and add a `// TODO: story 19-5 — populate capabilities when SDK supports it` comment.

### DaprComponentDetail Model

```csharp
public record DaprComponentDetail(
    string ComponentName,
    string ComponentType,
    DaprComponentCategory Category,
    string Version,
    HealthStatus Status,
    DateTimeOffset LastCheckUtc,
    IReadOnlyList<string> Capabilities)
```

- `ComponentName`, `ComponentType`: validated non-null/non-empty in constructor (same pattern as `DaprComponentHealth`).
- `Category`: derived from `ComponentType` prefix — `state.*` → StateStore, `pubsub.*` → PubSub, `bindings.*` → Binding, `configuration.*` → Configuration, `lock.*` → Lock, `secretstores.*` → SecretStore, `middleware.*` → Middleware, other → Unknown.
- `Version`: from `DaprComponentsMetadata.Version` (may be empty string for components that don't report a version).
- `Capabilities`: from `DaprComponentsMetadata.Capabilities` (may be empty list).
- `Status`: determined by health probing strategy (see below).

### DaprSidecarInfo Model

```
record DaprSidecarInfo(
    string AppId,
    string RuntimeVersion,
    int ComponentCount,
    int SubscriptionCount,
    int HttpEndpointCount)
```

Populated from `DaprMetadata.Id`, `DaprMetadata.RuntimeVersion`, and counts of the respective collections.

### DaprComponentCategory Enum

```csharp
public enum DaprComponentCategory
{
    Unknown,
    StateStore,
    PubSub,
    Binding,
    Configuration,
    Lock,
    SecretStore,
    Middleware,
}
```

Create a companion static class `DaprComponentCategoryHelper` in a **separate file** (`Models/Dapr/DaprComponentCategoryHelper.cs`) to avoid SA1402 (multiple types in one file):

```csharp
public static class DaprComponentCategoryHelper
{
    public static DaprComponentCategory FromComponentType(string? componentType)
    {
        if (string.IsNullOrEmpty(componentType))
        {
            return DaprComponentCategory.Unknown;
        }

        string prefix = componentType.Split('.')[0];
        return prefix switch
        {
            "state" => DaprComponentCategory.StateStore,
            "pubsub" => DaprComponentCategory.PubSub,
            "bindings" => DaprComponentCategory.Binding,
            "configuration" => DaprComponentCategory.Configuration,
            "lock" => DaprComponentCategory.Lock,
            "secretstores" => DaprComponentCategory.SecretStore,
            "middleware" => DaprComponentCategory.Middleware,
            _ => DaprComponentCategory.Unknown,
        };
    }
}
```

### Expandable Detail Content

The expandable detail row shows two lists:
- **Capabilities:** String list from `DaprComponentsMetadata.Capabilities` (e.g., `["ETAG", "TRANSACTIONAL", "TTL"]` for state stores). Display as `FluentBadge` tags.
- **Scopes:** String list of DAPR app IDs the component is scoped to. If the DAPR metadata does not directly expose scopes per component, show "All (no scope restriction)" as a default.

No credential redaction is needed — the DAPR metadata API does not expose configuration secrets.

### Reuse Existing Shared Components

Do NOT create new shared components. Reuse:
- `StatCard` — for sidecar info cards (App ID, Runtime Version, Components, Subscriptions)
- `StatusBadge` — for per-component health status
- `IssueBanner` — for API unavailability and stale data
- `EmptyState` — for no-components state
- `SkeletonCard` — for loading state

### Interface Coexistence: IHealthQueryService vs IDaprInfrastructureQueryService

These two interfaces coexist — do NOT refactor or merge them:
- **`IHealthQueryService`** (existing, story 14-1): Returns `SystemHealthReport` with the minimal `DaprComponentHealth` list. Used by `Health.razor` and `DashboardRefreshService` for the system-wide health overview. Has `GetDaprComponentStatusAsync()` returning basic name/type/status.
- **`IDaprInfrastructureQueryService`** (new, this story): Returns the richer `DaprComponentDetail` list and `DaprSidecarInfo`. Used only by `DaprComponents.razor` for the dedicated DAPR infrastructure page. Has `GetComponentsAsync()` and `GetSidecarInfoAsync()`.

Both services call `DaprClient.GetMetadataAsync()` internally. This is acceptable duplication — caching or deduplication is a future optimization, not a requirement for this story.

### Health Probing Strategy

Per-component health probing is **intentionally minimal** in this story. Story 19-5 (DAPR Component Health History) will add deep per-component probing with historical tracking. Do NOT over-engineer probes here.

- **State store** (`state.*`): Probe via `DaprClient.GetStateAsync<string>(componentName, "admin:dapr-probe", ct)` — Healthy on success, Unhealthy on exception. A missing key returns `null` (not an exception), which counts as Healthy (the state store responded).
- **All other types**: Mark as Healthy if present in DAPR metadata (registration = reachable).
- Do NOT block the page on probe results — run probes in parallel with `Task.WhenAll` and a 3-second timeout per probe.

### NavMenu Integration

Add the new DAPR link in `NavMenu.razor` between the existing "Health" and "Dead Letters" links:
```razor
<FluentNavLink Href="/dapr" Icon="@(new Icons.Regular.Size20.PlugDisconnected())">DAPR</FluentNavLink>
```
**Icon fallback:** If `Icons.Regular.Size20.PlugDisconnected` does not exist in the installed FluentUI Icons package, use `Icons.Regular.Size20.PlugConnectedSettings` or `Icons.Regular.Size20.CloudFlow` instead. Verify via IntelliSense.

### Project Structure Notes

All new files go in existing projects — no new `.csproj` files:

| File | Project | Path |
|------|---------|------|
| `DaprComponentDetail.cs` | Admin.Abstractions | `Models/Dapr/DaprComponentDetail.cs` |
| `DaprSidecarInfo.cs` | Admin.Abstractions | `Models/Dapr/DaprSidecarInfo.cs` |
| `DaprComponentCategory.cs` | Admin.Abstractions | `Models/Dapr/DaprComponentCategory.cs` |
| `DaprComponentCategoryHelper.cs` | Admin.Abstractions | `Models/Dapr/DaprComponentCategoryHelper.cs` |
| `IDaprInfrastructureQueryService.cs` | Admin.Abstractions | `Services/IDaprInfrastructureQueryService.cs` |
| `DaprInfrastructureQueryService.cs` | Admin.Server | `Services/DaprInfrastructureQueryService.cs` |
| `AdminDaprController.cs` | Admin.Server | `Controllers/AdminDaprController.cs` |
| `AdminDaprApiClient.cs` | Admin.UI | `Services/AdminDaprApiClient.cs` |
| `DaprComponents.razor` | Admin.UI | `Pages/DaprComponents.razor` |
| `DaprComponentDetailTests.cs` | Admin.Abstractions.Tests | `Models/Dapr/` |
| `DaprSidecarInfoTests.cs` | Admin.Abstractions.Tests | `Models/Dapr/` |
| `DaprComponentCategoryHelperTests.cs` | Admin.Abstractions.Tests | `Models/Dapr/` |
| `DaprInfrastructureQueryServiceTests.cs` | Admin.Server.Tests | `Services/` |
| `AdminDaprControllerTests.cs` | Admin.Server.Tests | `Controllers/` |
| `DaprComponentsPageTests.cs` | Admin.UI.Tests | `Pages/` |

### Testing Standards

- **Framework:** xUnit 2.9.3, **Assertions:** Shouldly 4.3.0, **Mocking:** NSubstitute 5.3.0
- Follow existing test patterns from `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthQueryServiceTests.cs` and `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminHealthControllerTests.cs`
- Service tests: mock `DaprClient`, `IOptions<AdminServerOptions>`, `IAdminAuthContext`, `ILogger<T>` via NSubstitute
- Controller tests: mock `IDaprInfrastructureQueryService` and `ILogger<T>`
- Model tests: validate constructor argument validation (null/empty strings throw `ArgumentException`)
- `DaprComponentCategoryHelper` tests: cover `FromComponentType` edge cases — null input → Unknown, empty string → Unknown, unknown prefix (e.g., `"workflow.temporal"`) → Unknown, all valid prefixes (`"state.redis"` → StateStore, `"pubsub.kafka"` → PubSub, etc.)
- UI page tests: follow patterns from `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/HealthPageTests.cs`

### Previous Story Intelligence (Story 18-5)

Key learnings from the most recent story:
- **Partial classes**: When extending clients, use partial class files (e.g., `AdminApiClient.Tenants.cs`) — do NOT create monolithic classes
- **ToolHelper patterns**: Follow established error handling patterns (`HandleHttpException` with status code mapping)
- **Test patterns**: Use `MockHttpMessageHandler` for HTTP client tests; use `NSubstitute.Received()` for service call verification
- **DI registration**: Register new services in the existing `AddAdmin*` extension methods — do NOT create new extension method files unless necessary

### Git Intelligence

Recent commits (stories 18-1 through 18-5) established:
- `AdminApiClient` partial class pattern in Admin.Mcp project
- `ToolHelper` utility for standardized error handling
- NSubstitute mock patterns for `DaprClient` metadata calls
- Sprint status update conventions

### References

- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Health/DaprComponentHealth.cs] — Existing minimal model
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Services/IHealthQueryService.cs] — Existing health service interface
- [Source: src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs] — Existing DAPR metadata usage pattern
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminHealthController.cs] — Controller pattern to follow
- [Source: src/Hexalith.EventStore.Admin.UI/Pages/Health.razor] — Page pattern to follow
- [Source: src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs] — API client pattern to follow
- [Source: src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor] — Navigation integration point
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/StatusBadge.razor] — Status display component
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor] — Metrics card component
- [Source: src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs] — DI registration (`AddAdminServer()` method)
- [Source: src/Hexalith.EventStore.Admin.UI/Program.cs] — UI DI registration (API client registrations at line ~34-43)
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 19] — Epic definition (FR75 DAPR portion)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Fixed collection-modified-during-enumeration bug in `DaprInfrastructureQueryService.GetComponentsAsync` by switching from `List<T>` + `foreach` to `DaprComponentDetail[]` + `for` loop for health probe mutations.
- DAPR SDK 1.16.1 does not expose `RuntimeVersion`, `Subscriptions`, or `HttpEndpoints` on `DaprMetadata`. `RuntimeVersion` is retrieved from `DaprMetadata.Extended["daprRuntimeVersion"]`; Subscriptions and HttpEndpoints return 0.
- `FluentDataGrid` `RowClick` parameter is `OnRowClick` (not `RowClick`).
- Removed unused `_disposed` field from `DaprComponents.razor` (warnings-as-errors).
- `Icons.Regular.Size20.PlugDisconnected` compiled successfully with the installed FluentUI package.

### Completion Notes List

- All 6 tasks completed with all subtasks checked.
- 4 new model types created: `DaprComponentDetail`, `DaprSidecarInfo`, `DaprComponentCategory`, `DaprComponentCategoryHelper`.
- `IDaprInfrastructureQueryService` interface + `DaprInfrastructureQueryService` DAPR-backed implementation with state store health probing.
- `AdminDaprController` REST controller with GET `/api/v1/admin/dapr/components` and GET `/api/v1/admin/dapr/sidecar`.
- `AdminDaprApiClient` UI HTTP client registered as scoped in `Program.cs`.
- `DaprComponents.razor` page at `/dapr` with: sidecar stat cards (AC1), component grid sorted by category (AC2), text/category filter (AC3), expandable detail panel (AC4), per-component health probe (AC5), auto-refresh via PeriodicTimer (AC7), NavMenu link (AC8), empty state (AC9), deep linking via `?type=` query param (AC10).
- 34 new tests: 12 model tests (DaprComponentDetail, DaprSidecarInfo, DaprComponentCategoryHelper), 10 service tests, 5 controller tests, 7 UI page tests.
- All 1,576 Tier 1 tests pass with zero regressions.

### File List

- src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprComponentCategory.cs (new)
- src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprComponentCategoryHelper.cs (new)
- src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprComponentDetail.cs (new)
- src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprSidecarInfo.cs (new)
- src/Hexalith.EventStore.Admin.Abstractions/Services/IDaprInfrastructureQueryService.cs (new)
- src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs (new)
- src/Hexalith.EventStore.Admin.Server/Controllers/AdminDaprController.cs (new)
- src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs (modified — added IDaprInfrastructureQueryService DI registration)
- src/Hexalith.EventStore.Admin.UI/Services/AdminDaprApiClient.cs (new)
- src/Hexalith.EventStore.Admin.UI/Program.cs (modified — added AdminDaprApiClient scoped registration)
- src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor (new)
- src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor (modified — added DAPR nav link)
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprComponentDetailTests.cs (new)
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprSidecarInfoTests.cs (new)
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprComponentCategoryHelperTests.cs (new)
- tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprInfrastructureQueryServiceTests.cs (new)
- tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDaprControllerTests.cs (new)
- tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprComponentsPageTests.cs (new)

### Change Log

- 2026-03-26: Story 19-1 implemented — DAPR Component Status Dashboard with full test coverage.
- 2026-03-26: Code review completed (3-layer: Blind Hunter, Edge Case Hunter, Acceptance Auditor). 11 actionable findings fixed: probe timeout token now passed to probes (P-1), cancelled probes mark Degraded (P-2), empty metadata.Id handled (P-3), null Name/Type filtered (P-4), controller returns 404 for null sidecar (P-5), OperationCanceledException no longer swallowed (P-6), toggle-to-deselect uses ComponentName (P-7), empty state shown for zero components (P-8), PollAsync resilient to exceptions (P-9), deep linking test added (P-10), SDK-unsupported counts show N/A (BS-1). 4 new tests added. All 856 affected tests green.
