---
stepsCompleted: [1, 2, 3, 4]
inputDocuments: []
session_topic: 'Hexalith.EventStore architecture and design — event sourcing store server'
session_goals: 'Architecture decisions, design patterns, component interactions, and system design for an event sourcing platform using .NET 10, Blazor Fluent UI 5, DAPR/Redis, inbox pattern, multi-tenant domain command processors'
selected_approach: 'ai-recommended'
techniques_used: ['First Principles Thinking', 'Morphological Analysis', 'Chaos Engineering', 'Reverse Brainstorming', 'Assumption Reversal', 'Question Storming']
ideas_generated: 95
session_active: false
workflow_completed: true
session_continued: true
continuation_date: '2026-02-11'
extension_direction: 'Stress the Decisions'
context_file: ''
---

# Brainstorming Session Results

**Facilitator:** Jerome
**Date:** 2026-02-11

## Session Overview

**Topic:** Hexalith.EventStore architecture and design — an event sourcing store server with command processing, multi-tenant domain isolation, and event persistence

**Goals:** Architecture decisions, design patterns, component interactions, and system design for this event sourcing platform

### Session Setup

- **Tech Stack:** .NET 10, Blazor Fluent UI 5, DAPR, Redis, Inbox Pattern
- **Core Components:** Command Processor, Event Store, Domain Server Registration (tenant + domain filtered), Domain Command Processors (command in → domain events out)
- **Scope:** Wide-open exploration across architecture, scalability, consistency, failure handling, and design patterns

## Technique Selection

**Approach:** AI-Recommended Techniques
**Analysis Context:** Event sourcing store server architecture with focus on design patterns, component interactions, and system resilience

**Recommended Techniques:**

- **First Principles Thinking:** Strip assumptions about event sourcing to rebuild from fundamental truths — ensures every architecture choice serves a real purpose
- **Morphological Analysis:** Systematically map all architecture parameters and explore combinations — discovers the full solution space for complex distributed systems
- **Chaos Engineering:** Stress-test architecture by deliberately imagining failure scenarios — surfaces design decisions that create resilience vs. fragility

**AI Rationale:** Complex distributed architecture with multi-tenancy, DAPR/Redis, and critical reliability requirements demands a flow from fundamental clarity → systematic exploration → battle-tested confidence.

## Technique Execution: First Principles Thinking

### Core Problem (Fundamentals 1-3)
- **#1 Deployment-Agnostic State:** The core problem is portable, scalable state across on-prem and cloud — the event log is the mechanism, not the purpose
- **#2 Append-Only Log as Universal Truth:** Simplest replication primitive — no conflict resolution, just ship the log tail
- **#3 Full History as First-Class Citizen:** Current state is derived; history IS the system — auditing, debugging, compliance come free

### Why Event Sourcing (Fundamentals 4-5)
- **#4 Testability Through Determinism:** Command → Events is a pure function — no mocking, assert on events
- **#5 Complexity Decomposition:** Small domain microservices, event log as integration backbone — domains talk to the log, not each other

### Delivery Guarantees (Fundamentals 6-8)
- **#6 Command Processor Responsibilities:** Router + tenant gateway + orchestrator + inbox — four responsibilities converging
- **#7 Inbox/Outbox Are Patterns, Not Components:** Inbox = exactly-once processing via message ID dedup; Outbox = exactly-once publishing via same-transaction write
- **#8 The Transaction Boundary is the Architecture:** Store events + mark command processed must be atomic — this is the hardest constraint

### DAPR Actor Model (Fundamentals 9-11)
- **#9 The DAPR Actor IS the Aggregate:** Single-threaded turn-based access — no concurrency conflicts, statestore IS persistence, pub/sub IS outbox
- **#10 Turn-Based Concurrency Eliminates the Hardest Problem:** Two concurrent commands on same aggregate structurally impossible
- **#11 Actor Identity = Tenant + Domain + Aggregate ID:** Multi-tenancy baked into addressing scheme

