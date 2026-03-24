# Story 16.5: Tenant Management, Quotas, Onboarding & Comparison

Status: ready-for-dev

Size: Large — ~22 new/modified files, 7 task groups, 16 ACs, ~31 tests (~14-18 hours estimated). Core new work: Tenant DTOs + request DTOs + command service interfaces (Task 1), DaprTenantCommandService + controller write endpoints (Task 2), AdminTenantApiClient (Task 3), Tenants.razor full page with detail panel, user management, onboarding wizard, comparison view (Task 4), bUnit tests (Task 5), nav/breadcrumb/CSS updates (Task 6), Aspire topology verification (Task 7).

**Split advisory:** This story is at the upper size bound. If implementation velocity slows, consider splitting into two PRs: (A) Tasks 1-3 + Task 4 partial (tenant list + detail panel + quotas) + Task 5 partial + Task 6, then (B) Task 4 remainder (onboarding wizard + comparison view + user management dialogs) + remaining tests. The natural split point is between "tenant visibility" (list, detail, quotas) and "tenant operations" (create, disable, user management, comparison). Both halves are independently shippable.

## Definition of Done

- All 16 ACs verified
- 16 merge-blocking bUnit tests green (Task 5 blocking tests: 5.1-5.16)
- 15 recommended bUnit tests green (Task 5 recommended tests: 5.17-5.31)
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions

## Story

As a **database administrator (Maria) or infrastructure admin using the Hexalith EventStore admin dashboard**,
I want **a tenant management page where I can view all tenants with their status and quota usage, create new tenants via a guided onboarding wizard, manage tenant lifecycle (enable/disable), view and manage tenant users and roles, inspect quota limits vs current usage, and compare usage metrics across multiple tenants**,
so that **I can perform self-service tenant administration — onboarding new customers, monitoring quota compliance, managing user access, handling tenant suspension, and analyzing cross-tenant resource distribution — all through the admin UI without developer involvement**.

## Acceptance Criteria

1. **Tenant list** — The `/tenants` page displays a `FluentDataGrid` listing all tenants from `ListTenantsAsync`. Columns: Tenant ID (monospace), Display Name, Status (badge — green Active, orange Onboarding, red Suspended), Domains (right-aligned, `N0`), Events (right-aligned, `N0`), Quota Usage (progress bar — green < 75%, orange 75-90%, red > 90%, or "N/A" when quotas not set). Grid is sortable by all columns, default sort by Display Name ascending. When no tenants exist, show `EmptyState` with title "No tenants configured." and description "Create your first tenant to start managing multi-tenant event streams." with "Create Tenant" action button (Admin role only). Clicking a row opens the tenant detail panel.

2. **Summary stat cards** — Above the grid, display four stat cards in a `FluentGrid` (xs=6, sm=6, md=3): (a) "Total Tenants" showing total count, (b) "Active" showing count of tenants with Status == Active, severity Success, (c) "Suspended" showing count with Status == Suspended, severity Error when > 0, (d) "Onboarding" showing count with Status == Onboarding, severity Warning when > 0. Loading state shows 4 `SkeletonCard` placeholders.

3. **Status filter** — A `FluentSelect` dropdown filters tenants by status: "All" (default), "Active", "Suspended", "Onboarding". Filter persists in URL query parameter `?status=<value>`. Additionally, a `FluentTextField` with debounced input (300ms) filters by tenant ID or display name prefix. Filter persists as `?search=<value>`. Same debounce pattern as `Compaction.razor` (Timer + `InvokeAsync`).

4. **Tenant detail panel** — Clicking a tenant row expands an inline detail section below the row (same expand pattern as failed-job error detail in Compaction/Backups pages, but larger). Detail shows: Tenant ID (monospace, copyable), Display Name, Status badge, Created Date (relative time), Domain Count, Total Events, Storage Usage (via `TimeFormatHelper.FormatBytes`). Below the summary, show quota information: Max Events/Day, Max Storage, Current Usage with progress bar. If `Hexalith.Tenants` service is unreachable, show a graceful fallback: "Tenant details unavailable — Tenants service is not responding."

5. **Create tenant dialog (onboarding wizard)** — A "Create Tenant" button (visible only to `Admin` role via `AuthorizedView MinimumRole="AdminRole.Admin"`) opens a multi-step `FluentDialog`. **Step 1 — Basics**: Tenant ID (`FluentTextField`, required, validated as non-empty, URL-safe characters only — alphanumeric + hyphens, lowercase), Display Name (`FluentTextField`, required). **Step 2 — Configuration**: Subscription tier (`FluentSelect`: "Standard", "Premium", "Enterprise" — maps to quota defaults), Max Events/Day (`FluentNumberField`, pre-filled from tier), Max Storage (`FluentNumberField` in MB, pre-filled from tier). **Step 3 — Initial Admin**: Admin User Email (`FluentTextField`, required, email format validation), Admin Role (`FluentSelect`: "Admin", "Operator", "ReadOnly", default "Admin"). **Step 4 — Confirmation**: Summary of all entered values. Dynamic text: "This will create tenant **{tenantId}** with display name **{displayName}** and assign **{email}** as the initial administrator." Primary button "Create Tenant" calls `CreateTenantAsync` then `AddUserToTenantAsync` sequentially. **Partial failure handling:** If `CreateTenantAsync` succeeds but `AddUserToTenantAsync` fails, show warning toast "Tenant {tenantId} created, but failed to add initial admin user: {error}. Add the user manually from the tenant detail panel." Close the dialog and reload (tenant exists, user must be added separately). On full success: close dialog, toast "Tenant {tenantId} created successfully.", reload. On create failure: error toast, stay on current step.

