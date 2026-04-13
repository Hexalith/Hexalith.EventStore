# Story 21.3: Appearance Enum — ButtonAppearance

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer migrating from Fluent UI Blazor v4 to v5,
I want all `Appearance` enum usages on `FluentButton` components replaced with the new `ButtonAppearance` enum,
so that the codebase compiles against v5 APIs and the ~189 obsolete `Appearance` references on buttons are eliminated.

## Acceptance Criteria

1. **Given** any `.razor` file in `src/Hexalith.EventStore.Admin.UI/` containing `Appearance="Appearance.Accent"` on a `<FluentButton>`,
   **When** migrated,
   **Then** it reads `Appearance="ButtonAppearance.Primary"`,
   **And** this applies to all occurrences (~42 expected, grep-for-zero is the hard gate).

2. **Given** any `.razor` file containing `Appearance="Appearance.Outline"` on a `<FluentButton>`,
   **When** migrated,
   **Then** it reads `Appearance="ButtonAppearance.Outline"`,
   **And** this applies to all occurrences (~100 expected, grep-for-zero is the hard gate).

3. **Given** any `.razor` file containing `Appearance="Appearance.Lightweight"` on a `<FluentButton>`,
   **When** migrated,
   **Then** it reads `Appearance="ButtonAppearance.Transparent"`,
   **And** this applies to all occurrences (~18 expected, grep-for-zero is the hard gate).

4. **Given** any `.razor` file containing `Appearance="Appearance.Stealth"` on a `<FluentButton>`,
   **When** migrated,
   **Then** it reads `Appearance="ButtonAppearance.Subtle"`,
   **And** this applies to all occurrences (~7 expected, grep-for-zero is the hard gate).

5. **Given** any `.razor` file containing `Appearance="Appearance.Neutral"` on a `<FluentButton>`,
   **When** migrated,
   **Then** it reads `Appearance="ButtonAppearance.Default"`,
   **And** this applies to all occurrences (grep-for-zero is the hard gate).

5b. **Given** any `.razor` file containing `Appearance="Appearance.Hypertext"` or `Appearance="Appearance.Filled"` on a `<FluentButton>`,
    **When** migrated,
    **Then** it reads `Appearance="ButtonAppearance.Default"` (both map to Default per MCP guide).

6. **Given** ternary/conditional expressions that evaluate to `Appearance.X` for a `FluentButton`,
   **When** migrated,
   **Then** both branches use `ButtonAppearance.X` equivalents,
   **And** the expression type is `ButtonAppearance` (not `Appearance`).

7. **Given** code-behind methods that return `Appearance` for FluentButton consumption (e.g., `CommandPipeline.razor` `GetAppearance` method),
   **When** migrated,
   **Then** the return type is `ButtonAppearance`,
   **And** all returned values use `ButtonAppearance.X` equivalents.

7b. **Given** `HeaderStatusIndicator.razor` `_badgeAppearance` switch expression,
    **When** the template is inspected to determine the consuming component,
    **Then** if consumed by `<FluentButton>`, migrate return type and values to `ButtonAppearance`,
    **Or** if consumed by `<FluentBadge>`, leave as-is, document the finding in Completion Notes, and defer to Story 21-4.

8. **Given** test files asserting on `FluentButton` `Appearance` values (e.g., `ConsistencyPageTests.cs` line 305),
   **When** migrated,
   **Then** assertions use `ButtonAppearance.X` equivalents.

9. **Given** all FluentButton Appearance changes are complete,
   **When** a grep for `Appearance="Appearance.` across all `<FluentButton>` elements in `src/` returns zero results,
   **Then** the migration is verified complete.
   **Note:** CS0618 errors will remain for `Appearance` on `FluentBadge`/`FluentAnchor` — those are Story 21-4 scope and expected.

10. **Given** all non-UI Tier 1 tests (Contracts, Client, Testing, SignalR = 691 tests),
    **When** run after migration,
    **Then** all pass with zero regressions.

11. **Given** the `Admin.UI.Tests` project,
    **When** a build is attempted after this story,
    **Then** it is NOT expected to compile fully (other v5 errors remain from Stories 21-4+),
    **And** FluentButton Appearance assertions in test files are updated to `ButtonAppearance` equivalents regardless of whether the test project compiles.

## Tasks / Subtasks

