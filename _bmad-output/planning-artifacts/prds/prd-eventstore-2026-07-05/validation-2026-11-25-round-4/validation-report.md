# EventStore Phase 4 PRD validation

**BMAD grade · Excellent**

**Implementation readiness · Blocked (Reject)**

Prior review · 13 resolved, 0 partially resolved, 0 open. Current review · 0 critical, 0 high, 5 medium, 7 low.

Grade basis · All seven rubric dimensions are strong or adequate and no deduplicated finding is critical or high, which is the Excellent condition in validate.md; the formula does not weigh the five medium findings.

Reviewed 2026-11-25 on the working tree of EventStore `main` at `517fedfe1357e2157e17d1d8621637a31cd0e961` with uncommitted update-run edits. PRD SHA-256 `8ba4e8c044653fbcb17230d4886c9bef256f37ef81acc3cbb3bd27218fb20923`. Epic plan SHA-256 `3297793cbc74cb9e4ec42c98fba5d87385c90ae1565ae2c2f2f43641dc7a3aee`. Synthesized 2026-11-25T23:52:45+01:00. [PRD](../../../../../_bmad-output/planning-artifacts/prd.md) · [Epics](../../../../../_bmad-output/planning-artifacts/epics.md) · [Prior validation](../../../../../_bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/validation-2026-11-20-round-3/validation-report.md) · [October readiness decision](../../../../../_bmad-output/planning-artifacts/implementation-readiness.md) · [25 November change proposal](../../../../../_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-25.md)

## Overall verdict

The PRD still supports continued corrective work. All 13 round-3 findings are resolved in the reviewed bytes. SM11 now credits no loss class without a current production-path result and agrees word for word with the AD-12 rule. FR26-C4 names the observed Admin correlation defect. FR4-C3 binds Story 2.18's producer-bound ETag and matching-tag 304 rule. §11.4 names the 15 inputs of the readiness request and never feeds G-HIGH-RISK, G-READINESS or `READY` back into it. The two requirement inventories match (56/56 definitions, 49/49 clause rows). The main remaining risk is the gap round-3 finding M5 exposed, now visible in three other November 20 audit outcomes. Query-failure redaction (Story 2.19), Admin storage scope (Story 5.15) and stream-activity counts (Story 7.23) are written into §6 under clause-partitioned parents (FR4, FR26, FR34), but no clause row names them, so those parents can close without them.

The other three lenses found the same failure mode in the story declarations and gate text: the documents can pass their own checks while the work they describe stays unowned or misdescribed. Today's FR26-C4 edit keeps the clause open until Story 5.4 binds the fix, but Story 5.4 is done and the follow-up exists only in the memlog, so nothing in the tracker or the deferred-work ledger will select it and FR26 cannot close. The G-TENANT row and OR20 still tell owners to build a gate validator that shipped on 22 November. Twenty unqualified primary NFR claims in 16 stories, plus three primary NFR17 claims outside its clause partition, break the epic Primary-ownership rule that Story 9.5 must enforce, and nothing is scheduled to resolve them before that validator fails. None of this is critical or high. The October 6 readiness rejection remains in force; that blocked state is expected and is not a finding.

Where the fixes land. PRD text: M2 (with mirrored epic ledger rows), M3, L1, L2 and L3. epics.md, tracker or deferred-work: M1 (deferred-work entry first, then PRD and epic pointers), M4 and M5 (with §11.2 companions), L4, L5, L6 and L7. Architecture route (bmad-architecture): only the AD-26-impact memlog line in L2; the stale readiness row at `architecture.md:586` was already routed and is not counted.

## Rubric judgments

| Dimension | Judgment | Basis |
| --- | --- | --- |
| Decision-readiness | strong | The stop decision, the 17-row gate contract and the ordered publication decisions are explicit. §11.4 now names the 15 inputs of the readiness request, and OR4, OR21 and OR30 assign open decisions to named owners. |
| Substance over theater | strong | Requirements bind observable behavior to concrete bounds, such as the 16-entry/2,048-byte SignalR limit, the 60-second clock skew and the typed 501 after authentication, authorization and validation. |
| Strategic coherence | strong | SM8–SM12 measure the reuse-plus-hardening thesis, each with a counter-metric. SM11 now credits no class without a current production-path run, but it has lost its stated target (L1). |
| Done-ness clarity | adequate | FR4-C3 and FR26-C4 are now testable. Three November 20 outcomes have precise §6 text but no clause that makes their parent's closure depend on them (M2). |
| Scope honesty | strong | The MVP aggregate, nine exclusions and the single owner-approved reduction are explicit, Epic 8 and G5 sit outside the MVP, and open items are owned OR rows. |
| Downstream usability | adequate (round 3: thin) | Both ledgers agree on all 49 rows, and the FR7, FR5 and FR36 owners declare their clauses. An open clause has no schedulable work (M1), and the traceability lens adds two ownership mediums and three lows in this dimension. |
| Shape fit | adequate | Journeys, clause tables, a gate contract and an owed-refinement register fit a brownfield platform. Readiness history stays inline under OR9 to protect machine-read anchors. |

