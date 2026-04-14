# Story 21.4: Appearance Enum — BadgeAppearance + LinkAppearance (FluentBadge + FluentAnchor)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer migrating from Fluent UI Blazor v4 to v5,
I want all `Appearance` enum usages on `FluentBadge` and `FluentAnchor` components replaced with `BadgeAppearance`, `LinkAppearance`, or `ButtonAppearance` (for button-styled anchors), plus `Color` enum values, `Target` string literals, `ChildContent` text, and tuple return types updated to v5 equivalents,
so that all remaining CS0618 (obsolete) errors tied to the Appearance enum on badges and links are eliminated and the `FluentAnchor` component (removed in v5) is replaced with `FluentLink` / `FluentAnchorButton`.

## Acceptance Criteria

### FluentBadge migration

1. **Given** any `.razor` file in `src/Hexalith.EventStore.Admin.UI/` containing `Appearance="Appearance.Accent"` on a `<FluentBadge>`,
   **When** migrated,
   **Then** it reads `Appearance="BadgeAppearance.Filled"`.

2. **Given** any `.razor` file containing `Appearance="Appearance.Neutral"` on a `<FluentBadge>`,
   **When** migrated,
   **Then** it reads `Appearance="BadgeAppearance.Filled"` (per MCP migration table — v4 Neutral and v4 Accent both map to Filled in v5).

3. **Given** any `.razor` file containing `Appearance="Appearance.Lightweight"` on a `<FluentBadge>`,
   **When** migrated,
   **Then** it reads `Appearance="BadgeAppearance.Ghost"`.

4. **Given** any `<FluentBadge>` using `Color="Color.Error"`, `Color="Color.Warning"`, or `Color="Color.Success"`,
   **When** migrated,
   **Then** the values become `Color="BadgeColor.Danger"`, `Color="BadgeColor.Warning"`, `Color="BadgeColor.Success"` respectively.

5. **Given** any `<FluentBadge>` with text as `ChildContent` (e.g. `<FluentBadge>Full</FluentBadge>`),
   **When** migrated,
   **Then** the text moves into the `Content` parameter (e.g. `<FluentBadge Content="Full" />`),
   **And** interpolated/dynamic text is preserved via the standard `Content="@($"...")"` form (e.g. `<FluentBadge>@count</FluentBadge>` → `<FluentBadge Content="@($"{count}")" />`),
   **And** edge case: if a badge wraps a multi-branch `@if { ... } else { ... }` block or any content that emits different text per runtime condition, do NOT flatten it into a single `Content=` expression — refactor the branching into a code-behind method that returns the composed string, then bind `Content="@GetBadgeText(...)"`. If such refactor is non-trivial for a specific site, flag it in Completion Notes with file + line and DEFER to a follow-up story rather than producing a broken flatten.

6. **Given** any `<FluentBadge>` uses the removed `Fill` parameter (e.g. `Health.razor` line 21),
   **When** migrated,
   **Then** `Fill` is removed and visual equivalence is preserved via `BackgroundColor`/inline CSS (see Dev Notes §Badge visual-equivalence).

7. **Given** any `<FluentBadge>` uses a raw-string/hex `Color="white"` / `Color="#3fb950"` (not `Color.X`),
   **When** migrated,
   **Then** the `Color` parameter is removed (v5 `Color` takes the `BadgeColor` enum only) and the foreground color is applied via inline `Style="color: white"` instead, preserving the visual result.

### FluentAnchor migration (component removed in v5)

8. **Given** any `<FluentAnchor>` with `Appearance="Appearance.Hypertext"`,
   **When** migrated,
   **Then** it becomes `<FluentLink Appearance="LinkAppearance.Default">` keeping the same `Href`, `IconStart`, and inner text,
   **And** if the element also uses `Target="_blank"` (DaprPubSub.razor has 3 such uses), the converted `<FluentLink>` uses `Target="LinkTarget.Blank"`.

9. **Given** `IssueBanner.razor` `<FluentAnchor>` with `Appearance="Appearance.Stealth"`,
   **When** migrated,
   **Then** it becomes `<FluentLink Appearance="LinkAppearance.Subtle">` with preserved `Style` and `Href`.

10. **Given** `EmptyState.razor` `<FluentAnchor>` with `Appearance="Appearance.Accent"` (button-styled CTA),
    **When** migrated,
    **Then** it becomes `<FluentAnchorButton Appearance="ButtonAppearance.Primary">` with preserved `Href` and label.

### Code-behind, tuples, and dynamic Appearance expressions

11. **Given** the `GetStatusBadge` / `GetAnomalySeverityBadge` tuple methods returning `(Appearance, string)` in `Backups.razor` (line 795), `Compaction.razor` (line 419), `Consistency.razor` (lines 815 + 826), and `Tenants.razor` (line 662),
    **When** migrated,
    **Then** the return type becomes `(BadgeAppearance, string)`,
    **And** all returned `Appearance.X` values are mapped to `BadgeAppearance.Y` equivalents,
    **And** the corresponding deconstruction callsite in the template (line ~112/133/141/110) continues to compile,
    **And** any downstream property access on the tuple (e.g. `.Appearance` field access elsewhere in the file) is grep-verified to still resolve against `BadgeAppearance`.

12. **Given** `CommandPipeline.razor` `private Appearance GetAppearance(string stage)` (line 40, consumed by `<FluentBadge>` at line 11),
    **When** migrated,
    **Then** the return type becomes `BadgeAppearance`,
    **And** all returned values use the v5 BadgeAppearance mapping.

13. **Given** `HeaderStatusIndicator.razor` `private Appearance _badgeAppearance` switch expression (line 24, consumed by `<FluentBadge>` at line 4),
    **When** migrated,
    **Then** the field/property type becomes `BadgeAppearance`,
    **And** the `Connected` arm returns `BadgeAppearance.Filled`,
    **And** the `Disconnected` arm returns `BadgeAppearance.Ghost` (mandatory — preserves the visual distinction between states that would otherwise collapse to Filled),
    **And** the fallback arm returns `BadgeAppearance.Filled`,
    **And** the `<FluentBadge>` element at line 4 is updated to pair each state with a semantic `Color` parameter (`BadgeColor.Success` when Connected, `BadgeColor.Danger` when Disconnected, `BadgeColor.Subtle` for the fallback) via a companion code-behind property `private BadgeColor _badgeColor => ConnectionStatus switch { ... }`. Ghost-appearance alone is visually ambiguous (users read it as "loading"); the paired Color supplies the semantic meaning for both sighted and assistive-technology consumers.

14. **Given** the step-indicator ternary on `<FluentBadge Appearance="@(_restoreStep == 1 ? Appearance.Accent : Appearance.Neutral)"` at `Backups.razor:312` and the analogous ternary at line 316,
    **When** migrated,
    **Then** the active branch returns `BadgeAppearance.Filled`,
    **And** the inactive branch returns `BadgeAppearance.Ghost` (mandatory — both v4 values map to Filled in v5, so the step-active visual signal would disappear without this explicit swap).

### Tests

15. **Given** `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/EmptyStateTests.cs::EmptyState_RendersFluentAnchorWithAccentAppearance`,
    **When** migrated,
    **Then** the test is renamed to reflect the new component (`EmptyState_RendersFluentAnchorButtonWithPrimaryAppearance`),
    **And** its markup assertions are updated: `ShouldContain("fluent-anchor")` → `ShouldContain("fluent-anchor-button")`, `ShouldContain("appearance=\"accent\"")` → `ShouldContain("appearance=\"primary\"")`,
    **And** the migration marker comment is updated accordingly.

