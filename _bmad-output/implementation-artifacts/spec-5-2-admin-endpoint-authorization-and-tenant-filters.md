---
title: 'Story 5.2: Admin Endpoint Authorization And Tenant Filters'
type: 'feature'
created: '2026-09-06'
status: 'in-progress'
review_loop_iteration: 1
followup_review_recommended: false
baseline_revision: 'acf5c4e403699d4f9290fd6636e4d6b1872a3bd6'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-ux-instructions.md'
warnings: [oversized]
deferred: []
---

<intent-contract>

## Intent

**Problem:** The public Admin API has authorization metadata, but its ReadOnly policy accepts arbitrary role values, omitted tenant filters can widen non-admin reads to every tenant, opaque identifiers can disclose cross-tenant data, and JSON bodies are not consistently bounded before protected work.

**Approach:** Harden the `Hexalith.EventStore.Admin.Server` facade with exact role policies, fail-closed tenant narrowing, an explicit authorization/limit inventory, bounded request-body handling, and negative-path HTTP evidence that proves denial precedes service invocation.

## Boundaries & Constraints

**Always:** Preserve `/health`, `/alive`, and `/ready` reachability; accept only `ReadOnly`, `Operator`, or `Admin` for ReadOnly access; narrow omitted optional tenant filters to an authorized tenant and deny callers with no tenant; let global Admin callers retain cross-tenant access; use generic bounded Problem Details and redacted logs; clamp recent-command `count` before service invocation; apply 1 MiB to ordinary JSON bodies and 10 MiB to backup import; propagate cancellation and use `ConfigureAwait(false)` for production awaits.

**Never:** Treat OpenAPI metadata as enforcement, trust a caller-supplied role or tenant without policy validation, log authorized tenant lists or echo request bodies/resource identifiers in denials, perform protected service work after denial, change internal/admin-computation credential ownership from Story 5.5, implement deferred backup workflows owned by Story 7.4, change production authentication startup guards owned by Story 5.3, or modify `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Role gate | Anonymous, unknown role, or insufficient role | `401`/`403` before controller service work | Generic Problem Details; no protected identifiers |
| Tenant filter | Non-admin with matching, mismatching, omitted, or absent tenant claim | Match passes; mismatch denies; omission narrows to one claim; no claim denies | `403`, zero service calls, redacted correlation-only evidence |
| Global scope | Valid Admin with no tenant claim | May use unscoped and tenant-scoped Admin routes | Existing service result semantics remain |
| Opaque lookup | Non-admin requests check result/cancel or actor instance state | Reject before lookup because tenant cannot be proven from the public contract | `403` without existence disclosure |
| Recent commands | Count omitted, below 1, in range, or above 1000 | Service receives `1000`, `1`, unchanged value, or `1000` respectively | Never request an unbounded result set |
| Ordinary JSON | Body at 1 MiB or one byte larger | Exact limit reaches normal binding; oversized body is rejected | Bounded `413 application/problem+json`; no body echo or service call |
| Backup import | JSON body at 10 MiB or one byte larger, including JSON quotes | Exact limit reaches normal binding; oversized body is rejected | Same bounded `413` contract without partial work |

</intent-contract>

## Code Map

- `src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs` -- ReadOnly policy currently requires any role claim; constrain its accepted values and register shared limit handling.
- `src/Hexalith.EventStore.Admin.Server/Authorization/AdminAuthorizationMiddlewareResultHandler.cs` -- bounded 401/403 writer; replace the framework registration rather than relying on `TryAdd`, and normalize challenge output even when an authentication handler writes first.
- `src/Hexalith.EventStore.Admin.Server/Authorization/AdminTenantAuthorizationFilter.cs` -- public tenant gate; reuse the gateway filter's action-argument and narrow-or-deny pattern, but emit redacted diagnostics.
- `src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs` -- read-only reuse reference for omitted `tenantId` handling; do not widen Story 5.2 into the internal gateway.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` -- clamp recent-command count at the facade and mark sandbox JSON with the ordinary-body limit.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs` -- preserve the 10 MiB import exception and apply ordinary limits to export, admission, and crypto-shredding bodies.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminConsistencyController.cs` and `AdminDaprController.cs` -- deny unscoped body work and make opaque check/actor lookups Admin-only.
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`, `DaprActors.razor`, and their page tests -- hide Admin-only detail/cancel/actor-state interactions from lower roles and render the canonical denied state without misclassifying it as infrastructure failure.
- `src/Hexalith.EventStore.Admin.UI/Services/AdminConsistencyApiClient.cs` and `AdminActorApiClient.cs` -- preserve typed forbidden responses so pages can restore focus and render role-aware denial.
- `docs/brownfield/api-contracts.md` -- synchronize the policy matrix with Admin-only opaque lookups and record bodyless/unavailable request-limit ownership and reasons.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminProjectionsController.cs`, `AdminDeadLettersController.cs`, and `AdminTenantsController.cs` -- remaining ordinary JSON surfaces and existing tenant checks to harden without changing domain behavior.
- `src/Hexalith.EventStore.Admin.Server/Configuration/AdminRequestSizeLimits.cs`, `src/Hexalith.EventStore.Admin.Server.Host/Middleware/AdminRequestBodySizeMiddleware.cs`, and `src/Hexalith.EventStore.Admin.Server.Host/Program.cs` -- central limit values and safe `413` normalization after authorization and before action execution.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Authorization/AdminTenantAuthorizationFilterTests.cs`, `IntegrationTests/AdminAuthorizationIntegrationTests.cs`, and `Controllers/AdminStreamsControllerTests.cs` -- tenant, zero-work/non-disclosure, role, and count regressions.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Authorization/AdminActionSecurityMetadataTests.cs` and `tests/Hexalith.EventStore.Admin.Server.Host.Tests/AdminRequestBodySizeTests.cs` -- reflection/HTTP matrices for all Admin actions, body-limit metadata, exact-boundary behavior, and anonymous probe preservation.
- `tests/Hexalith.EventStore.Admin.Server.Tests/OpenApi/AdminOpenApiDocumentTests.cs` -- verify every body-owning operation's generated `413 application/problem+json` schema rather than attributes alone.

