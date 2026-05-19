# Story 22.7d-4: Protected Data Redaction in Replay, Rebuild, Backup Validation, and Tests

Status: ready-for-dev

Context created: 2026-05-19
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR104 protected-data redaction for replay/read APIs, aggregate replay, projection rebuild/checkpoint progress, backup validation/restore-admission/deferred backup paths, operational evidence artifacts, docs/examples, and no-leak test infrastructure. This child story intentionally excludes runtime logs/ProblemDetails completed by 22.7d-1, Admin API/Web UI rendering completed by 22.7d-2, and CLI/MCP output staged by 22.7d-3 except where this story provides shared evidence or recovery-status guardrails those surfaces consume.

Prerequisites verified from `sprint-status.yaml`: 22.7a, 22.7b, 22.7c, 22.7d-1, and 22.7d-2 are done; 22.7d-3 is already `ready-for-dev`; parent 22.7d remains an unassignable backlog container. Reuse the completed provider-neutral protection metadata, unreadable taxonomy, restored-backup admission status, `ProtectedDataDiagnosticRedactor`, `AdminRedactedContent`, and `ProtectedDataLeakSentinel`. Do not create a second redaction taxonomy.

## Story

As a platform owner,
I want replay, rebuild, backup validation, and test artifacts to preserve redaction guarantees,
so that operational recovery cannot bypass protection boundaries.

## Acceptance Criteria

1. **Replay/read and aggregate reconstruction do not leak protected content.**
   - Given public stream read/replay, aggregate reconstruction, event replay tests, or downstream replay documentation touches protected payloads, protected snapshots, unreadable protected data, provider-opaque metadata, crypto-shredding statuses, or restored-backup conflicts
   - When `StreamsController`, `EventStoreGatewayClient` stream APIs, `AggregateReplayer`, replay controllers, sample replay tests, docs examples, or generated API references emit responses, diagnostics, test snapshots, or evidence
   - Then protected payload bytes, snapshot state JSON, reconstructed state JSON, provider-private metadata, unsafe key aliases, state-store keys, connection strings, provider exception text, stack traces, and test assertion sentinels are absent.
   - And safe recovery metadata remains present: tenant, domain, aggregateId, sequenceNumber, eventTypeName, correlationId, causationId, protection state, metadataVersion, reasonCode, stage, retryable/permanent flags, and safe next action where available.
   - And replay fails closed on unreadable protected content; it must not skip past unreadable events, reconstruct state from stale snapshots, or imply plaintext is available unless a future approved skip policy explicitly permits it.

2. **Projection rebuild and checkpoint progress are redaction-safe.**
   - Given projection delivery, operator-triggered rebuild, replay/reset/pause/resume/cancel/retry, checkpoint persistence, active-index cleanup, projection apply failures, or rebuild status polling touches protected content
   - When `ProjectionUpdateOrchestrator`, `ProjectionRebuildCheckpointStore`, `AdminProjectionRebuildController`, public `ProjectionRebuildOperation`/`ProjectionRebuildCheckpoint` DTOs, logs, tests, or evidence artifacts expose progress or failure details
   - Then only safe identifiers, operation IDs, checkpoint positions, lifecycle status, stable reason codes, retry guidance, and redacted placeholders are present.
   - And `FailureReasonCode`, operation messages, checkpoint rows, active-index diagnostics, and terminal status responses never store or serialize raw projection payloads, protected event payloads, snapshot state, provider exception text, domain-service response bodies, state-store keys, or unsafe metadata dictionaries.
   - And unreadable protected content blocks rebuild advancement with a stable reason code and auditable checkpoint/status evidence instead of advancing `LastAppliedSequence` past data that was not safely read.

