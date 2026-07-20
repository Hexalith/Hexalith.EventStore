# Reconciliation — 2026-07-17 Split Story 2.7 Pre-Authorization Correction From Runtime Adoption

- **Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-17.md`
- **PRD:** `_bmad-output/planning-artifacts/prd.md` (status `final`, updated 2026-07-19)
- **Verdict:** `downstream-only` for all requirement content; one PRD provenance gap (missing frontmatter `source_artifacts` entry)

## 1. Input

Approved (Administrator, 2026-07-17; implemented 2026-07-17) sprint change proposal splitting the active Tenants Story 2.7 into:

- **Story 2.7 — Pre-Authorization Registration And Provenance Correction:** EventStore-owned correction of stale sample registrations (`orders`/`inventory` registered vs `counter`/`greeting` hosted) and the live source-topology query-provenance proof (`404` / `query_projection_missing` at commit `772cdfef...`). Prerequisite for Story 1.20; changes no dependency identity.
- **Story 2.12 — Tenants Runtime Identity Adoption And Package-Mode Validation:** receives former AC4-AC7 (source gitlink adoption, package/hash adoption, Gateway graph alignment, dual-mode validation evidence) plus a fail-closed Story 1.20 activation gate; remains `backlog` until Story 1.20 durably authorizes migration.

The proposal self-classifies as "an implementation-slicing correction, not a new product requirement or architectural pivot" and records "MVP impact: None. Scope is redistributed, not added or removed." Its artifact table asserts `prd.md | No change. FR15, FR21, FR22, NFR9, NFR12, and NFR16 already cover the preserved scope.`

## 2. Approved Changes (enumerated from sections 4.1-4.6 and section 5)

| # | Change | Proposal section |
| --- | --- | --- |
| C1 | Story 2.7 retitled/rescoped to pre-authorization registration/provenance correction only | 4.1 |
| C2 | Story 2.7 AC3 revised: reviewable while Story 1.20 is non-authorizing; review entry requires no dependency-identity change | 4.2 |
| C3 | Story 2.7 AC4-AC7 removed from scope | 4.3 |
| C4 | New Story 2.12 created in Epic 2 with activation gate + preserved adoption criteria; `backlog`, no implementation story file until Story 1.20 authorizes | 4.4 |
| C5 | Epic 2 / `sprint-status.yaml` sequencing and sole-owner references rewritten to the split ownership; dependency order Story 2.7 → Story 1.20 → Story 2.12 | 4.5, 2 |
| C6 | Story 1.20 adoption register split into "Tenants prerequisite" (2.7) and "Tenants adoption" (2.12) rows; proof-packet references reconciled | 4.6, 2 |
| C7 | Historical outbound-header "Story 2.7" (renumbered 2.10) references preserved untouched | 2 |
| C8 | Handoff/routing: Developer completes 2.7 AC1-AC3 only; Tenants maintainer reviews 2.12 after authorization | 5 |

## 3. PRD-Scoped vs Downstream Classification

PRD-scoped means it changes FR/NFR intent, MVP scope, constraints/guardrails, glossary, success metrics, or FR-to-epic traceability. Downstream-only means story slicing, sequencing, acceptance criteria, architecture mechanisms, or test evidence (owned by `epics.md` / `architecture.md` / story artifacts / `sprint-status.yaml`).

| # | Classification | Basis |
| --- | --- | --- |
| C1 | Downstream-only | Story title/scope; `epics.md` + active story artifact own this. No FR intent changes: the Tenants proof requirement (FR15) is unchanged in what must ultimately be true. |
| C2 | Downstream-only | Acceptance-criterion wording and story lifecycle/review semantics; epics/story-artifact owned. |
| C3 | Downstream-only | Acceptance-criteria redistribution between stories; no criterion is deleted from product intent — all four are preserved verbatim in Story 2.12. |
| C4 | Downstream-only | Story creation inside existing Epic 2. FR-to-epic traceability (PRD §11.1) is unaffected: FR15/FR21/FR22 remain Epic 2/Epic 3 mapped, and Story 2.12 lives in Epic 2. FR-to-*story* mapping is owned by `epics.md` (SM2 delegates story-level mapping there). NFR §11.2 story-coverage rows are unaffected: Story 2.7 never appeared in §11.2 (NFR9 → 3.5/3.8/3.11; NFR16 → 1.9-1.15/3.11/7.4; NFR12 is not a high-risk-table row), so the split introduces no stale story ID in the PRD. Verified: no occurrence of "2.7", "2.10", or "2.12" anywhere in `prd.md`. |
| C5 | Downstream-only | Sequencing/tracker ownership; explicitly `epics.md` + `sprint-status.yaml` per PRD §1 ("`epics.md` owns story slicing, sequencing, acceptance criteria, and implementation handoff"). |
| C6 | Downstream-only | Story 1.20 packet/register mechanics; FR36 intent (owner-approved packet, approved runtime SHA, matching consumer checkout) is untouched. |
| C7 | Downstream-only | Historical-reference hygiene in implementation artifacts; the PRD never referenced the outbound-header story by number (AD-18 policy entered the PRD baseline via the 2026-07-07/2026-07-13 proposals already listed in `source_artifacts`). |
| C8 | Downstream-only | Handoff/ownership assignments; epics/story owned. |
| — | **PRD-scoped (provenance only):** recording this approved proposal in the PRD frontmatter `source_artifacts` list | PRD §1 states the baseline "is derived from the approved sprint-change proposals"; every other approved proposal (2026-07-02 through 2026-07-19) is listed. Omission breaks the PRD's own provenance claim. |

## 4. Coverage Check Per Item

The proposal claims FR15, FR21, FR22, NFR9, NFR12, NFR16 already cover the preserved scope. Verified against the current PRD text:

- **FR15** (`prd.md` §6.2, line 161): Tenants external API proof + UI client-library adoption + platform query-metadata path for freshness/ETag/paging evidence. Covers the product intent of both the provenance correction (evidence must come from the platform query metadata path — the `HandlerComputed` provenance proof Story 2.7 must produce) and the eventual adoption proof (Story 2.12 AC5). Covered; no edit needed.
- **FR21** (§6.3, line 176): explicit `UseHexalithProjectReferences=true` source opt-in, package-safe defaults, Builds-owned version catalog. Covers Story 2.12 AC2-AC4 conditional source/package intent. Reinforced by §8.1 guardrails ("Require explicit `UseHexalithProjectReferences=true`...", "Never initialize nested submodules; only root-declared submodules under `references/`"). Covered.
- **FR22** (§6.3, line 178): release commands assert package-reference mode and avoid packaging submodule projects. Covers Story 2.12 AC3/AC4 mixed-graph prohibition intent. Covered.
- **NFR9** (§7, line 262): reproducible Release behavior independent of submodule checkout state. Covers dual-mode reproducibility intent. Covered.
- **NFR12** (§7, line 265): backward compatibility for additive framework changes / existing generic gateway APIs. Covers "no consumer dependency-identity change" during the pre-authorization correction. Covered.
- **NFR16** (§7, line 269): integration tests assert persisted/production-path evidence, not smoke status. Covers the source-topology proof reaching the real assertion (the reproduced `404/query_projection_missing` through the live handler path) and Story 2.12's focused evidence suite. Covered.
- **FR36 / SM6 / §11.3** (lines 236-238, 346, 423): the Story 2.7 → 1.20 → 2.12 gate direction is a story-level refinement of the already-stated FR36 pattern (owner-approved packet + exact runtime SHA before consumer migration). §11.3's readiness narrative names Stories 1.14-1.20 and 8.6 but never Story 2.7/2.12, so no PRD text becomes stale. Covered.
- **FR4 provenance classification** (§6.1, line 141): projection-backed / handler-computed / unknown provenance and the prohibition on inferring lifecycle from ETags already state the intent behind the query-provenance proof Story 2.7 corrects. Covered.
- **MVP scope** (§9): proposal records "MVP impact: None"; §9.1 bullet "REST generator contract and controller emission work with Sample and Tenants external API proofs" is unchanged by redistribution. Covered.
- **Success metrics** (§10): SM2 requires every FR to map to at least one epic *and story in `epics.md`* — satisfied by the downstream `epics.md` rewrite (Story 2.12 carries FR15/FR21/FR22), not by a PRD edit. No metric wording change needed.
- **Glossary / constraints** (§4, §8): no new product-level term ("pre-authorization" is a story-lifecycle notion) and no new guardrail; existing §8.1 submodule/package guardrails already express the fail-closed identity boundary. Covered.

## 5. Gaps With Proposed Minimal Edits

**Gap G1 — frontmatter provenance (only gap found).** The approved, implemented 2026-07-17 proposal is absent from the PRD frontmatter `source_artifacts` list, which currently jumps from `sprint-change-proposal-2026-07-16.md` (line 40) to `sprint-change-proposal-2026-07-18.md` (line 41). The PRD's §1 provenance claim and the list's own date-ordered convention require the entry.

Minimal edit (target: `prd.md` frontmatter, insert between current lines 40 and 41):

```yaml
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-17.md
```

No `updated:` bump is strictly required by this metadata-only correction; if the maintainer's convention bumps `updated:` on any frontmatter change, today's value (`2026-07-19`) already satisfies it. No body-text edit is proposed.

## 6. Conflicts With Prior PRD Decisions

- **None with requirement ownership.** The proposal's split reasoning matches the standing decision (PRD §1; prior reconciliations): PRD owns FR/NFR intent, MVP scope, and FR-to-epic traceability; `epics.md` owns slicing, sequencing, and acceptance criteria. Every substantive change lands where that decision says it belongs.
- **None with FR36/AD-22 gating.** The activation-gate direction (2.7 prerequisite → 1.20 authorization → 2.12 adoption) is consistent with FR36, SM6, and §11.3; the proposal executes the gate rather than altering it.
- **Minor discrepancy (not a conflict):** the proposal's artifact table states `prd.md | No change`, which is accurate for PRD body text but overlooked the frontmatter `source_artifacts` maintenance obligation (G1 above). The proposal's checklist item 6.2 ("reviewed for consistency with PRD") therefore stands for intent but not for provenance bookkeeping.
- **Historical Story 2.7 identifier:** the outbound-DAPR-header policy (AD-18) proposals of 2026-07-07/2026-07-13 are already in `source_artifacts`; the PRD never binds AD-18 to a story number, so the 2.7 → 2.10 renumbering history creates no PRD ambiguity.

## 7. Disposition

Apply G1 (one-line frontmatter insertion). All other approved changes are downstream-only and are represented — or must be represented — in `epics.md`, the active Story 2.7 artifact, the Story 2.12 backlog entry, `sprint-status.yaml`, and the Story 1.20 story/proof packet, per the proposal's own handoff. No FR/NFR wording, MVP boundary, glossary, guardrail, success-metric, or §11 traceability edit is required.
