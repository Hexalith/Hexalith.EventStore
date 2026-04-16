# Sprint Change Proposal: Epic 21 Post-Boot Fixes

**Date:** 2026-04-16
**Triggered by:** Story 21-9 browser session + code review final pass
**Project:** Hexalith.EventStore
**Epic:** 21 (Fluent UI Blazor v4 → v5 Migration)
**Scope:** Minor — Direct Adjustment (add 3 stories to Epic 21)

**Party Review Patches Applied (2026-04-16):**
- **[Bob] Build gate AC added to 21-11.** Every Epic 21 story since 21-3 has had `dotnet build` = 0 errors. Added to 21-11 and 21-13.
- **[Bob] 21-13 scope cap on Ctrl+K.** 2-hour investigation cap; beyond → spin off `21-13b-commandpalette-shortcut-fix`.
- **[Winston] 21-12 restructured as spike-first.** ACs no longer prescribe FluentDesignTheme behavior. Task 0 = MCP investigation + spike. ACs written as outcomes, not implementation.
- **[Winston] 21-12 CSS architecture change flagged.** `@media (prefers-color-scheme)` → class-based selector risk included in spike scope.
- **[Amelia] "Consult MCP" moved from ACs to Task 0.** ACs assert outcomes; tasks describe process.
- **[Amelia] 21-13 Ctrl+K AC is outcome-based.** Root cause investigation in tasks, AC asserts "Ctrl+K works repeatedly."
- **[Amelia] 21-13 TypeCatalog fix: `_lastTab` guard alternative noted.** `NavigateTo` with `replace: true` may still retrigger `OnParametersSet`.
- **[Murat] Test gates added to all 3 stories.** Tier 1 753/753 + Admin.UI.Tests as gates.
- **[Murat] Visual verification ACs added to all 3 stories.** No more deferred screenshots.
- **[Sally] 21-11 responsive viewport AC added.** NavMenu must work on both wide and narrow viewports.
- **[Sally] 21-12 System theme mode question flagged.** Spike must determine if FluentDesignTheme supports 3-state (Light/Dark/System) or 2-state toggle.

**Advanced Elicitation Patches Applied (2026-04-16, 5-method sweep):**
- **[Pre-mortem F1] 21-12 spike GO/NO-GO exit.** If spike reveals restructure larger than one story, spike itself files a new Sprint Change Proposal. Explicit NO-GO exit criteria added to Task 0 point 6.
- **[Pre-mortem F2] 21-11 breadcrumb preservation AC.** Story 15-8 wired breadcrumbs into MainLayout. Restructuring for FluentNav v5 could break them. AC 7 added.
- **[Pre-mortem F3] 21-13 TypeCatalog browser-back-button test.** `_lastTab` guard may prevent legitimate URL-driven tab restoration. AC 3 expanded.
- **[Pre-mortem F4] Test gate "all pass (0 failures)" replaces fixed counts.** 611 count is fragile — could drift from main merges. All 3 stories now use count-independent gate.
- **[Pre-mortem F5] 21-12 hexalith-status AC references `app.css` hex values.** No working light-theme screenshots exist to compare against. Source of truth is the CSS file, not screenshots.
- **[Pre-mortem F6] Ctrl+K scope-cap spin-off must be filed formally.** Spin-off recorded in sprint-status.yaml + epics.md, not just Completion Notes.
- **[Self-Consistency C1] 21-11 scope expanded to include viewport-related files.** AC 6 may touch `MainLayout.razor.css` or viewport JS beyond the declared scope.
- **[Self-Consistency C2] 21-12 AC 5 "document limitation" target specified.** Goes to Completion Notes AND deferred-work.md.
- **[Self-Consistency C3] 21-13 AC 1 OR clause split.** Separate AC for Ctrl+K investigation outcome vs. behavioral fix.
- **[Self-Consistency C4] Slnx gate quantified.** "≤ 36 errors (all 21-10 Sample.BlazorUI scope)" replaces ambiguous "no new errors."
- **[Self-Consistency C5] Task 0.5 test grep added to all stories.** Each story greps `tests/` for affected component references before code edits.
- **[First Principles P1] 21-11 → 21-12 explicit dependency.** Both modify MainLayout.razor. 21-12 MUST start from 21-11's completed state. Not independent.
- **[First Principles P2] 21-13 Ctrl+K: verify after 21-12.** FluentDesignTheme DOM restructure may fix Ctrl+K as a side effect. Check before investigating.
- **[First Principles P3] 21-12 re-captures Tier-A visual baseline.** 21-9's 36 screenshots were taken with broken sidebar + broken theme. 21-12 replaces them as Epic 21's visual baseline.
- **[Red Team R1] 21-12 spike hard gate.** Spike findings MUST be in Completion Notes BEFORE code edits. Code review rejects if absent.
- **[Red Team R2] FluentLabel → FluentText: check label-input association.** `<FluentLabel>` renders `<label>`; `<FluentText>` renders `<span>`. If `CommandSandbox.razor:200` has a `For=`/`AssociatedId=` binding, the change breaks accessibility.
- **[Red Team R3] "No v4 references" grep narrowed.** Single-line comment fix doesn't need a codebase-wide grep. AC scoped to the specific file.
- **[Lessons L1] No visual verification deferral.** Each story completes its own screenshots before `review`.
- **[Lessons L2] Admin.UI.Tests compile check pre AND post code edits.** Task 0.5 on all stories.
- **[Lessons L3] MCP guides necessary but not sufficient.** Task 0 includes grep for related v4 patterns beyond migration guide.
- **[Lessons L4] ACs requiring data/auth note prerequisites.** Cold-start environment blocks most runtime verification.
- **[Lessons L5] Story specs lean.** Proposal is the design document; story specs focus on ACs and tasks.

