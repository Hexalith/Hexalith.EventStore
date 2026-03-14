---
stepsCompleted: [1, 2, 3, 4]
inputDocuments: []
session_topic: 'DDD Message Design for Hexalith.Tenants - Metadata Envelope, Command/Event Patterns, and Cross-Boundary Consumption'
session_goals: 'Design metadata envelope structure, establish command/event message patterns for Tenants bounded context, address projection and cross-boundary consumer implications'
selected_approach: 'ai-recommended'
techniques_used: ['First Principles Thinking', 'Morphological Analysis', 'Constraint Mapping']
ideas_generated: [59]
context_file: ''
---

# Brainstorming Session Results

**Facilitator:** Jerome
**Date:** 2026-03-13

## Session Overview

**Topic:** DDD Message Design for Hexalith.Tenants — Metadata Envelope, Command/Event Patterns, and Cross-Boundary Consumption

**Goals:**
1. Design the metadata envelope structure (what belongs alongside aggregateId)
2. Establish command/event message design patterns for the Tenants bounded context
3. Address implications for projections and cross-boundary consumers that won't have the envelope

### Session Setup

**Key Decision Already Made:** AggregateId lives in metadata only — payloads contain pure business facts.

**Three brainstorming threads:**
- Thread A: Metadata envelope composition
- Thread B: Command/Event message patterns for Tenants
- Thread C: Projection & cross-boundary consumer strategies

## Technique Selection

**Approach:** AI-Recommended Techniques
**Analysis Context:** DDD Message Design with focus on metadata envelope, command/event patterns, cross-boundary consumption

**Recommended Techniques:**

- **First Principles Thinking:** Strip assumptions from event sourcing conventions — derive metadata requirements from fundamentals
- **Morphological Analysis:** Systematically map all metadata parameters and their option spaces, then explore combinations
- **Constraint Mapping:** Identify real vs. imagined constraints for projections and cross-boundary consumers

**AI Rationale:** Complex technical architecture problem requiring structured divergence before convergence. Three interconnected threads benefit from first-principles foundation, systematic parameter exploration, then constraint-driven refinement.

## Technique Execution Results

### Phase 1: First Principles Thinking (29 ideas)

**Architecture Discovery:**
- EventStore is the central gateway — authenticates, routes commands to domain microservices, persists events
- Flow: Client → EventStore (auth + route) → Domain Service (process) → EventStore (persist)
- Domain services are pure functions: (command, aggregateState) → event[]

**Client Command Envelope (4 required + 1 optional):**
- messageId (ULID, client-generated, idempotency)
- aggregateId (ULID, target aggregate)
- command type (kebab, includes bounded context: `tenants-create-tenant-v1`)
- payload (JSON, business parameters)
- correlationId (optional ULID, defaults to messageId)

**Domain Service Returns (pure business output):**
- event type (.NET type, EventStore converts to kebab)
- aggregate type (short kebab: `tenant`)
- event payload (business facts)

**EventStore Enriches and Persists:**
- event messageId (ULID, EventStore generates)
- causationId (= command messageId)
- userId (from authentication context)
- correlationId (from command, defaulted if missing)
- bounded context (extracted from command type prefix)
- sequence number (stream ordering)
- global position (cross-stream ordering)
- timestamp (persistence time)

**Key Principles Established:**
- Commands are ephemeral, events are durable truth
- Bounded context is embedded in message type prefix, not a separate field
- AggregateId is metadata-only, payload contains pure business facts
- Three logical layers within EventStore: infrastructure (auth), domain delegation, persistence
- Client-generated messageId for idempotency at command level
- Server-authenticated userId — never client-supplied

### Phase 2: Morphological Analysis (14 ideas)

**Design Decisions Matrix:**

| Parameter | Decision |
|---|---|
| ID format | ULID everywhere |
| Event type naming | Kebab: `{bc}-{event}-v{ver}`, assembled by EventStore |
| Command type naming | Kebab: `{bc}-{command}-v{ver}`, sent by client |
| Query type naming | Kebab: `{bc}-{projection}-{query}-v{ver}` |
| Aggregate type | Short kebab: `tenant` (no scope prefix) |
| Bounded context | Extracted from message type prefix, no separate field |
| Serialization | Two separate JSONs: metadata + payload |
| API structure | Separate endpoints per message kind (`/commands`, `/queries`) |

