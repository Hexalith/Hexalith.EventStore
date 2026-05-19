# Story 22.7d-3: Protected Data Redaction in CLI and MCP

Status: ready-for-dev

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

- [ ] **ST0 - Freeze the CLI/MCP redaction surface inventory.** (AC: 1, 2, 3, 4, 5)
  - [ ] Read this story, parent Story 22.7d, Stories 22.7a/22.7b/22.7c/22.7d-1/22.7d-2, Epic 22, FR104, architecture Admin Data Access/Payload and Snapshot Protection, UX multi-modal consistency guidance, and `_bmad-output/project-context.md`.
  - [ ] Confirm the single shared output policy name and location before code edits. Expected shape: a CLI/MCP-safe projection helper in Admin tooling that reuses `AdminRedactedContent` and `ProtectedDataLeakSentinel` rather than adding another taxonomy.
  - [ ] Inventory CLI output chokepoints: `JsonOutputFormatter`, `CsvOutputFormatter`, `TableOutputFormatter`, `OutputWriter`, `AdminApiClient`, `AdminApiException`, command `ColumnDefinition` lists, command stderr messages, and output-file paths.
  - [ ] Inventory MCP output chokepoints: `ToolHelper.SerializeResult`, `SerializePreview`, `SerializeError`, `HandleException`, `HandleHttpException`, `ServerTools`, write-tool preview payloads, and every tool returning raw Admin DTOs.
  - [ ] Classify each raw-capable property as safe by construction, replaced by `AdminRedactedContent`, retained only for internal replay/storage semantics, or forbidden from CLI/MCP serialization.
  - [ ] Record the inventory and chosen safe projection boundaries in the Dev Agent Record as a mini ADR.

- [ ] **ST1 - Harden CLI serialization and formatters.** (AC: 1, 2, 5)
  - [ ] Keep centralized package management; do not add package versions to `.csproj` files.
  - [ ] Reuse existing `JsonDefaults.Options` and `System.Text.Json` patterns. If a converter is added, register it once in shared CLI options and prove it handles `AdminRedactedContent` without mutating global behavior unexpectedly.
  - [ ] Add a safe value formatter for table/CSV paths so `AdminRedactedContent` renders deterministic safe fields and `ToString()` cannot hide required metadata or leak raw content.
  - [ ] Replace legacy columns in stream commands: `StreamEventCommand.Columns` must not use `PayloadJson`, `StreamStateCommand.Columns` must not use `StateJson`, and `StreamDiffCommand.Columns` must not use raw `OldValue`/`NewValue` when descriptor properties exist.
  - [ ] Audit backup/projection/snapshot/storage/consistency/dead-letter commands that print `AdminOperationResult.Message`, `ErrorMessage`, `FailureReason`, or `ex.Message`; map protected-data-capable responses to safe status output.
  - [ ] Ensure `--output` files receive the same safe content as stdout and are included in sentinel tests.

- [ ] **ST2 - Preserve protected-data ProblemDetails and safe API errors in the CLI.** (AC: 1, 2, 5)
  - [ ] Update `AdminApiClient` so protected-data ProblemDetails responses are parsed and preserved as safe CLI exceptions/status objects instead of reduced to `EnsureSuccessStatusCode()` or generic raw-message exceptions.
  - [ ] Add an allow-list for relayed `ProblemDetails` extension values. Scalar safe fields may pass; nested objects/arrays, provider-private metadata, raw response bodies, and arbitrary upstream extensions must be dropped unless proven safe.
  - [ ] Keep authentication, authorization, timeout, not-found, and connection errors concise and useful while avoiding response-body leakage.
  - [ ] Add CLI client tests for 400/404/410/422/503 protected-data ProblemDetails, malformed ProblemDetails, oversized response bodies, and unknown extension values.

- [ ] **ST3 - Harden MCP serialization, previews, and error handling.** (AC: 3, 4, 5)
  - [ ] Update `ToolHelper.SerializeResult` or the shared MCP projection boundary so returned Admin DTOs cannot serialize raw protected fields in tool JSON.
  - [ ] Update `ToolHelper.SerializePreview` so preview `description`, `parameters`, `endpoint`, and `warning` are safe by construction and never include user-supplied protected payload/state values.
  - [ ] Replace `ToolHelper.HandleException` and `HandleHttpException` paths that currently serialize `ex.Message` for generic, not-found, conflict, unprocessable, and unreachable cases when the exception may include protected upstream text.
  - [ ] Audit `ServerTools` because it currently emits exception details directly in JSON; route these through the same safe error helper.
  - [ ] Add MCP tests for `StreamTools`, `ProjectionWriteTools`, `ConsistencyWriteTools`, `BackupWriteTools`, `DiagnosticTools`, and `ToolHelper` no-leak behavior.

- [ ] **ST4 - Update command/tool contracts and docs.** (AC: 1, 2, 3, 4)
  - [ ] Update command help/descriptions only where legacy wording promises payload/state plaintext. Use surface-appropriate language such as "redaction summary" or "safe protected-content metadata".
  - [ ] Update CLI/MCP docs or examples where output shape changes. Examples must show redacted placeholders and reason/status fields, not fake plaintext.
  - [ ] Update `docs/guides/payload-protection-and-crypto-shredding.md` with the CLI/MCP redaction surface matrix and explicitly note that replay/rebuild/backup-validation artifact scans remain 22.7d-4.
  - [ ] If output contracts change in a SemVer-relevant way, record the compatibility impact and update package/API docs accordingly.

- [ ] **ST5 - Validation.** (AC: 1, 2, 3, 4, 5)
  - [ ] Run focused tests individually and record exact commands/results in the Dev Agent Record.
  - [ ] Minimum validation:
    - `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests --filter "FullyQualifiedName~Stream|FullyQualifiedName~Backup|FullyQualifiedName~Projection|FullyQualifiedName~Formatting|FullyQualifiedName~AdminApiClient" --no-restore`
    - `dotnet test tests/Hexalith.EventStore.Admin.Mcp.Tests --filter "FullyQualifiedName~ToolHelper|FullyQualifiedName~StreamTools|FullyQualifiedName~ProjectionWriteTools|FullyQualifiedName~ConsistencyWriteTools|FullyQualifiedName~BackupWriteTools|FullyQualifiedName~ServerTools|FullyQualifiedName~DiagnosticTools" --no-restore`
    - `dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests --filter AdminRedactedContentSerializationTests --no-restore`
    - `dotnet test tests/Hexalith.EventStore.Testing.Tests --filter ProtectedDataLeakSentinel --no-restore`
  - [ ] Run `dotnet build Hexalith.EventStore.slnx --configuration Debug --no-restore` if shared contracts, CLI/MCP project references, or docs-generated APIs change.
  - [ ] Use Aspire/browser verification only if this story touches runtime app behavior beyond CLI/MCP serialization tests.

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

### Completion Notes List

- Story context created on 2026-05-19 by BMad create-story workflow.
- No implementation tests were run because this is story creation only.

### File List

- `_bmad-output/implementation-artifacts/22-7d-3-protected-data-redaction-in-cli-and-mcp.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Verification Status

- Story context created and set to `ready-for-dev`.
- Prerequisites verified from `sprint-status.yaml`: 22.7a, 22.7b, 22.7c, 22.7d-1, and 22.7d-2 are done; parent 22.7d remains an unassignable container row.
- No implementation tests were run because this is story creation only.

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-19 | Story created for child 22.7d-3 with focused CLI/MCP redaction scope, output chokepoint inventory, test evidence requirements, and source references. |
