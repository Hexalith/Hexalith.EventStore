# PRD Quality Review — eventstore Phase 4 Implementation Readiness Recovery

## Overall verdict

Reviewed PRD SHA-256 `9a35ebce65d0f5f5bbcf72b07a213c6a232f1020071a6f1d0430ed01e0e78c6f` at repository `HEAD` `9cde9f2d57bc6531ddbb38df09720229b24f7efe` plus the documented dirty worktree. The PRD now resolves the supplied validation's critical/high specification gaps or retains their unresolved implementation/evidence conditions as explicit mandatory `FAIL/BLOCKED` gates; implementation readiness remains correctly `blocked` with aggregate `Reject`, and no failed gate is mistaken for delivered evidence.

No critical or high PRD-quality finding remains. The PRD passes this rubric/finalize review as a truthful final requirements baseline; this does not pass any implementation gate, approve the dirty artifact set, or change the established MVP/post-MVP boundary.

## Decision-readiness — strong

The product and authorization decisions are explicit. §0 separates final-document status from implementation readiness, records the historical and finalize-review baselines separately, enumerates prohibited authorities, and permits only bounded gate-closing corrective work. §2 states the reuse-plus-hardening thesis and its trade-off. §11.4 defines a conjunctive, non-waivable exit contract whose G-MVP-COVERAGE row is total over FR1–FR36, NFR1–NFR18, and every stable §7.1 clause; G-READINESS consumes that manifest and all mandatory rows.

The live evidence supports the PRD's result. `python3 tools/validate-oq8-platform-evidence.py --pre-review` exits 1 on `docs/guides/configuration-reference.md` drift, and the exact G-RUNTIME-PARITY command exits 1 because the packet has 0 of 3 required receipts. The canonical production profile, publication-authority validator, corrective-work validator, MVP-coverage manifest/validator, consumer-removal manifest/validator, projection-cost specification, and AOT/trimming posture document are absent. These are correctly represented as failed gates, not additional PRD-quality defects.

### Findings

- None.

## Substance over theater — strong

The Vision, journeys, FRs, NFRs, and gates are product-specific. Examples include NFR2's normalization and grammar order, the versioned MessageId grammars, NFR3's environment/algorithm matrix, NFR5's count and byte limits, NFR7's five loss classes, the publication lifecycle, and the exact evidence identities and invalidation conditions for OQ8 and runtime parity. UJ1–UJ5 are named, failure-aware flows that drive actual requirements and now carry explicit requirement maps.

The PRD does not fabricate missing specifications, receipts, ownership, source bytes, or passing validators. The failed NFR8, compatibility, authentication-host, append, OQ8, runtime, publication, consumer, high-risk-control, ownership, coverage, and baseline gates are substantive safety controls rather than NFR theater.

### Findings

- None.

## Strategic coherence — strong

The feature set follows one platform thesis: reusable domain-author seams and operational hardening must be delivered together. §9.1 makes MVP completion total over the stable MVP requirement universe, §9.2 names exclusions without converting them into waivers, and §9.3 keeps FR37/NFR19 and G5 committed but outside the Phase 4 MVP aggregate. §10 now gives every metric an explicit disposition, defines closed-denominator manifests for SM9/SM10/SM12, and retains counter-metrics against superficial closure.

The unresolved append race creates a difficult scope decision, but the PRD handles it coherently: a provider-portable fence requires approved scope change, while a mechanically enforced supported envelope is the only within-scope alternative; neither observation nor risk acceptance can satisfy NFR7 or SM11.

### Findings

- None.

## Done-ness clarity — strong

Every FR has at least one observable consequence in its requirement or feature-level evidence statement. The previously ambiguous omnibus requirements now have stable clauses in §7.1, including FR4, FR5, FR7, FR12, FR26, FR33, FR34, FR36, and NFR17, with an all-clauses-required parent rule. The remaining requirements are covered by the total G-MVP-COVERAGE contract, which requires a primary owner, terminal lifecycle state, exact evidence digest, validator result, approval, and blocking-gate linkage for every MVP ID.

The absent NFR8 numeric projection bound is not presented as done: G-NFR8 and OR16 define the exact specification contents and prohibit Story 6.4 from starting. Likewise, current tenant, status-identity, append, compatibility, all-host authentication, runtime-parity, and consumer-removal implementation failures remain explicit failed acceptance gates. Story 5.4's wrapper `in-progress`, tracker `review`, and epics `backlog` states are recorded consistently in §11.3 and OR15 as non-terminal and non-authorizing. This is correct fail-closed done-ness, not incomplete PRD wording.

### Findings

- None.

## Scope honesty — strong

The main scope boundary is explicit and unchanged: FR1–FR36 and NFR1–NFR18 are Phase 4 MVP; FR37/NFR19/Epic 8 are committed post-MVP; G5 is excluded from the MVP aggregate. §9.2 preserves the existing non-goals, and §0 requires an approved scope change before append fencing can enter MVP implementation. The Assumptions Index reports no PRD-owned assumptions, while unresolved external assumptions and decisions are named in the baseline gate.