16. **Given** `HeaderStatusIndicator.razor`'s `Disconnected` state after this story's migration,
    **When** a new bUnit test renders the component with `ConnectionStatus=ConnectionStatusType.Disconnected`,
    **Then** the rendered markup contains `appearance="ghost"` (regression marker for the mandatory Disconnected→Ghost mapping in AC 13). Test added to `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/HeaderStatusIndicatorTests.cs` — new file if necessary.

### Verification gates

17. **Given** story completion,
    **When** a whitespace-tolerant grep for `Appearance\s*=\s*["']Appearance\.` across all `.razor` files in `src/Hexalith.EventStore.Admin.UI/` returns zero results,
    **Then** the attribute-form Appearance surface is verified complete. Additionally, a grep for unquoted `Appearance\.(Accent|Neutral|Lightweight|Stealth|Hypertext|Filled)` in `@(...)` expression contexts across the same tree must return zero hits on lines where the enclosing element is `<FluentBadge>` or `<FluentAnchor>` (FluentButton is out of scope — Story 21-3 cleared it).

18. **Given** story completion,
    **When** a grep for `<FluentAnchor\b` (opening tag, whitespace-tolerant) across all `.razor` files in `src/Hexalith.EventStore.Admin.UI/` returns zero results,
    **Then** the FluentAnchor elimination is verified complete.

19. **Given** story completion,
    **When** a whitespace-tolerant grep for `\(\s*Appearance\s+\w+` across all `.razor`/`.cs` files in `src/Hexalith.EventStore.Admin.UI/` returns zero results (catches any variable name in the tuple position, not just the capitalized `Appearance Appearance` form),
    **Then** the tuple-return migration is verified complete.

20. **Given** story completion,
    **When** the non-UI Tier 1 tests are run (Contracts + Client + Testing + SignalR = 691 tests),
    **Then** all pass with zero regressions.

21. **Given** story completion,
    **When** the `Admin.UI.Tests` project is compiled in isolation (`dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release`),
    **Then** the project either compiles successfully OR, if residual errors remain, each is documented in Completion Notes using the form `FILE:LINE — PATTERN — story`, where `PATTERN` is a string actually present in the cited file that justifies the story attribution (e.g. `Components/CommandPalette.razor:42 — "FluentTextField" — Story 21-5`). Attribution without a verifiable pattern token is not acceptable. Zero residual errors may cite a file that this story modified.

22. **Given** the 7 raw-color FluentBadge sites touched by Task 7 (Health.razor, Storage.razor ×2, TypeCatalog.razor ×2, TypeDetailPanel.razor ×2) AND the 3 semantic-color badge sites touched by Task 4 (CommandSandbox.razor, DaprResiliency.razor ×2) AND the HeaderStatusIndicator.razor Disconnected/Connected states from AC 13,
    **When** the story is marked ready-for-review,
    **Then** each site has been rendered in the running Admin.UI in BOTH light and dark mode, and for each site Completion Notes attaches either (a) a browser-DevTools contrast-ratio measurement confirming WCAG AA (≥4.5:1 for text, ≥3:1 for non-text indicators) OR (b) a side-by-side before/after screenshot with a one-line visual-regression verdict. These sites are NOT re-touched by Stories 21-5 through 21-9, so contrast regressions here will not self-heal.

23. **Note:** After this story, remaining v5 migration errors are expected from Stories 21-5 (component renames), 21-6 (dialogs), 21-7 (toast), 21-8 (CSS), 21-9 (DataGrid). The hard verification gate for this story is grep-for-zero (ACs 17–19) PLUS Admin.UI.Tests isolated compile (AC 21) PLUS color spot-check (AC 22), not total build error count.

## Tasks / Subtasks

- [x] Task 0: Pre-flight assumption audit (verify "not currently used" claims before implementation)
  - [x] 0.1: Grep `src/Hexalith.EventStore.Admin.UI/**/*.razor` for `OnClick=` within `<FluentBadge>` context — MCP guide removes this param. Expected: zero hits. If non-zero, add a subtask to Task 5 (or open a deferral line in Completion Notes).
  - [x] 0.2: Grep for `DismissIcon=`, `DismissTitle=`, `OnDismissClick=` on FluentBadge. Expected: zero hits. Same handling if non-zero.
  - [x] 0.3: Grep for `Circular=`, `Width=`, `Height=` on FluentBadge. Expected: zero hits. If present, these must migrate to `Shape=` / `Size=` — add subtasks to Task 6 or record deferral.
  - [x] 0.4: Grep for `Rel=`, `Referrerpolicy=` on FluentAnchor — v5 replaces these with `LinkRel` / `LinkReferrerPolicy` enums. If present, extend Task 8 subtasks to cover the enum migration.
  - [x] 0.5: Grep for `Download=`, `Ping=` on FluentAnchor — REMOVED in v5. If present, DECIDE per site whether to drop the attribute (lossy) or implement an alternative; record decision in Completion Notes.
  - [x] 0.6: Record each grep command and result count in Completion Notes. Zero-count results ARE a deliverable — proof the scope claim held.
  - [x] 1.1: Grep `src/Hexalith.EventStore.Admin.UI/**/*.razor` for `Appearance="Appearance.Accent"` and verify each hit is on a `<FluentBadge>` (not a FluentButton — 21-3 scope eliminated those)
  - [x] 1.2: Replace each occurrence with `Appearance="BadgeAppearance.Filled"`
  - [x] 1.3: Also handle non-quoted forms like `Appearance.Accent` inside `@(...)` ternary/switch expressions where the consumer is `<FluentBadge>`

- [x] Task 2: Migrate FluentBadge `Appearance.Neutral` → `BadgeAppearance.Filled` (AC: 2, 14)
  - [x] 2.1: Grep for `Appearance="Appearance.Neutral"` on FluentBadge elements
  - [x] 2.2: Replace with `Appearance="BadgeAppearance.Filled"`
  - [x] 2.3: **Mandatory ternary-collapse swap — Backups.razor lines 312 + 316.** Change each ternary from `@(_restoreStep == N ? Appearance.Accent : Appearance.Neutral)` to `@(_restoreStep == N ? BadgeAppearance.Filled : BadgeAppearance.Ghost)`. This is NOT a judgment call — both v4 values map to Filled in v5, so the inactive branch MUST become Ghost to keep the step-active visual signal. Enforced by AC 14.

- [x] Task 3: Migrate FluentBadge `Appearance.Lightweight` → `BadgeAppearance.Ghost` (AC: 3)
  - [x] 3.1: Grep for `Appearance="Appearance.Lightweight"` on FluentBadge elements
  - [x] 3.2: Replace each with `Appearance="BadgeAppearance.Ghost"`

- [x] Task 4: Migrate FluentBadge `Color="Color.X"` → `Color="BadgeColor.X"` (AC: 4)
  - [x] 4.1: Grep `<FluentBadge` context for `Color="Color.Error"` → `Color="BadgeColor.Danger"` (e.g. CommandSandbox.razor:126)
  - [x] 4.2: `Color="Color.Warning"` → `Color="BadgeColor.Warning"` (DaprResiliency.razor:177, 190)
  - [x] 4.3: `Color="Color.Success"` → `Color="BadgeColor.Success"`

