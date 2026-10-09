# Dapr logical snapshot candidate model v1

This distinct, dormant model selects local schema choices under Story 6.6. It
supplies no production key, catalog, profile, provider history, activation or
snapshot cost qualification. `dapr-actor-logical-v1` route/prefix decoding and
its absent tags 0d–0f remain unchanged. No snapshot candidate is a continuation,
command-state proof or committed replay progress.

## Exact framing

U is u32 strict UTF-8 bytes; B is u32 exact bytes; H is exactly 32 unframed bytes;
N is nonnegative signed big-endian i64. Every separator includes its final NUL.
Each record uses byte 01, u16 field count and strictly ascending one-byte tags.
Unknown, missing, reordered or duplicate fields, trailing bytes, negative N,
invalid hash widths and over-limit text refuse before state callbacks. Records
are at most 64 KiB and each U is at most 512 UTF-8 bytes.

The snapshot record is `HX-EV-DAPR-SNAPSHOT-WITNESS-1\0 || 01 || 0014`:
01 U tenant; 02 U domain; 03 U aggregate ID; 04 U aggregate type;
05 H source binding hash; 06 N positive covered sequence;
07 U exact storage key; 08 U exact paired witness key;
09 H exact storage bytes hash; 0a H readable folded bytes hash;
0b H current registry; 0c H reconstruction binding; 0d H covered accumulator;
0e U completed replay operation ID; 0f N positive owner generation;
10 H completed page transcript; 11 H completed effective chain;
12 U protection codec exactly `plaintext-canonical-v1`;
13 U model exactly `dapr-actor-logical-snapshot-v1`; 14 U exact state serializer ID.

The source hash is the unchanged v1 source-binding codec, fixing the actual head,
metadata presence/ETag/time/configuration and target **covered sequence**. Candidate
admission compares it with the requested fixed source with only target replaced
by that covered sequence. A head/metadata change therefore requires fresh full
replay/re-witnessing; equality of folded bytes cannot rebind it. The completed
origin must target exactly the covered sequence and match every accumulator,
registry, reconstruction, transcript, effective-chain and state hash. Current
readback is independently verified through the actual dedicated operation owner,
including every ledger/response/state/final participant and next-ordinal absence.
This distinct model authenticates current application state by actual owner
readback and completed replay verification. It adds no signature carrier or
purpose-02/04 authority; historical signed snapshot/checkpoint schemas are not
reinterpreted. The returned origin privately owns canonical bytes and rechecks that exact owner
before every use. Caller-created witness DTOs and hashes cannot create an origin.

The separate checkpoint candidate record is
`HX-EV-DAPR-CHECKPOINT-WITNESS-1\0 || 01 || 0009`:
01 U tenant; 02 U domain; 03 U projection; 04 H exact key-space scope;
05 N nonnegative covered sequence; 06 H covered accumulator;
07 H current registry; 08 H actual named root; 09 U model exactly
`dapr-actor-logical-checkpoint-v1`.

The separate rebase candidate record is
`HX-EV-DAPR-SNAPSHOT-REBASE-1\0 || 01 || 0008`:
01 H prior exact witness; 02 H successor exact witness; 03 H prior source binding;
04 H successor source binding; 05 H prior registry; 06 H successor registry;
07 H identical canonical folded state; 08 U model exactly
`dapr-actor-logical-snapshot-rebase-v1`.
These latter codecs supply exact bytes/vectors only. No checkpoint/rebase owner,
issuer, equivalence proof or serving intake is supplied. Aggregate snapshot
admission cannot consume either separator.

## Actual owner intake and fallback

An explicit trusted-host constructor selects the snapshot model and fixes the
actual owning IActorStateManager, source, supplied reconstruction, serializer,
key mapping and serialized owner fence. It reads only the separate application
keys `SnapshotKey + ":logical-v1"` and its `:evolution-witness` pair, preserving
legacy snapshot storage. Both application values are byte arrays in this schema.
Only plaintext canonical bytes are supported. Protected or legacy objects remain
untouched and require a separately qualified adapter; they supply no candidate.

The serialized fence must invoke its supplied decision exactly once, with the
originating token, and await completion before returning. It must serialize all
participating source/operation owner reads/writes for this decision; independent async reads or a
trusted current predicate alone do not establish that property. Production
composition must separately qualify this prerequisite and Dapr SDK materialization.
Local tests exercise an explicit serialized fixture, not a live actor topology.

Admit all retained/copy/decode/callback graph work in the existing composed
128 MiB budget before allocation/callback. The actual completed-origin factory
receives that exact parent budget; origins from another budget or command-bound
operations with separately retained command ownership refuse. Accepted configured
maxima are ceilings, not guaranteed admissible sizes: the conservative paired
read workspace is `2 * maximumStateBytes + 4 * 64 KiB + 4096`. A configured
64 MiB maximum therefore refuses under the 128 MiB composed budget before pair
read/copy; it does not claim a 64 MiB candidate fits. Capture private storage/witness bytes,
compare their hashes and strict fields, authenticate the actual completed origin,
and canonical-roundtrip through the exact supplied codec. Leases/writers expire
before an actual await. Original-token checks in finally take precedence even
when a callback throws a foreign cancellation. Compare private pins after awaits,
then re-read the exact pair under the same supplied owner decision before private
handoff. Refusal clears full private capacities, drops owned references and
restores reservations. Both owning-return APIs retain their transferred owner
through a real final await and the last originating-token check: the candidate
is constructed inside the serialized decision before the fence returns; the
completed origin is constructed before a final no-lock verification of its
actual history under the operation gate. Any intervening failure disposes that
owner, and originating cancellation clears it before the API can return.
The intermediate private pair-return tuple likewise keeps both copied owners
through its final token check and disposes both if that check refuses.
Borrowed actor/caller bytes are never zeroed.

Missing, stale, free, mismatched or malformed **privately captured witness
records**, unsupported protection and typed canonical-roundtrip evidence mismatch
return full replay from sequence 1, with no candidate state/tail or Apply. A Dapr
`TryGetStateAsync<byte[]>` materialization `JsonException` propagates as an
infrastructure failure; an application callback `JsonException`/`IOException`
also propagates. This path cannot call unmaterialized actor bytes a malformed
private candidate or claim absence from a deserialization failure. Stored
bytes remain intact: this path never calls RemoveStateAsync, SetStateAsync or
SaveStateAsync and never calls SnapshotManager.LoadSnapshotAsync. Cancellation
propagates; infrastructure errors propagate rather than claiming absence.

Always refuse snapshot reuse for timelines, covered sequence above target, or
covered=target>0 with actual head greater than target. A nonzero snapshot at the
actual head may propose a zero tail: start=k+1/end=k for k<MAX, start=end=MAX for
MAX, never increment MAX. A lower eligible snapshot proposes start=k+1 through
the original target under the original actual head. Snapshot sequence zero is
unsupported. These are private candidate plans, not signed prefixes or replay
success. Invalid evidence discards the entire candidate before requesting page 1.

## Remaining work

No application snapshot pair staging/single-save issuance, protected v1 adapter,
anchor-aware prefix/ledger/transcript selection, canonical initial-state adoption,
checkpoint owner, actual rebase equivalence/re-witnessing, public endpoint or
production registration is implemented here. Existing replay still starts at 1.
This slice qualifies a local candidate dependency without claiming tail-cost,
transport lifetime, atomic cross-owner storage or historical-provider assurance.
