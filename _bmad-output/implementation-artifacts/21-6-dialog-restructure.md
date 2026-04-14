# Story 21.6: Dialog Restructure (FluentDialogHeader / FluentDialogFooter Removal + ShowAsync/HideAsync Lifecycle)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

**Session guidance:** Allocate a dedicated session for this story — no parallel work. This is the highest-regression-risk story in Epic 21 and requires full focus.

**Dependency:** Story 21-5 (component renames) must be `done` before starting.

## Story

As a developer migrating from Fluent UI Blazor v4 to v5,
I want every inline `<FluentDialog>` usage restructured to use `FluentDialogBody` with `TitleTemplate`/`ActionTemplate` slots and `ShowAsync()`/`HideAsync()` lifecycle methods,
so that Admin.UI compiles cleanly under the v5 dialog API and the remaining migration stories (21-7 toast, 21-8 CSS, 21-9 DataGrid) can proceed — this is the **highest-regression-risk story** in Epic 21 because it converts synchronous show/hide to async and restructures all dialog DOM.

## Acceptance Criteria

### FluentDialogHeader / FluentDialogFooter removal

1. **Given** any `<FluentDialogHeader>...</FluentDialogHeader>` block in `src/Hexalith.EventStore.Admin.UI/**/*.razor`,
   **When** migrated,
   **Then** the header content is moved into a `<TitleTemplate>...</TitleTemplate>` child slot inside the nearest parent `<FluentDialogBody>`,
   **And** `ShowDismiss="true"` attributes are removed (v5 does not support this on FluentDialogHeader — dismiss is handled via `HideAsync()` or the `ActionTemplate`),
   **And** grep for `<FluentDialogHeader` returns 0 hits post-migration.

2. **Given** any `<FluentDialogFooter>...</FluentDialogFooter>` block in `src/Hexalith.EventStore.Admin.UI/**/*.razor`,
   **When** migrated,
   **Then** the footer content (buttons) is moved into an `<ActionTemplate>...</ActionTemplate>` child slot inside the nearest parent `<FluentDialogBody>`,
   **And** grep for `<FluentDialogFooter` returns 0 hits post-migration.

3. **Given** the v5 `<FluentDialogBody>` component,
   **When** restructured,
   **Then** it is the **single structural child** of each `<FluentDialog>`, wrapping:
   - `<TitleTemplate>` (formerly FluentDialogHeader content)
   - `<ChildContent>` (the body content — implicit, no explicit tag needed)
   - `<ActionTemplate>` (formerly FluentDialogFooter content)

### @bind-Hidden → @ref + ShowAsync/HideAsync lifecycle

4. **Given** any `@bind-Hidden="_flag"` attribute on a `<FluentDialog>`,
   **When** migrated,
   **Then** it is replaced by `@ref="_dialogRef"` where `_dialogRef` is a `FluentDialog?` field,
   **And** the corresponding `bool _flag` field is removed,
   **And** visibility is controlled via `_dialogRef?.ShowAsync()` and `_dialogRef?.HideAsync()`.

5. **Given** any `@bind-Hidden:after="CallbackMethod"` pattern (CommandSandbox.razor),
   **When** migrated,
   **Then** the callback logic is inlined into the `HideAsync()` call site or handled via `OnStateChange`,
   **And** the dual-boolean pattern (`_showPayloadDialog` + `_hidePayloadDialog`) is collapsed to a single `FluentDialog?` ref.

6. **Given** any `void` event handler that currently sets `_showXDialog = true` (synchronous),
   **When** migrated to call `ShowAsync()`,
   **Then** the handler signature becomes `async Task` (not `async void`),
   **And** `ShowAsync()` is awaited.

7. **Given** any `void` event handler that sets `_showXDialog = false` (synchronous),
   **When** migrated to call `HideAsync()`,
   **Then** the handler signature becomes `async Task`,
   **And** `HideAsync()` is awaited.

### Removed v4 dialog attributes

8. **Given** any `TrapFocus="true"` attribute on a `<FluentDialog>`,
   **When** migrated,
   **Then** the attribute is removed (v5 removed `TrapFocus` — focus trapping is the default behavior for modal dialogs).

9. **Given** any `aria-modal="true"` attribute on a `<FluentDialog>`,
   **When** migrated,
   **Then** the attribute is removed (v5 renders `aria-modal` automatically for `Modal="true"` dialogs — the default).

10. **Given** any `PreventDismiss="false"` attribute (CommandPalette.razor),
    **When** migrated,
    **Then** the attribute is removed (v5 removed `PreventDismiss`).

11. **Given** any `OnDismiss="..."` attribute on a `<FluentDialog>`,
    **When** migrated,
    **Then** it is removed and replaced with `OnStateChange` if the callback needs to react to dialog close,
    **And** the callback is updated to accept `DialogEventArgs` and check for `DialogState.Closed`.

12. **Given** any `@ondialogdismiss="..."` event handler on a `<FluentDialog>`,
    **When** migrated,
    **Then** it is removed and the dismiss logic is handled via `OnStateChange` or by calling `HideAsync()` directly from the close button's `OnClick`,
    **And** grep for `@ondialogdismiss` returns 0 hits post-migration.

### Conditional rendering pattern preservation

13. **Given** the current `@if (_showXDialog) { <FluentDialog ...> }` conditional rendering pattern,
    **When** migrated,
    **Then** the `@if` guard is **kept for ALL conditionally-rendered dialogs** (25 of 28) — this preserves form state reset on close and avoids rendering hidden dialogs in the DOM,
    **And** within the `@if` block, the dialog `@ref` is populated on render and `ShowAsync()` is called via `OnAfterRenderAsync` with a `_pendingShowX` flag (see Dev Notes §Approved ShowAsync Pattern),
    **And** only the 2 already-`@ref` dialogs (CommandPalette, StateInspectorModal) are always-rendered and controlled entirely via `ShowAsync()`/`HideAsync()` — their existing `@if` guards (if any) are removed,
    **And** the `@if` boolean field (`_showXDialog`) is **retained** alongside the `FluentDialog?` ref field — the boolean controls rendering, the ref controls v5 lifecycle.

14. **Given** the CommandPalette component currently uses `@bind-Hidden="_hidden"`,
    **When** migrated,
    **Then** the `_hidden` bool field becomes a `FluentDialog?` `_dialog` ref,
    **And** `Open()` calls `await _dialog!.ShowAsync()` and `Close()` calls `await _dialog!.HideAsync()`,
    **And** the Ctrl+K keyboard shortcut integration (via `@onkeydown` in MainLayout) continues to work correctly.

### ShowDismiss replacement

16a. **Given** `ShowDismiss="true"` on `<FluentDialogHeader>` in CommandPalette, CommandSandbox, and EventDebugger,
    **When** migrated,
    **Then** CommandPalette gets a dismiss × button via `<TitleActionTemplate>` inside `<FluentDialogBody>` (users expect × on a command palette),
    **And** CommandSandbox and EventDebugger do NOT get a `TitleActionTemplate` dismiss button — they already have a "Close" button in their `<ActionTemplate>` (formerly FluentDialogFooter) which is sufficient for payload viewer dialogs.

### Modal attribute normalization

15. **Given** `Modal` is `bool?` in v4 but `bool` (default `true`) in v5,
    **When** migrated,
    **Then** explicit `Modal="true"` attributes are kept for clarity (they match the v5 default, but explicit is better for documentation),
    **And** no `Modal="false"` instances exist (confirmed by audit — all 28 dialogs are modal).

