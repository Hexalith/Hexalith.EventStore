# Dormant Dapr logical query intake model

Recorded before runtime changes on 2026-10-08 under the selected logical-model
authorization. This companion resolves local M4 choices; it supplies no production
catalog, principal grant, named-generation publication, key or activation authority.
The parent frozen intent and M1–M8/O-row dispositions remain unchanged.

The scope is private query intake through the existing DomainQueryDispatcher and
read-model interfaces, plus internal route classification before Server cache
access. Public QueryEnvelope, handler signatures and legacy constructor/wire
behavior remain unchanged. No query signature or derived-result membership proof
is introduced. Snapshot/checkpoint anchors remain refused.

## Catalog and execution

The compatible `5b` row is exactly B7c's fifteen-field row, including its ordered
binding bytes. Its keys obey the 1–64 ASCII-byte canonical grammar. Query route
hashes use the existing `HX-EV-QUERY-ROUTE-1\0`, version byte `01`, exact row,
selected input rows and selected G rows, each with their existing counts/framing.
No query bytes enter a legacy hash when no query rows exist. Independently encoded
known answers precede the production codec.

A supplied frozen route table classifies every known route as legacy or logical;
unknown classification refuses before ETag/cache/handler access. It is an internal
object, never caller data. It retains a shared observed-loss scope and current
capability fence. An absent table preserves historical legacy execution, and
cannot activate a logical query. The host must provide an authoritative complete
catalog and dependency closure before enabling this dormant seam.

An explicit local descriptor pins the exact scoped handler type, factory, request
validator, pure read-plan resolver, response validator and input materializers.
The descriptor owns exact row/options/schema/input-map bytes and immutable callable
identities; file-only bindings qualify local controls only. Readiness refuses
singleton/captive handler/store registration, missing mappings, direct-provider
dependencies, duplicate routes and inconsistent descriptor fields. An isolated
request scope installs its platform-owned holder before resolving the handler or
store. A captured/closed holder refuses, and caller scope arguments cannot widen it.

## Distinct actor-owned input root

This local reader admits a distinct `dapr-actor-logical-v1` query root through
IActorStateManager. It does not accept a historical provider certificate, infer a
production named-generation publication, or write/relabel existing projection
keys. Its explicit actor owner and root key are supplied by trusted composition.
A future named-generation producer must atomically publish this complete logical
root and its originating row witnesses in one actor save before serving integration
can be enabled. A root label alone is not originating-operation evidence.

The typed root contains model ID, tenant, domain, handler route, key-space, state
store, backend descriptor, generation, and up to 256 exact logical rows. Each row
contains key, originating logical-operation ValueTypeName, payload bytes (absent
for a proven absent/deleted row), UTC expiry if present, and origin operation ID.
The current actor pointer is the entire root; hashing its exact canonical image
binds every row and origin witness. This is complete logical readback, not a
provider signature or historical generation attestation.

Canonical root hash: SHA256 of `HX-EV-DAPR-LOGICAL-QUERY-ROOT-1\0`, u16 version 1,
U model, U tenant, U domain, U route, U key-space, U store, B backend, N generation,
u32 row count, then each ordinal UTF-8-key-sorted row: U key, U valueTypeName,
O(B payload), O(Q expiry), U originOperationId. All names are bounded strict UTF-8;
Optional fields use historical O framing: `00` absent and `02` followed by the
value. The typed local root has no explicit-null value; `01` is not admitted.
Duplicate/out-of-order keys, nonpositive generation, ambiguous absent witnesses,
missing origin, foreign model/address, unknown mapping and malformed bytes refuse.
Present rows require nonempty ValueTypeName; absent/deleted rows use the empty U
and absent payload/expiry. Each row also requires actual actor-owned origin readback
at `rootKey + ":origin:" + originOperationId + ":" + lowercaseHex(SHA256(Utf8(key)))`.
The distinct typed origin record binds operation ID, tenant, domain, store, exact
key, ValueTypeName, optional exact payload SHA-256 and optional expiry. Every field
must equal the root row and fixed address; absent/delete records have absent hash
and expiry. Missing/altered origin participants refuse before materialization and
again at final actual root readback, including unchanged root generations. This
is logical same-owner participant evidence, not a historical provider witness.
The full root must fit the configured bounded root capacity (at most 64 MiB).
No new signing purpose is allocated.

