# PRD Quality Review — eventstore Phase 4 Implementation Readiness Recovery

## Overall verdict

This is a technically substantive and unusually candid requirements catalogue that supports one immediate decision well: keep implementation readiness blocked. It is not yet safe as the authoritative chain-top baseline it claims to be because downstream usability is broken by an unreconciled artifact set and unavailable normative OQ8 source, while acceptance clarity and shape remain thin; the explicit `Reject` posture should stay in force until those gaps close.

## Decision-readiness — adequate

The PRD makes the current decision unmistakable: document finality is separated from readiness, the bound result is `Reject`, and §0 forbids `READY`, completion, release, deployment, migration, and downstream handoff. The trade-offs are also unusually candid: §2 says reuse without hardening is unsafe and hardening without reusable seams perpetuates duplication, while §9.2 refuses to let an out-of-scope append fence count as a safety waiver.

The blocker register in §12 is owned and trigger-bound, so this is decision-ready as a stop/go artifact today. It is less ready for the eventual go decision because the mandatory gates, evidence, evaluators, and waiver rules are not yet assembled into one exit contract.

### Findings

- **high** The future `READY` decision has no single executable exit contract (§12 OR17) — OR17 correctly admits that the PRD lacks one Phase 4 MVP exit-decision table identifying mandatory gates, evidence, evaluator, waiver policy, and current result. Until it exists, a reviewer must reconstruct the authorization rule across §0, §1.1, §9, §10, §11.3, and eleven blocking refinements. *Fix:* Add the OR17 exit table and make every readiness-changing claim resolve to one row with a pass/fail result and durable approval evidence.

## Substance over theater — strong

The content is earned. The vision is specific to a DAPR-native event-sourcing platform, the role list directly drives SDK, generated API, UI, release, operator, and evidence requirements, and the non-functional requirements use product-specific bounds such as NFR5's `16` entries / `2048` UTF-8 bytes and NFR3's exact algorithms and 60-second clock skew. The document avoids persona and innovation theater, names known failures and residual evasions, and retains negative evidence instead of presenting every capability as successful.

## Strategic coherence — adequate

The PRD has a clear thesis in §2: platform reuse and operational hardening must ship together. SM8–SM12 measure both halves of that bet, and SM-C1–SM-C5 prevent attractive but invalid shortcuts. MVP, post-MVP, and explicit exclusions are also separated cleanly in §9.

However, the MVP is still defined primarily as the broad set of "Epics 1-7 as listed in `epics.md`" rather than as a thesis-derived priority order. That makes the strategic arc understandable but leaves the rationale for why every repository, generator, topology, admin, cost, and planning-only item belongs in the same minimum slice mostly implicit.

### Findings

- **medium** MVP priority follows the epic inventory more than the product thesis (§9.1, first bullet) — The scope declares all Epics 1–7 in scope and delegates sequence to `epics.md`, but it does not state which requirements are indispensable to validating the combined reuse-and-hardening bet versus enabling or follow-on work. *Fix:* Add a short scope-logic paragraph that groups mandatory MVP outcomes by the thesis they validate and distinguishes exit-critical work from supporting readiness work without duplicating story sequencing.

## Done-ness clarity — thin

Many individual clauses are concrete, and the PRD deserves credit for defining evidence denominators, failure semantics, production-path meaning, and precise security limits. Yet an engineer cannot determine completion from the PRD alone for every requirement: acceptance criteria are delegated to `epics.md`, feature-level "Done evidence" paragraphs cover several FRs at once, and multiple FR/NFR rows bundle independently fail-able consequences.

The PRD already recognizes the worst cases in OR7 and the missing NFR8 projection-cost bound in OR16. Those are readiness blockers, not editorial polish.

### Findings

- **high** Omnibus requirements have no clause-level closure rule (§6 FR26, FR33, FR34; §7 NFR17; §12 OR7) — Each row combines many independently testable outcomes, while §11 maps the whole ID to multiple stories. A story can therefore claim the ID without demonstrating which clause it closes, exactly as OR7 acknowledges. *Fix:* Sub-letter every independent clause with stable IDs, or add a clause-to-story-to-evidence table with an explicit all-clauses-required completion rule.
- **high** Acceptance ambiguity extends beyond the four requirements named by OR7 (§6.1, §6.2) — FR4, FR5, FR7, and FR12 each contain several distinct outcomes, but their section-level "Done evidence" paragraphs do not round-trip every clause; for example, §6.1's evidence does not explicitly accept FR4's six-state lifecycle/provenance behavior or FR5's partial-failure, concurrency, flush, and DAPR semantics. *Fix:* Give each FR a compact verifiable consequence list, or broaden the clause-to-evidence mechanism so every multi-clause FR has explicit closure coverage.
- **high** NFR8's projection-cost outcome has no measurable bound (§7 NFR8; §12 OR16) — The requirement says cost must be bounded but explicitly records the projection specification as missing, leaving Story 6.4 unauthorized and the projection half of NFR8 untestable. *Fix:* Approve the named specification with a numeric cost bound, measurement workload, pass condition, and evidence path, then reference those acceptance facts from NFR8.

## Scope honesty — strong

Scope is unusually honest. §9 separates MVP, out-of-scope, and committed post-MVP work; the append-fencing exclusion expressly remains a blocker rather than a waiver; FR24 and FR31 are described as planning/evidence outcomes rather than implementations; and FR37/NFR19 cannot silently enlarge MVP. The eleven blocking refinements have owners and revisit triggers, while the three non-blocking items are visibly separated.

