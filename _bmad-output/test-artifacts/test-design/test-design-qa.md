---
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
lastSaved: '2026-03-29'
workflowType: 'testarch-test-design'
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/implementation-artifacts/3-6-adr-round2-domain-authorization-and-tenant-offboarding.md
  - _bmad-output/test-artifacts/traceability-report.md
---

# Test Design for QA: Hexalith.EventStore v1

**Purpose:** Test execution recipe. Defines what to test, how to test it, and what QA needs from other teams.

**Date:** 2026-03-29
**Author:** Murat (TEA Agent)
**Status:** Draft
**Project:** Hexalith.EventStore

**Related:** See Architecture doc (`test-design-architecture.md`) for testability concerns and architectural blockers.

---

## Executive Summary

**Scope:** System-level test design covering the command-to-event pipeline, query/projection caching, multi-tenant isolation, infrastructure portability, and operational resilience for Hexalith.EventStore v1.

**Risk Summary:**

- Total Risks: 10 (3 high-priority score >=6, 5 medium, 2 low)
- Critical Categories: SEC (auth verification), DATA (envelope freeze), OPS (chaos testing)

**Coverage Summary:**

- P0 tests: ~10 (core pipeline, security, data integrity)
- P1 tests: ~13 (infrastructure swap, chaos recovery, query pipeline)
- P2 tests: ~10 (secondary flows, edge cases)
- P3 tests: ~8 (benchmarks, scale tests)
- **Total**: ~41 scenarios (~2-4 weeks solo developer)

---

## Not in Scope

| Item | Reasoning | Mitigation |
|------|-----------|------------|
| **v2 Admin Blazor UI** | v2 scope; Blazor components not part of v1 GA | Admin UI test files tracked in git but not validated in this plan |
| **v2 Admin CLI/MCP** | v2 scope | CLI/MCP test files exist but are out of v1 test design scope |
| **GDPR/data retention** | Deferred to Story 8.x (ADR-6) | Operational runbooks document manual procedures |
| **gRPC Command API** | Deferred to v4 | REST-only in v1 |
| **External authorization (OpenFGA/OPA)** | Deferred to v2 | JWT six-layer auth sufficient for v1 |

---

## Dependencies & Test Blockers

**CRITICAL:** Testing cannot proceed without these items.

### Backend/Architecture Dependencies (Pre-Implementation)

**Source:** See Architecture doc "Quick Guide" for detailed mitigation plans.

1. **FR65 metadataVersion implementation** — Dev — Current sprint
   - QA needs `MetadataVersion` property on `EventMetadata` to write envelope validation tests
   - Blocks P0 envelope testing (TD-P0-008)

2. **D11 Keycloak in Aspire** — Dev — Pre-GA
   - QA needs real IdP in Aspire topology for E2E auth tests
   - Blocks P0 auth testing (TD-P0-001, TD-P0-002)

3. **Chaos test harness** — Dev — v1.0
   - QA needs programmatic control to kill/restart DAPR containers during test runs
   - Blocks P1 chaos recovery tests (TD-P1-003, TD-P1-004)

### QA Infrastructure Setup

1. **Performance benchmark project** — Dev/QA
   - k6 or NBomber project targeting Tier 3 Aspire topology
   - Baseline NFR1-NFR8 latency targets

2. **PostgreSQL DAPR component variant** — DevOps
   - Alternative DAPR component YAML for PostgreSQL state store + pub/sub
   - Required for infrastructure swap test (TD-P1-002)

---

## Risk Assessment

**Note:** Full risk details in Architecture doc. This section summarizes risks relevant to QA test planning.

### High-Priority Risks (Score >=6)

| Risk ID | Category | Description | Score | QA Test Coverage |
|---------|----------|-------------|-------|-----------------|
| **R-001** | SEC | 6-layer auth not E2E verified | **6** | TD-P0-001 (JWT auth E2E), TD-P0-002 (cross-tenant isolation), TD-P1-010 (claims transformation) |
| **R-002** | DATA | Envelope freeze without metadataVersion | **6** | TD-P0-008 (metadataVersion field unit tests) |
| **R-004** | OPS | Zero-data-loss claim unverified | **6** | TD-P1-003 (state store crash recovery), TD-P1-004 (pub/sub outage recovery), TD-P3-007 (actor rebalancing) |

### Medium/Low-Priority Risks

