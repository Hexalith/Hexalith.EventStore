---
title: 'TEA Test Design -> BMAD Handoff Document'
version: '1.0'
workflowType: 'testarch-test-design-handoff'
inputDocuments:
  - _bmad-output/test-artifacts/test-design/test-design-architecture.md
  - _bmad-output/test-artifacts/test-design/test-design-qa.md
sourceWorkflow: 'testarch-test-design'
generatedBy: 'TEA Master Test Architect (Murat)'
generatedAt: '2026-03-29'
projectName: 'Hexalith.EventStore'
---

# TEA -> BMAD Integration Handoff

## Purpose

This document bridges TEA's test design outputs with BMAD's epic/story decomposition workflow (`create-epics-and-stories`). It provides structured integration guidance so that quality requirements, risk assessments, and test strategies flow into implementation planning.

## TEA Artifacts Inventory

| Artifact | Path | BMAD Integration Point |
|----------|------|----------------------|
| Architecture Test Design | `_bmad-output/test-artifacts/test-design/test-design-architecture.md` | Epic quality requirements, risk-based story priority |
| QA Test Design | `_bmad-output/test-artifacts/test-design/test-design-qa.md` | Story acceptance criteria, test scenario mapping |
| Risk Assessment | Embedded in architecture doc (Section: Risk Assessment) | Epic risk classification, mitigation as story requirements |
| Coverage Strategy | Embedded in QA doc (Section: Test Coverage Plan) | Story test requirements (P0-P3 mapping) |
| Traceability Report | `_bmad-output/test-artifacts/traceability-report.md` | Coverage gap tracking, FR/NFR mapping |

## Epic-Level Integration Guidance

### Risk References

The following high-priority risks (score >= 6) should appear as epic-level quality gates:

| Risk ID | Category | Score | Epic Impact | Quality Gate |
|---------|----------|-------|-------------|-------------|
| R-001 | SEC | 6 | Any epic touching auth, multi-tenancy, or command processing | E2E auth tests with Keycloak must pass before epic closure |
| R-002 | DATA | 6 | Epic containing event envelope or persistence work | FR65 metadataVersion implemented and tested before envelope freeze |
| R-004 | OPS | 6 | Any epic claiming reliability or zero-data-loss | Chaos test harness verifies state store crash recovery before GA |

### Quality Gates

| Epic Scope | Required Gate | Test Coverage |
|------------|---------------|---------------|
| Command Pipeline (Epic 1-3) | P0 pass rate = 100% | TD-P0-001 through TD-P0-010 |
| Query Pipeline (Epic 7) | P1 pass rate >= 95% | TD-P1-007, TD-P1-008 |
| Multi-Tenant (Epic 3.6) | Cross-tenant isolation verified | TD-P0-002, TD-P1-010 |
| Infrastructure Portability | PostgreSQL backend passes all Tier 2 tests | TD-P1-002 |
| Operational Resilience | Chaos scenarios pass with zero data loss | TD-P1-003, TD-P1-004 |

## Story-Level Integration Guidance

### P0/P1 Test Scenarios -> Story Acceptance Criteria

The following test scenarios MUST be embedded as acceptance criteria in their corresponding stories:

| Test ID | Scenario | Story AC Wording |
|---------|----------|-----------------|
| TD-P0-001 | JWT auth E2E with Keycloak | "Given a valid JWT with tenant claims, when submitting a command, then the command is accepted and processed for the correct tenant" |
| TD-P0-002 | Cross-tenant isolation | "Given two tenants A and B, when tenant A submits a command, then tenant B's actor/events/topics are never accessed" |
| TD-P0-004 | Atomic event writes | "Given a command producing N events, when persisted, then either all N events are stored or none (no partial writes)" |
| TD-P0-005 | Optimistic concurrency | "Given two concurrent commands for the same aggregate, when both attempt to persist, then the second receives 409 Conflict" |
| TD-P0-006 | Idempotency | "Given a previously processed command resubmitted, when received, then the system returns idempotent success without duplicate events" |
| TD-P0-008 | metadataVersion field | "Given an event is persisted, when serialized, then the envelope includes metadataVersion = 1" |
| TD-P1-003 | State store crash recovery | "Given a state store crash mid-command, when the state store recovers, then the actor resumes from checkpoint with zero data loss" |
| TD-P1-004 | Pub/sub outage recovery | "Given a pub/sub outage during event publishing, when pub/sub recovers, then all persisted events are eventually delivered" |

### Data-TestId Requirements

Not applicable for this backend/API project. Hexalith.EventStore is a server platform — testability is achieved via API contracts, DAPR test containers, and Aspire topology, not DOM selectors.

## Risk-to-Story Mapping

| Risk ID | Category | P x I | Recommended Story/Epic | Test Level |
|---------|----------|-------|----------------------|-----------|
| R-001 | SEC | 2x3=6 | Epic 5 (Security & Authorization) | Integration (Tier 3) |
| R-002 | DATA | 2x3=6 | Epic 2 (Event Management) — FR65 implementation story | Unit |
| R-003 | PERF | 2x2=4 | Epic 6 (Observability) — Performance benchmark story | Benchmark |
| R-004 | OPS | 2x3=6 | Epic 4 (Reliability) — Chaos testing story | Integration (Tier 3) |
| R-005 | TECH | 2x2=4 | Epic 8 (Deployment) — PostgreSQL CI matrix story | Integration (Tier 3) |
| R-006 | SEC | 2x2=4 | Epic 5 (Security) — DAPR ACL verification story | Integration (Tier 3) |
| R-007 | DATA | 1x3=3 | Epic 2 (Event Management) — Snapshot stress test story | Integration |
| R-008 | BUS | 2x2=4 | Cross-epic — NFR test gap closure stories | Mixed |
| R-009 | TECH | 1x2=2 | v2 scope — Admin test review | Unit |
| R-010 | OPS | 1x2=2 | Epic 3.6 (Multi-Tenant) — Graceful degradation story | Integration (Tier 2) |

## Recommended BMAD -> TEA Workflow Sequence

1. **TEA Test Design** (`TD`) -> produces this handoff document (COMPLETE)
2. **BMAD Create Epics & Stories** -> consumes this handoff, embeds quality requirements
3. **TEA ATDD** (`AT`) -> generates acceptance tests per story (P0 scenarios first)
4. **BMAD Implementation** -> developers implement with test-first guidance
5. **TEA Automate** (`TA`) -> generates full test suite (P1-P3 expansion)
6. **TEA Trace** (`TR`) -> validates coverage completeness (target: FR >= 85%, NFR >= 65%)

## Phase Transition Quality Gates

| From Phase | To Phase | Gate Criteria |
|------------|----------|---------------|
| Test Design | Epic/Story Creation | All P0 risks have mitigation strategy (R-001, R-002, R-004: DONE) |
| Epic/Story Creation | ATDD | Stories have acceptance criteria from test design P0/P1 scenarios |
| ATDD | Implementation | Failing acceptance tests exist for all P0 scenarios (10 tests) |
| Implementation | Test Automation | All P0 acceptance tests pass; FR65 implemented |
| Test Automation | Release | Trace matrix shows >= 85% FR coverage, >= 65% NFR coverage |
