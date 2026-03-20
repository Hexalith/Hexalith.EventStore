# Story 12.1: Three Blazor Refresh Patterns

Status: ready-for-dev

## Story

As a developer evaluating EventStore,
I want three reference patterns demonstrating how to handle real-time projection updates in Blazor,
So that I can choose the right approach for my application.

## Acceptance Criteria

1. **Given** the sample Blazor UI, **When** a projection change signal arrives via SignalR, **Then** Pattern 1 (Notification) shows a persistent notification prompting manual refresh (FR60).
2. **Given** the sample Blazor UI, **When** a projection change signal arrives via SignalR, **Then** Pattern 2 (Silent Reload) automatically reloads data without user interaction (FR60).
3. **Given** the sample Blazor UI, **When** a projection change signal arrives via SignalR, **Then** Pattern 3 (Selective Refresh) refreshes only the affected projection component (FR60).
4. **Given** the CounterCommandForm component, **When** rendering command submission feedback, **Then** success uses green, failure uses red, and idle uses gray — applying semantic status colors per UX-DR40. Note: the full 8-state lifecycle visualization (Received/Processing/EventsStored/EventsPublished/Completed/Rejected/PublishFailed/TimedOut) is v2 Dashboard scope; this sample only observes success/fail/idle from the HTTP response.
5. **Given** all three pattern pages, **When** rendered, **Then** each includes `<CounterCommandForm TenantId="@_tenantId" />` for interactive command submission (SCP-Buttons).

## Tasks / Subtasks

- [ ] Task 1: Verify existing code meets ACs #1-3 and #5 (AC: #1, #2, #3, #5)
  - [ ] 1.1 Read each file listed in "What exists and is functional" (Dev Notes) and confirm it matches the corresponding AC
  - [ ] 1.2 Pattern 1: `NotificationPattern.razor` — FluentMessageBar on signal, "Refresh Now" button, SubscribeAsync/UnsubscribeAsync lifecycle
  - [ ] 1.3 Pattern 2: `SilentReloadPattern.razor` — auto-reload with 200ms debounce, fade transition, CancellationTokenSource cleanup
  - [ ] 1.4 Pattern 3: `SelectiveRefreshPattern.razor` — independent CounterValueCard + CounterHistoryGrid components, each with own subscription
  - [ ] 1.5 All 3 pages include `<CounterCommandForm TenantId="@_tenantId" />` (SCP-Buttons)
  - [ ] 1.6 Overview page (`Index.razor`): FluentDataGrid comparison, learning order, "How to Run" card
  - [ ] 1.7 `MainLayout.razor`: FluentNavMenu sidebar links to all 4 pages
  - [ ] 1.8 Infrastructure: AppHost registers `sample-blazor-ui`, Program.cs has all DI registrations, dual-mode auth, ETag caching

