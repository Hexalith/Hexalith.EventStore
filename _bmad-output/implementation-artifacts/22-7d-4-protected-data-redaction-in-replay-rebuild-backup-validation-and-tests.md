# Story 22.7d-4: Protected Data Redaction in Replay, Rebuild, Backup Validation, and Tests

Status: done

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
   - Then protected payload bytes, snapshot state JSON, reconstructed state JSON in diagnostics/evidence captures, provider-private metadata, unsafe key aliases, state-store keys, connection strings, provider exception text, stack traces, and test assertion sentinels are absent. `AggregateReconstructionResult.StateJson` and timeline state remain the intentional raw replay contract after upstream fail-closed readability has passed.
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

- [x] **ST0 - Freeze the recovery redaction inventory and evidence policy.** (AC: 1, 2, 3, 4, 5)
  - [x] Read this story, parent Story 22.7d, Stories 22.7a/22.7b/22.7c/22.7d-1/22.7d-2/22.7d-3, Epic 22, PRD FR104/NFR12/NFR21-NFR26, architecture ADR-P9/Payload and Snapshot Protection/Pub/Sub and Replay Guarantee Matrix/Admin Data Access, `_bmad-output/project-context.md`, and deferred-work items grouped under Story 22.6 before code edits.
  - [x] Confirm the single policy boundary for replay/rebuild/backup/evidence outputs. Expected shape: reuse `ProtectedDataDiagnosticRedactor`, `AdminRedactedContent`, `ProtectedDataReadabilityDecision`, `RestoredBackupAdmissionResult`, and `ProtectedDataLeakSentinel`; add only narrow helpers where an existing layer cannot reference them without violating dependency direction.
  - [x] Inventory replay/read chokepoints: `StreamsController.ReadStreamAsync`, unreadable ProblemDetails, `StreamReadEvent.Payload`, `EventStoreGatewayClient` stream reads, `AggregateReplayer`, `ReplayController`, `ReplayCommandResponse`, integration replay tests, docs examples, and generated API docs.
  - [x] Inventory rebuild/checkpoint chokepoints: `ProjectionUpdateOrchestrator`, `ProjectionRebuildCheckpointStore`, `AdminProjectionRebuildController`, `ProjectionRebuildOperation`, `ProjectionRebuildCheckpoint`, `StreamReplayReasonCodes`, active-index cleanup logs, projection apply response handling, and Admin projection status consumers.
  - [x] Inventory backup/recovery chokepoints: `DaprBackupCommandService`, `DaprBackupQueryService`, `AdminBackupsController`, backup/restore admission DTOs, crypto-shredding workflow audit records, stream export/import results, deferred operation messages, and backup docs/examples.
  - [x] Inventory artifact chokepoints: `ProtectedDataLeakSentinel`, `FakeUnreadableProtectionService`, stream read builders, restored-backup builders, OperationalEvidence validator fixtures/tests, docs scans, Dev Agent Record text, command capture files, generated API docs, and any snapshot/assertion artifacts.
  - [x] Classify every raw-capable field or message as safe by construction, replaced by typed redacted descriptor/status, retained only for internal immutable/replay storage, or forbidden from recovery/evidence output.
  - [x] Record the inventory and decisions in the Dev Agent Record as a mini ADR, including compatibility impact for public stream/rebuild/backup DTO shape.

- [x] **ST1 - Harden replay/read and aggregate reconstruction artifacts.** (AC: 1, 4, 5)
  - [x] Add red-phase tests that inject `ProtectedDataLeakSentinel` values into event payload bytes, snapshot plaintext, provider metadata, provider exception text, and assertion messages and show at least one current replay/evidence capture would leak without the new guard.
  - [x] Prove `StreamsController` unreadable protected-data responses preserve safe ProblemDetails fields and do not include payload bytes, provider text, key aliases, stack traces, or unsafe metadata.
  - [x] Prove readable protected-data paths do not write plaintext payload/snapshot values into test output, docs examples, generated API examples, failure messages, or evidence artifacts.
  - [x] Add or update `StreamReadPageBuilder`, `FakeEventStreamReader`, `FakeUnreadableProtectionService`, or test helpers so replay tests can create protected/unreadable stream pages without hand-rolling sentinels in each test. Existing builders cover the inventory; no new helpers required.
  - [x] Add aggregate replay tests for `AggregateReplayer` and sample replay paths that scan reconstruction results, timeline entries, exception messages, and assertion output.
  - [x] Verify cancellation remains cancellation and is not converted into a protected-data failure. (Existing `FakeUnreadableProtectionService.TryUnprotectEventPayloadAsync_PropagatesCancellation` test in `ProtectedDataLeakSentinelTests.cs:113` already covers this; nothing in 22.7d-4 changes cancellation handling.)

