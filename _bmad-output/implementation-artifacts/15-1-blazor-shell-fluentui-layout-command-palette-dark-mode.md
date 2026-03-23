# Story 15.1: Blazor Shell, FluentUI Layout, Command Palette, Dark Mode

Status: ready-for-dev
Size: Large (multi-session) — ~30 new files, 10 task groups, 15 ACs

## Definition of Done

- All 15 ACs verified
- Merge-blocking bUnit tests green (9.3, 9.10, 9.11, 9.12, 9.13)
- Recommended bUnit tests green (9.4–9.9)
- E2E smoke tests green (Task 10)
- Visual ACs (12, 13) verified via Playwright screenshots at each breakpoint tier
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions

## Story

As a **developer or operator using the Hexalith EventStore admin tool**,
I want **a Blazor Server shell application with FluentUI V4 layout, sidebar navigation, command palette (Ctrl+K), and dark/light theme switching**,
so that **I have a professional, accessible, keyboard-navigable foundation for all admin dashboard pages**.

## Acceptance Criteria

1. **Blazor Server Shell boots and renders** within the Aspire topology at `admin-ui` resource, connecting to `admin-server` REST API via HttpClient with JWT Bearer authentication.
2. **FluentUI V4 layout** uses `FluentLayout`, `FluentHeader`, `FluentBodyContent`, `FluentNavMenu` for app shell structure matching UX spec.
3. **Sidebar navigation** has a static section (Home, Commands, Events, Health, Services, Settings) and a collapsible dynamic Topology section (placeholder `FluentNavGroup` with empty `FluentTreeView` — populated in future stories). Full width: 220px; icon-only: 48px. Collapse/expand via Ctrl+B; state persisted per-viewport-tier in localStorage.
4. **Dark/light theme** respects OS `prefers-color-scheme` by default, with an explicit toggle in the header. Theme choice persisted in localStorage. Uses `FluentDesignTheme` provider with Hexalith brand color mapped to `--accent-base-color`.
5. **Command palette** (UX-DR41) opens on Ctrl+K, provides fuzzy search across placeholder categories (streams, projections, tenants, actions). Uses FluentDialog + FluentSearch. Results grouped by type. Selecting a result navigates. Escape closes.
6. **Breadcrumb trail** (UX-DR43) renders below header on sub-pages, with clickable segments. Final segment non-clickable. Monospace for identifiers.
7. **Landing page (/)** renders with skeleton StatCard placeholders (simulated brief delay), then EmptyState: "EventStore Admin is running. Connect to Admin API to begin." Header bar shows connection status placeholder (Unknown) and system health placeholder ("—") — live data wired in story 15-7.
8. **Stub pages** exist for all routes with page-specific EmptyState content:
   - `/commands` — "No commands processed yet." + action link to Admin.Server swagger (full URL via Aspire service discovery, NOT relative `/swagger`)
   - `/events` — "No events stored yet." + link to getting started guide
   - `/health` — "All systems nominal. No issues detected." (positive empty state, no action)
   - `/health/dead-letters` — "No dead letters. All commands processed successfully."
   - `/tenants` — "No tenants configured." + "Create your first tenant" button
   - `/services` — "EventStore is running. 0 domain services connected." + link to domain service registration guide
   - `/settings` — "Configure admin dashboard preferences."
