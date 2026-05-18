# Story 22.7c: Crypto-Shredding Workflow and Restored-Backup Safety

Status: ready-for-dev

Context created: 2026-05-13
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR103 key deletion/invalidation workflow and restored-backup safety, building on the Story 22.7a protection metadata/result contract and Story 22.7b unreadable protected-data taxonomy. This story owns provider-neutral workflow contracts, restore admission/safety decisions, audit evidence, and documentation. It does not implement real cryptography, key vault/KMS integration, legal hold UX, or broad operational redaction across every surface.

## Story

As a platform owner,
I want key deletion/invalidation and restored-backup checks to be documented and tested,
so that GDPR deletion workflows remain safe after backup restore and operational recovery.

## Contract Boundaries

- This story must not start runtime implementation until Story 22.7a has delivered provider-neutral event and snapshot protection metadata/result parity. Without that metadata, EventStore cannot prove whether restored data is protected, legacy-compatible, malformed, provider-opaque, or unsafe.
- Party-mode review on 2026-05-14 confirmed this is a hard implementation blocker, not a documentation preference. Keep this story blocked until Story 22.7a has implemented and merged the canonical metadata/result shape, or a human architecture decision explicitly narrows this story to non-runtime contract/documentation work.
- This story should consume the Story 22.7b unreadable protected-data taxonomy when available. If 22.7b is still blocked or incomplete, ST0 must freeze a temporary compatibility table for this story and record the dependency gap instead of guessing from provider exceptions or raw payload bytes.
- Any temporary ST0 compatibility table must be named as temporary, include the removal/reconciliation condition once Story 22.7b lands, and avoid introducing taxonomy names that conflict with Story 22.7b.
- Crypto-shredding means EventStore records and enforces the operational consequences of key deletion or invalidation. It does not mean deleting immutable event records, snapshots, command status records, or audit metadata from the event store.
- Key lifecycle ownership remains provider/operator-owned. EventStore owns safe workflow state, restore validation, audit metadata, idempotency, and no-silent-readability guarantees at EventStore boundaries.
- Restored backups must never make previously shredded protected content silently readable. Any restored protected stream, snapshot, or manifest that conflicts with key invalidation state must require an explicit documented operator decision before protected content can be served.
- This story may add restore admission/status contracts even if backup execution remains deferred. Current admin backup command services return deferred results, so runtime backup engine claims must be limited to surfaces that actually exist.
- Audit evidence must preserve stream metadata needed for compliance: tenant, domain, aggregate, sequence/checkpoint ranges, protection metadata state, key alias or provider reference only when approved as non-secret, workflow command identity, correlation ID, timestamp, and decision outcome.
- Public and operational responses must never include payload bytes, snapshot state, plaintext markers, raw keys, IVs/nonces, provider-private metadata, connection strings, state-store keys, stack traces, or provider exception text.
- Domain services must not receive key lifecycle commands, key material, restored-backup decisions, or provider-specific deletion semantics. EventStore and operator/provider integration own the workflow boundary.
- Domain services must receive either valid payload events or no invocation. They must not receive placeholder payloads, "shredded" payload variants, provider-neutral lifecycle status, or restored-backup admission state.
- Story 22.7d owns full redaction coverage across logs, ProblemDetails, admin UI, CLI, MCP, replay, rebuild, backup validation, and test artifacts. This story must still protect any new workflow/status/audit surfaces it creates from obvious protected-data leakage.

## Party-Mode Hardening Decisions

- **Implementation gate:** Story 22.7c is blocked until Story 22.7a provider-neutral event/snapshot protection metadata and typed result parity exist in merged code. Story 22.7b unreadable taxonomy remains a dependent contract; ST0 may define only a temporary compatibility table when 22.7b is still blocked.
- **Single decision boundary:** Runtime paths must consume one EventStore-owned crypto-shredding/readability decision result instead of recomputing policy in command rehydration, publication, public reads, replay, rebuild, backup validation, admin, CLI, or MCP layers.
- **Canonical decision shape:** ST0 must define stable provider-neutral status and reason-code names for `Readable`, `Unreadable`, `QuarantineRequired`, `OperatorDecisionRequired`, `DeferredValidation`, `MalformedMetadata`, `ProviderUnavailable`, `ProviderOpaque`, `UnknownVersion`, and `RestoreConflict` or their approved equivalents.
- **Workflow state shape:** ST0 must define allowed transitions for requested, approved, rejected, pending-provider, invalidated, deleted, verification-failed, restore-conflict, quarantined, operator-decision-required, completed, and cancelled-before-decision states. Cancellation after an irreversible decision is recorded must not undo that decision; it must return honest auditable status.
- **Restore admission boundary:** This story may model restore admission/status based on EventStore metadata and shredding evidence. It must not claim physical backup scanning, repair, transport validation, provider reachability, durable quarantine enforcement, or cryptographic safety while backup execution remains deferred.
- **Operator-facing output:** Restore conflicts must expose a provider-neutral status, stable safe reason code, correlation/audit identifier, and allowed next action. Text must be localizable and must not encode the only machine-readable semantics.
- **No-leak scope:** No-leak checks apply to surfaces created or touched by this story. Broad redaction across all existing operational surfaces remains Story 22.7d work unless this story modifies that surface.