3. **Backup validation, restore admission, import/export, and deferred backup paths remain honest and safe.**
   - Given backup trigger, backup validation, restore trigger, restored-backup admission, crypto-shredding workflow, stream export/import, and deferred backup operations touch protected content or protected-data status
   - When `DaprBackupCommandService`, backup controllers, backup query services, Admin UI/CLI/MCP callers, docs, or tests emit results
   - Then every response is redaction-safe by construction and carries only stable operation IDs, tenant/domain/aggregate scope, sequence ranges, protection metadata version, safe key-reference policy/fingerprint fields, reason codes, audit IDs, and safe next action.
   - And deferred paths stay honest: backup validation, physical backup scanning, provider verification, and restore-serving capability are not claimed until a real backup engine/manifest scanner exists.
   - And `ex.Message`, raw HTTP response bodies, backup manifest content, stream export/import content, provider-private metadata, state-store keys, connection strings, and plaintext payload/state values cannot flow into `AdminOperationResult.Message`, error codes, audit records, or evidence artifacts.

4. **Evidence artifacts, docs, generated examples, and test output are scanned with protected-data sentinels.**
   - Given focused tests, operational evidence fixtures, docs examples, OpenAPI/generated API docs, markdown reports, Dev Agent Records, command output captures, and snapshot/assertion files exercise protected-data paths
   - When artifacts are created or validated
   - Then `ProtectedDataLeakSentinel.AssertNoLeak(...)` or an equivalent centralized scanner checks every captured string and file content that could carry protected payload, snapshot state, key alias, provider metadata, provider exception text, state-store keys, connection strings, raw secret markers, or assertion-message sentinels.
   - And scans include nested JSON, stringified JSON, XML/markdown/code fences, table cells, ProblemDetails extension values, activity/log captures, stdout/stderr captures, output files, and failed assertion messages.
   - And tests prove safe metadata remains present so the no-leak guard is not satisfied by blanking or deleting useful diagnostic context.

5. **Recovery no-leak validation is broad enough to prevent regressions across completed 22.7d surfaces.**
   - Given 22.7d-1 and 22.7d-2 are done and 22.7d-3 is ready for dev
   - When this story adds replay/rebuild/backup/evidence guardrails
   - Then it reuses completed runtime/Admin/CLI/MCP contracts instead of duplicating policy.
   - And focused regression tests cover runtime ProblemDetails/log safe fields, Admin redacted DTOs, pending CLI/MCP output chokepoints where available, and operational evidence scans.
   - And the story records exact validation commands and any pre-existing or out-of-scope failures before moving to review.

## Tasks / Subtasks

- [ ] **ST0 - Freeze the recovery redaction inventory and evidence policy.** (AC: 1, 2, 3, 4, 5)
  - [ ] Read this story, parent Story 22.7d, Stories 22.7a/22.7b/22.7c/22.7d-1/22.7d-2/22.7d-3, Epic 22, PRD FR104/NFR12/NFR21-NFR26, architecture ADR-P9/Payload and Snapshot Protection/Pub/Sub and Replay Guarantee Matrix/Admin Data Access, `_bmad-output/project-context.md`, and deferred-work items grouped under Story 22.6 before code edits.
  - [ ] Confirm the single policy boundary for replay/rebuild/backup/evidence outputs. Expected shape: reuse `ProtectedDataDiagnosticRedactor`, `AdminRedactedContent`, `ProtectedDataReadabilityDecision`, `RestoredBackupAdmissionResult`, and `ProtectedDataLeakSentinel`; add only narrow helpers where an existing layer cannot reference them without violating dependency direction.
  - [ ] Inventory replay/read chokepoints: `StreamsController.ReadStreamAsync`, unreadable ProblemDetails, `StreamReadEvent.Payload`, `EventStoreGatewayClient` stream reads, `AggregateReplayer`, `ReplayController`, `ReplayCommandResponse`, integration replay tests, docs examples, and generated API docs.
  - [ ] Inventory rebuild/checkpoint chokepoints: `ProjectionUpdateOrchestrator`, `ProjectionRebuildCheckpointStore`, `AdminProjectionRebuildController`, `ProjectionRebuildOperation`, `ProjectionRebuildCheckpoint`, `StreamReplayReasonCodes`, active-index cleanup logs, projection apply response handling, and Admin projection status consumers.
  - [ ] Inventory backup/recovery chokepoints: `DaprBackupCommandService`, `DaprBackupQueryService`, `AdminBackupsController`, backup/restore admission DTOs, crypto-shredding workflow audit records, stream export/import results, deferred operation messages, and backup docs/examples.
  - [ ] Inventory artifact chokepoints: `ProtectedDataLeakSentinel`, `FakeUnreadableProtectionService`, stream read builders, restored-backup builders, OperationalEvidence validator fixtures/tests, docs scans, Dev Agent Record text, command capture files, generated API docs, and any snapshot/assertion artifacts.
  - [ ] Classify every raw-capable field or message as safe by construction, replaced by typed redacted descriptor/status, retained only for internal immutable/replay storage, or forbidden from recovery/evidence output.
  - [ ] Record the inventory and decisions in the Dev Agent Record as a mini ADR, including compatibility impact for public stream/rebuild/backup DTO shape.

