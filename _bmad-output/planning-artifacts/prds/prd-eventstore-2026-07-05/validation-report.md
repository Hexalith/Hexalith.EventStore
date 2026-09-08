# Validation Report — eventstore Phase 4 Implementation Readiness Recovery

- **PRD:** `_bmad-output/planning-artifacts/prd.md` (frontmatter `status: final`, `updated: 2026-08-16`; last content commit `8312bced` 2026-09-06)
- **Rubric:** `.claude/skills/bmad-prd/assets/prd-validation-checklist.md`
- **Run at:** 2026-09-08T17:39:43Z
- **Grade:** Poor

## Overall verdict

The PRD's skeleton still holds: decisions are stated as decisions (§1.1 OQ8 authority order, §9.3 Epic 8 gating, §11.3 no-authority boundary), scope boundaries are explicit, IDs are stable, the epic-level FR map is accurate for all 37 FRs, and the September rewrite of NFR3/NFR4 is the best-bounded NFR text in the document. What is at risk is done-ness and truthfulness of self-description: every rubric deferral logged on 2026-07-16 and 2026-07-19 is still open, the PRD asserts "no open questions" while owing them, the success metrics are almost all already satisfied so the PRD can no longer say whether the remaining four epics succeed, and the unlogged NFR3 rewrite is simultaneously looser than the shipped implementation on Production break-glass and out of sync with the epics.md inventory that stories are cut from.

The adversarial and brownfield reviewers shift the picture from Fair to Poor. Three findings are critical. NFR7's append-race guard reads as delivered (Story 4.5 `done`) while the only evidence records a silently lost durable write with no fence authorized and no story owning one. The §1.1 OQ8 authority order binds three of the PRD's own requirements to a hashed document that is not in this repository, has no path, and per `epics.md` overrides the PRD. And NFR3/NFR4 were rewritten on 2026-09-06 with no approving proposal, no memlog entry, no frontmatter bump, and the Story 5.3 spec that cites the new text as authority was created the day after. The grade is Poor because of those criticals, not because the document is weak overall; each is a bounded edit, but together they mean the PRD cannot currently be trusted as the authoritative source it claims to be for Epics 4-7.

## Dimension verdicts
- Decision-readiness — adequate
- Substance over theater — strong
- Strategic coherence — adequate
- Done-ness clarity — thin
- Scope honesty — adequate
- Downstream usability — adequate
- Shape fit — strong

## Findings by severity

Findings raised by more than one reviewer are merged; the originating reviewers are named in brackets.

### Critical (3)

**[Adversarial]** NFR7's append-race guard is marked delivered while the only evidence records silent data loss and no story owns a fix (§7 NFR7 line 276; §6.4 FR31 line 209; §11.2 line 427)
NFR7 requires that "append races ... must be explicitly guarded or recovered" and its coverage row lists Story 4.5 (`done`). FR31 requires only a live-sidecar two-writer race test before choosing a fencing design, a spike rather than a guard. Story 4.5's reconciliation in `epics.md:3112` records the outcome `same-key-overwrite-raw-durable-write-lost` ("silently replaced by the accepted actor write without an exception or retry ... No fencing implementation is authorized") and its AC says 4.5 "grants no authority to implement the fence". No FR, story, or deferred-work entry owns the fence. FR24 (sharding) has the same shape: Story 4.6 renegotiates the spec, nothing implements it.
Fix: split NFR7 into one row per loss class (staged flush, stale pipeline, append race, committed-unpublished, duplicate side effects), each with an owning implementation story or an explicit "NOT delivered in Phase 4 MVP, deferred to <story>" marker; add an FR for the fencing implementation or state in §9.2 that append fencing is out of MVP scope; do the same for global-position sharding.

