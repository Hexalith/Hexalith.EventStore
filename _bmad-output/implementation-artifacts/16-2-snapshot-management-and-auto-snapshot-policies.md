# Story 16.2: Snapshot Management and Auto-Snapshot Policies

Status: done

Size: Medium — ~11 new/modified files, 5 task groups, 13 ACs, ~21 tests (~8-12 hours estimated). Core new work: AdminSnapshotApiClient (Task 1), server-side delete endpoint + interface + implementation (Task 2), Snapshots page with policy grid + CRUD dialogs (Task 3), bUnit tests (Task 4), nav + breadcrumb + CSS + cross-page links (Task 5).

## Definition of Done

- All 13 ACs verified
- 11 merge-blocking bUnit tests green (Task 4 blocking tests, including write-operation happy/error paths)
- 10 recommended bUnit tests green
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions

## Story

As a **database administrator (Maria) using the Hexalith EventStore admin dashboard**,
I want **a snapshot management page where I can view existing snapshot policies, create or edit auto-snapshot policies per aggregate type, manually trigger snapshot creation for specific aggregates, and delete obsolete policies**,
so that **I can optimize storage usage and reduce event replay times for large aggregates by configuring automatic snapshot intervals — all through the admin UI without developer involvement**.

## Acceptance Criteria

1. **Snapshot policy list** — The `/snapshots` page displays a `FluentDataGrid` listing all snapshot policies from `GetSnapshotPoliciesAsync`. Columns: Tenant ID (monospace), Domain, Aggregate Type, Interval (events, right-aligned, `N0`), Created (relative time using `FormatRelativeTime`, with full UTC timestamp on tooltip). Grid is sortable by all columns, default sort by Tenant ID ascending then Domain ascending. When no policies exist, show `EmptyState` with title "No snapshot policies configured" and description "Create an auto-snapshot policy to automatically snapshot aggregates after a specified number of events."

2. **Summary stat cards** — Above the grid, display three stat cards in a `FluentGrid` (xs=6, sm=6, md=4): (a) "Total Policies" showing the count of snapshot policies, (b) "Tenants Covered" showing the distinct tenant count from policies, (c) "Avg Interval" showing the mean `IntervalEvents` across all policies formatted as `N0` events, or "N/A" when no policies exist. Loading state shows 3 `SkeletonCard` placeholders.

3. **Tenant filter** — A `FluentTextField` with debounced input (300ms) filters policies by tenant ID prefix. Filter persists in URL query parameter `?tenant=<id>`. Clearing the filter shows all policies. Same debounce pattern as `Storage.razor` (Timer + `InvokeAsync` for Blazor Server threading).

4. **Create policy dialog** — An "Add Policy" button (visible only to `Operator` role or above via `AuthorizedView`) opens a `FluentDialog` with form fields: Tenant ID (`FluentTextField`, required), Domain (`FluentTextField`, required), Aggregate Type (`FluentTextField`, required), Interval Events (`FluentNumberField`, required, min=1, default=100). Primary button "Create Policy" calls `SetSnapshotPolicyAsync`. On success: close dialog, show toast "Snapshot policy created.", reload policy list. On failure: show error toast with `AdminOperationResult.Message`, keep dialog open.

5. **Edit policy dialog** — Clicking a policy row opens a `FluentDialog` pre-filled with the policy's values. Only `IntervalEvents` is editable (tenant/domain/aggregate type are read-only display fields identifying the policy). Primary button "Update Policy" calls `SetSnapshotPolicyAsync` with updated interval. Same success/failure pattern as create.

6. **Delete policy** — Each policy row has a "Delete" action button (visible only to `Operator` role or above). Clicking opens a confirmation `FluentDialog`: title "Delete Snapshot Policy", body "This will remove the auto-snapshot policy for {AggregateType} in {Domain} ({TenantId}). Existing snapshots will not be deleted.", primary button "Delete Policy" (danger style), secondary "Cancel". On confirm: call `DELETE api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy`. On success: toast "Policy deleted.", reload list. On failure: error toast.

7. **Manual snapshot creation** — A "Create Snapshot" button (visible only to `Operator` role or above) opens a `FluentDialog` with fields: Tenant ID, Domain, Aggregate ID (all required `FluentTextField`). Primary button "Create Snapshot" calls `CreateSnapshotAsync`. On success: toast "Snapshot created for {aggregateId}.", close dialog. On failure: error toast with details. This enables Maria to create one-off snapshots for specific aggregates identified as problematic from the Storage page (story 16-1).

