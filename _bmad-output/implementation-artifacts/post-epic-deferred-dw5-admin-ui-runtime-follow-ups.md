# Post-Epic Deferred DW5: Admin UI Runtime Follow-Ups

Status: ready-for-dev

<!-- Source: sprint-change-proposal-2026-05-04-deferred-work-triage.md - Proposal F / DW5 -->
<!-- Source: deferred-work.md - Epic 21 Admin UI runtime follow-ups through 2026-05-04 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Admin UI operator and accessibility reviewer,
I want the remaining Admin UI runtime follow-ups to be reproduced, fixed or dispositioned, and evidenced in a live browser,
so that TypeCatalog navigation, sidebar shortcuts, and dialog accessibility are no longer carried as unresolved deferred-work notes.

## Story Context

`deferred-work.md` still carries user-visible Admin UI runtime items from the Epic 21 Fluent UI Blazor v5 migration and the 21-13 follow-up browser session. The deferred-work triage proposal groups those items into DW5 because they are runtime/UI confidence gaps rather than backend architecture, evidence-schema, or deferred-work governance problems.

This story is a narrow Admin UI runtime closure pass. It should reproduce the TypeCatalog `/types` sidebar navigation block, fix or explicitly disposition the Ctrl+B sidebar toggle failure, complete CommandSandbox and EventDebugger dialog `aria-label` runtime/assistive-technology evidence where feasible, and mark obsolete Epic 21 resolved entries in `deferred-work.md`. It must not absorb DW2 live DAPR/MCP evidence, DW3 Admin debugging JSON hardening, DW4 evidence-template validation, or DW6 deferred-work governance.

Current HEAD at story creation: `2dc4986d`.

## Acceptance Criteria

1. **DW5 scope is baselined from the real deferred entries.** Given this story starts, when the developer begins Task 0, then they must re-read Proposal F / DW5 in `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md` and the `## Epic 21 Follow-ups` entries in `_bmad-output/implementation-artifacts/deferred-work.md`. Classify each selected item as `patch-now`, `evidence-now`, `accepted-debt`, `obsolete-resolved`, `duplicate`, or `not-DW5`.

2. **TypeCatalog navigation blocking is reproduced or closed with explicit evidence.** Given the user is on `/types?tab=*`, when they click at least three sidebar navigation links, then the URL and rendered page must change within a bounded time without needing repeated clicks. If the current code no longer reproduces the block, record a same-run browser evidence table showing the tested tabs, nav links, URLs, and timings. If it still reproduces, fix the smallest TypeCatalog/render-loop cause and prove the same scenario passes.

3. **TypeCatalog render-loop hypotheses are tested before broad rewrites.** Given the deferred note lists likely causes such as inline `AsQueryable()`, `DashboardRefreshService.OnDataChanged`, `ViewportService.IsWideViewport`, and FluentTabs URL synchronization, when investigating the navigation block, then add temporary diagnostics only as needed and remove them before handoff. Do not rewrite the whole TypeCatalog page, replace FluentDataGrid, or remove URL-driven tab state unless evidence shows that specific path causes the failure.

4. **TypeCatalog URL and selection behavior remains non-regressive.** Given TypeCatalog already supports deep links, tab history, domain/search filters, and type selection, when DW5 changes TypeCatalog behavior, then `/types`, `/types?tab=commands`, `/types?tab=aggregates`, and a selected `type=` URL must still initialize the expected tab/selection and must not reintroduce the previous redirect loop. Existing bUnit tests around `UpdateUrl` and deep links must remain green or be updated with equivalent coverage.

5. **Ctrl+B sidebar toggle works on the Blazor renderer context.** Given the Admin UI shell is open in a browser, when the user presses Ctrl+B repeatedly, then the sidebar collapses and expands without browser console errors, SignalR circuit exceptions, or lost local-storage state. The fix must address the `MainLayout.razor` `OnToggleSidebarShortcut` renderer-context hazard directly, preferably by keeping state mutation and `StateHasChanged` on the Blazor synchronization context rather than hiding the failure in JavaScript.

