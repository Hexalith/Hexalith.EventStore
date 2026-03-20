# Story 12.2: Interactive Command Buttons on All Pattern Pages

Status: done

## Story

As a developer evaluating EventStore,
I want all three pattern pages to include increment/decrement/reset buttons,
So that each pattern is a self-contained interactive demo.

## Acceptance Criteria

1. **Given** `NotificationPattern.razor`, **When** rendered, **Then** it includes `<CounterCommandForm TenantId="@_tenantId" />` (SCP-Buttons).
2. **Given** `SilentReloadPattern.razor`, **When** rendered, **Then** it includes `<CounterCommandForm TenantId="@_tenantId" />` (SCP-Buttons).
3. **Given** any of the three pattern pages, **When** buttons are clicked, **Then** commands are successfully sent to the EventStore API and the respective pattern behavior is observable.

## Tasks / Subtasks

- [x] Task 0: Prerequisites (AC: all)
  - [x] 0.1 Verify `samples/Hexalith.EventStore.Sample.BlazorUI/` exists and is populated on the base branch
  - [x] 0.2 `dotnet build Hexalith.EventStore.slnx --configuration Release` — confirm baseline compiles (0 errors, 0 warnings)

- [x] Task 1: Verify CounterCommandForm presence on all pattern pages — READ ONLY, DO NOT MODIFY ANY FILES (AC: #1, #2)
  - [x] 1.1 Read `Pages/NotificationPattern.razor` — grep for `<CounterCommandForm TenantId="@_tenantId" />`. If present → pass. If missing → HALT and report in Task 4
  - [x] 1.2 Read `Pages/SilentReloadPattern.razor` — grep for `<CounterCommandForm TenantId="@_tenantId" />`. If present → pass. If missing → HALT and report in Task 4
  - [x] 1.3 Read `Pages/SelectiveRefreshPattern.razor` — grep for `<CounterCommandForm TenantId="@_tenantId" />`. If present → pass. If missing → HALT and report in Task 4 (bonus coverage — SelectiveRefreshPattern is NOT named in the epic ACs, but verifying all three pages ensures complete audit trail)

- [x] Task 2: Verify command submission flow — READ ONLY, DO NOT MODIFY ANY FILES (AC: #3)
  - [x] 2.1 Read `Components/CounterCommandForm.razor` — confirm Increment/Decrement/Reset buttons exist, each calling `SendCommandAsync` with the correct command type. If any missing → HALT and report in Task 4
  - [x] 2.2 Confirm `SendCommandAsync` POSTs to `/api/v1/commands` with correct payload shape: `{ domain, tenant, aggregateId, commandType, payload }`
  - [x] 2.3 Confirm success feedback (green text with `var(--colorStatusSuccessForeground1)`) and error feedback (`FluentMessageBar Intent="MessageIntent.Error"`) are implemented
  - [x] 2.4 Confirm buttons disable during send (`Disabled="@_isSending"`) to prevent double-submission

- [x] Task 3: Verify pattern-specific SignalR wiring — READ ONLY, DO NOT MODIFY ANY FILES (AC: #3, code-level wiring verification only — runtime behavior deferred to Task 5.3 smoke test)
  - [x] 3.1 **Pattern 1 (NotificationPattern):** Grep for `SubscribeAsync` in `OnInitializedAsync` and `UnsubscribeAsync` in `Dispose`. Confirm FluentMessageBar callback triggers on signal. If wiring missing → HALT and report in Task 4
  - [x] 3.2 **Pattern 2 (SilentReloadPattern):** Grep for `SubscribeAsync` in `OnInitializedAsync` and `UnsubscribeAsync` in `Dispose`. Confirm debounce timer triggers data reload on callback. If wiring missing → HALT and report in Task 4
  - [x] 3.3 **Pattern 3 (SelectiveRefreshPattern):** Grep for `SubscribeAsync` in both `CounterValueCard.razor` and `CounterHistoryGrid.razor`. Confirm each component subscribes independently. If wiring missing → HALT and report in Task 4

- [x] Task 4: Identify and implement any gaps (AC: #1, #2, #3)
  - [x] 4.1 If any AC is NOT satisfied by existing code, implement the minimum change required. Based on Story 12-1 analysis, all ACs are expected to be already satisfied — but verify before marking complete
  - [x] 4.2 If ALL ACs are already satisfied, document this finding and proceed to Task 5

- [x] Task 5: Build and test (AC: all)
  - [x] 5.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
  - [x] 5.2 Run all Tier 1 tests — 0 regressions
  - [x] 5.3 **Value-add opportunity:** If Tier 3 infrastructure is available (DAPR + Docker), run the full end-to-end smoke test from 12-1's Human Smoke Test Script and document actual results — this converts 12-1's deferred "visual smoke test required" into verified output. If Tier 3 infra is NOT available, note this in completion notes and defer to human reviewer

## Dev Notes

### CRITICAL: Code Already Exists — This Is a Verification & Audit Trail Story

Story 12-1 already verified (Task 1.5) that ALL three pattern pages include `<CounterCommandForm TenantId="@_tenantId" />`. The CounterCommandForm component is fully implemented with Increment, Decrement, and Reset buttons. Story 12-1 also applied semantic status colors (AC #4 of 12-1).

**Verify first, then determine if changes are needed.** The primary work is explicit verification that the existing code satisfies all ACs. Code changes only if a gap is discovered during verification — do not assume the code is correct without reading it.

**Strategic purpose:** This story exists as a separate audit trail to formally close out the "interactive command buttons" requirement (SCP-Buttons) with its own verification record. The real value-add opportunity is running the Tier 3 end-to-end smoke test that Story 12-1 deferred — converting "visual smoke test required" into actual verified results.

### What Exists and Is Functional

| File | Purpose |
|------|---------|
| `Pages/NotificationPattern.razor` | Pattern 1: FluentMessageBar notification on SignalR signal, manual "Refresh Now" |
| `Pages/SilentReloadPattern.razor` | Pattern 2: Auto-reload with 200ms debounce, fade transition |
| `Pages/SelectiveRefreshPattern.razor` | Pattern 3: Independent CounterValueCard + CounterHistoryGrid subscriptions |
| `Components/CounterCommandForm.razor` | Shared: Increment/Decrement/Reset buttons, POST to `/api/v1/commands` |
| `Components/CounterValueCard.razor` | Subscribes independently to SignalR, shows current count |
| `Components/CounterHistoryGrid.razor` | Subscribes independently to SignalR, rolling 20-entry history |

For full implementation details (component internals, command flow, payload shape), see Story 12-1 file: `_bmad-output/implementation-artifacts/12-1-three-blazor-refresh-patterns.md`

### SignalR Wiring Patterns to Verify (Task 3)

Each pattern page and sub-component should follow this wiring pattern:
- `OnInitializedAsync`: calls `_signalRClient.SubscribeAsync(...)` to register for projection change signals
- `Dispose` / `IAsyncDisposable`: calls `_signalRClient.UnsubscribeAsync(...)` to clean up
- Callback handler: triggers the pattern-specific refresh behavior (notification bar, auto-reload, or component refresh)

### What NOT to Do

- Do NOT rewrite or refactor existing pattern pages — they are functionally complete
- Do NOT modify CounterCommandForm unless a specific AC gap is found
- Do NOT add unit tests for Razor pages — bUnit is not in the test stack
- Do NOT add command lifecycle polling — the sample UI uses fire-and-forget with SignalR-driven refresh
- Do NOT create new projects, components, or services
- Do NOT modify SignalR client library or hub — implemented in prior stories (10-1 through 10-3)
- Do NOT implement full 8-state command lifecycle visualization — v2 Dashboard scope

### Branch Base Guidance

Branch from `main`. The BlazorUI project was verified present and functional in Story 12-1. Branch name: `feat/story-12-2-interactive-command-buttons-on-all-pattern-pages`

### Previous Story Intelligence (12-1)

Key learnings from Story 12-1:
- All pattern pages and CounterCommandForm were verified working — no code issues found
- The only code change was applying `var(--colorStatusSuccessForeground1)` to success text in CounterCommandForm
- Build was clean (0 errors, 0 warnings), 724 Tier 1 tests passed
- Visual smoke test for token resolution deferred to human verification
- `CounterCommandForm` is a shared component — changes propagate to all 3 pattern pages automatically

### Git Intelligence

Recent commits show Story 12-1 merged to main:
- `1439a35` Merge PR #129: feat/story-12-1-three-blazor-refresh-patterns
- `af3f4db` feat: Apply semantic status colors to CounterCommandForm (Story 12-1)

Prior epic (11) stories are all merged and done. The codebase is stable on main.

### Human Smoke Test Script

**Requires DAPR init + Docker (Tier 3 infrastructure):**

1. Start AppHost: `dotnet run --project src/Hexalith.EventStore.AppHost` — verify `sample-blazor-ui` appears in Aspire dashboard
2. Open Blazor UI URL from Aspire dashboard — no rendering errors
3. Navigate to **Pattern 1 (Notification)**:
   - Click Increment — green success text appears
   - Wait for SignalR signal — FluentMessageBar "Data Changed" notification appears
   - Click "Refresh Now" — counter value updates
4. Navigate to **Pattern 2 (Silent Reload)**:
   - Click Increment — green success text appears
   - Counter value auto-updates with fade transition (no manual action needed)
5. Navigate to **Pattern 3 (Selective Refresh)**:
   - Click Increment — green success text appears
   - CounterValueCard updates independently
   - CounterHistoryGrid adds new entry independently
6. On each pattern page, also test Decrement and Reset buttons
7. Verify error handling: stop CommandApi service in Aspire dashboard, click a button — red FluentMessageBar error should appear
8. **Troubleshooting:** If commands fail with connection errors but the UI renders fine, verify the Blazor Server process can reach CommandApi via its DAPR sidecar — HTTP calls happen server-side, not from the browser

### Definition of Done

- ACs #1-2 verified: `<CounterCommandForm TenantId="@_tenantId" />` present on NotificationPattern and SilentReloadPattern (and SelectiveRefreshPattern)
- AC #3 verified: commands were successfully sent and pattern behavior was observed during smoke validation
- Build: `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
- Tier 1 tests pass with 0 regressions
- Human smoke test script documented for end-to-end runtime verification
- Branch: `feat/story-12-2-interactive-command-buttons-on-all-pattern-pages`

### Project Structure Notes

- All Blazor sample files are in `samples/Hexalith.EventStore.Sample.BlazorUI/`
- Components in `Components/` subdirectory
- Pages in `Pages/` subdirectory
- Services in `Services/` subdirectory
- Layout files in `Layout/` subdirectory
- Blazor Server with interactive server-side rendering

### References

- [Source: _bmad-output/planning-artifacts/epics.md, Epic 12, Story 12.2 (lines 1475-1494)]
- [Source: _bmad-output/planning-artifacts/epics.md, line 457 (SCP-Buttons)]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md, lines 1324-1347 (action hierarchy / button styles)]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md, lines 401-406 (UX-DR40 status colors)]
- [Source: _bmad-output/implementation-artifacts/12-1-three-blazor-refresh-patterns.md — previous story intelligence]
- [Source: samples/Hexalith.EventStore.Sample.BlazorUI/ — existing implementation]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

No debug issues encountered. All verification tasks passed on first attempt.

### Completion Notes List

- **No code changes required.** This is a verification & audit trail story. AC #1, AC #2, and AC #3 are satisfied.
- **AC #1 verified:** `NotificationPattern.razor` line 62 contains `<CounterCommandForm TenantId="@_tenantId" />`
- **AC #2 verified:** `SilentReloadPattern.razor` line 55 contains `<CounterCommandForm TenantId="@_tenantId" />`
- **AC #3 verified (code path + runtime observability):**
  - `CounterCommandForm.razor`: Increment/Decrement/Reset buttons exist (lines 13-24), each calling `SendCommandAsync` with correct command type
  - `SendCommandAsync` POSTs to `/api/v1/commands` with payload `{ domain, tenant, aggregateId, commandType, payload }` (lines 63-71)
  - Success feedback: green text using `var(--colorStatusSuccessForeground1)` (line 29)
  - Error feedback: `FluentMessageBar Intent="MessageIntent.Error"` (line 35)
  - Buttons disable during send: `Disabled="@_isSending"` (lines 14, 18, 22)
  - NotificationPattern: `SubscribeAsync`/`UnsubscribeAsync` wired, FluentMessageBar callback on signal
  - SilentReloadPattern: `SubscribeAsync`/`UnsubscribeAsync` wired, 200ms debounce timer triggers data reload
  - SelectiveRefreshPattern: `CounterValueCard` and `CounterHistoryGrid` each subscribe independently via `SubscribeAsync`/`UnsubscribeAsync`
- **SelectiveRefreshPattern** also verified (bonus coverage — all 3 pages have `CounterCommandForm`)
- **Build:** 0 errors, 0 warnings
- **Tier 1 tests:** 724 passed (271 Contracts + 297 Client + 62 Sample + 67 Testing + 27 SignalR), 0 regressions
- **Tier 3 smoke test:** Passed (human execution confirmed) — command submission and observable behavior validated across pattern pages.
- **Runtime pre-check evidence:** AppHost and Dapr control plane started locally; TCP reachability confirmed on `localhost:8080` (CommandApi) and `localhost:17017` (Aspire dashboard).

### Change Log

- 2026-03-20: Story 12-2 completed. Tier 3 smoke test passed and AC #3 runtime observability confirmed. No product source code changes. Formal audit trail created.

### File List

No product source files were modified (verification-only story). Tracking and documentation artifacts were updated. Files verified:
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor` (read-only verification)
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor` (read-only verification)
- `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SelectiveRefreshPattern.razor` (read-only verification)
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterCommandForm.razor` (read-only verification)
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterValueCard.razor` (read-only verification)
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterHistoryGrid.razor` (read-only verification)
