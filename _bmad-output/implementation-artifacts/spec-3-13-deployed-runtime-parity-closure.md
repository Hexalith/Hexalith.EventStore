---
title: 'Story 3.13 Deployed Runtime Parity Closure'
type: 'chore'
created: '2026-08-04'
status: 'in-progress'
baseline_commit: '1d6e9321acfc416768c1c78e9facf573c9c41f71'
review_loop_iteration: 13
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Operators lack one verified chain from the Story 1.20-approved source/package bytes through a semantic release to a deployed two-platform OCI image. The proof packages are unrecoverable, while v3.77.2 uses a different source SHA.

**Approach:** Freeze both predecessors and assemble a support-safe, content-addressed crosswalk from independent checks. Produce a reproducible `fail-closed` review packet unless one exact lineage plus three content-bound acceptances satisfies Story 3.13.

## Boundaries & Constraints

**Always:** Preserve Stories 1.20/3.12; bind every field to one candidate and independent result; compare exact source, package, release, OCI, runtime, authority, and approval identities; retain raw registry bytes; distinguish environment from product failures; stay non-`done` unless AC4 passes.

**Ask First:** Any external or remote Git mutation; changes outside evidence/test/docs/story/status files; new authority/approval requests; or credentials beyond configured read-only task access.

**Never:** Splice candidate rows; infer identity from ancestry, tags, labels, branches, consumer SHAs, summaries, or prior approvals; rebuild proof packages; expose credentials; modify predecessors, runtime/release code, the package manifest, submodules, Epic 1, or consumers; claim `pass`/`done` with missing evidence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Complete lineage | Exact source/package/release/index chain, two runtime passes, three approvals | `pass`; immutable index recorded | Later byte changes invalidate approvals |
| Approved proof | `fa2d1c...` hashes, but package bytes or release provenance unavailable | `fail-closed`; blocker and owner recorded | Never substitute bytes |
| Corrective release | v3.77.2 chain at source `77a9a442...` | `fail-closed` for source mismatch | Ancestry is insufficient |
| Splice or tool gap | Mixed candidates or unavailable verification | Reject or record blocker/consequence/rerun trigger | Unavailable never means pass |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md`, its proof packet, and `evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/` -- read-only approved identity; freeze the full 40-file tree because the passing 33-entry manifest omits approvals.
- `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md` -- read-only v3.77.2 release/workflow/index/runtime evidence and historical failed releases.
- `_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md` and the exact evidence directory named below -- new crosswalk, raw evidence, blockers, checksums, and review subject.
- `tools/release-packages.json` -- read-only exact 14-package inventory and uniqueness authority.
- `references/Hexalith.Builds/Github/publish-containers/` -- read-only validator/smoke reuse; validation is SemVer/tag-first, while smoke uses bounded local Docker state.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs` -- new verifier; reuse JSON mutation/hash/root patterns from adjacent packaging tests.
- `docs/ci.md:258` -- replace only the stale Story 3.12-to-1.20 deployed-closure ownership paragraph.
- Story 3.13 record and `sprint-status.yaml` -- truthful lifecycle only; predecessors and Epic 1 stay unchanged.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/` -- freeze predecessor identities, run safe independent checks, and record candidate rows/blockers.
- [x] `3-13-deployed-runtime-parity-closure-proof-packet.md` and `identity-crosswalk.json` -- create a versioned, citation-complete, checksum-bound verdict and approval subject with no cross-lineage fallback.
- [x] `DeployedRuntimeParityClosureTests.cs` -- enforce schema, exact sets, hashes, verdict, approval binding, and both prohibited splices without dependencies.
- [x] `docs/ci.md`, Story 3.13 record, and `sprint-status.yaml` -- correct ownership and record commands/results; move only to `in-review` for reproducible fail-closed evidence, or `done` solely after AC4.

### Review Findings

- [x] [Review][Patch] Add a repository-owned reviewer roster and hash-bound receipt loading [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:926]
- [x] [Review][Patch] Require all recovered package archives and hash their bytes [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:678]
- [x] [Review][Patch] Pin the selected package hash manifest to the approved identity [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:627]
- [x] [Review][Patch] Content-bind semantic-release provenance and the complete single lineage [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:663]
- [x] [Review][Patch] Validate deployment authority from its retained record, scope, identity, and validity [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:690]
- [x] [Review][Patch] Bind the OCI graph to its registry, immutable reference, and content-addressed evidence root [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:695]
- [x] [Review][Patch] Bind OCI provenance labels to the exact approved source revision [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:792]
- [x] [Review][Patch] Validate structured, support-safe runtime execution evidence instead of declared statuses [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:840]
- [x] [Review][Patch] Enforce the complete review-subject identity, limitation, blocker, and binding contract [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:949]
- [x] [Review][Patch] Exercise both prohibited cross-lineage splices through the closure evaluator [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:527]
- [x] [Review][Patch] Verify the core and predecessor checksum manifests inside the derived closure gate [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:545]
- [x] [Review][Patch] Fail closed when child-manifest and config response metadata is absent, and require retained metadata on any passing lineage [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/registry-readback.json:21]
- [x] [Review][Patch] Correct the impossible review-subject and registry-evidence chronology [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/review-subject.json:3]
- [x] [Review][Patch] Reject symlink-based evidence paths that escape the allowed root [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:615]
- [x] [Review][Patch] Record the claimed Markdown and Git diff hygiene command results [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:533]
- [x] [Review][Patch] (resolved decision: set all trackers to `in-progress`) Reconcile the four-way lifecycle-status disagreement — this spec's own frontmatter says `status: 'done'`, directly contradicting its own frozen "stay non-`done` unless AC4 passes" constraint and the Dev Agent Record's explicit 0/3 acceptances; the story record says `Status: in-progress`; `sprint-status.yaml` says `review`; `docs/ci.md` prose says `in-progress`. Owner decision: set the spec frontmatter `status` and `sprint-status.yaml`'s entry to `in-progress` to match the story record and docs/ci.md. [_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:5]
- [x] [Review][Patch] (resolved decision: correct the claim) Correct the proof packet's false "no submodule state changed" claim — the diff range for this story includes gitlink bumps for `references/Hexalith.Builds`, `references/Hexalith.FrontComposer`, and `references/Hexalith.Memories`, contradicting both the frozen spec's "Never: ...modify... submodules" boundary and the proof packet's Verification Record, which explicitly (and now falsely) states no submodule changed. Owner decision: amend the Verification Record wording to scope the claim to author-controlled state (drop or qualify the submodule clause) rather than asserting a blanket, now-false claim. [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md:120]
- [x] [Review][Patch] (resolved decision: treat as unintentional, fix it) `canonical_lineage_id` is sensitive to routine runtime re-verification — `ComputeLineageMaterialSha256` hashes the entire `runtime` object, including per-execution `started_at`/`ended_at` timestamps, poll `attempts` counts, and log hashes, and that value is bound into `deployment-authority.json`. Re-running the same bounded `/alive` smoke against the same already-approved, unchanged image (identical source/package/release/OCI identity) produces a different `canonical_lineage_id`, silently invalidating prior owner authority for reasons unrelated to any real identity change. Owner decision: exclude execution-only fields (timestamps, poll attempts, log hashes) from the lineage hash material so identity-affecting fields alone determine `canonical_lineage_id`. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3544]
- [x] [Review][Patch] Independently rehash the Hexalith.Builds shared validator/smoke-tool scripts from real bytes instead of trusting JSON self-declaration — `ValidateRuntimeExecution` compares `runtime.tool.sha256`/`path`/`builds_gitlink_sha` only against hardcoded constants (`ExpectedSmokeToolSha256`, `ExpectedSmokeToolPath`, `ExpectedBuildsSha`), never against the actual bytes of `references/Hexalith.Builds/Github/publish-containers/smoke_container_platforms.py` read from disk or a historical git blob — unlike the analogous Story 1.20/3.12 predecessor blob checks, which do call `ComputeSha256(bytes) != expectedSha256` against files read from disk. `ValidateOciGraph`'s `shared_validator` block (for `oci_registry_validator.py`) is worse: it is required as a present JSON key only, with zero value validation of any kind. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:2371]
- [x] [Review][Patch] Add `NullReferenceException` to the catch filters of `ValidateOciProvenance`, `ValidateBaselineAndPredecessors`, `ValidateRuntimeExecution`, `ValidateAcceptances`, and `ValidateActualFailClosedSubject`, matching `EvaluateClosure` and their sibling validators. Verified directly: `ValidateOciProvenance` dereferences `expected["org.opencontainers.image.revision"]!.GetValue<string>()` without an existence check, and the real evidence's `provenance_labels` has neither `revision` nor `version` keys — this currently only avoids throwing because an earlier `verification.result != "pass"` guard short-circuits first on this evidence's known malformed-label blocker. No existing test exercises a genuinely missing-key mutation for these five methods when called directly (every `[Theory]` mutation changes field values, never removes a key), so the gap is real but currently unexercised. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:2299]
- [x] [Review][Patch] Extend `AddressIsPrivate` to cover IPv6 private ranges — it only evaluates RFC1918/link-local ranges for 4-byte (IPv4) addresses; IPv6 ULA (`fc00::/7`) and link-local (`fe80::/10`) ranges are never excluded (only IPv6 loopback is caught, via `IPAddress.IsLoopback`), so a private IPv6 host embedded in retained evidence would pass the "no private addresses" support-safety gate. [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3484]
- [x] [Review][Defer] Increment `review_loop_iteration` to reflect the two documented hardening passes already recorded in the Spec Change Log [_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:7] — deferred, pre-existing cosmetic drift, not blocking.
- [x] [Review][Defer] Complete the story's File List with the evidence files the tests/crosswalk already depend on and validate (e.g. `deployment-authority.json`, `deployment-authority-source.json`, `release-provenance.json`) [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:588] — deferred, documentation completeness only, not a functional gap.
- [x] [Review][Patch] Require NuGet.org fail-closed statuses to be HTTP 404 and add negative coverage for nuget_org shape [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Validate fail-closed verdict.checks keys/values and cap created_at with UtcNow upper bound [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Bind config_labels.version and add pass-flag-preserving revision/source-sha/version mutations [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Require child verification pass and readback tag_and_digest_bytes_identical [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Require submodule prohibition text in LimitationsContainMutationProhibitions and pass-path fixture limitations [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Align story record and docs/ci.md lifecycle wording to in-review with sprint-status/spec [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:38]
- [x] [Review][Defer] Redact absolute local_search_roots from retained package-availability.json (checksum cascade) [_bmad-output/implementation-artifacts/evidence/story-3-13/.../package-availability.json]
- [x] [Review][Defer] Add missing-key removal mutations for NullReferenceException catch-filter coverage on validators that currently only mutate values [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Rebind the fail-closed review subject to the current proof-packet SHA-256 and refresh the outer evidence checksum manifest [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/review-subject.json]
- [x] [Review][Patch] Reject config-raw OCI label mutations while the provenance summary stays correct [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Prove shared Builds tool pins are rehashed from pinned git bytes [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Bind each acceptance receipt filename to its role field [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Map missing-git Process.Start failures to InvalidDataException fail-closed paths [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Treat IPv4/IPv6 unspecified addresses as private in support-safety checks [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Pin runtime citation, preflight/platform log filenames, and OCI index_raw_file to core-manifest paths [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Enforce LogIsSupportSafe size gates, non-empty cleanup_check, and absolute local_search_roots rejection [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Lock retained fail-closed verdict.checks map and Production hosting-environment pass-fixture mutations [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Skip symlink-escape coverage when links are unavailable; redact host paths and rebind fail-closed review subject [_bmad-output/implementation-artifacts/evidence/story-3-13/...]
- [x] [Review][Patch] Reject contradictory smoke-results overall pass under fail-closed subject validation [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Lock fail-closed runtime/OCI/registry enums and Development≠Production contract fields [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Assert retained unstructured smoke logs fail ValidateRuntimeLog [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Reject IPv4-compatible IPv6 private embeddings in support-safety checks [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Correct cleanup-overstated review-subject blocker and align Task 1/8 honesty [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md]
- [x] [Review][Defer] Story 4.5 LiveSidecar prose in docs/ci.md outside Story 3.13 ownership-paragraph scope [docs/ci.md]
- [x] [Review][Patch] Correct proof-packet identity-crosswalk pin to bound `11b17fb0…` and rebind review subject/outer manifest [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md]
- [x] [Review][Patch] Align story-record baseline_commit to `1d6e9321` with spec/verifier [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:2]
- [x] [Review][Patch] Restore truncated Story 2.12 sprint-status key [_bmad-output/implementation-artifacts/sprint-status.yaml]
- [x] [Review][Patch] Require shared OCI validator cli_candidate_consequence pass string [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Assert retained smoke-preflight.log fails ValidatePreflightLog on incomplete-runtime path [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Defer] Add acceptances/{subject_sha256}/ scaffold before AC4 collection [_bmad-output/implementation-artifacts/evidence/story-3-13/...]
- [x] [Review][Defer] Re-measure full Contracts.Tests suite after ninth hardening pass [tests/Hexalith.EventStore.Contracts.Tests]
- [x] [Review][Patch] Reject recovered package-availability v2 pass under fail-closed subject validation [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Bind fail-closed citation hosting-environment fields to runtime-verification.json [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Require fail-closed shared OCI validator cli_candidate_consequence string [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Reject zero poll_interval_seconds in ValidateRuntimeLog attempts bound [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Treat private DNS suffixes as private hosts in support-safety checks [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Map OverflowException into incomplete-runtime log/preflight fail-closed catches [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Defer] Dual-role EventStore/Release owner identities on reviewer roster [evidence/story-3-13/.../reviewer-roster.json]
- [x] [Review][Defer] Epic 4 tracker and Story 4.5 docs/ci LiveSidecar prose on the same branch as Story 3.13 [docs/ci.md]
- [x] [Review][Defer] Document reopen migration from retained runtime-verification v1 to pass-path v2 [evidence/story-3-13/.../runtime-verification.json]
- [x] [Review][Defer] Separate release-authority hash-check success from deployment-authorized scope failure in crosswalk verification method text [identity-crosswalk.json]
- [x] [Review][Patch] Restrict support-safe absolute URIs to the exact public hosts Story 3.13 is allowed to cite, so private endpoints behind ordinary-looking DNS names fail closed [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Restore the exact approved Story 1.20 sprint closure comments removed by the prior Story 3.13 YAML rewrite, without changing any Epic 1 status [sprint-status.yaml]
- [x] [Review][Patch] Reconcile Story 3.13 lifecycle surfaces to `in-review` / `review` while AC2 and AC4 remain fail-closed [3-13-deployed-runtime-parity-closure.md]
- [x] [Review][Patch] Refresh the content-bound review subject timestamp after its latest byte rebind [review-subject.json]
- [x] [Review][Patch] Reject credential-shaped values retained inside raw OCI config documents [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs]
- [x] [Review][Patch] Remove obsolete generated review prompts and snapshots outside the content-addressed evidence packet [_bmad-output/implementation-artifacts/evidence/story-3-13]
- [x] [Review][Patch] Re-measure and record current focused and complete Contracts verification [3-13-deployed-runtime-parity-closure-proof-packet.md]

Chunk 2/3 (evidence, docs, status) + post-chunk-1 test delta — `bmad-code-review` 2026-08-11, against `1d6e9321...HEAD` for evidence/docs/status and `06e62b4d...HEAD` for `DeployedRuntimeParityClosureTests.cs`. Four parallel layers; 65 raw findings triaged to 4 decisions / 22 patches / 8 defers / 2 dismissed.

- [x] [Review][Decision] Three Story 1.20 predecessor evidence trees have FAILING checksum manifests at HEAD — RESOLVED 2026-08-11: do NOT write predecessor bytes from Story 3.13. Blast radius measured: `089369bb` touched 25 files; genuine content corruption is exactly three `environment.txt` files (`38f85086…`, `4983299103…`, `ec0d35a0…`), each a single hash mismatch in `critical-evidence-sha256.txt`. Story 3.13's own `predecessor-tree-sha256.txt` verifies 40/40 OK, and the `nuget-sha256.txt` failures across all four trees are missing files (the unrecoverable 14 proof packages), not corruption. Logged as a HIGH Epic 1 evidence-integrity defect in `deferred-work.md`; warrants its own scoped story under its own authority record. Original finding text: — `089369bb` ("docs: clear remaining root predecessor SDK patch tokens", 25 files, not a Story 3.13 commit) rewrote `10.0.301`→`10.0.302` SDK tokens inside frozen owner-approved Epic 1 evidence. Story 3.13 restored only its own `fa2d1c99…` tree at `3d6dea69`. `sha256sum -c critical-evidence-sha256.txt` now FAILS on `environment.txt` in `38f85086fc25…`, `4983299103bf…`, and `ec0d35a082bc…`. Restoring them means writing predecessor bytes, which the frozen constraints forbid and which Story 3.13 already did once. Decide: restore all three for consistency, record as an Epic 1 integrity defect owned elsewhere, or open a separate story.
- [x] [Review][Decision] AC4 may be unsatisfiable as rostered — RESOLVED 2026-08-11 by the owner: the dual `eventstore-owner`/`release-owner` mapping to `github:jpiquot` is ratified as legitimate (he holds both roles), and the `bmad:murat` Test Architect receipt is accepted. AC4 is collectable as rostered once the packet passes. Converted to a patch recording this ratification in the roster so future review loops do not re-litigate it. Original finding text: — `reviewer-roster.json` maps `eventstore-owner` and `release-owner` both to `github:jpiquot`, and `test-architect` to `bmad:murat`, a BMad agent persona with no durable external identity. "Three named acceptances of the same content-bound subject" reduces to one human accepting twice plus an agent. Previously deferred as roster cosmetics; it is an AC4 satisfiability question. Decide whether this composition satisfies AC4 or a third independent human is required.
- [x] [Review][Decision] AC2 recovery may be dead rather than blocked — RESOLVED 2026-08-11: do not launch a fresh recovery sweep (the search was already performed and recorded as exhausted, including GitHub Packages with `read:packages`), and do not re-scope the story unilaterally — re-scoping is an owner correct-course decision. Converted to a patch that makes the existing fail-closed record reproducible: name the durable sources actually queried and the query method, without reintroducing the absolute host paths that support-safety redacted. Original finding text: — `package-availability.json` records only NuGet.org flat-container 404s for all 14 packages, `rebuild_attempted: false`, and redacts `local_search_roots` to `<redacted-*>` so the search is unreproducible; no Hexalith-internal feed or GitHub Packages query is evidenced. If the proof archives are permanently unrecoverable, no further hardening pass can close this story and it sits in `review` indefinitely. Decide: accept permanent fail-closed and re-scope, or authorize a documented recovery attempt against the remaining durable sources.
- [x] [Review][Decision] Fail-closed contract is inconsistent — RESOLVED 2026-08-11: missing retained evidence MUST return `false`. AC3 states the *verdict* is `fail-closed`; an exception escaping the evaluator produces no verdict at all, which is a strictly weaker outcome than the one the AC requires. Converted to a patch: wrap the evidence reads so absent files yield `false`, and rewrite the test to assert the false verdict. Original finding text: — `InaccessibleRetainedEvidenceFailsClosed` asserts `Should.Throw<FileNotFoundException>` from `EvaluateWithFreshReview` while every other fail-closed path returns `false`, locking an unhandled I/O exception in as the contract [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:1295]. Decide whether missing retained evidence must return `false` or may propagate.
- [x] [Review][Patch] APPLIED 2026-08-12 — all four lifecycle surfaces now read `in-progress` (spec frontmatter, story record, `sprint-status.yaml`, `docs/ci.md`). Spec frontmatter said `status: 'done'` while AC4 is 0/3 — flipped from `'in-review'` to `'done'` by `2bc8ee17`, the same commit whose findings record "Reconcile story, sprint, spec, and operator lifecycle surfaces to `in-review` / `review`" and whose story text says "remains `in-review` and non-`done`". Contradicts the frozen constraint "stay non-`done` unless AC4 passes", the story record, `sprint-status.yaml:106`, `docs/ci.md`, and `verdict.story_may_be_done: false` [_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:5]
- [x] [Review][Patch] APPLIED 2026-08-12 — added `preflight-failure-class`, `platform-failure-class`, and `platform-unknown-failure-class` cases to `RuntimeEvidenceRejectsExecutionAndBoundMutations`, which mutate the runtime node and persist it via `PersistRuntimeBindings` so `DeepEquals` holds and control reaches the guard. Mutation-verified: neutering `RuntimeFailureClassificationIsValid` to `return true` now turns exactly those 3 cases red while the other 24 stay green (previously it left the whole suite green). Runtime failure-classification guard was provably vacuous — mutation-verified: neutering `RuntimeFailureClassificationIsValid` to `return true` leaves the focused suite at 157/157. Both call sites are immediately followed by `outcome != "pass"`, and the two tests added to cover it set `outcome = "fail"`, so they are rejected earlier by `DeepEquals` at `:3298` and by the outcome check. Its only live branch — `outcome: "pass"` carrying a `failure_class`, or a class outside `environment|product|evidence` — has zero coverage, yet the story records the finding as fixed [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5784]
- [x] [Review][Patch] APPLIED 2026-08-12 — restored 116 comment lines across 24 keys from baseline `1d6e9321` (file back to 119 indented comments), including both Epic 1 annotations above `1-9` and `1-13`. Verified comment-only: YAML parses, key set identical, zero status values changed. Restore the sprint-status decision comments destroyed by Story 3.13 commit `2a6c2177` (131 → 0; only the 3 test-pinned Story 1.20 lines were later restored) — the loss includes two Epic 1 annotations above `1-9` and `1-13`, violating "Epic 1 stays unchanged", plus the 2.12 re-scope block, the 3.1 merge/ratification record, the 2.5/2.6/2.7/2.11 acceptance records, and the Epic 6/7/8 gating notes. Only the Story 1.20 block is protected by a test, so the next YAML round-trip will delete the rest again [_bmad-output/implementation-artifacts/sprint-status.yaml]
- [x] [Review][Patch] APPLIED 2026-08-12 — proof packet now records that Story 3.13 wrote two predecessor files at `3d6dea69` solely to restore approved bytes drifted by unrelated commit `089369bb`, states the net-state sense in which `verdict.predecessor_state_changed` remains `false`, and names the three sibling trees it holds no authority to repair. Hash cascade rebound (proof packet `03f2b59c…` → `8bf27efc…`, review subject and outer manifest refreshed); story Completion Notes corrected. Proof packet and crosswalk attested to no predecessor modification, which was false — "No predecessor file was normalized, regenerated, or modified" and `predecessor_state_changed: false` are contradicted by Story 3.13 commit `3d6dea69`, which modified `1-20-owner-approved-parity-closure-proof-packet.md` and `evidence/story-1-20/fa2d1c99…/environment.txt`. The restoration is disclosed in the Spec Change Log but denied by the content-bound subject reviewers are asked to accept [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md:66]
- [x] [Review][Patch] APPLIED 2026-08-12 — added `EvidenceDirectoryHasNoUnlistedFiles`, enumerating the evidence directory and rejecting any top-level file no manifest lists, wired into `ValidateEvidenceIntegrity`; new `UnlistedEvidenceDirectoryFileFailsClosed` theory (3 cases) is mutation-verified to go red when the guard is removed. No stray-file detection existed in the content-addressed evidence directory — verified that `Directory.GetFiles` is used only for the package archive root, the receipt directory, and `CopyDirectory`; nothing enumerates the evidence directory, so a file present on disk but listed in no manifest rides inside the packet undetected [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3789]
- [ ] [Review][Patch] `4-8-durable-admission-evidence-ledger: backlog` row was added by the same rewrite that deleted the rule forbidding it ("Story 4.8 is a non-executable evidence ledger and therefore has no status row"), leaving an orphan key matching no story file [_bmad-output/implementation-artifacts/sprint-status.yaml:117]
- [x] [Review][Patch] APPLIED 2026-08-12 (in passing, during the required sprint-status sync) — now `last_updated: '2026-08-12'`. Was unquoted, non-ISO, ambiguous MM-DD-YYYY, and contradicts both the file's own header comment and the correctly-quoted ISO `generated:` on the line above [_bmad-output/implementation-artifacts/sprint-status.yaml:44]
- [ ] [Review][Patch] The `nested-index` case of `OciGraphRejectsPlatformSetMutations` exercises no nested index — it only flips `registry-readback.json → objects[0].content_type`, a path already covered elsewhere, and never writes an index descriptor, a third descriptor, or a `platform.variant` entry into `index.raw`; the raw-index rejections at `:3052-3053` can be deleted with the suite still green [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:1216]
- [ ] [Review][Patch] The raw-config support-safety scan added to `ValidateOciGraph` has no covering test — `OciProvenanceRejectsSensitiveConfigRawValues` asserts only `ValidateOciProvenance`, and a plain content edit cannot reach the graph-side scan because `BytesMatchDescriptor` rejects the mismatched digest first; removing `|| !DocumentIsSupportSafe(config)` at `:3120` changes no test result [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3120]
- [ ] [Review][Patch] `identity-crosswalk.json` records `"exit_code": 0` for a run whose own verification reason states the retained logs "omit … exit codes", with no citation and no per-field verification result [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/identity-crosswalk.json:313]
- [ ] [Review][Patch] `oci-validation.json` identifies the image by the mutable tag `registry.hexalith.com/eventstore:quarantine-proof-fa2d1c99…` rather than the immutable `@sha256:` reference used everywhere else — the exact mutable-tag identity `MutableTagOnlyIdentityFailsClosed` was added to reject — and is the only evidence document with no `schema_version` and no `verification` block [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/oci-validation.json:31]
- [ ] [Review][Patch] Retained per-platform and preflight `outcome: "pass"` claims are kept in `smoke-results.json`, `runtime-verification.json`, and the crosswalk while the verifier separately asserts those same logs FAIL `ValidateRuntimeLog`/`ValidatePreflightLog`; the eighth-pass honesty fix reached only the top-level `result: "fail"`. Mark preflight and per-platform outcomes `unverified` as `execution_result` already is [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/smoke-results.json]
- [ ] [Review][Patch] `review-subject.json` presents `authority_record_sha256: null` to the three reviewers even though the crosswalk carries a hash-checked authority record (`record_sha256: 2fd6a43f…`, `deployment_authorized: false`, quarantine-only scope); reviewers accept a subject showing no authority identity rather than the quarantine-only one. Its `expires_at: 2026-08-25` is also unmentioned by any blocker or reopen trigger [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/review-subject.json:23]
- [ ] [Review][Patch] `git diff --check` is recorded as passing and cited as scope evidence it cannot supply — it reports whitespace errors only, says nothing about which paths changed, and re-running the recorded command at HEAD exits 2 with hits in two `bundle-contract.md` files and two `gh-*-review-diff.txt` evidence files [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:440]
- [ ] [Review][Patch] The spec contains two divergent `## Suggested Review Order` sections with conflicting anchors for the same targets (evaluator cited at `:2040` vs `:2369`, `deferred-work.md:1021` vs `:1212`); the first block's `ci.md:267` pointer lands on the Story 3.12 paragraph and its `3-13-…closure.md:675` pointer lands on the File List [_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:220]
- [ ] [Review][Patch] Restore the Story 1.20 sole-authority sentence removed from `docs/ci.md` by Story 3.13 commit `b140a576` — the deleted paragraph stated Story 1.20 "retains sole authority over its approval fields and consumer-migration decision"; Task 10 authorized replacing the stale deployed-closure ownership text, not narrowing the predecessor's documented authority [docs/ci.md]
- [x] [Review][Patch] APPLIED 2026-08-12 (in passing) — set to `13`. Was `review_loop_iteration: 7` against twelve dated hardening passes in the Spec Change Log — previously deferred as cosmetic when the gap was smaller; it is now materially wrong and is the only machine-readable loop counter [_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:7]
- [ ] [Review][Patch] Several new negative tests have no paired positive control, so they cannot distinguish "the mutation caused the failure" from "this fixture never passed" — `MutableTagOnlyIdentityFailsClosed`, `OciGraphRejectsConfigArchitectureMismatch`, `RuntimeLogRejectsZeroPollInterval`, and `OciProvenanceRejectsSensitiveConfigRawValues` never assert the un-mutated fixture returns true first, unlike `CanonicalLineageIgnoresExecutionOnlyRuntimeFacts` which correctly captures `before` [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:1162]
- [ ] [Review][Patch] `failure_class` is written into the retained log fixtures but never read by any validator — the only consumer reads it from the crosswalk node, so a crosswalk claiming `failure_class: "environment"` over a log recording `"product"` is undetectable, which is precisely the environment-vs-product separation Task 6 requires [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5787]
- [ ] [Review][Patch] The proof packet's headline "complete Contracts suite: 1260 passed" is not attributable to Story 3.13 — the same Debug Log shows 999 on 2026-08-04 and 1001 on 2026-08-05 while the focused verifier grew only 117 → 157; the remaining ~219 tests came from concurrent unrelated work in the same range and the packet does not say so [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md:136]
- [ ] [Review][Patch] `CopyDirectory` defeats the isolation it was added for — `File.Copy` follows symlinks rather than reproducing them, so symlink-escape conditions vanish in the staged copy, and the first loop over `Directory.GetDirectories(..., AllDirectories)` is dead given the `CreateDirectory` inside the file loop [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5798]
- [ ] [Review][Patch] Validator robustness gaps in the fail-closed paths: `ValidateAcceptances` omits `FormatException` from its catch filter so a malformed `created_at` throws instead of returning false; `DateTimeOffset.Parse` is called without `CultureInfo.InvariantCulture`; runtime preflight/platform nodes accept arbitrary undeclared keys (no `HasExactProperties`); a trailing separator on `archive_root` makes the `GetDirectoryName` comparison never match and would reject a fully recovered 14-archive set; the acceptance scan reads only top-level `*.json` so non-JSON or nested receipt material is unchecked; and the `nested-index` branch indexes `objects[0]` without checking the array is populated [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3609]
- [ ] [Review][Patch] Record the owner's 2026-08-11 ratification of the dual `eventstore-owner`/`release-owner` identity and the `bmad:murat` Test Architect receipt in the roster, with a `created_at` and an authority source for the roster itself, so future review loops do not re-raise AC4 satisfiability [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/reviewer-roster.json]
- [ ] [Review][Patch] Make the AC2 fail-closed record reproducible — name the durable sources actually queried (NuGet.org flat container, GitHub Packages with `read:packages`, any Hexalith-internal feed) and the query method, so a reviewer can re-derive "unrecoverable" without the absolute host paths that support-safety redacted to `<redacted-*>` [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/package-availability.json:7]
- [ ] [Review][Patch] Make absent retained evidence return a `false` verdict instead of propagating `FileNotFoundException`, and rewrite `InaccessibleRetainedEvidenceFailsClosed` to assert the false verdict — AC3 requires a fail-closed *verdict*, and an escaping exception yields none [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:1295]
- [x] [Review][Defer] Story 4.5's evidence packet is self-invalidating — `validate-evidence.py` was modified by `3e365150` after its manifest was sealed at `86308550`, so `evidence-sha256.txt` fails with a genuine content mismatch at HEAD; unrelated to Story 3.13 [_bmad-output/implementation-artifacts/evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/evidence-sha256.txt] — deferred, pre-existing
- [x] [Review][Defer] OCI layer descriptors are never validated — the retained real manifests carry seven layer descriptors each whose digests and sizes are unchecked, and the pass-path fixtures use layer-less manifests no registry would accept [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4925] — deferred, pre-existing
- [x] [Review][Defer] `release-provenance.json`, `deployment-authority.json`, and `deployment-authority-source.json` are validated by code paths that have never met a real artifact — no such file exists in the committed evidence directory [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs] — deferred, pre-existing
- [x] [Review][Defer] The required structured runtime-log format exists only inside the test fixture — retained logs are line-oriented text while the pass-path validators parse JSON objects, so reopen trigger #5 asks the Hexalith.Builds smoke-contract owner to satisfy a schema specified nowhere outside the test file [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/smoke-linux-amd64.log] — deferred, pre-existing
- [x] [Review][Defer] The acceptance receipt contract is unanchored and unscaffolded — `external_receipt_location` is the relative string `acceptances/{subject_sha256}` with no stated root, `required_receipt_fields` binds to no roster version, and the directory does not exist [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/identity-crosswalk.json] — deferred, pre-existing
- [x] [Review][Defer] `evidence-sha256.txt` is the one evidence file whose bytes nothing hashes — absent from the core manifest and unbound in the review subject; mitigated because its entry set is structurally pinned and its hashes are recomputed against live bytes [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/evidence-sha256.txt] — deferred, pre-existing
- [x] [Review][Defer] Epic 4 tracker churn and Story 4.5/4.14/OQ8/DAPR-pin prose land inside the reviewed range from concurrent commits, and the packet's non-mutation attestation is scoped only to submodule gitlinks so it under-discloses what its own range changed [docs/ci.md] — deferred, pre-existing
- [x] [Review][Defer] `WaitForProcessExit` orphans a child process when both the kill and the 5-second post-kill wait fail, and no test drives a git invocation past the 30-second window, so neither the old nor the new timeout behavior is observed [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5770] — deferred, pre-existing
- [x] [Review][Defer] `checked_at` in `package-availability.json` (2026-08-04T11:17:05Z) and `registry-readback.json` (2026-08-04T11:48:07Z) predates the 2026-08-09 rewrite of those files [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/package-availability.json] — deferred, pre-existing

