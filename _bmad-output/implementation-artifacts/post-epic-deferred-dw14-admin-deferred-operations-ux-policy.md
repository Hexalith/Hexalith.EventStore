# Post-Epic Deferred DW14: Admin Deferred Operations UX Policy

Status: ready-for-dev

Context created: 2026-05-20
Context refreshed: 2026-05-21
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md`
Source evidence: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`
Related completed baseline: `_bmad-output/implementation-artifacts/admin-storage-snapshot-compaction-backup-operations.md`

## Story

As an EventStore operator using storage administration pages,
I want unsupported or deferred operations to be communicated before I commit to an action,
so that the Admin UI is honest about current backend capabilities and never presents a fake success.

## Decision Locked For This Story

Implement the approved Correct Course recommendation for this iteration: accept the current backend `Deferred` responses, but make the Admin UI pre-communicate deferred or unsupported state before an operator submits destructive, heavy, or recovery-sensitive operations.

Use the **Truth-before-submit deferred operation pattern** for all five Issue 15 operations:

1. Show a visible inline or action-surface state such as `Deferred by backend` near the affected action before the operator opens the dialog.
2. Keep the action discoverable and openable, but make the dialog body state that the operation is currently deferred and will not complete real backend work in this iteration.
3. Rename the final primary action so it does not imply execution. Use `Submit Deferred Request`, `Acknowledge Deferred`, or an equally truthful label rather than `Start`, `Create`, `Validate`, or `Export`.
4. If the backend is still called for diagnostic parity, render `Success=false`, `Deferred`, `Unsupported`, or deferred-looking messages as a warning/info result, not success.

Do not implement real snapshot creation, compaction, backup creation, backup validation, stream export, restore, or import engines in this story. If product or architecture chooses real backend operation support instead, stop this story and split separate architecture-backed stories for:

- snapshot job model;
- non-destructive compaction model;
- backup engine and manifest;
- backup validation;
- bounded export;
- restore and import safety.

## Scope

This story covers CC-3 / Issue 15 from the 2026-05-20 manual Admin UI retest:

- Manual snapshot creation is explicitly deferred.
- Compaction execution is explicitly deferred.
- Backup creation is explicitly deferred.
- Backup validation is explicitly deferred.
- Stream export is explicitly deferred.

This story may also preserve or clarify restore/import copy where those actions are visible, because the same Backups page exposes recovery-sensitive controls. Restore/import are not required Issue 15 operations, and real restore/import backend support remains out of scope unless already implemented and proven by separate accepted work.

## Out Of Scope

- Changing Admin.Server storage, snapshot, compaction, backup, restore, export, or import engines except to preserve existing deferred response handling if a UI test requires a client contract assertion.
- Adding DAPR, Aspire, storage backend, or resiliency configuration.
- Changing role model or authorization. Preserve `Operator` access for snapshots/compaction and `Admin` access for backups/export/import as currently coded.
- Introducing a second UI framework, new frontend build tooling, or broad design-system refactors.
- Treating a backend `Success=false` deferred response as success, even if HTTP status is 200.

## Evidence To Preserve

The manual retest observed these backend messages for CC-3:

```text
Manual snapshot creation is deferred. EventStore does not yet have an approved snapshot job model for operator-triggered snapshots.
Compaction is deferred. EventStore write-once event keys require an approved non-destructive compaction model before this operation can run.
Backup creation is deferred. EventStore does not yet have an approved backup engine and manifest model.
Backup validation is deferred. EventStore does not yet have an approved backup manifest and validation model.
Stream export is deferred. EventStore needs an approved bounded export contract, format, and event limit before this operation can run.
```

The source evidence classifies this as a scope/UX gap, not a runtime crash. The UI currently lets an operator open action dialogs and submit operations that later return deferred messages. That is recoverable, but not honest enough before commitment.

## Acceptance Criteria

