---
stepsCompleted:
    - step-01-validate-prerequisites
    - step-02-design-epics
    - step-03-create-stories
inputDocuments:
    - prd.md
    - architecture.md
    - ux-design-specification.md
---

# Hexalith.EventStore - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Hexalith.EventStore, decomposing the requirements from the PRD, UX Design, and Architecture into implementable stories.

## Requirements Inventory

### Functional Requirements

**Command Processing (9 FRs):**

- FR1: An API consumer can submit a command to the EventStore via a REST endpoint with tenant, domain, aggregate ID, command type, and payload
- FR2: The system can validate a submitted command for structural completeness (required fields: tenantId, domain, aggregateId, commandType, payload; well-formed JSON structure) before routing it for processing
- FR3: The system can route a command to the correct aggregate actor based on the identity scheme (`tenant:domain:aggregate-id`)
- FR4: An API consumer can receive a correlation ID upon command submission for tracking the command lifecycle
- FR5: An API consumer can query the processing status of a previously submitted command using its correlation ID
- FR6: An operator can replay a previously failed command via the Command API after root cause is fixed
- FR7: The system can reject duplicate commands targeting an aggregate that has an optimistic concurrency conflict, returning an appropriate error
- FR8: The system can route failed commands to a dead-letter topic with full command payload, error details, and correlation context
- FR49: The system can detect and reject duplicate commands by tracking processed command IDs per aggregate, returning an idempotent success response for already-processed commands

**Event Management (10 FRs):**

- FR9: The system can persist events in an append-only, immutable event store where events are never modified or deleted after persistence
- FR10: The system can assign strictly ordered, gapless sequence numbers to events within a single aggregate stream
- FR11: The system can wrap each event in a 14-field metadata envelope (aggregate ID, tenant, domain, sequence, timestamp, correlation ID, causation ID, user identity, domain service version, event type, serialization format) plus opaque payload and extension metadata bag
- FR12: The system can reconstruct aggregate state by replaying all events in an aggregate's stream from sequence 1 to current
- FR13: The system can create snapshots of aggregate state at administrator-configured event count intervals (default: every 100 events, configurable per tenant-domain pair)
- FR14: The system can reconstruct aggregate state from the latest snapshot plus subsequent events, producing identical state to full replay
- FR15: The system can store events using a composite key strategy that includes tenant, domain, and aggregate identity for isolation
- FR16: The system can enforce atomic event writes -- a command produces 0 or N events as a single transaction, never a partial subset
- FR65: The event metadata envelope can include a `metadataVersion` field (integer, starting at 1) enabling external consumers to detect envelope schema changes and adapt their deserialization without breaking
- FR66: The system can mark an aggregate as terminated (tombstoned) via a terminal event. A terminated aggregate rejects all subsequent commands with a domain rejection event, while its event stream remains immutable and replayable

**Event Distribution (5 FRs):**

- FR17: The system can publish persisted events to subscribers via a pub/sub mechanism using CloudEvents 1.0 envelope format
- FR18: The system can deliver events to subscribers with at-least-once delivery guarantee
- FR19: The system can publish events to per-tenant-per-domain topics, ensuring subscribers only receive events for their authorized scope
- FR20: The system can continue persisting events when the pub/sub system is temporarily unavailable, draining the backlog on recovery
- FR67: The system can apply backpressure when aggregate command queues exceed a configurable depth threshold (default: 100 pending commands per aggregate), returning HTTP 429 with Retry-After header to prevent saga storms

**Domain Service Integration (5 FRs):**

- FR21: A domain service developer can implement a domain processor as a pure function with the contract `(Command, CurrentState?) -> List<DomainEvent>`
- FR22: A domain service developer can register their domain service with EventStore by tenant and domain via explicit DAPR configuration entry or automatically via convention-based assembly scanning
- FR23: The system can invoke a registered domain service when processing a command, passing the command and current aggregate state
- FR24: The system can process commands for at least 2 independent domains within the same EventStore instance
- FR25: The system can process commands for at least 2 tenants within the same domain, each with isolated event streams

**Identity & Multi-Tenancy (4 FRs):**

- FR26: The system can derive actor IDs, event stream keys, pub/sub topics, and queue sessions from a single canonical identity tuple (`tenant:domain:aggregate-id`)
- FR27: The system can enforce data path isolation so that commands for one tenant are never routed to another tenant's domain service
- FR28: The system can enforce storage key isolation so that event streams for different tenants are inaccessible to each other even at the state store level
- FR29: The system can enforce pub/sub topic isolation so that event subscribers only receive events from tenants they are authorized to access

**Security & Authorization (5 FRs):**

- FR30: An API consumer can authenticate with the Command API using a JWT token
- FR31: The system can authorize command submissions based on JWT claims for tenant, domain, and command type
- FR32: The system can reject unauthorized commands at the API gateway before they enter the processing pipeline
- FR33: The system can validate that a command's tenant matches the authenticated user's authorized tenants at the actor level
- FR34: The system can enforce service-to-service access control between EventStore components via DAPR policies

**Observability & Operations (5 FRs):**

- FR35: The system can emit OpenTelemetry traces spanning the full command lifecycle (received, processing, events stored, events published, completed)
- FR36: The system can emit structured logs with correlation and causation IDs at each stage of the command processing pipeline
- FR37: An operator can trace a failed command from the dead-letter topic back to its originating request via correlation ID
- FR38: The system can expose health check endpoints indicating DAPR sidecar, state store, and pub/sub connectivity status
- FR39: The system can expose readiness check endpoints indicating all dependencies are healthy and the system is accepting commands

**Developer Experience & Deployment (10 FRs):**

- FR40: A developer can start the complete EventStore system with a single Aspire command
- FR41: A developer can reference a sample domain service implementation as a working example of the pure function programming model
- FR42: A developer can install EventStore client packages via NuGet to build and register domain services, with a zero-configuration quickstart
- FR43: A DevOps engineer can deploy EventStore to different environments by changing only DAPR component configuration files with zero application code changes
- FR44: A DevOps engineer can generate deployment manifests for Docker Compose, Kubernetes, or Azure Container Apps via Aspire publishers
- FR45: A developer can run unit tests against domain service pure functions without any DAPR runtime dependency
- FR46: A developer can run integration tests against the actor processing pipeline using DAPR test containers
- FR47: A developer can run end-to-end contract tests validating the full command lifecycle across the complete Aspire topology
- FR48: A domain service developer can implement a domain aggregate by inheriting from EventStoreAggregate with typed Apply methods, as a higher-level alternative to implementing IDomainProcessor directly
- FR60: The EventStore documentation and sample application can provide at least 3 reference patterns for handling the SignalR "changed" signal in Blazor UI components

**Query Pipeline & Projection Caching -- v2 (15 FRs):**

- FR50: The system can route incoming query messages to query actors using a 3-tier routing model based on EntityId and payload presence
- FR51: The system can maintain one ETag actor per `{ProjectionType}-{TenantId}` storing a self-routing ETag representing the current projection version
- FR52: A domain service developer can notify EventStore of a projection change by calling `NotifyProjectionChanged(projectionType, tenantId, entityId?)`
- FR53: The query REST endpoint can perform an ETag pre-check (first gate) by decoding the self-routing ETag from the client's `If-None-Match` header
- FR54: A query actor can serve as an in-memory page cache (second gate) with no state store persistence
- FR55: The system can broadcast a signal-only "changed" message to connected SignalR clients when a projection's ETag is regenerated
- FR56: The system can host a SignalR hub inside the EventStore server, using a Redis backplane for multi-instance distribution
- FR57: A query contract library (NuGet) can define mandatory query metadata fields (Domain, QueryType, TenantId) and optional fields (EntityId)
- FR58: The system can invalidate all cached query results for a projection+tenant pair on any projection change notification (coarse invalidation)
- FR59: The SignalR client helper (NuGet) can automatically rejoin SignalR groups on connection recovery
- FR61: The system can encode self-routing ETags in the format `{base64url(projectionType)}.{guid}` and decode them at the query endpoint
- FR62: The microservice query response contract (`IQueryResponse<T>`) can enforce at compile time that every query response includes a non-empty `ProjectionType` field
- FR63: A query actor can discover its projection type mapping at runtime from the microservice's `IQueryResponse<T>` response on its first (cold) call
- FR64: The EventStore documentation can recommend short projection type names to keep self-routing ETags compact in HTTP headers

### NonFunctional Requirements

**Performance (8 NFRs):**

- NFR1: Command submission via REST API must complete (return 202 Accepted) within 50ms at p99
- NFR2: End-to-end command lifecycle must complete within 200ms at p99
- NFR3: Event append latency must be under 10ms at p99
- NFR4: Actor cold activation with state rehydration must complete within 50ms at p99
- NFR5: Pub/sub delivery must complete within 50ms at p99
- NFR6: Full aggregate state reconstruction from 1,000 events must complete within 100ms
- NFR7: The system must support at least 100 concurrent command submissions per second per instance
- NFR8: DAPR sidecar overhead per building block call must not exceed 2ms at p99

**Security (7 NFRs):**

- NFR9: All communication encrypted via TLS 1.2+
- NFR10: JWT tokens validated for signature, expiry, and issuer on every request
- NFR11: Failed auth attempts logged with request metadata, without logging JWT tokens
- NFR12: Event payload data must never appear in log output; only envelope metadata
- NFR13: Multi-tenant isolation enforced at all three layers (actor identity, DAPR policies, command metadata)
- NFR14: Secrets must never be stored in application code or committed config files
- NFR15: Service-to-service communication authenticated via DAPR access control policies

**Scalability (5 NFRs):**

- NFR16: Horizontal scaling via DAPR actor placement distributing aggregates across replicas
- NFR17: Support at least 10,000 active aggregates per instance
- NFR18: Support at least 10 tenants with full isolation and no cross-tenant performance interference
- NFR19: Event stream growth bounded by snapshot strategy -- constant rehydration time
- NFR20: Adding new tenant/domain without system restart via dynamic DAPR config

**Reliability (6 NFRs):**

- NFR21: 99.9%+ availability with HA DAPR control plane
- NFR22: Zero events lost under any tested failure scenario
- NFR23: After state store recovery, resume from last checkpoint -- no manual intervention
- NFR24: After pub/sub recovery, all persisted events delivered via DAPR retry
- NFR25: Actor crash after persistence must not cause duplicate event persistence
- NFR26: Optimistic concurrency conflicts detected and reported (409 Conflict)

**Integration (6 NFRs):**

- NFR27: Compatible with any DAPR-compatible state store (validated: Redis, PostgreSQL)
- NFR28: Compatible with any DAPR-compatible pub/sub (validated: RabbitMQ, Azure Service Bus)
- NFR29: Backend switching via DAPR component YAML only -- zero code changes
- NFR30: Domain services invocable via DAPR service invocation over HTTP
- NFR31: OpenTelemetry exportable to any OTLP-compatible collector
- NFR32: Deployable via Aspire publishers to Docker Compose, Kubernetes, Azure Container Apps

**Rate Limiting (2 NFRs):**

