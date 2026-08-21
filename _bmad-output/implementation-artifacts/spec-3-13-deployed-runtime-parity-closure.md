---
title: 'Story 3.13 v3.94.1 Deployed Runtime Evidence Disposition'
type: 'chore'
created: '2026-08-21'
status: 'in-review'
baseline_commit: '1d6e9321acfc416768c1c78e9facf573c9c41f71'
review_loop_iteration: 1
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-16.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The approved 2026-08-16 correct-course re-scoped Story 3.13 from positive
deployed-runtime parity to a negative evidence disposition, but the Story 3.13 spec and story record
still encode the superseded positive contract, and the focused verifier can only complete the story
on a `pass` verdict that immutable `v3.94.1` can never produce.

**Approach:** Assemble one content-bound disposition envelope over the retained `v3.94.1` review
subject `6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97`, and narrow the focused
verifier so exactly that rejected/non-authorizing shape — and nothing else — becomes story-completable.

## Boundaries & Constraints

**Always:** Leave every byte of both retained evidence trees, their checksum manifests, the reviewer
roster, and both proof packets unchanged; record the malformed `https` provenance labels, the absent
revision label, and `deployment_authorized: false` verbatim as failures; bind the envelope to the
existing subject digest by hash rather than regenerating it; keep Story 3.13 non-`done` until the
envelope and three role-bound receipts verify.

**Ask First:** Any external or remote mutation; any change outside Story 3.13 spec/story/evidence/
verifier/tracking/`docs/ci.md` files; renaming any story, spec, or proof-packet file; regenerating
the review subject or identity crosswalk; or credentials beyond configured read-only task access.

**Never:** Expose credentials; reinterpret, normalize, or omit a failed `v3.94.1` fact; emit a `pass` verdict, a non-null
selected deployed identity, or `deployment_authorized: true`; splice `1.20`, `v3.77.2`, or corrective
`3.14` lineages; claim positive FR36 deployed parity or substitute for Story 3.15; infer a receipt
from planning approval; mutate predecessors, Epic 1, runtime/release code, the package manifest, or
submodules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Complete rejected disposition | Envelope bound to subject `6cee8dad…` with `candidate: v3.94.1`, `candidate_disposition: rejected-non-authorizing`, `deployed_runtime_parity: unavailable-for-v3.94.1`, `selected_deployed_identity: null`, `deployment_authorized: false`, plus 3 role-bound receipts | Story-completable; Story 3.15 stays open | Any later byte change invalidates all receipts |
| Pass-shaped disposition | Envelope asserting `pass`, non-null selected identity, or `deployment_authorized: true` | Rejected fail-closed | Support-safe diagnostic naming the offending field |
| Omitted or normalized defect | A malformed `https` label rewritten, a revision label synthesized, or a retained blocker dropped | Rejected fail-closed | Diagnostic names the missing retained fact |
| Frozen-chain drift | Any retained checksum entry no longer matches its file | Rejected; revalidation trigger recorded | Never re-capture evidence to make it match |
| Incomplete acceptance | Fewer than 3 receipts, wrong role/filename binding, stale subject digest, or self-declared role | Rejected; story stays non-`done` | Planning approval is never a receipt |
| Cross-lineage splice | Envelope mixes `1.20`, `v3.77.2`, or `3.14` facts | Rejected fail-closed | Ancestry and labels are insufficient |

</frozen-after-approval>

## Code Map

**Authority (read-only)**

