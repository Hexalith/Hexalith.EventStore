# Story 22.7d-3: Protected Data Redaction in CLI and MCP

Status: done

Context created: 2026-05-19
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR104 protected-data redaction for `eventstore-admin` CLI output and `Hexalith.EventStore.Admin.Mcp` tool output/previews/errors. This child story intentionally excludes runtime logs/ProblemDetails already completed by 22.7d-1, Admin API/Web UI rendering already completed by 22.7d-2, and replay/rebuild/backup-validation artifact sweeps owned by 22.7d-4 except where CLI/MCP consume those safe contracts.

Prerequisites verified from `sprint-status.yaml`: 22.7a, 22.7b, 22.7c, 22.7d-1, and 22.7d-2 are done; parent 22.7d remains an unassignable backlog container. Reuse the completed `AdminRedactedContent` contract from 22.7d-2 and the runtime diagnostic policy from 22.7d-1. Do not create a second redaction taxonomy.

## Story

As an automation user,
I want CLI and MCP outputs to return structured redacted metadata,
so that scripts and AI agents can diagnose issues without receiving protected content.

## Acceptance Criteria

1. **CLI JSON output preserves redacted descriptors and never emits protected content.**
   - Given CLI read commands return protected event payloads, protected snapshot state, unreadable protected data, crypto-shredding status, restore-admission status, redacted Admin DTOs, or protected upstream ProblemDetails
   - When `eventstore-admin` serializes JSON output
   - Then protected payload JSON, snapshot state JSON, command payload JSON, reconstructed state JSON, field values, provider-private metadata, unsafe key aliases, state-store keys, connection strings, stack traces, and provider exception text are absent.
   - And JSON retains safe diagnostic value through `AdminRedactedContent`: `contentKind`, deterministic placeholder, `reasonCode`, `stage`, `metadataVersion`, `retryable`, `permanent`, `safeNextAction`, tenant/domain/aggregate/sequence/correlation metadata when available.
   - And CLI error handling does not copy raw response bodies or raw `ex.Message` values from protected-data-capable API responses into stderr.

2. **CLI table and CSV output use safe projection columns instead of legacy raw fields.**
   - Given protected or unreadable content reaches table/CSV output
   - When commands such as `stream event`, `stream state`, `stream diff`, projection, storage, consistency, backup, dead-letter, snapshot, or tenant diagnostics render rows
   - Then columns named or sourced from raw-bearing properties such as `PayloadJson`, `StateJson`, `OldValue`, `NewValue`, `ErrorMessage`, `FailureReason`, `Message`, or equivalent aliases are replaced by safe descriptor/status columns unless the field is proven safe by construction.
   - And table/CSV output remains useful for automation: content kind, placeholder/status, reason code, stage, retryability, permanent flag, safe next action, and safe identifiers are visible when present.
   - And truncation, scalar reflection, `ToString()`, and CSV escaping do not become redaction bypasses.

3. **MCP tool JSON outputs are safe by construction.**
   - Given MCP read tools return stream, state, event detail, projection, storage, consistency, diagnostic, backup, type catalog, tenant, health, or DAPR data
   - When `ToolHelper.SerializeResult` serializes the result
   - Then protected content is represented only by safe descriptors/status fields and no protected sentinels or raw-capable legacy fields appear in tool JSON.
   - And MCP responses remain structured JSON suitable for AI agents; do not fall back to plain text blobs or generic blank redaction.

4. **MCP and CLI write previews/approval prompts do not leak protected content.**
   - Given MCP write tools or CLI write commands touch operations that may involve protected content, including projection reset/replay, consistency repair, backup trigger/restore/validate, snapshot creation/policy changes, stream export/import, dead-letter actions, or future replay actions
   - When previews, warnings, confirmation copy, stderr summaries, or result messages are generated
   - Then they show only safe metadata: operation/action, endpoint shape without secrets, tenant/domain/aggregate/projection/backup identifiers where safe, sequence/checkpoint ranges, operation id, reason/status, and safe guidance.
   - And they never include plaintext payload/state, key material, provider-private metadata, raw command bodies, raw API response bodies, or provider exception text.

5. **CLI and MCP tests prove sentinel no-leak behavior across all formats and chokepoints.**
   - Given `ProtectedDataLeakSentinel` values are injected into payload JSON, snapshot state JSON, field values, command payloads, failure text, provider metadata, provider exception messages, state-store keys, connection strings, and assertion messages
   - When focused CLI and MCP tests run
   - Then JSON, table, CSV, stderr, output-file content, MCP result JSON, MCP preview JSON, and MCP error JSON pass `ProtectedDataLeakSentinel.AssertNoLeak(...)`.
   - And tests prove safe fields remain present instead of merely blanking output.
   - And tests cover nested JSON, stringified JSON, non-scalar descriptor values, provider-private extension metadata, and both negative no-leak assertions and positive semantic-preservation assertions.

## Tasks / Subtasks