- NFR33: Per-tenant rate limiting (default: 1,000 commands/minute/tenant) with 429 + Retry-After
- NFR34: Per-consumer rate limiting (default: 100 commands/second/consumer) with 429 + Retry-After

**Query Pipeline Performance -- v2 (5 NFRs):**

- NFR35: ETag pre-check within 5ms at p99 for warm actors
- NFR36: Query actor cache hit within 10ms at p99
- NFR37: Query actor cache miss within 200ms at p99
- NFR38: SignalR "changed" signal delivery within 100ms at p99
- NFR39: Query pipeline supports at least 1,000 concurrent queries per second per instance

### Additional Requirements

**From Architecture:**

- Custom solution from individual `dotnet new` templates (no existing starter fits the 5-package architecture)
- Project initialization using these templates should be the first implementation story
- Event storage: single-key-per-event with actor-level ACID writes (D1)
- Command status: dedicated state store key with 24h TTL (D2)
- Domain errors expressed as events with `IRejectionEvent` marker interface (D3)
- DAPR access control: per-app-id allow list (D4)
- API errors: RFC 7807 Problem Details with correlationId/tenantId extensions (D5)
- Pub/sub topic naming: `{tenant}.{domain}.events` (D6)
- Domain service invocation via DAPR `InvokeMethodAsync` (D7)
- Rate limiting: ASP.NET Core built-in `SlidingWindowRateLimiter` (D8)
- Package versioning: MinVer from Git tags (D9)
- CI/CD: GitHub Actions (D10)
- E2E security testing: Keycloak in Aspire with realm-as-code (D11)
- 17 enforcement rules covering naming, structure, pipeline, resilience, security, observability, and data integrity
- 5 security constraints (SEC-1 through SEC-5) for envelope ownership, tenant validation, status scoping, metadata sanitization, and payload logging
- CRITICAL-1: UnknownEvent during rehydration is an error, not a skip path -- backward-compatible deserialization mandatory
- CRITICAL-2: Command status TTL (24h default) to prevent unbounded state store growth
- Implementation sequence: Contracts → Testing → Server → CommandApi → Client → Sample → Aspire/AppHost → Deploy configs → CI/CD
- Convention-derived resource names use kebab-case with automatic type suffix stripping

**From UX Design:**

- Four distinct interaction surfaces: Developer SDK, REST API Consumer, CLI/Operator, Blazor Dashboard (v2)
- Developer SDK: pure function model with zero infrastructure imports
- REST API: standard REST with 202 Accepted, Location header, status polling -- no event sourcing terminology exposed
- CLI/Operator: Aspire-powered single-command startup with OpenTelemetry trace visibility
- Blazor Dashboard (v2): Fluent UI 4.x with real-time SignalR push, three refresh patterns (toast, auto-reload, selective component refresh)
- Swagger UI grouped endpoints with example payloads
- RFC 7807 error responses that never leak event sourcing internals
- Accessibility and responsive design requirements for v2 dashboard

### FR Coverage Map

| FR | Epic | Brief Description |
|---|---|---|
| FR1 | Epic 1 | Command submission via REST endpoint |
| FR2 | Epic 1 | Structural validation before routing |
| FR3 | Epic 1 | Route to actor via identity scheme |
| FR4 | Epic 1 | Correlation ID on submission |
| FR5 | Epic 1 | Status query by correlation ID |
| FR6 | Epic 5 | Operator replay of failed commands |
| FR7 | Epic 5 | Optimistic concurrency conflict rejection |
| FR8 | Epic 3 | Dead-letter routing for failed commands |
| FR9 | Epic 1 | Append-only immutable event store |
| FR10 | Epic 1 | Gapless sequence numbers per aggregate |
| FR11 | Epic 1 | 14-field metadata envelope |
| FR12 | Epic 1 | State reconstruction via event replay |
| FR13 | Epic 5 | Configurable snapshot intervals |
| FR14 | Epic 5 | State from snapshot + tail events |
| FR15 | Epic 1 | Composite key with tenant/domain/aggregate |
| FR16 | Epic 1 | Atomic event writes (all or nothing) |
| FR17 | Epic 3 | Publish events via CloudEvents 1.0 |
| FR18 | Epic 3 | At-least-once delivery guarantee |
| FR19 | Epic 3 | Per-tenant-per-domain topics |
| FR20 | Epic 3 | Persist events during pub/sub outage |
| FR21 | Epic 1 | Pure function domain processor contract |
| FR22 | Epic 2 | Domain service registration (config + scanning) |
| FR23 | Epic 1 | Invoke domain service on command |
| FR24 | Epic 4 | Multi-domain within single instance |
| FR25 | Epic 4 | Multi-tenant within same domain |
| FR26 | Epic 1 | Canonical identity tuple |
| FR27 | Epic 4 | Data path isolation |
| FR28 | Epic 4 | Storage key isolation |
| FR29 | Epic 4 | Pub/sub topic isolation |
| FR30 | Epic 4 | JWT authentication |
| FR31 | Epic 4 | Claims-based authorization |
| FR32 | Epic 4 | API gateway rejection of unauthorized |
| FR33 | Epic 4 | Actor-level tenant validation |
| FR34 | Epic 4 | DAPR service-to-service access control |
| FR35 | Epic 6 | OpenTelemetry traces full lifecycle |
| FR36 | Epic 6 | Structured logs with correlation IDs |
| FR37 | Epic 6 | Dead-letter to originating request trace |
| FR38 | Epic 6 | Health check endpoints |
| FR39 | Epic 6 | Readiness check endpoints |
| FR40 | Epic 2 | Single Aspire command startup |
| FR41 | Epic 2 | Sample domain service reference |
| FR42 | Epic 2 | NuGet packages with zero-config quickstart |
| FR43 | Epic 7 | Environment deployment via DAPR config |
| FR44 | Epic 7 | Aspire publisher deployment manifests |
| FR45 | Epic 2 | Unit tests without DAPR dependency |
| FR46 | Epic 7 | Integration tests with DAPR containers |
| FR47 | Epic 7 | E2E contract tests full topology |
| FR48 | Epic 2 | EventStoreAggregate base class alternative |
| FR49 | Epic 5 | Idempotent duplicate command detection |
| FR50 | Epic 8 | 3-tier query actor routing |
| FR51 | Epic 8 | ETag actor per projection+tenant |
| FR52 | Epic 8 | NotifyProjectionChanged helper |
| FR53 | Epic 8 | ETag pre-check (first gate) |
| FR54 | Epic 8 | Query actor in-memory cache (second gate) |
| FR55 | Epic 8 | SignalR "changed" broadcast |
| FR56 | Epic 8 | SignalR hub with Redis backplane |
| FR57 | Epic 8 | Query contract library (NuGet) |
| FR58 | Epic 8 | Coarse projection invalidation |
| FR59 | Epic 8 | SignalR auto-rejoin on reconnection |
| FR60 | Epic 8 | 3 Blazor refresh reference patterns |
| FR61 | Epic 8 | Self-routing ETag encoding/decoding |
| FR62 | Epic 8 | IQueryResponse compile-time enforcement |
| FR63 | Epic 8 | Runtime projection type discovery |
| FR64 | Epic 8 | Short projection type name guidance |
| FR65 | Epic 1 | Metadata version field in envelope |
| FR66 | Epic 5 | Aggregate tombstoning/lifecycle termination |
| FR67 | Epic 5 | Per-aggregate backpressure with HTTP 429 |

## Epic List

### Epic 1: Core Command-to-Event Pipeline
A developer can submit a command via REST API, have it routed to an aggregate actor, processed by a domain service, and see events persisted with full lifecycle status tracking.
**FRs covered:** FR1, FR2, FR3, FR4, FR5, FR9, FR10, FR11, FR12, FR15, FR16, FR21, FR23, FR26, FR65

### Epic 2: Developer SDK & Experience
A domain developer can build their own domain service using the SDK, register it, run the complete system with Aspire, reference the sample Counter implementation, and write unit tests without DAPR.
**FRs covered:** FR22, FR40, FR41, FR42, FR45, FR48

### Epic 3: Event Distribution & Pub/Sub
Persisted events are automatically published to subscribers using CloudEvents 1.0 with at-least-once delivery and per-tenant-per-domain topics. Failed commands route to dead-letter topics.
**FRs covered:** FR8, FR17, FR18, FR19, FR20

### Epic 4: Security, Authorization & Multi-Tenant Isolation
The Command API enforces JWT authentication and fine-grained authorization. Multiple tenants operate with proven data path, storage key, and pub/sub topic isolation. DAPR access control enforced between services.
**FRs covered:** FR24, FR25, FR27, FR28, FR29, FR30, FR31, FR32, FR33, FR34

### Epic 5: Resilience & Advanced Processing
The system handles duplicate commands idempotently, optimizes state rehydration with snapshots, supports operator command replay, and detects optimistic concurrency conflicts.
**FRs covered:** FR6, FR7, FR13, FR14, FR49, FR66, FR67

### Epic 6: Observability & Operations
Full pipeline visibility with distributed OpenTelemetry traces, structured logs with correlation/causation IDs, dead-letter traceability, and health/readiness endpoints.
**FRs covered:** FR35, FR36, FR37, FR38, FR39

### Epic 7: Production Deployment & CI/CD
EventStore deploys to production via Aspire publishers with GitHub Actions CI/CD pipeline, integration tests with DAPR containers, and E2E contract tests across the full topology.
**FRs covered:** FR43, FR44, FR46, FR47
**Architecture decisions:** D9 (MinVer), D10 (GitHub Actions), D11 (Keycloak E2E)

### Epic 8: Query Pipeline & Real-Time Updates (v2)
API consumers query projections efficiently with self-routing ETag-based caching and in-memory query actor pages. Blazor clients receive real-time SignalR push notifications on projection changes.
**FRs covered:** FR50, FR51, FR52, FR53, FR54, FR55, FR56, FR57, FR58, FR59, FR60, FR61, FR62, FR63, FR64

---

## Epic 1: Core Command-to-Event Pipeline

A developer can submit a command via REST API, have it routed to an aggregate actor, processed by a domain service, and see events persisted with full lifecycle status tracking.

### Story 1.1: Solution Scaffold & Event Envelope Contracts

As a domain service developer,
I want a well-structured solution with typed event envelope and identity contracts,
So that I can understand the event sourcing data model and start building against stable types.

**Acceptance Criteria:**

**Given** the repository is cloned with no projects
**When** the solution scaffold is created using the architecture-prescribed templates
**Then** the `Hexalith.EventStore.slnx` solution builds successfully with `dotnet build --configuration Release`
**And** the `src/`, `tests/`, and `samples/` directory structure matches the architecture document
**And** `global.json` pins the .NET SDK to 10.0.103
**And** `Directory.Build.props` configures `TreatWarningsAsErrors`, nullable, implicit usings, and centralized package management
**And** `Directory.Packages.props` declares all NuGet package versions centrally

