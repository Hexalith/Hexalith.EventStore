# Story 8.2 Preflight Before ADR Amendment

Captured at `2026-09-13T11:46:11Z` before any Contracts source edit.

## Source and worktree identity

- EventStore HEAD: `dfc0ac557c43363159b55bffb4d40feceab1f787`.
- Approved historical source: `b200305978577530ee2e6ba9e92b886d26dc6f6f`.
- `git diff --quiet b200305978577530ee2e6ba9e92b886d26dc6f6f..HEAD -- src/Hexalith.EventStore.Contracts/Security` exited `0`; the Contracts security surface is byte-identical between the approved historical source and current HEAD.
- Pre-amendment worktree changes were limited to BMad execution artifacts: modified `sprint-status.yaml`, new `epic-8-context.md`, and new Story 8.2 specification. No `src/`, `tests/`, `scripts/`, project, package, or manifest file was modified.

## Authority and release identity

- Story 8.1 normative SHA-256 recomputed with its section 1.2 command: `0f841d5a72a0d0b10fa42a7e765b7282a810f3a5a2aa2b41da2001d17a054ae7`.
- Complete pre-amendment authority file SHA-256: `384576d042cd4663cd8f4bab426d7b1328a43debdfc7b1fcfbcd9e2acad9aed7`.
- Detached approval packet: `AR-20260801-01`, approved for the old normative digest only.
- `story_8_2_authorized` before amendment: `true` for the old normative digest only.
- `tools/release-packages.json` SHA-256: `6b0b70b856839d4117bcd969f6a2de0093c477c109cb79f3f2882b1f05effcae`.
- Release inventory: 14 packages; Story 8.2 has no authority to change it.

## Current Contracts security inventory

The deterministic combined SHA-256 over sorted per-file SHA-256 output is
`e1ad731332ada09e13c489b34fd6df46ae9ceefba73c12d3ffb8ff40938c77d0`.

Payload-protection seams to retain unchanged:

- `IEventPayloadProtectionService.cs`: `649a95cb67565de19dd3952431924a0cb9fd6394c5c0eddbcf8853cbe3b2d789`.
- `EventStorePayloadProtectionMetadata.cs`: `42b257105c44bfc084f38c7386c70d93bcb164875e3d77218cab450ce3087f13`.
- `EventStorePayloadProtectionMetadataCarrier.cs`: `535fcaf1be45fe976e58da3e226ca21e06c34a35105fb59553de795720fd85d3`.
- `PayloadProtectionResult.cs`: `db02df395d9d2077ef6453eb148797ec546c63b2faf0b761c0e0df8775e04479`.
- `SnapshotProtectionResult.cs`: `06a9fcc271f5c460578fd4c64adcb4c1ae8d95c16143a1a47beb9f447cafd4cf`.
- `PayloadUnprotectionOutcome.cs`: `5c0e17b177ca2d7107acde3b4ccdd487dbc5352430bfdc307cd4458459655f02`.
- `SnapshotUnprotectionOutcome.cs`: `42db0ead92349678eb5f333f232b1ca1b5e545f1dfe42fb5ab610cfc474d3870`.
- `UnreadableProtectedDataReason.cs`: `ef391b3b4a5eca66241a5306222b122c6dbb9f48fa67102da585f0b80c51cdf3`.

The current public provider contract contains two required event methods, two
required object-based snapshot methods, metadata-aware/default typed snapshot
methods, and typed event/snapshot unprotect defaults. There is no occurrence,
stable v2 snapshot-carrier, write-result/completion, policy, or erasure-state
contract. Existing result records and metadata/reason serialization shapes must
remain unchanged; additions must use new types and default interface methods.

## Preflight disposition

The old digest and approval match, current Contracts security bytes match the
approved source, and the release manifest matches. Later planning authority,
however, requires exact snapshot-carrier, context-aware overload, and completion
signatures absent from the approved normative bytes. Per the approved Option A,
the old approval must be superseded, Story 8.1 reopened, and source work blocked
until the replacement normative digest receives content-bound approval.
