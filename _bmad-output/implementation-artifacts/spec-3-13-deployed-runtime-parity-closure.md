---
title: 'Story 3.13 v3.94.1 Deployed Runtime Evidence Disposition'
type: 'chore'
created: '2026-08-21'
status: 'done'
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

- `evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd/` -- the SELECTED v3.94.1 tree (38 files: 24 root files plus 14 nupkgs under `packages/`).
  - `review-subject.json` -- hashes to `6cee8dad…`; `proposed_decision: fail-closed`, 3 blockers, `identity.canonical_lineage_id: null`, all 3 `required_acceptances[].status: "missing"`.
  - `identity-crosswalk.json:56` `candidate_id: v3.94.1-selected-release`; `:242` `release_authority.deployment_authorized: false`; `:307`–`:309` the literal `https` for `image.source`/`.url`/`.documentation`; `:505` verdict `fail-closed`; `:520` the 3 blockers; `:566` `receipt_count: 0`.
  - `reviewer-roster.json:5` -- `authority_source` issue comment 5290564372; roles `eventstore-owner`/`release-owner` → `github:jpiquot`, `test-architect` → `bmad:murat`.
  - `evidence-sha256.txt` (3) / `evidence-core-sha256.txt` (34) / `nuget-sha256.txt` (14) / `predecessor-tree-sha256.txt` (40) -- verified 91/91 for this tree (3+34+14+40), 0 failed, 0 missing; 151 is the both-trees total. Each needs a different base dir; a wrong cwd yields false "No such file" noise, so grep for `: FAILED$` separately.
- `evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/` -- the HISTORICAL fail-closed tree. Read-only; never a source of facts for the envelope.
- `3-13-deployed-runtime-parity-closure-v3.94.1-proof-packet.md` (`684e5ced…`) and `3-13-deployed-runtime-parity-closure-proof-packet.md` (`349e0998…`) -- pinned by the two subjects; `DeployedRuntimeParityClosureTests.cs:35` is `ProofRelativePath`, a path constant for the *historical* packet only, and no test yet compares the selected subject's `proof_packet.sha256` to the file. Filenames are unrenameable. The v3.94.1 packet carries no verification record — the envelope must supply one.

**Verifier**

- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs` (9939 lines, 72 test methods).
  - `EvaluateDisposition:4188` -- the negative Story 3.13 gate. It enforces canonical bytes, the closed disposition inventory, frozen-subject bindings, exact retained defects/limitations, whole-envelope foreign-lineage rejection, chronology, and three envelope-addressed receipts.
  - `RejectSelectedEvidenceInventory:4905` and `RejectDispositionManifest:5442` -- separate closed inventories for the frozen selected tree and the mutable disposition/receipt directory.
  - `RejectForeignLineage:5104` and `RejectDispositionReceipt:5245` -- recursively scan the complete envelope and validate each receipt plus its durable source with injectable validation time.
  - `EvaluateClosure:5830` -- the preserved positive gate. It remains welded to `ApprovedSourceSha = fa2d1c99…`; keep it and its negative tests intact.
  - `ValidateAcceptances:7017`, `ValidateActualFailClosedSubject:7290`, `LoadReviewerRoster:7501`, and `EvidenceDirectoryHasNoUnlistedFiles:7740` -- retained predecessor behavior.
  - Reuse: `ResolveWithin:9396` (reparse-point safe), `ComputeSha256:9445`/`:9447`, `ParseChecksumManifest:9366`, `VerifyChecksumManifest:9345`, `Binding:9339`, `ReadEvidenceFile:9393`, `FindRepositoryRoot:9924`.
  - **Trap:** a positive-gate mutation test that changes bytes must rebind through `EvaluateWithFreshReview:9179` (or `PersistRuntimeBindings:9199` for runtime files); calling the gate directly lets upstream byte pins reject first.
- `tools/release_evidence_handlers/v3.py:76`/`:83` -- the Python `canonical_bytes`/`canonical_sha256` authoring authority; `_publisher_canonical_bytes:448` is deliberately different and indented. `tools/release_evidence_codec.py` is now an 11-line facade.

**Lifecycle surfaces**

- `_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md` -- ACs `:116`–`:153`, tasks `:155`–`:300`, completion status `:787`, and the now-superseded 2026-08-12 gate note `:808`.
- `_bmad-output/implementation-artifacts/sprint-status.yaml:218` -- key `3-13-v3-94-1-deployed-runtime-evidence-disposition`.
- `docs/ci.md` -- the Story 3.13 ownership paragraph.

## Tasks & Acceptance

**Execution:**

- [x] `3-13-deployed-runtime-parity-closure.md` -- replace the title, Story, AC1–AC4, and Tasks 1–10 with the re-scoped contract from `epics.md:2645`; resolve Tasks 4–7 as satisfied-by-retained-evidence per proposal §4.4 (registry readback, package download, runtime smoke, and subject regeneration are no longer required); rewrite `## Story Completion Status` and supersede the 2026-08-12 gate note, citing the 2026-08-16 approval.
- [x] `evidence/story-3-13/disposition/6cee8dad…/disposition-envelope.json` -- create the envelope **outside both frozen trees**, serialized with `canonical_bytes`, referencing the subject, crosswalk, core manifest, roster, and v3.94.1 proof packet by `{file,size,sha256}`; carry the five verbatim disposition fields, the 3 retained blockers, the malformed-label and absent-revision facts, and a verification record; add `disposition-sha256.txt` closing recursively over the directory.
- [x] `DeployedRuntimeParityClosureTests.cs` -- add a disposition gate that accepts exactly the rejected/non-authorizing envelope bound to `6cee8dad…` and its 3 role-bound receipts, plus negative coverage for every matrix row; leave `EvaluateClosure` and its existing tests unchanged; leave the superseded-spec archive byte-verbatim and instead re-derive this spec's own Code Map anchors from the post-change file.
- [x] `DeployedRuntimeParityClosureTests.cs` -- **reconcile the envelope against the frozen subject, not just against disk.** Assert that each `referenced_evidence.{identity_crosswalk,evidence_core_manifest,proof_packet}.sha256` and every `retained_identity` scalar equals the value recorded *inside* `review-subject.json` (`ba4e909e…`, `00136b53…`, `684e5ced…`). Without this the `6cee8dad…` pin is inert: re-declaring the envelope after a crosswalk or proof-packet edit keeps the whole suite green. Add negative cases that drift the retained bytes and the envelope declaration together.
- [x] `DeployedRuntimeParityClosureTests.cs` -- **close the selected tree's inventory.** `EvidenceDirectoryHasNoUnlistedFiles` is reachable only via `EvaluateClosure`, which short-circuits on `source.sha != ApprovedSourceSha`, so the `80d12ef5…` tree is never inventory-checked and a planted file (including a forged receipt) survives with all checksums verifying. Assert an exact file-set over the selected tree, with a positive control and a stray-file negative case on a temp copy.
- [x] `DeployedRuntimeParityClosureTests.cs` -- **emit a support-safe diagnostic.** The frozen matrix requires every rejection to name the offending field and carry a remediation or revalidation trigger. Return a structured rejection reason, assert the *specific* expected reason in every negative case, and make an unexpected reason (including a fixture fault reaching the catch) fail the case rather than pass it.
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

Reuse the frozen receipt schema at `DeployedRuntimeParityClosureTests.cs:71` rather than inventing
one. Disposition receipts intentionally differ from predecessor receipts in four content-bound places:
their accepted scope describes the rejection, their directory is addressed by the envelope digest,
their durable-source anchor names the disposition, and their accepted limitations exactly reproduce
the envelope's retained plus disposition-specific limitations.

The 2026-08-14 proposal is narrative context only and is deliberately not content-bound by the
envelope. The owner accepted that trade on 2026-08-21 to preserve the frozen canonical envelope digest
`a7ecd45524ca3ebd6f2c9a23143e2786f31d705f6a4a741be8f35cfc1c1851ec`; the bound governing authority
remains the approved 2026-08-16 proposal.

## Verification

