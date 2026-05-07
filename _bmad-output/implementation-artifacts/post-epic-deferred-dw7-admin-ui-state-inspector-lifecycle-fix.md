# Post-Epic Deferred DW7: Admin UI State Inspector Lifecycle Fix

Status: ready-for-dev

<!-- Source: sprint-change-proposal-2026-05-07-deferred-work-open-cleanup.md - Proposal B / DW7 -->
<!-- Source: deferred-work.md - admin-ui-state-inspection-cluster-fix follow-up lifecycle entries -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an EventStore administrator,
I want the State Inspector dialog to fail closed when its Fluent dialog show lifecycle fails,
so that the Admin UI does not repeatedly retry an invisible dialog or leave the stream detail page stuck in a modal-open state.

## Story Context

DW7 is the first story from the approved Deferred-Work OPEN cleanup package. The triggering defects are two routed deferred-work entries from the 2026-05-07 follow-up review of `admin-ui-state-inspection-cluster-fix`:

- `OnAfterRenderAsync` catches a `ShowAsync` failure, sets `_fetchError`, but leaves `_pendingShow = true`. The rendered error can cause another render, which retries `ShowAsync` again and can repeat indefinitely on persistent JS interop failure.
- The same failure path leaves the host page's "show modal" gate open because `OnDialogStateChange.Closed` is never fired. The operator may see no dialog while the page still believes the inspector is open.

This is a narrow Admin UI lifecycle story. It must not reopen the broader state/diff/causation endpoint work, TypeCatalog shortcut work, DW5 dialog accessibility debt, DW6 governance tooling, or the manual Aspire smoke evidence operator action.

Current HEAD at story creation: `fe03cb2e`.

## Acceptance Criteria

1. **Show failure does not retry indefinitely.** Given `StateInspectorModal.OnAfterRenderAsync` attempts to show the Fluent dialog and `_dialog.ShowAsync()` throws, when the component renders again, then `_pendingShow` is false and the component does not call `ShowAsync` again for that same failed open attempt. The failed show path must clear the pending-show state before any render-triggering error-state update, host close callback, or disposal cascade can re-enter the show attempt. Do not add a retry loop, timer, or background retry policy for this failure.

2. **Show failure leaves a visible component-level error.** Given `ShowAsync` fails, then the modal component renders the existing failure copy, "Could not open the state inspector dialog. Try again.", in the error region and logs a warning with stream metadata only. The error must stay in the existing flat Fluent UI Blazor v5 dialog body structure and must not introduce nested cards, custom modal chrome, or a new localization system. It must not log aggregate state JSON, event payloads, JWTs, or local-storage values.

3. **Host gate closes exactly once on show failure.** Given the parent rendered `StateInspectorModal` because its host state said "show inspector", when `ShowAsync` fails, then the modal invokes the close callback once so the parent can collapse its modal-open gate. A persistent JS interop failure must not emit repeated close callbacks on later renders, even if the error state causes additional renders or the parent disposes the modal after the callback. The duplicate-notification guard must be set before awaiting or invoking the callback so a re-entrant render cannot produce a second notification.

4. **Normal open and user close behavior stays intact.** Given `ShowAsync` succeeds, when the operator uses Inspect, sequence stepping, timestamp mode, or the close button, then existing behavior remains unchanged: the dialog stays open after successful inspection, stream identity remains visible, and `OnClose` is invoked by the normal Fluent `DialogState.Closed` path. If the implementation shares close logic between failure and normal close paths, it must guard duplicate close notifications. The show-failure path should not call `HideAsync` for a dialog that never opened unless a focused test proves Fluent requires it.

5. **The fix stays local to StateInspectorModal lifecycle.** The implementation may touch `StateInspectorModal.razor`, its focused tests, this story, sprint status, and deferred-work disposition bookkeeping. It must not change Admin Server contracts, EventStore state/diff/causation endpoints, `AdminStreamApiClient`, `StateDiffViewer`, `EventDetailPanel`, sidebar shortcut code, DAPR/Aspire configuration, or Fluent UI package versions unless a failing focused test proves a directly related lifecycle dependency. Do not change the `StreamDetail` host gate beyond the minimum callback wiring needed to observe the existing close event.

