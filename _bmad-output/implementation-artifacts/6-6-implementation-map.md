# Story 6.6 implementation map

This map began as an index for the approved AD-13 design. The owner's 2026-10-05 [Dapr-only amendment](story-6-6-dapr-only-amendment.md) now supersedes its direct PostgreSQL and provider-proof portions for Story 6.6. Paths below resolve from the EventStore repository root. Earlier normative sections remain inputs only where compatible with that amendment. Unqualified proof-dependent operations remain fenced.

## Authority and investigation

- Planning revision: `2242ad55a1b678828df8aa093fd92399c29af5bf`, clean `main` before workflow artifacts.
- Normative source: `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md`, approved content digest `bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050`. Its §12 records Jérôme Piquot's conversational approval. This invocation requests Story 6.6.
- `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py` passed at this revision: actual reviewed inputs, independent known answers, dispositions and current approval digest validate. The 20 O-rows and 47 implementation/evidence follow-ups remain open.
- `spec-6-5-event-versioning-and-upcasting-spec.md` and completed 6.5a–d execution records provide continuity. Historical statements that approval was pending do not override the current §12 record. Do not alter their pinned inputs or execution captures.
- `_bmad-output/planning-artifacts/architecture.md` AD-1/AD-13/AD-26 and the Dapr-only amendment now require application storage through Dapr APIs. `6-5-integration/metadata-adapter-contract.md` is historical evidence of the superseded SQL design. Actor event/snapshot/drain authority remains separate.
- None of the proposed authenticated-reader, upcaster, verified replay or projection interfaces currently exists in `src` or `tests`. Existing typed DTO reads cannot establish original-byte authority. This is substantial new runtime work across the current packages.
- Actual configuration pins SDK `10.0.401` and Microsoft.Testing.Platform; the older project-context SDK statement is stale. Use tracked configuration and the required baseline for build mode and test execution.

## M1 — Contracts, registry and bounded primitives

Normative scope: §§2–5, A2/A3/A4/A10, B2/B8 and §10.3.

Modify Contracts `Events/EventMetadata.cs`, `Events/ISerializedEventPayload.cs`, `Commands/DomainServiceRequest.cs`, `Results/DomainServiceWireResult.cs` and the relevant `Replay/`, `Streams/` and projection records. Move `DomainServiceWireEvent` into its own `Results/DomainServiceWireEvent.cs` when modifying it. Preserve existing positional constructors, deconstruction, defaults and enum values; use additive nullable init properties and member-specific null omission. Distinguish absent properties from explicit null during raw admission.

Reuse Client `Events/EventContractResolver.cs` and `Conventions/NamingConventionEngine.cs` grammar. Add the exact approved event descriptors, legacy aliases, immutable domain registry, bounded codecs/fingerprints and allow-listed deserializers under Client `Events/` and Contracts `Events/`. Add the named `IEventUpcaster`, `IV1Downserializer`, `IBoundedPayloadWriter`, `IBoundedScratchAllocator` and `IReadOnlyPayload` interfaces in single-type files. Validate contiguous unique chains and retained-source reachability within 16 hops; seal dependency closure and distinguish registry, event-transform and handler compatibility identities. Register an explicit domain-specific `AggregateTerminated` rejection adapter instead of inventing an event identity.

Tests: Contracts.Tests `Events/EventMetadataTests.cs`, `Results/DomainServiceWireResultTests.cs`, Client.Tests event resolver tests, new registry/codec/upcaster tests, and old-source/already-compiled consumer fixtures. Assert original bytes and known answers, registration-order independence, ambiguous aliases, chain topology, protection readability, mutation attempts and allocation refusal.

## M2 — Bounded V1 producer, actor evidence and shared reader

Normative scope: §§3/4/6/9/10, A3–A9, B2–B6.

DomainService `DomainServiceRequestRouter.cs` must replace both production calls to the unbounded `FromDomainResult` helper with capability-selected bounded writing. Retain that helper as the approved V1 compatibility seam. Server `DomainServices/DaprDomainServiceInvoker.cs` must stream response headers/body and validate raw ingress before allocation, deserialize incrementally, validate negotiation echoes and preserve the metadata triplet. Its private normal/rejection serialized carriers currently share that file; extract modified types into dedicated files.

Server `Events/EventPersister.cs`, `Events/EventEnvelope.cs` and `Actors/AggregateActor.cs` implement pre-reservation identity/size admission, deterministic preparation and same-save event/application-byte/result/outbox values. Reuse `InspectEventBatchSaveFailureAsync`, no-op terminal paths and bounded logical readback, while retaining the actor's exclusive `SaveStateAsync` authority. The existing independent recovery budget is 30 seconds; reread its current location before updating O-02 evidence. Do not issue or require a provider receipt.