**Commands:**
- `(cd _bmad-output/implementation-artifacts/evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594 && sha256sum -c critical-evidence-sha256.txt)` -- expected: all 33 frozen predecessor entries pass.
- `(cd _bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd && sha256sum -c evidence-sha256.txt && sha256sum -c evidence-core-sha256.txt && (cd packages && sha256sum -c ../nuget-sha256.txt))` -- expected: 51 OK, zero lines matching `: FAILED$`.
- `(cd _bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd && sha256sum review-subject.json)` -- expected: `6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97`.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings, zero errors.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 --no-restore -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --filter FullyQualifiedName~DeployedRuntimeParityClosureTests` -- expected: all focused tests pass, zero skipped; count strictly above the 190 baseline.
- `git status --porcelain _bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28 _bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` -- expected: empty output.
- `npx markdownlint-cli2 docs/ci.md` -- expected: passes.

### Review Findings (2026-08-21 loop, resolved)

- [x] [Review][bad_spec] Bound the envelope's crosswalk, core-manifest, proof-packet, and retained-identity declarations to the values recorded inside the frozen review subject.
- [x] [Review][bad_spec] Added a reachable exact inventory for the selected v3.94.1 tree and negative planted-file/directory coverage.
- [x] [Review][bad_spec] Added support-safe, field-coded rejection diagnostics and exact-code assertions.
- [x] [Review][intent_gap] Owner restored the credential-exposure and ask-first credential clauses in the frozen block.
- [x] [Review][Patch] Corrected the selected/both-tree counts, test baseline, AC count, proof binding, lifecycle prose, receipt assertions, role-field case, predecessor verification command, and operator-facing digest.
- [x] [Review][Defer] Recorded the GitHub-minted receipt-anchor gap, cross-language canonicalizer equivalence, JSON LF pin, and hand-maintained lineage-token completeness in `deferred-work.md`; none authorizes completion at 0/3 receipts.


### Review Findings (2026-08-21 loop 1, historical ledger — dispositions below)

Scope: `git diff 56aa0fec~1 56aa0fec -- tests/…/DeployedRuntimeParityClosureTests.cs` (+2,514, 0 removed).
Four layers ran (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor); none failed.
48 raw findings, 33 after merge, 2 dismissed. Runtime ground truth at review time: Release build
0W/0E; focused suite **255 passed / 0 failed / 0 skipped**; both frozen trees byte-clean with 91+60
checksum entries verifying; `review-subject.json` = `6cee8dad…`; envelope = `7ff7e150…`.
Chunk 2 (envelope, story record, spec, superseded archive, `docs/ci.md`, `sprint-status.yaml`) not yet reviewed.

**decision (human input required — the correct fix is ambiguous):**

- [x] [Review][Decision→Patch] RESOLVED 2026-08-21 (owner: keep unbound, record why). `governing_authority` pins only `sprint-change-proposal-2026-08-16.md` (`DispositionAuthorityRelativePath:78`, checked at `:4008`-`:4020`), while the retained limitations and the `deployment-authority-missing` blocker consequence rest on "The 2026-08-14 proposal authorizes the identity replacement only". Pinning `sprint-change-proposal-2026-08-14.md` would change the envelope's canonical bytes, which the KEEP clause freezes at `7ff7e150…`. **Decision: leave it unbound and keep `7ff7e150…` intact.** Follow-up patch below.

- [x] [Review][Patch] Record that the 2026-08-14 authority is deliberately unbound — add an explicit note to the spec (Design Notes or Code Map) stating that `sprint-change-proposal-2026-08-14.md` is cited as narrative context only, is **not** content-bound, and that this is an accepted trade to preserve the frozen envelope digest `7ff7e150…`. Without the note the next loop will re-raise it as a binding hole. [spec-3-13-deployed-runtime-parity-closure.md:113-126] — APPLIED: Design Notes now carries this exact note ("The 2026-08-14 proposal is narrative context only and is deliberately not content-bound by the envelope...").
- [x] [Review][Decision→Defer] RESOLVED 2026-08-21 (owner: keep the deferral, correct the rationale) — deferred. The spec Defer list calls the unreachable commit anchor a "Pre-existing pattern inherited from `ValidateAcceptances`". It is not: `ValidateAcceptances` uses `#story-3-13-<subjectHash>-<role>` (`:6613`), while this diff authors a **new** format `#story-3-13-disposition-<envelopeHash>-<role>` (`:4910`-`:4912`). GitHub mints `#commitcomment-<id>`, so the 3/3 path stays reachable only from hand-authored fixtures. **Deferral reason: the gap is real but non-blocking while 3/3 receipts are uncollected; the corrected rationale is that the anchor format is new in this change and the durable-source cross-check proves consistency, not independence — not that the pattern was inherited.**

**patch (unambiguous fix; no human input needed):**

