# Sprint Change Proposal: Global Admin Tenant/RBAC Bypass

**Date:** 2026-04-08
**Triggered by:** Admin UI Tenants page returning "Access denied. Insufficient permissions to view tenants."
**Scope:** Minor — Direct implementation fix
**Status:** Implemented and verified

---

## 1. Issue Summary

The Admin UI Tenants page failed with HTTP 403 when accessed by a global administrator (`admin-user` with `global_admin: "true"` Keycloak claim). Root cause analysis revealed a gap in the EventStore's authorization pipeline:

- **`ClaimsTenantValidator`** checked `eventstore:tenant` claims for exact match against the requested tenant. The Tenant management domain uses tenant ID `"system"` (via `TenantIdentity.DefaultTenantId`), which is not in any user's tenant claims list.
- **`ClaimsRbacValidator`** checked `eventstore:domain` claims for exact match against domain `"tenants"`, which is not in the `admin-user`'s domain claims (`orders`, `counter`, `inventory`).
- **`DaprTenantQueryService`** treated HTTP 404 (no tenant projection data yet) as a service error rather than returning an empty list.

The six-layer auth model defined in the PRD (FR30-FR34) specifies `Admin from role=admin` as a permission dimension, but the validator implementations did not honor this — global administrators were subject to the same claim-matching rules as regular users.

**Evidence:**
- Direct API test: `POST /api/v1/queries` with Keycloak JWT returned `403 Forbidden: "Not authorized for tenant 'system'. Not authorized for domain 'tenants'."`
- JWT payload confirmed: `global_admin: "true"`, `eventstore:tenant: ["tenant-a", "tenant-b"]`, `eventstore:domain: ["orders", "counter", "inventory"]`

---

## 2. Impact Analysis

### Epic Impact
- **Epic 5 (Security & Multi-Tenant Isolation):** Acceptance criteria gap — global admin bypass was not implemented in `ClaimsTenantValidator` and `ClaimsRbacValidator`. The `AdminClaimsTransformation` on the Admin.Server side already handled global admin for admin-level policies, but the EventStore pipeline validators did not.
- **Epic 16 (Admin DBA Ops):** Unblocked — Story 16-5 (Tenant Management) now works as designed.

### Story Impact
- **Story 16-5 (Tenant Management):** Was blocked; now functional.
- No future stories impacted — the fix is backward compatible.

### Artifact Conflicts
- **PRD:** No conflict. The PRD already defines admin as a permission dimension. Implementation now matches spec.
- **Architecture:** No conflict. ADR-P4 pattern preserved — Admin.Server still delegates through EventStore command/query pipeline.
- **Keycloak Realm (`hexalith-realm.json`):** Added `"system"` to admin-user's tenant attributes as defense-in-depth (not strictly required after code fix).

### Technical Impact
- 3 source files modified
- 0 new dependencies
- 67 existing authorization tests pass
- No API contract changes

---

## 3. Recommended Approach

**Selected Path:** Direct Adjustment (Option 1)

**Rationale:** The fix is minimal, targeted, and aligns the existing validator implementations with the PRD's authorization model. Global administrators (`global_admin: "true"`) should logically bypass tenant and domain restrictions — this is the standard RBAC pattern for super-admin roles. The change introduces no new architectural concepts; it simply completes an existing design intent.

- **Effort:** Low (3 files, < 20 lines changed)
- **Risk:** Low (67 existing tests pass, no behavioral change for non-admin users)
- **Timeline Impact:** None

---

## 4. Detailed Change Proposals

### Change 1: ClaimsTenantValidator — Global admin bypass

**File:** `src/Hexalith.EventStore/Authorization/ClaimsTenantValidator.cs`

**OLD:**
```csharp
public Task<TenantValidationResult> ValidateAsync(
    ClaimsPrincipal user, string tenantId, ...) {
    var tenantClaims = user.FindAll("eventstore:tenant")...;
    if (tenantClaims.Count == 0) return Denied("No tenant authorization claims found.");
    if (!tenantClaims.Any(t => t == tenantId)) return Denied($"Not authorized for tenant '{tenantId}'.");
    return Allowed;
}
```