6. **Ctrl+B persistence stays viewport-scoped.** Given the sidebar uses viewport-tier storage keys from `GetSidebarStorageKeyAsync`, when Ctrl+B toggles the sidebar, then the stored key remains `hexalith-sidebar-collapsed-{tier}` for the current viewport tier and the saved state is restored after refresh. Do not collapse all viewport tiers into one key or remove compact-width default behavior.

7. **Command palette shortcut remains non-regressive.** Given Ctrl+K was already browser-verified after 21-13, when DW5 changes shortcut registration, JS interop, MainLayout, or shared shortcut code, then Ctrl+K open/close/re-open must still work and must not conflict with Ctrl+B handling. Evidence should include both shortcuts in the same browser session.

8. **CommandSandbox dialog accessibility evidence is completed or honestly deferred.** Given a stream and command context exists that can open the CommandSandbox event-payload dialog, when the dialog is opened, then evidence must show whether `aria-label="Event payload"` lands on the actual rendered Fluent dialog element and whether an assistive-technology pass was completed. If a live event stream or AT tool is unavailable, record the exact blocker and keep the bUnit regression-signal test in place; do not claim full accessibility verification from markup-only evidence.

9. **EventDebugger dialog accessibility evidence is completed or honestly deferred.** Given an event stream exists that can open the EventDebugger payload dialog, when the dialog is opened, then evidence must show whether `aria-label="Event payload"` lands on the actual rendered Fluent dialog element and whether an assistive-technology pass was completed. If data or AT tooling is unavailable, record the exact blocker and treat the result as partial evidence, not done.

10. **Runtime evidence artifacts are durable.** Store screenshots, console snippets, DOM extracts, Playwright traces, or a concise evidence markdown file under `_bmad-output/test-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups/`. At minimum include a table for environment, app URLs, TypeCatalog nav checks, shortcut checks, dialog checks, console errors, and deferred-work dispositions.

11. **Testing uses the smallest useful mix of bUnit and browser coverage.** Add or update bUnit tests for deterministic component behavior such as MainLayout shortcut methods, TypeCatalog URL idempotency, and dialog attributes where bUnit can prove the contract. Use Playwright or manual browser evidence for behavior that depends on Blazor Server hydration, browser keyboard shortcuts, Fluent web-component rendering, sidebar navigation, or assistive technology.

12. **Fluent UI Blazor v5 component behavior is respected.** Given the repository is pinned to `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.2-26098.1` and Icons `4.14.0`, when changing Fluent components, then use v5 parameter names and patterns. Keep `FluentDialogBody` for dialogs, preserve `FluentTabs ActiveTabId/ActiveTabIdChanged` behavior, and do not reintroduce removed v4 APIs such as `Typo` on `FluentText`.

13. **Deferred-work dispositions are narrow and auditable.** Given DW5 closes or routes one of the selected bullets in `_bmad-output/implementation-artifacts/deferred-work.md`, when the story moves to review, then each touched bullet must receive a clear disposition marker such as `STORY:post-epic-deferred-dw5-admin-ui-runtime-follow-ups`, `RESOLVED`, `ACCEPTED-DEBT`, `DUPLICATE`, or `NO-ACTION`. Do not rewrite unrelated DW1-DW4, DW6, DAPR, MCP, SignalR, query, release-governance, or backend entries.

14. **Scope boundaries stay intact.** DW5 must not change Admin API contracts, DAPR component YAML, EventStore command/query contracts, SignalR hub semantics, TypeCatalog server endpoints, MCP behavior, evidence-schema validators, or deferred-work governance conventions unless a reproduced Admin UI runtime defect proves the change is the minimum local fix. Any pressure to do those things belongs to another story.

