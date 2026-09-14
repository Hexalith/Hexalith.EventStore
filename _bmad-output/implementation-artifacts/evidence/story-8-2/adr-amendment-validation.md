# Story 8.1 ADR Amendment Validation

Validated on `2026-09-13` before replacement approval and before any Story 8.2
source edit.

## Approval subject

- Replacement packet: `AR-20260913-01`.
- Normative SHA-256: `de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e`.
- EventStore source: `dfc0ac557c43363159b55bffb4d40feceab1f787`.
- Parties source: `4378dede55d92e489caf7aad63d6c2892e6f856d`.
- Complete pending-approval authority file SHA-256:
  `cf479b3df7761571084ad7a255f824e94be94154783f6264b56765d00d22bfe7`.

## Frozen additive API

The normative range now contains exactly these missing public types:

- `ProtectedSnapshotPayloadV2`.
- `PayloadProtectionCompletionContext`.
- `PayloadProtectionPersistenceOutcome`.
- `PayloadProtectionWriteResult`.
- `SnapshotProtectionWriteResult`.

It retains the previously frozen `PayloadProtectionOccurrenceContext` and adds
exact default-interface signatures for context-aware event/snapshot protect,
typed context-aware event/snapshot unprotect, completion-lease acquisition, and
completion with `Persisted`, `NotPersisted`, or `Unknown`.

The mandatory defaults preserve old providers by delegating only non-v2 paths.
They do not fabricate a completion context, do not pass v2 through an old
member, preserve cancellation, return the existing typed unsupported reason on
v2 reads, and fault explicitly if an old path claims v2 or receives completion
work.

## Checks

The Story 8.1 section 1.2 recomputation command produced the replacement digest
above and asserted unique markers, LF-only bytes, and no BOM. The exact V001–V138
registry count remained `138`. `git diff --check` produced no findings.
`git diff --name-only -- src tests scripts tools` produced no paths, proving the
approval gate was observed. Sprint Story 8.1 is `in-progress`; Story 8.2 remains
`in-progress` for planning/evidence only and `story_8_2_authorized` is `false`.

The pre-existing `AR-20260801-01` record is retained as superseded historical
evidence and cannot authorize these replacement bytes. Source work remains
blocked until the required named roles approve this exact subject with no open
material finding and accept the documented residual risks plus this additive
API design.
