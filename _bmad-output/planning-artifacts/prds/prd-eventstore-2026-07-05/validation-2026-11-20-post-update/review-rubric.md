# PRD quality review — EventStore Phase 4

November 20, 2026. Full working-tree PRD SHA-256 `b0aed43df45d9f82a8cf2e93760bff6edb3ebf9a437c45e3daebea779c532299`, repository commit `607072c682be05885fccf9aefcf9f92289c4e98b`. [Reviewed PRD](../../../prd.md) · [Rubric](../../../../../.agents/skills/bmad-prd/assets/prd-validation-checklist.md)

## Overall verdict

**Fair.** The product thesis, seven November audit cases and October readiness rejection give owners a sound basis for corrective work. Production authentication, loss-prevention metrics and clause ownership still admit incompatible acceptance decisions. The October 6 **Reject** remains the formal readiness result. [Readiness assessment](../../../implementation-readiness.md)

## Decision-readiness — adequate

Sections 0 and 11.4 distinguish owner-authorized correction, release, deployment and consumer removal. The publication evaluator returns unproven authority. The offline matrix still falls below required assurance on all 12 high-risk gates. Both are documented delivery blockers.

- **High — The next-release dependency decision names the wrong candidate version** (§12 OR30, line 782). The historical decision concerns MediatR 14.2.0. Both the Builds commit pinned by the superproject and the checked-out Builds main declare 15.0.0. Server and Gateway import that catalog. An owner could settle 14.2.0 and leave the next release unevaluated. Retain the historical question, bind the review to the candidate’s resolved packages and version, and identify where the release owner checks that decision. [Catalog](../../../../../references/Hexalith.Builds/Props/Directory.Packages.props) · [EventStore imports](../../../../../Directory.Packages.props)
- **Medium — OR13 describes work already implemented** (§§11.4 and 12, lines 728, 753). The completion writer guards both spec and tracker updates. A bounded Story 5.3 adapter exists, and the current-source full proof and live assurance are outstanding. Identify the existing entry point and adapter, then assign the missing evidence and remaining adapters. Parent Story 9.2 stays in progress. [Guard specification](../../../../implementation-artifacts/spec-9-2-mandatory-high-risk-nfr-completion-guard.md) · [Current proof limits](../../../../implementation-artifacts/spec-9-2-story-5-3-guarded-concurrent-write-proof.md)

## Substance over theater — strong

The journeys connect actual decisions to users. Priya can distinguish projection-backed confirmation from command acceptance. Nora gets an unavailable total when ownership of legacy Admin rows cannot be established. The conditional-query case specifies HTTP 200 with neither header nor metadata ETag after an untagged read races a projection change. These observations supply falsifiable acceptance tests (§§3.3, 6.1 and 6.5). [Approved November scope](../../../sprint-change-proposal-2026-11-20.md)

## Strategic coherence — adequate

The SDK-reuse and operational-hardening goals reinforce each other. SM8 identifies actual remaining Tenants host plumbing, and SM-C4 prevents a team from merely relocating it. Two metric definitions could still mislead a delivery decision.

- **High — SM11 counts a class before qualifying its evidence** (§§7, 10 and 11.3, lines 403, 582, 689). The numerator includes class (e) on historical source-only evidence while NFR7 requires prevention proven through the production path for the supported envelope. The retained evidence records real scenario results for a bounded profile. Its scope does not establish the current fourth delivery claim. Show the three other credited classes separately and mark (e) pending until the exact runtime, envelope and qualifying results are bound. G-OQ8 design availability alone cannot establish this result. [Historical capture](../../../../implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml) · [Successor scope](../../../../implementation-artifacts/4-15-oq8-platform-closure-successor.json)
- **Medium — SM10 leaves excluded surfaces in the apparent denominator** (§§9.2, 10 and 11.4, lines 545, 581, 731). The metric requires 100% of inventory entries to demonstrate rejection. Out-of-profile UI surfaces must be listed and never counted as covered. Define the fraction over in-scope protected surfaces, keep excluded entries and reasons visible, and validate anonymous probes/assets/callbacks against their separate metadata and safe-response contract. This lets Story 9.5 compute one answer. [Owner routing decision §7.2](../../../sprint-change-proposal-2026-10-07-architecture-routing.md)

## Done-ness clarity — thin

FR33 now protects stored GUID replay and requires ULIDs on new writes. NFR6 and the redelivery example distinguish external CloudEvents identity from EventStore’s subscription marker. The SignalR bounds, snapshot overhead and projection workload are measurable. Two acceptance boundaries still need correction.

- **High — Production authentication has conflicting acceptance rules** (§§6.5 and 7, lines 337, 399). “Production auth rejects insecure modes unless explicitly break-glassed” lets a tester accept the Production symmetric-key configuration that NFR3 explicitly forbids. Require rejection in Production even when the flag is enabled, and test the separately permitted non-Development, non-Production case. [Ratified authentication policy](../../../sprint-change-proposal-2026-09-08-nfr3-nfr4-authentication-ratification.md)
- **Medium — Status-link closure has a wider wording than its approved scope** (§§7.1, 8.2 and 11.4, lines 440, 493, 719). FR12-C4 and §8.2 forbid any correlation-selected public status resource. G-STATUS-ID can verify generated links while the gateway GET still performs its separately deferred lookup. Scope the generator clause to emitted links and record an owner and acceptance decision for the GET compatibility path. That avoids counting generator coverage as whole-endpoint conformance. [Story 2.15 scope](../../../../implementation-artifacts/spec-2-15-messageid-only-command-status-identity.md) · [Gateway status controller](../../../../../src/Hexalith.EventStore/Controllers/CommandStatusController.cs)

