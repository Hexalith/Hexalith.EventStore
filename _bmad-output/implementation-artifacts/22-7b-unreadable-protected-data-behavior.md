# Story 22.7b: Unreadable Protected Data Behavior

Status: blocked

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

- [ ] **ST0 - Freeze unreadable-data taxonomy and state transitions.** (AC: 1, 2, 3, 4, 5, 6)
    - [ ] Read this story, Story 22.7a, Epic 22, PRD FR102-FR104, architecture `Payload and Snapshot Protection`, Story 22.6 replay/rebuild contracts, and `_bmad-output/project-context.md` before code edits.
    - [ ] Confirm the Story 22.7a protection metadata carrier and typed event/snapshot result parity exist in merged code. If they do not, stop and record this story as blocked instead of guessing from payload bytes.
    - [ ] Define unreadable reason categories for missing key, deleted/invalidated key, provider unavailable, provider denied, source-or-metadata consistency mismatch, malformed metadata, unknown required metadata version, provider-opaque unsupported operation, and bytes/metadata mismatch.
    - [ ] If source-or-metadata consistency mismatch is caused by restored-backup provenance, record only the safe status evidence and defer governance/remediation workflow to Story 22.7c.
    - [ ] Define which categories are retryable, permanent, operator-actionable, safe-to-skip, or blocked from checkpoint advancement.
    - [ ] Produce a dated decision table covering `reason category x surface x retryability x permanence x checkpoint behavior x ProblemDetails/status x allowed safe fields x required tests`.
    - [ ] Define event and snapshot state-transition tables for persist, publish, rehydrate, public read, replay, rebuild, backup validation, and admin inspection.
    - [ ] Define when protected snapshots may fall back to replay, when they must be retained, and when removal is allowed as corrupt data.
    - [ ] Define stable ProblemDetails type URIs or operational status values and their allowed non-secret fields.
    - [ ] Define whether key aliases or provider identifiers are ever safe to expose per surface; default to redacted when not explicitly approved.
    - [ ] Define exact no-leak sentinel values for tests: payload plaintext, snapshot plaintext, key alias, provider-private blob, state-store key, connection string, and provider exception message.
    - [ ] Record explicit out-of-scope items for 22.7c and 22.7d: key lifecycle workflow, restored-backup governance, irreversible deletion UX, CLI/MCP redaction, and complete operational surface coverage.

- [ ] **ST1 - Add contract-level unreadable-data result and ProblemDetails shape.** (AC: 1, 3, 5, 6)
    - [ ] Add immutable result/status types in Contracts for unreadable protected data where public clients, Testing builders, replay/read DTOs, or admin responses need stable shape.
    - [ ] Add or extend ProblemDetails helpers for unreadable protected data using a stable type URI, status code policy, reason category, stream identity metadata, and safe operator guidance.
    - [ ] Keep provider-specific exception types, key-store SDK details, and encryption-provider dependencies out of Contracts.
    - [ ] Ensure serialization round trips preserve the reason category and safe metadata without preserving secret fields.
    - [ ] Add contract tests for JSON serialization, nullability, validation, no-op compatibility, and unsafe metadata rejection/redaction.

- [ ] **ST2 - Handle unreadable events in publish, read, replay, and rehydrate flows.** (AC: 1, 3, 4, 6)
    - [ ] Update event unprotection call sites to map provider unreadable outcomes to the ST0 taxonomy without logging payload bytes or provider-private exception messages.
    - [ ] Update `EventPublisher` so unreadable protected payloads are not published as plaintext, protected bytes with unprotected metadata, or generic infrastructure failures without the stable unreadable reason.
    - [ ] Sanitize `EventPublishResult.FailureReason`, command status failure reasons, dead-letter messages, pipeline failure reason fields, logs, and `Activity.SetStatus` descriptions so provider exception messages and protected-data markers never surface.
    - [ ] Update command-time rehydration/deserialization paths so domain services are not invoked after unreadable event data is detected.
    - [ ] Add a single pre-domain readability boundary before `DomainServiceCurrentState` construction and `domainServiceInvoker.InvokeAsync`; domain services must never receive stored protected bytes through `ToContractEventEnvelope`.
    - [ ] Update public stream read/replay DTO mapping to return safe ProblemDetails/status information instead of payload bytes when unreadable data is encountered.
    - [ ] Preserve `OperationCanceledException` propagation and cancellation-token flow through protection hooks.
    - [ ] Add focused tests for missing key, invalidated key, provider unavailable, malformed metadata, unknown version, provider-opaque unsupported read, and bytes/metadata mismatch.

