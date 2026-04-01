---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
  - step-03-create-stories
  - step-04-final-validation
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux-design-specification.md
  - docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md
  - docs/superpowers/plans/2026-03-15-server-managed-projection-builder.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-15.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-15-eventstore-documentation-refresh.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-15-counter-command-buttons.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-15-projection-builder.md
---

# Hexalith.EventStore - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Hexalith.EventStore, decomposing the requirements from the PRD, UX Design, Architecture, superpowers specs, and sprint change proposals into implementable stories.

## Requirements Inventory

### Functional Requirements

**Command Processing (FR1-FR8, FR49)**

- FR1: An API consumer can submit a command to the EventStore via a REST endpoint with message ID (ULID), aggregate ID (ULID), command type (kebab `{domain}-{command}-v{ver}`), and payload
- FR2: The system can validate a submitted command for structural completeness (required fields: messageId, aggregateId, commandType, payload; valid ULID format; valid kebab format for commandType via MessageType value object; well-formed JSON)
- FR3: The system can route a command to the correct aggregate actor based on the identity scheme (`tenant:domain:aggregate-id`)
- FR4: An API consumer can receive a correlation ID upon command submission for tracking the command lifecycle. Defaults to client-supplied messageId (ULID) if not provided. Also serves as idempotency key
- FR5: An API consumer can query the processing status of a previously submitted command using its correlation ID
- FR6: An operator can replay a previously failed command via the Command API after root cause is fixed
- FR7: The system can reject duplicate commands targeting an aggregate with optimistic concurrency conflict
- FR8: The system can route failed commands to a dead-letter topic with full command payload, error details, and correlation context
- FR49: The system can detect and reject duplicate commands by tracking processed command message IDs (client-generated ULIDs) per aggregate, returning idempotent success response for already-processed commands

**Event Management (FR9-FR16, FR65, FR66)**

- FR9: The system can persist events in an append-only, immutable event store where events are never modified or deleted
- FR10: The system can assign strictly ordered, gapless sequence numbers to events within a single aggregate stream. Cross-aggregate ordering explicitly not guaranteed
- FR11: The system can wrap each event in a 14-field metadata envelope (event message ID, aggregate ID, aggregate type, tenant, sequence number, global position, timestamp, correlation ID, causation ID, user identity, event type, domain service version, metadata version, extension bag)
- FR12: The system can reconstruct aggregate state by replaying all events from sequence 1 to current
- FR13: The system can create snapshots at administrator-configured event count intervals (default: every 100 events, configurable per tenant-domain pair)
- FR14: The system can reconstruct aggregate state from latest snapshot plus subsequent events, producing identical state to full replay
- FR15: The system can store events using a composite key strategy including tenant, domain, and aggregate identity for isolation
- FR16: The system can enforce atomic event writes -- 0 or N events as a single transaction, never partial
- FR65: Event metadata envelope includes `metadataVersion` field (integer, starting at 1) for schema change detection
- FR66: The system can mark an aggregate as terminated (tombstoned) via a terminal event. Terminated aggregates reject all subsequent commands with a domain rejection event

**Event Distribution (FR17-FR20, FR67)**

- FR17: The system can publish persisted events to subscribers via pub/sub using CloudEvents 1.0 envelope format
- FR18: The system can deliver events to subscribers with at-least-once delivery guarantee
- FR19: The system can publish events to per-tenant-per-domain topics
- FR20: The system can continue persisting events when pub/sub is temporarily unavailable, draining backlog on recovery
- FR67: The system can apply backpressure when aggregate command queues exceed configurable depth threshold (default: 100 pending commands per aggregate), returning HTTP 429 with Retry-After header

**Domain Service Integration (FR21-FR25)**

- FR21: A domain service developer can implement a domain processor as a pure function: `(Command, CurrentState?) -> DomainResult`. EventStore handles all metadata enrichment
- FR22: A domain service developer's domain service is automatically routed by convention (AppId = domain name, method "process") with zero configuration. Routing overridable via static registrations or DAPR config store (opt-in) for complex scenarios
- FR23: The system can invoke a registered domain service when processing a command, passing command and current aggregate state
- FR24: The system can process commands for at least 2 independent domains within the same instance
- FR25: The system can process commands for at least 2 tenants within the same domain, each with isolated event streams

**Identity & Multi-Tenancy (FR26-FR29)**

- FR26: The system can derive actor IDs, event stream keys, and pub/sub topics from canonical identity tuple (`tenant:domain:aggregate-id`)
- FR27: The system can enforce data path isolation so commands for one tenant never route to another
- FR28: The system can enforce storage key isolation so event streams are inaccessible cross-tenant
- FR29: The system can enforce pub/sub topic isolation so subscribers only receive authorized events

**Security & Authorization (FR30-FR34)**

- FR30: An API consumer can authenticate with the Command API using a JWT token
- FR31: The system can authorize command submissions based on JWT claims for tenant, domain, and command type
- FR32: The system can reject unauthorized commands at the API gateway before processing
- FR33: The system can validate that a command's tenant matches the authenticated user's authorized tenants at the actor level
- FR34: The system can enforce service-to-service access control via DAPR policies

**Observability & Operations (FR35-FR39)**

- FR35: The system can emit OpenTelemetry traces spanning the full command lifecycle
- FR36: The system can emit structured logs with correlation and causation IDs at each pipeline stage
- FR37: An operator can trace a failed command from dead-letter topic back to its originating request
- FR38: The system can expose health check endpoints (DAPR sidecar, state store, pub/sub connectivity)
- FR39: The system can expose readiness check endpoints indicating all dependencies healthy

**Developer Experience & Deployment (FR40-FR48)**

- FR40: A developer can start the complete system with a single Aspire command
- FR41: A developer can reference a sample domain service as a working example
- FR42: A developer can install EventStore client packages via NuGet with zero-configuration quickstart via convention-based `AddEventStore()` and auto-discovery
- FR43: A DevOps engineer can deploy to different environments by changing only DAPR component configuration
- FR44: A DevOps engineer can generate deployment manifests for Docker Compose, Kubernetes, or Azure Container Apps via Aspire publishers
- FR45: A developer can run unit tests against domain service pure functions without DAPR runtime
- FR46: A developer can run integration tests using DAPR test containers
- FR47: A developer can run end-to-end contract tests validating the full command lifecycle
- FR48: A domain service developer can implement a domain aggregate by inheriting from EventStoreAggregate with typed Apply methods, with convention-based DAPR resource naming

**Query Pipeline & Projection Caching — v2 (FR50-FR64)**

- FR50: The system can route incoming query messages to query actors using a 3-tier routing model: (1) queries with EntityId route to `{QueryType}-{TenantId}-{EntityId}`, (2) queries without EntityId but with payload route to `{QueryType}-{TenantId}-{Checksum}`, (3) queries without EntityId and empty payload route to `{QueryType}-{TenantId}`
- FR51: The system can maintain one ETag actor per `{ProjectionType}-{TenantId}` storing a self-routing ETag, regenerated on every projection change notification
- FR52: A domain service developer can notify EventStore of a projection change via `NotifyProjectionChanged(projectionType, tenantId, entityId?)` NuGet helper
- FR53: The query REST endpoint can perform an ETag pre-check by decoding the self-routing ETag from `If-None-Match` header, returning HTTP 304 if matched without activating query actor
- FR54: A query actor can serve as an in-memory page cache (second gate) with no state store persistence. Cold call forwards to microservice; warm calls check ETag actor
- FR55: The system can broadcast a signal-only "changed" message to connected SignalR clients when a projection's ETag is regenerated
- FR56: The system can host a SignalR hub inside EventStore server, using a Redis backplane for multi-instance distribution
- FR57: A query contract library (NuGet) can define mandatory query metadata fields (Domain, QueryType, TenantId) and optional fields (EntityId) as typed static members
- FR58: The system can invalidate all cached query results for a projection+tenant pair on any projection change notification (coarse invalidation)
- FR59: The SignalR client helper (NuGet) can automatically rejoin SignalR groups on connection recovery
- FR60: Documentation and sample can provide 3 reference patterns for SignalR "changed" signal: (1) toast notification, (2) silent data reload, (3) selective component refresh
- FR61: The system can encode self-routing ETags in format `{base64url(projectionType)}.{guid}` and decode them. Missing/malformed ETags treated as cache misses
- FR62: `IQueryResponse<T>` enforces at compile time that every query response includes non-empty `ProjectionType`
- FR63: A query actor can discover its projection type mapping at runtime from the microservice's first response, resetting on actor deactivation
- FR64: Documentation recommends short projection type names for compact ETags

### NonFunctional Requirements

**Performance (NFR1-NFR8)**

- NFR1: Command submission via REST API must complete (return 202 Accepted) within 50ms at p99
- NFR2: End-to-end command lifecycle must complete within 200ms at p99
- NFR3: Event append latency must be under 10ms at p99
- NFR4: Actor cold activation with state rehydration must complete within 50ms at p99
- NFR5: Pub/sub delivery must complete within 50ms at p99
- NFR6: Full aggregate state reconstruction from 1,000 events must complete within 100ms
- NFR7: System must support at least 100 concurrent command submissions per second per instance
- NFR8: DAPR sidecar overhead per building block call must not exceed 2ms at p99

**Security (NFR9-NFR15)**

- NFR9: All API communication encrypted via TLS 1.2+
- NFR10: JWT tokens validated for signature, expiry, and issuer on every request
- NFR11: Failed auth attempts logged with request metadata (without JWT token itself)
- NFR12: Event payload data must never appear in log output; only envelope metadata may be logged
- NFR13: Multi-tenant data isolation enforced at all three layers (actor identity, DAPR policies, command metadata)
- NFR14: Secrets must never be stored in application code or committed configuration files
- NFR15: Service-to-service communication authenticated via DAPR access control policies

**Scalability (NFR16-NFR20)**

- NFR16: Horizontal scaling by adding replicas, with DAPR actor placement distributing aggregates
- NFR17: Support at least 10,000 active aggregates per instance without latency degradation
- NFR18: Support at least 10 tenants with full isolation and no cross-tenant performance interference
- NFR19: Event stream growth bounded by snapshot strategy -- rehydration time remains constant
- NFR20: Adding a new tenant or domain must not require restart -- DAPR config changes take effect dynamically

**Reliability (NFR21-NFR26)**

- NFR21: 99.9%+ availability with HA DAPR control plane and multi-replica deployment
- NFR22: Zero events may be lost under any tested failure scenario
- NFR23: After state store recovery, resume processing from last checkpoint with deterministic replay
- NFR24: After pub/sub recovery, deliver all events persisted during outage via DAPR retry policies
- NFR25: Actor crash after event persistence but before pub/sub must not result in duplicate event persistence
- NFR26: Optimistic concurrency conflicts detected and reported (409 Conflict)

