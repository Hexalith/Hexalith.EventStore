# Sprint Change Proposal - Admin UI Manual Test Suite Issues

**Project:** Hexalith.EventStore  
**Date:** 2026-05-07  
**Source evidence:** `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`  
**Workflow:** Correct Course, Batch mode  
**Status:** Approved by Jerome on 2026-05-07

## 1. Issue Summary

The 2026-05-07 Admin UI manual test continuation uncovered 13 corrective issues, numbered #6 through #18. Issues #6 through #13 have detailed sections in the source report. Issues #14 through #18 are present in the report recap only and should be treated as triaged but still requiring code audit before implementation.

The common pattern is that the Admin UI now has broad page coverage, but several pages are either:

- truthful UI bugs around loading, stale display state, or inconsistent metric rendering;
- observability gaps caused by DAPR sidecar scoping or multiple metadata sources;
- incomplete admin data flows where Admin.Server reads state-store indexes that are never populated;
- missing upstream EventStore admin write endpoints that Admin.Server already forwards to.

This is not one defect. It is a bundle that should be split into a small number of focused stories so the team can preserve momentum without hiding architecture decisions inside UI bug fixes.

## 2. Impact Analysis

### PRD Impact

The issues conflict with the Admin Tooling requirements:

- FR73: projection dashboard status, lag, throughput, errors, controls.
- FR74: registered event, command, and aggregate type catalog.
- FR75: operational health dashboard with truthful event count, throughput, error rate, DAPR component status, and observability links.
- FR76: storage, compaction, snapshot, and backup management.
- FR77: tenant management clarity.
- FR78: dead-letter queue browse, retry, skip, archive.
- FR79: shared Admin API behind Web UI, CLI, and MCP.
- NFR40: Admin API response behavior.
- NFR41: Admin Web UI health dashboard render behavior.
- NFR44: admin data access remains DAPR-backend-agnostic.
- NFR46: role-based admin access.

The MVP scope does not need to be redefined, but the Admin Tooling slice is not releasable as-is because several advertised pages are placeholders or misleading under normal manual-test conditions.

### Epic Impact

- Epic 14, Admin API Foundation: affected by missing or incomplete upstream EventStore admin operations and read indexes.
- Epic 15, Admin Web UI Core Developer Experience: affected by `/health`, `/dapr`, `/dapr/actors`, `/projections`, `/types`, `/consistency`, and dashboard metric correctness.
- Epic 16, Admin Web UI DBA Operations: affected by `/storage`, `/snapshots`, `/compaction`, `/backups`, and role-gated operator/admin actions.
- Epic 19, Admin DAPR Infrastructure Visibility: affected by DAPR component inventory, pubsub visibility, subscriptions consistency, health history, and actor diagnostics.

No existing epic needs removal. Add or update post-epic corrective stories.

### Architecture Impact

The strongest architecture finding is the "reader without writer" pattern:

- `admin:projections:*` is read by Admin.Server and consistency checks, but no writer was found in `src/`.
- `admin:type-catalog:*` is read by Admin.Server, but no writer was found in `src/`.
- `admin:storage-overview:*`, `admin:storage-hot-streams:*`, `admin:storage-stream-count:*`, `admin:storage-snapshot-policies:*`, `admin:storage-compaction-jobs:*`, and `admin:backup-jobs:*` are read by Admin.Server services, but the report indicates no corresponding population flow.

This should be treated as an Admin Operational Indexing architecture gap, not as isolated page bugs.

## 3. Recommended Approach

Recommended path: **Hybrid - Direct Adjustment plus backlog reorganization.**

Do direct fixes for narrow UI and resilience defects. Create separate backlog stories for backend/index/endpoints work where implementation needs design decisions.

### Workstream A - Health, DAPR Inventory, and Metrics Truthfulness

Scope: issues #6, #7, #8.

Fixes:

- Make `DaprHealthQueryService.GetSystemHealthAsync` degrade to a partial `SystemHealthReport` when Redis or DAPR state-store calls fail.
- Ensure `/health` never blank-screens on query failure; reuse the Home error/empty-state pattern.
- Choose and enforce one metric semantics rule for `EventsPerSecond` and `ErrorPercentage` across `/` and `/health`.
- Resolve DAPR pubsub visibility by either adding `eventstore-admin` to `pubsub.yaml` scopes or exposing inventory from the `eventstore` sidecar through a read-only service invocation.
- Unify `/health`, `/dapr`, `/dapr/pubsub`, and `/dapr/health-history` component/subscription counts or label them explicitly as configured vs active.