---

## Section 1: Issue Summary

During Story 21-9's browser session (2026-04-16), Admin.UI booted successfully under Fluent UI Blazor v5 for the **first time since Epic 21 started**. This first-boot exposed 4 pre-existing runtime/visual bugs that were invisible while the project couldn't compile. The 21-9 code review also surfaced 3 deferred findings (pre-existing v5 migration gaps in files untouched by 21-9).

**These issues are all v5-migration consequences** — they existed before 21-9 but were masked by the 82-error compile wall that 21-9 resolved. Epic 21's goal ("Upgrade all Blazor UI projects from Fluent UI Blazor v4.14.0 to v5.0.0") cannot be considered complete with a broken sidebar, broken theme toggle, and broken command palette.

### Issues Inventory

| # | Issue | Severity | v5-caused? | Source |
|---|-------|----------|------------|--------|
| 1 | **NavMenu unstyled/mispositioned** — `FluentNav`/`FluentNavItem` render as raw hyperlink text, no padding/spacing/hover states | High | Yes — FluentNav web component restructured in v5 | 21-9 Completion Notes §Pre-existing bugs #4 |
| 2 | **Theme toggle broken** — `setColorScheme('light')` sets CSS `color-scheme` but v5 web components don't respond; requires `<FluentDesignTheme>` | High | Yes — v5 removed FAST web component theming | 21-9 Completion Notes §Pre-existing bugs #3; also deferred from 21-2 review |
| 3 | **Ctrl+K CommandPalette re-open broken** — opens once, then Escape kills the JS `registerShortcuts` listener permanently | Medium | Possibly — v5 dialog lifecycle may affect JS interop cleanup | 21-9 Completion Notes §Pre-existing bugs #1 |
| 4 | **TypeCatalog redirect loop** — `/types?tab=aggregates` triggers `OnTabChanged` → `UpdateUrl()` → `NavigateTo` → `OnParametersSet` cycle | Medium | No — logic bug in NavigateTo/OnParametersSet interaction | 21-9 Completion Notes §Pre-existing bugs #2 |
| 5 | **`FluentLabel Typo=` removed in v5** — `CommandSandbox.razor:200` uses removed property | Low | Yes — `Typo` moved to `FluentText` in v5 | 21-9 code review Finding #7 |
| 6 | **Stale "Fluent UI v4" comment** — `AdminUIServiceExtensions.cs:27` says v4, package is v5 | Low | No — cosmetic | 21-9 code review Finding #8 |
| 7 | **`FluentDialog aria-label` splatting** — 3 files use HTML `aria-label` on v5 `FluentDialog`; needs ARIA verification | Low | Possibly — v5 rewrote dialog DOM structure | 21-9 code review Finding #9 |