## Findings by severity

### Critical (0)

None.

### High (0)

None.

### Medium (5)

**M1 · FR26-C4's open correlation fix has no schedulable owner** · PRD FR26-C4 at line 452; epics Story 5.4 reconciliation at 4535 and ledger at 356; sprint tracker line 208; spec 5.4 line 5; `.memlog.md:74`

Source · Product acceptance (medium) and PRD quality rubric N3 (low), merged at the higher severity. Where · deferred-work ledger first (where work is selected), then a pointer in PRD FR26-C4 and the epics Story 5.4 reconciliation.

Today's edit keeps the clause open "until Story 5.4 binds the AD-32 host fix and a host-level regression". Story 5.4 is `done` in the tracker and the spec, the epic paragraph that assigns the fix opens with "Story 5.4 is done", and the owner kept it done on purpose. The update run routed the fix as "a bounded FR26-C4 follow-up", but that follow-up exists only in the memlog: `deferred-work.md` has no entry for the Admin host middleware (DW-543 covers the gateway predicate and is done) and the tracker has no key. Work selection reads the tracker and the ledger, so no loop will ever pick up the fix, and FR26, an MVP requirement, has no path to closure.

The defect is live. The host middleware accepts the header only when `Guid.TryParse` succeeds and otherwise mints `Guid.NewGuid()` (lines 15–20), and `Program.cs:54` registers it. The existing host regression pins the defect instead of guarding against it: `MiddlewareOrderTests.cs:41` asserts that the response header parses as a GUID, and it passed in this round.

**Correction.** Record one bounded follow-up in the deferred-work ledger: the Admin host middleware fix under the AD-32 grammar plus a replacement for the GUID assertion at `MiddlewareOrderTests.cs:41`. Point FR26-C4 and the Story 5.4 reconciliation to that entry in place of "Story 5.4 binds", and keep Story 5.4 done. [Admin correlation middleware](../../../../../src/Hexalith.EventStore.Admin.Server.Host/Middleware/CorrelationIdMiddleware.cs) · [GUID assertion](../../../../../tests/Hexalith.EventStore.Admin.Server.Host.Tests/MiddlewareOrderTests.cs) · [Deferred-work ledger](../../../../../_bmad-output/implementation-artifacts/deferred-work.md) · [Sprint tracker](../../../../../_bmad-output/implementation-artifacts/sprint-status.yaml)

**M2 · Three November 20 audit outcomes are bound to no clause, so FR4, FR26 and FR34 can close without them** · PRD §6.1 line 279 (Story 2.19), §6.5 line 343 (Story 5.15) and §6.7 line 366 (Story 7.23); §7.1 line 427 and G-CLAUSE line 742; epics 2264, 5129 and 7089; ledgers at epics 331–380

Source · PRD quality rubric (N1). Where · PRD text (§7.1 clause rows), with the mirrored epic ledger rows as the companion edit.

FR4, FR26 and FR34 are clause-partitioned. A parent requirement "closes only when every listed clause passes" (PRD:427), and G-CLAUSE checks only the clause rows. Stories 2.19 (query-failure redaction, "Supports FR4 and NFR12"), 5.15 (Admin `all`-tenant storage scope, "Supports FR26, NFR2 and NFR16") and 7.23 (stream-head and lifetime counts, "Supports FR34 and NFR16") appear in no clause row of either ledger; the rubric reviewer checked this mechanically.

The November 20 proposal bound them only as supporting declarations on unpartitioned NFRs, the primary owners' gates (G-COMPAT for NFR12, for example) do not test these behaviors, and G-MVP-COVERAGE enumerates FR/NFR IDs and clauses, not §6 paragraphs (PRD:752). No gate can withhold closure for these approved outcomes. This is exactly the M5 defect from round 3. Today's edit fixed it for Story 2.18 and stopped there.