**Given** the Contracts package exists
**When** I reference `Hexalith.EventStore.Contracts`
**Then** I can create an `EventMetadata` record with all 14 metadata fields: messageId (ULID), aggregateId (ULID), aggregateType, tenantId, sequenceNumber, globalPosition, timestamp, correlationId (ULID), causationId (ULID), userId, eventType (kebab), domainServiceVersion, metadataVersion, extensions. Event storage uses two-document format: `EventEnvelope` contains `EventMetadata` + opaque `payload` (JSON)
**And** the `metadataVersion` field defaults to 1 and is included in every event's metadata JSON, enabling external consumers to detect envelope schema evolution (FR65)
**And** a `EventStoreIdentity` value object encapsulates the canonical tuple `tenant:domain:aggregate-id`
**And** `EventStoreIdentity` provides derived keys for actor IDs, event stream keys, and pub/sub topics per FR26
**And** composite storage keys follow the pattern `{tenant}:{domain}:{aggId}:events:{seq}` per D1

**Given** the Contracts package defines base types
**When** I inspect the command and event contracts
**Then** there is a `CommandEnvelope` with mandatory fields (messageId ULID, aggregateId ULID, commandType kebab `{domain}-{command}-v{ver}`, payload) and optional correlationId (ULID). No tenantId, domain, userId, or causationId — these are server-derived (D15, D16)
**And** there is a base domain event type
**And** there is an `IRejectionEvent` marker interface per D3
**And** events follow past-tense naming convention per enforcement rule 8

**And** a `MessageType` value object parses and validates the `{domain}-{name}-v{ver}` kebab convention (D13), exposing `Domain`, `Name`, and `Version` properties, with factory validation rejecting malformed strings
**And** a `UlidId` value object wraps ULID generation and parsing (D12), used for messageId, aggregateId, correlationId

**Given** all contract types are defined
**When** I run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/`
**Then** all serialization round-trip tests pass for both `EventMetadata` and payload JSON documents independently. `MessageType` parsing tests cover valid conventions, malformed strings, and edge cases. `UlidId` generation and parsing tests pass
**And** identity tuple parsing and key derivation tests pass

---

### Story 1.2: Domain Processor Contract & Event Replay

As a domain service developer,
I want to implement a pure function `(Command, State?) -> List<DomainEvent>` and verify state reconstruction by replaying events,
So that I can write domain logic with zero infrastructure concerns and trust that event replay produces correct state.

**Acceptance Criteria:**

**Given** the Contracts package defines the domain processor interface
**When** I inspect `IDomainProcessor<TCommand, TState>`
**Then** it declares a method with signature `Process(TCommand command, TState? currentState) -> DomainResult`
**And** `DomainResult` contains `AggregateType` (short kebab, e.g., `tenant`) and a `List<EventOutput>` where each `EventOutput` is (event .NET type, event payload). May be empty for no-op commands. **And** the domain service has zero knowledge of metadata fields, ULIDs, timestamps, or correlation chains — it returns only aggregate type, event types, and business payloads
**And** the interface imposes no DAPR, HTTP, or infrastructure dependencies

**Given** an event replay engine exists in the Server package
**When** I call `ReplayEvents(IReadOnlyList<DomainEvent> events, TState initialState)` with a sequence of events
**Then** each event is applied to the state in sequence order via typed `Apply(TEvent)` methods
**And** the final state matches the expected aggregate state
**And** replaying the same events always produces identical state (deterministic)

**Given** the Testing package provides in-memory helpers
**When** I write a unit test for a domain processor
**Then** I can test the pure function without any DAPR runtime or sidecar
**And** `InMemoryEventStream` stores events in memory for assertion
**And** `DomainProcessorTestHarness<TCommand, TState>` provides `Given(events).When(command).ThenExpect(events)` fluent API

**Given** a domain processor returns rejection events
**When** the returned events include a type implementing `IRejectionEvent`
**Then** the rejection events are treated as normal events per D3
**And** they increment the aggregate sequence number
**And** they are persisted to the event stream like any other event

**Given** an event type is unknown during replay
**When** the replay engine encounters an unrecognized event type
**Then** it throws an error (not silently skips) per CRITICAL-1
**And** the error message includes the unknown event type name and sequence number

---

### Story 1.3: Aggregate Actor & Event Persistence

As a system operator,
I want commands routed to aggregate actors that persist events atomically with gapless sequence numbers,
So that the event store guarantees consistency and no partial writes.

**Acceptance Criteria:**

**Given** an aggregate actor is activated for identity `tenant-a:orders:order-123`
**When** the actor receives a command
**Then** the actor rehydrates its state by reading all events from the state store at keys `tenant-a:orders:order-123:events:{1..N}`
**And** reads aggregate metadata from `tenant-a:orders:order-123:metadata`
**And** replays events in sequence order to reconstruct current state

**Given** the actor has rehydrated state and invoked the domain processor
**When** the domain processor returns N events (N >= 1)
**Then** each event is assigned a gapless sequence number continuing from the current aggregate sequence
**And** the EventStore populates all 14 envelope metadata fields (SEC-1): messageId (ULID, EventStore-generated), aggregateId, aggregateType (from DomainResult), tenantId (from JWT), sequenceNumber, globalPosition, timestamp (server clock), correlationId (from command, defaulted to command messageId), causationId (= command messageId), userId (from JWT), eventType (assembled as `{domain}-{event}-v{ver}` from .NET type), domainServiceVersion, metadataVersion
**And** events are stored via `IActorStateManager.SetStateAsync` at keys `{identity}:events:{seq}` as two-document JSON: `{ metadata: {...}, payload: {...} }` per D14
**And** aggregate metadata is updated with the new sequence number and the command messageId is added to the processed messageIds set (for idempotency per FR49)
**And** `SaveStateAsync` commits all state changes atomically (all or nothing per FR16)

**Given** the actor has rehydrated state and invoked the domain processor
**When** the domain processor returns 0 events
**Then** no state changes are written
**And** the command status is updated to `Completed` with event count 0

**Given** two concurrent commands target the same aggregate
**When** the second command attempts to write with a stale ETag
**Then** the write fails with an optimistic concurrency conflict
**And** the actor does not persist any partial events

**Given** a DAPR actor is activated
**When** it receives its first command after activation
**Then** actor state operations use only `IActorStateManager` (enforcement rule 6: never bypass to DaprClient)
**And** event store keys are write-once -- never updated or deleted (enforcement rule 11)

---

### Story 1.4: Command Submission API & Validation

As an API consumer,
I want to submit a command via `POST /api/v1/commands` with structural validation and receive a correlation ID,
So that I can send commands to the event store and track their lifecycle.

**Acceptance Criteria:**

**Given** the CommandApi is running
**When** I send `POST /api/v1/commands` with a valid JSON payload containing messageId (ULID), aggregateId (ULID), commandType (kebab `{domain}-{command}-v{ver}`), and payload
**Then** I receive HTTP `202 Accepted`
**And** the response includes a `correlationId` in the body
**And** the response includes a `Location` header pointing to `/api/v1/commands/{correlationId}/status`
**And** the response includes a `Retry-After: 1` header

**Given** the CommandApi is running
**When** I send `POST /api/v1/commands` with a missing `messageId` field
**Then** I receive HTTP `400 Bad Request`
**And** the response body is `application/problem+json` per RFC 7807 (D5)
**And** the `validationErrors` array includes a field-level error for `messageId`
**And** the error response never references event sourcing, actors, or DAPR internals

**Given** the CommandApi is running
**When** I send `POST /api/v1/commands` with malformed JSON
**Then** I receive HTTP `400 Bad Request` with RFC 7807 problem details
**And** no actor is activated and no events are persisted

**Given** a valid command is submitted
**When** the API generates the correlation ID
**Then** the correlationId defaults to the client-supplied messageId if not provided **And** tenantId is extracted from JWT claims (D15) **And** domain is parsed from commandType prefix via `MessageType` (D13, D15)
**And** the command status is written to `Received` state at the API layer (D2: before actor invocation)
**And** the command is routed to the aggregate actor based on `{tenant}:{domain}:{aggregateId}` where tenant comes from JWT and domain from commandType prefix

**Given** the CommandApi exposes Swagger documentation
**When** I navigate to `/swagger`
**Then** endpoints are grouped logically with example payloads per UX specification

---

### Story 1.5: Command Status Tracking

As an API consumer,
I want to query the processing status of my command using its correlation ID,
So that I can monitor the command lifecycle without understanding event sourcing internals.

**Acceptance Criteria:**

**Given** a command was submitted and received correlation ID `abc-123`
**When** I send `GET /api/v1/commands/abc-123/status`
**Then** I receive HTTP `200 OK` with the current status
**And** the response includes: stage (e.g., `Received`, `Processing`, `EventsStored`, `Completed`), timestamp, and aggregateId
**And** for `Completed` status, the response includes event count
**And** for `Rejected` status, the response includes the rejection event type name
**And** the response uses standard REST terminology -- no event sourcing jargon exposed

**Given** a command completed successfully
**When** I query its status
**Then** the stage is `Completed`
**And** the status reflects the checkpointed state machine: `Received → Processing → EventsStored → EventsPublished → Completed`

**Given** the domain processor returned a rejection event
**When** I query the command status
**Then** the stage is `Rejected`
**And** the rejection event type name is included (e.g., `InsufficientFundsDetected`)

**Given** a correlation ID that does not exist or has expired (24h TTL per CRITICAL-2)
**When** I send `GET /api/v1/commands/{unknown-id}/status`
**Then** I receive HTTP `404 Not Found` with RFC 7807 problem details

**Given** the status endpoint reads from the DAPR state store
**When** the status is queried
**Then** no actor is activated (D2: status reads are decoupled from actor lifecycle)
**And** command status writes in the pipeline are advisory -- they never block the processing pipeline (enforcement rule 12)

---

### Story 1.6: Domain Service Invocation Integration

As a system operator,
I want the aggregate actor to invoke the registered domain service via DAPR, passing command and current state and persisting the returned events,
So that the end-to-end command-to-event pipeline is functional.

**Acceptance Criteria:**

**Given** a domain service is registered in DAPR configuration for tenant `tenant-a` and domain `orders`
**When** the aggregate actor processes a command for `tenant-a:orders:order-123`
**Then** the actor invokes the domain service via `DaprClient.InvokeMethodAsync` (D7)
**And** the request payload includes the command and current aggregate state (or null for first command)
**And** the response is a `DomainResult` containing aggregate type (short kebab) and a list of event outputs (event .NET type + payload). EventStore enriches each with full 14-field metadata, assembles kebab event type, and persists as two-document format

**Given** the domain service returns events successfully
**When** the actor receives the response
**Then** events are persisted atomically per Story 1.3
**And** the command status transitions through `Processing → EventsStored`
**And** the status is updated to `Completed` (pub/sub delivery is Epic 3 scope; status skips `EventsPublished` until then)

**Given** the domain service is unreachable or times out
**When** the DAPR sidecar exhausts retry policies
**Then** the command fails with an infrastructure error
**And** the command status is updated to reflect the failure
**And** the error is logged with correlation ID (enforcement rule 9) but without event payload data (enforcement rule 5)
**And** no partial events are persisted

**Given** the DAPR sidecar is configured with resiliency policies
**When** a transient failure occurs during domain service invocation
**Then** DAPR automatically retries per its configured policy (enforcement rule 4: never add custom retry logic)
**And** the DAPR sidecar call timeout is 5 seconds (enforcement rule 14)

**Given** the complete pipeline is wired (API → Actor → Domain Service → Persistence → Status)
**When** I submit a command via `POST /api/v1/commands` and poll the status endpoint
**Then** the status progresses from `Received` to `Completed`
**And** the events are queryable in the state store under the expected keys
**And** an integration test validates this full flow using DAPR slim init

---

### Story 1.7: MessageType Value Object & ULID Integration

As a domain service developer,
I want a `MessageType` value object that validates `{domain}-{name}-v{ver}` and a `UlidId` type for identity fields,
So that message routing is safe and IDs are lexicographically sortable.

**FRs:** FR2 (validation), D12, D13

**Acceptance Criteria:**

**Given** the Contracts package provides a `MessageType` value object
**When** I call `MessageType.Parse("tenants-create-tenant-v1")`
**Then** it returns domain=`tenants`, name=`create-tenant`, version=`1`
**And** `MessageType.Parse("invalid")` throws with descriptive error
**And** `MessageType.Assemble("tenants", typeof(TenantCreated), 1)` produces `tenants-tenant-created-v1` (PascalCase → kebab conversion)

**Given** the Contracts package provides a `UlidId` value object
**When** I call `UlidId.New()`
**Then** it generates a valid ULID
**And** `UlidId.Parse(string)` validates ULID format and rejects malformed strings
**And** ULIDs sort lexicographically by creation time

**Given** all new types are in the Contracts package
**When** I check dependencies
**Then** the Contracts package has zero dependencies beyond the ULID library
**And** all serialization round-trip tests pass for `MessageType` and `UlidId`

---

**Epic 1 Complete: 7 stories, 15 FRs covered.**

Now moving to **Epic 2: Developer SDK & Experience**.

**FRs covered:** FR22, FR40, FR41, FR42, FR45, FR48

**Relevant Architecture:** Client SDK package, Sample domain service (Counter), Aspire AppHost orchestration, convention-based registration, `EventStoreAggregate` base class.

Proposed stories:

### Story 2.1: Client SDK & Domain Service Registration

**As a** domain service developer,
**I want** to install the EventStore Client NuGet package and register my domain service with zero configuration,
**So that** I can integrate my service with EventStore without manual wiring.

**FRs:** FR22, FR42

---

### Story 2.2: EventStoreAggregate Base Class

**As a** domain service developer,
**I want** a higher-level `EventStoreAggregate` base class with typed `Apply` methods as an alternative to implementing `IDomainProcessor` directly,
**So that** I can use convention-based aggregate patterns with less boilerplate.

**FRs:** FR48

---

### Story 2.3: Sample Counter Domain Service

**As a** domain service developer,
**I want** a working Counter sample domain service as a reference implementation,
**So that** I can learn the pure function programming model from a real example.

**FRs:** FR41

---

### Story 2.4: Aspire AppHost & Single-Command Startup

**As a** developer,
**I want** to start the complete EventStore system (server, sample service, state store, message broker) with a single Aspire command,
**So that** I can develop and test locally with zero manual infrastructure setup.

**FRs:** FR40

---

### Story 2.5: Unit Testing Without DAPR

**As a** domain service developer,
**I want** to run unit tests against my domain processor pure functions without any DAPR runtime dependency,
**So that** I can write fast, reliable tests as part of my normal development flow.

**FRs:** FR45

---

## Epic 2: Developer SDK & Experience

A domain developer can build their own domain service using the SDK, register it, run the complete system with Aspire, reference the sample Counter implementation, and write unit tests without DAPR.

### Story 2.1: Client SDK & Domain Service Registration

As a domain service developer,
I want to install the EventStore Client NuGet package and register my domain service via configuration or convention-based scanning,
So that I can integrate my service with EventStore without manual wiring.

**Acceptance Criteria:**

**Given** a developer has a .NET project
**When** they install `Hexalith.EventStore.Client` via NuGet
**Then** the package installs successfully with no transitive DAPR runtime dependencies
**And** the package exposes an `AddEventStore()` extension method on `IServiceCollection`

**Given** a domain service implements `IDomainProcessor<TCommand, TState>`
**When** `AddEventStore()` is called with assembly scanning enabled
**Then** all domain processor implementations in the assembly are automatically discovered and registered
**And** the registration maps each processor to its command type for routing
**And** zero explicit configuration is required for the quickstart path (FR42)

**Given** a domain service needs explicit tenant/domain mapping
**When** the developer adds a DAPR configuration entry specifying tenant ID, domain name, and service invocation endpoint
**Then** the service is registered for that specific tenant-domain pair (FR22)
**And** explicit configuration takes precedence over convention-based discovery

**Given** both convention-based and explicit registrations exist
**When** the application starts
**Then** convention-based registrations are applied first
**And** explicit DAPR config entries override convention-based registrations for the same tenant-domain pair
**And** startup logs list all registered domain services with their tenant-domain mappings

### Story 2.2: EventStoreAggregate Base Class

As a domain service developer,
I want a higher-level `EventStoreAggregate` base class with typed `Apply` methods,
So that I can use convention-based aggregate patterns with less boilerplate than implementing `IDomainProcessor` directly.

**Acceptance Criteria:**

**Given** the Client package provides `EventStoreAggregate<TState>`
**When** a developer inherits from it
**Then** they implement `Handle(TCommand command, TState? state)` methods returning `List<DomainEvent>`
**And** they implement `Apply(TEvent event, TState state)` methods for each event type
**And** the Handle/Apply methods are discovered via reflection-based fluent convention (no manual registration)

**Given** an aggregate uses convention-based DAPR resource naming
**When** the aggregate type is named `CounterAggregate`
**Then** the derived domain name is `counter` (kebab-case, automatic `Aggregate` suffix stripping per enforcement rule 17)
**And** this domain name becomes the message type prefix for all commands and events: `counter-{name}-v{ver}` per D13
**And** attribute overrides are validated at startup for non-empty, kebab-case compliance

**Given** a developer implements both `IDomainProcessor` and `EventStoreAggregate`
**When** they compare the two approaches
**Then** `EventStoreAggregate` requires less boilerplate (no explicit interface wiring)
**And** both approaches produce identical event streams for the same inputs
**And** both approaches are fully tested with the `DomainProcessorTestHarness` from Story 1.2

**Given** an `EventStoreAggregate` subclass has Handle and Apply methods
**When** a command is processed
**Then** the aggregate follows the same `(Command, State?) -> List<DomainEvent>` contract as `IDomainProcessor`
**And** Apply methods are called during state replay to reconstruct aggregate state

### Story 2.3: Sample Counter Domain Service

As a domain service developer,
I want a working Counter sample domain service as a reference implementation,
So that I can learn the pure function programming model from a real example.

**Acceptance Criteria:**

**Given** the `samples/Hexalith.EventStore.Sample` project exists
**When** I review the sample code
**Then** it implements a Counter aggregate using `EventStoreAggregate<CounterState>` (Story 2.2)
**And** it handles at least: `IncrementCounter`, `DecrementCounter`, `ResetCounter` commands
**And** it produces corresponding events: `CounterIncremented`, `CounterDecremented`, `CounterReset`
**And** it demonstrates a rejection event (e.g., `CounterDecrementRejected` implementing `IRejectionEvent` when decrementing below zero)

**Given** the sample demonstrates the pure function model
**When** I inspect the domain logic
**Then** the Handle methods contain zero infrastructure imports (no DAPR, no HTTP, no DI references)
**And** the Apply methods are pure state transitions
**And** the code includes inline comments explaining the programming model

**Given** the sample has companion tests
**When** I run `dotnet test tests/Hexalith.EventStore.Sample.Tests/`
**Then** all tests pass
**And** tests demonstrate: successful command → events, rejection scenario, state replay from events
**And** tests use the `DomainProcessorTestHarness` from the Testing package
**And** no DAPR runtime is required to run the tests

**And** commands use kebab message types per D13: `counter-increment-counter-v1`, `counter-decrement-counter-v1`, `counter-reset-counter-v1`
**And** the sample demonstrates ultra-thin command submission with messageId (ULID), aggregateId, commandType, payload per D16

### Story 2.4: Aspire AppHost & Single-Command Startup

As a developer,
I want to start the complete EventStore system with a single Aspire command,
So that I can develop and test locally with zero manual infrastructure setup.

**Acceptance Criteria:**

**Given** the AppHost project is configured
**When** I run `dotnet run` on `src/Hexalith.EventStore.AppHost`
**Then** the complete EventStore topology starts: CommandApi, Sample domain service, DAPR sidecars, Redis state store, RabbitMQ message broker
**And** the Aspire dashboard is accessible showing all services and their health
**And** OpenTelemetry traces flow to the Aspire dashboard

**Given** the AppHost uses `CommunityToolkit.Aspire.Hosting.Dapr` (v9.7.0)
**When** the topology starts
**Then** DAPR sidecars are attached to the CommandApi and Sample service
**And** DAPR component configurations (state store, pub/sub) are loaded from local component YAML files
**And** the DAPR placement service is running for actor support

**Given** the full topology is running
**When** I submit a command via `POST http://localhost:{port}/api/v1/commands` with a Counter increment command
**Then** the command flows through the full pipeline: API → Actor → Sample Service → Events Persisted
**And** I can see the traces in the Aspire dashboard spanning all hops
**And** I can query command status and see `Completed`

