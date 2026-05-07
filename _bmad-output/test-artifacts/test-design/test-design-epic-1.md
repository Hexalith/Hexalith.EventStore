---
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted:
  - 'step-01-detect-mode'
  - 'step-02-load-context'
  - 'step-03-risk-and-testability'
  - 'step-04-coverage-plan'
  - 'step-05-generate-output'
lastStep: 'step-05-generate-output'
nextStep: ''
lastSaved: '2026-05-07'
mode: 'epic-level'
scope: 'Epic 1 — Domain Contract Foundation'
---

# Test Design: Epic 1 — Domain Contract Foundation

**Date:** 2026-05-07
**Author:** Jerome (drafted with Murat / TEA)
**Status:** Draft — retroactive coverage audit (epic shipped 2026-03-15, retro 2026-04-26)

---

## Executive Summary

**Scope.** Epic-level retroactive risk-based test design for **Epic 1 — Domain Contract Foundation** (5 stories, all `done`). Epic 1 establishes the type contract layer every downstream domain consumes: `AggregateIdentity`, `EventMetadata` (15 fields), `EventEnvelope`, `CommandEnvelope`, `MessageType`, `DomainResult`, `IRejectionEvent`, `CommandStatus` enum, `EventStoreAggregate<TState>` reflection-based dispatch, `ITerminatable` + `AggregateTerminated` tombstoning. The output is a coverage / risk audit, not a forward-looking plan — 21 epics already shipped on this surface.

**Risk Summary.**

- Total risks identified: **12**
- High-priority (score ≥6): **2** — both **TECH** category (positional-record swap vulnerability, `[DataMember]` runtime contract)
- Medium (score 4–5): **3**
- Low (score 1–3): **7**
- **Critical (score 9): 0** — Epic 1 is shippable as-is and has shipped

**Coverage Summary.**

- P0 scenarios: **~35** (all already covered — every AC mapped to at least one Tier 1 test)
- P1 scenarios: **~12** — gap of **3 tests** (DataContract round-trips for `EventMetadata` + `EventEnvelope`, plus `CommandEnvelope` field-count canary verification)
- P2/P3 scenarios: **~8** — gap of **2 tests** (zero-Handle aggregate boundary; solution-wide `ITerminatable` compliance scan)
- **Total net adds proposed: 5 tests, ~6–11 h dev effort, ~1 day**

---

## Not in Scope

| Item | Reasoning | Mitigation |
|------|-----------|------------|
| Tier 3 IntegrationTests (Aspire end-to-end) for Epic 1 contracts | Epic 1 ships pure type contracts; cross-process Aspire flow tested under later epics (8 AppHost, 11 projections, 13 SignalR) | Existing Tier 1+2 coverage at the contract level is the right level per `test-levels-framework.md` |
| `EventStoreProjection<TReadModel>` functional verification | Story 1.4 explicitly out-of-scope — Epic 11 owns projections | Already covered by Epic 11 coverage |
| Performance benchmarks of reflection-based Handle/Apply discovery | Cache (`ConcurrentDictionary`) makes cold-start the only path; product hasn't articulated an SLO at this layer | Cache thread-safety test exists; SLO assessment belongs in `*nfr` workflow, not test design |
| Forward-looking ATDD for new Epic 1 stories | Epic 1 is closed; no new stories planned (Epic 2+ build on this surface) | Run `*atdd` only if a new story re-opens this scope |
| ULID *format* (vs non-empty) validation in Contracts | Per design (D12 + Story 1.2 deferred item), format validation lives in Epic 3 FluentValidation rules | Risk R-D1 tracked under MONITOR until Epic 3 enforcement is verified end-to-end |

---

## Risk Assessment

> Scoring: probability (1–3) × impact (1–3) = score (1–9). Action thresholds: 1–3 DOCUMENT, 4–5 MONITOR, 6–8 MITIGATE, 9 BLOCK. See `risk-governance.md` and `probability-impact.md`.

