# Story 5.2: Claims-Based Command Authorization

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform developer**,
I want command submissions authorized based on JWT claims for tenant, domain, and command type,
So that consumers can only submit commands they are permitted to.

**Note:** This is a **verification story**. The claims-based command authorization infrastructure is already fully implemented (extracted and refactored in Story 17-1). The dev agent's job is to verify correctness of all authorization layers, fill test gaps, and document the verification results -- not to build new authorization infrastructure. If verification uncovers a non-trivial issue (architectural flaw, security vulnerability, or change requiring > 30 minutes of work), STOP and escalate to the user rather than fixing inline.

## Acceptance Criteria

1. **Command tenant validated against consumer's authorized tenants** -- Given an authenticated consumer with `eventstore:tenant` claims, When they submit a command, Then `AuthorizationBehavior` (MediatR Layer 4) delegates to `ITenantValidator.ValidateAsync` which checks the command's tenant against the consumer's tenant claims (FR31). Tenant comparison is case-SENSITIVE (`StringComparison.Ordinal`). A consumer with no tenant claims is denied.

2. **Command type authorized via domain and permission claims** -- Given an authenticated consumer with `eventstore:domain` and `eventstore:permission` claims, When they submit a command, Then `AuthorizationBehavior` delegates to `IRbacValidator.ValidateAsync` which checks: (a) domain authorization -- if domain claims exist, command domain must match (case-insensitive); no domain claims = all domains authorized; (b) permission authorization -- if permission claims exist, must have `commands:*` (wildcard), `command:submit` (category), or exact `messageType` match (case-insensitive); no permission claims = all types authorized (FR31).

3. **Unauthorized commands rejected with 403 before processing** -- Given an authorization failure (tenant mismatch, wrong domain, wrong permission), When `AuthorizationBehavior` throws `CommandAuthorizationException`, Then `AuthorizationExceptionHandler` returns HTTP 403 Forbidden with RFC 7807/9457 ProblemDetails body (`application/problem+json`), `type` = `ProblemTypeUris.Forbidden`, and `correlationId` + `tenantId` extensions (FR32, D5).

4. **Actor-level tenant validation provides defense-in-depth** -- Given a command reaching the actor processing pipeline, When `AggregateActor` processes the command, Then Step 2 (`TenantValidator`) validates command tenant matches actor identity (`{tenant}:{domain}:{aggregateId}`) BEFORE any state rehydration (FR33, SEC-2). Mismatch throws `TenantMismatchException`. This is Layer 5, defense-in-depth behind Layer 4.

5. **Authorization failure audit logging** -- Given an authorization failure, When `AuthorizationBehavior` denies the request, Then a structured Warning-level security audit log is emitted with: `SecurityEvent=AuthorizationDenied`, `CorrelationId`, `CausationId`, `TenantClaims`, `Tenant`, `Domain`, `MessageType`, `Reason`, `SourceIp`, `FailureLayer=MediatR.AuthorizationBehavior`. JWT token content and event payloads NEVER appear in logs (SEC-5).

6. **Authorization success logging** -- Given a successful authorization, When `AuthorizationBehavior` passes, Then a Debug-level log is emitted with: `CorrelationId`, `CausationId`, `Tenant`, `Domain`, `MessageType`, `Stage=AuthorizationPassed`.

7. **Forbidden term sanitization in error responses** -- Given an authorization failure originating from actor-based validators, When `AuthorizationExceptionHandler` creates the ProblemDetails response, Then internal terms (`actor`, `aggregate`, `event stream`, `event store`, `DAPR`, `sidecar`, `state store`, `pub/sub`) are sanitized from the `detail` field (UX-DR6). Client-facing responses must not reveal internal architecture.

8. **Claims-based vs actor-based validator factory** -- Given `EventStoreAuthorizationOptions` bound to `EventStore:Authorization`, When `TenantValidatorActorName` or `RbacValidatorActorName` is null, Then claims-based validators are used (default). When non-null, actor-based validators are used. Mixed configurations (claims tenant + actor RBAC, or vice versa) are supported. `CommandApiAuthorizationStartupValidator` validates configuration at startup.

9. **MediatR pipeline ordering** -- Given the MediatR pipeline is configured, When a command flows through behaviors, Then ordering is: `LoggingBehavior` -> `ValidationBehavior` -> `AuthorizationBehavior` -> `SubmitCommandHandler`. Authorization runs AFTER validation (invalid commands rejected cheaply) and BEFORE the handler (unauthorized commands never reach actors).

