# PRD Quality Review — eventstore Phase 4 Implementation Readiness Recovery

## Overall verdict

The PRD is a credible, source-grounded brownfield platform baseline: its product bet, MVP/post-MVP boundary, requirement inventory, and July proposal provenance are clear enough to support planning decisions. It is not yet self-sufficient as an engineering definition of done, because several omnibus FRs and qualitative NFR bounds still push material acceptance decisions into `epics.md` or future specifications; the document is therefore adequate for readiness recovery but thin at the acceptance boundary.

## Decision-readiness — adequate

The central decision is explicit in §2: platform reuse and operational hardening must ship together, and §1 clearly assigns requirement intent to the PRD while architecture, UX, and implementation slicing remain separate artifacts. Sections 9.2–9.3 also make the consequential MVP decision visible: FR37/NFR19 are committed post-MVP work and do not expand Phase 4.

The PRD is honest that some design decisions remain gated, but two correctness choices in FR24 and FR31 are expressed as work to renegotiate or investigate rather than as resolved product contracts. That is acceptable for a gated readiness document only if the gate is as explicit and source-extractable as the Story 8.1 gate described elsewhere.

### Findings

- **medium** Correctness design gates are not fully bound (§6.4 FR24/FR31, §11.3, §12) — FR24 leaves the allocation scope at “per tenant or domain,” and FR31 defers the fencing decision until a spike, yet §11.3 does not name the decision owner, required decision artifact, or acceptance condition for either gate. Section 12 says no PRD-level questions are open, which is defensible on scope but makes these unresolved contract decisions easy to miss. *Fix:* add explicit FR24/FR31 decision-gate entries to §11.3 with owner, output path/ADR, evidence required, and the stories blocked by each decision.

## Substance over theater — strong

The content is earned. The user/job framing in §3 directly drives the feature groups, the glossary supports the platform boundary, and the NFRs name EventStore-specific failure and evidence paths rather than generic “secure/scalable/reliable” aspirations. There are no decorative personas or novelty claims.

The July additions reinforce this strength. FR12 states an absolute, gateway-authoritative, fail-closed `Location` contract; FR27 separates the gateway-owned status key from any `CorrelationId == MessageId` coincidence; and NFR2 adds the concrete reserved-tenant rejection rule. All 33 July proposal files are now listed in frontmatter, with no missing or extra July proposal source.

### Findings

No findings.

## Strategic coherence — adequate

The feature groups form a coherent arc from domain-author self-service through external APIs, release reliability, correctness, security, bounded cost, operator trust, consumer parity, and optional payload protection. The explicit central bet in §2 prevents the PRD from reading as an arbitrary backlog, and the counter-metrics guard against three plausible readiness shortcuts.

The metrics are less coherent with that thesis than the requirements are. SM1–SM5 primarily prove that planning artifacts, mappings, and story shapes exist; SM6–SM7 prove two downstream gates. None directly measures whether domains actually shed boilerplate or whether the hardened platform produces the intended operational outcome.

### Findings

- **medium** Primary metrics validate planning completion more than the product bet (§2, §10) — a readiness rerun and complete traceability are necessary for this recovery effort, but they do not establish that reusable seams work for domain authors or that security, recovery, and release behavior improved in production paths. *Fix:* retain the readiness metrics and add at least one adoption/reuse outcome and one operational-proof outcome, each with an explicit target and evidence source.

## Done-ness clarity — thin

Many requirements have concrete observable consequences, and the July edits are notably stronger than the surrounding baseline. FR12 specifies absolute versus omitted `Location` behavior, FR27 names the three resume-match fields and forbids reliance on correlation/message equality, and NFR2 gives tenant provisioning an exact rejection case. Feature-level “Done evidence” and the high-risk NFR story table also provide useful validation anchors.

However, this chain-top PRD delegates too much completion detail to `epics.md`. A downstream engineer can understand the intended capability but cannot consistently determine whether every clause of every FR or qualitative NFR is complete from the PRD alone.

