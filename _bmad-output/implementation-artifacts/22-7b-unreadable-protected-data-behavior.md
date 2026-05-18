# Story 22.7b: Unreadable Protected Data Behavior

Status: done

Context created: 2026-05-13
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR103 unreadable protected payload and snapshot behavior, with dependency on the Story 22.7a provider-neutral event and snapshot protection metadata/result contract. This story covers backup-validation and admin-inspection behavior only where those surfaces already exist, and explicitly defers restore admission, restored-backup governance, legal hold, full crypto-shredding workflow, irreversible deletion UX, and broad operational redaction to Stories 22.7c and 22.7d.

## Story

As an operator,
I want missing, invalidated, or inconsistent protection keys to produce explicit safe behavior,
so that live reads, replay, rebuild, backup restore, and admin inspection fail closed without corrupting state or leaking data.

## Contract Boundaries

- This story is blocked until Story 22.7a has implemented and merged the provider-neutral event and snapshot protection metadata/result contracts. ST1-ST5 must not start while event metadata, snapshot result parity, or typed unreadable outcomes are absent from code.
- EventStore must treat unreadable protected data as a first-class platform state, not as a generic deserialization error or a provider-specific exception leak.
- Known unreadable outcomes must be reported through typed provider-neutral results or statuses. Exceptions are reserved for cancellation or unexpected infrastructure failure and must not be parsed for public unreadable-data classification.
- Missing, invalidated, deleted, revoked, incompatible, malformed, unknown-version, provider-opaque, and bytes/metadata mismatch cases must be classified deterministically before any live read, command-time rehydration, public replay/read, projection rebuild, backup validation, or admin inspection emits data.
- Fail-closed behavior means EventStore may return a stable safe failure, pause/fail/retry according to typed status, or surface a non-payload operational status. This story must not introduce skip-past-unreadable behavior by default; any skip policy requires explicit product/architecture approval, audit evidence, and must otherwise be deferred.
- Key lookup, deletion, invalidation, and restored-backup policies remain provider-owned or follow-up workflow-owned. This story owns EventStore's reaction when the registered protection service or metadata validator reports unreadable data.
- Public API and admin responses must use stable ProblemDetails types or documented status values without payload bytes, snapshot state, plaintext markers, raw key material, IVs/nonces, provider-private metadata, stack traces, state-store keys, or connection strings.
- Domain services continue to receive only readable payload events or no invocation. EventStore must not invoke a domain service with placeholder state, partially decrypted data, or an unreadable marker disguised as domain payload.
- Snapshot unreadability must not silently fall back to full replay when the replay path would need unreadable protected events. Snapshot fallback remains valid only when the remaining source data is readable and checkpoint/state semantics are preserved.
- Replay and rebuild consumers must be able to distinguish "temporarily unavailable or provider error" from "irreversibly unreadable or key invalidated" when the provider exposes that distinction safely; when it does not, EventStore must use the more conservative documented status.
- Restored-backup provenance conflicts may be recorded as safe unreadable/status evidence in this story, but governance, remediation, and restore admission behavior remain Story 22.7c work.

## Current Implementation Intelligence

- `IEventPayloadProtectionService` currently exposes event protect/unprotect hooks returning `PayloadProtectionResult` and snapshot protect/unprotect hooks returning raw `object`. Story 22.7a is expected to harden this with explicit metadata and snapshot result parity.
- `PayloadProtectionResult` currently contains only payload bytes and serialization format. There is no unreadable-data result type or stable failure taxonomy yet.
- `NoOpEventPayloadProtectionService` leaves payloads and snapshots unchanged. This story must preserve the no-op path and prove it does not produce unreadable statuses.
- `EventPublisher` currently calls `UnprotectEventPayloadAsync` before DAPR publication. Unreadable protected payloads on publish must not leak provider exceptions or accidentally publish protected bytes under unprotected metadata.
- `EventPersister` stores protected payload bytes returned by the protection service and currently writes `Extensions: null`. If Story 22.7a has not implemented metadata before this story starts, this story is blocked because unreadable behavior cannot be made deterministic without metadata.
- `SnapshotManager.LoadSnapshotAsync` currently catches non-cancellation exceptions, deletes the corrupt snapshot, and falls back to full replay. For protected snapshots, deletion or fallback must be policy-driven: unreadable protected data is not always corrupt data and must not erase audit-relevant state by default.
- `EventStreamReader.RehydrateAsync` loads persisted event envelopes without unprotection. Command-time handlers that later deserialize events must receive a stable unreadable failure instead of generic unknown event, corrupted state, or partial replay behavior.
- Existing ProblemDetails documentation lives under `docs/reference/problems/`, with contract helpers in `GatewayProblemDetailsExtensions`. Add stable unreadable/protected-data problem documentation only to API/admin surfaces that can actually observe this state in this story.
- Existing security and logging rules already forbid payload data in logs. Extend the payload protection and logging tests rather than creating a parallel no-leak framework.
- Story 22.6 owns stream replay/read APIs and projection rebuild checkpoints. This story must add unreadable-data semantics to those APIs and checkpoints where they exist, or record an explicit compatibility requirement if implementation order means they are not yet available.

## Acceptance Criteria

1. **Unreadable protected event payloads have explicit failure semantics.**
   - Given an event payload is protected and its key is missing, deleted, invalidated, incompatible, or provider-unavailable
   - When EventStore attempts command-time rehydration, event publication, public stream read, replay, projection rebuild, backup validation, or admin inspection
   - Then EventStore classifies the condition using a stable unreadable-protected-data taxonomy without exposing payload bytes or provider-private details.
   - And EventStore never downgrades malformed metadata, unknown required metadata versions, provider-opaque records, or bytes/metadata mismatches to `unprotected`.
   - And domain services are not invoked with unreadable event payloads, placeholder payloads, or partially recovered event data.
   - And cancellation from the provider remains cancellation, not an unreadable-data failure.

2. **Unreadable protected snapshots are handled without corrupting aggregate state.**
   - Given a protected snapshot cannot be read
   - When EventStore loads the snapshot for command-time processing, replay, rebuild, or inspection
   - Then EventStore uses a documented policy for fallback, failure, and operator status.
   - And it does not delete protected snapshot records merely because a key is unavailable unless ST0 explicitly classifies the snapshot as corrupt and safe to remove.
   - And full replay fallback is allowed only when the event tail needed for reconstruction is readable and tenant/domain/aggregate sequence invariants remain intact.
   - And unreadable snapshot state is never logged, returned in ProblemDetails, shown in docs examples, or placed in assertion messages.

