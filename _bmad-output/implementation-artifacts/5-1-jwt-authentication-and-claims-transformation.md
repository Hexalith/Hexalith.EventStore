# Story 5.1: JWT Authentication & Claims Transformation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an **API consumer**,
I want to authenticate with the Command API using JWT tokens,
So that my identity and tenant context are established before any processing.

**Note:** This is a **verification story**. The JWT authentication and claims transformation code already exists. The dev agent's job is to verify correctness, fill test gaps, and document the verification results -- not to build new auth infrastructure. If verification uncovers a non-trivial issue (architectural flaw, security vulnerability, or change requiring > 30 minutes of work), STOP and escalate to the user rather than fixing inline.

## Acceptance Criteria

1. **JWT signature, expiry, and issuer verified on every request** -- Given a request with a valid JWT bearer token, When the token is validated by `ConfigureJwtBearerOptions`, Then signature, expiry, and issuer are verified before any processing occurs (FR30, NFR10). Both OIDC discovery (production, `Authority` set) and symmetric key (development/testing, `SigningKey` set) modes are supported.

2. **Claims transformed to extract tenant, domain, and permission arrays** -- Given a valid JWT with custom claims (`tenants`, `domains`, `permissions`), When `EventStoreClaimsTransformation` runs, Then claims are normalized to `eventstore:tenant`, `eventstore:domain`, `eventstore:permission` claim types. Supports JSON array and space-delimited string formats. Also supports singular `tenant_id` and `tid` fallback claims for tenant extraction (lines 72-76 in `EventStoreClaimsTransformation.cs`). Transformation is idempotent (skip if already transformed).

3. **Invalid/missing JWT rejected at API gateway** -- Given a request without a JWT or with an invalid JWT (expired, wrong issuer, bad signature), When processed by the auth middleware, Then the request is rejected with HTTP 401 Unauthorized BEFORE entering the processing pipeline (FR32). Response includes RFC 7807 ProblemDetails body and `WWW-Authenticate` header (RFC 6750).

4. **Authentication options validated at startup** -- Given `EventStoreAuthenticationOptions` bound to `Authentication:JwtBearer` configuration section, Then either `Authority` (OIDC) or `SigningKey` (symmetric) must be provided. `SigningKey` must be >= 32 characters for HS256. Validation fails fast at startup via `ValidateEventStoreAuthenticationOptions`.

5. **Auth failure audit logging** -- Given an authentication failure (expired token, invalid signature, missing token), When the failure is handled in JWT events (`OnAuthenticationFailed`, `OnChallenge`), Then a structured Warning-level security audit log is emitted with: `correlationId`, `sourceIp`, `requestPath`, `reason`, `failureLayer`. Token values are NEVER logged (SEC-5).

6. **Claims transformation logging** -- Given a successful claims transformation, When transformation completes, Then a Debug-level log entry is emitted with: `Subject`, `TenantCount`, `DomainCount`. No permission values or token content in logs.

7. **MapInboundClaims disabled** -- Given JWT bearer options configuration, When `ConfigureJwtBearerOptions.Configure` runs, Then `MapInboundClaims = false` to preserve original JWT claim names (avoiding Microsoft namespace mapping that breaks custom claim parsing).

8. **NameIdentifier claim mapped from sub** -- Given a JWT with `sub` claim, When claims transformation runs, Then `ClaimTypes.NameIdentifier` is added from `sub` for ASP.NET Core identity compatibility.

9. **All existing tests pass** -- All Tier 1 (baseline: >= 659) and Tier 2 (baseline: >= 1414 total, or >= 1387 if Story 4.3 is not yet merged) tests continue to pass. No regressions from any verification or gap-closure changes. Expected new test count: +8-13 gap-closure tests (8 from Task 5.3, up to 5 from Task 5.4).

### Definition of Done