6. **Focused bUnit coverage proves the failure path.** Add or extend tests in `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StateInspectorModalTests.cs` that prove:
   - `ShowAsync` throwing `JSDisconnectedException` or an equivalent JS interop failure does not call `ShowAsync` again after a follow-up render.
   - The host close callback fires exactly once on show failure.
   - The user-facing error is rendered after the failed open.
   - The existing flat Fluent UI Blazor v5 dialog body structure remains covered.
   - A parent callback that triggers another render or removes the modal still observes exactly one close notification and no second show attempt.

7. **Deferred-work dispositions are auditable.** When development starts or completes, update only the DW7-owned entries in `_bmad-output/implementation-artifacts/deferred-work.md`: lines currently routed to `STORY:post-epic-deferred-dw7-admin-ui-state-inspector-lifecycle-fix` for the infinite retry and stuck host gate, plus the related `No bUnit coverage for OnAfterRenderAsync ShowAsync failure path` test-coverage entry. Do not sweep unrelated accepted-debt lifecycle or UX bullets.

8. **Validation is targeted and recorded.** Before moving to `review`, run `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~StateInspectorModalTests"` and then the full Admin UI test project if the focused run passes. Record exact commands and results in the Dev Agent Record. If the full project is blocked by a pre-existing baseline issue, record the blocker and keep the focused lifecycle tests green.

9. **Bookkeeping is closed.** At dev handoff, update this story's Dev Agent Record, File List, Verification Status, and Change Log. Move this story and its sprint-status row to `review` only after the lifecycle fix, focused tests, deferred-work dispositions, and validation evidence are recorded. Move both to `done` only after code-review signoff.

## Scope Boundaries

- Do not redesign the state inspector, state diff, event detail, or causation experiences.
- Do not change the Fluent UI Blazor package version or migrate to a different dialog architecture unless the local failure path cannot be fixed safely.
- Do not claim manual no-clipping Aspire smoke evidence. OA1 remains an operator action in `deferred-work.md`.
- Do not touch DW8 server classifier / ULID audit or DW9 evidence-validator / governance CI work.
- Do not initialize or update nested submodules.
- Do not edit generated preflight JSON audit files.

## Implementation Inventory

| Area | File / artifact | Expected use |
| --- | --- | --- |
| Planning source | `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-deferred-work-open-cleanup.md` | Proposal B / DW7 scope and acceptance direction |
| Deferred source | `_bmad-output/implementation-artifacts/deferred-work.md` | DW7 routed defects and related test-coverage entry |
| Parent stream detail page | `src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor` | host gate and `OnClose` consumer |
| State inspector component | `src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor` | primary lifecycle fix |
| Component tests | `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StateInspectorModalTests.cs` | focused bUnit regression coverage |
| Test base | `tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs` | existing bUnit service/test wiring |
| Sprint status | `_bmad-output/implementation-artifacts/sprint-status.yaml` | story status bookkeeping only |
| Run log | `_bmad-output/process-notes/predev-hardening-runs.log` | automation-created run trace |

## Current Code Intelligence

- `StateInspectorModal.razor` currently stores `_pendingShow = true` and shows the dialog from `OnAfterRenderAsync` when `_dialog is not null`.
- The current show path awaits `_dialog.ShowAsync().ConfigureAwait(false)` and sets `_pendingShow = false` only after the await succeeds.
- The catch block sets `_fetchError = "Could not open the state inspector dialog. Try again."` and logs a warning, but it intentionally leaves `_pendingShow = true`; that is the bug DW7 owns.
- `OnDialogStateChangeAsync` invokes `OnClose` only when Fluent reports `DialogState.Closed`. A failed `ShowAsync` does not necessarily produce that state-change event, so the host gate must be closed explicitly or through a shared helper that is idempotent.
- `HandleCloseAsync` currently calls `_dialog.HideAsync()`. Keep this path intact for normal user close behavior unless the implementation extracts an idempotent close helper used by both failure and normal close paths.
- `StateInspectorModalTests.cs` already verifies prefilled sequence rendering, flat v5 dialog body structure, fetch success, no-state, timestamp toggle visibility, stays-open behavior, stream identity visibility, backend-unavailable copy, and sign-in-required copy.
- Existing tests substitute `AdminStreamApiClient` and render the component through `AdminUITestContext`. Prefer extending this test file instead of creating a new test project.
- If bUnit cannot directly force `FluentDialog.ShowAsync()` failure through JSInterop, introduce the smallest internal test seam that keeps production behavior identical and is scoped to the dialog show operation. Record the seam in Dev Agent Record if used.

