---
stepsCompleted:
    - step-01-validate-prerequisites
    - step-02-design-epics
    - step-03-create-stories
    - step-04-final-validation
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

**Event Management (8 FRs):**

- FR9: The system can persist events in an append-only, immutable event store where events are never modified or deleted after persistence
- FR10: The system can assign strictly ordered, gapless sequence numbers to events within a single aggregate stream. Cross-aggregate event ordering is explicitly not guaranteed
- FR11: The system can wrap each event in an 11-field metadata envelope (aggregate ID, tenant, domain, sequence, timestamp, correlation ID, causation ID, user identity, domain service version, event type, serialization format) plus opaque payload and extension metadata bag
- FR12: The system can reconstruct aggregate state by replaying all events in an aggregate's stream from sequence 1 to current
- FR13: The system can create snapshots of aggregate state at administrator-configured event count intervals (default: every 100 events, configurable per tenant-domain pair)
- FR14: The system can reconstruct aggregate state from the latest snapshot plus subsequent events, producing identical state to full replay
- FR15: The system can store events using a composite key strategy that includes tenant, domain, and aggregate identity for isolation
- FR16: The system can enforce atomic event writes -- a command produces 0 or N events as a single transaction, never a partial subset

**Event Distribution (4 FRs):**

- FR17: The system can publish persisted events to subscribers via a pub/sub mechanism using CloudEvents 1.0 envelope format
- FR18: The system can deliver events to subscribers with at-least-once delivery guarantee
- FR19: The system can publish events to per-tenant-per-domain topics, ensuring subscribers only receive events for their authorized scope
- FR20: The system can continue persisting events when the pub/sub system is temporarily unavailable, draining the backlog on recovery

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

- FR40: A developer can start the complete EventStore system (server, sample domain service, state store, message broker, OpenTelemetry) with a single Aspire command
- FR41: A developer can reference a sample domain service implementation as a working example of the pure function programming model
- FR42: A developer can install EventStore client packages via NuGet to build and register domain services, with zero-configuration quickstart via convention-based `AddEventStore()` registration
- FR43: A DevOps engineer can deploy EventStore to different environments by changing only DAPR component configuration files with zero application code changes
- FR44: A DevOps engineer can generate deployment manifests for Docker Compose, Kubernetes, or Azure Container Apps via Aspire publishers
- FR45: A developer can run unit tests against domain service pure functions without any DAPR runtime dependency
- FR46: A developer can run integration tests against the actor processing pipeline using DAPR test containers
- FR47: A developer can run end-to-end contract tests validating the full command lifecycle across the complete Aspire topology
- FR48: A domain service developer can implement a domain aggregate by inheriting from EventStoreAggregate with typed Apply methods, as a higher-level alternative to implementing IDomainProcessor directly

**Query Pipeline & Projection Caching -- v2 (11 FRs):**

- FR50: The system can route incoming query messages to query actors using a 3-tier routing model
- FR51: The system can maintain one ETag actor per `{ProjectionType}-{TenantId}` storing a GUID representing the current projection version
- FR52: A domain service developer can notify EventStore of a projection change by calling `NotifyProjectionChanged(projectionType, tenantId, entityId?)`
- FR53: The query REST endpoint can perform an ETag pre-check by calling the ETag actor before routing to the query actor -- returning HTTP 304 on match
- FR54: A query actor can serve as an in-memory page cache comparing its cached ETag against the current ETag actor value
- FR55: The system can broadcast a signal-only "changed" message to connected SignalR clients when a projection's ETag is regenerated
- FR56: The system can host a SignalR hub inside the EventStore server using a Redis backplane
- FR57: A query contract library (NuGet) can define mandatory query metadata fields as typed static members
- FR58: The system can invalidate all cached query results for a projection+tenant pair on any projection change notification (coarse invalidation)
- FR59: The SignalR client helper (NuGet) can automatically rejoin SignalR groups on connection recovery
- FR60: The EventStore documentation and sample application can provide at least 3 reference patterns for handling the SignalR "changed" signal in Blazor UI components

### NonFunctional Requirements

**Performance (8 NFRs):**

- NFR1: Command submission via REST API must complete (202 Accepted) within 50ms at p99
- NFR2: End-to-end command lifecycle (API receipt to event published) must complete within 200ms at p99
- NFR3: Event append latency (actor persist to state store confirmation) must be under 10ms at p99
- NFR4: Actor cold activation with state rehydration (snapshot + events) must complete within 50ms at p99
- NFR5: Pub/sub delivery (event persistence to subscriber delivery) must complete within 50ms at p99
- NFR6: Full aggregate state reconstruction from 1,000 events must complete within 100ms
- NFR7: The system must support at least 100 concurrent command submissions per second per instance
- NFR8: DAPR sidecar overhead per building block call must not exceed 2ms at p99

**Security (7 NFRs):**

- NFR9: All communication between API consumers and the Command API must be encrypted via TLS 1.2+
- NFR10: JWT tokens must be validated for signature, expiry, and issuer on every request
- NFR11: Failed authentication/authorization attempts must be logged with request metadata without logging the JWT token
- NFR12: Event payload data must never appear in log output; only event metadata may be logged
- NFR13: Multi-tenant data isolation must be enforced at all three layers (actor identity, DAPR policies, command metadata)
- NFR14: Secrets must never be stored in application code or configuration files committed to source control
- NFR15: Service-to-service communication between EventStore components must be authenticated via DAPR access control policies

**Scalability (5 NFRs):**

- NFR16: Horizontal scaling via adding EventStore server replicas with DAPR actor placement distribution
- NFR17: Support at least 10,000 active aggregates per EventStore instance
- NFR18: Support at least 10 tenants per instance with full isolation
- NFR19: Event stream growth bounded by snapshot strategy -- constant rehydration time
- NFR20: Adding new tenant or domain must not require system restart -- dynamic config via DAPR config store

**Reliability (6 NFRs):**

- NFR21: 99.9%+ availability with HA DAPR control plane and multi-replica deployment
- NFR22: Zero events lost under any tested failure scenario
- NFR23: After state store recovery, resume processing from last checkpoint with deterministic replay
- NFR24: After pub/sub recovery, all events persisted during outage delivered to subscribers
- NFR25: Actor crash after event persistence but before pub/sub delivery must not result in duplicate event persistence
- NFR26: Optimistic concurrency conflicts must be detected and reported (409 Conflict)

**Integration (6 NFRs):**

- NFR27: Function correctly with any DAPR-compatible state store supporting key-value ops and ETag optimistic concurrency
- NFR28: Function correctly with any DAPR-compatible pub/sub supporting CloudEvents 1.0 and at-least-once delivery
- NFR29: Backend switch requires only DAPR component YAML changes -- zero application code changes
- NFR30: Domain services invocable via DAPR service invocation over HTTP
- NFR31: OpenTelemetry exportable to any OTLP-compatible collector
- NFR32: Deployable via Aspire publishers to Docker Compose, Kubernetes, and Azure Container Apps

**Rate Limiting (2 NFRs):**

- NFR33: Per-tenant rate limiting with configurable threshold (default: 1,000 commands/min/tenant), returning 429 with Retry-After
- NFR34: Per-consumer rate limiting with configurable threshold (default: 100 commands/sec/consumer), returning 429 with Retry-After

**Query Pipeline Performance -- v2 (5 NFRs):**

- NFR35: ETag pre-check at query endpoint must complete within 5ms at p99 for warm actors
- NFR36: Query actor cache hit must complete within 10ms at p99
- NFR37: Query actor cache miss must complete within 200ms at p99
- NFR38: SignalR "changed" signal delivery must complete within 100ms at p99
- NFR39: Query pipeline must support at least 1,000 concurrent query requests per second per instance

### Additional Requirements

**From Architecture:**

