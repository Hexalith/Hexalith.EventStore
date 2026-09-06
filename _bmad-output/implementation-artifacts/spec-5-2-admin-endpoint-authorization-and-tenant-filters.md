---
title: 'Story 5.2: Admin Endpoint Authorization And Tenant Filters'
type: 'feature'
created: '2026-09-06'
status: 'in-progress'
review_loop_iteration: 2
followup_review_recommended: false
baseline_revision: 'acf5c4e403699d4f9290fd6636e4d6b1872a3bd6'
baseline_commit: '04ce5380cd1f6e6e90a5ca03a751f6d94832fc2c'
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
- `src/Hexalith.EventStore.Admin.Server/Authorization/AdminAuthorizationMiddlewareResultHandler.cs` -- bounded Admin 401/403 writer; replace the framework default without removing arbitrary host registrations, delegate non-Admin endpoints, suppress scheme bodies, and retain only explicitly safe challenge headers such as `WWW-Authenticate`.
- `src/Hexalith.EventStore.Admin.Server/Authorization/AdminTenantAuthorizationFilter.cs` -- public tenant gate; reuse the gateway filter's action-argument and narrow-or-deny pattern, but emit redacted diagnostics.
- `src/Hexalith.EventStore/Authorization/AdminTenantAuthorizationFilter.cs` -- read-only reuse reference for omitted `tenantId` handling; do not widen Story 5.2 into the internal gateway.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` -- clamp recent-command count at the facade and mark sandbox JSON with the ordinary-body limit.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs` -- preserve the 10 MiB import exception and apply ordinary limits to export, admission, and crypto-shredding bodies.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminConsistencyController.cs` and `AdminDaprController.cs` -- authorize an explicit body tenant against the complete current tenant-claim set, deny unscoped body work, and make opaque check/actor lookups Admin-only.
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`, `DaprActors.razor`, `Commands.razor`, `Events.razor`, shared tenant-filter components, `Services/AdminUserContext.cs`, and their tests -- keep UI role checks exact and current, clear protected state on demotion, hide Admin-only interactions, describe omitted tenant scope truthfully, render canonical denial, and restore focus to the exact initiating action.
- `src/Hexalith.EventStore.Admin.UI/Services/AdminConsistencyApiClient.cs` and `AdminActorApiClient.cs` -- preserve typed forbidden responses so pages can restore focus and render role-aware denial.
- `docs/brownfield/api-contracts.md` -- synchronize each operation's exact policy and record the complete bodyless/unavailable request-limit matrix with endpoint, body shape, owner, and reason, including bodyless PUT operations.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminProjectionsController.cs`, `AdminDeadLettersController.cs`, and `AdminTenantsController.cs` -- remaining ordinary JSON surfaces and existing tenant checks to harden without changing domain behavior.
- `src/Hexalith.EventStore.Admin.Server/Configuration/AdminRequestSizeLimits.cs`, `src/Hexalith.EventStore.Admin.Server.Host/Middleware/AdminRequestBodySizeMiddleware.cs`, and `src/Hexalith.EventStore.Admin.Server.Host/Program.cs` -- central limit values and safe `413` normalization after authorization and before action execution.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Authorization/AdminTenantAuthorizationFilterTests.cs`, `IntegrationTests/AdminAuthorizationIntegrationTests.cs`, and `Controllers/AdminStreamsControllerTests.cs` -- tenant, zero-work/non-disclosure, role, and count regressions.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Authorization/AdminActionSecurityMetadataTests.cs` and `tests/Hexalith.EventStore.Admin.Server.Host.Tests/AdminRequestBodySizeTests.cs` -- exhaustive explicit policy and tenant-filter maps that fail on unclassified actions, effective body binding/limit metadata, exact-boundary behavior, and anonymous probe preservation.
- `tests/Hexalith.EventStore.Admin.Server.Tests/OpenApi/AdminOpenApiDocumentTests.cs` -- verify every body-owning operation's generated `413 application/problem+json` schema resolves specifically to Problem Details rather than accepting any schema.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs`, `src/Hexalith.EventStore.Admin.Server/Authorization/AdminAuthorizationMiddlewareResultHandler.cs`, and `tests/Hexalith.EventStore.Admin.Server.Tests/Configuration/ServiceCollectionExtensionsTests.cs` -- add `RequireAuthenticatedUser()` to every policy, restrict ReadOnly to the three defined roles, replace the framework default without erasing host-provided registrations, scope normalization to Admin endpoints, retain only safe challenge headers, and prove unauthenticated claims cannot authorize, scheme bodies/unsafe headers cannot escape, non-Admin failures delegate, and the bounded handler is the resolved runtime service.
- [x] `src/Hexalith.EventStore.Admin.Server/Authorization/AdminTenantAuthorizationFilter.cs`, `tests/Hexalith.EventStore.Admin.Server.Tests/Authorization/AdminTenantAuthorizationFilterTests.cs`, and `tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs` -- resolve route, query, and action `tenantId`; narrow omitted nullable tenant arguments; deny missing/mismatched scope; retain Admin bypass; redact logs; prove `next` and services are not called on denial.
- [x] `src/Hexalith.EventStore.Admin.Server/Controllers/AdminConsistencyController.cs`, `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDaprController.cs`, `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminConsistencyControllerTests.cs`, and `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDaprControllerActorTests.cs` -- accept an explicit body tenant when it matches any usable tenant claim, narrow an omitted body tenant to one claim, deny no-claim body checks, verify correlation-only denial logs, and require Admin for opaque check-result, check-cancel, and actor-state lookups.
- [x] `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`, `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor`, tenant-filter UI surfaces, `src/Hexalith.EventStore.Admin.UI/Services/AdminUserContext.cs`, the two opaque-lookup clients, their page/service/context tests, and `docs/brownfield/api-contracts.md` -- propagate exact role casing and current authentication-state changes through UI visibility, clear protected detail/state on demotion, use role-aware omitted-tenant wording, exercise Admin allow and server-forbidden paths, close denied dialogs, restore focus to the exact row/button that initiated the action, and publish the exact per-operation policy matrix.
- [x] `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` and `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsControllerTests.cs` -- clamp recent-command count to `1..1000` after the default is applied and before calling `IStreamQueryService`.
- [x] `src/Hexalith.EventStore.Admin.Server/Configuration/AdminRequestSizeLimits.cs`, all six body-owning files under `src/Hexalith.EventStore.Admin.Server/Controllers/`, `src/Hexalith.EventStore.Admin.Server.Host/Middleware/AdminRequestBodySizeMiddleware.cs`, `src/Hexalith.EventStore.Admin.Server.Host/Program.cs`, and `tests/Hexalith.EventStore.Admin.Server.Host.Tests/AdminRequestBodySizeTests.cs` -- mark every JSON body as 1 MiB except 10 MiB import, expose `413` response metadata, normalize only proven size-limit overflow to static Problem Details, propagate unrelated I/O/cancellation failures, and prove exact/+1-byte boundaries for sandbox, a mutation, import, and unknown-length bodies with zero downstream work.
- [x] `tests/Hexalith.EventStore.Admin.Server.Tests/Authorization/AdminActionSecurityMetadataTests.cs`, `tests/Hexalith.EventStore.Admin.Server.Tests/OpenApi/AdminOpenApiDocumentTests.cs`, `tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs`, controller tests, and `tests/Hexalith.EventStore.Admin.Server.Host.Tests/HostBootstrapTests.cs` -- use an explicit exhaustive policy map and tenant-filter map that reject every unclassified action; classify effective body binding; record every bodyless/unavailable endpoint, body shape, owner, and reason; prove the exact Problem Details `413` schema; exercise real JWT 401/403 redaction, backup admission/workflow tenant denial with opaque-ID redaction and zero work, and retain unauthenticated `200` only for `/health`, `/alive`, and `/ready`.

