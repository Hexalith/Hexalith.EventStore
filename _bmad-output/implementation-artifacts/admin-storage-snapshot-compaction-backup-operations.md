# Story: admin-storage-snapshot-compaction-backup-operations

Status: review

Context created: 2026-05-09
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-suite-issues.md`
Triggering evidence: `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
Scope: Issue #15 only.

## Story

As an EventStore DBA or infrastructure admin using the Admin UI,
I want snapshot policy, manual snapshot, compaction, backup, restore, export, and import actions to reach real EventStore-owned upstream operations or be explicitly disabled/deferred,
so that `/snapshots`, `/compaction`, and `/backups` no longer forward to missing endpoints and report "Admin service unavailable" for shipped-but-unimplemented DBA operations.

## Issue Traceability

| Issue | Failing symptom | Covered by | Required evidence |
| --- | --- | --- | --- |
| #15 | Admin.Server forwards snapshot, compaction, and backup actions to missing EventStore endpoints. Manual traces show "Admin service unavailable" for Add Policy, Trigger Compaction, Create Backup, and Export Stream 404s. | AC1-AC9 | EventStore upstream route tests, Admin.Server proxy tests, UI/manual evidence that actions no longer forward to missing endpoints. |

## Product And Architecture Decision Gate

Issue #15 was explicitly routed to Architect/Product Owner before developer implementation. This story is ready for development only because the first development task is a bounded decision record. The dev agent must not silently implement destructive storage behavior.

The decision record must classify each operation as one of:

- `implemented-now`: implement the upstream EventStore endpoint and backing job or index behavior in this story.
- `honest-defer`: leave the UI action disabled or return a typed not-implemented result with clear copy and manual-guide alignment.
- `blocked`: stop the story and set status to blocked if neither product nor architecture can approve an honest behavior.

The decision table must include one row per operation and the columns:
operation, UI action, client method, Admin controller route, Admin.Server
service method, current DAPR target, required EventStore-owned upstream route,
upstream owner, classification, sync-or-queued behavior, required role,
destructive risk, expected result code, UI outcome, test obligation, and
rationale. The table is a blocking pre-edit artifact, not a retrospective
note.

The default bias is `honest-defer` for backup/restore and physical compaction if the required engine semantics are still undefined. Returning a fake success is not allowed.
If no EventStore-owned upstream write contract exists for an operation, the
operation must be classified as `honest-defer` or `blocked`; Admin.Server must
not infer behavior locally, and the UI must not call a missing upstream route.

## Operation Truth Contract

- Admin.Server is a shared Admin API and proxy. It must not become the storage, compaction, backup, or restore engine by scraping UI reads or bypassing EventStore ownership.
- EventStore owns write-side upstream operations invoked by Admin.Server through DAPR service invocation.
- Event keys remain write-once. Do not delete, rewrite, compact, or restore original event keys unless a specific architecture decision approves the semantics.
- Snapshot policy and job indexes are operational read models. They may be stored under `admin:storage-snapshot-policies:*`, `admin:storage-compaction-jobs:*`, and `admin:backup-jobs:*`, but they must not be confused with the event stream source of truth.
- Missing upstream implementation, backend limitations, and unavailable state-store access must be visible as explicit status or copy. They must not appear as healthy empty data, successful queued work, or generic "Admin service unavailable".
- Backup and restore are infrastructure-admin operations. They require Admin role. Snapshot and compaction write actions require Operator role.
- All operation responses must preserve `AdminOperationResult.OperationId`, `Message`, and `ErrorCode` through EventStore, Admin.Server, API clients, and UI to support manual evidence and troubleshooting.
- Accepted result vocabulary for this story is `Implemented`, `Queued`,
  `Deferred`, `Blocked`, `RejectedValidation`, `RejectedUnauthorized`,
  `NotFound`, `UpstreamUnavailable`, `UnsupportedBackend`, and `UnexpectedError`.
  Existing DTO fields may carry these values if they are already compatible;
  do not add a second result envelope unless the existing contract cannot
  express the distinction.
