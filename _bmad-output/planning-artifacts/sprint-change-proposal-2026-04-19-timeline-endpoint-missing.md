# Sprint Change Proposal: `/timeline` Endpoint Missing on EventStore Service (Events Page Silently Empty)

**Date:** 2026-04-19
**Triggered by:** Live observation — `https://localhost:60034/events` remained empty even after the 2026-04-18 timeout fix (`sprint-change-proposal-2026-04-18-events-page-slow.md`) was built and deployed. Fresh structured logs revealed the real cause.
**Scope Classification:** Minor — Direct implementation by dev team
**Supersedes (partial):** `sprint-change-proposal-2026-04-18-events-page-slow.md` — that memo's root-cause diagnosis (5 s HttpClient timeout) was incorrect. The 5 s → 30 s bump is retained as a safety margin, but the true defect was a missing server-side route, not a timeout.
**Related:** `sprint-change-proposal-2026-04-18-tenant-query-auth.md` (separate 401/400 bug on `/api/v1/queries`, independent flow).

---

## Section 1: Issue Summary

**Symptom:** The Admin UI `/events` page renders with `Recent Events = 0`, `Unique Event Types = 0`, `Active Streams = 0` and an empty grid, even though:

- Commands have been submitted (EventStore logs show `Events persisted: ... NewSequence=23` for `tenant-a/counter/counter-1`).
- The `admin:stream-activity:all` index is populated correctly.
- The `/streams` page and Commands page both work.
- The 30 s HttpClient timeout fix (from the 2026-04-18 memo) is in effect.

**Root cause:** `DaprStreamQueryService.GetStreamTimelineAsync` (`src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs:137-170`) invokes `GET api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/timeline` on the EventStore app via DAPR service invocation. **That route does not exist on the EventStore service.** `AdminStreamQueryController.cs:31` is routed at `api/v1/admin/streams` but only exposes `/bisect`, `/blame`, `/step`, and `/sandbox`. The EventStore returns 404 in ~2 ms, Polly retries four times, then `InvokeEventStoreAsync` throws `HttpRequestException: Response status code does not indicate success: 404 (Not Found)` at line 515. `Events.razor` swallows the exception in its per-stream `catch` block, so every stream is silently skipped and the page stays empty.

**Why the 2026-04-18 memo missed this:** It relied on UI-side log signatures (`Resilience event OnRetry Result:503`) without inspecting Admin.Server traces. The 503 the UI sees is the Admin.Server's own `ServiceUnavailable` response produced by its `AdminStreamsController` exception handler — which fires for **any** downstream failure, including a 404. So the surface symptom looks identical to a timeout, while the underlying cause is completely different. The `.NET HttpClient` timeout bump had no effect because the call was failing fast, not slow.

**Why Story 15.12 didn't catch this:** Story 15.12's tests mocked `AdminStreamApiClient.GetStreamTimelineAsync` directly. Neither unit nor integration tests ever wired a real HTTP request through to a real `AdminStreamQueryController`, so the missing route was never exercised end-to-end.

**Evidence:**

| Source | Reference |
|---|---|
| Admin.Server trace | trace_id `350b758`, log_id `4748`: `System.Net.Http.HttpRequestException: Response status code does not indicate success: 404 (Not Found). at DaprStreamQueryService.InvokeEventStoreAsync (DaprStreamQueryService.cs:515) at GetStreamTimelineAsync (line 159)` |
| DAPR invoke URL | `http://localhost:49974/v1.0/invoke/eventstore/method/api/v1/admin/streams/tenant-a/counter/counter-1/timeline?*` → 404 in ~1.4 ms |
| EventStore logs | No incoming `/timeline` requests recorded — request is rejected by DAPR router before reaching user code |
| Source confirmation | `Grep -n "HttpGet"` in `AdminStreamQueryController.cs` returns only `bisect`, `blame`, `step`, `sandbox` — no `timeline` |
| Dependent callers (8+) | `AdminStreamApiClient.cs`, `DaprStreamQueryService.cs`, `AdminStreamsController.cs`, `StreamTools.cs` (MCP), `AdminApiClient.Streams.cs` (MCP), `StreamEventsCommand.cs` (CLI), `Events.razor`, `StreamDetail.razor`, `BisectTool.razor` |

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact |
|------|--------|
| **Epic 15 (Admin Web UI)** | Multiple stories silently depend on `/timeline`: 15.3 (stream browser), 15.4 (aggregate state inspector — uses timeline entries for navigation), 15.12 (Events page). Every user-facing timeline surface is empty or degraded. One follow-up story (15.12a) patches the root cause. No scope change. |
| All other epics | None. |