8. **URL state persistence** — The `/snapshots` page persists filter state in URL query parameters: `?tenant=<id>` (tenant filter). Page loads with filter pre-applied from URL. Uses `NavigationManager.NavigateTo(url, forceLoad: false, replace: true)`. All values escaped with `Uri.EscapeDataString()`. **Create dialog pre-fill:** When URL contains `?create=true&tenant=X&domain=Y&aggregateType=Z`, the page auto-opens the create policy dialog pre-filled with those values. This enables the Storage page's snapshot risk banner to deep-link directly into policy creation for a specific aggregate type.

9. **Breadcrumb integration** — The breadcrumb route label dictionary in `Breadcrumb.razor` includes `"snapshots" -> "Snapshots"`. Navigating to `/snapshots` renders breadcrumb: `Home / Snapshots`.

10. **Navigation entry** — The snapshots page appears in `NavMenu.razor` after "Storage", with a `Icons.Regular.Size20.Camera` icon (snapshot metaphor). Visible to all authenticated users (the page is viewable by ReadOnly; write actions are operator-gated). Add "Snapshots" and "Snapshot Policies" entries to `CommandPaletteCatalog.cs` navigating to `/snapshots`.

11. **Data loading and refresh** — Initial data loads in `OnInitializedAsync` via `AdminSnapshotApiClient`. Manual refresh only via "Refresh" button (same rationale as Storage page — policies change infrequently). Error state shows `IssueBanner` with "Unable to load snapshot policies" and retry button. `IAsyncDisposable` for cleanup.

12. **Admin role enforcement** — The `/snapshots` page is visible to all authenticated users (`ReadOnly` minimum) for viewing policies. All write operations (create/edit/delete policy, create snapshot) require `Operator` role minimum. Write action buttons are hidden for `ReadOnly` users via `AuthorizedView`. API calls for write operations use `Operator`-level endpoints.

13. **Accessibility** — Page heading `<h1>Snapshots</h1>` is the first focusable element. All dialogs use `FluentDialog` with proper `aria-label`. Form fields have associated labels. Stat cards follow existing `StatCard` accessibility pattern. Data grid follows `FluentDataGrid` built-in accessibility. Action buttons have `aria-label` attributes.

## Tasks / Subtasks

- [x] **Task 1: Create AdminSnapshotApiClient** (AC: 1, 4, 5, 6, 7, 11)
  - [x] 1.1 Create `src/Hexalith.EventStore.Admin.UI/Services/AdminSnapshotApiClient.cs` following `AdminStorageApiClient` pattern exactly: constructor takes `IHttpClientFactory` + `ILogger<AdminSnapshotApiClient>`, uses named client `"AdminApi"`. **Rationale for separate client:** `AdminStorageApiClient` is read-only analytics (overview, hot streams); `AdminSnapshotApiClient` handles operator-level mutations with different error semantics (returns `AdminOperationResult` instead of empty defaults).
  - [x] 1.2 Add method `GetSnapshotPoliciesAsync(string? tenantId, CancellationToken ct)` → `HttpClient.GetAsync("api/v1/admin/storage/snapshot-policies?tenantId={id}")`, returns `IReadOnlyList<SnapshotPolicy>`. Return empty list on error.
  - [x] 1.3 Add method `SetSnapshotPolicyAsync(string tenantId, string domain, string aggregateType, int intervalEvents, CancellationToken ct)` → `HttpClient.PutAsync("api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy?intervalEvents={n}", null)` (empty body, interval via query param), returns `AdminOperationResult?`. Throw `ServiceUnavailableException` on non-auth errors.
  - [x] 1.4 Add method `CreateSnapshotAsync(string tenantId, string domain, string aggregateId, CancellationToken ct)` → `HttpClient.PostAsync("api/v1/admin/storage/{tenantId}/{domain}/{aggregateId}/snapshot", null)` (empty body), returns `AdminOperationResult?`. Same error pattern.
  - [x] 1.5 Add method `DeleteSnapshotPolicyAsync(string tenantId, string domain, string aggregateType, CancellationToken ct)` → `HttpClient.DeleteAsync("api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy")`, returns `AdminOperationResult?`. Same error pattern. **Depends on:** Task 2 (server endpoint must exist).
  - [x] 1.6 Error handling: copy `HandleErrorStatus` from `AdminStorageApiClient`. Mark all public methods `virtual` for NSubstitute mocking.
  - [x] 1.7 Register `AdminSnapshotApiClient` as scoped in `Program.cs`: `builder.Services.AddScoped<AdminSnapshotApiClient>();`
  - [x] 1.8 **Checkpoint**: Client builds, all methods callable, errors handled gracefully.

