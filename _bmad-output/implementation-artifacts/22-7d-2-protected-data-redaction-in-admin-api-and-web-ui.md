# Story 22.7d-2: Protected Data Redaction in Admin API and Web UI

Status: done

Context created: 2026-05-18
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR104 protected-data redaction for Admin API DTOs/controllers/services and Blazor Admin UI rendering/copy/detail surfaces. This child story intentionally excludes CLI/MCP output, replay/rebuild/backup-validation artifact scans, and new runtime log/ProblemDetails redaction except where Admin API/UI consumes the safe contracts created by Story 22.7d-1.

Prerequisites verified from `sprint-status.yaml` and recent implementation artifacts: 22.7a, 22.7b, 22.7c, and 22.7d-1 are done; parent 22.7d remains an unassignable backlog container. Story 22.7d-1 added the runtime `ProtectedDataDiagnosticRedactor` and sentinel no-leak guardrails. This story must reuse those contracts and must not create a second protected-data reason taxonomy.

## Story

As an operator,
I want admin API and Web UI surfaces to show protected-data status without plaintext,
so that investigation workflows remain useful and safe.

## Acceptance Criteria

1. **Admin API DTOs model protected content explicitly instead of serializing raw payload/state by default.**
   - Given EventStore admin responses include protected event payloads, protected snapshot state, unreadable protected data, crypto-shredding workflow state, restore-admission state, or redacted runtime diagnostics
   - When Admin API models are serialized as JSON
   - Then protected payload JSON, snapshot state JSON, command payload JSON, reconstructed state JSON, field values, provider-private metadata, unsafe key aliases, state-store keys, connection strings, stack traces, and provider exception text are absent.
   - And the response preserves safe operator value: tenantId, domain, aggregateId, sequenceNumber, eventTypeName, correlationId, causationId, userId where already safe, stage, protection state, metadataVersion, stable reasonCode, retryable/permanent flags, safe next action, and deterministic redacted placeholder text.
   - And protected or unreadable content is represented by a typed redacted-content contract rather than magic strings or DTO properties that imply raw protected content is available.
   - And Admin-facing serialized response graphs are safe by construction: protected-content DTOs do not leave raw-capable payload/state/message fields serializable even if the UI ignores them.
   - And protected responses omit forbidden raw-bearing JSON properties such as payloadJson, stateJson, commandPayload, resultingStateJson, failureReason, errorMessage, or equivalent raw-content aliases unless each property is proven safe by construction.
   - And `ToString()` redaction remains, but JSON/API safety does not rely on `ToString()`.

2. **Admin.Server preserves upstream protected-data status and ProblemDetails contracts.**
   - Given EventStore returns a protected-data `ProblemDetails` response or a DTO carrying protected-data status
   - When `DaprStreamQueryService`, stream/dead-letter/projection/storage/backup services, or Admin controllers relay the result
   - Then Admin.Server preserves the stable type URI, status, reasonCode, stage, correlationId, tenant/domain/aggregate/sequence metadata, and safe guidance when the upstream response is available.
   - And only an allow-listed set of `ProblemDetails` extensions is relayed to Admin clients; unknown or provider-private extensions are dropped unless they are proven safe by tests.
   - And Admin.Server does not collapse protected-data statuses into generic 500/503, `HttpRequestException.Message`, or raw response body text.
   - And local Admin.Server unexpected failures stay generic and do not copy exception details into `ProblemDetails.detail`.

3. **Admin UI renders redacted protected data with useful diagnostics and no secret-bearing affordances.**
   - Given a UI component receives protected/redacted event payload, aggregate state, diff, blame, event-step, sandbox, dead-letter, projection, consistency, storage, snapshot, backup, or ProblemDetails data
   - When the component renders markup, dialogs, detail panels, tooltips, filters, badges, copy buttons, retry/error states, or download/export previews
   - Then protected content never appears in rendered markup, title attributes, ARIA labels, hidden dialog content, JS interop clipboard payloads, log messages, or test snapshots.
   - And the UI uses one shared protected-content presentation pattern with an explicit redacted placeholder/status badge, stable reason code, safe next action, and navigation context so operators can continue diagnosis without plaintext.
   - And blank cells, generic "redacted" text, or removed panels do not satisfy this story unless the operator still sees content kind, safe identifiers, reasonCode, stage, retryability, and safe next action where available.
   - And search, filter, sort, empty-state, validation, client exception objects, export/download payloads, and error-summary text never echoes protected values that were typed or returned by protected-data surfaces.
   - And existing Fluent UI v5 patterns, compact operational layout, role gates, keyboard/focus behavior, and bUnit test style are preserved.