6. **Disable/Enable tenant** — Each tenant row has a lifecycle action button (Admin role only): "Disable" for Active tenants (opens confirmation dialog: "Disable tenant {tenantId}? All command processing for this tenant will be suspended. Existing events are preserved."), "Enable" for Suspended tenants (opens confirmation dialog: "Enable tenant {tenantId}? Command processing will resume."). On confirm: calls `DisableTenantAsync` or `EnableTenantAsync`. Success toast + reload. Concurrent guard: If the tenant is currently Onboarding (provisioning in progress), both buttons are disabled with tooltip "Tenant is being provisioned."

7. **Tenant user management** — The tenant detail panel includes a "Users" tab/section showing a `FluentDataGrid` of users assigned to the tenant (from `GetTenantUsersAsync`). Columns: Email, Role (badge), Added Date (relative time). Admin-only actions: "Add User" button opens dialog (Email + Role fields), "Remove" per-row button with confirmation, "Change Role" per-row dropdown. Each action dispatches the corresponding command: `AddUserToTenantAsync`, `RemoveUserFromTenantAsync`, `ChangeUserRoleAsync`. If the users endpoint is unavailable, show graceful fallback: "User management unavailable — Tenants service is not responding."

8. **Tenant comparison view** — A "Compare Tenants" button (above the grid, visible to all authenticated users) opens a dialog where the user selects 2-5 tenants from a multi-select list. On confirm: calls `CompareTenantUsageAsync` and displays results in a comparison table: rows are metrics (Event Count, Domain Count, Storage Usage, Quota Usage %), columns are selected tenants. Highest value per metric row is highlighted with bold. Close button dismisses the comparison view. If fewer than 2 tenants exist, the "Compare" button is disabled with tooltip "At least 2 tenants required."

9. **Quota usage visualization** — Each tenant's quota usage renders as a `FluentProgressBar` in the grid and detail panel. Colors: green (< 75% used), orange (75-90%), red (> 90%). Tooltip shows exact values: "{current} / {max} ({percent}%)". When MaxStorageBytes is 0 (unlimited/not configured), display "Unlimited" instead of a progress bar.

10. **URL state persistence** — The `/tenants` page persists filter state in URL query parameters: `?status=<status>&search=<text>`. Page loads with filters pre-applied from URL. Uses `NavigationManager.NavigateTo(url, forceLoad: false, replace: true)`. All values escaped with `Uri.EscapeDataString()`.

11. **Breadcrumb integration** — Breadcrumb route label dictionary in `Breadcrumb.razor` already includes `"tenants" -> "Tenants"` (verify — added in Epic 15). Navigating to `/tenants` renders breadcrumb: `Home / Tenants`.

12. **Navigation entry** — The tenants page already appears in `NavMenu.razor` (verify — added in Epic 15, likely uses building/people icon). Ensure it is positioned after "Streams" and before "Storage" in the DBA Operations section. Verify `CommandPaletteCatalog.cs` has entries for "Tenants" and "Tenant Management" navigating to `/tenants`. If missing, add them.

13. **Data loading and refresh** — Initial data loads in `OnInitializedAsync` via `AdminTenantApiClient`. Manual refresh via "Refresh" button. Error state shows `IssueBanner` with "Unable to load tenant data" and retry button. `IAsyncDisposable` for cleanup. When `Hexalith.Tenants` peer service is unavailable, show `IssueBanner` with message "Tenants service is not responding. Tenant data may be stale or unavailable." — do NOT crash the page.

14. **Admin role enforcement** — The `/tenants` page is visible to all authenticated users (`ReadOnly` minimum) for viewing tenant list and details. ALL write operations (create, disable, enable, add user, remove user, change role) require `Admin` role. Write buttons are hidden for non-Admin users via `AuthorizedView MinimumRole="AdminRole.Admin"`. API calls for writes use `Admin` policy. Comparison view is available to all authenticated users (read-only operation).

15. **Accessibility** — Page heading `<h1>Tenants</h1>` is the first focusable element. All dialogs use `FluentDialog` with proper `aria-label`. Multi-step onboarding wizard has step indicators with `aria-current="step"`. Form fields have associated labels. Stat cards follow existing `StatCard` accessibility pattern. Data grid follows `FluentDataGrid` built-in accessibility. Status badges have `aria-label` attributes. Progress bars have `aria-label="Quota usage: {percent}%"`.

16. **Graceful degradation when Tenants service is unavailable** — All API client methods must handle the case where Hexalith.Tenants peer service is down. Read methods return empty lists/default values with a logged warning. Write methods return `AdminOperationResult(Success: false, Message: "Tenants service is unavailable")`. The page shows partial data (e.g., cached tenant list from TopologyCacheService) with a warning banner rather than a blank error page.

## Tasks / Subtasks

