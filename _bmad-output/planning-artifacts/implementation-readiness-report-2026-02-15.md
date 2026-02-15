---
stepsCompleted: [1, 2, 3, 4, 5, 6]
date: '2026-02-15'
project_name: 'Hexalith.EventStore'
user_name: 'Jerome'
scope: 'D11 Keycloak E2E Security Testing Infrastructure'
inputDocuments:
  - prd.md
  - prd-validation-report.md
  - architecture.md
  - epics.md
  - ux-design-specification.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-02-15
**Project:** Hexalith.EventStore
**Scope:** D11 — Keycloak E2E Security Testing Infrastructure (amendment to existing architecture)

## Document Inventory

### PRD Documents
- `prd.md` (whole document)
- `prd-validation-report.md` (validation artifact)

### Architecture Documents
- `architecture.md` (whole document, amended 2026-02-15 with D11)

### Epics & Stories Documents
- `epics.md` (whole document)

### UX Design Documents
- `ux-design-specification.md` (whole document)

### Issues
- No duplicates found
- No missing required documents
- All documents are whole files (no sharded versions)

## PRD Analysis

### Functional Requirements (47 FRs)

**Command Processing (FR1-FR8):**
- FR1: Submit command via REST endpoint with tenant, domain, aggregate ID, command type, payload
- FR2: Validate command structural completeness before routing
- FR3: Route command to correct aggregate actor via identity scheme
- FR4: Return correlation ID upon command submission
- FR5: Query command processing status by correlation ID
- FR6: Replay failed commands via Command API
- FR7: Reject duplicate commands with optimistic concurrency conflict
- FR8: Route failed commands to dead-letter topic with full context

**Event Management (FR9-FR16):**
- FR9: Append-only immutable event persistence
- FR10: Strictly ordered, gapless sequence numbers per aggregate stream
- FR11: 11-field metadata envelope + payload + extensions
- FR12: Reconstruct aggregate state by replaying event stream
- FR13: Create snapshots at configurable intervals
- FR14: Reconstruct state from snapshot + subsequent events
- FR15: Composite key strategy with tenant/domain/aggregate isolation
- FR16: Atomic event writes (0 or N, never partial)

**Event Distribution (FR17-FR20):**
- FR17: Publish events via pub/sub with CloudEvents 1.0
- FR18: At-least-once delivery guarantee
- FR19: Per-tenant-per-domain topic isolation
- FR20: Continue persisting during pub/sub outages, drain on recovery

**Domain Service Integration (FR21-FR25):**
- FR21: Pure function contract `(Command, CurrentState?) -> List<DomainEvent>`
- FR22: Register domain services by tenant/domain via configuration
- FR23: Invoke registered domain service with command and current state
- FR24: Support multiple independent domains per instance
- FR25: Support multiple tenants per domain with isolated event streams

**Identity & Multi-Tenancy (FR26-FR29):**
- FR26: Canonical identity tuple derives actor IDs, keys, topics
- FR27: Data path isolation across tenants
- FR28: Storage key isolation across tenants
- FR29: Pub/sub topic isolation across tenants

**Security & Authorization (FR30-FR34):**
- FR30: JWT authentication on Command API
- FR31: Claims-based authorization (tenant + domain + command type)
- FR32: Reject unauthorized commands at API gateway
- FR33: Actor-level tenant validation
- FR34: DAPR policy-based service-to-service access control

**Observability & Operations (FR35-FR39):**
- FR35: OpenTelemetry traces spanning full command lifecycle
- FR36: Structured logs with correlation/causation IDs at each stage
- FR37: Trace failed commands from dead-letter to originating request
- FR38: Health check endpoints (DAPR sidecar, state store, pub/sub)
- FR39: Readiness check endpoints

**Developer Experience & Deployment (FR40-FR47):**
- FR40: Single Aspire command for full local topology
- FR41: Sample domain service as reference implementation
- FR42: NuGet packages for domain service development
- FR43: Environment changes via DAPR component config only
- FR44: Aspire publisher deployment manifests
- FR45: Unit tests without DAPR dependency
- FR46: Integration tests with DAPR test containers
- FR47: End-to-end contract tests across full Aspire topology

### Non-Functional Requirements (32 NFRs)