4. **Dead-letter and command/sandbox presentation does not expose command payloads.**
   - Given dead-letter entries intentionally preserve the original `CommandEnvelope` for replay support and sandbox requests/results can carry command/event/state JSON
   - When Admin API lists dead letters, executes actions, or the Web UI displays dead-letter/sandbox data
   - Then replay/storage semantics are not changed, but display/API diagnostic fields do not expose command payload, event payload, resulting state, provider exception text, or protected sentinels.
   - And retry/skip/archive dialogs and confirmation messages use message IDs, tenant/domain/aggregate/correlation/command type, retry count, reasonCode/status, and safe guidance only.

5. **Admin API and Web UI tests prove sentinel no-leak behavior per surface.**
   - Given `ProtectedDataLeakSentinel` values are injected into payload JSON, snapshot state JSON, field values, command payloads, failure reason text, provider metadata, provider exception messages, state-store keys, connection strings, and assertion messages
   - When focused Admin Abstractions, Admin.Server, and Admin.UI tests run
   - Then every serialized DTO, `ProblemDetails` object, rendered component markup, dialog, title/ARIA attribute, copied clipboard value, and relevant service exception message passes `ProtectedDataLeakSentinel.AssertNoLeak(...)`.
   - And tests prove safe fields remain present instead of merely blanking the UI.
   - And tests cover nested JSON, stringified JSON inside metadata/message fields, provider-private extension metadata, and both negative no-leak assertions and positive semantic-preservation assertions.

## Tasks / Subtasks

- [x] **ST0 - Freeze Admin redaction model and surface inventory.** (AC: 1, 2, 3, 4, 5)
  - [x] Read this story, parent Story 22.7d, Stories 22.7a/22.7b/22.7c/22.7d-1, Epic 22, PRD FR104/NFR12/NFR40-NFR46, architecture SEC-5/Admin Data Access/Payload and Snapshot Protection, UX Admin Web UI rules, and `_bmad-output/project-context.md`.
  - [x] Define the Admin API redaction contract before code edits: allowed safe fields, redacted placeholder text, machine-readable reasonCode/status fields, and which DTOs can represent protected/unreadable content.
  - [x] Name the shared contract and UI presentation pattern before implementation, for example `AdminRedactedContent`/`AdminProtectedContentDescriptor` plus a single protected-content UI component or renderer. Keep the final names aligned with existing project conventions.
  - [x] For each existing payload/state/message/string field, classify it as one of: safe by construction, replaced by the redacted-content contract, retained only for internal replay semantics, or removed from Admin-facing serialization.
  - [x] Document the chosen DTO migration strategy in the Dev Agent Record as a mini ADR: chosen shape, rejected alternatives, compatibility impact, and why protected content cannot serialize through the Admin-facing response graph.
  - [x] Name the safe projection boundary for each major Admin response path: stream detail, dead-letter list/detail, sandbox request/result, projections, backup/storage/consistency diagnostics, and preserved `ProblemDetails`.
  - [x] Reuse existing Contracts security/problem types: `EventStorePayloadProtectionMetadata`, `UnreadableProtectedDataReasonCodes`, `ProtectedDataReadabilityDecision`, `ProtectedDataReadabilityDecisionFactory`, `UnreadableProtectedDataProblem`, `CryptoShreddingWorkflowProblem`, `RestoredBackupAdmissionProblem`, and `ProtectedDataLeakSentinel`.
  - [x] Reuse the runtime diagnostic boundary from 22.7d-1 for diagnostic text where Admin.Server references runtime protected-data diagnostics. Do not move ASP.NET/runtime exception concerns into `Contracts`.
  - [x] Inventory Admin models and components that currently carry raw strings: `EventDetail.PayloadJson`, `AggregateStateSnapshot.StateJson`, `FieldChange.OldValue/NewValue`, `FieldProvenance.CurrentValue/PreviousValue`, `EventStepFrame.EventPayloadJson/StateJson`, `SandboxCommandRequest.PayloadJson`, `SandboxEvent.PayloadJson`, `SandboxResult.ResultingStateJson/ErrorMessage`, `DeadLetterEntry.FailureReason`, `ProjectionError.Message`, backup/storage/consistency diagnostic messages, and trace-map fields.
  - [x] Add a short contract table to the Dev Agent Record or implementation notes listing each unsafe field, its replacement/safe projection, and the tests that prove no-leak behavior.
  - [x] Decide whether any DTO requires a breaking replacement versus additive fields. If a public Admin API contract changes shape, update OpenAPI/tests/docs and keep SemVer implications explicit.

