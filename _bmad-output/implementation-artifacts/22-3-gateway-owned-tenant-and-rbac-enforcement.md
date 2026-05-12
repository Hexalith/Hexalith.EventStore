# Story 22.3: Gateway-Owned Tenant and RBAC Enforcement

Status: ready-for-dev

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
- Claims-based validators may remain the local/dev fallback, but production-grade gateway enforcement must be able to use Hexalith.Tenants-backed validation.
- 401/403 ProblemDetails type URIs, reason-code extension names, and client/fake behavior are public gateway contract surface. Treat changes as SemVer-relevant.
- Story 22.3 may add or harden public tenant/RBAC fake paths in `Hexalith.EventStore.Testing`, but must not reopen Story 22.1's broader gateway fake/builders unless needed for auth coverage.

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

2. **Tenant lifecycle and membership failures fail closed before invocation.**
   - Given tenant data is missing, stale, unavailable, ambiguous, not found, disabled, suspended, or the user is not a member
   - When EventStore evaluates a command or query request
   - Then it blocks the request before `SubmitCommandHandler`, `CommandRouter`, `SubmitQueryHandler`, `QueryRouter`, DAPR domain invocation, or projection actor invocation.
   - And tests prove the downstream domain service/projection adapter is not called for each blocked category.

3. **Role and permission failures fail closed before invocation.**
   - Given the authenticated user lacks the required tenant role, domain access, message-category permission, or specific command/query permission
   - When EventStore evaluates a command or query request
   - Then it returns a stable authorization failure and never invokes downstream processing.
   - And command and query paths are both covered, including `command` vs `query` message-category behavior.

4. **ProblemDetails and reason codes are stable.**
   - Given authentication or authorization fails
   - When the HTTP response is returned
   - Then 401/403 ProblemDetails type URIs and reason-code extension names are stable and documented.
   - And the taxonomy covers authentication required, tenant not found, tenant disabled/suspended, user not a member, insufficient role, insufficient permission, ambiguous authorization state, stale authorization state, and authorization service unavailable.
   - And unavailable validator infrastructure maps to a fail-closed 503 or explicitly approved 403/503 policy, with stable reason code and `Retry-After` behavior documented.
   - And responses do not leak actor type names, DAPR details, state-store keys, tenant membership lists, tokens, payload data, or protected data.

5. **Preflight validation and client/testing contracts align.**
   - Given downstream services call command/query validation endpoints or use gateway fakes
   - When tenant/RBAC denial is simulated
   - Then validation responses, client exceptions/results, and testing fakes expose deterministic reason codes without requiring downstream code to parse free-text details.
   - And Story 22.1 public package ownership remains intact: DTOs in Contracts, HTTP convenience behavior in Client, deterministic fakes/builders in Testing.

6. **Hexalith.Tenants integration is verifiable without submodule shortcuts.**
   - Given Hexalith.Tenants is the tenant authority
   - When EventStore validates authorization
   - Then the integration uses public Tenants contracts/client/query behavior or documented DAPR actor adapter behavior.
   - And tests cover active, disabled/suspended-equivalent, missing/not-found, non-member, insufficient role, insufficient permission, stale/unavailable, and ambiguous responses.
   - And no implementation reads Hexalith.Tenants state-store keys, projection actor state, or internal server aggregates directly from EventStore.

7. **Regression and evidence are recorded before review.**
   - EventStore Server authorization/controller/error-handling focused tests pass.
   - Contracts, Client, and Testing focused tests pass when public DTO/client/fake behavior changes.
   - Hexalith.Tenants contract/client/test-helper evidence is recorded when Tenants integration types are referenced or changed.
   - Documentation validation is run where available.
   - Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story to review.

## Tasks / Subtasks