- Long-running operations must return a queued/job-tracked outcome unless the
  upstream work is proven bounded. A synchronous success may only mean the
  requested state was written or the operation was safely queued.
- Admin.Server may authorize, proxy, parse typed upstream results, and preserve
  timeout/unavailable distinctions. It must not inspect local storage, scan
  Redis keys, manipulate backup files, or perform compaction/restore/import
  logic itself.

## Acceptance Criteria

1. **Decision record is captured before code changes.**
   - Given Issue #15 is routed to Architect/Product Owner
   - When implementation starts
   - Then the Dev Agent Record includes one decision table covering snapshot policy set/delete, manual snapshot, compaction, backup trigger, backup validate, restore, export-stream, and import-stream.
   - And each row is classified as `implemented-now`, `honest-defer`, or `blocked`.
   - And each row identifies the UI action, client method, Admin controller route, Admin.Server service method, current DAPR target, required EventStore-owned upstream route, upstream owner, sync-or-queued behavior, required role, destructive risk, expected result code, UI outcome, test obligation, and rationale.
   - And any `honest-defer` row names the exact user-visible behavior and manual-guide update required.
   - And any `blocked` row names the missing product or architecture decision required before implementation.

2. **EventStore upstream route inventory is complete and tested.**
   - Given Admin.Server currently invokes EventStore app-id endpoints
   - When the story completes
   - Then every route in `DaprStorageCommandService` and `DaprBackupCommandService` has a corresponding tested EventStore upstream route or an intentionally disabled UI/API path.
   - Required upstream route family:
     - `PUT api/v1/admin/storage/snapshot-policy`
     - `DELETE api/v1/admin/storage/snapshot-policy`
     - `POST api/v1/admin/storage/snapshot`
     - `POST api/v1/admin/storage/compact`
     - `POST api/v1/admin/backups/{tenantId}`
     - `POST api/v1/admin/backups/{backupId}/validate`
     - `POST api/v1/admin/backups/{backupId}/restore`
     - `POST api/v1/admin/backups/export-stream`
     - `POST api/v1/admin/backups/import-stream`
   - And no shipped Admin UI action forwards to a route that 404s because the upstream endpoint is absent.
   - And the route inventory includes success, invalid input, not found, not implemented/deferred, timeout, upstream unavailable, forbidden, and tenant-mismatch expectations for each route where the case applies.

3. **Snapshot policy operations are implemented or honestly deferred.**
   - Given `/snapshots` can set and delete policies through Admin.Server
   - When the approved decision is `implemented-now`
   - Then EventStore handles set/delete policy requests, validates tenant/domain/aggregate type/interval input, writes `admin:storage-snapshot-policies:all` plus tenant-scoped keys, and returns deterministic `AdminOperationResult` values.
   - And delete of a missing policy returns a not-found result, not a success.
   - And policy writes are idempotent and portable across DAPR state stores.
   - And the decision table states whether policies are persisted runtime configuration, read-only placeholders, or admin-only UI state; accidental creation of an unapproved configuration subsystem is not allowed.
   - If the approved decision is `honest-defer`, the UI and manual guide must make policy mutation unavailable with clear copy and no upstream call.

4. **Manual snapshot creation is implemented or honestly deferred.**
   - Given `/snapshots` can request a manual snapshot for a tenant/domain/aggregate
   - When the approved decision is `implemented-now`
   - Then EventStore creates or queues a snapshot using the existing aggregate identity, metadata, and snapshot key conventions.
   - And the operation never fabricates snapshot success without writing or queueing a verifiable snapshot.
   - And success evidence includes the snapshot key or operation id, tenant, domain, aggregate id, and sequence source.
   - And queued snapshot requests write a job/status record that can be correlated to the returned operation id.
   - If no snapshot production path is safe in this story, the action must be disabled or return a typed `NOT_IMPLEMENTED` result that the UI renders as an honest limitation.

