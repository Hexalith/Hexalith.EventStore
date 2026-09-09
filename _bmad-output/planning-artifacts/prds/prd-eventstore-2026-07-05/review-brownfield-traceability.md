# Brownfield Traceability Review — EventStore Phase 4 PRD

**Review date:** 2026-09-09  
**Artifact reviewed:** `_bmad-output/planning-artifacts/prd.md` (`status: final`, `updated: 2026-09-08`)  
**Addendum:** none present  
**Verdict:** **FAIL — the PRD is not a reliable current traceability baseline and must not support a `READY` implementation-readiness verdict.** Two blocking integrity failures remain: section 11.1 still publishes ownership data that the same document says was retired, and the supposedly authoritative epic plan is bound by stale input digests. The repository also contains newer delivery and specification state that the PRD either contradicts or does not record.

## Critical findings

### C1. Section 11.1 still publishes the retired, known-wrong FR ownership map

**PRD evidence:** `prd.md:427-467`, `prd.md:400`, `prd.md:537-541`.

- FR1 is mapped to Story `4.11` at line 429 and line 467 says its owner sits in Epic 4. The same PRD's retired-OR12 record at lines 539-541 says that was a misread and the real sole primary owner is Story `1.11` in Epic 1. `epics.md:825-831` confirms Story 1.11 declares primary FR1; `epics.md:3397-3403` declares Story 4.11 owns a slice of FR27, not FR1.
- FR11, FR13, FR19, FR21, FR22, and FR25 are still labelled “multiple primary owners (OR12)” at lines 439, 441, 447, 449, 450, and 453. Current `epics.md` explicitly makes Stories 2.1, 2.3, 3.3, 3.5, 3.6, and 3.7 their sole primary owners and demotes the other listed stories to supporting coverage (`epics.md:1385-1391`, `1480-1486`, `2084-2090`, `2186-2192`, `2252-2258`, `2308-2314`, `2593-2599`, `2695-2701`, `2813-2821`).
- FR12 and FR15 are still described as ambiguous multiple-primary rows even though `epics.md:386-390` now defines approved disjoint slices and completion rules.
- SM2 at `prd.md:400` still says OR12 is open, while `prd.md:535-541` says OR12 is retired.

This is not merely stale prose: section 11.1 calls its third column “Owning stories,” says it exists so SM2 can be read from one page, and is the PRD's primary story trace. It currently gives mutually exclusive answers inside the same final artifact.

**Fix:** regenerate section 11.1 from the current `Requirements coverage` declarations in `epics.md`; set FR1 to Story 1.11; distinguish sole primary owners, disjoint primary slices, and supporting stories; remove every OR12 warning; and update SM2 plus line 467 in the same change. Add the proposed machine check so retired ownership findings cannot remain in the live map.

### C2. The authoritative epic plan fails its own source-drift gate

**Evidence:** `epics.md:13-18`, `epics.md:374`, `epics.md:400`; current file digests computed on 2026-09-09.

| Input | Digest recorded in `epics.md` | Current SHA-256 | Result |
| --- | --- | --- | --- |
| `prd.md` | `8f9c88e8b8665c2ded07a6a4df88db95d04339e5c728ef361ee9fbe9c115a699` | `39e22f7ca480157b04e66c9de3dfa27c5fbbead837ac780c61a0a16e234bc475` | mismatch |
| `architecture.md` | `623bc23e453aba5a703c5aa1b208bf9f985f5937f5900f5e665f5cb5abe5ca94` | `2b96a810f34f3cc40ee2ef5fc0f9b1cfaf01a395c73d1321953a946162fe945b` | mismatch |
| `DESIGN.md` | `3be78b6b856d3bb8e76451ebbfa550d9018bd4bce05e3dc4a2968758f7abf83e` | same | match |
| `EXPERIENCE.md` | `6a058112512b3dcc4468bdf698d6949345ab7ba3844e1a61b725f9a0aca38a3c` | same | match |
| `ux.md` | `3c827e922c2a05559eac09ad3bff638fed0e2aca789eacd733dbb904e4a42c8c` | same | match |

The epic plan's source-drift rule says changed inputs require reconciliation and renewed approval, and its historical-continuity rule calls these five documents requirements authority. The PRD simultaneously says `epics.md` is authoritative for slicing and sequencing (`prd.md:75-86`). Until the digest mismatch is reconciled, neither artifact can prove that the current stories implement the current PRD/architecture baseline.

**Fix:** reconcile the current PRD and architecture deltas through the epic plan, replace the two stored digests with the exact reconciled bytes, rerun the epic final-validation/source-drift check, and preserve the renewed approval evidence. Do not merely update hashes without reviewing the changed requirements and decisions.

## High findings

### H1. NFR7 declares duplicate-side-effect delivery before the non-weakenable Story 4.15 closure gate has passed

**Evidence:** `prd.md:92-96`, `prd.md:311`, `epics.md:3629-3667`, `sprint-status.yaml:180`, `spec-4-15-oq8-platform-closure-and-handoff.md:5`.

