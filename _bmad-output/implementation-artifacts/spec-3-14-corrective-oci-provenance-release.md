---
title: 'Story 3.14 Corrective OCI Provenance Release'
type: 'bugfix'
created: '2026-08-20'
status: 'in-progress'
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
| Candidate selection | Protected `production` environment reviewer approval, live release/package/registry destinations, and (opt-in, `require-publication-authority: true`) a dispatch-reserved stable version | The shared Builds preflight resolves the version floor from every live destination and admits only a candidate that is absent and newer than all of them, and Semantic Release must independently resolve the identical value; when the opt-in gate is enabled, that value must additionally equal the reservation | Ambiguity, stale read, or collision blocks; with the gate enabled, a reservation mismatch also blocks |
| Authorized publication | Protected `production` environment reviewer approval; when the opt-in gate is enabled, also an unexpired one-use GitHub authority for run/attempt and scope, pinned to `github:jpiquot` | Environment approval authorizes an ordinary publication; when the gate is enabled, all writes must additionally match one reservation, consumed once | Missing approval blocks; with the gate enabled, a missing, expired, replayed, wrong-role, wrong-owner, or mismatched authority also blocks |
| Partial publication | Any write succeeds before a later failure | Preserve the partial version as immutable non-authorizing evidence; tag immutability and one-use authority consumption are what force a retry onto a fresh version and a fresh authority | Retry requires a new version and new authority |
| Complete publication | Packages, OCI bytes, and both smokes pass | Emit canonical evidence for 3.15 without selecting a deployed identity | Environment/product failures remain distinct and blocking |

</frozen-after-approval>

## Code Map

