# Story 6.4 implementation continuation checkpoint

Status: **in progress, incomplete, unqualified, guard activation unavailable**.
Do not mark the story or any wrapper task complete from this checkpoint.

**Continuation 02 is recorded at the end of this file.** Its implementation,
validation and remaining-work entries supersede conflicting statements in the
earlier checkpoint below; the approved normative specification always controls.

The parent owns the rendered `bmad-build` workflow and this is its sequential
step-03 implementation handoff. Continue directly; do not rerender/restart the
workflow or spawn parallel implementers. The wrapper's frontmatter context was
loaded completely. The sole normative contract remains
`_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md`.
Do not edit its approved bytes.

## Preflight and workspace

- Owning repository: `/home/administrator/projects/hexalith/eventstore`.
- HEAD: `e17e7294f540ebba42fe25192998689f0ecd89c1`; branch `main`.
- Original user changes were the modified sprint-status file and untracked
  Story 6.4 wrapper. Both were preserved. No staging, commit, push, branch,
  submodule update, dependency change, cutover execution or activation occurred.
- Read the required root-declared AI.Tools baseline, project context, build
  configuration, formatting rules, source/rebuild preservation contracts and
  relevant architecture decisions. Do not load skills under `references/`.
- Normative SHA-256 verified:
  `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`.
- Detached approval-statement SHA-256 verified:
  `657f0dc25b18b826b37ab3be53273043fa98ca61a40923c9a913922816bf531f`.
- Baseline `6cd4e738ff259c6a6602650a8f49fd8373e49524` to HEAD is documentation-only.
- Aspire baseline startup succeeded with the repository-local orchestration
  skill: `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive`;
  `aspire wait eventstore --apphost ... --non-interactive --timeout 30`
  reported healthy. Resource state was inspected and that started AppHost was
  stopped successfully with `aspire stop --apphost ... --non-interactive`.
- SDK 10.0.401; Dapr CLI 1.18.2, **runtime 1.18.7**; Docker 29.8.2.
  `daprd --version` was not on PATH. The runtime differs from the pinned 1.18.2
  fixture lane and has not been qualified as that lane.
- C# changes were normalized to LF; `git diff --check` passed. Normalize again
  after subsequent edits because several original files had CRLF working bytes.

## Implemented source foundation

- `Events/SourceProof.cs`, `SourceProofCodec.cs`: exact approved source-v1 binary
  codec, scoped seed, stored envelope field order, timestamp offset, null/empty,
  ordinal extension order and duplicate rejection, contiguous scope validation.
  `ExtendVerified` reuses an already computed E(i) to avoid a duplicate source
  hash during guarded passes. `SourceEnvelopeResponseSize` streams exact compact
  JSON sizing with a bounded counter and conservative early oversize rejection.
- `AggregateMetadata.SourceProof` is additive and init-only. Existing constructors
  and deconstruction are retained. `EventPersister` creates an incarnation only
  for a new stream, extends every stored envelope after protection/application
  digest creation, and stages the final proof in the same metadata save as the
  envelopes. Existing nonempty streams with absent proof continue unqualified;
  invalid present proof fails closed. Only EventPersister reconstructs metadata.
- New actor methods `InspectSourceProofAsync`, `ReadEventsBoundedRangeAsync`,
  `BootstrapSourceProofAsync`; old signatures remain. Inspection retains the
  cache barrier and reads metadata only. Bounded range validates exact identity,
  floor/order, count <=256 and byte cap <=2 MiB, with at most one unreturned
  lookahead and no lookahead after the count/head bound. It preserves the old
  range implementation separately. FakeAggregateActor has explicit unsupported
  proof behavior and a bounded range, never an invented anchor.
- Bootstrap is a signed exact-scope/head/spec/operator/backup/quiescence/restore
  authorization with a five-minute lifetime, using the existing platform key
  provider with a separate HMAC domain. Actor bootstrap reads a bounded canonical
  full source, verifies readability without rewriting envelopes, rereads the
  identical metadata before one metadata save and handles ambiguous save via
  durable readback. An attributable GlobalAdministrator controller endpoint
  issues the capability. Empty sources stay Unknown and are not seeded by this
  bootstrap implementation; final empty-scope qualification remains to address.

## Implemented projection/SDK foundation