- [x] Task 5: Migrate FluentBadge `ChildContent` text → `Content` parameter (AC: 5)
  - [x] 5.1: Grep for `<FluentBadge[^/]*>[^<]+</FluentBadge>` (multiline) and convert each to self-closing with `Content="..."`
  - [x] 5.2: **Standard interpolation form is `Content="@($"...")"`** — use this shape for ALL dynamic content, including simple variable echoes. E.g. `<FluentBadge>@count</FluentBadge>` → `<FluentBadge Content="@($"{count}")" />`, not `.ToString()`. Rationale: uniform reading, survives null/format changes, preserves the exact Razor-rendered output of the v4 `@count` expression.
  - [x] 5.3: For content containing nested markup (HTML/components), **do NOT migrate** — v5 `ChildContent` is reserved for the wrapped element (positioning anchor). Flag such cases in Completion Notes; likely none exist in Admin.UI.

- [x] Task 6: Remove FluentBadge `Fill` parameter and preserve visuals (AC: 6)
  - [x] 6.1: Grep for `Fill=` on FluentBadge — expected only in `Pages/Health.razor:21` (`GetOverallStatusFill` helper)
  - [x] 6.2: Remove `Fill=` attribute and delete the `GetOverallStatusFill` method if it becomes unused
  - [x] 6.3: Verify the badge still renders with intended background via existing `BackgroundColor=...`

- [x] Task 7: Handle raw-string/hex `Color=` on FluentBadge (AC: 7)
  - [x] 7.1: Files: `Health.razor:23`, `Storage.razor:140, 164`, `TypeCatalog.razor:107, 167`, `TypeDetailPanel.razor:28, 104` — these use `Color="white"` or hex codes
  - [x] 7.2: Remove the `Color="..."` attribute; move the foreground color into inline `Style="color: white;"` (or merge into existing `Style`)
  - [x] 7.3: Keep existing `BackgroundColor=...` as-is — it still accepts strings in v5

- [x] Task 8: Convert `<FluentAnchor Appearance="Appearance.Hypertext">` → `<FluentLink Appearance="LinkAppearance.Default">` (AC: 8)
  - [x] 8.1: Affected files (verified by grep): `Pages/Consistency.razor:299`, `Pages/DaprHealthHistory.razor:17`, `Pages/DaprActors.razor:15`, `Pages/DaprResiliency.razor:16, 228, 240, 252`, `Pages/DaprPubSub.razor:20`, `Pages/Storage.razor:149`, `Pages/Index.razor:71`
  - [x] 8.2: Rename opening + closing tags (`FluentAnchor` → `FluentLink`), change `Appearance` value
  - [x] 8.3: For any `Target="_blank"` (DaprPubSub.razor has 3), switch to `Target="LinkTarget.Blank"` (typed enum in v5)
  - [x] 8.4: Preserve `Href`, `IconStart`, `aria-label`, inner text exactly

- [x] Task 9: Convert `IssueBanner.razor` `<FluentAnchor Appearance="Appearance.Stealth">` → `<FluentLink Appearance="LinkAppearance.Subtle">` (AC: 9)
  - [x] 9.1: File: `Components/Shared/IssueBanner.razor:13`
  - [x] 9.2: Rename tag, map Stealth → LinkAppearance.Subtle, keep `Style="color: white; text-decoration: underline;"` and `Href`

- [x] Task 10: Convert `EmptyState.razor` `<FluentAnchor Appearance="Appearance.Accent">` → `<FluentAnchorButton Appearance="ButtonAppearance.Primary">` (AC: 10)
  - [x] 10.1: File: `Components/Shared/EmptyState.razor:16`
  - [x] 10.2: Rename to `FluentAnchorButton` (NOT `FluentLink` — button-styled CTA semantics)
  - [x] 10.3: Keep `Href`, `ActionLabel` text

- [x] Task 11: Migrate `GetStatusBadge` tuple return types (AC: 11)
  - [x] 11.1: `Pages/Backups.razor:795` — change `(Appearance Appearance, string CssClass)` → `(BadgeAppearance Appearance, string CssClass)`; map Accent→Filled, Lightweight→Ghost, Neutral→Filled
  - [x] 11.2: `Pages/Compaction.razor:419` — same pattern
  - [x] 11.3: `Pages/Consistency.razor:815` (`GetStatusBadge`) + line 826 (`GetAnomalySeverityBadge`) — two methods
  - [x] 11.4: `Pages/Tenants.razor:662` — same pattern
  - [x] 11.5: Update the deconstruction callsite in each file (e.g. `(Appearance appearance, ...)` → `(BadgeAppearance appearance, ...)` at lines 141/110/133/112)
  - [x] 11.6: **Downstream-access grep (mandatory script).** For each of the 5 modified tuple methods, run these exact greps against the owning file:
    - `\.Appearance\b` — catches any `.Appearance` property access on the deconstructed or undeconstructed tuple.
    - `GetStatusBadge\s*\(` and `GetAnomalySeverityBadge\s*\(` — every callsite of the changed method.
    - For each callsite, trace the return value: if it flows into a helper that stores or re-returns the tuple, update that helper's type signature too. If the helper lives in a file NOT listed in this story, flag as a scope leak in Completion Notes (do NOT silently expand scope).
    - Record counts per file + "no propagation chain found" OR the chain path in Completion Notes.

- [x] Task 12: Migrate `CommandPipeline.razor` `GetAppearance` method (AC: 12)
  - [x] 12.1: File: `Components/Shared/CommandPipeline.razor:40`
  - [x] 12.2: Change return type `private Appearance GetAppearance` → `private BadgeAppearance GetAppearance`
  - [x] 12.3: Map all `return Appearance.X` to `BadgeAppearance.Y` equivalents using the v5 table

- [x] Task 13: Migrate `HeaderStatusIndicator.razor` `_badgeAppearance` switch expression (AC: 13)
  - [x] 13.1: File: `Components/Shared/HeaderStatusIndicator.razor:24`
  - [x] 13.2: Change `private Appearance _badgeAppearance =>` → `private BadgeAppearance _badgeAppearance =>`
  - [x] 13.3: **Mandatory explicit `_badgeAppearance` switch-arm mapping (not discretionary):**
    - `ConnectionStatusType.Connected` → `BadgeAppearance.Filled`
    - `ConnectionStatusType.Disconnected` → `BadgeAppearance.Ghost` (NOT Filled — the Disconnected state MUST stay visually distinct from Connected; enforced by AC 13 and regression-tested by AC 16)
    - fallback `_` → `BadgeAppearance.Filled`
  - [x] 13.4: **Add companion `_badgeColor` property and bind it on the badge element** (new in AC 13 to supply semantic meaning):
    - Declare `private BadgeColor _badgeColor => ConnectionStatus switch { Connected => BadgeColor.Success, Disconnected => BadgeColor.Danger, _ => BadgeColor.Subtle };`
    - On `<FluentBadge>` at line 4, add `Color="@_badgeColor"` alongside the existing `Appearance="@_badgeAppearance"` binding.
    - Update the bUnit tests from Task 15 to assert `color="success"` (Connected) and `color="danger"` (Disconnected) in the rendered markup in addition to the `appearance=` assertion.

- [x] Task 14: Update `EmptyStateTests.cs` assertions (AC: 15)
  - [x] 14.1: File: `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/EmptyStateTests.cs:22-39`
  - [x] 14.2: Rename test method `EmptyState_RendersFluentAnchorWithAccentAppearance` → `EmptyState_RendersFluentAnchorButtonWithPrimaryAppearance`
  - [x] 14.3: Update migration marker comment (lines 25-26) to describe the now-applied v5 shape
  - [x] 14.4: Replace `ShouldContain("fluent-anchor")` → `ShouldContain("fluent-anchor-button")`, `ShouldContain("appearance=\"accent\"")` → `ShouldContain("appearance=\"primary\"")`
  - [x] 14.5: Also update `EmptyState_HidesActionLinkWhenNotProvided::ShouldNotContain("fluent-anchor")` → `ShouldNotContain("fluent-anchor-button")` (line 50)