- `Directory.Build.targets:21` -- bind five provenance labels to exact publisher inputs without colon truncation.
- `references/Hexalith.Builds/Github/publish-containers/{publish-containers.sh,oci_registry_validator.py,publication_preflight.py}` -- safely forward labels, validate raw configs, and enforce one-use GitHub authority before writes.
- `references/Hexalith.Builds/Github/publish-containers/tests/` -- real publisher, authority replay/mismatch, and label mutation fixtures.
- `.github/workflows/release.yml:79` -- keep the thin caller and rotate reusable workflow/action identity together to one reviewed corrected Builds SHA.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/{ContainerPublishingGovernanceTests.cs,CorrectiveOciProvenanceReleaseTests.cs}` -- pin the caller and run real archive/evidence cases without changing Story 3.13 fixtures.
- `tools/release-packages.json`, `tools/release_evidence_codec.py`, `tools/validate-corrective-release-evidence.py`, and `tools/release_evidence_handlers/v3.py` -- 14-package authority, compatibility facade, trusted dispatcher, and pinned v3 packet handler.
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

### Review Findings — chunk A+B (2026-08-21)

Code review 2026-08-21 (chunk A+B: release mechanics + governance tests, baseline `c21bd749`..`c8902353`).
Four layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor. No layer failed.

- [x] [Review][Decision] Release caller is pinned to a Hexalith.Builds commit that does not exist on the remote — `.github/workflows/release.yml:103,110` pin `63409393541f1437e23006b7a4e05174f8b50da7` for both `uses:` and `builds-execution-sha`. `gh api repos/Hexalith/Hexalith.Builds/commits/63409393…` returns 422 and the git-database endpoint returns 404; Builds `main` is `eadddc7b…`. This pin is live on `origin/main`, so the next Release dispatch cannot resolve the reusable workflow — the same startup-failure class as quarantined run `32347773728`. Resolved 2026-08-21: the caller was re-pinned to reviewed SHA `a07078ad74d3727bc5a6b6d85d47d56a6e5c9fec`, which is reachable on Hexalith.Builds `origin/main`; `63409393…` later also reached the remote but remains superseded.
- [x] [Review][Decision] The corrective release still ships the SDK colon-truncation defect in two labels — both retained v3.96.2 child configs carry `org.opencontainers.image.created` and `org.opencontainers.artifact.created` = `2026-08-20T11`, the same `String.Split(':')[1]` shape as rejected v3.94.1 (`2026-08-14T08`). `Directory.Build.targets` rebinds only five labels, `release_evidence_codec._expected_labels` requires only those five, and `ProvenanceLabelMutationsFailClosed` explicitly asserts extra labels are accepted, so the gate can never fail on it. Resolved 2026-08-21: the owner accepted and disclosed the timestamp-only defect without reissuing immutable v3.96.2; post-release hardening now rebinds and validates both created labels and the Builds development source supplies one publisher-owned instant across all RID publishes.
- [x] [Review][Decision] The canonical identity binds live repo bytes and was already regenerated once, undisclosed — `validate-corrective-release-evidence.py::_codec_identity()` hashes the on-disk `tools/release_evidence_codec.py` and verifier, so any codec fix invalidates re-verification of the frozen packet. `1e5abd26` bumped `CODEC_VERSION` 1→2 and rewrote `release-identity.json`, moving the digest from `926ccfdf…` (`a55b5bef`) to the published `92b7479b…`. Resolved 2026-08-21: the story record now discloses every identity regeneration and digest, and the owner selected a trusted versioned live-handler model that keeps retained packet code non-executable while pinning both the handler bytes and the frozen packet's expected codec digest.
- [x] [Review][Decision] Two I/O-matrix rows are covered only by helpers no production path calls — `select_absent_version`, `publication_disposition` and `canonical_sha256` (`tools/release_evidence_codec.py:81,98`) are referenced only from `python3 -c` invocations in the tests. The real candidate version is a free-text `release-version` dispatch input checked by a semver regex plus equality with semantic-release. `select_absent_version` also always bumps patch, which contradicts the reservation gate on any `feat:` release. Resolved 2026-08-21: the owner re-scoped the matrix to the production Builds preflight, Semantic Release equality, tag immutability, and one-use authority behavior, then removed the uncalled helpers and their tests; the later opt-in-authority wording gap in `Partial publication` remains tracked separately below.
- [x] [Review][Decision] The protected release job now holds `attestations: write` and `id-token: write` — added to satisfy GitHub's static permission validation of the skipped `governed-release` job. Spec Never excludes "signing, SBOM, or attestation" and epic-3-context.md states deferred attestation gains no implementation authority from this epic. Resolved 2026-08-21: the owner ratified the widened permission ceiling required by the statically referenced shared workflow; Story 3.14 still performs no signing, SBOM, or attestation operation.

- [x] [Review][Patch] Pin the story-3-14 evidence packet to LF like story-1-21 [.gitattributes:12]
- [x] [Review][Patch] Anchor the Builds fixture run and the recorded 123/54 counts to the pinned release SHA [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:514]
- [x] [Review][Patch] Add a required-provenance-label mutation case that runs through the real codec [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:135]
- [x] [Review][Patch] Read HEXALITH_RELEASE_AUTHORITY_OWNER instead of hardcoding it, and assert both forwarded values [scripts/validate-publication-preflight.sh:18]
- [x] [Review][Patch] Derive the authority issue number from issue_url instead of hardcoding 346 [tools/release_evidence_codec.py:756]
- [x] [Review][Patch] Validate provenance input shape, not just non-emptiness, and add a negative publish test [Directory.Build.targets:47]
- [x] [Review][Patch] Delete or wire up the ~335-line dead evidence-packet fixture [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:529]
- [x] [Review][Patch] Add executing negative cases for the two new dispatch-input gates [.github/workflows/release.yml:52]
- [x] [Review][Patch] Correct the story record's development-gitlink claim [_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md:29]
- [x] [Review][Patch] Scope the repository-role proof to this repository [tools/release_evidence_codec.py:705]
- [x] [Review][Patch] Restore the two deleted ShouldNotContain negative guards [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:153]
- [x] [Review][Patch] Scope the release-job regex and fix pipe-drain/timeout in the new process tests [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:377]
- [x] [Review][Patch] Replace the fabricated staging release URL and document the two new dispatch inputs [docs/brownfield/deployment-guide.md:30]
- [x] [Review][Patch] Correct the SDK version named in the provenance comment [Directory.Build.targets:19]

- [x] [Review][Defer] Five pre-existing Windows early-return vacuous passes [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:207] — deferred, pre-existing
- [x] [Review][Defer] Codec hygiene cluster: misleading `_nuspec_identity` parameter, `_parse_timestamp` replace-all, children[0]-only index distinctness, fourth copy of the package count, duplicate helper re-hash [tools/release_evidence_codec.py:441] — deferred, pre-existing
- [x] [Review][Defer] `observations.json` is checksummed but never semantically validated [tools/release_evidence_codec.py:865] — deferred, pre-existing
- [x] [Review][Defer] The OCI image index carries no provenance annotations [Directory.Build.targets:57] — deferred, pre-existing
- [x] [Review][Defer] Three divergent JSON canonicalisers are never cross-checked [tools/release_evidence_codec.py:69] — deferred, pre-existing
- [x] [Review][Defer] nuspec parsing has no size or entity-expansion bound [tools/release_evidence_codec.py:466] — deferred, pre-existing
- [x] [Review][Defer] Issue-comment snapshot completeness is unproven under pagination [tools/release_evidence_codec.py:770] — deferred, pre-existing
- [x] [Review][Defer] Authority-window theory is coupled to the frozen timestamps by one second, and seven mutations share one `[Fact]` [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:248] — deferred, pre-existing

Dismissed as noise (4): `record_sha256` re-serialisation (by design; `authority_record_sha256` binds raw bytes); "reservation leaves no evidence trace" (refuted — the authority binds `identity_sha256`, which binds `version`); duplicate `licenses`/`vendor` labels (verified identical values, retained configs correct); split codec import styles (both forms work in their own execution contexts).

### Review Findings — chunk 1A follow-up (2026-08-21)

Chunk 1A follow-up code review 2026-08-21 (`c21bd749`..`f8b514f3`, EventStore
release implementation surface). Four layers: blind-hunter, edge-case-hunter,
verification-gap, acceptance-auditor. No layer failed.

- [x] [Review][Patch] [HIGH] Implement the owner-selected trusted, versioned live-verifier model: dispatch minimally parsed schema/version metadata to immutable backward-compatible handlers, keep retained packet code as non-executable evidence, and pin the v3 handler with the frozen packet's expected digest. Selected 2026-08-21; packet-supplied code must never execute. [tools/validate-corrective-release-evidence.py:10]
- [x] [Review][Patch] [MEDIUM] Make repository scoping explicit in future role evidence by binding the collaborator-permission request URL or repository alongside the response and validating it. Preserve v3 compatibility: its frozen packet is indirectly scoped because the hash-bound executed preflight helper derives that endpoint from the publication identity already fixed to `Hexalith/Hexalith.EventStore`. [tools/release_evidence_codec.py:651]

- [x] [Review][Patch] [HIGH] Derive authority and receipt HTML URLs from the accepted `authority.issue_url`; the codec currently hard-codes issue 346 and rejects a valid future authority on any other EventStore issue [tools/release_evidence_codec.py:591]
- [x] [Review][Patch] [HIGH] Generate one publisher-owned `ContainerProvenanceCreated` value and pass it to every RID, then exercise the production-shaped fallback; the pinned publisher omits the property, inner builds can evaluate different instants, and the only archive test overrides the fallback [Directory.Build.targets:38]
- [x] [Review][Patch] [HIGH] Validate provenance source SHA, release-version/tag, and created-timestamp shape before container publication instead of checking only for empty strings [Directory.Build.targets:57]
- [x] [Review][Patch] [HIGH] Reject conflicting smoke-log outcomes and cleanup values instead of accepting one expected pass line alongside additional failure lines [tools/release_evidence_codec.py:801]
- [x] [Review][Patch] [HIGH] Run shared Builds authority fixtures from the immutable release-workflow SHA rather than the independently moving development gitlink [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:475]
- [x] [Review][Patch] [MEDIUM] Enforce stable SemVer without leading-zero numeric identifiers consistently in the dispatch gate, wrapper, shared preflight, and evidence codec [.github/workflows/release.yml:52]
- [x] [Review][Patch] [MEDIUM] Repair the deployment examples so locally produced tags match Compose/Kubernetes/ACR consumers, ACR uses `ContainerRegistry`, and local builds do not claim nonexistent GitHub release URLs [docs/guides/deployment-docker-compose.md:315; docs/guides/deployment-kubernetes.md:263; docs/guides/deployment-azure-container-apps.md:261]
- [x] [Review][Patch] [MEDIUM] Correct the release-permission documentation: the legacy reusable job inherits the caller's `attestations: write` and `id-token: write` grants; it does not narrow them [docs/ci.md:247]
- [x] [Review][Patch] [MEDIUM] Delete or execute the uncalled, stale synthetic evidence-packet builder and its orphaned record [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:490]
- [x] [Review][Patch] [MEDIUM] Require both platform smoke entries to bind the same two-platform summary file and digest, preventing two individually self-consistent but mutually divergent summaries [tools/release_evidence_codec.py:902]
- [x] [Review][Patch] [MEDIUM] Bound the real multi-RID publish and helper subprocesses with timeouts and process-tree cleanup so the focused suite cannot hang indefinitely [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:53]
- [x] [Review][Patch] [MEDIUM] Add executing negative publishes for missing source SHA and release version so removal or detachment of `ValidateContainerProvenanceInputs` is mutation-detectable [Directory.Build.targets:57]
- [x] [Review][Patch] [MEDIUM] Add canonical packet mutations that independently set `selects_deployed_identity` and `grants_mutation_authority` to true [tools/release_evidence_codec.py:396]
- [x] [Review][Patch] [MEDIUM] Execute negative cases for invalid `release-version` and `release-authority-issue-url` dispatch inputs instead of testing only valid values and later source/reservation failures [.github/workflows/release.yml:52]

Dismissed as noise or already dispositioned (12): the release pin is now reachable on the
Hexalith.Builds remote branch; widened token scopes were owner-ratified and are already tracked;
v3.96.2's malformed created timestamps were explicitly accepted and disclosed; live-observation
semantics and issue-comment pagination are already in the deferred ledger; malformed-input
traceback, non-finite JSON, alternate timestamp syntax, CRLF manifest, OCI-layer, duplicate-tar,
and archive-memory suggestions either remain fail-closed or fall outside the accepted Story 3.14
contract without a demonstrated consumer failure.

## Spec Change Log

- 2026-08-22 -- Owner-ratified amendment of the `Candidate selection` and `Authorized publication`
  rows to resolve the chunk-2 review Decision that the frozen matrix still mandated the reservation
  gate as mandatory while `release.yml` had already disabled it. Reflects the 2026-08-21 owner call
  (recorded separately) that the two dispatch-reservation inputs were over-engineered for an ordinary
  publication and that protected-`production`-environment reviewer approval is the operative gate;
  the dispatch-reserved-version and one-use-authority mechanism remains fully implemented and tested
  but is opt-in via `require-publication-authority` (Builds default `true`, EventStore declares
  `false`). Also resolves the paired Decision on authority-owner pinning: the rows now state the
  gate binds to `github:jpiquot` whenever enabled, enforced by
  `ReleaseAuthorityOwnerIsPinnedWheneverTheAuthorityGateIsEnabled` in
  `ContainerPublishingGovernanceTests.cs`, which fails closed if a future edit re-enables the gate
  without re-pinning the owner.
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

### Review Findings — chunk 2 (2026-08-21)

Chunk 2 code review 2026-08-21 (`f8b514f3`..`94591f35`, Story 3.14 release surface only;
concurrent Story 3.13 files in the same range were excluded from scope). Four layers:
blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor. No layer failed.

Independently re-verified green before triage: Contracts Release/package-mode build 0W/0E;
focused `CorrectiveOciProvenanceReleaseTests` + `ContainerPublishingGovernanceTests` 52 passed /
0 failed / 0 skipped; `validate-corrective-release-evidence.py` on the checked-in packet passes at
`4d1a0c33…`; Builds pin `a07078ad` is reachable on `origin/main` and is an ancestor of the current
gitlink, which closes the chunk-A+B blocking Decision about the unpublished `63409393` pin.

- [x] [Review][Decision] The frozen `I/O & Edge-Case Matrix` still mandates the reservation gate the caller now disables — resolved 2026-08-22: amended both rows plus a Spec Change Log entry to describe protected-environment approval as the operative gate with the reservation/authority mechanism opt-in (matches the 2026-08-21 owner call that made the two dispatch inputs optional).
- [x] [Review][Decision] No EventStore file pins who may authorize a corrective publication any more — resolved 2026-08-22: added `ReleaseAuthorityOwnerIsPinnedWheneverTheAuthorityGateIsEnabled`, which fails closed if `require-publication-authority: true` is ever set in `release.yml` without also setting `release-authority-owner: github:jpiquot`; `docs/ci.md` corrected to describe the caller (not the shared wrapper) as the pinning point.

- [x] [Review][Patch] [HIGH] The new "dispatch takes no inputs" assertion is vacuous — fixed 2026-08-22: replaced the `\s*$` regex with `ExtractYamlBlock` over the `workflow_dispatch:` block asserted to not contain `inputs:` [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:519]
- [x] [Review][Patch] [HIGH] The live handler's own bytes are pinned by nothing and its header comment claims the opposite — fixed 2026-08-22: corrected the comment, added `HANDLER_FILE_SHA256` self-hash pinning to `_load_handler` in `validate-corrective-release-evidence.py`, and added `TamperedLiveHandlerBytesCannotExecuteEvenWithAValidPacket` proving a tampered `v3.py` fails closed against the checked-in valid packet [tools/release_evidence_handlers/v3.py:17]
- [x] [Review][Patch] [HIGH] The story record names the superseded pin and repeats a claim that is now false — fixed 2026-08-22: corrected to state the caller pins `a07078ad…`, which superseded the never-published `63409393…` [_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md:36]
- [x] [Review][Patch] [HIGH] The three provenance URL properties became environment-overridable and are still unvalidated — fixed 2026-08-22: added well-formed-https-URL validation for all three to `ValidateContainerProvenanceInputs`, plus three negative `ContainerPublicationRejectsMalformedProvenanceInputs` cases [Directory.Build.targets:39]
- [x] [Review][Patch] [MEDIUM] The `ContainerImageTag` equality gate is bypassed by the plural `ContainerImageTags` the file's own header advertises — the guard's `'$(ContainerImageTag)' != ''` precondition is false on that path, so an image can be tagged `latest` while carrying `org.opencontainers.image.version=3.96.2` [Directory.Build.targets:64]
- [x] [Review][Patch] [MEDIUM] The repo's own `staging-latest` container-tag default is now a guaranteed publish error, and `docs/brownfield/deployment-guide.md:13` still documents it as the default. No test publishes without an explicit tag, so nothing records the contradiction [Directory.Build.targets:17]
- [x] [Review][Patch] [MEDIUM] `ContainerProvenanceCreated` remains the one gated input production never supplies — the pinned publisher passes only source SHA and version, every test site passes `-p:ContainerProvenanceCreated` explicitly, and the new check validates shape per invocation rather than requiring one publisher-owned instant shared by both children. The chunk-1A HIGH finding is not closed by a shape check [Directory.Build.targets:38]
- [x] [Review][Patch] [MEDIUM] The story record claims the `created` fix landed and that "the archive test now asserts the complete label surface" — the publisher still never supplies the value and the archive tests still override it [_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md:110]
- [x] [Review][Patch] [MEDIUM] Story record verification counts are stale — the focused suite is 52 cases, not 31, and the 123/54 Builds counts were recorded against `eadddc7b` while the fixture test now archives and runs `a07078ad` [_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md:138]
- [x] [Review][Patch] [MEDIUM] `docs/ci.md:215` states the publication pin in unguarded prose while the same test method binds the sibling checklist to `ApprovedBuildsReleaseSha` with a comment about that exact drift class. Add `ci.ShouldContain(ApprovedBuildsReleaseSha);` beside it [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:719]
- [x] [Review][Patch] [MEDIUM] `docs/ci.md` still describes the authority gate as unconditional — `publishCmd` runs "only after the authority gate", and `:190` presents the `v<reserved-version>` self-tag rule as always active. Its re-enable instructions also omit that the two deleted `workflow_dispatch` inputs and the governance `ShouldNotContain` assertions must be restored first, so the documented path currently turns the suite red [docs/ci.md:190]
- [x] [Review][Patch] [MEDIUM] `docs/ci.md` claims authority comes "from the pinned `github:jpiquot` release-owner identity", which the wrapper no longer pins (see Decision 2) [docs/ci.md:225]
- [x] [Review][Patch] [MEDIUM] The Compose and Kubernetes local-build recipes still publish to `registry.hexalith.com` because `ContainerRegistry` defaults there, so the `docker tag`, `minikube image load`, `kind load docker-image` and `COMMANDAPI_IMAGE=hexalith-eventstore:0.0.0-local.1` lines all name an image the build never produces locally. Only the ACA guide received an explicit `-p:ContainerRegistry=` [docs/guides/deployment-docker-compose.md:335; docs/guides/deployment-kubernetes.md:264]
- [x] [Review][Patch] [MEDIUM] The new repository-scoped role-evidence envelope has no producer and no test — the only checked-in packet takes the legacy `else` branch, so deleting the `request_url` scoping check leaves the whole suite green. That check was the chunk-1A repository-scoping fix [tools/release_evidence_handlers/v3.py:679]
- [x] [Review][Patch] [MEDIUM] Spec bookkeeping is out of step with the tree — all 15 chunk-1A `[Review][Patch]` items are still unchecked although roughly twelve shipped in this range; frontmatter reads `status: 'done'` / `review_loop_iteration: 0` while `sprint-status.yaml:226` reads `review`; and the Code Map plus six open items still anchor to `tools/release_evidence_codec.py`, now a 12-line facade [_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md:48]
- [x] [Review][Patch] [MEDIUM] An unhashable dispatch value raises an uncaught `TypeError` traceback instead of the `[corrective-release-evidence] fail:` line, because `key not in HANDLERS` sits outside the `try` (verified with `"version": [1]`). Anything grepping that prefix sees nothing [tools/validate-corrective-release-evidence.py:40]
- [x] [Review][Patch] [LOW] The retained-codec digest is a bare literal in two files with no cross-check, and `v3.py`'s own copy of the pin can never fire because the dispatcher already gated the identical tuple [tools/validate-corrective-release-evidence.py:13]
- [x] [Review][Patch] [LOW] `repository_issue_html_url` validates more weakly than the regex it replaces — `.../issues/007` and the Arabic-Indic `.../issues/٣` are accepted, and `.../issues/²` escapes as a bare `ValueError` rather than `EvidenceError` (all three verified). Use `[1-9][0-9]*` [tools/release_evidence_handlers/v3.py:477]
- [x] [Review][Patch] [LOW] The single-shared-smoke-summary rule is enforced twice and the second copy is unreachable — `validate_identity` always runs first on the CLI path, so the `validate_packet_files` guard is green by construction and the split-summary test only ever reaches the first message [tools/release_evidence_handlers/v3.py:948]
- [x] [Review][Patch] [LOW] `RunWrapperWithPosture` never clears ambient `HEXALITH_RELEASE_RESERVED_VERSION` / `_AUTHORITY_ISSUE_URL` / `_AUTHORITY_OWNER`, so the disabled- and unset-posture cases are environment-dependent on a developer machine [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:832]
- [x] [Review][Patch] [LOW] The Builds fixture test fails with a raw `git archive` error whenever the release pin is absent from the local submodule object store — precisely the case the deliberately-independent pin design creates. Fetch the pin or fail with a pin-specific diagnostic [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:711]
- [x] [Review][Patch] [LOW] `UnsupportedSyntheticPacketCannotSelectTrustedLiveHandler` proves malformed-shape rejection, not the unsupported-version path its name promises, and still hashes the now-12-line facade into a field nothing reads [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:675]
- [x] [Review][Patch] [LOW] Naming and cross-reference drift — `ReleaseCallerPinsSharedExecutionAndOneMappingWithGitHubAuthority` now asserts the absence of every GitHub-authority input, and the `release.yml` permissions comment cites a `deferred-work.md` phrase ("reusable release workflow split") that does not appear in the ledger [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:490]
- [x] [Review][Patch] [LOW] Deployment-guide hygiene — the ACA guide's two `docker push` lines are dead now that `-p:ContainerRegistry` makes `dotnet publish` push directly; `0.0.0-staging.${SOURCE_SHA:0:12}` yields a leading-zero numeric pre-release identifier the new SemVer gate rejects whenever those twelve hex characters are all digits starting with `0`; and the Kubernetes guide's push and local-cluster blocks depend on `$RELEASE_VERSION` defined in a non-adjacent block [docs/guides/deployment-azure-container-apps.md:266]
- [x] [Review][Patch] [LOW] `RunProcess` and `RunProcessAsync` throw a `TimeoutException` carrying only the file name and budget, discarding the stdout/stderr they already captured — a CI timeout in the multi-minute publish cases would be undiagnosable [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:53]

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

### Review Findings — chunk 3 (2026-08-24)

Chunk 3 code review 2026-08-24 (`94591f35`..`da52e2c8`, Story 3.14 surface only; the concurrent
Story 3.13/3.15 files in the same range were excluded from scope, matching the chunk-2 precedent).
Four layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor. No layer failed.

Independently re-verified before triage: `HANDLER_FILE_SHA256` (`c768b1ef…`) matches the committed
`v3.py` bytes; the checked-in packet still validates at `4d1a0c33…`; deleting the self-pin lets a
tampered `v3.py` validate `exit 0`, so `TamperedLiveHandlerBytesCannotExecuteEvenWithAValidPacket`
*is* mutation-detectable; `63409393…` is now reachable on Hexalith.Builds `origin/main` and
`fix/story-3-14-release-hardening` no longer exists. Carry-forward: the chunk-2 item asking for
`ci.ShouldContain(ApprovedBuildsReleaseSha)` is confirmed still open.

- [x] [Review][Decision] Story 3.15's execution path bypasses the handler pin this diff added, while `docs/ci.md` — edited here, on the 3.14 surface — advertises the opposite. `validate-corrected-deployed-runtime-parity.py:54` does a bare `importlib.import_module` with no on-disk digest, and `deployed_runtime_parity_handlers/v1.py:12` imports `release_evidence_handlers.v3` directly, so the 3.15 verdict is produced by whatever `v3.py` is on disk, never passing through `_load_handler`. The technical fix is clear (same pin, covering the transitive import), but it edits a concurrently-live `in-progress` story's files. Resolved 2026-08-24: narrow the `docs/ci.md` claim to the 3.14 CLI and file the bypass to Story 3.15. The frozen 3.15 `closure.json` binds the live bytes *and* sizes of both 3.15 tools (`v1.py` `d0eb781f…`/38010, verifier `aaa5d067…`/3367, re-checked at `v1.py:268-273`), so any edit invalidates that packet and its three role receipts; Story 3.15 will bind the transitive `release_evidence_handlers.v3` import during the re-freeze it pays at close.
- [x] [Review][Decision] The frozen `Partial publication` matrix row still mandates the gate the 2026-08-22 amendment made opt-in — it reads "tag immutability and one-use authority consumption are what force a retry" and "Retry requires a new version and new authority", while `release.yml:109` declares `require-publication-authority: false`, so no authority exists to consume. This is the identical inconsistency the chunk-2 Decision was raised for; the amendment corrected only `Candidate selection` and `Authorized publication`. Resolved 2026-08-24: owner-ratified amendment. The row was already amended once (2026-08-21, which introduced the authority-consumption language) and the 2026-08-22 opt-in amendment did not revisit it, so this completes a half-applied change rather than opening a new concession.
- [x] [Review][Decision] Removing the unpublished-pin warning left no guard behind. The story record's warning that a release pin must exist on the Builds remote was deleted, and nothing asserts that `release.yml`'s `uses:` SHA is reachable on `origin/main` rather than only in the local object store — precisely the defect that produced the chunk-A+B blocking Decision. Resolved 2026-08-24: deferred. An unresolvable `uses:` SHA already fails the Release dispatch at startup (the quarantined run `32347773728` failure mode), so nothing publishes silently; every guard shape costs either network in the Tier-1 CI-gating lane or a remote-tracking ref CI may not populate. The recurring *drift* class is closed instead by binding the `docs/ci.md` pin prose to `ApprovedBuildsReleaseSha`.

- [x] [Review][Patch] [HIGH] Hash the handler source before importing it — `importlib.import_module` runs first, so a tampered handler's top-level code executes before the pin rejects it (verified: an appended marker printed ahead of the fail line). Resolve via `importlib.util.find_spec(...).origin`, hash, then import; this also removes the `module.__file__ is None` traceback path [tools/validate-corrective-release-evidence.py:53]
- [x] [Review][Patch] [HIGH] Make the URL negative cases property-qualified — `ContainerProvenanceRepositoryUrl` is green by construction. Its two siblings default to values derived from it, so `-p:ContainerProvenanceRepositoryUrl=https` also malforms `ReleaseUrl`, and MSBuild stops at the first `<Error>` with an identically-worded message. Verified by mutation: deleting that guard still yields exit 1 plus the asserted substring, so the case passes; deleting either sibling's guard correctly goes red (exit 0). Use the full property-qualified message as `expectedError`, matching lines 200-203 [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:204]
- [x] [Review][Patch] [HIGH] Stop mutating a tracked source file inside a parallelized CI-gating assembly — the tamper test appends to `tools/release_evidence_handlers/v3.py` in the live working tree while `CorrectedDeployedRuntimeParityClosureTests:378` shells the same validator asserting exit 0. `Contracts.Tests` deliberately carries no `CollectionBehavior`/`xunit.runner.json` (its three siblings do), so the classes run concurrently. A killed run also leaves a tree whose `v3.py` fails every validation, and this repo has a documented concurrent auto-commit loop. Run against a temp copy of `tools/`, or place both classes in one collection [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:696]
- [x] [Review][Patch] [MEDIUM] Both new comments name constants that do not exist or are misplaced — `v3.py` says the dispatcher pins `EXPECTED_V3_HANDLER_SHA256` "before importing it" (the constant appears nowhere in the repo, and the pin is after the import); the verifier says `EXPECTED_PACKET_CODEC_SHA256` is "above, keyed into HANDLERS" when the constant above is `V3_PACKET_CODEC_SHA256` and `EXPECTED_PACKET_CODEC_SHA256` lives in `v3.py:15`. This re-introduces the exact "header comment claims the opposite" defect the chunk-2 HIGH closed [tools/release_evidence_handlers/v3.py:20]
- [x] [Review][Patch] [MEDIUM] The story record replaces one false pin claim with another — it asserts `63409393…` "was never published to the Hexalith.Builds remote (it existed only on the local branch `fix/story-3-14-release-hardening`)". Verified false at HEAD: the commit is contained in `main`/`origin/main` and that branch no longer exists. The still-true phrasing is "was not yet published when the pin was set" [_bmad-output/implementation-artifacts/3-14-corrective-oci-provenance-release.md:36]
- [x] [Review][Patch] [MEDIUM] Spec bookkeeping records as open two items this same diff closed, and one whose correction target is now stale — the `docs/ci.md` "pinned" wording was deleted here, the re-enable prose was added here, and the focused suite is now 70 cases (30 corrective and 40 governance), not the 52 the open item prescribes [_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md:136]
- [x] [Review][Patch] [MEDIUM] Give `_load_handler` its own rejection message — it reuses `_load_dispatch_metadata`'s verbatim "release identity does not select a trusted live handler", so neither the tamper test nor an operator can distinguish an edited handler from a stale packet [tools/validate-corrective-release-evidence.py:55]
- [x] [Review][Patch] [MEDIUM] Anchor the owner pin and exercise the enabled branch — `ReleaseAuthorityOwnerIsPinnedWheneverTheAuthorityGateIsEnabled` only ever takes its `else` branch, so the literal it requires has never been matched against a real enabled block; the pin is an unanchored `ShouldContain`, so `github:jpiquot-bot` satisfies it; and the true branch requires the owner but neither reservation input. Add a synthetic enabled-workflow fixture and an anchored regex [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:569]
- [x] [Review][Patch] [MEDIUM] Declare digest-bearing raw OCI evidence `-text`/`binary`, not `text eol=lf` — `evidence/story-3-15/oci/index.raw` hashes to `4b141085…`, which *is* the selected image identity `docs/ci.md` names, yet the new rule marks it for CRLF normalization with only `*.nupkg` exempted. Nothing is broken today (`git ls-files --eol` is clean), so this is cheap hardening; add a `git check-attr` assertion, since only `CommitMessagePolicyTests` reads `.gitattributes` at all [.gitattributes:21]
- [x] [Review][Patch] [MEDIUM] `docs/ci.md` states Story 3.15's verdict as settled fact — "have provided real authenticated receipts", "the checked-in validation now passes" — while `sprint-status.yaml:227` reads `in-progress` and `spec-3-15` frontmatter reads `in-review` (those two also disagree). The adjacent 3.13 paragraph, edited in the same diff, is carefully stamped "reached `done` on 2026-08-24" [docs/ci.md:377]
- [x] [Review][Patch] [MEDIUM] The 3.15 paragraph omits the self-attestation caveat the 3.13 paragraph just gained — `deployed_runtime_parity_handlers/v1.py:41-43` maps both `eventstore-owner` and `release-owner` to `github:jpiquot`, the identical property for which the same diff added "that acceptance is a self-attestation rather than independent three-party review" to the 3.13 text [docs/ci.md:386]
- [x] [Review][Patch] [MEDIUM] Add env-var and boundary coverage for the three URL guards — the new comment's whole premise is that an environment variable pre-empts the `Condition="'$(X)' == ''"` default, yet every case passes its value via `-p:`. `^https://\S+$` also accepts `https://.`, matches before a trailing newline in .NET, and no case covers `http://` or an empty host [tests/Hexalith.EventStore.Contracts.Tests/Packaging/CorrectiveOciProvenanceReleaseTests.cs:204]
- [x] [Review][Patch] [LOW] Assert `github.event.inputs.` is absent too — the dispatch check only forbids `${{ inputs.`, and the alternate expression form would pass every current assertion (verified absent today, so the assertion is free) [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:519]
- [x] [Review][Patch] [LOW] Add an `ExtractYamlBlock` self-test over a literal fixture — the real `workflow_dispatch:` block extracts to the empty string today, so the helper's capture behavior is unproven by any committed input. It does have teeth (a synthetic `inputs:` child is captured — verified), but the repo's recurring failure mode is exactly an unexercised helper [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:756]
- [x] [Review][Patch] [LOW] Assert exactly one `    with:` block before extracting, and match a quoted `"inputs":` key — `ExtractYamlBlock` takes the first exact-match line, so a second calling job would go unexamined, and the plain substring check is evadable by quoting [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:519]
- [x] [Review][Patch] [LOW] Assert `HANDLERS` and `HANDLER_FILE_SHA256` have identical keys and document the recompute command — the `expected is None` branch fails closed, so a forgotten entry is a silently dead handler with no coverage [tools/validate-corrective-release-evidence.py:21]
- [x] [Review][Patch] [LOW] Documentation and ledger hygiene — `docs/ci.md:239` says "neither test validates `reserved-version` or `release-authority-issue-url` themselves" three sentences after stating that one asserts their absence (intended: their *values*); the sibling `ShouldNotContain` assertions are unscoped, so a `release.yml` comment naming those keys reddens the suite; and `spec-3-14` now carries three identically-named `### Review Findings` headings (`:65`, `:102`, `:204`) at inconsistent nesting, colliding as anchors [docs/ci.md:239]