- Exact `ProjectionSourceGuard` key codec and strict JSON parser with the 15
  required properties, duplicate/unknown-property rejection, fixed versions,
  scope/incarnation/catalog/semantics/spec/full-commit/UTC checks and 4096-byte
  bound. The global writer marker class/serialization is unchanged.
  DaprProjectionDeliveryStateStore reads the **separate scope marker** on every
  row read, in addition to the existing global marker. No activation method exists.
- Optional `ProjectionSourceGuardCapability` is attached to ProjectionDispatchRoute
  and bound into catalog fingerprints only when present; absent-descriptor bytes
  have a pinned golden test. Metadata enrollment is explicit via
  DomainProjectionOwnerProofRegistration, not marker-interface inference.
  DomainService fingerprint recomputation, metadata ingestion and server catalog
  entries preserve the descriptors. `DomainProjectionCatalogRegistry.Authorizes`
  now compares route identity independently of descriptor record equality.
- Additive read-only SDK `IReadModelOwnerProofStore` implemented by
  `DaprReadModelStore.OwnerProof`: reads the existing selected model row and
  existing terminal batch receipt, at most two <=16KiB keys, no repair,
  compaction, handler call or model payload in the response. A model opts in with
  persisted `projectionOwner` and `projectionVersion`, and the **existing batch**
  explicitly declares `OwnerSelection`. The existing terminal receipt copies
  that metadata only after validation that it occurs in fingerprint-bound model
  operations. Default batches/receipts remain unchanged when absent.
- Authenticated SDK proof route is
  `/project/source-guard/owner-proof/v1`, enrolled in the existing workload route
  catalog. Server `DaprProjectionSelectedOwnerProofGateway` invokes it and
  rechecks the supplied lifecycle snapshot through the owning gateway, for the
  four-key proof ceiling. It does not use mutating shared status/repair methods.
- Schema2 support is additive via `ProjectionDeliveryState.GuardSchemaVersion=2`;
  the historical schema1 creation constant remains 1 for guard-disabled
  compatibility. Optional completion/version/pending fields and immutable source
  reservation bindings are added. Legacy admission/completion refuses guarded
  rows, preventing an accidental bypass. Guarded admission validates marker,
  scoped source/base/head/catalog/model generation and complete identity digests.
- `PrepareIdleBindingAsync` conditionally persists pending immutable model/head
  references while C and ActiveReservation remain unchanged. Ordinary release
  refuses pending work. Guarded final completion requires the exact pending
  fence/model/head binding and positive supplied Idle revision, then atomically
  advances the original C and records existing receipts/digests plus bindings.
  **The caller that proves actual post-release Idle and crash recovery still
  needs implementation.** Do not treat this primitive as finalization proof.
- Guard-specific complete-ID validation checks every canonical Base64 digest,
  uniqueness, canonical v1/v2 fingerprint bytes, and retained receipt ID/digest
  agreement. It never truncates proofs.
- `ProjectionSourceGuardDelivery` implements double-source current observation
  and EmptyNoWork/Unknown classification, with no history or mutation. It is
  wired before outbox/diagnostic checkpoint/history work in immediate/poller
  orchestration and retry. It checks the validated schema2 row, both markers,
  selected proof, actual bound Idle revision and exact source proof. Head change
  returns typed stale. Required routes share a consistent observed source.
  **C<H currently returns rebuild-required; application is not integrated.**
- `ProjectionSourceGuardHistoryReader` provides count/byte-bounded stored-source
  admission and page reads for later tail/application/verification integration.
  It validates every contiguous page before decryption, entire-tail readability,
  source-v1 chain/head, 10,000 total head ceiling, 64MiB source/wire ceilings,
  duplicate suffix IDs, frozen route fingerprints and immutable payload-free
  `(sequence, MessageId, source E(i), route E(i))` tuples. It retains no old prefix.
  Its caller still needs pre-effect matching, receipt recovery and shared passes.

## Validation observed

Commands were individual projects/assemblies in Debug/source mode, not solution
test. Retained logs are beside this checkpoint. Latest stable reported results:

- Server build: 0 warnings/errors; focused approved section-10 class set:
  **308 passed, 0 failed, 0 skipped**. Includes exact golden hashes/every raw
  field mutation, null/empty/version/offset cases, atomic append staging,
  legacy non-seeding, bootstrap tampering/ambiguous-save/idempotent readback,
  exact 8190/8191/8192 framing/lookahead, schema2 three-transition primitive,
  current double sample with zero history/write, plus existing preservation tests.