9. **Page titles** follow format `"{PageName} - Hexalith EventStore"` on every page (e.g., "Commands - Hexalith EventStore", "Health - Hexalith EventStore"). Landing page title: "Hexalith EventStore Admin".
10. **Authentication** — HttpClientFactory named "AdminApi" with JWT Bearer token injection. Unauthorized requests redirect to login or show 401 message.
11. **Role-based UI** — Navigation items and action buttons conditionally render based on `AdminRole` (ReadOnly, Operator, Admin) extracted from JWT claims.
12. **Responsive layout** — Four viewport tiers: Optimal (1920px+), Standard (1280–1919px), Compact (960–1279px), Minimum (<960px). Sidebar collapses to icon-only (48px) at Compact. StatCards: 4-column at Standard+, 2-column at Compact, 1-column at 200% zoom. Below 960px, show warning banner: "Dashboard optimized for wider screens." Page chrome never scrolls horizontally.
13. **Accessibility** — Skip-to-main-content link, `lang="en"`, semantic HTML (`<nav>`, `<main>`), WCAG 2.1 AA contrast, keyboard-navigable sidebar and command palette. Windows high-contrast mode: `forced-colors` media query support with StatusBadge switching to icon-only differentiation, focus indicators using `Highlight` system color. `<ErrorBoundary>` wraps routes for graceful error handling.
14. **Performance** — App shell (layout + skeleton placeholders) renders within 2 seconds on initial load (NFR41 baseline — full NFR41 validation deferred to story 15-2 when live data flows). Support at least 10 concurrent users with independent views (NFR45). StatCard `aria-live` updates debounced at 5-second minimum to prevent screen reader flooding.
15. **Aspire integration** — AdminUI registered as project resource in AppHost, wired with admin-server reference, Keycloak environment variables, and service discovery.

## Tasks / Subtasks

- [ ] **Task 1: Create Blazor Server project** (AC: 1, 15)
  - [ ] 1.1 Create `src/Hexalith.EventStore.Admin.UI/` as Blazor Server project (`<Project Sdk="Microsoft.NET.Sdk.Web">`)
  - [ ] 1.2 Add PackageReferences: `Microsoft.FluentUI.AspNetCore.Components`, `Microsoft.FluentUI.AspNetCore.Components.Icons`, `Microsoft.AspNetCore.SignalR.Client`
  - [ ] 1.3 Add ProjectReference to `Hexalith.EventStore.Admin.Abstractions` (for DTOs and AdminRole enum)
  - [ ] 1.4 Add ProjectReference to `Hexalith.EventStore.ServiceDefaults`
  - [ ] 1.5 Set `<IsPackable>false</IsPackable>`, `<RootNamespace>Hexalith.EventStore.Admin.UI</RootNamespace>`
  - [ ] 1.6 Create `Program.cs` — **copy `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs` as baseline**, then adapt. Registration order: `AddServiceDefaults()` → `AddRazorComponents().AddInteractiveServerComponents()` → `AddFluentUIComponents()` → `AddAuthentication("Bearer").AddJwtBearer()` → `AddAuthorization()` → `AddCascadingAuthenticationState()` → HttpClientFactory "AdminApi" with auth handler. Middleware: `UseExceptionHandler` → `MapDefaultEndpoints` → `UseAuthentication` → `UseAuthorization` → `MapRazorComponents<App>().AddInteractiveServerRenderMode()`. **Verify middleware order matches exactly — auth before map is critical.**
  - [ ] 1.7 Create `_Imports.razor` with global usings: `Microsoft.FluentUI.AspNetCore.Components`, `Microsoft.AspNetCore.Components.Authorization`, `Microsoft.AspNetCore.Components.Web`, `Hexalith.EventStore.Admin.UI`, `Hexalith.EventStore.Admin.UI.Components`, `Hexalith.EventStore.Admin.UI.Layout`, `Hexalith.EventStore.Admin.Abstractions.Models`
  - [ ] 1.8 Create `appsettings.json` with `EventStore:Authentication` section (Authority, ClientId) and `appsettings.Development.json` with Keycloak localhost overrides (same pattern as Sample.BlazorUI)
  - [ ] 1.9 Add project to `Hexalith.EventStore.slnx` — **edit XML directly**: add `<Project Path="src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj" />` in the `src` folder group. Do NOT use `dotnet sln add` (only works with `.sln`, not `.slnx`).
  - [ ] 1.10 Add `bunit` and `Microsoft.Playwright` package versions to `Directory.Packages.props` now (query NuGet for latest stable compatible with .NET 10) — adding early enables incremental test writing alongside feature tasks
  - [ ] 1.11 Run `dotnet restore Hexalith.EventStore.slnx` and verify zero errors before proceeding