**Correction.** As was done for 2.18 on FR4-C3, add each story as a named supporting story with a one-sentence consequence on the closest existing clause: 5.15 on FR26-C2, 7.23 on FR34-C1 and 2.19 on the FR4 clause the owner judges closest. Mirror each row in the epic ledger and each story's declaration. Alternatively, require each outcome as a mandatory MVP manifest entry, the other option round 3 offered. [November 20 audit decision](../../../../../_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-20.md) · [Clause ledger and story declarations](../../../../../_bmad-output/planning-artifacts/epics.md)

**M3 · The G-TENANT row and OR20 still list the delivered gate validator as outstanding** · PRD G-TENANT current result at line 739; OR20 at line 793

Source · Readiness and evidence. Where · PRD text.

The G-TENANT row says "The G-TENANT business validator and NFR2 story-completion adapter are outstanding", and OR20 says "Complete the G-TENANT business validator and NFR2 story-completion adapter." On 22 November, the `done` spec-9-2-tenant-gate-business-evidence delivered that adapter as `tools/phase_4_tenant_gate.py`, and the readiness verifier enrolls it (`phase_4_readiness.py:8`). The approved 25 November proposal calls it "done 22 November" (line 253) and defines the open Story 2.14 work under P-12 as the thin NFR2 completion adapter that consumes it.

A Story 2.14 or Test owner reading OR20 is told to build a component that already exists and cannot tell whether OR20's first clause is complete. What actually remains (the NFR2 adapter, Tenants evidence on shipping packages, the CI seal and the owner attestation) is not stated anywhere in the PRD.

**Correction.** In both places, state that the Story 9.2 G-TENANT gate adapter exists (link its spec) and that the NFR2 completion adapter, the shipping-package evidence, the CI seal and the owner attestation remain. [G-TENANT gate spec](../../../../../_bmad-output/implementation-artifacts/spec-9-2-tenant-gate-business-evidence.md) · [Gate adapter](../../../../../tools/phase_4_tenant_gate.py) · [Readiness verifier](../../../../../tools/phase_4_readiness.py) · [25 November proposal](../../../../../_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-25.md)

**M4 · Seven NFRs still have competing unqualified primary owners** · Epic Primary-ownership rule at epics 473; PRD §11.2 at lines 661, 663–666, 669 and 670; G-MVP-COVERAGE at PRD 752; OR27 at PRD 800

Source · Traceability. Where · epics.md story declarations, with the PRD §11.2 primary column as the companion edit.

The epic rule allows exactly one primary owner per NFR, or explicitly named disjoint slices, and says "an unqualified primary claim on a requirement another story also claims unqualified is forbidden." Twenty unqualified claims in 16 stories break it:

- **NFR7:** 4.1 (3473), 4.2 (3516), 4.4 (3622), 4.5 (3685)
- **NFR9:** 3.8 (2664), 3.11 (2832), 3.14 (2995)
- **NFR10:** 3.1 (2282), 3.7 (2608)
- **NFR11:** 3.8, 3.14
- **NFR12:** 1.17 (1204), 3.13 (2949), 3.15 (3054)
- **NFR15:** 7.4 (5864), 7.19 (6841)
- **NFR16:** 1.20 (1372), 3.10 (2776), 3.13, 3.14

§11.2 copies these claims into its primary column as "Declared" without disposing of them. G-MVP-COVERAGE and OR27 require Story 9.5 to reject duplicate ownership, and G-NFR-OWNERSHIP (PRD 751) covers only NFR12 among the seven, so nothing is scheduled to resolve the other six before Story 9.5's validator fails on them. NFR7 is the sharpest case: Story 4.5 declares "Primary FR31 and NFR7", although OR3 (PRD 830) and §11.3 (PRD 704) say its observation cannot authorize NFR7 closure and Story 4.16 owns class (c).

Today's update made this correction for FR5 and FR36 "so the Story 9.4 declaration check can pass" (memlog line 73) and skipped the NFRs. Lower-risk cases belong in the same pass: NFR6, NFR8 and NFR14 each have one unqualified claim beside named slices (Stories 1.18, 1.19 and 7.14), and Story 5.3's NFR4 "committed-secret removal" slice overlaps Story 7.6's "committed-secret exclusion" claim.

**Correction.** Name the slice each of the 20 claims delivers, as Story 4.16 does with "NFR7 class (c)", or demote the claim to supporting and move its §11.2 entry. The coverage reader compares only the union of NFRs, so its result stays the same. [Story declarations and ownership rule](../../../../../_bmad-output/planning-artifacts/epics.md) · [Coverage reader](../../../../../tools/phase_4_story_completion.py)