- [ ] [Review][Patch] All seven acceptance-side rejection codes are unexercised, leaving the story-completion decision unprotected — `acceptance.receipt.schema`, `acceptance.receipt.accepted_at`, `acceptance.receipt.accepted_limitations`, `acceptance.source.record`, `acceptance.source.subject_sha256`, `acceptance.source.decision`, `acceptance.chronology` each occur exactly once in the 9,392-line file: at their own emission site. Verified empirically — deleting the `accepted_at` ordering check leaves 255/255 green, and a receipt back-dated to `2026-01-01` (before the envelope's `assembled_at` of `2026-08-21T11:23:36Z`) is then accepted, `CountDispositionReceipts` returns 3, and `DispositionStoryMayBeDone` returns `true`. This is the one decision the gate exists to make. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4821-4977]
- [ ] [Review][Patch] The free-text tail of `limitations` is unconstrained, unscanned for foreign lineage, and propagated verbatim into every receipt — `RejectDispositionRetainedRecords` requires only `limitations.Length > subjectLimitations.Length` plus a verbatim first-7 match; entries 8..N are arbitrary text. `DispositionIdentitySections:198-206` scans only six sections, excluding `limitations`, `verification`, `successor_boundary`, `acceptance_contract`, `retained_blockers`, `revalidation_trigger`. Receipts must `SequenceEqual` those same limitations. Frozen matrix row 6 requires a `1.20`/`v3.77.2`/`3.14` splice to fail closed; via an appended limitation or top-level `verification.method` it does not. [DeployedRuntimeParityClosureTests.cs:4413-4416, :198-206, :4668-4687]
- [ ] [Review][Patch] Spec bookkeeping contradicts the committed implementation — all four Execution tasks left `[ ]` are in fact implemented at `56aa0fec`, and the `Review Findings (2026-08-21 loop, triaged — NOT yet applied)` block plus Change Log describe a different build. Concretely: the block claims `EvaluateDisposition` "returns a bare `bool`" (it returns `(bool Verified, string Rejection, int AcceptedReceipts, string AcceptanceRejection)` at `:3802`); claims "~46 negative cases assert only `ShouldBeFalse()`" (every negative theory routes through `ShouldRejectWith`); cites a "dead role-dedup guard at `:4069`" (no such guard exists); and the Change Log states the commit shifts anchors "by exactly +1555" with the gate "at `:3457`" (actual shift +2514, gate at `:3802`). Left as-is this will drive a `bad_spec` loopback that reverts working code. [spec-3-13-deployed-runtime-parity-closure.md:139-170, :205-213]
- [ ] [Review][Patch] Ten rejection branches are provably unreachable — the `subject.<name>.path` and `subject.proof_packet.path` blocks and the eight `subject.crosswalk_coherence.*` checks compare operands that upstream digest pins (`:4012`-`:4043`, `:4097`) have already fixed byte-for-byte. Verified: deleting each block, and replacing `return coherence.FirstOrDefault(...)` with `return null`, each leaves 255/255 green, while a control mutation turns the suite red. The envelope's `verification.method` claims this coherence is re-checked; it is enforced solely by the upstream pins, and nothing signals that if those constants rotate for 3.14/3.15 the guards will not compensate. [DeployedRuntimeParityClosureTests.cs:4103-4117, :4135-4177, :4224, :4259, :4264]
- [ ] [Review][Patch] The disposition directory has no closed inventory — `RejectDispositionManifest` asserts only that `disposition-sha256.txt` closes over whatever is on disk (`actual.SequenceEqual(entries.Keys…)`) with no allow-list. Add a stray file **and** regenerate the manifest over it and the packet verifies self-consistently. This is the same hole Task 5 closed for the selected evidence tree, still open for the directory that holds the receipts. [DeployedRuntimeParityClosureTests.cs:4981-5011]
- [ ] [Review][Patch] The `disposition.directory` content-addressing guard is never exercised — it requires the directory name to equal `SelectedReviewSubjectSha256`, but every test reaches the gate through `CopyDisposition`/`CopyDispositionWithEvidence`, which always construct the correct name. Deleting the guard leaves 255/255 green. The analogous receipt-directory rule *is* covered (`stale-envelope-directory`), which makes this the outlier. [DeployedRuntimeParityClosureTests.cs:3810-3819]
- [ ] [Review][Patch] Every Code Map anchor is stale by exactly +959 lines and Task 3's re-derivation clause was not done — `EvaluateClosure` 4357→**5316**, `ValidateAcceptances` 5544→**6503**, `ValidateActualFailClosedSubject` 5817→**6776**, `LoadReviewerRoster` 6028→**6987**, `EvidenceDirectoryHasNoUnlistedFiles` 6267→**7226**, `ResolveWithin` 7909→**8868**, `FindRepositoryRoot` 8418→**9377**, `EvaluateWithFreshReview` 7692→**8651**, `PersistRuntimeBindings` 7712→**8671**, `ProofRelativePath` 35→**36**, receipt schema 73→**233**. The file is **9,392 lines / 67 test methods**, not the "8433 lines, 62 test methods" claimed twice. `tools/release_evidence_codec.py:74` is now an 11-line facade — `canonical_bytes`/`canonical_sha256`/`_publisher_canonical_bytes` live at `tools/release_evidence_handlers/v3.py:76`/`:83`/`:448`. The selected tree holds **38** files (24 + 14 nupkgs), not the "26 files + packages/ with 14 nupkgs" (=40) claimed. [spec-3-13-deployed-runtime-parity-closure.md:69, :75, :79-87, :118, :124, :144, :213]
- [ ] [Review][Patch] `ShouldRejectWith`'s remediation assertion is green by construction — it accepts `"; remediation: "` OR `"; revalidation: "`, but every rejection string is built by `DispositionReason` or `DispositionDriftReason`, which unconditionally append one or the other. The clause can never fail and adds no coverage. Assert the specific expected remediation text per code instead. [DeployedRuntimeParityClosureTests.cs:3792-3794, :3888-3892]
- [ ] [Review][Patch] `SingleOrDefault` on retained-defect rows converts a duplicate into `internal.exception` — `absent_labels`, `malformed_labels` and `retained_checksum_manifests` use `SingleOrDefault`, so a duplicated row that preserves the cardinality checks throws `InvalidOperationException`, which the catch converts to `internal.exception` instead of the intended field-specific diagnostic. No `[InlineData]` covers a duplicate row. [DeployedRuntimeParityClosureTests.cs:4341, :4357, :4438]
- [ ] [Review][Patch] The chronology surface is wall-clock dependent and machine dependent — `RejectDispositionChronology` and the receipt check compare against `DateTimeOffset.UtcNow.AddMinutes(5)`, while fixtures derive timestamps from the committed `assembled_at` + 1 minute, so the ordering rule is satisfied by construction and never probed. `:5160` also uses bare `DateTimeOffset.Parse(..., CultureInfo.InvariantCulture)` with no `DateTimeStyles`, unlike `TryParseExplicitOffset` used elsewhere, so generated receipt bytes carry the *local* UTC offset and differ per machine. [DeployedRuntimeParityClosureTests.cs:3949-3963, :4967-4977, :5157-5164]
- [ ] [Review][Patch] The "envelope lives outside both hashed trees" assertions are constant-true — the two `Path.GetFullPath(disposition).StartsWith(...).ShouldBeFalse()` checks compare compile-time `const string` paths that share only the `evidence/story-3-13/` prefix, so they cannot observe a regression. Separately `acceptance_contract.outside_hashed_evidence` is asserted `true` at `:4599` but `EvaluateDisposition` never checks where `dispositionRoot` actually is. [DeployedRuntimeParityClosureTests.cs:2967-2973, :4599]
- [ ] [Review][Patch] Two lifecycle guards cannot fail for the reason they claim, and the test is literal-coupled — `sprint.ShouldNotContain("Story 3.13 remains in-progress")` searches a YAML key/comment file that could never contain that prose sentence; `ci.ShouldNotContain("6cee8dad…")` forbids only the U+2026 ellipsis, so an ASCII `6cee8dad...` elision passes. `Story313LifecycleSurfacesRecordTheRejectedDisposition` asserts literal substrings and will fail by design once three receipts are legitimately collected and the status advances, inviting a literal edit rather than a re-derived rule. [DeployedRuntimeParityClosureTests.cs:3758-3781]
- [ ] [Review][Patch] Collection-level diagnostics name the container, not the offending fact — frozen matrix rows 2 and 3 require the diagnostic to name the offending field / missing retained fact, but `envelope.retained_provenance_defects.malformed_labels` is emitted with `platform`, `configFile` and `label` all in scope, and `envelope.retained_blockers` covers a drop, a rewording, or a crosswalk mismatch without naming which of the three. `envelope.deployment_authority` is likewise emitted for two distinct causes. [DeployedRuntimeParityClosureTests.cs:4369, :4392, RejectDispositionIdentity]
- [ ] [Review][Patch] An unrecognized `[InlineData]` mutation identifier silently aliases to the switch `default:` — a newly added theory case can pass without exercising its intended mutation. Throw `ArgumentOutOfRangeException` on unknown identifiers in all eight mutation switches. [DeployedRuntimeParityClosureTests.cs:3109, :3229, :3316, :3372, :3448, :3571, :3653, :3709]
- [ ] [Review][Patch] Three positionally-coupled parallel arrays have no pairing assertion — `RetainedManifestFiles`, `RetainedManifestEntryCounts` and `RetainedManifestBases` are indexed together; reordering any one silently re-pairs manifests with the wrong entry count and base directory, and a length divergence raises `IndexOutOfRangeException`, which is not in the catch filter so it escapes as an unhandled fault rather than a support-safe diagnostic. The magic numbers `38`/`14` duplicate the same facts a third time. [DeployedRuntimeParityClosureTests.cs:180-196, :4432-4462, :3060-3062]
- [ ] [Review][Patch] A malformed envelope is misclassified as a fixture fault — `RejectDispositionManifest` deliberately catches `InvalidDataException` so a bad manifest surfaces as `disposition.manifest`, but a bad envelope throws `JsonException` out of `:3820` and is reported as `internal.exception`, i.e. as a harness problem rather than an envelope defect. The `JsonNode.Parse(...) is not JsonObject` branch is also unreachable for malformed JSON (Parse throws) and untested for the valid-but-non-object case (`[]`, `"x"`). The same asymmetry hits an unparsable receipt or durable-source file. [DeployedRuntimeParityClosureTests.cs:3820-3826, :3859-3877]
- [ ] [Review][Patch] A dead re-check that cannot express what it appears to check — `RejectDispositionReceipt` re-evaluates `LimitationsContainMutationProhibitions(envelopeLimitations)` on the *envelope's* limitations, which `RejectDispositionRetainedRecords` already enforced before `CountDispositionReceipts` is reached; removing it leaves 255/255 green. It looks like it validates the *receipt's* accepted limitations but does not. `CountDispositionReceipts:4781-4789` likewise re-parses timestamps already proven parseable, making `acceptance.chronology` unreachable. [DeployedRuntimeParityClosureTests.cs:4889, :4781-4789]
- [ ] [Review][Patch] Comment overclaims its own coverage — `// All 91 retained checksum entries still verify` precedes two `RetainedManifestStillVerifies` calls covering 3 + 34 = 37 entries; `nuget-sha256.txt` (14) and `predecessor-tree-sha256.txt` (40) are not re-checked there. The full claim is honoured only by `RetainedDispositionEvidenceTreesStillVerifyEveryChecksumEntry`. [DeployedRuntimeParityClosureTests.cs:3115-3117]
- [ ] [Review][Patch] The `duplicate-role` case does not create a duplicate role — it rewrites `release-owner.json`'s `role` field to `eventstore-owner`, which the filename/role binding rejects. A genuine duplicate is structurally impossible because `CountDispositionReceipts` requires exactly the three role-named files, so the case name overstates what is covered. Rename it to `role-field-mismatch`. [DeployedRuntimeParityClosureTests.cs:3482, :4759-4770]
- [ ] [Review][Patch] The platform pair is hardcoded where the surrounding file derives it — `string[] platforms = ["linux/amd64", "linux/arm64"]` and its `configFiles` companion, versus `:5992` which derives `platforms` from the retained index children. The new defect re-derivation cannot notice a retained index whose platform set differs. `MalformedProvenanceLabels` is a field while its two companions are locals, which is also inconsistent. [DeployedRuntimeParityClosureTests.cs:4317-4318]
- [ ] [Review][Patch] Half the synthesized-revision check is vacuous — `defects["observed_config_revision"] is not null || release["observed_config_revision"] is not null`: the frozen crosswalk's `release` object carries no such key (verified: its key set is empty), so the second operand is a constant `false`, and the `synthesized-revision-label` case only mutates the envelope side. [DeployedRuntimeParityClosureTests.cs:4295]
- [ ] [Review][Patch] `depends_on_corrective_release` is grouped with authorization flags and gets a wrong diagnostic — any `true` value yields "the disposition claims an authorization or closure a rejected candidate cannot grant". A scheduling dependency is neither. The envelope simultaneously declares `corrective_release_owner: "3.14"` while being forced to deny any dependency on it; `docs/ci.md:375` states this pairing is intentional but no test asserts it. Split the diagnostic. [DeployedRuntimeParityClosureTests.cs:4549, :4558]
- [ ] [Review][Patch] Two distinct inventory rules share one reason code — `RejectSelectedEvidenceInventory` returns `selected_evidence.inventory` from both its directory clause and its file clause, and `ShouldRejectWith` compares only the code, so no case can prove which rule fired. (The file clause *is* exercised by `stray-root-file` and `stray-package`; only the discrimination is missing.) [DeployedRuntimeParityClosureTests.cs:4498-4511]
- [ ] [Review][Patch] Leftover duplicate comment block on `SubjectFrozenIdentityFields` — the second block restates the first and reads as an un-removed edit remnant. [DeployedRuntimeParityClosureTests.cs:152-157]
- [ ] [Review][Patch] Untracked path literal — `"docs/ci.md"` is inlined while its two siblings use constants (`StoryRecordRelativePath:82`, `SprintStatusRelativePath:84`). [DeployedRuntimeParityClosureTests.cs:3773]
- [ ] [Review][Patch] Temp-tree cleanup can leak and can mask the original failure — `CopyDisposition*` is called inside the `try` in ten tests, so a throw before assignment skips cleanup, and a `finally` `Directory.Delete` that throws `IOException` replaces the real assertion failure. [DeployedRuntimeParityClosureTests.cs:3079-3081 and the nine sibling call sites]
- [ ] [Review][Patch] Design Note contradicted without a change-log entry — it states the receipts' `accepted_scope` is "the only string that must change", but three further receipt-contract strings changed: the receipt directory template `acceptances/{subject_sha256}` → `acceptances/{envelope_sha256}` (`:70`), the durable `source_url` anchor, and `accepted_limitations` (now the envelope's 10, not the subject's 7). The `{envelope_sha256}` change is arguably *required* by matrix row 1, which strengthens the case for recording it. [spec-3-13-deployed-runtime-parity-closure.md:124-126]

**defer:**

- [x] [Review][Defer] Commit `56aa0fec` does not describe the change it carries [git:56aa0fec] — deferred, pre-existing. Its subject is `feat(release_evidence_handlers): add v3 codec for corrective release packet and initial handler setup`, yet it carries the 2,514-line Story 3.13 disposition verifier, the disposition artifacts, `docs/ci.md`, `sprint-status.yaml`, and deployment guides. Already on `main`; not fixable without history rewrite.
- [x] [Review][Defer] `ForeignLineageTokens` has no completeness guard [DeployedRuntimeParityClosureTests.cs:113-127] — deferred, pre-existing. Already on the spec Defer list; still omits the two explicitly voided subject digests `394292a2…`/`93d70d51…` and the historical proof-packet digest `349e0998…`. Note the retained subject's own `limitations[4]` names `394292a2` and `fa2d1c99` — and `limitations` is unscanned anyway.
- [x] [Review][Defer] Malformed labels beyond the declared three cannot be declared [DeployedRuntimeParityClosureTests.cs:4313-4360] — deferred, pre-existing. If retained configs ever carry another label equal to `MalformedLabelValue` outside `MalformedProvenanceLabels`, the guard rejects while the cardinality check forbids declaring it. Not live for the frozen config (exactly 3 labels × 2 platforms); a robustness gap for successor candidates.
- [x] [Review][Defer] Two canonicalizers define one authority with no equivalence test [tools/release_evidence_handlers/v3.py:76] — deferred, pre-existing. Already on the spec Defer list: Python `canonical_bytes` for authoring vs C# `CanonicalDispositionBytes` for verification, untested for non-ASCII or line-separator input.


### Review Closure (2026-08-22)

This closure is authoritative over the unchecked historical rows above.

- [x] [Review][Patch] Closed the disposition allow-list, recursively scanned the whole envelope for foreign lineage, required the exact limitation set, made receipt/source parsing fail under their own diagnostics, added deterministic receipt chronology, and covered the previously unexercised receipt/source rejection branches.
- [x] [Review][Patch] Added the disposition-location guard test, distinct file/directory inventory codes, duplicate retained-row diagnostics, unknown-mutation throws, tuple-bound manifest definitions, retained-platform derivation, a distinct corrective-release dependency diagnostic, canonical-envelope diagnostics, and best-effort temporary-tree cleanup.
- [x] [Review][Patch] Removed the redundant receipt limitation re-check and dead acceptance chronology branch; retained the upstream-pinned subject/coherence guards as explicit defense-in-depth and retained collection-level reason codes because they identify the exact contract property while the support-safe detail identifies the defect.
- [x] [Review][Patch] Reconciled every Execution checkbox, restored the superseded archive byte-for-byte, corrected the receipt Design Note and 2026-08-14 authority trade, and re-derived the live Code Map at 9742 lines / 68 test methods.
- [x] [Review][Defer] Appended the production receipt-anchor gap to `deferred-work.md`. Existing deferrals remain for immutable Git history, cross-language canonicalizer equivalence, `.gitattributes` LF hardening (an Ask First surface), complete foreign-token derivation, and successor-only malformed-label generalization.

### Review Findings (2026-08-22 loop 2 — `bmad-code-review` against `a1b4fe54^..a1b4fe54`)

Four parallel layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor); all four completed. 18 raw findings triaged to 0 decisions / 5 patches / 5 defers / 6 dismissed (verified false or already resolved on closer read of the code at HEAD). A 6th patch, found while verifying the other 5 by actually running the suite (not surfaced by any of the four review layers), is recorded below.

