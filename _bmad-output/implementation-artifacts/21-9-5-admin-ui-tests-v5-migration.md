# Story 21.9.5: Admin.UI.Tests Fluent UI v5 + bUnit v2 Migration

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

**Party Review Patches Applied (2026-04-15):**
- **[Bob] AC 1 warning clause removed.** `TreatWarningsAsErrors=true` elevates warnings to errors, so "0 errors" already covers warnings. Dropped the redundant "no new warnings" clause from AC 1 — ambiguity source eliminated.
- **[Bob] Task 0.2 hypothesis probe uses explicit git state management.** Pre-probe: `git stash push -m "21-9.5 Task 0.2 probe"`. Post-probe (cascade confirmed OR refuted): `git stash pop` to restore the baseline, then Task 4.1 applies the real full-file fix on clean tree. Memory-based revert removed.
- **[Bob] Task 7.4 verb corrected.** Entry `21-9-5-admin-ui-tests-v5-migration: ready-for-dev` already exists in sprint-status.yaml (added during story creation). Dev-story transitions `ready-for-dev → in-progress`; code-review transitions `review → done`. Task 7.4 now says "update" not "add."
- **[Amelia] AC 4 / Task 2.2 ToastCloseReason kind-check.** Before the `default!` edit on line 95, confirm `ToastCloseReason` kind via `grep -rE "public (class|record|enum|struct) ToastCloseReason" ~/.nuget/packages/microsoft.fluentui.aspnetcore.components/5.*/` — prevents CA-nullable warnings if ToastCloseReason turns out to be a class instead of a struct/enum.
- **[Amelia] Task 0.5 bUnit v2 API fallback gate.** If Task 0.5 finds NEITHER `Render(Action<ComponentParameterCollectionBuilder<T>>)` NOR a 1:1 replacement for `SetParametersAndRender` in installed `Bunit.xml`, STOP the story, file follow-up `21-9.5.1-bunit-v2-api-investigation`, close 21-9.5 with AC 6 deferred + NumericInputTests.cs:102 as a `[Fact(Skip="21-9.5.1")]`. Prevents open-ended Task 5.
- **[Amelia] Task 3.2 remaining-ConfigureAwait count assertion.** Post-fix grep `grep -cE "ConfigureAwait\(false\)" tests/Hexalith.EventStore.Admin.UI.Tests/Services/ToastServiceExtensionsTests.cs` MUST return `= 2` (the two calls inside private helper `CaptureOptionsAsync`), NOT `0`. Assertion replaces "trust the scope."
- **[Amelia] Task 5.3 v1-vs-v2 render-semantics triage.** `NumericInputTests.cs:98` calls `cut.Find("fluent-text-input").Change("100")` BEFORE line 102's re-render. If bUnit v2 `Render` replaces DOM root, the old DOM reference from Find is stale and Change() may not compose with re-render the way v1 semantics promised. Task 5.3 now walks the DOM-reference lifecycle explicitly.
- **[Amelia] Task 7.5 dynamic line lookup for deferred-work.md.** The 21-9.5 follow-up entry was at `deferred-work.md:101-102` on 2026-04-15 but append-only edits shift line numbers. Task 7.5 now uses `grep -nE "21-9\.5|21-9-5" _bmad-output/implementation-artifacts/deferred-work.md` to find the current line, NOT hardcoded 101/102.
- **[Murat] AC 8 MergedCssSmokeTests runtime triage path.** If any of the 3 merged-CSS smoke tests fails at runtime (first-ever execution), explicit decision tree: (a) test assertion wrong → update assertion + document 21-8 authoring drift; (b) CSS actually missing a class the test correctly asserts → file `21-9.5.2-css-hook-restoration` + `[Fact(Skip="21-9.5.2")]` the failing test. No undefined pass/fail scenario.
- **[Murat] Task 0.2 time-box.** Hypothesis probe + investigation capped at 15 minutes. Beyond cap → file `21-9.5.3-navmenu-parameter-api-investigation`, defer AC 7 + NavMenuTests.cs fix, close 21-9.5 with that file's 32 errors absorbed into the follow-up. Prevents Task 0.2 consuming story budget.

**Advanced Elicitation Patches Applied (2026-04-15, 5-method sweep — Pre-mortem + Red Team + FMA + Self-Consistency + Hindsight):**
- **[Pre-mortem F1] Dependency clause corrected.** 21-9 is `review`, not `done` (sprint-status.yaml:250). Dev-story MUST block if 21-9 is not `done` at start. See corrected Dependency section below + Task 0.0 gate.
- **[Pre-mortem F2] Task 0.1 tolerance band.** Baseline `86` captured 2026-04-15 on one machine with specific NuGet cache. Allow `86 ± 4` with documented drift rationale; outside tolerance → recalibrate story before proceeding.
- **[Pre-mortem F3] `21-9.5.4-toast-api-investigation` pre-filed as trigger-only.** If Task 0.4 finds neither `ToastCloseReason` nor obvious v5 ShowToastAsync return-type equivalent → file 21-9.5.4 on trigger, defer AC 4.
- **[Pre-mortem F4] src-side symmetry grep.** Task 2 adds per-site verification that `FluentTextInput` (test rename target) matches the ACTUAL v5 component used in `Pages/Backups.razor` at the asserted-on field (Tenant ID vs Description etc). Multi-line fields may map to `FluentTextArea`.
- **[Red Team A1] AC 1 `[Fact(Skip` count guard.** Story-explicit skips = only 21-9.5.1/2/3/4 follow-ups. Any other Skip attribute = dev escape hatch, AC 1 fails.
- **[Red Team A2] Task 3.2 dynamic helper count.** Capture `helper_configure_await_count_pre` before edit; assert POST == PRE (replaces hardcoded `= 2` which breaks if 21-9 merge adds a 3rd helper call).
- **[Red Team A3] AC 7 + Task 4 NavMenu.razor source guard.** `grep -E "public string Width" src/.../NavMenu.razor` must return 1 hit unchanged. Prevents "fix" that changes src Width type to int.
- **[Red Team A4] Known Pitfalls expanded.** No `#pragma warning disable xUnit1030`, no `<Compile Remove>`, no `<ExcludeFromBuild>`, no `.editorconfig` analyzer downgrades.
- **[Red Team A5] AC 11 exact output line pinned.** `Passed! - Failed: 0, Passed: 753, Skipped: 0` — verbatim match.
- **[FMA M1] Task 0.2 stash flags.** `git stash push --include-untracked --keep-index -m "21-9.5 Task 0.2 probe"` — handles staged, untracked, and bin/obj state correctly.
- **[FMA M2] Task 0.3 drift clause.** Pre-counts expected 16/2/1 but document actual if drift; no halt unless net new FluentTextField/ToastResult/SetParametersAndRender sites exceed the expected inventory.
- **[FMA M3] Task 5.3 explicit rollback before skip.** If one-fix-attempt fails → `git checkout tests/.../NumericInputTests.cs` before `[Fact(Skip="21-9.5.1...")]` — avoids leaving partial edit + skip in same commit.
- **[FMA M4] AC 10 slnx tolerance.** `= 36 attributable to 21-10 Sample.BlazorUI OR documented drift with per-project error source breakdown`. Relax from rigid 36.
- **[FMA M5] AC 8 (a) git-diff evidence requirement.** Smoke test assertion update MUST cite `git diff` snippet + 1-sentence justification referencing actual CSS class name observed in `wwwroot/css/app.css`. Prevents silent intent-change.
- **[Self-Consistency S1] ADR-21-9-5-004 added.** Documents 5 alternative migration approaches (A-E) + Task 4/5 coupling (NavMenu cascade may be same root cause as Task 5 bUnit v2 API change). If Task 0.5 fallback triggers, reassess Task 0.2 hypothesis jointly — don't treat them as independent.
- **[Hindsight H1] Task 0.10 added.** Pre-verify MergedCssSmokeTests class assertions against actual `wwwroot/css/app.css` at story-start (5-min grep). Neutralizes AC 8 triage-tree-runtime-burn risk BEFORE dev reaches that gate.
- **[Hindsight H2] Review Checklist added at top-of-file.** 10-line reviewer quick-gate table (same pattern as 21-9 H2). Review can verify 21-9.5 in 2 min instead of 10.

## 🚦 Review Checklist (Hindsight H2 — reviewer quick-gate, 2-min check)

Code-review workflow picks up this story. To avoid skim-past of review-injected gates in a 500+-line story, verify these 10 items in Completion Notes BEFORE any deeper review:

| # | Check | Where to find evidence | Expected |
|---|---|---|---|
| 1 | Task 0.0 dependency gate: 21-9 status was `done` when story started | Completion Notes + sprint-status.yaml git log | explicit timestamp |
| 2 | Task 0.1 `test_errors_pre` recorded within `86 ± 4` tolerance | Completion Notes | integer `82-90` with drift note if outside |
| 3 | Task 0.2 probe used `git stash push --include-untracked --keep-index` + explicit start/end timestamps | Completion Notes | stash SHA or "no changes to save" message |
| 4 | Task 0.4 ToastCloseReason kind recorded + (if missing) 21-9.5.4 filed | Completion Notes | `toast_close_reason_kind: <kind>` OR follow-up story key |
| 5 | Task 0.10 MergedCssSmokeTests pre-verification PASS (or deferred to 21-9.5.2 pre-emptively) | Completion Notes | per-test class-name match verdict |
| 6 | Task 3.2 `helper_configure_await_count_pre` + `_post` recorded + equal | Completion Notes | two integers, must match |
| 7 | AC 7 NavMenu.razor source guard: `public string Width` still at 1 hit | Completion Notes | `grep` output verbatim |
| 8 | AC 1 `[Fact(Skip=...)]` count matches baseline + only 21-9.5.x follow-ups cited | Completion Notes | per-skip reason table |
| 9 | AC 8 smoke test outcomes documented per-test (PASS / FAIL→updated / FAIL→skipped-to-21-9.5.2) with git-diff evidence if (a) | Completion Notes + code review viewable diff | 3 verdicts, diff snippet if (a) |
| 10 | AC 11 exact output line pasted verbatim | Completion Notes | `Passed! - Failed: 0, Passed: 753, Skipped: 0` |

Any missing row → reject the story for this review cycle, return to dev.

**Dependency:** Story 21-9 (DataGrid + remaining enum renames) must reach **status: done** before 21-9.5 dev-story starts. As of 2026-04-15 21-9 is `review` (code-review pending, browser gates Tasks 8/9 also pending). **DO NOT start 21-9.5 dev-story until sprint-status.yaml shows `21-9-datagrid-remaining-enum-renames: done`.** If dev-story fires against a branch where 21-9 hasn't merged to main (or status is not `done`), Task 0.0 gate halts — the Admin.UI errors cascade will inflate the 86 baseline to 168 (82 cascade + 86 own) and the whole story's assertions misfire. See Task 0.0 below for the hard gate.

**Unblocks:**
- **Admin.UI.Tests compile to 0 errors.** 21-9 Task 7.3 (AC 12) was DEFERRED here when the 86 test-project errors exceeded 21-9's 15-min/5-line scope cap (Pre-mortem F6). This story is the explicit follow-up referenced in 21-9 Completion Notes and `deferred-work.md` (2026-04-15).
- **Admin.UI bUnit suite runs green.** 21-9 Task 7.5 (AC 14) — the `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release --no-build` command pinned by Red Team A6 — runs for the first time since Epic 21 started. Includes the 3 merged-CSS smoke tests authored in 21-8 Task 6.5 (`MergedCssSmokeTests.cs` — `ProjectionStatusBadge_Renders_WithExpectedClassAttributes`, `StateDiffViewer_Renders_WithDiffFieldClasses`, `JsonViewer_Renders_SyntaxClasses`) which have been WRITTEN but NEVER EXECUTED.
- **Slnx residual error count drops further.** Post-21-9 slnx residual is 36 (Sample.BlazorUI, 21-10 scope) + 86 (Admin.UI.Tests, this story). After 21-9.5, only 36 remain (attributable to 21-10). AC 11 from 21-9 was reinterpreted to reference these follow-ups — this story closes the Admin.UI.Tests leg.
- **Epic 21 retrospective.** `epic-21-retrospective: optional` becomes eligible once 21-9, 21-9.5, and 21-10 are all `done`.

**Blocks:** Epic 21 retrospective (blocked until 21-10 + 21-9.5 both reach `done`).

## Story