- [x] **Task 2: Add delete endpoint to AdminStorageController** (AC: 6)
  - [x] 2.1 Add `DeleteSnapshotPolicyAsync(string tenantId, string domain, string aggregateType, CancellationToken ct)` to `IStorageCommandService` interface in `src/Hexalith.EventStore.Admin.Abstractions/Services/IStorageCommandService.cs`, returning `Task<AdminOperationResult>`.
  - [x] 2.2 Implement `DeleteSnapshotPolicyAsync` in the DAPR-backed `StorageCommandService` implementation class. **Find it by searching:** `grep -r "IStorageCommandService" src/Hexalith.EventStore.Admin.Server/` — look in `Services/` or `Storage/` subdirectories. Follow the same pattern as `SetSnapshotPolicyAsync` implementation — delete the snapshot policy key from the DAPR state store. Return `AdminOperationResult` with `Success = true` on success, or `ErrorCode = "NotFound"` if the policy doesn't exist.
  - [x] 2.3 Add `DeleteSnapshotPolicy` action to `AdminStorageController`: `[HttpDelete("{tenantId}/{domain}/{aggregateType}/snapshot-policy")]` with `[Authorize(Policy = AdminAuthorizationPolicies.Operator)]`. Delegates to `IStorageCommandService.DeleteSnapshotPolicyAsync`. Uses `MapOperationResult` for response (returns 200 with body, consistent with existing pattern — do NOT use 204).
  - [x] 2.4 **Checkpoint**: Delete endpoint compiles, follows same pattern as existing controller actions, implementation handles not-found case.

- [x] **Task 3: Create Snapshots.razor page** (AC: 1, 2, 3, 4, 5, 6, 7, 8, 11, 12, 13)
  - [x] 3.1 Create `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` with `@page "/snapshots"`. Inject: `AdminSnapshotApiClient`, `NavigationManager`, `IToastService`. Implement `IAsyncDisposable`.
  - [x] 3.2 Implement `OnInitializedAsync`: parse URL tenant filter, load policies via `GetSnapshotPoliciesAsync`.
  - [x] 3.3 Render 3 stat cards in `FluentGrid` (xs=6, sm=6, md=4): Total Policies, Tenants Covered, Avg Interval. Show `SkeletonCard` during loading.
  - [x] 3.4 Render tenant filter `FluentTextField` with 300ms debounce Timer. On filter change: update URL, reload data. Use `InvokeAsync(() => { ... })` in Timer callback for Blazor Server threading safety.
  - [x] 3.5 Render `FluentDataGrid` with policy list. Columns: Tenant ID (monospace), Domain, Aggregate Type, Interval (right-aligned), Created (relative time), Actions (Edit/Delete buttons gated by `AuthorizedView`). Default sort: Tenant ID asc. Clicking a row opens the edit dialog. Delete button wrapped in div with `@onclick:stopPropagation` to prevent row click.
  - [x] 3.6 Implement `EmptyState` when no policies match the current filter.
  - [x] 3.7 Add "Add Policy" button in page header, gated by `AuthorizedView MinimumRole="AdminRole.Operator"`. On click: open create dialog.
  - [x] 3.8 Implement create policy `FluentDialog` with form fields: Tenant ID, Domain, Aggregate Type (all `FluentTextField`, required), Interval Events (`FluentNumberField`, min=1, default=100). Validate all fields non-empty before enabling "Create Policy" button. On success: close dialog, toast, reload. On failure: error toast, keep dialog open.
  - [x] 3.9 Implement edit policy `FluentDialog`: pre-filled fields, only IntervalEvents editable. Tenant/Domain/AggregateType displayed as read-only text. "Update Policy" button calls `SetSnapshotPolicyAsync`.
  - [x] 3.10 Implement delete confirmation `FluentDialog` per AC 6 spec. Danger styling for primary action.
  - [x] 3.11 Add "Create Snapshot" button in page header (operator-gated). Dialog with Tenant ID, Domain, Aggregate ID fields. Calls `CreateSnapshotAsync`.
  - [x] 3.12 Add manual "Refresh" button, `IssueBanner` for error state, `IAsyncDisposable` for Timer + CancellationTokenSource cleanup.
  - [x] 3.13 Add URL state management: `ReadUrlParameters()` on init, `UpdateUrl()` on filter change with `replace: true`. Also handle `?create=true&tenant=X&domain=Y&aggregateType=Z` URL params — when present, auto-open the create dialog pre-filled with those values after initial data load.
  - [x] 3.14 **Checkpoint**: Page loads, stat cards show, grid renders, CRUD dialogs work, role enforcement active, URL state persists, pre-fill from URL works.