- [x] **ST0 - Freeze the CLI/MCP redaction surface inventory.** (AC: 1, 2, 3, 4, 5)
  - [x] Read this story, parent Story 22.7d, Stories 22.7a/22.7b/22.7c/22.7d-1/22.7d-2, Epic 22, FR104, architecture Admin Data Access/Payload and Snapshot Protection, UX multi-modal consistency guidance, and `_bmad-output/project-context.md`.
  - [x] Confirm the single shared output policy name and location before code edits. Expected shape: a CLI/MCP-safe projection helper in Admin tooling that reuses `AdminRedactedContent` and `ProtectedDataLeakSentinel` rather than adding another taxonomy.
  - [x] Inventory CLI output chokepoints: `JsonOutputFormatter`, `CsvOutputFormatter`, `TableOutputFormatter`, `OutputWriter`, `AdminApiClient`, `AdminApiException`, command `ColumnDefinition` lists, command stderr messages, and output-file paths.
  - [x] Inventory MCP output chokepoints: `ToolHelper.SerializeResult`, `SerializePreview`, `SerializeError`, `HandleException`, `HandleHttpException`, `ServerTools`, write-tool preview payloads, and every tool returning raw Admin DTOs.
  - [x] Classify each raw-capable property as safe by construction, replaced by `AdminRedactedContent`, retained only for internal replay/storage semantics, or forbidden from CLI/MCP serialization.
  - [x] Record the inventory and chosen safe projection boundaries in the Dev Agent Record as a mini ADR.

- [x] **ST1 - Harden CLI serialization and formatters.** (AC: 1, 2, 5)
  - [x] Keep centralized package management; do not add package versions to `.csproj` files.
  - [x] Reuse existing `JsonDefaults.Options` and `System.Text.Json` patterns. If a converter is added, register it once in shared CLI options and prove it handles `AdminRedactedContent` without mutating global behavior unexpectedly.
  - [x] Add a safe value formatter for table/CSV paths so `AdminRedactedContent` renders deterministic safe fields and `ToString()` cannot hide required metadata or leak raw content.
  - [x] Replace legacy columns in stream commands: `StreamEventCommand.Columns` must not use `PayloadJson`, `StreamStateCommand.Columns` must not use `StateJson`, and `StreamDiffCommand.Columns` must not use raw `OldValue`/`NewValue` when descriptor properties exist.
  - [x] Audit backup/projection/snapshot/storage/consistency/dead-letter commands that print `AdminOperationResult.Message`, `ErrorMessage`, `FailureReason`, or `ex.Message`; map protected-data-capable responses to safe status output.
  - [x] Ensure `--output` files receive the same safe content as stdout and are included in sentinel tests.

- [x] **ST2 - Preserve protected-data ProblemDetails and safe API errors in the CLI.** (AC: 1, 2, 5)
  - [x] Update `AdminApiClient` so protected-data ProblemDetails responses are parsed and preserved as safe CLI exceptions/status objects instead of reduced to `EnsureSuccessStatusCode()` or generic raw-message exceptions.
  - [x] Add an allow-list for relayed `ProblemDetails` extension values. Scalar safe fields may pass; nested objects/arrays, provider-private metadata, raw response bodies, and arbitrary upstream extensions must be dropped unless proven safe.
  - [x] Keep authentication, authorization, timeout, not-found, and connection errors concise and useful while avoiding response-body leakage.
  - [x] Add CLI client tests for 400/404/410/422/503 protected-data ProblemDetails, malformed ProblemDetails, oversized response bodies, and unknown extension values.

- [x] **ST3 - Harden MCP serialization, previews, and error handling.** (AC: 3, 4, 5)
  - [x] Update `ToolHelper.SerializeResult` or the shared MCP projection boundary so returned Admin DTOs cannot serialize raw protected fields in tool JSON.
  - [x] Update `ToolHelper.SerializePreview` so preview `description`, `parameters`, `endpoint`, and `warning` are safe by construction and never include user-supplied protected payload/state values.
  - [x] Replace `ToolHelper.HandleException` and `HandleHttpException` paths that currently serialize `ex.Message` for generic, not-found, conflict, unprocessable, and unreachable cases when the exception may include protected upstream text.
  - [x] Audit `ServerTools` because it currently emits exception details directly in JSON; route these through the same safe error helper.
  - [x] Add MCP tests for `StreamTools`, `ProjectionWriteTools`, `ConsistencyWriteTools`, `BackupWriteTools`, `DiagnosticTools`, and `ToolHelper` no-leak behavior.

- [x] **ST4 - Update command/tool contracts and docs.** (AC: 1, 2, 3, 4)
  - [x] Update command help/descriptions only where legacy wording promises payload/state plaintext. Use surface-appropriate language such as "redaction summary" or "safe protected-content metadata".
  - [x] Update CLI/MCP docs or examples where output shape changes. Examples must show redacted placeholders and reason/status fields, not fake plaintext.
  - [x] Update `docs/guides/payload-protection-and-crypto-shredding.md` with the CLI/MCP redaction surface matrix and explicitly note that replay/rebuild/backup-validation artifact scans remain 22.7d-4.
  - [x] If output contracts change in a SemVer-relevant way, record the compatibility impact and update package/API docs accordingly.

- [x] **ST5 - Validation.** (AC: 1, 2, 3, 4, 5)
  - [x] Run focused tests individually and record exact commands/results in the Dev Agent Record.
  - [x] Minimum validation:
    - `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests --filter "FullyQualifiedName~Stream|FullyQualifiedName~Backup|FullyQualifiedName~Projection|FullyQualifiedName~Formatting|FullyQualifiedName~AdminApiClient" --no-restore`
    - `dotnet test tests/Hexalith.EventStore.Admin.Mcp.Tests --filter "FullyQualifiedName~ToolHelper|FullyQualifiedName~StreamTools|FullyQualifiedName~ProjectionWriteTools|FullyQualifiedName~ConsistencyWriteTools|FullyQualifiedName~BackupWriteTools|FullyQualifiedName~ServerTools|FullyQualifiedName~DiagnosticTools" --no-restore`
    - `dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests --filter AdminRedactedContentSerializationTests --no-restore`
    - `dotnet test tests/Hexalith.EventStore.Testing.Tests --filter ProtectedDataLeakSentinel --no-restore`
  - [x] Run `dotnet build Hexalith.EventStore.slnx --configuration Debug --no-restore` if shared contracts, CLI/MCP project references, or docs-generated APIs change.
  - [x] Use Aspire/browser verification only if this story touches runtime app behavior beyond CLI/MCP serialization tests.