As a developer finishing the Fluent UI Blazor v4 → v5 migration across the Admin.UI test project,
I want every v4 symbol and bUnit v1 API call in `tests/Hexalith.EventStore.Admin.UI.Tests/` updated to its v5 / bUnit v2 equivalent (`FluentTextField` → `FluentTextInput`, `ToastResult` → `ToastCloseReason`, `ConfigureAwait(false)` removed from `[Fact]`/`[Theory]` method bodies per xUnit1030, bUnit `SetParametersAndRender` / `parameters.Add` API shifts addressed),
so that the Admin.UI.Tests project compiles with 0 errors, the full bUnit suite (including the 3 merged-CSS smoke tests deferred from 21-8 Task 6.5) executes green, and Epic 21 code work closes cleanly.

## Acceptance Criteria

### Compile gate (HARD — primary blocker)

1. **Given** `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release 2>&1 | grep -cE "error "` currently returns **86** (verified 2026-04-15),
   **When** this story completes,
   **Then** the same command returns **0**.

   **Note:** `TreatWarningsAsErrors=true` (from `Directory.Build.props`) elevates every warning to an error, so the "0 errors" assertion already covers "0 warnings." No separate warning-count clause needed (Bob party-review patch — redundancy removed).

   **`[Fact(Skip=...)]` count guard (Red Team A1 — prevents dev-escape-hatch by skipping failing tests to achieve "0 errors"):** After this story, `grep -rnE "\[Fact\(Skip\s*=" tests/Hexalith.EventStore.Admin.UI.Tests --include='*.cs'` MUST return ONLY skips whose message cites one of the explicitly-permitted follow-up story keys: `21-9.5.1-bunit-v2-api-investigation`, `21-9.5.2-css-hook-restoration`, `21-9.5.3-navmenu-parameter-api-investigation`, `21-9.5.4-toast-api-investigation`. Any other `[Fact(Skip=...)]` added during this story (including "TODO", "flaky", "investigate", empty-string, or unrelated story keys) fails AC 1. Baseline count of `[Fact(Skip=...)]` pre-21-9.5 MUST be captured in Task 0.1 as `skip_count_pre: <N>`; post-count `skip_count_post: <M>` with `M - N` equal to the number of explicitly-permitted follow-up deferrals that actually triggered (0 if none, up to 4 if all four follow-ups were needed).

### `FluentTextField` → `FluentTextInput` rename (CS0246 × 16 raw / 8 unique sites)

2. **Given** `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/BackupsPageTests.cs` references the v4 type `FluentTextField` at lines **192 (×2), 193, 225 (×2), 226, 458 (×2), 489 (×2)** — 10 unique references,
   **When** migration completes,
   **Then** every `FluentTextField` is renamed to `FluentTextInput` (v5 equivalent, confirmed via `grep -rnE "FluentTextInput" src/Hexalith.EventStore.Admin.UI` — already used in production code at `Pages/Backups.razor:55, 214, 218, 356, 414` etc.),
   **And** the grep `grep -rn "FluentTextField" tests/Hexalith.EventStore.Admin.UI.Tests --include='*.cs'` returns **0 hits**,
   **And** the matching `FindComponents<FluentTextField>()` / `IRenderedComponent<FluentTextField>` generic parameters are also updated to `FluentTextInput`.

3. **Given** `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CompactionPageTests.cs` references `FluentTextField` at lines **197 (×2), 198, 230 (×2), 231** — 6 unique references,
   **When** migration completes,
   **Then** every `FluentTextField` is renamed to `FluentTextInput`,
   **And** AC 2's grep over the whole test project continues to return **0 hits** (coverage is cross-file).

### `ToastResult` → `ToastCloseReason` rename (CS0246 × 4 raw / 2 unique + CS1503 × 4 raw)

4. **Given** `tests/Hexalith.EventStore.Admin.UI.Tests/Services/ToastServiceExtensionsTests.cs:77` writes `Task.FromException<ToastResult>(...)` and `:95` writes `Task.FromResult<ToastResult>(default!)`, but v5 `IToastService.ShowToastAsync(Action<ToastOptions>)` returns `Task<ToastCloseReason>` (`ToastResult` was removed in v5),
   **When** migration completes,
   **Then** both generic type arguments are updated to `ToastCloseReason` (`Task.FromException<ToastCloseReason>`, `Task.FromResult<ToastCloseReason>(default!)`),
   **And** the grep `grep -rn "ToastResult" tests/Hexalith.EventStore.Admin.UI.Tests --include='*.cs'` returns **0 hits** (excluding `.artifacts/` cache directories),
   **And** the associated CS1503 "`Task<ToastResult>` → `Func<CallInfo, Task<ToastCloseReason>>`" conversion errors on lines 77 and 95 resolve.

### xUnit1030 `ConfigureAwait(false)` removal in `[Fact]`/`[Theory]` methods (xUnit1030 × 12 raw / 6 unique sites)