**M5 · NFR17 has three primary claimants outside its five-clause ledger** · PRD §7.1 lines 427 and 475–479; epic ledger 379–383; Stories 3.14 (epics 2995), 5.6 (4633) and 5.8 (4791); PRD §11.2 NFR17 row at line 671

Source · Traceability. Where · epics.md declarations of Stories 3.14, 5.6 and 5.8, with PRD §11.2 as the companion edit; PRD §7.1 and the epic ledger only if the owner keeps them as closure conditions.

Both ledgers divide NFR17 into C1–C5, owned by Stories 7.6, 7.7, 7.8, 7.9 and 7.21, with no supporting stories. Three other stories also declare primary NFR17 ownership: Story 3.14 ("primary NFR9, NFR11, NFR16, and NFR17", unqualified), Story 5.6 ("the AppHost-loaded runtime slice of FR32, NFR2, and NFR17") and Story 5.8 ("the topology-verification slice of FR32, NFR2, NFR16, and NFR17"). §11.2 lists all three as primary while noting that §7.1 assigns C1–C5 to the Epic 7 stories.

Story 9.4 AC1 checks only that each clause owner declares its clause, so it passes while two answers remain. Either the 5.6 and 5.8 slices are additional closure conditions, missing from the rule that "a parent requirement closes only when every listed clause passes", or their primary claims carry no authority. This is the M2 pattern on the primary side: stories outside the clause partition of a clause-partitioned parent.

**Correction.** Change the three NFR17 claims to supporting, naming the clause each one serves (for example, 3.14 → NFR17-C4), and move them to §11.2's supporting column. If the owner wants them to remain closure conditions, add them as named supporting stories on the relevant clause rows in both ledgers, as was done for FR4-C3 today. [NFR17 ledger and story declarations](../../../../../_bmad-output/planning-artifacts/epics.md)

### Low (7)

**L1 · SM11 no longer states its target** · PRD §10 line 590; product-bet preamble line 585

Source · PRD quality rubric (N2). Where · PRD text.

The preamble says each product-bet metric "is a count with a target, so it can fail". SM8, SM9, SM10 and SM12 each state a target. The P-10 rewrite removed SM11's "toward the target of five" (sprint-change-proposal-2026-11-25.md:346–347) and did not restate it, so SM11 reports current status but no pass condition.

**Correction.** Add "Target: all five NFR7 classes credited (5/5); current count 0." to SM11 without changing the approved P-10 wording. [Approved P-10 text](../../../../../_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-25.md)

**L2 · OR14 does not record that the AD-26 baseline disposition was approved** · PRD OR14 at line 783, §11.3 architecture row at 701 and G-BASELINE at 737; epics 8121 (Story 9.3 dependencies)

Source · Readiness and evidence. Where · PRD text (OR14 or the §11.3 architecture row), with the epics Story 9.3 dependencies as companion; architecture route (bmad-architecture) for the missing AD-26-impact memlog line.

Decision D3 in the 25 November proposal (line 484) is the disposition record. It binds AD-26 as an unratified `[ASSUMPTION]` at subject `5070ebdcf50d9fe7a7013be67d5c872e60563acf2ca4031382d0123754080803` for G-BASELINE only, lapses if that digest changes, and is consumed by the Story 9.3 manifest. The PRD and Story 9.3 still describe a disposition as something to "obtain". §11.3, the register that exists to record owner decisions, lists it as outstanding. A 9.3 implementer must find the record in the proposal unaided. The recomputed subject is still `5070ebdc…`, so the disposition is current.

Architecture route: commit `f6e5698e5` (DW-163) changed one AD-18 paragraph after the S1 closure, moving the spine from `caa4d4c2…` to `323ea5c9…` with no AD-26-impact line in the architecture memlog. The closure note therefore binds superseded bytes.

**Correction.** Add one sentence to OR14 or the §11.3 architecture row: "On 2026-11-25 the owner approved the AD-26 baseline disposition at subject `5070ebdc…` for G-BASELINE only (proposal §7.1 D3); it lapses if that digest changes." Mirror it in the Story 9.3 dependencies. Route the one-line AD-26-impact memlog entry for `f6e5698e5` to bmad-architecture before S5. [25 November proposal](../../../../../_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-25.md) · [Architecture](../../../../../_bmad-output/planning-artifacts/architecture.md)

**L3 · G-TENANT and Story 2.14 omit the platform-path refusals that UJ2 and §8.2 now require** · PRD G-TENANT evidence column at line 739; epics Story 2.14 platform criterion at 2134–2137; compare UJ2 at 199, §8.2 at 500 and spec 2.14 line 40

