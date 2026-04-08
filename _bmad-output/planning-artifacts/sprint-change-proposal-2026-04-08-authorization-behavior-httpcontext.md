# Sprint Change Proposal — AuthorizationBehavior HttpContext Crash

**Date:** 2026-04-08
**Trigger:** TenantBootstrapHostedService crashes host — AuthorizationBehavior requires HttpContext for internal MediatR calls
**Scope:** Minor — 1 file, 1 change
**Status:** Approved and implemented

## Section 1: Issue Summary

`AuthorizationBehavior` (Story 5.2) unconditionally requires `HttpContext` at line 34, throwing `InvalidOperationException` when it is null. Any `SubmitCommand` sent via MediatR outside an HTTP request context (e.g. `TenantBootstrapHostedService.StartAsync`) crashes the host process.

## Section 2: Impact Analysis

- **Epic 5** (done): Story 5.2 bug fix. No status change needed.
- **No other epics affected.**
- Domain-level RBAC in aggregate Handle methods (six-layer defense, Architecture doc) still applies.

## Section 3: Recommended Approach

**Direct Adjustment** — Skip API-level authorization when `HttpContext` is null. Internal service calls are trusted (same process). Domain RBAC provides defense-in-depth.

## Section 4: Change Detail

**File:** `src/Hexalith.EventStore/Pipeline/AuthorizationBehavior.cs` (line 34)

```csharp
// OLD:
HttpContext httpContext = httpContextAccessor.HttpContext
    ?? throw new InvalidOperationException("HttpContext is not available in AuthorizationBehavior.");

// NEW:
if (httpContextAccessor.HttpContext is not { } httpContext)
{
    return await next().ConfigureAwait(false);
}
```

## Section 5: Implementation Handoff

**Scope:** Minor — direct implementation
**Success criteria:** TenantBootstrapHostedService can send SubmitCommand via MediatR without crashing the host
