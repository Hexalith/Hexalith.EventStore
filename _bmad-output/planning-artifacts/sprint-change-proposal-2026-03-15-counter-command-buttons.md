---
title: "Sprint Change Proposal: Add Counter Command Buttons to All Pattern Pages"
date: 2026-03-15
scope: Minor
status: Draft
trigger: User report — increment/decrement/reset buttons not visible in sample UI
---

# Sprint Change Proposal: Add Counter Command Buttons to All Pattern Pages

## Section 1: Issue Summary

**Problem Statement:** The Blazor sample UI's Pattern 1 (Notification) and Pattern 2 (Silent Reload) pages are display-only — they show the counter value but provide no way to modify it. The `CounterCommandForm` component (with Increment, Decrement, and Reset buttons) is only included on Pattern 3 (Selective Refresh). This makes Patterns 1 and 2 non-interactive dead ends unless the user has Pattern 3 open in a separate tab.

**Discovery Context:** Reported by user during sample UI usage. The buttons exist in the codebase (`CounterCommandForm.razor`) but are not rendered on two of three pattern pages.

**Evidence:**
- `grep -r "CounterCommandForm"` returns only one reference: `SelectiveRefreshPattern.razor:23`
- `NotificationPattern.razor` and `SilentReloadPattern.razor` contain no command form reference

## Section 2: Impact Analysis

### Epic Impact
- **Epic 18** (Query Pipeline & Real-Time Updates): In-progress. Story 18-6 (Sample UI Refresh Patterns) is marked done but left this gap. No epic-level change required — this is a minor fix within existing scope.
- **All other epics:** No impact.

### Story Impact
- **Story 18-6** (Sample UI Refresh Patterns): Acceptance criteria gap — patterns should be self-contained interactive demos. No new story needed; this is a fixup within the completed story's intent.

### Artifact Conflicts
- **PRD:** No conflict. Sample completeness aligns with functional requirements.
- **Architecture:** No impact. Same `CounterCommandForm` component, same `/api/v1/commands` endpoint.
- **UX Design:** No conflict. Adding the component improves UX consistency across all pattern pages.

### Technical Impact
- Two `.razor` files modified (one line each).
- No new components, no new dependencies, no API changes.

## Section 3: Recommended Approach

**Selected Path:** Direct Adjustment

**Rationale:** The `CounterCommandForm` component already exists and works correctly on Pattern 3. Adding it to Patterns 1 and 2 is a single-line markup addition per file. Zero risk, zero architectural impact, immediate UX improvement.

**Effort:** Low (< 5 minutes)
**Risk:** Low (no new code, no behavior changes)
**Timeline Impact:** None

## Section 4: Detailed Change Proposals

### Change 1: SilentReloadPattern.razor

**File:** `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/SilentReloadPattern.razor`
**Section:** After the `</FluentCard>` closing tag (line 53)

**OLD:**
```razor
</FluentCard>

@code {
```

**NEW:**
```razor
</FluentCard>

<CounterCommandForm TenantId="@_tenantId" />

@code {
```

**Rationale:** Makes the Silent Reload pattern page self-contained — users can send increment/decrement/reset commands and immediately see the silent reload behavior.

### Change 2: NotificationPattern.razor

**File:** `samples/Hexalith.EventStore.Sample.BlazorUI/Pages/NotificationPattern.razor`
**Section:** After the `</FluentCard>` closing tag (line 60)

**OLD:**
```razor
</FluentCard>

@code {
```

**NEW:**
```razor
</FluentCard>

<CounterCommandForm TenantId="@_tenantId" />

@code {
```

**Rationale:** Makes the Notification pattern page self-contained — users can trigger counter changes and observe the notification bar behavior.

## Section 5: Implementation Handoff

**Change Scope:** Minor — Direct implementation by dev team.

**Handoff:**
- **Recipient:** Development team
- **Deliverables:** Two `.razor` file edits as specified above
- **Dependencies:** None
- **Success Criteria:** All three pattern pages display Increment, Decrement, and Reset buttons; buttons successfully send commands to the EventStore API