**Acceptance Criteria:**
- Given the complete public Admin action inventory, when authorization metadata is inspected and representative routes are invoked, then every action requires its intended exact role policy, none is anonymous, and only the three transport probes remain anonymously reachable.
- Given a non-admin request that is explicitly cross-tenant, omits a tenant on a tenant-filterable action, supplies a body tenant without a matching claim, or uses an opaque lookup, when authorization executes, then the request is narrowed or rejected before protected service work and the response/log cannot reveal tenant or resource existence.
- Given an Admin principal, when it invokes scoped or unscoped Admin routes, then global access remains available without fabricating tenant claims.
- Given the recent-commands query receives any supported boundary input, when the facade invokes the service, then omitted equals `1000`, non-positive equals `1`, `1..1000` is unchanged, and values above `1000` equal `1000`.
- Given an authenticated request reaches a JSON-body endpoint, when its encoded HTTP body is exactly at the applicable boundary or one byte beyond it, then the exact-size request proceeds normally and the next byte returns bounded `413 application/problem+json` with no payload echo, partial work, or service invocation.
- Given implementation is complete, when focused authorization/controller/host tests, both Admin test projects, and the Release solution build run, then all pass without warnings or regressions.

## Spec Change Log

- 2026-09-06 -- Review found that the first derivation registered a dead authorization-result handler, omitted explicit authenticated-user requirements, could convert unrelated request-body I/O failures to `413`, verified metadata instead of enough effective host/OpenAPI behavior, and changed opaque lookup roles without updating Admin UI and documentation. Expanded the Code Map, Tasks, Design Notes, and verification targets to require registration replacement, bounded challenge normalization, real-host JWT checks, mutation-size and generated-OpenAPI coverage, executable N/A ownership, and role-aware UI/client/docs propagation. Known-bad states avoided: claims-bearing unauthenticated access, empty or unbounded denial bodies, false `413` classification, hidden runtime metadata drift, and Operator/ReadOnly controls that always fail. KEEP: exact role values; fail-closed tenant narrowing and Admin bypass; correlation-only tenant-denial logs; recent-command default/clamp; 1 MiB ordinary and 10 MiB import limits; exact/+1 and unknown-length tests; opaque lookups remain Admin-only; health probes remain reachable; `sprint-status.yaml` remains untouched.
- 2026-09-06 -- The second derivation checked consistency body scope against only the first tenant claim, used non-exhaustive role/tenant inventories, allowed unsafe challenge headers and cross-host handler side effects, left exact/current UI-role behavior and initiating-action focus underspecified, and published incomplete policy/N/A documentation. Expanded the Code Map, Tasks, Design Notes, and evidence requirements to match any authorized tenant claim, use explicit exhaustive maps, scope denial normalization, allowlist safe headers, react to role transitions, align exact role casing, make omitted scope truthful, exercise backup/opaque allow-deny paths, and record the full endpoint/body-shape/owner/reason matrix. Known-bad states avoided: false cross-tenant denials, silent ReadOnly defaults for future mutations, missing tenant filters, stale protected UI data, controls that the server always rejects, focus stranded in denied dialogs, and misleading public policy/scope text. KEEP: authenticated exact server policies; effective handler replacement and captured scheme bodies; bounded redacted Problem Details; fail-closed route/query/action tenant filtering and Admin bypass; correlation-only logs; recent-command default/clamp; 1 MiB ordinary and 10 MiB import limits; exact/+1 and unknown-length no-work tests; generated OpenAPI coverage; Admin-only opaque lookups with typed forbidden clients; health probes; and the untouched `sprint-status.yaml`.

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