**Key Insight:** The message type string does triple duty — routing address, type discriminator, and bounded context carrier.

### Phase 3: Constraint Mapping (16 ideas)

**Consumer Access Model:**
- Internal consumers (projections, sagas): stream subscription with full metadata + payload
- External consumers: Dapr pub/sub with CloudEvents envelope

**External Event Envelope (CloudEvents):**
```json
{
  "specversion": "1.0",
  "type": "tenants-tenant-created-v1",
  "source": "/hexalith/tenants",
  "id": "01HXK...",
  "time": "2026-03-13T...",
  "datacontenttype": "application/json",
  "data": {
    "metadata": { "...full internal metadata..." },
    "payload": { "...business facts..." }
  }
}
```

**Key Constraint Resolutions:**

| Constraint | Resolution |
|---|---|
| AggregateId for external consumers | Full metadata published, aggregateId available |
| Schema evolution | Multi-version handlers, immutable events, no upcasting |
| Version handling on replay | Projections must handle all historical versions |
| Consumer idempotency | Checkpoint (streams) + messageId dedup (broker) |
| Ordering guarantees | Broker partitioned by aggregateId |
| Internal vs external metadata | No difference — full metadata in both paths |

**Key Principle:** Aggregate boundary = ordering boundary = partition boundary. DDD concept maps directly to infrastructure.

### Creative Facilitation Narrative

Session evolved through progressive refinement driven by Jerome's corrections. Several assumptions were challenged and corrected:
1. "Server and EventStore are the same" → then clarified as separate domain + EventStore microservices
2. EventStore is the authentication authority, not the domain service
3. EventStore is the gateway — clients call EventStore, EventStore calls domain services
4. CausationId is set by EventStore, not domain service
5. Bounded context removed from separate metadata — embedded in message type prefix
6. Full metadata exposed to external consumers — no curation

Each correction sharpened the architecture toward a cleaner, simpler design with fewer fields and clearer responsibilities.

## Advanced Elicitation Results

### ADR — Architecture Decision Records

**ADR-001: EventStore as Command Gateway**
- **Decision:** EventStore is the entry point — authenticates, routes to domain services, persists events.
- **Alternatives rejected:** Domain services as entry points (auth duplication), separate API gateway (extra hop), direct client-to-domain (no centralized persistence).
- **Trade-off:** Single point of failure, but Dapr sidecar handles scaling.

**ADR-002: Ultra-Thin Client Command (4+1 fields)**
- **Decision:** messageId, aggregateId, commandType, payload + optional correlationId.
- **Alternatives rejected:** Separate bounded context field, client-supplied userId, client-supplied timestamp.
- **Trade-off:** Client must know kebab naming convention.

**ADR-003: Bounded Context Embedded in Message Type**
- **Decision:** `{bc}-{name}-v{ver}` convention, parsed by EventStore for routing.
- **Alternatives rejected:** Separate metadata field (redundancy), routing registry (configuration overhead).
- **Trade-off:** String parsing dependency — malformed type = routing failure.

**ADR-004: Domain Service as Pure Function**
- **Decision:** Domain returns only event type, aggregate type, payload.
- **Alternatives rejected:** Domain builds full event envelope, domain generates messageIds.
- **Trade-off:** EventStore must know how to enrich events.

**ADR-005: Two-JSON Storage (Metadata + Payload)**
- **Decision:** Each event stored as separate metadata JSON and payload JSON.
- **Alternatives rejected:** Single nested JSON, Protobuf, mixed formats.
- **Trade-off:** Two documents per event, two parse operations.

**ADR-006: Full Metadata in CloudEvents for External Consumers**
- **Decision:** No curation — external consumers see everything via CloudEvents `data: { metadata, payload }`.
- **Alternatives rejected:** Curated subset (maintenance burden), payload-only (breaks aggregateId access).
- **Trade-off:** Leaks internal structure. External consumers could depend on internal fields.

**ADR-007: Immutable Events, Multi-Version Handlers**
- **Decision:** No upcasting. Events stored as-is forever. Consumers handle all versions.
- **Alternatives rejected:** Upcasting (store complexity), schema registry (infrastructure overhead).
- **Trade-off:** Projection code grows with every version.

### Red Team vs Blue Team — Vulnerabilities Found & Defenses