Add a Dapr actor-owned logical event source and shared evolution adapter under Server `Events/`, with interfaces/views under Contracts/Client `Events/`. Integrate `IEventStreamReader.cs`, `EventStreamReader.cs`, `SnapshotManager.cs` and `SnapshotRecord.cs`. Preserve separate immutable stored application-byte, readable and effective views; validate addressed identity, application digest and complete prefix before domain code. A typed actor read is valid for logical history, while arbitrary type loading and unsupported provider-attestation claims remain forbidden.

Tests: Server.Tests `DomainServices/DaprDomainServiceInvokerTests.cs`, `Events/EventPersisterTests.cs`, `EventStreamReaderTests.cs`, `SnapshotManagerTests.cs`, `SnapshotRehydrationTests.cs`, actor infrastructure-failure tests, and DomainService.Tests router/admission tests. Include Dapr logical participant readback, commit-then-throw, no-op, empty prefix, protection, cancellation and allocation instrumentation. `EventPersister` still must not save actor state itself.

## M3 — Dapr control ownership and admission

Current scope: [Dapr-only amendment](story-6-6-dapr-only-amendment.md), AD-1/AD-13/AD-26, and actual hold-producer requirements. The former SQL schema, Npgsql dependency, provider receipt, SERIALIZABLE participant set and same-database claim are withdrawn.

Keep aggregate/event/snapshot/outbox and existing drain registration under `AggregateActor` and `IActorStateManager`. For each new control record, choose one Dapr owner: an actor with one `SaveStateAsync` boundary, or Dapr state APIs with ETag and transactional operations only after the component passes a live capability probe. Use stable keys, bounded values, explicit generations and owner fences, idempotent commands, durable intent and readback. Cross-actor, actor-to-state, broker and external-object work uses a recoverable sequence; do not describe it as one atomic transaction. No code may open a database connection, inspect Dapr private tables or require application PostgreSQL credentials.

Before any hold producer is enabled, test the exact Dapr component and topology for concurrent owners, lost acknowledgment, restart, cancellation and capability refusal. If a control's required atomic participant set cannot fit one supported Dapr transaction/actor save, keep that control and its dependent operation unavailable until it is redesigned. Provider-specific SQL tests are historical only; new product tests use the Dapr API and inspect logical readback.

## M4 — Paged replay, processing, snapshots and query intake

Normative scope: B3–B9, §§6/9, D5 replay activation.

Add approved records/interfaces under Contracts `Replay/`, including `PagedContext`, `PagedProgress`, `IPagedReplayStateSession` and transcript selection. Add Client `Aggregates/IAsyncAggregateReplay.cs`, `Handlers/IAsyncDomainProcessor.cs` and private charged replay/session implementations. Prefer them in DomainService `DomainServiceRequestRouter.cs` and `EventStoreDomainServiceExtensions.cs`; register them through Client `Registration/EventStoreServiceCollectionExtensions.cs`. Legacy calls stay explicitly bounded zero-hop adapters with cancellation checks before/after calls and between events.

Route Server `DomainServices/DaprAggregateStateReconstructor.cs`, `Actors/AggregateActor.cs` and both source/target reads in `Actors/CoordinatedCommandActor.cs` through verified fixed-head pages. Select replay capability independently of command processor registration. Validate command-state proof before admission. Maintain private pre-Apply last-good state, atomic successor/page transitions, pinned retry responses and exact terminal readback; emit no partial state/timeline from `InProgress`. Invalid snapshot evidence restarts at page one; timeline always starts at one. Empty operations still commit the prescribed count-zero final page, and terminal sequence arithmetic cannot overflow.

DomainService `DomainQueryDispatcher.cs`, Server `Queries/QueryRouter.cs`, `Actors/CachingProjectionActor.cs`, and Client read-model stores need authenticated query catalog, dispatcher-installed pinned prior session, logical-key/type authorization and final root/TTL fence. Versioned query execution bypasses the cache before lookup and stores zero result/proof entries. Keep ordinary non-versioned cache/store behavior.

Tests: Client.Tests replay/aggregate/apply tests, Server.Tests reconstructor/query/cache tests, DomainService.Tests endpoint tests, and new private page/session tests. Assert folded state hashes, pre-failure state, exact token forwarding, zero-hop compatibility, empty/terminal pages, root drift, expired/disposed sessions and foreign-scope access refusal.

## M5 — Projections and named state visibility

Normative scope: B7/B8/B9, §§6/9/10.