- [ ] **ST1 - Harden replay/read and aggregate reconstruction artifacts.** (AC: 1, 4, 5)
  - [ ] Add red-phase tests that inject `ProtectedDataLeakSentinel` values into event payload bytes, snapshot plaintext, provider metadata, provider exception text, and assertion messages and show at least one current replay/evidence capture would leak without the new guard.
  - [ ] Prove `StreamsController` unreadable protected-data responses preserve safe ProblemDetails fields and do not include payload bytes, provider text, key aliases, stack traces, or unsafe metadata.
  - [ ] Prove readable protected-data paths do not write plaintext payload/snapshot values into test output, docs examples, generated API examples, failure messages, or evidence artifacts.
  - [ ] Add or update `StreamReadPageBuilder`, `FakeEventStreamReader`, `FakeUnreadableProtectionService`, or test helpers so replay tests can create protected/unreadable stream pages without hand-rolling sentinels in each test.
  - [ ] Add aggregate replay tests for `AggregateReplayer` and sample replay paths that scan reconstruction results, timeline entries, exception messages, and assertion output.
  - [ ] Verify cancellation remains cancellation and is not converted into a protected-data failure.

- [ ] **ST2 - Harden projection rebuild/checkpoint progress artifacts.** (AC: 2, 4, 5)
  - [ ] Add tests proving unreadable protected events in `ProjectionUpdateOrchestrator` stop rebuild advancement, write a stable failure reason code, and do not advance `LastAppliedSequence` past the unreadable sequence.
  - [ ] Add tests for `ProjectionRebuildCheckpointStore` and `AdminProjectionRebuildController` that scan `ProjectionRebuildOperation`, `ProjectionRebuildCheckpoint`, `AdminOperationResult`, and ProblemDetails output for sentinels while retaining safe status and operation metadata.
  - [ ] Audit projection apply HTTP response handling. If a domain service response body or exception text can include protected sentinel values, ensure only status/reason code/content-type/status-code safe metadata reaches logs, status, or artifact output.
  - [ ] Ensure active-index cleanup, cancel cleanup, retry terminal snapshot reads, checkpoint conflict messages, and transient store failure paths do not copy state-store keys or exception messages into externally visible fields.
  - [ ] Add regression coverage for deferred Story 22.6 risks that matter to no-leak evidence: exact `RebuildPageSize` page boundary, continuation-token fail-closed text, operation-id malformed rows, and no raw state-store keys in diagnostics.

- [ ] **ST3 - Harden backup validation, restore admission, and deferred recovery paths.** (AC: 3, 4, 5)
  - [ ] Add tests for `DaprBackupCommandService` deferred trigger/validate/restore/export/import results proving deferred messages are honest, no-leak, and do not claim physical backup scanning exists.
  - [ ] Audit `InvokeEventStorePostAsync`: protected-data-capable failures must not return raw `ex.Message` as `AdminOperationResult.Message`; route through a safe message/status helper where needed.
  - [ ] Add restored-backup admission tests proving `RestoredBackupAdmissionResult`, audit events, idempotent replay results, denied/deferred/accepted decisions, and invalid transition errors do not leak sentinels while retaining safe key-reference policy/fingerprint fields.
  - [ ] Add crypto-shredding workflow/audit tests for safe workflow IDs, audit IDs, reason codes, state transitions, and next actions without key aliases, key material, provider metadata, or plaintext.
  - [ ] Add stream export/import no-leak tests and keep import/export deferred unless an approved bounded export/import contract exists.