1. Snapshot creation, compaction, backup creation, backup validation, and stream export display explicit deferred or unsupported state before submission. The operator must see that the operation cannot currently run before clicking the final primary action.
2. Each affected action uses the Truth-before-submit deferred operation pattern: visible deferred status before opening the dialog, openable dialog with explicit deferred copy, and truthful final action labeling. Do not use fully disabled initiating buttons unless implementation evidence proves an operation cannot even be requested; if disabled buttons are used, the same visible reason and test gates still apply.
3. Manual snapshot creation in `/snapshots` uses the exact deferred policy from this story or a stricter equivalent. `Add Policy`, edit policy, delete policy, filtering, stat cards, and policy grid behavior continue to work as before.
4. Compaction in `/compaction` uses the exact deferred policy from this story or a stricter equivalent. Existing job listing, failed-job expansion, tenant filtering, active-job guard, stat cards, and refresh behavior continue to work as before.
5. Backup creation, backup validation, and stream export in `/backups` use the exact deferred policy from this story or a stricter equivalent. Existing job listing, validate/restore visibility rules, tenant filtering, active backup/restore guards, stat cards, refresh behavior, and failed-job expansion continue to work as before.
6. Restore and import, if visible, remain gated by explicit confirmation and risk copy. This story must not weaken the existing restore two-step acknowledgement, point-in-time validation, dry-run option, import preview validation, or file-size guard.
7. No affected unsupported operation displays a success toast or success-state copy when the backend response is deferred, unsupported, unavailable, `Success=false`, or even `Success=true` with deferred/unsupported wording. Existing deferred response tests must continue to prove busy state clears and dialogs remain usable.
8. UI copy distinguishes "operation works" from "operation is honestly deferred". Avoid labels such as `Start Backup`, `Start Compaction`, `Create Snapshot`, `Validate`, or `Export` as final actionable copy for the deferred action. Use `Submit Deferred Request`, `Acknowledge Deferred`, or equivalent truthful copy.
9. bUnit coverage proves pre-communication for all five Issue 15 operations: manual snapshot creation, compaction, backup creation, backup validation, and stream export. Tests must inspect the UI before the final action is submitted, assert the exact deferred message or a stricter approved equivalent, and keep or replace post-submit deferred response tests so the no-fake-success behavior remains proven.
10. The manual retest guide or evidence block is updated so Issue 15 can be recorded as `OK - deferred explicit` when UI and backend responses are truthful. Do not mark it as real operation success.
11. The implementation follows the existing Blazor/Fluent UI v5 page patterns, including `FluentDialogBody`, `TitleTemplate`, `ActionTemplate`, existing `AuthorizedView` role gates, `InvokeAsync(StateHasChanged)` where async completion can occur off dispatcher, and no nested card-in-card layout.
12. The Dev Agent Record records the Truth-before-submit deferred operation pattern for each operation, targeted test results, and whether manual Issue 15 validation was performed or explicitly left for operator follow-up.

## Tasks / Subtasks

- [ ] Reconfirm deferred UX scope before editing. (AC: 1, 2, 8)
  - [ ] Re-read the CC-3 section in the 2026-05-20 proposal and source evidence.
  - [ ] Treat the recommended decision as selected for this story: honest deferred UX, not backend engines.
  - [ ] Use the Truth-before-submit deferred operation pattern for all five Issue 15 operations unless evidence proves a stricter disabled-button variant is necessary.
  - [ ] Record any discovered backend support that contradicts the deferred decision before changing UI copy.
- [ ] Add focused bUnit coverage first. (AC: 1, 2, 7, 8, 9, 11)
  - [ ] Add or update `SnapshotsPageTests` to prove manual snapshot creation shows visible deferred status before final submission, uses truthful final action copy, and does not imply a real snapshot will be created.
  - [ ] Add or update `CompactionPageTests` to prove compaction shows visible deferred status before final submission, uses truthful final action copy, and does not imply real compaction will start.
  - [ ] Add or update `BackupsPageTests` to prove backup creation, backup validation, and stream export each show visible deferred status before final submission, use truthful final action copy, and do not imply real backup/validation/export work will start.
  - [ ] Assert exact deferred messages or stricter approved equivalents, not just "some warning appears."
  - [ ] Keep existing tests that prove deferred backend responses clear busy state and do not produce success UI.