**Given** a developer is setting up for the first time
**When** they clone the repo and have .NET 10 SDK + Docker installed
**Then** `dotnet run` on AppHost is the only command needed (FR40)
**And** no manual DAPR init, Redis setup, or RabbitMQ configuration is required (Aspire manages containers)

### Story 2.5: Unit Testing Without DAPR

As a domain service developer,
I want to run unit tests against my domain processor pure functions without any DAPR runtime dependency,
So that I can write fast, reliable tests as part of my normal development flow.

**Acceptance Criteria:**

**Given** the Testing package (`Hexalith.EventStore.Testing`) is installed
**When** I write a unit test for my domain processor
**Then** I can test the pure function `(Command, State?) -> List<DomainEvent>` without DAPR sidecar, state store, or message broker
**And** tests run in milliseconds, not seconds
**And** no Docker containers are required

**Given** the Testing package provides test utilities
**When** I inspect the available helpers
**Then** `InMemoryEventStream` stores and retrieves events without DAPR state store
**And** `InMemoryStateManager` implements `IActorStateManager` for actor-level testing
**And** `FakeDomainServiceInvoker` simulates domain service invocation responses
**And** assertion helpers validate event sequences, envelope metadata, and state transitions

**Given** a developer runs Tier 1 tests
**When** they execute `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` or `dotnet test tests/Hexalith.EventStore.Sample.Tests/`
**Then** all tests pass with zero external dependencies (FR45)
**And** tests complete in under 10 seconds on a standard development machine
**And** the Testing package NuGet is available for third-party domain service developers

## Epic 3: Event Distribution & Pub/Sub

Persisted events are automatically published to subscribers using CloudEvents 1.0 with at-least-once delivery and per-tenant-per-domain topics. Failed commands route to dead-letter topics.

