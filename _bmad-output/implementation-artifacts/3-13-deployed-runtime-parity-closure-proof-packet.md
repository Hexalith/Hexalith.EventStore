# Story 3.13 Deployed Runtime Parity Closure Proof Packet

## Decision

**Verdict: `fail-closed`. Story 3.13 must remain non-`done`.**

The Story 1.20 source/package proof and immutable OCI bytes remain unchanged. Fresh read-only
validation confirms index/descriptor/body relationships, but the packet does not retain child or
config response metadata, and its smoke logs omit the structured HTTP/platform/timing facts needed
to replay the claimed `/alive` result. No single candidate supplies those facts together with the
approved source, independently rehashed original package bytes, semantic-release provenance, valid
OCI source labels, Production-equivalent runtime, deployed authority, and three Story 3.13
acceptances. The conforming v3.77.2 release is a different source lineage and cannot fill those gaps.

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
  SHA-256: `10c7d96795c311abede34fc9f7ffbc9f93d062de76c5b55fdff97430d13d3669`.
- [Evidence manifest](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/evidence-sha256.txt)
  binds the core manifest, crosswalk, and review subject entry-by-entry. Its own hash is not quoted
  here because the review subject binds this proof packet, deliberately avoiding a checksum cycle.
- [Reviewer roster](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/reviewer-roster.json)
  SHA-256: `759afcbe2429638affb8e5ebe4afd26112fc7b4108376fe38e0b103b5701024f`.
  The roster is authorized by GitHub issue comment 5290564372 on 2026-08-14; that comment
  ratifies the exact three-role mapping and does not authorize Story 3.13 done status.
- [Review subject](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/review-subject.json)
  is frozen after this packet and binds the raw crosswalk SHA-256
  `10c7d96795c311abede34fc9f7ffbc9f93d062de76c5b55fdff97430d13d3669`, evidence-core manifest
  SHA-256 `98ec34537eef70a43194e905a4f35402d4da76bd64322699ddc35d22502bfb26`, and this packet. The
  subject file digest and this packet digest are recorded only in `evidence-sha256.txt` and the
  subject's `proof_packet.sha256` field so this packet does not create a checksum cycle.

Any change to the crosswalk, evidence-core manifest, or proof-packet bytes requires a replacement
review subject. The three missing receipts remain outside the evidence checksum manifests, must be
stored beneath `acceptances/{subject_sha256}`, and must cite the exact unchanged subject hash and a
reviewer identity authorized by the hash-bound repository roster.

## Builds Identity Pins

Story 3.13 records two distinct Hexalith.Builds identities; they are not interchangeable:

- `baseline.builds_gitlink_sha` (`e69891f67578c2f0dec1cd7d7eea113430d31077`) is the EventStore
  repository's `references/Hexalith.Builds` gitlink at the Story 3.13 baseline commit. Predecessor
  Git-object checks bind that historical pointer.
- Tool pins (`a53166539bf4441d5e33d04281b14c2d59e950c3`) identify the shared OCI validator and
  smoke-script bytes read from the Builds object store for independent OCI/runtime verification.

## Frozen Predecessor Inputs

| Input | Git identity | SHA-256 / result |
| --- | --- | --- |
| Story 1.20 record | blob `bf644e41a1ac59673329e71dd7ef4daa1591eb49` | `0feee912874154a3885fbe69ac68419c89b209b8c9c5b9291833604881f34fa5`; pass |
| Story 1.20 proof packet | blob `47f09bdf65057fdda1ec1b0a77bb9398675b1de7` | `cb1ccde9d5cc5ca6cb52cbeab30fb9cd59bd89771e14f4b489e20bd5e3d46743`; pass |
| Story 1.20 evidence tree | tree `fcd0c25c9cf6bb0554e208d529f1ef09c223725a` | 40 files frozen; all 33 critical-manifest entries pass |
| Story 3.12 record | blob `e420feb72715b726fa421683a829413d80c4a31b` | `2bfc9ff991c9aeeaf11fd9c1926a17bb44ca290f99bd75b05df68a6edaf3e09c`; pass |

