# Story 6.4: control, measured costs, query proof and live source handoff

**In progress, incomplete and unqualified. No environment was admitted or cut over.**
Continue the existing parent-owned sequential step-03 implementation. The receiving
implementation agent is the **sole active implementer**; the parent is not editing
runtime files. Do not restart planning, spawn parallel implementers or repeat an
ownership clarification. The approved normative specification controls every
disagreement. No task or acceptance criterion is complete.

**Immediate next-step priority:** remove duplicate Server source sizing without
weakening B+Blook; account the already-read oversized-event decode/sizing bytes
on early rejection; independently enforce the 4096-byte source-inspection body
cap. Then finish pinned live W0-W6, the remaining compatibility/crash/cost matrix
and the final authorized gates. The successor is the sole active implementer.

## Implemented and checked

- Scheduled activation cleanup can complete only its sampled outbox revision after
  a separately proven current observation. Retry cleanup additionally requires the
  exact original head MessageId/catalog and a claimed durable revision/lease. The
  ordinary current probe still performs zero writes or cleanup.
- Private guarded replay preparation has a conditional cleanup lane at the
  authenticated SDK `/project/rebuild/reclaim-guarded/v1` route. It verifies the
  original selected owners/generations and existing terminal receipt fingerprint,
  then conditionally deletes only the private manifest/reference. A failed cleanup
  retains original retry work. A lost first delete can resume using the retained
  reference and independently proven terminal selection; no new preparation or
  receipt protocol is used.
- An explicit single-event rebuild-required result before effects can choose one
  canonical fallback. Existing original per-event recovery must first prove
  unchanged owner progress. An exact delivery-row CAS increments the original
  reservation fence and sets CanonicalReplay, preserving C/H, source, generation,
  head dispatch and route manifest. Generic/ambiguous failures never switch modes.
- Canonical fallback support for a single-event handler is the optional,
  fingerprint-bound descriptor field `CanonicalReplayProtocol =
  "staged-full-replay/v1"`. Historical SingleEvent/FullReplay metadata flags remain
  disjoint, including old requests for dual-capable handlers. Descriptor-absent
  fingerprints remain unchanged. Unknown protocol values fail closed.
- A downgraded guarded detail row no longer blocks an independently qualified
  index sibling. The detail row/model remain unchanged, the sibling can complete,
  and the incomplete catalog cannot report Current. The broader absent-scope and
  unsupported-handler partial-admission cases below remain unfinished.
- QueryRouter's production DI path now uses `InspectCurrentAsync`, which performs
  only read-only proof inspection and cannot enter tails, pending recovery or
  cleanup. Activated Current also requires the returned projection version to
  equal the proven selected freshness version. Empty/unproven/missing-version and
  unsupported shared-family evidence stays Unknown; Local-only remains Local-only.
  Public query/lifecycle DTOs and the public QueryRouter constructor are preserved.
- Actual owning AggregateActor, ProjectionLifecycleActor, delivery-state/marker,
  raw Dapr model, handler and scheduler operations emit identity-free cost
  measurements on the existing Hexalith.EventStore meter. Transactions count one
  RPC and their individual mutation attempts. Guard decisions emit bounded mode,
  status/reason and numeric H/C/lag; exporter failures cannot interrupt work.
- An optional production-owner lane in GuardedTailProductionHarness uses real
  AggregateActor and ProjectionLifecycleActor methods, DaprProjectionDeliveryStateStore,
  production DomainService and DaprReadModelStore, over recording Dapr storage.
  It measures exact current reads/zero work; long and multipage tails; R=4096
  current rows; greedy large-payload lookahead; and an exact 67,108,864-byte source
  pass versus one byte over with no effects. Scheduler tests cover 64x64 items
  plus one 128-item shard and actual complete serialized scan bytes.
- Two new real Dapr/Redis tests append 256/257 events atomically, inspect and
  recompute the exact stored source chain through actor methods, validate bounded
  pages, and recreate the proxy. They exposed missing DataContract/DataMember
  annotations on SourceProofInspection/SourceProof; those and the bootstrap request
  carrier are now annotated. Persisted STJ and source-v1 codec bytes are unchanged.

## Critical remaining sizing failure

**The frozen sizing ceiling is not satisfied. Do not call W4 sizing qualified.**
`AggregateActor.SourceProof.cs` sizes each returned envelope once and each actual
lookahead once. `ProjectionSourceGuardHistoryReader.cs` sizes every returned
envelope again to enforce the independent pass/page byte limits. If E is the sum
of returned envelope JSON lengths and B includes their array framing, actual
`SourceSizingBytes = 2E + Blook` per pass. The approved category permits B+Blook.
The new mixed-payload test asserts the truthful actual duplicate work, not a
substitute relaxed qualification ceiling. The retained cost vectors expose it.

Remove the duplicate while retaining independent byte enforcement, preferably by
an additive actor-owned measured-range response carrying exact event-array byte
lengths (and explicit accounting for its transport wrapper). Preserve existing
actor APIs. Count actual framework/body serialization/deserialization and every
lookahead/failure; do not hide the second pass or relabel it out of the budget.
`SourceResponseSerializationBytes` and `SourceResponseDeserializationBytes` now
separate returned body work from sizing; complete remoting/framing accounting is
still unproven. Oversized/early-aborted sizing and metadata measurement work also
need a complete byte-accounting audit. Do not sum overlapping model/control byte
counters into a fabricated total.

