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

- FR1: An API consumer can submit a command to the EventStore via a REST endpoint with tenant, domain, aggregate ID, command type, and payload
- FR2: The system can validate a submitted command for structural completeness before routing it for processing
- FR3: The system can route a command to the correct aggregate actor based on the identity scheme (`tenant:domain:aggregate-id`)
- FR4: An API consumer can receive a correlation ID upon command submission for tracking the command lifecycle
- FR5: An API consumer can query the processing status of a previously submitted command using its correlation ID
- FR6: An operator can replay a previously failed command via the Command API after root cause is fixed
- FR7: The system can reject duplicate commands targeting an aggregate that has an optimistic concurrency conflict, returning an appropriate error
- FR8: The system can route failed commands to a dead-letter topic with full command payload, error details, and correlation context
- FR9: The system can persist events in an append-only, immutable event store where events are never modified or deleted after persistence
- FR10: The system can assign strictly ordered, gapless sequence numbers to events within a single aggregate stream
- FR11: The system can wrap each event in an 11-field metadata envelope (aggregate ID, tenant, domain, sequence, timestamp, correlation ID, causation ID, user identity, domain service version, event type, serialization format) plus opaque payload and extension metadata bag
- FR12: The system can reconstruct aggregate state by replaying all events in an aggregate's stream from sequence 1 to current
- FR13: The system can create snapshots of aggregate state at configurable intervals (every N events) to optimize state rehydration
- FR14: The system can reconstruct aggregate state from the latest snapshot plus subsequent events, producing identical state to full replay
- FR15: The system can store events using a composite key strategy that includes tenant, domain, and aggregate identity for isolation
- FR16: The system can enforce atomic event writes -- a command produces 0 or N events as a single transaction, never a partial subset
- FR17: The system can publish persisted events to subscribers via a pub/sub mechanism using CloudEvents 1.0 envelope format
- FR18: The system can deliver events to subscribers with at-least-once delivery guarantee
- FR19: The system can publish events to per-tenant-per-domain topics, ensuring subscribers only receive events for their authorized scope
- FR20: The system can continue persisting events when the pub/sub system is temporarily unavailable, draining the backlog on recovery
- FR21: A domain service developer can implement a domain processor as a pure function with the contract `(Command, CurrentState?) -> List<DomainEvent>`
- FR22: A domain service developer can register their domain service with EventStore by tenant and domain via configuration
- FR23: The system can invoke a registered domain service when processing a command, passing the command and current aggregate state
- FR24: The system can process commands for multiple independent domains within the same EventStore instance
- FR25: The system can process commands for multiple tenants within the same domain, each with isolated event streams
- FR26: The system can derive actor IDs, event stream keys, pub/sub topics, and queue sessions from a single canonical identity tuple (`tenant:domain:aggregate-id`)
- FR27: The system can enforce data path isolation so that commands for one tenant are never routed to another tenant's domain service
- FR28: The system can enforce storage key isolation so that event streams for different tenants are inaccessible to each other even at the state store level
- FR29: The system can enforce pub/sub topic isolation so that event subscribers only receive events from tenants they are authorized to access
- FR30: An API consumer can authenticate with the Command API using a JWT token
- FR31: The system can authorize command submissions based on JWT claims for tenant, domain, and command type
- FR32: The system can reject unauthorized commands at the API gateway before they enter the processing pipeline
- FR33: The system can validate that a command's tenant matches the authenticated user's authorized tenants at the actor level
- FR34: The system can enforce service-to-service access control between EventStore components via DAPR policies
- FR35: The system can emit OpenTelemetry traces spanning the full command lifecycle (received, processing, events stored, events published, completed)
- FR36: The system can emit structured logs with correlation and causation IDs at each stage of the command processing pipeline
- FR37: An operator can trace a failed command from the dead-letter topic back to its originating request via correlation ID
- FR38: The system can expose health check endpoints indicating DAPR sidecar, state store, and pub/sub connectivity status
- FR39: The system can expose readiness check endpoints indicating all dependencies are healthy and the system is accepting commands
- FR40: A developer can start the complete EventStore system (server, sample domain service, state store, message broker, OpenTelemetry) with a single Aspire command
- FR41: A developer can reference a sample domain service implementation as a working example of the pure function programming model
- FR42: A developer can install EventStore client packages via NuGet to build and register domain services
- FR43: A DevOps engineer can deploy EventStore to different environments by changing only DAPR component configuration files with zero application code changes
- FR44: A DevOps engineer can generate deployment manifests for Docker Compose, Kubernetes, or Azure Container Apps via Aspire publishers
- FR45: A developer can run unit tests against domain service pure functions without any DAPR runtime dependency
- FR46: A developer can run integration tests against the actor processing pipeline using DAPR test containers
- FR47: A developer can run end-to-end contract tests validating the full command lifecycle across the complete Aspire topology

### NonFunctional Requirements

- NFR1: Command submission via the REST API must complete (return 202 Accepted) within 50ms at p99 under normal load
- NFR2: End-to-end command lifecycle (API receipt to event published on pub/sub) must complete within 200ms at p99
- NFR3: Event append latency (actor persist to state store confirmation) must be under 10ms at p99
- NFR4: Actor cold activation with state rehydration (snapshot + subsequent events) must complete within 50ms at p99
- NFR5: Pub/sub delivery (event persistence to subscriber delivery confirmation) must complete within 50ms at p99
- NFR6: Full aggregate state reconstruction from 1,000 events must complete within 100ms
- NFR7: The system must support at least 100 concurrent command submissions per second per EventStore instance without exceeding latency targets
- NFR8: DAPR sidecar overhead per building block call must not exceed 2ms at p99
- NFR9: All communication between API consumers and the Command API must be encrypted via TLS 1.2+
- NFR10: JWT tokens must be validated for signature, expiry, and issuer on every request before any processing occurs
- NFR11: Failed authentication or authorization attempts must be logged with request metadata (source IP, attempted tenant, attempted command type) without logging the JWT token itself
- NFR12: Event payload data must never appear in log output; only event metadata (envelope fields) may be logged
- NFR13: Multi-tenant data isolation must be enforced at all three layers (actor identity, DAPR policies, command metadata) -- failure at one layer must not compromise isolation
- NFR14: Secrets (connection strings, JWT signing keys, DAPR component credentials) must never be stored in application code or configuration files committed to source control
- NFR15: Service-to-service communication between EventStore components must be authenticated and authorized via DAPR access control policies
- NFR16: The system must support horizontal scaling by adding EventStore server replicas, with DAPR actor placement distributing aggregates across replicas
- NFR17: The system must support at least 10,000 active aggregates per EventStore instance without degradation beyond defined latency targets
- NFR18: The system must support at least 10 tenants per EventStore instance with full isolation and no cross-tenant performance interference
- NFR19: Event stream growth per aggregate must be bounded by the snapshot strategy -- state rehydration time must remain constant regardless of total event count (snapshot + tail events only)
- NFR20: Adding a new tenant or domain must not require system restart or downtime -- configuration changes via DAPR config store must take effect dynamically
- NFR21: The system must achieve 99.9%+ availability with HA DAPR control plane and multi-replica deployment
- NFR22: Zero events may be lost under any tested failure scenario (state store crash, actor crash, pub/sub unavailability, network partition)
- NFR23: After a state store recovery, the system must resume processing from the last checkpointed state with deterministic replay -- no manual intervention required
- NFR24: After a pub/sub recovery, all events persisted during the outage must be delivered to subscribers via DAPR retry policies -- no events silently dropped
- NFR25: Actor crash after event persistence but before pub/sub delivery must not result in duplicate event persistence -- the checkpointed state machine must resume from the correct stage
- NFR26: Optimistic concurrency conflicts must be detected and reported (409 Conflict) -- never silently overwriting events
- NFR27: The system must function correctly with any DAPR-compatible state store that supports key-value operations and ETag-based optimistic concurrency (validated: Redis, PostgreSQL)
- NFR28: The system must function correctly with any DAPR-compatible pub/sub component that supports CloudEvents 1.0 and at-least-once delivery (validated: RabbitMQ, Azure Service Bus)
- NFR29: Switching between validated backend configurations must require only DAPR component YAML changes -- zero application code, zero recompilation, zero redeployment of application containers
- NFR30: Domain services must be invocable via DAPR service invocation over HTTP -- the EventStore must not impose language or framework constraints on domain service implementations beyond the pure function contract
- NFR31: OpenTelemetry telemetry must be exportable to any OTLP-compatible collector (validated: Aspire dashboard, Jaeger, Grafana/Tempo)
- NFR32: The system must be deployable via Aspire publishers to Docker Compose, Kubernetes, and Azure Container Apps without custom deployment scripts