PRD section 1.1 says nothing may weaken the 4.9-4.15 sequence or Story 4.15 closure gate. NFR7 nevertheless labels loss class (e) “guarded, delivered” by Stories 4.9-4.13. Current lifecycle authority is not closed: the tracker says Story 4.15 is `review`; its wrapper says `done`; and `epics.md:3643` explicitly records that conflict and says lifecycle must be reconciled before the story is called done. The repository validator confirms the gate is red: `python3 tools/validate-oq8-platform-evidence.py --pre-review` exits 1 with `Lifecycle status drift: 4-15-oq8-platform-closure-and-handoff`.

**Fix:** change class (e) to implemented/evidence-complete but closure-pending until one authoritative lifecycle is reconciled and the current validator passes, or complete the approved Story 4.15 lifecycle transition and then retain the delivered claim with the passing command/result. Update SM11 consistently.

### H2. The PRD claims all tracker/epic contradictions are in section 12, but at least three newer contradictions are absent

**Evidence:** `prd.md:467`, `prd.md:516-535`; current tracker, story wrappers, and `epics.md` reconciliation paragraphs.

| Story | `sprint-status.yaml` | Story wrapper | `epics.md` current reconciliation | PRD section 12 |
| --- | --- | --- | --- | --- |
| 5.2 | `done` (`:185`) | `in-review` | “remains backlog” (`epics.md:3760`) | absent |
| 5.4 | `review` (`:193`) | `done` | “remains backlog” (`epics.md:3870`) | absent |
| 6.1 | `review` (`:204`) | `done` | “remains backlog” and artifact absent (`epics.md:4261`) | represented only as obsolete path drift (OR5) |

Story 4.15's conflict is at least recorded in `epics.md`, but not in PRD section 12. These contradictions appeared after the PRD's reconciliation and show that its delivery annotations cannot safely be read from the tracker as line 467 promises.

**Fix:** reconcile each story against its completion gate and immutable evidence; then update tracker, wrapper state, epic reconciliation, and PRD delivery annotations atomically. Add a gate that prevents a tracker transition from diverging from the story wrapper and epic reconciliation.

### H3. Story 6.1's required path and authorization are now the opposite of what the PRD says

**PRD evidence:** `prd.md:461`, `prd.md:506-509`, `prd.md:526`.  
**Repository evidence:** `_bmad-output/implementation-artifacts/spec-folded-snapshot.md:1-32,613-655`; `spec-6-1-folded-snapshot-frozen-spec.md:1-7`; `sprint-status.yaml:204`; `epics.md:4261,4305-4308,4324`.

The PRD says Story 6.1 is all-backlog, the gate path is in drift, and the actual artifact is only `spec-6-1-folded-snapshot-frozen-spec.md`. Current repository truth is that the exact path required by `epics.md` — `_bmad-output/implementation-artifacts/spec-folded-snapshot.md` — exists, has `status: approved-authorized`, records `story_6_2_authorized: true`, has no open decisions, and carries a named approval. Its normative-region SHA-256 recomputes to the recorded `0b456b5fcc49c6f7431e3476cefd073c184cf181d64bf84fb76414c1110d16b2`. The wrapper is `done`; only the tracker remains `review`, while `epics.md` still says the canonical artifact is absent and Story 6.2 unauthorized.

**Fix:** retire OR5 as resolved by the canonical artifact, reconcile Story 6.1 lifecycle, update the FR33 row and section 11.3, and update the Story 6.1/6.2 reconciliation paragraphs. Preserve the exact normative digest and approval; do not infer Story 6.2 runtime completion from its authorization.

### H4. The governing OQ8 authority remains unreproducible from EventStore

**Evidence:** `prd.md:90-96`, `architecture.md:60-65`, `epics.md:149`, `tools/validate-oq8-platform-evidence.py:62-64,2071-2082,3708-3713`.

The PRD correctly discloses that Hexalith.Folders commit `a9cfea91c8a987ef7a836c216e633a92321fc3c2` and `docs/exit-criteria/oq8-idempotency-design.md` are external and that the bytes are not tracked here. That external document supersedes parts of FR27, NFR7, and NFR16, so it is normative rather than informational. EventStore's validator contains only a hard-coded version/digest and compares packet declarations to that constant; it does not possess or hash the governing bytes. The repository therefore cannot independently prove that the cited commit contains the approved bytes or reconstruct the superseding clauses. The repository/path/commit identity still appears only in the PRD; architecture, epics, and evidence retain the digest without full provenance.

**Fix:** retain an immutable, license-permitted copy of the exact governing bytes or a Folders-produced signed/content-addressed attestation that binds repository, path, commit, and digest; make the validator verify that authority rather than a local constant; and propagate the full identity into architecture, epics, and OQ8 evidence. Until then, keep this as a readiness exception requiring external verification, not a self-contained EventStore proof.