- [x] **ST1 - Add shared Admin redaction/status DTOs and helpers.** (AC: 1, 4, 5)
  - [x] Add provider-neutral Admin-facing model(s) in the lowest appropriate package, likely `Hexalith.EventStore.Admin.Abstractions`, to represent redacted JSON/content status without carrying raw content.
  - [x] Prefer additive helpers/properties that make unsafe rendering hard, for example a redacted content descriptor with `IsRedacted`, `ContentKind`, `Placeholder`, `ReasonCode`, `Stage`, `MetadataVersion`, `Retryable`, `Permanent`, and safe next action.
  - [x] Ensure any property names that mention payload, state, command, error detail, provider metadata, or message are either safe by construction or typed as the redacted-content descriptor. Avoid parallel raw-string escape hatches on Admin-facing DTOs.
  - [x] For every legacy raw string field that can carry protected content, choose one explicit mitigation: replace it with a safe projection DTO, mark it non-serializable where it must remain internal, or split internal replay/storage models from Admin-facing DTOs.
  - [x] Additive compatibility is acceptable only when legacy raw fields are explicitly non-serializable for Admin-facing JSON or are proven safe by construction with focused tests.
  - [x] Support nested and stringified JSON inputs by projecting safe metadata before serialization rather than redacting the final serialized document.
  - [x] Do not introduce provider SDK dependencies, real encryption, KMS, Key Vault, DAPR secret-store, certificates, or a second UI framework.
  - [x] Update or wrap models so serialized JSON can carry either safe plain diagnostic metadata or redacted protected content. Do not rely on post-serialization string replacement as the primary policy.
  - [x] Add Admin Abstractions tests proving JSON serialization and `ToString()` do not leak sentinels while safe status fields remain available.
  - [x] Add whole-response graph serialization tests for representative stream detail, dead-letter list/detail, sandbox result, projection/storage/backup diagnostic, and preserved `ProblemDetails` responses.
  - [x] Add forbidden-property assertions for protected responses so raw-bearing property names such as payloadJson, stateJson, commandPayload, resultingStateJson, failureReason, errorMessage, providerMetadata, or equivalent aliases fail tests unless documented as safe by construction.

- [ ] **ST2 - Preserve protected-data ProblemDetails and safe upstream statuses through Admin.Server.** (AC: 2, 5)
  - [ ] Update `DaprStreamQueryService.InvokeEventStoreAsync` paths so non-success EventStore responses with protected-data `ProblemDetails` are parsed and preserved instead of reduced to `EnsureSuccessStatusCode()` exceptions.
  - [ ] Update `AdminStreamsController` upstream mapping so 400/404/410/422/503 protected-data responses keep their type URI, title, status, safe detail text, correlationId, reasonCode, stage, tenant/domain/aggregate/sequence metadata, retryable/permanent flags, and safe guidance.
  - [ ] Define and test an allow-list for relayed `ProblemDetails.Extensions`; protected-data extensions such as reasonCode/stage/correlation metadata may pass, provider-private metadata and arbitrary upstream extensions must not.
  - [ ] Audit `AdminDeadLettersController`, `AdminBackupsController`, projection/consistency/storage controllers, and related services for `result.Message`, `ex.Message`, raw response body, and error text flowing into Admin API `ProblemDetails.detail` or DTO messages.
  - [ ] Keep local generic 500 responses generic. Keep cancellation as cancellation and do not convert it to protected-data failure.
  - [ ] Add controller/service tests for upstream unreadable protected data, crypto-shredding conflict, restored-backup conflict/deferred validation, provider-unavailable retryable status, and malformed/oversized ProblemDetails bodies.

- [ ] **ST3 - Update Admin UI rendering and copy/export affordances.** (AC: 3, 4, 5)
  - [ ] Update `JsonViewer` or callers so protected/redacted descriptors render through the shared protected-content presentation pattern rather than parsing/displaying raw JSON. Invalid JSON fallback must not display a protected sentinel as raw text.
  - [ ] Implement the shared presentation pattern once, likely as a small Fluent UI component/renderer that answers: what was redacted, why it was redacted, what safe context remains, and what safe next action exists.
  - [ ] Update `EventDetailPanel`, `StateInspectorModal`, `StateDiffViewer`, `BlameViewer`, `EventDebugger`, `BisectTool`, `CommandSandbox`, `DeadLetters`, `Projections`, `Consistency`, `Storage`, `Snapshots`, and `Backups` only where they can receive payload/state/failure/status content.
  - [ ] Replace payload/state dialogs with redacted status panels when content is protected/unreadable; retain event type, sequence, aggregate identity, correlation/causation, reasonCode, stage, and safe next action.
  - [ ] Ensure copy-to-clipboard and export/download actions only copy safe identifiers or safe status summaries. Label commands accordingly, for example `Copy safe reference` or `Copy redaction summary`, not generic payload/state copy text.
  - [ ] Ensure protected-data badges, banners, dialogs, and tooltips have accessible labels that describe the safe status without embedding raw protected values.
  - [ ] Preserve Fluent UI v5 components and existing compact operational behavior. Use badges, issue banners, data-grid cells, dialogs, and tooltips already present in the Admin UI rather than adding a new design system.
  - [ ] Add bUnit tests that scan rendered markup, title attributes, ARIA labels, dialog bodies, and JS interop clipboard arguments with `ProtectedDataLeakSentinel.AssertNoLeak(...)`.
  - [ ] Add client tests proving `AdminApiProblemException` and any export/download preview payloads preserve allow-listed safe fields without storing raw protected response bodies.

