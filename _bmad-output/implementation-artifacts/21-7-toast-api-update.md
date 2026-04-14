# Story 21.7: Toast API Update (IToastService.Show* → ShowToastAsync + Extension Helpers)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

**Dependency:** Story 21-6 (dialog restructure) must be `done` before starting. ✅ (done 2026-04-14)

**Unblocks:** Story 21-6 AC 30 (visual verification of 28 dialogs) — currently `DEFERRED-TO-21-7-OR-EPIC-21-RETRO`. Story 21-4 AC 22 (badge contrast). Entire Admin.UI compilation — 69 × CS1061 errors trace to this migration.

## Story

As a developer migrating Admin.UI from Fluent UI Blazor v4 to v5,
I want every `IToastService.ShowSuccess/ShowError/ShowWarning/ShowInfo` call replaced with async `ShowToastAsync(options => …)` equivalents wrapped in thin extension helpers,
so that Admin.UI compiles cleanly under the v5 toast API (which removed all short-form `Show*` convenience methods) and the remaining migration stories (21-8 CSS, 21-9 DataGrid, 21-10 Sample) can land on a green build.

## Acceptance Criteria

### Extension helpers

1. **Given** no project-level wrapper for the v5 toast API exists,
   **When** the file `src/Hexalith.EventStore.Admin.UI/Services/ToastServiceExtensions.cs` is created,
   **Then** it contains a `public static class ToastServiceExtensions` in namespace `Hexalith.EventStore.Admin.UI.Services`,
   **And** it defines four `public static Task ShowSuccessAsync / ShowErrorAsync / ShowWarningAsync / ShowInfoAsync` extension methods on `IToastService`,
   **And** each extension delegates to `ToastService.ShowToastAsync(options => { options.Intent = ToastIntent.X; options.Title = title; options.Body = body; })` (Title + Body split per Dev Notes §Title/Body mapping),
   **And** each extension returns the `Task` from `ShowToastAsync` (do NOT discard/await internally — callers await at the call site).

2. **Given** a call site that only passes a single string to the v4 `.Show*(...)` method,
   **When** migrated,
   **Then** the extension method is called with `title: string.Empty` and `body: <the original string>` (per Dev Notes §Title/Body mapping),
   **And** the behavior matches v4 (full message rendered in the toast body; no visible title).

### Call-site migration (106 sites across 9 files)

3. **Given** any `ToastService.ShowSuccess("message")` call in `src/Hexalith.EventStore.Admin.UI/**/*.razor`,
   **When** migrated,
   **Then** it becomes `await ToastService.ShowSuccessAsync("message")` (with the argument rendered as `body`; no explicit title argument required — see AC 2).

4. **Given** any `ToastService.ShowError("message")` call,
   **When** migrated,
   **Then** it becomes `await ToastService.ShowErrorAsync("message")`.

5. **Given** any `ToastService.ShowWarning("message")` call,
   **When** migrated,
   **Then** it becomes `await ToastService.ShowWarningAsync("message")`.

6. **Given** any `ToastService.ShowInfo("message")` call,
   **When** migrated,
   **Then** it becomes `await ToastService.ShowInfoAsync("message")`.

### Calling-method signatures

7. **Given** a method that makes one or more toast calls,
   **When** migrated,
   **Then** the method is `async Task` (NOT `async void` — `async void` swallows Blazor exceptions silently; see Dev Notes §Known v5 gotchas),
   **And** every `ShowXAsync` call is awaited,
   **And** callers of the (now async) method are updated to `await` the call or register it on an `EventCallback` that invokes `async Task`.

8. **Given** a toast call inside a `private void` handler that cannot be converted to `async Task` without breaking an existing callback contract,
   **When** migrated,
   **Then** the surrounding method MUST be converted to `async Task` and every caller (Blazor `@onclick`, `EventCallback`, internal invocation) updated — there are **no exceptions** in the 106 call-site inventory (all current sync handlers that show toasts can become `async Task` safely).

### ~~Single special case: Projections.OnRefreshSignal~~ *(removed: redundant with AC 7)*

9. *(Removed during review — `Pages/Projections.razor:239` is already inside an `async Task` method; AC 7 covers it. See Dev Notes §Projections.razor edge case for implementation note.)*

### Removed v5 toast content-component types

10. **Given** the v5 migration guide removes `CommunicationToast` / `ConfirmationToast` / `ProgressToast` (and their `*ToastContent` counterparts),
    **When** the codebase is audited,
    **Then** `grep -rnE "CommunicationToast|ConfirmationToast|ProgressToast" src/` returns 0 hits (confirmed pre-story — nothing to remove, but AC verifies cleanliness).

11. **Given** the v5 toast API uses `ToastIntent` (enum: `Info`, `Success`, `Warning`, `Error`),
    **When** the extension methods are written,
    **Then** each uses the correct `ToastIntent` value (Success / Error / Warning / Info),
    **And** the appropriate `using Microsoft.FluentUI.AspNetCore.Components;` import exists in `ToastServiceExtensions.cs`.

### ~~Optional UX enhancement~~ *(removed: documentation, not acceptance)*

