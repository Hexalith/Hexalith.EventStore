# Reconciliation — 2026-07-18 Align Story 3.1 With The Dedicated Live-Sidecar Topology

- **Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-1-live-sidecar-topology.md` (status `final`, approved by Administrator 2026-07-18, scope `minor`, target Story 3.1)
- **PRD:** `_bmad-output/planning-artifacts/prd.md` (status `final`, updated 2026-07-19)
- **Verdict:** `gaps-found` — all requirement content is downstream-only; two PRD-scoped bookkeeping gaps (missing frontmatter `source_artifacts` entry; stale Story 7.4 identifier in the §11.2 NFR10 coverage row) plus one same-root-cause secondary gap (NFR16 row).

## 1. Input

Approved minor course correction rewriting the Story 3.1 specification only. Story 3.1 was authored against the superseded post-PR-271 topology (one shared `tests/Hexalith.EventStore.Server.Tests` project with `Category!=LiveSidecar` / `Category=LiveSidecar` CI filters). Commit `12baa75c` (2026-07-09) shipped the current authoritative topology: a dedicated `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` project, an unfiltered deterministic lane through the shared `domain-ci.yml@main` caller, an unfiltered live lane in `integration.yml`, and `release.yml` gated only on `CI` completion. The proposal realigns Story 3.1 to verify that shipped topology; no runtime code, test, workflow, DAPR component, package, or release behavior changes.

The proposal's own artifact table asserts: `epics.md | No change` (Story 3.1 criteria already match), `prd.md | No FR17/NFR10 requirement change`, `architecture.md | No change (AD-9/AD-11/AD-12 remain applicable)`, `docs/ci.md | No change`. Checklist item 3.1 records "PRD FR17/NFR10 remain valid; no product-scope change," and the MVP-impact statement is "None."

## 2. Approved Changes (enumerated from sections 2, 4.1-4.7, and 5)

| # | Change | Proposal section |
| --- | --- | --- |
| C1 | Story 3.1 frontmatter/baseline/source paths rewritten: new baseline commit, `topology_origin_commit: 12baa75c`, dedicated-project and shared-workflow sources added; `CLAUDE.md` and the old fixture path removed as sources | 4.1 |
| C2 | Lane model rewritten: both lanes selected by physical project ownership plus workflow input, run unfiltered; traits remain required for discoverability/semantics but no longer select the CI lane; release listens only to successful `CI` completion (no `Integration Tests` dependency) | 4.2 |
| C3 | Live inventory unfrozen: the durable invariant is project membership (every live-`daprd` test in the dedicated project, none in `Server.Tests`); the 14-class count is recorded as evidence, not an acceptance invariant | 4.2, 4.3 AC1 |
| C4 | Obsolete `CLAUDE.md` deliverable removed; `docs/ci.md` and current workflows named the repository-specific CI authorities while `AGENTS.md`/`CLAUDE.md`/Copilot files stay synchronized universal baselines | 4.2, 4.3 AC5, 4.4 |
| C5 | AC1-AC7 rewritten around the dedicated topology: project ownership/traits, deterministic gate via `unit-test-projects`, dedicated unfiltered live lane, fixture at its dedicated-project path with readiness/warm-up and persisted-state evidence, authority reconciliation, no-filter validation evidence, and preserved scope boundaries | 4.3 |
| C6 | Task list replaced; every task demanding `Category!=LiveSidecar`/`Category=LiveSidecar` command filters or `CLAUDE.md` edits removed | 4.4 |
| C7 | Dev notes/code-state block replaced with current structural evidence (slnx membership, thin `ci.yml` caller, shared `domain-ci.yml`, `integration.yml`, `release.yml`, commit `12baa75c`, `docs/ci.md`) | 4.5 |
| C8 | Validation commands corrected: `.slnx` used only for restore/build, each test project run individually and unfiltered, Story 3.10 preflight classifies environment blockers, old #271 counts not preserved as a baseline | 4.6 |
| C9 | Cross-story ownership reassigned: Story 3.10 (not 3.8) is the preflight companion; Story 7.10 (not the now-unrelated Story 7.4) owns adjacent Integration-CI recovery; Story 7.12 owns broader test reclassification | 1, 2, 4.3 AC5, 4.5 |
| C10 | References/Dev Agent Record reconciled; 2026-07-18 halt/blocker evidence preserved; Story 3.1 stays `in-progress` with no tracker-key change until verification and review complete | 4.7, 5 |

## 3. PRD-Scoped vs Downstream Classification

PRD-scoped means it changes FR/NFR intent, MVP scope, constraints/guardrails, glossary, success metrics, or FR-to-epic traceability (plus the PRD's own provenance frontmatter and §11.2 NFR story-coverage rows, which live in the PRD). Downstream-only means story slicing, sequencing, acceptance criteria, architecture mechanisms, or test evidence, owned by `epics.md` / `architecture.md` / story artifacts / `sprint-status.yaml` / `docs/ci.md`.

| # | Classification | Basis |
| --- | --- | --- |
| C1 | Downstream-only | Story-artifact metadata; owned by the Story 3.1 implementation artifact. |
| C2 | Downstream-only | CI lane mechanism (project-selected vs filter-selected). NFR10 (`prd.md` line 263) is deliberately mechanism-neutral — "separate deterministic release-gate tests from live-sidecar/integration tests while preserving live-sidecar coverage in a dedicated lane" — and the physical split satisfies and strengthens it. What "dedicated live-sidecar lane" *must mean* at requirement level is unchanged; how the lane is delimited is architecture/CI-doc territory (`docs/ci.md`, AD-9/AD-11/AD-12). The release-gate direction (release does not depend on the live lane) is already FR17's stated intent ("removed from the per-push release gate"). |
| C3 | Downstream-only | The PRD never mentions a live-test class count; frozen-inventory language existed only in the story. Nothing to change. |
| C4 | Downstream-only | Documentation-authority routing between story artifacts, `docs/ci.md`, and the synchronized universal entry points; the PRD contains no CI-documentation-ownership requirement and never mandates `CLAUDE.md` edits. |
| C5 | Downstream-only | Acceptance criteria; owned by `epics.md`/story artifact (and `epics.md` already matches — proposal checklist 2.2). |
| C6 | Downstream-only | Task mechanics. |
| C7 | Downstream-only | Story dev notes. |
| C8 | Downstream-only for the story; already covered by PRD guardrails | The corrected commands now *conform* to existing §8.1 constraints ("Use `Hexalith.EventStore.slnx` only for restore and build", "Run unit tests by project", lines 278-279) which the superseded story's per-csproj build + filtered solution-project test commands did not exemplify. No guardrail change needed. |
| C9 | **Split: story text downstream-only; §11.2 NFR10 row PRD-scoped** | Replacing 3.8→3.10 and 7.4→7.10 inside Story 3.1 is story-owned. But the PRD's own high-risk NFR story-coverage table (§11.2, line 411) still reads `NFR10 | 3.1, 3.11, 7.4`. Per current `epics.md` (post-2026-07-15 renumbering, "Story 7.4 -> Stories 7.10-7.13"), Story 7.4 is now "Honest Deferred Admin Operations" and does not declare NFR10; Story 7.10 "Integration CI Recovery" declares `FR34, NFR10, NFR16`. The approved proposal makes this reassignment explicit ("Story 7.10 remains the owner of full integration-lane recovery"; success criterion "Story 3.10 and Story 7.10 references replace obsolete Story 3.8 and Story 7.4 ownership claims"). Leaving 7.4 in the NFR10 row contradicts the approved proposal inside PRD-owned traceability and undermines SM3 (line 340), which certifies NFR10 story coverage. Story 3.1 and 3.11 references remain valid (3.11 declares NFR10 in `epics.md`). |
| C10 | Downstream-only | Status/tracker semantics; `sprint-status.yaml` + story artifact owned. |
| — | **PRD-scoped (provenance only):** recording this approved proposal in the PRD frontmatter `source_artifacts` | PRD §1 states the baseline "is derived from the approved sprint-change proposals"; the frontmatter lists the other 2026-07-18 proposals (`sprint-change-proposal-2026-07-18.md`, `...-story-3-5-reconciliation.md`) but not this one. Omission breaks the PRD's provenance claim. |

## 4. Coverage Check Per Item

- **FR17** (§6.3, line 172): "Live DAPR sidecar tests must be tagged and removed from the per-push release gate, then run in a dedicated integration workflow with sidecar warm-up and readiness retry." All four clauses survive the topology change: traits are retained ("Traits remain required for discoverability and semantic classification"), removal from the release gate is now physical (the live project is absent from `unit-test-projects` and `release.yml` has no `Integration Tests` dependency), `integration.yml` is the dedicated workflow, and AC4 preserves `WaitForDaprHealthAsync`/`WarmUpActorRuntimeAsync` bounded readiness and warm-up retries. FR17 never mandated filter-based lane selection, so no wording becomes false. **Covered; no edit.**
- **NFR10** (§7, line 263): requirement text is mechanism-neutral and remains exactly satisfied (deterministic lane = unfiltered `Server.Tests` in shared CI; dedicated lane = unfiltered `Server.LiveSidecar.Tests` in `integration.yml`). **Requirement text covered; the §11.2 coverage row is the gap (G2).**
- **§6.3 done evidence** (line 180): "CI separates release-gate tests from live-sidecar tests" — still literally true, now structurally rather than by filter. **Covered; no edit.**
- **§11.1 FR17 row** (line 377): "Epic 3 - Live-sidecar tests re-tiered off release gate" — epic-level mapping unchanged (proposal checklist 2.x: no epic added/removed/resequenced). **Covered.**
- **§8.1 constraints** (lines 278-279): slnx-for-restore/build-only and test-by-project are already PRD guardrails; the corrected validation commands align with them. **Covered; no edit.**
- **§9 MVP scope** (line 311): "live-sidecar re-tiering" bullet unchanged; proposal states "MVP impact: None" and an MVP review is unnecessary because FR17/NFR10 scope is intact. **Covered.**
- **§4 Glossary**: no lane/live-sidecar/topology term exists or is needed; "physical project ownership" is a CI mechanism, not product vocabulary. **Covered; no edit.**
- **§10 Success metrics**: SM3 (line 340) certifies NFR10 story coverage — its validity is restored by fixing the §11.2 row (G2); no metric wording change. **Covered after G2.**
- **FR34** (§6.7, line 225): "restore meaningful IntegrationTests CI coverage" is story-number-free; Story 7.10 ownership needs no FR wording change. **Covered.**
- **FR31** (§6.4, line 193): the live-sidecar two-writer race prerequisite is untouched; the proposal explicitly preserves the no-new-two-writer-test boundary (AC7). **Covered.**
- **NFR16** (§7, line 269 / §11.2 row line 415): the requirement text (persisted-state evidence, echoed by proposal AC4/AC6 persisted Redis actor-state read-back) is covered. The coverage row's `7.4` entry is stale for the identical renumbering reason as NFR10's — see secondary gap G3.

## 5. Gaps With Proposed Minimal Edits

**Gap G1 — frontmatter provenance.** The approved 2026-07-18 story-3-1 topology proposal is absent from `source_artifacts` (the list currently holds `sprint-change-proposal-2026-07-18.md` at line 41 and `...-story-3-5-reconciliation.md` at line 42 with nothing between).

Minimal edit (target: `prd.md` frontmatter, insert between current lines 41 and 42):

```yaml
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-18-story-3-1-live-sidecar-topology.md
```

Observation while editing here: `sprint-change-proposal-2026-07-17.md` is also still missing (G1 of `reconcile-2026-07-17-story-2-7-preauthorization-split.md`, not yet applied); apply both in one pass if convenient, but only the 2026-07-18 entry is owed by *this* proposal.

**Gap G2 — §11.2 NFR10 story-coverage row cites superseded Story 7.4.** The approved proposal assigns adjacent Integration-CI recovery to Story 7.10 and records Story 7.4 as "now-unrelated"; current `epics.md` confirms (Story 7.4 = "Honest Deferred Admin Operations"; Story 7.10 = "Integration CI Recovery" declaring `FR34, NFR10, NFR16`).

Minimal edit (target: `prd.md` §11.2, line 411):

```text
OLD: | NFR10 | 3.1, 3.11, 7.4 |
NEW: | NFR10 | 3.1, 3.11, 7.10 |
```

Optional wider sync (not required by this proposal): `epics.md` also declares NFR10 on Stories 3.7, 3.8, 7.12, and 7.13; the PRD row is "primary story coverage," so adding them is a maintainer choice, not an obligation of this reconciliation.

**Gap G3 (secondary, same root cause) — §11.2 NFR16 row cites Story 7.4.** Line 415 ends `..., 3.11, 7.4`. The root cause is the 2026-07-15 renumbering (Story 7.4 → 7.10-7.13), not this proposal, but the proposal's approved text ("Story 7.10 remains the owner of full integration-lane recovery; Story 7.12 owns broader test reclassification") makes the staleness explicit inside PRD-owned traceability. Minimal edit if applied in the same pass:

```text
OLD: | NFR16 | 1.9, 1.10, 1.11, 1.12, 1.13, 1.14, 1.15, 3.11, 7.4 |
NEW: | NFR16 | 1.9, 1.10, 1.11, 1.12, 1.13, 1.14, 1.15, 3.11, 7.10 |
```

(`epics.md` NFR16 carriers among the renumbered children are 7.10, 7.11, and 7.12; 7.10 is the minimal like-for-like replacement. Adding 7.11/7.12 is the same optional-widening choice as in G2.) If the maintainer prefers strict scope discipline — mirroring the proposal's own handling of pre-existing brownfield-doc staleness — G3 may instead be routed to a separately scoped 2026-07-15-renumbering hygiene task, but it should not be left uncorrected: SM3 certifies NFR16 coverage through this table.

## 6. Conflicts With Prior PRD Decisions

- **None with requirement intent or ownership.** The proposal lands every substantive change exactly where PRD §1 assigns it: `epics.md`/story artifacts own slicing, acceptance criteria, sequencing; `architecture.md` owns topology decisions (AD-9/AD-11/AD-12 unchanged); the PRD's FR17/NFR10 intent is untouched. No FR/NFR wording, MVP boundary, glossary term, guardrail, or success-metric text changes.
- **No conflict with FR17's "tagged" clause.** Traits are retained for semantics even though they no longer select lanes, so the strictest reading of FR17 still holds; no FR17 rewording is needed or proposed.
- **Minor discrepancy (not a conflict):** the proposal's artifact table states `prd.md | No FR17/NFR10 requirement change`, which is accurate for requirement text but overlooks the PRD-owned §11.2 coverage rows (G2/G3) and the frontmatter provenance obligation (G1) — the same pattern the 2026-07-17 reconciliation recorded.
- **Observed adjacent staleness (out of scope here):** §11.3 (line 427) still says `epics.md` "contains coordinated-slice gates for Stories 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, and 7.4," whereas current `epics.md` marks those parents superseded by focused children (1.3-1.5, 1.8-1.11, 2.4-2.7/2.12, 3.7-3.9, 5.6-5.9, 7.2-7.5, 7.6-7.9, 7.10-7.13). That correction is owed by the 2026-07-15 renumbering proposal's reconciliation, not this one; recorded so it is not silently absorbed into this pass.

## 7. Disposition

Apply G1 (one-line frontmatter insertion) and G2 (NFR10 row `7.4` → `7.10`); apply or explicitly route G3 (NFR16 row, same one-token substitution). Everything else in the approved proposal is downstream-only and is represented in the rewritten Story 3.1 artifact, `sprint-status.yaml` (unchanged `in-progress`), and the already-aligned `epics.md`, `architecture.md`, and `docs/ci.md`. No FR/NFR wording, MVP-scope, constraint, glossary, or success-metric edit is required.
