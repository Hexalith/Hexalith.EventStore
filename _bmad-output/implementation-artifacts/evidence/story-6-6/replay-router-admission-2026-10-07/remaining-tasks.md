# Remaining M1–M8 work at the replay-router prerequisite

This is an implementation inventory under the approved
[map](../../../6-6-implementation-map.md), Dapr-only amendment and trusted-code
amendment. All eight parent tasks remain open. Existing local kernels and
Development/Testing evidence are partial prerequisites. Missing production
authority prevents activation; it does **not** prevent further dormant
implementation or source-derived declarations for repository-owned domains.
The unfinished local items below are implementation omissions, not claims that
an external gate makes them impossible to prepare.

## M1 — Registry, codecs and trusted execution

Present: additive metadata/contracts, bounded payload/scratch ownership,
canonical registry/codec kernels, supplied-graph checks, registered E/F callback
bindings, privately retained managed artifact images and local managed
observations. These do not establish complete startup readiness.

Local work remains: complete the immutable execution binding and observation
boundary for transitive, framework, native and dynamic paths under the current
trusted-code amendment; compose registry, validators, deserializers, adapters,
filters and handlers through one admitted service; extend refusal and
compatibility fixtures for that composition.

Activation depends on the complete reviewed D/V/A/E/F/S catalogs and dependency
closure for each serving domain, exact immutable artifact/loader-context
bindings and serving-peer pins. The partial supplied graphs cannot invent
missing domain declarations or qualify unobserved code. Complete reviewed
catalogs and deployment bindings were not supplied by this run.

## M2 — Producer, actor evidence and shared reader

Present: bounded V1 producer/parser kernels, streamed response admission,
same-save application digests and actor/outbox values, addressed Dapr logical
page/range readers, and six source-derived Counter writer declarations. Counter
Testing recovery evidence predates this run. Other domain producer routes retain
the approved compatibility fallback.

Local work remains: derive additional exact writer declarations from owning
application types/options where available; bind an admitted shared reader to
actual validators, deserializers and transforms; implement current-amendment
route/prefix proof codecs and writer negotiation. The existing
`DaprProductionLogicalEventReader.FromCallerPin` explicitly binds no upcasters
or schema/identity validators and reads only unversioned events. Streaming
response admission already exists; it is not an outstanding implementation gap.

Enabling the shared/versioned consumer route depends on M1's authoritative
catalog/artifact and peer binding, exact source/proof/key authority and qualified
Dapr capability. Per-domain payload aliases, formats, immutable serializer
options and real bounds must come from each owner; the Counter declarations do
not authorize another domain. Production qualification additionally needs the
ratified profile described under M8.

## M3 — Dapr control owner and recoverable transactions

Present: the SQL adapter and Story 6.6 Npgsql footprint are withdrawn. Existing
aggregate/event/snapshot/outbox/drain state stays under `AggregateActor`.

Local work remains: define the actual hold-producer participant sets, choose a
single Dapr owner for each new control, implement bounded keys/values,
generations/fences, idempotent intent and readback, and add concurrent-owner,
lost-acknowledgment, cancellation and restart tests. Cross-owner protocols must
be recoverable sequences. Dormant protocol and actor-owner preparation is
possible before production ratification.

Enabling any producer depends on its concrete atomic participant requirements
from M5–M7 and live qualification of the selected component/topology through
Dapr APIs. A participant set exceeding one supported actor save/transaction
requires redesign. No production component/topology or qualification exists in
this run, and no cross-actor atomicity may be inferred from current tests.

## M4 — Paged replay, command state and queries

Present: additive async replay/processing, bounded private legacy array and JSON
admission, independent replay capability selection, private page/session local
kernels and guarded legacy routes. This run fixes router ownership, dispatch and
cancellation. The private paged kernels remain unregistered prerequisites.

Local work remains: Dapr page-ledger durability, owner fences and CAS transitions,
pinned retry responses and terminal readback; private typed state serializer and
graph isolation; command-state proof codecs/intake; fixed-head reconstructor and
coordinated source/target integration; authenticated query intake and pinned
prior sessions with key/type/root/TTL checks and versioned cache bypass. A retained
earlier diagnostic demonstrates caller-owned typed snapshot mutation on Apply
failure; this run does not repair or qualify that path.