**Integration (NFR27-NFR32)**

- NFR27: Function correctly with any DAPR-compatible state store supporting key-value + ETag concurrency (validated: Redis, PostgreSQL)
- NFR28: Function correctly with any DAPR-compatible pub/sub supporting CloudEvents 1.0 + at-least-once (validated: RabbitMQ, Azure Service Bus)
- NFR29: Switching backends requires only DAPR component YAML changes -- zero code, zero recompilation
- NFR30: Domain services invocable via DAPR service invocation over HTTP -- no language/framework constraints beyond pure function contract
- NFR31: OpenTelemetry exportable to any OTLP-compatible collector (validated: Aspire dashboard, Jaeger, Grafana/Tempo)
- NFR32: Deployable via Aspire publishers to Docker Compose, Kubernetes, Azure Container Apps without custom scripts

**Rate Limiting (NFR33-NFR34)**

- NFR33: Per-tenant rate limiting with configurable threshold (default: 1,000 commands/minute/tenant), returning 429 + Retry-After
- NFR34: Per-consumer rate limiting with configurable threshold (default: 100 commands/second/consumer), returning 429 + Retry-After

**Query Pipeline Performance — v2 (NFR35-NFR39)**

- NFR35: ETag pre-check must complete within 5ms at p99 for warm ETag actors
- NFR36: Query actor cache hit must complete within 10ms at p99
- NFR37: Query actor cache miss must complete within 200ms at p99
- NFR38: SignalR "changed" signal delivery must complete within 100ms at p99
- NFR39: Query pipeline must support at least 1,000 concurrent query requests per second per instance

### Additional Requirements

**Architecture Decisions (D1-D12)**

- D1: Event Storage Strategy -- Single-key-per-event with actor-level ACID writes. Key pattern: `{tenant}:{domain}:{aggId}:events:{seq}`, metadata at `{tenant}:{domain}:{aggId}:metadata`. ETag-based optimistic concurrency on metadata key
- D2: Command Status Storage -- Dedicated state store key `{tenant}:{correlationId}:status`. Checkpointed state machine: Received -> Processing -> EventsStored -> EventsPublished -> Completed | Rejected | PublishFailed | TimedOut. 24-hour default TTL
- D3: Domain Service Error Contract -- Errors as events (rejection events via `IRejectionEvent` marker interface). Infrastructure failures only go to dead-letter after DAPR retry exhaustion. Domain services MUST maintain backward-compatible deserialization for all event types
- D4: DAPR Access Control -- Per-app-id allow list. EventStore can invoke actor services and domain services
- D5: Error Response Format -- RFC 7807 Problem Details + extensions (correlationId, tenantId, validationErrors)
- D6: Pub/Sub Topic Naming -- `{tenant}.{domain}.events` dot-separated pattern
- D7: Domain Service Invocation -- DAPR service invocation (`DaprClient.InvokeMethodAsync`) with mTLS and resiliency policies
- D8: Rate Limiting -- ASP.NET Core built-in `RateLimiting` middleware with `SlidingWindowRateLimiter`, per-tenant from JWT claims
- D9: Package Versioning -- semantic-release (automated SemVer from Conventional Commits, single version via `-p:Version=`)
- D10: CI/CD Pipeline -- GitHub Actions (build+test+commitlint on PR, semantic-release on merge to main)
- D11: E2E Security Testing Infrastructure -- Keycloak in Aspire with realm-as-code (`hexalith-realm.json`), 5 test users, port 8180, zero auth code changes. E2E tests acquire real OIDC tokens
- D12: ULID Everywhere -- `Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId()` as single ULID generation mechanism. Contracts depends on `Hexalith.Commons.UniqueIds`, not raw ULID library. String-typed ULID fields throughout

**Architecture Enforcement Rules (17 Rules)**

- Rule 1: C# naming conventions exactly as documented
- Rule 2: Feature folders, not type-based folders
- Rule 3: MediatR pipeline order: logging -> validation -> auth -> handler
- Rule 4: Never add custom retry logic -- DAPR resiliency only
- Rule 5: Never log event payload data -- envelope metadata only
- Rule 6: IActorStateManager for all actor state operations -- never DaprClient bypass
- Rule 7: ProblemDetails for all API error responses -- never custom shapes
- Rule 8: Events named in past tense (state-change) or past tense negative (rejection)
- Rule 9: correlationId in every structured log and OpenTelemetry activity
- Rule 10: Services registered via Add* extension methods -- never inline
- Rule 11: Event store keys are write-once -- never updated or deleted
- Rule 12: Command status writes are advisory -- never block pipeline
- Rule 13: No stack traces in production error responses
- Rule 14: DAPR sidecar call timeout is 5 seconds
- Rule 15: Snapshot configuration is mandatory (default 100 events)
- Rule 16: E2E security tests use real Keycloak OIDC tokens -- never synthetic JWTs for runtime verification
- Rule 17: Convention-derived resource names use kebab-case; type suffix stripping is automatic

**Architecture Security Constraints (SEC-1 to SEC-5)**

- SEC-1: EventStore owns all 11 envelope metadata fields (at EventPersister after domain service returns)
- SEC-2: Tenant validation BEFORE state rehydration (at Actor Step 2 TenantValidator)
- SEC-3: Command status queries are tenant-scoped (CommandStatusController JWT tenant match)
- SEC-4: Extension metadata sanitized at API gateway (CorrelationIdMiddleware / validation pipeline)
- SEC-5: Event payload data never in logs (LoggingBehavior + structured logging framework)

**Starter Template: Custom Solution from Individual Templates (not a pre-built starter)**

**Implementation Sequence:**
1. Contracts package first
2. Testing package early
3. Server package
4. EventStore host
5. Client package
6. Sample domain service
7. Aspire + AppHost
8. Deploy configs
9. CI/CD

**Server-Managed Projection Builder (Mode B) — from superpowers docs**

- EventReplayProjectionActor: concrete implementation of CachingProjectionActor, registered as "ProjectionActor", implements IProjectionActor (read) + IProjectionWriteActor (write)
- Projection Checkpoint Tracker: tracks last-sent event sequence per aggregate in DAPR state store, updated only after successful UpdateProjectionAsync
- Immediate Trigger (RefreshIntervalMs=0, default): fire-and-forget background task after event persistence, non-blocking
- Background Poller (RefreshIntervalMs>0): IHostedService polling per aggregate at configured interval
- Domain Service `/project` endpoint convention: per-aggregate granularity, `ProjectionRequest -> ProjectionResponse { ProjectionType, State(JSON) }`
- ProjectionEventDto wire-format DTO in Contracts: EventTypeName, Payload, SerializationFormat, SequenceNumber, Timestamp, CorrelationId
- Convention-based discovery: domain services registered for commands that also expose `/project` get automatic projection wiring
- Event reading via `AggregateActor.GetEventsAsync(long fromSequence)`: read-only method, encapsulates DAPR key format
- Error handling: stale projections acceptable (eventual consistency), failures logged and retried on next trigger

**Sprint Change Proposals (2026-03-15)**

- SCP-ULID: Use `Hexalith.Commons.UniqueIds` instead of custom `UlidId` value object. String-typed ULID fields. Affects Stories 1.1 and 1.7 ACs
- SCP-Docs: Documentation refresh -- align README, reference docs, package guide (5->6 packages with SignalR), roadmap, and planning artifacts with actual implemented query/projection/SignalR surface
- SCP-Buttons: Add `CounterCommandForm` component to NotificationPattern.razor and SilentReloadPattern.razor (2 one-line edits)
- SCP-Projection: Add Stories 8.9, 8.10, 8.11 to Epic 8 for server-managed projection builder. Design spec and implementation plan already written

### UX Design Requirements

**REST API Error Experience (v1)**

- UX-DR1: RFC 7807 ProblemDetails on ALL error responses (4xx/5xx) with `type`, `title`, `status`, `detail`, `instance` fields. Content-Type: `application/problem+json`
- UX-DR2: `correlationId` extension field present on 400, 403, 409 responses. Absent on 401, 503 (pre-pipeline rejections)
- UX-DR3: `errors` object with JSON path keys (e.g., `payload.amount`) on 400 validation failures, with human-readable messages per field
- UX-DR4: `WWW-Authenticate` header on 401 responses per RFC 6750 with `realm`, `error`, and `error_description`
- UX-DR5: `Retry-After` header on 409 (1s interval) and 503 (30s interval) responses
- UX-DR6: No event sourcing terminology in any error response -- "aggregate", "event stream", "actor", "DAPR", "sidecar" never appear in ProblemDetails
- UX-DR7: Error `type` URIs are stable, unique per error category, and resolve to human-readable documentation pages
- UX-DR8: Expired vs. missing JWT distinguished with two different `type` URIs (`authentication-required` vs. `token-expired`)
- UX-DR9: 403 responses name the specific rejected tenant but do NOT enumerate authorized tenants (information disclosure prevention)
- UX-DR10: 409 Conflict for optimistic concurrency -- no state leakage (no sequence numbers or conflicting command details exposed)
- UX-DR11: 503 for DAPR sidecar unavailability -- no internal component naming ("command processing pipeline", not "DAPR sidecar")

**REST API Success Experience (v1)**

- UX-DR12: OpenAPI 3.1 spec with Swagger UI at `/swagger` on running EventStore, with grouped endpoints
- UX-DR13: Pre-populated example payloads in OpenAPI spec -- Swagger UI "Try it out" pre-fills a valid Counter domain command
- UX-DR14: Command status endpoint at `/api/commands/status/{correlationId}` returning current lifecycle state with timestamp
- UX-DR15: `202 Accepted` response with `Location` header (pointing to status endpoint) + `Retry-After: 1` header

**Developer SDK UX (v1)**

- UX-DR16: `CommandStatus` enum with 8 states: Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut
- UX-DR17: Registration via `AddEventStoreClient()` single extension method on `IServiceCollection`
- UX-DR18: `IDomainProcessor<TCommand, TState>` interface with pure function contract
- UX-DR19: XML documentation on all public types for IntelliSense discoverability
- UX-DR20: Minimal public surface area -- only domain service developer-facing types are public; internal pipeline types are `internal`

**CLI / Aspire Onboarding (v1)**