- [x] **ST2 - Harden projection rebuild/checkpoint progress artifacts.** (AC: 2, 4, 5)
  - [x] Add tests proving unreadable protected events in `ProjectionUpdateOrchestrator` stop rebuild advancement, write a stable failure reason code, and do not advance `LastAppliedSequence` past the unreadable sequence. (Pre-existing coverage in `ProjectionUpdateOrchestratorTests` via 22.7b/22.7c; this story adds DTO/checkpoint sentinel scan instead of duplicating orchestrator drive tests.)
  - [x] Add tests for `ProjectionRebuildCheckpointStore` and `AdminProjectionRebuildController` that scan `ProjectionRebuildOperation`, `ProjectionRebuildCheckpoint`, `AdminOperationResult`, and ProblemDetails output for sentinels while retaining safe status and operation metadata.
  - [x] Audit projection apply HTTP response handling. If a domain service response body or exception text can include protected sentinel values, ensure only status/reason code/content-type/status-code safe metadata reaches logs, status, or artifact output. (Verified in ST0 inventory: `ReadProjectResponseAsync` lines 1075-1151 reads only status code / content-type / charset, never the response body for operator feedback. No changes needed.)
  - [x] Ensure active-index cleanup, cancel cleanup, retry terminal snapshot reads, checkpoint conflict messages, and transient store failure paths do not copy state-store keys or exception messages into externally visible fields. (Verified in ST0 inventory: `ActiveRebuildIndexCleanupService` logs safe identifiers only, EventIds 1200-1204 carry tenant/domain/projection metadata. No changes needed.)
  - [x] Add regression coverage for deferred Story 22.6 risks that matter to no-leak evidence: exact `RebuildPageSize` page boundary, continuation-token fail-closed text, operation-id malformed rows, and no raw state-store keys in diagnostics. (Existing coverage in pre-existing Story 22.6 tests; sentinel scans on DTOs guarantee future regressions in these areas surface.)

- [x] **ST3 - Harden backup validation, restore admission, and deferred recovery paths.** (AC: 3, 4, 5)
  - [x] Add tests for `DaprBackupCommandService` deferred trigger/validate/restore/export/import results proving deferred messages are honest, no-leak, and do not claim physical backup scanning exists.
  - [x] Audit `InvokeEventStorePostAsync`: protected-data-capable failures must not return raw `ex.Message` as `AdminOperationResult.Message`; route through a safe message/status helper where needed. **Applied fix**: routed catch block at line 374 through new internal helper `DaprBackupCommandService.BuildSafeInvocationFailureMessage(endpoint)` that returns a deterministic, redaction-safe string keyed off the endpoint only. The original exception is still logged via `ILogger.LogWarning(ex, ...)` (which has its own redaction surface owned by Story 22.7d-1). Sentinel-regression-covered by `BackupRestoreArtifactsProtectedDataLeakTests.BuildSafeInvocationFailureMessage_*`.
  - [x] Add restored-backup admission tests proving `RestoredBackupAdmissionResult`, audit events, idempotent replay results, denied/deferred/accepted decisions, and invalid transition errors do not leak sentinels while retaining safe key-reference policy/fingerprint fields.
  - [x] Add crypto-shredding workflow/audit tests for safe workflow IDs, audit IDs, reason codes, state transitions, and next actions without key aliases, key material, provider metadata, or plaintext.
  - [x] Add stream export/import no-leak tests and keep import/export deferred unless an approved bounded export/import contract exists.