- Client batch/catalog subset: **33 passed, 0 failed, 0 skipped**.
  Covers both transaction/resumable existing receipt proof, read-only rejection
  of legacy unproven models, descriptor fingerprint binding and historical bytes.
  Client build succeeded with **10 existing NU1902/NU1903 warnings**, 0 errors,
  for centrally pinned `System.Security.Cryptography.Pkcs/Xml` 10.0.12.
  No dependencies were changed or auditing weakened. The zero-warning release
  gate is not proven and these warnings must remain explicit.
- DomainService focused section-10 compatibility/shared set:
  **104 passed, 0 failed, 0 skipped**; build 0 warnings/errors.
- W0-W6 production/live matrices, complete affected suites, Contracts suite,
  real sidecar fixtures and Release/package solution restore/build/suite gate
  have **not** been executed/qualified. Existing focused tests are preservation
  evidence, not completion or production qualification.

## Earlier remaining implementation sequence (superseded by Continuation 01)

1. Integrate frozen tail orchestration. Collect per-route plans from valid rows
   and base owner proof at C; admit the entire suffix with the new history reader
   before lifecycle/reservation/handler effects. Preserve outer head MessageId
   dispatch identity and explicit per-event identities. Share the application
   source pass among routes at one base: q admissions + 1 application + q
   verifications preserves the required three-route seven-event count.
2. Before EACH handler call compare its application reread against the immutable
   admitted sequence, MessageId, raw source E(i) and existing route fingerprint.
   Never send a page to an explicit single-event handler. Persist/claim existing
   retry work before effects, then acquire owned lifecycle tokens and CAS the
   exact frozen guard reservation. CAS conflicts must not reread source passes.
3. Add strictly read-only existing per-event receipt/progress proof operations.
   Same reservation resumes at proven K with original C/H/generation/dispatch/
   manifest identities; validate all C+1..K receipts, reconcile any original
   partial manifest, then apply K+1..H. Never fresh-admit C against a model at K,
   derive a new manifest from the advanced model, or leapfrog a pending head.
4. Add pending finalization recovery: same-work rightful/expired fence reclaim,
   acquire/release ONLY a recoverer's own lifecycle token, bind its token into
   pending CAS, prove the same H/model/receipt without handlers, release, read
   actual Idle L1, proof operation recheck L2, final conditional row completion,
   then row and Idle L3 readback. No predicted revision; bounded <=30-second
   independent finalization only for readback/fence completion/cleanup.
5. Every rebuild/erase/reconciliation writer must honor pending row fences before
   competing lifecycle/selection admission. No guard activation until these paths
   and their race windows are covered. Retain scope-guard records as admission
   evidence across scoped erasure, exactly as normative section 2.3 requires;
   an obsolete incarnation must not authorize a recreated stream. Preserve the
   unchanged global writer marker across erasure as well.
6. Add canonical staged fallback via existing stage/commit/verify/abort and
   promotion protocols, preserving all actor/detail/index surfaces, full-prefix
   semantics, empty/erased distinctions and existing wider streamed rebuilds.
   A failed/ambiguous incremental effect cannot immediately switch to replay.
7. Complete authenticated quiescent bootstrap/qualification/model hydration and
   persisted cutover-control attestations; first-write/CAS exact scope activation.
   Preserve C, receipts, complete ID digests, floor and fingerprint version when
   upgrading schema1. History alone cannot attest model completion. No live
   activation/cutover execution is authorized by the implementation task.
8. Cost instrumentation at production actor/store/handler/scheduler seams: actual
   RPC/key/returned/lookahead reads, serialization/decode/hash work, proof visits,
   all bytes, CAS/control/model mutations, batches/folds, retry N independent of
   saturated Attempt, 64-shard actual occupancy/bytes. Use bounded enums/tags,
   no IDs/keys/digests/provider messages or payloads in telemetry.
9. Add all named W0-W6/crash/race/compatibility/live fixtures; run every exact
   section-10 command and complete full affected regressions and Release/package
   gates. Retain exact sanitized results and source/diff identities; leave
   environmental failures explicitly unproven. Update the two delivery docs.