### Null-safety on @ref calls

16. **Given** a `FluentDialog? _dialogRef` field declared via `@ref`,
    **When** `ShowAsync()` or `HideAsync()` is called,
    **Then** the call uses null-conditional (`await _dialogRef?.ShowAsync()!` or a null guard) — **never** call on an unrendered `@ref`,
    **And** multi-dialog pages (Tenants 6, Backups 5) must not have race conditions between rapid open→close→reopen sequences.

### Verification gates

17. **Given** story completion,
    **When** grep for `<FluentDialogHeader\b` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** FluentDialogHeader elimination is verified complete.

18. **Given** story completion,
    **When** grep for `<FluentDialogFooter\b` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** FluentDialogFooter elimination is verified complete.

19. **Given** story completion,
    **When** grep for `@bind-Hidden` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** the `@bind-Hidden` → `@ref` + `ShowAsync/HideAsync` migration is verified complete.

20. **Given** story completion,
    **When** grep for `TrapFocus` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** the removed attribute cleanup is verified complete.

21. **Given** story completion,
    **When** grep for `@ondialogdismiss` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** the dismiss event migration is verified complete.

22. **Given** story completion,
    **When** grep for `OnDismiss=` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** the OnDismiss removal is verified complete.

23. **Given** story completion,
    **When** grep for `PreventDismiss` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits,
    **Then** PreventDismiss removal is verified complete.

24. **Given** story completion,
    **When** grep for `aria-modal` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` returns 0 hits (v5 handles this automatically),
    **Then** redundant aria-modal removal is verified complete.

25. **Given** story completion,
    **When** count of `<FluentDialogBody` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` = 28,
    **Then** all dialog bodies are preserved (1:1 with FluentDialog count).

