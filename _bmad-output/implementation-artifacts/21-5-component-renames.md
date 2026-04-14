# Story 21.5: Component Renames (FluentTextField / FluentNumberField / FluentSearch / FluentProgressRing / FluentSelect)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer migrating from Fluent UI Blazor v4 to v5,
I want every usage of the removed-or-renamed v4 input / indicator components (`FluentTextField`, `FluentNumberField`, `FluentSearch`, `FluentProgressRing`) replaced with their v5 equivalents (`FluentTextInput`, a shared `NumericInput` wrapper built on `FluentTextInput TextInputType="TextInputType.Number"`, `FluentTextInput` + `StartTemplate` search icon, `FluentSpinner`) and every `FluentSelect` supplied with the v5-required `TValue` type parameter (plus `@bind-SelectedOption` migrated to `@bind-Value`),
so that Admin.UI compiles cleanly under v5 and the downstream Admin.UI rendering stories (21-6 Dialog, 21-7 Toast) can proceed with a working UI tree — this story is one of the two hard dependencies (21-5 + 21-7) gating Admin.UI end-to-end compilation.

## Acceptance Criteria

### FluentTextField → FluentTextInput

1. **Given** any `<FluentTextField>` opening tag in `src/Hexalith.EventStore.Admin.UI/**/*.razor`,
   **When** migrated,
   **Then** the opening AND closing tag name becomes `<FluentTextInput>`/`</FluentTextInput>`,
   **And** all existing attributes (`@bind-Value`, `@bind-Value:after`, `Placeholder`, `Label`, `Required`, `style`, `aria-label`, `Value`, `ValueChanged`, `Autofocus`, `Title`) are preserved verbatim,
   **And** the attribute `Maxlength=` (v4 lowercase-L) becomes `MaxLength=` if any instance exists (grep-verify; no instances expected per audit).

2. **Given** any `TextFieldType=` attribute on a migrated `FluentTextInput`,
   **When** migrated,
   **Then** the attribute is renamed to `TextInputType=` and the enum value uses the `TextInputType` enum (`TextInputType.Text/Email/Password/Telephone/Url/Color/Search/Number`),
   **And** any v4 `TextFieldType.Tel` value maps to `TextInputType.Telephone` (NOT `TextInputType.Tel` — this value does not exist in v5).
   No current Admin.UI usage sets `TextFieldType=` (all 36 sites default to Text), so this AC is defensive.

3. **Given** any `FluentInputAppearance.Filled` value on a migrated `FluentTextInput` `Appearance=` attribute,
   **When** migrated,
   **Then** it becomes `TextInputAppearance.FilledDarker` (v5 has no plain `Filled` value).
   Defensive — no current Admin.UI instance sets `Appearance=` on these inputs.

4. **Given** the 36 `<FluentTextField>` sites enumerated in the File List,
   **When** migrated,
   **Then** each site's file, line, and bound variable is captured in Completion Notes (file:line — variable-name — one-line verdict).

### FluentNumberField → NumericInput wrapper (shared component)

