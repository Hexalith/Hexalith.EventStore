# PRD Quality Review — EventStore Phase 4 (round 4, rubric lens)

Reviewed 2026-11-25 on the working tree of EventStore `main` at `517fedfe1357e2157e17d1d8621637a31cd0e961`. PRD SHA-256 `8ba4e8c044653fbcb17230d4886c9bef256f37ef81acc3cbb3bd27218fb20923` (835 lines). Epic plan SHA-256 `3297793cbc74cb9e4ec42c98fba5d87385c90ae1565ae2c2f2f43641dc7a3aee` (8,235 lines). Both hashes were recomputed before review. Lens: [PRD quality rubric](../../../../../.claude/skills/bmad-prd/assets/prd-validation-checklist.md). No addendum exists. Read-only review; no repository file other than this one was written.

## Overall verdict

The PRD still supports continued corrective work. All three round-3 findings assigned to this lens are resolved in the reviewed bytes. SM11 now credits no loss class without a current production-path result and agrees word for word with the AD-12 rule. FR26-C4 names the observed Admin correlation defect. FR4-C3 binds Story 2.18's producer-bound ETag and matching-tag 304 rule. The two requirement inventories match (56/56 definitions, 49/49 clause rows). The main remaining risk is the same gap that M5 exposed, now visible in three other November 20 audit outcomes. The query-failure redaction (Story 2.19), Admin storage scope (Story 5.15) and stream-activity counts (Story 7.23) are written into §6 under clause-partitioned parents (FR4, FR26, FR34), but no clause row names them, so those parents can close without them. Two low findings concern SM11's dropped target statement and the absence of any scheduled vehicle for the FR26-C4 fix. The October 6 readiness rejection remains in force; that blocked state is expected and is not a finding.

## Disposition of round-3 findings in this lens

| Round-3 finding | Disposition | Current evidence |
| --- | --- | --- |
| H3 · SM11 credits staged-state recovery without production-path evidence | **Resolved** | PRD:590 now reads "No class currently has the production-path run, source revision, operating profile and result that AD-12 requires for credit. Stories 5.1 (a), 4.2 (b) and 4.4 (d) hold historical guards. Each needs a current production-path result before it counts." This matches the AD-12 rule at architecture.md:242 and the approved P-10 text (sprint-change-proposal-2026-11-25.md:347). The retired OR3 note at PRD:830 no longer says class (a) is "confirmed guarded"; it now says "SM11 does not credit until a current production-path result exists". A search of the PRD and epics finds no remaining class (a), (b) or (d) credit and no "three of five" count. The only SM11 reference in the epics (epics.md:4275) is Story 4.16's class (c) ownership. One residual wording gap is reported separately as N2 (low). |
| M4 · The Admin correlation-ID handoff omits an observed failing case | **Resolved** | PRD:452 (FR26-C4) now states "The registered Admin Server Host correlation middleware still uses `Guid.TryParse` and replaces a valid ULID, so this clause stays open until Story 5.4 binds the AD-32 host fix and a host-level regression." The epic Story 5.4 reconciliation at epics.md:4535 and the architecture status row at architecture.md:602 say the same, and Story 5.4 stays `done` (sprint-status.yaml:208). Verified in code: `src/Hexalith.EventStore.Admin.Server.Host/Program.cs:54` registers the host-local `Middleware/CorrelationIdMiddleware.cs`. Lines 15–20 of that file accept the header only when `Guid.TryParse` succeeds and otherwise assign `Guid.NewGuid()`, so a canonical ULID is replaced. `tests/Hexalith.EventStore.Admin.Server.Host.Tests/MiddlewareOrderTests.cs:41` currently asserts that the header parses as a GUID. The acceptance target is explicit at epics.md:4554–4557. All three parts of the round-3 correction are met in the planning text: the mismatch is recorded, the story result is preserved and the fix is assigned. A gap in scheduling the fix is reported separately as N3 (low). |
| M5 · The FR4 closure check omits conditional-query evidence | **Resolved** | PRD:433 (FR4-C3) keeps primary 1.2, adds supporting 2.18 and adds "An ETag is emitted only when its producer bound it to the returned representation, never from a later lookup, and only a matching producer-bound tag can yield HTTP 304 (§6.1)". That sentence agrees with §6.1 at PRD:277. The epic ledger row at epics.md:337 mirrors `1.2 \| 2.7, 2.11, 2.18`. Story 2.18 now declares "Supports FR4-C3" (epics.md:2252). Its acceptance criteria cover the stale-tag negative and the matching-tag positive (epics.md:2256–2257). Clause closure therefore depends on both cases. The same pattern remains for three other November 20 stories; see N1. |

