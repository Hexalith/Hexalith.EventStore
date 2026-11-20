# EventStore Phase 4 PRD validation after the November 20 update

**BMAD grade · Fair**

**Phase 4 implementation readiness · Blocked (Reject)**

0 critical · 8 high · 11 medium · 0 low

Reviewed 2026-11-20T16:00:41+01:00. EventStore main at 607072c682be05885fccf9aefcf9f92289c4e98b, with working-tree edits. PRD SHA-256 `b0aed43df45d9f82a8cf2e93760bff6edb3ebf9a437c45e3daebea779c532299`. Epic inventory SHA-256 `1010d536bfa52d3d53ee33fee0b35e72f533b942b0ffc967076fc7bd976982db`. [Reviewed PRD](../../../prd.md) · [Epic inventory](../../../epics.md) · [October 6 readiness decision](../../../implementation-readiness.md)

## Overall verdict

The revised PRD gives owners a sound basis for corrective work. The November 20 fixes are present in the sections reviewed during the update, including legacy GUID replay, subscriber identity and the three repaired numeric story mappings. Across the full document, several acceptance rules still give different answers for the same production configuration, Admin request or evidence record.

The readiness review found a circular prerequisite for the final assessment and two gates that overlook existing controls. The traceability review showed where the story reader can report agreement even when the accountable owner differs. Phase 4 remains blocked under the October 6 decision.

## Rubric judgments

| Dimension | Judgment | Basis |
| --- | --- | --- |
| Decision-readiness | Adequate | The PRD supports the current stop decision and names corrective owners. Its prerequisite order and several gate descriptions could misdirect the next assessment. |
| Substance over theater | Strong | The seven November audit cases specify observable behavior for clients, subscribers and operators, including untagged query responses and durable effects after redelivery. |
| Strategic coherence | Adequate | SDK reuse and operational hardening each have a metric and counter-metric. The SM10 denominator and SM11 evidence standard still need reconciliation. |
| Done-ness clarity | Thin | Identity, metadata bounds and workload budgets are testable. Production authentication and deferred-route responses still admit opposite acceptance decisions. |
| Scope honesty | Adequate | Epic 9 is in the MVP. Epic 8 and G5 remain separately gated. The FR31 feature handoff obscures the narrower protection that Story 4.16 is authorized to deliver. |
| Downstream usability | Thin | All 56 requirement definitions agree. Four clauses still lack a single accountable story and two letter-suffixed specification stories fall outside the high-risk mapping. |
| Shape fit | Adequate | The capability sections, five named user journeys and decision register fit a brownfield platform. Repeated status text has drifted between the journey introduction and the register. |

## Findings by severity

### Critical (0)

None.

### High (8)

**The readiness assessment requires its own result** · Opening decision, §11.4 G-READINESS, OR1 · lines 109, 710, 732, 794

OR1 requires every mandatory gate to pass before the assessment runs. One of those gates, G-READINESS, is the result the assessment writes. The opening also puts release and promotion behind the aggregate, even though G-CONSUMER consumes their records. Following these instructions literally leaves no first valid transition.

**Correction.** Specify the predecessor gates for each transition. Run the final assessment after its prerequisite rows pass, record G-READINESS, then use that result for READY. Keep release, promotion and consumer removal tied to their respective subjects and approvals.

[Decision and exit contract](../../../prd.md) · [Transition evaluator](../../../../../tools/phase_4_transition.py) · [Publication state order](../../../../../docs/architecture/publication-authority-v1.md)

**Production authentication has two conflicting acceptance rules** · §6.5 and NFR3 · lines 205, 337, 399

Section 6.5 allows an explicit override for insecure Production authentication. NFR3 and its tests require Production to reject symmetric-key mode even when the override is enabled. Two reviewers could reach opposite conclusions on the same configuration.

**Correction.** Make unconditional Production rejection the §6.5 acceptance criterion. Retain the separate provision for eligible non-Production environments and its test.

[PRD acceptance criteria](../../../prd.md) · [Shared JWT contract](../../../../../src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtBearerAuthenticationContract.cs) · [Environment-specific tests](../../../../../tests/Hexalith.EventStore.Server.Tests/Authentication/EventStoreAuthenticationOptionsTests.cs)

**Deferred Admin routes have conflicting response rules** · §6.7, NFR15, FR34-C4 and §8.3 · lines 362, 411, 454, 506

The PRD says every retained deferred endpoint returns HTTP 501. Story 7.4 requires authentication, tenant authorization and request validation first. The final UX also hides restore/import actions and retains read-only tracking where specified. An unconditional 501 could disclose a capability to an unauthorized caller.

