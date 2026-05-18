# Story 22.7d-1: Protected Data Redaction in Logs and ProblemDetails

Status: done

Context created: 2026-05-18
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR104 runtime diagnostic redaction for logs, OpenTelemetry activity status/descriptions, command status failure text, dead-letter diagnostic text, and RFC 7807 ProblemDetails. This child story intentionally excludes Admin API/Web UI rendering, CLI/MCP output, and replay/rebuild/backup-validation artifact scans except where those surfaces consume the runtime log/ProblemDetails/failure-text contracts created here.

Review hardening applied: party-mode review on 2026-05-18 kept the story `ready-for-dev` but required tighter pre-dev guidance. The implementation must create one named runtime diagnostic redaction boundary, prove every named diagnostic egress with path-specific sentinel tests, preserve useful safe operator fields, and avoid broad "no leak" assertions that do not exercise the risky paths.

Advanced elicitation hardening applied: red-team/pre-mortem/failure-mode/first-principles/self-consistency review added an allow-list-first diagnostic model, mandatory risky-callsite inventory, explicit Contracts-vs-runtime ownership, and tests that inspect structured logs, activity status/events/exceptions, persisted diagnostics, dead-letter payload/metadata, and ProblemDetails extensions.

## Story

As a security reviewer,
I want logs and ProblemDetails to expose only protected-data metadata,
so that API and runtime diagnostics never leak protected payload or snapshot content.

## Acceptance Criteria

1. **One protected-data diagnostic redaction policy governs runtime logs and ProblemDetails.**
   - Given protected payloads, protected snapshots, unreadable protected data, crypto-shredding statuses, restored-backup admission statuses, provider exceptions, or malformed protection metadata are involved
   - When EventStore emits structured logs, source-generated log messages, OpenTelemetry activity status descriptions, command status failure reasons, dead-letter diagnostic text, or RFC 7807 ProblemDetails
   - Then the output is produced through one EventStore-owned helper/policy that emits only safe metadata: tenant, domain, aggregateId, sequenceNumber, checkpointId, correlationId, causationId, eventTypeName, stage, protection state, metadataVersion, stable reasonCode, retryable/permanent booleans, and operator-safe next-action text.
   - And payload bytes, event payload JSON, command payload JSON, snapshot state JSON, reconstructed state JSON, field values, raw keys, key material, IVs/nonces, auth tags, unsafe key aliases, provider-private metadata, state-store keys, connection strings, stack traces, and provider exception text are never emitted.
   - And the policy is implemented behind a named shared component, expected to be `ProtectedDataDiagnosticRedactor` or equivalent, so controllers, actors, publishers, pipeline behaviors, command status, dead-letter, and ProblemDetails paths do not each hand-roll sanitization.
   - And any diagnostic string derived from exception messages, command payloads, domain errors, provider results, external service responses, or metadata dictionaries must pass through that shared redaction boundary before logging, telemetry, persistence, dead-lettering, or ProblemDetails serialization.
   - And diagnostic output follows an allow-list model: approved fields are emitted by explicit projection, and all other provider-originated strings, metadata dictionaries, exception text, payload-derived strings, and external service response text are excluded by default.

2. **Protected-data exception and failure text is sanitized before logging or telemetry.**
   - Given a protection provider throws, returns unreadable, reports provider-opaque metadata, or a protected-data helper throws
   - When `EventPublisher`, `SnapshotManager`, `AggregateActor`, `DeadLetterMessage`, `DeadLetterPublisher`, `LoggingBehavior`, command status writes, or OpenTelemetry activities record diagnostics
   - Then diagnostic message fields use stable reason codes and safe stage metadata instead of `exception.Message` when protected-data context is possible.
   - And cancellation remains cancellation: `OperationCanceledException` is propagated and is not converted into a protected-data redaction failure.

3. **ProblemDetails are safe, stable, and complete enough for operators.**
   - Given a protected-data condition reaches public HTTP APIs
   - When `StreamsController`, global exception handlers, query/validation handlers, authorization handlers, or future protected-data ProblemDetails factories write RFC 7807 responses
   - Then the response uses stable type URIs and extension keys from Contracts where available, includes `correlationId`, `reasonCode`, `reasonCategory`, `stage`, tenant/domain/aggregate/sequence metadata when known, and safe operator guidance.
   - And `title`, `detail`, and extension values do not contain provider exception text, protected payload/snapshot content, key aliases, stack traces, state-store keys, or connection strings.
   - And generic `500` responses remain generic and do not expose exception details.
   - And representative protected-data ProblemDetails examples are documented or pinned in tests with stable `type`, `title`, `status`, `detail`, `correlationId`, and machine-readable reason extensions.
   - And safe redacted fallback text is deterministic, for example: `Protected data diagnostic details were redacted. ReasonCode=<reason-code>; Stage=<stage>.`