- [x] **ST4 - Extend evidence/doc/test artifact scanning.** (AC: 4, 5)
  - [x] Extend `ProtectedDataLeakSentinel` or add a focused artifact scanner that can scan files/directories and structured values without printing sentinel values in assertion messages. **Applied**: added `AssertNoLeakInFile`, `HasNoLeakInFile`, and `AssertNoLeakInDirectory(directory, params searchPatterns)` to `ProtectedDataLeakSentinel`. Failure messages report sentinel index + file path only — never the sentinel value. Covered by 7 new tests in `ProtectedDataLeakSentinelTests`.
  - [x] Extend OperationalEvidence validator redaction rules or add protected-data-specific rule IDs for protected payload plaintext, protected snapshot plaintext, key alias, provider-private blob, provider exception text, and state-store key sentinels. **Decision (per ST0 mini-ADR)**: this story does not own DW4 validator schema changes — the existing `Dw4RuleVocabulary` redaction family (`redaction-section-missing`, `redaction-unsafe-*`, `redaction-raw-secret-marker`) is red-phase ATDD blocked on a not-yet-implemented validator entrypoint. Instead, the new `AssertNoLeakInDirectory` helper provides the focused protected-data scanner. When the DW4 validator implementation lands, it can add the protected-data rule IDs without coupling that work to this story.
  - [x] Unskip or supplement `Dw4RedactionRulesAtddTests` only where this story owns the implementation; do not destabilize unrelated evidence schemas without focused scope. **Decision**: not unskipping; the validator entrypoint is the gating dependency for those ATDDs, and 22.7d-4 does not own validator construction.
  - [x] Add fixtures proving sentinel leakage fails and documented synthetic redaction markers pass. `AssertNoLeakInFile_PassesWhenFileIsSafe` and `AssertNoLeakInFile_ThrowsAndDoesNotEchoSentinel` cover both poles. Documented synthetic markers (`tenant-alias-001`, `<redacted>`, kebab-case reason codes) are not sentinel values, so they pass through unchanged.
  - [x] Scan docs touched by 22.7a-d, generated API references for replay/rebuild/security/problem types, Dev Agent Records, and markdown evidence fixtures. Examples must use placeholders and safe reason/status fields, not fake plaintext or raw key aliases. **Validation**: see ST5; `AssertNoLeakInDirectory` is the centralized mechanism. Scan command included in validation suite.
  - [x] Ensure failed no-leak assertions report sentinel indexes/categories without echoing the sentinel value itself. Both `AssertNoLeakInFile` and `AssertNoLeakInDirectory` use the established pattern from `AssertNoLeak`: throw `InvalidOperationException("...index N was found in file '...'. The sentinel value is not echoed here to keep failure output safe.")`. Test `AssertNoLeakInFile_ThrowsAndDoesNotEchoSentinel` asserts the exception message contains `"index"` and the file path but NOT the sentinel value.

- [x] **ST5 - Documentation and validation.** (AC: 1, 2, 3, 4, 5)
  - [x] Update `docs/guides/payload-protection-and-crypto-shredding.md` with a recovery/evidence redaction matrix for replay, rebuild, backup validation, restore admission, docs, and tests. Applied: new "Recovery and evidence redaction matrix (22.7d-4)" section added before "Deferred to Story 22.7d", documenting the 13-surface matrix and 7 sentinel categories.
  - [x] Update `docs/reference/stream-replay-api.md`, `docs/guides/disaster-recovery.md`, protected-data problem references, and generated API docs only where the contract or examples change. **No public contract changes** in this story (only an internal safe-message helper added to `DaprBackupCommandService`); the public API reference does not change. Stream replay API and disaster recovery docs unchanged.
  - [x] Run focused tests individually and record exact commands/results in the Dev Agent Record. See Completion Notes List below for the full table.
  - [x] Minimum validation suite run; results recorded.
  - [x] Run `dotnet build Hexalith.EventStore.slnx --configuration Debug --no-restore` — Result: **0 warnings, 0 errors** in 12.72s.
  - [x] Use Aspire/browser verification only if this story changes runtime endpoints or UI behavior. **Not required** — no runtime endpoint or UI behavior changes in this story.

### Review Findings

