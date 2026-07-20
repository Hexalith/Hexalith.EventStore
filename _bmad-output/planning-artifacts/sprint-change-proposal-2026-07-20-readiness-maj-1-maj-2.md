---
project: eventstore
date: 2026-07-20
workflow: bmad-correct-course
mode: batch
scope_classification: minor
status: implemented
approved_by: Administrator
approved_on: 2026-07-20
trigger: implementation-readiness-report-2026-07-20 MAJ-1 and MAJ-2
---

# Sprint Change Proposal — Readiness MAJ-1 And MAJ-2

**Author:** Amelia (Developer) via `bmad-correct-course`
**Finding severity:** 2 Major documentation findings
**Change scope:** Minor direct adjustment; no runtime, backlog, MVP, or sequencing change
**Status:** APPROVED AND IMPLEMENTED

The workflow default path, `sprint-change-proposal-2026-07-20.md`, already contains a prior same-date proposal. This descriptive filename preserves that existing artifact.

## 1. Issue Summary

The 2026-07-20 implementation-readiness assessment reports `READY` with two non-blocking Major documentation corrections:

1. **MAJ-1 — mode scope and gate traceability:** Story 1.20's header says Epic 1 cannot become `done` until Story 3.12 produces a conforming deployed image, while Story 1.20's acceptance criteria make deployed-image identity conditional (`where applicable`). The cross-epic relationship is also absent from the Implementation Readiness Execution Gates authority.
2. **MAJ-2 — duplicate provenance ownership:** Stories 2.6 and 2.11 both own Tenants UI provenance-to-lifecycle behavior, but use different reviewers and proof bars. Story 2.6 should own typed-client/UI-host alignment and UX presentation; Story 2.11 should exclusively own provenance preservation, authoritative lifecycle selection, fail-closed `Unknown`, and production-path proof.

The trigger is a planning self-consistency defect discovered during readiness review. It is not a new product requirement, technical limitation, strategic pivot, or failed implementation.

### Evidence

| Evidence | Finding |
| --- | --- |
| `epics.md`, Implementation Readiness Execution Gates | The Parties parity gate distinguishes source, package, and deployed consumers but does not state that Story 3.12 is conditional on selecting deployed mode. |
| `epics.md`, Story 1.20 header and runtime-identity AC | The header unconditionally blocks Epic 1 on Story 3.12; the AC requires deployed-image identity only “where applicable.” |
| Architecture AD-22 | Source mode verifies a source SHA, package mode verifies package versions/hashes, and deployed mode verifies the OCI image identity chain. This confirms mode-specific closure. |
| `epics.md`, Stories 2.6 and 2.11 | Both stories state the same `ProjectionBacked` versus `Unknown` rendering rule; Story 2.11 additionally requires real-gateway and persisted-read-model proof. |
| Architecture AD-14/AD-15 and canonical UX | The platform/provenance boundary selects authoritative evidence; consumer UX presents the resulting lifecycle accessibly and support-safely. |
| Canonical SPEC package | `SPEC.md` and `readiness-gates.md` still say Story 1.20 depends only on Epic 1/no forward dependency. MAJ-1 therefore requires a small preservation edit there as well as in `epics.md`. |

## 2. Impact Analysis

### Epic And Story Impact

| Area | Impact | Disposition |
| --- | --- | --- |
| Epic 1 / Story 1.20 | Conditional deployed-mode dependency is worded as unconditional. | Add the mode-specific execution gate and scope the story header to deployed mode. |
| Epic 2 / Story 2.6 | UI/UX story duplicates provenance-selection proof. | Retain client-library/UI-host alignment and presentation of already-classified states; remove provenance proof ownership. |
| Epic 2 / Story 2.11 | Correct proof bar exists but exclusive ownership is implicit. | Make ownership and focused production-path validation explicit. |
| Epic 3 / Story 3.12 | Produces the conforming release identity only when deployed mode is selected. | No story scope or status change; retain the existing independent revalidation handoff. |
| Epics 4–8 | No dependency, scope, or acceptance impact. | No change. |