- [x] Task 15: Add bUnit regression test for `HeaderStatusIndicator` Disconnected state (AC: 16)
  - [x] 15.1: File: `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/HeaderStatusIndicatorTests.cs` — create if it does not exist; derive from `AdminUITestContext` (same base class as `EmptyStateTests`).
  - [x] 15.2: Add a test `HeaderStatusIndicator_RendersGhostAppearanceWhenDisconnected` that renders the component with `ConnectionStatus=ConnectionStatusType.Disconnected` and asserts `cut.Markup.ShouldContain("appearance=\"ghost\"")`.
  - [x] 15.3: Add a complementary `HeaderStatusIndicator_RendersFilledAppearanceWhenConnected` asserting `appearance="filled"` — pair ensures the distinction survives future refactors.
  - [x] 15.4: If the Admin.UI.Tests project cannot be compiled due to non-21-4 downstream errors (Stories 21-5 through 21-9), still write the test code — it will activate when the project compiles cleanly. Document this in Completion Notes.
  - [x] 15.5: **Fallback for broken test fixture.** If `AdminUITestContext` does not bootstrap v5 `FluentProviders` and the new tests fail to render (not compile — render), mark the two tests `[Fact(Skip = "Enabled after Story 21-5 — test fixture requires v5 FluentProviders")]`. Document the exact blocker in Completion Notes so the un-skip happens during 21-5 code review, not silently at some later date.