### High-Priority Risks (Score ≥6)

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner | Timeline |
|---------|----------|-------------|---|---|-------|------------|-------|----------|
| R-T1 | TECH | **Positional-record swap vulnerability** on `EventMetadata` (15 params) and `CommandEnvelope` (10 params). Reordering same-typed args silently corrupts data; compiler stays green. | 2 | 3 | **6** | Story-spec convention: every construction site uses **named arguments**. Tests assert each field individually. Field-count canary tests catch additions/removals. Carry the rule into Epic 2+ specs (already practiced — see Epic 1 retro). | Project Lead | Standing |
| R-T2 | TECH | **`[DataMember]` runtime contract on positional records.** `MessageId` on `CommandEnvelope` uses an explicit property + `[DataMember]` (not `[property: DataMember]` on the positional param) because otherwise it vanishes during DAPR actor-state serialization. No compile-time enforcement. | 2 | 3 | **6** | Add Tier 1 `DataContract` round-trip tests for `EventMetadata` and `Contracts.EventEnvelope` (TG-1) so the silent-vanish trap surfaces immediately on any future positional-record field add. Existing `CommandEnvelope` round-trip test (Story 1.2) is the template. | Dev | This sprint (~2–4 h) |

### Medium-Priority Risks (Score 4–5)

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner |
|---------|----------|-------------|---|---|-------|------------|-------|
| R-D1 | DATA | **ULID format silent acceptance.** Per D12, ULID fields are bare `string`. Contracts validates non-empty only; ULID-format validation lives in Epic 3 FluentValidation. | 2 | 2 | **4** | Accepted by design. Document the boundary in a Tier 1 negative test (TG-5) so behavior change surfaces explicitly. CLAUDE.md already encodes the `Ulid.TryParse` rule for Epic 3 controllers/validators (R2-A7 retro item). | Dev (deferred to Epic 3) |
| R-T3 | TECH | **`Apply(AggregateTerminated)` runtime-only constraint.** Any `ITerminatable` state must also have a no-op `Apply(AggregateTerminated)` or actor reactivation throws `MissingApplyMethodException`. | 2 | 2 | **4** | `TerminatableComplianceAssertions` (R1-A2 ✅) and `MissingApplyMethodException` (R1-A6 ✅) shipped — discoverable. Adoption is opt-in. Add solution-wide architectural test (TG-2) so any new `ITerminatable` implementor is automatically checked. | Dev (~2–4 h) |
| R-T4 | TECH | **Reflection silent-skip on signature mismatch** in `EventStoreAggregate.Handle/Apply` discovery. Wrong-return-type method silently produces a "no matching method" runtime failure. | 2 | 2 | **4** | Existing test asserts the silent-skip behavior is intentional. Add zero-Handle-aggregate boundary test (TG-3) so the current contract is pinned for future framework refactors. | Dev (~1–2 h) |

### Low-Priority Risks (Score 1–3)

| Risk ID | Category | Description | P | I | Score | Action |
|---------|----------|-------------|---|---|-------|--------|
| R-S1 | SEC | `ToString()` payload redaction on `Contracts.EventEnvelope`, `Server.EventEnvelope`, and `CommandEnvelope`. | 1 | 3 | 3 | Covered by `PayloadProtectionTests` (Server.Tests/Security + Server.Tests/Logging) and Contracts.Tests envelope tests |
| R-S2 | SEC | Tenant injection via composite `AggregateIdentity` strings (`tenant:domain:id`). | 1 | 3 | 3 | Covered — colon-prohibition + regex validation in `AggregateIdentityTests` |
| R-B1 | BUS | Tombstoning is irreversible by design (D3 / Rule 11). | 1 | 3 | 3 | Covered — Counter sample close + post-termination rejection tests; Tier 2 lifecycle test |
| R-D2 | DATA | `MessageType` JSON / `ToString()` round-trip drift. | 1 | 2 | 2 | Covered — Story 1.3 round-trip + JSON converter tests |
| R-D3 | DATA | Mixed-event `DomainResult`. | 1 | 2 | 2 | Covered — `DomainResultTests` rejects mixed lists at construction |
| R-O1 | OPS | Pre-release schema break invalidates DAPR dev/CI state stores. | 2 | 1 | 2 | Accepted (no prod data); `dapr init --slim` documented in Story 1.2 dev notes |
| R-P1 | PERF | Reflection-based Handle/Apply discovery cold-start. | 1 | 1 | 1 | Mitigated by `ConcurrentDictionary` cache; thread-safety test covers |

### Risk Category Legend

- **TECH**: technical/architecture (flaws, integration, scalability)
- **SEC**: security (access, auth, data exposure)
- **PERF**: performance (SLA violations, degradation, resource limits)
- **DATA**: data integrity (loss, corruption, inconsistency)
- **BUS**: business impact (UX, logic errors, revenue)
- **OPS**: operations (deployment, config, monitoring)

---