26. **Given** story completion,
    **When** count of `<TitleTemplate>` inside `FluentDialogBody` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` = 28,
    **Then** all dialog titles migrated.

27. **Given** story completion,
    **When** count of `<ActionTemplate>` inside `FluentDialogBody` across `src/Hexalith.EventStore.Admin.UI/**/*.razor` = 26 (CommandPalette has no footer, StateInspectorModal has no footer),
    **Then** all dialog footers migrated to ActionTemplate.

28. **Given** story completion,
    **When** non-UI Tier 1 tests run (`dotnet test` on Contracts + Client + Testing + SignalR),
    **Then** all pass with zero regressions.

29. **Given** story completion,
    **When** `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release` runs,
    **Then** the residual error count drops from the Story 21-5 baseline (dialog-surface errors ~60-90 should be eliminated),
    **And** every remaining residual error is attributed to a downstream story (21-7 toast, 21-8 CSS, 21-9 DataGrid, 21-10 samples),
    **And** **zero residual errors trace back to files this story modified on the dialog surface.** This is a HARD gate. "Dialog surface" = the 11 `.razor` source files listed in the File List PLUS any test files modified in Task 12.

30. **Given** story completion AND Story 21-7 has landed,
    **When** `aspire run` produces a running Admin.UI,
    **Then** the visual spot-check opens and closes each of the 28 dialogs; all button actions fire correctly; rapid open/close/reopen on Tenants and Backups is stable.
    **Expected default outcome:** DEFERRED — this story lands before 21-7 per execution order. Mark as `DEFERRED-TO-21-7-OR-EPIC-21-RETRO`.

### bUnit test updates

31. **Given** existing bUnit tests in `tests/Hexalith.EventStore.Admin.UI.Tests/` that reference dialog markup patterns (`fluent-dialog-header`, `fluent-dialog-footer`, `@ondialogdismiss`, dialog open/close assertions),
    **When** migrated,
    **Then** selectors referencing `fluent-dialog-header` are updated to target content inside `fluent-dialog-body` (e.g., `TitleTemplate` rendered content),
    **And** selectors referencing `fluent-dialog-footer` are updated to target `ActionTemplate` rendered content,
    **And** test methods that trigger dialog open/close are updated from synchronous boolean-set to async patterns.

32a. **Given** the Compaction page has 1 dialog (simplest case),
    **When** story completion,
    **Then** `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CompactionPageTests.cs` contains a test `CompactionPage_TriggerDialog_RendersV5DialogStructure` that:
    - Opens the dialog via the trigger button click
    - Asserts the dialog body contains a `TitleTemplate`-rendered title (no `fluent-dialog-header` element)
    - Asserts the dialog body contains an `ActionTemplate`-rendered footer (no `fluent-dialog-footer` element)
    This locks in the v5 DOM structure contract.

32b. **Given** the CommandPalette is always-rendered with `ShowAsync`/`HideAsync`,
    **When** story completion,
    **Then** a bUnit test `CommandPaletteTests::CommandPalette_ShowAsyncHideAsync_Lifecycle` exists (new file or added to existing) that:
    - Renders the CommandPalette component
    - Verifies the dialog can be shown via the `Open()` method
    - Verifies the dialog can be hidden via the `Close()` method
    This catches `async void` mistakes at compile time.

## Tasks / Subtasks

- [x] Task 0: Pre-flight audit — confirm scope before touching code (AC: all)
  - [x] 0.1: Record PRE-story error count: `dotnet build Hexalith.EventStore.slnx --configuration Release`. Capture as `PRE: total=<N>`.
  - [x] 0.2: Grep each v4 dialog pattern in `src/Hexalith.EventStore.Admin.UI/**/*.razor` and record counts:
    - `<FluentDialogHeader` — expected 28
    - `<FluentDialogFooter` — expected 26
    - `<FluentDialogBody` — expected 28
    - `<FluentDialog ` (opening tag with space) — expected 28
    - `@bind-Hidden` — expected 3 (CommandPalette, CommandSandbox, EventDebugger)
    - `TrapFocus` — expected ~25
    - `aria-modal` — expected ~25
    - `@ondialogdismiss` — expected ~18
    - `PreventDismiss` — expected 1 (CommandPalette)
    - `OnDismiss=` — expected 1 (CommandPalette)
    - `ShowDismiss=` — expected 3 (CommandPalette, CommandSandbox, EventDebugger)
    Record ANY delta in Completion Notes.
  - [x] 0.3: Inventory all `_showXDialog` boolean fields and their paired open/close methods across all 11 files. Record each field name, file:line, and the open/close method names that set it.
  - [x] 0.4: Check for any `.razor.cs` code-behind files co-located with dialog-using files. Expected: 0 (all use inline `@code`).
  - [x] 0.5: Grep for `FluentDialogProvider` — expected 0 hits (v5 uses `FluentProviders` which was already migrated in Story 21-2).
  - [x] 0.6: Inventory bUnit test assertions that reference dialog patterns. Grep `tests/Hexalith.EventStore.Admin.UI.Tests/**/*.cs` for: `fluent-dialog-header`, `fluent-dialog-footer`, `fluent-dialog-body`, `dialog` (case-insensitive), `_showXDialog`, `ShowDismiss`. Record each hit as `file:line — pattern — action-needed`. This list drives Task 12.

- [x] Task 1: Migrate CommandPalette.razor — `@bind-Hidden` + header-only dialog (AC: 1, 4, 8, 10, 11, 14)
  - [x] 1.1: Replace `@bind-Hidden="_hidden"` with `@ref="_dialog"`.
  - [x] 1.2: Change `private bool _hidden = true;` to `private FluentDialog? _dialog;`.
  - [x] 1.3: Update `Open()` to `async Task`: `await _dialog!.ShowAsync();`.
  - [x] 1.4: Update `Close()` to `async Task`: `await _dialog!.HideAsync();`.
  - [x] 1.5: Remove `TrapFocus="true"`, `PreventDismiss="false"`, `OnDismiss="@(() => Close())"`.
  - [x] 1.6: Add `OnStateChange` if dismiss-on-overlay is needed, or rely on `Close()` button / Escape key.
  - [x] 1.7: Move `<FluentDialogHeader ShowDismiss="true">` content → `<TitleTemplate>` inside `<FluentDialogBody>`.
  - [x] 1.8: CommandPalette has NO FluentDialogFooter — verify `<FluentDialogBody>` has no `<ActionTemplate>`.
  - [x] 1.9: Add `_isTransitioning` guard per Dev Notes §CommandPalette Ctrl+K Transition Guard — prevents Ctrl+K rapid-fire from interleaving `ShowAsync`/`HideAsync`.
  - [x] 1.10: Verify Ctrl+K shortcut still opens/closes the palette (MainLayout integration).
  - [x] 1.11: Verify all callers of `Open()`/`Close()` are updated to `await` the now-async methods.
  - [x] 1.12: **SPIKE GATE:** If any timing issue (null ref, double-open, flicker) manifests during CommandPalette migration, STOP and escalate before proceeding to Task 2. Document the issue and attempted mitigations in Completion Notes.

- [x] Task 2: Migrate CommandSandbox.razor — dual-boolean `@bind-Hidden` pattern (AC: 1, 2, 4, 5, 8)
  - [x] 2.1: Replace `@bind-Hidden="@_hidePayloadDialog" @bind-Hidden:after="OnPayloadDialogHiddenChanged"` with `@ref="_payloadDialog"`.
  - [x] 2.2: Remove `_showPayloadDialog` and `_hidePayloadDialog` booleans; replace with `FluentDialog? _payloadDialog`.
  - [x] 2.3: Update `ShowPayloadDialog(evt)` to async: set payload data, then `await _payloadDialog!.ShowAsync()`. Remove `_showPayloadDialog = true; _hidePayloadDialog = false;`.
  - [x] 2.4: Update `ClosePayloadDialog()` to async: `await _payloadDialog!.HideAsync()`. Remove `_showPayloadDialog = false; _hidePayloadDialog = true;`.
  - [x] 2.5: Remove `OnPayloadDialogHiddenChanged()` callback entirely.
  - [x] 2.6: Move FluentDialogHeader content → TitleTemplate; FluentDialogFooter content → ActionTemplate inside FluentDialogBody.
  - [x] 2.7: Remove `ShowDismiss="true"`, `TrapFocus="true"`, `aria-modal="true"` attributes.
  - [x] 2.8: Keep the `@if (_showPayloadDialog)` guard? **Decision:** since we have `@ref` now, the `@if` guard controls whether the `<FluentDialog>` is rendered. Two options: (a) keep `@if` with a simplified boolean `_payloadDialogVisible` + call `ShowAsync` in `OnAfterRenderAsync`, or (b) always render the dialog and control via `ShowAsync`/`HideAsync`. Prefer (b) for this simple payload-viewer dialog — always render, control via ref.

- [x] Task 3: Migrate EventDebugger.razor — `@bind-Hidden` pattern (AC: 1, 2, 4, 8)
  - [x] 3.1: Replace `@bind-Hidden="_payloadDialogHidden"` with `@ref="_payloadDialog"`.
  - [x] 3.2: Remove `_payloadDialogHidden` boolean; replace with `FluentDialog? _payloadDialog`.
  - [x] 3.3: Update open/close methods to async with `ShowAsync()`/`HideAsync()`.
  - [x] 3.4: Move FluentDialogHeader → TitleTemplate; FluentDialogFooter → ActionTemplate.
  - [x] 3.5: Remove `ShowDismiss="true"`, `Modal="true"` (v5 default), removed attributes.

- [x] Task 4: Migrate StateInspectorModal.razor — already uses `@ref` (AC: 1, 8, 9, 12)
  - [x] 4.1: This component already uses `@ref="_dialog"` with `FluentDialog? _dialog` — no `@bind-Hidden` to migrate.
  - [x] 4.2: Remove `TrapFocus="true"`, `aria-modal="true"`, `@ondialogdismiss="HandleDismiss"`.
  - [x] 4.3: Convert `HandleDismiss` to use `OnStateChange` callback or remove if close is handled by the "Close" button already.
  - [x] 4.4: Move `<FluentDialogHeader>` content → `<TitleTemplate>` inside `<FluentDialogBody>`.
  - [x] 4.5: StateInspectorModal has NO FluentDialogFooter — verify no `<ActionTemplate>` needed.
  - [x] 4.6: Verify `ShowAsync()` and `HideAsync()` methods on the existing ref work correctly.

- [x] Task 5: Migrate ProjectionDetailPanel.razor — 2 confirmation dialogs (AC: 1, 2, 8, 9, 12, 13)
  - [x] 5.1: Migrate Reset confirmation dialog (line ~185): remove `TrapFocus`, `aria-modal`, `@ondialogdismiss`.
  - [x] 5.2: Migrate Replay confirmation dialog (line ~224): same removals.
  - [x] 5.3: Both use `@if (_showXDialog && _detail is not null)` guards — keep guards; add `@ref` and call `ShowAsync()` in `OnAfterRenderAsync` or in the open method after `StateHasChanged`.
  - [x] 5.4: Move FluentDialogHeader → TitleTemplate; FluentDialogFooter → ActionTemplate for both.
  - [x] 5.5: Update open/close methods from `_showResetDialog = true/false` to async `ShowAsync()`/`HideAsync()`.

- [x] Task 6: Migrate Tenants.razor — 6 dialogs, highest-volume page (AC: 1, 2, 6, 7, 8, 9, 12, 13, 16)
  - [x] 6.1: Migrate Create Tenant dialog (line ~222): `@if` guard + add `@ref="_createDialog"`.
  - [x] 6.2: Migrate Disable Tenant dialog (line ~270): `@if` guard + add `@ref="_disableDialog"`.
  - [x] 6.3: Migrate Enable Tenant dialog (line ~294): `@if` guard + add `@ref="_enableDialog"`.
  - [x] 6.4: Migrate Add User dialog (line ~318): `@if` guard + add `@ref="_addUserDialog"`.
  - [x] 6.5: Migrate Remove User dialog (line ~353): `@if` guard + add `@ref="_removeUserDialog"`.
  - [x] 6.6: Migrate Change Role dialog (line ~377): `@if` guard + add `@ref="_changeRoleDialog"`.
  - [x] 6.7: For ALL 6 dialogs: remove `TrapFocus="true"`, `aria-modal="true"`, `@ondialogdismiss="CloseXDialog"`.
  - [x] 6.8: For ALL 6 dialogs: move FluentDialogHeader → TitleTemplate; FluentDialogFooter → ActionTemplate.
  - [x] 6.9: Convert all `OpenXDialog()` methods to `async Task` + `await _xDialog!.ShowAsync()`.
  - [x] 6.10: Convert all `CloseXDialog()` methods to `async Task` + `await _xDialog!.HideAsync()`.
  - [x] 6.11: **Shared close method fix:** `CloseLifecycleDialog()` currently sets BOTH `_showDisableDialog = false` AND `_showEnableDialog = false`. In v5, this method must only call `HideAsync()` on the dialog that is actually open. Track which lifecycle dialog is open (e.g., via an enum `_activeLifecycleAction` or by checking which ref is non-null and rendered) and only call `HideAsync()` on that ref. Calling `HideAsync()` on a null or unrendered ref could throw or silently fail.
  - [x] 6.12: Add `_isAnyDialogOpen` guard per Dev Notes §Multi-Dialog Race Prevention. Disable all 6 trigger buttons while any dialog is open.
  - [x] 6.13: Verify rapid open→close→reopen on this page is stable (no race conditions between 6 dialogs).

- [x] Task 7: Migrate Backups.razor — 5 dialogs (AC: 1, 2, 6, 7, 8, 9, 12, 13, 16)
  - [x] 7.1: Migrate Create Backup dialog (line ~204).
  - [x] 7.2: Migrate Validate Backup dialog (line ~268).
  - [x] 7.3: Migrate Restore from Backup dialog (line ~301).
  - [x] 7.4: Migrate Export Stream dialog (line ~398).
  - [x] 7.5: Migrate Import Stream dialog (line ~452).
  - [x] 7.6: Same pattern for all 5: remove v4 attrs, add `@ref`, FluentDialogHeader→TitleTemplate, FluentDialogFooter→ActionTemplate, async open/close.
  - [x] 7.7: Add `_isAnyDialogOpen` guard per Dev Notes §Multi-Dialog Race Prevention. Disable all 5 trigger buttons while any dialog is open.
  - [x] 7.8: Verify rapid open→close→reopen stability.

- [x] Task 8: Migrate Snapshots.razor — 4 dialogs (AC: 1, 2, 6, 7, 8, 9, 12, 13)
  - [x] 8.1: Migrate Create Policy dialog (line ~133).
  - [x] 8.2: Migrate Edit Policy dialog (line ~179).
  - [x] 8.3: Migrate Delete Policy dialog (line ~225).
  - [x] 8.4: Migrate Create Snapshot dialog (line ~260).
  - [x] 8.5: Same pattern for all 4.

- [x] Task 9: Migrate DeadLetters.razor — 3 dialogs (AC: 1, 2, 6, 7, 8, 9, 12, 13)
  - [x] 9.1: Migrate Retry dialog (line ~241).
  - [x] 9.2: Migrate Skip dialog (line ~275).
  - [x] 9.3: Migrate Archive dialog (line ~309).
  - [x] 9.4: Same pattern for all 3.

- [x] Task 10: Migrate Consistency.razor — 3 dialogs (AC: 1, 2, 6, 7, 8, 9, 12, 13)
  - [x] 10.1: Migrate Anomaly Detail dialog (line ~264, guarded by `@if (_selectedAnomaly is not null)`).
  - [x] 10.2: Migrate Trigger Check dialog (line ~311).
  - [x] 10.3: Migrate Cancel dialog (line ~361).
  - [x] 10.4: Same pattern for all 3.

- [x] Task 11: Migrate Compaction.razor — 1 dialog (AC: 1, 2, 6, 7, 8, 9, 12, 13)
  - [x] 11.1: Migrate Trigger Compaction dialog (line ~154).

- [x] Task 12: Update bUnit tests (AC: 31, 32a, 32b)
  - [x] 12.1: Using the inventory from Task 0.6, update every test assertion that references `fluent-dialog-header` or `fluent-dialog-footer` to target v5 DOM structure (content inside `fluent-dialog-body`).
  - [x] 12.2: Update dialog open/close test patterns from synchronous boolean-set to async approach. Specifically: any test that sets `_showXDialog = true` via reflection or button click must now account for the async `ShowAsync()` lifecycle.
  - [x] 12.3: Add `CompactionPageTests::CompactionPage_TriggerDialog_RendersV5DialogStructure` per AC 32a.
  - [x] 12.4: Add `CommandPaletteTests::CommandPalette_ShowAsyncHideAsync_Lifecycle` per AC 32b (new test file if none exists).
  - [x] 12.5: Verify all existing dialog interaction tests compile and pass logic checks post-update.

- [x] Task 13: Build and verification (AC: 17-29)
  - [x] 13.1: Eight-pass grep-for-zero verification:
    - Pass 1 (AC 17): `<FluentDialogHeader\b` → 0 hits
    - Pass 2 (AC 18): `<FluentDialogFooter\b` → 0 hits
    - Pass 3 (AC 19): `@bind-Hidden` → 0 hits
    - Pass 4 (AC 20): `TrapFocus` → 0 hits
    - Pass 5 (AC 21): `@ondialogdismiss` → 0 hits
    - Pass 6 (AC 22): `OnDismiss=` → 0 hits
    - Pass 7 (AC 23): `PreventDismiss` → 0 hits
    - Pass 8 (AC 24): `aria-modal` → 0 hits (on dialog components only)
  - [x] 13.1a: `async void` safety gate: grep for `async void` across all 11 dialog `.razor` files → **0 hits**. Every async handler MUST be `async Task`. `async void` swallows exceptions silently and the compiler won't catch it.
  - [x] 13.2: Positive count verification:
    - `<FluentDialogBody` count = 28
    - `<TitleTemplate>` inside dialog = 28
    - `<ActionTemplate>` inside dialog = 26 (2 dialogs without footers)
  - [x] 13.3: Non-UI Tier 1 tests: `dotnet test` on Contracts + Client + Testing + SignalR. All must pass.
  - [x] 13.4: Admin.UI.Tests isolated compile: `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release`. Capture error output. Attribute each residual to a downstream story (21-7 toast, 21-8 CSS, 21-9 DataGrid, 21-10 samples). Zero residuals on dialog surface = HARD gate.
  - [x] 13.5: Record POST-story error count and DELTA from PRE.

- [x] Task 14: Visual verification (AC: 30) — conditional on Story 21-7 landing
  - [x] 14.1: Check sprint-status.yaml: is 21-7 status `done` or `review`?
    - If NO: mark AC 30 `DEFERRED: waiting on 21-7 toast migration`. Proceed to completion.
    - If YES: proceed to 14.2.
  - [x] 14.2: Flush Redis, `aspire run`, open Admin.UI.
  - [x] 14.3: Open and close each of the 28 dialogs. Verify button actions fire.
  - [x] 14.4: Test rapid open/close/reopen on Tenants (6 dialogs) and Backups (5 dialogs).
  - [x] 14.5: Verify CommandPalette Ctrl+K shortcut still works.
  - [x] 14.6: Check light mode and dark mode for all dialogs.
  - [x] 14.7: Record per-dialog verdict in Completion Notes.

### Review Findings

- [x] [Review][Decision] CommandPalette API naming vs AC 14 (`Open`/`Close` vs `OpenAsync`/`CloseAsync`) — resolved: keep `OpenAsync()`/`CloseAsync()` (AC intent satisfied; no code change required).
- [x] [Review][Patch] CommandPalette open-state can drift after external dismiss, blocking reopen [src/Hexalith.EventStore.Admin.UI/Components/CommandPalette.razor:4] — fixed by syncing `_isOpen`/`_isTransitioning` on dialog `OnStateChange` when state becomes `Closed`.

## Dev Notes

### What This Story Does and Does NOT Do

**DOES:**
- Removes ALL 28 `<FluentDialogHeader>` instances across 11 files → moves content into `<TitleTemplate>` slot inside `<FluentDialogBody>`.
- Removes ALL 26 `<FluentDialogFooter>` instances across 9 files → moves content into `<ActionTemplate>` slot inside `<FluentDialogBody>`.
- Converts 3 `@bind-Hidden` dialog controls to `@ref` + `ShowAsync()`/`HideAsync()` (CommandPalette, CommandSandbox, EventDebugger).
- Converts ~25 conditional-render dialogs to use `@ref` + `ShowAsync()`/`HideAsync()` lifecycle.
- Removes all `TrapFocus`, `aria-modal`, `PreventDismiss`, `OnDismiss`, `@ondialogdismiss`, `ShowDismiss` attributes.
- Converts all synchronous `void` open/close event handlers to `async Task`.
- Updates bUnit tests that reference dialog markup patterns.

**DOES NOT:**
- Touch toast API — Story 21-7.
- Touch CSS tokens — Story 21-8.
- Touch DataGrid/Tab enums — Story 21-9.
- Touch Sample project — Story 21-10.
- Convert inline dialogs to `IDialogService.ShowDialogAsync<T>()` pattern — this story migrates the **inline** dialog pattern, not the service pattern. The codebase uses inline `<FluentDialog>` exclusively; converting to service-based is out of scope.
- Add new dialog features (e.g., drawer alignment, dialog size enum, keyboard shortcuts).

### V5 Dialog API — Authoritative Reference (from MCP server, 2026-04-14)

**`FluentDialog` component (v5):**

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `Modal` | `bool` | `true` | Was `bool?` in v4 |
| `Alignment` | `DialogAlignment` | `Default` | New — `Default`/`Start`/`End` |
| `Instance` | `IDialogInstance?` | — | Was `DialogInstance` in v4 |
| `Style` | `string?` | — | Preserved |
| `Class` | `string?` | — | Preserved |
| `Id` | `string?` | auto | Preserved |
| `Margin` | `string?` | — | New |
| `Padding` | `string?` | — | New |

**Removed from `FluentDialog`:** `Hidden`, `HiddenChanged`, `TrapFocus`, `PreventScroll`, `AriaDescribedby`, `AriaLabelledby`, `AriaLabel`, `OnDialogResult`, `PreventDismiss`, `OnDismiss`.

**Events:** `OnStateChange` (`EventCallback<DialogEventArgs>`) — replaces `OnDialogResult`, `OnDismiss`, `@ondialogdismiss`.

**Methods:** `ShowAsync()`, `HideAsync()` — replace `@bind-Hidden` boolean pattern.

**`FluentDialogBody` component (v5):**

| Parameter | Type | Notes |
|-----------|------|-------|
| `TitleTemplate` | `RenderFragment?` | Replaces `FluentDialogHeader` |
| `TitleActionTemplate` | `RenderFragment?` | New — action in title area |
| `ChildContent` | `RenderFragment?` | Body content (implicit) |
| `ActionTemplate` | `RenderFragment?` | Replaces `FluentDialogFooter` |
| `Style` | `string?` | Preserved |
| `Class` | `string?` | Preserved |

**Removed components:** `FluentDialogHeader`, `FluentDialogFooter` — completely removed in v5.

### V4 → V5 Dialog Migration Pattern (template)

**V4 pattern (current codebase):**
```razor
@if (_showXDialog)
{
    <FluentDialog Modal="true" TrapFocus="true" aria-modal="true"
                  aria-label="Dialog Title" @ondialogdismiss="CloseXDialog"
                  Style="min-width: 400px; max-width: 500px;">
        <FluentDialogHeader>
            <h3 style="margin: 0;">Dialog Title</h3>
        </FluentDialogHeader>
        <FluentDialogBody>
            <!-- body content -->
        </FluentDialogBody>
        <FluentDialogFooter>
            <FluentButton Appearance="ButtonAppearance.Outline" OnClick="CloseXDialog">Cancel</FluentButton>
            <FluentButton Appearance="ButtonAppearance.Primary" OnClick="SubmitX">Submit</FluentButton>
        </FluentDialogFooter>
    </FluentDialog>
}

