---
title: 'Story 6.5b: Verified Read, Replay, and Projection Spec'
type: 'feature'
created: '2026-09-27'
status: 'candidate'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

## Intent

Specify one authenticated, bounded event-evolution read path for replay, projection, query, reconstruction, backup, and inspection as an input to the single Story 6.5 AD-13 artifact. This story changes no runtime behavior.

## Boundaries & Constraints

Read-time adaptation never rewrites stored bytes, changes message/sequence identity, skips poison history, advances an unearned checkpoint, or publishes partial authoritative state. Protected evidence remains protected. Legacy adapters stay explicitly bounded and source compatible. No independent approval of Story 6.6 is granted.

## Tasks & Acceptance

- [x] Inventory every raw source, proof, unprotect, upcast, Apply, projection, query, Admin, backup, and snapshot-recovery path in this slice.
- [x] Propose exact authenticated source and effective-view contracts, alias and hop validation, complete-prefix paging, route scope, named-root and checkpoint proof, timeline semantics, typed failure, and cancellation behavior.
- [x] Propose compositional memory and byte budgets, successor buffer ownership across async saves, and replay/index retention rules with a safe hold before capacity is exhausted.
- [x] Give `BH37-3`, `BH37-4`, `BH37-5`, and `BH37-10` explicit accepted or rejected dispositions with cross-links to proposed normative sections and verification vectors.

**Acceptance Criteria:**

- Given one mixed-version prefix, when any supported consumer reads it, then the same registered chain and authenticated source evidence yield the same current domain meaning or the same typed last-good-state failure; adaptation happens once.
- Given paging, cancellation, oversized inputs, stale proofs, or an async successor write, when the design is checked, then no partial success, caller-mutable committed bytes, unbounded allocation, or unearned checkpoint is possible.
- Given Story 6.5 integration, when this work is reviewed, then its section candidate and findings are ready for reconciliation with Stories 6.5a and 6.5c; this child story alone authorizes no runtime work.

## Verification

Review the candidate against the current source inventory and the exact `BH37-*` triage rows. Check cross-consumer equivalence, tamper/race, page/timeline, 64 MiB legacy input plus prior state, cancellation, and rollback vectors; keep the AD-13 receipt `UNAPPROVED`.

## Candidate scope and integration authority

This is a **section candidate**, grounded in source at `68492519b868899e6ab6bf64931f19f0cb1ca6a7`. It changes documentation only. “Required” below describes proposed future behavior, not an implemented guarantee. The [single AD-13 draft](spec-event-versioning-upcasting.md) remains unchanged and its §12 receipt remains `UNAPPROVED`; Story 6.6 remains unauthorized. The [epic context](epic-6-context.md) supplies the complete-prefix, bounded-cost and compatibility constraints. Epic 8 is not a prerequisite.