**Correction.** State the order in both inventories. Preserve normal denial and validation outcomes. Return typed 501 and make no downstream call for an authorized, valid request to a retained deferred route. Give the UI its separate visibility rules.

[PRD](../../../prd.md) · [Story 7.4](../../../epics.md) · [Experience specification](../../../ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md)

**Four clauses have multiple primary owners** · §§7.1, 9.1, 11.2 · lines 424, 427, 460–461, 467–471, 528, 663

FR4-C2, FR4-C5, FR34-C10 and FR34-C11 each name two or three primary stories. Story 9.4 requires exactly one primary per clause. Three NFR17 clauses also promote Stories 7.6, 7.8 and 7.9 from supporting in the story declarations to primary in the ledger. The numeric coverage check combines owner roles and cannot detect the discrepancy.

**Correction.** For each stable clause, have owners name one accountable story and list required contributions separately. Reconcile NFR17 role assignments and verify the ownership columns before accepting closure evidence.

[PRD clause and ownership tables](../../../prd.md) · [Story declarations and Story 9.4](../../../epics.md) · [Current coverage reader](../../../../../tools/phase_4_story_completion.py)

**SM11 counts historical source evidence as current prevention** · NFR7 and SM11 · lines 403, 582

The PRD counts four of five loss classes as delivered. For duplicate side effects, class (e), it cites Story 4.15’s historical, source-only result. NFR7 requires production-path proof for the supported operating envelope. The cited evidence alone cannot support that fourth credit.

**Correction.** Keep classes (a), (b) and (d) as the three the PRD otherwise credits. Show (e) as implemented with historical evidence pending current qualification and (c) as open. Bind the numerator to its evidence subject.

[NFR7 and SM11](../../../prd.md) · [Story 4.15 evidence scope](../../../../implementation-artifacts/spec-4-15-oq8-lifecycle-closure-v4.md) · [Story 4.17 authority limits](../../../../implementation-artifacts/spec-4-17-oq8-governing-design-authority-import.md)

**The compatibility gate overlooks an enforced release check** · §11.4 G-COMPAT · line 723

Release preparation already runs API compatibility checks against signed 4.2.0 baselines across all 14 release packages and resolves isolated package consumers. The PRD says an enforcing release gate and representative consumer coverage are absent. That directs Story 3.18 toward work partly delivered.

**Correction.** Record what the assembly API and package-consumer checks cover. Identify the outstanding wire, generated-surface and consumer cases, and require assurance against the exact release candidate.

[G-COMPAT](../../../prd.md) · [Release preparation](../../../../../.releaserc.json) · [Compatibility verifier](../../../../../tools/verify-release-api-compatibility.py)

**The Tenants Production API already uses shared JWT validation** · §11.4 G-AUTH-HOSTS and OR23 · lines 724, 775

The gate describes Tenants as using a weaker hand-written subset. On synchronized Tenants revision 8318f917, the Production API calls the shared JWT validator and configurator, with negative tests. Other environments follow a separate path. A single host’s adoption still leaves the all-host requirement open.

**Correction.** Record the verified host, environment and revision. Direct Story 5.11 to the outstanding fingerprints, generated and future hosts, and release-subject assurance.

[G-AUTH-HOSTS](../../../prd.md) · [Tenants API entry point](../../../../../references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs) · [Production configuration](../../../../../references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/ProductionApiAuthentication.cs) · [Tenants Production tests](../../../../../references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiProductionAuthenticationTests.cs)

**The release licensing decision names an older dependency** · §12 OR30 · line 782

OR30 names MediatR 14.2.0 for the next-release decision. The synchronized Builds catalog selects 15.0.0, and the restored Server and Gateway assets resolve that version. Reviewing the historical version does not settle the candidate’s licensing question.

**Correction.** Identify the candidate package graph and applicable terms before the release owner records a decision. Keep any previously released version tied to its original release.

[OR30](../../../prd.md) · [Catalog import](../../../../../Directory.Packages.props) · [Current Builds catalog](../../../../../references/Hexalith.Builds/Props/Directory.Packages.props)

### Medium (11)

**SM10 has no achievable coverage denominator as written** · §10 SM10 and §11.4 G-MVP-COVERAGE · lines 581, 731

SM10 targets 100% of the surface inventory. Its next sentence requires out-of-scope interactive UI surfaces to remain listed and never count as covered. One compliant exclusion can prevent a 100% result even when every in-scope surface passes.