---

## Section 2: Impact Analysis

### Epic Impact

**Epic 21** is the only affected epic. All other epics (1–20) are `done`.

Current Epic 21 state: 13 stories (21-0 through 21-10, plus 21-9.5 and 21-9.5.7) — all `done`. Only `epic-21-retrospective: optional` remains.

**Proposed:** Add 3 new stories (21-11, 21-12, 21-13) before the retrospective. Epic stays `in-progress` until all 3 are `done`.

### Story Impact

No existing stories are modified. The 3 new stories are additive. **21-11 and 21-12 share a dependency on `MainLayout.razor`** — 21-12 MUST start from 21-11's completed state [P1]. 21-13 is fully independent.

### Artifact Conflicts

- **PRD:** No conflict. Implementation-level fixes within existing scope.
- **Architecture:** Potential minor conflict — 21-12 may require switching `app.css` from `@media (prefers-color-scheme)` to class-based selectors if FluentDesignTheme controls theming via CSS classes rather than media queries. This is a CSS architecture change scoped to Admin.UI only [Winston review finding].
- **UI/UX:** Minor — Admin.UI layout behavior changes with NavMenu fix and theme system replacement. Theme toggle may change from 3-state (Light/Dark/System) to 2-state depending on FluentDesignTheme capabilities [Sally review finding]. No wireframe changes.
- **Epics document:** Needs update — add stories 21-11, 21-12, 21-13 to Epic 21's story list.

### Technical Impact

- **NavMenu (21-11):** Requires investigating FluentNav v5 API changes via MCP migration guide (Task 0), likely CSS/component restructure in `NavMenu.razor` and `MainLayout.razor`. Must verify responsive behavior on wide and narrow viewports. Must preserve breadcrumbs (Story 15-8) [F2].
- **Theme (21-12):** **Spike-first story** [Winston/Amelia review]. Task 0 investigates FluentDesignTheme via MCP before ACs are finalized. May require replacing JS-based `setColorScheme()`, restructuring `ThemeState.cs`, and converting `@media (prefers-color-scheme)` CSS blocks to class-based selectors. Affects `App.razor`, `MainLayout.razor`, `ThemeToggle.razor`, `ThemeState.cs`, `wwwroot/js/hexalith-admin.js`, `wwwroot/css/app.css`. **Depends on 21-11 completing first** (shared MainLayout.razor) [P1].
- **Bug fixes (21-13):** Localized fixes. Ctrl+K has a 2-hour investigation cap — if complex, spins off to `21-13b` (filed formally in sprint-status.yaml + epics.md) [F6]. **Ctrl+K must be verified after 21-12 lands** — FluentDesignTheme's DOM restructure may fix it as a side effect [P2]. TypeCatalog fix must also verify browser back-button behavior [F3].

---

## Section 3: Recommended Approach

**Option 1: Direct Adjustment** — Add 3 stories to Epic 21 before retrospective.

### Rationale

- The issues are well-scoped with an explicit dependency chain (21-11 → 21-12 → 21-13).
- No rollback needed — all completed work is valid.
- MVP is not affected — these are fixes within the existing v5 migration goal.
- Epic 21's migration gate ("Complete Epic 21 before starting any new UI stories") means these must land before any post-migration UI work.
- Effort: Medium overall (NavMenu and Theme are non-trivial; bug fixes are small).
- Risk: Low for 21-11 and 21-13. Medium for 21-12 (CSS architecture change risk, mitigated by spike-first approach with GO/NO-GO exit [F1]).

### Story Grouping Rationale

| Story | Issues | Why grouped | Dependencies |
|-------|--------|-------------|-------------|
| **21-11** | #1 (NavMenu) | Standalone — affects layout component, needs FluentNav v5 MCP investigation | None |
| **21-12** | #2 (Theme toggle) | Standalone — spike-first, may require FluentDesignTheme integration + CSS architecture change | **Depends on 21-11** (shared MainLayout.razor) [P1] |
| **21-13** | #3 (Ctrl+K), #4 (TypeCatalog), #5 (FluentLabel), #6 (stale comment), #7 (aria-label) | Batch of small fixes — Ctrl+K has 2-hour scope cap | Ctrl+K: verify after 21-12 [P2]; rest: independent |