5. **Compaction is never destructive by accident.**
   - Given architecture currently marks event stream compaction/archival as deferred v2/v3 and event keys are write-once
   - When `/compaction` triggers compaction
   - Then the story either implements an approved non-destructive job semantics or honestly defers the action.
   - And any implemented job writes `admin:storage-compaction-jobs:all` plus tenant-scoped keys with `Pending`, `Running`, `Completed`, or `Failed` status.
   - And it records what was compacted, what was not compacted, and why.
   - And it must not delete, rewrite, or archive event keys unless the decision record explicitly approves that behavior.
   - And the decision table states whether compaction is request-only, dry-run/status-only, logical metadata, or actual execution. Physical storage reclamation is `blocked` unless the architecture decision explicitly defines portable semantics.

6. **Backup and restore operations do not pretend to be complete.**
   - Given `/backups` exposes backup trigger, validate, restore, export-stream, and import-stream
   - When full backup/restore engine semantics are not approved in this story
   - Then those actions are disabled or return typed `NOT_IMPLEMENTED`/`DEFERRED` results with clear UI copy and no "Admin service unavailable" masking.
   - When any backup operation is approved as `implemented-now`
   - Then it writes or updates `admin:backup-jobs:all` plus tenant-scoped keys and records job type, status, tenant, operation id, timestamps, and errors.
   - Restore must never overwrite original streams. Any approved restore writes to explicitly named parallel restore streams or performs dry-run validation only.
   - Restore and import-stream default to `blocked` or `honest-defer` unless ST0 records an approved recovery/import model, idempotency rule, tenant isolation rule, audit expectation, and safe target namespace.
   - Export-stream may be implemented as a bounded read-only operation only if it uses the aggregate metadata and per-event key conventions, enforces an event limit, and validates tenant access.
   - Import-stream must validate payload structure before writing and must not bypass command/event validation rules.

7. **Admin.Server and UI preserve typed operation outcomes.**
   - Given EventStore returns a structured `AdminOperationResult`
   - When Admin.Server forwards the result
   - Then non-success outcomes such as validation failure, upstream missing route, unsupported operation, authorization failure, tenant mismatch, not found, timeout, service unavailable, and unexpected exception remain distinguishable in API responses, API clients, toast/error copy, and tests.
   - And `DaprStorageCommandService` and `DaprBackupCommandService` do not collapse expected upstream non-success results into generic `HttpRequestException` messages when the upstream response body contains a usable `AdminOperationResult`.
   - And the UI does not keep buttons busy indefinitely after an expected not-implemented or rejected operation result.
   - And UI copy distinguishes `Deferred`, `Blocked`, `RejectedValidation`, `RejectedUnauthorized`, `UpstreamUnavailable`, and `UnsupportedBackend`; no fallback copy may hide the reason as generic service unavailability.

8. **Tests pin route existence, authorization, state indexes, and honest-defer paths.**
   - Server tests cover each EventStore upstream route selected as `implemented-now`.
   - Admin.Server tests cover proxy behavior for success, typed failure, 404/not-found, not-implemented/deferred, timeout, upstream unavailable, authorization failure, and tenant authorization or tenant mismatch.
   - Admin.UI tests cover `/snapshots`, `/compaction`, and `/backups` rendering of implemented and honestly deferred actions, including busy-state cleanup after success, validation errors, not implemented/deferred, timeout, unavailable, and authorization failure.
   - Contract/model tests cover any new or changed DTO fields required for job/index evidence.
   - State-index tests prove successful implemented operations update only the expected snapshot, compaction, or backup indexes, and prove failed/deferred/disabled operations do not mutate those indexes.
   - Route-contract tests come before broad integration assumptions; timeout and unavailable behavior may be unit-tested with deterministic fakes and supplemented by Aspire evidence when practical.

9. **Manual Aspire evidence is captured before review.**
   - With `EnableKeycloak=false` Aspire dev mode, Redis flushed, and the canonical sample flow seeded, record the behavior of `/snapshots`, `/compaction`, and `/backups`.
   - Capture endpoint payloads or screenshots for every Issue #15 operation that is implemented, disabled, blocked, or honestly deferred.
   - Capture at least one authorized and one unauthorized or tenant-rejected path for the operation families where role/tenant behavior applies.
   - Capture request/response bodies, relevant EventStore and Admin.Server logs, UI screenshots, and typed result examples that map each artifact back to AC1-AC8.
   - Capture state-store evidence for `admin:storage-snapshot-policies:*`, `admin:storage-compaction-jobs:*`, and `admin:backup-jobs:*` where applicable.
   - Save sanitized evidence under `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/`.

