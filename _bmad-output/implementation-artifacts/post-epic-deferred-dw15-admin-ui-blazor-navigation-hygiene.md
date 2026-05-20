# Post-Epic Deferred DW15: Admin UI Blazor Navigation Hygiene

Status: backlog

Context created: 2026-05-20
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md`
Source evidence: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`

## Story

As an Admin UI user navigating type catalog deep links,
I want tab and filter URL updates to remain safe during Blazor Server disconnect or disposal,
so that trace noise does not become a visible navigation failure.

## Scope

This story covers CC-6:

- TypeCatalog navigation logs show failures when changing location to `/types?type=CounterAggregate` and `/types?tab=aggregates`.
- Exception evidence: `JSDisconnectedException: JavaScript interop calls cannot be issued at this time. This is because the circuit has disconnected and is being disposed.`
- Stack evidence references `TypeCatalog.UpdateUrl(...)` and `TypeCatalog.OnTabChanged(...)`.

This story is lower priority than DW11-DW13 unless the navigation issue is visible and reproducible for users.

## Acceptance Criteria

1. TypeCatalog tab/filter URL updates do not call JS interop or navigation after circuit disposal/disconnect.

2. Existing TypeCatalog deep links remain stable:
   - `/types?tab=events`
   - `/types?tab=commands`
   - `/types?tab=aggregates`
   - `/types?type=CounterAggregate`

3. Tests cover idempotent URL updates and user-left-page/disconnect-safe guards.

4. If current tests already cover this behavior, classify CC-6 as trace noise and document why no code change is needed.

5. Manual validation confirms Issue 12 Type Catalog behavior remains stable after refresh and tab changes.

## Expected File Touches

Likely implementation file:

- `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor`

Likely tests:

- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogUrlIdempotencyAtddTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogRenderLoopAtddTests.cs`

## Validation

Run targeted TypeCatalog tests:

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~TypeCatalog" -m:1
```

Then rerun Issue 12 manual checks if TypeCatalog code changes.

## Tasks

- [ ] Review current TypeCatalog URL/navigation tests before changing code.
- [ ] Decide whether CC-6 is already covered trace noise or needs a code fix.
- [ ] Add failing test if behavior is not covered.
- [ ] Implement navigation/disposal guard if needed.
- [ ] Run targeted TypeCatalog tests and record results.
- [ ] Rerun manual Issue 12 validation if code changes.