### Epic 21 Lessons Applied [L1–L5]

These lessons from 13 prior stories inform the story design:

1. **No visual verification deferral** [L1] — Each story completes its own screenshots before `review`. No "deferred to next story" chains.
2. **Pre/post test compile check** [L2] — Task 0.5 runs Admin.UI.Tests compile before code edits. Catches cascade surprises early instead of at the final gate.
3. **MCP + grep** [L3] — Task 0 consults MCP migration guides AND greps for related v4 patterns beyond what the guide lists. Migration guides are necessary but not sufficient.
4. **Cold-start prerequisites** [L4] — ACs that require populated data or Keycloak auth must explicitly note the prerequisite. Don't write unverifiable ACs.
5. **Lean story specs** [L5] — This proposal is the design document. Story specs focus on ACs, tasks, and Dev Notes — they don't repeat the proposal's context.

---

## Section 4: Detailed Change Proposals

### Epic 21 — Epics Document Update

```
OLD (epics.md line 638):
**Stories (11):** 21-0 bUnit baseline, 21-1 packages/csproj, 21-2 layout+navigation, 21-3 ButtonAppearance, 21-4 BadgeAppearance+LinkAppearance, 21-5 component renames, 21-6 dialog restructure, 21-7 toast API, 21-8 CSS tokens, 21-9 DataGrid/remaining, 21-10 Sample alignment

NEW:
**Stories (14):** 21-0 bUnit baseline, 21-1 packages/csproj, 21-2 layout+navigation, 21-3 ButtonAppearance, 21-4 BadgeAppearance+LinkAppearance, 21-5 component renames, 21-6 dialog restructure, 21-7 toast API, 21-8 CSS tokens, 21-9 DataGrid/remaining, 21-10 Sample alignment, 21-11 NavMenu v5 fix, 21-12 FluentDesignTheme integration, 21-13 UI bug fixes batch
```

### Story 21-11: NavMenu v5 Navigation Fix

As a developer completing the Fluent UI Blazor v5 migration,
I want the sidebar navigation (`NavMenu.razor`) to render correctly under v5's restructured `FluentNav`/`FluentNavItem` web components,
So that Admin.UI has functional, styled navigation with proper padding, spacing, hover states, and vertical layout.

**Context:** FluentNav web component was restructured in v5. The current `<FluentNav>`/`<FluentNavItem>`/`<FluentNavCategory>` markup renders as raw hyperlink text with no padding, no vertical spacing, no hover/active states, no background. Only the Topology `<FluentNavCategory>` dropdown is partially styled. Visible on ALL pages (sidebar is in MainLayout).

**Scope:** `NavMenu.razor`, `MainLayout.razor`, potentially `MainLayout.razor.css`, `app.css`, or viewport-related JS if v5 navigation requires adjustments.

**Task 0 (mandatory before code edits):** Consult Fluent UI MCP `FluentNav`/`FluentNavMenu` migration guide via `mcp__fluent-ui-blazor__get_component_migration`. Record v5 API changes and size the fix (rename-only vs. restructure). Check if `FluentNavGroup` → `FluentNavCategory` rename has additional structural requirements. Also grep for related v4 patterns not covered by the migration guide [L3].

**Task 0.5 (mandatory before code edits):** Run `grep -rn "NavMenu\|FluentNav" tests/` to identify any bUnit tests that render NavMenu or FluentNav components. Record count. If any exist, they are in-scope for this story [C5, L2]. Run Admin.UI.Tests compile-only pass — record pre-edit error count.