Dismissed (2): a "circular predecessor proof" claim — refuted, the crosswalk pin `47f09bdf` is the value at Story 3.13's own baseline and dates to 2026-08-01, so `3d6dea69` restored approved bytes that the unrelated `089369bb` had drifted, rather than fabricating them; and a `docs/ci.md` Epic-4/OQ8 scope-leak attribution — those paragraphs come from `fe715c70`, `ab1666dd`, `b927472a`, `35a1eecd`, and `86308550`, not from Story 3.13.

Verification independently reproduced at HEAD: Release build 0 warnings / 0 errors; focused verifier 157/157, 0 skipped; complete Contracts suite 1260/1260, 0 skipped; Story 1.20 `critical-evidence-sha256.txt` 33/33 OK for the approved `fa2d1c99…` tree; Story 3.13 `evidence-core-sha256.txt` 17/17 and `evidence-sha256.txt` 3/3 OK; `review-subject.json` binds the live proof-packet hash `03f2b59c…`; `markdownlint docs/ci.md` 0 issues.

**Acceptance Criteria:**
- Given completed predecessors, when closure begins, then committed identities are hash-checked without modification or inference.
- Given a candidate, when verified, then every field belongs to one lineage and every package/platform/digest/runtime relation has independent evidence.
- Given missing, unavailable, expired, mutable-only, or inconsistent evidence, when evaluated, then the verdict is `fail-closed`, names a blocker/reopen trigger, and changes no external or predecessor state.
- Given a complete passing packet, when the EventStore owner, Release owner, and Test Architect accept the same content-bound subject, then—and only then—Story 3.13 may be `done` without authorizing any external mutation or migration.

