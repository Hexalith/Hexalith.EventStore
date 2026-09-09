# Validation Report — eventstore Phase 4 Implementation Readiness Recovery

- **PRD:** _bmad-output/planning-artifacts/prd.md
- **Rubric:** .agents/skills/bmad-prd/assets/prd-validation-checklist.md
- **Repository baseline:** 1b6f08d41de040615d3b08675d98e46cfa5bab0c
- **Run at:** 2026-09-09T08:18:44+02:00
- **Grade:** Poor

## Overall verdict

This is a technically substantive and unusually candid brownfield PRD, with specific requirements, explicit scope exclusions, stable IDs, and strong security and evidence bounds. It is nevertheless unsafe as the authoritative chain-top baseline it claims to be: the live FR ownership map contradicts its own retired-correction record, the epic plan fails its own source-drift gate, safety-sensitive OQ8 clauses depend on normative bytes this repository cannot reproduce, and the MVP success model can pass while a known concurrent append race still loses durable data.

The adversarial and brownfield reviews strengthen the rubric's reject verdict. Repository truth has also moved beyond the PRD around Story 6.1, Story 4.15, architecture authority, and several story lifecycle states. Do not use this PRD to authorize a READY verdict, MVP completion, consumer migration, or release until the critical findings are resolved and the dependent planning artifacts are reconciled against one baseline.

## Dimension verdicts

- Decision-readiness — adequate
- Substance over theater — strong
- Strategic coherence — adequate
- Done-ness clarity — thin
- Scope honesty — strong
- Downstream usability — broken
- Shape fit — thin

## Findings by severity

### Critical (4)

**[Downstream usability / Brownfield] — The authoritative FR ownership map contradicts its own correction record (§10 SM2; §11.1; §12 retired OR12)**

The live map assigns FR1 to Story 4.11 and still labels eight requirements as having multiple primary owners under OR12. The same PRD says OR12 is retired, FR1 belongs to Story 1.11, and the duplicate ownership was resolved. Current epics.md confirms the retirement narrative, not the live table.

Fix: Rebuild SM2 and §11.1 from current epics.md declarations, encode sole owners and approved disjoint slices, remove every live OR12 warning, and add a drift check.

**[Brownfield] — The epic plan fails its own source-drift gate (epics.md source register and drift rule)**

The digests recorded by epics.md do not match the current PRD or architecture. Recorded/current PRD digests are 8f9c88e8… / 39e22f7c…; architecture digests are 623bc23e… / 2b96a810…. Since the PRD delegates slicing and sequencing authority to epics.md, the repository cannot prove that current stories implement the current requirements and architecture.

Fix: Reconcile the current PRD and architecture deltas into epics.md, update digests only after review, rerun the source-drift/final-validation gate, and preserve renewed approval evidence.

**[Adversarial / Brownfield] — Normative OQ8 requirements are unavailable inside EventStore (§1.1; FR27; NFR7; NFR16)**

The PRD makes an external Hexalith.Folders document more specific and therefore normative for state-machine behavior, timers, tombstones, failure semantics, and evidence denominators, while admitting that EventStore cannot reproduce the governing bytes. A locally stored digest verifies identity only if those bytes are available.

Fix: Track an immutable permitted copy, a complete normative projection, or a signed/content-addressed attestation that binds repository, path, commit, and digest. Make absence or mismatch a readiness failure and propagate the full identity to architecture, epics, evidence, and validation.

**[Decision-readiness / Adversarial] — MVP success can coexist with known concurrent append data loss (§7 NFR7; §9.2; §10 SM11/SM-C5; §12 OR4)**

Story 4.5 observed same-key-overwrite-raw-durable-write-lost, no provider-level append fence exists, and DW-326 has no owner or trigger. Yet SM11 counts either a guard or an out-of-MVP declaration toward five-of-five success, allowing paperwork to satisfy a loss-prevention metric.

Fix: Deliver and prove append fencing, or enforce and document an MVP operating envelope in which the race cannot occur. Keep SM11 failed until loss is prevented in the supported envelope; record explicit risk acceptance, approver, bounds, and revisit trigger.

### High (9)

**[Brownfield] — NFR7 declares duplicate-side-effect protection delivered before Story 4.15 closes (§1.1; NFR7; Story 4.15)**

The tracker says review, the story wrapper says done, epics.md records the conflict, and python3 tools/validate-oq8-platform-evidence.py --pre-review fails on lifecycle drift.

Fix: Mark class (e) closure-pending or reconcile the lifecycle and retain the claim only with a passing validator result.

**[Downstream usability / Brownfield] — Story 6.1 path and authorization claims are stale (§11.3; OR5)**

The canonical spec-folded-snapshot.md now exists, is approved-authorized, and authorizes Story 6.2; the wrapper says done while the tracker says review and epics.md still says backlog/absent. The PRD describes the opposite repository state.

Fix: Establish authority order, reconcile the canonical spec, wrapper, tracker, epics, and PRD atomically, and retire or rewrite OR5.

**[Brownfield] — New lifecycle contradictions are absent from the promised ledger (§11.1 explanation; §12)**

Stories 5.2, 5.4, 6.1, and 4.15 disagree across sprint-status.yaml, story wrappers, or epics.md, while the PRD says contradictions are recorded in §12.

Fix: Resolve each against its completion gate and add a mechanical guard preventing divergent status transitions.

