# PRD Quality Review — eventstore Phase 4 Implementation Readiness Recovery (validate, 2026-10-07)

- **PRD:** `_bmad-output/planning-artifacts/prd.md`, SHA-256 `d5632ba71c838ba7f0b8ca61a24889cb21edb4506531e823d8c0dfb12b7e6ae6` (identical at `HEAD` `c60503c1` and in the worktree; other planning files are dirty)
- **Rubric:** `.claude/skills/bmad-prd/assets/prd-validation-checklist.md`
- **Addendum:** none
- **Method:** full read of all 770 lines; spot checks against `epics.md`, `architecture.md`, `ux.md`, the detailed UX files, the three 2026-10-07 proposals, and the repository paths the PRD cites. The prior 2026-09-10 report was read only after these judgments were formed.

## Overall verdict

The PRD now fixes almost every content gap the 2026-09-10 review raised. It has a computed exit contract (§11.4), a 49-clause acceptance ledger (§7.1), five journeys with named protagonists, NFR2, NFR3, NFR12 and FR36 contracts defined by capability rather than by a closed list, and a named primary story for every gate. Its `final`-but-`Reject` posture is coherent and well defended. The weak point is the PRD's second job as a live readiness ledger. The §11.3 identity register is stale for every artifact it binds. 18 of 56 FR/NFR texts differ from the `epics.md` inventory, and the PRD does not list them. The 2026-10-07 edits also brought in new inconsistencies:

- NFR1 adds an anonymous-endpoint class that is never listed and cites AD-16, which currently forbids it.
- The exit contract and SM10 cannot represent the UI-host carve-out.
- SM11 counts an OQ8 loss class that the PRD itself calls non-authoritative.
- OR30, the MediatR licence blocker, sits outside the gate contract.

**Grade: Fair** (up from Poor). The PRD is a trustworthy requirements source for the stop decision and for writing corrective stories. It is not yet a clean chain-top handoff.

## Decision-readiness — adequate

§0 and §11.4 make the stop decision computable rather than rhetorical: "`READY` is computed, not asserted." The table has 17 mandatory rows. Each row binds governed requirements, evidence, evaluator, current result, and invalidation, and each now names a primary owner story. Owner decisions are written as decisions, with dates:

- Story 4.16 is "owner-selected enforced-envelope path; fencing requires a separate approved scope change".
- The roster-dependent Assurance Control was adopted on 2026-09-26.
- Interactive UI hosts were excluded from the production profile on 2026-10-07.
- The Story 3.15 tracker `done` applies "for FR36-C2 only".

Trade-offs name what is given up. §9 calls the append-fencing exclusion "not a safety waiver". §2 says what each half of the bet costs without the other. There are no `[NOTE FOR PM]` callouts, but the OR register (24 blocking items, each with an owner and a trigger) does that job.

The newest decision items are weaker than the older ones. OR30 offers options without a recommendation and cannot be enforced. Two status headlines misdescribe the control that is actually blocking.

### Findings

