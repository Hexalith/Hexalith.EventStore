# Sprint Change Proposal — Admin UI Post-Mutation Refresh & Error Feedback

**Date:** 2026-04-09
**Triggered by:** Hands-on usage of Admin UI Tenants page
**Scope Classification:** Minor
**Mode:** Incremental (approved proposal-by-proposal)

---

## Section 1: Issue Summary

Two related UX bugs were discovered during hands-on usage of the Admin Web UI Tenants page. While the issues were surfaced on the Tenants page, the root causes are systemic and affect all admin pages with mutation operations.

**Problem A — Post-mutation UI doesn't refresh:**
After performing actions like adding a user, removing a user, or changing a role in the tenant detail panel, the UI doesn't update to reflect the change. The user must manually click a dedicated Refresh button to see updated data. Root cause: `ReloadDetailPanel()` fetches fresh data but never calls `StateHasChanged()` to trigger Blazor re-render.

**Problem B — Silent failures on invalid operations:**
When performing impossible actions (e.g., creating a duplicate user, invalid state transitions), there is no error message shown to the user. The action silently fails. Root cause: all 15 API client classes throw exceptions using only the HTTP reason phrase ("Unprocessable Entity") instead of parsing the response body containing the actual business error message from `AdminOperationResult.Message`.

**Discovery context:** Both issues were found during routine testing of the Tenants admin page. The user requested these fixes be applied globally across all admin pages for a consistent experience.

---

## Section 2: Impact Analysis

### Epic Impact
- **Epic 15 (Admin Web UI — Core Developer Experience):** Affected but no scope change required. Fixes are implementation quality improvements within existing story boundaries.
- **No other epics affected.**

### Story Impact
- No new stories needed. Fixes are bug corrections within existing Epic 15 scope.

### Artifact Conflicts
- **PRD:** No conflict. PRD already requires proper error feedback (UX-DR1-DR11: RFC 7807 ProblemDetails with actionable messages).
- **Architecture:** No conflict. Architecture defines `AdminOperationResult(Success, OperationId, Message, ErrorCode)` — the fix is to actually surface the `Message` field in the UI.
- **UX Design:** No conflict. UX spec already specifies toast notifications for all outcomes (success, warning, error).

### Technical Impact
- Pure UI-layer changes. No backend, API, infrastructure, or deployment changes required.
- The server already returns structured error responses with meaningful messages — the client simply needs to read them.

---

## Section 3: Recommended Approach

**Selected path: Direct Adjustment**

Two targeted fixes applied consistently across all affected files:

1. Add `await InvokeAsync(StateHasChanged)` to `ReloadDetailPanel()` in Tenants.razor
2. Refactor `HandleErrorStatus()` to `HandleErrorStatusAsync()` across all 15 API clients, parsing the response body for 422 errors to extract business error messages

**Rationale:**
- Effort: **Low** — pattern fix repeated across known files
- Risk: **Low** — isolated UI changes, no backend modifications
- Timeline impact: **None** — can be implemented within current sprint
- The server-side already does the right thing; only the client-side needs fixing

---

## Section 4: Detailed Change Proposals

