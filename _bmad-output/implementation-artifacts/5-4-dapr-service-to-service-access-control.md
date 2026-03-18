# Story 5.4: DAPR Service-to-Service Access Control

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform developer**,
I want service-to-service communication between EventStore components authenticated via DAPR policies,
So that the internal call graph is enforced and unauthorized component interactions are blocked.

**Note:** This is a **verification story**. The DAPR access control infrastructure is already fully implemented -- Story 5.1 (old numbering) created `accesscontrol.yaml` (local + production), configured AppHost and Aspire wiring, and added 13 unit tests in `AccessControlPolicyTests.cs`. Story 5.5 (E2E with Keycloak) added 2 E2E tests in `DaprAccessControlE2ETests.cs`. This story formally verifies the complete service-to-service access control model against the new Epic 5 acceptance criteria, confirms FR34/NFR15/D4 compliance, and fills any remaining test gaps. If verification uncovers a non-trivial issue (architectural flaw, security vulnerability, or change requiring > 30 minutes of work), STOP and escalate to the user rather than fixing inline.

## Acceptance Criteria

1. **CommandApi can invoke actor services and domain services (D4, FR34)** -- Given the local `accesscontrol.yaml` and production `accesscontrol.yaml`, When the commandapi policy is evaluated, Then commandapi has a policy entry with `defaultAction: deny` and an allowed operation `/**` restricted to `httpVerb: ['POST']` with `action: allow`. This permits `DaprClient.InvokeMethodAsync` (D7) to call domain services via any method path using POST. Both local and production configs must have this policy.

2. **Domain services cannot invoke other services directly (D4)** -- Given the access control policies, When a domain service (e.g., `sample`) attempts to invoke another service, Then the domain service's policy entry has `defaultAction: deny` with NO allowed operations (zero-trust posture). In production (deny-by-default globally), any unlisted domain service is also denied. The `sample` app-id is additionally excluded from state store scopes, pub/sub scopes, and publishingScopes (zero infrastructure access).

3. **Policy expressed as per-app-id allow list with `allowedOperations`** -- Given the access control YAML configuration, When parsed, Then it is a DAPR `Configuration` CRD (`apiVersion: dapr.io/v1alpha1`, `kind: Configuration`) with `spec.accessControl.policies` containing per-app-id entries. Each entry specifies `appId`, `defaultAction`, `trustDomain`, `namespace`, and optionally `operations` with `name` (path), `httpVerb` (list), and `action`.

4. **Production uses deny-by-default security posture (D4, NFR15)** -- Given the production `accesscontrol.yaml`, When evaluated, Then `spec.accessControl.defaultAction` is `deny`. Any app-id without an explicit policy is blocked. Trust domain uses SPIFFE identity (`{env:DAPR_TRUST_DOMAIN|hexalith.io}`).

5. **Aspire topology wires access control to all sidecars** -- Given the AppHost `Program.cs`, When sidecars are configured, Then both `commandapi` and `sample` sidecars load `accesscontrol.yaml` via the `Config` property. AppHost validates the file exists at startup (throws `FileNotFoundException` if missing).

6. **DaprDomainServiceInvoker uses POST-only invocation (D7)** -- Given the `DaprDomainServiceInvoker`, When it invokes a domain service, Then it uses `DaprClient.InvokeMethodAsync<TRequest, TResponse>` which defaults to POST. This is consistent with the `accesscontrol.yaml` policy that allows only `httpVerb: ['POST']` for commandapi.

7. **Component scoping enforces infrastructure isolation** -- Given DAPR component YAML files (state store, pub/sub), When scopes are evaluated, Then state store scopes contain only `commandapi` (domain services excluded). Pub/sub scopes include `commandapi` plus authorized subscribers but NOT domain services. PublishingScopes explicitly deny domain services. SubscriptionScopes restrict per-topic access.

### Definition of Done

This story is complete when: all 7 ACs are verified as implemented and tested, access control policies enforce the intended call graph (commandapi -> domain services, domain services -> nothing), production is deny-by-default, Aspire wiring loads configs for all sidecars, component scoping prevents domain service infrastructure access, and no regressions exist in Tier 1 or Tier 2 suites.

## Tasks / Subtasks