**Acceptance Criteria:**
1. NavMenu renders with proper vertical layout, padding, and spacing.
2. Nav items show hover and active states.
3. Current page indicator is visible.
4. FluentNavCategory expandable groups work correctly.
5. Icons render alongside labels.
6. NavMenu renders correctly on both wide and narrow viewports (responsive behavior preserved or adapted to v5).
7. Breadcrumbs (Story 15-8) continue to render correctly on all pages with breadcrumb trails. MainLayout restructuring does not break breadcrumb positioning or CSS context [F2].
8. `dotnet build src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj --configuration Release` → 0 errors, 0 warnings.
9. `dotnet build Hexalith.EventStore.slnx --configuration Release` → ≤ 36 errors (all 21-10 Sample.BlazorUI scope) [C4].
10. Tier 1 non-UI tests: 753/753 green.
11. Admin.UI.Tests: all pass (0 failures). Count-independent gate [F4].
12. Visual verification: screenshot of sidebar in both themes (or dark-only if theme toggle still broken pre-21-12), saved to `_bmad-output/test-artifacts/21-11-navmenu/`. Must show styled, functional navigation. No deferral [L1].

### Story 21-12: FluentDesignTheme Integration (Theme Toggle Fix)

**Dependency: Story 21-11 MUST be `done` before 21-12 begins.** Both stories modify `MainLayout.razor`. 21-12 builds on 21-11's completed layout state [P1].

As a developer completing the Fluent UI Blazor v5 migration,
I want the dark/light theme toggle to work correctly with Fluent UI v5 web components,
So that all v5 components respond to theme changes and the Admin.UI provides a consistent themed experience.

**Context:** The current `ThemeToggle.razor` calls `hexalithAdmin.setColorScheme('light'/'dark')` which sets `color-scheme` CSS property on `<html>`, but Fluent UI v5 web components (which are real web components, not just CSS) do NOT respond to it. This was already deferred from the 21-2 code review.

**Scope:** `App.razor`, `MainLayout.razor`, `ThemeToggle.razor`, `ThemeState.cs`, `wwwroot/js/hexalith-admin.js`, `wwwroot/css/app.css`.

**SPIKE-FIRST STORY** [Winston/Amelia party review]: Task 0 is a mandatory investigation before any code edits. ACs are written as outcomes — the specific implementation approach is determined by the spike, not prescribed up front.

**Task 0 — Spike (mandatory, 30 min cap):**
1. Consult Fluent UI MCP `FluentDesignTheme` documentation via `mcp__fluent-ui-blazor__get_component_details` and `mcp__fluent-ui-blazor__get_documentation_topic`.
2. Determine: Does `FluentDesignTheme` support 3-state mode (Light/Dark/System) or 2-state only? Document finding.
3. Determine: Does `FluentDesignTheme` manage its own localStorage persistence, or does the app need to handle it? Document finding.
4. Determine: Will `@media (prefers-color-scheme)` CSS blocks (including `--hexalith-status-*` tokens on `app.css:5-10` and `:29-34`) still work, or must they be converted to class-based selectors? Document finding.
5. Determine: Can `ThemeState.cs` be removed/simplified, or does it still serve a purpose alongside FluentDesignTheme? Document finding.
6. **GO/NO-GO decision** [F1]: If spike reveals the fix requires App.razor render pipeline restructuring, SSR changes, or effort exceeding a single story — STOP. File a new Sprint Change Proposal to split the work. Do NOT proceed to code edits. Document the NO-GO rationale in Completion Notes.
7. If GO: write final implementation tasks based on spike findings.

**Spike hard gate** [R1]: Task 0 spike findings MUST be recorded in Completion Notes BEFORE any code edits begin. Code review will reject the story if spike findings are absent.

**Task 0.5 (mandatory before code edits):** Run `grep -rn "ThemeToggle\|ThemeState\|FluentDesignTheme\|setColorScheme" tests/` to identify any bUnit tests that reference theme components. Record count [C5, L2]. Run Admin.UI.Tests compile-only pass — record pre-edit error count. If FluentDesignTheme cascades parameters, existing tests may need a provider wrapper — flag during spike.