## Entry Criteria

- [x] Story specs (1.1–1.5) and Epic 1 retro available and read
- [x] Source surface present in `src/Hexalith.EventStore.Contracts/` and `src/Hexalith.EventStore.Client/`
- [x] Existing Tier 1 + Tier 2 test inventory mapped
- [x] R1-action-item carryover status verified against current `main` (R1-A1, A2, A3, A4, A6, A7 ✅; only A5 outstanding, Low)
- [x] DAPR + Docker not required — Epic 1 net-add tests are pure Tier 1

## Exit Criteria (for closing the proposed test gaps)

- [ ] 1.1-UNIT-010 + 1.1-UNIT-011 (`DataContract` round-trips for `EventMetadata` + `EventEnvelope`) merged with green Contracts.Tests
- [ ] 1.2-UNIT-008 verified — `CommandEnvelope_HasExactly10Fields` canary either confirmed in `CommandEnvelopeTests.cs` or added
- [ ] 1.4-UNIT-010 (zero-Handle boundary) merged
- [ ] 1.5-UNIT-012 (solution-wide `ITerminatable` compliance scan) merged
- [ ] No Tier 1 regression (current baseline ≥ 651 tests at Epic 1 close; today's count ratchets higher)
- [ ] Release build remains 0 warnings / 0 errors

---

## Test Coverage Plan

> Note: P0/P1/P2/P3 = priority/risk classification, **not execution timing**. Execution lanes (PR / Nightly / Weekly) are defined separately in the Execution Strategy section.

### P0 (Critical)

**Criteria:** blocks the contract surface every downstream epic depends on + high risk + no workaround.

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
|-------------|------------|-----------|------------|-------|-------|
| `EventMetadata` 15-field shape, validation, immutability (1.1-UNIT-001..009, 012) | Unit (Tier 1) | R-T1, R-S1 | 10 | Dev | All exist in `EventMetadataTests` + `EventEnvelopeTests` + Server `PayloadProtectionTests` |
| `AggregateIdentity` parse/format/colon-prohibition + derived keys | Unit (Tier 1) | R-S2 | ≥5 | Dev | Exists — `AggregateIdentityTests`, `IdentityParserTests` |
| `CommandEnvelope` validation + `[DataMember]` survival (1.2-UNIT-001..006) | Unit (Tier 1) | R-T2, R-S1 | 6 | Dev | Exists incl. `DataContract` round-trip |
| `MessageType` parse / assemble / max-length / round-trip (1.3-UNIT-001..006) | Unit (Tier 1) | R-D2 | 6 | Dev | Exists — `MessageTypeTests` |
| `UniqueIdHelper` ULID generation, sort order, round-trip (1.3-UNIT-007..010) | Unit (Tier 1) | R-D1 (boundary) | 4 | Dev | Exists — `UniqueIdHelperIntegrationTests` |
| `EventStoreAggregate` Handle/Apply discovery + dispatch + rehydration (1.4-UNIT-001..008) | Unit (Tier 1) | R-T4, R-P1 | ≥8 | Dev | Exists — `EventStoreAggregateTests`, `NamingConventionEngineTests` |
| `MissingApplyMethodException` shape + context | Unit (Tier 1) | R-T3 / R1-A6 | ≥3 | Dev | Exists — `MissingApplyMethodExceptionTests` |
| `CommandStatus` enum + `IsTerminal()` (1.5-UNIT-001..002) | Unit (Tier 1) | BUS contract | 2 | Dev | Exists — `CommandStatusTests` + `CommandStatusExtensionsTests` |
| Tombstoning: guard, replay, lifecycle (1.5-UNIT-003..011 + 1.5-INT-001) | Unit (Tier 1) + Integration (Tier 2) | R-B1, R-T3, R1-A2, R1-A7 | 12 | Dev | Exists — `EventStoreAggregateTests`, `CounterAggregateTests`, `TombstoningLifecycleTests`, `TerminatableComplianceAssertionsTests` |

**Total P0:** ~56 existing tests across the Epic 1 surface, all green at last full run. **No P0 gaps.**

### P1 (High)

**Criteria:** important contracts where review/test rules need reinforcement + medium risk.

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
|-------------|------------|-----------|------------|-------|-------|
| `EventMetadata` `DataContract` serialization round-trip (**1.1-UNIT-010**) | Unit (Tier 1) | R-T2 / TG-1 | **+1** | Dev | **NEW** — closes the silent `[DataMember]` trap on the 15-field record |
| `Contracts.EventEnvelope` `DataContract` serialization round-trip (**1.1-UNIT-011**) | Unit (Tier 1) | R-T2 / TG-1 | **+1** | Dev | **NEW** — pair with 1.1-UNIT-010 |
| `CommandEnvelope_HasExactly10Fields` canary (**1.2-UNIT-008**) | Unit (Tier 1) | R-T1 / TG-4 | **+1 or verify** | Dev | Verify presence in `CommandEnvelopeTests`; add if missing |
| `Counter` rejection-event naming (Rule 8) | Unit (Tier 1) | BUS naming | 1 | Dev | Exists — `CounterAggregateTests` |
| `EventStoreAggregate` metadata-cache thread safety | Unit (Tier 1) | R-P1 | 1 | Dev | Exists — `EventStoreAggregateTests` + `NamingConventionEngineTests` |

**Total P1:** ~9 tests, **3 net adds** (closes R-T2 silent trap + verifies the field-count canary symmetry).

### P2 (Medium)

**Criteria:** secondary or boundary scenarios + low/medium risk.

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
|-------------|------------|-----------|------------|-------|-------|
| Zero-`Handle` aggregate boundary (**1.4-UNIT-010**) | Unit (Tier 1) | R-T4 / TG-3 | **+1** | Dev | **NEW** — pin current behavior |
| Solution-wide `ITerminatable` compliance scan (**1.5-UNIT-012**) | Unit (Tier 1) | R-T3 / TG-2 | **+1** | Dev | **NEW** — assembly scan + reflective `Apply(AggregateTerminated)` assertion across all `ITerminatable` implementors |
| `EventStoreDomainAttribute` constraints | Unit (Tier 1) | BUS contract | 3+ | Dev | Exists — `EventStoreDomainAttributeTests` |
| ULID-format current-behavior boundary (TG-5) | Unit (Tier 1) | R-D1 | optional | Dev | Defer until Epic 3 format validation lands; revisit then |

**Total P2:** ~5 tests, **2 net adds**.

### P3 (Low)

**Criteria:** nice-to-have / exploratory.

None proposed for Epic 1. The contract layer is small and the priority gradient flattens at P3 — every meaningful scenario is already at P0–P2.

---

## Execution Strategy

> Philosophy: run everything in PRs unless infrastructure overhead makes it expensive. Epic 1 surface is pure .NET Tier 1 unit + one Tier 2 actor lifecycle file — comfortably under the 15-minute PR budget.

| Lane | Suite | Wall-Clock | Cadence |
|------|-------|------------|---------|
| **Every PR** | All Tier 1 (`Contracts.Tests`, `Client.Tests`, `Sample.Tests`, `Testing.Tests`, `SignalR.Tests`) | ~2–4 min | every PR |
| **Every PR (post `dapr init`)** | Tier 2 `Server.Tests` including `Actors/TombstoningLifecycleTests.cs` (R1-A7) | ~5–10 min | every PR (CI runs `dapr init`) |
| **Nightly / on `main` merge** | Full Release-build solution + Tier 1 + 2 + 3 (`IntegrationTests`) | ~15–25 min | nightly |
| **Weekly / on-demand** | None for Epic 1 surface | — | — |

The 5 net-adds proposed below each run in <1s and stay in the PR lane.

---

## Resource Estimates

> Interval ranges only — no false precision. Effort includes setup time and the typical Epic-1-style review-driven rework (Epic 2 retro flagged 5/5 stories with reviewer-found patches).

### Test Development Effort (close-out of proposed gaps)

| Priority | Count | Hours/Test (approx) | Total Hours | Notes |
|----------|-------|---------------------|-------------|-------|
| P1 | 3 (1.1-UNIT-010, 1.1-UNIT-011, 1.2-UNIT-008) | ~1–2 | **~3–6 h** | Mirror existing `CommandEnvelope` `DataContract` test |
| P2 | 2 (1.4-UNIT-010, 1.5-UNIT-012) | ~1–3 | **~3–7 h** | 1.5-UNIT-012 is the bigger unknown (assembly scan + reflection helper extension) |
| P3 | 0 | — | — | — |
| **Total** | **5** | — | **~6–11 h** (~**1 dev-day**) | Single PR or two thin PRs |

### Prerequisites

**Test data:** none beyond existing builders (`CommandEnvelopeBuilder`, `EventEnvelopeBuilder` — already in `Hexalith.EventStore.Testing`).

**Tooling:** existing — xUnit 2.9.3, Shouldly 4.3.0, NSubstitute 5.3.0, coverlet.collector 6.0.4. No new dependency.

**Environment:** Tier 1 only — no DAPR, no Docker. Same `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` / `Client.Tests/` / `Testing.Tests/` invocation per CLAUDE.md.

---

## Quality Gate Criteria

### Pass / Fail Thresholds

- **P0 pass rate:** **100%** (no exceptions). Already met — Epic 1 closed at 651 Tier 1 green + Tier 2 lifecycle green.
- **P1 pass rate:** **≥95%.** Currently in violation of intent for R-T2 (no `EventMetadata` / `EventEnvelope` round-trip tests). Closing 1.1-UNIT-010 + 1.1-UNIT-011 raises P1 to 100%.
- **P2 pass rate:** **≥80%.** Currently met. TG-2 + TG-3 close-out raises confidence but is not a release blocker.
- **High-risk mitigations (score ≥6):** **100% complete or with documented compensating control.**
  - R-T1: compensating control = named-argument convention + per-field assertions + field-count canary. **Met.**
  - R-T2: compensating control gap. Close TG-1.

### Coverage Targets

- Critical contracts: **≥80%** — already met by file-to-test ratio across Contracts + Client.
- Security scenarios (`R-S1`, `R-S2`): **100%** — met.
- Reflection / dispatch logic: **≥70%** — met by `EventStoreAggregateTests` (35+) + `NamingConventionEngineTests` (40+).
- Edge cases (validation negatives): **≥50%** — met across stories 1.1–1.5.
- Line-coverage measurement (coverlet): **enabled but not currently published as an Epic 1 number.** Optional one-off audit if Jerome wants the percentage on record.

### Non-Negotiable Requirements

- [x] All P0 tests pass at Epic 1 close (verified 2026-03-15)
- [x] No high-risk (≥6) items unmitigated **at code level** — both R-T1 and R-T2 have compensating controls; close TG-1 to formalize R-T2's
- [x] Security tests (R-S1, R-S2) pass 100% — verified
- [n/a] PERF targets — none articulated for Epic 1 (no SLO at the contract layer)

---

## Mitigation Plans

### R-T1 — Positional-record swap vulnerability (Score 6)

- **Mitigation strategy:**
  1. Every story spec touching `EventMetadata`, `CommandEnvelope`, or any positional record with ≥3 same-typed parameters **must** mandate named arguments at every construction site (already the convention since Story 1.1).
  2. Maintain the field-count canary tests (`EventMetadata_HasExactly15Fields`, `CommandEnvelope_HasExactly10Fields` if present — see TG-4).
  3. Maintain per-field round-trip assertion tests; reject any review that adds a positional-record field without updating the count canary and per-field test.
- **Owner:** Project Lead + dev (story specs)
- **Timeline:** standing convention — re-asserted every time a positional record is touched
- **Status:** **Active control** — practiced through Epic 1 (Stories 1.1, 1.2) and Epic 2; documented in Epic 1 retro § "What Went Well #2"
- **Verification:** review checklist item — every PR adding/removing a field on a positional record must show: (a) named-arg construction at every site, (b) field-count canary updated, (c) per-field test updated. Run `grep "new EventMetadata(" / "new CommandEnvelope("` after change to spot positional callers.

### R-T2 — `[DataMember]` runtime contract (Score 6)

- **Mitigation strategy:**
  1. Add `EventMetadata` `DataContract` serialization round-trip test (1.1-UNIT-010) — mirror the `CommandEnvelope` test added in Story 1.2.
  2. Add `Contracts.EventEnvelope` `DataContract` serialization round-trip test (1.1-UNIT-011) — covers envelope + metadata + payload + Extensions.
  3. Document in story-spec template: "any new field on a positional record consumed by serialization must have a `DataContract` round-trip test in `Contracts.Tests`."
- **Owner:** Dev
- **Timeline:** **this sprint** (~3–6 h, single PR)
- **Status:** **Planned** — gap identified in this design
- **Verification:** add the two tests; run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/`; introduce a deliberate `[DataMember]` strip in a throwaway branch and confirm both tests fail; revert.

---

## Assumptions and Dependencies

### Assumptions

1. The Epic 1 surface is closed: no new stories planned in this scope; this design is a retroactive audit, not forward-looking ATDD input.
2. R1-A5 (single-event `DomainResult.Rejection(IRejectionEvent e)` overload) remains a Low-priority ergonomic gap with no correctness implication; not addressed by this design.
3. Tier 1 test runtime (~2–4 min for the full Epic 1 surface) stays well below any CI budget; no need to defer to nightly.
4. ULID format validation lives in Epic 3 FluentValidation (R-D1 accepted at Contracts boundary).

### Dependencies

1. None for the proposed P1/P2 net-adds — all are pure Tier 1 unit tests using existing fixtures.
2. Tier 2 lifecycle tests (`TombstoningLifecycleTests`) require `dapr init` for any local re-runs; CI handles this in the standard pipeline.

### Risks to Plan

- **Risk:** the proposed `solution-wide ITerminatable compliance scan` (1.5-UNIT-012) becomes brittle as the project grows and new domains land.
  - **Impact:** maintenance churn — every new `ITerminatable` aggregate triggers test review.
  - **Contingency:** ship as a single architectural test that scans loaded assemblies for `ITerminatable` implementors and asserts `Apply(AggregateTerminated)` exists via reflection. Brittleness is the point — that's what closes R-T3.

---

## Interworking & Regression

| Service / Component | Impact | Regression Scope |
|---------------------|--------|------------------|
| `Hexalith.EventStore.Contracts` | Direct (5 stories' surface) | All `Contracts.Tests` (~280+) |
| `Hexalith.EventStore.Client` | Direct (Story 1.4 dispatch + Story 1.5 tombstoning guard) | All `Client.Tests` (~280+) |
| `Hexalith.EventStore.Testing` | Direct (R1-A2 helper + builders) | All `Testing.Tests` (~64+) |
| `Hexalith.EventStore.Sample` | Indirect (Counter sample exercises every contract) | All `Sample.Tests` (~29+) |
| `Hexalith.EventStore.Server` | Indirect (Server.EventEnvelope mirror; Server.PayloadProtectionTests; tombstoning lifecycle) | All `Server.Tests` Tier 2 incl. `TombstoningLifecycleTests` |
| `Hexalith.EventStore` (REST API) | Indirect (`SubmitCommandRequest` carries MessageId) | API validator tests |

A regression on the proposed net-add round-trip tests (1.1-UNIT-010, 1.1-UNIT-011) would localize to `Contracts.Tests` only — minimal blast radius.

---

## Follow-on Workflows (Manual)

- This is a retroactive design — `*atdd` is **not** the natural follow-up because no new red-phase tests are needed for already-shipped code. If Jerome wants TDD-style failing tests for the 5 net-adds first, run `*atdd` with explicit scope = "Epic 1 R-T2 / TG-1..3 close-out."
- Run `*automate` is the better fit if you want the 5 net-adds generated, prioritized, and given fixtures + DoD in one go.
- Run `*trace` on Epic 1 once the 5 tests are merged to produce the requirements-to-tests traceability matrix and the P0/P1 gate decision (`PASS` / `CONCERNS` / `FAIL`).
- Run `*nfr` only if a new SLO is articulated for the contract layer (none today).

---

## Appendix

### Knowledge Base References

- `risk-governance.md` — risk classification framework, gate decision rules
- `probability-impact.md` — 1-3 × 1-3 = 1-9 scoring methodology
- `test-levels-framework.md` — unit / integration / E2E selection
- `test-priorities-matrix.md` — P0–P3 criteria, coverage targets

### Related Documents

- Epic 1 retrospective: `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md`
- Story 1.1: `_bmad-output/implementation-artifacts/1-1-core-identity-and-event-envelope.md`
- Story 1.2: `_bmad-output/implementation-artifacts/1-2-command-types-domainresult-and-error-contract.md`
- Story 1.3: `_bmad-output/implementation-artifacts/1-3-messagetype-value-object-and-hexalith-commons-ulid-integration.md`
- Story 1.4: `_bmad-output/implementation-artifacts/1-4-pure-function-contract-and-eventstoreaggregate-base.md`
- Story 1.5: `_bmad-output/implementation-artifacts/1-5-commandstatus-enum-and-aggregate-tombstoning.md`
- Epic index: `_bmad-output/planning-artifacts/epics.md`
- Project guide: `CLAUDE.md`

---

**Generated by:** BMad TEA (Murat) — Test Architect Module
**Workflow:** `bmad-testarch-test-design` (Epic-Level / Phase 4)
**Version:** 4.0 (BMad v6)