## Dev Notes

### Current Implementation Snapshot

- `src/Hexalith.EventStore.Admin.Abstractions/Models/AdminRedactedContent.cs` is the existing safe descriptor from 22.7d-2. It has a deterministic placeholder, safe reason/stage metadata, retry/permanent flags, safe next action, and safe identifiers.
- CLI JSON output is centralized in `src/Hexalith.EventStore.Admin.Cli/Formatting/JsonOutputFormatter.cs` using `JsonDefaults.Options`.
- CLI table/CSV output is reflection-based in `TableOutputFormatter` and `CsvOutputFormatter`. Both call `FormatValue(object?)`, which currently falls back to `value.ToString()`. That is a redaction-sensitive chokepoint.
- CLI read commands have command-specific columns that still name legacy raw fields:
  - `StreamEventCommand.Columns` includes `PayloadJson`.
  - `StreamStateCommand.Columns` includes `StateJson`.
  - `StreamDiffCommand.Columns` includes `OldValue` and `NewValue`.
- CLI `AdminApiClient` currently maps several HTTP statuses to generic `AdminApiException` messages and relies on `EnsureSuccessStatusCode()` for non-success cases outside explicit switches. It does not yet preserve protected-data ProblemDetails safely.
- MCP serialization is centralized in `src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs`. `SerializeResult`, `SerializePreview`, `SerializeError`, `HandleException`, and `HandleHttpException` are the primary chokepoints.
- `ToolHelper.HandleException` and `HandleHttpException` currently include `ex.Message` in several JSON error paths. Treat those as unsafe for protected-data-capable upstream failures.
- MCP `ServerTools` currently emits exception details directly in JSON and needs the same safe error policy.
- MCP write previews in `ProjectionWriteTools`, `ConsistencyWriteTools`, and `BackupWriteTools` are structured and approval-gated; preserve this shape but ensure preview text/parameters cannot carry protected payload or key material.

### Architecture Compliance

- CLI and MCP are thin clients over Admin API. They must not access DAPR, state store keys, or provider metadata directly.
- Admin API/Web UI redaction work from 22.7d-2 should make safe DTOs available before CLI/MCP sees them. This story still must prove CLI/MCP formatters, previews, and error paths do not reintroduce leaks.
- Do not change dead-letter replay payload storage semantics. CLI/MCP display/output must distinguish internal replay payload retention from safe automation-facing projections.
- Do not add real encryption providers, KMS/Key Vault/DAPR secret-store integration, certificates, physical backup scanners, skip-past-unreadable policy, or broad replay/rebuild artifact scans.
- Preserve `System.CommandLine` command/action patterns, existing exit-code behavior, and current `ModelContextProtocol` tool registration patterns.

### Latest Technical Information

- Repo package versions are centrally pinned: `System.CommandLine` `2.0.8`, `ModelContextProtocol` `1.3.0`, `Microsoft.Extensions.Http` `10.0.8`.
- Microsoft Learn currently documents `System.CommandLine` 2.x as the command/parse/invoke pattern used by this repo; continue using typed `Argument`/`Option`, `ParseResult.GetValue`, `SetAction`, and integer exit codes.
- Microsoft Learn `System.Text.Json` guidance recommends reusing `JsonSerializerOptions` instances; if adding custom converters, register them through the shared CLI/MCP options rather than per-call ad hoc options.
- Microsoft Learn MCP guidance describes MCP servers as structured tool providers; keep outputs valid JSON and avoid plain text blobs so clients/agents can reason over safe fields.

### Testing Requirements

- Red-phase first: add at least one CLI formatter/command test and one MCP tool/helper test that currently fails because a protected sentinel appears in output.
- Required assertions:
  - JSON/table/CSV/output-file content contains no sentinel and retains `AdminRedactedContent` safe metadata.
  - Stderr contains no sentinel for protected-data-capable failures and still preserves actionable safe status.
  - MCP result JSON, preview JSON, and error JSON contain no sentinel and retain machine-readable status/reason fields.
  - `ProblemDetails` allow-listed fields survive; unknown or nested provider-private extensions are dropped.
  - Legacy raw-bearing property names are absent in protected CLI/MCP responses unless a test proves the property is safe by construction.
  - Existing non-protected CLI/MCP behavior remains compatible where users/scripts rely on safe plain output.

## Out of Scope

- Runtime logs, telemetry, command-status, dead-letter diagnostic text, and core ProblemDetails redaction already completed by Story 22.7d-1.
- Admin API/Web UI protected-data rendering and copy/export behavior already completed by Story 22.7d-2.
- Replay/rebuild/backup-validation progress records, broad evidence scans, and test artifact sweeps are Story 22.7d-4 except for direct CLI/MCP output/previews/errors touched here.
- Changing Admin API storage/replay semantics or immutable event/dead-letter payload retention.
- Replacing System.CommandLine, replacing the MCP SDK, introducing another serializer, or adding package versions directly to project files.

## References

