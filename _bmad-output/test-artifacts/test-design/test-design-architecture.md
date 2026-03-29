---
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
lastSaved: '2026-03-29'
workflowType: 'testarch-test-design'
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/implementation-artifacts/3-6-adr-round2-domain-authorization-and-tenant-offboarding.md
  - docs/concepts/architecture-overview.md
  - _bmad/tea/testarch/knowledge/risk-governance.md
  - _bmad/tea/testarch/knowledge/test-levels-framework.md
  - _bmad/tea/testarch/knowledge/test-quality.md
  - _bmad/tea/testarch/knowledge/adr-quality-readiness-checklist.md
---

# Test Design for Architecture: Hexalith.EventStore v1

**Purpose:** Architectural concerns, testability gaps, and risk assessment for review by the Architecture/Dev team. Serves as a contract between QA and Engineering on what must be addressed before GA.

**Date:** 2026-03-29
**Author:** Murat (TEA Agent)
**Status:** Architecture Review Pending
**Project:** Hexalith.EventStore
**PRD Reference:** `_bmad-output/planning-artifacts/prd.md`
**ADR Reference:** `_bmad-output/planning-artifacts/architecture.md`, `_bmad-output/implementation-artifacts/3-6-adr-round2-domain-authorization-and-tenant-offboarding.md`

---

## Executive Summary

**Scope:** Full system-level test design for Hexalith.EventStore v1 — DAPR-native event sourcing server covering the command-to-event pipeline, query/projection caching pipeline, multi-tenant isolation, and infrastructure portability.

**Business Context** (from PRD):

- **Impact:** Core platform for all Hexalith DDD applications; v1 GA enables production deployments
- **Problem:** No DAPR-native event sourcing server exists for .NET — developers must choose between infrastructure-coupled options (EventStoreDB, Marten) or build from scratch
- **GA Launch:** v1.0 — timeline driven by envelope validation across 3+ domain implementations

**Architecture** (from ADR):

- **D1:** Single-key-per-event with actor-level ACID writes (universal backend support)
- **D3:** Domain errors as events, infrastructure errors as exceptions
- **D11:** Keycloak in Aspire for E2E security testing (decided but not yet implemented)

**Expected Scale** (from NFRs):

- 100 cmd/sec/instance, 10K active aggregates, 10+ tenants, p99 lifecycle < 200ms

**Risk Summary:**

- **Total risks**: 10
- **High-priority (>=6)**: 3 risks requiring immediate mitigation (R-001, R-002, R-004)
- **Test effort**: ~41 scenarios (~2-4 weeks solo developer)

---

## Quick Guide

### BLOCKERS - Team Must Decide (Can't Proceed Without)

**Pre-Implementation Critical Path** — these MUST be completed before integration tests can validate security and data integrity:

1. **R-002: Implement FR65 (metadataVersion)** — Event envelope schema freeze is irreversible post-GA. The metadataVersion field must exist in EventMetadata before v1 ships. (recommended owner: Dev)
2. **D11: Keycloak in Aspire** — E2E auth tests (R-001) require a real IdP in the Aspire topology. Realm-as-code configuration needed. (recommended owner: Dev)

**What we need from team:** Complete these 2 items pre-implementation or auth and envelope testing is blocked.

---

### HIGH PRIORITY - Team Should Validate (We Provide Recommendation, You Approve)

1. **R-001: 6-layer auth E2E verification** — Add Tier 3 integration tests exercising JWT -> Claims -> Controller -> MediatR -> Actor -> DAPR ACL with Keycloak. (implementation phase)
2. **R-004: Chaos test harness** — Build automated fault injection (kill state store, kill pub/sub, rebalance actors) in Tier 3 environment. Verify checkpoint recovery and zero data loss. (v1.0)
3. **R-005: PostgreSQL backend CI** — Add PostgreSQL DAPR component variant to Tier 3 test matrix. Validate NFR27/NFR29 "zero code change" promise. (pre-GA)

**What we need from team:** Review recommendations and approve (or suggest alternatives).

---

### INFO ONLY - Solutions Provided (Review, No Decisions Needed)

1. **Test strategy**: Unit (Tier 1) + Integration with DAPR slim (Tier 2) + Full Aspire topology (Tier 3) — aligned with existing three-tier architecture
2. **Tooling**: xUnit + Shouldly + NSubstitute (existing stack); k6 or NBomber for performance benchmarks
3. **Tiered CI/CD**: PR (Tier 1+2, <10 min), Nightly (Tier 3, ~30-60 min), Weekly (perf + chaos, ~1-2 hr)
4. **Coverage**: 41 test scenarios prioritized P0-P3 with risk-based classification
5. **Quality gates**: P0 = 100%, P1 >= 95%, high-risk mitigations complete before GA