## Tasks & Acceptance

**Execution:**
- `src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs`, `src/Hexalith.EventStore.Admin.Server/Authorization/AdminAuthorizationMiddlewareResultHandler.cs`, and `tests/Hexalith.EventStore.Admin.Server.Tests/Configuration/ServiceCollectionExtensionsTests.cs` -- add `RequireAuthenticatedUser()` to every policy, restrict ReadOnly to the three defined roles, replace the framework result handler, and prove unauthenticated claims cannot authorize and the bounded handler is the resolved runtime service.
- `src/Hexalith.EventStore.Admin.Server/Authorization/AdminTenantAuthorizationFilter.cs`, `tests/Hexalith.EventStore.Admin.Server.Tests/Authorization/AdminTenantAuthorizationFilterTests.cs`, and `tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs` -- resolve route, query, and action `tenantId`; narrow omitted nullable tenant arguments; deny missing/mismatched scope; retain Admin bypass; redact logs; prove `next` and services are not called on denial.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminConsistencyController.cs`, `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDaprController.cs`, `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminConsistencyControllerTests.cs`, and `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDaprControllerActorTests.cs` -- deny no-claim body checks and require Admin for opaque check-result, check-cancel, and actor-state lookups so tenant existence is never inferred through a lookup.
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`, `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor`, `src/Hexalith.EventStore.Admin.UI/Services/AdminConsistencyApiClient.cs`, `src/Hexalith.EventStore.Admin.UI/Services/AdminActorApiClient.cs`, `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs`, `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprActorsPageTests.cs`, their two matching service-client test files, and `docs/brownfield/api-contracts.md` -- propagate the Admin-only opaque-lookup contract through UI visibility, forbidden-state handling, focus restoration, client guidance, and the public policy matrix.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` and `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsControllerTests.cs` -- clamp recent-command count to `1..1000` after the default is applied and before calling `IStreamQueryService`.
- `src/Hexalith.EventStore.Admin.Server/Configuration/AdminRequestSizeLimits.cs`, all six body-owning files under `src/Hexalith.EventStore.Admin.Server/Controllers/`, `src/Hexalith.EventStore.Admin.Server.Host/Middleware/AdminRequestBodySizeMiddleware.cs`, `src/Hexalith.EventStore.Admin.Server.Host/Program.cs`, and `tests/Hexalith.EventStore.Admin.Server.Host.Tests/AdminRequestBodySizeTests.cs` -- mark every JSON body as 1 MiB except 10 MiB import, expose `413` response metadata, normalize only proven size-limit overflow to static Problem Details, propagate unrelated I/O/cancellation failures, and prove exact/+1-byte boundaries for sandbox, a mutation, import, and unknown-length bodies with zero downstream work.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Authorization/AdminActionSecurityMetadataTests.cs`, `tests/Hexalith.EventStore.Admin.Server.Tests/OpenApi/AdminOpenApiDocumentTests.cs`, `tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs`, and `tests/Hexalith.EventStore.Admin.Server.Host.Tests/HostBootstrapTests.cs` -- enumerate effective built-host role/tenant/size metadata, record every no-body/unavailable N/A owner and reason, prove generated `413` content/schema, require non-null Problem Details media types, exercise real JWT 401/403 redaction and zero service resolution, and retain unauthenticated `200` only for `/health`, `/alive`, and `/ready`.

