# Post-Epic Deferred DW15: Admin UI Blazor Navigation Hygiene

Status: done

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

- [x] Reconfirm the current failure and existing guards before editing. (AC: 1, 2, 4, 5)
  - [x] Read CC-6 in the manual evidence and DW15 in the approved Correct Course proposal.
  - [x] Read the current `TypeCatalog.razor` `OnTabChanged`, cross-tab navigation methods, search debounce, `DisposeAsync`, and `UpdateUrl` paths.
  - [x] Read the existing DW5/21-13 TypeCatalog tests so the fix preserves redirect-loop and canonical URL behavior.
- [x] Add or harden failing tests first. (AC: 3, 4, 5, 6, 7, 8, 9, 12)
  - [x] Add a disposal-safe URL-update test in `TypeCatalogPageTests.cs` or a focused DW15 test file.
  - [x] Add a pending async/debounce or refresh-callback suppression test proving late work cannot navigate after disposal begins.
  - [x] Add or extend a deep-link test for `CounterAggregate`, including `/types?tab=aggregates&type=CounterAggregate`.
  - [x] Keep the existing idempotent URL and user-left-page tests passing.
  - [x] Keep or add a positive navigation test proving active tab/type navigation still updates the URL in normal interaction.
  - [x] If no production change is needed, make the test/evidence prove why the current code already blocks the CC-6 path, including disposal/debounce/navigation idempotency coverage.
- [x] Implement the narrow guard only if tests prove a gap. (AC: 1, 2, 6, 10, 11, 12)
  - [x] Prefer a simple `_disposed` flag set at the beginning of `DisposeAsync` and checked by `UpdateUrl`, async debounce callbacks, refresh callbacks, and cross-tab navigation entry points as needed.
  - [x] Preserve the existing current-path guard that returns when `currentUri.AbsolutePath` is not `/types`.
  - [x] Preserve the existing target-equals-current idempotency guard before `NavigateTo`.
  - [x] If wrapping `NavigateTo`, catch only documented teardown/disconnect exceptions and leave unexpected navigation bugs visible.
- [x] Validate TypeCatalog behavior. (AC: 3, 4, 5, 6, 7, 8, 9, 12, 13, 14)
  - [x] Run the targeted TypeCatalog test slice.
  - [x] If production code changed and Aspire can run, repeat Issue 12 manual checks for `/types`.
  - [x] During manual validation, repeatedly navigate away or refresh during tab/detail/search changes and confirm there is no visible regression and no new navigation/disposal noise.
  - [x] Record whether runtime validation was performed or blocked by local SDK/AppHost environment.

### Review Findings

- [x] [Review][Patch] Bare `CounterAggregate` deep link remains uncovered and appears unsupported [src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor:340] — AC3 explicitly requires `/types?type=CounterAggregate`, but `ReadUrlParameters` stores `type` without inferring the aggregate tab and `SelectTypeByName` only searches the current `_activeTab`, which defaults to `events`. The new CounterAggregate test covers only `/types?tab=aggregates&type=CounterAggregate`, so the bare URL still appears to render the events tab with no aggregate selection. Fixed by allowing bare type links to infer events, then commands, then aggregates, and by adding `DeepLink_BareType_CounterAggregate_SelectsAggregateTab`.
- [x] [Review][Patch] Initial-load disposal can subscribe a disposed component to refresh events [src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor:288] — `OnInitializedAsync` awaits `LoadDataAsync` before subscribing to `RefreshService.OnDataChanged`. If `DisposeAsync` runs while the initial load is awaiting, the unsubscribe at disposal time is a no-op and the continuation can subscribe afterward with `_disposed = true`, leaving a disposed component retained by the refresh service. Fixed by checking `_disposed` before subscribing and by adding `InitialLoad_DoesNotSubscribeRefresh_WhenDisposedBeforeLoadCompletes`.
- [x] [Review][Patch] Late search callback can throw before reaching the disposal guard [src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor:499] — `DisposeAsync` cancels and disposes `_debounceTokenSource` but leaves the field populated. A late `OnSearchValueChanged` callback after disposal can call `Cancel()` or `Dispose()` on that disposed CTS before the new `_disposed` checks inside the background task run. Fixed by returning from `OnSearchValueChanged` after disposal, nulling the CTS during disposal, and adding `SearchCallback_DoesNotThrowOrNavigate_WhenInvokedAfterDispose`.
- [x] [Review][Patch] Refresh teardown test can pass while hiding callback exceptions [tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw15TypeCatalogDisposalSafeNavigationAtddTests.cs:197] — `RefreshSignal_DoesNotNavigate_AfterDispose` catches and ignores `TargetInvocationException`, then asserts only that no navigation occurred. A post-dispose refresh regression that throws teardown noise would still pass. Fixed by asserting the direct refresh callback produces no exception and no navigation.

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