- [x] [Review][Patch] [HIGH] (from Decision 1) Narrow the trusted-live-handler claim to the 3.14 CLI — the Story 3.15 section states the handler "revalidates the frozen predecessor with its trusted live handler", but `validate-corrected-deployed-runtime-parity.py:54` bare-imports and `v1.py:12` pulls `release_evidence_handlers.v3` transitively, so no pin covers it on that path [docs/ci.md:377]
- [ ] [Review][Patch] [MEDIUM] (from Decision 2) Amend the frozen `Partial publication` row and add a Spec Change Log entry — tag immutability alone forces retry onto a fresh version while the gate is opt-out; one-use authority consumption applies only when `require-publication-authority: true` [_bmad-output/implementation-artifacts/spec-3-14-corrective-oci-provenance-release.md:36]
- [x] [Review][Patch] [MEDIUM] (from Decision 3) Bind the `docs/ci.md` publication-pin prose to the constant — add `ci.ShouldContain(ApprovedBuildsReleaseSha);` beside the existing `secrets.ShouldContain(...)`, whose own comment already names this drift class. Closes the carry-forward chunk-2 item [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:747]

- [x] [Review][Defer] (from Decision 3) No executable guard asserts the release caller's `uses:` SHA is reachable on the Hexalith.Builds remote [.github/workflows/release.yml:103] — deferred, the Release dispatch already fails closed and loudly on an unresolvable SHA
- [x] [Review][Defer] `gitlinkEntry.…ShouldNotBe(ApprovedBuildsReleaseSha)` encodes "deliberately independent" as "must never be equal", so it goes red the day a legitimate submodule bump lands on the release pin — a correct state the test forbids [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:539] — deferred, pre-existing

Dismissed as noise or refuted (6): Story 3.15 content landing on the 3.14 surface (`.gitattributes`
rules and the new `docs/ci.md` section) matches the chunk-2 dismissal already made for 3.13 prose in
the same shared file; the 3.13 two-digest disambiguation and the missing `### Story 3.13` sibling
heading are 3.13-owned prose for a now-`done` story; the `sprint-status.yaml:217-221` narrative
contradicting line 225 is likewise 3.13-owned; a forged `__pycache__` `.pyc` is not additional
exposure, because write access to it equally permits editing the pinned constant; and
"the `workflow_dispatch` guard is still vacuous" is **refuted** — simulating `ExtractYamlBlock`
against a mutated workflow shows an injected `inputs:` child *is* captured, so the assertion has
teeth.