### Additional Requirements

**From Architecture:**

- Starter Template: Custom solution from individual `dotnet new` templates establishing 5 NuGet packages + CommandApi host + Aspire orchestration + Sample domain service + 3 test projects (see Architecture: Starter Template Evaluation)
- D1: Event Storage Strategy -- Single-key-per-event with actor-level ACID writes via `IActorStateManager`. Key pattern: `{tenant}:{domain}:{aggId}:events:{seq}`
- D2: Command Status Storage -- Dedicated state store key `{tenant}:{correlationId}:status` with 24-hour default TTL, written at API layer before actor invocation
- D3: Domain Service Error Contract -- Domain errors expressed as rejection events (IRejectionEvent marker interface); infrastructure errors are exceptions. Empty event list = valid (no state change)
- D4: DAPR Access Control -- Per-app-id allow list. CommandApi can invoke actor services and domain services
- D5: API Error Response Format -- RFC 7807 Problem Details with extensions (correlationId, tenantId, validationErrors)
- D6: Pub/Sub Topic Naming -- `{tenant}.{domain}.events` pattern
- D7: Domain Service Invocation -- DAPR service invocation (`DaprClient.InvokeMethodAsync`), service discovery via DAPR config store
- D8: Rate Limiting Strategy -- ASP.NET Core built-in `RateLimiting` middleware, per-tenant sliding window, configurable via DAPR config store
- D9: NuGet Package Versioning -- MinVer (version from Git tags, zero config), monorepo single-version strategy
- D10: CI/CD Pipeline -- GitHub Actions (build+test on PR, pack+publish NuGet on release tag, DAPR integration tests)
- SEC-1: EventStore owns all 11 envelope metadata fields; domain services return payloads only
- SEC-2: Tenant validation occurs BEFORE state rehydration during actor activation
- SEC-3: Command status queries are tenant-scoped (JWT tenant must match)
- SEC-4: Extension metadata sanitized at API gateway (max size, character validation, injection prevention)
- SEC-5: Event payload data never in logs (enforced at framework level)
- 15 Enforcement Rules covering naming conventions, feature folders, MediatR pipeline order, DAPR-only retries, no payload in logs, IActorStateManager only, ProblemDetails only, event naming, correlationId everywhere, Add* extensions, write-once event keys, advisory status writes, no stack traces in responses, 5s DAPR sidecar timeout, mandatory snapshot config (default 100 events)
- AggregateActor as thin orchestrator with 5-step delegation: idempotency check, tenant validation, state rehydration, domain service invocation, state machine execution
- MediatR pipeline behaviors ordered: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> CommandHandler
- Command lifecycle states: Received -> Processing -> EventsStored -> EventsPublished -> Completed | Rejected | PublishFailed | TimedOut
- Backward-compatible deserialization CRITICAL: Domain services MUST maintain deserialization for all event types ever produced. UnknownEvent during rehydration is an error condition
- Project structure with clear dependency boundaries: Contracts (zero deps) <- Server <- CommandApi; Testing -> Contracts + Server

**From UX Design Specification:**

- OpenAPI 3.1 specification with interactive Swagger UI at `/swagger` on CommandApi, with pre-populated example payloads for sample Counter domain service
- REST API follows RFC 7807 for all errors with human-readable `detail` messages written for the reader (Stripe principle)
- `202 Accepted` response with `Location` header pointing to status endpoint and `Retry-After: 1` header
- Consistent status color system across all surfaces: Green (Completed/Healthy), Blue (in-flight states), Yellow (Rejected), Red (Failed/TimedOut), Gray (Unknown/Inactive)
- Cross-surface terminology consistency: same lifecycle state names in SDK enum, API response, structured logs, and dashboard
- Monospace rendering for all machine-generated values (correlation IDs, aggregate IDs, tenant IDs, JSON payloads)
- v2 Blazor Dashboard using Fluent UI V4 (4.13.2) with 7 custom components: StatusBadge, CommandPipeline, IssueBanner, StatCard, PatternGroup, EmptyState, ActivityChart
- v2 Dashboard page hierarchy: Landing (Adaptive Hub), Commands, Events/Timeline, Health (with Dead Letter Explorer), Tenants, Services, Settings
- v2 Dashboard sidebar: static navigation + dynamic topology FluentTreeView with error badge bubbling
- WCAG 2.1 AA compliance target for v2 dashboard with axe-core in CI (merge-blocking)
- Responsive design: Optimal (1920px+), Standard (1280px+), Compact (960px+), Minimum (below 960px)
- System-preference-first theme strategy (light/dark via `prefers-color-scheme`)
- Domain service hot reload: domain services restart independently without restarting EventStore or full Aspire topology
- Empty state design: every empty view guides users toward first meaningful action
- Master-detail as resizable side panel for command investigation
- Pattern grouping for dead letter batch triage
- Correlation ID as hyperlink to Events/Timeline filtered view across all surfaces

### FR Coverage Map

- FR1: Epic 2 - Command submission via REST endpoint
- FR2: Epic 2 - Command structural validation
- FR3: Epic 3 - Command routing to aggregate actor via identity scheme
- FR4: Epic 2 - Correlation ID generation and return on submission
- FR5: Epic 2 - Command status query via correlation ID
- FR6: Epic 2 - Failed command replay via Command API
- FR7: Epic 2 - Optimistic concurrency conflict rejection
- FR8: Epic 4 - Dead-letter routing with full context
- FR9: Epic 3 - Append-only immutable event persistence
- FR10: Epic 3 - Strictly ordered gapless sequence numbers
- FR11: Epic 1 - 11-field event envelope metadata definition
- FR12: Epic 3 - Aggregate state reconstruction via event replay
- FR13: Epic 3 - Snapshot creation at configurable intervals
- FR14: Epic 3 - State reconstruction from snapshot plus subsequent events
- FR15: Epic 3 - Composite key strategy for tenant/domain/aggregate isolation
- FR16: Epic 3 - Atomic event writes (0 or N, never partial)
- FR17: Epic 4 - Event publication via pub/sub with CloudEvents 1.0
- FR18: Epic 4 - At-least-once delivery guarantee
- FR19: Epic 4 - Per-tenant-per-domain topic isolation
- FR20: Epic 4 - Persist-then-publish resilience during pub/sub outage
- FR21: Epic 1 - Pure function domain processor contract definition
- FR22: Epic 3 - Domain service registration via configuration
- FR23: Epic 3 - Domain service invocation with command and current state
- FR24: Epic 3 - Multi-domain processing within single instance
- FR25: Epic 3 - Multi-tenant processing with isolated event streams
- FR26: Epic 1 - Canonical identity tuple deriving all addressing
- FR27: Epic 5 - Data path isolation (tenant command routing)
- FR28: Epic 3 - Storage key isolation (tenant event stream keys)
- FR29: Epic 5 - Pub/sub topic isolation (authorized tenant events only)
- FR30: Epic 2 - JWT authentication on Command API
- FR31: Epic 2 - Claims-based authorization (tenant + domain + command type)
- FR32: Epic 2 - Unauthorized command rejection at API gateway
- FR33: Epic 3 - Actor-level tenant validation against JWT claims
- FR34: Epic 5 - DAPR policy-based service-to-service access control
- FR35: Epic 6 - OpenTelemetry traces spanning full command lifecycle
- FR36: Epic 6 - Structured logs with correlation/causation IDs per stage
- FR37: Epic 6 - Dead-letter to originating request tracing via correlation ID
- FR38: Epic 6 - Health check endpoints (DAPR sidecar, state store, pub/sub)
- FR39: Epic 6 - Readiness check endpoints (all dependencies healthy)
- FR40: Epic 1 - Single Aspire command startup of complete system
- FR41: Epic 7 - Sample domain service reference implementation
- FR42: Epic 1 - NuGet client packages for domain service development
- FR43: Epic 7 - Environment deployment via DAPR component config only
- FR44: Epic 7 - Aspire publisher deployment manifests (Docker Compose, K8s, ACA)
- FR45: Epic 1 - Unit tests without DAPR runtime dependency
- FR46: Epic 7 - Integration tests with DAPR test containers
- FR47: Epic 7 - End-to-end contract tests across full Aspire topology

### Elicitation Notes

**5-method Advanced Elicitation applied to epic structure:**