- `_bmad-output/planning-artifacts/epics.md:2645` -- the re-scoped story and its six replacement AC blocks (AC1-AC6). Verbatim source for the new story-record ACs.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-16.md:168` -- approved §4.1–4.4 old→new edits; §4.4 is the implementation boundary. §4.9:357 makes the key rename conditional.
- `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure-superseded-2026-08-16.md` -- verbatim archive of this spec's pre-re-scope content, including all 14 change-log entries and the closed Review Findings list.

**Frozen evidence — do not modify a byte**

- `evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd/` -- the SELECTED v3.94.1 tree (26 files + `packages/` with 14 nupkgs).
  - `review-subject.json` -- hashes to `6cee8dad…`; `proposed_decision: fail-closed`, 3 blockers, `identity.canonical_lineage_id: null`, all 3 `required_acceptances[].status: "missing"`.
  - `identity-crosswalk.json:56` `candidate_id: v3.94.1-selected-release`; `:242` `release_authority.deployment_authorized: false`; `:307`–`:309` the literal `https` for `image.source`/`.url`/`.documentation`; `:505` verdict `fail-closed`; `:520` the 3 blockers; `:566` `receipt_count: 0`.
  - `reviewer-roster.json:5` -- `authority_source` issue comment 5290564372; roles `eventstore-owner`/`release-owner` → `github:jpiquot`, `test-architect` → `bmad:murat`.
  - `evidence-sha256.txt` (3) / `evidence-core-sha256.txt` (34) / `nuget-sha256.txt` (14) / `predecessor-tree-sha256.txt` (40) -- verified 91/91 for this tree (3+34+14+40), 0 failed, 0 missing; 151 is the both-trees total. Each needs a different base dir; a wrong cwd yields false "No such file" noise, so grep for `: FAILED$` separately.
- `evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/` -- the HISTORICAL fail-closed tree. Read-only; never a source of facts for the envelope.
- `3-13-deployed-runtime-parity-closure-v3.94.1-proof-packet.md` (`684e5ced…`) and `3-13-deployed-runtime-parity-closure-proof-packet.md` (`349e0998…`) -- pinned by the two subjects; `DeployedRuntimeParityClosureTests.cs:35` is `ProofRelativePath`, a path constant for the *historical* packet only, and no test yet compares the selected subject's `proof_packet.sha256` to the file. Filenames are unrenameable. The v3.94.1 packet carries no verification record — the envelope must supply one.

**Verifier**

- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs` (8433 lines, 62 test methods).
  - `EvaluateClosure:4357` -- the positive gate. Requires all 12 `ExpectedChecks` `pass`, `verdict.decision == "pass"`, `story_may_be_done == true`, empty blockers (`:4409`–`:4416`), and is welded to `ApprovedSourceSha` = `fa2d1c99…` at `:4377`. **Keep it and its negative tests intact** — it is what proves a `pass` outcome stays rejected.
  - `ValidateActualFailClosedSubject:5817` -- real-tree fail-closed assertions; `:5906` already requires `deployment_authorized == false`.
  - `ValidateAcceptances:5544` -- 3/3 receipts, filename == `role + ".json"` (`:5624`), `decision == "accepted"` (`:5673`), exact `accepted_scope` string (`:5675`), limitations `SequenceEqual` the subject's (`:5678`), `durable_source` → `sources/<role>.json` (`:5679`).
  - `LoadReviewerRoster:6028` -- roster schema, `authority_source` shape, temporal chain `crosswalk.assembled_at ≤ roster.created_at ≤ subject.created_at`, pinned identities `:6095`–`:6101`. Throws rather than returning false.
  - `EvidenceDirectoryHasNoUnlistedFiles:6267` -- **closed inventory**: every file under the tree must be listed, and `acceptances/**` is admitted only when `receipt_count == 3` (frozen at 0). New artifacts therefore cannot live inside either tree.
  - Reuse: `ResolveWithin:7909` (reparse-point safe), `ComputeSha256:7958`/`:7960`, `ParseChecksumManifest:7879`, `VerifyChecksumManifest:7858`, `Binding:7852`, `ReadEvidenceFile:7906`, `FindRepositoryRoot:8418`.
  - **Trap:** a mutation test that changes bytes must rebind through `EvaluateWithFreshReview:7692` (or `PersistRuntimeBindings:7712` for runtime files); calling the gate directly lets `DeepEquals`/hash pins reject first, leaving the guard green by construction. This repo has hit that failure four times.
- `tools/release_evidence_codec.py:74` -- `canonical_bytes`/`canonical_sha256` (`sort_keys`, `separators=(",",":")`, trailing `\n`). Reuse for canonical envelope serialization; do **not** use `_publisher_canonical_bytes:442`, which is a different, indented canonicalization.