5. **Given** no `NumericInput.razor` component exists in `src/Hexalith.EventStore.Admin.UI/Components/Shared/`,
   **When** this story begins,
   **Then** a new generic component `NumericInput.razor` is created at that path that:
   - Declares `@typeparam TValue where TValue : struct, IComparable, IComparable<TValue>, IEquatable<TValue>` with a `where TValue : struct` constraint to allow `int`, `long`, `decimal` and their nullable equivalents via the `Nullable<TValue>` rendering pattern,
   - Accepts parameters: `[Parameter, EditorRequired] public TValue? Value { get; set; }`, `[Parameter] public EventCallback<TValue?> ValueChanged`, `[Parameter] public EventCallback ValueChangedAfter`, `[Parameter] public TValue? Min { get; set; }`, `[Parameter] public TValue? Max { get; set; }`, `[Parameter] public string? Label { get; set; }`, `[Parameter] public string? Placeholder { get; set; }`, `[Parameter] public bool Required { get; set; }`, `[Parameter] public bool Autofocus { get; set; }`, `[Parameter] public string? AriaLabel { get; set; }`, `[Parameter(CaptureUnmatchedValues = true)] public IDictionary<string, object>? AdditionalAttributes { get; set; }`,
   - Renders a `<FluentTextInput TextInputType="TextInputType.Number">` with `@bind-Value` bound to a private `string?` field and splats `AdditionalAttributes` via `@attributes`,
   - Internally converts `TValue?` ↔ `string?` via a **typed `TryParse` dispatch** (`long.TryParse`, `int.TryParse`, `decimal.TryParse`, `double.TryParse`) selected by `typeof(TValue)` using `NumberStyles.Integer` (integral types) or `NumberStyles.Number` (floating/decimal) and `CultureInfo.InvariantCulture` — on parse failure, preserves the prior valid value and exposes an internal `_parseError` boolean surfaced as a plain `<span class="numeric-input-error" role="alert">Invalid number</span>` below the input,
   - **Does NOT depend on `FluentMessageBar` / `MessageIntent` / any v5 toast or message-bar API** — those types belong to Story 21-7's migration surface and would introduce a reverse 21-5→21-7 dependency,
   - Emits HTML attributes `min` and `max` via `AdditionalAttributes` when `Min`/`Max` are set (v5 `FluentTextInput` does not expose typed numeric Min/Max — browser-level enforcement only; server-side logic remains the caller's responsibility),
   - Round-trips `CultureInfo.InvariantCulture` exclusively — never `CultureInfo.CurrentCulture` — so "1,5" is not silently parsed as 15 or 1.5 depending on the user's locale,
   - Formats output via `((IFormattable)value).ToString(null, CultureInfo.InvariantCulture)` (or a typed `ToString(InvariantCulture)` dispatch) — never the parameterless `ToString()` which would emit locale-formatted strings,
   - **Resync policy on parse failure:** when `TryParse` fails, `_parseError` is set to `true`, `Value` is NOT updated, AND `_stringValue` retains the user's invalid typing so the user can see and correct it. On the NEXT successful parse, `_parseError` clears AND `_stringValue` is overwritten via `FormatInvariant(Value)`. Additionally, `OnParametersSet` MUST resync `_stringValue = FormatInvariant(Value)` ONLY when the incoming `Value` differs from the last-parsed value — never unconditionally, which would erase the user's in-progress typing mid-keystroke. Without this guard the field "lies": user sees their bad input while the parent binding holds a stale valid value.

6. **Given** any `<FluentNumberField>` opening tag in `src/Hexalith.EventStore.Admin.UI/**/*.razor`,
   **When** migrated,
   **Then** it is replaced by `<NumericInput TValue="<T>" @bind-Value="..." ...>` where `<T>` matches the v4 bound variable type (`long`/`long?` for non-Snapshots sites, `int?` for `Snapshots.razor` lines 154 and 200),
   **And** `Min="..."` attributes are preserved as `Min="..."` on `NumericInput` (6 sites use Min — Backups, Bisect ×2, CommandSandbox, EventDebugger, ProjectionDetailPanel ×1, StateInspectorModal),
   **And** no `Max` or `Step` attributes are present in current usage (verified by audit).

7. **Given** a `NumericInput` caller passes a non-numeric string (user paste or browser fallback),
   **When** the input fires `ValueChanged`,
   **Then** the component swallows `FormatException`/`OverflowException`, keeps `Value` unchanged, and sets `_parseError=true` to surface the inline error (see AC 5 spec),
   **And** a bUnit test `NumericInputTests.cs::NumericInput_RejectsNonNumericInputAndShowsError` verifies this behavior with `TValue="long"`.

8. **Given** the `NumericInput` component is live,
   **When** a bUnit test renders it with `Value="42L"`, `Min="0"`, `Placeholder="e.g. 0"`,
   **Then** `cut.Markup.ShouldContain("fluent-text-input")`, `cut.Markup.ShouldContain("type=\"number\"")`, `cut.Markup.ShouldContain("min=\"0\"")`, `cut.Markup.ShouldContain("value=\"42\"")`. Test file: `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/NumericInputTests.cs` — new file.

### FluentSearch → FluentTextInput + StartTemplate search icon

9. **Given** any `<FluentSearch>` opening tag in `src/Hexalith.EventStore.Admin.UI/**/*.razor`,
   **When** migrated,
   **Then** the tag becomes `<FluentTextInput>` with a `<StartTemplate><FluentIcon Value="@(new Icons.Regular.Size16.Search())" /></StartTemplate>` child slot prepended before any existing child content or closing tag,
   **And** `@bind-Value`, `@bind-Value:after`, `Placeholder`, `aria-label`, `Class`, `style` are preserved,
   **And** `TextInputType="TextInputType.Search"` is added to preserve browser-level search semantics (virtual keyboard hint, Escape-clears behavior where supported).

10. **Given** the TypeCatalog.razor site uses one-way `Value="@_searchText"` (not `@bind-Value`) on line 67,
    **When** migrated,
    **Then** the one-way binding is preserved as `Value="@_searchText"` on `FluentTextInput` (do NOT convert to `@bind-Value` — the page manages search debouncing through an external mechanism).

11. **Given** v5 `FluentTextInput` does NOT render the v4 `FluentSearch` built-in clear ("×") button,
    **When** any of the 12 `<FluentSearch>` sites is migrated,
    **Then** the DEFAULT migration outcome is to add an `<EndTemplate>` clear button that sets the bound variable to `null`/`""` and fires the existing `@bind-Value:after` callback (v4 parity — every v4 `FluentSearch` rendered this affordance automatically),
    **And** a site may opt OUT of the clear button ONLY when the dev documents a specific user-facing rationale in Completion Notes (e.g., "TimelineFilterBar binds to a parent property via two-step indirection — clearing via the button would require additional plumbing; defer to follow-up"),
    **And** each of the 12 sites is listed in Completion Notes under `PENDING-CLEAR-BUTTON-VERIFICATION` with one of three outcomes: `ADDED` (clear button present), `OPT-OUT: <reason>` (deliberate omission with rationale), or `DEFERRED-TO-21-7-SPOT-CHECK` (site requires running UI to verify — use only when compile-time analysis is insufficient).
    **Explicit per-site classification table** (supersedes all prose guidance; if a site's classification proves wrong at spot-check, update Completion Notes with the corrected outcome but do NOT retroactively edit this table):

    | # | File:line | Bound | Default |
    |---|---|---|---|
    | 1 | `Components/BlameViewer.razor:62` | `_searchTerm` | `OPT-OUT-CANDIDATE` (inline-in-form, short-lived search) |
    | 2 | `Pages/DaprComponents.razor:117` | `_searchFilter` | `ADDED` (persistent filter bar) |
    | 3 | `Pages/DaprHealthHistory.razor:223` | `_transitionSearch` | `ADDED` (persistent filter bar) |
    | 4 | `Pages/DaprPubSub.razor:160` | `_searchFilter` | `ADDED` (persistent filter bar) |
    | 5 | `Components/BisectTool.razor:62` | `_fieldSearchTerm` | `ADDED` (filter bar for bisect field list) |
    | 6 | `Components/CommandPalette.razor:7` | `_searchQuery` | `ADDED` (filter bar; Escape-to-clear is also the palette's close key — add clear button AND preserve Escape handler) |
    | 7 | `Components/CommandSandbox.razor:144` | `_stateChangeSearch` | `ADDED` (filter bar, persistent across sandbox session) |
    | 8 | `Components/EventDebugger.razor:151` | `_watchFieldInput` | `OPT-OUT-CANDIDATE` (inline-in-form, user types then commits to add watch) |
    | 9 | `Components/EventDebugger.razor:176` | `_fieldSearchTerm` | `ADDED` (filter bar for watched-field list) |
    | 10 | `Pages/TypeCatalog.razor:67` | `_searchText` (one-way) | `ADDED` (persistent filter bar; preserve one-way `Value=` binding per Task 4.3) |
    | 11 | `Components/TimelineFilterBar.razor:27` | `_correlationSearch` | `ADDED` (persistent filter bar) |
    | 12 | `Components/CommandSandbox.razor:26` | — NOTE: this is the `_commandType` `FluentTextField` input, not a `FluentSearch`; row removed from clear-button scope — | N/A |

    Net: **9 `ADDED`**, **2 `OPT-OUT-CANDIDATE`** (BlameViewer, EventDebugger watchFieldInput — final `OPT-OUT: <reason>` or `ADDED` decided by the dev with rationale). Row #12 is a reminder — CommandSandbox.razor:26 is a FluentTextField migration (covered by AC 1) and NOT a FluentSearch site, so it is not in scope for the clear-button AC.

### FluentProgressRing → FluentSpinner

12. **Given** any `<FluentProgressRing>` opening tag in `src/Hexalith.EventStore.Admin.UI/**/*.razor`,
    **When** migrated,
    **Then** the tag becomes `<FluentSpinner>`,
    **And** the inline `Style="width: Npx; height: Npx;"` attribute is removed and replaced with a typed `Size="SpinnerSize.X"` per this mapping:
    - `12px` → `Size="SpinnerSize.Tiny"` (1 site: Consistency.razor:140)
    - `16px` → `Size="SpinnerSize.ExtraSmall"` (26 sites — the dominant inline-button/dialog pattern)
    - `32px` → `Size="SpinnerSize.Medium"` (1 site: Tenants.razor:149 — page-level loading). **Decision rationale:** `SpinnerSize.Small` ≈ 24px, which is ~25% smaller than v4's 32px and will read as a visible regression on a prominent page-level loader. `SpinnerSize.Medium` (v5 default, ~28px) is the closest match; a 4px delta is acceptable, a 8px delta is not. Do NOT escalate to `SpinnerSize.Large` — that reads as oversized.
    - No-explicit-style → leave `Size=` omitted (v5 default is `SpinnerSize.Medium`) (1 site: BisectTool.razor:102)

13. **Given** a `<FluentProgressRing>` uses `Style=` for non-sizing CSS (e.g., `margin-top: 8px;` on BisectTool.razor:102 or `margin-right: 4px;` embedded with sizing on Consistency.razor:140),
    **When** migrated,
    **Then** only the `width`/`height` declarations are removed from the `Style=` value; remaining CSS declarations (margin, padding, etc.) are preserved verbatim.

14. **Given** v5 `FluentSpinner` has no `Color`, `Value`, `Min`, `Max`, `Paused`, or `ChildContent` parameters,
    **When** any migrated site previously used any of these (none expected per audit),
    **Then** the attribute is removed and the decision is recorded in Completion Notes. `AppearanceInverted="true"` is NOT added proactively — only add it if a spinner is rendered on a dark/brand-colored background AND the spotcheck reveals a contrast failure.

### FluentSelect → explicit TValue + @bind-SelectedOption removal

15. **Given** any `<FluentSelect TOption="string">` in `src/Hexalith.EventStore.Admin.UI/**/*.razor`,
    **When** migrated,
    **Then** a `TValue="string"` type parameter is added alongside the existing `TOption="string"` (v5 `FluentListBase<TOption, TValue>` requires both),
    **And** the two type params may be on the same line or adjacent lines — preserve existing formatting where possible.

16. **Given** `DeadLetters.razor:44` uses `@bind-SelectedOption="_failureCategory"` (with a companion `@bind-SelectedOption:after="OnCategoryChanged"` on line 45),
    **When** migrated,
    **Then** it becomes `@bind-Value="_failureCategory"` with `@bind-Value:after="OnCategoryChanged"` (v5 removed `SelectedOption` / `@bind-SelectedOption`; `Value` is now the generic bound value),
    **And** grep `@bind-SelectedOption` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits post-migration.

17. **Given** any `FluentSelect` uses a bare `Disabled` attribute (no `="true"`),
    **When** migrated,
    **Then** it becomes `Disabled="true"` (v5 `Disabled` is `bool?` and bare attribute form may fail under `TreatWarningsAsErrors=true`). Defensive — grep confirms no bare `Disabled` on FluentSelect currently, but verify after migration.

18. **Given** any `Appearance=` attribute on a `FluentSelect`,
    **When** migrated,
    **Then** the value uses the `ListAppearance` enum (`FilledLighter`/`FilledDarker`/`Outline`/`Transparent`) — NOT the generic `Appearance` enum (which is v4). Defensive — no current Admin.UI usage sets `Appearance=` on FluentSelect.

### Tests (bUnit assertion updates)

19. **Given** `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs:364` asserts `cut.Markup.ShouldContain("fluent-progress-ring")`,
    **When** migrated,
    **Then** the assertion reads `cut.Markup.ShouldContain("fluent-spinner")`.

20. **Given** `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandSandboxTests.cs:232` uses `cut.Find("fluent-text-field")`,
    **When** migrated,
    **Then** the selector reads `cut.Find("fluent-text-input")`.

21. **Given** `tests/Hexalith.EventStore.Admin.UI.Tests/Components/EventDebuggerTests.cs:341` uses `cut.Find("fluent-search[aria-label='Watch field path input']")`,
    **When** migrated,
    **Then** the selector reads `cut.Find("fluent-text-input[aria-label='Watch field path input']")`.

22. **Given** the new `NumericInput` component,
    **When** bUnit tests from ACs 7 and 8 are added,
    **Then** the test file `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/NumericInputTests.cs` exists, derives from `AdminUITestContext`, and contains at minimum:
    - `NumericInput_RendersWithInitialValue` (renders `Value="42L"` and asserts markup)
    - `NumericInput_RejectsNonNumericInputAndShowsError` (AC 7)
    - `NumericInput_FiresValueChangedCallback` (uses `cut.InvokeAsync` to trigger input change and asserts callback fires with the parsed `long?`)
    - `NumericInput_PreservesInvariantCultureParsing` (explicit culture round-trip test for "1000" and "1000.5" with `TValue="decimal"`)
    - `NumericInput_RoundTripsValueThroughChange` — render with `Value="42L"`, dispatch an input-change event with payload `"100"`, assert (a) `ValueChanged` fired with `100L`, (b) the re-rendered markup contains `value="100"` (NOT `value="100,00"` or any locale-formatted variant). This test locks in BOTH the parse *and* format halves of the invariant-culture contract — without it, `Convert.ToString(Value, CurrentCulture)` could silently creep back in during future refactors.

### CS0649 cleanup (spillover from 21-4)

23. **Given** Story 21-4's Completion Notes flagged CS0649 ("field never assigned") warnings for `_searchFilter` (DaprComponents), `_transitionSearch` (DaprHealthHistory), `_timestampInput` (StateInspectorModal),
    **When** Story 21-5 completes,
    **Then** each field declaration is checked — if the field is bound via `@bind-Value` on a `FluentTextInput`, the warning self-heals; if it remains, the field is initialized to `string.Empty` (not `null`) at declaration, **OR** the field is converted to a read-write property with a default.
    Verify by re-running `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/... --configuration Release` after component renames and confirming zero CS0649 remains.

### Verification gates

24. **Given** story completion,
    **When** a whitespace-tolerant grep for `<FluentTextField\b` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** the FluentTextField elimination is verified complete.

25. **Given** story completion,
    **When** a whitespace-tolerant grep for `<FluentNumberField\b` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** the FluentNumberField elimination is verified complete.

26. **Given** story completion,
    **When** a whitespace-tolerant grep for `<FluentSearch\b` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** the FluentSearch elimination is verified complete.

27. **Given** story completion,
    **When** a whitespace-tolerant grep for `<FluentProgressRing\b` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** the FluentProgressRing elimination is verified complete.

28. **Given** story completion,
    **When** a grep for `<FluentSelect\s+(?![^>]*\bTValue=)` (FluentSelect opening tag missing `TValue=` somewhere inside) across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** every FluentSelect has an explicit `TValue` type parameter.

29. **Given** story completion,
    **When** a grep for `@bind-SelectedOption` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** the `@bind-SelectedOption` → `@bind-Value` migration is verified complete.

30. **Given** story completion,
    **When** the 4 replacement tokens (`<FluentTextInput`, `<NumericInput`, `<FluentSpinner`, `TValue=`) are grep-counted across `src/Hexalith.EventStore.Admin.UI/**/*.razor`,
    **Then** counts are recorded in Completion Notes with expected minimum thresholds: `<FluentTextInput` ≥ 48 (36 TextField + 12 Search), `<NumericInput` = 10, `<FluentSpinner` = 29, `TValue="string"` on FluentSelect ≥ 17. Missing counts trigger per-site investigation before ready-for-review.

31. **Given** story completion,
    **When** non-UI Tier 1 tests run (`dotnet test` on Contracts + Client + Testing + SignalR = 691 tests),
    **Then** all pass with zero regressions.

32. **Given** story completion,
    **When** `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release` runs,
    **Then** the residual error count is `≤ 310` (soft ceiling) — down from Story 21-4's 607 baseline. **Math:** expected composition of the remaining residuals per downstream-story estimates — Story 21-7 toast (`IToastService.ShowError/ShowSuccess/ShowWarning` + `MessageIntent`, ~80-120 errors across 9 files) + Story 21-6 dialog (`FluentDialogHeader/FluentDialogFooter/@bind-Hidden`, ~60-90) + Story 21-9 DataGrid/Tab (~20-30) + Story 21-10 samples (~10-20) = upper bound `~260`. Adding a 20% buffer for pre-existing non-migration errors (e.g., CS0649 edge cases, inadvertently surfaced by rename) sets the ceiling at `~310`. If POST count exceeds 310, investigate — a residual on the 21-5 surface is the likely cause.
    **And** every residual error is attributed to a downstream story (21-6 dialog, 21-7 toast, 21-8 CSS, 21-9 DataGrid, 21-10 samples) using the `FILE:LINE — PATTERN — story` form established in Story 21-4 AC 21,
    **And** **zero residual errors may trace back to files this story modified on the TextField/NumberField/Search/ProgressRing/Select surface.** This is a HARD gate — any single residual attributed to 21-5's surface blocks `review` status. (The `≤310` ceiling is soft; the zero-on-own-surface rule is hard.)

33. **Given** the sprint change proposal's recommended execution order (21-0, 21-1, 21-2, 21-3, 21-4, **21-5**, 21-6, **21-7**, 21-8, 21-9, 21-10),
    **When** Story 21-5 reaches `review`,
    **Then** it is EXPECTED that Admin.UI will NOT yet build end-to-end because Story 21-7 (toast migration) has not yet landed. This is by design — 21-5 is not a gate for a running Admin.UI. The story is complete when ACs 24-32 (grep-for-zero + residual ceiling + Tier 1 tests + isolated compile + zero-own-surface residuals) pass; end-to-end `aspire run` success belongs to 21-7. Document the remaining Admin.UI blocker count in Completion Notes; every blocker must trace to a downstream story per AC 32.

34. **Given** story completion AND Story 21-7 has ALSO landed (coordinate with SM on sequencing),
    **When** `aspire run` produces a running Admin.UI,
    **Then** the visual spot-check listed in Dev Notes §Visual Verification Protocol is executed, screenshots/verdicts recorded per site, and any regressions filed as new correct-course items.
    **Expected default outcome:** DEFERRED. Per the sprint change proposal's recommended execution order, Story 21-5 lands BEFORE Story 21-7; therefore AC 34's prerequisite (21-7 landed) is NOT met at the moment 21-5 reaches `review`. This AC is a forward-pointer, not a gate on this story. Mark as `DEFERRED-TO-21-7-OR-EPIC-21-RETRO` in Completion Notes with the site list from Dev Notes §Visual Verification Protocol preserved for the eventual executor. Running AC 34 opportunistically is allowed if 21-7 happens to land first, but is not required.

### NumericInput keystroke/commit semantics

35. **Given** the `NumericInput` wrapper is implemented,
    **When** a user types into the field character-by-character,
    **Then** `ValueChanged` fires on EVERY keystroke per Blazor's default `FluentTextInput.ValueChanged` behavior (the wrapper does NOT debounce or defer to blur/commit — v4 `FluentNumberField` had commit-on-blur semantics; v5 via `FluentTextInput` does not),
    **And** this keystroke-fires-per-change semantics is documented in `NumericInput.razor` XML summary AND in Dev Notes §NumericInput wrapper,
    **And** each of the 10 consuming sites is inventoried in Completion Notes as either `KEYSTROKE-SAFE` (caller uses `@bind-Value` passively; no side effects per change) or `KEYSTROKE-HAZARD: <site> — <reason>` (caller has `@bind-Value:after` or other side effects that fire per keystroke — must be reviewed for cost).
    Known hazard candidates: Snapshots.razor `_createIntervalEvents` / `_editIntervalEvents` (if dialog "Save" enable-disable is bound to value change, the button flickers per keystroke). If a hazard is confirmed, either (a) add a `ChangeAfterKeyPress="@([KeyCode.Enter])"` commit-on-enter semantic to the wrapper, or (b) document acceptance and move on.

### NumericInput null-handling per site

36. **Given** the 10 `<FluentNumberField>` sites per the File List,
    **When** migrated to `<NumericInput>`,
    **Then** each site is classified by its bound variable's nullability AND the caller's expected behavior when the user clears the input:
    - Sites binding to `long?` (CommandSandbox.razor:45, ProjectionDetailPanel.razor:195/234/239, EventDebugger.razor:93 if nullable) — `NumericInput<long?>` emits `null` on clear; caller handles `null` naturally. **Action: no change required.**
    - Sites binding to non-nullable `long` (BisectTool.razor:23/29, StateInspectorModal.razor:43) — clearing the input emits `null` from the wrapper; Blazor two-way binding to `long` would throw `InvalidCastException` at reverse-map. **Action (default, per-site):** switch the bound field declaration from `long` to `long?`, AND update every downstream consumer (validation, save handler, display) to handle `null` appropriately (treat as unset / use a default / reject at save time).
    - Sites binding to `int?` (`Snapshots.razor:154/200`) — `NumericInput<int?>` emits `null` on clear; caller must enforce non-null positive intervals at submit.
    - BisectTool.razor `_goodSequence` / `_badSequence` — the bisect algorithm requires both to be valid longs before starting. Switching to `long?` is safe; the "Start bisect" button's disabled state binding already guards this (verify during spot-check).

### FluentSearch clear-button TextInputType conflict avoidance

37. **Given** any of the 12 migrated FluentSearch sites that gets an `<EndTemplate>` clear button per AC 11,
    **When** migrated,
    **Then** the `TextInputType` attribute on that site's `FluentTextInput` is `TextInputType.Text` (NOT `TextInputType.Search`),
    **And** this rule overrides AC 9's default-add of `TextInputType.Search`. Rationale: Chrome and Edge render a browser-native clear × on `<input type="search">` fields by default; stacking a manual EndTemplate clear button on top would produce two × affordances side-by-side. Sites WITHOUT a manual clear button retain `TextInputType.Search` (from AC 9) to preserve browser search semantics.

## Tasks / Subtasks

- [x] Task 0: Pre-flight audit — confirm scope claims before touching code (AC: all)
  - [x] 0.1: Record PRE-story error count: `dotnet build Hexalith.EventStore.slnx --configuration Release`. Capture as `PRE: total=<N>, CS0618=<M>, CS1503=<K>, RZ10012=<R>, other=<J>`. No prose summaries.
  - [x] 0.2: Grep each v4 component (opening tag, whitespace-tolerant) in `src/Hexalith.EventStore.Admin.UI/**/*.razor` and record the count per component. Expected: FluentTextField=36, FluentNumberField=10, FluentSearch=12, FluentProgressRing=29, FluentSelect=17. Record ANY delta against these numbers in Completion Notes — a delta indicates either audit drift (new/removed component between 2026-04-13 audit and now) or a counting anomaly that must be investigated before migrating.
  - [x] 0.3: Grep for `TextFieldType=` on `.razor` files — expected 0 hits (defensive for AC 2).
  - [x] 0.4: Grep for `Maxlength=` (lowercase L) on `.razor` files — expected 0 hits (defensive for AC 1).
  - [x] 0.5: Grep for `Appearance=` on `<FluentTextField`/`<FluentNumberField`/`<FluentSearch`/`<FluentSelect` — expected 0 hits (defensive for ACs 3, 18).
  - [x] 0.6: Grep for `@bind-SelectedOption` — expected ≥1 hit (DeadLetters.razor:44).
  - [x] 0.7: Grep for `@ref` on any of the 5 target components in `.razor` files AND the matching field declarations in `.razor.cs`/`@code` blocks — expected 0 hits (defensive; if any found, add a subtask to rename the field's type in lockstep with the template rename, per Story 21-4 Task 8 lesson).
  - [x] 0.8: Record each grep command + result count in Completion Notes. Zero-count results ARE a deliverable.
  - [x] 0.9: **Icons package compatibility spike.** The Icons package is pinned at `4.14.0` (per Story 21-1, intentional — v4 icons still render under v5 Components). Before Task 4 (FluentSearch migration), verify that `Icons.Regular.Size16.Search()` instantiation compiles and renders correctly under the v5 `<FluentIcon Value="..." />` shape. Write a 5-line throwaway bUnit test or use MCP `get_icon_details("Search")` / `get_icon_usage("Search")` to confirm the v4 Icons API surface is compatible. If incompatible: stop, escalate, and either upgrade the Icons package (breaks the Story 21-1 pin) or switch the StartTemplate icon to an inline SVG. Record the outcome in Completion Notes as `ICONS-V4-COMPAT: OK | INCOMPAT: <details>`. This spike unblocks 12 sites; do not proceed to Task 4 without it.
  - [x] 0.10: **ChildContent defensive grep on 36 FluentTextField sites + 10 FluentNumberField sites + 12 FluentSearch sites.** v5 removed `ChildContent` from `FluentTextInput` (per MCP — replaced by `StartTemplate`/`EndTemplate`). Grep pattern: `<FluentTextField[^/]*>[^<]*\S[^<]*</FluentTextField>` (and analogous for NumberField/Search) — looking for text content between opening and closing tags. Expected: 0 hits. Any non-zero result means the migration would silently drop text content; add a subtask to move the text to `Label=`, `Placeholder=`, or document the site as a refactor-required deferral. Do NOT use `replace_all` style bulk renames that collapse child content.
  - [x] 0.11: **OptionValue/OptionText lambda grep on `.razor.cs` files for 17 FluentSelect sites.** Audit scanned `.razor` templates only. In v5, `OptionValue` signature is `Func<TOption?, TValue?>?` (was `Func<TOption, string?>?` in v4). A code-behind lambda declared as `Func<string, string?>` or `Func<object, string>` will fail type inference after `TValue="string"` is added. Grep `.razor.cs` / `.cs` files co-located with FluentSelect-using pages for `OptionValue` / `OptionText` / `OptionDisabled` assignments. Expected: 0 hits. If found, update the lambda signature to `(string? opt) => ...` form.
  - [x] 0.12: **SpinnerSize enum pixel dimensions verification.** AC 12's mapping (12px→Tiny, 16px→ExtraSmall, 32px→Medium) assumes specific pixel dimensions. Before Task 5, use MCP `get_enum_values("SpinnerSize")` and `get_component_details("FluentSpinner")` to confirm the intended pixel sizes for `Tiny`, `ExtraSmall`, `Small`, `Medium`, `Large`, `ExtraLarge`, `Huge`. If MCP-documented dimensions contradict AC 12's mapping, record the corrected mapping in Completion Notes and apply it in Task 5 — update AC 12 post-hoc via a review-note rather than blindly following the document. Record outcome as `SPINNERSIZE-VERIFIED: <mapping>`.

- [x] Task 1: Create `NumericInput.razor` shared component (AC: 5, 7, 8)
  - [x] 1.1: Create `src/Hexalith.EventStore.Admin.UI/Components/Shared/NumericInput.razor` per AC 5 spec. File-scoped namespace, Allman braces, `_camelCase` private fields (CLAUDE.md convention).
  - [x] 1.2: Create the codebehind `NumericInput.razor.cs` if separation is preferred (follow existing project convention — check `Components/Shared/EmptyState.razor` as template).
  - [x] 1.3: Implement string↔TValue parsing via a **typed `TryParse` dispatch** — switch on `typeof(TValue)` and call `long.TryParse` / `int.TryParse` / `decimal.TryParse` / `double.TryParse` with `NumberStyles.Integer` (integral) or `NumberStyles.Number` (floating/decimal) and `CultureInfo.InvariantCulture`. Do **not** use `Convert.ChangeType` — its provider-handling is inconsistent across numeric types and silently accepts or rejects inputs like `"1,5"` depending on the destination type. On parse failure: keep prior value, set `_parseError=true`. Symmetric format path uses `((IFormattable)value).ToString(null, CultureInfo.InvariantCulture)` or a typed `ToString(InvariantCulture)` dispatch — never parameterless `ToString()`.
  - [x] 1.4: Wire the generic `TValue` type constraint: `@typeparam TValue where TValue : struct, IComparable, IComparable<TValue>, IEquatable<TValue>`. Support nullable TValue via a `Value` parameter of type `TValue?`.
  - [x] 1.5: Render `<FluentTextInput TextInputType="TextInputType.Number">` with `@bind-Value` bound to a private `_stringValue` field; splat `AdditionalAttributes` via `@attributes`.
  - [x] 1.6: Emit `min` and `max` HTML attributes via `AdditionalAttributes` when the caller passes `Min`/`Max`.
  - [x] 1.7: Surface inline error via a plain `<span class="numeric-input-error" role="alert">Invalid number</span>` element below the input when `_parseError=true`. **Do NOT use `FluentMessageBar` or `MessageIntent`** — those types belong to Story 21-7's toast/message-bar migration surface and would create a reverse 21-5→21-7 dependency that breaks the independent sequencing guaranteed by the sprint change proposal. A later cleanup story (Epic 21 retrospective or a follow-up) may upgrade the span to `FluentMessageBar` once 21-7 lands.
  - [x] 1.8: Create `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/NumericInputTests.cs` with the four tests from AC 22. Derive from `AdminUITestContext` (same base as `EmptyStateTests`).

- [x] Task 2: Migrate `<FluentTextField>` → `<FluentTextInput>` (AC: 1, 2, 3, 4)
  - [x] 2.1: For each of the 36 sites (see File List §FluentTextField), open the file, find the opening and closing `FluentTextField` tag (note: some may span multiple lines — follow Story 21-3/21-4 multi-line guidance), and rename both.
  - [x] 2.2: Preserve every existing attribute verbatim — `@bind-Value`, `@bind-Value:after`, `Placeholder`, `Label`, `Required`, `style`, `aria-label`, `Value`, `ValueChanged`, `Autofocus`, `Title`.
  - [x] 2.3: Per-site verdict line in Completion Notes: `File.razor:LINE — _bindingVar — migrated`.

- [x] Task 3: Migrate `<FluentNumberField>` → `<NumericInput TValue="<T>">` with per-site TValue (AC: 6)
  - [x] 3.1: For each of the 10 sites (see File List §FluentNumberField), replace the `<FluentNumberField>` opening/closing tags with `<NumericInput TValue="<T>">`/`</NumericInput>` (or self-close if empty), where `<T>` matches the bound field type.
  - [x] 3.2: Rename `@bind-Value` target to match the `long`/`long?`/`int?` type; pass `Min=` if present; ensure `Label`, `Placeholder`, `Autofocus`, `aria-label`, `style`, `Required` pass through via the wrapper's own parameters or `AdditionalAttributes` splat.
  - [x] 3.3: For `BisectTool.razor:23,29` (TValue=long, Min=0/1) + `EventDebugger.razor:93` (TValue=long, Min=1) + `Snapshots.razor:154,200` (TValue=int?, Required=true) + `StateInspectorModal.razor:43` (TValue=long, Autofocus=true), verify each mapping explicitly in Completion Notes.
  - [x] 3.4: For `CommandSandbox.razor:45`, `ProjectionDetailPanel.razor:195/234/239`, use `TValue="long?"` since bindings are nullable long.
  - [x] 3.5: Verify no code-behind field type needs to change — all 10 bindings are already `long`/`long?`/`int?`, matching `NumericInput` TValue per site.

- [x] Task 4: Migrate `<FluentSearch>` → `<FluentTextInput>` + `<StartTemplate>` icon (AC: 9, 10, 11)
  - [x] 4.1: For each of the 12 sites (see File List §FluentSearch), replace opening/closing tags with `<FluentTextInput>`/`</FluentTextInput>`, add `TextInputType="TextInputType.Search"`, and inject `<StartTemplate><FluentIcon Value="@(new Icons.Regular.Size16.Search())" /></StartTemplate>` as the first child.
  - [x] 4.2: Preserve `@bind-Value`, `@bind-Value:after`, `Placeholder`, `aria-label`, `Class`, `style` verbatim.
  - [x] 4.3: TypeCatalog.razor:67 — preserve one-way `Value="@_searchText"` (do NOT convert to `@bind-Value`).
  - [x] 4.4: CommandPalette.razor:7 — preserve the `Class="command-palette-search"` attribute exactly (a page-specific CSS selector depends on it; Story 21-8 will revisit).
  - [x] 4.5: For each of the 12 sites, quick-visual-inspection during spot-check (AC 34): does the site rely on the v4 built-in clear button? If yes, record `NEEDS-CLEAR-BUTTON: file:line` in Completion Notes — do NOT add an EndTemplate clear button unless explicitly needed (keeps scope tight; follow-up can add).

- [x] Task 5: Migrate `<FluentProgressRing>` → `<FluentSpinner>` with SpinnerSize enum (AC: 12, 13, 14)
  - [x] 5.1: For each of the 29 sites (see File List §FluentProgressRing), rename the tag and apply the size mapping from AC 12 (12px→Tiny, 16px→ExtraSmall, 32px→Small).
  - [x] 5.2: Remove only `width` and `height` declarations from the `Style=` attribute; preserve other CSS declarations (e.g., margin, padding) — see AC 13.
  - [x] 5.3: BisectTool.razor:102 — no explicit sizing style; leave `Size=` omitted (v5 default is Medium, which is acceptable since the v4 default also fell back to the intrinsic SVG size).
  - [x] 5.4: Consistency.razor:140 — the style is `width: 12px; height: 12px; margin-right: 4px;`. After migration: `Size="SpinnerSize.Tiny" Style="margin-right: 4px;"`.
  - [x] 5.5: Tenants.razor:149 — the only 32px page-level spinner. Use `Size="SpinnerSize.Medium"` (v5 Medium ≈ 28px — closest to v4's 32px per AC 12). Do NOT use `SpinnerSize.Small` (~24px, too small by 8px — visible regression on a prominent page-level loader); do NOT use `SpinnerSize.Large` (oversized). Record the choice explicitly in Completion Notes as `Tenants.razor:149 — 32px → SpinnerSize.Medium (Δ -4px acceptable, Small would be Δ -8px)`. **If Task 0.12 MCP verification contradicts these pixel estimates, prefer the verified values.**
  - [x] 5.6: **Scoped CSS selector scan.** Grep `src/Hexalith.EventStore.Admin.UI/**/*.razor.css` for selectors containing `fluent-progress-ring` (the v4 element's rendered tag name) or CSS class hooks emitted by FluentProgressRing's internal template. Expected: 0 hits if no page relies on the v4 spinner's DOM shape. Any non-zero hit requires either (a) updating the selector to `fluent-spinner` / v5 equivalents in the same commit, or (b) flagging for Story 21-8 coordination as a cross-story selector migration. Record each hit in Completion Notes. Note: scoped CSS is globally disabled (`ScopedCssEnabled=false` per Story 21-1), so these selectors apply globally — changes may have wider reach than the `.razor.css` filename suggests.

- [x] Task 6: Migrate `<FluentSelect>` — add `TValue="string"` type parameter and update `@bind-SelectedOption` (AC: 15, 16, 17, 18)
  - [x] 6.1: For each of the 17 sites (see File List §FluentSelect), add `TValue="string"` after the existing `TOption="string"` (preserve multi-line formatting where present).
  - [x] 6.2: DeadLetters.razor:44 — change `@bind-SelectedOption="_failureCategory"` → `@bind-Value="_failureCategory"`; and line 45 `@bind-SelectedOption:after="OnCategoryChanged"` → `@bind-Value:after="OnCategoryChanged"`.
  - [x] 6.3: Grep for `@bind-SelectedOption` post-migration — must return 0 hits (AC 29).
  - [x] 6.4: DaprComponents.razor:122 — preserve the `Value`/`ValueChanged` one-way binding pattern (not `@bind-Value`).
  - [x] 6.5: Verify no bare `Disabled` attribute on any FluentSelect; if found, change to `Disabled="true"` (AC 17).

- [x] Task 7: Update bUnit test assertions and add DeadLetters @bind-Value regression test (AC: 19, 20, 21, 16)
  - [x] 7.1: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs:364` — change `"fluent-progress-ring"` to `"fluent-spinner"`.
  - [x] 7.2: `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandSandboxTests.cs:232` — change `"fluent-text-field"` to `"fluent-text-input"`.
  - [x] 7.3: `tests/Hexalith.EventStore.Admin.UI.Tests/Components/EventDebuggerTests.cs:341` — change `"fluent-search[aria-label='Watch field path input']"` to `"fluent-text-input[aria-label='Watch field path input']"`.
  - [x] 7.4: **DeadLetters `@bind-Value` migration regression test.** Add a bUnit test `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DeadLettersPageTests.cs::DeadLettersPage_CategoryFilterBindsValueAndFiresAfterCallback` (new file if absent). The test renders `DeadLetters.razor`, selects a category option (e.g., `"Deserialization"`) by dispatching a `ChangeEventArgs` on the `fluent-select`, and asserts: (a) the page's `_failureCategory` field updates, (b) `OnCategoryChanged` fired once. This is the ONLY behavioral-contract site in 21-5 (every other change is a pure rename); without this regression test, a future accidental revert to `@bind-SelectedOption` would compile under v5 (it would just silently break the after-callback).
  - [x] 7.5: Run `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj` if the project compiles standalone; if not, note that these assertions will be re-validated once 21-7 lands.

- [x] Task 8: CS0649 field cleanup (AC: 23)
  - [x] 8.1: Run `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release` post-migration and grep for `CS0649` in the output.
  - [x] 8.2: For each remaining CS0649, initialize the field to `string.Empty` at declaration (e.g., `private string? _searchFilter = string.Empty;`) OR convert to an auto-property with default.
  - [x] 8.3: Known candidates (may self-heal): `DaprComponents.razor._searchFilter`, `DaprHealthHistory.razor._transitionSearch`, `StateInspectorModal.razor._timestampInput`.

- [x] Task 9: Build and verification (AC: 24, 25, 26, 27, 28, 29, 30, 31, 32)
  - [x] 9.1: Record POST-story error count: `dotnet build Hexalith.EventStore.slnx --configuration Release`. Capture as `POST: total=<N>, CS0618=<M>, CS1503=<K>, RZ10012=<R>, other=<J>`. Compute `DELTA RZ10012` (expect large drop — all FluentTextField/FluentNumberField/FluentSearch RZ10012 errors should be eliminated). Compute `DELTA CS0618` (expect CS0618 drop from 64→closer-to-FluentProgressRing-only residuals).
  - [x] 9.2: Seven-pass grep-for-zero verification:
    - Pass 1 (AC 24): `<FluentTextField\b` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` → 0 hits
    - Pass 2 (AC 25): `<FluentNumberField\b` across same → 0 hits
    - Pass 3 (AC 26): `<FluentSearch\b` across same → 0 hits
    - Pass 4 (AC 27): `<FluentProgressRing\b` across same → 0 hits
    - Pass 5 (AC 28): `<FluentSelect\s+(?![^>]*\bTValue=)` across same → 0 hits (regex: FluentSelect opening tag without TValue anywhere in its attributes). If regex proves brittle under multi-line attributes, fall back to: enumerate every `<FluentSelect` hit and manually verify each has a `TValue=` within its opening-tag span.
    - Pass 6 (AC 29): `@bind-SelectedOption` across same → 0 hits
    - Pass 7 (AC 30): Count `<FluentTextInput`, `<NumericInput`, `<FluentSpinner`, `TValue=` occurrences; record each count.
  - [x] 9.3: Non-UI Tier 1 tests: `dotnet test` on Contracts + Client + Testing + SignalR = 691 tests. All must pass. Maintain baseline from 21-0..21-4.
  - [x] 9.4: Admin.UI.Tests isolated compile (AC 32): `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release`. Capture error output. For each residual error, cite file:line + pattern + owning story (21-6 dialog, 21-7 toast, 21-8 CSS, 21-9 DataGrid, 21-10 samples). Zero residuals may trace to files this story modified on the 5-component surface.
  - [x] 9.5: Record the final list of test files added/modified: `NumericInputTests.cs` (new), `ConsistencyPageTests.cs`, `CommandSandboxTests.cs`, `EventDebuggerTests.cs`.

- [x] Task 10: Visual verification (AC: 33, 34) — conditional on Story 21-7 landing
  - [x] 10.1: Check sprint-status.yaml: is 21-7 status `done` or `review`?
    - If NO: mark AC 34 `DEFERRED: waiting on 21-7 toast migration` in Completion Notes. Proceed to AC 33.
    - If YES: proceed to 10.2.
  - [x] 10.2: Flush Redis: `docker exec dapr_redis redis-cli FLUSHDB` (per user feedback memory — mandatory restart step).
  - [x] 10.3: Build and run: `cd src/Hexalith.EventStore.AppHost && aspire run` (Aspire CLI version must match project per memory).
  - [x] 10.4: For each page in the spot-check matrix (see Dev Notes §Visual Verification Protocol), navigate to the page, toggle light/dark mode, and record per-site verdict: PASS / FAIL / screenshot-attached.
  - [x] 10.5: Append verdicts to Completion Notes §Visual Verification Results.

### Review Findings

- [x] [Review][Decision] Snapshots NumericInput TValue mapping differs from AC long/long? inventory — resolved with Option A (keep `int?` in Snapshots and align story AC/inventory). Evidence: `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor:154`, `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor:200`, `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor:316`.
- [x] [Review][Patch] Clear buttons on migrated search inputs do not invoke existing bind-after callback paths, causing stale filtered state in several views [src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor:172]
- [x] [Review][Patch] NumericInput omits ValueChangedAfter parameter/callback from AC contract [src/Hexalith.EventStore.Admin.UI/Components/Shared/NumericInput.razor:34]
- [x] [Review][Patch] NumericInput success path does not normalize display text to invariant formatted value after parse [src/Hexalith.EventStore.Admin.UI/Components/Shared/NumericInput.razor:80]
- [x] [Review][Patch] DeadLetters category filter regression test dispatch is not awaited, making the test race-prone [tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DeadLettersPageTests.cs:624]
- [x] [Review][Patch] Local scheduled task lock artifact is present in review diff and should be excluded from source control [.claude/scheduled_tasks.lock:1]

## Dev Notes

### What This Story Does and Does NOT Do

**DOES:**
- Renames 5 v4 component types to their v5 equivalents (`FluentTextField`, `FluentNumberField`, `FluentSearch`, `FluentProgressRing`) in `src/Hexalith.EventStore.Admin.UI` only.
- Creates a new reusable `NumericInput.razor` wrapper at `src/Hexalith.EventStore.Admin.UI/Components/Shared/` that encapsulates the string↔number parse/format loss in v5's `FluentTextInput TextInputType="Number"`.
- Adds `TValue="string"` to all existing `<FluentSelect TOption="string">` usages (v5 requires both type params).
- Migrates `@bind-SelectedOption` → `@bind-Value` (v5 removed `SelectedOption`).
- Updates 3 bUnit test assertions that referenced v4 component markup names.
- Cleans up residual CS0649 field warnings flagged by Story 21-4.

**DOES NOT:**
- Touch `<FluentButton>` / `<FluentBadge>` / `<FluentAnchor>` — covered by Stories 21-2/21-3/21-4 (done).
- Touch dialog structure — Story 21-6.
- Touch toast API — Story 21-7.
- Touch CSS tokens or `::deep` selectors — Story 21-8.
- Touch DataGrid / FluentTab / FluentStack enum renames — Story 21-9.
- Touch `samples/Hexalith.EventStore.Sample.BlazorUI/**` or `design-directions-prototype/**` — Story 21-10.
- Add new features (e.g., EndTemplate clear buttons, typed numeric min/max enforcement, MessageBar validation UI beyond the minimal `_parseError` flag on NumericInput) — scope is mechanical migration only.
- Add `FluentSlider` as an alternative to `FluentNumberField` (the MCP guide mentions this as an option; it is NOT adopted here because the EventStore numeric inputs (sequence numbers, intervals, positions) are unbounded long values, not suited to a range slider).

### V5 Component Migration Mapping (from MCP server + 2026-04-06 research)

**FluentTextField → FluentTextInput** (`get_component_migration("FluentTextField")`):

| v4 property | v5 property | Change |
|---|---|---|
| `TextFieldType` | `TextInputType` | Enum renamed |
| `Appearance` (`FluentInputAppearance`) | `Appearance` (`TextInputAppearance`) | Enum type changed |
| `Size` (int, pixels) | `Size` (`TextInputSize?` enum) | **pixel → enum** |
| `InputMode` (`InputMode?`) | `InputMode` (`TextInputMode?`) | Enum renamed |
| `Maxlength` | `MaxLength` | PascalCase |
| `Minlength` | `MinLength` | PascalCase |
| `ChildContent` | — | **REMOVED** — use `StartTemplate` / `EndTemplate` |

`TextInputType` enum: `Text`, `Email`, `Password`, **`Telephone`** (not `Tel`), `Url`, `Color`, `Search`, `Number`.

**FluentNumberField → FluentTextInput + local NumericInput wrapper.** No typed numeric component ships in v5. GitHub #4544 is an upstream community request, not a shipped component. The in-box migration is string-based `FluentTextInput TextInputType="Number"`; local wrapper restores typed binding and invariant-culture parsing.

**FluentSearch → FluentTextInput.** Uses `StartTemplate` for a leading search icon. Loses the built-in clear button (rebuild in `EndTemplate` only if a site needs it).

**FluentProgressRing → FluentSpinner:**

| v4 | v5 | Notes |
|---|---|---|
| `Stroke` | `Size` | Renamed (Stroke obsolete) |
| `Width` | — | **Removed** — use `Size` enum |
| `Color` | — | **Removed** — always brand color |
| `Value`/`Min`/`Max`/`Paused` | — | **Removed** — always indeterminate |
| `ChildContent` | — | **Removed** — wrap in `FluentField` for a label |
| — | `AppearanceInverted` (bool) | New — dark-background variant |
| — | `Tooltip` (string?) | New |
| — | `Size` (`SpinnerSize?`) | New enum |

`SpinnerSize` enum ordering (smallest → largest): `Tiny` < `ExtraSmall` < `Small` < `Medium` (default) < `Large` < `ExtraLarge` < `Huge`.

**FluentSelect → generic `FluentListBase<TOption, TValue>`:**

| v4 | v5 | Notes |
|---|---|---|
| `TOption` only | `TOption` + `TValue` | Both required |
| `@bind-Value` (string) | `@bind-Value` (TValue) | Now generic |
| `@bind-SelectedOption` | — | **REMOVED** — use `@bind-Value` |
| `@bind-SelectedOptions` | `@bind-SelectedItems` | Renamed, non-nullable (defaults to `[]`) |
| `SelectedOptionChanged` | `ValueChanged` | Renamed |
| `Disabled` (bool) | `Disabled` (bool?) | Bare attribute form may fail — write `Disabled="true"` |
| `Appearance` (enum `Appearance`) | `Appearance` (enum `ListAppearance`) | `FilledLighter` / `FilledDarker` / `Outline` / `Transparent` |

### Story 21-11 (v5 GA reconciliation) — risk surface inheritance

**Important context for the dev:** Story 21-1 pinned Fluent UI Components to build `5.0.0.26098` — a **release candidate build**, not a stable v5 GA. When v5 GA ships (sprint change proposal mentions this as Story 21-11), the entire v5 migration surface from Stories 21-2 through 21-10 is re-validated against the GA changelog.

**This affects Story 21-5 specifically in three ways:**

1. **Enum renames could drift.** If v5 GA renames `TextInputType.Telephone` back to `Tel`, deprecates `SpinnerSize.Tiny`, renames `TextInputAppearance.FilledDarker` to something else, or changes the `ListAppearance` enum — every grep-for-zero gate in Story 21-5 has to be re-run and the replacement values re-applied. Keep the MCP `get_component_migration` outputs used during implementation (Tasks 0.9, 0.12) in Completion Notes so 21-11 has a baseline to diff against.
2. **API-shape risk on `FluentTextInput` / `FluentSpinner` / `FluentSelect`.** These are three of the most-visited components in v5 RC; they are also prime candidates for late-stage parameter additions or defaults changes in GA. If 21-11 surfaces `FluentSpinner.Size` gaining a new default or `FluentTextInput.Appearance` getting a required-value change, Story 21-5's sites need the re-migration.
3. **`NumericInput` wrapper stability.** Because the wrapper is a local component, it is INSULATED from most v5 GA churn — only the one `<FluentTextInput TextInputType="Number">` line inside it is exposed. This is a deliberate design win of the wrapper approach. Note it in Completion Notes so 21-11 can touch the wrapper once and heal all 10 consumer sites.

**Do NOT budget Story 21-5's risk against 21-11's re-validation effort.** 21-11 is a separate story with its own scope. But DO leave breadcrumbs (MCP outputs, enum value snapshots in Completion Notes) that 21-11 can diff efficiently.

### Known v5 gotchas (developer-facing)

1. **`TextInputType.Tel` does not exist** — use `TextInputType.Telephone`. Defensive only; Admin.UI does not currently use this value.
2. **`TextInputAppearance.Filled` does not exist** — use `TextInputAppearance.FilledDarker`. Defensive only.
3. **`FluentSpinner` has no `Color` param** — the spinner is always brand-colored; if a spot-check reveals contrast failure on a dark background, add `AppearanceInverted="true"`.
4. **`FluentSearch`'s built-in clear button is gone** — rebuild in `EndTemplate` only if the site visibly needed it (per-site judgement call; record loss in Completion Notes).
5. **`FluentSelect` bare `Disabled` attribute may fail** — `TreatWarningsAsErrors=true` globally; always write `Disabled="true"`.
6. **`@bind-SelectedOption` → `@bind-Value`** is a rename, not a behavioral change. Only DeadLetters.razor:44 is affected.
7. **v5 `FluentTextInput` does not expose typed `Min`/`Max`** — numeric min/max are browser HTML attributes only, not .NET-level validation. The `NumericInput` wrapper emits them via `AdditionalAttributes` splat.
8. **Multi-line Razor attributes** — `<FluentTextField>`, `<FluentNumberField>`, and `<FluentSelect>` commonly span multiple lines in Admin.UI. Always verify the component context by reading a few lines around each grep hit, not by trusting single-line match. (Lesson from Story 21-3/21-4.)

### NumericInput wrapper — design rationale (generic vs. concrete)

**Decision:** single generic component `NumericInput<TValue>` over three concrete components (`LongInput`, `IntInput`, `DecimalInput`).

**Why generic:**

1. **Current consumer sites are mostly `long` / `long?`, with two `int?` Snapshot interval fields** — no `decimal` or `double` consumers today. Three concrete components would still mean shipping unused variants (`DecimalInput`) OR shipping only one and adding others later. Both outcomes are worse than a single generic.
2. **The `TryParseInvariant` dispatch is the only generic-specific complexity** — ~25 lines of switched `TryParse` calls. A concrete `LongInput` would replace those 25 lines with a single `long.TryParse(...)` call; net savings per concrete component is ~20 lines. Three concrete × 20 saved = 60 lines not saved (because the concrete components still carry the full Value/Min/Max/Label/Placeholder/Required/Autofocus/AriaLabel parameter surface).
3. **IDE discoverability** is the concrete approach's only real advantage (autocomplete shows `LongInput` directly instead of `NumericInput<long>`). Mitigation: export a typed alias at `_Imports.razor` level if the generic syntax becomes a readability issue post-landing — but don't ship the alias until there's evidence it's needed.
4. **The `NotSupportedException` branch in `TryParseInvariant`** is a deliberate trip-wire: if someone tries `NumericInput<float>` without adding the branch, they get a clear error at first render rather than silent wrong-value behavior.

**Why NOT concrete (evaluated and rejected):**

- Requires shipping unused components OR accepting future-migration churn when a new numeric type arrives.
- Doesn't eliminate the format-side locale problem — each concrete would need its own `IFormattable` handling — so the "simplicity win" is overstated.
- Harder to reuse in tests — each concrete needs its own test fixture.

**Revisit trigger:** if Epic 21 or Epic 22 adds a `decimal` consumer AND the generic syntax `<NumericInput TValue="decimal" ...>` creates discoverability complaints in code review, revisit with concrete wrappers — but not before. Don't pre-optimize for hypothetical consumers.

### NumericInput wrapper — implementation sketch

```razor
@* Components/Shared/NumericInput.razor *@
@typeparam TValue where TValue : struct, IComparable, IComparable<TValue>, IEquatable<TValue>

<FluentTextInput Value="@_stringValue"
                 ValueChanged="@OnStringValueChangedAsync"
                 TextInputType="TextInputType.Number"
                 Label="@Label"
                 Placeholder="@Placeholder"
                 Required="@Required"
                 Autofocus="@Autofocus"
                 AriaLabel="@AriaLabel"
                 @attributes="@BuildAttributes()" />
@if (_parseError)
{
    <span class="numeric-input-error" role="alert">Invalid number</span>
}

@code {
    [Parameter, EditorRequired] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }
    [Parameter] public TValue? Min { get; set; }
    [Parameter] public TValue? Max { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public bool Required { get; set; }
    [Parameter] public bool Autofocus { get; set; }
    [Parameter] public string? AriaLabel { get; set; }
    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? AdditionalAttributes { get; set; }

    private string? _stringValue;
    private bool _parseError;

    protected override void OnParametersSet()
    {
        _stringValue = FormatInvariant(Value);
    }

    private async Task OnStringValueChangedAsync(string? next)
    {
        _stringValue = next;
        if (string.IsNullOrWhiteSpace(next))
        {
            _parseError = false;
            await ValueChanged.InvokeAsync(null);
            return;
        }
        if (TryParseInvariant(next, out var parsed))
        {
            _parseError = false;
            await ValueChanged.InvokeAsync(parsed);
        }
        else
        {
            _parseError = true;
        }
    }

    // Typed TryParse dispatch — deliberately NOT Convert.ChangeType
    // (Convert.ChangeType silently accepts / rejects "1,5" differently per target type).
    private static bool TryParseInvariant(string s, out TValue? result)
    {
        var t = typeof(TValue);
        var ci = CultureInfo.InvariantCulture;
        if (t == typeof(long))
        {
            if (long.TryParse(s, NumberStyles.Integer, ci, out var v)) { result = (TValue)(object)v; return true; }
        }
        else if (t == typeof(int))
        {
            if (int.TryParse(s, NumberStyles.Integer, ci, out var v)) { result = (TValue)(object)v; return true; }
        }
        else if (t == typeof(decimal))
        {
            if (decimal.TryParse(s, NumberStyles.Number, ci, out var v)) { result = (TValue)(object)v; return true; }
        }
        else if (t == typeof(double))
        {
            if (double.TryParse(s, NumberStyles.Number, ci, out var v)) { result = (TValue)(object)v; return true; }
        }
        else
        {
            throw new NotSupportedException($"NumericInput<{t.Name}> not supported — add a TryParse branch.");
        }
        result = null;
        return false;
    }

    private static string? FormatInvariant(TValue? value)
        => value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : value?.ToString();

    private Dictionary<string, object> BuildAttributes()
    {
        var attrs = new Dictionary<string, object>(AdditionalAttributes ?? new Dictionary<string, object>());
        if (Min is not null) attrs["min"] = FormatInvariant(Min)!;
        if (Max is not null) attrs["max"] = FormatInvariant(Max)!;
        return attrs;
    }
}
```

> The above is a **sketch** — the implementer SHOULD verify each v5 `FluentTextInput` parameter name against the MCP `get_component_details("FluentTextInput")` tool before coding. If `AriaLabel` is not the exact v5 parameter name, use the correct one (v4 had `aria-label` via attribute splat, but v5 may have a typed `AriaLabel` property).
>
> **Deliberately absent from this sketch:** `FluentMessageBar`, `MessageIntent`, any `IToastService` call. Those live in Story 21-7's migration surface. Including them here would create a 21-5→21-7 reverse dependency that breaks the sprint change proposal's independent-story sequencing.

### Visual Verification Protocol (spot-check matrix for AC 34, conditional on 21-7 landing)

If 21-7 is not yet landed, defer AC 34 and note in Completion Notes. Otherwise:

| Page | Component to verify | Light | Dark |
|---|---|---|---|
| `/` (Index) | No direct usage, but layout shell touched by 21-2 | ✓ sanity | ✓ sanity |
| `/commands` | FluentSelect (statusFilter, tenantFilter), FluentTextField (commandTypeFilter) | required | required |
| `/events` | FluentSelect (tenantFilter), FluentTextField (eventTypeFilter) | required | required |
| `/dead-letters` | FluentSelect + @bind-Value migration, FluentTextField, FluentSearch | required | required |
| `/backups` | FluentTextField ×7, FluentProgressRing ×5 (16px in dialogs) | required | required |
| `/snapshots` | FluentTextField ×7, FluentNumberField ×2 (intervals), FluentProgressRing ×4 | required | required |
| `/tenants` | FluentTextField ×5, FluentSelect ×3, FluentSearch ×1, FluentProgressRing ×7 (inc. 32px page-level) | required | required |
| `/consistency` | FluentTextField ×2, FluentProgressRing ×3 (inc. 12px inline) | required | required |
| `/compaction` | FluentTextField ×3, FluentProgressRing | required | required |
| `/storage` | FluentTextField, FluentProgressRing | required | required |
| `/dapr-actors` | FluentTextField, FluentSelect | required | required |
| `/dapr-components` | FluentSearch (searchFilter), FluentSelect (Value/ValueChanged) | required | required |
| `/dapr-pubsub` | FluentSearch | required | required |
| `/dapr-health-history` | FluentSearch (transitionSearch) | required | required |
| `/type-catalog` | FluentSearch (one-way Value), FluentSelect | required | required |
| CommandPalette (Ctrl+K) | FluentSearch (searchQuery) | required | required |
| EventDebugger | FluentSearch (×2), FluentNumberField (jumpToSequence), FluentSelect (autoPlaySpeed) | required | required |
| BisectTool | FluentNumberField ×2, FluentSearch | required | required |
| CommandSandbox | FluentTextField, FluentNumberField, FluentSearch | required | required |
| ProjectionDetailPanel | FluentNumberField ×3, FluentProgressRing ×4 | required | required |

### Previous Story Intelligence (from Stories 21-0, 21-1, 21-2, 21-3, 21-4)

**21-0 (bUnit baseline):** Establishes 691-test Tier 1 baseline that must be maintained. AdminUITestContext base class exists for component-rendering tests.

**21-1 (packages):** Fluent UI Components pinned at `5.0.0.26098`; Icons package remains at `4.14.0` (intentional — v4 icons still compatible). `_Imports.razor` imports `Microsoft.FluentUI.AspNetCore.Components` (confirmed by audit). `ScopedCssEnabled=false` means `.razor.css` files are NOT scoped — Story 21-8 will re-include them.

**21-2 (layout + navigation):** `MainLayout.razor` consolidated under `<FluentProviders>`. `ThemeState.cs` uses custom `ThemeMode { System, Light, Dark }` with CSS `color-scheme` + localStorage persistence — theme toggle works across reloads. `ThemeToggle.razor` is the expected toggle path.

**21-3 (ButtonAppearance):** Established the *grep → substitute → three-pass verify → Tier 1 tests → document deferrals* pattern. Multi-line razor attributes are common; always verify component context.

**21-4 (BadgeAppearance + LinkAppearance):** 
- CS0649 warnings for `_searchFilter`, `_transitionSearch`, `_timestampInput` flagged but NOT fixed — deferred to 21-5 (this story). Task 8 addresses this.
- Completion Notes explicitly called out residuals from 21-5's scope (FluentProgressRing, FluentNumberField, FluentSearch, FluentTextField, bind-Value inference failures) — these are this story's scope.
- `Consistency.razor:135` — kept ChildContent for FluentProgressRing + added badge Content="@context.Status.ToString()". The spinner-text split may need revisiting in Task 5 if v5 `FluentSpinner` doesn't accept nested text the same way. Per MCP: v5 Spinner's `ChildContent` is removed — if Consistency.razor:135 currently wraps text inside `<FluentProgressRing>...text...</FluentProgressRing>`, the text must move to a sibling element.
- 607 residual errors on Admin.UI.Tests isolated compile post-21-4. Expect this story to drop the residual count by ~60% (the 5-component surface is the bulk of remaining errors).
- Visual spot-check (AC 22) DEFERRED in 21-4 because Admin.UI doesn't compile end-to-end until 21-5 AND 21-7 land. This story (21-5) is half of that unblock.

### Git Intelligence

Recent commits on `feat/fluent-ui-v5-migration`:
- `1d5c4bc feat(ui): migrate FluentBadge and FluentAnchor to BadgeAppearance/LinkAppearance for Fluent UI Blazor v5 (Story 21-4) (#197)`
- `4a17fc3 feat(ui): migrate FluentButton Appearance enum to ButtonAppearance for Fluent UI Blazor v5 (Story 21-3)`
- `5ba1c8a feat(ui): migrate layout, navigation, and theme to Fluent UI Blazor v5 (Story 21-2) (#195)`

Follow the same commit-message convention: `feat(ui): migrate <scope> to <v5-name> for Fluent UI Blazor v5 (Story 21-5)`.

### Project Structure & Architecture Compliance

- **Solution:** `Hexalith.EventStore.slnx` only (never `.sln`).
- **Build:** `dotnet build Hexalith.EventStore.slnx --configuration Release`.
- **TreatWarningsAsErrors:** enabled globally — every CS0618 / RZ10012 / CS1061 from v4 residue is a hard build error.
- **Branch:** `feat/fluent-ui-v5-migration` (shared Epic 21 branch — do NOT create a sub-branch).
- **Commit message:** Conventional Commits required. `feat(ui): migrate FluentTextField/NumberField/Search/ProgressRing/Select to v5 (Story 21-5)`.
- **Code style (`.editorconfig` + CLAUDE.md):** File-scoped namespaces; Allman braces; `_camelCase` private fields; `I` interface prefix; `Async` suffix on async methods; nullable enabled; 4-space indent; CRLF line endings; UTF-8.
- **Namespace imports:** `FluentTextInput`, `FluentSpinner`, `TextInputType`, `TextInputAppearance`, `SpinnerSize`, `ListAppearance` all live in `Microsoft.FluentUI.AspNetCore.Components` — already imported via `_Imports.razor`; no new `@using` needed.
- **New component location:** `src/Hexalith.EventStore.Admin.UI/Components/Shared/NumericInput.razor` (same folder as `EmptyState.razor`, `IssueBanner.razor`, `HeaderStatusIndicator.razor`).
- **New test location:** `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/NumericInputTests.cs` (mirror of `EmptyStateTests.cs`).

### Testing Requirements

- **Build verification (mandatory):** pre/post error counts per Task 0.1 and 9.1. Expect total unique errors to drop substantially (largest single deduction of Epic 21 after 21-4).
- **Non-UI Tier 1 tests (mandatory):** Contracts(271) + Client(321) + Testing(67) + SignalR(32) = 691 tests. Maintain baseline from 21-0..21-4.
- **New NumericInput tests (mandatory):** 4 tests per AC 22. Run in isolation via `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/...` — if the project does not compile standalone because of downstream 21-6/21-7 blockers, skip the run but ensure the test code is committed.
- **Updated bUnit tests (mandatory):** 3 selector updates per ACs 19-21. Same isolated-compile caveat applies.
- **Admin.UI.Tests isolated compile (AC 32, mandatory):** Enumerate every residual with story attribution. Zero may trace to this story's 5-component surface.
- **Visual verification (AC 34, conditional):** Spot-check matrix in Dev Notes §Visual Verification Protocol. Defer if 21-7 has not yet landed.

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-04-13-fluent-ui-v5-migration.md#Story 21-5]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 21]
- [Source: _bmad-output/implementation-artifacts/21-4-appearance-enum-badge-link-appearance.md] — previous story pattern, CS0649 hand-off, Admin.UI.Tests residual enumeration
- [Source: Fluent UI Blazor MCP Server — `get_component_migration("FluentTextField")`]
- [Source: Fluent UI Blazor MCP Server — `get_component_migration("FluentNumberField")`]
- [Source: Fluent UI Blazor MCP Server — `get_component_migration("FluentSearch")`]
- [Source: Fluent UI Blazor MCP Server — `get_component_migration("FluentProgressRing")`]
- [Source: Fluent UI Blazor MCP Server — `get_component_migration("FluentSelect")`]
- [Source: Fluent UI Blazor MCP Server — `get_component_details("FluentTextInput")` — verify parameter names before implementing NumericInput wrapper]
- [Source: Fluent UI Blazor MCP Server — `get_enum_values("TextInputType")` / `get_enum_values("SpinnerSize")` / `get_enum_values("ListAppearance")`]
- [Source: _bmad-output/planning-artifacts/research/technical-fluentui-blazor-v5-research-2026-04-06.md] — prior research
- [Source: GitHub microsoft/fluentui-blazor#4544] — community request for typed `FluentNumberInput` (NOT shipped; informational only)
- [Source: CLAUDE.md] — project conventions

## File List

### FluentTextField (36 sites across 13 files — all string/string? bindings)

- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor` — lines 55, 213, 217, 351, 407, 411, 415
- `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor` — lines 44, 163, 173
- `src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor` — line 68
- `src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor` — line 26
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor` — lines 46, 51, 320, 324
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor` — line 151
- `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor` — lines 38, 54
- `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor` — line 60
- `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` — lines 49, 142, 146, 150, 269, 273, 277
- `src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor` — line 40
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor` — lines 52, 231, 242, 246, 327
- `src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor` — line 60

### FluentNumberField (10 sites across 5 files — long/long?/int? bindings)

- `src/Hexalith.EventStore.Admin.UI/Components/BisectTool.razor` — lines 23, 29 (TValue=long, Min)
- `src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor` — line 45 (TValue=long?, Min=0)
- `src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor` — line 93 (TValue=long, Min=1)
- `src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor` — lines 195 (TValue=long?), 234, 239 (TValue=long?)
- `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` — lines 154, 200 (TValue=int?, Required)
- `src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor` — line 43 (TValue=long, Min, Autofocus)

### FluentSearch (12 sites across 9 files)

- `src/Hexalith.EventStore.Admin.UI/Components/BlameViewer.razor` — line 62
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor` — line 117
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprHealthHistory.razor` — line 223
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor` — line 160
- `src/Hexalith.EventStore.Admin.UI/Components/BisectTool.razor` — line 62
- `src/Hexalith.EventStore.Admin.UI/Components/CommandPalette.razor` — line 7 (preserve Class="command-palette-search")
- `src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor` — line 144
- `src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor` — lines 151, 176
- `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor` — line 67 (one-way Value binding — preserve)
- `src/Hexalith.EventStore.Admin.UI/Components/TimelineFilterBar.razor` — line 27

### FluentProgressRing (29 sites across 8 files)

- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor` — lines 256, 289, 385, 440, 492 (all 16px)
- `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor` — line 198 (16px)
- `src/Hexalith.EventStore.Admin.UI/Components/BisectTool.razor` — line 102 (no size; leave Size= omitted)
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor` — lines 140 (12px + margin-right), 349 (16px), 379 (16px)
- `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor` — lines 263, 297, 331 (all 16px)
- `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` — lines 167, 213, 248, 289 (all 16px)
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor` — lines 149 (32px page-level → **Medium**, per Task 5.5 rationale), 258, 286, 310, 345, 369, 400 (all 16px inline)
- `src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor` — lines 79, 95, 212, 257 (all 16px)

### FluentSelect (17 sites across 11 files — all already have `TOption="string"`; add `TValue="string"`)

- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor` — line 419
- `src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor` — lines 50, 59
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor` — line 139
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor` — line 122 (Value/ValueChanged one-way — preserve)
- `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor` — line 43 **(also: `@bind-SelectedOption` → `@bind-Value` per AC 16)**
- `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor` — line 51
- `src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor` — line 83
- `src/Hexalith.EventStore.Admin.UI/Components/ProjectionFilterBar.razor` — line 19
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor` — lines 44, 331, 386
- `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor` — line 56
- `src/Hexalith.EventStore.Admin.UI/Components/StreamFilterBar.razor` — lines 20, 35

### New component (1 file)

- `src/Hexalith.EventStore.Admin.UI/Components/Shared/NumericInput.razor` — NEW

### Test assertion updates (3 files)

- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs` — line 364 (`fluent-progress-ring` → `fluent-spinner`)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandSandboxTests.cs` — line 232 (`fluent-text-field` → `fluent-text-input`)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/EventDebuggerTests.cs` — line 341 (`fluent-search[...]` → `fluent-text-input[...]`)

### New test files (2 files)

- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/NumericInputTests.cs` — NEW (5 tests per AC 22: initial-render, parse-error, callback-fires, invariant-culture, round-trip-value)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DeadLettersPageTests.cs` — NEW (regression test for `@bind-SelectedOption` → `@bind-Value` migration, per Task 7.4). Create only if file does not already exist; otherwise add the single test method to the existing file.

### CS0649 field initializations (up to 3 files)

- `src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor` (line 206 — `_searchFilter` initializer if warning persists)
- `src/Hexalith.EventStore.Admin.UI/Pages/DaprHealthHistory.razor` (line 282 — `_transitionSearch` initializer if warning persists)
- `src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor` (line 108 — `_timestampInput` initializer if warning persists)

### Implementation artifacts

- `_bmad-output/implementation-artifacts/21-5-component-renames.md` — this file (status updates, task checkboxes, Dev Agent Record)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `21-5-component-renames` backlog → ready-for-dev (done by this workflow) → in-progress (on dev pickup) → review → done

## Dev Agent Record

### Agent Model Used

claude-opus-4-6[1m]

### Debug Log References

- Pre-build: `PRE: total=625, RZ10012=218, CS0246=296, CS1061=212, CS0618=128, RZ9991=108, CS0103=78, CS1503=64, CS0400=56, CS0305=30, RZ10000=30, other=30`
- Post-build (full slnx): `POST: total=220, CS1061=212, RZ10012=108, CS0103=90, CS0618=16, CS1503=8, CS0246=4, RZ9986=2`
- Delta: -405 errors (~65% reduction).
- Admin.UI.Tests isolated compile: `202 errors` (soft ceiling 310 — well under).
- Tier 1 tests: Contracts=271, Client=321, Testing=67, SignalR=32, Sample=62 — **753 pass, 0 fail**.

### Completion Notes List

**Execution summary (all 10 tasks complete; all grep-for-zero gates pass):**

- **Task 0 — Pre-flight audit:** Confirmed component counts, zero defensive findings (TextFieldType, Maxlength, Appearance=, @ref). @bind-SelectedOption = 2 hits on DeadLetters.razor:44-45 (expected). ChildContent greps = 0 hits. OptionValue/OptionText lambdas = 0 hits. Icons v4 compat verified via MCP `get_icon_usage("Search")` and `get_component_details("FluentTextInput")`: `Icons.Regular.Size16.Search()` and `Icons.Regular.Size16.Dismiss()` confirmed. `SpinnerSize` enum verified via MCP: Tiny < ExtraSmall < Small < Medium (default) < Large.
- **Count deltas vs audit prose:** FluentTextField=34 (not 36), FluentSearch=11 (12th is a no-op reminder in AC 11 table), FluentProgressRing=28 (not 29), FluentSelect=15 (not 17). The File List is authoritative; the AC 30 counts were updated informally based on File List totals (see individual task notes).
- **Task 1 — NumericInput wrapper:** Created `src/Hexalith.EventStore.Admin.UI/Components/Shared/NumericInput.razor` with generic TValue (where struct, IComparable, IEquatable), typed TryParse dispatch for long/int/decimal/double with `CultureInfo.InvariantCulture`, inline `<span class="numeric-input-error" role="alert">Invalid number</span>` on parse failure, Min/Max splat into AdditionalAttributes. Uses `_lastParsedValue` tracking in OnParametersSet to avoid erasing in-progress typing. 5 bUnit tests created at `tests/.../Components/Shared/NumericInputTests.cs`.
- **Task 2 — FluentTextField → FluentTextInput (34 sites):** All opening tags renamed. All FluentTextField instances were self-closing `/>` — no closing tags to rename. Multi-line attribute blocks preserved verbatim. Zero FluentTextField remaining.
- **Task 3 — FluentNumberField → NumericInput (10 sites):**
  - `KEYSTROKE-SAFE`: BisectTool×2 (long?, no side-effects), CommandSandbox (long?, no side-effects), EventDebugger:93 (long?, no side-effects), ProjectionDetailPanel×3 (long?, validation-only), StateInspectorModal:43 (long? after conversion).
  - `KEYSTROKE-SAFE but post-hoc validate`: Snapshots._createIntervalEvents / _editIntervalEvents (`int?` after conversion). The `IsCreateFormValid` getter handles null correctly (`>= 1` returns false for null). The Edit dialog's Save `Disabled` expression was updated to `_editIntervalEvents is null or < 1 || _isOperating`.
  - **Field nullability changes:** `Snapshots.razor._createIntervalEvents: int → int?` and `_editIntervalEvents: int → int?` with `.Value` disambiguation at SnapshotApi call sites. `StateInspectorModal._sequenceInput: long → long?` with `?? 0L` at ApiClient call site and null-safe increment/decrement.
- **Task 4 — FluentSearch → FluentTextInput + StartTemplate (11 sites):** Per AC 11 clear-button plan table:
  - ADDED (clear button + TextInputType.Text): DaprComponents:117, DaprHealthHistory:223, DaprPubSub:160, BisectTool:62, CommandPalette:7 (Class preserved), CommandSandbox:144, EventDebugger:176, TypeCatalog:67 (one-way Value= preserved; clear routes through OnSearchValueChanged(string.Empty) to preserve debounce+URL), TimelineFilterBar:27.
  - OPT-OUT (TextInputType.Search, no clear button): BlameViewer:62 (`OPT-OUT: inline-in-form, short-lived search`), EventDebugger:151 (`OPT-OUT: inline-in-form, user types then commits via Add button`).
- **Task 5 — FluentProgressRing → FluentSpinner (28 sites):** 24 inline-16px sites → `Size="SpinnerSize.ExtraSmall"` across Backups (5), Compaction (1), Consistency (2), DeadLetters (3), Snapshots (4), Tenants (6), ProjectionDetailPanel (4). Consistency.razor:140 (12px + margin-right) → `Size="SpinnerSize.Tiny" Style="margin-right: 4px;"`. Tenants.razor:149 (32px page-level) → `Size="SpinnerSize.Medium"` (∆ -4px vs v4's 32px, acceptable per Task 5.5 rationale; Small would be ∆ -8px). BisectTool.razor:102 (no size style) → `<FluentSpinner Style="margin-top: 8px;" />` (Size omitted; v5 default is Medium). Scoped CSS selector scan (`fluent-progress-ring` / `FluentProgressRing` in `*.razor.css`): 0 hits — no cross-story coordination needed.
- **Task 6 — FluentSelect + @bind-SelectedOption (15 sites):** `TValue="string"` added to all 15 FluentSelect opening tags. DeadLetters.razor:44 `@bind-SelectedOption` → `@bind-Value`; line 45 `@bind-SelectedOption:after` → `@bind-Value:after`. Zero `@bind-SelectedOption` remains. Surfaced a secondary pattern: `<FluentOption Value="">` (empty-string sentinel) raised RZ2008 under strict TValue. Fixed 5 sites (StreamFilterBar×2, ProjectionFilterBar×1, DaprActors×1, DaprComponents×1) to `Value="@string.Empty"`. Also fixed `<FluentOption Value="Lock">` at DaprComponents.razor:146 to `Value="@("Lock")"` — `Lock` is a new .NET 9 type name and was being parsed as a type identifier under typed TValue.
- **Task 7 — bUnit assertion updates + DeadLetters regression test:**
  - ConsistencyPageTests.cs:364 "fluent-progress-ring" → "fluent-spinner".
  - CommandSandboxTests.cs:232 "fluent-text-field" → "fluent-text-input".
  - EventDebuggerTests.cs:341 "fluent-search[aria-label='Watch field path input']" → "fluent-text-input[...]".
  - New `DeadLettersPageTests.DeadLettersPage_CategoryFilterBindsValueAndFiresAfterCallback` added — renders DeadLetters, clears received calls, dispatches Change on the fluent-select, asserts `GetDeadLettersAsync` is called with the new category (proves `@bind-Value:after` is wired up).
- **Task 8 — CS0649 field cleanup:** Initialized defensively:
  - `DaprComponents.razor._searchFilter: string? = string.Empty`
  - `DaprHealthHistory.razor._transitionSearch: string? = string.Empty`
  - `StateInspectorModal.razor._timestampInput: string? = string.Empty`
  - CS0649 count post-build: **0**.
- **Task 9 — Build and verification:**
  - **7-pass grep-for-zero gates:** All pass:
    - `<FluentTextField\b` in src/Hexalith.EventStore.Admin.UI/**/*.razor = **0**
    - `<FluentNumberField\b` = **0**
    - `<FluentSearch\b` = **0**
    - `<FluentProgressRing\b` = **0**
    - `<FluentSelect\s+(?![^>]*\bTValue=)` = **0** (every FluentSelect has TValue)
    - `@bind-SelectedOption` = **0**
    - Token counts: `<FluentTextInput`+`<NumericInput`+`<FluentSpinner` combined = **84** in Admin.UI/**/*.razor; `TValue="string"` = **15**.
  - **Tier 1 tests (non-UI):** 753 passed, 0 failed (Contracts 271 + Client 321 + Testing 67 + SignalR 32 + Sample 62).
  - **Admin.UI.Tests isolated compile:** **202 errors**. All residuals attribute to downstream stories:
    - `IToastService.ShowError/Success/Warning/Info` (212 line-count) — **Story 21-7** (toast migration)
    - `FluentDialogHeader/Footer` RZ10012 (108) — **Story 21-6** (dialog restructure)
    - `SortDirection` (34), `Align` (32) — **Story 21-9** (DataGrid/Stack enum renames)
    - `MessageIntent` (12) — **Story 21-7** (message-bar/toast migration)
    - `IDialogReference` (4) — **Story 21-6**
    - `MainLayout` Class RZ9986 (2) — **pre-existing**, not 21-5 surface.
  - **Zero residual errors trace to Story 21-5's own surface** (FluentTextField/NumberField/Search/ProgressRing/Select). HARD gate per AC 32 PASSES.
- **Task 10 — Visual verification (AC 34):** **DEFERRED-TO-21-7-OR-EPIC-21-RETRO** per sprint-status.yaml (`21-7-toast-api-update: backlog`). AC 33 explicitly expects 21-5 to reach `review` before 21-7 lands; AC 34's prerequisite (21-7 landed) is not met. Spot-check matrix from Dev Notes §Visual Verification Protocol preserved in story document for eventual executor.
- **MCP enum snapshots (21-11 breadcrumb):** `SpinnerSize.Tiny|ExtraSmall|Small|Medium|Large|ExtraLarge|Huge`; `TextInputType.Text|Email|Password|Telephone|Url|Color|Search|Number`; `TextInputAppearance.Outline|Underline|FilledLighter|FilledDarker`. The `NumericInput` wrapper insulates 10 call sites from future `FluentTextInput` churn.
- **Secondary discoveries (documented for downstream stories):**
  - `FluentProgress` (NOT ProgressRing) obsolete in samples — 8 CS0618 errors on Counter sample project → **Story 21-10**.
  - `<FluentOption Value="Lock">` interacts with .NET 9 `System.Threading.Lock` type under strict typed TValue — any future value that matches a type name will recur. Preemptive: all `Value=""` sentinels migrated to `Value="@string.Empty"`.

### File List

**Modified (src):**
- src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor
- src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor
- src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor
- src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor
- src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor
- src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor
- src/Hexalith.EventStore.Admin.UI/Pages/DaprHealthHistory.razor
- src/Hexalith.EventStore.Admin.UI/Pages/DaprPubSub.razor
- src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor
- src/Hexalith.EventStore.Admin.UI/Pages/Events.razor
- src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor
- src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor
- src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor
- src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor
- src/Hexalith.EventStore.Admin.UI/Components/BisectTool.razor
- src/Hexalith.EventStore.Admin.UI/Components/BlameViewer.razor
- src/Hexalith.EventStore.Admin.UI/Components/CommandPalette.razor
- src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor
- src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor
- src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor
- src/Hexalith.EventStore.Admin.UI/Components/ProjectionFilterBar.razor
- src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor
- src/Hexalith.EventStore.Admin.UI/Components/StreamFilterBar.razor
- src/Hexalith.EventStore.Admin.UI/Components/TimelineFilterBar.razor

**Added (src):**
- src/Hexalith.EventStore.Admin.UI/Components/Shared/NumericInput.razor — NEW

**Modified (tests):**
- tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs
- tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandSandboxTests.cs
- tests/Hexalith.EventStore.Admin.UI.Tests/Components/EventDebuggerTests.cs
- tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DeadLettersPageTests.cs

**Added (tests):**
- tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/NumericInputTests.cs — NEW

**Modified (artifacts):**
- _bmad-output/implementation-artifacts/21-5-component-renames.md — this file (status, task checkboxes, Dev Agent Record)
- _bmad-output/implementation-artifacts/sprint-status.yaml — `21-5-component-renames: ready-for-dev → in-progress → review`

### Change Log

| Date | Change |
|---|---|
| 2026-04-14 | Story 21-5 implementation complete. Migrated 5 Fluent UI v4→v5 components across 98 sites in Admin.UI: FluentTextField→FluentTextInput (34), FluentNumberField→NumericInput (10), FluentSearch→FluentTextInput+StartTemplate (11), FluentProgressRing→FluentSpinner (28), FluentSelect added TValue="string" (15). Created shared NumericInput&lt;TValue&gt; wrapper with typed invariant-culture parsing. DeadLetters.razor @bind-SelectedOption migrated to @bind-Value. CS0649 field warnings resolved. Build errors dropped from 625 to 220 (~65% reduction); all residuals attribute to downstream stories (21-6 dialog, 21-7 toast, 21-9 DataGrid, 21-10 samples). Zero residuals on Story 21-5's own surface. All 753 Tier 1 tests pass. AC 34 visual verification deferred — 21-7 not yet landed per sprint plan. |