**[Adversarial] — Final document state obscures reopened readiness (frontmatter; §1; OR1)**

The document is status final even though its readiness gate is explicitly reopened and OR1 remains blocking. Machine consumers have no separate parsable readiness state.

Fix: Separate document_status from implementation_readiness_status and bind the latter to report path, date, baseline SHA, and result.

**[Done-ness clarity] — Omnibus requirements cannot be closed clause by clause (FR26, FR33, FR34, NFR17; OR7)**

Each combines independently fail-able behaviors while story mappings do not identify which clause is delivered. The PRD concedes that a done story may close no named clause.

Fix: Give clauses stable sub-IDs with consequences, owners, evidence, and all-clauses-required closure, or generate an equivalent clause-to-story-to-evidence matrix.

**[Done-ness clarity / Adversarial] — High-risk closure lacks an independent approval rule and bound correctness gate (OR10; OR13)**

Most high-risk evidence can be author-approved, and no exact test lane or trigger controls recording a story done against a high-risk NFR.

Fix: Define non-authorship requirements, exact CI lane/command, trigger, evidence identity, pass condition, and status-transition enforcement.

**[Strategic coherence / Adversarial] — No coherent Phase 4/MVP exit decision rule (§9-§12)**

Metrics mix historical artifact milestones, runtime outcomes, and post-MVP gates without one table defining mandatory gates, evidence, evaluator, waiver policy, and current result.

Fix: Add a release/phase exit table and separate non-gating post-MVP commitments.

**[Brownfield] — Architecture cites an obsolete PRD baseline (architecture.md frontmatter and authority section)**

Architecture is final at 2026-08-29 and still names the PRD updated 2026-08-16, predating the current 2026-09-08 NFR and OQ8 changes. Its stored digest also fails the epic source-drift check.

Fix: Reconcile architecture with the current PRD, update provenance and OQ8 identity, then refresh the epic input digest through the governed gate.

**[Done-ness clarity] — NFR8 and NFR18 are not acceptance-complete (§7; §11.3; OR5-OR6)**

NFR8's bound depends on an unresolved spec identity/status, while NFR18 requires a nonexistent document and has no owning story.

Fix: Bind and approve the NFR8 spec and numeric bounds; assign and produce the NFR18 posture document before either can be treated as complete.

### Medium (4)

**[Strategic coherence] — MVP selection logic is inherited rather than argued (§1; §9)**

The PRD preserves Epics 1-7 but does not explain why append fencing and sharding implementation are deferred while lower-risk planning, packaging, and UI work remains MVP.

Fix: Tie the major inclusions and exclusions to the two-part thesis, risk tolerance, sequencing constraints, and decision owner.

**[Shape fit / Adversarial] — Stable requirements are mixed with volatile execution history (§1.1; §6.8; §10-§12)**

Story states, receipt counts, tracker disputes, migration history, and retrospective facts make the PRD stale quickly and obscure its durable product contract.

Fix: Move live status and historical reconciliation to a generated ledger or audit addendum, leaving baseline-specific links and durable decisions in the PRD.

**[Adversarial / Brownfield] — Source authority is flat and incomplete (frontmatter source_artifacts)**

The source list lacks per-source approval, digest, affected clauses, and supersession, and it omits the proposal cited as authority for retiring OR2/OR3/OR12.

Fix: Add the missing proposal and replace the flat list with a compact authority register or a generated validation against one.

**[Shape fit] — Product constraints and replaceable implementation mechanisms are not separated (§6-§8)**

The PRD delegates technical design to architecture but embeds many method names, file paths, DAPR identifiers, workflow mechanics, and evidence scripts without an addendum.

Fix: Retain public contracts and non-negotiable bounds; move replaceable mechanisms and audit rationale to architecture or an addendum.

### Low (3)

**[Adversarial] — Scope and decision state arrive late (§9; OR9)**

Fix: Put supported operating envelope, in/out/post-MVP scope, unsafe gaps, and current readiness state immediately after purpose.

**[Brownfield] — Epic 2 tracker rollup is stale**

All Epic 2 stories and its retrospective are done, but the epic remains in-progress; the Story 2.12 key is truncated.

Fix: Correct through the tracker owner's guarded process.

**[Brownfield] — OCI platform enforcement depends on an external Builds pin (§8.1)**

Fix: Record the exact shared-workflow identity and EventStore evidence-handler identity, and verify that caller pin and validated workflow bytes match.

## Mechanical notes

- FR1-FR37 and NFR1-NFR19 are present and unique in their primary requirement sets.
- SM1-SM12 and SM-C1-SM-C5 are unique; live product-bet metrics carry more weight than the historical metrics.
- No user journeys are present; that is appropriate for this brownfield developer-platform capability specification.
- The assumptions roundtrip is clean: no inline assumption marker exists, and §13 says none exist.
- Cross-reference integrity is not clean because retired OR12 remains live in SM2 and §11.1.
- No addendum.md is present despite substantial audit-history and technical-mechanism content.
- Current validation of OQ8 evidence fails: python3 tools/validate-oq8-platform-evidence.py --pre-review exits 1 on Story 4.15 lifecycle drift.

## Reviewer files

- review-rubric.md
- review-adversarial-general.md
- review-brownfield-traceability.md

Historical reviewer files remain preserved in the workspace and were treated as prior-run context, not as current-baseline findings.