- [x] [Review][Patch] `IncompleteDispositionAcceptanceKeepsStoryNonDone(mutation: "future-receipt")` fails today, deterministically, due to date rollover — not a flake, reproduced on the unpatched `a1b4fe54` commit too. The test computes a fixed fixture time (`assembled_at` `2026-08-21T11:23:36Z` + 2 min) and correctly threads it through `EvaluateDisposition` for its main assertions, but its final assertion called `DispositionStoryMayBeDone`, which had no `validationTime` parameter and silently defaulted to real `DateTimeOffset.UtcNow`. The mutated "future" receipt (`fixture-time + 1 day` = `2026-08-22T11:25:36Z`) stopped being future once real UTC passed that instant today, so the guard silently stopped firing. APPLIED: added an optional `validationTime` parameter to `DispositionStoryMayBeDone`, forwarded to `EvaluateDisposition`, and passed the fixture's `validationTime` at the one call site that needs it (line ~3781); the other 4 call sites keep the real-`UtcNow` default, which is correct for their fixtures. Full suite verified green (329/329) after the fix. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4204-4212, :3781]

- [x] [Review][Patch] Spec frontmatter had prematurely advanced its lifecycle token to the fully-closed value, contradicting the frozen "Always" constraint, AC5 (0/3 receipts), the story record (`Status: review`), `sprint-status.yaml:225` (`review`), and this same commit's own new Spec Change Log entry ("Story 3.13 remains `in-review` with 0/3 production receipts"). `Story313LifecycleSurfacesRecordTheRejectedDisposition` never read this spec file, only the story record and sprint tracker, so nothing caught the frontmatter drift. APPLIED: reverted the frontmatter token back to non-closed, and added a spec-frontmatter assertion to that test. [spec-3-13-deployed-runtime-parity-closure.md:5]
- [x] [Review][Patch] The `disposition.directory` reason code (rejects a disposition not named after `SelectedReviewSubjectSha256`) appears exactly once in the whole file — at its own emission site — with no test asserting `ShouldRejectWith(rejection, "disposition.directory")`. Mutation-provable: the guard can be deleted with the full suite green. Sixth recurrence of the guards-green-by-construction pattern in this story. APPLIED: added `MisnamedDispositionDirectoryFailsClosed`. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4004-4010]
- [x] [Review][Patch] `envelope.retained_checksum_manifests`' new duplicate/empty-`file`-row `GroupBy` guard has no negative test case, unlike its two sibling collections (`malformed_labels`, `absent_labels`) which each received a matching `duplicate-*-label` case in the same diff. Verified: reverting the `GroupBy` clause routes a duplicate row into `SingleOrDefault`, which throws `InvalidOperationException` and is swallowed by the outer catch as `internal.exception` instead of the intended field-specific diagnostic — the same failure class this loop's own closure claims to have fixed for the other two arrays. APPLIED: added `DuplicateOrEmptyChecksumManifestDeclarationFailsClosed`. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4658-4669]
- [x] [Review][Patch] The new `disposition.location` guard has two disjuncts (nested inside the selected `80d12ef5…` tree, or nested inside the historical `fa2d1c99…` tree); `DispositionInsideFrozenEvidenceTreeFailsClosed` exercises only the first. Deleting the second disjunct (`PathIsWithin(dispositionRoot, Path.Combine(repositoryRoot, EvidenceRelativePath))`) leaves the suite green. APPLIED: added `DispositionInsideHistoricalEvidenceTreeFailsClosed`. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4013-4014]
- [x] [Review][Patch] Historical-ledger action item "Record that the 2026-08-14 authority is deliberately unbound..." is still `[ ]` even though this same diff added the exact requested note to Design Notes ("The 2026-08-14 proposal is narrative context only... deliberately not content-bound..."). APPLIED: checked the box at its original location. [spec-3-13-deployed-runtime-parity-closure.md:156-...]
- [x] [Review][Defer] `PathIsWithin` (backing the new `disposition.location`/`disposition.directory` guards) uses plain `Path.GetFullPath` + ordinal `StartsWith` with no reparse-point resolution, reproducing this story's own already-deferred `ResolveWithin` ordinal-`StartsWith`/TOCTOU weakness class in a brand-new guard rather than reusing the hardened helper. — deferred, pre-existing pattern [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:8187-8194]
- [x] [Review][Defer] `review_loop_iteration` frontmatter stays `1` although this diff narrates at least three distinct review passes (2026-08-21 loop, loop-1 historical ledger, 2026-08-22 closure). — deferred, cosmetic [spec-3-13-deployed-runtime-parity-closure.md:7]
- [x] [Review][Defer] The "Review Closure (2026-08-22)" section collapses roughly twenty individually-numbered historical findings into 5 broad bullets while leaving every underlying checkbox unchecked, reducing the per-finding traceability the granular ledger format exists to provide. — deferred, documentation-quality [spec-3-13-deployed-runtime-parity-closure.md:210-218]
- [x] [Review][Defer] The `depends_on_corrective_release` diagnostic split is fixed in code (own reason code + test case), but `docs/ci.md:375`'s claim that `authorizes_deployment: false` + `corrective_release_owner: "3.14"` is an intentional pairing remains asserted by no test. — deferred, pre-existing [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4549-4558]
- [x] [Review][Defer] `MalformedProvenanceLabels` stays a `static readonly` field while its new platform/config-file counterpart is a local variable recomputed by re-reading and re-parsing `index.raw` from disk on every call — inconsistent caching, minor repeated I/O. — deferred, pre-existing pattern [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4317-4318]

Dismissed (6, verified false or already resolved on reading the code at HEAD): a claim that the old compile-time-constant "envelope lives outside both hashed trees" `StartsWith` assertions were left in place — refuted, they are absent from the file at HEAD. A claim that deleting the old `sprint.ShouldNotContain("Story 3.13 remains in-progress")` / `ci.ShouldNotContain("6cee8dad…")` checks left a regression unguarded — refuted, the replacement `ci.ShouldContain(SelectedReviewSubjectSha256)` is a strictly stronger direct check than the ellipsis-avoidance heuristic it replaced. A receipt-schema anchor `:71` vs `:233` "disagreement" — refuted, `:71` is correct at HEAD and the `:233` citation lives in a section explicitly marked historical. A claim that the two `PathIsWithin` disjuncts in the `disposition.location` guard are redundant — refuted, they test two distinct frozen trees (selected vs. historical). A claim that `CountDispositionReceipts` re-parsing `subject.created_at`/`envelope.assembled_at` could throw uncaught — refuted, both are already fail-closed-validated earlier by `RejectDispositionChronology`, and any escape is still caught by `EvaluateDisposition`'s outer handler. A claim that removing the generic "contains remediation or revalidation" assertion lost coverage — refuted, `ShouldRejectWith` now asserts the exact reason code via `DispositionReasonCode(...).ShouldBe(expectedCode)`, which is strictly stronger.

### Review Findings (2026-08-22 loop 3 — `bmad-code-review` against uncommitted working tree)

Four parallel layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor); all four completed and all four independently confirmed accurate (verification-gap re-ran both focused suites — 281 + 48 = 329/329 green — and independently recomputed all three new Story 3.15 receipt SHA-256 digests from the on-disk files; the reviewing agent also fetched the two GitHub-issue-comment receipt sources via `gh api` and confirmed they are real comments from `jpiquot` with byte-matching bodies). 18 raw findings deduplicated and read against the surrounding code; 13 did not survive verification (same code path as an already-covered case, explicitly refuted by re-checking all call sites, a necessary test adaptation rather than a regression, or already self-flagged elsewhere in this same diff) and are dropped as noise. 1 decision, 2 patches, 2 defers remain.

**decision (human input required — the correct fix is ambiguous):**

- [x] [Review][Decision→Reassigned] RESOLVED 2026-08-24 by loop 5 (see the loop-5 section below): the co-landed files are owned and reviewed by Stories 3.14 and 3.15; the four gitlinks were verified reachable; no history rewrite. Original finding: This diff bundles Story 3.15's full positive-parity closure (3 collected receipts, `deployed_runtime_parity: "available"`, selected OCI index) into what is otherwise Story 3.13's own loop-2 review-hardening changeset. None of the touched 3.15 files (`3-15-corrected-deployed-runtime-parity-closure*.md`, `spec-3-15-corrected-deployed-runtime-parity-closure.md`, `evidence/story-3-15/.../closure.json`, `CorrectedDeployedRuntimeParityClosureTests.cs`) appear in Story 3.13's Code Map, and the `docs/ci.md` hunk lies entirely inside the Story 3.15 section — outside Story 3.13's frozen "Ask First: any change outside Story 3.13 spec/story/evidence/verifier/tracking/`docs/ci.md` files" boundary. The underlying Story 3.15 receipts are independently verified genuine (real `jpiquot` GitHub comments on issue #346), so this is not fabricated evidence — the question is purely whether landing both stories' changes as one changeset was authorized, or whether they should be split into two separate commits before landing. [spec-3-13-deployed-runtime-parity-closure.md Boundaries & Constraints; diff touches files outside its Code Map]

**patch (unambiguous fix; no human input needed):**

- [ ] [Review][Patch] `sprint-status.yaml` was not updated to match this diff's own Story 3.15 lifecycle changes — `spec-3-15-corrected-deployed-runtime-parity-closure.md:5` now reads `status: 'in-review'` and the story record narrates "Deployed-runtime parity is available," but `sprint-status.yaml:227` still reads `3-15-corrected-deployed-runtime-parity-closure: in-progress`. No test cross-checks Story 3.15's spec/story-record/sprint-status agreement the way this same diff's new `Story313LifecycleSurfacesRecordTheRejectedDisposition` assertion now does for Story 3.13 (`SpecRelativePath` check, `DeployedRuntimeParityClosureTests.cs:4079-4084`), so nothing in CI would catch this drift. [sprint-status.yaml:227]
- [ ] [Review][Patch] Spec-3-15's Change Log entry ("2026-08-22 (acceptances collected): with explicit owner authorization, retained the EventStore-owner and Release-owner acceptances…") cites no commit, comment, or ledger entry for the authorization to collect the receipts itself — only the resulting GitHub-comment receipts are cited, not the separate authority to act. Every other claim in this spec family is meticulously sourced; this one line is not. [spec-3-15-corrected-deployed-runtime-parity-closure.md Spec Change Log]

**defer:**

- [x] [Review][Defer] The Story 3.15 Test Architect receipt (`bmad:murat`) has no externally-checkable anchor comparable to the two GitHub-issue-comment-backed owner receipts — it is sourced from a `bmad-test-architect-record`, i.e. self-attested by the same tooling that assembled the packet. This reproduces, in a new story, the exact durable-receipt-anchor gap `deferred-work.md` already tracks for Story 3.13's disposition receipts (and is the project's established pattern for `bmad:`-role receipts generally, not a defect unique to this diff). — deferred, pre-existing pattern [evidence/story-3-15/.../acceptances/.../test-architect.json]
- [x] [Review][Defer] `closure.json` declares `deployed_runtime_parity: "available"` and a non-null `selected_deployed_identity` even in states where `acceptances.receipts` is empty (confirmed unchanged from `HEAD`, i.e. pre-existing, not introduced by this diff) — the real gate is `_exact_list(receipts, 3, ...)` in `tools/deployed_runtime_parity_handlers/v1.py:360`, which fails closed regardless of those two fields' values, so there is no functional hole. But a consumer reading the JSON file directly instead of running the Python verifier would misread pre-acceptance state as already authorized. — deferred, pre-existing, cosmetic/documentation risk only [tools/deployed_runtime_parity_handlers/v1.py:203-207]

