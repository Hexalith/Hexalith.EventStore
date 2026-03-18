# Story 5.5: E2E Security Testing with Keycloak

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform developer**,
I want end-to-end security tests using real OIDC tokens from Keycloak,
So that the full six-layer auth pipeline is verified at runtime with real IdP-issued tokens.

**Note:** This is a **verification story**. The Keycloak E2E testing infrastructure is already fully implemented across previous stories (old numbering). Story 5.1 (old) added `Aspire.Hosting.Keycloak` to the AppHost, created `hexalith-realm.json` with 5 test users and OIDC protocol mappers, and configured CommandApi Authority via environment variable overrides. Old Story 5.5 added the E2E test fixture (`AspireTopologyFixture`), token helper (`KeycloakTokenHelper`), smoke tests (`KeycloakE2ESmokeTests`), security tests (`KeycloakE2ESecurityTests`), and DAPR access control E2E tests (`DaprAccessControlE2ETests`). This story formally verifies the complete E2E security testing infrastructure against the new Epic 5 acceptance criteria, confirms D11 compliance, and fills any remaining test gaps. If verification uncovers a non-trivial issue (architectural flaw, security vulnerability, or change requiring > 30 minutes of work), STOP and escalate to the user rather than fixing inline.

## Acceptance Criteria

1. **Aspire.Hosting.Keycloak added to AppHost and Keycloak configured (D11)** -- Given the AppHost project, When `Aspire.Hosting.Keycloak` is referenced, Then Keycloak runs on port 8180 with the `hexalith` realm loaded from `hexalith-realm.json` (D11). CommandApi's `Authority` is configured to Keycloak's realm URL via environment variable overrides (`Authentication__JwtBearer__Authority`, `Authentication__JwtBearer__Issuer`, `Authentication__JwtBearer__Audience`). `SigningKey` is cleared to prevent dual-mode auth conflict.

2. **5 pre-configured test users with correct claims (D11)** -- Given the `hexalith-realm.json` realm export, When parsed, Then it contains exactly 5 test users with the following profiles:

   | User | Tenants | Domains | Permissions | E2E Scenario |
   |------|---------|---------|-------------|-------------|
   | `admin-user` | tenant-a, tenant-b | orders, inventory (+ counter for sample) | command:submit, command:replay, command:query | Multi-tenant admin |
   | `tenant-a-user` | tenant-a | orders | command:submit, command:query | Cross-tenant isolation proof |
   | `tenant-b-user` | tenant-b | inventory | command:submit | Lateral isolation proof |
   | `readonly-user` | tenant-a | orders | command:query | Permission enforcement |
   | `no-tenant-user` | *(none)* | orders | command:submit | Tenant validation rejection |

   OIDC protocol mappers correctly transform user attributes to `eventstore:tenant`, `eventstore:domain`, and `eventstore:permission` claims matching `EventStoreClaimsTransformation` expectations.

3. **E2E tests acquire tokens via Resource Owner Password Grant (D11)** -- Given `KeycloakTokenHelper`, When a test requests a token, Then it uses the ROPG flow against the running Keycloak instance at `http://localhost:8180/realms/hexalith/protocol/openid-connect/token` with client `hexalith-eventstore`.

4. **E2E tests validate all 5 D11 security scenarios** -- Given the `KeycloakE2ESecurityTests` class, When executed against the full Aspire topology, Then the following scenarios are covered:
   - **Multi-tenant admin access:** `admin-user` submits command for tenant-a/orders -> 202 Accepted
   - **Cross-tenant isolation proof:** `tenant-a-user` submits command for tenant-b -> 403 Forbidden
   - **Lateral isolation proof:** `tenant-b-user` submits command for tenant-a -> 403 Forbidden
   - **Permission enforcement:** `readonly-user` (command:query only) submits command -> 403 Forbidden
   - **Tenant validation rejection:** `no-tenant-user` (empty tenants) submits command -> 403 Forbidden