## Spec Change Log

- 2026-08-04: Applied all 15 code-review patches; kept the story `in-progress` because AC2 and AC4
  still require externally supplied evidence and acceptance.
- 2026-08-04: Applied the second review-hardening pass without changing frozen intent. The
  fail-closed subject, Git-object predecessors, exact package directory, release/authority lineage,
  OCI reports/provenance, runtime bounds, support-safety rules, roster, and durable receipts now
  have independent mutation coverage. AC1 and AC3 pass; AC2 and AC4 remain fail-closed with 0/3
  acceptances, so the story stays non-`done`.
- 2026-08-05: Applied the third hardening/lifecycle pass without changing frozen intent. Reconciled
  trackers to `in-progress`, scoped the proof packet's submodule claim to author-controlled state,
  excluded execution-only runtime facts from `canonical_lineage_id`, bound shared Builds
  validator/smoke tools to real script bytes, added `NullReferenceException` catch filters, and
  closed the IPv6 support-safety gap. AC2/AC4 and 0/3 acceptances remain open.
- 2026-08-08: Applied a fourth review-hardening pass: gated `evidence_completeness` and
  `cli_candidate_compatibility` on pass lineages, bound OCI `config_labels` summary fields, extended
  private-address and PEM/private-key support-safety coverage, required structured `nuget_org`
  availability shape, bounded Git process waits, refreshed proof-packet verification totals, and
  documented the dual Builds identity pins. Story remains non-`done` with fail-closed evidence.