15. **Bookkeeping is closed.** At dev handoff, update this story's Dev Agent Record, File List, Change Log, Verification Status, evidence artifact links, and any deferred-work dispositions. Move this story and its sprint-status row to `review` only after targeted tests and runtime evidence are recorded. Move both to `done` only after code review signoff.

## Scope Boundaries

- Do not reopen Epic 21 or migrate Fluent UI packages in this story.
- Do not implement a broad Admin UI redesign, navigation rewrite, or TypeCatalog replacement.
- Do not change Admin.Server contracts or data models unless a UI runtime defect proves a narrow contract bug.
- Do not add new DAPR, Aspire, MCP, query, SignalR, or backend evidence obligations.
- Do not claim AT verification from bUnit-only or screenshot-only evidence.
- Do not edit generated preflight JSON audit files.
- Do not initialize or update nested submodules.

## Implementation Inventory

| Area | File / artifact | Expected use |
| --- | --- | --- |
| Planning source | `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md` | Proposal F scope and acceptance direction |
| Deferred source | `_bmad-output/implementation-artifacts/deferred-work.md` | raw Epic 21 Admin UI runtime follow-ups |
| Main layout | `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor` | Ctrl+B handler, shortcut registration, sidebar state, renderer-context fix |
| Navigation menu | `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` | sidebar links used to reproduce `/types` navigation block |
| Type catalog | `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor` | `/types` tabs, URL sync, data grids, refresh subscription, render-loop investigation |
| Command palette | `src/Hexalith.EventStore.Admin.UI/Components/CommandPalette.razor` | Ctrl+K regression guard when shortcut code changes |
| Command sandbox | `src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor` | event payload dialog runtime aria-label evidence |
| Event debugger | `src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor` | event payload dialog runtime aria-label evidence |
| Browser interop | `src/Hexalith.EventStore.Admin.UI/wwwroot/js/interop.js` | shortcut registration and local-storage helpers |
| bUnit layout tests | `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/MainLayoutTests.cs` | deterministic shell/sidebar shortcut coverage |
| bUnit TypeCatalog tests | `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs` | URL sync and no-redirect-loop regression coverage |
| bUnit dialog tests | `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandSandboxTests.cs` and `EventDebuggerTests.cs` | dialog attribute regression-signal coverage |
| Browser E2E tests | `tests/Hexalith.EventStore.Admin.UI.E2E/BrowserSmokeTests.cs` and fixture files | browser-level shortcut/nav smoke coverage when feasible |
| Evidence folder | `_bmad-output/test-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups/` | screenshots, DOM snippets, console logs, Playwright traces, evidence index |

## Current Code Intelligence

- `MainLayout.razor` currently toggles `_sidebarCollapsed`, awaits `GetSidebarStorageKeyAsync().ConfigureAwait(false)`, awaits JS local-storage write with `ConfigureAwait(false)`, and then calls `StateHasChanged()`. This matches the deferred-work hypothesis that Ctrl+B can resume off the renderer synchronization context. Any fix must keep UI mutation and rerender dispatch safe for Blazor Server.
- `GetSidebarStorageKeyAsync` reads `hexalithAdmin.getViewportWidth` and maps width tiers to `optimal`, `standard`, `compact`, or `minimum`. Compact-tier default collapse behavior is intentional and should be preserved.
- `TypeCatalog.razor` already caches filtered lists, has URL idempotency checks in `UpdateUrl`, uses `pushHistory: true` for tab and cross-tab navigation, and subscribes to `DashboardRefreshService.OnDataChanged`. The page still renders `FluentDataGrid Items="@_filteredEvents.AsQueryable()"` and equivalents, so browser evidence should determine whether this remains a runtime issue before changing it.
- `TypeCatalogPageTests` already cover deep-link initialization and `UpdateUrl` idempotency. DW5 should extend those tests only where the new fix has deterministic component behavior; browser-only symptoms need Playwright/manual evidence.
- `CommandSandbox.razor` and `EventDebugger.razor` both render `<FluentDialog ... aria-label="Event payload">` around `FluentDialogBody`. Existing bUnit checks can prove markup intent, but runtime DOM and assistive-technology behavior depend on the Fluent UI Blazor v5 rendered element and require live browser evidence.
- `BrowserSmokeTests` already verifies shell rendering, accessible navigation landmark, command-page navigation, and a 3-second shell budget. It is a natural place for an additional TypeCatalog nav/shortcut smoke if the fixture can run reliably in this environment.