Integration would replace the draft §6 paged timeline/successor paragraphs and extend its §§3, 5, 8–11 with B2–B9. B2 consumes [6.5a A3–A6](spec-6-5a-event-contract-writer-and-migration-evidence.md#a3-bounded-producer-and-whole-response-ingress), including codec-02 intent, post-save receipt **and ActorBundleReadbackHash**. B8 replaces the contradictory read/write-F sentence in the [historical Loop-11 note](story-6-5-design-notes.md#design-notes) without editing that history. Existing signed V17/V20/V22/V23 fixtures, normative receipt and historical triage are untouched. Proposed new private codecs below are not covered by those fixtures. Publication membership, transport acknowledgement, rollout and mixed-fleet activation remain 6.5c's responsibility; this candidate supplies the read-capability and retained-evidence requirements that it must consume.

### B1. Source and test inventory

Paths in this table are existing sources. None implements the complete proposed authenticated evolution route. Test references describe present coverage, not execution or certification of future provider behavior.

| Seam | Current source and behavior | Existing test anchor; required candidate boundary |
| --- | --- | --- |
| Actor raw history and command input | [EventStreamReader](../../src/Hexalith.EventStore.Server/Events/EventStreamReader.cs) `RehydrateAsync` reads typed metadata/envelopes sequentially and accepts a supplied snapshot; [AggregateActor](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs) rehydrates, unprotects, reconstructs snapshots and exposes `GetEventsAsync`/paged reads. Typed state-manager objects cannot establish original raw bytes or presence. | [EventStreamReaderTests](../../tests/Hexalith.EventStore.Server.Tests/Events/EventStreamReaderTests.cs) covers order, missing keys and free-snapshot tails. B2 raw proof precedes typed allocation; command state needs complete proof, B3. |
| Envelope/protection evidence | [Contracts EventEnvelope](../../src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs) normalizes extensions; [protection metadata carrier](../../src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadataCarrier.cs) interprets metadata. Actor, [ProjectionEventWireBuilder](../../src/Hexalith.EventStore.Server/Projections/ProjectionEventWireBuilder.cs), [StreamsController](../../src/Hexalith.EventStore/Controllers/StreamsController.cs) and [EventPublisher](../../src/Hexalith.EventStore.Server/Events/EventPublisher.cs) have separate unprotect paths. | [protected replay regression](../../tests/Hexalith.EventStore.Client.Tests/Security/AggregateReplayerProtectedDataLeakRegressionTests.cs), [protected stream regression](../../tests/Hexalith.EventStore.Server.Tests/Security/StreamsControllerProtectedDataLeakRegressionTests.cs). B2 preserves storage/readable/effective byte domains; 6.5c owns delivery authority. |
| Snapshot recovery | [SnapshotManager](../../src/Hexalith.EventStore.Server/Events/SnapshotManager.cs) stages under actor ownership, normalizes legacy protection, preserves provider-opaque/unreadable protected snapshots and returns null for fallback. | [SnapshotManagerTests](../../tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs), [SnapshotRehydrationTests](../../tests/Hexalith.EventStore.Server.Tests/Events/SnapshotRehydrationTests.cs). Current tail equality is envelope equality, not canonical folded-state proof; B3/VB-08. |
| Apply and state rehydration | [AggregateReplayer](../../src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs) sorts a full array, resolves stored CLR names, uses synchronous reflection Apply and serializes the same mutable state after an Apply exception; [DomainProcessorStateRehydrator](../../src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs) accepts typed/JSON/enumerable states and shares [ApplyMethodResolver](../../src/Hexalith.EventStore.Client/Aggregates/ApplyMethodResolver.cs). [EventStoreProjection](../../src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs) is another convention consumer. | [AggregateReplayerTests](../../tests/Hexalith.EventStore.Client.Tests/Aggregates/AggregateReplayerTests.cs), [ApplyMethodResolverTests](../../tests/Hexalith.EventStore.Client.Tests/Aggregates/ApplyMethodResolverTests.cs). No verified view, private successor or paged timeline is proved; B2–B6. |
| Aggregate wrapper | [EventStoreAggregate](../../src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs) `ProcessAsync` rehydrates supplied state through DomainProcessorStateRehydrator, then dispatches Handle; `Replay` delegates directly to AggregateReplayer. Neither wrapper authenticates state/prefix authority. | [EventStoreAggregateTests](../../tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs) covers Handle dispatch/rehydration; [AggregateReplayerTests](../../tests/Hexalith.EventStore.Client.Tests/Aggregates/AggregateReplayerTests.cs) covers its replay delegate. B2/B3 require command-state admission before ProcessAsync and verified replay selection before Replay; VB-01/06/08/10. |
| Router/reconstruction | [DomainServiceRequestRouter](../../src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs) selects a keyed processor implementing synchronous `IAggregateReplay`; [DaprAggregateStateReconstructor](../../src/Hexalith.EventStore.Server/DomainServices/DaprAggregateStateReconstructor.cs) maps whole arrays to `/replay-state`. [CoordinatedCommandActor](../../src/Hexalith.EventStore.Server/Actors/CoordinatedCommandActor.cs) reads source and target history for continuation/retry. | [DaprAggregateStateReconstructorTests](../../tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprAggregateStateReconstructorTests.cs). Both source and target require B2–B3; a target retry cannot bypass verification. |
| Live projection | [ProjectionUpdateOrchestrator](../../src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs) `UpdateProjectionAsync` reads `GetEventsAsync(0)` **before** its checkpoint; comment explicitly preserves full replay until handlers receive prior state or declare incremental capability. Wire builder unprotects to a complete DTO array. | [ProjectionUpdateOrchestratorTests](../../tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs), [refresh interval tests](../../tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs). Current no-op/tail cost is not B7's zero-event-read guarantee. |
| Retry/history/page validation | [ProjectionDeliveryRetryWorker](../../src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryRetryWorker.cs) `LoadHistoryAsync`, [EventStoreProjectionDeliveryHistoryReader](../../src/Hexalith.EventStore.Server/Projections/EventStoreProjectionDeliveryHistoryReader.cs) and [ProjectionStreamPageValidation](../../src/Hexalith.EventStore.Server/Projections/ProjectionStreamPageValidation.cs) validate/read history then build projection wire events. | [retry worker tests](../../tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryRetryWorkerTests.cs), [history reader tests](../../tests/Hexalith.EventStore.Server.Tests/Projections/EventStoreProjectionDeliveryHistoryReaderTests.cs). B2 before fingerprint/handler; B7 before checkpoint on retries too. |
| Named and shared rebuild completion | [NamedProjectionDispatchCoordinator](../../src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs), [DomainProjectionDispatcher](../../src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs) `Project`/`DispatchAsync`/`ReconcileAsync`/rebuild phases, and [DomainSharedProjectionRebuildDispatcher](../../src/Hexalith.EventStore.DomainService/DomainSharedProjectionRebuildDispatcher.cs) maintain dispatch/history/inventory and staged completion. | [named dispatch tests](../../tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionDispatchCoordinatorTests.cs), [dispatcher tests](../../tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionDispatcherV2Tests.cs), [shared rebuild tests](../../tests/Hexalith.EventStore.DomainService.Tests/DomainSharedProjectionRebuildDispatcherTests.cs), [live named tests](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs). B7 adds source/effective proof and scoped root visibility; existing completion work remains relevant but insufficient. |
| Read model/query | [DaprReadModelStore](../../src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs) `GetAsync` resolves batch-marker visibility; `GetManyAsync` bulk reads then resolves envelopes. Neither receives route proof. [SharedProjectionEpochCoordinator](../../src/Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.cs) reads/writes physical generations. | [DaprReadModelStoreTests](../../tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelStoreTests.cs), [batch tests](../../tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelBatchTests.cs), [epoch tests](../../tests/Hexalith.EventStore.Client.Tests/Projections/SharedProjectionEpochCoordinatorTests.cs). Preserve non-versioned semantics; B7 protects every versioned generic/physical-key route. |
| Query dispatch and HTTP mappings | [DomainQueryDispatcher](../../src/Hexalith.EventStore.DomainService/DomainQueryDispatcher.cs) `ExecuteAsync` resolves IDomainQueryHandler from the request service provider, matches domain/query type case-insensitively and rejects duplicates; it has no pinned input session. [EventStoreDomainServiceExtensions](../../src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs) `MapEventStoreDomainService` wires `/process`, `/replay-state`, `/query`, `/project`, `/project/v2` and rebuild routes; `/query` currently omits the request token and `/replay-state`/`/project` use sync helpers. | [EventStoreDomainServiceExtensionsTests](../../tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs) includes route mapping, discovered/duplicate handlers and `DomainQueryDispatcher_Execute_RoutesToMatchingHandler`/missing/duplicate-route cases. B3/B7 require endpoint capability admission and original-token forwarding; B7c/B7d bind query selection and scoped DI reads before invocation. VB-01/10/11/19/20; these tests do not prove authenticated intake. |
| Query routing/cache | [QueryRouter](../../src/Hexalith.EventStore.Server/Queries/QueryRouter.cs) `RouteQueryAsync` derives the projection actor, observes persisted lifecycle before/after invocation and stamps result metadata. [CachingProjectionActor](../../src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs) uses a non-null ETag plus query/payload/paging/user key for a cache hit; changed ETag clears entries, null ETag bypasses caching, and returned cached lifecycle is normalized to Unknown. Neither implements the candidate's authenticated named-root/generation/compatibility verification. | [QueryRouterTests](../../tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs), [CachingProjectionActorTests](../../tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs). B7b bypasses versioned cached payload before lookup; B7 applies before Current; VB-12/19 test unchanged ETag and derived-query/code/capacity changes. |
| Stream/replay/Admin/trace inspection | [StreamsController](../../src/Hexalith.EventStore/Controllers/StreamsController.cs), [ReplayController](../../src/Hexalith.EventStore/Controllers/ReplayController.cs), [AdminStreamQueryController](../../src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs) state, detail, timeline, bisect, blame and diff routes, and [AdminTraceQueryController](../../src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs); several call `GetEventsAsync(0)`. | [StreamsControllerTests](../../tests/Hexalith.EventStore.Server.Tests/Controllers/StreamsControllerTests.cs), [ReplayControllerTests](../../tests/Hexalith.EventStore.Server.Tests/Commands/ReplayControllerTests.cs), [timeline tests](../../tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTimelineTests.cs), [replay delegation tests](../../tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerReplayDelegationTests.cs), [state diff tests](../../tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerStateDiffCausationTests.cs). B3/B4 complete replay; B9 safe diagnostics. |
| Diagnostic sandbox | [AdminStreamQueryController](../../src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs) `SandboxCommandAsync` invokes the domain service with historical state or explicit empty state, synthesizes unpersisted envelopes, appends them to base history and calls reconstruction again. Those hypothetical events have no storage proof. | [replay delegation tests](../../tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerReplayDelegationTests.cs) `SandboxCommandAsync_HistoricalAtSequence_ReplaysOnlyEventsUpToSandboxPointPlusSyntheticEvents` and safe invocation-error coverage. B2a replaces that second production replay with isolated simulation; VB-15. |
| Authorized backup | [DaprBackupCommandService](../../src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs) `ExportStreamAsync` probes/pages stream reads, collects events, rejects protected/provider-opaque exports and builds JSON/CloudEvents. | [DaprBackupCommandServiceTests](../../tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprBackupCommandServiceTests.cs). B2 authenticates stored provenance; B6/B9 bound and privately stage the entire export before release. |
| Subscription handoff | [EventStoreDomainEventProcessor](../../src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventProcessor.cs) is the consumer boundary for published events. | Reader contract is B2; delivery pins, route receipts, poison capture and acknowledgement tests remain draft §7 and 6.5c work. This slice grants no subscription activation. |

### B2. One authenticated source and one effective view

Retain draft §§2–4 and §6 claim codecs except B2’s explicit source-evidence replacement and B4’s private replay additions, zero-event rule and authenticated transcript selection. Use the proposed `IAuthenticatedRawEventSource.ReadRawPageAsync(tenantId, domain, aggregateType, aggregateId, startSequence, maxCount, maxRawBytes, maxReadableBytes, cancellationToken)` as the only storage entrance to `IEventEvolutionReader`. This is **not present** in current typed `EventStreamReader`. Provider-readiness proof must establish bounded transport/readback before a typed envelope, Base64 string, DOM or raw buffer is materialized. Missing provider support returns `RawSourceUnavailable` before that allocation; existing typed reads are not a proof fallback.

The reader owns these three distinct views, with no caller-writable alias:

| View | Evidence and permitted use |
| --- | --- |
| Authenticated source | Exact original raw bytes and raw hash; physical event key/sequence; tenant/domain/aggregate type/ID; original encoding/namespace evidence; raw member-presence map; stored payload/format/type/version; original protection scalar and derived protection record; MessageId, position, correlation/causation and original timestamp **offset**. Fresh backend-bound purpose-10 readback proves addressed bytes/head/ETag. Source is immutable replay authority, including when JSON parsing fails. |
| Readable source | Successful bounded unprotection result under that occurrence's authenticated protection metadata. Preserve source bytes and StoredDigest separately. `ProviderOpaque`, missing key or unsupported provider yields a typed hold; ciphertext never becomes readable state. An unprotected/no-op source still needs scope/schema proof. |
| Effective view | Draft `VerifiedEffectiveEventView`: sequence, StoredDigest, current canonical type/version/format, effective payload and exact purpose-01 route carrier. Sign only after admitted registry/alias, schema/identity and every hop validate. Public DTO construction/`isAdapted` cannot establish authority; receivers copy, revalidate and use private bytes. |

For each event, first verify fresh raw-source proof and the retained provenance branch. Upgraded V1 uses 6.5a A5's complete **codec-02** intent and separate purpose-11 provider receipt; verify exact complete committed-generation mutation rows, derived certificate/origins, member-root, backend/fence and the codec-02 `ActorBundleReadbackHash`, plus the whole batch's `Committed` readback. A certificate or a receipt alone is insufficient. New V2 consumes the same complete commit evidence without V1 origin. Retained V1 uses A4 branch-02 manifest, independent reviewer docket, decoder, encoding and digest sidecars. Authenticate historical committed-generation images, which may include metadata/index keys subsequently changed, separately from today's source observation: a new valid head/ETag does not invalidate immutable old event evidence. Verify retained signing intervals, revocation and backend identity; do not treat an expired historic authority as permission to mint current route proofs. Missing complete evidence is `ActorCommitEvidenceHold`/`LegacyEvidenceConflict`, never synthetic provenance.

**Explicit V2 replacement of draft §3:** replace “For V2, origin and branch-01 records are absent” insofar as it forbids actor intent/provider commit evidence. For active new V2 reads, raw bytes, encoding sidecar, StoredDigest sidecar, codec-02 actor intent and separate codec-02 purpose-11 receipt are **required**; only V1-origin is absent. In the unchanged purpose-10 raw-readback event-entry framing, the five optional-hash presence flags (encoding, digest, origin, intent, receipt) are therefore exactly `01,01,00,01,01`; each present flag is followed by its exact 32-byte record hash, never a zero placeholder. Upgraded V1 requires all five; retained V1 branch-02 requires encoding/digest/origin, with no invented new-writer intent or receipt. The raw hash remains separately mandatory. Hash the exact intent claim and receipt **claim** records in those slots; retrieve and verify the corresponding signed carrier, original interval and backend authority separately. A5’s committed batch root, complete generation images and codec-02 ActorBundleReadbackHash remain required out-of-line evidence, resolved through the authenticated intent OperationId/backend/generation. The absence/presence framing and purpose-10 codec `01` stay unchanged; what changes is V2 admission and the codec-02 content referenced by those hashes. An absent intent/receipt or codec-01 complete-save substitute fails `ActorCommitEvidenceHold` before typed binding, never V2 acceptance via the old sentence.

Keep V1 absent/null presence and exact domain-scoped alias/source version/format; no generic version-1 guess. V2 requires exact known lower-camel member spelling, metadata version 2, canonical pair and stored `EventTypeName == EventContractType`; enforce strict UTF-8, one object, depth/duplicate checks and allow-list before binding. Aliases are case-sensitive. Registry conflicts/gaps/cycles/ambiguous aliases or more than 16 consecutive `n→n+1` hops block readiness. After **each** hop validate declared type/version/format, schema and registered identity policy before another hop. `NoPayloadIdentity` waives only payload-ID extraction, never envelope/key/route equality. One hopped output is at most 1 MiB; a proven zero-hop current V1 event may reach the measured legacy ceiling, at most 64 MiB, subject to B6 combined admission.

Receiver recomputes effective payload hash and consumed metadata hash, including original timestamp offset, and verifies exact sequence, StoredDigest and route membership against the complete prefix. Missing/stale proof causes authenticated source lookup and fresh proof or a typed hold. Adapt from original readable bytes once; a verified current view is consumed directly and never sent through the upcast chain again. Hold views separately from stored DTO fields; adding `EffectivePayload` cannot make the current CLR-name replay implementation use it. Every B1 Apply, history fingerprint, handler and content response passes the same admission, including coordinated source/target retries and shared rebuild inventory. A source-specific raw-corrupt disposition follows 6.5a A6: fresh bytes/backend incarnation must match the historical source identity, but fresh head/ETag need not equal the decision-time observation; replay still stops at that event without invented digest or skipped poison.

#### B2a. Diagnostic-only sandbox simulation

The sandbox is an explicit exception to **persisted event admission**, not an exception to schema, authorization, isolation or bounded memory. Preserve the existing `SandboxResult` fields and `accepted`/`rejected`/`error` meanings; none conveys committed command success. The existing controller’s synthesized envelopes must never enter `IEventEvolutionReader`, `/replay-state/pages`, production command admission, checkpoint or publication. No fake StoredDigest, MessageId reservation, purpose-01/03/07 proof or persisted-event sequence is minted for hypothetical output.

At admission, authenticate the operator’s sandbox permission and tenant/domain/aggregate route. For historical/current simulation, reconstruct the fixed actual base prefix at the requested `AtSequence` through B2/B3; require completed proven state before invoking the diagnostic domain route. Invalid base proof, poison, an above-head target or unreadable protection returns the existing safe replay Problem response with no domain invocation. `AtSequence=0` explicitly means **hypothetical empty genesis for this authorized route**, including when a stream already exists: use the registered initial-state serializer/schema, record base kind `empty-simulation`, and assert nothing about stored stream absence/head. Resolve aggregate type from the allow-listed command/aggregate registration, not an empty or caller-guessed CLR name. An ambiguous/missing route holds before invocation. Empty state remains `null` for the existing domain invocation and `{}` in the diagnostic response where the route’s registered initial-state convention requires it.

Use a separately authenticated internal simulation invocation bound to operator, scope, selected route/code/options, base-kind, base-state hash, exact command bytes and request nonce. It carries private validated state only; it is rejected by production command verifiers as `CommandStateUnproven` and never issues production command-state authority. The endpoint invokes only a registered side-effect-free diagnostic adapter (same domain Handle/Apply code, isolated state; no actor save, event append, effect provider, projection or publication access). A route whose code/dependencies cannot satisfy that capability returns safe sandbox `Outcome=error`/`SandboxCapabilityHold` before invocation; a legacy production endpoint cannot silently accept free simulation state. This is a proposed capability, not a guarantee from today’s `domainServiceInvoker`.

Before materializing hypothetical output, reuse 6.5a A3’s producer/ingress preallocation and strict parser/metadata rules (depth 64, 1,000,000 nodes, 64 KiB windows, 512 KiB per-event metadata). The separately registered diagnostic response capability has hard maxima of 1,000 events, 1 MiB per hypothetical current payload and 128 MiB complete encoded output; it grants no writer-mode capability. If the adapter uses A3’s V2-shaped result transport, its stricter 256-event/16 MiB whole-result limits also apply. Keep B6’s **combined** 128 MiB working budget and bounded state/diff/response serialization. A smaller composed reservation wins. Validate every emitted event against the allow-listed current D/V descriptor, registered format/schema and identity policy for the selected aggregate, including rejection payload schemas; no upcast, stored alias guess or source signature substitutes for validation. Validate all output before any simulated Apply. No-op returns the private base state and empty changes; rejection returns validated rejection events and no applied resulting state, as today. Success Applies each validated current event exactly once to a private copy of the proven/simulation base, retains last-good bytes before each Apply, and renders the final bounded state/diff only on whole success. Simulated positions are local ordinals `1..count`; retain public `AtSequence` as the base and use checked arithmetic if a diagnostic display computes `AtSequence+ordinal`. Overflow is `SandboxSequenceLimit`, before Apply, including nonempty output from a max-sequence base.

Invalid hypothetical schema/identity/size returns safe `Outcome=error` with `SandboxOutputRejected`/the bound reason, empty produced events, empty resulting state and changes; Apply/serialization failure returns the existing safe replay failure Problem with no partial success. Invocation failures retain the current safe `Outcome=error` convention without exception details. Cancellation propagates the original token between base read, invocation, validation and Apply and before response; discard private output with no successful result. All paths perform zero event writes, checkpoint changes, reservations, outbox writes or publication. Protected base-state access remains subject to existing sandbox content permission and readability; diagnostic permission alone never widens content access. VB-15 and LB-09 cover the specified outcomes at their stated levels.

### B3. Complete prefixes, snapshots and public compatibility

This refines draft §§3, 6, 8–9. Fix scope, admitted head and inclusive target at operation admission (`0 ≤ target ≤ head ≤ long.MaxValue`). Authenticate actual source under that operation. New later appends do not extend its target; a changed/invalid fixed-head proof restarts page 1 without reusing partial state. Never silently clamp a requested beyond-head target into success. Existing `Replay_UpToSequenceBeyondLastEvent_ReturnsLastValidState` documents legacy behavior only: a legacy gateway must resolve and disclose the actual target before verified admission; it cannot assert a signed prefix through unavailable events.

Before Apply, require a contiguous ordered page, exact per-event route proofs and signed prefix count/start/end/head/target/accumulator; reject gaps, duplicates, reordered authenticated entries, truncation and mixed scope. Pages are at most 256 events/64 MiB readable and 64 MiB proof; command replay uses 2 MiB proof including its final wrapped/Base64 command-state carrier. Reduce and re-sign an unadmitted page until the entire live phase fits B6. Never shrink, re-sign or alter a pinned page under its existing operation. Each committed next page starts at prior end+1; final success requires end=target and complete transcript/state proof, not merely exhaustion of the supplied array.

The draft’s existing count-zero forms remain exact: empty stream start=1/end=head=target=0; witnessed snapshot or projection checkpoint at k<head's integer limit uses start=k+1/end=k/head=target=k; at `long.MaxValue` use start=end=head=target=k, count=0 with the appropriate anchor and no increment/next token. A count-one terminal event at k is distinct and applies once. Aggregate replay rejects projection checkpoint anchors. A checkpoint cannot substitute for a snapshot or complete history. To preserve replay-at-zero for an existing stream, selected v2 additionally admits an **empty-target prefix** with start=1/end=target=count=0 and the authenticated actual head>0, no anchors or routes, and scoped genesis accumulators. This explicitly replaces draft §6’s prohibition of every other count-zero nonempty-head form only for that zero target; it proves the requested empty prefix, never that the stored stream is empty. Old v1 parsers must not receive the added form.

Snapshot reuse requires the exact approved v1 storage bytes and paired `:evolution-witness`, matching source scope/key/sequence, batch operation/logical version, authenticated provider readback, folded-byte/storage-byte hashes, serializer, protection, `StateSchemaApplyHash`, `EventTransformHash` and covered accumulator. Authenticate storage/ciphertext independently from readable folded bytes. Deserialize only proven folded bytes. Missing/stale/free snapshots, protected unreadability, changed bytes or mismatched pair discard the **entire** candidate tail before Apply and request fresh page 1 without snapshot authority. A snapshot whose covered sequence equals a nonzero requested target **below the actual admitted head** is ineligible for the zero-tail shortcut: for snapshot=target=7/head=9, discard its entire candidate tail before Apply and perform full authenticated replay from 1 through the original target 7 under actual head 9. Do not lower the admitted head, extend the target, or invent a count-zero prefix. This fallback uses existing nonempty prefix codecs; stored snapshot/witness bytes remain intact. Preserve stored snapshot bytes. Cancellation propagates and infrastructure failure cannot fabricate an empty stream. Full authenticated replay may still fail on unreadable event history; fallback never skips it. Without approved folded runtime and its witness path, use full replay and claim no snapshot tail-cost optimization.

For `includeTimeline=true`, **always start at sequence 1 without a snapshot**: current public timeline means one post-event state for every event through target. A folded snapshot cannot reconstruct earlier entries. Target zero returns a completed empty timeline using the exact empty-stream or empty-target form above; `false` returns null. B4 keeps all timeline state private until final completion. B7's projection checkpoint optimization is independent of this aggregate timeline rule.

Keep existing public constructors/deconstruction, required `ReplayEventEnvelope.MetadataVersion`, `long? GlobalPosition`, stored `Payload`/`EventTypeName`/format meanings and old endpoints. New async replay is draft `IAsyncAggregateReplay` with token-aware `ReplayAsync(request, token)` and explicit aggregate-route selection; it is discoverable independently of keyed command processors. Hopped sources require it, and projections require verified handlers. Existing sync replay/rehydrator adapters are bounded, zero-hop only, reject `PagedContext`, and check cancellation immediately before/after invocation and between events where the adapter owns the loop. They cannot promise interruption within synchronous Apply. A nonzero-hop source never reaches stored-envelope Apply. Command processors receive only purpose-07 proven complete state, with all committed pages verified via the durable ledger, or `CommandStateUnproven` before invocation.

Keep legacy `Partial` results as diagnostics only where that contract already exists; never present them as a successful state/checkpoint/query or use them as command input. Paged `InProgress` has null final state/result/proof/timeline, `ErrorCategory=None`, a verified page-end sequence and opaque progress/token only. Last-good diagnostics must be retained **before** Apply (B5); the current catch-and-serialize mutated object is not sufficient. No intermediate timeline is publicly successful, even when signed.

### B4. Authenticated private timeline and atomic page transition

**BH37-3 accepted.** Draft §6 already promises final-only public results and exact pinned requests/responses but does not bind cumulative timeline to the successor. Replace that omission with the following private protocol. It neither introduces public chunked timelines nor permits intermediate `Succeeded`/`Partial` responses. These records supplement draft route/prefix proof; they do not independently authorize events.

Use draft §4 primitives: `U`/`B` are u32 big-endian byte length then strict UTF-8/exact bytes; `N` is signed-i64 big-endian restricted to `0..2^63-1`, `I` is signed-i32 big-endian restricted to `0..2^31-1`, `H` is exactly 32 bytes. The separators below end in one NUL. Ordered tagged records have the stated u16 count and one-byte tags `01..count`; reject duplicate/missing/unknown/out-of-order fields, invalid lengths and trailing bytes before allocation. Canonical state uses the registered state serializer; no current serializer may regenerate a pinned retry.

Reject negatives and values above those signed maxima **on encoding and immediately on decoding every I/N**, before arithmetic, allocation, range interpretation or use. This includes nested S head/target, E sequence/version, P pageStart/count/timeline counts/bytes/generation, selector versions/revisions/ticks, manifest lengths/indices and prior-state sequence/generation. Positive payload versions still require `1..1024` after scalar decoding; page counts still require `0..256`. `u32` length/count framing remains unsigned `0..2^32-1` followed by its smaller enclosing content cap; it is not I, and a high-bit u32 length does not become a negative scalar. Q uses checked nonnegative N ticks. LB-08 exercises both encode and decode boundaries, including nested scope, without changing any previously valid literal codec bytes.

| Candidate codec | Exact preimage/fields |
| --- | --- |
| Operation scope S | `U tenant || U domain || U aggregateType || U aggregateId || U OperationId || N head || N target || H RegistryFingerprint || H EventTransformHash || H StateSchemaApplyHash || byte includeTimeline`. The mode byte is exactly 00/01. |
| Timeline genesis T0 | `SHA256("HX-EV-REPLAY-TIMELINE-1\0" || 01 || B S)`. State-only mode retains this hash/count=0/bytes=0 with no entries; mode/scope substitution changes it. |
| Entry E | `"HX-EV-REPLAY-TIMELINE-ENTRY-1\0" || 01 || 0008`, then `N sequence; H StoredDigest; U storedEventTypeName; U effectiveType; I effectiveVersion; U effectiveFormat; U stateSerializerId; B canonicalPostApplyState`. Stored type preserves the old public timeline field; effective identity comes from the matching authenticated route. |
| Timeline step Tn | `SHA256("HX-EV-REPLAY-TIMELINE-STEP-1\0" || 01 || H Tprevious || H SHA256(E))`. Entries are contiguous 1..committed page end. Count is at most 1,000; sum of exact canonical state bytes at most 64 MiB; framing/index bytes count separately in B6 and durable quota. |
| Page transition P | `"HX-EV-REPLAY-TRANSITION-1\0" || 01 || 0013` (19 tags): `B S; N pageStart; I pageCount; H exactPinnedRequestHash; H priorCommittedTransitionHash; H priorCanonicalStateHash; H successorCanonicalStateHash; H resultingStoredAccumulator; H resultingEffectiveChain; H exactEventEvolutionProofHash; H priorTimelineHash; H resultingTimelineHash; I cumulativeTimelineCount; N cumulativeTimelineStateBytes; H exactPageEntryListHash; U stateSerializerId; N ownerScratchGeneration; U OperationId; byte isFinal`. Page-entry list is `u32 count || each B E`; an empty list has count zero. First prior-transition hash is `SHA256("HX-EV-REPLAY-TRANSITION-GENESIS-1\0" || 01 || B S)`; first prior-state hash is canonical initial state (or B3 witnessed state only when timeline=false). |
| Production page transcript v2 | Genesis `SHA256("HX-EV-COMMAND-PAGES-1\0" || 02 || B S)`; each committed page `SHA256("HX-EV-COMMAND-PAGE-STEP-1\0" || 02 || H priorTranscript || H SHA256(P))`. Purpose-07 tag `11` signs this terminal transcript. New capability must explicitly select this v2 transcript; do not accept v1 transcript as evidence for timeline accumulation. Existing signed claim codecs/fixtures are not rewritten. |

#### B4a. One zero-event terminal rule

For **transcript v2**, an empty-stream, empty-target or witnessed snapshot zero-tail `/replay-state/pages` request commits exactly **one count-zero final P**, followed by exactly one transcript step. This explicitly replaces draft §6’s “zero-page ... uses the genesis” rule **for selected v2**. Genesis alone is never a completed v2 transcript. Legacy v1 fixtures retain their original interpretation and cannot be relabeled v2. A normal final event page, including count=1 at `long.MaxValue`, completes with its own P and has no extra empty page. Projection checkpoint=head’s metadata-only shortcut creates no replay operation, P, successor or ledger writes; it uses B7’s existing checkpoint proof and is not a command/replay transcript.

For zero events, use the exact B3 signed prefix: empty stream `(start,end,head,target,count)=(1,0,0,0,0)`, empty target `(1,0,h,0,0)` for authenticated h>0, or snapshot k>0 `(k+1,k,k,k,0)` when k<MAX, or `(MAX,MAX,MAX,MAX,0)`. No continuation or increment is permitted in the max form. P has that start, count=0, isFinal=01, first transition-genesis hash, and identical prior/successor hashes over the exact registered canonical initial state or authenticated snapshot folded bytes. Stored accumulator is the scoped genesis for an empty requested prefix or the snapshot’s verified covered accumulator; resulting effective chain is the draft §6 scoped effective genesis because this operation consumed no events. Exact page proof still binds the empty/snapshot form. Timeline hash before/after is T0, timeline count/bytes are both zero, and page-entry-list hash is SHA256 of the literal four bytes `00000000`. Timeline=true is legal only for the empty-stream/empty-target forms (B3); its public final timeline is `[]`, while false is null. Owner generation is the admitted session generation, not an inferred sequence. Empty canonical state is the registered serializer’s initial bytes, never universally an empty string. Final state hash, P, transcript step, selected request and optional snapshot proof must agree before CAS/readback and command admission. LB-10 pins literal P/terminal hash examples for empty, snapshot-zero-tail and MAX; VB-16 requires provider verification of the same forms.

#### B4b. Authenticated transcript selection

Selection is operation-wide and immutable. Add nullable `byte[]? ReplayTranscriptSelection` / JSON `replayTranscriptSelection` to the proposed paged request, encoded as strict canonical padded Base64 and required for v2 (absence is **not** permission to infer v1). The selector claim is `HX-EV-REPLAY-SELECT-1\0 || 01 || 0010`, ordered tags `01..10`: `B S; I transcriptVersion` exactly 2; `U gatewayEndpointId; U receiverEndpointId; U commandVerifierEndpointId; U receiverInstanceId; H RegistryFingerprint; H EventTransformHash; H StateSchemaApplyHash; N capabilityRevision; H approvedDeploymentCapabilityHash; U transcriptCodecId` exactly `hx-ev-command-pages-v2`; `U manifestCodecId` exactly `hx-ev-replay-timeline-manifest-v1`; `Q issuedUtc; Q expiryUtc; H nonce`. S and repeated fingerprints must agree; revision is positive; nonce is 32 CSPRNG bytes treated as H-width opaque data. Endpoint IDs are configured authenticated service identities, never caller URLs, and receiver instance equals the owner lease. Expiry is greater than issuance and no later than operation’s 15-minute admission expiry. All strings are strict UTF-8, duplicate/unknown tags and trailing bytes fail.

Reserve new domain purpose **`12` (hex)** solely for this selector, using draft §6’s exact P-256/P1363 signature input and current active domain key in the revised TrustMapDigest. Its carrier is `HX-EV-REPLAY-SELECT-PROOF-1\0 || 01 || B exactClaim || U keyId || B signature`, with signature length exactly 64. Complete selector carrier is at most 2 MiB; its claim/strings are bounded by that total before allocation, and it is charged inside the operation’s existing proof/encoded-request budgets, never an additional pool. `approvedDeploymentCapabilityHash` is SHA256 of the exact authenticated sealed deployment-capability manifest bytes already pinned across peers in draft §5, with revision bound above. Its selected registry/dependency rows must attest the exact v2 transcript/manifest codec implementations and options, receiver protection provider configuration, and all three endpoint identities/capabilities. Missing/mismatching local code or unbound endpoint holds readiness. That manifest cannot be a caller-supplied hash-only promise.

Gateway first authenticates the current approved per-domain deployment capability and live receiver/command-verifier attestations. It selects v2 only if **all three** explicitly support the exact tuple (revision, registry/read/Apply hashes, transcript codec, manifest codec) and authorized endpoints. It signs/pins one selector before sending any page. The receiver verifies purpose/current key, fields, scope, endpoint authentication, **live** owner lease and current capability before new source processing, Apply or incomplete continuation. The command verifier checks those bindings from the completed ledger and its own authenticated identity before interpreting purpose-07 tag `11`, with the completed-recovery lease distinction below; no component independently guesses tag-11’s version. For non-command Admin replay the configured command-verifier identity is still bound so the result cannot later be routed to an arbitrary verifier. A mismatched endpoint, version, signature, tuple or unsupported codec returns `ReplayTranscriptCapabilityHold` (old-only rollback: `RollbackReaderCapabilityHold`) before Apply/command dispatch, with no silent negotiation fallback. In-flight capability/fingerprint change requires a new page-1 operation; historic selector retention grants exact-evidence access only, not new current proof authority. Expiry forbids new Apply; exact final retry may use the draft’s retained-pin exception only while key/fingerprint/capability and original admitted validity remain valid.

**Completed recovery is evidence verification, not owner reacquisition.** Retain authenticated original admission evidence binding selector SHA-256, receiver endpoint/instance, owner generation, lease interval and successful admission time to the same committed operation. For an exact retained **fully committed final** pin, both receiver/recovery service and command verifier verify that original instance/lease was valid at admission then verify the existing authenticated `ReplayOwnerLease`, consumed/next-token and committed ledger chain through every page. The receiver endpoint/instance stays the originally selected identity, but renewal may advance scratch and lease CAS generations: each P.ownerScratchGeneration must match **that page’s** admitted lease/handle and the linked ledger transition, and each renewed lease has its authenticated monotonically advancing CAS generation. The next page consumes the exact token issued by the prior committed transition; page 1 retains the draft’s absent-token digest rule. Reject a broken token link, changed receiver, stale/reused lease CAS generation or P/lease scratch mismatch. Do not require all pages to reuse the original scratch generation or introduce a new lease codec. They do not require that instance to be alive, its lease to be current, or the recovery service to impersonate it. The recovery service instead authenticates as a currently authorized verifier of that operation. Require unchanged current key/fingerprint/capability tuple, current caller authorization, valid current purpose-07/prefix proof and complete committed request/selector/P/manifest/chunk/response/result readback; missing original admission evidence holds recovery. An expired selector/lease may justify exact retained bytes only, never a new selector, proof, owner generation, Apply or incomplete continuation. Incomplete work still requires its live admitted owner/lease; loss follows B4’s restart/reconciliation rules. No lease exception relaxes signature revocation, current-proof checks or retained-object completeness. VB-07/16 and LB-17 distinguish these paths.

To bind selection without changing S/P or old signed fixtures, define `SelectedRequestHash = SHA256("HX-EV-REPLAY-SELECTED-REQUEST-1\0" || 01 || B exactPinnedWebRequestBytes || B exactSelectorCarrier)`, where the first B is the **complete original wire request including replayTranscriptSelection**, serialized once using the pinned Web/Base64 codec. The second B must equal the decoded field exactly. Both gateway and receiver pin/compare those original bytes; no reserialization is used to verify this hash. P tag `04` uses SelectedRequestHash for every v2 page, replacing the older canonical-17-field fingerprint at that binding only; retain the original draft canonical request record for field validation. Ledger also names SHA256(selector carrier) and retains its exact bytes. There is no cycle: selector names S/capability, request names selector, P hashes request, transcript hashes P, and final purpose-07 signs transcript. V2 purpose-07 requires a present completed operation ID even for one/zero event pages. Command admission retrieves selector, request and every P under that operation, verifies the exact chain and final objects, then accepts tag `11`. A carrier stripped from a request or a ledger cannot be reconstructed or accepted as v1. Old pre-existing v1 operations remain on their explicitly configured legacy capability/endpoint and original verification path; 6.5c must fence them before v2 activation, never route a v2 pin to an old verifier.

Add proposed synchronous `IPagedReplayStateSession.AppendTimelineEntry(long sequence, ReadOnlySpan<byte> canonicalPostApplyState)` to the draft in-process session. It reserves capacity, copies to private storage, validates the canonical bytes and derives E's stored/effective metadata from the currently admitted event, never caller metadata. The session checks exactly one entry after each successful Apply in timeline mode and none in state-only mode; an omitted/extra/out-of-order entry or final entry whose state hash differs from the page successor fails before commit. The trusted replay implementation must serialize each actual intermediate state; hashes do not prove arbitrary domain code obeyed Apply. Its implementation/configuration belongs to the attested route and future production tests.

After each successful event, append its private entry once into bounded authenticated encrypted **operation-owned** storage; no plaintext temp file or public URL. During computation these are bounded non-authoritative encrypted staging bytes; B4c’s final chunk objects can be formed only after L/state hashes make P available. Charge the staging/final-object overlap before work; prepared final chunks are encoded once and never regenerated after ambiguous save. The exact manifest/chunk contract is B4c. Charge manifest, ciphertext overhead, indexes, keys and simultaneously resident chunks. Private accumulation does not require retaining all earlier state entries in RAM. All staging remains invisible until its ledger pointer is proven committed.

The page operation state machine is `Pinned/Reserved → Computing → Prepared → Committed`, or `Ambiguous` after uncertain durable work. Before Computing, authenticate the exact original gateway and receiver request pins, lease, prior transition/state/timeline root, stored/effective chains and B6 reservations. Apply each event once into private working state, seal successor per B5, finish L and derive P from its hash and the state/chain values, then prepare protected timeline chunks/manifest bound to that P, compute the v2 transcript and render the exact response once. Prepare the response blob and optional final state/proof/result using draft §6. Then one linearizable ledger CAS selects **all** exact hashes: request, P, successor, timeline manifest/root/count/bytes, transcript, response and optional final result/proof, plus consumed/next token and owner generation. It compares the prior pointer/generation under the operation fence. P never hashes its own resulting ETag, response, final proof or ledger pointer, so construction is acyclic. The pointer commits the entire transition; separate immutable preparation objects never confer authority. A final response/proof may reference P/transcript before pointer CAS; it remains invisible until that CAS and readback succeed.

Read back the complete selected object set under the same fence, including each timeline chunk's exact hash and terminal timeline/state equality, before emitting any next token or final response. Final assembly streams **all committed page entries** in order into the bounded response; checks contiguous 1..target, count, cumulative bytes and Tn, and maps each to the unchanged public timeline entry (sequence, stored EventTypeName, canonical StateJson). It cannot return just the last page's timeline. `includeTimeline=false` remains fixed for the operation; switching it requires a new page-1 operation.

Lost acknowledgement at successor, timeline chunk/manifest, response, final result or pointer creation is `ReplayCommitAmbiguous`: retain owner, quota, pins and operation, freeze further Apply and reconcile all selected hashes and object presence. For incomplete pages prove final result absent. Matching committed pointer and complete objects returns byte-identical pinned response without another Apply; old pointer plus authenticated no-future-commit proof permits orphan cleanup and a new page-1 operation; any mixed/missing/inconclusive set holds. Absence alone never proves no future write. Crash/retry cannot re-create a missing timeline by replaying an already consumed token. Missing live owner/scratch after incomplete commit yields `ReplayRestartRequired` and a **new** operation from page 1; private durable entries do not bypass affinity. A fully committed final pin needs no live scratch, but still requires valid current proof and retained request evidence.

#### B4c. Exact private manifest and protected chunks

Logical page bytes `L = u32 pageEntryCount || each B E` in increasing contiguous event sequence; count equals P.pageCount in timeline mode, otherwise zero. State-only and zero-event pages have `L=00000000`. P tag `0f` stays **SHA256(L)**, preserving the existing literal P fixture. Split L consecutively at exact 1 MiB plaintext offsets: every nonfinal chunk is 1 MiB, final chunk is 1..1 MiB; even the empty-entry list has one four-byte chunk. No padding, compression, deduplication across operations or alternate split is allowed. One E may span chunks; validate its framing after authenticated reassembly/streaming. Limit L to 96 MiB per page and cumulative L across a timeline operation to 96 MiB, in addition to the 64 MiB cumulative state and 1,000-entry limits. More bytes cause `TimelineLimit` before Apply/admission; an unfit admitted historical route holds readiness, never truncates metadata. At most 96 chunks/page. State-only page lists consume the same quota/retention even though their entry count is zero.

Define chunk binding `C = B S || N pageStart || H SelectedRequestHash || H SHA256(P) || I chunkIndex || N plaintextOffset || I plaintextLength` with zero-based indices. Let `PH = SHA256("HX-EV-TIMELINE-PLAIN-1\0" || 01 || B C || B exactPlaintextChunk)`. Protection descriptor `D = U providerId || U algorithmId || U keyId || B nonce || H configurationHash`; IDs are nonempty, each ≤1 KiB UTF-8, nonce length is 1..256, and configurationHash selects the exact approved provider/options/dependency binding. Protection uses authenticated associated bytes `A = "HX-EV-TIMELINE-AAD-1\0" || 01 || B C || H PH || B D`. The registered bounded authenticated-encryption provider returns **separate** ciphertext bytes and authentication tag (tag length 1..256); its pinned algorithm defines nonce/tag lengths and nonce uniqueness per key. No-op protection is never encrypted private storage. Raw ciphertext length ≤1 MiB+4 KiB is checked from a proved provider bound before allocation; unavailable/decryption-key/nonce-capability support returns `TimelineProtectionHold`. This condition may be satisfied by an existing qualified provider; no new Epic 8 engine or cipher is required. Inability to provide it holds durable timeline mode.

Canonical chunk object `O = "HX-EV-TIMELINE-CHUNK-1\0" || 01 || B C || H PH || B D || B ciphertext || B authenticationTag`. Its ciphertext-object hash is `CH = SHA256("HX-EV-TIMELINE-CIPHER-1\0" || 01 || B O)`. Thus CH binds exact cipher **and** all protection metadata, associated scope/page/offset and tag; PH separately binds plaintext and scope. Chunk object ≤2 MiB, charged before serialization/provider copy. Immutable chunk key is `replay-timeline-chunk:` plus lowercase CH, within the tenant/operation namespace; lookup verifies full C, so hash equality is never permission to cross scope. Encryption may be randomized only on first preparation; exact prepared object bytes are pinned and reused after any save ambiguity, never re-encrypted as an exact retry.

Manifest `M = "HX-EV-REPLAY-TIMELINE-MANIFEST-1\0" || 01 || 000c`, tags `01..0c`: `B S; N pageStart; I pageCount; H SelectedRequestHash; H SHA256(P); H SHA256(L); N length(L); I pageEntryCount; I chunkCount; B orderedChunkRows; H P.resultingTimelineHash; H P.successorCanonicalStateHash`. Chunk rows are `u32 chunkCount || each (I index || N plaintextOffset || I plaintextLength || H PH || H CH || U immutableChunkKey || I exactObjectLength)` in index order. Manifest ≤2 MiB; chunk count 1..96; exact object length is positive and ≤2 MiB; total cipher-object capacities are also reserved in B6’s shared 1 GiB durable quota. No CH is interpreted as a plaintext hash. The ledger pointer authenticates **SHA256(exact M)** with P/response/successor; M binds P, which binds L but not M, avoiding a hash cycle. Prepared chunk or M existence alone grants no progress. The zero-list hash is fixed even though scope-bound PH/CH vary.

Recovery obtains M only via the committed operation pointer, verifies exact hash/codec/tags/caps/S/P/request, row count, gapless indices/offsets and deterministic split before any large allocation, then obtains each exact O by its scoped key. Verify length/CH/C/PH/D and provider binding; authenticate/decrypt with A into a reserved bounded window; verify PH; feed original plaintext in order through L framing, E schema/sequence and state-hash checks. Reject extra/missing/reordered chunks, cross-operation swaps, wrong metadata/tag, unavailable key, duplicate row, unknown version, trailing bytes, offset/length overflow, count mismatch or incorrect SHA256(L)/timeline/successor as `TimelineEvidenceHold` (`TimelineProtectionHold` for unavailable protection capability). No next token/final state, no Apply rerun and no quota release while recovery is uncertain. Retain old committed authority and reconcile B4; never regenerate missing bytes under a spent token. LB-11 supplies literal manifest bytes/hashes and corruption cases using opaque **noncryptographic** chunk values; only VB-17 can establish real provider protection/readback.

### B5. Successor ownership and last-good state

**BH37-5 accepted.** `ReadOnlyMemory<byte>` is a read-only view, not ownership. Replace draft §6's validation-then-async-save wording with this precise contract while preserving its proposed method signature:

`WriteSuccessorAsync(byte[] priorHandle, ReadOnlyMemory<byte> canonicalState, string serializerId, CancellationToken token): ValueTask<byte[]>` has a **synchronous, non-async capture front end**. Before returning any `ValueTask`, before scheduling deferred work, and before its first suspension it authenticates/copies the handle, validates serializer identity, reserves the B6 total capacity and copies canonicalState exactly once into a privately owned capacity. It then validates canonical syntax/schema/length and hashes **that private copy**, never the caller's memory. The caller must hold input stable for the synchronous copy; there is no claim of atomic capture from concurrently mutating unsafe/native memory. Mutation before capture can only affect the bytes subsequently validated; mutation after capture cannot affect the validated successor. No save may retain the caller's Memory, array, MemoryManager, owner, pin or pointer. Unknown capacity/memory provenance that prevents a bounded copy holds before allocation.

Only the private sealed owner enters asynchronous save. Save/readback hashes the same bytes validated above, verifies registered state schema and matches the page's successful Apply result and B4 terminal timeline state. No getter, returned handle, DTO, handler or continuation exposes that owner. The method returns only a copied opaque successor handle after create-once CAS/readback; one `(scope, OperationId, pageStart, priorGeneration)` admits one successor. Changed successor input under the same identity conflicts; an exact save retry uses the retained private sealed bytes, never a second serialization or Apply. A wrapper that merely awaits then copies is invalid. Private payload input/upcaster output retain draft §3's non-escaping copy facade and one-shot bounded writer rules.

`ReadPriorAsync` returns a separate charged defensive copy. Mutating it cannot affect the authoritative private prior state. Before each Apply, retain immutable canonical last-good bytes and use a separately reconstructed working object or an equivalently proven isolated copy; arbitrary mutable Apply cannot touch the retained state. If Apply mutates then throws, discard the working graph, any attempted timeline entry and successor, report the failure at its exact sequence and retain the last successful canonical bytes/sequence. Do not serialize the damaged working object as last-good. On a failed page, the last **committed page** remains durable authority; additional successful events on the failed page may appear only as bounded private diagnostic last-good evidence, never a checkpoint or next token.

All distinct live copies (caller buffer while live, prior canonical bytes, defensive prior copy, typed working graph, sealed successor, timeline entry, serialization staging and provider copy) count under B6. Release/zero the full actual private capacity only after all asynchronous readers and reconciliation obligations have ended. A committed transition invalidates the old handle immediately, but frees old bytes only after their retention obligations close; invalidation is not permission to return an owner still being read to a pool. Cancellation before durable prepare leaves no successor authority; cancellation or exception after a save starts retains sealed bytes and invokes B4 reconciliation with a bounded recovery token, independent of the cancelled caller token. Allocation/provider failure cannot silently substitute caller memory or rerun Apply.

### B6. Compositional admission, retention and capacity holds

**BH37-4 accepted.** Draft §8 and 6.5a A3 define individual ceilings; their maxima do not prove a complete execution fits. Keep them and require an operation-wide **phase reservation plan** before raw materialization, Apply or async capture. Use checked integer arithmetic, deterministic allocator bucket upper bounds and actual capacities, not `Length` alone. An unknown bound is a hold before the corresponding work, never an optimistic allocation/OOM strategy.

For each phase p, require `R(p) ≤ 128 MiB` for distinct immutable original raw-source capacities and `W(p) ≤ 128 MiB` for the sum of **all simultaneously live pipeline working capacities**. W includes copied raw/DTO bytes, readable/effective copies, proof/claims/metadata, caller-owned inputs retained by the operation, prior private state and defensive copies, pre-Apply last-good checkpoint, typed event/working graph with measured conservative bound, hop validation buffers, sealed successor, timeline buffers/manifest, crypto, serialization/Base64/JSON and provider buffering. A byte owner belongs to exactly one pool; copied raw bytes are W, not an extra R allowance. Aliased views are charged once only with a documented same-owner lifetime. A content ceiling (64 MiB page/proof/state/timeline) never adds another memory pool. Reserve every phase's peak and transitions where old/new buffers overlap, including cleanup/zeroing; do not release charge before its last use finishes.

The bounded legacy complete array remains a distinct at-most-256 MiB accounted owner set with at most 100,000 events and 64 MiB readable payload, charging stored/readable/effective distinct capacities, encoded metadata/extensions and 8,192 bytes/event. Active W remains capped at 128 MiB alongside that array; document this separate allowance and reserve both. Never move arbitrary replay state or duplicate page buffers into the array allowance to evade W. Every pipeline request also reserves its total R+W+legacy-array peak against the deployment's measured process/concurrency budget; plugin-local allocation is not magically bounded by these APIs and needs code/dependency audit plus measured admission. No process-memory guarantee is claimed for unaudited domain code.

| Admission example (MiB unless stated) | Deterministic result before Apply |
| --- | --- |
| Readable event 64 + retained prior state 64 + any positive proof/working/successor capacity | W exceeds 128. `ScratchLimit` and route readiness hold; individual legality is insufficient. Even without copies, proof makes 128+epsilon. |
| Event 64 + prior 32 + isolated working state 16 + successor 8 + proof 2 + parser/serialization/timeline windows 4 | W=126, admissible only if these are conservative **actual capacities** and no omitted caller/provider copy exists. One additional 4 MiB copy makes W=130 and holds. This arithmetic is an illustrative reservation, not evidence a CLR route has those bounds. |
| Pooled request of 1 MiB+1 bytes with deterministic 2 MiB bucket | Reserve/charge 2 MiB before rent. Unknown bucket or actual capacity above reservation blocks allocator activation. |
| State 64 plus timeline 64 in one final JSON response | At least Base64 expansion may exceed 128 MiB before metadata/proof; reserve exact pinned serializer's conservative encoded bound and check actual output. `FinalResponseLimit`; start a new state-only page-1 operation only when requested/authorized by the replay workflow. |
| 100,000 empty events in a legacy array | Per-event charge alone is 819,200,000 bytes, over 256 MiB. `LegacyArrayLimit` with no partial array. |

A legal 64 MiB event with 64 MiB state may run only through a **separately attested bounded route** whose complete phase plan proves lower concurrent capacities using streaming deserialization/Apply or charged encrypted spool and small read windows. Current synchronous CLR Apply/whole-object serializers provide no such proof. If no proven route fits, hold before Apply/readiness even when page count is one; shrinking cannot fix that case. Neither raising 128 MiB silently, treating prior state as free, nor moving plaintext to disk closes this finding. Admitted route bounds/capability identity are shared deployment evidence and influence 6.5c routing.

Before each page, atomically reserve continuation index entry and full conservative request/response/final/timeline/successor storage plus overlapping prepared/committed generations. Keep draft index limits 65,536 entries/64 MiB, exact request and response blobs at most 128 MiB **each**, and charge **all** new replay private-state/timeline/manifest storage to the same **shared 1 GiB combined replay storage quota** (and any smaller configured tenant/store quota), rather than inventing an unlimited spool pool. A final result referencing existing immutable bytes need not duplicate them; actual copies are separately charged. Each stored canonical state is at most 64 MiB; timeline at most 1,000 entries/64 MiB canonical state bytes; overhead still counts. Hold with `ContinuationCapacityHold` before Apply when quota is unavailable. Exact final Web JSON (including state, optional timeline, proof, escaping/Base64 and framing) is at most 128 MiB; before final Apply reserve a proven conservative upper bound and verify actual encoded size before commit. New timeline entries exceeding their own bound return `TimelineLimit`, preserving prior authority. A valid source that fails a new composition/proof/provider cap is a readiness hold, not poison.

**Shared quota boundary:** one durable pool for the authenticated `(deployment identity, canonical replay backend descriptor)` across every gateway/receiver replica, tenant, route, operation and retained generation using that deployment/backend. The backend descriptor is the draft §5 canonical backend identity (including namespace/store), never a friendly component alias or process ID; deployment identity is the stable authenticated replay-retention namespace shared by those replicas, pinned in the deployment capability, **not** its changing code hash/revision or process incarnation. Rolling upgrades, restarts and capability changes retain the same pool. Moving that namespace/backend requires a fenced migration that carries every reservation/retained generation and accounts any overlap before admitting destination work; renaming cannot reset the allowance. The 1 GiB ceiling is the sum of live reservations and allocated replay **and B9 export** objects, including original/overlapping pins, sealed successors, staged/final timeline or export chunks, responses, export completion pointers, recovery/quota metadata and all retained old/prepared/ambiguous generations. It is **not** reusable per operation or replica. Per-tenant/store sublimits are intersections with this shared ceiling, never separate extra allowances. B6 narrows the retained draft §6 shared request/response pool accordingly; 6.5a’s independent writer capsule quota grants no additional replay storage.

Under one linearizable durable quota fence, reserve the conservative added bytes and index slots against shared and applicable tenant/store counters **atomically before allocation or Apply**. Key each immutable reservation by admitted scope/OperationId/page/owner generation and exact object plan hash; exact retries reuse it, changed plans conflict, and conversion from reserved to allocated counts each owned byte once without a release gap. Restarted operations still share the pool with retained predecessors. Cross-object-store allocations require this same qualified shared reservation fence; without it return `ContinuationCapacityHold`. An ambiguous reservation acknowledgement admits no allocation until authenticated reservation readback proves it. An ambiguous object write/delete retains its full charge. Recovery fences new reservations until it has reconciled durable counters, outstanding reservations and all retained object generations; it never resets an in-memory counter or treats lost ownership/TTL as free capacity. Refund only after durable no-future-write evidence, end of every reader/retry obligation and authenticated deletion/key-revocation readback, updating the counters once under the same fence. New admissions hold if reconciliation or counter capacity cannot be proved. LB-18 models shared competing reservations/recovery; VB-14 requires multi-replica provider proof.

Retain exact request, response, P, successor hash evidence, timeline manifests/chunks, transcript and final result at least 24 hours and longer through every retry, rollback, backup, proof, ambiguous-commit and deletion obligation. Tokens last 15 minutes; final exact-pin lookup precedes token-expiry rejection within retained obligations, but expired tokens grant no new Apply. Current proof-key/fingerprint mismatch needs fresh authenticated replay, not re-signing the same request. Encrypted private scratch/spool requires scoped access, authenticated bytes, retained recovery keys where recovery is promised, full memory zeroing and read-back-proven ciphertext deletion/key revocation. Ambiguous deletion retains quota. Unreferenced prepared objects are reclaimable only after durable no-future-commit evidence. Index exhaustion cannot evict live retry evidence; return a capacity hold before work.

Retain draft §8 named-index bounds: 512-byte logical key, 4 KiB leaf/progress record, 1,000,000 members, 1 GiB each complete encoded row/progress index, 4,096 changed keys/event and 128 MiB live construction scratch. Copy only authenticated changed sparse-tree paths. Retained roots/tombstones/old handler versions remain until query, retry, rollback, TTL, backup and receipt obligations close; deletion/compaction is fenced and readback-proven. Keep one active TTL job per finite leaf, at most 1,000,000 jobs/1 GiB active index, shared ordinal hot log ≤1 GiB, archive segments ≤64 MiB. Extend the existing **64 GiB retained named-state/archive ceiling** to the combined encoded/ciphertext/metadata capacities of every retained named root, physical row/version, sparse/progress node, tombstone/delete witness, retained proof/handler evidence, TTL job and hot/archive record across **all** operations and generations in the same stable authenticated deployment/backend scope above. All smaller component/index/segment/tenant/store caps still apply. Use B6’s existing atomic fenced reservation, durable recovery and no-early-release rules before allocation or root publication; namespace, deployment revision or generation changes cannot reset this ceiling. Count a shared immutable object once only when authenticated ownership/reference-lifetime evidence proves the same physical owner; count distinct copies separately and retain its charge until the last obligation closes and deletion is proved. Unknown sharing/retention accounting holds with `NamedIndexLimit`/maintenance capacity hold. Reserve capacity before a new root/ordinal, never drop evidence to fit. `NamedIndexLimit`/maintenance capacity hold preserves the prior certified root. Reclamation advances no source-event sequence.

### B7. Projection capability, durable checkpoint and named query visibility

Reuse draft §§3, 5–6, 8–9; B1 shows why current full-history paths and marker-gated reads do not establish these guarantees. Explicit route capability has two independent axes: **verified current-payload support** and **fold mode**. Being asynchronous or implementing `IVerifiedDomainProjectionHandler` alone does not prove incremental folding. Add a capability row to the draft catalog for fold mode: tag `5a`, primary key `U domain || U HandlerRouteId`, u16 count `0003`, tags `01` U mode (`full-replay` or `incremental`), `02` U prior-state codec ID, `03` H immutable capability/options manifest hash. Full-replay uses empty codec ID; incremental requires a registered codec and authenticated prior-state/root intake. Include this row in HandlerCatalogFingerprint and append it after the route's `59` rows in HandlerCompatibilityHash; its selected dependency closure is attested. Absence means legacy full-replay and appends no hash bytes, preserving historical fixtures. Mode change is semantic handler compatibility change requiring rebuild or approved migration. Because this candidate admits one prior named root/session, an `incremental` named route must have **exactly one `59` key-space declaration**. A route-wide incremental capability with multiple declared key spaces holds readiness and returns `ProjectionPriorStateHold` before invocation, even when one incoming request names only one space; do not pick a declaration locally. This restriction leaves full-replay routes and the zero-`59` aggregate single-state/CanonicalState path unchanged.

For an **already-current** projection, first read authenticated head metadata, route capability and the same route's checkpoint/state/root proof. When checkpoint=head and transform/handler/backend/progress/readback all match, use draft §6's exact zero-route checkpoint prefix. Return metadata-only completion with **zero event reads, zero Apply/handler calls, zero state writes and zero checkpoint advancement**. Metadata/proof reads are permitted. A free sequence counter or unverified state cannot use this shortcut. A stale/absent root, checkpoint ahead of head, capability mismatch or invalid proof holds/rebuilds; it cannot claim Current. Empty head=0 requires proven empty genesis/model semantics and no fabricated nonempty checkpoint.

For a lagging incremental route, admit only contiguous tail pages from the certified prior checkpoint through one fixed head. The additive `VerifiedProjectionPriorState` below discriminates canonical state from a certified named root and supplies exact authenticated checkpoint/readback and immutable intake. A free bytes-or-root DTO is insufficient. `VerifiedProjectionRequest` gains a nullable property of this type; its constructor still grants no authority and dispatch validates it under the fence. An incremental handler requires it (authenticated genesis is the only empty exception); full-replay rejects it as a tail shortcut. Capability checks happen before fingerprint/dispatch on every live, retry, history, reconcile and shared-rebuild route. Unknown/hopped events require the verified current-type route set; no fallback to stored CLR names or single-primary-type assumptions.

#### B7a. Exact prior-state contract and pinned intake

Proposed public `enum VerifiedProjectionPriorKind : byte` has exactly `EmptyGenesis=0`, `CanonicalState=1`, `CertifiedNamedRoot=2`; reject unknown values. The public sealed `VerifiedProjectionPriorState` has these get-only properties (all strings required unless marked nullable; all byte arrays copied at construction and on return, under B6; no setter/owner alias):

| Members | Exact public types and meaning |
| --- | --- |
| `Kind` | `VerifiedProjectionPriorKind`. |
| `TenantId`, `Domain`, `AggregateType`, `AggregateId`, `HandlerRouteId`, `BackendIdentity`, `OperationId`, `CoordinatorId` | `string` each. Scope is the addressed source even for shared ownership; backend matches the `52` route configuration. OperationId is this dispatch, never the prior state's operation. |
| `KeySpaceId`, `OwnershipMode` | `string?` each, present together only for a named-key-space input and exactly matching its `59` row; OwnershipMode is `aggregate` or `shared`. Both are absent for single-state input, never empty strings or a fabricated key-space. |
| `CompletedSequence`, `RootGeneration`, `FenceGeneration` | `long` each, nonnegative signed N bounds. Sequence is the addressed source progress at that root; generation is provider-certified visibility generation (zero only for authenticated genesis). Canonical-state mode sets RootGeneration equal to the authenticated immutable state-pointer’s FenceGeneration; it does not invent a provider visibility counter. |
| `RegistryFingerprint`, `EventTransformHash`, `HandlerCompatibilityHash`, `StateReadbackHash` | `byte[]` each exactly 32; current proof and compatible state requirements are B8. |
| `CheckpointCarrier`, `ReadbackEvidence`, `GenesisEvidence` | `byte[]?` each. Checkpoint is exact signed purpose-04 carrier, readback evidence is the exact provider-authenticated pointer/state or named root/prepared-certificate/final-receipt evidence required by draft §6; no private alternate proof format. GenesisEvidence is the exact authenticated B3 empty-prefix EventEvolutionProof container; ReadbackEvidence separately proves empty root/state visibility under the intake fence. |
| `CanonicalStateBytes`, `CertifiedRootBytes` | `byte[]?` each; exactly one per non-genesis discriminator, both absent for EmptyGenesis. State bytes ≤64 MiB; root bytes are exact draft §6 canonical named-rowset root, no reserialized object. |
| `StateSerializerId`, `StateVersionKey`, `StateVersion`, `PriorStateOperationId` | `string?` each, present together only for CanonicalState; must equal checkpoint/state-pointer/readback evidence. |

`CanonicalState` represents an **aggregate single-state** route with a matching `52` row and no `59` named-key-space declaration for that route. It requires CanonicalStateBytes, all four state strings, CheckpointCarrier and ReadbackEvidence; KeySpaceId/OwnershipMode/CertifiedRootBytes/GenesisEvidence are absent. Bind TenantId/Domain/AggregateType/AggregateId/HandlerRouteId and BackendIdentity directly to `52`’s backend-bound handler/readback configuration, purpose-04 checkpoint and draft §6 `HX-EV-CHECKPOINT-STATE-POINTER-1` immutable state key/version/operation/fence/body-hash evidence. RootGeneration equals that pointer’s certified FenceGeneration as above. Its absence of key-space does not create named-key access. A named route must use CertifiedNamedRoot; it cannot relabel one leaf CanonicalState. `CertifiedNamedRoot` requires KeySpaceId/OwnershipMode, CertifiedRootBytes, CheckpointCarrier and ReadbackEvidence; CanonicalStateBytes/all four state strings/GenesisEvidence absent. Its root/progress proof must contain the addressed stream’s exact checkpoint and CompletedSequence. `EmptyGenesis` requires CompletedSequence=RootGeneration=0, GenesisEvidence and ReadbackEvidence. For a declared named route it also requires matching KeySpaceId/OwnershipMode and a certified empty named root in ReadbackEvidence; for a `52` single-state route without `59`, both are absent and ReadbackEvidence proves empty single-state visibility under its backend/checkpoint fence. All other nullable members are absent; it is legal only for a backend-authenticated empty route/model and zero addressed progress, not merely a missing checkpoint on an existing shared root. All required common hashes still describe the registered empty model/readback. An existing shared root with a new source lacking a certified addressed checkpoint is outside this incremental intake: `ProjectionPriorStateHold` before invocation and authenticated full rebuild or a separately approved genesis-intake extension. Do not invent a purpose-04 checkpoint or treat source absence as an empty shared model. Null and empty bytes/strings are distinct; absent serialized nullable properties are canonical and explicit null is accepted only as the transport representation of absence. No alternative member combination is accepted. Non-state evidence including root/checkpoint/carrier ≤8 MiB combined; total intake copies/state/proof/working storage must fit B6 before allocation. Canonical serializer, root and pointer formats remain the existing draft codecs; this typed descriptor adds no independently authoritative signature. ReadbackEvidence uses the exact provider evidence codec ID/options already attested for this route in the draft’s readback/backend catalog: that provider must expose deterministic bounded decoding into the required pointer, prepared-certificate/final-receipt and state/root objects. Unknown/ambiguous evidence codec or missing required object holds intake; never guess concatenation or treat a public byte array as authenticated readback.

Add nullable init-only `VerifiedProjectionPriorState? PriorState` to `VerifiedProjectionRequest`, preserving its existing constructor. Handler code additionally receives a gateway-installed, nonserializable `IVerifiedProjectionPriorReadSession? PriorReadSession` get-only property of that request; public DTO construction cannot install a session. It is required only for CertifiedNamedRoot, absent for CanonicalState/EmptyGenesis. The public session interface is `IAsyncDisposable` with `GetAsync<TValue>(string logicalKey, CancellationToken cancellationToken): Task<ReadModelEntry<TValue>>` and `GetManyAsync<TValue>(IReadOnlyList<string> logicalKeys, CancellationToken cancellationToken): Task<IReadOnlyList<ReadModelBulkEntry<TValue>>>`, both `where TValue : class`. No store/root/route override is accepted by those methods. Generic types are allow-listed by the handler’s state schemas; private input bytes are validated before materialization. After disposal/failure/cancellation every method rejects with `ProjectionPriorSessionClosed`.

For CanonicalState, dispatch uses the `52` backend/checkpoint fence and certified immutable state pointer in place of a named root/progress path; it installs no named prior-read session and deserializes only the admitted private canonical bytes. The corresponding EmptyGenesis variant uses that same single-state fence; named inputs use their declared key-space fence. Dispatch reserves applicable scope/key/proof and state/root leases, then under the draft’s **same linearizable route/key-space or single-state checkpoint fence** authenticates principal/handler capability, current pointer, exact checkpoint/readback, progress path, expected generation and hashes **before admitting the tail or calling the handler**. It installs one private immutable root-scoped session tied to those bytes, this dispatch OperationId and fence generation. Session reads use that exact root and immutable physical row versions with membership/absence/TTL proof, never the ordinary “latest root” named-query method or generic physical-key API. They enforce the exact handler-authorized key space, 256 keys/read, 8 MiB proof, 64 MiB result and combined live 128 MiB budget; returned objects are defensive working copies, not root owners. Reads do not change the admitted checkpoint or widen the tail.

A concurrent root advance detected before invocation yields `ProjectionPriorConflict`: zero handler calls, discard unadmitted tail, reread one consistent checkpoint/root and re-admit from its sequence (at most three pre-invocation retries, then `ProjectionPriorStateHold`). During execution, immutable leased root reads may finish against the original root only. If the lease, proof, TTL or root becomes unavailable, return `ProjectionPriorStateHold` and discard the candidate; never substitute a newer row. Before any state/effect publication revalidate current pointer, generation, complete source range and capabilities under that same fence, **and authoritative coordinator UTC against every TTL-bearing row the handler used**, even if generation is unchanged; a changed root is `ProjectionPriorConflict` with **no candidate publication/checkpoint advance**. For each contributing read retain a deduplicated private read-set entry naming exact root/hash/generation, logical key, leaf/absence path, physical version/readback digest, original visibility-generation evidence and checked expiry (or proved no TTL). At the final publication fence, any expired row (`now >= expiry`), unverifiable time/readback or lost retained lease is `ProjectionPriorStateHold` (safe expiry reason `ReadModelExpiryPending`), with no candidate effect/checkpoint; trigger/observe certified maintenance without substituting its newer row. This final check and pointer publication share the qualified fence’s linearization decision; providers unable to enforce expiry there cannot advertise incremental intake. Read-set capacity, including keys, proofs and time evidence, stays inside the same 8 MiB total prior/read proof and B6 128 MiB live working budget across **all** reads, not a fresh allowance per call; reserve before the first read and refuse an addition before using its row. Retain evidence until fenced publication or definitive no-publication/reconciliation, retaining referenced durable evidence under B6 obligations; clear/release private copies only when no reader/validation user remains. A separately admitted new dispatch may recompute from the new root only after proving no old effect/commit can still occur. Same-operation retries reconcile exact prepared/result receipts and do not rerun a completed or ambiguous handler. Incremental computation therefore stages effects privately until its fenced commit; providers/handlers unable to enforce that restriction cannot advertise this capability. Retain prior root/row/proof leases until all reads, candidate reconciliation and rollback obligations end. This deliberately differs from ordinary latest-root query retries. VB-18 exercises both pre-handler and commit-time races; LB-12 models the pinned-generation decision and LB-16 models final TTL/read-set admission; neither proves provider time/fencing.

A full-replay handler receives a complete authenticated prefix from sequence 1, bounded by the legacy array limits when it requires an array, or a declared private staged full-replay protocol. It must not treat one tail page as the whole model. If complete input cannot fit and no incremental/staged capability is proved, hold readiness. An incremental capability is required to claim cost proportional to page/tail size. This preserves the current orchestrator's full-replay meaning while specifying the explicit future optimization gate.

Handler `Completed` requires valid state and the retained draft §§3/6 durable completion receipt/readback bound to the exact tenant/domain/aggregate type/ID, HandlerRouteId, authenticated EventEvolutionProof page/prefix and contiguous completed sequence/accumulator, dispatch operation and state version/fence. For named batches, `NamedBatchScopeBytes`, BatchId (= root OperationId), original authenticated source-proof hash/source-transition intent, exact logical-operation witness and BatchStateReadbackHash must match the prepared root/certificate, separate final-pointer commit receipt and purpose-04 checkpoint/readback. Single-state completion binds the same source-prefix/operation to its immutable state version and checkpoint-state pointer. Aggregate-replay P is B4-only and is **not** a projection receipt or transition. `Completed` is accepted only after these exact bindings verify; `AlreadyCompleted` retrieves that same completed state/receipt, never re-Applies. `Retryable`, `Indeterminate`, `Failed` or missing state/proof has no success body/checkpoint advance (draft §3's typed mapping). A projection can durably advance through a verified contiguous page with proven state, but `Current` requires reaching the admitted head. Deterministic poison stops at that event; durable AD-31 evidence does not authorize skipping it. Keep last complete model and last contiguous proved checkpoint; stale/rebuilding/degraded status must reflect actual authority.

For named read models use draft §6's full route/key-space root, source-progress sparse tree, operation-bound prepared certificate and **separate provider-issued final-pointer commit receipt**. Prepare immutable rows, deletes, indexes and source intent; hash root before signing checkpoint; one fenced pointer CAS publishes both. Preserve acyclic previous-checkpoint references. Actual final-pointer ETag/time belong to post-CAS provider evidence, not guessed precommit fields. Before Current, authenticate exact root/checkpoint/state readback and receipt under canonical backend identity. Across stores, require the shared linearizable compare-and-write fence; otherwise `CheckpointFenceUnavailable`, prior immutable pointer/state remain authoritative. Prepared/shadow rows are never directly query-visible.

Add route-aware named reads; retain current `IReadModelStore` API and marker semantics for **non-versioned** keys. A startup-frozen key-space guard covers all legacy reads/writes, bulk, batch, TTL, delete and physical-generation paths. A versioned key needs the authorized scoped adapter or `ReadModelRouteContextRequired` before access. Migrate `SharedProjectionEpochCoordinator` physical operations through that same adapter; it is neither a bypass nor permanently blocked by its own guard. Descriptive `NamedProjectionReadScope` grants no authorization: validate the principal-bound capability, tenant/route/key-space/backend/keys/operation, expiry and catalog revision before root lookup. Read a single certified root, prove requested membership/absence and exact row/delete bytes, then recheck root generation and authoritative UTC under the fence before returning any values. Retry changed roots at most three times, then hold; TTL expiry holds for certified maintenance. GetMany returns one generation in request order, no duplicates/partial result. Retain limits 256 keys, 8 MiB proof, 64 MiB result, 128 MiB combined live memory and the draft's exact-key capability quotas; never broaden exact grants to prefix access.

#### B7b. Derived query responses and cache capacity

At **QueryRouter and CachingProjectionActor**, versioned queries **bypass result caching entirely** in this candidate, including cached single-leaf responses. Existing `ExecuteQueryAsync` can return arbitrary derived response bytes; a row-membership proof does not establish that response. This explicitly replaces the earlier cached-value-membership proposal. No result-binding codec is claimed or silently synthesized. Determine versioned key-space/route from the authenticated frozen catalog **before cache lookup**. Never return or normalize a previously cached versioned payload/Current label. On classification/activation change, make matching old entries inaccessible atomically and evict/clear their private retained buffers after active readers finish; ordinary ETag equality cannot keep them visible. Uncertain route classification is `ReadModelRouteContextRequired`, never a cache fallback. Non-versioned cache behavior remains source compatible.

Every versioned request reauthenticates the current principal and endpoint/capability, executes the currently attested allow-listed query implementation/options/dependencies against authenticated root-scoped input, and then performs B7’s final root-generation/UTC/authorization check before returning its privately computed bytes. All reads contributing to a result share the admitted immutable root, even for computed totals or formatting. B7c/B7d supply the exact catalog and dispatcher-installed input session for this rule. This candidate admits one root only; a multiple-root query is `ReadModelQueryConsistencyHold` before execution. Independently verified roots are insufficient; no common-fence root-vector capability is invented here. Query route/code/options/dependency bytes are checked against the active deployment catalog before execution and again at completion; drift during execution is `QueryCapabilityChanged`, with no result (a newly admitted query may use the new code). A query-code-only update therefore executes the new registered code even if root, ETag, event transform and projection handler hashes are unchanged. No source-root membership assertion is made for the **derived** response itself; its authority is current authenticated execution and final fenced input validation. Missing source proof/key/permission still holds or denies the query with no cached response.

Versioned retained cache budget is exactly **0 entries, 0 payload bytes, 0 proof bytes** after a request, per actor and process; cache admission is always “bypass.” This is an explicit bounded policy, not reliance on the current 32-entry count. Any legacy entry newly classified versioned is quarantined from lookup until safe eviction; account its actual buffer capacity against the existing process reservation until the last borrower exits, clear full private capacities, then release its charge. A borrower may not finish a versioned response from such an entry after activation; it restarts authenticated execution. New versioned requests do not reserve cache capacity, copy data into retained cache or require cache eviction to succeed. Retention refusal/saturation yields ordinary uncached execution, never a cache-capacity query failure. Only the independently applicable query working limits can reject valid execution.

Per-request read/proof/derived-response capacities remain ≤128 MiB combined, with ≤8 MiB input proofs and ≤64 MiB final response (including encoding), charged **before** allocation with actual owner capacity and caller/provider overlaps. Reserve root leases, serializer and derived aggregation working space too; large partial result streams remain private until final checks. Release/zero private response/proof owners when transport readers finish, and release root leases after the last source/read/final-check user, including cancellation; no response owner is transferred into a cache. Infrastructure-owned durable root/proof retention continues under B6 and is not a query-cache byte allowance. VB-19/LB-13 cover code-only drift and saturated/forbidden retained-cache admission; they do not claim that a toy cache proves production allocation behavior.

#### B7c. Authenticated query catalog and dispatch mapping

Draft §5's event and projection rows do **not** bind query implementation or query-to-key-space mapping. Add one query catalog row tag **`5b`**, primary key `U domain || U queryType`, u16 field count `000f` and ascending tags `01..0f`:

| Tag | Exact scalar and meaning |
| --- | --- |
| `01` | U query implementation ID, the exact internal CLR assembly-qualified handler type resolved by the admitted DI registration. |
| `02`, `03` | H query implementation assembly-file bytes; H fully expanded canonical query options. |
| `04`, `05`, `06` | U DI registration implementation ID; H registration assembly; H canonical registration options. |
| `07` | U authenticated serving endpoint identity, not a caller URL. |
| `08`, `09` | U registered request codec ID; H exact request schema/codec descriptor below. |
| `0a`, `0b` | U registered response codec ID; H exact response schema/codec descriptor below. |
| `0c`, `0d`, `0e` | U bounded read-plan resolver implementation ID; H resolver assembly; H canonical resolver options. |
| `0f` | B ordered declared root-input bindings below. |

Use draft §4 framing and §5 row semantics without a new row separator/version: `5b || U domain || U queryType || 000f || 01+U ... || 0f+B ...`. H is exactly 32 SHA-256 bytes. Keys use the exact registered **1–64 ASCII-byte** lowercase kebab-case domain/query discriminator, matching `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`. Implementation, registration, request/response codec, resolver and endpoint identifiers (tags `01`, `04`, `07`, `08`, `0a`, `0c`) must be nonempty and resolve uniquely to the attested local registration/descriptor or configured authenticated endpoint; empty, unknown or ambiguous identifiers hold readiness. Startup rejects duplicates under both ordinal keys and the existing `OrdinalIgnoreCase` dispatch comparison; one admitted descriptor maps the envelope's domain/query route to exactly one handler registration and endpoint. No CLR discovery fallback, assembly-name auto-load or user-supplied endpoint is allowed for a versioned query. Compatible input casing is resolved through that unique frozen table to the canonical key before admission, preserving legacy route matching; authenticated scope and the catalog comparison then use that canonical key.

Each referenced request/response schema descriptor has exact bytes `U serializerId || H serializerAssembly || H serializerOptions || B schemaDescriptor || U validatorId || H validatorAssembly || H validatorOptions`, no omitted fields or trailing bytes. Its codec ID must resolve to that exact descriptor and immutable implementations/options at startup. Canonical option/default and schema descriptor bytes use the same frozen manifest rules as draft §5; unknown codec/options, unresolved files or dynamic load edges hold readiness. Descriptors and options are retained as bounded content-addressed manifest inputs, not accepted as unexplained hash values.

The `0f` B is `u32 bindingCount || each B binding`, with `1..256` bindings. A binding is exactly `U HandlerRouteId || U KeySpaceId || U ownershipMode || U stateStoreId || B canonicalBackendDescriptor || U aggregateType`. It must match one same-domain `59` declaration and its backend-bound `52` route; ownership is exactly `aggregate` or `shared`. AggregateType is the exact registered source aggregate type for aggregate ownership and the empty U for shared ownership. These are root **templates**, not a pinned runtime root hash: tenant comes only from authenticated request authority; aggregate IDs and exact logical keys come from the admitted resolver below. Sort bindings lexicographically by their separately encoded components (unsigned UTF-8 bytes, backend descriptor as exact bytes); reject duplicate tuples, empty required names, a mismatched backend/store/prefix/ownership, unknown route or trailing bytes. A declaration prefix classifies keys; it grants no prefix authorization. A versioned query over an undeclared/single-state physical source is outside this named-root intake and holds before invocation, never invents a `59` declaration.

At admission the attested pure, bounded read-plan resolver accepts the validated immutable QueryEnvelope/parameters and authenticated tenant/principal scope and returns the complete set of exact `(binding, aggregate ID if aggregate-owned, logical keys)` needed by that invocation. It performs no source reads, effects or external lookups; its code/options/schema closure is cataloged. Reject unknown bindings, cross-tenant substitutions, keys outside the declaration, duplicate keys, ambiguous aggregate mapping or a plan requiring more than 256 distinct keys **in total** before root lookup. Charge plan/parameter copies and validation scratch under B7b. Every key needs the existing principal-bound exact-key capability; a resolver cannot mint it. Data-dependent reads outside this pre-admitted set hold with `ReadModelRouteContextRequired`; they do not widen a grant during handler execution. This deliberately bounded rule requires a separately admitted plan if a route cannot declare its input set.

Include `5b` rows in **HandlerCatalogFingerprint**, sorted by tag then separately encoded `(domain, queryType)` keys, with the existing row count. Extend the draft §5 sealed G root set to query implementation, registration adapter, read-plan resolver, request/response and mapped input-row CLR materializer/serializer/validator, scoped query adapter/readback and their transitive managed/native dependencies. These G rows enter RegistryFingerprint by its existing deduplicated rule. Define `QueryCompatibilityHash = SHA256("HX-EV-QUERY-ROUTE-1\0" || 01 || B exact5bRow || u32 inputCatalogRowCount || each B selectedInputCatalogRow || u32 selectedGCount || each B selectedGRow)`. Input rows are the deduplicated exact `52` and `59` rows referenced by `0f`, sorted by draft tag/key order; selected G rows are the union reachable from these input/readback and query roots, sorted by their draft primary keys. No per-process subset or guessed closure is admitted. This is a catalog digest, **not** a new signature or result proof.

The already authenticated sealed per-domain deployment capability pins RegistryFingerprint, HandlerCatalogFingerprint, exact query/input/closure bytes, QueryCompatibilityHash and endpoint mapping before QueryRouter cache classification and DomainQueryDispatcher invocation. Every executing peer verifies its local files/DI/options against that capability. Query-only code/options/schema/resolver/endpoint drift changes the query digest and catalog; a query-only dependency also changes RegistryFingerprint through G. It does not change EventTransformHash or projection HandlerCompatibilityHash unless a changed dependency is reachable from those existing roots. Re-admit new query execution and refresh current attestations where required; do not rebuild semantically unchanged projection state just for query code. In-flight mismatch is `QueryCapabilityChanged`, with no response. When there are **no new query rows or query-only G rows**, append no new marker/count/bytes to the historical catalog, registry, event or projection hash preimages; all old fixtures remain byte-identical. The new query digest is absent for legacy routes.

The frozen deployment route table distinguishes admitted versioned queries from registered non-versioned handlers before any cache or DI handler resolution. Absence of a `5b` row alone cannot declassify a formerly versioned route or prove a handler cannot touch versioned data: startup must verify that registration/dependency/read plan against the key-space guard. Unknown or ambiguous classification holds. Existing non-versioned handlers retain their signature, DI and cache/store behavior; guarded versioned keys remain inaccessible to them without the existing scoped authority. VB-19/20 and LB-15 cover the proposed mapping/drift boundary at their stated evidence levels.

#### B7d. Dispatcher-installed query intake and final response fence

Preserve `DomainQueryDispatcher.ExecuteAsync(IServiceProvider, QueryEnvelope, CancellationToken)` and `IDomainQueryHandler.ExecuteAsync(QueryEnvelope, CancellationToken)`. Extend their **internal dispatch path**, not the public query request or a parallel proof carrier. After B7c startup mapping and per-request authentication/plan validation, the dispatcher creates an isolated request DI scope and installs a private, nonserializable immutable session in its platform-owned scoped holder **before constructing/resolving the selected handler or any read-store dependency**. The admitted registration factory resolves exactly that one handler. A handler or helper captured before installation, singleton/captive store, arbitrary service provider, direct DaprClient/physical provider access or unaudited dependency able to bypass the guard makes the route ineligible for versioned query capability; fail readiness instead of invoking it. The existing endpoint mapping forwards the originating request cancellation token through this path and every read/final check. No public DTO or ambient caller data can install/replace the holder.

The existing canonical query/registration options (bound by `5b` tags `03`/`06`) must carry one unambiguous input-value mapping for every admitted `(root binding, exact planned logical key)`: exact stored logical-operation `ValueTypeName`, exact permitted internal CLR assembly-qualified type/assembly bytes for `TValue`, and the input serializer/validator/schema/options descriptor using B7c’s existing descriptor framing. The attested resolver selects that mapping from those options for each planned key; callers and row payloads cannot select a type. Bind mapped CLR/serializer/validator implementation files and their transitive dependencies through the existing sealed G closure; the exact mapping/schema/options bytes remain in the authenticated canonical options/descriptor manifests bound by `5b` tags `03`/`06`. Both participate in QueryCompatibilityHash through its existing inputs. A Get/GetMany `TValue` must equal the mapped registered CLR type; unknown mappings, overlapping mappings, assignability guesses or caller-selected serializers hold with `ReadModelQueryConsistencyHold` before deserialization. For each present row, verify its retained **originating logical-operation witness’s ValueTypeName** through the existing certified root/operation/readback linkage to that exact physical version/digest and compare it to the mapping before validating/deserializing its private bytes. An unchanged leaf retains that originating evidence across later roots; missing evidence holds. Absence/tombstones require existing absence/delete proof and are not deserialized. Charge mapping and type-evidence retention in the existing query/read-set budgets. This adds no catalog field, public API or proof codec.

That scope resolves the existing `IReadModelStore` (and existing IReadModelBulkStore when used) to a read-only guarded adapter and `INamedProjectionVersionedReadModelStore` to the same pinned-session adapter. Reuse NamedProjectionReadScope and the draft's principal-bound exact-key capability, certified root, membership/absence, checkpoint/progress and provider readback codecs. The internal session fixes the authenticated tenant, canonical query route, admitted endpoint/principal/authorization revision/expiry, QueryCompatibilityHash and catalog tuple, immutable request/parameters and resolver plan, operation identity and one certified input root/hash/generation/backend plus retention leases. Its storeName/key/scope arguments must match that plan; descriptive scopes cannot override it. Adapter writes/batches/deletes, undeclared keys, direct physical/generation keys and alternate backend/root/latest-root calls fail before access. Non-versioned helpers contributing input to a versioned result must also be declared/admitted under this session; a second untracked source cannot be read just because its keys are legacy. The ordinary latest-root read path remains for independent non-versioned requests, but is never called by an active versioned query session.

Under the input route/key-space's existing linearizable fence, verify current authorization/capability, exact root pointer/generation, complete root/certificate/final-pointer receipt and progress proof, then install its immutable root and leases before handler invocation. Immediately recheck the same admission under the fence when opening execution. A root already changed returns `ReadModelQueryConsistencyHold` with **zero handler calls**; at most three pre-invocation re-admissions are permitted. This candidate supports **one resolved root per query**: multiple planned roots return that same hold before handler invocation, even if each independently verifies. It adds no common-fence root-vector capability or distributed coordinator. A future already qualified capability may be integrated separately without interpreting this hold as such a capability.

All subsequent Get/GetMany calls prove exact requested keys against the **same pinned root**, reuse the internal bound-root reader used by B7a and never retry an individual read against a newer root. This is the explicit query-session replacement for draft §6's per-call “latest root” selection/retry. Check scope/authorization/lease and cancellation before each read; verify leaf/absence paths, original visibility time/TTL and exact row/delete bytes before deserialization with the admitted schema. Return only defensive working copies. Count at most 256 distinct planned/read keys for the **whole query**, with ≤8 MiB total retained input/read-set proof and ≤64 MiB total admitted row-result bytes, all inside ≤128 MiB live request/proof/rows/working/serialized-response capacity. Repeated reads reuse the same evidence but charge every simultaneously live copy; B7b's ≤64 MiB encoded final response cap also applies. No per-call limit resets or unbounded scan/stream escape are allowed. Proof/result/working overflow is `ReadModelQueryLimit` before dependent allocation. Lost root/row retention or invalid proof yields `ReadModelQueryConsistencyHold`, and expiry yields `ReadModelExpiryPending`, with no newer-row substitution.

Handler results, failures, derived DTOs and streaming fragments stay private; handler return is not HTTP completion. Validate/serialize once under the registered response schema/codec and B7b capacity reservation. Before exposing any bytes, hold the same root fence and revalidate pointer/generation, current endpoint/DI/code/options/dependency/catalog tuple, current principal/exact-key grants and their expiry, retained root/row proofs, and **authoritative coordinator UTC for every TTL-bearing row in the complete read set**, including unchanged generations. B7a's read-set evidence/lifetime rules apply; a revoked grant denies access, changed capability yields `QueryCapabilityChanged`, changed root yields `ReadModelQueryConsistencyHold`, and expired/unverifiable TTL yields `ReadModelExpiryPending`/consistency hold. None returns candidate bytes or a cached fallback. This validation is the linearization point of the completed read: a subsequent ordinary root advance does not invalidate already completed input; no multi-root snapshot or guarantee through future network delivery is asserted. Only after that point can the dispatcher return the sealed private response for transport, using B7b's ownership/release rules. A final-check failure may be retried only as a separately admitted **whole** query; no individual read switches generation and no partial result escapes. Query handlers have no durable effects in this capability.

Cancellation/failure closes the scoped session, stops future reads and discards uncommitted result bytes. Retained session/store references reject after closure with `ReadModelRouteContextRequired`; transport readers own their copied response only until completion/cancellation. Keep read-set proof/root leases through the final check or definitive discard and until their last user completes, then clear private capacities and release proven-unneeded reservations. No query-success checkpoint, source-sequence advance, new signature or retained result cache is created. VB-20 requires real DI/transport/fence evidence; LB-16 is a small bounded root/TTL/auth/cancellation model, not implementation proof.

### B8. Read semantics, attestation refresh and rollback

**BH37-10 accepted.** Replace the contradictory historical sentence with: **A write-only F change does not, by itself, invalidate folded state or force semantic replay/rebuild. It changes RegistryFingerprint and therefore invalidates transient old-fingerprint route/prefix/continuation proof. Refresh that evidence through authenticated source admission; restart a paged operation from page 1 when its transient proof is stale.** Operation restart may perform replay work, but it is not proof that unchanged state semantics became incompatible.

The exact draft §5 distinction is retained: EventTransformHash includes applicable D/V/A/E, StateSchemaApplyHash, S-read/protection and selected read dependency closure; it excludes write-only F, signer/trust/publisher and handler-only fields. An F dependency that is also reachable from a read root is **not write-only**, and changing it changes the read hash. HandlerCompatibilityHash includes selected handler/filter/registration/effect/readback/backend/key-space/mode capability and dependencies; HandlerCatalogFingerprint inventories all routes. StoredDigest/raw/protection evidence never changes merely because any current fingerprint changes.

| Change | Required state and proof action |
| --- | --- |
| F implementation/options or F-only dependencies; identical applicable A/V/E and read/Apply state hashes | Preserve semantically compatible witnessed snapshot/checkpoint after durable source/state verification. Acquire new current route/prefix proof; stale continuation restarts page 1. Do not relabel an old final proof as current or rewrite pinned bytes. |
| Read alias/schema/serializer/upcaster/protection/read dependency, StateSchemaApplyHash, or affected handler/backend/key-space/fold-mode change | Old state cannot become Current by re-signing. Full authenticated replay/rebuild or separately approved migration, with new state/readback and proof; keep old immutable authority for its remaining obligations. |
| Signer/trust rotation only | Verifier-first capability/parity transition and fresh transient proof; no semantic state invalidation by itself. Historic checkpoint/delivery/encoding/provider keys verify only exact retained obligations and original intervals; revocation is not overridden. |
| V1-only or old effective-view/codec/mode endpoint after V2/hopped history or v2 page transcript exists | Fence routing before Apply. `RollbackReaderCapabilityHold` if no capable endpoint; retain work and old/new evidence. Downgrade cannot reinterpret new metadata or omit timeline binding. |

A stale-proof refresh can verify unchanged state through its durable witness/compatible hash and issue fresh transient source evidence without rerunning domain Apply; when that evidence cannot be established, perform authenticated replay or hold. There is no promise of zero source-read cost on registry rotation. Keep A5 historic provider receipts/complete images and A6 original corruption observations separate from fresh head/ETag. Neither historical evidence nor a current signer substitutes for the other. 6.5c must include all these capability, key retention and rollback requirements in activation; this text grants no activation authority.

### B9. Failure, cancellation and inspection outcomes

Use draft §9 plus the explicit B4–B6 bindings. Error results carry safe reason, addressed sequence, bound/count and operation/correlation identifiers only under existing access policy. Do not expose raw/effective/protected payload, identity secrets, CLR assembly identity, provider internals or stacks in ordinary Admin/trace/logs. Authorized backup content remains its distinct existing permission, never inferred from diagnostics permission.

| Trigger | Typed outcome and durable authority |
| --- | --- |
| Authenticated immutable malformed/schema/identity failure | Stop at exact event; `RawEnvelopeCorrupt`, `MalformedMetadata`, `EventSchemaRejected` or identity failure. No following hop, Apply, checkpoint or success. Retain source evidence; AD-31 capture-before-ack remains 6.5c's separate requirement. |
| Missing/untrusted source, capability, proof/key/provider; unexpected upcaster exception | `RawSourceUnavailable`, `ActorCommitEvidenceHold`, `HandlerCapabilityMismatch` or deployment/provider hold; no poison skip or synthetic success. Source mutation/owner escape is `UpcasterContractViolation`, a route-code hold. |
| Preallocation failure | `RawEnvelopeLimit`, `ReadableLimit`, `ProofLimit`, `ScratchLimit`, `LegacyArrayLimit`, `TimelineLimit`, `CheckpointStateLimit`, `FinalResponseLimit`, `ContinuationCapacityHold`, `NamedIndexLimit` or `ReadModelQueryLimit` at the corresponding bound. No unearned transition; newly insufficient capability is a readiness hold. |
| Apply mutates then throws | `ApplyFailed` at that sequence; retain pre-Apply last-good bytes, old committed pointer/checkpoint and no timeline entry for the failed event. Legacy partial diagnostics remain non-authoritative. |
| Cancellation before computation/prepare | Propagate original token, stop bounded read/upcast/Apply loop, discard private uncommitted work and release proven-unneeded quota after safe cleanup. Existing actor events and last-good state remain. |
| Cancellation, timeout or lost acknowledgement after successor/commit begins | `ReplayCommitAmbiguous` and same-operation readback under recovery token; no second Apply or claim of rolled-back state. A proven commit returns pinned committed truth even after caller cancellation; unknown outcome retains quota. |
| Invalid replay scalar/selector/manifest | `ReplayScalarInvalid`, `ReplayTranscriptCapabilityHold`, `TimelineEvidenceHold` or `TimelineProtectionHold` before dependent allocation/Apply or authority release. Retain uncertain prepared objects and quota; unsupported rollback holds. |
| Invalid projection prior descriptor, root/session race | `ProjectionPriorStateHold`, `ProjectionPriorConflict` or `ProjectionPriorSessionClosed` with no mixed-state handler input or checkpoint publication; B7a defines pre-handler retry versus post-handler reconciliation. |
| Diagnostic output/base failure | B2a returns existing safe Problem or sandbox error shape; no fabricated source proof, committed state or partial success. Cancellation propagates. |
| Query code/root-vector failure or retained-cache saturation | `QueryCapabilityChanged`/`ReadModelQueryConsistencyHold` without result for invalid execution authority; capacity of the bypassed cache alone executes uncached and is not a failure. |
| Snapshot rejection or replay restart | Fresh authenticated page 1; no tail Apply under rejected proof, no deletion of old protected snapshot, no reuse of partial timeline across operations. |
| Query/root race or failed checkpoint proof | `ProjectionStateUnproven`, `CheckpointFenceUnavailable`, `ReadModelExpiryPending` or access-denied outcome; return no mixed generation, expired bytes or partial bulk values. |
| Backup page tamper, cancellation, final fixed-head/proof failure or total output above 64 MiB | `ExportLimit` or typed source/protection failure. Stage authenticated encrypted output privately, validate every bounded page and final fixed-head completion, then commit/read back one completion pointer before any HTTP content chunk/download URL. No partial export escapes. Preserve stored provenance; existing protected/provider-opaque export rejection remains. |

Export staging follows **the same shared 1 GiB pool as replay** in B6, in its stable deployment/backend scope. Before the first chunk allocation, atomically reserve the conservative complete encoded output, ciphertext/protection overhead, staging/chunk metadata, completion pointer, recovery evidence and any overlapping copies; the 64 MiB encoded export cap is not an extra storage allowance. Concurrent exports, replay and abandoned/ambiguous export generations share those counters and smaller tenant/store limits; insufficient capacity returns `ContinuationCapacityHold` before allocation, never partial output. Use the existing admitted export operation and exact object-plan reservation/readback under B6’s fence, without another endpoint or codec. Lost completion-pointer acknowledgement reconciles the original exact chunks/hash/pointer before any download; cancellation cannot report an uncertain pointer absent or regenerate an export under the same operation. Pre-completion cancellation leaves private objects invisible and charged until no-future-write evidence and authenticated deletion/key-revocation readback permit cleanup. Completed exports retain exact chunks, metadata, pointer and necessary keys through every active download, retry and backup obligation; recheck existing download authorization and retain charges while readers remain. After all obligations close, fence new readers, reconcile the pointer and in-flight writes, then delete/revoke with authenticated readback before refunding once. Ambiguous deletion or abandoned work retains its reservation for recovery; namespace/revision/operation replacement cannot reset accounting. No partial output becomes visible and completed truth survives caller cancellation.

### B10. Dispositions and verification vectors

The four original rows remain open in the immutable [historical triage](story-6-5-review-triage.md#review-triage-log). “Accepted” here means the defect is accepted and a testable replacement is proposed; it is not a claim of runtime closure or human AD-13 approval.

| Finding | Disposition and rejected alternative | Section and vectors |
| --- | --- | --- |
| BH37-3 | Accepted: bind cumulative private timeline and state to one committed page transition. Reject last-page-only timeline, public incomplete timeline success and regenerating consumed pages. | [B4](#b4-authenticated-private-timeline-and-atomic-page-transition); [VB-01](#vb-01), [VB-02](#vb-02), [VB-07](#vb-07), [local LB-02/03/07](#executable-local-model-checks). |
| BH37-4 | Accepted: reserve composed peaks including prior/successor/caller/serialization capacity. Reject individual-maxima admission or an unproved streaming claim. | [B6](#b6-compositional-admission-retention-and-capacity-holds); [VB-03](#vb-03), [VB-04](#vb-04), [local LB-01](#executable-local-model-checks). |
| BH37-5 | Accepted: capture private bytes synchronously before any async suspension and validate/save that same owner. Reject storing a caller-backed ReadOnlyMemory after validation. | [B5](#b5-successor-ownership-and-last-good-state); [VB-05](#vb-05), [VB-06](#vb-06), [local LB-03/04](#executable-local-model-checks). |
| BH37-10 | Accepted: separate current attestation freshness from state semantics; write-only F does not force semantic rebuild. Reject both “F never changes any proof” and “F alone invalidates read state.” | [B8](#b8-read-semantics-attestation-refresh-and-rollback); [VB-09](#vb-09), [VB-10](#vb-10), [local LB-05](#executable-local-model-checks). |

All VB vectors below are **future production/provider tests, not executed here**. They must inspect exact persisted bytes, pointers, leases, receipts, read counts and absence of forbidden mutation, not just HTTP codes or mocks. Run through each B1 consumer where applicable.

| Vector | Setup/action and required evidence |
| --- | --- |
| <a id="vb-01"></a>VB-01 mixed/paged equivalence | One fixed prefix containing retained V1 alias, zero-hop current large V1, V2 and a registered hop; compare command, replay, reconstruction, projection live/retry/history/shared rebuild and inspection effective digests and final state. Page it at several boundaries with timeline=true; compare every sequence/state to full canonical replay and terminal Tn/v2 transcript. Each event adapts once per read, no incomplete public state/timeline, no final-page-only timeline. |
| <a id="vb-02"></a>VB-02 timeline tamper/retry | After two committed pages, alter/reorder/remove an earlier entry, chunk, mode, manifest count, P or root; copy a valid chunk from another tenant/operation; lose manifest/response/pointer acknowledgement separately. Require no next token/complete result on mismatch, retained quotas and exact response on proven retry, unchanged Apply count. Losing live scratch restarts only with a new page-1 operation; losing a referenced chunk never regenerates it under the spent token. |
| <a id="vb-03"></a>VB-03 simultaneous memory | Preflight 64 MiB event +64 MiB prior +proof+successor; instrument allocator/transport/serializer/provider capacities to show `ScratchLimit` before Apply, allocation beyond reservation or state write. Exercise the 126/130 MiB example with actual bucket capacities and checked overflow. A separately supported streaming route must prove its phase peaks and unchanged meaning on the same source before it may pass. |
| <a id="vb-04"></a>VB-04 independent bounds | Test at/below/above each event/page/proof/state/timeline/encoded-response/legacy-array bound, 1,000/1,001 timeline entries, max metadata, long JSON escaping and Base64. Final JSON >128 MiB returns `FinalResponseLimit`; fresh state-only replay may succeed, same-operation mode change conflicts. Unknown pool rounding and unaudited unbounded serializer hold readiness. |
| <a id="vb-05"></a>VB-05 caller mutation across await | Gate provider save after synchronous capture; mutate caller array/MemoryManager-backed input, prior copy and handle before releasing save; persisted successor hash/bytes must equal the privately validated capture, not mutated input. Validate wrong schema, second successor, stale owner and reused handle before save. A provider callback must never receive the original owner. Test late writer reference and full-capacity clearing. |
| <a id="vb-06"></a>VB-06 mutate-then-throw | Extend the existing `FailingState` scenario to assert `CallCount=0` in retained last-good bytes after event 1 throws, not merely non-null StateJson. For a later throw, assert exact preceding state/timeline, prior committed page pointer/checkpoint and no failed-event entry. Typed working state corruption must never be pinned. |
| <a id="vb-07"></a>VB-07 cancellation/ambiguous commit | Cancel before raw read, hop, Apply, synchronous capture, during provider save, between each prepare object and final CAS, and after proven commit. Inject crashes and lost acknowledgements at all those durable points. Reconcile full object set by one operation; no repeated Apply, premature quota release, partial final state or synthetic rollback. Exact final retry after 15 minutes but within retained ≥24h pin works without scratch/live receiver/live lease when authenticated original selector/instance/owner-generation admission evidence, current caller/key/fingerprint/capability checks and complete final readback all verify. Missing original lease evidence, changed instance binding or incomplete final objects holds; expired live lease on incomplete work cannot continue. Exercise two committed pages with same receiver, renewed scratch/monotonic lease CAS generations and exact linked consumed/next tokens: completed recovery succeeds without a live owner; changed receiver, broken token link or P/lease generation mismatch holds. No new selector/proof/Apply is minted from that exception. |
| <a id="vb-08"></a>VB-08 source/snapshot boundary | Mutate raw null presence, alias casing, stored/effective format, offset at same UTC instant, scope/key, route signature, old codec receipt or bundle-readback hash. Test snapshot storage/readable mismatch, stale Apply hash, unreadable provider and zero-tail at `long.MaxValue`; bad snapshot requests fresh page 1 before tail Apply. A valid snapshot at target 7 with actual head 9 also falls back to full replay 1..7/head=9; no new zero-tail form or target/head rewrite is admitted. Valid snapshot+tail state equals full replay, whereas timeline=true always replays 1..target. Retained corruption at old head remains the same decision after valid append, but changed bytes/incarnation conflict. |
| <a id="vb-09"></a>VB-09 F/read compatibility | Change only F bytes/options and prove RegistryFingerprint changes while read/Apply hashes stay equal; reuse witnessed state with fresh proof and no semantic invalidation, but reject stale continuation/final proof. Then change read E/V/serializer/shared dependency or handler capability and require replay/rebuild before Current. Historic provider images remain independently verifiable after today's head/ETag advances. |
| <a id="vb-10"></a>VB-10 rollback/capability | Route retained V2/hopped history, v2 transcript or incremental prior-state request to old-only endpoint; assert fencing/`RollbackReaderCapabilityHold` before Apply. Verified async full-replay handler cannot receive tail as complete input. Test unrelated handler-catalog changes separately from selected HandlerCompatibilityHash. |
| <a id="vb-11"></a>VB-11 current/lagging projection | Checkpoint=head with valid named root/receipt causes zero event reads, handler calls, writes and advancement. Checkpoint ahead, missing root or wrong anchor fails. Lagging incremental route reads only bounded contiguous tail; full-replay route receives complete prefix or holds. Missing/hopped secondary event capability stops before dispatch/fingerprint; retry/history/shared rebuild behave identically. |
| <a id="vb-12"></a>VB-12 named-root/query races | Two hosts/shared backend: prepare rows/checkpoint, lose CAS acknowledgement, advance coordinator generation, race TTL and query root recheck. Queries return only one certified generation; unscoped legacy/physical-key access fails, non-versioned marker-gated behavior stays compatible. Keep ordinary ETag equal while changing authenticated route, root generation, selected compatibility or principal authorization: QueryRouter/CachingProjectionActor must bypass versioned cache and reauthenticate execution or hold, never serve old cached bytes/Current. Checkpoint cannot advance from free state/delete absence, stale fence or wrong backend. Ambiguous root/deletion retains old generations/quota. |
| <a id="vb-13"></a>VB-13 export/inspection | Tamper a later authenticated page, exceed 64 MiB encoded export, cancel before final pointer, or supply unreadable protected data: no downloadable bytes/URL/partial success. Safe Admin/trace errors contain sequence/reason only; state diff/blame/bisect require completed canonical timeline. Inspect raw source bytes unchanged after both success and failure. Race replay reservations with two exports and abandon/cancel one before pointer acknowledgement: the shared 1 GiB cap still charges its staged/pointer/metadata bytes, blocks unfit new work and refunds only after complete reconciliation and proved deletion; active completed downloads/retries retain their charges. |
| <a id="vb-14"></a>VB-14 capacity/retention | Exhaust ledger, shared 1 GiB replay quota, timeline and named-index/archive limits; next work holds before Apply/root publication. Race two replicas/tenants/operations reserving 600+600 MiB in the same stable deployment/backend pool: only one may reserve, and a smaller tenant/store cap still applies. Repeat with old retained and new prepared generations, lost reservation acknowledgements, restart and deployment revision changes: exact retry reuses a reservation, recovery proves counters before admission, no rename/new operation resets the pool. Race rollback/backup reader with expiry/delete and require retained exact request/response/chunk/key evidence until obligations close and absence/revocation readback proves release. Corrupt/inconclusive cleanup cannot refund quota. Retain 60 GiB of old named generations plus 4 GiB of current/new root/row/node/tombstone/archive bytes: exactly 64 GiB fits only if all component caps also fit; one additional byte holds before allocation/publication. Prove shared immutable references count once until their last obligation ends, while distinct copies and namespace migration overlap remain charged. |

Additional review-loop production vectors extend, rather than replace, VB-01–VB-14:

| Vector | Setup/action and deterministic required outcome |
| --- | --- |
| <a id="vb-15"></a>VB-15 sandbox | Run historical base and explicit empty genesis, success/no-op/rejection; assert unchanged SandboxResult shape, current-schema hypothetical Apply and exact diagnostic state/diff. Tamper base proof: no invocation. Invalid output/schema/identity/limit: sandbox error, no simulated Apply. Mutate-then-throw: safe replay Problem without partial success. Cancel at every phase: cancellation and zero append/checkpoint/outbox/publication. Assert simulation carriers fail all production proof/admission paths. |
| <a id="vb-16"></a>VB-16 selector and zero completion | Independently encode empty, snapshot-zero-tail and MAX zero-event P; compare literal hashes/one-step terminal v2 transcript, final state, counts and null/empty timeline. Replay target=0 at nonempty head must use the new empty-target form without claiming an empty stream. Genesis-only v2 completion fails. Count-one MAX Applies once with no extra transition/increment. Strip/swap version, endpoint/instance, zero/wrong revision, unsupported transcript/manifest codec, scope, nonce, fingerprint or selector signature/pinned request: hold before Apply/command invocation. Reject v1 fallback and old verifiers; rotate capability mid-operation: new page 1. Probe all signed I/N negative/high-bit/max+1 fields, including decoded nested S and manifest scalars, before allocation. |
| <a id="vb-17"></a>VB-17 manifest recovery | Execute LB-11 framing against a qualified encryption provider with chunks crossing E boundaries. Alter tag/nonce/algorithm/configuration/key/scope/P/request, split/offset, PH/CH, counts, byte totals and final hashes; require TimelineEvidenceHold/TimelineProtectionHold, no token/result/Apply rerun and retained quota. Crash after each object/manifest/pointer save; reconcile exact prepared ciphertext without re-encrypting. Zero list is a four-byte plaintext chunk, not absent evidence. Measure all caps/preallocation and key-retention/deletion outcomes. |
| <a id="vb-18"></a>VB-18 prior-state intake | Every invalid discriminator/presence combination, free DTO, wrong source checkpoint/root/readback/serializer or genesis on nonempty model fails before handler. Admit CanonicalState for an aggregate `52` route with no `59` using absent KeySpaceId/OwnershipMode and exact immutable state-pointer evidence; reject fabricated/empty key-space strings or named-root/state cross-discriminators. A route-wide incremental handler with two `59` declarations holds before invocation; a one-space named incremental route, zero-space single-state route and full-replay route retain their respective behavior. Admit genuine empty single-state and named genesis separately; named absence is not generic genesis. Advance shared root from R1 to R2 before intake: re-admit bounded retry or hold, zero old-tail handler calls. Advance after intake: every read stays on R1; candidate commit conflicts without effect/checkpoint. Lose R1 lease or expire row: hold, never read R2. Read a TTL leaf before expiry then advance authoritative UTC past expiry during the handler **without changing root/generation**: final publication must fail with zero effects/checkpoint. Missing visibility time/proof or exhausted cumulative read-set budget also holds; measure read-set reservations/retention and no early cleanup. Verify completion receipt/source-prefix/BatchId/root/pointer linkage without any replay P. Race ambiguous prior candidate/effect completion: reconcile old operation before a new dispatch. |
| <a id="vb-19"></a>VB-19 derived query and capacity | Query computes a total/DTO absent as any persisted leaf. Keep root and ETag equal while changing only query implementation/options/dependencies; execute new authenticated code, return its newly computed result, never cached bytes. Change code/root/authorization mid-query: fail/re-admit per B7b. Saturate legacy cache and deny every versioned cache admission: valid versioned query succeeds uncached. Inspect zero retained versioned entries/payload/proofs after transport completion/cancellation; count retained borrowed/quarantined capacity until safe release, and test input-proof/response/working caps independently. |
| <a id="vb-20"></a>VB-20 authenticated query intake | Run through `/query`, DomainQueryDispatcher, actual DI handler/store helpers and QueryRouter/CachingProjectionActor. Independently encode `5b`, descriptor/root bindings and query hash; test 1/64-byte valid names, 65-byte names and empty/unknown implementation/registration/codec/resolver/endpoint IDs. Request a different `TValue` or swap originating logical-operation ValueTypeName/mapping/schema evidence for the same key: hold before deserialization, including an unchanged leaf inherited by a later root; mutate every route/endpoint/code/options/schema/registration/resolver/dependency/input-row field, unknown/duplicate/order/bounds and local-vs-deployment parity: startup/admission holds deterministically. No-row historical fixtures remain exact. After R1 intake, advance to R2 between two source reads: both use R1; final fence rejects the private result, with no individual latest-root retry. Test unchanged-generation TTL expiry, revoked exact-key grants, root/proof lease loss, code-only drift at final check, unauthorized/undeclared/physical-key/helper reads and scope override. Multi-root plan holds with zero handler calls. Test singletons/direct-provider/captured stores at readiness, isolated scopes across concurrent tenants, original-token propagation, pre/post-handler cancellation and reads after disposal. No response fragments escape before complete schema/size/final-fence checks; inspect cumulative 256-key/8 MiB proof/64 MiB row and response/128 MiB live budgets and final clearing. Preserve non-versioned handlers and zero versioned retained cache. |

Review-loop disposition mapping: BH1-1 → B1/B2a, VB-15/LB-09; BH1-2/3 → B4a/B4b, VB-16/LB-10/14; BH1-4 → B4c, VB-17/LB-11; BH1-5 → B7a, VB-18/LB-12; BH1-6/7 → B7b, VB-19/LB-13; BH1-8/EC1-1 → B4 scalar rule, VB-16/LB-08; P1-1 → B2 explicit V2 replacement, VB-08 source tamper/evidence vector. Each is a proposed deterministic closure, not a provider-test pass.

Loop-2 mapping: BH2-1 → [B7c](#b7c-authenticated-query-catalog-and-dispatch-mapping), VB-19/20, LB-15; BH2-2 → [B7d](#b7d-dispatcher-installed-query-intake-and-final-response-fence), VB-20, LB-16; BH2-3 → B7a/B7d final TTL/read-set fence, VB-18/20, LB-16; BH2-4 → B7a discriminated single-state/`52` intake, VB-18; BH2-5 → B7 exact projection completion linkage, VB-11/18; BH2-6 → B4b retained-final lease distinction, VB-07/16, LB-17; BH2-7 → B6 shared pool, VB-14, LB-18; BH2-8 → B1 explicit wrappers/dispatcher/mappings, VB-01/10/11/19/20; BH2-9 → B4b, VB-16, strengthened LB-14. BH2-10 stays rejected as an unclaimed full-wire/provider fixture extension; SelectedRequestHash remains exact and its synthetic local request hashes remain explicitly limited.

Present coverage limits: `Replay_IncludeTimeline_EmitsPerEventStateSnapshots` checks 18 in-memory entries and `Replay_IncludeTimelineFalse_DoesNotAllocateSnapshots` checks null, not durable multipage continuity. `Replay_ApplyMethodThrows_ReturnsPartialAtLastGoodSequence` uses a state that increments then throws but does **not** assert the serialized value is still zero. `RehydrateAsync_WithSnapshot_ReadsOnlyTailEvents` and `RehydrateAsync_WithSnapshot_NoTailEvents_ReturnsSnapshotState` prove mock key selection under a supplied snapshot, not authenticated snapshot authority. `SnapshotPlusTail_TailEventsMatch_FullReplayEvents` compares event metadata/tail entries, not folded state hashes. These are starting points for VB-01/06/08, not substitutes for the new assertions. No existing tests were edited or run for this documentation change.

#### Executable local model checks

Run the following self-contained Python block by extracting this section's fenced block into a temporary file or piping it to `python3`. It uses small values for ownership/timeline and integer byte counts for large-budget checks; it never allocates 64 MiB buffers. It checks proposed codec bytes, deterministic arithmetic and in-memory ownership/commit, adaptation and capability **models** only. It does not verify ECDSA, a CLR MemoryManager, provider CAS/fencing, transport bounds, allocator behavior, actual Apply equivalence, production rollback or query/cache races. LB-06's issued-view set is a model stand-in for authenticated authority; its consumer labels exercise one toy folding function, not the repository's independent production consumers. LB-07's literal full P/genesis/step bytes are codec known answers, not signed/provider evidence. LB-08 adds signed scalar boundaries; LB-09/12/13 are deliberately small simulation/root/cache models, not controller/session/allocator implementations. LB-10/11/14 add fixed zero-event hashes, complete manifest bytes and selector hash; their request/proof hashes and ciphertext/tag are explicit synthetic inputs, with no ECDSA or encryption claim. The manifest decoder models the displayed one-chunk empty-list fixture only; multi-chunk/provider protection remains VB-17. LB-15 checks new query-row framing/catalog, name/required-ID bounds, input-type tuple selection and drift with synthetic input-catalog/G rows, not complete DI/dependency/identifier resolution or schema-witness authentication. LB-16 checks finite pinned reads, TTL/auth/code final decisions and aggregate key/proof limits; its clock/proof booleans are external-evidence stand-ins. LB-17 checks renewed lease/token/page-generation decisions, completed/incomplete recovery and the snapshot-at-target fallback; tuples stand in for authenticated lease evidence. LB-18 models sequential decisions under a hypothetical shared quota fence, not real concurrency, atomic counters, storage allocation, sharing proofs or safe deletion; its added named-generation and replay/export examples use integer capacities only. VB-14/18/20 must establish those provider/runtime properties. LB-14 is a scalar/capability predicate and never a selector signature verifier.

| Execution-spec matrix row | Executed local model and future production vector |
| --- | --- |
| Mixed history | LB-06: stored versions 1/2 and the sole registered 1→2 edge; identical toy-consumer outcomes, verified views consumed without another hop, typed schema/Apply failure preserving last-good bytes. [VB-01](#vb-01), [VB-06](#vb-06) prove the real consumer paths later. |
| Paged timeline | LB-02/03/07/08/10/11/14: page-continuous Tn, immutable retry model and literal complete 19-field P/transcript-v2 encodings plus signed ranges, zero completion, selector and manifest known answers/malformed cases. [VB-16](#vb-16)/[VB-17](#vb-17) extend the provider coverage. [VB-02](#vb-02), [VB-07](#vb-07) remain provider work. |
| Memory boundary | LB-01/18: shared competing/retained reservations plus 64+64 MiB plus proof fails, actual-capacity arithmetic/overflow and encoded limits. [VB-03](#vb-03), [VB-04](#vb-04), [VB-14](#vb-14) require measured production peaks and atomic shared accounting. |
| Async successor | LB-03/04/17: exact completed recovery versus live incomplete ownership plus mutation after synchronous capture, cancellation, lost-ack exact retry and last-good preservation. [VB-05](#vb-05), [VB-06](#vb-06), [VB-07](#vb-07) require CLR/provider proof. |
| Compatibility | LB-05/14: write-only F/read-hash separation, stale-proof hold, fresh compatible proof admission, unsupported-reader rollback hold and semantic replay on read drift. [VB-09](#vb-09), [VB-10](#vb-10), [VB-16](#vb-16) exercise production gates later. |
| Diagnostic sandbox | LB-09: verified-base/explicit-empty distinction, schema failure before Apply, private failure and cancellation; [VB-15](#vb-15) covers production isolation and preserved response semantics. |
| Prior root and derived queries | LB-12/13/15/16: immutable root read/conflict, exact query-row/dispatch drift, cache bypass and final TTL/authorization/read-set bounds; [VB-18](#vb-18)/[VB-19](#vb-19)/[VB-20](#vb-20) establish real DI, fence, authorization and retained-capacity behavior later. |

```python
import asyncio
import hashlib
import json
import struct
from dataclasses import dataclass, replace

MiB = 1024 * 1024
MAX = (1 << 63) - 1
H = lambda b: hashlib.sha256(b).digest()
def checked_scalar(n, bits):
    if type(n) is not int or not 0 <= n < (1 << (bits - 1)):
        raise ValueError('ReplayScalarInvalid')
    return n

def I(n):
    return struct.pack('>i', checked_scalar(n, 32))

def N(n):
    return struct.pack('>q', checked_scalar(n, 64))

def U32(n):
    if type(n) is not int or not 0 <= n < (1 << 32):
        raise ValueError('u32 framing')
    return struct.pack('>I', n)

def read_scalar(raw, bits):
    if len(raw) != bits // 8:
        raise ValueError('scalar width')
    return checked_scalar(int.from_bytes(raw, 'big', signed=True), bits)

B = lambda b: U32(len(b)) + b
U = lambda s: B(s.encode('utf-8'))

def total(parts):
    result = 0
    for n in parts:
        if n < 0 or n > MAX - result:
            raise ValueError('checked overflow')
        result += n
    return result

def rejects(action):
    try:
        action()
    except ValueError:
        return
    raise AssertionError('expected rejection')

# LB-01: actual simultaneous capacities, encoding and independent ceilings.
assert total([64 * MiB, 64 * MiB, 1]) > 128 * MiB
assert total([64, 32, 16, 8, 2, 4]) * MiB == 126 * MiB
assert total([64, 32, 16, 8, 2, 4, 4]) * MiB > 128 * MiB
assert 100000 * 8192 > 256 * MiB
assert 4 * ((64 * MiB + 2) // 3) * 2 > 128 * MiB
assert (1 * MiB + 1 + MiB - 1) // MiB * MiB == 2 * MiB
rejects(lambda: total([MAX, 1]))

scope = (U('t') + U('d') + U('a') + U('id') + U('op') + N(3) + N(3)
         + H(b'registry') + H(b'transform') + H(b'apply') + b'\x01')
SEP = b'HX-EV-REPLAY-TIMELINE-ENTRY-1\0'

def entry(seq, state):
    fields = [N(seq), H(b'stored' + N(seq)), U('Legacy.Event'), U('evt'),
              I(1), U('json'), U('state-json'), B(state)]
    return SEP + b'\x01\x00\x08' + b''.join(bytes([i]) + f
                                              for i, f in enumerate(fields, 1))

def decode(data):
    prefix = SEP + b'\x01\x00\x08'
    if not data.startswith(prefix):
        raise ValueError('header')
    pos = len(prefix)
    result = []
    for tag, fixed in enumerate([8, 32, None, None, 4, None, None, None], 1):
        if pos >= len(data) or data[pos] != tag:
            raise ValueError('tag')
        pos += 1
        if fixed is None:
            if pos + 4 > len(data):
                raise ValueError('length')
            size = int.from_bytes(data[pos:pos + 4], 'big')
            pos += 4
            if size > 64 * MiB:
                raise ValueError('bounded length')
        else:
            size = fixed
        if pos + size > len(data):
            raise ValueError('truncated')
        value = data[pos:pos + size]
        if tag == 1:
            read_scalar(value, 64)
        if tag == 5:
            read_scalar(value, 32)
        if tag in (3, 4, 6, 7):
            value.decode('utf-8', errors='strict')
        result.append(value)
        pos += size
    if pos != len(data):
        raise ValueError('trailing')
    return result

def timeline(entries, prior=None, expected=1):
    root = prior if prior is not None else H(b'HX-EV-REPLAY-TIMELINE-1\0\x01' + B(scope))
    for raw in entries:
        fields = decode(raw)
        if int.from_bytes(fields[0], 'big') != expected:
            raise ValueError('sequence')
        root = H(b'HX-EV-REPLAY-TIMELINE-STEP-1\0\x01' + root + H(raw))
        expected += 1
    return root

# LB-02: canonical framing, page continuity, mutation and exact scope.
entries = [entry(n, ('{"n":%d}' % n).encode()) for n in (1, 2, 3)]
assert entries[0].startswith(SEP + b'\x01\x00\x08\x01' + N(1) + b'\x02')
assert decode(entries[0])[-1] == b'{"n":1}'
root = timeline(entries)
assert root.hex() == '2376c63b2d88ea99bb8f91a43bc1879ce92929c9d3bb41b7b919ec7a9dbebfce'
assert timeline(entries[2:], timeline(entries[:2]), expected=3) == root
assert timeline([entries[0], entries[1], entry(3, b'{"n":9}')]) != root
rejects(lambda: timeline([entries[1], entries[0]]))
rejects(lambda: decode(entries[0] + b'\x00'))
rejects(lambda: decode(entries[0][:-1]))
bad = bytearray(entries[0])
bad[len(SEP) + 3] = 2
rejects(lambda: decode(bytes(bad)))
assert H(b'HX-EV-REPLAY-TIMELINE-1\0\x01' + B(scope[:-1] + b'\x00')) != timeline([])

class Session:
    def __init__(self):
        self.prior = b'{"n":0}'
        self.pointer = None
        self.prepared = None
        self.apply_calls = 0

    def capture(self, caller):
        # Ordinary function: the private copy exists before coroutine scheduling.
        sealed = bytes(caller)
        if sealed != b'{"n":3}':
            raise ValueError('schema/model result')
        digest = H(sealed)
        async def save(gate):
            await gate.wait()
            self.prepared = (sealed, digest, root)
            return self.prepared
        return save

    def publish(self, value, lost_ack=False):
        if self.pointer is not None:
            if self.pointer != value:
                raise ValueError('same-operation conflict')
            return self.pointer
        self.pointer = value
        if lost_ack:
            raise TimeoutError('ack lost after commit')
        return value

async def ownership():
    # LB-03: after-capture mutation and ambiguous commit never re-Apply.
    session = Session()
    caller = bytearray(b'{"n":3}')
    session.apply_calls = 3
    save = session.capture(caller)
    caller[:] = b'{"n":9}'
    gate = asyncio.Event()
    task = asyncio.create_task(save(gate))
    await asyncio.sleep(0)
    assert session.prepared is None and session.pointer is None
    gate.set()
    captured = await task
    assert captured[0] == b'{"n":3}' and captured[1] == H(captured[0])
    assert session.prior == b'{"n":0}'
    try:
        session.publish(captured, lost_ack=True)
    except TimeoutError:
        pass
    assert session.publish(captured) == captured and session.apply_calls == 3
    rejects(lambda: session.publish((b'{"n":9}', H(b'{"n":9}'), root)))
    # Precommit cancellation preserves prior authority in this model.
    fresh = Session()
    pending = asyncio.create_task(fresh.capture(bytearray(b'{"n":3}'))(asyncio.Event()))
    pending.cancel()
    try:
        await pending
    except asyncio.CancelledError:
        pass
    assert fresh.pointer is None and fresh.prior == b'{"n":0}'

asyncio.run(ownership())

# LB-04: mutation followed by throw cannot corrupt retained last-good bytes.
prior = b'{"n":0}'
working = {'n': 0}
try:
    working['n'] = 99
    raise RuntimeError('Apply failed')
except RuntimeError:
    working = None
assert prior == b'{"n":0}'

# LB-05: model fingerprints and routing gates, not the draft manifest codec.
def fingerprints(read, write, signer):
    return H(read + write + signer), H(read)
old = fingerprints(b'read', b'F1', b'key1')
new = fingerprints(b'read', b'F2', b'key1')
rotated = fingerprints(b'read', b'F1', b'key2')
drift = fingerprints(b'read2', b'F1', b'key1')
assert old[0] != new[0] and old[1] == new[1]
assert old[0] != rotated[0] and old[1] == rotated[1]
assert old[1] != drift[1]

def compatibility(proof_registry, active_registry, state_read_hash, active_read_hash,
                  required_version, reader_max_version):
    if reader_max_version < required_version:
        return 'RollbackReaderCapabilityHold'
    if proof_registry != active_registry:
        return 'StaleProofHold'
    if state_read_hash != active_read_hash:
        return 'SemanticReplayRequired'
    return 'Admitted'

assert compatibility(old[0], new[0], old[1], new[1], 2, 2) == 'StaleProofHold'
assert compatibility(old[0], rotated[0], old[1], rotated[1], 2, 2) == 'StaleProofHold'
assert compatibility(new[0], new[0], old[1], new[1], 2, 2) == 'Admitted'
assert compatibility(rotated[0], rotated[0], old[1], rotated[1], 2, 2) == 'Admitted'
assert compatibility(new[0], new[0], old[1], new[1], 2, 1) == 'RollbackReaderCapabilityHold'
assert compatibility(drift[0], drift[0], old[1], drift[1], 2, 2) == 'SemanticReplayRequired'

# LB-06: toy registered schemas and one 1->2 edge; no source/signature verification.
@dataclass(frozen=True)
class Stored:
    sequence: int
    version: int
    payload: bytes

@dataclass(frozen=True)
class View:
    sequence: int
    version: int
    payload: bytes
    stored_digest: bytes

class ModelFailure(Exception):
    def __init__(self, reason):
        self.reason = reason

class Reader:
    def __init__(self):
        self.edges = {(1, 2): lambda data: {'delta': data['increment']}}
        self.hop_calls = 0
        self.issued = set()

    def resolve(self, item):
        if isinstance(item, View):
            # In-memory issued-view membership stands in for verified authority.
            if item not in self.issued:
                raise ModelFailure('UnverifiedView')
            return item  # No alias/schema hop is rerun for an admitted view.
        if item.version not in (1, 2):
            raise ModelFailure('HandlerCapabilityMismatch')
        try:
            data = json.loads(item.payload)
        except (ValueError, UnicodeError):
            raise ModelFailure('EventSchemaRejected')
        key = 'increment' if item.version == 1 else 'delta'
        if not isinstance(data, dict) or set(data) != {key} or type(data[key]) is not int:
            raise ModelFailure('EventSchemaRejected')
        if item.version == 1:
            data = self.edges[(1, 2)](data)
            self.hop_calls += 1
        payload = json.dumps(data, separators=(',', ':'), sort_keys=True).encode()
        view = View(item.sequence, 2, payload,
                    H(N(item.sequence) + I(item.version) + B(item.payload)))
        self.issued.add(view)
        return view

def consume(reader, items):
    last_good, last_sequence = b'{"n":0}', 0
    for item in items:
        try:
            view = reader.resolve(item)
            if view.sequence != last_sequence + 1:
                raise ModelFailure('MissingSequence')
            working = json.loads(last_good)
            delta = json.loads(view.payload)['delta']
            working['n'] += delta
            if delta == -999:
                raise ModelFailure('ApplyFailed')  # Mutation occurred only in working copy.
            last_good = json.dumps(working, separators=(',', ':')).encode()
            last_sequence = view.sequence
        except ModelFailure as failure:
            return ('Failed', failure.reason, last_sequence, last_good)
    return ('Succeeded', None, last_sequence, last_good)

history = (Stored(1, 1, b'{"increment":2}'), Stored(2, 2, b'{"delta":3}'),
           Stored(3, 1, b'{"increment":4}'))
original_payloads = tuple(item.payload for item in history)
reader = Reader()
views = tuple(reader.resolve(item) for item in history)
assert reader.hop_calls == 2  # Exactly one registered hop for each v1 source; zero for v2.
expected = ('Succeeded', None, 3, b'{"n":9}')
consumers = ('command', 'replay', 'projection', 'reconstruction', 'inspection')
for consumer in consumers:
    assert consume(reader, views) == expected
    assert reader.hop_calls == 2  # Reusing verified views never adapts again.
    independent = Reader()
    assert consume(independent, history) == expected
    assert independent.hop_calls == 2  # Independent reads apply the same registered meaning.
    failed = Reader()
    poison = (history[0], Stored(2, 2, b'{"wrong":3}'), history[2])
    assert consume(failed, poison) == ('Failed', 'EventSchemaRejected', 1, b'{"n":2}')
    assert failed.hop_calls == 1  # Never process history after the bad source.
    failed_apply = Reader()
    throws = (history[0], Stored(2, 2, b'{"delta":-999}'), history[2])
    assert consume(failed_apply, throws) == ('Failed', 'ApplyFailed', 1, b'{"n":2}')
assert consume(reader, (replace(views[0], payload=b'{"delta":99}'),)) == (
    'Failed', 'UnverifiedView', 0, b'{"n":0}')
assert tuple(item.payload for item in history) == original_payloads

class Cursor:
    def __init__(self, data):
        self.data, self.pos = data, 0

    def take(self, size):
        if size < 0 or size > len(self.data) - self.pos:
            raise ValueError('truncated')
        value = self.data[self.pos:self.pos + size]
        self.pos += size
        return value

    def number(self, bits):
        return read_scalar(self.take(bits // 8), bits)

    def blob(self, maximum=2 * MiB):
        size = int.from_bytes(self.take(4), 'big')  # u32, NOT signed I.
        if size > maximum:
            raise ValueError('bounded length')
        return self.take(size)

    def text(self, maximum=2 * MiB):
        return self.blob(maximum).decode('utf-8', errors='strict')

    def end(self):
        if self.pos != len(self.data):
            raise ValueError('trailing')

def decode_scope(data):
    c = Cursor(data)
    names = [c.text() for _ in range(5)]
    head, target = c.number(64), c.number(64)
    hashes = [c.take(32) for _ in range(3)]
    mode = c.take(1)
    c.end()
    if target > head or mode not in (b'\x00', b'\x01'):
        raise ValueError('scope')
    return names, head, target, hashes, mode

# LB-07: complete 19-field P and transcript-v2 literal byte known answers.
PSEP = b'HX-EV-REPLAY-TRANSITION-1\0'
transition_fields = [
    B(scope), N(1), I(3), H(b'pinned-request'),
    H(b'HX-EV-REPLAY-TRANSITION-GENESIS-1\0\x01' + B(scope)),
    H(b'{"n":0}'), H(b'{"n":3}'), H(b'stored-accumulator'), H(b'effective-chain'),
    H(b'page-proof'), timeline([]), root, I(3), N(21),
    H(U32(3) + b''.join(B(e) for e in entries)), U('state-json'), N(1), U('op'), b'\x01',
]
def transition(fields):
    assert len(fields) == 19
    return PSEP + b'\x01\x00\x13' + b''.join(
        bytes([tag]) + value for tag, value in enumerate(fields, 1))

def decode_transition(data):
    prefix = PSEP + b'\x01\x00\x13'
    if not data.startswith(prefix):
        raise ValueError('transition header/count')
    pos, decoded = len(prefix), []
    widths = [None, 8, 4, 32, 32, 32, 32, 32, 32, 32, 32, 32, 4, 8, 32,
              None, 8, None, 1]
    for tag, width in enumerate(widths, 1):
        if pos >= len(data) or data[pos] != tag:
            raise ValueError('transition tag')
        pos += 1
        if width is None:
            if pos + 4 > len(data):
                raise ValueError('transition length')
            width = int.from_bytes(data[pos:pos + 4], 'big')
            pos += 4
            if width > 64 * MiB:
                raise ValueError('transition bound')
        if pos + width > len(data):
            raise ValueError('transition truncated')
        value = data[pos:pos + width]
        if tag == 1:
            decode_scope(value)
        if tag in (2, 14, 17):
            read_scalar(value, 64)
        if tag in (3, 13):
            read_scalar(value, 32)
        if tag in (16, 18):
            value.decode('utf-8', errors='strict')
        if tag == 19 and value not in (b'\x00', b'\x01'):
            raise ValueError('transition boolean')
        decoded.append(value)
        pos += width
    if pos != len(data):
        raise ValueError('transition trailing')
    return decoded

golden_p = bytes.fromhex(
    '48582d45562d5245504c41592d5452414e534954494f4e2d3100010013010000008c0000000174000000016400000001'
    '61000000026964000000026f7000000000000000030000000000000003872491a30d60d598962de6e7b834ab76b2aa65'
    'fbab102c6ebaaae6acdc238822aa214ea38326805d95661c3ad1643cc07f88e2bae0438ac0448a66d93335ca6e97a5e4'
    '1b45ddd2b2381046e77ccfa5c45d3553b81b66aeba7ed5c460c660f93f01020000000000000001030000000304bc765d'
    'cf6d312884e1e40629de7b0b570d70c57c4ffcf6a597fbf40d396bcafd05397d9cb1956fc79e8cf1865d2034f7fe6186'
    '8789afdbc1092a0a5032fc81a60606f3013f933b9fb80ab6d995e7ad9da36f683837ba1d81e950c943d40111eac2f007'
    '215ddd5567ca2590efd4ea109b4e56cbe591e2676fbf54a9262692c539166da6084e16e5752126daee374f95fb3f2f84'
    'f6219e215e01c297a27b986c5bfe629081092404b0141443f8a281c1f67a2a96a584765b5857ede13596c5141fc59505'
    'b6720a0a53f435466bdfd70e7b4cfda912a879dd8a0c8b3315e37b641467249b1bbecd0bbfbf83d4e657af823b6ec32d'
    '29731e58c70c3a6bd60a58dd36203d0610758bde0c2376c63b2d88ea99bb8f91a43bc1879ce92929c9d3bb41b7b919ec'
    '7a9dbebfce0d000000030e00000000000000150feead8d56447df54028f01e768d7e4db60817f95e52a1aeebd912cdad'
    '85d32a3b100000000a73746174652d6a736f6e11000000000000000112000000026f701301')
p = transition(transition_fields)
assert len(p) == 565 and p == golden_p
assert H(p).hex() == 'ed02a36e49aa3e0cb2a5a2b4326e48eaf6f245b1bca31a69e04186d2aea0064f'
decoded_p = decode_transition(p)
assert len(decoded_p) == 19 and decoded_p[0] == scope
assert decoded_p[12:14] == [I(3), N(21)]
assert decoded_p[15:] == [b'state-json', N(1), b'op', b'\x01']
rejects(lambda: decode_transition(p[:-1]))
rejects(lambda: decode_transition(p + b'\x00'))
rejects(lambda: decode_transition(p[:-1] + b'\x02'))
wrong_count = bytearray(p)
wrong_count[len(PSEP) + 2] = 18
rejects(lambda: decode_transition(bytes(wrong_count)))
wrong_tag = bytearray(p)
wrong_tag[len(PSEP) + 3] = 2
rejects(lambda: decode_transition(bytes(wrong_tag)))

genesis = b'HX-EV-COMMAND-PAGES-1\0\x02' + B(scope)
assert genesis == bytes.fromhex(
    '48582d45562d434f4d4d414e442d50414745532d3100020000008c000000017400000001640000000161000000026964'
    '000000026f7000000000000000030000000000000003872491a30d60d598962de6e7b834ab76b2aa65fbab102c6ebaaa'
    'e6acdc238822aa214ea38326805d95661c3ad1643cc07f88e2bae0438ac0448a66d93335ca6e97a5e41b45ddd2b23810'
    '46e77ccfa5c45d3553b81b66aeba7ed5c460c660f93f01')
assert H(genesis).hex() == '596dfbbe1d1c48a479d2ca3572e3c64fc3c7f3196a2b32a0e91ec6822a584780'
step = b'HX-EV-COMMAND-PAGE-STEP-1\0\x02' + H(genesis) + H(p)
assert step == bytes.fromhex(
    '48582d45562d434f4d4d414e442d504147452d535445502d310002596dfbbe1d1c48a479d2ca3572e3c64fc3c7f3196a'
    '2b32a0e91ec6822a584780ed02a36e49aa3e0cb2a5a2b4326e48eaf6f245b1bca31a69e04186d2aea0064f')
assert H(step).hex() == 'e090ef651446afa0c83dbcb4a662ee3641590ae4c87e857663b9ec0ad411ad4c'
changed_fields = transition_fields.copy()
changed_fields[11] = H(b'changed-timeline-root')
changed_step = b'HX-EV-COMMAND-PAGE-STEP-1\0\x02' + H(genesis) + H(transition(changed_fields))
assert H(changed_step) != H(step)
assert H(b'HX-EV-COMMAND-PAGE-STEP-1\0\x01' + H(genesis) + H(p)) != H(step)
# Additional review-loop checks follow; the original valid LB-07 bytes stay fixed.

# LB-08: signed scalar ranges on encode and decode, distinct unsigned framing.
for bits, encode in ((32, I), (64, N)):
    maximum = (1 << (bits - 1)) - 1
    assert read_scalar(encode(0), bits) == 0
    assert read_scalar(encode(maximum), bits) == maximum
    rejects(lambda: encode(-1))
    rejects(lambda: encode(maximum + 1))
    rejects(lambda: read_scalar((maximum + 1).to_bytes(bits // 8, 'big'), bits))
    rejects(lambda: read_scalar(b'\xff' * (bits // 8), bits))
assert U32(1 << 31) == b'\x80\x00\x00\x00'
assert U32((1 << 32) - 1) == b'\xff' * 4
rejects(lambda: U32(1 << 32))
for field_index, bits in ((1, 64), (2, 32), (12, 32), (13, 64), (16, 64)):
    bad_fields = transition_fields.copy()
    bad_fields[field_index] = (1 << (bits - 1)).to_bytes(bits // 8, 'big')
    rejects(lambda: decode_transition(transition(bad_fields)))
for offset in (27, 35):  # Five U fields occupy 27 bytes in the original scope.
    bad_scope = scope[:offset] + (1 << 63).to_bytes(8, 'big') + scope[offset + 8:]
    rejects(lambda: decode_scope(bad_scope))
    bad_fields = transition_fields.copy()
    bad_fields[0] = B(bad_scope)
    rejects(lambda: decode_transition(transition(bad_fields)))
for offset, bits in ((len(SEP) + 4, 64), (len(SEP) + 4 + 8 + 1 + 32
                                         + 1 + len(U('Legacy.Event'))
                                         + 1 + len(U('evt')) + 1, 32)):
    raw = entries[0]
    invalid = raw[:offset] + (1 << (bits - 1)).to_bytes(bits // 8, 'big') + raw[offset + bits // 8:]
    rejects(lambda: decode(invalid))

# LB-09: diagnostic authority separation model, no provider/source authentication.
def sandbox(base_verified, empty_simulation, outputs, cancel=False, invocation_error=False):
    if not base_verified and not empty_simulation:
        return ('BaseProofHold', None, 0, 0)
    if cancel:
        return ('Cancelled', None, 0, 0)
    if invocation_error:
        return ('error', None, 0, 0)
    # All hypothetical output validated before Apply; no Stored/route proof is made.
    if len(outputs) > 1000 or any(type(x) is not int for x in outputs):
        return ('SandboxOutputRejected', None, 0, 0)
    state = 0 if empty_simulation else 5
    calls = 0
    for delta in outputs:
        prior = state
        state += delta
        calls += 1
        if delta == -999:
            return ('ApplyFailed', prior, calls, 0)
    return ('accepted', state, calls, 0)  # Last element = durable mutations.
assert sandbox(True, False, [2, 3]) == ('accepted', 10, 2, 0)
assert sandbox(False, True, [2]) == ('accepted', 2, 1, 0)
assert sandbox(False, False, [2])[0:3] == ('BaseProofHold', None, 0)
assert sandbox(True, False, [2, 'bad'])[0:3] == ('SandboxOutputRejected', None, 0)
assert sandbox(True, False, [-999]) == ('ApplyFailed', 5, 1, 0)
assert sandbox(True, False, [], cancel=True) == ('Cancelled', None, 0, 0)
assert sandbox(True, False, [], invocation_error=True)[0] == 'error'

# LB-10: literal zero-event P hashes and terminal transcript hashes (not signed).
def scope_at(k, mode=False, head=None):
    head = k if head is None else head
    return (U('t') + U('d') + U('a') + U('id') + U('zero') + N(head) + N(k)
            + H(b'registry') + H(b'transform') + H(b'apply') + bytes([mode]))

def zero_completion(k, mode=False, head=None):
    if k and mode:
        raise ValueError('snapshot cannot supply complete timeline')
    scoped = scope_at(k, mode, head)
    _, admitted_head, target, _, _ = decode_scope(scoped)
    if k != 0 and admitted_head != target:
        raise ValueError('snapshot is not zero-tail')
    state = b'{"n":0}' if k == 0 else b'{"n":7}'
    start = 1 if k == 0 else k if k == MAX else k + 1
    t0 = H(b'HX-EV-REPLAY-TIMELINE-1\0\x01' + B(scoped))
    stored = H(b'HX-EV-CHECKPOINT-CHAIN-1\0\x01' + U('t') + U('d') + U('id') + U('a')) if k == 0 else H(b'witnessed-accumulator' + N(k))
    effective = H(b'HX-EV-COMMAND-EFFECTIVE-1\0\x01' + U('t') + U('d') + U('id') + U('a') + N(k))
    fields = [B(scoped), N(start), I(0), H(b'exact-selected-zero-request' + N(k)),
              H(b'HX-EV-REPLAY-TRANSITION-GENESIS-1\0\x01' + B(scoped)),
              H(state), H(state), stored, effective, H(b'exact-zero-prefix' + N(k)),
              t0, t0, I(0), N(0), H(U32(0)), U('state-json'), N(1), U('zero'), b'\x01']
    encoded = transition(fields)
    transcript_genesis = H(b'HX-EV-COMMAND-PAGES-1\0\x02' + B(scoped))
    terminal = H(b'HX-EV-COMMAND-PAGE-STEP-1\0\x02' + transcript_genesis + H(encoded))
    assert terminal != transcript_genesis
    decoded = decode_transition(encoded)
    assert decoded[5] == decoded[6] == H(state)
    assert decoded[10] == decoded[11] == t0
    assert decoded[12:15] == [I(0), N(0), H(b'\0\0\0\0')]
    assert decoded[18] == b'\x01'
    return encoded, terminal

ZERO_GOLDENS = {
    0: ('5aa8b5cd227161f9fad77354c7fab627485f1a276517f6560562656c143a578b',
        'd72fffd5273de2b5e7d2898b9bca2add448c0dda7607ebc553d5d1f2d6a38847'),
    7: ('f43ddfb89ffc4400a906ace90c312cb727ab58dc5f246ec57483f1741ba84b9f',
        '194ec8f55dc1cbdb758614568caea8983b0e4c57be8a0ab8e65faaf92b854c0b'),
    MAX: ('963a35e071e837b449c4f97bc40d9c73b5a9cd8ea7c7af4049310964e70b371b',
          '310f91f0ea4ba0c7067fec762cf0a05cecceb117288566599def3e565555aeb4'),
}
for k in (0, 7, MAX):
    encoded, terminal = zero_completion(k)
    assert (H(encoded).hex(), terminal.hex()) == ZERO_GOLDENS[k]
assert zero_completion(0, True) != zero_completion(0, False)
empty_target, empty_target_terminal = zero_completion(0, head=9)
assert decode_scope(decode_transition(empty_target)[0])[1:3] == (9, 0)
assert empty_target_terminal != zero_completion(0)[1]
rejects(lambda: zero_completion(7, True))

# Shared bounded tagged decoder for LB-11 and LB-14; no signature verification.
def record(separator, values):
    return separator + b'\x01' + struct.pack('>H', len(values)) + b''.join(
        bytes([i]) + value for i, value in enumerate(values, 1))

def parse_record(raw, separator, kinds, maximum=2 * MiB):
    if len(raw) > maximum:
        raise ValueError('record bound')
    c = Cursor(raw)
    expected = separator + b'\x01' + struct.pack('>H', len(kinds))
    if c.take(len(expected)) != expected:
        raise ValueError('record header')
    values = []
    for tag, kind in enumerate(kinds, 1):
        if c.take(1) != bytes([tag]):
            raise ValueError('record tag')
        value = (c.number(32) if kind == 'I' else c.number(64) if kind == 'N'
                 else c.text() if kind == 'U' else c.blob() if kind == 'B'
                 else c.take(32) if kind == 'H' else c.take(1))
        values.append(value)
    c.end()
    return values

# LB-11: exact manifest known answer; opaque cipher/tag are framing placeholders.
MSEP = b'HX-EV-REPLAY-TIMELINE-MANIFEST-1\0'
MKINDS = ['B', 'N', 'I', 'H', 'H', 'H', 'N', 'I', 'I', 'B', 'H', 'H']
zero_p, zero_terminal = zero_completion(0)
zero_fields = decode_transition(zero_p)
mscope = scope_at(0)
logical = U32(0)
cbinding = B(mscope) + N(1) + zero_fields[3] + H(zero_p) + I(0) + N(0) + I(len(logical))
ph = H(b'HX-EV-TIMELINE-PLAIN-1\0\x01' + B(cbinding) + B(logical))
descriptor = U('model-provider') + U('opaque-model') + U('key') + B(b'nonce') + H(b'config')
aad = b'HX-EV-TIMELINE-AAD-1\0\x01' + B(cbinding) + ph + B(descriptor)
# These are NOT encrypted/protected production evidence.
cipher, tag = b'opaque-ciphertext', b'opaque-tag'
obj = b'HX-EV-TIMELINE-CHUNK-1\0\x01' + B(cbinding) + ph + B(descriptor) + B(cipher) + B(tag)
ch = H(b'HX-EV-TIMELINE-CIPHER-1\0\x01' + B(obj))
chunk_key = 'replay-timeline-chunk:' + ch.hex()
rows = U32(1) + I(0) + N(0) + I(4) + ph + ch + U(chunk_key) + I(len(obj))
manifest_values = [B(mscope), N(1), I(0), zero_fields[3], H(zero_p), H(logical),
                   N(4), I(0), I(1), B(rows), zero_fields[11], zero_fields[6]]
manifest = record(MSEP, manifest_values)
MANIFEST_HEX = (
    '48582d45562d5245504c41592d54494d454c494e452d4d414e49464553542d310001000c010000008e00000001740000'
    '0001640000000161000000026964000000047a65726f00000000000000000000000000000000872491a30d60d598962d'
    'e6e7b834ab76b2aa65fbab102c6ebaaae6acdc238822aa214ea38326805d95661c3ad1643cc07f88e2bae0438ac0448a'
    '66d93335ca6e97a5e41b45ddd2b2381046e77ccfa5c45d3553b81b66aeba7ed5c460c660f93f00020000000000000001'
    '030000000004b8a831ccba389ef07c9fb028653afea43105b30995daa9826b44697f51b3bbbc055aa8b5cd227161f9fa'
    'd77354c7fab627485f1a276517f6560562656c143a578b06df3f619804a92fdb4057192dc43dd748ea778adc52bc498c'
    'e80524c014b81119070000000000000004080000000009000000010a000000b200000001000000000000000000000000'
    '000000048d647be0cd2a80c8c7aab27642669d8077cc58a7b59e10644ba02320085a5241e0a8e402cab8d6f74b68b00a'
    'ba4ba34c8dcfeb1e0dc6c52b8bad1a6df67af616000000567265706c61792d74696d656c696e652d6368756e6b3a6530'
    '613865343032636162386436663734623638623030616261346261333463386463666562316530646336633532623862'
    '61643161366466363761663631360000019f0b6b091c27367de1d6f873951abd27678774120df83886a0aa9d4cef09b6'
    '3502010cf3013f933b9fb80ab6d995e7ad9da36f683837ba1d81e950c943d40111eac2f0')
assert manifest == bytes.fromhex(MANIFEST_HEX)
assert H(manifest).hex() == '293ee34a67b37993cf0ea5e2f891f11cd286187b392895a293b8944eee9017fe'

# Verify framing, offsets, scope, lengths, PH/CH and binding for this one-chunk model.
def verify_manifest(raw, candidate_obj, plain):
    m = parse_record(raw, MSEP, MKINDS)
    decode_scope(m[0])
    if m[:9] != [mscope, 1, 0, zero_fields[3], H(zero_p), H(logical), 4, 0, 1]:
        raise ValueError('manifest binding/count')
    c = Cursor(m[9])
    if int.from_bytes(c.take(4), 'big') != m[8]:
        raise ValueError('chunk count')
    row = [c.number(32), c.number(64), c.number(32), c.take(32), c.take(32), c.text(), c.number(32)]
    c.end()
    if row != [0, 0, 4, ph, ch, chunk_key, len(candidate_obj)]:
        raise ValueError('row binding/order/length')
    if H(b'HX-EV-TIMELINE-CIPHER-1\0\x01' + B(candidate_obj)) != row[4]:
        raise ValueError('cipher object')
    if H(b'HX-EV-TIMELINE-PLAIN-1\0\x01' + B(cbinding) + B(plain)) != row[3]:
        raise ValueError('plaintext')
    if H(plain) != m[5] or m[10:] != [zero_fields[11], zero_fields[6]]:
        raise ValueError('logical/state/timeline')
    return 'AdmittedFramingOnly'
assert verify_manifest(manifest, obj, logical) == 'AdmittedFramingOnly'
rejects(lambda: verify_manifest(manifest, obj[:-1] + b'X', logical))
rejects(lambda: verify_manifest(manifest, obj, b'\0\0\0\1'))
rejects(lambda: verify_manifest(manifest + b'X', obj, logical))
for idx, replacement in ((0, B(scope)), (3, H(b'other-request')), (4, H(b'other-P')),
                          (8, I(2)), (6, (1 << 63).to_bytes(8, 'big')),
                          (2, (1 << 31).to_bytes(4, 'big'))):
    changed = manifest_values.copy()
    changed[idx] = replacement
    rejects(lambda: verify_manifest(record(MSEP, changed), obj, logical))
# Alter a chunk offset with a decoded high bit: reject before comparison/allocation.
changed = manifest_values.copy()
changed[9] = B(rows[:8] + (1 << 63).to_bytes(8, 'big') + rows[16:])
rejects(lambda: verify_manifest(record(MSEP, changed), obj, logical))
assert H(aad) != H(aad + b'changed-metadata')

# LB-12: pinned-root read/commit model; no provider CAS, TTL or durable lease proof.
class PriorSession:
    def __init__(self, current_generation, expected_generation, immutable_rows):
        if current_generation != expected_generation:
            raise ValueError('ProjectionPriorConflict')
        self.generation = expected_generation
        self.rows = dict(immutable_rows)
        self.closed = False

    def read(self, key):
        if self.closed:
            raise ValueError('ProjectionPriorSessionClosed')
        return self.rows[key]

    def commit(self, current_generation):
        if self.closed or current_generation != self.generation:
            raise ValueError('ProjectionPriorConflict')
        return 'PreparedModelMayCommit'
rows_at_one = {'total': 5}
prior_session = PriorSession(1, 1, rows_at_one)
rows_at_one['total'] = 99  # A newer mutable view cannot replace privately pinned input.
assert prior_session.read('total') == 5
rejects(lambda: PriorSession(2, 1, rows_at_one))
rejects(lambda: prior_session.commit(2))
assert prior_session.commit(1) == 'PreparedModelMayCommit'
prior_session.closed = True
rejects(lambda: prior_session.read('total'))

# LB-13: derived query cache bypass/capacity model with code-only drift.
class VersionedQuery:
    def __init__(self):
        self.calls = 0
        self.retained_payload = 0
        self.retained_proof = 0

    def execute(self, implementation, rows, ordinary_etag, cache_capacity):
        self.calls += 1
        # ETag/cache capacity intentionally have no cache authority here.
        result = implementation(rows)
        assert self.retained_payload == self.retained_proof == 0
        return result
q = VersionedQuery()
assert q.execute(lambda r: sum(r), [2, 3], 'same', 0) == 5
assert q.execute(lambda r: sum(r) * 2, [2, 3], 'same', 0) == 10
assert q.calls == 2 and q.retained_payload == q.retained_proof == 0
assert total([64 * MiB, 8 * MiB, 56 * MiB]) == 128 * MiB
assert total([64 * MiB, 8 * MiB, 56 * MiB, 1]) > 128 * MiB

# LB-14: exact selector codec + endpoint/version/registry negotiation model.
SELECTSEP = b'HX-EV-REPLAY-SELECT-1\0'
SKINDS = ['B', 'I', 'U', 'U', 'U', 'U', 'H', 'H', 'H', 'N', 'H', 'U', 'U', 'N', 'N', 'H']
selection_values = [B(mscope), I(2), U('gateway'), U('receiver'), U('verifier'), U('instance'),
                    H(b'registry'), H(b'transform'), H(b'apply'), N(1), H(b'approved-capability'),
                    U('hx-ev-command-pages-v2'), U('hx-ev-replay-timeline-manifest-v1'),
                    N(1), N(2), bytes(range(32))]
selection = record(SELECTSEP, selection_values)
SELECT_HASH = '44600a3d68552fffc7552c19359edf08f5a026ab02f2789bec5546fb87fa23c8'
assert H(selection).hex() == SELECT_HASH

def negotiate(raw, endpoints, versions, active_registry, receiver_instance='instance',
              capability_revision=1, transcript_codec='hx-ev-command-pages-v2',
              manifest_codec='hx-ev-replay-timeline-manifest-v1'):
    if raw is None or versions != (2, 2, 2):
        raise ValueError('ReplayTranscriptCapabilityHold')
    values = parse_record(raw, SELECTSEP, SKINDS)
    _, _, _, scoped_hashes, _ = decode_scope(values[0])
    if (values[1] != 2 or tuple(values[2:5]) != endpoints
            or values[6] != active_registry or values[6:9] != scoped_hashes
            or values[5] != receiver_instance
            or values[9] <= 0 or values[9] != capability_revision
            or values[10] != H(b'approved-capability')
            or values[11:13] != [transcript_codec, manifest_codec]
            or values[13] >= values[14]):
        raise ValueError('ReplayTranscriptCapabilityHold')
    return 'ModelSelected2'
assert negotiate(selection, ('gateway', 'receiver', 'verifier'), (2, 2, 2), H(b'registry')) == 'ModelSelected2'
rejects(lambda: negotiate(None, ('gateway', 'receiver', 'verifier'), (2, 2, 2), H(b'registry')))
rejects(lambda: negotiate(selection, ('gateway', 'other', 'verifier'), (2, 2, 2), H(b'registry')))
rejects(lambda: negotiate(selection, ('gateway', 'receiver', 'verifier'), (2, 1, 2), H(b'registry')))
rejects(lambda: negotiate(selection, ('gateway', 'receiver', 'verifier'), (2, 2, 2), H(b'new-registry')))
# All use the unchanged selector literal; negatives never re-sign it.
for idx, replacement in ((5, U('other-instance')), (9, N(0)), (9, N(2)),
                          (10, H(b'other-capability')), (11, U('unsupported-transcript')),
                          (12, U('unsupported-manifest'))):
    changed = selection_values.copy()
    changed[idx] = replacement
    rejects(lambda: negotiate(record(SELECTSEP, changed), ('gateway', 'receiver', 'verifier'),
                              (2, 2, 2), H(b'registry')))
rejects(lambda: negotiate(selection, ('gateway', 'receiver', 'verifier'), (2, 2, 2),
                          H(b'registry'), receiver_instance='new-live-instance'))
for idx, bits in ((1, 32), (9, 64), (13, 64), (14, 64)):
    changed = selection_values.copy()
    changed[idx] = (1 << (bits - 1)).to_bytes(bits // 8, 'big')
    rejects(lambda: parse_record(record(SELECTSEP, changed), SELECTSEP, SKINDS))
# LB-15: exact new query-row framing + catalog/code/closure drift model.
# Input 52/59/G rows below are synthetic stand-ins, not provider/signed fixtures.
def query_row(values, domain='d', query_type='total'):
    if not domain or not query_type or len(values) != 15:
        raise ValueError('query route/count')
    result = b'\x5b' + U(domain) + U(query_type) + b'\x00\x0f' + b''.join(
        bytes([tag]) + value for tag, value in enumerate(values, 1))
    if len(result) > 64 * 1024:
        raise ValueError('RegistryLimit')
    return result

QKINDS = ['U', 'H', 'H', 'U', 'H', 'H', 'U', 'U', 'H', 'U', 'H', 'U', 'H', 'H', 'B']
def parse_query_row(raw):
    if len(raw) > 64 * 1024:
        raise ValueError('RegistryLimit')
    c = Cursor(raw)
    if c.take(1) != b'\x5b':
        raise ValueError('query row tag')
    key = c.text(), c.text()
    if any(not 1 <= len(name) <= 64 or any(ch not in 'abcdefghijklmnopqrstuvwxyz0123456789-' for ch in name)
           or name[0] == '-' or name[-1] == '-' for name in key):
        raise ValueError('canonical route')
    if c.take(2) != b'\x00\x0f':
        raise ValueError('query row count')
    fields = []
    for tag, kind in enumerate(QKINDS, 1):
        if c.take(1) != bytes([tag]):
            raise ValueError('query row field')
        fields.append(c.text() if kind == 'U' else c.blob(64 * 1024) if kind == 'B' else c.take(32))
    c.end()
    if any(not fields[i] for i in (0, 3, 6, 7, 9, 11)):
        raise ValueError('required query identifier')
    bindings = Cursor(fields[-1])
    count = int.from_bytes(bindings.take(4), 'big')
    if not 1 <= count <= 256:
        raise ValueError('binding count')
    rows = []
    for _ in range(count):
        item = Cursor(bindings.blob(64 * 1024))
        names = [item.text() for _ in range(4)]
        backend, aggregate = item.blob(), item.text()
        item.end()
        if any(not n for n in names) or names[2] not in ('aggregate', 'shared'):
            raise ValueError('root binding')
        if not backend or bool(aggregate) != (names[2] == 'aggregate'):
            raise ValueError('root ownership')
        rows.append(tuple(n.encode() for n in names) + (backend, aggregate.encode()))
    bindings.end()
    if rows != sorted(set(rows)):
        raise ValueError('root order/duplicate')
    return key, fields

schema_descriptor = (U('json') + H(b'serializer') + H(b'options') + B(b'object-schema')
                     + U('validator') + H(b'validator-code') + H(b'validator-options'))
backend = b'HX-EV-STATE-BACKEND-1\0\x01' + U('p') + U('cluster') + U('ns') + H(b'endpoint') + U('store')
root_binding = U('route') + U('space') + U('shared') + U('store') + B(backend) + U('')
qvalues = [U('Query, Assembly'), H(b'query-code'), H(b'query-options'), U('registration'),
           H(b'registration-code'), H(b'registration-options'), U('endpoint'), U('request-json'),
           H(schema_descriptor), U('response-json'), H(schema_descriptor), U('plan'),
           H(b'plan-code'), H(b'plan-options'), B(U32(1) + B(root_binding))]
qrow = query_row(qvalues)
assert parse_query_row(qrow)[0] == ('d', 'total')
input_rows = [b'\x52' + U('d') + U('route') + b'\x00\x00',
              b'\x59' + U('d') + U('route') + U('space') + b'\x00\x00']
query_g = b'\x47' + U('d') + U('query-dependency') + U('managed') + b'\x00\x00'
def query_digest(row, selected_input_rows, selected_g):
    return H(b'HX-EV-QUERY-ROUTE-1\0\x01' + B(row)
             + U32(len(selected_input_rows)) + b''.join(B(r) for r in selected_input_rows)
             + U32(len(selected_g)) + b''.join(B(r) for r in selected_g))

def model_catalog(query_rows):
    admitted = [parse_query_row(r) for r in query_rows]
    keys = [key for key, fields in admitted]
    if len(set(keys)) != len(keys):
        raise ValueError('duplicate query route')
    by_key = sorted(zip(keys, query_rows))
    return H(b'HX-EV-HANDLERS-1\0\x01' + U32(len(by_key)) + b''.join(r for k, r in by_key))

initial_query_hash = query_digest(qrow, input_rows, [query_g])
for index, new_value in ((1, H(b'new-code')), (2, H(b'new-options')), (6, U('other-endpoint')),
                         (8, H(b'new-request-schema')), (10, H(b'new-response-schema')),
                         (12, H(b'new-plan-code'))):
    changed = qvalues.copy()
    changed[index] = new_value
    candidate = query_row(changed)
    assert query_digest(candidate, input_rows, [query_g]) != initial_query_hash
    assert model_catalog([candidate]) != model_catalog([qrow])
assert query_digest(qrow, input_rows, [query_g + b'changed']) != initial_query_hash
assert query_digest(qrow, input_rows[:-1], [query_g]) != initial_query_hash
assert model_catalog([]) == H(b'HX-EV-HANDLERS-1\0\x01' + U32(0))  # No absent-row marker.
rejects(lambda: model_catalog([qrow, qrow]))
rejects(lambda: parse_query_row(query_row(qvalues, query_type='Total')))
assert parse_query_row(query_row(qvalues, domain='d' * 64, query_type='q' * 64))[0] == ('d' * 64, 'q' * 64)
assert parse_query_row(query_row(qvalues, domain='d', query_type='q'))[0] == ('d', 'q')
rejects(lambda: parse_query_row(query_row(qvalues, domain='d' * 65)))
rejects(lambda: parse_query_row(query_row(qvalues, query_type='q' * 65)))
for index in (0, 3, 6, 7, 9, 11):
    changed = qvalues.copy()
    changed[index] = U('')
    rejects(lambda: parse_query_row(query_row(changed)))
rejects(lambda: parse_query_row(qrow + b'X'))
rejects(lambda: query_row(qvalues[:-1]))
changed = qvalues.copy()
changed[-1] = B(U32(2) + B(root_binding) + B(root_binding))
rejects(lambda: parse_query_row(query_row(changed)))
changed[-1] = B(U32(257))
rejects(lambda: parse_query_row(query_row(changed)))
changed[0] = U('x' * (64 * 1024))
rejects(lambda: query_row(changed))

def model_dispatch(catalog_row, expected_query_hash, local_query_hash, endpoint):
    _, fields = parse_query_row(catalog_row)
    if local_query_hash != expected_query_hash or endpoint != fields[6]:
        raise ValueError('QueryCapabilityChanged')
    return fields[0]
assert model_dispatch(qrow, initial_query_hash, initial_query_hash, 'endpoint') == 'Query, Assembly'
rejects(lambda: model_dispatch(qrow, initial_query_hash, H(b'new-code'), 'endpoint'))
rejects(lambda: model_dispatch(qrow, initial_query_hash, initial_query_hash, 'other'))

# Exact tuple checks only; authenticated options/type witness validation stays provider work.
def admit_row_type(mappings, binding, key, tvalue, witnessed_type):
    matches = mappings.get((binding, key), [])
    if len(matches) != 1 or matches[0][0:2] != (witnessed_type, tvalue):
        raise ValueError('ReadModelQueryConsistencyHold')
    return matches[0][2]  # Already admitted input codec/schema/options descriptor hash.
input_mapping = {('space', 'total'): [('TotalValue', 'Total, Assembly', H(schema_descriptor))]}
assert admit_row_type(input_mapping, 'space', 'total', 'Total, Assembly', 'TotalValue') == H(schema_descriptor)
rejects(lambda: admit_row_type(input_mapping, 'space', 'total', 'Other, Assembly', 'TotalValue'))
rejects(lambda: admit_row_type(input_mapping, 'space', 'total', 'Total, Assembly', 'OtherValue'))
rejects(lambda: admit_row_type(input_mapping, 'space', 'unknown', 'Total, Assembly', 'TotalValue'))
ambiguous_mapping = {('space', 'total'): input_mapping[('space', 'total')] * 2}
rejects(lambda: admit_row_type(ambiguous_mapping, 'space', 'total', 'Total, Assembly', 'TotalValue'))

# LB-16: bounded pinned query/read-set model, also modeling the projection TTL fence.
# Auth/proof booleans and integer clock values stand in for verified external evidence.
class QueryIntake:
    def __init__(self, roots, expected_generation, current_generation, rows, allowed,
                 capability, proof_limit=8 * MiB):
        if len(roots) != 1 or expected_generation != current_generation:
            raise ValueError('ReadModelQueryConsistencyHold')
        if not 0 < len(allowed) <= 256 or len(set(allowed)) != len(allowed):
            raise ValueError('ReadModelQueryLimit')
        self.generation = current_generation
        self.rows = dict(rows)  # Immutable scalar values/expiries in this model.
        self.allowed = frozenset(allowed)
        self.capability = capability
        self.read_set = {}
        self.proof_bytes, self.row_bytes, self.proof_limit = 0, 0, proof_limit
        self.calls, self.closed = 0, False
        self.private_result = None

    def read(self, key, now, proof_bytes=1, row_bytes=1, authorized=True):
        if self.closed or key not in self.allowed or not authorized:
            raise ValueError('ReadModelRouteContextRequired')
        value, expiry = self.rows[key]
        if expiry is not None and now >= expiry:
            raise ValueError('ReadModelExpiryPending')
        if key not in self.read_set:
            if total([self.proof_bytes, proof_bytes]) > self.proof_limit or total([self.row_bytes, row_bytes]) > 64 * MiB:
                raise ValueError('ReadModelQueryLimit')
            self.proof_bytes += proof_bytes
            self.row_bytes += row_bytes
            self.read_set[key] = expiry
        return value

    def invoke(self, now):
        self.calls += 1
        self.private_result = sum(self.read(key, now) for key in sorted(self.allowed))

    def complete(self, current_generation, capability, now, authorized=True, proof_valid=True):
        if self.closed or not authorized:
            raise ValueError('ReadModelRouteContextRequired')
        if capability != self.capability:
            raise ValueError('QueryCapabilityChanged')
        if current_generation != self.generation or not proof_valid:
            raise ValueError('ReadModelQueryConsistencyHold')
        if any(expiry is not None and now >= expiry for expiry in self.read_set.values()):
            raise ValueError('ReadModelExpiryPending')
        return self.private_result

    def close(self):
        self.closed = True
        self.private_result = None
        self.read_set.clear()
        self.proof_bytes = self.row_bytes = 0

root_rows = {'a': (2, 5), 'b': (3, None)}
qi = QueryIntake(['R1'], 1, 1, root_rows, ['a', 'b'], initial_query_hash)
assert qi.read('a', 1) == 2
root_rows['b'] = (100, None)  # Concurrent newer root cannot replace a later read.
assert qi.read('b', 1) == 3
qi.invoke(1)
assert qi.complete(1, initial_query_hash, 4) == 5
rejects(lambda: qi.complete(2, initial_query_hash, 4))
rejects(lambda: qi.complete(1, H(b'new-code'), 4))
rejects(lambda: qi.complete(1, initial_query_hash, 4, authorized=False))
rejects(lambda: qi.complete(1, initial_query_hash, 4, proof_valid=False))
rejects(lambda: qi.complete(1, initial_query_hash, 5))  # TTL expires with unchanged generation.
assert qi.calls == 1  # No internal rerun or new-root read at failed completion.
rejects(lambda: qi.read('unplanned', 1))
rejects(lambda: QueryIntake(['R1', 'R2'], 1, 1, root_rows, ['a'], initial_query_hash))
rejects(lambda: QueryIntake(['R2'], 1, 2, root_rows, ['a'], initial_query_hash))
rejects(lambda: QueryIntake(['R1'], 1, 1, {}, list(range(257)), initial_query_hash))
small = QueryIntake(['R1'], 1, 1, root_rows, ['a', 'b'], initial_query_hash, proof_limit=2)
assert small.read('a', 1, proof_bytes=2) == 2
rejects(lambda: small.read('b', 1, proof_bytes=1))
assert 'b' not in small.read_set and small.proof_bytes == 2
qi.close()  # Cancellation/failure cleanup model.
rejects(lambda: qi.read('a', 1))
rejects(lambda: qi.complete(1, initial_query_hash, 1))
assert qi.private_result is None and qi.proof_bytes == qi.row_bytes == 0

# Route-wide incremental mode cannot pick one declaration from a multi-space route.
def admit_fold(mode, declared_spaces):
    if mode == 'incremental' and len(declared_spaces) > 1:
        raise ValueError('ProjectionPriorStateHold')
    return mode
rejects(lambda: admit_fold('incremental', ['a', 'b']))
assert admit_fold('incremental', ['a']) == admit_fold('incremental', []) == 'incremental'
assert admit_fold('full-replay', ['a', 'b']) == 'full-replay'

# LB-17: original admitted-owner evidence versus renewed page generations and live lease.
# Direct existing-codec rejection and fallback decision: snapshot=target=7, actual head=9.
rejects(lambda: zero_completion(7, head=9))
def snapshot_at_target_action(snapshot, target, head):
    if not 0 < snapshot == target <= head <= MAX:
        raise ValueError('snapshot target')
    return ('FullReplay', 1, target, head) if target < head else ('WitnessedZeroTail', target, target, head)
assert snapshot_at_target_action(7, 7, 9) == ('FullReplay', 1, 7, 9)
assert snapshot_at_target_action(7, 7, 7) == ('WitnessedZeroTail', 7, 7, 7)

# Tuples stand in for authenticated ReplayOwnerLease/ledger evidence, not a new codec.
def valid_owner_chain(leases, pages):
    prior_token, prior_cas = 'absent-token-digest', 0
    if not leases or len(leases) != len(pages):
        return False
    for (instance, scratch, cas, consumed, next_token), (p_scratch, ledger_cas) in zip(leases, pages):
        if (instance != 'instance' or cas <= prior_cas or consumed != prior_token
                or p_scratch != scratch or ledger_cas != cas):
            return False
        prior_token, prior_cas = next_token, cas
    return True
renewed_leases = [('instance', 1, 1, 'absent-token-digest', 'token-1'),
                  ('instance', 2, 2, 'token-1', None)]
renewed_pages = [(1, 1), (2, 2)]
assert valid_owner_chain(renewed_leases, renewed_pages)
assert not valid_owner_chain(renewed_leases, [(1, 1), (1, 2)])
for second in [('other', 2, 2, 'token-1', None), ('instance', 2, 1, 'token-1', None),
               ('instance', 2, 2, 'wrong-token', None)]:
    assert not valid_owner_chain([renewed_leases[0], second], renewed_pages)

def recover_final(complete, original_admission, same_instance, current_proofs,
                  all_objects, live_lease, lease_chain_valid=True):
    if not (original_admission and same_instance and current_proofs and all_objects and lease_chain_valid):
        return 'EvidenceHold'
    if complete:
        return 'ExactPinnedResponse'  # Live owner/lease intentionally unnecessary.
    return 'IncompleteMayContinue' if live_lease else 'ReplayRestartRequired'
assert recover_final(True, True, True, True, True, False,
                     valid_owner_chain(renewed_leases, renewed_pages)) == 'ExactPinnedResponse'
assert recover_final(True, True, True, True, True, False, False) == 'EvidenceHold'
assert recover_final(False, True, True, True, True, False) == 'ReplayRestartRequired'
assert recover_final(False, True, True, True, True, True) == 'IncompleteMayContinue'
for slot in range(1, 5):
    args = [True, True, True, True, True, False]
    args[slot] = False
    assert recover_final(*args) == 'EvidenceHold'

# LB-18: shared reservation/recovery decisions; sequential model of a quota fence.
# No threading, backend CAS, deletion proof or actual GiB allocation is claimed.
class ReplayPool:
    def __init__(self, durable):
        self.durable = durable
        self.ready = False

    def reconcile(self):
        if total(v[0] for v in self.durable.values()) > 1024 * MiB:
            raise ValueError('ContinuationCapacityHold')
        self.ready = True

    def reserve(self, identity, size, plan):
        if not self.ready or size < 0:
            raise ValueError('ContinuationCapacityHold')
        old = self.durable.get(identity)
        if old:
            if old[:2] != (size, plan):
                raise ValueError('reservation conflict')
            return old
        if total([*(v[0] for v in self.durable.values()), size]) > 1024 * MiB:
            raise ValueError('ContinuationCapacityHold')
        self.durable[identity] = (size, plan, 'Reserved')
        return self.durable[identity]

    def ambiguous(self, identity):
        size, plan, _ = self.durable[identity]
        self.durable[identity] = (size, plan, 'Ambiguous')

    def release(self, identity, no_future_write, no_readers, deletion_proved):
        if not (no_future_write and no_readers and deletion_proved):
            raise ValueError('ContinuationCapacityHold')
        self.durable.pop(identity, None)  # Idempotent refund under the modeled fence.

journal = {}
pool = ReplayPool(journal)
rejects(lambda: pool.reserve('a/g1', 600 * MiB, b'plan-a'))
pool.reconcile()
pool.reserve('a/g1', 600 * MiB, b'plan-a')
pool.reserve('b/g1', 400 * MiB, b'plan-b')
pool.ambiguous('a/g1')
rejects(lambda: pool.reserve('a/g2', 25 * MiB, b'next-generation'))
assert pool.reserve('a/g1', 600 * MiB, b'plan-a')[2] == 'Ambiguous'
rejects(lambda: pool.reserve('a/g1', 1, b'changed'))
rejects(lambda: pool.release('a/g1', True, True, False))
restarted = ReplayPool(journal)
rejects(lambda: restarted.reserve('c/g1', 1, b'c'))
restarted.reconcile()
rejects(lambda: restarted.reserve('c/g1', 25 * MiB, b'c'))
assert sum(v[0] for v in journal.values()) == 1000 * MiB
restarted.release('a/g1', True, True, True)
restarted.reserve('c/g1', 600 * MiB, b'c')
assert sum(v[0] for v in journal.values()) == 1000 * MiB
# Named generations share the existing aggregate 64 GiB ceiling (integer arithmetic only).
GiB = 1024 * MiB
assert total([60 * GiB, 4 * GiB]) == 64 * GiB
assert total([60 * GiB, 4 * GiB, 1]) > 64 * GiB
# Authenticated same-owner sharing counts once; a distinct copy is an extra capacity.
retained_named_owners = {'shared-old-current': 4 * GiB, 'other-retained': 60 * GiB}
assert total(retained_named_owners.values()) == 64 * GiB
assert total([*retained_named_owners.values(), 4 * GiB]) > 64 * GiB

# Replay and abandoned/concurrent exports use the very same modeled shared pool.
export_journal = {}
export_pool = ReplayPool(export_journal)
export_pool.reconcile()
export_pool.reserve('replay/g1', 900 * MiB, b'replay-plan')
export_pool.reserve('export-1/g1', 100 * MiB, b'encoded-cipher-metadata-pointer')
export_pool.ambiguous('export-1/g1')  # Abandoned/cancelled does not refund bytes.
rejects(lambda: export_pool.reserve('export-2/g1', 25 * MiB, b'next-export'))
rejects(lambda: export_pool.release('export-1/g1', True, False, True))  # Active download.
rejects(lambda: export_pool.release('export-1/g1', True, True, False))
assert total(v[0] for v in export_journal.values()) == 1000 * MiB
export_pool.release('export-1/g1', True, True, True)
export_pool.reserve('export-2/g1', 25 * MiB, b'next-export')
assert total(v[0] for v in export_journal.values()) == 925 * MiB
print('LB-01..LB-18 passed: codec, bounds, ownership, query/root/TTL and quota models; no provider proof')


```

#### Integration handoff and verification evidence

Integrate B2’s explicit V2 complete-save replacement and diagnostic simulation distinction, B4’s selected v2/zero-event/manifest protocol, B5 ownership and B6 phase admission as one change to the single draft; update every producer/verifier capability together. B7 fold-mode and query catalog rows, exact prior-state/single-state intake, dispatcher-installed pinned query session and versioned-cache bypass are explicit additions, not latent capabilities of today’s asynchronous handlers or ETag cache. 6.5c must account for current versus historical proof verification, old endpoint fencing, capacity holds and final-only visibility. No fixture can certify these additions until new production vectors exist; the old signed fixture bytes and their original interpretation remain preserved.

Local verification for this documentation candidate consists of executing LB-01..LB-18, resolving all linked repository paths/section and disposition mappings, `git diff --check`, and comparing preserved-file SHA-256 values. Runtime builds/tests cannot validate these contracts and are intentionally not used as substitute evidence. VB-01..VB-20 remain unexecuted future runtime/provider work, including source readback, allocator measurement, snapshot fallback, rollback, query/root/cache races and durable cleanup. The AD-13 approval receipt remains `UNAPPROVED` and Story 6.6 remains unauthorized.

Review-loop verification performed on 2026-09-27: extracted the sole Python block to `/tmp/6-5b-loop1-checks.py` and ran `python3 /tmp/6-5b-loop1-checks.py`; exit 0, output `LB-01..LB-14 passed: codec, bounds, ownership, simulation, root and cache models; no provider proof`. The original 565-byte LB-07 P, genesis/step bytes and hashes remain unchanged. Added signed negative/max/max+1/decode-overflow cases; fixed empty/snapshot/MAX P and terminal transcript hashes; the complete 564-byte manifest known answer (SHA-256 `293ee34a67b37993cf0ea5e2f891f11cd286187b392895a293b8944eee9017fe`); selector claim hash `44600a3d68552fffc7552c19359edf08f5a026ab02f2789bec5546fb87fa23c8`; and sandbox, pinned-root and cache-bypass models. These synthetic inputs are not production signatures, encryption or provider evidence.

`python3 /tmp/6-5b-loop1-audit.py` passed: 100 local paths/anchors, all four accepted disposition mappings, 19 uniquely anchored future vectors, 14 embedded local groups and **8,011 protected tracked-file hashes unchanged**. `git diff --check` passed. The worker edited only this candidate; the changed-file list also contains the pre-existing sprint-status edit and untracked execution record managed by the parent workflow. Runtime/tests, 6.5a, epic context, historical triage/design notes and the normative artifact/receipt retained their bytes. The unchanged AD-13 whole-file preservation checksum is `bcf6eee0b2d0795fa8a53b6d4e99ae0fd1ea79896acfca9927f5e448042f3d4a`; it is not an approval digest or approval evidence. No runtime build, production test, provider probe or deployment was performed. VB-01–VB-19, including provider protection, root-race and allocation measurements, remain future work; the AD-13 receipt is still `UNAPPROVED` and Story 6.6 is unauthorized.

Loop-2 verification performed on 2026-09-27: restored the sound candidate from `/tmp/bmad-6-5b-before-review-2.md`, applied the query-catalog/intake and narrow consistency corrections, extracted the sole Python block and ran `python3 /tmp/6-5b-loop2-checks.py`; exit 0, output `LB-01..LB-18 passed: codec, bounds, ownership, query/root/TTL and quota models; no provider proof`. LB-14 now rejects zero/mismatched capability revision, unsupported transcript/manifest codecs and a receiver instance differing from original admitted selection; it remains a negotiation model without signature verification. LB-15–18 add bounded query catalog/drift, pinned root/final TTL/authorization/read-set, completed recovery and shared quota decision models.

`python3 /tmp/6-5b-loop2-audit.py` passed: **110 local paths/anchors, all four accepted BH37 disposition mappings, 20 uniquely anchored future vectors, 18 local groups and 8,011 protected tracked-file hashes unchanged**. `python3 /tmp/6-5b-loop2-fixture-audit.py` passed: LB-01–LB-13 are byte-identical to the saved candidate and the exact original transition, transcript, zero-event, manifest and selector fixture assignments remain unchanged. `git diff --check` passed. Direct source/test inspection reconfirmed the wrapper/dispatcher/endpoint behavior and the previously recorded replay/snapshot test limitations. The worker changed only this candidate; pre-existing tracker/execution-record changes remain owned by the parent workflow. No runtime/tests, 6.5a, epic context, historical triage/design notes, signed fixtures or normative artifact/receipt changed. VB-01–VB-20 remain unexecuted production/provider obligations; no runtime build or provider claim substitutes for them. AD-13 remains `UNAPPROVED` and Story 6.6 remains unauthorized.

Targeted review corrections on 2026-09-27: bound input-row ValueTypeName/CLR/schema mappings through existing query options/closure; limited incremental named routes to one declared key space; made snapshot=target<head fall back without new prefix bytes; reconciled completed recovery with existing renewed owner-lease/token generations; included all retained named generations in the shared 64 GiB ceiling and export staging/pointers in the replay/export 1 GiB pool; enforced 1–64-byte query names and nonempty resolvable catalog identifiers. Added direct cases to existing VB-07/08/13/14/18/20 and LB-15–18, leaving original fixture assignments and LB-01–LB-13 intact. Focused validation: `python3 /tmp/6-5b-targeted-patch-checks.py` passed all embedded groups; `git diff --check -- _bmad-output/implementation-artifacts/spec-6-5b-verified-read-replay-and-projection.md` passed. Broader verification is left to the parent workflow. These checks remain bounded local models; runtime/provider vectors and the unapproved AD-13 posture are unchanged.