**Performance (NFR1-NFR8):**
- NFR1: Command submission < 50ms p99
- NFR2: End-to-end lifecycle < 200ms p99
- NFR3: Event append < 10ms p99
- NFR4: Actor cold activation < 50ms p99
- NFR5: Pub/sub delivery < 50ms p99
- NFR6: 1000-event replay < 100ms
- NFR7: 100 concurrent commands/sec/instance
- NFR8: DAPR sidecar overhead < 2ms p99

**Security (NFR9-NFR15):**
- NFR9: TLS 1.2+ for all API communication
- NFR10: JWT validation (signature, expiry, issuer) on every request
- NFR11: Log failed auth with request metadata, never log JWT
- NFR12: Event payload never in logs, only envelope metadata
- NFR13: Triple-layer tenant isolation (actor, DAPR, command metadata)
- NFR14: No secrets in source control
- NFR15: DAPR access control for service-to-service auth

**Scalability (NFR16-NFR20):**
- NFR16: Horizontal scaling via DAPR actor placement
- NFR17: 10K+ active aggregates per instance
- NFR18: 10+ tenants with full isolation
- NFR19: Snapshot-bounded rehydration time
- NFR20: Dynamic tenant/domain onboarding without restart

**Reliability (NFR21-NFR26):**
- NFR21: 99.9%+ availability with HA deployment
- NFR22: Zero event loss under any tested failure
- NFR23: Deterministic replay after state store recovery
- NFR24: Event delivery after pub/sub recovery
- NFR25: No duplicate persistence after actor crash (checkpointed state machine)
- NFR26: Optimistic concurrency conflict detection (409)

**Integration (NFR27-NFR32):**
- NFR27: Any DAPR state store with ETag support
- NFR28: Any DAPR pub/sub with CloudEvents + at-least-once
- NFR29: Backend switch via YAML only, zero code changes
- NFR30: Domain services via DAPR service invocation (HTTP)
- NFR31: OTLP-compatible telemetry export
- NFR32: Aspire publisher deployment (Docker Compose, K8s, ACA)

### D11-Relevant Requirements

The following requirements are directly impacted by D11 (Keycloak E2E testing):

| Requirement | D11 Relevance |
|-------------|---------------|
| FR30-FR34 | E2E tests prove the full six-layer auth pipeline with real OIDC tokens |
| FR47 | Keycloak enables true end-to-end contract tests with real IdP |
| NFR10 | Runtime proof of JWT validation via OIDC discovery (not synthetic keys) |
| NFR13 | E2E tenant isolation verification with real token-based identity |
| NFR15 | DAPR access control tested alongside real OIDC auth |

### PRD Completeness Assessment

The PRD is comprehensive — 47 FRs and 32 NFRs with clear six-layer auth model. The testing strategy (FR45-FR47) explicitly calls for three tiers including "end-to-end contract tests validating the full command lifecycle across the complete Aspire topology" (FR47). D11 directly supports FR47 by providing real OIDC infrastructure for E2E tests. No gaps identified for D11 scope.

## Epic Coverage Validation

### D11-Scoped FR Coverage Matrix

D11 provides infrastructure for testing existing security and E2E requirements. The following matrix traces D11-relevant FRs to their epic coverage:

| FR | PRD Requirement | Epic Coverage | Status |
|----|-----------------|---------------|--------|
| FR30 | JWT authentication on Command API | Epic 2, Story 2.3 | Covered |
| FR31 | Claims-based authorization (tenant + domain + command type) | Epic 2, Story 2.3 | Covered |
| FR32 | Reject unauthorized commands at API gateway | Epic 2, Story 2.3 | Covered |
| FR33 | Actor-level tenant validation | Epic 3, Story 3.3 | Covered |
| FR34 | DAPR policy-based service-to-service access control | Epic 5, Story 5.1 | Covered |
| FR47 | E2E contract tests across full Aspire topology | Epic 7, Story 7.5 | Covered |

| NFR | PRD Requirement | Epic Coverage | Status |
|-----|-----------------|---------------|--------|
| NFR10 | JWT validation (signature, expiry, issuer) on every request | Epic 2, Story 2.3 | Covered |
| NFR13 | Triple-layer tenant isolation | Epic 5, Story 5.2 | Covered |
| NFR15 | DAPR access control for service-to-service auth | Epic 5, Story 5.1 | Covered |

### Coverage Statistics

- Total PRD FRs: 47
- FRs covered in epics: 47 (per FR Coverage Map in epics.md)
- Coverage percentage: 100%
- D11-relevant FRs covered: 6/6
- D11-relevant NFRs covered: 3/3