## Known gaps to address while integrating

- The optional selected-model proof currently supports one selected owner per
  existing batch receipt. Coordinated full-replay promotion can use projection
  scope `rebuild` and several route selections; it needs an additive binding to
  those existing receipts without another receipt ledger or changing v1/v2
  fingerprints. Shared epoch applied/selected proof needs its owning SDK seam;
  journal checkpoint/status is not completion. No automatic handler conversion.
- The current probe must require complete stable catalog route coverage before
  bypassing a coexisting legacy projection; legacy response-time type discovery
  cannot authorize current. Verify this in the integration tests.
- Owner proof gateway reports SDK read bytes, but lifecycle read-body bytes and
  transport retries still need actual instrumentation; do not claim complete
  measured cost from these result fields alone. The history reader also sizes
  returned pages, so charge that duplicate sizing work and actor lookahead.
- Proof-row S(H,R)/absolute byte limits and bounded actual read/write byte
  accounting still need enforcement at the Dapr store seam. Preserve all IDs.
- There is no scheduler cleanup lane for already-current outbox/ledger work yet;
  the current path intentionally does not delete/settle it.
- No test/status/task was marked complete. The repository has only reversible
  uncommitted implementation edits and evidence artifacts.

## Continuation 01: guarded single-event tails and finalization

Status remains **in progress, incomplete, unqualified; activation unavailable**.
The parent still owns the existing step-03 workflow. Continue sequentially;
do not restart the build workflow or launch parallel implementers. This
continuation verified the unchanged normative and detached approval digests.
HEAD remains `e17e7294f540ebba42fe25192998689f0ecd89c1`. No commit, branch,
push, dependency/submodule update, live cutover or activation was performed.
Original sprint-status/wrapper changes were preserved.

### Newly implemented paths

- `ProjectionSourceGuardTailDelivery.cs` integrates explicitly negotiated
  **single-event-v1** tails with C>0. Each route admits its entire readable
  suffix before lifecycle/reservation/handler effects. Application is shared
  among routes at the same source base; every application event matches the
  immutable admitted sequence, MessageId, raw source E(i) and route fingerprint
  before its own handler call. A handler receives exactly one event and the
  unchanged outer head-MessageId dispatch ID. A per-route verification pass
  precedes finalization. Existing v1/v2 route fingerprints are retained.
- Active work retains its original C/H, source/route prefix, owner generation,
  dispatch and manifest. Same-reservation recovery proves every original
  terminal per-event receipt through selected progress K, then skips the
  completed prefix and applies K+1..H. Missing/conflicting selected proof or
  receipts retain the reservation and require reconciliation. A newer observed
  source head does not replace the original frozen H.
- `IReadModelAppliedReceiptStore` and
  `DaprReadModelStore.AppliedReceipt.cs` read the **existing terminal batch
  receipt**, one <=16KiB metadata key, without model effects, repair or new
  manifests. `ProjectionAppliedReceiptRequest` carries the exact catalog scope,
  original event/batch ID, sequence and owner/generation. The authenticated SDK
  `/project/source-guard/applied-receipt/v1` route and
  `DaprProjectionSourceGuardTransport` use owning platform/Dapr seams and the
  existing dispatch request/response carrier. Current selected-model proof
  still uses its existing two-key SDK operation plus lifecycle recheck.
- `ProjectionSourceGuardFinalizer.cs` prepares immutable pending evidence,
  releases only the caller's owned lifecycle token, reads actual Idle L1,
  verifies the same model/receipt with the gateway's L2 check, CAS-completes the
  exact reservation, then reads the row and Idle L3. No revision is predicted.
  Ambiguous final CAS responses are resolved through persisted readback.
  `CompletionProven` distinguishes a completed checkpoint from Current; source
  growth or a later lifecycle revision reports `head_changed` without undoing
  proven completion or advancing to the newer head.
- `TryReclaimIdleBindingAsync` conditionally reclaims only the original pending
  work under the rightful/expired v2 rules, advances its fence, preserves its
  immutable references and binds the recoverer's own operation token. Pending
  recovery rereads the frozen source and selected receipt with **no handler
  calls**. Ordinary admission/release cannot redispatch pending finalization.