- [ ] **Task 1: Expand Tenant DTOs and add command service interface** (AC: 1, 4, 5, 6, 7, 8, 9)
  - [ ] 1.1 Create `src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/TenantDetail.cs` — record:
    ```csharp
    /// <summary>
    /// Detailed tenant information including quota and configuration.
    /// </summary>
    /// <param name="TenantId">The tenant identifier.</param>
    /// <param name="DisplayName">The tenant display name.</param>
    /// <param name="Status">The current tenant status.</param>
    /// <param name="EventCount">Total events for this tenant.</param>
    /// <param name="DomainCount">Number of active domains.</param>
    /// <param name="StorageBytes">Current storage usage in bytes.</param>
    /// <param name="CreatedAtUtc">When the tenant was created.</param>
    /// <param name="Quotas">Quota configuration, null if not set.</param>
    /// <param name="SubscriptionTier">Subscription tier name.</param>
    public record TenantDetail(
        string TenantId,
        string DisplayName,
        TenantStatusType Status,
        long EventCount,
        int DomainCount,
        long StorageBytes,
        DateTimeOffset CreatedAtUtc,
        TenantQuotas? Quotas,
        string? SubscriptionTier);
    ```
  - [ ] 1.2 Create `src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/TenantUser.cs` — record:
    ```csharp
    /// <summary>
    /// A user assigned to a tenant with their role.
    /// </summary>
    /// <param name="Email">The user's email address.</param>
    /// <param name="Role">The user's role within this tenant.</param>
    /// <param name="AddedAtUtc">When the user was added to this tenant.</param>
    public record TenantUser(string Email, string Role, DateTimeOffset AddedAtUtc);
    ```
  - [ ] 1.3 Create `src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/CreateTenantRequest.cs` — record:
    ```csharp
    /// <summary>
    /// Request to create a new tenant.
    /// </summary>
    /// <param name="TenantId">The tenant identifier (URL-safe, lowercase).</param>
    /// <param name="DisplayName">The tenant display name.</param>
    /// <param name="SubscriptionTier">Subscription tier: Standard, Premium, Enterprise.</param>
    /// <param name="MaxEventsPerDay">Maximum events allowed per day.</param>
    /// <param name="MaxStorageBytes">Maximum storage in bytes.</param>
    public record CreateTenantRequest(
        string TenantId,
        string DisplayName,
        string SubscriptionTier,
        long MaxEventsPerDay,
        long MaxStorageBytes);
    ```
  - [ ] 1.4 Create user operation request DTOs in `src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/`:
    ```csharp
    /// <summary>Request to add a user to a tenant.</summary>
    public record AddTenantUserRequest(string Email, string Role);

    /// <summary>Request to remove a user from a tenant.</summary>
    public record RemoveTenantUserRequest(string Email);

    /// <summary>Request to change a user's role within a tenant.</summary>
    public record ChangeTenantUserRoleRequest(string Email, string NewRole);
    ```
    **Security rationale:** Email addresses must be in request body, never in URL paths or query parameters. URLs appear in server logs, browser history, proxy logs, and CDN caches. PII in URLs violates security best practices.
  - [ ] 1.5 Create `src/Hexalith.EventStore.Admin.Abstractions/Services/ITenantCommandService.cs`:
    ```csharp
    using Hexalith.EventStore.Admin.Abstractions.Models.Common;
    using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

    namespace Hexalith.EventStore.Admin.Abstractions.Services;

    /// <summary>
    /// Tenant write operations delegated to Hexalith.Tenants peer service.
    /// EventStore does NOT own tenant state (FR77).
    /// </summary>
    public interface ITenantCommandService
    {
        /// <summary>Creates a new tenant.</summary>
        Task<AdminOperationResult> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default);

        /// <summary>Disables (suspends) an active tenant.</summary>
        Task<AdminOperationResult> DisableTenantAsync(string tenantId, CancellationToken ct = default);

        /// <summary>Enables a suspended tenant.</summary>
        Task<AdminOperationResult> EnableTenantAsync(string tenantId, CancellationToken ct = default);

        /// <summary>Adds a user to a tenant.</summary>
        Task<AdminOperationResult> AddUserToTenantAsync(string tenantId, string email, string role, CancellationToken ct = default);

        /// <summary>Removes a user from a tenant.</summary>
        Task<AdminOperationResult> RemoveUserFromTenantAsync(string tenantId, string email, CancellationToken ct = default);

        /// <summary>Changes a user's role within a tenant.</summary>
        Task<AdminOperationResult> ChangeUserRoleAsync(string tenantId, string email, string newRole, CancellationToken ct = default);
    }
    ```
  - [ ] 1.6 Expand `ITenantQueryService` — add two methods:
    ```csharp
    /// <summary>Gets detailed tenant information including quotas.</summary>
    Task<TenantDetail?> GetTenantDetailAsync(string tenantId, CancellationToken ct = default);

    /// <summary>Gets users assigned to a tenant.</summary>
    Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(string tenantId, CancellationToken ct = default);
    ```
  - [ ] 1.7 **Checkpoint**: All DTOs and interfaces compile, follow existing record patterns (see `TenantSummary.cs`, `BackupJob.cs`).