- [ ] **ST4 - Extend evidence/doc/test artifact scanning.** (AC: 4, 5)
  - [ ] Extend `ProtectedDataLeakSentinel` or add a focused artifact scanner that can scan files/directories and structured values without printing sentinel values in assertion messages.
  - [ ] Extend OperationalEvidence validator redaction rules or add protected-data-specific rule IDs for protected payload plaintext, protected snapshot plaintext, key alias, provider-private blob, provider exception text, and state-store key sentinels.
  - [ ] Unskip or supplement `Dw4RedactionRulesAtddTests` only where this story owns the implementation; do not destabilize unrelated evidence schemas without focused scope.
  - [ ] Add fixtures proving sentinel leakage fails and documented synthetic redaction markers pass.
  - [ ] Scan docs touched by 22.7a-d, generated API references for replay/rebuild/security/problem types, Dev Agent Records, and markdown evidence fixtures. Examples must use placeholders and safe reason/status fields, not fake plaintext or raw key aliases.
  - [ ] Ensure failed no-leak assertions report sentinel indexes/categories without echoing the sentinel value itself.

- [ ] **ST5 - Documentation and validation.** (AC: 1, 2, 3, 4, 5)
  - [ ] Update `docs/guides/payload-protection-and-crypto-shredding.md` with a recovery/evidence redaction matrix for replay, rebuild, backup validation, restore admission, docs, and tests.
  - [ ] Update `docs/reference/stream-replay-api.md`, `docs/guides/disaster-recovery.md`, protected-data problem references, and generated API docs only where the contract or examples change.
  - [ ] Run focused tests individually and record exact commands/results in the Dev Agent Record.
  - [ ] Minimum validation:
    - `dotnet test tests/Hexalith.EventStore.Contracts.Tests --filter "FullyQualifiedName~Security|FullyQualifiedName~Streams|FullyQualifiedName~Replay|FullyQualifiedName~Problems" --no-restore`
    - `dotnet test tests/Hexalith.EventStore.Testing.Tests --filter "ProtectedDataLeakSentinel|FakeUnreadableProtectionService|StreamReadPageBuilder|RestoredBackupAdmissionBuilder" --no-restore`
    - `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~StreamsControllerTests|FullyQualifiedName~ReplayControllerTests|FullyQualifiedName~AdminProjectionRebuildControllerTests|FullyQualifiedName~ProjectionUpdateOrchestrator|FullyQualifiedName~ProjectionRebuildCheckpointStore|FullyQualifiedName~ProtectedDataDiagnosticRedactionTests|FullyQualifiedName~UnreadableProtectedDataBehaviorTests" --no-restore`
    - `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --filter "FullyQualifiedName~AdminBackupsController|FullyQualifiedName~DaprBackupCommandService|FullyQualifiedName~DaprBackupQueryService|FullyQualifiedName~AdminStreamsController|FullyQualifiedName~DaprStreamQueryService" --no-restore`
    - `dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests --filter "AdminRedactedContentSerializationTests|Streams|Storage" --no-restore`
    - `dotnet test tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests --filter "FullyQualifiedName~Redaction|FullyQualifiedName~ProtectedData|FullyQualifiedName~DocsValidation" --no-restore`
    - `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests --filter "FullyQualifiedName~Stream|FullyQualifiedName~Backup|FullyQualifiedName~Projection|FullyQualifiedName~Formatting" --no-restore` if 22.7d-3 has landed or shared CLI output helpers are touched.
    - `dotnet test tests/Hexalith.EventStore.Admin.Mcp.Tests --filter "FullyQualifiedName~ToolHelper|FullyQualifiedName~StreamTools|FullyQualifiedName~BackupWriteTools|FullyQualifiedName~ProjectionWriteTools" --no-restore` if 22.7d-3 has landed or shared MCP helpers are touched.
  - [ ] Run `dotnet build Hexalith.EventStore.slnx --configuration Debug --no-restore` if shared contracts, generated docs, project references, or evidence validator code changes.
  - [ ] Use Aspire/browser verification only if this story changes runtime endpoints or UI behavior. For story creation, Aspire MCP baseline showed no active AppHost and environment diagnostics passed with warnings only for multiple HTTPS dev certificates and deprecated Claude Code MCP config.