5. **E2E tests use `[Trait("Category", "E2E")]` (Rule 16)** -- Given all Keycloak-based test classes, When examined, Then they are decorated with `[Trait("Category", "E2E")]` and `[Collection("AspireTopology")]` to separate from fast symmetric-key tests. `TestJwtTokenGenerator` (HS256) is for fast unit/integration tests only; runtime proof of the six-layer auth pipeline requires real IdP-issued tokens.

6. **Aspire topology fixture provides reliable startup and health checks** -- Given `AspireTopologyFixture`, When the fixture starts, Then it: (a) starts the full Aspire topology with Keycloak enabled, (b) validates CommandApi health endpoint, (c) validates Keycloak OIDC discovery endpoint availability, (d) retries token acquisition with timeout, (e) validates sample domain service health endpoint.

7. **E2E tests use real OIDC tokens exclusively -- no synthetic JWTs (D11, Rule 16)** -- Given all E2E test classes in `Security/`, When examined, Then no E2E test references `TestJwtTokenGenerator` or uses HS256 symmetric tokens. All token acquisition flows through `KeycloakTokenHelper` (ROPG against live Keycloak). This enforces D11's design rationale: real OIDC discovery, asymmetric JWKS validation, IdP-issued claims, and issuer URL validation -- capabilities that synthetic JWTs cannot prove.

### Definition of Done