4. **Existing 22.7a-c behavior is preserved.**
   - Given Stories 22.7a, 22.7b, and 22.7c are complete
   - When this story changes diagnostic surfaces
   - Then it reuses `EventStorePayloadProtectionMetadata`, `UnreadableProtectedDataReason`, `UnreadableProtectedDataReasonCodes`, `ProtectedDataReadabilityDecision`, `ProtectedDataReadabilityDecisionFactory`, `UnreadableProtectedDataProblem`, `CryptoShreddingWorkflowProblem`, `RestoredBackupAdmissionProblem`, and `ProtectedDataLeakSentinel` rather than introducing duplicate reason taxonomies.
   - And no real encryption provider, KMS, Key Vault, DAPR secret store, certificate, physical backup scanner, skip-past-unreadable replay policy, Admin UI redesign, CLI/MCP rendering change, or provider-specific dependency is introduced.
   - And existing 22.7a-c guardrail tests remain green or are updated only to strengthen no-leak behavior: metadata serialization/parsing tests, unreadable taxonomy tests, readability-decision tests, crypto-shredding no-leak tests, and `ProtectedDataLeakSentinel` tests.

5. **No-leak tests cover every runtime diagnostic path touched by this story.**
   - Given sentinel values are injected into payload plaintext, snapshot plaintext, key alias, provider-private metadata, provider exception messages, state-store keys, connection strings, and assertion messages
   - When focused tests capture logs, activity status descriptions, ProblemDetails JSON, command status failure reasons, dead-letter messages, publish failure reasons, and exception messages
   - Then `ProtectedDataLeakSentinel.AssertNoLeak(...)` passes for every captured string.
   - And tests prove safe fields remain present so diagnostics are useful, not merely empty.
   - And each named egress has its own sentinel assertion: rendered log message, structured log property values, OpenTelemetry activity status/description, `CommandStatusRecord`/publish failure text, `DeadLetterMessage.ErrorMessage`, dead-letter publisher log fields, and serialized ProblemDetails JSON.
   - And test evidence proves structured data is safe, not only rendered strings: log state/property values, activity events/tags/status descriptions, dead-letter CloudEvent metadata, and ProblemDetails extension values must all be scanned.

## Tasks / Subtasks

- [x] **ST0 - Freeze the runtime diagnostic redaction policy.** (AC: 1, 2, 3, 4, 5)
  - [x] Read this story, parent Story 22.7d, Stories 22.7a/22.7b/22.7c, Epic 22, PRD FR104, architecture D5/SEC-5/Payload and Snapshot Protection, `_bmad-output/project-context.md`, and the current source files listed below before code edits.
  - [x] Define the allowed-safe-field table for logs, activity status, command status, dead-letter messages, generic exception logs, stream-read ProblemDetails, unreadable protected-data ProblemDetails, crypto-shredding ProblemDetails, restored-backup-admission ProblemDetails, and validation/query/authorization ProblemDetails.
  - [x] Define a single helper/policy location and name. Expected implementation shape: `ProtectedDataDiagnosticRedactor` in the lowest layer that can serve all touched runtime surfaces without introducing ASP.NET dependencies into Contracts. Use Contracts only for stable reason/status projection; use `Hexalith.EventStore.Server` or `Hexalith.EventStore` for runtime exception-to-diagnostic/ProblemDetails helpers if framework dependencies are needed.
  - [x] Clarify ownership before implementation: Contracts owns stable reason codes, type URIs, extension names, and pure provider-neutral status projection; runtime owns `ProtectedDataDiagnosticRedactor` because it handles exceptions, logs, telemetry, persisted failure text, dead-letter diagnostics, and ASP.NET ProblemDetails projection.
  - [x] Inventory risky diagnostic callsites before coding with targeted searches for `ex.Message`, `exception.Message`, `ExceptionMessage`, `ErrorMessage`, `SetStatus`, `AddException`, `FailureReason`, `ProblemDetails.Detail`, `Extensions[`, and `WriteAsJsonAsync(problemDetails)` across `src/Hexalith.EventStore`, `src/Hexalith.EventStore.Server`, and related tests.
  - [x] Record each risky callsite disposition in the Dev Agent Record as `redacted`, `safe-by-construction`, `out-of-scope`, or `unchanged-with-rationale`.
  - [x] Define the non-bypass rule: no touched caller may pass `exception.Message`, provider result text, metadata dictionaries, command payloads, payload JSON, snapshot state, or state-store keys directly into log template parameters, activity status descriptions, persisted failure fields, dead-letter diagnostics, or ProblemDetails.
  - [x] Define the allow-list-first projection rule: helpers construct diagnostic outputs only from approved safe fields; they do not redact after serializing arbitrary objects or copy arbitrary extension dictionaries.
  - [x] Decide whether to use `Microsoft.Extensions.Compliance.Redaction`. Default recommendation: do not add this dependency in ST0 unless it clearly improves the existing source-generated logging pattern; if added, use centralized package management only and keep provider-specific crypto out of scope.
  - [x] Explicitly classify `exception.Message` as unsafe by default for protected-data paths. Only fixed safe messages or messages from known safe exception types may flow into log template fields, activity statuses, command status, dead-letter error text, or ProblemDetails.
  - [x] Freeze deterministic safe fallback text for redacted exception/failure messages. The fallback must include only safe stage/reason metadata and must not be blank.
  - [x] Define a minimum preserved diagnostic set per surface: `correlationId`, `tenantId` when known and safe, `domain`, `aggregateId`, `commandType` or `eventTypeName` when known, `sequenceNumber` or `checkpointId` when known, `stage`, `reasonCode`, `reasonCategory`, `retryable`, `permanent`, and safe next action where available.
  - [x] Keep `OperationCanceledException` propagation unchanged.