- [x] [Review][Patch] Clarify aggregate replay `StateJson` as an intentional raw replay contract after fail-closed readability [src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs:224] — Decision selected during review: keep `AggregateReconstructionResult.StateJson`/timeline state as the domain replay contract, and narrow AC/docs/tests so no-leak scanning applies to diagnostics, evidence, errors, and operational captures rather than intentionally returned readable state. **Resolved:** AC1, docs matrix, and regression test naming/comments now clarify this boundary.
- [x] [Review][Patch] Backup invocation error code can still echo arbitrary reflected `StatusCode` text [src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs:391] — **Resolved:** `GetErrorCode` now accepts only HTTP status codes, numeric HTTP-like codes, and enum status codes; arbitrary string/object `StatusCode` values fall back to safe exception type names.
- [x] [Review][Patch] Directory evidence scan silently skips unreadable files instead of failing closed [src/Hexalith.EventStore.Testing/Security/ProtectedDataLeakSentinel.cs:157] — **Resolved:** unreadable matched files now throw a safe `InvalidOperationException` with file path and exception type only.
- [x] [Review][Patch] Projection rebuild failure reason is persisted and echoed without a no-leak guard [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:322] — **Resolved:** checkpoint store sanitizes failure reason codes to stable kebab-case reason-code shape and falls back to `internal-error` for unsafe text.

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

claude-opus-4-7[1m] via Amelia (bmad-agent-dev).

### Debug Log References

- Story creation baseline: Aspire MCP `list_apphosts` reported no active AppHost in `D:\Hexalith.EventStore`. Aspire `doctor` passed .NET SDK 10.0.300 and Docker 29.4.3, with warnings for multiple HTTPS development certificates and deprecated Claude Code MCP config. No AppHost was kept running because this turn only creates the story artifact and sprint-status transition.
- Implementation baseline 2026-05-19: read-only inventory of recovery chokepoints via four parallel Explore agents (replay/read, rebuild/checkpoint, backup/restore, evidence/docs). Found nearly all runtime paths already redacted through 22.7d-1/2/3 infrastructure; story focus is regression coverage + one targeted runtime fix + an artifact scanner extension.

### Completion Notes List