### Story Impact

| Story | Action |
|-------|--------|
| Story 15.12 (Events Page Cross-Stream Browser) | Append dated note pointing at this proposal. No AC change. |
| Story 15.3 (Stream Browser) | Was already broken by the same defect but not surfaced because streams with no events rendered as "empty timeline" rather than "error". Auto-fixed by this patch. |
| Story 15.4 (Aggregate State Inspector) | Same as 15.3. |
| **New Story 15.12a** (Implement Missing Timeline Endpoint) | Added to `sprint-status.yaml` as `backlog`. |

### Artifact Conflicts

| Artifact | Conflict | Action |
|----------|----------|--------|
| PRD | None | FR69 ("unified command/event/query timeline") is the intended behaviour — implementation catches up. |
| Architecture | None | One new action on an existing controller. No pattern or contract change. |
| UX Design | None | UI already implements the correct shape once the endpoint returns data. |
| epics.md | Minor | Dated follow-up note under Story 15.12. |
| sprint-status.yaml | Minor | Add `15-12a-implement-missing-timeline-endpoint: backlog` under Epic 15. |
| Tests | Minor additive | New Tier 1 unit test file for the endpoint. Story 15.12a is expected to also add an integration test to prevent regression. |
| CI/CD / IaC / deployment | None | |

### Technical Impact

- **3 modified/new source files:**
  - **NEW** — `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` — 1 new action (`GetStreamTimelineAsync`) + 1 `using` directive for `PagedResult<T>`.
  - **NEW** — `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTimelineTests.cs` — 6 Tier 1 unit test cases.
  - **EDIT** — `_bmad-output/planning-artifacts/epics.md` — append dated follow-up note under Story 15.12.
  - **EDIT** — `_bmad-output/implementation-artifacts/sprint-status.yaml` — add one story entry.
- **0 API contract changes.** The route shape and response DTO (`PagedResult<TimelineEntry>`) match exactly what `DaprStreamQueryService` already expects. Clients need no code changes.
- **0 schema changes / 0 infrastructure changes.**

---

## Section 3: Recommended Approach

**Selected path:** Direct Adjustment — Option 1.

**How it works:**

1. Add a `GetStreamTimelineAsync` action to `AdminStreamQueryController.cs`, mirroring the existing `bisect`/`blame`/`step` pattern: actor proxy → `actor.GetEventsAsync(fromSequence)` → range-filter by `to` → project each envelope to a `TimelineEntry` with `EntryType = Event` → wrap in `PagedResult<TimelineEntry>`.
2. Scope to events-only in this patch. Commands-in-timeline and queries-in-timeline (the "unified" part of FR69) are a separate enhancement and are not required to unbreak the Events page.
3. Add Tier 1 unit tests covering happy path, empty stream, range filters, count cap, bad-request validation, and `UserId` empty-string → `null` projection.

**Why Option 1 over alternatives:**

- **Option 2 — Rewrite `DaprStreamQueryService.GetStreamTimelineAsync` to read state-store entries directly (no HTTP hop)**: architecturally cleaner (one DAPR state read vs. actor round-trip) but violates the existing "Admin.Server delegates reads to EventStore via DAPR invoke" pattern used by every other admin query. Effort: Medium. Risk: Medium (touches 8+ callers by implication).
- **Option 3 — Rollback Story 15.12**: regresses shipped Events UX. Doesn't solve the problem for 15.3 or 15.4 which have the same defect. Not viable.
- **Option 1 (selected)**: smallest blast radius. Fixes the Events page, StreamDetail page, BisectTool, MCP tools, and CLI `stream events` command simultaneously. Effort: Low (~1 hr incl. tests). Risk: Low.

**Effort estimate:** Low — single developer, one session.
**Risk level:** Low — additive endpoint, no existing behaviour changes, mirrors tested sibling patterns.
**Timeline impact:** None.

---