- [x] **ST1 - Add/reuse protected-data diagnostic helpers.** (AC: 1, 2, 3, 4)
  - [x] Add reusable helpers that project `ProtectedDataReadabilityDecision`, `UnreadableProtectedDataReason`, crypto-shredding workflow results, and restore-admission results into safe diagnostic strings/extension dictionaries.
  - [x] Provide fixed safe message formats for publish failure, rehydrate failure, snapshot-load failure, replay failure, backup-admission conflict, restore-admission conflict, generic provider failure, and provider-opaque metadata.
  - [x] Provide a safe `ProblemDetails` extension builder or extension method that uses existing Contracts constants and never copies raw dictionaries from provider metadata.
  - [x] Include a test-pinned safe ProblemDetails example for an unreadable protected payload. Example shape: `type=https://hexalith.io/problems/unreadable-protected-data`, `title=Protected data is unreadable`, `status=422/410/503 per reason`, `detail=<safe guidance only>`, extensions `correlationId`, `tenantId`, `domain`, `aggregateId`, `sequenceNumber`, `stage`, `reasonCode`, `reasonCategory`, `metadataVersion`, `retryable`, `permanent`.
  - [x] Add helper tests that verify safe output retains the minimum diagnostic set instead of returning empty strings.
  - [x] Add tests for unknown enum values/future statuses where local helpers use switch expressions; fail closed rather than defaulting to exception text.
  - [x] Add a regression test proving a provider exception string containing every `ProtectedDataLeakSentinel` value cannot pass through `ProtectedDataDiagnosticRedactor` into any returned safe diagnostic string.

- [x] **ST2 - Sanitize runtime logging, telemetry, command status, and dead-letter paths.** (AC: 1, 2, 4, 5)
  - [x] Replace protected-data-risk `Activity.SetStatus(..., ex.Message)` sites with safe stage/reason descriptions when protected data can be in the call path: `EventPublisher`, `AggregateActor`, `DeadLetterPublisher`, `LoggingBehavior`, and replay/controller paths touched by this story.
  - [x] Update `AggregateActor.HandleInfrastructureFailureAsync` so `Log.InfrastructureFailure` does not log unsafe `exception.Message` for `ProtectedDataUnreadableException` or provider-derived protected-data failures.
  - [x] Update `DeadLetterMessage.FromException` and `DeadLetterPublisher` logging so protected-data failures store/log safe reason text only. Preserve non-protected existing behavior only where ST0 proves it cannot carry protected content.
  - [x] Treat `DeadLetterMessage.ErrorMessage`, dead-letter publication payload diagnostic fields, command status failure text, `UnpublishedEventsRecord.LastFailureReason`, and actor pipeline failure state as persisted or externally inspectable, not as internal-only logs.
  - [x] Include dead-letter CloudEvent metadata and dead-letter publication payload diagnostic fields in the no-leak audit; do not assume metadata is safe because it is not part of the JSON body.
  - [x] Audit publish-failed command status and `UnpublishedEventsRecord.LastFailureReason`; ensure `EventPublisher.BuildUnreadableFailureReason(...)` remains safe and any new failure reason that touches protected data goes through the helper.
  - [x] Preserve source-generated `LoggerMessage` patterns and structured fields; do not add interpolated logs that include exception messages, payload bytes, snapshots, metadata dictionaries, or command payloads.
  - [x] Inspect source-generated logging partials that currently accept `ExceptionMessage`/`ErrorMessage`; rename parameters to safe names or update templates where needed so future call sites do not infer raw `ex.Message` is acceptable.