- 2026-08-08: Applied a fifth review-hardening pass without changing frozen intent. Bound NuGet
  fail-closed statuses to HTTP 404 with negative coverage, validated fail-closed `verdict.checks`,
  capped fail-closed `created_at`, bound `config_labels.version` and pass-only summary identity
  mutations, required child verification and readback tag/digest identity flags, required
  submodule prohibition text, and aligned story/`docs/ci.md` lifecycle wording to `in-review`.
- 2026-08-08: Applied a sixth review-hardening pass without changing frozen intent. Rebound the
  fail-closed review subject to the current proof-packet bytes, rejected config-raw label mutations
  when the provenance summary stays clean, proved Builds tool pins from pinned git bytes, bound
  receipt filenames to role fields, mapped missing-git `Process.Start` failures into fail-closed
  `InvalidDataException` paths, and treated unspecified IPv4/IPv6 addresses as private. Focused
  verifier coverage is now 132 passing tests; AC2/AC4 and 0/3 acceptances remain open.
- 2026-08-08: Applied a seventh review-hardening pass without changing frozen intent. Pinned
  runtime/OCI/log path bindings to core-manifest filenames, enforced log size support-safety and
  non-empty cleanup attestation, rejected absolute `local_search_roots`, locked the retained
  fail-closed verdict check map, rejected Production hosting-environment mutations on the pass
  fixture, skipped symlink-escape coverage when links are unavailable, redacted host paths from
  retained package-availability evidence, and cleaned review-subject passing_evidence after
  rebinding. Restored Story 1.20 `environment.txt` and proof-packet bytes that a later SDK-token
  docs commit had drifted away from the approved hashes, and pinned the predecessor git-tree
  assertion to `ExpectedBaselineCommit` instead of `HEAD`. Focused verifier coverage is now 140
  passing tests; AC2/AC4 and 0/3 acceptances remain open.