- [ ] **Task 2: Implement server-side tenant command service and controller endpoints** (AC: 5, 6, 7, 13, 16)
  - [ ] 2.1 Implement `DaprTenantCommandService` in `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs`. Follow `DaprTenantQueryService` pattern exactly — constructor takes `DaprClient`, `IOptions<AdminServerOptions>`, `IAdminAuthContext`, `ILogger`. All methods use `InvokeTenantServiceAsync<T>` helper. Endpoint mapping:
    - `CreateTenantAsync` → POST `api/v1/tenants` on Hexalith.Tenants service
    - `DisableTenantAsync` → POST `api/v1/tenants/{tenantId}/disable`
    - `EnableTenantAsync` → POST `api/v1/tenants/{tenantId}/enable`
    - `AddUserToTenantAsync` → POST `api/v1/tenants/{tenantId}/users`
    - `RemoveUserFromTenantAsync` → POST `api/v1/tenants/{tenantId}/remove-user` (body: `RemoveTenantUserRequest`)
    - `ChangeUserRoleAsync` → POST `api/v1/tenants/{tenantId}/change-role` (body: `ChangeTenantUserRoleRequest`)
    All methods catch exceptions gracefully and return `AdminOperationResult(Success: false, Message: "Tenants service unavailable")` when the peer service is down. JWT is forwarded via `_authContext.GetToken()`.
    **IMPORTANT — Verify Hexalith.Tenants API routes before implementation:** The endpoint paths above are assumed based on REST conventions. Before coding, verify the actual routes by checking the Hexalith.Tenants repository's controller source or OpenAPI spec. If routes differ, adjust `DaprTenantCommandService` endpoint mapping accordingly. Mismatched routes will result in 404 errors from DAPR service invocation.
  - [ ] 2.2 Add `GetTenantDetailAsync` and `GetTenantUsersAsync` to `DaprTenantQueryService`:
    - `GetTenantDetailAsync` → GET `api/v1/tenants/{tenantId}` — return null on 404
    - `GetTenantUsersAsync` → GET `api/v1/tenants/{tenantId}/users` — return empty list on error
  - [ ] 2.3 Add write endpoints to `AdminTenantsController`:
    ```csharp
    [HttpPost]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken ct)

    [HttpPost("{tenantId}/disable")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> DisableTenant(string tenantId, CancellationToken ct)

    [HttpPost("{tenantId}/enable")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> EnableTenant(string tenantId, CancellationToken ct)

    [HttpPost("{tenantId}/users")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> AddUserToTenant(string tenantId, [FromBody] AddTenantUserRequest request, CancellationToken ct)
    // AddTenantUserRequest: record(string Email, string Role) — keep email out of URL/query params for security (PII in logs)

    [HttpPost("{tenantId}/remove-user")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RemoveUserFromTenant(string tenantId, [FromBody] RemoveTenantUserRequest request, CancellationToken ct)
    // POST instead of DELETE — avoids HTTP DELETE-with-body controversy (some proxies strip body from DELETE)

    [HttpPost("{tenantId}/change-role")]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ChangeUserRole(string tenantId, [FromBody] ChangeTenantUserRoleRequest request, CancellationToken ct)
    // POST instead of PUT — email in body, avoids PII-in-URL and DELETE-with-body issues
    ```
    Also add read endpoints for detail and users:
    ```csharp
    [HttpGet("{tenantId}")]
    [ProducesResponseType(typeof(TenantDetail), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantDetail(string tenantId, CancellationToken ct)

    [HttpGet("{tenantId}/users")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantUser>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantUsers(string tenantId, CancellationToken ct)
    ```
    All write endpoints require `AdminAuthorizationPolicies.Admin`. All read endpoints require `AdminAuthorizationPolicies.ReadOnly`. Follow existing `AdminTenantsController` error handling pattern.
  - [ ] 2.4 Register `ITenantCommandService` in `ServiceCollectionExtensions.cs`:
    ```csharp
    services.TryAddScoped<ITenantCommandService, DaprTenantCommandService>();
    ```
  - [ ] 2.5 **Checkpoint**: All endpoints compile, follow existing controller patterns, correct auth policies applied.

- [ ] **Task 3: Create AdminTenantApiClient** (AC: 1, 4, 5, 6, 7, 8, 13, 16)
  - [ ] 3.1 Create `src/Hexalith.EventStore.Admin.UI/Services/AdminTenantApiClient.cs` following `AdminSnapshotApiClient` pattern: constructor takes `IHttpClientFactory` + `ILogger<AdminTenantApiClient>`, uses named client `"AdminApi"`. Mark all public methods `virtual` for NSubstitute mocking.
  - [ ] 3.2 Read methods:
    ```csharp
    public virtual async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken ct = default)
    // → GET api/v1/admin/tenants

    public virtual async Task<TenantDetail?> GetTenantDetailAsync(string tenantId, CancellationToken ct = default)
    // → GET api/v1/admin/tenants/{tenantId}

    public virtual async Task<TenantQuotas> GetTenantQuotasAsync(string tenantId, CancellationToken ct = default)
    // → GET api/v1/admin/tenants/{tenantId}/quotas

    public virtual async Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(string tenantId, CancellationToken ct = default)
    // → GET api/v1/admin/tenants/{tenantId}/users

    public virtual async Task<TenantComparison> CompareTenantUsageAsync(IReadOnlyList<string> tenantIds, CancellationToken ct = default)
    // → POST api/v1/admin/tenants/compare
    // Body: serialize as JSON array of tenant IDs (the server-side TenantCompareRequest in Admin.Server/Models/ wraps this).
    // Client sends: PostAsJsonAsync("api/v1/admin/tenants/compare", new { TenantIds = tenantIds })
    ```
  - [ ] 3.3 Write methods:
    ```csharp
    public virtual async Task<AdminOperationResult?> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default)
    // → POST api/v1/admin/tenants (body: CreateTenantRequest)

    public virtual async Task<AdminOperationResult?> DisableTenantAsync(string tenantId, CancellationToken ct = default)
    // → POST api/v1/admin/tenants/{tenantId}/disable

    public virtual async Task<AdminOperationResult?> EnableTenantAsync(string tenantId, CancellationToken ct = default)
    // → POST api/v1/admin/tenants/{tenantId}/enable

    public virtual async Task<AdminOperationResult?> AddUserToTenantAsync(string tenantId, string email, string role, CancellationToken ct = default)
    // → POST api/v1/admin/tenants/{tenantId}/users (body: AddTenantUserRequest)

    public virtual async Task<AdminOperationResult?> RemoveUserFromTenantAsync(string tenantId, string email, CancellationToken ct = default)
    // → POST api/v1/admin/tenants/{tenantId}/remove-user (body: RemoveTenantUserRequest)

    public virtual async Task<AdminOperationResult?> ChangeUserRoleAsync(string tenantId, string email, string newRole, CancellationToken ct = default)
    // → POST api/v1/admin/tenants/{tenantId}/change-role (body: ChangeTenantUserRoleRequest)
    ```
  - [ ] 3.4 Error handling: copy `HandleErrorStatus` from `AdminSnapshotApiClient` (maps 401, 403, 503).
  - [ ] 3.5 Register `AdminTenantApiClient` as scoped in `Program.cs`: `builder.Services.AddScoped<AdminTenantApiClient>();`
  - [ ] 3.6 **Checkpoint**: Client builds, all methods callable, errors handled gracefully.