## Tasks / Subtasks

- [x] **ST0 - Record the Product/Architecture decision table.** (AC: 1)
  - [x] Re-read Issue #15 in the manual-test issue file and Workstream E in the sprint-change proposal.
  - [x] Inventory current Admin.Server proxy calls in `DaprStorageCommandService` and `DaprBackupCommandService`.
  - [x] Confirm that no matching EventStore upstream routes exist today by searching the EventStore server projects.
  - [x] Record the blocking route and operation inventory table before production code edits. Include UI action, client method, Admin controller route, Admin.Server service method, current DAPR target, required EventStore upstream route, upstream owner, classification, sync-or-queued behavior, required role, destructive risk, expected result code, UI outcome, test obligation, and rationale.
  - [x] Classify each operation as `implemented-now`, `honest-defer`, or `blocked` in this story's Dev Agent Record before production code edits.
  - [x] Save the completed decision table under `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/` or copy it into the Dev Agent Record before any route implementation commit.

- [x] **ST1 - Add or disable upstream storage routes.** (AC: 2, 3, 4, 5, 7)
  - [x] Implement approved EventStore upstream routes for snapshot policy set/delete, manual snapshot, and compaction.
  - [x] For deferred operations, prevent the UI from invoking missing upstream routes and surface the limitation clearly.
  - [x] Preserve route/body compatibility with the existing Admin.Server command services unless ST2 intentionally changes both sides.
  - [x] Return structured `AdminOperationResult` values for accepted, rejected, not-found, not-implemented, and failed outcomes.
  - [x] For manual snapshot and compaction, record whether the approved behavior is synchronous bounded work, queued job, request-only, dry-run/status-only, logical metadata, or deferred.

- [x] **ST2 - Preserve typed results through Admin.Server.** (AC: 2, 7, 8)
  - [x] Update `DaprStorageCommandService` and `DaprBackupCommandService` only as needed to read structured result bodies before throwing away upstream non-success detail.
  - [x] Keep DAPR service invocation timeouts bounded by existing `AdminServerOptions.ServiceInvocationTimeoutSeconds`.
  - [x] Add tests that prove 404 from a truly missing route is no longer the normal path for shipped UI actions.
  - [x] Add result-mapping examples for success, validation failure, unsupported/deferred operation, missing upstream route, authorization failure, tenant mismatch, timeout, upstream unavailable, and unexpected exception.

- [x] **ST3 - Implement or honestly defer backup route family.** (AC: 2, 6, 7, 8)
  - [x] Add approved EventStore routes for backup trigger, validation, restore, export-stream, and import-stream.
  - [x] For full backup/restore operations that remain out of scope, return typed `NOT_IMPLEMENTED`/`DEFERRED` results or disable UI controls.
  - [x] Treat restore and import-stream as `blocked` or `honest-defer` unless the decision table records a safe target namespace, idempotency rule, tenant isolation rule, and audit expectation.
  - [x] If export-stream is implemented, bound reads by aggregate metadata and event keys; do not use backend-specific key scans.
  - [x] If import-stream is implemented, validate payload structure and authorization before any writes.

- [x] **ST4 - Populate operation indexes where behavior is implemented.** (AC: 3, 5, 6, 8)
  - [x] Write snapshot policy, compaction job, and backup job indexes under the existing key names.
  - [x] Maintain both `all` and tenant-scoped index views when the UI/query services require both.
  - [x] Use deterministic ordering and bounded index sizes.
  - [x] Do not report fake size, compaction, or backup metrics when the backend cannot provide them.

