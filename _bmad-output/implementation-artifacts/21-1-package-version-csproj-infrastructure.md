# Story 21.1: Package Version + csproj Infrastructure

Status: done

## Story

As a developer preparing for the Fluent UI Blazor v4 -> v5 migration,
I want all Fluent UI package references upgraded to v5 and csproj build infrastructure updated,
so that subsequent migration stories can focus on component-level changes with the correct package version already in place.

## Acceptance Criteria

1. **Given** the centralized `Directory.Packages.props`,
   **When** the developer updates the Fluent UI package versions,
   **Then** `Microsoft.FluentUI.AspNetCore.Components` is `5.0.0.26098`
   **And** `Microsoft.FluentUI.AspNetCore.Components.Icons` is `5.0.0.26098`
   **And** no other Fluent UI v4 version references remain in any `.csproj` or `.props` file.

2. **Given** the Admin.UI project (`src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj`),
   **When** scoped CSS bundling properties are added to the PropertyGroup,
   **Then** `<DisableScopedCssBundling>true</DisableScopedCssBundling>` is present
   **And** `<ScopedCssEnabled>false</ScopedCssEnabled>` is present.

3. **Given** the Sample.BlazorUI project (`samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj`),
   **When** scoped CSS bundling properties are added to the PropertyGroup,
   **Then** `<DisableScopedCssBundling>true</DisableScopedCssBundling>` is present
   **And** `<ScopedCssEnabled>false</ScopedCssEnabled>` is present.

4. **Given** the design-directions prototype (`_bmad-output/planning-artifacts/design-directions-prototype/Hexalith.EventStore.DesignDirections.csproj`),
   **When** the `VersionOverride="4.13.2"` attributes are removed from both FluentUI PackageReferences,
   **Then** the prototype inherits version `5.0.0.26098` from `Directory.Packages.props`.

5. **Given** all package and csproj changes are complete,
   **When** `dotnet restore Hexalith.EventStore.slnx` is run,
   **Then** restore succeeds with zero errors.

6. **Given** all package and csproj changes are complete,
   **When** `dotnet build Hexalith.EventStore.slnx --configuration Release` is run,
   **Then** the raw `dotnet build` error output is pasted into the Dev Agent Record,
   **And** a summary table maps each distinct error type (CS error code + component name) to the resolving story (21-2 through 21-9).
   **Note:** Build failures in UI projects are EXPECTED and do NOT block story completion. This story is COMPLETE when package versions are updated, csproj properties are set, restore succeeds, build errors are documented, and non-UI tests pass.

7. **Given** the build result,
   **When** the developer inspects all `.csproj` and `.props` files,
   **Then** zero v4 Fluent UI package references (version `4.x.x`) remain anywhere in the repository.

8. **Given** all package changes are complete,
   **When** non-UI Tier 1 tests are run individually (`Contracts.Tests`, `Client.Tests`, `Testing.Tests`, `SignalR.Tests`),
   **Then** all pass with zero failures, confirming the package bump has no side effects on non-UI projects.
   **Note:** `Server.Tests` requires DAPR — skip if DAPR is not initialized.

## Tasks / Subtasks

- [x] Task 0: Create migration branch (prerequisite)
  - [x] 0.1: **MUST** create and checkout branch `feat/fluent-ui-v5-migration` from `main`. If branch already exists, checkout it. All Epic 21 stories use this single branch.
- [x] Task 1: Update centralized package versions (AC: 1)
  - [x] 1.1: Edit `Directory.Packages.props` lines 45-46: change `Version="4.14.0"` to `Version="5.0.0.26098"` for both `Microsoft.FluentUI.AspNetCore.Components` and `Microsoft.FluentUI.AspNetCore.Components.Icons`
  - [x] 1.2: Grep the entire repo for any remaining `4.14.0` or `4.13.2` FluentUI references in `.csproj` or `.props` files
- [x] Task 2: Add scoped CSS bundling properties to Admin.UI (AC: 2)
  - [x] 2.1: Edit `src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj` — add `<DisableScopedCssBundling>true</DisableScopedCssBundling>` and `<ScopedCssEnabled>false</ScopedCssEnabled>` inside the existing `<PropertyGroup>`