Server `Projections/ProjectionUpdateOrchestrator.cs`, `ProjectionEventWireBuilder.cs`, `ProjectionDeliveryRetryWorker.cs`, `EventStoreProjectionDeliveryHistoryReader.cs`, `ProjectionStreamPageValidation.cs` and `NamedProjectionDispatchCoordinator.cs` must share source/effective validation before fingerprints or handler selection. Read the checkpoint before history; a proven current projection does zero reads, handler calls and writes. Full replay never treats a tail as complete input.

Add `IVerifiedDomainProjectionHandler`, verified request/prior/capability interfaces and records in their named Client/Contracts files. Wire DomainService `DomainProjectionDispatcher.cs`, `DomainProjectionServiceCollectionExtensions.cs` and `DomainSharedProjectionRebuildDispatcher.cs`. Versioned paths require an application generation, Dapr logical bundle readback and one pinned prior session; legacy handlers receive only byte-identical verified zero-hop input. No provider-certified generation is available.

Extend Client `Projections/DaprReadModelStore.cs`, `ReadModelBatchProtocol.cs`, `SharedProjectionEpochCoordinator.cs` and named-store interfaces with the prescribed versioned key-space guard and fenced writer. Inventory/copy/readback old physical keys before enabling that guard; it cannot block a serving legacy coordinator prematurely. Preserve existing non-versioned batch visibility and rebuild completion behavior.

Tests: matching Server.Tests `Projections/`, DomainService.Tests projection/rebuild suites, Client.Tests read-model/batch/epoch suites and Server.LiveSidecar.Tests `Integration/NamedProjectionDispatchLiveSidecarTests.cs`. Assert complete persisted model/checkpoint/generation equality, current-path zero work, last-good state, cancellation and crash/retry idempotency.

## M6 — Publication, subscription and effect receipts

Normative scope: C1–C6, A8, D3/D4/D6/D7, §§7/9.

Server `Events/EventPublisher.cs` and `IEventPublisher.cs` need exact pinned Binary/Structured carriers, authenticated membership/acceptance observations, byte-preserving timestamps, whole-batch reservation and per-topic send authority. Actor outbox/drain paths keep actor mutation ownership and original MessageIds. Command first-response preparation is immutable; authoritative D4 status precedence supersedes scalar inference only at its activation slice.

Client `Subscriptions/EventStoreDomainEventProcessor.cs`, envelope/context DTOs, marker store implementations/interfaces and `Registration/EventStoreDomainEventsServiceCollectionExtensions.cs` must verify transport/root/route/effective view before marker acquisition or handler/type selection. Implement stable `EventEffectKey` identity and idempotent effect handling using Dapr-backed state/actor boundaries and logical readback. Separate effect stores require their own explicit idempotency contract. Existing AD-26 trusted-effect identity/receipt records are different contracts, not substitutes. DomainService `EventStoreDomainEventsEndpointExtensions.cs` acknowledges only the demonstrated durable outcome.

Tests: current publisher/actor publication tests, Client subscription/marker tests, DomainService endpoint tests and live effect/marker tests. Add real carrier, multi-route, same-MessageId retry, partial broker acceptance, wrong membership, lost acknowledgement and atomic effect/receipt/readback evidence. Preserve duplicate/fingerprint identity based on original evidence, never effective adapted bytes.

## M7 — Operator resume, capture, holds and diagnostics

Normative scope: D2–D8, B2a/B9, §§7.4/8.1/10.2/10.3.

Add the declared Admin resume/precondition, held-delivery redrive and tenant/deployment hold-inventory controllers under the gateway's `src/Hexalith.EventStore/Controllers/`, backed by the single metadata owner and exposed through Admin.Server typed services. Implement purpose-2d authorization/audit, exact eligibility/window fences, unresolved-only sends, capsule-before-cleanup legacy recovery, scoped cursor and per-item evidence incidents. Re-arm the original committed batch; never execute its command again. Reuse `AggregateActor.DrainUnpublishedEventsAsync`, drain registration/retry and existing lost-save inspection without moving actor ownership. Gateway `Controllers/ReplayController.cs` enforces the committed-publication replay denial only after the resume safety route exists.

Operations `Capture/DeadLetterEnvelopeParser.cs`, `Endpoints/DeadLetterOperationsEndpointExtensions.cs`, `Actors/DeadLetterDrainActor.cs`, `Replay/DaprDeadLetterReplayTransport.cs` and telemetry use Dapr-accessible logical capture, retention/redrive and separate physical custody/route completion. Operations requiring physical raw-envelope capture remain disabled. Change ack-on-oversize/conflict/unretainable behavior only after its Dapr-backed safety path is qualified. Reuse the scoped operator authorization boundary; a fixture token is never production authorization.

