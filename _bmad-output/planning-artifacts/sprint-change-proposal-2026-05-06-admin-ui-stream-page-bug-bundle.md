# Sprint Change Proposal — Admin UI Stream Page Bug Bundle

**Date:** 2026-05-06
**Triggered by:** Hands-on testing of the Admin UI Stream Detail page (Trace Causation, Blame, Bisect, Step Through, Sandbox)
**Scope Classification:** Admin investigation reliability/usability fix (localized Admin UI only; no contract or backend changes)
**Risk Classification:** Medium, localized UI (one bug class terminates the Blazor Server circuit)
**Mode:** Batch (one consolidated story; proposals presented for review before remaining implementation)

---

## Section 1: Issue Summary

Three categories of bugs were discovered during hands-on testing of `/streams/{tenant}/{domain}/{aggregate}` in the Admin UI. They share a page but have independent root causes.

### Group A — Blazor Server threading bug (circuit crash)

Several `.razor` components combine `.ConfigureAwait(false)` on async API calls with a subsequent `StateHasChanged()` call (typically in a `finally` block). After the await, the continuation can resume on a thread-pool thread instead of the Blazor `Renderer.Dispatcher`. `StateHasChanged()` then throws:

```
System.InvalidOperationException: The current thread is not associated with the Dispatcher.
Use InvokeAsync() to switch execution to the Dispatcher when triggering rendering or component state.
```

