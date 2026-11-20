# EventStore Phase 4 PRD validation

**BMAD grade · Fair**\
**Phase 4 readiness · Blocked (Reject)**\
0 critical · 9 high · 4 medium · 1 low

Validated 2026-11-20T15:24:43+01:00. Working-tree PRD on EventStore main at 607072c682be05885fccf9aefcf9f92289c4e98b.

PRD SHA-256 `709069aa88cb92b08f9b51252f3ce63bef0aa351b904f6f72e5e64beef4d1d9e`. [Reviewed PRD](../../../prd.md)

## Overall verdict

The PRD is clear enough to explain why Phase 4 remains blocked. The seven November 20 audit findings have observable acceptance criteria, and Stories 2.14 and 2.15 distinguish passing technical checks from the approvals still owed. A maintainer using the full PRD to schedule work or accept an implementation, however, will encounter conflicting instructions about replay compatibility, story ownership and which evidence can be reproduced.

First reconcile the gate table with the owning specifications. Then resolve differences that change product behavior or prevent the completion tool from reading a story’s requirements. Story 9.3 can use those decisions to reconcile the baseline before its owners approve new digests.

## Rubric judgments

| Dimension | Judgment | Basis |
| --- | --- | --- |
| Decision-readiness | Thin | The blocked decision is supported. Several gate rows identify the wrong remaining action or cite retired verification commands. |
| Substance over theater | Strong | The audit cases specify visible outcomes, including an untagged HTTP 200 response, two durable subscriber effects after one redelivery, and unavailable Admin data when its tenant scope cannot be established. |
| Strategic coherence | Strong | The platform reuse and operational trust goals have corresponding success metrics and counter-metrics. The November findings fit the existing MVP. |
| Done-ness clarity | Thin | The 49 stable acceptance clauses give engineers concrete tests. Conflicting statements about event identity, authorization and required evidence can still produce opposite acceptance decisions. |
| Scope honesty | Adequate | The separate post-MVP gate for payload protection is explicit. The MVP scope list omits its governance epic, and the post-MVP handoff understates verified progress. |
| Downstream usability | Thin | All 56 requirement IDs are present in both documents. Some definitions differ in meaning, and three current story mappings fail the repository’s coverage check. |
| Shape fit | Adequate | Capabilities, five named user journeys and clause ownership suit a brownfield platform. Undated status claims alongside historical decisions make the current handoff harder to use. |

## Findings by severity

### Critical (0)

None.

### High (9)

**Runtime parity relies on a retired verifier** · §§6.8, 10, 11.3–11.4 · lines 377, 573, 693, 730

The PRD treats the September three-receipt result as reproducible on this checkout. Its prescribed Python command exits 2 because the verifier was removed. The current gate matrix lists the business evidence binding as missing.

**Correction.** Date the September result and its subject. Have the Story 3.15 owner define a current evaluator under AD-12 before anyone uses this gate to support a release or migration decision.

**Evidence.** Story 3.15 tracker entry, evidence-storage simplification spec and `phase-4-gate-risk-matrix.json` lines 232–247.

**The publication validator already exists** · §11.4 G-PUBLICATION-AUTH and OR29 · lines 731, 786

The gate row says the validator is absent and Story 3.19 is backlog. The schema and evaluator exist. Local evaluation exited 1 with authority unproven and no seal. The parent story is in progress.

**Correction.** Record the delivered validation work. Direct the release owner to the missing approved profile, authority records, bound prerequisites and authenticated consumption.

**Evidence.** `tools/validate-publication-authority.py evaluate`, `docs/architecture/publication-authority-v1.md` and Story 3.19 validator spec lines 141–157.

**FR33 can break historical replay** · §6.6 FR33 · line 344

FR33 requires identity components to be ULID-safe without distinguishing stored events from new writes. Story 6.6 supports reading legacy GUID MessageIds. Its correlation and causation rules apply on both reads and writes.

**Correction.** Specify the rule for each field and direction. Add a positive case for replaying an existing event with a GUID MessageId and retain the stricter write rules.

