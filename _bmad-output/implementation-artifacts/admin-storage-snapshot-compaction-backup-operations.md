# Story: admin-storage-snapshot-compaction-backup-operations

Status: ready-for-dev

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

The default bias is `honest-defer` for backup/restore and physical compaction if the required engine semantics are still undefined. Returning a fake success is not allowed.

## Operation Truth Contract

- Admin.Server is a shared Admin API and proxy. It must not become the storage, compaction, backup, or restore engine by scraping UI reads or bypassing EventStore ownership.
- EventStore owns write-side upstream operations invoked by Admin.Server through DAPR service invocation.
- Event keys remain write-once. Do not delete, rewrite, compact, or restore original event keys unless a specific architecture decision approves the semantics.
- Snapshot policy and job indexes are operational read models. They may be stored under `admin:storage-snapshot-policies:*`, `admin:storage-compaction-jobs:*`, and `admin:backup-jobs:*`, but they must not be confused with the event stream source of truth.
- Missing upstream implementation, backend limitations, and unavailable state-store access must be visible as explicit status or copy. They must not appear as healthy empty data, successful queued work, or generic "Admin service unavailable".
- Backup and restore are infrastructure-admin operations. They require Admin role. Snapshot and compaction write actions require Operator role.
- All operation responses must preserve `AdminOperationResult.OperationId`, `Message`, and `ErrorCode` through EventStore, Admin.Server, API clients, and UI to support manual evidence and troubleshooting.

## Acceptance Criteria

1. **Decision record is captured before code changes.**
   - Given Issue #15 is routed to Architect/Product Owner
   - When implementation starts
   - Then the Dev Agent Record includes one decision table covering snapshot policy set/delete, manual snapshot, compaction, backup trigger, backup validate, restore, export-stream, and import-stream.
   - And each row is classified as `implemented-now`, `honest-defer`, or `blocked`.
   - And any `honest-defer` row names the exact user-visible behavior and manual-guide update required.

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

3. **Snapshot policy operations are implemented or honestly deferred.**
   - Given `/snapshots` can set and delete policies through Admin.Server
   - When the approved decision is `implemented-now`
   - Then EventStore handles set/delete policy requests, validates tenant/domain/aggregate type/interval input, writes `admin:storage-snapshot-policies:all` plus tenant-scoped keys, and returns deterministic `AdminOperationResult` values.
   - And delete of a missing policy returns a not-found result, not a success.
   - And policy writes are idempotent and portable across DAPR state stores.
   - If the approved decision is `honest-defer`, the UI and manual guide must make policy mutation unavailable with clear copy and no upstream call.

4. **Manual snapshot creation is implemented or honestly deferred.**
   - Given `/snapshots` can request a manual snapshot for a tenant/domain/aggregate
   - When the approved decision is `implemented-now`
   - Then EventStore creates or queues a snapshot using the existing aggregate identity, metadata, and snapshot key conventions.
   - And the operation never fabricates snapshot success without writing or queueing a verifiable snapshot.
   - And success evidence includes the snapshot key or operation id, tenant, domain, aggregate id, and sequence source.
   - If no snapshot production path is safe in this story, the action must be disabled or return a typed `NOT_IMPLEMENTED` result that the UI renders as an honest limitation.

5. **Compaction is never destructive by accident.**
   - Given architecture currently marks event stream compaction/archival as deferred v2/v3 and event keys are write-once
   - When `/compaction` triggers compaction
   - Then the story either implements an approved non-destructive job semantics or honestly defers the action.
   - And any implemented job writes `admin:storage-compaction-jobs:all` plus tenant-scoped keys with `Pending`, `Running`, `Completed`, or `Failed` status.
   - And it records what was compacted, what was not compacted, and why.
   - And it must not delete, rewrite, or archive event keys unless the decision record explicitly approves that behavior.

6. **Backup and restore operations do not pretend to be complete.**
   - Given `/backups` exposes backup trigger, validate, restore, export-stream, and import-stream
   - When full backup/restore engine semantics are not approved in this story
   - Then those actions are disabled or return typed `NOT_IMPLEMENTED`/`DEFERRED` results with clear UI copy and no "Admin service unavailable" masking.
   - When any backup operation is approved as `implemented-now`
   - Then it writes or updates `admin:backup-jobs:all` plus tenant-scoped keys and records job type, status, tenant, operation id, timestamps, and errors.
   - Restore must never overwrite original streams. Any approved restore writes to explicitly named parallel restore streams or performs dry-run validation only.
   - Export-stream may be implemented as a bounded read-only operation only if it uses the aggregate metadata and per-event key conventions, enforces an event limit, and validates tenant access.
   - Import-stream must validate payload structure before writing and must not bypass command/event validation rules.

7. **Admin.Server and UI preserve typed operation outcomes.**
   - Given EventStore returns a structured `AdminOperationResult`
   - When Admin.Server forwards the result
   - Then non-success outcomes such as not found, invalid operation, not implemented, timeout, and service unavailable remain distinguishable in API responses, API clients, toast/error copy, and tests.
   - And `DaprStorageCommandService` and `DaprBackupCommandService` do not collapse expected upstream non-success results into generic `HttpRequestException` messages when the upstream response body contains a usable `AdminOperationResult`.
   - And the UI does not keep buttons busy indefinitely after an expected not-implemented or rejected operation result.