**[Adversarial]** The OQ8 authority order binds governance to a document that is not in this repository, has no path in the PRD, and overrides the PRD's own FR text (§1.1 lines 76-83)
"The Architecture + Security + Test-approved OQ8 design version 1.0.0 (SHA-256 `1a55b030…`) govern" and "Any pre-change FR27, NFR7, NFR16, or architecture wording is historical context only". No file in this repository hashes to that digest. The 2026-07-20 proposal locates it at `docs/exit-criteria/oq8-idempotency-design.md` in a different repository ("Folders"), untracked at the time. `epics.md:149` says the design "override[s] historical FR27/NFR7/NFR16 text", so the document §0 calls the authoritative baseline declares three of its own requirements non-authoritative in favour of an unlocatable external file. "Folders" appears nowhere in the PRD.
Fix: name the repository, path, and commit of the governing design in §1.1; either project its normative content into FR27/NFR7/NFR16 so the PRD is self-sufficient, or replace "historical context only" with a precise statement of which clauses are superseded and by what; add "Folders" and "Parties" to the glossary as external repositories.

**[Brownfield; Rubric rated medium; Adversarial folded into high]** NFR3/NFR4 were rewritten out-of-band with no authorizing artifact and no provenance update (§7 lines 272-273; frontmatter line 5)
Commit `8312bced` (2026-09-06) replaced both NFR rows (+2/-2), the Story 5.3 body in `epics.md`, and `epic-5-context.md`. No `sprint-change-proposal-2026-09-06*.md` exists; the 2026-09-07 proposal states "No PRD, architecture, epic, or UX content changes". The Story 5.3 spec was first committed on 2026-09-07 (`dfbbde37`), after the PRD edit, and cites "the authoritative Epic 5.3 AC and NFR3" as its authority, which is circular. Frontmatter still reads `status: final` / `updated: 2026-08-16`; the run memlog has no entry between the 2026-08-16 finalize and this run; `source_artifacts` lists no source. The July 2026 out-of-band edits were recovered into the memlog with `(override)` entries; this one was not.
Fix: either author and approve a dated sprint-change proposal ratifying the 2026-09-06 text, bump `updated`, add memlog `(override)`/`(decision)` entries, and propagate to the `epics.md` inventory; or revert lines 272-273 to the 2026-08-16 text and carry the detailed contract only in Story 5.3.

### High (9)

**[Rubric; Adversarial]** "No open questions" contradicts eight logged, unapplied deferrals (§12 line 460; §13 line 464)
The memlog (2026-07-16, 2026-07-19) deferred: clause-level completion evidence for omnibus FR26/FR33/FR34; finite bounds for NFR5/NFR7/NFR8/NFR17; correctness-gate binding; product-bet success metrics; glossary and cross-reference expansion (G5, goldens); the front-loaded-scope restructuring. None has landed. The PRD gives readers no signal that any of this is owed, by whom, or on what trigger. Zero `[NOTE FOR PM]` callouts on a launch-stakes PRD is itself a signal.
Fix: add an "Owed refinements" list under §12 with owner and trigger for each item, or `[NOTE FOR PM]` callouts at FR26/FR33/FR34, NFR5/NFR8/NFR17, and §10.

**[Rubric; Adversarial]** Success metrics are exhausted before the work is, and SM1-SM3 cannot fail (§10 lines 353-364; §2 line 91)
SM1 ("readiness re-run no longer reports missing PRD") is satisfied by the file existing. SM2 requires every FR to map to an epic "and story" but §11.1 maps to epics only. SM3's deadline ("before Phase 4 implementation resumes") elapsed months ago. SM4-SM6 are met. Only post-MVP SM7 is live. Nothing measures the §2 bet that "platform reuse and operational hardening are delivered together": no metric for boilerplate removed from Sample/Tenants (FR9), zero per-message controllers in UI hosts (FR13/NFR14), fail-closed surface coverage (NFR1), or persisted-evidence test share (NFR16). Deferred 2026-07-16; blocking in effect with four epics executing.
Fix: add a story column to §11.1; retire SM1/SM3 as achieved; add thesis metrics with numbers (platform plumbing deleted from Tenants, count of surfaces with a negative fail-closed test, count of silent-loss classes with an owning implementation story), each with a counter-metric.

