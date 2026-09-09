# Input Reconciliation — 2026-09-09 Validation Report

## Reconciliation Basis

- **Input:** `validation-report.md` — *Validation Report — eventstore Phase 4 Implementation Readiness Recovery*
- **Canonical target:** `_bmad-output/planning-artifacts/prd.md`, updated 2026-09-09
- **Addendum:** None exists; no addendum content was available to reconcile.
- **Authorized scope:** PRD-only correction. Architecture, epics, tracker, evidence, OQ8 authority, append fencing, acceptance design, and CI policy remain outside this update.
- **Readiness result:** The PRD remains machine-readably blocked with `implementation_readiness_status: blocked` and `implementation_readiness_result: reject`.

Disposition meanings:

- **Fixed in PRD** — the misleading or stale PRD statement is corrected; any named external closure remains open.
- **Blocking owed refinement** — the PRD now states the unresolved condition, owner, trigger, and fail-closed consequence instead of inferring a solution.
- **Still external/unresolved** — completion requires an artifact or authority outside this PRD-only update.
- **Intentionally deferred** — a prior recorded decision deliberately preserves the current structure or scope until its stated revisit condition.

## Finding-by-Finding Reconciliation

| # | Severity | Validation finding | Disposition | Canonical result |
| --- | --- | --- | --- | --- |
| 1 | Critical | FR ownership map contradicted the retired OR12 correction | **Fixed in PRD.** | §11.1 now mirrors current `epics.md` primary-owner declarations, FR1 maps to Story 1.11, duplicate-primary warnings are removed, and named disjoint slices are explicit. SM2 no longer claims OR12 is live. The mechanical ownership/text drift guard is separately blocking under OR8. |
| 2 | Critical | `epics.md` fails its PRD/architecture source-drift gate | **Still external/unresolved.** | The PRD does not refresh hashes or claim reconciliation. SM2 remains partially met, §11.3 blocks downstream handoff, and OR14 requires architecture reconciliation followed by reviewed epic reconciliation and renewed approval; a hash-only refresh is forbidden. |
| 3 | Critical | Normative OQ8 requirements are unavailable inside EventStore | **Blocking owed refinement (OR11).** | §1.1 now makes absent or mismatched normative authority fail readiness and every OQ8 closure claim. OR11 requires a permitted immutable copy, complete approved normative projection, or signed/content-addressed attestation and propagation of repository, path, commit, and digest. The governing bytes remain externally unavailable. |
| 4 | Critical | MVP success could coexist with known concurrent append loss | **Blocking owed refinement (OR4).** | NFR7 class (c) and SM11 are explicitly not met; an out-of-scope declaration or deferral never counts. §9.2 says the scope exclusion is not a safety waiver. OR4 requires a proven portable fence or an approved, enforced, bounded operating envelope before MVP completion, READY, release, or deployment. |
| 5 | High | NFR7 declared duplicate-side-effect protection delivered before Story 4.15 closed | **Fixed in PRD.** | NFR7 class (e) is closure-pending, SM11 counts only three of five delivered classes, and OR15 blocks the claim until lifecycle sources agree and the OQ8 pre-review validator passes. The external lifecycle reconciliation itself remains open. |
| 6 | High | Story 6.1 path and authorization claims were stale | **Fixed in PRD.** | NFR8 and §11.3 recognize the canonical approved-authorized `spec-folded-snapshot.md`, its digest, the 4096-byte bound, and Story 6.2 authorization. OR5 truthfully preserves the unresolved wrapper/tracker/epic lifecycle disagreement. |
| 7 | High | Lifecycle contradictions were absent from the promised ledger | **Fixed in PRD.** | OR15 now records Stories 4.15, 5.2, 5.4, and 6.1 and requires guarded status-transition validation. Their actual external lifecycle states remain unresolved. |
| 8 | High | Final document state obscured reopened readiness | **Fixed in PRD.** | Frontmatter separates document status from implementation readiness and binds the 2026-09-09 report, baseline SHA, blocked status, and reject result. §0 denies READY, MVP-completion, release, deployment, migration, and dependent implementation handoff while blockers remain. |
| 9 | High | Omnibus FR26, FR33, FR34, and NFR17 cannot close clause by clause | **Blocking owed refinement (OR7).** | OR7 requires stable sub-clauses or a clause-to-story-to-evidence table with an all-clauses-required completion rule before readiness is re-run. No unauthorized requirement split was invented in the PRD-only pass. |
| 10 | High | High-risk closure lacks independent approval and a bound correctness gate | **Blocking owed refinements (OR10, OR13).** | OR10 owns the non-authorship control; OR13 owns the exact lane/command, trigger, evidence identity, pass condition, approval rule, and enforced status transition. High-risk NFRs cannot authorize READY, release, or migration without them. |
| 11 | High | No coherent Phase 4/MVP exit decision rule | **Blocking owed refinement (OR17).** | OR17 requires one decision table separating mandatory gates from post-MVP commitments and naming evidence, evaluator, waiver policy, and current result before readiness is re-run. The PRD does not fabricate those owner decisions. |
| 12 | High | Architecture cites an obsolete PRD baseline | **Still external/unresolved.** | OR14 requires the architecture owner to reconcile `architecture.md` to this PRD and OQ8 identity before the epic owner refreshes downstream authority. The PRD-only update intentionally made no architecture mutation. |
| 13 | High | NFR8 and NFR18 are not acceptance-complete | **Blocking owed refinements (OR6, OR16).** | The NFR8 snapshot half is corrected and bound, but its projection-cost specification and numeric bound remain open under OR16. NFR18 still lacks its required posture document and owning story under OR6. Neither can be treated as acceptance-complete. |
| 14 | Medium | MVP selection logic is inherited rather than argued | **Still unresolved.** | The PRD preserves the prior owner decision not to reduce or expand Phase 4 scope and now blocks MVP completion on append safety, but it still does not explain why each major inclusion and exclusion follows from the product thesis, risk tolerance, sequencing constraints, and decision ownership. No dedicated owner/trigger was added for this strategic narrative gap. |
| 15 | Medium | Stable requirements are mixed with volatile execution history | **Intentionally deferred under the prior anchor-preservation decision (OR9).** | OR9 retains the 2026-07-16 decision to preserve stable downstream section anchors and moves the restructure to the next major PRD revision. Volatile history remains visible rather than silently deleted. |
| 16 | Medium | Source authority is flat and incomplete | **Still unresolved, with a PRD-level partial repair.** | The missing 2026-09-08 proposal and this validation input are now listed, and §1 distinguishes provenance from approval/authority and records the supersession nuance. The source list still lacks a structured per-source approval, digest, affected-clause, and supersession register; the PRD names that future need but does not assign it as a numbered refinement. |
| 17 | Medium | Product constraints and replaceable mechanisms are not separated | **Intentionally deferred under the prior anchor-preservation decision (OR9).** | OR9 explicitly includes relocating replaceable mechanisms into a generated ledger or addendum. No addendum exists, and none was created because this pass preserves the prior decision and PRD-only scope. |
| 18 | Low | Scope and decision state arrive late | **Intentionally deferred under the prior anchor-preservation decision (OR9).** | The machine-readable readiness state and §0 prohibition are now front-loaded, but the full scope restructure remains deferred until anchor churn is acceptable. |
| 19 | Low | Epic 2 tracker rollup and Story 2.12 key are stale | **Still external/unresolved (OR19).** | OR19 assigns the correction to the tracker owner's guarded process before the next sprint-status rollup. The PRD-only update does not alter the tracker. |
| 20 | Low | OCI platform enforcement depends on an external Builds pin | **Still external/unresolved (OR18).** | §8.1 remains candid that the platform set is not locally verifiable. OR18 requires the exact shared-workflow and EventStore evidence-handler identities and byte/pin agreement before container promotion. |

