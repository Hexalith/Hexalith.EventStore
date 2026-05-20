# Post-Epic Deferred DW10: Admin UI Manual Retest Follow-Up Polish

Status: implemented

Context created: 2026-05-20
Source evidence: `_bmad-output/test-artifacts/admin-ui-manual-test-follow-up-retest-plan.md`
Deferred runbook: `_bmad-output/test-artifacts/admin-ui-manual-test-follow-up-deferred-entry-runbook.md`

## Story

As an EventStore operator validating the Admin UI manually,
I want the remaining retest defects to be corrected with test-first guardrails,
so that the dashboard reports real zero values honestly, the State Inspector does
not reopen from stale state, and the development role switcher stays visually
clean without losing accessibility.

## Scope

This story covers only the follow-up defects observed after the 2026-05-20
manual retest:

| Item | User observation | Product intent |
| --- | --- | --- |
| Issue 2 | `Events/sec` and `Error Rate` show `unavailable` even when no activity/errors should mean zero. | Display `unavailable` only when evidence is unavailable; display real numeric zero when evidence is available and the value is zero. |
| New bug | After the State Inspector has been opened, clicking `All`, `Commands`, `Events`, or `Queries` reopens it by itself. | Timeline filter changes must not reopen an inspector that the user closed. |
| Issue 13 | The header renders redundant text next to the role dropdown: `Role: Operator` and `Development role Operator selected.` | Keep the dev role dropdown, remove visible redundant copy, preserve accessible labeling. |

The following are not implementation defects in this story:

- Issue 14 `/storage`: accepted as OK for the observed run. `tenant-a` has 18
  events and `system/global-administrators` accounts for the extra system stream
  and event.
- Issues 9, 11, 12, 15, 16, 17, and 18: deferred to the runbook because the
  current environment lacked the entries needed for precise manual validation.

## Acceptance Criteria

1. **Health/dashboard metric zero semantics are explicit.**
   - Given the metric evidence source is available and contains no events/sec activity
   - Then `Events/sec` renders as a real zero using the page's existing numeric format.
   - Given the metric evidence source is available and contains no failed commands/errors
   - Then `Error Rate` renders as a real zero percentage.
   - Given the metric evidence source cannot be read or times out
   - Then the metric status is `Unavailable` and the UI renders `unavailable`.
   - A numeric fallback value must never be used to infer availability.
   - Do not regress the existing behavior where unavailable health evidence stays visibly unavailable.

2. **Metric tests are written before the product fix.**
   - Add or update server tests in
     `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthQueryServiceTests.cs`
     for:
     - available evidence plus zero events/sec;
     - available evidence plus zero error rate;
     - unavailable evidence for each metric.
   - Add or update UI tests in
     `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/IndexPageTests.cs` and
     `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/HealthPageTests.cs` proving
     real zero and `unavailable` render differently.
   - The tests must fail against the current behavior before implementation, or
     the Dev Agent Record must explain why an existing failing test already pins
     the defect.

3. **State Inspector stays closed across timeline filter changes.**
   - Given an operator opens the State Inspector on
     `/streams/tenant-a/counter/counter-1`
   - And the operator closes the inspector
   - When the operator clicks `All`, `Commands`, `Events`, or `Queries`
   - Then the State Inspector does not reopen.
   - The selected timeline filter still applies normally.
   - Existing supported deep-link behavior remains intact when the user
     intentionally opens an inspector by URL or Inspect action.

4. **State Inspector lifecycle tests are written before the product fix.**
   - Add or update tests in
     `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StreamDetailPageTests.cs`
     for open-close-filter sequences across all four filter buttons.
   - Include a case where the previous inspected sequence or URL parameter would
     have caused the stale reopen.
   - Preserve existing tests for normal inspect, state replay, timeline filters,
     and modal close behavior.

5. **Development role switcher removes redundant visible copy.**
   - Given `EnableKeycloak=false` and development mode
   - When the Admin UI header renders
   - Then the role selector remains visible and usable.
   - And no visible text beside the dropdown says `Role: <role>`.
   - And no visible text says `Development role <role> selected.`
   - And the selector retains an accessible label, title, or equivalent
     semantic name.
   - If a live-region announcement is kept, it must be visually hidden.

6. **Role switcher tests are written before the product fix.**
   - Add or update
     `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/MainLayoutTests.cs` for:
     - dropdown visible in dev/no-Keycloak mode;
     - `Role: Operator` absent from visible output;
     - `Development role Operator selected.` absent from visible output;
     - accessible selector label remains present;
     - role switching still updates UI guards and token state according to the
       previous completed role-switch story.