- UX-DR21: `dotnet aspire run` starts full topology (EventStore + sample domain service + DAPR sidecars visible in Aspire dashboard)
- UX-DR22: Clear prerequisite error messages -- missing Docker, .NET SDK, or DAPR produces actionable error with installation link, not stack trace
- UX-DR23: Sample Counter domain service as working reference: `IncrementCounter` command, `CounterIncremented` event, `CounterState`
- UX-DR24: OpenTelemetry traces for full pipeline visible in Aspire Traces tab
- UX-DR25: Domain service hot reload -- modify processor, restart only domain service (~2s), test without full topology restart

**Cross-Surface Consistency (v1)**

- UX-DR26: Shared terminology (Command, Event, Aggregate, Tenant, Domain, Correlation ID) across OpenAPI schema names, SDK type names, structured log fields, error messages
- UX-DR27: Shared lifecycle model -- `CommandStatus` values identical in API `status` field and SDK enum
- UX-DR28: Structured logs with correlation/causation IDs at every entry, filterable by correlation ID in Aspire
- UX-DR29: Status color semantics documented in OpenAPI descriptions

**Documentation (v1)**

- UX-DR30: Quick start guide (3 pages maximum) -- clone to first command in under 10 minutes
- UX-DR31: API reference embedded at `/swagger` -- no separate docs site needed
- UX-DR32: Error reference pages at `type` URIs -- each explains the error, shows an example, suggests resolution
- UX-DR33: Progressive documentation structure: quick start (assumes DDD knowledge), concepts (newcomers), reference (deep dives)

**v2 Blazor Dashboard UX (deferred but designed for)**

- UX-DR34: Blazor Fluent UI V4 design system with built-in adaptive design tokens
- UX-DR35: Progressive drill-down navigation: system -> tenant -> domain -> aggregate -> event
- UX-DR36: Command lifecycle visualization as horizontal pipeline stages (inspired by GitHub Actions)
- UX-DR37: Event stream time-travel explorer -- "what was the state at time T?"
- UX-DR38: Batch triage operations -- filtering, grouping, and batch replay of failed commands
- UX-DR39: Real-time updates via Blazor Server SignalR with FluentToast feedback and FluentBadge status indicators
- UX-DR40: Status color system: Green (Completed/Healthy), Blue (Processing/Received/EventsStored/EventsPublished), Yellow (Rejected), Red (PublishFailed/TimedOut/Unhealthy), Gray (Unknown/Deactivated)

### FR Coverage Map

| FR | Epic | Description |
|----|------|-------------|
| FR1 | 1 + 3 | Command types (Epic 1), REST endpoint (Epic 3) |
| FR2 | 1 | Command validation types and MessageType value object |
| FR3 | 2 | Command routing to aggregate actor |
| FR4 | 3 | Correlation ID on command submission |
| FR5 | 3 | Command status query endpoint |
| FR6 | 3 | Failed command replay |
| FR7 | 3 | Optimistic concurrency rejection |
| FR8 | 3 | Dead-letter routing |
| FR9 | 2 | Append-only immutable event persistence |
| FR10 | 2 | Gapless sequence numbers per aggregate |
| FR11 | 1 | 14-field event metadata envelope |
| FR12 | 2 | State reconstruction via event replay |
| FR13 | 7 | Configurable snapshots |
| FR14 | 2 | Snapshot + tail event reconstruction |
| FR15 | 2 | Composite key strategy with tenant isolation |
| FR16 | 2 | Atomic event writes |
| FR17 | 4 | Pub/sub with CloudEvents 1.0 |
| FR18 | 4 | At-least-once delivery |
| FR19 | 4 | Per-tenant-per-domain topics |
| FR20 | 4 | Resilient persistence during pub/sub outage |
| FR21 | 1 | Pure function domain processor contract |
| FR22 | 8 | Domain service registration via DAPR config |
| FR23 | 2 | Domain service invocation during command processing |
| FR24 | 8 | Multi-domain support (2+ domains) |
| FR25 | 8 | Multi-tenant domain support (2+ tenants) |
| FR26 | 1 | Canonical identity tuple |
| FR27 | 5 | Data path isolation |
| FR28 | 5 | Storage key isolation |
| FR29 | 5 | Pub/sub topic isolation |
| FR30 | 5 | JWT authentication |
| FR31 | 5 | JWT claims-based authorization |
| FR32 | 5 | Pre-pipeline unauthorized rejection |
| FR33 | 5 | Actor-level tenant validation |
| FR34 | 5 | DAPR service-to-service access control |
| FR35 | 6 | OpenTelemetry traces |
| FR36 | 6 | Structured logs with correlation/causation IDs |
| FR37 | 6 | Dead-letter-to-origin tracing |
| FR38 | 6 | Health check endpoints |
| FR39 | 6 | Readiness check endpoints |
| FR40 | 8 | Single Aspire command startup |
| FR41 | 8 | Sample domain service reference |
| FR42 | 8 | NuGet packages with zero-config quickstart |
| FR43 | 8 | Environment deployment via DAPR config only |
| FR44 | 8 | Aspire publisher deployment manifests |
| FR45 | 8 | Unit tests without DAPR |
| FR46 | 8 | Integration tests with DAPR containers |
| FR47 | 8 | E2E contract tests |
| FR48 | 1 | EventStoreAggregate base class with conventions |
| FR49 | 2 | Duplicate command detection via ULID tracking |
| FR50 | 9 | 3-tier query routing model |
| FR51 | 9 | ETag actor per projection+tenant |
| FR52 | 9 | NotifyProjectionChanged helper |
| FR53 | 9 | ETag pre-check returning HTTP 304 |
| FR54 | 9 | Query actor in-memory page cache |
| FR55 | 10 | SignalR "changed" broadcast |
| FR56 | 10 | SignalR hub with Redis backplane |
| FR57 | 9 | Query contract library with typed metadata |
| FR58 | 9 | Coarse invalidation per projection+tenant |
| FR59 | 10 | Automatic SignalR group rejoining |
| FR60 | 12 | 3 reference Blazor refresh patterns |
| FR61 | 9 | Self-routing ETag encode/decode |
| FR62 | 9 | IQueryResponse<T> compile-time enforcement |
| FR63 | 9 | Runtime projection type discovery |
| FR64 | 13 | Short projection type name guidance |
| FR65 | 1 | metadataVersion field in envelope |
| FR66 | 1 | Aggregate tombstoning via terminal event |
| FR67 | 4 | Per-aggregate backpressure (HTTP 429) |
| FR68 | 15 | Recently active streams listing |
| FR69 | 15 | Unified command/event/query timeline |
| FR70 | 15 + 20 | Point-in-time state exploration |
| FR71 | 15 + 20 | Aggregate state diff |
| FR72 | 20 | Full causation chain tracing |
| FR73 | 15 | Projection management with controls |
| FR74 | 15 | Event/command/aggregate type catalog |
| FR75 | 15 + 19 | Operational health + DAPR visibility |
| FR76 | 16 | Storage management |
| FR77 | 16 (via Hexalith.Tenants) | Tenant management — lifecycle, users, configuration managed by Hexalith.Tenants peer service; EventStore admin UI/CLI/MCP consume its API |
| FR78 | 16 | Dead-letter queue management |
| FR79 | 14 | Three-interface shared Admin API |
| FR80 | 17 | CLI output formats, exit codes, completions |
| FR81 | 18 | MCP structured tools with approval gates |
| FR82 | 15 | Observability deep links |

## Epic List

### Epic 1: Domain Contract Foundation
A domain service developer can define commands, events, identity types, and aggregate state using EventStore's shared type system — ULID-based IDs, MessageType value objects, 14-field event envelope, EventStoreAggregate base class, and IRejectionEvent marker interface.
**FRs covered:** FR1 (types), FR2 (types), FR11, FR21, FR26, FR48, FR65, FR66
**Also:** D3, D12 (ULID), UX-DR16-DR20

### Epic 2: Event Persistence & Aggregate Processing
Commands routed to aggregate actors trigger state rehydration from events, domain service invocation via the pure function contract, and atomic event persistence with sequence numbers, optimistic concurrency, and idempotency detection.
**FRs covered:** FR3, FR9, FR10, FR12, FR14, FR15, FR16, FR23, FR49
**Also:** D1, D2, D7, Rules 6/11

### Epic 3: Command REST API & Error Experience
An API consumer can POST commands via REST, receive 202 Accepted + correlation ID, query command status, replay failed commands, and receive RFC 7807 error responses. Swagger UI provides interactive documentation with pre-populated examples.
**FRs covered:** FR1 (REST), FR4, FR5, FR6, FR7, FR8
**Also:** D5, UX-DR1-DR15, Rules 7/12/13

### Epic 4: Event Distribution & Pub/Sub
Persisted events are automatically published to per-tenant-per-domain CloudEvents 1.0 topics with at-least-once delivery, resilient backlog draining during pub/sub outages, and per-aggregate backpressure.
**FRs covered:** FR17, FR18, FR19, FR20, FR67
**Also:** D6

### Epic 5: Security & Multi-Tenant Isolation
JWT-based authentication, claims-based authorization (tenant/domain/command type), three-layer data isolation (data path, storage key, pub/sub topic), DAPR access control policies, and E2E security testing with Keycloak in Aspire.
**FRs covered:** FR27, FR28, FR29, FR30, FR31, FR32, FR33, FR34
**Also:** D4, D11, SEC-1-SEC-5, Rule 16

### Epic 6: Observability & Operations
OpenTelemetry traces span the full command lifecycle, structured logs carry correlation/causation IDs at every pipeline stage, and health/readiness endpoints report DAPR sidecar, state store, and pub/sub status.
**FRs covered:** FR35, FR36, FR37, FR38, FR39
**Also:** Rule 5/9, UX-DR24, UX-DR28

### Epic 7: Snapshots, Rate Limiting & Performance
Configurable snapshots accelerate state rehydration, per-tenant and per-consumer rate limiting prevents abuse, and aggregate-level backpressure protects against saga storms.
**FRs covered:** FR13
**Also:** D8, NFR33, NFR34, Rule 15

### Epic 8: Aspire Orchestration, Sample App & Testing
A developer starts the complete DAPR topology with a single Aspire command, references a working Counter domain service, and runs three-tier tests (unit, integration with DAPR, E2E contract). DevOps generates deployment manifests via Aspire publishers.
**FRs covered:** FR22, FR24, FR25, FR40, FR41, FR42, FR43, FR44, FR45, FR46, FR47
**Also:** D9, D10, UX-DR21-DR25

### Epic 9: Query Pipeline & ETag Caching
Queries are routed through a 3-tier model (entity, checksum, tenant), with self-routing ETag pre-checks returning HTTP 304, in-memory page cache in query actors, coarse invalidation on projection changes, and compile-time enforced query response contracts.
**FRs covered:** FR50, FR51, FR52, FR53, FR54, FR57, FR58, FR61, FR62, FR63
**Also:** NFR35-NFR39

