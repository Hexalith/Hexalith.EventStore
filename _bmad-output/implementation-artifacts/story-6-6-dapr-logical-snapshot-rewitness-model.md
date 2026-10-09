# Dormant current-prefix snapshot re-witnessing policy v1

This distinct local policy selects
`dapr-actor-logical-current-prefix-rewitness-v1` for an explicit internal
AggregateActor entry. It implements the evidence-supported earlier-head
dependency through fresh full-prefix reconstruction, not historical-generation
authentication. AD-1/AD-13, the Dapr-only amendment, selected logical model,
historical §6/B3 and the parent Code Map govern. No serving registration,
snapshot cost redesign, data migration, production profile or key authority is
supplied. The preceding replacement packet remains intact. A new explicitly
scoped runtime revision adds immutable pending policy identity to the shared
paired-write owner and entry guards; established policy behavior is preserved.

## Historical limit and exact scope

The addressed reader requires current metadata head, floor, ETag and time to
equal its fixed binding. Once the head advances, the old completed operation's
source fence cannot succeed. Dapr current readback supplies no authenticated
historical generation. `dapr-actor-logical-snapshot-rebase-v1` remains untrusted
candidate framing; this policy cannot consume it or authenticate its prior
witness historically. Historical rebase retains a hold until an actual
historical-source owner/equivalence protocol is separately selected and
qualified. Changing ActorHead on an old binding supplies no such evidence.

The supported operation instead obtains a fresh actual completed event-only
replay under the current fixed head H, targeted at the existing covered
sequence S. It must reconstruct the complete retained prefix 1..S under that
binding, with canonical state, registry, reconstruction, effective-chain,
transcript and every durable participant freshly verified. The observed prior
binding describes a declared older head K with 0<S<=K<H; both targets are S.
It is only a bounded hint whose hash must match the exact observed witness.
Its head/ETag/time and old origin identity acquire no historical authority.

Both bindings require floor=1, present metadata and identical application,
namespace, actor type, canonical actor/tenant/domain/aggregate/type, configuration
hash and key/digest mapping. Only head, metadata ETag/time and the resulting
source hash may differ. Registry, reconstruction and serializer must remain
exactly equal. Protection is plaintext canonical only. Pruned prefixes,
configuration/scope changes, registry/state-schema changes, historical proof
requests and any unsupported composition hold; no retention bridge is invented.

Strictly decode the complete existing snapshot witness, bound its exact keys,
coverage, source hint, state/storage hashes, registry/reconstruction/serializer
and model, then freeze both complete private images. Torn, missing, malformed,
mixed or mismatched pairs remain intact and produce zero stages/save. Full
current-origin canonical read/write must succeed and its exact state bytes must
equal the privately observed prior state. A matching hash or free typed state is
insufficient. This proves the successor's value under current history. It does
not prove the old witness's claimed operation, generation or earlier history.
No old-origin factory is invoked or bypassed under a historical claim.

## Actual owner composition

All work occurs in one supplied serialized owner decision covering the actual
aggregate pair, current addressed source and fresh completed-origin owner.
Exactly one original-token decision must finish before the fence returns.
Independent awaits, nested fences, reentrant/out-of-band writes and separate
unqualified actor serialization cannot substitute for that common owner;
unsupported production composition remains dormant/refused.

Capture a new standard `dapr-actor-logical-snapshot-v1` witness solely from the
fresh current origin, at the same S, preserving the state value. The old witness
is replaced only after full current-prefix proof, canonical equivalence and
fresh exact prior-pair readback. Stage state and new witness in one actual
AggregateActor save. Recheck current source/origin and all private pins after
every callback/await/stage and before save. Every Proven exit finishes exact
pair readback, then current source/origin fences, inside that common decision.
No later pair readback reopens the history window.

The charged paired-write owner fixes the exact immutable initial, replacement
or re-witness policy at construction. Every entry refuses a different pending
policy before actual classification or serving, preserving its owner and charge.
The existing pending slot retains exact desired
and observed-prior images across uncertainty. Independent bounded readback
classifies exact desired as Proven, exact observed prior as NoCommit, otherwise
Indeterminate; acknowledgement never proves truth. No pending retry repeats
save. Proven reconciliation freshly proves the desired current completed
history and canonical state. It needs no historical prior authority because
this policy never asserted it. A fresh exact-desired call independently proves
that current pair with zero stages/save. Existing initial/replacement entry
behavior and its independently scoped recovery obligations remain unchanged.

One shared 128 MiB parent admits materialization, old/new/readback images,
strict decoding strings, hashes, origin images and codec graph/copy workspace
before allocation. Configured maxima are ceilings, not admission guarantees.
Borrowed facades expire before addressed awaits. Original cancellation wins
over callback failures and foreign cancellation; foreign callback tokens refuse
before addressed work. Private arrays are cleared and references dropped only
after SDK cache release; failed release retains ownership and the ordinary
actor barrier. Durable/caller/legacy images are never cleared or deleted.

## Serving and qualification limits

The wire snapshot schema/keys and its six independent vectors are reused
unchanged; this policy adds no claim separator, signature purpose or rebase
carrier. Ordinary v1 tags 0d–0f remain absent. A re-witnessed S below current H
does not permit zero-tail replay when target=S, and timelines always start at
1. Candidate admission still enforces those B3 rules. There is no automatic
fallback that fabricates historical proof or skips unreadable retained events.

Local real-owner controls qualify current-prefix equivalence, actual paired
save/readback, cancellation, private/actual substitution, budget clearing,
uncertain-save recovery and compiling behavioral mutations. Real SDK/common
cross-actor serialization, restart/token retention and fleet/profile authority
remain separate evidence. Historical rebase stays unavailable. Checkpoint
owner/readback, anchored completed-command/origin schemas, query transport and
the other parent M4/M5 integrations remain executable local dependencies where
their own evidence permits. Parent acceptance, M1–M8 and every O row remain open.