In particular, `SourceEnvelopeResponseSize.Count` can throw from its
`minimum > maximumBytes` check before recording SourceSizingBytes, and the actor
records SourceDeserializationBytes only after Count returns. An individually
oversized event can therefore report zero decode/sizing bytes despite an actual
event-key read and full envelope deserialization. Move measurements to truthful
owning operations/finally paths, preserving the exact rejection and zero effects.
AsyncLocal collectors do not cross Dapr/HTTP remoting boundaries. Live cost
qualification requires exported production measurements or an equivalent actual
seam collector, not only an in-process ambient scope.
Also audit each independent current metadata-body cap: the actor records
serialized source metadata bytes but does not yet enforce the 4096-byte source
body cap. A long permitted head MessageId must not yield qualified Current with
an oversized source body. The two markers each have a 4096-byte cap; each source
body has 4096; each lifecycle/selected-owner body has 16,384. Larger proof-row
identifier allowance under its own absolute cap does not relax these limits.

## Remaining mandatory work

1. Finish all §4/§9 compatibility and partial exact-scope admission. The probe
   still returns unowned when ordinary routes lack descriptors, and qualification
   still vetoes an otherwise supported scope when siblings lack implemented guard
   profiles. SourceGuard null handling can stop a partially admitted catalog.
   The new downgraded-row test covers only independently guarded siblings. Preserve
   overall Current requirements while permitting compatible unqualified route
   diagnosis/delivery through upgraded writers and exact per-route fences.
2. Audit actual new-server/old-handler and legacy v1 behavior. The ordinary
   descriptor-absent compatibility path remains the existing GetEventsAsync/full
   dispatch/direct actor-write path; no bounded staged fallback or optimized-cost
   qualification is established for it by this continuation. Implement missing
   approved fallback behavior without narrowing established wider configurations.
   Mixed mapped/nonmapped replay groups without a common advertised part protocol
   remain typed reconcilable/no-effect; do not publish colliding rebuild manifests.
3. Finish every §6 crash/cancellation/lease/generation/competing-writer interruption
   with persisted outcomes, including private cleanup lost responses and model
   changes during cleanup. Current tests cover explicit no-effect switching,
   ordinary cleanup failure, original pending/tail recovery and the earlier
   continuation's stage/commit lost responses, not the complete interruption matrix.
4. Complete costs at every owning seam/mode. Existing proof visit instrumentation
   covers core validation/membership loops, not yet every copying/LINQ/size-validation
   traversal. Scheduler scans and shard CAS sizes plus lease attempts are measured;
   full cold migration/outbox/shared/fold/rebuild/part/cleanup costs remain unproven.
   Metadata measurement serializations and all raw/framework bytes need separate
   truthful accounting. No full memory/working-byte qualification is established.
5. Finish W0's P=1/16/256 cartesian scope, W1 three-route production-owner costs,
   every W2/W3 boundary, protected W4 bytes, complete wire-array exact/one-over,
   proof absolute/fixture caps, R/R+1 at 256/4096, A=8/32 complete CAS byte/visit
   vectors, and N=8/9 through actual scheduler/model seams. The earlier N=9 test
   still uses scheduler substitutes. Measure independently of saturated Attempt.
   W5 all legacy/named actor/detail/index/freshness surfaces and wider configured
   streamed-rebuild cases still require final qualification.
6. Extend the actual guard current/tail/fallback/crash production fixtures into the
   live lane. The two new live tests prove source primitives only. Installed daprd,
   placement and scheduler are 1.18.7, while the expected runtime is 1.18.2. Do not
   overwrite the user's runtime or label it pinned. An isolated temporary pinned
   binary and fixture configuration may be used in a following continuation.
7. Complete all approved §10 affected suites and Release/package restore/build
   and suite gates after final runtime edits. Existing crypto advisory warnings
   remain unqualified gates; do not change dependencies or audit policy. Inspect
   and classify baseline skips explicitly. Parent final diff/matrix/review work
   remains after implementation and all required checks.

Shared family Current remains intentionally unsupported. Per-source terminal fold
proof neither establishes complete family owner/freshness coverage nor fits the
selected-owner hot-path profile once lifecycle is checked. Do not invent a
parallel receipt protocol or infer support from interfaces/journal acceptance.

## Key continuation paths

- Client: ProjectionDeliveryCost*.cs, DaprReadModelBatchStateAccessor.cs,
  DaprReadModelStore.cs, ReadModelBatchProtocol.cs, ProjectionRouteCatalogFingerprint.cs.
- Server: ProjectionSourceGuardDelivery*.cs, ProjectionSourceGuardTailDelivery*.cs,
  ProjectionSourceGuardReplayCleanup.cs, ProjectionDeliveryIdempotencyCoordinator.SourceGuard.cs,
  ProjectionActivationWorker.cs, ProjectionDeliveryRetryWorker.cs,
  DaprProjectionDeliveryRetryScheduler.cs, ProjectionDeliveryDiagnostics.cs,
  Queries/QueryRouter.cs and Configuration/ServiceCollectionExtensions.cs.
- DomainService: DomainProjectionGuardedReplay.cs, EventStoreDomainServiceExtensions.cs,
  EventStoreDomainServiceRoutes.cs, AdminOperationalIndexMetadata.cs.
- Tests: NamedProjectionDispatchCoordinatorTests.Costs.cs/.SourceGuard.cs,
  GuardedTailProductionHarness.Costs.cs, DaprProjectionDeliveryRetrySchedulerTests.Costs.cs,
  ProjectionActivationWorkerTests.cs, Queries/QueryRouterSourceGuardTests.cs,
  and live NamedProjectionDispatchLiveSidecarTests.SourceProof.cs.

See [Continuation 05 verification](continuation-05-verification.md) for exact
commands, observed results, failures, environment identity and retained costs.