**Lifecycle surfaces**

- `_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md` -- ACs `:116`–`:153`, tasks `:155`–`:300`, completion status `:787`, and the now-superseded 2026-08-12 gate note `:808`.
- `_bmad-output/implementation-artifacts/sprint-status.yaml:218` -- key `3-13-v3-94-1-deployed-runtime-evidence-disposition`.
- `docs/ci.md` -- the Story 3.13 ownership paragraph.

## Tasks & Acceptance

**Execution:**

- [x] `3-13-deployed-runtime-parity-closure.md` -- replace the title, Story, AC1–AC4, and Tasks 1–10 with the re-scoped contract from `epics.md:2645`; resolve Tasks 4–7 as satisfied-by-retained-evidence per proposal §4.4 (registry readback, package download, runtime smoke, and subject regeneration are no longer required); rewrite `## Story Completion Status` and supersede the 2026-08-12 gate note, citing the 2026-08-16 approval.
- [x] `evidence/story-3-13/disposition/6cee8dad…/disposition-envelope.json` -- create the envelope **outside both frozen trees**, serialized with `canonical_bytes`, referencing the subject, crosswalk, core manifest, roster, and v3.94.1 proof packet by `{file,size,sha256}`; carry the five verbatim disposition fields, the 3 retained blockers, the malformed-label and absent-revision facts, and a verification record; add `disposition-sha256.txt` closing recursively over the directory.
- [ ] `DeployedRuntimeParityClosureTests.cs` -- add a disposition gate that accepts exactly the rejected/non-authorizing envelope bound to `6cee8dad…` and its 3 role-bound receipts, plus negative coverage for every matrix row; leave `EvaluateClosure` and its existing tests unchanged; leave the superseded-spec archive byte-verbatim and instead re-derive this spec's own Code Map anchors from the post-change file.
- [ ] `DeployedRuntimeParityClosureTests.cs` -- **reconcile the envelope against the frozen subject, not just against disk.** Assert that each `referenced_evidence.{identity_crosswalk,evidence_core_manifest,proof_packet}.sha256` and every `retained_identity` scalar equals the value recorded *inside* `review-subject.json` (`ba4e909e…`, `00136b53…`, `684e5ced…`). Without this the `6cee8dad…` pin is inert: re-declaring the envelope after a crosswalk or proof-packet edit keeps the whole suite green. Add negative cases that drift the retained bytes and the envelope declaration together.
- [ ] `DeployedRuntimeParityClosureTests.cs` -- **close the selected tree's inventory.** `EvidenceDirectoryHasNoUnlistedFiles` is reachable only via `EvaluateClosure`, which short-circuits on `source.sha != ApprovedSourceSha`, so the `80d12ef5…` tree is never inventory-checked and a planted file (including a forged receipt) survives with all checksums verifying. Assert an exact file-set over the selected tree, with a positive control and a stray-file negative case on a temp copy.
- [ ] `DeployedRuntimeParityClosureTests.cs` -- **emit a support-safe diagnostic.** The frozen matrix requires every rejection to name the offending field and carry a remediation or revalidation trigger; the gate currently returns a bare `bool` and swallows ten exception types, and all negative cases assert only `ShouldBeFalse()`. Return a structured rejection reason, assert the *specific* expected reason in every negative case, and make an unexpected reason (including a fixture fault reaching the catch) fail the case rather than pass it.
- [x] `docs/ci.md` and `sprint-status.yaml:218` -- state that Story 3.13 owns the v3.94.1 rejection only, that 3.14 owns the corrective release and 3.15 positive parity, and record why the story-key rename stays tracker-level (the proof-packet filenames are hash-pinned and unrenameable, so proposal §4.9's atomic-rename condition cannot be met).

**Acceptance Criteria:**

- Given the envelope and verifier are complete but no receipts exist, when the suite runs, then the disposition is verifiable, acceptance reports 0/3, and Story 3.13 reaches `review` — never `done`.
- Given the story is later marked `done`, when the retained result is read, then it selects no image, authorizes no deployment or consumer migration, leaves positive FR36 parity open for Story 3.15, and creates no 3.13→3.14 dependency.
- Given the whole change set, when both trees are re-verified, then all 151 checksum entries still pass and `git diff` shows no byte changed under either content-addressed evidence directory.

## Design Notes

The envelope must live outside the two content-addressed trees, because the frozen crosswalk pins
`receipt_count: 0`, so adding files inside would force a crosswalk edit that invalidates the very
subject the envelope cites. Do **not** rely on the inventory guard as the reason: although
`EvidenceDirectoryHasNoUnlistedFiles:6267` exists, it is reachable only through `EvaluateClosure`,
which short-circuits on `source.sha != ApprovedSourceSha`, so it never runs against the selected
`80d12ef5…` tree. Closing that hole is a task above, not an assumption. Receipts are already designed to sit outside hashed evidence
(`approval_contract.outside_hashed_evidence: true`), which is what makes 3/3 reachable later without
re-hashing the subject.

Reuse the frozen receipt schema at `DeployedRuntimeParityClosureTests.cs:73` rather than inventing
one; the receipts' `accepted_scope` is the only string that must change, because what the reviewers
accept is a rejection, not a closure.

## Verification

**Commands:**
- `(cd _bmad-output/implementation-artifacts/evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594 && sha256sum -c critical-evidence-sha256.txt)` -- expected: all 33 frozen predecessor entries pass.
- `(cd _bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd && sha256sum -c evidence-sha256.txt && sha256sum -c evidence-core-sha256.txt && (cd packages && sha256sum -c ../nuget-sha256.txt))` -- expected: 51 OK, zero lines matching `: FAILED$`.
- `(cd _bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd && sha256sum review-subject.json)` -- expected: `6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97`.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings, zero errors.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 --no-restore -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --filter FullyQualifiedName~DeployedRuntimeParityClosureTests` -- expected: all focused tests pass, zero skipped; count strictly above the 190 baseline.
- `git status --porcelain _bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28 _bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` -- expected: empty output.
- `npx markdownlint-cli2 docs/ci.md` -- expected: passes.

### Review Findings (2026-08-21 loop, triaged — NOT yet applied)

**bad_spec (high) — trigger a loopback; root cause is in this spec's non-frozen sections:**

- [ ] [Review][bad_spec] The envelope's `referenced_evidence` hashes are validated against files on disk but never against the hashes the frozen subject itself records (`identity_crosswalk` `ba4e909e…`, `evidence_core_manifest` `00136b53…`, `proof_packet` `684e5ced…`). Those three digests appear zero times in the verifier. Editing the crosswalk or proof packet and re-declaring the envelope keeps every test green while the `6cee8dad…` pin goes stale, so AC1's content-binding is asserted, not enforced. Task 2 asked only for `{file,size,sha256}` bindings.
- [ ] [Review][bad_spec] The selected v3.94.1 tree has no reachable closed-inventory assertion. `EvidenceDirectoryHasNoUnlistedFiles:6267` is reached only through `EvaluateClosure:4385`, which short-circuits at `:4379` on `source.sha != ApprovedSourceSha` (`fa2d1c99…`); the selected tree is `80d12ef5…`. A stray file — including a hand-planted receipt — can be committed inside the frozen packet with all 151 checksum entries still verifying. This spec's Design Notes assert the opposite as the justification for the envelope's placement.
- [ ] [Review][bad_spec] No support-safe diagnostic exists. `EvaluateDisposition` returns a bare `bool` and swallows ten exception types; three frozen I/O-matrix rows require a diagnostic naming the offending field plus a remediation or revalidation trigger. All ~46 negative cases assert only `ShouldBeFalse()`, so no test can detect the absence, and a fixture fault would silently keep a negative case green.

**intent_gap (medium) — root cause inside the frozen block; needs human resolution:**

- [ ] [Review][intent_gap] The regenerated frozen `Never` list dropped the prior intent's credential-exposure prohibition. It now survives only in `DocumentIsSupportSafe`, not in human-owned intent.

**patch (survive loopback):**

- [ ] [Review][Patch] Code Map attributes "verified 151/151" to the selected tree's four manifests; that tree totals 91 (3+34+14+40). 151 is the both-trees figure.
- [ ] [Review][Patch] Verification expects a count "strictly above the 186 baseline"; the real pre-change baseline is 190 (186 is stale from an earlier loop).
- [ ] [Review][Patch] Code Map says `epics.md:2645` supplies "five replacement AC blocks"; there are six (AC1–AC6).
- [ ] [Review][Patch] Code Map claims the v3.94.1 proof packet is "pinned by ... `DeployedRuntimeParityClosureTests.cs:35`"; line 35 is `ProofRelativePath`, a path constant for the *historical* packet, and no test compares the selected subject's `proof_packet.sha256` to the file.
- [ ] [Review][Patch] `sprint-status.yaml` retains a comment reading "Story 3.13 remains in-progress until its exact negative disposition is accepted" directly above a row whose value is now `review`.
- [ ] [Review][Patch] Story record Task 2 says all four checksum manifests exist "in both content-addressed trees"; the historical tree has no `nuget-sha256.txt` (3+17+40 = 60).
- [ ] [Review][Patch] `receipts.ShouldBe(0)` after a rejected evaluation is green by construction — `EvaluateDisposition` zeroes the out-param on every failure path.
- [ ] [Review][Patch] The role-dedup guard at `:4069` can never fire; the `duplicate-role` case is decided by the filename guard at `:3998`.
- [ ] [Review][Patch] Verification dropped the Story 1.20 `critical-evidence-sha256.txt` check the prior spec carried.
- [ ] [Review][Patch] `docs/ci.md` has an orphaned 42-char line and renders the digest with a Unicode ellipsis, so the operator-facing surface cannot be used to verify it.

**defer:**

- [ ] [Review][Defer] Receipt `source_url` requires a `#story-3-13-…` commit anchor that GitHub cannot produce (real anchors are `#commitcomment-<id>`), so the 3/3 path is reachable only by hand-authored fixtures. Pre-existing pattern inherited from `ValidateAcceptances`.
- [ ] [Review][Defer] Two canonicalizers now define one authority — Python `canonical_bytes` for authoring, C# `CanonicalDispositionBytes` for verification — with no equivalence test for non-ASCII or line-separator input.
- [ ] [Review][Defer] Evidence JSON has no `.gitattributes` `eol=lf` pin; a `core.autocrlf` clone breaks both the canonical byte compare and the manifest.
- [ ] [Review][Defer] `ForeignLineageTokens` is hand-maintained with no completeness guard; it omits the two explicitly voided subject digests (`394292a2…`, `93d70d51…`).


## Spec Change Log

- 2026-08-21 (review loop 1, bad_spec amendment): three confirmed high findings root-caused to this
  spec's non-frozen sections, not to the implementation. (1) Task 2 asked only for `{file,size,sha256}`
  bindings, so the envelope was reconciled against disk but never against the hashes the frozen subject
  itself records -- leaving `6cee8dad…` inert, since re-declaring the envelope after a crosswalk or
  proof-packet edit keeps the suite green. (2) The Design Notes asserted both trees were closed
  inventories; `EvidenceDirectoryHasNoUnlistedFiles` is in fact unreachable for the selected `80d12ef5…`
  tree because `EvaluateClosure` short-circuits on `ApprovedSourceSha`, so a planted file survives with
  all checksums verifying. (3) No task required the support-safe diagnostic the frozen matrix demands,
  so the gate returns a bare `bool` and ~46 negative cases assert only `ShouldBeFalse()` -- the vacuous-
  guard shape this repository has hit five times. All three are now explicit tasks. The known-bad state
  avoided is a disposition that *looks* content-bound and closed while enforcing neither.

  Frozen intent was renegotiated with the human on 2026-08-21 to restore the credential clauses that
  template regeneration dropped: `Never ... expose credentials` and `Ask First ... credentials beyond
  configured read-only task access`. `DocumentIsSupportSafe` covers evidence *content*, but nothing
  constrained implementer *behaviour* or covered non-evidence surfaces.

  `review_loop_iteration` was reset from 15 to 1 on human authority: the 14 prior loops were spent on
  the superseded positive-parity contract, and this is the re-scoped contract's first review.

  **KEEP (must survive re-derivation):** the disposition envelope's canonical bytes and its
  `7ff7e150…` digest, which reproduce exactly under `release_evidence_codec.canonical_bytes`; its five
  verbatim disposition fields; the placement outside both frozen trees; the verbatim re-derivation of
  the six malformed `https` labels and two absent-`revision` facts from `child-linux-*.config.raw`;
  `EvaluateClosure` and every pre-existing test byte-unchanged; the story-record re-scope taken verbatim
  from `epics.md:2645`; the byte-verbatim superseded archive; and the untouched state of both
  content-addressed evidence trees. Deviation from the standard bad_spec revert, recorded deliberately:
  the three fixes are strictly additive to a verifier that is otherwise green at 240/240 and
  mutation-audited, so the implementer is re-engaged against the amended spec instead of the +1555-line
  file being reverted and regenerated, which would risk losing verified work for no contract benefit.

- 2026-08-21 (post-implementation correction): reverted the two line-anchor edits made to
  `spec-3-13-deployed-runtime-parity-closure-superseded-2026-08-16.md`; it is byte-verbatim with the
  pre-re-scope spec at `f8b514f3` again. Those anchors were accurate when written, so retargeting them
  corrupted the historical record rather than fixing it -- the stale anchors were in this spec's live
  Code Map instead. Task 3's closing clause was misdirected and has been amended to say so. Re-derived
  every Code Map anchor for `DeployedRuntimeParityClosureTests.cs` from the post-change file: the
  insertion is purely additive, so all anchors at or after the old `:2802` shift by exactly +1555
  (`EvaluateClosure` is at `:4357`, not the `:4275` recorded above; the new `EvaluateDisposition` gate
  is at `:3457`), and the file is now 8433 lines with 62 test methods. No evidence byte was touched.

- 2026-08-21 (implementation): executed all four tasks. Created the canonical disposition envelope
  and its recursive checksum manifest at
  `evidence/story-3-13/disposition/6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97/`
  (envelope SHA-256 `7ff7e1501d1cdb49307f820dcdd0d8abc15bf2eee01c9e7450fc54255d8dfba4`), re-scoped the
  story record, added the focused disposition gate plus matrix-complete negative coverage, and
  updated `docs/ci.md` and `sprint-status.yaml`. Two clarifications to the task list: (1) the stale
  `:545`/`:2691` anchors exist only in
  `spec-3-13-deployed-runtime-parity-closure-superseded-2026-08-16.md`, so that archive is no longer
  byte-verbatim -- exactly those two line anchors were retargeted at `EvaluateClosure`, and nothing
  else in it changed; (2) inserting the disposition gate moved `EvaluateClosure` from `:2802` to
  `:4275`, so both retargeted anchors use `:4275` rather than the `:2802` value this spec's Code Map
  recorded against the pre-change file. `EvaluateClosure` and every pre-existing test are unchanged.
  No retained evidence byte changed: all 151 checksum entries still verify and `git status` is empty
  for both content-addressed evidence directories.

- 2026-08-21: Re-scoped the spec to the approved 2026-08-16 correct-course decision. The frozen intent,
  boundaries, matrix, Code Map, and tasks now describe the `v3.94.1` rejected/non-authorizing evidence
  disposition instead of positive deployed-runtime parity, which immutable `v3.94.1` provably cannot
  satisfy. The prior content — 14 review-loop change-log entries and the closed Review Findings list —
  is preserved verbatim in `spec-3-13-deployed-runtime-parity-closure-superseded-2026-08-16.md`. The
  2026-08-12 "no further `bmad-build`" gate is superseded: it reserved terminal re-scoping for another
  explicit Correct Course decision, which the 2026-08-16 proposal is. No evidence byte was changed.