**Evidence.** Story 6.6 event versioning and upcasting implementation spec, lines 25–33.

**The two inventories allow different acceptance decisions** · §§6–7 and epic requirements inventory

Both inventories contain the same 56 IDs. After removing Markdown row delimiters, 38 definitions match and 18 differ. The epic summary of FR12 omits important constraints on status identity and tenant checks. Its FR36 summary omits separate release, promotion and consumer-removal conditions. Some differences, including the FR37 cross-reference, are editorial.

**Correction.** Through Story 9.3, reconcile FR12, FR36, NFR2 and NFR3 first. Classify the remaining differences by their effect on acceptance before rebinding the documents.

**Evidence.** PRD FR12, FR36, NFR2 and NFR3. Epic inventory lines 64, 112, 120 and 122, and the epic completion rule at line 405.

**Three current story mappings fail the completion reader** · §11.2 · lines 654, 663, 666

The PRD omits NFR7 for Story 4.16 and omits NFR7 and NFR16 for Story 4.17. Its Story 6.6 mapping adds post-MVP NFR19 to the story’s declared requirements. The reader refuses to derive completion from these disagreements.

**Correction.** Align each primary or supporting mapping with its approved story. Rerun the coverage reader and preserve the historical dispositions for Stories 3.22, 3.23 and 4.8.

**Evidence.** `tools/phase_4_story_completion.py` and epic declarations at lines 4215, 4255 and 5431. The traceability review includes the reproduction command.

**Story 6.4 has conflicting authorization and lifecycle instructions** · §§7, 11.1–11.4 · lines 405, 632, 655, 701, 727

One row describes the specification as absent and 6.4 as unauthorized. Another records the specification approval and calls 6.4 backlog. The tracker shows 6.2 and 6.3 done and 6.4 in progress.

**Correction.** Use the approved projection-cost specification and current tracker states throughout the handoff. Keep runtime acceptance contingent on measured evidence.

**Evidence.** `spec-projection-cost-sequence-guard.md` and the Epic 6 rows of `sprint-status.yaml`.

**The AOT gate asks the owner to recreate an existing reference** · NFR18, G-NFR18 and OR6 · lines 415, 734, 765

The AOT/trimming page exists and Story 6.7 is done. The recorded owner attestation covers digest `babd1cd1…`. The current page hashes to `99395b0a…` and the owning spec explicitly leaves re-attestation open.

**Correction.** Direct the owner to review the current bytes, record the attestation and reconcile the gate row. A standard-control classification does not settle this requirement.

**Evidence.** `docs/reference/aot-and-trimming-posture.md` and Story 6.7 spec lines 123–141.

**The UX baseline register conflicts with the current gate row** · §§11.3–11.4 · lines 687–689, 721

The register describes the detailed UX as draft and records a matching UX handoff digest. DESIGN, EXPERIENCE and the UX handoff have final frontmatter. All five recorded epic input digests differ from their files, as the later gate row already recognizes.

**Correction.** Label the earlier register as historical and link its current replacement. Reconcile the source changes and obtain baseline approval before updating the digests.

**Evidence.** Epic frontmatter `inputDocumentDigests`, UX frontmatter and the November 9 readiness-recovery proposal.

**The post-MVP handoff understates Story 8.12 completion** · §§6.9, 8.4, 11.3 and OR31 · lines 392, 519, 697, 793

The PRD calls the Dapr/Kubernetes work pending. The tracker and spec mark the bounded story done. Revision 4 approved SDK 1.19.1, and the spec records 14/14 isolated Kubernetes cases passing for its pinned cohort.

**Correction.** Record the bounded handoff to Story 8.7. Keep activation, fleet and capacity qualification, release and G5 approval with their respective owners.

**Evidence.** Story 8.12 supplement and implementation spec lines 310–317. The sprint tracker records completion at line 305.

### Medium (4)

**The MVP scope list omits Epic 9** · §9.1 · lines 527–540

The list names Epics 1–7. The epic plan also classifies Epic 9 as MVP governance, and several mandatory exit controls depend on it.