- [x] **Task 4: bUnit and unit tests** (AC: 1-13)
  - **Mock dependencies**: extend `AdminUITestContext`, mock `AdminSnapshotApiClient` using `Substitute.For<AdminSnapshotApiClient>(...)` with `IHttpClientFactory` + `NullLogger<AdminSnapshotApiClient>.Instance`.
  - **Culture sensitivity**: Any test asserting `N0` formatted numbers (e.g., stat card values) must use `CultureInfo.InvariantCulture` or format-string assertions rather than hardcoded locale-dependent strings. Story 16-1 learned this the hard way with French locale producing "1,0" instead of "1.0".
  - **Merge-blocking tests**:
  - [x] 4.1 Test `Snapshots` page renders 3 stat cards with correct values from policy list (AC: 2)
  - [x] 4.2 Test `Snapshots` page shows `SkeletonCard` during loading state (AC: 2)
  - [x] 4.3 Test policy grid renders all policies with correct columns (AC: 1)
  - [x] 4.4 Test `EmptyState` shown when no policies exist (AC: 1)
  - [x] 4.5 Test `IssueBanner` shown when API returns error (AC: 11)
  - [x] 4.6 Test snapshots page has `<h1>Snapshots</h1>` heading (AC: 13)
  - [x] 4.7 Test "Add Policy" button hidden for ReadOnly users (AC: 12)
  - [x] 4.8 Test "Add Policy" button visible for Operator users (AC: 12)
  - [x] 4.9 Test create policy dialog opens via URL pre-fill (AC: 4) — **write operation happy path**
  - [x] 4.10 Test create policy dialog shows error toast when API returns `AdminOperationResult { Success = false }` (AC: 4) — **write operation error path**
  - [x] 4.11 Test delete confirmation calls `DeleteSnapshotPolicyAsync` on confirm, reloads list, and shows success toast (AC: 6) — **delete round-trip**
  - **Recommended tests**:
  - [x] 4.12 Test URL parameters read on page initialization (AC: 8)
  - [x] 4.13 Test create policy dialog renders with correct form fields (AC: 4)
  - [x] 4.14 Test edit policy dialog pre-fills values and makes interval editable (AC: 5)
  - [x] 4.15 Test delete confirmation dialog shows policy details (AC: 6)
  - [x] 4.16 Test "Create Snapshot" button hidden for ReadOnly users (AC: 12)
  - [x] 4.17 Test policy list filters by tenant when tenant filter is applied (AC: 3)
  - [x] 4.18 Test stat card "Tenants Covered" shows distinct count (AC: 2)
  - [x] 4.19 Test stat card "Avg Interval" shows "N/A" when no policies exist (AC: 2)

- [x] **Task 5: Breadcrumb, NavMenu, Command Palette, and CSS** (AC: 9, 10, 13)
  - [x] 5.1 Update `Breadcrumb.razor` route label dictionary: add `"snapshots" -> "Snapshots"`.
  - [x] 5.2 Update `NavMenu.razor`: add `<FluentNavLink Href="/snapshots" Icon="@(new Icons.Regular.Size20.Camera())">Snapshots</FluentNavLink>` after the Storage link.
  - [x] 5.3 Update `CommandPaletteCatalog.cs`: add entries `("Actions", "Snapshots", "/snapshots")` and `("Snapshots", "Snapshot Policies", "/snapshots")`.
  - [x] 5.4 Add CSS styles in `wwwroot/css/app.css`: `.snapshot-readonly-field` (muted color for read-only display in edit dialog).
  - [x] 5.5 Create `src/Hexalith.EventStore.Admin.UI/Components/Shared/TimeFormatHelper.cs` as a `public static` method. `Snapshots.razor` uses this shared helper. Note: Storage.razor does not have a local FormatRelativeTime — it was in Projections.razor and ProjectionDetailPanel.razor. The shared helper is new for Snapshots.razor.
  - [x] 5.6 Add "Manage Snapshot Policies" link to the Storage page's snapshot risk banner.
  - [x] 5.7 **Checkpoint**: Snapshots page accessible from sidebar, breadcrumb shows "Home / Snapshots", Ctrl+K "Snapshots" navigates correctly, dialogs styled consistently, Storage page links to Snapshots, TimeFormatHelper shared helper works.

## Dev Notes

### Architecture Compliance

- **ADR-P4**: Admin.UI communicates exclusively via HTTP REST API to Admin.Server. The `AdminSnapshotApiClient` calls `api/v1/admin/storage/*` endpoints — no direct DAPR access. Write operations (create snapshot, set/delete policy) are delegated through Admin.Server which itself delegates to CommandApi via DAPR service invocation.
- **NFR41**: Initial render ≤ 2 seconds. Policy list is typically small (< 100 entries), so no pagination needed for v1.
- **NFR46**: Operator role required for all write operations. ReadOnly users can view policies but cannot create/edit/delete.
- **FR76**: This story covers "snapshot creation" and "snapshot policies" portions of FR76. Storage growth analysis is story 16-1 (done). Compaction trigger is story 16-3.

