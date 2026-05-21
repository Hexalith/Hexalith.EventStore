# Post-Epic Deferred DW15: Admin UI Blazor Navigation Hygiene

Status: ready-for-dev

Context created: 2026-05-20
Context refreshed: 2026-05-21
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md`
Source evidence: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`

## Story

As an Admin UI user navigating type catalog deep links,
I want TypeCatalog tab, filter, and selection URL updates to stop safely when the Blazor Server circuit or component is being disposed,
so that disconnect-time trace noise never pulls me back to `/types`, logs avoidable navigation failures, or hides a real Type Catalog regression.

## Scope

This story covers CC-6 from the 2026-05-20 Admin UI manual retest:

- `eventstore-admin-ui` logged navigation failures while changing location to `/types?type=CounterAggregate` and `/types?tab=aggregates`.
- The observed exception was `JSDisconnectedException: JavaScript interop calls cannot be issued at this time. This is because the circuit has disconnected and is being disposed.`
- The stack referenced `TypeCatalog.UpdateUrl(...)` and `TypeCatalog.OnTabChanged(...)`.

This is a low/medium hygiene story unless reproduced as visible user failure. Default to a narrow disposal/cancellation guard. A no-code evidence closure is allowed only after a focused reproduction attempt plus tests prove no component callback can call `UpdateUrl` after disposal or circuit disconnect, and normal tab/type navigation still updates the URL.

## Out Of Scope

- Rewriting TypeCatalog layout, grids, filters, API clients, or data-loading strategy.
- Changing route shape for `/types`, query parameter names, or TypeDetailPanel behavior.
- Changing global navigation, NavMenu, Breadcrumb, MainLayout, Fluent UI package versions, or apphost resources.
- Introducing a new frontend framework, JavaScript routing helper, or custom design-system abstraction.
- Treating Issue 12 Type Catalog fixture visibility as part of this bug unless the fix breaks it.

## Acceptance Criteria

1. TypeCatalog URL updates do not call `NavigationManager.NavigateTo` after the component has begun disposal or when the current URI proves the user is no longer on `/types`.
2. A stray `FluentTabs` `ActiveTabIdChanged` or TypeDetailPanel navigation callback fired during teardown cannot navigate the browser back to `/types`.
3. Existing TypeCatalog deep links keep working and remain canonical:
   - `/types`
   - `/types?tab=events` resolves to the events tab and canonicalizes default state without emitting redundant `tab=events`.
   - `/types?tab=commands`
   - `/types?tab=aggregates`
   - `/types?type=CounterAggregate`
   - `/types?tab=aggregates&type=CounterAggregate`
4. Existing idempotency behavior remains intact: if the target URL already matches `NavigationManager.Uri`, `UpdateUrl` does not call `NavigateTo` and does not fire `LocationChanged`.
5. Existing user-left-page behavior remains intact: after navigation to another page such as `/streams`, a late TypeCatalog URL update does not navigate back to `/types`.
6. Normal interaction remains observable: when the component is active and the user changes tabs, filters, or selected type, URL state still updates as before. A skipped navigation is acceptable only when the component is disposing/disconnected, the user is no longer on `/types`, or the target URL already matches the current URL.
7. Tests explicitly cover disposal-safe behavior. At minimum, a test must render `TypeCatalog`, dispose the component or invoke `DisposeAsync`, then force the URL-update path and assert no navigation, no exception, and no `LocationChanged` event. If bUnit cannot model the exact `JSDisconnectedException`, document the simulation boundary and cover the deterministic guard condition.
8. Tests explicitly cover pending async work after disposal: a debounced search continuation, refresh callback, tab callback, or equivalent forced pending path must not call `NavigationManager.NavigateTo` after disposal begins.
9. Tests cover `CounterAggregate` selection/deep-link stability, not only the seeded `OrderCreated` event happy path. The manual evidence fixture uses `CounterAggregate`; keep that exact shape represented in at least one regression.
10. If implementation catches exceptions around navigation, it catches only teardown/disconnect-safe exceptions such as `ObjectDisposedException`, `Microsoft.JSInterop.JSDisconnectedException`, or a documented Blazor navigation teardown exception. Do not swallow unrelated exceptions broadly.
11. Search debounce and refresh callbacks remain safe after disposal: pending debounce work, `DashboardRefreshService.OnDataChanged`, and any `InvokeAsync(StateHasChanged)` path must not schedule URL navigation after `DisposeAsync`.
12. Logging behavior is explicit: expected disposal/disconnect skips should either produce no log or a narrowly scoped debug/trace message. The fix must not log avoidable `Navigation failed` / `JSDisconnectedException` noise for the known CC-6 path, and it must not hide normal-interaction navigation failures.
13. Manual Issue 12 behavior remains stable if TypeCatalog code changes: Counter events, commands, aggregate row, tab switching, refresh, and deep links all behave as before.
14. The Dev Agent Record states whether CC-6 was fixed with a code guard or classified as trace noise, and includes the exact test commands, results, logging decision, and runtime/manual validation status.