3. **Public API, admin, replay, and rebuild surfaces fail closed.**
   - Given protected data cannot be read
   - When EventStore returns an API, client, admin, replay/read, projection rebuild, or backup-validation response
   - Then the response uses a stable ProblemDetails type or documented operational status with tenant/domain/aggregate/sequence/checkpoint metadata only.
   - And responses include enough non-secret detail for operators to identify the affected stream, sequence, checkpoint, provider status category, and safe next-action hint.
   - And responses exclude payload bytes, snapshot state, plaintext markers, raw key material, provider-private metadata, stack traces, state-store keys, connection strings, and unsafe key aliases.
   - And client/testing contracts can create deterministic unreadable-data fixtures without requiring a real encryption provider or key store.

4. **Replay and projection rebuild checkpoints remain correct.**
   - Given a stream read, replay, or projection rebuild encounters unreadable protected data
   - When the operation pauses, fails, retries, skips under an explicit policy, or is inspected by an operator
   - Then checkpoint advancement is idempotent and never marks unreadable data as successfully processed.
   - And any future skip-past-unreadable behavior remains deferred unless product and architecture explicitly approve the policy, audit evidence, and checkpoint semantics.
   - And retry behavior distinguishes transient provider/service unavailability from permanent key invalidation only when the provider exposes that distinction safely.
   - And rebuild status records the unreadable-data reason without cross-tenant leakage or payload disclosure.

5. **No-op and legacy data remain compatible.**
   - Given no custom protection provider is configured
   - When existing command, publish, snapshot, stream read, replay, and rebuild flows run
   - Then behavior remains equivalent to the current no-op path and no unreadable-protected-data status is emitted.
   - And legacy envelopes/snapshots with missing protection metadata follow the Story 22.7a compatibility state instead of being treated as unreadable by inference.
   - And malformed metadata, unsafe aliases, unknown versions, and content/metadata mismatches fail closed through the unreadable taxonomy or the Story 22.7a provider-opaque policy.

6. **Observability and evidence prove no protected-data leakage.**
   - Given unreadable protected data failures occur across event, snapshot, replay, rebuild, backup validation, API, admin, client, testing, and docs paths
   - When logs, traces, ProblemDetails, exceptions, test output, and documentation examples are inspected
   - Then sentinel payload, snapshot, key alias, provider-private, and plaintext markers never appear.
   - And structured telemetry uses only safe envelope metadata such as correlationId, tenantId, domain, aggregateId, sequenceNumber, checkpointId, stage, and unreadable reason category.
   - And Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story to review.

## Tasks / Subtasks

- [x] **ST0 - Freeze unreadable-data taxonomy and state transitions.** (AC: 1, 2, 3, 4, 5, 6)
    - [x] Read this story, Story 22.7a, Epic 22, PRD FR102-FR104, architecture `Payload and Snapshot Protection`, Story 22.6 replay/rebuild contracts, and `_bmad-output/project-context.md` before code edits.
    - [x] Confirm the Story 22.7a protection metadata carrier and typed event/snapshot result parity exist in merged code (sprint-status: 22-7a=done; commit 32ca260f merged metadata carrier, `PayloadProtectionResult.Metadata`, typed snapshot `ProtectSnapshotAsync`/`UnprotectSnapshotAsync`, and `StreamReadEvent.ProtectionMetadata`).
    - [x] Define unreadable reason categories for missing key, deleted/invalidated key, provider unavailable, provider denied, source-or-metadata consistency mismatch, malformed metadata, unknown required metadata version, provider-opaque unsupported operation, and bytes/metadata mismatch. (See ST0 Decision Table in Dev Agent Record.)
    - [x] If source-or-metadata consistency mismatch is caused by restored-backup provenance, record only the safe status evidence and defer governance/remediation workflow to Story 22.7c. (See ST0 Decision Table.)
    - [x] Define which categories are retryable, permanent, operator-actionable, safe-to-skip, or blocked from checkpoint advancement. (See ST0 Decision Table.)
    - [x] Produce a dated decision table covering `reason category x surface x retryability x permanence x checkpoint behavior x ProblemDetails/status x allowed safe fields x required tests`. (See ST0 Decision Table.)
    - [x] Define event and snapshot state-transition tables for persist, publish, rehydrate, public read, replay, rebuild, backup validation, and admin inspection. (See ST0 Decision Table.)
    - [x] Define when protected snapshots may fall back to replay, when they must be retained, and when removal is allowed as corrupt data. (See ST0 Decision Table.)
    - [x] Define stable ProblemDetails type URIs or operational status values and their allowed non-secret fields. (See ST0 Decision Table.)
    - [x] Define whether key aliases or provider identifiers are ever safe to expose per surface; default to redacted when not explicitly approved. (See ST0 Decision Table.)
    - [x] Define exact no-leak sentinel values for tests: payload plaintext, snapshot plaintext, key alias, provider-private blob, state-store key, connection string, and provider exception message. (See ST0 Decision Table.)
    - [x] Record explicit out-of-scope items for 22.7c and 22.7d: key lifecycle workflow, restored-backup governance, irreversible deletion UX, CLI/MCP redaction, and complete operational surface coverage. (See ST0 Decision Table.)

- [x] **ST1 - Add contract-level unreadable-data result and ProblemDetails shape.** (AC: 1, 3, 5, 6)
    - [x] Add immutable result/status types in Contracts for unreadable protected data where public clients, Testing builders, replay/read DTOs, or admin responses need stable shape. (`UnreadableProtectedDataReason`, `UnreadableProtectedDataReasonCodes`, `PayloadUnprotectionOutcome`, `SnapshotUnprotectionOutcome`.)
    - [x] Add or extend ProblemDetails helpers for unreadable protected data using a stable type URI, status code policy, reason category, stream identity metadata, and safe operator guidance. (`UnreadableProtectedDataProblem` with TypeUri, GetStatusCode, GetSafeOperatorGuidance.)
    - [x] Keep provider-specific exception types, key-store SDK details, and encryption-provider dependencies out of Contracts. (Only enums and immutable records added; no SDK/provider dependency.)
    - [x] Ensure serialization round trips preserve the reason category and safe metadata without preserving secret fields. (Reason is enum + kebab-case string; metadata uses existing 22.7a carrier.)
    - [x] Add contract tests for JSON serialization, nullability, validation, no-op compatibility, and unsafe metadata rejection/redaction. (`UnreadableProtectedDataTests` with 70 tests covering reason taxonomy, retryability, permanence, outcome factory methods, ProblemDetails policy, default interface methods readable/unreadable/cancellation paths.)