### 2026-09-06 — Review pass
- verdicts: 39 findings — high 3, medium 27, low 1, false 8, maybe-false 0
- findings:
  - `[medium]` `[bad_spec]` Blind hunter: consistency body scope checks only the first tenant claim — an explicit tenant that matches a later current claim is wrongly denied; the spec now requires comparison with every usable claim while retaining first-claim narrowing only for omission.
  - `[medium]` `[bad_spec]` Blind hunter: scheme-controlled headers survive Admin denial normalization — the handler clears only content metadata, so a challenge can return a protected `Location`; the spec now requires clearing unsafe headers and restoring only an explicit safe allowlist.
  - `[medium]` `[bad_spec]` Blind hunter: `RemoveAll<IAuthorizationMiddlewareResultHandler>` changes unrelated co-hosted authorization behavior — the public service extension erases host registrations and the handler has no Admin guard; the spec now requires non-destructive registration and framework delegation outside Admin routes.
  - `[false]` `[reject]` Blind hunter: normalized `413` behavior must be installed by `AddAdminApi` — the deployable public boundary is `Admin.Server.Host`, where the middleware runs, while a service-registration extension cannot install pipeline middleware and endpoint metadata still supplies server enforcement.
  - `[medium]` `[bad_spec]` Blind hunter: the action-policy inventory defaults every unlisted action to ReadOnly — a future mutation can be misclassified as the weakest policy; the spec now requires an explicit exhaustive map with no fallback.
  - `[high]` `[bad_spec]` Blind hunter: the complete built-host inventory does not classify tenant-filter enforcement — removal or omission of a filter can enable cross-tenant access without failing the matrix; the spec now requires an exhaustive action-to-tenant-filter map.
  - `[false]` `[reject]` Blind hunter: current inferred body actions escape the `[FromBody]` inventory — every current Admin body action declares `[FromBody]`, so the alleged bad outcome is absent; the revised evidence nevertheless uses effective binding metadata for future classification.
  - `[medium]` `[patch]` Blind hunter: the generated `413` test accepts any schema with `$ref` or `type` — that does not prove Problem Details; the replacement derivation must resolve and assert the specific Problem Details schema.
  - `[medium]` `[bad_spec]` Blind hunter: consistency capabilities are snapshotted at initialization — supported authentication-state changes can leave protected detail visible after demotion or block a promoted Admin; the spec now requires current-state refresh and protected-state clearing.
  - `[medium]` `[bad_spec]` Blind hunter: DAPR actor capabilities are snapshotted at initialization — `AuthorizedView` reacts while the page flag and rendered state remain stale; the spec now requires the page to refresh and clear actor state on demotion.
  - `[medium]` `[bad_spec]` Blind hunter: UI role parsing is case-insensitive while server policies now accept exact values — lowercase `admin` exposes controls that every request rejects; the spec now aligns UI parsing and tests with the exact server role contract.
  - `[medium]` `[bad_spec]` Blind hunter: consistency UI describes omitted tenant scope as all tenants — non-Admin omission is now narrowed to one authorized tenant, so the spec now requires role-aware truthful scope wording.
  - `[medium]` `[bad_spec]` Blind hunter: shared command/event/stream/projection filters retain misleading “all tenants” wording — the same new narrowing contract affects those consumers; the spec now includes shared tenant-filter surfaces and role-aware omission guidance.
  - `[medium]` `[bad_spec]` Blind hunter: actor denial focuses the ID input instead of the initiating Inspect action — this violates the explicit UX focus contract; the spec now names the exact initiating button and its test.
  - `[medium]` `[bad_spec]` Blind hunter: consistency forbidden paths neither restore initiating focus nor close the denied cancellation dialog — the spec now names row/button focus and denied-dialog closure explicitly.
  - `[medium]` `[patch]` Blind hunter: the public policy table assigns consistency list to Operator although `GetChecks` is ReadOnly — the replacement derivation must publish the exact per-operation policy.
  - `[medium]` `[bad_spec]` Blind hunter: the N/A record omits the bodyless PUT snapshot-policy operation and does not enumerate endpoints/body shapes — the spec now requires a complete endpoint-level method/path/body/owner/reason table.
  - `[medium]` `[bad_spec]` Edge hunter: a requested body tenant matching a later claim is denied — verified with `FindFirst`; grouped with the first-claim defect and covered by the all-claims requirement.
  - `[medium]` `[bad_spec]` Edge hunter: consistency detail stays visible after role revocation — the page does not subscribe to the supported authentication-state change contract; grouped with the stale-capability amendment.
  - `[medium]` `[bad_spec]` Edge hunter: actor state and capability become stale across role changes — the nested `AuthorizedView` already proves such changes are supported; grouped with the current-state amendment.
  - `[medium]` `[defer]` Edge hunter: consistency row lookup leaves pre-existing unauthorized, unavailable, or canceled failures unhandled — baseline code already awaited the same client without these catches, so this is not caused by Story 5.2.
  - `[medium]` `[defer]` Edge hunter: consistency cancellation leaves pre-existing 401/409/422 failures unhandled — baseline cancellation handled only unavailable/cancellation paths; Story 5.2 adds the newly relevant 403 path without causing the older gap.
  - `[low]` `[reject]` Edge hunter: deferred actor focus can throw during a circuit disconnect — the narrow disconnect race is unlikely in ordinary use and adding another guard branch is not justified by a demonstrated user-visible failure.
  - `[medium]` `[bad_spec]` Edge hunter: forbidden consistency cancellation does not restore initiating focus — verified and grouped with the explicit focus/dialog amendment.
  - `[high]` `[bad_spec]` Edge hunter: built-host evidence omits tenant-filter metadata — verified and grouped with the exhaustive tenant-filter map amendment.
  - `[medium]` `[defer]` Edge hunter: OpenAPI and Swagger are anonymous non-probe surfaces — carried: the same location and claim remain as previously logged; this is pre-existing and Story 5.4 owns the environment-safe Swagger gate.
  - `[medium]` `[bad_spec]` Verification-gap reviewer: new backup admission and crypto-shredding read filters lack hosted redaction regressions — controller-only fallback tests can remain green while echoing tenant and opaque route IDs; the spec now requires hosted denial/redaction/zero-work cases for both routes.
  - `[medium]` `[patch]` Verification-gap reviewer: the consistency Admin allow-path test never invokes row selection — the replacement derivation must exercise the handler, verify the API call, and render returned detail/anomaly data.
  - `[medium]` `[patch]` Verification-gap reviewer: page-level consistency 403 handling is untested — the replacement derivation must cover server-forbidden detail and cancellation interactions and assert canonical denial behavior.
  - `[medium]` `[patch]` Verification-gap reviewer: consistency body-denial log redaction is untested — the replacement derivation must capture the controller warning and prove correlation-only output.
  - `[medium]` `[bad_spec]` Verification-gap reviewer: actor focus restoration is not asserted — the implementation also focused the wrong element; grouped with the requirement to target and test the initiating Inspect button.
  - `[false]` `[reject]` Intent auditor: workflow execution facts are not visible in the code diff — skill invocation, delegation, commit, and final result are orchestration evidence evaluated outside the product diff, not a product defect.
  - `[false]` `[reject]` Intent auditor: no independent Story 5.2 contract is demonstrated — the authoritative pre-existing `epics.md` Story 5.2 was loaded and compiled into the referenced epic context before the spec was derived.
  - `[medium]` `[defer]` Intent auditor: broad public-surface reading includes anonymous OpenAPI/Swagger — carried: the same claim remains pre-existing and explicitly owned by Story 5.4, while Story 5.2 covers the controller boundary.
  - `[high]` `[bad_spec]` Intent auditor: tenant evidence is representative but not a complete effective tenant-filter inventory — the authoritative story requires the full matrix; grouped with the explicit exhaustive tenant map and hosted backup regressions.
  - `[false]` `[reject]` Intent auditor: global Admin access is proved mainly below HTTP — existing integration tests exercise Admin scoped and unscoped public routes, which satisfies the story's representative runtime requirement alongside the complete inventory.
  - `[false]` `[reject]` Intent auditor: recent-command bounds are tested at a direct controller boundary — this is exactly the required facade-to-service boundary and verifies the value before service invocation.
  - `[false]` `[reject]` Intent auditor: request-body evidence uses representative real-host routes — this matches the story's explicit representative mutation, sandbox, and import runtime requirement plus a complete metadata inventory.
  - `[false]` `[reject]` Intent auditor: the diff is broader than a title-only interpretation — the authoritative Story 5.2 text explicitly owns query-count, request-size, UI, OpenAPI, and evidence work, so the narrow title reading is not controlling.