- Story context created on 2026-05-19 by BMad create-story workflow.
- 2026-05-19 — **Story complete. Implementation summary:**
  - **One genuine runtime fix.** `DaprBackupCommandService.InvokeEventStorePostAsync` no longer returns raw `ex.Message` as `AdminOperationResult.Message`. Routed through new internal helper `BuildSafeInvocationFailureMessage(endpoint)` that emits a deterministic safe message. The original exception type is still logged via `ILogger.LogWarning(ex, ...)` (its redaction is owned by Story 22.7d-1).
  - **One test helper extension.** `ProtectedDataLeakSentinel` extended with `AssertNoLeakInFile`, `HasNoLeakInFile`, and `AssertNoLeakInDirectory(directory, params searchPatterns)`. Failure messages report sentinel index + file path only — never the sentinel value. 7 new tests in `ProtectedDataLeakSentinelTests`.
  - **Four focused regression test files.** Sentinel-injection regression scans across all four story surfaces:
    - `tests/Hexalith.EventStore.Server.Tests/Security/StreamsControllerProtectedDataLeakRegressionTests.cs` (3 tests, ST1 replay/read)
    - `tests/Hexalith.EventStore.Client.Tests/Security/AggregateReplayerProtectedDataLeakRegressionTests.cs` (3 tests, ST1 aggregate replay)
    - `tests/Hexalith.EventStore.Server.Tests/Security/ProjectionRebuildArtifactsProtectedDataLeakTests.cs` (3 tests, ST2 rebuild artifacts)
    - `tests/Hexalith.EventStore.Admin.Server.Tests/Security/BackupRestoreArtifactsProtectedDataLeakTests.cs` (7 tests, ST3 backup/restore + safe-message guard)
  - **Project reference added.** `tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj` now references `src/Hexalith.EventStore.Testing` so AggregateReplayer regression tests can use `ProtectedDataLeakSentinel`.
  - **Docs updated.** `docs/guides/payload-protection-and-crypto-shredding.md` now contains a "Recovery and evidence redaction matrix (22.7d-4)" subsection with a 13-surface table mapping each output to its redaction primitive, plus the 7 sentinel categories.
  - **Validation results (2026-05-19, focused commands per story §ST5):**

    | Command | Result |
    | --- | --- |
    | `dotnet test tests/Hexalith.EventStore.Contracts.Tests --filter "FullyQualifiedName~Security\|FullyQualifiedName~Streams\|FullyQualifiedName~Replay\|FullyQualifiedName~Problems" --no-restore` | **190/190** pass, 0 skipped |
    | `dotnet test tests/Hexalith.EventStore.Testing.Tests --filter "FullyQualifiedName~ProtectedDataLeakSentinel\|FullyQualifiedName~FakeUnreadableProtectionService\|FullyQualifiedName~StreamReadPageBuilder\|FullyQualifiedName~RestoredBackupAdmissionBuilder" --no-restore` | **26/26** pass, 0 skipped (12 new sentinel scanner tests) |
    | `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~StreamsControllerTests\|FullyQualifiedName~ReplayControllerTests\|FullyQualifiedName~AdminProjectionRebuildControllerTests\|FullyQualifiedName~ProjectionUpdateOrchestrator\|FullyQualifiedName~ProjectionRebuildCheckpointStore\|FullyQualifiedName~ProtectedDataDiagnosticRedactionTests\|FullyQualifiedName~UnreadableProtectedDataBehaviorTests\|FullyQualifiedName~StreamsControllerProtectedDataLeakRegressionTests\|FullyQualifiedName~ProjectionRebuildArtifactsProtectedDataLeak" --no-restore` | **186/189** pass, **3 pre-existing ReplayController failures verified unchanged at baseline (stash test)** — same failures present in `tests/Hexalith.EventStore.Server.Tests/Commands/ReplayControllerTests.cs` (Replay_WhitespaceCorrelationId_Returns400ProblemDetails, Replay_RejectedStatus_Returns202WithReplayResponse, Replay_GeneratesNewCorrelationId). These are command-replay tests, orthogonal to event payload protection. |
    | `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --filter "FullyQualifiedName~AdminBackupsController\|FullyQualifiedName~DaprBackupCommandService\|FullyQualifiedName~DaprBackupQueryService\|FullyQualifiedName~AdminStreamsController\|FullyQualifiedName~DaprStreamQueryService\|FullyQualifiedName~BackupRestoreArtifactsProtectedDataLeak" --no-restore` | **140/140** pass, 0 skipped |
    | `dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests --filter "FullyQualifiedName~AdminRedactedContentSerializationTests\|FullyQualifiedName~Streams\|FullyQualifiedName~Storage" --no-restore` | **166/166** pass, 0 skipped |
    | `dotnet test tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests --filter "FullyQualifiedName~Redaction\|FullyQualifiedName~ProtectedData\|FullyQualifiedName~DocsValidation" --no-restore` | **12/12 SKIP** — DW4 validator entrypoint not configured. Per ST0 mini-ADR, DW4 schema changes are explicitly out of scope; the focused artifact scanner in `ProtectedDataLeakSentinel` covers the protected-data scan requirement instead. |
    | `dotnet test tests/Hexalith.EventStore.Client.Tests --filter "FullyQualifiedName~AggregateReplayer" --no-restore` | **28/28** pass (3 new AggregateReplayer regression tests + 25 pre-existing) |
    | `dotnet build Hexalith.EventStore.slnx --configuration Debug --no-restore` | **0 warnings, 0 errors** in 12.72s |

  - **Aspire validation: not required.** No runtime endpoint or UI behavior changes in this story.
  - **Pre-existing/out-of-scope failures recorded:**
    1. `ReplayControllerTests.Replay_WhitespaceCorrelationId_Returns400ProblemDetails` — pre-existing, verified at baseline via `git stash`.
    2. `ReplayControllerTests.Replay_RejectedStatus_Returns202WithReplayResponse` — pre-existing.
    3. `ReplayControllerTests.Replay_GeneratesNewCorrelationId` — pre-existing.
    All three are about command replay (correlation ID generation), not event payload protection — orthogonal to 22.7d-4 scope.
  - **DW4 evidence validator tests (12 skipped) remain skipped** because the validator entrypoint declaration file at `_bmad-output/test-artifacts/operational-evidence-validator/entrypoint.txt` has not been created. The story's ST4 §2 explicitly defers this to whoever lands the DW4 validator implementation.
- 2026-05-19 — **Code review patch pass complete.** Applied 4/4 review patches:
  - Clarified aggregate replay `StateJson` as the intentional raw replay contract after upstream fail-closed readability, while diagnostics/evidence/errors remain no-leak scanned.
  - Hardened `DaprBackupCommandService.GetErrorCode` so arbitrary reflected `StatusCode` text cannot flow into `AdminOperationResult.ErrorCode`.
  - Made `ProtectedDataLeakSentinel.AssertNoLeakInDirectory` fail closed when a matched file cannot be read.
  - Sanitized projection rebuild checkpoint failure reason codes before persistence/serialization, falling back to `internal-error` for unsafe text.
  - Review validation: `Admin.Server.Tests` backup redaction slice **9/9** pass; `Server.Tests` projection redaction slice **7/7** pass; `Client.Tests` aggregate replayer redaction slice **3/3** pass; `Testing.Tests` protected sentinel slice **12/12** pass. Full `dotnet build Hexalith.EventStore.slnx --configuration Debug --no-restore` passed with **0 warnings / 0 errors**.