### Scope — Snapshot Policy CRUD + Manual Snapshot Creation

This story includes:
- Viewing snapshot policies (all users)
- Creating/editing/deleting auto-snapshot policies (Operator+)
- Manually triggering snapshot creation for specific aggregates (Operator+)

This story does NOT include:
- Snapshot validation/integrity checking (future story or 16-7 consistency checker)
- Viewing individual snapshot contents or state diffs (covered by story 15-4 Aggregate State Inspector)
- Compaction (story 16-3)
- Capacity projection / "days until full" trend lines

### Delete Endpoint — Must Be Added to Server (Task 2)

The `AdminStorageController` does NOT currently have a delete endpoint for snapshot policies. Task 2 adds three things:

**1. Interface** — add to `IStorageCommandService`:
```csharp
Task<AdminOperationResult> DeleteSnapshotPolicyAsync(
    string tenantId, string domain, string aggregateType, CancellationToken ct = default);
```

**2. Implementation** — add to the DAPR-backed `StorageCommandService` (or equivalent implementation class in `Admin.Server/`). Follow the same pattern as `SetSnapshotPolicyAsync` — delete the snapshot policy key from DAPR state store. Return `AdminOperationResult { Success = true }` on success, or `ErrorCode = "NotFound"` if the policy doesn't exist.

**3. Controller action** — add to `AdminStorageController`:
```csharp
/// <summary>
/// Deletes the automatic snapshot policy for an aggregate type.
/// </summary>
[HttpDelete("{tenantId}/{domain}/{aggregateType}/snapshot-policy")]
[Authorize(Policy = AdminAuthorizationPolicies.Operator)]
[ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
[ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
public async Task<IActionResult> DeleteSnapshotPolicy(
    string tenantId,
    string domain,
    string aggregateType,
    CancellationToken ct = default)
```

Uses `MapOperationResult` for response — returns 200 with `AdminOperationResult` body, consistent with existing PUT/POST pattern. Do NOT use 204 No Content.

### Existing Code to Reuse (DO NOT Recreate)

| What | Where | How |
|------|-------|-----|
| `SnapshotPolicy` DTO | `Admin.Abstractions/Models/Storage/SnapshotPolicy.cs` | USE — TenantId, Domain, AggregateType, IntervalEvents, CreatedAtUtc |
| `AdminOperationResult` DTO | `Admin.Abstractions/Models/Common/AdminOperationResult.cs` | USE — Success, OperationId, Message, ErrorCode for write operation responses |
| `AdminRole` enum | `Admin.Abstractions/Models/Common/AdminRole.cs` | USE — ReadOnly, Operator, Admin for role gating |
| `IStorageQueryService` | `Admin.Abstractions/Services/IStorageQueryService.cs` | REFERENCE — `GetSnapshotPoliciesAsync` already defined |
| `IStorageCommandService` | `Admin.Abstractions/Services/IStorageCommandService.cs` | MODIFY — add `DeleteSnapshotPolicyAsync` |
| `AdminStorageController` | `Admin.Server/Controllers/AdminStorageController.cs` | MODIFY — add delete endpoint |
| `AdminStorageApiClient` | `Admin.UI/Services/AdminStorageApiClient.cs` | PATTERN MODEL — follow exact same constructor, HttpClient, error handling, `HandleErrorStatus` pattern |
| `Projections.razor` | `Admin.UI/Pages/Projections.razor` | PATTERN MODEL — page with operator-level action buttons gated by `AuthorizedView` |
| `ProjectionDetailPanel.razor` | `Admin.UI/Components/ProjectionDetailPanel.razor` | PATTERN MODEL — detail panel with async operations + toast notifications |
| `StatCard` component | `Admin.UI/Components/Shared/StatCard.razor` | USE — Label, Value, Severity, Title |
| `SkeletonCard` component | `Admin.UI/Components/Shared/SkeletonCard.razor` | USE — loading placeholder |
| `IssueBanner` component | `Admin.UI/Components/Shared/IssueBanner.razor` | USE — error/warning state |
| `EmptyState` component | `Admin.UI/Components/Shared/EmptyState.razor` | USE — no data fallback |
| `AuthorizedView` component | `Admin.UI/Components/Shared/AuthorizedView.razor` | USE — role-gated rendering with `MinimumRole` |
| `AdminUITestContext` | `Admin.UI.Tests/AdminUITestContext.cs` | USE — base test class |
| `Storage.razor` | `Admin.UI/Pages/Storage.razor` | REFERENCE — URL state, debounce timer, manual refresh, `FormatRelativeTime` patterns (from story 16-1) |
| `HandleErrorStatus` method | `AdminStorageApiClient.cs` | COPY — same HTTP status → typed exception mapping |