**What we need from team:** Just review and acknowledge.

---

## For Architects and Devs

### Risk Assessment

**Total risks identified**: 10 (3 high-priority score >=6, 5 medium, 2 low)

#### High-Priority Risks (Score >=6) - IMMEDIATE ATTENTION

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner | Timeline |
|---------|----------|-------------|---|---|-------|------------|-------|----------|
| **R-001** | **SEC** | 6-layer auth not E2E verified. JWT flow through real IdP never tested. Misconfigured claims transformation could allow cross-tenant access. | 2 | 3 | **6** | Implement D11 (Keycloak in Aspire); add Tier 3 auth integration tests | Dev | Pre-GA |
| **R-002** | **DATA** | Event envelope schema freeze without metadataVersion (FR65). Missing field means external consumers can't detect envelope schema evolution. Irreversible post-GA. | 2 | 3 | **6** | Implement FR65; validate with serialization round-trip tests | Dev | Sprint |
| **R-004** | **OPS** | Zero-data-loss claim (NFR22) unverified. Chaos scenarios described in Journey 6 but never automated. State store crash, pub/sub outage, actor rebalancing untested. | 2 | 3 | **6** | Build chaos test harness; verify checkpoint recovery in Tier 3 | Dev | v1.0 |

#### Medium-Priority Risks (Score 3-5)

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner |
|---------|----------|-------------|---|---|-------|------------|-------|
| R-003 | PERF | Performance KPIs (NFR1-NFR8) unvalidated. No load testing harness. | 2 | 2 | 4 | Add k6/NBomber perf suite | Dev/QA |
| R-005 | TECH | Backend portability untested in CI. Only Redis validated. | 2 | 2 | 4 | Add PostgreSQL to Tier 3 CI matrix | DevOps |
| R-006 | SEC | DAPR ACL policies not integration-tested at runtime. | 2 | 2 | 4 | Add Tier 3 ACL enforcement test | Dev |
| R-008 | BUS | NFR coverage at 51%. Scalability and reliability NFRs weak. | 2 | 2 | 4 | Prioritize NFR test gaps in ATDD | QA |
| R-007 | DATA | Snapshot divergence undetected at scale (1000+ events). | 1 | 3 | 3 | Add stress test with large aggregates | Dev |

#### Low-Priority Risks (Score 1-2)

| Risk ID | Category | Description | P | I | Score | Action |
|---------|----------|-------------|---|---|-------|--------|
| R-009 | TECH | Admin tooling (v2) test files untracked; quality unverified | 1 | 2 | 2 | Run test review on new files |
| R-010 | OPS | Tenant offboarding undefined (ADR-6); graceful degradation untested | 1 | 2 | 2 | Add config-deletion test |

#### Risk Category Legend

- **TECH**: Technical/Architecture (integration, scalability, design flaws)
- **SEC**: Security (auth, access controls, data exposure)
- **PERF**: Performance (SLA violations, degradation)
- **DATA**: Data Integrity (loss, corruption, inconsistency)
- **BUS**: Business Impact (coverage gaps, logic errors)
- **OPS**: Operations (deployment, config, monitoring)

---

### Testability Concerns and Architectural Gaps

**ACTIONABLE CONCERNS - Architecture Team Must Address**

#### 1. Blockers to Fast Feedback (WHAT WE NEED FROM ARCHITECTURE)

| Concern | Impact | What Architecture Must Provide | Owner | Timeline |
|---------|--------|-------------------------------|-------|----------|
| **No real IdP in test topology** | Cannot verify 6-layer JWT auth E2E (R-001) | Keycloak resource in Aspire AppHost with realm-as-code (D11) | Dev | Pre-GA |
| **FR65 not implemented** | Cannot test envelope versioning before irreversible freeze (R-002) | `MetadataVersion` property on `EventMetadata` | Dev | Sprint |
| **No chaos injection mechanism** | Cannot verify NFR22-25 zero data loss claims (R-004) | Test harness to kill/restart DAPR components in Tier 3 | Dev | v1.0 |

#### 2. Architectural Improvements Needed

1. **PostgreSQL backend in CI pipeline**
   - **Current problem**: Only Redis backend tested in CI. "Zero code change" portability claim unverified.
   - **Required change**: Add PostgreSQL DAPR component variant to Tier 3 GitHub Actions matrix.
   - **Impact if not fixed**: Infrastructure portability (NFR27/NFR29) is aspirational, not validated.
   - **Owner**: DevOps
   - **Timeline**: Pre-GA

---