## Remaining Gaps

1. **The safety and authority blockers remain real:** append-race prevention or an enforced safe envelope (OR4), reproducible OQ8 authority (OR11), and Story 4.15/lifecycle closure (OR15) are not delivered by clearer PRD language.
2. **The planning chain is not one approved baseline:** architecture and epics still require ordered reconciliation and renewed approval (OR14), plus lifecycle and tracker repairs (OR5, OR15, OR19).
3. **Closure policy is still owner-bound:** omnibus-clause evidence, independent approval, the exact correctness gate, and the unified Phase 4 exit rule remain OR7, OR10, OR13, and OR17.
4. **Two strategic nuances remain only partially carried forward:** the rationale for MVP selection is still absent, and provenance is still not a machine-usable authority register. These are the principal qualitative-intent losses from the validation input; it contained no separate tone, voice, persona, or experience intent.
5. **The deferred document-shape critique remains visible but unapplied:** volatile execution history and replaceable mechanisms stay in the PRD because OR9 preserves anchors until a major revision; no addendum exists.

## Reconciliation Verdict

All twenty validation findings are accounted for. The canonical PRD corrects false or stale claims and converts unresolved safety, authority, acceptance, and cross-artifact conditions into explicit fail-closed refinements. It does not turn PRD wording into implementation evidence: implementation readiness remains blocked and the 2026-09-09 `Poor` / `Reject` result remains authoritative until the owner-bound work closes and readiness is re-run.