### Review Findings (2026-08-23 loop 4 — `bmad-code-review` against `a1b4fe54`)

Four parallel layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor); all four
completed. Scope requested by the owner: commit `a1b4fe54` alone (`test(packaging): harden Story 3.13
disposition gate`). 15 findings after merge/verification: 0 decisions / 2 patches / 5 defers / 8
dismissed. Verification against HEAD (`f2d2575c`) showed most of what the four layers independently
rediscovered — the frontmatter `status: 'done'` self-contradiction, the untested
`disposition.location` historical-tree disjunct, `review_loop_iteration` staleness, and the collapsed
Review Closure checkboxes — had already been found and either fixed or deferred by loop 2/loop 3
above, reviewing the same or a later diff; those are dismissed here as already handled, not as false
positives.

**patch (unambiguous fix; no human input needed):**

- [x] [Review][Patch] APPLIED 2026-08-24 by loop 5 (`UnreadableDispositionEnvelopeReportsAnInternalDiagnostic`). Original finding: The `internal.exception` catch-all diagnostic is not exercised by any test —
  this diff replaced `DispositionFixtureFaultReportsAnInternalDiagnostic` with
  `InvalidDispositionEnvelopeReportsCanonicalBytesDiagnostic`, which asserts
  `disposition.canonical_bytes`, a different code path. No test in the file now asserts
  `ShouldRejectWith(rejection, "internal.exception")`. Add a case that forces a genuine internal
  fault (e.g. an unreadable/locked disposition file raising `IOException`/
  `UnauthorizedAccessException`) distinct from a malformed-JSON canonical-bytes mismatch.
  [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4185-4200]
- [x] [Review][Patch] APPLIED 2026-08-24 by loop 5 (JSON-parse failures now emit `acceptance.receipt.json`). Original finding: `receipt-schema-mismatch` and `malformed-receipt-json` assert the identical
  reason code `acceptance.receipt.schema`, so no test discriminates which of the two distinct code
  paths (wrong schema string vs. JSON parse failure) actually fired — the same "two rules, one code"
  gap this story already split for `corrective_release_owner`/`depends_on_corrective_release` and for
  `disposition.manifest`'s file/directory clauses elsewhere in this same diff, left unfixed here.
  [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3635,
  3642, 3723, 3755]

**defer:**

- [x] [Review][Defer] `RejectDispositionManifest`'s new `allowedDirectories` allow-list branch has no
  negative test planting an unlisted stray directory (e.g. `acceptances/<hash>/junk/`) — every
  existing negative case plants a file, not a directory, leaving that branch unproven. — deferred,
  low value given the file-level allow-list is already well covered
  [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5381-5394]
- [x] [Review][Defer] `RejectDispositionManifest`'s directory enumeration
  (`Directory.GetDirectories(dispositionRoot, "*", SearchOption.AllDirectories)`) and
  `DispositionFilesUnder`'s file enumeration are not reparse-point-safe, the same weakness class
  already deferred above for `PathIsWithin` (loop 2) — a symlink planted in the disposition directory
  could evade both the location guard and the closed-inventory check. — deferred, pre-existing pattern
  class, requires repo write access to exploit
  [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5386-5392,
  5401-5405]
- [x] [Review][Defer] `DispositionSpecificLimitations` hardcodes three full sentences of frozen
  evidence prose as C# string literals with no automated cross-check against the frozen JSON file —
  a third, positionally-coupled source of truth, the same pattern this diff fixed for the retained
  manifest arrays (`RetainedManifestFiles`/`-EntryCounts`/`-Bases`) but left unfixed here. — deferred,
  low, evidence is frozen so live drift risk is theoretical
  [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:188-206]
- [x] [Review][Defer] `role-filename-mismatch`, `undeclared-sidecar`, and `stale-envelope-directory`
  moved from a soft acceptance-layer diagnostic (`Verified: true`, only `story_may_be_done` blocked,
  reason `acceptance.receipt_set`/`acceptance.receipt_directory`) to a hard whole-envelope
  `Verified: false` failure under `disposition.manifest` — a real contract change to what `Verified`
  means, not called out in Design Notes. Appears to be a strengthening, not a regression. — deferred,
  documentation-completeness only
  [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:857-927]