## Decision-readiness — strong

The stop decision (PRD:115, §11.4), the 17-row gate contract and the ordered publication decisions (PRD:727) remain explicit. The final-assessment paragraph now identifies the inputs to the readiness request: "gate inputs are those 15 results, never a G-HIGH-RISK, G-READINESS or `READY` record" (PRD:729). It links the Story 9.2 readiness-decision spec. Trade-offs are named together with what each one gives up. OR4 (PRD:771) records the owner's choice of capability path A with its Dapr 1.20 release-candidate trigger and states that risk acceptance cannot satisfy NFR7. OR21 and OR30 assign the gateway GET and MediatR license decisions to named owners rather than resolving them silently. The §0 lead still describes the round-2 review. The approved P-11 step will update it after this validation, so it is listed only in the mechanical notes.

## Substance over theater — strong

Requirements describe observable behavior with concrete bounds. Examples are the 16-entry/2,048-byte SignalR limit (NFR5), the 60-second clock skew (NFR3), the two-durable-effect redelivery case (PRD:329) and the typed 501 only after authentication, authorization and validation (NFR15). Personas are limited to five named UJ protagonists, and each UJ lists the requirements it drives. §5 restates §6 and §7, and OR9 (PRD:821) already acknowledges that, so it is not raised again.

## Strategic coherence — strong

The §2 thesis that reuse and hardening must ship together is measured by SM8–SM12 (PRD:585–591), with a counter-metric for each (SM-C1–SM-C5). SM11 now measures the safety thesis honestly: no historical guard counts until a current production-path run exists, and SM-C5 prevents a loss class from being retired by redefinition.

### Findings
- **low** SM11 no longer states its target (§10, PRD:590). See N2. *Fix:* restore the target phrase.

## Done-ness clarity — adequate

Stable clauses give most FRs a testable closure condition. Today's FR4-C3 and FR26-C4 edits make the conditional-query and correlation cases testable. Three November 20 audit outcomes have precise §6 acceptance text but no clause that makes their parent's closure depend on them.

### Findings
- **medium** The query-failure, Admin storage-scope and stream-activity outcomes cannot block the closure of their parent requirements (§6.1 PRD:279, §6.5 PRD:343, §6.7 PRD:366; §7.1 PRD:427). See N1. *Fix:* name each story as a supporting story on an existing clause, as was done for 2.18.

## Scope honesty — strong

§9.1 states that the MVP is total over FR1–FR36, NFR1–NFR18 and every §7.1 clause. §9.2 names nine out-of-scope items and the single owner-approved reduction (interactive UI hosts, PRD:553), and §13 indexes it. Epic 8 and G5 sit outside the MVP aggregate (PRD:755–759). Story 4.16's safety boundary (PRD:532) separates approved scope from unimplemented capability. The PRD has no inline `[ASSUMPTION]` or `[NOTE FOR PM]` tags. Open items are tracked as OR rows with owners and triggers, which suits a blocked recovery PRD.

## Downstream usability — adequate

This dimension improves from round 3's *thin*. The PRD and epic clause ledgers agree on all 49 rows. Stories 1.15, 1.17, 1.18 and 1.19 now declare their FR7 clauses. Stories 1.3, 1.14, 1.15, 1.20 and 3.15 name their FR5 and FR36 clauses. No epic dependency still requires a "Story 9.1 authorization record". The remaining extraction hazards are N1, where clause-ledger readers miss three approved outcomes, and N3, where a reader of the tracker finds no scheduled work for an open clause.