- **high** The OR30 licence blocker sits outside the decision machinery and has an undefined trigger (§12 OR30, line 744; §11.4) — The row is labelled "**Blocking for the next release**", yet no §11.4 row lists OR30, and neither G-PUBLICATION-AUTH nor G-READINESS computes it. "Release" is also not one of the PRD's own Publication Lifecycle states (`release-available` is), and a NuGet publication by the release workflow is a different event. The row admits that the already "released `Server` and `Gateway` packages reference it" and that "the host suppresses its license log". It still decides nothing about those published versions. It lists three options with no recommendation and no trade-off: RPL-1.5 reciprocal obligations passed to consumers, against the cost of an NFR12-classified dependency replacement. *Fix:* Add OR30 to the governed blockers of G-PUBLICATION-AUTH (or G-COMPAT) so the computed aggregate sees it. Define "release" as either the package publication or `release-available`. Recommend an option and state its consequence for consumers. Say what happens to the versions already published (notice, deprecate, or accept with a documented reason).
- **medium** "INDEPENDENT" status labels contradict the roster-dependent Assurance Control (§6.8 line 342 "TECHNICALLY VALIDATED; INDEPENDENT CONTROL OPEN"; §11.4 G-RUNTIME-PARITY line 687 "TECHNICAL PASS; INDEPENDENT GATE BLOCKED") — While the registry names one human, the glossary sets the required level to `single-maintainer-attested`. What unblocks it is sealed CI plus a time-separated owner attestation (Story 9.2), not an independent approver. The two most visible status headlines tell readers the opposite of the 2026-09-26 owner decision, and invite exactly the "find a second reviewer" reading that decision superseded. *Fix:* Rename them, for example "ASSURANCE CONTROL OPEN" and "TECHNICAL PASS; ASSURANCE GATE BLOCKED". Update the guard in the same change: `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:4593` pins the G-RUNTIME-PARITY string.
- **medium** The residual risk accepted with the Assurance Control is never named (§4 Owner Roles line 200; Assurance Control line 201) — The owner "accepted its residual risk", but the PRD never says what that risk is. A reader cannot tell what `single-maintainer-attested` fails to catch: for example, a requirement the author misreads, or a validator that is green by construction. Sealed CI checks that the validator ran, not that it checks the right property, and the 24-hour separation protects against haste, not against a shared blind spot. Without that statement nobody can revisit the trade-off. *Fix:* Add one sentence that names what the control does not protect against and what partly compensates (the optional `tool-persona` adversarial review, mutation checks). Note that the control re-evaluates automatically when the registry gains a second human.
- **low** Gate evidence text keeps an alternative the owner already decided against (§11.4 G-TENANT line 680, G-STATUS-ID line 681; OR20/OR21) — G-STATUS-ID still requires an "Approved corrective or reopened Story 2.9", while the same row and OR21 name successor Story 2.15 as primary owner. The same applies to G-TENANT and Story 2.14. *Fix:* State the decision (successor story) and drop the reopen alternative, or say why it remains open.

## Substance over theater — strong

The content is earned. NFRs carry product-specific thresholds:

- NFR2's tenant grammar `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`
- NFR3's algorithm allow-list and 60-second clock skew
- NFR5's 16 entries / 2,048 bytes
- NFR8's 4,096-byte snapshot overhead
- FR26's `1_048_576` and `10 * 1024 * 1024` request limits
- NFR17's named resiliency policies

The vision makes a falsifiable bet. Each of the six roles appears in a UJ or a requirement. UJ1–UJ5 each end with a requirement list and a failure path, for example "absent proof leaves the operation unresolved rather than successful". The product-bet metrics are counts with targets that "can fail". Negative evidence is kept rather than smoothed over: the §6.1 accepted evasions DW-64/65/77–79, and "the Test Architect receipt is self-attested".

### Findings

- **low** Furniture the PRD itself acknowledges remains (§5 lines 214–226; §10 SM1 line 526, SM5 line 528) — §5 restates §6/§7, as OR9 admits. SM1 "cannot fail and does not measure Phase 4 delivery". Neither SM1 nor SM5 discriminates any longer. *Fix:* Fold §5 into the OR9 pass. Move the achieved planning-recovery metrics to a one-line history note so §10 lists only live metrics.

## Strategic coherence — adequate

The thesis is explicit in §2: reuse and hardening must ship together, and "evidence discipline" belongs to the hardening half. That covers the Epic 9 gate machinery that now dominates the remaining work. SM8–SM12 measure both halves of the bet. Each counter-metric (SM-C1–SM-C5) names the metric it counterbalances, and SM-C5 explicitly forbids reaching SM11 "by narrowing its definition". MVP, post-MVP (Epic 8/G5), and exclusions are cleanly separated.

Two things weaken the arc. One thesis metric reports progress that the PRD elsewhere declares non-authoritative. And MVP scope is still defined by the epic inventory rather than derived from the thesis, which matters more now that 17 failed gates compete for one maintainer.

### Findings