**Acceptance Criteria:**
- Given the complete public Admin action inventory, when authorization metadata is inspected and representative routes are invoked, then every action requires its intended exact role policy, none is anonymous, and only the three transport probes remain anonymously reachable.
- Given a non-admin request that is explicitly cross-tenant, omits a tenant on a tenant-filterable action, supplies a body tenant without a matching claim, or uses an opaque lookup, when authorization executes, then the request is narrowed or rejected before protected service work and the response/log cannot reveal tenant or resource existence.
- Given an Admin principal, when it invokes scoped or unscoped Admin routes, then global access remains available without fabricating tenant claims.
- Given the recent-commands query receives any supported boundary input, when the facade invokes the service, then omitted equals `1000`, non-positive equals `1`, `1..1000` is unchanged, and values above `1000` equal `1000`.
- Given an authenticated request reaches a JSON-body endpoint, when its encoded HTTP body is exactly at the applicable boundary or one byte beyond it, then the exact-size request proceeds normally and the next byte returns bounded `413 application/problem+json` with no payload echo, partial work, or service invocation.
- Given implementation is complete, when focused authorization/controller/host tests, both Admin test projects, and the Release solution build run, then all pass without warnings or regressions.

## Spec Change Log

- 2026-09-06 -- Review found that the first derivation registered a dead authorization-result handler, omitted explicit authenticated-user requirements, could convert unrelated request-body I/O failures to `413`, verified metadata instead of enough effective host/OpenAPI behavior, and changed opaque lookup roles without updating Admin UI and documentation. Expanded the Code Map, Tasks, Design Notes, and verification targets to require registration replacement, bounded challenge normalization, real-host JWT checks, mutation-size and generated-OpenAPI coverage, executable N/A ownership, and role-aware UI/client/docs propagation. Known-bad states avoided: claims-bearing unauthenticated access, empty or unbounded denial bodies, false `413` classification, hidden runtime metadata drift, and Operator/ReadOnly controls that always fail. KEEP: exact role values; fail-closed tenant narrowing and Admin bypass; correlation-only tenant-denial logs; recent-command default/clamp; 1 MiB ordinary and 10 MiB import limits; exact/+1 and unknown-length tests; opaque lookups remain Admin-only; health probes remain reachable; `sprint-status.yaml` remains untouched.

## Review Triage Log