- [x] Task 3: Add scoped CSS bundling properties to Sample.BlazorUI (AC: 3)
  - [x] 3.1: Edit `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj` — add `<DisableScopedCssBundling>true</DisableScopedCssBundling>` and `<ScopedCssEnabled>false</ScopedCssEnabled>` inside the existing `<PropertyGroup>`
- [x] Task 4: Remove VersionOverride from design-directions prototype (AC: 4)
  - [x] 4.1: Edit `_bmad-output/planning-artifacts/design-directions-prototype/Hexalith.EventStore.DesignDirections.csproj` — remove the `VersionOverride="4.13.2"` **attribute** from both PackageReference elements. Keep the `<PackageReference Include="..." />` elements intact — only strip the VersionOverride attribute so they inherit from Directory.Packages.props.
- [x] Task 5: Verify restore and build (AC: 5, 6, 7)
  - [x] 5.0: Verify NuGet can reach v5 packages: check `nuget.config` (if present) for feed URLs. If feeds are restricted to a private source, confirm `5.0.0.26098` is available. If no `nuget.config` exists, nuget.org is the default source and the package is available there.
  - [x] 5.1: Run `dotnet restore Hexalith.EventStore.slnx` — must succeed. If restore fails with "package not found", check nuget.config feed configuration (see 5.0).
  - [x] 5.2: Run `dotnet build Hexalith.EventStore.slnx --configuration Release` — record result
  - [x] 5.3: If build fails, categorize all errors by type (removed components, renamed types, changed APIs) and map each category to the story that will fix it (21-2 through 21-9)
  - [x] 5.4: Run non-UI Tier 1 tests individually (AC: 8):
    - `dotnet test tests/Hexalith.EventStore.Contracts.Tests/`
    - `dotnet test tests/Hexalith.EventStore.Client.Tests/`
    - `dotnet test tests/Hexalith.EventStore.Testing.Tests/`
    - `dotnet test tests/Hexalith.EventStore.SignalR.Tests/`
    - `dotnet test tests/Hexalith.EventStore.Server.Tests/` (skip if DAPR not initialized)
    - All must pass. These have no FluentUI dependency.
    - If any test fails, compare against the last CI run on `main` to determine if the failure is pre-existing or caused by the package bump. Pre-existing failures should be noted but do not block this story.
  - [x] 5.5: Final grep: confirm zero v4 FluentUI version strings (`4.14.0`, `4.13.2`) remain in any `*.csproj` or `*.props` file. Grep only these file types — exclude `.md`, `.yaml`, and other documentation files which may legitimately reference old versions in historical context.

## Dev Notes

### What This Story Does and Does NOT Do

**DOES:** Updates package versions and csproj build infrastructure. This is the foundation for all subsequent v5 migration stories.

**DOES NOT:** Fix any component-level breaking changes. The build will likely fail after this story because v5 removes/renames many components. That is expected and correct — subsequent stories (21-2 through 21-9) address each category of breaking change.

**DO NOT attempt to fix any build errors caused by v5 breaking changes. Document them in the Dev Agent Record and move on. Stories 21-2 through 21-9 handle all component-level fixes. Do NOT modify any `.razor`, `.cs`, or test files in this story.**

### Completion Criteria

This story is **COMPLETE** when all of these are true:
1. Package versions updated to `5.0.0.26098` in `Directory.Packages.props`
2. Scoped CSS properties added to Admin.UI and Sample.BlazorUI csproj files
3. VersionOverride removed from design-directions prototype
4. `dotnet restore` succeeds
5. Build errors documented (raw output + categorized summary table) in Dev Agent Record
6. Non-UI Tier 1 tests pass (Contracts, Client, Testing, SignalR)
7. Zero v4 FluentUI version strings in any `*.csproj` or `*.props` file

**Build failures in UI projects do NOT block completion.** The build errors are the expected input for stories 21-2 through 21-9.

### Why Scoped CSS Bundling Must Be Disabled

V5 Fluent UI Blazor components no longer emit scoped CSS identifiers. The `::deep` CSS selector (used in 4 files, 12 occurrences) becomes useless. Setting `DisableScopedCssBundling=true` and `ScopedCssEnabled=false` prevents the build from generating scoped CSS bundles that won't work with v5 components. The actual `::deep` selector removal happens in Story 21-8.