**[Rubric; Brownfield]** NFR3 permits Production break-glass that the implementation and Story 5.3 forbid (§7 line 272)
"Authentication outside Development must reject symmetric-key mode unless explicitly enabled by the narrowly scoped `AllowInsecureSymmetricKey` break-glass option" admits symmetric mode in Production when the flag is set. `src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtBearerAuthenticationContract.cs:118-122` returns "SigningKey is forbidden in Production, including when AllowInsecureSymmetricKey is true" before the option is consulted; the option only admits non-Development, non-Production environments. The Story 5.3 spec agrees with the code ("Never: Permit symmetric JWT validation in Production even when the legacy override is set"). Every other NFR3 claim (option name, HS256-only, nonempty asymmetric allowlist with no default, HTTPS metadata outside Development, fixed 60-second skew) is implemented. As the authoritative NFR, the current text would justify a later change that re-enables HS256 in Production.
Fix: "Production must always reject symmetric-key mode. Non-Development, non-Production environments may accept HS256 symmetric mode only when `AllowInsecureSymmetricKey` is explicitly enabled ..." and align the Story 5.3 AC in `epics.md`.

**[Rubric; Adversarial rated medium]** Omnibus FR26/FR33/FR34 still have no per-clause done consequence (§6.5 line 219 vs 223; §6.6 line 231 vs 233; §6.7 line 241 vs 244)
FR26 lists nine remediations; its done evidence covers four (nothing for infrastructure-failure staged-state clearing, tenant-filter parity, admin Swagger gate, destructive CLI confirmation, ULID-safe correlation middleware, stale test-baseline docs). FR34 lists eleven; done evidence covers four, and it shares one §6.7 block with FR35 which is `done` while 7.1-7.10 are all `backlog`. FR33's done evidence is spec-existence only. NFR17's row lists 3.12 (`done`, satisfies only "immutable image tags") alongside backlog 5.6/7.6-7.9, so the row reads green for a requirement whose core (openbao, secretKeyRef, default-deny) has zero implementation. Epic 5 is in progress with 5.2/5.3 `done`; a story can now close against FR26 with no PRD statement of which clauses it retires. Deferred 2026-07-16; blocking in effect.
Fix: sub-letter the clauses (FR26a-i, FR33a-f, FR34a-k, NFR17a-g) with one testable consequence each, or add a clause-to-story-to-evidence table in §11; do not let a `done` story appear in a row unless it closes a named clause.

**[Rubric; Brownfield rated medium]** `epics.md` Requirements Inventory disagrees with the PRD on NFR3/NFR4 (§7 lines 272-273 vs `epics.md` lines 111, 113)
The inventory still carries the pre-2026-09-06 wording ("pin accepted JWT algorithms", "require HTTPS metadata where appropriate"); the PRD carries the rewrite; Story 5.3's AC carries a third, fuller rendering. Three wordings for two NFRs across the two artifacts the story pipeline reads. A story cut from the inventory would be held to the weaker contract.
Fix: sync the inventory to the ratified text in the same change that resolves the provenance critical; add a Contracts-style guard that diffs PRD §7 against the inventory.

**[Adversarial; Brownfield rated medium]** FR36's real state is unreadable, and Stories 1.21 and 3.16 are invisible to the PRD (§6.8 line 254; §11.1 lines 396-398, 413; §11.3 line 442; SM6 line 363; §9.1 line 328)
§11.1 calls FR36 "completed ... in Story 1.20" and Epic 1 is `done`, but positive deployed-runtime parity sits with Story 3.15 at 0 of 3 receipts with a verifier that exits 1, and the 2026-09-07 Epic 3 retrospective is `rejected` and says "FR36 remains open". §11.3 calls 1.20's evidence "immutable inputs", yet Story 1.21 (`done`) exists precisely to repair three drifted files in 1.20's frozen manifests and is never mentioned. Story 3.16 (`backlog`, references FR19/FR21/NFR9/NFR11/NFR12, a condition for Epic 3 closure per `epic-3-context.md:54`) is also absent; the §8.1 "latest validated compatible versions" constraint has no other implementer.
Fix: add a per-FR status line for FR36 with three sub-states (source/package closed via 1.20; frozen-evidence integrity repaired via 1.21; deployed-runtime open via 3.15 at 0/3); add 3.16 to the FR19/FR21 rows, the NFR9/NFR11 rows, and the §9.1 release bullet; add 1.21 to the FR36 row.

