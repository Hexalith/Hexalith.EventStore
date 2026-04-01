# Sprint Change Proposal: Events Page Shows No Events

**Date:** 2026-04-01
**Triggered by:** Admin UI Events page (`/events`) displays static "No events stored yet" stub — no data fetching implemented
**Scope Classification:** Minor — Direct implementation by dev team

---

## Section 1: Issue Summary

The Events page at `/events` in the Admin UI is a hardcoded `EmptyState` stub. Unlike the fully-implemented Commands page (`/commands`), the Events page has no data loading, no filtering, no grid, and no real-time refresh. It always shows "No events stored yet" regardless of system state.

**Root cause:** The Events page was defined in the UX spec (D3, Timeline-Centric direction) and has a NavMenu link, but was never implemented beyond the stub. No dedicated story existed for a cross-stream events listing page — Story 15.3 covered per-stream timeline (at `/streams/{t}/{d}/{id}`), and Story 15.9 covered the Commands page.

**Evidence:**
- `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor` — 15 lines, hardcoded EmptyState, zero data fetching
- Commands page (`Commands.razor`) — 460 lines with full data grid, filters, pagination, refresh
- UX spec D3 defines `/events` as "Activity histogram, time-range picker, chronological event stream with filter chips"

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact | Detail |
|------|--------|--------|
| Epic 15 (Admin Web UI) | Add story | New story for Events page implementation |
| All other epics | No impact | Unaffected |

### Story Impact

No existing stories need modification. A single new story covers the Events page implementation.

### Artifact Conflicts

| Artifact | Conflict | Action |
|----------|----------|--------|
| PRD | None | FR69 covers per-stream timeline (done). Cross-stream events browsing is an implied UX feature |
| Architecture | None | No backend changes — approach A uses existing APIs only |
| UX Design | Minor simplification | D3 specifies histogram + time-range picker; this delivers the core data grid first. Histogram is a future enhancement |
| Epics | None | Additive story within Epic 15 |

### Technical Impact

- **1 file modified** (`Events.razor` — complete rewrite from stub to functional page)
- **0 new files** — all shared components already exist
- **0 backend changes** — uses existing `GetRecentlyActiveStreamsAsync` + `GetStreamTimelineAsync` APIs
- **0 API contract changes**
- **0 infrastructure changes**

---

## Section 3: Recommended Approach

**Selected path:** Direct Adjustment — Approach A (lightweight, UI-only).

**How it works:**
1. On page load, call `AdminStreamApiClient.GetRecentlyActiveStreamsAsync()` to get active streams
2. For each stream (capped at 50), call `GetStreamTimelineAsync(count: 100)` and filter to `EntryType == Event`
3. Merge all event entries, sort by timestamp descending
4. Display in `FluentDataGrid` following the Commands.razor pattern exactly

**Rationale:**
- Zero backend changes — no new APIs, no new DTOs, no new DAPR state
- Follows established Commands.razor pattern for consistency
- All required APIs and shared components already exist
- N+1 API calls are acceptable for admin/dev workloads (typically 10-50 streams)
- Delivers the core D3 experience; histogram and time-range picker can follow as enhancements

**Effort estimate:** Low (single story, ~2-3 hours implementation)
**Risk level:** Low (no API changes, no schema changes, UI-only)
**Timeline impact:** None — fits within current sprint

---

## Section 4: Detailed Change Proposals

### Replace Events.razor stub with functional data grid

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor`

**OLD (entire file — 15 lines):**
```razor
@page "/events"

<PageTitle>Events - Hexalith EventStore</PageTitle>

<h1 style="font-size: 24px; margin: 0 0 16px;">Events</h1>

<EmptyState Title="No events stored yet."
            Description="Events will appear here once commands are processed and events are persisted."
            Icon="@EventsIcon"
            ActionLabel="Read the getting started guide"
            ActionHref="https://github.com/Hexalith/Hexalith.EventStore/blob/main/docs/getting-started/quickstart.md" />

@code {
    private static RenderFragment EventsIcon => builder => builder.AddMarkupContent(0, "&#128340;");
}
```

**NEW:** Complete rewrite implementing:

1. **Data loading** — `OnInitializedAsync` fetches recently active streams via `AdminStreamApiClient.GetRecentlyActiveStreamsAsync()`, then for each stream (capped at 50) fetches timeline via `GetStreamTimelineAsync(count: 100)`, filters to `EntryType == Event`, merges and sorts by timestamp descending
2. **Data grid** — `FluentDataGrid` with columns: Event Type (monospace), Tenant, Domain, Aggregate ID (truncated with tooltip), Correlation ID (truncated with tooltip), Timestamp (monospace, default sort descending)
3. **StatCards** — Total Events, Unique Streams, Unique Event Types
4. **Filters** — Tenant dropdown (`FluentSelect`), Event Type text filter (`FluentTextField`), same pattern as Commands page
5. **Pagination** — Previous/Next buttons with page counter, 25 items per page (same as Commands)
6. **Loading states** — `SkeletonCard` during load, `EmptyState` when no events match filters, error message on API failure, forbidden state on 403
7. **Real-time refresh** — Subscribe to `DashboardRefreshService.OnDataChanged`, re-fetch on signal with scroll position preservation
8. **Row click** — Navigate to `/streams/{tenant}/{domain}/{aggregateId}?detail={sequenceNumber}` for event detail inspection
9. **URL sync** — Query params for `page`, `tenant`, `eventType` with `NavigationManager` (same pattern as Commands)
10. **Dispose** — `IAsyncDisposable` with `_disposed` guard (same pattern as Commands)

**Justification:** Follows the Commands.razor pattern exactly. No backend changes needed. Delivers the D3 (Timeline-Centric) core experience using existing APIs and shared components.

---

## Section 5: Implementation Handoff

**Scope:** Minor — direct implementation by dev team.

**Handoff:**

| Role | Responsibility |
|------|---------------|
| Developer | Rewrite Events.razor following Commands.razor pattern |
| Developer | Run Tier 1 tests to verify no regressions |
| Developer | Manual smoke test: run AppHost, increment counter, verify Events page shows events with correct types, filtering works, row click navigates to stream detail |

**Success Criteria:**

1. Events page shows events after counter increments (at minimum `CounterIncremented` events)
2. Tenant filter scopes events correctly
3. Event type filter narrows results
4. Row click navigates to stream detail with event selected
5. Page refreshes automatically when new events arrive
6. Loading skeleton shown during fetch
7. Error message shown when Admin API is unavailable (not silent empty state)
8. Tier 1 tests pass
9. Project builds with zero warnings

**New Story ID:** `15-12-events-page-cross-stream-browser`

**Dependencies:** None — uses only existing APIs and components