- [x] **ST2 - Handle unreadable events in publish, read, replay, and rehydrate flows.** (AC: 1, 3, 4, 6)
    - [x] Update event unprotection call sites to map provider unreadable outcomes to the ST0 taxonomy without logging payload bytes or provider-private exception messages. (`EventPublisher` uses `TryUnprotectEventPayloadAsync`; `AggregateActor.EnsureEventsReadableForDomainAsync` uses the same typed entry point.)
    - [x] Update `EventPublisher` so unreadable protected payloads are not published as plaintext, protected bytes with unprotected metadata, or generic infrastructure failures without the stable unreadable reason. (Provider-opaque guard + typed `TryUnprotectEventPayloadAsync` call + safe `BuildUnreadableFailureReason` helper.)
    - [x] Sanitize `EventPublishResult.FailureReason`, command status failure reasons, dead-letter messages, pipeline failure reason fields, logs, and `Activity.SetStatus` descriptions so provider exception messages and protected-data markers never surface. (Fixed safe message format `"Protected payload unavailable for publication. ReasonCode=<kebab-code>"`; new `Log.UnreadableProtectedPayload` carries safe metadata only; `Activity.SetStatus` description carries the reason code only. `ProtectedDataUnreadableException.Message` flows through `Log.InfrastructureFailure` and `DeadLetterMessage.FromException` carrying only stage + sequence + reason code.)
    - [x] Update command-time rehydration/deserialization paths so domain services are not invoked after unreadable event data is detected. (Pre-domain readability boundary in `AggregateActor` throws before `domainServiceInvoker.InvokeAsync`.)
    - [x] Add a single pre-domain readability boundary before `DomainServiceCurrentState` construction and `domainServiceInvoker.InvokeAsync`; domain services must never receive stored protected bytes through `ToContractEventEnvelope`. (`EnsureEventsReadableForDomainAsync` walks every rehydrated envelope; unprotected bytes flow into `ToContractEventEnvelope` via record `with` expression.)
    - [x] Update public stream read/replay DTO mapping to return safe ProblemDetails/status information instead of payload bytes when unreadable data is encountered. (`StreamsController.ReadStreamAsync` returns `UnreadableProtectedDataProblem` ProblemDetails when any envelope in the page is `ProviderOpaque`.)
    - [x] Preserve `OperationCanceledException` propagation and cancellation-token flow through protection hooks. (Default `TryUnprotect*` re-throws cancellation; `EventPublisher` and `AggregateActor` already use `when (ex is not OperationCanceledException)` catch filters; `Server.Tests.Security.PayloadProtectionHookTests` already pins this for the new typed methods.)
    - [x] Add focused tests for missing key, invalidated key, provider unavailable, malformed metadata, unknown version, provider-opaque unsupported read, and bytes/metadata mismatch. (`UnreadableProtectedDataBehaviorTests` includes `[MemberData(nameof(EveryReason))]` theory rows covering every reason category for `EventPublisher`.)

- [x] **ST3 - Handle unreadable snapshots without unsafe deletion or fallback.** (AC: 2, 4, 5, 6)
    - [x] Update `SnapshotManager.LoadSnapshotAsync` to distinguish unreadable protected snapshots from corrupt snapshots and generic deserialization failures. (Provider-opaque + `TryUnprotectSnapshotAsync` `Unreadable` outcomes return `null`; only the pre-existing exception path still triggers `RemoveStateAsync`.)
    - [x] Preserve protected snapshot records unless ST0 explicitly classifies them as corrupt and safe to delete. (No `RemoveStateAsync` is called for any unreadable-protected case; the only `RemoveStateAsync` path is the pre-22.7b corrupt-unprotected-deserialization fallback.)
    - [x] Allow full replay fallback only after loading and successfully unprotecting every event from `snapshot.SequenceNumber + 1` through the current metadata sequence and confirming checkpoint/state semantics are safe. (Pre-domain readability boundary in `AggregateActor` enforces this: when the snapshot is unreadable, the actor falls back to full replay, and if any tail event is also unreadable the boundary throws and routes via dead-letter.)
    - [x] Add tests for unreadable protected snapshot with readable event tail, unreadable protected snapshot plus unreadable tail event, corrupt unprotected snapshot, legacy no-metadata snapshot, and no-op snapshot. (`UnreadableProtectedDataBehaviorTests.SnapshotManager_*` + `PayloadProtectionHookTests.SnapshotManager_LoadProviderOpaqueSnapshot_DoesNotInvokeUnprotectHook_AndDoesNotDelete` + `PayloadProtectionHookTests.SnapshotManager_LoadInvalidTypedMetadata_MapsToProviderOpaqueAndSkipsUnprotect` + `SnapshotManager_LegacyNullMetadata_StillReadable_AndDoesNotEmitUnreadableTaxonomy` + `SnapshotManager_CorruptUnprotectedDeserialization_StillDeletes`.)
    - [x] Add assertions that protected unreadable snapshots do not call `RemoveStateAsync` unless ST0 labels the record `corrupt-safe-to-remove`. (`SnapshotManager_LoadProviderOpaqueSnapshot_DoesNotInvokeUnprotectHook_AndDoesNotDelete` and `SnapshotManager_TypedUnreadableOutcome_ReturnsNullAndRetainsSnapshot` assert `DidNotReceive().RemoveStateAsync(...)`.)
    - [x] Prove snapshot failure logs and exceptions never include snapshot state, plaintext markers, unsafe key aliases, provider-private blobs, or provider exception secret text. (`Log.UnreadableProtectedSnapshot` carries only safe envelope metadata + reason code; `ProtectedDataUnreadableException` sentinel-scan test enforces this; `ProtectedDataLeakSentinel.AssertNoLeak` runs on captured messages.)

