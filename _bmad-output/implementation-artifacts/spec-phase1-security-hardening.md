---
title: 'Phase 1 Security Hardening'
type: 'bugfix'
created: '2026-07-04'
status: 'done'
baseline_commit: 'b7823e4f05c16a263d2066e00f12eac1a45d4d2e'
context:
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-04.md'
---

<frozen-after-approval reason="human-owned intent - do not modify unless human renegotiates">

## Intent

**Problem:** Phase 0 removed anonymous access from direct gateway admin computation endpoints, but authenticated tenant-scoped callers can still request another tenant's stream/trace/command data if they bypass the admin facade. Admin.Server.Host also still permits symmetric-key JWT auth outside Development, unlike the gateway guard added in Phase 0.

**Approach:** Apply the same tenant-claim enforcement to gateway admin read endpoints that the admin facade already applies, including narrowing optional-tenant list requests for non-global callers. Mirror the gateway production symmetric-key guard in Admin.Server.Host with an explicit break-glass option.

## Boundaries & Constraints

**Always:** Preserve global-admin access across tenants; preserve tenant-scoped access for matching `eventstore:tenant` claims; keep unauthenticated requests handled by existing `[Authorize]`; use `ConfigureAwait(false)` on awaits; keep changes out of submodules.

**Ask First:** Broadening CP-8 to DAPR app-api-token/mTLS endpoint hardening, changing JWT claim names, changing Admin.UI tenant-selection behavior, or making a breaking public API/package contract change.

**Never:** Reintroduce anonymous admin reads; trust a requested `tenantId` without comparing it to claims; allow non-Development symmetric-key mode by default.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Matching tenant read | Authenticated user has `eventstore:tenant=tenant-a`, route/query asks `tenant-a` | Request reaches controller action | N/A |
| Cross-tenant read | Authenticated user has `eventstore:tenant=tenant-a`, route/query asks `tenant-b` | Request stops before action | 403 ProblemDetails with correlationId |
| Unfiltered commands | Non-global user omits `tenantId` on direct gateway command activity query | Query is narrowed to caller's first tenant claim | If no tenant claim, 403 |
| Global admin read | Caller has recognized global admin claim/role | Requested tenant or unfiltered request is allowed | N/A |
| Admin host production symmetric key | Environment is not Development, `SigningKey` set, no `Authority`, no override | Startup options validation fails | Validation message names OIDC `Authority` and override |
| Break-glass symmetric key | Same as above with `AllowInsecureSymmetricKey=true` | Startup options validation succeeds | N/A |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs` -- new gateway-side action filter for route/query tenant enforcement and optional-list narrowing.
- `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` -- DI registration for the gateway filter.
- `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` -- direct stream computation endpoints that must enforce route tenant claims.
- `src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs` -- direct trace computation endpoint that must enforce route tenant claims.
- `src/Hexalith.EventStore/Controllers/AdminCommandsQueryController.cs` -- direct command activity endpoint that must narrow unfiltered non-global reads.
- `src/Hexalith.EventStore.Admin.Server.Host/Authentication/AdminServerAuthenticationOptions.cs` -- Admin host auth options and validator.
- `tests/Hexalith.EventStore.Admin.Server.Host.Tests/HostBootstrapTests.cs` -- host validator regression tests.
- `tests/Hexalith.EventStore.Server.Tests/Authorization/AdminTenantAuthorizationFilterTests.cs` -- gateway tenant filter regression tests.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs` -- add a gateway filter that permits global admins, checks `eventstore:tenant`, returns 403 ProblemDetails, and can rewrite nullable `tenantId` action arguments for non-global list endpoints.
- [x] `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` and gateway admin controllers -- register/apply the filter to direct admin computation controllers.
- [x] `src/Hexalith.EventStore.Admin.Server.Host/Authentication/AdminServerAuthenticationOptions.cs` -- add `AllowInsecureSymmetricKey` and environment-aware validation matching the gateway.
- [x] Tests -- cover tenant match/denial/narrowing/global bypass and Admin host symmetric-key production guard.

**Acceptance Criteria:**
- Given a tenant-scoped JWT for `tenant-a`, when a direct gateway admin stream or trace route requests `tenant-b`, then the request returns 403 before reading actor state.
- Given a tenant-scoped JWT for `tenant-a`, when direct gateway recent commands omits `tenantId`, then the controller receives `tenant-a` as the effective tenant filter.
- Given a global-admin JWT, when direct gateway admin endpoints request any tenant or omit `tenantId`, then the filter does not narrow or deny.
- Given Admin.Server.Host runs outside Development with only `SigningKey`, when options validate without override, then validation fails; with `AllowInsecureSymmetricKey=true`, it succeeds.

## Spec Change Log

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Host.Tests/` -- expected: all tests pass.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~AdminTenantAuthorizationFilterTests"` -- expected: targeted filter tests pass if the known Server.Tests CA2007 baseline allows the project to build.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- expected: 0 warnings, 0 errors.

## Suggested Review Order

**Gateway Tenant Enforcement**

- Start here: filter owns direct admin tenant authorization.
  [AdminTenantAuthorizationFilter.cs:18](../../src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs#L18)

- Missing tenant filters narrow list endpoints to the caller's first tenant.
  [AdminTenantAuthorizationFilter.cs:28](../../src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs#L28)

- Requested tenants are resolved from route, query, then action arguments.
  [AdminTenantAuthorizationFilter.cs:62](../../src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs#L62)

- Denials return support-safe ProblemDetails with correlation id.
  [AdminTenantAuthorizationFilter.cs:86](../../src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs#L86)

**Controller Wiring**

- Direct command activity queries now run through tenant scoping.
  [AdminCommandsQueryController.cs:17](../../src/Hexalith.EventStore/Controllers/AdminCommandsQueryController.cs#L17)

- Direct stream computation routes now run through tenant scoping.
  [AdminStreamQueryController.cs:37](../../src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs#L37)

- Direct trace routes now run through tenant scoping.
  [AdminTraceQueryController.cs:25](../../src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs#L25)

- Gateway DI registers the filter for ServiceFilter activation.
  [ServiceCollectionExtensions.cs:114](../../src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs#L114)

**Admin Host Auth Guard**

- Break-glass option defaults to false.
  [AdminServerAuthenticationOptions.cs:23](../../src/Hexalith.EventStore.Admin.Server.Host/Authentication/AdminServerAuthenticationOptions.cs#L23)

- Non-Development symmetric-key mode now fails without OIDC or override.
  [AdminServerAuthenticationOptions.cs:40](../../src/Hexalith.EventStore.Admin.Server.Host/Authentication/AdminServerAuthenticationOptions.cs#L40)

**Tests**

- Gateway tests pin matching, denial, narrowing, global bypass, and tenant-agnostic skip.
  [AdminTenantAuthorizationFilterTests.cs:17](../../tests/Hexalith.EventStore.Server.Tests/Authorization/AdminTenantAuthorizationFilterTests.cs#L17)

- Admin host tests pin Development, production failure, override, and OIDC success.
  [AdminServerAuthenticationOptionsTests.cs:10](../../tests/Hexalith.EventStore.Admin.Server.Host.Tests/AdminServerAuthenticationOptionsTests.cs#L10)