**Correction.** List Epic 9 with its assurance and baseline responsibilities. Continue to describe Epic 8 as separately gated post-MVP work.

**Evidence.** `epics.md` lines 393–407 and its Epic 9 story sections.

**Subscriber identity needs an explicit boundary** · §6.4 and NFR6 · lines 320, 403

The new acceptance case uses the received CloudEvents `(source, id)` within a subscription. NFR6 uses `MessageId` without stating whether it refers to an external subscriber or platform processing.

**Correction.** Name the consumer and scope at both sites. Retain the publisher’s mapping from persisted `MessageId` to CloudEvents `id` and the test with two distinct events plus one replay.

**Evidence.** Story 7.1 audit acceptance and the November 20 sprint proposal.

**OR25 and OR26 still request owners for delivered stories** · §12 · lines 782–783

Stories 2.16 and 2.17 appear as backlog in these rows. The tracker and owning specs mark the bounded SignalR and generator-quality stories done.

**Correction.** Use the existing ownership and evidence. Identify any separate verification needed for the coverage or readiness decision.

**Evidence.** `spec-2-16-bounded-signalr-detail-metadata.md`, `spec-2-17-generated-code-and-generator-build-quality.md` and the sprint tracker.

**The historical validation reference points to a different assessment** · PRD frontmatter · lines 12–16

The PRD identifies a September 10 review. The referenced report currently opens an October 7 review of a different commit and document digest. Their shared grade cannot identify which decision the PRD cites.

**Correction.** Bind the September assessment to an immutable revision or update the metadata to identify the intended October report.

**Evidence.** PRD validation frontmatter and the October 7 `validation-report.md` header.

### Low (1)

**The metrics heading and lifecycle examples imply the wrong status** · §10 · lines 563–572

The group labelled “achieved” includes SM3, which is unmet. SM4 also describes Story 8.2 as backlog although the tracker records it done.

**Correction.** Use a neutral heading and date the historical decomposition. Keep the achievement distinct from the later execution status.

**Evidence.** SM3, SM4 and the Story 7.14 and 8.2 tracker rows.

## Verification

- Validated the uncommitted November 20 PRD on repository commit `607072c68`. The October 6 implementation-readiness assessment remains the last formal decision. The October 7 PRD quality review remains a separate historical artifact.
- The offline gate-risk-matrix CLI exited 1. All 12 high-risk gates achieved `none` against required `single-maintainer-attested`. Five standard-control rows passed their classification checks. Those checks confer no product acceptance or release authority.
- The OQ8 governing-design verifier exited 0 for authority availability. Runtime conformance and an assured gate decision still require separate evidence.
- All 80 frontmatter source paths resolve locally. The normative inventory contains 37 functional and 19 non-functional requirements. The clause ledger contains 49 unique IDs. All five epic input digests differ from the current planning files.
- The November 20 proposal’s seven outcomes and four new backlog rows agree with the ten affected story mappings. The broader coverage walk examined all 142 numeric story headings and found six refusals. Three concern current mappings, two concern superseded stories, and one is a historical ledger.
- The earlier update recorded 32/32 story-completion tests in an isolated checkout and 42/43 transition fixtures. This validation reran the document, coverage, OQ8 and gate-matrix checks. Those earlier suite totals are historical evidence.

## Reviewer files

- [Full PRD rubric review](review-rubric.md) — The seven dimensions distinguish the strengths of the product requirements from defects in acceptance, evidence and handoff.
- [Readiness evidence review](review-readiness.md) — The offline assurance result supports the blocked decision. Gate rows for runtime parity, publication and AOT name retired tooling or work already delivered. The UX register and historical review reference also need reconciliation.
- [Scope and traceability review](review-traceability.md) — All seven November findings have story-level acceptance cases. The full inventory comparison and completion reader expose semantic differences and three current mapping failures. The review includes reproduction commands and direct sources.

Rubric · `.agents/skills/bmad-prd/assets/prd-validation-checklist.md`