- [ ] **Task 4: Implement Tenants.razor page** (AC: 1-16)
  - [ ] 4.1 **Replace** the existing placeholder in `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor` with full implementation. Inject: `AdminTenantApiClient`, `NavigationManager`, `IToastService`. Implement `IAsyncDisposable`.
  - [ ] 4.2 Implement `OnInitializedAsync`: parse URL filters (`?status=`, `?search=`), load tenant list via `ListTenantsAsync`.
  - [ ] 4.3 Render 4 stat cards in `FluentGrid` (xs=6, sm=6, md=3): Total Tenants, Active (Success), Suspended (Error when > 0), Onboarding (Warning when > 0). Show `SkeletonCard` during loading.
  - [ ] 4.4 Render filter bar: `FluentSelect` for status filter (All/Active/Suspended/Onboarding) + `FluentTextField` for search with 300ms debounce. On filter change: update URL, re-filter displayed data (client-side filtering is sufficient — tenant lists are small).
  - [ ] 4.5 Render `FluentDataGrid` with tenant list. Columns: Tenant ID (monospace), Display Name, Status (badge), Domains (right-aligned), Events (right-aligned), Quota Usage (progress bar or "Unlimited"), Actions (lifecycle + expand). Default sort: Display Name ascending. Clicking a row toggles detail expansion.
  - [ ] 4.6 Implement `EmptyState` when no tenants exist (reuse existing `EmptyState` component with building icon).
  - [ ] 4.7 Implement inline detail expansion panel: When a tenant row is clicked, expand a detail section below the row showing tenant details + quota info + user list. **This is structurally different from the simple error-detail toggle in Compaction/Backups** — it requires async data loading into the expanded row. Implementation: track `_expandedTenantId` (string?) and `_expandedDetail` (TenantDetail?) / `_expandedUsers` (IReadOnlyList<TenantUser>?) / `_isLoadingDetail` (bool) state variables. On row click: if same tenant → collapse (set to null). If different → **cancel any in-flight detail load** (`_detailCts?.Cancel(); _detailCts = new CancellationTokenSource();`) to prevent race conditions when user clicks rapidly between rows. Set `_expandedTenantId`, set `_isLoadingDetail = true`, call `GetTenantDetailAsync` + `GetTenantUsersAsync` in parallel with `_detailCts.Token`, then set `_isLoadingDetail = false`. Render: below the expanded row, show `FluentProgressRing` while loading, then detail content. If Tenants service is unreachable, show inline warning "Tenant details unavailable — Tenants service is not responding."
  - [ ] 4.8 Implement tenant user management within the detail panel. **Use stacked sections with `<h3>` headers (Summary, Quotas, Users), not `FluentTabs`** — tabs inside an expanded grid row have rendering quirks in FluentUI v4. User section renders a `FluentDataGrid` for users (Email, Role badge, Added Date). **Ensure inner user `FluentDataGrid` uses a distinct `TGridItem="TenantUser"` type to avoid column ID conflicts with the outer tenant grid.** Admin-only "Add User" button, per-row "Remove" and "Change Role" actions. "Add User" opens dialog: Email (required, email format) + Role dropdown (Admin/Operator/ReadOnly). "Remove" shows confirmation dialog. "Change Role" opens dialog with role dropdown.
  - [ ] 4.9 Implement "Create Tenant" button in page header (Admin-only). Multi-step `FluentDialog` (4 steps): Basics → Configuration → Initial Admin → Confirmation. **This is the most complex dialog in Epic 16 — 4 steps vs 2 in Backups restore. Implement the wizard inline in Tenants.razor — do NOT extract a reusable wizard component.** Implementation: use `_wizardStep` (int, 1-4) state variable. Per-step state: `_wizardTenantId`, `_wizardDisplayName`, `_wizardTier`, `_wizardMaxEvents`, `_wizardMaxStorage`, `_wizardAdminEmail`, `_wizardAdminRole`. **On dialog open, reset all `_wizard*` fields to defaults and set `_wizardStep = 1`** — prevents stale data from a previous cancelled attempt. Per-step validation methods: `IsStep1Valid()` (tenantId non-empty, URL-safe regex `^[a-z0-9-]+$`, displayName non-empty), `IsStep2Valid()` (tier selected, quotas > 0), `IsStep3Valid()` (email format regex, role selected). **Tenant ID regex:** Verify this regex matches what Hexalith.Tenants accepts for TenantId — check the `CreateTenantValidator` in Hexalith.Tenants.Contracts. If they allow dots or underscores, adjust. "Next" button disabled until current step is valid. "Back" preserves entered values. **FluentNumberField:** Verify `FluentNumberField` exists in FluentUI v4.13.2 before using. If not available, use `FluentTextField` with `type="number"` and manual `long.TryParse` conversion. On step 4 confirm: calls `CreateTenantAsync` then `AddUserToTenantAsync`. Subscription tier pre-fills (hardcoded defaults for v1 — not backend-driven): Standard (10K events/day, 1 GB), Premium (100K events/day, 10 GB), Enterprise (1M events/day, 100 GB). **Partial failure handling:** If create succeeds but add-user fails, show warning toast and close dialog (tenant exists, user can be added from detail panel). Full success → close + success toast + reload. Create failure → error toast + stay on step.
  - [ ] 4.10 Implement "Disable" / "Enable" per-row action buttons (Admin-only). Confirmation dialogs with clear warnings. Disable: "All command processing for this tenant will be suspended. Existing events are preserved." Enable: "Command processing will resume."
  - [ ] 4.11 Implement "Compare Tenants" button. Opens dialog with multi-select tenant list (checkboxes, min 2, max 5). **Tenant selection list must have `max-height: 300px; overflow-y: auto;`** to handle environments with many tenants gracefully. Enforce max 5 selection client-side (disable unchecked items when 5 are selected). On confirm: calls `CompareTenantUsageAsync`. Results display in a comparison table: rows = metrics, columns = selected tenants. Highest value per row highlighted.
  - [ ] 4.12 Implement quota progress bars: green < 75%, orange 75-90%, red > 90%. Use `FluentProgressBar` with `aria-label`. Show "Unlimited" when max is 0.
  - [ ] 4.13 Implement URL state management: `ReadUrlParameters()` on init, `UpdateUrl()` on filter change with `replace: true`.
  - [ ] 4.14 Add manual "Refresh" button, `IssueBanner` for error state (with specific message for Tenants service unavailability vs general errors), `IAsyncDisposable` for Timer + CancellationTokenSource cleanup.
  - [ ] 4.15 Add action buttons in page header, gated by `AuthorizedView MinimumRole="AdminRole.Admin"`:
    - "Create Tenant" (primary, accent)
    - "Compare Tenants" (outline — visible to all users, no auth gate)
  - [ ] 4.16 **Checkpoint**: Page loads, stat cards show, grid renders with sorting/filtering, detail panel expands, user management works, create wizard completes, disable/enable lifecycle works, comparison view works, role enforcement active, URL state persists, graceful degradation when Tenants service is down.