### Testability Assessment Summary

**CURRENT STATE - FYI**

#### What Works Well

- Pure function domain model (Handle/Apply) is trivially unit-testable without DAPR
- Three-tier test architecture with clean dependency boundaries already established
- 69.8% FR coverage (74/106 requirements have tests per traceability report)
- DAPR actor single-threaded model eliminates concurrency test complexity
- OpenTelemetry instrumentation enables trace-based assertions
- Aspire `dotnet aspire run` provides reproducible full-topology test environments
- Convention-based domain service registration (FR22) simplifies integration test setup

#### Accepted Trade-offs (No Action Required)

- **DAPR idle timeout (60 min) as garbage collection** — Accepted for v1; query actor cache orphans self-clean via DAPR default. Revisit if cache coherence becomes a concern.
- **Coarse projection invalidation (FR58)** — All cached queries for a projection+tenant invalidated on any change. Trades unnecessary refreshes for design simplicity. Acceptable for v1 scale.
- **At-least-once pub/sub delivery** — Subscribers must be idempotent. This is a DAPR constraint, not an EventStore design choice.

---

### Risk Mitigation Plans (High-Priority Risks >=6)

#### R-001: 6-Layer Auth Not E2E Verified (Score: 6)

**Mitigation Strategy:**

1. Implement D11: Add Keycloak as an Aspire resource with realm-as-code configuration (test tenants, domains, permissions as JSON import)
2. Create Tier 3 integration tests: valid token accepted, expired token rejected (401), wrong tenant rejected (403), cross-tenant isolation verified
3. Test claims transformation edge cases: missing domain claims = all domains allowed (ADR-5 Option A behavior)

**Owner:** Dev
**Timeline:** Pre-GA
**Status:** Planned
**Verification:** All Tier 3 auth tests pass with Keycloak IdP; cross-tenant command routing verified

#### R-002: Envelope Schema Freeze Without metadataVersion (Score: 6)

**Mitigation Strategy:**

1. Add `MetadataVersion` integer property (starting at 1) to `EventMetadata`
2. Add unit tests: serialization round-trip, default value, version increment on schema change
3. Validate with existing EventEnvelopeTests to ensure backward compatibility

**Owner:** Dev
**Timeline:** Current sprint (pre-GA blocker)
**Status:** Planned
**Verification:** FR65 tests pass; existing envelope tests remain green

#### R-004: Zero-Data-Loss Claim Unverified (Score: 6)

**Mitigation Strategy:**

1. Build chaos test harness in Tier 3: programmatically kill/restart DAPR state store and pub/sub containers during command processing
2. Test scenarios: state store crash mid-command (checkpoint resumes), pub/sub outage (events persist, backlog drains), actor rebalancing under load (zero command loss)
3. Verify deterministic replay produces identical events after recovery

**Owner:** Dev
**Timeline:** v1.0
**Status:** Planned
**Verification:** All chaos scenarios complete with zero data loss and correct event streams

---

### Assumptions and Dependencies

#### Assumptions

1. DAPR actor `IActorStateManager.SaveStateAsync` provides atomic batch commits on all target backends (Redis, PostgreSQL) — this is a DAPR runtime guarantee
2. Solo developer (Jerome) serves as both Dev and QA; estimates assume single-person execution
3. v2 admin tooling (Blazor UI, CLI, MCP) is out of scope for v1 test design; admin test files in git status are tracked but not validated here

#### Dependencies

1. **D11 Keycloak in Aspire** — Required before R-001 mitigation (pre-GA)
2. **FR65 implementation** — Required before R-002 mitigation (current sprint)
3. **DAPR 1.17.x SDK** — Current dependency; verify compatibility if upgrading before GA

#### Risks to Plan

- **Risk**: Solo developer bandwidth constrains parallel progress on all 3 high-priority mitigations
  - **Impact**: Could delay v1 GA if all mitigations attempted simultaneously
  - **Contingency**: Sequence as R-002 (sprint) -> R-001 (pre-GA) -> R-004 (v1.0); R-004 chaos tests can ship as post-GA hardening

---

**End of Architecture Document**

**Next Steps for Architecture Team:**

1. Review Quick Guide (BLOCKERS/HIGH PRIORITY/INFO) and prioritize
2. Assign owners and timelines for high-priority risks (R-001, R-002, R-004)
3. Validate assumptions and dependencies
4. Provide feedback on testability gaps

**Next Steps for QA Team:**

1. Wait for FR65 implementation and D11 Keycloak setup
2. Refer to companion QA doc (`test-design-qa.md`) for test scenarios
3. Begin test infrastructure setup (chaos harness, perf benchmark project)