8. **Tests pin route existence, authorization, state indexes, and honest-defer paths.**
   - Server tests cover each EventStore upstream route selected as `implemented-now`.
   - Admin.Server tests cover proxy behavior for success, typed failure, 404/not-found, not-implemented, timeout, and tenant authorization.
   - Admin.UI tests cover `/snapshots`, `/compaction`, and `/backups` rendering of implemented and honestly deferred actions, including busy-state cleanup.
   - Contract/model tests cover any new or changed DTO fields required for job/index evidence.

9. **Manual Aspire evidence is captured before review.**
   - With `EnableKeycloak=false` Aspire dev mode, Redis flushed, and the canonical sample flow seeded, record the behavior of `/snapshots`, `/compaction`, and `/backups`.
   - Capture endpoint payloads or screenshots for every Issue #15 operation that is implemented or honestly deferred.
   - Capture state-store evidence for `admin:storage-snapshot-policies:*`, `admin:storage-compaction-jobs:*`, and `admin:backup-jobs:*` where applicable.
   - Save sanitized evidence under `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/`.

## Tasks / Subtasks

- [ ] **ST0 - Record the Product/Architecture decision table.** (AC: 1)
  - [ ] Re-read Issue #15 in the manual-test issue file and Workstream E in the sprint-change proposal.
  - [ ] Inventory current Admin.Server proxy calls in `DaprStorageCommandService` and `DaprBackupCommandService`.
  - [ ] Confirm that no matching EventStore upstream routes exist today by searching the EventStore server projects.
  - [ ] Classify each operation as `implemented-now`, `honest-defer`, or `blocked` in this story's Dev Agent Record before production code edits.

- [ ] **ST1 - Add or disable upstream storage routes.** (AC: 2, 3, 4, 5, 7)
  - [ ] Implement approved EventStore upstream routes for snapshot policy set/delete, manual snapshot, and compaction.
  - [ ] For deferred operations, prevent the UI from invoking missing upstream routes and surface the limitation clearly.
  - [ ] Preserve route/body compatibility with the existing Admin.Server command services unless ST2 intentionally changes both sides.
  - [ ] Return structured `AdminOperationResult` values for accepted, rejected, not-found, not-implemented, and failed outcomes.

- [ ] **ST2 - Preserve typed results through Admin.Server.** (AC: 2, 7, 8)
  - [ ] Update `DaprStorageCommandService` and `DaprBackupCommandService` only as needed to read structured result bodies before throwing away upstream non-success detail.
  - [ ] Keep DAPR service invocation timeouts bounded by existing `AdminServerOptions.ServiceInvocationTimeoutSeconds`.
  - [ ] Add tests that prove 404 from a truly missing route is no longer the normal path for shipped UI actions.

- [ ] **ST3 - Implement or honestly defer backup route family.** (AC: 2, 6, 7, 8)
  - [ ] Add approved EventStore routes for backup trigger, validation, restore, export-stream, and import-stream.
  - [ ] For full backup/restore operations that remain out of scope, return typed `NOT_IMPLEMENTED`/`DEFERRED` results or disable UI controls.
  - [ ] If export-stream is implemented, bound reads by aggregate metadata and event keys; do not use backend-specific key scans.
  - [ ] If import-stream is implemented, validate payload structure and authorization before any writes.

- [ ] **ST4 - Populate operation indexes where behavior is implemented.** (AC: 3, 5, 6, 8)
  - [ ] Write snapshot policy, compaction job, and backup job indexes under the existing key names.
  - [ ] Maintain both `all` and tenant-scoped index views when the UI/query services require both.
  - [ ] Use deterministic ordering and bounded index sizes.
  - [ ] Do not report fake size, compaction, or backup metrics when the backend cannot provide them.

- [ ] **ST5 - Update UI/manual-test copy for honest behavior.** (AC: 6, 7, 9)
  - [ ] Align `/snapshots`, `/compaction`, and `/backups` button states, dialogs, and toasts with implemented/deferred decisions.
  - [ ] Ensure rejected/deferred operation results clear busy state and show actionable copy.
  - [ ] Update the manual-test guide or evidence notes only for Issue #15 expectations changed by this story.

- [ ] **ST6 - Validate and record evidence.** (AC: 8, 9)
  - [ ] Run impacted unit test projects individually per repository guidance.
  - [ ] Run canonical Aspire dev-mode manual evidence when environment allows.
  - [ ] Save sanitized payload/screenshot/key evidence under `_bmad-output/test-artifacts/admin-storage-snapshot-compaction-backup-operations/`.
  - [ ] Update this story's Dev Agent Record, File List, Verification Status, and Change Log.

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

TBD by dev agent.

### Debug Log References

TBD by dev agent.

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- No `project-context.md` file was present in the repository at story creation.
- No implementation work has been performed for this story.

### File List

TBD by dev agent.

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, or submodules.
- Party-mode review has NOT yet been run for this story.
- Advanced elicitation has NOT yet been run for this story.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-09 | 0.1 | Created ready-for-dev story for Issue #15 upstream snapshot, compaction, and backup operation behavior. | Codex automation |