| Risk ID | Category | Description | Score | QA Test Coverage |
|---------|----------|-------------|-------|-----------------|
| R-003 | PERF | Performance KPIs unvalidated | 4 | TD-P3-001 through TD-P3-003 (benchmarks) |
| R-005 | TECH | Backend portability untested | 4 | TD-P1-002 (Redis -> PostgreSQL swap) |
| R-006 | SEC | DAPR ACL policies untested | 4 | TD-P1-001 (ACL enforcement) |
| R-007 | DATA | Snapshot divergence at scale | 3 | TD-P3-006 (1000-event stress test) |
| R-008 | BUS | NFR coverage at 51% | 4 | Addressed across P1/P2 scenarios |
| R-009 | TECH | Admin test quality unverified | 2 | Out of v1 scope |
| R-010 | OPS | Tenant offboarding untested | 2 | TD-P2-009 (config deletion graceful degradation) |

---

## Entry Criteria

- [ ] FR65 (metadataVersion) implemented and unit-tested
- [ ] D11 Keycloak resource available in Aspire AppHost
- [ ] PostgreSQL DAPR component YAML ready for Tier 3
- [ ] Chaos test harness can kill/restart DAPR containers programmatically
- [ ] All Tier 1 + Tier 2 tests currently passing

## Exit Criteria

- [ ] All P0 tests passing (100%)
- [ ] P1 tests >= 95% passing
- [ ] No open high-severity bugs
- [ ] R-001, R-002, R-004 mitigations verified
- [ ] FR coverage >= 85% (up from 69.8%)
- [ ] NFR coverage >= 65% (up from 51%)

---

## Test Coverage Plan

**IMPORTANT:** P0/P1/P2/P3 = **priority and risk level**, NOT execution timing. See "Execution Strategy" for when tests run.

### P0 (Critical)

**Criteria:** Blocks core functionality + High risk (>=6) + No workaround

| Test ID | Requirement | Test Level | Risk Link | Notes |
|---------|-------------|------------|-----------|-------|
| **TD-P0-001** | E2E JWT auth: valid token accepted, multi-tenant claims enforced | Integration (Tier 3) | R-001 | Keycloak in Aspire; validates 6-layer defense |
| **TD-P0-002** | Cross-tenant isolation: tenant A command never reaches tenant B | Integration (Tier 3) | R-001 | FR27-FR29, SEC-1 through SEC-3 |
| **TD-P0-003** | Command -> Event -> Pub/Sub full lifecycle (happy path) | Integration (Tier 3) | — | FR1-FR4, FR17, core pipeline |
| **TD-P0-004** | Event append atomicity: 0 or N events, never partial | Unit + Integration | R-004 | FR16, actor state machine |
| **TD-P0-005** | Optimistic concurrency: concurrent commands -> 409 | Integration (Tier 2) | — | FR7, NFR26 |
| **TD-P0-006** | Idempotency: duplicate command -> idempotent success | Integration (Tier 2) | — | FR49 |
| **TD-P0-007** | State rehydration: snapshot + tail = full replay | Unit + Integration | R-007 | FR14, snapshot invariant |
| **TD-P0-008** | metadataVersion field in event envelope | Unit | R-002 | FR65, pre-GA blocker |
| **TD-P0-009** | Dead-letter routing with full context | Unit + Integration | — | FR8, FR37 |
| **TD-P0-010** | Command status lifecycle: Received -> Completed/Rejected | Integration (Tier 2) | — | FR5, D2 |

**Total P0:** ~10 scenarios

---

### P1 (High)

**Criteria:** Critical paths + Medium/high risk + Common workflows

| Test ID | Requirement | Test Level | Risk Link | Notes |
|---------|-------------|------------|-----------|-------|
| **TD-P1-001** | DAPR ACL: domain service cannot call EventStore directly | Integration (Tier 3) | R-006 | D4, NFR15 |
| **TD-P1-002** | Infrastructure swap: Redis -> PostgreSQL, tests pass | Integration (Tier 3) | R-005 | NFR27, NFR29 |
| **TD-P1-003** | Crash recovery: state store dies -> checkpoint resumes, zero loss | Integration (Tier 3) | R-004 | NFR22, NFR23, NFR25 |
| **TD-P1-004** | Pub/sub outage: events persist, backlog drains on recovery | Integration (Tier 3) | R-004 | NFR24, persist-then-publish |
| **TD-P1-005** | Domain service error contract: rejection events persisted | Unit + Integration | — | D3, FR21 |
| **TD-P1-006** | Rate limiting: per-tenant + per-consumer -> 429 | Integration (Tier 2) | — | NFR33, NFR34 |
| **TD-P1-007** | ETag pre-check: valid ETag -> 304, stale -> query actor | Integration (Tier 2) | — | FR51, FR53, FR54 |
| **TD-P1-008** | SignalR projection changed notification delivery | Integration (Tier 2) | — | FR55, FR56, FR59 |
| **TD-P1-009** | Convention-based domain service routing (zero config) | Integration (Tier 2) | — | FR22 |
| **TD-P1-010** | Claims transformation: tenant/domain/permission extraction | Unit | R-001 | EventStoreClaimsTransformation |
| **TD-P1-011** | Health/readiness endpoints: DAPR component status | Integration (Tier 2) | — | FR38, FR39 |
| **TD-P1-012** | Aggregate tombstoning: terminal event -> commands rejected | Unit + Integration | — | FR66 |
| **TD-P1-013** | Per-aggregate backpressure: 429 on queue depth exceeded | Integration (Tier 2) | — | FR67 |

