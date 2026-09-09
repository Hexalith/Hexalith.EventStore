# PRD Quality Review — eventstore Phase 4 Implementation Readiness Recovery PRD

## Overall verdict

The PRD is a truthful, fail-closed requirements baseline: it states a coherent platform-reuse and operational-hardening thesis, distinguishes document finality from implementation readiness, and expressly authorizes no readiness, release, deployment, migration, or dependent implementation handoff while its blocking prerequisites remain open. It is therefore safe to retain as a blocked draft, but it is not yet ready to drive implementation: omnibus requirements still lack clause-level completion semantics, and several lesser traceability and gate-definition ambiguities must be resolved before a future `READY` decision.

## Decision-readiness — adequate

The governing decision is unusually clear in §0, §1, §11.3, and §12: the 2026-09-09 `Poor` / `Reject` result supersedes the historical `READY` result, and the current document grants no implementation or release authority. The open-refinement register names owners, triggers, and blocking status, while §9.2 refuses to misrepresent an observed append-loss race as delivered safety.

That is sufficient for the decision available now—remain blocked and perform the owned remediation—but not yet for a later exit decision. The PRD itself acknowledges in OR17 that mandatory gates, evidence, evaluators, waiver rules, and current results are distributed rather than bound in one decision table.

### Findings

- **[medium]** Future exit decision is not yet mechanically decidable (§12, OR17) — The present stop decision is explicit and safe, but a future reviewer must assemble gate policy from §0, §7, §9.2, §10, §11.3, and §12. This does not weaken the current block; it does leave room for inconsistent interpretation when someone later attempts to return readiness to `READY`. *Fix:* Add the OR17 Phase 4/MVP exit decision table with one row per mandatory gate, its evidence identity, evaluator, waiver policy, and current result; make the overall transition fail closed if any mandatory row is absent, failing, stale, or unapproved.

## Substance over theater — adequate

The content is largely earned. The target roles drive identifiable jobs, the Vision names a real product bet, the NFRs use product-specific constraints, and the extensive security, tenant, DAPR, delivery, and evidence language reflects actual brownfield risks rather than generic template prose. The absence of user journeys is appropriate for this developer-platform and operations-hardening shape.

Some of the main narrative now carries historical and repeated material whose primary value is auditability rather than product definition. The PRD recognizes this itself in OR9.

### Findings

- **[low]** Volatile history and repeated concern summaries obscure the durable requirement narrative (§1, §5, §11.3, §12 OR9) — §5 largely previews requirements already stated in §§6-8, while lifecycle corrections and historical verdicts recur across the baseline, success metrics, follow-on work, and retired-refinement record. The material is meaningful, but its repetition makes the core baseline harder to scan. *Fix:* At the next anchor-safe major revision, move volatile readiness history to a generated ledger or addendum, fold §5 into the sections where each concern is governed, and keep short stable pointers in the PRD.

## Strategic coherence — adequate

The thesis in §2 is coherent: platform reuse and operational hardening must land together. The feature groups follow that arc, §9 separates MVP from committed post-MVP work, SM8-SM11 test the two sides of the bet, and SM-C1-SM-C5 protect against gaming the metrics. Known failures, including FR36 deployed-runtime parity and NFR7 loss classes, are reported rather than normalized away.

Most product-bet metrics have targets and current states, but SM12 cannot yet be independently computed from the PRD because its denominator is described as the “full required set” without an enumerated population.

### Findings

- **[medium]** Persisted-evidence coverage metric lacks a fixed denominator (§10, SM12; §7, NFR16) — SM12 asks for the share of high-tier tests in the required evidence set, while NFR16 names evidence classes and behaviors but not a stable list of qualifying tests or cases. Different reviewers can therefore produce different coverage percentages without violating the text. *Fix:* Bind SM12 to a versioned manifest or explicit case list, define numerator and denominator rules, and record the current measured result and evidence identity.

## Done-ness clarity — thin