No epic is added, removed, redefined, reprioritized, or resequenced. Source/package parity closure does not wait for Story 3.12; deployed-mode closure does.

### Artifact Impact

| Artifact | Impact |
| --- | --- |
| `epics.md` | Direct edits for both findings. |
| `SPEC.md` and `readiness-gates.md` | MAJ-1 preservation edits so the declared canonical SPEC package does not retain an unconditional “no forward dependency / Epic 1 only” invariant. |
| `prd.md` | No change. FR15 and FR36 already express the correct requirement intent without duplicate story ownership or an unconditional Story 3.12 gate. |
| `architecture.md` | No change. AD-14/AD-15 and AD-22 already define the correct provenance and mode-specific identity boundaries. |
| Canonical UX (`DESIGN.md`, `EXPERIENCE.md`) | No change. It already owns accessible, support-safe presentation of canonical evidence states. |
| Project documentation | No change. `docs/concepts/projection-lifecycle.md` and `docs/reference/query-api.md` already match AD-14/AD-15. |
| `sprint-status.yaml` | No status edit. No story is added, removed, or renumbered; the current rows already identify 2.11 as consumer-only and 3.12 as a conditional release handoff. |

### Technical Impact

None. No code, API, deployment, package, infrastructure, test, or runtime behavior changes. The edits clarify which existing story proves which existing contract.

## 3. Recommended Approach

**Selected: Option 1 — Direct Adjustment.**

- **Effort:** Low — focused planning/SPEC text changes.
- **Risk:** Low — no scope, identifier, status, or runtime mutation.
- **Timeline impact:** None expected; removes ambiguity before story closure/review.
- **Sustainability:** Establishes one owner and one proof bar for provenance consumption, and accurately represents the mode-specific runtime identity model.

**Option 2 — Potential Rollback:** Not viable. There is no completed implementation to revert and rollback would not correct documentation ownership.

**Option 3 — MVP Review:** Not viable. The assessment confirmed 37/37 FR coverage and no Critical issue; neither finding changes product scope or success metrics.

## 4. Detailed Change Proposals

### Edit 1 — `epics.md`, Parties Projection/Query Parity Gate

**Section:** Implementation Readiness Execution Gates → Parties Projection/Query Parity Gate, after the source/package/deployed identity paragraph.

**OLD:**

No explicit mode-specific Story 1.20 → Story 3.12 gate entry exists.

**NEW:**

```markdown
Runtime-identity closure is mode-specific. Source mode may close against the approved EventStore source SHA, and package mode may close against the approved package versions and hashes, without waiting for Story 3.12 when every gate applicable to the selected mode is satisfied. If Story 1.20 selects deployed mode, Story 3.12 is the intentional conditional cross-epic prerequisite: it must produce a conforming two-platform EventStore release and Story 1.20 must independently revalidate its identity and record the required EventStore/release-owner approvals. This conditional dependency does not resequence the epics or block source/package parity closure.
```

**Rationale:** Makes the cross-epic relationship discoverable from the execution-gate authority while preserving AD-22's three independent identity modes.

### Edit 2 — `epics.md`, Story 1.20 Dependency Header

**OLD:**

```markdown
**Cross-epic dependency (intentional, governed):** Recording an approved runtime identity with a *deployed EventStore image digest* is satisfiable only by a conforming two-platform container release. **Story 3.12 (Epic 3)** is the scoped corrective item that produces that release; Story 1.20 independently revalidates and selects its identity under its A/B/C authorization gates. **Epic 1 cannot reach `done` until Story 3.12 delivers a conforming release *and* named EventStore/release-owner approval is recorded.** This forward dependency is deliberate — it is not resolved by reordering epics or rolling back completed work.
```

**NEW:**

```markdown
**Deployed-mode cross-epic dependency (intentional, governed):** When Story 1.20 selects a *deployed EventStore image identity*, a conforming two-platform container release is required. **Story 3.12 (Epic 3)** is the scoped corrective item that produces that release; Story 1.20 independently revalidates and selects its identity under the applicable A/B/C authorization gates. **Epic 1 cannot reach `done` on the deployed-mode path until Story 3.12 delivers a conforming release and named EventStore/release-owner approval is recorded.** Source and package paths may close against their approved exact identities without Story 3.12 when every gate applicable to the selected mode is satisfied. This conditional forward dependency is deliberate; it is not resolved by reordering epics or rolling back completed work.
```