- [ ] Implement Snapshots UX policy. (AC: 1, 2, 3, 7, 8, 11)
  - [ ] Update `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` around `OpenCreateSnapshotDialog`, the Create Snapshot dialog body, and final action button.
  - [ ] Change the manual snapshot action surface or dialog copy so the operator sees `Deferred by backend` or equivalent before final submission.
  - [ ] Preserve Add/Edit/Delete snapshot policy behavior and URL pre-fill behavior for policy creation.
  - [ ] Preserve `Operator` role gate and existing loading/error/filter states.
- [ ] Implement Compaction UX policy. (AC: 1, 2, 4, 7, 8, 11)
  - [ ] Update `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor` around `OpenTriggerDialog`, the Trigger Compaction dialog body, and final action button.
  - [ ] Change the compaction action surface or dialog copy so the operator sees `Deferred by backend` or equivalent before final submission.
  - [ ] Preserve active-job guard, tenant/domain fields if the dialog remains openable, failed-job expansion, and status badges.
  - [ ] Preserve `_disposed` guards around debounce callbacks.
- [ ] Implement Backups UX policy. (AC: 1, 2, 5, 6, 7, 8, 11)
  - [ ] Update `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor` around Create Backup, Validate Backup, and Export Stream.
  - [ ] Change backup creation, backup validation, and stream export action surfaces or dialog copy so the operator sees `Deferred by backend` or equivalent before final submission.
  - [ ] Preserve restore two-step confirmation and import preview/validation behavior.
  - [ ] Preserve `Admin` role gate, active backup/restore guards, toast best-effort wrappers, and `blazorDownloadFile` use only for true successful export.
- [ ] Update manual validation wording. (AC: 10, 12)
  - [ ] Update `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md` or a follow-up evidence artifact so Issue 15 has an `OK - deferred explicit` outcome option.
  - [ ] Keep the fixture keys for snapshot policies, compaction jobs, and backup jobs intact unless the manual retest artifact explicitly moves them.
- [ ] Validate and record. (AC: 9, 10, 12)
  - [ ] Run targeted Admin UI tests:

    ```powershell
    $dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
    $env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
    dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~Snapshots|FullyQualifiedName~Compaction|FullyQualifiedName~Backups" -m:1
    ```

  - [ ] If UI behavior needs runtime confirmation, start Aspire per repository instructions and run the Issue 15 manual path on `/snapshots`, `/compaction`, and `/backups`.
  - [ ] Record exact test commands, results, skipped runtime checks, and any remaining manual follow-up in the Dev Agent Record.

## Dev Notes

### Current State To Preserve

- `Snapshots.razor` renders `/snapshots`, loads policies through `AdminSnapshotApiClient.GetSnapshotPoliciesAsync`, supports tenant query filtering, policy add/edit/delete, and manual snapshot creation. The create snapshot dialog currently collects tenant, domain, and aggregate ID, calls `CreateSnapshotAsync`, and shows success only when `AdminOperationResult.Success == true`; deferred failures already show an error toast and leave the dialog open.
- `Compaction.razor` renders `/compaction`, loads jobs through `AdminCompactionApiClient.GetCompactionJobsAsync`, filters by tenant, shows job statuses, expands failed-job detail on row click, and blocks triggering when a job is already active for the tenant. The trigger dialog currently warns that compaction is resource intensive, calls `TriggerCompactionAsync`, and shows success only when `Success == true`; deferred failures already show an error toast and leave the dialog open.
- `Backups.razor` renders `/backups`, loads jobs through `AdminBackupApiClient.GetBackupJobsAsync`, filters by tenant, shows backup job actions, and exposes create, validate, restore, export, and import dialogs. It uses best-effort toast wrappers and downloads only when `StreamExportResult.Success == true` with content and filename.
- Existing restore behavior is already intentionally careful: completed and validated backups show Restore, the dialog has two steps, an acknowledgement checkbox, point-in-time validation, and dry-run. Do not loosen this while improving deferred copy for Issue 15.
- Existing import behavior validates file content for `TenantId`, `Domain`, `AggregateId`, and `Events` before submission and caps file read size at 10 MB. Do not loosen this.
- Existing tests already cover post-submit deferred responses:
  - `SnapshotsPage_CreateSnapshotDialog_ShowsDeferredToastAndClearsBusyState`
  - `CompactionPage_TriggerDialog_ShowsDeferredToastAndClearsBusyState`
  - `BackupsPage_CreateDialog_ShowsDeferredToastAndClearsBusyState`
  - `BackupsPage_ValidateDialog_ShowsDeferredToastAndClearsBusyState`
  - `BackupsPage_ExportDialog_ShowsDeferredToastAndClearsBusyState`