### Command Lifecycle (Fundamentals 12-15)
- **#12 Outbox as Internal Actor:** Separation of processing and publishing
- **#13 Two-Actor Pipeline:** Domain actor stores events → outbox actor publishes
- **#14 Actor Owns Full Command Lifecycle:** Pull → process → store → publish → mark done — all in one actor turn
- **#15 Pull-Based Command Consumption:** Actor controls throughput — backpressure is structural

### Domain Boundary (Fundamentals 16-21)
- **#16 EventStore is Pure Infrastructure:** Zero domain knowledge — domain-agnostic like a database
- **#17 The Registration Contract:** Domain servers register as handlers scoped by tenant + domain
- **#18 Domain Processor is a Pure Function Over the Network:** (Command, CurrentState?) → List<DomainEvent> — no side effects
- **#19 Event Store is Both Lifecycle Engine AND State Provider:** Domain service calls back to read event streams
- **#20 Domain Service Owns Its Own State Interpretation:** Each service decides how to fold events into state
- **#21 Serialization by Actor = Concurrency Eliminated:** Structurally impossible for two commands to process concurrently on same aggregate

### Registration (Fundamentals 22-28)
- **#22 Registration is Deployment-Time Configuration:** Static/declarative — no service discovery protocol
- **#23 Configuration as Single Source of Routing Truth:** Auditable, version-controlled, reviewable
- **#24 Registration is Tenant + Domain → DAPR Service Endpoint:** Minimal routing table
- **#25 Command Type Routing is the Domain's Problem:** EventStore routes by tenant+domain, domain service dispatches internally
- **#26 DAPR Service Identity = Domain Registration:** Endpoint is just a DAPR app ID
- **#27 Versioned Domain Routing:** Per-tenant progressive migration via version property
- **#28 Version is a Routing Property:** Decouples "which versions exist" from "which version this tenant uses"

### Schema Evolution (Fundamentals 29-30)
- **#29 Event Upcasting is the Domain's Responsibility:** Each version reads all prior event formats
- **#30 EventStore is Schema-Ignorant:** Stores opaque payloads with metadata — future-proof by being intentionally ignorant

### Failover (Fundamentals 31-38)
- **#31 Active-Passive with On-Prem Primary:** Cloud is warm standby
- **#32 Append-Only Log = Trivial Replication:** Send everything cloud hasn't seen
- **#33 DAPR Pub/Sub as Replication Channel:** Cloud replica is just another subscriber
- **#34 Cloud Replica is an Event Consumer:** Receives events and rebuilds its own store
- **#35 Inbox Pattern Saves You at Failover:** Unprocessed commands retried, deterministic results
- **#36 Distributed Statestore Eliminates Replication Gap:** Data already durable across locations
- **#37 Two Distinct Distribution Concerns:** Statestore = durability, pub/sub = notification
- **#38 Failover is Actor Activation, Not Data Migration:** Compute failover, not data failover

### UI Control Plane (Fundamentals 39-43)
- **#39 UI is Operations Control Plane:** Admin/monitoring + tenant management + event explorer — domain-agnostic
- **#40 Three Distinct UI Concerns:** System health, routing CRUD, event stream browsing
- **#41 Configuration is Static in Shape, Managed Through UI:** Declarative but editable
- **#42 Configuration Refresh Native to .NET and DAPR:** IOptionsMonitor, DAPR config API — no custom code
- **#43 Event Stream Explorer is a Time Machine:** Full causal history of any aggregate

### Event Metadata (Fundamentals 44-46)
- **#44 Rich Event Metadata Envelope:** Aggregate ID, tenant, domain, sequence, timestamp, correlation ID, causation ID, user identity, domain service version, event type name
- **#45 Domain Service Version on Every Event:** Built-in migration archaeology
- **#46 Correlation + Causation = Full Causal Graph:** Trace any event to its command to its user request

### Pub/Sub Subscribers (Fundamentals 47-50)
- **#47 DAPR Pub/Sub Manages All Subscriber Concerns:** EventStore publishes, DAPR handles the rest
- **#48 EventStore Has Exactly One Outbound Interface:** DAPR pub/sub topic
- **#49 Subscriber Diversity is DAPR Configuration:** Projections, sagas, replication, monitoring, integrations — all just subscribers
- **#50 EventStore Boundary is Minimal by Design:** Accept commands, orchestrate processing, store events, publish events — nothing else