**Rationale:** Removes the contradiction with the “where applicable” acceptance criterion and makes clear that Story 3.12 gates deployed mode only.

### Edit 3 — `epics.md`, Query Response Provenance Gate

**OLD:**

```markdown
Query-response provenance is governed by architecture invariants AD-14 and AD-15. Story 1.2 owns the EventStore platform contract, route stamping, route-aware gateway enforcement, typed-client propagation, and real-gateway-path evidence before any consumer may claim current/stale projection state. Story 2.11 owns generated REST and Tenants consumption only.

No UI or generated-API story may render current/stale state or projection version unless provenance is `ProjectionBacked`; handler-computed and unknown routes render `Unknown`. Story 4.7 remains the maintainer-approved Tenants producer follow-up and does not block the EventStore platform prerequisite.
```

**NEW:**

```markdown
Query-response provenance is governed by architecture invariants AD-14 and AD-15. Story 1.2 owns the EventStore platform contract, route stamping, route-aware gateway enforcement, typed-client propagation, and real-gateway-path platform evidence before any consumer may claim projection-backed state. Story 2.6 owns Tenants UI client-library/host alignment and canonical UX presentation of an already-classified evidence state only. Story 2.11 exclusively owns generated REST and Tenants consumption of the provenance contract, including preservation, authoritative lifecycle/header selection, the fail-closed `Unknown` fallback, and real-gateway/persisted-read-model consumer proof.

No UI or generated-API story may render authoritative lifecycle state or projection version unless provenance is `ProjectionBacked`; handler-computed and unknown routes render `Unknown`. Story 4.7 remains the maintainer-approved Tenants producer follow-up and does not block the EventStore platform prerequisite.
```

**Rationale:** Establishes one story and one proof bar for consumer provenance while leaving UX presentation in Story 2.6.

### Edit 4 — `epics.md`, Story 2.6 Review Boundary And Acceptance Criterion

**Owner/review boundary — OLD:**

```markdown
**Owner / review boundary:** Amelia (Developer); Sally (UX Designer) and the Tenants maintainer review UI-host and evidence-state behavior.
```

**Owner/review boundary — NEW:**

```markdown
**Owner / review boundary:** Amelia (Developer); Sally (UX Designer) and the Tenants maintainer review the UI-host boundary, typed-client usage, and canonical presentation of already-classified evidence states only. Provenance preservation, authoritative state selection, fail-closed `Unknown`, and gateway-path proof are owned exclusively by Story 2.11.
```

**Acceptance criterion — OLD:**

```markdown
**Given** query provenance is `ProjectionBacked`, `HandlerComputed`, or `Unknown`
**When** lifecycle is rendered
**Then** only projection-backed evidence may show `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, or `LocalOnly`
**And** handler-computed, missing, or invalid provenance renders `Unknown` without claiming projection-confirmed success.
```

**Acceptance criterion — NEW:**

```markdown
**Given** the Tenants typed-client boundary supplies an already-classified canonical evidence state under the Story 2.11 provenance contract
**When** focused UI acceptance renders that state
**Then** the UI applies the canonical support-safe and accessible treatment for the supplied lifecycle state
**And** Story 2.6 cites Story 2.11 for provenance preservation, authoritative state selection, fail-closed `Unknown`, and real-gateway/persisted-read-model proof rather than re-proving or signing off those behaviors.
```

**Rationale:** Keeps Story 2.6 testable as client-library and UX conformance without allowing it to close the stronger provenance proof.

### Edit 5 — `epics.md`, Story 2.11 Ownership And Focused Validation

**Section:** Insert after `**Requirements covered:**`.

**OLD:**

Story 2.11 has no explicit owner/review-boundary or focused-validation metadata at its header.

**NEW:**

```markdown
**Owner / review boundary:** Amelia (Developer); an EventStore reviewer verifies generated REST and Tenants provenance preservation, while the Tenants maintainer verifies consumer compatibility against the accepted Tenants identity. Story 2.6 retains Sally's UX-presentation review and cannot substitute for this provenance proof.
**Focused validation:** generated REST runtime tests and Tenants consumer integration tests covering projection-backed, handler-computed, unknown, missing, and invalid provenance through the real gateway and persisted read-model path; mock-only metadata cannot close the story.
```

**Rationale:** Makes the exclusive owner and existing high proof bar explicit rather than relying on inference from the acceptance criteria.

### Edit 6 — Canonical SPEC Package, Mode-Specific Dependency Preservation

#### `SPEC.md`, CAP-8 success

**OLD:**

```markdown
  - **success:** The seven-epic plan has no forward dependency, all eight oversized parents are replaced by focused children, active identifiers and evidence are auditably migrated, and a fresh implementation-readiness assessment reports no structural blocker.