### Required Deferred Copy

Use these exact messages unless a stricter, equally explicit version is needed for layout:

| Operation | Required message | Truthful final action copy |
| --- | --- | --- |
| Manual snapshot creation | `Manual snapshot creation is deferred. EventStore does not yet have an approved snapshot job model for operator-triggered snapshots.` | `Submit Deferred Request` or `Acknowledge Deferred` |
| Compaction | `Compaction is deferred. EventStore write-once event keys require an approved non-destructive compaction model before this operation can run.` | `Submit Deferred Request` or `Acknowledge Deferred` |
| Backup creation | `Backup creation is deferred. EventStore does not yet have an approved backup engine and manifest model.` | `Submit Deferred Request` or `Acknowledge Deferred` |
| Backup validation | `Backup validation is deferred. EventStore does not yet have an approved backup manifest and validation model.` | `Submit Deferred Request` or `Acknowledge Deferred` |
| Stream export | `Stream export is deferred. EventStore needs an approved bounded export contract, format, and event limit before this operation can run.` | `Submit Deferred Request` or `Acknowledge Deferred` |

The selected pattern is visible deferred status plus truthful openable dialog. The initiating action may remain enabled for discoverability and diagnostic parity, but the visible label, dialog body, and final action must not imply real work will start. Use fully disabled initiating buttons only if implementation evidence proves the backend cannot even accept a request; if that variant is used, place the reason near the button or action group so screen readers and sighted users both understand why the command cannot run.

### UX Policy

- "Minimum friction to next step" means the page should still show useful existing data, refresh, filters, and job/policy rows. It does not mean pretending unavailable operations work.
- Prefer truthful action affordances over after-the-fact error toasts. The user should not have to submit a form to discover that the operation is intentionally not implemented.
- Use **Truth-before-submit deferred operation pattern** consistently: surface badge/message, explicit dialog, truthful final action copy, warning/info result on deferred response.
- Do not use in-app explanatory teaching text about the application's features broadly. Keep the copy scoped to the affected operation state and next action.
- Avoid a one-off visual pattern that makes these pages diverge from the rest of Admin UI. Use existing Fluent UI components and local page idioms.
- Preserve accessibility names on buttons/dialogs. If the visual label changes, make sure `aria-label` remains truthful.
- If a backend response is `Success=true` but the message or error code still indicates deferred/unsupported behavior, the operator-facing result remains deferred, not success.

### Architecture And Product Constraints

- PRD FR76 expects storage, compaction, snapshot, and backup management in admin tooling, but the 2026-05-20 Correct Course proposal explicitly accepts deferred backend operation responses for this iteration if the UI is honest.
- EventStore keys are write-once. Any future compaction model must be non-destructive and architecture-approved; do not create ad hoc state deletion or mutation here.
- Snapshots are optimizations, not a source of truth. Manual operator-triggered snapshot jobs need an approved job model before they become real work.
- Backup, validation, restore, import, and export affect event immutability, DAPR portability, protected-data behavior, and disaster recovery semantics. Do not implement them incidentally while changing UI copy.
- API errors and operation results must stay structured. `AdminOperationResult.Success=false` with `ErrorCode=Deferred` is not success.
- Logs, toasts, and UI copy must not include event payloads, command payloads, secrets, or protected data.

### File Structure Notes

Expected update paths:

- `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/SnapshotsPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CompactionPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/BackupsPageTests.cs`
- `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md` or a new dated follow-up evidence artifact

Avoid broad edits to Admin.Server, DAPR components, AppHost, package versions, shared authorization, or unrelated pages unless a focused failing test proves the current deferred response contract has regressed.