- [ ] **ST0 - Baseline current authorization boundary and classify gaps.** (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Read this story, Epic 22, PRD FR90-FR92, architecture ADR-P8, Story 22.1, Story 22.2, and `docs/guides/security-model.md` before code edits.
  - [ ] Inventory current API validator boundaries in `src/Hexalith.EventStore/Authorization`.
  - [ ] Inventory current MediatR and controller entry points: `AuthorizationBehavior`, `CommandsController`, `QueriesController`, `CommandValidationController`, and `QueryValidationController`.
  - [ ] Inventory current ProblemDetails handlers and type URI constants.
  - [ ] Inventory Hexalith.Tenants public contracts/client/projection surfaces needed for lifecycle, membership, and role checks.
  - [ ] Record a decision table for claims fallback, actor adapter, Tenants client/query adapter, reason-code taxonomy, and deferred items.

- [ ] **ST1 - Define typed tenant/RBAC authorization outcomes.** (AC: 1, 4, 5)
  - [ ] Add or extend result models so validator outcomes have stable machine-readable reason codes separate from human-readable detail.
  - [ ] Include categories for allowed, authentication-required, tenant-not-found, tenant-disabled, tenant-suspended or equivalent inactive state, user-not-member, insufficient-role, insufficient-permission, stale, ambiguous, and unavailable.
  - [ ] Preserve existing `IsAuthorized` behavior where public or test code depends on it; add compatibility constructors/helpers if needed.
  - [ ] Add focused tests that prove free-text `Reason` is not the only way to identify failures.

- [ ] **ST2 - Implement Hexalith.Tenants-backed validation path or document the approved adapter shape.** (AC: 1, 2, 3, 6)
  - [ ] Prefer an EventStore-side adapter that consumes public Hexalith.Tenants contracts/client/query behavior for tenant lifecycle, membership, and role checks.
  - [ ] If the approved integration path is DAPR actor validation, freeze the actor type names, actor IDs, request/response shapes, timeout/unavailable semantics, and mapping to Tenants state in docs and tests.
  - [ ] Keep claims-based validators as local/dev fallback and explicitly label the gap they cannot close: lifecycle state, stale/ambiguous authority, and role hierarchy from Tenants.
  - [ ] Ensure cancellation tokens flow through external validation calls.
  - [ ] Ensure validator unavailability, null responses, malformed responses, and ambiguous results throw or return unavailable/ambiguous outcomes that fail closed.

- [ ] **ST3 - Enforce command and query pre-invocation blocking.** (AC: 2, 3)
  - [ ] Add focused tests proving denied command authorization stops before `SubmitCommandHandler`, `CommandRouter`, domain service invocation, aggregate actor state access, or command processing side effects.
  - [ ] Add focused tests proving denied query authorization stops before `SubmitQueryHandler`, `QueryRouter`, projection actor invocation, and ETag/projection state access where relevant.
  - [ ] Verify the `HttpContext`-absent internal MediatR bypass is still intentional and cannot be used by external HTTP requests.
  - [ ] Verify command and query validation endpoints either keep `200 OK` preflight semantics with typed reason codes or move only with explicit docs/tests approval.

- [ ] **ST4 - Freeze auth ProblemDetails taxonomy and docs.** (AC: 4, 5)
  - [ ] Add or update `ProblemTypeUris` and/or documented `reasonCode` extension constants for authentication and authorization failures.
  - [ ] Update `AuthorizationExceptionHandler`, `AuthorizationServiceUnavailableHandler`, and any authentication failure hook needed to emit stable ProblemDetails metadata.
  - [ ] Preserve sanitization of internal terms and add tests that internal actor/DAPR/state-store details do not leak.
  - [ ] Update `docs/reference/command-api.md`, `docs/reference/query-api.md`, `docs/reference/problems/forbidden.md`, and `docs/guides/security-model.md`.
  - [ ] Add new or updated problem reference docs for tenant lifecycle and permission reason codes if the docs taxonomy requires separate pages.

- [ ] **ST5 - Align Client and Testing support for tenant/RBAC paths.** (AC: 5)
  - [ ] Update `EventStoreGatewayException` parsing or public client result models if reason-code extensions are added.
  - [ ] Add deterministic fake/builder support in `Hexalith.EventStore.Testing` for tenant/RBAC denial and unavailable paths.
  - [ ] Preserve existing fake command/query request recording and not-modified behavior from Story 22.1.
  - [ ] Add tests for client/fake behavior covering 401, 403 reason-code variants, and authorization service unavailable.

- [ ] **ST6 - Validate Hexalith.Tenants integration and runtime wiring.** (AC: 6, 7)
  - [ ] Add focused unit tests using Hexalith.Tenants contracts/test helpers for active tenant, disabled/inactive tenant, missing tenant, user membership, and role hierarchy.
  - [ ] Add service registration/startup validation tests for selected authorization mode, options, and missing configuration.
  - [ ] If AppHost or DAPR access control changes are required, update only the root-level submodule/apphost wiring needed and record Aspire restart evidence.
  - [ ] If no AppHost changes are needed, explicitly record why current topology is sufficient.

- [ ] **ST7 - Validate and record evidence.** (AC: 7)
  - [ ] Run focused authorization/server tests, starting with `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "Authorization|CommandsControllerTenant|AuthorizationServiceUnavailable|QueryValidation|CommandValidation"`.
  - [ ] Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests` if Contracts reason-code DTOs/constants change.
  - [ ] Run `dotnet test tests/Hexalith.EventStore.Client.Tests` if gateway client ProblemDetails parsing changes.
  - [ ] Run `dotnet test tests/Hexalith.EventStore.Testing.Tests` if fake/builders change.
  - [ ] Run Hexalith.Tenants focused contract/client/testing tests if the submodule is changed or referenced through new integration code.
  - [ ] Run docs/markdown validation where available.
  - [ ] Update Dev Agent Record, File List, Verification Status, and Change Log.

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

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, generated API docs, or submodules.
- Party-mode review has NOT yet been run for this story.
- Advanced elicitation has NOT yet been run for this story.

### File List

- `_bmad-output/implementation-artifacts/22-3-gateway-owned-tenant-and-rbac-enforcement.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, generated API docs, or submodules.
- YAML validation, whitespace validation, and markdown validation pending in this run.
- Party-mode review has NOT yet been run for this story.
- Advanced elicitation has NOT yet been run for this story.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-12 | 0.1 | Created ready-for-dev story for gateway-owned tenant and RBAC enforcement. | Codex automation |
