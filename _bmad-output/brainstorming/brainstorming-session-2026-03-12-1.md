---
stepsCompleted: [1, 2, 3, 4]
inputDocuments: []
session_topic: 'Projection refresh notification — ETag actor pattern for cache invalidation inside EventStore'
session_goals: 'Design projection lifecycle management inside Hexalith.EventStore: ETag tracking, page-cache staleness, client notification (SignalR), transparent to downstream microservices'
selected_approach: 'AI-Recommended Techniques'
techniques_used: ['First Principles Thinking', 'Morphological Analysis', 'Chaos Engineering', 'Reverse Brainstorming', 'Role Playing', 'SCAMPER', 'First Principles Rebuild (scoped)', 'Chaos Engineering (scoped)', 'Reverse Brainstorming (scoped)', 'Role Playing (scoped)']
ideas_generated: [77]
context_file: ''
session_active: false
workflow_completed: true
session_continued: true
continuation_date: '2026-03-13'
---

# Brainstorming Session Results

**Facilitator:** Jerome
**Date:** 2026-03-12

## Session Overview

**Topic:** Projection refresh notification — ETag actor pattern for cache invalidation inside EventStore
**Goals:** Design a projection lifecycle management system inside Hexalith.EventStore that handles ETag tracking per projection, page-cache staleness detection, client notification (SignalR), all transparent to the downstream microservice.

### Context Guidance

_DAPR-native event sourcing server. Actors for state, pub/sub for events, multi-tenant. The microservice that owns the projection should have minimal code — EventStore owns the full invalidation + notification + caching pipeline._

### Session Setup

_Paginated grid scenario: 1M+ orders, SQL projection, page actors as cache, ETag actor as staleness detector. Core question: how does EventStore orchestrate this transparently?_

## Technique Selection

**Approach:** AI-Recommended Techniques
**Analysis Context:** Projection refresh notification with focus on transparent lifecycle management inside EventStore

**Recommended Techniques:**

- **First Principles Thinking:** Strip assumptions about projection invalidation to find irreducible truths in a DAPR actor/event-sourced context
- **Morphological Analysis:** Systematically decompose the problem into parameters (notification mechanism, granularity, transport, actor topology, consistency model) and explore all combinations
- **Chaos Engineering:** Stress-test top candidate designs against actor failures, race conditions, split-brain, thundering herd scenarios

**AI Rationale:** Complex distributed systems architecture problem requiring deep fundamentals, exhaustive parameter exploration, then adversarial stress-testing to find robust designs.

## Technique Execution Results

### Phase 1: First Principles Thinking

**Interactive Focus:** Strip away all assumptions about projection cache invalidation and rebuild from fundamental truths.

**First Principles Established:**

| # | Principle | Decision |
|---|-----------|----------|
| 1 | Binary staleness | Page actor needs only "stale? yes/no" — no event details, no diff, no changed row IDs |
| 2 | Projection is source of truth | Only the projection knows if data actually changed. An event may arrive but not alter the projection. |
| 3 | Dual notification | Event-based (pub/sub) or direct API call — developer chooses |
| 4 | Two granularity levels | List projections = type only. Detail projections = type + entity ID. |
| 5 | Dual strategy offered | EventStore supports both pub/sub and direct call |
| 6 | Pull between actors | DAPR virtual actor model makes push wasteful — can't target only active actors |
| 7 | Push to clients | SignalR for browser/UI notification |
| 8 | ETag = GUID base64-22 | Fresh unique value on each change. No ordering, no clock, no collision. |
| 9 | Per projection per tenant | Actor ID = `{ProjectionType}-{TenantId}` |
| 10 | Coarse invalidation | All filters invalidated on any change — simplicity over precision |
| 11 | ETag actor as dual gateway | One actor, one event: (1) regenerate GUID, (2) SignalR broadcast to clients |

**Key Breakthrough:** The ETag actor isn't just a passive cache tag — it's the single orchestration point for both actor-side staleness and client-side refresh. The microservice is completely unaware of SignalR.

**Data Flow:**

```
Microservice                    EventStore
    |                               |
    |-- "ProjectionChanged" ------->|
    |                          ETag Actor:
    |                            1. New GUID
    |                            2. SignalR -> clients
    |                               |
    |                          Page Actor (on next request):
    |                            "My ETag != current? -> re-query"
```