Severity: high.  
Effort: medium.  
Risk: medium, mostly around DAPR scope/security semantics.

### Workstream B - Operator Action UX and Dev Role Switching

Scope: issues #9 and #13.

Fixes:

- Ensure dead-letter action dialogs reset busy state in `finally` for Retry, Skip, and Archive.
- Show actionable backend failure details in the modal itself: HTTP status/category, affected message IDs when available, and trace ID.
- Audit other operator/admin action modals for the same busy-state failure pattern.
- Implement or expose a development-only role switcher for `ReadOnly`, `Operator`, and `Admin` when `EnableKeycloak=false`, or update the manual test guide with the exact JWT workaround if a UI toggle is intentionally out of scope.

Severity: medium/high for testability and operator trust.  
Effort: low to medium.  
Risk: low if dev-only role switcher is gated to Development and non-production auth mode.

### Workstream C - Actor Diagnostics Honesty

Scope: issue #10.

Fixes:

- Stop presenting partial active actor counts as authoritative totals.
- Prefer `unavailable` with explanatory copy when exact active actor counts cannot be queried through DAPR.
- Fix actor inspection lookup to use the owning app id (`eventstore`) and DAPR Redis actor key format when reading state-store-backed actor state.
- Consider an explicit admin-maintained actor activity index only if exact actor listing is a product requirement.

Severity: high for diagnostics.  
Effort: medium.  
Risk: medium because DAPR actor placement is not publicly queryable and Redis key-format coupling may weaken backend portability.

### Workstream D - Admin Operational Index Population

Scope: issues #11, #12, #14, and part of #17.

Fixes:

- Decide and implement population for `admin:projections:*`.
- Decide and implement population for `admin:type-catalog:*`; likely write-once at `eventstore` boot from loaded domain assemblies.
- Decide and implement population for storage indexes:
  - `admin:storage-overview:*`
  - `admin:storage-hot-streams:*`
  - `admin:storage-stream-count:*`
- Update `/projections`, `/types`, `/storage`, and `/consistency` to distinguish "no data exists" from "index unavailable/not configured."
- Add Counter sample projection/type/storage evidence so the canonical seed exercises these pages.
- Re-run `/consistency` after `admin:projections:*` exists before deciding whether issue #17 is a real consistency bug or a false positive from missing projection index data.

Severity: blocking for Admin Tooling completeness.  
Effort: high.  
Risk: high because this defines source-of-truth behavior for admin read models.

### Workstream E - Snapshot, Compaction, and Backup Upstream Operations

Scope: issue #15.

Fixes:

- Implement or explicitly defer upstream EventStore endpoints expected by Admin.Server:
  - `PUT api/v1/admin/storage/snapshot-policy`
  - `DELETE api/v1/admin/storage/snapshot-policy`
  - `POST api/v1/admin/storage/snapshot`
  - `POST api/v1/admin/storage/compact`
  - `api/v1/admin/backups/*` family: trigger, validate, restore, export-stream, import-stream.
- Populate related job/policy indexes:
  - `admin:storage-snapshot-policies:*`
  - `admin:storage-compaction-jobs:*`
  - `admin:backup-jobs:*`
- If full backup/restore is not ready, make the UI honest: disabled controls, "not implemented" operation result, and manual-test-guide expectations aligned with shipped behavior.

Severity: blocking for DBA operations.  
Effort: high.  
Risk: high, especially for backup/restore correctness and event-store immutability.

### Workstream F - Consistency and Tenant UX Polish

Scope: issues #16, #17, #18.

Fixes:

- Fix `/consistency` stat-card subtitles to bind to refreshed model state, not stale initial values.
- Reclassify issue #17 after Workstream D. If anomalies remain, investigate consistency algorithm behavior independently.
- Decide tenant deletion policy. Given event-sourcing audit expectations, the recommended default is: no physical delete in Admin UI; add explicit UX copy that tenants are disabled, not deleted, unless a separate audited delete/archive story is approved.

Severity: medium except #17 if confirmed after index fixes.  
Effort: low for #16/#18, unknown for #17.  
Risk: low for UX copy/binding, medium if consistency findings remain real.

## 4. Detailed Change Proposals

### Issue #6 - `/health` fails when Redis is down

OLD:

- `/health` depends on Redis-backed DAPR calls and can blank-screen/spin forever when Redis is stopped.

NEW:

- Health query catches DAPR/state-store failures and returns a partial report marking `state.redis` unhealthy.
- `Health.razor` renders an issue banner or error state instead of a blank page.

Rationale:

Monitoring must remain useful when the monitored dependency is down.

### Issue #7 - Metrics disagree between Home and Health

OLD:

- Home renders `Events/sec` and `Error Rate` as `unavailable`, while `/health` renders `0,0` and `0,0%` for the same report.

NEW:

- Both pages consume `SystemHealthMetricStatus` consistently.
- True zero renders as zero. Unwired source renders as `unavailable`.

Rationale:

Contradictory metrics undermine operator trust.

### Issue #8 - DAPR pubsub missing from `/health` and `/dapr`

OLD:

- `eventstore-admin` sees only `state.redis`; `/dapr/pubsub` sees pubsub via the `eventstore` sidecar.
- Subscription counts disagree.

NEW:

- One coherent DAPR component inventory source.
- Pubsub either appears everywhere it should, or the UI explicitly explains scoping limitations.

Rationale:

Observability must not hide a core DAPR component.

### Issue #9 - Dead-letter action modal spinner never resets on backend error

OLD:

- Retry/Skip/Archive error path leaves the confirmation button spinning.

NEW:

- All action dialogs use `try/catch/finally` and always reset busy state.
- Error details appear inside the modal and include trace ID when available.

Rationale:

Operators need clear failure feedback and a recoverable action state.

### Issue #10 - Actor diagnostics show false counts and false not-found

OLD:

- Total active actors shows `1` despite at least 5 Redis actor entries.
- Inspect cannot find a known active `AggregateActor`.

NEW:

- Counts are exact, or explicitly `unavailable`.
- Inspection uses the owner app id and correct actor state key format, or is disabled with an honest limitation message.

Rationale:

The page is currently misleading for its main diagnostic job.

### Issue #11 - `/projections` has no populated index

OLD:

- `admin:projections:*` has readers but no writer.
- Manual guide expects a sample projection that is not actually registered.

NEW:

- A projection registry/populator writes projection status data, or the feature is documented as intentionally empty until the registry exists.
- Counter sample includes a named projection if the guide expects one.

Rationale:

The page is non-functional by construction without a population flow.

### Issue #12 - `/types` has no populated type catalog

OLD:

- `admin:type-catalog:*` has readers but no writer.

NEW:

- EventStore writes type catalog indexes at boot from discovered domain assemblies, at least for `all` and preferably per domain.

Rationale:

Type catalog is static and should be a straightforward boot-time admin index.

### Issue #13 - No dev role switcher for manual RBAC validation

OLD:

- Manual guide references a UI role toggle that is not visible in `EnableKeycloak=false` mode.

NEW:

- Dev-only role switcher exists and is discoverable, or the guide documents a precise JWT workaround.

Rationale:

Seven manual-test sections depend on role switching.

### Issue #14 - `/storage` indexes are not populated

OLD:

- `/storage` shows `0/0/0/N/A` despite seeded events.
- Storage overview, hot streams, and stream count indexes appear to have no writer.

NEW:

- Storage admin indexes are populated from event activity or a dedicated storage summary process.
- Empty and unavailable states are distinguished.

Rationale:

Storage management cannot be trusted if it ignores persisted events.

### Issue #15 - Snapshot, compaction, and backup upstream endpoints are missing

OLD:

- Admin.Server forwards DBA actions to missing EventStore endpoints and returns "Admin service unavailable."

NEW:

- Implement the upstream endpoints and backing job/policy indexes, or disable/defer the UI actions honestly until implemented.

Rationale:

Forwarding to nonexistent operations creates a broken DBA surface.

### Issue #16 - `/consistency` subtitles show stale values

OLD:

- Main card values update but secondary text stays at initial values.

NEW:

- Subtitles bind to the same refreshed model as primary values.

Rationale:

Visible and accessible secondary text must not contradict the page.

### Issue #17 - `/consistency` reports 20 likely false anomalies

OLD:

- Consistency check detects 20 anomalies after canonical seed, likely because projection indexes are empty.

NEW:

- Re-test after projection index population.
- If anomalies remain, create a dedicated consistency correctness story.

Rationale:

Do not tune the consistency algorithm until missing admin index data is resolved.

### Issue #18 - `/tenants` delete behavior is unclear

OLD:

- No delete button or DELETE endpoint exists, but the UI does not explain whether deletion is intentionally unsupported.

NEW:

- Prefer explicit copy: tenants cannot be deleted by design; disable them to suspend access.
- Only add delete if a separate audited tenant lifecycle requirement is approved.

Rationale:

Event-sourced tenant lifecycle should favor auditability over destructive deletion.

## 5. Implementation Handoff

