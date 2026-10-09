# Dapr logical completed command-state model v1

This companion records the remaining local command-state choices authorized by
the selected-model amendment and the implementation Code Map. It extends dormant
operation preparation. The selected source/route/prefix preimages and historical
provider-purpose-07 records remain unchanged. No catalog, key issuance, serving
profile, production or activation authority follows from this model.

All separators include their final NUL. Primitives retain the selected model's
strict U/B/B32/I/N/T and O(X) meanings. SHA256 covers the complete preimage.
SourceHash and Registry are the operation's fixed source/current registry hashes;
Binding is the optional exact reconstruction fingerprint. Every digest is B32.
Existing complete-page framing and signature verification precede commitment
calculation. An unverified caller hash cannot supply predecessor authority.

## Exact command route

CommandHash is SHA256 of `HX-EV-DAPR-COMMAND-ROUTE-1\0 || 01 ||`:

U MessageId, U tenant, U domain, U aggregate ID, U command type,
N command payload byte length, B32 SHA256(exact command payload), U correlation ID,
O(U) causation ID, U user ID, O(extensions).

Present extensions use u32 count and strict U key/U value pairs sorted by unsigned
UTF-8 key bytes, with duplicate exact keys refused. Null and empty maps differ.
The metadata preimage has a 512 KiB ceiling and at most 65,536 pairs. The dormant
operation owner privately captures a command of at most 16 MiB under its shared
budget. It fixes CommandHash when created; later request state, command payload,
metadata or route substitutions refuse. The actual source separately binds the
aggregate type and owning application/namespace/actor/key mapping.

## Cumulative effective-event chain

`C0 = SHA256("HX-EV-DAPR-EFFECTIVE-GENESIS-1\0" || 01 || B32 SourceHash ||
U model ID || B32 Registry || O(B32) Binding)`.

Each already verified route/image extends the preceding chain:

`Ci = SHA256("HX-EV-DAPR-EFFECTIVE-STEP-1\0" || 01 || B32 SourceHash ||
U model ID || B32 Registry || O(B32) Binding || B32 C(i-1) || N sequence ||
B32 SHA256(exact logical route claim) || U canonical current type ||
I current payload version || U effective format || B32 effective payload SHA256)`.

The sequence comes from the actual admitted contiguous response and operation
ledger, starting at one. Empty-target completion retains C0. The route claim hash
binds the complete signed source/metadata/evolution identity; the payload hash
binds the actual privately admitted current image. No event or stored digest is
rewritten.

## Exact page transcript

`T0 = SHA256("HX-EV-DAPR-TRANSCRIPT-GENESIS-1\0" || 01 || U tenant ||
U operation ID || B32 SourceHash || U model ID || B32 Registry ||
O(B32) Binding || O(B32) initial canonical state hash || O(B32) CommandHash)`.

The exact page entry is `HX-EV-DAPR-PAGE-TRANSCRIPT-1\0 || 01 ||`:

N page ordinal, N generation, B32 request hash, B32 prior logical accumulator,
B32 successor logical accumulator, N start sequence, N end sequence, I count,
B32 exact pinned response hash, byte final discriminator (00/01),
O(B32) prior canonical state hash, O(B32) successor canonical state hash,
B32 prior effective chain, B32 successor effective chain.

`Tp = SHA256("HX-EV-DAPR-TRANSCRIPT-STEP-1\0" || 01 || U tenant ||
U operation ID || B32 SourceHash || U model ID || B32 Registry ||
O(B32) Binding || B32 T(p-1) || B(exact page entry))`.

The transcript includes the exact persisted response (including its signature
bytes), not a regenerated DTO. It includes every committed page from genesis,
including the prescribed count-zero final page. Each source-only or reconstruction
operation records these commitments. Takeover retains the genesis and prior pages;
each subsequent page records its admitted generation and exact request hash.

New ledgers with these commitments use `HX-EV-DAPR-REPLAY-LEDGER-2\0`, codec 01,
the existing scalar ledger order, then B32 prior/successor effective chains,
B32 prior/successor transcripts and O(B32) terminal command-proof hash. The old
ledger-1 encoding remains available for the unchanged historical vectors only;
new operation admission requires the complete new commitment fields. The page
transcript excludes transcript fields and terminal command-proof hash to avoid
self-referential hashing. The proof itself binds the terminal transcript, while
its separate retained hash and exact participant readback bind its bytes.

## Completed purpose-07 claim and proof

Claim separator `HX-EV-DAPR-COMMAND-STATE-1\0`, codec 01, count 001b. Tags 01..1b
are mandatory, ascending and unique:

| Tag | Primitive and meaning |
| --- | --- |
| 01 | U literal `dapr-actor-logical-v1` |
| 02..07 | U tenant, domain, aggregate ID, aggregate type, command type, MessageId |
| 08 | B32 exact CommandHash |
| 09 | B32 SourceHash |
| 0a..0b | N immutable actor head, N immutable target |
| 0c..0d | B32 current Registry, B32 exact reconstruction Binding |
| 0e..0f | U stable operation ID, U terminal owner ID |
| 10..12 | N terminal generation, N page ordinal, N completed sequence |
| 13 | B32 complete logical accumulator |
| 14 | B32 SHA256(exact final prefix claim) |
| 15 | B32 SHA256(exact final source proof frame) |
| 16 | B32 SHA256(exact final pinned response) |
| 17 | B32 SHA256(exact final canonical state) |
| 18..19 | B32 terminal effective chain, B32 terminal transcript |
| 1a..1b | T issued-at, T expires-at |

Completed sequence equals target. Generation is positive and page ordinal is
1..65,536; target zero has ordinal one, otherwise ordinal cannot exceed target.
Validity begins at signing under current logical trust and expires no later than
five minutes afterward or that current key's expiry, whichever is earlier.
Current trust validates both times and checks expiry on every verification/use.
The claim ceiling is 1 MiB. Reject historical separators, wrong model, fields,
tags, sizes, times, trailing bytes and inconsistent terminal forms.

Sign purpose 07 using the existing exact `HX-EV-SIG-1` input and the explicitly
supplied current logical-model domain/key/SPKI/registry trust object. This does
not authorize an old provider-purpose-07 key or claim. Proof framing is separately
identified: `HX-EV-DAPR-COMMAND-PROOF-1\0 || 01 || B(claim) || U(key ID) ||
B(64-byte P1363 signature)`, with a 2 MiB ceiling. Signature and all current scope
checks precede private completed-state admission.

## Actual owner and router admission

A command-bound operation fixes the private exact command route and reconstruction
binding before Begin. Its terminal save includes the ledger, pinned source response,
canonical state, final response/state, distinct command proof and operation pointer.
The owner compares every participant through actual addressed actor readback,
including all prior pages and next-ordinal absence. Ambiguous save retains private
charges and exact expected participant digests until Proven/NoCommit/Indeterminate
readback; cancellation or current source/trust loss may withhold authority without
changing proven committed truth. Exact retry reads the same retained proof bytes.

Explicit dormant router intake accepts a privately verified completed-state owner,
not caller-supplied state/hash/carrier substitutions. It binds the exact command,
complete operation commitments, fixed source/current trust and reconstruction
binding, re-reading actual participants before/after state decode and application
callbacks. The state reader receives a scoped payload lease that expires before
the next asynchronous fence. Only a detached exact supplied state type reaches
synchronous or asynchronous processors. Original cancellation is checked first
after callbacks, including throwing callbacks. Private proof/state buffers and
declared graph reservations remain owned through dispatch and clear on disposal.
Each admission stage is followed immediately by a pinned-codec canonical state
comparison; a stage's mutation cannot reach another stage or processor. The
processor-visible private command is rehashed both before and after each actual
owner await, including an actually yielding source readback.

Logical result dispatch requires the explicit bounded producer even for no-op.
Caller-supplied result Count/indexer, virtual result payload and serialized-event
metadata/byte getters each have their own original-token and actual-owner fence.
The produced private wire owner retains its payload reservation through the final
asynchronous owner fence; refusal clears all full payload arrays before release.
Successful dispatch transfers the detached wire bytes at that boundary. The public
legacy fallback remains available through its existing route.

Both the retained operation command and private invocation command reserve their
actual payload length plus 4 MiB for their separate copied dictionaries and
retained metadata. Command hashing separately admits 4 MiB before its metadata
writer and sorting workspace. The invocation partition includes the actual command
payload length, 12 MiB fixed headroom, declared working graph bytes, fourteen times
the state bound, four times canonical state length and twelve times proof length.
The parent operation charges that whole partition before allocation; the two
private command copies remain simultaneous charges. The reviewed graph declaration
must cover application state/processor/result graphs throughout invocation; wire
payload reservations are separate. A 16 MiB command fits a sufficiently large
parent budget and refuses before decode when the remaining parent capacity cannot
admit the partition. Both command owners clear payload capacity, clear extension
references and drop their command ownership before releasing the reservations.
Initial canonical bytes and staging reservations also clear if C0/T0 admission
refuses. Synchronous create/read/write/Apply callbacks preserve original-token
cancellation, including a callback throwing an OCE with another token.

The public legacy router and historical `CommandStateProof` carrier retain their
existing refusals. No new service/actor registration or serving capability is
enabled. The independent Python generator records fourteen exact new preimages
and hashes separately from the selected source/route/prefix and historical approval
inputs. Production codec/commitment/ledger tests compare all fourteen vectors;
the scoped verification packet records executed controls, mutation kills and their
actual input seals.