### AdminSnapshotApiClient — Exact API Endpoints

| Method | HTTP | URL | Params | Returns |
|--------|------|-----|--------|---------|
| GetSnapshotPoliciesAsync | GET | `api/v1/admin/storage/snapshot-policies` | `?tenantId=` | `IReadOnlyList<SnapshotPolicy>` |
| SetSnapshotPolicyAsync | PUT | `api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy` | `?intervalEvents=` | `AdminOperationResult` |
| CreateSnapshotAsync | POST | `api/v1/admin/storage/{tenantId}/{domain}/{aggregateId}/snapshot` | — | `AdminOperationResult` |
| DeleteSnapshotPolicyAsync | DELETE | `api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy` | — | `AdminOperationResult` |

All GET endpoints require `ReadOnly` auth policy. All PUT/POST/DELETE endpoints require `Operator` auth policy. All endpoints have `AdminTenantAuthorizationFilter`.

### Dialog Pattern — Match Existing Codebase

**IMPORTANT:** Check how `Projections.razor` and other existing pages implement their dialogs before coding. The codebase may use either:
- **Option A:** `IDialogService.ShowDialogAsync<TComponent>()` (service-based, injected)
- **Option B:** Direct `FluentDialog` component reference with `ShowAsync()`/`HideAsync()`

Match whichever pattern the existing pages use. Do NOT mix the two APIs.

**Regardless of dialog API, the callback logic is:**
```csharp
// Handle create/update confirm
private async Task OnCreatePolicyConfirm()
{
    _isOperating = true;
    StateHasChanged();
    try
    {
        AdminOperationResult? result = await SnapshotApi.SetSnapshotPolicyAsync(
            _createForm!.TenantId!, _createForm.Domain!,
            _createForm.AggregateType!, _createForm.IntervalEvents, _loadCts?.Token ?? default);

        if (result?.Success == true)
        {
            ToastService.ShowSuccess("Snapshot policy created.");
            // Close dialog using whichever API the codebase uses
            await LoadDataAsync();
        }
        else
        {
            ToastService.ShowError(result?.Message ?? "Failed to create policy.");
        }
    }
    catch (ServiceUnavailableException)
    {
        ToastService.ShowError("Admin service unavailable. Try again later.");
    }
    finally
    {
        _isOperating = false;
        StateHasChanged();
    }
}
```

### Upsert Semantics — SetSnapshotPolicyAsync (PUT)

`SetSnapshotPolicyAsync` uses HTTP PUT, which is idempotent. If a policy already exists for the same tenant/domain/aggregateType combination, calling PUT will **overwrite the interval** (upsert behavior). The create dialog effectively performs "create or update." For v1 this is acceptable — no duplicate detection or warning dialog needed. The dev agent should not add extra "policy already exists" validation.

### FormatRelativeTime Helper — Extract to Shared Helper

`Storage.razor` (story 16-1) already has this method. Do NOT duplicate it. Extract to a shared static helper class `src/Hexalith.EventStore.Admin.UI/Components/Shared/TimeFormatHelper.cs` and reference from both `Storage.razor` and `Snapshots.razor`. Pattern:
```csharp
private static string FormatRelativeTime(DateTimeOffset created)
{
    TimeSpan age = DateTimeOffset.UtcNow - created;
    return age.TotalMinutes < 1 ? "just now"
        : age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes}m ago"
        : age.TotalHours < 24 ? $"{(int)age.TotalHours}h ago"
        : age.TotalDays < 30 ? $"{(int)age.TotalDays}d ago"
        : created.ToString("yyyy-MM-dd");
}
```

### bUnit Test Pattern

Follow `StoragePageTests` pattern from story 16-1:

```csharp
public class SnapshotsPageTests : AdminUITestContext
{
    private readonly AdminSnapshotApiClient _mockSnapshotApi;

    public SnapshotsPageTests()
    {
        _mockSnapshotApi = Substitute.For<AdminSnapshotApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminSnapshotApiClient>.Instance);
        Services.AddScoped(_ => _mockSnapshotApi);
    }

    [Fact]
    public void SnapshotsPage_RendersStatCards_WithCorrectValues()
    {
        IReadOnlyList<SnapshotPolicy> policies =
        [
            new("tenant-a", "orders", "OrderAggregate", 100, DateTimeOffset.UtcNow.AddDays(-5)),
            new("tenant-b", "inventory", "StockAggregate", 200, DateTimeOffset.UtcNow.AddDays(-2)),
        ];
        _mockSnapshotApi.GetSnapshotPoliciesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(policies));

        IRenderedComponent<Snapshots> cut = Render<Snapshots>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Total Policies"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("2"); // Total Policies
    }
}
```