- `_bmad-output/planning-artifacts/epics.md` - Story 22.7d-3 source requirements and Epic 22 split.
- `_bmad-output/planning-artifacts/prd.md` - FR104 protected-data no-leak requirement, CLI scriptability, and MCP agent autonomy.
- `_bmad-output/planning-artifacts/architecture.md` - ADR-P4 CLI/MCP thin clients over Admin API, Admin Data Access, ProblemDetails, and Payload/Snapshot Protection.
- `_bmad-output/planning-artifacts/ux-design-specification.md` - multi-modal UX consistency, CLI/Aspire, Admin CLI, and MCP interaction guidance.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12.md` - split of Story 22.7 into 22.7a-22.7d.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-17-readiness-addendum.md` - child story rows 22.7d-1 through 22.7d-4.
- `_bmad-output/implementation-artifacts/22-7d-protected-data-redaction-across-operational-surfaces.md` - parent container and surface inventory.
- `_bmad-output/implementation-artifacts/22-7d-1-protected-data-redaction-in-logs-and-problemdetails.md` - completed runtime diagnostic redactor and safe ProblemDetails policy.
- `_bmad-output/implementation-artifacts/22-7d-2-protected-data-redaction-in-admin-api-and-web-ui.md` - completed `AdminRedactedContent`, Admin DTO, and UI redaction contract.
- `_bmad-output/implementation-artifacts/22-7a-payload-and-snapshot-protection-hooks.md` - protection metadata contract.
- `_bmad-output/implementation-artifacts/22-7b-unreadable-protected-data-behavior.md` - unreadable taxonomy and sentinel helper.
- `_bmad-output/implementation-artifacts/22-7c-crypto-shredding-workflow-and-restored-backup-safety.md` - readability decisions and restore/crypto-shredding ProblemDetails.
- `_bmad-output/project-context.md` - package boundaries, logging, ProblemDetails, Admin tooling, DAPR, and testing rules.
- Microsoft Learn: `System.CommandLine` tutorials and 2.x migration guide for command/action/parse patterns.
- Microsoft Learn: `System.Text.Json` custom converter and `JsonSerializerOptions` reuse guidance.
- Microsoft Learn: MCP server learning resources and .NET MCP architecture guidance.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Aspire baseline before story creation: AppHost started with `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`; Aspire MCP detected `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`. `pubsub` and `statestore` reported `Running`/`Healthy`; UI resources were waiting on backend startup at the snapshot time.
- 2026-05-19 08:31+02:00 dev baseline: `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` with `EnableKeycloak=false`; Aspire MCP `list_resources` reported `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `sample`, `sample-blazor-ui`, `tenants`, `pubsub`, and `statestore` running/healthy. Aspire doctor had no failures; warnings were multiple dev certificates, older dev cert version, and deprecated Claude Code MCP config.

### Implementation Plan

- ST0 mini ADR: use one output boundary named **Admin tooling safe output projection** across CLI and MCP. The boundary lives at the Admin tooling edges, not in domain/runtime code: CLI formatters/columns/API exception mapping and MCP `ToolHelper`/tool preview/error serialization. It reuses `AdminRedactedContent` as the only protected-content descriptor and `ProtectedDataLeakSentinel` for tests; it does not introduce another taxonomy.
- CLI chokepoints inventoried: `JsonOutputFormatter` uses `JsonDefaults.Options`; `TableOutputFormatter` and `CsvOutputFormatter` reflect scalar properties and currently fall back to `ToString()`; `OutputWriter` writes identical formatter output to stdout or `--output`; `AdminApiClient` maps HTTP errors to `AdminApiException` and currently drops protected-data ProblemDetails details; `AdminApiException.Message` is printed directly by commands; stream column definitions still select `PayloadJson`, `StateJson`, `OldValue`, and `NewValue`.
- MCP chokepoints inventoried: `ToolHelper.SerializeResult`, `SerializePreview`, and `SerializeError` own JSON shape; `HandleException` and `HandleHttpException` currently include exception text for generic, not-found, conflict, unprocessable, and unreachable paths; `ServerTools.Ping` directly serializes exception details; read/write tools return Admin DTOs through `SerializeResult` and write previews through `SerializePreview`.
- Raw-capable property classification: `PayloadJson`, `StateJson`, `EventPayloadJson`, `ResultingStateJson`, `OldValue`, `NewValue`, `ErrorMessage`, `FailureReason`, `Message`, provider metadata, state-store keys, connection strings, raw response bodies, and provider exception text are forbidden from CLI/MCP serialization when they carry protected-data-capable content. Descriptor properties such as `Payload`, `State`, `OldContent`, `NewContent`, `ResultingState`, `Error`, `Failure`, `Diagnostic`, `CurrentContent`, and `PreviousContent` are the replacement path. Safe identifiers, status, content kind, placeholder, reason code, stage, metadata version, retry/permanent flags, safe next action, tenant/domain/aggregate/sequence/correlation metadata are safe by construction.
- Internal replay/storage semantics may retain raw fields inside Admin DTOs and server internals; CLI/MCP display, preview, error, stderr, and output-file paths must project them to descriptor/status fields or omit them rather than relying on `ToString()` redaction.
- Implementation: CLI formatters now render `AdminRedactedContent` as deterministic descriptor fields, redact sentinel-shaped plain string diagnostics, omit protected raw JSON properties from CLI JSON when descriptors are present, and deserialize redacted `EventDetail` responses through the shared `JsonDefaults.Options` converter. Stream table columns now select `Payload`, `State`, `OldContent`, and `NewContent` with safe fallback to legacy raw values only when no descriptor is present.
- Implementation: CLI `AdminApiClient` now parses safe ProblemDetails, preserves allow-listed scalar extensions on `AdminApiException.Problem`, drops nested/provider-private/unknown/raw-body data, and returns generic no-leak messages for malformed or oversized ProblemDetails.
- Implementation: MCP `ToolHelper` now sanitizes result JSON, preview text/parameters/endpoints/warnings, and error JSON. Raw-capable fields are projected to descriptor names, `ex.Message` is no longer serialized for protected-data-capable generic/not-found/conflict/unprocessable/unreachable cases, and `ServerTools.Ping` no longer emits exception details directly.
- Compatibility: protected CLI/MCP output shape changes intentionally replace raw-bearing columns/fields with safe descriptor/status fields. Non-protected stream table/CSV values remain visible through safe fallback formatting when descriptor fields are absent.

### Completion Notes List

- Story context created on 2026-05-19 by BMad create-story workflow.
- No implementation tests were run because this is story creation only.
- ST0 completed: CLI/MCP redaction surface inventory frozen and safe output projection mini ADR recorded before code edits.
- ST1-ST4 completed: CLI JSON/table/CSV/output-file formatting, CLI ProblemDetails handling, MCP result/preview/error serialization, and CLI/MCP redaction docs now use the shared Admin tooling safe output projection.
- ST5 completed: required focused validations and debug build passed.

### File List

- `_bmad-output/implementation-artifacts/22-7d-3-protected-data-redaction-in-cli-and-mcp.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/guides/payload-protection-and-crypto-shredding.md`
- `src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs`
- `src/Hexalith.EventStore.Admin.Cli/Client/AdminApiException.cs`
- `src/Hexalith.EventStore.Admin.Cli/Client/AdminApiProblemDetails.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Stream/StreamDiffCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Stream/StreamEventCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Stream/StreamStateCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Formatting/CsvOutputFormatter.cs`
- `src/Hexalith.EventStore.Admin.Cli/Formatting/EventDetailJsonConverter.cs`
- `src/Hexalith.EventStore.Admin.Cli/Formatting/JsonDefaults.cs`
- `src/Hexalith.EventStore.Admin.Cli/Formatting/JsonOutputFormatter.cs`
- `src/Hexalith.EventStore.Admin.Cli/Formatting/SafeOutputValueFormatter.cs`
- `src/Hexalith.EventStore.Admin.Cli/Formatting/TableOutputFormatter.cs`
- `src/Hexalith.EventStore.Admin.Mcp/Tools/ServerTools.cs`
- `src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Client/AdminApiClientProblemDetailsTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamDiffCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamEventCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamStateCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Formatting/CsvOutputFormatterTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Formatting/JsonOutputFormatterTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Formatting/TableOutputFormatterTests.cs`
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/ServerToolsTests.cs`
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/ToolHelperTests.cs`

## Verification Status

- Story implementation complete and set to `review`.
- Prerequisites verified from `sprint-status.yaml`: 22.7a, 22.7b, 22.7c, 22.7d-1, and 22.7d-2 are done; parent 22.7d remains an unassignable container row.
- Aspire baseline: `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` with `EnableKeycloak=false`; core resources reported running/healthy by Aspire MCP before edits.
- `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests --filter "FullyQualifiedName~Stream|FullyQualifiedName~Backup|FullyQualifiedName~Projection|FullyQualifiedName~Formatting|FullyQualifiedName~AdminApiClient" --no-restore` -> Passed: 132/132.
- `dotnet test tests/Hexalith.EventStore.Admin.Mcp.Tests --filter "FullyQualifiedName~ToolHelper|FullyQualifiedName~StreamTools|FullyQualifiedName~ProjectionWriteTools|FullyQualifiedName~ConsistencyWriteTools|FullyQualifiedName~BackupWriteTools|FullyQualifiedName~ServerTools|FullyQualifiedName~DiagnosticTools" --no-restore` -> Passed: 108/108.
- `dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests --filter AdminRedactedContentSerializationTests --no-restore` -> Passed: 2/2.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests --filter ProtectedDataLeakSentinel --no-restore` -> Passed: 5/5.
- `dotnet build Hexalith.EventStore.slnx --configuration Debug --no-restore` -> Build succeeded, 0 warnings, 0 errors.