## Section 4: Detailed Change Proposals

### 4.1 NEW — `GetStreamTimelineAsync` action in EventStore `AdminStreamQueryController`

**File:** `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`

Add `using Hexalith.EventStore.Admin.Abstractions.Models.Common;` at the top (for `PagedResult<T>`).

Insert the following action between `BisectAggregateStateAsync` and `GetAggregateBlameAsync`:

```csharp
/// <summary>
/// Returns a paginated timeline of events for the specified aggregate stream.
/// Used by the Admin UI Events page, StreamDetail page, MCP and CLI tools (FR69).
/// </summary>
[HttpGet("{tenantId}/{domain}/{aggregateId}/timeline")]
[ProducesResponseType(typeof(PagedResult<TimelineEntry>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GetStreamTimelineAsync(
    string tenantId,
    string domain,
    string aggregateId,
    [FromQuery] long? from,
    [FromQuery] long? to,
    [FromQuery] int count = 100,
    CancellationToken _ = default) {
    if (from.HasValue && from.Value < 0) {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: "Parameter 'from' must be >= 0 when provided.");
    }

    if (to.HasValue && to.Value < 1) {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: "Parameter 'to' must be >= 1 when provided.");
    }

    if (from.HasValue && to.HasValue && to.Value < from.Value) {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: "Parameter 'to' must be >= 'from'.");
    }

    if (count <= 0) {
        count = 100;
    }

    try {
        var identity = new AggregateIdentity(tenantId, domain, aggregateId);
        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId), "AggregateActor");

        ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(from ?? 0).ConfigureAwait(false);

        IEnumerable<ServerEventEnvelope> filtered = allEvents;
        if (to.HasValue) {
            filtered = filtered.Where(e => e.SequenceNumber <= to.Value);
        }

        List<TimelineEntry> entries = [.. filtered
            .OrderBy(e => e.SequenceNumber)
            .Take(count)
            .Select(e => new TimelineEntry(
                e.SequenceNumber,
                e.Timestamp,
                TimelineEntryType.Event,
                e.EventTypeName,
                e.CorrelationId,
                string.IsNullOrWhiteSpace(e.UserId) ? null : e.UserId))];

        return Ok(new PagedResult<TimelineEntry>(entries, entries.Count, null));
    }
    catch (OperationCanceledException) {
        throw;
    }
    catch (Exception ex) {
        logger.LogError(ex,
            "Failed to fetch stream timeline for {TenantId}/{Domain}/{AggregateId}.",
            tenantId, domain, aggregateId);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "Failed to fetch stream timeline.");
    }
}
```

**Rationale:** Restores the endpoint the Admin.Server and downstream callers have always expected. Mirrors the actor-proxy + `GetEventsAsync` pattern used by `bisect`/`blame`/`step` in the same file, so no new architectural ground is broken. Events-only for now; commands/queries can be layered on in a future FR69 enhancement story.

### 4.2 NEW — Tier 1 unit tests for the new action

**File:** `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTimelineTests.cs`

Cases (xUnit + Shouldly + NSubstitute, following the existing `QueriesControllerTests` pattern):

1. Happy path — actor returns 3 envelopes; endpoint returns `PagedResult<TimelineEntry>` with 3 items, `EntryType = Event`, correct `SequenceNumber`/`Timestamp`/`TypeName`/`CorrelationId`/`UserId` projection.
2. Empty stream — actor returns `[]`; endpoint returns `200 OK` with empty `Items` and `TotalCount = 0` (not 404 — empty stream is valid).
3. Range filter — actor returns 10 events; `from=3&to=7` → exactly sequences 3..7 returned.
4. Count cap — actor returns 500 events; `count=25` → 25 items returned.
5. Bad request validation — `from=-1` → 400; `to=0` → 400; `from=5&to=3` → 400.
6. `UserId` empty-string → `null` — matches `TimelineEntry.UserId` nullability contract.

**Rationale:** Prevents regression if the endpoint is ever removed or its route renamed. Protects the 8+ callers that silently depend on this contract.

### 4.3 EDIT — `epics.md` dated follow-up

**File:** `_bmad-output/planning-artifacts/epics.md`

Append after Story 15.12's closing approach bullets (approximately line 600):