- [x] [Review][Defer] The "Suggested Review Order" section's absolute line-number anchors into four
  sibling files (`3-13-deployed-runtime-parity-closure.md:802`, `docs/ci.md:357`,
  `sprint-status.yaml:225`, `deferred-work.md:1410`) have no re-derivation task tied to them — the
  same anchor-rot class this spec elsewhere treats as a bug requiring an explicit "re-derive Code Map
  anchors" checklist item. — deferred, low
  [spec-3-13-deployed-runtime-parity-closure.md#suggested-review-order]

Dismissed (8, already handled elsewhere or verified false): the frontmatter `status: 'done'`
self-contradiction (already found and fixed by loop 2, spec:226; confirmed correct at current HEAD).
The `disposition.location` historical-tree disjunct being untested (already found and fixed by loop 2,
spec:229; `DispositionInsideHistoricalEvidenceTreeFailsClosed` confirmed present at HEAD). A possible
`NullReferenceException` reading `platform["os"]`/`["architecture"]` from `index.raw` — mitigated by
the outer `EvaluateDisposition` catch (`NullReferenceException` degrades to `internal.exception`) and
by the input being frozen, checksum-verified evidence already validated before this code runs; same
accepted-defense-in-depth precedent loop 2 established for an equivalent `CountDispositionReceipts`
re-parse concern. `ParseVerifiedExplicitOffset`'s re-parse-and-throw path being uncovered — same
precedent: loop 2 explicitly examined and refuted an equivalent claim on the grounds the outer catch
makes any escape safe. `RejectDispositionDefects` dropping its `crosswalk` parameter without a new
test proving the narrowed check still fails closed — verified false: the removed crosswalk-side
disjunct was provably always-false dead code (the frozen crosswalk's `release` object carries no
`observed_config_revision` key, per loop 1's own investigation), and the surviving envelope-side check
is already covered by the pre-existing `synthesized-revision-label` test case
(DeployedRuntimeParityClosureTests.cs:3338). `review_loop_iteration` staying `1` (already found and
deferred by loop 2, spec:232). The ~20 stale unchecked historical-ledger checkboxes under "Review
Closure" (already found and deferred by loop 2, spec:233). `sprint-status.yaml`'s stale Story 3.15 row
— not present in the reviewed commit `a1b4fe54` (which touches only this spec file and the test file);
already separately tracked as an open, unapplied patch item by loop 3's review of a later commit
(spec:249).

### Review Findings (2026-08-24 loop 5 — `bmad-code-review` against `a1b4fe54..6bf13757`)

Four parallel layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor); all four
completed, none failed. Scope: the un-reviewed Story 3.13 delta since loop 4's subject, narrowed to
Story 3.13 surfaces (spec, story record, `story-3-13` evidence, focused verifier, `deferred-work.md`,
`epic-3-context.md`, `sprint-status.yaml`, `docs/ci.md`) — 406 content lines across 7 files. ~73 raw
findings merged to 30 after dedupe and verification: 2 decisions / 11 patches / 7 defers / 10
dismissed. The focused verifier was executed at HEAD as part of triage and is **red**: 1 failed, 328
passed, 329 total.

> **Note for future loops:** this section deliberately avoids quoting the two lifecycle tokens as
> literals, because the whole-file guard described in P1 fails on any prose that does. Do not
> reintroduce the literal spellings here until P1 is applied.

**decision (human input required — the correct fix is ambiguous):**

- [x] [Review][Decision→Ratified] RESOLVED 2026-08-24 (owner: ratify the compression, record the authority). `epic-3-context.md` was recompiled by `516f2489`, a Story 3.15 commit, and the rewrite compresses several normative enumerations. **The finding as first written overstated the loss and is corrected here.** The deployment-authority guarantee was *not* deleted: it survives verbatim twice — "Planning approval, story completion, tags, prior pass flags, self-declared roles, or evidence from another release authorize neither publication, deployment, nor consumer infrastructure removal" (Requirements) and "These establish evidence only; deployment and consumer removal require separate authority" (Technical Decisions) — as does "content-bound receipts". `docs/ci.md`'s "evidence, not operational authority" claim is therefore fully backed. What was actually dropped is narrower: the enumerated OCI rejection list; the enumerated positive-parity binding list; `content-bound` as a qualifier on the corrective-release *authority* sentence; "status codes"/"mutable tags" from the insufficient-validation list and "topology" from the inspected list; "any changed transitive evidence invalidates the receipts"; the Story 3.15 clause "or treat parity evidence as deployment or consumer-mutation authority"; and two unrelated Epic 3 sentences (timeout cancellation semantics, the preflight environment-blocker-vs-product-defect distinction). The rewrite also hardcodes the package count "14" beside the manifest that owns it. **Ratified because the authority is provably intact upstream and this file is a derived artifact:** its own header reads "Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change.", and every dropped clause survives in stronger form in `architecture.md` — the OCI rejection list and the environment-setup-failure classification at `:157` and `:163`, deployment/consumer-removal authority at `:163`, `:333` and AD-22 `:321`, and the content-bound corrective authority plus transitive-evidence receipt invalidation at `:344-352`; `epics.md` retains the cancellation and environment-blocker language. Restoring the text into a regenerable summary would create a third copy that the next `compile-epic-context` run silently discards — the duplicate-source-of-truth pattern this same review flags elsewhere. Severity downgraded medium → low. No change made to `epic-3-context.md`. [_bmad-output/implementation-artifacts/epic-3-context.md:29,35,37,39,45,47,53]

- [x] [Review][Decision→Reassigned] RESOLVED 2026-08-24 (owner: reassign to the owning stories, do not rewrite published history). Loop 3's bundling decision (`spec:245`) is closed here. `a1b4fe54..HEAD` is 64 files / +3618 lines and co-lands Story 3.15 work with Story 3.13's surfaces, which does sit outside this story's frozen "Ask First" boundary. **But the coverage claim behind the finding does not hold: every material co-landed file is owned and already reviewed under its own story.** `Directory.Build.targets` is in Story 3.14's Code Map (`spec-3-14:21`, `:55`) and carries two live open Story 3.14 review patches (`Directory.Build.targets:47`, `:19`). `tools/deployed_runtime_parity_handlers/v1.py`, `CorrectedDeployedRuntimeParityClosureTests.cs`, `.gitattributes`, `sprint-status.yaml`, `deferred-work.md`, `docs/ci.md` and the four new `tools/` files were all inside the declared scope of Story 3.15's own review (`spec-3-15:66`). The only surfaces reviewed under no story are the four submodule gitlink bumps (`Hexalith.Builds 2f46aaee`, `.FrontComposer c4df0290`, `.Memories 64709388`, `.Tenants 09c746b3`); all four were verified present and reachable on `origin/main` of their respective submodules during this loop, so no dangling gitlink is being published. **Not split, because the cost is disproportionate and the risk points the wrong way:** `origin/main` is at `f2d2575c`, so three of the four commits are already published on a ruleset-protected branch, and history surgery adjacent to `evidence/story-3-13/` would put this story's one real asset — the byte-stability of the frozen trees — at risk to win a filing improvement on a story that is hard-blocked at 0/3 receipts regardless. The identical complaint against `56aa0fec` is already a standing medium deferral at `deferred-work.md:1367` on exactly this rationale, and the repository has ratified co-landed changesets retroactively before (Story 3.3 `afb90369`, Story 3.1 `b32c9e90`). Story 3.13 records that it did **not** review these files; Stories 3.14 and 3.15 did. [spec-3-13-deployed-runtime-parity-closure.md:245]

**patch (unambiguous fix; no human input needed):**

- [x] [Review][Patch] **HIGH — the focused verifier is red at HEAD, and the guard that reddened it is vacuous in the other direction.** `f2d2575c` appended two whole-file substring assertions over the spec to `Story313LifecycleSurfacesRecordTheRejectedDisposition`; `6bf13757` then wrote loop 4's narrative into that same spec, which quotes the closed lifecycle token verbatim at `spec:263` and `:322`. Verified by execution: `Failed: 1, Passed: 328, Total: 329`, `Shouldly.ShouldAssertException ... should not contain` at `DeployedRuntimeParityClosureTests.cs:4083`. The positive half is independently broken the other way — `spec:249` (loop 3's own patch bullet) already contains the in-review token as prose, so a mutation control that rewrites frontmatter line 5 to the closed value still leaves that assertion green. Both halves therefore observe the review ledger's wording rather than the frontmatter they claim to pin — the seventh recurrence of guards-green-by-construction in this story. Fix: extract the YAML frontmatter block (the `ExtractYamlBlock` approach used at `ContainerPublishingGovernanceTests.cs:546`) and assert on the parsed `status` key, not on the whole file. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4081-4083] — APPLIED: replaced both whole-file substring assertions with a `FrontmatterValue(spec, "status")` check anchored to the parsed YAML block, added that helper, and mutation-proved it (flipping frontmatter line 5 turns the test red on the parsed value; the prose quoting both tokens is now inert). Suite green.

- [x] [Review][Patch] Loop 4's first patch item was recorded but never applied: no test asserts the `internal.exception` catch-all. Verified at HEAD — `grep -c 'ShouldRejectWith(.*internal\.exception'` returns **0**; the code appears only at its emission site (`:4198`) and in two comments. Add a case forcing a genuine internal fault (unreadable/locked disposition file raising `IOException`/`UnauthorizedAccessException`), distinct from the malformed-JSON canonical-bytes path. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4185-4200] — APPLIED: added `UnreadableDispositionEnvelopeReportsAnInternalDiagnostic`, which replaces the envelope file with a directory of the same name so the read fails with an I/O-class exception on every platform, without depending on file modes a privileged runner would bypass.

- [x] [Review][Patch] Loop 4's second patch item was recorded but never applied: `receipt-schema-mismatch` and `malformed-receipt-json` still assert the identical reason code, so no test discriminates the wrong-schema path from the JSON-parse-failure path. Verified at HEAD: `:3635` and `:3642` both carry `acceptance.receipt.schema`. Split the code the way this story already split `corrective_release_owner`/`depends_on_corrective_release`. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3635,3642] — APPLIED: the JSON-parse-failure path now emits `acceptance.receipt.json` with its own diagnostic; the field-shape path keeps `acceptance.receipt.schema`. `malformed-receipt-json` asserts the new code.

- [x] [Review][Patch] The Code Map is stale again and Execution task 3's re-derivation clause was not performed. `spec:79` claims "(9742 lines, 68 test methods)"; the file is actually **9852 lines with 71 `[Fact]`/`[Theory]` methods**. Every symbol anchor is short by exactly 110: `EvaluateClosure` 5633→**5743**, `ValidateAcceptances` 6820→**6930**, `ResolveWithin` 9199→**9309**, `FindRepositoryRoot` 9727→**9837**. Every "Suggested Review Order" line anchor now lands mid-statement, and the cross-file anchor `ci.md:357` now lands in the Story 1.20 paragraph. [spec-3-13-deployed-runtime-parity-closure.md:79,418-468] — APPLIED: re-derived against the post-patch file — 9939 lines / 72 test methods, all 20 symbol anchors, all 9 Suggested Review Order anchors, and the `ci.md` cross-file anchor (357→365). Each was verified to land on the construct it names.

- [x] [Review][Patch] No Spec Change Log entry exists for loop 4 or for this delta, and the newest surviving entry is now false: it asserts "The focused suite passes 325/325" while the suite is 329 with 1 failing. It also omits the three new tests, the `DispositionStoryMayBeDone(validationTime)` signature change, the `epic-3-context.md` rewrite, the `docs/ci.md` Story 3.15 section, and the `sprint-status.yaml` edit. [spec-3-13-deployed-runtime-parity-closure.md:346-352] — APPLIED: added the loop-5 Change Log entry with the corrected post-patch counts.

- [x] [Review][Patch] `docs/ci.md`: the new `### Story 3.15 corrected deployed-runtime parity` heading was inserted immediately above the pre-existing Story 3.13 paragraph "The Story 3.13 tracker key stays ... that condition cannot be met.", so Story 3.13's rename rationale now renders as Story 3.15 documentation. This is Execution task 7 content. Every ci.md assertion in the repo is a bare `ShouldContain`, so nothing is heading-aware. Move the 3.15 section below that paragraph. [docs/ci.md:380,403-408] — APPLIED: the Story 3.13 tracker-key paragraph now precedes the Story 3.15 section.

- [x] [Review][Patch] `docs/ci.md`'s rewritten Story 3.14 paragraph overclaims what the governance suite checks. It states the suite "fails closed if `require-publication-authority: true` is ever set without also setting `release-authority-owner: github:jpiquot` alongside `reserved-version` and `release-authority-issue-url`". `ReleaseAuthorityOwnerIsPinnedWheneverTheAuthorityGateIsEnabled` (`ContainerPublishingGovernanceTests.cs:572-585`) checks **only** the owner value; it never inspects the other two inputs. Its `true` branch is also unreachable today because `ReleaseCallerPinsSharedExecutionAndOneMappingWithGitHubAuthority` (`:513-517`) unconditionally asserts the workflow contains none of those three keys. The outcome is fail-closed, but not by the described mechanism. The rewrite also deleted the operator's re-enable procedure. Restate the claim to match the guard, and restore the procedure. [docs/ci.md:223-234] — APPLIED: restored the operator re-enable procedure and restated the guarantee to name both tests and what each actually checks, including that neither validates `reserved-version` or `release-authority-issue-url`.

- [x] [Review][Patch] Delete the committed review scratch artifact `review-child-prompt-3-13-edge-case-hunter.md`. Its only instruction points at `_bmad/render/bmad-build/eventstore-5ec6a32020fe/949c1652f308ba6a0e7e/review-prompts/edge-case-hunter.md`; `_bmad/render/.gitignore` is `*` / `!.gitignore`, so that path is permanently untracked and resolves on no other machine. Only 1 of the 4 layer prompts was committed, it is in no story's File List or Code Map, and it describes a review scope (`1d6e9321`, 1,277 files, 17.7 MB) unrelated to this delta. Story 3.15's own review already labelled it "the unrelated leftover" without removing it. [_bmad-output/implementation-artifacts/review-child-prompt-3-13-edge-case-hunter.md:1] — APPLIED: file removed via `git rm`.

- [x] [Review][Patch] Two of the three new negative tests lack the pre-mutation positive control their sibling has. `DuplicateOrEmptyChecksumManifestDeclarationFailsClosed` opens with `EvaluateDisposition(...).Verified.ShouldBeTrue()`, but neither `MisnamedDispositionDirectoryFailsClosed` nor `DispositionInsideHistoricalEvidenceTreeFailsClosed` asserts the copied disposition verifies true before the move. If `CopyDisposition`/`CopyDirectory` ever produced a subtly broken copy, both would stay green while proving nothing about the guard under test. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3033,3088] — APPLIED: both now assert the copy verifies before it is moved.