### Snapshots (Fundamentals 51-58)
- **#51 Snapshot Problem is a Domain Service Problem:** EventStore provides infrastructure, domain defines content
- **#52 EventStore Provides Snapshot Infrastructure:** Opaque bytes with sequence number
- **#53 Snapshot is a Read Optimization:** Doesn't affect write path
- **#54 Snapshot Creation Flows Backward:** Domain service → EventStore
- **#55 Snapshot as a Domain Event:** Uniform data model — flows through same pipeline
- **#56 EventStore Controls WHEN, Domain Controls WHAT:** createSnapshot parameter, domain returns snapshot event
- **#57 Snapshot Event = Stream Checkpoint:** EventStore finds latest snapshot event, serves it + subsequent events
- **#58 Everything is an Event:** One primitive — events with metadata — for all data in the system

### Security (Fundamentals 59-61)
- **#59 Security is a DAPR Infrastructure Concern:** Auth, mTLS, access policies managed by DAPR
- **#60 User Identity in Command Metadata:** Business-level identity separate from transport-level identity
- **#61 Tenant Isolation is Triple-Layered:** Actor identity (structural) + DAPR policies (authorization) + command metadata (audit)

## Technique Execution: Morphological Analysis

### Parameter Decisions

| Parameter | Decision |
|---|---|
| P1: Event Stream Partitioning | Per aggregate instance |
| P2: Command Queue Structure | Per aggregate type + sessions per instance |
| P3: Snapshot Trigger | Every N events, configurable |
| P4: Pub/Sub Topic Structure | Per tenant + domain |
| P5: Actor Lifecycle | DAPR default |
| P6: Routing Config Storage | DAPR configuration store |
| P7: Event Serialization | JSON (MVP) |
| P8: Failover Trigger | DAPR/infrastructure managed |

### Combination Insights

- **#1 P1+P2 Stream & Queue Alignment:** Actor, queue session, and event stream share one identity: `tenant:domain:aggregate-id`
- **#2 P4+P7 Topic Isolation + JSON:** Any subscriber can inspect any event without schema knowledge — powerful for debugging
- **#3 P3+P1 Snapshot + Per-Aggregate Streams:** Snapshot decision is local to the actor — no global coordination needed
- **#4 Cross-Domain Subscribers:** Sagas subscribe to multiple tenant+domain topics, correlate by correlation ID, emit new commands
- **#5 All Commands Through EventStore:** Saga-generated commands flow through the same command queue — no back door, full lifecycle guarantees
- **#6 System Forms a Feedback Loop:** Commands → Events → Subscribers → Sagas → New Commands → ... with full causation tracing
- **#7 Max Causation Depth Guardrail:** Infrastructure-level circuit breaker prevents runaway saga loops via causation chain depth counter
- **#8 P6+P4 Config Drives Topology:** Routing config is single source of truth for actors, queues, topics, and service endpoints
- **#9 P7+P3 JSON + Snapshot Frequency:** JSON size means lower snapshot threshold — future optimization lever when migrating to binary
- **#10 P2+P5 Sessions + Actor Lifecycle:** DAPR manages both queue sessions and actor lifecycle — naturally synchronized

## Technique Execution: Chaos Engineering

### Resilience Test Results

| Scenario | Verdict | Mechanism |
|---|---|---|
| Redis dies mid-command | SURVIVES | Pull-based retry, deterministic replay |
| Domain service dies | SURVIVES | Stateless pure function, no trace of failure |
| Actor crashes after store, before publish | SURVIVES | Actor state machine resumes from checkpoint |
| Pub/Sub down | DEGRADES GRACEFULLY | Events safe, DAPR retry policies, subscribers catch up |
| Buggy domain service deployed | PREVENTED | DAPR health checks + instant version rollback via UI |
| Redis memory full | DEGRADES GRACEFULLY | Backpressure queues commands, snapshot-based archival to cold storage |
| Network partition on-prem to cloud | SURVIVES | AP safe with append-only single-writer, cloud catches up |
| Saga command storm (10K commands) | ACCEPTABLE | Latency degrades, correctness preserved, pull-based backpressure |
| Tenant data leak | EXTREMELY UNLIKELY | Triple-layer isolation, REST gateway trust boundary, multiplicative failure probability |
| EventStore process crash | SURVIVES | Process is stateless, DAPR holds all durable state, Kubernetes restarts |

