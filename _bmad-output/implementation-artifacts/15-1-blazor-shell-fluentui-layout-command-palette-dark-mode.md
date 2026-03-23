# Story 15.1: Blazor Shell, FluentUI Layout, Command Palette, Dark Mode

Status: ready-for-dev

## Story

As a **developer or operator using the Hexalith EventStore admin tool**,
I want **a Blazor Server shell application with FluentUI V4 layout, sidebar navigation, command palette (Ctrl+K), and dark/light theme switching**,
so that **I have a professional, accessible, keyboard-navigable foundation for all admin dashboard pages**.

## Acceptance Criteria

1. **Blazor Server Shell boots and renders** within the Aspire topology at `admin-ui` resource, connecting to `admin-server` REST API via HttpClient with JWT Bearer authentication.
2. **FluentUI V4 layout** uses `FluentLayout`, `FluentHeader`, `FluentBodyContent`, `FluentNavMenu` for app shell structure matching UX spec.
3. **Sidebar navigation** has a static section (Home, Commands, Events, Health, Services, Settings) and a collapsible dynamic Topology section. Collapse/expand via Ctrl+B; state persisted in localStorage.
4. **Dark/light theme** respects OS `prefers-color-scheme` by default, with an explicit toggle in the header. Theme choice persisted in localStorage. Uses `FluentDesignTheme` provider with Hexalith brand color mapped to `--accent-base-color`.
5. **Command palette** opens on Ctrl+K, provides fuzzy search across placeholder categories (streams, projections, tenants, actions). Uses FluentDialog + FluentSearch. Results grouped by type. Selecting a result navigates. Escape closes.
6. **Breadcrumb trail** renders below header on sub-pages, with clickable segments. Final segment non-clickable. Monospace for identifiers.
7. **Landing page (/)** renders with skeleton StatCard placeholders during load, then StatCard components with values or EmptyState: "EventStore Admin is running. Connect to Admin API to begin."
8. **Stub pages** exist for all routes with page-specific EmptyState content:
   - `/commands` — "No commands processed yet." + action link to `/swagger`
   - `/events` — "No events stored yet." + link to getting started guide
   - `/health` — "All systems nominal. No issues detected." (positive empty state, no action)
   - `/health/dead-letters` — "No dead letters. All commands processed successfully."
   - `/tenants` — "No tenants configured." + "Create your first tenant" button
   - `/services` — "EventStore is running. 0 domain services connected." + link to domain service registration guide
   - `/settings` — "Configure admin dashboard preferences."
9. **Authentication** — HttpClientFactory named "AdminApi" with JWT Bearer token injection. Unauthorized requests redirect to login or show 401 message.
10. **Role-based UI** — Navigation items and action buttons conditionally render based on `AdminRole` (ReadOnly, Operator, Admin) extracted from JWT claims.
11. **Responsive layout** — Sidebar collapses to icon-only at <1280px. StatCards stack at <1280px. Minimum 960px supported without horizontal scroll on page chrome.
12. **Accessibility** — Skip-to-main-content link, `lang="en"`, semantic HTML (`<nav>`, `<main>`), WCAG 2.1 AA contrast, keyboard-navigable sidebar and command palette.
13. **Aspire integration** — AdminUI registered as project resource in AppHost, wired with admin-server reference, Keycloak environment variables, and service discovery.

## Tasks / Subtasks