## Design Notes

Tenant scope must be established without reading the protected resource. Nullable `tenantId` action arguments provide the safe narrowing seam: non-admin callers receive one authorized claim value, while callers with no usable claim are denied. Routes whose only key is an opaque `checkId` or actor ID cannot establish that proof, so their existing route shape remains but their policy becomes Admin-only.

Request limits are encoded-body limits, not deserialized-object estimates. Enforcement must use endpoint metadata for documentation and server behavior, run after authentication/authorization, and normalize both known `Content-Length` rejection and body-reader overflow into the same safe Problem Details contract.

`AddAuthorizationBuilder()` already installs the framework `IAuthorizationMiddlewareResultHandler`; the Admin handler must explicitly replace it. Challenge/forbid side effects such as `WWW-Authenticate` remain, but any scheme-written body is captured and replaced with the bounded identifier-free Admin Problem Details before bytes escape. Effective endpoint tests resolve the built host rather than treating method attributes as runtime truth.

Replacement is scoped behavior, not permission to erase the host's authorization composition. Register the Admin handler after the framework default without `RemoveAll`; for non-Admin endpoints it delegates to the framework path. For Admin failures it captures the scheme response, clears scheme-controlled headers and body, restores only an explicit safe header allowlist (`WWW-Authenticate`), and writes the canonical bounded body. Tests must use a scheme that writes both a protected body and an unsafe `Location` header, and must prove a non-Admin endpoint retains framework behavior.

