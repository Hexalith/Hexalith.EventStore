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

- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs` (9742 lines, 68 test methods).
  - `EvaluateDisposition:3994` -- the negative Story 3.13 gate. It enforces canonical bytes, the closed disposition inventory, frozen-subject bindings, exact retained defects/limitations, whole-envelope foreign-lineage rejection, chronology, and three envelope-addressed receipts.
  - `RejectSelectedEvidenceInventory:4711` and `RejectDispositionManifest:5245` -- separate closed inventories for the frozen selected tree and the mutable disposition/receipt directory.
  - `RejectForeignLineage:4910` and `RejectDispositionReceipt:5051` -- recursively scan the complete envelope and validate each receipt plus its durable source with injectable validation time.
  - `EvaluateClosure:5633` -- the preserved positive gate. It remains welded to `ApprovedSourceSha = fa2d1c99…`; keep it and its negative tests intact.
  - `ValidateAcceptances:6820`, `ValidateActualFailClosedSubject:7093`, `LoadReviewerRoster:7304`, and `EvidenceDirectoryHasNoUnlistedFiles:7543` -- retained predecessor behavior.
  - Reuse: `ResolveWithin:9199` (reparse-point safe), `ComputeSha256:9248`/`:9250`, `ParseChecksumManifest:9169`, `VerifyChecksumManifest:9148`, `Binding:9142`, `ReadEvidenceFile:9196`, `FindRepositoryRoot:9727`.
  - **Trap:** a positive-gate mutation test that changes bytes must rebind through `EvaluateWithFreshReview:8982` (or `PersistRuntimeBindings:9002` for runtime files); calling the gate directly lets upstream byte pins reject first.
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
`7ff7e1501d1cdb49307f820dcdd0d8abc15bf2eee01c9e7450fc54255d8dfba4`; the bound governing authority
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

- [ ] [Review][Patch] Record that the 2026-08-14 authority is deliberately unbound — add an explicit note to the spec (Design Notes or Code Map) stating that `sprint-change-proposal-2026-08-14.md` is cited as narrative context only, is **not** content-bound, and that this is an accepted trade to preserve the frozen envelope digest `7ff7e150…`. Without the note the next loop will re-raise it as a binding hole. [spec-3-13-deployed-runtime-parity-closure.md:113-126]
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

## Spec Change Log

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

## Suggested Review Order

**Disposition contract**

- Start at the fail-closed gate that composes every Story 3.13 invariant.
  [`DeployedRuntimeParityClosureTests.cs:3994`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L3994)

- Inspect the canonical rejected envelope and its explicit successor boundaries.
  [`disposition-envelope.json:1`](evidence/story-3-13/disposition/6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97/disposition-envelope.json#L1)

- Verify retained facts and limitations match the frozen subject exactly.
  [`DeployedRuntimeParityClosureTests.cs:4612`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L4612)

- Review the receipt-aware allow-list closing the mutable disposition directory.
  [`DeployedRuntimeParityClosureTests.cs:5245`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L5245)

**Acceptance boundary**

- Follow envelope-addressed receipt discovery and exact three-role counting.
  [`DeployedRuntimeParityClosureTests.cs:4956`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L4956)

- Check roster, schema, source, limitation, and deterministic chronology validation.
  [`DeployedRuntimeParityClosureTests.cs:5051`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L5051)

**Adversarial verification**

- See planted selected-tree files and directories rejected under distinct diagnostics.
  [`DeployedRuntimeParityClosureTests.cs:3089`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L3089)

- Review malformed, stale, backdated, and mismatched receipt/source mutations.
  [`DeployedRuntimeParityClosureTests.cs:3541`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L3541)

- Confirm foreign lineage fails from every envelope section.
  [`DeployedRuntimeParityClosureTests.cs:3705`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L3705)

- Confirm resealed stray files still fail the disposition allow-list.
  [`DeployedRuntimeParityClosureTests.cs:3809`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L3809)

**Lifecycle and follow-up**

- Read the review-state outcome and unchanged 0/3 acceptance boundary.
  [`3-13-deployed-runtime-parity-closure.md:802`](3-13-deployed-runtime-parity-closure.md#L802)

- Check operator ownership language: rejection here, corrective release and parity elsewhere.
  [`ci.md:357`](../../docs/ci.md#L357)

- Confirm sprint tracking remains review rather than done.
  [`sprint-status.yaml:225`](sprint-status.yaml#L225)

- End with the production receipt-anchor deferral before collecting real receipts.
  [`deferred-work.md:1410`](deferred-work.md#L1410)