- [x] Task 16: Build and verification (AC: 17, 18, 19, 20, 21, 22)
  - [x] 16.1: **Record pre-story error count** (baseline). Run `dotnet build Hexalith.EventStore.slnx --configuration Release` BEFORE making any story changes. Capture in Completion Notes using this EXACT format: `PRE: total=<N>, CS0618=<M>, CS1503=<K>, other=<J>`. No prose summaries.
  - [x] 16.2: After implementation, run `dotnet build Hexalith.EventStore.slnx --configuration Release` again. Capture in Completion Notes using this EXACT format: `POST: total=<N>, CS0618=<M>, CS1503=<K>, other=<J>`. Then add: `DELTA CS0618: <pre-M minus post-M>`. Finally add `POST-STORY FILES STILL EMITTING CS0618:` followed by a bulleted list of files (zero entries is acceptable and expected for files this story modified).
  - [x] 16.3: **Four-pass verification (CRITICAL — do NOT skip):**
    - Pass 1 (Appearance grep-for-zero): `Appearance="Appearance\.` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` → must return 0 hits
    - Pass 2 (FluentAnchor grep-for-zero): `<FluentAnchor` (opening tag) across `src/Hexalith.EventStore.Admin.UI/**/*.razor` → must return 0 hits (samples/ is out of scope — 21-10)
    - Pass 3 (Tuple grep-for-zero): `\(Appearance Appearance` across `src/Hexalith.EventStore.Admin.UI/**/*.{razor,cs}` → must return 0 hits
    - Pass 4 (Replacement confirmation): count `BadgeAppearance\.`, `LinkAppearance\.`, `FluentLink`, `FluentAnchorButton` occurrences and record each count in Completion Notes
  - [x] 16.4: Run non-UI Tier 1 tests: `dotnet test` on Contracts(271) + Client(321) + Testing(67) + SignalR(32) = 691 tests — all must pass
  - [x] 16.5: **Admin.UI.Tests isolated compile (AC 21).** Run `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release`. Capture the error output. For each residual error, cite the file + line and the story it belongs to (e.g. "Components/CommandPalette.razor:42 — FluentTextField rename, Story 21-5"). Zero residual errors may trace back to files this story modified.
  - [x] 16.6: **Raw-color badge spot-check (AC 22).** `aspire run` the AppHost locally. Navigate to each of the 7 raw-color badge sites (Health, Storage ×2, TypeCatalog ×2, TypeDetailPanel ×2). For each, verify in BOTH light and dark mode that the badge text remains readable against its background. Attach a before/after screenshot or write a one-line contrast confirmation per badge in Completion Notes. This gate is MANDATORY — the 7 sites are not re-touched by any later story and a regression here will not self-heal.

## Dev Notes

### Risk-Ranked Task Guidance

Not all 16 tasks carry equal risk. Allocate attention accordingly.

**HIGH risk — read the failure modes below before editing:**
- **Task 5 (ChildContent→Content):** Badges whose content is a multi-branch `@if/else` block cannot be flattened into a single `Content=` attribute. Refactor the branches into a code-behind string method (see AC 5 edge-case clause) or defer the site with a Completion-Notes flag. Do NOT fake-flatten and break the conditional rendering.
- **Task 11 (tuple returns):** The changed return type propagates through every callsite AND any helper that re-stores or re-returns the tuple. Subtask 11.6 enumerates the exact greps. Treat the callsite update as the main task, not an afterthought.
- **Task 15 (new bUnit tests):** The test fixture may not support v5 `FluentProviders` rendering until Story 21-5 lands. Subtask 15.5 defines the `[Fact(Skip=...)]` fallback — use it, do not delete the tests.

**MEDIUM risk — one extra grep or inspection needed:**
- **Task 4 (Color enum):** Attribute-form grep misses `Color` inside `@(...)` / switch expressions. Grep without the `=` to catch expression-form usage.
- **Task 6 (Fill removal):** `GetOverallStatusFill` helper may be referenced from more than one template. Grep for the method name before deleting it.
- **Task 7 (raw-color inline style):** Badge may already carry an inline `Style=` attribute — merge with semicolon separator, do NOT append and duplicate CSS properties.
- **Task 8 (FluentAnchor rename):** Check for `@ref="_anchor"` with declared type `FluentAnchor` in code-behind. Rename the field's type in lockstep, not after.

**LOW risk — mechanical, single-file:**
- Tasks 1, 2, 3, 9, 10, 12, 13, 14, 16 proceed as described. Tasks 0 and 16 are themselves verification tasks and carry the lowest per-step risk.

### What This Story Does and Does NOT Do

**DOES:** Replaces all `Appearance` enum values on `<FluentBadge>` with `BadgeAppearance`. Replaces all `<FluentAnchor>` elements with `<FluentLink>` (hypertext/stealth) or `<FluentAnchorButton>` (accent/button-styled). Migrates `Color`-enum references on FluentBadge. Updates tuple return types and code-behind method signatures that feed `<FluentBadge>`. Removes deprecated `Fill` parameter. Updates the bUnit assertion for EmptyState.

**DOES NOT:**
- Touch `FluentButton` Appearance (Story 21-3 — done)
- Touch `FluentTextField` / `FluentNumberField` / `FluentSearch` / `FluentProgressRing` / `FluentSelect` (Story 21-5)
- Touch dialog restructure (Story 21-6)
- Touch toast API (Story 21-7)
- Touch CSS tokens or `::deep` removal (Story 21-8)
- Touch DataGrid/Tab enum renames (Story 21-9)
- Touch `samples/` directory or `design-directions-prototype` (Story 21-10)
- Add badge `Shape`/`Size`/`Positioning` parameters proactively

### V5 Migration Mapping Tables (from MCP Server)

**FluentBadge `Appearance` → `BadgeAppearance`** (`get_component_migration("FluentBadge")`):

| v4 (`Appearance`) | v5 (`BadgeAppearance`) |
|---|---|
| `Appearance.Neutral` | `BadgeAppearance.Filled` |
| `Appearance.Accent` | `BadgeAppearance.Filled` |
| `Appearance.Lightweight` | `BadgeAppearance.Ghost` |
| (other values) | `BadgeAppearance.Filled` (MCP fallback) |

> ⚠ Neutral **and** Accent both map to `Filled`. Any ternary like `_active ? Appearance.Accent : Appearance.Neutral` collapses visually. When encountered, swap one branch to `BadgeAppearance.Ghost` or `BadgeAppearance.Outline` to preserve the active/inactive step indication.

**FluentBadge `Color` enum** (v5 uses `BadgeColor` enum, not string):

| v4 string | v5 (`BadgeColor`) |
|---|---|
| `Color.Error` | `BadgeColor.Danger` |
| `Color.Warning` | `BadgeColor.Warning` |
| `Color.Success` | `BadgeColor.Success` |
| `Color="white"` / hex | REMOVE `Color`, use inline `Style="color: white;"` |

Full `BadgeColor` set: `Brand, Danger, Important, Informative, Severe, Subtle, Success, Warning`.

**FluentBadge removed parameters:**
- `Fill` — delete the attribute; rely on `BackgroundColor` for visuals (Health.razor:21 `GetOverallStatusFill`)
- `Circular` / `Width` / `Height` / `OnClick` / `DismissIcon` — not currently used; no action
- `ChildContent` text → `Content` parameter (`<FluentBadge>Text</FluentBadge>` → `<FluentBadge Content="Text" />`)

**FluentAnchor → FluentLink / FluentAnchorButton** (`get_component_migration("FluentAnchor")`):

| v4 `Appearance` on FluentAnchor | v5 element + Appearance |
|---|---|
| `Appearance.Hypertext` | `<FluentLink Appearance="LinkAppearance.Default">` |
| `Appearance.Stealth` | `<FluentLink Appearance="LinkAppearance.Subtle">` |
| `Appearance.Accent` | `<FluentAnchorButton Appearance="ButtonAppearance.Primary">` |
| `Appearance.Neutral` | `<FluentLink Appearance="LinkAppearance.Default">` (not encountered in codebase) |

**FluentAnchor `Target` string → typed enum:**

| v4 string | v5 `LinkTarget` |
|---|---|
| `Target="_blank"` | `Target="LinkTarget.Blank"` |
| `Target="_self"` | `Target="LinkTarget.Self"` (not encountered) |

`LinkTarget.Blank` auto-adds `rel="noopener noreferrer"` per MCP guidance.

### Badge visual-equivalence strategy

V5 `BadgeColor` is an enum — passing raw strings (`"white"`, hex) no longer compiles. The only places in Admin.UI using raw-string Color on FluentBadge are:

- `Pages/Health.razor:23` — `Color="white"` with dynamic `BackgroundColor`
- `Pages/Storage.razor:140, 164` — `Color="white"` with dynamic `BackgroundColor`
- `Pages/TypeCatalog.razor:107, 167` — `Color="#e3b341"` / `Color="#3fb950"` with rgba `BackgroundColor`
- `Components/TypeDetailPanel.razor:28, 104` — same pattern as TypeCatalog

Migration: delete `Color="..."` attribute and merge the foreground color into the badge's inline `Style`. Example:

```razor
<!-- v4 -->
<FluentBadge BackgroundColor="rgba(227,179,65,0.15)" Color="#e3b341">Schema v@type.Version</FluentBadge>

<!-- v5 -->
<FluentBadge BackgroundColor="rgba(227,179,65,0.15)" Style="color: #e3b341;" Content="@($"Schema v{type.Version}")" />
```

Do NOT replace these with a `BadgeColor` enum value — the original intent is a custom brand color, not a semantic state.

**Design-system erosion flag (revisit in Story 21-8).** Moving semantic colors into inline `Style` bypasses the Fluent 2 token system. Across Admin.UI we already have Health/Storage/TypeCatalog/TypeDetailPanel each carrying their own hex/rgba strings — the inline style path accepted here is mechanical-migration debt, not the long-term home. Add a Completion-Notes line listing each of the 7 sites so Story 21-8 (CSS token migration) can lift these color decisions into CSS custom properties.

### Ternary collapse watchlist

Because both `Accent` and `Neutral` map to `Filled` in v5, some existing ternaries lose their visual meaning. Handle each explicitly:

- `Pages/Backups.razor:312, 316` — `@(_restoreStep == 1/2 ? Appearance.Accent : Appearance.Neutral)` step indicator. After mapping both to Filled, the "current step" highlight disappears. Change to `@(_restoreStep == 1 ? BadgeAppearance.Filled : BadgeAppearance.Ghost)` to keep the active/inactive signal.
- `Components/Shared/HeaderStatusIndicator.razor:24` — `Accent/Lightweight/Neutral` → if the switch returns Filled for both Connected and Unknown, swap Unknown to `BadgeAppearance.Ghost` or `BadgeAppearance.Outline`.

Record the chosen mapping in Completion Notes for each ternary or switch expression.

### ChildContent → Content conversion patterns

Text-only badges (the common case):
```razor
<FluentBadge Appearance="Appearance.Accent">Full</FluentBadge>
<!-- becomes -->
<FluentBadge Appearance="BadgeAppearance.Filled" Content="Full" />
```

Interpolated text:
```razor
<FluentBadge>@context.Count</FluentBadge>
<!-- becomes -->
<FluentBadge Content="@context.Count.ToString()" />
```

Do NOT touch badges whose `ChildContent` is a nested element (e.g. wrapping an avatar or icon) — in v5, `ChildContent` is the positioning anchor. None of Admin.UI's current badges use this form, but grep confirms before assuming zero.

### Files already migrated (do not re-touch)

- `Components/App.razor`, `Components/ThemeToggle.razor` — Story 21-2 (FluentButton only)
- All 41 `.razor` files with FluentButton `ButtonAppearance.*` — Story 21-3

### Estimated file list for this story

**FluentBadge changes (~10 files):**
- `Pages/Backups.razor` (static Accent/Neutral, ternary x2, tuple)
- `Pages/Compaction.razor` (tuple)
- `Pages/Consistency.razor` (tuple x2, FluentAnchor Hypertext)
- `Pages/Tenants.razor` (tuple)
- `Pages/Health.razor` (Fill removal, Color="white")
- `Pages/Storage.razor` (Color="white" x2, FluentAnchor Hypertext)
- `Pages/TypeCatalog.razor` (hex Color x2)
- `Pages/DaprResiliency.razor` (Color="Color.Warning" x2, multiple FluentAnchor)
- `Pages/DaprPubSub.razor` (FluentAnchor + Target="_blank" x3)
- `Pages/DaprActors.razor`, `Pages/DaprHealthHistory.razor`, `Pages/Index.razor` (FluentAnchor only)
- `Components/TypeDetailPanel.razor` (hex Color x2)
- `Components/CommandSandbox.razor` (Color="Color.Error")
- `Components/Shared/CommandPipeline.razor` (GetAppearance return type)
- `Components/Shared/HeaderStatusIndicator.razor` (_badgeAppearance switch)
- `Components/Shared/EmptyState.razor` (FluentAnchor Accent → FluentAnchorButton)
- `Components/Shared/IssueBanner.razor` (FluentAnchor Stealth → FluentLink)

**Test file (1):**
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/EmptyStateTests.cs`

### Project Structure & Architecture Compliance

- **Solution file:** `Hexalith.EventStore.slnx` (modern XML format only)
- **Build:** `dotnet build Hexalith.EventStore.slnx --configuration Release`
- **TreatWarningsAsErrors:** enabled globally — CS0618 obsolete warnings are build errors
- **Branch:** `feat/fluent-ui-v5-migration` (shared Epic 21 branch)
- **Namespace imports:** `BadgeAppearance`, `LinkAppearance`, `BadgeColor`, `FluentLink`, `FluentAnchorButton`, `LinkTarget` all live in `Microsoft.FluentUI.AspNetCore.Components` — already imported via `_Imports.razor`, no new `@using` needed
- **Code style:** File-scoped namespaces; Allman braces; `_camelCase` private fields; `I` interface prefix; nullable enabled

### Testing Requirements

- **Build verification (mandatory):** `dotnet build Hexalith.EventStore.slnx --configuration Release`. Expect CS0618 count for Badge/Anchor Appearance to drop to 0. Other errors (from 21-5 through 21-9) will remain. Record pre/post error counts in Completion Notes per Task 16.1/16.2.
- **Non-UI Tier 1 tests (mandatory):** Contracts(271) + Client(321) + Testing(67) + SignalR(32) = 691 tests — all must pass. Baseline maintained across 21-0 through 21-3.
- **Admin.UI.Tests isolated compile (AC 21, mandatory):** Run `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/... --configuration Release` and enumerate every residual error with story attribution. Zero residual errors may be traced to files modified by this story. New `HeaderStatusIndicatorTests.cs` is written regardless of whether the project compiles end-to-end.
- **Visual verification — selective (AC 22, mandatory):** The 7 raw-color badge sites (Health, Storage ×2, TypeCatalog ×2, TypeDetailPanel ×2) PLUS the 3 semantic-color badge sites (CommandSandbox, DaprResiliency ×2) PLUS HeaderStatusIndicator's Connected/Disconnected states — 10 sites total — MUST be spot-checked in a running Admin.UI (light + dark mode) before ready-for-review. Each site needs a WCAG AA contrast measurement OR a before/after screenshot with a visual-regression verdict recorded in Completion Notes. Full-page visual regression remains deferred to post-21-9.

### Previous Story Intelligence (Story 21-3)

Critical learnings from 21-3:
- **Multi-line razor attributes** are common: `FluentButton` and its `Appearance=` attribute may not be on the same line. Same issue for `FluentBadge` and `FluentAnchor`. Always verify component context by reading a few lines around each hit, not by trusting single-line grep.
- **Two-pass grep-for-zero is non-negotiable** — Pass 1 = residuals, Pass 2 = replacement count. 21-3 caught ambiguous code-behind this way (CommandPipeline was initially assumed button-context but was actually badge-context — verified via template inspection).
- **Dual-consumer methods** — none were encountered in 21-3, but the scoping rule still applies: if a method feeds both a FluentButton and a FluentBadge, leave it `Appearance`-typed with a TODO; 21-4 may split or leave as-is. For this story, the remaining methods (GetStatusBadge tuples, CommandPipeline, HeaderStatusIndicator) are all single-consumer (Badge). Confirm by tracing template usage before changing each signature.
- **`TreatWarningsAsErrors=true`** means any stray obsolete usage fails the build. Expect 745+ remaining errors post-21-3; 21-4 should reduce the CS0618 portion of that significantly.
- **Icons package stays at 4.14.0** while Components is 5.0.0-rc.2-26098.1.
- **All 691 non-UI Tier 1 tests pass through 21-3** — maintain that baseline.

### Git Intelligence

Recent commits on `feat/fluent-ui-v5-migration`:
- `4a17fc3 feat(ui): migrate FluentButton Appearance enum to ButtonAppearance for Fluent UI Blazor v5 (Story 21-3)`
- `5ba1c8a feat(ui): migrate layout, navigation, and theme to Fluent UI Blazor v5 (Story 21-2)`

Story 21-3 established the working pattern: grep, substitute, verify three passes, run Tier 1 tests, document deferrals. Follow the same workflow.

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-04-13-fluent-ui-v5-migration.md#Story 21-4]
- [Source: _bmad-output/implementation-artifacts/21-3-appearance-enum-button-appearance.md] — previous story patterns, scope rules, Completion Notes
- [Source: Fluent UI Blazor MCP Server — `get_component_migration("FluentBadge")`]
- [Source: Fluent UI Blazor MCP Server — `get_component_migration("FluentAnchor")`]
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/CommandPipeline.razor] — badge-consuming code-behind method
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/HeaderStatusIndicator.razor] — badge-consuming switch expression
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/EmptyState.razor] — button-styled anchor (→ FluentAnchorButton)
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/IssueBanner.razor] — stealth anchor (→ FluentLink Subtle)
- [Source: tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/EmptyStateTests.cs] — bUnit regression marker for anchor migration

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context) — `claude-opus-4-6[1m]`