Opaque check and actor identifiers remain Admin-only because their current route contracts cannot prove tenant scope before lookup. This security change must propagate as one contract: lower-role UI controls and deep-link work are hidden or show the canonical denied state, focus returns to the initiating control, API clients preserve typed forbidden outcomes, and public policy documentation names the same minimum role.

UI authorization is not a one-time snapshot. The same authentication-state change source used by `AuthorizedView` must refresh page-level capability flags and clear already-rendered protected check/actor state on demotion or sign-out. UI parsing of `eventstore:admin-role` uses the same exact case-sensitive values as the server; a noncanonical value must not expose a control the server rejects. Omitted tenant filters are labeled as the caller's authorized scope for non-Admin users, never “all tenants.” Denied detail selection restores focus to the initiating row; denied cancellation closes its dialog and returns focus to the initiating Cancel button; denied actor inspection returns focus to the Inspect button, with page tests asserting the actual focus target.

Tenant authorization compares an explicit body tenant with every non-empty current tenant claim. Selecting the first claim is allowed only when an optional scope was omitted and must be narrowed. The built-host evidence uses explicit exhaustive action-to-policy and action-to-tenant-filter maps with no fallback classification; newly discovered actions fail until deliberately classified. Backup admission and crypto-shredding workflow reads receive hosted wrong-tenant tests because their controller fallback can otherwise echo route identifiers.

Request-body overflow is the only condition mapped to `413`; generic `IOException` and cancellation are not overflow evidence. The matrix must explicitly classify bodyless and currently unavailable operations with their owner/reason and test a retained ordinary mutation in addition to sandbox and import.

The request-limit record is an enumerated table, not a generic method-category sentence: every current endpoint appears with HTTP method/path, effective body binding or `none`, limit or N/A/unavailable, owner, and reason. Generated OpenAPI evidence resolves each `413` schema specifically to Problem Details. Current controller actions all declare body binding explicitly; effective binding metadata is still the source used for the inventory so inferred future bodies cannot be silently categorized as bodyless.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj --configuration Release` -- all Admin authorization/controller tests pass.
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Host.Tests/Hexalith.EventStore.Admin.Server.Host.Tests.csproj --configuration Release` -- real host authorization, probe, and request-limit tests pass.
- `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release` -- role-aware Admin UI/client regressions pass.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- solution builds with zero warnings/errors.
- `git diff --check` -- no whitespace errors.