**Total P1:** ~13 scenarios

---

### P2 (Medium)

**Criteria:** Secondary features + Low risk + Edge cases

| Test ID | Requirement | Test Level | Risk Link | Notes |
|---------|-------------|------------|-----------|-------|
| **TD-P2-001** | Multi-domain processing: 2+ domains in single instance | Integration (Tier 2) | — | FR24 |
| **TD-P2-002** | Config store override: per-tenant routing | Integration (Tier 2) | — | FR22 override path |
| **TD-P2-003** | Extension metadata sanitization | Unit | — | SEC-4, GAP-11 |
| **TD-P2-004** | Structured log contract: correlation/causation at every stage | Integration (Tier 2) | — | FR36, NFR12 |
| **TD-P2-005** | CloudEvents 1.0 envelope format compliance | Unit | — | FR17 |
| **TD-P2-006** | Command validation: malformed ULID, invalid commandType -> 400 | Unit | — | FR2 |
| **TD-P2-007** | Query actor cache: cold -> warm -> deactivation -> re-learn | Integration (Tier 2) | — | FR54, FR63 |
| **TD-P2-008** | Self-routing ETag encoding/decoding (RFC 7232) | Unit | — | FR61 |
| **TD-P2-009** | Tenant config deletion -> DomainServiceNotFoundException | Integration (Tier 2) | R-010 | ADR-6 graceful degradation |
| **TD-P2-010** | Dynamic tenant/domain registration without restart | Integration (Tier 2) | — | NFR20 |

**Total P2:** ~10 scenarios

---

### P3 (Low)

**Criteria:** Nice-to-have + Benchmarks + Exploratory

| Test ID | Requirement | Test Level | Notes |
|---------|-------------|------------|-------|
| **TD-P3-001** | Command submission p99 < 50ms | Benchmark (k6/NBomber) | NFR1 |
| **TD-P3-002** | E2E lifecycle p99 < 200ms | Benchmark | NFR2 |
| **TD-P3-003** | 100 cmd/sec sustained throughput | Benchmark | NFR7 |
| **TD-P3-004** | 10,000 active aggregates scale test | Benchmark | NFR17 |
| **TD-P3-005** | 10 tenants, no cross-tenant perf interference | Benchmark | NFR18 |
| **TD-P3-006** | Snapshot stress: 1000-event aggregate replay equivalence | Integration | FR14 at scale |
| **TD-P3-007** | Actor rebalancing under load: zero command loss | Chaos | NFR22 |
| **TD-P3-008** | DAPR sidecar overhead < 2ms p99 per call | Benchmark | NFR8 |

**Total P3:** ~8 scenarios

---

## Execution Strategy

**Philosophy:** Run everything in PRs if < 15 minutes. Defer only if expensive or long-running.

### Every PR: xUnit Tests (~5-10 min)

**All functional tests** (Tier 1 + Tier 2):

- All unit tests (P0-P2 unit-level scenarios)
- All DAPR slim integration tests (Tier 2)
- Parallelized via xUnit test collections
- Total: ~25-30 xUnit test projects

**Why run in PRs:** Fast feedback, DAPR slim init is lightweight, no Docker required for Tier 1

### Nightly: Aspire Topology Tests (~30-60 min)

**Tier 3 integration tests** (requires full DAPR init + Docker):

- E2E auth with Keycloak (TD-P0-001, TD-P0-002)
- Infrastructure swap: PostgreSQL variant (TD-P1-002)
- Full pipeline lifecycle (TD-P0-003)
- DAPR ACL enforcement (TD-P1-001)

**Why defer to nightly:** Requires Docker, full DAPR init, Keycloak container startup

### Weekly: Performance + Chaos (~1-2 hours)

**Expensive infrastructure tests:**

- k6/NBomber benchmarks against Aspire topology (TD-P3-001 through TD-P3-005, TD-P3-008)
- Chaos test suite: kill state store, kill pub/sub, rebalance actors (TD-P1-003, TD-P1-004, TD-P3-007)
- Scale tests: 10K aggregates, 10 tenants (TD-P3-004, TD-P3-005)
- Snapshot stress: 1000-event aggregate (TD-P3-006)

**Why defer to weekly:** Long-running, resource-intensive, infrequent validation sufficient