### Story 3.1: CloudEvents Publishing with At-Least-Once Delivery

As an event subscriber,
I want persisted events published to me via DAPR pub/sub in CloudEvents 1.0 format with at-least-once delivery,
So that I can react to domain events reliably without coupling to the event store internals.

**Acceptance Criteria:**

**Given** a command has been processed and events persisted in the aggregate actor
**When** the actor completes event persistence (status: `EventsStored`)
**Then** each persisted event is published to DAPR pub/sub
**And** the published message uses CloudEvents 1.0 envelope format (FR17)
**And** the CloudEvents envelope includes: `source`, `type`, `subject`, `id`, `time`, `datacontenttype`, and the event payload as `data`
**And** the command status transitions to `EventsPublished` after successful publish

**Given** DAPR pub/sub is configured with at-least-once delivery semantics
**When** a subscriber acknowledgment fails or times out
**Then** DAPR retries delivery per its configured retry policy (FR18)
**And** subscribers must be idempotent (documented in sample and SDK guidance)

**Given** multiple events are produced by a single command
**When** the actor publishes events
**Then** events are published in sequence order
**And** each event is published as a separate message (not batched)
**And** the command status is updated to `EventsPublished` only after all events are confirmed published

**Given** the full pipeline is operational
**When** I submit a command and it produces events
**Then** the command status reaches `Completed` (final state after `EventsPublished`)
**And** an integration test validates the publish path with a test subscriber receiving the CloudEvents message

### Story 3.2: Per-Tenant-Per-Domain Topic Isolation

As a SaaS operator,
I want events published to per-tenant-per-domain topics,
So that subscribers only receive events for their authorized scope and tenants cannot see each other's events.

**Acceptance Criteria:**

**Given** a command is processed for tenant `acme-corp` and domain `orders`
**When** events are published
**Then** they are published to topic `acme-corp.orders.events` (D6: dot-separated naming)
**And** the topic name is derived from the `EventStoreIdentity` tuple (FR26)

**Given** two tenants (`acme-corp` and `globex`) both have `orders` domains
**When** each processes commands
**Then** `acme-corp` events go to `acme-corp.orders.events`
**And** `globex` events go to `globex.orders.events`
**And** a subscriber to `acme-corp.orders.events` never receives `globex` events (FR19)

**Given** a single tenant has multiple domains (`orders` and `inventory`)
**When** commands are processed in each domain
**Then** events are published to separate topics: `tenant.orders.events` and `tenant.inventory.events`
**And** a subscriber to one topic does not receive events from the other

**Given** the topic naming convention uses dot separation
**When** the system publishes events
**Then** the topic format is compatible across DAPR pub/sub backends (RabbitMQ, Azure Service Bus, Kafka per D6 rationale)

### Story 3.3: Resilient Persistence During Pub/Sub Outage

As a system operator,
I want the event store to continue persisting events when pub/sub is temporarily unavailable,
So that command processing is never blocked by downstream subscriber infrastructure failures.

**Acceptance Criteria:**

**Given** the DAPR pub/sub component is temporarily unavailable
**When** a command is processed and events are persisted
**Then** event persistence succeeds (events are in the state store)
**And** the command status reflects `EventsStored` (not `EventsPublished`)
**And** the command status transitions to `PublishFailed` after DAPR retry exhaustion
**And** no events are lost -- they remain persisted regardless of pub/sub state (FR20)

**Given** the pub/sub component recovers from an outage
**When** the system resumes publishing
**Then** events persisted during the outage are delivered to subscribers via DAPR retry policies (FR20)
**And** event ordering within an aggregate stream is preserved during drain

**Given** event persistence and pub/sub are separate operations
**When** a pub/sub failure occurs after persistence
**Then** the actor does not roll back persisted events
**And** the actor does not duplicate persisted events (enforcement rule 11: write-once keys)
**And** DAPR resiliency policies handle the retry (enforcement rule 4: no custom retry logic)

### Story 3.4: Dead-Letter Routing for Infrastructure Failures

As an operator,
I want failed commands routed to a dead-letter topic with full context,
So that I can investigate and remediate infrastructure failures without losing command data.

**Acceptance Criteria:**

**Given** a command fails due to infrastructure error (network, timeout, domain service unreachable)
**When** DAPR retry policies are exhausted
**Then** the command is routed to a dead-letter topic (FR8)
**And** the dead-letter message includes: full command payload, error details, correlation ID, causation ID, tenant, domain, aggregate ID, and timestamp
**And** the dead-letter message includes enough context to replay the command after root cause is fixed

**Given** a domain processor returns a rejection event (e.g., `InsufficientFundsDetected`)
**When** the rejection event is persisted
**Then** the command is NOT routed to dead-letter (D3: domain rejections are normal events, not error paths)
**And** the command status is `Rejected` with the rejection event type name

**Given** an operator is investigating a dead-letter message
**When** they inspect the message metadata
**Then** the correlation ID links back to the original command submission
**And** the error details indicate the failure type (timeout, unreachable, state store error)
**And** event payload data is never included in the dead-letter error logs (enforcement rule 5)

**Given** the dead-letter topic exists
**When** an infrastructure failure occurs
**Then** the dead-letter message is published to a well-known topic (e.g., `dead-letter.commands`)
**And** the topic is configurable via DAPR component configuration

## Epic 4: Security, Authorization & Multi-Tenant Isolation

The Command API enforces JWT authentication and fine-grained authorization. Multiple tenants operate with proven data path, storage key, and pub/sub topic isolation. DAPR access control enforced between services.

### Story 4.1: JWT Authentication & API Gateway Rejection

As an API consumer,
I want to authenticate with the Command API using a JWT token and have unauthenticated requests rejected at the gateway,
So that only verified identities can interact with the event store.

**Acceptance Criteria:**

**Given** the CommandApi is configured with JWT bearer authentication
**When** I send `POST /api/v1/commands` without a JWT token
**Then** I receive HTTP `401 Unauthorized`
**And** the response is `application/problem+json` per RFC 7807
**And** no actor is activated and no command enters the processing pipeline (FR32)

**Given** I have a valid JWT token
**When** I send `POST /api/v1/commands` with the token in the `Authorization: Bearer` header
**Then** the token is validated for signature, expiry, and issuer (NFR10)
**And** the request proceeds to authorization checks
**And** the JWT token itself is never logged (NFR11)

**Given** a JWT token has expired
**When** I send a request with the expired token
**Then** I receive HTTP `401 Unauthorized`
**And** the response indicates the token is expired
**And** the failed attempt is logged with source IP, attempted tenant, and attempted command type (NFR11)

**Given** a JWT token has an invalid signature or unknown issuer
**When** I send a request with the invalid token
**Then** I receive HTTP `401 Unauthorized`
**And** no stack trace appears in the error response (enforcement rule 13)

**Given** the auth pipeline uses OIDC discovery
**When** the CommandApi starts with an `Authority` URL configured
**Then** it discovers the JWKS endpoint automatically via `.well-known/openid-configuration`
**And** asymmetric key validation is performed via the JWKS endpoint (not symmetric test keys)

### Story 4.2: Claims-Based Authorization & Actor-Level Tenant Validation

As a SaaS operator,
I want command submissions authorized based on JWT claims for tenant, domain, and command type, with a second validation at the actor level,
So that authorization is enforced at two layers and a compromised API gateway cannot bypass tenant isolation.

**Acceptance Criteria:**

**Given** a JWT token contains claims for `tenants: ["tenant-a"]`, `domains: ["orders"]`, `permissions: ["command:submit"]`
**When** I submit a command for `tenant-a`, domain `orders`
**Then** the request is authorized and proceeds to processing (FR31)

**Given** a JWT token contains claims for `tenants: ["tenant-a"]` only
**When** I submit a command for `tenant-b`
**Then** I receive HTTP `403 Forbidden` at the API gateway (FR32)
**And** the command never reaches an actor
**And** the failed authorization is logged with request metadata (NFR11)

**Given** a JWT token contains claims for `domains: ["orders"]` only
**When** I submit a command for domain `inventory`
**Then** I receive HTTP `403 Forbidden`
**And** the rejection reason indicates domain mismatch

**Given** a JWT token contains claims for `permissions: ["command:query"]` only (no `command:submit`)
**When** I submit a command
**Then** I receive HTTP `403 Forbidden`
**And** the rejection reason indicates insufficient permissions

**Given** a command passes API gateway authorization
**When** the aggregate actor receives the command
**Then** the actor validates that the command's tenant matches the authenticated user's authorized tenants (FR33, SEC-2)
**And** tenant validation occurs BEFORE state rehydration (SEC-2: never load state for unauthorized tenant)
**And** if validation fails, the actor rejects the command without loading any state

**Given** the `EventStoreClaimsTransformation` processes JWT claims
**When** it encounters `tenants`, `domains`, and `permissions` claims
**Then** it transforms JSON array claims into the expected format for authorization checks
**And** missing or malformed claims result in authorization failure (not silent pass-through)

### Story 4.3: DAPR Service-to-Service Access Control

As a security engineer,
I want service-to-service communication between EventStore components controlled via DAPR access policies,
So that only authorized services can invoke each other, preventing lateral movement in the event of a component compromise.

**Acceptance Criteria:**

**Given** DAPR access control policies are configured (D4)
**When** the CommandApi app attempts to invoke the aggregate actor service
**Then** the invocation succeeds (CommandApi is in the allow list)

**Given** DAPR access control policies are configured
**When** the CommandApi attempts to invoke a domain service
**Then** the invocation succeeds (CommandApi is allowed to invoke domain services)

**Given** DAPR access control policies are configured
**When** a domain service attempts to invoke the CommandApi or another service directly
**Then** the invocation is blocked (domain services are called, never call out per D4)
**And** the blocked attempt is logged

**Given** the access control policy YAML is defined
**When** I inspect the configuration
**Then** it uses per-app-id allow list with `allowedOperations` (FR34)
**And** mTLS between sidecars is enabled (automatic with DAPR, NFR15)
**And** the policy enforces the intended call graph: CommandApi → actors, CommandApi → domain services, nothing → CommandApi

### Story 4.4: Multi-Domain & Multi-Tenant Processing Proof

As a SaaS operator,
I want proof that multiple domains and tenants can process commands within the same EventStore instance,
So that I can confidently run a multi-tenant SaaS platform on shared infrastructure.

**Acceptance Criteria:**

**Given** two domain services are registered: `orders` and `inventory`
**When** commands are submitted for both domains
**Then** each command is routed to the correct domain service (FR24)
**And** events from `orders` commands are persisted in `orders` event streams
**And** events from `inventory` commands are persisted in `inventory` event streams
**And** the two domains share the same EventStore instance without interference

**Given** two tenants (`tenant-a` and `tenant-b`) share the `orders` domain
**When** each tenant submits commands
**Then** each tenant's commands are processed independently (FR25)
**And** each tenant has its own isolated event streams
**And** `tenant-a` event counts and state are completely independent of `tenant-b`

**Given** a multi-tenant, multi-domain deployment
**When** concurrent commands arrive for different tenant-domain pairs
**Then** commands are processed without cross-tenant or cross-domain interference
**And** an integration test demonstrates at least 2 tenants × 2 domains operating concurrently