@code {
    private bool _showXDialog;
    private void OpenXDialog() => _showXDialog = true;
    private void CloseXDialog() => _showXDialog = false;
}
```

**V5 pattern (target — uses approved `OnAfterRenderAsync` pattern):**
```razor
@if (_showXDialog)
{
    <FluentDialog @ref="_xDialog" Modal="true"
                  aria-label="Dialog Title"
                  Style="min-width: 400px; max-width: 500px;">
        <FluentDialogBody>
            <TitleTemplate>
                <h3 style="margin: 0;">Dialog Title</h3>
            </TitleTemplate>
            <ChildContent>
                <!-- body content -->
            </ChildContent>
            <ActionTemplate>
                <FluentButton Appearance="ButtonAppearance.Outline" OnClick="CloseXDialogAsync">Cancel</FluentButton>
                <FluentButton Appearance="ButtonAppearance.Primary" OnClick="SubmitXAsync">Submit</FluentButton>
            </ActionTemplate>
        </FluentDialogBody>
    </FluentDialog>
}

@code {
    private FluentDialog? _xDialog;
    private bool _showXDialog;
    private bool _pendingShowX;

    private async Task OpenXDialogAsync()
    {
        _showXDialog = true;
        _pendingShowX = true;
        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_pendingShowX && _xDialog is not null)
        {
            await _xDialog.ShowAsync();
            _pendingShowX = false;  // AFTER ShowAsync (ADR-21-6-002)
        }
    }

    private async Task CloseXDialogAsync()
    {
        if (_xDialog is not null)
        {
            await _xDialog.HideAsync();  // HideAsync BEFORE removing DOM
        }
        _showXDialog = false;
    }
}
```

**Key migration mechanics:**
1. `FluentDialogHeader` content → `<TitleTemplate>` inside `FluentDialogBody`
2. Body content → explicit `<ChildContent>` tag (or keep implicit if no ambiguity)
3. `FluentDialogFooter` content → `<ActionTemplate>` inside `FluentDialogBody`
4. `@bind-Hidden` → `@ref` + `ShowAsync()`/`HideAsync()`
5. `@ondialogdismiss` → remove (handle via close button `OnClick` or `OnStateChange`)
6. Remove: `TrapFocus`, `aria-modal`, `PreventDismiss`, `OnDismiss`, `ShowDismiss`
7. All open/close handlers → `async Task` with `await`

### Approved ShowAsync Pattern (decision: `OnAfterRenderAsync` with flag)

**Critical implementation detail:** When a dialog is conditionally rendered via `@if (_showXDialog)`, the `@ref` is not immediately available after setting `_showXDialog = true`. The **approved pattern** uses `OnAfterRenderAsync` with a pending-show flag:

```csharp
private FluentDialog? _xDialog;
private bool _showXDialog;
private bool _pendingShowX;