### Chaos Insights

- **#1 Actor is a State Machine with Checkpoints:** Command lifecycle has discrete persisted states — recovery is resumption, not replay. Actor state IS the outbox.
- **#2 DAPR Retry Policies Handle Pub/Sub Recovery:** Built-in resiliency policies handle transient failures — no custom retry logic.
- **#3 DAPR Resiliency Prevents Bad Deployments:** Health checks, rolling deployments, readiness probes catch buggy services before they process commands.
- **#4 Versioned Routing Enables Instant Rollback:** Operator changes version in config via Blazor UI — no redeployment, seconds to rollback.
- **#5 Snapshot Events Enable Safe Archival:** Everything before latest snapshot can move to cold storage — snapshot frequency controls hot storage size.
- **#6 Two-Tier Storage:** Hot events in Redis, cold events in archive. Append-only means never delete, but can move.
- **#7 Append-Only Makes AP Safe:** No concurrent writes in active-passive — availability during partition is free.
- **#8 Volume Storms Degrade Latency, Never Correctness:** Pull-based actors drain queues at their own pace — the system gets slower, never wrong.
- **#9 Tenant Isolation Failure Requires Multiple Simultaneous Bugs:** Triple-layer defense means multiplicative (not additive) failure probability.
- **#10 REST Gateway is the Trust Boundary:** All auth validated once at entry — everything downstream operates in trusted zone.
- **#11 Full Entry Path:** Client → REST API (auth) → Queue (trusted) → Actor → Domain Service → Events — one security check, zero redundancy.
- **#12 EventStore Process is Disposable:** No irreplaceable state in process memory — crash, restart, scale freely.

## Idea Organization and Prioritization

### Thematic Organization

**Theme 1: Core Architecture Identity** — The EventStore is radically minimal. Every decision pushes complexity to edges (domains, DAPR, subscribers) and keeps the center thin.

**Theme 2: DAPR as the Operating System** — DAPR is not a dependency, it's the platform. Actors, pub/sub, config, security, lifecycle, retry — all delegated. The EventStore is a thin application layer.

**Theme 3: The Actor State Machine** — The actor is the heart. A checkpointed state machine guarantees exactly-once processing through lifecycle stages, not distributed transactions.

**Theme 4: Multi-Tenancy as an Addressing Scheme** — Tenant isolation isn't a feature bolted on — it's the addressing scheme. Every infrastructure primitive is tenant-scoped by default.

**Theme 5: Resilience Architecture** — No scenario produces data loss or permanent inconsistency. Self-healing under infrastructure failure, graceful degradation under load.

**Theme 6: Data Architecture** — Uniform data model. Everything is an event with metadata. Snapshots, archival, and schema evolution build on one primitive.

**Theme 7: Operational Control Plane** — Full operator visibility and control through Blazor UI. Detection → diagnosis → action without redeployment.

### Breakthrough Concepts

- **The Feedback Loop:** Commands → Events → Sagas → Commands creates a self-driving system with full causation tracing
- **Process is Disposable, Data is Eternal:** EventStore process can die freely — all state lives in DAPR
- **One Identity Rules Everything:** `tenant:domain:aggregate-id` addresses actors, queue sessions, event streams, and topics

### Prioritization Results

**Top 3 High-Impact Architecture Decisions:**

**Priority #1: One Identity Scheme — `tenant:domain:aggregate-id`**
- Foundation everything else addresses
- Eliminates mapping complexity, makes debugging trivial
- Actors, queues, streams, topics all derive from the same tuple
- **Implementation order: FIRST**

