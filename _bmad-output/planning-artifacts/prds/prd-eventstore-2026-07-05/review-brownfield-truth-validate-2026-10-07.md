# Brownfield Truth Review — PRD Validate Run 2026-10-07

## Verdict

**Fail as the current chain-top baseline; pass as a fail-closed readiness posture.** The PRD's central safety claim still holds: readiness is `Reject`, every mandatory §11.4 row except the bounded G-RUNTIME-PARITY technical pass is FAIL/BLOCKED, and none of the spot checks found a gate shown as passing that should fail. Every artifact the PRD calls absent is absent. Each code-level contradiction it records is real: the generator's `MessageId ?? CorrelationId` fallback, raw and synthetic `system` tenants, Tenants' hand-rolled JWT subset, MediatR 14.2.0 with a suppressed license log, and the 14 release packages. There is therefore no critical finding. The document still cannot be trusted as the current baseline, for six reasons. First, its §11.3 baseline register reports September digests and statuses as current. Second, its ownership tables cite `epics.md` story sections that declare something else. Third, SM11 was quietly raised to count NFR7 class (e) as delivered on source-only evidence that the PRD's own §1.1 and NFR7 rules disallow. Fourth, NFR1 cites AD-16 for an anonymous UI-host class that AD-16 still forbids. Fifth, the Epic 8 and Epic 6 lifecycle statements are stale. Sixth, the current bytes (`d5632ba7…`) were never reviewed: 15 commits changed the PRD after the last sealed version (`9a35ebce…`, 2026-09-10). Five of them carry unrelated commit subjects, and the run memlog states that nothing was committed when the Group 7 edits are already in `HEAD`.

## Review Basis

- Repository `HEAD` `40c92e085d8a6463d469c1b340c410fec84a690f` (branch `main`). `prd.md` at `HEAD` equals the worktree: SHA-256 `d5632ba71c838ba7f0b8ca61a24889cb21edb4506531e823d8c0dfb12b7e6ae6`.
- The worktree is dirty in unrelated paths: Story 6.6 source and tests, UX docs, `references/Hexalith.Memories`, `references/Hexalith.Platform`, and this run's memlog. A concurrent loop is committing. Every git comparison below uses committed `HEAD` unless marked "worktree".
- Sources: `epics.md` (`0697679b…`), `architecture.md` (`7fd805a8…`), `implementation-readiness.md` (2026-10-06), `implementation-artifacts/sprint-status.yaml`, the story and spec files, `_bmad-output/planning-artifacts/.memlog.md` (2026-09-10 run), this folder's `.memlog.md`, and the 2026-09-23 through 2026-10-07 sprint-change proposals.
- Executed read-only checks (exit codes): `python3 -I tools/validate-oq8-platform-evidence.py` → 0 ("OQ8 platform evidence validation passed."); `--pre-review` → 1 ("Story 4.15 lifecycle drift: sprint status does not match lifecycle state"); the exact G-RUNTIME-PARITY command → 0 (`pass: subject=sha256:66be1b4a… selected=sha256:4b141085…`).

## Delta vs 2026-09-10

The last reviewed and sealed PRD is SHA-256 `9a35ebce…` at commit `94482189`. Its 2026-09-10 update run was logged in `_bmad-output/planning-artifacts/.memlog.md`, not in this folder's memlog: "Final reviewers sealed PRD SHA-256 9a35ebce… at HEAD 9cde9f2d". No reviewer gate has run since. Commits that changed `prd.md` after that seal (`git log 94482189..HEAD -- prd.md`; merges took one side and changed no content):

