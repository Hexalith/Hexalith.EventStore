---
schema: hexalith.eventstore.frontcomposer-runtime-decision.v1
recorded_at: '2026-08-10T07:06:11Z'
subject_sha256: '9d074dfd0758a8934f122aab18659627dff1cf5d4c3e548b222cc0d79a881065'
source_sha: 'bb94d93e9b84132cff83a38fba84f25455820d31'
tag: 'v3.91.1'
version: '3.91.1'
consumer_scope: 'Hexalith.FrontComposer Story 11.24'
final_decision: unavailable
authorize_consumer_migration: false
---

# FrontComposer Story 11.24 EventStore Runtime Identity Successor

## Decision

The exact source/package tuple has complete reproduced evidence, but the two content-bound owner
receipts do not yet exist. This record is therefore non-authorizing. FrontComposer Story 11.24 must
remain backlog and must not migrate until the unchanged subject receives both valid receipts.

## Bound Candidate

- EventStore source and `v3.91.1` tag: `bb94d93e9b84132cff83a38fba84f25455820d31`.
- Package version: `3.91.1`.
- Package inventory: all 14 IDs in `tools/release-packages.json` at SHA-256
  `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae`.
- Package hash domain: exact NuGet.org signed-feed response bytes, not the smaller unsigned GitHub
  release-asset bytes.
- Consumer scope: `Hexalith.FrontComposer Story 11.24` only.

## Reproduced Evidence

- [Package manifest](evidence/frontcomposer-story-11-24/bb94d93e9b84132cff83a38fba84f25455820d31/package-manifest.json)
  binds all 14 signed archives to the exact embedded repository commit and SHA-256 values.
- [SHA-256 manifest](evidence/frontcomposer-story-11-24/bb94d93e9b84132cff83a38fba84f25455820d31/nuget-sha256.txt)
  records every independently retrieved archive.
- [Restore receipt](evidence/frontcomposer-story-11-24/bb94d93e9b84132cff83a38fba84f25455820d31/restore-receipt.json)
  records 13 isolated library consumers and one isolated tool install, all passing with fresh
  per-consumer package caches and no project edges.
- [Release and catalog provenance](evidence/frontcomposer-story-11-24/bb94d93e9b84132cff83a38fba84f25455820d31/release-catalog-provenance.json)
  keeps the Builds catalog exposure `a8a50859fa2f27f511a9470dfe1e3ae54d0ebc1a`, release execution
  `f75daebd4c522c081a6f62e274cf25e07971de69`, and historical source gitlink
  `824d7ef100455423aabbcd399c8364074000b2e0` distinct. It also records the separate, non-authorizing
  Builds runner/schema `3.88.0` candidate.
- [Frozen review subject](evidence/frontcomposer-story-11-24/bb94d93e9b84132cff83a38fba84f25455820d31/review-subject.json)
  SHA-256: `9d074dfd0758a8934f122aab18659627dff1cf5d4c3e548b222cc0d79a881065`.

## Approval Checkpoint

Required receipts: `eventstore-owner` and `release-owner`, each separately issued by roster-authorized
`github:jpiquot`, each accepted after the subject freeze, each citing the exact subject SHA-256,
candidate tuple, scope, and durable GitHub source. The exact actions and receipt shapes are in
[owner-actions.md](evidence/frontcomposer-story-11-24/bb94d93e9b84132cff83a38fba84f25455820d31/owner-actions.md).

No receipt is inferred from the release actor, workflow success, tag, current `main`, ancestry, or
catalog exposure. Until both receipts validate, the literal frontmatter values remain
`final_decision: unavailable` and `authorize_consumer_migration: false`.

## Rejected Bases

- Story 1.20's retired proof packages, whether reused or rebuilt.
- Source ancestry or current-main equality.
- Release success or catalog presence without the exact signed-feed bytes.
- The Hexalith.Tenants Story 2.12 waiver.
- Any source, version, consumer scope, subject bytes, package hash, or Builds identity different from
  the frozen review subject.
