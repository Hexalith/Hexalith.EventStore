# Story: admin-ui-consistency-and-tenant-clarity-polish

Status: review

Context created: 2026-05-10
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-suite-issues.md`
Triggering evidence: `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
Scope: Issues #16 and #18, plus Issue #17 only after `admin-operational-index-populators` has produced its retest evidence.

## Story

As an EventStore operator using the Admin UI,
I want `/consistency` secondary text to reflect refreshed check data and `/tenants` to explain why tenants are disabled rather than deleted,
so that the Admin UI no longer presents stale accessible values or ambiguous tenant lifecycle behavior during manual operations.

## Issue Traceability

| Issue | Failing symptom | Covered by | Required evidence |
| --- | --- | --- | --- |
| #16 | `/consistency` stat-card primary values update, but visible/accessible secondary subtitles still show initial values such as `Total Checks: 0`, `Last Check: Never`, and `Total Anomalies: 0`. | AC1, AC2, AC5 | bUnit proof that stat-card values and titles/subtitles come from the same refreshed model after load, refresh, trigger, and auto-refresh. |
| #17 | `/consistency` reported 20 likely false anomalies after canonical seed, probably because projection indexes were empty. | AC3, AC5, AC6 | Retest consumes the evidence from `admin-operational-index-populators`; only investigate algorithm behavior if anomalies remain after populated `admin:projections:*` evidence exists. |
| #18 | `/tenants` has no Delete button or DELETE endpoint, but the page does not explain whether that absence is intentional. | AC4, AC5 | UI/manual evidence showing explicit tenant lifecycle copy: disable suspends access; physical delete is not offered by design unless a separate audited lifecycle story approves it. |

## Consistency and Tenant Truth Contract

- Primary stat-card values, secondary text, accessible names, and titles must all be derived from the same current in-memory model. A refresh must not update only the big number while leaving stale supporting text behind.
- `/consistency` must distinguish UI-binding defects from backend consistency findings. This story owns stale subtitle/binding polish. It does not own projection index population or broad consistency algorithm rewrites.
- Issue #17 is conditional. Do not tune the consistency algorithm until `admin-operational-index-populators` has populated or honestly classified `admin:projections:*` and recorded a canonical retest.
- Tenant lifecycle must be explicit. The default event-sourced policy is disable/enable for operational suspension and auditability, not physical deletion from the Admin UI.
- Do not add a tenant Delete button, DELETE endpoint, or hard-delete command in this story. If product later needs deletion/archive, route it through a separate audited tenant lifecycle story with Product and Architecture approval.
- UX copy must be visible to sighted users and useful to screen-reader users. Do not rely on color, tooltip-only text, or hidden comments to explain destructive-action absence.

## Acceptance Criteria

1. **Consistency stat cards update all displayed text from refreshed state.**
   - Given `/consistency` loads with no checks
   - When the Admin UI receives a refreshed list containing completed and running checks
   - Then the primary values for Total Checks, Last Check, Total Anomalies, and Running Now reflect the refreshed list.
   - And any stat-card `Title`, subtitle, `aria-label`, or equivalent secondary text reflects the same refreshed values rather than initial defaults.
   - And no visible or accessible string says `Total Checks: 0`, `Last Check: Never`, or `Total Anomalies: 0` when the current refreshed model contains non-zero checks or anomalies.

2. **Consistency refresh paths share the same summary model.**
   - Given checks change after initial page load
   - When the operator clicks Refresh, starts a check successfully, cancels a running check, changes the tenant filter, or auto-refresh runs while a check is running
   - Then the summary cards recompute from the current `_allChecks` list and filtered view rules consistently.
   - And implementation avoids duplicate summary state that can drift from `_allChecks`.
   - And tests cover at least one post-initial refresh where stale secondary text would previously survive.

3. **Issue #17 is gated behind the operational index retest.**
   - Given `admin-operational-index-populators` owns projection index population and the initial #17 retest
   - When this story begins development
   - Then the dev agent first reads that story's Dev Agent Record and any evidence under `_bmad-output/test-artifacts/admin-operational-index-populators/`.
   - If the retest shows zero or explained anomalies after populated projection indexes, this story records Issue #17 as resolved-by-prerequisite and does not change the consistency algorithm.
   - If anomalies remain, this story may only capture a bounded investigation note, check id, anomaly count, and payload summary; algorithm fixes must be deferred to a dedicated consistency-correctness story unless the defect is clearly a UI display/binding issue.