## Tasks / Subtasks

- [ ] Reconfirm the current failure and existing guards before editing. (AC: 1, 2, 4, 5)
  - [ ] Read CC-6 in the manual evidence and DW15 in the approved Correct Course proposal.
  - [ ] Read the current `TypeCatalog.razor` `OnTabChanged`, cross-tab navigation methods, search debounce, `DisposeAsync`, and `UpdateUrl` paths.
  - [ ] Read the existing DW5/21-13 TypeCatalog tests so the fix preserves redirect-loop and canonical URL behavior.
- [ ] Add or harden failing tests first. (AC: 3, 4, 5, 6, 7, 8, 9, 12)
  - [ ] Add a disposal-safe URL-update test in `TypeCatalogPageTests.cs` or a focused DW15 test file.
  - [ ] Add a pending async/debounce or refresh-callback suppression test proving late work cannot navigate after disposal begins.
  - [ ] Add or extend a deep-link test for `CounterAggregate`, including `/types?tab=aggregates&type=CounterAggregate`.
  - [ ] Keep the existing idempotent URL and user-left-page tests passing.
  - [ ] Keep or add a positive navigation test proving active tab/type navigation still updates the URL in normal interaction.
  - [ ] If no production change is needed, make the test/evidence prove why the current code already blocks the CC-6 path, including disposal/debounce/navigation idempotency coverage.
- [ ] Implement the narrow guard only if tests prove a gap. (AC: 1, 2, 6, 10, 11, 12)
  - [ ] Prefer a simple `_disposed` flag set at the beginning of `DisposeAsync` and checked by `UpdateUrl`, async debounce callbacks, refresh callbacks, and cross-tab navigation entry points as needed.
  - [ ] Preserve the existing current-path guard that returns when `currentUri.AbsolutePath` is not `/types`.
  - [ ] Preserve the existing target-equals-current idempotency guard before `NavigateTo`.
  - [ ] If wrapping `NavigateTo`, catch only documented teardown/disconnect exceptions and leave unexpected navigation bugs visible.
- [ ] Validate TypeCatalog behavior. (AC: 3, 4, 5, 6, 7, 8, 9, 12, 13, 14)
  - [ ] Run the targeted TypeCatalog test slice.
  - [ ] If production code changed and Aspire can run, repeat Issue 12 manual checks for `/types`.
  - [ ] During manual validation, repeatedly navigate away or refresh during tab/detail/search changes and confirm there is no visible regression and no new navigation/disposal noise.
  - [ ] Record whether runtime validation was performed or blocked by local SDK/AppHost environment.

## Dev Notes

### Current State To Preserve

- `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor` is a Blazor page at `/types` using Fluent UI tabs, selects, text input, data grids, and a `TypeDetailPanel`.
- `OnInitializedAsync` reads URL parameters, loads events/commands/aggregates, then subscribes to `DashboardRefreshService.OnDataChanged`.
- `DisposeAsync` currently cancels and disposes the search debounce CTS and unsubscribes `OnRefreshSignal`. It does not currently expose a `_disposed` state to `UpdateUrl`.
- `OnTabChanged`, row click handlers, `NavigateToAggregate`, `NavigateToEvent`, `NavigateToCommand`, `ClearSelection`, domain-filter changes, and debounced search all call `UpdateUrl`.
- `UpdateUrl` already has two important guards:
  - It returns if the current path is not `/types`, preventing a late tab event from pulling users back after they clicked away.
  - It returns if current path/query already equals the target path/query, preventing the earlier redirect loop.
- The page currently stores default events tab as canonical `/types`; non-default tabs add `tab=commands` or `tab=aggregates`.
- A correct fix must distinguish safe teardown no-ops from normal-interaction failures. Do not make ordinary tab/type navigation silently fail while solving disposal-time noise.