### Previous Story Intelligence (16-1 Storage Growth Analyzer)

Key learnings from story 16-1 that apply to 16-2:

- **bUnit SVG `<text>` tag conflicts**: Razor's `<text>` directive conflicts with SVG `<text>` — resolved by using `MarkupString`. Not directly relevant to 16-2 (no SVG) but good to know.
- **Culture-dependent formatting in tests**: Use `$"{value:F1}"` format strings rather than hardcoded expected values like "1.0" — may produce "1,0" in French locale.
- **FluentDataGrid `OnRowClick`**: Use `OnRowClick` attribute, not `RowClick`.
- **Timer + InvokeAsync**: The debounce Timer callback runs on a thread pool thread. All state mutations and `StateHasChanged()` MUST be wrapped in `InvokeAsync(() => { ... })`.
- **AdminStorageApiClient error pattern**: Throws `ServiceUnavailableException` on non-auth errors (not returning empty defaults for write operations). Read operations fall through to `ServiceUnavailableException` too — the page catches this and shows `IssueBanner`.
- **214 Admin.UI tests pass** after story 16-1 — no regressions allowed.
- **JSInterop in tests uses `Mode = Loose`** — no explicit JS setup needed.
- **FluentUI Blazor v4 icon syntax**: `@(new Icons.Regular.Size20.Camera())`.
- **`DashboardRefreshService`**: Do NOT subscribe on this page — it doesn't fetch snapshot data. Manual refresh only.

### Deferred Improvements (Out of Scope — Future Stories)

- **Create form autocomplete** — The create policy form uses freeform text fields for Tenant ID, Domain, and Aggregate Type. No validation against existing entities. Maria could create a policy for a misspelled tenant and it would silently never trigger. Consider adding autocomplete dropdowns querying known tenants/domains/aggregate types in a future iteration.
- **"Avg Interval" → "Uncovered Tenants"** — The "Avg Interval" stat card is an operational summary but not highly actionable. Maria's real question is "which tenants have NO policies at all?" Consider replacing with "Uncovered Tenants" (tenants with streams but no snapshot policies) for more actionable DBA insight.
- **Policy effectiveness metrics** — After creating a policy, Maria has no feedback on whether it's working. A "last triggered" timestamp or "snapshots created" count per policy would close the feedback loop. Requires server-side tracking.

### Git Intelligence

Branch naming: `feat/story-16-2-snapshot-management`. PR workflow: feature branch → PR → merge to main. Conventional commits: `feat:` prefix.

### Project Structure Notes

Files to create:
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/Services/AdminSnapshotApiClient.cs` — HTTP client for snapshot endpoints
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` — Snapshot management page
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/Components/Shared/TimeFormatHelper.cs` — Shared `FormatRelativeTime` static helper (extracted from Storage.razor, used by both pages)
- **CREATE**: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/SnapshotsPageTests.cs` — bUnit tests

Files to modify:
- **MODIFY**: `src/Hexalith.EventStore.Admin.Abstractions/Services/IStorageCommandService.cs` — add `DeleteSnapshotPolicyAsync`
- **MODIFY**: `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs` — add delete endpoint
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Program.cs` — register `AdminSnapshotApiClient`
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` — add Snapshots nav link
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` — add "snapshots" route label
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` — add Snapshots entries
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor` — extract FormatRelativeTime to shared helper, add "Manage Policies" link to snapshot risk banner
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` — snapshot dialog styles

### References

