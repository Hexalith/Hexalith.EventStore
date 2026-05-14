# Story 22.3: Gateway-Owned Tenant and RBAC Enforcement

Status: done

Context created: 2026-05-12
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR90-FR92, with direct dependency awareness for Story 22.1 public gateway DTO/client behavior and Story 22.2 projection adapter routing.

## Story

As a platform owner,
I want EventStore to validate tenant lifecycle, membership, role, and permission before invoking domain services or projection adapters,
so that Parties request paths do not perform EventStore gateway tenant authorization.

## Gateway Authorization Boundary Contract

- EventStore owns request-path tenant and RBAC enforcement for command and query gateway paths before any domain service or projection adapter invocation.
- Hexalith.Tenants is the lifecycle, membership, and role authority. EventStore must integrate through `ITenantValidator` and `IRbacValidator` adapter boundaries, not by reading Tenants state-store keys or duplicating Tenants aggregates.
- Authorization is fail-closed. Missing, stale, unavailable, ambiguous, disabled, suspended, not-found, non-member, insufficient-role, and insufficient-permission states must block invocation.
- EventStore must prefer Hexalith.Tenants public client/query contracts when they are available for the required validation operation. A documented DAPR actor adapter is allowed only when a suitable public Tenants client/query contract is unavailable, and it must still live behind the EventStore validator interfaces.
- Claims-based validators may remain only as explicitly configured local/dev/test fallback. In Tenants-backed runtime mode, stale, unavailable, ambiguous, malformed, or failed Tenants validation must fail closed and must not fall back to claims.
- The authorization context is immutable once resolved for a request: tenant ID, authenticated subject, message category, message type, aggregate ID, projection/query identity, correlation ID, and cancellation token must flow unchanged from gateway validation through downstream invocation.
- Tenant mismatch states, including missing tenant, conflicting tenant sources, route/body/header/client DTO disagreement, or aggregate/projection tenant disagreement, must be blocked before validator adapter calls when the mismatch is locally detectable and must never be normalized into an allowed request.
- Authorization must run before cache, ETag, not-modified, projection-state, aggregate-state, replay, or adapter lookup paths that could disclose tenant existence, resource existence, stream metadata, or projection freshness.
- 401/403 ProblemDetails type URIs, reason-code extension names, and client/fake behavior are public gateway contract surface. Treat changes as SemVer-relevant.
- Story 22.3 may add or harden public tenant/RBAC fake paths in `Hexalith.EventStore.Testing`, but must not reopen Story 22.1's broader gateway fake/builders unless needed for auth coverage.
- Story 22.3 owns only authn/authz tenant/RBAC outcomes on command, query, and validation paths. Non-auth query validation, missing projections, malformed filters, projection consistency, and business/query taxonomy remain Story 22.4 scope.

## Current Implementation Intelligence

- `src/Hexalith.EventStore/Authorization/ITenantValidator.cs` and `IRbacValidator.cs` are the current API-layer adapter boundaries. They accept `ClaimsPrincipal`, tenant/domain/message data, optional aggregate ID, and cancellation token.
- `TenantValidationResult` and `RbacValidationResult` currently expose only `IsAuthorized` plus optional free-text `Reason`. They do not expose stable reason codes, tenant lifecycle status, role, permission, ambiguity, staleness, or unavailable categories.
- `ClaimsTenantValidator` is the default when `EventStore:Authorization:TenantValidatorActorName` is null. It checks normalized `eventstore:tenant` claims, allows global administrators, and denies missing/mismatched tenant claims.
- `ClaimsRbacValidator` is the default when `EventStore:Authorization:RbacValidatorActorName` is null. It checks optional `eventstore:domain` and `eventstore:permission` claims, supports `commands:*`, `command:submit`, `queries:*`, `query:read`, legacy `command:query`, and exact message-type permissions.
- `ActorTenantValidator` and `ActorRbacValidator` already delegate to DAPR actors through `ITenantValidatorActor` and `IRbacValidatorActor`. They wrap actor failures/null responses as `AuthorizationServiceUnavailableException`, preserving fail-closed behavior.
- Actor validator request/response contracts currently live under `src/Hexalith.EventStore.Server/Actors/Authorization`. They are DAPR actor-oriented, not explicitly Hexalith.Tenants-backed integration contracts.
- `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` selects claims-based or actor-based validator implementations by configuration and validates non-empty actor names at startup.
- `AuthorizationBehavior<TRequest,TResponse>` runs for `SubmitCommand` and `SubmitQuery` before handlers. It skips API-layer authorization when no `HttpContext` exists for internal MediatR calls.
- `CommandsController` and `QueriesController` both require `[Authorize]`, require a `sub` claim, store `RequestTenantId`, and then send into the MediatR pipeline. The tenant/RBAC check happens in `AuthorizationBehavior`, before command handler or query router invocation.
- `CommandValidationController` and `QueryValidationController` use the same validators but intentionally return `200 OK` with `PreflightValidationResult(IsAuthorized=false)` for denied tenant/RBAC checks. Decide whether Story 22.3 should add reason codes to this preflight result without changing its status-code semantics.
- `AuthorizationExceptionHandler` maps `CommandAuthorizationException` to generic 403 ProblemDetails with `correlationId` and `tenantId` extensions. It sanitizes internal terms but currently does not expose stable auth reason codes.
- `AuthorizationServiceUnavailableHandler` maps unavailable authorization infrastructure to 503 ProblemDetails with `Retry-After: 30` and a generic detail. It does not currently expose correlation ID or reason-code extensions in the response body.
- `ProblemTypeUris` currently has generic `AuthenticationRequired`, `Forbidden`, and `ServiceUnavailable` constants, but no tenant-not-found, tenant-disabled, user-not-member, insufficient-role, insufficient-permission, ambiguous, stale, or authorization-service-unavailable reason taxonomy.
- `Hexalith.Tenants` submodule contains tenant lifecycle, membership, and role models: `TenantStatus`, `TenantRole`, `TenantSummary`, `TenantDetail`, `UserTenantMembership`, `GetTenantQuery`, `GetTenantUsersQuery`, `GetUserTenantsQuery`, `TenantsProjectionActor`, and client projection helpers. Use public Tenants contracts/client paths; do not bypass its command/query surface or projections.
- `src/Hexalith.EventStore.Testing/Fakes/FakeTenantValidatorActor.cs` and `FakeRbacValidatorActor.cs` already fake actor validator contracts for tests. They do not yet provide high-level gateway fake builders for public tenant/RBAC ProblemDetails paths.

## Acceptance Criteria

1. **Gateway validator boundaries are Hexalith.Tenants-ready.**
   - Given a command or query request enters EventStore
   - When tenant/RBAC validation is required
   - Then EventStore validates through `ITenantValidator` and `IRbacValidator` adapter boundaries before invoking any domain service or projection adapter.
   - And the implementation path for Hexalith.Tenants-backed lifecycle, membership, role, and permission checks is explicit in code, configuration, and docs.
   - And claims-based validators remain clearly documented as local/dev fallback, not the production source of tenant lifecycle truth.
   - And deployed/runtime wiring fails closed when Tenants-backed validation is configured but unavailable, stale, ambiguous, malformed, or misconfigured.