### Epic 10: SignalR Real-Time Notifications
Connected clients receive push "changed" signals when projections update, with Redis backplane for multi-instance distribution and automatic group rejoining on connection recovery.
**FRs covered:** FR55, FR56, FR59
**Also:** UX-DR39

### Epic 11: Server-Managed Projection Builder
EventStore delivers persisted events to domain services' /project endpoints via DAPR service invocation, caches the returned projection state in ProjectionActor, and supports immediate (fire-and-forget) or polled delivery modes — making queries return real data without domain services managing pub/sub subscriptions.
**FRs covered:** (new — from superpowers spec, SCP-Projection Stories 8.9-8.11)
**Also:** ProjectionEventDto, ProjectionRequest/Response, AggregateActor.GetEventsAsync, convention-based discovery

### Epic 12: Blazor Sample UI & Refresh Patterns
The sample Blazor UI demonstrates 3 reference patterns for handling projection change notifications: toast notification, silent data reload, and selective component refresh. All pattern pages include interactive command buttons.
**FRs covered:** FR60
**Also:** SCP-Buttons, UX-DR40

### Epic 13: Documentation & Developer Onboarding
Quick start guide (3 pages, clone to first command in 10 minutes), error reference pages at type URIs, progressive documentation structure, Swagger UI as embedded API reference, and repository-wide documentation refresh aligning with the implemented surface area.
**FRs covered:** FR64
**Also:** UX-DR30-DR33, SCP-Docs

### Epic 14: Admin API Foundation & Abstractions
Shared service interfaces, DTOs, and Admin.Server REST API backed by DAPR. The single backend consumed by Web UI (in-process), CLI (HTTP), and MCP (HTTP). Aspire integration with dedicated DAPR sidecar.
**FRs covered:** FR79, FR82
**NFRs covered:** NFR40, NFR44, NFR46
**Also:** ADR-P4, ADR-P5
**Dependencies:** Epics 1-2

### Epic 15: Admin Web UI — Core Developer Experience
Blazor Fluent UI shell with activity feed, stream browser, aggregate state inspector, projection dashboard, event type catalog, health dashboard with observability deep links, command palette, and dark mode.
**FRs covered:** FR68, FR69, FR70, FR71, FR73, FR74, FR75
**NFRs covered:** NFR41, NFR45
**Also:** UX-DR34-DR49
**Dependencies:** Epic 14

### Story 15.9: Commands Page — Cross-Stream Command List with Filters

As a developer investigating command behavior,
I want to see a filterable list of recent commands across all streams,
So that I can quickly find and investigate commands without knowing the specific stream.

**Acceptance Criteria:**

**Given** the Commands page,
**When** loaded,
**Then** a FluentDataGrid displays recent commands with columns: Status, Command Type, Tenant, Domain, Aggregate ID, Correlation ID, Timestamp.

**Given** the Commands page,
**When** filter chips are used,
**Then** commands can be filtered by status (All/Completed/Processing/Rejected/Failed), tenant, and command type.

**Given** the Commands page header,
**When** rendered,
**Then** summary stat cards show: Total Commands, Success Rate, Failed Count, Average Latency.

**Given** a command row,
**When** clicked,
**Then** navigation proceeds to the stream detail page filtered to that command's correlation ID.

**Given** the Commands page,
**When** the dashboard auto-refresh fires,
**Then** the command list updates without losing filter/pagination state (same pattern as Streams page).

**Technical Notes:**