- [x] Task 1: Replace all static `Appearance.Accent` on FluentButton with `ButtonAppearance.Primary` (AC: 1)
  - [x] 1.1: Grep all `.razor` files in `src/Hexalith.EventStore.Admin.UI/` for `Appearance="Appearance.Accent"` on FluentButton elements
  - [x] 1.2: Replace each occurrence with `Appearance="ButtonAppearance.Primary"`
  - [x] 1.3: Verify no `Appearance.Accent` remains on any FluentButton

- [x] Task 2: Replace all static `Appearance.Outline` on FluentButton with `ButtonAppearance.Outline` (AC: 2)
  - [x] 2.1: Grep all `.razor` files for `Appearance="Appearance.Outline"` on FluentButton elements
  - [x] 2.2: Replace each occurrence with `Appearance="ButtonAppearance.Outline"`
  - [x] 2.3: Verify no `Appearance.Outline` remains on any FluentButton

- [x] Task 3: Replace all static `Appearance.Lightweight` on FluentButton with `ButtonAppearance.Transparent` (AC: 3)
  - [x] 3.1: Grep all `.razor` files for `Appearance="Appearance.Lightweight"` on FluentButton elements
  - [x] 3.2: Replace each occurrence with `Appearance="ButtonAppearance.Transparent"`

- [x] Task 4: Replace all static `Appearance.Stealth` on FluentButton with `ButtonAppearance.Subtle` (AC: 4)
  - [x] 4.1: Grep all `.razor` files for `Appearance="Appearance.Stealth"` on FluentButton elements
  - [x] 4.2: Replace each occurrence with `Appearance="ButtonAppearance.Subtle"`

- [x] Task 5: Replace all static `Appearance.Neutral` on FluentButton with `ButtonAppearance.Default` (AC: 5)
  - [x] 5.1: Grep all `.razor` files for `Appearance="Appearance.Neutral"` on FluentButton elements
  - [x] 5.2: Replace each occurrence with `Appearance="ButtonAppearance.Default"`

- [x] Task 5b: Replace any `Appearance.Hypertext` or `Appearance.Filled` on FluentButton with `ButtonAppearance.Default` (AC: 5b)
  - [x] 5b.1: Grep for `Appearance.Hypertext` and `Appearance.Filled` in `src/` `.razor` files — if found on FluentButton, replace with `ButtonAppearance.Default`
  - [x] 5b.2: If none found, note "zero occurrences" in Completion Notes

- [x] Task 6: Migrate ternary/conditional Appearance expressions on FluentButton (AC: 6)
  - [x] 6.1: Identify all ternary expressions (e.g., `@(_active ? Appearance.Accent : Appearance.Outline)`) on FluentButton — found in: `ProjectionFilterBar.razor`, `StreamFilterBar.razor`, `TimelineFilterBar.razor` (5 buttons), `StorageTreemap.razor` (2 buttons), `DaprHealthHistory.razor`
  - [x] 6.2: Replace both branches with `ButtonAppearance.X` equivalents

- [x] Task 7: Migrate code-behind methods returning Appearance for FluentButton (AC: 7)
  - [x] 7.1: `CommandPipeline.razor` line 40: Verified `GetAppearance` is consumed by `<FluentBadge>` (line 11), NOT FluentButton — deferred to Story 21-4
  - [x] 7.2: `HeaderStatusIndicator.razor` line 24: `_badgeAppearance` consumed by `<FluentBadge>` (line 4) — deferred to Story 21-4

- [x] Task 7c: Scan for `Appearance`-typed variable declarations in `@code` blocks (AC: 7)
  - [x] 7c.1: Grep `src/Hexalith.EventStore.Admin.UI/ --include="*.razor"` for patterns: `Appearance ` (the type followed by a space, indicating a variable declaration like `Appearance buttonStyle = ...`)
  - [x] 7c.2: For each hit, check if the variable is consumed by a FluentButton in the template — if yes, change type to `ButtonAppearance` and update the assigned value
  - [x] 7c.3: If consumed by FluentBadge/FluentAnchor, leave for Story 21-4