## Current Implementation Intelligence

- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs` currently exposes protect/unprotect hooks for events and snapshots. Snapshot hooks still return raw `object`, and event results currently carry only payload bytes plus serialization format.
- `src/Hexalith.EventStore.Contracts/Security/PayloadProtectionResult.cs` currently has no provider-neutral protection metadata, key reference, invalidation state, or unreadable-result shape. Story 22.7a is expected to change this.
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs` currently stores sequence, state, created time, domain, aggregate ID, and tenant ID, with no protection or restore-provenance metadata.
- `docs/guides/payload-protection-and-crypto-shredding.md` does not exist yet. Story 22.7a and 22.7b mention it as the expected guide; this story must extend it with key lifecycle and restore-safety workflow details without claiming provider-specific crypto support.
- Admin backup services exist in `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs` and `DaprBackupQueryService.cs`, but trigger, validate, restore, export, and import operations currently return deferred results. Do not claim live backup/restore behavior unless this story creates the approved engine or narrows to admission/status contracts around the deferred surface.
- Backup and snapshot user-facing surfaces exist in Admin CLI, Admin Server, Admin UI, Admin MCP, and tests. Use existing models and services rather than creating a parallel backup subsystem.
- Story 22.7b is currently blocked by missing 22.7a metadata/result parity. Its review deferred restored-backup mismatch governance and remediation workflow to this story.
- Existing project rules require no payload logging, EventStore-owned envelope metadata, no direct DAPR state-store bypass for actor state, root-level submodules only, and focused tests run individually.

## Acceptance Criteria

1. **Crypto-shredding workflow state is provider-neutral and auditable.**
   - Given a platform operator initiates a key deletion or invalidation workflow
   - When EventStore records the workflow decision
   - Then the resulting contract identifies tenant, domain, aggregate or stream scope, affected sequence/checkpoint range, protection metadata state, safe provider/key reference policy, requested action, decision outcome, actor identity, correlation ID, timestamp, and irreversible-read consequence without storing key material or protected payload content.
   - And repeated deletion/invalidation requests are idempotent and produce deterministic status rather than duplicate or contradictory audit records.
   - And idempotency is based on stable workflow identity plus tenant/domain/aggregate-or-stream/range/key-reference scope, not repeated request-body comparison alone.
   - And repeated requests after terminal success return existing status and audit evidence rather than creating another workflow record.
   - And immutable event and snapshot records remain present as audit evidence; protected content becomes unreadable by key lifecycle policy, not by deleting event-store records.
   - And cancellation before the irreversible decision freeze can stop with an auditable non-terminal state, while cancellation after the decision freeze cannot undo key invalidation/deletion and must return honest auditable status instead of a generic provider failure.

2. **Key deletion and invalidation consequences are explicit on reads and rebuild paths.**
   - Given protected payloads or snapshots reference a deleted, invalidated, revoked, or intentionally unavailable key
   - When EventStore performs command-time rehydration, event publication, public stream read, replay, projection rebuild, backup validation, or admin inspection
   - Then the operation uses the single EventStore-owned decision boundary and the Story 22.7b unreadable-data taxonomy or this story's ST0 compatibility table to fail closed with safe status only.
   - And command-time rehydration, event publication, public stream reads, replay, snapshot hydration, projection rebuild, backup validation/status, and admin inspection each have path-specific expected outcomes recorded in tests or Dev Agent Record notes.
   - And domain services are not invoked with placeholder payloads, partially recovered state, or restored protected bytes that bypass the key lifecycle decision.
   - And projection rebuild checkpoints do not advance past intentionally shredded content unless a future approved skip policy records audit evidence and remains out of scope for this story.
   - And responses include safe operator guidance while excluding payload, snapshot, key material, provider-private details, and provider exception messages.