Admitting these routes depends on actual state/serializer declarations,
catalog/proof/key and serving-peer authority, plus M3's qualified owner protocol.
The available local kernels and DTOs cannot choose those application
declarations or grant authority. Dormant implementations and source-derived
tests remain possible while these inputs and production qualification are open.

## M5 — Verified projections and named generations

Present: legacy metadata/digest/address fences and existing named projection and
read-model protocols. The required `IVerifiedDomainProjectionHandler` and
`VerifiedProjectionRequest` are absent from current source.

Local work remains: add the prescribed verified contracts and full/incremental
dispatch; compose shared effective validation before fingerprints/selection;
implement application generation, Dapr logical bundle readback, one pinned prior
session and final visibility checks; prepare fenced named writes and the
versioned key-space guard with migration/readback fixtures. Existing non-versioned
batch/rebuild visibility must stay compatible.

Activation depends on M1/M2 admitted event and handler/model catalogs, M3's
qualified control owner, and an actual inventory/copy/readback of serving legacy
physical keys. The guard must not block a live legacy coordinator prematurely.
Catalog preparation and dormant migration tooling can proceed locally; a retained
data migration and serving cutover require their separate authority and evidence.

## M6 — Publication, subscriptions and effect receipts

Present: legacy version fences before publication/marker work and preservation of
original application evidence and additive event metadata. These are not the
complete transport/effective-view or effect-receipt protocol.

Local work remains: exact Binary/Structured carrier codecs and timestamp-offset
admission, whole-batch reservation and per-topic send authority, membership and
acceptance state, shared subscriber verification before marker/type/handler
selection, stable `EventEffectKey` and atomic effect/receipt/readback or a separate
explicit idempotency contract. Current source has no `EventEffectKey`.

Enabling these routes depends on admitted transport/route/effective catalogs,
M3 control ownership, the actual broker profile and demonstrable acceptance,
membership and no-future-acceptance controls. A separate effect store needs its
application idempotency contract. None follows from a successful legacy publish
or a Development Redis fixture; dormant carriers/receipts and injected-failure
tests remain possible locally.

## M7 — Resume, redrive, holds and safe diagnostics

Present: existing actor drain/recovery and operator/inspection routes, with
unavailable proof-dependent behavior fenced.

Local work remains: authorized same-event resume/preconditions, single-owner hold
inventory and scoped cursors, unresolved-only sends, logical capture/capsule before
cleanup, held-delivery redrive and per-item incidents; safe provenance, filter,
terminality, SDK/CLI/Admin ABI audit and isolated sandbox/export behavior. These
contracts, services and diagnostic fields can be prepared without enabling new
operator actions. This run makes no UI changes.

Action admission depends on purpose-2d authorization/audit policy and real
operator/tenant authority, the M3/M6 qualified safety route, broker disable/reject
controls and supported Dapr capture/retention capability. Deployment, publication
and retained-data migration keep their separate approval boundaries. Fixture
tokens grant no production authorization. Historical physical custody/provider
proof is unavailable through Dapr logical readback; dependent operations remain
disabled. Committed commands must never be executed again to resume publication.

## M8 — Compatibility, CI and qualification

Present: historical approval verification, a separate current-amendment audit
with all 20 obligations open, 22 timed current source controls, partial earlier
API/compiled-consumer and Development/Testing probes. This run adds five isolated
router mutations, three full affected assembly regressions, the required Release
build, 14 local packages and three current package-only consumers.

Local work remains: implement and qualify the unfinished paths above, add their
production-path/crash/cancellation matrices and meaningful timed guard controls,
complete API/wire/already-compiled consumer coverage for every shipped compatibility
claim, audit public terminality, and run the full affected acceptance set. The new
router mutation script is locally executable; it is not wired into CI in this run.
The existing sealed OQ8 source/workflow is preserved; its separately governed
reseal/change work is not silently folded into this prerequisite.

External qualification depends on the exact EventStore-ratified and
Platform-owned environment/provider inventory and deployment profile. The exact
check `test -f deploy/dapr/production-profile.yaml` returned exit 1 with no output:
the required profile is absent and AD-26 remains unratified. The configured
production component and a second supported component/harness, fleet and broker
matrices need their applicable live Dapr evidence. Development/Testing PostgreSQL
or Redis results cannot supply that production authority. Publication of an
incompatible set remains a separately authorized complete SemVer-major release.

No parent task or obligation is closed by this inventory. The next work includes
the local omissions above; the production-profile absence is a separate external
activation blocker, not a blanket reason to stop dormant preparation.