### Phase 2: Morphological Analysis

**Interactive Focus:** Systematically decompose remaining design parameters and select optimal configuration for each.

**Morphological Matrix — Decisions:**

| # | Parameter | Decision | Rationale |
|---|-----------|----------|-----------|
| 1 | ETag actor scope | Uniform: `{ProjectionType}-{TenantId}` | Same actor type for list and detail projections. Projection type encodes granularity. |
| 2 | SignalR hub location | Inside EventStore server | Simplest, zero additional infrastructure. EventStore holds WebSocket connections. |
| 3 | Client subscription model | SignalR groups = ETag actor IDs | One naming convention everywhere. Client joins group, ETag actor broadcasts to group. |
| 4 | SignalR message payload | Signal only — "changed" | No ETag, no metadata. Client knows which projection from its group. Minimal. |
| 5 | Page actor idle timeout | Default DAPR (60 min) | No custom lifecycle. DAPR already solves this. |
| 6 | Re-query strategy | Full page re-query | No diffing, no patching, no incremental API. Stateless refresh. |
| 7 | Client refresh behavior | Developer chooses | EventStore delivers signal + provides hooks. UI behavior is developer's concern. |
| 8 | Consistency guarantee | At-least-once, naturally idempotent | DAPR pub/sub with retry. Duplicate GUID regen is harmless. |
| 9 | API surface | NuGet helper wrapping both strategies | `eventStore.NotifyProjectionChanged(type, tenantId)` — config chooses pub/sub or direct call underneath. |

### Phase 3: Chaos Engineering

**Interactive Focus:** Deliberately attack the design with 10 failure scenarios. Zero design changes required.

| # | Scenario | Result | Reasoning |
|---|----------|--------|-----------|
| 1 | Thundering herd (50 page actors, 10 updates/sec) | **Accepted** | Database's responsibility. No debounce. |
| 2 | ETag actor state loss | **Safe** | New GUID = all page actors refresh. Never stale data. |
| 3 | Page actor reactivation after sleep | **Safe** | No state to lose. First request = fresh query. |
| 4 | SignalR multi-instance | **Solved** | DAPR pub/sub as SignalR backplane. Zero new dependencies. |
| 5 | Tenant leakage via SignalR groups | **Accepted** | Signal-only, no data. Timing side-channel is acceptable risk. |
| 6 | Stale ETag race condition | **Accepted** | Eventual consistency. Next request catches up. SignalR triggers refresh. |
| 7 | Orphaned page actors | **Non-issue** | Pull-based + no state + DAPR lifecycle = zero overhead. |
| 8 | Projection lag | **Accepted** | EventStore is "as fresh as projection tells me." Projection's responsibility. |
| 9 | ETag actor hot spot | **Non-issue** | Sub-millisecond ops (generate GUID, return string). Single-threaded is fine. |
| 10 | Split brain / actor failover | **Safe** | DAPR guarantees single actor instance. Failover = new GUID = safe refresh. |

**Resilience Property:** Every failure mode results in "unnecessary refresh," never "stale data served." The GUID ETag design makes failure safe by construction.

## Idea Organization and Prioritization

### Thematic Organization

**Theme 1: Core Invalidation Model**
- Binary staleness signal (yes/no only)
- Projection is source of truth (not event stream)
- ETag = GUID base64-22, regenerated on every change
- Pull-based between actors (DAPR virtual actor constraint)
- Full page re-query on staleness
- Coarse invalidation — all filters invalidated per projection+tenant

**Theme 2: Actor Topology & Lifecycle**
- Uniform ETag actor: `{ProjectionType}-{TenantId}`
- ETag actor is dual gateway: GUID update + SignalR broadcast
- Page actor is pure in-memory cache — no state store
- Default DAPR idle timeout — no custom lifecycle
- DAPR virtual actor model eliminates push, orphan, and split-brain concerns

**Theme 3: Client Notification Pipeline**
- SignalR hub hosted inside EventStore server
- SignalR groups = ETag actor IDs
- Signal-only payload ("changed")
- DAPR pub/sub as SignalR backplane across instances
- Client refresh behavior is developer's choice