## Dev Notes

### Current Implementation Snapshot

- `StreamsController.ReadStreamAsync` already checks replay readability through `IEventPayloadProtectionService.TryUnprotectEventPayloadAsync` and returns `UnreadableProtectedDataProblem` via `ProtectedDataDiagnosticRedactor.BuildUnreadableProblemExtensions(...)` when a protected event is unreadable. It still returns `StreamReadEvent.Payload` for readable events, so tests and docs must avoid capturing sentinel plaintext from intentionally readable fixtures.
- `StreamReadEvent` in `src/Hexalith.EventStore.Contracts/Streams/StreamReadEvent.cs` contains `byte[] Payload` plus `ProtectionMetadata`. This is a supported replay contract, not a diagnostic field. Redaction must govern artifacts and operational surfaces, not mutate immutable event-store semantics.
- `ProjectionUpdateOrchestrator` already maps unreadable protected events to `UnreadableProtectedDataReasonCodes.From(...)`, writes failed checkpoint/status via `ResetAsync`, and logs safe identifiers/reason/stage. Story 22.7d-4 must prove this path with sentinel tests and checkpoint assertions.
- `ProjectionRebuildCheckpoint` and `ProjectionRebuildOperation` carry `FailureReasonCode`, lifecycle status, operation ID, checkpoint positions, and timestamps. They should remain reason-code/status surfaces; do not add raw failure text fields to them.
- `AdminProjectionRebuildController` maps checkpoint save failures and terminal rebuild failures to ProblemDetails with stable reason codes. Existing messages include operation IDs and reason codes. Ensure no new path uses provider/domain response bodies or raw exception text.
- `DaprBackupCommandService` intentionally returns deferred backup/restore/import/export results while there is no approved backup engine/manifest scanner. Keep this honesty: validation is deferred unless actual runtime capability lands.
- `DaprBackupCommandService.InvokeEventStorePostAsync` currently catches generic exceptions and returns `ex.Message` in `AdminOperationResult.Message`. Treat this as a redaction-sensitive risk if any protected-data-capable EventStore invocation is routed through it.
- Restored-backup admission and crypto-shredding workflow DTOs are safe status/audit records by design, but they still include key-reference policy/fingerprint and watermarks. Tests must prove those are safe fingerprints/statuses, not aliases, key material, or provider-private blobs.
- `ProtectedDataLeakSentinel` currently covers payload plaintext, snapshot plaintext, key alias, provider-private blob, state-store key, connection string, and provider exception text. It intentionally reports sentinel index rather than echoing the value.
- OperationalEvidence redaction tests currently cover bearer tokens, connection-string keywords, production hostnames, raw secret markers, and missing redaction sections. Several ATDDs are still skipped red-phase scaffolds; this story may either implement the protected-data subset or add a focused scanner without over-expanding DW4.

### Previous Story Intelligence