**[Brownfield; Adversarial]** Story 5.3, the only NFR3/NFR4 owner, has three contradictory statuses (§11.2 lines 424-425; §10 SM3 line 357)
`sprint-status.yaml:181` says `done`; the spec frontmatter says `status: 'in-progress'`, review loop 3, `warnings: [oversized]`; `epics.md:3810` says "Story 5.3 remains backlog ... committed Development configuration still carries fixed signing-key and administrator credential values". The tracker moved `backlog` to `done` in `c83cc4c3` (2026-09-07, a `feat(tests)` commit with no closure record), and the spec's Boundaries forbid the story from touching `sprint-status.yaml`. SM3 ("NFR1-NFR4 map to concrete story coverage") cannot be read off any single source.
Fix: reconcile the three sources before the PRD claims NFR3/NFR4 coverage; if 5.3 is genuinely done, close the spec with its review evidence and refresh the `epics.md` reconciliation paragraph; otherwise return the tracker row to `in-progress` or `review`.

**[Brownfield]** Eight §11.2 cells name stories whose `epics.md` bodies do not claim that NFR (§11.2 lines 422-436)
Line-scoped grep per `### Story` section: NFR1 -> 5.3 (0 hits); NFR6 -> 1.13 (0 hits); NFR14 -> 7.19 (0 hits); NFR16 -> 1.11, 1.12, 8.2, 8.3, 8.4, 8.5, 8.6 (0 hits each); NFR19 -> 8.8 (0 hits). The `epics.md` story traceability rule requires every story to name its NFR coverage, so these PRD cells assert ownership the epics do not declare. All other cells are backed.
Fix: narrow the ranges (NFR16 to `1.9-1.10, 1.13-1.15, 3.11-3.15, 4.9-4.15, 7.10, 8.7-8.11`; NFR19 to `8.1-8.7, 8.9-8.11`; drop 5.3 from NFR1, 1.13 from NFR6, 7.19 from NFR14) or add the missing NFR ids to the `epics.md` coverage lines if ownership is intended.

**[Adversarial; Rubric rated medium/low]** Story identifiers collide across repositories and reused numbers; SM4 cites IDs that now name different stories (§6.8 line 254; §9.2 line 337; §9.3 line 348; SM4 line 361; SM6 line 363)
"Before Story 8.6 resumes" and "Story 8.11 blocks Parties Story 8.7 migration" use Parties numbers, but EventStore Epic 8 has its own Story 8.6 (Azure Key Vault adapter) and 8.7 (Server persistence); FR36's done evidence names "Story 8.6" with no qualifier. "Stories 22.7a-d" is never explained as Parties. SM4 says oversized "Stories 4.8, 7.14, and 8.2" are replaced by children, but 7.14 and 8.2 are live `backlog` stories under reused numbers, so a reader checking SM4 against the tracker sees the "replaced" stories still pending.
Fix: qualify every foreign story as `Parties 8.6`, `Parties 22.7a-d`; rewrite SM4 as "former 4.8/7.14/8.2 umbrellas per `story-id-migration-2026-08-01.md`, now 4.9-4.15, 7.14/7.19/7.20, 8.2-8.11"; add "Parties" to the glossary.

### Medium (8)

