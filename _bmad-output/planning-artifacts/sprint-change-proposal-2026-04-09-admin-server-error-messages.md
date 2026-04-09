# Sprint Change Proposal — Admin Server User-Friendly Error Messages

**Date:** 2026-04-09
**Triggered by:** Follow-up to sprint-change-proposal-2026-04-09-admin-ui-refresh-and-error-feedback.md
**Scope Classification:** Minor
**Follows:** UI-layer fixes now surface error messages via toast — but the messages themselves are generic

---

## Section 1: Issue Summary

When EventStore rejects a command (duplicate tenant, duplicate user, disabled tenant, etc.), it returns RFC 7807 ProblemDetails with a meaningful `detail` field (e.g., "Tenant 'my-tenant' already exists"). However, `DaprTenantCommandService.SubmitCommandAsync()` reads this response body, **logs it**, then discards it and returns a generic message:

```
"Command rejected (409). See server logs with correlation ID xxx."
```

The user sees this unhelpful message in the UI toast instead of the actual business error.

**Root cause:** `DaprTenantCommandService.cs` lines 249-253 — the `errorBody` variable contains the full ProblemDetails JSON but is only used for logging, never parsed.

---

## Section 2: Impact Analysis

- **Epic 15 (Admin Web UI):** Improves UX quality — no scope change
- **PRD:** Aligns with UX-DR1-DR11 (actionable error messages)
- **Architecture:** No conflict — uses existing ProblemDetails format
- **Other artifacts:** No impact — single file server-side change

---

## Section 3: Recommended Approach

**Direct Adjustment** — Parse the ProblemDetails `detail` field from the EventStore response body and use it as the `AdminOperationResult.Message`.

**Effort:** Very Low (single method change)
**Risk:** Very Low (fallback to current generic message if parsing fails)

---

## Section 4: Detailed Change Proposal

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs`
**Method:** `SubmitCommandAsync()` (lines ~227-253)

**OLD (lines 227-253):**
```csharp
string errorBody = await httpResponse.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

// ... logging ...

return new AdminOperationResult(
    false,
    correlationId,
    $"Command rejected ({(int)httpResponse.StatusCode}). See server logs with correlation ID {correlationId}.",
    ((int)httpResponse.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));
```

**NEW:**
```csharp
string errorBody = await httpResponse.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

// ... logging (unchanged) ...

// Extract rejection type from ProblemDetails and humanize it
string? userMessage = null;
try {
    using JsonDocument doc = JsonDocument.Parse(errorBody);
    if (doc.RootElement.TryGetProperty("type", out JsonElement typeEl)) {
        string? typeName = typeEl.GetString();
        if (!string.IsNullOrEmpty(typeName)) {
            int lastDot = typeName.LastIndexOf('.');
            string className = lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;
            className = className.Replace("Rejection", string.Empty, StringComparison.Ordinal);
            userMessage = Regex.Replace(className, "(?<=.)([A-Z])", " $1").Trim();
            if (userMessage.Length > 1) {
                userMessage = char.ToUpperInvariant(userMessage[0]) + userMessage[1..].ToLowerInvariant();
            }
        }
    }
} catch (JsonException) {
    // Fall through to default message
}

return new AdminOperationResult(
    false,
    correlationId,
    userMessage ?? $"Command rejected ({(int)httpResponse.StatusCode}). See server logs with correlation ID {correlationId}.",
    ((int)httpResponse.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));
```

**Approach:** Instead of using the raw `detail` field (which contains technical text like "Domain rejection: Hexalith.Tenants.Contracts.Events.Rejections.TenantAlreadyExistsRejection"), we extract the `type` field (the rejection class name) and humanize it by removing the `Rejection` suffix and inserting spaces before uppercase letters.

**Result:** Users see clean, humanized messages:

| Rejection Type | User Message |
|---|---|
| `TenantAlreadyExistsRejection` | **Tenant already exists** |
| `UserAlreadyInTenantRejection` | **User already in tenant** |
| `TenantDisabledRejection` | **Tenant disabled** |
| `InsufficientPermissionsRejection` | **Insufficient permissions** |
| `TenantNotFoundRejection` | **Tenant not found** |
| `UserNotInTenantRejection` | **User not in tenant** |
| `LastGlobalAdministratorRejection` | **Last global administrator** |
| `RoleEscalationRejection` | **Role escalation** |
| `ConfigurationLimitExceededRejection` | **Configuration limit exceeded** |

---

## Section 5: Implementation Handoff

**Change scope: Minor** — Direct implementation by development team.
**Estimated effort:** Very Low (30 minutes)
**Success criteria:** Toast messages show the actual business error from EventStore instead of generic "Command rejected (xxx)" messages.