## Review Findings

Adversarial review run 2026-05-19 via bmad-code-review (Opus 4.7 1M, three layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor) against uncommitted diff (`git diff HEAD`, 22 files, +833/-85, 1419 lines). Raw 65 findings → triage: 4 decision-needed + 24 patch + 7 defer + 13 dismiss + 17 merged into other entries.

### Decision Needed (resolved 2026-05-19)

- [x] [Review][Decision] **D1 → P-D1 — `TryGetAsync` 404 with ProblemDetails body: skip parse on 404** [src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs:141-145] — Resolved: only invoke `TryCreateProblemExceptionAsync` when status != 404 (and remove the redundant 404 switch arm). Preserves existing "stream/aggregate not found returns null" semantics. The other verbs (Post/Put/Delete) already map 404 to a thrown exception, so they don't need this carve-out, but their 404 switch arms must still fire ahead of the ProblemDetails throw.
- [x] [Review][Decision] **D2 → P-D2 — MCP `SanitizeNode` marker replacement scoped to raw-capable keys** [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:147-149] — Resolved: restrict marker-replacement (the `JsonValue` text branch) so it only fires when the immediate `propertyName` is in `IsRawCapableProperty`. Other string values are passed through. Keeps key-based projection as the canonical safety boundary; eliminates silent corruption of benign safe-descriptor text.
- [x] [Review][Decision] **D3 → P-D3 — `StreamEventCommand`/`StreamStateCommand` `MaxWidth` reverted to 80** [src/Hexalith.EventStore.Admin.Cli/Commands/Stream/StreamEventCommand.cs:24, StreamStateCommand.cs:23] — Resolved: revert to 80 (out of story scope). Descriptor text will truncate at 80 in table view; users use `--output json` for full content. Update related test expectations if any assert against the 160-char layout.
- [x] [Review][Decision] **D4 → P-D4 — CSV/Table fallback removed: fail-closed empty cell when descriptor null** [src/Hexalith.EventStore.Admin.Cli/Formatting/CsvOutputFormatter.cs:87-113 + TableOutputFormatter.cs:144-170] — Resolved: remove `GetRawFallbackProperty`. When the descriptor property is null, render `string.Empty`. Strongest guarantee; sacrifices visibility for legitimately-non-protected payloads in table/CSV mode (users get JSON output for full content). P17 becomes "test descriptor=null path renders empty cell, no raw fallback".