```

**NEW:**

```markdown
  - **success:** The seven-epic plan has no unconditional forward dependency; Story 1.20's deployed-mode identity path carries the explicit conditional Story 3.12 release gate while source/package paths remain independently closable. All eight oversized parents are replaced by focused children, active identifiers and evidence are auditably migrated, and a fresh implementation-readiness assessment reports no structural blocker.
```

#### `SPEC.md`, Success signal

**OLD:** `no later-epic prerequisite`

**NEW:** `no ungoverned or unconditional later-epic prerequisite`

#### `readiness-gates.md`, Dependency direction

**OLD:**

```markdown
| Dependency direction | Story 1.2 owns the platform provenance prerequisite; Stories 1.16 and 1.20 depend only on Epic 1 work, while Story 2.11 consumes the contract for generated REST/Tenants behavior. |
```

**NEW:**

```markdown
| Dependency direction | Story 1.2 owns the platform provenance prerequisite. Story 1.16 and Story 1.20 source/package closure depend only on Epic 1 work; Story 1.20 deployed-mode closure has the explicit conditional Story 3.12 release gate. Story 2.6 owns typed-client/UI presentation of already-classified states, while Story 2.11 exclusively owns generated REST/Tenants provenance consumption and production-path proof. |
```

#### `readiness-gates.md`, Readiness rerun

**OLD:** `no forward-dependency`

**NEW:** `no ungoverned or unconditional forward-dependency`

**Rationale:** The SPEC declares itself canonical. These preservation edits prevent MAJ-1/MAJ-2 from remaining contradicted by its readiness companion after `epics.md` is corrected.

### Explicit Non-Edits And Separate Follow-Up

- No `prd.md`, `architecture.md`, UX, project documentation, or sprint-status change.
- No story status, identifier, owner role, requirement coverage, or runtime behavior changes.
- **Separate follow-up found during mandatory SPEC loading:** `SPEC.md` and `readiness-gates.md` still pin FrontComposer Shell/Contracts.UI `3.2.2`, contradicting architecture/epics' Builds-catalog `HexalithFrontComposerVersion` (`4.0.1` at the reviewed baseline). This predates MAJ-1/MAJ-2 and is not silently added to this proposal. It should be reconciled in a separately approved documentation correction or explicitly folded in during revision.

## 5. Implementation Handoff

**Scope classification:** Minor — direct implementation by the Developer agent after approval.

| Recipient | Responsibility |
| --- | --- |
| Developer agent | Apply Edits 1–6 exactly, preserving unrelated user changes. |
| EventStore reviewer | Confirm the Story 1.20 mode boundary and Story 2.11 production-path proof wording. |
| Sally / Tenants maintainer | Confirm Story 2.6 retains UX/client-host acceptance without duplicate provenance sign-off. |
| Assessor | Recheck MAJ-1 and MAJ-2 as closed; retain the report's explicit condition concerning the two previously unenumerated Criticals. |

### Success Criteria

1. Execution Gates and Story 1.20 state that Story 3.12 gates deployed mode only; source/package identity paths remain independently closable.
2. The canonical SPEC package carries the same conditional mode boundary and contains no unconditional “Story 1.20 depends only on Epic 1” statement.
3. Story 2.6 owns typed-client/UI-host alignment and canonical presentation only.
4. Story 2.11 exclusively owns provenance preservation, authoritative state/header selection, fail-closed `Unknown`, and real-gateway/persisted-read-model proof.
5. PRD, architecture, UX, story coverage, story identifiers, statuses, and MVP scope remain unchanged.

## Change Analysis Checklist

### 1. Understand The Trigger And Context

- [x] **1.1** Triggering stories identified: 1.20, 2.6, and 2.11.
- [x] **1.2** Core problem classified as planning self-consistency/ownership ambiguity discovered during readiness review.
- [x] **1.3** Evidence collected from the readiness report, epics, architecture, canonical UX, SPEC package, sprint status, and project query/lifecycle documentation.

### 2. Epic Impact Assessment

- [x] **2.1** Epics 1 and 2 remain completable as planned after wording/ownership correction.
- [x] **2.2** Existing execution-gate and story text changes are sufficient; no epic scope change.
- [x] **2.3** Remaining epics reviewed; only conditional Story 3.12 handoff is affected.
- [x] **2.4** No epic becomes obsolete and no new epic is needed.
- [x] **2.5** No priority or epic-order change.

### 3. Artifact Conflict And Impact Analysis

- [x] **3.1** PRD checked; no change.
- [x] **3.2** Architecture checked; AD-14/AD-15/AD-22 already support the correction.
- [x] **3.3** Canonical UX checked; no change.
- [!] **3.4** Canonical SPEC/companion requires preservation edits; unrelated FrontComposer `3.2.2` drift recorded as a separate follow-up. No code/deployment/test/CI change.

### 4. Path Forward Evaluation

- [x] **4.1** Direct Adjustment viable — Low effort / Low risk.
- [N/A] **4.2** Rollback not viable or useful.
- [N/A] **4.3** MVP review not needed.
- [x] **4.4** Direct Adjustment selected for smallest coherent correction.

### 5. Sprint Change Proposal Components

- [x] **5.1** Issue summary complete.
- [x] **5.2** Epic/story/artifact impacts documented.
- [x] **5.3** Recommended path and alternatives documented.
- [x] **5.4** MVP unaffected; ordered edit plan defined.
- [x] **5.5** Minor-scope Developer handoff defined.

### 6. Final Review And Handoff

- [x] **6.1** Applicable checklist items addressed; separate pre-existing SPEC drift is explicit.
- [x] **6.2** Proposal checked against PRD, architecture, UX, epics, SPEC companions, sprint status, and project documentation.
- [x] **6.3** Administrator explicitly approved the proposal on 2026-07-20.
- [N/A] **6.4** Sprint-status update not applicable; no epic/story identity or status change.
- [x] **6.5** Minor-scope handoff routed to and implemented by the Developer agent; EventStore, UX/Tenants, and assessor review responsibilities remain as listed in Section 5.

---

## Workflow Execution Log

| Date | Event | Result |
| --- | --- | --- |
| 2026-07-20 | Correct Course activated; project context and canonical planning/SPEC/UX inputs loaded. | Complete |
| 2026-07-20 | Administrator selected Batch mode. | Complete |
| 2026-07-20 | Change-analysis checklist and six-edit proposal completed. | Complete |
| 2026-07-20 | Administrator continued review and explicitly approved implementation. | Approved |
| 2026-07-20 | Edits applied to `epics.md`, `SPEC.md`, and `readiness-gates.md`. | Complete |
| 2026-07-20 | `git diff --check` and focused mode/ownership consistency assertions executed. | Passed |
| 2026-07-20 | Repository Markdown lint configuration inspected. | `_bmad-output/**` is intentionally excluded; structural fence/trailing-whitespace checks passed for this proposal. |

**Implementation result:** MAJ-1 and MAJ-2 corrections are applied. No code, PRD, architecture, UX, sprint-status, story identifier, story status, or MVP-scope change was made. The separate pre-existing FrontComposer `3.2.2` SPEC drift remains explicitly out of scope.