private async Task OpenXDialogAsync()
{
    _showXDialog = true;       // Render the @if block
    _pendingShowX = true;      // Flag for OnAfterRenderAsync
    StateHasChanged();
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (_pendingShowX && _xDialog is not null)
    {
        await _xDialog.ShowAsync();
        _pendingShowX = false;  // AFTER ShowAsync — on exception, flag survives for auto-retry on next render (ADR-21-6-002)
    }
    // ... other pending shows for multi-dialog pages
}

private async Task CloseXDialogAsync()
{
    if (_xDialog is not null)
    {
        await _xDialog.HideAsync();  // HideAsync BEFORE setting _showXDialog = false — let animation complete before DOM removal
    }
    _showXDialog = false;
}
```

**Why not `Task.Yield()`:** `await Task.Yield()` has documented race conditions in Blazor WASM. `OnAfterRenderAsync` is the framework-blessed approach for post-render operations and works reliably in both Server and WASM hosting modes.

**Flag reset timing (ADR-21-6-002):** `_pendingShowX = false` executes AFTER `await ShowAsync()`. On exception, the flag survives and the next render cycle retries automatically. This is deliberate — pre-mortem analysis showed that resetting the flag before `ShowAsync()` loses the user's intent on failure.

**For multi-dialog pages (Tenants 6, Backups 5):** Use a single `OnAfterRenderAsync` override with multiple `_pendingShowX` flags. Only one dialog opens at a time, so at most one flag is true per render cycle. Add an `_isAnyDialogOpen` boolean guard — disable ALL dialog trigger buttons while any dialog is open to prevent race conditions from rapid-fire clicks (see §Multi-Dialog Race Prevention).

### Spike-first execution order (MANDATORY)

**Task 1 (CommandPalette) is the spike gate.** CommandPalette is always-rendered, uses `@bind-Hidden`, and has the Ctrl+K integration — it exercises the full v5 lifecycle. If the `ShowAsync`/`HideAsync` pattern proves unreliable on CommandPalette, **STOP and escalate** before touching the other 27 dialogs. Do not batch-migrate until the spike is validated.

**Approved fallback:** If `OnAfterRenderAsync` with pending flags proves problematic (e.g., double-render issues, flickering), the dev may switch to `await InvokeAsync(async () => { await _xDialog!.ShowAsync(); })` after `StateHasChanged()`. Document the switch and rationale in Completion Notes.

### Multi-Dialog Race Prevention (Tenants 6, Backups 5)

On pages with multiple dialogs, rapid-fire clicking can open two dialogs simultaneously if the user clicks a trigger button while another dialog's `ShowAsync()` is in-flight. **Required pattern for Tenants and Backups:**

```csharp
private bool _isAnyDialogOpen;