- **high** SM11 counts NFR7 class (e) as delivered on evidence the PRD calls non-authoritative (§10 SM11 line 544; §1.1 line 133; NFR7 line 367; §11.3 OQ8 row line 653; G-OQ8 line 679) — SM11 says "four of five are delivered", counting class (e) on "closed source-only platform evidence from Stories 4.9-4.15". NFR7 counts a class as delivered only when "production-path evidence proves" prevention. §1.1 says "No OQ8 closure claim under FR27, NFR7, or NFR16 is authoritative" until G-OQ8 passes. §11.3 calls Story 4.15's completion "a bounded source-only packet claim". The thesis metric therefore overstates hardening by one class, which is the drift SM-C5 exists to prevent. *Fix:* Report SM11 as three of five delivered, with class (e) shown as "implemented; not countable until G-OQ8 passes and production-path evidence is bound". Alternatively, state explicitly why the 4.9–4.15 evidence meets NFR7's production-path bar despite the "source-only" label.
- **medium** MVP scope still follows the epic inventory rather than the thesis (§9.1 lines 490–500) — This is open from 2026-09-10. §9.1 still begins "Epics 1-7 as listed in `epics.md`". With 17 failed gates and 24 blocking ORs, the PRD gives no thesis-derived order. Which outcomes are exit-critical for the bet? Which are enabling infrastructure? The 2026-10-07 work already implies an order (Story 9.1 bootstrap, gate validators, G-APPEND envelope first), but only `epics.md` carries it. *Fix:* Add a short scope-logic paragraph that groups gates by thesis leg and enabling dependency, for example: corrective-work bootstrap (9.1), then gate infrastructure (9.2–9.5), then the safety gates (G-APPEND, G-TENANT, G-STATUS-ID, G-AUTH-HOSTS), then the parity → publication → consumer chain. Do not duplicate story sequencing.
- **low** SM8 measures only the Tenants module (line 541) — FR9 and UJ1 cover Sample and Tenants adoption, but SM8 counts duplicated plumbing in Tenants alone. *Fix:* Include Sample in the count or state why it is excluded.

## Done-ness clarity — adequate

This is the most improved dimension. The §7.1 ledger gives 49 stable clauses for FR4, FR5, FR7, FR12, FR26, FR33, FR34, FR36 and NFR17. Each clause has an observable consequence and a primary slice, under an explicit rule: "A `done` label, ownership declaration, smoke status, or evidence for another clause never substitutes". G-MVP-COVERAGE forbids `N/A` and unsupported `done`. Most single-clause FRs carry a directly testable consequence (FR3's endpoint list, FR18, FR20, FR21's no-local-`PackageVersion` rule). NFR8 is honest about its missing projection bound and fails closed.

The remaining softness is concentrated in three places: the newest NFR1 text; "bounded" used without a bound; and four gates that cannot yet be evaluated mechanically.

### Findings

- **high** NFR1's new anonymous-endpoint class is never listed, and it cites a decision that forbids it (§7 NFR1 line 361; §11.2 NFR1 row line 609; `architecture.md` AD-16 line 244) — NFR1 now admits "an enumerated set of static framework assets and authentication-protocol callback endpoints … enumerated by endpoint-metadata tests (AD-16)". Neither the PRD nor AD-16 lists that set. AD-16 still reads: "Only `/health`, `/alive`, and `/ready` carry explicit `AllowAnonymous` metadata … Any future anonymous asset or callback first requires a governed PRD change." The closed list is therefore whatever the tests happen to enumerate, so the test becomes the requirement, and the cited authority contradicts the PRD. No OR tracks the AD-16 amendment: OR14's architecture task list names AD-10, AD-11 and AD-26 only. The approved proposal `sprint-change-proposal-2026-10-07-story-9-2-nfr1.md` §4 notes that the amendment "remains routed to `bmad-architecture`". "Carry no tenant, operational, or user data" is a judgment, not a check, inside a fail-closed security NFR. *Fix:* List the class in the PRD, or name the single approved artifact that holds the closed list, by endpoint kind or path pattern (for example framework static web assets, and OIDC sign-in/sign-out callback paths). Define the mechanical test for "data-free". Add the AD-16 amendment to OR14 or a new OR so that NFR1 no longer cites a contradicting decision.
- **medium** "Bounded" is used without a bound or a pointer to one (FR34 line 323 "bound in-memory deduplication"; FR34-C1 line 415 "bounded deduplication"; FR6 line 241 "payload limits"; FR17 line 270 "readiness retry"; FR30 line 290, with no recovery window; UJ4 line 172 "bounded recovery contract") — NFR5, NFR8 and FR26 either give a number or name the spec that will hold it. These give neither, so any finite value satisfies them and a story can close them by assertion. *Fix:* Give each a number, or name the owning artifact that holds it (an architecture AD or a spec), as NFR8 does for the projection bound.
- **low** Four of the 17 mandatory gates have no command that can be evaluated (G-BASELINE line 678 and G-CLAUSE line 683, "not yet defined"; G-TENANT line 680 and G-STATUS-ID line 681, "must be bound by that story") — OR17 acknowledges this, but OR17 is the only gate-infrastructure refinement without a primary story. *Fix:* Assign OR17 to the stories that own those validators (9.3, 9.4, 2.14, 2.15), and require each story to name its command before it starts.