- 22.7d-1 established `ProtectedDataDiagnosticRedactor` and proved logs, activity status/events, command status/publish failure, dead-letter diagnostics, and ProblemDetails do not leak sentinels. Reuse its deterministic safe fallback: `Protected data diagnostic details were redacted. ReasonCode=<reason-code>; Stage=<stage>.`
- 22.7d-2 established `AdminRedactedContent`, protected Admin DTO factories, safe Admin API ProblemDetails propagation, and UI rendering/copy/export no-leak tests. Recovery/admin status surfaces should use those descriptors when they leave runtime DTOs for Admin-facing responses.
- 22.7d-3 is `ready-for-dev`, not done. Do not assume CLI/MCP output is hardened yet. If this story touches CLI/MCP validation, gate those checks on 22.7d-3 implementation state or keep them as explicit follow-up evidence.
- Story 22.6 left several replay/rebuild deferred risks in `deferred-work.md`: exact `RebuildPageSize`-multiple completion behavior, continuation-token request-binding, `RebuildSchedulerActor`, HMAC-signed continuation tokens, operation ID migration/hardening, and Tier-3 Aspire integration proof. This story should not solve all of them, but no-leak evidence must not pretend those risks are closed.

### Architecture Compliance

- Preserve append-only event-store semantics. Do not delete, mutate, or redact immutable persisted event payloads as the mechanism for operational redaction.
- Preserve EventStore ownership of envelope metadata; domain services must not own redaction, key lifecycle, unreadable status, or restore-safety decisions.
- CLI/MCP/Admin clients remain thin clients over Admin API. They must not access DAPR, state-store keys, provider-private metadata, or actor state directly.
- DAPR actor state must continue through `IActorStateManager`; do not bypass aggregate/projection actor isolation to inspect protected content.
- Backup validation remains deferred until an approved backup engine, manifest model, scanning contract, restore namespace, and audit model exist.
- Do not add real encryption providers, KMS, Key Vault, DAPR secret-store integration, certificates, physical backup scanners, or provider-specific crypto dependencies.
- Use centralized package management. Do not add package versions directly to project files.

### Latest Technical Information

- Microsoft Learn ASP.NET Core error-handling guidance for .NET 10 still warns against serving sensitive error information to clients and supports ProblemDetails via `AddProblemDetails`, exception-handler middleware, and status-code pages. Keep protected-data recovery responses on stable ProblemDetails/status DTOs, not raw exception messages.
- Microsoft Learn `System.Text.Json` guidance recommends reusing `JsonSerializerOptions` instances because options metadata caches are thread-safe after first use. If artifact scanners or converters serialize protected DTOs, register options centrally instead of creating ad hoc options per call.
- Current Dapr docs describe actor state through the sidecar/actor runtime and state stores with actor state-store metadata and ETag-capable concurrency. Keep rebuild/checkpoint tests aligned with DAPR sidecar/state-store abstractions instead of backend-specific direct reads.
- Current Aspire docs describe AppHost as the local distributed app model and `aspire run` as the development orchestrator that builds/starts resources and dashboard endpoints. Runtime changes should be validated with Aspire when they alter endpoint behavior; story-only changes do not require keeping an AppHost running.

## Out of Scope

- Runtime logs, telemetry, command-status, dead-letter diagnostic text, and core ProblemDetails redaction already completed by 22.7d-1 except where this story adds recovery-specific regression evidence.
- Admin API/Web UI protected-data rendering and copy/export behavior already completed by 22.7d-2 except where backup/rebuild/replay recovery surfaces require additional safe status descriptors.
- CLI and MCP output redaction is 22.7d-3. This story may consume its helpers after they land but must not silently expand into full CLI/MCP implementation.
- Implementing a real backup engine, physical backup scanner, backup manifest parser, crypto provider verification, skip-past-unreadable replay/rebuild policy, KMS/Key Vault/DAPR secret-store integration, or deletion of immutable event/snapshot/audit records.
- Solving all Story 22.6 deferred rebuild architecture items such as `RebuildSchedulerActor` or signed continuation tokens unless a focused no-leak regression requires a small guard.

## References

