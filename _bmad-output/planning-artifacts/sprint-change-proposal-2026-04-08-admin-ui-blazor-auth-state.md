# Sprint Change Proposal — Admin UI Blazor Authentication State

**Date:** 2026-04-08
**Trigger:** "Create Tenant" button and all AuthorizedView-gated admin actions invisible
**Scope:** Minor — 3 files (1 new, 2 modified)
**Status:** Approved and implemented

## Section 1: Issue Summary

All `<AuthorizedView MinimumRole="AdminRole.Admin">` components in the Admin UI render nothing. Two interlinked gaps:

1. **No Blazor authentication state:** The Admin UI is a Blazor Server app (WebSocket). The default `ServerAuthenticationStateProvider` reads from HTTP context which has no JWT. `AdminApiAccessTokenProvider` (Story 15-1) acquires Keycloak tokens server-side for API calls but never feeds them into the Blazor auth state.

2. **Missing global_admin fallback:** `AdminUserContext` (Story 15-1) only checks `eventstore:admin-role` claim. Keycloak tokens have `global_admin: true` instead — the `AdminClaimsTransformation` that converts between them only runs on Admin Server, not Admin UI.

**Impact:** All admin-gated buttons hidden across Tenants, Backups, Snapshots, Compaction, DeadLetters, Consistency pages.

## Section 2: Impact Analysis

- **Epic 15** (done): Story 15-1 design gap — AuthorizedView + AdminUserContext were specified but the bridge to Blazor auth state with Keycloak was not
- **Epic 16** (in-progress): Story 16-5 directly blocked (Create Tenant button)
- **No PRD/Architecture/UX conflicts** — additive fix bridging existing components

## Section 3: Recommended Approach

**Direct Adjustment** — Create `TokenAuthenticationStateProvider` to bridge `AdminApiAccessTokenProvider` to Blazor + add `global_admin` fallback to `AdminUserContext`.

## Section 4: Change Details

### New: `TokenAuthenticationStateProvider.cs`

**Path:** `src/Hexalith.EventStore.Admin.UI/Services/TokenAuthenticationStateProvider.cs`

Custom `AuthenticationStateProvider` that:
- Gets JWT from `AdminApiAccessTokenProvider` (already acquires Keycloak tokens)
- Decodes JWT payload (base64url) to extract all claims
- Builds `ClaimsPrincipal` with `ClaimsIdentity("JWT")`
- Returns as Blazor authentication state
- Logs authentication result at Information level for diagnostics

### Modified: `AdminUIServiceExtensions.cs`

**Path:** `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs`

Added DI registration after `AddCascadingAuthenticationState()`:
```csharp
builder.Services.AddScoped<AuthenticationStateProvider, TokenAuthenticationStateProvider>();
```

### Modified: `AdminUserContext.cs`

**Path:** `src/Hexalith.EventStore.Admin.UI/Services/AdminUserContext.cs`

Added `global_admin` / `is_global_admin` claim fallback when `eventstore:admin-role` is absent. Defense-in-depth: works with both dev tokens (which have `eventstore:admin-role`) and Keycloak tokens (which have `global_admin`).

## Section 5: Implementation Handoff

**Scope:** Minor — direct implementation
**Success criteria:** "Create Tenant" button visible on Tenants page when authenticated as admin-user via Keycloak