Source · Product acceptance. Where · PRD text (G-TENANT evidence column), with the epics Story 2.14 criterion as companion; can share one edit with M3.

The corrected journey and constraint require a cataloged platform operation to pass its declared permission check and reject any request tenant. The G-TENANT evidence column asks only for "a distinct authenticated platform scope", and Story 2.14's criterion asks only that "no path synthesizes or forwards `system`". A reviewer could therefore accept G-TENANT without the mixed-scope and missing-permission negatives. The impact is limited to closure evidence: the shipped filter enforces both refusals, and the platform `tenant-header` and `missing-permission` tests cover them.

**Correction.** Add to the G-TENANT evidence column and Story 2.14's platform-operation criterion: "a supplied tenant or mixed scope and a missing declared permission are refused before downstream work". [Spec 2.14 scope cases](../../../../../_bmad-output/implementation-artifacts/spec-2-14-canonical-tenant-boundary.md) · [Scope preflight tests](../../../../../tests/Hexalith.EventStore.Server.Tests/Routing/GeneratedCatalogScopePreflightTests.cs) · [Platform model tests](../../../../../tests/Hexalith.EventStore.Server.Tests/Routing/PlatformCommandModelValidationTests.cs)

**L4 · Story 5.4's correlation criterion does not state the AD-32 grammar that FR26-C4 now names** · Epics Story 5.4 at 4554–4557; PRD FR26-C4 at 452; architecture AD-32 at 410–414

Source · Product acceptance. Where · epics.md (Story 5.4 acceptance criterion); AD-32 is the reference and needs no change.

The criterion preserves "an accepted bounded non-whitespace correlation token under the documented compatibility rule" and lists "GUID-only legacy input" among the inputs otherwise replaced. AD-32 accepts 1–128 ASCII alphanumeric or hyphen characters, preserves valid values downstream including ULIDs, and never validates correlation as a ULID or GUID. GUID text is therefore valid, and "non-whitespace" admits characters AD-32 rejects. The gateway middleware already preserves such values through `CorrelationIdGrammar`. Whoever implements the unscheduled M1 fix could introduce a third grammar.

The rubric reviewer read the same criterion as following AD-32. Two lenses reading one criterion two ways is the ambiguity. The PRD side of the same drift ("ULID-safe" in FR26 and FR26-C4) is kept as a mechanical note under Verification.

**Correction.** Restate the criterion: preserve exactly one 1–128-character ASCII alphanumeric-or-hyphen value, including ULID and GUID text, using `CorrelationIdGrammar`; replace blank, duplicated, oversized or malformed input with a ULID-safe value. [Correlation grammar](../../../../../src/Hexalith.EventStore.Contracts/Identity/CorrelationIdGrammar.cs) · [Gateway correlation middleware](../../../../../src/Hexalith.EventStore/Middleware/CorrelationIdMiddleware.cs) · [AD-32](../../../../../_bmad-output/planning-artifacts/architecture.md)

**L5 · Sixteen clause owners declare their clause only in prose** · Story 9.4 AC1 at epics 8177; Stories 2.2 (1525), 5.1–5.4 (4369, 4417, 4471, 4527), 6.1–6.4 (5152, 5217, 5280, 5350) and 7.1–7.7 (5678, 5743, 5801, 5864, 5927, 5990, 6077)

Source · Traceability. Where · epics.md.

Thirty-three of the 49 clause owners name their clause ID. The other 16 (FR12-C1, FR26-C1 to C4, FR33-C1 to C4 and FR34-C1 to C7) use slice prose under the parent requirement. The prose satisfies the Primary-ownership rule, but a literal Story 9.4 check that each owner "declares the same clause" fails all 16, which undercuts the reason today's run gave for adding the FR5 and FR36 identifiers.

Story 2.2's prose ("generator discovery, controller-emission, gateway-delegation, and query-metadata-header slice") also omits three parts of FR12-C1: diagnostics, `304` and safe Problem Details. Its acceptance criteria cover `304` and Problem Details but not generator diagnostics, which only `RestApiDiagnosticTests.*.cs` test.

**Correction.** Append each clause ID in parentheses to these 16 declarations, as was done for FR5-C1–C3 and FR36-C1/C2. [Story declarations](../../../../../_bmad-output/planning-artifacts/epics.md)