| Attack | Vulnerability | Defense |
|---|---|---|
| Command type parsing | Malformed strings crash routing | `MessageType` value object with factory validation at entry |
| Version explosion | 50 event types × 3 versions = 150 handlers | Base handler pattern with v1→v2 adapter one-liners |
| Full metadata exposure | External consumers depend on internal fields | Add metadata schema version for external contract evolution |
| Gateway bottleneck | EventStore down = system down | Dapr scaling, read replicas for queries, partition by BC |
| ULID clock skew | Client clocks produce non-monotonic ULIDs | MessageId used for equality only, not ordering — no impact |
| Aggregate type validation | Domain service could return wrong type | EventStore validates against registered types at persistence |

### Pre-mortem Analysis — Failure Scenarios & Prevention

| Failure | Scenario | Prevention |
|---|---|---|
| The Great Rename | Bounded context renamed, historical events have old prefix | Treat BC names as permanent identifiers, not display names. Consider alias registry |
| The Payload Surprise | Developer puts aggregateId in payload, creating dual source of truth | Lint rule: payload must never contain known metadata field names |
| The Version Nobody Handled | New event version deployed, projection silently skips it | Monitor unhandled versions. CI gate: every projection must handle all versions |
| The Correlation Flood | Saga bug creates infinite command→event loop | Max causation chain depth. Circuit breaker per correlationId |
| The Clock That Lied | EventStore node with clock skew produces wrong timestamps | NTP monitoring. Document that global position is authoritative ordering |

### Self-Consistency Validation

| Check | Status | Issue |
|---|---|---|
| BC in message type + client sends full string + EventStore parses | PASS | Consistent flow |
| Domain returns aggregate type + EventStore assembles event type | FLAG | **Stream naming convention not defined.** If stream = `{bc}-{aggregateType}-{aggregateId}`, EventStore needs aggregate type for persistence, not just metadata |
| Commands ephemeral + causationId = command messageId | PASS | Causal link survives without the command |
| EventStore generates event messageId + messageId for idempotency | FLAG | **EventStore needs its own retry safety** for event persistence. What if crash between generation and persistence? |
| Full metadata for external + broker partitioned by aggregateId | FLAG | **Partition key setting** — EventStore publishing logic must extract aggregateId and set as Dapr partition key. Implementation detail not explicit |
| CorrelationId defaults to messageId + inherited through chain | PASS | Consistent lifecycle |

### Stakeholder Review

**Winston (Architect) 🏗️:** Define stream naming convention explicitly. Add Dapr service invocation with retries/circuit breakers between EventStore and domain services. Add metadata schema version for external contract.

**Amelia (Dev) 💻:** Need clear aggregate type and command handler registration pattern for domain services. Need abstract base class or interface for multi-version event handler dispatch.

**Murat (Test Architect) 🧪:** Need test EventStore with real routing but in-memory storage for integration tests. Need CI contract tests: every event type version must have handlers in every projection.

### Design Gaps Identified

1. **Stream naming convention** — not defined (`{bc}-{aggregateType}-{aggregateId}`?)
2. **Event messageId idempotency** — EventStore generates it but needs its own retry safety
3. **Dapr partition key** — implementation detail for broker publishing not explicit
4. **Domain service registration** — how aggregate types and handlers are registered
5. **Metadata schema version** — needed for external contract evolution
6. **MessageType validation** — value object with factory method needed at entry point

### Risks to Monitor

1. Bounded context rename is irreversible
2. Version explosion in long-lived projections
3. Correlation chain loops (saga bugs)
4. Implicit external contracts via full metadata exposure
5. EventStore as single point of failure

### Confirmed Strengths

1. Domain services as pure functions — excellent testability
2. Ultra-thin client command — minimal attack surface
3. Single source of truth for bounded context (message type prefix)
4. ULID for idempotency without ordering dependency
5. Two-JSON storage enables metadata-only queries
6. CloudEvents for external consumers — industry standard

## Idea Organization and Prioritization

### Thematic Organization

**Theme 1: Message Contracts & Naming Convention**
- Universal `{bc}-{name}-v{ver}` kebab pattern across commands, events, queries
- Ultra-thin client command (4+1 fields)
- Bounded context embedded in message type prefix — no separate field
- EventStore assembles event type from domain output + command context
- `MessageType` value object with factory validation at entry

**Theme 2: Architecture — EventStore as Gateway**
- Client → EventStore (auth + route) → Domain Service (process) → EventStore (persist)
- Separate API endpoints per message kind (`/commands`, `/queries`)
- EventStore sets causationId, generates event messageId, defaults correlationId
- Domain services are pure functions with zero infrastructure concerns