- [x] Task 8: Migrate test assertions on FluentButton Appearance (AC: 8)
  - [x] 8.1: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs` line 305: change `Appearance.Accent` assertion to `ButtonAppearance.Primary`
  - [x] 8.2: Grep all test files (`tests/**/*.cs`) for remaining `Appearance.` references that apply to FluentButton context — update accordingly
  - [x] 8.3: Note: `EmptyStateTests.cs` line 25 is a comment about FluentAnchor — Story 21-4 scope, do NOT modify

- [x] Task 9: Build and verification (AC: 9, 10, 11)
  - [x] 9.1: Run `dotnet build Hexalith.EventStore.slnx --configuration Release` and record error count
  - [x] 9.2: **Two-pass verification (CRITICAL — do NOT skip):**
    - Pass 1 (residual hunt): 0 FluentButton hits with old Appearance.X values — all remaining are FluentBadge/FluentAnchor (Story 21-4)
    - Pass 2 (replacement confirmation): 181 ButtonAppearance. occurrences across 41 files (expected ~189, close match)
  - [x] 9.3: Run non-UI Tier 1 tests: Contracts(271) + Client(321) + Testing(67) + SignalR(32) = 691/691 passed

## Dev Notes

### What This Story Does and Does NOT Do

**DOES:** Replaces all `Appearance` enum values on `<FluentButton>` components with `ButtonAppearance` equivalents. Covers .razor templates, code-behind methods returning Appearance for button context, ternary expressions on buttons, and test assertions.

**DOES NOT:**
- Touch `FluentBadge` Appearance values (Story 21-4 — `BadgeAppearance`)
- Touch `FluentAnchor` Appearance values (Story 21-4 — `LinkAppearance`)
- Touch `GetStatusBadge` tuple return methods in Backups.razor, Compaction.razor, Consistency.razor, Tenants.razor (Story 21-4 — these return `(Appearance, string)` for FluentBadge consumption)
- Touch `samples/` directory files (Story 21-10 — Sample BlazorUI alignment)
- Touch component renames like FluentTextField/FluentSearch (Story 21-5)
- Touch dialog restructure (Story 21-6)
- Touch CSS tokens (Story 21-8)

### V5 ButtonAppearance Migration Mapping (from MCP Server)

| v4 (`Appearance`) | v5 (`ButtonAppearance`) |
|---|---|
| `Appearance.Neutral` | `ButtonAppearance.Default` |
| `Appearance.Accent` | `ButtonAppearance.Primary` |
| `Appearance.Lightweight` | `ButtonAppearance.Transparent` |
| `Appearance.Outline` | `ButtonAppearance.Outline` |
| `Appearance.Stealth` | `ButtonAppearance.Subtle` |
| `Appearance.Hypertext` | `ButtonAppearance.Default` |
| `Appearance.Filled` | `ButtonAppearance.Default` |

**Source:** Fluent UI Blazor MCP Server `get_component_migration("FluentButton")`

### Other FluentButton Parameter Changes (NOT in scope — note for awareness)

The MCP migration guide also documents these FluentButton renames (handle ONLY IF encountered while doing Appearance changes — do not seek them out as a separate pass):
- `Autofocus` → `AutoFocus` (bool? → bool)
- `Action` → `FormAction`
- `Enctype` → `FormEncType`
- `Method` → `FormMethod`
- `NoValidate` → `FormNoValidate`
- `Target` → `FormTarget`

New v5 parameters available (do NOT add proactively): `Shape`, `Size`, `DisabledFocusable`, `IconOnly`, `Label`, `Tooltip`.

### Code-Behind Patterns Requiring Special Attention

**1. CommandPipeline.razor (lines 40-47) — FluentButton context:**
```csharp
private Appearance GetAppearance(string stage)
{
    // ... logic ...
    return Appearance.Accent;    // → ButtonAppearance.Primary
    // ...
    return Appearance.Neutral;   // → ButtonAppearance.Default
}
```
Change return type from `Appearance` to `ButtonAppearance`. Update all returned values.

**2. HeaderStatusIndicator.razor (lines 24-28) — CHECK CONTEXT:**
```csharp
private Appearance _badgeAppearance => ConnectionStatus switch
{
    ConnectionStatusType.Connected => Appearance.Accent,
    ConnectionStatusType.Disconnected => Appearance.Lightweight,
    _ => Appearance.Neutral,
};
```
Despite the field name `_badgeAppearance`, check the template: if it's consumed by `<FluentButton Appearance="@_badgeAppearance">`, migrate to `ButtonAppearance`. If consumed by `<FluentBadge>`, leave for Story 21-4.

**3. GetStatusBadge tuple methods — Story 21-4 scope, DO NOT TOUCH:**
- `Backups.razor:795` — `private static (Appearance Appearance, string CssClass) GetStatusBadge(...)`
- `Compaction.razor:419` — same pattern
- `Consistency.razor:815, 826` — same pattern (2 methods)
- `Tenants.razor:662` — same pattern

These return `Appearance` for FluentBadge consumption → Story 21-4 changes to `BadgeAppearance`.

**4. Ternary expressions on FluentButton (~10 buttons with 20 Appearance references):**
```razor
<!-- Pattern: both branches must change -->
<FluentButton Appearance="@(_active ? Appearance.Accent : Appearance.Outline)" ...>
<!-- becomes: -->
<FluentButton Appearance="@(_active ? ButtonAppearance.Primary : ButtonAppearance.Outline)" ...>
```
Found in: `ProjectionFilterBar.razor`, `StreamFilterBar.razor`, `TimelineFilterBar.razor` (5), `StorageTreemap.razor` (2), `DaprHealthHistory.razor`.

### Search Strategy: Multi-Line Razor Attributes

Razor attributes can span multiple lines. A `FluentButton` and its `Appearance=` may not be on the same line:
```razor
<FluentButton OnClick="@HandleClick"
              Appearance="Appearance.Accent"
              IconStart="@(new Icons.Regular.Size20.Add())">