### Story 4.5: Data Path, Storage Key & Pub/Sub Topic Isolation Proof

As a security auditor,
I want verified proof that tenant isolation is enforced at data path, storage key, and pub/sub topic layers,
So that I can certify the platform meets multi-tenant security requirements.

**Acceptance Criteria:**

**Given** `tenant-a` and `tenant-b` both process commands in the `orders` domain
**When** I inspect the DAPR state store
**Then** `tenant-a` events are stored under keys `tenant-a:orders:{aggId}:events:{seq}`
**And** `tenant-b` events are stored under keys `tenant-b:orders:{aggId}:events:{seq}`
**And** there is no key that could allow cross-tenant state access (FR28)

**Given** commands are submitted for `tenant-a:orders:order-1`
**When** the aggregate actor processes the command
**Then** the actor ID includes the tenant prefix
**And** the data path ensures the command is never routed to `tenant-b`'s domain service (FR27)
**And** the actor never loads state from a different tenant's key namespace

**Given** events are published for both tenants
**When** I inspect the pub/sub topics
**Then** `tenant-a` events go to `tenant-a.orders.events`
**And** `tenant-b` events go to `tenant-b.orders.events`
**And** a subscriber to `tenant-a.orders.events` never receives `tenant-b` events (FR29)

**Given** multi-tenant isolation must be enforced at all three layers (NFR13)
**When** one isolation layer fails (e.g., actor routing error)
**Then** the remaining layers (storage keys, DAPR policies) prevent data leakage
**And** the failure is logged and detectable

**Given** an E2E test validates the full isolation stack
**When** the test runs with authenticated users for different tenants
**Then** each user can only access their own tenant's data
**And** cross-tenant access attempts are rejected at every layer
**And** the test proves isolation at: actor identity, storage keys, pub/sub topics (NFR13 three-layer proof)

## Epic 5: Resilience & Advanced Processing

The system handles duplicate commands idempotently, optimizes state rehydration with snapshots, supports operator command replay, and detects optimistic concurrency conflicts.

### Story 5.1: Idempotent Duplicate Command Detection

As an API consumer,
I want the system to detect and reject duplicate commands, returning an idempotent success response for already-processed commands,
So that network retries and at-least-once delivery patterns never produce duplicate events.

**Acceptance Criteria:**

**Given** a command with ID `cmd-abc` has been successfully processed for aggregate `tenant-a:orders:order-1`
**When** the same command ID `cmd-abc` is submitted again for the same aggregate
**Then** the system returns an idempotent success response (same correlation ID, same status)
**And** no duplicate events are persisted (FR49)
**And** the aggregate sequence number does not increment

**Given** the aggregate actor tracks processed command IDs
**When** a command arrives
**Then** the actor checks if the command ID exists in its processed set
**And** the processed command ID set is persisted as part of the actor state
**And** the check occurs after state rehydration but before domain service invocation

**Given** two different commands with different command IDs target the same aggregate
**When** both are submitted
**Then** both are processed normally (they are not duplicates)
**And** each produces its own events with distinct sequence numbers

**Given** a command was partially processed (persisted but not published)
**When** the same command ID is retried
**Then** the system recognizes it as already-processed
**And** returns the existing status without re-invoking the domain service

### Story 5.2: Optimistic Concurrency Conflict Handling

As an API consumer,
I want the system to detect optimistic concurrency conflicts and return a clear error,
So that I can handle concurrent writes safely without risking data corruption.

**Acceptance Criteria:**

**Given** two concurrent commands target the same aggregate `tenant-a:orders:order-1`
**When** both actors attempt to write events with the same expected ETag
**Then** one succeeds and the other fails with a concurrency conflict (FR7)
**And** the failed command receives HTTP `409 Conflict` response (NFR26)
**And** no partial events are persisted for the failed command

**Given** a concurrency conflict occurs
**When** the conflict is reported
**Then** the error response is `application/problem+json` per RFC 7807 (D5)
**And** the response includes the correlation ID and aggregate ID
**And** the response does not expose internal ETag values or state store details

**Given** the aggregate metadata key uses ETag-based concurrency (D1)
**When** the actor calls `SaveStateAsync` with a stale ETag
**Then** DAPR state store rejects the write
**And** the actor does not retry automatically (the caller should retry with fresh state)
**And** the command status is updated to reflect the conflict

**Given** a concurrency conflict occurs
**When** the API consumer receives the 409 response
**Then** they can resubmit the command, which will rehydrate fresh state and succeed
**And** events are never silently overwritten (NFR26)

### Story 5.3: Snapshot Creation & Snapshot-Based Rehydration

As a system operator,
I want aggregate state snapshots created at configurable intervals and used for fast rehydration,
So that state reconstruction time remains constant regardless of total event count.

**Acceptance Criteria:**

**Given** snapshot configuration is set to every 100 events (default per enforcement rule 15)
**When** an aggregate reaches event sequence number 100
**Then** the EventStore signals the domain service that a snapshot threshold is reached (FR13)
**And** the domain service produces the snapshot content inline as part of command processing
**And** the snapshot is stored in the DAPR state store at a well-known key (e.g., `{identity}:snapshot:{seq}`)
**And** the snapshot includes the aggregate state and the sequence number it represents

**Given** an aggregate has a snapshot at sequence 100 and events 101-115
**When** the actor is activated and rehydrates state
**Then** it loads the snapshot at sequence 100 first
**And** replays only events 101-115 on top of the snapshot (FR14)
**And** the resulting state is identical to replaying all 115 events from scratch
**And** rehydration time is proportional to tail events (15), not total events (115)

**Given** snapshot intervals are configurable per tenant-domain pair
**When** the operator sets `orders` domain to snapshot every 50 events for `tenant-a`
**Then** snapshots are created at events 50, 100, 150, etc. for `tenant-a:orders` aggregates
**And** the default (100) applies to all other tenant-domain pairs

**Given** an aggregate has 1,000 events with snapshots every 100
**When** the actor rehydrates
**Then** it loads the snapshot at sequence 900 and replays events 901-1000
**And** full state reconstruction completes within 100ms (NFR6)
**And** rehydration time remains constant as event count grows (NFR19)

**Given** a snapshot exists but is corrupted or missing
**When** the actor attempts rehydration
**Then** it falls back to full event replay from sequence 1
**And** logs a warning about the missing/corrupted snapshot
**And** a new snapshot is created at the next threshold

### Story 5.4: Operator Command Replay

As an operator,
I want to replay a previously failed command after fixing the root cause,
So that I can recover from infrastructure failures without manual event manipulation.

**Acceptance Criteria:**

**Given** a command failed and was routed to the dead-letter topic (Epic 3, Story 3.4)
**When** I submit a replay request via `POST /api/v1/commands/replay` with the original command payload and correlation context
**Then** the command is reprocessed through the full pipeline (FR6)
**And** a new correlation ID is generated for the replay attempt
**And** the replay request references the original correlation ID for traceability

**Given** the root cause of the original failure has been fixed
**When** the replayed command is processed
**Then** the domain service processes it normally
**And** events are persisted with the next sequence numbers (not re-using original sequences)
**And** the replay status is trackable via the new correlation ID

**Given** the root cause has NOT been fixed
**When** the replayed command fails again
**Then** it follows the same failure path (dead-letter routing after retry exhaustion)
**And** the replay attempt is logged with both original and new correlation IDs

**Given** an operator replays a command for an aggregate that has progressed since the failure
**When** the aggregate has new events since the original failure
**Then** the replayed command is processed against the current aggregate state
**And** the domain service decides whether the command is still valid given the new state
**And** if the domain rejects it, a rejection event is persisted normally (D3)

### Story 5.5: Aggregate Tombstoning & Lifecycle Termination

As a system operator,
I want aggregates to support terminal states that reject subsequent commands,
So that completed or cancelled aggregates are permanently sealed while their event history remains intact.

**FRs:** FR66

**Acceptance Criteria:**

**Given** a domain processor returns an event implementing `ITerminalEvent` marker interface
**When** the event is persisted to the aggregate's event stream
**Then** the aggregate metadata is updated to mark the aggregate as terminated
**And** the terminal event increments the sequence number like any other event

**Given** an aggregate is marked as terminated
**When** a new command targets that aggregate
**Then** the command is rejected with a domain rejection event (e.g., `AggregateTerminated`)
**And** no domain service invocation occurs
**And** the rejection event is persisted and the command status reflects `Rejected`

**Given** an aggregate is terminated
**When** its event stream is replayed
**Then** all events including the terminal event are replayed correctly
**And** the aggregate state reflects the terminated status

### Story 5.6: Per-Aggregate Backpressure

As a system operator,
I want the system to apply backpressure when an aggregate's command queue exceeds a configurable depth,
So that saga storms and head-of-line blocking cascades are prevented without affecting other aggregates.

**FRs:** FR67

**Acceptance Criteria:**

**Given** the backpressure threshold is configured at 100 pending commands per aggregate (default)
**When** an aggregate's pending command queue reaches the threshold
**Then** subsequent commands targeting that aggregate receive HTTP 429 Too Many Requests
**And** the response includes a `Retry-After` header with a configurable delay
**And** commands targeting other aggregates are unaffected

**Given** the backpressure threshold is configurable per tenant-domain pair via DAPR config store
**When** an operator changes the threshold
**Then** the new threshold takes effect without system restart (NFR20)

**Given** backpressure is active on an aggregate
**When** the pending command queue drains below the threshold
**Then** new commands are accepted normally
**And** no manual intervention is required to resume processing

## Epic 6: Observability & Operations

Full pipeline visibility with distributed OpenTelemetry traces, structured logs with correlation/causation IDs, dead-letter traceability, and health/readiness endpoints.

### Story 6.1: Distributed OpenTelemetry Traces

As an operator,
I want distributed OpenTelemetry traces spanning the full command lifecycle,
So that I can visualize the entire command-to-event pipeline in standard tracing tools.

**Acceptance Criteria:**

**Given** the CommandApi and Server are instrumented with OpenTelemetry
**When** a command is submitted and processed end-to-end
**Then** a single distributed trace spans the full lifecycle: API received → command validated → actor activated → state rehydrated → domain service invoked → events persisted → events published → status updated (FR35)
**And** each stage appears as a separate span with timing information
**And** the trace ID propagates across DAPR service invocation boundaries

**Given** a command produces multiple events
**When** events are persisted and published
**Then** each event persistence and publish operation appears as a child span
**And** the spans include event type and sequence number as attributes (but never event payload data per enforcement rule 5)

**Given** a command fails at any stage
**When** the failure occurs
**Then** the span for that stage is marked with error status
**And** the error details are captured in the span (without payload data)
**And** the trace remains complete up to the failure point for diagnostic purposes

**Given** OpenTelemetry is configured via ServiceDefaults
**When** the system starts
**Then** traces are exportable to any OTLP-compatible collector (NFR31)
**And** the Aspire dashboard displays traces during local development
**And** the same instrumentation works with Jaeger, Grafana/Tempo, or other OTLP backends