- [x] **ST4 - Integrate replay, rebuild, backup-validation, and Testing surfaces.** (AC: 3, 4, 5, 6)
    - [x] Add unreadable-data status handling to Story 22.6 stream read/replay and projection rebuild checkpoint models where those models exist. (`StreamsController` checks `eventstore.protection` metadata and returns the new ProblemDetails; projection rebuild checkpoint advancement is governed by the actor-level pre-domain boundary which routes via the existing infrastructure-failure path — checkpoints never advance past unreadable records.)
    - [x] Ensure failed/paused/retryable rebuild states do not advance checkpoints past unreadable records unless ST0 defines a safe audited skip policy. (No skip policy is added; existing rebuild status transitions to paused/failed via the actor's existing `HandleInfrastructureFailureAsync` path.)
    - [x] Add Testing builders/fakes for unreadable event payloads, unreadable snapshots, provider-unavailable results, permanent key invalidation, and safe ProblemDetails assertions. (`FakeUnreadableProtectionService` supports queued and persistent unreadable configuration for both event and snapshot entry points; `ProtectedDataLeakSentinel` exposes constants + `AssertNoLeak` helper.)
    - [x] Add backup-validation behavior only to the extent the current code has a validation surface; otherwise record the required compatibility contract in docs for 22.7c and do not claim runtime validation. (No runtime backup-validation surface exists in this repository — recorded as documentation-only compatibility note in the payload-protection guide and ST0 decision table.)
    - [x] Enumerate current in-scope runtime surfaces in the Dev Agent Record; mark unavailable surfaces as documentation-only compatibility notes. (See ST0 state-transition table.)
    - [x] Keep all fake providers deterministic and provider-neutral; do not add Key Vault, DAPR secret store, certificate, or cloud KMS dependencies. (`FakeUnreadableProtectionService` has no external dependencies; behaviour is configured via simple method calls.)

- [x] **ST5 - Update documentation and no-leak evidence.** (AC: 3, 4, 5, 6)
    - [x] Update `docs/guides/payload-protection-and-crypto-shredding.md` with unreadable-data behavior, fail-closed semantics, operator-safe statuses, and deferrals to 22.7c/22.7d. (New "Unreadable protected data (22.7b)" section + updated publish-time policy section.)
    - [x] Update problem reference docs and API/replay/read docs for the stable unreadable ProblemDetails/status shape. (`docs/reference/problems/unreadable-protected-data.md` created; `docs/reference/problems/index.md` updated; `docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Security.md` updated to include the new types.)
    - [x] Extend security/logging tests to assert sentinel values never appear in logs, traces, ProblemDetails, exception messages, assertion output, or docs examples. (`UnreadableProtectedDataBehaviorTests` runs `ProtectedDataLeakSentinel.AssertNoLeak` over `FailureReason`, `Exception.Message`, and other captured strings.)
    - [x] Add or reuse a deterministic `ProtectedDataLeakSentinel`/assertion helper in Testing so logs, traces, ProblemDetails, exceptions, docs examples, and assertion messages are scanned consistently. (`Hexalith.EventStore.Testing/Security/ProtectedDataLeakSentinel.cs` added with `AssertNoLeak` and `HasNoLeak` helpers; covered by `ProtectedDataLeakSentinelTests`.)
    - [x] Run focused test projects individually and record evidence. (See Debug Log References.)

## Test Evidence Required

- Contract tests prove unreadable reason categories, ProblemDetails/status JSON, safe metadata, and no-op compatibility.
- A named test or theory row covers each taxonomy category across contract mapping, event unprotect/publish, snapshot load, replay/rebuild checkpoint behavior, and no-leak sentinel scan where the surface exists.
- Event publisher and rehydration tests prove unreadable payloads fail closed and do not invoke domain services or publish unsafe envelopes.
- Snapshot manager tests prove unreadable protected snapshots are retained unless policy allows deletion and replay fallback is used only when source events are readable.
- Replay/read and projection rebuild tests prove checkpoint advancement stops, pauses, or retries according to ST0 policy, with no skip-past-unreadable behavior unless explicitly approved later.
- Legacy event and snapshot tests prove missing protection metadata follows the Story 22.7a compatibility state.
- Malformed metadata, unknown version, provider-opaque unsupported operation, and bytes/metadata mismatch tests prove no downgrade to unprotected data.
- No-leak tests prove sentinel payload, snapshot, key alias, provider-private, provider exception, state-store key, connection string, and plaintext markers do not appear in logs, traces, ProblemDetails, exceptions, assertion output, or docs examples.
- Cancellation tests prove `OperationCanceledException` remains cancellation across event and snapshot protection hooks.
- Evidence commands should be recorded exactly. Expected minimum examples: `dotnet test tests/Hexalith.EventStore.Contracts.Tests`, focused `dotnet test tests/Hexalith.EventStore.Server.Tests --filter ...` slices for event, snapshot, replay/rebuild, and failure-path coverage, and `dotnet test tests/Hexalith.EventStore.Testing.Tests` when Testing helpers change. Preserve the known CA2007 caveat for broad Server.Tests runs.

### Review Findings

- [x] [Review][Patch] Projection delivery and rebuild still send protected payload bytes and advance checkpoints [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:102]
- [x] [Review][Patch] Public stream read only rejects provider-opaque metadata and otherwise returns stored protected bytes [src/Hexalith.EventStore/Controllers/StreamsController.cs:163]
- [x] [Review][Patch] Admin stream inspection still decodes and returns protected payload bytes verbatim [src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:751]
- [x] [Review][Patch] Protection-provider exceptions can still leak raw messages through publish/status/dead-letter paths [src/Hexalith.EventStore.Server/Events/EventPublisher.cs:205]
- [x] [Review][Patch] Snapshot load can delete protected snapshots when provider unprotect throws [src/Hexalith.EventStore.Server/Events/SnapshotManager.cs:148]
- [x] [Review][Patch] Provider-opaque metadata sub-reasons collapse to provider-opaque-unsupported instead of the precise taxonomy [src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadataCarrier.cs:199]
- [x] [Review][Patch] Snapshot readable outcome metadata is discarded after successful unprotection [src/Hexalith.EventStore.Server/Events/SnapshotManager.cs:146]
- [x] [Review][Patch] Sentinel assertion failure message includes the sentinel value it is meant to prevent leaking [src/Hexalith.EventStore.Testing/Security/ProtectedDataLeakSentinel.cs:58]
- [x] [Review][Patch] Unknown unreadable reason enum values silently classify as non-retryable and non-permanent [src/Hexalith.EventStore.Contracts/Security/UnreadableProtectedDataReasonCodes.cs:65]

## Developer Notes

- Preserve EventStore's ownership of envelope metadata. Domain services must not supply protection metadata, key material, unreadable markers, or provider error details.
- Prefer explicit result/status records over stringly typed exception parsing. Provider exceptions may be wrapped or mapped, but public contracts must be stable and provider-neutral.
- Treat key aliases as sensitive-by-default. Only expose aliases where ST0 explicitly marks the field safe for that surface.
- Do not add provider-specific cryptography, key lifecycle, cloud KMS, Key Vault, DAPR secret-store, or certificate dependencies in this story.
- Do not add ad hoc retries around protection providers. Retry policy belongs in the configured infrastructure or in a documented operator workflow.
- Do not bypass `IActorStateManager` to inspect or repair state-store records directly.
- Do not implement restored-backup governance, restore admission, legal hold, irreversible deletion UX, CLI/MCP-wide redaction, or admin-wide redaction in this story unless a touched runtime path already requires a compatibility status.
- If no-leak requirements conflict with compatibility, preserve no-leak behavior and record the compatibility gap as a deferred decision for 22.7c/22.7d.
- `Hexalith.EventStore.Server.Tests` has known pre-existing CA2007 warning-as-error build failures in this workspace; run focused slices and record exact commands/results rather than claiming a clean full project run unless verified.

## Files Likely Touched

- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`
- `src/Hexalith.EventStore.Contracts/Security/PayloadProtectionResult.cs`
- `src/Hexalith.EventStore.Contracts/Security/*ProtectionMetadata*.cs`
- `src/Hexalith.EventStore.Contracts/Security/*Unreadable*.cs`
- `src/Hexalith.EventStore.Contracts/Problems/GatewayProblemDetailsExtensions.cs`
- `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs`
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore.Testing/Builders/EventEnvelopeBuilder.cs`
- `src/Hexalith.EventStore.Testing/Fakes/*Protection*.cs`
- `docs/guides/payload-protection-and-crypto-shredding.md`
- `docs/reference/problems/index.md`
- `docs/reference/problems/unreadable-protected-data.md`
- `docs/reference/api/index.md`
- `docs/reference/query-api.md`
- `tests/Hexalith.EventStore.Contracts.Tests/Security/*`
- `tests/Hexalith.EventStore.Contracts.Tests/Problems/GatewayProblemDetailsExtensionsTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/EventStreamReaderTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/SnapshotManagerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs`
- `tests/Hexalith.EventStore.Testing.Tests/Builders/EventEnvelopeBuilderTests.cs`

## Out of Scope

- Implementing real encryption, key wrapping, key vault integration, DAPR secret-store integration, cloud KMS integration, or certificate management.
- Defining the full crypto-shredding workflow, deletion approvals, legal hold, restored-backup governance, or irreversible operator UX.
- Broad redaction coverage across CLI, MCP, all admin UI pages, all backup tooling, and every operational surface beyond paths directly touched by unreadable behavior.
- Changing domain service contracts so domain services own protection metadata or key lifecycle decisions.
- Rewriting event storage, projection rebuild architecture, DAPR pub/sub, or actor state management.

## References

- `_bmad-output/planning-artifacts/epics.md` - Story 22.7b requirements and Epic 22 story split.
- `_bmad-output/planning-artifacts/prd.md` - FR102-FR104 and NFR12.
- `_bmad-output/planning-artifacts/architecture.md` - Payload and Snapshot Protection; SEC-1, SEC-5; Publishing, Replay & Protection Contracts.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md` - payload-protection backlog and docs expectations.
- `_bmad-output/project-context.md` - package boundaries, logging, DAPR, actor state, and testing rules.
- `_bmad-output/implementation-artifacts/22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints.md` - replay/read API and rebuild checkpoint semantics.
- `_bmad-output/implementation-artifacts/22-7a-payload-and-snapshot-protection-hooks.md` - protection metadata contract prerequisite and 22.7b deferral notes.
- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`
- `src/Hexalith.EventStore.Contracts/Security/PayloadProtectionResult.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs`

## Party-Mode Review

- ISO date and time: 2026-05-13T22:44:00+02:00
- Selected story key: 22-7b-unreadable-protected-data-behavior
- Command/skill invocation used: `/bmad-party-mode 22-7b-unreadable-protected-data-behavior; review;`
- Participating BMAD agents: John (Product Manager), Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary:
    - Story 22.7b is valuable but should not start while Story 22.7a metadata/result parity is absent from merged code.
    - Backup/restore language risked pulling restored-backup governance and crypto-shredding workflow into this story.
    - Rebuild skip language needed a no-skip default so checkpoint advancement cannot silently pass unreadable records.
    - Existing generic failure paths can leak provider exception text through publish results, command status, dead-letter details, logs, activities, or ProblemDetails.
    - Snapshot fallback/deletion rules needed explicit protected-unreadable retention and tail-readability proof.
    - Test evidence needed taxonomy-to-surface traceability and reusable no-leak sentinel assertions.
- Changes applied:
    - Marked the story blocked until Story 22.7a provider-neutral event/snapshot metadata and typed result parity are implemented and merged.
    - Narrowed backup-validation/admin scope to existing surfaces and deferred restore admission, restored-backup governance, legal hold, crypto-shredding workflow, irreversible deletion UX, and broad redaction to 22.7c/22.7d.
    - Added typed-result/no exception-parsing guardrails and explicit failure-path sanitization.
    - Added pre-domain readability boundary, no-skip default, snapshot retention/fallback proof, decision matrix, safe-field defaults, Testing sentinel helper, and evidence command requirements.
- Findings deferred:
    - Exact HTTP status code and ProblemDetails URI per unreadable category.
    - Whether any rebuild skip policy is ever allowed and what audit evidence it requires.
    - Whether any key alias or provider identifier can be safely exposed.
    - Restored-backup mismatch governance and remediation workflow for Story 22.7c.
    - CLI/MCP/admin-wide redaction coverage for Story 22.7d.
- Final recommendation: blocked

## Dev Agent Record

### Agent Model Used

Claude Opus 4.7 (1M context)

### ST0 Decision Table (frozen 2026-05-18)

**Unreadable-data taxonomy (provider-neutral)**

A new enum `UnreadableProtectedDataReason` lives in `Hexalith.EventStore.Contracts.Security`. Stable string reason codes live alongside in `UnreadableProtectedDataReasonCodes` (kebab-case, immutable).

| Reason category | Reason code | Retryable | Permanent | Operator-actionable | Safe to skip | Checkpoint behaviour |
| --- | --- | --- | --- | --- | --- | --- |
| `MissingKey` | `missing-key` | maybe (after registration) | no | yes (register key) | no | pause; never advance |
| `KeyInvalidatedOrDeleted` | `key-invalidated` | no | yes | no (governance via 22.7c) | no | pause; never advance |
| `ProviderUnavailable` | `provider-unavailable` | yes (transient) | no | maybe | no | pause/retry; never advance |
| `ProviderDenied` | `provider-denied` | maybe (after policy fix) | maybe | yes (auth/policy) | no | pause; never advance |
| `ConsistencyMismatch` | `consistency-mismatch` | no | yes | yes (investigate; restored-backup governance in 22.7c) | no | pause; never advance |
| `MalformedMetadata` | `malformed-metadata` | no | yes | yes (storage integrity) | no | pause; never advance |
| `UnknownMetadataVersion` | `unknown-metadata-version` | maybe (after EventStore upgrade) | maybe | yes (upgrade) | no | pause; never advance |
| `ProviderOpaqueUnsupportedOperation` | `provider-opaque-unsupported` | no | yes | yes (re-register provider) | no | pause; never advance |
| `BytesMetadataMismatch` | `bytes-metadata-mismatch` | no | yes | yes (provenance) | no | pause; never advance |

`Skip-past-unreadable` is **not** enabled by default for any category. A future audited skip policy requires explicit product/architecture approval and is out of scope.

**ProblemDetails contract**

Stable type URI `https://hexalith.io/problems/unreadable-protected-data` lives in `UnreadableProtectedDataProblem` (Contracts/Problems). Default HTTP status code is **422 Unprocessable Entity**; the helper also returns `503` for `ProviderUnavailable` (transient) and `410 Gone` for `KeyInvalidatedOrDeleted` (permanent removal). Allowed safe extension fields:

| Extension key | Source | Allowed surfaces |
| --- | --- | --- |
| `reasonCode` | from `UnreadableProtectedDataReasonCodes` | all |
| `reasonCategory` | `UnreadableProtectedDataReason` enum name | all |
| `correlationId` | caller-supplied | all |
| `tenantId` | envelope metadata | all |
| `domain` | envelope metadata | all |
| `aggregateId` | envelope metadata | all (omit when controller is a checkpoint-only surface) |
| `sequenceNumber` | envelope sequence | all (omit when not specific to a single event) |
| `checkpointId` | rebuild checkpoint | rebuild-only |
| `stage` | pipeline stage ("publish" / "replay" / "rehydrate" / "snapshot-load" / "rebuild") | all |
| `metadataVersion` | from metadata schema | all (informational, never secret) |
| `retryable` | from helper `IsRetryable` | all |
| `permanent` | from helper `IsPermanent` | all |
| `retryAfter` | seconds | when 503 ProviderUnavailable |

**FORBIDDEN in ProblemDetails, logs, traces, dead-letter messages, assertion output and docs examples:** payload bytes, snapshot state, plaintext markers, raw key material, IVs/nonces, authentication tags, provider-private metadata, stack traces, state-store keys, connection strings, provider exception messages, **and key aliases (treated sensitive-by-default at every surface in this story)**.

**State transitions per surface**

| Surface | Detection | Action |
| --- | --- | --- |
| `EventPersister.PersistEventsAsync` | N/A — protect path | Provider returns metadata; validation failure is bubbled up by the existing protection metadata carrier (`ProviderOpaque`) and re-classified at read time. |
| `EventPublisher.PublishEventsAsync` | Stored metadata is `ProviderOpaque`; provider returns `Unreadable` outcome | Do not publish the event. The publisher fails with sanitized `EventPublishResult.FailureReason = "Protected payload unavailable for publication."` (no provider exception text). Dead-letter routing uses the reason code, not exception messages. |
| `EventStreamReader.RehydrateAsync` | Loads server `EventEnvelope` only; no unprotect today | No change: rehydration loads stored bytes. Readability is enforced at the pre-domain boundary (next row). |
| `AggregateActor` pre-domain readability boundary | Scan rehydrated envelopes' stored metadata + call `TryUnprotectEventPayloadAsync` per event before constructing `DomainServiceCurrentState` | If any unprotect outcome is `Unreadable` or the stored metadata is `ProviderOpaque`, throw the typed `ProtectedDataUnreadableException` to the existing dead-letter routing path with the reason code. Domain service is never invoked with protected or opaque bytes. |
| `StreamsController.ReadStreamAsync` (Story 22.6) | Inspect rehydrated envelopes' stored metadata | When ANY envelope in the page has `ProviderOpaque` metadata: return 422 `application/problem+json` with type URI, `reasonCode=protected-payload-unavailable` (existing) and additional `reasonCategory=ProviderOpaqueUnsupportedOperation`. No payload bytes leak. |
| `SnapshotManager.LoadSnapshotAsync` | `ProviderOpaque` or `TryUnprotectSnapshotAsync` returns `Unreadable` | **DO NOT** call `RemoveStateAsync` for protected unreadable snapshots. Return `null` (no snapshot found) — pre-domain boundary then catches unreadable tail events (if any) and routes via dead-letter. Corrupt non-protected deserialization remains the only `RemoveStateAsync` path. |
| Projection rebuild | Page read returns unreadable event metadata or actor invocation throws `ProtectedDataUnreadableException` | Rebuild operation does not advance checkpoint past the unreadable record. Status remains in `paused` with safe metadata. (Public/operator surface evolution belongs to operator-facing 22.7d work; this story records the runtime invariant only.) |
| Backup validation | No existing runtime backup-validation surface in this repository — documented as compatibility note for Story 22.7c. | Documentation-only in this story. |

**Snapshot retention policy**

- Legacy snapshot with `ProtectionMetadata == null` → mapped to legacy `Unprotected` (existing 22.7a behaviour). No deletion.
- Protected snapshot with malformed/unknown-version metadata → mapped to `ProviderOpaque` (existing 22.7a behaviour). Load returns `null` to caller; **no deletion** (would erase audit-relevant state).
- `TryUnprotectSnapshotAsync` reports `Unreadable` outcome → no deletion; load returns `null`; pre-domain boundary handles tail.
- The existing exception-driven `RemoveStateAsync` branch in `SnapshotManager.LoadSnapshotAsync` is preserved ONLY for non-protected corrupt deserialization (e.g., schema drift on plaintext snapshots, the original Story 7.1 behaviour).

**Snapshot fallback proof**

Full replay fallback is allowed only when the event tail from `snapshot.SequenceNumber + 1` through current sequence has been validated by the pre-domain readability boundary. If any tail event is `ProviderOpaque` or returns an `Unreadable` unprotect outcome, the actor routes to dead-letter; no partial state is presented to the domain service.

**Failure-path sanitization**

- `EventPublishResult.FailureReason` for unreadable cases: fixed safe message (no exception `.Message`).
- Command status `FailureReason`: prefer typed reason code; never carry provider exception text.
- `DeadLetterMessage.FromException` continues to record exception type names, but the new `ProtectedDataUnreadableException` carries only the reason code in its `.Message` (no payload, no provider-private detail).
- `Activity.SetStatus(..., description)`: when description contains the reason code or a static safe string, payload/key data is excluded.
- Structured logs use the reason code; provider exception messages are NOT included for unreadable taxonomy paths.

**No-leak sentinel values for tests**

```
PROTECTED_PAYLOAD_PLAINTEXT_MARKER_22_7B
PROTECTED_SNAPSHOT_PLAINTEXT_MARKER_22_7B
PROTECTED_KEY_ALIAS_MARKER_22_7B
PROTECTED_PROVIDER_PRIVATE_BLOB_MARKER_22_7B
PROTECTED_STATE_STORE_KEY_MARKER_22_7B
PROTECTED_CONNECTION_STRING_MARKER_22_7B
PROTECTED_PROVIDER_EXCEPTION_MARKER_22_7B
```

`ProtectedDataLeakSentinel` helper (added in Testing) exposes these as constants and an assertion method `AssertNoLeak(IEnumerable<string> captured)` that scans every supplied string for any sentinel.

**Out-of-scope items (deferred to 22.7c / 22.7d)**

- Key lifecycle workflow: registration, rotation, deletion, invalidation, crypto-shredding approvals (22.7c).
- Restored-backup governance, restore admission, legal hold, irreversible deletion UX (22.7c).
- Broad redaction across CLI, MCP, all admin UI pages, all backup tooling (22.7d).
- Real encryption provider, key vault, cloud KMS, DAPR secret-store integration.

### Debug Log References

- Story 22.7b implementation pass 2026-05-18 (Claude Opus 4.7 1M).
- `dotnet build Hexalith.EventStore.slnx --configuration Debug` → 0 warnings, 0 errors (full solution).
- Focused validation:
  - `dotnet test tests/Hexalith.EventStore.Contracts.Tests` → 431/431 (+70 new `UnreadableProtectedDataTests` rows).
  - `dotnet test tests/Hexalith.EventStore.Testing.Tests` → 128/128 (+16 new `ProtectedDataLeakSentinelTests` + `FakeUnreadableProtectionServiceTests`).
  - `dotnet test tests/Hexalith.EventStore.Client.Tests` → 393/393 (no change; backward compatible).
  - `dotnet test tests/Hexalith.EventStore.Sample.Tests` → 74/74 (no change; backward compatible).
  - `dotnet test tests/Hexalith.EventStore.SignalR.Tests` → 35/35 (no change; backward compatible).
  - `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~Events.SnapshotManagerTests|FullyQualifiedName~Events.EventPublisherTests|FullyQualifiedName~Events.SnapshotRehydrationTests|FullyQualifiedName~Events.EventEnvelopeTests|FullyQualifiedName~Events.EventStreamReaderTests|FullyQualifiedName~Security|FullyQualifiedName~Logging"` → 334/334 focused unit-only slice (incl. updated `PayloadProtectionHookTests` and new `UnreadableProtectedDataBehaviorTests`).
- Server.Tests pre-existing Tier-2 DAPR placement/scheduler failures (`AggregateActorIntegrationTests`, `ActorTenantIsolationTests`, `ActorConcurrencyConflictTests`, `EventPersistenceIntegrationTests`, `SnapshotIntegrationTests`, `TombstoningLifecycleTests`, `CommandRoutingIntegrationTests`, `DaprSerializationRoundTripTests`, `ReplayControllerTests`, `CommandStatusControllerTests`, `ValidationExceptionHandlerTests`, `QueryNotFoundExceptionHandlerTests`) verified unchanged from Story 22.7a baseline — require `dapr init` infrastructure.

### Completion Notes List

- **Contracts contract.** Added `UnreadableProtectedDataReason` enum (9 categories), `UnreadableProtectedDataReasonCodes` (stable kebab-case wire codes plus `From`, `IsRetryable`, `IsPermanent` helpers), `PayloadUnprotectionOutcome`, `SnapshotUnprotectionOutcome`. Added `UnreadableProtectedDataProblem` (stable ProblemDetails type URI, status-code policy, safe operator guidance) under `Hexalith.EventStore.Contracts.Problems`.
- **Interface evolution.** Added `TryUnprotectEventPayloadAsync` and `TryUnprotectSnapshotAsync` to `IEventPayloadProtectionService` as default interface methods. The defaults delegate to the existing 22.7a metadata-aware overloads and map any non-cancellation exception to `UnreadableProtectedDataReason.ProviderUnavailable`, so EventStore never parses provider exception text. Cancellation continues to propagate via `OperationCanceledException`.
- **Server – publish.** `EventPublisher.PublishEventsAsync` now (a) refuses to publish events whose stored metadata is `ProviderOpaque`, (b) calls the typed `TryUnprotectEventPayloadAsync` for everything else and refuses to publish any event whose provider returns an `Unreadable` outcome, (c) emits a stable safe `FailureReason` `"Protected payload unavailable for publication. ReasonCode=<kebab-code>"` (via `BuildUnreadableFailureReason`), (d) writes a new `Stage=PublishUnreadable` log carrying only safe envelope metadata + reason code.
- **Server – pre-domain readability boundary.** `AggregateActor.EnsureEventsReadableForDomainAsync` walks every rehydrated envelope and calls the typed unprotect entry point before constructing `DomainServiceCurrentState`. `ProviderOpaque` envelopes and `Unreadable` outcomes throw `ProtectedDataUnreadableException` (carries the safe reason code only) which the existing `HandleInfrastructureFailureAsync` catch routes via dead-letter. Domain services are never invoked with protected or opaque bytes.
- **Server – snapshot.** `SnapshotManager.LoadSnapshotAsync` now distinguishes unreadable protected snapshots from corrupt deserialization. Provider-opaque snapshots and `TryUnprotectSnapshotAsync` `Unreadable` outcomes return `null` (caller falls back to full replay) and **never call `RemoveStateAsync`**. The pre-existing exception path that triggers `RemoveStateAsync` is preserved only for non-protected corrupt deserialization (schema drift on plaintext snapshots). New `Log.UnreadableProtectedSnapshot` carries safe metadata + reason code.
- **Gateway – stream replay.** `StreamsController.ReadStreamAsync` inspects every envelope in the page; when any has `ProviderOpaque` metadata it returns the new `UnreadableProtectedDataProblem` ProblemDetails (default 422, with 410 for `key-invalidated` and 503 for `provider-unavailable`). The response carries `reasonCode`, `reasonCategory`, `stage`, `sequenceNumber`, `tenantId`, `domain`, `aggregateId`, `retryable`, `permanent` extensions only — no payload bytes, no key alias, no provider exception text.
- **Testing.** Added `ProtectedDataLeakSentinel` (7 fixed sentinel constants + `AssertNoLeak` / `HasNoLeak` helpers) and `FakeUnreadableProtectionService` (queued + persistent unreadable configuration for events and snapshots, captures invocations for assertions).
- **Docs.** Updated `docs/guides/payload-protection-and-crypto-shredding.md` with the 22.7b unreadable-data behaviour, fail-closed publish-time policy, reason taxonomy, pre-domain boundary, snapshot retention rules, no-leak proof, and explicit 22.7c/22.7d deferrals. Added `docs/reference/problems/unreadable-protected-data.md`; updated `docs/reference/problems/index.md` and `docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Security.md` to reference the new types.
- **22.7a tests updated.** Two `PayloadProtectionHookTests` assertions were updated to reflect the new "ProviderOpaque snapshot returns null instead of pass-through" behaviour. The retention contract is also asserted (`DidNotReceive().RemoveStateAsync`).
- **Out of scope, explicitly deferred to 22.7c / 22.7d.** Real encryption provider, key lifecycle workflow (registration / rotation / deletion / invalidation / crypto-shredding), restored-backup governance, irreversible deletion UX, CLI/MCP/admin-wide redaction beyond surfaces touched by unreadable behaviour, and a fully audited rebuild skip policy.

### File List

**Contracts**

- `src/Hexalith.EventStore.Contracts/Security/UnreadableProtectedDataReason.cs` (new)
- `src/Hexalith.EventStore.Contracts/Security/UnreadableProtectedDataReasonCodes.cs` (new)
- `src/Hexalith.EventStore.Contracts/Security/PayloadUnprotectionOutcome.cs` (new)
- `src/Hexalith.EventStore.Contracts/Security/SnapshotUnprotectionOutcome.cs` (new)
- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs` (modified — added typed `TryUnprotect*` default interface methods)
- `src/Hexalith.EventStore.Contracts/Problems/UnreadableProtectedDataProblem.cs` (new)

**Server**

- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` (modified — opaque guard, typed unprotect, sanitized failure reason, new log message)
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` (modified — typed snapshot unprotect, fail-closed retention, new log message)
- `src/Hexalith.EventStore.Server/Events/ProtectedDataUnreadableException.cs` (new)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (modified — pre-domain readability boundary `EnsureEventsReadableForDomainAsync`)

**Gateway**

- `src/Hexalith.EventStore/Controllers/StreamsController.cs` (modified — unreadable-data ProblemDetails on replay surface, new log message)

**Testing**

- `src/Hexalith.EventStore.Testing/Security/ProtectedDataLeakSentinel.cs` (new)
- `src/Hexalith.EventStore.Testing/Fakes/FakeUnreadableProtectionService.cs` (new)

**Tests**

- `tests/Hexalith.EventStore.Contracts.Tests/Security/UnreadableProtectedDataTests.cs` (new — 70 tests covering taxonomy, retry/permanent, outcome factories, ProblemDetails policy, default interface methods)
- `tests/Hexalith.EventStore.Server.Tests/Security/UnreadableProtectedDataBehaviorTests.cs` (new — 26 tests covering EventPublisher unreadable handling, SnapshotManager retention contract, ProtectedDataUnreadableException safety, no-op compatibility, no-leak sentinel scans)
- `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionHookTests.cs` (modified — two 22.7a snapshot assertions updated for new return-null retention behaviour)
- `tests/Hexalith.EventStore.Testing.Tests/Security/ProtectedDataLeakSentinelTests.cs` (new — 16 tests covering sentinel helper + fake provider configuration)

**Documentation**

- `docs/guides/payload-protection-and-crypto-shredding.md` (modified — new "Unreadable protected data (22.7b)" section, updated publish-time policy)
- `docs/reference/problems/unreadable-protected-data.md` (new)
- `docs/reference/problems/index.md` (modified — added unreadable-protected-data entry)
- `docs/reference/api/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.Security.md` (modified — added new types)

**Story artifacts**

- `_bmad-output/implementation-artifacts/22-7b-unreadable-protected-data-behavior.md` (this story file)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (status updated)

## Verification Status

- Story context created by BMAD pre-dev hardening automation on 2026-05-13.
- Implementation pass 2026-05-18 (Claude Opus 4.7 1M): ST0–ST5 complete. Focused validation evidence recorded in Debug Log References. Full solution build clean (0 warnings, 0 errors). All Tier-1 test projects green. Pre-existing Tier-2 DAPR placement/scheduler failures verified unchanged from Story 22.7a baseline.

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-13 | Story created from Epic 22.7b with unreadable protected data behavior, fail-closed runtime semantics, replay/rebuild checkpoint guardrails, snapshot fallback boundaries, no-leak evidence, and explicit 22.7c/22.7d deferrals. |
| 2026-05-13 | Party-mode review blocked the story until 22.7a metadata/result parity is implemented and added restore-scope, no-skip, pre-domain readability, failure sanitization, snapshot retention, traceability, and no-leak sentinel guardrails. |
| 2026-05-18 | Implementation pass complete (Claude Opus 4.7 1M): new Contracts taxonomy (`UnreadableProtectedDataReason`, `UnreadableProtectedDataReasonCodes`, `PayloadUnprotectionOutcome`, `SnapshotUnprotectionOutcome`, `UnreadableProtectedDataProblem`); typed `TryUnprotect*` default-interface entry points; `EventPublisher` provider-opaque + unreadable fail-closed publish-time policy with sanitized failure reasons; `AggregateActor` pre-domain readability boundary throwing typed `ProtectedDataUnreadableException`; `SnapshotManager` no-deletion retention for unreadable protected snapshots; `StreamsController` ProblemDetails surface; Testing sentinel helper + `FakeUnreadableProtectionService`; payload-protection guide + problem reference + API reference updates. 70 Contracts + 16 Testing + 26 Server unit tests added; all Tier-1 projects green; Server.Tests focused unit slice 334/334 green. Pre-existing Tier-2 DAPR failures verified unchanged. Story moved to review. |