- [ ] **Task 5: bUnit and unit tests** (AC: 1-16)
  - **Mock dependencies**: extend `AdminUITestContext`, mock `AdminTenantApiClient` using `Substitute.For<AdminTenantApiClient>(...)` with `IHttpClientFactory` + `NullLogger<AdminTenantApiClient>.Instance`.
  - **Culture sensitivity**: Any test asserting formatted numbers must use culture-invariant assertions (lesson from story 16-1 with French locale).
  - **Merge-blocking tests**:
  - [ ] 5.1 Test `Tenants` page renders 4 stat cards with correct values from tenant list (AC: 2)
  - [ ] 5.2 Test `Tenants` page shows `SkeletonCard` during loading state (AC: 2)
  - [ ] 5.3 Test tenant grid renders all tenants with correct columns (AC: 1)
  - [ ] 5.4 Test `EmptyState` shown when no tenants exist (AC: 1)
  - [ ] 5.5 Test `IssueBanner` shown when API returns error (AC: 13)
  - [ ] 5.6 Test tenants page has `<h1>Tenants</h1>` heading (AC: 15)
  - [ ] 5.7 Test "Create Tenant" button hidden for ReadOnly and Operator users (AC: 14)
  - [ ] 5.8 Test "Create Tenant" button visible for Admin users (AC: 14)
  - [ ] 5.9 Test create tenant wizard calls `CreateTenantAsync` on confirm, reloads list, shows success toast (AC: 5)
  - [ ] 5.10 Test create tenant wizard shows error toast when API returns failure (AC: 5)
  - [ ] 5.11 Test status badges render correct appearance per tenant status (AC: 1)
  - [ ] 5.12 Test "Disable" button only appears on Active tenants (AC: 6)
  - [ ] 5.13 Test "Enable" button only appears on Suspended tenants (AC: 6)
  - [ ] 5.14 Test quota progress bar colors: green < 75%, orange 75-90%, red > 90% (AC: 9)
  - [ ] 5.15 Test create tenant wizard partial failure: CreateTenantAsync succeeds but AddUserToTenantAsync fails — shows warning toast, closes dialog, tenant appears in list (AC: 5)
  - [ ] 5.16 Test "Compare Tenants" button visible and functional for ReadOnly users (AC: 14) — verifies comparison is a read-only operation accessible to all roles
  - **Recommended tests**:
  - [ ] 5.17 Test URL parameters read on page initialization (AC: 10)
  - [ ] 5.18 Test status filter shows correct subset of tenants (AC: 3)
  - [ ] 5.19 Test search filter filters by tenant ID prefix (AC: 3)
  - [ ] 5.20 Test tenant detail panel loads on row click (AC: 4)
  - [ ] 5.21 Test tenant detail shows "Tenants service not responding" when service is unavailable (AC: 16)
  - [ ] 5.22 Test "Add User" dialog calls `AddUserToTenantAsync` (AC: 7)
  - [ ] 5.23 Test "Remove User" confirmation dialog calls `RemoveUserFromTenantAsync` (AC: 7)
  - [ ] 5.24 Test "Change Role" dialog calls `ChangeUserRoleAsync` with correct params (AC: 7)
  - [ ] 5.25 Test "Create Tenant" shows error toast when Tenants service is down during write (AC: 16) — write-operation graceful degradation
  - [ ] 5.26 Test "Compare Tenants" button disabled when fewer than 2 tenants (AC: 8)
  - [ ] 5.27 Test comparison table renders correct metrics for selected tenants (AC: 8)
  - [ ] 5.28 Test comparison table highlights highest value per metric row with bold (AC: 8)
  - [ ] 5.29 Test create wizard step 2 pre-fills quotas from subscription tier selection (AC: 5)
  - [ ] 5.30 Test "Disable" and "Enable" buttons disabled for Onboarding tenants (AC: 6)
  - [ ] 5.31 Test quota shows "Unlimited" when MaxStorageBytes is 0 (AC: 9)