**Priority #2: Everything is an Event — Uniform Data Primitive**
- One serialization path, one storage path, one transport path
- Snapshots are events, metadata envelope is universal
- 10-field metadata: aggregate ID, tenant, domain, sequence, timestamp, correlation ID, causation ID, user identity, domain service version, event type name
- **Implementation order: SECOND**

**Priority #3: Actor State Machine with Checkpointed Lifecycle**
- Pull → process → store → publish → done with persisted state at each stage
- Self-healing, exactly-once processing, crash recovery all flow from this
- Actor state IS the outbox — no separate outbox table
- **Implementation order: THIRD**

**Implementation Sequence:** Identity → Event Primitive → Actor State Machine

### Action Plans

**Step 1 — One Identity Scheme:**
1. Define canonical identity format and parsing logic
2. Implement actor ID derivation from command metadata
3. Implement queue session ID derivation from same tuple
4. Implement event stream key derivation from same tuple
5. Implement pub/sub topic naming from `tenant:domain`

**Step 2 — Event Primitive:**
1. Define event envelope: 10-field metadata + opaque JSON payload
2. Define snapshot event as subtype — same envelope, special type marker
3. Build event stream read API: latest snapshot + subsequent events
4. Build event stream write API: append events, detect snapshot trigger
5. Build pub/sub publish path: same event envelope on the wire

**Step 3 — Actor State Machine:**
1. Define command lifecycle states: received → processing → events_stored → events_published → done
2. Design DAPR actor state schema: lifecycle stage + command data + produced events
3. Implement recovery logic: on activation, check state, resume from checkpoint
4. Build pull mechanism: actor reads from queue session, processes one command per turn

## Session Summary and Insights

### Key Achievements

- **83 ideas** generated across 3 techniques (61 fundamentals, 10 morphological insights, 12 chaos insights)
- **7 organized themes** identifying key architectural patterns
- **3 prioritized concepts** with concrete implementation sequence and action plans
- **10 chaos scenarios** tested with zero data-loss failures found
- **8 infrastructure parameters** decided with clear rationale
- **Complete architecture** from first principles through stress-tested design

### Architecture at a Glance

```
Client → REST API (auth + validation)
  → Command Queue (per aggregate type, sessions per instance)
    → DAPR Actor (tenant:domain:aggregate-id)
      → Calls external Domain Service (pure function)
        → Domain reads event stream from EventStore
        → Returns List<DomainEvent> (+ optional snapshot event)
      → Actor stores events in DAPR statestore
      → Actor publishes events to DAPR pub/sub (tenant:domain topic)
      → Actor marks command done
      → Pulls next command

Subscribers (all via DAPR pub/sub):
  - Projection builders
  - Saga/process managers → emit new commands back to EventStore
  - Cloud replica
  - Monitoring dashboard (Blazor Fluent UI 5)
  - External integrations

Configuration:
  - DAPR config store: tenant + domain + version → DAPR service endpoint
  - Managed via Blazor admin UI, native hot reload

Failover:
  - Active-passive, on-prem primary
  - Distributed statestore (data already durable)
  - Infrastructure-managed trigger
  - Failover = actor activation in cloud, not data migration
```

### Session Reflections

This session moved from "what's the problem?" to a complete, stress-tested architecture through three complementary techniques. First Principles established 61 irreducible truths. Morphological Analysis mapped 8 parameter decisions with 10 combination insights. Chaos Engineering validated resilience across 10 failure scenarios with zero data-loss vulnerabilities found. The architecture's strength comes from radical simplicity — one data primitive (events), one identity scheme (tenant:domain:aggregate-id), one platform (DAPR), and one processing model (actor state machine).

---

## Session Extension: Stress the Decisions

**Direction:** Challenge existing architecture decisions through adversarial techniques
**Date:** 2026-02-11 (continuation)

## Technique Execution: Reverse Brainstorming

### Attack Vectors Explored