## Scope honesty — adequate

Epic 9 is explicitly in the MVP. Epic 8 and G5 stay separate. Story 8.12’s 14/14 isolated cases at SDK/runtime 1.19.1 support a bounded handoff to 8.7. Platform qualification, activation and rollback still require their own evidence. [Story 8.12 verification](../../../../implementation-artifacts/spec-8-12-dapr-kubernetes-cryptography.md)

- **Medium — The FR31 handoff can exclude the approved append-protection slice** (§§6.4, 9 and 11.4, lines 323, 524, 720). The standalone “done evidence” paragraph calls both implementations outside MVP and points to §9.2. The November 1 decision explicitly brought mandatory Dapr-owned atomic conflict protection into Story 4.16, while general activation fencing and sharded allocation remain outside. Keep FR31 as the historical spike and name the approved 4.16 implementation and evidence boundary in its handoff. [Approved scope, §§4–5](../../../sprint-change-proposal-2026-11-01-story-4-16-atomic-conflicting-writes.md)

## Downstream usability — thin

The two inventories agree verbatim on 37 FRs and 19 NFRs. The three repaired numeric story mappings now pass. A downstream reviewer can still encounter incompatible instructions when deriving clause owners or asking for all supporting high-risk work.

- **High — Four clauses cannot be mirrored with exactly one primary as written** (§§7.1, 9.1 and 11.4, lines 424, 427, 460, 461, 528, 721). FR4-C2, FR4-C5, FR34-C10 and FR34-C11 each list two or three primary slices. Story 9.4 requires exact agreement with the PRD and one primary slice per clause. Give each clause one accountable story and mark the other contributors as supporting, or approve disjoint subclauses with explicit parent closure. This resolves a contract conflict independently of implementing the validator. [Story 9.4 acceptance](../../../epics.md#story-94-stable-clause-ledger-and-all-clauses-validator)
- **Medium — Two letter-suffixed specification stories disappear from the NFR map** (§11.2, lines 653, 658). The epics record 6.5c as supporting NFR7 and 6.5d as supporting NFR7/NFR12. The PRD lists neither. The completion reader disagrees on both, and its key grammar excludes all four 6.5a–d IDs. Define how the historical design contributions are represented and consumed, including a test for lettered IDs or an explicit parent mapping. Keep 6.5 as design approver. [Epic specifications](../../../epics.md#story-65c-publication-subscription-and-rollout-spec) · [Completion reader](../../../../../tools/phase_4_story_completion.py)
- **Low — Owed-refinement text repeats completed assignments and tracker repairs** (§12, lines 770, 776, 802). OR7 and OR24 still instruct assignment of clauses that already have owners. OR19 still requests repair of a complete 2.12 key. Epic 2 currently has open stories, consistent with its in-progress rollup. Replace the obsolete actions with verification of the existing assignments and retire the tracker repair against its current evidence. [Epic ownership](../../../epics.md) · [Sprint tracker](../../../../implementation-artifacts/sprint-status.yaml)

## Shape fit — adequate

Capabilities, user journeys and a dedicated exit contract suit this brownfield platform. Volatile evidence also appears in the journeys, glossary and normative rows. OR9 already records a future structural change to reduce that maintenance burden.

- **Medium — The journey introduction still calls the detailed UX draft** (§§3.3 and 11.3, lines 188, 685). DESIGN and EXPERIENCE are both final. A reader entering through UJ3 receives the opposite status from the baseline register. Update the journey introduction and point to G-BASELINE for the separate cross-artifact approval. [DESIGN](../../../ux-designs/ux-eventstore-2026-07-05/DESIGN.md) · [EXPERIENCE](../../../ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md)

## Mechanical notes

All 80 frontmatter source paths and local Markdown links resolve. All 49 stable clause IDs are unique. Five named journeys, twelve success metrics and five counter-metrics have contiguous IDs. The assumptions index agrees with the absence of inline assumptions. No PRD addendum was present.

The coverage reader accepted 139 of 142 numeric story headings. The three refusals concern superseded 3.22 and 3.23 and ledger 4.8. Including the four lettered headings gives 141 of 146 accepted, with the two additional disagreements above. Historical story labels grant no new approval.

The previous repairs also hold for the scoped runtime-parity and publication rows, Stories 6.2–6.7, OR25–26, SM3/SM4 and the source-bound November validation reference. All five epic input digests still differ from current planning bytes. This review makes no baseline or readiness decision. [Earlier update review](../review-rubric-update-2026-11-20.md) · [Original validation](../validation-2026-11-20/validation-report.md)

**Finding counts** · 0 critical · 4 high · 6 medium · 1 low.