The session privately pins one full root and the complete exact plan before any
handler invocation. Multiple roots and more than 256 distinct keys refuse before
readback. Every plan key needs the separately supplied current principal-bound
exact-key grant. Per-read checks revalidate capability/grants/expiry and use only
the pinned root: when R2 replaces R1 between reads, both reads use R1 and the final
current-root check refuses the entire response. Opening execution and final
completion compare actual complete actor readback with the pinned root hash.
Authoritative UTC and authorization come from the supplied current owner fence;
expiry is checked for every row read, including unchanged generations.
Composition must supply an actual serialized actor-owner fence shared with every
root writer, and authoritative synchronous UTC under that fence. Missing either
refuses construction. Readback, current grants, fresh UTC and private-byte pins
form one decision inside that supplied fence; separate independent awaits are not
a common-fence capability. The local reader cannot certify an arbitrary supplied
fence implementation, so fixture serialization remains separate from live topology
and named-generation publication qualification.

## Callbacks, capacity and completion

The original token is checked before admission and in finally immediately after
every application callback, including throwing callbacks and foreign-token OCE.
Local descriptor/request/session pins are checked before and after actual owner
awaits. Payload leases expire before those awaits. Get/GetMany accept only exact
store/key/type mappings; writes, physical keys, scope override and closed/captured
session access refuse before source/provider access.
The internal request pin avoids serializing an unbounded/base64 body. Hash
`HX-EV-DAPR-LOGICAL-QUERY-REQUEST-1\0`, u16 version 1, U tenant/domain/aggregate/query,
B32 payload SHA-256, U correlation/user, O(U entity), one-byte admin flag,
O(paging: O(I pageSize), O(I offset), O(U cursor)), O(U originalActor),
O(U authenticatedWorkload), one-byte delegation flag, O(ordered scopes),
O(ordered audiences), O(U delegationId), O(U identityAdmissionProof).
Ordered lists are u32 count and each U; optional values use 00/02 as above.
Exact base envelopes only, at most 256 scope/audience entries and 64 KiB total
UTF-8 metadata are admitted before capture. The actor owner also pins this full
private request and exact query compatibility digest, and receives the current
private request when revalidating principal-bound exact grants. Foreign query,
tenant/domain or principal scope refuses before source lookup.

The live operation budget is at most 128 MiB. Admission reserves provider/root
overlap, retained root/mapping/plan evidence, declared materializer/aggregation
graphs, callback scratch and private encoded output before dependent allocation.
Input/proof metadata is at most 8 MiB, admitted row payloads at most 64 MiB in
total, and final encoded response at most 64 MiB. Repeated reads retain each
declared graph charge; no per-call budget reset occurs. All private byte capacities
are cleared before charge release on refusal/disposal. Final response bytes stay
private and charged through the actual final fence; only successful dispatch
transfers a detached response to transport. No streaming fragment, durable effect,
success checkpoint or cache entry is created.

The versioned cache policy is zero new entries/payload/proof bytes. Internal route
classification occurs before any cache lookup or ETag access.
The current historical cache exposes shared array aliases without an explicit
last-borrower owner. This local slice therefore refuses activation on an actor
containing any matching historical entry (case-insensitive route comparison),
before ETag/lookup/execution. It does not clear or release a borrowed legacy array.
Fresh actors have no matching entries and perform ordinary uncached logical
execution. Safe live eviction/ownership transition remains unfinished production
integration; its absence cannot be described as zero retained legacy bytes.
Legacy behavior remains on its existing path. Serving classification changes and
borrowed cache-buffer retention still require the separate production ownership
and activation qualification; no such qualification follows from local fixtures.

## Qualification boundary

Local controls must exercise actual dispatcher/DI/store and actor-state readback,
root drift, TTL and grant loss, exact type/origin/schema, private request and result
pins, cancellation/throwing callbacks, cleanup, cache bypass and ordinary legacy
behavior. Timed compiling mutations must fail on forbidden effects or disclosure.
Full workspace Release/package and affected root regressions remain separate
gates. Snapshot/rebase, named-generation producers, complete authoritative 52/59/G
catalog closure and production profile/live topology remain unfinished or external
authority dependencies; the parent remains in progress.

The local actual-owner scope pins the exact base query request digest, tenant, domain, canonical query discriminator, supplied principal and selected query compatibility hash before any actor lookup. Current authorization receives that complete private request and exact immutable plan; its callbacks are fenced against request substitution. The acquired input rejects a different request/catalog at descriptor admission. A supplied principal/authorization callback is local fixture evidence, not authenticated production authority. Planned logical keys use strict UTF-8, 1–512 bytes each, at most 256 keys, and at most 512 KiB of complete encoded plan metadata admitted before private array capture.

Local read-store results return a null ETag: originating operation identity remains internal evidence and is never relabelled as a concurrency token. No logical ETag schema is admitted here. The model was recorded before the initial runtime draft; independent vector generation occurred before focused qualification, not before every codec draft.

Private request capture copies both scope/audience reference arrays; closure clears those arrays and payload bytes before releasing its charge, while caller arrays remain unchanged. Caller-supplied bulk Count/indexer callbacks have individual original-token finally checks and actual owner authorization fences before any later getter or materializer.
