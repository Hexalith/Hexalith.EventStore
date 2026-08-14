# Story 3.13 Deployed Runtime Parity Closure Proof Packet — v3.94.1

## Decision

**Verdict: `fail-closed`. Story 3.13 must remain non-`done`.**

Administrator approved `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-14.md` on
2026-08-14. The selected exact identity is source `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`,
release `v3.94.1`, and the 14 manifest packages at version `3.94.1`. The historical
`fa2d1c99` packet remains immutable fail-closed evidence and is not this subject.

Independent checks recovered and hashed all 14 NuGet.org archives, bound GitHub release
`v3.94.1` to workflow run `31781920404` attempt `1` and Builds execution
`f75daebd4c522c081a6f62e274cf25e07971de69`, recaptured digest-bound OCI index/child/config response metadata for
`sha256:ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd`, and executed Production `/alive` on both platform children
(HTTP 200, zero redirects). OCI provenance labels remain the malformed value `https`
with no revision. The proposal authorizes identity replacement only, not deployment.
Zero of three content-bound acceptances exist for this new subject. Prior comments on
subject `394292a2` are void.

This packet authorizes no package publication, registry mutation, deployment mutation, consumer
migration, predecessor change, Epic 1 change, submodule change, or G5 decision.

## Artifact Identity Pin

- Selected source: `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`.
- Selected package version: `3.94.1`.
- Selected package-hash manifest SHA-256: `56b4f0ef9175f1c8a3a42f9d1a0af7ab3fada64120be07f43e8ee8ebeb59c4d9`.
- Selected immutable OCI index: `registry.hexalith.com/eventstore@sha256:ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd`.
- Container discovery tag: `3.94.1` (Hexalith SemVer tag; GitHub release tag is `v3.94.1`).
- Workflow: `31781920404` attempt `1`; Builds execution `f75daebd4c522c081a6f62e274cf25e07971de69`.
- Historical fail-closed packet remains at
  `_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/`.

Any change to the crosswalk, evidence-core manifest, or these proof-packet bytes requires a
replacement review subject. The three missing receipts remain outside the evidence checksum
manifests and must cite the exact unchanged new subject hash.

## Builds Identity Pins

- Story 3.13 baseline Builds gitlink remains `e69891f67578c2f0dec1cd7d7eea113430d31077`.
- The `v3.94.1` release reusable-workflow pin is `f75daebd4c522c081a6f62e274cf25e07971de69`.
- Current checkout tool bytes for the shared validator and smoke scripts still hash to
  `e1547e31fbdb8a678c99a245510e718c1cb35f6b9ec51264aa7bc1cdae419509` and `c7ec862fd79bf96be12670d53707e3c8a828e0161e58745e57b652a42243e8a9`.

## Frozen Predecessor Inputs

Stories 1.20 and 3.12 are unchanged historical predecessors. Their fingerprints match the
historical fail-closed packet. Story 3.13 wrote no predecessor bytes for this replacement.

## Remaining Blockers

1. Malformed OCI provenance labels and absent revision.
2. No deployment-authorizing durable record.
3. Three content-bound acceptances of this new subject are missing.