## Scope honesty — adequate

The posture is exemplary. FR24 and FR31 are framed as evidence and planning outcomes only. "Assignment is not delivery" recurs wherever a 2026-10-07 owner was named. Each of the 24 blocking ORs has an owner and a trigger, and three non-blocking items are kept visibly separate. §13 names the one scope reduction. Open-item density (24 blocking ORs, 17 failed gates, 0 inline assumptions) suits a document that authorizes nothing.

The scope statement itself, however, now has gaps that the reader must infer.

### Findings

- **high** The exit contract cannot represent the 2026-10-07 UI-host carve-out (§9.1 line 490; §9.2 line 507; §10 SM10 line 543; §11.4 G-MVP-COVERAGE line 693; §13 line 770) — §9.2 moves interactive UI hosts' NFR1 conformance out of the MVP production profile. But §9.1 says MVP is "total over FR1-FR36, NFR1-NFR18", and G-MVP-COVERAGE says "`N/A` is forbidden for MVP IDs". SM10 targets "100% of entries in the … `nfr1_surfaces` inventory", while UI-host surfaces are "listed with that scope reason and never counted as covered". So either SM10 can never reach 100%, or its denominator silently excludes them. The Story 9.5 validator will have to invent a rule the PRD never states. *Fix:* Give the carve-out a stable clause, for example NFR1-C1 for MVP surfaces and NFR1-C2 for the interactive-UI-host class (post-MVP). Allow one reason-coded `outside-mvp-production-profile` state in G-MVP-COVERAGE for that clause only. Define SM10's denominator to exclude it explicitly.
- **medium** Epic 9 is missing from the MVP scope statement (§9.1 line 492; §11.4 owners) — §9.1 lists "Epics 1-7" and notes that `epics.md` "also lists Epic 8". The PRD never mentions Epic 9, yet `epics.md` line 389 declares it an "MVP epic, independent of Epic 8". Its Stories 9.1–9.5 own the §0 corrective-work bootstrap, G-BASELINE, G-CLAUSE, G-HIGH-RISK and G-MVP-COVERAGE. *Fix:* Add Epic 9 to §9.1 with one line on its role (gate machinery that grants no readiness, release or deployment authority).
- **medium** §13 reports no assumptions, but normative text rests on an architecture `[ASSUMPTION]` (§4 Canonical Production Profile Inventory line 188; G-PUBLICATION-AUTH line 688; G-CONSUMER line 689; §11.3 architecture row line 648) — The glossary states that "AD-26 defines exactly one currently authorizing production profile", and two gates compute against it. §11.3 says that AD-26 "remains `[ASSUMPTION]`" pending owner ratification, and `architecture.md` line 337 confirms the tag. *Fix:* Index imported assumptions in §13 (AD-26, its owner, and the ratification trigger). Phrase the glossary entry conditionally ("AD-26, once ratified, defines …").

## Downstream usability — thin

The aids for extracting from the PRD are now strong:

- 32 glossary terms, including the six-way "Parity" disambiguation, MessageId Contract Version and Assurance Control.
- Stable IDs.
- UJs that list their requirements.
- Complete §11.1 and §11.2 tables for every FR and NFR.
- All 24 recently added owner or supporting stories (2.13–2.17, 3.17–3.21, 4.16, 4.17, 5.11–5.14, 6.7, 7.21, 7.22, 9.1–9.5) exist as `### Story` headings in `epics.md`.
- Accurate presence and absence claims for cited paths. `deploy/dapr/production-profile.yaml`, the five unbuilt validators, the AOT document and the projection-cost spec are absent as stated. The Story 3.15 `closure.json`, its validator and `deploy/dapr/resiliency.yaml` exist as stated.

What breaks downstream use is the PRD's own identity data and contradictions between its sections.

### Findings

- **high** The §11.3 identity register is stale for every artifact it binds (lines 648–650 and the `ux.md` row) — At `HEAD` `c60503c1`:

  | Artifact | PRD states | Actual |
  | --- | --- | --- |
  | `architecture.md` | `7e3dbc7b…` | `7fd805a8…` |
  | `epics.md` | `d067c8fb…` | `0697679b…` |
  | `ux.md` | `2927f97d…` | `23b17217…` at HEAD, `a135c969…` in the worktree |
  | DESIGN "current" | `3f4f0181…` | `7b743e79…` at HEAD; worktree differs again |
  | EXPERIENCE "current" | `11f75403…` | `3985789a…` at HEAD; worktree differs again |

  The `architecture.md` and `ux.md` values are the digests recorded in `epics.md` (lines 15–18), relabelled as each artifact's own "SHA-256". The status claims are also wrong. §11.3 and §3.3 call `ux.md` `final`, but `HEAD` reads `Status: draft`. The uncommitted worktree reads `final` and now also marks DESIGN and EXPERIENCE `final`, contrary to "both have `status: draft`". The column header says "Identity/status at stated date", but no row states a date. §1 makes this register the PRD's mechanism for content identity, so wrong values are a false anchor. G-BASELINE's FAIL remains correct. *Fix:* Either generate the register (the Story 9.3 baseline manifest) and replace the in-PRD digests with a pointer, or date every row and separate "digest recorded in `epics.md`" from "artifact digest at `<commit>`".
- **high** The `epics.md` Requirements Inventory differs from the PRD for 18 of 56 requirement texts, and the PRD reports only digest staleness (§11.3 rows lines 647, 650; OR8; OR14) — I compared each PRD §6/§7 row with the `FRn:`/`NFRn:` lines in `epics.md`. These differ: FR5, FR12, FR15, FR16, FR26, FR33, FR34, FR36, FR37, NFR2, NFR3, NFR5, NFR7, NFR8, NFR12, NFR17, NFR18 and NFR19. Several differences are substantive weakenings:
  - The `epics.md` NFR2 is still the pre-correction one-liner ("Tenant provisioning must reject the reserved `system` tenant name"), with no canonicalizer or grammar.
  - The `epics.md` NFR5 has no 16 / 2,048 bound.
  - The `epics.md` NFR12 protects only "additive framework changes such as SignalR …".
  - The `epics.md` FR36 lacks the five-outcome structure.

  A story writer working from the `epics.md` inventory would build to superseded security and compatibility contracts. NFR1 shows propagation works when someone does it. *Fix:* List the differing IDs in §11.3 or OR14 so the reconciliation is scoped and checkable. Put the security and compatibility rows (NFR2, NFR3, NFR12, FR12, FR36) first in the OR14 sequence.
- **medium** §11.2.1 contradicts §11.2 on NFR5 ownership (line 634 vs line 613 and G-NFR-OWNERSHIP line 692) — §11.2.1 says "NFR5's primary-owner gap remains open", while §11.2 says "Primary owner Story 2.16 (assigned 2026-10-07)". *Fix:* Replace it with "NFR5's primary owner is Story 2.16; Story 2.13 supports."
- **low** Some cross-references are stale:
  - OR6 (line 722) still asks to "give NFR18 an owning story", but NFR18, §11.2 and G-NFR18 already name Story 6.7.
  - The OR2 retirement note (line 764) says `epics.md` "still describe[s] loop-3 partial delivery" for Story 5.3, but OR15 (line 725) says Story 5.3 was reconciled on 2026-10-07.
  - §0 (line 108) cites only the 2026-09-10 observations, while the frontmatter records a 2026-10-06 readiness assessment against `a6fc951e…`.

  *Fix:* Refresh these. They are exactly the drift the OR8 guard should catch.