**Attack Vector: The JSON Tax at Scale**
Deep analysis of JSON serialization cost across all hops: command arrival, actor state loading, domain service calls, event storage, pub/sub publishing, subscriber consumption.

**Mitigations Confirmed:**
- Snapshot truncation (default 100 events) caps replay cost at ~200KB per actor activation
- Projections read from event store stream with snapshot-aware truncation — same optimization as actors
- MongoDB future eliminates Redis memory cost concern
- DAPR statestore abstraction means backend swap is config-only, no code change

**Vulnerability Found:**
- JSON format is an APPLICATION-layer concern, not infrastructure. DAPR abstracts the store but not the payload format. Every domain service and subscriber produces/consumes JSON directly. Migration to binary (Protobuf, MessagePack) would touch every domain service.

### Reverse Brainstorming Finding

**#RB-1: Add `serialization_format` to Event Metadata Envelope**
The 10-field envelope becomes 11 fields. Default: `json/utf-8`. Future values: `protobuf`, `msgpack`, `avro`. Enables gradual per-aggregate or per-domain format migration without big-bang cutover. One field now saves a painful migration later.

**Updated Event Metadata Envelope (11 fields):**
aggregate ID, tenant, domain, sequence, timestamp, correlation ID, causation ID, user identity, domain service version, event type name, serialization format

## Technique Execution: Assumption Reversal

### Flip Results

| # | Assumption Flipped | Verdict | Finding |
|---|---|---|---|
| 1 | DAPR Actors ARE the Aggregate | **Holds** | Simplified by Workflow separation — actor is pure state + consistency |
| 2 | EventStore Has Zero Domain Knowledge | **Holds** | Metadata ≠ domain knowledge. Payload stays opaque. EventStore has a domain: event lifecycle management |
| 3 | Domain Service is a Pure Function | **Holds with design work** | Saga decomposition is the pattern for side effects. Saga subsystem needs explicit design |
| 4 | Pull-Based Command Consumption | **Replaced** | DAPR Workflows orchestrate command lifecycle. Actors hold state. Resolves wake-up, checkpointing, and sagas |
| 5 | Configuration is Static | **Holds** | Enrich with `traffic_weight` later if needed |

### Assumption Reversal Findings

**#AR-1: Saga is the Side-Effect Pattern**
All domain services stay pure: `(Command, CurrentState?) → List<DomainEvent>`. No exceptions. External side effects (payment gateways, inventory checks, KYC services) live exclusively in saga/process managers that subscribe to events and emit new commands back through EventStore.

**#AR-2: Saga Subsystem Needs Explicit Design**
Three open design questions:
- **User notification channel:** How does the UI learn the final outcome of a multi-step saga? (SignalR / pub/sub subscription / polling)
- **Compensation pattern:** Every saga step with an external side effect needs a matching undo. Framework-level or per-saga manual?
- **Saga state storage:** Saga process managers need durable state. Are sagas themselves event-sourced actors?

**#AR-3: Command Response = "Accepted," Never "Completed"**
The architecture is inherently eventually consistent from the user's perspective. Command responses mean "accepted for processing." The UI/API layer must be designed around this from day one.

**#AR-4: DAPR Workflows Replace Actor State Machine (MAJOR REVISION)**
Replace custom actor state machine with DAPR Workflow orchestration:
- **Actor** = aggregate state + consistency (simplified role)
- **Workflow** = command lifecycle orchestration + checkpointing + saga management
- Resolves: actor wake-up problem, saga state storage, crash recovery — all in one move

**Revised Command Processing Model:**
```
Command → DAPR Workflow orchestrates lifecycle:
  1. Activate/call Actor (holds aggregate state)
  2. Actor calls Domain Service (pure function)
  3. Workflow stores events (checkpoint)
  4. Workflow publishes events (checkpoint)
  5. Workflow marks done (checkpoint)

Saga needed? Workflow spawns sub-workflow.
  Sub-workflow calls external service
  Sub-workflow sends result command back
  Parent workflow resumes
```

**Impact on Original Priorities:**
- Priority #3 revised: "Actor State Machine with Checkpointed Lifecycle" → "DAPR Workflow with Actor-Backed Aggregates"
- Implementation sequence unchanged: Identity → Event Primitive → Workflow + Actor