- Tail finalization, outer lifecycle cleanup and scheduler cleanup share one
  30-second independent deadline, using injected `TimeProvider`. Pending
  recovery and its scheduler cleanup share the same deadline as well. The
  deterministic elapsed-budget test spends 29 seconds in release and reaches
  the same deadline during cleanup at exactly 30 seconds; cleanup is canceled
  and original work is retained. No new deadline extends the independent work.
- `DaprProjectionLifecycleGateway` checks guarded active/pending delivery
  fences before competing rebuild/erase admission and again after acquiring
  their lifecycle. A race unwinds only that competitor's own token before any
  selection effects. Promotion also checks the delivery fence. The ordinary
  `ProjectionDeliveryReconciler` now refuses to reconstruct a guarded row as
  schema1 and drop its source/model/ID evidence; the dedicated authenticated
  qualification path still needs implementation.
- Source-guard enrollment is suppressed for a domain with coexisting legacy
  projection output, both when issuing metadata and when authorizing/rebuilding
  its catalog/proof operations. This prevents bypassing an uncatalogued legacy
  output. The original two-argument `BindMetadata` overload is retained.
- Guard digest-list validation now requires canonical Base64 bytes as well as
  uniqueness. New retry paths preserve saturated Attempt plus existing bounded
  exponential backoff, original dispatch/manifest and lifetime-pending work.
  No separate lifetime retry cap or parking policy was added.
- Scope-guard records and unchanged store-global writer markers remain retained
  across scoped erasure. There is still **no scope activation method**.

### Deterministic production fixtures

The new partial named-coordinator tests use the production guard, real
DomainService dispatcher, real `DaprReadModelStore` batch/owner/receipt protocol,
and serialized HTTP carriers over the existing recording Dapr client. The
Server test project links the existing Client test `RecordingDaprClient.cs`;
this is test-source reuse, not a package dependency. Actor/state/lifecycle
seams remain deterministic test doubles; these tests do **not** qualify live
Dapr provider behavior or the full cost vector.

- W1-shaped H=10000/C=9999 at P=1/256 reads exactly 3 returned suffix events for
  one route; the three-route fixture reads exactly 7. No sequence <=C is read.
- W2-shaped H=9771/C=9000 at P=256/1 reads exactly 2313 returned events and the
  expected 12/2313 range calls. Persisted selected detail/index semantics equal
  canonical `AggregateReplayer.Replay<CounterState>` through the same H.
- A lost event response resumes the same reservation and skips completed K;
  no repeated domain effects or new outer dispatch ID occur. A conflicting
  original receipt prevents every later effect and retains work/reservation.
- A changed application reread fails before that event's handler, preserving
  earlier independently completed effects with C unchanged.
- Growth during final source inspection completes only frozen H=4, reports
  Stale/head_changed at H=5, and later work applies only event 5.
- Nine failed activations retain pending work, original dispatch/manifest and
  fence lineage; Attempt saturates at 8 and backoff remains 128 seconds.
  No handler/model effects occur in this failure fixture. Actual production N
  telemetry and scheduler scan/shard measurements are still missing.
- Finalizer tests cover actual Idle binding, lost final CAS response, changed
  lifecycle after CAS, rightful/expired pending reclaim, no redispatch on
  pending recovery, and missing selected receipt. Competing rebuild/erase
  tests exercise both pre-admission and post-acquisition race windows.
- SDK receipt tests run both transaction-qualified and resumable profiles;
  they prove an original receipt remains readable after selected progress
  advances, and reject missing receipts/wrong generations without writes.

### Latest verification and retained evidence

All commands ran individually in Debug/source mode. No broad runtime gate is
claimed complete. Exact commands, exits and evidence filenames are in
`continuation-01-verification.md`; final logs and source/test-asset SHA-256
identities are retained alongside it.

- Server Debug/source build: **exit 0, 0 warnings, 0 errors**.
- Approved Server focused class set plus `DaprProjectionLifecycleGatewayTests`:
  **344 passed, 0 failed, 0 skipped**, 7.842 seconds. Includes all newly added
  tail, lifetime-pending and shared-deadline assertions.
- DomainService Debug/source build: **exit 0, 0 warnings, 0 errors**; approved
  focused compatibility/shared class set: **105 passed, 0 failed, 0 skipped**,
  92.795 seconds.
