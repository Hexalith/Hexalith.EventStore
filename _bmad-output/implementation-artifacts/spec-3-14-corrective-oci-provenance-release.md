---
title: 'Story 3.14 Corrective OCI Provenance Release'
type: 'bugfix'
created: '2026-08-20'
status: 'done'
baseline_commit: 'c21bd749154d701c3b7d68e40d1008d3475e35c4'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Latest release `v3.95.0` still inherits the .NET SDK multi-RID defect that truncates URL labels to `https` and omits source revision. Neither it nor immutable `v3.94.1` is deployment-grade.

**Approach:** Correct EventStore label inputs and the shared Builds publisher/validator, then produce one separately authorized release from the latest exact green `main`. Derive its version at authorization; never hard-code the current `3.96.0` projection.

## Boundaries & Constraints

**Always:** Preserve `v3.94.1`/`v3.95.0` as immutable failed evidence; keep EventStore a thin caller and shared mechanics in Builds; publish exactly 14 manifest packages and one two-platform `eventstore` index; bind retained raw bytes and both child smokes to one canonical version/source/run/Builds/authority lineage.

**Ask First:** Any Git mutation, release dispatch/approval, external write, credential use, or authority creation/reservation/consumption. Spec approval permits only named EventStore and Builds file edits.

**Never:** Rewrite an existing release; trust mutable names, ancestry, labels alone, copied pass flags, or projected `3.96.0`; fabricate authority/receipts; add Dockerfiles, runtime/dependency/inventory changes, deployment, consumer migration, signing, SBOM, or attestation.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Real multi-RID archive | Source SHA and version | Both configs contain identical exact source, release, SHA-pinned docs, revision, and version labels | Any invalid/divergent label fails |
| Candidate selection | Dispatch-reserved stable version plus live release/package/registry destinations | The shared Builds preflight resolves the version floor from every live destination and admits only a reserved version that is absent and newer than all of them, and Semantic Release must independently resolve the identical value | Ambiguity, stale read, or collision blocks |
| Authorized publication | Unexpired one-use GitHub authority for run/attempt and scope | All writes match one reservation, consumed once | Missing, expired, replayed, wrong-role, or mismatch blocks |
| Partial publication | Any write succeeds before a later failure | Preserve the partial version as immutable non-authorizing evidence; tag immutability and one-use authority consumption are what force a retry onto a fresh version and a fresh authority | Retry requires a new version and new authority |
| Complete publication | Packages, OCI bytes, and both smokes pass | Emit canonical evidence for 3.15 without selecting a deployed identity | Environment/product failures remain distinct and blocking |

</frozen-after-approval>

## Code Map