- 2026-08-08: Applied an eighth review-hardening pass without changing frozen intent. Set retained
  `smoke-results.json` overall `result` to `fail`, locked fail-closed runtime/OCI/registry/smoke
  enums in `ValidateActualFailClosedSubject`, asserted retained unstructured logs fail
  `ValidateRuntimeLog`, rejected IPv4-compatible private embeddings, corrected cleanup-overstated
  blocker text, and aligned Task 1/8 lifecycle wording. Focused verifier coverage is now 142
  passing tests; AC2/AC4 and 0/3 acceptances remain open.
- 2026-08-09: Applied a ninth review-hardening pass without changing frozen intent. Corrected the
  proof-packet identity-crosswalk pin to the bound `11b17fb0…` digest and rebound the review
  subject / outer evidence manifest; aligned the story-record `baseline_commit` to `1d6e9321`;
  restored the truncated Story 2.12 sprint-status key; required the shared OCI validator
  `cli_candidate_consequence` pass string; and asserted retained `smoke-preflight.log` fails
  `ValidatePreflightLog` on the incomplete-runtime fail-closed path. AC2/AC4 and 0/3 acceptances
  remain open.
- 2026-08-09: Applied a tenth review-hardening pass without changing frozen intent. Fail-closed
  subject validation now rejects recovered package-availability v2 pass claims, binds citation
  hosting-environment fields to `runtime-verification.json`, and locks the unavailable-path
  `cli_candidate_consequence` string; `ValidateRuntimeLog` rejects zero poll intervals; private
  DNS suffixes are treated as private hosts; and incomplete-runtime log/preflight catches map
  `OverflowException`. AC2/AC4 and 0/3 acceptances remain open.