### Findings
- **low** The FR26-C4 fix is assigned to a story recorded as done and has no scheduled follow-up (§7.1 PRD:452; epics.md:4535). See N3. *Fix:* record the follow-up in the existing deferred-work ledger and cite it.

## Shape fit — adequate

Named journeys, a stable-clause table, a gate contract and an owed-refinement register fit a brownfield platform PRD with separate developer, operator, release and consumer roles. The document still carries a lot of readiness history inline. OR9 explicitly defers that restructuring to protect machine-read anchors, so it is not raised again.

## New findings

**N1 · medium · Three November 20 audit outcomes are not bound to any clause, so their parent requirements can close without them**
- *Location:* PRD §6.1 line 279 (Story 2.19, under FR4), §6.5 line 343 (Story 5.15, under FR26) and §6.7 line 366 (Story 7.23, under FR34); §7.1 line 427 and G-CLAUSE line 742; epics.md:2264, 5129 and 7089; ledgers at epics.md:331–380.
- *What is wrong:* FR4, FR26 and FR34 are clause-partitioned. A parent requirement "closes only when every listed clause passes" (PRD:427), and G-CLAUSE checks only the clause rows. Stories 2.19 ("Supports FR4 and NFR12"), 5.15 ("Supports FR26, NFR2 and NFR16") and 7.23 ("Supports FR34 and NFR16") appear in no clause row of either ledger; this was checked mechanically. The November 20 proposal bound them only as supporting declarations on unpartitioned NFRs (sprint-change-proposal-2026-11-20.md:60), and the primary owners' gates (for example G-COMPAT for NFR12) do not test these behaviors. G-MVP-COVERAGE enumerates FR/NFR IDs and clauses, not §6 paragraphs (PRD:752). As a result, the approved query-failure privacy, `all`-tenant storage scope and stream-head/lifetime-count outcomes have no gate that can withhold closure. This is the defect M5 found for Story 2.18, which today's edit fixed for that story only.
- *Correction:* As was done for 2.18 and FR4-C3, add each story as a named supporting story with a one-sentence consequence on the closest existing clause (5.15 on FR26-C2; 7.23 on FR34-C1; 2.19 on the FR4 clause the owner judges closest). Alternatively, require each outcome as a mandatory MVP manifest entry, which round 3 offered as the other option.

**N2 · low · SM11 no longer states its target**
- *Location:* PRD §10 line 590; product-bet preamble line 585.
- *What is wrong:* The preamble says each product-bet metric "is a count with a target, so it can fail". SM8, SM9, SM10 and SM12 each state a target. The P-10 rewrite removed SM11's "toward the target of five" (sprint-change-proposal-2026-11-25.md:346–347) and did not restate it, so SM11 now gives the current status but not the pass condition.
- *Correction:* Add "Target: all five NFR7 classes credited (5/5); current count 0." to SM11 without changing the approved wording.

**N3 · low · The FR26-C4 fix has no scheduled vehicle**
- *Location:* PRD §7.1 line 452; epics.md:4535; sprint-status.yaml:208; .memlog.md:74.
- *What is wrong:* The clause stays open "until Story 5.4 binds the AD-32 host fix and a host-level regression", but Story 5.4 is `done` in the tracker, spec and epic. The owner intentionally kept it done. The memlog routes the fix "as a bounded FR26-C4 follow-up", but no entry exists in `deferred-work.md` and no tracker key exists, so the dev and sprint loops have nothing to pick up. The existing host regression (`MiddlewareOrderTests.cs:41`) also asserts the GUID behavior that must change.
- *Correction:* Record the bounded follow-up, the middleware fix plus a replacement for the GUID assertion, as one entry in the existing deferred-work ledger and cite it in the Story 5.4 reconciliation.

## Mechanical checks