**#AR-5: Static Configuration Holds**
No architectural change required. DAPR handles replica load-balancing, Kubernetes handles scaling. Progressive rollout expressible as `traffic_weight` field if needed.

## Technique Execution: Question Storming

### Questions Generated (66 total)

**Architecture Boundaries:**
1. Where exactly does the REST API gateway live — part of EventStore or separate deployable?
2. Who validates command schema — gateway, actor, or domain service?
3. What happens to a command targeting a tenant+domain with no registered handler?
4. How does a domain service discover EventStore's API endpoint?

**Event Lifecycle:**
5. Maximum event size — is there a limit?
6. Can a single command produce zero events?
7. Who assigns event sequence number — actor, store, or workflow?
8. What if two events arrive at a subscriber out of order?

**Multi-Tenancy:**
9. Can a single aggregate span multiple tenants?
10. Can a tenant be suspended? What happens to in-flight commands?
11. How do you delete a tenant's data completely? GDPR right-to-erasure on append-only store?

**Operational:**
12. How do you replay all events for a single aggregate from the beginning?
13. What does system "health" look like? What's on the dashboard?
14. How do you know a saga is stuck? What's the alert?

**Testing:**
15. How do you integration-test a full command→event→saga→command cycle locally?
16. Can you run the entire system without DAPR for local development?

**Authorization (user-contributed):**
17. Who is the user? External identity provider or EventStore-managed?
18. Where does authorization live — gateway, actor, domain service, all three?
19. What's the authorization model — RBAC, ABAC, per-tenant roles, per-command ACLs?
20. Can a user belong to multiple tenants with different roles?
21. Who decides "user X can execute command Y on tenant Z in domain W"?
22. Is authorization data itself event-sourced?
23. Does the domain service receive user identity/permissions or trust upstream authorization?
24. Can authorization rules depend on aggregate state?
25. How do you audit authorization failures?
26. Is there a super-admin concept that crosses all tenants?

**Data & Schema:**
27. Event retention policy — keep forever? Per-tenant configurable?
28. How do you handle event schema versioning between domain service versions?
29. Can you query "all aggregates of type X for tenant Y"?
30. Where does aggregate type live — metadata, routing config, or derived?

**Workflow & Saga:**
31. Maximum saga duration — can a saga wait days?
32. How do you cancel a saga mid-flight?
33. Can a saga span multiple tenants?
34. Who owns the saga definition — EventStore or domain service?

**Developer Experience:**
35. What does the domain service SDK look like?
36. How does a domain developer test in isolation?
37. Debugging story — "my command went in but nothing happened"?
38. Local dev emulator — Aspire or docker-compose?

**Deployment & Operations:**
39. Can you deploy new domain service version without restarting EventStore?
40. EventStore upgrade path — zero downtime?
41. How do you handle poison commands?
42. Dead letter strategy — where do failed commands go?

**Scale Boundaries:**
43. Maximum number of tenants?
44. Maximum domains per tenant?
45. What happens when a single aggregate's event stream gets enormous?
46. Back-fill strategy for new projections needing historical events?

**Consistency & Ordering:**
47. Global event ordering or per-aggregate only?
48. Can two different aggregates' events require ordered processing by a subscriber?
49. Consistency guarantee across read models?
50. "Read your own writes" guarantee?

**Versioning & Migration:**
51. Two EventStore versions simultaneously during upgrades?
52. In-flight DAPR Workflows during workflow definition upgrade?
53. Domain service v2 produces different events — how do projections handle both?
54. Migrate a tenant without replaying entire history?

**Edge Cases:**
55. Command targets never-existed aggregate — who validates?
56. Can an aggregate be "deleted"? Tombstone event?
57. Domain service returns empty event list — success or rejection?
58. Command addressed to multiple aggregates atomically?

**Integration & Ecosystem:**
59. External systems send commands — REST only? gRPC?
60. External system subscribe to events directly?
61. Webhook support — HTTP callbacks for specific events?
62. Event replay API — re-publish events from sequence X to Y?