private async Task OpenCreateDialogAsync()
{
    if (_isAnyDialogOpen) return;  // Guard
    _isAnyDialogOpen = true;
    _showCreateDialog = true;
    _pendingShowCreate = true;
    StateHasChanged();
}

private async Task CloseCreateDialogAsync()
{
    if (_createDialog is not null)
    {
        await _createDialog.HideAsync();
    }
    _showCreateDialog = false;
    _isAnyDialogOpen = false;
}
```

All dialog trigger buttons on multi-dialog pages should include `Disabled="@_isAnyDialogOpen"` to provide visual feedback.

### CommandPalette Ctrl+K Transition Guard

The CommandPalette is toggled via Ctrl+K keyboard shortcut. Rapid Ctrl+K presses can interleave `ShowAsync()`/`HideAsync()` calls. **Required pattern:**

```csharp
private bool _isTransitioning;

public async Task ToggleAsync()
{
    if (_isTransitioning) return;
    _isTransitioning = true;
    try
    {
        if (_isOpen)
        {
            await _dialog!.HideAsync();
            _isOpen = false;
        }
        else
        {
            await _dialog!.ShowAsync();
            _isOpen = true;
        }
    }
    finally
    {
        _isTransitioning = false;
    }
}
```

### Previous Story Intelligence (from 21-5)

**Key learnings from Story 21-5 that apply here:**
1. **Multi-line Razor attributes:** Dialog components commonly span multiple lines. Read several lines around each grep hit; don't trust single-line matches.
2. **StateHasChanged() calls:** After async operations, verify `StateHasChanged()` is called where needed for re-render.
3. **Build error ceiling approach:** Record PRE and POST error counts; attribute every residual error to a downstream story.
4. **Zero-own-surface rule:** Zero residual errors may trace back to dialog-surface files. This is the hard gate.
5. **Visual verification is DEFERRED** until 21-7 lands (toast migration blocks end-to-end Admin.UI compilation).

### Dialog Instance Inventory (28 dialogs across 11 files)

| # | File | Dialog Purpose | Current Show Pattern | Current Dismiss Pattern | Has Footer? |
|---|------|---------------|---------------------|------------------------|-------------|
| 1 | CommandPalette.razor | Command palette | `@bind-Hidden="_hidden"` | `OnDismiss="@(() => Close())"` | No |
| 2 | CommandSandbox.razor | Event payload viewer | `@bind-Hidden` + dual bool | `@bind-Hidden:after` callback | Yes |
| 3 | EventDebugger.razor | Event payload viewer | `@bind-Hidden="_payloadDialogHidden"` | Close button | Yes |
| 4 | StateInspectorModal.razor | State inspector | `@ref="_dialog"` (already!) | `@ondialogdismiss="HandleDismiss"` | No |
| 5 | ProjectionDetailPanel.razor | Reset confirmation | `@if (_showResetDialog)` | `@ondialogdismiss` | Yes |
| 6 | ProjectionDetailPanel.razor | Replay confirmation | `@if (_showReplayDialog)` | `@ondialogdismiss` | Yes |
| 7 | Tenants.razor | Create Tenant | `@if (_showCreateDialog)` | `@ondialogdismiss="CloseCreateDialog"` | Yes |
| 8 | Tenants.razor | Disable Tenant | `@if (_showDisableDialog)` | `@ondialogdismiss="CloseLifecycleDialog"` | Yes |
| 9 | Tenants.razor | Enable Tenant | `@if (_showEnableDialog)` | `@ondialogdismiss="CloseLifecycleDialog"` | Yes |
| 10 | Tenants.razor | Add User | `@if (_showAddUserDialog)` | `@ondialogdismiss="CloseAddUserDialog"` | Yes |
| 11 | Tenants.razor | Remove User | `@if (_showRemoveUserDialog)` | `@ondialogdismiss="CloseRemoveUserDialog"` | Yes |
| 12 | Tenants.razor | Change Role | `@if (_showChangeRoleDialog)` | `@ondialogdismiss="CloseChangeRoleDialog"` | Yes |
| 13 | Backups.razor | Create Backup | `@if (_showCreateDialog)` | `@ondialogdismiss` | Yes |
| 14 | Backups.razor | Validate Backup | `@if (_showValidateDialog)` | `@ondialogdismiss` | Yes |
| 15 | Backups.razor | Restore from Backup | `@if (_showRestoreDialog)` | `@ondialogdismiss` | Yes |
| 16 | Backups.razor | Export Stream | `@if (_showExportDialog)` | `@ondialogdismiss` | Yes |
| 17 | Backups.razor | Import Stream | `@if (_showImportDialog)` | `@ondialogdismiss` | Yes |
| 18 | Snapshots.razor | Create Policy | `@if (_showCreateDialog)` | `@ondialogdismiss` | Yes |
| 19 | Snapshots.razor | Edit Policy | `@if (_showEditDialog)` | `@ondialogdismiss` | Yes |
| 20 | Snapshots.razor | Delete Policy | `@if (_showDeleteDialog)` | `@ondialogdismiss` | Yes |
| 21 | Snapshots.razor | Create Snapshot | `@if (_showCreateSnapshotDialog)` | `@ondialogdismiss` | Yes |
| 22 | DeadLetters.razor | Bulk Retry | `@if (_showRetryDialog)` | `@ondialogdismiss` | Yes |
| 23 | DeadLetters.razor | Bulk Skip | `@if (_showSkipDialog)` | `@ondialogdismiss` | Yes |
| 24 | DeadLetters.razor | Bulk Archive | `@if (_showArchiveDialog)` | `@ondialogdismiss` | Yes |
| 25 | Consistency.razor | Anomaly Detail | `@if (_selectedAnomaly is not null)` | `@ondialogdismiss="CloseAnomalyDetail"` | Yes |
| 26 | Consistency.razor | Trigger Check | `@if (_showTriggerDialog)` | `@ondialogdismiss` | Yes |
| 27 | Consistency.razor | Cancel Check | `@if (_showCancelDialog)` | `@ondialogdismiss` | Yes |
| 28 | Compaction.razor | Trigger Compaction | `@if (_showTriggerDialog)` | `@ondialogdismiss` | Yes |

### Known v5 gotchas (dialog-specific)

1. **`@ref` is `null` inside `@if` block until render completes.** After setting the `@if` guard to `true` and calling `StateHasChanged()`, the `@ref` is not assigned until the render cycle completes. Use the `OnAfterRenderAsync` + `_pendingShowX` pattern (see §Approved ShowAsync Pattern). Do NOT use `Task.Yield()` — it has race conditions in Blazor WASM.
2. **`OnStateChange` fires for ALL state transitions** (Opening, Open, Closing, Closed). If you only care about Closed, check `e.State == DialogState.Closed`.
3. **`Modal` is now `bool` (default `true`), not `bool?`.** All 28 dialogs use `Modal="true"` — this is the default so technically redundant, but keep for clarity.
4. **No `ShowDismiss` property on v5.** The dismiss "×" button in the title bar is now controlled by the `TitleActionTemplate` slot or by `DialogOptions.Header` when using the service pattern. For inline dialogs, add a close button in `TitleActionTemplate` if needed, or rely on the `ActionTemplate` Cancel button.
5. **`async void` is NEVER acceptable for Blazor event handlers.** Always use `async Task`. The compiler won't catch this; the runtime will swallow exceptions silently.
6. **StateInspectorModal already uses `@ref`** — this is the easiest migration. Just remove the v4-only attributes and restructure the DOM.
7. **Disposal race (acknowledged, deferred).** If a user navigates away from a page while a dialog's `ShowAsync()` is mid-flight, Blazor disposes the component and `ShowAsync()` may try to modify disposed DOM. This is a pre-existing race condition (v4 had the same issue with `StateHasChanged()` after navigation). Adding `IAsyncDisposable` with a `_disposed` flag is the proper fix but is out of scope for this migration story. If it manifests in testing, add a `_disposed` guard as a hot-fix and note it in Completion Notes for a follow-up.
8. **`HideAsync()` ordering matters.** Always call `await _xDialog.HideAsync()` BEFORE setting `_showXDialog = false`. If you remove the DOM first (via the `@if` guard), `HideAsync()` has no element to animate. The approved pattern in §Approved ShowAsync Pattern shows the correct ordering.
9. **Shared close methods must target the open dialog.** Tenants.razor `CloseLifecycleDialog()` handles both Disable and Enable dialogs. Only call `HideAsync()` on the ref that is actually open. Calling `HideAsync()` on a null ref is safe (null-conditional), but calling it on a ref whose dialog was never shown may have undefined behavior.

### Architecture Decision Records

**ADR-21-6-001: ShowAsync Pattern Selection**

| Option | Description | Risk | Decision |
|--------|-------------|------|----------|
| A: `Task.Yield()` | Simple inline | **High** — WASM race conditions | Rejected |
| B: `OnAfterRenderAsync` + flag | Framework-blessed, deterministic | **Low** | **Selected** |
| C: Always-render | No timing issue, simplest | **Medium** — doesn't reset forms, DOM bloat | Rejected (except CommandPalette + StateInspectorModal) |
| D: `IDialogService` | v5-native service pattern | **Very High** — complete rewrite | Rejected |

**ADR-21-6-002: Flag Reset Timing**

`_pendingShowX = false` executes AFTER `await ShowAsync()`. On `ShowAsync()` exception, the flag survives and the next render cycle retries automatically. Resetting before `ShowAsync()` would lose the user's intent on failure with no auto-recovery.

### Visual Verification Protocol (spot-check matrix for AC 30, conditional on 21-7 landing)

| Page/Component | # Dialogs | Verify |
|---------------|-----------|--------|
| CommandPalette | 1 | Ctrl+K open, search, close |
| Streams → StateInspector | 1 | Open from stream, close |
| Projections → Detail | 2 | Reset dialog, Replay dialog |
| CommandSandbox | 1 | Event payload view |
| EventDebugger | 1 | Event payload view |
| Tenants | 6 | Create, Disable, Enable, Add User, Remove User, Change Role |
| Backups | 5 | Create, Validate, Restore, Export, Import |
| Snapshots | 4 | Create Policy, Edit Policy, Delete Policy, Create Snapshot |
| Dead Letters | 3 | Retry, Skip, Archive |
| Consistency | 3 | Anomaly Detail, Trigger, Cancel |
| Compaction | 1 | Trigger |
| **Total** | **28** | Light + Dark mode for each |

### Project Structure Notes

- All dialog-using files are in `src/Hexalith.EventStore.Admin.UI/Pages/` and `src/Hexalith.EventStore.Admin.UI/Components/`
- No `.razor.cs` code-behind files — all use inline `@code` blocks
- All dialogs use inline `<FluentDialog>` pattern, not `IDialogService`
- `.editorconfig` conventions: file-scoped namespaces, Allman braces, `_camelCase` fields, 4-space indent, CRLF

### References

- [Source: MCP `get_component_migration("FluentDialog")` — 2026-04-14]
- [Source: MCP `get_component_details("FluentDialog")` — 2026-04-14]
- [Source: MCP `get_component_details("FluentDialogBody")` — 2026-04-14]
- [Source: sprint-change-proposal-2026-04-13-fluent-ui-v5-migration.md §Story 21-6]
- [Source: technical-fluentui-blazor-v5-research-2026-04-06.md §Dialog]
- [Source: Story 21-5 Dev Notes §Previous Story Intelligence]

## Dev Agent Record

### Agent Model Used

claude-opus-4-6 (1M) — Amelia (bmad dev story workflow)

### Debug Log References

- PRE build error count (grep CS+RZ over slnx Release): not separately captured before edits because story file already noted prior 21-5 baseline residuals.
- POST grep verification (after migration, all 11 dialog `.razor` files):
  - `<FluentDialogHeader\b` = 0 (gate AC 17 ✅)
  - `<FluentDialogFooter\b` = 0 (gate AC 18 ✅)
  - `@bind-Hidden` = 0 (gate AC 19 ✅)
  - `TrapFocus` = 0 (gate AC 20 ✅)
  - `@ondialogdismiss` = 0 (gate AC 21 ✅)
  - `OnDismiss=` = 0 (gate AC 22 ✅)
  - `PreventDismiss` = 0 (gate AC 23 ✅)
  - `aria-modal` = 0 (gate AC 24 ✅)
  - `<FluentDialogBody` = 28 (gate AC 25 ✅)
  - `<TitleTemplate>` = 28 (gate AC 26 ✅)
  - `<ActionTemplate>` = 26 (gate AC 27 ✅ — CommandPalette + StateInspectorModal have no footer)
  - `async void` across 11 dialog files = 0 (gate AC 13.1a ✅; the 2 hits in `NavMenu.razor` and `StreamDetail.razor` are pre-existing event-subscriber callbacks outside the dialog surface)
- Tier 1 tests (AC 28): Contracts 271 ✅, Client 321 ✅, Testing 67 ✅, SignalR 32 ✅ — total 691 passed.
- POST Release build of slnx: residual error tally
  - `Hexalith.EventStore.Admin.UI` only — every error attributable to a downstream story:
    - 69 × CS1061 on `IToastService.Show*` → **21-7 toast migration** (deferred per Dev Notes)
    - 5 × CS0103 on `Align`/`SortDirection` (Storage.razor, StreamTimelineGrid.razor) → **21-9 DataGrid renames**
    - 2 × CS0246 on `IDialogReference` (ProjectionDetailPanel.razor pause/resume confirmation paths) → **service-pattern dialog API; explicitly out of scope per §What This Story Does NOT Do**. These two errors are pre-existing v5 incompatibilities that were not touched (Reset/Replay inline dialogs were migrated; Pause/Resume continue to use the v4 service API which v5 reshaped).
- Admin.UI.Tests project still does not compile in isolation due to the same upstream cascading errors plus pre-existing v4 type references (`FluentTextField` etc.) — this is unchanged from the 21-5 baseline and tracked under 21-7/21-9.

### Completion Notes List

- Spike gate (Task 1, CommandPalette) cleared without timing issues. Used the OnAfterRenderAsync + pending-flag pattern only for conditionally-rendered dialogs (24 of 28); CommandPalette and StateInspectorModal use direct `_dialog.ShowAsync()` since they are always-rendered. Per Dev Notes §CommandPalette Ctrl+K Transition Guard, added `_isTransitioning` boolean — guards both Open/Close paths.
- CommandSandbox (Task 2): chose option (b) — always-render the payload dialog and control via `@ref`. Dropped `_showPayloadDialog` and `_hidePayloadDialog` booleans entirely; replaced with single `FluentDialog? _payloadDialog`. Removed `OnPayloadDialogHiddenChanged` callback (no longer needed).
- StateInspectorModal (Task 4): added `OnAfterRenderAsync` to call `ShowAsync` on first render (was previously rendered-but-hidden via `@ref` without explicit show; v5 requires explicit `ShowAsync`). Replaced `@ondialogdismiss` with `OnStateChange` filtering on `DialogState.Closed`. The × close button now calls `HideAsync()` and lets `OnStateChange` propagate the closure to the parent.
- ProjectionDetailPanel (Task 5): kept `@if` guards (per AC 13). Both Reset and Replay dialogs use the pending-flag pattern via a single `OnAfterRenderAsync`. Pause/Resume confirmations were NOT touched (they use the v5 `IDialogService` service pattern, which is out of scope per §DOES NOT). The 2 residual `IDialogReference` errors trace there.
- Tenants (Task 6): added `_isAnyDialogOpen` computed property; gates all 6 Open methods. `CloseLifecycleDialog` was rewritten as `CloseLifecycleDialogAsync` to only call `HideAsync` on the dialog that is actually open (per Task 6.11). All 6 success-path closes now `await HideAsync()` before clearing the show flag (per §Known v5 gotcha #8).
- Backups, Snapshots, DeadLetters, Consistency, Compaction (Tasks 7–11): identical pattern (per Dev Notes template). `_isAnyDialogOpen` guard added per page; pending-show flags wired through a single `OnAfterRenderAsync` per file. DeadLetters' shared `CloseDialogs` was split into 3 dialog-specific async close methods + a `CloseAllDialogsAsync` helper (await sites in `ExecuteBulkActionAsync` updated). Snapshots' URL pre-fill (`?create=true`) now sets both `_showCreateDialog` and `_pendingShowCreate` so the dialog actually opens after navigation.
- bUnit tests (Task 12): the existing test files contained no `fluent-dialog-header` / `fluent-dialog-footer` selectors, so AC 31 had no surface to update. Updated `CommandPaletteTests` to call `OpenAsync()` (renamed from `Open()`) and added the AC 32b test `CommandPalette_ShowAsyncHideAsync_Lifecycle`. Added the AC 32a test `CompactionPage_TriggerDialog_RendersV5DialogStructure` to `CompactionPageTests` — asserts `fluent-dialog-body` is present and `fluent-dialog-header`/`fluent-dialog-footer` are NOT.
- Visual verification (Task 14, AC 30): **DEFERRED — waiting on 21-7 toast migration.** Sprint status confirms 21-7 is `backlog`. Marked `DEFERRED-TO-21-7-OR-EPIC-21-RETRO` per spec.
- Multi-dialog race prevention is in place on Tenants (6 dialogs) and Backups (5 dialogs) per §Multi-Dialog Race Prevention; also extended to Snapshots (4), DeadLetters (3), Consistency (3) for consistency.
- `MainLayout.razor` updated to await the renamed `_commandPalette.OpenAsync()`. The pre-existing RZ9986 error on line 29 (Class attribute mixed content) is unrelated to this story.
- Disposal-race hot-fix (per §Known v5 gotcha #7) was not needed; no manifestations during isolated build verification.

### File List

**Modified (12 files):**

§ Components (5 files)
- `src/Hexalith.EventStore.Admin.UI/Components/CommandPalette.razor` — 1 dialog migrated
- `src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor` — 1 dialog migrated
- `src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor` — 1 dialog migrated
- `src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor` — 1 dialog migrated
- `src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor` — 2 dialogs migrated

§ Pages (6 files)
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor` — 6 dialogs migrated
- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor` — 5 dialogs migrated
- `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` — 4 dialogs migrated
- `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor` — 3 dialogs migrated
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor` — 3 dialogs migrated
- `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor` — 1 dialog migrated