3. **Restored backups cannot silently reverse crypto-shredding.**
   - Given a backup is restored after key deletion or invalidation
   - When EventStore validates restored event payloads, snapshots, manifests, replay checkpoints, or backup provenance
   - Then restored data that conflicts with key lifecycle state is blocked, quarantined, marked unreadable, or requires an explicit documented operator decision before any protected content can be served.
   - And restored backup admission compares safe metadata such as tenant, domain, aggregate, sequence range, protection metadata version, provider/key alias policy, deletion/invalidation watermark, backup creation time, restore time, and manifest identity.
   - And restore admission can block, quarantine, require operator decision, defer validation, or accept only based on available EventStore metadata/status evidence; it must not claim physical backup inspection, repair, provider reachability, or cryptographic verification when those capabilities are absent.
   - And a restored backup admission conflict exposes only provider-neutral status, stable reason code, audit/correlation identifier, and allowed next action.
   - And a restored backup never downgrades missing, malformed, unknown-version, provider-opaque, or bytes/metadata mismatch states to `unprotected`.
   - And the operator decision, when allowed, is idempotent, auditable, and scoped to the affected stream/range rather than global.

4. **Backup and restore surfaces remain honest about current capabilities.**
   - Given current backup trigger, validate, restore, export, and import operations are deferred
   - When this story touches admin, CLI, MCP, API, or documentation surfaces
   - Then it either implements an approved provider-neutral admission/status contract or documents the required compatibility contract without pretending a full backup engine exists.
   - And any new deferred or blocked result explains the safety reason without exposing protected data.
   - And existing admin backup services, models, CLI commands, MCP tools, and tests are extended where appropriate instead of replaced with a parallel workflow.
   - And restore admission decisions are separated from physical backup transport, storage engine, and provider key deletion execution.
   - And admin/CLI/MCP/UI tests are required only for surfaces touched by this story; untouched surfaces remain under Story 22.7d or future backup-engine work.

5. **No-op, legacy, and provider-opaque data remain safe and compatible.**
   - Given no custom protection provider is configured
   - When existing event, snapshot, backup, restore, replay, rebuild, and admin flows run
   - Then behavior remains equivalent to the current no-op path and no shredding or restore-safety status is emitted by inference.
   - And legacy events/snapshots with missing protection metadata follow the Story 22.7a compatibility state or this story's ST0 table.
   - And provider-opaque metadata is carried or rejected according to an explicit policy; EventStore never inspects provider-private blobs or guesses whether content is readable.
   - And malformed metadata, unsafe aliases, unknown versions, and restored content/metadata mismatches fail closed rather than being treated as unprotected data.
   - And provider-opaque, unknown, or contradictory restore/readability states are never treated as verified shredding success or verified safe readability.

6. **Documentation and tests prove irreversible workflow and no-leak behavior.**
   - Given crypto-shredding, key invalidation, and restore-safety paths are exercised
   - When docs, tests, logs, ProblemDetails, exceptions, audit records, admin responses, CLI output, MCP output, and test assertion messages are inspected
   - Then sentinel payload, snapshot, key, key-alias, provider-private, connection-string, state-store-key, and provider-exception markers never appear.
   - And `docs/guides/payload-protection-and-crypto-shredding.md` explains the workflow, irreversible consequences, restore-safety checks, operator decisions, and current deferred runtime boundaries.
   - And docs/examples use stable reason codes plus localizable safe text for operator messages; English prose is not the contract.
   - And Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story to review.

## Tasks / Subtasks