The corrective ownership refinements now preserve that boundary. OR25 is explicitly limited to NFR5's existing SignalR detail-metadata contract and excludes DAPR, generated APIs, and other response types. OR26 is explicitly limited to NFR13's existing warnings/style/nullable/ULID/`ConfigureAwait(false)` build-quality contract and disclaims a new deterministic or byte-stable generation requirement. OR29 implements no deployment scope: it requires a later publication-authority contract and binds promotion/consumer evidence to AD-26's already-defined canonical production-profile path, while the absent profile, unratified architecture assumption, authority record, and validator keep the gate failed.

### Findings

- None.

## Downstream usability — strong

The glossary, named journeys with requirement maps, complete FR/NFR coverage tables, stable clause ledger, explicit gates, owners, evidence identities, and invalidation rules make the PRD source-extractable. FR36's §11.1 row now maps all five stable slices and keeps C3–C5 explicitly unassigned. The prior atomic-baseline and OQ8 availability findings remain G-BASELINE and G-OQ8 failures, so downstream handoff is correctly prohibited; an intentionally failed handoff gate is not itself a defect in the PRD that defines it.

The corrective-work exception is also defined by a content-bound record, allowed-path/mutation scope, expiry/revocation, preflight and postflight validation, and explicit false authority flags. OR28 correctly says the validator and approval are absent, so no corrective implementation handoff is currently authorized; once implemented, the narrow mechanism can permit gate-closing work without weakening the general handoff prohibition.

### Findings

- None.

## Shape fit — adequate

The capability-spec shape is appropriate for a brownfield developer platform, while the five concise journeys cover the cross-role API, UI, operations, recovery, release, and consumer boundaries that need journey treatment. Existing behavior, corrective requirements, and post-MVP commitments are distinguished. The chain-top role justifies strong traceability and a rigorous exit-gate model.

The remaining shape cost is the volume of volatile repository history embedded in the stable PRD. §11.3 and the retired-refinement narrative are accurate and useful for this recovery, but they make the document expensive to refresh and can obscure the durable requirement spine. OR9 already identifies the appropriate non-blocking restructuring.

### Findings

- **medium** Move replaceable readiness history out of the stable requirement spine when anchor churn is acceptable (frontmatter `source_artifacts`; §11.3; §12 retired refinements; OR9) — current snapshot hashes, receipt counts, lifecycle conflicts, and retired-item narratives are valuable audit evidence but stale independently of product intent. *Fix:* retain current gate result, authority, owner, trigger, and immutable evidence references in the PRD; move detailed snapshots and retired history into a generated digest-bound ledger or addendum, preserving the current fail-closed posture.

## Mechanical notes

- Finding counts: **0 critical, 0 high, 1 medium, 0 low**.
- FR1–FR37, NFR1–NFR19, UJ1–UJ5, and SM1–SM12 are unique and complete. Thematic FR order and historical SM order are intentionally non-numeric; downstream tooling should sort by identifier rather than document position.
- Internal section, FR/NFR, gate, and active OR references reviewed here resolve. `FR12-C3, FR12-C4` is now explicit in G-STATUS-ID.
- The Assumptions Index roundtrips semantically. The literal `[ASSUMPTION]` in §11.3 reports AD-26 in `architecture.md`; it is not a PRD-owned assumption. UJ protagonists are all named and carry role context inline.
- Lifecycle vocabulary is consistent: Story 3.15 proves only an evidence-candidate runtime's `evidence-validated` state; G-PUBLICATION-AUTH and OR29 separately govern `release-available` and profile-specific `production-promoted` authority, with the latter bound to AD-26's canonical profile bytes rather than a self-declared profile name.
- Required high-stakes brownfield chain-top sections are present: vision, users/jobs, named journeys, glossary, FRs, NFRs, constraints, MVP/non-goals/post-MVP scope, status-bearing success and counter-metrics, traceability, total exit gates, owned open items, and assumptions.
- Prior critical/high disposition: the missing atomic baseline and OQ8 bytes remain truthfully failed under G-BASELINE/G-OQ8; tenant and status contradictions have closed target contracts under NFR2/MessageId versions while G-TENANT/G-STATUS-ID remain failed; append loss remains failed under G-APPEND; total exit coverage is now G-MVP-COVERAGE; omnibus acceptance is now §7.1/G-CLAUSE; NFR8 remains G-NFR8; NFR ownership gaps are exposed by §11.2/G-NFR-OWNERSHIP; journeys are present; compatibility and all-host JWT rules are NFR12/G-COMPAT and NFR3/G-AUTH-HOSTS; runtime evidence, later publication authority, and consumer removal are separately bound by G-RUNTIME-PARITY, G-PUBLICATION-AUTH/OR29, and G-CONSUMER; stale readiness is historical and G-READINESS remains aggregate `Reject`. The later platform-review critical/high issues are also addressed by total MVP coverage, one normalized tenant policy, versioned MessageId grammars, a complete consumer universe plus exact Story 3.15 subject, a complete all-gate risk-matrix contract requiring both independent second identity and sealed CI validation for high-risk rows, and the bounded corrective-work exception. The risk matrix and controls remain unimplemented, so G-HIGH-RISK correctly fails.