- [x] **ST5 - Update UI/manual-test copy for honest behavior.** (AC: 6, 7, 9)
  - [x] Align `/snapshots`, `/compaction`, and `/backups` button states, dialogs, and toasts with implemented/deferred decisions.
  - [x] Ensure rejected/deferred operation results clear busy state and show actionable copy.
  - [x] Add UI/client checks proving busy state clears for success, validation error, not implemented/deferred, timeout, unavailable, and authorization failure.
  - [x] Update the manual-test guide or evidence notes only for Issue #15 expectations changed by this story.

- [x] **ST6 - Validate and record evidence.** (AC: 8, 9)
  - [x] Run impacted unit test projects individually per repository guidance.
  - [x] Run canonical Aspire dev-mode manual evidence when environment allows.
  - [x] Save sanitized payload/screenshot/key evidence under `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/`, including request/response bodies, UI screenshots, EventStore/Admin.Server log excerpts, typed result examples, and an AC mapping README.
  - [x] Update this story's Dev Agent Record, File List, Verification Status, and Change Log.

## Developer Notes

Current code intelligence from story creation:

- `DaprStorageCommandService` forwards storage writes to EventStore app-id using `api/v1/admin/storage/compact`, `api/v1/admin/storage/snapshot`, `PUT api/v1/admin/storage/snapshot-policy`, and `DELETE api/v1/admin/storage/snapshot-policy`.
- `DaprBackupCommandService` forwards backup writes to EventStore app-id using `api/v1/admin/backups/{tenantId}`, `{backupId}/validate`, `{backupId}/restore`, `api/v1/admin/backups/export-stream`, and `api/v1/admin/backups/import-stream`.
- No matching upstream routes were found under `src/Hexalith.EventStore`, `src/Hexalith.EventStore.Server`, or `src/Hexalith.EventStore.Client` during story creation.
- `DaprStorageQueryService` reads `admin:storage-snapshot-policies:{scope}` and `admin:storage-compaction-jobs:{scope}` from the state store.
- `DaprBackupQueryService` reads `admin:backup-jobs:{scope}` from the state store.
- `AdminStorageController` already exposes external Admin API routes for storage overview, hot streams, snapshot policies, compaction jobs, compaction trigger, manual snapshot, set snapshot policy, and delete snapshot policy.
- `AdminBackupsController` already exposes external Admin API routes for backup jobs, trigger backup, validate backup, restore, export-stream, and import-stream.
- `AdminSnapshotApiClient` and `AdminBackupApiClient` already call Admin.Server routes. The broken part is the upstream EventStore operation boundary and honest result propagation, not page discovery.
- Prior story `admin-operational-index-populators` owns storage overview/hot-stream/stream-count population. Do not absorb its index population scope here except where snapshot policies, compaction jobs, or backup jobs are directly caused by Issue #15 operations.

Architecture and product guardrails:

- ADR-P4 keeps Admin.Server as the shared Admin API. It reads through DAPR state store and delegates writes to EventStore through DAPR service invocation.
- PRD FR76 requires storage management, compaction, snapshot creation, and backup operations. FR79 requires the shared Admin API surface to back Web UI, CLI, and MCP.
- NFR40 requires admin reads under 500ms p99 and admin writes under 2s p99 for trigger-style operations. Long work must be queued/job-tracked rather than performed inline.
- NFR44 requires DAPR backend portability. Do not implement production behavior by scanning Redis keys or relying on backend-specific keyspace operations.
- NFR46 requires Operator for snapshot/compaction write operations and Admin for backup/restore operations.
- Architecture enforcement rule 11 says event store keys are write-once. Restore and compaction must not mutate original event keys unless a new approved architecture decision changes that rule.
- Architecture currently lists event stream compaction/archival as v2/v3 deferred. Treat physical compaction as a decision-sensitive operation, not an automatic implementation detail.
- 2026-05-10 party-mode review tightened ST0 into a blocking route/operation inventory. Dev should not start route implementation until the per-operation classification, result code, owner, role, destructive-risk, and test-obligation columns are complete.
- Backup restore/import/export and physical compaction are the highest-risk rows. Default them to `honest-defer` or `blocked` unless an approved EventStore-owned recovery, import/export, or compaction model already exists and can be tested in this story.