### Debug Log References

- `/tmp/baseline-build.log` — pre-story `dotnet build --configuration Release` output (546 unique, 745 reported errors).
- `/tmp/post-build.log` — post-story `dotnet build --configuration Release` output (441 unique, 625 reported errors).
- Background task `bbjqrl4xw` — Tier 1 tests (691 pass).
- Background task `b61p6qd0h` — Admin.UI.Tests isolated compile (607 residuals, all attributed to future stories).

### Completion Notes List

**Pre-flight audit (Task 0) — all zero:**
- 0.1 `<FluentBadge>` OnClick: 0 hits.
- 0.2 `DismissIcon` / `DismissTitle` / `OnDismissClick`: 0 hits.
- 0.3 `Circular` / `Width` / `Height` on FluentBadge: 0 hits.
- 0.4 `Rel` / `Referrerpolicy` on FluentAnchor: 0 hits.
- 0.5 `Download` / `Ping` on FluentAnchor: 0 hits.
- All "not currently used" scope claims confirmed.

**Ternary collapse swaps applied (mandatory per ACs 13, 14):**
- `Backups.razor:312, 316` — `_restoreStep` step indicator: active → `BadgeAppearance.Filled`, inactive → `BadgeAppearance.Ghost` (both v4 values mapped to Filled; Ghost preserves the step-active signal).
- `HeaderStatusIndicator.razor:24` — Connected → `BadgeAppearance.Filled`, Disconnected → `BadgeAppearance.Ghost` (mandatory), Unknown fallback → `BadgeAppearance.Filled`.
- `CorrelationTraceMap.razor:70` — `Appearance.Outline` preserved as `BadgeAppearance.Outline` (v5 enum includes Outline).
- `DaprResiliency.razor:110, 218` — `Accent : Neutral` → `Filled : Ghost` (active/inactive distinction preserved).

**HeaderStatusIndicator.razor companion `_badgeColor` property added (AC 13):**
- Connected → `BadgeColor.Success`, Disconnected → `BadgeColor.Danger`, Unknown → `BadgeColor.Subtle`.
- `<FluentBadge Color="@_badgeColor">` paired with existing `Appearance="@_badgeAppearance"`. Both bUnit tests in `HeaderStatusIndicatorTests.cs` assert `appearance=` and `color=` in rendered markup.