- [ ] **Task 2: Aspire integration — verify boot** (AC: 1, 15)
  - **Rationale**: Wire Aspire FIRST to prove the bare project renders in the topology before layering features. Prevents "built 30 files and nothing renders" scenario.
  - [ ] 2.1 Create `Components/App.razor` (with full `<FluentDesignTheme>`, `<HeadOutlet>`, `<Routes>` from the start — avoid creating a placeholder to rewrite later), `Components/Routes.razor` (router with `<FocusOnNavigate>`), `Layout/MainLayout.razor` (minimal `FluentLayout` + `FluentHeader` + `FluentBodyContent` with "Hello Admin" in content area) — enough to render and verify boot
  - [ ] 2.2 Update `HexalithEventStoreExtensions.cs` to accept optional `adminUI` parameter
  - [ ] 2.3 Register AdminUI project in `AppHost/Program.cs` with reference to admin-server
  - [ ] 2.4 Wire Keycloak environment variables to AdminUI (same pattern as admin-server)
  - [ ] 2.5 Update `HexalithEventStoreResources` record to include AdminUI resource
  - [ ] 2.6 **Checkpoint**: Run `dotnet build Hexalith.EventStore.slnx --configuration Release` and verify Admin.UI compiles with zero errors. Full Aspire boot verified in Task 10 E2E tests.

- [ ] **Task 3: App shell components** (AC: 2, 3, 6, 9, 13)
  - [ ] 3.1 Expand `Components/App.razor` — add `<FluentToastProvider>`, `<ErrorBoundary>` with `<ErrorContent>` (accessible recovery UI: `role="alert"`, "Something went wrong" message, reload button with auto-focus) wrapping `<Routes>` (FluentDesignTheme already added in Task 2)
  - [ ] 3.2 Expand `Layout/MainLayout.razor` — `FluentLayout` + `FluentHeader` (with placeholder `<span>` elements for connection status + system health badge — replaced by `ConnectionStatus.razor` and `SystemHealthBadge.razor` in Task 6) + `FluentBodyContent` + `FluentStack` sidebar (220px full / 48px collapsed)
  - [ ] 3.3 Create `Layout/NavMenu.razor` — `FluentNavMenu` with static links (Home, Commands, Events, Health, Services, Settings) and collapsible Topology `FluentNavGroup` containing placeholder empty `FluentTreeView` (populated in future stories)
  - [ ] 3.4 Implement sidebar collapse/expand (Ctrl+B) with per-viewport-tier localStorage persistence via JS interop. **Wrap localStorage reads/writes in try/catch** — fallback to defaults on failure (quota exceeded, private browsing).
  - [ ] 3.5 Create `Layout/Breadcrumb.razor` — dynamic breadcrumb from current route, monospace for IDs
  - [ ] 3.6 Add skip-to-main-content link, `lang="en"` on HTML root, `<PageTitle>` format: "{PageName} - Hexalith EventStore" on all pages