- `_bmad-output/planning-artifacts/epics.md` - Story 22.7d-4 source requirements and Epic 22 split.
- `_bmad-output/planning-artifacts/prd.md` - FR104 protected-data no-leak requirement, replay/rebuild reliability, and backup/operator expectations.
- `_bmad-output/planning-artifacts/architecture.md` - ADR-P9, Payload and Snapshot Protection, Pub/Sub and Replay Guarantee Matrix, Admin Data Access, and testing standards.
- `_bmad-output/planning-artifacts/ux-design-specification.md` - recovery/replay progress guidance and multi-modal consistency.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12.md` - split of Story 22.7 into 22.7a-22.7d.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-17-readiness-addendum.md` - child story rows 22.7d-1 through 22.7d-4.
- `_bmad-output/implementation-artifacts/22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints.md` - replay/read API and projection rebuild checkpoint implementation context.
- `_bmad-output/implementation-artifacts/deferred-work.md` - open replay/rebuild deferred risks and evidence gaps.
- `_bmad-output/implementation-artifacts/22-7d-protected-data-redaction-across-operational-surfaces.md` - parent container and surface inventory.
- `_bmad-output/implementation-artifacts/22-7d-1-protected-data-redaction-in-logs-and-problemdetails.md` - completed runtime diagnostic redactor and no-leak evidence.
- `_bmad-output/implementation-artifacts/22-7d-2-protected-data-redaction-in-admin-api-and-web-ui.md` - completed `AdminRedactedContent`, Admin DTO, and UI redaction contract.
- `_bmad-output/implementation-artifacts/22-7d-3-protected-data-redaction-in-cli-and-mcp.md` - pending CLI/MCP redaction story and output chokepoint inventory.
- `_bmad-output/implementation-artifacts/22-7a-payload-and-snapshot-protection-hooks.md` - protection metadata contract.
- `_bmad-output/implementation-artifacts/22-7b-unreadable-protected-data-behavior.md` - unreadable taxonomy and sentinel helper.
- `_bmad-output/implementation-artifacts/22-7c-crypto-shredding-workflow-and-restored-backup-safety.md` - readability decisions, crypto-shredding workflow, and restore admission ProblemDetails.
- `_bmad-output/project-context.md` - package boundaries, DAPR, ProblemDetails, logging, testing, and submodule rules.
- `src/Hexalith.EventStore/Controllers/StreamsController.cs`
- `src/Hexalith.EventStore/Controllers/ReplayController.cs`
- `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs`
- `src/Hexalith.EventStore.Testing/Security/ProtectedDataLeakSentinel.cs`
- Microsoft Learn: [Handle errors in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0) and [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0).
- Microsoft Learn: [How to instantiate JsonSerializerOptions instances with System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/configure-options).
- Dapr docs: [Actors overview](https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/), [State management overview](https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/), and [State management API reference](https://docs.dapr.io/reference/api/state_api/).
- Microsoft Learn: [.NET Aspire AppHost overview](https://learn.microsoft.com/dotnet/aspire/fundamentals/app-host-overview) and [aspire run command](https://learn.microsoft.com/dotnet/aspire/cli-reference/aspire-run).

## Dev Agent Record

### Agent Model Used

TBD by dev agent.

### Debug Log References

- Story creation baseline: Aspire MCP `list_apphosts` reported no active AppHost in `D:\Hexalith.EventStore`. Aspire `doctor` passed .NET SDK 10.0.300 and Docker 29.4.3, with warnings for multiple HTTPS development certificates and deprecated Claude Code MCP config. No AppHost was kept running because this turn only creates the story artifact and sprint-status transition.

### Completion Notes List

- Story context created on 2026-05-19 by BMad create-story workflow.
- No implementation tests were run because this is story creation only.

### File List

- `_bmad-output/implementation-artifacts/22-7d-4-protected-data-redaction-in-replay-rebuild-backup-validation-and-tests.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Verification Status

- Story context created and set to `ready-for-dev`.
- Prerequisites verified from `sprint-status.yaml`: 22.7a, 22.7b, 22.7c, 22.7d-1, and 22.7d-2 are done; 22.7d-3 is already ready-for-dev; parent 22.7d remains an unassignable container row.
- No implementation tests were run because this is story creation only.

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-19 | Story created for child 22.7d-4 with focused replay/rebuild/backup-validation/test-artifact redaction scope, recovery chokepoint inventory, no-leak evidence requirements, and source references. |