4. **Tenant delete absence is explained as product behavior.**
   - Given `/tenants` shows tenant rows and lifecycle actions
   - When an operator or admin reviews the page
   - Then the page includes concise visible copy explaining that tenants are disabled to suspend access and preserve audit history.
   - And the detail panel or lifecycle area makes clear that physical tenant deletion is not available in this Admin UI.
   - And the copy avoids implying that Delete is missing due to a loading failure, permission issue, or incomplete UI.
   - And no Delete tenant action is added unless a separate approved story changes the lifecycle policy.

5. **Automated tests pin stale binding and lifecycle clarity.**
   - `ConsistencyPageTests` or an equivalent bUnit suite verifies refreshed stat-card values and secondary/accessibility text after a non-empty check list is loaded.
   - Tests include a regression where initial empty state changes to non-empty state through reload/refresh and no stale `0`/`Never` supporting text remains.
   - `TenantsPageTests` verifies the tenant lifecycle copy is present for normal loaded state and does not disappear when the list is empty or filtered.
   - ReadOnly and Admin-role tests continue to prove that Create/Disable/Enable actions respect RBAC; tenant-delete explanatory copy must not leak new write permissions.

6. **Manual or bounded evidence is recorded.**
   - With Aspire dev mode available, capture `/consistency` after the latest index-populator retest and `/tenants` with at least one active and one disabled tenant.
   - Save sanitized evidence under `_bmad-output/test-artifacts/admin-ui-consistency-and-tenant-clarity-polish/`.
   - If Aspire is unavailable, record the exact blocker and provide bUnit/API test evidence instead.
   - Update this story's Dev Agent Record, File List, Verification Status, and Change Log before moving to review.

## Tasks / Subtasks

- [x] **ST0 - Baseline the two UI defects and prerequisite state.** (AC: 1, 3, 4)
  - [x] Re-read Issues #16, #17, and #18 in `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`.
  - [x] Read `admin-operational-index-populators` Dev Agent Record and evidence before touching Issue #17.
  - [x] Inspect `Consistency.razor`, `StatCard`, `ConsistencyPageTests`, `Tenants.razor`, and `TenantsPageTests`.
  - [x] Record whether Issue #17 is resolved, still blocked on the prerequisite, or still anomalous after populated projection indexes.

- [x] **ST1 - Fix consistency summary binding.** (AC: 1, 2)
  - [x] Ensure Total Checks, Last Check, Total Anomalies, Running Now, card title/subtitle text, and accessible text are computed from one current summary source.
  - [x] Prefer computed properties over storing duplicated summary values.
  - [x] Confirm tenant/domain filters do not produce mismatched totals unless the copy explicitly states whether totals are filtered or global.
  - [x] Preserve existing Run Check, Cancel, Refresh, row expansion, export, and anomaly detail behavior.

- [x] **ST2 - Add stale-subtitle regression tests.** (AC: 1, 2, 5)
  - [x] Add bUnit coverage for non-empty loaded checks proving no stale `Total Checks: 0`, `Last Check: Never`, or `Total Anomalies: 0` support text remains.
  - [x] Add a refresh/reload path where the mocked API returns empty first and non-empty second.
  - [x] Keep assertions resilient to relative-time wording by checking the presence/absence of stale values and current anomaly/count text.

- [x] **ST3 - Add tenant lifecycle clarity copy.** (AC: 4)
  - [x] Add concise copy near the tenant action area, detail panel, or page-level lifecycle help explaining disable/enable semantics.
  - [x] State that tenant disable suspends access while preserving audit history.
  - [x] State that physical deletion is not available in the Admin UI and requires a separate audited lifecycle decision if needed later.
  - [x] Avoid new destructive actions, new endpoints, or backend lifecycle changes.

- [x] **ST4 - Add tenant clarity tests.** (AC: 4, 5)
  - [x] Add bUnit coverage that the lifecycle copy appears after normal list load.
  - [x] Add coverage for empty or filtered states if the copy is page-level.
  - [x] Verify ReadOnly role still does not see write actions and Admin role still sees only the existing Create/Disable/Enable actions.
  - [x] Add a negative assertion that no `Delete Tenant` action is rendered by this story.