```
Use `Appearance="Appearance.` as the primary grep pattern, then verify component context by reading surrounding lines (scroll up to find the `<FluentButton` opening tag). Do NOT rely on a single-line grep that matches both `FluentButton` and `Appearance` on the same line.

Also check for whitespace variations: `Appearance = "Appearance.` (space around `=`) and unquoted code-behind patterns like `Appearance.Accent` (no surrounding quotes — these appear in `@code` blocks, ternary expressions, and switch arms).

### Expected Build State After This Story

After completing this story, **CS0618 (obsolete) errors will remain** in the build for:
- `Appearance.` references on `FluentBadge` components (Story 21-4)
- `Appearance.` references on `FluentAnchor` components (Story 21-4)
- `GetStatusBadge` tuple return types in Backups/Compaction/Consistency/Tenants (Story 21-4)

This is expected. The hard verification gate for this story is **grep-for-zero on FluentButton** (Task 9.2), not total build error count.

### Scoping Rule: How to Determine Button vs Badge/Anchor Context

When encountering `Appearance.X` in code-behind:
1. Trace where the value is consumed in the template
2. If consumed by `<FluentButton>` → Story 21-3 (this story): change to `ButtonAppearance.X`
3. If consumed by `<FluentBadge>` → Story 21-4: leave as-is
4. If consumed by `<FluentAnchor>` → Story 21-4: leave as-is
5. If consumed by multiple component types → **do NOT change the return type.** Leave the method as `Appearance` for now. Add a comment in the code: `// TODO Story 21-4: split into ButtonAppearance + BadgeAppearance`. Changing the return type to `ButtonAppearance` here would break the FluentBadge binding and cause a compile error that Story 21-4 must fix in a different way (BadgeAppearance).

**Dual-consumer example to watch for:**
```csharp
// If GetStyle() is consumed by BOTH <FluentButton> AND <FluentBadge> in the template:
private Appearance GetStyle() => isActive ? Appearance.Accent : Appearance.Neutral;
// Do NOT change to ButtonAppearance — it would break the FluentBadge consumer.
// Leave as-is and document for Story 21-4.
```

### Files Already Migrated by Story 21-2

These files had FluentButton Appearance changes done in Story 21-2 as part of the layout foundation. **Do NOT modify again** (they already use `ButtonAppearance`):
- `src/Hexalith.EventStore.Admin.UI/Components/App.razor` — `Appearance.Accent` → `ButtonAppearance.Primary` (done)
- `src/Hexalith.EventStore.Admin.UI/Components/ThemeToggle.razor` — `Appearance.Stealth` → `ButtonAppearance.Subtle` (done)

### Estimated File List (~41 files in src/Hexalith.EventStore.Admin.UI/)

**High-count files (8+ occurrences):**
- `Pages/Tenants.razor` (~19 FluentButton Appearance refs)
- `Pages/Backups.razor` (~18 FluentButton Appearance refs)
- `Components/EventDebugger.razor` (~16)
- `Pages/Snapshots.razor` (~12)
- `Pages/DeadLetters.razor` (~11)
- `Components/ProjectionDetailPanel.razor` (~10)
- `Pages/Consistency.razor` (~9)
- `Components/StreamDetail.razor` (~8)

