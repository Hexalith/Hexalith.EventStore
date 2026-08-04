# Story 3.13 Deployed Runtime Parity Closure Proof Packet

## Decision

**Verdict: `fail-closed`. Story 3.13 must remain non-`done`.**

The Story 1.20 source/package proof and its immutable OCI graph remain unchanged, and fresh
read-only validation proves the index plus both platform children/configs and `/alive` executions.
No single candidate, however, supplies the approved source, independently rehashed original
package bytes, semantic-release provenance, valid OCI source labels, Production-equivalent runtime,
deployed authority, and three Story 3.13 acceptances. The conforming v3.77.2 release is a different
source lineage and cannot fill those gaps.

This packet authorizes no package publication, registry mutation, deployment mutation, consumer
migration, predecessor change, Epic 1 change, submodule change, or G5 decision.

## Artifact Identity Pin

- Selected source: `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`.
- Selected package version: `999.1.20-proof.fa2d1c9910f8`.
- Selected package-hash manifest SHA-256:
  `4271ddc76411780591423ab024b776cd34a2abccf1cc2dac03a245e141dbe0bc`.
- Selected immutable OCI index:
  `registry.hexalith.com/eventstore@sha256:523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87`.
- [Identity crosswalk](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/identity-crosswalk.json)
  SHA-256: `8ddee2e187a8393c0036fa26dee496d86b6cd36e93d43a6bf63a2b1e840cf63d`.
- [Evidence manifest](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/evidence-sha256.txt)
  SHA-256: `0823a279fc5b115b3ae5f6ed32475a2e79e187d6f03cee153f06f6d95a77de7f`.
- [Review subject](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/review-subject.json)
  is frozen after this packet and binds the raw crosswalk, evidence-core manifest, and this packet.

Any change to the crosswalk, evidence-core manifest, or proof-packet bytes requires a replacement
review subject. The three missing receipts remain external to the hashed evidence and must cite
the exact unchanged subject hash.

## Frozen Predecessor Inputs

| Input | Git identity | SHA-256 / result |
| --- | --- | --- |
| Story 1.20 record | blob `bf644e41a1ac59673329e71dd7ef4daa1591eb49` | `0feee912874154a3885fbe69ac68419c89b209b8c9c5b9291833604881f34fa5`; pass |
| Story 1.20 proof packet | blob `47f09bdf65057fdda1ec1b0a77bb9398675b1de7` | `cb1ccde9d5cc5ca6cb52cbeab30fb9cd59bd89771e14f4b489e20bd5e3d46743`; pass |
| Story 1.20 evidence tree | tree `fcd0c25c9cf6bb0554e208d529f1ef09c223725a` | 40 files frozen; all 33 critical-manifest entries pass |
| Story 3.12 record | blob `e420feb72715b726fa421683a829413d80c4a31b` | `2bfc9ff991c9aeeaf11fd9c1926a17bb44ca290f99bd75b05df68a6edaf3e09c`; pass |

The full 40-file tree is independently bound by
[predecessor-tree-sha256.txt](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/predecessor-tree-sha256.txt).
No predecessor file was normalized, regenerated, or modified.

## Candidate Crosswalk

| Candidate | Source/packages | Release/index | Disposition |
| --- | --- | --- | --- |
| Story 1.20 approved proof | Exact approved `fa2d1c...` and retained 14-package hash list | No semantic release; quarantined proof index `523f01df...` | Selected for independent checking; `fail-closed` |
| Story 3.12 v3.77.2 | Source `77a9a442...`; exact 14 packages at `3.77.2` | Run `29694935552` attempt 1; Builds `9ec0a032...`; index `db3ab41e...` | Rejected: source differs from approved `fa2d1c...` |
| v3.75.0 | Historical release | Single-platform image | Excluded failed history |
| v3.77.1 | Historical release | Two-platform publication with failed product smokes | Excluded quarantined history |