**Theme 3: Metadata Model — Two Sources, Two JSONs**
- Client-supplied vs server-derived metadata with clear ownership
- Two separate JSON documents per event: metadata + payload
- ULID for all identity fields
- Aggregate type short kebab, no scope duplication

**Theme 4: Consumer Strategy — Internal Streams + External CloudEvents**
- Internal consumers: stream subscription with full metadata + payload
- External consumers: Dapr pub/sub with CloudEvents envelope, full metadata
- Broker partitioned by aggregateId = aggregate boundary = ordering boundary
- Consumer idempotency: checkpoint (streams) + messageId dedup (broker)

**Theme 5: Schema Evolution — Immutable Events, Versioned Handlers**
- Events stored as-is forever, no upcasting
- Multi-version handlers: projections handle all historical versions
- CI gate: no deployment if unhandled version exists

**Theme 6: Design Gaps & Risks**
- Stream naming convention, event messageId idempotency, domain service registration
- Metadata schema version, correlation chain circuit breaker
- Bounded context rename irreversibility

### Breakthrough Concepts

- **Domain Service as Pure Function** — `(command, state) → event[]` with zero infrastructure types
- **Message Type String as Universal Address** — routing, typing, and context identification in one string
- **Aggregate Boundary = Ordering Boundary = Partition Boundary** — DDD maps 1:1 to infrastructure

### Prioritization Results

**Top 3 High-Impact Decisions:**
1. Message type convention `{bc}-{name}-v{ver}` — all other decisions depend on this
2. EventStore gateway architecture — defines entire system topology
3. Two-JSON metadata + payload storage — persistence contract for all consumers

**Quick Wins:**
1. ULID package integration + `UlidId` value object
2. Kebab naming utility (PascalToKebab, AssembleEventType, ParseMessageType)
3. Command envelope DTO (messageId, aggregateId, commandType, correlationId?, payload)
4. Event envelope DTO (metadata JSON + payload JSON)
5. CloudEvents wrapper for Dapr pub/sub publishing

**Items Needing Further Design:**
1. Stream naming convention (High priority)
2. Domain service registration & contract (High priority)
3. Event messageId idempotency strategy (Medium priority)
4. Metadata schema version for external contract (Medium priority)
5. Multi-version handler pattern (Medium priority)
6. Correlation chain circuit breaker (Low — before sagas)

### Action Planning — Implementation Sequence

**Phase 1 — Foundation (Quick Wins)**
- ULID integration
- Kebab naming utility
- Command envelope DTO
- Event envelope DTO

**Phase 2 — Core Architecture (High-Impact)**
- Stream naming convention
- MessageType value object with validation
- EventStore /commands endpoint
- Domain service contract
- Two-JSON storage implementation

**Phase 3 — Consumer Infrastructure**
- CloudEvents wrapper
- Metadata schema version
- Subscription API (composable filters)
- Checkpoint / dedup mechanisms

**Phase 4 — Developer Experience**
- Multi-version handler pattern
- Domain service SDK
- Integration test harness
- CI contract tests

**Phase 5 — Resilience (before sagas)**
- Event messageId idempotency
- Correlation chain circuit breaker
- Dapr retry / circuit breaker configuration

## Session Summary and Insights

**Key Achievements:**
- 59 ideas generated across 3 structured techniques (First Principles, Morphological Analysis, Constraint Mapping)
- 7 Architecture Decision Records with trade-offs documented
- 6 vulnerabilities identified and defended (Red Team / Blue Team)
- 5 production failure scenarios anticipated and mitigated (Pre-mortem)
- 6 consistency checks performed, 3 gaps flagged (Self-Consistency)
- 3 stakeholder perspectives captured (Architect, Dev, Test Architect)
- 5-phase implementation sequence defined

**Breakthrough Moments:**
- Discovering EventStore is the gateway, not a backend persistence service
- Realizing bounded context doesn't need a separate metadata field
- The "three corrections" that purified domain services to zero infrastructure concerns
- Aggregate boundary = ordering boundary = partition boundary collapse

**Session Reflections:**
Jerome's architectural instincts drove the design toward simplicity at every step. Each correction removed a field, eliminated a concern, or clarified a responsibility. The final design has fewer moving parts than the initial assumptions — a sign of good architecture.