5. **Given** `tests/Hexalith.EventStore.Admin.UI.Tests/Services/ToastServiceExtensionsTests.cs` has 6 `[Fact]` methods that call `.ConfigureAwait(false)` inside the test body at lines **20, 31, 42, 53, 64, 79** (violating xUnit1030 analyzer rule — "Test methods should not call ConfigureAwait(false)"),
   **When** migration completes,
   **Then** `.ConfigureAwait(false)` is removed from every `await` statement inside a method decorated with `[Fact]` or `[Theory]` in the file (the sync-context-bypass concern is inappropriate for xUnit test bodies — xUnit's own test runner controls continuation context),
   **And** `.ConfigureAwait(false)` calls inside **private helper methods** (e.g. `CaptureOptionsAsync` at line 82+) are **preserved** — xUnit1030 only fires on test methods,
   **And** the grep `grep -cE "xUnit1030" <(dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release 2>&1)` returns **0**,
   **And** the two `await mockToast.Received(1).ShowToastAsync(...).ConfigureAwait(false)` on lines 99–100 (inside the private `CaptureOptionsAsync` helper, NOT a test method) remain untouched.

   **Scope note:** `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs` and `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CommandsPageTests.cs` also contain `ConfigureAwait(false)` (per `grep -rn` 2026-04-15) but produce **0 xUnit1030 errors** — those calls are inside non-test helper methods. Do NOT touch them.

### bUnit v2 `SetParametersAndRender` API change (CS1061 × 2 raw / 1 unique site)

6. **Given** `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/NumericInputTests.cs:102` calls `cut.SetParametersAndRender(parameters => parameters.Add(p => p.Value, 100L))` but bUnit v2's `IRenderedComponent<T>` no longer exposes `SetParametersAndRender` as an instance method (it was moved to an extension method in `Bunit` namespace, OR renamed to `Render`),
   **When** migration completes,
   **Then** the call is rewritten to the v2 API — either:
   - `cut.Render(parameters => parameters.Add(p => p.Value, 100L))` (v2 in-place re-render with new parameters), OR
   - the equivalent per bUnit v2 migration guide (consult `https://bunit.dev/docs/migrating-to-bunit-v2` or the Bunit package XML docs at `~/.nuget/packages/bunit/<v2.x>/lib/.../Bunit.xml` for the canonical replacement),
   **And** the test's intent is preserved: the component is re-rendered with a new `Value` parameter, and `cut.Markup` after the call reflects the updated value,
   **And** no other call sites of `SetParametersAndRender` exist — `grep -rn "SetParametersAndRender" tests/Hexalith.EventStore.Admin.UI.Tests --include='*.cs'` returns **0 hits** post-migration.

### `NavMenuTests.cs` bUnit parameter API / type mismatch (CS0029 × 8 raw + CS1662 × 8 raw + CS1929 × 8 raw + CS1061 × 8 raw = 32 raw / 4 unique sites)

7. **Given** `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/NavMenuTests.cs` calls `Render<NavMenu>(parameters => parameters.Add(p => p.Width, 220).Add(p => p.UserRole, AdminRole.Admin))` at 4 test methods (lines **13–15, 23–26, 63–65, 83–85** — `NavMenu_RendersWithoutException`, `NavMenu_ContainsExpectedNavigationLinks`, `NavMenu_SettingsHiddenForNonAdminRole`, and one more),
   **And** `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor:62` declares `public string Width { get; set; } = "220px"` (type `string`, default `"220px"`) and `:65` declares `public AdminRole UserRole { get; set; } = AdminRole.ReadOnly`,
   **When** migration completes,
   **Then** each `parameters.Add(p => p.Width, 220)` is rewritten to `parameters.Add(p => p.Width, "220px")` — the int literal `220` is replaced with the correctly-typed string literal `"220px"` (matching the parameter's default value to preserve test intent),
   **And** the CS0029/CS1662/CS1929 compiler-overload-resolution cascade (triggered by the int→string mismatch confusing bUnit's `Add` overload resolver) disappears,
   **And** the CS1061 `'Type' ne contient pas de définition pour 'UserRole'` errors (which are **downstream cascades** of the same overload-resolution failure — once `Width` type-checks, bUnit picks the correct `Add` overload and `UserRole` resolves) also disappear,
   **And** the 4 tests in the file then either pass OR produce a runtime assertion failure (which, if surfaced, is addressed in Task 4 — NOT in this AC; this AC only covers compile-green).

   **Hypothesis verification (Task 0.2):** Before editing, the developer MUST verify the hypothesis that `Width: string` vs `int 220` is the root cause by running `dotnet build` after a one-line change at line 14 only (from `220` to `"220px"`) on a single test method and observing whether lines 24+ also recover (confirming cascade). If the cascade does NOT clear → the root cause is a bUnit v2 breaking API change in `parameters.Add`, not the type mismatch; pivot to the bUnit v2 API surface fix and document the investigation.

   **Source-side guard (Red Team A3 — prevents "compile-green the wrong way" by changing `NavMenu.Width` src type):** `grep -nE "public string Width" src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` MUST return exactly 1 hit, unchanged from baseline, post-story. If the dev "fixes" by editing src (changing `Width: string` → `Width: int` to accept int `220` natively), story closure is rejected. Task 4 is test-side fix ONLY. Paste the grep output verbatim in Completion Notes as `navmenu_width_src_guard: <grep output>`.

   **Time-box (Murat party-review patch):** Task 0.2 probe + any bUnit v2 API investigation it triggers is capped at **15 minutes total wall-clock**. If the 15-min cap is hit without a confirmed fix approach → STOP, file follow-up story `21-9.5.3-navmenu-parameter-api-investigation`, defer AC 7 + Task 4 + NavMenuTests.cs entirely to that follow-up, close 21-9.5 with NavMenuTests's 32 errors absorbed into 21-9.5.3. Record the 15-min cap decision in Completion Notes with timestamps.

### Run Admin.UI.Tests bUnit suite green

8. **Given** AC 1 green (compile to 0 errors),
   **When** `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release --no-build` runs (the exact Red Team A6 no-filter command pinned by 21-9 for this story),
   **Then** **all** Admin.UI bUnit tests pass,
   **And** the 3 merged-CSS smoke tests from 21-8 Task 6.5 (`MergedCssSmokeTests.cs` — `ProjectionStatusBadge_Renders_WithExpectedClassAttributes`, `StateDiffViewer_Renders_WithDiffFieldClasses`, `JsonViewer_Renders_SyntaxClasses`) — **running for the first time** — are **included in the green result**,
   **And** the 21-0 baseline bUnit smoke tests (`StatCardTests`, `EmptyStateTests`, `NavMenuTests` — note: NavMenu tests rely on AC 7 compile-green first; if any are runtime-failing after AC 7's type fix, triage per Task 4 per-test-failure protocol),
   **And** the test-run output line `Passed! - Failed: 0, Passed: <N>, Skipped: 0` is pasted verbatim into Completion Notes (with the exact `<N>` value recorded).

   **MergedCssSmokeTests runtime triage (Murat party-review patch — first-ever execution, so failure mode MUST be pre-specified):** If any of the 3 smoke tests fails at runtime, decide per-test using this decision tree:
   - (a) **Test assertion is wrong** (e.g. asserts `ShouldContain("projection-status-active")` but post-21-8 merged `wwwroot/css/app.css` uses `status-active` without the `projection-` prefix) → update the ASSERTION to match the actual merged CSS class; document the drift in Completion Notes as `[21-8 authoring gap] MergedCssSmokeTests.<test name> asserted <wrong>; updated to <right>. 21-8 wrote the test against intended-but-not-landed class names.` **Evidence required (FMA M5):** paste the `git diff tests/Hexalith.EventStore.Admin.UI.Tests/Components/MergedCssSmokeTests.cs` snippet for the assertion change verbatim AND a 1-sentence justification citing the actual CSS class name observed via `grep -nE "<actual-class-name>" src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css`. No-diff-no-justification = path (a) deferred to path (b).
   - (b) **Test assertion is correct, CSS is missing a class hook** (e.g. test asserts `ShouldContain("diff-field-added")` and merged CSS genuinely doesn't declare that class anywhere) → file follow-up story `21-9.5.2-css-hook-restoration`, mark the failing test `[Fact(Skip = "21-9.5.2")]` with the exact skip message citing the follow-up story key, keep the other 2 smoke tests green, close 21-9.5 with 1 skipped test documented in Completion Notes.
   - (c) **Indeterminate** (can't tell whether assertion or CSS is wrong within 15 min of triage) → default to path (b) — file 21-9.5.2 and skip. Never leave an undefined-mode runtime failure.

   Completion Notes MUST document per-smoke-test verdict: `<test name>: PASS | FAIL→(a) updated | FAIL→(b) skipped to 21-9.5.2`. "All 3 pass" is fine; silently ignoring a failure is not.

   **Pre-emptive verification note (Hindsight H1):** Task 0.10 runs the smoke-test assertion-vs-actual-CSS grep at story-START (5-min check), not at AC 8 runtime gate. If Task 0.10 already flagged drift, that drift is addressed via path (a) with evidence captured upfront and the runtime triage is informational. Reduces AC 8 triage burn risk from 2+ hours to near-zero if Task 0.10 is honored.

### Grep-for-zero gates

9. **Given** story completion,
   **When** the grep-for-zero batch runs in `tests/Hexalith.EventStore.Admin.UI.Tests/` (excluding `.artifacts/`, `bin/`, `obj/` directories),
   **Then** all passes return **0 hits**:
   - **Pass A (v4 FluentTextField):** `grep -rn "FluentTextField" tests/Hexalith.EventStore.Admin.UI.Tests --include='*.cs' --include='*.razor'`
   - **Pass B (v4 ToastResult):** `grep -rn "ToastResult" tests/Hexalith.EventStore.Admin.UI.Tests --include='*.cs' --include='*.razor'`
   - **Pass C (v1 bUnit SetParametersAndRender):** `grep -rn "SetParametersAndRender" tests/Hexalith.EventStore.Admin.UI.Tests --include='*.cs'`

   Paste the raw grep output (including "0 matches" or empty-line) verbatim in Completion Notes — do not abbreviate to "Pass X: ✓" (Lesson 5-D pattern from 21-8/21-9).

### Slnx closure

10. **Given** AC 1 green,
    **When** `dotnet build Hexalith.EventStore.slnx --configuration Release 2>&1 | grep -cE "error "` runs,
    **Then** the count drops from the post-21-9 raw baseline of **122** (36 Sample.BlazorUI + 86 Admin.UI.Tests) to **`= 36` attributable to `samples/Hexalith.EventStore.Sample.BlazorUI/` (21-10 scope) OR a different count with documented per-project error source breakdown** (FMA M4 — tolerance: another slnx project may have regressed in parallel merge, 21-9.5 cannot be blocked by state outside its scope),
    **And** the residual error inventory is captured in Completion Notes via `dotnet build Hexalith.EventStore.slnx --configuration Release 2>&1 | grep -E "error " | grep -oE "(src|samples|tests)[\\/][^(\\]]+" | sort | uniq -c | sort -rn` — expected: all hits under `samples/Hexalith.EventStore.Sample.BlazorUI/`. If any hit appears under `src/` or `tests/Hexalith.EventStore.Admin.UI.Tests/`, that's a direct 21-9.5 regression and story closure is rejected.

### Non-UI tests remain green

11. **Given** story completion,
    **When** `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ tests/Hexalith.EventStore.Client.Tests/ tests/Hexalith.EventStore.Sample.Tests/ tests/Hexalith.EventStore.Testing.Tests/ tests/Hexalith.EventStore.SignalR.Tests/` runs,
    **Then** all tests pass with **753/753** green (match the 21-8/21-9 baseline — this story does not touch non-UI projects),
    **And** the exact per-project summary lines are pasted verbatim in Completion Notes (Red Team A5 — prevents "expected 753" being fuzzed by skip counts). Expected shape for each of the 5 projects, combined:
    ```
    Passed! - Failed: 0, Passed: 271, Skipped: 0  (Contracts.Tests)
    Passed! - Failed: 0, Passed: 321, Skipped: 0  (Client.Tests)
    Passed! - Failed: 0, Passed: 62,  Skipped: 0  (Sample.Tests)
    Passed! - Failed: 0, Passed: 67,  Skipped: 0  (Testing.Tests)
    Passed! - Failed: 0, Passed: 32,  Skipped: 0  (SignalR.Tests)
    ```
    Any `Skipped: N>0` line → AC 11 fails (non-UI projects should have zero skips; a skip indicates unintended 21-9.5 side-effect). Any `Failed: N>0` → AC 11 fails unconditionally.

## Tasks / Subtasks

- [x] Task 0: Pre-flight — confirm scope, capture baselines (AC: 1, 2, 3, 4, 5, 6, 7, 9)
  - [ ] 0.0: **Dependency gate (Pre-mortem F1 — HARD BLOCKER).** Read `_bmad-output/implementation-artifacts/sprint-status.yaml` line matching `21-9-datagrid-remaining-enum-renames:`. Assert the status is **exactly `done`**. If status is `review`, `in-progress`, or anything else → HALT immediately, post in Completion Notes `dependency_gate: FAIL — 21-9 status is <status>, expected done` and do not proceed with any other Task. Wait for 21-9 to merge to main + code-review workflow to transition status to `done`. Record success as `dependency_gate: PASS — 21-9 status=done at <timestamp>`. This prevents the entire story from misfiring against a 168-error baseline (82 21-9 cascade + 86 own) instead of the intended 86.
  - [ ] 0.1: Record PRE-21-9.5 error count: `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release 2>&1 | grep -cE "error "` → expect **`86 ± 4` tolerance band** (Pre-mortem F2 — NuGet cache drift, SDK patch shifts, xUnit-analyzer minor-version bumps can flex the count). Document in Completion Notes as `test_errors_pre: <N>` with N ∈ [82, 90]. If N is outside [82, 90] → STOP, re-run all 3 grep-for-zero passes from Task 0.3 to check if the source inventory drifted (e.g. another story added new FluentTextField references), update the expected per-error-code breakdown in the story, and only then proceed. Per-error-code breakdown: `dotnet build ... 2>&1 | grep -E "error " | awk -F'error ' '{print $2}' | awk -F':' '{print $1}' | sort | uniq -c | sort -rn` → expected: `36 CS0246, 12 xUnit1030, 10 CS1061, 8 CS1929, 8 CS1662, 8 CS0029, 4 CS1503` (each ±2 tolerance). Also capture `skip_count_pre: <M>` via `grep -rcE "\[Fact\(Skip\s*=" tests/Hexalith.EventStore.Admin.UI.Tests --include='*.cs' | awk -F: '{sum+=$2}END{print sum}'` — baseline for the AC 1 skip-count guard.
  - [ ] 0.2: **Hypothesis-verify NavMenuTests root cause (AC 7) — explicit git state management + 15-min time-box.**

    **Pre-probe state isolation (Bob party-review patch + FMA M1 stash-flags refinement):** Before touching `NavMenuTests.cs`, run `git stash push --include-untracked --keep-index -m "21-9.5 Task 0.2 probe isolation"` to park any uncommitted work including untracked files (bin/obj, editor scratch, .artifacts/ui-test-obj) while preserving the index state. Without `--include-untracked`, untracked changes persist and confuse the post-probe revert. Without `--keep-index`, staged work vanishes into the stash and has to be unstashed + re-staged. Record the resulting stash hash in Completion Notes as `probe_stash_ref: stash@{0} <SHA>` OR `probe_stash_ref: no local changes to save` (stash push reports this message on clean tree and exits 0 — acceptable, do NOT treat as error). If stash push FAILS for any other reason (non-zero exit, error message) → HALT Task 0.2 entirely, do not touch NavMenuTests.cs, report the failure in Completion Notes, investigate repo state before retrying.

    **Probe (≤15-min time-box, Murat party-review patch — start timer NOW, record start timestamp in Completion Notes):**
    1. Edit ONLY `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/NavMenuTests.cs:14` — change `220` to `"220px"`, save, rebuild.
    2. Observe error count: if lines 23–85's CS1929/CS0029/CS1662/CS1061 cascade clears on the single edited method → hypothesis confirmed (int→string overload failure cascades through `.Add` chain). Record `cascade_cleared: true, elapsed_min: <N>` in Completion Notes. Then `git checkout tests/Hexalith.EventStore.Admin.UI.Tests/Layout/NavMenuTests.cs` to revert the probe, `git stash pop` to restore pre-probe work, and proceed to Task 4 with the full-file fix on a clean tree.
    3. If cascade persists → the root cause is a bUnit v2 breaking API change. Investigate via `~/.nuget/packages/bunit.*/lib/**/Bunit.xml` for the current `ComponentParameterCollectionBuilder<T>.Add` overload set (may require `Render<NavMenu>(new ComponentParameter[] { ... })` or a new parameter-builder signature). Record new error signature in Completion Notes.

    **15-min cap enforcement (Murat party-review patch):** If total elapsed wall-clock for Task 0.2 exceeds 15 minutes without a confirmed fix approach → STOP, `git checkout tests/Hexalith.EventStore.Admin.UI.Tests/Layout/NavMenuTests.cs` to revert, `git stash pop` to restore, file follow-up story `21-9.5.3-navmenu-parameter-api-investigation`, defer AC 7 + Task 4 + NavMenuTests.cs entirely. Absorbed error count (32) is documented in 21-9.5.3's scope. Record cap-hit timestamps in Completion Notes as `probe_started: HH:MM, probe_abandoned: HH:MM, elapsed: 15m+, follow-up: 21-9.5.3`.

    **Post-probe invariant:** Whether hypothesis-confirmed, hypothesis-refuted, or time-boxed-out — the repo state at end of Task 0.2 MUST be identical to start-of-Task-0.2 state (verified by `git status` showing only stash-pop restored changes, no `NavMenuTests.cs` modifications). Task 4.1 is where the real fix lands on a clean tree.
  - [ ] 0.3: Run all 3 grep-for-zero passes (AC 9) against the current tree to establish PRE counts. Expected:
    - Pass A (`FluentTextField`): `10 (Backups) + 6 (Compaction) = 16` hits.
    - Pass B (`ToastResult`): `2` hits in `Services/ToastServiceExtensionsTests.cs`.
    - Pass C (`SetParametersAndRender`): `1` hit in `Components/Shared/NumericInputTests.cs:102`.
    Post-migration: all 3 passes return 0.

    **Drift clause (FMA M2):** If PRE counts differ from expected (16/2/1) — e.g. Pass A returns 14 or 18, or Pass C returns 0 hits indicating the call was already migrated by a parallel branch merge — document actual counts in Completion Notes as `pass_a_pre: <N>, pass_b_pre: <M>, pass_c_pre: <K>`, update the per-file expected inventory (Task 1.1/1.2/2.1-2.2/5.1 file line numbers) to match the observed state, and proceed. Do NOT halt on drift alone — ONLY halt if NEW categories appear (e.g. a new `FluentTextField` reference in a file other than Backups/Compaction, which would indicate scope expansion beyond this story's plan). Scope expansion → file `21-9.5.5-test-project-scope-expansion` and re-plan.
  - [ ] 0.4: Confirm v5 replacement types exist: `grep -rE "FluentTextInput|ToastCloseReason" ~/.nuget/packages/microsoft.fluentui.aspnetcore.components/5.*/ 2>/dev/null | head -5` → must find references.

    **Pre-filed fallback stories (Pre-mortem F3 — trigger-only, do not create up-front):**
    - If `FluentTextInput` not found in v5 package → STOP, file `21-9.5.6-fluent-text-input-investigation` (scope: find actual v5 rename target via Fluent UI MCP `get_component_migration FluentTextField`). DEFER AC 2, AC 3, Task 1 entirely. This is catastrophic enough to re-plan the story.
    - If `ToastCloseReason` not found in v5 package → STOP, file `21-9.5.4-toast-api-investigation` (scope: consult Fluent UI MCP `get_component_migration FluentToast` for actual v5 `ShowToastAsync` return type — might be `ToastResult` preserved, `ToastCompletionReason`, `ToastDismissReason`, or a union type). DEFER AC 4 + Task 2 in this story. Skip `[Fact]` methods in ToastServiceExtensionsTests.cs that reference ToastResult with `[Fact(Skip = "21-9.5.4-toast-api-investigation")]` on lines 71-80 + 89-100 (the two tests that hit the symbol). Leave AC 5 (ConfigureAwait removal) in scope — it is independent of the type-rename.
    - If BOTH types found (happy path) → proceed to Task 0.5. Record in Completion Notes as `v5_types_found: FluentTextInput=yes, ToastCloseReason=yes`.
  - [ ] 0.5: Confirm bUnit v2 API shape: read the installed bUnit package's XML docs — `ls ~/.nuget/packages/bunit.*/lib/ 2>/dev/null` to find version, then `grep -E "SetParametersAndRender|Render" ~/.nuget/packages/bunit.*/lib/**/Bunit.xml | head -20`. Confirm whether `Render(parameters => ...)` is the v2 replacement for `SetParametersAndRender` OR another API. Document the canonical v2 replacement in Completion Notes before editing NumericInputTests.

    **Fallback gate (Amelia party-review patch — prevents open-ended Task 5):** If the grep finds NEITHER `Render(Action<ComponentParameterCollectionBuilder<T>>)` NOR a clear 1:1 replacement for `SetParametersAndRender` (e.g. bUnit v2 shipped a genuinely different re-render API that needs investigation + test rewrite, OR the installed package still has `SetParametersAndRender` but Admin.UI.Tests is calling a deprecated overload removed in a minor version): STOP this story's Task 5 path, file follow-up story `21-9.5.1-bunit-v2-api-investigation`, mark `NumericInputTests.cs:102` as `[Fact(Skip = "21-9.5.1-bunit-v2-api-investigation")]` to let the rest of the suite compile, defer AC 6. Document the observed bUnit API surface in Completion Notes with exact XML doc snippets. Do NOT guess at Task 5 — deferral is cheap, a wrong guess cascades into bUnit upgrade scope.
  - [ ] 0.6: Confirm no other test files reference the renamed symbols. Run:
    - `grep -rn "FluentTextField" tests/ --include='*.cs' --include='*.razor'` — expect hits ONLY in Admin.UI.Tests/Pages/{Backups,Compaction}PageTests.cs.
    - `grep -rn "ToastResult" tests/ --include='*.cs' --include='*.razor'` — expect hits ONLY in Admin.UI.Tests/Services/ToastServiceExtensionsTests.cs.
    If hits appear in other test projects (Contracts, Client, Sample, SignalR, Testing, IntegrationTests, Server.Tests) → expand this story's scope and document; those projects were previously green, so this would reveal latent v5 issues elsewhere.

  - [ ] 0.10: **Pre-verify MergedCssSmokeTests class assertions vs. actual merged `app.css` (Hindsight H1 — neutralizes AC 8 triage-tree runtime burn BEFORE dev reaches that gate).**

    The 3 smoke tests (`ProjectionStatusBadge_Renders_WithExpectedClassAttributes`, `StateDiffViewer_Renders_WithDiffFieldClasses`, `JsonViewer_Renders_SyntaxClasses`) were authored in 21-8 Task 6.5 against intended merged-CSS class names. They have NEVER been executed. At story-start (now, not at AC 8 runtime), extract each test's asserted class names and grep `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` for each:

    1. Read `tests/Hexalith.EventStore.Admin.UI.Tests/Components/MergedCssSmokeTests.cs` fully.
    2. For each `ShouldContain("<class-name>")` or `FindAll(".<class-name>")` or equivalent assertion, extract the literal class name(s) and record as `<test-method>: asserts <class-list>`.
    3. For each extracted class name, run `grep -nE "\.<class-name>\b|class=[\"'][^\"']*\b<class-name>\b" src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` — expect ≥1 hit per class (either as a CSS selector `.<name>` or as a merged scope block that declares `class="<name>"`).
    4. Record per-test verdict in Completion Notes:
       - `<test-method>: MATCH — all N asserted classes found in app.css lines <line-list>` → runtime pass expected at AC 8, proceed confidently.
       - `<test-method>: DRIFT — <N-asserted>/<M-found>, missing classes: <list>` → AC 8 will likely hit triage path (a) or (b). Pre-handle NOW: if the missing class is an obvious 21-8 authoring typo that should be renamed in the test, apply path (a) + git-diff evidence AT STORY-START (before Task 1); if the missing class is a real CSS hook gap (21-8 forgot to include), file `21-9.5.2-css-hook-restoration` + `[Fact(Skip="21-9.5.2")]` NOW.

    **Budget:** 5 minutes wall-clock. If Task 0.10 drags beyond 5 min, accept the unverified state and defer full verdict to AC 8 runtime triage. The goal of 0.10 is to FRONT-LOAD triage risk, not create a new time sink.

    **Completion Notes MUST contain one of:** `smoke_preverify: ALL MATCH` (happy path) OR `smoke_preverify: PATH_A_APPLIED (<tests>)` OR `smoke_preverify: PATH_B_APPLIED (21-9.5.2 filed, <tests skipped>)` OR `smoke_preverify: BUDGET_HIT — deferred to AC 8 runtime`.

- [x] Task 1: Rename `FluentTextField` → `FluentTextInput` in Backups + Compaction page tests (AC: 2, 3, 9 Pass A)
  - [ ] 1.0: **src-side symmetry pre-check (Pre-mortem F4 — prevents wrong-target rename).** For each Backups/Compaction test file FluentTextField reference site, identify which src-side `.razor` field the test targets (usually by grepping the test's `Markup.Contains("<label>")` or `FindComponents<FluentTextField>().First(f => ...)` filter). Then verify the src-side `.razor` line actually uses `FluentTextInput` (not `FluentTextArea`, `FluentNumberField`, `FluentSelect`, or another v5 component):
    - **BackupsPageTests.cs:** test line 192-193 filters on `"Tenant ID"` — grep `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor` for the Tenant ID input's actual v5 component tag. Expected: `FluentTextInput`. If it's anything else, AC 2 rename target is wrong; update the test to use the matching v5 component name instead of blind `FluentTextInput`.
    - Repeat per occurrence (lines 225, 226, 458, 489 in Backups; 197, 198, 230, 231 in Compaction).
    - Record symmetry verdict per site in Completion Notes as `<test-file>:<line> → <v5-component-tag>: <MATCH|DRIFT>`. Any DRIFT → adjust the rename target on that site before executing Task 1.1/1.2.

    If the pre-check reveals that tests target a mix of `FluentTextInput` + `FluentTextArea` + other, Task 1 is not a blind replace_all — it's a per-site Edit with site-specific targets. Document this in the per-file verdict from 1.1/1.2.
  - [ ] 1.1: Edit `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/BackupsPageTests.cs` — use the Edit tool per site (NOT `sed -i` per 21-9 Amelia review finding: Windows Git Bash sed corrupts CRLF + can emit BOM on `.editorconfig`-CRLF files). `replace_all: true` acceptable **ONLY IF Task 1.0 symmetry check confirmed all 10 sites map uniformly to `FluentTextInput`**. Otherwise, per-site context-bound Edits with site-specific target. 10 unique references (lines 192, 193, 225, 226, 458, 489 per Task 0.3 grep).
  - [ ] 1.2: Edit `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CompactionPageTests.cs` — same approach, 6 unique references (lines 197, 198, 230, 231). Same Task 1.0 symmetry gate applies.
  - [ ] 1.3: Verify Pass A grep returns 0 hits. Paste verbatim into Completion Notes.
  - [ ] 1.4: `git diff --check tests/Hexalith.EventStore.Admin.UI.Tests/Pages/` → expect 0 whitespace errors.

- [x] Task 2: Rename `ToastResult` → `ToastCloseReason` in ToastServiceExtensionsTests (AC: 4, 9 Pass B)
  - [ ] 2.0: **ToastCloseReason kind-check (Amelia party-review patch — prevents CA-nullable warning on `default!`).** Run `grep -rE "public (class|record|enum|struct) ToastCloseReason" ~/.nuget/packages/microsoft.fluentui.aspnetcore.components/5.*/ 2>/dev/null | head -3`. Record the kind in Completion Notes as `toast_close_reason_kind: <class|record|enum|struct>`. Implication for line 95's `default!`:
    - **enum or struct (value type):** `default!` is safe, no null-suppression warning fires.
    - **class (reference type):** `default!` is the null-suppression form; ensure nothing else in the test method dereferences the returned `ToastCloseReason` as non-null without a guard. If the returned value IS dereferenced, switch to `Task.FromResult<ToastCloseReason>(new ToastCloseReason())` or whichever ctor is public.
    - **record:** same as class.
    If the grep returns 0 hits (package path layout differs), fall back to `grep -rE "ToastCloseReason" ~/.nuget/packages/microsoft.fluentui.aspnetcore.components/5.*/ 2>/dev/null | head -3` and manually inspect the first referenced declaration.
  - [ ] 2.1: Edit `tests/Hexalith.EventStore.Admin.UI.Tests/Services/ToastServiceExtensionsTests.cs:77` — change `Task.FromException<ToastResult>(...)` to `Task.FromException<ToastCloseReason>(...)`.
  - [ ] 2.2: Edit the same file line 95 — change `Task.FromResult<ToastResult>(default!)` to `Task.FromResult<ToastCloseReason>(default!)`. If Task 2.0 determined `ToastCloseReason` is a class/record AND the surrounding test method dereferences the returned value, swap `default!` for a concrete instantiation per Task 2.0 guidance.
  - [ ] 2.3: Verify Pass B grep returns 0 hits. Paste into Completion Notes.
  - [ ] 2.4: Confirm the 4 × CS0246 + 4 × CS1503 errors disappear — `dotnet build <test project> 2>&1 | grep -cE "CS0246|CS1503"` should drop by 8 from the Task 0.1 baseline. Also confirm 0 net-new CA-family warnings (`dotnet build ... 2>&1 | grep -cE "warning CA"` → 0).

- [x] Task 3: Remove `ConfigureAwait(false)` from test method bodies (AC: 5)
  - [ ] 3.1: For each of the 6 `[Fact]` methods in `ToastServiceExtensionsTests.cs` (lines 20, 31, 42, 53, 64, 79 per Task 0.1), remove ONLY the `.ConfigureAwait(false)` suffix on the single top-level `await` inside the test body. The private helper `CaptureOptionsAsync` (lines 82+) and the `await mockToast.Received(1).ShowToastAsync(...)` call on line 99 are NOT test methods — leave their `.ConfigureAwait(false)` calls intact. Use the Edit tool per line (each `.ConfigureAwait(false)` appears inside a longer context string — use the surrounding `await ...` text to make each Edit unique).
  - [ ] 3.2: **Dual assertion — dynamic PRE/POST helper count (Amelia party-review patch + Red Team A2 dynamic-baseline refinement).**

    **PRE-capture (run BEFORE any Task 3.1 edits):** `grep -cE "ConfigureAwait\(false\)" tests/Hexalith.EventStore.Admin.UI.Tests/Services/ToastServiceExtensionsTests.cs` → record as `helper_configure_await_count_pre: <N>`. Also sub-count by method kind: `grep -B5 -E "ConfigureAwait\(false\)" <file> | grep -cE "\[Fact\]|\[Theory\]"` for test-method occurrences (expected 6); the helper-method count is `N - 6` (expected 2 from lines ~97, ~99). Record both: `test_method_cfg_await_pre: 6, helper_cfg_await_pre: <N-6>`.

    **POST-capture (after Task 3.1 edits):**
    - **Negative assertion (what MUST be 0):** `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release 2>&1 | grep -cE "xUnit1030"` → **0**. Paste output verbatim.
    - **Positive assertion (what MUST remain — dynamic, not hardcoded):** `grep -cE "ConfigureAwait\(false\)" <file>` → **= `helper_cfg_await_pre`** (the PRE helper count from above, NOT hardcoded 2). If POST count equals `helper_cfg_await_pre` → helper ConfigureAwait preserved, story intent achieved. If POST < PRE-helper → dev over-reached into the helper (revert + redo). If POST > PRE-helper → dev missed `[Fact]` body removals (audit + finish).

    This dynamic form handles the case where a parallel story adds a 3rd helper method with `ConfigureAwait(false)` on main between story-creation and dev-story execution — the hardcoded `= 2` would false-alarm in that scenario.

    Paste all 4 values in Completion Notes: `test_method_cfg_await_pre: 6, helper_cfg_await_pre: <N-6>, xunit1030_count_post: 0, cfg_await_count_post: <helper_cfg_await_pre>`.
  - [ ] 3.3: Do NOT edit `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs` or `.../CommandsPageTests.cs` — their `ConfigureAwait(false)` calls produce 0 xUnit1030 errors (they live in non-test helper methods, per Task 0.1 output).

- [x] Task 4: Fix `NavMenuTests.cs` parameter type mismatch (AC: 7)
  - [ ] 4.1: Based on Task 0.2's hypothesis verification:
    - **If hypothesis confirmed (int `220` → string `"220px"` fixes the cascade):** Edit all 4 test methods' `parameters.Add(p => p.Width, 220)` → `parameters.Add(p => p.Width, "220px")` on lines 14, 25, 64, 84 (approximate line numbers — use `grep -nE "p.Width" tests/Hexalith.EventStore.Admin.UI.Tests/Layout/NavMenuTests.cs` to confirm exact lines before editing).
    - **If hypothesis refuted (bUnit v2 API breaking change):** Apply the v2-canonical parameter-builder pattern per Task 0.5 findings (e.g. `ComponentParameter.CreateParameter(nameof(NavMenu.Width), "220px")` if bUnit v2 requires explicit `ComponentParameter` construction, OR whatever the v2 migration guide prescribes). Document the chosen approach in Completion Notes.
  - [ ] 4.2: Rebuild and verify lines 14–85 CS0029 + CS1662 + CS1929 + CS1061 cascade all resolve: `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release 2>&1 | grep -cE "CS0029|CS1662|CS1929|CS1061"` → **0**.
  - [ ] 4.3: After compile-green, run ONLY the NavMenu tests: `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/ --filter "FullyQualifiedName~NavMenuTests" --no-build --configuration Release`. If any runtime assertion fails, triage per the per-test-failure protocol below in Task 5.3. Pass/fail/skip counts recorded in Completion Notes.

- [x] Task 5: Fix `NumericInputTests.cs` bUnit v2 API call (AC: 6)

    **Task 5 gate (Amelia party-review patch):** Task 0.5's fallback gate MUST have determined a canonical v2 replacement. If Task 0.5 invoked the fallback and filed `21-9.5.1-bunit-v2-api-investigation`, SKIP Task 5 entirely — add `[Fact(Skip = "21-9.5.1-bunit-v2-api-investigation")]` to the one failing test method (`NumericInputTests.CultureInvariance_ReformatsAfterValueChange` or whichever contains line 102), document the skip in Completion Notes, and AC 6 is explicitly DEFERRED. Do not invent a v2 API surface.

  - [ ] 5.1: Edit `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/NumericInputTests.cs:102` — replace `cut.SetParametersAndRender(parameters => parameters.Add(p => p.Value, 100L))` with the bUnit v2 equivalent identified in Task 0.5 (most likely `cut.Render(parameters => parameters.Add(p => p.Value, 100L))` — v2's `IRenderedComponent<T>.Render(Action<ComponentParameterCollectionBuilder<T>>)` extension method replaces v1's `SetParametersAndRender`).
  - [ ] 5.2: Verify Pass C grep returns 0 hits. Paste into Completion Notes.
  - [ ] 5.3: **DOM-reference lifecycle triage (Amelia party-review patch — v1 vs v2 re-render semantics differ).** Read `NumericInputTests.cs` lines 93–105 before running the test. Note the call order: line 98 invokes `cut.Find("fluent-text-input").Change("100")` — this obtains an `IElement` reference from the FIRST render AND fires the change event. Line 102 then re-renders with `Value=100L`. The v1 semantics guaranteed that a re-render kept the SAME DOM root (so the `cut.Markup` read on line 103 reflected the re-render of the same tree). In v2, `Render` MAY replace the DOM root entirely — which means:
    - (a) If v2 preserves DOM root → behaviour unchanged, test passes. Record `v2_render_semantics: preserves-root` in Completion Notes.
    - (b) If v2 replaces DOM root → the `cut.Find("fluent-text-input").Change("100")` on line 98 fired against the old root, but line 102 replaced that root, so `cut.Markup` on line 103 reflects the re-rendered tree WITHOUT the line 98 `Change()` effect unless v2 preserves event-callback state across re-renders. Record `v2_render_semantics: replaces-root` in Completion Notes, and if the assertion on line 103 (`current-value="100"`) fails because of this, the test is asserting on a semantic that v1 provided and v2 doesn't — fix by moving the `.Change("100")` call to AFTER the `cut.Render(...)` re-render, OR by using `cut.SetParametersAndRender`-equivalent that v2 documents for in-place updates (per Task 0.5's probe).
    Run the single test: `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/ --filter "FullyQualifiedName~NumericInputTests" --no-build --configuration Release`. If pass, record (a) and move on. If fail, walk the decision tree above, apply the chosen fix, re-run once.

    **Explicit rollback before skip (FMA M3 — prevents partial edit + skip in same commit):** If still failing after one fix attempt: BEFORE marking the test `[Fact(Skip = "21-9.5.1-bunit-v2-render-semantics")]`, run `git checkout tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/NumericInputTests.cs` to revert BOTH the Task 5.1 Render-API edit AND the one-attempt runtime fix. This returns the file to baseline. THEN apply only the minimal change: the line-102 `SetParametersAndRender` → `Render` rename (to clear AC 6 / Pass C grep) AND add `[Fact(Skip = "21-9.5.1-bunit-v2-render-semantics")]` to the containing test method. File (or append to) follow-up story `21-9.5.1-bunit-v2-api-investigation`. Commit represents "migrated + deferred runtime," not "half-fixed + skipped" — clean signal for reviewers. Do NOT modify the original assertion intent (`current-value="100"` is what the component guarantees; the test fidelity is the goal — 21-9.5.1 owns getting it green).

- [x] Task 6: Build + full-suite test gates (AC: 1, 8, 10, 11) — AC 1, 10, 11 PASS; AC 8 partial (3 MergedCssSmokeTests green; 62 latent failures deferred to 21-9.5.7)
  - [ ] 6.1: `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release`. Expect **0 errors, 0 warnings**. Paste count line verbatim in Completion Notes. AC 1 PASS.
  - [ ] 6.2: `dotnet build Hexalith.EventStore.slnx --configuration Release 2>&1 | grep -cE "error "`. Expect **36** (Sample.BlazorUI only, 21-10 scope). Verify residual traceability: `grep -B2 "error " /tmp/slnx-build.log 2>&1 | grep -oE "samples[\\/][^(]*" | sort -u | head`. AC 10 PASS.
  - [ ] 6.3: Run Admin.UI bUnit suite — **exact command pinned by 21-9 Red Team A6 (no `--filter`, no project-filter shortcut):** `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release --no-build`. Expect `Passed! - Failed: 0, Passed: <N>, Skipped: 0`. Paste the exact stdout line verbatim in Completion Notes. Include explicit confirmation that `MergedCssSmokeTests` (3 tests) appear in the passing set. AC 8 PASS.
  - [ ] 6.4: Run Tier 1 non-UI tests (regression guard): `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ tests/Hexalith.EventStore.Client.Tests/ tests/Hexalith.EventStore.Sample.Tests/ tests/Hexalith.EventStore.Testing.Tests/ tests/Hexalith.EventStore.SignalR.Tests/`. Expect **753/753 green** (match the 21-9 baseline). AC 11 PASS.
  - [ ] 6.5: Cold-cache rebuild: `dotnet clean tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj && dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release`. Confirm 0 errors on fresh build (no caching artifacts masking regressions).

- [x] Task 7: Close story — status=review set
  - [ ] 7.1: Ensure all 3 grep-for-zero passes (AC 9) return 0. Paste all 3 outputs verbatim in Completion Notes.
  - [ ] 7.2: Ensure AC 1, 8, 10, 11 build + test gates green. Document each AC's PASS evidence in Completion Notes. If Task 0.2 cap-hit filed 21-9.5.3, or Task 0.5 fallback filed 21-9.5.1, or AC 8 triage filed 21-9.5.2 — document each deferral explicitly with the follow-up story key and the specific ACs/tests affected.
  - [ ] 7.3: Update the Status at the top of this file to `review`.
  - [ ] 7.4: **Update (not add)** `sprint-status.yaml` — the entry `21-9-5-admin-ui-tests-v5-migration: ready-for-dev` was added during story creation (2026-04-15). Dev-story MUST transition `ready-for-dev → in-progress` at Task 0 start and `in-progress → review` at Task 7.3. Update `last_updated` field at each transition. Preserve all comments/structure/STATUS DEFINITIONS. Per STATUS DEFINITIONS, `done` is set by the code-review workflow, not by this task. (Bob party-review patch — verb corrected from "add" to "update.")
  - [ ] 7.5: **Dynamic line lookup for deferred-work.md (Amelia party-review patch — append-only file drifts line numbers).** Run `grep -nE "21-9\.5|21-9-5|Admin\.UI\.Tests compile failures remain deferred" _bmad-output/implementation-artifacts/deferred-work.md` to locate the current line of the "Admin.UI.Tests compile failures remain deferred to follow-up 21-9.5" bullet. Record the resolved line number in Completion Notes as `deferred_work_bullet_line: <N>` (baseline was 102 on 2026-04-15 — note any drift). Then edit that specific line to mark the bullet as **RESOLVED-IN-21-9-5** (e.g. prepend `✅ [RESOLVED-IN-21-9-5 on YYYY-MM-DD]` or append the same tag). Do not hardcode line 102.
  - [ ] 7.6: Prepare § File List with every file touched.
  - [ ] 7.7: Document epic-21-retrospective eligibility: 21-9 + 21-9.5 + 21-10 must all be done before retrospective runs. Note in Completion Notes. If this story filed any of 21-9.5.1, 21-9.5.2, or 21-9.5.3 as follow-ups, the retrospective is blocked until those close too — document that blocker explicitly.

### Review Findings

- [x] [Review][Decision] Story ordering conflict in sprint tracking — `21-9-5-admin-ui-tests-v5-migration` is now `review` while dependency story `21-9-datagrid-remaining-enum-renames` remains `in-progress` in sprint tracking, despite this story's hard dependency gate requiring 21-9=`done` before 21-9.5 starts. Resolved: keep 21-9 in-progress and allow 21-9.5 closure as a parallel exception (user-approved, 2026-04-15).
- [x] [Review][Patch] Story status header is stale (`ready-for-dev` vs actual review state) [_bmad-output/implementation-artifacts/21-9-5-admin-ui-tests-v5-migration.md:3] — fixed (Status set to done).
- [x] [Review][Patch] Resolved deferred-work bullet still embeds stale "still fails (43 errors observed in this run)" text, which contradicts the new resolved state [_bmad-output/implementation-artifacts/deferred-work.md:102] — fixed (wording updated to resolved-only narrative).

## Dev Notes

### What This Story Does and Does NOT Do

**DOES:**
- Renames all `FluentTextField` → `FluentTextInput` in 2 test files (`BackupsPageTests.cs`, `CompactionPageTests.cs`) — 16 unique sites, resolves 36 CS0246 raw errors (including razor-multiplier doubling).
- Renames `ToastResult` → `ToastCloseReason` in `ToastServiceExtensionsTests.cs` (2 sites, resolves 4 CS0246 + 4 CS1503 raw errors).
- Removes `ConfigureAwait(false)` from 6 `[Fact]` test bodies in `ToastServiceExtensionsTests.cs` (xUnit1030 rule — resolves 12 raw errors).
- Fixes `NavMenuTests.cs` `parameters.Add(p => p.Width, 220)` type mismatch (int → string per `NavMenu.Width: string`) at 4 call sites (resolves 32 raw CS0029+CS1662+CS1929+CS1061 cascade errors).
- Fixes `NumericInputTests.cs:102` bUnit v1 `SetParametersAndRender` call to v2 `Render` API (1 site, resolves 2 CS1061 raw errors — part of the 10 CS1061 count).
- Runs the full Admin.UI bUnit suite with the exact command pinned by 21-9 Red Team A6 — includes the 3 21-8 merged-CSS smoke tests executing for the first time.
- Brings Admin.UI.Tests compile to **0 errors** and slnx total errors from **122 → 36** (36 residual = 21-10 Sample.BlazorUI scope).

**DOES NOT:**
- Touch `src/Hexalith.EventStore.Admin.UI/` production code. Admin.UI was fully migrated in 21-1 through 21-9 and is at 0 errors.
- Touch `samples/Hexalith.EventStore.Sample.BlazorUI/` — 21-10 scope (separate story).
- Add new bUnit tests beyond the existing set. The goal is compile + run green, not coverage expansion.
- Edit `TenantsPageTests.cs` or `CommandsPageTests.cs` `ConfigureAwait(false)` calls — those produce 0 xUnit1030 errors (non-test helper method scope).
- Edit the private helper method `CaptureOptionsAsync` in `ToastServiceExtensionsTests.cs:82+` — xUnit1030 only fires on test methods (`[Fact]`/`[Theory]`).
- Restructure how tests are organized or authored. Pure compile-fix work, no refactor.
- Add backwards-compat shims, feature flags, or try/catch workarounds. Clean v4/bUnit-v1 removal.
- Run visual browser verification (Tasks 8/9 of 21-9 scope — separate manual browser session).

### Admin.UI.Tests Error Inventory — 86 raw errors across 5 files

**Source:** `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release 2>&1` run 2026-04-15.

| File | Error Count (raw) | Error Codes | Unique Sites | Root Cause |
|---|---:|---|---:|---|
| `Layout/NavMenuTests.cs` | 32 | CS0029×8 + CS1662×8 + CS1929×8 + CS1061×8 | 4 test methods × 4-error cascade per | `p.Width` is `string` but test passes int `220` — bUnit `.Add` overload resolution cascades into a RenderFragment miscompile |
| `Services/ToastServiceExtensionsTests.cs` | 20 | CS0246×4 + CS1503×4 + xUnit1030×12 | 2 sites (ToastResult) + 6 sites (ConfigureAwait) | v5 removed `ToastResult` (replaced by `ToastCloseReason`); xUnit1030 analyzer forbids `ConfigureAwait(false)` in `[Fact]` method bodies |
| `Pages/BackupsPageTests.cs` | 20 | CS0246×20 | 10 sites (FluentTextField × 2 razor-multiplier) | v5 renamed `FluentTextField` → `FluentTextInput` (same rename applied to `src/*.razor` already in 21-5 / 21-9; test project missed) |
| `Pages/CompactionPageTests.cs` | 12 | CS0246×12 | 6 sites (FluentTextField × 2 razor-multiplier) | Same as BackupsPageTests |
| `Components/Shared/NumericInputTests.cs` | 2 | CS1061×2 | 1 site (SetParametersAndRender × 2 razor-multiplier) | bUnit v1 → v2 API rename: `SetParametersAndRender` moved/renamed to `Render(Action<ComponentParameterCollectionBuilder<T>>)` |

**Razor source-gen note:** errors in `.cs` files that reference razor-compiled types can appear 2× because Razor source-gen emits both a component class and an event-handler closure, doubling the diagnostic emission (same pattern observed in 21-9's 78 CS0103 = 33 unique sites × 2 multiplier).

### Pre-21-9.5 Baseline (from 21-9 Task 7 + 2026-04-15 verification)

| Metric | Pre-21-9.5 Count | Target Post-21-9.5 |
|---|---:|---:|
| Admin.UI.Tests errors (total) | 86 | **0** (AC 1) |
| Admin.UI.Tests CS0246 (type not found) | 36 | **0** (AC 2, 3, 4) |
| Admin.UI.Tests xUnit1030 (ConfigureAwait in test method) | 12 | **0** (AC 5) |
| Admin.UI.Tests CS1061 (member missing — `SetParametersAndRender` + `UserRole` cascade) | 10 | **0** (AC 6, 7) |
| Admin.UI.Tests CS0029 / CS1662 / CS1929 (NavMenu cascade) | 24 | **0** (AC 7) |
| Admin.UI.Tests CS1503 (Task<ToastResult> conversion) | 4 | **0** (AC 4) |
| Admin.UI.Tests warnings | 0 | 0 (unchanged; elevated to errors) |
| Admin.UI.Tests bUnit suite | ❌ blocked (compile fail) | **✓ green incl. 3 merged-CSS smoke tests** (AC 8) |
| Slnx raw errors | 122 (36 Sample.BlazorUI + 86 Admin.UI.Tests) | **36** (Sample.BlazorUI only, 21-10 scope) (AC 10) |
| Tier 1 non-UI tests | 753/753 green | 753/753 (unchanged — no touch) (AC 11) |

### V4 → V5 / bUnit v1 → v2 Rename Reference

| Legacy | Current | Source |
|---|---|---|
| `FluentTextField` | `FluentTextInput` | Fluent UI Blazor v5 migration guide (`FluentTextField` renamed in v5; `src/*.razor` already migrated via 21-5 / 21-9 — only tests lag) |
| `ToastResult` | `ToastCloseReason` | Fluent UI Blazor v5 `IToastService.ShowToastAsync` signature: returns `Task<ToastCloseReason>`. Consult via Fluent UI MCP: `mcp__fluent-ui-blazor__get_component_migration FluentToast` |
| `await ... .ConfigureAwait(false)` in `[Fact]` body | `await ...` (no ConfigureAwait) | xUnit1030 analyzer rule (https://xunit.net/xunit.analyzers/rules/xUnit1030). NOTE: `ConfigureAwait(false)` in **helper methods** is still fine. |
| `cut.SetParametersAndRender(parameters => ...)` (bUnit v1) | `cut.Render(parameters => ...)` (bUnit v2) | bUnit v2 migration — `IRenderedComponent<T>.SetParametersAndRender` replaced by extension method `Render` in the `Bunit` namespace. Confirm exact signature via installed `Bunit.xml` (Task 0.5). |
| `parameters.Add(p => p.Width, 220)` (int to string `Width` — type mismatch + bUnit overload cascade) | `parameters.Add(p => p.Width, "220px")` (correctly-typed literal) | Not a v5/v2 rename per se — a pre-existing type-compat bug that the 21-9 cascade cleanup exposed. |

### Known Pitfalls (LLM-dev guardrails)

1. **Do NOT rename `FluentTextField` in production code (`src/Hexalith.EventStore.Admin.UI/`).** The rename already happened there in 21-5 / 21-9. Only the test project `tests/Hexalith.EventStore.Admin.UI.Tests/` lags. Scope is explicit: `tests/` only.
2. **Do NOT remove `.ConfigureAwait(false)` from NON-test methods.** xUnit1030 only fires on `[Fact]`/`[Theory]` method bodies. The private helper `CaptureOptionsAsync` in `ToastServiceExtensionsTests.cs` MUST keep its `ConfigureAwait(false)` calls — otherwise the project convention (all non-test async calls use `ConfigureAwait(false)`) breaks.
3. **Do NOT edit `TenantsPageTests.cs` or `CommandsPageTests.cs` `ConfigureAwait(false)`.** Verify via Task 0.1 breakdown: those files produce 0 xUnit1030 errors. Their `ConfigureAwait` calls are inside setup helpers, not test bodies.
4. **Do NOT change `NavMenu.Width` property type** — the test is wrong, not the component. `NavMenu.razor:62` has `public string Width { get; set; } = "220px"` as the intended contract. Tests must pass `"220px"` (string), not `220` (int).
5. **Do NOT rename `AdminRole` to something else.** `NavMenu.razor:65` declares `public AdminRole UserRole { get; set; } = AdminRole.ReadOnly` — the enum exists in `Hexalith.EventStore.Admin.UI.Services.AdminRole` and is correct. The CS1061 `Type.UserRole` errors are **downstream cascades** of the `Width` overload failure (bUnit's `.Add` resolution chain breaks after the first failure), NOT a missing member. AC 7 + Task 4.2 verify the cascade clears after the Width fix.
6. **Do NOT assume bUnit v2's `Render` behaves identically to v1's `SetParametersAndRender`.** Task 5.3 triages any runtime behavior delta. v2 may re-render in place OR replace root — document any observed difference.
7. **Do NOT add new tests in this story.** The goal is compile-green + run-green. Coverage expansion is separate scope (21-0 backlog items under `deferred-work.md` track that).
8. **Do NOT touch the `MergedCssSmokeTests.cs` file** unless it fails to compile (per Task 0.1 breakdown, it currently has 0 own errors — it just cascaded through the rest of the project's 82-error blocker before 21-9 landed). If the 3 smoke tests fail at runtime, investigate whether 21-8's merged-CSS class hooks match what the tests assert — do NOT modify the tests to match reality; the tests document the intended behavior.
9. **Do NOT use `sed -i` on Windows Git Bash** — per 21-9 Amelia review finding. Use the Edit tool per site to preserve CRLF and avoid BOM emission.
10. **Do NOT introduce compat shims, `#pragma warning disable xUnit1030`, deprecated-alias using-statements, or feature flags.** Clean v5 / bUnit v2 migration. `TreatWarningsAsErrors=true` in `Directory.Build.props` is deliberate — do not relax it for this story.
11. **Do NOT use `.csproj` tricks to achieve "0 errors" (Red Team A4 — escape-hatch prevention).** Specifically prohibited (any use fails AC 1):
    - `<Compile Remove="..." />` — removing source files from compilation.
    - `<ExcludeFromBuild>true</ExcludeFromBuild>` on items.
    - `.editorconfig` severity downgrades on any analyzer rule that fires in this story (xUnit1030, CS0246, CS0029, CS1061, CS1503, CS1662, CS1929).
    - `<NoWarn>` additions in `.csproj` or `Directory.Build.props`.
    - `[SuppressMessage]` attributes on test methods for the analyzer rules this story touches.
    The intent is that Admin.UI.Tests ships the FIX, not a masked-out version of the problem. A Reviewer verification grep for Task 7 closure: `grep -rnE "Compile Remove|ExcludeFromBuild|NoWarn|SuppressMessage" tests/Hexalith.EventStore.Admin.UI.Tests/*.csproj .editorconfig` — expect 0 new hits vs. baseline.
12. **Removing `ConfigureAwait(false)` from test method bodies is behaviorally safe.** xUnit's test runner manages the continuation context; test-method bodies do not need `ConfigureAwait(false)` for correctness. The project-wide convention (use `ConfigureAwait(false)` in async library code) still applies to helper methods, non-test library code, and Admin.UI production source — those MUST keep `ConfigureAwait(false)`. The exception for `[Fact]`/`[Theory]` bodies is what xUnit1030 enforces and what this story honors.

### Previous Story Intelligence (21-9 DataGrid + remaining enum renames)

**21-9 landed 2026-04-15** with status `review` (Admin.UI at 0 errors, Tasks 8/9 manual browser sessions + 21-9.5 test-project follow-up both deferred). Key inheritance for 21-9.5:

- **The 82 Admin.UI.Tests errors observed at 21-9 Task 0.5 were 100% cascaded from Admin.UI** — all traced to `Hexalith.EventStore.Admin.UI.csproj`, zero from `*.Tests.csproj`. After 21-9 landed and Admin.UI reached 0 errors, the test project's **own** 86 errors surfaced. Of those 86, per 21-9 Task 7 Completion Notes: `CS0246=36 (FluentTextField not found — v5 rename to FluentTextInput, 10 source lines × razor multiplier), CS1061=10 (bUnit SetParametersAndRender API change), CS0029=8 / CS1503=12 / CS1662=8 / CS1929=8 (various type-conversion cascades), xUnit1030=12 (ConfigureAwait(false) in 21-7's ToastServiceExtensionsTests.cs test methods — xUnit analyzer forbids)`. Note: 21-9's breakdown said CS1503=12 but actual build shows CS1503=4 — likely a typo in 21-9 Completion Notes; trust the live `dotnet build` output from Task 0.1 as authoritative.
- **21-9 Task 0.5 scope cap triggered (Pre-mortem F6).** 86 errors spanning 4 distinct v5/bUnit/xUnit migration patterns exceeded the 15-minute/5-line cap. 21-9 accepted the gap and filed this follow-up. The cross-reference to `21-8-css-token-migration.md` Review Findings (Hindsight H3 pattern) was NOT appended because no `MergedCssSmokeTests.cs` typos were found in 21-9 Task 0.5 — all 86 errors are in OTHER test files.
- **The 3 merged-CSS smoke tests from 21-8 Task 6.5 (`MergedCssSmokeTests.cs`) are WRITTEN but not RUN.** First run happens in AC 8 of this story. If they fail, investigate the merged `app.css` class-name wrap in `.razor.css` source — NOT this story's v5 migration. Most likely cause: a class hook renamed during 21-8 edits that conflicts with a merged block selector. But: compile gate (Task 0.1) already confirmed the file itself has 0 own errors, so it at least compiles. Runtime semantics are the open question.
- **21-9 Red Team A6 no-filter command pinned for THIS story:** `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release --no-build` — exact, no `--filter`, no project-filter. AC 8 + Task 6.3 cite this verbatim.
- **Admin.UI.Tests.csproj `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is inherited** via `Directory.Build.props` — any warning will break the 0-error gate. If xUnit1030 or CA2007 fires elsewhere, address root-cause (not suppression).
- **Windows Git Bash `sed -i` corrupts CRLF / emits BOM** — per 21-9 Amelia finding. Use the Edit tool per site.

### Git Intelligence Summary (last 5 commits on main)

- **21-9 landed as `in-progress` / `review`** on branch before merge (as of 2026-04-15). Look for the rename PR commit when it merges — the 21-9 File List already enumerates which `.razor` files moved `Align`/`SortDirection`/`FluentTab.Label`/`FluentSkeleton.Shape`/`IDialogReference`. No direct impact on 21-9.5 scope, but a useful cross-reference if Task 0.6's grep finds unexpected hits outside tests/.
- `20a4538` feat(ui): migrate Admin.UI CSS v4 FAST tokens to v5 Fluent 2 and merge scoped CSS (Story 21-8) — the 3 merged-CSS smoke tests were born here. This story runs them for the first time.
- `a544571` feat(ui): migrate IToastService.Show* calls to ShowToastAsync with extension helpers (Story 21-7) — `src/Hexalith.EventStore.Admin.UI/Services/ToastServiceExtensions.cs` is what `ToastServiceExtensionsTests.cs` tests. The v5 `IToastService.ShowToastAsync` signature (returns `Task<ToastCloseReason>`) is the source of truth; AC 4 aligns the test to match.
- `ec37c58` feat(ui): restructure dialogs to Fluent UI Blazor v5 template slots (Story 21-6) — unrelated to 21-9.5 scope.
- `3f8ad99` feat(container): migrate CD pipeline — no test impact.

### Architecture Decision Records

**ADR-21-9-5-001: Fix `NavMenuTests.cs` at the test site, not the component.**

| Option | Description | Trade-off | Decision |
|---|---|---|---|
| A (chosen) | Change test literal `220` → `"220px"` to match `NavMenu.Width: string` | Respects component contract; matches default-value intent; 4 small edits | ✓ Default |
| B | Change `NavMenu.Width` type from `string` to `int` | Would accept `220` natively but breaks production styling (width is a CSS dimension string like `"220px"`, not a raw number) | Rejected — inverts the dependency direction |

**Rationale:** `NavMenu.Width` is a CSS dimension applied directly to a `style="width: @Width"` attribute in `NavMenu.razor`. Strings like `"220px"`, `"15rem"`, or `"clamp(200px, 20vw, 300px)"` must all be valid. Making `Width` an int would require a unit-suffix convention baked into the component and break all existing callers that use CSS-string idioms. The test literal is a type mismatch; the component contract is correct.

**ADR-21-9-5-002: Do NOT suppress xUnit1030 — remove `ConfigureAwait(false)` from test bodies.**

| Option | Description | Trade-off | Decision |
|---|---|---|---|
| A (chosen) | Remove `.ConfigureAwait(false)` from 6 `[Fact]` test-body awaits | Aligns with xUnit guidance; future tests in other files don't need to think about it; matches the rule's intent | ✓ Default |
| B | `#pragma warning disable xUnit1030` at file-top OR `.editorconfig` rule downgrade | Keeps existing idiom; zero code edits | Rejected — suppressing an analyzer rule that fires on a real pattern violates project's warnings-as-errors hygiene |
| C | Set `TreatWarningsAsErrors=false` for this project | Broad escape hatch | Rejected — nukes the project-wide invariant for one rule |

**Rationale:** xUnit1030 is a correctness rule. `ConfigureAwait(false)` in test bodies can bypass the xUnit synchronization context used for parallelization and rare deadlock avoidance — rare in practice but documented by xUnit. Removing the calls respects the rule's intent. Helper methods (not `[Fact]`/`[Theory]`) are exempt and keep their `ConfigureAwait(false)` — this matches how the rule was authored.

**ADR-21-9-5-003: bUnit v2 `Render` replaces v1 `SetParametersAndRender` — canonical per install probe.**

| Option | Description | Trade-off | Decision |
|---|---|---|---|
| A (chosen) | Replace `SetParametersAndRender` with `Render(parameters => ...)` per Task 0.5 probe of installed `Bunit.xml` | Confirms API empirically, not by guess | ✓ Default |
| B | Upgrade bUnit package version | Out of scope — 21-1 set the package version, not this story | Rejected |

**Rationale:** bUnit v2 streamlined the API. The installed version (whatever 21-1 pinned via `Directory.Packages.props`) is authoritative. Task 0.5 probes `~/.nuget/packages/bunit.*/lib/.../Bunit.xml` for the exact replacement — guard against guessing.

**ADR-21-9-5-004: Chosen migration approach is surgical rename (Approach A) — not rewrite, not upgrade, not adapter. (Self-Consistency S1)**

5 approaches were considered for migrating Admin.UI.Tests from v4/bUnit-v1 to v5/bUnit-v2:

| # | Approach | Risk | Time | Verdict |
|---|---|---|---:|---|
| **A** | **Surgical v4/v1 → v5/v2 rename + remove (CHOSEN)** — per-site Edit for 4 rename categories | medium — depends on 1:1 API mappings holding | 2-4h | ✓ Default |
| B | Delete failing tests + rewrite from scratch with v5/v2 idioms | high — loses coverage investment from 21-0, 21-7, 21-8; loses the 3 MergedCssSmokeTests as written in 21-8 | 1-2d | Rejected — coverage loss outweighs "clean idiom" benefit |
| C | Upgrade bUnit to v3 (skip v2 intermediate state entirely) | high — out of scope; 21-1 pinned bUnit version; cascading project-wide changes | unknown | Rejected — scope violation |
| D | Build v1→v2 adapter layer (wrap v1 API into helper calling v2 underneath) | medium compile-risk, high long-term debt | 2-3h | Rejected — violates "no shims" per 21-9 Known Pitfall #9 + `Directory.Build.props` philosophy |
| E | Pin bUnit at v1.x permanently | high — .NET 11 drops incompatible Blazor changes; accumulating tech debt | — | Rejected out-of-gate |

**Rationale:** Approach A is the smallest delta that achieves compile-green + run-green AND preserves all test coverage investment. Approaches B/C/D/E each introduce their own larger risk surface without proportional benefit. The 4 rename categories (FluentTextField→FluentTextInput, ToastResult→ToastCloseReason, xUnit1030 ConfigureAwait removal, bUnit SetParametersAndRender→Render) are well-understood 1:1 mappings per installed package probes (Tasks 0.4, 0.5). If any mapping turns out to be non-1:1, the story has pre-filed follow-up stories (21-9.5.1 through .5.6) for scope-escape, keeping the main story surgical.

**Task 4/5 coupling (Self-Consistency S1 finding):** Task 4 (NavMenu `Width` int→string) and Task 5 (bUnit SetParametersAndRender→Render) BOTH touch `parameters.Add`. The NavMenu cascade may actually be a bUnit v2 API change on `ComponentParameterCollectionBuilder<T>.Add` that manifests as CS0029/CS1662/CS1929 on the `Width` call site, NOT a pure type mismatch. **Implication:** If Task 0.5 fallback triggers (bUnit v2 API non-1:1, `21-9.5.1-bunit-v2-api-investigation` filed), re-run Task 0.2 probe AFTER Task 0.5 findings — not in isolation. The NavMenu hypothesis probe (int `220` → string `"220px"`) may clear the cascade ONLY because bUnit v2's new overload accepts string via a different resolution path. If Task 0.5 says bUnit v2 `Add` was redesigned, Task 0.2's probe result is suspect and should be re-evaluated against the new overload signature. Dev note: read both Task 0.5 and Task 0.2 outputs together before committing Task 4.1 + Task 5.1 edits.

### Testing Requirements

- **xUnit 2.9.3** via `Directory.Packages.props` centralized — no new test framework additions.
- **Shouldly 4.3.0** for assertions — existing style preserved.
- **NSubstitute 5.3.0** for mocking — used in `ToastServiceExtensionsTests.cs`; signatures don't change (only the generic type argument to `Task.FromException`/`FromResult` updates).
- **bUnit** — the test project's bUnit version pinned by 21-1. Confirm via `cat Directory.Packages.props | grep -i bunit` in Task 0.5. The `Render` extension method on `IRenderedComponent<T>` is the v2 canonical API.
- **Coverage threshold:** coverlet.collector 6.0.4 installed but no enforced threshold. No coverage obligation for this story.
- **All existing tests must pass** before story completion (CLAUDE.md § Test Conventions). Specifically:
  - AC 8: full Admin.UI.Tests bUnit suite green.
  - AC 11: 753/753 Tier 1 non-UI tests still green.
- **No new tests required.** Pure compile-fix + existing-test-run work.

### File Structure Requirements

All edits scoped to `tests/Hexalith.EventStore.Admin.UI.Tests/`. Specifically:

| Category | Files | Scope |
|---|---|---|
| `FluentTextField` rename | `Pages/BackupsPageTests.cs`, `Pages/CompactionPageTests.cs` | All `FluentTextField` occurrences → `FluentTextInput` |
| `ToastResult` rename | `Services/ToastServiceExtensionsTests.cs` | Generic type args on `Task.FromException` / `Task.FromResult` |
| `ConfigureAwait(false)` removal | `Services/ToastServiceExtensionsTests.cs` | Test method bodies ONLY (6 `[Fact]` methods) |
| bUnit v2 `Render` API | `Components/Shared/NumericInputTests.cs` | Line 102 |
| NavMenu `Width` type fix | `Layout/NavMenuTests.cs` | 4 test methods |
| Sprint tracking | `_bmad-output/implementation-artifacts/sprint-status.yaml` | Add 21-9-5 entry; update last_updated |
| Deferred work closure | `_bmad-output/implementation-artifacts/deferred-work.md` | Mark 21-9-5 follow-up RESOLVED |

No files outside `tests/Hexalith.EventStore.Admin.UI.Tests/` (plus the two BMad tracking files) are modified. Production code, `src/`, `samples/`, and other test projects are untouched.

### Library/Framework Requirements

- **Microsoft.FluentUI.AspNetCore.Components 5.x** (already installed via 21-1) — use v5 API surface only.
- **bUnit v2.x** (pinned by 21-1 via `Directory.Packages.props`) — use v2 API surface only (`Render` extension, not `SetParametersAndRender`).
- **xunit 2.9.3** + **xunit.analyzers** — `xUnit1030` analyzer is the source-of-truth for test-body `ConfigureAwait` policy.
- **.NET 10 SDK 10.0.103** pinned via `global.json` — do not change.
- **Directory.Packages.props** centralized package management — do not add new packages.
- **Hexalith.EventStore.slnx** modern XML solution format — use the `.slnx` file only.
- **Fluent UI Blazor MCP server** (registered per `.claude/mcp.json`) — consult migration guides in real time via `mcp__fluent-ui-blazor__get_component_migration FluentTextField` or `FluentToast` if needed.

### Project Structure Notes

- All edits in 5 `.cs` files under `tests/Hexalith.EventStore.Admin.UI.Tests/`.
- No `.csproj`, `.props`, or `.editorconfig` changes required.
- No new files created.
- No test artifacts produced (no screenshots, no HTML reports — this is a pure code-green story, no manual browser session).

### References

- [Source: _bmad-output/implementation-artifacts/21-9-datagrid-remaining-enum-renames.md#Review-Findings] — the 21-9 review-deferred Admin.UI.Tests follow-up is this story
- [Source: _bmad-output/implementation-artifacts/21-9-datagrid-remaining-enum-renames.md#Task-7.3] — 21-9 Task 7.3 DEFERRED-TO-21-9-5 marker
- [Source: _bmad-output/implementation-artifacts/21-9-datagrid-remaining-enum-renames.md#Completion-Notes] — error breakdown (86 errors, 4 distinct migration patterns) matches Task 0.1 baseline here
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:101-102] — "Admin.UI.Tests compile failures remain deferred to follow-up 21-9.5" entry dated 2026-04-15
- [Source: _bmad-output/implementation-artifacts/sprint-status.yaml:250] — 21-9 status comment references "21-9.5 still pending"
- [Source: src/Hexalith.EventStore.Admin.UI/Services/ToastServiceExtensions.cs:14-71] — `ShowToastAsync` wrapper defines the return type boundary (`Task<ToastCloseReason>` per v5)
- [Source: FluentUI MCP `mcp__fluent-ui-blazor__get_component_migration FluentToast`] — authoritative v5 `IToastService.ShowToastAsync` signature + `ToastResult`→`ToastCloseReason` rename (Hindsight H-cite: infer-from-code-comment was lucky; MCP guide is the primary source)
- [Source: FluentUI MCP `mcp__fluent-ui-blazor__get_component_migration FluentTextField`] — authoritative v5 `FluentTextField`→`FluentTextInput` rename + any v5 multi-line variant (`FluentTextArea`) that applies to non-single-line test sites
- [Source: FluentUI MCP `mcp__fluent-ui-blazor__get_component_details FluentTextInput` / `FluentTextArea`] — for Task 1.0 src-side symmetry pre-check when classifying which v5 component each test-targeted field uses
- [Source: src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor:62,65] — `Width: string` + `UserRole: AdminRole` parameter contracts
- [Source: src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor:55,214,218] — confirms production code uses `FluentTextInput` (v5), validating the test rename target
- [Source: tests/Hexalith.EventStore.Admin.UI.Tests/Components/MergedCssSmokeTests.cs] — 3 merged-CSS smoke tests from 21-8 Task 6.5 (authored but never RUN; this story's AC 8 is their first run)
- [Source: xUnit analyzer documentation — https://xunit.net/xunit.analyzers/rules/xUnit1030] — xUnit1030 rule text and rationale
- [Source: bUnit v2 migration notes — installed `Bunit.xml` XML docs] — canonical `SetParametersAndRender` → `Render` rename
- [Source: CLAUDE.md § Code Style & Conventions] — file-scoped namespaces, Allman braces, `_camelCase` fields (no new fields in this story)
- [Source: CLAUDE.md § Commit Messages] — Conventional Commits; target commit type `fix(ui-tests):` or `refactor(ui-tests):`

## Dev Agent Record

### Agent Model Used

claude-opus-4-6[1m]

### Debug Log References

### Completion Notes List

#### Review Checklist (Hindsight H2 — reviewer quick-gate)

| # | Check | Evidence |
|---|---|---|
| 1 | Task 0.0 dependency gate | `dependency_gate: PROCEED-WITH-DRIFT — 21-9 status=in-progress at 2026-04-15 but commit 400ecd1 has physically merged to main 2 commits back from HEAD (user-authorized proceed, option 2). Baseline verified at 86 (matches expected 86 ± 4, confirming Admin.UI cascade absent per 21-9's landed state).` |
| 2 | Task 0.1 `test_errors_pre` tolerance | `test_errors_pre: 86` (exact, ∈ [82,90]); per-code: `CS0246=36, xUnit1030=12, CS1061=10, CS1929=8, CS1662=8, CS0029=8, CS1503=4` — matches expected inventory exactly. `skip_count_pre: 0`. |
| 3 | Task 0.2 probe git stash | `probe_stash_ref: stash@{0} (warning: in the working copy of '.claude/mcp.json', LF will be replaced by CRLF)`; `probe_started: 2026-04-15 (edit line 14 only), probe_completed: same session, elapsed_min: <5`; `cascade_cleared: true` (hypothesis CONFIRMED — single-line edit cleared all 4 CS1929/CS0029/CS1662/CS1061 at lines 13–15). Reverted via `git checkout` + `git stash pop`. |
| 4 | Task 0.4 ToastCloseReason | `v5_types_found: FluentTextInput=yes, ToastCloseReason=yes` (found in `microsoft.fluentui.aspnetcore.components/5.0.0-rc.2-26098.1` — `ToastCloseReason` referenced as param type in `IToastInstance.CloseAsync`). `toast_close_reason_kind: enum-or-struct` (value type; `default!` safe on line 95). No 21-9.5.4 filing needed. |
| 5 | Task 0.10 MergedCssSmokeTests pre-verify | `smoke_preverify: ALL MATCH` — 3 tests assert `.status-badge`, `.status-badge__icon`, `.status-badge__label`, `.state-diff-viewer`, `.json-viewer`, `.json-line`, `.json-line-number`, `.json-key`, `.json-string`, `.json-number`, `.json-boolean`, `.json-null`. All classes present in merged `app.css`. Confirmed runtime-green at AC 8 (all 3 smoke tests PASS in AC 8 test run). |
| 6 | Task 3.2 helper ConfigureAwait | `test_method_cfg_await_pre: 6, helper_cfg_await_pre: 2` (lines 97, 99 inside `CaptureOptionsAsync`). Post-edit: `xunit1030_count_post: 0`, `cfg_await_count_post: 2` = PRE helper count. Test-method bodies cleaned; helper preserved. |
| 7 | AC 7 NavMenu.razor source guard | `navmenu_width_src_guard: src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor:62:    public string Width { get; set; } = "220px"` — 1 hit, unchanged from baseline. Fix applied test-side ONLY (4 sites: lines 14, 25, 64, 84). |
| 8 | AC 1 skip-count guard | `skip_count_post: 0` (no `[Fact(Skip=...)]` added). AC 1 skip-guard honored. |
| 9 | AC 8 smoke test outcomes | `ProjectionStatusBadge_Renders_WithExpectedClassAttributes: PASS`, `StateDiffViewer_Renders_WithRootClass: PASS`, `JsonViewer_Renders_WithSyntaxHighlightClasses: PASS`. All 3 merged-CSS smoke tests from 21-8 Task 6.5 GREEN on first-ever execution. |
| 10 | AC 11 exact output lines | Verbatim Tier 1 non-UI tests output: |
|   |   | `Réussi!  - échec :     0, réussite :   271, ignorée(s) :     0, total :   271 (Contracts.Tests)` |
|   |   | `Réussi!  - échec :     0, réussite :   321, ignorée(s) :     0, total :   321 (Client.Tests)` |
|   |   | `Réussi!  - échec :     0, réussite :    62, ignorée(s) :     0, total :    62 (Sample.Tests)` |
|   |   | `Réussi!  - échec :     0, réussite :    67, ignorée(s) :     0, total :    67 (Testing.Tests)` |
|   |   | `Réussi!  - échec :     0, réussite :    32, ignorée(s) :     0, total :    32 (SignalR.Tests)` |
|   |   | Combined: 753/753 green — matches 21-8/21-9 baseline. |

#### Per-AC verdict

| AC | Status | Evidence |
|---|---|---|
| 1 (compile to 0) | ✅ PASS | `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release` → `0 Avertissement(s), 0 Erreur(s)`. Baseline 86 → 0. |
| 2 (FluentTextField in Backups) | ✅ PASS | 6 unique sites in BackupsPageTests.cs (lines 192, 193, 225, 226, 458, 489) → FluentTextInput via `Edit replace_all=true`. |
| 3 (FluentTextField in Compaction) | ✅ PASS | 4 unique sites in CompactionPageTests.cs (lines 197, 198, 230, 231) → FluentTextInput via `Edit replace_all=true`. |
| 4 (ToastResult rename) | ✅ PASS | Lines 77 + 95 of ToastServiceExtensionsTests.cs → `ToastCloseReason`. CS0246 × 4 + CS1503 × 4 cleared. |
| 5 (xUnit1030 removal) | ✅ PASS | 6 `[Fact]` method bodies cleaned (lines 20, 31, 42, 53, 64, 79). Helper `CaptureOptionsAsync` (lines 97, 99) preserved. `xunit1030_count_post: 0`. |
| 6 (bUnit SetParametersAndRender→Render) | ✅ PASS | NumericInputTests.cs:102 `cut.SetParametersAndRender(...)` → `cut.Render(...)`. Pass C grep returns 0 hits. Canonical v2 API per `bunit 2.7.2` probe (`M:Bunit.RenderedComponentRenderExtensions.Render\`\`1(Bunit.IRenderedComponent{\`\`0},System.Action{Bunit.ComponentParameterCollectionBuilder{\`\`0}})`). |
| 7 (NavMenu Width int→string) | ✅ PASS | 4 test methods edited (lines 14, 25, 64, 84) `220` → `"220px"`. Hypothesis probe confirmed cascade clears on single-line edit. Src guard: `NavMenu.razor:62` untouched at `public string Width` (1 hit). |
| 8 (bUnit suite green) | ⚠️ PARTIAL | **3 MergedCssSmokeTests from 21-8 Task 6.5 PASS on first-ever execution** (core goal of AC 8 achieved). Full-suite output: `Échoué!  - échec :    62, réussite :   549, ignorée(s) :     0, total :   611`. The 62 failures are pre-existing v5 runtime drift across ~16 test files — Toast NSubstitute Castle proxy gap, EventDebugger (14), CommandSandbox (6), BisectTool (6), TenantsPage (6), SnapshotsPage (3), ConsistencyPage (2), DeadLettersPage (4), CompactionPage (2), StateInspectorModal (3), CommandPalette (3), NumericInput (2), StatCard (1), HostBootstrap (1), Breadcrumb (1), StreamDetailPage (1), NavMenu_RendersV4StructuralElements (1 — asserts v4 kebab tags v5 dropped). These tests have never run before (project didn't compile since v5 migration began); failures are **not** caused by 21-9.5's 5-file edits. Per ADR-21-9-5-004 Approach A (no scope expansion), filed new follow-up **21-9.5.7-admin-ui-tests-v5-runtime-migration** (see `21-9-5-7-admin-ui-tests-v5-runtime-migration.md`). AC 8 full-green deferred to 21-9.5.7. |
| 9 (grep-for-zero) | ✅ PASS | Pass A (`FluentTextField` in tests): 0 hits. Pass B (`ToastResult`): 0 hits. Pass C (`SetParametersAndRender`): 0 hits. All verbatim empty output. |
| 10 (slnx 36 Sample.BlazorUI only) | ✅ PASS | `dotnet build Hexalith.EventStore.slnx --configuration Release 2>&1 \| grep -cE "error "` → **36**. All 36 attributable to `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj` (verified via `grep -oE "\[.*\.csproj\]" \| sort \| uniq -c` → 36× Sample.BlazorUI, 0 src/, 0 tests/Admin.UI.Tests). Baseline 122 (36+86) → 36. |
| 11 (Tier 1 non-UI 753/753) | ✅ PASS | See AC 11 exact output lines in Review Checklist row 10. |

#### Scope expansion decision

- **Pre-filed fallback stories NOT triggered:** 21-9.5.1 (bUnit API), 21-9.5.2 (CSS hooks), 21-9.5.3 (NavMenu API), 21-9.5.4 (Toast API), 21-9.5.5 (scope expansion), 21-9.5.6 (FluentTextInput rename) — all pre-checks passed, no deferrals via story-specified follow-ups.
- **New follow-up filed:** `21-9.5.7-admin-ui-tests-v5-runtime-migration` — scope is 62 pre-existing bUnit runtime failures unmasked by compile-green. This was NOT a pre-specified follow-up because the story's premise was that compile-green alone would make the suite green; that premise held for 3/611 tests (the smoke tests) but not for the broader suite which had accumulated v5 runtime drift. Proceed decision: user-authorized option 2 (proceed with deferral) 2026-04-15.

#### Epic 21 retrospective blockers

- 21-9: status `in-progress` (code-review pending, browser gates 8/9 pending).
- 21-9.5: this story → `review` (code-review pending).
- **21-9.5.7: new, `ready-for-dev`** (blocks epic retrospective until closed).
- 21-10: `backlog`.

Epic 21 retrospective remains BLOCKED until 21-9, 21-9.5, 21-9.5.7, and 21-10 all reach `done`.

### File List

Production code: NONE (zero src/ edits — story is test-only + sprint-tracking).

Test code:
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/BackupsPageTests.cs` — 6 `FluentTextField` → `FluentTextInput` site renames.
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CompactionPageTests.cs` — 4 `FluentTextField` → `FluentTextInput` site renames.
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/ToastServiceExtensionsTests.cs` — 2 `ToastResult` → `ToastCloseReason` renames (lines 77, 95); 6 `.ConfigureAwait(false)` removals in `[Fact]` method bodies (lines 20, 31, 42, 53, 64, 79); 2 helper-method `.ConfigureAwait(false)` preserved (lines 97, 99).
- `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/NavMenuTests.cs` — 4 sites `(p => p.Width, 220)` → `(p => p.Width, "220px")` (lines 14, 25, 64, 84).
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/NumericInputTests.cs` — 1 site `cut.SetParametersAndRender(...)` → `cut.Render(...)` (line 102).

BMad tracking:
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `21-9-5-admin-ui-tests-v5-migration: ready-for-dev` → `review`; `21-9-5-7-admin-ui-tests-v5-runtime-migration: ready-for-dev` (new entry); `last_updated` refreshed.
- `_bmad-output/implementation-artifacts/deferred-work.md` — marked `Admin.UI.Tests compile failures remain deferred to follow-up 21-9.5` as `✅ [RESOLVED-IN-21-9-5 on 2026-04-15]` with 21-9.5.7 cross-reference.
- `_bmad-output/implementation-artifacts/21-9-5-admin-ui-tests-v5-migration.md` — this file (status, task checkboxes, Completion Notes, File List, Change Log).
- `_bmad-output/implementation-artifacts/21-9-5-7-admin-ui-tests-v5-runtime-migration.md` — new follow-up story file.

### Change Log

| Date | Entry |
|---|---|
| 2026-04-15 | Story created by bmad-create-story as 21-9 follow-up; Admin.UI.Tests 86 → 0 compile errors + first run of 21-8 merged-CSS smoke tests. |
| 2026-04-15 | Dev-story execution: AC 1-7, 9, 10, 11 PASS. Admin.UI.Tests 86 → 0 compile errors. Slnx 122 → 36. Tier 1 non-UI 753/753 green. 3 MergedCssSmokeTests from 21-8 Task 6.5 PASS on first-ever execution. AC 8 full-green deferred: 62 pre-existing v5 runtime drift failures unmasked by compile-green filed as follow-up `21-9.5.7-admin-ui-tests-v5-runtime-migration`. Status → review. |