**[Rubric; Adversarial]** Resume gate reads as still pending, has no re-run trigger, and provenance stops at 2026-08-16 (§1 line 74; §11.3 line 440; frontmatter lines 7-52)
"Implementation should not resume as a full Phase 4 package until ... implementation readiness is re-run" while readiness passed on 2026-08-01 and Epics 4-5 are in progress. Since the last re-run: the 2026-08-16 "major-change proposal", the 2026-09-06 NFR rewrite, and a rejected Epic 3 retrospective, with no re-run. `source_artifacts` omits seven approved proposals on disk (2026-08-01 story-3-11, 2026-08-12 x2, 2026-08-14, 2026-08-20 x2, 2026-08-29, 2026-09-07) despite the memlog rule that approved proposals must be provenance-listed even when text is unchanged.
Fix: restate as "Readiness re-run passed on 2026-08-01; the gate re-opens when any FR/NFR text changes or an epic retrospective is rejected"; list the missing proposals; either re-run readiness or record in §12 that the READY verdict predates the current text.

**[Rubric]** NFR5, NFR8, NFR17 remain adjective-bounded (§7 lines 274, 277, 286)
NFR5 "must remain bounded and metadata-only" and FR16 "bounded metadata" name no byte or field bound (Story 2.8 is `done`, so a real bound exists and can be lifted). NFR8 "bounded cost model" has no bound and does not say the bound is owed by `spec-projection-cost-sequence-guard.md`. NFR17 "resiliency targets" names no policy set. NFR7 is now acceptable via §1.1 and NFR16. Deferred 2026-07-16; still open.
Fix: NFR5: state the max detail-payload size and allowed field set from Story 2.8. NFR8: "bound defined by the approved spec at <path>; until approved, no Epic 6 implementation story may start." NFR17: name the DAPR resiliency policy set and target components.

**[Rubric]** Unbounded verbs with no consequence (FR16 line 178; FR33 line 231; FR34 line 241; FR5 line 158; NFR12 line 281; NFR19 line 288)
"DAPR notification support where needed"; "reduce projection replay cost"; "validate event metadata identity components"; "restore meaningful IntegrationTests CI coverage"; "or an approved equivalent" (approver unnamed); "such as" leaves the compatibility set open; "zeroed when no longer needed" has no observable point.
Fix: replace each with a named condition, e.g. FR34 "IntegrationTests lane runs on every push to main and asserts persisted state for at least the FR23/FR27/FR30 paths"; FR33 "replay of an already-current projection performs zero event reads".

**[Adversarial]** §6.1 "Done evidence" claims guardrails that are green by construction (§6.1 line 165)
"Guardrails prevent domain modules from reintroducing reusable platform boilerplate" is asserted for a `done` epic, but `epics.md:833` records "broad transitional host wiring and cross-file/computed route analysis remain explicit residual boundaries" and `deferred-work.md` DW-64/65/77/78/79 document that the guards are evadable by a non-`*Dapr*`-named handler, a non-literal setter, a constant route, or a non-`.cs` file.
Fix: restate done evidence as a falsifiable negative control ("a fixture domain module that adds `AddDaprClient` in a non-`*Dapr*`-named class fails test X") and list the accepted evasions as residual risk in §12.

**[Adversarial]** "Production path" is load-bearing in FR36, NFR6, NFR8, NFR16 but never defined, and the accepted evidence is fixture-instrumented (§4; NFR16 line 285; FR36 line 252)
Story 4.14's AC accepts "deterministic time support is clearly labeled while durable PostgreSQL state and restart observations remain production-path proof", implemented via test-owned `Oq8FileTimeProvider`, `Oq8HostingStartup`, and `Oq8BoundaryCounterStartupFilter` injected into the host; Story 1.20's approved runtime is a proof package. Whether a startup filter, a file-backed clock, or a proof-suffixed package counts is a judgment each story makes for itself.
Fix: add a glossary entry enumerating which layers must be real (OS process, DAPR sidecar, state component, actor placement, host pipeline) and which substitutions are permitted (time, external secrets, network); require each packet to declare its substitutions against that list.

