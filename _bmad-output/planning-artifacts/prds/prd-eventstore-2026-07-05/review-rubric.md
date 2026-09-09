# PRD Quality Review — eventstore Phase 4 Implementation Readiness Recovery

## Overall verdict

This is a technically substantive and unusually candid brownfield PRD: its two-part thesis is specific, its scope exclusions and residual risks are explicit, and most security, reliability, and evidence requirements carry product-specific bounds. It is not yet safe as the authoritative chain-top baseline it claims to be, however, because its live FR ownership map contradicts its own retired-correction record, several omnibus requirements cannot be closed clause by clause, and the correctness/readiness gates remain unbound or overdue.

## Decision-readiness — adequate

The PRD makes many real decisions visible instead of smoothing them away. The central bet is explicit (§2), the document distinguishes MVP from committed post-MVP work (§9), and each owed refinement has an owner role and trigger (§12). It also clearly says the 2026-08-01 `READY` decision is stale and that a re-run is owed (§1, §12 OR1), so a reader is not invited to mistake historical readiness for current authorization.

The main decision gap is the treatment of a known silent-loss class. The document repeatedly says provider-level append fencing is not delivered, records the observed result as `same-key-overwrite-raw-durable-write-lost`, and admits its deferral record has neither owner nor trigger (§7 NFR7, §9.2, §10 SM11, §12 OR4). That is honest, but it does not state who accepted this risk, why the Phase 4 MVP remains acceptable without the guard, or what concrete condition forces the missing decision.

### Findings

- **high** Known append-loss risk is disclosed but not accepted as a decision (§7 NFR7; §9.2; §10 SM11; §12 OR4) — The PRD says append races are “**NOT delivered in the Phase 4 MVP**” and the fence “currently has no owner,” while the Vision promises “operational trust.” A decision-maker can see the risk but cannot identify an accountable acceptance, rationale, blast-radius bound, or mandatory revisit point. *Fix:* Add an explicit MVP risk-acceptance decision naming the approver, evidence considered, bounded operating posture, rationale for shipping without the fence, and a mandatory trigger/owner for implementing or re-evaluating it.

## Substance over theater — strong

The document earns its detail. The Vision is specific to a DAPR-native event-sourcing platform (§2); target roles map directly to concrete jobs (§3); the NFRs define fail-closed modes, exact algorithm allowlists, metadata byte/count limits, delivery semantics, production-path evidence, and secret-store posture (§7). There are no decorative personas or novelty claims, and the absence of user journeys fits a developer-platform and operations-hardening artifact.

Even where §5 repeats later requirements, it works as a concern index rather than pretending generic concerns are requirements. The PRD itself recognizes that this duplication should be folded during a future structural revision (§12 OR9), but the repeated material remains specific rather than boilerplate.

### Findings

- None.

## Strategic coherence — adequate

The thesis is clear: platform reuse and operational hardening must ship together (§2), and SM8-SM12 plus SM-C4/SM-C5 measure both halves rather than relying only on activity (§10). The grouped features broadly serve that arc, and the PRD distinguishes historical planning-recovery metrics from live delivery and product-bet metrics.

The MVP boundary is nevertheless inherited more than argued. §1 says the correction “does not reduce MVP scope,” while §9.1 effectively includes Epics 1-7 and §9.2 excludes append fencing and global-position sharding implementation. The PRD describes those boundaries but does not give product/risk criteria that explain why those two correctness implementations are deferred while repository, packaging, UI, and planning-artifact work stays in MVP.

### Findings

- **medium** MVP selection logic is asserted rather than derived from the thesis (§1 Planning Baseline; §9.1-§9.2) — “The baseline correction does not reduce MVP scope” preserves an inherited backlog, but the PRD does not explain why known append-loss prevention and sharded allocation implementation lose priority to the many other included corrections. *Fix:* Add a short prioritization rationale tying each major inclusion/exclusion to the platform-reuse/operational-trust thesis, risk tolerance, sequencing constraints, and decision owner.

## Done-ness clarity — thin

Many atomic requirements are testable: NFR3 pins accepted algorithms and clock skew; NFR5 fixes entry and byte limits; FR12 defines `Location` behavior; FR36 has explicit gated substates. Feature-level “Done evidence” also gives useful proof categories.