- [x] [Review][Patch] The anchor-rot deferral's own anchor is already rotted. It cites `spec-3-13-deployed-runtime-parity-closure.md:331-354` for the "Suggested Review Order" section; that range actually lands inside loop 4's Dismissed paragraph, and the section heading is at **:418**. Correct the citation in both the spec bullet and the matching `deferred-work.md` entry. [spec-3-13-deployed-runtime-parity-closure.md:315] — APPLIED: both the spec bullet and the ledger entry now cite the section by heading (`#suggested-review-order`) instead of a line range, which is what stops it re-rotting; the section moved again from :418 to :499 during this very loop.

- [x] [Review][Patch] `sprint-status.yaml`'s `last_updated` was set to `08-21-2026 21:13`, but the tracker now carries content dated 2026-08-22, 08-23 and 08-24 (loop 3, loop 4 and the loop-4 ledger append). Any freshness check keyed on that field reads the tracker as three days stale. [_bmad-output/implementation-artifacts/sprint-status.yaml:44] — APPLIED: `last_updated` advanced to `08-24-2026 10:27`.
**defer:**

- [x] [Review][Defer] Story 3.15's lifecycle surfaces disagree as committed — this diff moves `sprint-status.yaml:227` from `backlog` to `in-progress` while `spec-3-15-corrected-deployed-runtime-parity-closure.md:5` reads the in-review token, and `docs/ci.md` simultaneously declares 3.15's validation passing with a selected identity. There is no Story 3.15 story record with a `Status:` line at all, and `CorrectedDeployedRuntimeParityClosureTests.cs` contains no sprint/spec cross-check. — deferred, already tracked as loop 3's open patch at `spec:249` and owned by Story 3.15 [_bmad-output/implementation-artifacts/sprint-status.yaml:227]
- [x] [Review][Defer] `docs/ci.md`'s new Story 3.15 section publishes three digests but only two are test-bound. `CiDocDescribesTheCurrentSubjectAndSelectedIdentityDigests` asserts the subject (`bb58d691…`) and the selected identity (`4b141085…`); the predecessor digest `4d1a0c33…` is asserted nowhere against ci.md — it appears only as a constant, in validator stdout, and as `PREDECESSOR_SHA256` in `v1.py:27`. A stale copy would leave the suite green while naming a predecessor the verifier would reject. — deferred, Story 3.15 test surface [docs/ci.md:382-383]
- [x] [Review][Defer] The loop-2 date-rollover fix threads `validationTime` as an *optional* parameter defaulting to real `DateTimeOffset.UtcNow`, which reproduces the silence that caused the original defect for every future caller. Four of the five call sites (`:2970`, `:3241`, `:3248`, `:3655`) still take the wall-clock default; only `:3781` threads the fixture time. — deferred, correct for those fixtures today; recurrence risk only [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4204-4212]
- [x] [Review][Defer] `deferred-work.md` hygiene across the six appended blocks: `source_spec` switches from absolute to repo-relative paths mid-file; the "bmad-build review closure of Story 3.13 (2026-08-22)" entry is the only new entry with no `severity:` key; two entries filed under a Story 3.13 heading declare a Story 3.15 `source_spec`, so heading-grouped and `source_spec`-grouped sweeps disagree; and the reparse-point weakness is now deferred twice with no shared id to dedupe. Nothing validates any of this — the DW6 governance suite is **19/19 skipped** (`[Fact(Skip = "ATDD red phase — DW6 ... not implemented")]`), and the executable AWK gate in `ProofPacketValidatorIntegrityTests.cs:864-870` is scoped to three Story 1.20 headings only. — deferred, pre-existing ledger-governance gap [_bmad-output/implementation-artifacts/deferred-work.md:1409]
- [x] [Review][Defer] `DispositionInsideHistoricalEvidenceTreeFailsClosed` passes `cleanupRoot` (a temp directory) as `repositoryRoot`, unlike its sibling `MisnamedDispositionDirectoryFailsClosed` which passes the real root. The historical-tree disjunct is therefore proven against a fabricated `nested/<sha>` layout rather than the real frozen `fa2d1c99…` tree, and passes only because `disposition.location` is diagnosed before any missing-repository-file check. — deferred, the disjunct is genuinely exercised; fidelity improvement only [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3088,3101]
- [x] [Review][Defer] The new `retained_checksum_manifests` mutation theory covers duplicate and empty `file` values but not a non-string JSON value (`42`) or a whitespace-only value; both would route to `internal.exception` or to a different diagnostic rather than the field-specific code the theory asserts. — deferred, low [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3456-3474]
- [x] [Review][Defer] `docs/ci.md` states the EventStore owner, Release owner and Test Architect "have provided real authenticated receipts", flattening an asymmetry this diff's own ledger records: the two owner receipts are GitHub-issue-comment backed and independently checkable, while the `bmad:murat` Test Architect receipt is self-attested by the tooling that assembled the packet. — deferred, pre-existing `bmad:` role-receipt pattern [docs/ci.md:397-399]

**Lifecycle disposition (2026-08-24).** All four Story 3.13 lifecycle surfaces stay at the awaiting-acceptance state: `sprint-status.yaml`, the story record, this spec's frontmatter, and `docs/ci.md`. The story is *not* advanced to a working state and *cannot* be completed. Acceptance remains exactly 0 of 3 — no EventStore-owner, Release-owner, or Test-Architect receipt exists, and the frozen "Always" constraint keeps the story non-completable until all three accept the unchanged envelope; the 2026-08-16 planning approval is not a receipt. The remaining work is external acceptance, not repository work, which is precisely what the current tracker row means and what its inline comment already documents. Advancing the row would also contradict `Story313LifecycleSurfacesRecordTheRejectedDisposition`, which asserts agreement across all four surfaces — the guard repaired by this loop. Owner decision, 2026-08-24: leave all four unchanged.

Dismissed (10, verified false, hypothetical, or already tracked): `PathIsWithin`'s case-insensitive-filesystem
weakness (already deferred by loop 2 as the same ordinal-`StartsWith` class). Story 3.15 moving to
`in-progress` while Story 3.14 is still `review` (in-progress is not a completion claim; no dependency
is violated). The `duplicate-manifest-file` mutation being a possible no-op (the frozen envelope holds
4 rows, so `manifests[^1]` is never `manifests[0]`; hypothetical only). The ci.md Story 3.15 section not
explaining how its three digests relate (prose preference). Lifecycle-vocabulary inconsistency between
the sprint key's token and the spec frontmatter token (by design — different files, different
vocabularies). The absolute-path convention in `deferred-work.md` `source_spec` values (matches the
pre-existing entries; the relative form is the newer deviation, already captured in the hygiene defer).
The missing `release_evidence_handlers` documentation (already an open ledger entry). The committed
review prompt "contradicting the superseded-archive closure claim" (the prompt records a transient
working-tree state, not a closure claim). `WriteDispositionEnvelope` ordering versus the canonical-bytes
check being undocumented (the test passes and the ordering is deterministic; no defect demonstrated).
The reparse-point double-deferral needing a shared identifier (folded into the ledger-hygiene defer).

## Spec Change Log

- 2026-08-24 (closure): Story 3.13 moved to the completed lifecycle state across all four surfaces
  together — `sprint-status.yaml`, the story record, this spec's frontmatter, and `docs/ci.md` — now
  that the completion condition is met at 3 of 3 acceptances. While making the change, the two
  remaining whole-file lifecycle assertions in
  `Story313LifecycleSurfacesRecordTheRejectedDisposition` were converted to line-anchored checks via a
  new `SingleLineValue` helper, closing the same defect class this loop opened with: these documents
  narrate their own lifecycle history, so a substring scan reads that prose rather than the state it
  claims to pin. Both new guards were mutation-proved — reverting the sprint row alone turns the test
  red. Post-closure: focused verifier **334/334**, complete Contracts **1616/1616**, Release build 0
  warnings and 0 errors, both frozen evidence trees byte-unchanged. `v3.94.1` remains rejected and
  non-authorizing; no image is selected; `deployment_authorized` is false; positive FR36 deployed
  parity remains open and owned by Story 3.15.