## Files Likely Touched

- `src/Hexalith.EventStore.Server/**` for EventStore-owned upstream admin routes and handlers.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Common/AdminOperationResult.cs` only if the existing result shape cannot carry required typed outcomes.
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/*` only for needed job/index fields.
- `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminSnapshotApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminBackupApiClient.cs`
- `tests/Hexalith.EventStore.Server.Tests/**`
- `tests/Hexalith.EventStore.Admin.Server.Tests/**`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/SnapshotsPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CompactionPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/BackupsPageTests.cs`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md` only if Issue #15 manual expectations change.

## Out of Scope

- Issues #6, #7, and #8 health/DAPR truthfulness.
- Issues #9 and #13 operator action dialog and dev role switching.
- Issue #10 actor diagnostics.
- Issues #11, #12, #14, and the initial Issue #17 retest owned by `admin-operational-index-populators`.
- Issues #16 and #18 consistency subtitle and tenant delete clarity.
- Storage overview, hot streams, and stream-count population unrelated to snapshot/compaction/backup jobs.
- Production Redis keyspace scanning.
- Full destructive compaction, event archival, cross-tenant restore, external backup storage targets, encryption, compression, and recurring backup/compaction scheduling unless specifically approved in ST0.
- DAPR component YAML, access-control policy, AppHost topology, or auth model changes unless a narrow route invocation defect is proven.

## References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-suite-issues.md`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
- `_bmad-output/implementation-artifacts/admin-operational-index-populators.md`
- `_bmad-output/implementation-artifacts/16-2-snapshot-management-and-auto-snapshot-policies.md`
- `_bmad-output/implementation-artifacts/16-3-compaction-manager.md`
- `_bmad-output/implementation-artifacts/16-4-backup-and-restore-console.md`
- `_bmad-output/planning-artifacts/prd.md#Administration Tooling`
- `_bmad-output/planning-artifacts/prd.md#Administration Tooling (NFR40-NFR46)`
- `_bmad-output/planning-artifacts/architecture.md#ADR-P4 Admin Tooling - Three-Interface Architecture Over Single DAPR API`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminSnapshotApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminBackupApiClient.cs`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-10T12:08:24+02:00 - Dev-start baseline: `aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json` succeeded, stopped the previous instance, and returned dashboard URL `https://localhost:17017/login?t=ca20e8bbaa96a8867896d8f8bd375dcf`.
- 2026-05-10T12:08:54+02:00 - Aspire MCP selected `src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj`; structured logs showed `sample` started on `http://localhost:56713` / `https://localhost:56712`.
- 2026-05-10T12:09:00+02:00 - ST0 inventory confirmed `DaprStorageCommandService` and `DaprBackupCommandService` forward all Issue #15 write operations to EventStore app-id routes that do not exist under the EventStore server projects.
- 2026-05-10T13:15:28+02:00 - Baseline apphost restarted with `aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json`; build completed with 0 warnings and 0 errors, dashboard URL `https://localhost:17017/login?t=75a39ee2acbff8a4c2adb04ee9520efa`.
- 2026-05-10T13:22:06+02:00 - EnableKeycloak=false apphost restart completed and returned dashboard URL `https://localhost:17017/login?t=82ab2cc4cacc0bc99f863ef765ce2cb9`; build completed with 0 errors and 3 transient MSB3026 copy-retry warnings while the prior Admin.Server host released its DLL.
- 2026-05-10T13:24:15+02:00 - Redis `FLUSHDB` completed for manual evidence. Live API evidence captured typed deferred responses for snapshot policy set/delete, manual snapshot, compaction, backup trigger, backup validate, restore, export-stream, and import-stream. Unauthorized no-token compaction returned 401; tenant mismatch returned 403; snapshot, compaction, and backup state-index probes returned 204/no mutation after deferred calls.
- 2026-05-10T13:29:30+02:00 - Aspire MCP resource check showed the EnableKeycloak=false topology running healthy with Admin.Server on `http://localhost:8090`, Admin.UI on `http://localhost:8092`, EventStore on `http://localhost:8080`, and EventStore DAPR HTTP on `http://localhost:3501`.
- 2026-05-10T13:31:00+02:00 - Aspire MCP structured logs showed Admin.Server configured `EventStoreDaprHttpEndpoint=http://localhost:3501`, DAPR metadata probes returning 200, and tenant mismatch logged as `Tenant access denied: requested=tenant-b, authorized=[tenant-a]`; EventStore logs showed no Issue #15 upstream route invocation.
- 2026-05-10T13:35:00+02:00 - UI screenshots captured for `/snapshots`, `/compaction`, and `/backups` under `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/`.
- 2026-05-10T13:40:00+02:00 - Validation passed: Admin.Server.Tests 586 passed / 18 skipped; Admin.UI.Tests 756 passed; Client.Tests 362 passed; Contracts.Tests 291 passed; Sample.Tests 74 passed; Testing.Tests 78 passed; Admin.Abstractions.Tests 404 passed. Server.Tests was not run because repository guidance records a pre-existing CA2007 warnings-as-errors build failure in that project.