- [ ] **ST0 - Freeze crypto-shredding and restore-safety decisions.** (AC: 1, 2, 3, 4, 5, 6)
    - [ ] Read this story, Stories 22.7a and 22.7b, Epic 22, PRD FR102-FR104, architecture `Payload and Snapshot Protection`, Story 22.6 replay/rebuild contracts, backup/admin stories 16.4 and 17.6, and `_bmad-output/project-context.md` before code edits.
    - [ ] Confirm whether Story 22.7a protection metadata/result parity is implemented in code. If absent, limit runtime changes to docs/status/admission contracts and record the implementation blocker.
    - [ ] Confirm whether Story 22.7b unreadable-data taxonomy is implemented. If absent, define a temporary compatibility table for key-deleted, key-invalidated, restored-conflict, provider-opaque, malformed-metadata, unknown-version, and bytes/metadata-mismatch outcomes.
    - [ ] If Story 22.7a metadata/result parity is still absent, stop before runtime implementation and leave this story blocked unless a human architecture decision explicitly narrows the work to contract/documentation-only preparation.
    - [ ] Define the single EventStore-owned crypto-shredding/readability decision result consumed by read, replay, rebuild, publication, backup admission/status, admin, CLI, and MCP surfaces.
    - [ ] Define closed provider-neutral status/reason names for readable, unreadable, quarantine-required, operator-decision-required, deferred-validation, malformed-metadata, provider-unavailable, provider-opaque, unknown-version, restore-conflict, and no-op/legacy-compatible outcomes.
    - [ ] Define the provider-neutral crypto-shredding workflow states: requested, approved, rejected, pending-provider, invalidated, deleted, verification-failed, restore-conflict, quarantined, operator-decision-required, and completed.
    - [ ] Define cancellation-before-decision and cancellation-after-decision behavior so cancellation never creates partial shredding evidence and never reverses an irreversible invalidation/deletion decision.
    - [ ] Define idempotency keys and conflict behavior for repeated workflow requests over the same tenant/domain/aggregate/range.
    - [ ] Define what EventStore may safely store for key references. Treat provider IDs and aliases as sensitive-by-default until the table marks the exact field safe for the exact surface.
    - [ ] Define restore admission states and allowed transitions for backups created before and after key invalidation.
    - [ ] Define safe fields allowed in audit records, ProblemDetails/status, logs/traces, admin/CLI/MCP output, and docs examples.
    - [ ] Define operator-facing restore-conflict output as provider-neutral status, stable reason code, audit/correlation identifier, and allowed next action, with localizable safe text.
    - [ ] Define exact no-leak sentinel values for tests and documentation scans.
    - [ ] Record explicit out-of-scope items for provider-specific crypto, KMS/key vault integration, legal hold policy, physical backup engine implementation, broad CLI/MCP/admin redaction, and skip-past-shredded replay policy.

- [ ] **ST1 - Add provider-neutral workflow and restore-safety contracts.** (AC: 1, 3, 4, 5)
    - [ ] Add small immutable contract/status records where public clients, admin services, Testing builders, or backup validation surfaces need stable shape.
    - [ ] Model key lifecycle decisions as safe metadata and status, not provider exception strings.
    - [ ] Add restore admission/status records that can represent blocked, quarantined, unreadable, operator-decision-required, and accepted outcomes without payload disclosure.
    - [ ] Keep contracts limited to provider-neutral DTOs/enums/status records; do not add provider SDK, KMS, vault, cloud crypto, DAPR secret-store, certificate, or algorithm dependencies.
    - [ ] Ensure all server/admin/CLI/MCP surfaces consume the shared decision result rather than recomputing crypto-shredding or restore-safety policy locally.
    - [ ] Keep provider-specific SDKs, key vaults, cloud KMS, DAPR secret-store APIs, and encryption algorithms out of Contracts and default packages.
    - [ ] Add validation for nullability, bounded string lengths, serialization round trips, invalid status combinations, unsafe key aliases, and unsupported provider-private metadata.
    - [ ] Add Testing builders/fakes for workflow decisions, restore conflicts, and safe audit/status assertions.

- [ ] **ST2 - Integrate key lifecycle state with read/replay/rebuild behavior.** (AC: 2, 5, 6)
    - [ ] Add a single EventStore-owned decision point that maps key lifecycle or restored-conflict state to unreadable/fail-closed behavior before domain invocation, publication, public read, replay, rebuild, or backup validation can emit content.
    - [ ] Prove command-time rehydration, event publication/projection, public stream read, replay, snapshot hydration, projection rebuild, backup validation/status, and admin inspection each route through that decision point or explicitly record why the path is untouched.
    - [ ] Ensure command-time rehydration and projection rebuild do not treat intentionally shredded content as corrupt data to delete or skip.
    - [ ] Ensure checkpoint advancement stops or pauses at shredded/unreadable content unless a future approved policy explicitly allows audited skip behavior.
    - [ ] Preserve cancellation-token flow through protection/key lifecycle checks and let `OperationCanceledException` propagate.
    - [ ] Sanitize command status failure reasons, event publication failure reasons, logs, traces, `Activity.SetStatus`, ProblemDetails, and admin results so provider details do not leak.
    - [ ] Add focused tests for deleted key, invalidated key, provider unavailable during verification, restored-conflict state, provider-opaque state, malformed metadata, unknown version, and no-op compatibility.