## Shape fit — thin

Fit improved where it matters most. The UJs are load-bearing for a platform with several roles. The brownfield path references are accurate. The NFRs are defined by capability rather than by a closed host list.

The structural problem the earlier reviews flagged has grown, however. This is still three documents in one: a requirements baseline, a readiness exit contract, and a live status and evidence ledger. It is now 770 lines and 163 KB, against 583 lines in the 2026-09-10 review. Volatile state has also moved *into requirement text*:

- NFR8 carries Story 6.1's 2026-10-04 reopen and review patch P-D1.
- NFR18 carries an assignment date.
- The FR26 row in §11.1 carries Story 5.3 status.

§1 says "Readiness re-opens whenever FR or NFR text changes", and OR8's guard will diff FR/NFR text against `epics.md`. So every lifecycle update now edits a requirement and churns both. Three sections are also pinned word-for-word by `Contracts.Tests` (the GUARDED comments at lines 338, 643 and 674), so correcting PRD prose requires a code change.

### Findings

- **high** Lifecycle state embedded in requirement text and test-pinned prose means the PRD cannot stay current (NFR8 line 368; NFR18 line 378; §6.8 lines 340–343; §11.3; §11.4 "Current result" cells; OR9 line 756) — This review shows the cost: stale identities (§11.3), stale ownership (§11.2.1, OR6), and an 18-requirement inventory divergence. All come from status churn in a hand-maintained ledger. OR9 defers the restructure "to preserve stable downstream section anchors", but anchor stability is now costing correctness. *Fix:* Do the OR9 split in its minimal form now:
  - Keep FR/NFR text free of dates, story states and patch IDs. Move NFR8's Story 6.1 narrative to OR5 and §11.3, and NFR18's assignment to §11.2.
  - Move the §6.8 status bullets, the §11.3 register and the §11.4 "Current result" cells into a generated ledger keyed by gate and clause ID. The G-BASELINE and G-MVP-COVERAGE manifests are the natural home.
  - Leave anchor stubs that point to the ledger.
  - Retarget the GUARDED tests to the ledger.

## Mechanical notes

- **IDs:** FR1–FR37 (in thematic rather than numeric order, as before), NFR1–NFR19, UJ1–UJ5, SM1–SM12 and SM-C1–SM-C5 are unique and contiguous. §7.1 clause IDs are contiguous within each parent. OR1–OR30 are complete, with OR2, OR3 and OR12 retired and documented (line 760).
- **§11.4 inventory:** G-HIGH-RISK's enumerated gate list matches the 17 mandatory rows exactly. Every blocking OR is referenced by at least one gate row except OR17 (a meta item, acceptable) and OR30 (see Decision-readiness).
- **Range annotations:** I checked §11.2's "literal range endpoint" vs "range-only interior" annotations against Story 8.1 ("NFR1–NFR4"). They are consistent, because they classify the NFR range, not the story range. The column header does not say so.
- **§10 heading:** "Planning-recovery metrics (achieved)" contains SM3, which is marked "not met".
- **Source artifacts:** Every frontmatter `source_artifacts` path exists. `sprint-change-proposal-2026-10-07-story-9-2-nfr1.md` (approved, edits `epics.md` only, "No PRD edit is proposed") is not listed. That is acceptable, but listing it would record that the NFR1 propagation happened.
- **Assumptions Index:** It round-trips, since there are no inline `[ASSUMPTION]` tags. See Scope honesty for the imported AD-26 assumption.
- **Line length:** Two lines exceed 2,000 characters: the Assurance Control glossary entry (line 201, 2,266) and OR15 (line 725, 2,174). They are hard to diff and review.
- **Terminology:** "Stories 22.7a-d" (line 504) is legacy numbering explained inline, which is fine. For "INDEPENDENT" used as a status label, see Decision-readiness.

