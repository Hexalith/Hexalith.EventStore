# Sprint Change Proposal — Server-Managed Projection Builder

**Date:** 2026-03-15
**Triggered by:** Epic 8 (Query Pipeline & Real-Time Updates) — missing ProjectionActor implementation
**Scope:** Minor — Direct implementation by development team

---

## Section 1: Issue Summary

The query pipeline infrastructure (Stories 8.1-8.7) is fully implemented — routing, ETag caching, SignalR notifications, Blazor UI patterns all work. However, **no concrete `ProjectionActor` is registered** and **no mechanism exists to build projection state from events**.

The Blazor UI counter always shows 0 after incrementing because:
1. `QueryRouter` tries to invoke `ProjectionActor` — actor type not registered
2. DAPR throws "actor type not registered" → caught as `NotFound`
3. `SubmitQueryHandler` throws `QueryNotFoundException` → HTTP 404
4. `CounterQueryService` interprets 404 as "no projection yet" → returns `Count = 0`

The `LoggingBehavior` logs every occurrence at Error level, flooding logs every ~5 seconds as the Blazor UI polls.

**Evidence:**
- Runtime: `QueryNotFoundException` repeating every 5s in commandapi logs
- Code: `ServiceCollectionExtensions.cs` only registers `AggregateActor` + `ETagActor` — no `ProjectionActor`
- No projection builder code exists anywhere in the codebase

---

## Section 2: Impact Analysis

### Epic Impact
- **Epic 8** — directly impacted. Stories 8.1-8.7 are implemented but the pipeline is non-functional without a projection builder. Three new stories (8.9, 8.10, 8.11) added.
- **Epics 1-7** — no impact.

### Artifact Conflicts
- **PRD** — no conflict. FR50-FR64 assume projections exist but don't specify how they're built. This is an implementation gap, not a requirements gap.
- **Architecture** — needs a new section describing Mode B projection building (event delivery to domain services, `IProjectionWriteActor`, `AggregateActor.GetEventsAsync`, `/project` convention).
- **UI/UX** — no conflict. Existing Blazor UI patterns (Stories 8.6-8.8) will work once projections produce state.
- **DAPR components** — no changes needed.
- **CI/CD** — no changes needed.

### Technical Impact
- **Server project**: 6 new files, 2 modified files
- **Contracts project**: 3 new files
- **Sample project**: 1 new file, 1 modified file
- All changes are additive — no existing code broken

---

## Section 3: Recommended Approach

**Selected: Option 1 — Direct Adjustment**

Add 3 new stories to Epic 8. Spec and implementation plan already written and reviewed.

**Rationale:**
- Low risk — builds on existing infrastructure with well-defined interfaces
- Medium effort — 9 implementation tasks
- No rollback or scope reduction needed
- The query pipeline is architecturally sound; it just needs a projection state source

**Alternatives considered:**
- Rollback (Option 2): Not viable — nothing to roll back, existing infrastructure is correct
- MVP Review (Option 3): Not viable — query pipeline is core to the Blazor UI sample

---

## Section 4: Detailed Change Proposals

### Epic 8: New Stories

#### Story 8.9 — Server-Managed Projection Builder (Mode B)

As a domain service developer, I want EventStore to deliver new events to my /project endpoint and cache the returned projection state, so that query actors can serve real projection data without my service managing pub/sub subscriptions.

**Acceptance Criteria:**

- Given events are persisted by AggregateActor, when RefreshIntervalMs = 0 (default), then a fire-and-forget background task reads new events via `AggregateActor.GetEventsAsync`, maps them to `ProjectionEventDto[]`, sends them to the domain service `/project` endpoint via DAPR service invocation, stores the returned state in `EventReplayProjectionActor`, and regenerates ETag + broadcasts SignalR notification.
- Given a domain service exposes a `/project` endpoint, when it receives a `ProjectionRequest`, then it applies events to its own projection state and returns `ProjectionResponse { ProjectionType, State (JSON) }`.
- Given `EventReplayProjectionActor` is registered as `"ProjectionActor"`, when `QueryRouter` routes a query, then the actor serves persisted projection state with `CachingProjectionActor` ETag caching on top.
- Given projection update fails (domain service unavailable, error), when the failure is logged, then the projection stays at last known state (eventual consistency) and the next trigger retries.

#### Story 8.10 — Projection Contract DTOs and AggregateActor Event Reading

As a platform developer, I want wire-format DTOs for the /project endpoint and a read-only method on AggregateActor to fetch events, so that the projection builder can deliver events to domain services without coupling to DAPR internal key formats.

**Acceptance Criteria:**

- Given the Contracts project, when `ProjectionEventDto` is defined, then it contains: EventTypeName, Payload, SerializationFormat, SequenceNumber, Timestamp, CorrelationId — and excludes Server-internal fields (CausationId, UserId, DomainServiceVersion, Extensions).
- Given the Contracts project, when `ProjectionRequest` is defined, then it contains: TenantId, Domain, AggregateId, ProjectionEventDto[] — with per-aggregate granularity.
- Given the Contracts project, when `ProjectionResponse` is defined, then it contains: ProjectionType, State (JsonElement) — State is opaque, CommandApi never interprets it.
- Given `IAggregateActor`, when `GetEventsAsync(long fromSequence)` is called, then it returns `EventEnvelope[]` for events after fromSequence, encapsulates DAPR actor state key format internally, and returns empty array for new aggregates.

#### Story 8.11 — Counter Sample /project Endpoint

As a developer evaluating EventStore, I want the counter sample domain service to expose a /project endpoint, so that I have a working reference for how domain services build projection state from events.

**Acceptance Criteria:**

- Given the Sample domain service, when a POST `/project` request arrives with `ProjectionRequest`, then `CounterProjectionHandler` applies events to in-memory state and returns `ProjectionResponse { "counter", { "count": N } }`.
- Given `CounterProjectionHandler` receives increment/decrement/reset events, when it applies them, then Count is updated correctly.
- Given the Blazor UI increments the counter, when the full pipeline executes (command → events → /project → ProjectionActor → query), then the counter value card displays the correct count and no `QueryNotFoundException` errors appear in commandapi logs.

### Architecture Document

**Section to add:** "Mode B: Server-Managed Projection Building" under the Query Pipeline section.

Content: describes the event delivery flow (AggregateActor → ProjectionUpdateOrchestrator → domain service /project → EventReplayProjectionActor), the `/project` convention, the `ProjectionEventDto` wire format, RefreshIntervalMs configuration, and the fire-and-forget immediate trigger.

---

## Section 5: Implementation Handoff

**Change scope:** Minor — Direct implementation by development team.

**Deliverables:**
- Implementation plan: `docs/superpowers/plans/2026-03-15-server-managed-projection-builder.md` (already written, 9 tasks)
- Design spec: `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md` (already written, reviewed)

**Deferred to follow-up:**
- `ProjectionCheckpointTracker` — incremental event delivery (currently replays all events each trigger)
- `ProjectionPollerService` — background polling for RefreshIntervalMs > 0

**Success criteria:**
1. `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 warnings, 0 errors
2. All Tier 1 + Tier 2 tests pass
3. Aspire smoke test: increment counter in Blazor UI → counter displays correct value
4. No `QueryNotFoundException` errors in commandapi logs after increment