**Given** every span and trace includes correlation context
**When** I search by correlation ID in the tracing tool
**Then** I find the complete trace for that command (enforcement rule 9)

### Story 6.2: Structured Logging with Correlation IDs & Dead-Letter Traceability

As an operator,
I want structured logs with correlation and causation IDs at every pipeline stage and the ability to trace dead-letter commands back to their origin,
So that I can diagnose failures quickly and understand the full causal chain.

**Acceptance Criteria:**

**Given** the command processing pipeline emits structured logs
**When** a command is processed
**Then** every log entry includes `correlationId` and `causationId` fields (FR36, enforcement rule 9)
**And** logs are emitted at each stage: received, validating, routing, processing, persisting, publishing, completing
**And** log entries use structured format (key-value pairs, not unstructured strings)

**Given** event payload data must never appear in logs (enforcement rule 5, NFR12)
**When** any component logs
**Then** only event metadata (envelope fields) appears in log output
**And** command payloads are never logged
**And** JWT tokens are never logged (NFR11)

**Given** a command was routed to the dead-letter topic
**When** an operator investigates the failure
**Then** they can search logs by the dead-letter message's correlation ID
**And** the logs show the full causal chain: original submission → processing attempt → failure reason (FR37)
**And** the dead-letter message itself contains the correlation ID for cross-referencing

**Given** failed authentication or authorization attempts occur
**When** the system logs the failure
**Then** the log includes source IP, attempted tenant, and attempted command type (NFR11)
**And** the log does not include the JWT token or its claims values
**And** the log does not include stack traces in production (enforcement rule 13)

### Story 6.3: Health Check & Readiness Endpoints

As a platform operator,
I want health and readiness check endpoints indicating dependency status,
So that load balancers and orchestrators can route traffic only to healthy instances.

**Acceptance Criteria:**

**Given** the CommandApi exposes health endpoints
**When** I call `GET /healthz` (or `/health`)
**Then** I receive HTTP `200 OK` when the system is healthy
**And** the response indicates status of: DAPR sidecar connectivity, state store connectivity, pub/sub connectivity (FR38)
**And** I receive HTTP `503 Service Unavailable` when any critical dependency is unhealthy

**Given** the CommandApi exposes a readiness endpoint
**When** I call `GET /readyz` (or `/ready`)
**Then** I receive HTTP `200 OK` when the system is ready to accept commands (FR39)
**And** readiness requires: DAPR sidecar healthy, state store reachable, actor placement service available
**And** I receive HTTP `503 Service Unavailable` when the system cannot process commands

**Given** the health check endpoints are standard ASP.NET Core health checks
**When** configured
**Then** they integrate with Kubernetes liveness/readiness probes
**And** they integrate with Aspire's health monitoring dashboard
**And** they integrate with Azure Container Apps health probes

**Given** a dependency becomes unhealthy after startup
**When** the health check detects the failure
**Then** the health endpoint returns `503` within the next check interval
**And** the readiness endpoint returns `503` to stop new traffic routing
**And** existing in-flight commands are not interrupted (graceful degradation)

## Epic 7: Production Deployment & CI/CD

EventStore deploys to production via Aspire publishers with GitHub Actions CI/CD pipeline, integration tests with DAPR containers, and E2E contract tests across the full topology.

### Story 7.1: Environment-Specific Deployment via DAPR Configuration

As a DevOps engineer,
I want to deploy EventStore to different environments by changing only DAPR component configuration files,
So that I can target development, staging, and production without any application code changes.

**Acceptance Criteria:**

**Given** the EventStore application is built as a single set of container images
**When** I deploy to a development environment with Redis state store and RabbitMQ pub/sub
**Then** I provide DAPR component YAML files pointing to Redis and RabbitMQ
**And** the application runs without code changes, recompilation, or redeployment (FR43, NFR29)

**Given** the same application images
**When** I deploy to a production environment with PostgreSQL state store and Azure Service Bus pub/sub
**Then** I provide DAPR component YAML files pointing to PostgreSQL and Azure Service Bus
**And** the application functions identically (NFR27, NFR28)
**And** zero application code changes are required

**Given** environment-specific DAPR component configurations
**When** I inspect the configuration files
**Then** the repository includes reference configurations for: Redis + RabbitMQ (dev), PostgreSQL + RabbitMQ (staging), PostgreSQL + Azure Service Bus (production)
**And** each configuration set is a complete, deployable set of DAPR component YAMLs
**And** secrets (connection strings, credentials) are referenced via DAPR secret store, not inline (NFR14)

**Given** a new tenant needs to be added to an existing deployment
**When** the operator updates the DAPR config store with the new tenant's configuration
**Then** the new tenant is available without system restart or downtime (NFR20)

### Story 7.2: Aspire Publisher Deployment Manifests

As a DevOps engineer,
I want to generate deployment manifests for Docker Compose, Kubernetes, and Azure Container Apps via Aspire publishers,
So that I can deploy EventStore to my target platform without custom deployment scripts.

**Acceptance Criteria:**

**Given** the AppHost project is configured with Aspire publishers
**When** I run the Docker Compose publisher
**Then** a valid `docker-compose.yml` is generated with all services, DAPR sidecars, state store, and pub/sub (FR44)
**And** the generated manifest can be deployed with `docker compose up`
**And** the deployed system is functional end-to-end

**Given** the AppHost project is configured
**When** I run the Kubernetes publisher
**Then** valid Kubernetes manifests are generated (Deployments, Services, ConfigMaps, DAPR annotations)
**And** the manifests can be applied with `kubectl apply` to a DAPR-enabled cluster
**And** DAPR component configurations are generated as Kubernetes CRDs

**Given** the AppHost project is configured
**When** I run the Azure Container Apps publisher
**Then** valid ACA deployment manifests are generated
**And** the manifests integrate with DAPR-enabled Azure Container Apps environment (NFR32)

**Given** any generated deployment manifest
**When** inspected for security
**Then** no secrets are embedded in the manifest (NFR14)
**And** secret references point to the platform's secret management (Docker secrets, K8s secrets, Azure Key Vault)

### Story 7.3: Integration Tests with DAPR Test Containers

As a developer,
I want to run integration tests against the actor processing pipeline using DAPR test containers,
So that I can validate server-side behavior with real DAPR runtime without a full infrastructure setup.

**Acceptance Criteria:**

**Given** `tests/Hexalith.EventStore.Server.Tests/` exists as Tier 2 test project
**When** I run `dotnet test tests/Hexalith.EventStore.Server.Tests/`
**Then** DAPR is initialized in slim mode (`dapr init --slim`) for the test run
**And** tests execute against real DAPR actor runtime with in-memory state store (FR46)
**And** tests validate: actor activation, state rehydration, event persistence, sequence numbering, atomic writes

**Given** the integration tests use DAPR slim mode
**When** tests run
**Then** no Docker containers are required (slim mode uses local processes)
**And** tests complete within a reasonable time (under 60 seconds for the full suite)
**And** tests are isolated from each other (each test uses unique tenant/aggregate IDs)

**Given** integration tests cover the actor processing pipeline
**When** I review the test coverage
**Then** tests validate: successful command processing, rejection event handling, concurrency conflict detection, status tracking updates
**And** tests use the Testing package utilities (`InMemoryStateManager`, `FakeDomainServiceInvoker`) where appropriate

**Given** integration tests are Tier 2 in the test strategy
**When** CI runs
**Then** Tier 2 tests run after Tier 1 (unit) tests pass
**And** DAPR slim init is performed as a CI step before Tier 2 execution

### Story 7.4: E2E Contract Tests with Full Aspire Topology

As a developer,
I want end-to-end contract tests validating the full command lifecycle across the complete Aspire topology,
So that I can verify the system works as an integrated whole before release.

**Acceptance Criteria:**

**Given** `tests/Hexalith.EventStore.IntegrationTests/` exists as Tier 3 test project
**When** I run `dotnet test tests/Hexalith.EventStore.IntegrationTests/`
**Then** the full Aspire topology starts: CommandApi, Sample service, DAPR sidecars, Redis, RabbitMQ (FR47)
**And** tests validate the complete command lifecycle: submit → process → persist → publish → status complete

**Given** D11 specifies Keycloak for E2E security testing
**When** E2E security tests run
**Then** `Aspire.Hosting.Keycloak` starts a Keycloak instance with the `hexalith-realm.json` realm export
**And** test users (`admin-user`, `tenant-a-user`, `tenant-b-user`, `readonly-user`, `no-tenant-user`) are pre-configured
**And** tests acquire real OIDC tokens via Resource Owner Password Grant
**And** tokens are validated through the full auth pipeline (OIDC discovery, JWKS, asymmetric key validation)
**And** security tests use `[Trait("Category", "E2E")]` to separate from fast symmetric-key tests (enforcement rule 16)

**Given** E2E contract tests cover cross-cutting scenarios
**When** I review the test suite
**Then** tests validate: multi-tenant isolation proof, cross-tenant access rejection, dead-letter routing, pub/sub event delivery, status tracking across the full lifecycle
**And** each test is self-contained and does not depend on other test execution order

**Given** E2E tests are Tier 3 in the test strategy
**When** CI runs
**Then** Tier 3 tests are optional (require full DAPR init + Docker)
**And** they run after Tier 1 and Tier 2 pass
**And** the Keycloak realm export is a living artifact updated as security stories progress

### Story 7.5: GitHub Actions CI/CD Pipeline with MinVer & NuGet Publishing

As a maintainer,
I want a GitHub Actions CI/CD pipeline that builds, tests, and publishes NuGet packages on release tags,
So that every PR is validated and releases are automated with zero manual steps.

**Acceptance Criteria:**

**Given** a PR is opened or pushed to `main`
**When** the CI pipeline runs
**Then** it executes: `dotnet restore`, `dotnet build --configuration Release`, Tier 1 tests, Tier 2 tests (with DAPR slim init)
**And** optionally Tier 3 tests (if Docker is available in the runner)
**And** the pipeline fails if any test fails or any warning is treated as error

**Given** a Git tag matching `v*` (e.g., `v1.0.0`) is pushed
**When** the release pipeline runs
**Then** it executes the full test suite
**And** runs `dotnet pack` to produce NuGet packages
**And** validates that exactly 5 NuGet packages are produced: Contracts, Client, Server, Testing, Aspire
**And** pushes all 5 packages to NuGet.org (D10)

**Given** MinVer is configured for versioning (D9)
**When** a tag `v1.0.0` exists on a commit
**Then** all 5 packages get version `1.0.0`
**And** pre-release versions are auto-calculated from tag + commit height (e.g., `1.0.1-alpha.3`)
**And** all packages share the same version (monorepo single-version strategy)

**Given** the CI pipeline definition
**When** I inspect it
**Then** it uses standard GitHub Actions .NET actions
**And** secrets (NuGet API key) are stored in GitHub Secrets, not in code (NFR14)
**And** the pipeline is defined in `.github/workflows/` with separate jobs for build, test, and release

## Epic 8: Query Pipeline & Real-Time Updates (v2)

API consumers query projections efficiently with self-routing ETag-based caching and in-memory query actor pages. Blazor clients receive real-time SignalR push notifications on projection changes.