| Commit (date) | Subject | Substantive PRD change | Authority |
| --- | --- | --- | --- |
| `98395e5f` (09-21) | fix(oq8): close story 4.15 lifecycle | **SM11 3→4 of 5: class (e) counted as delivered on "closed source-only platform evidence"**; OQ8 §11.3 row, G-OQ8, OR11, and OR15 rewritten after the 4.15 closure | No proposal, no PRD memlog entry (see H1) |
| `06aaf950` (09-24), `694b9157`, `400589a5`, `cdad8278` (subject "build: sync local changes via /pushall"), `61ebd332` (09-26) | 3.15 evidence churn | §6.8 header → "TECHNICALLY VALIDATED; INDEPENDENT CONTROL OPEN"; G-RUNTIME-PARITY, G-PUBLICATION-AUTH, and G-CONSUMER re-pointed from subject `aafe9040` through the superseded 2026-09-24 pass and receipt-free `c98fdef2…` to `66be1b4a`; GUARDED comments added; OR15 gains the "v5 clean-checkout" sentence | `sprint-change-proposal-2026-09-23.md` covers the reconciliation in general; the edits themselves are evidence-driven |
| `9d4e5ec3`, `b8e4ac7f`, `ec67e340` (09-26) | docs(planning) | 3.15 `done` for FR36-C2 only; Epic 9 owners; Assurance Control (`single-maintainer-attested`) | Proposals 09-26 and 09-26-solo |
| `4fcb2b5c` (09-27) | docs: … McpCli | Unnumbered "Approved McpCli course correction" banner above §0; **FR26-C4 rewritten** | No in-repo proposal (see M5) |
| `23680543` (10-05) | feat: … Dapr Amendment Candidate | New §8.4 and §11.2.1; **FR37 requirement text changed** | Proposal 10-05, approved edit by edit, **not in `source_artifacts`** |
| `48ef7171` (10-07) | test: add BoundedV1DomainResultProducerScratchTests… | Ownership assigned (2.14–2.17, 3.18–3.20, 4.16, 4.17, 5.11, 6.7, 7.21, 9.3–9.5); NFR8 records the 6.1 reopen; §11.3 lifecycle facts; frontmatter now binds the 2026-10-06 readiness report | Proposal 10-07 (Application Record digest `ca97f4ef…` matches) |
| `4b1377a7` (10-07) | feat(tests): … private image registry callbacks | §11.2 rows for 3.21, 5.12–5.14, 7.22; `source_artifacts` | Routing proposal |
| `b2c4bf79`, `c60503c1` (10-07) | test: add LegacyCommandReplayJsonAdmissionTests…; feat: add obligation audit… | Group 7: Assurance Control seal definition, **NFR1 UI-host anonymous class**, §9.2 UI-host production-profile exclusion (**the PRD's one scope reduction**), NFR18 owner, DAPR Boundary glossary, OR30. The run memlog entries that say "nothing committed" were committed in `c60503c1` | Routing proposal Group 7 owner decisions |

Since the sealed version, FR/NFR requirement text changed only in **FR37, NFR1, NFR8, and NFR18**, and each change traces to an approved proposal or a dated owner decision. The silent changes are in metrics, gates, and status narrative: SM11, the §11.3 register, and OR15. Correction to the run memlog (line 107): it counts 18 unlogged commits from `2dd7ebfb` through `4b1377a7`. The range actually holds 16 non-merge commits, and the first three (`2dd7ebfb`, `9cde9f2d`, `94482189`) are logged in the planning-level memlog.

**State of the 2026-09-10 critical findings:** all five remain open and correctly fail-closed. (1) The baseline is still not reconciled, and the drift is now wider. (2) The OQ8 bytes are still absent; G-OQ8 fails; Story 4.17 is backlog. (3) The generator still emits `"system"` and raw tenants (`RestApiControllerEmitter.cs:410-444`); Story 2.14 is backlog. (4) The generator still emits `MessageId ?? CorrelationId` (`RestApiControllerEmitter.cs:281`); Story 2.15 is backlog. (5) The append race is still unfenced; Story 4.16 is backlog under AD-5 (envelope first). The 2026-09-10 highs on §11.2 completeness, user journeys, NFR12 breadth, and FR36 consumer authority were fixed in the text. The highs on the clause ledger, the exit contract, and the NFR8 specification remain open, each with an owner.

## Findings

### Critical (0)

None. No checked claim misstates readiness or lets a gated transition proceed.

### High (6)

**H1 — SM11 counts NFR7 class (e) as delivered on evidence the PRD itself says cannot close it**

- Location: §10 SM11, line 544. Related: NFR7 (line 367), §1.1 (line 133), §11.2 NFR7 row (line 615), G-OQ8 (line 679).
- Claim: "Currently four of five are delivered: … class (e) duplicate side effects has implementation and closed source-only platform evidence from Stories 4.9-4.15."
- Evidence: NFR7 requires that "production-path evidence proves that the guard or recovery prevents the loss". §1.1 says "No OQ8 closure claim under FR27, NFR7, or NFR16 is authoritative" until G-OQ8 passes, and G-OQ8 is FAIL/BLOCKED. Class (e) is the OQ8 durable-admission class. The change from "three" to "four" was made in `98395e5f` ("fix(oq8): close story 4.15 lifecycle", 2026-09-21; `git show --word-diff 98395e5f -- prd.md` shows `[-three-]{+four+}` and the class (e) sentence replacing "closure remains pending"). No sprint-change proposal and no PRD memlog entry covers it. The Story 4.15 v4 record calls itself source-only (`evidence/story-4-15-successors/v4/source-only-handoff.json`; limitations: "grants no release approval, package authority…"). The §11.2 NFR7 note names only class (c) as failed, which repeats the over-claim.
- Fix: Restore "three of five delivered; class (e) has implementation and source-only platform evidence, but delivery is pending G-OQ8 and production-path proof." The alternative is an owner decision, logged and proposal-backed, that amends NFR7 and §1.1 to accept this evidence. In either case, name class (e) next to class (c) in the §11.2 NFR7 note.

**H2 — The §11.3 baseline register reports 2026-09 digests and statuses as current**

- Location: §11.3 rows `architecture.md`, `ux.md`, and `epics.md` (lines 648-650); §3.3 (line 167).
- Claims: architecture SHA-256 `7e3dbc7b…`; "`ux.md` SHA-256 `2927f97d…`; top-level status `final`"; "`epics.md` matches this top-level digest"; DESIGN "current `3f4f0181…`"; EXPERIENCE "current `11f75403…`"; `epics.md` SHA-256 `d067c8fb…`; line 167: "the final top-level `ux.md` handoff".
- Evidence (`git show HEAD:<path> | sha256sum`): architecture `7fd805a8…`; ux.md `23b17217…` with `Status: draft` at `HEAD` (changed in `40c92e08`); DESIGN `7b743e79…`; EXPERIENCE `3985789a…`; epics `0697679b…`. The epics `inputDocumentDigests` still pin ux.md `2927f97d…`, so "epics matches" is false. It was already false on 2026-10-06, since the readiness report found all five epics pins stale. The values the PRD gives date from commit `0994c378` (2026-09-09); DESIGN and EXPERIENCE went stale at `40c92e08`. The worktree holds an uncommitted UX update that sets ux.md back to `final` (`a135c969…`) and removes the two EXPERIENCE `[ASSUMPTION]`s, so these rows will drift again when it lands. The column header "Identity/status at stated date" does not rescue the rows, because they state no date.
- Fix: Give every row an explicit observation date and commit, or replace the hard-coded digests with "see G-BASELINE manifest (Story 9.3)". Delete the "epics matches this top-level digest" sentence. Fix line 167 so it does not call ux.md final.

**H3 — Epic 8 lifecycle is wrong: Story 8.2 is done and Story 8.3 is in progress**

- Location: §11.3 bullet (line 658); SM4 (line 533).
- Claims: "Story 8.2 is authorized but still `backlog` pending its story file, and Stories 8.3-8.11 remain predecessor-gated." (line 658). "Stories 7.14 and 8.2 … Both remain `backlog`" (SM4, line 533).
- Evidence: `sprint-status.yaml` has `8-2…: done` and `8-3…: in-progress`. `epics.md:6779`: "Story 8.2 is done under `AR-20260914-01`, which … authorizes 8.3". `implementation-artifacts/8-2-payload-protection-contracts-and-golden-vectors.md` records implementation done on 2026-09-13 and Story 8.3 authorized at `2026-09-14T07:04:19Z`. `8-3-pdenc-v2-core-cryptographic-engine.md` records implementation through a fourth review pass on 2026-09-17.
- Fix: Restate the G5 path as 8.1 and 8.2 done, 8.3 in progress, and 8.4–8.11 predecessor-gated. Correct SM4 so that only 7.14 remains backlog. G5 stays `needs-additive-api`.

**H4 — §11.1 and §7.1 primary slices are not what the cited `epics.md` story sections declare**

- Location: §11.1 FR4, FR5, FR7, and FR34 rows (lines 564, 565, 567, 594) and the footnote at line 599 ("Story ownership is declared by each `### Story` section in `epics.md`"); §7.1 FR4-C2 to C6, FR5-C1, FR5-C2, FR7-C2 to C5, FR34-C10, and NFR17-C1, C3, C4 (lines 388-434).
- Evidence (each story's `**Requirements coverage:**` line in `epics.md`):
  - FR4: 1.16 "Supports FR4" (1081); 2.7 "Supports FR4 and FR15" (1704); 2.11 "supporting … FR4" (1893). Only 1.2 is primary.
  - FR5: 1.3 and 1.14 "Supports primary FR5" (540, 969).
  - FR7: 1.6 "Supports primary FR7" (656); 1.18 and 1.19 "supporting FR7" (1192, 1248). Story 1.15, which the PRD assigns FR7-C3, declares no FR7 at all ("Primary FR5; supporting FR36, NFR7, and NFR16", 1025).
  - FR34-C10: 7.11 is "supporting FR34" (5960).
  - Internal PRD contradiction: §7.1 makes 7.6, 7.8, and 7.9 the primary slices for NFR17-C1, C3, and C4, but §11.2's NFR17 row (line 625) lists 7.6 and 7.8-7.9 as supporting, matching epics (5630, 5766, 5829).
  - `epics.md` contradicts itself on FR12: Story 2.9 still claims "Primary ownership of FR12's accepted-command `Location` clause" (1796), and Story 2.11 says "FR12's two primary slices stay with Stories 2.2 and 2.9" (1893). The FR12 completion rule at line 407 and the PRD both name 2.15.
  - §11.1 attributes FR4 to "Epic 2 - metadata/provenance", but the epics FR Coverage Map gives FR4 to Epic 1 only.
- G-CLAUSE fails closed on clause closure, but the ownership tables misstate their own source.
- Fix: Either label the §7.1 and §11.1 slices "PRD-proposed, not yet declared in `epics.md` (G-CLAUSE / Story 9.4)", or promote the declarations in `epics.md` through correct-course. Make §7.1 and §11.2 agree on NFR17. Route the stale coverage lines on Stories 2.9 and 2.11 to the OR8 guard.

**H5 — NFR1 cites AD-16 for an anonymous class that AD-16 still forbids, and no story owns that class**

- Location: NFR1 (line 361); SM10 (line 543); §11.2 NFR1 row (line 609).
- Claim: "…and, on interactive UI hosts only, an enumerated set of static framework assets and authentication-protocol callback endpoints … each explicitly pinned `AllowAnonymous`, support-safe, and enumerated by endpoint-metadata tests (AD-16)".
- Evidence: `architecture.md:244` (AD-16, `[ADOPTED]`, unchanged at `HEAD` and in the worktree): "Only `/health`, `/alive`, and `/ready` carry explicit `AllowAnonymous` metadata. Endpoint metadata tests enumerate exactly those support-safe exceptions… Any future anonymous asset or callback first requires a governed PRD change." Story 5.14's AC (`epics.md:4815`, 4828-4831) still requires "only `/health`, `/alive`, and `/ready` carry `AllowAnonymous`" and treats the UI-host decision as "still pending". `architecture.md:541` still says PRD Assurance Control (1) reads "required, blocking run", which predates the 7.1 amendment. In code, only `ServiceDefaults/Extensions.cs:184-186` applies `AllowAnonymous`. The UI-host class has no enumeration owner; the exclusion of UI hosts from the production profile (§9.2) limits the exposure.
- Fix: Until `bmad-architecture` amends AD-16, mark the UI-host clause "AD-16 amendment owed (routing 7.2)". Route the Story 5.14 AC and the `architecture.md:541` row to their owners, and name which story will enumerate the class once a UI host enters a production profile.

**H6 — The current PRD bytes are unreviewed; `final` and the finalize fields describe an earlier document**

- Location: frontmatter (lines 3-4, 17-19); this folder's `.memlog.md` lines 106-120.
- Claims: `status: final`; `prd_finalize_assessed: 2026-09-10`; `prd_finalize_reviewed_head: dd55a6d1…`. Memlog line 119: "…readiness stays FAIL, nothing committed"; line 120: "No reviewer gate run".
- Evidence: Reviewers sealed `9a35ebce…` at `9cde9f2d` (planning-level `.memlog.md`), so the frontmatter head `dd55a6d1` was not the sealed head. The 15 later commits listed in the Delta table changed requirement text (FR37, NFR1, NFR8, NFR18), the MVP production-profile scope (§9.2, §13), the Assurance Control definition, SM11, and the gate narrative, with no reviewer gate. H1, H2, H4, and H5 all come from that unreviewed delta. `git show --stat c60503c1` includes `prd.md` and `prds/…/.memlog.md` under the subject "feat: add obligation audit for current amendments…", and `b2c4bf79` carries item 7.1 under "test: add LegacyCommandReplayJsonAdmissionTests…". Planning changes landing in `feat` commits also feed semantic-release versioning.
- Fix: Run the reviewer gate on the current bytes before keeping `status: final`, or set `document_status` to `draft-pending-review`. Record the sealed digest and head that the reviewers actually examined. Append a memlog correction stating that the Group 7 edits were committed in `b2c4bf79` and `c60503c1`. Keep planning edits in separate `docs(planning)` commits.

### Medium (8)

**M1 — The Story 6.5 event-versioning gate is done and Story 6.6 is in progress, but the PRD still says the specification is required**

- Location: §11.3 bullet (line 662); FR33-C5 (line 413).
- Claim: "…and the event-versioning specification remains required before Story 6.6."
- Evidence: `sprint-status.yaml` has `6-5`, `6-5a` through `6-5d`: `done` and `6-6: in-progress`. `spec-event-versioning-upcasting.md` has `status: normative-approved` and says "The owner has approved this design; Story 6.6 is ready". `spec-6-6-…-implementation.md` has `status: 'in-progress'`.
- Fix: Record FR33-C5's approved specification by identity and Story 6.6's in-progress state. The FR33-C6 evidence stays open.

**M2 — §11.2 and §11.2.1 contradict each other and omit declared owners**

- Location: §11.2.1 (line 634); §11.2 NFR5, NFR7, NFR12, NFR17 rows (lines 613, 615, 620, 625).
- Claim: "NFR5's primary-owner gap remains open." (line 634)
- Evidence: §11.2 NFR5 names primary owner 2.16, and G-NFR-OWNERSHIP repeats it. The NFR7 row omits Story 4.16 ("Primary OR4, NFR7 class (c)", `epics.md:4085`) and the supporting Story 4.17 (4116). Supporting cells also omit 2.13 for NFR5, NFR12, NFR15, and NFR16 (2005) and 3.17 for NFR12 and NFR17.
- Fix: Remove the stale NFR5 sentence, add 4.16 as the class (c) primary owner, and complete the supporting cells.

**M3 — The PRD contradicts itself on whether the default OQ8 validator passes**

- Location: §11.3 OQ8 row (line 653) and G-OQ8 (line 679), against OR15 (line 725).
- Claims: "Story 4.15 v4 default, closed, and historical validators pass…" against "the current default OQ8 probe is blocked by v5 clean-checkout validation and the changed `docs/ci.md` requires separate gate-input reconciliation" (sentence added in `400589a5`).
- Evidence: `python3 -I tools/validate-oq8-platform-evidence.py` exited 0 with "OQ8 platform evidence validation passed." on `HEAD` plus the dirty worktree. `--pre-review` exited 1 with a 4.15 lifecycle drift, which is expected after closure.
- Fix: Drop or date the OR15 sentence, or name the exact failing command and checkout mode.

**M4 — OR14 still owes architecture work that has already landed**

- Location: OR14 (line 724).
- Claim: "Architecture must add the Tenants boundary to AD-10 and align AD-11/AD-26 to the PRD's `built` → … → `production-promoted` vocabulary".
- Evidence: AD-10 (`architecture.md:186`) names the "Tenants domain-service host, Tenants API". AD-11 (line 208) carries the full five-state lifecycle, and AD-26 uses it. `architecture.md:540` reports that all eleven inline `[ASSUMPTION]`s were owner-resolved on 2026-10-07; AD-26 alone keeps its tag, and §11.3 says so correctly.
- Fix: Narrow OR14 to what remains: reviewer closure, the AD-26 ratification records, the AD-16 amendment, the UX assumptions, and the epics renewal.

**M5 — Provenance gaps: a proposal that changed the PRD is unlisted, and an "Approved" claim has no source**

- Location: frontmatter `source_artifacts` (lines 23-96); banner (line 101); FR26-C4 (line 408).
- Evidence: `sprint-change-proposal-2026-10-05.md`, which added §8.4 and §11.2.1 and changed FR37's text, is not listed, although the 2026-09-08 decision recorded at memlog line 68 is that every proposal is listed. Also unlisted, though PRD-referencing: 2026-09-27 (Story 6.5 split, cites FR33-C5), 2026-09-30 (cites `prd.md:408`), and 2026-10-07-story-9-2-nfr1. The "Approved McpCli course correction (2026-09-27)" banner and the FR26-C4 rewrite (`4fcb2b5c`) cite no artifact. `sprint-change-proposal-2026-09-27.md` concerns the Story 6.5 split, and AD-35 attributes the correction to a "Platform course correction" that lives only in the `references/Hexalith.Platform` planning artifacts.
- Fix: Add the four proposals and cite the McpCli authority by repository, path, and commit, or mark it unverified. Move the banner into §1.

**M6 — §9.1 MVP scope omits Epic 9, which `epics.md` calls an MVP epic and which owns mandatory gates**

- Location: §9.1 (line 492). The PRD never mentions "Epic 9".
- Claim: "Epics 1-7 as listed in `epics.md`; `epics.md` also lists Epic 8, which is committed post-MVP".
- Evidence: `epics.md:389`: "MVP epic, independent of Epic 8". Stories 9.1–9.5 are the primary owners of OR28, G-HIGH-RISK, OR10, OR13, the G-BASELINE validator, G-CLAUSE, and G-MVP-COVERAGE (§11.4 and §12).
- Fix: Add Epic 9 to §9.1 as governance scope with no FR and say whether it enters the G-MVP-COVERAGE denominator.

**M7 — 18 of 56 FR/NFR texts differ from the `epics.md` Requirements Inventory, but the register records only digest drift**

- Location: §11.3 `epics.md` row (line 650); G-BASELINE (line 678); OR8 (line 723).
- Evidence: A read-only comparison of PRD §6/§7 rows against the `epics.md` `FRn:` and `NFRn:` lines found 38 identical and 18 different: FR5, FR12, FR15, FR16, FR26, FR33, FR34, FR36, FR37, NFR2, NFR3, NFR5, NFR7, NFR8, NFR12, NFR17, NFR18, NFR19. The epics inventory still carries pre-2026-09-10 wording for safety contracts: NFR2 ("Tenant provisioning must reject…", word similarity 0.36), NFR7 (0.30), NFR12 (0.10), and FR36 (0.20). The 2026-10-07 proposal mentioned only "NFR3 and NFR5". NFR1 now matches, after `40c92e08`.
- Fix: List the divergent IDs in §11.3 as an explicit G-BASELINE and OR8 input, so stories written from the inventory are known to cite superseded text.

**M8 — "INDEPENDENT" labels misapply the 2026-09-26 Assurance Control**

- Location: §6.8 (line 342) "TECHNICALLY VALIDATED; INDEPENDENT CONTROL OPEN"; G-RUNTIME-PARITY (line 687) "TECHNICAL PASS; INDEPENDENT GATE BLOCKED".
- Evidence: With a one-human registry, the required level is `single-maintainer-attested` (glossary, line 201). The gate's own text says the blocker is "G-HIGH-RISK's assurance and sealed CI controls". The labels are pinned by `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectedDeployedRuntimeParityClosureTests.cs:4562-4592`. The PRD-bound record `implementation-artifacts/3-15-corrected-deployed-runtime-parity-closure.md:24` links an "independent Test Architect decision" that the same paragraph and the tracker call self-attested.
- Fix: Rename the labels to "ASSURANCE CONTROL OPEN" and "ASSURANCE GATE BLOCKED" and update the pinned test in the same change. Fix the record's link text.

### Low (7)

- **L1 — OR rows still owe work that the 2026-10-07 assignments completed.** OR6 (line 722): "give NFR18 an owning story", although 6.7 is assigned. OR7 (732): "assign FR36-C3, FR36-C4, FR36-C5, and NFR17-C5", although 3.19, 3.20, and 7.21 are assigned. OR25 and OR26 (739-740): "Assign NFR5/NFR13 one disjoint primary story", although 2.16 and 2.17 are assigned. OR20 and OR21: "Reopen … or approve corrective successors", although 2.14 and 2.15 exist. Fix: reword each as "deliver and prove".
- **L2 — OR19's premises no longer hold** (line 758). `epic-2: in-progress` is now correct because 2.13–2.17 are backlog, and the Story 2.12 key is complete (`sprint-status.yaml:114`, matching the epics title). Fix: retire or restate OR19.
- **L3 — SM8 undercounts residual per-domain plumbing** (line 541). Tenants still ships `src/Hexalith.Tenants.AppHost` and `src/Hexalith.Tenants.Aspire` (gitlink `81144734`), which is per-domain Aspire wiring on FR9's own removal list. SM8 names only the host composition (`AddDaprClient`, `UseCloudEvents`, MediatR, controllers at `Hexalith.Tenants/Program.cs:46-181`). Fix: add those projects to the SM8 count.
- **L4 — The glossary overstates AD-26** (line 188). "AD-26 defines exactly one currently authorizing production profile" omits that AD-26 is `[ASSUMPTION]` (`architecture.md:337`): its target binds only after ratification, and only the profile-slot mechanics bind now. Fix: say so.
- **L5 — The body cites a superseded readiness result.** §0 (line 110), §1 (line 127), and OR1 (line 750) still present the 2026-09-10 Poor/Reject as the bound result, but the frontmatter binds `implementation-readiness.md` (2026-10-06), whose verdict is "FAIL" rather than "Reject". Fix: cite the 2026-10-06 report and map FAIL to `reject` once.
- **L6 — Mandatory rows and blocking refinements without a primary story.** G-NFR-OWNERSHIP, G-READINESS, OR14, OR17, and OR30 name none, although the 2026-10-07 proposal's success criterion 1 requires every mandatory gate and blocking refinement to name one. Fix: name an owner story or mark the row owner-only.
- **L7 — The run memlog miscounts the drift** (line 107). It reports 18 unlogged commits; there are 16 non-merge commits, and three of them are logged in `_bmad-output/planning-artifacts/.memlog.md`. Fix: append a correction.

## Claims Verified True (sample)

- **Frontmatter and baselines.** `a6fc951e`, `293c69c4`, and `dd55a6d1` exist and are ancestors of `HEAD`. The 2026-09-10 report matches the `prd_source_validation_*` fields (Poor, baseline `293c69c4`). All 73 `source_artifacts` paths exist at `HEAD`. Epics still pins the PRD at `b99effdb…`, as §11.3 says.
- **Absent artifacts.** These are absent as stated: `deploy/dapr/production-profile.yaml`, `docs/reference/aot-and-trimming-posture.md`, `spec-projection-cost-sequence-guard.md`, the four §0 and §11.4 validators and their records, `consumer-removal-manifest.json`, and the routing catalog. Present as cited: `deploy/dapr/resiliency.yaml` with the exact eight NFR17 policies and targets, `docs/concepts/dapr-infrastructure-boundary.md`, `docs/architecture/dapr-infrastructure-exceptions.yaml`, `tools/release_evidence_handlers/v3.py`, and the `publish-containers` gate in `release.yml:159` (no EventStore build file declares the platform set).
- **Code checks.** NFR5 constants are 16 and 2048 (`ProjectionChangeNotifierOptions.cs:19,24`). The FR2 and FR3 SDK entry points and the five routes exist (`EventStoreDomainServiceRoutes.cs:20-32`). Probes are `AllowAnonymous` (`ServiceDefaults/Extensions.cs:184-186`). OR30's facts check out: Builds catalog line 194 pins `MediatR` 14.2.0; Server, Gateway, and host csproj reference it; `appsettings.json:6` sets the license log to `None`; the latest tag is `v3.115.0`.
- **Lifecycle.** The 3.15 `closure.json` names subject `66be1b4a…` with 3 receipts, selected index `4b141085…`, and all authority flags false. Tracker states match the PRD for 3.15, 4.5, 4.6, 4.15, 5.2, 5.3, 5.4, 6.1, and 7.15–7.18. The `spec-folded-snapshot.md` frontmatter still reads `approved-authorized` / `story_6_2_authorized: true`, as OR5 says. Story 4.6's spec has `approval_state: 'absent'`. The DW-64/65/77/78/79/326 entries exist. The Epic 3 retrospective verdict is `rejected`.
- **Owners and alignment.** Every story the PRD names exists in `epics.md` and the tracker. §11.1 matches epics for FR1–FR3, FR6, FR8–FR11, FR13–FR33, FR35–FR37. §11.2 primary cells match epics for NFR1–NFR4, NFR6, NFR8–NFR16, NFR18, and NFR19, apart from the omissions in M2. The PRD matches AD-5's envelope-first G-APPEND path (`architecture.md:150`) and AD-17's MessageId versioning. The epics NFR1 inventory and the Story 9.2 seal AC were propagated in `40c92e08`.