**Theme 4: Microservice Integration Surface**
- NuGet helper: `eventStore.NotifyProjectionChanged(type, tenantId, entityId?)`
- Wraps both DAPR pub/sub and direct service invocation (config-driven)
- Two granularity levels: list (type only) vs. detail (type + entity ID)

**Theme 5: Resilience Properties**
- State loss = safe invalidation (new GUID = full refresh)
- Eventual consistency — no locking, no distributed transactions
- At-least-once delivery, naturally idempotent
- No debounce — trust the database
- DAPR actor placement handles failover

### Breakthrough Concepts

- **GUID ETag makes failure safe by construction** — every failure mode results in "unnecessary refresh," never "stale data served"
- **Page actor as disposable in-memory cache** — zero state store I/O, DAPR handles lifecycle entirely
- **DAPR pub/sub as SignalR backplane** — zero additional infrastructure beyond what DAPR already provides

### Prioritization Results

**Priority 1 — ETag Actor + Notification API (Foundation)**
1. Define `ProjectionChanged` event contract in `Hexalith.EventStore.Contracts`
2. Implement ETag actor (`{ProjectionType}-{TenantId}`) — GUID generation, pub/sub broadcast
3. NuGet helper method: `NotifyProjectionChanged(projectionType, tenantId, entityId?)`
4. DAPR pub/sub subscription for inbound projection-changed events

**Priority 2 — Page Cache Actor (Consumer)**
1. Page actor — in-memory only, no state store
2. ETag comparison on each request
3. Full re-query on staleness, return cached on match
4. Wire into existing query pipeline

**Priority 3 — SignalR Client Notification (Push to Browser)**
1. SignalR hub inside EventStore server
2. Group management: clients join `{ProjectionType}-{TenantId}`
3. DAPR pub/sub subscription on all EventStore instances -> broadcast to local SignalR clients
4. Thin client helper (Blazor/JS) with hooks for developer-defined refresh behavior

## Session Summary and Insights

**Key Achievements:**
- 11 first principles establishing the irreducible truths of projection invalidation in DAPR
- 9 morphological decisions covering every design axis
- 10 chaos scenarios validating the design with zero changes required
- Complete architecture from microservice notification to browser refresh

**Design Philosophy:**
- Minimal code in the microservice (one line: `NotifyProjectionChanged`)
- EventStore owns the full pipeline: ETag tracking, actor caching, SignalR notification
- Safe by construction: every failure mode = refresh, never stale data
- Leverage DAPR: actors, pub/sub, lifecycle — no reinvented wheels

**Session Reflections:**
The First Principles phase was decisive — establishing that DAPR's virtual actor model forces pull-based staleness checking eliminated an entire design axis early. The Morphological Analysis systematically resolved every remaining parameter. Chaos Engineering confirmed the design's resilience without requiring any modifications, validating that the simplicity-first approach (GUID ETags, no state store, coarse invalidation) produces a naturally robust system.

---

## Extension Session

**Date:** 2026-03-12
**Purpose:** Stress-test the human side of the design — developer experience, API footguns, and systematic challenge of each decision.

### Extension Techniques

- **Reverse Brainstorming:** How could a developer break, misuse, or be confused by this system?
- **Role Playing:** Junior dev, ops engineer, Blazor dev, senior architect personas
- **SCAMPER:** Substitute, Combine, Adapt, Modify, Put to other use, Eliminate, Reverse applied to each design element

### Architecture Clarifications Surfaced During Extension

**Query Actor Routing Model (3 tiers):**

```
Incoming query message
    ├── EntityId in metadata
    │   → {QueryType}-{TenantId}-{EntityId}       (one actor per entity)
    │
    ├── No EntityId + non-empty payload
    │   → {QueryType}-{TenantId}-{Checksum}        (bucketed by params)
    │
    └── No EntityId + empty payload
        → {QueryType}-{TenantId}                   (singleton per tenant)
```

**Mandatory Query Metadata:**

| Field | Required | Purpose |
|---|---|---|
| `Domain` | **Yes** | Routes message to correct microservice |
| `QueryType` | **Yes** | Actor type routing |
| `TenantId` | **Yes** | Tenant isolation |
| `ProjectionType` | **Yes** | Links to ETag actor for invalidation |
| `EntityId` | Optional | Per-entity cache routing |