- [ ] **ST3 - Add restored-backup admission and safety checks.** (AC: 3, 4, 5, 6)
    - [ ] Inventory existing backup models and deferred services before adding new code: Admin Abstractions backup models, Admin Server backup services/controllers, Admin CLI backup commands, Admin MCP backup tools, Admin UI backup page, and backup tests.
    - [ ] Add restore-safety validation only to an approved admission/status boundary. Do not implement physical backup restore unless architecture explicitly approves it in ST0.
    - [ ] Compare restored backup metadata against key lifecycle watermarks and protection metadata using safe fields only.
    - [ ] Return blocked/quarantined/operator-decision-required status when restored data could reverse an invalidation or deletion.
    - [ ] Require explicit operator decision for any accepted restore conflict and record tenant/domain/aggregate/range, backup identity, decision identity, correlation ID, timestamp, and reason.
    - [ ] Ensure old backups without protection metadata are handled by the legacy compatibility table and cannot silently become unprotected.
    - [ ] Add tests for backup before deletion, backup after deletion, restore after deletion, restore with missing metadata, restore with stale manifest, restore with provider-opaque metadata, and restore dry-run/status-only flows.
    - [ ] Add tests for restore status `DeferredValidation` or equivalent when provider/runtime evidence is unavailable; do not mark the backup safe in that case.

- [ ] **ST4 - Update admin, CLI, MCP, and documentation surfaces honestly.** (AC: 3, 4, 6)
    - [ ] Update admin/API/CLI/MCP contracts only where the current surface exists. If a surface is still deferred, return or document a deferred safety status instead of inventing a live operation.
    - [ ] Extend `docs/guides/payload-protection-and-crypto-shredding.md` with provider-neutral lifecycle states, irreversible consequences, restore admission, operator decision requirements, no-op/legacy behavior, and explicit deferrals.
    - [ ] Update backup/restore docs and problem/reference docs if new statuses or ProblemDetails types are introduced.
    - [ ] Keep examples provider-neutral and avoid Azure Key Vault, DAPR secret store, local certificate, or algorithm-specific assumptions.
    - [ ] Add or extend docs/test no-leak scans for workflow examples, audit examples, restore-status examples, and deferred-result text.
    - [ ] Record unavailable runtime surfaces in the Dev Agent Record rather than claiming unimplemented behavior.

- [ ] **ST5 - Prove idempotency, restore safety, compatibility, and no-leak evidence.** (AC: 1, 2, 3, 4, 5, 6)
    - [ ] Add Contracts tests for workflow/status serialization, validation, idempotency keys, unsafe metadata rejection, and no-op/legacy compatibility.
    - [ ] Add Server tests for read/replay/rebuild/key lifecycle decision boundaries where implementation exists.
    - [ ] Add Admin Server, CLI, MCP, UI, or documentation tests only for surfaces touched by this story.
    - [ ] Add no-leak sentinel scans covering logs, traces, ProblemDetails/status, exceptions, audit records, admin output, CLI output, MCP output, docs examples, and assertion messages for touched surfaces.
    - [ ] Add path-specific negative tests for key invalidated before read, rebuild/replay, event publication/projection, snapshot hydration, repeated workflow request, cancellation before decision freeze, cancellation after decision freeze, restore with shredded key/version, and restore with missing/legacy/provider-opaque metadata.
    - [ ] Run focused test projects individually and record evidence. Minimum expected commands: `dotnet test tests/Hexalith.EventStore.Contracts.Tests`, focused `dotnet test tests/Hexalith.EventStore.Server.Tests --filter ...` slices for touched event/snapshot/replay behavior, and the relevant Admin/CLI/MCP/Testing project tests for any touched surface.
    - [ ] Preserve the known `Hexalith.EventStore.Server.Tests` CA2007 caveat for broad project runs; record exact commands and results.

## Test Evidence Required