The unhandled exception kills the Blazor Server **circuit**. After that, the entire page becomes non-interactive (no buttons respond, including the framework's "Reload Page" error UI), while the sidebar — which lives in a separate render tree — keeps working. This is a reliability failure in the operator's investigation workflow, not a cosmetic UI defect.

**Confirmed evidence (Aspire trace `5ca47908bbd36f5e77c651047959e790`):**

```
System.InvalidOperationException ... at ComponentBase.StateHasChanged()
   at CorrelationTraceMap.LoadTraceMapAsync()  CorrelationTraceMap.razor:line 315
   at CorrelationTraceMap.OnParametersSetAsync()  CorrelationTraceMap.razor:line 262
```

**Affected sites identified so far:**

| File | Method | Line(s) | Status |
|------|--------|---------|--------|
| `CorrelationTraceMap.razor` | `LoadTraceMapAsync` | 282 (await) + 315 (StateHasChanged) | ✅ **Fixed in this session** |
| `EventDetailPanel.razor` | `OpenInspector`, `DiffWithPrevious`, `TraceCausation` | 275, 280, 293 | ✅ **Fixed in this session** |
| `BlameViewer.razor` | `LoadBlameAsync` | 209 (await) + 237 (StateHasChanged) | ⏳ Pending |
| `BisectTool.razor` | `LoadFieldsAsync` | 337 (await) + 361 (StateHasChanged) | ⏳ Pending |

**User-visible symptoms reported:**
- Trace Causation: page hangs, all buttons inert, refresh shows "Something went wrong" with non-functional Reload Page → CorrelationTraceMap (already fixed).
- Blame: "ça charge à l'infini et fait bugger le tout / le bouton Close ne fonctionne pas" → BlameViewer (pending fix; same root cause).
- Bisect (latent risk for the "Load Fields" step in setup) → BisectTool (pending fix).

**Note on broader audit.** A repository-wide scan flagged 51 files using `.ConfigureAwait(false)`, including many in `Services/*` (which are correct — not Blazor lifecycle code). Within `.razor` components I verified BlameViewer and BisectTool have the dangerous pattern. `CommandSandbox.razor` uses `.ConfigureAwait(false)` only on `dialog.Show/Hide` and `cts.CancelAsync` — none followed by `StateHasChanged()` in the same path, so it is not currently affected. Create a follow-up item now for a `.razor` audit of `.ConfigureAwait(false)` before component-state mutation or `StateHasChanged()`.

### Group B — Mode-switch buttons don't reset other modes

`StreamDetail.razor` uses five mutually-exclusive bool flags for stream-page view modes: `_traceMode`, `_sandboxMode`, `_stepMode`, `_bisectMode`, `_blameMode` (plus `_diffMode`). The render block at lines 119–176 uses `if / else if` precedence:

```
Sandbox > Trace > Step > Bisect > Blame > Diff > Detail
```

The `Open*` methods reset *some* but not *all* other flags before setting their own. The asymmetric reset matrix:

| Method | Resets | Forgets |
|---|---|---|
| `OpenBlameViewer` (580) | `_diffMode` | `_sandboxMode`, `_stepMode`, `_bisectMode` |
| `OpenBisectTool` (607) | `_blameMode`, `_diffMode` | `_sandboxMode`, `_stepMode` |
| `OpenStepDebugger` (658) | `_bisectMode`, `_blameMode`, `_diffMode` | `_sandboxMode` |
| `OpenSandbox` (712) | `_stepMode`, `_bisectMode`, `_blameMode` | (`_diffMode` low-risk) |
| `OpenTraceMap` (742) | `_sandboxMode`, `_stepMode`, `_bisectMode`, `_blameMode`, `_diffMode` | ✓ correct |
| `OpenTraceMapForCorrelation` (759) | `_sandboxMode`, `_stepMode`, `_bisectMode`, `_blameMode`, `_diffMode` | ✓ correct |

**Consequence.** If the user is in Sandbox and clicks Bisect/Step/Blame, both flags become true and `_sandboxMode` wins the precedence — page does not switch. Same for Step → Blame, Bisect → Blame, Step → Bisect, etc. The trace-map handlers behave correctly today, but the invariant is only enforced there rather than across all mode transitions.

This matches the user report: *"quand je clique sur les autres boutons Bisect / Step Through / Sandbox et que j'essaye de cliquer sur un autre d'entre eux, la page ne change pas pour celle dont j'ai cliqué."*

### Group C — Copy / Trace button semantic mismatch

The 📋 button visually labelled "Copy correlation ID" doesn't actually copy. In `StreamDetail.razor:413`:

```razor
<EventDetailPanel ... OnCopyCorrelation="@OpenTraceMapForCorrelation" ... />
```

The `OnCopyCorrelation` callback is wired to `OpenTraceMapForCorrelation`, which navigates to `?trace=<id>` (opens the Correlation Trace Map). No clipboard copy ever happens.

Same inversion in:
- `EventDetailPanel.razor:33-38` — 📋 titled "Copy correlation ID", invokes `OnCopyCorrelation` → trace-map navigation
- `CausationChainView.razor:22-27` — 📋 titled "Copy correlation ID", invokes `OnCorrelationClick` → forwarded to `OnCopyCorrelation` → trace-map navigation
- `CommandDetailPanel.razor:14-19` — 🔗 titled "Filter timeline by this correlation ID", invokes `OnCorrelationFilter` → also wired to `OpenTraceMapForCorrelation` (the title says "filter", the action opens the trace map)

Bonus latent bug: `StreamDetail.razor:874-877`:
```csharp
private void CopyStreamKey()
{
    // Copy handled by clipboard JS interop if available; fallback is the title tooltip
}
```
The 📋 next to the stream key in the page title is a no-op — the comment is aspirational, no JS interop call is made.

A working clipboard helper already exists at `wwwroot/js/interop.js:108` (`hexalithAdmin.copyToClipboard`) and is used correctly in `CorrelationTraceMap.razor:392` and `Layout/Breadcrumb.razor:170`. The remaining places just need to call it.

User intent (Jerome): keep the trace-map navigation as a clearly-distinct *labelled* action and have the 📋 button actually copy. *"loupe mais il faudrait aussi l'inscrire"* → 🔍 + the literal text "Trace" alongside the icon.

---

## Section 2: Impact Analysis

### Epic Impact
- Stream-page features (Epic 21 area): bug fixes only, no scope change.

### Story Impact
- Single consolidated story (this proposal). No new stories created. Per Jerome's instruction, all modifications are bundled here for review before the remaining implementation.

### Artifact Conflicts
- **PRD / Epics / Architecture / UX:** No conflict. The intended behaviour for each feature already matches the spec — the bugs are implementation defects.

### Technical Impact
- Pure UI-layer changes (Razor + a small page helper). No backend, contract, DAPR, or infrastructure changes.
- No public NuGet contract changes.
- No new dependencies. The clipboard JS interop already exists.

### Test Impact
- Existing component tests do not assert off-dispatcher safety. A targeted bUnit dispatcher regression remains a follow-up, but this story must still add focused tests for the mode-reset and copy/trace callback wiring changes.
- Existing tests `CausationChainViewTests.CausationChainView_InvokesOnCorrelationClick` and `CommandDetailPanelTests.CommandDetailPanel_InvokesOnCorrelationFilter` must be updated or replaced so the assertions match the new semantics: copy callbacks copy, trace callbacks open trace.

### Risk
- **Medium, localized UI.** Removing `.ConfigureAwait(false)` from Blazor Server lifecycle / event-handler code is the canonical Microsoft guidance, but one current failure mode terminates the Blazor Server circuit. The mode reset and copy/trace split also touch parent-child component callback contracts, so compile and regression risk is higher than "mechanical."

---

## Section 3: Recommended Approach

Direct Adjustment, applied in **three independent patches** so each can be reverted in isolation if regressions appear:

| Patch | Scope | Effort | Risk |
|---|---|---|---|
| **A** Threading fix | BlameViewer + BisectTool | trivial (2 line edits) | Medium-low |
| **B** Mode-switch reset | StreamDetail.razor (refactor `Open*` methods to call shared `ResetAllViewModes()`) | small | Medium |
| **C** Button split + real copy | CausationChainView, EventDetailPanel, CommandDetailPanel, StreamDetail | small | Medium |

Already-applied portion: CorrelationTraceMap.razor:282 + EventDetailPanel.razor:275/280/293 (validated by build, smoke-tested via app restart).

---

## Section 4: Detailed Change Proposals

### Patch A — Threading fix (BlameViewer + BisectTool)

**A.1 — `src/Hexalith.EventStore.Admin.UI/Components/BlameViewer.razor:209`**

OLD:
```csharp
_blame = await ApiClient.GetAggregateBlameAsync(
    TenantId, Domain, AggregateId, AtSequence, default).ConfigureAwait(false);
```

NEW:
```csharp
_blame = await ApiClient.GetAggregateBlameAsync(
    TenantId, Domain, AggregateId, AtSequence, default);
```

Rationale: continuation must resume on the dispatcher so the `finally`-block `StateHasChanged()` at line 237 doesn't throw. Same root cause as `CorrelationTraceMap` — the Aspire trace already proved the symptom.

Implementation invariant: a `.razor` component lifecycle/event method must not use `.ConfigureAwait(false)` on an await when the continuation mutates component state, invokes component callbacks, or calls `StateHasChanged()`, unless the continuation explicitly marshals back through `InvokeAsync(...)`.

**A.2 — `src/Hexalith.EventStore.Admin.UI/Components/BisectTool.razor:337`**

OLD:
```csharp
AggregateStateSnapshot? state = await ApiClient.GetAggregateStateAtPositionAsync(
    TenantId, Domain, AggregateId, _goodSequence!.Value, _cts.Token).ConfigureAwait(false);
```

NEW:
```csharp
AggregateStateSnapshot? state = await ApiClient.GetAggregateStateAtPositionAsync(
    TenantId, Domain, AggregateId, _goodSequence!.Value, _cts.Token);
```

Rationale: same anti-pattern, latent crash on the "Load Fields" step of the Bisect setup. Currently not user-reported but identical to Blame/CorrelationTraceMap.

Acceptance requirement: Blame and Bisect async load completion must update the UI without `InvalidOperationException`, without circuit disconnect, and without leaving the tool in an infinite loading state.

---

### Patch B — Mode-switch reset (StreamDetail.razor)

Add one private helper and call it from each `Open*` before setting the new flag. Each `Open*` method must capture the source values it needs before calling the reset helper; resetting first can lose the selected event/sequence that the target mode needs.

**B.1 — Add helper near the existing `Open*` methods (e.g. just above `OpenBlameViewer` at line 580):**

```csharp
private void ResetAllViewModes()
{
    _traceMode = false;
    _traceCorrelationId = null;
    _sandboxMode = false;
    _sandboxSequence = null;
    _stepMode = false;
    _stepSequence = null;
    _bisectMode = false;
    _bisectGood = null;
    _bisectBad = null;
    _blameMode = false;
    _blameSequence = null;
    _diffMode = false;
    _diffFrom = 0;
    _diffTo = 0;
    _selectedSequence = null;
    _selectedEntry = null;
}
```

**B.2 — Refactor each `Open*` to use it.** Example for `OpenBlameViewer`:

OLD:
```csharp
private void OpenBlameViewer()
{
    _blameMode = true;
    _blameSequence = _selectedSequence;
    _diffMode = false;
    _selectedSequence = null;
    _selectedEntry = null;
    UpdateUrl();
}
```

NEW:
```csharp
private void OpenBlameViewer()
{
    long? prevSelected = _selectedSequence;
    ResetAllViewModes();
    _blameMode = true;
    _blameSequence = prevSelected;
    UpdateUrl();
}
```

Apply the same shape to `OpenBisectTool`, `OpenStepDebugger`, `OpenSandbox`, `OpenTraceMap`, and `OpenTraceMapForCorrelation`. Each captures the inputs it needs from the current state *before* the reset, then resets, then sets only its own flag(s).

**B.3 — Verify the rendering precedence block (lines 119–176) is unaffected.** No change required there — once only one mode flag is true, `if / else if` works correctly.

Rationale: removes the asymmetric-reset class of bugs by making "exactly one mode true" an invariant enforced at every transition point.

Acceptance requirement: switching from any active tool mode to any other tool mode renders the requested mode on the first click. The minimum matrix is Trace → Sandbox, Sandbox → Bisect, Bisect → Blame, Blame → Step, Step → Trace, plus close/reopen behavior for Blame, Bisect, Step, and Sandbox.

Follow-up technical debt: replace the mutually-exclusive bool cluster with a single `StreamDetailMode` enum plus separate payload state. `ResetAllViewModes()` is the tactical sprint fix; the enum is the durable state model.

---

### Patch C — Copy / Trace button split + real clipboard wiring

**C.1 — `CausationChainView.razor`**: split the single 📋 into 📋 (copy) + 🔍 Trace (open trace map).

OLD (lines 22–27):
```razor
<FluentButton Appearance="ButtonAppearance.Transparent"
              Title="Copy correlation ID"
              OnClick="() => OnCorrelationClick.InvokeAsync(Chain.CorrelationId)"
              aria-label="Copy correlation ID">
    &#x1F4CB;
</FluentButton>
```

NEW:
```razor
<FluentButton Appearance="ButtonAppearance.Transparent"
              Title="Copy correlation ID"
              OnClick="() => OnCorrelationClick.InvokeAsync(Chain.CorrelationId)"
              aria-label="Copy correlation ID">
    &#x1F4CB;
</FluentButton>
<FluentButton Appearance="ButtonAppearance.Transparent"
              Title="Open trace map"
              OnClick="() => OnOpenTraceMap.InvokeAsync(Chain.CorrelationId)"
              aria-label="Open trace map">
    &#x1F50D; Trace
</FluentButton>
```

Add new parameter (after line 67):
```csharp
[Parameter]
public EventCallback<string> OnOpenTraceMap { get; set; }
```

Rationale: keep `OnCorrelationClick` (existing test depends on it; semantics now genuinely "click on the correlation ID's primary action = copy"). Add `OnOpenTraceMap` for the new labelled trace-map button.

---

**C.2 — `EventDetailPanel.razor`**: same split (lines 33–38) and forward the new callback to the embedded `CausationChainView`.

Add the second `<FluentButton>` after the existing 📋 (mirror of C.1).

Update line 115:
```razor
<CausationChainView Chain="@_causationChain"
                    OnCorrelationClick="OnCopyCorrelation"
                    OnOpenTraceMap="OnOpenTraceMap" />
```

Add new parameter (next to existing `OnCopyCorrelation` around line 155):
```csharp
[Parameter]
public EventCallback<string> OnOpenTraceMap { get; set; }
```

---

**C.3 — `CommandDetailPanel.razor`**: split the 🔗 button (lines 15–20) into 📋 (copy via new `OnCopyCorrelation` callback) + 🔍 Trace (opens trace map via a new `OnOpenTraceMap` callback). Do not keep the `OnCorrelationFilter` parameter name for trace-map navigation; the old name encodes the wrong behavior and would preserve the semantic bug in the component API. Update the existing `CommandDetailPanelTests.CommandDetailPanel_InvokesOnCorrelationFilter` test to the new trace-map callback semantics.

NEW:
```razor
<FluentButton Appearance="ButtonAppearance.Transparent"
              Title="Copy correlation ID"
              OnClick="() => OnCopyCorrelation.InvokeAsync(Entry.CorrelationId)"
              aria-label="Copy correlation ID">
    &#x1F4CB;
</FluentButton>
<FluentButton Appearance="ButtonAppearance.Transparent"
              Title="Open trace map"
              OnClick="() => OnOpenTraceMap.InvokeAsync(Entry.CorrelationId)"
              aria-label="Open trace map">
    &#x1F50D; Trace
</FluentButton>
```

Add parameter:
```csharp
[Parameter]
public EventCallback<string> OnCopyCorrelation { get; set; }

[Parameter]
public EventCallback<string> OnOpenTraceMap { get; set; }
```

---

**C.4 — `StreamDetail.razor`**: actually copy via JSRuntime, and wire the new callbacks.

C.4.a — Inject `IJSRuntime`. Add at the top of the file (after the existing `@inject` lines):
```razor
@using Microsoft.JSInterop
@inject IJSRuntime JSRuntime
```

C.4.b — Add a copy helper method (next to `OpenTraceMapForCorrelation`, around line 759):
```csharp
private async Task CopyToClipboardAsync(string text)
{
    if (string.IsNullOrEmpty(text)) { return; }

    try
    {
        await JSRuntime.InvokeAsync<bool>("hexalithAdmin.copyToClipboard", text);
    }
    catch (JSDisconnectedException)
    {
        // The circuit is already gone; clipboard failure must not create a second circuit error.
    }
}
```

If the Admin UI already has a notification/status pattern available in this page, use it to give a small copy success/failure confirmation. If not, keep the first patch minimal but record the lack of feedback in the implementation notes so it is not mistaken for an intentional UX decision.

C.4.c — Update `EventDetailPanel` wiring (line 411–416):

OLD:
```razor
<EventDetailPanel TenantId="@TenantId" Domain="@Domain" AggregateId="@AggregateId"
                  SequenceNumber="@_selectedEntry.SequenceNumber"
                  OnCopyCorrelation="@OpenTraceMapForCorrelation"
                  OnInspectState="@OnInspectStateFromDetail"
                  OnDiffRequested="@OnDiffFromDetail"
                  AutoTraceCausation="@(QueryView == "causation")" />
```

NEW:
```razor
<EventDetailPanel TenantId="@TenantId" Domain="@Domain" AggregateId="@AggregateId"
                  SequenceNumber="@_selectedEntry.SequenceNumber"
                  OnCopyCorrelation="@CopyToClipboardAsync"
                  OnOpenTraceMap="@OpenTraceMapForCorrelation"
                  OnInspectState="@OnInspectStateFromDetail"
                  OnDiffRequested="@OnDiffFromDetail"
                  AutoTraceCausation="@(QueryView == "causation")" />
```

C.4.d — Update `CommandDetailPanel` wiring (line 420):

OLD:
```razor
<CommandDetailPanel Entry="@_selectedEntry" OnCorrelationFilter="@OpenTraceMapForCorrelation" />
```

NEW:
```razor
<CommandDetailPanel Entry="@_selectedEntry"
                    OnOpenTraceMap="@OpenTraceMapForCorrelation"
                    OnCopyCorrelation="@CopyToClipboardAsync" />
```

C.4.e — Fix the no-op `CopyStreamKey()` (line 874–877):

OLD:
```csharp
private void CopyStreamKey()
{
    // Copy handled by clipboard JS interop if available; fallback is the title tooltip
}
```

NEW:
```csharp
private async Task CopyStreamKey()
{
    string streamKey = $"{TenantId}/{Domain}/{AggregateId}";
    await CopyToClipboardAsync(streamKey);
}
```

(The Razor markup at line 49–55 calls `OnClick="@CopyStreamKey"` — Blazor accepts both `Action` and `Func<Task>`, so converting the method signature is safe.)

---

## Section 5: Acceptance Criteria

### AC-A — Blazor dispatcher safety
- `BlameViewer.razor` and `BisectTool.razor` no longer use `.ConfigureAwait(false)` on awaits whose continuations mutate component state or call `StateHasChanged()`.
- Blame and Bisect async load completion does not throw `InvalidOperationException`, disconnect the circuit, or leave the user in an infinite loading state.
- Server logs and browser console show no new circuit errors while opening Trace Causation, Blame, and Bisect.

### AC-B — Mode exclusivity
- Every `Open*` mode method in `StreamDetail.razor` captures needed source values, calls `ResetAllViewModes()`, and activates exactly one primary mode.
- Switching from any active tool mode to any other renders the requested mode on the first click.
- Closing Blame, Bisect, Step Through, or Sandbox returns to the expected stream detail state without leaving stale mode flags active.

### AC-C — Copy and trace semantics
- 📋 buttons copy the exact displayed correlation ID or stream key and do not navigate.
- Copy success/failure uses the existing Admin UI notification/status pattern if one is available; otherwise the implementation notes explicitly record that feedback remains a follow-up.
- 🔍 Trace buttons open the trace map for the exact correlation ID and do not write to the clipboard.
- Null or empty correlation IDs are handled deliberately: hide/disable the actions or no-op safely, but do not throw.
- Clipboard failure must surface through the existing Admin UI status/error pattern if one is already available in the affected component/page. If no such pattern exists, the implementation must handle the failure safely and record copy feedback as a follow-up.
- `JSDisconnectedException` is handled without crashing the circuit.

### AC-D — Regression coverage
- Add or update Admin UI component tests for the mode-switch matrix: Sandbox → Bisect, Step → Blame, Trace → Sandbox, and Blame → Trace.
- Add or update component tests proving copy actions invoke `hexalithAdmin.copyToClipboard` with the exact value.
- Add or update component tests proving trace actions activate/open trace mode without invoking clipboard interop.
- Update tests affected by renaming `CommandDetailPanel.OnCorrelationFilter` to `OnOpenTraceMap`.
- Add or update focused tests in the Admin UI test project, likely covering:
  - `StreamDetail` mode transition behavior.
  - `CommandDetailPanel` copy vs trace callbacks.
  - `EventDetailPanel` copy vs trace callbacks.
  - `CausationChainView` copy vs trace callbacks.

### AC-E — Follow-up tracking
- Create explicit follow-up backlog/planning items for:
  - `.razor` dispatcher-safety audit for `.ConfigureAwait(false)` before UI mutation.
  - `StreamDetailMode` enum refactor.
  - bUnit dispatcher regression test.
- Each follow-up must have an owner/context note or a link to the created artifact before this bundle is considered fully reviewed.

---

## Section 6: Decision Gate

This proposal is ready for implementation only after Jerome approves the revised scope/risk classification and the AC-A through AC-E acceptance criteria.

Implementation approval means:
- Apply Patch A, Patch B, Patch C, and the required Admin UI tests in the same bundle.
- Create or link the three follow-up items listed in AC-E.
- Do not treat follow-up creation as optional documentation-only work.

---

## Section 7: Implementation Handoff

**Already applied in this session (verified by build, smoke-tested via aspire restart):**
- ✅ `CorrelationTraceMap.razor:282` — removed `.ConfigureAwait(false)`
- ✅ `EventDetailPanel.razor:275, 280, 293` — removed `.ConfigureAwait(false)` (3 sites)

**Pending implementation after this revised proposal is accepted:**
- ⏳ Patch A: BlameViewer.razor + BisectTool.razor threading fixes
- ⏳ Patch B: StreamDetail.razor — `ResetAllViewModes()` helper + refactor of all `Open*` methods
- ⏳ Patch C: CausationChainView, EventDetailPanel, CommandDetailPanel, StreamDetail — copy/trace button split + real clipboard wiring + fix `CopyStreamKey` no-op
- ⏳ Tests: Admin UI component tests for mode exclusivity and copy/trace callback wiring

**Implementation order:**
1. Patch A first, because it removes known circuit-crash causes with minimal surface area.
2. Patch B second, because it stabilizes page mode state before copy/trace manual testing.
3. Patch C third, because it changes component callback contracts and tests.
4. Tests last or alongside each patch, but no review handoff until Admin UI build and tests pass.

**Definition of Done:**
- No known Stream Detail action causes a Blazor Server circuit disconnect.
- Only one primary Stream Detail mode is active after any mode transition.
- Copy and Trace controls have distinct behavior, labels, and tests.
- Admin UI build and Admin UI tests pass.
- Required follow-up items are created or linked.

**Verification steps after implementation:**
1. `dotnet build src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj` — expect 0 warnings, 0 errors.
2. `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj` — existing and new focused tests must pass.
3. Standard Jerome restart — flush Redis → build → `aspire run`.
4. Manual UI verification on `/streams/system/tenants/acme-corp` and at least one stream/event path with missing or empty correlation data:
   - Click **Trace Causation** → trace map renders, page stays interactive, the new 🔍 Trace button opens the trace map, the 📋 button copies the correlation ID (paste-test).
   - Click **Blame** → BlameViewer renders with no infinite spinner, **Close** button works.
   - Click **Sandbox** → **Bisect** → **Blame** → **Step Through** → **Trace Causation**; each click switches to the requested mode on first click.
   - Verify command detail, event detail, and causation chain copy/trace actions.
   - Click stream-key 📋 in the title → stream key is in the clipboard.
   - Rapidly switch modes while async loads are in flight; the page remains interactive and logs show no circuit disconnect.

**Required follow-ups created from this review (not blocking this bundle):**
- `.razor` audit for `.ConfigureAwait(false)` before component-state mutation, component callbacks, or `StateHasChanged()`.
- Replace the `StreamDetail.razor` mutually-exclusive bool mode cluster with a single `StreamDetailMode` enum plus payload state.
- Add a bUnit dispatcher regression test that runs an async load on a non-dispatcher thread and verifies no `InvalidOperationException`.

---

## Appendix — Aspire Trace Reference

- **Trace ID:** `5ca47908bbd36f5e77c651047959e790`
- **Resource:** `eventstore-admin-ui`
- **Failing span:** `486fe3d` — `Hexalith.EventStore.Admin.UI.Components.CorrelationTraceMap.LoadTraceMapAsync` → `ComponentBase.StateHasChanged()` → `InvalidOperationException`
- **Server-side leg (`f2e800b`):** `eventstore-admin` → DAPR sidecar → eventstore — all 200 OK in <8 ms. Confirms Group A is UI-only.
