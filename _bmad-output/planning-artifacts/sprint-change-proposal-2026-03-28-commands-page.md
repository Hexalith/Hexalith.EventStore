# Sprint Change Proposal: Commands Page Implementation

**Date:** 2026-03-28
**Triggered by:** Admin Commands page shows empty state despite commands being sent to the sample application
**Scope:** Minor (direct implementation by development team)

---

## 1. Issue Summary

The admin Commands page (`/commands`) displays a static "No commands processed yet" empty state regardless of system activity. The page is a stub — `Commands.razor` renders only an `EmptyState` component with no data fetching, no API client integration, and no backend endpoint to query recent commands.

The UX Design Specification defines a comprehensive Commands page (Direction D1: Command-Centric) with FluentDataGrid, status filter chips, sortable columns, tenant selector, and summary stat cards. This was intended to be Jerome's primary entry point for command investigation (Journey 2).

**Evidence:**
- `Commands.razor` contains 19 lines with only a static `EmptyState` component
- No `AdminCommandsController` or equivalent query endpoint exists
- No `GetRecentCommandsAsync()` method on `IStreamQueryService` or any admin service
- Story 15-3 (stream-browser-command-event-query-timeline) is marked `done` but only delivered the per-stream timeline view, not the cross-stream Commands list

---

## 2. Impact Analysis

### Epic Impact
- **Epic 15** (Admin Web UI - Core Developer Experience): Story 15-3 delivered the stream detail timeline but missed the cross-stream Commands page. Epic cannot be considered complete for FR69 ("Unified command/event/query timeline") without this.

### Story Impact
- **Story 15-3** was marked done prematurely — the stream timeline portion is correct, but the Commands page was omitted.
- **New Story 15-9** needed: "Commands Page — Cross-Stream Command List with Filters and Stats"

### Artifact Conflicts
- **PRD**: No changes needed. FR69 correctly requires this capability.
- **Architecture**: No changes needed. Backend data path exists (ICommandStatusStore + IStreamQueryService). New query method required on service interface.
- **UX Specification**: No changes needed. D1 direction is fully specified.

### Technical Impact
The gap is in three layers:
1. **Admin Abstractions**: Need `GetRecentCommandsAsync()` on `IStreamQueryService` (or new dedicated service) returning recent commands across all streams
2. **Admin Server**: Need API endpoint `GET /api/v1/admin/commands` with tenant/status/type filters
3. **Admin UI**: Need full `Commands.razor` page with FluentDataGrid, filters, pagination, auto-refresh

---

## 3. Recommended Approach

**Selected Path:** Direct Adjustment — add new story 15-9 within existing Epic 15 structure.

**Rationale:**
- All architectural patterns are established (Streams page is the reference implementation)
- `AdminStreamApiClient` already follows the exact patterns needed (error handling, pagination, named HttpClient)
- FluentDataGrid, StatusBadge, filter bar components already exist and are tested
- The DaprStreamQueryService already queries DAPR state store for stream data — the same pattern extends to command status queries
- Estimated effort: 1 story (comparable to Story 15-2 or 15-5)
- Risk: Low — no new architectural patterns, no new dependencies

---

## 4. Detailed Change Proposals

### Change 1: New Story in Epics

**Artifact:** `_bmad-output/planning-artifacts/epics.md`
**Section:** Epic 15 (insert after the epic description, before Epic 16)

**NEW (add story):**

```
### Story 15.9: Commands Page — Cross-Stream Command List with Filters

As a developer investigating command behavior,
I want to see a filterable list of recent commands across all streams,
So that I can quickly find and investigate commands without knowing the specific stream.

**Acceptance Criteria:**

**Given** the Commands page,
**When** loaded,
**Then** a FluentDataGrid displays recent commands with columns: Status, Command Type, Tenant, Domain, Aggregate ID, Correlation ID, Timestamp.

**Given** the Commands page,
**When** filter chips are used,
**Then** commands can be filtered by status (All/Completed/Processing/Rejected/Failed), tenant, and command type.

**Given** the Commands page header,
**When** rendered,
**Then** summary stat cards show: Total Commands, Success Rate, Failed Count, Average Latency.

**Given** a command row,
**When** clicked,
**Then** navigation proceeds to the stream detail page filtered to that command's correlation ID.

**Given** the Commands page,
**When** the dashboard auto-refresh fires,
**Then** the command list updates without losing filter/pagination state (same pattern as Streams page).

**Technical Notes:**
- Reference implementation: Streams.razor page pattern (API client, filter bar, pagination, refresh subscription)
- Backend: Add GetRecentCommandsAsync() to IStreamQueryService + DaprStreamQueryService
- API: Add GET /api/v1/admin/commands endpoint to AdminStreamsController (or new AdminCommandsController)
- UI: Replace Commands.razor stub with full FluentDataGrid implementation
- UX spec reference: D1 (Command-Centric), Journey 2 (Jerome's Command Investigation)
```

### Change 2: Sprint Status Update

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`
**Section:** Epic 15

**OLD:**
```yaml
  15-8-deep-linking-and-breadcrumbs: done
  epic-15-retrospective: optional
```

**NEW:**
```yaml
  15-8-deep-linking-and-breadcrumbs: done
  15-9-commands-page-cross-stream-command-list: backlog
  epic-15-retrospective: optional
```

**Rationale:** Adds the new story to the sprint tracker. Status starts as `backlog` until the story file is created.

---

## 5. Implementation Handoff

**Change Scope:** Minor — direct implementation by development team.

**Handoff:**
- **Developer**: Implement story 15-9 following Streams.razor as reference pattern
- **Key files to create/modify:**
  - `src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs` — add `GetRecentCommandsAsync()`
  - `src/Hexalith.EventStore.Admin.Abstractions/Models/Commands/` — add `CommandSummary` DTO
  - `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` — implement query
  - `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` — add endpoint (or new controller)
  - `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` — add client method
  - `src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor` — replace stub with full page

**Success Criteria:**
- Commands page shows recent commands from all streams
- Filter chips work for status, tenant, and command type
- Summary stat cards display accurate metrics
- Row click navigates to stream detail
- Auto-refresh works without losing filter state
- Existing tests continue to pass