### 2026-09-06 — Review pass
- verdicts: 30 findings — high 4, medium 17, low 1, false 8, maybe-false 0
- findings:
  - `[high]` `[bad_spec]` Blind hunter: custom authorization-result registration is a no-op — .NET 10 `AddAuthorizationBuilder()` calls `AddAuthorization()`, which registers the framework handler via `TryAddTransient`; the later `TryAddSingleton` cannot replace it, so the spec now requires replacement and a resolved-service test.
  - `[high]` `[bad_spec]` Blind hunter: matching role claims authorize an unauthenticated identity — all three policies lacked `RequireAuthenticatedUser()`, so the spec now requires it and an unauthenticated-claims regression.
  - `[medium]` `[defer]` Blind hunter: OpenAPI/Swagger remains anonymously mapped by default — this predates the change and Story 5.4 explicitly owns the environment-safe Swagger gate; no Story 5.2 patch is applied.
  - `[false]` `[reject]` Blind hunter: route tenant filtering after model binding violates denial ordering — Story 5.2 requires denial before application/query/state work, not before schema binding, and body-tenant authorization necessarily consumes a bound body; no protected service work occurs.
  - `[false]` `[reject]` Blind hunter: body-size handling before the tenant action filter must return `403` instead of `413` — the intent establishes both bounded input and tenant denial before services but no precedence between those safe failures; buffering remains bounded and invokes no service.
  - `[false]` `[reject]` Blind hunter: bodyless or future endpoints bypass the default size cap — bodyless actions do not read or deserialize bodies, while the executable `[FromBody]` inventory fails on every newly inferred or omitted body action.
  - `[medium]` `[bad_spec]` Blind hunter: every request-body `IOException` becomes `413` — connection, temporary-file, and transport failures are not proven limit overflows; the spec now requires unrelated I/O and cancellation to propagate to their proper path.
  - `[low]` `[reject]` Blind hunter: omitted scope selects the first of multiple tenant claims — this is the pre-existing `FindFirst` client contract, returns only an authorized tenant, and changing it adds selection semantics without a demonstrated security failure.
  - `[medium]` `[bad_spec]` Blind hunter: ReadOnly/Operator consistency rows remain expandable after detail became Admin-only — the page calls the now-forbidden route without a denied-state handler; UI visibility and handling were added to the spec.
  - `[medium]` `[bad_spec]` Blind hunter: Operators still see consistency cancellation after it became Admin-only — the control is guaranteed to fail and its handler omits `ForbiddenAccessException`; UI role propagation was added to the spec.
  - `[medium]` `[bad_spec]` Blind hunter: DAPR actor-state lookup remains exposed to lower roles — deep links and the Inspect control call an Admin-only endpoint and mislabel `403` as infrastructure failure; Admin gating and canonical denial were added.
  - `[medium]` `[bad_spec]` Blind hunter: public role documentation remains stale — `docs/brownfield/api-contracts.md` still advertises Operator consistency and ReadOnly actor-state access; policy-matrix synchronization was added.
  - `[medium]` `[bad_spec]` Blind hunter: reflection checks do not prove effective endpoint authorization — they ignore runtime-combined/global metadata and mapped endpoints; effective built-host inventory evidence was added.
  - `[medium]` `[bad_spec]` Blind hunter: authorization Problem Details lack real-JWT host verification — custom-test authentication cannot prove the production result handler, content type, bounded body, or zero resolution; real-host assertions were added.
  - `[false]` `[reject]` Blind hunter: tenant-ordering size tests are mandatory — the intent requires bounded no-work outcomes but does not define `403` precedence over `413`, so this proposed expectation is not authoritative.
  - `[high]` `[bad_spec]` Edge hunter: `TryAddSingleton` cannot replace the framework authorization-result handler — verified from .NET 10 decompiled/PDB source; grouped with the blind registration defect and added replacement evidence.
  - `[medium]` `[bad_spec]` Edge hunter: an authentication handler that starts a challenge response can bypass normalization — the public hosting extension permits configured schemes, so bounded challenge capture/normalization was added to the design.
  - `[false]` `[reject]` Edge hunter: `long.MaxValue` endpoint metadata overflows the middleware increment — no current Admin endpoint advertises that value, so the alleged trigger is unreachable in the reviewed program.
  - `[medium]` `[patch]` Edge hunter: authorization content-type assertions use null-conditional access — a missing header skips the assertion; the replacement derivation must use a non-null assertion.
  - `[high]` `[bad_spec]` Edge hunter: policies omit authenticated-user requirements — verified and grouped with the blind policy defect.
  - `[medium]` `[patch]` Edge hunter: body-limit content-type assertions use null-conditional access — the tests can pass with no media type; the replacement derivation must assert non-null before equality.
  - `[medium]` `[defer]` Edge hunter: OpenAPI/Swagger is an anonymous non-probe surface — verified as pre-existing and owned by Story 5.4's explicit Swagger gate.
  - `[medium]` `[patch]` Verification-gap reviewer: hosted wrong-tenant denial does not assert route redaction — the unit test uses an empty path, so a future `Instance` regression would pass; add a serialized hosted-body assertion.
  - `[medium]` `[patch]` Verification-gap reviewer: generated OpenAPI does not prove the new `413` media type/schema — reflection and runtime tests are independent of generated-client metadata; add a generated-document matrix.
  - `[false]` `[reject]` Intent auditor: full matrix expectations were narrowed to representative execution — the story itself requires representative read/write/sandbox/import runtime evidence, paired with a complete executable metadata inventory, which the approach preserves.
  - `[medium]` `[bad_spec]` Intent auditor: reflection does not cover generated OpenAPI or effective runtime composition — verified and grouped with the metadata and OpenAPI gaps; both surfaces were added to the spec.
  - `[false]` `[reject]` Intent auditor: unchanged probe tests are missing from the diff — the existing `HostBootstrapTests` probe cases ran in the 26-test host lane and passed; unchanged coverage remains valid evidence.
  - `[false]` `[reject]` Intent auditor: tenant behavior requires exhaustive per-route HTTP tests — the authoritative criterion asks representative interaction evidence plus the complete matrix, not redundant execution of every equivalent route binding.
  - `[medium]` `[bad_spec]` Intent auditor: real-host size evidence omits an ordinary mutation — Story 5.2 explicitly requires representative mutation and sandbox integration tests; the spec now adds a mutation exact/+1 case.
  - `[medium]` `[bad_spec]` Intent auditor: UI denial behavior and no-body/unavailable ownership are absent — both are explicit Story 5.2 planning requirements; UI/client/docs and an executable N/A owner/reason matrix were added.