### Proposal 1/2: Fix `ReloadDetailPanel()` missing `StateHasChanged()` [APPROVED]

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor`
**Method:** `ReloadDetailPanel()` (lines 944-955)

**OLD:**
```csharp
private async Task ReloadDetailPanel()
{
    if (_expandedTenantId is null) { return; }
    try
    {
        _expandedUsers = await TenantApi.GetTenantUsersAsync(_expandedTenantId).ConfigureAwait(false);
    }
    catch (Exception)
    {
        // Ignore — keeps existing data
    }
}
```

**NEW:**
```csharp
private async Task ReloadDetailPanel()
{
    if (_expandedTenantId is null) { return; }
    try
    {
        _expandedUsers = await TenantApi.GetTenantUsersAsync(_expandedTenantId).ConfigureAwait(false);
    }
    catch (Exception)
    {
        // Ignore — keeps existing data
    }
    await InvokeAsync(StateHasChanged);
}
```

**Rationale:** `LoadDataAsync()` correctly calls `await InvokeAsync(StateHasChanged)` in its finally block. `ReloadDetailPanel()` must follow the same pattern to trigger Blazor re-render after updating `_expandedUsers`.

**Affected operations:** AddUser, RemoveUser, ChangeRole on Tenants detail panel.

---

### Proposal 2/2: Fix `HandleErrorStatus()` to parse response body for business errors [APPROVED]

**Scope:** All 15 API client classes in `src/Hexalith.EventStore.Admin.UI/Services/`

**Change:** Rename `HandleErrorStatus(HttpResponseMessage)` to `HandleErrorStatusAsync(HttpResponseMessage)` and add response body parsing for 422 status codes.

**NEW implementation (applied to all 15 clients):**
```csharp
private static async Task HandleErrorStatusAsync(HttpResponseMessage response)
{
    if (response.IsSuccessStatusCode)
    {
        return;
    }

    HttpStatusCode statusCode = response.StatusCode;
    string? reasonPhrase = response.ReasonPhrase;

    // Attempt to extract business error message from response body
    if (statusCode == HttpStatusCode.UnprocessableEntity)
    {
        try
        {
            AdminOperationResult? result = await response.Content
                .ReadFromJsonAsync<AdminOperationResult>()
                .ConfigureAwait(false);
            if (result?.Message is not null)
            {
                throw new InvalidOperationException(result.Message);
            }
        }
        catch (JsonException)
        {
            // Fall through to default message
        }
    }

    throw statusCode switch
    {
        HttpStatusCode.Unauthorized => new UnauthorizedAccessException(
            reasonPhrase ?? "Authentication required."),
        HttpStatusCode.Forbidden => new ForbiddenAccessException(
            reasonPhrase ?? "Insufficient permissions."),
        HttpStatusCode.UnprocessableEntity => new InvalidOperationException(
            reasonPhrase ?? "The operation was rejected by the server."),
        HttpStatusCode.ServiceUnavailable => new ServiceUnavailableException(
            reasonPhrase ?? "Service unavailable."),
        _ => new HttpRequestException($"Request failed: {statusCode} {reasonPhrase}"),
    };
}
```

**Affected API clients (15 files):**

| # | File | Current 422 handling |
|---|------|---------------------|
| 1 | AdminTenantApiClient.cs | Has 422 case (reason phrase only) |
| 2 | AdminActorApiClient.cs | No 422 case |
| 3 | AdminBackupApiClient.cs | No 422 case |
| 4 | AdminCompactionApiClient.cs | No 422 case |
| 5 | AdminConsistencyApiClient.cs | No 422 case |
| 6 | AdminDaprApiClient.cs | No 422 case |
| 7 | AdminDeadLetterApiClient.cs | No 422 case |
| 8 | AdminHealthHistoryApiClient.cs | No 422 case |
| 9 | AdminProjectionApiClient.cs | No 422 case |
| 10 | AdminPubSubApiClient.cs | No 422 case |
| 11 | AdminResiliencyApiClient.cs | No 422 case |
| 12 | AdminSnapshotApiClient.cs | No 422 case |
| 13 | AdminStorageApiClient.cs | No 422 case |
| 14 | AdminStreamApiClient.cs | No 422 case |
| 15 | AdminTypeCatalogApiClient.cs | No 422 case |

**Caller update required:** Each call site using `HandleErrorStatus(response)` must be updated to `await HandleErrorStatusAsync(response).ConfigureAwait(false)`.

**Rationale:** The server already returns structured `AdminOperationResult` with meaningful messages on 422. The UI already has `catch (Exception ex) { ToastService.ShowError(ex.Message); }` blocks — they just receive "Unprocessable Entity" instead of the actual business error.

---

## Section 5: Implementation Handoff

**Change scope: Minor** — Direct implementation by development team.

**Handoff:** Development team (Amelia / dev agent)

**Responsibilities:**
1. Apply Proposal 1 fix to `Tenants.razor` `ReloadDetailPanel()` method
2. Apply Proposal 2 fix across all 15 API client classes
3. Update all call sites from `HandleErrorStatus()` to `await HandleErrorStatusAsync()`
4. Verify toast messages display meaningful business errors on 422 responses
5. Verify Tenants detail panel auto-refreshes after user mutations

**Success criteria:**
- After creating a tenant, adding a user, removing a user, or changing a role, the UI updates immediately without clicking Refresh
- When performing an invalid operation (e.g., duplicate user), a toast notification displays the specific business error message
- All admin pages (Tenants, Backups, Compaction, Consistency, DeadLetters, Snapshots) show meaningful error messages on 422 responses
- No regressions in existing functionality

**Estimated effort:** Low (2-4 hours)
**Risk level:** Low (isolated UI changes, no backend modifications)

---

## Addendum: Additional Fixes Discovered During Implementation

### Fix 3/4: Catch-when filter swallowing `InvalidOperationException` [IMPLEMENTED]

During implementation, we discovered that all 63 catch-when blocks across 15 API clients were catching `InvalidOperationException` (thrown by `HandleErrorStatusAsync` for 422 errors) and re-wrapping it as a generic `ServiceUnavailableException("Unable to...")` — completely discarding the business error message.

**Fix:** Added `and not InvalidOperationException` to the catch-when exclusion filter across all 15 API clients (63 occurrences), allowing business errors to propagate to the UI catch blocks.

### Fix 4/4: `FluentToastProvider` missing interactive render mode [IMPLEMENTED]

The `<FluentToastProvider />` in `App.razor` was outside the `@rendermode="RenderMode.InteractiveServer"` scope. In .NET 10 Blazor with per-component render modes, the toast provider must be interactive to receive toast notifications from interactive components.

**Fix:** Added `@rendermode="RenderMode.InteractiveServer"` to the `<FluentToastProvider />` in `Components/App.razor`.