### Testing Notes

- Test project: `tests/Hexalith.EventStore.Admin.UI.Tests`.
- Frameworks/patterns already in use: bUnit, xUnit v3, Shouldly, NSubstitute, Fluent UI component rendering, and `AdminUITestContext`.
- The targeted test command should use the repository's local dotnet path setup when needed:

  ```powershell
  $dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
  $env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
  dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~Snapshots|FullyQualifiedName~Compaction|FullyQualifiedName~Backups" -m:1
  ```

- Run test projects individually. Do not use solution-level `dotnet test` for this story.
- If runtime manual validation is performed, use Aspire guidance from repository instructions. DAPR placement/scheduler may be needed for actor flows, but Issue 15 can often be validated from Admin UI page behavior and existing deferred backend responses.

### Previous Story Intelligence

DW13 reinforces several patterns that apply here:

- Preserve honest operator state. Do not claim completion when the backend has only timed out, rejected, or deferred the work.
- Keep command/action failure distinct from read/list failure. For DW14, storage data grids should keep showing fixtures/jobs/policies even when the action is deferred.
- Preserve generated operation/correlation IDs when available, but keep technical IDs secondary to clear operator copy.
- Record manual validation state explicitly, including whether runtime evidence was performed or deferred to an operator.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md#5.5-Story-DW14-Admin-deferred-operations-UX-policy`] - approved CC-3 routing, decision, acceptance criteria, and code references.
- [Source: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md#CC-3-Issue-15-Snapshot-Compaction-Backup-Export-sont-deferred`] - observed deferred messages and `OK - deferred explicite` recommendation.
- [Source: `_bmad-output/planning-artifacts/prd.md#FR76`] - admin storage, compaction, snapshot, and backup management scope.
- [Source: `_bmad-output/planning-artifacts/ux-design-specification.md`] - minimum-friction operator-action principle cited by Correct Course.
- [Source: `_bmad-output/implementation-artifacts/admin-storage-snapshot-compaction-backup-operations.md`] - completed baseline that introduced storage/snapshot/compaction/backup admin surfaces and deferred operation evidence.
- [Source: `_bmad-output/project-context.md`] - .NET 10, Fluent UI v5, testing, Aspire, DAPR, logging, and architecture guardrails.

## Dev Agent Record

### Agent Model Used

TBD by dev agent.

### UX Pattern Decisions

Use the selected pattern below unless implementation evidence proves the stricter disabled-button variant is required:

| Operation | Pattern selected | Notes |
| --- | --- | --- |
| Manual snapshot creation | Truth-before-submit deferred operation pattern | Visible deferred status, explicit dialog copy, truthful final action copy, warning/info deferred result |
| Compaction | Truth-before-submit deferred operation pattern | Visible deferred status, explicit dialog copy, truthful final action copy, warning/info deferred result |
| Backup creation | Truth-before-submit deferred operation pattern | Visible deferred status, explicit dialog copy, truthful final action copy, warning/info deferred result |
| Backup validation | Truth-before-submit deferred operation pattern | Visible deferred status, explicit dialog copy, truthful final action copy, warning/info deferred result |
| Stream export | Truth-before-submit deferred operation pattern | Visible deferred status, explicit dialog copy, truthful final action copy, warning/info deferred result |

### Debug Log References

TBD.

### Completion Notes List

- Story context engine analysis completed on 2026-05-21. Comprehensive developer guide created for deferred operations UX policy.
- Party-mode review fixes applied on 2026-05-21: concrete Truth-before-submit pattern selected, AC/test gates tightened, and Dev Agent Record UX choices prefilled.

### File List

TBD by dev agent.

### Verification Status

TBD by dev agent.

### Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-05-21 | 1.1 | Applied party-mode review fixes: selected Truth-before-submit deferred operation pattern, tightened no-fake-success edge cases, exact-copy test gates, and UX decision table. | Codex |
| 2026-05-21 | 1.0 | Expanded DW14 from starter handoff to ready-for-dev story with locked deferred UX decision, scoped ACs, dev notes, tests, and validation guidance. | Codex |