Many individual requirements are verifiable: NFR3 fixes environments, algorithms, and clock skew; NFR5 fixes metadata limits; FR12 fixes `Location` behavior; and NFR7 gives loss-class-specific delivery truth. Feature-level “Done evidence” also gives useful test direction.

However, FR26, FR33, FR34, and NFR17 each combine numerous independently important obligations under one ID. Their traceability rows point at story ranges without a clause-level all-required rule or evidence map. OR7 explicitly concedes that “a `done` story can appear in these rows without closing any named clause,” which is precisely the ambiguity that downstream story creation and acceptance cannot safely absorb.

### Findings

- **[high]** Omnibus requirements permit partial work to masquerade as completion (§6.5 FR26, §6.6 FR33, §6.7 FR34, §7 NFR17, §12 OR7) — These requirements mix security, delivery, deployment, UX, persistence, testing, and documentation consequences, but their group-level evidence and story-range mappings do not identify which story and proof closes each clause. The current global readiness block prevents immediate unsafe handoff, yet this ambiguity would become unsafe if readiness were advanced without OR7. *Fix:* Sub-letter every independent obligation or add a clause-to-story-to-evidence table with stable clause IDs, one testable consequence per clause, and an explicit rule that the parent FR/NFR remains open until every mandatory clause is proven.

## Scope honesty — strong

The PRD is candid about what Phase 4 includes, excludes, and commits post-MVP. In particular, §9.2 says provider-level append fencing is out of scope while also stating that this exclusion is not a safety waiver and cannot satisfy NFR7 or SM11; §7 and OR4 consequently keep MVP completion blocked. FR24 and FR31 are explicitly evidence/specification requirements rather than claims that sharding or fencing has been delivered. §13 reports that there are no inferred assumptions instead of inventing certainty.

No substantive finding is needed for this dimension.

## Downstream usability — adequate

The capability grouping, stable FR/NFR identifiers, glossary, explicit product-shape statement, and FR-to-owner map make the PRD source-extractable. The PRD also protects downstream consumers by declaring that the architecture and epics digests are stale and that handoff remains blocked pending OR14; this review does not treat an honestly blocked external prerequisite as a PRD defect.

One internal coverage claim is broader than the table beneath it. SM2 says §11.2 carries the “equivalent NFR-to-story mapping” and calls the mapping internally complete, while §11.2 deliberately lists selected NFRs and omits NFR5, NFR12, and NFR13.

### Findings

- **[medium]** NFR ownership completeness is overstated (§10 SM2; §11.2) — The PRD presents §11.2 as the NFR counterpart to complete FR ownership, but the table has no rows for NFR5, NFR12, or NFR13. Even if those omissions are intentional because §11.2 focuses on high-risk requirements, the current wording lets downstream readers infer complete NFR ownership coverage. *Fix:* Either add the three missing NFR mappings with their owning stories and evidence expectations, or narrow SM2 and the §11.2 introduction to state exactly which NFR subset is mapped and where ownership for the omitted NFRs is governed.

## Shape fit — strong

The capability-spec shape fits a brownfield developer platform whose primary surfaces are libraries, hosted services, generated APIs, CI/CD, and operator UI. Named user journeys would add little compared with the jobs, technical capabilities, operational outcomes, and explicit handoff boundaries already present. The separate UX ownership rule appropriately keeps detailed interaction design out of this PRD while retaining the UI safety and governance constraints needed by downstream work.

No substantive finding is needed for this dimension.

## Mechanical notes

- FR definitions are unique and complete from FR1 through FR37. Their presentation order follows feature groups rather than numeric order; the stable IDs remain unambiguous.
- NFR definitions are unique and contiguous from NFR1 through NFR19.
- The §11.1 FR ownership table contains one row for every FR1-FR37; the NFR ownership qualification noted above is substantive rather than an ID-continuity defect.
- No inline `[ASSUMPTION]` or `[NOTE FOR PM]` tags are present. §13 round-trips that state correctly, and unresolved matters are instead carried explicitly as owned refinements.
- No user journeys are present, which is appropriate for the declared brownfield developer-platform/operations-hardening form.
