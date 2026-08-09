---
title: 'Story 3.13 Deployed Runtime Parity Closure'
type: 'chore'
created: '2026-08-04'
status: 'in-review'
baseline_commit: '1d6e9321acfc416768c1c78e9facf573c9c41f71'
review_loop_iteration: 7
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
- [x] [Review][Patch] Retain and validate child-manifest and config response metadata [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/registry-readback.json:21]
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

## Verification

**Commands:**
- `(cd _bmad-output/implementation-artifacts/evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594 && sha256sum -c critical-evidence-sha256.txt)` -- expected: all 33 listed predecessor files pass.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings/errors.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 --no-restore -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --filter FullyQualifiedName~DeployedRuntimeParityClosureTests` -- expected: all focused tests pass.
- `npx markdownlint-cli2 docs/ci.md && git diff --check` -- expected: documentation and diff checks pass.

## Suggested Review Order

**Identity pin honesty**

- Proof packet pin must match the bound crosswalk digest after subject rebind.
  [`3-13-deployed-runtime-parity-closure-proof-packet.md:27`](3-13-deployed-runtime-parity-closure-proof-packet.md#L27)

- Review subject binds the rebound proof-packet bytes without a checksum cycle.
  [`review-subject.json:13`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/review-subject.json#L13)

- Outer evidence manifest lists the rebound subject digest.
  [`evidence-sha256.txt:3`](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/evidence-sha256.txt#L3)

**Fail-closed runtime gates**

- Incomplete-runtime path also rejects unstructured smoke-preflight.log.
  [`DeployedRuntimeParityClosureTests.cs:387`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L387)

- Pass-path shared OCI validator consequence string is exact, not free-form.
  [`DeployedRuntimeParityClosureTests.cs:2479`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs#L2479)

**Lifecycle / tracker hygiene**

- Story baseline matches the spec/verifier review baseline.
  [`3-13-deployed-runtime-parity-closure.md:2`](3-13-deployed-runtime-parity-closure.md#L2)

- Story 2.12 sprint-status key is no longer truncated.
  [`sprint-status.yaml:87`](sprint-status.yaml#L87)

- Trackers stay in-review while acceptances remain 0/3.
  [`sprint-status.yaml:103`](sprint-status.yaml#L103)