## Latest Technical Notes

- Microsoft documents that Blazor uses a synchronization context for a single logical thread per circuit, and component updates triggered outside that context should use `InvokeAsync` to dispatch back to the renderer. This directly applies to Ctrl+B and any code path using `ConfigureAwait(false)` before `StateHasChanged`. Source: <https://learn.microsoft.com/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0>
- Microsoft also documents that `StateHasChanged` can only be called from the renderer synchronization context and that `InvokeAsync` is required when logic escapes the context, including after `ConfigureAwait(false)`. Source: <https://learn.microsoft.com/aspnet/core/blazor/components/rendering?view=aspnetcore-10.0>
- Fluent UI Blazor v5 redesigned several components and removed or renamed v4 APIs. Keep v5 naming such as `ButtonAppearance`, `TextTag`, `TextSize`, and `TextWeight`; do not bring back the v4 `Typo`/`Typography` pattern.
- Fluent UI Blazor `FluentDialog` guidance expects `FluentDialogBody` to contain title, content, and action templates, and modal dialogs should contain focusable elements. Runtime DOM evidence is still required for the repository's `aria-label` claim.
- Fluent UI Blazor `FluentDataGrid` accepts `IQueryable<T>` through `Items`; when behavior depends on browser rendering and Fluent internals, prefer evidence-driven changes over broad component replacement.

## Party-Mode Hardening Notes

- Treat DW5 as a closure pass with explicit dispositions, not an open-ended Admin UI rewrite. Each selected deferred-work item must finish as `fixed-with-evidence`, `not-reproduced-with-evidence`, or `deferred-with-target-and-reason`.
- Runtime evidence must be first-class. The evidence index should list artifact filenames, route or dialog tested, viewport size, interaction path, pass/fail result, console/circuit status, and the related acceptance criteria.
- TypeCatalog changes must be evidence-led. Do not replace FluentDataGrid, remove URL-driven tab state, or rewrite navigation unless the browser run ties that specific behavior to the sidebar navigation block.
- Ctrl+B is a targeted Blazor renderer-context fix. Keep component state mutation and rerender dispatch on the renderer context, preserve viewport-tier storage keys, and prove Ctrl+K still opens, closes, and reopens in the same browser session.
- Dialog accessibility evidence must distinguish component markup, live Fluent DOM, keyboard/focus behavior, and assistive-technology coverage. Do not claim full AT verification unless an AT tool or equivalent accessibility-tree evidence was actually captured.
- Localization and broader Admin UI text changes are out of scope unless existing visible text is directly touched by the narrow fix.
- Stop and record follow-up rather than absorbing backend/API/DAPR/MCP/evidence-schema work, generalized shortcut architecture, generalized Fluent UI migration work, or DW2/DW3/DW4/DW6 decisions.

## Advanced Elicitation Hardening Notes

The 2026-05-05 advanced-elicitation pass treated the party-mode notes as the baseline and tightened only the implementation handoff. These notes are binding for dev-story execution unless a human product or architecture decision supersedes them.

### Evidence-First Stop Rules

- Start with a pre-edit route and shortcut decision ledger. For TypeCatalog, record each tested starting tab, sidebar target, expected route marker, observed URL, visible page marker, elapsed time, and console/circuit result before deciding `patch-now` versus `not-reproduced-with-evidence`.
- A fix is allowed only for a reproduced DW5 defect or for deterministic bUnit coverage that protects a reproduced defect. Do not patch TypeCatalog, shortcut infrastructure, or dialog markup from hypothesis alone.
- Browser evidence must preserve the first failed observation before retrying. If a retry passes, keep both rows and classify the first failure instead of erasing it as "flaky".
- Stop and defer rather than coding if the path requires Admin.Server contract changes, new seed-data APIs, broad Fluent component replacement, global shortcut architecture, or new accessibility tooling integration.

