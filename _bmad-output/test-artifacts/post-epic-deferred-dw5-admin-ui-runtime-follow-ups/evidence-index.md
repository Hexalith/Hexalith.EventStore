# DW5 Admin UI Runtime Follow-Ups Evidence Index

Story: `post-epic-deferred-dw5-admin-ui-runtime-follow-ups`
Date started: 2026-05-05

## Environment

| Field | Value |
| --- | --- |
| Repository | `D:\Hexalith.EventStore` |
| Story source | `_bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md` |
| Evidence folder | `_bmad-output/test-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups/` |
| Runtime mode | Live Aspire pre-edit probe plus post-fix Playwright E2E browser fixture |
| Admin UI URL | Live Aspire: `https://localhost:8093`; E2E fixture: ephemeral `http://127.0.0.1:{port}` |
| Viewport | Live probe: 1366x900; E2E fixture: Playwright default viewport |
| Browser/test driver | Playwright MCP for live probe; `Microsoft.Playwright` Chromium via `tests/Hexalith.EventStore.Admin.UI.E2E` for post-fix proof |

## App URL Summary

| App | URL | Evidence use |
| --- | --- | --- |
| Live Aspire Admin UI | `https://localhost:8093` | Pre-edit `/types` browser probe and console capture |
| E2E Admin UI fixture | `http://127.0.0.1:{ephemeral-port}` | Post-fix TypeCatalog route and shortcut browser assertions |

## Disposition Vocabulary

| Closure mode | Meaning |
| --- | --- |
| `fixed-with-evidence` | A reproduced DW5 defect was patched and runtime/test evidence proves the target behavior. |
| `not-reproduced-with-evidence` | The current code did not reproduce the deferred symptom, and bounded browser evidence records the tested route, interaction, timing, and console/circuit result. |
| `deferred-with-target-and-reason` | The item could not be fully closed in DW5; the exact blocker and future target are recorded without claiming completion. |

## Baseline Deferred-Work Classification

| Source item | Initial classification | DW5 scope decision | Planned closure mode |
| --- | --- | --- | --- |
| TypeCatalog `/types` blocks sidebar navigation | `evidence-now` unless reproduced, then `patch-now` | DW5 | `not-reproduced-with-evidence` in post-fix E2E route matrix |
| Ctrl+B sidebar toggle runtime failure | `patch-now` | DW5 | `fixed-with-evidence` |
| CommandSandbox `aria-label="Event payload"` runtime/AT verification | `evidence-now` | DW5 | `deferred-with-target-and-reason` for live data/AT, with source/bUnit regression signal retained |
| EventDebugger `aria-label="Event payload"` runtime/AT verification | `evidence-now` | DW5 | `deferred-with-target-and-reason` for live data/AT, with source/bUnit regression signal retained |
| Ctrl+K CommandPalette re-open verified in 21-13 | `obsolete-resolved` | DW5 regression guard only | `fixed-with-evidence` for same-session regression guard after Ctrl+B |
| TypeCatalog deep-link/back-forward verified in 21-13 | `obsolete-resolved` | DW5 regression guard only | `not-reproduced-with-evidence` via TypeCatalog E2E tab starts |
| FluentText `Typography` enum gap | `obsolete-resolved` | DW5 disposition only | `not-reproduced-with-evidence` via static invariant scan |
| DW2 live DAPR/MCP evidence | `not-DW5` | Excluded | Not applicable |
| DW3 Admin debugging JSON hardening | `not-DW5` | Excluded | Not applicable |
| DW4 evidence-template validation | `not-DW5` | Excluded | Not applicable |
| DW6 deferred-work governance | `not-DW5` | Excluded | Not applicable |