**Correction.** Retain all entries for audit. Define the denominator as in-scope production surfaces, state how the two anonymous exception classes are evaluated, and report excluded surfaces separately.

[SM10 and MVP scope](../../../prd.md) · [Approved UI-host scope](../../../sprint-change-proposal-2026-11-09-readiness-recovery.md)

**Two letter-suffixed stories lose their supporting NFR mappings** · §11.2 and Stories 6.5c–6.5d · PRD lines 653–664

The 139/142 result covers numeric headings. Including all four suffixes gives 141 accepted mappings out of 146. The reader refuses 6.5c for NFR7 and 6.5d for NFR7 and NFR12. Their historical specification contributions cannot be recovered through the ordinary completion writer’s numeric story-key grammar.

**Correction.** Retain Story 6.5 as approval owner. Specify how a future consumer recovers the supporting evidence for 6.5c and 6.5d and test that route, or explicitly record the approved parent mapping.

[PRD coverage declarations](../../../prd.md) · [Epic 6 supporting stories](../../../epics.md) · [Story identifiers and reader](../../../../../tools/phase_4_story_completion.py)

**Two handoffs still prescribe superseded evidence paths** · §8.1, G-MVP-COVERAGE, OR24 and OR27 · lines 486, 727, 731, 776–779

OR27 requests a durable manifest at a literal path under the ignored evidence directory. AD-12 designates the owning spec and CI run as the durable record. The consumer-removal instruction has the same ambiguity. An implementer could satisfy the literal path locally and leave no durable decision record.

**Correction.** Name the durable owner and source, distinguish generated CI output from committed evidence, and preserve every coverage and refusal condition.

[PRD evidence policy and refinements](../../../prd.md) · [Architecture AD-12](../../../architecture.md) · [Repository ignore rules](../../../../../.gitignore)

**OQ8 omits a successful CI run on the committed source** · §1.1, G-OQ8 and OR11 · lines 143, 717, 752

The fixture and design-availability jobs succeeded on committed EventStore revision 607072c68. The manual assured-decision job was skipped. The blanket pending status hides a completed check and obscures exactly which bytes still need a hosted run.

**Correction.** Record the two successful jobs on their commit. Require new CI for the uncommitted PRD under review and retain the separate seal and attestation requirements.

[PRD OQ8 gate](../../../prd.md) · [OQ8 workflow](../../../../../.github/workflows/oq8-governing-design.yml) · [GitHub Actions run 44726206620](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/44726206620)

**OR7, OR24 and OR19 still assign completed work** · §12 OR7, OR24 and OR19 · lines 770, 776, 802

OR7 and OR24 call for assignments already recorded for FR36 and NFR17 clauses. The evidence and validators remain outstanding. OR19 requests repair of a truncated Story 2.12 key, although the current tracker contains the full key and records the story as done.

**Correction.** Close the obsolete assignment and key-repair instructions. Keep evidence work and require a conflicting source before assigning another Epic 2 rollup repair.

[PRD refinements](../../../prd.md) · [Epic declarations](../../../epics.md) · [Sprint tracker](../../../../implementation-artifacts/sprint-status.yaml)

**Epic summaries omit all four November 20 additions** · PRD §1 and Epic List for Epics 2, 5 and 7 · PRD line 137

The detailed epics and tracker contain 2.18, 2.19, 5.15 and 7.23. The handoff summaries still stop at 2.17, 5.14 and 7.22. They also label Stories 2.14 and 2.15 backlog despite their current in-progress and review states. A handoff based on the overview drops approved work.

**Correction.** Extend the three story ranges. Date historical lifecycle labels or refer readers to the tracker.

[Approved audit proposal](../../../sprint-change-proposal-2026-11-20.md) · [Epic overview and detailed stories](../../../epics.md) · [Sprint tracker](../../../../implementation-artifacts/sprint-status.yaml)

**A Tenants reuse metric holds unrelated requirements open** · §3.3 UJ1 and §10 SM8 · lines 190, 579

UJ1 says residual Tenants host wiring keeps FR1–FR10 open. SM8 measures Tenants adoption under FR9. FR6 cursor validation and FR10 package inventory have their own owners and evidence, and can be evaluated independently.

**Correction.** Keep the cross-feature journey. Apply the residual-wiring condition to the affected adoption and guardrail requirements and measure each other FR using its own acceptance criteria.

[User journey and metric](../../../prd.md) · [FR owners in Epics 1 and 2](../../../epics.md) · [Tenants host wiring](../../../../../references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs)

**The journey introduction sends readers to a superseded UX status** · §3.3 and §11.3 · lines 188, 685, 716