### Runtime Proof Requirements

- TypeCatalog navigation proof must show both URL transition and rendered-page transition. A URL-only change is insufficient if the old TypeCatalog content remains visible; a DOM-only change is insufficient if navigation history or query state is wrong.
- Ctrl+B proof must include repeated toggle behavior, viewport-tier storage key, refresh persistence, and absence of renderer-context or SignalR circuit errors. Ctrl+K proof must run in the same browser session after Ctrl+B.
- Dialog accessibility proof must name the evidence tier: component markup, live DOM, browser accessibility snapshot, keyboard/focus behavior, and assistive-technology pass. Only the tiers actually captured may be claimed.
- If stream data is unavailable for CommandSandbox or EventDebugger dialogs, record the blocker separately for each dialog. Do not let one unavailable dialog block or falsely close the other.

### Review Handoff

- Reviewers should reject DW5 completion if deferred-work dispositions are narrative-only, if evidence artifacts are not linked from the index, if TypeCatalog proof lacks both URL and visible content checks, if shortcut proof omits persistence, or if dialog accessibility claims exceed captured evidence.
- The final Dev Agent Record should identify which closure mode was used for each selected deferred item: `fixed-with-evidence`, `not-reproduced-with-evidence`, or `deferred-with-target-and-reason`.
- Keep screenshots, DOM extracts, console snippets, and Playwright traces sanitized. Do not store bearer tokens, event payloads, customer-sensitive identifiers, local storage secrets, or raw actor state.

## Tasks / Subtasks