This story is complete when: all 9 ACs are verified as implemented and tested, JWT authentication validates tokens on every request, claims transformation normalizes custom claims for downstream authorization, invalid tokens are rejected at the gateway with proper error responses, and no regressions exist in Tier 1 or Tier 2 suites.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and baseline (BLOCKING)
    - [x] 0.1 Run all Tier 1 tests -- confirm all pass (baseline: >= 659)
    - [x] 0.2 Run Tier 2 tests `Hexalith.EventStore.Server.Tests` -- confirm pass count (baseline: >= 1414)
    - [x] 0.3 Read `ConfigureJwtBearerOptions.cs` -- verify JWT validation covers signature, expiry, issuer, audience (AC #1, #7)
    - [x] 0.4 Read `EventStoreClaimsTransformation.cs` -- verify claims normalization for tenants, domains, permissions (AC #2, #6, #8)
    - [x] 0.5 Read `EventStoreAuthenticationOptions.cs` -- verify dual-mode (OIDC/symmetric) config with startup validation (AC #4)
    - [x] 0.6 Read `ConfigureJwtBearerOptionsTests.cs` -- inventory existing test coverage
    - [x] 0.7 Read `EventStoreClaimsTransformationTests.cs` -- inventory existing test coverage

- [x] Task 1: Verify JWT validation implementation (AC: #1, #7)
    - [x] 1.1 Confirm `ConfigureJwtBearerOptions.Configure` sets `ValidateIssuer`, `ValidateAudience`, `ValidateIssuerSigningKey`, `ValidateLifetime` all to `true` on `TokenValidationParameters`.
    - [x] 1.2 Confirm `MapInboundClaims = false` is set (AC #7).
    - [x] 1.3 Confirm OIDC discovery path: when `Authority` is set, `options.Authority` and `RequireHttpsMetadata` are configured.
    - [x] 1.4 Confirm symmetric key path: when `SigningKey` is set (no Authority), `SymmetricSecurityKey` is created from `Encoding.UTF8.GetBytes`.
    - [x] 1.5 Confirm `ClockSkew` is `TimeSpan.FromMinutes(1)`. This is intentional -- tighter than the `Microsoft.IdentityModel` default of 5 minutes for stronger security, validated as sufficient for clock drift in DAPR sidecar environments. Do NOT change to 5 minutes.
    - [x] 1.6 If any validation parameter is missing or incorrect, fix it.

- [x] Task 2: Verify claims transformation implementation (AC: #2, #6, #8)
    - [x] 2.1 Confirm `EventStoreClaimsTransformation.TransformAsync` extracts `tenants` -> `eventstore:tenant` claims.
    - [x] 2.2 Confirm `domains` -> `eventstore:domain` and `permissions` -> `eventstore:permission` extraction.
    - [x] 2.3 Confirm JSON array parsing with graceful fallback to space-delimited strings.
    - [x] 2.4 Confirm idempotency: already-transformed principals are skipped (no duplicate claims).
    - [x] 2.5 Confirm `sub` -> `ClaimTypes.NameIdentifier` mapping (AC #8).
    - [x] 2.6 Confirm Debug-level logging with Subject, TenantCount, DomainCount (AC #6).
    - [x] 2.7 If any transformation is missing or incorrect, fix it.

- [x] Task 3: Verify auth failure handling (AC: #3, #5)
    - [x] 3.1 Confirm `OnAuthenticationFailed` event handler logs structured Warning with security audit fields.
    - [x] 3.2 Confirm `OnChallenge` event handler writes RFC 7807 ProblemDetails response body.
    - [x] 3.3 Confirm `WWW-Authenticate` header is included (RFC 6750 compliance).
    - [x] 3.4 Confirm HTTP 401 status code for all auth failures (missing token, expired, bad signature, wrong issuer).
    - [x] 3.5 Confirm token values are NEVER logged (SEC-5 compliance).
    - [x] 3.6 Confirm `ProblemTypeUris` has appropriate types for authentication failure. **Already exist:** `ProblemTypeUris.AuthenticationRequired` (`<https://hexalith.io/problems/authentication-required>`) and `ProblemTypeUris.TokenExpired` (`<https://hexalith.io/problems/token-expired>`) in `CommandApi/ErrorHandling/ProblemTypeUris.cs` lines 9-10.
    - [x] 3.7 Verify middleware ordering in `Program.cs`: `UseAuthentication()` BEFORE rate limiting so tenant claims are available for partitioning, with `UseAuthorization()` remaining after rate limiting in the current design. Also verify `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` sets the correct default scheme.
    - [x] 3.8 If any error handling is missing or incorrect, fix it.

- [x] Task 4: Verify startup validation (AC: #4)
    - [x] 4.1 Confirm `ValidateEventStoreAuthenticationOptions` validates either `Authority` or `SigningKey` is present.
    - [x] 4.2 Confirm `SigningKey` minimum length validation (>= 32 chars for HS256).
    - [x] 4.3 Confirm options are registered with `.ValidateOnStart()` in `ServiceCollectionExtensions.cs`.
    - [x] 4.4 If any validation is missing or incorrect, fix it.

- [x] Task 5: Verify and extend test coverage (AC: #9)
    - [x] 5.1 Review `ConfigureJwtBearerOptionsTests.cs` -- verify tests cover: OIDC mode, symmetric key mode, MapInboundClaims, all TokenValidationParameters, OnAuthenticationFailed event, OnChallenge event with ProblemDetails.
    - [x] 5.2 Review `EventStoreClaimsTransformationTests.cs` -- verify tests cover: JSON array parsing, space-delimited strings, idempotency, NameIdentifier mapping, empty claims, null principal.
    - [x] 5.3 Add missing test scenarios identified in 5.1/5.2:
        - [x] 5.3.1 OIDC discovery mode configuration test: Authority set, no SigningKey -- verify `options.Authority` is set and no `IssuerSigningKey` on `TokenValidationParameters`
        - [x] 5.3.2 Expired token rejection event test: verify `OnAuthenticationFailed` logs structured Warning with `reason` containing expiry info
        - [x] 5.3.3 Wrong issuer rejection event test: verify `OnAuthenticationFailed` logs structured Warning with issuer mismatch
        - [x] 5.3.4 Missing token challenge response test: verify `OnChallenge` writes 401 with ProblemDetails body (`application/problem+json`) and `WWW-Authenticate` header
        - [x] 5.3.5 ProblemDetails body structure test: verify `type` is `ProblemTypeUris.AuthenticationRequired` or `ProblemTypeUris.TokenExpired`, `status` is 401, `correlationId` included
        - [x] 5.3.6 Claims transformation `tenant_id`/`tid` fallback test: verify singular `tenant_id` claim is extracted to `eventstore:tenant` when `tenants` array claim is absent. Same for `tid`. This IS a real code path (lines 72-76 in `EventStoreClaimsTransformation.cs`)
        - [x] 5.3.7 Algorithm confusion attack test: verify a JWT with `alg: none` (unsigned token) is rejected by the pipeline. `ValidateIssuerSigningKey = true` should handle this, but explicit test coverage is security-critical.
        - [x] 5.3.8 Dual-config precedence test: verify that when BOTH `Authority` AND `SigningKey` are set, `Authority` (OIDC) takes precedence and `SigningKey` is ignored. This is the current behavior at `ConfigureJwtBearerOptions.cs:52-60` (if/else). Operators WILL hit this edge case.
    - [x] 5.4 Verify `EventStoreAuthenticationOptions` validation tests exist. Add any missing scenarios:
        - [x] 5.4.1 Test default values
        - [x] 5.4.2 Test validation rejects missing both Authority and SigningKey
        - [x] 5.4.3 Test validation rejects short SigningKey (< 32 chars)
        - [x] 5.4.4 Test validation accepts Authority-only config
        - [x] 5.4.5 Test validation accepts SigningKey-only config

- [x] Task 6: Final verification
    - [x] 6.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- zero warnings, zero errors
    - [x] 6.2 Run all Tier 1 tests -- confirm pass count (baseline: >= 659)
    - [x] 6.3 Run all Tier 2 tests -- confirm pass count (baseline: >= 1414)
    - [x] 6.4 Confirm all 9 acceptance criteria are satisfied
    - [x] 6.5 Report final test count delta
    - [x] 6.6 Write verification report in Completion Notes (test inventory per file, gaps found, new tests added, deviations observed)

**Effort guidance:** If Tasks 1-4 pass verification with no issues found, expect ~1.5 hours total (verification + gap-closure tests). If issues are found, assess scope before fixing -- escalate non-trivial issues per the story note above.

## Dev Notes

### CRITICAL: This is a Verification Story

The JWT authentication and claims transformation infrastructure is **already implemented**. This is a verification story (similar to Story 4.2) that confirms correctness, fills any test gaps, and documents the implementation state. The architecture document D11 notes: "Story 5.1 has 14 passing static YAML validation tests but lacks runtime verification." Runtime verification with Keycloak (OIDC tokens) is Story 5.5 -- NOT this story. This story verifies the code-level implementation is correct and complete.

### Architecture Compliance

- **Six-Layer Auth Pipeline (layers relevant to this story):**
    - **Layer 1 (JWT Validation):** `ConfigureJwtBearerOptions` -- signature, expiry, issuer verification (FR30, NFR10)
    - **Layer 2 (Claims Transformation):** `EventStoreClaimsTransformation` -- normalize JWT claims to `eventstore:*` namespace
    - Layers 3-6 are covered by Stories 5.2 (authorization), 5.3 (data isolation), 5.4 (DAPR access control)

- **SEC-5 Compliance:** Event payload data and JWT token values NEVER appear in logs. Only envelope metadata and claim counts.

- **Dual-Mode Authentication:** OIDC discovery (production) vs. symmetric key HS256 (development/testing). Both paths must validate signature, expiry, issuer, audience. When both `Authority` and `SigningKey` are configured, `Authority` takes precedence (if/else at `ConfigureJwtBearerOptions.cs:52-60`).

- **JWT Auth is CommandApi-Only:** The Server package has NO auth middleware. Security depends on DAPR access control (D4) ensuring only CommandApi can invoke actor services. Direct actor invocation bypasses all six auth layers. This is by design -- verified via DAPR access control policies in Story 5.4.

- **OIDC Issuer Matching:** When `Authority` is set (production/Keycloak), `ValidIssuer` must exactly match the `iss` claim in the JWT. For Keycloak, this includes the realm path (e.g., `https://keycloak.example.com/realms/hexalith`). Setting `Issuer` to just the base URL without `/realms/{realm}` is a common misconfiguration that causes silent 401 rejections.

- **Rule 16:** E2E security tests use real Keycloak OIDC tokens (Story 5.5). This story's tests use symmetric key JWT (HS256) via `TestJwtTokenGenerator` for fast unit/integration tests. This is correct and expected per Rule 16.

### Key Source Files

| File                                                                                           | Purpose                                             |
| ---------------------------------------------------------------------------------------------- | --------------------------------------------------- |
| `src/Hexalith.EventStore.CommandApi/Authentication/ConfigureJwtBearerOptions.cs`               | JWT validation configuration (OIDC + symmetric key) |
| `src/Hexalith.EventStore.CommandApi/Authentication/EventStoreClaimsTransformation.cs`          | Claims normalization to eventstore:\* namespace     |
| `src/Hexalith.EventStore.CommandApi/Authentication/EventStoreAuthenticationOptions.cs`         | Auth config record with startup validation          |
| `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs`                 | DI registration for auth services                   |
| `src/Hexalith.EventStore.CommandApi/ErrorHandling/ProblemTypeUris.cs`                          | Problem type URIs for error responses               |
| `tests/Hexalith.EventStore.Server.Tests/Authentication/ConfigureJwtBearerOptionsTests.cs`      | JWT configuration tests                             |
| `tests/Hexalith.EventStore.Server.Tests/Authentication/EventStoreClaimsTransformationTests.cs` | Claims transformation tests                         |

### Existing Patterns to Follow

- **Options record:** Follow `EventStoreAuthenticationOptions` pattern -- record with init-only properties, `IValidateOptions<T>` validator.
- **Claims normalization:** Uses `eventstore:tenant`, `eventstore:domain`, `eventstore:permission` claim types (defined as `internal const` in `EventStoreClaimsTransformation`).
- **JWT event handlers:** `OnAuthenticationFailed` for structured logging, `OnChallenge` for ProblemDetails response body writing.
- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}` convention.
- **Assertion library:** Shouldly 4.3.0 fluent assertions.

### Cross-Story Dependencies

- **Story 5.2 (Claims-Based Command Authorization)** -- NEXT. Depends on this story's claims transformation producing correct `eventstore:*` claims for `AuthorizationBehavior` to consume. Authorization behavior, tenant/RBAC validators already exist but will be formally verified in Story 5.2.
- **Story 5.3 (Three-Layer Multi-Tenant Data Isolation)** -- depends on tenant claims being correctly extracted here.
- **Story 5.5 (E2E Security Testing with Keycloak)** -- depends on the OIDC discovery path verified here. Will add runtime verification with real IdP tokens (D11).

### Previous Story Intelligence

**Story 4.3 (Per-Aggregate Backpressure)** -- status: review:

- Added `BackpressureOptions` to `AggregateActor` constructor, updated 15 test files (27 constructor call sites).
- Test baseline after 4.3: Tier 1: 659, Tier 2: 1414 (1387+27 new).
- **Pattern to follow:** Verification-style tasks with clear baseline checks, reading existing code before modifying, structured test additions.
- **Baseline note:** Tier 2 baseline is 1414 if Story 4.3 is merged, or 1387 if not. Check actual count in Task 0.2 and use that as your baseline.

### Git Intelligence

Recent commits (relevant context):

- `2b71890` -- Merge PR #106: Story 4.2 resilient publication verification
- `e2bc377` -- Story 4.2 verification complete, 5 code review patches
- Auth infrastructure was built in earlier epics under old numbering (pre-migration to new epic structure)
- Integration tests exist: `JwtAuthenticationIntegrationTests.cs`, `AuthorizationIntegrationTests.cs` (Tier 3)

### Anti-Patterns to Avoid

- **DO NOT rewrite existing auth code.** Verify and fix gaps only. The auth infrastructure is production-ready.
- **DO NOT add Keycloak or real OIDC testing.** That is Story 5.5 (D11). This story uses symmetric key JWT (HS256) for fast tests.
- **DO NOT modify authorization behavior, tenant validators, or RBAC validators.** Those are Story 5.2.
- **DO NOT add new NuGet dependencies.** The `Microsoft.AspNetCore.Authentication.JwtBearer` package is already referenced in CommandApi.
- **DO NOT create new authentication middleware or handler classes.** All auth components exist. Verify them, don't duplicate them.
- **DO NOT log JWT token values or event payload data.** SEC-5 is non-negotiable.

### Testing Standards

- **Framework:** xUnit 2.9.3 / Shouldly 4.3.0 / NSubstitute 5.3.0
- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}`
- **Existing auth tests:** `ConfigureJwtBearerOptionsTests.cs` (JWT config), `EventStoreClaimsTransformationTests.cs` (claims), `CommandApiAuthorizationRegistrationTests.cs` (DI registration)
- **Tier separation:** Tier 1 (unit, no DAPR) for auth options/claims tests. Tier 2 (DAPR slim) for server tests. Tier 3 (full Aspire) for integration tests with real HTTP pipeline.

### Project Structure Notes

Auth files are correctly organized:

- Authentication components in `CommandApi/Authentication/`
- Authorization components in `CommandApi/Authorization/` and `Server/Actors/Authorization/`
- Error handling in `CommandApi/ErrorHandling/`
- Tests mirror source structure in `Server.Tests/Authentication/`

### References

- [Source: epics.md#Story-5.1] JWT Authentication & Claims Transformation acceptance criteria
- [Source: prd.md#FR30] API consumer can authenticate with JWT tokens
- [Source: prd.md#FR32] Unauthorized commands rejected at API gateway
- [Source: prd.md#NFR10] JWT tokens validated for signature, expiry, issuer on every request
- [Source: architecture.md#D11] E2E Security Testing Infrastructure (Keycloak) -- Story 5.5 scope
- [Source: architecture.md#SEC-5] Event payload data never appears in logs
- [Source: architecture.md#Six-Layer-Auth] JWT -> Claims -> Endpoint -> MediatR -> Actor -> DAPR
- [Source: architecture.md#Rule-16] E2E tests use real Keycloak tokens; unit tests use HS256
- [Source: 4-3-per-aggregate-backpressure.md] Test baseline: Tier 1 659, Tier 2 1414

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Pre-existing bug: duplicate "backpressure-exceeded" in ErrorReferenceEndpoints.cs (lines 75-78 and 90-93) caused 5 test failures. Removed first duplicate to fix.

### Completion Notes List

#### Verification Report

#### Test inventory per file (after gap-closure)

- `ConfigureJwtBearerOptionsTests.cs`: 19 tests (9 existing + 10 new). Covers: OIDC mode config, symmetric key mode config, MapInboundClaims, TokenValidationParameters, OnAuthenticationFailed structured Warning logging (expired, issuer), OnChallenge (missing, expired, invalid signature, invalid issuer, invalid_token error, unknown error, ProblemDetails structure, content type, no correlationId, no forbidden terms), unsigned `alg:none` rejection, dual-config precedence, wrong scheme no-op.
- `EventStoreClaimsTransformationTests.cs`: 12 tests (9 existing + 3 new). Covers: JSON array tenants, single tenant_id, sub->NameIdentifier, existing NameIdentifier no-dup, domains+permissions, no custom claims, idempotency (tenant), idempotency (domain-only), null principal, tid fallback, tenant_id/tid precedence, space-delimited domains.
- `EventStoreAuthenticationOptionsTests.cs`: 8 tests (all new). Covers: default values, missing both Authority+SigningKey, short SigningKey, Authority-only success, SigningKey-only success, missing Issuer, missing Audience, null options.

#### Gaps found during verification

- Pre-existing bug: duplicate "backpressure-exceeded" entry in `ErrorReferenceEndpoints.cs` (Story 4.3 added second entry without removing first). Fixed by removing the first, less detailed duplicate.
- No configuration-level tests existed for `ConfigureJwtBearerOptions` (OIDC mode, symmetric mode, TokenValidationParameters, MapInboundClaims). All added.
- No tests existed for `ValidateEventStoreAuthenticationOptions`. Full test suite created.
- No tests existed for `OnAuthenticationFailed` structured Warning logging. Added expired and issuer mismatch log assertions.
- No unsigned-token (`alg:none`) rejection coverage existed for the configured token validation parameters. Added a direct validation test.
- Missing `tid` fallback test and space-delimited string format test for claims transformation. Both added.

#### New tests added: 21 total

- 10 in `ConfigureJwtBearerOptionsTests.cs` (OIDC mode, symmetric mode, MapInboundClaims, TokenValidationParameters, 2x OnAuthenticationFailed structured logging, ProblemDetails structure, unsigned token rejection, dual-config precedence, wrong scheme)
- 3 in `EventStoreClaimsTransformationTests.cs` (tid fallback, tenant_id/tid precedence, space-delimited domains)
- 8 in `EventStoreAuthenticationOptionsTests.cs` (defaults, 2x rejection, 2x success, missing issuer, missing audience, null)

#### Deviations observed

- Middleware ordering in `Program.cs` is: UseAuthentication (L27) -> UseRateLimiter (L28) -> UseAuthorization (L29). This is intentional because rate limiting partitions on the `eventstore:tenant` claim produced during authentication.
- Task 5.3.7 (algorithm confusion test): Covered directly by unsigned-token validation against the configured `TokenValidationParameters`, without needing a Tier 3 HTTP pipeline.
- `tenant_id` and `tid` claims use fallback chain (`??` operator), not additive extraction. When both are present, only `tenant_id` is used.
- Tier 2 baseline was 1427 (not 1414 as expected), likely due to tests added between Story 4.3 merge and this run.

#### Final test counts

- Tier 1: 659 (unchanged)
- Tier 2: 1448 (+21 from 1427 actual baseline)
- Build: 0 warnings, 0 errors

#### All 9 acceptance criteria verified and satisfied

### Change Log

- 2026-03-17: Story 5.1 verification complete. Fixed pre-existing duplicate backpressure-exceeded in ErrorReferenceEndpoints.cs. Added 21 gap-closure tests across 3 test files, including structured auth logging assertions and unsigned-token rejection coverage. All ACs verified.

### File List

- `src/Hexalith.EventStore.CommandApi/OpenApi/ErrorReferenceEndpoints.cs` (modified — removed duplicate backpressure-exceeded entry)
- `tests/Hexalith.EventStore.Server.Tests/Authentication/ConfigureJwtBearerOptionsTests.cs` (modified — added 9 gap-closure tests)
- `tests/Hexalith.EventStore.Server.Tests/Authentication/EventStoreClaimsTransformationTests.cs` (modified — added 3 gap-closure tests)
- `tests/Hexalith.EventStore.Server.Tests/Authentication/EventStoreAuthenticationOptionsTests.cs` (new — 8 validation tests)
