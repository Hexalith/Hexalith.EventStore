# Sprint Change Proposal: `/events/{seq}` Endpoint Missing on EventStore Service (Event Detail Panel Returns Misleading 503)

**Date:** 2026-05-05
**Triggered by:** Live observation via Aspire MCP during fresh-boot reproduction of the full local topology — Event Detail panel returns a "Service unavailable" error banner for sequences that exist in Redis. Two structured-log traces confirmed reproducibility:
- `trace_id 2a404d3f05e8ea0d21e1b301ef2fdcf1` — first occurrence (Admin UI initial page load, RequestIds `0HNLAERCD8BG5:00000012-16`).
- `trace_id 2608793690c186663cf3a8e1e48a8a9b` — second occurrence (later in same session, different connection `0HNLAERCD8BG6`, same defect signature).

**Scope Classification:** Minor — Direct implementation by dev team.
**Related:** `sprint-change-proposal-2026-04-19-timeline-endpoint-missing.md` — same defect class on the sibling `/timeline` endpoint, fixed as Story 15.12a. The 04-19 memo's appendix filed exactly the predicate-discriminator fix promoted in-scope here as an "optional follow-up". Today's recurrence justified the promotion.
**Supersedes:** None.
**Story:** New Story `15-12b-implement-missing-event-detail-endpoint` filed in `sprint-status.yaml` under Epic 15.

---

## Section 1: Issue Summary