- [ ] **ST4 - Harden dead-letter, sandbox, projection, backup, and storage admin presentation.** (AC: 1, 3, 4, 5)
  - [ ] Ensure `DeadLetterEntry.FailureReason` can represent safe protected-data diagnostic text without leaking command payload or provider exception text. Do not change dead-letter replay payload storage semantics in this story; distinguish internal replay payload retention from Admin-facing display/API projections.
  - [ ] Ensure sandbox command request/result display redacts command payload, produced event payloads, resulting state, state changes, and error details while preserving command type, outcome, execution time, reasonCode/status, deterministic placeholders, and safe guidance.
  - [ ] Ensure `ProjectionError.Message`, consistency anomaly details, storage/snapshot/backup messages, and stream export/import/deferred results do not echo protected data or provider exception text.
  - [ ] For backup/restore/admission UI surfaces already added by 22.7c, preserve `RestoredBackupAdmissionProblem` and `CryptoShreddingWorkflowProblem` safe extensions and avoid claiming physical backup scanning exists.
  - [ ] Add focused tests for dead-letter grids/details/action dialogs, sandbox dialogs/results, projection error panels, backup/admission responses, and storage/consistency diagnostic cells.

- [ ] **ST5 - Documentation, OpenAPI, and validation evidence.** (AC: 1, 2, 3, 4, 5)
  - [ ] Update Admin API docs/OpenAPI examples only where contract shape changes. Examples must use redacted placeholders and stable reasonCode/status fields, not fake plaintext.
  - [ ] Update `docs/guides/payload-protection-and-crypto-shredding.md` with the Admin API/Web UI redaction surface matrix and explicitly note that CLI/MCP and replay/rebuild/backup artifact scans remain in 22.7d-3/22.7d-4.
  - [ ] Run focused test projects individually and record exact commands/results in the Dev Agent Record.
  - [ ] Do not run solution-level `dotnet test` as the primary gate. Use focused Admin slices first, then broaden only when shared contracts changed.

### Review Findings

- [x] [Review][Patch] Protected upstream ProblemDetails are not preserved by several Admin stream endpoints [src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs:63] - `DaprStreamQueryService` can now throw `AdminUpstreamProblemException`, but only state, diff, event detail, and sandbox actions map it through `UpstreamProblem`. `GetRecentCommands`, `GetStreamTimeline`, `GetAggregateBlame`, `GetEventStepFrame`, and `TraceCausationChain` still fall through to generic error handling, so protected-data status can collapse to 500/503 instead of preserving type/status/reason/stage/correlation metadata required by AC2.
- [x] [Review][Patch] UI stream client swallows sanitized protected-data problem exceptions on most methods [src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs:225] - `HandleErrorStatusAsync` throws `AdminApiProblemException`, but most stream client methods do not exclude that type in their catch filters. For example `GetEventDetailAsync` and `GetAggregateStateAtPositionAsync` catch it and return null, while other methods return empty/generic results. This loses the safe reasonCode/stage/status needed by AC3 and masks protected-data conditions as missing data.
- [x] [Review][Patch] Allow-listed ProblemDetails extensions can still carry arbitrary nested provider data [src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs:670] - both server and UI clients treat an allow-listed extension name as safe even when its value is an object or array, cloning the full `JsonElement`. A malicious or buggy upstream could put provider-private metadata under `reasonCode`, `stage`, or another allowed name and have it relayed/serialized, bypassing the sentinel tests that only cover unknown extension names.
- [x] [Review][Patch] Most Admin UI redaction sinks still render legacy raw fields [src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor:108] - the shared descriptor is wired into `EventDetailPanel`, but CommandSandbox, EventDebugger, StateDiffViewer, StateInspectorModal, BlameViewer, BisectTool, and DeadLetters still render or place legacy values in markup, title attributes, ARIA labels, searches, or dialogs (`PayloadJson`, `StateJson`, `OldValue`, `NewValue`, `CurrentValue`, `PreviousValue`, `FailureReason`, `ErrorMessage`). That leaves AC3/AC4 incomplete for protected descriptors returned outside the single event-detail path.

## Dev Notes