### H5. The architecture handoff names an obsolete PRD authority and predates the ratified requirements it claims to bind

**Evidence:** `architecture.md:8-16,60-71`; `prd.md:3-5,73-96,305-323`.

Architecture is `final`, updated 2026-08-29, and says for deployed-runtime parity “the final PRD updated 2026-08-16 govern[s].” The current PRD is updated 2026-09-08 and ratifies changed NFR3/NFR4, revised OQ8 provenance/supersession boundaries, and changed traceability/status conclusions. Architecture frontmatter nevertheless claims to bind FR1-FR37 and NFR1-NFR19. Combined with C2's digest mismatch, the referenced architecture is not demonstrably the architecture handoff for the current PRD.

**Fix:** reconcile architecture against the 2026-09-08 PRD/proposals, correct the obsolete authority sentence and OQ8 full identity, update its date/provenance, then update the epic input digest and rerun readiness.

## Medium findings

### M1. The PRD omits the proposal that authorized its own OR2/OR3/OR12 retirement

**Evidence:** `prd.md:7-66`, `prd.md:537-541`, `.memlog.md` final entries; `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-08.md`.

The frontmatter includes the NFR3/NFR4 ratification proposal but not `sprint-change-proposal-2026-09-08.md`, even though section 12 cites that proposal as the authority for retiring three refinements and the memlog says the correct-course run applied it. This makes the document's declared `source_artifacts` incomplete for its latest ownership and status changes.

**Fix:** add the exact proposal path to `source_artifacts` when correcting section 11, and ensure every consequential proposal cited in the body is provenance-listed.

### M2. NFR18 has neither its required normative document nor an owning story

**Evidence:** `prd.md:322`, `prd.md:491`, `prd.md:527`; filesystem check on 2026-09-09.

`docs/reference/aot-and-trimming-posture.md` is absent, and the PRD explicitly says no story owns it. The trace table lists only supporting references in Stories 6.5-6.6, so NFR18 is not actionable or closeable.

**Fix:** create an owning story with the exact document as its deliverable and a test/documentation check, then keep the NFR open until that artifact exists and is approved.

### M3. The promised correctness gate is still unbound while status drift continues

**Evidence:** `prd.md:533` (OR13) and the lifecycle contradictions in H1/H2.

OR13 asks for a named test lane and trigger before a high-risk-NFR story can be marked done, but neither is bound. The current 4.15, 5.2, 5.4, and 6.1 disagreements demonstrate that this is an active control gap, not editorial debt.

**Fix:** name the exact command/lane, trigger, required artifacts, and status-transition rule; make it fail when tracker, wrapper, and epic reconciliation disagree; require that gate before future `done` transitions.

## Low findings

### L1. Tracker rollups remain stale for Epic 2

**Evidence:** `sprint-status.yaml:101-114`.

All twelve Epic 2 stories and its retrospective are `done`, but `epic-2` remains `in-progress`. The Story 2.12 key is also truncated to `...-validatio`. This does not change FR ownership, but it makes epic-level delivery reporting unreliable.

**Fix:** correct the rollup and key through the tracker owner's guarded process, preserving all contract-bound comment blocks.

### L2. The OCI platform-set constraint is delegated to an external Builds pin

**Evidence:** `prd.md:335`; `tools/release_evidence_handlers/v3.py`; `.github/workflows/release.yml` and the Hexalith.Builds submodule/pinned workflow.

The PRD accurately discloses that no EventStore build file declares the exact `linux/amd64` + `linux/arm64` set. EventStore evidence code asserts it, but enforcement is delegated to a pinned shared workflow whose source identity can differ from the checked-out Builds gitlink/worktree. This is a lower-risk reproducibility caveat, not a current contradiction.

**Fix:** record the exact shared-workflow commit plus EventStore evidence-handler identity in release evidence, and test that the caller pin and validated workflow bytes are the same authority.

## Verification performed

- Repository baseline and root guidance were read; working tree was clean on `main...origin/main` before this review.
- All 37 FRs and 19 NFRs were compared with current `epics.md` story declarations and `sprint-status.yaml`.
- Current input SHA-256 values were recomputed with `sha256sum`.
- Story 6.1's normative digest was recomputed successfully as `0b456b5fcc49c6f7431e3476cefd073c184cf181d64bf84fb76414c1110d16b2`.
- `python3 tools/validate-oq8-platform-evidence.py --pre-review` failed with `Lifecycle status drift: 4-15-oq8-platform-closure-and-handoff`.
- Required paths checked: `deploy/dapr/resiliency.yaml`, `tools/release-packages.json`, `tools/release_evidence_handlers/v3.py`, and `spec-folded-snapshot.md` exist; `docs/reference/aot-and-trimming-posture.md`, `spec-projection-cost-sequence-guard.md`, and `spec-event-versioning-upcasting.md` do not. The latter two are expected backlog gates; the first is unowned NFR18 debt.

No PRD, architecture, epic, source, test, tracker, or addendum file was modified by this review.