**Symptom.** Clicking an event row in the Admin UI Events page (`/events`) navigates to `/streams/{tenant}/{domain}/{aggregateId}?detail={sequenceNumber}` (per Story 15.12 AC #4), which opens an event-detail panel. That panel renders a "Service unavailable" banner, even though:

- The Events grid itself was just populated by `/timeline` (which works since 15.12a).
- The event's payload is present in Redis at `eventstore||AggregateActor||system:tenants:global-administrators||system:tenants:global-administrators:events:1`.
- The corresponding `GET .../timeline` returns `200 OK` with `{ totalCount: 1, items: [{ sequenceNumber: 1, ... }] }` referencing the same sequence the panel asks for.

**Root cause (two layers).**

1. **Missing route.** `Hexalith.EventStore.Admin.Server.Services.DaprStreamQueryService.GetEventDetailAsync` (`src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs:341`) invokes `GET api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber}` on the EventStore app via DAPR service invocation. **That route does not exist on the EventStore service.** `AdminStreamQueryController.cs` (routed at `api/v1/admin/streams`) only exposes `bisect` (line 47), `timeline` (line 243), `blame` (line 323), and `step` (line 421). The EventStore returns 404 in ~1.5 ms; `InvokeEventStoreAsync` throws `HttpRequestException` at line 515.

2. **Misleading 503 obfuscation.** `AdminStreamsController.GetEventDetail` (`src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs:321`) catches the `HttpRequestException` via `catch (Exception ex) when (IsServiceUnavailable(ex))` (line 335). The local predicate at `cs:435` matches **any** `HttpRequestException` regardless of upstream status code, so a permanent 404 is mistranslated into a transient 503. The UI sees `503 ServiceUnavailable` and surfaces "Admin backend service unavailable. Retry shortly." — exactly the wrong UX signal.

**Why this wasn't caught earlier.** Story 15.12's tests mocked `AdminStreamApiClient.GetEventDetailAsync` directly. Neither unit nor integration tests ever wired a real HTTP request through to a real `AdminStreamQueryController`, so the missing route was never exercised end-to-end. This is the same gap that produced the 2026-04-19 timeline defect.

**Why the 04-19 deferral re-bit us.** The 2026-04-19 memo's Appendix said:

> *"Admin.Server's exception-to-HTTP mapping should distinguish between 'downstream responded with a permanent 4xx' and 'downstream is transiently unavailable' so that future diagnoses can be faster. Filed as an optional follow-up, not part of this patch."*

That deferral lived only in a memo appendix. It was never assigned a story ID, never added to `sprint-status.yaml`, and never picked up. Today, when the second route gap (`events/{seq}`) appeared on the same controller, the same predicate-misclassification cost approximately one full debugging session — repeating the 04-19 cost. This proposal **closes that local follow-up** in `AdminStreamsController` *and* leaves a `TODO(15.12c-deferred)` breadcrumb above the duplicated predicate in seven sibling controllers so any future recurrence on a different surface is immediately greppable from code.

**Evidence:**

| Source | Reference |
|---|---|
| Admin.Server trace #1 | `trace_id 2a404d3`, log_id `1094`: `System.Net.Http.HttpRequestException: Response status code does not indicate success: 404 (Not Found). at DaprStreamQueryService.InvokeEventStoreAsync (DaprStreamQueryService.cs:515) at GetEventDetailAsync (line 343) at AdminStreamsController.GetEventDetail (line 328)` |
| Admin.Server trace #2 | `trace_id 2608793`, log_id `1293`: identical exception path, different connection `0HNLAERCD8BG6` |
| DAPR invoke URL | `http://localhost:56985/v1.0/invoke/eventstore/method/api/v1/admin/streams/system/tenants/global-administrators/events/1` → 404 in ~1.5–1.9 ms |
| Live probe with admin JWT | `GET http://localhost:8090/api/v1/admin/streams/system/tenants/global-administrators/timeline` → `200 OK` with `totalCount: 1, sequenceNumber: 1`. `GET .../events/0`, `.../events/1`, `.../events/2` all → `503 ServiceUnavailable`. |
| Redis state-store | `eventstore||AggregateActor||system:tenants:global-administrators||system:tenants:global-administrators:events:1` is present (DBSIZE = 16 after fresh boot). |
| Source confirmation (route side) | `Grep -n "HttpGet"` in `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` returns only `bisect`, `timeline`, `blame`, `step` — no `events/{seq}`. |
| Source confirmation (predicate side) | `IsServiceUnavailable` at `AdminStreamsController.cs:435` matches `ex is HttpRequestException` unconditionally; same shape duplicated across 7 sibling controllers (`AdminBackupsController.cs:240`, `AdminConsistencyController.cs:216`, `AdminDaprController.cs:183`, `AdminDeadLettersController.cs:198`, `AdminHealthController.cs:117`, `AdminProjectionsController.cs:246`, `AdminStorageController.cs:300`). |
| Dependent callers (5+) | `AdminStreamsController` (6 actions share the predicate), `DaprStreamQueryService.GetEventDetailAsync`, MCP `StreamTools` (event detail), CLI `stream event` command, `EventDetailPanel.razor`. |

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact |
|------|--------|
| **Epic 15 (Admin Web UI — done)** | Defect in shipped Story 15.12. The "click an event row to inspect detail" flow has been broken since 15.12 shipped. One follow-up story (`15-12b`) added to `sprint-status.yaml` with status `backlog`. No epic AC change. |
| All other epics | None. |

### Story Impact

| Story | Action |
|-------|--------|
| Story 15.12 (Events Page Cross-Stream Browser) | Append dated note pointing at this proposal. **No AC change** — AC #4 (`When I click an event row, Then I navigate to /streams/{tenant}/{domain}/{aggregateId}?detail={sequenceNumber}`) is correct as written; the bug is in the implementation behind the AC, not the AC itself. |
| Story 15.12a (Implement Missing Timeline Endpoint) | None — fix remains correct and unaffected. |
| **New Story 15.12b** (Implement Missing Event-Detail Endpoint + Refine Predicate) | Added to `sprint-status.yaml` as `backlog`. Scope = Proposals 4.1, 4.2, 4.3, 4.4, 4.5, 4.6. |

### Artifact Conflicts

| Artifact | Conflict | Action |
|----------|----------|--------|
| PRD | None | FR69 ("unified command/event/query timeline") and the per-event detail UX are the intended behaviour — implementation catches up. |
| Architecture | None | One new action on an existing controller + a localized predicate refinement on the consumer side. No pattern, contract, or topology change. |
| UX Design | None | UI already binds the `EventDetail` record correctly; once the endpoint returns it, the panel renders. |
| `epics.md` | Minor | Append second dated follow-up paragraph under Story 15.12, after the existing 04-19 paragraph at line 605. |
| `sprint-status.yaml` | Minor | Insert one line after `15-12a-implement-missing-timeline-endpoint: done` (line 218). |
| Tests | Minor additive | One new Tier 1 test file (`AdminStreamQueryControllerEventDetailTests`); four new cases appended to existing `AdminStreamsControllerTests`. |
| CI/CD / IaC / deployment | None | |

### Technical Impact

- **Modified files (3) + new files (1) + 7 comment-only edits = 11 files touched.**
  - **EDIT** — `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` — 1 new action (`GetEventDetailAsync`) + 1 `using` directive for `EventDetail`.
  - **EDIT** — `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` — refine `IsServiceUnavailable` predicate, add `IsTransientHttpFailure` and `MapDownstreamHttpError` helpers, insert one new `catch` arm in each of the 6 actions (`GetStreamTimeline`, `BisectAggregateState`, `GetAggregateBlame`, `GetEventStepFrame`, `GetEventDetail`, `SandboxCommand`). +1 `using System.Net;` and +1 `using Microsoft.AspNetCore.WebUtilities;` if absent.
  - **NEW** — `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerEventDetailTests.cs` — 7 Tier 1 unit test cases.
  - **EDIT** — `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsControllerTests.cs` — append 4 new cases for the predicate refinement.
  - **EDIT (comment-only ×7)** — Insert a 3-line `// TODO(15.12c-deferred): ...` comment above `IsServiceUnavailable` in each of: `AdminBackupsController.cs:240`, `AdminConsistencyController.cs:216`, `AdminDaprController.cs:183`, `AdminDeadLettersController.cs:198`, `AdminHealthController.cs:117`, `AdminProjectionsController.cs:246`, `AdminStorageController.cs:300`.
  - **EDIT** — `_bmad-output/planning-artifacts/epics.md` — append dated follow-up paragraph under Story 15.12.
  - **EDIT** — `_bmad-output/implementation-artifacts/sprint-status.yaml` — add one story entry.
- **0 API contract changes.** The route shape and response DTO (`EventDetail`) match exactly what `DaprStreamQueryService` already expects. Clients need no code changes.
- **0 schema changes / 0 infrastructure changes.**
- **Behavior change matrix (predicate refinement only):**
  | Scenario | Before | After |
  |---|---|---|
  | Downstream 404 (route or resource missing) | 503 ServiceUnavailable | **404 Not Found** |
  | Downstream 400 (validation) | 503 | **400** |
  | Downstream 401/403 (token) | 503 | **401/403** |
  | Downstream 408/429/502/503/504 | 503 | 503 *(unchanged)* |
  | Network failure / no response | 503 | 503 *(unchanged)* |
  | `TimeoutException` | 503 | 503 *(unchanged)* |
  | Transient gRPC `Unavailable`/`DeadlineExceeded`/`Aborted`/`ResourceExhausted` | 503 | 503 *(unchanged)* |

---

## Section 3: Recommended Approach

**Selected path:** Direct Adjustment — Option 1.

**How it works:**

1. Add `GetEventDetailAsync` to `AdminStreamQueryController.cs`, mirroring the existing `step` pattern (actor proxy → `actor.GetEventsAsync(0)` → filter for the requested sequence → project the envelope into an `EventDetail` record with payload bytes UTF-8 decoded into `PayloadJson`). Returns 404 when the sequence is not in the stream.
2. Refine `AdminStreamsController.IsServiceUnavailable` to discriminate transient vs. permanent HTTP failures using `HttpRequestException.StatusCode`. Add a `MapDownstreamHttpError` helper that propagates permanent upstream codes (404, 400, 401, 403, etc.) instead of masking them as 503. Insert one new `catch` arm per action.
3. Add Tier 1 unit tests for both the new endpoint (7 cases) and the refined predicate behavior (4 cases).
4. Defer the same predicate refinement on the seven sibling admin controllers — file the deferral as `// TODO(15.12c-deferred)` comments so it is greppable from code (no opaque memo-appendix tracking).

**Why Option 1 over alternatives:**

- **Option 2 — Rollback Story 15.12** (or its event-detail panel sub-feature): regresses shipped UX. Doesn't solve the underlying defect class. Not viable.
- **Option 3 — PRD MVP Review**: PRD is unaffected. FR69 remains the intent; this patch is implementation catching up to PRD.
- **Option 1 (selected)**: smallest blast radius. Fixes the Event Detail panel + closes the local 04-19 deferral on the same controller + records the broader cleanup as a grep-able TODO. Effort: Low (~1.5 hr incl. tests). Risk: Low (additive route, predicate change only along error paths that today are misleading, no happy-path change).

**Trade-off acknowledged.** Option 1 leaves the same predicate-misclassification bug in place on seven sibling controllers (`AdminBackupsController`, `AdminConsistencyController`, `AdminDaprController`, `AdminDeadLettersController`, `AdminHealthController`, `AdminProjectionsController`, `AdminStorageController`). Those controllers serve features (backups, consistency, projections, storage, etc.) that have not exhibited the misleading-503 symptom in production. Generalizing speculatively across all seven would expand scope from Minor to Moderate and is rejected per the project rule "Don't add features ... beyond what the task requires" (CLAUDE.md). The `TODO(15.12c-deferred)` comments make the deferral data-driven: if any of the seven ever shows the symptom, `grep TODO\(15.12c` resurfaces the recipe, and Story 15.12c is opened with real evidence.

**Effort estimate:** Low — single developer, ~1.5 hr session.
**Risk level:** Low — additive endpoint, behavior changes only along error paths that today are objectively wrong (404 misclassified as 503), no happy-path change.
**Timeline impact:** None.

---

## Section 4: Detailed Change Proposals

### 4.1 NEW — `GetEventDetailAsync` action in EventStore `AdminStreamQueryController`

**File:** `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`

Add `using Hexalith.EventStore.Admin.Abstractions.Models.Streams;` at the top (for `EventDetail`).

Insert the following action between `GetStreamTimelineAsync` (currently ending around line 317) and `GetAggregateBlameAsync` (currently starting at line 323):

```csharp
/// <summary>
/// Returns full detail (metadata + payload JSON) for a single event at the specified
/// sequence number. Used by the Admin UI Event Detail panel and the MCP/CLI tools.
/// </summary>
[HttpGet("{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber:long}")]
[ProducesResponseType(typeof(EventDetail), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetEventDetailAsync(
    string tenantId,
    string domain,
    string aggregateId,
    long sequenceNumber,
    CancellationToken ct = default) {
    if (sequenceNumber < 1) {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: "Parameter 'sequenceNumber' must be >= 1.");
    }

    try {
        var identity = new AggregateIdentity(tenantId, domain, aggregateId);
        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId), "AggregateActor");

        ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(0).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        ServerEventEnvelope? target = allEvents.FirstOrDefault(e => e.SequenceNumber == sequenceNumber);
        if (target is null) {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Event at sequence {sequenceNumber} not found.");
        }

        string payloadJson;
        try {
            payloadJson = System.Text.Encoding.UTF8.GetString(target.Payload);
        }
        catch {
            payloadJson = "{}";
        }

        EventDetail detail = new(
            TenantId: tenantId,
            Domain: domain,
            AggregateId: aggregateId,
            SequenceNumber: sequenceNumber,
            EventTypeName: target.EventTypeName ?? string.Empty,
            Timestamp: target.Timestamp,
            CorrelationId: target.CorrelationId ?? string.Empty,
            CausationId: string.IsNullOrWhiteSpace(target.CausationId) ? null : target.CausationId,
            UserId: string.IsNullOrWhiteSpace(target.UserId) ? null : target.UserId,
            PayloadJson: payloadJson);

        return Ok(detail);
    }
    catch (OperationCanceledException) {
        throw;
    }
    catch (Exception ex) {
        logger.LogError(ex,
            "Failed to fetch event detail at {Sequence} for {TenantId}/{Domain}/{AggregateId}.",
            sequenceNumber, tenantId, domain, aggregateId);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "Failed to fetch event detail.");
    }
}
```

**Rationale:** Restores the endpoint the Admin Server and MCP/CLI callers have always expected. Mirrors the actor-proxy + `GetEventsAsync` pattern used by `bisect`/`timeline`/`blame`/`step` in the same file, so no new architectural ground is broken. Honors the `EventDetail` record's required-field validation (non-empty `TenantId`/`Domain`/`AggregateId`/`EventTypeName`/`CorrelationId`, nullable `CausationId`/`UserId`, non-null `PayloadJson`).

### 4.2 EDIT — Distinguish permanent downstream 4xx from transient unavailability in `AdminStreamsController`

**File:** `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`

#### 4.2.a — Refactor the predicate at line 435

Replace the existing `private static bool IsServiceUnavailable(Exception ex)` body with:

```csharp
private static bool IsServiceUnavailable(Exception ex)
    => (ex is HttpRequestException http && IsTransientHttpFailure(http))
       || ex is TimeoutException
       || (ex is Grpc.Core.RpcException rpc && rpc.StatusCode is
           Grpc.Core.StatusCode.Unavailable or
           Grpc.Core.StatusCode.DeadlineExceeded or
           Grpc.Core.StatusCode.Aborted or
           Grpc.Core.StatusCode.ResourceExhausted);

private static bool IsTransientHttpFailure(HttpRequestException ex)
    => ex.StatusCode is null                          // network-level failure, no response received
       or HttpStatusCode.RequestTimeout               // 408
       or HttpStatusCode.TooManyRequests              // 429
       or HttpStatusCode.BadGateway                   // 502
       or HttpStatusCode.ServiceUnavailable           // 503
       or HttpStatusCode.GatewayTimeout;              // 504
```

Add `using System.Net;` and `using Microsoft.AspNetCore.WebUtilities;` at the top of the file if not already present.

#### 4.2.b — Add a propagation helper

Just below the predicate, add:

```csharp
/// <summary>
/// Propagates a permanent downstream HTTP failure (non-transient 4xx and 5xx)
/// as the same status code, so callers can tell "route/resource not found" apart from
/// "downstream temporarily down". Compare with <see cref="ServiceUnavailable"/>.
/// </summary>
private ObjectResult MapDownstreamHttpError(string method, HttpRequestException ex) {
    int statusCode = ex.StatusCode is { } s ? (int)s : StatusCodes.Status502BadGateway;
    logger.LogWarning(ex,
        "Admin downstream returned permanent {Status} for {Method}.",
        statusCode, method);
    return CreateProblemResult(
        statusCode,
        ReasonPhrases.GetReasonPhrase(statusCode),
        ex.Message);
}
```

#### 4.2.c — Insert one new catch arm in each of the 6 actions

Between the existing `IsServiceUnavailable` catch arm and the catch-all `UnexpectedError`, insert:

```csharp
catch (HttpRequestException ex) when (!IsTransientHttpFailure(ex)) {
    return MapDownstreamHttpError(nameof(<Action>), ex);
}
```

…with `nameof(...)` substituted per action: `GetStreamTimeline` (around line 45), `BisectAggregateState` (around line 75), `GetAggregateBlame` (around line 106), `GetEventStepFrame` (around line 138), `GetEventDetail` (around line 171), `SandboxCommand` (around line 208). Six identical insertions, each producing a per-action handler shape:

```csharp
try { ... }
catch (ArgumentException ex) {
    return CreateProblemResult(StatusCodes.Status400BadRequest, "Bad Request", ex.Message);
}
catch (Exception ex) when (IsServiceUnavailable(ex)) {                  // genuinely transient
    return ServiceUnavailable(nameof(<Action>), ex);
}
catch (HttpRequestException ex) when (!IsTransientHttpFailure(ex)) {    // NEW — permanent downstream
    return MapDownstreamHttpError(nameof(<Action>), ex);
}
catch (Exception ex) when (ex is not OperationCanceledException) {
    return UnexpectedError(nameof(<Action>), ex);
}
```

**Rationale:** Closes the 04-19 memo's deferred-follow-up locally. Discriminates "downstream said no permanently" from "downstream is genuinely down". UI/MCP/CLI callers can now distinguish the two — they could not before. Behavior changes only along error paths that today are objectively wrong; no happy-path change.

### 4.3 NEW & EDIT — Tier 1 unit tests

#### 4.3.a NEW — `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerEventDetailTests.cs`

Mirrors the structure of `AdminStreamQueryControllerTimelineTests.cs` (same `BuildEnvelope`, same `CreateController`, xUnit + Shouldly + NSubstitute). Seven cases:

| # | Test | Expected |
|---|---|---|
| 1 | `EventDetail_HappyPath_ReturnsProjectedRecord` — actor returns envelopes for sequences 1, 2, 3; request `sequenceNumber: 2`; payload bytes contain `{"counter":7}`. | 200 `OkObjectResult`. `EventDetail.SequenceNumber == 2`, `EventTypeName`, `Timestamp`, `CorrelationId` projected from envelope; `PayloadJson == "{\"counter\":7}"`. |
| 2 | `EventDetail_SequenceNotInStream_Returns404` — actor returns sequences 1–3; request `sequenceNumber: 99`. | 404 `ObjectResult` with `ProblemDetails.Title == "Not Found"`, detail mentioning `99`. |
| 3 | `EventDetail_EmptyStream_Returns404` — actor returns `[]`; request `sequenceNumber: 1`. | 404 `ObjectResult`. |
| 4 | `EventDetail_BadSequenceNumber_Returns400` — `sequenceNumber: 0`, `-1`. | 400 `ObjectResult` with `Title == "Bad Request"`. |
| 5 | `EventDetail_EmptyUserId_ProjectsAsNull` — envelope `UserId = ""`. | `EventDetail.UserId is null`. |
| 6 | `EventDetail_EmptyCausationId_ProjectsAsNull` — envelope `CausationId = ""`. | `EventDetail.CausationId is null`. |
| 7 | `EventDetail_PayloadJsonRoundTrip` — envelope `Payload = "{\"counter\":42}"u8.ToArray()`. | `EventDetail.PayloadJson == "{\"counter\":42}"`. |

#### 4.3.b EDIT — `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsControllerTests.cs`

Append four new cases:

| # | Test | Expected |
|---|---|---|
| 1 | `GetEventDetail_DownstreamReturns404_PropagatesAs404` — `streamQueryService.GetEventDetailAsync(...)` throws `new HttpRequestException("not found", inner: null, statusCode: HttpStatusCode.NotFound)`. | 404 `ObjectResult` with `ProblemDetails.Title == "Not Found"`. **NOT** 503. |
| 2 | `GetEventDetail_DownstreamReturns400_PropagatesAs400` — same shape with `HttpStatusCode.BadRequest`. | 400 `ObjectResult`. |
| 3 | `GetEventDetail_DownstreamReturns503_StaysAs503` — `HttpStatusCode.ServiceUnavailable`. | 503 `ObjectResult` (still classified as transient by the refined predicate). |
| 4 | `GetEventDetail_NoStatusCode_StaysAs503` — bare `HttpRequestException("unreachable")` (StatusCode null). | 503 `ObjectResult`. |

**Rationale:** Tests 4.3.a #1, #7 directly prevent regression of the contract `DaprStreamQueryService.GetEventDetailAsync` consumes. Tests #2, #3 protect 404 semantics that 4.2 makes meaningful end-to-end. Test #4 guards against off-by-one. Tests #5, #6 protect `EventDetail`'s non-empty-string invariants. Tests 4.3.b #1, #2 directly exercise the new propagation arm; would have caught today's defect end-to-end. Tests #3, #4 pin existing 503 behavior so the refined predicate doesn't silently regress transient-failure handling.

### 4.4 EDIT — `epics.md` follow-up note under Story 15.12

**File:** `_bmad-output/planning-artifacts/epics.md`

**Insertion point:** Append immediately after the existing 2026-04-19 follow-up paragraph at line 605, before the Epic 16 heading at line 607.

```markdown
**2026-05-05 follow-up (Sprint Change Proposal):** Same defect class re-surfaced for the per-event detail panel — Story 15.12 also depends on a `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber}` endpoint that was never registered on the EventStore service. Click-through from the Events grid to a row's detail view returned 404 upstream, which `AdminStreamsController.IsServiceUnavailable` mistranslated to a misleading 503 ServiceUnavailable. Story 15.12b adds the missing route and refines the predicate locally in `AdminStreamsController`; the same refinement is left as a `TODO(15.12c-deferred)` comment above `IsServiceUnavailable` in the seven sibling admin controllers, to be promoted to a story only if the symptom is ever observed on one of them. See `sprint-change-proposal-2026-05-05-event-detail-endpoint-missing.md`. The 2026-04-19 timeline fix is unaffected and remains correct.
```