**Existential:**
63. Simplest thing that works for v1?
64. One decision that forces a rewrite if wrong?
65. Only three features — which three?
66. Who is the first user?

### The Dangerous Six — Blocking Questions

**#11: GDPR Right-to-Erasure on Append-Only Store**
Fundamental tension with core data model. Crypto-shredding (per-tenant encryption, destroy key to erase) is the common pattern but requires per-tenant encryption from day one. Retrofitting is nearly impossible.
**Must answer before:** Event envelope design.

**#37: Debugging Story — "My Command Went In But Nothing Happened"**
Command traverses 6+ hops. Without end-to-end tracing (OpenTelemetry? Custom trace viewer in Blazor UI?), debugging is guesswork. Correlation ID exists but needs an aggregation layer.
**Must answer before:** Workflow implementation.

**#42: Dead Letter Strategy**
Poison commands block aggregate queue sessions forever without max-retry → dead letter policy. Operators need inspect/fix/replay capabilities in Blazor UI.
**Must answer before:** Workflow retry policy design.

**#57: Empty Event List — Success or Rejection?**
Semantic contract between EventStore and every domain service. Must be defined explicitly or every domain developer guesses differently.
**Must answer before:** Domain service API contract definition.

**#63: Simplest Thing That Works for v1**
Scope control. 83+ ideas and full architecture, but v1 must be ruthlessly scoped or it ships never.
**Must answer before:** Writing any code.

**#64: The One Decision That Forces a Rewrite — THE EVENT ENVELOPE**
The 11-field metadata structure + payload format. Every event ever stored conforms to this shape. Every domain service produces it. Every subscriber consumes it. Every projection depends on it. Get it wrong, rewrite everything.
**Must answer before:** Everything else.

## Extension Session Summary

### Key Achievements

- **3 additional techniques** applied: Reverse Brainstorming, Assumption Reversal, Question Storming
- **1 major architectural revision:** DAPR Workflows replace custom actor state machine
- **1 event envelope change:** `serialization_format` added as 11th metadata field
- **66 questions** generated, 6 identified as blocking
- **1 existential risk identified:** Event envelope is the single decision that forces a rewrite if wrong

### Revised Architecture at a Glance

```
Client → REST API (auth + validation)
  → Command Queue (per aggregate type, sessions per instance)
    → DAPR Workflow (command lifecycle orchestrator):
      → Step 1: Activate DAPR Actor (tenant:domain:aggregate-id)
      → Step 2: Actor calls Domain Service (pure function)
        → Domain reads event stream from EventStore
        → Returns List<DomainEvent> (+ optional snapshot event)
      → Step 3: Workflow stores events in DAPR statestore (checkpoint)
      → Step 4: Workflow publishes events to DAPR pub/sub (checkpoint)
      → Step 5: Workflow marks command done (checkpoint)

Sagas: DAPR sub-workflows
  → Subscribe to events
  → Call external services (side effects live here, not in domain)
  → Emit new commands back through EventStore
  → Built-in compensation support

Subscribers (all via DAPR pub/sub):
  - Projection builders (read from event store stream, snapshot-aware)
  - Saga/process managers (DAPR sub-workflows)
  - Cloud replica
  - Monitoring dashboard (Blazor Fluent UI 5)
  - External integrations

Event Metadata Envelope (11 fields):
  aggregate ID, tenant, domain, sequence, timestamp,
  correlation ID, causation ID, user identity,
  domain service version, event type name, serialization format
```

### Implementation Priority Update

1. **Event Envelope Design** — The one decision that can't be wrong. 11-field metadata + payload format. Address GDPR crypto-shredding requirement here.
2. **One Identity Scheme** — `tenant:domain:aggregate-id` addressing for all primitives
3. **DAPR Workflow + Actor Model** — Workflow orchestrates lifecycle, Actor holds aggregate state
4. **Dead Letter + Tracing** — Poison command handling and end-to-end observability
5. **Domain Service API Contract** — Including empty event list semantics
