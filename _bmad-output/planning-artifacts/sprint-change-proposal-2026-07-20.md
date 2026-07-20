# Sprint Change Proposal — 2026-07-20

**Project:** eventstore (Hexalith.EventStore)
**Author:** Amelia (Developer) via `bmad-correct-course`
**Trigger:** Implementation Readiness Assessment 2026-07-20 — *NOT READY for broad Phase 4 execution*
**Mode:** Batch
**Scope classification:** **Moderate** (backlog/artifact reorganization; no MVP redefinition, no rollback)
**Status:** DRAFT — awaiting user approval before artifact edits are applied

---

## Section 1 — Issue Summary

The 2026-07-20 implementation-readiness assessment returned **NOT READY for broad Phase 4 execution**. Positives are strong: all required artifacts exist, FR coverage is 37/37 (100%), and UX/PRD/architecture are materially aligned. The blocker is a set of **plan-consistency and executability defects** that make it unsafe to hand the plan to broad, parallel Phase 4 development as-is:

1. **A cross-epic forward dependency** (Story 1.20 in Epic 1 depends on Story 3.12 in Epic 3) that is real and governed in `sprint-status.yaml` comments but **not documented in the epics themselves** — so naive epic-order execution would assume Epic 1 closes independently.
2. **A stale hardcoded version requirement** (Story 7.14 / AD-21 one-liner pin FrontComposer `3.2.2`) that **contradicts the already-corrected `architecture.md`** and the Builds-owns-versions rule.
3. **Epic-sized stories** (4.8, 8.2, 3.12, 7.14) that cannot be handed to a single dev-loop unit without decomposition — but whose correct disposition varies sharply by current status.

### Evidence (verified against the live tree, not the report)

| Claim | Verification |
|---|---|
| FrontComposer `3.2.2` is stale | Builds central `HexalithFrontComposerVersion` = **`4.0.1`** (`references/Hexalith.Builds/Props/Directory.Packages.props:9`). FluentUI = `5.0.0-rc.4-26180.1` (V5 ✓). |
| `Contracts.UI` isn't even a catalog package | Central props expose `Hexalith.FrontComposer.Contracts` — **no `.Contracts.UI`** entry exists. |
| Architecture is already correct | `architecture.md:445` pins catalog `HexalithFrontComposerVersion` **`4.0.1`**; `architecture.md:296` (AD-21) already says resolve from the catalog variable and that `Contracts.UI` "is added there under that variable before adoption, **never pinned locally**." **`epics.md` drifted; `architecture.md` did not.** |
| 1.20 → 3.12 dependency is real | `sprint-status.yaml:82-83,131-133`: Story 3.12 (`review`) produced `v3.77.2` two-platform OCI index; Story 1.20 (`in-progress`) is blocked on selecting/approving it. `epics.md:1954-1957` — 3.12 references 1.20, but 1.20 does **not** reference 3.12. |
| 4.8 governed by same-day SCP | `sprint-status.yaml:146-149` + `sprint-change-proposal-2026-07-20-oq8-durable-idempotency-admission.md` (approved 2026-07-20). |
| PRD is clean | `prd.md` never pins a FrontComposer version (`prd.md:139,309-310`) and states 1.20 done-evidence without naming 3.12 (`prd.md:251,439`). |

### Operational flag (report integrity)

The on-disk `implementation-readiness-report-2026-07-20.md` is a **4-line stub** — the substantive findings exist only in the invocation summary. That summary names **3** primary blockers but asserts **5** critical defects; **2 critical defects are unenumerated** and cannot be dispositioned here. See Action Item A-1.

---

## Section 2 — Impact Analysis

### Epic Impact

