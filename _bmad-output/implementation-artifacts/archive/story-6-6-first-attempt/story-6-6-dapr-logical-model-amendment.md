# Story 6.6: Selected Dapr logical evidence model

**Decision date:** 2026-10-08.

**Authority:** The owner answered “Use the proposed model (Recommended)” to the
question whether to use the [Dapr logical claim proposal](evidence/story-6-6/source-candidates-2026-10-07/dapr-logical-claims.design-candidate.md)
as the basis and resolve its remaining schema/control choices within the approved
Dapr-only and trusted-code constraints. This authorizes those implementation
choices. It supplies no production profile, key issuance, serving-peer enrollment,
catalog completeness, release, deployment or retained-data migration authority.

**Status:** Selected implementation basis. The original proposal remains historical
preparation. The [Dapr-only](story-6-6-dapr-only-amendment.md) and
[trusted-code](story-6-6-trusted-code-amendment.md) amendments still govern.
Historical AD-13 approval and fixtures remain unchanged. All M1–M8/O01–O20
dispositions and activation fences remain open until their actual gates pass.

## Logical claims and carriers

Use a separately identified `dapr-actor-logical-v1` evidence model. Add a logical
carrier rather than changing the meaning of any existing `StoredDigest` field.
Retain original application payload bytes, metadata, MessageIds and sequence.
Logical actor readback proves the returned application value; it supplies neither
physical provider evidence nor an authenticated historical committed generation.

Select the proposal's route separator `HX-EV-DAPR-ROUTE-1\0`, codec 01, 20 fields:
the historical route's 18 ordered scalar fields with tag 01 renamed to
`ApplicationLogicalDigest`, followed by tag 13 `SourceBindingHash` (B32) and tag 14
`LogicalEvidenceModelId` (U). Select prefix separator `HX-EV-DAPR-PREFIX-1\0`, codec
01, 17 fields: the historical prefix's 15 ordered scalar fields with distinct
logical digest/list/accumulator meanings, followed by tags 10 and 11 for the same
source hash and model ID. Tags are hexadecimal. Reject historical separators,
extra/missing/reordered/duplicate fields, trailing bytes and model disagreement.
The existing bounded `HX-EV-PROOF-1` outer framing remains compatible.

Reuse signing purposes 01 (route) and 03 (prefix) only with these new separators
and a distinct model/domain-scoped current-key trust configuration. Use the
existing ECDSA P-256/SHA-256, exact SPKI and 64-byte IEEE P1363 signature framing.
An old provider/raw-source key or claim does not gain logical-model authority.
Validate current key, domain, model and registry fingerprint on every verification.
Local explicit keys qualify local tests only; do not issue a production key or
register a signing host from a fixture.

Before implementing codecs, record the exact selected source-binding,
consumed-metadata, ordered-list and accumulator preimages in a companion model
document with independently recomputed vectors. Use strict existing big-endian
U/B/B32/I/N/T primitives and distinct `HX-EV-DAPR-*` domain separators. Bind every
consumed metadata field, preserving timestamp offsets, null versus empty extension
maps, sorted exact extension keys/values and optional logical null/value presence.
Typed actor readback does not establish whether an original JSON property was
absent or explicitly null; make no physical wire-presence claim from that value. A
logical digest alone does not bind aggregate type or all consumed metadata.
Hashes and caller DTOs do not establish source authority by themselves.

## Source admission and initial integration

The source binding identifies the actual actor application/namespace, actor type,
canonical actor ID and tenant/domain/aggregate/type, fixed head and target,
immutable event/metadata key mapping, exact pinned source configuration and digest
codec. Use the current `AggregateIdentity.ActorId`, `EventStreamKeyPrefix` and
`MetadataKey` application mappings; event suffixes are invariant decimal sequence
numbers. Exclude varying per-event keys from the range binding. Derive each key
from that mapping and signed sequence, never from an arbitrary caller key.

Read only through the owning actor and `IActorStateManager`. Prepare an entire
contiguous page, verify addressed identity, unprotection, application digest when
present, immutable metadata and fixed head/floor/ETag before catalog callbacks.
Resolve and validate through the existing shared evolution service. Verify source
and capability again before signing or returning success. Preserve originating
cancellation; cancellation or observed loss produces no uncommitted proof or
partial domain result.

Historical metadata-V1 values without a same-save digest remain admissible only
through the addressed logical source, complete prefix and unambiguous registry
mapping. Compute their logical digest from the admitted bytes, and bind the
absence of the stored digest into consumed metadata. This is current logical
readback, never a fabricated historical witness. Metadata-V2 without its required
same-save digest refuses. Preserve the existing implicit-writer V2 fence.

The first integrated logical replay is event-only and starts from sequence 1.
Require a retained floor allowing that complete prefix and an immutable head/target.
Reject checkpoint and snapshot anchors until their distinct logical schemas and
actor-owned readback are implemented and qualified. Permit only the prescribed
empty-stream or empty-target count-zero forms, with no routes or anchors; no
arithmetic may increment `long.MaxValue`. Extend an admitted prior accumulator
only through its bound operation ledger, not a caller-provided hash or token.

## Control owner and durable boundaries

Select a dedicated replay-operation actor for new page ledger, pinned response
blob and final-result participants, with stable tenant/operation identity,
bounded application keys/values and one `SaveStateAsync` boundary. Keep existing
aggregate/event/snapshot/outbox/drain mutations exclusively under AggregateActor.
The replay actor owns generation/fence admission, idempotent request identity,
page transitions, pinned retries and exact final logical readback. Source actor
reads and target writes are a recoverable cross-owner sequence, never one atomic
transaction. Do not introduce SQL, backend credentials or private Dapr keys.

Implement fresh logical readback after ambiguous save, distinct proven/no-commit/
indeterminate outcomes, same-identity retries, takeover fencing and cancellation
around the durable boundary. Retain charges and last-good truth while uncertainty
remains. Fit all atomic participants into the selected actor save; otherwise keep
that operation unavailable. Qualify live component/topology capabilities before
admitting a hold producer or production continuation.

## Delivery and verification

Prepare the exact model document, logical DTOs/codecs/signature checks, addressed
reader composition and actor-owned operation protocol in dependency order. Use
real shared-service/source paths and persisted end-state assertions, independent
vectors, substitution/refusal, multi-page/empty/terminal, cancellation, lost-ack,
restart and compiling mutation controls. Verify applicable compatibility and full
affected regressions, the required Release/package build and local Dapr lanes.

Keep unqualified registrations unavailable. Complete reviewed domain catalogs,
immutable framework/native execution, serving-peer/source/key pins, broker/fleet
evidence and the separately ratified production profile remain required. Passing
local codecs, actor tests or injected manifests alone closes no parent obligation
and enables no V2, deployment, publication or retained-data migration.