### Scope Classification

Overall scope: **Moderate to Major**.

Reason:

- Workstreams A, B, C, and F contain direct implementation fixes.
- Workstreams D and E require backlog reorganization and architecture decisions before coding.
- Issue #15 may become major if backup/restore semantics are not already designed elsewhere.

### Recommended Story Split

1. `admin-ui-health-dapr-truthfulness-fix`
   - Owns issues #6, #7, #8.
   - Route to Developer, with Architect review for DAPR pubsub scoping.

2. `admin-ui-operator-action-and-dev-role-testability-fix`
   - Owns issues #9, #13.
   - Route to Developer.

3. `admin-ui-actor-diagnostics-honesty-fix`
   - Owns issue #10.
   - Route to Developer plus Architect if exact actor inventory is requested.

4. `admin-operational-index-populators`
   - Owns issues #11, #12, #14 and initial #17 retest.
   - Route to Architect plus Developer.

5. `admin-storage-snapshot-compaction-backup-operations`
   - Owns issue #15.
   - Route to Architect/Product Owner before Developer implementation.

6. `admin-ui-consistency-and-tenant-clarity-polish`
   - Owns issues #16, #18, and issue #17 only if still present after workstream D.
   - Route to Developer.

### Success Criteria

- `/health` loads and reports degraded state when Redis is stopped.
- Home and Health render metric zero/unavailable semantics identically.
- DAPR component and subscription counts are consistent or explicitly labeled by source.
- Dead-letter action modals never remain busy after backend failure.
- Actor diagnostics do not present partial counts as exact totals.
- `/projections`, `/types`, and `/storage` either show populated canonical-seed data or honest unavailable/not-configured states.
- Snapshot, compaction, and backup actions no longer forward to missing upstream endpoints without clear user feedback.
- `/consistency` visible and secondary values agree after checks.
- Manual role switching or documented JWT role override unblocks RBAC validation.
- Tenant delete absence is explained as product behavior or routed into a separate audited deletion story.

## 6. Checklist Status

- [x] 1.1 Trigger identified: manual Admin UI test session 2026-05-07, issues #6-#18.
- [x] 1.2 Core problem defined: Admin UI contains multiple incomplete or misleading operational surfaces.
- [x] 1.3 Supporting evidence captured from the issue file; code spot-check confirms several reader-without-writer index patterns.
- [x] 2.1 Current epic impact assessed: Epics 14, 15, 16, and 19 affected.
- [x] 2.2 Epic-level changes identified: add post-epic corrective stories, no epic removal.
- [x] 2.3 Remaining epics reviewed at high level.
- [x] 2.4 No future epic invalidated; new corrective stories are needed.
- [x] 2.5 Priority should favor #6, #10, #11/#12/#14, and #15 before polish.
- [x] 3.1 PRD conflicts mapped to FR73-FR79 and NFR40-NFR46.
- [x] 3.2 Architecture conflicts identified around admin index source-of-truth and DAPR sidecar scoping.
- [x] 3.3 UX conflicts identified around blank screens, stale subtitles, modal busy states, and role-switch discoverability.
- [x] 3.4 Secondary artifacts: manual test guide and sprint-status need updates after approval.
- [x] 4.1 Direct adjustment is viable for UI/resilience fixes.
- [x] 4.2 Rollback is not recommended; these issues are additive corrections.
- [x] 4.3 MVP review is not needed, but Admin Tooling release readiness is affected.
- [x] 4.4 Recommended path selected: Hybrid.
- [x] 5.1 Issue summary created.
- [x] 5.2 Epic impact and artifact adjustments documented.
- [x] 5.3 Recommended path documented.
- [x] 5.4 MVP impact and high-level action plan documented.
- [x] 5.5 Handoff plan documented.
- [x] 6.3 User approval received from Jerome on 2026-05-07.
- [x] 6.4 `sprint-status.yaml` updated with approved corrective story split.

## 7. Approval and Routing

Jerome approved this Sprint Change Proposal on 2026-05-07.

Approved handoff:

- Developer: `admin-ui-health-dapr-truthfulness-fix`
- Developer: `admin-ui-operator-action-and-dev-role-testability-fix`
- Developer plus Architect if exact inventory is required: `admin-ui-actor-diagnostics-honesty-fix`
- Architect plus Developer: `admin-operational-index-populators`
- Architect/Product Owner before Developer implementation: `admin-storage-snapshot-compaction-backup-operations`
- Developer: `admin-ui-consistency-and-tenant-clarity-polish`

Correct Course workflow complete, Jerome.