- **Epic 1** — `in-progress`. Closure (`Story 1.20 done` → `Epic 1 done`) is gated on an Epic 3 story (3.12) plus owner approval. No scope change; needs the dependency made **explicit** so it is not read as a defect or executed out of order.
- **Epic 3** — `in-progress`. Story 3.12 is `review` with a conforming `v3.77.2`. No change; it is the intentional "scoped corrective item" Story 1.20 itself mandates.
- **Epic 4** — `in-progress`. Story 4.8 is oversized but governed by a same-day approved SCP. No re-split; decomposition belongs in its dedicated story file.
- **Epic 7 / Epic 8** — `backlog`. 7.14 carries the stale version pin; 8.2 is post-MVP, gated on the 8.1 spec. Neither is in the immediate-ready Phase-4 lane, so neither blocks *broad* execution on its own.

### Story Impact

| Story | Status | Impact | Disposition |
|---|---|---|---|
| 1.20 | in-progress | Missing explicit dependency on 3.12 | Add cross-epic dependency note (Edit 3) |
| 3.12 | review | Flagged "epic-sized" but effectively delivered | No split; record disposition |
| 4.8 | in-progress | Oversized; governed by 2026-07-20 OQ8 SCP | No re-split; decompose in dedicated story file (Edit 4) |
| 7.14 | backlog | Stale FrontComposer `3.2.2` pin | Align to catalog variable (Edit 2); flag for split at create-story |
| 8.2 | backlog | Oversized; post-MVP, gated on 8.1 | Defer split to 8.1 spec authoring; mark multi-slice / non-Phase-4-MVP (Edit 5) |

### Artifact Conflicts

- **`epics.md`** — CONFLICTS with `architecture.md` on FrontComposer version (`epics.md:287`, `epics.md:2996`); missing 1.20→3.12 dependency (`epics.md` Story 1.20). **Edits 1–4.**
- **`architecture.md`** — already correct (AD-21, Stack table). **No change.**
- **`prd.md`** — no conflict. **No change.**
- **`ux.md` / UX designs** — no conflict (assessment: UX already contains archived requirements). **No change.**
- **`sprint-status.yaml`** — dependency documented in comments; add one concise cross-reference under `3-12` (Edit 6) and reflect any approved dispositions.

### Technical Impact

None to runtime code. All edits are planning-artifact text. The FrontComposer alignment removes a future build-break risk (a locally pinned `3.2.2` against a `4.0.1` catalog, plus a non-existent `Contracts.UI` package id) before Story 7.14 is ever coded.

---

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment (Hybrid with light documentation/governance).**

Rejected alternatives:
- **Option 2 — Rollback:** Nothing to roll back; the defects are plan-consistency, not bad committed work. **Not viable.**
- **Option 3 — MVP Review:** MVP is intact (FR 37/37, PRD/architecture aligned). Reducing scope would be an over-correction. **Not viable.**

Rationale: the two critical defects are a **stale artifact vs. an already-correct artifact** (fix = alignment) and a **real-but-undocumented dependency** (fix = make it explicit). The "epic-sized" stories are mostly already-delivered (3.12), governed by a fresh approved decision (4.8), or post-MVP and gated (8.2, 7.14) — so the safe correction is **disposition + decomposition-at-create-time**, not disruptive re-splitting that would contradict same-day approvals or re-open coverage maps.

- **Effort:** Low (planning-artifact edits + dispositions).
- **Risk:** Low. No runtime change, no coverage-map churn, no reopening of approved SCPs.
- **Timeline:** Unblocks broad Phase 4 execution immediately upon applying edits + enumerating the 2 missing critical defects (A-1).

---

## Section 4 — Detailed Change Proposals

> **Batch — all edits below are proposed together and applied only on approval.**

### Edit 1 — `epics.md:287` (AD-21 one-liner) — align FrontComposer version to catalog