- [ ] **Task 1: Create Blazor Server project** (AC: 1, 13)
  - [ ] 1.1 Create `src/Hexalith.EventStore.Admin.UI/` as Blazor Server project (`<Project Sdk="Microsoft.NET.Sdk.Web">`)
  - [ ] 1.2 Add PackageReferences: `Microsoft.FluentUI.AspNetCore.Components`, `Microsoft.FluentUI.AspNetCore.Components.Icons`, `Microsoft.AspNetCore.SignalR.Client`
  - [ ] 1.3 Add ProjectReference to `Hexalith.EventStore.Admin.Abstractions` (for DTOs and AdminRole enum)
  - [ ] 1.4 Add ProjectReference to `Hexalith.EventStore.ServiceDefaults`
  - [ ] 1.5 Set `<IsPackable>false</IsPackable>`, `<RootNamespace>Hexalith.EventStore.Admin.UI</RootNamespace>`
  - [ ] 1.6 Create `Program.cs` with this exact registration order: `AddServiceDefaults()` → `AddRazorComponents().AddInteractiveServerComponents()` → `AddFluentUIComponents()` → `AddAuthentication("Bearer").AddJwtBearer()` → `AddAuthorization()` → `AddCascadingAuthenticationState()` → HttpClientFactory "AdminApi" with auth handler. Middleware: `UseExceptionHandler` → `MapDefaultEndpoints` → `UseAuthentication` → `UseAuthorization` → `MapRazorComponents<App>().AddInteractiveServerRenderMode()`
  - [ ] 1.7 Create `_Imports.razor` with global usings: `Microsoft.FluentUI.AspNetCore.Components`, `Microsoft.AspNetCore.Components.Authorization`, `Microsoft.AspNetCore.Components.Web`, `Hexalith.EventStore.Admin.UI`, `Hexalith.EventStore.Admin.UI.Components`, `Hexalith.EventStore.Admin.UI.Layout`, `Hexalith.EventStore.Admin.Abstractions.Models`
  - [ ] 1.8 Add project to `Hexalith.EventStore.slnx`

- [ ] **Task 2: App shell components** (AC: 2, 3, 6, 12)
  - [ ] 2.1 Create `Components/App.razor` — root component with `<FluentDesignTheme>` provider, `<HeadOutlet>`, `<Routes>`
  - [ ] 2.2 Create `Components/Routes.razor` — router with `<FocusOnNavigate>`
  - [ ] 2.3 Create `Layout/MainLayout.razor` — `FluentLayout` + `FluentHeader` + `FluentBodyContent` + `FluentStack` sidebar
  - [ ] 2.4 Create `Layout/NavMenu.razor` — `FluentNavMenu` with static links (Home, Commands, Events, Health, Services, Settings) and collapsible Topology `FluentNavGroup`
  - [ ] 2.5 Implement sidebar collapse/expand (Ctrl+B) with localStorage persistence via JS interop
  - [ ] 2.6 Create `Layout/Breadcrumb.razor` — dynamic breadcrumb from current route, monospace for IDs
  - [ ] 2.7 Add skip-to-main-content link, `lang="en"` on HTML root, `<PageTitle>` on all pages