- Client Debug/source build: **exit 0, 0 errors, 10 existing NU1902/NU1903
  warnings** for pinned cryptography 10.0.12 packages. No dependency/audit
  settings changed. Batch/owner/applied-receipt subset: **21 passed, 0 failed,
  0 skipped**. This is not the full Client suite.
- Changed C# files normalized to LF; `git diff --check` passed. Normative
  spec/approval digests unchanged. No sidecars were started in this continuation.

### Current remaining implementation (blocking completion)

1. Add fresh C=0/new-owner initialization and **canonical staged fallback** for
   unsupported/full-replay-only handlers, using existing stage/commit/verify/
   abort and promotion protocols. Current fresh C=0 and unsupported tail modes
   safely return rebuild-required; the complete feature is not implemented.
   Never fallback after ambiguous incremental effects without same-work proof.
2. Implement owning mapped-append and shared-epoch applied/selected receipts
   and complete writer/owner coverage. Current generic batch receipt support is
   for explicit single-event-v1 only; mapped/shared tail modes remain unsupported.
   Coordinated full replay may have projection scope `rebuild` and multiple
   owner selections; bind those existing receipts additively, without a new
   receipt ledger or v1/v2 fingerprint changes.
3. Reconcile an original **partially committed per-event multi-key manifest**
   through the owning SDK before later events. Current proof readers reject
   unresolved envelopes/receipts and retain work; they do not repair it. Add
   all crash/cancellation/generation/lease-expiry races, not only terminal K
   recovery and pending finalization. Audit every competing writer and every
   section-6 boundary, including source growth during active partial recovery.
4. Complete authenticated schema1->2/source/model qualification, empty-scope
   maintenance behavior, persisted fleet/operator/backup/spec/quiescence/
   no-downgrade attestations and exact first-write/CAS scope activation beside
   the unchanged writer marker. No execution of cutover is authorized.
5. Implement a **separately charged cleanup lane** for already-current outbox/
   retry work. The pure current probe deliberately performs no cleanup. Audit
   mixed current/tail route work items and delayed old-head settlement; do not
   erase pending/reconciliation work merely because an attempt started.
6. Complete actual production cost instrumentation at actor/state/model/handler/
   scheduler seams: key/RPC/lookahead reads, repeated serialization/decode/hash
   work, actual bytes, digest/receipt visits, attempted writes/CAS, batches,
   lifecycle/retry/cold-start work, N independent of Attempt, and occupancy-
   dependent 64-shard reads/bytes. Enforce **S(H,R)** and absolute proof/control/
   manifest byte limits at owning Dapr seams. Existing reported fields/count
   assertions are insufficient for the complete quantitative contract.
7. Add the full W0-W6 matrix and all preservation/live fixtures, exact numeric
   boundary/one-over cases, R=256/4096 and R+1 horizons, 64x64/128 shard fixtures,
   A=8/32 races and per-mode semantic/protocol evidence. The deterministic
   W1/W2-shaped fixtures above are progress, not full workload qualification.
8. Update `docs/operations/projection-delivery-v2-cutover.md` and
   `docs/concepts/projection-delivery.md`. Execute every approved section-10
   command: full affected regressions, Contracts, real pinned Dapr/Redis fixtures,
   Release/package solution restore/build and Release suites, with exact skips,
   warning/error/process evidence. These broad/live/Release gates remain
   **unproven**. Local Dapr runtime 1.18.7 vs pinned 1.18.2 remains unqualified,
   and the existing Client advisory warnings prevent a zero-warning claim.

No wrapper task, acceptance criterion, story or sprint item was marked done.

## Continuation 02: qualification, proof byte limits and original partial manifests

Status remains **in progress, incomplete and unqualified**. No environment was
activated. The newly implemented activation API is not permission to execute
cutover. The parent owns step-03; continue sequentially without rerendering the
workflow or spawning parallel implementers. The approved normative contract
and approval digests remain unchanged and were independently reverified.

### Added maintenance and owning-store paths

- `ProjectionSourceGuardQualification.cs` validates an authenticated exact-scope
  maintenance inventory, canonical readable 1..H history, every existing retained
  receipt and complete ID digest through C, selected-model terminal receipt,
  freshness and actual Idle lifecycle revision. It hydrates schema2 at the SAME
  C without handler/model writes, preserving exact fingerprint version, receipt
  list, retained floor, digest list and migration provenance. Source is double
  sampled before save and sampled again after durable readback. CAS conflicts
  reuse the immutable admitted proof; they do not restart history.