Classification vocabulary values `accepted-debt` and `duplicate` were kept available for the ledger but were not selected for any DW5 item after the final evidence pass; dialog runtime/AT gaps use final closure mode `deferred-with-target-and-reason` and deferred-work marker `DEFERRED-WITH-TARGET-AND-REASON` (aligned with the AC #1 / Hardening-Note vocabulary).

## Pre-Edit Decision Ledger

| Area | Starting decision | Evidence required before production edits | Stop rule |
| --- | --- | --- | --- |
| TypeCatalog navigation | Test before patching hypotheses | URL transition, visible page marker, elapsed time, console/circuit result for `/types`, `/types?tab=commands`, `/types?tab=aggregates` and at least three sidebar targets | Do not rewrite grids/tabs/navigation unless a specific TypeCatalog cause is reproduced |
| Sidebar Ctrl+B | Patch targeted renderer-context hazard after preserving symptom or code-level proof | Repeated Ctrl+B toggle, viewport-tier storage key, refresh persistence, console/circuit clean result | Do not replace shortcut architecture or collapse viewport-tier storage |
| Ctrl+K regression | Same browser session as Ctrl+B | Open, close, reopen command palette without conflict | Do not change CommandPalette unless shortcut regression is observed |
| CommandSandbox dialog | Runtime evidence if data can open the dialog | DOM `aria-label`, keyboard/focus behavior, accessibility snapshot if available, AT blocker if not | Do not claim full AT verification from markup or screenshot only |
| EventDebugger dialog | Runtime evidence if data can open the dialog | DOM `aria-label`, keyboard/focus behavior, accessibility snapshot if available, AT blocker if not | Do not claim full AT verification from markup or screenshot only |

## Runtime Evidence Matrix

The matrix groups runs by area: TypeCatalog navigation (DW5-TC-*), Shortcut behavior (DW5-SC-*), and Dialog accessibility (DW5-DLG-*). Each row records the route/interaction, expected/observed result, console/circuit status, and AC mapping.

### TypeCatalog, Shortcut, and Dialog Evidence Rows

| ID | Area | Route/context | Interaction | Expected | Observed | Timing | Console/circuit | Artifacts | AC |
| --- | --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| DW5-TC-001 | TypeCatalog | `/types` | Click `/commands`, `/events`, `/streams` from sidebar | URL and visible page change within 3s for each click | Passed in `SidebarNav_From_Types_TransitionsUrlAndVisiblePage`; Type Catalog H1 detached after each route | 3s bound per click | No E2E console failure; live probe had only favicon 404 | `validation-log.md`; `live-aspire-preedit-types-snapshot.md`; `page-2026-05-05T17-07-28-232Z.yml` | #2, #3, #4, #10, #11 |
| DW5-TC-002 | TypeCatalog | `/types?tab=commands` | Click `/commands`, `/events`, `/streams` from sidebar | URL and visible page change within 3s for each click | Passed in `SidebarNav_From_TypesTabCommands_TransitionsUrlAndVisiblePage` | 3s bound per click | No E2E console failure | `validation-log.md` | #2, #3, #4 |
| DW5-TC-003 | TypeCatalog | `/types?tab=aggregates` | Click `/commands`, `/events`, `/streams` from sidebar | URL and visible page change within 3s for each click | Passed in `SidebarNav_From_TypesTabAggregates_TransitionsUrlAndVisiblePage` | 3s bound per click | No E2E console failure | `validation-log.md`; `page-2026-05-05T17-08-00-510Z.yml` | #2, #3, #4 |
| DW5-SC-001 | Shortcuts | E2E shell `/` | Press Ctrl+B five times | Sidebar class alternates each time; zero error-level console messages | Passed in `CtrlB_RepeatedToggle_NoConsoleErrors_SidebarWidthAlternates` | 3s bound per toggle | Clean in E2E | `validation-log.md` | #5 |
| DW5-SC-002 | Shortcuts | E2E shell `/` | Press Ctrl+B, inspect localStorage, refresh | `hexalith-sidebar-collapsed-{tier}` key written and value persists after refresh | Passed in `CtrlB_StorageKey_MatchesViewportTier_AndPersistsAcrossRefresh` | 3s bound for storage write | Clean in E2E | `validation-log.md` | #6 |
| DW5-SC-003 | Shortcuts | E2E shell `/` | Press Ctrl+B twice, then Ctrl+K, Escape, Ctrl+K | Command palette search becomes visible, hides, and reopens in same session | Passed in `CtrlK_OpenCloseReopen_StillWorksAfterCtrlBActivity` | 3s bound per transition | Clean in E2E | `validation-log.md` | #7 |
| DW5-DLG-001 | CommandSandbox dialog | Source/bUnit-level only | Static markup and existing bUnit regression signal | `<FluentDialog Modal="true" aria-label="Event payload">` wraps `FluentDialogBody` | Static DW5 tests and existing `CommandSandbox_PayloadDialog_CarriesAriaLabel` pass; live DOM/AT not claimed | Not applicable | Not applicable | `validation-log.md` | #8, #11, #12 |
| DW5-DLG-002 | EventDebugger dialog | Source/bUnit-level only | Static markup and existing bUnit regression signal | `<FluentDialog Modal="true" aria-label="Event payload">` wraps `FluentDialogBody` | Static DW5 tests and existing `EventDebugger_PayloadDialog_CarriesAriaLabel` pass; live DOM/AT not claimed | Not applicable | Not applicable | `validation-log.md` | #9, #11, #12 |

## Console And Circuit Notes

Live Aspire pre-edit console capture contains one error, a `favicon.ico` 404, with no SignalR circuit or renderer exception. Post-fix E2E browser tests asserted no error-level console messages during Ctrl+B repeated toggles and TypeCatalog sidebar navigation.

### Pre-edit Ctrl+B reproduction inheritance

DW5's own pre-edit live probe captured `/types` page load and console only — Ctrl+B was not pressed against the unfixed code in this run. The Ctrl+B circuit/renderer-context exception that motivated the patch was reproduced and recorded during the Epic 21 follow-up session (story `21-13-ui-bug-fixes-batch`) and carried into the deferred-work entry "Epic 21 Ctrl+B sidebar toggle runtime failure" verbatim. DW5 inherits that reproduction rather than re-capturing it; the Advanced Elicitation evidence-first stop rule is satisfied via the cited 21-13 evidence plus the new bUnit `OnToggleSidebarShortcut_FlipsCollapseStateAndDispatchesRerender` test that pins the renderer-context contract deterministically.

## Dialog Accessibility Evidence Tiers

| Dialog | Component markup | Live Fluent DOM | Keyboard/focus | Browser accessibility snapshot | Assistive technology pass | Disposition |
| --- | --- | --- | --- | --- | --- | --- |
| CommandSandbox `Event payload` | Passed via static source and bUnit regression-signal tests | Not captured; no live seeded command/event flow was available in the DW5 run | Not captured; dialog could not be opened with real data | Not captured | Blocked: no configured AT tooling in this run | `deferred-with-target-and-reason` |
| EventDebugger `Event payload` | Passed via static source and bUnit regression-signal tests | Not captured; no live seeded stream/debugger path was available in the DW5 run | Not captured; dialog could not be opened with real data | Not captured | Blocked: no configured AT tooling in this run | `deferred-with-target-and-reason` |

## Deferred-Work Dispositions

| Source line | Disposition marker | Evidence row | Notes |
| --- | --- | --- | --- |
| `deferred-work.md` Epic 21 TypeCatalog nav block | `STORY:post-epic-deferred-dw5-admin-ui-runtime-follow-ups / NOT-REPRODUCED-WITH-EVIDENCE` | DW5-TC-001..003 | Post-fix E2E covers `/types`, `/types?tab=commands`, `/types?tab=aggregates`, and three sidebar targets per start. |
| `deferred-work.md` Epic 21 Ctrl+B sidebar toggle | `STORY:post-epic-deferred-dw5-admin-ui-runtime-follow-ups / RESOLVED` | DW5-SC-001..002 | Renderer-context and shortcut key matching patches landed; browser tests cover repeated toggle and viewport-tier storage. |
| `deferred-work.md` Epic 21 FluentDialog aria-label partial runtime verification | `STORY:post-epic-deferred-dw5-admin-ui-runtime-follow-ups / DEFERRED-WITH-TARGET-AND-REASON` | DW5-DLG-001, DW5-DLG-002 | Source/bUnit contract is green; live DOM/AT evidence still requires seeded stream/command data and AT tooling. |
| `deferred-work.md` Epic 21 Ctrl+K CommandPalette re-open | `STORY:post-epic-deferred-dw5-admin-ui-runtime-follow-ups / RESOLVED-REGRESSION-GUARD` | DW5-SC-003 | Same-session Ctrl+B then Ctrl+K open/Escape/Ctrl+K reopen passes in browser. |
| `deferred-work.md` Epic 21 FluentText Typography enum gap | `STORY:post-epic-deferred-dw5-admin-ui-runtime-follow-ups / RESOLVED-NO-ACTION` | DW5-DLG-001, DW5-DLG-002 | Static invariant scan confirms no removed v4 `Typo`/`Typography` parameter on `FluentText`. |
| `deferred-work.md` Epic 21 TypeCatalog deep-link/back-forward | `STORY:post-epic-deferred-dw5-admin-ui-runtime-follow-ups / RESOLVED-NO-ACTION` | DW5-TC-001..003 | Existing URL state retained; TypeCatalog E2E starts from tab query URLs and passes. |
| `deferred-work.md` Epic 21 screenshots partial capture | `STORY:post-epic-deferred-dw5-admin-ui-runtime-follow-ups / DEFERRED-WITH-TARGET-AND-REASON` | DW5-DLG-001, DW5-DLG-002 | Screenshot gap remains only for data-dependent payload dialogs; captured evidence is source/bUnit plus explicit runtime blocker. |