2. **Tenant lifecycle and membership failures fail closed before invocation.**
   - Given tenant data is missing, stale, unavailable, ambiguous, not found, disabled, suspended, or the user is not a member
   - When EventStore evaluates a command or query request
   - Then it blocks the request before `SubmitCommandHandler`, `CommandRouter`, `SubmitQueryHandler`, `QueryRouter`, DAPR domain invocation, or projection actor invocation.
   - And it blocks before aggregate actor state access, projection adapter resolution, ETag comparison, cache lookup, replay/read model lookup, or any resource-existence side channel.
   - And locally detectable tenant-source conflicts return a stable tenant mismatch/missing reason without invoking Tenants, domain services, or projection adapters.
   - And tests prove the downstream domain service/projection adapter is not called for each blocked category.

3. **Role and permission failures fail closed before invocation.**
   - Given the authenticated user lacks the required tenant role, domain access, message-category permission, or specific command/query permission
   - When EventStore evaluates a command or query request
   - Then it returns a stable authorization failure and never invokes downstream processing.
   - And unknown roles, unknown permissions, malformed permission values, missing `sub`, or ambiguous role hierarchy data default to deny or unavailable according to the decision table.
   - And command and query paths are both covered, including `command` vs `query` message-category behavior.

4. **ProblemDetails and reason codes are stable.**
   - Given authentication or authorization fails
   - When the HTTP response is returned
   - Then 401/403 ProblemDetails type URIs and reason-code extension names are stable and documented.
   - And the taxonomy covers authentication required, tenant not found, tenant disabled/suspended, user not a member, insufficient role, insufficient permission, ambiguous authorization state, stale authorization state, and authorization service unavailable.
   - And unavailable validator infrastructure maps to a fail-closed 503 or explicitly approved 403/503 policy, with stable reason code and `Retry-After` behavior documented.
   - And responses do not leak actor type names, DAPR details, state-store keys, tenant membership lists, tokens, payload data, or protected data.
   - And docs list the ProblemDetails type URI strings, the `reasonCode` extension key, canonical reason-code values, retryability, caller-action category, and whether each public field is safe for end-user display.
   - And logs/telemetry may record stable reason codes and correlation metadata but must not log user display names, membership lists, payloads, tokens, protected data, or raw Tenants internals.

5. **Preflight validation and client/testing contracts align.**
   - Given downstream services call command/query validation endpoints or use gateway fakes
   - When tenant/RBAC denial is simulated
   - Then validation responses, client exceptions/results, and testing fakes expose deterministic reason codes without requiring downstream code to parse free-text details.
   - And preflight validation endpoints continue returning `200 OK` with `isAuthorized=false` and a stable reason code for authorization denials; any deviation requires updated API docs and client tests in this story.
   - And fake/client defaults must not accidentally allow a request when tenant, subject, role, permission, or denial reason configuration is omitted.
   - And runtime command/query endpoints still return RFC 7807 401/403 or the documented authorization-service-unavailable response.
   - And Story 22.1 public package ownership remains intact: DTOs in Contracts, HTTP convenience behavior in Client, deterministic fakes/builders in Testing.

6. **Hexalith.Tenants integration is verifiable without submodule shortcuts.**
   - Given Hexalith.Tenants is the tenant authority
   - When EventStore validates authorization
   - Then the integration uses public Tenants contracts/client/query behavior or documented DAPR actor adapter behavior.
   - And tests cover active, disabled/suspended-equivalent, missing/not-found, non-member, insufficient role, insufficient permission, stale/unavailable, and ambiguous responses.
   - And no implementation reads Hexalith.Tenants state-store keys, projection actor state, or internal server aggregates directly from EventStore.
   - And public reason codes and client exception/result types stay in stable public Contracts/Client surfaces, Tenants runtime integration stays in server/infrastructure wiring, and Testing fakes do not depend on Tenants runtime packages.
   - And any caching, snapshot, or projection freshness used by the Tenants-backed adapter has explicit staleness semantics, tenant scoping, cancellation/timeout behavior, and fail-closed mapping.

7. **Regression and evidence are recorded before review.**
   - EventStore Server authorization/controller/error-handling focused tests pass.
   - Contracts, Client, and Testing focused tests pass when public DTO/client/fake behavior changes.
   - Hexalith.Tenants contract/client/test-helper evidence is recorded when Tenants integration types are referenced or changed.
   - Documentation validation is run where available.
   - The final decision table, selected integration-path note, test command list/results, and any Tenants adapter contract notes are recorded in the Dev Agent Record or a linked evidence file.
   - Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story to review.

## Tasks / Subtasks

- [x] **ST0 - Baseline current authorization boundary and classify gaps.** (AC: 1, 2, 3, 4, 5, 6)
  - [x] Read this story, Epic 22, PRD FR90-FR92, architecture ADR-P8, Story 22.1, Story 22.2, and `docs/guides/security-model.md` before code edits.
  - [x] Inventory current API validator boundaries in `src/Hexalith.EventStore/Authorization`.
  - [x] Inventory current MediatR and controller entry points: `AuthorizationBehavior`, `CommandsController`, `QueriesController`, `CommandValidationController`, and `QueryValidationController`.
  - [x] Inventory current ProblemDetails handlers and type URI constants.
  - [x] Inventory Hexalith.Tenants public contracts/client/projection surfaces needed for lifecycle, membership, and role checks.
  - [x] Record a validator contract decision table covering `tenantId`, `subjectId` from `sub`, requested operation, required role/permission, correlation/cancellation flow, typed allow/deny/unavailable result, denial reason mapping, retryability for stale/unavailable cases, and public response exposure.
  - [x] Record a request-path interception table for every public command, query, validation endpoint, and affected client operation with tenant source, identity source, required role, required permission, validator call, failure reason, HTTP/preflight behavior, caller-action category, and downstream call that must not occur.
  - [x] Record the authorization pipeline order as authenticate request, resolve tenant from the gateway contract, validate tenant lifecycle, validate membership, validate role/permission, then invoke command handler, domain service, query router, or projection adapter.
  - [x] Record the immutable authorization-context fields and prove downstream handlers, adapters, fakes, and client helpers cannot mutate or recompute tenant/subject/message authorization inputs after validation.
  - [x] Record all tenant-source conflict cases, including missing tenant, conflicting route/body/header/client DTO values, aggregate/projection tenant mismatch, and malformed subject/tenant claims.
  - [x] Record a decision table for claims fallback, actor adapter, Tenants client/query adapter, reason-code taxonomy, package ownership, and deferred items.