- D1: Event storage uses single-key-per-event pattern with actor-level ACID writes (`{tenant}:{domain}:{aggId}:events:{seq}`)
- D2: Command status stored in dedicated state store key `{tenant}:{correlationId}:status` with 24h TTL
- D3: Domain errors expressed as events (IRejectionEvent marker interface), not exceptions. Backward-compatible deserialization mandatory
- D4: DAPR access control via per-app-id allow list
- D5: API errors use RFC 7807 Problem Details with correlationId/tenantId extensions
- D6: Pub/sub topic naming: `{tenant}.{domain}.events`
- D7: Domain service invocation via `DaprClient.InvokeMethodAsync` with DAPR resiliency policies
- D8: Rate limiting via ASP.NET Core built-in `RateLimiting` middleware with sliding window
- D9: Package versioning via MinVer (Git tag-based SemVer)
- D10: CI/CD via GitHub Actions (build+test on PR, pack+publish on release tag)
- D11: E2E security testing infrastructure -- Keycloak in Aspire with realm-as-code for runtime OIDC validation
- No starter template -- greenfield project with pre-validated architecture
- Implementation sequence: Contracts -> Testing -> Server -> CommandApi -> Client -> Sample -> Aspire/AppHost -> Deploy -> CI/CD
- AggregateActor as thin orchestrator: 5-step delegation (idempotency check, tenant validation, state rehydration, domain invocation, state machine execution)
- MediatR pipeline order: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> CommandHandler
- Convention engine for DAPR resource naming derived from aggregate type names
- 5-layer convention override priority: Convention defaults -> Global code options -> Domain self-config -> External config -> Explicit override
- Feature folders over type folders in project organization
- Structured logging minimum fields defined per pipeline stage (GAP-5 resolved)
- OpenTelemetry activity naming: `EventStore.{Component}.{Operation}`

**From UX Design:**

- Four distinct interaction surfaces: Developer SDK (v1), REST API (v1), CLI/Operator (v1), Blazor Dashboard (v2)
- REST API is the primary product interface (highest UX investment for v1)
- Blazor Dashboard uses Fluent UI V4 components with design token-based theming (light/dark/high-contrast)
- Desktop-first responsive strategy: Full (1920px+), Standard (1280-1919px), Compact (960-1279px)
- WCAG 2.1 AA compliance target for Blazor Dashboard
- Accessibility: All custom components require ARIA labels, keyboard navigation, focus management, reduced motion support
- `aria-live` debouncing strategy for real-time data updates
- Custom components: StatusBadge, CommandPipeline, IssueBanner, StatCard, PatternGroup, EmptyState, ActivityChart
- Transition durations: 150ms micro-interactions, 300ms layout changes
- Master-detail pattern uses URL-driven state at compact tier
- ARIA tree snapshot tests for custom components (block merge on ARIA changes)
- Axe-core automated WCAG testing blocks merge on violations

### FR Coverage Map

- FR1: Epic 3 - Command submission REST endpoint
- FR2: Epic 3 - Command structural validation
- FR3: Epic 2 - Command routing to aggregate actor
- FR4: Epic 3 - Correlation ID generation and return
- FR5: Epic 3 - Command status query endpoint
- FR6: Epic 3 - Failed command replay endpoint
- FR7: Epic 2 - Optimistic concurrency conflict rejection
- FR8: Epic 4 - Dead-letter topic routing for failed commands
- FR9: Epic 2 - Append-only immutable event persistence
- FR10: Epic 2 - Strictly ordered gapless sequence numbers
- FR11: Epic 1 - 11-field event metadata envelope
- FR12: Epic 2 - Aggregate state reconstruction via event replay
- FR13: Epic 2 - Configurable snapshot creation
- FR14: Epic 2 - State reconstruction from snapshot + subsequent events
- FR15: Epic 2 - Composite key strategy with tenant/domain/aggregate isolation
- FR16: Epic 2 - Atomic event writes (0 or N, never partial)
- FR17: Epic 4 - CloudEvents 1.0 pub/sub publishing
- FR18: Epic 4 - At-least-once delivery guarantee
- FR19: Epic 4 - Per-tenant-per-domain topic publishing
- FR20: Epic 4 - Event persistence during pub/sub unavailability
- FR21: Epic 1 - Pure function domain processor contract
- FR22: Epic 7 - Domain service registration (config + convention)
- FR23: Epic 2 - Domain service invocation during command processing
- FR24: Epic 5 - Multi-domain processing within single instance
- FR25: Epic 5 - Multi-tenant processing with isolated event streams
- FR26: Epic 1 - Canonical identity tuple derivation
- FR27: Epic 5 - Data path isolation enforcement
- FR28: Epic 5 - Storage key isolation enforcement
- FR29: Epic 5 - Pub/sub topic isolation enforcement
- FR30: Epic 5 - JWT authentication
- FR31: Epic 5 - Claims-based authorization (tenant + domain + command type)
- FR32: Epic 5 - Gateway-level unauthorized command rejection
- FR33: Epic 5 - Actor-level tenant validation
- FR34: Epic 5 - DAPR service-to-service access control
- FR35: Epic 6 - OpenTelemetry full lifecycle traces
- FR36: Epic 6 - Structured logs with correlation/causation IDs
- FR37: Epic 6 - Dead-letter to originating request tracing
- FR38: Epic 6 - Health check endpoints (DAPR, state store, pub/sub)
- FR39: Epic 6 - Readiness check endpoints
- FR40: Epic 8 - Single Aspire command startup
- FR41: Epic 7 - Sample domain service reference implementation
- FR42: Epic 7 - NuGet client packages with convention-based registration
- FR43: Epic 8 - Environment deployment via DAPR component config only
- FR44: Epic 8 - Aspire publisher deployment manifests
- FR45: Epic 1 - Unit testing without DAPR dependency
- FR46: Epic 8 - Integration testing with DAPR test containers
- FR47: Epic 8 - End-to-end contract tests across Aspire topology
- FR48: Epic 1 - EventStoreAggregate base class with typed Apply methods
- FR49: Epic 2 - Command idempotency detection
- FR50: Epic 9 - 3-tier query actor routing model
- FR51: Epic 9 - ETag actor per projection+tenant
- FR52: Epic 9 - NotifyProjectionChanged API
- FR53: Epic 9 - ETag pre-check with HTTP 304
- FR54: Epic 9 - Query actor in-memory page cache
- FR55: Epic 9 - SignalR "changed" broadcast
- FR56: Epic 9 - SignalR hub with DAPR pub/sub backplane
- FR57: Epic 9 - Query contract library (NuGet)
- FR58: Epic 9 - Coarse invalidation per projection+tenant
- FR59: Epic 9 - SignalR client auto-rejoin on reconnect
- FR60: Epic 9 - 3 sample Blazor UI refresh patterns

## Epic List

### Epic 1: Domain Contracts & Testing Foundation

A developer can define event-sourced domain types (event envelope, commands, identity scheme, domain results), implement pure function processors, and unit test them without any infrastructure dependency.
**FRs covered:** FR11, FR21, FR26, FR45, FR48
**Key NFRs:** Foundation for all performance and reliability targets
**Architectural decisions:** D1 (key patterns), D3 (IRejectionEvent)

### Epic 2: Actor-Based Command Processing

Commands are processed through DAPR actors with event persistence, state replay, snapshots, optimistic concurrency, and idempotency -- the complete command-to-event pipeline core.
**FRs covered:** FR3, FR7, FR9, FR10, FR12, FR13, FR14, FR15, FR16, FR23, FR49
**Key NFRs:** NFR3, NFR4, NFR6, NFR22, NFR25, NFR26
**Architectural decisions:** D1 (storage strategy), D7 (domain invocation), thin orchestrator pattern

### Epic 3: Command API Gateway

API consumers submit commands via REST, receive correlation IDs, track command status, and replay failed commands -- with structural validation and rate limiting.
**FRs covered:** FR1, FR2, FR4, FR5, FR6
**Key NFRs:** NFR1, NFR2, NFR7, NFR33, NFR34
**Architectural decisions:** D2 (status storage), D5 (RFC 7807), D8 (rate limiting), MediatR pipeline