**Medium-count files (4-7):**
- `Components/BisectTool.razor` (~7)
- `Components/TimelineFilterBar.razor` (~5, includes ternaries)
- `Pages/DaprHealthHistory.razor` (~5, includes ternary)
- `Pages/DaprComponents.razor` (~5)
- `Components/EventDetailPanel.razor` (~5)
- `Components/Shared/CommandPipeline.razor` (~3, includes code-behind method)

**Low-count files (1-3) — numerous, grep to discover all.**


### Project Structure & Architecture Compliance

- **Solution file:** `Hexalith.EventStore.slnx` (modern XML format only)
- **Build:** `dotnet build Hexalith.EventStore.slnx --configuration Release`
- **TreatWarningsAsErrors:** Enabled globally — CS0618 obsolete warnings are build errors
- **Namespaces:** File-scoped (`namespace X.Y.Z;`)
- **Braces:** Allman style
- **Private fields:** `_camelCase`
- **Nullable:** Enabled globally
- **Branch:** `feat/fluent-ui-v5-migration` — all Epic 21 stories share this branch
- **`ButtonAppearance` namespace:** `Microsoft.FluentUI.AspNetCore.Components` — already imported via `_Imports.razor`, no new `@using` needed

### Testing Requirements

- **Build verification (mandatory):** `dotnet build Hexalith.EventStore.slnx --configuration Release` — CS0618 errors for `Appearance` on FluentButton must be eliminated
- **Non-UI Tier 1 tests (mandatory):** Contracts(271) + Client(321) + Testing(67) + SignalR(32) = 691 tests must pass
- **Admin.UI.Tests:** Will likely still not compile until Stories 21-4+ resolve remaining component errors. Update FluentButton Appearance assertions in tests, but do not expect full bUnit suite to pass yet.
- **Visual verification:** Deferred to post-21-9 (build still broken from other stories). Record "visual verification deferred" in Completion Notes.

### Previous Story Intelligence (Story 21-2)

Key learnings from 21-2:
- Package version is `5.0.0-rc.2-26098.1` (RC prerelease)
- Icons package remains at `4.14.0`
- `TreatWarningsAsErrors=true` means all obsolete API warnings are build errors
- After 21-2: 219 unique errors reported, but the real picture is ~64 layout errors eliminated and ~866 new errors exposed (CS0618 obsolete, CS1503 type mismatch) that were previously masked. Story 21-3's Appearance changes should eliminate a significant portion of those CS0618 errors.
- ThemeMode: v5 provides `ThemeMode { Light, Dark, System }` in `Microsoft.FluentUI.AspNetCore.Components` — used directly (no custom enum needed). Same namespace approach applies to `ButtonAppearance`.
- All 691 non-UI Tier 1 tests pass through 21-2 — baseline to maintain.

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-04-13-fluent-ui-v5-migration.md#Story 21-3]
- [Source: _bmad-output/implementation-artifacts/21-2-layout-and-navigation-foundation.md] — previous story learnings + file states
- [Source: Fluent UI Blazor MCP Server — get_component_migration("FluentButton")] — v5 ButtonAppearance mapping
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/CommandPipeline.razor] — code-behind Appearance method
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/HeaderStatusIndicator.razor] — code-behind switch expression
- [Source: tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs] — test assertion on Appearance

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

None required — clean implementation with no blockers.

### Completion Notes List