- [ ] Task 2: Implement status color system (AC: #4, UX-DR40)
  - [ ] 2.1 Update `CounterCommandForm.razor` to apply semantic status colors to command feedback using inline Fluent UI design tokens (no separate class — keep it simple for a reference sample). Before coding, verify these CSS custom property names exist in Fluent UI v4 by checking the rendered page's computed styles or the Fluent UI docs:
    - Green (`var(--colorStatusSuccessForeground1)`) = success (command accepted, 202 response)
    - Red (`var(--colorStatusDangerForeground1)`) = failure (HTTP error)
    - Gray (`var(--neutral-foreground-hint)`) = idle (already used for "Last command" text)
    - If token names don't resolve, fall back to `color: var(--accent-fill-rest)` pattern or use `Color.Success`/`Color.Error` from FluentUI component APIs
  - [ ] 2.2 Apply color to the "Last command" text so success is visually distinguishable from idle
  - [ ] 2.3 Verify colors adapt correctly in both light and dark FluentDesignTheme modes

- [ ] Task 3: Build and test (AC: #1-5)
  - [ ] 3.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
  - [ ] 3.2 Run all Tier 1 tests — 0 regressions
  - [ ] 3.3 Manual smoke test (human verification — dev agent should skip and note "requires human smoke test" in completion notes. Requires DAPR init + Docker — Tier 3 infrastructure):
    1. Start AppHost: `dotnet run --project src/Hexalith.EventStore.AppHost` — verify `sample-blazor-ui` appears in Aspire dashboard
    2. Open Blazor UI URL from Aspire dashboard — no rendering errors on any page
    3. Navigate to each pattern page via sidebar — all 4 pages load (Overview + 3 patterns)
    4. On each pattern page: click Increment — verify command feedback shows green success color (AC #4)
    5. On Pattern 1: verify FluentMessageBar "Data Changed" appears after signal, click "Refresh Now" to reload
    6. On Pattern 2: verify counter auto-updates with fade transition after sending command
    7. On Pattern 3: verify CounterValueCard and CounterHistoryGrid refresh independently

## Dev Notes

### Implementation Scope

Only **Task 2** requires code changes (`CounterCommandForm.razor` status colors). Task 1 is code verification (read and confirm). Task 3 is build/test. Do not over-engineer or expand scope beyond this.

### CRITICAL: Old Story File Exists — Ignore It

`_bmad-output/implementation-artifacts/12-1-architecture-overview-with-mermaid-topology.md` is from the **old epic numbering** (pre-migration). It is unrelated to THIS story. Do NOT reference it.

### CRITICAL: Code Already Exists

The entire Blazor sample UI project **already exists** at `samples/Hexalith.EventStore.Sample.BlazorUI/` with all three pattern pages fully implemented. This story is primarily a **verification and completion** story, not a greenfield implementation.

**What exists and is functional:**
- `Pages/NotificationPattern.razor` — FluentMessageBar notification on SignalR signal, manual "Refresh Now" button
- `Pages/SilentReloadPattern.razor` — Auto-reload with 200ms debounce, fade transition
- `Pages/SelectiveRefreshPattern.razor` — Independent CounterValueCard + CounterHistoryGrid components
- `Pages/Index.razor` — Overview with FluentDataGrid comparison and learning order
- `Components/CounterCommandForm.razor` — Increment/Decrement/Reset buttons (already on all 3 pages)
- `Components/CounterValueCard.razor` — Subscribes independently, shows count with FluentProgress loading
- `Components/CounterHistoryGrid.razor` — FluentDataGrid with last 20 snapshots, subscribes independently
- `Layout/MainLayout.razor` — FluentLayout with sidebar FluentNavMenu
- `Services/CounterQueryService.cs` — ETag-based HTTP 304 query caching
- `Services/EventStoreApiAccessTokenProvider.cs` — Dual-mode auth (Keycloak + dev JWT)
- `Services/EventStoreApiAuthorizationHandler.cs` — Bearer token injection DelegatingHandler
- `Services/SignalRClientStartup.cs` — IHostedService to start SignalR on app startup
- `Program.cs` — Full DI registration (FluentUI, SignalR, HttpClient, CounterQueryService)
- AppHost integration at `src/Hexalith.EventStore.AppHost/Program.cs` (lines 75-94)

**What may need work:**
- AC #4 (UX-DR40 status colors): The current `CounterCommandForm` shows command result text ("Last command: IncrementCounter at 14:30:05") but does NOT use semantic status colors. The color system needs to be applied to command feedback.

### Design Decision: FluentMessageBar over FluentToast for Pattern 1

Pattern 1 deliberately uses `FluentMessageBar` (persistent inline notification) instead of `FluentToast` (auto-dismissing toast). Rationale (documented in NotificationPattern.razor comments):
- Toasts auto-dismiss (~5s default) — insufficient time for user to act on a "Refresh" action
- FluentMessageBar persists until user acts, preventing notification fatigue on frequent changes
- This is a conscious departure from the epic text ("toast notification") based on UX analysis

### Status Color System (UX-DR40) — Scoped for Sample UI

The full UX-DR40 defines 5 semantic color roles across 8 command lifecycle states. The **sample UI only observes 3 states** (success/fail/idle) because `CounterCommandForm` gets a synchronous HTTP response — it doesn't poll for intermediate lifecycle states. Full lifecycle visualization is v2 Dashboard scope.

**Colors applicable to this story:**

| State | Color | CSS Token |
|---|---|---|
| Success (202 Accepted) | Green | `var(--colorStatusSuccessForeground1)` |
| Failure (HTTP error) | Red | `var(--colorStatusDangerForeground1)` |
| Idle (no command sent) | Gray | `var(--neutral-foreground-hint)` |

**Token verification:** Before using these CSS custom properties, check they resolve in Fluent UI v4's rendered output. If not, use FluentUI component color APIs (`Color.Success`, `Color.Error`) or fall back to `var(--accent-fill-rest)` patterns.

**Dark/Light mode:** Use Fluent UI design tokens (not hardcoded hex). Fluent UI automatically adapts to FluentDesignTheme.

### Branch Base Guidance

Branch from `main`. The Blazor UI project (`samples/Hexalith.EventStore.Sample.BlazorUI/`) is already on main and compiles independently of Epic 11 work. Verify with `dotnet build Hexalith.EventStore.slnx --configuration Release` before starting any changes.

### Existing Project References

| File | Purpose |
|---|---|
| `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj` | Project file: refs SignalR + ServiceDefaults + FluentUI NuGet |
| `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` | Client library: auto-reconnect, group management, callback dispatch |
| `src/Hexalith.EventStore.CommandApi/SignalR/ProjectionChangedHub.cs` | Server hub: JoinGroup/LeaveGroup, max 50 groups/connection |
| `src/Hexalith.EventStore.AppHost/Program.cs` | Aspire topology with sample-blazor-ui resource (lines 75-94) |

### NuGet Dependencies (from Directory.Packages.props)

- `Microsoft.FluentUI.AspNetCore.Components` — Fluent UI V4 component library
- `Microsoft.FluentUI.AspNetCore.Components.Icons` — Icon set
- `Microsoft.AspNetCore.SignalR.Client` (v10.0.5) — Client-side SignalR

### What NOT to Do

- Do NOT rewrite existing pattern pages — they are functionally complete. Exception: `CounterCommandForm.razor` modification for AC #4 status colors IS in scope (it's a shared component, not a pattern page)
- Do NOT change the FluentMessageBar to FluentToast in Pattern 1 — the persistent notification is a deliberate design choice
- Do NOT add complex command lifecycle visualization (StatusBadge, CommandPipeline) — those are v2 Dashboard components (UX spec lines 1114-1166), not sample UI
- Do NOT create new projects or move files — the sample UI structure is established
- Do NOT modify the SignalR client library or hub — those are implemented in prior stories (10-1, 10-2, 10-3)
- Do NOT add unit tests for Razor pages — Blazor component tests require bUnit which is not in the project's test stack; manual smoke testing suffices for this sample
- Do NOT implement full 8-state command lifecycle colors (Received/Processing/EventsStored/EventsPublished/Completed/Rejected/PublishFailed/TimedOut) — the sample UI only observes success/fail/idle from HTTP responses; full lifecycle is v2 Dashboard scope

### Definition of Done

- All 5 ACs verified in code
- Build: `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
- Tier 1 tests pass with 0 regressions
- Manual smoke test passes per Task 3.3 structured script (human verification)
- Branch: `feat/story-12-1-three-blazor-refresh-patterns`

### Project Structure Notes

- All Blazor sample files are in `samples/Hexalith.EventStore.Sample.BlazorUI/`
- Components go in `Components/` subdirectory
- Pages go in `Pages/` subdirectory
- Services go in `Services/` subdirectory
- Layout files go in `Layout/` subdirectory
- The project uses Blazor Server with interactive server-side rendering

### References

- [Source: _bmad-output/planning-artifacts/epics.md, Epic 12, Stories 12.1-12.2]
- [Source: _bmad-output/planning-artifacts/epics.md, lines 259 (SCP-Buttons), 323 (UX-DR40)]
- [Source: _bmad-output/planning-artifacts/prd.md, line 845 (FR60 — 3 reference patterns)]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md, lines 547-581 (status colors)]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md, lines 392-394 (FluentToast, FluentBadge)]
- [Source: samples/Hexalith.EventStore.Sample.BlazorUI/ — complete existing implementation]
- [Source: src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs — client library]
- [Source: src/Hexalith.EventStore.CommandApi/SignalR/ — hub, broadcaster, options]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs — Aspire topology (lines 75-94)]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