**L6 · UX-DR42 has no primary story owner** · Epics 287 (definition), 399, 441 and 473 (rule); support-only declarations at 800, 1525, 1575, 1671, 1722 and 1864

Source · Traceability. Where · epics.md (epics owner).

UX-DR1 to UX-DR41 each have exactly one primary story. UX-DR42, which covers the Sample accepted-submission and Tenants projection-confirmed UI flows, appears only in supporting declarations, including Story 1.8's "Supports primary FR9, NFR14, and UX-DR42". This breaks the epic rule only; no PRD requirement depends on it.

**Correction.** Name one primary story for UX-DR42 in that story's declaration. [UX-DR42 and declarations](../../../../../_bmad-output/planning-artifacts/epics.md)

**L7 · Twelve detailed stories report a lifecycle state that the tracker contradicts** · The latest 'Current reconciliation' paragraph in each story compared with the sprint tracker

Source · Traceability. Where · epics.md; the tracker is the authority, and G-BASELINE and OR15 assign the guard to Story 9.3.

The epic paragraphs say otherwise for these stories (epic state → tracker state):

- 2.13 (2081) Backlog → done; 2.16 (2220) Backlog → done; 2.17 (2246) Backlog → done
- 3.19 (3307) Backlog → in-progress; 5.5 (4583) backlog → in-progress
- 5.12 (5034) In-progress → done; 5.13 (5080) Backlog → done
- 8.3 (7265) in progress → done; 8.4 (7338) backlog → done; 8.5 (7416) backlog → done
- 8.6 (7494) Backlog → in-progress; 8.12 (7954) Backlog → done

The PRD and the epic overviews already record the current state of 2.13, 2.16, 2.17 and 3.19, yet the detailed sections still tell a story owner otherwise. The paragraphs for 2.13, 5.5, 8.4, 8.5 and 8.6 are undated.

**Correction.** Replace each label with the tracker state, or add a dated pointer to the tracker. Start with 2.13, 2.16, 2.17 and 3.19, whose states the PRD already records. [Sprint tracker](../../../../../_bmad-output/implementation-artifacts/sprint-status.yaml) · [Detailed stories](../../../../../_bmad-output/planning-artifacts/epics.md)

## Disposition of all 13 prior findings

Today's update run applied the H1, M4 and M5 edits, removed an H3 residue from the retired OR3 note and corrected the Epic 3 overview (L1) as an epics companion. Commit `8627d32cb` (21 November) fixed H2, H4, M1, H5, M2 and H6. Commit `e2cb52894` (P-10) fixed H3. The architecture fixed M3 and L2.