### Epic 4: Event Distribution

Persisted events flow to subscribers via CloudEvents pub/sub with at-least-once delivery, per-tenant-per-domain topics, and dead-letter routing for failures.
**FRs covered:** FR8, FR17, FR18, FR19, FR20
**Key NFRs:** NFR5, NFR22, NFR24, NFR28
**Architectural decisions:** D6 (topic naming)

### Epic 5: Security & Multi-Tenant Isolation

JWT authentication, claims-based authorization at every layer, and triple-layer multi-tenant isolation are enforced -- multiple tenants and domains coexist safely.
**FRs covered:** FR24, FR25, FR27, FR28, FR29, FR30, FR31, FR32, FR33, FR34
**Key NFRs:** NFR9-NFR15, NFR13, NFR18
**Architectural decisions:** D4 (DAPR access control), D11 (Keycloak E2E testing)

### Epic 6: Observability & Operational Readiness

Full command lifecycle traceable via OpenTelemetry, structured logs with correlation IDs enable fast diagnosis, health/readiness endpoints confirm system status.
**FRs covered:** FR35, FR36, FR37, FR38, FR39
**Key NFRs:** NFR11, NFR12, NFR21, NFR31
**Architectural decisions:** Structured logging pattern, OpenTelemetry activity naming

### Epic 7: Developer SDK & Sample Application

A developer installs NuGet packages, registers domain services via convention or DAPR config, and follows a working Counter sample as reference implementation.
**FRs covered:** FR22, FR41, FR42
**Key NFRs:** NFR20, NFR27, NFR29, NFR30
**Architectural decisions:** Convention engine, assembly scanning, 5-layer override priority

### Epic 8: Aspire Orchestration, Deployment & CI/CD

Developers start the full system with `dotnet aspire run`, DevOps deploys via Aspire publishers with zero code changes, CI/CD automates build/test/release, and integration + contract tests validate the full topology.
**FRs covered:** FR40, FR43, FR44, FR46, FR47
**Key NFRs:** NFR16, NFR17, NFR27-NFR32
**Architectural decisions:** D9 (MinVer), D10 (GitHub Actions)

### Epic 9: Query Pipeline & Projection Caching (v2)

A developer wires up projection caching with one `NotifyProjectionChanged` call, achieving ETag-based cache invalidation, HTTP 304 support, query actor routing, and real-time SignalR push notifications.
**FRs covered:** FR50, FR51, FR52, FR53, FR54, FR55, FR56, FR57, FR58, FR59, FR60
**Key NFRs:** NFR35-NFR39
**Architectural decisions:** 3-tier query routing, ETag actor pattern, coarse invalidation model

---

## Epic 1: Domain Contracts & Testing Foundation

A developer can define event-sourced domain types (event envelope, commands, identity scheme, domain results), implement pure function processors, and unit test them without any infrastructure dependency.

### Story 1.1: Event Envelope & Core Domain Types

As a domain service developer,
I want a complete set of domain types (event envelope with 11-field metadata, command envelope, domain result, and marker interfaces),
So that I have a shared contract for all command and event processing.

**Acceptance Criteria:**

**Given** a developer references the Contracts NuGet package
**When** they create an EventEnvelope
**Then** it contains all 11 metadata fields (aggregateId, tenantId, domain, sequence, timestamp, correlationId, causationId, userId, domainServiceVersion, eventTypeName, serializationFormat) plus opaque JSON payload and extension metadata bag
**And** the EventEnvelope is an immutable record type

**Given** a domain service returns a rejection
**When** the event implements IRejectionEvent marker interface
**Then** it is programmatically identifiable as a domain rejection (D3)

**Given** a CommandEnvelope is created
**When** it includes tenantId, domain, aggregateId, commandType, and payload
**Then** it passes structural completeness validation

**Given** a DomainResult is returned from a domain processor
**When** it contains events
**Then** all events are typed as IEventPayload implementations

**Given** a CommandStatus record
**When** tracking command lifecycle
**Then** it supports all 8 states: Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut (D2)

**Given** any domain type (EventEnvelope, CommandEnvelope, DomainResult)
**When** serialized to JSON and deserialized back
**Then** the round-trip produces an identical object with no data loss
**And** JSON property naming uses camelCase convention per architecture patterns

### Story 1.2: Identity Scheme & Key Derivation

As a domain service developer,
I want a canonical identity scheme that derives all system identifiers from a single `tenant:domain:aggregate-id` tuple,
So that actor IDs, event stream keys, pub/sub topics, and state store keys are consistent and predictable.

**Acceptance Criteria:**

**Given** an AggregateIdentity with tenantId="acme", domain="payments", aggregateId="order-123"
**When** deriving an actor ID
**Then** it produces a deterministic, unique actor identity string

**Given** an AggregateIdentity
**When** deriving event stream keys
**Then** it produces `{tenant}:{domain}:{aggId}:events:{seq}` pattern (D1)
**And** metadata key is `{tenant}:{domain}:{aggId}:metadata`
**And** snapshot key is `{tenant}:{domain}:{aggId}:snapshot`

**Given** an AggregateIdentity
**When** deriving a pub/sub topic
**Then** it produces `{tenant}.{domain}.events` pattern (D6)

**Given** a raw identity string in `tenant:domain:aggregate-id` format
**When** parsing it via IdentityParser
**Then** it produces a valid AggregateIdentity with all three components
**And** invalid formats throw a descriptive exception

### Story 1.3: Domain Processor Contract & Aggregate Base Class

As a domain service developer,
I want a pure function contract `(Command, CurrentState?) -> List<DomainEvent>` and a higher-level EventStoreAggregate base class with typed Apply methods,
So that I can implement domain logic without infrastructure code.

**Acceptance Criteria:**

**Given** a developer implements IDomainProcessor
**When** they define a Handle method with signature `(Command, State?) -> List<DomainEvent>`
**Then** the framework discovers it via reflection-based convention (fluent convention engine)

**Given** a developer inherits from EventStoreAggregate
**When** they define typed `Apply(EventType)` methods
**Then** the framework discovers and invokes them during state rehydration

**Given** a domain processor receives a command for a new aggregate
**When** CurrentState is null
**Then** the processor can create new aggregate state by returning initial events

**Given** a domain processor receives a command it wants to reject
**When** the processor returns a rejection event implementing IRejectionEvent
**Then** the rejection is a normal event (D3) -- no exceptions thrown for domain logic

**Given** a domain processor receives a command with no state change needed
**When** the processor returns an empty event list
**Then** the command is acknowledged with no state change (valid per D3)

### Story 1.4: Testing Utilities & Unit Test Infrastructure

As a domain service developer,
I want test builders, in-memory fakes, and assertion helpers,
So that I can unit test my domain logic without any DAPR runtime dependency.

**Acceptance Criteria:**

**Given** a developer uses CommandEnvelopeBuilder
**When** building a test command
**Then** it provides a fluent API with sensible defaults for all fields
**And** any field can be overridden

**Given** a developer uses EventEnvelopeBuilder
**When** building a test event
**Then** it provides a fluent API with sensible defaults for all 11 metadata fields

**Given** a developer uses InMemoryStateManager
**When** testing actor state operations
**Then** it behaves like IActorStateManager (set, get, save, remove) without DAPR

**Given** a developer uses FakeDomainServiceInvoker
**When** testing command processing
**Then** it accepts a configurable response (events or exceptions) for each command

**Given** a developer uses EventAssertions
**When** verifying event streams
**Then** they can assert event count, types, sequence ordering, and specific metadata values using Shouldly-style fluent assertions

**Given** all Epic 1 types (envelope, identity, processor, aggregate base)
**When** running Tier 1 unit tests
**Then** all tests pass without DAPR runtime, Docker, or external dependencies

---

## Epic 2: Actor-Based Command Processing