10. **All existing tests pass** -- All Tier 1 (baseline: >= 659) and Tier 2 (baseline: >= 1447) tests continue to pass. No regressions from any verification or gap-closure changes.

### Definition of Done

This story is complete when: all 10 ACs are verified as implemented and tested, claims-based command authorization validates tenant/domain/permission claims at MediatR Layer 4, actor-level tenant validation provides SEC-2 defense-in-depth at Layer 5, 403 ProblemDetails responses sanitize internal terms, and no regressions exist in Tier 1 or Tier 2 suites.

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and baseline (BLOCKING)
  - [ ] 0.1 Run all Tier 1 tests -- confirm all pass (baseline: >= 659)
  - [ ] 0.2 Run Tier 2 tests `Hexalith.EventStore.Server.Tests` -- confirm pass count (baseline: >= 1447)
  - [ ] 0.3 Read `AuthorizationBehavior.cs` -- verify tenant-then-RBAC sequential flow, ClaimsPrincipal extraction, `CommandAuthorizationException` throwing (AC #1, #2, #3, #5, #6)
  - [ ] 0.4 Read `ClaimsTenantValidator.cs` -- verify case-SENSITIVE tenant matching, no-tenant-claims denial (AC #1)
  - [ ] 0.5 Read `ClaimsRbacValidator.cs` -- verify domain (case-insensitive) + permission (wildcard/category/exact) checks (AC #2)
  - [ ] 0.6 Read `AuthorizationExceptionHandler.cs` -- verify 403 ProblemDetails, forbidden term sanitization (AC #3, #7)
  - [ ] 0.7 Read `TenantValidator.cs` (Server/Actors/) -- verify actor-level SEC-2 defense-in-depth (AC #4)
  - [ ] 0.8 Read `ServiceCollectionExtensions.cs` -- verify DI factory delegates, pipeline ordering (AC #8, #9)
  - [ ] 0.9 Inventory existing test files and counts:
    - `AuthorizationBehaviorTests.cs` -- count tests
    - `ClaimsTenantValidatorTests.cs` -- count tests
    - `ClaimsRbacValidatorTests.cs` -- count tests
    - `AuthorizationExceptionHandlerTests.cs` -- count tests
    - `CommandApiAuthorizationRegistrationTests.cs` -- count tests
    - `ActorTenantValidatorTests.cs` -- count tests
    - `ActorRbacValidatorTests.cs` -- count tests
    - `AuthorizationServiceUnavailableExceptionTests.cs` -- count tests
    - `AuthorizationServiceUnavailableHandlerTests.cs` -- count tests

- [ ] Task 1: Verify tenant authorization (AC: #1)
  - [ ] 1.1 Confirm `AuthorizationBehavior.Handle` extracts tenant from `SubmitCommand.Tenant` and passes to `ITenantValidator.ValidateAsync`
  - [ ] 1.2 Confirm `ClaimsTenantValidator` reads `eventstore:tenant` claims, filters whitespace-only, and uses `StringComparison.Ordinal` (case-SENSITIVE)
  - [ ] 1.3 Confirm no-tenant-claims returns `Denied("No tenant authorization claims found. Access denied.")`
  - [ ] 1.4 Confirm multiple tenant claims -- any one matching authorizes access
  - [ ] 1.5 If any tenant validation logic is missing or incorrect, fix it

- [ ] Task 2: Verify RBAC authorization (AC: #2)
  - [ ] 2.1 Confirm `AuthorizationBehavior.Handle` extracts domain, messageType, and messageCategory from `SubmitCommand` and passes to `IRbacValidator.ValidateAsync`
  - [ ] 2.2 Confirm `ClaimsRbacValidator` domain check: no domain claims = pass; has domain claims = require match (case-insensitive, `OrdinalIgnoreCase`)
  - [ ] 2.3 Confirm `ClaimsRbacValidator` permission check for "command" category: accepts `commands:*`, `command:submit`, or exact messageType match (all case-insensitive)
  - [ ] 2.4 Confirm `ClaimsRbacValidator` permission check for "query" category: accepts `queries:*`, `query:read`, legacy `command:query`, or exact messageType match
  - [ ] 2.5 Confirm no permission claims = all message types authorized (no enforcement)
  - [ ] 2.6 Confirm `AuthorizationConstants` defines `SubmitPermission`, `WildcardPermission`, `ReadPermission`, `QueryWildcardPermission`
  - [ ] 2.7 If any RBAC logic is missing or incorrect, fix it

- [ ] Task 3: Verify error response handling (AC: #3, #7)
  - [ ] 3.1 Confirm `AuthorizationExceptionHandler.TryHandleAsync` returns 403 with ProblemDetails, `type = ProblemTypeUris.Forbidden`, `title = "Forbidden"`
  - [ ] 3.2 Confirm content type is `application/problem+json`
  - [ ] 3.3 Confirm ProblemDetails extensions include `correlationId` and `tenantId`
  - [ ] 3.4 Confirm `SanitizeForbiddenTerms` removes/replaces: `by actor` -> removed, `actor` -> "service", `aggregate` -> "entity", `event stream` -> "data", `event store` -> "service", `DAPR` -> "infrastructure", `sidecar` -> "service", `state store` -> "storage", `pub/sub` -> "messaging"
  - [ ] 3.5 Confirm `CreateClientDetail` avoids duplicating tenant in reason when it's already present (case-insensitive check)
  - [ ] 3.6 Confirm `AuthorizationServiceUnavailableHandler` returns 503 with `Retry-After: 30` header, does NOT expose actor type/ID in response
  - [ ] 3.7 Confirm exception handler ordering in DI: `AuthorizationServiceUnavailableHandler` (503) BEFORE `AuthorizationExceptionHandler` (403)
  - [ ] 3.8 If any error handling is missing or incorrect, fix it

- [ ] Task 4: Verify actor-level defense-in-depth (AC: #4)
  - [ ] 4.1 Confirm `TenantValidator.Validate` in `Server/Actors/` parses actor ID (`{tenant}:{domain}:{aggregateId}`), extracts tenant from first segment
  - [ ] 4.2 Confirm case-SENSITIVE tenant comparison (`StringComparison.Ordinal`) between command tenant and actor tenant
  - [ ] 4.3 Confirm mismatch throws `TenantMismatchException` with Warning-level structured log (EventId 5000)
  - [ ] 4.4 Confirm validation happens in `AggregateActor` Step 2, BEFORE state rehydration (SEC-2)
  - [ ] 4.5 If any actor-level validation is missing or incorrect, fix it

- [ ] Task 5: Verify audit logging (AC: #5, #6)
  - [ ] 5.1 Confirm `AuthorizationBehavior.Log.AuthorizationFailed` (EventId 1021, Warning) includes: SecurityEvent, CorrelationId, CausationId, TenantClaims, Tenant, Domain, MessageType, Reason, SourceIp, FailureLayer
  - [ ] 5.2 Confirm `AuthorizationBehavior.Log.AuthorizationPassed` (EventId 1020, Debug) includes: CorrelationId, CausationId, Tenant, Domain, MessageType
  - [ ] 5.3 Confirm JWT token content never appears in any log message (SEC-5)
  - [ ] 5.4 Confirm `AuthorizationExceptionHandler` logs Warning with SecurityEvent=AuthorizationDenied, correlation ID, tenant, domain, command type
  - [ ] 5.5 If any logging is missing or incorrect, fix it

- [ ] Task 6: Verify DI registration and pipeline ordering (AC: #8, #9)
  - [ ] 6.1 Confirm `ServiceCollectionExtensions.AddCommandApi` registers `EventStoreAuthorizationOptions` bound to `EventStore:Authorization` with `ValidateOnStart()`
  - [ ] 6.2 Confirm factory delegate pattern: null actor name -> claims-based, non-null -> actor-based
  - [ ] 6.3 Confirm `CommandApiAuthorizationStartupValidator` is registered as `IHostedService`
  - [ ] 6.4 Confirm MediatR pipeline ordering: `LoggingBehavior` -> `ValidationBehavior` -> `AuthorizationBehavior` (in `AddOpenBehavior` call order)
  - [ ] 6.5 Confirm middleware ordering in `Program.cs`: `UseAuthentication()` -> `UseRateLimiter()` -> `UseAuthorization()`
  - [ ] 6.6 If any registration or ordering is missing or incorrect, fix it

- [ ] Task 7: Verify and extend test coverage (AC: #10)
  - [ ] 7.1 Review all existing authorization test files (listed in Task 0.9) for completeness against ACs
  - [ ] 7.2 Identify test gaps. Known potential gaps to check:
    - [ ] 7.2.1 `AuthorizationBehavior` unauthenticated user test: verify `user.Identity?.IsAuthenticated != true` throws `CommandAuthorizationException` with "User is not authenticated."
    - [ ] 7.2.2 `SanitizeForbiddenTerms` unit tests for ALL 9 patterns: "by actor", "actor", "aggregate", "event stream", "event store", "DAPR", "sidecar", "state store", "pub/sub" -- some may be missing (check existing coverage)
    - [ ] 7.2.3 `CommandAuthorizationException` constructor variants: parameterless, message-only, message+innerException -- verify property defaults
    - [ ] 7.2.4 `AuthorizationBehavior` authorization success Debug log test: verify EventId 1020 emitted on successful authorization
    - [ ] 7.2.5 MediatR pipeline ordering test: verify behaviors resolve in correct order from DI
    - [ ] 7.2.6 `EnsureReasonNamesTenant` edge cases: null/empty tenant, null/empty reason, reason already containing tenant
  - [ ] 7.3 Add any missing test scenarios identified in 7.2
  - [ ] 7.4 Verify all 9 existing authorization test files pass

- [ ] Task 8: Final verification
  - [ ] 8.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- zero warnings, zero errors
  - [ ] 8.2 Run all Tier 1 tests -- confirm pass count (baseline: >= 659)
  - [ ] 8.3 Run all Tier 2 tests -- confirm pass count (baseline: >= 1447)
  - [ ] 8.4 Confirm all 10 acceptance criteria are satisfied
  - [ ] 8.5 Report final test count delta
  - [ ] 8.6 Write verification report in Completion Notes (test inventory per file, gaps found, new tests added, deviations observed)

**Effort guidance:** If Tasks 1-6 pass verification with no issues found, expect ~1-2 hours total (verification + gap-closure tests). If issues are found, assess scope before fixing -- escalate non-trivial issues per the story note above.

## Dev Notes

### CRITICAL: This is a Verification Story

The claims-based command authorization infrastructure is **already fully implemented**. Story 17-1 extracted authorization logic from inline code into dedicated validator classes (`ClaimsTenantValidator`, `ClaimsRbacValidator`) with interfaces (`ITenantValidator`, `IRbacValidator`), added actor-based alternatives, and established the factory delegate DI pattern. Comprehensive test suites already exist. This story formally verifies the implementation against the Epic 5 acceptance criteria.

### Architecture Compliance

- **Six-Layer Auth Pipeline (layers relevant to this story):**
  - **Layer 3 (Endpoint Authorization):** ASP.NET Core `[Authorize]` attribute on endpoints (basic authenticated check)
  - **Layer 4 (MediatR Authorization):** `AuthorizationBehavior` -- tenant x domain x command type check via `ITenantValidator` + `IRbacValidator` (FR31)
  - **Layer 5 (Actor Tenant Validation):** `TenantValidator` in `AggregateActor` Step 2 -- defense-in-depth, SEC-2, validates before state rehydration (FR33)
  - Layers 1-2 (JWT, Claims Transformation) were verified in Story 5.1
  - Layer 6 (DAPR Access Control) is Story 5.4

- **Two-Tier Authorization Architecture:**
  - **Claims-based (default):** Zero I/O, zero latency, all permissions encoded in JWT token. Uses `eventstore:tenant`, `eventstore:domain`, `eventstore:permission` claims (set by Story 5.1 claims transformation).
  - **Actor-based (configurable):** Delegates to DAPR actors for dynamic authorization (e.g., external RBAC store). Configured via `EventStoreAuthorizationOptions`.

- **Critical Comparison Semantics (intentional asymmetry):**
  - Tenant IDs: Case-SENSITIVE (`StringComparison.Ordinal`) -- system-assigned identifiers
  - Domains: Case-insensitive (`OrdinalIgnoreCase`) -- user-facing names
  - Permissions: Case-insensitive (`OrdinalIgnoreCase`) -- user-facing permission strings

- **SEC-5 Compliance:** JWT token content and event payload data NEVER appear in logs. Only correlation IDs, tenant names, domains, message types, and claim counts.

- **UX-DR6 Compliance:** `AuthorizationExceptionHandler.SanitizeForbiddenTerms` strips internal architecture terms (actor, aggregate, DAPR, etc.) from client-facing ProblemDetails responses.

### Key Source Files

| File | Purpose |
|------|---------|
| `src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationBehavior.cs` | MediatR Layer 4 -- tenant + RBAC authorization |
| `src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationConstants.cs` | Permission constants (command:submit, commands:*, etc.) |
| `src/Hexalith.EventStore.CommandApi/Authorization/ITenantValidator.cs` | Tenant validator interface |
| `src/Hexalith.EventStore.CommandApi/Authorization/IRbacValidator.cs` | RBAC validator interface |
| `src/Hexalith.EventStore.CommandApi/Authorization/ClaimsTenantValidator.cs` | Claims-based tenant validation (default) |
| `src/Hexalith.EventStore.CommandApi/Authorization/ClaimsRbacValidator.cs` | Claims-based RBAC validation (default) |
| `src/Hexalith.EventStore.CommandApi/Authorization/ActorTenantValidator.cs` | Actor-based tenant validation (configurable) |
| `src/Hexalith.EventStore.CommandApi/Authorization/ActorRbacValidator.cs` | Actor-based RBAC validation (configurable) |
| `src/Hexalith.EventStore.CommandApi/Authorization/CommandApiAuthorizationStartupValidator.cs` | Fail-fast startup validation |
| `src/Hexalith.EventStore.CommandApi/Configuration/EventStoreAuthorizationOptions.cs` | Authorization config record |
| `src/Hexalith.EventStore.CommandApi/ErrorHandling/CommandAuthorizationException.cs` | Authorization failure exception |
| `src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationExceptionHandler.cs` | 403 ProblemDetails with forbidden term sanitization |
| `src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationServiceUnavailableHandler.cs` | 503 when actor-based auth is down |
| `src/Hexalith.EventStore.CommandApi/ErrorHandling/ProblemTypeUris.cs` | URI constants (Forbidden, ServiceUnavailable) |
| `src/Hexalith.EventStore.Server/Actors/TenantValidator.cs` | Actor-level SEC-2 defense-in-depth (Layer 5) |
| `src/Hexalith.EventStore.Server/Actors/TenantMismatchException.cs` | Exception for actor-level tenant mismatch |
| `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` | DI registration, factory delegates, pipeline ordering |
| `tests/Hexalith.EventStore.Server.Tests/Pipeline/AuthorizationBehaviorTests.cs` | AuthorizationBehavior tests |
| `tests/Hexalith.EventStore.Server.Tests/Authorization/ClaimsTenantValidatorTests.cs` | Tenant validator tests |
| `tests/Hexalith.EventStore.Server.Tests/Authorization/ClaimsRbacValidatorTests.cs` | RBAC validator tests |
| `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/AuthorizationExceptionHandlerTests.cs` | Exception handler tests |
| `tests/Hexalith.EventStore.Server.Tests/Configuration/CommandApiAuthorizationRegistrationTests.cs` | DI registration tests |
| `tests/Hexalith.EventStore.Server.Tests/Authorization/ActorTenantValidatorTests.cs` | Actor tenant validator tests |
| `tests/Hexalith.EventStore.Server.Tests/Authorization/ActorRbacValidatorTests.cs` | Actor RBAC validator tests |

### Existing Patterns to Follow

- **Verification task structure:** Same as Story 5.1 -- read code first, verify against ACs, then fill test gaps.
- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}` convention.
- **Assertion library:** Shouldly 4.3.0 fluent assertions.
- **Mocking:** NSubstitute 5.3.0 for interface mocks.
- **Test helpers:** `TestLogger<T>` from `AuthorizationBehaviorTests.cs` captures log entries for assertion. `LogEntry` record holds Level + Message.
- **Test principal creation:** Use `CreatePrincipal(tenants:, domains:, permissions:)` helper pattern from existing tests.

### Cross-Story Dependencies

- **Story 5.1 (JWT Authentication & Claims Transformation)** -- PREREQUISITE (review). Produces the `eventstore:tenant`, `eventstore:domain`, `eventstore:permission` claims that this story's authorization consumes.
- **Story 5.3 (Three-Layer Multi-Tenant Data Isolation)** -- depends on tenant validation verified here to enforce data path isolation.
- **Story 5.4 (DAPR Service-to-Service Access Control)** -- Layer 6 auth, depends on the pipeline verified here.
- **Story 5.5 (E2E Security Testing with Keycloak)** -- will exercise the full six-layer pipeline with real OIDC tokens.

### Previous Story Intelligence

**Story 5.1 (JWT Authentication & Claims Transformation)** -- status: review:
- Added 20 gap-closure tests across 3 test files.
- Fixed pre-existing duplicate `backpressure-exceeded` entry in `ErrorReferenceEndpoints.cs`.
- Test baseline after 5.1: Tier 1: 659, Tier 2: 1447 (1427+20 new).
- **Pattern to follow:** Verification-style tasks with clear baseline checks, reading existing code before modifying, structured test additions.
- **Key learning:** Claims transformation produces `eventstore:*` claims that feed directly into the validators verified in this story.

### Git Intelligence

Recent commits (relevant context):
- `687a7e0` -- Merge PR #108: per-aggregate backpressure fix
- `0748651` -- Implement backpressure handling in command API
- Authorization infrastructure was built in Story 17-1 (old numbering)
- Story 5.1 is currently in review status (added 20 auth tests, not yet merged to main)

### Anti-Patterns to Avoid

- **DO NOT rewrite existing authorization code.** Verify and fix gaps only. The authorization infrastructure is production-ready and well-tested.
- **DO NOT modify claims transformation.** That is Story 5.1's scope.
- **DO NOT add Keycloak or real OIDC testing.** That is Story 5.5 (D11). This story uses claims-based unit tests.
- **DO NOT add new NuGet dependencies.** All authorization packages are already referenced.
- **DO NOT create new authorization middleware or handler classes.** All authorization components exist.
- **DO NOT change comparison semantics.** Tenant = Ordinal (case-sensitive), Domain/Permission = OrdinalIgnoreCase. This is intentional.
- **DO NOT log JWT token values or event payload data.** SEC-5 is non-negotiable.
- **DO NOT expose actor/aggregate/DAPR terminology in client-facing responses.** UX-DR6 sanitization is enforced by `AuthorizationExceptionHandler`.

### Testing Standards

- **Framework:** xUnit 2.9.3 / Shouldly 4.3.0 / NSubstitute 5.3.0
- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}`
- **Existing authorization tests span 9 test files** across Pipeline/, Authorization/, ErrorHandling/, Configuration/ directories
- **Tier separation:** Tier 1 (unit, no DAPR) for validator tests. Tier 2 (DAPR slim) for server tests including AuthorizationBehavior. Tier 3 (full Aspire) for integration tests with real HTTP pipeline.
- **LogEntry assertion pattern:** Use `TestLogger<T>` to capture log entries, assert on Level + Message content. Verify absence of JWT/sensitive data.

### Project Structure Notes

Authorization files are correctly organized:
- Pipeline behaviors in `CommandApi/Pipeline/`
- Validator interfaces and implementations in `CommandApi/Authorization/`
- Configuration options in `CommandApi/Configuration/`
- Error handling in `CommandApi/ErrorHandling/`
- Actor-level auth in `Server/Actors/` and `Server/Actors/Authorization/`
- Tests mirror source structure in `Server.Tests/Pipeline/`, `Server.Tests/Authorization/`, `Server.Tests/ErrorHandling/`, `Server.Tests/Configuration/`

### References

- [Source: epics.md#Story-5.2] Claims-Based Command Authorization acceptance criteria
- [Source: prd.md#FR31] Authorized based on tenant/domain/command type claims
- [Source: prd.md#FR32] Unauthorized commands rejected at API gateway
- [Source: prd.md#FR33] Actor-level tenant validation defense-in-depth (SEC-2)
- [Source: architecture.md#Six-Layer-Auth] JWT -> Claims -> Endpoint -> MediatR -> Actor -> DAPR
- [Source: architecture.md#SEC-2] Tenant validation BEFORE state rehydration
- [Source: architecture.md#SEC-5] Event payload data never appears in logs
- [Source: architecture.md#UX-DR6] Forbidden term sanitization in client-facing errors
- [Source: architecture.md#D5] RFC 7807 Problem Details + Extensions
- [Source: 5-1-jwt-authentication-and-claims-transformation.md] Test baseline: Tier 1 659, Tier 2 1447

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