## Design Notes

Tenant scope must be established without reading the protected resource. Nullable `tenantId` action arguments provide the safe narrowing seam: non-admin callers receive one authorized claim value, while callers with no usable claim are denied. Routes whose only key is an opaque `checkId` or actor ID cannot establish that proof, so their existing route shape remains but their policy becomes Admin-only.

Request limits are encoded-body limits, not deserialized-object estimates. Enforcement must use endpoint metadata for documentation and server behavior, run after authentication/authorization, and normalize both known `Content-Length` rejection and body-reader overflow into the same safe Problem Details contract.

`AddAuthorizationBuilder()` already installs the framework `IAuthorizationMiddlewareResultHandler`; the Admin handler must explicitly replace it. Challenge/forbid side effects such as `WWW-Authenticate` remain, but any scheme-written body is captured and replaced with the bounded identifier-free Admin Problem Details before bytes escape. Effective endpoint tests resolve the built host rather than treating method attributes as runtime truth.

Opaque check and actor identifiers remain Admin-only because their current route contracts cannot prove tenant scope before lookup. This security change must propagate as one contract: lower-role UI controls and deep-link work are hidden or show the canonical denied state, focus returns to the initiating control, API clients preserve typed forbidden outcomes, and public policy documentation names the same minimum role.

Request-body overflow is the only condition mapped to `413`; generic `IOException` and cancellation are not overflow evidence. The matrix must explicitly classify bodyless and currently unavailable operations with their owner/reason and test a retained ordinary mutation in addition to sandbox and import.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj --configuration Release` -- all Admin authorization/controller tests pass.
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Host.Tests/Hexalith.EventStore.Admin.Server.Host.Tests.csproj --configuration Release` -- real host authorization, probe, and request-limit tests pass.
- `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release` -- role-aware Admin UI/client regressions pass.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- solution builds with zero warnings/errors.
- `git diff --check` -- no whitespace errors.