- [ ] **Task 3: Theme system** (AC: 4)
  - [ ] 3.1 Configure `FluentDesignTheme` with `Mode` bound to user preference (OS default, localStorage override)
  - [ ] 3.2 Map Hexalith brand blue (#0066CC light / #4A9EFF dark) to `--accent-base-color`
  - [ ] 3.3 Create `Components/ThemeToggle.razor` — switch in header toggling light/dark/system, persisted to localStorage
  - [ ] 3.4 Define semantic status color CSS custom properties from UX spec (Success green, In-flight blue, Warning yellow, Error red, Neutral gray)

- [ ] **Task 4: Command palette** (AC: 5)
  - [ ] 4.1 Create `Components/CommandPalette.razor` — `FluentDialog` modal with `FluentSearch` input
  - [ ] 4.2 Register Ctrl+K keyboard shortcut (JS interop) to open palette
  - [ ] 4.3 Implement fuzzy search with result grouping by category (Streams, Projections, Tenants, Actions)
  - [ ] 4.4 Navigate on result selection via `NavigationManager`; Escape closes
  - [ ] 4.5 Placeholder data source — static list of admin routes and actions for initial implementation

- [ ] **Task 5: Custom foundation components** (AC: 7, 8)
  - [ ] 5.1 Create `Components/Shared/StatusBadge.razor` — maps status enum to icon + color + label (per UX spec)
  - [ ] 5.2 Create `Components/Shared/StatCard.razor` — FluentCard with value (monospace for numeric values), label, severity coloring. Loading state renders skeleton placeholder matching card dimensions.
  - [ ] 5.3 Create `Components/Shared/EmptyState.razor` — icon + title + description + optional action button/link. Parameters: `Icon`, `Title`, `Description`, `ActionLabel?`, `ActionHref?`
  - [ ] 5.4 Create `Components/Shared/SkeletonCard.razor` — skeleton placeholder matching StatCard dimensions for loading states
  - [ ] 5.5 Create stub pages with page-specific EmptyState content (see AC8 for exact messages per page): `Pages/Index.razor` (landing with skeleton → StatCards + EmptyState), `Pages/Commands.razor`, `Pages/Events.razor`, `Pages/Health.razor`, `Pages/DeadLetters.razor`, `Pages/Tenants.razor`, `Pages/Services.razor`, `Pages/Settings.razor`

- [ ] **Task 6: Authentication & authorization** (AC: 9, 10)
  - [ ] 6.1 Create `Services/AdminApiAuthorizationHandler.cs` — DelegatingHandler injecting JWT Bearer token (adapt from Sample.BlazorUI `EventStoreApiAuthorizationHandler`)
  - [ ] 6.2 Create `Services/AdminApiAccessTokenProvider.cs` — token provider (adapt from Sample.BlazorUI)
  - [ ] 6.3 Register HttpClientFactory "AdminApi" using Aspire service discovery: `builder.Services.AddHttpClient("AdminApi", client => client.BaseAddress = new("https+http://admin-server")).AddHttpMessageHandler<AdminApiAuthorizationHandler>()`
  - [ ] 6.4 Create `Services/AdminUserContext.cs` — extracts AdminRole from `AuthenticationStateProvider` JWT claims
  - [ ] 6.5 Create `Components/Shared/AuthorizedView.razor` — wraps content requiring minimum role level

- [ ] **Task 7: Aspire integration** (AC: 13)
  - [ ] 7.1 Update `HexalithEventStoreExtensions.cs` to accept optional `adminUI` parameter
  - [ ] 7.2 Register AdminUI project in `AppHost/Program.cs` with reference to admin-server
  - [ ] 7.3 Wire Keycloak environment variables to AdminUI (same pattern as admin-server)
  - [ ] 7.4 Update `HexalithEventStoreResources` record to include AdminUI resource

- [ ] **Task 8: Responsive layout** (AC: 11)
  - [ ] 8.1 Implement CSS media queries: Optimal (1920px+), Standard (1280px+), Compact (960px+), Minimum (<960px)
  - [ ] 8.2 Sidebar auto-collapses to icon-only at Compact tier
  - [ ] 8.3 StatCard grid: 4-column at Standard+, 2-column at Compact
  - [ ] 8.4 Per-viewport-tier sidebar state in localStorage

- [ ] **Task 9: Unit tests (bUnit)** (AC: all)
  - [ ] 9.1 Add `bunit` package version to `Directory.Packages.props` (check latest stable compatible with .NET 10)
  - [ ] 9.2 Create `tests/Hexalith.EventStore.Admin.UI.Tests/` project with xUnit + bUnit + Shouldly
  - [ ] 9.3 Test MainLayout renders FluentLayout + FluentHeader + FluentNavMenu
  - [ ] 9.4 Test NavMenu renders all 6 static links; Settings hidden when role < Admin
  - [ ] 9.5 Test ThemeToggle cycles light/dark/system and persists
  - [ ] 9.6 Test CommandPalette opens on trigger, closes on Escape, navigates on selection
  - [ ] 9.7 Test StatusBadge renders correct icon + color + label for each CommandStatus value
  - [ ] 9.8 Test EmptyState renders with all parameter combinations (with/without action)
  - [ ] 9.9 Test StatCard renders value in monospace, label, severity styling, and skeleton loading state
  - [ ] 9.10 Test each stub page renders with correct `<PageTitle>` and page-specific EmptyState content
  - [ ] 9.11 Test Blazor Server host bootstraps without errors (WebApplicationFactory smoke test)
  - [ ] 9.12 Test auth handler attaches Bearer token to outgoing requests
  - [ ] 9.13 Test AdminUserContext extracts correct AdminRole from claims (Admin > Operator > ReadOnly mapping)

- [ ] **Task 10: E2E test scaffolding (Playwright)** (AC: 12)
  - [ ] 10.1 Add `Microsoft.Playwright` package version to `Directory.Packages.props`
  - [ ] 10.2 Create `tests/Hexalith.EventStore.Admin.UI.E2E/` project with xUnit + Playwright
  - [ ] 10.3 Create base test fixture that starts Admin.UI host via WebApplicationFactory
  - [ ] 10.4 First accessibility smoke test: load landing page, run axe-core, assert zero violations
  - [ ] 10.5 First high-contrast test: load landing page in forced-colors mode, screenshot for manual review
  - [ ] 10.6 Verify keyboard navigation: Tab through header → sidebar → main content in correct focus order

## Dev Notes

### Architecture Compliance

- **ADR-P4 (Intentional Deviation)**: ADR-P4 originally specified a single Admin.Server project hosting both REST API and Blazor Web UI. This story creates Admin.UI as a **separate Blazor Server project** communicating with Admin.Server via HTTP REST API (`/api/v1/admin/*`). **Rationale**: Decoupled frontend enables independent scaling, independent testing, cleaner separation of concerns, and avoids mixing API controllers with Blazor Server rendering in one host. ADR-P4 will be updated post-implementation. The UI does NOT use DaprClient — all data flows through the Admin.Server REST API.
- **ADR-P5**: Observability deep-links (trace/metrics/logs URLs) will be consumed from Admin API health endpoint in later stories. For now, ensure the layout has a header area where deep-link buttons can be added.
- Admin.UI project is `IsPackable=false` — it's a deployable host, not a NuGet package.

### Technical Stack (Pinned Versions)

| Package | Version | Source |
|---------|---------|--------|
| Microsoft.FluentUI.AspNetCore.Components | 4.13.2 | Directory.Packages.props |
| Microsoft.FluentUI.AspNetCore.Components.Icons | 4.13.2 | Directory.Packages.props |
| Microsoft.AspNetCore.SignalR.Client | 10.0.5 | Directory.Packages.props |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 | Directory.Packages.props |
| bunit | TBD (add to Directory.Packages.props) | New dependency for Blazor component tests |
| Microsoft.Playwright | TBD (add to Directory.Packages.props) | New dependency for E2E accessibility tests |

All existing packages are centrally managed in `Directory.Packages.props`. Add bUnit and Playwright versions there before referencing.

### Loading & Transition States (UX Spec Compliance)

| State | Pattern | Example |
|-------|---------|---------|
| Initial page load | Skeleton screen matching layout structure | StatCards show gray skeleton placeholders |
| Data refresh | Subtle fade on stale content + loading indicator | Content dims while refreshing |
| Empty data | EmptyState component with guidance and action | Page-specific message + CTA |

Motion: 150ms for micro-interactions, 300ms for layout changes. Respect `prefers-reduced-motion`.

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
      keyboard.js          → Ctrl+K, Ctrl+B shortcut handlers
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

### Sidebar Navigation Items

| Icon Intent | Label | Route | Min Role |
|-------------|-------|-------|----------|
| Home | Home | / | ReadOnly |
| List view | Commands | /commands | ReadOnly |
| Timeline | Events | /events | ReadOnly |
| Health | Health | /health | ReadOnly |
| Server | Services | /services | ReadOnly |
| Settings | Settings | /settings | Admin |

### Authentication Flow

1. Admin.UI registered in Keycloak as OIDC client (same realm as CommandApi and Admin.Server)
2. User authenticates via Keycloak login page
3. JWT Bearer token stored in auth state
4. HttpClientFactory "AdminApi" injects Bearer token on every request to Admin.Server REST API
5. Admin.Server validates JWT + extracts role + enforces tenant scoping
6. UI extracts AdminRole from JWT claims via `AdminUserContext` for conditional rendering

### Previous Story Intelligence (Epic 14)

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

### Project Structure Notes

- Alignment: follows same `src/` folder organization as all other projects
- The project name `Hexalith.EventStore.Admin.UI` matches the architecture spec naming for the Blazor Web UI component
- Test project at `tests/Hexalith.EventStore.Admin.UI.Tests/` follows existing test project naming pattern

### References

- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4, ADR-P5]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR34-DR49]
- [Source: _bmad-output/planning-artifacts/prd.md — FR68-FR75, NFR41, NFR45]
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