Claude Opus 4.7 (claude-opus-4-7[1m]).

### Decision: Code Fix Or Trace Noise

**Code fix.** The narrowest change is a `_disposed` flag set at the top of `DisposeAsync` and checked at the entry of `UpdateUrl`, the search-debounce continuation, and `OnRefreshSignal`. The `NavigateTo` call is wrapped in a narrow catch for `Microsoft.JSInterop.JSDisconnectedException` and `ObjectDisposedException` only — every other navigation failure remains visible.

The two failing red-phase tests (`UpdateUrl_DoesNotNavigate_AfterDisposedFlagSet`, `UpdateUrl_DoesNotNavigate_AfterDisposeAsyncCompleted`) proved the gap: before the fix, a stray `OnTabChanged` after `DisposeAsync` produced `LocationChanged` count 1 (pulling the user back to `/types`). After the fix, both go to 0. The existing current-path guard and target-equals-current idempotency guard are preserved unchanged — the new guard layers on top.

The four green-on-arrival tests (`PendingSearchDebounce_*`, `RefreshSignal_*`, `DeepLink_TabAggregatesType_CounterAggregate_*`, `NormalTabChange_StillUpdatesUrl_*`) prove that the debounce CTS cancellation, refresh unsubscription, the CounterAggregate deep-link contract, and the positive normal-navigation behavior all hold both before and after the fix. They are kept as forward regression anchors.

### Debug Log References

- Red phase (pre-fix): `dotnet test ... --filter "FullyQualifiedName~Dw15TypeCatalogDisposalSafe"` → 4 passed, **2 failed** (the two `_disposed`-guard tests, with the `TryGetPrivateField(_, "_disposed", _)` assertion explicitly pointing at the missing field).
- Green phase (post-fix): same command → **6/6 passed**.
- TypeCatalog slice: `dotnet test ... --filter "FullyQualifiedName~TypeCatalog"` → **36/36 passed**.
- Story-named regression anchors (`TypeCatalogPage_UpdateUrl_DoesNotNavigate_WhenUserHasLeftTypesPage`, `TypeCatalogPage_UpdateUrl_IsIdempotent_WhenTargetEqualsCurrentUrl`, `DeepLink_TabAggregates_InitializesAggregatesTab`) → **3/3 passed**.
- Full Admin.UI.Tests project: **824/825 passed**. The single failure is `JsonViewerTests.JsonViewer_ShowsWarning_WhenJsonIsInvalid`, **pre-existing on `main` (commit 5da278df)** and unrelated to TypeCatalog — verified by reverting DW15 changes (`git stash`) and re-running the same test in isolation: same failure. Out of DW15 scope.

### Logging Decision (AC 12)

No new diagnostics added. The expected disposal/disconnect path short-circuits silently — no log entries are emitted on the CC-6 hot path. Real navigation failures remain visible because:

1. The `_disposed` guard returns before `NavigateTo`, so framework-level navigation errors are unaffected.
2. The `try/catch` around `NavigateTo` catches only `JSDisconnectedException` and `ObjectDisposedException`. Any other exception bubbles up as before.
3. The debounce continuation's existing `OperationCanceledException` swallow is unchanged. A narrow `ObjectDisposedException` catch is added there for `InvokeAsync` on a torn-down scope — same teardown class as the rest.

This satisfies "no avoidable `Navigation failed` / `JSDisconnectedException` noise for the known CC-6 path, and must not hide normal-interaction navigation failures."

### Completion Notes List