This story is complete when: all 7 ACs are verified as implemented and tested, Keycloak infrastructure matches D11 specification, all 5 test users have correct claim profiles, all 5 E2E security scenarios pass with real OIDC tokens, test trait separation enforces Rule 16, the Aspire fixture reliably orchestrates the full topology, and no regressions exist in Tier 1 or Tier 2 suites.

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and baseline (BLOCKING)
  - [ ] 0.1 Run all Tier 1 tests -- confirm all pass (baseline: >= 659)
  - [ ] 0.2 Run Tier 2 tests `Hexalith.EventStore.Server.Tests` -- record actual pass count as baseline. **Baseline note:** Use the actual count from this run as your baseline for Task 5.3. Do NOT reconcile with historical baselines from other stories.
  - [ ] 0.3 Inventory existing Keycloak E2E test files and counts:
    - `KeycloakE2ESmokeTests.cs` -- count tests (expected: 2)
    - `KeycloakE2ESecurityTests.cs` -- count tests (expected: 6)
    - `DaprAccessControlE2ETests.cs` -- count tests (expected: 2)
    - `AspireTopologyFixture.cs` -- verify present and functional
    - `KeycloakE2ETestBase.cs` -- verify base class structure
    - `KeycloakTokenHelper.cs` -- verify ROPG flow
  - [ ] 0.4 Read `hexalith-realm.json` -- verify 5 test users with correct attributes (AC #2)
  - [ ] 0.5 Read `AppHost/Program.cs` -- verify Keycloak configuration matches D11 (port 8180, realm import, env var overrides)

- [ ] Task 1: Verify AppHost Keycloak integration (AC: #1)
  - [ ] 1.1 Confirm `Hexalith.EventStore.AppHost.csproj` references `Aspire.Hosting.Keycloak`
  - [ ] 1.2 Confirm `Program.cs` configures Keycloak on port 8180 (not conflicting with CommandApi 8080)
  - [ ] 1.3 Confirm realm import path points to `./KeycloakRealms` directory with `hexalith-realm.json`
  - [ ] 1.4 Confirm environment variable overrides set: `Authentication__JwtBearer__Authority` (Keycloak realm URL), `Authentication__JwtBearer__Issuer` (same), `Authentication__JwtBearer__Audience` (`hexalith-eventstore`), `Authentication__JwtBearer__RequireHttpsMetadata` (`false`), `Authentication__JwtBearer__SigningKey` (cleared)
  - [ ] 1.5 Confirm `ConfigureJwtBearerOptions.cs` OIDC discovery path is triggered when Authority is set (lines 50-54 or equivalent) -- verify that no auth code changes are needed for Keycloak (D11 rationale: zero auth code changes)

- [ ] Task 2: Verify realm configuration and test users (AC: #2)
  - [ ] 2.1 Confirm `hexalith-realm.json` defines realm `hexalith` with `enabled: true`
  - [ ] 2.2 Confirm client `hexalith-eventstore` is configured as public client with direct access grants enabled (needed for ROPG)
  - [ ] 2.3 Confirm 3 protocol mappers exist for custom claims:
    - `tenants-mapper` -> `eventstore:tenant` (JSON array, user attribute)
    - `domains-mapper` -> `eventstore:domain` (JSON array, user attribute)
    - `permissions-mapper` -> `eventstore:permission` (JSON array, user attribute)
  - [ ] 2.4 Confirm all 5 test users have correct attributes per AC #2 table:
    - [ ] 2.4.1 `admin-user`: tenants `["tenant-a","tenant-b"]`, domains `["orders","inventory","counter"]`, permissions `["command:submit","command:replay","command:query"]`
    - [ ] 2.4.2 `tenant-a-user`: tenants `["tenant-a"]`, domains `["orders"]`, permissions `["command:submit","command:query"]`
    - [ ] 2.4.3 `tenant-b-user`: tenants `["tenant-b"]`, domains `["inventory"]`, permissions `["command:submit"]`
    - [ ] 2.4.4 `readonly-user`: tenants `["tenant-a"]`, domains `["orders"]`, permissions `["command:query"]`
    - [ ] 2.4.5 `no-tenant-user`: tenants `[]` (empty), domains `["orders"]`, permissions `["command:submit"]`
  - [ ] 2.5 Confirm audience mapper adds `hexalith-eventstore` to access token (needed for audience validation)
  - [ ] 2.6 Identify test gaps for realm configuration. Potential gaps:
    - [ ] 2.6.1 ~~Test: `KeycloakRealm_HasFiveTestUsers_WithCorrectClaims`~~ -- **NOT WARRANTED.** Realm JSON is checked-in, version-controlled, and Keycloak-generated (not hand-edited YAML). Existing E2E tests (`KeycloakE2ESecurityTests`) are the real guard: if a user attribute breaks, `GetTokenAsync` or security assertions fail immediately. Adding a parse-the-JSON test would test the test infrastructure, not the system. Risk-to-maintenance ratio is poor. ACCEPTED.
    - [ ] 2.6.2 Review: Confirm protocol mapper `claimType` values match `EventStoreClaimsTransformation` expected claim names. Read `EventStoreClaimsTransformation.cs` and cross-reference with mapper `claim.name` values in realm JSON.

- [ ] Task 3: Verify token acquisition infrastructure (AC: #3)
  - [ ] 3.1 Read `KeycloakTokenHelper.cs` -- confirm ROPG flow: POST to `/realms/hexalith/protocol/openid-connect/token` with `grant_type=password`, `client_id=hexalith-eventstore`, `username`, `password`. Response parses `access_token`.
  - [ ] 3.2 Confirm `KeycloakTokenHelper` uses static `HttpClient` for thread safety and socket reuse
  - [ ] 3.3 Confirm error handling throws meaningful exception with endpoint and response body on failure
  - [ ] 3.4 CODE REVIEW: Verify `KeycloakTokenHelper` endpoint URL uses port 8180 matching AppHost config (AC #1)

- [ ] Task 4: Verify E2E test coverage for all 5 D11 scenarios (AC: #4)
  - [ ] 4.1 Map each D11 scenario to an existing test:
    - [ ] 4.1.1 Multi-tenant admin access: `AdminUser_SubmitCommand_ReturnsAcceptedAsync` -- admin-user submits for tenant-a/orders -> 202
    - [ ] 4.1.2 Cross-tenant isolation proof: `TenantAUser_SubmitCommandForTenantB_Returns403Async` -- tenant-a-user submits for tenant-b -> 403
    - [ ] 4.1.3 Lateral isolation proof: `TenantBUser_SubmitCommandForTenantA_Returns403Async` -- tenant-b-user submits for tenant-a -> 403
    - [ ] 4.1.4 Permission enforcement: `ReadonlyUser_SubmitCommand_Returns403Async` -- readonly-user (query-only) submits -> 403
    - [ ] 4.1.5 Tenant validation rejection: `NoTenantUser_SubmitCommand_Returns403Async` -- no-tenant-user submits -> 403
  - [ ] 4.2 Verify smoke tests: `AuthenticatedCommandSubmission_WithKeycloakToken_ReturnsAccepted` and `UnauthenticatedRequest_Returns401`
  - [ ] 4.3 Identify test gaps for E2E coverage. Potential gaps:
    - [ ] 4.3.1 Test: `TenantAUser_SubmitCommandForOwnTenant_ReturnsAcceptedAsync` -- scoped user within authorized tenant. Verify this test exists (expected in KeycloakE2ESecurityTests).
    - [ ] 4.3.2 Review: Does `AdminUser_SubmitCommand_ReturnsAcceptedAsync` verify `Location` header? **PRE-VERIFIED:** Line 53 asserts `response.Headers.Location.ShouldNotBeNull()`. Smoke test (`KeycloakE2ESmokeTests`) may omit Location assertion -- acceptable, smoke tests are lighter by design. Confirm and mark done.
    - [ ] 4.3.3 Review: Domain-level authorization E2E. **NO GAP.** `ClaimsRbacValidator.cs` domain enforcement is optional (current behavior). No E2E test needed for optional enforcement. ACCEPTED.

- [ ] Task 5: Verify test trait separation (AC: #5, Rule 16)
  - [ ] 5.1 Confirm `KeycloakE2ESmokeTests` has `[Trait("Category", "E2E")]` and `[Collection("AspireTopology")]`
  - [ ] 5.2 Confirm `KeycloakE2ESecurityTests` has `[Trait("Category", "E2E")]` and `[Collection("AspireTopology")]`
  - [ ] 5.3 Confirm `DaprAccessControlE2ETests` has `[Trait("Category", "E2E")]` and `[Collection("AspireTopology")]`
  - [ ] 5.4 Confirm `MultiTenantStorageIsolationTests` and `CommandStatusIsolationTests` do NOT have E2E trait (they use in-memory fakes, not Keycloak -- correct classification as component-level tests)
  - [ ] 5.5 Confirm `KeycloakE2ETestBase` documents the trait requirement in its XML doc comment

- [ ] Task 6: Verify Aspire topology fixture (AC: #6)
  - [ ] 6.1 Read `AspireTopologyFixture.cs` -- confirm: (a) implements `IAsyncLifetime`, (b) starts full topology via `DistributedApplicationTestingBuilder`, (c) enables Keycloak via `EnableKeycloak` env var, (d) has adequate startup timeout (expected: >= 5 minutes)
  - [ ] 6.2 Confirm health check sequence: CommandApi endpoint, Keycloak OIDC discovery, token acquisition retry, CommandApi health, sample service health
  - [ ] 6.3 Confirm shared fixture pattern via `[Collection("AspireTopology")]` and `AspireTopologyCollection` -- topology starts ONCE per test collection, not per test
  - [ ] 6.4 Confirm error diagnostics: container log capture on timeout failure (Docker ps + logs)

- [ ] Task 7: Final verification
  - [ ] 7.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- zero warnings, zero errors
  - [ ] 7.2 Run all Tier 1 tests -- confirm pass count (baseline: >= 659)
  - [ ] 7.3 Run all Tier 2 tests -- confirm pass count (baseline: use actual count from Task 0.2)
  - [ ] 7.4 Confirm all 7 acceptance criteria are satisfied
  - [ ] 7.5 Report final test count delta
  - [ ] 7.6 Write verification report in Completion Notes (test inventory per file, gaps found, new tests added, deviations observed)

**Effort guidance:** The Keycloak E2E infrastructure and tests are already comprehensive (2 smoke tests + 6 security tests + 2 DAPR access control tests = 10 E2E tests, plus fixture, base class, and token helper). Expect ~45-60 minutes for verification. This is a pure read-verify story -- **expected new tests: 0**. Realm JSON structural test not warranted (party mode review consensus: E2E tests are the real guard). Domain-level auth E2E not needed (optional enforcement). Most tasks are file reads confirming existing code matches ACs. Write zero code, document verification in Completion Notes. Several tasks are CODE REVIEW only (no test needed) -- these are explicitly marked. Do NOT write tests for code-review-only tasks.

## Dev Notes

### CRITICAL: This is a Verification Story

The Keycloak E2E testing infrastructure is **already fully implemented** across previous stories:
- **Old Story 5.1** added `Aspire.Hosting.Keycloak` to AppHost, created `hexalith-realm.json` with 5 test users, OIDC protocol mappers for custom claims (`eventstore:tenant`, `eventstore:domain`, `eventstore:permission`), and configured CommandApi Authority via environment variable overrides.
- **Old Story 5.5** added the full E2E test infrastructure: `AspireTopologyFixture` (287 lines, shared topology with health checks), `KeycloakE2ETestBase` (base class), `KeycloakTokenHelper` (ROPG flow), `KeycloakE2ESmokeTests` (2 tests), `KeycloakE2ESecurityTests` (6 tests covering all 5 D11 scenarios), and `DaprAccessControlE2ETests` (2 tests).

This story formally verifies the COMPLETE E2E security testing model against the new Epic 5 acceptance criteria and fills remaining gaps.

### Architecture Compliance

- **D11 (E2E Security Testing Infrastructure -- Keycloak in Aspire):**
  - **Package:** `Aspire.Hosting.Keycloak` in AppHost (hosting only -- no `Aspire.Keycloak.Authentication` client package needed; existing `ConfigureJwtBearerOptions` OIDC discovery path is sufficient)
  - **Port:** `8180` (avoids conflict with CommandApi on `8080`)
  - **Realm:** `hexalith` with client `hexalith-eventstore`, OIDC protocol mappers for `tenants`, `domains`, `permissions` (JSON array claims matching `EventStoreClaimsTransformation` expectations)
  - **CommandApi wiring:** Environment variable overrides set `Authority` to Keycloak realm URL, triggering existing OIDC discovery. Zero auth code changes.
  - **Opt-in:** `EnableKeycloak` environment variable (defaults to enabled). When disabled, falls back to symmetric key auth (`appsettings.Development.json`).

- **Rule 16 (E2E test separation):**
  - E2E security tests use real Keycloak OIDC tokens -- never synthetic JWTs for runtime security verification.
  - `TestJwtTokenGenerator` (HS256) is for fast unit/integration tests only.
  - All Keycloak-based test classes decorated with `[Trait("Category", "E2E")]` and `[Collection("AspireTopology")]`.

- **Six-Layer Auth Pipeline (Runtime Proof):**
  - **Layer 1 (JWT Authentication):** Keycloak-issued RS256 token validated via OIDC discovery + JWKS
  - **Layer 2 (Claims Transformation):** Protocol mappers inject `eventstore:tenant/domain/permission` claims
  - **Layer 3 (Endpoint Authorization):** `[Authorize]` policy on CommandApi endpoints
  - **Layer 4 (MediatR Authorization):** `AuthorizationBehavior` validates tenant/domain/permission claims
  - **Layer 5 (Actor Tenant Validation):** `TenantValidator` in actor pipeline (SEC-2)
  - **Layer 6 (DAPR Access Control):** Access control policies restrict service-to-service calls (D4)
  - **D11 proves layers 1-4 with real OIDC tokens.** Layers 5-6 are exercised by the full Aspire topology (actor processing + DAPR sidecar policies).

- **What Keycloak proves that symmetric keys cannot:**
  - Real OIDC discovery flow (`/.well-known/openid-configuration`)
  - Asymmetric key validation via JWKS (RS256 vs HS256)
  - IdP-issued claims structure (protocol mappers, not synthetic)
  - Issuer URL validation (real Keycloak realm URL)
  - Token expiry management by Keycloak (real JWT lifecycle)

### Key Source Files

| File | Purpose |
|------|---------|
| `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` | AppHost project file (Aspire.Hosting.Keycloak reference) |
| `src/Hexalith.EventStore.AppHost/Program.cs` | Aspire topology: Keycloak on 8180, realm import, env var overrides |
| `src/Hexalith.EventStore.AppHost/KeycloakRealms/hexalith-realm.json` | Realm-as-code: 5 users, client, protocol mappers (D11) |
| `src/Hexalith.EventStore.CommandApi/Authentication/ConfigureJwtBearerOptions.cs` | Dual-mode JWT: OIDC discovery (Keycloak) + symmetric key (dev) |
| `src/Hexalith.EventStore.CommandApi/Authentication/EventStoreAuthenticationOptions.cs` | Auth options record: Authority, SigningKey, Issuer, Audience |
| `src/Hexalith.EventStore.CommandApi/Authentication/EventStoreClaimsTransformation.cs` | Claims transformation: maps Keycloak claims to internal format |
| `src/Hexalith.EventStore.CommandApi/Authorization/ClaimsRbacValidator.cs` | Claims-based RBAC: tenant/domain/permission validation |
| `tests/Hexalith.EventStore.IntegrationTests/Security/AspireTopologyFixture.cs` | Shared fixture: full Aspire topology startup + health checks (287 lines) |
| `tests/Hexalith.EventStore.IntegrationTests/Security/AspireTopologyCollection.cs` | xUnit collection: ensures single topology startup per test run |
| `tests/Hexalith.EventStore.IntegrationTests/Security/KeycloakE2ETestBase.cs` | Base class: CommandApiClient, KeycloakBaseUrl, GetTokenAsync |
| `tests/Hexalith.EventStore.IntegrationTests/Security/KeycloakE2ESmokeTests.cs` | 2 smoke tests: authenticated submission, unauthenticated rejection |
| `tests/Hexalith.EventStore.IntegrationTests/Security/KeycloakE2ESecurityTests.cs` | 6 security tests: admin access, scoped access, isolation (2), permission, no-tenant |
| `tests/Hexalith.EventStore.IntegrationTests/Security/DaprAccessControlE2ETests.cs` | 2 DAPR access control E2E tests (sidecar denial) |
| `tests/Hexalith.EventStore.IntegrationTests/Helpers/KeycloakTokenHelper.cs` | ROPG token acquisition from running Keycloak (53 lines) |
| `tests/Hexalith.EventStore.IntegrationTests/Helpers/TestJwtTokenGenerator.cs` | HS256 symmetric token generator (fast tests only, NOT E2E) |
| `src/Hexalith.EventStore.CommandApi/appsettings.Development.json` | Dev fallback: symmetric key auth when Keycloak disabled |

### Existing Test Coverage Summary

**KeycloakE2ESmokeTests.cs (2 tests):**
1. `AuthenticatedCommandSubmission_WithKeycloakToken_ReturnsAccepted` -- admin-user, real token, POST /api/v1/commands -> 202
2. `UnauthenticatedRequest_Returns401` -- no Authorization header -> 401

**KeycloakE2ESecurityTests.cs (6 tests):**
1. `AdminUser_SubmitCommand_ReturnsAcceptedAsync` -- admin-user for tenant-a/orders -> 202 + Location header
2. `TenantAUser_SubmitCommandForOwnTenant_ReturnsAcceptedAsync` -- scoped user in authorized scope -> 202
3. `TenantAUser_SubmitCommandForTenantB_Returns403Async` -- cross-tenant isolation -> 403
4. `TenantBUser_SubmitCommandForTenantA_Returns403Async` -- lateral isolation -> 403
5. `ReadonlyUser_SubmitCommand_Returns403Async` -- permission enforcement (query-only) -> 403
6. `NoTenantUser_SubmitCommand_Returns403Async` -- tenant validation rejection -> 403

**DaprAccessControlE2ETests.cs (2 tests):**
1. `SampleSidecar_InvokeCommandApi_DeniedByAccessControl` -- unauthorized sidecar invocation -> 403
2. `SampleSidecar_DeniedInvocation_ResponseContainsErrorContext` -- error body contains PermissionDenied context

**Total: 10 Keycloak-based E2E tests across 3 test classes.**

### Existing Patterns to Follow

- **E2E test base class:** All Keycloak E2E tests extend `KeycloakE2ETestBase` which provides `CommandApiClient`, `KeycloakBaseUrl`, and `GetTokenAsync(username, password)`.
- **Collection fixture:** `[Collection("AspireTopology")]` ensures single topology startup. `AspireTopologyFixture` implements `IAsyncLifetime`.
- **Trait separation:** `[Trait("Category", "E2E")]` on all classes. Tests filterable with `--filter Category=E2E` or excluded from fast CI.
- **Assertion library:** Shouldly 4.3.0 fluent assertions.
- **Command request helper:** `CreateCommandRequest(token, tenant, domain, commandType)` builds POST /api/v1/commands with Bearer auth.
- **Test naming:** `{User}_{Action}_{ExpectedResult}Async` convention for E2E tests.
- **Error diagnostics:** Tests that expect 202 include response body in assertion failure message for debugging.

### Cross-Story Dependencies

- **Story 5.1 (JWT Authentication & Claims Transformation)** -- DONE. Created JWT auth infrastructure, ConfigureJwtBearerOptions with OIDC discovery, EventStoreClaimsTransformation.
- **Story 5.2 (Claims-Based Command Authorization)** -- REVIEW. AuthorizationBehavior validates tenant/domain/permission claims in MediatR pipeline.
- **Story 5.3 (Three-Layer Multi-Tenant Data Isolation)** -- REVIEW. Verified data path, storage key, and pub/sub topic isolation.
- **Story 5.4 (DAPR Service-to-Service Access Control)** -- REVIEW. Verified access control policies, component scoping, DaprDomainServiceInvoker.

### Previous Story Intelligence

**Story 5.4 (DAPR Service-to-Service Access Control)** -- status: review:
- Verification story pattern: read existing code, verify against ACs, fill test gaps.
- Added 3 gap-closure tests (POST-only verb guard, defaultAction divergence guard, production operations guard).
- Tier 2: 1479 -> 1482 (+3 tests).
- Key learning: Verification stories should focus on confirming existing behavior, not refactoring.
- Key learning: DO NOT duplicate tests that already exist -- review first, add only what's missing.

**Story 5.3 (Three-Layer Multi-Tenant Data Isolation)** -- status: review:
- Added 6 gap-closure tests across 4 test files.
- Tier 2: 1469 -> 1475.
- Verified pub/sub topic isolation, storage key isolation, metadata ownership.

**Story 5.2 (Claims-Based Command Authorization)** -- status: review:
- Verified 10 ACs for claims-based authorization.
- Tier 1 baseline: 659. Tier 2 baseline: 1466 (at time of story).

### Git Intelligence

Recent commits (relevant context):
- `726ccf8` -- Update Story 5.4 for DAPR Service-to-Service Access Control
- `6f6cfaa` -- tighten 5-3 review follow-up tests
- `3b8d5bc` -- Update story status for claims-based command authorization and DAPR service-to-service access control
- `61e05d3` -- Update sprint status and implement various tests for command authorization and event persistence
- `fe3d99b` -- Add comprehensive tests for AuthorizationBehavior and document three-layer multi-tenant data isolation

### Keycloak EnableKeycloak Toggle

The AppHost conditionally enables Keycloak via `EnableKeycloak` environment variable (defaults to enabled). When disabled:
- CommandApi falls back to symmetric key auth from `appsettings.Development.json`
- `TestJwtTokenGenerator` (HS256) works for unit/integration tests
- No Docker dependency for Keycloak container

When enabled (default):
- Keycloak container starts on port 8180
- Environment variable overrides switch CommandApi to OIDC discovery mode
- `SigningKey` is cleared to prevent dual-mode conflict
- E2E tests acquire real OIDC tokens via `KeycloakTokenHelper`

### Tier 3 Test Requirements

E2E tests in `Hexalith.EventStore.IntegrationTests` are Tier 3 (full Aspire topology). Requirements:
- `dapr init` (full, not `--slim`) -- Docker containers for DAPR, Redis, Keycloak
- Docker Desktop running
- Adequate resources for Aspire topology (Keycloak + Redis + CommandApi + sample service + DAPR sidecars)
- Tests NOT run in CI by default (Tier 3 optional in CI pipeline)

### Anti-Patterns to Avoid

- **DO NOT modify Keycloak realm JSON** unless a user attribute or mapper is genuinely missing. The realm is realm-as-code and should be stable.
- **DO NOT modify ConfigureJwtBearerOptions.cs.** D11 rationale: zero auth code changes for Keycloak. Verify OIDC discovery works without changes.
- **DO NOT modify AppHost/Program.cs** unless Keycloak configuration is genuinely incorrect. Verify only.
- **DO NOT duplicate tests that already exist.** Review the 10 existing E2E tests FIRST (Task 0.3), then add ONLY what's missing.
- **DO NOT add symmetric-key E2E tests.** Rule 16: E2E security verification uses real Keycloak tokens. Symmetric-key tests belong in Tier 1/2.
- **DO NOT add new NuGet dependencies.** All required packages are already referenced.
- **DO NOT use `TestJwtTokenGenerator` in E2E tests.** E2E tests MUST use `KeycloakTokenHelper` for real OIDC tokens.

### Testing Standards

- **Framework:** xUnit 2.9.3 / Shouldly 4.3.0
- **E2E Fixture:** `AspireTopologyFixture` (collection-scoped, shared across all E2E tests)
- **Token Acquisition:** `KeycloakTokenHelper.GetTokenAsync(username, password)` -- ROPG against live Keycloak
- **Test naming:** `{User}_{Action}_{ExpectedResult}Async`
- **Trait separation:** `[Trait("Category", "E2E")]` + `[Collection("AspireTopology")]` on all E2E test classes
- **Tier classification:** Tier 3 (full Aspire topology, Docker required)
- **Test locations:** Security E2E tests in `tests/Hexalith.EventStore.IntegrationTests/Security/`

### Project Structure Notes

- No new project folders expected
- No new NuGet packages needed
- All test files in existing directories (Security/, Helpers/)
- New tests (if any) should be added to existing test classes, not new files
- Realm JSON is in `src/Hexalith.EventStore.AppHost/KeycloakRealms/` -- copied to output via MSBuild Content item

### References

- [Source: epics.md#Story-5.5] E2E Security Testing with Keycloak acceptance criteria
- [Source: architecture.md#D11] E2E Security Testing Infrastructure -- Keycloak in Aspire
- [Source: architecture.md#Rule-16] E2E security tests use real Keycloak OIDC tokens
- [Source: architecture.md#Six-Layer-Auth] Six-layer defense-in-depth auth pipeline
- [Source: architecture.md#SEC-1..SEC-5] Security-critical architectural constraints
- [Source: 5-4-dapr-service-to-service-access-control.md] Previous verification story pattern
- [Source: KeycloakE2ESecurityTests.cs] 6 existing E2E security tests (D11 scenarios)
- [Source: KeycloakE2ESmokeTests.cs] 2 existing smoke tests
- [Source: DaprAccessControlE2ETests.cs] 2 existing DAPR access control E2E tests
- [Source: AspireTopologyFixture.cs] E2E fixture (287 lines, shared topology)
- [Source: hexalith-realm.json] Realm-as-code (5 users, client, mappers)
- [Source: ConfigureJwtBearerOptions.cs] Dual-mode JWT validation (OIDC + symmetric)

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