**Design Constraints Confirmed:**
- One query reads exactly one projection (by design)
- Metadata is managed by client contract library (NuGet package with static fields)
- Query actor is payload-agnostic — byte-level comparison, never deserializes
- SignalR is strictly a UI concern — backend uses pull model through query actors
- Query actor serves as concurrency shield (DAPR single-threaded turn-based model coalesces concurrent requests)

### Extension Technique Results

#### Phase 4: Reverse Brainstorming

**Interactive Focus:** How could developers break, misuse, or be confused by the ETag/query actor system?

**Surviving Footguns:**

| # | Footgun | Severity | Description |
|---|---------|----------|-------------|
| 1 | Serialization non-determinism | Medium | Different JSON key ordering → different checksum → duplicate cache entries. One invalidates, the other stays stale. Caller's responsibility. |
| 3 | Silent serialization divergence | Medium | Same root cause as #1. Developer thinks "same query" but bytes differ. Results in support tickets, not bugs. |
| 6 | Unbounded search space → actor sprawl | Medium | Free-text search generates unique checksum per variation. Thousands of actors, thundering herd on invalidation. |
| 7 | EntityId on parameterized query | High | Metadata misuse routes all variations to single actor. Wrong cached data served. |
| 9 | Wrong ProjectionType | Low | Semantic mismatch between query and projection. Logic error, unpreventable by framework. |
| 12 | Contract library version mismatch | Medium | Client references v1, microservice ships v2 with renamed ProjectionType. Invalidation targets non-existent actor. Deployment coordination problem. |

**Footguns Killed by Design:**

| # | Footgun | Killed By |
|---|---------|-----------|
| 5 | Missing EntityId → wrong data | Empty payload → singleton actor → loudly wrong, not silently wrong |
| 8 | Missing ProjectionType | Mandatory metadata field |
| 10 | Query reads two projections | Design rule: one query = one projection |
| 11 | Junior dev discovery hell | Contract library carries all metadata as static fields |
| 12-orig | Copy-paste propagation | Contract library defines EntityId presence per query type |
| 17 | Projection refactoring leaks | Change contract package, recompile — single source of truth |

#### Phase 5: Role Playing

**Interactive Focus:** Embody personas who interact with the system — surface friction, confusion, and unmet needs.

| # | Persona | Insight |
|---|---------|---------|
| 13 | Ops Engineer (2am alert) | Notification pipeline is invisible to operations. No metric for "time since last projection change." Mitigated: DAPR observability covers actor method invocations — Grafana query on ETag actor calls provides this. |
| 14 | Ops Engineer (actor explosion) | Actor count in DAPR dashboard lacks EventStore semantic context. Mitigated: DAPR metrics sufficient — no custom EventStore metrics needed. |
| 15 | Blazor Dev (circuit reconnect) | Blazor Server circuit reconnection loses SignalR group membership. Stale UI until page refresh. Needs: client helper handling reconnect + group rejoin. |
| 16 | Blazor Dev (what now?) | "Changed" signal with no UX guidance. Every team invents their own refresh pattern. Needs: sample patterns (toast, reload, selective refresh). |

#### Phase 6: SCAMPER

**Interactive Focus:** Systematically challenge each design decision through seven lenses.

| # | Lens | Idea | Outcome |
|---|------|------|---------|
| 18 | **S — Substitute** | Replace int checksum with truncated SHA256 base64url (8-12 chars) | **Accepted** — human-readable actor IDs in dashboards, negligible compute cost |
| 19 | **S — Substitute** | Replace GUID ETag with monotonic counter | **Rejected** — adds ordering info but requires state store persistence, breaks stateless principle |
| 20 | **C — Combine** | Merge query actor + ETag actor | **Rejected** — can't enumerate DAPR virtual actors, pull model requires separation |
| 21 | **A — Adapt** | SignalR signal carries new ETag value | **Rejected** — signal means "you're stale," carrying the new GUID is redundant |
| 22 | **M — Modify** | Partial invalidation for parameterized queries | **Rejected** — entity queries already have surgical invalidation; collection queries are coarse by necessity |
| 23 | **P — Put to use** | ETag actor as health indicator | **Rejected** — DAPR observability already provides this via actor method invocation metrics |
| 24 | **P — Put to use** | Query actor hit/miss ratio metric | **Rejected** — DAPR method invocation metrics on re-query vs return-cached paths already distinguish hits from misses |
| 25 | **E — Eliminate** | Remove query actor for entity queries | **Rejected** — actor provides concurrency shield / request coalescing (N concurrent requests → 1 DB query) |
| 26 | **E — Eliminate** | Remove SignalR for non-browser clients | **Confirmed** — SignalR is strictly UI-only. Backend uses pull model. Clear architectural boundary. |
| 27 | **R — Reverse** | Push from ETag actor to query actors | **Rejected** (again) — DAPR virtual actors can't be enumerated or targeted |
| 28 | **R — Reverse** | **ETag pre-check at REST endpoint → 304** | **Accepted (Breakthrough)** — endpoint calls ETag actor first; if client ETag matches, return 304 without activating query actor |
| 29 | **R — Reverse** | Two-tier caching: server for cold clients, 304 for warm | **Accepted** — natural HTTP caching semantics, zero configuration |