1. **Pre-mortem Analysis** identified: Epic 2 overload (17 FRs), Aspire AppHost misplaced in capstone, security bolt-on risk, observability as afterthought, snapshots unnecessarily isolated
2. **User Persona Focus Group** (Marco, Jerome, Priya, Sanjay, Alex): Confirmed Aspire AppHost needed from day one, Epic 2 too large for incremental delivery, Swagger UI needed alongside API not at end
3. **Self-Consistency Validation** (3 alternative groupings compared): Consensus that Aspire belongs in foundation, snapshots merge into processing, basic auth weaves into API epic
4. **Comparative Analysis Matrix** (5 weighted criteria): Epic 2 scored 3/10 on Scope Balance, Epic 6 scored 5/10 on User Value, Epic 7 scored 4/10 on Scope Balance -- all addressed in revision
5. **Critique and Refine**: Strengths (100% FR coverage, clean dependency flow) preserved; weaknesses (overloaded epics, misplaced concerns, bolt-on patterns) resolved

**Key revisions from elicitation:**
- Split old Epic 2 into Command API Gateway (new Epic 2) and Actor Processing Engine (new Epic 3)
- Moved Aspire AppHost + ServiceDefaults into Epic 1 (foundation)
- Merged Snapshots into Actor Processing epic (new Epic 3)
- Wove basic JWT auth into Command API epic (new Epic 2)
- Basic OpenTelemetry/structured logging woven into Epics 2-4 as built; Epic 6 covers health/readiness completeness
- Focused security hardening epic (new Epic 5) on multi-tenant DAPR policies
- Capstone epic (new Epic 7) focused on sample app, testing, CI/CD, deployment

## Epic List

### Epic 1: Developer SDK, Core Contracts & Local Development Setup
A developer can scaffold the complete solution, start the system with `dotnet aspire run`, install NuGet packages, implement a domain processor pure function `(Command, CurrentState?) -> List<DomainEvent>`, and test it locally with zero DAPR dependency.
**FRs covered:** FR11, FR21, FR26, FR40, FR42, FR45

**What this delivers:**
- Complete solution structure (5 NuGet packages + CommandApi + Aspire + Sample + Tests)
- Contracts package: EventEnvelope (11-field metadata), CommandEnvelope, AggregateIdentity, IRejectionEvent, DomainResult, CommandStatus enum
- Client package: IDomainProcessor, DomainProcessorBase, AddEventStoreClient()
- Testing package: InMemoryStateManager, FakeDomainServiceInvoker, builders, assertions
- Aspire AppHost (skeleton topology) + ServiceDefaults (resilience, telemetry, health)
- Build infrastructure: Directory.Build.props, Directory.Packages.props, global.json, .editorconfig
- Unit tests for contracts (Tier 1)

### Epic 2: Command API Gateway & Status Tracking
An API consumer can submit a command via REST with JWT authentication, receive a correlation ID, track command status, replay failed commands, and receive RFC 7807 error responses. The API surface is complete, secured, and self-documenting via Swagger UI.
**FRs covered:** FR1, FR2, FR4, FR5, FR6, FR7, FR30, FR31, FR32

**What this delivers:**
- CommandApi host with REST endpoints (POST /commands, GET /status, POST /replay)
- MediatR pipeline (LoggingBehavior, ValidationBehavior, AuthorizationBehavior, SubmitCommandHandler)
- JWT authentication middleware (signature, expiry, issuer validation)
- Claims transformation (IClaimsTransformation for tenant/domain/command permissions)
- Endpoint authorization policies (ABAC on controllers)
- Correlation ID middleware (generation + propagation)
- Command status writer (D2: dedicated state store key with 24h TTL, written at API layer)
- Rate limiting middleware (per-tenant sliding window, D8)
- Global exception handler (RFC 7807 ProblemDetails with extensions, D5)
- OpenAPI/Swagger UI at `/swagger` with pre-populated example payloads
- Basic OpenTelemetry activities and structured logging at API layer

### Epic 3: Command Processing, Event Storage & State Management
The system routes commands to aggregate actors that invoke registered domain services, persist events atomically with optimistic concurrency, reconstruct state from snapshots plus events, and support multi-tenant multi-domain processing with storage isolation.
**FRs covered:** FR3, FR9, FR10, FR12, FR13, FR14, FR15, FR16, FR22, FR23, FR24, FR25, FR28, FR33