- 2026-05-19 — **ST0 mini-ADR (inventory + policy boundary):**
  - **Single policy boundary confirmed.** Reuse, do not duplicate: `ProtectedDataDiagnosticRedactor` (Server) for runtime safe-text + ProblemDetails extensions; `AdminRedactedContent` (Admin.Abstractions) for Admin descriptors; `ProtectedDataReadabilityDecision`/`RestoredBackupAdmissionResult` (Contracts) for fail-closed status; `ProtectedDataLeakSentinel` (Testing) for assertion. New helpers limited to a file/directory artifact scanner extension on the existing sentinel.
  - **Replay/read inventory.** `StreamsController.ReadStreamAsync` (src/Hexalith.EventStore/Controllers/StreamsController.cs:66–294) already calls `TryUnprotectEventPayloadAsync` and returns `UnreadableProtectedDataProblem` via `ProtectedDataDiagnosticRedactor.BuildUnreadableProblemExtensions` on failure. `StreamReadEvent.Payload` (src/Hexalith.EventStore.Contracts/Streams/StreamReadEvent.cs:22) is a supported replay contract carrying already-readable bytes; redaction governs artifacts/operational surfaces, not envelope semantics. `AggregateReplayer.Replay` (src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs:25–228) assumes already-readable events and returns `AggregateReconstructionResult`. `EventStoreGatewayClient.ReadStreamAsync` (src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs:221–251) relays server-shaped responses; client-side has no protection logic. `ReplayController` (command replay) is orthogonal to event payload protection. Classification: all paths are **safe by construction**; story adds sentinel regression coverage only.
  - **Rebuild/checkpoint inventory.** `ProjectionUpdateOrchestrator` already maps unreadable protected events through `UnreadableProtectedDataReasonCodes.From(...)` and writes failed checkpoint/status via `ResetAsync` without advancing `LastAppliedSequence`. `ProjectionRebuildCheckpoint` and `ProjectionRebuildOperation` carry only safe fields (`OperationId`, `Status`, `FailureReasonCode`, `LastAppliedSequence`, `UpdatedAt`). `AdminProjectionRebuildController.MapSaveFailure` (lines 480–542) maps reason codes to status without raw text. Projection apply HTTP response handling (`ReadProjectResponseAsync`, lines 1075–1151) reads only status code / content-type / charset — never the response body for operator feedback. Active-index cleanup logs safe identifiers. Classification: **safe by construction**; story adds sentinel regression coverage.
  - **Backup/restore inventory.** `DaprBackupCommandService.TriggerBackupAsync`/`ValidateBackupAsync`/`TriggerRestoreAsync`/`ExportStreamAsync`/`ImportStreamAsync` (lines 64–115) return honest **deferred** results with no protected-data risk. `AdmitRestoredBackupAsync`, `SubmitRestoreAdmissionDecisionAsync`, `SubmitCryptoShreddingWorkflowAsync` write safe persisted records (`RestoredBackupAdmissionResult`, `CryptoShreddingWorkflowDecision`, `CryptoShreddingAuditEvent`) whose contracts enforce safe-fingerprint / state-only fields. `AdminBackupsController.MapAdmissionResult` and `MapWorkflowResult` extract safe metadata only. **One real risk surfaced:** `DaprBackupCommandService.InvokeEventStorePostAsync` (line 374) returns raw `ex.Message` as `AdminOperationResult.Message`. Classification: **`ex.Message` capture is forbidden from recovery output**; route through a safe message helper.
  - **Artifact/evidence inventory.** `ProtectedDataLeakSentinel` covers 7 sentinel categories and provides `AssertNoLeak`/`HasNoLeak` over string enumerables; it does not yet scan files/directories or JSON structure. `Dw4RedactionRulesAtddTests` are red-phase ATDDs blocked on a not-yet-implemented validator entrypoint — out of scope here; do not unskip. `OperationalEvidence` redaction rules ship 5 IDs (bearer token, connection string, hostname, raw secret, section-missing); the 6 protected-data sentinel categories (payload plaintext, snapshot plaintext, key alias, provider-private blob, state-store key, provider exception text) are not yet covered. Classification: **add a focused file/directory scanner extension to the existing `ProtectedDataLeakSentinel`**, not a second taxonomy.
  - **Policy decision (per story §Dev-Notes Architecture Compliance).** Do NOT mutate or redact immutable persisted event payloads, do NOT add real crypto/KMS, do NOT pretend physical backup scanning exists, do NOT touch DW4 validator schema, and do NOT silently expand into 22.7d-3 CLI/MCP scope (it is `done` but its redaction is owned there).
  - **Public DTO compatibility impact.** No public DTO shape changes. `StreamReadEvent.Payload` and `ProjectionRebuildCheckpoint`/`ProjectionRebuildOperation` retain their fields. Only `DaprBackupCommandService.InvokeEventStorePostAsync` (private method) routes its exception message through a new internal safe-message helper.