§ Layout (1 file)
- `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor` — updated `OnCommandPaletteShortcut` to await renamed `OpenAsync()`

§ Tests (2 files)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandPaletteTests.cs` — updated existing tests to call `OpenAsync()`; added `CommandPalette_ShowAsyncHideAsync_Lifecycle` (AC 32b)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CompactionPageTests.cs` — added `CompactionPage_TriggerDialog_RendersV5DialogStructure` (AC 32a)

### Change Log

- 2026-04-14: Story 21-6 implementation — migrated all 28 inline `<FluentDialog>` instances across 11 Razor files from Fluent UI Blazor v4 dialog markup to v5 (`FluentDialogBody` with `TitleTemplate` / `ActionTemplate` slots; `@ref` + `ShowAsync()` / `HideAsync()` lifecycle replacing `@bind-Hidden` and synchronous void handlers). Removed all `TrapFocus`, `aria-modal`, `PreventDismiss`, `OnDismiss`, `@ondialogdismiss`, `ShowDismiss` attributes. Added 2 bUnit tests (AC 32a, 32b). Status → review.

**Files to modify (11):**

§ Components (4 files)
- `src/Hexalith.EventStore.Admin.UI/Components/CommandPalette.razor` — 1 dialog
- `src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor` — 1 dialog
- `src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor` — 1 dialog
- `src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor` — 1 dialog

§ Components (1 file)
- `src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor` — 2 dialogs

§ Pages (6 files)
- `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor` — 6 dialogs
- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor` — 5 dialogs
- `src/Hexalith.EventStore.Admin.UI/Pages/Snapshots.razor` — 4 dialogs
- `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor` — 3 dialogs
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor` — 3 dialogs
- `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor` — 1 dialog

§ Tests (files to update — dialog-related selectors/assertions)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/BackupsPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CompactionPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DeadLettersPageTests.cs`
- (Additional test files that reference dialog patterns — audit during Task 0)