### Implementation Plan

- ST0 classification: all Issue #15 write operations are `honest-defer` because no approved EventStore-owned runtime snapshot policy engine, manual snapshot job model, portable compaction model, backup manifest/engine, restore namespace, export format/limit contract, or import/idempotency/audit model exists in the current codebase.
- Admin.Server command services will return structured `AdminOperationResult` values with `ErrorCode = "Deferred"` without invoking missing EventStore upstream routes.
- Export stream will return a failed `StreamExportResult` with explicit deferred copy because the current export DTO has no `ErrorCode` field.
- UI/API client paths will preserve the typed operation outcome and clear busy state instead of collapsing the result into generic service unavailable copy.
- The manual/evidence artifact will record that these operations are intentionally deferred in this story rather than implemented as storage-engine behavior.

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- No `project-context.md` file was present in the repository at story creation.
- 2026-05-10 party-mode review applied story clarifications for the blocking route/operation decision table, conservative destructive-operation defaults, typed result vocabulary, route/auth/state-index test matrix, busy-state cleanup coverage, and manual evidence checklist.
- ST0 decision record captured before production code edits at `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/operation-decision-record.md`.
- ST0 classification honored `honest-defer` for every Issue #15 write operation; no fake upstream storage, compaction, snapshot, backup, restore, export, or import engine was introduced.
- Admin.Server command services now return structured deferred outcomes directly and do not invoke the missing EventStore upstream routes for shipped UI actions. Export-stream returns an explicit failed `StreamExportResult` because that DTO has no `ErrorCode` field.
- Admin.Server controllers preserve typed business outcomes across the accepted vocabulary, including `Deferred`, `Blocked`, `RejectedValidation`, `RejectedUnauthorized`, `NotFound`, `UpstreamUnavailable`, `UnsupportedBackend`, and `UnexpectedError`.
- Admin.UI clients and pages preserve deferred messages and clear busy state for `/snapshots`, `/compaction`, and `/backups`; focused client/page tests pin that behavior.
- Manual Aspire evidence, API payloads, state-store non-mutation probes, log excerpts, and UI screenshots were saved under `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/`.

### File List