12. *(Removed during review — the QuickAction retry enhancement is not an acceptance criterion, it's a scope statement. See Dev Notes §What This Story Does and Does NOT Do — documented as explicitly out of scope.)*

### Grep-for-zero gates

13. **Given** story completion,
    **When** the broader grep `grep -rnE "ToastService\.Show(Success|Error|Warning|Info)[^A]" src/Hexalith.EventStore.Admin.UI --include='*.razor' --include='*.cs'` runs (the `[^A]` excludes the new `*Async` variants and catches the v4 short-form in expression contexts, ternaries, lambda captures, and method references — not just bare call-statements),
    **Then** it returns **0 hits** (every v4 `Show*` is gone in every syntactic position — statements, expressions, ternaries, delegate captures, `@(...)` Razor expressions).

14. **Given** story completion,
    **When** `grep -rn "ShowSuccessAsync\|ShowErrorAsync\|ShowWarningAsync\|ShowInfoAsync" src/Hexalith.EventStore.Admin.UI --include='*.razor' --include='*.cs'` runs (excluding the 4 extension-method declarations in `ToastServiceExtensions.cs`),
    **Then** the count equals the **PRE-migration `Show*(` call count** recorded in Task 0.2 — i.e. every `ShowSuccess(` / `ShowError(` / `ShowWarning(` / `ShowInfo(` that existed pre-story has exactly one `Show*Async(` successor,
    **And** any net delta (legitimate new calls added in catch blocks, or calls removed as dead code during migration) is enumerated in Completion Notes with file:line and rationale.

    Rationale: an absolute "= 106" assertion breaks if Amelia legitimately adds a 107th call during migration (e.g., in an error path not previously toasted). Symmetry — `Show*(` removed = `Show*Async(` added — is the correct invariant.

15. **Given** story completion,
    **When** `grep -rnE "async void" src/Hexalith.EventStore.Admin.UI --include='*.razor' --include='*.cs'` runs on files this story modified,
    **Then** it returns **0 hits on modified files** (pre-existing `async void` on unrelated surfaces — `NavMenu.razor`, `StreamDetail.razor` — is out of scope and may remain; see 21-6 baseline).

### Build gates (HARD)

16. **Given** story completion,
    **When** `dotnet build src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj --configuration Release` runs,
    **Then** all **69 × CS1061** errors on `IToastService.Show*` members (inventoried in 21-6 Completion Notes) are eliminated,
    **And** zero new errors are introduced on files this story modifies.

17. **Given** story completion,
    **When** `dotnet build Hexalith.EventStore.slnx --configuration Release` runs,
    **Then** the total error count drops by **at least the 69 CS1061 toast errors** recorded as the 21-6 baseline,
    **And** every residual error is attributable to a **downstream** story (21-8 CSS tokens, 21-9 DataGrid renames, 21-10 Sample alignment, or the pre-existing 2 × CS0246 on `IDialogReference` in ProjectionDetailPanel.razor which remains deferred to a separate dialog-service story — see 21-6 File List),
    **And** **zero residual errors trace back to the 9 files this story modified.** This is a HARD gate. "Modified surface" = the 9 `.razor` source files listed in §Call-Site Inventory PLUS any Admin.UI.Tests files modified in Task 4 PLUS the new `ToastServiceExtensions.cs`.

18. **Given** story completion,
    **When** non-UI Tier 1 tests run (`dotnet test` on Contracts + Client + Testing + SignalR + Sample),
    **Then** all pass with zero regressions (should be automatic — this story does not touch non-UI projects).

### bUnit tests

19. **Given** existing `Admin.UI.Tests` tests that exercise toast-showing code paths (`CompactionPageTests.CompactionPage_TriggerDialog_ShowsErrorToastOnFailure`, plus any in `BackupsPageTests` and `SnapshotsPageTests`),
    **When** the surrounding page methods become `async Task`,
    **Then** the tests still compile and pass **without** modification beyond `await` at new async boundaries,
    **And** any `NSubstitute` mock of `IToastService` continues to intercept the new extension-method path (extension methods call `ShowToastAsync` — if a test needs to assert a toast was shown, it must now verify `ShowToastAsync` was invoked, not `Show*`).

20. **Given** a new bUnit test for the extension-method surface,
    **When** story completion,
    **Then** `tests/Hexalith.EventStore.Admin.UI.Tests/Services/ToastServiceExtensionsTests.cs` exists and contains at minimum **6 tests**:
    - `ShowSuccessAsync_CallsShowToastAsync_WithSuccessIntent`
    - `ShowErrorAsync_CallsShowToastAsync_WithErrorIntent`
    - `ShowWarningAsync_CallsShowToastAsync_WithWarningIntent`
    - `ShowInfoAsync_CallsShowToastAsync_WithInfoIntent`
    - `ShowSuccessAsync_NullMessage_PassesNullBodyWithoutThrowing` — call `ShowSuccessAsync(null!)`; assert the captured `options.Body` is `null` and no `ArgumentNullException` is thrown by the extension itself. (If v5 `ShowToastAsync` throws on null body, update this test to assert the exception propagates — the extension must NOT swallow it.)
    - `ShowSuccessAsync_WhenShowToastAsyncThrows_PropagatesException` — configure the `IToastService` substitute so `ShowToastAsync` returns `Task.FromException<ToastResult>(new InvalidOperationException("provider not registered"))`; assert `await ShowSuccessAsync("x")` rethrows `InvalidOperationException`. (Pattern: `await Should.ThrowAsync<InvalidOperationException>(() => mockToast.ShowSuccessAsync("x"))`.)

    Each Intent-test substitutes `IToastService`, calls the extension, and asserts `ShowToastAsync(Arg.Any<Action<ToastOptions>>())` was received once with an options delegate that produces the correct `ToastIntent`. Pattern: capture the delegate with `Arg.Do<Action<ToastOptions>>(a => a(options))` and assert on the captured `options` instance.

    **Pre-flight spike (Murat):** Before writing all 6 tests, verify `new ToastOptions()` (parameterless constructor) works against the v5 assembly this project references. If `ToastOptions` requires a non-default constructor in v5, adapt the capture pattern accordingly (e.g., use a subclass or a factory). One test written and passing = green-light for the other 5.

### Visual verification (gates 21-6 AC 30 and 21-4 AC 22)

21. **Given** story completion AND Admin.UI compiles cleanly AND DAPR topology boots,
    **When** `aspire run` produces a running Admin.UI,
    **Then** a visual spot-check triggers at least one **success toast** (create tenant / create backup / create snapshot policy) and one **error toast** (disconnect admin API mid-flight → expect "Admin service unavailable" toast),
    **And** both toasts render with correct color/icon per `ToastIntent`,
    **And** both toasts auto-dismiss after the v5 default timeout (7000ms; see §V5 Toast API §Timeout),
    **And** **neither toast displays a visible empty title bar** (the body-only mapping sets `Title = string.Empty` — if v5 renders a 20px-tall empty header for non-null-but-empty titles, this is a UX regression and the extension must change to `Title = null` instead; verify via screenshot comparison or DevTools inspection of the toast DOM — expected: no title slot rendered),
    **And** light mode AND dark mode are both verified,
    **And** 21-6 AC 30 (28 dialogs open/close) is exercised in the same session,
    **And** 21-4 AC 22 (status badge contrast on Consistency page) is exercised in the same session.

22. **Given** story completion,
    **When** DevTools is opened on a running Admin.UI page with a toast visible,
    **Then** no console errors reference missing `Show*` methods,
    **And** no `WASM` exceptions reference `ToastOptions` misconfiguration.

## Tasks / Subtasks

- [x] Task 0: Pre-flight audit — confirm scope before touching code (AC: all)
  - [x] 0.1: PRE-AdminUI: total=148 (CS1061=212, CS0103=78, CS0246=4, RZ9986=2 — error-line doubles due to razor source-gen passes). Deviates from 21-6's "~76 baseline" estimate: actual toast-related CS1061 dominates.
  - [x] 0.2: Grep counts verified: ShowSuccess=23, ShowError=79, ShowWarning=3, ShowInfo=1 → **106 total** ✓ (matches §Call-Site Inventory). Distribution per file matches spec.
  - [x] 0.3: `IToastService` registered via `AddFluentUIComponents()` in `AdminUIServiceExtensions.cs` — confirmed, no DI changes needed.
  - [x] 0.4: All 106 enclosing methods already `async Task` (or `async ValueTask`) — verified per file below. Zero `async void` conversions required.
  - [x] 0.5: Admin.UI.Tests inventory: zero test files mock `IToastService` or assert `Show*` — Task 4.1 is a no-op. Only Task 4.2 (new test file) applies.
  - [x] 0.6: `FluentToastProvider` at `src/Hexalith.EventStore.Admin.UI/Components/App.razor:18`, inside `<FluentProviders>`. Render tree verified.
  - [x] 0.7: Broader toast-call audit — zero ternary/lambda/`@(...)`/`_ =` captures. All 106 sites are bare statement-form `ToastService.ShowX(...);`.
  - [x] 0.8: Namespace collision — all 9 target `.razor` files already declare `@using Hexalith.EventStore.Admin.UI.Services` locally (confirmed by grep). `_Imports.razor` change NOT required — extensions resolve via existing per-file using. Task 1.7 is a no-op.

- [x] Task 1: Create `ToastServiceExtensions.cs` with four async helpers (AC: 1, 2, 11)
  - [x] 1.1: `src/Hexalith.EventStore.Admin.UI/Services/ToastServiceExtensions.cs` created.
  - [x] 1.2: File-scoped namespace `Hexalith.EventStore.Admin.UI.Services;`.
  - [x] 1.3: `using Microsoft.FluentUI.AspNetCore.Components;` added.
  - [x] 1.4: Four extensions (ShowSuccessAsync/ShowErrorAsync/ShowWarningAsync/ShowInfoAsync) return `Task` from `ShowToastAsync`, set `Title=string.Empty`, body carries message, Intent per method.
  - [x] 1.5: XML `<summary>` + `<param>` + `<returns>` comments added.
  - [x] 1.6: Isolated compile verified (69 toast errors elsewhere as expected until Task 2+3).
  - [x] 1.7: No-op per Task 0.8. `_Imports.razor` untouched.
  - **Post-build refinement:** CA1062 (non-null validation on public APIs) triggered by `TreatWarningsAsErrors`. Added `ArgumentNullException.ThrowIfNull(toastService);` to each method. Compiler now clean on the new file.

- [x] Task 2: Migrate Layout/Breadcrumb.razor (2 calls) — simplest file, validate pattern (AC: 3, 4, 7)
  - [x] 2.1: Line 178 → `await ToastService.ShowSuccessAsync("Link copied to clipboard")`.
  - [x] 2.2: Line 182 → `await ToastService.ShowErrorAsync("Could not copy link")`.
  - [x] 2.3: `CopyUrlAsync` already `async Task` — no signature change.
  - [x] 2.4: SPIKE GATE passed — toast-error count 212 → 208 (Δ=4, reflects double-reporting per razor source-gen pass; Δ=2 call sites is the real delta). Zero new errors on Breadcrumb.

- [x] Task 3: Migrate remaining 8 files (AC: 3-6, 7)
  - [x] 3.1: Projections.razor (1 call, line 239 `ShowWarning` in `LoadDataAsync` async Task) → migrated.
  - [x] 3.2: DeadLetters.razor (4 calls, `LoadMoreAsync` + `ExecuteBulkActionAsync` — all async Task) → migrated.
  - [x] 3.3: Compaction.razor (6 calls, `OnTriggerConfirm` async Task — confirmed) → migrated.
  - [x] 3.4: Consistency.razor (11 calls: OnTriggerConfirm 7, OnCancelConfirm 3, ExportCheckResultAsync 1 — all async Task) → migrated.
  - [x] 3.5: Backups.razor (18 calls across OnCreateBackupConfirm / OnValidateConfirm / OnRestoreConfirm / OnExportConfirm / OnImportConfirm — all async Task) → migrated.
  - [x] 3.6: Tenants.razor (18 calls across OnCreateTenantConfirm / ExecuteLifecycleAction / OnAddUserConfirm / OnRemoveUserConfirm / OnChangeRoleConfirm — all async Task) → migrated.
  - [x] 3.7: ProjectionDetailPanel.razor (22 calls across LoadDetailAsync / OnPauseClick / OnResumeClick / ConfirmResetAsync / ConfirmReplayAsync / ExecuteOperationAsync / PollForUpdateAsync — all async Task) → migrated.
  - [x] 3.8: Snapshots.razor (24 calls across OnCreatePolicyConfirm / OnEditPolicyConfirm / OnDeletePolicyConfirm / OnCreateSnapshotConfirm — all async Task) → migrated.
  - [x] 3.9: Zero sync methods required conversion (Task 0.4 audit confirmed).
  - [x] 3.10: No caller chain updates needed (no sig changes).
  - [x] 3.11: After all files, POST Admin.UI error count = 42 (Δ=−106, matches 106 call sites exactly).

- [x] Task 4: Update / add bUnit tests (AC: 19, 20)
  - [x] 4.1: No-op (Task 0.5 inventory returned zero hits).
  - [x] 4.2: `tests/Hexalith.EventStore.Admin.UI.Tests/Services/ToastServiceExtensionsTests.cs` added with 6 tests: 4 intent tests + null-message + exception-propagation. Uses shared `CaptureOptionsAsync` helper with `Arg.Do<Action<ToastOptions>>` pattern on NSubstitute.
  - [x] 4.2a: Murat's spike — `new ToastOptions()` parameterless ctor valid against v5 assembly (confirmed by clean compile of test file).
  - [x] 4.3: **Test-project compile verified** — zero errors originate in `tests/`. Residual 42 errors all from `src/Hexalith.EventStore.Admin.UI/` (21-8/21-9/21-6-IDialogRef scope). **Test execution DEFERRED per spec note** until 21-8 + 21-9 land and Admin.UI compiles cleanly.

- [x] Task 5: Build & verification (AC: 13-18)
  - [x] 5.1: Four-pass grep-for-zero all green:
    - Pass 1 `ToastService.Show(Success|Error|Warning|Info)[^A]`: **0 hits** (AC 13 ✓)
    - Pass 2 `ShowXAsync` count: 110 total across 10 files = 4 declarations (ToastServiceExtensions.cs) + 106 call sites = PRE-migration count exactly (AC 14 ✓)
    - Pass 3 `CommunicationToast|ConfirmationToast|ProgressToast`: **0 hits** (AC 10 ✓)
    - Pass 4 `async void` on modified files: **0 hits** (pre-existing hits on NavMenu.razor:98 and StreamDetail.razor:478 untouched, explicitly out of scope per AC 15 rationale) ✓
  - [x] 5.2: Admin.UI isolated build: **148 → 42 errors**, Δ=−106 on toast CS1061 (AC 16 ✓). Residuals: 78 CS0103 (21-9 DataGrid `SortDirection`/`Align`/DaprComponentType), 4 CS0246 (21-6 `IDialogReference` × 2 sites × 2 passes), 2 RZ9986 (21-8 MainLayout mixed-content Class). Zero residuals trace to toast-modified surface.
  - [x] 5.3: Full slnx build: **60 errors** — 90 CS0103 (21-9), 16 CS0618 + 8 CS1503 (21-10 Sample.BlazorUI FluentProgress/Appearance.Accent), 4 CS0246 (21-6), 2 RZ9986 (21-8). All attributable to downstream stories (AC 17 ✓). Total error count dropped by the 106 toast errors vs. PRE-baseline.
  - [x] 5.3a: Cold-cache CI rehearsal — deleted `obj/` + `bin/` on Admin.UI and Admin.UI.Tests, re-built. Same 42 errors as warm cache. No `_Imports.razor` race.
  - [x] 5.4: Tier 1 tests: Contracts 271/271, Client 321/321, Sample 62/62, Testing 67/67, SignalR 32/32 — **753/753 pass** (AC 18 ✓).
  - [x] 5.5: Admin.UI.Tests run DEFERRED per Task 4.3 caveat (test project inherits Admin.UI's 42 residuals until 21-8 + 21-9 land).

- [x] Task 6: Visual verification (AC 21, 22) — **DEFERRED-TO-21-8-OR-21-9-LAND** (precondition "Admin.UI compiles cleanly" not met)
  - [x] 6.1-6.9: **Cannot execute** — AC 21 requires "Admin.UI compiles cleanly AND DAPR topology boots". Admin.UI compile is currently red on 42 downstream errors. Same deferral pattern as 21-4 AC 22 / 21-6 AC 30. Run this Task 6 (plus rolled-in 21-4 AC 22 and 21-6 AC 30) during 21-8 completion or Epic 21 retrospective. Does not block 21-7 from entering review.

### Review Findings

- [x] [Review][Decision] Body-only toast title mapping may regress UI if empty title renders a header slot — resolved: keep `Title = string.Empty` now and validate visual rendering during AC 21.
- [x] [Review][Decision] Story status policy with deferred hard gates — resolved: keep story status as `review` during review triage while downstream blockers are tracked separately; workflow then advanced story to `done` after patch resolution.
- [x] [Review][Patch] Awaited toast calls can couple operation flow to toast infrastructure failures [src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor:904] — fixed by routing Backups page success/error to best-effort toast wrappers so operation flow is not blocked by toast provider failures.
- [x] [Review][Patch] Extension method nullability contract does not match null-body behavior validated by tests [src/Hexalith.EventStore.Admin.UI/Services/ToastServiceExtensions.cs:23] — fixed by updating `Show*Async` message parameters to nullable `string?` and aligning test helper signatures.
- [x] [Review][Defer] AC 19/21/22 execution remains blocked by downstream compile issues in 21-8/21-9 [_bmad-output/implementation-artifacts/21-7-toast-api-update.md:213] — deferred, pre-existing

## Dev Notes

### What This Story Does and Does NOT Do

**DOES:**
- Creates `Admin.UI/Services/ToastServiceExtensions.cs` with 4 extension methods wrapping `ShowToastAsync` + `ToastIntent`.
- Replaces 106 `ToastService.ShowSuccess/ShowError/ShowWarning/ShowInfo(...)` calls with `await ToastService.ShowXAsync(...)` across 9 files.
- Converts any non-async calling method to `async Task` and propagates `await` through callers.
- Adds 4 unit tests covering the extension-method surface.
- Unblocks 21-6 AC 30 (dialog visual verification) and 21-4 AC 22 (badge contrast).
- Eliminates the 69 × CS1061 `IToastService.Show*` errors blocking Admin.UI compilation.

**DOES NOT:**
- Touch CSS tokens (21-8), DataGrid enums (21-9), Sample project (21-10).
- Add new toast features (QuickAction retry buttons, custom dismissal, `IsDismissable`, progress toasts, inverted color scheme, etc.) — the optional UX enhancement from the Sprint Change Proposal (enrich "Admin service unavailable" errors with `QuickAction1 = "Retry"`) is **DEFERRED to a follow-up story**. Rationale: 21-7's scope is pure API-level migration; adding retry UX is net-new behavior and expands surface area beyond what unblocks Epic 21's remaining stories. Keep this story surgical.
- Migrate the 2 × CS0246 `IDialogReference` residuals in `ProjectionDetailPanel.razor` — those are a service-pattern dialog issue inherited from 21-6 (Pause/Resume paths), not a toast issue. Out of scope.
- Touch `FluentToastProvider` registration — `AddFluentUIComponents()` in `AdminUIServiceExtensions.cs` already wires it and no v5 registration changes are needed.
- Migrate `DialogService.ShowConfirmationAsync` in `ProjectionDetailPanel.razor:369,401` — those are dialog-service calls (not toasts), already v5-compatible, untouched.

### V5 Toast API — Authoritative Reference (from MCP server, 2026-04-14)

**`IToastService.ShowToastAsync` (v5):**

```csharp
Task<ToastResult> ShowToastAsync(Action<ToastOptions> configure);
```

**`ToastOptions` (key properties for our use case):**

| Property | Type | Default | Use in this story |
|----------|------|---------|-------------------|
| `Title` | `string?` | `null` | `string.Empty` (we only pass body) |
| `Body` | `string?` | `null` | the single `message` parameter |
| `Intent` | `ToastIntent` | `Info` | `Success` / `Error` / `Warning` / `Info` per extension |
| `Timeout` | `int` (ms) | `7000` | Accept v5 default — do NOT override |
| `IsDismissable` | `bool` | `false` | Accept v5 default — user dismissal via timeout only |
| `Position` | `ToastPosition?` | `null` | Accept default position from `FluentToastProvider` |

**`ToastIntent` enum:** `Info`, `Success`, `Warning`, `Error`.

**Removed content components (v5):** `CommunicationToast`, `ConfirmationToast`, `ProgressToast` + `*ToastContent` types. Not used in this codebase — verified.

**Removed short-form methods:** `ShowSuccess(string)`, `ShowError(string)`, `ShowWarning(string)`, `ShowInfo(string)`, plus overloads taking `ToastParameters` etc. The v4 shortcuts are **entirely gone** in v5 — there is no compat shim.

### Title/Body mapping (AC 2)

The v4 `ShowSuccess("message")` API rendered the entire string as the toast body (no title). The v5 `ToastOptions` exposes separate `Title` and `Body` — populating both creates visual noise.

**Decision:** body-only mapping. The extension methods set `options.Title = string.Empty` and `options.Body = message`. Rendered output should match v4 (single-line message, no title bar).

**Risk (from Pre-mortem):** v5 may render `Title = ""` differently from `Title = null` — if the empty-string path emits a 20px empty header slot, this is a silent UX regression. **AC 21 explicitly verifies "no visible empty title bar."** If the visual check fails, change the extension to `options.Title = null` and re-verify.

**Compat-shim status (not final design):** these extensions are **v4-compatibility shims**, not the target UX. They exist to keep 106 call sites on a single-line idiom during the Epic 21 migration. A follow-up story will likely introduce overloads:
- `ShowSuccessAsync(string title, string body)` — two-slot toast
- `ShowSuccessAsync(ToastOptions options)` or `ShowSuccessAsync(Action<ToastOptions> configure)` — full-fidelity configuration
- Single-arg calls continue to work (body-only) for gradual migration.

Do NOT add these overloads in 21-7 — this story stays surgical.

### Extension Method Pattern (for Task 1)

```csharp
// <copyright file="ToastServiceExtensions.cs" company="Itaneo">
// Copyright (c) Itaneo. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.EventStore.Admin.UI.Services;

using System.Threading.Tasks;
using Microsoft.FluentUI.AspNetCore.Components;

/// <summary>
/// Provides legacy-compatible shortcut extensions for <see cref="IToastService"/> that target
/// the v5 <c>ShowToastAsync</c> pipeline. Mirrors the ergonomics of the v4 <c>Show*</c> helpers
/// with body-only messages and the v5 default timeout.
/// </summary>
public static class ToastServiceExtensions
{
    /// <summary>Shows a success toast with the given body message.</summary>
    public static Task ShowSuccessAsync(this IToastService toastService, string message)
        => toastService.ShowToastAsync(options =>
        {
            options.Intent = ToastIntent.Success;
            options.Title = string.Empty;
            options.Body = message;
        });

    /// <summary>Shows an error toast with the given body message.</summary>
    public static Task ShowErrorAsync(this IToastService toastService, string message)
        => toastService.ShowToastAsync(options =>
        {
            options.Intent = ToastIntent.Error;
            options.Title = string.Empty;
            options.Body = message;
        });

    /// <summary>Shows a warning toast with the given body message.</summary>
    public static Task ShowWarningAsync(this IToastService toastService, string message)
        => toastService.ShowToastAsync(options =>
        {
            options.Intent = ToastIntent.Warning;
            options.Title = string.Empty;
            options.Body = message;
        });

    /// <summary>Shows an info toast with the given body message.</summary>
    public static Task ShowInfoAsync(this IToastService toastService, string message)
        => toastService.ShowToastAsync(options =>
        {
            options.Intent = ToastIntent.Info;
            options.Title = string.Empty;
            options.Body = message;
        });
}
```

Notes:
- Return `Task` (NOT `Task<ToastResult>`) — callers never await on the result; simpler signature is better.
- Do NOT `await` inside the extensions — callers await at their call sites. Avoids double-`async` overhead and keeps stack frames minimal.
- XML doc comments are required (project has `TreatWarningsAsErrors = true`).
- Body-only mapping per §Title/Body mapping.

### Extension Method Test Pattern (for Task 4.2)

```csharp
// <copyright file="ToastServiceExtensionsTests.cs" company="Itaneo">
// Copyright (c) Itaneo. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

using System.Threading.Tasks;
using Hexalith.EventStore.Admin.UI.Services;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Shouldly;
using Xunit;

public class ToastServiceExtensionsTests
{
    [Fact]
    public async Task ShowSuccessAsync_CallsShowToastAsync_WithSuccessIntent()
    {
        // Arrange
        IToastService mockToast = Substitute.For<IToastService>();
        ToastOptions? capturedOptions = null;
        mockToast.ShowToastAsync(Arg.Do<Action<ToastOptions>>(configure =>
        {
            capturedOptions = new ToastOptions();
            configure(capturedOptions);
        })).Returns(Task.FromResult<ToastResult>(default!));

        // Act
        await mockToast.ShowSuccessAsync("hello");

        // Assert
        await mockToast.Received(1).ShowToastAsync(Arg.Any<Action<ToastOptions>>());
        capturedOptions.ShouldNotBeNull();
        capturedOptions.Intent.ShouldBe(ToastIntent.Success);
        capturedOptions.Body.ShouldBe("hello");
        capturedOptions.Title.ShouldBe(string.Empty);
    }

    // ... three more tests for Error / Warning / Info — same pattern
}
```

Notes:
- `Arg.Do<T>` captures the delegate and invokes it against a local `ToastOptions()` so we can assert on its final state.
- Verify `Intent`, `Body`, `Title` — the three fields the extension actually sets.
- Verify `Received(1)` to prove the extension doesn't accidentally swallow the call.

### V4 → V5 Migration Pattern (per-call)

**V4 pattern (current codebase):**
```csharp
ToastService.ShowSuccess("Snapshot policy created.");
ToastService.ShowError($"Request failed ({(int?)ex.StatusCode ?? 0}).");
```

**V5 pattern (target):**
```csharp
await ToastService.ShowSuccessAsync("Snapshot policy created.");
await ToastService.ShowErrorAsync($"Request failed ({(int?)ex.StatusCode ?? 0}).");
```

**Method-signature change (only if a sync handler contained toast calls):**
```csharp
// V4
private void OnRefreshSignal() => ToastService.ShowInfo("refreshed");

// V5 — option A: convert to async Task and propagate await up the chain
private async Task OnRefreshSignal() => await ToastService.ShowInfoAsync("refreshed");

// V5 — option B (if caller is a non-converable sync event, e.g., Timer.Elapsed in a framework):
private void OnRefreshSignal() => _ = ToastService.ShowInfoAsync("refreshed"); // fire-and-forget, DOCUMENT why
```

**Default:** Option A. Option B is a last resort and MUST be documented in Completion Notes with the specific caller and why async propagation was impossible.

**From the Task 0.4 pre-flight inventory:** no site in the current 106-call inventory requires Option B. All calling methods are already `async Task` or can be converted without breaking a non-convertable framework contract.

#### Projections.razor edge case (formerly AC 9)

`Pages/Projections.razor:239` calls `ToastService.ShowWarning("Projection no longer available")` from inside `LoadDataAsync` — which is already `async Task`. Reached via the `OnRefreshSignal(DashboardData)` pipeline. Nothing special here: await the extension normally. Noted explicitly because it's the only `ShowWarning` call in a data-reload path (not a user-confirm handler), so it's worth verifying in visual testing (Task 6) that the warning actually renders when a projection goes missing mid-poll.

### Call-Site Inventory (106 calls across 9 files)

| # | File | Toast calls | Intents | Enclosing method pattern |
|---|------|-------------|---------|--------------------------|
| 1 | `Layout/Breadcrumb.razor` | 2 | 1 Success, 1 Error | `CopyUrlAsync` (async Task ✓) |
| 2 | `Pages/Projections.razor` | 1 | 1 Warning | `LoadDataAsync` (async Task ✓) |
| 3 | `Pages/DeadLetters.razor` | 4 | mixed | `ExecuteBulkActionAsync` family (async Task ✓) |
| 4 | `Pages/Compaction.razor` | 6 | 1 Success, 5 Error | `OnTriggerCompactionConfirm` (verify in Task 0.4) |
| 5 | `Pages/Consistency.razor` | 11 | mixed | multiple async Task handlers |
| 6 | `Pages/Backups.razor` | 18 | mixed | 5 dialog-confirm handlers (async Task ✓) |
| 7 | `Pages/Tenants.razor` | 18 | mixed | 6 dialog-confirm handlers (async Task ✓ — OnCreateTenantConfirm, OnDisableConfirm, OnEnableConfirm, OnAddUserConfirm, OnRemoveUserConfirm, OnChangeRoleConfirm) |
| 8 | `Components/ProjectionDetailPanel.razor` | 22 | mixed | async Task handlers (ConfirmResetAsync, ConfirmReplayAsync, OnPauseClick, OnResumeClick, ExecuteOperationAsync, PollForUpdateAsync) |
| 9 | `Pages/Snapshots.razor` | 24 | mixed | 4 dialog-confirm handlers (async Task ✓) |
| **Total** | **9 files** | **106** | — | **All enclosing methods expected async Task (verify in Task 0.4)** |

### Project Structure Notes

- Target directory for the new file: `src/Hexalith.EventStore.Admin.UI/Services/` (matches existing pattern — `ThemeState.cs`, `ViewportService.cs`, `DashboardRefreshService.cs` all live here).
- `.editorconfig` conventions: file-scoped namespaces, Allman braces, `_camelCase` private fields, 4-space indent, CRLF. `TreatWarningsAsErrors = true` — XML doc comments required on public APIs.
- `IToastService` is already registered via `AddFluentUIComponents()` in `AdminUIServiceExtensions.cs:30` — no DI changes.
- `_Imports.razor`: check for `@using Hexalith.EventStore.Admin.UI.Services`. If missing, adding it once eliminates the need for per-file `@using` in all 9 `.razor` files. **Do NOT skip this step** — the extension methods won't resolve without it.

### Previous Story Intelligence (from 21-6)

**Learnings from Story 21-6 that apply here:**

1. **Build-error ceiling approach:** Record PRE and POST error counts; attribute every residual to a downstream story. In 21-6, after completion, 69 × CS1061 on toast methods were explicitly flagged as "Story 21-7 will fix." This story must drop the Admin.UI error count by at least 69.

2. **async void is NEVER acceptable** for Blazor event handlers. Always use `async Task`. Compiler won't catch this; runtime swallows exceptions silently.

3. **Zero-own-surface rule:** Zero residual errors may trace back to the 9 files this story modifies. Every residual after 21-7 completion must be attributable to 21-8 (CSS), 21-9 (DataGrid), 21-10 (Sample), or the pre-existing 2 × CS0246 `IDialogReference` residuals from 21-6.

4. **Visual verification is the gate for 21-6 AC 30 AND 21-4 AC 22.** Both stories explicitly deferred their visual checks with a note "waiting on 21-7 toast migration." Completing Task 6 unblocks and verifies BOTH. Consider cross-linking in Completion Notes.

5. **Multi-line Razor attributes and methods:** Read several lines around each grep hit; don't trust single-line matches. From 21-5 and 21-6 experience, some method bodies span 20+ lines with toast calls in catch blocks.

6. **`@inject IToastService ToastService` is already present in all 9 files** — confirmed by 21-6 audit. No `@inject` changes needed.

### Previous Story Intelligence (from 21-5)

- Commit patterns: one focused commit per file (or per logical migration group) with clear conventional-commit message `feat(ui): migrate Admin.UI toast API to v5 (Story 21-7)`.
- PR description template: summary + acceptance criteria status + grep-for-zero evidence + build PRE/POST counts.

### Known v5 gotchas (toast-specific)

1. **`ShowToastAsync` returns `Task<ToastResult>`, not `Task`.** The extensions return `Task` for simplicity — calling `await ShowSuccessAsync(...)` drops the `ToastResult`. This is intentional and matches v4 semantics. If a caller ever needs `ToastResult`, they can call `ShowToastAsync` directly.

2. **`ToastOptions.Title` is not nullable in all builds — confirm with the v5 assembly you're targeting.** If `Title` validation throws on null, the body-only mapping must use `string.Empty` (as shown) not `null`.

3. **`ToastPosition` / `ToastProvider` defaults.** v5 introduces a `FluentToastProvider` component (part of `FluentProviders` — already migrated in 21-2). If toasts don't appear, verify `FluentToastProvider` is in the component tree (check `MainLayout.razor` or `App.razor`). Do not add it in this story if missing — flag as a blocker and escalate.

4. **Timeout default changed between v4 (5000ms) and v5 (7000ms).** UX difference is minor; accepting the v5 default is fine. Do NOT hard-code 5000ms to match v4 — user feedback has not flagged the timeout as an issue.

5. **`ShowToastAsync` with a configure delegate executes the delegate immediately** on call, not at render time. Any side effects in the configure delegate run on the caller's thread. Keep the delegate pure (only set `options.X = ...`).

6. **`_Imports.razor` resolution race.** If tooling shows "extension method not found" errors in .razor files despite the extension existing and `_Imports.razor` being correct, restart the IDE / run `dotnet build --no-restore`. Blazor's Razor compiler occasionally caches old symbol tables.

7. **NSubstitute on `IToastService` + extension methods.** Extension methods call `ShowToastAsync` on the substitute — the substitute intercepts the underlying interface call, not the extension. Tests must assert on `ShowToastAsync`, not `ShowSuccessAsync`. This is a common gotcha when migrating tests; see §Extension Method Test Pattern.

8. **`await ShowToastAsync` may deadlock in sync contexts** if called from `.Result` or `.Wait()`. All 106 current sites are in async methods — safe. If a new caller in a sync context ever appears, use `_ = ShowSuccessAsync(...)` fire-and-forget (Option B in the migration pattern) and document.

### Why this story exists (motivation & blast radius)

The v4 `IToastService.ShowSuccess/ShowError/ShowWarning/ShowInfo` shortcut methods were **entirely removed** in Fluent UI Blazor v5. There is no compat shim. Admin.UI today has 106 calls to these removed methods — every one is a compile error. 21-6 (dialog restructure) completed successfully and left all 69 × CS1061 toast-errors (grouped across the 106 call sites) as "21-7's problem" per the explicit Epic 21 execution order. Until this story lands:
- Admin.UI does not compile.
- No new UI work (21-8 / 21-9 / 21-10) can proceed.
- 21-6 AC 30 (visual verification of 28 dialogs) cannot run.
- 21-4 AC 22 (badge contrast) cannot run.

Scope is intentionally narrow: **migrate the API, nothing else.** Adding the optional QuickAction retry UX from the Sprint Change Proposal would double the risk surface and delay green-build — defer it.

### Architecture Decision Records

**ADR-21-7-001: Extension methods vs. service wrapper**

| Option | Description | Risk | Decision |
|--------|-------------|------|----------|
| A: Extension methods | 4 `public static Task Show*Async` on `IToastService` | **Low** — minimal code, no DI changes | **Selected** |
| B: Service wrapper | New `IToastHelper` service + impl | **Medium** — extra DI, surface area to maintain | Rejected — net-negative |
| C: Inline `ShowToastAsync(opts => ...)` at every call | 106 lambdas inline | **Medium** — 106 lines of duplication, regressions likely | Rejected — violates DRY |

**ADR-21-7-002: Title/Body mapping**

| Option | Description | Decision |
|--------|-------------|----------|
| A: Body-only (`title = string.Empty`, `body = message`) | Matches v4 rendering | **Selected** |
| B: Title-only (`title = message`, `body = null`) | Shorter toasts but different visual | Rejected — UX regression |
| C: Split on sentence boundary | Heuristic title + body | Rejected — fragile + over-engineered |

**ADR-21-7-003: Defer QuickAction retry enhancement**

| Option | Description | Decision |
|--------|-------------|----------|
| A: Include retry buttons in this story | Net-new UX | Rejected — scope creep |
| B: Defer to a follow-up story | Keep 21-7 surgical | **Selected** |

**ADR-21-7-004: Extensions as exit-ramp to future `IAdminToastService`**

Self-consistency validation (Winston's design): if a future story needs to swap toast providers — e.g., route success toasts through SignalR to other connected clients, or introduce a project-owned notification center — the right abstraction is a project-owned `IAdminToastService` interface registered in DI. This story deliberately does NOT create that interface (YAGNI; no current driver).

**Why it still matters:** the 4 extension methods wrap `ShowToastAsync` on a single line each. When the driver appears, the migration is trivially mechanical — replace `public static Task ShowSuccessAsync(this IToastService ...)` with `Task IAdminToastService.ShowSuccessAsync(...)` as an instance method on the new implementation, swap the `@inject IToastService` to `@inject IAdminToastService` in 9 files, done. The 106 call sites stay untouched.

**Do not pre-build this seam in 21-7.** The extension-method shape is already a seam.

### Visual Verification Protocol (spot-check matrix for AC 21)

| Page | Trigger | Expected intent | Verify |
|------|---------|-----------------|--------|
| Tenants | Create Tenant with valid id | Success | green toast, "created successfully" body |
| Tenants | Create Tenant with invalid id | Error | red toast, error message body |
| Backups | Create Backup | Success | green toast, "backup started" body |
| Backups | Create Backup after killing admin API | Error | red toast, "Admin service unavailable" body |
| Snapshots | Create Policy | Success | green toast, "Snapshot policy created" body |
| Projections | Projection goes missing mid-poll | Warning | yellow toast, "Projection no longer available" body |
| Breadcrumb | Copy URL (success) | Success | green toast, "Link copied to clipboard" body |

Cross-cutting checks per page: light mode, dark mode, auto-dismiss after 7s.

### References

- [Source: MCP `get_component_migration("FluentToast")` — 2026-04-14]
- [Source: MCP `get_component_details("FluentToast")` — 2026-04-14]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-13-fluent-ui-v5-migration.md` §Story 21-7 Toast API Update]
- [Source: `_bmad-output/implementation-artifacts/21-6-dialog-restructure.md` §Debug Log References — 69 × CS1061 Toast errors attributed here]
- [Source: `_bmad-output/implementation-artifacts/21-5-component-renames.md` §Dev Notes (pattern for methodical find/replace at scale)]
- [Source: `CLAUDE.md` §Code Style (file-scoped namespace, Allman, `_camelCase`, XML docs required, `TreatWarningsAsErrors`)]
- [Source: user memory `feedback_restart_procedure.md` — flush Redis → build → `aspire run` before visual verification]
- [Source: user memory `project_epic_21_migration_compile_chain.md` — Admin.UI does not compile until 21-7 lands]

## Dev Agent Record

### Agent Model Used

claude-opus-4-6 (1M context) — Amelia (bmad-agent-dev)

### Debug Log References

- PRE Admin.UI build (baseline): 148 errors total — CS1061=212 (all `IToastService.Show*`), CS0103=78 (21-9 DataGrid enums), CS0246=4 (`IDialogReference` × 2 × 2 passes), RZ9986=2 (21-8 Class mixed-content).
- POST Admin.UI build: 42 errors total — CS0103=78, CS0246=4, RZ9986=2. CS1061 on toast methods eliminated. Δ=−106 (exact call-site count).
- Full slnx POST: 60 errors — adds 16 CS0618 + 8 CS1503 from Sample.BlazorUI (21-10 scope).
- Tier 1 tests: 753/753 green (Contracts 271, Client 321, Sample 62, Testing 67, SignalR 32).
- Cold-cache rehearsal: 42 errors, matches warm cache — no `_Imports.razor` race.

### Completion Notes List

- ✅ Created `Services/ToastServiceExtensions.cs` with 4 extension methods (ShowSuccessAsync/ShowErrorAsync/ShowWarningAsync/ShowInfoAsync). Each sets `Title=string.Empty` + `Body=message` + correct `ToastIntent`. Added `ArgumentNullException.ThrowIfNull(toastService)` to satisfy CA1062 under `TreatWarningsAsErrors`.
- ✅ Migrated 106 v4 `ToastService.ShowX(...)` call-sites → `await ToastService.ShowXAsync(...)` across 9 files. Zero method-signature changes required (all enclosing methods were already `async Task`).
- ✅ Distribution verified: ShowSuccess=23, ShowError=79, ShowWarning=3, ShowInfo=1 (PRE) → identical count of `*Async` successors (POST). Net delta: **0** (no net add/remove).
- ✅ `_Imports.razor` left untouched — all 9 target `.razor` files already declare `@using Hexalith.EventStore.Admin.UI.Services` locally.
- ✅ Added `tests/Hexalith.EventStore.Admin.UI.Tests/Services/ToastServiceExtensionsTests.cs` with 6 tests covering 4 intents + null-message handling + exception propagation via NSubstitute `Arg.Do<Action<ToastOptions>>` capture pattern. Test project compiles clean; execution blocked only by upstream 21-9 DataGrid residuals.
- ✅ Build gates: Admin.UI dropped 148 → 42 errors (Δ=−106 matches call-site count exactly). Zero residuals trace to toast-modified surface. Every residual attributable to 21-8 (CSS), 21-9 (DataGrid), 21-10 (Sample.BlazorUI), or pre-existing 21-6 `IDialogReference` defer.
- ✅ Tier 1 regression suite: 753/753 green.
- ⏳ **Task 6 visual verification DEFERRED-TO-21-8-OR-21-9-LAND.** AC 21 precondition "Admin.UI compiles cleanly" cannot be satisfied until 21-8 + 21-9 land. Same pattern as 21-4 AC 22 / 21-6 AC 30. Rolled-in scope (21-6 AC 30 dialog sweep + 21-4 AC 22 badge contrast) also deferred to the same visual-sweep session.
- 📝 **Deviation from spec:** story inventory estimated "~76 PRE baseline". Actual PRE=148. Reason: razor source-generator reports each CS1061 twice (once per compile pass). Real call-site count (106) and ShowError/ShowSuccess/ShowWarning/ShowInfo breakdown match §Call-Site Inventory exactly.
- 📝 **Deviation from spec:** CA1062 added null-guard `ArgumentNullException.ThrowIfNull(toastService)` to each extension — not mentioned in §Extension Method Pattern but required by project's `TreatWarningsAsErrors`. Preserves semantic equivalence with spec (body-only, v5 default timeout).

### File List

**New files (2):**
- `src/Hexalith.EventStore.Admin.UI/Services/ToastServiceExtensions.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/ToastServiceExtensionsTests.cs`

**Modified files (9):**
- `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` — 2 calls migrated
- `src/Hexalith.EventStore.Admin.UI/Pages/Projections.razor` — 1 call migrated
- `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor` — 4 calls migrated
- `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor` — 6 calls migrated
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor` — 11 calls migrated
- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor` — 18 calls migrated
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor` — 18 calls migrated
- `src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor` — 22 calls migrated
- `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` — 24 calls migrated

**Sprint tracking:**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — status update (ready-for-dev → in-progress → review)
- `_bmad-output/implementation-artifacts/21-7-toast-api-update.md` — this file

## Change Log

| Date | Change | Details |
|------|--------|---------|
| 2026-04-14 | Story created | Toast v4→v5 migration spec with 106 call-site inventory across 9 files |
| 2026-04-14 | Implementation complete | 4 extension helpers + 106 call-site migrations + 6 bUnit tests; Admin.UI errors 148→42 (Δ=−106); zero own-surface residuals; Tier 1 suite 753/753 green. Visual verification (AC 21/22) DEFERRED-TO-21-8-OR-21-9-LAND |