**OLD**
```
- AD-21 makes `src/Hexalith.EventStore.Admin.UI` the single consolidated EventStore UI under resource `eventstore-admin-ui`, FrontComposer module `event-store-admin`, matching Shell/Contracts.UI `3.2.2`, and Fluent UI V5. No additional UI host is created.
```
**NEW**
```
- AD-21 makes `src/Hexalith.EventStore.Admin.UI` the single consolidated EventStore UI under resource `eventstore-admin-ui`, FrontComposer module `event-store-admin`, with all consumed FrontComposer packages (`Shell`, `Contracts.UI`) resolved from the Builds catalog's single `HexalithFrontComposerVersion` (currently `4.0.1`; `Contracts.UI` is added to that catalog under the same variable before adoption, never pinned locally), and Fluent UI V5. No additional UI host is created.
```
**Rationale:** Aligns `epics.md` to the already-correct `architecture.md:287`/AD-21 and enforces the Builds-owns-versions rule. Removes the stale `3.2.2` and the non-existent locally-pinned `Contracts.UI`.

---

### Edit 2 — `epics.md:2994-2997` (Story 7.14 AC "the UI composes its shell") — same alignment in the acceptance criterion

**OLD**
```
**Given** the UI composes its shell
**When** Debug/source and Release/package graphs are validated
**Then** matching `3.2.2` versions of `Hexalith.FrontComposer.Shell` and `Hexalith.FrontComposer.Contracts.UI` plus Fluent UI V5 are used
**And** the stable module identity is `event-store-admin` with label **Event Store Admin**.
```
**NEW**
```
**Given** the UI composes its shell
**When** Debug/source and Release/package graphs are validated
**Then** `Hexalith.FrontComposer.Shell` and `Hexalith.FrontComposer.Contracts.UI` both resolve from the Builds catalog's single `HexalithFrontComposerVersion` (currently `4.0.1`), with `Contracts.UI` added to that catalog under the same variable before adoption and never pinned locally, plus Fluent UI V5
**And** the stable module identity is `event-store-admin` with label **Event Store Admin**.
```
**Rationale:** Makes the acceptance test verify catalog resolution (the actual invariant) instead of asserting a stale literal that would fail the moment Story 7.14 is built.

---

### Edit 3 — `epics.md` Story 1.20 (after `**Requirements covered:** FR36, NFR12, NFR16`, ~line 1099) — make the forward dependency explicit

**INSERT**
```
**Cross-epic dependency (intentional, governed):** Recording an approved runtime identity with a *deployed EventStore image digest* is satisfiable only by a conforming two-platform container release. **Story 3.12 (Epic 3)** is the scoped corrective item that produces that release; Story 1.20 independently revalidates and selects its identity under its A/B/C authorization gates. **Epic 1 cannot reach `done` until Story 3.12 delivers a conforming release *and* named EventStore/release-owner approval is recorded.** This forward dependency is deliberate — it is not resolved by reordering epics or rolling back completed work.
```
**Rationale:** `epics.md` currently documents the dependency only one-way (3.12 → 1.20). This makes 1.20 → 3.12 explicit so broad epic-order execution does not treat Epic 1 as independently closable. Mirrors `sprint-status.yaml:82-83,131-133`.

---

### Edit 4 — `epics.md` Story 4.8 (after its `**Requirements covered:**` line, ~line 2167) — mark multi-slice, no re-split

**INSERT**
```
**Sizing / delivery note:** Story 4.8 is an intentionally multi-slice story governed by `sprint-change-proposal-2026-07-20-oq8-durable-idempotency-admission.md`. It is delivered by decomposing its acceptance criteria into tracked task slices inside its dedicated story file, not as a single dev-loop unit. Its scope and coverage map (FR27, NFR7, NFR16) are not re-partitioned by this proposal.
```
**Rationale:** Answers the "epic-sized" flag without contradicting the same-day approved OQ8 SCP or reopening the coverage map. Directs decomposition to the correct place (the story file mandated by `sprint-status.yaml:148`).

---

### Edit 5 — `epics.md` Story 8.2 (after its `**Focused validation:**` block, ~line 3137) — defer split, mark non-Phase-4-MVP