### Gap Analysis: D11 Infrastructure Not in Existing Stories

All 47 FRs are mapped to epics and stories. However, **D11 introduces new infrastructure work that is not captured in any existing story task list**:

**Gap 1 — No story covers Keycloak container provisioning in Aspire.**
- Story 5.1 (DAPR Access Control Policies) covers policy configuration but does not mention Keycloak or OIDC IdP setup.
- Story 7.5 (E2E Contract Tests) mentions "JWT authentication and authorization flow" verification but does not specify Keycloak, OIDC discovery, or real token acquisition.
- No story includes `Aspire.Hosting.Keycloak` package addition or `builder.AddKeycloak()` wiring.

**Gap 2 — No story covers realm-as-code configuration.**
- The 5 test users (admin-user, tenant-a-user, tenant-b-user, readonly-user, no-tenant-user) with specific claims (tenants, domains, permissions) are defined in D11 but have no corresponding task in any story.
- Custom protocol mappers for multi-valued JSON claims are D11-specific and not in any story.

**Gap 3 — No story covers E2E token acquisition helpers.**
- Existing `TestJwtTokenGenerator` uses symmetric keys (Tier 2 integration tests).
- D11 requires Resource Owner Password Grant token acquisition from Keycloak (Tier 3 E2E tests).
- No story task covers building this helper or the `KeycloakTokenHelper` class.

**Gap 4 — No story covers CommandApi authority override for E2E tests.**
- E2E tests need `Authentication__Authority` pointed at the Keycloak container endpoint.
- This Aspire environment variable wiring is not in any story task list.

### Recommendation

D11 infrastructure work should be added as new tasks to **Story 5.1** (which is already in-progress and awaiting Task 7 runtime verification) and/or as a new cross-cutting story. The Keycloak infrastructure is a prerequisite for completing:
- Story 5.1 Task 7 (runtime denial verification)
- Story 5.2 (data path isolation verification with real tokens)
- Story 7.5 (E2E contract tests with JWT auth flow)

## UX Alignment Assessment

### UX Document Status

Found: `ux-design-specification.md` (complete, 14 steps, completed 2026-02-12)

### D11 UX Relevance

D11 (Keycloak E2E Security Testing Infrastructure) is a **testing infrastructure concern** with no direct UX surface. It does not add user-facing features, APIs, dashboards, or developer SDK changes. The UX specification defines four interaction surfaces (Developer SDK, REST API, CLI/Operator, Blazor Dashboard) — none are modified by D11.

### Alignment Issues

**None identified for D11 scope.** D11 validates existing UX contracts (JWT auth, RFC 7807 errors, claims-based authorization) rather than adding new UX surfaces.

### Indirect UX Validation

D11's E2E tests will exercise UX-specified behaviors:
- REST API `202 Accepted` with `Location` header and `Retry-After: 1`
- RFC 7807 Problem Details for 401/403 responses
- JWT claims transformation for tenant/domain/permission enforcement

These are validation of existing UX specification — not new UX work.

### Warnings

None. D11 is infrastructure-only with no UX impact.

## Epic Quality Review

### Scope

This review is scoped to D11-impacted epics (Epic 5: Security & Access Control, Epic 7: Testing & Deployment) and their stories relevant to Keycloak E2E testing.

### Epic Structure Validation

#### A. User Value Focus

| Epic | Title | User Value? | Assessment |
|------|-------|-------------|------------|
| Epic 5 | Multi-Tenant Security & Access Control Enforcement | Yes — security auditor can verify isolation | Pass |
| Epic 7 | Sample Application, Testing, CI/CD & Deployment | Yes — developer/DevOps can test and deploy | Pass |

Neither is a pure technical milestone. Both deliver value to named personas (security auditor, developer, DevOps engineer).

#### B. Epic Independence

- Epic 5 depends on Epics 2-4 (JWT auth, actors, pub/sub) which is correct sequential ordering.
- Epic 7 depends on Epics 1-6 (needs full system to test) which is correct for a capstone epic.
- No forward dependencies (Epic 5 does not require Epic 7; Epic 7 can run without Epic 5 albeit with reduced security test coverage).

Pass — no violations.

### Story Quality Assessment (D11-Impacted Stories)

#### Story 5.1: DAPR Access Control Policies