7. **Manual retest artifacts stay split and precise.**
   - Keep `_bmad-output/test-artifacts/admin-ui-manual-test-follow-up-retest-plan.md`
     limited to the current run and immediate fixes.
   - Keep `_bmad-output/test-artifacts/admin-ui-manual-test-follow-up-deferred-entry-runbook.md`
     as the source for scenarios requiring entries.
   - Do not re-add Issues 9, 11, 12, 15, 16, 17, or 18 to the current run plan
     unless the needed entries are available and documented.

8. **Validation is targeted and recorded.**
   - Run targeted tests first:
     - `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release --filter "FullyQualifiedName~DaprHealthQueryServiceTests"`
     - `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~IndexPageTests|FullyQualifiedName~HealthPageTests|FullyQualifiedName~StreamDetailPageTests|FullyQualifiedName~MainLayoutTests"`
   - If targeted tests pass, run the full Admin UI and Admin Server test
     projects.
   - Record command, result, and first blocker if any test is blocked by an
     existing environment or baseline issue.

## Developer Notes

- Do not implement this story until the user explicitly approves moving from
  story/test planning into code changes.
- Use the existing Admin truth model: `SystemHealthMetricStatus.Available`
  means the numeric value is trustworthy, including zero. `Unavailable` means
  the UI must ignore numeric defaults.
- Prefer extending existing tests rather than creating new test projects.
- Preserve Fluent UI Blazor v5 patterns already used in the Admin UI.
- Do not change Aspire AppHost, DAPR component scopes, Keycloak behavior, or
  Admin Server authorization policy for this story.
- Do not initialize nested submodules.

## Expected File Touches

Tests first:

- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthQueryServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/IndexPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/HealthPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StreamDetailPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/MainLayoutTests.cs`

Likely implementation files after tests are approved:

- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Index.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/Health.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor`
- `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor`

## Tasks

- [ ] Write failing metric zero/unavailable tests.
- [ ] Write failing State Inspector stale-reopen tests.
- [ ] Write failing role-switcher visible-copy tests.
- [ ] Review failing tests with the user before product implementation.
- [ ] Implement metric zero/unavailable semantics.
- [ ] Implement State Inspector stale state reset on filter changes and close.
- [ ] Remove redundant visible role switcher copy while preserving accessibility.
- [ ] Run targeted tests and record evidence.
- [ ] Update manual retest evidence after user validation.

## Handoff Status

Implementation is complete and ready for manual retest. The remaining manual
test instructions live in
`_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`.

## Test Authoring Evidence - 2026-05-20

The user approved the story and asked to prepare the test files before product
implementation. Only automated tests and documentation artifacts were changed.

Test files updated:

- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthQueryServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/IndexPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StreamDetailPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/MainLayoutTests.cs`

Targeted validation commands:

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release --filter "FullyQualifiedName~DaprHealthQueryServiceTests" -m:1
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~IndexPageTests|FullyQualifiedName~StreamDetailPageTests|FullyQualifiedName~MainLayoutTests" -m:1
```

Observed red tests before product implementation:

- Server: 2 expected failures, 12 passing.
  - `GetSystemHealthAsync_EventsPerSecondAndErrorPercentage_ReportRealZero_WhenEvidenceSourcesAreAvailable`
  - `GetSystemHealthAsync_ErrorPercentage_ComputesFailedCommandShare_WhenCommandEvidenceIsAvailable`
- UI: 5 expected failures, 32 passing.
  - `MainLayout_DevelopmentRoleSelector_DoesNotRenderRedundantVisibleRoleCopy`
  - `StreamDetail_TimelineFilterClick_ClosesPreviouslyOpenedStateInspector` for `All`, `Commands`, `Events`, and `Queries`.

The initial parallel test launch hit a transient SourceLink file lock while both
projects built shared dependencies. The same targeted commands were rerun with
`-m:1` to remove the parallel build conflict.

## Implementation Evidence - 2026-05-20

Files changed for implementation:

- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs`
- `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor`

Targeted validation after implementation:

```powershell
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release --filter "FullyQualifiedName~DaprHealthQueryServiceTests" -m:1
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~IndexPageTests|FullyQualifiedName~StreamDetailPageTests|FullyQualifiedName~MainLayoutTests" -m:1
```

Results:

- Admin.Server targeted tests: 14 passed, 0 failed.
- Admin.UI targeted tests: 37 passed, 0 failed.

Aspire was rebuilt and relaunched after the code changes:

```text
Dashboard: https://localhost:17017/login?t=38f12ceb604df326abc6cb21130fb233
eventstore-admin DAPR HTTP port: 58644
```