The full 40-file tree is independently bound by
[predecessor-tree-sha256.txt](evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/predecessor-tree-sha256.txt).
Story 3.13 wrote two predecessor files at `3d6dea69` solely to restore approved bytes that the
unrelated commit `089369bb` ("docs: clear remaining root predecessor SDK patch tokens") had
drifted: `1-20-owner-approved-parity-closure-proof-packet.md` and
`evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/environment.txt`. No predecessor
file was normalized, regenerated, or otherwise rewritten toward a new value. Net predecessor
state at HEAD is byte-identical to the approved identity — blob `47f09bdf65057fdda1ec1b0a77bb9398675b1de7`,
tree `fcd0c25c9cf6bb0554e208d529f1ef09c223725a`, and all 33 critical-manifest entries pass — which
is the sense in which `verdict.predecessor_state_changed` remains `false`. Three sibling Story 1.20
evidence trees (`38f85086…`, `4983299103…`, `ec0d35a0…`) remain drifted by that same commit and are
recorded as a separate Epic 1 integrity defect; Story 3.13 holds no authority to repair them.

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
| Immutable index | Separate raw 493-byte `tag-response.raw` and `digest-response.raw` bodies; byte-identical to `index.raw`; matching retained index content type and `Docker-Content-Digest` | Pass |
| Platform graph | Exactly `linux/amd64` and `linux/arm64`; every retained child/config body matches its descriptor, but child/config response content types and digest headers were not retained | Fail: response metadata cannot be independently replayed |
| Config source provenance | Both configs set source, URL, and documentation to the malformed value `https`; version is `3.82.0`, revision is absent, and `v3.82.0` resolves to `0b12950f...` | Fail: labels are not usable URLs and provide no exact approved-source mapping |
| Runtime execution | Digest-pinned smoke attempt, `2026-08-04T11:10:03Z` through `11:12:03Z` | Fail: logs omit structured HTTP status, redirects, observed platform, per-platform timestamps, and exit codes |
| Runtime contract equivalence | `docs/ci.md` requires `Production`; captured execution used `Development` | Fail: Task 6 equivalence remains open |
| Semantic release | No release tag/version, workflow run/attempt, Builds execution SHA, or publisher identity in selected lineage | Fail |
| Authority | Hash-checked proof publication authority permits quarantine only and explicitly excludes deployment/migration | Fail for deployed closure |
| Story 3.13 acceptance | EventStore owner, Release owner, and Test Architect acceptances | Missing: 0 of 3 |

The raw registry bodies and bounded runtime files are retained under the content-addressed
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
4. Repeat the registry capture and retain child-manifest and config response content types, digest
   headers, byte lengths, and raw-body hashes for independent replay.
5. Repeat both digest-pinned smokes and retain structured support-safe `/alive`, redirect,
   observed-platform, timing, exit-code, and readiness evidence.
6. Run and retain that same contract under the documented `Production` hosting environment.
7. Supply separately authorized deployed-identity authority for the complete exact lineage. The
   retained quarantine-only authority expires at `2026-08-25T00:00:00Z`, after which a replacement
   authority record is mandatory.
8. Only after all checks pass, obtain EventStore owner, Release owner, and Test Architect
   acceptance of one unchanged replacement review subject.
9. After every lineage check passes, obtain EventStore owner, Release owner, and Test Architect
   acceptance of one unchanged replacement review subject. The reviewer roster is now bound to
   issue comment 5290564372; earlier acceptances of subject 93d70d51 are invalid for this packet.

## Verification Record

- Story 1.20 critical manifest: all 33 entries passed `sha256sum -c`.
- Contracts test project Release build: succeeded with zero warnings and zero errors.
- Focused `DeployedRuntimeParityClosureTests`: 186 passed, zero failed/skipped/not-run
  (re-measured 2026-08-13); this is the test count attributable to Story 3.13. The zero-skip result
  was measured on a symlink-capable host; link-restricted hosts skip the reparse-point test.
- Complete Contracts suite: `1260` was the 2026-08-11 workspace aggregate and includes concurrent,
  unrelated tests; it is not attributable to Story 3.13. The current aggregate is reported only as
  a regression signal. The 2026-08-13 pre-patch aggregate passed 1409/1409 with zero failures and
  zero skips; the earlier 21 OQ8 failures were caused by this change set's status-row removal and
  were subsequently reconciled in the OQ8 verifier.
- The verifier also derives the actual fail-closed review subject and outer checksum manifest,
  rejects extra or byte-mutated package archives, validates baseline Git objects, and exercises
  independently rebound mutations across release, authority, OCI, runtime, roster, and receipts.
- The prior smoke tool returned pass under `Development`, but the retained logs do not independently
  prove its HTTP/platform execution facts; Production contract equivalence also failed closed.
- Story 3.13 authored no change to runtime source, workflow, release configuration, package
  manifest, consumer, deployment, or registry object. Historical Story 3.13 commits did advance
  root-declared submodule gitlinks under `references/`; those changes are outside this packet's
  runtime/external-state claims. Predecessor identity is pinned to historical Git objects at the
  recorded baseline commit, so live gitlink drift cannot alter the frozen evidence.

Because the evidence is explicitly fail-closed, Story 3.13 remains non-`done`. The response and
runtime evidence gaps above must be closed before the packet can claim reproducible partial passes;
AC4 also remains unsatisfied.