### Story 8.1: Query Contract Library & IQueryResponse Compile-Time Enforcement

As a domain service developer,
I want a typed query contract library that enforces mandatory metadata at compile time,
So that query routing works correctly and missing projection type information is caught before runtime.

**Acceptance Criteria:**

**Given** a new NuGet package `Hexalith.EventStore.Contracts` (extended) defines query contracts
**When** I create a query type
**Then** mandatory fields are enforced: Domain, QueryType, TenantId as typed static members (FR57)
**And** optional fields (EntityId) are available but not required
**And** the query contract is the single source of truth for query routing

**Given** a microservice implements `IQueryResponse<T>`
**When** it returns a query response
**Then** the response must include a non-empty `ProjectionType` field enforced at compile time (FR62)
**And** an empty or whitespace-only `ProjectionType` is treated as an error by the query actor
**And** the compile-time enforcement prevents silent caching degradation

**Given** projection type names are used in self-routing ETags
**When** documentation guidance is provided
**Then** it recommends short projection type names (e.g., `OrderList` not fully qualified type names) (FR64)
**And** guidance explains that projection type names are base64url-encoded and longer names produce longer ETag tokens

### Story 8.2: Query Actor Routing & Runtime Projection Type Discovery

As an API consumer,
I want my queries routed to the correct query actor based on query metadata,
So that each query is handled by the right cached actor instance.

**Acceptance Criteria:**

**Given** a query with EntityId `entity-1`
**When** the system routes the query
**Then** it routes to actor `{QueryType}-{TenantId}-{entity-1}` (tier 1 routing) (FR50)

**Given** a query without EntityId but with a non-empty payload
**When** the system routes the query
**Then** it computes a truncated SHA256 base64url hash (11 characters) of the serialized payload
**And** routes to actor `{QueryType}-{TenantId}-{Checksum}` (tier 2 routing) (FR50)
**And** serialization non-determinism producing different checksums for identical queries is an accepted trade-off

**Given** a query without EntityId and with an empty payload
**When** the system routes the query
**Then** it routes to actor `{QueryType}-{TenantId}` (tier 3 routing) (FR50)

**Given** a query actor receives its first request (cold call)
**When** it forwards the query to the microservice
**Then** it receives `IQueryResponse<T>` containing data and ProjectionType
**And** it stores the ProjectionType mapping in memory for subsequent ETag actor lookups (FR63)
**And** the mapping resets on actor deactivation (DAPR idle timeout)
**And** the next cold call after deactivation re-learns the mapping from the microservice

### Story 8.3: ETag Actor & Self-Routing ETag Encoding

As an API consumer,
I want efficient ETag-based cache validation that routes to the correct projection without server-side lookup tables,
So that cache checks are sub-5ms and the system scales without centralized routing state.

**Acceptance Criteria:**

**Given** a projection type `OrderList` and tenant `tenant-a`
**When** the ETag actor `OrderList-tenant-a` is created
**Then** it stores a self-routing ETag representing the current projection version (FR51)
**And** the ETag is regenerated (new GUID) on every projection change notification

**Given** the self-routing ETag format
**When** an ETag is generated
**Then** it follows the format `{base64url(projectionType)}.{guid}` (FR61)
**And** ETags are wrapped in quotes in HTTP response headers per RFC 7232
**And** the base64url encoding of `OrderList` produces a compact, URL-safe token

**Given** a client sends an `If-None-Match` header with a self-routing ETag
**When** the query endpoint decodes it
**Then** it extracts the projection type from the base64url prefix
**And** uses the projection type to route to the correct ETag actor
**And** compares the GUID portion against the current ETag

**Given** an ETag is malformed, undecodable, or references a non-existent projection type
**When** the query endpoint attempts to decode it
**Then** it treats the ETag as a cache miss (FR61)
**And** routes the query to the query actor (safe degradation by construction)
**And** never returns an error for bad ETags

**Given** ETag actor performance requirements
**When** the ETag actor is warm (already activated)
**Then** the ETag check completes within 5ms at p99 (NFR35)

### Story 8.4: Projection Change Notification & Cache Invalidation

As a domain service developer,
I want to notify EventStore when a projection changes so cached query results are invalidated,
So that API consumers always get fresh data after projection updates.

**Acceptance Criteria:**

**Given** the Client NuGet package provides a notification helper
**When** a domain service calls `NotifyProjectionChanged(projectionType, tenantId, entityId?)` (FR52)
**Then** the notification is sent to EventStore via the configured transport (DAPR pub/sub by default, or direct service invocation)
**And** the transport is selectable by configuration without code changes

**Given** EventStore receives a projection change notification for `OrderList` and `tenant-a`
**When** the notification is processed
**Then** the ETag actor `OrderList-tenant-a` regenerates its ETag (new GUID)
**And** all cached query results for `OrderList` + `tenant-a` are invalidated (FR58)
**And** invalidation is coarse: all filters/variants for that projection+tenant are invalidated

**Given** a notification includes an optional `entityId`
**When** the notification is processed
**Then** the system still invalidates at the projection+tenant level (coarse model per FR58)
**And** the entityId is available for future fine-grained invalidation but not used in v2

**Given** coarse invalidation trades unnecessary refreshes for simplicity
**When** a single entity's projection changes
**Then** all query actors for that projection+tenant will re-query on their next request
**And** this is an accepted trade-off: the EventStore does not understand projection schemas (opacity principle)

### Story 8.5: ETag Pre-Check & Query Actor In-Memory Cache

As an API consumer,
I want a two-gate caching system that returns HTTP 304 for unchanged data without activating query actors,
So that repeated queries for unchanged data are extremely fast and resource-efficient.

**Acceptance Criteria:**

**Given** a client sends a query with `If-None-Match` header containing a valid self-routing ETag
**When** the query endpoint decodes the ETag and calls the corresponding ETag actor
**Then** if the GUID portion matches the current ETag, the endpoint returns HTTP `304 Not Modified` (FR53)
**And** no query actor is activated (first gate optimization)
**And** this completes within 5ms at p99 for warm ETag actors (NFR35)

**Given** the client's ETag is stale (GUID mismatch) or missing
**When** the query is routed to the query actor
**Then** the query actor checks its in-memory cached data (second gate) (FR54)
**And** if the query actor has cached data and its learned projection type's ETag matches, it returns cached data
**And** cache hit completes within 10ms at p99 (NFR36)

**Given** the query actor has stale cached data (ETag mismatch)
**When** it detects the mismatch
**Then** it re-queries the microservice via domain service invocation
**And** caches the new response data and projection type mapping
**And** returns the fresh data with an updated self-routing ETag header
**And** cache miss completes within 200ms at p99 (NFR37)

**Given** a query actor has no cached data (cold call after activation)
**When** it receives a query
**Then** it forwards to the microservice, caches the response, learns the projection type (FR54, FR63)
**And** returns the result with a self-routing ETag header
**And** subsequent requests use the cached data until ETag invalidation

**Given** a query actor is deactivated (DAPR idle timeout)
**When** it is re-activated by a new request
**Then** its in-memory cache and projection type mapping are reset
**And** the next request is a cold call to the microservice

### Story 8.6: SignalR Hub & Real-Time "Changed" Broadcast

As a Blazor client developer,
I want real-time push notifications when projections change,
So that my UI can update without polling and users see fresh data immediately.

**Acceptance Criteria:**

**Given** the EventStore server hosts a SignalR hub
**When** the hub is configured
**Then** it runs inside the EventStore server process (FR56)
**And** a Redis backplane is configured for multi-instance SignalR message distribution
**And** the DAPR-managed Redis instance may be reused where supported

**Given** a projection's ETag is regenerated (due to a change notification)
**When** the ETag actor updates
**Then** it broadcasts a signal-only "changed" message to connected SignalR clients (FR55)
**And** the message contains only the projection type and tenant — no data payload
**And** clients are grouped by ETag actor ID (`{ProjectionType}-{TenantId}`)

**Given** a Blazor client connects to the SignalR hub
**When** it subscribes to `OrderList-tenant-a` group
**Then** it receives "changed" signals whenever the `OrderList` projection for `tenant-a` is updated
**And** the signal delivery completes within 100ms at p99 from ETag regeneration to client receipt (NFR38)

**Given** the query pipeline handles high concurrency
**When** 1,000 concurrent queries per second hit the system
**Then** the pipeline serves them without exceeding latency targets (NFR39)
**And** SignalR broadcasts scale via the Redis backplane across multiple server instances

### Story 8.7: SignalR Client Auto-Rejoin on Connection Recovery

As a Blazor client developer,
I want the SignalR client to automatically rejoin groups after connection drops,
So that real-time notifications resume seamlessly without manual intervention.

**Acceptance Criteria:**

**Given** the Client NuGet package provides a SignalR client helper
**When** a Blazor Server circuit reconnects after a network interruption
**Then** the helper automatically rejoins all previously subscribed SignalR groups (FR59)
**And** no developer intervention is required — the helper manages group membership

**Given** a WebSocket connection drops and reconnects
**When** the SignalR client re-establishes the connection
**Then** group subscriptions are restored within one reconnection cycle
**And** any "changed" signals missed during the outage are handled by the client re-querying with its last known ETag (natural cache miss recovery)

**Given** a Blazor Server circuit fully disconnects and a new circuit is created
**When** the client reconnects
**Then** the helper re-subscribes to the same groups
**And** the UX is seamless — the user does not need to refresh the page

**Given** the auto-rejoin helper
**When** I inspect the NuGet package API
**Then** the helper is configurable (which groups to rejoin)
**And** it provides events/callbacks for connection state changes (connected, reconnecting, disconnected)
**And** it handles all reconnection scenarios: circuit reconnection, WebSocket drops, network interruption (FR59)

### Story 8.8: Sample Blazor UI with Three Refresh Reference Patterns

As a Blazor client developer,
I want at least three reference patterns for handling the SignalR "changed" signal in Blazor UI components,
So that I can choose the best UX approach for my application.

**Acceptance Criteria:**

**Given** the sample application includes a Blazor UI section
**When** I review the reference patterns
**Then** at least 3 patterns are implemented (FR60):

**Pattern 1: Toast Notification (Manual Refresh)**
**Given** a "changed" signal is received for a subscribed projection
**When** the component handles the signal
**Then** a toast notification appears informing the user data has changed
**And** the user can click to manually refresh the data
**And** no automatic data reload occurs

**Pattern 2: Automatic Silent Reload**
**Given** a "changed" signal is received
**When** the component handles the signal
**Then** it automatically re-queries the projection using the query API
**And** the UI updates with fresh data without user interaction
**And** a subtle loading indicator shows during the fetch

**Pattern 3: Selective Component Refresh**
**Given** a "changed" signal is received for a specific projection
**When** the component handles the signal
**Then** only the UI component bound to that specific projection refreshes
**And** other components on the page remain untouched
**And** the refresh targets only the affected projection type

**Given** all three patterns are implemented in the sample
**When** I review the code
**Then** each pattern is self-contained in a separate Blazor component
**And** each includes inline comments explaining when to use the pattern (trade-offs)
**And** the sample uses Blazor Fluent UI 4.x components per the UX specification
