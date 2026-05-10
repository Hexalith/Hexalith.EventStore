# Admin Storage Snapshot Compaction Backup Operations Evidence

Date: 2026-05-10
Story: `admin-storage-snapshot-compaction-backup-operations`
Scope: Issue #15 only

## Runtime

- AppHost command: `EnableKeycloak=false aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json`
- Dashboard: `https://localhost:17017/login?t=82ab2cc4cacc0bc99f863ef765ce2cb9`
- Admin API base: `http://localhost:8090`
- Admin UI: `https://localhost:8093`
- EventStore Dapr HTTP endpoint used for state evidence: `http://localhost:55105`
- Redis baseline: `redis-flush.json` records `FLUSHDB` returning `+OK`.

## Decision

`operation-decision-record.md` classifies all Issue #15 write operations as `honest-defer`. No fake storage, compaction, backup, restore, export, or import engine was added.

## Live API Evidence

See `live-api-evidence-summary.json` for the rollup and the per-operation JSON files for request/response bodies.

| Operation | Evidence file | Expected outcome |
| --- | --- | --- |
| Snapshot policy set | `snapshot-policy-set-deferred.json` | HTTP 200, `AdminOperationResult.Success=false`, `ErrorCode=Deferred` |
| Snapshot policy delete | `snapshot-policy-delete-deferred.json` | HTTP 200, `AdminOperationResult.Success=false`, `ErrorCode=Deferred` |
| Manual snapshot | `manual-snapshot-deferred.json` | HTTP 200, `AdminOperationResult.Success=false`, `ErrorCode=Deferred` |
| Compaction | `compaction-deferred.json` | HTTP 200, `AdminOperationResult.Success=false`, `ErrorCode=Deferred` |
| Backup trigger | `backup-trigger-deferred.json` | HTTP 200, `AdminOperationResult.Success=false`, `ErrorCode=Deferred` |
| Backup validate | `backup-validate-deferred.json` | HTTP 200, `AdminOperationResult.Success=false`, `ErrorCode=Deferred` |
| Backup restore | `backup-restore-deferred.json` | HTTP 200, `AdminOperationResult.Success=false`, `ErrorCode=Deferred` |
| Export stream | `export-stream-deferred.json` | HTTP 200, `StreamExportResult.Success=false`, deferred `ErrorMessage` |
| Import stream | `import-stream-deferred.json` | HTTP 200, `AdminOperationResult.Success=false`, `ErrorCode=Deferred` |
| Unauthorized request | `unauthorized-compaction-no-token.json` | HTTP 401 |
| Tenant mismatch | `tenant-mismatch-compaction.json` | HTTP 403, `Tenant Access Denied` |

## State Evidence

Deferred paths must not create operational indexes. After Redis flush and all deferred operation calls, these Dapr state reads returned HTTP 204:

- `state-admin-storage-snapshot-policies-all.json`
- `state-admin-storage-snapshot-policies-tenant-a.json`
- `state-admin-storage-compaction-jobs-all.json`
- `state-admin-storage-compaction-jobs-tenant-a.json`
- `state-admin-backup-jobs-all.json`
- `state-admin-backup-jobs-tenant-a.json`

## UI Evidence

Browser screenshots and DOM snapshots were captured for the three affected pages after the no-Keycloak dev run:

- `ui-snapshots-page.png` and `ui-snapshots-page-dom.txt`
- `ui-compaction-page.png` and `ui-compaction-page-dom.txt`
- `ui-backups-page.png` and `ui-backups-page-dom.txt`
- `ui-screenshot-summary.json`

## Log Evidence

`aspire-log-excerpts.md` records the relevant Aspire MCP observations:

- resources were running and healthy in the no-Keycloak topology;
- Admin.Server advertised the EventStore Dapr HTTP endpoint;
- the tenant-mismatch probe produced an explicit tenant access denied structured log;
- EventStore structured logs did not show Issue #15 upstream admin route invocation entries for the deferred operation calls.

## AC Mapping

- AC1: `operation-decision-record.md`
- AC2-AC7: per-operation API JSON files and unit tests listed in the story record
- AC8: Admin.Server, Admin.UI, Admin.Abstractions, Client, Contracts, Sample, and Testing unit test results in the story record
- AC9: Redis flush, live API evidence, state evidence, UI screenshots, and Aspire log excerpts in this folder