### Findings

- **high** Omnibus FRs lack clause-level completion evidence (§6.5 FR26, §6.6 FR33, §6.7 FR34) — these requirements bundle respectively nine, six, and roughly ten independently failing behaviors, while each feature has only one aggregate “Done evidence” paragraph. Partial delivery could therefore be reported as FR-complete without a stable per-clause consequence. *Fix:* split each omnibus FR into nested, stably identified clauses or add a clause-level acceptance list that maps every behavior to its evidence and story.
- **high** Several NFR bounds remain adjectives rather than testable contracts (§7 NFR5, NFR7, NFR8, NFR17) — “bounded metadata,” “explicitly guarded or recovered,” “bounded cost model,” and “resiliency targets” do not state limits, failure budgets, measurement points, or the specification that owns them. NFR2’s `system`-tenant rejection is testable, but its broader cross-surface isolation clause likewise depends on downstream test interpretation. *Fix:* give each qualitative bound a numeric/finite rule or a named spec/ADR owner and require a production-path test matrix for cross-surface tenant isolation.

## Scope honesty — strong

Sections 9.1–9.3 clearly distinguish Phase 4 MVP, explicit non-goals, and committed post-MVP payload protection. The document also states what it does not own, names the readiness conditions that remain, and distinguishes generic projection erasure from full GDPR aggregate/event erasure. There are no hidden assumption tags or rhetorical open questions; remaining payload-protection design is openly assigned to Story 8.1.

### Findings

No findings.

## Downstream usability — adequate

The PRD is structured for source extraction: FR1–FR37 and NFR1–NFR19 are unique and gap-free, feature groups are cohesive, traceability is explicit, and no user journeys are needed for this capability-oriented brownfield platform. The 33-entry July source inventory also gives downstream reviewers complete proposal provenance.

The main weakness is vocabulary at the newest and most cross-repository boundaries. Several load-bearing references require readers to consult architecture, epics, or Parties context before they can interpret an extracted requirement correctly.

### Findings

- **medium** Load-bearing terms and cross-references are undefined locally (§4, §6.4 FR27, §6.8–6.9, §7 NFR1, §11.3) — “gateway-owned status key,” “G5,” `pdenc-v2`, `AD-16`, and “Parties Story 8.6/8.7” are not defined or fully qualified in the glossary. An extracted FR27, FR37, or NFR1 can therefore lose its contract context. *Fix:* add glossary entries for the status key, G5, and payload formats, and qualify external ADR/story references with owning artifact paths or stable links.

## Shape fit — strong

The capability-spec shape matches the declared brownfield developer-platform and operations-hardening product in §3.3. User journeys would add little for library, runtime, CI/CD, and governance seams; the PRD appropriately keeps UI governance at requirement level and routes detailed interaction work to `ux.md`. Its higher rigor on traceability and production-path evidence is appropriate because it feeds architecture, UX, epics, and implementation readiness.

### Findings

No findings.

## Mechanical notes

- Source provenance is complete for the requested July set: 33 matching proposal files exist and all 33 appear exactly once in `source_artifacts`; there are no missing or extra July proposal entries.
- FR IDs are unique and continuous from FR1 through FR37. Their presentation is intentionally grouped by concern rather than numeric order: FR25 appears in §6.3, FR26/FR28/FR32 in §6.5, and FR27/FR29–FR31 in §6.4.
- NFR IDs are unique and continuous from NFR1 through NFR19. The high-risk traceability table intentionally covers a subset, not every NFR.
- The Assumptions Index roundtrips: there are no actual inline `[ASSUMPTION: …]` tags; the only literal `[ASSUMPTION]` occurrence is the explanatory sentence in §13.
- No UJs are present, which fits this capability-oriented platform PRD; protagonist naming is therefore not applicable.
- No broken internal section references were found. External identifiers such as AD-16 and Parties story numbers resolve only through sibling/external artifacts and are covered by the downstream-usability finding above.