- `_bmad-output/implementation-artifacts/admin-storage-snapshot-compaction-backup-operations.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/README.md`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/aspire-log-excerpts.md`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/backup-restore-deferred.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/backup-trigger-deferred.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/backup-validate-deferred.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/compaction-deferred.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/export-stream-deferred.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/import-stream-deferred.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/live-api-evidence-summary.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/manual-snapshot-deferred.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/operation-decision-record.md`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/redis-flush.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/snapshot-policy-delete-deferred.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/snapshot-policy-set-deferred.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/state-admin-backup-jobs-all.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/state-admin-backup-jobs-tenant-a.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/state-admin-storage-compaction-jobs-all.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/state-admin-storage-compaction-jobs-tenant-a.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/state-admin-storage-snapshot-policies-all.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/state-admin-storage-snapshot-policies-tenant-a.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/tenant-mismatch-compaction.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/ui-backups-page-dom.txt`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/ui-backups-page.png`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/ui-compaction-page-dom.txt`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/ui-compaction-page.png`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/ui-screenshot-summary.json`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/ui-snapshots-page-dom.txt`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/ui-snapshots-page.png`
- `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/unauthorized-compaction-no-token.json`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageCommandService.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminBackupsControllerTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStorageControllerTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprBackupCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStorageCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStorageServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/BackupsPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CompactionPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/SnapshotsPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminBackupApiClientTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminCompactionApiClientTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminSnapshotApiClientTests.cs`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, or submodules.
- Party-mode review completed on 2026-05-10 and is recorded below.
- Advanced elicitation has NOT yet been run for this story.
- Implementation completed and story moved to `review` on 2026-05-10.
- Focused Admin.Server, Admin.UI, Admin.Abstractions, Client, Contracts, Sample, and Testing test projects passed individually.
- Canonical EnableKeycloak=false Aspire evidence captured typed deferred outcomes, authorization and tenant rejection paths, state-index non-mutation probes, relevant logs, and UI screenshots.
- `Hexalith.EventStore.Server.Tests` was not run because repository guidance identifies a pre-existing CA2007 warnings-as-errors build failure for that project.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-10 | 1.0 | Implemented honest-defer Issue #15 behavior, typed Admin.Server result preservation, UI busy-state/deferred-result coverage, live Aspire evidence, and moved story to review. | Codex |
| 2026-05-10 | 0.2 | Applied party-mode review hardening for operation ownership, destructive-operation safety defaults, typed result mapping, route/auth/state-index tests, and manual evidence requirements. | Codex automation |
| 2026-05-09 | 0.1 | Created ready-for-dev story for Issue #15 upstream snapshot, compaction, and backup operation behavior. | Codex automation |

## Party-Mode Review

- Date/time: 2026-05-10T10:49:13+02:00
- Selected story key: `admin-storage-snapshot-compaction-backup-operations`
- Command/skill invocation used:
  `/bmad-party-mode admin-storage-snapshot-compaction-backup-operations; review;`
- Participating BMAD agents: John (Product Manager), Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary:
  - The story was directionally correct, but the decision gate was too loose; every operation needed explicit owner, route, role, result, UX, side-effect, and test expectations before implementation.
  - Restore, import-stream, export-stream, backup job behavior, and physical compaction carried high destructive or long-running-operation risk without a pinned EventStore-owned contract.
  - Typed result semantics and UI copy needed a shared vocabulary so deferred, blocked, validation, authorization, tenant mismatch, unavailable, and unsupported-backend outcomes cannot collapse into generic service-unavailable copy.
  - Tests and manual evidence needed route-by-route auth, tenant, state-index, busy-state, timeout, unavailable, and honest-defer obligations.
- Changes applied:
  - Expanded the Product And Architecture Decision Gate with the required blocking decision table columns and a conservative no-upstream-contract default.
  - Added accepted result vocabulary and long-running queued/job-tracked behavior to the Operation Truth Contract.
  - Tightened AC1-AC9 with route inventory expectations, snapshot policy boundary clarity, queued snapshot evidence, compaction mode classification, restore/import safety defaults, typed UI copy, route/auth/state-index assertions, busy-state cleanup, and manual evidence checklist.
  - Updated ST0-ST6 and Developer Notes with blocking inventory, result-mapping examples, high-risk operation defaults, UI/client checks, and evidence artifact requirements.
  - Updated Verification Status and Change Log with this dated party-mode review.
- Findings deferred:
  - Product/Architecture must still decide which operations are `implemented-now`, `honest-defer`, or `blocked` when ST0 is executed.
  - Restore/import safety model, physical compaction semantics, export-stream format/idempotency, and canonical admin operation audit ownership remain human product/architecture decisions unless an approved contract already exists.
  - Exact implementation shape for queued jobs, polling, cancellation, retry, and audit trail remains a dev/architecture decision within the story guardrails.
- Final recommendation: ready-for-dev after applied story updates.