Commands are processed through DAPR actors with event persistence, state replay, snapshots, optimistic concurrency, and idempotency -- the complete command-to-event pipeline core.

### Story 2.1: Aggregate Actor & Command Routing

As a platform operator,
I want commands routed to the correct aggregate actor based on the identity scheme,
So that each aggregate has single-threaded, turn-based command processing.

**Acceptance Criteria:**

**Given** a command with tenantId="acme", domain="payments", aggregateId="order-123"
**When** the command is routed
**Then** it activates the actor with the identity derived from the canonical tuple (FR3)

**Given** the AggregateActor receives a command
**When** processing it
**Then** it delegates via the 5-step thin orchestrator pattern: (1) idempotency check, (2) tenant validation, (3) state rehydration, (4) domain service invocation, (5) state machine execution

**Given** a DAPR actor runtime
**When** two commands target the same aggregate concurrently
**Then** they are serialized by DAPR's turn-based concurrency -- never processed in parallel

### Story 2.2: Event Persistence & Atomic Writes

As a platform operator,
I want events persisted atomically in an append-only store with strictly ordered sequence numbers,
So that event streams are immutable, gapless, and never partially written.

**Acceptance Criteria:**

**Given** a domain processor returns 3 events for an aggregate
**When** the actor persists them
**Then** all 3 events are written atomically via `IActorStateManager.SaveStateAsync` (D1, FR16)
**And** each event is stored at key `{tenant}:{domain}:{aggId}:events:{seq}` (FR15)
**And** sequence numbers are strictly ordered and gapless starting from the current sequence + 1 (FR10)

**Given** events have been persisted
**When** attempting to modify or delete them
**Then** the operation is rejected -- events are append-only and immutable (FR9)

**Given** a concurrent command causes an ETag mismatch on the aggregate metadata key
**When** the actor attempts to save
**Then** the write fails with an optimistic concurrency conflict (FR7, NFR26)
**And** the conflict is reported as a 409 Conflict

**Given** the actor persists events and updates metadata
**When** using `IActorStateManager`
**Then** all state changes (event keys + metadata key) are committed in a single `SaveStateAsync` call -- actor-level ACID (D1)

### Story 2.3: State Rehydration & Event Replay

As a platform operator,
I want aggregate state reconstructed by replaying events from the stream,
So that actors can resume processing with correct state after activation.

**Acceptance Criteria:**

**Given** an aggregate with events at sequences 1 through 10
**When** the actor cold-activates and rehydrates state
**Then** it replays all 10 events in sequence order, applying each to reconstruct current state (FR12)

**Given** an aggregate with a snapshot at sequence 50 and events 51-55
**When** the actor rehydrates
**Then** it loads the snapshot first, then replays only events 51-55 (FR14)
**And** the resulting state is identical to replaying all 55 events from scratch

**Given** 1,000 events in an aggregate stream with no snapshot
**When** replaying all events
**Then** state reconstruction completes within 100ms (NFR6)

**Given** an actor cold-activates
**When** rehydrating from snapshot + subsequent events
**Then** activation completes within 50ms at p99 (NFR4)

### Story 2.4: Snapshot Creation & Management

As a platform operator,
I want snapshots created at configurable intervals to optimize state rehydration,
So that actor activation time remains constant regardless of total event count.

**Acceptance Criteria:**

**Given** a snapshot interval configured at 100 events (default)
**When** an aggregate reaches sequence 100 after command processing
**Then** the EventStore signals the domain service to produce a snapshot
**And** the snapshot is stored at key `{tenant}:{domain}:{aggId}:snapshot` (FR13)

**Given** a snapshot at sequence N and events N+1 through M
**When** reconstructing state
**Then** the result is identical to replaying events 1 through M (FR14, snapshot consistency invariant)

**Given** a tenant-domain pair with a custom snapshot interval of 50
**When** the aggregate reaches sequence 50
**Then** the snapshot threshold is triggered at 50, not the default 100