- Story context engine analysis completed on 2026-05-21. Comprehensive developer guide created for TypeCatalog disposal-safe navigation hygiene.
- Party-mode review fixes applied on 2026-05-21: bounded the trace-noise escape hatch, required disposal/debounce navigation suppression coverage, required normal-navigation positive evidence, and made logging expectations explicit.
- 2026-05-21 implementation: added `_disposed` flag in `TypeCatalog.razor`, set at start of `DisposeAsync`; checked at entry of `UpdateUrl`, `OnRefreshSignal`, and the search debounce continuation; narrow `JSDisconnectedException` + `ObjectDisposedException` catch around `NavigateTo`. Existing current-path and target-equals-current guards preserved.
- Red-then-green TDD verified: 6 new ATDD tests in `Dw15TypeCatalogDisposalSafeNavigationAtddTests.cs`, two failing pre-fix and all six passing post-fix.
- 2026-05-21 code-review patches: bare `CounterAggregate` deep links now infer the aggregate tab; `OnInitializedAsync` skips refresh subscription after disposal; `DisposeAsync` nulls the debounce CTS; late search callbacks return after disposal; refresh teardown test now asserts no exception. Expanded DW15 tests to 9/9 and TypeCatalog slice to 39/39.
- Runtime/manual Issue 12 validation **blocked** by SDK pinning: `global.json` pins SDK 10.0.300 but the machine-wide `C:\Program Files\dotnet` ships 5.0.408 / 6.0.428 / 9.0.304 / 10.0.103. The user-local `%LOCALAPPDATA%\Microsoft\dotnet` has 10.0.300 and was used for the bUnit slice, but `aspire run` against the AppHost was not re-attempted in this story — the SDK/PATH situation is unchanged from the story-context's 2026-05-21 Aspire baseline note.

### File List

- `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor` — added `_disposed` field, set in `DisposeAsync`, checked in `UpdateUrl`, `OnRefreshSignal`, and the search debounce continuation. Wrapped `NavigationManager.NavigateTo` in narrow `JSDisconnectedException` + `ObjectDisposedException` catches. Idempotency guard and user-left-page guard preserved.
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw15TypeCatalogDisposalSafeNavigationAtddTests.cs` — new bUnit test class with 9 tests covering disposed-flag UpdateUrl short-circuit, post-DisposeAsync stray callback, pending debounce after dispose, late search after dispose, initial-load disposal subscription safety, refresh signal after dispose, `/types?tab=aggregates&type=CounterAggregate`, `/types?type=CounterAggregate`, and positive normal-tab-change navigation.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `post-epic-deferred-dw15-admin-ui-blazor-navigation-hygiene: ready-for-dev → review`; header `last_updated` refreshed.
- `_bmad-output/implementation-artifacts/post-epic-deferred-dw15-admin-ui-blazor-navigation-hygiene.md` — story status, Tasks/Subtasks checkboxes, Dev Agent Record, File List, Change Log updated.

### Verification Status

- **Tier 1 bUnit tests:** PASS — TypeCatalog slice 39/39, story-named regression anchors 3/3, new DW15 tests 9/9.
- **Tier 1 full Admin.UI project:** 824/825 PASS — single unrelated `JsonViewer_ShowsWarning_WhenJsonIsInvalid` failure pre-existing on `main` (verified by stash-and-rerun); out of DW15 scope.
- **Tier 2 / Tier 3 integration tests:** not run — DW15 is a UI-only navigation hygiene change with no contract, server, or AppHost edits.
- **Aspire/manual Issue 12 runtime validation:** BLOCKED — `aspire run` requires SDK 10.0.300 on the system path. User-local SDK is present at `%LOCALAPPDATA%\Microsoft\dotnet` but the AppHost evaluation chain still resolves machine-wide first. Same blocker as the story-context Aspire Baseline Note (2026-05-21). Manual evidence MUST be captured by an environment that satisfies the SDK pin before this story can be considered runtime-verified.

### Change Log

| Date | Version | Description | Author |
| --- | --- | --- | --- |
| 2026-05-21 | 1.3 | Applied code-review patches for bare `CounterAggregate` deep-link inference, initial-load disposal refresh subscription race, late search-after-dispose CTS safety, and refresh teardown test exception assertion. DW15 tests 9/9 PASS; TypeCatalog slice 39/39 PASS. Status moved review → done. | Codex |
| 2026-05-21 | 1.2 | Implemented DW15 narrow `_disposed` guard in `TypeCatalog.razor` (`DisposeAsync`, `UpdateUrl`, debounce continuation, refresh signal) with narrow `JSDisconnectedException`/`ObjectDisposedException` catch around `NavigateTo`. Added `Dw15TypeCatalogDisposalSafeNavigationAtddTests.cs` (6 tests, red-then-green, CounterAggregate fixture). Status moved ready-for-dev → review. Runtime validation blocked by SDK pin; bUnit slice 36/36 + story anchors 3/3 + DW15 6/6 PASS. | Claude Opus 4.7 |
| 2026-05-21 | 1.1 | Applied party-mode review fixes: stricter trace-noise decision rule, disposal/debounce suppression tests, active navigation proof, regression test anchors, and logging expectations. | Codex |
| 2026-05-21 | 1.0 | Expanded DW15 starter handoff to ready-for-dev story with disposal-safe navigation guardrails, current-code intelligence, test anchors, and validation guidance. | Codex |