### Extension Idea Organization

#### Theme 6: Query Routing & Metadata Integrity

Remaining risks after contract library eliminates most metadata errors:
- **Serialization discipline** for parameterized queries (caller's responsibility)
- **Version coordination** between contract library and microservice deployments
- **Actor sprawl** from high-cardinality search queries (mitigated by DAPR idle timeout)

#### Theme 7: Endpoint Optimization (Breakthrough)

**Optimized query flow with ETag pre-check:**

```
Client sends query + ETag header
  → REST endpoint calls ETag actor: "is this ETag current?"
    → YES: return 304 (query actor never activated)
    → NO:  route to query actor → fresh data + new ETag → return 200
```

- Hot path: endpoint → ETag actor → 304 (one lightweight actor call)
- Cold path: endpoint → ETag actor → query actor → microservice → 200
- Query actors become **cold-path-only resources** under steady state
- Standard HTTP caching semantics (ETag/If-None-Match)

#### Theme 8: Architectural Boundaries Confirmed

Every SCAMPER challenge either confirmed the original design or produced a genuine improvement:
- GUID ETag: stateless, failure-safe — stays
- Actor separation: load-bearing due to DAPR virtual actor model — stays
- Coarse invalidation for collections: correct granularity — stays
- Signal-only payload: sufficient information — stays
- Query actor for entity queries: concurrency shield justifies its existence — stays

#### Theme 9: Developer Experience Gaps

- Blazor Server circuit reconnect loses SignalR group → needs client helper
- "Changed" signal with no UX guidance → needs sample patterns
- Content hash for actor IDs → improves debuggability for ops and devs

### Extension Breakthrough Concepts

- **ETag pre-check at endpoint (#28)** — Transforms the hot path. Most requests never touch the query actor. Single most impactful optimization from the extension session.
- **Two-tier caching (#29)** — Server-side cache for cold clients, HTTP 304 for warm clients. Standard semantics, zero configuration, emerges naturally from #28.
- **Contract library as routing truth** — Killed 3 footguns by moving metadata into the type system. The NuGet contract package is the single source of truth for routing.

### Extension Prioritization

**Priority 1 — ETag Pre-Check at Endpoint (Foundation shift)**
1. REST query endpoint calls ETag actor before routing to query actor
2. If client ETag matches → return 304 immediately
3. If no match or no client ETag → route to query actor as today
4. Return new ETag in response headers

**Priority 2 — Content Hash for Actor IDs**
1. Replace int checksum with truncated SHA256 base64url (8-12 chars)
2. Human-readable actor IDs in DAPR dashboards and traces
3. Zero functional change to routing or collision resolution

**Priority 3 — Developer Guidance Layer**
1. Document serialization contract for parameterized queries
2. Provide Blazor SignalR helper handling circuit reconnection + group rejoin
3. Ship sample "on changed" patterns (toast, reload, selective refresh)

## Extension Session 2 — Self-Routing ETag (2026-03-13)

**Date:** 2026-03-13
**Purpose:** Eliminate client-side ProjectionType requirement. Make ETags self-routing so the server can resolve projection mapping without client knowledge.

### Design Challenge

**Problem:** The original design required the client to send `ProjectionType` as mandatory query metadata. This leaks server-side concerns (projection topology) into the client contract and adds developer friction.

**Goal:** Client sends only `QueryType + params + If-None-Match`. Zero projection knowledge.

### Phase 7: First Principles Rebuild (scoped)

**Interactive Focus:** Strip the ETag flow and rebuild without the ProjectionType assumption on the client side.

**Approaches Evaluated:**

| # | Approach | Mechanism | Outcome |
|---|---|---|---|
| A | Server-side resolution | Contract library registers QueryType → ProjectionType at startup | Explored but superseded |
| B | Self-routing ETag | ETag encodes projection type opaquely; server decodes on If-None-Match | **Selected — combines with runtime discovery** |
| C | Query-scoped ETags | Each query actor owns its own ETag | Rejected — too many invalidation targets |

**Key Insight:** The microservice already knows which projection backs a query. Instead of duplicating that knowledge at compile time (attributes, conventions), let the microservice declare it at runtime in the query response. The query actor learns the mapping on first call.

**Design Decision: Self-Routing ETag with Runtime Discovery**

**ETag Format:** `{base64url(projectionType)}.{guid}`

- Dot separator — easy to split
- ProjectionType base64url-encoded — handles any characters
- GUID portion stays as-is (base64url-22 from original design)
- Human-readable enough for debugging
- Undecodable or missing ETag → cache miss → fresh query (safe by construction)

**Example:**
```
OrderListProjection → T3JkZXJMaXN0UHJvamVjdGlvbg.k7Hs9fQ2mXpL1vN3bR8wYa
                       ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ ^^^^^^^^^^^^^^^^^^^^^^
                       base64url(projectionType)        guid (22 chars)
```

**Revised Data Flow:**

```
First call (cold — no If-None-Match):
  Client → endpoint
    → query actor (no cache, no mapping)
    → forwards to microservice
    → microservice returns { data, projectionType, etag }
    → query actor caches: data + projectionType mapping
    → endpoint returns data + ETag: encode(projectionType, guid)

Subsequent calls (warm client — has If-None-Match):
  Client → endpoint + If-None-Match: "opaque-token"
    → endpoint decodes → extracts projectionType
    → endpoint calls ETag actor({ProjectionType}-{TenantId}) directly
      → guid matches: 304 (query actor never wakes)
      → guid differs: route to query actor → fresh data → new ETag → 200

After query actor deactivation (DAPR idle timeout):
  Client → endpoint + If-None-Match: "opaque-token"
    → endpoint decodes → ETag actor check
      → guid matches: 304 (still no query actor needed)
      → guid differs: query actor → microservice → re-learns mapping → 200
```

**Architecture Changes from Original Design:**

| Element | Original | Updated |
|---|---|---|
| Client sends ProjectionType | Mandatory metadata | **Eliminated** |
| ETag format | GUID base64-22 | `{base64url(projectionType)}.{guid}` |
| ETag pre-check | Required client-supplied ProjectionType | **Self-routing from ETag token** |
| Mapping source | Compile-time (contract library attributes) | **Runtime (microservice response)** |
| Query actor first call | Stateless | Learns ProjectionType from microservice response |
| Query actor state model | Pure in-memory, no state store | Same — deactivation = cold call to microservice |
| Mandatory query metadata | Domain, QueryType, TenantId, ProjectionType, EntityId? | Domain, QueryType, TenantId, EntityId? |

### Phase 8: Chaos Engineering (scoped)

**Interactive Focus:** Stress-test the self-routing ETag design against failure scenarios.

| # | Scenario | Result | Reasoning |
|---|---|---|---|
| 1 | Client sends garbage If-None-Match | **Safe** | Decode fails → treated as cache miss → fresh query |
| 2 | Client sends valid ETag with renamed ProjectionType (after refactor) | **Safe** | ETag actor ID no longer exists → treated as new → fresh query + new ETag |
| 3 | Query actor deactivated, client has warm ETag | **Safe** | Endpoint decodes ETag → checks ETag actor directly. If fresh → 304 without waking query actor. If stale → query actor reactivates, re-learns mapping. |
| 4 | ETag actor state loss (DAPR failover) | **Safe** | New GUID generated. Client's ETag won't match → fresh query. Same as original design. |
| 5 | Microservice changes which projection backs a query (version update) | **Safe** | Next cold call returns new projectionType → query actor updates mapping → new ETag with new projection prefix. Old client ETags fail gracefully (projection mismatch at ETag actor → treated as stale). |
| 6 | Man-in-the-middle decodes ETag, sees projection type name | **Accepted** | Projection type names are not secret. No data in ETag. Same risk profile as exposing URL paths. |
| 7 | Two query types backed by same projection | **Safe** | Both ETags encode same projectionType → same ETag actor. Invalidation works correctly for both. |
| 8 | Thundering herd after invalidation, all clients decode and hit ETag actor | **Safe** | ETag actor is sub-millisecond (compare GUID). Same as original design. |

**Resilience Verdict:** All 8 scenarios pass. The self-routing ETag inherits the original design's "failure = refresh, never stale" property. The encoding adds no new failure modes — undecodable tokens degrade to cache miss.

### Phase 9: Reverse Brainstorming (scoped)

**Interactive Focus:** How could developers break, misuse, or be confused by the self-routing ETag system?

| # | Footgun | Severity | Description | Outcome |
|---|---|---|---|---|
| 33 | Client caches ETag locally and reuses after projection rename | Low | Old ETag decodes to non-existent projection → cache miss → fresh query | **Safe by design** |
| 34 | Developer inspects ETag, decodes base64, starts depending on projection name | Medium | Coupling to an internal format. ETag is documented as opaque — but devs read base64. | **Accepted** — document as opaque, accept the risk |
| 35 | Microservice returns empty/null projectionType in response | High | Query actor has no mapping → can't build self-routing ETag. Silent degradation — caching disabled. | **Killed** — `IQueryResponse<T>` requires ProjectionType at compile time (C) |
| 36 | Microservice returns different projectionType for same query across calls | Medium | Query actor mapping flips. Clients with old ETag decode to wrong projection → ETag actor mismatch → cache miss. | **Safe** — wasteful but correct |
| 37 | Load balancer or CDN strips/rewrites ETag header | Medium | Standard HTTP problem. Dot in format could confuse weak ETag parsers. | **Mitigated** — always wrap in quotes per HTTP spec |
| 38 | Two microservices return same projectionType for different queries | Low | Shared projection — correct behavior. Invalidation of one invalidates both. | **Safe** — same as original design |

**Critical Design Decision:** Footgun #35 (missing projectionType) killed at compile time via required response contract:

```csharp
public interface IQueryResponse<TData>
{
    TData Data { get; }
    string ProjectionType { get; }  // Required — no caching without it
}
```

### Phase 10: Role Playing (scoped)

**Interactive Focus:** How do different personas experience the self-routing ETag change?

| # | Persona | Experience | Friction vs Original |
|---|---|---|---|
| 39 | **Microservice dev** | Returns `IQueryResponse<T>` with ProjectionType. One field added to response. Never thinks about ETags, actors, or SignalR. | Low — one required field, clear purpose |
| 40 | **Blazor dev** | Sends query, gets data + opaque ETag header. Passes ETag back via `If-None-Match`. Gets 304 or fresh data. Standard HTTP. | **None** — doesn't know projections exist |
| 41 | **Ops engineer (2am)** | Sees ETag actor calls in DAPR dashboard. Can decode base64 prefix in ETag to identify which projection is hot. Actor IDs still `{ProjectionType}-{TenantId}`. | **Improved** — ETag is now debuggable, can correlate HTTP headers to actor IDs |
| 42 | **Junior dev onboarding** | Reads docs: "return IQueryResponse, include ProjectionType." Doesn't need to understand ETag actors, routing, or SignalR. Cache just works. | **Improved** — one fewer metadata field to learn |

**Verdict:** Every persona's experience is equal or better than the original design.

### Extension 2 Design Decisions Summary

| # | Decision | Rationale |
|---|---|---|
| 30 | Self-routing ETag format: `{base64url(projectionType)}.{guid}` | Client sends opaque token, server decodes routing. Zero projection knowledge on client. |
| 31 | Runtime projection discovery from microservice response | No compile-time mapping needed. Self-configuring system. |
| 32 | Endpoint-level pre-check preserved | ETag token tells endpoint which ETag actor to check. Query actors stay cold. |
| 33 | Coarse invalidation kept (no entity in ETag) | Simpler, consistent, already validated by original chaos engineering. |
| 34 | ProjectionType required in response contract (`IQueryResponse<T>`) | Compile-time enforcement kills silent degradation footgun. |
| 35 | ETag always quoted in HTTP headers | Prevents weak parser confusion from dot separator. |

### Extension 2 Breakthrough Concepts

- **Self-routing ETag (#30)** — The ETag token carries its own routing information. Server decodes it to find the right ETag actor without any client metadata. Standard HTTP semantics preserved.
- **Runtime projection discovery (#31)** — The microservice declares which projection backs a query in the response. No compile-time mapping, no attributes, no conventions. Self-configuring system.
- **Preserved hot path (#32)** — Endpoint-level 304 still works. The ETag token tells the endpoint which ETag actor to check. Query actors stay cold under steady state. Best of both worlds.
- **Compile-time contract enforcement (#34)** — `IQueryResponse<T>` with required ProjectionType eliminates the only silent degradation footgun at the type system level.

### Extension 2 Prioritization

**Priority 1 — Self-Routing ETag Format**
1. Define ETag encoding/decoding utility: `{base64url(projectionType)}.{guid}`
2. Update ETag actor to generate composite ETag (not just GUID)
3. REST endpoint decodes If-None-Match → extracts projectionType → calls ETag actor
4. Fallback: undecodable ETag → treat as cache miss
5. Always quote ETag in HTTP response headers

**Priority 2 — Runtime Projection Discovery**
1. Define `IQueryResponse<T>` contract with required `ProjectionType` field
2. Microservice query endpoints return `IQueryResponse<T>`
3. Query actor stores projectionType mapping on first call
4. Query actor uses stored mapping for subsequent ETag actor lookups
5. Deactivation resets mapping (pure in-memory — re-learned on next cold call)

**Priority 3 — Remove ProjectionType from Client Contract**
1. Remove `ProjectionType` from mandatory query metadata
2. Update contract library to reflect reduced client requirements
3. Update documentation and sample code

## Combined Session Summary

**Total Ideas:** 77 across 10 techniques (30 original + 29 extension 1 + 18 extension 2)
**Techniques Used:** First Principles Thinking, Morphological Analysis, Chaos Engineering, Reverse Brainstorming, Role Playing, SCAMPER, First Principles Rebuild (scoped), Chaos Engineering (scoped), Reverse Brainstorming (scoped), Role Playing (scoped)

**Combined Design Philosophy:**
- Minimal code in the microservice (one line: `NotifyProjectionChanged`)
- EventStore owns the full pipeline: ETag tracking, actor caching, SignalR notification
- Safe by construction: every failure mode = refresh, never stale data
- Leverage DAPR: actors, pub/sub, lifecycle, observability — no reinvented wheels
- **Self-routing ETags: client sends opaque token, server decodes routing — zero projection knowledge on client**
- **Runtime projection discovery: microservice declares mapping in response, not compile-time attributes**
- HTTP-native caching: ETag pre-check at endpoint, 304 for warm clients — preserved with self-routing ETags
- SignalR is UI-only acceleration, not a core mechanism

**Extension 1 Reflections:**
The Reverse Brainstorming phase revealed that the mandatory metadata fields and contract library eliminate most critical footguns by construction. The surviving risks (serialization non-determinism, version mismatch) are deployment and documentation concerns, not architectural flaws. Role Playing surfaced that the system is correct but could guide Blazor developers more at the SignalR consumer layer. SCAMPER was decisive — systematically challenging every design decision confirmed the architecture's robustness while producing the session's biggest breakthrough: the ETag pre-check at the REST endpoint, which transforms the hot path from two actor calls to one and brings standard HTTP caching semantics (304) into an actor-based system.

**Extension 2 Reflections:**
The self-routing ETag design emerged from a simple ergonomic question ("why does the client need to know ProjectionType?") and produced an architecturally cleaner system. By encoding the projection type into the ETag token and letting the microservice declare the mapping at runtime, we eliminated compile-time coupling, removed a mandatory metadata field, and preserved the endpoint-level 304 optimization. Chaos engineering confirmed all 8 failure scenarios are safe — the encoding adds zero new failure modes. Reverse Brainstorming surfaced one critical footgun (missing ProjectionType in response) which was killed at compile time via `IQueryResponse<T>`. Role Playing confirmed every persona's experience is equal or better than the original design. The key insight: the ETag was already an opaque token to the client; making it self-routing to the server costs nothing and gains everything.