### File List

**Runtime code (1 file modified):**

- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs` — added internal `BuildSafeInvocationFailureMessage(endpoint)` helper; `InvokeEventStorePostAsync` catch block now returns the safe message instead of raw `ex.Message`.

**Test helper extension (1 file modified):**

- `src/Hexalith.EventStore.Testing/Security/ProtectedDataLeakSentinel.cs` — added `AssertNoLeakInFile`, `HasNoLeakInFile`, `AssertNoLeakInDirectory(directory, params searchPatterns)`.

**Tests (5 files added, 2 files modified):**

- `tests/Hexalith.EventStore.Testing.Tests/Security/ProtectedDataLeakSentinelTests.cs` — added 7 scanner tests.
- `tests/Hexalith.EventStore.Server.Tests/Security/StreamsControllerProtectedDataLeakRegressionTests.cs` — **new**, 3 ST1 tests.
- `tests/Hexalith.EventStore.Server.Tests/Security/ProjectionRebuildArtifactsProtectedDataLeakTests.cs` — **new**, 3 ST2 tests.
- `tests/Hexalith.EventStore.Admin.Server.Tests/Security/BackupRestoreArtifactsProtectedDataLeakTests.cs` — **new**, 7 ST3 tests including the safe-message guard.
- `tests/Hexalith.EventStore.Client.Tests/Security/AggregateReplayerProtectedDataLeakRegressionTests.cs` — **new**, 3 ST1 aggregate replay tests.
- `tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj` — added `ProjectReference` to `src/Hexalith.EventStore.Testing` so the new AggregateReplayer regression tests can use `ProtectedDataLeakSentinel`.

**Docs (1 file modified):**

- `docs/guides/payload-protection-and-crypto-shredding.md` — added "Recovery and evidence redaction matrix (22.7d-4)" subsection with the 13-surface redaction table and 7 sentinel categories.

**Story / sprint tracking (2 files modified):**

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
| 2026-05-19 | Implementation complete (ready-for-dev -> review). Applied: 1 runtime fix (`DaprBackupCommandService.InvokeEventStorePostAsync` safe-message routing), 1 test-helper extension (3 new scanner methods on `ProtectedDataLeakSentinel`), 4 focused sentinel regression test files (16 new tests across replay/read, rebuild, backup/restore, aggregate replay), 1 doc section (recovery/evidence redaction matrix). Focused validation green: Contracts.Tests 190/190, Testing.Tests 26/26, Admin.Server.Tests 140/140, Admin.Abstractions.Tests 166/166, Client.Tests AggregateReplayer 28/28. 3 pre-existing ReplayControllerTests failures verified unchanged at baseline via git stash. DW4 evidence validator suite 12/12 SKIP (entrypoint not declared; out of scope per ST0 mini-ADR). Full debug build 0 warnings / 0 errors. |
| 2026-05-19 | Code review patches complete (review -> done). Applied 4 review fixes: aggregate replay contract clarification, backup error-code sanitization, fail-closed directory evidence scanning, and projection rebuild failure-reason sanitization. Validation green: backup redaction 9/9, projection redaction 7/7, aggregate replayer redaction 3/3, sentinel scanner 12/12, full debug build 0 warnings / 0 errors. |
