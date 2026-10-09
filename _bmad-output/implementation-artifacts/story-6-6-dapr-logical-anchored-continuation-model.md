# Dapr logical anchored continuation model v1

This dormant local M4 continuation selects the matching consumers of the
separately qualified anchored preparation model. It leaves event-only
`dapr-actor-logical-v1`, its absent prefix tags 0d–0f, historical framing and
purpose-07 command admission unchanged. Command, checkpoint, rebase, timeline,
production registration and serving remain unavailable on this new route.
No snapshot/projection cost or protection redesign is authorized.

All separators include NUL. U/B/H/I/N/T retain the selected strict big-endian
primitives; H is exactly 32 unframed bytes. Optional hashes use the existing local
00 absent / 01 H present convention, without logical null. Encodings reject
unknown/missing/duplicate/reordered/trailing fields and unsupported model.
The exact anchor selection image, state and three seeds come only from the
private initial owner and its actual paired snapshot/completed-origin fence.
A caller image/hash/token cannot establish predecessor authority.

## Prefix and source response

Select `HX-EV-DAPR-ANCHORED-PREFIX-1\0 || 01 || 0011` with mandatory ascending
tags 01–11. Fields 01–0c retain the scalar logical prefix order: U tenant/domain/
aggregate ID/type; N start/end/head/target; I count; H ordered logical digest list;
H complete accumulator; H registry. Tag 0d is H exact selection image hash,
0e N positive covered sequence, 0f H exact initial canonical state hash,
10 H original requested-source hash, 11 U literal
`dapr-actor-logical-anchored-replay-v1`. Maximum claim is 8 KiB.

Require 1 <= covered <= target <= head. Every nonempty tail starts after covered,
has 1..256 contiguous entries and ends at/before target. The only zero-count form
has covered=target=head, end=covered and start=covered+1 below MAX, start=end=MAX
at MAX. Its ordered list is empty and its successor accumulator equals the
operation-owned seed. Below-head zero tails and timelines restart from one.

Current routes retain their exact logical route-1 separator/signatures and
application-byte identity. An explicitly selected separate anchored trust owner
pins the exact current route trust object, domain, registry, key ID/SPKI and
shared loss scope. Only that owner signs/verifies purpose 03 for the new prefix
separator/model. Ordinary route trust cannot verify the new prefix; anchored
trust cannot verify ordinary prefix bytes. No fixture grants production keys.

The additive response uses `HX-EV-DAPR-ANCHORED-PAGE-1\0 || 01`, followed by the
same bounded B proof-frame and u32 count/event scalar/payload order as page-1.
Whole-page route/prefix/payload/source verification precedes state callbacks.
Ordered list and accumulator-step preimages remain the selected source/registry
logical computations, extending the distinct private anchor seed; the new prefix
binds the selection/model and the actual ledger binds its predecessor.

## Actual operation, history and terminal readback

The actual dedicated replay actor selects this model explicitly with a private
initial owner, exact reconstruction binding and the same composed parent budget.
The initial owner remains retained and current throughout the operation. Its exact
originating operation token is retained unchanged through Begin, every page,
retry and takeover. Entry checks originating cancellation first, then refuses a
non-equal request token before argument validation, gate or cache work at Begin,
page execution and unsupported completed-origin/command capture entries. The
original token wins when both tokens are canceled; no callback or staging runs.
A live original token with a canceled foreign token yields the anchored hold,
not the foreign cancellation. This slice does not qualify cross-request token
retention, actor reactivation or operation restart. Use
separate application keys under `logical-replay:anchored:` for operation, state,
ledger, response and final participants; ordinary operation keys remain intact.
An anchor route refuses command-bound operation construction and command capture.

Initial same-save participants include exact selection bytes and canonical
state-0. The operation fixes selection hash, covered sequence, requested source,
registry/reconstruction and private seeds. Page ordinal zero has completed
sequence=covered, is incomplete even for head zero-tail, and retains the distinct
accumulator/effective/transcript genesis. It must commit its prescribed final
zero-count page before terminal disclosure. Takeover keeps selection/genesis and
all prior participants; it changes only the current positive owner generation.

Effective tail step uses `HX-EV-DAPR-ANCHORED-EFFECTIVE-STEP-1\0 || 01 ||`
H selection hash followed by the exact existing effective-step scalar order,
with its model U set to the anchored model. Transcript genesis uses
`HX-EV-DAPR-ANCHORED-TRANSCRIPT-GENESIS-1\0 || 01 || U tenant || U operation ||`
H selection hash || H covered transcript seed || H initial state hash.
Transcript step uses `HX-EV-DAPR-ANCHORED-TRANSCRIPT-STEP-1\0 || 01 ||`
U tenant || U operation || H selection hash || H prior transcript ||
B exact page transcript entry. The entry keeps the command model's existing
page-1 scalar order; all state/effective hashes are present and command proof
hash is absent. The transcript includes each actual persisted response image.

Anchored ledgers use `HX-EV-DAPR-ANCHORED-LEDGER-1\0 || 01 || H selection hash ||`
B exact complete ledger-2 scalar encoding. The inner record is only a bounded
participant preimage; it supplies no ordinary route authority. Request identity
uses a distinct anchored separator plus selection hash and the existing exact
tenant/operation/owner/generation/ordinal/request/max-count/source/registry order.
Participant readback binds the separate selection bytes and every initial,
ledger/response/state/final participant including next-ordinal absence.

Before callbacks freeze predecessor participants; after callbacks re-read exact
operation/history and compare that frozen digest before staging. Atomic page
save retains ledger, exact response, canonical successor, operation pointer and
terminal response/state when final. Independent fresh readback classifies exact
prior/expected/indeterminate without trusting current serving capability. Keep
uncertain private charges/cache aliases; retry returns exact pinned bytes and
never repeats Apply. Actual source, initial-origin and local pins surround every
application callback and actual await, with originating cancellation first. Decoded selection hash fields are slices
of the retained initial selection image, not independent hash arrays. Intake
references drop before initial disposal zeroes that full image and releases
its metadata capacity; borrowed current trust remains externally owned.

Malformed snapshot evidence selects full replay from one before anchored Begin.
Infrastructure/SDK materialization or arbitrary callback failures propagate.
Once an anchored operation commits, substitution/expiry refuses its continuation;
it does not silently change its selected history or restart a committed page.
Public detached terminal canonical bytes are released only after full actual
history, exact final participants and final currentness checks. No anchored
command proof or processor result is issued.

## Qualification boundary

Independent vectors, actual source/reconstruction/save/readback/retry controls
and compiling timed mutation guards qualify this dormant local composition only.
Production catalogs/domain/state/key/profile, immutable execution, real common
owner serialization, Dapr SDK/cache/component/topology/fleet and activation
remain separate missing evidence/authority. A common owner fence must serialize
all participating source/origin/operation reads and writes; separate actors are
a recoverable sequence, not a cross-actor transaction. Parent and O rows remain
open. Older-snapshot replacement, checkpoint/rebase and query transport work
remain unfinished local dependencies.