- `ProjectionSourceGuardMaintenance` and `ProjectionSourceGuardWriterInventory`
  require authenticated operator, full canonical fleet revision, exact package
  inventory, backup/verified restore, approved digest, stopped traffic and
  no-downgrade acknowledgements. Every append, immediate, poller, retry,
  reconciliation, rebuild and erasure role must be inventoried as upgraded and
  quiesced. The existing per-scope reconciliation control record additively
  persists these attestations and the exact qualified scope marker.
- `DaprProjectionDeliveryStateStore.TryActivateSourceGuardAsync` atomically
  conditionally saves the unchanged qualified row, first-write/CAS scope marker
  and attributable control record in the existing component. Row+marker+audit
  readback resolves an ambiguous response. It never mutates the global writer
  marker. An obsolete-incarnation replacement requires the explicit previous
  incarnation and full qualification. Pending/active work or changed model/source
  proof prevents admission. Current qualification explicitly rejects incomplete
  runtime modes: fresh C=0, mapped/shared and full-replay fallback. C>0 explicit
  single-event mode is the only implemented qualification route.
- GlobalAdministrator maintenance actions are added under the existing admin
  controller at `/{tenantId}/source-guard/{domain}/{aggregateId}/{projectionName}`
  with separate `/qualify` and `/activate` POSTs. The server supplies the operator
  claim; request bodies cannot supply it. Empty maintenance observes source,
  lifecycle and model evidence without a write or empty receipt; it remains
  unqualified Unknown. Existing current EmptyNoWork now also requires Idle.
- Delivery state reads use the existing pinned raw Dapr byte-state seam, retain
  row/scope ETAGs, expose actual proof/marker read bytes and bound raw proof bodies
  before parsing. Guarded proof S(H,R), 4 MiB absolute, 4 KiB marker and 16 KiB
  control limits are enforced before owning-store mutations without pruning any
  receipt/digest. Longer permitted identifiers retain the absolute-cap path;
  the fixture's S(H,R) claim is not applied to larger identifier profiles.
  Ordinary proof saves serialize once and use byte-state first-write concurrency.
- Ordinary pre-guard v2 reconciliation now retains complete ordered SHA-256
  MessageId digests when hydrating its already-persisted canonical checkpoint.
  Guarded rows still cannot go through ordinary schema1 hydration.

### Original partial event recovery

- Explicit `OwnerSelection` batches now retain the SAME canonical immutable
  manifest in the EXISTING prepared/committed batch marker, along with its owner
  binding. Its existing v1 fingerprint bytes do not change. Guarded active
  control records fail above 16 KiB before effects, independently of the 1 MiB
  manifest ceiling. Default non-owner batch bytes remain unchanged. Completed
  terminal receipts discard the manifest and retain existing owner/receipt proof.
- `ReadModelBatchManifestCodec` restores only that canonical manifest: exact
  scope, event/batch identity, original operation order/types/values/concurrency
  ETAGs/TTL and fingerprint. It never recomputes a new manifest from advanced
  live state. Duplicate/corrupt/missing/conflicting material fails closed.
- A separate mutating SDK `IReadModelAppliedReceiptRecoveryStore` operation and
  authenticated `/project/source-guard/reconcile-applied-receipt/v1` route resume
  original Prepared/Committed work through `ReadModelBatchProtocol`, then read
  the original terminal receipt. Read-only owner/current/applied proof never
  invokes this operation. Its measured accessor counts actual recovery reads,
  read bytes, mutation attempts/bytes and RPCs; returned read costs include the
  protocol, not only its first marker lookup. Full metrics/export remain pending.
- Same-reservation tail recovery can start when current owner proof is unresolved
  by the original partial batch. After reclaiming the SAME frozen work/fence and
  owned lifecycle, it checks original receipts in sequence, reconciles a partial
  original event, rereads selected progress K, skips C+1..K effects and applies
  K+1..H. No later handler runs until original progress is proven. New-head work
  waits until this original frozen H finishes.