- `Directory.Build.targets:21` -- bind five provenance labels to exact publisher inputs without colon truncation.
- `references/Hexalith.Builds/Github/publish-containers/{publish-containers.sh,oci_registry_validator.py,publication_preflight.py}` -- safely forward labels, validate raw configs, and enforce one-use GitHub authority before writes.
- `references/Hexalith.Builds/Github/publish-containers/tests/` -- real publisher, authority replay/mismatch, and label mutation fixtures.
- `.github/workflows/release.yml:79` -- keep the thin caller and rotate reusable workflow/action identity together to one reviewed corrected Builds SHA.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/{ContainerPublishingGovernanceTests.cs,CorrectiveOciProvenanceReleaseTests.cs}` -- pin the caller and run real archive/evidence cases without changing Story 3.13 fixtures.
- `tools/release-packages.json` and `tools/{release_evidence_codec.py,validate-corrective-release-evidence.py}` -- 14-package authority and canonical identity codec/verifier.
- `docs/ci.md:227` and Story 3.14 artifacts -- document gates, authority lifecycle, evidence, and 3.15 handoff.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs` -- reproduce the real multi-RID `https`/missing-revision archive defect before corrections.
- [x] `Directory.Build.targets` and `references/Hexalith.Builds/Github/publish-containers/` -- correct label transport, raw-config validation, and one-use authority with mutation tests.
- [x] `tools/release_evidence_codec.py`, `tools/validate-corrective-release-evidence.py`, `.github/workflows/release.yml`, governance tests, and `docs/ci.md` -- add canonical evidence, rotate one Builds identity, and document operation.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/` and `references/Hexalith.Builds/Github/publish-containers/tests/` -- run the named package-mode, publisher, archive, package, and smoke checks without writes.
- [x] `_bmad-output/implementation-artifacts/evidence/story-3-14/` and `_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md` -- after separate authority, resolve/publish once and retain all bytes; quarantine any partial identity.

**Acceptance Criteria:**
- Given all matrix scenarios, when focused suites run, then every row executes with mutation-proven, zero-skipped coverage.
- Given an authorized release, when evidence is reverified, then one canonical identity binds repository, version/tag, source, workflow, corrected Builds/helpers, authority, all packages, OCI bytes/labels, and both smokes.
- Given the 3.14 packet, when handed to 3.15, then it selects no deployed identity or mutation authority.

### Review Findings

Code review 2026-08-21 (chunk A+B: release mechanics + governance tests, baseline `c21bd749`..`c8902353`).
Four layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor. No layer failed.

- [ ] [Review][Decision] Release caller is pinned to a Hexalith.Builds commit that does not exist on the remote — `.github/workflows/release.yml:103,110` pin `63409393541f1437e23006b7a4e05174f8b50da7` for both `uses:` and `builds-execution-sha`. `gh api repos/Hexalith/Hexalith.Builds/commits/63409393…` returns 422 and the git-database endpoint returns 404; Builds `main` is `eadddc7b…`. This pin is live on `origin/main`, so the next Release dispatch cannot resolve the reusable workflow — the same startup-failure class as quarantined run `32347773728`. Options: push `63409393` to Hexalith.Builds, or re-pin to a reachable reviewed SHA.
- [ ] [Review][Decision] The corrective release still ships the SDK colon-truncation defect in two labels — both retained v3.96.2 child configs carry `org.opencontainers.image.created` and `org.opencontainers.artifact.created` = `2026-08-20T11`, the same `String.Split(':')[1]` shape as rejected v3.94.1 (`2026-08-14T08`). `Directory.Build.targets` rebinds only five labels, `release_evidence_codec._expected_labels` requires only those five, and `ProvenanceLabelMutationsFailClosed` explicitly asserts extra labels are accepted, so the gate can never fail on it. Decide: re-release with `created` rebound, accept and document, or re-scope.
- [ ] [Review][Decision] The canonical identity binds live repo bytes and was already regenerated once, undisclosed — `validate-corrective-release-evidence.py::_codec_identity()` hashes the on-disk `tools/release_evidence_codec.py` and verifier, so any codec fix invalidates re-verification of the frozen packet. `1e5abd26` bumped `CODEC_VERSION` 1→2 and rewrote `release-identity.json`, moving the digest from `926ccfdf…` (`a55b5bef`) to the published `92b7479b…`; the story presents the latter as the canonical digest without disclosing the regeneration. A re-freeze policy is needed before any codec patch lands.
- [ ] [Review][Decision] Two I/O-matrix rows are covered only by helpers no production path calls — `select_absent_version`, `publication_disposition` and `canonical_sha256` (`tools/release_evidence_codec.py:81,98`) are referenced only from `python3 -c` invocations in the tests. The real candidate version is a free-text `release-version` dispatch input checked by a semver regex plus equality with semantic-release. `select_absent_version` also always bumps patch, which contradicts the reservation gate on any `feat:` release. Decide: wire them into the release path or re-scope the Candidate-selection and Partial-publication rows.
- [ ] [Review][Decision] The protected release job now holds `attestations: write` and `id-token: write` — added to satisfy GitHub's static permission validation of the skipped `governed-release` job. Spec Never excludes "signing, SBOM, or attestation" and epic-3-context.md states deferred attestation gains no implementation authority from this epic. Decide: ratify the widened token scope, or ask Builds to restructure so the legacy path does not require the grants.

- [ ] [Review][Patch] Pin the story-3-14 evidence packet to LF like story-1-21 [.gitattributes:12]
- [ ] [Review][Patch] Anchor the Builds fixture run and the recorded 123/54 counts to the pinned release SHA [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:514]
- [ ] [Review][Patch] Add a required-provenance-label mutation case that runs through the real codec [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:135]
- [ ] [Review][Patch] Read HEXALITH_RELEASE_AUTHORITY_OWNER instead of hardcoding it, and assert both forwarded values [scripts/validate-publication-preflight.sh:18]
- [ ] [Review][Patch] Derive the authority issue number from issue_url instead of hardcoding 346 [tools/release_evidence_codec.py:756]
- [ ] [Review][Patch] Validate provenance input shape, not just non-emptiness, and add a negative publish test [Directory.Build.targets:47]
- [ ] [Review][Patch] Delete or wire up the ~335-line dead evidence-packet fixture [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:529]
- [ ] [Review][Patch] Add executing negative cases for the two new dispatch-input gates [.github/workflows/release.yml:52]
- [ ] [Review][Patch] Correct the story record's development-gitlink claim [_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md:29]
- [ ] [Review][Patch] Scope the repository-role proof to this repository [tools/release_evidence_codec.py:705]
- [ ] [Review][Patch] Restore the two deleted ShouldNotContain negative guards [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:153]
- [ ] [Review][Patch] Scope the release-job regex and fix pipe-drain/timeout in the new process tests [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:377]
- [ ] [Review][Patch] Replace the fabricated staging release URL and document the two new dispatch inputs [docs/brownfield/deployment-guide.md:30]
- [ ] [Review][Patch] Correct the SDK version named in the provenance comment [Directory.Build.targets:19]

- [x] [Review][Defer] Five pre-existing Windows early-return vacuous passes [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:207] — deferred, pre-existing
- [x] [Review][Defer] Codec hygiene cluster: misleading `_nuspec_identity` parameter, `_parse_timestamp` replace-all, children[0]-only index distinctness, fourth copy of the package count, duplicate helper re-hash [tools/release_evidence_codec.py:441] — deferred, pre-existing
- [x] [Review][Defer] `observations.json` is checksummed but never semantically validated [tools/release_evidence_codec.py:865] — deferred, pre-existing
- [x] [Review][Defer] The OCI image index carries no provenance annotations [Directory.Build.targets:57] — deferred, pre-existing
- [x] [Review][Defer] Three divergent JSON canonicalisers are never cross-checked [tools/release_evidence_codec.py:69] — deferred, pre-existing
- [x] [Review][Defer] nuspec parsing has no size or entity-expansion bound [tools/release_evidence_codec.py:466] — deferred, pre-existing
- [x] [Review][Defer] Issue-comment snapshot completeness is unproven under pagination [tools/release_evidence_codec.py:770] — deferred, pre-existing
- [x] [Review][Defer] Authority-window theory is coupled to the frozen timestamps by one second, and seven mutations share one `[Fact]` [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:248] — deferred, pre-existing

Dismissed as noise (4): `record_sha256` re-serialisation (by design; `authority_record_sha256` binds raw bytes); "reservation leaves no evidence trace" (refuted — the authority binds `identity_sha256`, which binds `version`); duplicate `licenses`/`vendor` labels (verified identical values, retained configs correct); split codec import styles (both forms work in their own execution contexts).

### Review Findings

Chunk 1A follow-up code review 2026-08-21 (`c21bd749`..`f8b514f3`, EventStore
release implementation surface). Four layers: blind-hunter, edge-case-hunter,
verification-gap, acceptance-auditor. No layer failed.

- [ ] [Review][Patch] [HIGH] Implement the owner-selected trusted, versioned live-verifier model: dispatch minimally parsed schema/version metadata to immutable backward-compatible handlers, keep retained packet code as non-executable evidence, and pin the v3 handler with the frozen packet's expected digest. Selected 2026-08-21; packet-supplied code must never execute. [tools/validate-corrective-release-evidence.py:10]
- [ ] [Review][Patch] [MEDIUM] Make repository scoping explicit in future role evidence by binding the collaborator-permission request URL or repository alongside the response and validating it. Preserve v3 compatibility: its frozen packet is indirectly scoped because the hash-bound executed preflight helper derives that endpoint from the publication identity already fixed to `Hexalith/Hexalith.EventStore`. [tools/release_evidence_codec.py:651]

- [ ] [Review][Patch] [HIGH] Derive authority and receipt HTML URLs from the accepted `authority.issue_url`; the codec currently hard-codes issue 346 and rejects a valid future authority on any other EventStore issue [tools/release_evidence_codec.py:591]
- [ ] [Review][Patch] [HIGH] Generate one publisher-owned `ContainerProvenanceCreated` value and pass it to every RID, then exercise the production-shaped fallback; the pinned publisher omits the property, inner builds can evaluate different instants, and the only archive test overrides the fallback [Directory.Build.targets:38]
- [ ] [Review][Patch] [HIGH] Validate provenance source SHA, release-version/tag, and created-timestamp shape before container publication instead of checking only for empty strings [Directory.Build.targets:57]
- [ ] [Review][Patch] [HIGH] Reject conflicting smoke-log outcomes and cleanup values instead of accepting one expected pass line alongside additional failure lines [tools/release_evidence_codec.py:801]
- [ ] [Review][Patch] [HIGH] Run shared Builds authority fixtures from the immutable release-workflow SHA rather than the independently moving development gitlink [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:475]
- [ ] [Review][Patch] [MEDIUM] Enforce stable SemVer without leading-zero numeric identifiers consistently in the dispatch gate, wrapper, shared preflight, and evidence codec [.github/workflows/release.yml:52]
- [ ] [Review][Patch] [MEDIUM] Repair the deployment examples so locally produced tags match Compose/Kubernetes/ACR consumers, ACR uses `ContainerRegistry`, and local builds do not claim nonexistent GitHub release URLs [docs/guides/deployment-docker-compose.md:315; docs/guides/deployment-kubernetes.md:263; docs/guides/deployment-azure-container-apps.md:261]
- [ ] [Review][Patch] [MEDIUM] Correct the release-permission documentation: the legacy reusable job inherits the caller's `attestations: write` and `id-token: write` grants; it does not narrow them [docs/ci.md:247]
- [ ] [Review][Patch] [MEDIUM] Delete or execute the uncalled, stale synthetic evidence-packet builder and its orphaned record [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:490]
- [ ] [Review][Patch] [MEDIUM] Require both platform smoke entries to bind the same two-platform summary file and digest, preventing two individually self-consistent but mutually divergent summaries [tools/release_evidence_codec.py:902]
- [ ] [Review][Patch] [MEDIUM] Bound the real multi-RID publish and helper subprocesses with timeouts and process-tree cleanup so the focused suite cannot hang indefinitely [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:53]
- [ ] [Review][Patch] [MEDIUM] Add executing negative publishes for missing source SHA and release version so removal or detachment of `ValidateContainerProvenanceInputs` is mutation-detectable [Directory.Build.targets:57]
- [ ] [Review][Patch] [MEDIUM] Add canonical packet mutations that independently set `selects_deployed_identity` and `grants_mutation_authority` to true [tools/release_evidence_codec.py:396]
- [ ] [Review][Patch] [MEDIUM] Execute negative cases for invalid `release-version` and `release-authority-issue-url` dispatch inputs instead of testing only valid values and later source/reservation failures [.github/workflows/release.yml:52]

Dismissed as noise or already dispositioned (12): the release pin is now reachable on the
Hexalith.Builds remote branch; widened token scopes were owner-ratified and are already tracked;
v3.96.2's malformed created timestamps were explicitly accepted and disclosed; live-observation
semantics and issue-comment pagination are already in the deferred ledger; malformed-input
traceback, non-finite JSON, alternate timestamp syntax, CRLF manifest, OCI-layer, duplicate-tar,
and archive-memory suggestions either remain fail-closed or fall outside the accepted Story 3.14
contract without a demonstrated consumer failure.

## Spec Change Log

- 2026-08-21 -- Owner-ratified amendment of two `I/O & Edge-Case Matrix` rows during code review.
  The `Candidate selection` and `Partial publication` rows described behavior that existed only in
  `tools/release_evidence_codec.py::select_absent_version` and `::publication_disposition`, which no
  production path ever called; the tests covering those rows therefore proved decision rules nothing
  consulted. The rows now describe the mechanism that actually runs -- the shared Builds preflight
  version floor checked against the dispatch reservation and Semantic Release, and tag immutability
  plus one-use authority consumption -- and the two uncalled helpers and their tests were removed.
  `select_absent_version` was additionally wrong for any non-patch release: it returned
  `max(observed).patch + 1` unconditionally, and it rejected the whole candidate floor if any
  observed tag was not stable semver, which the repository's own `staging-latest` container tag is.

## Design Notes

Resolve from authorized green `main`; source/destination change invalidates the `3.96.0` projection. Bind authenticated GitHub authority to run/attempt, helper hashes, scope, expiry, and one-use consumption.

## Verification

**Commands:**
- `pwsh -NoProfile -File references/Hexalith.Builds/Tools/test-publish-containers.ps1` -- expected: all shared fixtures pass.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: 0 warnings/errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.CorrectiveOciProvenanceReleaseTests` -- expected: all matrix cases pass, none skipped.
- `python3 tools/pack-release-packages.py /tmp/eventstore-3-14-packages 0.0.0-ci-test && python3 tools/validate-release-packages.py /tmp/eventstore-3-14-packages 0.0.0-ci-test` -- expected: exactly 14 valid packages.
- `bash -n scripts/validate-publication-preflight.sh && bash -n references/Hexalith.Builds/Github/publish-containers/publish-containers.sh && git diff --check` -- expected: syntax and hygiene pass in both owning repositories.