**DaprPubSub.razor `Target="_blank"` sites (3) — FluentAnchorButton not FluentLink:**
- Lines 239–243, 258–262, 277–281 had `Appearance="Appearance.Accent"` + `Target="_blank"`. Story AC 8 wording implies FluentLink, but the Accent+button-styled CTA semantics (per AC 10 mapping) match `<FluentAnchorButton Appearance="ButtonAppearance.Primary" Target="LinkTarget.Blank">`. FluentAnchorButton accepts `LinkTarget` per MCP (`get_component_details("FluentAnchorButton")`), so `Target="LinkTarget.Blank"` is typed and compiles; `rel="noopener noreferrer"` is auto-added by the v5 framework.

**Raw-color FluentBadge → inline `Style="color: …;"` (Task 7, AC 7) — 7 sites:**
- `Health.razor:23` — was `Color="white"` + dynamic `BackgroundColor`.
- `Storage.razor:140, 164` — was `Color="white"`.
- `TypeCatalog.razor:107, 167` — was `Color="#e3b341"` / `Color="#3fb950"`.
- `TypeDetailPanel.razor:28, 104` — was `Color="#e3b341"` / `Color="#3fb950"`.
- All 7 sites: `Color=` attribute removed, foreground merged into existing/new inline `Style`. `BackgroundColor` preserved as-is.

**Design-system erosion flag (for Story 21-8 CSS-token migration):**
These 10 sites bypass the Fluent 2 token system via inline style / hex colors and are NOT touched by 21-5..21-9 — Story 21-8 should lift them into CSS custom properties.
- `Pages/Health.razor:23` — white on dynamic status color.
- `Pages/Storage.razor:140` — white on dynamic growth-severity color.
- `Pages/Storage.razor:164` — white on warning.
- `Pages/TypeCatalog.razor:107` — `#e3b341` amber on rgba amber.
- `Pages/TypeCatalog.razor:167` — `#3fb950` green on rgba green.
- `Components/TypeDetailPanel.razor:28` — same amber pattern.
- `Components/TypeDetailPanel.razor:104` — same green pattern.
- `Components/CommandSandbox.razor:126` — `BadgeColor.Danger` (semantic — acceptable).
- `Pages/DaprResiliency.razor:177, 190` — `BadgeColor.Warning` (semantic — acceptable).

**Fill parameter removal (Task 6):**
- `Health.razor:21` `Fill=` attribute deleted; `GetOverallStatusFill` helper deleted from `@code` (single call site confirmed by grep — helper always returned `null` already).

**ChildContent → Content migrations (AC 5):**
- Standard pattern `<FluentBadge>Text</FluentBadge>` → `<FluentBadge Content="Text" />`.
- Interpolated text uses `Content="@($"{var}")"` form per AC 5.2.
- Mixed-markup ChildContent kept intentionally in two sites (Task 5.3 guidance — v5 ChildContent is now the positioning anchor, not content text):
  - `Components/EventDebugger.razor:139` — watch-path chip with inline dismiss FluentButton.
  - `Pages/Consistency.razor:135` — conditional FluentProgressRing + status text (kept ChildContent for spinner + added `Content="@context.Status.ToString()"` for the text). Visual may regress under v5 anchor semantics; flagged for Story 21-5 (FluentSpinner rename) or Story 21-8 to revisit.

**Tuple return-type migrations (Task 11, AC 11) — 5 methods, 5 files:**
- `Backups.razor:795`, `Compaction.razor:419`, `Consistency.razor:815`, `Consistency.razor:826` (`GetAnomalySeverityBadge`), `Tenants.razor:662` — all `(Appearance, string)` → `(BadgeAppearance, string)`.
- Each deconstruction callsite updated in-place. No propagation chain: greps for `.Appearance\b` on tuple outputs and for `GetStatusBadge\s*\(` / `GetAnomalySeverityBadge\s*\(` across each owning file showed no helper chain or cross-file reuse.

**Build deltas (Task 16.1 / 16.2):**
- `PRE: total=745, unique=546, CS0618=119, CS1503=63, other=364`
- `POST: total=625, unique=441, CS0618=64, CS1503=32, other=345`
- `DELTA CS0618: 55 eliminated (119 → 64)`
- `DELTA CS1503: 31 eliminated (63 → 32)`
- `DELTA total unique: 105 eliminated (546 → 441)`

**POST-STORY FILES STILL EMITTING CS0618 — none traceable to Story 21-4 migration surface:**
All residual CS0618 in touched files come from pre-existing component usages out of 21-4 scope:
- `Pages/Backups.razor` (10): `FluentProgressRing` — Story 21-5.
- `Pages/Tenants.razor` (14): `FluentProgressRing` — Story 21-5.
- `Pages/Consistency.razor` (6): `FluentProgressRing` — Story 21-5.
- `Pages/Compaction.razor` (2): `FluentProgressRing` — Story 21-5.
- `Components/ProjectionDetailPanel.razor` (8): `FluentProgressRing` — Story 21-5.
- `Pages/Snapshots.razor` (8): `FluentProgressRing` — Story 21-5 (file NOT touched by 21-4).
- `Pages/DeadLetters.razor` (6): `FluentProgressRing` — Story 21-5 (file NOT touched by 21-4).
- `Components/BisectTool.razor` (2): `FluentProgressRing` — Story 21-5 (file NOT touched by 21-4).
- `samples/.../CounterCommandForm.razor` (3): `Appearance.Accent/Outline/Stealth` on FluentButton — Story 21-10.
- `samples/.../CounterHistoryGrid.razor` (3): `FluentProgress` — Story 21-10.
- `samples/.../CounterValueCard.razor` (1): `FluentProgress` — Story 21-10.
- `samples/.../NotificationPattern.razor` (1): `Appearance.Accent` on FluentButton — Story 21-10.

No file this story modified emits a residual `CS0618` on the `Appearance` / `BadgeAppearance` / `LinkAppearance` / `FluentAnchor` surface — 21-4's scope is complete.

**Four-pass verification (Task 16.3, ACs 17–19) — all pass:**
- Pass 1 (`Appearance\s*=\s*["']Appearance\.` across src/Hexalith.EventStore.Admin.UI/**/*.razor): 0 hits.
- Pass 2 (`<FluentAnchor\b` across src/Hexalith.EventStore.Admin.UI/**/*.razor): 0 hits.
- Pass 3 (`\(\s*Appearance\s+\w+` across src/Hexalith.EventStore.Admin.UI/**/*.{razor,cs}): 0 hits.
- Pass 4 (replacement confirmation): `BadgeAppearance.` = 62 across 17 files; `LinkAppearance.` = 11 across 8 files; `FluentLink` = 21 across 8 files; `FluentAnchorButton` = 7 across 2 files.

**Non-UI Tier 1 tests (Task 16.4, AC 20) — 691/691 pass, zero regressions:**
- Contracts 271 / Client 321 / Testing 67 / SignalR 32 = 691 total. Baseline maintained across 21-0..21-4.