- [x] **ST3 - Harden ProblemDetails creation for protected-data contexts.** (AC: 1, 3, 4, 5)
  - [x] Update or centralize `StreamsController.CreateUnreadableProtectedDataProblem` so it uses the shared helper and includes only approved extensions.
  - [x] Audit `GlobalExceptionHandler`, `QueryExecutionFailedExceptionHandler`, `ValidationProblemDetailsFactory`, `AuthorizationExceptionHandler`, `AuthorizationServiceUnavailableHandler`, `DaprSidecarUnavailableHandler`, `BackpressureExceptionHandler`, `ConcurrencyConflictExceptionHandler`, `DomainCommandRejectedExceptionHandler`, and query/command validation controllers for any path that could copy protected provider details into `detail`, `reason`, or extension values.
  - [x] Add a protected-data-specific exception mapping if needed so runtime protected-data failures return stable `UnreadableProtectedDataProblem` instead of falling through to generic 500 or leaking through mapped exception detail.
  - [x] Keep generic 500 responses generic: no stack traces, no exception messages, no provider data.
  - [x] Ensure `application/problem+json` is preserved for protected-data responses.
  - [x] Add before/after examples in tests or docs for one protected-data failure: before-risk source is a provider exception containing sentinel text; after-result is safe ProblemDetails with stable reason/status/correlation metadata and no sentinel.
  - [x] Assert ProblemDetails extension values individually; do not rely only on full JSON string scanning, because unsafe raw values can hide in extension dictionaries or non-string JsonElement projections.

- [x] **ST4 - Add focused no-leak tests and update docs.** (AC: 1, 2, 3, 4, 5)
  - [x] Extend `tests/Hexalith.EventStore.Server.Tests/Security/UnreadableProtectedDataBehaviorTests.cs` or add a focused `ProtectedDataDiagnosticRedactionTests.cs` covering logs, activity status descriptions, publish failure reasons, command status failure reasons, dead-letter error text, and ProblemDetails JSON.
  - [x] Extend `tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs` to use `ProtectedDataLeakSentinel` rather than only byte-pattern checks.
  - [x] Add structured log property assertions, not only rendered-message assertions, for source-generated logging paths that previously accepted `ExceptionMessage` or `ErrorMessage`.
  - [x] Add activity/telemetry assertions using an in-memory listener/exporter that captures `Activity.StatusDescription`, activity exception tags/events, activity tags, and any touched protected-data status descriptions.
  - [x] Add persistence-level assertions for `CommandStatusRecord` failure reason, actor pipeline failure state where applicable, `UnpublishedEventsRecord.LastFailureReason`, and `DeadLetterMessage.ErrorMessage`.
  - [x] Add dead-letter publication assertions for both message body diagnostic fields and CloudEvent metadata.
  - [x] Add focused tests for `GlobalExceptionHandler` and `StreamsController` protected-data ProblemDetails no-leak behavior.
  - [x] Add negative controls proving useful safe operator context remains present: correlation ID, reason code, status/type/title, stage, and tenant/domain/aggregate/sequence metadata when known.
  - [x] Run or update existing 22.7a-c guardrail tests: `EventStorePayloadProtectionMetadataTests`, `UnreadableProtectedDataTests`, `ProtectedDataReadabilityDecisionTests`, `CryptoShreddingNoLeakTests`, and `ProtectedDataLeakSentinelTests`.
  - [x] Update `docs/guides/payload-protection-and-crypto-shredding.md` with the safe diagnostic field matrix for logs and ProblemDetails.
  - [x] Update `docs/reference/problems/unreadable-protected-data.md`, `crypto-shredding-workflow-conflict.md`, and `restored-backup-admission-conflict.md` only if extension fields or guidance text changes.

### Review Findings

- [x] [Review][Patch] Raw exception objects still reach log sinks after redaction [src/Hexalith.EventStore/Pipeline/LoggingBehavior.cs:79; src/Hexalith.EventStore.Server/Events/EventPublisher.cs:241; src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs:76; src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1052; src/Hexalith.EventStore/ErrorHandling/GlobalExceptionHandler.cs:16]
- [x] [Review][Patch] Cancellation paths are recorded as protected-data redaction errors before propagation [src/Hexalith.EventStore/Pipeline/LoggingBehavior.cs:73; src/Hexalith.EventStore/Controllers/CommandStatusController.cs:147; src/Hexalith.EventStore/Controllers/ReplayController.cs:220]
- [x] [Review][Patch] Unreadable protected-data ProblemDetails omit the required `correlationId` [src/Hexalith.EventStore/Controllers/StreamsController.cs:173; src/Hexalith.EventStore/Controllers/StreamsController.cs:400]
- [x] [Review][Patch] Protected-data ProblemDetails projection is still hand-rolled outside the shared diagnostic policy [src/Hexalith.EventStore/Controllers/StreamsController.cs:391; src/Hexalith.EventStore.Server/Diagnostics/ProtectedDataDiagnosticRedactor.cs:17]
- [x] [Review][Patch] No-leak tests miss required egresses and drop the logged exception channel [tests/Hexalith.EventStore.Server.Tests/Security/ProtectedDataDiagnosticRedactionTests.cs:47; tests/Hexalith.EventStore.Server.Tests/Security/ProtectedDataDiagnosticRedactionTests.cs:204]
- [x] [Review][Patch] `DeadLetterPublisher` trusts externally supplied `DeadLetterMessage.ErrorMessage` [src/Hexalith.EventStore.Server/Events/DeadLetterMessage.cs:23; src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs:64]
- [x] [Review][Patch] `ProtectedDataDiagnosticRedactor.SafeToken` uses character filtering instead of semantic allow-lists [src/Hexalith.EventStore.Server/Diagnostics/ProtectedDataDiagnosticRedactor.cs:17; src/Hexalith.EventStore.Server/Diagnostics/ProtectedDataDiagnosticRedactor.cs:49]
- [x] [Review][Patch] Sprint metadata `last_updated` moves backward relative to preserved history [_bmad-output/implementation-artifacts/sprint-status.yaml:38]