- [ ] Task 0: Baseline DW5 and decide closure shape (AC: #1, #13, #14)
    - [ ] 0.1 Re-read Proposal F / DW5 and the selected `## Epic 21 Follow-ups` deferred-work entries.
    - [ ] 0.2 Classify each selected deferred item as `patch-now`, `evidence-now`, `accepted-debt`, `obsolete-resolved`, `duplicate`, or `not-DW5`.
    - [ ] 0.3 Confirm DW2, DW3, DW4, and DW6 scopes are excluded.
    - [ ] 0.4 Create the DW5 evidence folder and an evidence index stub before runtime work begins.
    - [ ] 0.5 Define the final disposition vocabulary in the evidence index before changing code: `fixed-with-evidence`, `not-reproduced-with-evidence`, or `deferred-with-target-and-reason`.
    - [ ] 0.6 Create a pre-edit decision ledger for TypeCatalog route proof, shortcut proof, dialog evidence tiers, and data/AT blockers before production edits.

- [ ] Task 1: Reproduce and close TypeCatalog navigation blocking (AC: #2, #3, #4, #10, #11)
    - [ ] 1.1 Start the Admin UI through Aspire when feasible and navigate to `/types`, `/types?tab=commands`, and `/types?tab=aggregates`.
    - [ ] 1.2 Click at least three sidebar links from each tested TypeCatalog tab and record URL/page-change timing plus console errors.
    - [ ] 1.3 Define blocking evidence as at least one of: route unchanged after bounded wait, visible page unchanged after click, render hang, console/circuit exception, data-grid interaction lock, focus trap, or repeated-click requirement.
    - [ ] 1.4 If blocked, isolate whether URL sync, refresh subscription, grid item source, viewport checks, or another render loop causes the issue.
    - [ ] 1.5 Apply the smallest fix and keep deep-link, selection, search, and history behavior intact.
    - [ ] 1.6 Add bUnit or Playwright coverage for the fixed behavior where deterministic.
    - [ ] 1.7 If not reproduced, record the tested starting page, tab, sidebar target, before/after URL, visible page marker, viewport, timing, and console result.
    - [ ] 1.8 If patched, prove the same route matrix after the fix and include at least one browser history or deep-link check so URL synchronization remains honest.

- [ ] Task 2: Fix Ctrl+B sidebar toggle without regressing Ctrl+K (AC: #5, #6, #7, #11)
    - [ ] 2.1 Reproduce Ctrl+B in browser and capture the console/circuit symptom before fixing when possible.
    - [ ] 2.2 Update `MainLayout.razor` so shortcut state mutation, JS storage write, and rerender happen on the correct Blazor context.
    - [ ] 2.3 Preserve viewport-tier storage keys and compact-default collapse behavior.
    - [ ] 2.4 Verify Ctrl+B collapse/expand across repeated presses and refresh.
    - [ ] 2.5 Verify Ctrl+K still opens, closes, and reopens the command palette in the same browser session.
    - [ ] 2.6 Record the storage key observed for the current viewport tier and confirm no renderer-thread or SignalR circuit exception occurs.
    - [ ] 2.7 If the first browser attempt does not reproduce Ctrl+B failure, still validate the current implementation against the same storage, refresh, and console/circuit evidence before choosing `not-reproduced-with-evidence`.

- [ ] Task 3: Complete dialog accessibility runtime evidence (AC: #8, #9, #10, #11, #12)
    - [ ] 3.1 Use a stream/command setup that opens the CommandSandbox event-payload dialog, or record why it is unavailable.
    - [ ] 3.2 Use a stream setup that opens the EventDebugger event-payload dialog, or record why it is unavailable.
    - [ ] 3.3 Capture DOM evidence showing whether `aria-label="Event payload"` lands on the rendered Fluent dialog element.
    - [ ] 3.4 Run an assistive-technology check when tooling is available; otherwise record the explicit blocker and classify as partial evidence.
    - [ ] 3.5 Keep or add bUnit regression-signal tests for both dialog attributes.
    - [ ] 3.6 Record keyboard/focus behavior for dialog entry and close, including Escape or close-button behavior when available.
    - [ ] 3.7 When available, capture a browser accessibility snapshot or equivalent tree evidence and label it separately from full assistive-technology verification.

- [ ] Task 4: Update deferred-work and evidence artifacts narrowly (AC: #10, #13, #15)
    - [ ] 4.1 Save runtime evidence under `_bmad-output/test-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups/`.
    - [ ] 4.2 Include an evidence index with dated artifact filenames, browser route, viewport size, interaction path, console/circuit status, and AC mapping.
    - [ ] 4.3 Mark only DW5-relevant deferred-work bullets with disposition markers.
    - [ ] 4.4 Mark obsolete Epic 21 resolved entries as `RESOLVED` or `NO-ACTION` with a one-line rationale.
    - [ ] 4.5 Do not sweep unrelated deferred-work sections into this story.
    - [ ] 4.6 Link each deferred-work disposition to one evidence row or blocker row in the DW5 evidence index.

- [ ] Task 5: Validate and close bookkeeping (AC: #11, #15)
    - [ ] 5.1 Run targeted `tests/Hexalith.EventStore.Admin.UI.Tests` bUnit tests for changed components.
    - [ ] 5.2 Run `tests/Hexalith.EventStore.Admin.UI.E2E` Playwright tests or record environment blockers.
    - [ ] 5.3 Run the Admin UI project build if production UI code changed.
    - [ ] 5.4 Update this story's Dev Agent Record, File List, Change Log, Verification Status, and sprint-status row at dev handoff.
    - [ ] 5.5 Record final per-item closure modes and reject the handoff if any selected DW5 item lacks evidence, blocker, or deferred-work disposition.

## Dev Notes

### Architecture Guardrails

- Admin.Server remains the single API backing the Admin UI; DW5 should not add direct DAPR access from the UI.
- Keep Admin UI behavior browser-verifiable. bUnit tests are useful for contracts, but TypeCatalog navigation blocking and shortcut failures are runtime symptoms.
- Do not bypass Blazor Server's renderer context. Use `InvokeAsync` or remove `ConfigureAwait(false)` in UI event paths where component state or rendering is touched.
- Keep TypeCatalog URL state deliberate: path comparisons may be case-insensitive, but query values are case-sensitive because type names may differ by case.
- Do not hide console/circuit errors by swallowing exceptions without fixing the root behavior.
- Accessibility claims must be evidence-scoped: DOM attribute present, keyboard behavior, and AT pass are separate claims.

### Previous Story Intelligence

- Story 21-13 fixed Ctrl+K command palette reopen behavior and verified it in a browser; DW5 must keep that behavior intact.
- Story 21-13 also added TypeCatalog URL idempotency and history behavior, but later runtime evidence still found `/types` sidebar navigation blocking. Treat both facts as true until the current browser run proves otherwise.
- The deferred-work entry for CommandPalette dialog `aria-label` was browser-verified; CommandSandbox and EventDebugger remained deferred because a cold-start environment had no usable stream/command data.
- DW1-DW4 established the deferred cleanup pattern: close only the selected cluster, use disposition markers, and route unrelated pressure to a later DW story or accepted debt.

### Testing Guidance

- Prefer targeted bUnit tests for MainLayout shortcut methods, TypeCatalog URL invariants, and dialog markup contracts.
- Prefer Playwright or manual browser evidence for Ctrl+B, Ctrl+K, TypeCatalog sidebar navigation, Fluent web-component DOM, and console/circuit error checks.
- If the Aspire environment is used, follow repository guidance: start with `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` when Keycloak is not needed, and use the resource endpoint for Admin UI.
- Do not run solution-level `dotnet test`; run affected test projects individually.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md#Proposal-F-DW5-Admin-UI-Runtime-Follow-Ups`] - DW5 scope and acceptance direction.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md#Epic-21-Follow-ups`] - raw TypeCatalog, Ctrl+B, and dialog accessibility follow-ups.
- [Source: `_bmad-output/implementation-artifacts/21-13-ui-bug-fixes-batch.md`] - original Epic 21 follow-up context and scope-lock rules.
- [Source: `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor`] - Ctrl+B shortcut and sidebar persistence.
- [Source: `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor`] - TypeCatalog tab, filter, URL, and grid behavior.
- [Source: `src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor`] - CommandSandbox payload dialog.
- [Source: `src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor`] - EventDebugger payload dialog.
- [Source: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs`] - existing TypeCatalog URL regression tests.
- [Source: `tests/Hexalith.EventStore.Admin.UI.E2E/BrowserSmokeTests.cs`] - browser-level Admin UI smoke test pattern.
- [Source: `Directory.Packages.props`] - pinned Fluent UI Blazor, Icons, bUnit, and Playwright versions.
- [Source: Microsoft Learn Blazor synchronization context](https://learn.microsoft.com/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0) - renderer-context guidance.
- [Source: Microsoft Learn Blazor rendering](https://learn.microsoft.com/aspnet/core/blazor/components/rendering?view=aspnetcore-10.0) - `StateHasChanged` and `InvokeAsync` guidance.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Pre-dev hardening preflight: `_bmad-output/process-notes/predev-preflight-latest.json`, timestamp `2026-05-04T18:25:31Z`, result `fail`.
- Soft working-tree warning from preflight JSON `working tree cleanliness` stdout listed `_bmad-output/test-artifacts/test-review.md` and `_bmad-output/test-artifacts/archive/test-review-2026-04-18.md`. These paths were outside BMAD pre-dev story-operation paths and were left untouched.

### Completion Notes List

- Created ready-for-dev story from first backlog row after DW4 in the Post-Epic Deferred Work Cleanup package.
- No implementation work has been performed for this story.
- No `project-context.md` file was present in the repository at story creation.
- Advanced elicitation applied low-risk handoff clarifications for evidence-first stop rules, TypeCatalog URL and visible-content proof, shortcut persistence proof, dialog evidence tiers, and reviewer rejection criteria.

### File List

- `_bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Party-mode review and advanced elicitation traces are recorded inline; no status change was required.
- Markdown, targeted bUnit, and browser evidence validation should be run before dev handoff if local tooling is available.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-04 | 0.1 | Created ready-for-dev DW5 Admin UI runtime follow-ups story. | Codex automation |
| 2026-05-05 | 0.2 | Applied party-mode hardening for runtime evidence, disposition rules, renderer-context closure, and scope stop signs. | Codex automation |
| 2026-05-05 | 0.3 | Applied advanced-elicitation hardening for evidence-first stop rules, route proof, shortcut persistence, and dialog evidence tiers. | Codex automation |

## Party-Mode Review

- Date/time: 2026-05-05T05:09:56+02:00
- Selected story key: `post-epic-deferred-dw5-admin-ui-runtime-follow-ups`
- Command/skill invocation used: `/bmad-party-mode post-epic-deferred-dw5-admin-ui-runtime-follow-ups; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor); Sally (UX Designer) was requested but did not return before the review timeout.
- Findings summary: reviewers converged on `needs-story-update`; DW5 is directionally sound, but the development handoff needed sharper runtime evidence criteria, explicit per-item dispositions, concrete TypeCatalog blocking definitions, targeted Blazor renderer-context expectations for Ctrl+B, runtime Fluent dialog accessibility evidence, and stronger stop signs against absorbing adjacent Admin UI, backend, DAPR, MCP, evidence-schema, or deferred-work governance scope.
- Changes applied: added Party-Mode Hardening Notes; tightened task details for disposition vocabulary, TypeCatalog blocking evidence, Ctrl+B/Ctrl+K verification, dialog focus/accessibility evidence, and the evidence index; added change-log row.
- Findings deferred: exact browser evidence capture mechanism; whether TypeCatalog requires a code fix or a not-reproduced disposition; whether assistive-technology tooling is available during development; any broad FluentDataGrid, shortcut architecture, backend/API/DAPR/MCP, evidence-schema, localization, or DW2/DW3/DW4/DW6 follow-up decisions.
- Final recommendation: needs-story-update

## Advanced Elicitation

- Date/time: 2026-05-05T10:02:42+02:00
- Selected story key: `post-epic-deferred-dw5-admin-ui-runtime-follow-ups`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-deferred-dw5-admin-ui-runtime-follow-ups`
- Batch 1 method names: Self-Consistency Validation; Red Team vs Blue Team; Architecture Decision Records; Security Audit Personas; Failure Mode Analysis.
- Reshuffled Batch 2 method names: Chaos Monkey Scenarios; Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Lessons Learned Extraction.
- Findings summary: The story was directionally ready after party-mode review, but elicitation exposed five handoff risks: patches could be made from TypeCatalog hypotheses rather than reproduced evidence, route proof could overclaim from URL-only changes, Ctrl+B proof could miss storage persistence and same-session Ctrl+K regression, dialog accessibility claims could exceed captured evidence tiers, and deferred-work dispositions could remain narrative-only.
- Changes applied: Added Advanced Elicitation Hardening Notes for evidence-first stop rules, runtime proof requirements, and review rejection criteria. Tightened Tasks 0.6, 1.8, 2.7, 3.7, 4.6, and 5.5, and updated Completion Notes, Verification Status, and Change Log.
- Findings deferred: Exact browser evidence capture mechanism, whether TypeCatalog is patched or closed as not reproduced, assistive-technology availability, seed data availability for each dialog, and any broad Admin UI, backend/API, DAPR, MCP, evidence-schema, or deferred-work governance changes remain out of scope until separate product or architecture decisions approve them.
- Final recommendation: ready-for-dev