### Files To Read Before Coding

- `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor`
  - Current URL update hot path: `OnTabChanged` around line 453, search debounce around line 476, cross-tab navigation around lines 550-589, and `UpdateUrl` around line 610.
  - Preserve: filter behavior, TypeDetailPanel navigation, `/types` query contract, idempotency guard, and user-left-page guard.
  - Likely change: add `_disposed` guard and disposal-aware checks before any late URL update.

- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs`
  - Existing anchors: `TypeCatalogPage_DeepLink_SetsActiveTabToAggregatesWithoutRedirectLoop`, `TypeCatalogPage_UpdateUrl_DoesNotNavigate_WhenUserHasLeftTypesPage`, and `TypeCatalogPage_UpdateUrl_IsIdempotent_WhenTargetEqualsCurrentUrl`.
  - Add the DW15 disposal-safe regression here if it fits the existing reflection-test style.

- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogUrlIdempotencyAtddTests.cs`
  - Existing anchors: `/types`, `/types?tab=commands`, `/types?tab=aggregates`, `/types?type=OrderCreated`, and default-tab canonicalization.
  - Add or mirror a `CounterAggregate` selection case if a new DW15 file would duplicate setup too much.

- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogRenderLoopAtddTests.cs`
  - Existing anchors: rapid tab toggles and same-tab render-count idempotency.
  - Keep these green; do not loosen them to pass a disposal fix.

- `src/Hexalith.EventStore.Admin.UI/Services/ViewportService.cs`
  - Pattern reference: catches `JSDisconnectedException` and `ObjectDisposedException` only during JS interop disposal/teardown.
  - Do not move TypeCatalog URL navigation into ViewportService.

- `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor`
  - Pattern reference: sets `_disposed` and unsubscribes refresh events. It also uses direct `Navigation.NavigateTo` only for user-initiated topology links.

### Existing Test Intelligence

- DW5 and Story 21.13 already fixed TypeCatalog redirect loops and sidebar navigation regressions. Recent relevant commits:
  - `a99fff4f fix(admin-ui): types page redirect-back + responsive sidebar 2-state model`
  - `c0751b77 fix(admin-ui): close DW5 runtime follow-ups with shortcut and dialog hardening`
- Do not remove the current path guard or target URL idempotency guard. The DW15 fix should layer on top of those protections.
- Current tests already cover idempotency and user-left-page behavior, but the story still needs explicit disposal-safe coverage because CC-6 happened during circuit disposal/disconnect.
- Trace-noise classification is not a shortcut. It requires a documented failed reproduction attempt and test evidence that disposal, debounce/refresh, user-left-page, and active-navigation paths are all intentionally covered.

### Latest Technical Notes

- Microsoft Blazor navigation docs for .NET 10 state that `NavigationManager.NavigateTo` performs internal navigation when `forceLoad` is false and enhanced navigation is available; this story should keep `forceLoad: false` and avoid unnecessary full reloads. Source: https://learn.microsoft.com/aspnet/core/blazor/fundamentals/navigation?view=aspnetcore-10.0
- Microsoft component disposal docs warn that `IAsyncDisposable` timing can occur before or after awaited lifecycle work completes, so disposal guards must not assume initialization work has fully completed. Source: https://learn.microsoft.com/aspnet/core/blazor/components/component-disposal?view=aspnetcore-10.0
- Microsoft JS interop docs state server-side Blazor JS interop calls fail with `JSDisconnectedException` after the SignalR circuit disconnects, and teardown code should catch that exception when appropriate. Source: https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability/?view=aspnetcore-10.0
- Fluent UI Blazor `FluentTabs` exposes `ActiveTabIdChanged` as the bound-value callback. The current page uses this callback, so late callback behavior is a realistic guard target. Source: https://fluentui-blazor.azurewebsites.net/Tabs

### Architecture And UX Guardrails

- Admin UI must stay in Blazor and Microsoft Fluent UI v5 patterns. Current package: `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.2-26098.1`.
- Use bUnit, xUnit v3, Shouldly, and NSubstitute for UI tests. Current package versions are centralized in `Directory.Packages.props`.
- Preserve UX principle "minimum friction to next step": a cleanup for trace noise must not make TypeCatalog tabs, search, filters, detail navigation, or deep links feel heavier.
- Do not add broad explanatory text to the UI for this issue. This is an internal navigation hygiene fix, not an operator-facing feature.
- Do not log event payloads, command payloads, secrets, or raw state values in any new diagnostics.

### Aspire Baseline Note

The story-context run attempted an Aspire baseline on 2026-05-21 before editing this artifact. The first command form from older instructions used `--project` and this Aspire CLI expects `--apphost`. The corrected command still failed before apphost evaluation because `global.json` pins SDK `10.0.300`, while this machine has SDKs `5.0.408`, `6.0.428`, `9.0.304`, and `10.0.103`. Treat runtime verification as blocked until SDK `10.0.300` is available or `global.json` is intentionally changed outside this story.

### Validation Commands

Run the focused TypeCatalog tests first:

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~TypeCatalog" -m:1
```