| Round-3 ID | Prior finding | Disposition | Fixed by | Evidence and boundary |
| --- | --- | --- | --- | --- |
| H1 | The first readiness seal has no defined inputs | Resolved | Today's update run | §11.4 (PRD 729) names a readiness request whose gate inputs are the 15 results, never a G-HIGH-RISK, G-READINESS or `READY` record, and links the frozen readiness-decision spec. `phase_4_readiness.py`, both matrix rows, OR1 and the G-READINESS row agree; the sealed-order and inventory fixtures passed from no prior readiness seal. Routed residue, not counted: `architecture.md:586` and the spec's implementation note. |
| H2 | Story 5.3 permits a Production configuration the PRD rejects | Resolved | `8627d32cb` | Epic Story 5.3 (4490–4498) fails Production startup with either flag value and exercises the override in Staging. This matches NFR3, the glossary, frozen spec 5.3 and the shared validator; the gateway regressions for all three cases pass. |
| H3 | SM11 credits staged-state recovery without production-path evidence | Resolved | `e2cb52894` (P-10); OR3-note residue today | SM11 (PRD 590) credits no class without a current production-path result, matching AD-12 (architecture 242). The retired OR3 note no longer calls class (a) "confirmed guarded", and no "three of five" count remains. Residue: SM11 lost its target (new L1). |
| H4 | An accepted command can be reported to the caller as rejected | Resolved | `8627d32cb` | "Safe rejection" is gone. UJ2, FR12, FR12-C3, §8.2 and Story 2.15 keep HTTP 202 and `Retry-After: 1` and ignore caller-supplied versions; the generator agrees and its 71 status tests pass. |
| H5 | FR7 clause owners disagree with the detailed stories | Resolved | `8627d32cb` | Stories 1.17, 1.6, 1.15, 1.18 and 1.19 declare FR7-C1 to C5 as primary, and Story 9.4 AC1 checks all five. Residue: Story 1.17's unqualified NFR12 claim sits inside new M4. |
| H6 | Mandatory corrective stories still require retired authorization tooling | Resolved | `8627d32cb` | The dependencies of Stories 3.19, 3.20, 5.13, 7.21 and 9.2–9.5 cite the owning spec under AD-12. Story 9.1 is marked superseded, and the architecture calls its machinery historical. |
| M1 | The tenant instruction omits cataloged platform operations | Resolved | `8627d32cb` | UJ2 and §8.2 limit the one-tenant rule to tenant-scoped routes and describe the platform path, matching NFR2, FR12-C2, spec 2.14 and the shipped filter. The gate evidence text is new finding L3. |
| M2 | Story 9.4 compares FR15 against a ledger with no FR15 rows | Resolved | `8627d32cb` | Story 9.4 AC1 checks FR15's six slices against §11.1 and the epic FR15 completion rule; each of the six slice owners declares its slice as primary. |
| M3 | The architecture still prescribes a check that blocks main | Resolved | Architecture | `architecture.md:244` and `:586` bind a seal to one guarded transition and never to `main`; the readiness workflow is dispatch-only. |
| M4 | The Admin correlation-ID handoff omits an observed failing case | Resolved | Today's update run | FR26-C4 (PRD 452), the Story 5.4 reconciliation (epics 4535) and architecture 602 record the `Guid.TryParse` defect, confirmed in code. The missing schedulable vehicle is new finding M1. |
| M5 | The FR4 closure check omits conditional-query evidence | Resolved | Today's update run | FR4-C3 (PRD 433) adds supporting Story 2.18 with the producer-bound ETag and matching-tag 304 rule; the epic ledger and Story 2.18's declaration mirror it. The same gap remains for Stories 2.19, 5.15 and 7.23 (new M2). |
| L1 | Epic 3's overview sends an active qualification story back to backlog | Resolved | Today's update run (epics companion) | The Epic 3 story set (epics 406) shows 3.19 and 3.21 in progress and links the 3.21 spec, matching the tracker. Residue: Story 3.19's detailed paragraph still says Backlog (new L7). |
| L2 | The architecture still lists all OQ8 hosted evidence as pending | Resolved | Architecture | AD-25 (architecture 355) and the status row (603) cite run [44892480313](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/44892480313) on `df5dc226a` and require a new hosted run for each changed candidate. |

Counts: 13 resolved, 0 partially resolved, 0 open.

## Verification