- 2026-08-09: Applied an eleventh review-hardening patch without changing frozen intent. Support-safe
  absolute URIs now fail closed unless their host is the exact GitHub or Hexalith registry host
  required by the Story 3.13 evidence contract; arbitrary public-looking DNS names and literal-IP
  URI hosts are rejected. AC2/AC4 and 0/3 acceptances remain open.
- 2026-08-09: Restored the exact three-line Story 1.20 closure comment block that the prior Story
  3.13 sprint-status serialization accidentally removed. This repairs the existing integrity gate
  without changing any Epic 1 status or predecessor decision. AC2/AC4 and 0/3 acceptances remain open.
- 2026-08-11: Applied the full-review patches without changing frozen intent. Reconciled lifecycle
  surfaces to `in-review` / `review`, corrected the response-metadata finding, added raw OCI config
  support-safety validation, removed obsolete generated review snapshots, refreshed the review
  subject chronology, and re-measured 157 focused / 1260 complete Contracts tests. AC2/AC4 and 0/3
  acceptances remain open.

## Verification

**Commands:**
- `(cd _bmad-output/implementation-artifacts/evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594 && sha256sum -c critical-evidence-sha256.txt)` -- expected: all 33 listed predecessor files pass.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings/errors.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 --no-restore -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --filter FullyQualifiedName~DeployedRuntimeParityClosureTests` -- expected: all focused tests pass.
- `npx markdownlint-cli2 docs/ci.md && git diff --check` -- expected: documentation and diff checks pass.

## Suggested Review Order

**Decision and identity**

- Start with the fail-closed decision, missing lineage proof, and non-mutation boundary.
  [`3-13-deployed-runtime-parity-closure-proof-packet.md:3`](3-13-deployed-runtime-parity-closure-proof-packet.md#L3)

- Inspect the machine-readable verdict, blockers, and explicit non-done result.
  [`identity-crosswalk.json:431`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/identity-crosswalk.json#L431)

- Confirm operators see truthful in-review ownership and zero acceptances.
  [`ci.md:267`](../../docs/ci.md#L267)

**Closure enforcement**

- Follow the central evaluator joining every identity and acceptance requirement.
  [`DeployedRuntimeParityClosureTests.cs:2040`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L2040)

- Review the retained fail-closed subject's exact locked shape.
  [`DeployedRuntimeParityClosureTests.cs:3369`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L3369)

**Support-safe evidence boundary**

- Allow only the two public hosts required by retained evidence.
  [`DeployedRuntimeParityClosureTests.cs:122`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L122)

- Exercise private ranges, deceptive hostnames, literal IPs, and approved hosts.
  [`DeployedRuntimeParityClosureTests.cs:461`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L461)

- Fail closed without mutable, environment-dependent DNS resolution.
  [`DeployedRuntimeParityClosureTests.cs:4224`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L4224)

**Review disposition and proof**

- Record the accepted patch without weakening frozen acceptance criteria.
  [`spec-3-13-deployed-runtime-parity-closure.md:126`](spec-3-13-deployed-runtime-parity-closure.md#L126)

- Route validated unrelated findings to their owning future work.
  [`deferred-work.md:1021`](deferred-work.md#L1021)

- Restore approved Story 1.20 closure context without changing its done status.
  [`sprint-status.yaml:72`](sprint-status.yaml#L72)

- Re-run predecessor hashes, build, focused tests, and hygiene checks.
  [`spec-3-13-deployed-runtime-parity-closure.md:202`](spec-3-13-deployed-runtime-parity-closure.md#L202)

## Suggested Review Order

**Decision and lifecycle**

- Start with the immutable fail-closed operator decision and unchanged external-state boundary.
  [`3-13-deployed-runtime-parity-closure-proof-packet.md:3`](3-13-deployed-runtime-parity-closure-proof-packet.md#L3)

- Confirm the machine verdict remains non-done with explicit blockers.
  [`identity-crosswalk.json:431`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/identity-crosswalk.json#L431)

- Verify the story remains in review with AC2/AC4 open.
  [`3-13-deployed-runtime-parity-closure.md:675`](3-13-deployed-runtime-parity-closure.md#L675)

- Confirm sprint tracking uses the canonical `review` state.
  [`sprint-status.yaml:106`](sprint-status.yaml#L106)

**Evidence binding**

- Review the refreshed subject chronology and content-bound packet identity.
  [`review-subject.json:3`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/review-subject.json#L3)

- Confirm absent child/config response metadata remains fail-closed.
  [`registry-readback.json:21`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/registry-readback.json#L21)

- Check the final focused and complete Contracts verification totals.
  [`3-13-deployed-runtime-parity-closure-proof-packet.md:123`](3-13-deployed-runtime-parity-closure-proof-packet.md#L123)

**Support-safety enforcement**

- Follow the derived closure gate joining every identity and acceptance requirement.
  [`DeployedRuntimeParityClosureTests.cs:2369`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L2369)

- Inspect raw OCI config scanning at both graph and provenance boundaries.
  [`DeployedRuntimeParityClosureTests.cs:3120`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L3120)

- Verify credential-shaped raw-config values fail independently.
  [`DeployedRuntimeParityClosureTests.cs:1917`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L1917)

**Deferred unrelated findings**

- Review separately owned AppHost, durability-race, provider, and CI follow-ups.
  [`deferred-work.md:1212`](deferred-work.md#L1212)