**What this delivers:**
- AggregateActor (thin orchestrator: idempotency check -> tenant validation -> state rehydration -> domain invocation -> state machine)
- EventPersister (append-only via IActorStateManager, write-once keys, ETag concurrency on metadata, D1)
- EventStreamReader (state reconstruction from snapshot + subsequent events)
- SnapshotManager (creation at configurable intervals, default 100 events, mandatory per rule #15)
- ActorStateMachine (checkpointed stages: Received -> Processing -> EventsStored -> EventsPublished -> terminal)
- IdempotencyChecker (command deduplication)
- TenantValidator (SEC-2: tenant validation before state rehydration)
- DaprDomainServiceInvoker (DAPR service invocation, D7)
- DomainServiceResolver (config store lookup for tenant + domain -> service endpoint)
- CommandRouter (routes to correct actor via identity scheme)
- EventStore populates all 11 envelope metadata fields (SEC-1)
- OpenTelemetry activities and structured logging at actor/persistence layer

### Epic 4: Event Distribution & Dead-Letter Handling
After events are persisted, subscribers automatically receive them via pub/sub with CloudEvents 1.0, per-tenant-per-domain topic isolation, at-least-once delivery, and resilient persist-then-publish flow. Failed commands route to dead-letter topics with full context.
**FRs covered:** FR8, FR17, FR18, FR19, FR20

**What this delivers:**
- EventPublisher (DAPR pub/sub with CloudEvents 1.0 envelope)
- Topic naming: `{tenant}.{domain}.events` (D6)
- Persist-then-publish flow integrated with ActorStateMachine checkpoints
- Dead-letter routing for infrastructure failures after DAPR retry exhaustion
- At-least-once delivery with pub/sub unavailability resilience (events safe in state store, drain on recovery)
- OpenTelemetry activities and structured logging at distribution layer

### Epic 5: Multi-Tenant Security & Access Control Enforcement
Multi-tenant data isolation is enforced across all three layers (actor identity, DAPR policies, command metadata), with DAPR access control policies restricting service-to-service communication and pub/sub topic isolation ensuring authorized-only event delivery.
**FRs covered:** FR27, FR29, FR34

**What this delivers:**
- DAPR access control policies (D4: per-app-id allow list, CommandApi -> actors -> domain services)
- Data path isolation verification (commands for tenant A never reach tenant B)
- Pub/sub topic isolation enforcement (subscribers receive only authorized tenant events)
- Extension metadata sanitization at API gateway (SEC-4: max size, character validation, injection prevention)
- Security audit logging (NFR11: failed auth attempts logged without JWT token)
- Event payload never in logs enforcement (SEC-5, NFR12)

### Epic 6: Observability, Health & Operational Readiness
An operator can trace any command through the full pipeline via OpenTelemetry, diagnose failures via structured logs with correlation IDs, check system health/readiness, and trace dead-letter commands back to their originating requests.
**FRs covered:** FR35, FR36, FR37, FR38, FR39

**What this delivers:**
- Complete OpenTelemetry trace instrumentation across full command lifecycle (named activities per architecture pattern)
- Structured logging completeness verification (all defined fields per pipeline stage, GAP-5)
- Correlation ID tracing from dead-letter topic to originating request
- Health check endpoints: DaprSidecarHealthCheck, DaprConfigStoreHealthCheck (FR38)
- Readiness check endpoints: ReadinessCheck combining all dependency health (FR39)
- ServiceDefaults configuration completeness (resilience, telemetry, health)

### Epic 7: Sample Application, Testing, CI/CD & Deployment
A new developer references a working sample domain service, runs integration and contract tests at all three tiers, and a DevOps engineer deploys to any environment via Aspire publishers with zero application code changes.
**FRs covered:** FR41, FR43, FR44, FR46, FR47

**What this delivers:**
- Sample Counter domain service (commands, events, rejection events, state, CounterProcessor)
- Local DAPR component configs (Redis state store, Redis pub/sub, resiliency, access control)
- Production DAPR component configs (deploy/: PostgreSQL, Cosmos DB, RabbitMQ, Kafka)
- Integration tests with DAPR test containers (Tier 2: Server.Tests)
- Contract tests with full Aspire topology (Tier 3: IntegrationTests)
- CI/CD pipeline (GitHub Actions: build+test on PR, pack+publish NuGet on release tag)
- MinVer package versioning (D9)
- Aspire publisher deployment manifests (Docker Compose, Kubernetes, Azure Container Apps)
- Infrastructure portability validation (same tests on Redis vs PostgreSQL)
- Domain service hot reload validation (development inner loop under 5 seconds)

## Epic 1: Developer SDK, Core Contracts & Local Development Setup

A developer can scaffold the complete solution, start the system with `dotnet aspire run`, install NuGet packages, implement a domain processor pure function `(Command, CurrentState?) -> List<DomainEvent>`, and test it locally with zero DAPR dependency.

### Story 1.1: Solution Structure & Build Infrastructure

As a **developer**,
I want a complete solution scaffold with all projects, build infrastructure (Directory.Build.props, Directory.Packages.props, global.json, .editorconfig), and feature folder conventions,
So that I can begin development with consistent tooling and dependency management from day one.

**Acceptance Criteria:**

**Given** a fresh clone of the repository
**When** I open the solution in an IDE
**Then** the solution contains: Hexalith.EventStore.Contracts, Hexalith.EventStore.Client, Hexalith.EventStore.Server, Hexalith.EventStore.Aspire, Hexalith.EventStore.Testing projects
**And** Directory.Build.props sets common properties (TargetFramework net10.0, Nullable enable, ImplicitUsings enable, TreatWarningsAsErrors true)
**And** Directory.Packages.props uses central package management for all NuGet dependencies
**And** global.json pins the .NET 10 SDK version
**And** .editorconfig enforces project coding conventions

### Story 1.2: Contracts Package - Event Envelope & Core Types

As a **domain service developer**,
I want a Contracts NuGet package defining the 11-field EventEnvelope, CommandEnvelope, AggregateIdentity, IRejectionEvent, DomainResult, and CommandStatus enum,
So that I can build domain services against stable, versioned contracts with zero external dependencies.

**Acceptance Criteria:**

**Given** the Contracts package is referenced
**When** I inspect the EventEnvelope record
**Then** it contains exactly 11 metadata fields: AggregateId, TenantId, Domain, SequenceNumber, Timestamp, CorrelationId, CausationId, UserId, DomainServiceVersion, EventTypeName, SerializationFormat, plus Payload (byte[]) and Extensions (IDictionary<string, string>)
**And** AggregateIdentity encapsulates the canonical `tenant:domain:aggregate-id` tuple and derives string representations for actor IDs, event stream keys, pub/sub topics, and queue sessions (FR26)
**And** IRejectionEvent is a marker interface for domain rejection events (D3)
**And** DomainResult wraps `List<DomainEvent>` with success/rejection semantics
**And** CommandStatus enum defines: Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut
**And** the Contracts package has zero external dependencies

### Story 1.3: Client Package - Domain Processor Contract & Registration

As a **domain service developer**,
I want a Client NuGet package providing IDomainProcessor interface with the pure function contract `(Command, CurrentState?) -> List<DomainEvent>`, a DomainProcessorBase helper, and an `AddEventStoreClient()` DI extension,
So that I can implement and register domain services with a clean, testable programming model.

**Acceptance Criteria:**

**Given** the Client package is referenced
**When** I implement IDomainProcessor
**Then** the contract is `Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)` matching the pure function model (FR21)
**And** DomainProcessorBase provides boilerplate (command type routing, state casting) so developers focus on business logic
**And** `AddEventStoreClient(IServiceCollection)` registers the domain processor and related services
**And** the Client package depends only on Contracts

### Story 1.4: Testing Package - In-Memory Test Helpers

As a **domain service developer**,
I want a Testing NuGet package providing InMemoryStateManager, FakeDomainServiceInvoker, test builders (CommandEnvelopeBuilder, AggregateIdentityBuilder), and assertion helpers,
So that I can unit-test my domain processor pure functions with zero DAPR runtime dependency (FR45).

**Acceptance Criteria:**

**Given** the Testing package is referenced
**When** I write a unit test for my domain processor
**Then** InMemoryStateManager simulates IActorStateManager without DAPR
**And** FakeDomainServiceInvoker allows injecting canned domain service responses
**And** CommandEnvelopeBuilder creates valid CommandEnvelope instances with sensible defaults
**And** assertion helpers verify event sequences, rejection events, and envelope field correctness
**And** no DAPR runtime or sidecar is required to run tests

### Story 1.5: Aspire AppHost & ServiceDefaults Scaffolding

As a **developer**,
I want an Aspire AppHost project that orchestrates the EventStore topology (CommandApi, sample domain service, Redis state store, Redis pub/sub) and a ServiceDefaults project configuring resilience, telemetry, and health check defaults,
So that I can start the complete system with a single `dotnet aspire run` command (FR40).

**Acceptance Criteria:**

**Given** the Aspire AppHost project exists with resource definitions
**When** I run `dotnet aspire run` from the AppHost directory
**Then** the CommandApi host starts (even if endpoints are stub/placeholder at this stage)
**And** Redis state store and pub/sub containers are provisioned
**And** the Aspire dashboard is accessible showing all resources
**And** ServiceDefaults configures: OpenTelemetry (basic traces + metrics), health check endpoints (/health, /alive), HTTP resilience policies
**And** all projects reference ServiceDefaults via `AddServiceDefaults()`

### Story 1.6: Contracts Unit Tests (Tier 1)

As a **developer**,
I want comprehensive unit tests for the Contracts package covering EventEnvelope creation, AggregateIdentity derivation, CommandStatus transitions, and IRejectionEvent detection,
So that the foundational types are validated and regression-protected before any dependent code is built.

**Acceptance Criteria:**

**Given** the Contracts.Tests project exists
**When** I run `dotnet test` on Contracts.Tests
**Then** EventEnvelope construction with all 11 fields is validated
**And** AggregateIdentity correctly derives actor ID, event stream key, pub/sub topic, and queue session from `tenant:domain:aggregate-id`
**And** AggregateIdentity rejects malformed identity tuples (missing components, empty strings, injection characters)
**And** IRejectionEvent marker interface is correctly detected on implementing types
**And** all tests pass with zero DAPR dependency

## Epic 2: Command API Gateway & Status Tracking

An API consumer can submit a command via REST with JWT authentication, receive a correlation ID, track command status, replay failed commands, and receive RFC 7807 error responses. The API surface is complete, secured, and self-documenting via Swagger UI.

### Story 2.1: CommandApi Host & Minimal Endpoint Scaffolding

As an **API consumer**,
I want a running CommandApi host with a POST `/commands` endpoint that accepts a command payload and returns `202 Accepted` with a correlation ID,
So that I can submit commands and receive immediate acknowledgment.

**Acceptance Criteria:**

**Given** the CommandApi is running
**When** I POST a valid JSON command payload to `/commands` with tenant, domain, aggregateId, commandType, and payload fields
**Then** the response is `202 Accepted`
**And** the response body contains a `correlationId` (GUID)
**And** the response includes a `Location` header pointing to the status endpoint `/commands/status/{correlationId}`
**And** the response includes a `Retry-After: 1` header
**And** the endpoint uses ASP.NET Core minimal APIs or controllers with proper routing

### Story 2.2: Command Validation & RFC 7807 Error Responses

As an **API consumer**,
I want submitted commands validated for structural completeness and all errors returned as RFC 7807 Problem Details with extensions,
So that I receive actionable, machine-readable error responses when my requests are malformed.

**Acceptance Criteria:**

**Given** the CommandApi is running
**When** I submit a command missing required fields (tenant, domain, aggregateId, commandType, payload)
**Then** the response is `400 Bad Request` with RFC 7807 ProblemDetails body
**And** ProblemDetails includes `type`, `title`, `status`, `detail` (human-readable), `instance`
**And** ProblemDetails extensions include `correlationId`, `tenantId`, `validationErrors` array (D5)
**And** a MediatR ValidationBehavior performs structural validation before the handler
**And** a global exception handler converts unhandled exceptions to RFC 7807 (no stack traces in responses per enforcement rules)
**And** extension metadata is sanitized at the API gateway (SEC-4: max size, character validation, injection prevention)

### Story 2.3: MediatR Pipeline & Logging Behavior

As a **developer**,
I want the CommandApi to use a MediatR pipeline with LoggingBehavior as the outermost behavior, producing structured logs with correlation and causation IDs at each pipeline stage,
So that every command can be traced through the API layer.

**Acceptance Criteria:**

**Given** a command is submitted to the API
**When** it flows through the MediatR pipeline
**Then** LoggingBehavior is the first behavior in the pipeline (outermost)
**And** it logs entry/exit with correlation ID, command type, tenant, and domain
**And** event payload data never appears in logs (SEC-5, NFR12)
**And** basic OpenTelemetry activities are created for the command submission span
**And** pipeline order is: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> CommandHandler

### Story 2.4: JWT Authentication & Claims Transformation

As an **API consumer**,
I want the CommandApi to authenticate my requests via JWT token (signature, expiry, issuer validation) and transform claims into tenant/domain/command-type permissions,
So that only authenticated consumers can submit commands (FR30).

**Acceptance Criteria:**

**Given** the CommandApi has JWT authentication middleware configured
**When** I submit a request without a JWT token or with an invalid/expired token
**Then** the response is `401 Unauthorized` as RFC 7807 ProblemDetails
**And** failed authentication attempts are logged with request metadata (source IP, attempted tenant, attempted command type) without logging the JWT token (NFR11)
**When** I submit a request with a valid JWT token
**Then** the token is validated for signature, expiry, and issuer on every request (NFR10)
**And** IClaimsTransformation extracts tenant, domain, and command type permissions from JWT claims
**And** all communication is encrypted via TLS 1.2+ (NFR9)

### Story 2.5: Endpoint Authorization & Command Rejection

As an **API consumer**,
I want the system to authorize my command submissions based on JWT claims for tenant, domain, and command type, rejecting unauthorized commands at the API gateway before processing,
So that the system enforces access control at the perimeter (FR31, FR32).

**Acceptance Criteria:**

**Given** I am authenticated with a valid JWT
**When** I submit a command for a tenant not in my authorized tenants
**Then** the response is `403 Forbidden` as RFC 7807 ProblemDetails with `tenantId` extension
**And** the command is rejected before entering the MediatR pipeline
**When** I submit a command for a domain or command type not in my authorized scope
**Then** the response is `403 Forbidden` with appropriate detail message
**And** AuthorizationBehavior in the MediatR pipeline enforces claims-based ABAC
**And** failed authorization attempts are logged (NFR11)

### Story 2.6: Command Status Tracking & Query Endpoint

As an **API consumer**,
I want to query the processing status of a previously submitted command using its correlation ID via GET `/commands/status/{correlationId}`,
So that I can monitor command lifecycle progression (FR5).

**Acceptance Criteria:**

**Given** a command was previously submitted and a correlation ID was returned
**When** I GET `/commands/status/{correlationId}`
**Then** the response contains the current command status (Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut)
**And** the status is read from a dedicated state store key `{tenant}:{correlationId}:status` (D2)
**And** status entries have a 24-hour default TTL (D2)
**And** status queries are tenant-scoped: the JWT tenant must match the command's tenant (SEC-3)
**And** querying a non-existent or expired correlation ID returns `404 Not Found` as ProblemDetails

### Story 2.7: Command Replay Endpoint

As an **API consumer**,
I want to replay a previously failed command via POST `/commands/replay/{correlationId}` after the root cause has been fixed,
So that I can recover from transient or fixed failures without re-creating the command (FR6).

**Acceptance Criteria:**

**Given** a command previously failed (status: Rejected, PublishFailed, or TimedOut)
**When** I POST `/commands/replay/{correlationId}`
**Then** the original command is resubmitted to the processing pipeline with the same correlation ID
**And** a new causation ID is generated to distinguish the replay from the original submission
**And** the command status is reset to Received
**And** replaying a command with status Completed or Processing returns `409 Conflict` as ProblemDetails
**And** replaying a non-existent correlation ID returns `404 Not Found`
**And** authorization rules apply to the replay (JWT must be authorized for the command's tenant/domain)

### Story 2.8: Optimistic Concurrency Conflict Handling

As an **API consumer**,
I want the system to detect and report optimistic concurrency conflicts when two commands target the same aggregate simultaneously,
So that I know to retry or handle the conflict (FR7).

**Acceptance Criteria:**

**Given** two concurrent commands target the same aggregate
**When** the second command encounters an optimistic concurrency conflict (ETag mismatch propagated from the actor processing layer)
**Then** the API returns `409 Conflict` as RFC 7807 ProblemDetails (NFR26)
**And** ProblemDetails includes `correlationId`, `aggregateId`, and a detail message explaining the concurrency conflict
**And** the command status is updated to Rejected with reason "ConcurrencyConflict"
**And** the consumer can retry the command (it will be processed against the updated state)

**Scope note:** This story covers the API layer's handling and response formatting for concurrency conflicts. The actual ETag-based conflict detection at the state store level is implemented in Epic 3 (Story 3.7). For Epic 2 testing, concurrency conflicts can be simulated via a mock/stub of the actor processing layer.

### Story 2.9: Rate Limiting & OpenAPI/Swagger UI

As an **API consumer**,
I want per-tenant rate limiting on command submissions and interactive Swagger UI documentation at `/swagger`,
So that the system is protected from abuse and I can explore the API interactively (D8).

**Acceptance Criteria:**

**Given** the CommandApi is running
**When** I navigate to `/swagger`
**Then** OpenAPI 3.1 specification is served with all endpoints documented
**And** Swagger UI shows pre-populated example payloads for the sample Counter domain service
**And** per-tenant sliding window rate limiting is enforced via ASP.NET Core RateLimiting middleware (D8)
**When** a tenant exceeds the rate limit
**Then** the response is `429 Too Many Requests` as RFC 7807 ProblemDetails with `Retry-After` header
**And** rate limit configuration is loaded from DAPR config store (configurable without restart)

## Epic 3: Command Processing, Event Storage & State Management

The system routes commands to aggregate actors that invoke registered domain services, persist events atomically with optimistic concurrency, reconstruct state from snapshots plus events, and support multi-tenant multi-domain processing with storage isolation.

### Story 3.1: Command Router & Actor Activation

As a **system operator**,
I want submitted commands routed to the correct aggregate actor based on the canonical identity scheme (`tenant:domain:aggregate-id`),
So that each aggregate has a dedicated processing context (FR3).

**Acceptance Criteria:**

**Given** a validated command arrives from the MediatR pipeline
**When** the CommandHandler processes the command
**Then** it derives the actor ID from AggregateIdentity (`tenant:domain:aggregate-id`) using the canonical derivation from Contracts (FR26)
**And** it invokes the correct DAPR actor using the derived actor ID
**And** the AggregateActor activates (cold or warm) and receives the command
**And** the CommandRouter is registered in DI via `AddEventStoreServer()` extension

### Story 3.2: AggregateActor Orchestrator & Idempotency Check

As a **system operator**,
I want the AggregateActor to be a thin orchestrator that first checks for duplicate commands (idempotency) before proceeding with processing,
So that replayed or duplicate commands don't produce duplicate events.

**Acceptance Criteria:**

**Given** the AggregateActor receives a command
**When** it begins the 5-step delegation sequence
**Then** Step 1 (idempotency check) verifies whether this correlation ID has already been processed for this aggregate
**And** if the command is a duplicate, the actor returns the previous result without reprocessing
**And** idempotency state is stored via IActorStateManager (enforcement rule #6)
**And** the actor is implemented as a thin orchestrator delegating to focused components

### Story 3.3: Tenant Validation at Actor Level

As a **security auditor**,
I want the AggregateActor to validate that the command's tenant matches the authenticated user's authorized tenants before any state rehydration occurs,
So that tenant isolation is enforced at the actor level as a second line of defense (FR33, SEC-2).

**Acceptance Criteria:**

**Given** the AggregateActor passes the idempotency check
**When** Step 2 (tenant validation) executes
**Then** the command's tenant ID is verified against the JWT claims passed through the command context
**And** validation occurs BEFORE any state rehydration (SEC-2: tenant validation before state access)
**And** if the tenant doesn't match, the command is rejected with a Rejected status and reason "TenantMismatch"
**And** the rejection is logged with correlation ID, attempted tenant, and authorized tenants (without JWT token)

### Story 3.4: Event Stream Reader & State Rehydration

As a **system operator**,
I want the AggregateActor to reconstruct aggregate state by replaying all events in sequence from the event stream,
So that the actor has the correct current state before invoking the domain service (FR12).

**Acceptance Criteria:**

**Given** the AggregateActor passes tenant validation
**When** Step 3 (state rehydration) executes for a new aggregate (no prior events)
**Then** the current state is null (passed to domain service as `CurrentState? = null`)
**When** state rehydration executes for an existing aggregate
**Then** EventStreamReader reads all events from sequence 1 to current from the state store
**And** events are read using composite key pattern `{tenant}:{domain}:{aggId}:events:{seq}` (D1)
**And** events are replayed in strict sequence order to reconstruct current state
**And** only IActorStateManager is used for state store access (enforcement rule #6)
**And** full replay of 1,000 events completes within 100ms (NFR6)

### Story 3.5: Domain Service Registration & Invocation

As a **domain service developer**,
I want to register my domain service with EventStore by tenant and domain via configuration, and have the system invoke it with the command and current state,
So that my business logic processes commands without infrastructure concerns (FR22, FR23).

**Acceptance Criteria:**

**Given** a domain service is registered in the DAPR config store for a specific tenant and domain
**When** Step 4 (domain service invocation) executes
**Then** DomainServiceResolver looks up the service endpoint for the command's tenant + domain from the DAPR config store (D7)
**And** DaprDomainServiceInvoker calls the domain service via `DaprClient.InvokeMethodAsync` with the command and current state
**And** the domain service returns a DomainResult containing `List<DomainEvent>` (could be empty, events, or rejection events per D3)
**And** if the domain service returns an empty list, no state change occurs (valid per D3)
**And** if the domain service returns IRejectionEvent instances, the command status transitions to Rejected

### Story 3.6: Multi-Domain & Multi-Tenant Processing

As a **platform operator**,
I want the system to process commands for multiple independent domains within the same EventStore instance, and multiple tenants within the same domain with isolated event streams,
So that a single deployment serves diverse workloads (FR24, FR25).

**Acceptance Criteria:**

**Given** multiple domain services are registered for different tenant+domain combinations
**When** commands arrive for different domains (e.g., tenant1:orders, tenant1:inventory)
**Then** each command is routed to the correct domain service based on domain
**And** each domain maintains independent aggregate actors and event streams
**When** commands arrive for different tenants in the same domain (e.g., tenantA:orders, tenantB:orders)
**Then** each tenant's event streams are fully isolated via composite key strategy (FR15, FR28)
**And** actors for different tenants are independent even within the same domain
**And** adding a new tenant or domain does not require system restart (NFR20)

### Story 3.7: Event Persistence with Atomic Writes & Sequence Numbers

As a **system operator**,
I want events persisted in an append-only, immutable event store with strictly ordered gapless sequence numbers and atomic writes,
So that event streams are consistent and trustworthy (FR9, FR10, FR16).

**Acceptance Criteria:**

**Given** the domain service returns one or more events
**When** Step 5 (state machine execution) persists events
**Then** EventPersister writes events via IActorStateManager using write-once keys `{tenant}:{domain}:{aggId}:events:{seq}` (D1, enforcement rule #11)
**And** sequence numbers are strictly ordered and gapless within each aggregate stream (FR10)
**And** EventStore populates all 11 envelope metadata fields on each event (SEC-1)
**And** a command producing N events writes all N atomically -- never a partial subset (FR16)
**And** events are immutable after persistence -- never modified or deleted (FR9)
**And** event append latency is under 10ms at p99 (NFR3)
**And** ETag-based optimistic concurrency is used on aggregate metadata to detect conflicts

### Story 3.8: Storage Key Isolation & Composite Key Strategy

As a **security auditor**,
I want event streams for different tenants to use isolated storage keys that are inaccessible to each other at the state store level,
So that multi-tenant data isolation is enforced at the storage layer (FR15, FR28).

**Acceptance Criteria:**

**Given** events are persisted for multiple tenants
**When** I examine the state store keys
**Then** each event key includes the tenant prefix: `{tenant}:{domain}:{aggId}:events:{seq}`
**And** tenant A's keys are structurally disjoint from tenant B's keys
**And** no API or actor code path can read events across tenant boundaries
**And** snapshot keys follow the same tenant-scoped pattern: `{tenant}:{domain}:{aggId}:snapshot`
**And** the composite key strategy works with any DAPR-compatible state store supporting key-value operations (NFR27)

### Story 3.9: Snapshot Creation at Configurable Intervals

As a **system operator**,
I want snapshots of aggregate state created at configurable intervals (default every 100 events),
So that state rehydration remains fast regardless of total event count (FR13, NFR19).

**Acceptance Criteria:**

**Given** an aggregate has accumulated N events since the last snapshot (or since creation)
**When** N reaches the configured snapshot interval (default 100 per enforcement rule #15)
**Then** SnapshotManager captures the current aggregate state as a snapshot
**And** the snapshot is stored via IActorStateManager with key `{tenant}:{domain}:{aggId}:snapshot`
**And** the snapshot includes the sequence number it was taken at
**And** snapshot configuration is mandatory (enforcement rule #15) -- no aggregate can opt out
**And** the snapshot interval is configurable per domain via DAPR config store

### Story 3.10: State Reconstruction from Snapshot + Tail Events

As a **system operator**,
I want the system to reconstruct aggregate state from the latest snapshot plus only subsequent events,
So that actor cold activation remains fast regardless of total event history (FR14, NFR4).

**Acceptance Criteria:**

**Given** an aggregate has a snapshot at sequence 500 and events 501-520
**When** the actor rehydrates state
**Then** EventStreamReader loads the snapshot first, then reads only events from sequence 501 onward
**And** the reconstructed state is identical to a full replay from sequence 1 to 520
**And** actor cold activation with snapshot + tail events completes within 50ms at p99 (NFR4)
**And** state rehydration time remains constant regardless of total event count (NFR19)
**And** if no snapshot exists, full replay from sequence 1 is used as fallback

### Story 3.11: Actor State Machine & Checkpointed Stages

As a **system operator**,
I want the AggregateActor to use a checkpointed state machine tracking command lifecycle stages (Received -> Processing -> EventsStored -> EventsPublished -> terminal),
So that crash recovery resumes from the correct stage without duplicate persistence (NFR25).

**Acceptance Criteria:**

**Given** the AggregateActor processes a command through the 5-step delegation
**When** the state machine transitions between stages
**Then** each stage transition is checkpointed via IActorStateManager
**And** if the actor crashes after EventsStored but before EventsPublished, it resumes from EventsStored (not re-persisting events, NFR25)
**And** terminal states are: Completed, Rejected, PublishFailed, TimedOut
**And** command status is updated advisorily at each stage transition (enforcement rule #12)
**And** OpenTelemetry activities and structured logging are emitted at each stage transition

## Epic 4: Event Distribution & Dead-Letter Handling

After events are persisted, subscribers automatically receive them via pub/sub with CloudEvents 1.0, per-tenant-per-domain topic isolation, at-least-once delivery, and resilient persist-then-publish flow. Failed commands route to dead-letter topics with full context.

### Story 4.1: Event Publisher with CloudEvents 1.0

As a **subscriber system**,
I want persisted events published to a DAPR pub/sub component wrapped in CloudEvents 1.0 envelope format,
So that I receive events in a standard, interoperable format (FR17).

**Acceptance Criteria:**

**Given** events have been persisted by the AggregateActor (state machine at EventsStored)
**When** the EventPublisher publishes events
**Then** each event is wrapped in a CloudEvents 1.0 envelope with `type`, `source`, `id`, `time`, `datacontenttype`, and `data` fields
**And** publication uses `DaprClient.PublishEventAsync` with the appropriate topic
**And** the state machine transitions from EventsStored to EventsPublished upon successful publication
**And** pub/sub delivery latency is under 50ms at p99 (NFR5)
**And** OpenTelemetry activities span the publication step

### Story 4.2: Per-Tenant-Per-Domain Topic Isolation

As a **subscriber system**,
I want events published to per-tenant-per-domain topics (`{tenant}.{domain}.events`),
So that I only receive events for my authorized scope (FR19).

**Acceptance Criteria:**

**Given** events are published for tenant "acme" and domain "orders"
**When** EventPublisher determines the target topic
**Then** events are published to topic `acme.orders.events` (D6)
**And** events for tenant "globex" in the same domain go to `globex.orders.events`
**And** a subscriber to `acme.orders.events` never receives events from `globex.orders.events`
**And** topic names are derived from the AggregateIdentity canonical tuple
**And** the topic naming convention works with any DAPR-compatible pub/sub component (NFR28)

### Story 4.3: At-Least-Once Delivery & DAPR Retry Policies

As a **subscriber system**,
I want at-least-once delivery guarantee for published events with DAPR retry policies handling transient failures,
So that no events are silently lost during distribution (FR18, NFR22).

**Acceptance Criteria:**

**Given** events are published to the pub/sub topic
**When** a subscriber fails to acknowledge delivery
**Then** DAPR retry policies (configured in resiliency component) automatically retry delivery
**And** retry configuration uses DAPR-only retries -- no application-level retry logic (enforcement rule #4)
**And** after all retries are exhausted, the event follows the dead-letter path
**And** subscribers must handle idempotent processing (at-least-once means possible duplicates)

### Story 4.4: Persist-Then-Publish Resilience

As a **system operator**,
I want events to remain safe in the state store when the pub/sub system is temporarily unavailable, with automatic drain of the backlog on recovery,
So that events are never lost even during infrastructure outages (FR20, NFR24).

**Acceptance Criteria:**

**Given** events have been persisted (state machine at EventsStored)
**When** the pub/sub system is unavailable during publication
**Then** the state machine remains at EventsStored (events are safe in state store)
**And** the command status transitions to PublishFailed
**And** when the pub/sub recovers, a recovery mechanism drains unpublished events from the state store
**And** all events persisted during the outage are delivered to subscribers after recovery (NFR24)
**And** zero events are lost under any tested failure scenario (NFR22)
**And** the actor does not block waiting for pub/sub -- it transitions to PublishFailed and moves on

### Story 4.5: Dead-Letter Routing with Full Context

As an **operator**,
I want failed commands routed to a dead-letter topic with the full command payload, error details, and correlation context,
So that I can diagnose and recover from failures (FR8).

**Acceptance Criteria:**

**Given** a command fails processing (infrastructure error after DAPR retry exhaustion)
**When** the dead-letter handler activates
**Then** the full command payload is published to a dead-letter topic
**And** error details include: exception type, message (no stack trace per enforcement rules), failure stage
**And** correlation context includes: correlationId, causationId, tenantId, domain, aggregateId, commandType
**And** the dead-letter message includes enough information to replay the command via the replay endpoint (Story 2.7)
**And** the command status is updated to the appropriate terminal state (Rejected or PublishFailed)

## Epic 5: Multi-Tenant Security & Access Control Enforcement

Multi-tenant data isolation is enforced across all three layers (actor identity, DAPR policies, command metadata), with DAPR access control policies restricting service-to-service communication and pub/sub topic isolation ensuring authorized-only event delivery.

### Story 5.1: DAPR Access Control Policies

As a **security auditor**,
I want DAPR access control policies configured with per-app-id allow lists restricting which services can invoke which other services,
So that service-to-service communication is authenticated and authorized at the infrastructure level (FR34, NFR15).

**Acceptance Criteria:**

**Given** DAPR access control policy configuration files exist
**When** the system is deployed with these policies
**Then** CommandApi can invoke actor services and domain services (D4)
**And** actor services can invoke domain services and state store
**And** domain services cannot directly invoke actor services or CommandApi
**And** no service can bypass the DAPR sidecar for inter-service communication
**And** unauthorized service-to-service calls are rejected by DAPR with appropriate error
**And** policy violations are logged with source app-id, target app-id, and operation

### Story 5.2: Data Path Isolation Verification

As a **security auditor**,
I want verification that commands for one tenant are never routed to another tenant's domain service or actor,
So that the data path isolation guarantee is validated end-to-end (FR27, NFR13).

**Acceptance Criteria:**

**Given** commands arrive for multiple tenants (tenantA:orders, tenantB:orders)
**When** the system routes commands through actors to domain services
**Then** tenantA's commands are processed only by tenantA's actor instances
**And** tenantA's commands invoke domain services only with tenantA's context
**And** three-layer isolation is enforced: actor identity (Story 3.3), DAPR policies (Story 5.1), command metadata validation
**And** failure at one isolation layer does not compromise isolation at other layers (NFR13)
**And** isolation verification tests exist as automated test cases

### Story 5.3: Pub/Sub Topic Isolation Enforcement

As a **security auditor**,
I want pub/sub topic isolation enforced so that event subscribers only receive events from tenants they are authorized to access,
So that cross-tenant event leakage is impossible (FR29).

**Acceptance Criteria:**

**Given** events are published to per-tenant-per-domain topics (Story 4.2)
**When** a subscriber subscribes to a tenant's topic
**Then** DAPR pub/sub scoping rules restrict which app-ids can subscribe to which topics
**And** a subscriber authorized for tenantA cannot subscribe to tenantB's topics
**And** subscription scoping is configured via DAPR component metadata (not application code)
**And** unauthorized subscription attempts are rejected by DAPR

### Story 5.4: Security Audit Logging & Payload Protection

As a **security auditor**,
I want comprehensive security audit logging for failed authentication/authorization attempts and enforcement that event payload data never appears in logs,
So that security incidents are traceable while sensitive data is protected (SEC-4, SEC-5, NFR11, NFR12).

**Acceptance Criteria:**

**Given** the system processes commands with security checks at multiple layers
**When** an authentication or authorization failure occurs at any layer
**Then** the failure is logged with: timestamp, correlation ID, source IP, attempted tenant, attempted command type, failure reason, failure layer
**And** the JWT token itself is never logged (NFR11)
**And** event payload data never appears in any log output (SEC-5, NFR12)
**And** extension metadata is sanitized at the API gateway: max size enforced, character validation applied, injection patterns rejected (SEC-4)
**And** secrets (connection strings, JWT signing keys, DAPR credentials) never appear in logs or source control (NFR14)
**And** payload protection is enforced at the framework level (not relying on individual developer discipline)

## Epic 6: Observability, Health & Operational Readiness

An operator can trace any command through the full pipeline via OpenTelemetry, diagnose failures via structured logs with correlation IDs, check system health/readiness, and trace dead-letter commands back to their originating requests.

### Story 6.1: End-to-End OpenTelemetry Trace Instrumentation

As an **operator**,
I want complete OpenTelemetry trace instrumentation spanning the full command lifecycle (Received -> Processing -> EventsStored -> EventsPublished -> Completed),
So that I can visualize the entire command flow in any OTLP-compatible collector (FR35).

**Acceptance Criteria:**

**Given** a command is submitted and processed through the full pipeline
**When** I view traces in the Aspire dashboard (or Jaeger, Grafana/Tempo)
**Then** a single distributed trace spans all stages: API receipt, MediatR pipeline, actor activation, domain invocation, event persistence, event publication
**And** each stage has a named activity matching the architecture pattern (e.g., `EventStore.CommandApi.Submit`, `EventStore.Actor.Process`, `EventStore.Actor.PersistEvents`, `EventStore.Actor.PublishEvents`)
**And** trace context (correlation ID, causation ID) propagates across all spans
**And** traces are exportable to any OTLP-compatible collector (NFR31)
**And** trace instrumentation adds minimal overhead (within NFR2 200ms e2e budget)

### Story 6.2: Structured Logging Completeness Verification

As an **operator**,
I want structured logs emitted at each stage of the command processing pipeline with all required fields (correlation ID, causation ID, tenant, domain, command type, stage),
So that I can diagnose issues using log queries without needing traces (FR36).

**Acceptance Criteria:**

**Given** a command flows through the pipeline
**When** I query structured logs
**Then** each pipeline stage emits a log entry with: correlationId, causationId, tenantId, domain, commandType, stage, timestamp
**And** log levels follow convention: Information for normal flow, Warning for retries/recoverable issues, Error for failures
**And** event payload data never appears in log output (enforcement rule #3, SEC-5, NFR12)
**And** log field completeness is verified for every defined pipeline stage
**And** logs are machine-parseable (structured JSON format)

### Story 6.3: Dead-Letter to Origin Tracing

As an **operator**,
I want to trace a failed command from the dead-letter topic back to its originating API request using the correlation ID,
So that I can diagnose the full failure chain end-to-end (FR37).

**Acceptance Criteria:**

**Given** a command has been routed to the dead-letter topic (Story 4.5)
**When** I take the correlation ID from the dead-letter message
**Then** I can query structured logs filtered by that correlation ID to see every pipeline stage the command passed through
**And** I can find the originating API request (source IP, timestamp, user identity) via the same correlation ID
**And** I can view the OpenTelemetry trace for the full lifecycle of that correlation ID
**And** the dead-letter message itself contains the correlation ID, failure stage, and error details

### Story 6.4: Health Check Endpoints

As an **operator**,
I want health check endpoints indicating DAPR sidecar, state store, and pub/sub connectivity status,
So that I can monitor infrastructure dependencies and configure load balancer probes (FR38).

**Acceptance Criteria:**

**Given** the CommandApi is running
**When** I GET `/health`
**Then** the response indicates the health status of: DAPR sidecar connectivity, state store availability, pub/sub availability
**And** DaprSidecarHealthCheck verifies sidecar is responsive (with 5s timeout per enforcement rule #14)
**And** DaprConfigStoreHealthCheck verifies config store is accessible
**And** each dependency check runs independently (one failing doesn't block others)
**And** response format follows ASP.NET Core health check conventions (Healthy/Degraded/Unhealthy)
**And** health checks are registered via ServiceDefaults

### Story 6.5: Readiness Check Endpoints

As an **operator**,
I want readiness check endpoints indicating all dependencies are healthy and the system is accepting commands,
So that I can gate traffic routing in orchestrated environments (FR39).

**Acceptance Criteria:**

**Given** the CommandApi is running
**When** I GET `/alive`
**Then** the response indicates whether the system is ready to accept commands
**And** ReadinessCheck combines all dependency health checks (sidecar, state store, pub/sub, config store)
**And** the system reports not-ready if any critical dependency is unhealthy
**And** readiness checks are suitable for Kubernetes readiness probes and load balancer health checks
**And** ServiceDefaults configuration is complete: resilience policies, telemetry exporters, health endpoints all properly wired
**And** the system achieves operational readiness for 99.9%+ availability target (NFR21)

## Epic 7: Sample Application, Testing, CI/CD & Deployment

A new developer references a working sample domain service, runs integration and contract tests at all three tiers, and a DevOps engineer deploys to any environment via Aspire publishers with zero application code changes.

### Story 7.1: Sample Counter Domain Service

As a **new developer**,
I want a working sample Counter domain service implementing the pure function programming model with commands (IncrementCounter, DecrementCounter, ResetCounter), events (CounterIncremented, CounterDecremented, CounterReset), a rejection event (CounterCannotGoNegative), and state (CounterState),
So that I have a concrete reference implementation to learn from (FR41).

**Acceptance Criteria:**

**Given** the sample Counter domain service project exists
**When** I review the CounterProcessor implementation
**Then** it implements IDomainProcessor with the pure function contract `(Command, CurrentState?) -> List<DomainEvent>`
**And** IncrementCounter produces CounterIncremented event
**And** DecrementCounter produces CounterDecremented event (or CounterCannotGoNegative rejection if counter is 0)
**And** ResetCounter produces CounterReset event
**And** CounterState tracks the current count value
**And** the domain service is registered in the DAPR config store for a sample tenant and domain
**And** the sample service demonstrates all three D3 outcomes: events, rejection events, empty list (no-op)

### Story 7.2: Local DAPR Component Configurations

As a **developer**,
I want local DAPR component configuration files (Redis state store, Redis pub/sub, resiliency policies, access control policies) that work out-of-the-box with the Aspire AppHost,
So that I can run the complete system locally without manual infrastructure setup (FR43).

**Acceptance Criteria:**

**Given** the local DAPR component configs exist in the project
**When** I run `dotnet aspire run`
**Then** Redis state store is configured and connected for event persistence and command status
**And** Redis pub/sub is configured for event distribution
**And** resiliency policies define retry, timeout, and circuit breaker behaviors (enforcement rule #4)
**And** access control policies match the D4 allow list specification
**And** switching between local configs requires zero application code changes (NFR29)

### Story 7.3: Production DAPR Component Configurations

As a **DevOps engineer**,
I want production-ready DAPR component configuration templates for multiple backends (PostgreSQL state store, Cosmos DB state store, RabbitMQ pub/sub, Kafka pub/sub),
So that I can deploy to different environments by changing only DAPR config files (FR43, NFR29).

**Acceptance Criteria:**

**Given** production DAPR component config templates exist in a `deploy/` directory
**When** I swap local Redis configs for PostgreSQL state store and RabbitMQ pub/sub configs
**Then** the system functions correctly with zero application code changes, zero recompilation, zero redeployment of application containers (NFR29)
**And** templates exist for: PostgreSQL state store, Cosmos DB state store, RabbitMQ pub/sub, Kafka pub/sub, Azure Service Bus pub/sub
**And** each template includes connection string placeholders and documentation for required secrets
**And** secrets are never stored in configuration files committed to source control (NFR14)

### Story 7.4: Integration Tests with DAPR Test Containers (Tier 2)

As a **developer**,
I want integration tests that validate the actor processing pipeline using DAPR test containers,
So that I can verify server-side behavior with real DAPR infrastructure in CI (FR46).

**Acceptance Criteria:**

**Given** the Server.Tests project exists with DAPR test container configuration
**When** I run `dotnet test` on Server.Tests
**Then** tests spin up DAPR sidecar and Redis containers via test containers
**And** tests validate: actor activation, command routing, event persistence, snapshot creation, state rehydration
**And** tests verify optimistic concurrency conflict detection
**And** tests verify tenant isolation at the actor and storage level
**And** tests run in CI without manual infrastructure setup
**And** the system functions correctly with Redis state store (NFR27) and Redis pub/sub (NFR28)

### Story 7.5: End-to-End Contract Tests with Aspire Topology (Tier 3)

As a **developer**,
I want end-to-end contract tests that validate the full command lifecycle across the complete Aspire topology (CommandApi -> Actor -> Domain Service -> State Store -> Pub/Sub),
So that I can verify the entire system works correctly before release (FR47).

**Acceptance Criteria:**

**Given** the IntegrationTests project exists with Aspire test host configuration
**When** I run `dotnet test` on IntegrationTests
**Then** the full Aspire topology starts (CommandApi, sample domain service, Redis, DAPR sidecars)
**And** tests submit commands via the REST API and verify the complete lifecycle: 202 Accepted -> status tracking -> events persisted -> events published -> Completed
**And** tests verify JWT authentication and authorization flow
**And** tests verify RFC 7807 error responses for invalid/unauthorized requests
**And** tests verify dead-letter routing for simulated failures
**And** tests verify infrastructure portability (same tests pass on Redis and PostgreSQL via config swap)

### Story 7.6: CI/CD Pipeline & NuGet Publishing

As a **DevOps engineer**,
I want a GitHub Actions CI/CD pipeline that builds, tests (all 3 tiers), and publishes NuGet packages on release tags,
So that the project has automated quality gates and package distribution (D10).

**Acceptance Criteria:**

**Given** GitHub Actions workflow files exist in `.github/workflows/`
**When** a pull request is opened or updated
**Then** the pipeline runs: build -> Tier 1 unit tests -> Tier 2 integration tests -> Tier 3 contract tests
**And** DAPR integration tests run in CI with test containers
**When** a release tag is pushed (e.g., `v1.0.0`)
**Then** the pipeline builds, tests, packs NuGet packages, and publishes to NuGet.org (or configured feed)
**And** MinVer derives the package version from the Git tag (D9)
**And** all 5 NuGet packages are published with the same version (monorepo single-version strategy)

### Story 7.7: Aspire Publisher Deployment Manifests

As a **DevOps engineer**,
I want to generate deployment manifests for Docker Compose, Kubernetes, and Azure Container Apps via Aspire publishers,
So that I can deploy to any target environment without custom deployment scripts (FR44, NFR32).

**Acceptance Criteria:**

**Given** the Aspire AppHost is configured with all resources
**When** I run the Aspire publisher for Docker Compose
**Then** a valid `docker-compose.yml` is generated with all services, volumes, and networking
**When** I run the Aspire publisher for Kubernetes
**Then** valid K8s manifests (Deployments, Services, ConfigMaps) are generated
**When** I run the Aspire publisher for Azure Container Apps
**Then** valid ACA deployment artifacts are generated
**And** generated manifests include DAPR annotations/configurations
**And** no custom deployment scripts are required (NFR32)
**And** environment-specific configuration is injected via DAPR component files, not application code

### Story 7.8: Domain Service Hot Reload Validation

As a **developer**,
I want to modify domain service logic, restart only the domain service process, and verify updated behavior without restarting the EventStore server or the full Aspire topology,
So that my development inner loop is fast and predictable (UX critical experience: "My inner loop is fast").

**Acceptance Criteria:**

**Given** the full Aspire topology is running (EventStore, sample domain service, DAPR sidecars, state store, pub/sub)
**When** I modify the sample Counter domain service logic (e.g., change CounterIncremented event payload)
**And** I restart only the domain service process (not EventStore, not the Aspire AppHost)
**Then** subsequent commands sent to the Command API invoke the updated domain service logic
**And** the EventStore server continues running without interruption during the domain service restart
**And** DAPR service invocation automatically discovers the restarted domain service instance
**And** the domain service restart completes in under 5 seconds
**And** no commands are lost during the brief restart window (commands received during restart are retried via DAPR resiliency policies)
**And** the Aspire dashboard continues showing all resources without requiring a full topology restart
