# Dapr logical anchored replay preparation model v1

This distinct dormant schema resolves the next local M4 choices under the selected
model. It does not change event-only `dapr-actor-logical-v1`, its absent prefix
tags 0d–0f, historical claims, purpose-07 command proofs or legacy snapshots.
Snapshot-cost/protection redesign and production activation are outside this
slice. Source-only aggregate reconstruction anchors are supported; command,
checkpoint and rebase continuations remain unavailable.

## Exact anchor selection and genesis

U is u32 strict UTF-8 length followed by bytes (nonblank, at most 512 bytes); H is
exactly 32 unframed bytes; N is nonnegative signed big-endian i64. Separators
include NUL. Selection records use codec byte 01, u16 field count and strictly
ascending one-byte tags, with no unknown/missing/duplicate/reordered/trailing
fields. Maximum selection image is 8 KiB.

`HX-EV-DAPR-REPLAY-ANCHOR-1\0 || 01 || 0010` fields:
01 U tenant; 02 U domain; 03 U aggregate ID; 04 U aggregate type;
05 H original requested fixed-source binding; 06 H current registry;
07 H exact reconstruction binding; 08 H exact snapshot witness image;
09 H canonical initial state; 0a H completed covered accumulator;
0b H completed covered effective chain; 0c H completed covered page transcript;
0d N positive covered sequence; 0e N actual head; 0f N original target;
10 U model exactly `dapr-actor-logical-anchored-replay-v1`.

Require 1 <= covered <= target <= head. Covered=target<head is ineligible;
timelines always use full replay from one. At covered=target=head, the planned
zero tail uses start=k+1/end=k below MAX, start=end=MAX at MAX. No code increments
MAX. A selection DTO or matching hashes cannot establish actual snapshot/origin
or committed progress. Original source hash fixes the requested target; the
snapshot witness independently fixes the same source with target=covered.

Bind the verified completed origin into new distinct seeds; never relabel its
old-target accumulator as a requested-target v1 predecessor. Each preimage uses
separator || 01 || H(selection image SHA-256) || H(covered commitment):

- `HX-EV-DAPR-ANCHOR-ACCUMULATOR-1\0` uses covered accumulator.
- `HX-EV-DAPR-ANCHOR-EFFECTIVE-1\0` uses covered effective chain.
- `HX-EV-DAPR-ANCHOR-TRANSCRIPT-1\0` uses covered page transcript.

The selection already binds the full scope, requested source, registry,
reconstruction, exact witness and state. Seeds are private preparation only.
Future anchored prefix, operation ledger, page transcript and terminal consumers
must explicitly select matching separate schemas and actual owner readback;
existing v1 prefix/operation/command paths cannot consume these seeds. This slice
must not issue an anchored signed prefix or command proof from codec output.

## Actor-owned snapshot pair issuance

Only the actual AggregateActor may save the separate plaintext canonical snapshot
and witness pair described by the snapshot model. Keep its legacy SnapshotKey
unchanged. Use the actual actor StateManager and exact actor identity, source,
registry/reconstruction/serializer and a freshly captured actual completed origin.
The witness's target is its covered sequence under the current fixed head.

Prepare bounded private canonical/witness bytes before staging. Stage both
application values under one existing AggregateActor SaveStateAsync boundary;
helpers never save. The supplied common serialized owner fence must serialize
all participating source/origin reads/writes and run exactly one original-token
decision to completion. Source and replay-operation owners are separate: their
readback is a recoverable sequence, not a cross-actor atomic transaction.

This initial issuer admits only a fully absent prior pair or an exact desired
already-complete pair (idempotent, zero staging/save). Any other existing pair,
including malformed/torn bytes or uncertain history after activation, holds
without replacement. Replacing an older valid snapshot requires a separately
qualified policy and is unavailable here.

After save or an ambiguous acknowledgement, clear the actor cache and read the
actual exact pair afresh. Exact desired pair proves the logical save; exact prior
pair proves no commit; missing/mixed/substituted/unavailable readback is
indeterminate. Neither acknowledgement nor a completed flag is sufficient.
Never delete stored bytes or repeat a save after indeterminate classification.
An unconfirmed cache discard also sets the actual AggregateActor's existing
unsafe-cache barrier, so ordinary subsequent actor work must confirm discard
before proceeding. Pending private ownership remains until logical reconciliation;
this does not qualify activation/destruction or production SDK retention.
Keep private staging buffers charged and owned while SDK aliases/cache or save
readback remain uncertain; release only after confirmed cache release/readback.
Original-token/capability/source/origin loss withholds uncommitted output while
preserving already committed truth. Reconciliation uses the existing independent
bounded durable-boundary policy; no production SDK/materialization guarantee is
inferred from local fixture arrays.

## Private initial-state adoption

Adopt only an admitted private snapshot candidate from the exact parent budget,
requested fixed source, current registry/reconstruction and originating token.
Recheck its actual pair/completed origin before and after every codec callback
and actual await. The private owner captures selection bytes/seeds/canonical
state under shared admission, expires read/writer leases before awaits, and
retains the candidate's actual-owner fence. Refusal clears full private capacities,
drops references and restores charges without changing caller/actor bytes.

Adoption cannot create an operation, advance a page pointer, execute a tail,
invoke a processor or disclose a command result. Anchor-aware page/ledger/terminal
integration and issuer-to-replay composition require their own behavioral,
retry/cancellation/save/readback and independent vector controls before admission.
Existing replay still starts at one until that integration is implemented.

## Qualification limits

Private codecs, actor fixture saves and detached initial ownership supply local
implementation evidence only. Authoritative domain/catalog/keys/profile,
qualified actual common-owner serialization, Dapr cache/materialization/component/
topology/fleet behavior and activation remain absent. Parent tasks/acceptance and
all O rows stay open. Unsupported composition, protection, anchored command or
checkpoint serving and production registration remain unavailable.