- [ ] Task 0: Verify prerequisites and baseline (BLOCKING)
  - [ ] 0.1 Run all Tier 1 tests -- confirm all pass (baseline: >= 659)
  - [ ] 0.2 Run Tier 2 tests `Hexalith.EventStore.Server.Tests` -- record actual pass count as baseline. **Baseline note:** Use the actual count from this run as your baseline for Task 5.3. Do NOT reconcile with historical baselines from other stories.
  - [ ] 0.3 Inventory existing access control test files and counts:
    - `AccessControlPolicyTests.cs` -- count tests (expected: 13)
    - `DaprComponentValidationTests.cs` -- count tests
    - `ProductionDaprComponentValidationTests.cs` -- count tests
    - `DaprAccessControlE2ETests.cs` -- count tests (expected: 2, Tier 3)
  - [ ] 0.4 Read `accesscontrol.yaml` (local) -- verify structure matches AC #1, #2, #3
  - [ ] 0.5 Read `accesscontrol.yaml` (production) -- verify deny-by-default, SPIFFE trust domain (AC #4)

- [ ] Task 1: Verify commandapi policy (AC: #1, #3)
  - [ ] 1.1 Confirm local `accesscontrol.yaml`: commandapi entry has `defaultAction: deny`, wildcard `/**` path, `httpVerb: ['POST']`, `action: allow`
  - [ ] 1.2 Confirm production `accesscontrol.yaml`: same commandapi policy structure with env-parameterized `trustDomain` and `namespace`
  - [ ] 1.3 Review `AccessControlPolicyTests.LocalAccessControlYaml_CommandApiPolicy_AllowsRequiredOperations` -- verify it checks wildcard path, POST verb, allow action
  - [ ] 1.4 Review `AccessControlPolicyTests.AllDaprComponents_LocalAndProduction_PolicyTopologyConsistent` -- verify local-production consistency is tested
  - [ ] 1.5 Identify test gaps for commandapi policy. Potential gaps:
    - [ ] 1.5.1 Test: `CommandApiPolicy_OnlyAllowsPOST_OtherVerbsBlocked` -- verify the policy does NOT list GET/PUT/DELETE in httpVerb (defense-in-depth: DaprClient.InvokeMethodAsync uses POST only)
    - [ ] 1.5.2 Test: `ProductionAccessControlYaml_CommandApiPolicy_AllowsRequiredOperations` -- verify production commandapi policy mirrors local. LIKELY COVERED by test #9 (`AllDaprComponents_LocalAndProduction_PolicyTopologyConsistent`) which checks production commandapi wildcard POST. Verify test #9 covers this and mark as covered if so -- do NOT write a duplicate test.

- [ ] Task 2: Verify domain service denial (AC: #2)
  - [ ] 2.1 Confirm local `accesscontrol.yaml`: sample entry has `defaultAction: deny`, no `operations` key
  - [ ] 2.2 Confirm production `accesscontrol.yaml`: sample is omitted (relies on global deny-by-default)
  - [ ] 2.3 Review `AccessControlPolicyTests.LocalAccessControlYaml_SamplePolicy_DeniesDirectInvocation` -- verify deny + no operations
  - [ ] 2.4 Review `AccessControlPolicyTests.DomainServicePolicy_ZeroInfrastructureAccess_AllDenied` -- verify scoping exclusions
  - [ ] 2.5 Identify test gaps for domain service denial. Potential gaps:
    - [ ] 2.5.1 Test: `ProductionAccessControlYaml_OnlyCommandApiHasAllowedOperations` -- verify that in production accesscontrol.yaml, ONLY the commandapi policy has allowed operations. Any other policy entry with `operations` defined is a regression. This is a forward-looking guard: if a future developer adds a domain service policy with operations, this test catches it.
    - [ ] 2.5.2 CODE REVIEW (not a test): Verify `AppHost/Program.cs:62-70` -- sample sidecar is configured WITHOUT `.WithReference(stateStore)` or `.WithReference(pubSub)`. This validates "zero infrastructure access" beyond component scoping. Confirm by reading the code -- do NOT write a unit test for this (it's an Aspire builder API call, not testable without integration test infrastructure).

- [ ] Task 3: Verify Aspire topology wiring (AC: #5)
  - [ ] 3.1 Read `AppHost/Program.cs` -- confirm `accessControlConfigPath` resolved and validated
  - [ ] 3.2 Confirm commandapi sidecar loads `accesscontrol.yaml` via `AddHexalithEventStore` -> `HexalithEventStoreExtensions.cs` -> `Config = daprConfigPath`
  - [ ] 3.3 Confirm sample sidecar loads `accesscontrol.yaml` via `WithDaprSidecar` -> `Config = accessControlConfigPath`
  - [ ] 3.4 Confirm `HexalithEventStoreExtensions.AddHexalithEventStore` accepts `daprConfigPath` and passes it to sidecar options
  - [ ] 3.5 Identify test gaps for Aspire wiring. Potential gaps:
    - [ ] 3.5.1 CODE REVIEW (not a test): Verify `AppHost/Program.cs:9-20` -- `File.Exists` check throws `FileNotFoundException` when `accesscontrol.yaml` is missing. This is a 6-line runtime guard with obvious correctness. Confirm by reading the code -- do NOT write a test (requires mocking file system or integration test infrastructure for negligible risk).

- [ ] Task 4: Verify DaprDomainServiceInvoker compliance (AC: #6)
  - [ ] 4.1 Read `DaprDomainServiceInvoker.cs` -- confirm `InvokeMethodAsync<TRequest, TResponse>` usage (POST by default)
  - [ ] 4.2 Confirm no GET/PUT/DELETE overloads used for service invocation
  - [ ] 4.3 Confirm error handling catches access control denials (DaprApiException or similar)
  - [ ] 4.4 Identify test gaps for invoker compliance. Potential gaps:
    - [ ] 4.4.1 Test: `DaprDomainServiceInvoker_UsesPostHttpVerb_ConsistentWithAccessControlPolicy` -- verify InvokeMethodAsync is called (mocked) confirming POST-only invocation pattern. NOTE: `DaprClient.InvokeMethodAsync<TReq, TRes>(appId, method, request)` uses POST by default. This is a design-documentation test rather than a behavioral gap. If existing `DaprDomainServiceInvokerTests` already verify the call pattern, mark as covered.

- [ ] Task 5: Final verification
  - [ ] 5.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- zero warnings, zero errors
  - [ ] 5.2 Run all Tier 1 tests -- confirm pass count (baseline: >= 659)
  - [ ] 5.3 Run all Tier 2 tests -- confirm pass count (baseline: use actual count from Task 0.2)
  - [ ] 5.4 Confirm all 7 acceptance criteria are satisfied
  - [ ] 5.5 Report final test count delta
  - [ ] 5.6 Write verification report in Completion Notes (test inventory per file, gaps found, new tests added, deviations observed)

**Effort guidance:** The access control infrastructure and tests are already comprehensive (13 unit tests + 2 E2E tests). Expect ~1-2 hours for verification and minor gap-closure. Most work is reading and confirming existing tests cover the ACs. New tests are only needed if specific gaps are found in the existing `AccessControlPolicyTests.cs` or component validation tests.

## Dev Notes

### CRITICAL: This is a Verification Story

The DAPR service-to-service access control infrastructure is **already fully implemented** across previous stories:
- **Old Story 5.1** created `accesscontrol.yaml` (local + production), configured Aspire wiring, and added 13 tests in `AccessControlPolicyTests.cs` covering YAML structure, deny-by-default, commandapi policy, sample policy, pub/sub scoping, state store scoping, topology consistency, mTLS trust domain, dead-letter scoping, namespace, and zero-infrastructure-access for domain services.
- **Old Story 5.5** added 2 E2E tests in `DaprAccessControlE2ETests.cs` verifying: (1) unauthorized service-to-service invocation returns 403, (2) denial response contains error context (PermissionDenied, target app-id, operation path, HTTP verb).
- **DaprComponentValidationTests.cs** and **ProductionDaprComponentValidationTests.cs** validate component YAML structure.

This story formally verifies the COMPLETE access control model against the new Epic 5 acceptance criteria and fills remaining gaps.

### Architecture Compliance

- **Six-Layer Auth Pipeline (Layer 6 -- this story):**
  - **Layer 6 (DAPR Access Control):** Access control policies restrict which app-ids can invoke which services (D4, FR34, NFR15).
  - Layers 1-3 (JWT, Claims, Endpoint): Story 5.1
  - Layer 4 (MediatR Authorization): Story 5.2
  - Layer 5 (Actor Tenant Validation): Story 5.3

- **D4 (DAPR Access Control -- Per-App-ID Allow List):**
  - **Policy:** CommandApi can invoke actor services and domain services. Domain services can invoke nothing directly.
  - **Enforcement:** DAPR access control Configuration CRD with per-app-id policies and `allowedOperations`.
  - **Local:** `defaultAction: allow` (self-hosted without mTLS; policies still defined for structural validation).
  - **Production:** `defaultAction: deny` (Kubernetes with mTLS + Sentry; unlisted app-ids blocked).

- **D7 (Domain Service Invocation -- DAPR Service Invocation):**
  - `DaprClient.InvokeMethodAsync<TRequest, TResponse>` uses POST by default.
  - Domain service endpoint resolved from DAPR config store registration.
  - mTLS between sidecars is automatic with DAPR.
  - DAPR resiliency policies (retry, circuit breaker, timeout) applied at sidecar level.

- **Component Scoping (3-layer defense-in-depth):**
  - **Layer 1 (Component Scopes):** `scopes` field on state store/pub/sub YAML restricts which app-ids can access the component.
  - **Layer 2 (Publishing Scopes):** `publishingScopes` metadata controls which app-ids can publish to topics.
  - **Layer 3 (Subscription Scopes):** `subscriptionScopes` metadata controls which app-ids can subscribe to topics.
  - Domain services are excluded from ALL three layers.

- **Zero Infrastructure Access (D4, AC #13 from Story 5.1):**
  - Domain services have: no state store access (excluded from scopes), no pub/sub access (excluded from scopes + denied in publishingScopes/subscriptionScopes), no outbound service invocation (defaultAction: deny, no operations).
  - In AppHost: sample sidecar does NOT reference StateStore or PubSub components (stronger isolation than scoping alone).

- **Enforcement Rules (relevant):**
  - #4: No custom retry logic -- DAPR resiliency only
  - #5: Never log event payload data -- envelope metadata only

### Key Source Files

| File | Purpose |
|------|---------|
| `src/Hexalith.EventStore.AppHost/DaprComponents/accesscontrol.yaml` | Local access control Configuration CRD (D4) |
| `deploy/dapr/accesscontrol.yaml` | Production access control Configuration CRD (D4) |
| `src/Hexalith.EventStore.AppHost/Program.cs` | Aspire topology: resolves and validates accesscontrol.yaml, wires to both sidecars |
| `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` | Aspire extension: wires commandapi sidecar with config path |
| `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` | DAPR service invocation (D7): POST-only InvokeMethodAsync |
| `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` | Local pub/sub component (scoping, publishing/subscription scopes) |
| `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml` | Local state store component (scoping) |
| `deploy/dapr/pubsub-rabbitmq.yaml` | Production RabbitMQ pub/sub (scoping) |
| `deploy/dapr/pubsub-kafka.yaml` | Production Kafka pub/sub (scoping) |
| `deploy/dapr/statestore-postgresql.yaml` | Production PostgreSQL state store (scoping) |
| `deploy/dapr/statestore-cosmosdb.yaml` | Production Cosmos DB state store (scoping) |
| `tests/Hexalith.EventStore.Server.Tests/Security/AccessControlPolicyTests.cs` | 13 YAML validation tests (Story 5.1) |
| `tests/Hexalith.EventStore.Server.Tests/DaprComponents/DaprComponentValidationTests.cs` | Component YAML validation |
| `tests/Hexalith.EventStore.Server.Tests/DaprComponents/ProductionDaprComponentValidationTests.cs` | Production component validation |
| `tests/Hexalith.EventStore.Server.Tests/DaprComponents/DaprYamlTestHelper.cs` | YAML test utilities (YamlDotNet-based) |
| `tests/Hexalith.EventStore.IntegrationTests/Security/DaprAccessControlE2ETests.cs` | 2 E2E tests (Tier 3) |

### Existing Test Coverage Summary

**AccessControlPolicyTests.cs (13 tests, all YAML-based):**
1. `LocalAccessControlYaml_IsValidYaml_ParsesCorrectly` -- CRD structure
2. `LocalAccessControlYaml_HasDenyDefault_SecureByDefault` -- local default=allow (no mTLS)
3. `LocalAccessControlYaml_CommandApiPolicy_AllowsRequiredOperations` -- wildcard POST
4. `LocalAccessControlYaml_SamplePolicy_DeniesDirectInvocation` -- deny, no ops
5. `ProductionAccessControlYaml_IsValidYaml_ParsesCorrectly` -- CRD structure
6. `ProductionAccessControlYaml_HasDenyDefault_SecureByDefault` -- production default=deny
7. `PubSubYaml_HasScopes_RestrictsAppAccess` -- component scopes
8. `StateStoreYaml_HasScopes_RestrictsAppAccess` -- component scopes
9. `AllDaprComponents_LocalAndProduction_PolicyTopologyConsistent` -- cross-config consistency
10. `LocalAccessControlYaml_HasTrustDomain_MtlsConfigured` -- trust domain=public
11. `PubSubYaml_DeadLetterTopics_ScopedToCommandApiOnly` -- dead-letter scoping
12. `ProductionPubSubYaml_DeadLetterTopics_ScopedToCommandApiOnly` -- production dead-letter
13. `DomainServicePolicy_ZeroInfrastructureAccess_AllDenied` -- comprehensive denial check
14. `AccessControlYaml_HasNamespace_IdentityConfigured` -- namespace validation

**DaprAccessControlE2ETests.cs (2 tests, Tier 3):**
1. `SampleSidecar_InvokeCommandApi_DeniedByAccessControl` -- 403 on unauthorized invocation
2. `SampleSidecar_DeniedInvocation_ResponseContainsErrorContext` -- error body contains PermissionDenied, target app-id, path, verb

### Existing Patterns to Follow

- **YAML test pattern:** YamlDotNet-based parsing via `DaprYamlTestHelper` (LoadYaml, Nav, NavList, GetString, GetComponentMetadataValue). DO NOT use `File.ReadAllText` + `ShouldContain` -- AccessControlPolicyTests uses structured YamlDotNet parsing.
- **File paths in tests:** `Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", ...))`
- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}` convention.
- **Assertion library:** Shouldly 4.3.0 fluent assertions.
- **Verification task structure:** Same as Stories 5.1, 5.2, 5.3 -- read code first, verify against ACs, then fill test gaps.
- **Security tests location:** `tests/Hexalith.EventStore.Server.Tests/Security/`
- **DAPR component tests location:** `tests/Hexalith.EventStore.Server.Tests/DaprComponents/`

### Cross-Story Dependencies

- **Story 5.1 (JWT Authentication & Claims Transformation)** -- DONE. Created the access control infrastructure and 13 tests.
- **Story 5.2 (Claims-Based Command Authorization)** -- REVIEW. Verified MediatR Layer 4 authorization.
- **Story 5.3 (Three-Layer Multi-Tenant Data Isolation)** -- REVIEW. Verified data path, storage key, and pub/sub topic isolation.
- **Story 5.5 (E2E Security Testing with Keycloak)** -- BACKLOG. Will exercise access control with real OIDC tokens. Created 2 E2E tests that are Tier 3 (requires full Aspire + DAPR + Keycloak).

### Previous Story Intelligence

**Story 5.3 (Three-Layer Multi-Tenant Data Isolation)** -- status: review:
- Added 6 gap-closure tests across 4 test files.
- Tier 2 tests: 1469 -> 1475.
- Verified pub/sub topic isolation, storage key isolation, metadata ownership.
- Pattern: Verification-style with clear baseline, read-then-verify-then-fill approach.

**Story 5.2 (Claims-Based Command Authorization)** -- status: review:
- Verified 10 acceptance criteria for claims-based authorization.
- Confirmed test baselines: Tier 1 659, Tier 2 1466.
- Added tests for AuthorizationBehavior, ClaimsTenantValidator, AuthorizationExceptionHandler.

**Old Story 5.1** -- created the access control YAML and Aspire wiring:
- 13 tests in AccessControlPolicyTests.cs using YamlDotNet parsing via DaprYamlTestHelper.
- Comprehensive coverage: YAML structure, policies, scoping, topology consistency, trust domain, dead-letter, zero-infrastructure-access.
- **Key learning:** DAPR access control policy matching requires mTLS to extract caller identity. Without mTLS (self-hosted), `defaultAction: allow` is required because policies cannot verify callers.

### Git Intelligence

Recent commits (relevant context):
- `61e05d3` -- Update sprint status and implement various tests for command authorization and event persistence (Story 5.2/5.3)
- `fe3d99b` -- Add comprehensive tests for AuthorizationBehavior and document three-layer multi-tenant data isolation
- `91fd854` -- Enhance verification tasks for claims-based command authorization
- `6ae83e1` -- Update sprint status and implement claims-based command authorization

### Local vs Production Security Posture

**Important distinction** the dev agent must understand:
- **Local (self-hosted):** `defaultAction: allow`. DAPR self-hosted mode does not enable mTLS by default, so caller identity (SPIFFE) is unavailable. Policies are structurally validated but cannot be enforced at runtime without mTLS. The policies exist for: (1) structural validation in tests, (2) documentation of intended behavior, (3) consistency with production.
- **Production (Kubernetes):** `defaultAction: deny`. DAPR Kubernetes with Sentry enables mTLS, allowing SPIFFE-based caller identity. Policies are enforced at runtime. Unlisted app-ids are blocked.

This means the unit tests in AccessControlPolicyTests validate YAML **structure** (correct policies exist), while the E2E tests in DaprAccessControlE2ETests validate **runtime behavior** (invocation is actually denied). The Tier 2 tests run with DAPR slim (no mTLS), so access control enforcement is limited. Full enforcement verification requires Tier 3 E2E.

### Anti-Patterns to Avoid

- **DO NOT modify accesscontrol.yaml files.** The policies are correct and tested. Verify only.
- **DO NOT modify component YAML files (pubsub.yaml, statestore.yaml, etc.).** Scoping is correct. Verify only.
- **DO NOT modify DaprDomainServiceInvoker.cs.** The invocation pattern is correct. Verify only.
- **DO NOT modify AppHost/Program.cs or HexalithEventStoreExtensions.cs.** The wiring is correct. Verify only.
- **DO NOT duplicate tests that already exist.** Review the 13 existing AccessControlPolicyTests FIRST (Task 0.3), then add ONLY what's missing.
- **DO NOT add Keycloak or real OIDC testing.** That is Story 5.5 (D11).
- **DO NOT add new NuGet dependencies.** All required packages are already referenced.
- **DO NOT change access control defaultAction or policies.** These are architectural decisions (D4).
- **DO NOT use `File.ReadAllText` + `ShouldContain` for YAML tests.** Use YamlDotNet via `DaprYamlTestHelper` (matches existing pattern).

### Testing Standards

- **Framework:** xUnit 2.9.3 / Shouldly 4.3.0 / NSubstitute 5.3.0
- **YAML parsing:** YamlDotNet via `DaprYamlTestHelper` (`LoadYaml`, `Nav`, `NavList`, `GetString`, `GetComponentMetadataValue`)
- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}`
- **Tier separation:** Tier 1 (unit, no DAPR) for contract tests. Tier 2 (DAPR slim) for server/security tests. Tier 3 (full Aspire) for E2E tests.
- **Test locations:** Security tests in `tests/Hexalith.EventStore.Server.Tests/Security/`, DAPR component tests in `tests/Hexalith.EventStore.Server.Tests/DaprComponents/`

### Project Structure Notes

- No new project folders expected
- No new NuGet packages needed
- All test files in existing directories (Security/, DaprComponents/)
- New tests (if any) should be added to existing test classes, not new files

### References

- [Source: epics.md#Story-5.4] DAPR Service-to-Service Access Control acceptance criteria
- [Source: prd.md#FR34] Service-to-service access control via DAPR policies
- [Source: prd.md#NFR15] Service-to-service communication authenticated via DAPR access control
- [Source: architecture.md#D4] DAPR Access Control -- Per-App-ID Allow List
- [Source: architecture.md#D7] Domain Service Invocation -- DAPR Service Invocation
- [Source: architecture.md#Six-Layer-Auth] Layer 6: DAPR access control policies
- [Source: architecture.md#Enforcement-4] No custom retry logic -- DAPR resiliency only
- [Source: accesscontrol.yaml (local)] Local self-hosted access control Configuration
- [Source: accesscontrol.yaml (production)] Production deny-by-default Configuration
- [Source: AccessControlPolicyTests.cs] 13 existing YAML validation tests
- [Source: DaprAccessControlE2ETests.cs] 2 existing E2E tests (Tier 3)
- [Source: 5-3-three-layer-multi-tenant-data-isolation.md] Previous story verification pattern

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