- Contract tests prove workflow state, restore admission status, idempotency keys, safe metadata fields, serialization round trips, and invalid combination rejection.
- Compatibility tests prove no-op providers and legacy no-metadata events/snapshots/backups do not emit shredding or restore-conflict status by inference.
- Runtime tests prove deleted or invalidated key state fails closed before domain invocation, publication, read/replay output, rebuild checkpoint advancement, or backup validation emits content.
- Path-specific runtime tests prove key invalidated before read, replay/rebuild, event publication/projection, and snapshot hydration each return safe unreadable/status behavior without decrypted payload or snapshot state.
- State-machine tests prove repeated workflow requests return existing terminal audit/status, cancellation before decision freeze records a non-terminal cancellation state, and cancellation after decision freeze cannot undo an invalidation/deletion decision.
- Restore-safety tests prove backups restored after deletion or invalidation are blocked, quarantined, or operator-decision-required until an explicit documented decision is recorded.
- Restore admission tests prove missing, legacy, provider-opaque, stale manifest, shredded key/version, unavailable provider/runtime evidence, and bytes/metadata mismatch cases fail closed or return deferred/operator-decision-required status without claiming safety.
- Admin/CLI/MCP/UI tests cover only touched surfaces and must remain honest when the underlying backup engine is deferred.
- No-leak tests prove sentinel payload, snapshot, key, key alias, provider-private, provider exception, state-store key, connection string, and plaintext markers do not appear in logs, traces, ProblemDetails/status, exceptions, audit records, admin output, CLI output, MCP output, docs examples, or assertion messages.
- Cancellation tests prove cancellation during provider/key lifecycle checks remains cancellation and does not create partial workflow or restore decisions.
- Evidence commands are recorded exactly in the Dev Agent Record.

## Developer Notes

- Preserve EventStore's ownership of envelope metadata. Domain services return payload events only and must not own protection metadata, key lifecycle, or restore-safety decisions.
- Do not delete immutable event records or snapshots as part of crypto-shredding. The security outcome comes from key lifecycle plus fail-closed runtime/read behavior while audit metadata remains intact.
- Prefer explicit immutable records and decision tables over stringly typed dictionaries or exception-message parsing.
- Treat key aliases and provider identifiers as sensitive-by-default. Only expose them where ST0 marks a field safe for that surface.
- Keep provider integration out of this story unless a provider-neutral test fake is needed. Do not add Key Vault, cloud KMS, DAPR secret-store, certificate, or algorithm dependencies.
- Do not bypass `IActorStateManager` or inspect actor state directly to validate restored data.
- Do not add ad hoc retry loops around provider verification. Retryability belongs in the approved status taxonomy and operator workflow.
- If backup runtime support is still deferred, implement only the safest admission/status/documentation layer and record physical restore execution as out of scope.
- If compatibility pressure conflicts with no-leak or no-silent-readability guarantees, preserve the security guarantee and record the compatibility gap for product/architecture decision.

## Files Likely Touched

- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`
- `src/Hexalith.EventStore.Contracts/Security/PayloadProtectionResult.cs`
- `src/Hexalith.EventStore.Contracts/Security/*ProtectionMetadata*.cs`
- `src/Hexalith.EventStore.Contracts/Security/*Unreadable*.cs`
- `src/Hexalith.EventStore.Contracts/Security/*CryptoShredding*.cs`
- `src/Hexalith.EventStore.Contracts/Security/*RestoreSafety*.cs`
- `src/Hexalith.EventStore.Contracts/Problems/GatewayProblemDetailsExtensions.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/*`
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IBackupCommandService.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IBackupQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/*`
- `src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs`
- `src/Hexalith.EventStore.Testing/Fakes/*Protection*.cs`
- `docs/guides/payload-protection-and-crypto-shredding.md`
- `docs/guides/disaster-recovery.md`
- `docs/reference/problems/index.md`
- `docs/reference/problems/*protected*.md`
- `tests/Hexalith.EventStore.Contracts.Tests/Security/*`
- `tests/Hexalith.EventStore.Server.Tests/Events/*`
- `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprBackupCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/*`
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/*Backup*`
- `tests/Hexalith.EventStore.Testing.Tests/*`

## Out of Scope

- Real encryption provider implementation, key wrapping, key vault integration, cloud KMS integration, local certificate management, DAPR secret-store setup, or provider-specific crypto algorithms.
- Physical backup engine implementation unless ST0 records an explicit approved architecture change.
- Deleting immutable event records, snapshots, or audit records as the crypto-shredding mechanism.
- Legal hold policy, data-subject request UX, approval workflow UI, or jurisdiction-specific compliance automation.
- Broad redaction coverage across all admin UI, CLI, MCP, ProblemDetails, replay/rebuild diagnostics, backup validation, and test artifacts beyond surfaces touched by this story.
- Skip-past-shredded replay or projection rebuild behavior without a future explicit product and architecture decision.
- Changing domain service contracts so domain services own protection metadata, key lifecycle, or restore-safety decisions.

## References

- `_bmad-output/planning-artifacts/epics.md` - Story 22.7c requirements and Epic 22 story split.
- `_bmad-output/planning-artifacts/prd.md` - FR102-FR104 and NFR12.
- `_bmad-output/planning-artifacts/architecture.md` - Payload and Snapshot Protection; SEC-1, SEC-5; Publishing, Replay & Protection Contracts.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md` - payload-protection backlog and docs expectations.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-parties-integration-contract-gaps.md` - original 22.7 payload protection and crypto-shredding scope.
- `_bmad-output/project-context.md` - package boundaries, logging, DAPR, actor state, and testing rules.
- `_bmad-output/implementation-artifacts/22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints.md` - replay/read API and rebuild checkpoint semantics.
- `_bmad-output/implementation-artifacts/22-7a-payload-and-snapshot-protection-hooks.md` - protection metadata prerequisite and 22.7c deferrals.
- `_bmad-output/implementation-artifacts/22-7b-unreadable-protected-data-behavior.md` - unreadable protected-data taxonomy dependency and restored-backup deferrals.
- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`
- `src/Hexalith.EventStore.Contracts/Security/PayloadProtectionResult.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupQueryService.cs`

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
- Party-mode review completed on 2026-05-14 and blocked runtime development until Story 22.7a provider-neutral protection metadata/result parity is implemented and merged.

## Party-Mode Review

- Date/time: 2026-05-14T10:29:00+02:00
- Selected story key: `22-7c-crypto-shredding-workflow-and-restored-backup-safety`
- Command/skill invocation used: `/bmad-party-mode 22-7c-crypto-shredding-workflow-and-restored-backup-safety; review;`
- Participating BMAD agents: John (Product Manager), Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary:
    - Story value is sound, but runtime implementation is blocked until Story 22.7a provider-neutral event/snapshot metadata and typed result parity are merged.
    - Story 22.7b unreadable taxonomy remains a dependent contract; any ST0 fallback must be temporary and reconciled later.
    - Restore admission/status needs a named boundary and must stay honest about deferred physical backup execution.
    - Runtime paths need one EventStore-owned decision boundary rather than duplicated policy in server, admin, CLI, and MCP code.
    - Operator-facing restore conflict output, cancellation semantics, idempotency, path-specific tests, and no-leak assertions needed sharper observable requirements.
- Changes applied:
    - Marked the story blocked by the 22.7a metadata/result prerequisite.
    - Added party-mode hardening decisions for implementation gating, single decision boundary, canonical decision/status shape, restore admission limits, operator-facing output, localization, and no-leak scope.
    - Tightened acceptance criteria for idempotency, cancellation-before/after decision freeze, path-specific runtime outcomes, restore admission honesty, deferred backup claims, and provider-opaque/unknown compatibility.
    - Added ST0/ST1/ST2/ST3/ST5 guardrails for shared decision contracts, provider-neutral DTOs, touched-surface tests, deferred validation, and path-specific negative cases.
    - Expanded test evidence expectations for runtime paths, state-machine behavior, restore admission, and no-leak evidence.
- Findings deferred:
    - Human architecture/product decision may later narrow this story to contract/documentation-only preparation before 22.7a lands.
    - Story 22.7b final unreadable taxonomy reconciliation remains deferred until 22.7b is unblocked.
    - Real crypto/KMS deletion, physical backup engine, legal hold UX, full redaction coverage, and skip-past-shredded replay remain out of scope.
- Final recommendation: `blocked`

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-14 | Party-mode review blocked Story 22.7c until 22.7a metadata/result parity is implemented and added decision-boundary, restore-admission, operator-output, cancellation, idempotency, path-specific testing, and no-leak guardrails. |
| 2026-05-13 | Story created from Epic 22.7c with provider-neutral crypto-shredding workflow, restore-safety admission, audit/idempotency, deferred backup-engine honesty, no-silent-readability, and no-leak evidence requirements. |
