# Sprint Change Proposal: Admin UI Shows No Data

**Date:** 2026-03-29
**Triggered by:** Post-implementation integration testing of Admin Web UI (Epics 14-15)
**Scope Classification:** Minor — Direct implementation by dev team

---

## Section 1: Issue Summary

The administration UI does not display any data on any page. The Commands page shows 0 commands even after submitting many counter increment commands through the sample Blazor UI. The issue affects all admin data pages, not just commands.

**Root cause:** Multiple integration disconnects in the admin data pipeline:

1. **Claim type mismatch** between Admin UI (`eventstore:admin:role`) and Admin Server (`eventstore:admin-role`) — breaks dev-mode auth entirely
2. **Silent DAPR failure masking** — `DaprStreamQueryService` catches all exceptions from DAPR service invocation and returns empty results, making failures invisible to the UI
3. **Missing Keycloak global admin mapping** — `admin-user` gets `Operator` role instead of `Admin`, scoping visibility to a single tenant and preventing cross-tenant admin views
4. **Missing protocol mapper** — even after adding `global_admin` user attribute, Keycloak won't include it in tokens without a protocol mapper

Each layer works in isolation but the end-to-end flow fails silently.

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact | Detail |
|------|--------|--------|
| Epic 14 (Admin API Foundation) | Fix needed | DaprStreamQueryService error handling too permissive; claim type constant mismatch |
| Epic 15 (Admin Web UI) | Fix needed | AdminClaimTypes.Role constant uses wrong claim name |
| Epics 1-13, 16-20 | No impact | Unaffected |

### Story Impact

No existing stories need modification. A single remediation story covers all 4 fixes.

### Artifact Conflicts

| Artifact | Conflict | Action |
|----------|----------|--------|
| PRD | None | MVP goals unaffected |
| Architecture | None | DAPR service invocation pattern is correct |
| UX Design | Minor | Error states now visible instead of silent empty |
| Keycloak Realm | Fix needed | Add global_admin attribute + protocol mapper |

### Technical Impact

- **4 files modified** (AdminClaimTypes.cs, DaprStreamQueryService.cs, hexalith-realm.json)
- **No API contract changes** — same endpoints, same DTOs
- **No infrastructure changes** — same DAPR topology
- **Improved error visibility** — DAPR failures now surface as 503 in admin UI

---

## Section 3: Recommended Approach

**Selected path:** Direct Adjustment — 4 targeted code fixes within existing epic scope.

**Rationale:**
- All issues are integration bugs, not design flaws
- Fixes are small, isolated, and low-risk
- No architectural changes needed
- The DAPR service invocation pattern is correct; we just need to stop swallowing errors

**Effort estimate:** Low (single story, ~1 hour implementation)
**Risk level:** Low (no API changes, no schema changes, no new dependencies)
**Timeline impact:** None — remediation fits within current sprint

---

## Section 4: Detailed Change Proposals

### Fix 1: Align Admin Claim Type Constants

**File:** `src/Hexalith.EventStore.Admin.UI/Services/AdminClaimTypes.cs:12`

```
OLD:
    public const string Role = "eventstore:admin:role";

NEW:
    public const string Role = "eventstore:admin-role";
```

**Justification:** Must match server's `AdminClaimTypes.AdminRole` (`"eventstore:admin-role"`). Dev mode tokens are generated with the wrong claim type, causing auth failure when Keycloak is disabled.

---

### Fix 2: Stop Swallowing DAPR Service Invocation Errors

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`

**Apply to `GetRecentCommandsAsync` (lines 79-85) and all other methods with the same pattern:**

```
OLD:
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to get recent commands from EventStore.");
            return new PagedResult<CommandSummary>([], 0, null);
        }

NEW:
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get recent commands from EventStore via DAPR service invocation to '{AppId}'.", _options.EventStoreAppId);
            throw;
        }
```

**Justification:** Silently returning empty results makes failures invisible. The `AdminStreamsController` already has `IsServiceUnavailable` exception handling that produces proper 503 responses. The admin UI surfaces 503 as "Unable to load commands. The admin backend may be unavailable." — giving the user actionable feedback.

**Scope:** Apply to all `DaprStreamQueryService` methods that currently catch exceptions and return empty/default results. The controller layer handles exception-to-HTTP mapping.

---

### Fix 3: Add Global Admin Role to Keycloak admin-user

**File:** `src/Hexalith.EventStore.AppHost/KeycloakRealms/hexalith-realm.json`

**Section: admin-user attributes**

```
OLD:
      "attributes": {
        "tenants": ["tenant-a", "tenant-b"],
        "domains": ["orders", "inventory", "counter"],
        "permissions": ["command:submit", "command:replay", "command:query"]
      }

NEW:
      "attributes": {
        "tenants": ["tenant-a", "tenant-b"],
        "domains": ["orders", "inventory", "counter"],
        "permissions": ["command:submit", "command:replay", "command:query"],
        "global_admin": ["true"]
      }
```

**Justification:** `admin-user` should be a global administrator with cross-tenant visibility. `AdminClaimsTransformation` checks for `global_admin` claim to grant `Admin` role, which makes `ResolveTenantScope` return `null` (all tenants). Without this, admin-user gets `Operator` role and is scoped to their first tenant.

---

### Fix 4: Add global_admin Protocol Mapper to Keycloak Clients

**File:** `src/Hexalith.EventStore.AppHost/KeycloakRealms/hexalith-realm.json`

**Section: hexalith-eventstore client protocolMappers (add after permissions-mapper)**

```json
{
  "name": "global-admin-mapper",
  "protocol": "openid-connect",
  "protocolMapper": "oidc-usermodel-attribute-mapper",
  "consentRequired": false,
  "config": {
    "user.attribute": "global_admin",
    "claim.name": "global_admin",
    "jsonType.label": "String",
    "multivalued": "false",
    "id.token.claim": "true",
    "access.token.claim": "true",
    "userinfo.token.claim": "true"
  }
}
```

**Same mapper to be added to the `hexalith-frontshell` client.**

**Justification:** Without a protocol mapper, Keycloak does not include user attributes in JWT tokens regardless of the attribute being set on the user.

---

## Section 5: Implementation Handoff

**Scope:** Minor — direct implementation by dev team.

**Handoff:**
| Role | Responsibility |
|------|---------------|
| Developer | Implement all 4 fixes, run Tier 1 tests |
| Developer | Manual smoke test: run AppHost, increment counter, verify Commands page shows data |

**Success Criteria:**
1. Admin UI Commands page shows commands after counter increments
2. Admin UI shows "Unable to load commands" error (not 0 commands) when DAPR is unavailable
3. Dev mode auth (no Keycloak) works with corrected claim type
4. admin-user gets `Admin` role and sees all tenants

**New Story ID:** `15-10-admin-ui-data-pipeline-fixes`