## Delta vs 2026-09-10

| 2026-09-10 finding (severity) | Status now | Note |
| --- | --- | --- |
| No executable exit contract (high) | **Resolved** | §11.4 has 17 computed rows plus a post-MVP G5 row. |
| Omnibus requirements lack clause-level closure: FR26, FR33, FR34, NFR17 (high) | **Resolved in PRD** | §7.1 ledger with an all-clauses-required rule. Mirroring into `epics.md` stays open under G-CLAUSE / Story 9.4. |
| Acceptance ambiguity in FR4, FR5, FR7, FR12 (high) | **Resolved** | Clauses FR4-C1..C6, FR5-C1..C3, FR7-C1..C5, FR12-C1..C4. |
| §11.2 omits NFR5, NFR12, NFR13 (high) | **Resolved** | Rows added with primary owners 2.16, 3.18 and 2.17. A new §11.2.1 contradiction on NFR5 was introduced. |
| No user journeys (high) | **Resolved** | UJ1–UJ5 with named protagonists, requirement lists and failure paths. |
| Generated APIs contradict the tenant boundary (critical); `Location` falls back to `CorrelationId` (critical) | **Resolved in PRD; implementation open** | NFR2 rewrite, MessageId Contract Version glossary entry, FR12-C2..C4, and G-TENANT / G-STATUS-ID (FAIL) owned by Stories 2.14 and 2.15. |
| NFR12 scope (high); NFR3 closed host list (high); FR36 consumer authority (high) | **Resolved in PRD; implementation open** | Inventory-backed NFR12; capability-defined NFR3; five-outcome FR36 plus a Consumer owner role; G-COMPAT / G-AUTH-HOSTS / G-CONSUMER owned by Stories 3.18, 5.11 and 3.20. |
| Silent append loss unguarded (critical) | **Ownership resolved; outcome open** | G-APPEND, with Story 4.16 on the envelope-first path. Fails closed. Not re-raised. |
| OQ8 normative bytes unavailable (critical) | **Still open** | G-OQ8 / OR11, now owned by Story 4.17. The PRD states it accurately and fails closed. Not re-raised. |
| NFR8 projection bound missing (high) | **Still open** | G-NFR8 / OR16 / Story 6.3. Correctly gated. Not re-raised. |
| Planning artifacts not one approved baseline (critical) | **Still open, and worse in its detail** | G-BASELINE still fails, as it should. The register that documents it is now itself stale, and the PRD/`epics.md` text divergence is 18 of 56. Re-raised as two highs under Downstream usability. |
| Bound reject result not a coherent current baseline (high) | **Partially resolved** | The frontmatter records a 2026-10-06 assessment. The §0 body still cites only the 2026-09-10 observations (low). |
| MVP priority follows epic inventory (medium) | **Still open** | Re-raised. Epic 9 is now also missing from §9.1. |
| Volatile readiness evidence embedded in PRD (medium) | **Worse** | 583 → 770 lines, and lifecycle state now sits inside NFR8 and NFR18. Raised to high. |
| Ownership traceability ≠ delivery traceability (medium) | **Partially resolved** | G-MVP-COVERAGE manifest specified but not built (Story 9.5). |
| Shared-workflow mutable vs immutable authority, FR25 / OR18 (medium) | **Unchanged** | FR25 still uses `@main`, and OR18 is still non-blocking. Not re-raised; nothing new to add. |
| **New since 2026-09-10** | — | NFR1 anonymous class never listed and contradicting AD-16 (high); UI-host carve-out unrepresentable in §9.1, G-MVP-COVERAGE and SM10 (high); SM11 counts OQ8 class (e) (high); OR30 outside the gate contract (high); "INDEPENDENT" labels against the Assurance Control (medium); unnamed residual risk (medium); Epic 9 omission (medium); AD-26 imported assumption (medium). |

**Net:** 0 critical, 7 high, 7 medium and 5 low findings, against 5 critical, 10 high, 4 medium and 0 low on 2026-09-10. Every earlier PRD-content defect has been addressed in the text. Every remaining high is either ledger drift or an inconsistency introduced by the 2026-10-07 edits.