**INSERT**
```
**Sizing / delivery note:** Story 8.2 is post-MVP (Epic 8) and gated on the approved Story 8.1 spec/ADR. Its multi-slice decomposition (engine, key lifecycle, production-backend conformance, Parties dual-provider parity, rollback rehearsal) is authored **at Story 8.1 spec time**, not in this proposal. It does not block broad Phase 4 execution and its 12-artifact G5 proof packet remains a single closure gate.
```
**Rationale:** Confirms 8.2 is out of the Phase-4-MVP critical path, so it is not a broad-execution blocker; defers the split to where the spec authority lives.

---

### Edit 6 — `sprint-status.yaml` — one-line cross-reference under `3-12`

**INSERT** (comment above `3-12-multi-platform-eventstore-container-publishing-correction: review`)
```
  # Gates Epic 1 done: Story 1.20 AC8 (deployed image digest) selects this release's identity.
```
**Rationale:** Makes the Epic-1-gating relationship discoverable at the exact status row a sprint planner reads. (Reflected in checklist item 6.4.)

---

### Non-edits (explicit dispositions, no artifact change)

- **Story 3.12 "epic-sized" flag → dismissed.** Story is in `review` with conforming `v3.77.2`; splitting delivered work is inappropriate.
- **`architecture.md` → unchanged.** AD-21 and the Stack table are already correct; the drift was one-directional in `epics.md`.
- **`prd.md`, `ux.md` → unchanged.** No conflict found.

---

## Section 5 — Implementation Handoff

**Scope: Moderate** → Product Owner / Developer coordination, with Architect sign-off on the AD-21 alignment wording.

| Recipient | Responsibility |
|---|---|
| **Amelia (Developer) / PO** | Apply Edits 1–6 to `epics.md` and `sprint-status.yaml` after approval. |
| **Winston (Architect)** | Confirm Edits 1–2 match AD-21 intent (they restate `architecture.md:296`); no architecture edit required. |
| **John (PM)** | **A-1:** enumerate the 2 unlisted critical defects (of the stated 5) and the ~11 minor findings, or confirm the 3 named blockers were the full critical set. |
| **Assessor / Amelia** | **A-2:** backfill `implementation-readiness-report-2026-07-20.md` from this proposal (or re-run the readiness assessment) so the readiness record is not a stub. |

### Success criteria

1. `epics.md` and `architecture.md` agree on FrontComposer version resolution (catalog variable, no local `3.2.2`, no locally-pinned `Contracts.UI`).
2. Story 1.20 states its dependency on Story 3.12, and `sprint-status.yaml` shows the Epic-1-gating cross-reference.
3. Stories 4.8 and 8.2 carry explicit multi-slice delivery notes; 3.12 disposition recorded.
4. A-1 closes the enumeration gap so a subsequent readiness re-check can move to READY.

### Action Items (post-approval)

- **A-1 (PM):** Enumerate the 2 remaining critical defects + ~11 minor findings, or confirm scope. **Blocks a clean READY re-check.**
- **A-2 (Assessor):** Backfill/repair the 2026-07-20 readiness report.

---

## Change Analysis Checklist — Results

- **§1 Trigger & context:** Done. Trigger = 2026-07-20 readiness assessment; type = *plan-consistency / executability defects surfaced during readiness review*.
- **§2 Epic impact:** Done. Epics 1, 3, 4, 7, 8 assessed; no epic obsoleted, none added, no resequencing (forward dep is intentional).
- **§3 Artifact conflicts:** Done. `epics.md` ↔ `architecture.md` conflict identified; PRD/UX clean; `sprint-status.yaml` minor add.
- **§4 Path forward:** Done. Option 1 (Direct Adjustment/Hybrid) selected; Options 2 & 3 not viable.
- **§5 Proposal components:** Done (this document).
- **§6 Final review & handoff:** **Action-needed** — pending user approval (6.3) and A-1 enumeration gap (6.1).

---

*Generated by `bmad-correct-course`. No artifact edits have been applied; this proposal is awaiting approval.*