- **Task 1**: Replaced 37 static `Appearance.Accent` on FluentButton → `ButtonAppearance.Primary` across 15 files. Remaining `Appearance.Accent` on FluentBadge (5 files) and FluentAnchor (1 file) correctly left for Story 21-4.
- **Task 2**: Replaced 100 static `Appearance.Outline` on FluentButton → `ButtonAppearance.Outline` across 29 files. Zero non-FluentButton occurrences existed.
- **Task 3**: Replaced 18 static `Appearance.Lightweight` on FluentButton → `ButtonAppearance.Transparent` across 11 files. Remaining on FluentBadge (8 files) left for Story 21-4.
- **Task 4**: Replaced 8 static `Appearance.Stealth` on FluentButton → `ButtonAppearance.Subtle` across 8 files. 1 FluentAnchor occurrence (IssueBanner.razor) left for Story 21-4.
- **Task 5**: Zero `Appearance.Neutral` found on FluentButton — all 5 occurrences are on FluentBadge (Story 21-4 scope).
- **Task 5b**: Zero `Appearance.Hypertext` or `Appearance.Filled` found on FluentButton — all occurrences are on FluentAnchor (Story 21-4 scope).
- **Task 6**: Migrated 10 ternary expressions across 5 files (ProjectionFilterBar, StreamFilterBar, TimelineFilterBar, StorageTreemap, DaprHealthHistory).
- **Task 7**: CommandPipeline.razor `GetAppearance()` is consumed by `<FluentBadge>` (line 11), NOT FluentButton — deferred to Story 21-4. HeaderStatusIndicator.razor `_badgeAppearance` is consumed by `<FluentBadge>` (line 4) — deferred to Story 21-4. Dev notes initially indicated FluentButton context for CommandPipeline but actual template verification showed FluentBadge.
- **Task 7c**: All 6 `Appearance`-typed variable declarations in `@code` blocks (Backups, Compaction, Consistency x2, Tenants, HeaderStatusIndicator) are consumed by FluentBadge — Story 21-4 scope, no changes needed.
- **Task 8**: Updated ConsistencyPageTests.cs line 305 assertion from `Appearance.Accent` to `ButtonAppearance.Primary`. EmptyStateTests.cs line 25 is a FluentAnchor comment — correctly left for Story 21-4.
- **Task 9**: Build has 745 errors (all pre-existing v5 migration issues from Stories 21-4+). Grep-for-zero verification: 0 FluentButton hits with old Appearance values. 181 ButtonAppearance replacements confirmed across 41 files. All 691 non-UI Tier 1 tests pass (271+321+67+32).
- **Visual verification**: Deferred to post-21-9 (build still broken from other stories).

### Change Log

- 2026-04-13: Story 21-3 implemented — migrated all FluentButton Appearance enum values to ButtonAppearance across 41 .razor files and 1 test file. 181 ButtonAppearance references now in place. All 691 Tier 1 tests pass.

### File List

**Modified .razor files (41 total in src/Hexalith.EventStore.Admin.UI/):**

Components:
- Components/BisectTool.razor
- Components/BlameViewer.razor
- Components/CausationChainView.razor
- Components/CommandDetailPanel.razor
- Components/CommandPalette.razor
- Components/CommandSandbox.razor
- Components/CorrelationTraceMap.razor
- Components/EventDebugger.razor
- Components/EventDetailPanel.razor
- Components/ProjectionDetailPanel.razor
- Components/ProjectionFilterBar.razor
- Components/RelatedTypeList.razor
- Components/StateInspectorModal.razor
- Components/StateDiffViewer.razor
- Components/StorageTreemap.razor
- Components/StreamFilterBar.razor
- Components/StreamTimelineGrid.razor
- Components/TimelineFilterBar.razor
- Components/TypeDetailPanel.razor
- Components/Shared/IssueBanner.razor

Pages:
- Pages/Backups.razor
- Pages/Commands.razor
- Pages/Compaction.razor
- Pages/Consistency.razor
- Pages/DaprActors.razor
- Pages/DaprComponents.razor
- Pages/DaprHealthHistory.razor
- Pages/DaprPubSub.razor
- Pages/DaprResiliency.razor
- Pages/DeadLetters.razor
- Pages/Events.razor
- Pages/Health.razor
- Pages/Projections.razor
- Pages/Snapshots.razor
- Pages/Storage.razor
- Pages/StreamDetail.razor
- Pages/Streams.razor
- Pages/Tenants.razor
- Pages/TypeCatalog.razor

**Modified test files (1):**
- tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs

**NOT modified (correctly scoped out):**
- Components/Shared/CommandPipeline.razor (FluentBadge context — Story 21-4)
- Components/Shared/HeaderStatusIndicator.razor (FluentBadge context — Story 21-4)
- Components/Shared/EmptyState.razor (FluentAnchor context — Story 21-4)
- Pages/Index.razor (FluentAnchor context — Story 21-4)
- Components/App.razor (already migrated in Story 21-2)
- Components/ThemeToggle.razor (already migrated in Story 21-2)

### Review Findings

- [x] [Review][Patch] Stale comment: "accent" → "Primary" in ConsistencyPageTests.cs:301 — fixed