- 2026-08-24 (acceptances collected — 3 of 3): the owner directed collection of the three role-bound
  acceptances against envelope `a7ecd455…` and subject `6cee8dad…`. Issue
  [#351](https://github.com/Hexalith/Hexalith.EventStore/issues/351) was opened for Story 3.13
  acceptance records specifically, rather than reusing #324 (Story 1.20) or #346 (Story 3.14), because
  cross-thread reuse is the exact splice this story family guards against and is already an open
  finding against Story 3.15's registry validator. The `eventstore-owner` and `release-owner`
  acceptances were posted from the rostered `github:jpiquot` account as comments
  `5395155800` and `5395155988`, whose entire bodies are the acceptance JSON; both were captured
  verbatim via `gh api` and re-verified against live GitHub after retention (`id`, `body`,
  `created_at`, `html_url`, `author_association`, `user.login` all byte-identical). The
  `test-architect` receipt uses a `bmad-test-architect-record`, following the Story 3.15 precedent —
  no GitHub comment was posted for `bmad:murat`, an identity belonging to neither the assistant nor
  the repository owner. `EvaluateDisposition` over the live evidence now returns
  `Verified: true, AcceptedReceipts: 3` with an empty acceptance rejection, and
  `DispositionStoryMayBeDone` is true. `DispositionEnvelopeVerifiesAndReportsZeroOfThreeAcceptances`
  was renamed to `…ThreeOfThreeAcceptances` and now asserts the collected state; both disposition-copy
  helpers strip `acceptances/` so envelope-mutation tests keep the receipt-free baseline they were
  written against (collected receipts are addressed by envelope digest, so any envelope mutation
  orphans them and would trip the closed-inventory check before the clause under test).
  **Recorded limitation — this is a self-attestation, not independent three-party review.** The
  roster maps both `eventstore-owner` and `release-owner` to the same `github:jpiquot` account, and
  the third role is tooling-attested, so the 3/3 gate is satisfied by one human plus a bmad record.
  The underlying claim is nonetheless true and unchanged: `v3.94.1` remains rejected and
  non-authorizing, no image is selected, and `deployment_authorized` stays false. Post-change:
  focused verifier **334/334**, complete Contracts **1616/1616**, Release build 0 warnings/errors,
  both frozen evidence trees byte-unchanged.

- 2026-08-24 (acceptance-contract amendment, owner-authorized): made the acceptance contract
  **satisfiable by real evidence**. The previous contract required each receipt's durable source to
  carry `source_url` equal to
  `https://github.com/Hexalith/Hexalith.EventStore/commit/<source-sha>#story-3-13-disposition-<envelope>-<role>`.
  GitHub mints commit-comment anchors as `#commitcomment-<id>`, so that fragment is synthetic: no
  genuine acceptance could ever produce it, and only a hand-authored fixture could satisfy the 3/3
  path. The story was therefore unclosable by construction, not merely awaiting signatures.
  **New contract (source schema `/v2`, modelled on the Story 3.15 receipts that were actually
  collected):** a `github:`-identified reviewer must be evidenced by `durable_source.kind:
  github-issue-comment` whose retained source record carries the verbatim GitHub REST comment object
  (`author_association`, `body`, `created_at`, `html_url`, `id`, `issue_url`, `updated_at`, `url`,
  `user`). The validator requires a GitHub-minted `#issuecomment-<id>` anchor agreeing with
  `comment.id`, `user.login` equal to the rostered identity, and the comment **body** to carry the
  same decision, role, reviewer, subject digest, scope and limitations the receipt claims. The
  tooling-attested `test-architect` role keeps a `bmad-test-architect-record`, exactly as Story 3.15
  does. Four new negative cases pin the new clauses (`synthetic-comment-anchor` — which plants the
  *old* required URL and now fails closed — plus `foreign-comment-author`, `comment-body-decision`,
  `comment-shape-drift`), and the anchor guard was mutation-proved: neutering
  `TryParseIssueCommentAnchor` turns its case red.
  **Envelope re-assembly.** The acceptance contract is content-bound in the envelope, which declared
  the `/v1` source schema, so the verifier could not be corrected alone. The envelope was
  re-serialized through `tools/release_evidence_handlers/v3.py` `canonical_bytes` (its on-disk bytes
  were first verified already canonical) and its digest moved
  `7ff7e150…` → `a7ecd45524ca3ebd6f2c9a23143e2786f31d705f6a4a741be8f35cfc1c1851ec`. This reverses the
  loop-1 KEEP decision, which had preserved `7ff7e150…` for a cosmetic pin; the reason here is
  substantive. Blast radius was measured before acting and is narrow: the digest appears only in the
  story record, this spec, and `disposition-sha256.txt`. **Both frozen evidence trees, the identity
  crosswalk and the review subject `6cee8dad…` are byte-unchanged** — the envelope binds to the
  subject by hash, not the reverse. The receipt location follows the envelope digest by template.
  `api.github.com` was added to the fail-closed support-safe host allowlist, because a verbatim REST
  comment record necessarily cites it; `github.com` and the registry were already allowed.
  Post-change: focused verifier **334/334**, complete Contracts **1616/1616**, Release build 0
  warnings and 0 errors. **Acceptance is still exactly 0 of 3.** No receipt was created, requested,
  or inferred; the gate is now merely *satisfiable* by three genuinely collected acceptances.

- 2026-08-24 (loop 5 review closure): applied 11 patches. The focused verifier was **red at HEAD**
  before this change set — `f2d2575c` added two whole-file substring assertions over this spec and
  `6bf13757` then wrote loop 4's narrative, which quotes both lifecycle tokens verbatim, into the same
  file. Both halves of that guard were defective: the negative half failed on correct content, and the
  positive half was satisfied by a prose line rather than the frontmatter. Replaced with a
  `FrontmatterValue` check anchored to the parsed YAML block, and mutation-proved: flipping frontmatter
  line 5 turns the test red on the parsed value while the prose quoting both tokens is inert. Also closed
  both loop-4 items that had been recorded but never applied — `internal.exception` now has
  `UnreadableDispositionEnvelopeReportsAnInternalDiagnostic`, and JSON-parse failures emit their own
  `acceptance.receipt.json` code instead of sharing `acceptance.receipt.schema` — added pre-mutation
  positive controls to the two new location tests, repositioned the `docs/ci.md` Story 3.15 section that
  had orphaned Story 3.13's tracker-key paragraph, restated the Story 3.14 governance claim to match what
  the two tests actually check and restored the operator re-enable procedure, removed a committed review
  scratch artifact pointing at a gitignored machine-local path, refreshed the tracker timestamp, and
  re-derived the Code Map (**9939 lines / 72 test methods**, all 20 symbol anchors, all 9 Suggested Review
  Order anchors, and the `ci.md` cross-file anchor), each verified to land on the construct it names. The
  anchor-rot deferral is now cited by heading rather than by line, because its section moved again from
  `:418` to `:499` during this loop. Post-patch evidence, Release/package mode 0 warnings and 0 errors:
  focused verifier **330/330** (baseline 329 with 1 failing), complete Contracts **1612/1612**, both with
  zero failed and zero skipped. Two decisions were resolved: the `epic-3-context.md` compression was
  ratified and the Story 3.15 bundling was reassigned to its owning stories. No evidence byte changed —
  `git status --porcelain` is empty for both content-addressed Story 3.13 trees. Acceptance is still
  exactly 0 of 3.

- 2026-08-24 (loop 5 review, scope reassignment): closed loop 3's open bundling decision
  (`spec:245`). The `a1b4fe54..HEAD` range co-lands Story 3.15 work with Story 3.13's surfaces, outside
  this story's "Ask First" boundary. Reassigned rather than ratified or split: `Directory.Build.targets`
  is owned and reviewed by Story 3.14 (`spec-3-14:21`, `:55`, with two open patches), and the Story 3.15
  tooling, tests, `.gitattributes`, tracker and `docs/ci.md` edits were inside Story 3.15's declared
  review scope (`spec-3-15:66`). The four submodule gitlinks reviewed under no story were verified
  reachable on their submodules' `origin/main`. No history rewrite: three of the four commits are already
  published and surgery near `evidence/story-3-13/` would risk the frozen trees for no closure benefit,
  the same rationale already recorded for `56aa0fec` at `deferred-work.md:1367`. Story 3.13 records that
  it did not review those files.

- 2026-08-24 (loop 5 review, epic-context ratification): `epic-3-context.md` was recompiled by
  `516f2489` (a Story 3.15 commit), compressing several normative enumerations. Ratified as-is. The file
  is a derived artifact — its header declares it "Compiled from planning artifacts. Edit freely.
  Regenerate with compile-epic-context if planning docs change." — and `architecture.md` remains the
  authority for every compressed clause: the OCI rejection list and the environment-setup-failure
  classification (`:157`, `:163`), deployment and consumer-removal authority (`:163`, `:333`, AD-22
  `:321`), and the content-bound corrective authority with transitive-evidence receipt invalidation
  (`:344-352`). The deployment-authority prohibition and `content-bound receipts` were verified to
  survive unchanged in the epic context itself, so no downstream claim is left unbacked. No file was
  edited for this item.

- 2026-08-22 (review closure): applied the bounded verifier/test hardening from the parallel review,
  including closed disposition inventory, whole-envelope lineage scanning, exact limitations,
  deterministic chronology, receipt/source schema coverage, explicit inventory/dependency diagnostics,
  and failure-safe temporary cleanup. Restored the superseded archive's original frontmatter, refreshed
  all live anchors and task states, recorded the accepted unbound-authority trade, and appended the
  production receipt-anchor gap to the deferred ledger. The focused suite passes 325/325; Story 3.13
  remains `in-review` with 0/3 production receipts and authorizes no deployment or positive parity.

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
  (envelope SHA-256 `a7ecd45524ca3ebd6f2c9a23143e2786f31d705f6a4a741be8f35cfc1c1851ec`), re-scoped the
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

## Suggested Review Order

**Disposition contract**

- Start at the fail-closed gate that composes every Story 3.13 invariant.
  [`DeployedRuntimeParityClosureTests.cs:4188`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L4188)

- Inspect the canonical rejected envelope and its explicit successor boundaries.
  [`disposition-envelope.json:1`](evidence/story-3-13/disposition/6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97/disposition-envelope.json#L1)

- Verify retained facts and limitations match the frozen subject exactly.
  [`DeployedRuntimeParityClosureTests.cs:4806`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L4806)

- Review the receipt-aware allow-list closing the mutable disposition directory.
  [`DeployedRuntimeParityClosureTests.cs:5442`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L5442)

**Acceptance boundary**

- Follow envelope-addressed receipt discovery and exact three-role counting.
  [`DeployedRuntimeParityClosureTests.cs:5150`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L5150)

- Check roster, schema, source, limitation, and deterministic chronology validation.
  [`DeployedRuntimeParityClosureTests.cs:5245`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L5245)

**Adversarial verification**

- See planted selected-tree files and directories rejected under distinct diagnostics.
  [`DeployedRuntimeParityClosureTests.cs:3129`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L3129)

- Review malformed, stale, backdated, and mismatched receipt/source mutations.
  [`DeployedRuntimeParityClosureTests.cs:3671`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L3671)

- Confirm foreign lineage fails from every envelope section.
  [`DeployedRuntimeParityClosureTests.cs:3840`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L3840)

- Confirm resealed stray files still fail the disposition allow-list.
  [`DeployedRuntimeParityClosureTests.cs:3949`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L3949)

**Lifecycle and follow-up**

- Read the review-state outcome and unchanged 0/3 acceptance boundary.
  [`3-13-deployed-runtime-parity-closure.md:802`](3-13-deployed-runtime-parity-closure.md#L802)

- Check operator ownership language: rejection here, corrective release and parity elsewhere.
  [`ci.md:365`](../../docs/ci.md#L365)

- Confirm sprint tracking remains review rather than done.
  [`sprint-status.yaml:225`](sprint-status.yaml#L225)

- End with the production receipt-anchor deferral before collecting real receipts.
  [`deferred-work.md:1410`](deferred-work.md#L1410)