**NEW:**
```csharp
public Task<TenantValidationResult> ValidateAsync(
    ClaimsPrincipal user, string tenantId, ...) {
    // Global administrators may access any tenant (including "system")
    if (IsGlobalAdministrator(user)) return Allowed;

    var tenantClaims = user.FindAll("eventstore:tenant")...;
    if (tenantClaims.Count == 0) return Denied("No tenant authorization claims found.");
    if (!tenantClaims.Any(t => t == tenantId)) return Denied($"Not authorized for tenant '{tenantId}'.");
    return Allowed;
}

private static bool IsGlobalAdministrator(ClaimsPrincipal user) {
    Claim? claim = user.FindFirst("global_admin") ?? user.FindFirst("is_global_admin");
    return claim is not null && bool.TryParse(claim.Value, out bool isAdmin) && isAdmin;
}
```

**Rationale:** Global admins with `global_admin: "true"` claim must access cross-tenant resources like `"system"` for tenant management operations.

---

### Change 2: ClaimsRbacValidator — Global admin bypass

**File:** `src/Hexalith.EventStore/Authorization/ClaimsRbacValidator.cs`

**OLD:**
```csharp
public Task<RbacValidationResult> ValidateAsync(
    ClaimsPrincipal user, string tenantId, string domain, ...) {
    var domainClaims = user.FindAll("eventstore:domain")...;
    if (domainClaims.Count > 0 && !domainClaims.Any(d => d == domain))
        return Denied($"Not authorized for domain '{domain}'.");
    // ... permission checks ...
}
```

**NEW:**
```csharp
public Task<RbacValidationResult> ValidateAsync(
    ClaimsPrincipal user, string tenantId, string domain, ...) {
    // Global administrators bypass domain and permission checks
    if (IsGlobalAdministrator(user)) return Allowed;

    var domainClaims = user.FindAll("eventstore:domain")...;
    if (domainClaims.Count > 0 && !domainClaims.Any(d => d == domain))
        return Denied($"Not authorized for domain '{domain}'.");
    // ... permission checks ...
}

private static bool IsGlobalAdministrator(ClaimsPrincipal user) {
    Claim? claim = user.FindFirst("global_admin") ?? user.FindFirst("is_global_admin");
    return claim is not null && bool.TryParse(claim.Value, out bool isAdmin) && isAdmin;
}
```

**Rationale:** Same principle — global admins must access all domains including `"tenants"` for admin operations.

---

### Change 3: DaprTenantQueryService — Handle empty state gracefully

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`

**OLD:**
```csharp
public async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(...) {
    do {
        SubmitQueryResponse response = await SubmitQueryAsync(...);
        // 404 throws HttpRequestException, bubbles up as service error
    } while (cursor is not null);
    return tenants;
}
```

**NEW:**
```csharp
public async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(...) {
    try {
        do {
            SubmitQueryResponse response = await SubmitQueryAsync(...);
        } while (cursor is not null);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
        // No tenant projection data yet — return empty list
        return tenants;
    }
    return tenants;
}
```

**Rationale:** When no tenants have been created, the EventStore returns 404 (no projection data). This is not a service error — it means "zero results."

---

### Change 4: Keycloak Realm — Defense in depth

**File:** `src/Hexalith.EventStore.AppHost/KeycloakRealms/hexalith-realm.json`

**OLD:**
```json
"tenants": ["tenant-a", "tenant-b"]
```

**NEW:**
```json
"tenants": ["tenant-a", "tenant-b", "system"]
```

**Rationale:** Even though the code fix makes this unnecessary, including `"system"` in the admin-user's tenants provides defense-in-depth.

---

## 5. Implementation Handoff

**Scope Classification:** Minor — Direct implementation by dev team.

**Status:** All changes are already implemented and verified:
- Build: Passes (0 warnings, 0 errors)
- Tests: 67 authorization tests pass
- E2E: Admin Server returns `HTTP 200 []` for tenant list with Keycloak JWT

**Remaining Tasks:**
- [ ] Commit changes with conventional commit message
- [ ] Add unit tests for `IsGlobalAdministrator` bypass in both validators
- [ ] Verify Admin UI Tenants page displays empty state correctly (no error banner)

**Success Criteria:**
- Admin UI Tenants page loads without errors for global admin users
- Non-admin users are still correctly restricted to their assigned tenants/domains
- All existing Tier 1 tests pass