- [Source: prd.md — Journey 8: Maria, the DBA — snapshot policy configuration workflow]
- [Source: prd.md — FR76: Admin tool can manage storage — snapshot creation]
- [Source: prd.md — NFR46: Admin API role-based access control — operator for snapshot/compaction]
- [Source: architecture.md — ADR-P4: Three-interface architecture, Admin.UI via HTTP to Admin.Server]
- [Source: architecture.md — NFR41: Admin Web UI render ≤ 2 seconds initial load]
- [Source: architecture.md — Snapshot configuration mandatory, default 100 events]
- [Source: ux-design-specification.md — UX-DR41: Command palette for global search]
- [Source: ux-design-specification.md — UX-DR42: Deep linking for every view]
- [Source: ux-design-specification.md — Confirmation dialogs for batch/destructive operations]
- [Source: ux-design-specification.md — Form patterns: labels above fields, required asterisk, save disabled until dirty]
- [Source: Admin.Abstractions/Models/Storage/SnapshotPolicy.cs — DTO record definition]
- [Source: Admin.Abstractions/Services/IStorageQueryService.cs — GetSnapshotPoliciesAsync already defined]
- [Source: Admin.Abstractions/Services/IStorageCommandService.cs — CreateSnapshotAsync, SetSnapshotPolicyAsync already defined]
- [Source: Admin.Server/Controllers/AdminStorageController.cs — existing snapshot endpoints (GET, PUT, POST)]
- [Source: Admin.UI/Services/AdminStorageApiClient.cs — HTTP client pattern to follow exactly]
- [Source: story 16-1 — Previous story learnings: Timer+InvokeAsync, error patterns, test patterns]
- [Source: sprint-change-proposal-2026-03-21-admin-tooling.md — Epic 16 story breakdown]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

- Initial build: BL0005 error resolved by wrapping Delete button in `<div @onclick:stopPropagation>` instead of mixing `@onclick` and `@onclick:stopPropagation` on FluentButton.
- bUnit test BL0005 errors for FluentTextField.Value: Restructured create policy tests to use URL pre-fill (`?create=true&tenant=X&domain=Y&aggregateType=Z`) instead of programmatic field value setting.
- Storage.razor did not have local FormatRelativeTime (it was in Projections.razor); created TimeFormatHelper as new shared helper for Snapshots.razor.

### Completion Notes List

- Created `AdminSnapshotApiClient` with 4 methods (GetSnapshotPoliciesAsync, SetSnapshotPolicyAsync, CreateSnapshotAsync, DeleteSnapshotPolicyAsync) following AdminStorageApiClient pattern exactly.
- Added `DeleteSnapshotPolicyAsync` to `IStorageCommandService` interface, `DaprStorageCommandService` implementation, and `AdminStorageController` with HttpDelete endpoint.
- Created Snapshots.razor page with full CRUD: stat cards (Total Policies, Tenants Covered, Avg Interval), tenant filter with 300ms debounce, FluentDataGrid with sortable columns, create/edit/delete/create-snapshot dialogs, role enforcement via AuthorizedView, URL state persistence with create dialog pre-fill.
- Created `TimeFormatHelper.cs` shared static helper for FormatRelativeTime.
- Updated Breadcrumb.razor, NavMenu.razor (Camera icon), CommandPaletteCatalog.cs, and app.css.
- Added "Manage Snapshot Policies" cross-link to Storage page's snapshot risk banner.
- 19 bUnit tests (11 merge-blocking + 8 recommended) — all pass.
- 234/234 Admin.UI tests pass (0 regressions from 215 pre-existing).
- 224 Admin.Abstractions tests pass, 185 Admin.Server tests pass — 0 regressions.
- Build succeeds with 0 warnings, 0 errors.
- Post-review fixes applied: expanded error handling paths in `Snapshots.razor`, ensured refresh failures still show `IssueBanner`, enforced tenant/domain default ordering in filtered grid source, and strengthened create-policy happy-path test to assert successful submission and dialog close behavior.

### File List

- `src/Hexalith.EventStore.Admin.UI/Services/AdminSnapshotApiClient.cs` (NEW)
- `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` (NEW)
- `src/Hexalith.EventStore.Admin.UI/Components/Shared/TimeFormatHelper.cs` (NEW)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/SnapshotsPageTests.cs` (NEW)
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IStorageCommandService.cs` (MODIFIED — added DeleteSnapshotPolicyAsync)
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageCommandService.cs` (MODIFIED — added DeleteSnapshotPolicyAsync)
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs` (MODIFIED — added DeleteSnapshotPolicy endpoint)
- `src/Hexalith.EventStore.Admin.UI/Program.cs` (MODIFIED — registered AdminSnapshotApiClient)
- `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` (MODIFIED — added Snapshots link)
- `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` (MODIFIED — added "snapshots" route label)
- `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` (MODIFIED — added Snapshots entries)
- `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` (MODIFIED — added snapshot-readonly-field style)
- `src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor` (MODIFIED — added "Manage Snapshot Policies" link to risk banner)

### Change Log

- 2026-03-24: Story 16-2 implemented — Snapshot Management and Auto-Snapshot Policies with full CRUD, 19 bUnit tests, 0 regressions across 643 total tests.
- 2026-03-24: Post-review remediation complete — all review findings addressed and full Admin.UI test suite remains green (234/234).