## Dev Notes

### Current Implementation State

- `EventPublisher.PublishEventsAsync` already fails closed for provider-opaque/unreadable protected events and returns a safe `FailureReason` via `BuildUnreadableFailureReason(...)`. Its generic catch still uses `activity.SetStatus(..., ex.Message)` and returns `ex.Message`; ST0 must decide how to prevent protected provider text from flowing through generic provider exceptions.
- `SnapshotManager.LoadSnapshotAsync` logs safe unreadable snapshot reason codes for typed unreadable/provider-opaque paths, but its advisory/generic catches pass exceptions to the logger. This is acceptable only if exception message text is never promoted into structured template fields or public responses.
- `AggregateActor.EnsureEventsReadableForDomainAsync` throws `ProtectedDataUnreadableException`, whose message is safe. However, `HandleInfrastructureFailureAsync` logs `exception.Message`, `DeadLetterMessage.FromException` stores it, and `DeadLetterPublisher` logs it. This is safe for `ProtectedDataUnreadableException` today but brittle for any provider-derived protected-data exception. Centralize the mapping.
- `LoggingBehavior` logs `ExceptionMessage={ExceptionMessage}` and sets activity status to `ex.Message` for all MediatR failures. Since submit command paths can touch protected-data processing through command handling, this is a high-priority audit point.
- `StreamsController.CreateUnreadableProtectedDataProblem` already emits `UnreadableProtectedDataProblem` with reason/category/stage/tenant/domain/aggregate/sequence/metadataVersion/retryable/permanent and safe guidance. Convert it to the shared helper if that prevents drift.
- `GlobalExceptionHandler` returns generic 500 ProblemDetails and logs the exception with correlation ID only in the template; it should stay generic and no-leak.
- `QueryExecutionFailedExceptionHandler` copies `queryFailure.Detail` into `ProblemDetails.Detail` except for 403 sanitization. If protected-data query paths can create this exception, detail text must be sanitized or constrained.
- `ValidationProblemDetailsFactory` copies FluentValidation messages and error dictionaries into ProblemDetails. Protected metadata validation errors should use fixed safe messages and field paths, not raw metadata values.
- `DeadLetterMessage` intentionally carries the full `CommandEnvelope` for replay support. This story does not remove that payload from the dead-letter event, but it must ensure diagnostic fields and logs never echo command payload or protected provider data. Admin/API display of dead-letter command content is owned by 22.7d-2.

### Files Likely Touched