The document is not consistently acceptance-ready at requirement granularity. FR26, FR33, FR34, and NFR17 each combine many independently closable clauses, and §12 OR7 explicitly admits that a story can be `done` “without closing any named clause.” NFR8 delegates its numeric cost bound to an as-yet unavailable approved spec, NFR18 requires a document that “does not yet exist” and has no owner, and OR13 says no test lane/trigger is bound as the correctness gate for high-risk NFR completion. Those gaps are material for a PRD whose purpose is implementation readiness and whose downstream stories rely on stable FR/NFR closure.

### Findings

- **high** Omnibus requirements have no clause-level completion model (§6 FR26, FR33, FR34; §7 NFR17; §12 OR7) — Each row bundles multiple independently testable behaviors, while shared “Done evidence” and story mappings do not show which clause a completed story closes. The PRD itself concedes that “a `done` story can appear in these rows without closing any named clause.” *Fix:* Sub-letter every clause with a stable identifier and at least one verifiable consequence, or add a clause-to-story-to-evidence table with explicit all-clauses-required completion rules.
- **high** High-risk NFR completion has no bound correctness gate (§12 OR13) — The PRD uses `done`, “production-path-proven,” and similar closure language throughout §7 and §11, yet OR13 says the required test lane and trigger are not defined. That leaves acceptance dependent on interpretation and allowed prior status contradictions to arise. *Fix:* Define the authoritative lane, trigger, environment/substitution policy, evidence artifact, and pass condition required before any story may claim high-risk NFR completion.
- **high** Two NFRs are intentionally not acceptance-complete (§7 NFR8, NFR18; §11.3; §12 OR5-OR6) — NFR8 says its numeric bound comes from an approved spec that is not yet available under the bound path, while NFR18 requires a nonexistent document and has no owning story. These are gates or debts, not implementable done conditions in the current baseline. *Fix:* Resolve and approve the NFR8 spec identity/bounds, create and assign the NFR18 posture artifact, then restate each NFR so its completion can be verified without an unresolved placeholder.

## Scope honesty — strong

Scope is one of the PRD’s strongest areas. §9 separately identifies in-scope, out-of-scope, and committed post-MVP work; §6.8 exposes FR36’s two closed substates and one open substate; §7 NFR7 distinguishes guarded, recovered, and deliberately unguarded loss classes; and §12 records owed refinements with owners and triggers instead of hiding them. The document also states that no inline `[ASSUMPTION]` tags exist (§13) and ties that claim to approved source proposals rather than silently treating model inferences as facts.

The callout syntax differs from the rubric’s preferred `[NOTE FOR PM]`/`[NON-GOAL]` markers, but the named OR table and explicit §9 boundaries are at least as visible and more operationally useful here. Open-item density is high for a final artifact, yet the PRD flags the sole readiness blocker rather than disguising the artifact as build-authorizing.

### Findings

- None.

## Downstream usability — broken

The document has a strong glossary (§4), stable FR/NFR/SM identifiers, named artifact boundaries (§0-§1), and extensive FR/NFR-to-story tables (§11). Those structures should make it highly extractable for architecture, UX, and story workflows.

The central ownership table is internally self-invalidating, however. §10 SM2 says eight FRs still have multiple primary owners; §11.1 marks those rows `**multiple primary owners** (OR12)` and says FR1 belongs to Story 4.11 with an Epic 1/Epic 4 mismatch; but §12 says OR12 is retired, the eight duplicates “are now resolved,” and the correct FR1 owner is Story 1.11. A downstream extractor cannot know whether the live table or the retirement narrative is authoritative. The repository also now contains `_bmad-output/implementation-artifacts/spec-folded-snapshot.md`, while §11.3 and OR5 still describe that path as absent/mismatched, showing that the PRD’s operational ledger has already drifted from current repository state.

### Findings

