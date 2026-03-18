# Story 5.4: DAPR Service-to-Service Access Control

Status: review

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

- [x] Task 0: Verify prerequisites and baseline (BLOCKING)
  - [x] 0.1 Run all Tier 1 tests -- confirm all pass (baseline: >= 659)
  - [x] 0.2 Run Tier 2 tests `Hexalith.EventStore.Server.Tests` -- record actual pass count as baseline. **Baseline note:** Use the actual count from this run as your baseline for Task 5.3. Do NOT reconcile with historical baselines from other stories.
  - [x] 0.3 Inventory existing access control test files and counts:
    - `AccessControlPolicyTests.cs` -- count tests (expected: 13)
    - `DaprComponentValidationTests.cs` -- count tests
    - `ProductionDaprComponentValidationTests.cs` -- count tests
    - `DaprAccessControlE2ETests.cs` -- count tests (expected: 2, Tier 3)
  - [x] 0.4 Read `accesscontrol.yaml` (local) -- verify structure matches AC #1, #2, #3
  - [x] 0.5 Read `accesscontrol.yaml` (production) -- verify deny-by-default, SPIFFE trust domain (AC #4)

- [x] Task 1: Verify commandapi policy (AC: #1, #3)
  - [x] 1.1 Confirm local `accesscontrol.yaml`: commandapi entry has `defaultAction: deny`, wildcard `/**` path, `httpVerb: ['POST']`, `action: allow`
  - [x] 1.2 Confirm production `accesscontrol.yaml`: same commandapi policy structure with env-parameterized `trustDomain` and `namespace`
  - [x] 1.3 Review `AccessControlPolicyTests.LocalAccessControlYaml_CommandApiPolicy_AllowsRequiredOperations` -- verify it checks wildcard path, POST verb, allow action
  - [x] 1.4 Review `AccessControlPolicyTests.AllDaprComponents_LocalAndProduction_PolicyTopologyConsistent` -- verify local-production consistency is tested
  - [x] 1.5 Identify test gaps for commandapi policy. Potential gaps:
    - [x] 1.5.1 Test: `CommandApiPolicy_OnlyAllowsPOST_OtherVerbsBlocked` -- verify the policy does NOT list GET/PUT/DELETE in httpVerb (defense-in-depth: DaprClient.InvokeMethodAsync uses POST only)
    - [x] 1.5.2 Test: `ProductionAccessControlYaml_CommandApiPolicy_AllowsRequiredOperations` -- verify production commandapi policy mirrors local. LIKELY COVERED by test #9 (`AllDaprComponents_LocalAndProduction_PolicyTopologyConsistent`) which checks production commandapi wildcard POST. Verify test #9 covers this and mark as covered if so -- do NOT write a duplicate test.
    - [x] 1.5.3 Test: `AccessControlYaml_LocalAllowAndProductionDeny_IntentionalDivergence` -- verify in a SINGLE test that local `defaultAction` is `allow` AND production `defaultAction` is `deny`. This is stronger than checking each separately (tests #2 and #6 do that). A single assertion guards against accidental synchronization -- if someone copies production config to local (or vice versa), this catches it. (RT-2 finding)

- [x] Task 2: Verify domain service denial (AC: #2)
  - [x] 2.1 Confirm local `accesscontrol.yaml`: sample entry has `defaultAction: deny`, no `operations` key
  - [x] 2.2 Confirm production `accesscontrol.yaml`: sample is omitted (relies on global deny-by-default)
  - [x] 2.3 Review `AccessControlPolicyTests.LocalAccessControlYaml_SamplePolicy_DeniesDirectInvocation` -- verify deny + no operations
  - [x] 2.4 Review `AccessControlPolicyTests.DomainServicePolicy_ZeroInfrastructureAccess_AllDenied` -- verify scoping exclusions
  - [x] 2.5 Identify test gaps for domain service denial. Potential gaps:
    - [x] 2.5.1 Test: `ProductionAccessControlYaml_OnlyCommandApiHasAllowedOperations` -- verify that in production accesscontrol.yaml, ONLY the commandapi policy has allowed operations. Any other policy entry with `operations` defined is a regression. This is a forward-looking guard: if a future developer adds a domain service policy with operations, this test catches it.
    - [x] 2.5.2 CODE REVIEW (not a test): Verify `AppHost/Program.cs:62-70` -- sample sidecar is configured WITHOUT `.WithReference(stateStore)` or `.WithReference(pubSub)`. This validates "zero infrastructure access" beyond component scoping. Confirm by reading the code -- do NOT write a unit test for this (it's an Aspire builder API call, not testable without integration test infrastructure).

- [x] Task 3: Verify Aspire topology wiring (AC: #5)
  - [x] 3.1 CODE REVIEW: Read `AppHost/Program.cs` and `HexalithEventStoreExtensions.cs` -- confirm: (a) `accessControlConfigPath` is resolved and validated with `File.Exists` + `FileNotFoundException` guard (lines 9-20), (b) commandapi sidecar loads config via `AddHexalithEventStore` -> `Config = daprConfigPath` (HexalithEventStoreExtensions.cs:50), (c) sample sidecar loads config via `WithDaprSidecar` -> `Config = accessControlConfigPath` (Program.cs:69), (d) `AddHexalithEventStore` accepts nullable `daprConfigPath` parameter and passes it to sidecar options. This is a single code-review pass over 2 files -- do NOT write tests for Aspire builder wiring.
  - [x] 3.2 Identify test gaps for Aspire wiring. Potential gaps:
    - [x] 3.2.1 CODE REVIEW (not a test): The `File.Exists` guard (Program.cs:9-20) is a 6-line runtime check with obvious correctness. Confirm by reading -- do NOT write a test (requires mocking file system for negligible risk).

- [x] Task 4: Verify DaprDomainServiceInvoker compliance (AC: #6)
  - [x] 4.1 Read `DaprDomainServiceInvoker.cs` -- confirm `InvokeMethodAsync<TRequest, TResponse>` usage (POST by default)
  - [x] 4.2 Confirm no GET/PUT/DELETE overloads used for service invocation
  - [x] 4.3 Confirm error handling catches access control denials -- `DaprDomainServiceInvoker.cs:62-76` has a generic `catch (Exception ex) when (ex is not OperationCanceledException)` that wraps failures in `DomainServiceException`. Verify this path handles DAPR access control denials (`DaprApiException` with `PermissionDenied` or `Grpc.Core.RpcException` with `StatusCode.PermissionDenied`) gracefully. Check if `DaprDomainServiceInvokerTests` has a test for the exception path -- if not, note as a gap.
  - [x] 4.4 Identify test gaps for invoker compliance. Potential gaps:
    - [x] 4.4.1 Test: `DaprDomainServiceInvoker_UsesPostHttpVerb_ConsistentWithAccessControlPolicy` -- verify InvokeMethodAsync is called (mocked) confirming POST-only invocation pattern. NOTE: `DaprClient.InvokeMethodAsync<TReq, TRes>(appId, method, request)` uses POST by default. This is a design-documentation test rather than a behavioral gap. If existing `DaprDomainServiceInvokerTests` already verify the call pattern, mark as covered.
    - [x] 4.4.2 Verify: `DaprDomainServiceInvoker_AccessControlDenial_WrappedInDomainServiceException` -- confirm that when DAPR returns a permission-denied error (access control policy blocks the call), the invoker catches it and wraps it in `DomainServiceException` with meaningful context (tenant, domain, appId). If existing tests cover this exception path, mark as covered. If not, add a test using NSubstitute to mock `DaprClient.InvokeMethodAsync` throwing an exception.
    - [x] 4.4.3 Review: Check whether `DaprDomainServiceInvoker` error logging (line 63-70) should distinguish access control denials from transient failures. Currently logged as generic "Domain service invocation failed" without a `SecurityEvent` tag. Compare with `AuthorizationBehavior` which emits `SecurityEvent=AuthorizationDenied`. If DAPR denials can be identified by exception type (e.g., `Grpc.Core.RpcException` with `StatusCode.PermissionDenied`), consider logging them with `SecurityEvent=DaprAccessControlDenied` for security audit trail. NOTE: This is an ENHANCEMENT -- if the exception type is not reliably distinguishable, document as accepted and move on. Do NOT spend more than 15 minutes on this. (SA-2 finding)

- [x] Task 5: Final verification
  - [x] 5.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- zero warnings, zero errors
  - [x] 5.2 Run all Tier 1 tests -- confirm pass count (baseline: >= 659)
  - [x] 5.3 Run all Tier 2 tests -- confirm pass count (baseline: use actual count from Task 0.2)
  - [x] 5.4 Confirm all 7 acceptance criteria are satisfied
  - [x] 5.5 Report final test count delta
  - [x] 5.6 Write verification report in Completion Notes (test inventory per file, gaps found, new tests added, deviations observed)

**Effort guidance:** The access control infrastructure and tests are already comprehensive (13 unit tests + 2 E2E tests). Expect ~1-2 hours for verification and minor gap-closure. Most work is reading and confirming existing tests cover the ACs. Expected new tests: 1-3 max (POST-only verb guard, production operations guard, possibly invoker exception path). Several tasks are CODE REVIEW only (no test needed) -- these are explicitly marked. Do NOT write tests for code-review-only tasks.

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

- **Why Wildcard Path `/**` (ADR rationale):**
  - Domain service method names are dynamically resolved from the DAPR config store registration (`tenant:domain:version -> appId + method`). Listing specific paths in `accesscontrol.yaml` would require YAML updates every time a domain service changes or adds endpoints.
  - Wildcard + POST-only is sufficient because: (1) `DaprClient.InvokeMethodAsync` uses POST exclusively, (2) GET/PUT/DELETE are blocked by the httpVerb restriction, (3) domain services are developer-controlled (not untrusted external services).
  - Do NOT question or change the wildcard -- it is an intentional architectural decision (D4).

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

### NFR15 Compliance Boundary

**NFR15 ("Service-to-service communication must be authenticated and authorized via DAPR access control policies")** is **fully met in production only**. Self-hosted mode provides structural validation of policies but NOT runtime authentication (no mTLS = no caller identity verification). This is by design -- self-hosted DAPR does not enable mTLS by default. The Tier 2 unit tests validate policy STRUCTURE; the Tier 3 E2E tests validate policy ENFORCEMENT. NFR15 runtime compliance depends on production Kubernetes with DAPR Sentry for mTLS.

### Local vs Production Security Posture

**Important distinction** the dev agent must understand:
- **Local (self-hosted):** `defaultAction: allow`. DAPR self-hosted mode does not enable mTLS by default, so caller identity (SPIFFE) is unavailable. Policies are structurally validated but cannot be enforced at runtime without mTLS. The policies exist for: (1) structural validation in tests, (2) documentation of intended behavior, (3) consistency with production.
- **Production (Kubernetes):** `defaultAction: deny`. DAPR Kubernetes with Sentry enables mTLS, allowing SPIFFE-based caller identity. Policies are enforced at runtime. Unlisted app-ids are blocked.

This means the unit tests in AccessControlPolicyTests validate YAML **structure** (correct policies exist), while the E2E tests in DaprAccessControlE2ETests validate **runtime behavior** (invocation is actually denied). The Tier 2 tests run with DAPR slim (no mTLS), so access control enforcement is limited. Full enforcement verification requires Tier 3 E2E.

**Scope boundary:** DAPR access control protects **sidecar-mediated communication only**. If a pod reaches another service directly (bypassing the sidecar), access control policies are irrelevant. Preventing direct pod-to-pod communication requires Kubernetes NetworkPolicy restricting pod ingress/egress to the DAPR sidecar. This is an infrastructure concern outside EventStore's application boundary -- do NOT attempt to test or enforce this in application code.

### Adding New Domain Services (Operational Procedure)

When a new domain service is added to the EventStore topology, the following access control checklist MUST be followed:
1. Add a policy entry in BOTH `accesscontrol.yaml` files (local + production) with `defaultAction: deny` and NO allowed operations.
2. Do NOT add the new app-id to state store scopes, pub/sub scopes, publishingScopes, or subscriptionScopes (zero infrastructure access, D4).
3. In AppHost, do NOT wire `.WithReference(stateStore)` or `.WithReference(pubSub)` to the new sidecar.
4. The commandapi wildcard policy (`/**` POST) already covers invocation to the new service -- no commandapi policy changes needed.
5. Run `AccessControlPolicyTests` to verify YAML structure after changes.

This procedure is documented here because the existing `accesscontrol.yaml` comments describe it, but a deployment-time omission could leave a new service without an explicit audit trail in the access control YAML. The guard test `ProductionAccessControlYaml_OnlyCommandApiHasAllowedOperations` (Task 2.5.1) catches accidental operation grants but does not verify that every deployed service has an explicit deny policy.

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

### Advanced Elicitation Findings

**5-method analysis applied to story content:**

1. **Red Team vs Blue Team** (5 attack vectors): Identified mTLS downgrade via config copy-paste (RT-2) as testable gap -- local-vs-production `defaultAction` divergence guard test added (Task 1.5.3). Sidecar bypass (RT-1), app-ID spoofing (RT-3), wildcard exploitation (RT-4), and component metadata injection (RT-5) accepted as infrastructure-level or by-design risks.
2. **Security Audit Personas** (Auditor + Hacker): Auditor identified NFR15 compliance as production-only (SA-1) -- Dev Note added. Hacker identified DAPR access control denials not logged with `SecurityEvent` tag (SA-2) -- Task 4.4.3 added as time-boxed enhancement review.
3. **Failure Mode Analysis** (4 scenarios): Sidecar crash (FM-1, handled by resiliency), config parse failure (FM-2, accepted DAPR platform risk), policy precedence (FM-3, prevented by structural simplicity), trust domain mismatch (FM-4, deployment config issue caught by E2E).
4. **Pre-mortem Analysis** (3 future failure scenarios): New domain service without explicit deny policy (PM-1) -- operational procedure Dev Note added. Component scoping drift (PM-2) noted as future enhancement. PublishingScopes format error (PM-3) covered by Story 5.3 tests.
5. **Chaos Monkey Scenarios** (4 scenarios): Runtime config deletion (CM-1, DAPR behavior), concurrent invocation flood (CM-2, negligible overhead), production config in self-hosted (CM-3, documented), YAML corruption (CM-4, caught by CI tests). No new gaps.

**Impact (Round 1):** 1 new test gap added (Task 1.5.3: defaultAction divergence guard). 1 new enhancement review added (Task 4.4.3: security audit logging for DAPR denials). 2 Dev Notes added (NFR15 compliance boundary, new domain service operational procedure). 5 findings accepted as covered or out-of-scope infrastructure risks.

**Round 2 (5 additional methods):**

6. **Self-Consistency Validation** (3 reviewers): All 7 ACs mapped to tasks with full coverage. AC #7 (component scoping) initially flagged as partial but resolved -- test #13 (`DomainServicePolicy_ZeroInfrastructureAccess_AllDenied`) comprehensively covers all 3 scoping layers. No inconsistency found.
7. **Occam's Razor** (complexity audit): Task 3 subtasks T3.1-T3.4 overly granular for a code-review pass over 2 files. Merged into single subtask T3.1 (reduces from 5 subtasks to 2). No coverage lost.
8. **Architecture Decision Records** (3 ADRs): ADR-1 (local allow vs production deny) well-documented. ADR-2 (wildcard `/**` path) rationale missing from Dev Notes -- added to prevent dev agent confusion. ADR-3 (explicit deny for sample) correctly documented.
9. **Mentor and Apprentice** (3 naive questions): Surfaced scope boundary assumption -- DAPR access control protects sidecar-mediated communication only; direct pod-to-pod requires Kubernetes NetworkPolicy. Added to "Local vs Production Security Posture" section.
10. **Comparative Analysis Matrix** (7 ACs x 4 dimensions): Average score 3.4/4. Weakest dimension: runtime E2E enforcement (appropriately deferred to Story 5.5). No actionable gaps.

**Impact (Round 2):** Task 3 simplified (4 subtasks merged to 1). 2 Dev Notes added (wildcard path ADR rationale, sidecar-only scope boundary). No new test gaps.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

### Completion Notes List

**Verification Report — Story 5.4: DAPR Service-to-Service Access Control**

**Baselines:**
- Tier 1: 659 passed (267 Contracts + 293 Client + 32 Sample + 67 Testing)
- Tier 2: 1479 passed

**Test Inventory (pre-verification):**
- `AccessControlPolicyTests.cs`: 14 tests (story expected 13; 14th is `AccessControlYaml_HasNamespace_IdentityConfigured`)
- `DaprComponentValidationTests.cs`: 12 methods / 19 test cases (Theory data)
- `ProductionDaprComponentValidationTests.cs`: 13 methods / 19 test cases (Theory data)
- `DaprAccessControlE2ETests.cs`: 2 tests (Tier 3)

**Gaps Found & Resolution:**
1. **Task 1.5.1** — ADDED: `CommandApiPolicy_OnlyAllowsPOST_OtherVerbsBlocked` — verifies commandapi wildcard lists exactly 1 verb (POST) in both local and production configs. Defense-in-depth guard.
2. **Task 1.5.2** — COVERED by test #9 (`AllDaprComponents_LocalAndProduction_PolicyTopologyConsistent`) + `ProductionAccessControl_CommandApiCanPostOnly`. No duplicate test written.
3. **Task 1.5.3** — ADDED: `AccessControlYaml_LocalAllowAndProductionDeny_IntentionalDivergence` — single test asserting local=allow AND production=deny with explicit divergence guard (RT-2 finding).
4. **Task 2.5.1** — ADDED: `ProductionAccessControlYaml_OnlyCommandApiHasAllowedOperations` — forward-looking guard that no non-commandapi policy has operations in production.
5. **Task 2.5.2** — CODE REVIEW confirmed: Program.cs:62-70 sample sidecar does NOT reference stateStore/pubSub.
6. **Task 4.4.1** — COVERED by design: `DaprClient.InvokeMethodAsync<TReq,TRes>` is non-virtual (cannot mock with NSubstitute). Tested at actor level via `IDomainServiceInvoker` interface mock.
7. **Task 4.4.2** — ACCEPTED limitation: same non-virtual constraint. Generic catch at line 62 wraps all non-cancellation exceptions in `DomainServiceException`.
8. **Task 4.4.3** — ACCEPTED enhancement: DAPR access control denial exception type (`InvocationException`/`DaprApiException`) is not reliably distinguishable from transient failures without adding DAPR gRPC dependency. Logged as generic "Domain service invocation failed." Future enhancement if DAPR SDK exposes typed denial exceptions.

**Final Counts:**
- Tier 1: 659 passed (unchanged)
- Tier 2: 1482 passed (+3 new tests)
- Build: 0 warnings, 0 errors

**AC Satisfaction:**
- AC #1: Verified — commandapi policy in both configs has `defaultAction: deny`, wildcard `/**`, POST-only, `action: allow`
- AC #2: Verified — sample has `defaultAction: deny`, no operations (local); omitted (production, global deny)
- AC #3: Verified — both configs are `Configuration` CRD (`apiVersion: dapr.io/v1alpha1`, `kind: Configuration`) with per-app-id policies and `allowedOperations`
- AC #4: Verified — production `defaultAction: deny`, SPIFFE trust domain `{env:DAPR_TRUST_DOMAIN|hexalith.io}`
- AC #5: Verified — AppHost resolves and validates `accesscontrol.yaml` with `File.Exists`/`FileNotFoundException`, wires to both commandapi and sample sidecars via `Config` property
- AC #6: Verified — `DaprDomainServiceInvoker` uses `InvokeMethodAsync<TRequest, TResponse>` (POST by default), no other HTTP verb overloads
- AC #7: Verified — state store scopes contain only `commandapi`; pub/sub scopes include `commandapi` + authorized subscribers; publishingScopes/subscriptionScopes deny sample; production configs consistent

**Deviations:**
- AccessControlPolicyTests count was 14 (not 13 as story expected) — the 14th test (`AccessControlYaml_HasNamespace_IdentityConfigured`) was present pre-verification.

### Change Log

- 2026-03-18: Story 5.4 verification complete. Added 3 gap-closure tests to AccessControlPolicyTests.cs: POST-only verb guard, local/production divergence guard, production operations guard. All 7 ACs verified. Tier 2: 1479 -> 1482.

### File List

- `tests/Hexalith.EventStore.Server.Tests/Security/AccessControlPolicyTests.cs` (modified — 3 new tests added)