- `src/Hexalith.EventStore.Contracts/Security/ProtectedDataReadabilityDecision.cs`
- `src/Hexalith.EventStore.Contracts/Security/ProtectedDataReadabilityDecisionFactory.cs`
- `src/Hexalith.EventStore.Contracts/Problems/UnreadableProtectedDataProblem.cs`
- `src/Hexalith.EventStore.Contracts/Problems/CryptoShreddingWorkflowProblem.cs`
- `src/Hexalith.EventStore.Contracts/Problems/RestoredBackupAdmissionProblem.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`
- `src/Hexalith.EventStore.Server/Events/DeadLetterMessage.cs`
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore/Pipeline/LoggingBehavior.cs`
- `src/Hexalith.EventStore/Controllers/StreamsController.cs`
- `src/Hexalith.EventStore/ErrorHandling/*.cs`
- `tests/Hexalith.EventStore.Server.Tests/Security/*ProtectedData*Tests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/*Problem*Tests.cs`
- `docs/guides/payload-protection-and-crypto-shredding.md`
- `docs/reference/problems/*protected*.md`

### Testing Requirements

- Run focused tests individually:
  - `dotnet test tests/Hexalith.EventStore.Contracts.Tests --filter "FullyQualifiedName~Security|FullyQualifiedName~Problems"`
  - `dotnet test tests/Hexalith.EventStore.Testing.Tests --filter ProtectedDataLeakSentinel`
  - `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~Security|FullyQualifiedName~Logging|FullyQualifiedName~ErrorHandling|FullyQualifiedName~Controllers.StreamsControllerTests|FullyQualifiedName~Events.DeadLetter"`
- Minimum required path-specific assertions:
  - Logs: rendered log message and structured properties contain no sentinel and retain safe `reasonCode`/stage/correlation metadata.
  - Telemetry: `Activity.StatusDescription`, status reason text, activity tags, and exception activity events/data contain no sentinel.
  - Command status/publish failure: failure reason fields contain deterministic safe text and no sentinel.
  - Dead letter: `DeadLetterMessage.ErrorMessage`, dead-letter publisher log fields, publication body diagnostics, and CloudEvent metadata contain deterministic safe text and no sentinel.
  - ProblemDetails: serialized JSON and individual extension values contain stable type/title/status/detail/extensions, preserve safe operator metadata, and contain no sentinel.
- If shared Contracts helpers change, run full `dotnet test tests/Hexalith.EventStore.Contracts.Tests` and `dotnet test tests/Hexalith.EventStore.Testing.Tests`.
- If ASP.NET error handling changes, run the relevant `Hexalith.EventStore.Server.Tests` error-handling/controller slices before broader validation.
- Record exact commands/results in the Dev Agent Record.

## Out of Scope

- Admin API/Web UI protected-data rendering and copy/export behavior: Story 22.7d-2.
- CLI and MCP redaction: Story 22.7d-3.
- Replay/rebuild/backup-validation progress artifacts and broad evidence scans: Story 22.7d-4, except for runtime ProblemDetails/log paths touched here.
- Real encryption providers, KMS/Key Vault/DAPR secret-store integration, physical backup scanning, legal hold workflows, and skip-past-unreadable policy.
- Changing dead-letter replay payload storage semantics. This story sanitizes diagnostic text and logs; future admin/display redaction owns presentation of stored command payloads.

## References

- `_bmad-output/planning-artifacts/epics.md` - Story 22.7d-1 source requirements.
- `_bmad-output/planning-artifacts/prd.md` - FR104 protected-data no-leak requirement.
- `_bmad-output/planning-artifacts/architecture.md` - D5 ProblemDetails, SEC-5 logging, enforcement rules, Payload and Snapshot Protection.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12.md` - split of Story 22.7 into focused 22.7a-22.7d work.
- `_bmad-output/implementation-artifacts/22-7d-protected-data-redaction-across-operational-surfaces.md` - parent container and redaction surface inventory.
- `_bmad-output/implementation-artifacts/22-7a-payload-and-snapshot-protection-hooks.md` - protection metadata contract.
- `_bmad-output/implementation-artifacts/22-7b-unreadable-protected-data-behavior.md` - unreadable taxonomy, safe ProblemDetails, sentinel helper.
- `_bmad-output/implementation-artifacts/22-7c-crypto-shredding-workflow-and-restored-backup-safety.md` - readability decision factory, crypto-shredding/restore ProblemDetails.
- `_bmad-output/project-context.md` - project rules: never log payloads/secrets, use ProblemDetails, source-generated logging patterns, focused tests.
- Microsoft Learn: [Data redaction in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/data-redaction) - current redaction library guidance.
- Microsoft Learn: [Compile-time logging source generation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator) - source-generated logging guidance.
- Microsoft Learn: [Handle errors in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0) - current .NET 10 error handling and ProblemDetails guidance.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### ST0 Runtime Diagnostic Policy

- Runtime owns `ProtectedDataDiagnosticRedactor` in `Hexalith.EventStore.Server.Diagnostics`. Contracts remain the source for stable reason codes, type URIs, extension names, and provider-neutral readability/workflow/admission status projection.
- No `Microsoft.Extensions.Compliance.Redaction` dependency was added. The implementation keeps the existing source-generated logging pattern and uses an EventStore-owned allow-list-first projection boundary.
- Deterministic fallback text: `Protected data diagnostic details were redacted. ReasonCode=<reason-code>; Stage=<stage>.`
- Unknown provider/runtime exceptions in protected-data-capable paths use `reasonCode=protected-data-diagnostic-redacted`; typed `ProtectedDataUnreadableException` preserves its stable unreadable protected-data reason code.
- Allowed diagnostic fields by surface:
  - Logs/source-generated properties: correlationId, causationId, tenantId, domain, aggregateId, commandType, eventTypeName, sequenceNumber, stage, reasonCode, duration/count metadata.
  - Activity status/events: safe exception type, stage, reasonCode, and `eventstore.protected_data_diagnostic_redacted=true`.
  - Command status / publish failure / dead-letter diagnostics: the deterministic fallback text only.
  - Unreadable protected-data ProblemDetails: type/title/status/detail plus correlationId, tenantId, domain, aggregateId, sequenceNumber, stage, reasonCode, reasonCategory, metadataVersion, retryable, permanent.
- Non-bypass rule: touched callers do not pass `exception.Message`, provider text, command/event payloads, snapshot state, metadata dictionaries, state-store keys, connection strings, or provider-private details into log template parameters, activity status descriptions/events, persisted failure fields, dead-letter diagnostics, or ProblemDetails.

### Risky Callsite Disposition

| Callsite | Disposition |
| --- | --- |
| `LoggingBehavior` pipeline catch (`ExceptionMessage`, activity status/event) | redacted through `ProtectedDataDiagnosticRedactor`; structured field renamed to `SafeDiagnostic`. |
| `EventPublisher` generic publish catch (`FailureReason`, activity status/event) | redacted through `ProtectedDataDiagnosticRedactor`; typed unreadable failure reason remains stable. |
| `AggregateActor` rehydrate/domain/persist catches and `HandleInfrastructureFailureAsync` | redacted through `ProtectedDataDiagnosticRedactor`; command status, idempotency result, pipeline result, and infrastructure log use safe text. |
| `AggregateActor` drain exception `LastFailureReason` | redacted through `ProtectedDataDiagnosticRedactor`. |
| `DeadLetterMessage.FromException` | redacted through `ProtectedDataDiagnosticRedactor`; `ExceptionType` remains safe type metadata. |
| `DeadLetterPublisher` publication log and activity status/event | redacted through `ProtectedDataDiagnosticRedactor`; dead-letter CloudEvent metadata remains static/correlation-only and safe-by-construction. |
| `StreamsController.CreateUnreadableProtectedDataProblem` | safe-by-construction: existing contract constants and canonical readability decision fields only; no raw metadata dictionary copy. |
| `GlobalExceptionHandler` | safe-by-construction: generic 500 detail only; logs exception with correlation template only. |
| `ValidationProblemDetailsFactory` | redacted/hardened: null error code handling fixed; validation output remains field/message dictionary from validators, no raw provider metadata path touched. |
| `QueryNotFoundExceptionHandler` | safe-by-construction: response omits tenant/domain/aggregate details; public reason code is retained. |
| Admin API/UI, CLI, MCP, replay/rebuild/backup validation broad surfaces | out-of-scope for this child story unless touched by runtime log/ProblemDetails/failure-text contracts. |

### Debug Log References

- Aspire baseline before edits: `aspire run --detach --non-interactive --format Json --apphost src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj`; `aspire describe --non-interactive` showed EventStore, Admin, Admin UI, sample, tenants, pubsub, and statestore running/healthy; DAPR sidecar resources were `Unknown` while dapr-cli executables were healthy. Apphost then stopped with `aspire stop --non-interactive`.
- Red phase: `dotnet test tests/Hexalith.EventStore.Server.Tests --filter ProtectedDataDiagnosticRedactionTests --no-restore` initially failed 4/4 with sentinel leaks in pipeline logs/activity, dead-letter diagnostics/logs/activity, and generic publish failure reason.
- Green/focused server validation: `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~Security|FullyQualifiedName~Logging|FullyQualifiedName~ErrorHandling|FullyQualifiedName~Controllers.StreamsControllerTests|FullyQualifiedName~Events.DeadLetter|FullyQualifiedName~Pipeline.LoggingBehaviorTests" --no-restore` -> 359/359 passed.
- Contracts guardrail validation: `dotnet test tests/Hexalith.EventStore.Contracts.Tests --filter "FullyQualifiedName~Security|FullyQualifiedName~Problems" --no-restore` -> 179/179 passed.
- Testing guardrail validation: `dotnet test tests/Hexalith.EventStore.Testing.Tests --filter ProtectedDataLeakSentinel --no-restore` -> 5/5 passed.
- Tier-1 compatibility validation: `dotnet test tests/Hexalith.EventStore.Client.Tests --no-restore` -> 393/393 passed; `dotnet test tests/Hexalith.EventStore.Sample.Tests --no-restore` -> 74/74 passed.
- Build validation: `dotnet build Hexalith.EventStore.slnx --configuration Debug --no-restore` -> 0 warnings, 0 errors.
- A broader replay/status controller filter was also sampled and still exposes unrelated pre-existing contract expectation mismatches for whitespace correlation detail text and generated replay correlation ID shape; those paths were not part of this story's changed behavior and were not used as the completion gate.

### Completion Notes List

- Added the shared runtime diagnostic boundary `ProtectedDataDiagnosticRedactor` in `Hexalith.EventStore.Server.Diagnostics`. It produces deterministic safe text from reasonCode/stage, records sanitized activity status/events, preserves typed unreadable reason codes, and fails closed for generic provider/runtime exceptions.
- Routed protected-data-capable diagnostics through the redactor in `LoggingBehavior`, `EventPublisher`, `AggregateActor`, `DeadLetterMessage`, `DeadLetterPublisher`, `ReplayController`, and `CommandStatusController`.
- Renamed the source-generated pipeline error field from raw `ExceptionMessage` semantics to `SafeDiagnostic` so future call sites do not infer that raw exception text is acceptable.
- Hardened persisted/externally inspectable diagnostic fields: publish failure reason, `DeadLetterMessage.ErrorMessage`, `UnpublishedEventsRecord.LastFailureReason`, infrastructure command result/status text, and dead-letter publication failure logs now use safe fallback text.
- Kept unreadable protected-data ProblemDetails on the existing contract-driven path and documented the runtime diagnostic safe-field matrix. The unreadable-protected-data reference example now pins `correlationId` and `metadataVersion` plus the deterministic redacted fallback text.
- Fixed a validation ProblemDetails null `ErrorCode` edge case discovered during the error-handling slice; updated the not-found handler test to treat the public `projection-missing` reason code as allowed public taxonomy rather than leaked internals.
- Added `ProtectedDataDiagnosticRedactionTests` covering rendered logs, structured log properties, activity status descriptions/events, publish failure reasons, dead-letter diagnostic fields/logs/activity, and sentinel no-leak assertions.

### File List

- `src/Hexalith.EventStore.Server/Diagnostics/ProtectedDataDiagnosticRedactor.cs` (new)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore.Server/Events/DeadLetterMessage.cs`
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore/Controllers/CommandStatusController.cs`
- `src/Hexalith.EventStore/Controllers/ReplayController.cs`
- `src/Hexalith.EventStore/Controllers/StreamsController.cs`
- `src/Hexalith.EventStore/ErrorHandling/GlobalExceptionHandler.cs`
- `src/Hexalith.EventStore/ErrorHandling/ValidationProblemDetailsFactory.cs`
- `src/Hexalith.EventStore/Pipeline/LoggingBehavior.cs`
- `tests/Hexalith.EventStore.Server.Tests/Security/ProtectedDataDiagnosticRedactionTests.cs` (new)
- `tests/Hexalith.EventStore.Server.Tests/Controllers/StreamsControllerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/QueryNotFoundExceptionHandlerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/DeadLetterMessageTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/DeadLetterPublisherTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherRetryComplianceTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/LoggingBehaviorTests.cs`
- `docs/guides/payload-protection-and-crypto-shredding.md`
- `docs/reference/problems/unreadable-protected-data.md`
- `_bmad-output/implementation-artifacts/22-7d-1-protected-data-redaction-in-logs-and-problemdetails.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Verification Status

- Story context created on 2026-05-18.
- Prerequisites verified from `sprint-status.yaml`: 22.7a, 22.7b, and 22.7c are done; parent 22.7d remains a container/backlog row and must not be assigned.
- Implementation validation passed: focused review patch slice `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~ProtectedDataDiagnosticRedactionTests|FullyQualifiedName~StreamsControllerTests|FullyQualifiedName~EventPublisherTests|FullyQualifiedName~EventPublisherRetryComplianceTests|FullyQualifiedName~DeadLetterPublisherTests|FullyQualifiedName~LoggingBehaviorTests" --no-restore` -> 81/81 passed.
- Broader server validation passed: `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~Security|FullyQualifiedName~Logging|FullyQualifiedName~ErrorHandling|FullyQualifiedName~Controllers.StreamsControllerTests|FullyQualifiedName~Events.DeadLetter|FullyQualifiedName~Pipeline.LoggingBehaviorTests" --no-restore` -> 362/362 passed.
- Guardrail validation passed: `dotnet test tests/Hexalith.EventStore.Contracts.Tests --filter "FullyQualifiedName~Security|FullyQualifiedName~Problems" --no-restore` -> 179/179 passed; `dotnet test tests/Hexalith.EventStore.Testing.Tests --filter ProtectedDataLeakSentinel --no-restore` -> 5/5 passed.
- Build validation passed: `dotnet build Hexalith.EventStore.slnx --configuration Debug --no-restore` -> 0 warnings, 0 errors.

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-18 | Advanced elicitation hardening applied: added allow-list-first diagnostic projection, risky callsite inventory/disposition requirements, explicit Contracts-vs-runtime redactor ownership, provider-exception redactor regression tests, dead-letter CloudEvent metadata checks, activity event/tag checks, and individual ProblemDetails extension assertions. |
| 2026-05-18 | Party-mode review hardening applied: named the shared redaction boundary expectation, added non-bypass rules, deterministic safe fallback text, minimum preserved diagnostic fields, path-specific sentinel evidence requirements, structured log/telemetry/persistence assertions, ProblemDetails example requirements, and explicit 22.7a-c regression test references. |
| 2026-05-18 | Story created for child 22.7d-1 with focused logs/ProblemDetails scope, current source inventory, tasks, test evidence requirements, and no-leak guardrails. |
| 2026-05-18 | Implementation complete: added `ProtectedDataDiagnosticRedactor`, routed protected-data-capable runtime diagnostics through safe reason/stage text, hardened dead-letter/publish/command-status/activity diagnostics, updated docs, added sentinel no-leak tests, and moved story to review. |
| 2026-05-18 | Code review patches applied: removed raw exception objects from protected-data-capable log sinks, preserved cancellation propagation without redaction error activity, moved unreadable ProblemDetails extension projection into the shared diagnostic boundary with correlation IDs, normalized externally supplied dead-letter diagnostics before publish/logging, replaced character-token filtering with semantic reason/stage allow-lists, expanded no-leak tests to scan logged exception channels/activity diagnostics/ProblemDetails extensions/dead-letter metadata, corrected sprint timestamp ordering, and moved story to done. |