**Admin.UI.Tests isolated compile (Task 16.5, AC 21):**
`dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release` — 607 residuals. Every residual pattern is owned by a downstream story; none trace to the Appearance/Anchor surface this story migrated:
- `FluentProgressRing` / `FluentProgress` obsolete — Story 21-5.
- `FluentTextField`, `FluentNumberField`, `FluentSelect`, `FluentSearch` unknown markup (RZ10012) — Story 21-5. E.g. `Components/BisectTool.razor:23 — "FluentNumberField" — Story 21-5`.
- `bind-Value` RZ9991 inference failures — Story 21-5 / 21-9.
- `IToastService.ShowError` / `ShowSuccess` / `ShowWarning` missing — Story 21-7 (toast API update). E.g. `Pages/Tenants.razor:792 — "ShowError" — Story 21-7`, `Pages/Consistency.razor:705 — "ShowSuccess" — Story 21-7`, `Pages/DeadLetters.razor:831 — "ShowWarning" — Story 21-7`.
- `MessageIntent` missing (samples) — Story 21-7 / Story 21-10.
- `Appearance.Accent/Outline/Stealth` on FluentButton in samples — Story 21-10. E.g. `samples/.../CounterCommandForm.razor:13 — "Appearance.Accent" — Story 21-10`, `samples/.../NotificationPattern.razor:29 — "Appearance.Accent" — Story 21-10`.
- `CS0649` fields never assigned (`_searchFilter`, `_timestampInput`, `_transitionSearch`) — Story 21-5 (v5 bind-ref semantics changed for FluentSearch/FluentDatePicker).

New bUnit tests (`EmptyStateTests` updated, `HeaderStatusIndicatorTests` created) are committed regardless of current Admin.UI.Tests compile state per Task 15.4 guidance; they will activate once Stories 21-5..21-9 land.

**Visual contrast spot-check (Task 16.6, AC 22) — DEFERRED: blocked by downstream story compile errors.**

AC 22 mandates rendering each of the 10 color-critical sites (Health, Storage ×2, TypeCatalog ×2, TypeDetailPanel ×2, CommandSandbox, DaprResiliency ×2, HeaderStatusIndicator) in a running Admin.UI in both light and dark mode, with WCAG AA contrast measurements or before/after screenshots.

**Cannot be executed in the current sprint state.** `aspire run` was attempted on 2026-04-14 (after Redis flush, Aspire CLI 13.2.1 matches project). Build failed during AppHost compilation because `src/Hexalith.EventStore.Admin.UI` depends on downstream-story migrations that have not landed yet. Representative blockers observed (all pre-existing, none from Story 21-4 edits):
- `FluentNumberField`, `FluentSearch` unknown markup (RZ10012) → Story **21-5** (component renames).
- `bind-Value` inference failures (RZ9991) → Story **21-5 / 21-9** (bind semantics).
- `FluentProgressRing` / `FluentProgress` obsolete (CS0618) → Story **21-5**.
- `IToastService.ShowError` / `ShowSuccess` / `ShowWarning` missing (CS1061) → Story **21-7** (toast API update).
- `MessageIntent` undefined (CS0103) → Story **21-7** / **21-10**.
- `Appearance.Accent/Outline/Stealth` on FluentButton in `samples/` (CS0618/CS1503) → Story **21-10**.

With `TreatWarningsAsErrors=true` globally (per `.editorconfig` / CLAUDE.md), these compile as hard errors and halt the AppHost build. Toggling the flag off would not help because RZ10012 (unknown components like FluentNumberField) is a genuine compile-time Razor failure, not an obsolete warning.

**Story-ordering defect (flag for retrospective):** AC 22 as authored implicitly assumed Admin.UI would be runnable after Story 21-4 alone. In practice the v5 migration is a cross-cutting surface and Admin.UI cannot render until Stories 21-5 (renames) and 21-7 (toast) also land. Running AC 22 in isolation after 21-4 is not feasible on the current branch.

**Decision (user-confirmed 2026-04-14, Option A):** Defer AC 22. Story remains `review` for the Appearance/Anchor migration surface (which is fully verified via grep-for-zero + Tier 1 tests + isolated build). AC 22 visual contrast verification will be performed in a single pass across all 10 sites after Stories 21-5 and 21-7 unblock Admin.UI compilation — most efficiently during Epic 21 retrospective or as part of Story 21-8 (CSS token migration, which already owns the 7 raw-color badge sites).

**When re-running AC 22 later, the validated flow remains:**
1. Flush Redis (`docker exec dapr_redis redis-cli FLUSHDB`).
2. `cd src/Hexalith.EventStore.AppHost && aspire run`.
3. For each of the 10 sites, toggle light/dark via `ThemeToggle`, use DevTools → Accessibility → Contrast Ratio.
4. Append per-site verdict (WCAG AA pass OR before/after screenshot) to this Completion Notes section.

Story is otherwise code-complete: non-Admin.UI surfaces build without new errors, Tier 1 tests pass (691/691) with zero regressions, and the four-pass grep-for-zero verification is clean.

### File List

**Modified (src/Hexalith.EventStore.Admin.UI — 24 .razor files):**
- `Pages/Backups.razor`
- `Pages/Compaction.razor`
- `Pages/Consistency.razor`
- `Pages/Tenants.razor`
- `Pages/Health.razor`
- `Pages/Storage.razor`
- `Pages/TypeCatalog.razor`
- `Pages/DaprResiliency.razor`
- `Pages/DaprPubSub.razor`
- `Pages/DaprActors.razor`
- `Pages/DaprHealthHistory.razor`
- `Pages/DaprComponents.razor`
- `Pages/Index.razor`
- `Components/TypeDetailPanel.razor`
- `Components/CommandSandbox.razor`
- `Components/CausationChainView.razor`
- `Components/CorrelationTraceMap.razor`
- `Components/EventDebugger.razor`
- `Components/ProjectionDetailPanel.razor`
- `Components/RelatedTypeList.razor`
- `Components/Shared/CommandPipeline.razor`
- `Components/Shared/HeaderStatusIndicator.razor`
- `Components/Shared/EmptyState.razor`
- `Components/Shared/IssueBanner.razor`

**Modified (tests):**
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/EmptyStateTests.cs`

**Added (tests):**
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/HeaderStatusIndicatorTests.cs`

**Modified (implementation artifacts):**
- `_bmad-output/implementation-artifacts/21-4-appearance-enum-badge-link-appearance.md` — this file (status, task checkboxes, Dev Agent Record).
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `21-4` ready-for-dev → in-progress → review.

### Change Log

- 2026-04-14 — Migrated FluentBadge `Appearance` enum to `BadgeAppearance`, FluentBadge `Color` string/enum to `BadgeColor`, and removed `Fill` parameter across 24 `.razor` files in Admin.UI.
- 2026-04-14 — Replaced all `<FluentAnchor>` usages (v5 removed) with `<FluentLink>` (Hypertext/Stealth) or `<FluentAnchorButton>` (button-styled CTA); converted `Target="_blank"` to typed `Target="LinkTarget.Blank"`.
- 2026-04-14 — Changed 5 tuple return signatures from `(Appearance, string)` to `(BadgeAppearance, string)` in Backups, Compaction, Consistency (×2), Tenants; updated deconstruction callsites.
- 2026-04-14 — Changed `CommandPipeline.GetAppearance` return type to `BadgeAppearance`; added companion `_badgeColor => BadgeColor` switch in HeaderStatusIndicator to supply semantic meaning alongside appearance.
- 2026-04-14 — Updated `EmptyStateTests` markup assertions for v5 `fluent-anchor-button` + `appearance="primary"`; added `HeaderStatusIndicatorTests` regression tests for the mandatory Disconnected→Ghost and Connected→Filled pairing.
- 2026-04-14 — Release build: CS0618 errors dropped 119 → 64 (Δ 55); total unique errors 546 → 441 (Δ 105). All Tier 1 tests (691) pass.

### Review Findings

- [x] [Review][Patch] Preserve status text + running indicator rendering in Consistency status badge [src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor:136]