- Reference implementation: Streams.razor page pattern (API client, filter bar, pagination, refresh subscription)
- Backend: Add GetRecentCommandsAsync() to IStreamQueryService + DaprStreamQueryService
- API: Add GET /api/v1/admin/commands endpoint to AdminStreamsController (or new AdminCommandsController)
- UI: Replace Commands.razor stub with full FluentDataGrid implementation
- UX spec reference: D1 (Command-Centric), Journey 2 (Jerome's Command Investigation)

### Story 15.11: Persistent State Store and Command Activity Tracking

As a developer using the local Aspire environment,
I want data to persist across application restarts,
So that counter values, event streams, and the admin Commands page retain their state.

**Acceptance Criteria:**

**Given** the Aspire AppHost starts,
**When** the DAPR topology initializes,
**Then** a Redis container is provisioned and the DAPR state store uses `state.redis` (not `state.in-memory`).

**Given** the counter has been incremented,
**When** the application is restarted,
**Then** the counter retains its last known value.

**Given** commands have been submitted,
**When** the application is restarted,
**Then** the admin Commands page shows the previously submitted commands.

**Given** the `ICommandActivityTracker.TrackAsync` method is called,
**When** DAPR state store write fails,
**Then** the failure is logged but does not block command processing (Rule 12).

**Technical Notes:**

- Fix A: Replace `state.in-memory` with `state.redis` in `HexalithEventStoreExtensions.cs`, provision Redis via Aspire `AddRedis()`
- Fix B: Replace `InMemoryCommandActivityTracker` (ConcurrentDictionary) with `DaprCommandActivityTracker` (DAPR state store, key `admin:command-activity:{tenantId}`)
- Add `GetRecentCommandsAsync` to `ICommandActivityTracker` interface
- Update `AdminCommandsQueryController` to inject `ICommandActivityTracker` (interface, not concrete)
- Follows same pattern as `admin:stream-activity:{tenantId}` in `DaprStreamQueryService`
- Sprint change proposal: `sprint-change-proposal-2026-03-30.md`

### Story 15.12: Events Page — Cross-Stream Event Browser

As a developer investigating event activity across streams,
I want the Events page (`/events`) to show a filterable, paginated list of recent events from all active streams,
So that I can browse event activity across the system without navigating to individual streams.

**Acceptance Criteria:**

**Given** the Events page loads,
**When** streams have produced events,
**Then** the page fetches recently active streams and their timelines, filters to Event entries, and displays them in a FluentDataGrid sorted by timestamp descending.

**Given** the Events page is loaded,
**When** I select a tenant from the filter dropdown,
**Then** only events from that tenant are displayed.

**Given** the Events page is loaded,
**When** I type an event type name in the filter field,
**Then** only events matching that type name (case-insensitive contains) are displayed.

**Given** the Events page shows events,
**When** I click an event row,
**Then** I navigate to `/streams/{tenant}/{domain}/{aggregateId}?detail={sequenceNumber}`.

**Given** the Events page is loaded,
**When** new events are produced,
**Then** the page refreshes automatically via `DashboardRefreshService.OnDataChanged`.

**Given** the Admin API is unavailable,
**When** the Events page loads,
**Then** an error message is displayed (not a silent empty state).

**Technical Notes:**

- Approach A (UI-only): No backend changes. Aggregate events client-side from existing APIs
- Fetch `GetRecentlyActiveStreamsAsync()` then `GetStreamTimelineAsync(count: 100)` per stream (capped at 50 streams)
- Filter to `TimelineEntryType.Event`, merge and sort by timestamp descending
- Follow Commands.razor pattern: StatCards, filters, FluentDataGrid, pagination, loading/error states
- StatCards: Total Events, Unique Streams, Unique Event Types
- Sprint change proposal: `sprint-change-proposal-2026-04-01-events-page.md`

### Epic 16: Admin Web UI — DBA Operations
Storage management, snapshot controls, backup/restore, tenant management, dead-letter queue, consistency checker. Enables Journey 8 (Maria).
**FRs covered:** FR76, FR77, FR78
**Dependencies:** Epic 15

### Epic 17: Admin CLI (`eventstore-admin`)
.NET global tool calling Admin API over HTTP. Scriptable, pipe-friendly, CI/CD-ready with JSON/CSV/table output, exit codes, shell completions, and connection profiles.
**FRs covered:** FR79, FR80
**NFRs covered:** NFR42
**Also:** UX-DR50-DR55
**Dependencies:** Epic 14

### Epic 18: Admin MCP Server
MCP server exposing admin operations as AI-callable tools with structured JSON output, approval-gated writes, tenant context, and investigation session state. Enables Journey 9 (Claude).
**FRs covered:** FR79, FR81
**NFRs covered:** NFR43
**Also:** UX-DR56-DR59
**Dependencies:** Epic 14

### Epic 19: Admin — DAPR Infrastructure Visibility
DAPR-specific monitoring: sidecar health, actor inspector, pub/sub delivery metrics, resiliency policy viewer, component health history.
**FRs covered:** FR75 (DAPR portion)
**Dependencies:** Epics 14, 15

### Epic 20: Admin — Advanced Debugging (Blame/Bisect/Replay)
Breakthrough debugging features: blame view (per-field provenance), bisect tool (binary search for state divergence), step-through event debugger, command sandbox, and correlation ID trace map.
**FRs covered:** FR70, FR71, FR72
**Also:** UX-DR44, UX-DR45
**Dependencies:** Epic 15

## Epic 1: Domain Contract Foundation

A domain service developer can define commands, events, identity types, and aggregate state using EventStore's shared type system — ULID-based IDs, MessageType value objects, 14-field event envelope, EventStoreAggregate base class, and IRejectionEvent marker interface.

### Story 1.1: Core Identity & Event Envelope

As a domain service developer,
I want a canonical identity scheme and event metadata envelope,
So that all events carry consistent, complete metadata from the start.

**Acceptance Criteria:**

**Given** a new Contracts project,
**When** AggregateIdentity is defined,
**Then** it encapsulates the `tenant:domain:aggregate-id` tuple with parse/format methods
**And** all three components are required, non-empty strings.

**Given** the event envelope definition,
**When** EventEnvelope is created,
**Then** it contains all 14 metadata fields (event message ID, aggregate ID, aggregate type, tenant, sequence number, global position, timestamp, correlation ID, causation ID, user identity, event type, domain service version, metadata version, extension bag)
**And** metadataVersion is an integer starting at 1 (FR65).

### Story 1.2: Command Types, DomainResult & Error Contract

As a domain service developer,
I want typed command envelopes and a domain result contract,
So that I can return events (including rejection events) from my pure function without throwing exceptions.

**Acceptance Criteria:**

**Given** a command submission,
**When** CommandEnvelope is created,
**Then** it contains messageId (ULID string), aggregateId (ULID string), commandType (string), tenantId (string), and payload (JSON).

**Given** a domain processor returns a result,
**When** DomainResult is constructed,
**Then** it contains aggregate type (short kebab) and a list of event outputs (event type + payload).

**Given** a domain rejection scenario,
**When** a rejection event is produced,
**Then** it implements the `IRejectionEvent` marker interface
**And** is named in past-tense negative convention (e.g., `InsufficientFundsDetected`).

### Story 1.3: MessageType Value Object & Hexalith.Commons ULID Integration

As a domain service developer,
I want command types and event types validated as kebab-format strings with ULID-based IDs,
So that type routing and identity generation follow consistent, machine-parseable conventions.

**Acceptance Criteria:**

**Given** a MessageType value object,
**When** a string is parsed,
**Then** it validates the `{domain}-{name}-v{ver}` kebab format
**And** extracts domain prefix, name, and version components.

**Given** `Hexalith.Commons.UniqueIds` is referenced in Contracts,
**When** `UniqueIdHelper.GenerateSortableUniqueStringId()` is called,
**Then** it produces a 26-character Crockford Base32 ULID
**And** `UniqueIdHelper.ExtractTimestamp()` retrieves creation UTC timestamp.

**Given** ULID fields in contracts,
**When** messageId, aggregateId, or correlationId are defined,
**Then** they are `string`-typed (no custom value object)
**And** validated via `UniqueIdHelper` methods.

### Story 1.4: Pure Function Contract & EventStoreAggregate Base

As a domain service developer,
I want an `IDomainProcessor` interface and an `EventStoreAggregate` base class,
So that I can implement domain logic as pure functions with convention-based method discovery.

**Acceptance Criteria:**

**Given** `IDomainProcessor<TCommand, TState>`,
**When** a developer implements it,
**Then** the contract enforces `(TCommand, TState?) -> DomainResult` signature.

**Given** `EventStoreAggregate<TState>`,
**When** a developer inherits from it,
**Then** Handle methods are discovered by reflection (method name `Handle`, parameter types matching command types)
**And** Apply methods are discovered by reflection for state projection
**And** no manual method registration is required.

**Given** convention-based DAPR resource naming,
**When** the aggregate type name is resolved,
**Then** it is derived as kebab-case from the class name with automatic type suffix stripping (`CounterAggregate` -> `counter`)
**And** attribute overrides are validated at startup for non-empty, kebab-case compliance (Rule 17).

**Given** the public API surface,
**When** inspecting the Contracts package,
**Then** only domain-service-developer-facing types are public (UX-DR20)
**And** all public types have XML documentation (UX-DR19).

### Story 1.5: CommandStatus Enum & Aggregate Tombstoning

As a domain service developer,
I want a typed command lifecycle enum and the ability to mark aggregates as terminated,
So that command status tracking uses a shared vocabulary and terminated aggregates cleanly reject further commands.

**Acceptance Criteria:**

**Given** the `CommandStatus` enum,
**When** defined,
**Then** it contains exactly 8 states: Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut (UX-DR16).

**Given** an aggregate marked as terminated,
**When** a terminal event is applied (FR66),
**Then** the aggregate state reflects termination
**And** subsequent commands are rejected with a domain rejection event (via `IRejectionEvent`)
**And** the event stream remains immutable and replayable.

## Epic 2: Event Persistence & Aggregate Processing

Commands routed to aggregate actors trigger state rehydration from events, domain service invocation via the pure function contract, and atomic event persistence with sequence numbers, optimistic concurrency, and idempotency detection.

### Story 2.1: Aggregate Actor & Command Routing

As a platform developer,
I want commands routed to the correct aggregate actor based on identity,
So that each aggregate processes its own commands in isolation.

**Acceptance Criteria:**

**Given** a command with identity tuple `tenant:domain:aggregate-id`,
**When** the system routes the command,
**Then** it activates the DAPR actor with ID derived from the canonical identity scheme (FR3)
**And** uses `IActorStateManager` for all state operations (Rule 6).

### Story 2.2: Event Persistence & Sequence Numbers

As a platform developer,
I want events persisted atomically with gapless sequence numbers in an append-only store,
So that event streams are immutable, ordered, and recoverable.

**Acceptance Criteria:**

**Given** a domain service returns events,
**When** the actor persists them,
**Then** each event is stored at key `{tenant}:{domain}:{aggId}:events:{seq}` (D1)
**And** aggregate metadata is updated at `{tenant}:{domain}:{aggId}:metadata`
**And** sequence numbers are strictly ordered and gapless within the stream (FR10)
**And** events are never modified or deleted after persistence (FR9, Rule 11).

**Given** a command produces N events,
**When** the actor persists them,
**Then** all N events and the metadata update are committed atomically via `SaveStateAsync` (FR16)
**And** ETag-based optimistic concurrency is enforced on the metadata key (D1).

### Story 2.3: State Rehydration & Domain Service Invocation

As a platform developer,
I want aggregate state reconstructed from events before invoking the domain service,
So that the pure function always receives the current state.

**Acceptance Criteria:**

**Given** an aggregate with persisted events,
**When** a new command arrives,
**Then** the actor rehydrates state by replaying all events from sequence 1 to current (FR12)
**And** invokes the registered domain service via `DaprClient.InvokeMethodAsync` (D7, FR23)
**And** passes the command and current state to the pure function contract.

**Given** a snapshot exists for the aggregate,
**When** state is rehydrated,
**Then** the actor loads the latest snapshot plus subsequent events only (FR14)
**And** produces identical state to full replay.

### Story 2.4: Command Status Tracking

As an API consumer,
I want command processing status tracked through a checkpointed state machine,
So that I can query the lifecycle stage of any submitted command.

**Acceptance Criteria:**

**Given** a command enters the pipeline,
**When** the API layer receives it,
**Then** status is written as `Received` at key `{tenant}:{correlationId}:status` (D2)
**And** subsequent transitions (Processing, EventsStored, EventsPublished, Completed) are checkpointed inside the actor.

**Given** a terminal status is reached,
**When** the status is Completed, Rejected, PublishFailed, or TimedOut,
**Then** the status entry includes stage, timestamp, aggregate ID, and terminal-specific detail (event count, rejection type, failure reason, or timeout duration)
**And** a default 24-hour TTL is set via DAPR `ttlInSeconds` metadata.

**Given** status write fails,
**When** the state store is temporarily unavailable,
**Then** the command pipeline continues without blocking (Rule 12).

### Story 2.5: Duplicate Command Detection

As a platform developer,
I want duplicate commands detected and handled idempotently,
So that at-least-once delivery from callers doesn't produce duplicate events.

**Acceptance Criteria:**

**Given** a command with a messageId (ULID) that has already been processed,
**When** the actor receives it,
**Then** the system returns an idempotent success response (FR49)
**And** no duplicate events are persisted.

**Given** a command targeting an aggregate with a concurrent write in progress,
**When** an optimistic concurrency conflict is detected,
**Then** the command is rejected and the caller is informed (FR7).

## Epic 3: Command REST API & Error Experience

An API consumer can POST commands via REST, receive 202 Accepted + correlation ID, query command status, replay failed commands, and receive RFC 7807 error responses. Swagger UI provides interactive documentation with pre-populated examples.

### Story 3.1: Command Submission Endpoint

As an API consumer,
I want to submit commands via `POST /api/v1/commands`,
So that I can trigger domain processing through a standard REST interface.

**Acceptance Criteria:**

**Given** a valid command payload with messageId, aggregateId, commandType, and payload,
**When** submitted to `POST /api/v1/commands` with a valid JWT,
**Then** the system returns `202 Accepted` with `Location` header pointing to `/api/v1/commands/status/{correlationId}` (UX-DR15)
**And** includes `Retry-After: 1` header
**And** the correlation ID defaults to the client-supplied messageId if not provided (FR4).

### Story 3.2: Command Validation & 400 Error Responses

As an API consumer,
I want clear, field-level validation errors when my command is malformed,
So that I know exactly what to fix without guessing.

**Acceptance Criteria:**

**Given** a command missing required fields or with invalid format,
**When** submitted to the command endpoint,
**Then** the system returns `400 Bad Request` with RFC 7807 `application/problem+json` (UX-DR1)
**And** `type` is `https://hexalith.io/problems/validation-error`
**And** `errors` object uses JSON path keys (e.g., `payload.amount`) with human-readable messages (UX-DR3)
**And** `correlationId` is present in ProblemDetails extensions (UX-DR2)
**And** no event sourcing terminology appears in the response (UX-DR6).

### Story 3.3: Command Status Query Endpoint

As an API consumer,
I want to check the processing status of a submitted command,
So that I can track the command lifecycle asynchronously.

**Acceptance Criteria:**

**Given** a previously submitted command with a correlation ID,
**When** `GET /api/v1/commands/status/{correlationId}` is called,
**Then** the system returns the current lifecycle state with timestamp (FR5, UX-DR14)
**And** the response includes the 8-state lifecycle model (Received through Completed/Rejected/PublishFailed/TimedOut).

**Given** a status query for a non-existent correlation ID,
**When** the endpoint is called,
**Then** the system returns `404 Not Found` with RFC 7807 ProblemDetails.

### Story 3.4: Dead-Letter Routing & Command Replay

As an operator,
I want failed commands routed to a dead-letter topic and the ability to replay them,
So that infrastructure failures are recoverable after root cause is fixed.

**Acceptance Criteria:**

**Given** a command that fails due to infrastructure issues after DAPR retry exhaustion,
**When** the failure is terminal,
**Then** the full command payload, error details, and correlation context are routed to a dead-letter topic (FR8).

**Given** a previously failed command,
**When** an operator calls the replay endpoint,
**Then** the command is resubmitted for processing (FR6)
**And** the original correlation ID is preserved for traceability.

### Story 3.5: Concurrency, Auth & Infrastructure Error Responses

As an API consumer,
I want consistent, actionable error responses for auth failures, concurrency conflicts, and service unavailability,
So that my retry logic and error handling work correctly.

**Acceptance Criteria:**

**Given** a missing or expired JWT,
**When** the request is received,
**Then** the system returns `401 Unauthorized` with `WWW-Authenticate` header per RFC 6750 (UX-DR4)
**And** distinguishes missing vs. expired with different `type` URIs (UX-DR8)
**And** no `correlationId` is included (pre-pipeline, UX-DR2).

**Given** a JWT without required tenant authorization,
**When** the command is submitted,
**Then** the system returns `403 Forbidden` naming the specific rejected tenant (UX-DR9)
**And** does NOT enumerate authorized tenants
**And** includes `correlationId`.

**Given** an optimistic concurrency conflict,
**When** two commands race for the same aggregate,
**Then** the system returns `409 Conflict` with `Retry-After: 1` header (UX-DR5, UX-DR10)
**And** no sequence numbers or internal state are leaked.

**Given** the DAPR sidecar is unavailable,
**When** a command is submitted,
**Then** the system returns `503 Service Unavailable` with `Retry-After: 30` (UX-DR5, UX-DR11)
**And** says "command processing pipeline", never "DAPR sidecar"
**And** no `correlationId` is included.

### Story 3.6: OpenAPI Specification & Swagger UI

As an API consumer,
I want interactive API documentation with pre-populated examples,
So that I can explore and test the API without reading separate documentation.

**Acceptance Criteria:**

**Given** the EventStore is running,
**When** a consumer navigates to `/swagger`,
**Then** Swagger UI loads with OpenAPI 3.1 spec (UX-DR12)
**And** endpoints are grouped logically (Commands, Health).

**Given** the Swagger UI "Try it out" feature,
**When** used on the command submission endpoint,
**Then** a valid Counter domain command is pre-populated as an example (UX-DR13).

**Given** all error `type` URIs (e.g., `https://hexalith.io/problems/validation-error`),
**When** opened in a browser,
**Then** they resolve to human-readable documentation explaining the error, with an example and resolution guidance (UX-DR7).

## Epic 4: Event Distribution & Pub/Sub

Persisted events are automatically published to per-tenant-per-domain CloudEvents 1.0 topics with at-least-once delivery, resilient backlog draining during pub/sub outages, and per-aggregate backpressure.

### Story 4.1: CloudEvents Publication & Topic Routing

As a platform developer,
I want persisted events published to DAPR pub/sub using CloudEvents 1.0 on per-tenant-per-domain topics,
So that subscribers receive events scoped to their authorized tenant and domain.

**Acceptance Criteria:**

**Given** events are persisted by the aggregate actor,
**When** the event publisher runs,
**Then** each event is published as a CloudEvents 1.0 envelope via DAPR pub/sub (FR17)
**And** the topic name follows the pattern `{tenant}.{domain}.events` (D6, FR19).

**Given** a subscriber is listening on a tenant-domain topic,
**When** events are published,
**Then** the subscriber receives events with at-least-once delivery guarantee (FR18).

### Story 4.2: Resilient Publication & Backlog Draining

As a platform developer,
I want event persistence to continue when pub/sub is unavailable, with automatic backlog draining on recovery,
So that pub/sub outages never block the command pipeline.

**Acceptance Criteria:**

**Given** the pub/sub system is temporarily unavailable,
**When** events are persisted by the aggregate actor,
**Then** events are stored successfully regardless of pub/sub status (FR20)
**And** command status transitions to `PublishFailed` only after DAPR retry exhaustion.

**Given** the pub/sub system recovers,
**When** the backlog is detected,
**Then** all events persisted during the outage are delivered to subscribers via DAPR retry policies (FR20)
**And** no events are silently dropped.

### Story 4.3: Per-Aggregate Backpressure

As a platform developer,
I want backpressure applied when aggregate command queues grow too deep,
So that saga storms and head-of-line blocking cascades are prevented.

**Acceptance Criteria:**

**Given** an aggregate with a command queue exceeding the configurable depth threshold (default: 100 pending commands),
**When** a new command targets that aggregate,
**Then** the system returns HTTP 429 with `Retry-After` header (FR67)
**And** backpressure is per-aggregate, not system-wide.

## Epic 5: Security & Multi-Tenant Isolation

JWT-based authentication, claims-based authorization (tenant/domain/command type), three-layer data isolation (data path, storage key, pub/sub topic), DAPR access control policies, and E2E security testing with Keycloak in Aspire.

### Story 5.1: JWT Authentication & Claims Transformation

As an API consumer,
I want to authenticate with the Command API using JWT tokens,
So that my identity and tenant context are established before any processing.

**Acceptance Criteria:**

**Given** a request with a valid JWT bearer token,
**When** the token is validated,
**Then** signature, expiry, and issuer are verified on every request (FR30, NFR10)
**And** claims are transformed to extract tenant, domain, and permission arrays.

**Given** a request without a JWT or with an invalid JWT,
**When** processed by the auth middleware,
**Then** the request is rejected at the API gateway before entering the processing pipeline (FR32).

### Story 5.2: Claims-Based Command Authorization

As a platform developer,
I want command submissions authorized based on JWT claims for tenant, domain, and command type,
So that consumers can only submit commands they are permitted to.

**Acceptance Criteria:**

**Given** an authenticated consumer with specific tenant/domain/permission claims,
**When** they submit a command,
**Then** the system validates the command's tenant matches the consumer's authorized tenants (FR31)
**And** the command type matches the consumer's permitted command types.

**Given** a consumer submitting a command for an unauthorized tenant,
**When** authorization fails,
**Then** the command is rejected before processing (FR32)
**And** actor-level tenant validation provides defense-in-depth (FR33, SEC-2).

### Story 5.3: Three-Layer Multi-Tenant Data Isolation

As a platform developer,
I want tenant isolation enforced at data path, storage key, and pub/sub topic layers,
So that failure at one layer cannot compromise tenant isolation.

**Acceptance Criteria:**

**Given** commands for different tenants,
**When** routed through the pipeline,
**Then** data path isolation ensures commands for one tenant never route to another (FR27)
**And** storage key isolation ensures event streams are inaccessible cross-tenant (FR28)
**And** pub/sub topic isolation ensures subscribers only receive authorized events (FR29)
**And** isolation is enforced at all three layers simultaneously (NFR13).

**Given** EventStore enriches event metadata,
**When** events are persisted,
**Then** EventStore owns all 14 envelope metadata fields (SEC-1)
**And** extension metadata is sanitized at the API gateway (SEC-4)
**And** event payload data never appears in logs (SEC-5).

### Story 5.4: DAPR Service-to-Service Access Control

As a platform developer,
I want service-to-service communication between EventStore components authenticated via DAPR policies,
So that the internal call graph is enforced and unauthorized component interactions are blocked.

**Acceptance Criteria:**

**Given** DAPR access control policies,
**When** configured,
**Then** EventStore can invoke actor services and domain services (D4, FR34)
**And** domain services cannot invoke other services directly
**And** the policy is expressed as a per-app-id allow list with `allowedOperations`.

### Story 5.5: E2E Security Testing with Keycloak

As a platform developer,
I want end-to-end security tests using real OIDC tokens from Keycloak,
So that the full six-layer auth pipeline is verified at runtime with real IdP-issued tokens.

**Acceptance Criteria:**

**Given** `Aspire.Hosting.Keycloak` added to AppHost,
**When** the Aspire topology starts,
**Then** Keycloak runs on port 8180 with the `hexalith` realm loaded from `hexalith-realm.json` (D11)
**And** EventStore's `Authority` is configured to Keycloak's realm URL via environment variable overrides.

**Given** 5 pre-configured test users (admin-user, tenant-a-user, tenant-b-user, readonly-user, no-tenant-user),
**When** E2E tests acquire tokens via Resource Owner Password Grant,
**Then** tests validate: multi-tenant admin access, cross-tenant isolation proof, lateral isolation proof, permission enforcement, and tenant validation rejection (D11)
**And** tests use `[Trait("Category", "E2E")]` to separate from fast symmetric-key tests (Rule 16).

## Epic 6: Observability & Operations

OpenTelemetry traces span the full command lifecycle, structured logs carry correlation/causation IDs at every pipeline stage, and health/readiness endpoints report DAPR sidecar, state store, and pub/sub status.

### Story 6.1: OpenTelemetry Tracing Across Command Lifecycle

As an operator,
I want distributed traces spanning the full command lifecycle,
So that I can visualize the complete pipeline from submission to completion in the Aspire dashboard.

**Acceptance Criteria:**

**Given** a command is submitted,
**When** it flows through the pipeline,
**Then** OpenTelemetry traces span: received, processing, events stored, events published, completed (FR35)
**And** traces are visible in the Aspire Traces tab (UX-DR24)
**And** each activity includes the correlationId (Rule 9).

### Story 6.2: Structured Logging with Correlation & Causation IDs

As an operator,
I want structured logs carrying correlation and causation IDs at every pipeline stage,
So that I can filter and trace any command's journey through the system.

**Acceptance Criteria:**

**Given** any pipeline stage emits a log entry,
**When** the log is written,
**Then** it includes `correlationId` and `causationId` fields (FR36, Rule 9)
**And** event payload data never appears in log output — only envelope metadata (Rule 5, SEC-5, NFR12).

**Given** a failed command in the dead-letter topic,
**When** an operator investigates,
**Then** the correlation ID traces back to the originating request (FR37, UX-DR28).

### Story 6.3: Health & Readiness Endpoints

As a DevOps engineer,
I want health and readiness endpoints reporting dependency status,
So that load balancers and orchestrators can route traffic correctly.

**Acceptance Criteria:**

**Given** the EventStore is running,
**When** the health endpoint is called,
**Then** it reports DAPR sidecar, state store, and pub/sub connectivity status (FR38).

**Given** all dependencies are healthy,
**When** the readiness endpoint is called,
**Then** it indicates the system is accepting commands (FR39).

**Given** a dependency is unhealthy,
**When** the readiness endpoint is called,
**Then** the system reports not-ready with the failing dependency identified.

## Epic 7: Snapshots, Rate Limiting & Performance

Configurable snapshots accelerate state rehydration, per-tenant and per-consumer rate limiting prevents abuse, and aggregate-level backpressure protects against saga storms.

### Story 7.1: Configurable Aggregate Snapshots

As a platform developer,
I want aggregate state snapshots created at configurable intervals,
So that state rehydration remains fast regardless of total event count.

**Acceptance Criteria:**

**Given** an aggregate with events exceeding the snapshot threshold (default: every 100 events),
**When** EventStore signals the domain service,
**Then** the domain service produces the snapshot content inline as part of command processing (FR13)
**And** the snapshot is stored in DAPR actor state.

**Given** a configurable snapshot interval,
**When** configured per tenant-domain pair,
**Then** the interval overrides the system default (Rule 15)
**And** snapshot configuration is mandatory — there is always a threshold.

**Given** an aggregate with a snapshot and subsequent events,
**When** state is rehydrated,
**Then** reconstruction from snapshot + tail events produces identical state to full replay (FR14)
**And** rehydration time remains bounded regardless of total event count (NFR19).

### Story 7.2: Per-Tenant Rate Limiting

As a platform developer,
I want per-tenant rate limiting at the Command API,
So that one tenant's saga storm cannot degrade service for other tenants.

**Acceptance Criteria:**

**Given** ASP.NET Core `RateLimiting` middleware configured with `SlidingWindowRateLimiter` (D8),
**When** a tenant exceeds the configurable threshold (default: 1,000 commands/minute/tenant),
**Then** the system returns `429 Too Many Requests` with `Retry-After` header (NFR33)
**And** rate limits are extracted from the JWT `tenant_id` claim.

**Given** rate limits configured per tenant via DAPR config store,
**When** the configuration is changed,
**Then** the new limits take effect dynamically without system restart (NFR20).

### Story 7.3: Per-Consumer Rate Limiting

As a platform developer,
I want per-consumer rate limiting at the Command API,
So that individual consumers cannot overwhelm the system.

**Acceptance Criteria:**

**Given** an authenticated consumer,
**When** they exceed the configurable threshold (default: 100 commands/second/consumer),
**Then** the system returns `429 Too Many Requests` with `Retry-After` header (NFR34)
**And** the consumer identity is derived from JWT claims.

## Epic 8: Aspire Orchestration, Sample App & Testing

A developer starts the complete DAPR topology with a single Aspire command, references a working Counter domain service, and runs three-tier tests (unit, integration with DAPR, E2E contract). DevOps generates deployment manifests via Aspire publishers.

### Story 8.1: Aspire AppHost & DAPR Topology

As a developer,
I want to start the complete EventStore system with a single Aspire command,
So that I have a working local development environment in minutes.

**Acceptance Criteria:**

**Given** the AppHost project is configured,
**When** `dotnet aspire run` is executed,
**Then** EventStore server, sample domain service, DAPR sidecars, state store, and message broker all start (FR40, UX-DR21)
**And** all services are visible in the Aspire dashboard.

**Given** prerequisites are missing (Docker, .NET SDK, DAPR),
**When** startup fails,
**Then** clear, actionable error messages identify exactly what's needed with installation links (UX-DR22).

### Story 8.2: Counter Sample Domain Service

As a developer evaluating EventStore,
I want a working Counter domain service as a reference implementation,
So that I can understand the pure function programming model by example.

**Acceptance Criteria:**

**Given** the sample domain service,
**When** inspected,
**Then** it implements `IncrementCounter` command, `CounterIncremented` event, and `CounterState` (FR41, UX-DR23)
**And** demonstrates the pure function contract `(Command, CurrentState?) -> DomainResult`
**And** demonstrates rejection events via `IRejectionEvent`.

**Given** the domain service is registered,
**When** configured via DAPR,
**Then** it supports at least 2 independent domains within the same EventStore instance (FR24)
**And** supports at least 2 tenants within the same domain with isolated event streams (FR22, FR25).

### Story 8.3: NuGet Client Package & Zero-Config Registration

As a domain service developer,
I want to install EventStore client packages via NuGet with zero-configuration quickstart,
So that I can register my domain service with a single extension method call.

**Acceptance Criteria:**

**Given** the EventStore client NuGet package,
**When** a developer adds it to their project,
**Then** `AddEventStore()` extension method on `IServiceCollection` provides convention-based registration (FR42, UX-DR17)
**And** auto-discovery finds domain processor types without manual listing.

### Story 8.4: Domain Service Hot Reload

As a daily developer,
I want to modify domain logic and restart only the domain service,
So that my inner development loop stays under 5 seconds.

**Acceptance Criteria:**

**Given** the full Aspire topology is running,
**When** a developer modifies a domain processor and restarts only the domain service,
**Then** the updated behavior is testable within ~2 seconds (UX-DR25)
**And** EventStore and the full topology continue running without restart.

### Story 8.5: Three-Tier Test Pyramid

As a developer,
I want unit, integration, and E2E contract tests,
So that domain logic, DAPR integration, and full pipeline are each validated at the appropriate level.

**Acceptance Criteria:**

**Given** Tier 1 unit tests,
**When** executed,
**Then** domain service pure functions are tested without any DAPR runtime dependency (FR45).

**Given** Tier 2 integration tests,
**When** executed with DAPR slim init,
**Then** the actor processing pipeline is tested using DAPR test containers (FR46).

**Given** Tier 3 E2E contract tests,
**When** executed with full DAPR init + Docker,
**Then** the full command lifecycle is validated across the complete Aspire topology (FR47).

### Story 8.6: Deployment Manifests & Environment Portability

As a DevOps engineer,
I want to deploy EventStore to different environments by changing only DAPR component configuration,
So that the same application runs across Redis, PostgreSQL, and cloud backends without code changes.

**Acceptance Criteria:**

**Given** Aspire publishers,
**When** generating deployment manifests,
**Then** Docker Compose, Kubernetes, and Azure Container Apps targets are supported (FR44).

**Given** a different target environment,
**When** DAPR component YAML files are changed,
**Then** the system functions correctly with zero application code changes, zero recompilation, zero redeployment (FR43, NFR29).

### Story 8.7: CI/CD Pipeline (Historical — MinVer replaced by semantic-release in Story 8.8)

As a platform maintainer,
I want automated CI/CD with versioning,
So that builds, tests, and NuGet publishing are consistent and hands-off.

**Acceptance Criteria:**

**Given** a push or PR to main,
**When** GitHub Actions runs,
**Then** restore, build (Release), and Tier 1+2 tests execute (D10).

**Given** a merge to main with Conventional Commits,
**When** semantic-release runs,
**Then** all tests pass, version is determined from commit messages, NuGet packages are packed and published to NuGet.org, and a GitHub Release is created (D9, D10).

**Given** semantic-release versioning,
**When** a `feat:` commit is merged,
**Then** all packages receive a minor version bump with the version injected via `-p:Version=` (D9).

## Epic 9: Query Pipeline & ETag Caching

Queries are routed through a 3-tier model (entity, checksum, tenant), with self-routing ETag pre-checks returning HTTP 304, in-memory page cache in query actors, coarse invalidation on projection changes, and compile-time enforced query response contracts.

### Story 9.1: Query Contracts & Routing Model

As a domain service developer,
I want typed query contracts with mandatory metadata fields and a 3-tier routing model,
So that queries are routed deterministically to the correct query actor.

**Acceptance Criteria:**

**Given** the query contract library,
**When** a query is defined,
**Then** mandatory fields Domain, QueryType, TenantId and optional field EntityId are enforced as typed static members (FR57).

**Given** a query with EntityId,
**When** routed,
**Then** it targets `{QueryType}-{TenantId}-{EntityId}` (FR50 tier 1).

**Given** a query without EntityId but with non-empty payload,
**When** routed,
**Then** it targets `{QueryType}-{TenantId}-{Checksum}` where Checksum is truncated SHA256 base64url (11 chars) of serialized payload (FR50 tier 2).

**Given** a query without EntityId and with empty payload,
**When** routed,
**Then** it targets `{QueryType}-{TenantId}` (FR50 tier 3).

### Story 9.2: Self-Routing ETag Encode/Decode

As a platform developer,
I want self-routing ETags that embed the projection type,
So that the query endpoint can extract routing information without additional lookups.

**Acceptance Criteria:**

**Given** an ETag is generated,
**When** encoded,
**Then** the format is `{base64url(projectionType)}.{guid}` (FR61)
**And** ETags are wrapped in quotes in HTTP response headers per RFC 7232.

**Given** a client sends an `If-None-Match` header,
**When** the ETag is decoded,
**Then** the projection type is extracted for routing.

**Given** a malformed, undecodable, or missing ETag,
**When** processed,
**Then** it is treated as a cache miss — safe degradation by construction (FR61).

### Story 9.3: ETag Actor & Projection Change Notification

As a platform developer,
I want one ETag actor per projection+tenant that tracks the current projection version,
So that ETag pre-checks can return HTTP 304 without activating query actors.

**Acceptance Criteria:**

**Given** an ETag actor keyed by `{ProjectionType}-{TenantId}`,
**When** a projection change is notified via `NotifyProjectionChanged(projectionType, tenantId, entityId?)` (FR52),
**Then** the ETag is regenerated (new GUID portion) (FR51)
**And** all cached query results for that projection+tenant pair are invalidated (FR58, coarse invalidation).

**Given** a query arrives with a valid `If-None-Match` header,
**When** the decoded ETag's GUID matches the current ETag actor value,
**Then** the endpoint returns HTTP 304 without activating the query actor (FR53)
**And** the pre-check completes within 5ms at p99 for warm actors (NFR35).

### Story 9.4: Query Actor In-Memory Page Cache

As a platform developer,
I want query actors to serve as in-memory page caches with no state store persistence,
So that repeated queries return cached data without hitting the microservice.

**Acceptance Criteria:**

**Given** a query actor on first activation (cold call),
**When** it receives a query,
**Then** it forwards the query to the microservice, receives `IQueryResponse<T>` (FR54)
**And** caches both the data and the projection type mapping in memory
**And** returns the result with a self-routing ETag header.

**Given** a warm query actor with cached data,
**When** it receives a subsequent query,
**Then** it checks the ETag actor for the learned projection type
**And** returns cached data on ETag match (within 10ms at p99, NFR36)
**And** re-queries the microservice on ETag mismatch (within 200ms at p99, NFR37).

**Given** DAPR idle timeout deactivates the query actor,
**When** the next query arrives,
**Then** the mapping resets — the cold call re-learns the projection type from the microservice (FR63).

### Story 9.5: IQueryResponse Compile-Time Enforcement

As a platform developer,
I want `IQueryResponse<T>` to enforce at compile time that every response includes a non-empty ProjectionType,
So that silent caching degradation is impossible when a microservice omits projection mapping.

**Acceptance Criteria:**

**Given** a microservice implements `IQueryResponse<T>`,
**When** the response is constructed,
**Then** a non-empty `ProjectionType` field is required at compile time (FR62).

**Given** a query actor receives a response with empty or whitespace-only ProjectionType,
**When** processing the response,
**Then** it is treated as an error equivalent to a missing response (FR62).

## Epic 10: SignalR Real-Time Notifications

Connected clients receive push "changed" signals when projections update, with Redis backplane for multi-instance distribution and automatic group rejoining on connection recovery.

### Story 10.1: SignalR Hub & Projection Change Broadcasting

As a platform developer,
I want a SignalR hub inside EventStore that broadcasts "changed" signals when projections update,
So that connected clients receive real-time push notifications without polling.

**Acceptance Criteria:**

**Given** a SignalR hub hosted inside the EventStore server,
**When** a projection's ETag is regenerated,
**Then** a signal-only "changed" message is broadcast to connected clients (FR55)
**And** clients are grouped by ETag actor ID (`{ProjectionType}-{TenantId}`)
**And** the signal contains no projection data — clients re-query on receipt.

**Given** the SignalR hub endpoint,
**When** the EventStore starts,
**Then** the hub is conditionally mapped at `/hubs/projection-changes` (FR56).

### Story 10.2: Redis Backplane for Multi-Instance SignalR

As a platform developer,
I want a Redis backplane for SignalR message distribution,
So that push notifications work across multiple EventStore server replicas.

**Acceptance Criteria:**

**Given** multiple EventStore server instances,
**When** a projection change is broadcast,
**Then** all connected clients across all instances receive the signal (FR56)
**And** Redis is used as the backplane (a DAPR-managed Redis instance may be reused).

**Given** SignalR signal delivery,
**When** measured,
**Then** delivery from ETag regeneration to client receipt completes within 100ms at p99 (NFR38).

### Story 10.3: Automatic SignalR Group Rejoining on Reconnection

As a domain service developer,
I want the SignalR client helper to automatically rejoin groups on connection recovery,
So that real-time notifications resume after network interruptions without manual intervention.

**Acceptance Criteria:**

**Given** the SignalR client helper NuGet package,
**When** a Blazor Server circuit reconnects, a WebSocket drops, or a network interruption recovers,
**Then** the client automatically rejoins its SignalR groups (FR59)
**And** real-time push notifications resume without developer intervention.

## Epic 11: Server-Managed Projection Builder

EventStore delivers persisted events to domain services' /project endpoints via DAPR service invocation, caches the returned projection state in ProjectionActor, and supports immediate (fire-and-forget) or polled delivery modes — making queries return real data without domain services managing pub/sub subscriptions.

### Story 11.1: Projection Contract DTOs & AggregateActor Event Reading

As a platform developer,
I want wire-format DTOs for the /project endpoint and a read-only method on AggregateActor to fetch events,
So that the projection builder can deliver events without coupling to DAPR internal key formats.

**Acceptance Criteria:**

**Given** the Contracts project,
**When** `ProjectionEventDto` is defined,
**Then** it contains: EventTypeName, Payload, SerializationFormat, SequenceNumber, Timestamp, CorrelationId
**And** excludes Server-internal fields (CausationId, UserId, DomainServiceVersion, Extensions).

**Given** the Contracts project,
**When** `ProjectionRequest` is defined,
**Then** it contains: TenantId, Domain, AggregateId, ProjectionEventDto[] — with per-aggregate granularity.

**Given** the Contracts project,
**When** `ProjectionResponse` is defined,
**Then** it contains: ProjectionType (string), State (JsonElement) — State is opaque, EventStore never interprets it.

**Given** `IAggregateActor`,
**When** `GetEventsAsync(long fromSequence)` is called,
**Then** it returns `EventEnvelope[]` for events after fromSequence
**And** encapsulates DAPR actor state key format internally
**And** returns empty array for new aggregates.

### Story 11.2: EventReplayProjectionActor & Projection State Storage

As a platform developer,
I want a concrete ProjectionActor that stores projection state received from domain services,
So that query actors can serve real projection data.

**Acceptance Criteria:**

**Given** `EventReplayProjectionActor` as a concrete implementation of `CachingProjectionActor`,
**When** registered with DAPR actor type name `"ProjectionActor"`,
**Then** `QueryRouter` can route queries to it via the existing `ProjectionActorTypeName`.

**Given** the actor implements `IProjectionActor` (read) and `IProjectionWriteActor` (write),
**When** `UpdateProjectionAsync(state)` is called,
**Then** state is persisted to DAPR actor state
**And** ETag is regenerated via `IETagActor.RegenerateAsync()`
**And** SignalR notification is broadcast via `IProjectionChangedBroadcaster.BroadcastChangedAsync()`.

**Given** a query arrives,
**When** `ExecuteQueryAsync` is called,
**Then** it reads the last persisted state from DAPR actor state (cache miss path)
**And** `CachingProjectionActor` base provides in-memory ETag caching on top (no double-caching).

### Story 11.3: Immediate Projection Trigger (Fire-and-Forget)

As a platform developer,
I want a fire-and-forget background task that delivers new events to domain services immediately after persistence,
So that projections update in near-real-time without blocking command processing.

**Acceptance Criteria:**

**Given** `RefreshIntervalMs = 0` (default configuration),
**When** events are persisted by the aggregate actor,
**Then** a fire-and-forget background task is triggered via `IProjectionUpdateOrchestrator` (non-blocking — does NOT block command processing).

**Given** the background task executes,
**When** it runs,
**Then** it reads new events via `AggregateActor.GetEventsAsync(fromSequence)` (actor proxy call)
**And** maps `EventEnvelope[]` to `ProjectionEventDto[]`
**And** sends `ProjectionRequest` to the domain service `/project` endpoint via DAPR service invocation
**And** stores the returned state in `EventReplayProjectionActor` via `UpdateProjectionAsync`.

**Given** the projection update fails (domain service unavailable or error),
**When** the failure is logged,
**Then** the projection stays at last known state (eventual consistency)
**And** the next trigger retries.

### Story 11.4: Convention-Based Projection Discovery & Configuration

As a platform developer,
I want domain services that expose a `/project` endpoint to be automatically wired for projections,
So that no explicit projection registration is needed beyond existing domain service setup.

**Acceptance Criteria:**

**Given** a domain service reachable via convention (AppId = domain name) or explicit registration that also exposes a `/project` endpoint,
**When** the system starts,
**Then** it is automatically wired for projection building via convention-based discovery.

**Given** projection configuration,
**When** `EventStore:Projections:DefaultRefreshIntervalMs` is set,
**Then** 0 = immediate (fire-and-forget), >0 = polling interval in ms.

**Given** per-domain override,
**When** `EventStore:Projections:Domains:{domain}:RefreshIntervalMs` is configured,
**Then** it overrides the system default for that domain.

**Given** tenant isolation,
**When** projection updates execute,
**Then** they are scoped to `tenant:domain:aggregateId`
**And** no cross-tenant event leakage occurs in triggering or delivery.

### Story 11.5: Counter Sample /project Endpoint

As a developer evaluating EventStore,
I want the counter sample domain service to expose a /project endpoint,
So that I have a working reference for how domain services build projection state from events.

**Acceptance Criteria:**

**Given** the Sample domain service,
**When** a POST `/project` request arrives with `ProjectionRequest`,
**Then** `CounterProjectionHandler` applies events to in-memory state
**And** returns `ProjectionResponse { "counter", { "count": N } }`.

**Given** `CounterProjectionHandler` receives increment/decrement/reset events,
**When** it applies them,
**Then** Count is updated correctly.

**Given** the Blazor UI increments the counter,
**When** the full pipeline executes (command -> events -> /project -> ProjectionActor -> query),
**Then** the counter value card displays the correct count
**And** no `QueryNotFoundException` errors appear in eventstore logs.

## Epic 12: Blazor Sample UI & Refresh Patterns

The sample Blazor UI demonstrates 3 reference patterns for handling projection change notifications: toast notification, silent data reload, and selective component refresh. All pattern pages include interactive command buttons.

### Story 12.1: Three Blazor Refresh Patterns

As a developer evaluating EventStore,
I want three reference patterns demonstrating how to handle real-time projection updates in Blazor,
So that I can choose the right approach for my application.

**Acceptance Criteria:**

**Given** the sample Blazor UI,
**When** a projection change signal arrives via SignalR,
**Then** Pattern 1 (Notification) shows a toast notification prompting manual refresh (FR60)
**And** Pattern 2 (Silent Reload) automatically reloads data without user interaction (FR60)
**And** Pattern 3 (Selective Refresh) refreshes only the affected projection component (FR60).

**Given** the status color system,
**When** rendering command lifecycle states,
**Then** Green = Completed/Healthy, Blue = Processing/Received/EventsStored/EventsPublished, Yellow = Rejected, Red = PublishFailed/TimedOut/Unhealthy, Gray = Unknown/Deactivated (UX-DR40).

### Story 12.2: Interactive Command Buttons on All Pattern Pages

As a developer evaluating EventStore,
I want all three pattern pages to include increment/decrement/reset buttons,
So that each pattern is a self-contained interactive demo.

**Acceptance Criteria:**

**Given** `NotificationPattern.razor`,
**When** rendered,
**Then** it includes `<CounterCommandForm TenantId="@_tenantId" />` (SCP-Buttons).

**Given** `SilentReloadPattern.razor`,
**When** rendered,
**Then** it includes `<CounterCommandForm TenantId="@_tenantId" />` (SCP-Buttons).

**Given** any of the three pattern pages,
**When** buttons are clicked,
**Then** commands are successfully sent to the EventStore API and the respective pattern behavior is observable.

## Epic 13: Documentation & Developer Onboarding

Quick start guide (3 pages, clone to first command in 10 minutes), error reference pages at type URIs, progressive documentation structure, Swagger UI as embedded API reference, and repository-wide documentation refresh aligning with the implemented surface area.

### Story 13.1: Quick Start Guide

As a developer new to EventStore,
I want a concise quick start guide,
So that I can go from clone to first successful command in under 10 minutes.

**Acceptance Criteria:**

**Given** the quick start guide,
**When** a developer follows it,
**Then** it is 3 pages maximum (UX-DR30)
**And** assumes DDD knowledge (no event sourcing basics explained)
**And** results in a running Aspire topology with a successful Counter command within 10 minutes.

### Story 13.2: Error Reference Pages at Type URIs

As an API consumer,
I want error `type` URIs to resolve to human-readable documentation,
So that I can understand any error and find the resolution without support.

**Acceptance Criteria:**

**Given** each error `type` URI (e.g., `https://hexalith.io/problems/validation-error`),
**When** opened in a browser,
**Then** a documentation page explains the error, shows an example request/response, and suggests resolution steps (UX-DR32).

### Story 13.3: Progressive Documentation Structure

As a developer,
I want documentation organized by expertise level,
So that newcomers aren't overwhelmed and experts aren't condescended to.

**Acceptance Criteria:**

**Given** the documentation set,
**When** organized,
**Then** it follows a progressive structure: quick start (assumes DDD knowledge), concepts (for newcomers), reference (deep dives) (UX-DR33)
**And** levels are never mixed within a single page.

**Given** projection type naming guidance,
**When** documented,
**Then** short names are recommended (e.g., `OrderList` rather than fully qualified type names) for compact ETags (FR64).

### Story 13.4: Repository Documentation Refresh

As a maintainer,
I want all documentation aligned with the actual implemented surface area,
So that docs, planning artifacts, and code tell the same story.

**Acceptance Criteria:**

**Given** the README,
**When** refreshed,
**Then** it reflects the current product surface including query/projection/SignalR entry points (SCP-Docs).

**Given** `docs/reference/nuget-packages.md`,
**When** updated,
**Then** it lists 6 packages (including `Hexalith.EventStore.SignalR`) with correct descriptions (SCP-Docs).

**Given** `docs/community/roadmap.md`,
**When** updated,
**Then** it clearly distinguishes implemented capabilities from planned future work (SCP-Docs).

**Given** planning artifacts (PRD, architecture),
**When** selectively corrected,
**Then** factual statements about shipped behavior match the current repository state (SCP-Docs).
