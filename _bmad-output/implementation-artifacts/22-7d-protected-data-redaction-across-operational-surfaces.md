# Story 22.7d: Protected Data Redaction Across Operational Surfaces

Status: backlog

> Implementation gate cleared 2026-05-18: Stories 22.7a (PR #243, commit 32ca260f), 22.7b (PR #245, commit a6b139d9), and 22.7c (PR #246, commit c8446744) are all merged to main and marked done in `sprint-status.yaml`. The provider-neutral protection metadata/result parity, unreadable taxonomy, and crypto-shredding/restore-safety contracts now exist. Per the container guidance in `sprint-status.yaml`, do not assign this row; split work into child stories 22-7d-1..4 via `bmad-create-story` before any of them can move to `ready-for-dev`.

Context created: 2026-05-14
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR104 protected-data redaction across logs, ProblemDetails, admin APIs, Web UI, CLI, MCP, replay, rebuild, backup validation, and test artifacts. Runtime implementation is blocked until Story 22.7a provides provider-neutral protection metadata/result parity and Stories 22.7b/22.7c provide stable unreadable and restore-safety status contracts, or a human architecture decision explicitly narrows this story to redaction infrastructure preparation only.

## Story

As a security reviewer,
I want diagnostics and admin surfaces to avoid protected payload disclosure,
so that logs, errors, replay, rebuild, backup validation, UI, CLI, MCP, and tests remain useful without leaking protected data.

## Implementation Gate

- Runtime redaction cannot be completed safely against the current code shape. `PayloadProtectionResult` still carries only payload bytes and serialization format, snapshot protection hooks still return raw `object`, and `SnapshotRecord` has no protection metadata. That means EventStore cannot yet distinguish unprotected, protected, unreadable, provider-opaque, restored-conflict, malformed, or legacy-compatible data across operational surfaces.
- This story must stay blocked until Story 22.7a implements provider-neutral event and snapshot protection metadata/result parity in merged code.
- Full runtime work should also wait for Story 22.7b unreadable protected-data taxonomy and Story 22.7c crypto-shredding/restore-safety decision contracts. Without those contracts, admin/CLI/MCP/replay/rebuild surfaces would each invent their own reason codes and leak-risk decisions.
- A human architecture decision may narrow this story to a preparatory "redaction registry and sentinel scan" slice before 22.7a-c land. If that happens, ST0 must record the narrowed scope and this story must not claim FR104 closure.
- Do not mark this story ready-for-dev until the dependency state is rechecked against code, not only against story files.

## Current Implementation Intelligence

- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs` exposes event protect/unprotect hooks plus snapshot protect/unprotect hooks, but snapshot hooks return raw `object` and carry no metadata.
- `src/Hexalith.EventStore.Contracts/Security/PayloadProtectionResult.cs` contains only `PayloadBytes` and `SerializationFormat`. There is no provider-neutral protection metadata, unreadable result, restore-safety status, or redaction classification.
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` calls `ProtectEventPayloadAsync` before state write but persists `Extensions: null`, so protected payload metadata is not currently persisted.
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` calls `UnprotectEventPayloadAsync` before DAPR publish. Its catch block returns and writes `ex.Message` into `EventPublishResult.FailureReason` and `Activity.SetStatus`, so provider exception text is a known no-leak risk for 22.7b/22.7d.
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs` catches non-cancellation snapshot load failures, logs the exception, deletes the snapshot, and falls back to full replay. Protected unreadable snapshots must not be treated as corrupt/delete-safe by default.
- Admin stream inspection models currently expose raw JSON fields such as `EventDetail.PayloadJson`, `AggregateStateSnapshot.StateJson`, `EventStepFrame.EventPayloadJson`, `EventStepFrame.StateJson`, and `SandboxResult.ResultingStateJson`. Their `ToString()` methods redact, but API JSON responses and UI/CLI/MCP rendering must be separately governed.
- Existing admin models already include some redaction-oriented `ToString()` tests: event detail, aggregate state snapshot, event step frame, sandbox command/result, field changes/provenance, dead letters, and projection detail. Reuse and generalize these rather than starting a parallel pattern.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` delegates event detail, state, diff, blame, step, bisect, sandbox, causation, and trace-map reads to EventStore through DAPR service invocation. Redaction status must be returned by EventStore-owned DTOs/statuses, then preserved by Admin.Server without replacing it with generic 500/503 errors.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` creates local `ProblemDetails` for errors and maps some upstream statuses. It must preserve stable protected-data ProblemDetails/status fields when upstream EventStore returns them.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs` currently returns deferred backup/restore/validate/export/import results. This story must not pretend backup validation is live; it may only add redaction-safe deferred/status behavior unless Story 22.7c adds a real restore-admission boundary.
- Admin CLI stream/backup/projection commands and Admin MCP stream/backup/projection tools consume Admin API responses. They need shared display rules so JSON/table/text/tool output never prints protected payload, snapshot state, provider exception text, unsafe key aliases, or provider-private metadata.
- The OperationalEvidence validator already has redaction rule vocabulary for bearer tokens, connection strings, production hostnames, and raw secret markers. Extend that style for protected-data sentinels instead of creating unrelated evidence scanners.

## Acceptance Criteria

1. **A single redaction policy governs every protected-data surface.**
   - Given protected payloads, protected snapshots, unreadable protected data, crypto-shredding statuses, or restored-backup conflicts exist
   - When any EventStore, Admin API, Web UI, CLI, MCP, replay, rebuild, backup-validation, logging, ProblemDetails, trace, exception, docs, or test-artifact surface emits diagnostics
   - Then the surface uses one EventStore-owned redaction classification/policy and does not decide locally from raw payload bytes, exception text, or provider-specific metadata.
   - And the policy defines allowed safe fields per surface: tenant, domain, aggregateId, sequenceNumber, checkpointId, correlationId, causationId, eventTypeName, protection state, provider-neutral status, stable reason code, and operator-safe next-action text.
   - And payload bytes, event payload JSON, snapshot state JSON, command payload JSON, reconstructed state JSON, field values, raw keys, key material, IVs/nonces, authentication tags, unsafe key aliases, provider-private blobs, state-store keys, connection strings, stack traces, and provider exception text are never emitted.

2. **Logs, traces, command status, publish failures, and ProblemDetails are no-leak by construction.**
   - Given protected or unreadable protected content causes success, failure, cancellation, retry, dead-letter, replay, rebuild, or backup validation diagnostics
   - When structured logs, source-generated log messages, OpenTelemetry activities, `Activity.SetStatus`, command status details, `EventPublishResult.FailureReason`, dead-letter records, exceptions, or RFC 7807 ProblemDetails are inspected
   - Then only safe identifiers, stage names, stable reason codes, and safe operator guidance are present.
   - And provider exception messages are sanitized before they reach public failure fields or telemetry status descriptions.
   - And cancellation remains cancellation and is not converted into a redacted protected-data failure.

3. **Admin API and Web UI preserve diagnostic value without protected content.**
   - Given admin stream, event detail, aggregate state, diff, blame, step-through debugger, bisect, sandbox, trace map, projection, dead-letter, consistency, storage, snapshot, and backup surfaces touch protected content or statuses
   - When responses are returned or UI components render them
   - Then protected payload/state fields are either omitted, replaced by a typed redacted placeholder, or represented by a protected/unreadable status with safe metadata only.
   - And Admin.Server preserves upstream protected-data ProblemDetails/status contracts instead of collapsing them to generic 500/503 when the upstream response is available.
   - And the UI renders redacted placeholders, reason codes, and safe next actions without offering copy/export paths for protected content.
   - And existing Fluent UI v5 patterns are preserved; no new UI framework or decorative redesign is introduced.

4. **CLI and MCP output are redaction-safe in every mode.**
   - Given an operator or agent requests stream/event/state/replay/rebuild/backup/projection/diagnostic data through `eventstore-admin` or Admin MCP tools
   - When output is rendered as table, text, JSON, CSV, or MCP tool result content
   - Then protected payloads and snapshot state never appear, even in raw JSON output.
   - And protected-data statuses expose stable machine-readable reason codes and safe non-secret fields so automation can still make decisions.
   - And CLI/MCP clients reuse shared DTO/status/redaction helpers rather than hand-rolling string replacements per command/tool.

5. **Replay, rebuild, backup validation, and deferred backup paths remain honest.**
   - Given replay/read APIs, projection rebuild checkpoints, backup validation, restore admission, import/export, or deferred backup operations touch protected content
   - When diagnostics or results are emitted
   - Then checkpoint/status/progress output never advances past unreadable protected content unless a future approved skip policy explicitly permits it and records audit evidence.
   - And backup validation and restore surfaces return redaction-safe deferred/operator-decision-required statuses when the runtime capability is not implemented.
   - And docs and generated examples do not claim that physical backup validation, restored-backup scanning, or crypto provider verification exists unless Story 22.7c has implemented it.

6. **Testing and documentation prove no protected-data leakage.**
   - Given fixed sentinel values are injected into payloads, snapshots, key aliases, provider metadata, provider exception messages, state-store keys, connection strings, docs examples, and assertion messages
   - When focused tests, docs scans, operational evidence validation, and rendered output checks run
   - Then none of the sentinel values appear in logs, traces, ProblemDetails, command status, dead letters, admin API JSON, UI markup, CLI output, MCP output, replay/rebuild diagnostics, backup results, docs, snapshots, or test failure messages.
   - And Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story out of blocked/review.

## Tasks / Subtasks

- [ ] **ST0 - Recheck blockers and freeze redaction policy.** (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Read this story, Stories 22.7a/22.7b/22.7c, Epic 22, PRD FR102-FR104, architecture `Payload and Snapshot Protection`, Story 22.6 replay/rebuild contracts, `_bmad-output/project-context.md`, and this story's referenced source files before code edits.
  - [ ] Confirm in code whether Story 22.7a metadata/result parity exists: event protection metadata, snapshot metadata/result parity, no-op/default state, legacy compatibility, and provider-opaque handling.
  - [ ] Confirm in code whether Story 22.7b unreadable-data taxonomy exists and whether Story 22.7c restore-safety/status contracts exist.
  - [ ] If the contracts are absent, keep this story blocked or narrow to an explicit preparatory slice; do not invent runtime protected-data statuses.
  - [ ] Define one redaction classification model for content kinds: event payload, command payload, snapshot state, reconstructed state, field value, provider-safe metadata, provider-sensitive metadata, unreadable status, restore-safety status, exception text, and diagnostic text.
  - [ ] Define allowed fields per surface: logs/traces, ProblemDetails, command status, dead letters, Admin API, Admin UI, CLI, MCP, replay/read, rebuild/checkpoints, backup validation/restore, docs, and tests.
  - [ ] Define placeholder text and machine-readable reason-code behavior. Placeholder prose may be localizable text; reason codes are the contract.
  - [ ] Define sentinel values and scan rules for payload plaintext, snapshot plaintext, command payload, key alias, provider-private metadata, provider exception message, state-store key, connection string, stack trace marker, and assertion-output marker.

- [ ] **ST1 - Add shared redaction contracts and helpers.** (AC: 1, 2, 4, 6)
  - [ ] Add or extend provider-neutral contract/helper types in the lowest appropriate package, likely `Hexalith.EventStore.Contracts.Security` or `Hexalith.EventStore.Contracts.Diagnostics`, without adding provider SDK dependencies.
  - [ ] Provide deterministic helpers for redacted placeholders, safe diagnostic fields, safe ProblemDetails extensions, and no-leak sentinel assertions.
  - [ ] Ensure public DTOs can carry "redacted protected content" or "unreadable protected content" status without also carrying raw content.
  - [ ] Keep `ToString()` redaction on DTOs, but do not treat `ToString()` as sufficient for JSON/API/UI/CLI/MCP redaction.
  - [ ] Add Testing builders/fakes for protected/redacted event details, protected/redacted snapshots, unreadable protected statuses, restore-conflict statuses, and sentinel scans.

- [ ] **ST2 - Sanitize runtime diagnostics and failure paths.** (AC: 1, 2, 5, 6)
  - [ ] Sanitize `EventPublisher` failure output so provider exception messages do not reach `EventPublishResult.FailureReason` or `Activity.SetStatus`.
  - [ ] Audit `EventPersister`, `EventPublisher`, `SnapshotManager`, `EventStreamReader`, aggregate replay/read controllers, command status writes, dead-letter publisher/indexes, and ProblemDetails factories for payload/snapshot/provider leakage.
  - [ ] Preserve source-generated logging patterns and structured fields; do not log raw data and do not add ad hoc string interpolation with content values.
  - [ ] Update replay/read and projection rebuild status output so protected/unreadable content reports safe reason codes and checkpoint metadata only.
  - [ ] Ensure snapshot load failures from protected data do not log state, delete records, or present fallback as safe unless 22.7b/22.7c policy explicitly allows it.

- [ ] **ST3 - Update Admin API and Web UI surfaces.** (AC: 3, 5, 6)
  - [ ] Inventory and update Admin Abstractions models that can carry payload/state/error text: `EventDetail`, `AggregateStateSnapshot`, `AggregateStateDiff`, `AggregateBlameView`, `EventStepFrame`, `BisectResult`, `SandboxCommandRequest`, `SandboxEvent`, `SandboxResult`, `DeadLetterEntry`, `ProjectionError`, consistency anomalies, backup/storage/snapshot models, and trace-map models.
  - [ ] Update Admin.Server stream/trace/projection/dead-letter/consistency/backup services and controllers to preserve upstream protected-data statuses and sanitize local `ProblemDetails`.
  - [ ] Update Admin UI pages/components for streams, events, state inspector, diff, blame, debugger, sandbox, projections, dead letters, consistency, storage, snapshots, backups, and diagnostics only where the model can expose protected content.
  - [ ] Ensure copy buttons, export/download actions, detail panels, modals, tooltips, empty/error states, and test snapshots never include protected content.
  - [ ] Preserve Fluent UI v5 patterns and existing compact operational UI behavior.

- [ ] **ST4 - Update CLI and MCP surfaces.** (AC: 4, 5, 6)
  - [ ] Update Admin CLI stream, backup, projection, consistency, and diagnostic commands so table/text/JSON/CSV output uses shared redaction/status helpers.
  - [ ] Update Admin MCP stream, backup, projection, consistency, storage, and diagnostic tools so tool results never include protected content and still include safe machine-readable reason codes.
  - [ ] Update CLI/MCP HTTP clients to preserve protected-data ProblemDetails/status extensions rather than reducing them to exception messages.
  - [ ] Add CLI/MCP tests for raw JSON mode, table mode, failed HTTP ProblemDetails, backup deferred/status output, and tool result serialization.

- [ ] **ST5 - Update docs, examples, evidence validation, and scans.** (AC: 5, 6)
  - [ ] Update `docs/guides/payload-protection-and-crypto-shredding.md` with redaction guarantees, safe fields, unsafe fields, surface matrix, and dependency notes on 22.7a-c.
  - [ ] Update `docs/reference/stream-replay-api.md`, `docs/guides/disaster-recovery.md`, problem reference docs, Admin docs, CLI docs, MCP docs, and OpenAPI examples only where the surface exists.
  - [ ] Extend OperationalEvidence redaction rules or add an equivalent focused protected-data sentinel scan, reusing existing validator patterns where practical.
  - [ ] Scan docs and generated examples for sentinel leakage, unsafe key aliases, provider exception examples, state-store keys, connection strings, and plaintext markers.
  - [ ] Run focused test projects individually and record exact evidence commands/results.

## Test Evidence Required

- Contract tests prove redaction classification/status serialization, safe fields, unsafe field rejection, placeholder behavior, and no-op/legacy compatibility.
- Runtime tests prove `EventPublisher`, `EventPersister`, `SnapshotManager`, replay/read, rebuild/checkpoint, command status, dead-letter, and ProblemDetails paths do not leak sentinel payloads, snapshot state, provider metadata, provider exception text, state-store keys, or connection strings.
- Admin Abstractions tests prove every DTO carrying payload/state/error text has safe `ToString()` and JSON/status behavior where applicable.
- Admin Server tests prove upstream protected-data ProblemDetails/status contracts are preserved and local unexpected errors stay generic.
- Admin UI bUnit tests prove rendered markup, copy/export actions, detail panels, modals, and error states do not contain sentinel content.
- CLI tests prove table, text, JSON, and CSV output do not contain sentinel content.
- MCP tests prove tool result content and serialized responses do not contain sentinel content.
- Documentation/evidence tests prove docs examples and operational evidence artifacts do not contain sentinel content.
- Cancellation tests prove `OperationCanceledException` remains cancellation, not redacted protected-data failure.
- Evidence commands must be recorded exactly. Minimum expected commands after dependencies are unblocked: `dotnet test tests/Hexalith.EventStore.Contracts.Tests`, focused `dotnet test tests/Hexalith.EventStore.Server.Tests --filter ...` slices, `dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests`, focused `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --filter ...`, `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --filter ...`, `dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests --filter ...`, `dotnet test tests/Hexalith.EventStore.Admin.Mcp.Tests --filter ...`, and `dotnet test tests/Hexalith.EventStore.Testing.Tests` when Testing helpers change.

## Developer Notes

- Preserve EventStore ownership of envelope metadata. Domain services must not own redaction, protection metadata, key lifecycle, unreadable statuses, or restore-safety decisions.
- Do not infer protection state from payload shape, JSON content, byte length, exception text, or provider-specific metadata. Use the provider-neutral contracts from 22.7a-c.
- Treat key aliases and provider identifiers as sensitive by default. Expose them only where ST0 explicitly marks the field safe for the exact surface.
- Do not add a real encryption provider, Key Vault, KMS, DAPR secret-store, certificate, or algorithm dependency in this story.
- Do not rely on `ToString()` redaction for serialized API output. API/JSON/UI/CLI/MCP paths need explicit model/status behavior.
- Prefer one reusable redaction/sentinel helper over scattered string replacement. String replacement after serialization is a fallback only for evidence scanning, not the primary policy.
- Keep diagnostic output useful: stable reason codes, sequence/checkpoint metadata, correlation IDs, tenant/domain/aggregate identity, and safe next-action hints should remain available.
- Do not bypass `IActorStateManager` or read actor state directly to inspect protected content.
- If compatibility pressure conflicts with no-leak guarantees, preserve no-leak behavior and record the compatibility gap for product/architecture decision.
- `Hexalith.EventStore.Server.Tests` has known pre-existing CA2007 warning-as-error build failures in this workspace; run focused slices and record exact commands/results rather than claiming a clean full project run unless verified.

## Files Likely Touched

- `src/Hexalith.EventStore.Contracts/Security/*ProtectionMetadata*.cs`
- `src/Hexalith.EventStore.Contracts/Security/*Unreadable*.cs`
- `src/Hexalith.EventStore.Contracts/Security/*Redaction*.cs`
- `src/Hexalith.EventStore.Contracts/Problems/GatewayProblemDetailsExtensions.cs`
- `src/Hexalith.EventStore.Contracts/Replay/*`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs`
- `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotRecord.cs`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore/ErrorHandling/*`
- `src/Hexalith.EventStore/Controllers/*Replay*`
- `src/Hexalith.EventStore/Controllers/*Stream*`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/*`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/DeadLetters/*`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/*`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/*`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprDeadLetterQueryService.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/*`
- `src/Hexalith.EventStore.Admin.UI/Components/*`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Stream/*`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/*`
- `src/Hexalith.EventStore.Admin.Cli/Formatting/*`
- `src/Hexalith.EventStore.Admin.Mcp/Tools/StreamTools.cs`
- `src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs`
- `src/Hexalith.EventStore.Admin.Mcp/AdminApiClient*.cs`
- `src/Hexalith.EventStore.Testing/Builders/*`
- `src/Hexalith.EventStore.Testing/Fakes/*`
- `tests/Hexalith.EventStore.Contracts.Tests/Security/*`
- `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/*`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/*`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/*`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/*`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/*`
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/*`
- `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/*`
- `docs/guides/payload-protection-and-crypto-shredding.md`
- `docs/guides/disaster-recovery.md`
- `docs/reference/stream-replay-api.md`
- `docs/reference/problems/*protected*.md`

## Out of Scope

- Implementing real encryption, key wrapping, KMS, Key Vault, DAPR secret-store integration, certificates, or provider-specific crypto.
- Implementing Story 22.7a protection metadata/result parity.
- Implementing Story 22.7b unreadable protected-data taxonomy.
- Implementing Story 22.7c crypto-shredding workflow, restore admission, physical backup scanning, or provider verification.
- Deleting immutable event records, snapshots, or audit records as a redaction mechanism.
- Skip-past-unreadable replay or rebuild behavior without a future explicit product and architecture decision.
- Replacing the Admin UI design system or adding a new frontend framework.

## References

- `_bmad-output/planning-artifacts/epics.md` - Story 22.7d requirements and Epic 22 story split.
- `_bmad-output/planning-artifacts/prd.md` - FR104 and NFR12.
- `_bmad-output/planning-artifacts/architecture.md` - Payload and Snapshot Protection; SEC-5; Publishing, Replay & Protection Contracts.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12.md` - split of oversized Story 22.7 into 22.7a-22.7d.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md` - Epic 22 integration-hardening goals and payload-protection gap summary.
- `_bmad-output/project-context.md` - package boundaries, logging, DAPR, actor state, and testing rules.
- `_bmad-output/implementation-artifacts/22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints.md` - replay/read API and rebuild checkpoint context.
- `_bmad-output/implementation-artifacts/22-7a-payload-and-snapshot-protection-hooks.md` - protection metadata prerequisite.
- `_bmad-output/implementation-artifacts/22-7b-unreadable-protected-data-behavior.md` - unreadable protected-data dependency and broad redaction deferrals.
- `_bmad-output/implementation-artifacts/22-7c-crypto-shredding-workflow-and-restored-backup-safety.md` - crypto-shredding/restore-safety dependency and redaction deferrals.
- `src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs`
- `src/Hexalith.EventStore.Contracts/Security/PayloadProtectionResult.cs`
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs`
- `src/Hexalith.EventStore.Server/Events/SnapshotManager.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`
- Microsoft Learn: [Logging in C# and .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/overview) - current structured logging and source-generation guidance.
- Microsoft Learn: [Compile-time logging source generation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/source-generation) - current sensitive-data/redaction warning and compliance-redaction pointer.
- Microsoft Learn: [Data redaction in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/data-redaction) - current .NET redaction library guidance; do not add the package unless ST0 approves a dependency.
- Microsoft Learn: [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/en-us/aspnet/core/web-api/handle-errors) - current ProblemDetails API guidance.

## Dev Agent Record

### Agent Model Used

TBD by dev agent.

### Debug Log References

TBD by dev agent.

### Completion Notes List

TBD by dev agent.

### File List

TBD by dev agent.

## Verification Status

- Story context created on 2026-05-14.
- Pre-dev blocker verified against current code: Story 22.7a metadata/result parity is absent (`PayloadProtectionResult` has only bytes/format, snapshot protection hooks return raw `object`, and `SnapshotRecord` has no protection metadata).
- No implementation tests were run because this is story creation only.

## Change Log

| Date | Change |
| --- | --- |
| 2026-05-14 | Story created from Epic 22.7d with blocked status, explicit dependencies on 22.7a-c, operational redaction scope, current implementation inventory, acceptance criteria, task plan, test evidence, and source references. |
