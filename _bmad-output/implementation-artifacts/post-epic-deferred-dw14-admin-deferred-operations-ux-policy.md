# Post-Epic Deferred DW14: Admin Deferred Operations UX Policy

Status: backlog

Context created: 2026-05-20
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md`
Source evidence: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`

## Story

As an EventStore operator using storage administration pages,
I want unsupported or deferred operations to be communicated before I commit to an action,
so that the Admin UI is honest about current backend capabilities and never presents a fake success.

## Scope

This story covers CC-3 / Issue 15:

- Snapshot creation is explicitly deferred.
- Compaction is explicitly deferred.
- Backup creation is explicitly deferred.
- Backup validation is explicitly deferred.
- Stream export is explicitly deferred.

This story is primarily a product/UX policy story. It should not silently implement real backup, restore, compaction, or export engines without separate architecture approval.

## Decision

Recommended decision from the approved Correct Course proposal:

Accept current backend deferred responses for this iteration, but make the UI pre-communicate deferred or unsupported state before an operator clicks destructive or heavy operations.

If real backend operation support is selected instead, split this story into separate architecture-backed stories for:

- snapshot job model;
- non-destructive compaction model;
- backup engine and manifest;
- backup validation;
- bounded export;
- restore/import safety.

## Acceptance Criteria

If deferred behavior remains accepted:

1. Snapshot creation, compaction, backup creation, backup validation, and stream export display an explicit deferred/unsupported state in the UI.

2. Buttons either render disabled with a reason, or the confirmation dialog clearly states the operation is deferred before submission.

3. Manual tests mark Issue 15 as `OK - deferred explicit` when the UI and response are truthful.

4. No operation displays a fake success for unsupported backend work.

5. Restore and import remain gated by explicit confirmation and risk copy if visible.

6. The manual retest guide is updated to distinguish "operation works" from "operation is honestly deferred".

If real backend work is approved instead:

1. Update PRD/architecture before implementation because backup/restore and compaction affect event immutability, DAPR portability, protected-data behavior, and disaster recovery semantics.

2. Create separate implementation stories rather than expanding this UX policy story.

## Expected File Touches

Likely UI files:

- `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor`

Possible docs/test artifacts:

- `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`
- Admin UI page tests for disabled/deferred action states.

## Validation

Run targeted Admin UI tests for snapshots, compaction, and backups if present. Then rerun Issue 15 manual checks.

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~Snapshots|FullyQualifiedName~Compaction|FullyQualifiedName~Backups" -m:1
```

## Tasks

- [ ] Confirm whether deferred behavior remains accepted for this iteration.
- [ ] Add or update UI tests for visible deferred/unsupported state.
- [ ] Update Snapshots, Compaction, and Backups UI behavior.
- [ ] Update manual retest wording if needed.
- [ ] Run targeted tests and record results.
- [ ] Rerun manual Issue 15 validation.