**Acceptance Criteria (outcome-based):**
1. Theme toggle changes the visual theme for ALL Fluent UI v5 web components (buttons, inputs, dialogs, badges, nav, data grids).
2. The `--hexalith-status-*` project-owned tokens render correctly in both themes, matching the hex values defined in `app.css:5-10` (light: `#1A7F37` success, `#0969DA` inflight, `#9A6700` warning, `#CF222E` error, `#656D76` neutral) and `:29-34` (dark: `#2EA043`, `#58A6FF`, `#D29922`, `#F85149`, `#8B949E`). Reference the CSS source of truth, not screenshots [F5].
3. Theme preference persists across page reloads (mechanism determined by spike — localStorage, FluentDesignTheme built-in, or other).
4. The JS-based `setColorScheme()` approach is removed or replaced with the v5 mechanism.
5. If FluentDesignTheme supports System mode (follow OS preference): implement 3-state toggle (Light/Dark/System). If not: implement 2-state toggle (Light/Dark) and document the limitation in Completion Notes AND `deferred-work.md` [C2].
6. `dotnet build src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj --configuration Release` → 0 errors, 0 warnings.
7. `dotnet build Hexalith.EventStore.slnx --configuration Release` → ≤ 36 errors (all 21-10 Sample.BlazorUI scope) [C4].
8. Tier 1 non-UI tests: 753/753 green.
9. Admin.UI.Tests: all pass (0 failures) [F4].
10. Visual verification: screenshot of the same page in BOTH light AND dark themes (minimum 2 pages), saved to `_bmad-output/test-artifacts/21-12-theme/`. Both themes MUST show correct Fluent UI v5 component styling. No deferral [L1].
11. **Epic 21 visual baseline refresh** [P3]: Re-capture the Tier-A 12-page screenshots in both themes (light + dark = 24 captures). Save to `_bmad-output/test-artifacts/21-12-theme/tier-a/<theme>/<page>.png`. These replace the 21-9 artifacts (which were captured with broken sidebar + broken theme) as the Epic 21 visual baseline.

### Story 21-13: UI Bug Fixes Batch (Post-Boot Cleanup)

As a developer completing the Fluent UI Blazor v5 migration,
I want the remaining small bugs and v5 migration gaps fixed,
So that Admin.UI is fully functional and clean before Epic 21's retrospective.

**Scope:** 5 independent fixes in one story. Ctrl+K has a 2-hour investigation cap.

**Task 0 — Pre-flight:**
1. **Ctrl+K: verify after 21-12** [P2]. Before investigating the Ctrl+K bug, confirm it still reproduces on the 21-12-completed codebase. FluentDesignTheme's DOM restructure may fix it as a side effect. If fixed, document "resolved-by-21-12" and skip Fix #1.
2. Run `grep -rn "CommandPalette\|TypeCatalog\|CommandSandbox\|FluentLabel.*Typo" tests/` to identify bUnit tests referencing affected components [C5, L2]. Run Admin.UI.Tests compile-only pass — record pre-edit error count.
3. Check `CommandSandbox.razor:200` context: does the `<FluentLabel Typo=...>` have a `For=` or `AssociatedId=` attribute binding it to a form input? If yes, `FluentText` is NOT the correct replacement — preserve the label-input association using v5's labeling approach [R2]. If it's purely decorative typography text, `FluentText` is correct.

**Fixes:**
1. **Ctrl+K CommandPalette re-open** (`CommandPalette.razor`) — Opens once, Escape dismisses, Ctrl+K no longer triggers. Root cause unknown — investigate first (JS listener lifecycle, v5 dialog CloseMode, focus management). **Scope cap: 2-hour investigation. If root cause is not found or fix exceeds 2 hours, spin off `21-13b-commandpalette-shortcut-fix` — file it in sprint-status.yaml as `backlog` AND add to epics.md story list before closing 21-13** [F6].
2. **TypeCatalog redirect loop** (`TypeCatalog.razor`) — `OnTabChanged` → `UpdateUrl()` → `NavigateTo(url, replace: true)` retriggers `OnParametersSet` → reads `?tab=` → cycle. Fix options: (a) guard `NavigateTo` to skip when URL matches current, or (b) add `_lastTab` field in `OnParametersSet` to detect no-op re-entries [Amelia review].
3. **`FluentLabel Typo=` migration** (`CommandSandbox.razor:200`) — v5 removed `Typo` from `FluentLabel`. Replace with `<FluentText Typo="Typography.PaneHeader">` ONLY if the label has no form-input association [R2]. If it does, use v5's labeling approach instead.
4. **Stale "Fluent UI v4" comment** (`AdminUIServiceExtensions.cs:27`) — Update comment to say "v5".
5. **`FluentDialog aria-label` verification** (`CommandPalette.razor:4`, `CommandSandbox.razor:197`, `EventDebugger.razor:261`) — Verify HTML `aria-label` on `<FluentDialog>` reaches correct DOM element in v5 runtime. If not, switch to v5's accessible naming approach. **Prerequisite: requires running Admin.UI instance (aspire run + browser)** [L4].