- [ ] **Task 6: Navigation, Command Palette, and CSS** (AC: 11, 12, 15)
  - [ ] 6.1 **Verify** `Breadcrumb.razor` already has `"tenants" -> "Tenants"`. If missing, add it.
  - [ ] 6.2 **Verify** `NavMenu.razor` already has Tenants `FluentNavLink`. If icon needs updating, use `Icons.Regular.Size20.Building` (tenant = building). Verify icon compiles — `TreatWarningsAsErrors` is enabled.
  - [ ] 6.3 **Verify** `CommandPaletteCatalog.cs` has entries for `("Actions", "Tenants", "/tenants")` and `("Tenants", "Tenant Management", "/tenants")`. If missing, add them.
  - [ ] 6.4 Add CSS styles in `wwwroot/css/app.css`:
    - `.tenant-status-onboarding` with pulse animation (reuse `.compaction-status-running` pattern)
    - `.tenant-quota-bar` for progress bar container
    - `.tenant-detail-panel` for expanded detail section (padded, muted background, border-top)
    - `.tenant-comparison-table` for comparison layout
    - `.tenant-user-grid` for nested user data grid within detail panel
  - [ ] 6.5 **Checkpoint**: Tenants page accessible from sidebar, breadcrumb shows "Home / Tenants", Ctrl+K "Tenants" navigates correctly, styles applied.

- [ ] **Task 7: Verify Aspire topology and integration** (AC: 16)
  - [ ] 7.1 Verify `AdminServerOptions` in `src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs` has `TenantServiceAppId` property (used by `DaprTenantQueryService`). If the default value needs updating, set it to `"hexalith-tenants"` or whatever the Hexalith.Tenants DAPR app ID is in the Aspire topology.
  - [ ] 7.2 Verify the Aspire AppHost already orchestrates Hexalith.Tenants as a peer resource (should have been done in story 14-4). If not present, this story's scope is **UI only** — the Tenants.razor page degrades gracefully when the service is unavailable (AC: 16). Log a note in Dev Agent Record that full integration requires Hexalith.Tenants to be running in the topology.
  - [ ] 7.3 **Checkpoint**: Topology verified, graceful degradation confirmed.

## Dev Notes

### Architecture Compliance

- **ADR-P4**: Admin.UI communicates exclusively via HTTP REST API to Admin.Server. The `AdminTenantApiClient` calls `api/v1/admin/tenants/*` endpoints — no direct DAPR access. Admin.Server delegates tenant operations to Hexalith.Tenants peer service via DAPR service invocation (`DaprClient.InvokeMethodAsync`).
- **FR77**: Tenant management is owned by Hexalith.Tenants bounded context. EventStore admin is a **consumer**, not the owner. All tenant commands (Create, Disable, Enable, AddUser, etc.) are dispatched to Hexalith.Tenants CommandApi. EventStore never directly modifies tenant state.
- **Sprint Change Proposal (2026-03-21)**: Approved scope change — story consumes Hexalith.Tenants.Client packages rather than building tenant domain logic internally. [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-21-tenant-management.md]
- **NFR40**: Admin API responses: 500ms at p99 for reads, 2s at p99 for writes.
- **NFR41**: Initial render ≤ 2 seconds. Tenant list is typically small.
- **NFR44**: All admin data access through DAPR abstractions. Tenant data accessed via DAPR service invocation to Hexalith.Tenants, not direct state store reads.
- **NFR46**: **Admin role required** for all tenant write operations. The `AdminRole` enum states: "Admin: Operator + tenant management, backup/restore."

### Scope — UI + API Surface (Not Tenant Domain Logic)

**CRITICAL SCOPE DECISION:** This story delivers the **admin UI, REST API controller endpoints, API client, and DAPR service invocation layer** for tenant management. The actual tenant domain logic (aggregates, events, validators, projections) lives in **Hexalith.Tenants** — a separate bounded context.

**What this story builds:**
- Tenants.razor full page (list, detail, users, quotas, comparison)
- Create Tenant multi-step onboarding wizard
- Disable/Enable tenant lifecycle management
- User management (add, remove, change role)
- Quota visualization and comparison view
- AdminTenantApiClient (UI → Admin.Server HTTP calls)
- DaprTenantCommandService (Admin.Server → Hexalith.Tenants DAPR calls)
- Controller write endpoints (REST API surface)
- bUnit tests

**What this story does NOT build:**
- Tenant aggregates, commands, events, or projections (owned by Hexalith.Tenants)
- DAPR state store keys for tenant state (owned by Hexalith.Tenants)
- Hexalith.Tenants CommandApi endpoints (consumed, not built)
- Tenant authentication/claims transformation (existing Epic 5 implementation)
- Tenant-scoped data isolation (existing triple-layer isolation in Core)

### Story Sequencing — No Dependency on Story 16-4

Story 16-4 (Backup & Restore Console) is currently `review`. Story 16-5 has **no code dependency on 16-4** — they touch different pages, different DTOs, different controllers, and different API clients. They can run in parallel on separate branches. The only shared touchpoints are `NavMenu.razor` and `CommandPaletteCatalog.cs` (both additive changes — no merge conflicts expected). If both stories are being implemented concurrently, coordinate the final merge to avoid trivial conflicts in those two files.

### Dependency: Hexalith.Tenants Service Availability

**The Hexalith.Tenants peer service may not be running in all development environments.** The UI must degrade gracefully:

1. **Service unavailable**: `DaprTenantQueryService` catches exceptions, returns empty data, logs warning
2. **Page behavior**: Shows `IssueBanner` with "Tenants service is not responding", displays cached/empty state
3. **Write operations**: Return `AdminOperationResult(Success: false)` with user-friendly error message
4. **TopologyCacheService**: Already fetches tenants and caches them — the Tenants page can fall back to this cache for the list view

This ensures the admin UI is functional even when Hexalith.Tenants is temporarily unavailable.

### Existing Infrastructure to Reuse (DO NOT Recreate)

The following already exists and must be reused — do NOT duplicate:

| Component | Location | What It Does |
|-----------|----------|-------------|
| `TenantSummary` record | `Admin.Abstractions/Models/Tenants/TenantSummary.cs` | Tenant list item DTO |
| `TenantQuotas` record | `Admin.Abstractions/Models/Tenants/TenantQuotas.cs` | Quota DTO |
| `TenantComparison` record | `Admin.Abstractions/Models/Tenants/TenantComparison.cs` | Comparison DTO |
| `TenantStatusType` enum | `Admin.Abstractions/Models/Tenants/TenantStatusType.cs` | Active/Suspended/Onboarding |
| `ITenantQueryService` | `Admin.Abstractions/Services/ITenantQueryService.cs` | Query interface (3 methods — extend, don't replace) |
| `DaprTenantQueryService` | `Admin.Server/Services/DaprTenantQueryService.cs` | DAPR delegation (extend, don't replace) |
| `AdminTenantsController` | `Admin.Server/Controllers/AdminTenantsController.cs` | REST endpoints (extend, don't replace) |
| `TenantCompareRequest` | `Admin.Server/Models/TenantCompareRequest.cs` | Compare request body DTO |
| `AdminStreamApiClient.GetTenantsAsync()` | `Admin.UI/Services/AdminStreamApiClient.cs:93` | Existing tenant list fetch — **intentional duplication**: this method is used by Streams.razor and TopologyCacheService for tenant dropdown filters. `AdminTenantApiClient.ListTenantsAsync()` is the new dedicated client for the Tenants page. Do NOT remove `AdminStreamApiClient.GetTenantsAsync()` and do NOT "refactor" to eliminate the duplication — both call paths serve different consumers. |
| `TopologyCacheService` | `Admin.UI/Services/TopologyCacheService.cs` | Caches tenant list |
| NavMenu tenant link | `Admin.UI/Layout/NavMenu.razor` | Already exists |
| Breadcrumb "tenants" | `Admin.UI/Layout/Breadcrumb.razor` | Already exists |
| Tenants.razor placeholder | `Admin.UI/Pages/Tenants.razor` | **Replace this entirely** |

### UI Pattern Consistency

Follow the established Epic 16 DBA page patterns:

1. **Page layout**: Header with h1 + action buttons → stat cards → filter bar → data grid (see Compaction.razor, Snapshots.razor, Backups.razor)
2. **Dialog pattern**: `FluentDialog` with `FluentDialogHeader`, `FluentDialogBody`, `FluentDialogFooter`. Single-step for simple operations, multi-step for complex ones (see Backups.razor restore dialog for multi-step)
3. **Error handling**: `IssueBanner` for API errors, toast for operation results, graceful fallback for service unavailability
4. **Auth gating**: `AuthorizedView MinimumRole="AdminRole.Admin"` wrapping write-action buttons
5. **Debounce**: Timer + `InvokeAsync(() => StateHasChanged())` in callback for Blazor Server threading
6. **URL state**: `ReadUrlParameters()` + `NavigationManager.NavigateTo(url, forceLoad: false, replace: true)`
7. **Data loading**: `_isLoading`, `_apiUnavailable`, CancellationTokenSource, `IAsyncDisposable`
8. **Stat card severity**: neutral (default), Success (green), Warning (orange), Error (red)
9. **Badge patterns**: `FluentBadge` with `Appearance.Success` (Active), `Appearance.Error` (Suspended), `Appearance.Warning` (Onboarding)
10. **Grid columns**: monospace for IDs, right-aligned for numbers, `TimeFormatHelper.FormatRelativeTime` for dates, `TimeFormatHelper.FormatBytes` for storage

### Previous Story Learnings

From stories 16-1 through 16-4:
- **Culture sensitivity in tests**: Number formatting tests must use culture-invariant assertions (16-1 lesson with French locale `CultureInfo.InvariantCulture`)
- **Icon verification**: Always verify `Icons.Regular.Size20.*` icon class exists at compile time — `TreatWarningsAsErrors` is enabled
- **FluentUI v4.13.2 API**: Verify component APIs match v4 documentation, not v5
- **Debounce threading**: Timer callbacks must use `InvokeAsync` for Blazor Server thread safety
- **Empty state pattern**: Always handle empty data with `EmptyState` component, not blank space
- **Service unavailability**: Always handle `ServiceUnavailableException` and show `IssueBanner` — do NOT let the page crash

### Git Intelligence

Recent commit patterns (from Epic 16):
```
17c8407 feat: Add compaction manager page with job history and trigger dialog for story 16-3
ce95956 feat: Add snapshot management page with auto-snapshot policies for story 16-2
4a10138 feat: Add storage growth analyzer with treemap visualization for story 16-1
```
Commit message pattern: `feat: Add <page description> for story 16-X`
Expected commit: `feat: Add tenant management page with quotas, onboarding, and comparison for story 16-5`

### Project Structure Notes

All new files follow existing folder structure:
- DTOs → `src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/`
- Interfaces → `src/Hexalith.EventStore.Admin.Abstractions/Services/`
- Server impls → `src/Hexalith.EventStore.Admin.Server/Services/`
- Controllers → `src/Hexalith.EventStore.Admin.Server/Controllers/`
- UI clients → `src/Hexalith.EventStore.Admin.UI/Services/`
- Pages → `src/Hexalith.EventStore.Admin.UI/Pages/`
- Tests → `tests/Hexalith.EventStore.Admin.UI.Tests/` (or similar existing test project)

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-21-tenant-management.md] — Approved scope change
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-P4] — Admin Server architecture
- [Source: _bmad-output/planning-artifacts/architecture.md#Cross-Cutting-11] — Admin data access pattern
- [Source: _bmad-output/planning-artifacts/architecture.md#Peer-Services] — Hexalith.Tenants integration
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#Tenant-Scope-Selector] — UX patterns
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-16] — Epic context and story requirements
- [Source: _bmad-output/implementation-artifacts/16-4-backup-and-restore-console.md] — Previous story patterns
- [Source: _bmad-output/implementation-artifacts/16-3-compaction-manager.md] — Established page patterns

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