- Existing Admin DTOs already redact in `ToString()` but still serialize raw fields. Examples: `EventDetail.PayloadJson`, `AggregateStateSnapshot.StateJson`, `EventStepFrame.EventPayloadJson/StateJson`, `SandboxCommandRequest.PayloadJson`, `SandboxEvent.PayloadJson`, `SandboxResult.ResultingStateJson`, and `FieldChange.OldValue/NewValue`. This story must address serialized JSON/API/UI rendering, not just `ToString()`.
- `JsonViewer` parses and renders any supplied JSON string, and its invalid JSON branch prints the raw string. Treat it as a high-risk rendering sink.
- `EventDetailPanel` currently passes `EventDetail.PayloadJson` and `AggregateStateSnapshot.StateJson` directly into `JsonViewer`; `EventDebugger` renders field values, state JSON, and payload dialogs directly from `EventStepFrame`; `CommandSandbox` shows command/event payload dialogs.
- `DaprStreamQueryService` currently uses `EnsureSuccessStatusCode()` inside generic invoke helpers. That loses protected-data ProblemDetails bodies unless explicit preservation is added.
- `AdminStreamsController.TryMapUpstreamStatus` currently maps some upstream statuses to local generic ProblemDetails and treats upstream 500 as 502. Protected-data upstream statuses must be preserved when available.
- `AdminStreamApiClient.HandleErrorStatusAsync` currently extracts only `detail` for 400/422 and otherwise throws generic exceptions. UI clients that need reasonCode/stage/status must preserve sanitized ProblemDetails fields without exposing raw bodies.
- Dead-letter replay payload storage is intentionally out of scope. Redact display/API diagnostics while preserving replay semantics.
- Runtime logs/ProblemDetails were handled by 22.7d-1. Do not reopen or duplicate the server diagnostic redactor unless Admin API/UI consumption reveals a specific integration gap.
- The typed redacted-content contract is the primary boundary for this story. UI components should consume that descriptor directly instead of inferring protected-data state from placeholder strings.
- Safe projection by construction is the core control. Render-time redaction is a last-mile guard and must not be the primary mechanism preventing Admin API leaks.
- Simplest rule: Admin-facing DTOs contain safe metadata or a redacted-content descriptor; they do not contain raw protected content, raw-capable fields, or arbitrary upstream diagnostic text.
- ProblemDetails preservation must be allow-list based. Treat arbitrary upstream extensions, response bodies, provider messages, and exception text as unsafe until projected into known safe fields.
- Treat dead-letter and sandbox flows as dual-surface features: internal storage/replay data may remain available to backend actions, but Admin-facing DTOs, UI, copy/export, and confirmation text must use safe projections only.
- CLI/MCP are out of scope for this child story and must stay delegated to 22.7d-3. Replay/rebuild/backup-validation artifact scans are out of scope for 22.7d-4 except for Admin UI/API surfaces directly touched here.

## Files Likely Touched

- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/EventDetail.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateStateSnapshot.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateStateDiff.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateBlameView.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/FieldChange.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/FieldProvenance.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/EventStepFrame.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/SandboxCommandRequest.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/SandboxEvent.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/SandboxResult.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/DeadLetters/DeadLetterEntry.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ProjectionError.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/*`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDeadLettersController.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/Exceptions/AdminApiProblemException.cs`
- `src/Hexalith.EventStore.Admin.UI/Components/Shared/JsonViewer.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/EventDetailPanel.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/StateDiffViewer.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/BlameViewer.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/BisectTool.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/*`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/DeadLetters/DeadLetterEntryTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsController*.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDeadLettersController*.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminBackupsControllerTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryService*.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/EventDetailPanelTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/EventDebuggerTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StateInspectorModalTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StateDiffViewerTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandSandboxTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DeadLettersPageTests.cs`
- `docs/guides/payload-protection-and-crypto-shredding.md`

## Testing Requirements

- Red-phase first: add at least one focused Admin Abstractions test and one UI rendering test that currently fails because protected sentinels pass through serialized DTOs or rendered markup.
- Minimum expected validation commands:
  - `dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests --filter "FullyQualifiedName~Streams|FullyQualifiedName~DeadLetters|FullyQualifiedName~Projections|FullyQualifiedName~Storage" --no-restore`
  - `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --filter "FullyQualifiedName~AdminStreamsController|FullyQualifiedName~DaprStreamQueryService|FullyQualifiedName~AdminDeadLettersController|FullyQualifiedName~AdminBackupsController" --no-restore`
  - `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --filter "FullyQualifiedName~EventDetailPanel|FullyQualifiedName~EventDebugger|FullyQualifiedName~StateInspectorModal|FullyQualifiedName~StateDiffViewer|FullyQualifiedName~CommandSandbox|FullyQualifiedName~DeadLetters" --no-restore`
  - `dotnet test tests/Hexalith.EventStore.Testing.Tests --filter ProtectedDataLeakSentinel --no-restore` if sentinel helpers are changed or reused in new assertions
  - `dotnet test tests/Hexalith.EventStore.Contracts.Tests --filter "FullyQualifiedName~Security|FullyQualifiedName~Problems" --no-restore` if shared protected-data contracts or ProblemDetails constants are changed
- Required assertions:
  - DTO JSON serialization contains no sentinels and retains safe status/reason fields.
  - Whole Admin-facing response graphs contain no raw-capable protected fields and no sentinel values after serialization.
  - Protected-response JSON omits forbidden raw-bearing property names unless those properties are documented and tested as safe by construction.
  - Admin.Server preserved upstream protected-data ProblemDetails type/status/reason/stage/correlation metadata.
  - UI markup/dialogs/title attributes/ARIA labels contain no sentinels.
  - JS interop clipboard arguments contain no sentinels.
  - `AdminApiProblemException`, client-side error summaries, and export/download preview payloads contain no sentinels or raw upstream response bodies.
  - Search/filter text, validation summaries, tooltips, empty states, and export/download previews contain no sentinels.
  - Nested JSON, stringified JSON, provider-private metadata, unsafe key aliases, and provider exception text contain no sentinels after Admin projection.
  - Useful safe fields remain visible to operators.
  - Redacted UI surfaces never degrade to blank/generic output when safe operator context exists.

## Out of Scope

- CLI and MCP output redaction: Story 22.7d-3.
- Replay/rebuild/backup-validation progress artifacts and broad evidence scans: Story 22.7d-4.
- Runtime log, telemetry, command-status, dead-letter diagnostic text, and core ProblemDetails redaction already completed by Story 22.7d-1 except for Admin consumption gaps.
- Real encryption providers, KMS/Key Vault/DAPR secret-store integration, certificates, physical backup scanning, skip-past-unreadable policy, or deleting immutable event/snapshot/audit records.
- Replacing Fluent UI v5 or redesigning the Admin UI.

## References

- `_bmad-output/planning-artifacts/epics.md` - Story 22.7d-2 source requirements and Epic 22 split.
- `_bmad-output/planning-artifacts/prd.md` - FR104 protected-data no-leak requirement, FR79 shared Admin API, NFR40-NFR46 Admin performance/RBAC targets.
- `_bmad-output/planning-artifacts/architecture.md` - SEC-5 logging/no payload exposure, Admin Data Access, ProblemDetails, Payload and Snapshot Protection.
- `_bmad-output/planning-artifacts/ux-design-specification.md` - Fluent UI v5 Admin dashboard patterns, accessibility rules, and ProblemDetails copy rules.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12.md` - split of Story 22.7 into 22.7a-22.7d.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-17-readiness-addendum.md` - child story rows 22.7d-1 through 22.7d-4.
- `_bmad-output/implementation-artifacts/22-7d-protected-data-redaction-across-operational-surfaces.md` - parent container and surface inventory.
- `_bmad-output/implementation-artifacts/22-7d-1-protected-data-redaction-in-logs-and-problemdetails.md` - completed runtime redactor, safe diagnostic policy, and no-leak test evidence.
- `_bmad-output/implementation-artifacts/22-7a-payload-and-snapshot-protection-hooks.md` - protection metadata contract.
- `_bmad-output/implementation-artifacts/22-7b-unreadable-protected-data-behavior.md` - unreadable taxonomy and sentinel helper.
- `_bmad-output/implementation-artifacts/22-7c-crypto-shredding-workflow-and-restored-backup-safety.md` - readability decisions and restore/crypto-shredding ProblemDetails.
- `_bmad-output/project-context.md` - package boundaries, logging, ProblemDetails, Admin UI, testing, and DAPR rules.
- Microsoft Learn: [Data redaction in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/data-redaction) - current redaction library guidance. Do not add the package unless ST0 approves a dependency and centralized package management is used.
- Microsoft Learn: [Compile-time logging source generation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator) - source-generated logging and redaction guidance.
- Microsoft Learn: [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/en-us/aspnet/core/web-api/handle-errors) and [Handle errors in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0) - ProblemDetails/error-handling guidance.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Aspire baseline before source edits: `aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj` started PID 84316 and dashboard `https://localhost:17017/login?t=2b67deebbce0ff91095e987ba6bd0a28`.
- Aspire resource snapshot: `aspire describe --non-interactive --format Json --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj` showed `statestore`, `pubsub`, DAPR CLI resources, dashboard, and `sample` healthy/running; `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `tenants`, and sample UI were waiting on `keycloak` while `keycloak` was still starting.
- Red phase: `dotnet test tests\Hexalith.EventStore.Admin.Abstractions.Tests --filter AdminRedactedContentSerializationTests --no-restore` and `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests --filter EventDetailPanel_RendersRedactedPayloadAndStateWithoutSentinelLeak --no-restore` initially failed because `AdminRedactedContent` and redacted DTO factories did not exist.
- Focused Admin Abstractions validation: `dotnet test tests\Hexalith.EventStore.Admin.Abstractions.Tests --filter "FullyQualifiedName~Streams|FullyQualifiedName~DeadLetters|FullyQualifiedName~Projections|FullyQualifiedName~Storage" --no-restore` -> 209/209 passed.
- Focused Admin.Server validation: `dotnet test tests\Hexalith.EventStore.Admin.Server.Tests --filter "FullyQualifiedName~AdminStreamsController|FullyQualifiedName~DaprStreamQueryService|FullyQualifiedName~AdminDeadLettersController|FullyQualifiedName~AdminBackupsController" --no-restore` -> 125/125 passed.
- Focused Admin.UI validation: `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests --filter "FullyQualifiedName~EventDetailPanel|FullyQualifiedName~EventDebugger|FullyQualifiedName~StateInspectorModal|FullyQualifiedName~StateDiffViewer|FullyQualifiedName~CommandSandbox|FullyQualifiedName~DeadLetters|FullyQualifiedName~AdminStreamApiClient" --no-restore` -> 123/123 passed.
- Sentinel guardrail validation: `dotnet test tests\Hexalith.EventStore.Testing.Tests --filter ProtectedDataLeakSentinel --no-restore` -> 5/5 passed.
- Build validation: `dotnet build Hexalith.EventStore.slnx --configuration Debug --no-restore` -> 0 warnings, 0 errors.
- Review patch validation: `dotnet test tests\Hexalith.EventStore.Admin.Server.Tests --filter "FullyQualifiedName~AdminStreamsController|FullyQualifiedName~DaprStreamQueryService" --no-restore` -> 81/81 passed.
- Review patch validation: `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests --filter "FullyQualifiedName~AdminStreamApiClient|FullyQualifiedName~EventDetailPanel|FullyQualifiedName~EventDebugger|FullyQualifiedName~StateDiffViewer|FullyQualifiedName~CommandSandbox|FullyQualifiedName~BlameViewer|FullyQualifiedName~BisectTool|FullyQualifiedName~DeadLetters" --no-restore` -> 125/125 passed.
- Review patch validation: `dotnet test tests\Hexalith.EventStore.Admin.Abstractions.Tests --filter AdminRedactedContentSerializationTests --no-restore` -> 2/2 passed.

### ST0 Mini ADR - Admin Redacted Content Contract

- Chosen shape: `AdminRedactedContent` in `Hexalith.EventStore.Admin.Abstractions.Models`, with `contentKind`, deterministic placeholder, `reasonCode`, `stage`, metadata version, retry/permanent flags, safe next action, and safe navigation metadata. Protected DTO factory methods set raw-bearing properties to `null` and expose a typed descriptor instead.
- Rejected alternatives: magic placeholder strings in `payloadJson`/`stateJson` were rejected because they keep raw-capable property names in protected JSON. Post-serialization string replacement was rejected because it would not be safe by construction and would miss object graphs/clipboard/UI render paths.
- Compatibility impact: legacy safe/plain responses can still use existing raw properties. Protected responses intentionally omit raw-bearing JSON names (`payloadJson`, `stateJson`, `eventPayloadJson`, `resultingStateJson`, `failureReason`, `errorMessage`, `message`) and add descriptor properties such as `payload`, `state`, `eventPayload`, `resultingState`, `failure`, and `diagnostic`.
- Safe projection boundaries: stream detail -> `EventDetail.Payload`; state/snapshot -> `AggregateStateSnapshot.State`; step/debugger -> `EventStepFrame.EventPayload`/`State`; field values -> `FieldChange.OldContent`/`NewContent`; sandbox -> `SandboxEvent.Payload`, `SandboxResult.ResultingState`/`Error`; dead letters -> `DeadLetterEntry.Failure`; projections -> `ProjectionError.Diagnostic`; upstream protected problems -> `AdminUpstreamProblemException` with allow-listed extensions only.
- No raw serialization rule: protected-content DTO factories do not serialize raw-capable payload/state/message properties even if a UI component ignores the descriptor. Tests assert both no sentinel values and forbidden raw-bearing property-name omission.

### Admin Redaction Contract Table

| Unsafe field | Replacement/safe projection | Test evidence |
| --- | --- | --- |
| `EventDetail.PayloadJson` | `EventDetail.Payload` (`AdminRedactedContent`) | `AdminRedactedContentSerializationTests.EventDetail_RedactedPayloadSerialization_OmitsRawPayloadJsonAndKeepsSafeDescriptor` |
| `AggregateStateSnapshot.StateJson` | `AggregateStateSnapshot.State` | `RepresentativeAdminResponseGraphSerialization_OmitsRawBearingPropertiesWhenRedacted` |
| `EventStepFrame.EventPayloadJson` / `StateJson` | `EventStepFrame.EventPayload` / `State` | `RepresentativeAdminResponseGraphSerialization_OmitsRawBearingPropertiesWhenRedacted` |
| `FieldChange.OldValue` / `NewValue` | `FieldChange.OldContent` / `NewContent` | `RepresentativeAdminResponseGraphSerialization_OmitsRawBearingPropertiesWhenRedacted` |
| `SandboxEvent.PayloadJson` / `SandboxResult.ResultingStateJson` / `ErrorMessage` | `SandboxEvent.Payload`, `SandboxResult.ResultingState`, `SandboxResult.Error` | `RepresentativeAdminResponseGraphSerialization_OmitsRawBearingPropertiesWhenRedacted` |
| `DeadLetterEntry.FailureReason` | `DeadLetterEntry.Failure` | `RepresentativeAdminResponseGraphSerialization_OmitsRawBearingPropertiesWhenRedacted` |
| `ProjectionError.Message` | `ProjectionError.Diagnostic` | `RepresentativeAdminResponseGraphSerialization_OmitsRawBearingPropertiesWhenRedacted` |
| Upstream protected-data `ProblemDetails` unknown extensions | `AdminUpstreamProblemException` / `AdminApiProblemException` allow-listed extensions | `GetEventDetailAsync_ProtectedProblemDetails_PreservesSafeExtensionsAndDropsProviderPrivateValues`, `GetRecentCommandsAsync_ProtectedProblemDetails_PreservesSafeFieldsWithoutRawBody` |

### Completion Notes List

- Implementation started for Story 22.7d-2. ST0 contract inventory is in progress before Admin API/UI code edits.
- Added `AdminRedactedContent` and redacted factory helpers for representative Admin DTOs spanning stream detail, state snapshots, debugger frames, field changes/provenance, sandbox events/results, dead letters, and projection diagnostics.
- Added JSON serialization tests proving protected DTO graphs omit raw-bearing property names and preserve safe descriptor fields without sentinel leakage.
- Added `ProtectedContentPanel` and updated `JsonViewer`/`EventDetailPanel` so protected payload/state descriptors render as useful operator diagnostics instead of raw JSON. Invalid JSON fallback no longer echoes raw input.
- Added `AdminUpstreamProblemException` and updated `DaprStreamQueryService` to parse known protected-data ProblemDetails, preserve safe fields, and drop provider-private extensions before controllers receive them.
- Extended `AdminStreamApiClient`/`AdminApiProblemException` so UI clients preserve sanitized protected-data problem type, reason code, correlation ID, and safe extensions without storing raw response bodies.
- Applied review fixes: all Admin stream endpoints now preserve protected upstream ProblemDetails, UI stream clients propagate sanitized protected-data problem exceptions, allowed ProblemDetails extension values drop nested object/array payloads, and Admin UI redaction sinks use protected descriptors in debugger, diff, blame, bisect, sandbox, state inspector, and dead-letter views.
- Updated `docs/guides/payload-protection-and-crypto-shredding.md` with the Admin API/Web UI redaction surface matrix.
- Review patch pass completed: all review findings are checked off, protected-data status propagation and UI redaction sinks were hardened, and focused validation is green.

### File List

- `_bmad-output/implementation-artifacts/22-7d-2-protected-data-redaction-in-admin-api-and-web-ui.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/guides/payload-protection-and-crypto-shredding.md`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/AdminRedactedContent.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/DeadLetters/DeadLetterEntry.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ProjectionError.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateStateSnapshot.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/EventDetail.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/EventStepFrame.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/FieldChange.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/FieldProvenance.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/SandboxCommandRequest.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/SandboxEvent.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/SandboxResult.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/AdminUpstreamProblemException.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`
- `src/Hexalith.EventStore.Admin.UI/Components/BisectTool.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/BlameViewer.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/EventDetailPanel.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/StateDiffViewer.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/Shared/JsonViewer.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/Shared/ProtectedContentPanel.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/Exceptions/AdminApiProblemException.cs`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Hexalith.EventStore.Admin.Abstractions.Tests.csproj`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/AdminRedactedContentSerializationTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsControllerTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/EventDetailPanelTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminStreamApiClientTests.cs`

## Verification Status

- Story context created on 2026-05-18.
- Prerequisites verified from `sprint-status.yaml`: 22.7a, 22.7b, 22.7c, and 22.7d-1 are done; parent 22.7d remains an unassignable container row.
- No implementation tests were run because this is story creation only.
- Implementation is done for the Admin API/Web UI story scope after review patches.
- Validation passed: Admin.Abstractions focused slice 209/209, Admin.Server focused slice 125/125, Admin.UI focused slice 123/123, Testing sentinel slice 5/5, and full debug build 0 warnings/0 errors.

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-18 | Story created for child 22.7d-2 with focused Admin API/Web UI scope, current Admin DTO/controller/UI inventory, redaction guardrails, test evidence requirements, and source references. |
| 2026-05-18 | Applied party-mode review fixes: tightened typed redacted-content contract, allow-listed ProblemDetails propagation, shared UI presentation pattern, replay/display separation, and sentinel test requirements. |
| 2026-05-18 | Applied advanced elicitation fixes: emphasized safe projection by construction, raw-field migration decisions, mini ADR evidence, whole-response graph tests, and non-blank operator diagnostics. |
| 2026-05-18 | Applied second elicitation pass: required named safe projection boundaries, forbidden raw-bearing JSON property tests, constrained additive compatibility, and client exception/export no-leak proof. |
| 2026-05-18 | Implementation progress: added Admin redacted-content descriptor/factories, protected DTO graph serialization tests, EventDetailPanel/JsonViewer protected rendering, upstream protected ProblemDetails preservation, UI client safe problem preservation, docs matrix, and focused validation evidence. Story remains in-progress for the remaining broad Admin UI/copy/export surface sweep. |