**[Adversarial]** "Owner" approval is invoked fifteen times but the roles are never defined and, where recorded, the approver is the author (FR36 line 252; §6.8 line 254; SM6/SM7 lines 363-364; §11.3 lines 442-444)
The PRD requires "owner-reviewed", "explicit owner approval", "owner/security-approved", and a "release-owner authority record" but names only "EventStore owner" and "Product owners" as roles. Story 1.20's approvals were issued by the repository author. As written, "owner-approved" and "author-asserted" are indistinguishable, the condition FR36 was created to prevent for consumers.
Fix: define each owner role, state whether one person may hold two of them for one packet, and require a non-authorship control (sealed validator run in CI, or a second identity) wherever the PRD says "owner-approved".

**[Brownfield]** §11.3 pending/done assertions have drifted from the tracker (§11.3 lines 444, 449)
Line 444 says G5 stays pending "until Story 8.1's approved security specification authorizes Story 8.2"; `sprint-status.yaml:230` has 8.1 `done` with "Story 8.2 alone is authorized". Line 449 names `spec-folded-snapshot.md` as the required Story 6.1 output; that path does not exist, while `spec-6-1-folded-snapshot-frozen-spec.md` (draft, 2026-09-08) and `epic-6-context.md` do, and `epics.md` still binds 6.1/6.2 to the old path. The 1.20/3.12/3.13/3.14 `done` claims and Story 5.2's request-size limits are verified accurate.
Fix: rewrite line 444 as "Story 8.1 approved; 8.2 authorized, not started"; either rename the 6.1 spec to the bound path or update the PRD and `epics.md` path in one change.

**[Rubric; Adversarial rated low]** Glossary has not kept pace and key terms are overloaded (§4 lines 117-130)
Missing: OQ8, G5, goldens, parity packet, break-glass, provenance classification, durable admission, canonical-intent descriptor, retention tier, `pdenc-v2`, Parties, Folders, corrective release. "Parity" is used in eight senses (query, package, deployed-runtime, topology, dual-provider, tenant-filter, posture, source/package), each with a different proof standard; "closure", "gate", "evidence", and "authority" are undefined; "projection-backed provenance" (FR4, NFR8) is the load-bearing distinction and is not defined. G5/goldens were deferred 2026-07-16 pending authoritative terms, which AD-22/AD-23/AD-25 and the OQ8 design now supply.
Fix: add the entries, each pointing at the owning AD or spec, and one entry per parity kind with its proof standard.

### Low (6)

**[Rubric]** §5 Product Concerns restates §6 and §7 (lines 132-144)
No decision or extraction depends on it. Fix: fold into §2 or delete.

**[Rubric; Adversarial]** "All seven Phase 4 epics currently listed in `epics.md`" (§9.1 line 325)
`epics.md` lists eight; Epic 8 is post-MVP per §9.3. The §8.1 "latest validated compatible versions" bullet is a policy with no FR and no named implementer. Fix: "Epics 1-7 as listed in `epics.md`; Epic 8 is post-MVP (§9.3)"; attach the catalog policy to FR21 or name Story 3.16.

**[Rubric; Brownfield]** NFR3/NFR4 altitude, host scope, and a misplaced authorization clause (§7 lines 272-273)
NFR3 names `AllowInsecureSymmetricKey` but not the `AllowedAlgorithms` option it also constrains. "Role, and tenant validation remain mandatory in every mode" is not part of `JwtBearerAuthenticationContract` (an authorization concern). The PRD says "outside Development" generically; the epics AC narrows to gateway and Admin Server; the code is wired into three hosts including the Sample API. NFR4's "clearly named Development configuration" and "ephemeral developer tooling" are categories a scanner cannot enumerate.
Fix: name both options or neither; move the role/tenant clause to NFR1/NFR2; state the host set; enumerate the approved injection channels as a closed list.

**[Rubric]** §11.2 bare references lack paths (§11.3 line 446)
`story-id-migration-2026-07-15.md` and `story-id-migration-2026-08-01.md` are cited without their `_bmad-output/planning-artifacts/` path while every other artifact reference is fully pathed. Fix: path the two files.