- **User value:** Security auditor can verify service-to-service access control. Pass.
- **Independence:** Can be completed with Epic 2-4 output. Pass.
- **Acceptance criteria:** 6 clear ACs with Given/When/Then. Pass.
- **Issue:** AC #5 ("unauthorized service-to-service calls are rejected by DAPR with appropriate error") and AC #6 ("policy violations are logged") require **runtime verification** which needs a running Aspire topology with real OIDC tokens to fully prove. This is the gap D11 fills.

#### Story 5.2: Data Path Isolation Verification

- **User value:** Security auditor can verify tenant isolation. Pass.
- **Acceptance criteria:** Clear multi-tenant test scenarios. Pass.
- **Issue:** AC #5 ("isolation verification tests exist as automated test cases") implies E2E tests but does not specify the IdP infrastructure needed to produce real tenant-scoped tokens.

#### Story 7.5: E2E Contract Tests with Aspire Topology (Tier 3)

- **User value:** Developer can verify full system before release. Pass.
- **Acceptance criteria:** 5 clear ACs. Pass.
- **Issue:** AC #3 ("tests verify JWT authentication and authorization flow") is stated but **no story task specifies how real JWT tokens are obtained**. The current implementation uses synthetic symmetric-key tokens (TestJwtTokenGenerator) which do not exercise the OIDC discovery path.

### Dependency Analysis

D11 creates a new implicit dependency:

```
Story 5.1 (Task 7) ──depends-on──> D11 Keycloak Infrastructure
Story 5.2 (E2E tests) ──depends-on──> D11 Keycloak Infrastructure
Story 7.5 (JWT auth flow) ──depends-on──> D11 Keycloak Infrastructure
```

This dependency is **not documented** in the epics document. It should be made explicit.

### Quality Findings

#### Minor Concerns

1. **Story 7.5 AC #3 underspecified**: "tests verify JWT authentication and authorization flow" does not distinguish between synthetic tokens (current Tier 2 approach) and real OIDC tokens (Tier 3 E2E approach). D11 clarifies this but the story AC should be amended.

2. **Story 5.1 missing runtime task**: The story's acceptance criteria mention runtime denial but the task list in the implementation artifact (5-1-dapr-access-control-policies.md) only partially covers it — Task 7.1 and 7.2 remain unchecked.

3. **No explicit Keycloak story**: D11 infrastructure (Keycloak container, realm config, test helpers) spans multiple stories but belongs to none. Recommend adding tasks to Story 5.1 (since it's in-progress and blocked on this) or creating a cross-cutting infrastructure sub-task.

#### No Critical Violations or Major Issues

The epic structure is sound. D11 fills a testability gap that was implicit in the original stories.

## Summary and Recommendations

### Overall Readiness Status

**READY** (with amendments)

D11 is architecturally sound and aligns with existing PRD requirements (FR30-FR34, FR47, NFR10, NFR13, NFR15). The architecture amendment is complete and the implementation approach (Aspire.Hosting.Keycloak, realm-as-code, zero auth code changes) is well-defined. The only gap is that the **epic/story task lists need updating** to capture the new Keycloak infrastructure work.

### Issues Requiring Action

1. **Story task gap (Medium):** D11 introduces 4 categories of new work (Keycloak provisioning, realm config, token helpers, authority override) not captured in any story task list. Add these as tasks to Story 5.1 or create a new sub-story.

2. **Story 7.5 AC clarification (Low):** AC #3 should distinguish Tier 2 (synthetic JWT) from Tier 3 (real OIDC via Keycloak) token strategies.

3. **Dependency documentation (Low):** Stories 5.1, 5.2, and 7.5 have an implicit dependency on D11 infrastructure that should be made explicit in the epics document.

### Recommended Next Steps

1. **Add D11 tasks to Story 5.1** — Keycloak container setup, realm-as-code, token helper, authority override wiring. This unblocks Task 7 runtime verification.
2. **Implement D11** — Package addition, AppHost wiring, realm JSON, E2E test infrastructure.
3. **Complete Story 5.1 Task 7** — Runtime denial and policy-violation logging verification using real Keycloak tokens.
4. **Extend to Stories 5.2 and 7.5** — Use the Keycloak infrastructure for tenant isolation E2E tests and full contract tests.

### Final Note

This assessment identified 3 issues across 2 categories (story task gaps, documentation). None are blocking — D11 can proceed to implementation immediately. The issues are documentation/traceability concerns that should be addressed during or after implementation to maintain requirements traceability.