The journey introduction still calls DESIGN and EXPERIENCE draft. Both files have final frontmatter, and the updated register correctly records that status. UX handoff readers reach different conclusions depending on where they enter the PRD.

**Correction.** Reference the current status register from the journey introduction. Keep approval of the common implementation baseline with Story 9.3 and OR14.

[Journey introduction and register](../../../prd.md) · [DESIGN](../../../ux-designs/ux-eventstore-2026-07-05/DESIGN.md) · [EXPERIENCE](../../../ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md)

**OR13 understates the story-completion control already implemented** · §12 OR13 · line 753

The refinement describes the high-risk story completion adapter as outstanding without recording the shared completion guard or the bounded Story 5.3 adapter. The remaining work concerns uncovered business adapters and live assurance.

**Correction.** Name the existing guard, its supported story and its limits. Assign the remaining exact-subject evidence adapters and approvals to their owners.

[OR13](../../../prd.md) · [Completion writer](../../../../../tools/phase_4_story_completion.py) · [Story 5.3 adapter](../../../../../tools/phase_4_story_5_3.py)

**Status-link acceptance exceeds the scope of Story 2.15** · FR12-C4, §8.2 and G-STATUS-ID · lines 440, 493, 719

FR12-C4 bans correlation-based status selection generally. Story 2.15 proves which identifier selects a generated command-response link. The gateway GET still has a separately deferred compatibility lookup. A passing generator check could be read as proof of the entire public endpoint.

**Correction.** Apply the generator clause to emitted links. Name an owner and separate acceptance decision for gateway GET compatibility, and keep the broader endpoint claim open until then.

[FR12-C4 and G-STATUS-ID](../../../prd.md) · [Story 2.15 scope](../../../../implementation-artifacts/spec-2-15-messageid-only-command-status-identity.md) · [Gateway status controller](../../../../../src/Hexalith.EventStore/Controllers/CommandStatusController.cs)

**The append-race handoff omits the approved MVP protection work** · §6.4, §9 scope boundary and G-APPEND · lines 323, 524, 720

Section 6.4 says the race spike and sharding study deliver no implementation and calls both implementations outside MVP. Read alone, that can exclude Story 4.16’s approved Dapr-owned atomic conflict protection. The November decision keeps general activation fencing outside scope while commissioning a specific protection for qualified write profiles.

**Correction.** Keep FR31’s race observation as historical evidence. State how it leads to Story 4.16’s bounded implementation and qualification, while sharded allocation and general activation fencing remain excluded.

[Feature handoff and MVP boundary](../../../prd.md) · [November 1 approved scope](../../../sprint-change-proposal-2026-11-01-story-4-16-atomic-conflicting-writes.md)

### Low (0)

None.

## Verification

- The three reviews raised 24 findings before consolidation. A repeated issue is counted once at the highest supported severity. The November 20 update reconciled all 14 earlier entries. The stale reference in §3.3 is a remaining occurrence outside the corrected UX register.
- The PRD and epic inventory agree verbatim on 37 functional and 19 non-functional requirements. The 49 stable clause IDs are unique, all 80 frontmatter source paths resolve, and the five journeys, twelve metrics and five counter-metrics have continuous identifiers. No PRD addendum was present.
- The coverage reader accepts 139 of 142 numeric headings. Stories 3.22 and 3.23 were superseded, and 4.8 is a historical ledger. Including the four letter-suffixed headings gives 141 of 146. The additional refusals are 6.5c and 6.5d.
- All five recorded epic input digests differ from the current files. Story 9.3 and OR14 retain baseline reconciliation and approval.
- The offline gate matrix exited 1. All 12 high-risk gates achieved `none` against the required `single-maintainer-attested` level. Five standard-control classifications passed. The publication evaluator exited 1 with `authority: unproven` and no seal.
- The local OQ8 governing-design availability check exited 0. Hosted run 44726206620 covers the committed source. Hosted verification of the uncommitted PRD and an assured gate decision are still outstanding.

## Reviewer files

- [Full BMAD rubric review](review-rubric.md) — Seven judgments, with evidence for the remaining acceptance, metric and handoff discrepancies.
- [Readiness and gate evidence](review-readiness.md) — The current refusal is justified. Several gate descriptions use outdated evidence or direct an owner to a control that already exists.
- [Downstream acceptance and traceability](review-traceability.md) — Matching inventory text masks conflicts in endpoint behavior, primary ownership and summaries used for planning.

Rubric · [BMAD PRD quality rubric](../../../../../.agents/skills/bmad-prd/assets/prd-validation-checklist.md)