Also keep the prior regression anchors green. These exact tests, or their renamed equivalents if the test file is refactored, must be included in the Dev Agent Record:

```powershell
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~TypeCatalogPage_UpdateUrl_DoesNotNavigate_WhenUserHasLeftTypesPage" -m:1
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~TypeCatalogPage_UpdateUrl_IsIdempotent_WhenTargetEqualsCurrentUrl" -m:1
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~DeepLink_TabAggregates_InitializesAggregatesTab" -m:1
```

If the new test class name does not include `TypeCatalog`, include it explicitly in the filter. If production code changes and runtime validation is possible, restart Aspire and rerun the Issue 12 manual path from the source evidence:

```powershell
$env:EnableKeycloak = 'false'
aspire run --apphost .\src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj
```

Manual Issue 12 checks:

- `/types` events tab shows `CounterIncremented`, `CounterDecremented`, and `CounterReset`.
- `/types?tab=commands` shows `IncrementCounter`, `DecrementCounter`, and `ResetCounter`.
- `/types?tab=aggregates` shows `CounterAggregate`.
- `/types?type=CounterAggregate` or `/types?tab=aggregates&type=CounterAggregate` selects the aggregate detail without redirect loops.
- Refresh and tab changes remain stable.
- Repeatedly navigate away or refresh during tab/detail/search changes. Expected: no visible UI regression, no pull-back to `/types`, and no new `Navigation failed` / `JSDisconnectedException` noise for the CC-6 path.

## References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md#5.6-Story-DW15-Admin-UI-Blazor-navigation-hygiene`] - approved DW15 scope and acceptance criteria.
- [Source: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md#CC-6-Trace-noise-utile-TypeCatalog-navigation-pendant-disconnect`] - observed `JSDisconnectedException` evidence and prioritization.
- [Source: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md#Issue-12-Type-Catalog`] - manual Type Catalog validation path and fixture expectations.
- [Source: `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor`] - current TypeCatalog implementation and URL-update guards.
- [Source: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs`] - existing redirect-loop, user-left-page, and idempotency tests.
- [Source: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogUrlIdempotencyAtddTests.cs`] - existing deep-link and canonical URL tests.
- [Source: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogRenderLoopAtddTests.cs`] - existing render-loop guard tests.
- [Source: `_bmad-output/project-context.md`] - .NET 10, Fluent UI v5, Blazor, testing, Aspire, and workflow guardrails.

## Dev Agent Record

### Agent Model Used

TBD by dev agent.

### Decision: Code Fix Or Trace Noise

TBD by dev agent. Record one of:

- `Code fix`: describe the guard and why it is the narrowest safe change.
- `Trace noise`: describe the failed reproduction attempt and tests/evidence proving the current code already prevents user-visible failure and prevents post-disposal `UpdateUrl` callbacks from reaching `NavigateTo`.

### Debug Log References

TBD.

### Completion Notes List

- Story context engine analysis completed on 2026-05-21. Comprehensive developer guide created for TypeCatalog disposal-safe navigation hygiene.
- Party-mode review fixes applied on 2026-05-21: bounded the trace-noise escape hatch, required disposal/debounce navigation suppression coverage, required normal-navigation positive evidence, and made logging expectations explicit.

### File List

TBD by dev agent.

### Verification Status

TBD by dev agent.

### Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-05-21 | 1.1 | Applied party-mode review fixes: stricter trace-noise decision rule, disposal/debounce suppression tests, active navigation proof, regression test anchors, and logging expectations. | Codex |
| 2026-05-21 | 1.0 | Expanded DW15 starter handoff to ready-for-dev story with disposal-safe navigation guardrails, current-code intelligence, test anchors, and validation guidance. | Codex |