- Guarded committed-batch compaction refuses reclaiming a different newer live
  value/generation. It cannot blindly overwrite a replaced owner while recovering
  the old marker. Non-owner historical batch behavior is preserved.
- New deterministic production-path tests cover partial Prepared/Committed
  manifests, lost original response, exact original fingerprint/dispatch reuse,
  source growth during partial recovery, corrupt manifests, replaced generations,
  no repeated completed handler effects, and read-only proof doing no repair.
  They use actual DomainService dispatch and DaprReadModelStore over the existing
  recording byte-state seam; they do not qualify provider/live behavior.

### Verification and continuation status

Exact final commands/results and evidence identities are recorded in
`continuation-02-verification.md`. Builds and suites must now run one at a time;
the earlier broad runs overlapped and their failures are retained separately.
The full DomainService run exposed an additive endpoint metadata inference
regression; all three source-guard endpoints now explicitly mark the catalog
parameter `[FromServices]`. A process worker-admission test also failed and
requires an isolated check followed by a full sequential suite rerun.

The two delivery documents now describe the optional source/receipt/Idle
boundary, original manifest recovery, maintenance API and still-unqualified
runtime posture. No wrapper task, acceptance criterion or sprint item is done.

Final broad Debug/source regression results: Server **4,747 total, 0 failed,
25 existing DW1 ATDD skips** (739.331 seconds); Client **1,534 passed, no
failures/skips** (6.936 seconds); Contracts **2,339 total, no failures, 2
package-inventory skips** (348.072 seconds). DomainService initially reported
two failures; both passed in isolation after the endpoint binding fix, and the
sequential full rerun passed **893, no failures/skips** (122.826 seconds).
The original failures are retained, not overwritten. All broad logs were
audited/sanitized to runner outcomes and exact skip/failure reasons; synthetic
host warnings and stack traces are not projection telemetry evidence.

### Work still required before completion

1. Fresh C=0/new-owner initialization and canonical staged full-prefix fallback,
   using existing prepare/stage/commit/verify/abort/promotion protocols for every
   owned actor/detail/index/freshness surface. Never publish page-local replay
   or fall back after ambiguous effects. Preserve wider configured rebuilds.
2. Owning mapped-append and shared-epoch selected/applied receipts and complete
   writer/owner coverage; the generic owner batch protocol remains single-event.
   Bind coordinated full-replay `rebuild` batches/multiple owners additively.
3. Expand original-manifest recovery/finalization through every section-6 crash,
   cancellation, lease expiry, generation and competing-writer boundary. Current
   new tests cover important boundaries, not the entire required matrix.
   The current original manifest is inline in the existing active marker, so
   guarded batches whose combined control/manifest exceeds 16 KiB fail before
   effects; the approved larger-manifest cases (up to 1 MiB) still need an
   owning-protocol solution that preserves the separate control-row ceiling.
4. Finish empty/new-scope qualification and all runtime modes before allowing their
   activation. Keep all existing environments disabled. Rehearse qualified
   first-write/CAS activation and retained markers through real actor/Dapr seams.
5. A separately charged already-current outbox/retry cleanup lane, including mixed
   route and delayed old-head work. Current inspection intentionally does no cleanup.
6. Complete actual production cost instrumentation/export/assertions for source
   actor invocations/key reads/lookahead, serialization/decode/hash work, all
   proof/ID visits, lifecycle/model/batch bytes, CAS attempts, J/D, activation N
   independent of Attempt, and all 64 shard occupancy/pre/post-mutation sizes.
   Raw proof-byte limits and recovery accessor counters are only foundations.
   Audit propagation of typed proof-limit rejection from coordinator write paths;
   its generic TrySave helper still maps exceptions to exhausted state attempts.
7. Complete W0–W6 numeric exact/one-over, R/R+1 (256/4096), A=8/32,
   64x64/128 shard, all capability/compatibility and live persisted-state vectors.
8. Complete every approved section-10 affected/live and Release/package gate.
   Runtime 1.18.7 versus pinned 1.18.2 remains unqualified; Client's existing
   pinned crypto advisory warnings remain explicit. Neither dependencies nor
   audit policy was changed. Package-inventory skips and every broad test
   failure must remain unproven until their exact required lane passes.

No commit, branch, staging, push, dependency/submodule update, production cutover
or non-fixture scope activation occurred. Original user changes were preserved.