- **critical** The authoritative FR ownership map contradicts its own correction record (§10 SM2; §11.1 FR1, FR11-FR13, FR15, FR19, FR21-FR22, FR25 and closing paragraph; §12 retired OR12) — The live map says “**multiple primary owners** (OR12)” and assigns FR1 to 4.11, while the retired record says duplicates were resolved and “primary FR1 ownership” is Story 1.11. This defeats the PRD’s stated purpose of PRD-to-epic traceability and can send story, readiness, and verification work to the wrong owners. *Fix:* Rebuild §10 SM2 and §11.1 from the authoritative `epics.md` ownership declarations, replace FR1’s story with 1.11, encode the resolved disjoint slices/supporting owners, and validate that no retired OR remains referenced as live debt.
- **medium** The Story 6.1 path warning is stale against current repository state (§11.3 Story 6.1 bullet; §12 OR5) — The PRD says the bound `spec-folded-snapshot.md` path is missing and the gate therefore reads unmet, but that file exists in the current repository alongside `spec-6-1-folded-snapshot-frozen-spec.md`. The reader cannot tell whether OR5 is closed, whether both files are authoritative, or whether the newly present file is merely a shim. *Fix:* Reconcile the two artifacts, record the single authoritative path and approval state in the PRD and `epics.md`, and retire or restate OR5 based on verified content rather than filename existence alone.

## Shape fit — thin

A capability-spec shape is right for this brownfield developer platform, and the decision not to invent consumer-style journeys or standalone personas is appropriate (§3.3). The feature grouping, cross-cutting NFR section, scope, metrics, and glossary are also suitable building blocks.

The artifact has nevertheless become three documents at once: product requirements, a historical readiness/status ledger, and a cross-artifact reconciliation report. The source list occupies lines 7-66, §1.1 embeds external-design provenance and supersession mechanics, §11.3 carries detailed story status/migration history, and §12 preserves long retired-resolution narratives. This buries the stable requirements and makes status drift more likely; OR9 already identifies the need to “front-load scope and relocate readiness mechanics.” In addition, the PRD declares that architecture owns component, integration, topology, and decision-record gates (§1), yet it embeds extensive implementation mechanism and file/workflow details in FRs, NFRs, and guardrails, with no addendum to absorb that material.

### Findings

- **medium** Readiness history and live status overwhelm the stable PRD shape (§1.1; §11.3; §12, especially retired OR2/OR3/OR12) — Historical commit, evidence-repair, tracker-dispute, story-migration, and retrospective details make a point-in-time audit possible but obscure the durable product contract and have already produced stale live sections. *Fix:* Keep the PRD’s current decisions, requirements, scope, metrics, and unresolved gates; move dated reconciliation/history into an audit addendum or changelog and leave concise links from the PRD.
- **medium** The PRD crosses its declared architecture/implementation boundary (§1 Planning Baseline; §6-§8) — It says `architecture.md` owns “component, integration, topology, and decision-record gates,” but requirements prescribe named methods, DAPR component/policy identifiers, source paths, workflow channels, option constants, and script/file evidence mechanics. Some are legitimate public-contract constraints, but the document does not separate those from technical how. *Fix:* Retain product-visible contracts and non-negotiable bounds in the PRD; move mechanism, topology, exact file/script, and design-supersession detail to `architecture.md` or an addendum, with stable requirement links back.

## Mechanical notes

- FR1-FR37 are each present once in the requirement set and once in §11.1; numbering is complete and unique, although feature presentation is intentionally nonnumeric (for example FR25 precedes FR23 and FR26).
- NFR1-NFR19 are complete and unique in §7. §11.2 intentionally maps only the declared high-risk/selected subset, and states that policy.
- SM1-SM12 and SM-C1-SM-C5 are unique. SM1 is explicitly historical and non-discriminating; the product-bet metrics and counter-metrics carry the live strategic load.
- No UJs are present. That is appropriate for the stated brownfield developer-platform/capability-spec shape; the target roles and jobs do not float as unnamed consumer journeys.
- The Assumptions Index roundtrip is clean: no inline `[ASSUMPTION]` marker was found, and §13 says none exist.
- Cross-reference integrity is not clean: live references to retired OR12 and the FR1 owner contradiction are substantive blockers, not cosmetic drift.
- No `addendum.md` is present for this PRD workspace or planning-artifact root, despite substantial architecture, mechanism, and audit-history material that would fit that role.
