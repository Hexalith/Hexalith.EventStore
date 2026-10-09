# Dormant logical snapshot replacement policy v1

This companion selects `dapr-actor-logical-snapshot-replacement-v1` only for the
explicit internal aggregate-actor replacement entry. It does not register a
serving route or authorize automatic replacement. The initial issuer continues
to refuse nonidentical existing pairs. Snapshot state and witness bytes retain
the already selected `dapr-actor-logical-snapshot-v1` schema and its independently
qualified vectors; this policy adds no encoding, separator, prefix tag or claim
purpose. Ordinary logical v1 and legacy snapshot keys remain unchanged.

Replacement requires a complete existing pair at the exact logical application
and witness keys. Its covered sequence must be positive and strictly less than
the desired completed origin's target. Tenant, domain, aggregate ID/type,
serializer, registry and reconstruction pins must match. Both origins must bind
the same fixed actor head, retained floor, source configuration and metadata
identity; only the target differs. A snapshot made against an earlier head or
different source configuration requires a separately qualified rebase and is
refused here. Absent, torn, malformed, mixed, equal nonidentical and newer pairs
are holds with zero staging/save. Exact desired bytes are independently
admissible after fresh desired-origin full-history proof, a fresh pinned
canonical read/write roundtrip and final exact pair readback, with zero stages
or saves. This admission does not require a superseded prior origin.

Prior witness decoding is strict. Its storage/folded hashes must equal the
private stored image. Its source hash is recomputed with the prior covered
target. A fresh actual completed-origin owner must prove the entire committed
chain and match every witness field and the exact stored bytes. The pinned
canonical codec must read and write those bytes identically. Evidence mismatch
refuses replacement; typed actor materialization, infrastructure and unrelated
application exceptions propagate. Originating cancellation takes precedence
over callback failures, including foreign-token cancellation exceptions.

All work runs inside the issuer's one supplied serialized owner decision. It
does not call candidate acquisition or nest another supplied owner fence. The
host must qualify that decision as a common serialization boundary across the
actual aggregate pair, addressed metadata and both actual completed-origin
owners, including their asynchronous readbacks. Reentrant writes, independently
serialized actors or merely sequential awaits do not satisfy this prerequisite;
unsupported production composition remains dormant/refused. Local fixtures
exercise this contract, without claiming actual SDK/cross-actor qualification.
Every Proven exit performs its final exact pair readback first, then a final
source and desired-origin fence (also prior-origin for replacement save or
pending recovery) before returning inside that decision. No subsequent pair
readback reopens the history-check window. The common serialized owner prevents
pair writes during those final fences; out-of-band physical writers do not
satisfy this contract. Controls inject valid ledger substitution during the
last readback and require zero Proven release in all three exit paths.

The desired origin and the prior origin stay privately owned through staging,
save and the call's final Proven serving fence. Private prior/desired bytes are sealed before
application callbacks. Codec payload/writer facades expire before an actual
await. After canonical admission and current-origin checks, the actor clears its
cache and reads the exact prior pair again before the first stage. A substituted
pair or private alias refuses before staging. Both origin fences and all local
pins are checked after each stage and before save. No cache clear discards a
partially staged pair between the two stages.

The same actual actor save contains both images. Independent bounded readback
classifies exact desired bytes as Proven, exact pinned prior bytes as NoCommit,
and any other state as Indeterminate. Save acknowledgement conveys no truth.
Indeterminate attempts retain charged cache/staging ownership, exact private
prior/desired images and a replacement-admission marker, and never repeat save
automatically. Their captured origin owners can be released without disclosing
Proven. Later Proven reconciliation freshly re-admits both actual completed
origins and the retained prior canonical roundtrip inside the same owner
decision before releasing an outcome. Durable classification and conclusive clearing remain independent of
current serving trust. If those serving checks fail after durable Proven, that
call withholds its result and conclusive cleanup releases the pending images
and marker. A later call can independently admit the current exact desired pair
through the full new completed history and canonical proof above. This is a new
current-pair admission, not release of the earlier failed call's result or reuse
of its lost prior proof. Controls cover both the third-call independent success
and third-call noncanonical refusal. Actor restart does not manufacture proof of an uncertain
attempt: any invalid or mixed current pair remains a hold. A fresh valid pair
can be considered only through the complete admission above.

One shared parent budget admits typed materialization, desired/prior/readback
images, strict decoding strings, completed-origin images and codec graph/copy
workspace before allocation. The existing conservative maximum is a configured
ceiling, not a promise that a 64 MiB image fits the 128 MiB composed budget.
Refusal restores the pre-call live charge after confirmed cache release; failed
cache release retains ownership and the ordinary actor cache barrier. Private
arrays are cleared and references dropped only after the SDK cache releases
them. Caller/durable images and legacy keys are never cleared or deleted.

This local slice does not close B3/M4: earlier-head rebase, ordinary activation,
cross-request/actor restart recovery, safe legacy buffer retirement, real SDK
paired-save ownership, fleet/provider qualification, production catalogs and
Platform authority remain separate required work. The next executable local
dependency is a distinct proof-qualified earlier-head snapshot rebase policy
and its actual owner composition; unsupported rebases continue to full replay
or hold as required by B3. All parent tasks and O rows stay open.