- [ ] **ST3 - Handle unreadable snapshots without unsafe deletion or fallback.** (AC: 2, 4, 5, 6)
    - [ ] Update `SnapshotManager.LoadSnapshotAsync` to distinguish unreadable protected snapshots from corrupt snapshots and generic deserialization failures.
    - [ ] Preserve protected snapshot records unless ST0 explicitly classifies them as corrupt and safe to delete.
    - [ ] Allow full replay fallback only after loading and successfully unprotecting every event from `snapshot.SequenceNumber + 1` through the current metadata sequence and confirming checkpoint/state semantics are safe.
    - [ ] Add tests for unreadable protected snapshot with readable event tail, unreadable protected snapshot plus unreadable tail event, corrupt unprotected snapshot, legacy no-metadata snapshot, and no-op snapshot.
    - [ ] Add assertions that protected unreadable snapshots do not call `RemoveStateAsync` unless ST0 labels the record `corrupt-safe-to-remove`.
    - [ ] Prove snapshot failure logs and exceptions never include snapshot state, plaintext markers, unsafe key aliases, provider-private blobs, or provider exception secret text.

- [ ] **ST4 - Integrate replay, rebuild, backup-validation, and Testing surfaces.** (AC: 3, 4, 5, 6)
    - [ ] Add unreadable-data status handling to Story 22.6 stream read/replay and projection rebuild checkpoint models where those models exist.
    - [ ] Ensure failed/paused/retryable rebuild states do not advance checkpoints past unreadable records unless ST0 defines a safe audited skip policy.
    - [ ] Add Testing builders/fakes for unreadable event payloads, unreadable snapshots, provider-unavailable results, permanent key invalidation, and safe ProblemDetails assertions.
    - [ ] Add backup-validation behavior only to the extent the current code has a validation surface; otherwise record the required compatibility contract in docs for 22.7c and do not claim runtime validation.
    - [ ] Enumerate current in-scope runtime surfaces in the Dev Agent Record; mark unavailable surfaces as documentation-only compatibility notes.
    - [ ] Keep all fake providers deterministic and provider-neutral; do not add Key Vault, DAPR secret store, certificate, or cloud KMS dependencies.

- [ ] **ST5 - Update documentation and no-leak evidence.** (AC: 3, 4, 5, 6)
    - [ ] Update `docs/guides/payload-protection-and-crypto-shredding.md` with unreadable-data behavior, fail-closed semantics, operator-safe statuses, and deferrals to 22.7c/22.7d.
    - [ ] Update problem reference docs and API/replay/read docs for the stable unreadable ProblemDetails/status shape.
    - [ ] Extend security/logging tests to assert sentinel values never appear in logs, traces, ProblemDetails, exception messages, assertion output, or docs examples.
    - [ ] Add or reuse a deterministic `ProtectedDataLeakSentinel`/assertion helper in Testing so logs, traces, ProblemDetails, exceptions, docs examples, and assertion messages are scanned consistently.
    - [ ] Run focused test projects individually and record evidence. At minimum: `tests/Hexalith.EventStore.Contracts.Tests`, relevant `tests/Hexalith.EventStore.Server.Tests` slices, and `tests/Hexalith.EventStore.Testing.Tests` if Testing builders/fakes change.

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

TBD by dev agent.

### Debug Log References

- TBD by dev agent.

### Completion Notes List

- TBD by dev agent.

### File List

- TBD by dev agent.

## Verification Status

- Story context created by BMAD pre-dev hardening automation on 2026-05-13.
- Pre-dev creation validation pending below is automation-only and does not claim implementation verification.

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-13 | Story created from Epic 22.7b with unreadable protected data behavior, fail-closed runtime semantics, replay/rebuild checkpoint guardrails, snapshot fallback boundaries, no-leak evidence, and explicit 22.7c/22.7d deferrals. |
| 2026-05-13 | Party-mode review blocked the story until 22.7a metadata/result parity is implemented and added restore-scope, no-skip, pre-domain readability, failure sanitization, snapshot retention, traceability, and no-leak sentinel guardrails. |