**[Brownfield]** The two-platform OCI image-index constraint is enforced outside this repository (§8.1 line 300)
No EventStore build file declares the platform set; enforcement lives in Hexalith.Builds `publish-containers` reached via `release.yml:112` `domain-release.yml@22a578b5`, with EventStore-side evidence in `tools/release_evidence_handlers/v3.py:30`. Not violated, but unverifiable from this repo; three Builds identities (worktree `a32cb422`, gitlink `35c3d1e5`, pin `22a578b5`) are in play. Fix: add "enforced by the pinned Hexalith.Builds `publish-containers` gate; EventStore evidence handler `v3.py`" to the bullet.

**[Adversarial]** NFR18 requires documentation that does not exist and has no coverage row (NFR18 line 287; §11.2)
"AOT/trimming is explicitly not a target ... and that constraint must be documented." No file under `README.md` or `docs/` mentions AOT, `PublishAot`, or trimming as a non-target, and NFR18 is absent from §11.2. Fix: name the target document path and add an NFR18 row.

## Mechanical notes
- Provenance drift: commit `8312bced` (2026-09-06) changed §7 NFR3/NFR4 after the 2026-08-16 finalize; frontmatter still `status: final`, `updated: 2026-08-16`; no memlog entry; no proposal in `source_artifacts`. The same commit updated `epics.md` Story 5.3 AC, `epic-5-context.md`, `spec-3-15-*`, `spec-4-7-*`, and `deferred-work.md` (DW-493, DW-494), but not the `epics.md` Requirements Inventory.
- ID continuity: FR1-FR37 and NFR1-NFR19 unique and complete; presentation is feature-grouped, not numeric (FR25 in §6.3, FR26 in §6.5, FR27-FR31 in §6.4). Resolvable, intentional.
- Assumptions Index roundtrip: no inline `[ASSUMPTION]` tags; §13 declares none. Consistent. Zero `[NOTE FOR PM]` callouts anywhere.
- Cross-references: all §11.2 story IDs resolve in `epics.md` (through 7.20); 1.21 and 3.16 exist and are unreferenced. §11.3 spec paths (lines 449-451) do not exist yet, expected while 6.1/6.3/6.5 are `backlog`. Backlog artifact paths (lines 453-456), `ux.md`, `architecture.md` exist.
- Glossary drift: "break-glassed" (old NFR3, still in `epics.md` inventory) vs "break-glass option" (new NFR3); "Story 8.6"/"Story 8.7" straddle EventStore and Parties numbering; "Parties" used eleven times, never defined.
- Section 8.1 constraints verified against the repo: `.slnx`-only, per-project tests, Builds catalog import with zero local `PackageVersion` items, `UseHexalithProjectReferences` default false, no Dockerfiles, root-declared submodules only.
- MVP items still unbuilt (from the brownfield tables): Stories 5.5-5.10 (backlog), 6.1-6.6 (backlog), 7.1-7.14/7.19/7.20 (backlog), 3.16 (backlog), 3.15 (in-progress, 0/3 receipts), 4.7 (in-progress), 4.15 and 5.4 (review).
- Tracker hygiene outside PRD scope: `epic-2` rollup is `in-progress` although all Epic 2 stories are `done`; key `2-12-...-validatio` is truncated. Do not edit the guarded comment blocks.
- Repeated deferrals: every 2026-07-16 and 2026-07-19 rubric deferral remains open at this pass; the 2026-07-19 OCI/3.12/§11.3 items were applied that day and are closed.
- UJ protagonist naming: not applicable; no user journeys, by shape.

## Reviewer files
- `review-rubric-validate-2026-09-08.md`
- `review-adversarial-general-validate-2026-09-08.md`
- `review-brownfield-traceability-validate-2026-09-08.md`
- Prior reviews preserved: `review-rubric.md` (2026-07-19), `review-rubric-update-2026-08-16.md`, `review-editorial-*.md`