- **Identity.** Every reviewer recomputed both digests before reviewing, and the synthesis recomputed them again; both still match at HEAD `517fedfe1`. The working tree differs from HEAD in `prd.md` (five hunks: two `source_artifacts` entries, FR4-C3, FR26-C4, the §11.4 final assessment and the retired OR3 note), `epics.md` (9 lines replaced), `.memlog.md` and the Story 4.16 spec. `git diff --check` on both files exited 0.
- **Inventories.** All 56 FR/NFR definitions are identical in the PRD and epics. All 49 clause rows have identical primary and supporting owners, and all 49 epic evidence cells are `pending`. The 37 §11.1 rows and 19 §11.2 rows (78 primary story–NFR pairs) agree with the story declarations, and all 19 named supporting roles appear in the supporting stories' own declarations.
- **Coverage reader.** `tools/phase_4_story_completion.py`, imported read-only with `python3 -B -I`, accepted 143 of 146 story headings. The exceptions match round 3: Stories 3.22 and 3.23 (`COVERAGE_MISMATCH`) and 4.8 (`COVERAGE_AMBIGUOUS`).
- **Tests.** The readiness lens ran 289 fixture tests, all passing: gate risk matrix 147, gate decision 19, OQ8 governing design 111, readiness transition 10 and readiness schema 2. The acceptance lens ran 142 prebuilt Release tests in place, all passing: 71 generator command-status, 63 Server authentication and scope, and 8 Admin host. The Admin host count includes the GUID assertion at `MiddlewareOrderTests.cs:41` that pins the FR26-C4 defect.
- **Live gates.** The gate risk-matrix validator exited 1 on PRD `8ba4e8c0…`. It reported 17 rows; the 5 standard controls passed classification, and all 12 high-risk rows failed with `SEAL_MISSING`, `ATTESTATION_MISSING` and `ACHIEVED_BELOW_REQUIRED`. Seven also reported `BINDING_UNBOUND`: APPEND, COMPAT, AUTH-HOSTS, RUNTIME-PARITY, PUBLICATION-AUTH, CONSUMER and MVP-COVERAGE. The publication-authority evaluator exited 1 with `unproven` authority and a null seal. The offline OQ8 validator exited 0 with design `1a55b030…`. The Story 2.14 completion guard exited 1 with `SOURCE_MISMATCH` because `prd.md` is uncommitted, which is the expected guard.
- **Readiness code.** `GATES` has 15 entries and `GATE_DECISIONS` 3 (OQ8, TENANT and STATUS-ID). The other 12 refuse with `ADAPTER_UNAVAILABLE`, which matches the epic Story 9.2 binding order.
- **Hosted OQ8.** The four cited runs (44726206620, 44775644088, 44780280952 and 44892480313) report fixtures and live evaluation successful and the assured decision skipped. The 12 most recent OQ8 runs on `main`, through run 45712925585, succeeded. These results apply only to their identified commits.
- **AD-26 subject.** Recomputed on the current architecture (`323ea5c9…`): `5070ebdc…`, unchanged. The S1 closure note binds `caa4d4c2…` (L2).
- **Links and paths.** All 40 relative PRD links (38 unique) and all 4 fragment anchors resolve. All 83 frontmatter `source_artifacts` exist. Of 21 backticked repository paths, 20 exist; `deploy/dapr/production-profile.yaml` is absent, as the glossary at PRD 217 states.
- **Tracker and licensing.** The 22 lifecycle values the PRD claims agree with the tracker; the 12 detailed epic reconciliation paragraphs in L7 do not. The Builds gitlink is `62876129`, and MediatR `15.0.0` carries the RPL terms that OR30 describes.
- **Deduplication.** The four reviewers raised 13 findings (5 medium, 8 low), which reduce to 12. Rubric N3 (low) and the acceptance medium describe the same unscheduled FR26-C4 fix and are counted once as M1. Kept separate: M3 and L3 (G-TENANT's current-result column versus its evidence column), M4 and M5 (unqualified NFR claims versus claims outside a clause partition), M2 and M5 (different stories and parents), and L4 versus the rubric's "ULID-safe" note (the epic criterion versus PRD wording).

Mechanical notes, not counted as findings:

- **Validation record (expected pending).** The frontmatter `prd_source_validation_*` fields (PRD 12–17) and the §0 lead (PRD 115) still describe the round-2 post-update review. Approved P-11 part B updates both after this round, so this is sequencing, not a finding.
- **Glossary drift.** FR26 (PRD 339) and FR26-C4 (PRD 452) say "ULID-safe" correlation, while §8.2 (PRD 498) and AD-32 say correlation is a bounded diagnostic token "never validated as a ULID or GUID". When the AD-32 fix lands, read "ULID-safe" as "accepts canonical ULIDs and other AD-32 tokens", and remove FR26-C4's status sentence in the same change; under §1 that edit reopens readiness.
- **Assumptions index.** The PRD has 0 inline `[ASSUMPTION]` tags; the mentions at PRD 783 and 835 are not tags, so the index's "none" is accurate.
- **Input digests.** `epics.md:18` still pins PRD input digest `b99effdb…`, as PRD 700 states. All five epic input digests differ from the current sources; Story 9.3 and G-BASELINE own that reconciliation.
- **Routed residues, not counted.** `architecture.md:586` still says the readiness inputs need an owner-defined contract (routed to bmad-architecture). The readiness-decision spec's implementation note (line 53) reports two enrolled consumers where the code enrolls three.
- **Observation.** The Admin host option tests have no Staging positive case. The host delegates to the shared validator and NFR3 assigns per-host negatives to Story 5.11 under G-AUTH-HOSTS, so the PRD makes no false claim.

## Reviewer files

- [PRD quality rubric](review-rubric.md) · Seven dimensions; dispositions of H3, M4 and M5; the unbound November 20 outcomes, the SM11 target and FR26-C4 scheduling.
- [Readiness and evidence](review-readiness.md) · Dispositions of H1, H6, M3 and L2; the stale G-TENANT and OR20 text, the AD-26 disposition record, live gates, OQ8 runs and licensing.
- [Product acceptance](review-acceptance.md) · Dispositions of H2, H4 and M1; FR26-C4 ownership, G-TENANT platform refusals, the Story 5.4 correlation grammar and 142 shipped-behavior tests.
- [Traceability](review-traceability.md) · Dispositions of H5, M2 and L1; NFR and NFR17 primary ownership, prose-only clause declarations, UX-DR42 and lifecycle labels.

[Rubric](../../../../../.claude/skills/bmad-prd/assets/prd-validation-checklist.md) · Generated 2026-11-25T23:52:45+01:00