- **Bytes.** `sha256sum` on both working files matched the requested PRD and epic digests. `git diff` confirmed today's PRD changes: frontmatter sources, FR4-C3, FR26-C4, the §11.4 final assessment and the OR3 note.
- **Definitions.** All 56 FR/NFR definitions (FR1–FR37, NFR1–NFR19) are identical in the PRD and epics after whitespace and quote normalization, with 0 mismatches.
- **Clause ledgers.** Both ledgers have 49 rows. The primary and supporting assignments differ in 0 rows, and all 49 epic statuses are `pending`.
- **Story declarations.** The epics contain 146 story headings; 144 have a bold `Requirements coverage` line, and 3.16 and 4.8 use other forms. 58 stories cite a clause-partitioned parent (FR4/5/7/12/26/33/34/36 or NFR17) without a clause ID; most are parent-level support. Stories 2.19, 5.15 and 7.23 appear in no ledger row (N1). Story 2.18 appears in FR4-C3 and Story 5.4 in FR26-C4.
- **IDs.** FR1–FR37 and NFR1–NFR19 are contiguous, with no references beyond them. The PRD defines SM1–SM12 and SM-C1–SM-C5, and UJ1–UJ5, each with a named protagonist (Alex, Morgan, Priya, Nora, Riley). It has 28 active OR rows; with retired OR2, OR3 and OR12, OR1–OR31 are all accounted for. §11.4 has 17 gate rows, which matches "15 business and coverage gates" plus G-HIGH-RISK and G-READINESS.
- **Links.** The PRD has 40 relative Markdown links (38 unique). All resolve except one regex fragment the scan misread as a link (NFR2 grammar). All 4 fragment anchors resolve: `epics.md#stable-clause-ledger`, the Story 6.5 approval section, the Story 4.16 1.19.2 recheck and the Story 2.15 deferred-work entry. All 83 frontmatter `source_artifacts` exist. Of 21 backticked repository paths, 20 exist; `deploy/dapr/production-profile.yaml` is absent, which matches the glossary statement at PRD:217.
- **Code and architecture.** Admin Server Host `Program.cs:54`, `Middleware/CorrelationIdMiddleware.cs:15–20` and `MiddlewareOrderTests.cs:41` were read; AD-12 (architecture.md:242) matches SM11, and AD-32 (architecture.md:452) and the status row at architecture.md:602 match FR26-C4. A search of `deferred-work.md` and `sprint-status.yaml` found no route for the Admin host fix.
- **Residue search.** "class (a)", "three of five", "4 of 5" and "SM11" across the PRD and epics found no remaining class credit.
- **Approved decisions.** sprint-change-proposal-2026-11-25.md P-10/P-11 and sprint-change-proposal-2026-11-20.md:26–60 were consulted for the approved wording and story bindings. No tests were run; this lens relies on document and code comparisons.

## Mechanical notes

- **Validation record (expected pending).** The frontmatter `prd_source_validation_*` fields (PRD:12–17) and the §0 lead (PRD:115, "The second November 20 PRD review … identified 19 findings") still describe the round-2 post-update review. The round-3 report is now listed in `source_artifacts` (PRD:25). Approved P-11 part B updates both after this round (sprint-change-proposal-2026-11-25.md:418; .memlog.md:76), so this is sequencing, not a finding.
- **Glossary drift.** FR26 (PRD:339) and FR26-C4 (PRD:452) say "ULID-safe" correlation. §8.2 (PRD:498) and AD-32 (architecture.md:452) say correlation is a bounded diagnostic token that is "never validated as a ULID or GUID". The Story 5.4 acceptance criteria (epics.md:4555) follow AD-32. When the AD-32 fix lands, read "ULID-safe" as "accepts canonical ULIDs and other AD-32 tokens", not as `Ulid.TryParse` validation. In the same change, remove FR26-C4's status sentence; under §1 that edit reopens readiness.
- **Assumptions Index roundtrip.** The PRD has 0 inline `[ASSUMPTION]` tags. The two textual mentions, at PRD:783 (the AD-26 disposition) and PRD:835 (the index statement), are not tags. The index's "none" is accurate.
- **Input digest consistency.** epics.md:18 still pins the PRD input digest `b99effdb…`, as PRD:700 states. All five epic input digests differ from the current sources; Story 9.3 and G-BASELINE own that reconciliation.