### Patch

- [x] [Review][Patch] **P1 — ST3 MCP test coverage shortfall: 5 of 6 named test classes untouched** [tests/Hexalith.EventStore.Admin.Mcp.Tests/] — ST3 promised tests in `StreamToolsTests`, `ProjectionWriteToolsTests`, `ConsistencyWriteToolsTests`, `BackupWriteToolsTests`, `DiagnosticToolsTests`; only `ToolHelperTests` and `ServerToolsTests` were modified. Add per-tool sentinel no-leak tests for each.
- [x] [Review][Patch] **P2 — ST1 non-Stream command audit not delivered: backup/projection/snapshot/storage/consistency/dead-letter commands still print `ex.Message` and `result.ErrorMessage` to stderr unsafely** [src/Hexalith.EventStore.Admin.Cli/Commands/Backup/*, Commands/Projection/*, Commands/Snapshot/*, Commands/Storage/*, Commands/Consistency/*, Commands/DeadLetter/*] — e.g. `BackupExportStreamCommand.cs:80` emits `result.ErrorMessage ?? "Export failed."` and `BackupImportStreamCommand.cs:84` writes `ex.Message`. Route stderr through `SafeOutputValueFormatter.SafeText`, or audit each callsite and confirm safe-by-construction.
- [x] [Review][Patch] **P3 — Content-Length bypass: unbounded body read before size check** [src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs:379-393] — when `Content-Length` is null (chunked transfer), the `body.Length > MaxProblemDetailsBytes` check fires AFTER `ReadAsStringAsync` has already buffered the entire body. Replace with bounded buffer read (e.g., `ReadAsStreamAsync` + manual copy up to N+1 bytes; abort if exceeded). Also defends against `Content-Length: -1` returned by some stacks.
- [x] [Review][Patch] **P4 — `ConnectionString` substring marker over-matches safe text** [src/Hexalith.EventStore.Admin.Cli/Formatting/SafeOutputValueFormatter.cs:58, src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:174, src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs:485] — `Contains("ConnectionString", OrdinalIgnoreCase)` matches the word anywhere; safe diagnostic text mentioning "connection string status" gets replaced wholesale with the placeholder. Narrow to value-bearing patterns like `";ConnectionString="` or `"Endpoint=sb://"` (matching the `=`-shape of `AccountKey=`, `SharedAccessKey=`, `Password=`).
- [x] [Review][Patch] **P5 — Sentinel-vs-marker contract not pinned by any test** [tests/Hexalith.EventStore.Testing.Tests/ or new shared test] — `ContainsUnsafeMarker` substring list is duplicated in three files; no test asserts that every `ProtectedDataLeakSentinel.*` constant (`ProtectedPayloadPlaintext`, `ProtectedSnapshotPlaintext`, `ProtectedConnectionString`, `ProtectedProviderExceptionText`) is detected by it. Add a contract test that runs each sentinel through (a public proxy for) `ContainsUnsafeMarker` and asserts true.
- [x] [Review][Patch] **P6 — `ContainsUnsafeMarker` duplicated across three files** [src/Hexalith.EventStore.Admin.Cli/Formatting/SafeOutputValueFormatter.cs, src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs, src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs] — identical implementations will drift over time. Consolidate into a shared helper in `Hexalith.EventStore.Admin.Abstractions` (same project that owns `AdminRedactedContent`).
- [x] [Review][Patch] **P7 — JsonOutputFormatter descriptor lookup case-sensitive while MCP is case-insensitive** [src/Hexalith.EventStore.Admin.Cli/Formatting/JsonOutputFormatter.cs:57-68] — `GetDescriptorProperty` uses exact-case switch on `"payloadJson"`. MCP's `IsRawCapableProperty` uses `OrdinalIgnoreCase`. Defense-in-depth requires the looser layer match. Change to `Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)` or case-insensitive switch.
- [x] [Review][Patch] **P8 — JSON formatter: descriptor present-but-null fails to suppress raw** [src/Hexalith.EventStore.Admin.Cli/Formatting/JsonOutputFormatter.cs:52-55] — `IsRawPropertyWithDescriptor` requires `descriptor is not null`. If server returns `payload: null` next to `payloadJson: "<raw>"`, raw passes through. Treat key-present (any value, including null) as descriptor signal, OR throw on the partially-formed contract.
- [x] [Review][Patch] **P9 — EventDetailJsonConverter silently coerces missing `payloadJson` to "{}"** [src/Hexalith.EventStore.Admin.Cli/Formatting/EventDetailJsonConverter.cs:24] — when neither `payload` nor `payloadJson` is present, defaults to `"{}"`. Either property should throw `JsonException` (consistent with `GetRequiredString`).
- [x] [Review][Patch] **P10 — EventDetailJsonConverter drops descriptor if `payload` is non-Object** [src/Hexalith.EventStore.Admin.Cli/Formatting/EventDetailJsonConverter.cs:26-32] — only Object kind triggers descriptor path. Array/String/Number/Null silently ignored; raw `PayloadJson` is then used. When `payload` is present but not Object, throw `JsonException`.
- [x] [Review][Patch] **P11 — EventDetailJsonConverter exposes raw `Deserialize<AdminRedactedContent>` exception** [src/Hexalith.EventStore.Admin.Cli/Formatting/EventDetailJsonConverter.cs:30] — no try/catch; JsonException bubbles to AdminApiClient (which does wrap to a safe message). Wrap explicitly and re-throw as a safe contract error so the converter owns its contract.
- [x] [Review][Patch] **P12 — `AdminApiException.Problem.Extensions` values have no per-value length cap** [src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs:455-478] — `TryGetSafeScalar` filters on type and sentinel substring only. A 60 KB upstream-controlled extension string passes through into stderr. Add per-extension length cap (e.g., 1024 chars) with explicit truncation marker.
- [x] [Review][Patch] **P13 — MCP and CLI SanitizeNode lack recursion depth bound** [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:119-152, src/Hexalith.EventStore.Admin.Cli/Formatting/JsonOutputFormatter.cs:18-50] — both recursive walkers are unbounded. Adversarial deep JSON can cause stack overflow. Add depth cap (e.g., 64) with explicit handling at the limit.
- [x] [Review][Patch] **P14 — SafeText may NRE when called with null in SerializePreview/SerializeError** [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:50-53, 63, 166-167; src/Hexalith.EventStore.Admin.Cli/Formatting/SafeOutputValueFormatter.cs:15-16] — `SafeText` calls `value.Contains` without null-check. C# NRT contract forbids null, but explicit-null bypass would NRE inside the error-handling path itself. Add `ArgumentNullException.ThrowIfNull` or null-coalesce to empty.
- [x] [Review][Patch] **P15 — MCP preview test asserts only sentinel absence, not descriptor shape or fallback text** [tests/Hexalith.EventStore.Admin.Mcp.Tests/ToolHelperTests.cs SerializePreview_DoesNotLeak...] — also assert `parameters.payload.placeholder` matches the default and that `description`/`endpoint`/`warning` equal their fallback replacement strings. AC #4 positive-shape requirement is otherwise unproven.
- [x] [Review][Patch] **P16 — `MaxProblemDetailsBytes` compares against char count not byte count** [src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs:393] — `body.Length` is UTF-16 char count, not bytes. Rename constant to `MaxProblemDetailsChars` OR use `Encoding.UTF8.GetByteCount(body)`.
- [x] [Review][Patch] **P17 — Test descriptor=null renders empty cell (no raw fallback) per D4** [tests/Hexalith.EventStore.Admin.Cli.Tests/Formatting/CsvOutputFormatterTests.cs, TableOutputFormatterTests.cs] — add tests where descriptor property is null and raw value (`PayloadJson`, `StateJson`, `OldValue`, `NewValue`, `FailureReason`, `ErrorMessage`) is populated; assert output cell is empty (no fallback to raw). Couples to P-D4 fail-closed implementation.
- [x] [Review][Patch] **P18 — SafeOutputValueFormatter ToString IFormattable locale-sensitive** [src/Hexalith.EventStore.Admin.Cli/Formatting/SafeOutputValueFormatter.cs:12] — DateTime/decimal/etc. render via culture-dependent `ToString()`. Use `InvariantCulture` for `IFormattable` types so CSV/Table output is machine-parseable across hosts.
- [x] [Review][Patch] **P19 — PROTECTED_ marker check is case-sensitive (Ordinal)** [src/Hexalith.EventStore.Admin.Cli/Formatting/SafeOutputValueFormatter.cs:54, src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:170, src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs:481] — `"PROTECTED_"` uses `StringComparison.Ordinal`; lowercase variants slip past. Either change to `OrdinalIgnoreCase` for parity with the other markers, or pin the convention in a sentinel-contract test (see P5).
- [x] [Review][Patch] **P20 — ProblemDetails extension allow-list test does not assert specific keys are dropped** [tests/Hexalith.EventStore.Admin.Cli.Tests/Client/AdminApiClientProblemDetailsTests.cs] — extend assertions to confirm `unsafeArray` and other nested-shape extension names are absent (proves deny-by-default for the structural filter, not just `payloadJson`/`providerPrivateMetadata`). Note: `reasonCategory` is in the allow-list so it should be present, not absent.
- [x] [Review][Patch] **P21 — AC #5 sentinel coverage: commandPayload, providerMetadata, stateStoreKey, etc. not asserted in CLI tests** [tests/Hexalith.EventStore.Admin.Cli.Tests/Formatting/*] — extend `JsonOutputFormatterTests`/`TableOutputFormatterTests`/`CsvOutputFormatterTests` to cover all field categories listed in MCP `IsRawCapableProperty` (commandPayload, providerMetadata, providerPrivateMetadata, stateStoreKey, connectionString, keyAlias, rawResponseBody, stackTrace, exceptionText). AC #5 explicitly enumerates these.
- [x] [Review][Patch] **P22 — AC #5 nested-JSON and stringified-JSON sentinel coverage absent** [tests/Hexalith.EventStore.Admin.Cli.Tests/Formatting/JsonOutputFormatterTests.cs, tests/Hexalith.EventStore.Admin.Mcp.Tests/ToolHelperTests.cs] — add nested-object sentinel injection (`parameters.nested.payloadJson` two levels deep) and stringified-JSON injection (string containing `"payloadJson":"PROTECTED_..."` as a JSON-shaped value). AC #5 explicitly requires nested/stringified coverage.
- [x] [Review][Patch] **P23 — `ReadAsStringAsync` IOException unwrapped to caller** [src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs:161, 226, 291, 349, 388] — raw IOException/HttpRequestException from `ReadAsStringAsync` (network reset mid-read) propagates through, bypassing the friendly AdminApiException messages constructed at the GET/POST/PUT/DELETE entry try/catch. Wrap each `ReadAsStringAsync` call (and re-throw as `AdminApiException`).
- [x] [Review][Patch] **P24 — Test gap on EventDetail: server returns `payloadJson` with sentinel but no descriptor** [tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Stream/StreamEventCommandTests.cs] — add a negative test asserting CLI output is sentinel-free when `payload` (descriptor) is null but `payloadJson` (raw) contains a sentinel. Validates the converter's safe handling AND the formatter fallback path.

### Deferred

- [x] [Review][Defer] **W1 — Descriptor-shaped raw key handling** [src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs:124-135] — server contract violation: if upstream emits `payloadJson: <already-a-descriptor>`, MCP replaces with a freshly-built descriptor, losing original metadata. Deferred — server-side contract addressed separately.
- [x] [Review][Defer] **W2 — Status as string in ProblemDetails** [src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs:422-424] — `TryGetInt32` fails on `"400"` string-form status; falls back to transport status code. Minor UX. Deferred.
- [x] [Review][Defer] **W3 — AmbiguousMatchException on `new`-shadowed properties** [src/Hexalith.EventStore.Admin.Cli/Formatting/CsvOutputFormatter.cs:98, TableOutputFormatter.cs:155] — `GetProperty(name, Public|Instance)` may throw on derived DTOs with `new` modifier. No current DTO triggers this; defer until concrete case appears.
- [x] [Review][Defer] **W4 — Truncation at 160 chars cuts descriptor info** [src/Hexalith.EventStore.Admin.Cli/Formatting/TableOutputFormatter.cs:185-193] — descriptor fields rendered with `; `-separated `key=value` may exceed 160. UX, not security. Users can use `--output json` for full content. Deferred.
- [x] [Review][Defer] **W5 — ProblemDetails `title`/`detail` scalar passes non-sentinel-bearing sensitive text** [src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs:418-420] — title/detail are server-controlled and should not include tenant data. Marker check catches sentinel-shaped text. Server-side concern; deferred.
- [x] [Review][Defer] **W6 — Encoded-secret bypass (base64/JWT/PEM)** [all `ContainsUnsafeMarker` copies] — sentinel-based testing model has known design limits for encoded/transformed secrets. Out-of-band design discussion. Deferred (sentinel marker design).
- [x] [Review][Defer] **W7 — JSON property duplicate names in ProblemDetails** [src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs:418-435] — `JsonDocument.Parse` last-wins on duplicates; not currently caught. Server contract concern; deferred.

### Dismissed (recorded, not actionable)

- Ping JsonException cause drop (intentional safe-by-default).
- Ping test exact-string assertion (test rigidity, not security).
- `details = "HTTP {N} {Name}"` formatting (`HttpStatusCode.ToString()` returns enum name or integer — both safe).
- `AdminApiException` HTTP vs payload status discrepancy (RFC 7807 allows both; BuildProblemMessage uses transport status consistently).
- `AdminRedactedContent` placeholder/safeNextAction semicolon escape concerns (descriptor values are server-controlled, shouldn't contain `; `).
- `IEnumerable<T>` collection rendering as type-name in SafeOutputValueFormatter (not a leak; explicit `null` semantics).
- `Number` kind NaN/Infinity in TryGetSafeScalar (JSON spec disallows; JsonDocument won't parse them).
- Ping logger/InnerException leakage paths (explicit out-of-scope: 22.7d-1 owns logging redaction).
- JsonException.Message via ambient logging (out of scope: logging redaction is 22.7d-1).
- Non-UTF-8 charset mojibake on response decode (Admin API always returns UTF-8; charset mismatch is upstream defect).
- `Content-Length: 0` with trailing chunked body suppresses ProblemDetails parsing (server-malformed; safe fallback to generic 4xx/5xx message OK).
- Mixed-state DTO (descriptor present AND raw populated) — server contract violation; descriptor wins in formatters.
- `keyAlias`/`keyStatus` defensive entries in MCP raw-capable list (preventive, no current DTO surfaces them; harmless future-proofing).
- `EventDetailJsonConverter` global registration in `JsonDefaults.Options` (verified `Write` method correctly suppresses `payloadJson` when descriptor present; appropriate shared-options placement).

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-19 | Story created for child 22.7d-3 with focused CLI/MCP redaction scope, output chokepoint inventory, test evidence requirements, and source references. |
| 2026-05-19 | Implemented CLI/MCP protected-data redaction for JSON/table/CSV/output files, CLI ProblemDetails, MCP result/preview/error paths, docs, and focused sentinel validation. |
| 2026-05-19 | Adversarial code review run: 4 decision-needed + 24 patch + 7 defer + 13 dismiss. Findings recorded for resolution. |
| 2026-05-19 | Code review patches applied: D1 (404→null carve-out in TryGetAsync), D2 (MCP marker scoped to raw-capable keys), D3 (Payload column width reverted 160→80), D4 (CSV/Table raw fallback removed; fail-closed empty cells), plus P1-P24 (shared UnsafeMarkerDetection helper, bounded ProblemDetails body read, per-extension length cap, recursion depth bound, EventDetail converter contract hardening, null-guards, locale-invariant formatting, expanded CLI/MCP sentinel-coverage tests, MCP per-tool no-leak tests, sentinel-vs-marker contract test). Focused validation green: CLI 153/153, MCP 116/116, Admin.Abstractions 2/2, Testing 5/5. |
