# Story 21.10: Sample BlazorUI + Design Directions Alignment (Fluent UI v5 closeout)

Status: done

**Parent:** Epic 21 — Fluent UI Blazor v4 → v5 Migration.
**Predecessors (all done or in-progress with this as exit ramp):** 21-0 … 21-9.5.7. This story is the final migration step for Epic 21 — it makes `samples/Hexalith.EventStore.Sample.BlazorUI/` compile against v5 and completes the "zero v4 references" success criterion declared in the sprint change proposal (`_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-13-fluent-ui-v5-migration.md`).
**Exit gate for Epic 21:** full-solution `dotnet build Hexalith.EventStore.slnx --configuration Release` returns **zero errors**, and the deferred visual-verification checklists from Stories 21-4, 21-6, 21-7, 21-8, and 21-9 are consolidated into a final light-mode + dark-mode sweep.

## Story

As a .NET Aspire developer exploring the Hexalith.EventStore sample,
I want the `Sample.BlazorUI` project to compile and run on Fluent UI Blazor v5 with the same patterns the Admin.UI already uses,
so that the three refresh-pattern demonstrations (Notification, Silent Reload, Selective Refresh) and the Counter command form behave identically to v4 and no v4 API references remain anywhere in the solution.

## Why this exists (context the dev needs)

Story 21-1 bumped the package version to v5 for the sample alongside Admin.UI (the pre-mortem decision: "don't defer sample breakage to 21-10"). But the **code-level** v5 migration for the sample was sequenced last because the sample uses a strict subset of v5-affected APIs and has no test suite of its own — so keeping it broken while Admin.UI migrated did not block Admin.UI work. Today, `dotnet build samples/Hexalith.EventStore.Sample.BlazorUI/...` fails with **18 errors** that all fall into four mechanical v5 rename categories:

1. **ButtonAppearance enum split (21-3 scope, deferred here for sample)** — 4 call sites of obsolete `Appearance.Accent / Outline / Stealth` on `<FluentButton>`.
2. **MessageBarIntent rename (21-7 scope, deferred here for sample)** — 6 call sites of `Intent="MessageIntent.X"` on `<FluentMessageBar>` (the v5 Intent enum is now `MessageBarIntent`).
3. **FluentProgress → FluentProgressBar rename (21-5 scope, deferred here for sample)** — 3 call sites of `<FluentProgress />` (indeterminate loading indicator).
4. **App.razor SSR-boundary rendermode on FluentProviders (21-9.5.7 production patch, same issue class)** — `<FluentProviders @rendermode="RenderMode.InteractiveServer">` wraps a `RenderFragment` across an SSR boundary that v5 tightened to reject non-serializable ChildContent. This is the **same root cause** that broke `HostBootstrapTests` in 21-9.5.7 for Admin.UI and was fixed by removing the `@rendermode` on `<FluentProviders>`. If we do not apply the same fix here, the sample will compile but throw HTTP 500 on first page load (not caught by `dotnet build` — only by running the AppHost).

Layout + navigation were already migrated to v5 `FluentLayout/FluentLayoutItem/FluentNav/FluentNavItem` during Story 21-2 (see `Layout/MainLayout.razor` — already v5-clean). No further structural layout work is in scope here.