---

## QA Effort Estimate

**QA test development effort only** (solo developer model):

| Priority | Count | Effort Range | Notes |
|----------|-------|-------------|-------|
| P0 | ~10 | ~1-2 weeks | Many partially covered; focus on R-001, R-002 gaps |
| P1 | ~13 | ~1.5-2.5 weeks | Chaos harness + infra swap are heavy lifts |
| P2 | ~10 | ~0.5-1 week | Mostly unit-level, some integration |
| P3 | ~8 | ~1-2 weeks | Benchmark infrastructure setup is main cost |
| **Total** | ~41 | **~2-4 weeks** | **Solo developer, full-time** |

**Assumptions:**

- Includes test design, implementation, debugging, CI integration
- Excludes ongoing maintenance (~10% effort)
- Assumes FR65 implemented and D11 Keycloak available before P0 work begins
- k6/NBomber setup is one-time cost included in P3 estimate

---

## Implementation Planning Handoff

| Work Item | Owner | Target | Dependencies/Notes |
|-----------|-------|--------|-------------------|
| Implement FR65 (metadataVersion) | Dev | Current sprint | Blocks TD-P0-008 |
| Set up Keycloak in Aspire (D11) | Dev | Pre-GA | Blocks TD-P0-001, TD-P0-002 |
| Create PostgreSQL DAPR component YAML | DevOps | Pre-GA | Blocks TD-P1-002 |
| Build chaos test harness (container kill/restart) | Dev | v1.0 | Blocks TD-P1-003, TD-P1-004 |
| Create k6/NBomber benchmark project | Dev/QA | v1.0 | Blocks TD-P3-001 through TD-P3-008 |
| Add Tier 3 auth integration tests | QA | Pre-GA | Depends on D11 |
| Add PostgreSQL CI matrix variant | DevOps | Pre-GA | Depends on DAPR component YAML |

---

## Interworking & Regression

| Service/Component | Impact | Regression Scope | Validation Steps |
|-------------------|--------|-----------------|-----------------|
| **DAPR Runtime (1.17.x)** | All building blocks depend on DAPR sidecar | All Tier 2 + Tier 3 tests | Upgrade DAPR SDK -> run full test suite |
| **Aspire (13.1.x)** | AppHost orchestration | Tier 3 E2E tests | Upgrade Aspire -> run Tier 3 suite |
| **Domain Service Sample** | Reference implementation for testing | Sample.Tests (Tier 1) | Any contract change -> verify sample passes |
| **Hexalith.Tenants (v2)** | Peer service for tenant lifecycle | Out of v1 scope | Validated separately when admin tooling ships |

**Regression test strategy:**

- All Tier 1 tests must pass on every PR (enforced by CI)
- All Tier 2 tests must pass on every PR (enforced by CI with DAPR slim)
- Tier 3 tests run nightly; failures block next day's merges
- DAPR or Aspire version upgrades trigger full Tier 1+2+3 validation

---

## Appendix A: Code Examples & Tagging

**xUnit test tagging for selective execution:**

```csharp
// P0 critical test
[Trait("Priority", "P0")]
[Trait("Category", "Security")]
public class CrossTenantIsolationTests
{
    [Fact]
    public async Task TenantA_Command_NeverReaches_TenantB_Actor()
    {
        // Arrange: submit command for tenant-a
        // Act: verify tenant-b actor never activated
        // Assert: tenant-b event stream empty
    }
}

// P1 integration test
[Trait("Priority", "P1")]
[Trait("Category", "Infrastructure")]
public class PostgreSqlBackendSwapTests
{
    [Fact]
    public async Task AllTier2Tests_Pass_WithPostgreSqlBackend()
    {
        // Run existing Tier 2 test suite against PostgreSQL DAPR component
    }
}
```

**Run specific priorities:**

```bash
# Run only P0 tests
dotnet test --filter "Priority=P0"

# Run P0 + P1 tests
dotnet test --filter "Priority=P0|Priority=P1"

# Run security tests
dotnet test --filter "Category=Security"
```

---

## Appendix B: Knowledge Base References

- **Risk Governance**: `risk-governance.md` — Risk scoring methodology (P x I, thresholds)
- **Test Priorities Matrix**: `test-priorities-matrix.md` — P0-P3 criteria definitions
- **Test Levels Framework**: `test-levels-framework.md` — Unit vs Integration vs E2E selection
- **Test Quality**: `test-quality.md` — Definition of Done (deterministic, isolated, < 300 lines)
- **ADR Quality Readiness Checklist**: `adr-quality-readiness-checklist.md` — 8-category, 29-criteria NFR framework

---

**Generated by:** BMad TEA Agent (Murat)
**Workflow:** `bmad-testarch-test-design`