**Important:** The Admin.UI project has 9 existing `.razor.css` files:
- `Components/ActivityChart.razor.css`
- `Components/CausationChainView.razor.css`
- `Components/ProjectionDetailPanel.razor.css`
- `Components/ProjectionStatusBadge.razor.css`
- `Components/StateDiffViewer.razor.css`
- `Components/TypeDetailPanel.razor.css`
- `Components/Shared/JsonViewer.razor.css`
- `Pages/TypeCatalog.razor.css`
- `Pages/Health.razor.css`

With scoped CSS disabled, these files will no longer be automatically scoped to their components. Their CSS rules will apply globally. Story 21-8 handles the CSS migration — for this story, just set the properties and document the scoped CSS file list.

### Exact Files to Modify

| # | File | Change |
|---|------|--------|
| 1 | `Directory.Packages.props` (lines 45-46) | `Version="4.14.0"` -> `Version="5.0.0.26098"` (both Components and Icons) |
| 2 | `src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj` | Add `DisableScopedCssBundling` + `ScopedCssEnabled` to PropertyGroup |
| 3 | `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj` | Add `DisableScopedCssBundling` + `ScopedCssEnabled` to PropertyGroup |
| 4 | `_bmad-output/planning-artifacts/design-directions-prototype/Hexalith.EventStore.DesignDirections.csproj` | Remove `VersionOverride="4.13.2"` from both PackageReferences (NOT in `.slnx` — cosmetic cleanup for AC #7 zero-v4 sweep) |

### Files NOT to Modify

- `tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj` — does NOT directly reference FluentUI packages; inherits via project reference to Admin.UI. No changes needed.
- `_Imports.razor` files — `@using Microsoft.FluentUI.AspNetCore.Components` namespace is unchanged in v5. No changes needed.
- Any `.razor` or `.cs` files — component-level changes are handled by stories 21-2 through 21-9.

### Current State of Each File

**Directory.Packages.props (lines 44-47):**
```xml
<ItemGroup Label="Blazor">
  <PackageVersion Include="Microsoft.FluentUI.AspNetCore.Components" Version="4.14.0" />
  <PackageVersion Include="Microsoft.FluentUI.AspNetCore.Components.Icons" Version="4.14.0" />
</ItemGroup>
```

**Admin.UI.csproj PropertyGroup (no scoped CSS properties):**
```xml
<PropertyGroup>
  <IsPackable>false</IsPackable>
  <IsPublishable>true</IsPublishable>
  <RootNamespace>Hexalith.EventStore.Admin.UI</RootNamespace>
</PropertyGroup>
```

**Sample.BlazorUI.csproj PropertyGroup (no scoped CSS properties):**
```xml
<PropertyGroup>
  <IsPackable>false</IsPackable>
  <IsPublishable>true</IsPublishable>
</PropertyGroup>
```

**DesignDirections.csproj (has VersionOverride):**
```xml
<PackageReference Include="Microsoft.FluentUI.AspNetCore.Components" VersionOverride="4.13.2" />
<PackageReference Include="Microsoft.FluentUI.AspNetCore.Components.Icons" VersionOverride="4.13.2" />
```

### Expected Build Error Categories (for Task 5.3 mapping)

After the package bump, the build will fail. These are the expected error categories and which story resolves each:

| Error Category | Example | Resolving Story |
|---|---|---|
| `FluentDesignTheme` not found | Used in App.razor, MainLayout.razor | 21-2 |
| `FluentHeader` / `FluentBodyContent` not found | Used in MainLayout.razor | 21-2 |
| `FluentMenuProvider` not found | Used in App.razor | 21-2 |
| `FluentNavMenu` / `FluentNavLink` / `FluentNavGroup` not found | Used in NavMenu.razor | 21-2 |
| `DesignThemeModes` not found | Used in ThemeState.cs | 21-2 |
| `Appearance` enum changes | Used in ~45 files | 21-3, 21-4 |
| `FluentTextField` / `FluentNumberField` / `FluentSearch` not found | Used in ~30 files | 21-5 |
| `FluentProgressRing` not found | Used in 8 files | 21-5 |
| `FluentDialogHeader` / `FluentDialogFooter` changes | Used in 12 files | 21-6 |
| `IToastService` method changes | Used in 9 files | 21-7 |
| CSS token compilation errors (unlikely — CSS is runtime) | N/A | 21-8 |
| `FluentAnchor` not found | Used in 10 files | 21-4 |
| `FluentTab Label=` parameter removed | Used in TypeCatalog.razor | 21-9 |
| `Align` / `SortDirection` enum moved | Used in DataGrid pages | 21-9 |
| `FluentComponentBase` constructor requires `LibraryConfiguration` | Any custom components inheriting `FluentComponentBase` | 21-2 (FluentProviders cascading) |

### TreatWarningsAsErrors Consideration

The project has `TreatWarningsAsErrors=true` globally. If v5 marks any old APIs as `[Obsolete]` instead of removing them, the obsolete warnings will become build errors. Do NOT suppress `TreatWarningsAsErrors` — the build errors are the intended signal for subsequent stories.

### Non-UI Tests Must Still Pass

The following test projects have NO FluentUI dependency and must continue to pass after the package bump:
- `tests/Hexalith.EventStore.Contracts.Tests/`
- `tests/Hexalith.EventStore.Client.Tests/`
- `tests/Hexalith.EventStore.Server.Tests/` (requires DAPR — skip if not available)
- `tests/Hexalith.EventStore.Testing.Tests/`
- `tests/Hexalith.EventStore.SignalR.Tests/`

The Admin.UI.Tests project WILL fail (it references Admin.UI which uses FluentUI). This is expected.

### Previous Story Intelligence (Story 21-0)

Story 21-0 established the bUnit baseline:
- **592 tests** passing on v4.14.0 (14 new baseline tests added)
- Test project uses `Microsoft.NET.Sdk.Razor` SDK
- `AdminUITestContext` base class provides FluentUI component registration
- bUnit renders FluentUI as kebab-case web component tags (`fluent-nav-menu`, `fluent-button`, etc.)
- Key v4 regression markers tested: `fluent-nav-menu`, `fluent-nav-link`, `fluent-nav-group`, `fluent-anchor` with `appearance="accent"`, `fluent-card`

After story 21-1, the Admin.UI.Tests will not compile until stories 21-2+ fix the component-level changes. This is expected.

### Project Structure Notes

- Centralized package management via `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`)
- Individual `.csproj` files reference packages WITHOUT version attributes (inherit from Directory.Packages.props)
- Exception: design-directions prototype uses `VersionOverride` to pin an older version (this project is NOT in `Hexalith.EventStore.slnx` — `dotnet build` won't touch it; the VersionOverride removal is for the zero-v4 sweep only)
- Solution file: `Hexalith.EventStore.slnx` (modern XML format — never use `.sln`)

### Architecture Compliance

- **Build command:** `dotnet build Hexalith.EventStore.slnx --configuration Release`
- **Restore command:** `dotnet restore Hexalith.EventStore.slnx`
- **Warnings as errors:** Enabled globally — do NOT disable
- **Naming:** Follow existing csproj property naming conventions (PascalCase XML elements)
- **No new packages needed** — this story only updates existing package versions and adds csproj properties

### Library/Framework Requirements

| Package | Current Version | Target Version | Source |
|---------|----------------|----------------|--------|
| Microsoft.FluentUI.AspNetCore.Components | 4.14.0 | 5.0.0.26098 | `Directory.Packages.props` line 45 |
| Microsoft.FluentUI.AspNetCore.Components.Icons | 4.14.0 | 5.0.0.26098 | `Directory.Packages.props` line 46 |

**DO NOT** add any new package references. Only update existing versions and add csproj properties.

### Git Intelligence

Recent commits:
- `6a8d0a1 test(admin-ui): add bUnit baseline smoke tests for Fluent UI v4->v5 migration (Story 21-0)` — previous story in this epic
- `5aeaf90 chore(release): 1.3.0 [skip ci]` — latest release on v4.14.0
- `bce2614 feat(server): add stream activity tracker writer (Story 15-13)` — last feature story
- `9f271c5 docs(planning): add Epic 21 Fluent UI v4->v5 migration and close Epic 19` — epic planning

**Branch strategy (from sprint change proposal):** All Epic 21 work should execute on a `feat/fluent-ui-v5-migration` branch.

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-04-13-fluent-ui-v5-migration.md#Story 21-1]
- [Source: Directory.Packages.props#L44-47] -- current FluentUI package versions
- [Source: src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj] -- Admin.UI project file
- [Source: samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj] -- Sample project file
- [Source: _bmad-output/planning-artifacts/design-directions-prototype/Hexalith.EventStore.DesignDirections.csproj] -- prototype with VersionOverride
- [Source: tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj] -- test project (no direct FluentUI ref)
- [Source: _bmad-output/implementation-artifacts/21-0-bunit-smoke-tests-baseline.md] -- previous story learnings
- [Source: Fluent UI Blazor MCP Server v5.0.0.26098] -- confirmed target version and migration overview

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Version `5.0.0.26098` specified in story does not exist on nuget.org. Closest: `5.0.0-rc.2-26098.1` (prerelease). User approved using this version.
- `Microsoft.FluentUI.AspNetCore.Components.Icons` has no v5 release; latest stable is `4.14.0`. Kept at `4.14.0`.
- Fluent UI v5 installation docs warn against `TreatWarningsAsErrors=true` with prerelease — noted but not suppressed per story instructions.

### Build Result (253 errors, 0 warnings)

Build fails as expected in UI projects only. Non-UI projects all build successfully.

#### Raw Error Summary (by count)

| Count | Error Code | Component / Description | Project |
|-------|-----------|------------------------|---------|
| 108 | RZ9991 | `bind-Value` attribute inference failure (removed input components) | Admin.UI |
| 68 | RZ10012 | `FluentTextField` not found | Admin.UI |
| 56 | RZ10012 | `FluentDialogHeader` not found | Admin.UI |
| 52 | RZ10012 | `FluentDialogFooter` not found | Admin.UI |
| 34 | RZ10012 | `FluentNavLink` not found | Admin.UI |
| 30 | RZ10012 | `FluentAnchor` not found | Admin.UI |
| 30 | RZ10000 | `FluentSelect` missing required type argument `TValue` | Admin.UI |
| 22 | RZ10012 | `FluentSearch` not found | Admin.UI |
| 20 | RZ10012 | `FluentNumberField` not found | Admin.UI |
| 12 | CS0103 | `MessageIntent` name not found | Sample.BlazorUI |
| 10 | RZ2008 | `FluentOption` `Value` attribute requires a value | Admin.UI |
| 10 | CS0246 | `DesignThemeModes` type not found | Admin.UI |
| 8 | RZ10012 | `FluentNavLink` not found | Sample.BlazorUI |
| 8 | CS1503 | `Appearance` → `ButtonAppearance?` conversion | Sample.BlazorUI |
| 8 | CS0618 | `FluentProgress` obsolete → renamed to `FluentProgressBar` | Sample.BlazorUI |
| 4 | CS0618 | `Appearance.Accent` obsolete → use `ButtonAppearance.Primary` | Sample.BlazorUI |
| 2 | RZ9991 | `bind-Expanded` attribute inference failure | Admin.UI |
| 2 | RZ10012 | `FluentNavMenu` not found | Admin.UI |
| 2 | RZ10012 | `FluentNavMenu` not found | Sample.BlazorUI |
| 2 | RZ10012 | `FluentNavGroup` not found | Admin.UI |
| 2 | RZ10012 | `FluentMenuProvider` not found | Admin.UI |
| 2 | RZ10012 | `FluentHeader` not found | Admin.UI |
| 2 | RZ10012 | `FluentHeader` not found | Sample.BlazorUI |
| 2 | RZ10012 | `FluentDesignTheme` not found | Admin.UI |
| 2 | RZ10012 | `FluentDesignTheme` not found | Sample.BlazorUI |
| 2 | RZ10012 | `FluentBodyContent` not found | Admin.UI |
| 2 | RZ10012 | `FluentBodyContent` not found | Sample.BlazorUI |
| 2 | CS0618 | `Appearance.Stealth` obsolete → use `ButtonAppearance.Default` | Sample.BlazorUI |
| 2 | CS0618 | `Appearance.Outline` obsolete → use `ButtonAppearance.Outline` | Sample.BlazorUI |

#### Error-to-Story Mapping

| Error Category | Components | Count | Resolving Story |
|---------------|-----------|-------|----------------|
| Layout/Nav removed | `FluentDesignTheme`, `FluentHeader`, `FluentBodyContent`, `FluentMenuProvider`, `FluentNavMenu`, `FluentNavLink`, `FluentNavGroup` | 56 | **21-2** |
| Theme enum removed | `DesignThemeModes` | 10 | **21-2** |
| Button Appearance enum split | `Appearance` → `ButtonAppearance` conversion errors | 8 | **21-3** |
| Appearance obsolete values | `Appearance.Accent`, `.Stealth`, `.Outline` obsolete | 8 | **21-3, 21-4** |
| Anchor removed | `FluentAnchor` not found | 30 | **21-4** |
| Input components renamed | `FluentTextField`, `FluentSearch`, `FluentNumberField` not found + `bind-Value` failures | 218 | **21-5** |
| Progress renamed | `FluentProgress` → `FluentProgressBar` | 8 | **21-5** |
| Dialog restructured | `FluentDialogHeader`, `FluentDialogFooter` not found | 108 | **21-6** |
| Toast/MessageIntent | `MessageIntent` not found | 12 | **21-7** |
| Select type arg required | `FluentSelect` missing `TValue` | 30 | **21-9** |
| FluentOption value required | `FluentOption` `Value` attribute | 10 | **21-9** |

### Non-UI Tier 1 Test Results

| Test Project | Result | Tests |
|-------------|--------|-------|
| Contracts.Tests | PASS | 271 |
| Client.Tests | PASS | 321 |
| Testing.Tests | PASS | 67 |
| SignalR.Tests | PASS | 32 |
| Server.Tests | SKIPPED | (DAPR not initialized) |
| **Total** | **PASS** | **691** |

### Completion Notes List

- Package versions updated: Components `4.14.0` → `5.0.0-rc.2-26098.1`, Icons kept at `4.14.0` (no v5 exists)
- Scoped CSS bundling disabled in Admin.UI and Sample.BlazorUI csproj files
- VersionOverride removed from design-directions prototype
- `dotnet restore` succeeds with zero errors
- Build produces 253 errors across Admin.UI and Sample.BlazorUI only — all expected v5 breaking changes
- All 691 non-UI Tier 1 tests pass — package bump has no side effects on non-UI projects
- Zero v4 FluentUI references in source `.csproj`/`.props` files (Icons at `4.14.0` is correct — no v5 exists)

### File List

- `Directory.Packages.props` — updated Components version to `5.0.0-rc.2-26098.1`
- `src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj` — added `DisableScopedCssBundling` + `ScopedCssEnabled`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj` — added `DisableScopedCssBundling` + `ScopedCssEnabled`
- `_bmad-output/planning-artifacts/design-directions-prototype/Hexalith.EventStore.DesignDirections.csproj` — removed `VersionOverride="4.13.2"` from both PackageReferences

### Review Findings

- [x] [Review][Decision] `.razor.css` files vanish entirely when scoped CSS disabled — Resolved: Option 1 chosen. Updated Story 21-8 backlog note in sprint-status.yaml to flag that `ScopedCssEnabled=false` causes `.razor.css` files to be excluded from build output entirely (not "apply globally"). Story 21-8 scope must include re-including the 9 Admin.UI `.razor.css` files.
- [x] [Review][Defer] Icons package remains at v4.14.0 [Directory.Packages.props:46] — deferred, no v5 Icons package exists on nuget.org. User-approved deviation from AC #1. Monitor for future v5 release.
- [x] [Review][Defer] Stale `.nuget.g.props` artifacts tracked in git reference v4.13.2 [src/.../.artifacts/, tests/.../.artifacts/] — deferred, pre-existing repo hygiene issue. Auto-generated NuGet restore cache files. Running `dotnet restore` would regenerate.

### Change Log

- 2026-04-13: Story 21-1 implemented — Fluent UI package infrastructure updated for v5 migration
- 2026-04-13: Code review complete — 1 decision-needed, 0 patch, 2 deferred, 5 dismissed