## Suggested Review Order

**Canonical evidence boundary**

- Start with the exact identity, authority, package, smoke, and packet invariants.
  [`release_evidence_codec.py:207`](../../tools/release_evidence_codec.py#L207)

- The CLI independently validates the authoritative 14-package manifest and codec bytes.
  [`validate-corrective-release-evidence.py:19`](../../tools/validate-corrective-release-evidence.py#L19)

- The completed packet freezes the successful v3.96.2 lineage and cycle-free checksum binding.
  [`release-identity.json:1`](evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/release-identity.json#L1)

- Exact release assets, public NuGet inventories, and release-environment execution remain observable.
  [`observations.json:1`](evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/observations.json#L1)

**Mutation-proof governance**

- Real packet mutations prove workflow, package, authority, receipt, smoke, and checksum rejection.
  [`CorrectiveOciProvenanceReleaseTests.cs:248`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs#L248)

- Reservation mismatch is rejected before shared preflight or any later publication marker.
  [`ContainerPublishingGovernanceTests.cs:286`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs#L286)

**Operator handoff**

- The story record explains retained raw evidence and the final canonical digest.
  [`3-14-corrective-oci-provenance-release.md:1`](3-14-corrective-oci-provenance-release.md#L1)

- Deployment examples now pass mandatory source and release provenance explicitly.
  [`deployment-guide.md:22`](../../docs/brownfield/deployment-guide.md#L22)

### Review Findings

Chunk 2 code review 2026-08-21 (`f8b514f3`..`94591f35`, Story 3.14 release surface only;
concurrent Story 3.13 files in the same range were excluded from scope). Four layers:
blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor. No layer failed.

Independently re-verified green before triage: Contracts Release/package-mode build 0W/0E;
focused `CorrectiveOciProvenanceReleaseTests` + `ContainerPublishingGovernanceTests` 52 passed /
0 failed / 0 skipped; `validate-corrective-release-evidence.py` on the checked-in packet passes at
`4d1a0c33…`; Builds pin `a07078ad` is reachable on `origin/main` and is an ancestor of the current
gitlink, which closes the chunk-A+B blocking Decision about the unpublished `63409393` pin.

- [ ] [Review][Decision] The frozen `I/O & Edge-Case Matrix` still mandates the reservation gate the caller now disables — `release.yml` deletes both `workflow_dispatch` inputs and declares `require-publication-authority: false`, but the `<frozen-after-approval>` rows `Candidate selection` ("Dispatch-reserved stable version… absent and newer") and `Authorized publication` ("Unexpired one-use GitHub authority… consumed once") are unchanged and the Spec Change Log has no entry for the removal. The 2026-08-21 amendment re-worded those rows specifically to "describe the mechanism that actually runs"; this change makes them wrong in the opposite direction. Amending frozen intent needs the owner. Options: amend both rows plus a Change Log entry to describe protected-environment approval as the operative gate with the reservation gate opt-in, or restore the reservation posture.
- [ ] [Review][Decision] No EventStore file pins who may authorize a corrective publication any more — `scripts/validate-publication-preflight.sh:18` replaced `readonly authority_owner="github:jpiquot"` with a shape-only regex, the caller dropped `release-authority-owner`, and `ContainerPublishingGovernanceTests.cs:517` now asserts its absence. The pinned shared preflight derives the expected login from whatever it receives, so with the gate re-enabled any well-formed login is forwarded and its authority comment consumed; the shared preflight's collaborator-permission check is the only remaining constraint. `v3.py` still hardcodes `github:jpiquot` for evidence validation, giving three postures for one identity. Note the shape-only read was itself a chunk-A+B review request. Options: re-pin the owner at the caller (`release-authority-owner: github:jpiquot` whenever the gate is true, asserted by the governance test), re-pin in the wrapper, or ratify shape-only and correct `docs/ci.md`.

- [ ] [Review][Patch] [HIGH] The new "dispatch takes no inputs" assertion is vacuous — `^  workflow_dispatch:\s*$` under `RegexOptions.Multiline` matches the old input-carrying form too, because `\s*` backtracks to empty and `$` matches before the newline (verified against both shapes). Assert the block has no `inputs:` child instead [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:519]
- [ ] [Review][Patch] [HIGH] The live handler's own bytes are pinned by nothing and its header comment claims the opposite — neutering `validate_packet_files` to `return None` still yields `pass` with the identical `4d1a0c33…` digest (verified). `EXPECTED_PACKET_CODEC_SHA256` pins the *retained* codec, never the executing one. Correct the comment and add a test binding `v3.py`'s own SHA-256 so an unreviewed edit fails closed [tools/release_evidence_handlers/v3.py:17]
- [ ] [Review][Patch] [HIGH] The story record names the superseded pin and repeats a claim that is now false — it states the caller "rotates … to `63409393…`" and that this SHA "is not yet published to the Hexalith.Builds remote". The shipped caller pins `a07078ad…` and both SHAs resolve on the remote [_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md:36]
- [ ] [Review][Patch] [HIGH] The three provenance URL properties became environment-overridable and are still unvalidated — adding `Condition="'$(X)' == ''"` does not enable the `-p:` overrides the docs now use (MSBuild global properties already won; verified with a minimal project), it newly lets an environment variable of the same name redirect `image.source`/`.url`/`.documentation`. Validate all three in `ValidateContainerProvenanceInputs`, which also covers `-p:` abuse and closes the exact truncated-`https` class this story exists to reject [Directory.Build.targets:39]
- [ ] [Review][Patch] [MEDIUM] The `ContainerImageTag` equality gate is bypassed by the plural `ContainerImageTags` the file's own header advertises — the guard's `'$(ContainerImageTag)' != ''` precondition is false on that path, so an image can be tagged `latest` while carrying `org.opencontainers.image.version=3.96.2` [Directory.Build.targets:64]
- [ ] [Review][Patch] [MEDIUM] The repo's own `staging-latest` container-tag default is now a guaranteed publish error, and `docs/brownfield/deployment-guide.md:13` still documents it as the default. No test publishes without an explicit tag, so nothing records the contradiction [Directory.Build.targets:17]
- [ ] [Review][Patch] [MEDIUM] `ContainerProvenanceCreated` remains the one gated input production never supplies — the pinned publisher passes only source SHA and version, every test site passes `-p:ContainerProvenanceCreated` explicitly, and the new check validates shape per invocation rather than requiring one publisher-owned instant shared by both children. The chunk-1A HIGH finding is not closed by a shape check [Directory.Build.targets:38]
- [ ] [Review][Patch] [MEDIUM] The story record claims the `created` fix landed and that "the archive test now asserts the complete label surface" — the publisher still never supplies the value and the archive tests still override it [_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md:110]
- [ ] [Review][Patch] [MEDIUM] Story record verification counts are stale — the focused suite is 52 cases, not 31, and the 123/54 Builds counts were recorded against `eadddc7b` while the fixture test now archives and runs `a07078ad` [_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md:138]
- [ ] [Review][Patch] [MEDIUM] `docs/ci.md:215` states the publication pin in unguarded prose while the same test method binds the sibling checklist to `ApprovedBuildsReleaseSha` with a comment about that exact drift class. Add `ci.ShouldContain(ApprovedBuildsReleaseSha);` beside it [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:719]
- [ ] [Review][Patch] [MEDIUM] `docs/ci.md` still describes the authority gate as unconditional — `publishCmd` runs "only after the authority gate", and `:190` presents the `v<reserved-version>` self-tag rule as always active. Its re-enable instructions also omit that the two deleted `workflow_dispatch` inputs and the governance `ShouldNotContain` assertions must be restored first, so the documented path currently turns the suite red [docs/ci.md:190]
- [ ] [Review][Patch] [MEDIUM] `docs/ci.md` claims authority comes "from the pinned `github:jpiquot` release-owner identity", which the wrapper no longer pins (see Decision 2) [docs/ci.md:225]
- [ ] [Review][Patch] [MEDIUM] The Compose and Kubernetes local-build recipes still publish to `registry.hexalith.com` because `ContainerRegistry` defaults there, so the `docker tag`, `minikube image load`, `kind load docker-image` and `COMMANDAPI_IMAGE=hexalith-eventstore:0.0.0-local.1` lines all name an image the build never produces locally. Only the ACA guide received an explicit `-p:ContainerRegistry=` [docs/guides/deployment-docker-compose.md:335; docs/guides/deployment-kubernetes.md:264]
- [ ] [Review][Patch] [MEDIUM] The new repository-scoped role-evidence envelope has no producer and no test — the only checked-in packet takes the legacy `else` branch, so deleting the `request_url` scoping check leaves the whole suite green. That check was the chunk-1A repository-scoping fix [tools/release_evidence_handlers/v3.py:679]
- [ ] [Review][Patch] [MEDIUM] Spec bookkeeping is out of step with the tree — all 15 chunk-1A `[Review][Patch]` items are still unchecked although roughly twelve shipped in this range; frontmatter reads `status: 'done'` / `review_loop_iteration: 0` while `sprint-status.yaml:226` reads `review`; and the Code Map plus six open items still anchor to `tools/release_evidence_codec.py`, now a 12-line facade [_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md:48]
- [ ] [Review][Patch] [MEDIUM] An unhashable dispatch value raises an uncaught `TypeError` traceback instead of the `[corrective-release-evidence] fail:` line, because `key not in HANDLERS` sits outside the `try` (verified with `"version": [1]`). Anything grepping that prefix sees nothing [tools/validate-corrective-release-evidence.py:40]
- [ ] [Review][Patch] [LOW] The retained-codec digest is a bare literal in two files with no cross-check, and `v3.py`'s own copy of the pin can never fire because the dispatcher already gated the identical tuple [tools/validate-corrective-release-evidence.py:13]
- [ ] [Review][Patch] [LOW] `repository_issue_html_url` validates more weakly than the regex it replaces — `.../issues/007` and the Arabic-Indic `.../issues/٣` are accepted, and `.../issues/²` escapes as a bare `ValueError` rather than `EvidenceError` (all three verified). Use `[1-9][0-9]*` [tools/release_evidence_handlers/v3.py:477]
- [ ] [Review][Patch] [LOW] The single-shared-smoke-summary rule is enforced twice and the second copy is unreachable — `validate_identity` always runs first on the CLI path, so the `validate_packet_files` guard is green by construction and the split-summary test only ever reaches the first message [tools/release_evidence_handlers/v3.py:948]
- [ ] [Review][Patch] [LOW] `RunWrapperWithPosture` never clears ambient `HEXALITH_RELEASE_RESERVED_VERSION` / `_AUTHORITY_ISSUE_URL` / `_AUTHORITY_OWNER`, so the disabled- and unset-posture cases are environment-dependent on a developer machine [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:832]
- [ ] [Review][Patch] [LOW] The Builds fixture test fails with a raw `git archive` error whenever the release pin is absent from the local submodule object store — precisely the case the deliberately-independent pin design creates. Fetch the pin or fail with a pin-specific diagnostic [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:711]
- [ ] [Review][Patch] [LOW] `UnsupportedSyntheticPacketCannotSelectTrustedLiveHandler` proves malformed-shape rejection, not the unsupported-version path its name promises, and still hashes the now-12-line facade into a field nothing reads [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:675]
- [ ] [Review][Patch] [LOW] Naming and cross-reference drift — `ReleaseCallerPinsSharedExecutionAndOneMappingWithGitHubAuthority` now asserts the absence of every GitHub-authority input, and the `release.yml` permissions comment cites a `deferred-work.md` phrase ("reusable release workflow split") that does not appear in the ledger [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:490]
- [ ] [Review][Patch] [LOW] Deployment-guide hygiene — the ACA guide's two `docker push` lines are dead now that `-p:ContainerRegistry` makes `dotnet publish` push directly; `0.0.0-staging.${SOURCE_SHA:0:12}` yields a leading-zero numeric pre-release identifier the new SemVer gate rejects whenever those twelve hex characters are all digits starting with `0`; and the Kubernetes guide's push and local-cluster blocks depend on `$RELEASE_VERSION` defined in a non-adjacent block [docs/guides/deployment-azure-container-apps.md:266]
- [ ] [Review][Patch] [LOW] `RunProcess` and `RunProcessAsync` throw a `TimeoutException` carrying only the file name and budget, discarding the stdout/stderr they already captured — a CI timeout in the multi-minute publish cases would be undiagnosable [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:53]

- [x] [Review][Defer] Two heavyweight publish theories run untraited in the CI-gating Contracts lane (2 × `dotnet publish` at an 8-minute budget, 4 × `dotnet msbuild`) [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:53] — deferred, pre-existing
- [x] [Review][Defer] The dispatch table has no `v4` handler and no documented procedure for adding one; `EXPECTED_PACKET_CODEC_SHA256` plus `CODEC_VERSION` make v3 a single-packet allowlist, and the legacy role-evidence pin `830af8af…` is the `eadddc7b` helper, which today's `a07078ad` pin no longer matches [tools/release_evidence_handlers/v3.py:15] — deferred, pre-existing
- [x] [Review][Defer] `deferred-work.md` still records `builds-execution-sha: cf04c419…` as the current pin [_bmad-output/implementation-artifacts/deferred-work.md:14] — deferred, pre-existing

Dismissed as noise or refuted (5): the `references/Hexalith.Builds` gitlink move and its Roslynator
4.16.0→4.16.1 bump arrived in a separate `build(deps)` commit and the story record already directs
readers to `git ls-tree` for it; the Story 3.13 prose added to `docs/ci.md` belongs to a concurrently
live story sharing that file; `_load_handler`'s uncaught `ImportError` is unreachable because the
verifier always executes as a script from `tools/`; "non-UTF-8 evidence crashes with a raw decode
message" is **refuted** — it prints the ordinary `fail: release identity dispatch metadata is
invalid` and exits 1 (verified); and "the facade's star-import breaks consumers importing private
helpers" is **refuted** — every consumer imports public names only (`EvidenceError`,
`validate_release_manifest`, `repository_issue_html_url`, `canonical_bytes`, `load_json_bytes`).