- [ ] **Task 4: Theme system** (AC: 4)
  - [ ] 4.1 Configure `FluentDesignTheme` with `Mode` bound to user preference (OS default, localStorage override). **Wrap localStorage reads/writes in try/catch** — fallback to OS default on failure.
  - [ ] 4.2 Map Hexalith brand blue (#0066CC light / #4A9EFF dark) to `--accent-base-color`
  - [ ] 4.3 Create `Components/ThemeToggle.razor` — switch in header toggling light/dark/system, persisted to localStorage
  - [ ] 4.4 Define semantic status color CSS custom properties from UX spec (Success green, In-flight blue, Warning yellow, Error red, Neutral gray)

- [ ] **Task 5: Command palette** (AC: 5)
  - [ ] 5.1 Create `Components/CommandPalette.razor` — `FluentDialog` modal with `FluentSearch` input. **Auto-focus FluentSearch** via `FocusAsync()` when dialog opens.
  - [ ] 5.2 Register Ctrl+K keyboard shortcut (JS interop) to open palette — **must call `e.preventDefault()`** in the keydown handler to suppress browser default (address bar focus). Test in Chrome, Edge, and Firefox.
  - [ ] 5.3 Implement fuzzy search with result grouping by category (Streams, Projections, Tenants, Actions)
  - [ ] 5.4 Navigate on result selection via `NavigationManager`; Escape closes
  - [ ] 5.5 Placeholder data source — static list of admin routes and actions for initial implementation

- [ ] **Task 6: Custom foundation components** (AC: 7, 8, 14)
  - [ ] 6.1 Create `Components/Shared/StatusBadge.razor` — maps status enum to icon + color + label (per UX spec). In `forced-colors` mode, switches to icon-only differentiation. `aria-label="Command status: {status name}"`.
  - [ ] 6.2 Create `Components/Shared/StatCard.razor` — FluentCard with value (monospace for numeric values), label, severity coloring. Loading state renders skeleton placeholder matching card dimensions. `aria-label="{Label}: {Value}"`. `aria-live="polite"` with 5-second debounce on value updates.
  - [ ] 6.3 Create `Components/Shared/EmptyState.razor` — icon + title + description + optional action button/link. Parameters: `Icon`, `Title`, `Description`, `ActionLabel?`, `ActionHref?`
  - [ ] 6.4 Create `Components/Shared/SkeletonCard.razor` — skeleton placeholder matching StatCard dimensions for loading states
  - [ ] 6.5 Create `Components/Shared/IssueBanner.razor` — static placeholder alert banner for degraded system state. Parameters: `Visible` (default `false`), `Title`, `Description`, `ActionLabel?`. Uses `role="alert"` with `aria-live="assertive"`. **This story**: render capability only with `Visible=false` default — no API health polling. State transitions and health API wiring deferred to story 15-7 (Health Dashboard).
  - [ ] 6.6 Create `Components/Shared/HeaderStatusIndicator.razor` — single component with two slots: connection status (`FluentBadge`: Connected green / Disconnected red / Unknown gray) + system health (text: "—" placeholder). Parameters: `ConnectionStatus` enum (default `Unknown`), `HealthSummary` string (default "—"). **This story**: purely visual, no API calls. Replace placeholder `<span>` elements in MainLayout header. Health check + live data wired in story 15-7.
  - [ ] 6.8 Create stub pages with page-specific EmptyState content (see AC8 for exact messages per page): `Pages/Index.razor` (landing with **simulated 200ms loading delay** → skeleton briefly → EmptyState, **no real API call** — IssueBanner area with `Visible=false`), `Pages/Commands.razor`, `Pages/Events.razor`, `Pages/Health.razor`, `Pages/DeadLetters.razor`, `Pages/Tenants.razor`, `Pages/Services.razor`, `Pages/Settings.razor`

- [ ] **Task 7: Authentication & authorization** (AC: 10, 11)
  - [ ] 7.1 Create `Services/AdminApiAuthorizationHandler.cs` — DelegatingHandler injecting JWT Bearer token (adapt from Sample.BlazorUI `EventStoreApiAuthorizationHandler`)
  - [ ] 7.2 Create `Services/AdminApiAccessTokenProvider.cs` — token provider (adapt from Sample.BlazorUI)
  - [ ] 7.3 Register HttpClientFactory "AdminApi" using Aspire service discovery: `builder.Services.AddHttpClient("AdminApi", client => client.BaseAddress = new("https+http://admin-server")).AddHttpMessageHandler<AdminApiAuthorizationHandler>()`
  - [ ] 7.4 Create `Services/AdminUserContext.cs` — extracts AdminRole from `AuthenticationStateProvider` JWT claims. **Use claim type constants from `Hexalith.EventStore.Admin.Abstractions`**: check `src/Hexalith.EventStore.Admin.Abstractions/` for existing `AdminClaimTypes` class. If not found, create `Models/Common/AdminClaimTypes.cs` with `public const string Role = "eventstore:admin:role";` to ensure single source of truth between Server and UI.
  - [ ] 7.5 Create `Components/Shared/AuthorizedView.razor` — wraps content requiring minimum role level

- [ ] **Task 8: Responsive layout** (AC: 12)
  - [ ] 8.1 Implement CSS `max-width` media queries: Optimal (1920px+ default), Standard (`max-width: 1919px`), Compact (`max-width: 1279px`), Minimum (`max-width: 959px`)
  - [ ] 8.2 Sidebar: 220px full at Standard+, auto-collapse to 48px icon-only at Compact (with tooltips on icons)
  - [ ] 8.3 StatCard grid: 4-column at Standard+, 2-column at Compact, 1-column at 200% zoom
  - [ ] 8.4 Per-viewport-tier sidebar state in localStorage
  - [ ] 8.5 Below 960px: show warning banner "Dashboard optimized for wider screens." Page chrome must not cause horizontal scroll.
  - [ ] 8.6 Add `forced-colors` media query in `app.css` for Windows high-contrast mode — **scope to components that exist in this story only**: StatusBadge icon-only differentiation, StatCard border visibility, sidebar focus indicators using `Highlight` system color, skip-to-main-content link visibility. Data grid high-contrast rules deferred to the story that adds FluentDataGrid.

- [ ] **Task 9: Unit tests (bUnit)** (AC: 2, 3, 4, 5, 7, 8, 9, 10, 11, 13)
  - **Prerequisite**: Run bUnit tests incrementally per task — don't batch until Tasks 1-8 complete.
  - **Test JWT**: Use `EventStoreApiAccessTokenProvider` development HS256 token mode (see `samples/Hexalith.EventStore.Sample.BlazorUI/Services/EventStoreApiAccessTokenProvider.cs`) with config: Issuer, Audience, SigningKey, Subject, Tenants[], Domains[], Permissions[].
  - [ ] 9.1 Verify `bunit` package version was added in Task 1.9. If not, add now and run `dotnet restore`.
  - [ ] 9.2 Create `tests/Hexalith.EventStore.Admin.UI.Tests/` project with xUnit + bUnit + Shouldly. **Register `bunit.JSInterop` in test context and add FluentUI mock services** — `AddFluentUIComponents()` requires `IJSRuntime`. See bUnit docs for FluentUI integration pattern. Add test project to `.slnx` via direct XML edit.
  - **Merge-blocking tests** (must pass):
  - [ ] 9.3 Test MainLayout renders FluentLayout + FluentHeader + FluentNavMenu (AC: 2)
  - [ ] 9.10 Test each stub page renders with correct `<PageTitle>` format and page-specific EmptyState content (AC: 8, 9)
  - [ ] 9.11 Test Blazor Server host bootstraps without errors (WebApplicationFactory smoke test) (AC: 1)
  - [ ] 9.12 Test auth handler attaches Bearer token to outgoing requests — use development HS256 token (AC: 10)
  - [ ] 9.13 Test AdminUserContext extracts correct AdminRole from claims (Admin > Operator > ReadOnly mapping) (AC: 11)
  - **Recommended tests** (important, implement after blocking tests):
  - [ ] 9.4 Test NavMenu renders all 6 static links; Settings hidden when role < Admin (AC: 3, 11)
  - [ ] 9.5 Test ThemeToggle cycles light/dark/system and persists (AC: 4)
  - [ ] 9.6 Test CommandPalette opens on trigger, closes on Escape, navigates on selection (AC: 5)
  - [ ] 9.7 Test StatusBadge renders correct icon + color + label for each CommandStatus value (AC: 7)
  - [ ] 9.8 Test EmptyState renders with all parameter combinations (with/without action) (AC: 8)
  - [ ] 9.9 Test StatCard renders value in monospace, label, severity styling, and skeleton loading state (AC: 7)

- [ ] **Task 10: E2E test scaffolding (Playwright)** (AC: 13, 14)
  - **Prerequisite**: Tasks 1-8 must be complete before E2E tests can run — shell, layout, theme, palette, pages, auth, Aspire, and responsive CSS must all render.
  - [ ] 10.1 Verify `Microsoft.Playwright` package version was added in Task 1.10. If not, add now and run `dotnet restore`.
  - [ ] 10.2 Create `tests/Hexalith.EventStore.Admin.UI.E2E/` project with xUnit + Playwright
  - [ ] 10.3 Create base test fixture that starts Admin.UI host via WebApplicationFactory with development HS256 token config. Include comment in fixture: `// Run 'pwsh bin/Debug/net10.0/playwright.ps1 install' to install browser binaries before first run`
  - [ ] 10.4 First accessibility smoke test: load landing page, run axe-core, assert zero violations
  - [ ] 10.5 First high-contrast test: load landing page in forced-colors mode, screenshot for manual review
  - [ ] 10.6 Verify keyboard navigation: Tab through header → sidebar → main content in correct focus order
  - [ ] 10.7 Verify shell renders within 2 seconds (AC: 14 baseline)

## Dev Notes

### Architecture Compliance

- **ADR-P4 (Intentional Deviation)**: ADR-P4 originally specified a single Admin.Server project hosting both REST API and Blazor Web UI. This story creates Admin.UI as a **separate Blazor Server project** communicating with Admin.Server via HTTP REST API (`/api/v1/admin/*`). **Rationale**: Decoupled frontend enables independent scaling, independent testing, cleaner separation of concerns, and avoids mixing API controllers with Blazor Server rendering in one host. **Network latency trade-off**: HTTP calls add 1ms (localhost/Aspire) to 5-20ms (production containers) per API call — acceptable for admin tooling within NFR41's 2-second budget. Trade-off chosen for testability (bUnit tests run without DAPR/Admin.Server) and independent scaling. ADR-P4 will be updated post-implementation. The UI does NOT use DaprClient — all data flows through the Admin.Server REST API.
- **ADR-P5**: Observability deep-links (trace/metrics/logs URLs) will be consumed from Admin API health endpoint in later stories. For now, ensure the layout has a header area where deep-link buttons can be added.
- Admin.UI project is `IsPackable=false` — it's a deployable host, not a NuGet package.

### Technical Stack (Pinned Versions)

| Package | Version | Source |
|---------|---------|--------|
| Microsoft.FluentUI.AspNetCore.Components | 4.13.2 | Directory.Packages.props |
| Microsoft.FluentUI.AspNetCore.Components.Icons | 4.13.2 | Directory.Packages.props |
| Microsoft.AspNetCore.SignalR.Client | 10.0.5 | Directory.Packages.props |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 | Directory.Packages.props |
| bunit | Latest stable for .NET 10 (query NuGet at Task 1.10) | Added to Directory.Packages.props in Task 1.10 |
| Microsoft.Playwright | Latest stable (query NuGet at Task 1.10) | Added to Directory.Packages.props in Task 1.10 |

All packages centrally managed in `Directory.Packages.props`. bUnit and Playwright added in Task 1.10.

### Loading & Transition States (UX Spec Compliance)

| State | Pattern | Example |
|-------|---------|---------|
| Initial page load | Skeleton screen matching layout structure | StatCards show gray skeleton placeholders |
| Data refresh | Subtle fade on stale content + loading indicator | Content dims while refreshing |
| Empty data | EmptyState component with guidance and action | Page-specific message + CTA |

Motion: 150ms for micro-interactions, 300ms for layout changes. Respect `prefers-reduced-motion`.

### Performance Requirements

- **NFR41 (baseline)**: App shell + skeleton placeholders must render within 2 seconds on initial load. Full NFR41 (live data + SignalR updates within 200ms) validated in story 15-2.
- **NFR45**: Support at least 10 concurrent users with independent views without performance degradation.

### Typography Hierarchy

- Page titles: capped at 24px (`--type-ramp-plus-3`). Data-dense tool — no oversized headings.
- Navigation items: semibold weight. Body content: regular weight.
- No bold emphasis within body text — use color or icons instead.
- Monospace: `Cascadia Code, Consolas, monospace` for all machine-generated values.
- All other typography: Fluent UI V4 defaults (Segoe UI Variable → system-ui fallback chain). No custom fonts.

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+K | Open command palette / focus global search |
| Ctrl+B | Toggle sidebar collapse/expand |
| Escape | Close dialog / clear search / deselect |
| Arrow keys | Navigate within filter chips, tree nodes, data grid rows |
| Enter/Space | Activate focused element |
| Tab/Shift+Tab | Move between interactive element groups |

### Monospace Convention

All machine-generated values render in monospace: correlation IDs, aggregate IDs, tenant IDs, event sequence numbers, timestamps, DAPR component names. This applies to StatCard numeric values, breadcrumb identifiers, and any data grid cells displaying system values.

### Code Patterns to Follow

1. **File-scoped namespaces** (`namespace Hexalith.EventStore.Admin.UI;`)
2. **Allman brace style** (new line before `{`)
3. **Private fields**: `_camelCase`
4. **4-space indentation**, CRLF, UTF-8
5. **Nullable enabled**, **implicit usings enabled**
6. **Primary constructors** for services (C# 14)
7. **ConfigureAwait(false)** on all async calls
8. **CancellationToken** parameter on all async methods

### Existing Code to Reuse (DO NOT Recreate)

| What | Where | How to Use |
|------|-------|------------|
| Admin DTOs (30+ types) | `src/Hexalith.EventStore.Admin.Abstractions/Models/` | Reference project, use types directly |
| AdminRole enum | `src/Hexalith.EventStore.Admin.Abstractions/Models/AdminRole.cs` | Import for role-based UI rendering |
| FluentUI Blazor patterns | `samples/Hexalith.EventStore.Sample.BlazorUI/` | Copy and adapt Program.cs, MainLayout, auth handlers |
| SignalR client | `src/Hexalith.EventStore.SignalR/` | Reuse in future stories for real-time updates |
| ServiceDefaults | `src/Hexalith.EventStore.ServiceDefaults/` | Reference for OpenTelemetry, health checks |
| Aspire extension pattern | `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` | Extend to add AdminUI resource |

### File Structure

```
src/Hexalith.EventStore.Admin.UI/
  Hexalith.EventStore.Admin.UI.csproj
  Program.cs
  _Imports.razor
  appsettings.json             → EventStore:Authentication:Authority, ClientId
  appsettings.Development.json → Dev overrides (Keycloak localhost, dev credentials)
  Components/
    App.razor
    Routes.razor
    CommandPalette.razor
    ThemeToggle.razor
    Shared/
      StatusBadge.razor
      StatCard.razor
      SkeletonCard.razor
      EmptyState.razor
      IssueBanner.razor
      HeaderStatusIndicator.razor
      AuthorizedView.razor
  Layout/
    MainLayout.razor
    NavMenu.razor
    Breadcrumb.razor
  Pages/
    Index.razor           → /
    Commands.razor         → /commands
    Events.razor           → /events
    Health.razor           → /health
    DeadLetters.razor      → /health/dead-letters
    Tenants.razor          → /tenants
    Services.razor         → /services
    Settings.razor         → /settings
  Services/
    AdminApiAuthorizationHandler.cs
    AdminApiAccessTokenProvider.cs
    AdminUserContext.cs
  wwwroot/
    css/
      app.css              → Custom properties, responsive breakpoints
    js/
      interop.js           → All JS interop: Ctrl+K, Ctrl+B shortcuts + localStorage helpers
```

### Semantic Color Tokens (from UX Spec)

```css
/* Map to CSS custom properties for use alongside FluentUI tokens */
--hexalith-status-success: #1A7F37;     /* dark: #2EA043 */
--hexalith-status-inflight: #0969DA;    /* dark: #58A6FF */
--hexalith-status-warning: #9A6700;     /* dark: #D29922 */
--hexalith-status-error: #CF222E;       /* dark: #F85149 */
--hexalith-status-neutral: #656D76;     /* dark: #8B949E */
--hexalith-brand: #0066CC;              /* dark: #4A9EFF */
```

### CSS Strategy

- `wwwroot/css/app.css` — global custom properties (semantic colors, responsive breakpoints, `forced-colors` media query, `prefers-reduced-motion` rules)
- Blazor CSS isolation (`.razor.css` co-located files) for component-scoped styles only when FluentUI tokens/classes don't suffice

### Sidebar Navigation Items

| Icon Intent | Label | Route | Min Role |
|-------------|-------|-------|----------|
| Home | Home | / | ReadOnly |
| List view | Commands | /commands | ReadOnly |
| Timeline | Events | /events | ReadOnly |
| Health | Health | /health | ReadOnly |
| Server | Services | /services | ReadOnly |
| Settings | Settings | /settings | Admin |

### Sample.BlazorUI → Admin.UI Adaptation Guide

When copying from `samples/Hexalith.EventStore.Sample.BlazorUI/`, change these values:

| Aspect | Sample.BlazorUI | Admin.UI |
|--------|----------------|----------|
| API target (Aspire resource) | `commandapi` | `admin-server` |
| Base path | `/api/v1/` | `/api/v1/admin/` |
| HttpClient name | `"CommandApi"` | `"AdminApi"` |

### Authentication Flow

1. Admin.UI registered in Keycloak as OIDC client (same realm as CommandApi and Admin.Server)
2. User authenticates via Keycloak login page
3. JWT Bearer token stored in auth state
4. HttpClientFactory "AdminApi" injects Bearer token on every request to Admin.Server REST API
5. Admin.Server validates JWT + extracts role + enforces tenant scoping
6. UI extracts AdminRole from JWT claims via `AdminUserContext` for conditional rendering

### Context from Epic 14

- **Admin.Server.Host bootstrap order is strict**: AddServiceDefaults → AddDaprClient → AddAdminApi → AddAuthentication → AddControllers. Admin.UI follows similar pattern but without DaprClient (UI talks HTTP to Admin.Server, not DAPR directly).
- **Three-tier role hierarchy**: Admin > Operator > ReadOnly. Role mapping from JWT claims defined in `AdminClaimsTransformation.cs`. UI must replicate this mapping for conditional rendering.
- **Tenant scoping**: JWT `eventstore:tenant` claims determine visible data. UI should show tenant selector pre-filtered to user's allowed tenants.
- **Admin.Server runs on ports 8090/8091**. Admin.UI will need its own port (suggest 8092/8093).
- **All Admin.Server endpoints**: `api/v1/admin/` prefix with 7 controllers (health, streams, projections, storage, tenants, dead-letters, type-catalog).
- **Error responses**: ProblemDetails format. UI should handle 401 (redirect to login), 403 (show forbidden message), 404 (show not found), 503 (show service unavailable).

### Anti-Patterns to Avoid

- Do NOT add DaprClient to Admin.UI — it communicates exclusively via HTTP to Admin.Server REST API
- Do NOT reference Admin.Server project — only reference Admin.Abstractions for DTOs
- Do NOT create custom CSS where FluentUI design tokens suffice
- Do NOT use raw HTML elements styled directly — compose FluentUI primitives
- Do NOT hardcode Admin.Server URL — use Aspire service discovery
- Do NOT skip `ConfigureAwait(false)` on async calls
- Do NOT use `.sln` files — use `.slnx` only
- Do NOT add packages not in `Directory.Packages.props`
- Do NOT log JWT tokens or sensitive data (SEC-5)
- Do NOT implement OpenAPI/Swagger on Admin.UI (that's Admin.Server's concern, story 14-5)
- Do NOT use relative URLs for cross-service links (e.g., `/swagger`) — resolve via Aspire service discovery or configuration

### Out of Scope (Deferred to Later Stories)

This story establishes the **UI shell only**. FR68-75 functional capabilities are implemented in stories 15-2 through 15-8.

| Deferred Item | Deferred To |
|---------------|-------------|
| Live Admin API data (streams, projections, health metrics) | Story 15-2+ |
| ConnectionStatus health check API call | Story 15-7 |
| SystemHealthBadge live data | Story 15-7 |
| IssueBanner state transitions (Active → Resolving → Resolved) | Story 15-7 |
| Topology FluentTreeView population (tenants → domains → aggregates) | Story 15-3+ |
| FluentDataGrid high-contrast CSS rules | Story that adds FluentDataGrid |
| Full NFR41 validation (live data + SignalR within 200ms) | Story 15-2 |
| SignalR real-time connection | Future stories |

### Project Structure Notes

- Alignment: follows same `src/` folder organization as all other projects
- The project name `Hexalith.EventStore.Admin.UI` matches the architecture spec naming for the Blazor Web UI component
- Test project at `tests/Hexalith.EventStore.Admin.UI.Tests/` follows existing test project naming pattern

### References

- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4, ADR-P5]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR34-DR49]
- [Source: _bmad-output/planning-artifacts/prd.md — FR79 (shell), NFR41, NFR45; FR68-FR75 deferred to stories 15-2+]
- [Source: _bmad-output/planning-artifacts/epics.md — Epic 15 summary]
- [Source: samples/Hexalith.EventStore.Sample.BlazorUI/ — Blazor patterns]
- [Source: src/Hexalith.EventStore.Admin.Server.Host/ — Host bootstrap pattern]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/ — DTOs and service interfaces]
- [Source: _bmad-output/implementation-artifacts/14-4-admin-server-aspire-resource-integration.md — Aspire patterns]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