**Rationale:** Leaves a chronological breadcrumb so future readers can trace the recurrence pattern (timeline 2026-04-19 → event-detail 2026-05-05). Names Story 15.12b explicitly so it links to the sprint-status entry. Records the deferred 7-controller cleanup in code (`TODO(15.12c-deferred)`) rather than in another invisible memo appendix.

### 4.5 EDIT — `sprint-status.yaml` add Story 15.12b

**File:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

**Insertion point:** Immediately after line 218 (`15-12a-implement-missing-timeline-endpoint: done`).

```yaml
  15-12-events-page-cross-stream-browser: done
  15-12a-implement-missing-timeline-endpoint: done
  15-12b-implement-missing-event-detail-endpoint: backlog
  15-13-stream-activity-tracker-writer: done
```

**Rationale:** Single new entry — Story 15.12b. Status = `backlog` (will move to `in-progress` when picked up by the developer, then `review` → `done`). Epic 15 stays `done` (this is a post-completion defect entry, consistent with the 04-19 precedent). No 15.12c entry per the team's Option C decision: the deferred 7-controller cleanup is tracked via `TODO(15.12c-deferred)` comments instead.

### 4.6 EDIT — `TODO(15.12c-deferred)` in seven sibling admin controllers

**Files (7):**
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs` (line 240)
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminConsistencyController.cs` (line 216)
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDaprController.cs` (line 183)
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDeadLettersController.cs` (line 198)
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminHealthController.cs` (line 117)
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminProjectionsController.cs` (line 246)
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs` (line 300)

**Insertion (identical in each):** Three lines immediately above `private static bool IsServiceUnavailable(Exception ex)`:

```csharp
// TODO(15.12c-deferred): Generalize permanent-vs-transient HTTP error discrimination here
// (apply the same IsTransientHttpFailure + MapDownstreamHttpError pattern from
// AdminStreamsController). See sprint-change-proposal-2026-05-05-event-detail-endpoint-missing.md.
private static bool IsServiceUnavailable(Exception ex)
```

**Rationale:** Makes the deferred work greppable from code (`grep -rn "TODO(15.12c"`). Honors CLAUDE.md's comment guidance ("only add a comment when the WHY is non-obvious: a hidden constraint, a subtle invariant, a workaround for a specific bug, behavior that would surprise a reader") — the comment captures historical context that would otherwise sit in a memo appendix and be forgotten, exactly as the 04-19 deferral was. Zero behavior change.

---

## Section 5: Implementation Handoff

**Scope:** Minor — direct implementation by the dev team.
**Story ID:** `15-12b-implement-missing-event-detail-endpoint`
**Dependencies:** None. Entirely additive (new route) plus localized error-path refinement (no happy-path change).

**Verification checklist:**

| # | Check | Status |
|---|-------|--------|
| 1 | `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 warnings, 0 errors | Pending developer |
| 2 | `dotnet test tests/Hexalith.EventStore.Server.Tests/` — Tier 1+2 including new event-detail tests green | Pending developer |
| 3 | `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/` — Tier 1 including new propagation tests green | Pending developer |
| 4 | Rebuild `eventstore` and `eventstore-admin` resources in Aspire (live host via dashboard "Rebuild" command) | Pending developer |
| 5 | With Keycloak admin-user JWT (or via Admin UI authenticated session): `GET http://localhost:8090/api/v1/admin/streams/system/tenants/global-administrators/events/1` returns 200 with a complete `EventDetail` (matches the timeline's `sequenceNumber: 1` entry, `PayloadJson` populated). | Pending developer |
| 6 | Same probe at `events/0` and `events/99` returns 404 (not 503) with `ProblemDetails.Title == "Not Found"`. | Pending developer |
| 7 | Reload `https://localhost:8093/...` — clicking an event row in the Events page populates the Event Detail panel with metadata + payload JSON. No "Service unavailable" banner. | Pending developer |
| 8 | `grep -rn "TODO(15.12c-deferred)" src/Hexalith.EventStore.Admin.Server` returns exactly 7 hits, one per sibling controller. | Pending developer |
| 9 | Re-run the 2026-04-19 timeline-endpoint-missing checks — confirm they remain green (no regression). | Pending developer |
| 10 | Move `15-12b-implement-missing-event-detail-endpoint` from `backlog` → `in-progress` at start, → `review` after PR opened, → `done` after merge. | Pending developer |

**Deliverables:**

1. New action `GetEventDetailAsync` in `AdminStreamQueryController.cs`.
2. Refactored predicate + new helpers + 6 catch arms in `AdminStreamsController.cs`.
3. New test file `AdminStreamQueryControllerEventDetailTests.cs` (7 cases).
4. Augmented `AdminStreamsControllerTests.cs` (4 new cases).
5. `TODO(15.12c-deferred)` comment in 7 sibling admin controllers.
6. Updated `epics.md` and `sprint-status.yaml`.

**Follow-ups (separate stories, NOT in this patch):**

- **Story 15.12c (deferred, tracked via code comments)** — generalize the predicate refinement across the seven sibling admin controllers. Triggered if and only if any of those surfaces shows the misleading-503 symptom in real use. `grep TODO\(15.12c-deferred` resurfaces the recipe.
- **Tier 3 end-to-end Playwright test** — when the suite is wired up, add a test that drives the Events page, clicks a row, and asserts the Event Detail panel is populated (not the error banner). Prevents this whole defect class on the same surface.
- **Audit other Admin UI features that also use service-invocation endpoints** — opportunistic check whether any other Admin UI surface depends on a non-existent `eventstore` route. The pattern is "Admin Server has a method, EventStore controller doesn't"; one disciplined `grep` over `DaprStreamQueryService`/`DaprAdminApiClient` callers vs. registered EventStore routes would catch any remaining gaps.

---

## Appendix: Why the 2026-04-19 deferral re-bit us, and what we changed in response

The 2026-04-19 timeline memo's appendix correctly identified the misleading-503 mapping as a follow-up. But that follow-up only lived in the appendix — it had no story ID, no `sprint-status.yaml` entry, no code comment. From any standard sprint-planning surface (`grep` of `sprint-status.yaml`, "what's open under Epic 15", retrospective queries), the deferral was invisible. So when the second route gap (`events/{seq}`) appeared today on the same controller, the same predicate-misclassification produced the same misleading symptom, requiring approximately one full debugging session to localize.

Three changes prevent the same re-mistake on this proposal's deferral:

1. **The local fix ships in this patch.** The predicate in `AdminStreamsController` is no longer a future TODO — it is in scope, tested, and merged. We are not deferring the controller that has actually shown the symptom *twice*.

2. **The remaining (sibling-controller) deferral is recorded in code, not in a memo appendix.** `TODO(15.12c-deferred)` comments are greppable, reviewable, visible in IDE warnings (with `dotnet build /p:WarningLevel=...` if extended), and survive any documentation reorganization. They cannot rot the way a memo appendix can.

3. **The recurrence pattern is now named in the epic note.** Future readers of `epics.md` see *two* dated follow-ups under Story 15.12 (2026-04-19, 2026-05-05), naming the same defect class. The third occurrence — if it happens — is more likely to trigger a structural fix (Story 15.12c, or a base-controller refactor) instead of a third local patch.

**Lesson generalized.** When deferring work from a sprint-change proposal, the deferral artifact must be *queryable from at least one standard project surface* (sprint-status YAML, story tracker, or grep-able code comment). Memo-appendix-only deferrals are equivalent to no tracking. This is a project-rule candidate for the next retrospective.
