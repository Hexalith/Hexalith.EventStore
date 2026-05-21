# Post-Epic Deferred DW18: Stream Export Backend

Status: ready-for-dev

Context created: 2026-05-21
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-admin-deferred-quick-wins.md`
Source proposal status: Approved by Jerome on 2026-05-21 via Party Mode review follow-up.
Source baseline: `_bmad-output/implementation-artifacts/post-epic-deferred-dw14-admin-deferred-operations-ux-policy.md`
Related completed baseline: `_bmad-output/implementation-artifacts/16-4-backup-and-restore-console.md`
Primary dependencies: Story 22.6 stream read APIs and Story 22.7d protected-data redaction surfaces.

## Story

As an EventStore administrator using the Admin API, Web UI, or CLI,
I want single-stream export for a specific tenant, domain, and aggregate to run against the real EventStore stream-read path,
so that I can download an auditable bounded stream export without direct state-store access or protected-data leakage.

## Decision Locked For This Story

Implement only the Group A DW18 backend slice from the 2026-05-21 sprint change proposal. This story replaces the DW14 stream-export `Deferred` response with a real bounded export path.

The export must reuse Story 22.6 public stream read contracts and the EventStore-owned stream-read authorization/redaction boundary. Do not read event keys directly from Admin.Server with `DaprClient`, do not reconstruct actor state from raw DAPR keys, and do not create a parallel stream reader.

Use the existing `StreamExportResult` response contract for DW18. The export content is returned through `StreamExportResult.Content` as bounded string content because the current Admin API, UI, and CLI already consume that shape. Streaming/chunked responses are explicitly deferred to a later transport-improvement story if real export sizes prove the compatibility shape is not enough.

If a stream contains more than `MaxEvents` events, export the most recent 50,000 events and mark the export as truncated in the exported document. This is an explicit bounded-window export, not a silent partial export. Protected/unreadable/corrupt content still fails the whole export and must not produce downloadable content.

Export is single-stream only: `(tenantId, domain, aggregateId)`. Cross-stream export, time-bounded export, backup manifest export, restore, import, compaction, and backup creation/validation remain out of scope.

## Scope

This story covers:

- `DaprBackupCommandService.ExportStreamAsync` no longer returning the deferred `StreamExportResult`.
- A real export implementation that pages through `api/v1/streams/read` / `StreamReadRequest` with `AggregateId` supplied.
- Bounded export limit with `MaxEvents = 50000` as the default configurable cap; larger streams export the most recent bounded window with explicit truncation metadata.
- JSON and CloudEvents export formats using the existing UI and CLI format selector values: `JSON` and `CloudEvents`.
- Fail-closed protected-data handling using the existing 22.7d redaction/readability machinery.
- Safe audit evidence per export: who, tenant, domain, aggregate, requested format, event count, truncation/limit status, timestamp, and correlation id.
- Admin UI cleanup on `/backups > Export Stream` so stream export is no longer presented as deferred once the backend path is green.
- CLI/API/client/test updates required by any chosen export response shape.
- Documentation of the compatibility response decision and memory-bound test evidence.

This story must not change:

- Backup creation, backup validation, restore, import, or compaction deferred behavior.
- Existing backup job list, validate/restore visibility rules, import preview/file-size guard, active backup/restore guards, stat cards, refresh behavior, and failed-job expansion.
- Story 22.6 stream-read authorization, paging, or ProblemDetails semantics except for focused fixes proven necessary by export tests.

## Acceptance Criteria

1. **Admin stream export delegates to the real EventStore stream-read path.**
   - Given an Admin calls `POST /api/v1/admin/backups/export-stream` with tenant, domain, aggregateId, and format
   - When tenant authorization passes
   - Then `DaprBackupCommandService.ExportStreamAsync` reads the target stream through the Story 22.6 stream-read API or equivalent EventStore-owned service invocation.
   - And Admin.Server remains a facade: it may enforce the existing Admin API request-tenant allow-list gate, then forwards the request/token and coordinates output only; EventStore remains authoritative for stream RBAC, actor traversal, payload decoding, redaction policy, and DAPR state-store access.
   - And tenant/domain/aggregate authorization is proven before any EventStore stream-read invocation that could create actor proxies or access actor state; Admin.Server must not create a second stream authorization or readability policy.
   - And it no longer returns the DW14 deferred message or a fake success on the happy path.
   - And backup creation, backup validation, restore, import, and compaction continue to return their existing deferred/accepted-debt outcomes.

2. **Export is bounded and paged.**
   - Given the target stream has zero to more than 50,000 events
   - When export runs
   - Then it reads pages in deterministic sequence order using `StreamReadRequest.FromSequence`, `ToSequence`, `PageSize`, and `StreamReadMetadata.LastSequenceReturned`/`IsTruncated` semantics.
   - And it exports at most the configured `MaxEvents` default of 50,000.
   - And the implementation obtains `LatestSequence` from the first authorized `StreamReadMetadata` page/probe, then restarts at `LatestSequence - MaxEvents + 1` when the stream is oversized; it must not export the oldest 50,000 events and only later mark truncation.
   - And if `LatestSequence > MaxEvents`, the export succeeds only for the most recent 50,000 events, starting at `LatestSequence - MaxEvents + 1`; the exported document includes `truncated: true`, `exportLimit: 50000`, `latestSequence`, `fromSequence`, and `toSequence`.
   - And if the stream has no events, no exportable sequence, or does not exist, the operation returns `Success=false`, `Content=null`, `FileName=null`, and a typed safe reason such as `MissingStream` or `NoExportableEvents`; no fake empty export success.
   - And zero/negative or implementation-configured invalid `MaxEvents` values fail configuration/tests; callers cannot request arbitrary per-call limits in DW18.
   - And the implementation does not rely on unsupported continuation tokens; current `StreamsController` rejects non-null continuation tokens, so pagination must advance by sequence (`lastSequenceReturned + 1`) unless continuation support is implemented in this story.

3. **Format handling is explicit and stable.**
   - Given format is `JSON`
   - When export succeeds
   - Then `StreamExportResult.Content` contains one JSON document with tenant, domain, aggregateId, format, eventCount, latestSequence, fromSequence, toSequence, exportLimit, truncated, exportedAtUtc, and an ordered `events` array with safe event metadata and payload bytes serialized according to existing stream-read contract rules.
   - Given format is `CloudEvents`
   - When export succeeds
   - Then `StreamExportResult.Content` contains one JSON document with the same export metadata and an ordered `events` array where each item is a CloudEvents-compatible JSON event using existing envelope metadata where available.
   - And supported format values are exactly `JSON` and `CloudEvents` after trimming and ordinal-ignore-case normalization; unsupported or blank values return `RejectedValidation` and do not silently fall back to JSON.
   - And `FileName` uses a sanitized `.json` filename for both formats, for example `{tenantId}_{domain}_{aggregateId}_{timestamp}.json`.

4. **Protected-data and redaction safety are fail-closed.**
   - Given any event page contains protected, provider-opaque, unreadable, corrupt, or otherwise non-exportable payload material
   - When export attempts to serialize that stream
   - Then the operation fails with a stable safe error code such as `RedactionRequired` or an existing 22.7d reason code mapped from `StreamReplayReasonCodes.ProtectedPayloadUnavailable`.
   - And `Success=false`, `Content=null`, and `FileName=null`; no partial export is downloaded or reported as success.
   - And API responses, UI toasts, CLI output, logs, audit records, and test artifacts include only safe identifiers/reason codes and never include protected payload bytes, snapshot state, key aliases, provider exception text, raw state-store keys, secrets, connection strings, or DAPR addresses.

5. **Missing, invalid, unauthorized, and unavailable cases are typed.**
   - Missing stream maps to a safe not-found export result or ProblemDetails path; no fake empty export success.
   - Invalid tenant/domain/aggregateId/format maps to typed validation failure.
   - Tenant mismatch or insufficient Admin authorization is rejected before any EventStore stream-read invocation, actor proxy creation, or state access.
   - EventStore stream-read 400/403/404/409/500/503 ProblemDetails are preserved or safely mapped to stable export failure codes without raw exception text.
   - Cancellation propagates and does not write success audit evidence.

6. **Download path is real and memory-bounded.**
   - Given an export succeeds
   - When the Admin API, Web UI, or CLI consumes it
   - Then the content path is real downloadable content, not a deferred toast or placeholder.
   - And DW18 uses the bounded compatibility response strategy: `StreamExportResult.Success=true`, `Content` contains the serialized export document, `FileName` contains the sanitized download filename, and `EventCount` is the number of exported events.
   - And `StreamExportResult.EventCount` equals the exported document `eventCount`; both count exported events only, not total stream length or `latestSequence`.
   - And memory-bound tests cover the largest practical fake export shape under the 50,000-event cap, including page-loop behavior, without introducing a streaming transport redesign.
   - And the Dev Agent Record states that streaming/chunked export remains deferred unless a measured failure proves the compatibility response unsafe.

7. **Audit evidence is safe and useful.**
   - Given export succeeds, fails validation, fails protected-data readability, or fails because EventStore is unavailable
   - When the operation completes
   - Then the system emits a safe audit/log entry with subject (`sub`), tenant, domain, aggregateId, format, requested cap, exported count when successful, truncation flag, reason code when failed, and correlation id.
   - And success audit/log evidence is emitted only after serialization succeeds and downloadable `Content` plus `FileName` exist.
   - And audit/log entries exclude event payloads and provider details.
   - And failed validation/auth-before-stream-read cases do not claim an export file or success.
   - And cancellation emits no success evidence and may emit only safe cancellation/failure evidence if the existing logging pattern supports it.

8. **Admin UI stream export no longer presents deferred state once backend is green.**
   - Given DW18 backend validation is green
   - When `/backups` renders the Export Stream action
   - Then only the stream-export deferred badge, deferred dialog notice, `Submit Deferred Request` label, and deferred warning path are removed or replaced for stream export.
   - And the final action label is `Export`, the stream export action remains available/enabled for Admin users, success triggers `blazorDownloadFile` or the existing download helper with `Content` and `FileName`, and success closes the dialog.
   - And typed non-success results show truthful error/warning copy, keep or reopen the dialog as appropriate, and always clear busy state.
   - And backup creation and backup validation continue to show their deferred badges and deferred warning behavior, including `data-deferred-action="backup-create"` and `data-deferred-action="backup-validate"`.
   - And import, restore, backup job listing, stat cards, refresh, active operation guards, and failed-job expansion remain unchanged.

9. **CLI and client behavior remains scriptable.**
   - Given `eventstore-admin backup export-stream ... --export-format JSON|CloudEvents`
   - When export succeeds
   - Then the command returns success and either writes the exported content to the requested output target or prints a safe summary consistent with the existing `GlobalOptions.OutputFile`/format behavior.
   - And failure returns a non-zero exit code with safe error text.
   - And tests prove the command passes format in the request and does not print protected payload on failure.

10. **Tests and evidence prove the real path.**
    - Admin.Server tests cover DAPR/EventStore invocation, bearer-token forwarding where applicable, page loop behavior, format validation, missing stream, EventStore ProblemDetails mapping, timeout/unavailable safe messages, cancellation, and unchanged deferred operations.
    - EventStore/Server tests cover export-facing use of stream-read pages or a focused export route if added, including auth-before-actor access, missing/corrupt/unreadable protected events, truncation, and page ordering.
    - Admin.UI bUnit tests cover no deferred stream-export badge, `Export` label, success download path, non-success busy-state clearing, and backup creation/validation deferred regressions.
    - CLI tests cover JSON and CloudEvents request formats, output target behavior, safe failure text, and non-zero exit codes.
    - Protected-data sentinel tests scan export result, UI/CLI output, logs/evidence strings, and saved test artifacts for no-leak guarantees.
    - Tier 2 or Aspire-backed evidence covers a seeded aggregate export through the Admin API when the environment is available.
    - Test names or comments map to these guardrail IDs:
      - `DW18-AC1`: bounded stream export succeeds through EventStore stream read.
      - `DW18-AC2`: no raw DAPR state access or Admin.Server-owned stream traversal.
      - `DW18-AC3`: JSON export contract and filename.
      - `DW18-AC4`: CloudEvents export contract and filename.
      - `DW18-AC5`: protected/unreadable/corrupt payload fails closed with safe evidence.
      - `DW18-AC6`: only stream export UI is no longer deferred.
      - `DW18-AC7`: CLI export success/failure remains scriptable.

## Tasks / Subtasks

- [ ] Reconfirm baseline and write ST0 decision table before editing. (AC: 1, 2, 3, 6, 8)
  - [ ] Read `DaprBackupCommandService.ExportStreamAsync`, `AdminBackupsController.ExportStream`, `StreamExportRequest`, `StreamExportResult`, `IBackupCommandService`, `AdminBackupApiClient.ExportStreamAsync`, `Backups.razor`, `BackupExportStreamCommand`, and current export tests.
  - [ ] Read `StreamsController.ReadStreamAsync`, `StreamReadRequest`, `StreamReadPage`, `StreamReadEvent`, `StreamReadMetadata`, and `StreamReplayReasonCodes`.
  - [ ] Record the locked response strategy: bounded compatibility `StreamExportResult.Content` response for DW18; streaming/chunked transport deferred.
  - [ ] Record the locked oversized-stream rule: export newest 50,000 events with explicit truncation metadata.
  - [ ] Record the JSON and CloudEvents document shapes from this story as golden-contract fixtures before production code edits.
  - [ ] Confirm no DAPR/AppHost/access-control change is needed before editing. Only change DAPR access-control/AppHost if a narrow runtime failure proves the service invocation path requires it.

- [ ] Implement or adapt the backend export reader. (AC: 1, 2, 3, 4, 5, 6, 7)
  - [ ] Replace the deferred branch in `DaprBackupCommandService.ExportStreamAsync`.
  - [ ] Invoke EventStore stream reads through the existing service invocation/client pattern, preserving bearer-token forwarding and bounded timeout behavior.
  - [ ] Prove Admin.Server does not direct-read DAPR state-store keys, actor state keys, or event keys for export.
  - [ ] Page with `PageSize <= 1000`, because `StreamsController` currently caps stream read pages at 1,000.
  - [ ] Read the first authorized page/probe to capture `StreamReadMetadata.LatestSequence`; if `LatestSequence > MaxEvents`, restart the export window at `latestSequence - MaxEvents + 1`.
  - [ ] Advance by `LastSequenceReturned + 1` while continuation tokens remain unsupported.
  - [ ] For oversized streams, calculate `fromSequence = latestSequence - MaxEvents + 1` and export through `toSequence = latestSequence`.
  - [ ] Map stream-read ProblemDetails reason codes to safe export failure codes and messages.
  - [ ] Validate/normalize export format explicitly.
  - [ ] Serialize JSON and CloudEvents deterministically.
  - [ ] Generate safe filenames, for example `{tenantId}_{domain}_{aggregateId}_{timestamp}.json`, after sanitizing path-hostile characters.
  - [ ] Emit safe audit/log evidence with source-generated logger patterns if adding new structured logs.

- [ ] Preserve redaction and fail-closed behavior. (AC: 4, 5, 7, 10)
  - [ ] Treat `StreamReplayReasonCodes.ProtectedPayloadUnavailable`, unreadable protected-data ProblemDetails, corrupt event, and provider-unavailable readability outcomes as failed export, not redacted-success.
  - [ ] Use existing `ProtectedDataDiagnosticRedactor`, `ProtectedDataReadabilityDecision`, `AdminRedactedContent`, or `ProtectedDataLeakSentinel` patterns rather than creating a second redaction taxonomy.
  - [ ] Do not log or return raw payload bytes, key aliases, provider exception messages, state-store keys, DAPR addresses, or connection strings.
  - [ ] Add sentinel regression coverage over any export content/error/audit artifacts created by this story.

- [ ] Update Admin API, client, UI, and CLI surfaces consistently. (AC: 6, 8, 9)
  - [ ] Preserve the `StreamExportResult` response shape unless fields are necessary for compatibility-safe truncation metadata; keep existing consumers compiling.
  - [ ] Put export metadata inside `StreamExportResult.Content` even if optional result-level fields are added.
  - [ ] In `Backups.razor`, remove only stream-export deferred UI/copy and re-enable the download path on successful export with content/stream and filename.
  - [ ] Keep `data-deferred-action="backup-create"` and `data-deferred-action="backup-validate"` behavior unchanged; remove or replace only the stream export deferred marker.
  - [ ] Ensure UI success closes the export dialog, clears `_isOperating`, and does not download on typed non-success results.
  - [ ] Ensure CLI failures write safe text to stderr and successes respect `--format` and output-file behavior.

- [ ] Add focused tests. (AC: 1-10)
  - [ ] Update `DaprBackupCommandServiceTests`: stream export invokes EventStore/read path; backup trigger, validation, restore, and import still return deferred without EventStore calls.
  - [ ] Update `AdminBackupsControllerTests`: tenant mismatch fails before service call; success/failure shape is mapped safely.
  - [ ] Add or update EventStore stream/export tests for page ordering, truncation, missing stream, invalid range, unreadable protected payload, corrupt event, and service unavailable.
  - [ ] Update `BackupsPageTests`: stream export no longer shows deferred badge/notice/submit label, success downloads, non-success clears busy state, backup create/validate deferred tests remain.
  - [ ] Update `AdminBackupApiClientTests` and `BackupExportImportCommandTests` for the chosen response/download strategy.
  - [ ] Add protected-data sentinel assertions for export success/failure surfaces and evidence artifacts.
  - [ ] Add golden-contract tests for JSON and CloudEvents content shapes.
  - [ ] Add regression tests for oversized streams proving newest-window export and explicit truncation metadata.

- [ ] Validate and capture evidence. (AC: 10)
  - [ ] Run focused Admin.Abstractions tests if DTOs changed.
  - [ ] Run focused Admin.Server backup command/controller tests.
  - [ ] Run focused EventStore.Server stream/export tests. If `Hexalith.EventStore.Server.Tests` still hits the known CA2007 build blocker, record the unchanged blocker and run the narrowest compilable subset.
  - [ ] Run focused Admin.UI backups bUnit tests.
  - [ ] Run focused Admin.Cli backup export tests.
  - [ ] If possible, run Aspire with `EnableKeycloak=false`, seed an aggregate, export JSON and CloudEvents, exercise truncation or a smaller configured cap, and save sanitized API/download/audit evidence under `_bmad-output/test-artifacts/post-epic-deferred-dw18-stream-export-backend/`.
  - [ ] Update this story's Dev Agent Record, File List, Verification Status, and Change Log before moving to review.

## Dev Notes

### Current State To Preserve

- `DaprBackupCommandService.ExportStreamAsync` currently returns `StreamExportResult(false, ..., ErrorMessage="Stream export is deferred...")` without making an EventStore call. DW18 replaces only this method's stream export path.
- `DaprBackupCommandService.TriggerBackupAsync`, `ValidateBackupAsync`, `TriggerRestoreAsync`, and `ImportStreamAsync` remain deferred/accepted debt. Tests must prove they still make zero upstream EventStore calls.
- `AdminBackupsController.ExportStream` already exposes `POST api/v1/admin/backups/export-stream`, requires Admin policy, manually validates request-body tenant access, and returns `Ok(StreamExportResult)`. If response streaming is chosen, update this controller and clients consistently.
- `Backups.razor` currently shows `data-deferred-action="stream-export"`, a deferred notice in the export dialog, and always routes export results through `ShowWarningToastBestEffortAsync`. DW18 removes this only for stream export.
- `BackupExportStreamCommand` already posts `StreamExportRequest` to `api/v1/admin/backups/export-stream`, passes `--export-format`, returns non-zero on `Success=false`, and writes formatted output via `OutputWriter`.
- `StreamExportRequest` currently has only `TenantId`, `Domain`, `AggregateId`, and `Format`. Do not add cross-stream, time range, or arbitrary filter fields in this story.
- `StreamExportResult` currently carries `Content` as a string. DW18 deliberately keeps this response shape for compatibility across Admin API, Blazor UI, and CLI; streaming is deferred.

### Response Contract Decision

DW18 uses `StreamExportResult.Content` as the downloadable content carrier. This keeps the existing `IBackupCommandService`, `AdminBackupsController`, `AdminBackupApiClient`, and `BackupExportStreamCommand` shape intact and lets the story focus on real export behavior rather than transport redesign.

The compatibility contract is acceptable only because export is bounded:

- `MaxEvents = 50000` by default.
- Page size remains <= 1000.
- Larger streams export the newest 50,000-event window with explicit truncation metadata.
- Tests must include a memory-bound fake export and page-loop proof.
- If the bounded string response proves unsafe in implementation or runtime evidence, stop and split a follow-up streaming transport story instead of smuggling streaming into DW18.

### Stream Read API Facts

- `StreamReadRequest` requires `Tenant`, `Domain`, and currently an `AggregateId` for the implemented stream read route. Omit `AggregateId` only remains unsupported for this export story.
- `StreamsController.ReadStreamAsync` enforces tenant and RBAC checks before actor proxy creation.
- Current `StreamsController` max page size is 1,000. Use pages no larger than that.
- `StreamReadMetadata.LatestSequence` is the supported source for oversized-window calculation. Use the first authorized read result to determine whether to restart at the newest bounded window; do not invent a raw state query or side-channel to discover stream length.
- Current continuation token support is fail-closed: non-null `ContinuationToken` is rejected as `StreamReplayReasonCodes.InvalidContinuation`. Export pagination must advance by sequence until continuation tokens are implemented.
- `StreamReadEvent.Payload` contains already-readable payload bytes. If the replay path cannot make protected payload readable, `StreamsController` returns unreadable protected-data ProblemDetails rather than returning opaque bytes.
- `StreamReplayReasonCodes` already includes `MissingStream`, `MissingEvent`, `CorruptEvent`, `ProtectedPayloadUnavailable`, `ServiceUnavailable`, `InvalidRange`, `InvalidAggregateIdentity`, `UnauthorizedTenant`, and `ForbiddenReplayScope`. Reuse or map these instead of inventing arbitrary strings.

### Format Guidance

JSON export should prefer one stable envelope:

```json
{
  "tenantId": "tenant-a",
  "domain": "counter",
  "aggregateId": "counter-1",
  "format": "JSON",
  "eventCount": 2,
  "latestSequence": 2,
  "fromSequence": 1,
  "toSequence": 2,
  "exportLimit": 50000,
  "truncated": false,
  "exportedAtUtc": "2026-05-21T00:00:00Z",
  "events": [
    {
      "sequenceNumber": 1,
      "eventTypeName": "counter-incremented-v1",
      "messageId": "01J...",
      "correlationId": "01J...",
      "causationId": "01J...",
      "timestamp": "2026-05-21T00:00:00Z",
      "userId": "operator-subject",
      "metadataVersion": 1,
      "serializationFormat": "json",
      "protectionState": "Unprotected",
      "payload": {}
    }
  ]
}
```

Each event should carry safe metadata needed to replay/import later: sequence number, event type, message id, correlation id, causation id, timestamp, user id when non-empty, metadata version, serialization format, protection metadata status, and payload only when readable and approved by the stream-read path.

CloudEvents export uses the same top-level export envelope with `format: "CloudEvents"` and an `events` array of CloudEvents-compatible objects:

```json
{
  "tenantId": "tenant-a",
  "domain": "counter",
  "aggregateId": "counter-1",
  "format": "CloudEvents",
  "eventCount": 1,
  "latestSequence": 1,
  "fromSequence": 1,
  "toSequence": 1,
  "exportLimit": 50000,
  "truncated": false,
  "exportedAtUtc": "2026-05-21T00:00:00Z",
  "events": [
    {
      "specversion": "1.0",
      "id": "01J...",
      "source": "/eventstore/tenants/tenant-a/domains/counter/aggregates/counter-1",
      "type": "counter-incremented-v1",
      "subject": "tenant-a/counter/counter-1#1",
      "time": "2026-05-21T00:00:00Z",
      "datacontenttype": "application/json",
      "data": {}
    }
  ]
}
```

CloudEvents fields should use existing envelope fields where possible:

- `id`: event message id.
- `source`: stable EventStore stream source, not a raw state-store key.
- `type`: persisted event type name.
- `subject`: tenant/domain/aggregate/sequence scope without payload.
- `time`: event timestamp.
- `datacontenttype`: JSON unless the stream event says otherwise.
- `data`: readable event payload.

Oversized streams use the same shape with `truncated: true`, `fromSequence = latestSequence - 49999`, `toSequence = latestSequence`, `eventCount = 50000`, and `exportLimit = 50000`. The top-level `StreamExportResult.EventCount` must equal this document `eventCount`.

Do not claim import compatibility unless `ImportStreamAsync` remains deferred; import validation is out of scope.

### Security And Safety Guardrails

- Authorization and tenant check must happen before any stream-read invocation.
- Admin.Server may enforce the existing Admin API tenant allow-list gate, then must defer stream RBAC, actor access, traversal, and readability behavior to the EventStore stream-read API boundary. It must not direct-read DAPR state-store keys, actor state, or event keys for export.
- Use `sub` as the operator identity in audit/log evidence. Do not use display name or untrusted claims.
- Do not expose payload in logs. Successful export content is the only approved place where readable payload bytes may appear.
- Write success audit/log evidence only after export serialization succeeds and downloadable content exists; failed validation, protected payload, unavailable EventStore, and cancellation must not claim an export file or success.
- A protected/unreadable event makes the export fail closed; do not produce a mixed export with redacted placeholder events unless product explicitly changes the requirement.
- Do not add new packages or infrastructure dependencies.
- Do not use backend-specific state-store scans or Redis commands.
- Do not change DAPR access-control/AppHost files unless a runtime failure proves the existing Admin.Server -> EventStore invocation path does not permit `api/v1/streams/read`.
- Keep API errors aligned with existing ProblemDetails/typed result patterns; no custom error envelopes.

### Likely Files To Modify

- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StreamExportRequest.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StreamExportResult.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IBackupCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminBackupApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/BackupExportStreamCommand.cs`
- `src/Hexalith.EventStore/Controllers/StreamsController.cs` only for focused fixes proven necessary by export tests.
- `docs/reference/` for the max-event export limit and format contract.
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/**`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprBackupCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminBackupsControllerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/StreamsControllerTests.cs` or focused export tests if a new route is added.
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/BackupsPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminBackupApiClientTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/BackupExportImportCommandTests.cs`
- `_bmad-output/test-artifacts/post-epic-deferred-dw18-stream-export-backend/`

### Previous Story Intelligence

- DW14 intentionally made deferred backup/export operations honest in the UI. DW18 must remove deferred behavior only for stream export after real backend support exists; backup creation, backup validation, restore, import, and compaction remain separately owned.
- DW16 locked the pattern that Admin.Server is a facade and EventStore owns write/actor state boundaries. Reuse that boundary: Admin.Server forwards JWT and typed results; EventStore stream-read route performs tenant/RBAC/actor access.
- DW17 is still ready-for-dev at story creation time. Do not depend on DW17 being implemented unless the dev agent starts after DW17 is done and records the updated baseline.
- Story 22.6 shipped `StreamReadRequest`, `StreamReadPage`, `EventStoreGatewayClient.ReadStreamAsync`, Testing fakes/builders, and the server `StreamsController`. Use those instead of raw DAPR reads.
- Story 22.7d-4 added protected-data leak sentinel patterns and explicitly found `DaprBackupCommandService.InvokeEventStorePostAsync` safe-message handling important. Keep invocation failures sanitized.

### Testing Notes

Recommended first-pass commands:

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
dotnet test tests/Hexalith.EventStore.Admin.Abstractions.Tests --configuration Release --filter "FullyQualifiedName~StreamExport" -m:1
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release --filter "FullyQualifiedName~DaprBackupCommandService|FullyQualifiedName~AdminBackupsController" -m:1
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~BackupsPage|FullyQualifiedName~AdminBackupApiClient" -m:1
dotnet test tests/Hexalith.EventStore.Admin.Cli.Tests --configuration Release --filter "FullyQualifiedName~BackupExport" -m:1
```

For EventStore.Server tests, prefer focused filters around `StreamsController`, stream read protected-data behavior, and any new export route/helper. The repository records pre-existing `Hexalith.EventStore.Server.Tests` build issues from CA2007 warnings treated as errors; record any unchanged blocker instead of marking it as DW18-caused.

Aspire manual validation, when available:

```powershell
$env:EnableKeycloak = 'false'
aspire run --project .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
```

Use the dev JWT shape from `AGENTS.md`: issuer `hexalith-dev`, audience `hexalith-eventstore`, `tenants` JSON array, and permissions/roles sufficient for Admin backup/export actions.

### Latest Technical Information

- Dapr state API docs still describe bulk state and ETag concurrency as state-store capabilities, but DW18 should not use raw state reads for export because Story 22.6 already provides the authorized stream-read boundary. Reference: https://docs.dapr.io/reference/api/state_api/
- Dapr .NET client docs confirm service invocation and `GetBulkStateAsync` remain available building-block APIs. Use service invocation to EventStore; use bulk state only inside EventStore-owned code if a focused implementation decision proves it is required. Reference: https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-client/
- Dapr service invocation docs were updated for Dapr 1.17.7 in May 2026, matching this repo's Dapr runtime family. Reference: https://docs.dapr.io/developing-applications/building-blocks/service-invocation/

## References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-admin-deferred-quick-wins.md#4.3-DW18--Stream-export-backend`] - approved DW18 scope, max event limit, JSON/CloudEvents, redaction, UI cleanup, and out-of-scope boundaries.
- [Source: `_bmad-output/implementation-artifacts/post-epic-deferred-dw14-admin-deferred-operations-ux-policy.md`] - current deferred UX baseline and no-fake-success rules.
- [Source: `_bmad-output/implementation-artifacts/16-4-backup-and-restore-console.md`] - original stream export UI/API contract and the documented 50,000-event limit.
- [Source: `_bmad-output/implementation-artifacts/22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints.md`] - stream-read contract, paging, authorization-before-state, client/testing fakes, and failure taxonomy.
- [Source: `_bmad-output/implementation-artifacts/22-7d-4-protected-data-redaction-in-replay-rebuild-backup-validation-and-tests.md`] - protected-data redaction/test-artifact guardrails for replay/rebuild/backup surfaces.
- [Source: `_bmad-output/planning-artifacts/prd.md#FR76`] - admin storage management includes backup operations.
- [Source: `_bmad-output/planning-artifacts/prd.md#FR99-FR104`] - stream read APIs and protected-data behavior requirements.
- [Source: `_bmad-output/planning-artifacts/architecture.md#ADR-P4-Admin-Tooling--Three-Interface-Architecture-Over-Single-DAPR-API`] - Admin UI/API/CLI/MCP boundary over one Admin API.
- [Source: `_bmad-output/project-context.md`] - .NET 10, DAPR, Aspire, testing, logging, and no-payload-leak guardrails.
- [Source: `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs`] - current deferred export implementation and DAPR invocation helper.
- [Source: `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs`] - current external Admin.Server export route and request-body tenant authorization.
- [Source: `src/Hexalith.EventStore.Contracts/Streams/*`] - stream read request/page/event metadata and stable reason codes.
- [Source: `src/Hexalith.EventStore/Controllers/StreamsController.cs`] - current stream-read endpoint, page-size cap, auth-before-actor checks, protected-data fail-closed behavior, and continuation-token limitation.
- [Source: `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor`] - current export deferred UI and download chokepoint.
- [Source: `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/BackupExportStreamCommand.cs`] - CLI export command and output behavior.
- [Source: Dapr official docs - state API reference](https://docs.dapr.io/reference/api/state_api/) - state API bulk/ETag reference.
- [Source: Dapr official docs - .NET client](https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-client/) - service invocation/client building blocks.
- [Source: Dapr official docs - service invocation](https://docs.dapr.io/developing-applications/building-blocks/service-invocation/) - Dapr service invocation behavior and current 1.17.7 docs.

## Dev Agent Record

### Agent Model Used

TBD by dev agent.

### Debug Log References

### Completion Notes List

- Story context engine analysis completed on 2026-05-21. Comprehensive developer guide created for stream export backend implementation.
- Party Mode review fixes applied on 2026-05-21: response contract locked to bounded `StreamExportResult.Content`, oversized-stream behavior pinned to newest 50,000 events with explicit truncation metadata, auth-before-read/no-raw-DAPR constraints promoted, JSON/CloudEvents examples made testable, and AC/test guardrail IDs added.
- Advanced elicitation refinements applied on 2026-05-21: clarified `LatestSequence` discovery, Admin.Server versus EventStore authorization ownership, empty/missing stream failure shape, result/document event-count consistency, and success-audit timing.

### File List

### Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- No product code or tests were changed during story creation.
- No Aspire runtime validation was run during story creation; this turn only creates the ready-for-dev implementation story.

### Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-21 | 1.2 | Applied advanced elicitation refinements for `LatestSequence` discovery, authorization boundary wording, empty/missing stream mapping, event-count consistency, and success-audit timing. | Codex |
| 2026-05-21 | 1.1 | Applied Party Mode review fixes: locked bounded compatibility response contract, specified newest-window truncation behavior, made auth-before-read and no raw DAPR access explicit, added JSON/CloudEvents golden shapes, and mapped tests to DW18 guardrail IDs. | Codex |
| 2026-05-21 | 1.0 | Created ready-for-dev DW18 story with real stream export backend scope, stream-read reuse, 50,000-event bound, JSON/CloudEvents format guidance, protected-data fail-closed requirements, UI/CLI cleanup, tests, and evidence guidance. | Codex |