**Given** every domain registration
**When** configuring EventStore
**Then** a snapshot interval is mandatory -- no "never snapshot" option (enforcement guideline #15)

**Given** the snapshot consistency invariant
**When** an automated verification test runs
**Then** it confirms: for every aggregate with snapshot at sequence N and events N+1..M, the state produced equals full replay from 1..M across all registered domain types
**And** this verification is included in the Tier 1 unit test suite as a mandatory regression test

### Story 2.5: Domain Service Invocation

As a platform operator,
I want the actor to invoke registered domain services via DAPR service invocation,
So that pure function processors are called with the command and current state.

**Acceptance Criteria:**

**Given** a command for domain="payments" and tenant="acme"
**When** the actor invokes the domain service
**Then** it uses `DaprClient.InvokeMethodAsync` to call the registered endpoint (D7, FR23)
**And** passes the command and current aggregate state (or null for new aggregates)
**And** domain service resolution uses IDomainServiceInvoker abstraction -- tested with FakeDomainServiceInvoker (from Story 1.4) until real registration is available in Epic 7

**Given** a domain service returns events
**When** the actor receives them
**Then** the events are passed to the state machine for persistence and publishing

**Given** a domain service is unreachable
**When** the invocation fails
**Then** DAPR resiliency policies (retry with backoff, circuit breaker, timeout) handle the transient failure
**And** no custom retry logic exists in application code (enforcement guideline #4)

**Given** all DAPR sidecar calls
**When** invoking the domain service
**Then** the call completes within the 5-second sidecar timeout (enforcement guideline #14)

### Story 2.6: Checkpointed State Machine & Idempotency

As a platform operator,
I want a checkpointed state machine that tracks processing stages and detects duplicate commands,
So that crash recovery is deterministic and duplicate submissions are safely handled.

**Acceptance Criteria:**

**Given** an actor persists events (stage: EventsStored) then crashes before publishing
**When** the actor reactivates and resumes
**Then** it continues from the EventsStored checkpoint -- publishes events without re-persisting them (NFR25)
**And** no duplicate events are written to the store

**Given** the state machine tracks stages
**When** processing a command
**Then** it transitions through: Processing -> EventsStored -> EventsPublished -> Completed (or Rejected/PublishFailed/TimedOut)

**Given** a command with a correlationId that has already been processed by this aggregate
**When** the same command is submitted again
**Then** the idempotency checker returns AlreadyProcessed (FR49)
**And** no domain service invocation or state changes occur

**Given** an actor crash after event persistence but before pub/sub delivery
**When** recovery occurs
**Then** deterministic replay produces the same events -- zero data loss (NFR22, NFR23)

---

## Epic 3: Command API Gateway

API consumers submit commands via REST, receive correlation IDs, track command status, and replay failed commands -- with structural validation and rate limiting.

### Story 3.1: Command Submission Endpoint

As an API consumer,
I want to submit commands via a REST POST endpoint with tenant, domain, aggregate ID, command type, and payload,
So that I can trigger domain processing through a standard HTTP interface.

**Acceptance Criteria:**

**Given** a valid command payload with tenantId, domain, aggregateId, commandType, and JSON payload
**When** POSTing to `POST /api/v1/commands`
**Then** the system returns `202 Accepted` with a correlation ID in the response body and headers (FR1, FR4)

**Given** the command is accepted
**When** it enters the MediatR pipeline
**Then** it flows through LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> CommandHandler (pipeline order)

**Given** command submission at p99 under normal load
**When** measuring response time
**Then** the `202 Accepted` response returns within 50ms (NFR1)

**Given** the API receives a command
**When** a correlation ID is not provided by the caller
**Then** the system generates a unique correlation ID and returns it

### Story 3.2: Command Validation & Error Responses

As an API consumer,
I want submitted commands validated for structural completeness with clear error responses,
So that malformed requests are rejected before entering the processing pipeline.

**Acceptance Criteria:**

**Given** a command missing required fields (tenantId, domain, aggregateId, commandType, or payload)
**When** submitting it
**Then** the system returns `400 Bad Request` with RFC 7807 ProblemDetails (D5) including `validationErrors` array (FR2)

**Given** a command with malformed JSON payload
**When** submitting it
**Then** the system returns `400 Bad Request` with a descriptive ProblemDetails error

**Given** any API error response
**When** the response is generated
**Then** it includes `correlationId` and `tenantId` extensions in ProblemDetails (D5)
**And** no stack traces are exposed to clients (enforcement guideline #13)

**Given** FluentValidation rules on CommandEnvelope
**When** the ValidationBehavior runs in the MediatR pipeline
**Then** structural validation occurs before any actor activation or domain processing

### Story 3.3: Command Status Tracking

As an API consumer,
I want to query the processing status of a submitted command using its correlation ID,
So that I can track command lifecycle without polling the event stream.

**Acceptance Criteria:**

**Given** a command was submitted with correlationId="abc-123"
**When** querying `GET /api/v1/commands/abc-123/status`
**Then** the system returns the current status (Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, or TimedOut) (FR5)

**Given** the status record is stored at `{tenant}:{correlationId}:status` in the DAPR state store (D2)
**When** querying status
**Then** the query reads directly from state store -- no actor activation required

**Given** a completed command
**When** querying its status
**Then** the response includes: current stage, timestamp, aggregate ID, and for terminal states: event count (Completed), rejection event type (Rejected), failure reason (PublishFailed), or timeout duration (TimedOut)

**Given** a status record
**When** 24 hours have elapsed (default TTL)
**Then** the record is automatically cleaned up via DAPR state store `ttlInSeconds` metadata (D2)

**Given** a status write fails
**When** processing a command
**Then** the command processing pipeline is NOT blocked -- status writes are advisory (enforcement guideline #12)

### Story 3.4: Command Replay Endpoint

As an operator,
I want to replay a previously failed command via the Command API,
So that I can reprocess commands after root cause is fixed.

**Acceptance Criteria:**

**Given** a failed command with correlationId="abc-123"
**When** POSTing to `POST /api/v1/commands/abc-123/replay` with the original command payload in the request body
**Then** the command is resubmitted to the processing pipeline with a new correlation ID (FR6)
**And** the response includes the new correlation ID for tracking
**And** the caller provides the command payload (sourced from dead-letter topic inspection or their own records -- the replay endpoint does not store original payloads)

**Given** a command that completed successfully
**When** attempting to replay it
**Then** the system returns an appropriate error indicating the command is not in a replayable state

**Given** a replayed command
**When** tracking its status
**Then** the new correlation ID tracks the replay attempt independently from the original

### Story 3.5: Rate Limiting

As a platform operator,
I want per-tenant and per-consumer rate limiting on the Command API,
So that one tenant's command storm cannot affect others.

**Acceptance Criteria:**

**Given** a per-tenant rate limit of 1,000 commands per minute (default)
**When** tenant "acme" exceeds 1,000 commands in a sliding window
**Then** subsequent requests return `429 Too Many Requests` with `Retry-After` header (NFR33)

**Given** a per-consumer rate limit of 100 commands per second (default)
**When** a single authenticated consumer exceeds 100 commands/sec
**Then** subsequent requests return `429 Too Many Requests` with `Retry-After` header (NFR34)

**Given** the rate limiting middleware (D8)
**When** extracting tenant identity
**Then** it reads the `tenant_id` claim from the JWT token

**Given** rate limit thresholds
**When** configured per tenant via DAPR config store
**Then** changes take effect dynamically without system restart (NFR20)

---

## Epic 4: Event Distribution

Persisted events flow to subscribers via CloudEvents pub/sub with at-least-once delivery, per-tenant-per-domain topics, and dead-letter routing for failures.

### Story 4.1: CloudEvents Publishing & Topic Routing

As a domain event subscriber,
I want persisted events published to per-tenant-per-domain topics using CloudEvents 1.0 format,
So that I receive only events for my authorized scope in a standard envelope.

**Acceptance Criteria:**

**Given** events persisted for tenant="acme", domain="payments"
**When** the actor's state machine reaches the EventsPublished stage
**Then** events are published to topic `acme.payments.events` (D6, FR19)
**And** each event is wrapped in a CloudEvents 1.0 envelope (FR17)

**Given** events for tenant="acme" and tenant="globex" in the same domain
**When** publishing
**Then** events are published to separate topics (`acme.payments.events`, `globex.payments.events`)
**And** a subscriber to `acme.payments.events` never receives `globex` events (FR19)

**Given** event publication
**When** delivering to subscribers
**Then** at-least-once delivery is guaranteed via DAPR pub/sub semantics (FR18)

**Given** pub/sub delivery latency
**When** measuring from event persistence to subscriber delivery confirmation
**Then** it completes within 50ms at p99 (NFR5)

### Story 4.2: Pub/Sub Resilience & Backlog Drain

As a platform operator,
I want events persisted even when pub/sub is unavailable, with automatic backlog drain on recovery,
So that no events are lost during infrastructure outages.

**Acceptance Criteria:**

**Given** the pub/sub system is temporarily unavailable
**When** the actor persists events
**Then** events are safely stored in the state store (FR20)
**And** the state machine records PublishFailed status

**Given** pub/sub recovers after an outage
**When** DAPR retry policies activate
**Then** all events persisted during the outage are delivered to subscribers (NFR24)
**And** no events are silently dropped

**Given** DAPR resiliency policies
**When** handling pub/sub transient failures
**Then** retry with backoff is applied at the sidecar level -- no custom retry logic in application code (enforcement guideline #4)

### Story 4.3: Dead-Letter Routing

As an operator,
I want failed commands routed to a dead-letter topic with full context,
So that I can inspect and diagnose failures without database queries.

**Acceptance Criteria:**

**Given** a command that fails due to infrastructure error (after DAPR retry exhaustion)
**When** the failure is permanent
**Then** the command is routed to a dead-letter topic with full command payload, error details, and correlation context (FR8)

**Given** a dead-letter event
**When** inspecting it
**Then** it contains: original command payload, error message, stack trace (server-side only), correlation ID, tenant ID, domain, and aggregate ID

**Given** a domain rejection (e.g., `OrderRejected`)
**When** the domain service returns a rejection event
**Then** it is NOT routed to dead-letter -- domain rejections are normal events persisted to the event stream (D3)

**Given** dead-letter events
**When** an operator queries them
**Then** they can trace back to the originating request via correlation ID (FR37)

---

## Epic 5: Security & Multi-Tenant Isolation

JWT authentication, claims-based authorization at every layer, and triple-layer multi-tenant isolation are enforced -- multiple tenants and domains coexist safely.

### Story 5.1: JWT Authentication & Claims Transformation

As an API consumer,
I want to authenticate with the Command API using a JWT token,
So that my identity and authorized scopes are verified on every request.

**Acceptance Criteria:**

**Given** a request with a valid JWT token
**When** the token is validated
**Then** signature, expiry, and issuer are verified before any processing (NFR10, FR30)

**Given** a request without a JWT token or with an expired/invalid token
**When** it reaches the Command API
**Then** the system returns `401 Unauthorized` with ProblemDetails

**Given** a valid JWT with custom claims (tenants, domains, permissions as JSON arrays)
**When** claims transformation runs (`EventStoreClaimsTransformation`)
**Then** the claims are mapped to the authorization model for tenant + domain + command type checks (FR31)

**Given** a failed authentication attempt
**When** logging the failure
**Then** request metadata (source IP, attempted tenant, attempted command type) is logged WITHOUT the JWT token itself (NFR11)

**Given** API consumer communication
**When** any request is made
**Then** it is encrypted via TLS 1.2+ (NFR9)

### Story 5.2: Claims-Based Authorization & Gateway Rejection

As a platform operator,
I want commands authorized based on JWT claims at the API gateway before entering the pipeline,
So that unauthorized commands never reach actors or domain services.

**Acceptance Criteria:**

**Given** a JWT with `tenants: ["tenant-a"]` and `domains: ["orders"]`
**When** submitting a command for tenant="tenant-a", domain="orders"
**Then** the command is authorized and enters the processing pipeline (FR31)

**Given** a JWT with `tenants: ["tenant-a"]`
**When** submitting a command for tenant="tenant-b"
**Then** the system returns `403 Forbidden` at the API gateway (FR32)
**And** the command never reaches any actor or domain service

**Given** a JWT with `permissions: ["command:submit"]`
**When** submitting a command
**Then** the permission is verified against the command type
**And** a user with only `command:query` permission cannot submit commands

**Given** the AuthorizationBehavior in the MediatR pipeline
**When** it runs (position #3 after logging and validation)
**Then** tenant x domain x command type authorization is enforced before the CommandHandler

### Story 5.3: Actor-Level Tenant Validation

As a platform operator,
I want tenant validation enforced at the actor level as a second defense layer,
So that even if gateway authorization is bypassed, cross-tenant access is prevented.

**Acceptance Criteria:**

**Given** a command reaches the AggregateActor
**When** the thin orchestrator runs step 2 (tenant validation)
**Then** the command's tenantId is validated against the actor's identity-derived tenant (FR33)

**Given** a command with tenantId="tenant-a" targeting an actor for tenant="tenant-b"
**When** tenant validation runs
**Then** the command is rejected with a security error before any state access

**Given** multi-tenant processing
**When** 2+ tenants submit commands to the same domain
**Then** each tenant's commands are processed in isolated actors with isolated event streams (FR25)

**Given** multi-domain processing
**When** 2+ domains operate within the same EventStore instance
**Then** each domain's commands are routed to domain-specific actors (FR24)

### Story 5.4: Triple-Layer Data Isolation

As a platform operator,
I want data path, storage key, and pub/sub topic isolation enforced for every tenant,
So that cross-tenant data access is impossible even under infrastructure failures.

**Acceptance Criteria:**

**Given** commands for tenant="acme" and tenant="globex"
**When** routed through the system
**Then** they are processed by different actor instances -- data path isolation (FR27)

**Given** events stored for tenant="acme"
**When** querying the state store
**Then** event stream keys include tenant ID in the composite key (`acme:payments:order-123:events:1`) -- storage key isolation (FR28)
**And** tenant="globex" keys are in a separate namespace, inaccessible even with direct state store queries

**Given** events published for tenant="acme"
**When** subscribing to pub/sub
**Then** events go to `acme.payments.events` only -- pub/sub topic isolation (FR29)
**And** a subscriber to `globex.payments.events` never receives `acme` events

**Given** all three isolation layers
**When** one layer has a misconfiguration
**Then** the remaining two layers still prevent cross-tenant access (NFR13)

### Story 5.5: DAPR Access Control Policies

As a platform operator,
I want DAPR access control policies restricting service-to-service communication,
So that only authorized components can invoke each other.

**Acceptance Criteria:**

**Given** DAPR access control policies (D4)
**When** configured per-app-id
**Then** CommandApi can invoke actor services and domain services
**And** domain services cannot invoke anything directly -- they are called, never call out (FR34)

**Given** a service without an allow-list entry
**When** attempting to invoke another service
**Then** DAPR rejects the call at the sidecar level (NFR15)

**Given** secrets (connection strings, JWT signing keys, DAPR credentials)
**When** the system is deployed
**Then** no secrets are stored in application code or committed configuration files (NFR14)

**Given** Epic 5 security stories (5.1-5.5)
**When** tested at Tier 1 and Tier 2
**Then** all tests use TestJwtTokenGenerator (HS256 symmetric key) for fast validation
**And** full Keycloak E2E runtime proof is deferred to Epic 8 (Story 8.6) where Aspire AppHost infrastructure is available

---

## Epic 6: Observability & Operational Readiness

Full command lifecycle traceable via OpenTelemetry, structured logs with correlation IDs enable fast diagnosis, health/readiness endpoints confirm system status.

### Story 6.1: OpenTelemetry Distributed Tracing

As an operator,
I want the full command lifecycle traced via OpenTelemetry spans,
So that I can visualize the entire pipeline in any OTLP-compatible collector.

**Acceptance Criteria:**

**Given** a command submitted to the API
**When** it flows through the pipeline
**Then** OpenTelemetry activities span: `EventStore.CommandApi.Submit`, `EventStore.Actor.ProcessCommand`, `EventStore.DomainService.Invoke`, `EventStore.Events.Persist`, `EventStore.Events.Publish` (FR35)

**Given** each activity
**When** created
**Then** it includes correlationId as a tag/attribute on every span

**Given** a command that flows through multiple components
**When** tracing it
**Then** all activities are linked in a single distributed trace with parent-child relationships

**Given** OpenTelemetry export
**When** configured
**Then** traces are exportable to any OTLP-compatible collector (Aspire dashboard, Jaeger, Grafana/Tempo) (NFR31)

### Story 6.2: Structured Logging Pipeline

As an operator,
I want structured logs with correlation and causation IDs at every pipeline stage,
So that I can diagnose issues by filtering on any context field.

**Acceptance Criteria:**

**Given** a command at each processing stage
**When** a structured log entry is emitted
**Then** it includes the minimum fields per stage (FR36):

- Command received: correlationId, tenantId, domain, aggregateId, commandType (Information)
- Actor activated: correlationId, tenantId, aggregateId, currentSequence (Debug)
- Domain service invoked: correlationId, tenantId, domain, domainServiceVersion (Information)
- Events persisted: correlationId, tenantId, aggregateId, eventCount, newSequence (Information)
- Events published: correlationId, tenantId, topic, eventCount (Information)
- Command completed: correlationId, tenantId, aggregateId, status, durationMs (Information)
- Domain rejection: correlationId, tenantId, aggregateId, rejectionEventType (Warning)
- Infrastructure failure: correlationId, tenantId, aggregateId, exceptionType, message (Error)

**Given** any log entry
**When** it contains event data
**Then** event payload data is NEVER logged -- only envelope metadata fields (NFR12)

**Given** a failed command on the dead-letter topic
**When** an operator searches structured logs by correlationId
**Then** the full processing history is visible from API receipt to failure point (FR37)

### Story 6.3: Health & Readiness Endpoints

As a platform operator,
I want health and readiness check endpoints,
So that load balancers and orchestrators can determine system availability.

**Acceptance Criteria:**

**Given** the CommandApi is running
**When** querying the health endpoint
**Then** it reports connectivity status for: DAPR sidecar, state store, and pub/sub (FR38)

**Given** the CommandApi is running
**When** querying the readiness endpoint
**Then** it reports whether all dependencies are healthy and the system is accepting commands (FR39)
**And** readiness includes DAPR config store availability

**Given** the DAPR sidecar is not reachable
**When** the health check runs
**Then** the health endpoint returns unhealthy status
**And** the readiness endpoint returns not-ready

**Given** a Kubernetes or container orchestrator
**When** it probes health/readiness
**Then** the endpoints return standard health check response format compatible with ASP.NET Core health checks

---

## Epic 7: Developer SDK & Sample Application

A developer installs NuGet packages, registers domain services via convention or DAPR config, and follows a working Counter sample as reference implementation.

### Story 7.1: Convention Engine & Domain Service Registration

As a domain service developer,
I want to register my domain service with EventStore via convention-based assembly scanning or explicit DAPR configuration,
So that my service is discoverable without boilerplate wiring code.

**Acceptance Criteria:**

**Given** a developer calls `AddEventStore()` in their service registration
**When** the application starts
**Then** assembly scanning discovers all types inheriting from EventStoreAggregate or implementing IDomainProcessor (FR42)
**And** DAPR resource names are derived from aggregate type names via the convention engine

**Given** a type named `OrderAggregate`
**When** the convention engine processes it
**Then** it derives: domain name = `order`, state store = `order-eventstore`, pub/sub topic pattern = `{tenant}.order.events`, command endpoint = `order-commands`

**Given** a developer applies `[EventStoreDomain("billing")]` to an aggregate
**When** the convention engine processes it
**Then** the attribute override replaces the derived name (`billing` instead of derived)

**Given** the 5-layer convention override priority
**When** multiple layers configure the same setting
**Then** lower layers override higher: Convention defaults -> Global code options -> Domain self-config -> External config -> Explicit override

**Given** a developer uses explicit DAPR configuration entry
**When** registering a domain service by tenant + domain
**Then** the registration specifies tenant ID, domain name, and service invocation endpoint (FR22)

**Given** a new tenant or domain added via DAPR config store
**When** the configuration changes
**Then** it takes effect dynamically without system restart (NFR20)

### Story 7.2: Client NuGet Package & DI Registration

As a domain service developer,
I want to install a single NuGet package and call `AddEventStore()` for zero-configuration quickstart,
So that I can build domain services with minimal setup.

**Acceptance Criteria:**

**Given** a developer installs `Hexalith.EventStore.Client` via NuGet
**When** they call `builder.Services.AddEventStore()` in their service
**Then** all required services are registered: domain processor discovery, convention engine, DAPR client integration (FR42)

**Given** the zero-configuration quickstart
**When** the developer has aggregates in the same assembly
**Then** auto-discovery finds and registers them without explicit configuration

**Given** a developer wants to customize behavior
**When** they call `AddEventStore(options => { ... })`
**Then** they can configure: custom serializer, per-domain state store component, snapshot intervals, and other cross-cutting options

**Given** the Client package references
**When** inspecting its dependencies
**Then** it depends on Contracts (for types) but NOT on Server (separation of concerns)

### Story 7.3: Counter Sample Domain Service

As a developer evaluating EventStore,
I want a complete working sample domain service (Counter) demonstrating the pure function programming model,
So that I have a reference implementation to learn from and copy.

**Acceptance Criteria:**

**Given** the Counter sample project
**When** a developer inspects it
**Then** it demonstrates:

- `IncrementCounter` command definition
- `CounterIncremented` state-change event (past tense naming)
- `CounterLimitExceeded` rejection event implementing IRejectionEvent (D3)
- `CounterState` aggregate state
- `CounterProcessor` pure function handler: `(IncrementCounter, CounterState?) -> List<DomainEvent>` (FR41)

**Given** the Counter sample
**When** a developer reads it
**Then** it contains zero infrastructure code -- no event store wiring, no pub/sub configuration, no actor management

**Given** the Counter sample project
**When** built and registered with EventStore
**Then** it demonstrates the full programming model: command handling, state-change events, rejection events, and snapshot production

**Given** a developer following the sample pattern
**When** they create their own domain service
**Then** the sample serves as a copy-paste template for the pure function approach

---

## Epic 8: Aspire Orchestration, Deployment & CI/CD

Developers start the full system with `dotnet aspire run`, DevOps deploys via Aspire publishers with zero code changes, CI/CD automates build/test/release, and integration + contract tests validate the full topology.

### Story 8.1: Aspire AppHost & Local Dev Topology

As a developer,
I want to start the complete EventStore system with a single `dotnet aspire run` command,
So that I have the full topology running locally in under 10 minutes.

**Acceptance Criteria:**

**Given** the Aspire AppHost project
**When** running `dotnet aspire run`
**Then** the full topology starts: EventStore CommandApi, sample Counter domain service, Redis state store, Redis pub/sub, OpenTelemetry collector, Aspire dashboard (FR40)

**Given** the AppHost project
**When** defining DAPR sidecars
**Then** local DAPR component configs are included: `statestore.yaml` (Redis), `pubsub.yaml` (Redis), `resiliency.yaml`, `accesscontrol.yaml` (D4)

**Given** the Keycloak integration (D11)
**When** the AppHost starts
**Then** Keycloak runs on port 8180 with the `hexalith-realm.json` realm import
**And** CommandApi receives environment variable overrides pointing Authority to Keycloak

**Given** the Aspire dashboard
**When** a developer opens it
**Then** they see all resources, OpenTelemetry traces, structured logs, and metrics

### Story 8.2: Infrastructure Portability & Deployment Manifests

As a DevOps engineer,
I want to deploy EventStore to different environments by changing only DAPR component configs and generate deployment manifests via Aspire publishers,
So that the same application code runs across dev, staging, and production.

**Acceptance Criteria:**

**Given** a production deployment target
**When** changing from Redis to PostgreSQL state store
**Then** only the DAPR component YAML is changed -- zero application code, zero recompilation (FR43, NFR29)

**Given** an Aspire publisher
**When** running `aspire publish --publisher docker-compose`
**Then** a complete Docker Compose deployment manifest is generated (FR44)

**Given** an Aspire publisher
**When** running `aspire publish --publisher kubernetes` or `--publisher azure-container-apps`
**Then** appropriate deployment manifests are generated for each target (FR44, NFR32)

**Given** production DAPR component configurations
**When** deploying
**Then** configs for PostgreSQL state store, RabbitMQ/Kafka pub/sub, and production resiliency policies are provided in `deploy/dapr/` directory

**Given** the same EventStore application containers
**When** deployed with different DAPR component YAMLs
**Then** the system functions correctly with any validated backend (NFR27, NFR28)

### Story 8.3: Integration Testing with DAPR

As a developer,
I want to run integration tests against the actor processing pipeline using DAPR,
So that I can verify the full actor lifecycle with real DAPR runtime behavior.

**Acceptance Criteria:**

**Given** DAPR slim init has been run
**When** running Tier 2 integration tests (`dotnet test tests/Hexalith.EventStore.Server.Tests/`)
**Then** tests execute against real DAPR actors with in-process state management (FR46)

**Given** the integration tests
**When** testing the actor processing pipeline
**Then** they verify: command routing, event persistence, state rehydration, optimistic concurrency, and snapshot behavior

**Given** the integration test infrastructure
**When** tests complete
**Then** no external Docker containers or cloud services are required -- DAPR slim mode is sufficient

### Story 8.4: End-to-End Contract Tests

As a developer,
I want end-to-end contract tests validating the full command lifecycle across the complete Aspire topology,
So that I can verify the entire system works as an integrated whole.

**Acceptance Criteria:**

**Given** DAPR full init and Docker are available
**When** running Tier 3 contract tests (`dotnet test tests/Hexalith.EventStore.IntegrationTests/`)
**Then** tests execute across the complete Aspire topology: CommandApi, actors, domain services, state store, pub/sub (FR47)

**Given** the contract tests
**When** validating the command lifecycle
**Then** they verify end-to-end: command submission via REST -> actor processing -> event persistence -> pub/sub delivery -> status tracking

**Given** the E2E security tests (D11)
**When** running with `[Trait("Category", "E2E")]`
**Then** they use real Keycloak OIDC tokens and validate the full six-layer auth pipeline

### Story 8.5: CI/CD Pipeline & NuGet Publishing

As a project maintainer,
I want GitHub Actions automating build, test, and NuGet package publishing,
So that every PR is validated and releases are automated from Git tags.

**Acceptance Criteria:**

**Given** a push or PR to main
**When** the CI pipeline runs
**Then** it executes: restore, build (Release), Tier 1 unit tests, Tier 2 integration tests (with DAPR slim) (D10)

**Given** a Git tag matching `v*` pattern (e.g., `v1.0.0`)
**When** the release pipeline runs
**Then** it executes: all tests, `dotnet pack`, validates 5 expected NuGet packages (Contracts, Client, Server, Testing, Aspire), and pushes to NuGet.org

**Given** package versioning via MinVer (D9)
**When** a Git tag `v1.0.0` exists
**Then** all packages receive version `1.0.0`
**And** pre-release versions are auto-calculated from tag + commit height

**Given** the CI pipeline
**When** Tier 3 contract tests are configured
**Then** they run as an optional step (requires full DAPR init + Docker)

### Story 8.6: Platform Validation & E2E Security Proof

As a platform architect,
I want chaos scenario testing, performance benchmarking, infrastructure portability validation, and Keycloak-based E2E security proof,
So that the platform meets all success criteria before v1 GA (Journey 6).

**Acceptance Criteria:**

**Given** the Keycloak instance in Aspire AppHost (D11)
**When** running E2E security tests with `[Trait("Category", "E2E")]`
**Then** real OIDC tokens are acquired via Resource Owner Password Grant from Keycloak
**And** tests validate through the full six-layer auth pipeline with real IdP-issued tokens

**Given** test users defined in `hexalith-realm.json` (admin-user, tenant-a-user, tenant-b-user, readonly-user, no-tenant-user)
**When** each user authenticates
**Then** cross-tenant isolation, permission enforcement, and tenant validation rejection are proven at runtime

**Given** the chaos scenario suite
**When** testing state store crash mid-command
**Then** the actor's checkpointed state machine resumes from the correct stage with deterministic replay and zero data loss (NFR22, NFR23)

**Given** the chaos scenario suite
**When** testing pub/sub unavailability during command processing
**Then** events persist safely in the state store and DAPR retry policies drain the backlog on recovery (NFR24)

**Given** performance benchmarking under sustained load (100 cmd/sec for 10 minutes)
**When** measuring p99 latencies
**Then** command submission < 50ms, end-to-end lifecycle < 200ms, event append < 10ms (NFR1, NFR2, NFR3)

**Given** the full test suite
**When** switching the entire topology from Redis to PostgreSQL state store
**Then** all tests pass with zero application code changes (NFR29)

**Given** horizontal scaling validation
**When** adding EventStore server replicas
**Then** DAPR actor placement distributes aggregates across replicas (NFR16)
**And** the system supports at least 10,000 active aggregates without exceeding latency targets (NFR17)

---

## Epic 9: Query Pipeline & Projection Caching (v2)

A developer wires up projection caching with one `NotifyProjectionChanged` call, achieving ETag-based cache invalidation, HTTP 304 support, query actor routing, and real-time SignalR push notifications.

### Story 9.1: ETag Actor & Projection Change Notification

As a domain service developer,
I want to notify EventStore of projection changes via a single API call, with an ETag actor tracking the current projection version,
So that cache invalidation happens automatically without writing caching infrastructure code.

**Acceptance Criteria:**

**Given** a domain service updates a projection for tenant="acme", projectionType="OrderList"
**When** calling `NotifyProjectionChanged("OrderList", "acme")`
**Then** the ETag actor for `OrderList-acme` regenerates its GUID (base64-22 encoded) (FR51, FR52)

**Given** the NotifyProjectionChanged API
**When** the transport is not explicitly configured
**Then** it uses DAPR pub/sub by default, with direct service invocation available via configuration (FR52)

**Given** an optional entityId parameter
**When** calling `NotifyProjectionChanged("OrderList", "acme", "order-123")`
**Then** the entityId is forwarded for future fine-grained invalidation but the current model invalidates the entire projection+tenant pair (FR58)

**Given** a projection change notification
**When** the ETag actor regenerates
**Then** all cached query results for that projection+tenant are invalidated -- coarse invalidation model (FR58)

### Story 9.2: 3-Tier Query Actor Routing

As a platform operator,
I want queries routed to query actors using a 3-tier model based on query parameters,
So that cached results are correctly scoped to the query's specificity.

**Acceptance Criteria:**

**Given** a query with EntityId="order-123"
**When** routing
**Then** it routes to actor `{QueryType}-{TenantId}-{EntityId}` (tier 1) (FR50)

**Given** a query without EntityId but with non-empty serialized payload
**When** routing
**Then** it routes to actor `{QueryType}-{TenantId}-{Checksum}` where Checksum is 11-char truncated SHA256 base64url of the serialized payload (tier 2) (FR50)

**Given** a query without EntityId and with empty payload
**When** routing
**Then** it routes to actor `{QueryType}-{TenantId}` (tier 3) (FR50)

**Given** two semantically identical queries with different JSON key ordering
**When** computing checksums
**Then** they produce different checksums and route to separate cache actors -- accepted trade-off documented in FR50

### Story 9.3: Query Endpoint with ETag Pre-Check & Cache

As an API consumer,
I want the query endpoint to return HTTP 304 when data hasn't changed and serve cached results from query actors,
So that read-heavy workloads avoid unnecessary database queries.

**Acceptance Criteria:**

**Given** a query request with `If-None-Match` header matching the current ETag
**When** the endpoint performs the ETag pre-check (first gate)
**Then** it calls the ETag actor, finds a match, and returns HTTP 304 without activating the query actor (FR53)
**And** the pre-check completes within 5ms at p99 for warm actors (NFR35)

**Given** a query request with a stale or missing ETag
**When** the query actor is activated (second gate)
**Then** it compares its cached ETag against the current ETag actor value (FR54)
**And** on match: returns cached data within 10ms at p99 (NFR36)
**And** on mismatch: re-queries the projection via domain service, caches the result, and returns it within 200ms at p99 (NFR37)

**Given** a query actor
**When** caching results
**Then** it uses in-memory storage only -- no state store persistence (FR54)
**And** DAPR idle timeout garbage-collects inactive query actors

**Given** the query pipeline under load
**When** handling concurrent requests
**Then** it supports at least 1,000 concurrent query requests per second per instance (NFR39)

### Story 9.4: Query Contract Library

As a domain service developer,
I want a query contract library defining mandatory query metadata fields as typed static members,
So that query routing is type-safe and consistent across all domain services.

**Acceptance Criteria:**

**Given** the query contract NuGet package
**When** a developer defines a query
**Then** mandatory fields (Domain, QueryType, TenantId, ProjectionType) are enforced as typed static members (FR57)
**And** optional fields (EntityId) are available

**Given** the query contract
**When** used for routing
**Then** it serves as the single source of truth for query actor ID derivation

**Given** a developer building a new query
**When** referencing the contract library
**Then** they get compile-time safety for query metadata -- no string-based routing errors

### Story 9.5: SignalR Real-Time Notifications

As a Blazor/browser client developer,
I want real-time "changed" signals via SignalR when projections update,
So that my UI refreshes without polling.

**Acceptance Criteria:**

**Given** a projection's ETag is regenerated
**When** the ETag actor broadcasts
**Then** a signal-only "changed" message is sent to all connected SignalR clients in the group `{ProjectionType}:{TenantId}` (FR55)
**And** delivery completes within 100ms at p99 (NFR38)

**Given** the EventStore server
**When** hosting the SignalR hub
**Then** it uses a Redis backplane for multi-instance SignalR message distribution (FR56)

**Given** a Blazor Server circuit reconnects after a disconnect
**When** the SignalR client helper detects recovery
**Then** it automatically rejoins all SignalR groups -- no manual intervention by the developer (FR59)

**Given** a WebSocket drop or network interruption
**When** the connection is restored
**Then** the client helper auto-rejoins and the developer's code receives subsequent "changed" signals normally (FR59)

### Story 9.6: Sample UI Refresh Patterns

As a Blazor developer,
I want at least 3 documented reference patterns for handling the SignalR "changed" signal,
So that I can choose the right UI update strategy for my use case.

**Acceptance Criteria:**

**Given** the EventStore documentation and sample application
**When** a developer looks for SignalR integration patterns
**Then** they find at least 3 reference implementations (FR60):

1. Toast notification prompting manual refresh
2. Automatic silent data reload
3. Selective component refresh targeting only the affected projection

**Given** each sample pattern
**When** implemented in a Blazor component
**Then** it demonstrates: subscribing to the SignalR group, handling the "changed" signal, and triggering the appropriate UI update

**Given** the sample patterns
**When** a developer evaluates them
**Then** trade-offs are documented: toast = least disruptive but requires user action, silent reload = seamless but may cause layout shifts, selective refresh = best UX but most implementation effort