The v3.77.2 source is an ancestor 103 commits behind the approved source. Ancestry does not satisfy
exact provenance. Both prohibited combinations—Story 1.20 source/packages with v3.77.2
release/index, and the inverse—are explicit rejected rows in the crosswalk and focused tests.

## Independent Results

| Check | Evidence | Result |
| --- | --- | --- |
| Exact release inventory | `tools/release-packages.json`, SHA-256 `6b0b70b8...`, exactly 14 unique IDs/projects | Pass |
| Original proof package bytes | [package-availability.json](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/package-availability.json) | Fail: 0 of 14 original archives recovered; every NuGet.org lookup returned 404 |
| Immutable index | Separate raw 493-byte `tag-response.raw` and `digest-response.raw` bodies; byte-identical to `index.raw`; matching content type and `Docker-Content-Digest` | Pass |
| Platform graph | Exactly `linux/amd64` and `linux/arm64`; every child/config digest, size, media type, and platform matches | Pass |
| Config source provenance | Both configs set source, URL, and documentation to the malformed value `https`; version is `3.82.0`, revision is absent, and `v3.82.0` resolves to `0b12950f...` | Fail: labels are not usable URLs and provide no exact approved-source mapping |
| Runtime execution | Digest-pinned `/alive`, `2026-08-04T11:10:03Z` through `11:12:03Z` | Both children and cleanup pass under `Development` |
| Runtime contract equivalence | `docs/ci.md` requires `Production`; captured execution used `Development` | Fail: Task 6 equivalence remains open |
| Semantic release | No release tag/version, workflow run/attempt, Builds execution SHA, or publisher identity in selected lineage | Fail |
| Authority | Hash-checked proof publication authority permits quarantine only and explicitly excludes deployment/migration | Fail for deployed closure |
| Story 3.13 acceptance | EventStore owner, Release owner, and Test Architect acceptances | Missing: 0 of 3 |

The raw registry graph and support-safe runtime files are retained under the content-addressed
[Story 3.13 evidence directory](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/).
The shared OCI validator bytes hash to
`e1547e31fbdb8a678c99a245510e718c1cb35f6b9ec51264aa7bc1cdae419509`; its CLI
requires a SemVer discovery tag, so the non-SemVer proof tag was checked through the unchanged
shared validation functions and immutable-digest reads. That compatibility gap is recorded and is
not treated as a pass for release provenance.

## Blockers And Reopen Triggers

1. Recover all 14 original proof archives from a content-addressed durable source and rehash them;
   rebuilding lookalike packages is forbidden.
2. Supply durable semantic-release provenance that binds the exact approved source, original
   package bytes, workflow run/attempt, Builds execution SHA, publisher, authority, and immutable
   index as one lineage.
3. Supply content-bound provenance that maps the proof index directly to the approved source; the
   malformed source/URL/documentation labels, absent revision, and mismatched v3.82.0 tag are
   insufficient.
4. Run and retain the same digest-pinned contract for both children under the documented
   `Production` hosting environment; the retained `Development` executions prove liveness only.
5. Supply separately authorized deployed-identity authority for the complete exact lineage.
6. Only after all checks pass, obtain distinct EventStore owner, Release owner, and Test Architect
   acceptance of one unchanged replacement review subject.

## Verification Record

- Story 1.20 critical manifest: all 33 entries passed `sha256sum -c`.
- Contracts test project Release build: succeeded with zero warnings and zero errors.
- Focused `DeployedRuntimeParityClosureTests`: 26 passed, zero failed/skipped/not-run.
- Both immutable child `/alive` executions and cleanup passed under `Development`; documented
  `Production` contract equivalence failed closed.
- No runtime source, workflow, release configuration, package manifest, submodule, consumer,
  deployment, registry object, Story 1.20, Story 3.12, or Epic 1 state changed.

Because the evidence is reproducible and explicitly fail-closed, Story 3.13 may enter review. AC4
does not pass, so Story 3.13 may not become `done`.