## Latest Technical Notes

- Repo package pins at story creation: `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.2-26098.1`, `Microsoft.FluentUI.AspNetCore.Components.Icons` `4.14.0`, bUnit `2.7.2`, xUnit v3 `3.2.2`, and NSubstitute `5.3.0`. Source: `Directory.Packages.props`.
- NuGet currently lists `Microsoft.FluentUI.AspNetCore.Components` stable `4.14.0` and prerelease v5 builds; keep the repo's pinned v5 RC and do not downgrade for this fix. Source: [NuGet package page](https://www.nuget.org/packages/Microsoft.FluentUI.AspNetCore.Components).
- Microsoft Learn documents that Blazor uses a synchronization context for a single logical thread per circuit and that component work triggered outside that context should use `InvokeAsync` to dispatch back to the renderer. Source: [Blazor synchronization context](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0).
- Microsoft Learn rendering guidance says `StateHasChanged` must run on the renderer synchronization context and `InvokeAsync` is required when logic escapes that context, including after `ConfigureAwait(false)`. Source: [Blazor component rendering](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/rendering?view=aspnetcore-10.0).

## Tasks / Subtasks

- [ ] Task 0: Baseline the failure and lock scope (AC: #1, #3, #5, #7)
    - [ ] 0.1 Re-read Proposal B / DW7 and the three DW7-related deferred-work bullets.
    - [ ] 0.2 Confirm current `StateInspectorModal.razor` show path still leaves `_pendingShow = true` on failure before editing.
    - [ ] 0.3 Identify how `StreamDetail.razor` hosts `StateInspectorModal` and how `OnClose` collapses the parent gate.
    - [ ] 0.4 Record that OA1 manual clipping smoke evidence remains out of scope.

- [ ] Task 1: Add the local lifecycle fix (AC: #1, #2, #3, #4, #5)
    - [ ] 1.1 Make the failed show path idempotent by setting `_pendingShow = false` before, during, or immediately after the first show attempt so a failure cannot re-enter endlessly.
    - [ ] 1.2 Preserve the existing user-facing error copy and warning log, with metadata-only logging.
    - [ ] 1.3 Invoke the host close callback once when show fails, either directly or through a small idempotent close helper.
    - [ ] 1.4 Ensure a successful show path still supports Inspect, sequence stepping, timestamp mode, and normal close.
    - [ ] 1.5 Avoid broad component redesign or unrelated UI copy changes.
    - [ ] 1.6 Keep lifecycle state mutations on the Blazor renderer context; if existing `ConfigureAwait(false)` calls are touched, remove them or marshal state updates through `InvokeAsync`.

- [ ] Task 2: Add focused regression tests (AC: #1, #3, #4, #6)
    - [ ] 2.1 Add a test that simulates `ShowAsync` JS interop failure and proves no repeated show call occurs after a follow-up render.
    - [ ] 2.2 Add a host-gate test that counts `OnClose` and proves exactly one close notification on failed show.
    - [ ] 2.3 Add or extend an assertion that the failed show error copy is rendered inside the existing flat Fluent UI Blazor v5 dialog body structure.
    - [ ] 2.4 Keep the existing flat Fluent UI Blazor v5 DOM test green and assert warning logs remain metadata-only where the test harness can observe logging.
    - [ ] 2.5 If a tiny test seam is introduced, test through the rendered component and document why JSInterop alone was insufficient.
    - [ ] 2.6 Include a re-entrant parent-render or parent-removal case so exactly-once close behavior is proven across the failure callback boundary.

- [ ] Task 3: Update deferred-work dispositions narrowly (AC: #7)
    - [ ] 3.1 Update the infinite-retry and stuck-host-gate entries only after the code/test fix lands.
    - [ ] 3.2 Promote or resolve the related bUnit coverage entry according to actual test coverage.
    - [ ] 3.3 Do not edit adjacent accepted-debt StateInspector UX/lifecycle bullets unless a direct code change resolves them.

- [ ] Task 4: Validate and capture evidence (AC: #8)
    - [ ] 4.1 Run `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~StateInspectorModalTests"`.
    - [ ] 4.2 If focused tests pass, run `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release`.
    - [ ] 4.3 If test execution is blocked by a pre-existing build/environment issue, record the exact command and first blocker.
    - [ ] 4.4 Confirm generated preflight JSON files remain unstaged.

- [ ] Task 5: Close story bookkeeping (AC: #9)
    - [ ] 5.1 Update Dev Agent Record, File List, Verification Status, and Change Log.
    - [ ] 5.2 Update `sprint-status.yaml` only when moving from implementation to review or done.
    - [ ] 5.3 Confirm no nested submodules were initialized or updated.

## Dev Notes

### Architecture Guardrails

- This is a Blazor component lifecycle fix, not a product-contract change.
- Do not add retries. The defect is an accidental lifecycle retry loop; the desired behavior is fail closed and unblock the host.
- Keep component logs metadata-only per SEC-5. Authorized state JSON may render in the UI, but logs must not contain payload or state content.
- Prefer idempotent state transitions over timing assumptions. A persistent JS interop failure should leave the component in one stable failed state.
- Avoid `.ConfigureAwait(false)` on Blazor component lifecycle paths when subsequent code mutates component state or participates in rendering. If existing awaits are touched, use the Microsoft Learn renderer-context guidance above.
- Treat close notification as a one-way lifecycle signal. Set the duplicate guard before awaiting user callbacks, and keep the failure path stable even when the callback causes the parent to stop rendering the modal.
- Any test seam for `ShowAsync` must stay local to the component's dialog-show operation and must not become a new product service, dependency-injection contract, or generic dialog abstraction.

### Testing Guidance

- Start with the existing `StateInspectorModalTests` fixture and service wiring.
- Test behavior, not implementation comments: count show attempts if a reliable seam exists, count close-callback invocations, and assert visible error copy.
- Keep test names specific to DW7 so later review can distinguish lifecycle coverage from the existing v5 DOM structure tests.
- Do not run solution-level `dotnet test`.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-deferred-work-open-cleanup.md#Proposal-B-DW7-Admin-UI-StateInspectorModal-Lifecycle-Fix`] - DW7 scope and acceptance direction.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md#Deferred-from-code-review-of-admin-ui-state-inspection-cluster-fix-follow-up-2026-05-07`] - routed infinite-retry, stuck-host-gate, and missing-test entries.
- [Source: `_bmad-output/implementation-artifacts/admin-ui-state-inspection-cluster-fix.md`] - parent state-inspection story and recent review context.
- [Source: `_bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md`] - adjacent Admin UI runtime evidence and scope-boundary precedent.
- [Source: `src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor`] - component to update.
- [Source: `src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor`] - host gate for `StateInspectorModal`.
- [Source: `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StateInspectorModalTests.cs`] - focused regression test location.
- [Source: `Directory.Packages.props`] - Fluent UI Blazor, bUnit, xUnit, and NSubstitute package pins.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Pre-dev hardening preflight: `_bmad-output/process-notes/predev-preflight-latest.json`, timestamp `2026-05-07T05:47:55Z`, result `pass`.
- Create-story activation: resolved workflow customization with no prepend/append steps; no `project-context.md` file was present in the workspace.
- Aspire pre-edit baseline attempt: `aspire run --detach --non-interactive --apphost src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json` stopped a prior instance, then failed to build. First blocker in `C:\Users\JeromePiquot\.aspire\logs\cli_20260507T054915013_detach-child_103d8a4ef49942c1958e93734468d4e0.log`: `CS0009 Metadata file 'D:\Hexalith.EventStore\src\Hexalith.EventStore.Contracts\obj\Debug\net10.0\ref\Hexalith.EventStore.Contracts.dll' could not be opened -- PE image doesn't contain managed metadata`, followed by missing `Hexalith.EventStore.Contracts` namespace/type errors in Client, Admin.Abstractions, SignalR, and Hexalith.Tenants projects. No apphost code was changed by this story creation run.

### Completion Notes List

- Created ready-for-dev story from first backlog row in the Post-Epic Deferred Work OPEN Cleanup package.
- Scoped DW7 to the `StateInspectorModal` failed-show lifecycle bug and the paired host-gate/test-coverage entries.
- Recorded current package pins and Microsoft Blazor renderer-context guidance for the future developer.

### File List

- `_bmad-output/implementation-artifacts/post-epic-deferred-dw7-admin-ui-state-inspector-lifecycle-fix.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- AppHost baseline run attempted before edits but blocked by the existing Debug ref assembly metadata failure described in Debug Log References.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, or submodules.

## Party-Mode Review

- Date: 2026-05-07T08:11:37+02:00
- Selected story key: `post-epic-deferred-dw7-admin-ui-state-inspector-lifecycle-fix`
- Command / skill invocation used: `/bmad-party-mode post-epic-deferred-dw7-admin-ui-state-inspector-lifecycle-fix; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Sally (UX Designer)
- Findings summary: Reviewers agreed the story is narrow and implementation-ready once the close-on-failure contract is explicit. `ShowAsync` failure must be terminal for the current open attempt, must clear `_pendingShow` before render-triggering error state can retry, must notify the host exactly once, must keep success-path dialog behavior intact, and must keep the error UI in the existing flat Fluent UI Blazor v5 body structure. Logs must remain metadata-only.
- Changes applied: Clarified AC1 pending-show ordering and at-most-once show attempt semantics; clarified AC2 visible error/body-structure/logging expectations; clarified AC3 exactly-once host close behavior across rerenders or parent disposal; clarified AC4 duplicate-close guard if close logic is shared; tightened Task 2 test expectations for flat body structure and metadata-only logging.
- Findings deferred: Broader DW5 accessibility debt; localization-system expansion unless already used by adjacent component copy; endpoint, TypeCatalog, Aspire/DAPR, package-version, and governance changes; advanced elicitation completion, which remains a later separate pass under L08.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date: 2026-05-07T09:14:41+02:00
- Selected story key: `post-epic-deferred-dw7-admin-ui-state-inspector-lifecycle-fix`
- Command / skill invocation used: `/bmad-advanced-elicitation post-epic-deferred-dw7-admin-ui-state-inspector-lifecycle-fix`
- Batch 1 methods: Self-Consistency Validation; Red Team vs Blue Team; Architecture Decision Records; Security Audit Personas; Failure Mode Analysis.
- Batch 2 methods: Chaos Monkey Scenarios; Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Lessons Learned Extraction.
- Findings summary: The story already scoped the bug well, but needed stronger ordering around `_pendingShow`, close-callback idempotency, re-entrant parent renders, and renderer-context safety. The highest-risk failure mode is a show-failure callback causing disposal or rerender before duplicate guards are set.
- Changes applied: Clarified that failed show attempts must clear pending state before render-triggering updates or close callbacks; required duplicate close guards before awaiting callbacks; constrained `HideAsync` use on never-opened dialogs; narrowed allowed `StreamDetail` changes; added re-entrant render/removal test coverage; added renderer-context and local test-seam guidance.
- Findings deferred: Broader dialog accessibility follow-ups, endpoint/state-inspection product changes, generic dialog abstractions, package-version changes, and manual Aspire smoke evidence remain outside DW7.
- Final recommendation: ready-for-dev

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-07 | 0.3 | Applied advanced elicitation and hardened lifecycle ordering, close idempotency, renderer-context, and re-entrant test guidance. | Codex automation |
| 2026-05-07 | 0.2 | Recorded party-mode review and clarified close-on-failure lifecycle/test contract. | Codex automation |
| 2026-05-07 | 0.1 | Created ready-for-dev DW7 Admin UI state inspector lifecycle story. | Codex automation |