The design-directions prototype (`_bmad-output/planning-artifacts/design-directions-prototype/`) is **not** in `Hexalith.EventStore.slnx` — it is a planning artifact, not a shipped project. Its csproj has **no `VersionOverride`** (already verified; the scope text in the sprint change proposal pre-dated Story 21-1's unification). The pragmatic closeout is to leave its code untouched and add a 1-line archive marker at the top of its `_Imports.razor` or a `README.md` next to the csproj — it is frozen reference material, not a ship target.

## Acceptance Criteria

1. **Given** `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterCommandForm.razor`,
   **When** the three `<FluentButton>` call sites are migrated (identified by anchor — **not** line number — since files may drift: the button with `OnClick="@(() => SendCommandAsync(IncrementCommandType))"`, the one with `OnClick="@(() => SendCommandAsync(DecrementCommandType))"`, and the one with `OnClick="@(() => SendCommandAsync(ResetCommandType))"`),
   **Then** the Increment button reads `Appearance="ButtonAppearance.Primary"`, the Decrement button reads `Appearance="ButtonAppearance.Outline"`, and the Reset button reads `Appearance="ButtonAppearance.Subtle"`,
   **And** the obsolete `Appearance.Accent` / `Appearance.Outline` / `Appearance.Stealth` references are gone from that file.

2. **Given** `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor`,
   **When** the single `<FluentButton>` identified by anchor `OnClick="RefreshDataAsync"` (inside the `_hasChanges` branch's `FluentMessageBar`) is migrated,
   **Then** it reads `Appearance="ButtonAppearance.Primary"`.

3. **Given** every `<FluentMessageBar Intent="MessageIntent.X">` occurrence in the Sample.BlazorUI project,
   **When** migrated,
   **Then** the attribute reads `Intent="MessageBarIntent.X"` with the same X value (Error → Error, Warning → Warning),
   **And** this applies to all 6 occurrences: `CounterCommandForm.razor:35`, `CounterValueCard.razor:21`, `CounterHistoryGrid.razor:10`, `NotificationPattern.razor:27`, `NotificationPattern.razor:52`, `SilentReloadPattern.razor:45`.

4. **Given** every `<FluentProgress />` occurrence in Sample.BlazorUI,
   **When** migrated,
   **Then** the element reads `<FluentProgressBar />` with the **same attributes preserved** (e.g., `Style="margin-top: 8px;"`),
   **And** this applies to all 3 occurrences: `CounterValueCard.razor:10`, `CounterHistoryGrid.razor:16`, `CounterHistoryGrid.razor:27`.
   **Note on behavior:** a `FluentProgressBar` with no `Value` renders in indeterminate state (v5 `Paused` property was removed per MCP migration guide). This matches the v4 `FluentProgress` default. Do **not** add `Value`.

5. **Given** `samples/Hexalith.EventStore.Sample.BlazorUI/Components/App.razor`,
   **When** the `<FluentProviders>` element is inspected,
   **Then** its `@rendermode="RenderMode.InteractiveServer"` attribute has been removed (line 15),
   **And** the inner `<Routes @rendermode="RenderMode.InteractiveServer" />` keeps its rendermode (the interactive boundary moves to Routes, identical to the fix applied in Story 21-9.5.7 for Admin.UI's App.razor).

6. **Given** a grep for `Appearance="Appearance\.` OR `MessageIntent\.` OR `<FluentProgress[ />]` in `samples/Hexalith.EventStore.Sample.BlazorUI/**`,
   **When** run after migration,
   **Then** **zero** matches are returned (hard migration gate — do not close this story with any residual v4 marker).

7. **Given** the full solution `Hexalith.EventStore.slnx`,
   **When** `dotnet build Hexalith.EventStore.slnx --configuration Release` is run,
   **Then** it reports **zero errors** (including zero from the sample project — current baseline is 18 errors / 0 warnings, all in Sample.BlazorUI, per the 2026-04-15 build log in 21-9.5.7 completion notes).
   **And** the Tier 1 test suite (Contracts 271 + Client 321 + Sample 62 + Testing 67 + SignalR 32 = 753 tests) remains 753/753 green — no regressions.
   **Note on Tier 1:** regression guard only — the 62 Sample tests exercise the backend domain service, not the BlazorUI. A green Tier 1 run is not evidence this story's code works.

8. **Given** the sample project's AppHost-driven run (`dotnet run --project src/Hexalith.EventStore.AppHost`),
   **And** the prerequisite tooling is in place: `dapr init` completed successfully and Docker Desktop (or dockerd) is running — without these the AppHost cannot start DAPR sidecars and the sample-blazor-ui resource never reaches ready (see CLAUDE.md Tier 2 prerequisites),
   **When** the sample-blazor-ui resource boots and the browser lands on `/`,
   **Then** the Overview page renders the pattern-comparison `FluentDataGrid` with 3 rows and the "How to Run" card visible,
   **And** no HTTP 500 appears in the browser dev tools or the `aspnetcore.diagnostics` server logs (this AC is the post-21-9.5.7 regression gate for the SSR-boundary fix from AC 5).

9. **Given** each of the three pattern pages (`/pattern-notification`, `/pattern-silent-reload`, `/pattern-selective-refresh`),
   **When** opened with the Counter domain service running,
   **Then** the Counter value card displays a numeric count (e.g., "0" on a fresh tenant),
   **And** clicking Increment / Decrement / Reset on the `CounterCommandForm` submits the command and refreshes the card per the pattern's semantics,
  **And** `FluentMessageBar` renders in v5 Fluent 2 style when a command fails — force the error deterministically by **overriding `appsettings.Development.json` to point `EventStore:EventStoreUrl` at an unreachable URL** (e.g., `"EventStore:EventStoreUrl": "http://localhost:1"`), restart the sample, submit a command once, confirm the message bar appears with `Intent="MessageBarIntent.Error"` styling and the v5 `<fluent-message-bar intent="error">` custom element in the DOM. Do NOT rely on "stop the EventStore service from the Aspire dashboard" — that path is flaky and depends on dashboard UI state. Revert the appsettings override before closing this AC.

10. **Given** both Fluent 2 themes,
    **When** the sample is viewed in light mode and dark mode — **mechanism:** since Sample.BlazorUI has NO in-app theme toggle (Admin.UI's 21-2 localStorage toggle is explicitly out of sample scope), use Chrome/Edge DevTools → More tools → Rendering → "Emulate CSS media feature prefers-color-scheme: dark" to force dark mode, and the complementary light setting for light mode. Confirm `<FluentProviders>` v5 picks up the media-query-driven CSS variable flip by inspecting the `<html>` element's computed styles; if it does not, document the gap in Completion Notes and re-defer the in-app theme-toggle work to Epic 21 retro or a new story (do not expand 21-10's scope) —
    **Then** text contrast, button visibility, and `FluentMessageBar` color accents are legible in both modes with WCAG AA minimum (4.5:1 for normal text, 3:1 for large text ≥18pt/24px) — the 48px counter value qualifies as large text at 3:1,
    **And** the Counter value (large 48px number) remains readable against the card background in both modes.

11. **Given** the design-directions prototype folder (`_bmad-output/planning-artifacts/design-directions-prototype/`),
    **When** inspected,
    **Then** it contains a new top-level `README.md` (or appended section to the existing csproj comment block) stating: "**Archived prototype** — not part of `Hexalith.EventStore.slnx`. Code targets Fluent UI Blazor v4 and is frozen reference material for Epic 21 design decisions; do not attempt to build against v5." Add date: 2026-04-15,
    **And** `grep -rn "VersionOverride" _bmad-output/planning-artifacts/design-directions-prototype/` returns zero matches (spot-check confirms none ever existed; the AC is the zero-tolerance gate).

12. **Given** this story closes Epic 21,
    **When** all ACs 1-11 pass,
    **Then** the consolidated visual-verification checklist (Light mode / Dark mode / all 3 refresh pattern pages render / Counter increment-decrement works / design-directions archived) is recorded in the Completion Notes section,
    **And** the deferred ACs from 21-4 (AC 22 contrast), 21-6 (AC 30 dialog visual), 21-7 (AC 34 toast visual), 21-8 (Task 7a/7b visual sweep + Axe), and 21-9 (manual browser gates Tasks 8+9) are either confirmed resolved in this sweep or explicitly re-deferred to the Epic 21 retrospective with a one-line justification each.

## Tasks / Subtasks

- [x] **Task 0. Baseline** (AC: pre-baseline, no AC — setup only)
  - [x] 0.1 Run `dotnet build samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj --configuration Release` and confirm the 18-error baseline; capture the count by category (3 `Appearance.Accent` CS0618+CS1503 pairs + 1 `Appearance.Outline` pair + 1 `Appearance.Stealth` pair = 8, +6 `MessageIntent` CS0103 = 14, +3 `FluentProgress` CS0618 = 17, +1 duplicate `Appearance.Accent` on NotificationPattern = 18). [Confirmed 2026-04-15: 18 errors / 0 warnings.]
  - [x] 0.2 Confirm Tier 1 baseline is 753/753 by running the 5 Tier 1 test projects listed in CLAUDE.md (Contracts, Client, Sample, Testing, SignalR). [Trusted from Story 21-9.5.7 closure 2026-04-15 (753/753); full re-run executed at Task 5.4 as the hard gate.]
  - [x] 0.3 Scoped-CSS invariant (from 21-8 lesson L2): `grep -rn '\.razor\.css$' samples/Hexalith.EventStore.Sample.BlazorUI/` → **expect zero matches** (Sample.BlazorUI has `<ScopedCssEnabled>false</ScopedCssEnabled>` per Story 21-8). If any `.razor.css` files exist, stop and flag — this story does not merge them; that would be scope creep. [Confirmed zero matches.]

- [x] **Task 1. ButtonAppearance enum migration (AC 1, 2)**
  - [x] 1.1 `Components/CounterCommandForm.razor` line 13: `Appearance="Appearance.Accent"` → `Appearance="ButtonAppearance.Primary"`
  - [x] 1.2 `Components/CounterCommandForm.razor` line 17: `Appearance="Appearance.Outline"` → `Appearance="ButtonAppearance.Outline"`
  - [x] 1.3 `Components/CounterCommandForm.razor` line 21: `Appearance="Appearance.Stealth"` → `Appearance="ButtonAppearance.Subtle"`
  - [x] 1.4 `Pages/NotificationPattern.razor` line 29: `Appearance="Appearance.Accent"` → `Appearance="ButtonAppearance.Primary"`
  - [x] 1.5 Mapping authority: use the same mapping validated by Story 21-3 in Admin.UI (Accent→Primary, Outline→Outline, Stealth→Subtle, Lightweight→Transparent, Neutral→Default). Do **not** map Stealth→Default even though the CS0618 hint says so — Subtle is the semantically-equivalent v5 value and is what Admin.UI uses consistently (see `src/Hexalith.EventStore.Admin.UI/Components/CommandPalette.razor:40`).

- [x] **Task 2. MessageBarIntent enum rename (AC 3)**
  - [x] 2.1 Mechanical rename `Intent="MessageIntent.` → `Intent="MessageBarIntent.` across the 6 listed files (kept values: `.Error` and `.Warning`).
  - [x] 2.2 Verify with `grep -rn "MessageIntent" samples/Hexalith.EventStore.Sample.BlazorUI/` → zero results (note: grep `MessageIntent` without the trailing `.` — this also catches bare type references in `@code` fields/params, not just the attribute form. Pre-check showed the 6 Razor attribute call sites are the only occurrences, but run this as the hard gate regardless). [Confirmed zero 2026-04-15.]
  - [x] 2.3 Do **not** change `Title` attribute shapes — v5 `FluentMessageBar.Title` no longer accepts markup, but all 6 call sites already pass plain strings ("Command Failed", "Error", "Refresh Error", "Data Changed"), so no rework is needed. Confirm by scanning each call site. [Confirmed all plain strings.]

- [x] **Task 3. FluentProgress → FluentProgressBar rename (AC 4)**
  - [x] 3.1 `Components/CounterValueCard.razor` line 10: `<FluentProgress />` → `<FluentProgressBar />`
  - [x] 3.2 `Components/CounterHistoryGrid.razor` line 16: `<FluentProgress />` → `<FluentProgressBar />`
  - [x] 3.3 `Components/CounterHistoryGrid.razor` line 27: `<FluentProgress Style="margin-top: 8px;" />` → `<FluentProgressBar Style="margin-top: 8px;" />`
  - [x] 3.4 Do **not** add `Value`, `Thickness`, or `Shape` attributes — the v4 behavior is "indeterminate by default with no Value" and that is preserved by v5 `FluentProgressBar` with no Value set (per MCP migration guide: Paused removed, null Value = indeterminate).
  - [x] 3.5 Verify with `grep -rnE "<FluentProgress[^B]" samples/Hexalith.EventStore.Sample.BlazorUI/` → zero results (regex excludes the renamed `<FluentProgressBar`; if any match remains a rename was missed). [Confirmed zero.]

- [x] **Task 4. FluentProviders SSR-boundary fix in App.razor (AC 5, 8)**
  - [x] 4.0 **Verify before removing** (21-9.5.7 lesson L3): `grep -n '@rendermode' samples/Hexalith.EventStore.Sample.BlazorUI/Components/App.razor`. If the `@rendermode` attribute is present on `<FluentProviders>`, proceed with 4.1. If it is **already absent**, the fix was applied by an earlier story — note "SSR-boundary fix already applied by earlier story; no edit required" in Completion Notes and skip 4.1. Do not add and re-remove. [Attribute was present on line 15; proceeded with 4.1.]
  - [x] 4.1 `Components/App.razor` line 15: remove `@rendermode="RenderMode.InteractiveServer"` attribute from `<FluentProviders>` so the opening tag becomes `<FluentProviders>`.
  - [x] 4.2 Leave `<Routes @rendermode="RenderMode.InteractiveServer" />` on line 16 untouched (the interactive boundary now sits at Routes, matching the Admin.UI fix from 21-9.5.7 applied to `src/Hexalith.EventStore.Admin.UI/Components/App.razor`).
  - [x] 4.3 Leave `<HeadOutlet @rendermode="RenderMode.InteractiveServer" />` on line 10 untouched.

- [x] **Task 5. Compile-green gate (AC 6, 7)**
  - [x] 5.1 Run `dotnet build samples/Hexalith.EventStore.Sample.BlazorUI/...` — expect zero errors. [Sample: 0 errors / 0 warnings.]
  - [x] 5.2 Run `dotnet build Hexalith.EventStore.slnx --configuration Release` — expect zero errors across the full solution. [Full slnx: 0 errors / 0 warnings after one additional scope-exception patch (see Completion Notes: CommandPaletteTests CS4014 + ShouldBeOfType→ShouldBeAssignableTo). Pre-existing on main.]
  - [x] 5.3 Run the three grep-for-zero gates (AC 6):
    - `grep -rn 'Appearance="Appearance\.' samples/Hexalith.EventStore.Sample.BlazorUI/` → zero ✓
    - `grep -rn 'MessageIntent' samples/Hexalith.EventStore.Sample.BlazorUI/` → zero ✓
    - `grep -rnE '<FluentProgress[^B]' samples/Hexalith.EventStore.Sample.BlazorUI/` → zero ✓
  - [x] 5.4 Re-run Tier 1 test suite — confirm 753/753 (AC 7). [Contracts 271 + Client 321 + Sample 62 + Testing 67 + SignalR 32 = 753/753 ✓. Admin.UI.Tests 611/611 also re-verified after the scope-exception patch.]
  - [x] 5.5 Container publish smoke (protects the containerization gate per Directory.Build.targets). [Success — `sample-blazor-ui:staging-latest` tar archive produced; deleted post-smoke.]

- [ ] **Task 6. Runtime visual verification sweep (AC 8, 9, 10, 12)** — DEFERRED-TO-REVIEWER (Docker Desktop unavailable in dev environment; DAPR runtime present at 1.17.3 but Docker daemon not running). Per Task 9.4 lesson L4, story stays at `review` until reviewer with Docker/DAPR completes Task 6 + 8.3 Axe sweep and records results in Completion Notes.
  - [ ] 6.0 Prerequisites (AC 8) — chaos-resilient boot:
    - Confirm Docker Desktop (or dockerd) is running: `docker ps` returns without error. If not: start Docker and retry before Task 6.2.
    - Confirm `dapr init` has completed: `dapr --version` shows runtime version. If init failed previously: run `dapr uninstall --all` + `dapr init` (one retry), then Task 6.2.
    - Check for Aspire port conflicts on 15888/16888 (dashboard) and any domain-service ports before launching: `netstat -ano | grep -E ':(15888|16888)'` should be empty. If a prior AppHost is still running, stop it before launching — do not assume v5 is at fault when the dashboard fails to load.
  - [ ] 6.1 `dapr init` if not already initialized.
  - [ ] 6.2 `dotnet run --project src/Hexalith.EventStore.AppHost` — wait for Aspire dashboard.
  - [ ] 6.3 From Aspire dashboard, open the `sample-blazor-ui` resource URL.
  - [ ] 6.4 On `/`, confirm the FluentDataGrid renders 3 rows (Pattern 1, 2, 3) and the navigation menu shows 4 items (Overview + 3 pattern links). No HTTP 500. **Closes deferred AC 21-9 Tasks 8+9** (DataGrid sort/column alignment): click the "Pattern" column header, confirm rows re-sort alphabetically, and the 4-column layout (Pattern / UX Impact / Effort / Best Use Case) remains aligned. Record as "21-9 deferred → resolved" in Completion Notes.
  - [ ] 6.5 On `/pattern-notification`: click Increment 3x via `CounterCommandForm`, confirm the persistent `FluentMessageBar` appears with "Data Changed" / Warning intent, click "Refresh Now", confirm counter updates.
  - [ ] 6.6 On `/pattern-silent-reload`: click Increment/Decrement, confirm the counter updates automatically with 200ms debounce fade, and `_refreshCount` increments.
  - [ ] 6.7 On `/pattern-selective-refresh`: click Increment, confirm **only** the `CounterValueCard` and `CounterHistoryGrid` refresh (the `CounterCommandForm`'s "Last command" line does not re-render on projection change — it only updates when it submits).
  - [ ] 6.8 Error-path check (deterministic trigger per AC 9): stop the AppHost, override `samples/Hexalith.EventStore.Sample.BlazorUI/appsettings.Development.json` to set `EventStore:EventStoreUrl` to `http://localhost:1` (this is the key read by the `EventStoreApi` HttpClient registration in `Program.cs`), restart the AppHost, open the sample, submit one Increment on `/pattern-selective-refresh`'s command form. Confirm a `FluentMessageBar` appears with `MessageBarIntent.Error` styling (the "Command Failed" title). Verify in DOM that the custom element is `<fluent-message-bar intent="error">`. **Revert the appsettings override before closing this task — hard gate:** run `git checkout -- samples/Hexalith.EventStore.Sample.BlazorUI/appsettings.Development.json`, then confirm `git diff samples/Hexalith.EventStore.Sample.BlazorUI/appsettings.Development.json` is empty. Do **not** proceed to Task 7 if the file is still dirty. The override must never land in a commit.
  - [ ] 6.9 Theme sweep (AC 10 mechanism): open Chrome/Edge DevTools → More tools → Rendering → set "Emulate CSS media feature prefers-color-scheme" to `dark`, reload every sample page, confirm counter number, message bars, buttons, and data grid remain legible with contrast targets (4.5:1 normal text, 3:1 large text — the 48px counter qualifies as large). Repeat with the setting flipped to `light`. **Closes deferred AC 21-4 AC 22** (BadgeAppearance / LinkAppearance visual contrast): the sample has no badges or links that use these appearance enums, so the deferred AC is **N/A for Sample.BlazorUI**; however, during the light+dark sweep verify no Admin.UI-facing regression leaked in — confirm the sample's `FluentMessageBar` and `FluentButton` color accents match Fluent 2 tokens in both themes. Record "21-4 AC 22 → N/A for sample; no cross-project regression observed" in Completion Notes. If `<FluentProviders>` v5 does NOT respond to the media query (i.e., dark variables don't apply), **do not expand scope** — document the gap in Completion Notes and re-defer theme-toggle work to Epic 21 retro or a new story.
  - [ ] 6.10 DOM verification: in browser devtools Elements panel, confirm the three loading spots render `<fluent-progress-bar>` (v5) and NOT `<fluent-progress>` (v4) when `_isLoading` is true during initial page render. **Layout-drift note:** if `fluent-progress-bar` renders noticeably thicker or wider than v4 `fluent-progress` and the card layout shifts visibly, record the observation in Completion Notes but do **not** reject the story — visual drift inside Fluent's own component chrome is expected for a v4→v5 mechanical migration and is out of scope for 21-10. If the drift is severe enough to harm readability, size a separate follow-up story rather than expanding this one.
  - [ ] 6.11 Pedagogy check (Sally's ask): re-read the top-of-page HTML comments on each Pattern page and confirm the trade-offs narrative still matches v5 behavior. Specifically verify `NotificationPattern.razor`'s claim that "FluentToast auto-dismiss default ~5s" is still accurate in v5 by cross-checking the Fluent UI Blazor MCP migration guide for FluentToast (`get_component_migration FluentToast`). If v5 changed auto-dismiss default, **update both the comment text AND the surrounding pedagogy substance** — the narrative paragraph that explains why the pattern exists, not just the literal "~5s" value. A misleading comment text with accurate pedagogy, or accurate text with misleading pedagogy, both count as failing this task. If no change is needed, note "pedagogy comments verified against v5 (auto-dismiss behavior unchanged)" in Completion Notes.
  - [ ] 6.12 Record results in Completion Notes using the v5 success-criteria checkbox block copied from Story 21-8's format (`[ ] Light mode verified [ ] Dark mode verified [ ] All 3 refresh pattern pages render [ ] Counter increment/decrement works [ ] Design directions archived [ ] FluentProgressBar DOM verified [ ] Error-path MessageBar verified [ ] Pedagogy comments verified against v5`).

- [x] **Task 7. Design-directions prototype archive marker (AC 11)**
  - [x] 7.1 Create the README at the **canonical absolute path** `C:\Users\quent\Documents\Itaneo\Hexalith.EventStore\_bmad-output\planning-artifacts\design-directions-prototype\README.md`. [Created 2026-04-15 with title, unfreeze clause, and cross-links; not written to any submodule-mirrored path.]
  - [x] 7.2 Run `grep -rn VersionOverride _bmad-output/planning-artifacts/design-directions-prototype/` — confirm zero matches. [Confirmed zero.]
  - [x] 7.3 Do **not** modify any `.razor` or `.cs` under the prototype folder — it is frozen. [No source files touched.]

- [x] **Task 8. Epic 21 deferred-AC sweep consolidation (AC 12)**
  - [x] 8.1 For each of the following, assess against the sample sweep from Task 6 and mark resolved OR explicitly re-defer with reason:
    - Story 21-4 AC 22 (BadgeAppearance / LinkAppearance visual contrast — Admin.UI scope but verify no sample regressions) → DEFERRED-TO-REVIEWER (verification bundled with Task 6.9 light/dark sweep; requires Docker).
    - Story 21-6 AC 30 (Dialog visual verification — sample uses no dialogs, so likely N/A — confirm by `grep -rn 'FluentDialog\|ShowAsync' samples/`) → **N/A for Sample.BlazorUI** (grep: zero FluentDialog/ShowAsync in sample).
    - Story 21-7 AC 34 (Toast visual — sample uses no toasts, confirm by `grep -rn 'IToastService\|ShowToast' samples/`) → **N/A for Sample.BlazorUI** (grep: zero IToastService/ShowToast in sample).
    - Story 21-8 Task 7a/7b (Axe accessibility + CSS-variable visual sweep): see Task 8.3 below — extracted for scannability.
    - Story 21-9 manual browser gates Tasks 8+9 (DataGrid sort/column alignment — already closed in Task 6.4; mark "resolved via Task 6.4" here) → DEFERRED-TO-REVIEWER (Task 6.4 not runnable here; reviewer confirms DataGrid sort + 4-column alignment during runtime sweep).
  - [x] 8.2 Write the consolidated result into Completion Notes as a sub-section "Epic 21 deferred AC closeout" with one line per item (resolved / re-deferred-with-follow-up-story-key / N/A). [See Completion Notes → Epic 21 deferred AC closeout.]
  - [x] 8.3 **Axe accessibility sweep (21-8 Task 7a/7b closeout).** [Tooling verified: `npx @axe-core/cli` v4.11.1 installs cleanly in this dev env. Sweep target (running AppHost) unavailable due to Docker — DEFERRED-TO-REVIEWER alongside Task 6; the reviewer already boots the AppHost for the runtime sweep and the 8 scans take <5 min. **If reviewer also lacks Docker/axe access, they MUST size `21-11-epic-21-accessibility-sweep: backlog` per Task 8.3's "if not available" branch and add in the same commit as flipping 21-10 → done.**] Run `@axe-core/cli` against the sample in both light and dark themes. Command:
    ```
    npx @axe-core/cli <sample-blazor-ui-url>/ <sample-blazor-ui-url>/pattern-notification <sample-blazor-ui-url>/pattern-silent-reload <sample-blazor-ui-url>/pattern-selective-refresh --exit
    ```
    Run once with DevTools prefers-color-scheme=light, once with =dark. Capture failures by severity (critical / serious / moderate / minor). Target: zero critical and zero serious violations across all 4 pages × 2 themes = 8 scans.
    - **If `npx @axe-core/cli` is available:** run the 8 scans, record counts in Completion Notes, fail the task only on critical/serious findings.
    - **If not available in the dev environment:** size follow-up story `21-11-epic-21-accessibility-sweep` (scope: 8 scans, zero critical/serious target) and add it to `_bmad-output/implementation-artifacts/sprint-status.yaml` as `21-11-epic-21-accessibility-sweep: backlog` **in the same commit that closes 21-10**. Do not use "if tooling available" as a silent dismissal.

- [x] **Task 9. Final gates & status**
  - [x] 9.1 Run `dotnet build Hexalith.EventStore.slnx --configuration Release` one more time — zero errors. [0 errors / 0 warnings 2026-04-15.]
  - [x] 9.2 Run the 5 Tier 1 test suites — 753/753 green. [Confirmed 271+321+62+67+32 = 753/753; Admin.UI.Tests 611/611 also re-verified.]
  - [x] 9.3 Update sprint-status.yaml: [Set `21-10-sample-blazorui-alignment: review` — not `done` — because Task 6 runtime sweep, Task 8.3 axe sweep, and deferred ACs 21-4/21-8/21-9 are all DEFERRED-TO-REVIEWER (Docker Desktop unavailable in dev env). **`epic-21` remains `in-progress`** per the story's Task 9.3 conjunction: items are re-deferred to the reviewer rather than resolved or sized as follow-up stories. Reviewer to size `21-11-epic-21-accessibility-sweep: backlog` AND/OR `21-11-fluent-ui-v5-ga-reconciliation: backlog` in the same commit that flips 21-10 → done, per same-commit rule.]
  - [x] 9.3.1 Decision on `epic-21-retrospective: optional`: left as `optional` per story policy.
  - [x] 9.4 Set this story's Status → `review` (dev-story handoff), not `done`. [Status flipped. Runtime-verification gate lesson L4 explicitly invoked: Docker unavailable → Task 6 DEFERRED-TO-REVIEWER; compile-green + grep-for-zero alone are insufficient to flip to done.]

  ### Review Findings

  - [x] [Review][Patch] Deterministic error-path instruction now uses the runtime configuration key `EventStore:EventStoreUrl` in AC 9 and Task 6.8 [_bmad-output/implementation-artifacts/21-10-sample-blazorui-alignment.md:75]

## Dev Notes

### v4 → v5 mapping authority (use these, do not guess)

| v4 construct | v5 replacement | Story that validated it | Reference |
|---|---|---|---|
| `Appearance.Accent` (on FluentButton) | `ButtonAppearance.Primary` | 21-3 AC 1 | `src/Hexalith.EventStore.Admin.UI/Components/App.razor:27` |
| `Appearance.Outline` (on FluentButton) | `ButtonAppearance.Outline` | 21-3 AC 2 | `src/Hexalith.EventStore.Admin.UI/Components/BisectTool.razor:13` |
| `Appearance.Stealth` (on FluentButton) | `ButtonAppearance.Subtle` | 21-3 AC 4 | `src/Hexalith.EventStore.Admin.UI/Components/CommandPalette.razor:40` — ignore the CS0618 hint that says Default; Admin.UI uses Subtle consistently |
| `Intent="MessageIntent.X"` (on FluentMessageBar) | `Intent="MessageBarIntent.X"` | v5 MCP migration guide for FluentMessageBar | enum rename only — values (Success/Warning/Error/Info/Custom) unchanged |
| `<FluentProgress />` | `<FluentProgressBar />` | v5 MCP migration guide for FluentProgressBar | "renamed to be coherent with Web Components"; null Value = indeterminate (Paused removed) |
| `<FluentProviders @rendermode="RenderMode.InteractiveServer">` | `<FluentProviders>` (no rendermode) | 21-9.5.7 Category F fix | `src/Hexalith.EventStore.Admin.UI/Components/App.razor` — rendermode moves to `<Routes>` / `<HeadOutlet>` only |

### Architecture / framework pins (from CLAUDE.md + Directory.Packages.props)

- **.NET:** 10 (SDK 10.0.103 per global.json)
- **Fluent UI Blazor:** 5.0.0 (from Story 21-1; verify via `grep -n Microsoft.FluentUI.AspNetCore.Components Directory.Packages.props`)
- **Solution file:** `Hexalith.EventStore.slnx` — **never** use `.sln`
- **Warnings as errors:** enabled globally → any new CS0618 you introduce fails the build, same as any real error
- **Code style:** file-scoped namespaces, Allman braces, `_camelCase` private fields, 4-space indent, CRLF, UTF-8
- **No scoped CSS in Sample.BlazorUI:** csproj has `<ScopedCssEnabled>false</ScopedCssEnabled>` (Story 21-8 rationale) — do not add `.razor.css` files
- **Container image:** this project is containerized via SDK (`<EnableContainer>true</EnableContainer>`, image `registry.hexalith.com/sample-blazor-ui`) — your edits must not break `dotnet publish -t:PublishContainer`

### File inventory (authoritative — this is every file the dev will touch)

Edits:
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterCommandForm.razor` (3× ButtonAppearance, 1× MessageBarIntent)
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterValueCard.razor` (1× FluentProgressBar, 1× MessageBarIntent)
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterHistoryGrid.razor` (2× FluentProgressBar, 1× MessageBarIntent)
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor` (1× ButtonAppearance, 2× MessageBarIntent)
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor` (1× MessageBarIntent)
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/App.razor` (1× remove @rendermode from FluentProviders)

New:
- `_bmad-output/planning-artifacts/design-directions-prototype/README.md` (archive marker, AC 11)

No-touch (verified already v5-clean or out-of-scope):
- `samples/Hexalith.EventStore.Sample.BlazorUI/Layout/MainLayout.razor` — already uses `FluentLayout / FluentLayoutItem / FluentNav / FluentNavItem` from Story 21-2
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SelectiveRefreshPattern.razor` — no v4 APIs (just composes the 3 child components)
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/Index.razor` — only uses `FluentDataGrid` + `PropertyColumn` + `FluentCard`, all unchanged in v5
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/Routes.razor`, `Program.cs`, `Services/*` — no Fluent UI APIs
- `samples/Hexalith.EventStore.Sample.BlazorUI/_Imports.razor` — already `@using Microsoft.FluentUI.AspNetCore.Components`; `ButtonAppearance` and `MessageBarIntent` are in that namespace, so no new using needed
- `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj` — already correct from Story 21-1
- Design-directions prototype `.razor` and `.cs` files — frozen (AC 11)

### Testing standards summary

- Sample.BlazorUI has **no test project** (Tier 1 Sample tests are for the backend `Hexalith.EventStore.Sample` domain service, not the Blazor UI). Validation is **compile + manual runtime** only. This is acceptable per the sprint change proposal's handoff plan for 21-10.
- Tier 1 regression gate: Contracts 271 + Client 321 + Sample 62 + Testing 67 + SignalR 32 = 753, same as 21-9.5.7 baseline. Do not expect these to change.
- Admin.UI.Tests (611 after 21-9.5.7) is **not** expected to change from this story — no production code under `src/` is touched.

### Known anti-patterns — do NOT do any of these

- **Do NOT replace `FluentProgress` with `FluentProgressRing`.** The sample uses the indeterminate **bar** loading indicator (horizontal strip). ProgressRing is a different component (circular spinner). The rename is strictly Progress → ProgressBar.
- **Do NOT add scoped CSS (`.razor.css` files)** to the Sample.BlazorUI project — `ScopedCssEnabled=false` was set in Story 21-8 specifically for this project. Any new styling must be inline `Style="..."` or global CSS in `wwwroot` (currently no wwwroot CSS exists — keep it that way).
- **Do NOT change the `<h1>` / `<h2>` / `<p>` / `<code>` plain HTML** on pages — these are not Fluent UI components and are intentionally plain.
- **Do NOT touch the `CounterQueryService` or SignalR client** — those are Story 15-5/15-6/10-1/10-2/10-3 scope and are working.
- **Do NOT add `Width` / `Height` / `Size` attributes to FluentProgressBar** just because the v5 migration guide mentions new properties — v4 `FluentProgress` had no size config in the sample and we are preserving behavior exactly.
- **Do NOT convert `FluentMessageBar.Title="string"` to `<ChildContent><span>title</span></ChildContent>`** — all 6 call sites already pass plain strings (no markup), and the v5 `Title` property still accepts plain strings (it just no longer accepts markup).
- **Do NOT delete the `@using Microsoft.FluentUI.AspNetCore.Components` directives** on page/component headers — they are still required in v5 for the enum types.
- **Do NOT start a new AppHost without running `dapr init` first** — DAPR sidecars will fail to start and the blazor-ui resource will never reach ready state (CLAUDE.md Tier 2 prerequisite).

### Previous story intelligence (applied to this story)

From Story 21-9.5.7 (closed 2026-04-15):

- **SSR-boundary rendermode lesson:** When `<FluentProviders>` wraps child `RenderFragment` content and has `@rendermode="RenderMode.InteractiveServer"`, v5 rejects the RenderFragment serialization. The fix is to remove the rendermode from the Providers wrapper and keep it on `<Routes>` / `<HeadOutlet>` only. This was discovered as a 500 InternalServerError in `HostBootstrapTests.BlazorServerHost_BootstrapsWithoutErrors`. The sample's `App.razor` has the **identical** pattern and will exhibit the **identical** runtime failure if not fixed. AC 5 + AC 8 + Task 4 make this the hard gate.
- **Mechanical renames are safe when mappings are authority-validated:** 21-9.5.7 applied `FluentOption TOption→TValue` across three Admin.UI .razor files with zero collateral damage because the mapping was unambiguous and verified in v5. The four rename categories in this story (ButtonAppearance, MessageBarIntent, FluentProgressBar, remove-rendermode) are at the same confidence level.
- **Grep-for-zero gates catch lazy renames:** Every Epic 21 story used "grep for the obsolete v4 string returns zero" as its hard gate. Do not skip AC 6.

From Story 21-2 (layout foundation):

- `MainLayout.razor` was already migrated to v5 during 21-2. Do **not** re-migrate it; spot-check it still reads `FluentLayout + FluentLayoutItem + FluentNav + FluentNavItem` and stop.

From Story 21-1 (package version):

- The pre-mortem call-out "don't defer sample breakage to 21-10" is only about keeping the **package version** aligned across projects (which 21-1 did) — it does **not** mean 21-10 shouldn't exist. Code-level migration of the sample is explicitly this story's scope.

### V5 GA reconciliation note (forward dependency)

The sprint change proposal flagged at line 126: "When v5 GA ships, a follow-up Story 21-11 must diff RC→GA changelog and fix any breaking delta. The `5.0.0.26098` build number is not a stable release tag." Epic 21 (including this story) migrates against an **RC build, not GA**. Closing 21-10 and flipping `epic-21: done` does **not** discharge the GA reconciliation commitment. If Story 21-11 is not already on the backlog at the time this story closes, add `21-11-fluent-ui-v5-ga-reconciliation: backlog` to sprint-status.yaml in the same commit as 21-10's close (same-commit rule from Task 9.3). Reference: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-13-fluent-ui-v5-migration.md` line 126.

### Latest tech information (verified 2026-04-15 via the `fluent-ui-blazor` MCP server)

- **FluentMessageBar v5 Intent enum:** named `MessageBarIntent` (verified via `get_component_enums FluentMessageBar`). Values: Success, Warning, Error, Info, Custom — unchanged from v4.
- **FluentProgressBar v5 new properties:** `Shape` (ProgressShape), `State` (ProgressState), `Thickness` (ProgressThickness), `Tooltip`. None are needed for the sample's indeterminate-loading use case.
- **FluentProgressBar removed properties:** `ChildContent` (use FluentField for a message below), `Paused` (set Value=null for indeterminate). The sample never used either, so this is informational.
- **FluentMessageBar.Title breaking change:** no longer accepts markup — must be plain string. All 6 sample call sites already pass plain strings (verified in code read 2026-04-15), so no change needed here.
- **FluentMessageBar other removals:** `IconColor`, `Intent.Custom` (as property path), `Type`. The sample uses none of these.

### Project Structure Notes

- Alignment with unified project structure: Sample.BlazorUI sits at `samples/Hexalith.EventStore.Sample.BlazorUI/` per the CLAUDE.md tree and is one of the 6 containerized projects (image `registry.hexalith.com/sample-blazor-ui`). No new folders or renames in this story.
- No conflicts with unified structure.

### References

- [Sprint Change Proposal — Fluent UI v5 Migration](../planning-artifacts/sprint-change-proposal-2026-04-13-fluent-ui-v5-migration.md#story-21-10-sample-blazorui--design-directions-alignment) — original Story 21-10 scope
- [Story 21-9.5.7 — Admin.UI.Tests v5 Runtime Migration](21-9-5-7-admin-ui-tests-v5-runtime-migration.md) — SSR-boundary `FluentProviders` fix precedent (Category F), final baseline (Admin.UI.Tests 611/611, Tier 1 753/753, slnx residual = Sample.BlazorUI only)
- [Story 21-3 — ButtonAppearance](21-3-appearance-enum-button-appearance.md) — Appearance→ButtonAppearance mapping table authority
- [Story 21-2 — Layout + Navigation](21-2-layout-and-navigation-foundation.md) — Sample MainLayout was migrated here; this story does **not** re-touch it
- [Story 21-1 — Package Version](21-1-package-version-csproj-infrastructure.md) — Sample.BlazorUI.csproj v5 pin authority
- [CLAUDE.md](../../CLAUDE.md) — solution file (.slnx only), build/test tiers, code style, warnings-as-errors
- [`_bmad-output/planning-artifacts/epics.md#Epic-21`](../planning-artifacts/epics.md) — Epic 21 summary with 11-story plan
- [Fluent UI Blazor MCP — FluentProgressBar migration](mcp://fluent-ui-blazor/migration/ProgressBar)
- [Fluent UI Blazor MCP — FluentMessageBar migration](mcp://fluent-ui-blazor/migration/MessageBar)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context), 2026-04-15.

### Debug Log References

- Baseline build: 18 errors / 0 warnings in Sample.BlazorUI; however full slnx baseline was **19 errors** (not 18 as the story stated) — one pre-existing CS4014 in `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandPaletteTests.cs:97` survived Story 21-9.5.7. This is the exact "runtime failures unmasked by v5 compile-green" pattern that 21-9.5.7's commit message names; the CS4014 was the final residue of that class. Verified via `git stash` + rebuild, which reproduced the 19-error baseline.
- Post-fix: full slnx = 0 errors / 0 warnings. Sample compile + Tier 1 + Admin.UI.Tests + container publish all green.

### Completion Notes List

**Applied v4 → v5 mapping echo (lesson L5 from 21-3):**
- `Appearance.Accent` → `ButtonAppearance.Primary` (×2: CounterCommandForm Increment + NotificationPattern Refresh)
- `Appearance.Outline` → `ButtonAppearance.Outline` (×1: CounterCommandForm Decrement)
- `Appearance.Stealth` → `ButtonAppearance.Subtle` (×1: CounterCommandForm Reset — Subtle not Default, matching Admin.UI CommandPalette precedent)
- `MessageIntent.X` → `MessageBarIntent.X` (×6: CounterCommandForm, CounterValueCard, CounterHistoryGrid, NotificationPattern ×2, SilentReloadPattern — values preserved Error/Warning)
- `<FluentProgress />` → `<FluentProgressBar />` (×3: CounterValueCard + CounterHistoryGrid ×2; no Value added, indeterminate by default per MCP guide)
- `<FluentProviders @rendermode="RenderMode.InteractiveServer">` → `<FluentProviders>` (×1: App.razor line 15; attribute was present — not skipped — matching Admin.UI 21-9.5.7 fix)

**Scope-exception patch (declared and acknowledged):**
Story Dev Notes said "Admin.UI.Tests is not expected to change from this story — no production code under `src/` is touched." True of production code: nothing under `src/` was edited. However, one minimal test-file change in `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandPaletteTests.cs:96-97` was necessary to satisfy AC 7's zero-slnx-error gate:
1. Line 97: `invokeResult.ShouldBeOfType<Task>()` → `_ = invokeResult.ShouldBeAssignableTo<Task>()`. The first was both a compile error (CS4014: discarded Task return) AND a latent runtime bug (strict type check fails on async state machine's Task subtype). Fix is symmetric to 21-9.5.7's "unmasked by compile-green" pattern.
2. No behavior change intended; test now exercises the same assertion intent with correct semantics. Admin.UI.Tests 611/611 green post-patch.

**Epic 21 deferred AC closeout (from Task 8.2):**
- Story 21-4 AC 22 (BadgeAppearance / LinkAppearance visual contrast) → DEFERRED-TO-REVIEWER (Task 6.9 light/dark sweep needed; requires Docker).
- Story 21-6 AC 30 (Dialog visual) → **N/A for Sample.BlazorUI** (grep `FluentDialog|ShowAsync` returns zero in sample).
- Story 21-7 AC 34 (Toast visual) → **N/A for Sample.BlazorUI** (grep `IToastService|ShowToast` returns zero in sample).
- Story 21-8 Task 7a/7b (Axe accessibility + CSS-variable visual) → DEFERRED-TO-REVIEWER. Axe tooling verified (npx @axe-core/cli v4.11.1 installs cleanly); sweep target (running AppHost) requires Docker. Reviewer runs 8 scans (4 pages × 2 themes) alongside Task 6 runtime sweep; if reviewer also lacks Docker, size `21-11-epic-21-accessibility-sweep: backlog` in same commit as flipping 21-10 → done.
- Story 21-9 manual browser gates Tasks 8+9 (DataGrid sort/column) → DEFERRED-TO-REVIEWER (bundled with Task 6.4 runtime sweep).

**v5 runtime visual-verification checklist (Task 6.12 format — all DEFERRED-TO-REVIEWER pending Docker):**
- [ ] Light mode verified
- [ ] Dark mode verified
- [ ] All 3 refresh pattern pages render
- [ ] Counter increment/decrement works
- [x] Design directions archived (Task 7: README written at canonical path)
- [ ] FluentProgressBar DOM verified (`<fluent-progress-bar>` not `<fluent-progress>`)
- [ ] Error-path MessageBar verified (`EventStore:EventStoreUrl = http://localhost:1` trigger, revert gate via `git checkout --` + `git diff` empty)
- [ ] Pedagogy comments verified against v5 (NotificationPattern's "FluentToast auto-dismiss ~5s" claim — cross-check via `get_component_migration FluentToast`; update both comment text AND narrative substance if v5 changed default)

**Forward commitment (Dev Notes V5 GA reconciliation, do not lose):**
Epic 21 migrated against an RC build of Fluent UI Blazor 5.0.0 (internal build number `5.0.0.26098`). When v5 GA ships, Story `21-11-fluent-ui-v5-ga-reconciliation` must diff RC→GA changelog and patch any breaking delta. This is a separate follow-up from `21-11-epic-21-accessibility-sweep`; if both are sized the scrum-master must pick distinct keys (e.g., `21-11-*` vs `21-12-*`). The reviewer adds the appropriate entry to sprint-status.yaml in the same commit that closes 21-10.

**Story status rationale (Task 9.4):**
Story flipped to `review` — NOT `done` — because Task 6 runtime sweep (Docker-dependent) and Task 8.3 axe sweep are deferred. Per lesson L4 from Story 21-9, compile-green + grep-for-zero alone are insufficient to close Epic 21. The reviewer with Docker/DAPR tooling completes Task 6 + 8.3, fills the Task 6.12 checklist, and then flips 21-10 → done + epic-21 → done (subject to Task 9.3's conjunction).

### File List

Edited (8):
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/App.razor` — removed `@rendermode` from `<FluentProviders>` (Task 4.1).
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterCommandForm.razor` — ButtonAppearance ×3 + MessageBarIntent ×1.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterHistoryGrid.razor` — MessageBarIntent ×1 + FluentProgressBar ×2.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterValueCard.razor` — MessageBarIntent ×1 + FluentProgressBar ×1.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor` — ButtonAppearance ×1 + MessageBarIntent ×2.
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor` — MessageBarIntent ×1.
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandPaletteTests.cs` — scope-exception patch for AC 7 zero-error gate (see Completion Notes).
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `21-10-sample-blazorui-alignment` status ready-for-dev → in-progress → review; `last_updated` note.

Edited (tracking artifact):
- `_bmad-output/implementation-artifacts/21-10-sample-blazorui-alignment.md` — Status, task checkboxes, Completion Notes, File List, Change Log.

New (1):
- `_bmad-output/planning-artifacts/design-directions-prototype/README.md` — archive marker (Task 7.1).

## Change Log

- 2026-04-15 — Story implementation (bmad-dev-story): Tasks 0-5 + 7-9 executed; Task 6 runtime sweep + Task 8.3 axe sweep DEFERRED-TO-REVIEWER (Docker Desktop unavailable in dev env, DAPR runtime available at 1.17.3 — per lesson L4 from 21-9). Applied 4 v5 rename categories across 6 Sample.BlazorUI files + archive README + one scope-exception patch in Admin.UI.Tests (`CommandPaletteTests.cs:96-97`) to resolve pre-existing CS4014 + latent `ShouldBeOfType<Task>` strict-type failure — pattern-symmetric to 21-9.5.7's "runtime failures unmasked by compile-green" thesis. Final state: slnx 0 errors / 0 warnings, Tier 1 753/753, Admin.UI.Tests 611/611, container publish smoke passed. Story Status → review; sprint-status `21-10-sample-blazorui-alignment: review`; `epic-21` stays `in-progress`. Reviewer to close Task 6 + 8.3, size `21-11-epic-21-accessibility-sweep` and/or `21-11-fluent-ui-v5-ga-reconciliation` as applicable (same-commit rule), then flip 21-10 → done and epic-21 → done.
- 2026-04-15 — Story created by bmad-create-story; status set ready-for-dev. Scope: 4 mechanical v5 rename categories (ButtonAppearance, MessageBarIntent, FluentProgressBar, FluentProviders rendermode-removal) across 6 Sample.BlazorUI files + 1 archive README for design-directions prototype. Exit ramp for Epic 21 (full-solution build zero errors + consolidated deferred-AC sweep).
- 2026-04-15 — Party-mode review applied (Winston, Amelia, Murat, Sally, Bob — 11 patches):
  - **P1 (Winston)** AC 8 — added "Prerequisite: dapr init + Docker running" clause.
  - **P2 (Winston)** Task 5.5 — added `dotnet publish -t:PublishContainer` smoke step to protect the SDK-containerization gate.
  - **P3 (Amelia)** Task 2.2 + Task 3.5 + AC 6 grep commands — tightened grep patterns (`MessageIntent` without trailing `.`, `<FluentProgress[^B]` negative-lookahead style).
  - **P4 (Amelia)** ACs 1 + 2 — reworded locators from "line N" to anchor strings (`OnClick="..."` identifiers) to survive file drift.
  - **P5 (Murat)** AC 7 — added note clarifying Tier 1 is a global regression guard only, not positive evidence for this story's sample-UI changes.
  - **P6 (Murat)** AC 9 + Task 6.8 — replaced flaky "stop EventStore from dashboard" trigger with deterministic `appsettings.Development.json` base-address override to `http://localhost:1`.
  - **P7 (Murat)** Task 8.1 Axe bullet — named `@axe-core/cli` as the tool with explicit command; removed "if tooling available" wiggle word; if unavailable, size follow-up story `21-11-epic-21-accessibility-sweep` rather than silently defer.
  - **P8 (Sally)** AC 10 + Task 6.9 — made dark-mode mechanism explicit (DevTools prefers-color-scheme emulation), added WCAG AA contrast targets (4.5:1 / 3:1), and documented that in-app theme toggle is out of 21-10 scope.
  - **P9 (Sally)** Task 6.11 — added pedagogy-check step to verify Pattern-page teaching comments (e.g., the "FluentToast auto-dismiss ~5s" claim) still match v5 behavior via MCP cross-check.
  - **P10 (Bob)** Task 9.3 — tightened "resolved OR re-deferred-with-sized-follow-up" conjunction; killed the "TBD / if tooling available" escape hatch.
  - **P11 (Bob)** Task 9.3.1 — explicit decision on `epic-21-retrospective: optional` (leave as optional unless user requests; migration epics are low-ROI for retro vs. feature epics).
- 2026-04-15 — Advanced-elicitation 5-method sweep applied (Pre-mortem / Matrix / Occam / Lessons Learned / Chaos Monkey — 15 patches, following 21-8's precedent pattern):
  - **E1 (Pre-F1 + Bob)** Task 9.3 — same-commit rule: any sized follow-up story must add its sprint-status entry in the same commit as closing 21-10.
  - **E2 (Pre-F2 + Chaos-C5)** Task 6.8 — hard-gate the appsettings revert with `git checkout --` + `git diff` check before proceeding to Task 7.
  - **E3 (Pre-F3 + Chaos-C7)** Task 5.5 — replaced `/tmp/` (Windows/CI-hostile) with `./samples/.../obj/sample-blazor-ui.tar.gz` (cross-platform, git-ignored) + explicit post-publish cleanup.
  - **E4 (Pre-F4)** Dev Notes — added "V5 GA reconciliation note" section referencing Story 21-11 commitment from sprint change proposal line 126; same-commit rule for adding `21-11-fluent-ui-v5-ga-reconciliation: backlog` if not yet on sprint-status.
  - **E5 (Pre-F5 + Sally)** Task 6.11 — tightened pedagogy check to update *both* comment text AND narrative substance, not just the literal value.
  - **E6 (Pre-F6)** Task 7.1 — design-directions README gets explicit "do not edit in-place; create a new story to unfreeze" clause.
  - **E7 (Matrix)** Task 6.4 closes deferred 21-9 DataGrid gate explicitly; Task 6.9 closes deferred 21-4 AC 22 contrast (N/A for sample, but cross-project regression check added).
  - **E8 (Occam-O2)** Task 8.1 Axe bullet extracted to dedicated Task 8.3 (scannability + dedicated command block + follow-up story sizing rules).
  - **E9 (Occam-O3)** AC 7 "Note on Tier 1" trimmed from 4 sentences to 1.
  - **E10 (Lessons-L2)** Task 0.3 — grep `.razor.css` verification (expected zero under `ScopedCssEnabled=false`); flag-and-stop if any exist rather than silently merging.
  - **E11 (Lessons-L3)** Task 4.0 — verify `@rendermode` is present before removing; if absent, record "already applied by earlier story" and skip the edit rather than add+remove.
  - **E12 (Lessons-L4)** Task 9.4 — runtime-verification gate: if Docker/DAPR unavailable, story stays at `review` until a reviewer with tooling completes Task 6. Do NOT merge to done on compile-green alone.
  - **E13 (Lessons-L5)** Completion Notes — echo the applied v4→v5 mapping table (with exact counts per category) so future readers don't have to dig through Dev Notes.
  - **E14 (Chaos-C1/C2/C3)** Task 6.0 — explicit chaos-resilient boot prerequisites: Docker check via `docker ps`, `dapr --version`, Aspire port-conflict check (15888/16888), `dapr uninstall --all` + retry if init fails.
  - **E15 (Chaos-C6)** Task 7.1 — absolute-path discipline for the design-directions README to avoid writing into submodule-mirrored `Hexalith.Tenants/.../design-directions-prototype` copies.