There are no inline `[ASSUMPTION]` or `[NOTE FOR PM]` callouts, but the empty Assumptions Index round-trips correctly and the OR register provides a stronger explicit mechanism for the unresolved decisions in this document. The high open-item density is appropriate because the frontmatter and §0 say `blocked` / `reject`; it would be unacceptable only if this artifact were presented as a build authorization.

## Downstream usability — broken

The PRD has strong local extraction aids: a substantial glossary, unique requirement IDs, feature grouping, FR-to-story mappings, and a high-risk NFR coverage table. Nevertheless, the document itself says the PRD, architecture, UX, and epics "do not currently form one approved baseline" (§11.3), and OR14/OR15 block handoff on source and lifecycle contradictions. A chain-top artifact whose current requirements cannot be trusted to match its current stories is not downstream-usable, even when it diagnoses the problem accurately.

Safety-sensitive OQ8 clauses are additionally governed by external bytes that EventStore cannot reproduce. That prevents architecture, story, and test workflows from source-extracting the normative state machine, timers, tombstone fields, and evidence denominators inside the declared evidence boundary.

### Findings

- **critical** The PRD, architecture, epics, and lifecycle sources are not one approved baseline (§11.3; §12 OR14, OR15) — The PRD delegates implementation slicing and acceptance to `epics.md` while explicitly recording stale input digests and contradictory story states. Its traceability tables therefore describe intended ownership but cannot authorize downstream implementation or completion claims. *Fix:* Reconcile architecture to this PRD, reconcile epics to both, resolve the named lifecycle contradictions, run the guarded drift/status validations, and renew approval before handoff.
- **critical** Normative OQ8 requirements are unavailable inside the EventStore evidence boundary (§1.1; §12 OR11) — The external design overrides FR27, NFR7, and NFR16 on state-machine behavior, timers, tombstones, public errors, and evidence denominators, while §1.1 admits the bytes cannot be reproduced in this repository. A digest alone does not let downstream workflows extract or validate the governing content. *Fix:* Retain a permitted immutable copy, complete approved normative projection, or signed/content-addressed attestation; propagate the bound repository/path/commit/digest and make absence or mismatch fail closed.
- **high** NFR traceability is incomplete despite the PRD's ownership claim (§0; §11.2) — The PRD says it owns FR/NFR traceability, but §11.2 has no rows for NFR5, NFR12, or NFR13 even though `epics.md` declares coverage for them. The section explains its high-risk focus, but omission still prevents complete PRD-to-story extraction for three active requirements. *Fix:* Add rows for every NFR, distinguishing primary from supporting ownership and retaining the existing warning that declaration does not prove delivery.

## Shape fit — thin

The capability-spec shape fits much of this brownfield developer platform: technical users, operational constraints, public contracts, security, and evidence rules are more useful here than consumer-style personas. However, the PRD also covers six stakeholder roles and meaningful Admin, Sample, and Tenants UI plus release and incident/recovery workflows, but includes no user journeys with named protagonists. The Jobs list is too compressed to expose end-to-end handoffs, failure recovery, and what each role sees when state changes.

The document also mixes a stable product baseline with volatile readiness history, receipt counts, story lifecycle state, and source identities. That is understandable for a recovery effort, but it makes the 583-line PRD function partly as an evidence ledger and creates recurring anchor-preserving updates that §12 OR9 already identifies as structural debt.

### Findings

- **high** Multi-stakeholder and UI-affecting workflows have no user journeys (§3; §8.3) — Six roles and three UI surfaces are named, yet there is no narrated flow for domain adoption, generated API exposure, projection-confirmed UI success, operator recovery, or release authorization. Downstream UX and story work must reconstruct ordering and cross-role boundaries from scattered FRs. *Fix:* Add a small set of named-protagonist journeys for the load-bearing cross-role flows, including failure and recovery states; keep purely internal implementation mechanics in the capability sections.
- **medium** Volatile readiness evidence is embedded in the stable PRD narrative (§1.1, §6.8, §10, §11.3, §12 OR9) — Receipt counts, story statuses, historical verdicts, external digests, and retired refinements compete with the product thesis and are likely to stale after each review cycle. *Fix:* Keep stable authority and fail-closed rules in the PRD, but move replaceable run history and evidence state into a generated ledger or addendum linked by immutable identity.

## Mechanical notes

- Requirement declarations are complete and unique: FR1–FR37 and NFR1–NFR19 each appear exactly once in the requirements sections. FR declarations are thematically grouped rather than numerically ordered (`FR25` precedes `FR23`, and `FR26` follows `FR31`); this is readable but worth preserving deliberately in tooling.
- §11.1 contains one coverage row for every FR1–FR37. §11.2 omits NFR5, NFR12, and NFR13; this is reported above because it materially affects downstream extraction.
- No duplicate IDs were found. No obviously broken internal section references were found in the PRD, and every path listed in frontmatter `source_artifacts` exists at validation time.
- Glossary terminology is generally disciplined; especially useful distinctions include source/package versus deployed-runtime parity and projection-backed versus handler-computed provenance.
- The Assumptions Index round-trips cleanly: no inline `[ASSUMPTION]` tags exist and the index says none exist.
- No UJ IDs or named protagonists are present; this is a shape finding rather than an ID-continuity defect.