Gateway `Controllers/StreamsController.cs`, `AdminStreamQueryController.cs`, `AdminTraceQueryController.cs`; Admin.Server `Services/DaprBackupCommandService.cs`, `DaprTypeCatalogService.cs` and command query/filter services expose safe verified provenance and authoritative outcomes. Export is complete/fixed-head/private until final pointer readback. Sandbox uses isolated diagnostic simulation; synthetic events never enter persisted proof paths. Add stable version/hop/reason fields in Admin.Abstractions models and existing Admin.UI pages/components with Fluent UI V5/FrontComposer conventions. Audit all shipped command/status/polling/filter surfaces for BC-01 through BC-16, including CLI and SDK types, using the approved field/nullability matrix.

Tests: gateway controller/sandbox/protection-leak suites; Admin.Server resume/authorization/inventory/export/status/filter tests; Operations capture/drain/replay tests; Admin.UI rendered contract tests. Assert tenant isolation, payload redaction, original MessageIds, current authoritative filters, refusal with unchanged persisted bytes and no ambiguous success acknowledgement.

## M8 — Migration, automatic gates and final evidence

Normative scope: §10's four ordered slices, BC table, A10/B10/C7, §§11.4–11.7 and O-01 through O-20.

Add `scripts/verify-event-evolution.py`, meaningful Contracts.Tests `Events/EventEvolutionEvidenceTests.cs` and fixture vectors, and dedicated Server.LiveSidecar.Tests Dapr evolution scenarios. Keep earlier pinned A/B/C/D known-answer checks as historical validation; add independent current-amendment checks rather than repinning the prior approval. Mutation runs require per-mutation timeouts. Do not fabricate completed rows or claim provider-attested results from logical readback.

Reuse `scripts/validate-consumer-package-references.py`, `tools/release-packages.json`, current `.github/workflows/release.yml` and package/consumer test lanes. Add API/wire baseline and already-compiled/package-only consumer fixtures for every compatibility claim. No alternate release lane/version override; publish the complete approved incompatible set only in SemVer-major. Audit documentation/OpenAPI/XML/UI terminality again at the implementation revision. Amend material public behavior only with owner review.

Record evidence under `_bmad-output/implementation-artifacts/evidence/story-6-6/`: canonical source/profile identities, exact commands/results, persisted bytes/end-state, injected failures, carrier evidence, API/wire baselines, affected project regressions, and closure for each O-row/follow-up. Preserve historical evidence. Update only this story's spec/tracker and owned follow-ups after their actual gates pass.

Current slice order: (1) additive contracts, bounded V1 writer and actor-owned application-byte evidence; (2) shared allow-listed registry/upcaster and qualified Dapr logical readback/control capabilities; (3) all consumer, replay, projection, subscription and inspection paths through that service with compatibility and crash/retry tests; (4) complete fleet/broker/Dapr/compatibility qualification before V2 writes. A retained V1 row must have an unambiguous allow-listed mapping and readable logical payload; absent mapping or unreadable data blocks the affected read. Immutable application history is not rewritten. Operations still requiring the superseded provider proof stay disabled.

`deploy/dapr/production-profile.yaml` is currently absent and AD-26 unratified. Runtime preparation can proceed; actual production qualification/activation remains fenced pending the separately approved exact profile and proof. Redis/development tests cannot be labeled production evidence. The configured production component and a second supported component/harness need applicable Dapr API probes. If tooling or external authority blocks a required gate, keep the story incomplete and report the exact blocker separately from passing focused evidence.

## Verification execution

Before runtime edits, follow the baseline Aspire workflow to run/inspect the AppHost. Restart after AppHost changes and close owned processes/ports at verification end. Reuse local assets only after restoring the selected dependency mode.

- Preflight: `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py --mutations`; then approved child bounded/regression scripts and `python3 scripts/check-deferred-work.py --json` when their inputs are affected.
- Local builds: individual affected projects with `--configuration Debug -p:UseHexalithProjectReferences=true -m:1`; tests individually, using built xUnit assemblies with `-class`/`-method` for focused selection when needed.
- Final story build: `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false`; package/API/consumer and affected regression lanes, including live sidecars, must pass without unexpected skips.
- For environment failures, follow the baseline fallback ladder, retaining the exact broad command/result and focused evidence separately. Never weaken a gate to hide a failure.
- Planning artifacts: check frontmatter/story key, actionable task and acceptance coverage, approved digest, LF and `git diff --check`. No runtime test claim follows from these document checks.