- [x] **ST1 - Define typed tenant/RBAC authorization outcomes.** (AC: 1, 4, 5)
  - [x] Add or extend result models so validator outcomes have stable machine-readable reason codes separate from human-readable detail.
  - [x] Include categories for allowed, authentication-required, tenant-not-found, tenant-disabled, tenant-suspended or equivalent inactive state, user-not-member, insufficient-role, insufficient-permission, stale, ambiguous, and unavailable.
  - [x] Define a single typed authorization failure reason enum/value object or equivalent stable model shared by command, query, validation endpoint, client, and Testing fake paths.
  - [x] Use canonical reason-code strings such as `authentication_required`, `subject_missing`, `tenant_missing`, `tenant_mismatch`, `tenant_not_found`, `tenant_disabled`, `tenant_suspended`, `tenant_stale`, `tenant_unavailable`, `tenant_ambiguous`, `principal_not_member`, `insufficient_role`, and `insufficient_permission`, unless the decision table documents a compatible alternative.
  - [x] Define exact mapping from typed validator outcome to HTTP status, ProblemDetails type URI, title/detail safety rules, `extensions.reasonCode`, preflight validation response fields, retryability, and caller-action category.
  - [x] Preserve existing `IsAuthorized` behavior where public or test code depends on it; add compatibility constructors/helpers if needed.
  - [x] Add focused tests that prove free-text `Reason` is not the only way to identify failures.
  - [x] Add focused tests that unknown enum values, malformed reason strings, missing reason codes, and legacy actor responses fail closed instead of silently mapping to generic allow.

- [x] **ST2 - Implement Hexalith.Tenants-backed validation path or document the approved adapter shape.** (AC: 1, 2, 3, 6)
  - [x] Prefer an EventStore-side adapter that consumes public Hexalith.Tenants contracts/client/query behavior for tenant lifecycle, membership, and role checks.
  - [x] Record a short ADR-style integration note with selected path, package/project references, DTO/query/actor contracts used, timeout/error behavior, and why the path does not depend on Tenants internals or state-store keys.
  - [x] If the approved integration path is DAPR actor validation, freeze the actor type names, actor IDs, request/response shapes, timeout/unavailable semantics, mapping to Tenants state, and public-contract fallback condition in docs and tests.
  - [x] Keep claims-based validators as local/dev fallback and explicitly label the gap they cannot close: lifecycle state, stale/ambiguous authority, and role hierarchy from Tenants.
  - [x] Ensure claims-based validators are activated only through explicit local/dev/test configuration and are not used as fallback when Tenants-backed runtime validation is configured.
  - [x] Ensure cancellation tokens flow through external validation calls.
  - [x] Ensure validator unavailability, null responses, malformed responses, and ambiguous results throw or return unavailable/ambiguous outcomes that fail closed.
  - [x] Ensure adapter timeouts, cancellation, stale data, and transient Tenants errors are classified deterministically without ad hoc retry loops or claims fallback.
  - [x] Ensure any Tenants projection/cache use is tenant-scoped and cannot reuse membership, role, permission, or lifecycle state across tenants or subjects.

- [x] **ST3 - Enforce command and query pre-invocation blocking.** (AC: 2, 3)
  - [x] Add focused tests proving denied command authorization stops before `SubmitCommandHandler`, `CommandRouter`, domain service invocation, aggregate actor state access, or command processing side effects.
  - [x] Add focused tests proving denied query authorization stops before `SubmitQueryHandler`, `QueryRouter`, projection actor invocation, and ETag/projection state access where relevant.
  - [x] For each deny outcome, assert the validator was called, downstream command/query/projection/domain-service collaborators were not called, and the HTTP/client/preflight result carries the stable reason code.
  - [x] Verify the `HttpContext`-absent internal MediatR bypass is still intentional and cannot be used by external HTTP requests.
  - [x] Verify authorization runs before `If-None-Match`/ETag shortcuts, query cache hits, projection freshness checks, stream/replay reads, and not-found/missing-projection responses that could leak cross-tenant resource existence.
  - [x] Verify the same resolved tenant/subject/message authorization context is passed to validators and downstream dispatch; no handler may re-read mutable HTTP claims, headers, route values, or body fields to change authorization scope.
  - [x] Verify command and query validation endpoints either keep `200 OK` preflight semantics with typed reason codes or move only with explicit docs/tests approval.
  - [x] Keep query-path assertions limited to tenant/RBAC authorization outcomes; non-auth query validation, not-found, malformed-query, projection, and adapter taxonomy remains Story 22.4.

- [x] **ST4 - Freeze auth ProblemDetails taxonomy and docs.** (AC: 4, 5)
  - [x] Add or update `ProblemTypeUris` and/or documented `reasonCode` extension constants for authentication and authorization failures.
  - [x] Update `AuthorizationExceptionHandler`, `AuthorizationServiceUnavailableHandler`, and any authentication failure hook needed to emit stable ProblemDetails metadata.
  - [x] Preserve sanitization of internal terms and add tests that internal actor/DAPR/state-store details do not leak.
  - [x] Add contract tests for each auth failure category that assert status, ProblemDetails `type`, safe field presence, reason-code extension, correlation/trace identifier behavior where existing conventions allow, and absence of actor, DAPR, state-store, membership-list, token, payload, or protected-data details.
  - [x] Avoid coupling tests to exact English `title` or `detail` text except for required presence and leak checks; reason codes and type URIs are the stable contract.
  - [x] Add log/telemetry tests or review evidence proving reason codes and correlation IDs are observable while sensitive authorization inputs and Tenants internals are not emitted.
  - [x] Update `docs/reference/command-api.md`, `docs/reference/query-api.md`, `docs/reference/problems/forbidden.md`, and `docs/guides/security-model.md`.
  - [x] Add new or updated problem reference docs for tenant lifecycle and permission reason codes if the docs taxonomy requires separate pages.

- [x] **ST5 - Align Client and Testing support for tenant/RBAC paths.** (AC: 5)
  - [x] Update `EventStoreGatewayException` parsing or public client result models if reason-code extensions are added.
  - [x] Add deterministic fake/builder support in `Hexalith.EventStore.Testing` for tenant/RBAC denial and unavailable paths.
  - [x] Preserve `reasonCode`, ProblemDetails `type`, HTTP status, and safe tenant/correlation context in client exceptions/results.
  - [x] Add validation endpoint tests proving authorization denials return `200 OK`, `isAuthorized=false`, stable reason code, and safe fields, while true transport/authentication failures follow the documented HTTP error path.
  - [x] Add client and Testing fake parity tests proving every typed deny outcome can round-trip without parsing free-text details.
  - [x] Preserve existing fake command/query request recording and not-modified behavior from Story 22.1.
  - [x] Add tests for client/fake behavior covering 401, 403 reason-code variants, and authorization service unavailable.
  - [x] Add fake default-behavior tests proving omitted tenant, subject, role, permission, or reason-code setup denies deterministically instead of implicitly allowing.