- [x] **ST5 - Handle Issue #17 based on prerequisite evidence.** (AC: 3, 6)
  - [x] If `admin-operational-index-populators` is not done, record #17 as blocked by prerequisite and do not edit algorithm code.
  - [x] If prerequisite evidence shows #17 resolved, add a Dev Agent Record note and no algorithm changes.
  - [x] If anomalies remain, capture check id, anomaly count, and payload summary; create or update a deferred-work entry for dedicated consistency correctness rather than expanding this polish story.

- [x] **ST6 - Validate and record evidence.** (AC: 5, 6)
  - [x] Run impacted UI tests individually.
  - [x] Run broader Admin.UI tests if changes touch shared `StatCard` or shared authorization components.
  - [x] Capture Aspire/manual evidence if available.
  - [x] Update this story's Dev Agent Record, File List, Verification Status, and Change Log.

## Developer Notes

Current code intelligence from story creation:

- `Consistency.razor` computes `TotalChecks`, `LastCheckDisplay`, `TotalAnomalies`, and `RunningCount` from `_allChecks`.
- Current stat-card `Title` values are static strings such as `Total consistency checks`, `Time of most recent completed check`, and `Total anomalies found across all completed checks`. The manual issue reported stale subtitle/supporting text, so inspect the rendered `StatCard` component and any title/subtitle/aria plumbing before assuming only this page is wrong.
- `LoadDataAsync`, `OnManualRefresh`, trigger success, cancel success, tenant-filter debounce, and auto-refresh all update `_allChecks` through `LoadDataAsync`. A stale display is likely a render/binding or duplicated child-component state issue.
- `ConsistencyPageTests` already covers loading, empty state, grid rendering, stat-card labels, Run Check authorization, trigger failures, running spinner, and filter URL behavior. Add focused stale-value tests rather than rewriting the suite.
- `Tenants.razor` currently exposes Create Tenant, Disable, Enable, Add User, Remove User, and Change Role behind Admin role authorization. There is no Delete Tenant action in the page.
- `AdminTenantsController` exposes create, disable, enable, add-user, remove-user, and change-role routes. No tenant DELETE endpoint was found during story creation.
- Story 16.5 rewired tenant management through the EventStore command/query pipeline and removed quota/compare behavior. Preserve that simplified lifecycle model.
- `TenantsPageTests` already proves stat cards, grid, empty state, create behavior, Disable/Enable visibility, filters, detail loading, and user-management calls. Add lifecycle-copy and no-delete assertions without weakening existing RBAC tests.

Architecture and product guardrails:

- PRD FR77 covers tenant management clarity. This story should make lifecycle behavior understandable, not introduce destructive lifecycle semantics.
- PRD FR79 and ADR-P4 keep Admin.Server as the shared Admin API. Do not add UI-only tenant lifecycle behavior that bypasses Admin.Server contracts.
- Event-sourced operational posture favors preserving historical evidence. Tenant disable/enable is the existing shipped lifecycle control; deletion/archive requires separate product and architecture approval.
- PRD FR75/FR76 and NFR41 require truthful operational rendering. Stale secondary text is a user-facing and accessibility defect even when the primary value is correct.
- `admin-operational-index-populators` owns Issues #11, #12, #14, and initial #17 retest. Do not absorb its writer/index work here.

## Files Likely Touched

- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor` only if the stale subtitle is component-level
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs`
- `_bmad-output/implementation-artifacts/deferred-work.md` only if Issue #17 remains after prerequisite evidence
- `_bmad-output/test-artifacts/admin-ui-consistency-and-tenant-clarity-polish/`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md` only if manual expectations need clarification

## Out of Scope

- Issues #6, #7, and #8 health/DAPR truthfulness.
- Issues #9 and #13 operator action dialog and dev role switching.
- Issue #10 actor diagnostics.
- Issues #11, #12, #14, and the initial Issue #17 retest owned by `admin-operational-index-populators`.
- Issue #15 snapshot, compaction, backup upstream endpoints, and DBA operation indexes.
- Broad consistency algorithm rewrites unless a new dedicated consistency-correctness story is approved.
- Tenant hard delete, archive, purge, GDPR erasure, stream deletion, or event rewrite semantics.
- New Admin.Server tenant endpoints, command contracts, or Hexalith.Tenants domain model changes.
- DAPR component YAML, AppHost topology, auth model, or role-switching changes.

## References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-suite-issues.md`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues-2026-05-07.md`
- `_bmad-output/implementation-artifacts/admin-operational-index-populators.md`
- `_bmad-output/implementation-artifacts/16-5-tenant-management-quotas-onboarding-comparison.md`
- `_bmad-output/planning-artifacts/prd.md#Administration Tooling`
- `_bmad-output/planning-artifacts/architecture.md#ADR-P4 Admin Tooling - Three-Interface Architecture Over Single DAPR API`
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json` baseline succeeded at 2026-05-10T13:45:44+02:00.
- `$env:EnableKeycloak='false'; aspire run --detach --non-interactive --project src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json` restarted the rebuilt app for browser evidence at 2026-05-10T13:53:35+02:00.
- Release verification used because live Aspire held the Debug Admin UI executable lock.

### Completion Notes List

- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- No `project-context.md` file was present in the repository at story creation.
- Implemented a single computed `ConsistencySummary` over the current filtered check view and bound Total Checks, Last Check, Total Anomalies, and Running Now primary values, visible subtitles, and titles from that summary.
- Updated `StatCard` accessible labels to include subtitle content when present, so screen-reader text follows the same current model as visible secondary text.
- Added stale-secondary regression coverage for an empty-to-non-empty consistency refresh and added tenant lifecycle coverage for empty and filtered-empty tenant states with negative `Delete Tenant` assertions.
- Issue #17 prerequisite evidence from `admin-operational-index-populators` shows check `01KR8N6MTQJGVF7BNN9ZZEAWV5` resolved the missing projection-index false-positive cluster; the one residual granularity warning is already recorded in `deferred-work.md`, so no consistency algorithm change was made here.
- Captured rebuilt Aspire dev-mode browser evidence under `_bmad-output/test-artifacts/admin-ui-consistency-and-tenant-clarity-polish/`.

### File List

- `_bmad-output/implementation-artifacts/admin-ui-consistency-and-tenant-clarity-polish.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/test-artifacts/admin-ui-consistency-and-tenant-clarity-polish/README.md`
- `_bmad-output/test-artifacts/admin-ui-consistency-and-tenant-clarity-polish/consistency-empty-summary-subtitles-snapshot.md`
- `_bmad-output/test-artifacts/admin-ui-consistency-and-tenant-clarity-polish/consistency-empty-summary-subtitles.png`
- `_bmad-output/test-artifacts/admin-ui-consistency-and-tenant-clarity-polish/tenants-lifecycle-copy-empty-snapshot.md`
- `_bmad-output/test-artifacts/admin-ui-consistency-and-tenant-clarity-polish/tenants-lifecycle-copy-empty.png`
- `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, or submodules.
- Party-mode review has NOT yet been run for this story.
- Advanced elicitation has NOT yet been run for this story.
- 2026-05-10 implementation moved to review after consistency summary/subtitle binding, tenant lifecycle clarity regression tests, rebuilt Aspire dev-mode browser evidence, and Release Admin.UI test validation.
- Validation: `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj -c Release --filter "FullyQualifiedName~Consistency_RefreshUpdatesStatCardSecondaryText_FromCurrentSummary|FullyQualifiedName~TenantsPage_ExplainsLifecycle"` passed 3/3.
- Validation: `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj -c Release --filter "FullyQualifiedName~ConsistencyPageTests|FullyQualifiedName~TenantsPageTests"` passed 56/56.
- Validation: `dotnet test tests\Hexalith.EventStore.Admin.UI.Tests\Hexalith.EventStore.Admin.UI.Tests.csproj -c Release` passed 763/763.
- Validation: `git diff --check` passed with line-ending warnings only.
- Debug validation note: the equivalent Debug test command was blocked by the live Aspire `Hexalith.EventStore.Admin.UI.exe` file lock, so final validation was performed in Release without stopping the evidence environment.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-10 | 0.2 | Implemented consistency summary subtitle/accessibility freshness, tenant lifecycle clarity test hardening, Issue #17 prerequisite disposition, and browser evidence. | GPT-5 Codex |
| 2026-05-10 | 0.1 | Created ready-for-dev story for consistency subtitle freshness, tenant lifecycle clarity, and conditional Issue #17 follow-up. | Codex automation |