**Acceptance Criteria:**
1. **Ctrl+K investigation outcome:** Root cause identified and documented in Completion Notes (JS listener lifecycle, v5 dialog CloseMode, focus trap, or other). If resolved-by-21-12, document that [P2] [C3].
2. **Ctrl+K behavioral fix (conditional):** If root cause found within 2-hour cap: Ctrl+K opens the command palette, Escape closes it, Ctrl+K re-opens it — repeatedly. If scope cap exceeded: `21-13b` filed in sprint-status.yaml + epics.md [F6] [C3].
3. `/types?tab=aggregates` loads without redirect loop; tab switching works in both directions (Events↔Commands↔Aggregates). **Browser back button restores the previous tab selection correctly** [F3].
4. `grep -rn "FluentLabel.*Typo" src/Hexalith.EventStore.Admin.UI` returns 0 hits.
5. Comment on `AdminUIServiceExtensions.cs:27` updated from "v4" to "v5" [R3].
6. `FluentDialog aria-label` verified on CommandPalette, CommandSandbox, and EventDebugger in browser DevTools (requires `aspire run`). ARIA label present on the dialog DOM element. **If cold-start environment prevents testing** (no data to trigger dialog), document the verification approach for a future browser session [L4].
7. `dotnet build src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj --configuration Release` → 0 errors, 0 warnings.
8. `dotnet build Hexalith.EventStore.slnx --configuration Release` → ≤ 36 errors (all 21-10 Sample.BlazorUI scope) [C4].
9. Tier 1 non-UI tests: 753/753 green.
10. Admin.UI.Tests: all pass (0 failures) [F4].
11. Visual verification: screenshot of TypeCatalog tab switching (all 3 tabs) + screenshot of CommandPalette Ctrl+K open/close cycle (if fix landed), saved to `_bmad-output/test-artifacts/21-13-bugfixes/`. No deferral [L1].

---

## Section 5: Implementation Handoff

**Change scope: Minor** — Direct implementation by development team.

| Role | Responsibility |
|------|---------------|
| **Scrum Master (Bob)** | Update epics.md story list, add 21-11/12/13 entries to sprint-status.yaml |
| **Developer (Amelia)** | Create story files via `create-story`, implement via `dev-story` |
| **QA (Quinn)** | Verify fixes in browser session after each story lands |

**Sequencing — STRICT ORDER [P1]:**
1. **21-11 (NavMenu)** — First. No dependencies. Most visible fix.
2. **21-12 (Theme)** — Second. **DEPENDS on 21-11** (shared MainLayout.razor). Spike-first with GO/NO-GO.
3. **21-13 (Bug fixes)** — Third. Ctrl+K must be verified after 21-12 lands [P2]. Rest is independent.

**Success criteria:** After all 3 stories land:
- Admin.UI sidebar navigation is fully styled and functional on all viewports.
- Breadcrumbs preserved from Story 15-8.
- Theme toggle works end-to-end with v5 web components in both (or all three) modes.
- `--hexalith-status-*` tokens match `app.css` hex values in both themes.
- Tier-A 12-page visual baseline refreshed in both themes (replaces 21-9 artifacts).
- Ctrl+K, TypeCatalog tabs, FluentLabel, and dialog ARIA all work correctly (or Ctrl+K deferred to 21-13b with formal sprint tracking).
- All test gates green: Tier 1 753/753, Admin.UI.Tests all pass (0 failures).
- Visual verification screenshots captured for all 3 stories — no deferrals.
- Epic 21 retrospective can proceed.

---

## Approval

**Status:** APPROVED (2026-04-16, Jerome)