- [x] **ST6 - Validate Hexalith.Tenants integration and runtime wiring.** (AC: 6, 7)
  - [x] Add focused unit tests using Hexalith.Tenants contracts/test helpers for active tenant, disabled/inactive tenant, missing tenant, user membership, and role hierarchy.
  - [x] Add service registration/startup validation tests for selected authorization mode, options, and missing configuration.
  - [x] Verify public reason codes/client contract types do not require Tenants runtime packages, Tenants runtime integration is server/infrastructure-only, and Testing fakes avoid Tenants runtime package dependencies.
  - [x] Verify service registration cannot configure mutually exclusive claims fallback and Tenants-backed runtime validation in a way that silently downgrades production enforcement.
  - [x] If AppHost or DAPR access control changes are required, update only the root-level submodule/apphost wiring needed and record Aspire restart evidence.
  - [x] If no AppHost changes are needed, explicitly record why current topology is sufficient.

- [x] **ST7 - Validate and record evidence.** (AC: 7)
  - [x] Run focused authorization/server tests, starting with `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "Authorization|CommandsControllerTenant|AuthorizationServiceUnavailable|QueryValidation|CommandValidation"`.
  - [x] Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests` if Contracts reason-code DTOs/constants change.
  - [x] Run `dotnet test tests/Hexalith.EventStore.Client.Tests` if gateway client ProblemDetails parsing changes.
  - [x] Run `dotnet test tests/Hexalith.EventStore.Testing.Tests` if fake/builders change.
  - [x] Run Hexalith.Tenants focused contract/client/testing tests if the submodule is changed or referenced through new integration code.
  - [x] Run docs/markdown validation where available.
  - [x] Record the decision table location, selected integration-path note, downstream non-invocation proof, client/fake parity proof, Story 22.4 exclusions, and individually run test commands/results in the Dev Agent Record.
  - [x] Record authorization-before-cache/ETag/projection lookup proof, tenant-source conflict proof, immutable authorization-context proof, and safe-observability proof.
  - [x] Update Dev Agent Record, File List, Verification Status, and Change Log.

## Developer Notes

Architecture and product guardrails:

- ADR-P8 is controlling: EventStore validates tenant existence, lifecycle state, user membership, role, and permission before invoking domain services or projection adapters.
- Story 22.1 owns public gateway DTO/client/fake package boundaries. Reuse its package ownership decisions and avoid moving unrelated DTOs.
- Story 22.2 owns projection adapter contracts. This story may require query authorization before adapter invocation, but must not reopen adapter serialization or routing scope.
- Story 22.4 owns broad query paging/filter/freshness/error taxonomy. This story owns only auth-related ProblemDetails and reason-code behavior.
- Hexalith.Tenants is a root-level submodule and peer service. Do not initialize nested submodules. Do not patch Tenants internals unless the chosen integration cannot be completed through public contracts/client surfaces.
- EventStore must not trust domain services or Parties to enforce platform authorization for EventStore gateway requests.

Implementation traps to avoid:

- Do not read Hexalith.Tenants state-store keys directly from EventStore. Use public Tenants contracts/client/query behavior or a documented validator actor boundary.
- Do not treat claims-only validation as proof of tenant lifecycle, current membership, role hierarchy, stale data, or ambiguous authorization state.
- Do not use display names or user-controllable claims for authorization identity. Follow project rule: use `sub` as authenticated user identifier; preserve existing `NameIdentifier/sub` mapping deliberately where actor adapters require it.
- Do not collapse tenant-not-found, disabled, non-member, insufficient-role, and insufficient-permission into one free-text `Forbidden` without a stable machine-readable reason code.
- Do not leak internal actor, DAPR, state-store, aggregate, membership-list, token, command payload, query payload, or protected-data details in client-facing ProblemDetails.
- Do not let `ProjectionActorType` or generic query adapter routing bypass the same gateway authorization rules as commands.
- Do not compute `NotModified`, query cache hits, projection freshness, stream existence, aggregate existence, or replay availability before tenant/RBAC authorization completes.
- Do not mutate or recompute tenant ID, subject ID, message category, message type, aggregate ID, projection/query identity, correlation ID, or cancellation token after gateway authorization.
- Do not make validators retry in ad hoc loops. If retry/resiliency is needed, use existing DAPR/Aspire/resilience patterns or record a deferred infrastructure decision.
- Do not change validation endpoint HTTP semantics from `200 OK` with `isAuthorized=false` to hard 403 without updating docs, client behavior, and tests.
- Do not run broad solution-level tests first. Use focused slices because Server.Tests has a known CA2007 warning-as-error risk in this workspace.

Current file intelligence:

- API authorization abstractions:
  - `src/Hexalith.EventStore/Authorization/ITenantValidator.cs`
  - `src/Hexalith.EventStore/Authorization/IRbacValidator.cs`
  - `src/Hexalith.EventStore/Authorization/TenantValidationResult.cs`
  - `src/Hexalith.EventStore/Authorization/RbacValidationResult.cs`
  - `src/Hexalith.EventStore/Authorization/ClaimsTenantValidator.cs`
  - `src/Hexalith.EventStore/Authorization/ClaimsRbacValidator.cs`
  - `src/Hexalith.EventStore/Authorization/ActorTenantValidator.cs`
  - `src/Hexalith.EventStore/Authorization/ActorRbacValidator.cs`
- Authorization configuration and pipeline:
  - `src/Hexalith.EventStore/Configuration/EventStoreAuthorizationOptions.cs`
  - `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs`
  - `src/Hexalith.EventStore/Pipeline/AuthorizationBehavior.cs`
  - `src/Hexalith.EventStore/Pipeline/AuthorizationConstants.cs`
  - `src/Hexalith.EventStore/Authentication/EventStoreClaimsTransformation.cs`
- Gateway controllers:
  - `src/Hexalith.EventStore/Controllers/CommandsController.cs`
  - `src/Hexalith.EventStore/Controllers/QueriesController.cs`
  - `src/Hexalith.EventStore/Controllers/CommandValidationController.cs`
  - `src/Hexalith.EventStore/Controllers/QueryValidationController.cs`
- Error handling and docs:
  - `src/Hexalith.EventStore/ErrorHandling/AuthorizationExceptionHandler.cs`
  - `src/Hexalith.EventStore/ErrorHandling/AuthorizationServiceUnavailableHandler.cs`
  - `src/Hexalith.EventStore/ErrorHandling/CommandAuthorizationException.cs`
  - `src/Hexalith.EventStore/ErrorHandling/AuthorizationServiceUnavailableException.cs`
  - `src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs`
  - `docs/guides/security-model.md`
  - `docs/reference/command-api.md`
  - `docs/reference/query-api.md`
  - `docs/reference/problems/forbidden.md`
- Actor authorization contracts and fakes:
  - `src/Hexalith.EventStore.Server/Actors/Authorization/ITenantValidatorActor.cs`
  - `src/Hexalith.EventStore.Server/Actors/Authorization/IRbacValidatorActor.cs`
  - `src/Hexalith.EventStore.Server/Actors/Authorization/TenantValidationRequest.cs`
  - `src/Hexalith.EventStore.Server/Actors/Authorization/RbacValidationRequest.cs`
  - `src/Hexalith.EventStore.Server/Actors/Authorization/ActorValidationResponse.cs`
  - `src/Hexalith.EventStore.Testing/Fakes/FakeTenantValidatorActor.cs`
  - `src/Hexalith.EventStore.Testing/Fakes/FakeRbacValidatorActor.cs`
- Hexalith.Tenants public surfaces to inspect before implementation:
  - `Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Enums/TenantStatus.cs`
  - `Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Enums/TenantRole.cs`
  - `Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/GetTenantQuery.cs`
  - `Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/GetTenantUsersQuery.cs`
  - `Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/GetUserTenantsQuery.cs`
  - `Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/TenantDetail.cs`
  - `Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/UserTenantMembership.cs`
  - `Hexalith.Tenants/src/Hexalith.Tenants.Client/Registration/TenantServiceCollectionExtensions.cs`
  - `Hexalith.Tenants/src/Hexalith.Tenants.Client/Projections/ITenantProjectionStore.cs`
  - `Hexalith.Tenants/src/Hexalith.Tenants.Testing/Projections/InMemoryTenantProjection.cs`
- Focused tests:
  - `tests/Hexalith.EventStore.Server.Tests/Authorization/ClaimsTenantValidatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Authorization/ClaimsRbacValidatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Authorization/ActorTenantValidatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Authorization/ActorRbacValidatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Pipeline/AuthorizationBehaviorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Controllers/CommandsControllerTenantTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Controllers/CommandValidationControllerTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Controllers/QueryValidationControllerTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/AuthorizationExceptionHandlerTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Integration/AuthorizationServiceUnavailableTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreAuthorizationRegistrationTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreAuthorizationOptionsTests.cs`

Testing standards:

- Use xUnit v3, Shouldly, and NSubstitute where existing test projects already use them.
- Run test projects individually per repository guidance.
- Prefer unit/controller/pipeline tests for authorization behavior before integration tests.
- Integration tests under `tests/Hexalith.EventStore.IntegrationTests` require Docker and a running Aspire/DAPR environment.
- If Hexalith.Tenants submodule code changes, validate the changed Tenants project/test slice separately and do not initialize nested submodules.
- `Hexalith.EventStore.Server.Tests` has a known pre-existing CA2007 warning-as-error risk in broad runs. Use focused filters first and record unrelated blockers exactly.

## Files Likely Touched

- `src/Hexalith.EventStore/Authorization/*`
- `src/Hexalith.EventStore/Configuration/EventStoreAuthorizationOptions.cs`
- `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore/Pipeline/AuthorizationBehavior.cs`
- `src/Hexalith.EventStore/Pipeline/AuthorizationConstants.cs`
- `src/Hexalith.EventStore/ErrorHandling/AuthorizationExceptionHandler.cs`
- `src/Hexalith.EventStore/ErrorHandling/AuthorizationServiceUnavailableHandler.cs`
- `src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs`
- `src/Hexalith.EventStore/Controllers/CommandValidationController.cs`
- `src/Hexalith.EventStore/Controllers/QueryValidationController.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayException.cs` if reason-code extensions become client-visible.
- `src/Hexalith.EventStore.Testing/Fakes/*` and `src/Hexalith.EventStore.Testing/Builders/*` if tenant/RBAC gateway fakes/builders are added.
- `docs/guides/security-model.md`
- `docs/reference/command-api.md`
- `docs/reference/query-api.md`
- `docs/reference/nuget-packages.md`
- `docs/reference/problems/*`
- Related tests under `tests/Hexalith.EventStore.Server.Tests`, `tests/Hexalith.EventStore.Client.Tests`, `tests/Hexalith.EventStore.Testing.Tests`, and optionally Hexalith.Tenants focused test projects.

## Out of Scope

- Moving gateway command/query DTOs or broad gateway fake ownership; Story 22.1 owns that.
- Moving projection adapter contracts, actor serialization, or query routing model; Story 22.2 owns that.
- Query paging, blank search, filter validation, stale/degraded freshness, and full non-auth query taxonomy; Story 22.4 owns that.
- Redefining non-auth query error taxonomy, query business not-found behavior, projection data-shape errors, malformed query/filter behavior, or projection consistency taxonomy; only authn/authz tenant/RBAC denials are in scope.
- Pub/sub ordering, retry, outbox/drain, and backend deployment matrices; Story 22.5 owns that.
- Stream read/replay APIs and projection rebuild checkpoints; Story 22.6 owns that.
- Payload/snapshot protection hooks, unreadable protected data behavior, crypto-shredding, and redaction across operational surfaces; Stories 22.7a through 22.7d own that.
- Rewriting Hexalith.Tenants aggregates, admin UI tenant management, or Parties repository behavior unless required by a clearly documented public contract mismatch.
- Changing DAPR access-control policies or AppHost topology unless the selected validator integration requires a new service invocation path.

## References

- `_bmad-output/planning-artifacts/epics.md#Story 22.3: Gateway-Owned Tenant and RBAC Enforcement`
- `_bmad-output/planning-artifacts/prd.md#Public Gateway and Downstream Integration Contracts - v1.1 (FR83-FR104)`
- `_bmad-output/planning-artifacts/architecture.md#ADR-P8: EventStore Owns Gateway Tenant/RBAC Enforcement`
- `_bmad-output/planning-artifacts/architecture.md#Downstream Authorization Boundary`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-parties-integration-contract-gaps.md`
- `_bmad-output/implementation-artifacts/22-1-gateway-command-query-contract-closure-and-package-docs.md`
- `_bmad-output/implementation-artifacts/22-2-projection-adapter-contract-and-generic-query-actor-model.md`
- `_bmad-output/project-context.md`
- `_bmad-output/process-notes/story-creation-lessons.md#L08 - Party Review Vs. Elicitation`
- `docs/guides/security-model.md`
- `docs/reference/command-api.md`
- `docs/reference/query-api.md`
- `Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Enums/TenantStatus.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Enums/TenantRole.cs`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-12T19:36:49Z - Pre-dev hardening preflight passed via `_bmad-output/process-notes/predev-preflight-latest.json`.
- 2026-05-12T21:44:06+02:00 - Story creation context gathered from Epic 22, PRD FR90-FR92, architecture ADR-P8 and downstream authorization boundary, Stories 22.1 and 22.2, EventStore authorization adapters, MediatR authorization behavior, command/query controllers, ProblemDetails handlers, Hexalith.Tenants public lifecycle/role surfaces, recent commits, project context, and lessons ledger.
- 2026-05-13T07:19:42+02:00 - Party-mode review completed with John, Winston, Amelia, and Murat; applied bounded story hardening for validator contracts, authorization ordering, typed reason-code taxonomy, Tenants integration precedence, claims fallback fencing, validation endpoint compatibility, client/testing parity, package dependency guardrails, and evidence requirements.
- 2026-05-13T12:03:15+02:00 - Advanced elicitation completed in two batches; applied bounded hardening for immutable authorization context, tenant-source conflict handling, auth-before-cache/ETag/projection lookup ordering, adapter staleness and timeout semantics, safe observability, and fake/client fail-closed parity.
- 2026-05-13T20:43:39+02:00 - Development started. Aspire baseline run succeeded with `EnableKeycloak=false`; resources were healthy for EventStore, Tenants, Admin, sample, DAPR sidecars, `statestore`, and `pubsub`. AppHost was stopped before test builds to release locked assemblies.
- 2026-05-13T22:05:00+02:00 - Implemented typed authorization reason codes, ProblemDetails/preflight/client propagation, query authorization-before-ETag lookup, and DAPR actor adapter reason-code mapping.
- 2026-05-13T22:20:00+02:00 - Final focused validation passed: Server authorization/query/controller slice 233/233, Contracts reason-code slice 22/22, Client gateway slice 20/20, Testing fake/builder slice 14/14, and markdownlint 0 errors.

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, generated API docs, or submodules.
- Party-mode review completed on 2026-05-13 and applied story hardening for the gateway authorization contract, reason-code taxonomy, Tenants integration boundary, validation endpoint behavior, client/testing parity, and focused evidence obligations.
- Advanced elicitation completed on 2026-05-13 and applied story-text hardening only; product code, tests, DAPR/Aspire configuration, generated API docs, sprint status, and submodules were not changed.
- Added `AuthorizationFailureReason` and stable canonical `reasonCode` mapping in Contracts; tenant/RBAC validator results now preserve machine-readable reason codes separately from safe human-readable text.
- Runtime 401/403/503 ProblemDetails now emit stable `reasonCode` metadata, while preserving existing sanitization of actor/DAPR/state-store/aggregate terms and safe `reason` text for backward-compatible clients.
- Command/query preflight validation endpoints continue returning `200 OK` with `isAuthorized=false`; denied responses now include `reasonCode`.
- Query submission now authorizes tenant/RBAC before any `If-None-Match`/ETag lookup or mediator/query-router/projection invocation; the prevalidated immutable context is reused by the MediatR authorization behavior to avoid double validation.
- Selected integration path: documented DAPR actor validator adapter as the approved Tenants-backed runtime boundary. Actor responses carry optional stable reason codes; missing/malformed legacy deny responses stay denied and map to fail-closed fallback codes.
- No AppHost, DAPR access-control, or Hexalith.Tenants runtime code changes were required. The existing Aspire topology already starts the Tenants service and DAPR sidecar; story scope only needed EventStore adapter contracts, docs, and tests.

### File List

- `_bmad-output/implementation-artifacts/22-3-gateway-owned-tenant-and-rbac-enforcement.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/guides/security-model.md`
- `docs/reference/command-api.md`
- `docs/reference/query-api.md`
- `docs/reference/problems/forbidden.md`
- `src/Hexalith.EventStore.Contracts/Authorization/AuthorizationFailureReason.cs`
- `src/Hexalith.EventStore.Contracts/Authorization/AuthorizationFailureReasonExtensions.cs`
- `src/Hexalith.EventStore.Contracts/Problems/GatewayProblemDetailsExtensions.cs`
- `src/Hexalith.EventStore.Contracts/Validation/PreflightValidationResult.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayException.cs`
- `src/Hexalith.EventStore.Server/Actors/Authorization/ActorValidationResponse.cs`
- `src/Hexalith.EventStore.Testing/Builders/EventStoreGatewayExceptionBuilder.cs`
- `src/Hexalith.EventStore/Authentication/ConfigureJwtBearerOptions.cs`
- `src/Hexalith.EventStore/Authorization/ActorRbacValidator.cs`
- `src/Hexalith.EventStore/Authorization/ActorTenantValidator.cs`
- `src/Hexalith.EventStore/Authorization/ClaimsRbacValidator.cs`
- `src/Hexalith.EventStore/Authorization/ClaimsTenantValidator.cs`
- `src/Hexalith.EventStore/Authorization/RbacValidationResult.cs`
- `src/Hexalith.EventStore/Authorization/TenantValidationResult.cs`
- `src/Hexalith.EventStore/Controllers/CommandValidationController.cs`
- `src/Hexalith.EventStore/Controllers/QueriesController.cs`
- `src/Hexalith.EventStore/Controllers/QueryValidationController.cs`
- `src/Hexalith.EventStore/ErrorHandling/AuthorizationExceptionHandler.cs`
- `src/Hexalith.EventStore/ErrorHandling/AuthorizationServiceUnavailableHandler.cs`
- `src/Hexalith.EventStore/ErrorHandling/CommandAuthorizationException.cs`
- `src/Hexalith.EventStore/Pipeline/AuthorizationBehavior.cs`
- `src/Hexalith.EventStore/Pipeline/GatewayAuthorizationContext.cs`
- `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Authorization/AuthorizationFailureReasonTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Problems/GatewayProblemDetailsExtensionsTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Validation/PreflightValidationResultTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Authorization/ActorRbacValidatorTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Authorization/ActorTenantValidatorTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Authorization/AuthorizationResultReasonCodeTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Authorization/AuthorizationServiceUnavailableHandlerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Authorization/TenantsAuthorizationContractMappingTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreAuthorizationRegistrationTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/CommandValidationControllerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/QueryValidationControllerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/AuthorizationExceptionHandlerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj`
- `tests/Hexalith.EventStore.Testing.Tests/Builders/EventStoreGatewayExceptionBuilderTests.cs`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, generated API docs, or submodules.
- YAML validation and whitespace validation passed in this run; repository markdownlint scope passed with `_bmad-output/**` ignored by project configuration.
- Party-mode review completed on 2026-05-13 and is recorded below.
- Party-mode findings were applied only as story-text clarifications; product scope, architecture policy, sprint status, source code, tests, DAPR/Aspire configuration, generated API docs, and submodules were not changed.
- Advanced elicitation completed on 2026-05-13 and is recorded below.
- Advanced elicitation findings were applied only as story-text clarifications; product scope, architecture policy, sprint status, source code, tests, DAPR/Aspire configuration, generated API docs, and submodules were not changed.
- Aspire baseline: `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`; `aspire describe --format Json` showed healthy EventStore, Tenants, Admin, sample, DAPR sidecars, `statestore`, and `pubsub`. AppHost was stopped before final test builds.
- `dotnet test tests\Hexalith.EventStore.Server.Tests --filter "Authorization|CommandValidation|QueryValidation|QueriesController"`: Passed 233/233.
- `dotnet test tests\Hexalith.EventStore.Contracts.Tests --filter "AuthorizationFailureReason|PreflightValidationResult|GatewayProblemDetailsExtensions"`: Passed 22/22.
- `dotnet test tests\Hexalith.EventStore.Client.Tests --filter "EventStoreGatewayClient"`: Passed 20/20.
- `dotnet test tests\Hexalith.EventStore.Testing.Tests --filter "EventStoreGatewayExceptionBuilder|FakeEventStoreGatewayClient"`: Passed 14/14.
- `npx markdownlint-cli2 docs/guides/security-model.md docs/reference/command-api.md docs/reference/query-api.md docs/reference/problems/forbidden.md`: 0 errors.
- Decision table and integration-path note location: `docs/guides/security-model.md` and `docs/reference/problems/forbidden.md`.
- Downstream non-invocation proof: `QueriesControllerTests.Submit_UnauthorizedTenant_DoesNotReadETagOrInvokeMediator` proves tenant denial blocks ETag and mediator/query dispatch; existing authorization behavior/controller tests cover command, query, and preflight denial flow.
- Immutable context proof: `GatewayAuthorizationContext` stores tenant/domain/message/category/aggregate/subject after controller-level query authorization; `AuthorizationBehavior` reuses it only when all fields match the MediatR request.
- Tenants contract evidence: `TenantsAuthorizationContractMappingTests` references `Hexalith.Tenants.Contracts` in tests only and maps active/disabled tenant status plus known roles without adding Tenants runtime dependencies to Contracts, Client, Testing, or EventStore production packages.
- Story 22.4 exclusions preserved: non-auth query validation, missing projection, malformed query/filter, projection consistency, and business/query taxonomy remain out of scope.

### Review Findings

Code review run: 2026-05-14 — Claude Sonnet 4.6 (3-layer: Blind Hunter, Edge Case Hunter, Acceptance Auditor)

**Decision-Needed:**
- [x] [Review][Decision] GlobalAdmin with empty tenantId — resolved as regression; GlobalAdmin bypass moved before empty-tenantId guard. [`src/Hexalith.EventStore/Authorization/ClaimsTenantValidator.cs`]

**Patches:**
- [x] [Review][Patch] Renamed `CommandType` → `MessageType` in `CommandAuthorizationException` and updated log in `AuthorizationExceptionHandler`. Tests updated. [`src/Hexalith.EventStore/ErrorHandling/CommandAuthorizationException.cs`, `AuthorizationExceptionHandler.cs`]
- [x] [Review][Patch] `TenantValidationResult.Denied` default changed from `TenantUnavailable` to `PrincipalNotMember`. [`src/Hexalith.EventStore/Authorization/TenantValidationResult.cs`]
- [x] [Review][Patch] `FakeTenantValidatorActor` / `FakeRbacValidatorActor` default changed from allow to deny. [`src/Hexalith.EventStore.Testing/Fakes/FakeTenantValidatorActor.cs`, `FakeRbacValidatorActor.cs`]
- [x] [Review][Patch] Added gateway-level `FakeTenantValidator`/`FakeRbacValidator` with default-deny in `tests/Hexalith.EventStore.Server.Tests/Fakes/`. (Note: placed in server tests project, not in Testing library, as gateway interfaces live in the API project not accessible from the Testing NuGet package.)
- [x] [Review][Patch] Added `AuthorizationBehavior_TenantDenied_HandlerDelegateNotInvoked` test proving handler not called when auth fails. [`tests/Hexalith.EventStore.Server.Tests/Pipeline/AuthorizationBehaviorTests.cs`]
- [x] [Review][Patch] Added 4 actor reason-code test cases (`tenant_not_found`, `tenant_suspended`, `tenant_stale`, `tenant_ambiguous`) to `ActorTenantValidatorTests`. [`tests/Hexalith.EventStore.Server.Tests/Authorization/ActorTenantValidatorTests.cs`]
- [x] [Review][Patch] Added `correlationId` to `AuthorizationServiceUnavailableHandler` 503 ProblemDetails; removed misleading UX-DR2 comment. Updated test. [`src/Hexalith.EventStore/ErrorHandling/AuthorizationServiceUnavailableHandler.cs`]
- [x] [Review][Patch] Added warning log for unrecognized actor reason codes in `ActorTenantValidator` and `ActorRbacValidator`. [`src/Hexalith.EventStore/Authorization/ActorTenantValidator.cs`, `ActorRbacValidator.cs`]
- [x] [Review][Patch] Added `private const string StartupValidatorTypeName` in `EventStoreAuthorizationRegistrationTests` and replaced all 3 hardcoded string usages. [`tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreAuthorizationRegistrationTests.cs`]
- [x] [Review][Patch] Added `Submit_UnauthorizedRbac_ThrowsWithCorrectReasonCode` test for RBAC-denied query path. [`tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs`]

**Deferred:**
- [x] [Review][Defer] Non-HTTP context bypass in `AuthorizationBehavior` allows full auth skip for internal MediatR calls — pre-existing architectural trust boundary, intentional design [`src/Hexalith.EventStore/Pipeline/AuthorizationBehavior.cs:40`] — deferred, pre-existing
- [x] [Review][Defer] New `TenantStatus` values added to Hexalith.Tenants submodule will silently map to `TenantAmbiguous` via `_` default arm — submodule only has Active/Disabled today; revisit when new statuses are added [`tests/.../TenantsAuthorizationContractMappingTests.cs`] — deferred, pre-existing
- [x] [Review][Defer] `AuthorizationServiceUnavailableHandler` emits `Retry-After` in both HTTP header and ProblemDetails extension — intentional belt-and-suspenders for RFC compliance, ordering invariant undocumented — deferred, pre-existing
- [x] [Review][Defer] `AuthorizationExceptionHandler.SanitizeForbiddenTerms` regex ordering is load-bearing but undocumented — pre-existing [`src/Hexalith.EventStore/ErrorHandling/AuthorizationExceptionHandler.cs`] — deferred, pre-existing
- [x] [Review][Defer] `CommandApiAuthorizationStartupValidator` validates DI wiring but does not enforce actor-backed mode in production — a misconfigured deployment silently falls back to claims-only — deferred, pre-existing architectural gap
- [x] [Review][Defer] All 401 responses emit `authentication_required` regardless of expired vs missing token — `AuthorizationFailureReason` enum has no `TokenExpired` member; extends beyond story 22.3 scope — deferred, out of scope

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-12 | 0.1 | Created ready-for-dev story for gateway-owned tenant and RBAC enforcement. | Codex automation |
| 2026-05-13 | 0.2 | Applied party-mode review hardening for validator contracts, authorization ordering, reason-code taxonomy, Tenants integration precedence, claims fallback fencing, validation endpoint compatibility, client/testing parity, and evidence requirements. | Codex automation |
| 2026-05-13 | 0.3 | Applied advanced elicitation hardening for immutable authorization context, tenant conflicts, auth-before-cache ordering, adapter failure semantics, safe observability, and fake/client fail-closed parity. | Codex automation |
| 2026-05-13 | 1.0 | Implemented gateway-owned tenant/RBAC reason-code contract, query auth-before-ETag enforcement, actor adapter mapping, client/testing support, docs, and focused validation evidence. | GPT-5 Codex |
| 2026-05-14 | 1.1 | Code review by Claude Sonnet 4.6: 1 decision-needed, 10 patches, 6 deferred identified. Story moved to in-progress. | Claude Sonnet 4.6 |
| 2026-05-14 | 1.2 | All review patches applied: DN-1 regression fixed, P1–P10 all patched. 1879 tests pass (4 pre-existing failures unrelated to story scope). Story moved to done. | Claude Sonnet 4.6 |

## Party-Mode Review

- Date/time: 2026-05-13T07:19:42+02:00
- Selected story key: `22-3-gateway-owned-tenant-and-rbac-enforcement`
- Command/skill invocation used:
  `/bmad-party-mode 22-3-gateway-owned-tenant-and-rbac-enforcement; review;`
- Participating BMAD agents: John (Product Manager), Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary:
  - All four reviewers recommended `needs-story-update` before development because the story left too much room for partial enforcement, inconsistent typed outcomes, unsafe claims fallback, vague Tenants integration choices, validation endpoint drift, and weak non-invocation/test evidence.
  - Product review emphasized Tenants public-contract precedence, frozen validation endpoint semantics, caller-action/retryability grouping for reason codes, claims fallback fencing, client/testing artifact names, and concrete evidence artifacts.
  - Architecture review emphasized validator contract tables, explicit command/query authorization ordering, package dependency guardrails, canonical reason-code names, Tenants integration fallback conditions, and the Story 22.4 query taxonomy boundary.
  - Engineering review emphasized request-path interception maps, a single typed authorization failure model, exact ProblemDetails/preflight mappings, non-invocation assertions, client result behavior, and deferring caching/retry/non-auth query details.
  - Test architecture review emphasized fail-closed proof matrices across command/query/validation/client/fake paths, contract-level ProblemDetails assertions, Tenants outage/staleness fixtures, validation endpoint regression tests, no-leak assertions, and evidence granularity.
- Changes applied:
  - Added Gateway Authorization Boundary Contract bullets for Tenants public client/query precedence, actor adapter fallback rules, claims fallback fencing, and Story 22.4 non-auth query boundary.
  - Hardened AC1, AC4, AC5, AC6, and AC7 with deployed/runtime fail-closed behavior, public reason-code documentation, validation endpoint compatibility, package ownership constraints, and evidence artifacts.
  - Expanded ST0 with validator contract, request-path interception, authorization ordering, and decision-table requirements.
  - Expanded ST1-ST6 with typed reason-code model, canonical reason-code examples, mapping tables, Tenants integration ADR note, claims fallback configuration guard, downstream non-invocation assertions, ProblemDetails contract tests, validation endpoint regression tests, client/fake parity, and package dependency checks.
  - Expanded ST7 and Out of Scope to make evidence obligations and Story 22.4 exclusions explicit.
- Findings deferred:
  - Full localization strategy and end-user UX copy polish.
  - Tenants query optimization, caching, retry policy, and broad Aspire/e2e runtime suites unless selected implementation requires them.
  - Non-auth query validation, missing projection, malformed query/filter, projection consistency, and business/query taxonomy decisions for Story 22.4.
  - Concrete Tenants transport implementation choice if public client/query availability must be confirmed during development; this story now requires the integration-path note and fallback rules before coding completes.
- Final recommendation: ready-for-dev after applied story updates.

## Advanced Elicitation

- Date/time: 2026-05-13T12:03:15+02:00
- Selected story key: `22-3-gateway-owned-tenant-and-rbac-enforcement`
- Command/skill invocation used:
  `/bmad-advanced-elicitation 22-3-gateway-owned-tenant-and-rbac-enforcement`
- Batch 1 method names:
  - Red Team vs Blue Team
  - Security Audit Personas
  - Failure Mode Analysis
  - Self-Consistency Validation
  - Architecture Decision Records
- Reshuffled Batch 2 method names:
  - Pre-mortem Analysis
  - First Principles Analysis
  - Challenge from Critical Perspective
  - Comparative Analysis Matrix
  - Stakeholder Round Table
- Findings summary:
  - The story had the right tenant/RBAC boundary, but implementation could still accidentally authorize after query cache/ETag/projection lookup paths, leak resource existence, or allow mutable tenant/subject/message context after validation.
  - Tenant-source disagreement and malformed or missing subject/tenant inputs needed explicit fail-closed handling rather than relying on later validator or downstream behavior.
  - Tenants-backed adapters needed clearer timeout, cancellation, stale/cache, ambiguous, unavailable, and no-ad-hoc-retry semantics to avoid claims fallback or cross-tenant state reuse.
  - ProblemDetails, logs, telemetry, client exceptions, validation responses, and testing fakes needed the same deterministic reason-code contract, with fake defaults denying rather than implicitly allowing incomplete setup.
  - Evidence requirements needed to force proof that authorization runs before not-modified/cache/projection/stream existence decisions and that observability remains useful without exposing sensitive authorization inputs.
- Changes applied:
  - Added Gateway Authorization Boundary Contract bullets for immutable authorization context, tenant-source mismatch handling, and authorization-before-cache/ETag/projection-state ordering.
  - Hardened AC2-AC6 with pre-resource-lookup blocking, tenant mismatch reasons, unknown role/permission deny behavior, safe logs/telemetry, fake default deny behavior, and adapter staleness/cache semantics.
  - Expanded ST0-ST7 with immutable-context tables, tenant-source conflict inventory, canonical `authentication_required`, `subject_missing`, and `tenant_mismatch` reason codes, legacy/malformed response fail-closed tests, adapter timeout/staleness rules, auth-before-ETag/cache/projection evidence, safe observability evidence, and mutually exclusive registration checks.
  - Added implementation traps against pre-auth not-modified/cache/projection/resource existence decisions and post-auth mutation/recomputation of tenant, subject, message, aggregate, projection/query, correlation, or cancellation context.
- Findings deferred:
  - Exact Tenants transport implementation and cache/staleness policy remain a development-time decision, bounded by the required integration-path note and fail-closed adapter semantics.
  - Full localization/end-user wording remains deferred; reason codes, type URIs, retryability, caller-action category, and safe display metadata are the stable contract.
  - Non-auth query validation, projection consistency, missing projection, malformed query/filter, and business/query taxonomy remain Story 22.4 scope.
  - Broad runtime Aspire/e2e security proof remains optional unless AppHost, DAPR access control, or selected Tenants transport wiring changes.
- Final recommendation: ready-for-dev