```markdown
---
**2026-04-19 follow-up (Sprint Change Proposal):** Story 15.12 shipped referencing a `/timeline` endpoint on the EventStore service that was never implemented — all per-stream fetches returned 404 and were silently swallowed. See `sprint-change-proposal-2026-04-19-timeline-endpoint-missing.md` for root cause and the Story 15.12a fix (implement `GetStreamTimelineAsync` in `AdminStreamQueryController.cs`). Supersedes the timeout-focused diagnosis in `sprint-change-proposal-2026-04-18-events-page-slow.md` (the 5 s → 30 s timeout bump is retained as a safety margin but was not the root cause).
```

**Rationale:** Leaves a breadcrumb so future readers understand why there are two memos for the same symptom, and which one represents the real fix.

### 4.4 EDIT — `sprint-status.yaml` new story

**File:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

Insert after line 183 (`15-12-events-page-cross-stream-browser: done`):

```yaml
  15-12a-implement-missing-timeline-endpoint: backlog
```

**Rationale:** Checklist 6.4 — keeps sprint status consistent with approved epic changes.

---

## Section 5: Implementation Handoff

**Scope:** Minor — direct implementation by the dev team.

**Verification checklist:**

| # | Check | Status |
|---|-------|--------|
| 1 | `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 warnings, 0 errors | Pending developer |
| 2 | `dotnet test tests/Hexalith.EventStore.Server.Tests/` — Tier 2 including new timeline tests green | Pending developer |
| 3 | Rebuild `eventstore` resource in Aspire (live host) | Pending developer |
| 4 | Reload `https://localhost:60034/events` — stat cards populate, grid shows recent events across streams, warning banner absent | Pending developer |
| 5 | Reload `https://localhost:60034/streams/tenant-a/counter/counter-1` — timeline populates | Pending developer |
| 6 | Verify `/commands` page and Bisect tool unaffected | Pending developer |
| 7 | Re-run the 2026-04-18 tenant-query-auth and events-page-slow memos' checks — confirm they remain green | Pending developer |

**New Story ID:** `15-12a-implement-missing-timeline-endpoint`

**Dependencies:** None. Entirely additive.

**Deliverables:**

1. New action in `AdminStreamQueryController.cs`.
2. New test file `AdminStreamQueryControllerTimelineTests.cs`.
3. Updated `epics.md` and `sprint-status.yaml`.

**Follow-ups (separate stories, NOT in this patch):**

- **FR69 unified view** — extend `GetStreamTimelineAsync` to also return `Command` and `Query` entries so the Events page (or a dedicated Unified Timeline page) can show all three kinds interleaved. Requires a design decision on how to merge command activity (from `admin:command-activity:all`) with actor-sourced events.
- **Tier 3 end-to-end assertion** — when the Playwright suite is wired up, add a test that drives `/events` and asserts non-empty stat cards + at least one grid row after a sample command is submitted.

---

## Appendix: Why the Prior Memo Was Incomplete

The 2026-04-18 memo (`sprint-change-proposal-2026-04-18-events-page-slow.md`) diagnosed the symptom correctly (empty Events page, Polly 503 retries on `/timeline`) but traced the cause to the wrong layer. It looked at:

1. **UI logs** — saw `Resilience event OnRetry Result:503` on the timeline URL.
2. **`AdminUIServiceExtensions.cs:81`** — found the 5 s HttpClient timeout.
3. **Concluded** — 5 s is too short for 50-way parallel actor activation tail latency.

What it did not look at:

1. **Admin.Server logs** — would have shown `HttpRequestException: 404 (Not Found)` immediately.
2. **EventStore `AdminStreamQueryController.cs`** — would have shown no `/timeline` route.

The 503 the UI sees is the Admin.Server's own `ServiceUnavailable()` exception handler (`AdminStreamsController.cs:107`) firing for any downstream failure — including a 404. That handler turns an unambiguous "route not found" signal into a generic "service unavailable", which in turn looks indistinguishable from a timeout to the UI. The two memos are both correct in their own layer; this one fixes the real defect.

**Lesson:** Admin.Server's exception-to-HTTP mapping should distinguish between "downstream responded with a permanent 4xx" and "downstream is transiently unavailable" so that future diagnoses can be faster. Filed as an optional follow-up, not part of this patch.
