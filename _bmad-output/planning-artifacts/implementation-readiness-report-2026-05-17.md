---
project: Hexalith.EventStore
date: 2026-05-17
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
includedFiles:
  prd: D:/Hexalith.EventStore/_bmad-output/planning-artifacts/prd.md
  architecture: D:/Hexalith.EventStore/_bmad-output/planning-artifacts/architecture.md
  epics: D:/Hexalith.EventStore/_bmad-output/planning-artifacts/epics.md
  ux: D:/Hexalith.EventStore/_bmad-output/planning-artifacts/ux-design-specification.md
supplementalFiles:
  prdValidationReports:
    - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/prd-validation-report-2026-03-14.md
    - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/prd-validation-report.md
    - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/prd-documentation-validation-report.md
  sprintChangeProposals:
    - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-16-epic-21-post-boot-fixes.md
    - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md
    - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md
    - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md
    - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-05-17
**Project:** Hexalith.EventStore

## Document Discovery

Assessment document set:

- PRD: D:/Hexalith.EventStore/_bmad-output/planning-artifacts/prd.md
- Architecture: D:/Hexalith.EventStore/_bmad-output/planning-artifacts/architecture.md
- Epics and Stories: D:/Hexalith.EventStore/_bmad-output/planning-artifacts/epics.md
- UX Design: D:/Hexalith.EventStore/_bmad-output/planning-artifacts/ux-design-specification.md

Supplemental documents discovered:

- PRD validation reports:
  - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/prd-validation-report-2026-03-14.md
  - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/prd-validation-report.md
  - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/prd-documentation-validation-report.md
- Sprint change proposals:
  - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-16-epic-21-post-boot-fixes.md
  - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md
  - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md
  - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md
  - D:/Hexalith.EventStore/_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md

Discovery notes:

- No sharded PRD, Architecture, Epics, or UX folders were found.
- No critical whole-versus-sharded duplicate conflicts were found.

## PRD Analysis

### Functional Requirements

- FR1: An API consumer can submit a command to the EventStore via a REST endpoint with message ID (ULID), aggregate ID (ULID), command type (kebab `{domain}-{command}-v{ver}`), and payload
- FR2: The system can validate a submitted command for structural completeness (required fields: messageId, aggregateId, commandType, payload; valid ULID format for messageId and aggregateId; valid `{domain}-{command}-v{ver}` kebab format for commandType via MessageType value object; well-formed JSON structure) before routing it for processing
- FR3: The system can route a command to the correct aggregate actor based on the identity scheme (`tenant:domain:aggregate-id`)
- FR4: An API consumer can receive a correlation ID upon command submission for tracking the command lifecycle. The correlation ID defaults to the client-supplied messageId (ULID) if not provided. The client's messageId also serves as the idempotency key
- FR5: An API consumer can query the processing status of a previously submitted command using its correlation ID
- FR6: An operator can replay a previously failed command via the Command API after root cause is fixed
- FR7: The system can reject duplicate commands targeting an aggregate that has an optimistic concurrency conflict, returning an appropriate error
- FR8: The system can route failed commands to a dead-letter topic with full command payload, error details, and correlation context
- FR9: The system can persist events in an append-only, immutable event store where events are never modified or deleted after persistence
- FR10: The system can assign strictly ordered, gapless sequence numbers to events within a single aggregate stream. Cross-aggregate event ordering is explicitly not guaranteed and must not be relied upon by consumers
- FR11: The system can wrap each event in a 14-field metadata envelope (event message ID, aggregate ID, aggregate type, tenant, sequence number, global position, timestamp, correlation ID, causation ID, user identity, event type, domain service version, metadata version, extension bag) stored as separate metadata JSON and payload JSON (two-document storage per D14)
- FR12: The system can reconstruct aggregate state by replaying all events in an aggregate's stream from sequence 1 to current
- FR13: The system can create snapshots of aggregate state at administrator-configured event count intervals (default: every 100 events, configurable per tenant-domain pair) to optimize state rehydration. The EventStore signals the domain service when a snapshot threshold is reached; the domain service produces the snapshot content inline as part of command processing
- FR14: The system can reconstruct aggregate state from the latest snapshot plus subsequent events, producing identical state to full replay
- FR15: The system can store events using a composite key strategy that includes tenant, domain, and aggregate identity for isolation
- FR16: The system can enforce atomic event writes -- a command produces 0 or N events as a single transaction, never a partial subset
- FR17: The system can publish persisted events to subscribers via a pub/sub mechanism using CloudEvents 1.0 envelope format
- FR18: The system can deliver events to subscribers with at-least-once delivery guarantee
- FR19: The system can publish events to per-tenant-per-domain topics, ensuring subscribers only receive events for their authorized scope
- FR20: The system can continue persisting events when the pub/sub system is temporarily unavailable, draining the backlog on recovery
- FR21: A domain service developer can implement a domain processor as a pure function with the contract `(Command, CurrentState?) -> DomainResult`. The domain service returns only aggregate type (short kebab), event types (.NET types, EventStore converts to kebab), and event payloads (pure business facts). EventStore handles all metadata enrichment
- FR22: A domain service developer's domain service is automatically routed by convention (DAPR AppId matches the domain name, method "process") with zero configuration. Routing can be overridden via static registrations (appsettings.json) or DAPR config store (opt-in via `ConfigStoreName`) for complex scenarios such as per-tenant routing to different services
- FR23: The system can invoke a registered domain service when processing a command, passing the command and current aggregate state. The domain service returns a `DomainResult` containing aggregate type and event outputs (event type + payload). EventStore enriches each event with all 14 metadata fields per FR11
- FR24: The system can process commands for at least 2 independent domains within the same EventStore instance
- FR25: The system can process commands for at least 2 tenants within the same domain, each with isolated event streams
- FR26: The system can derive actor IDs, event stream keys, and pub/sub topics from a canonical identity tuple (`tenant:domain:aggregate-id`) where tenant is extracted from JWT claims (D15) and domain is parsed from the message type prefix (`{domain}-{name}-v{ver}` per D13)
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
- FR42: A developer can install EventStore client packages via NuGet to build and register domain services, with a zero-configuration quickstart via convention-based `AddEventStore()` registration and auto-discovery of domain types
- FR43: A DevOps engineer can deploy EventStore to different environments by changing only DAPR component configuration files with zero application code changes
- FR44: A DevOps engineer can generate deployment manifests for Docker Compose, Kubernetes, or Azure Container Apps via Aspire publishers
- FR45: A developer can run unit tests against domain service pure functions without any DAPR runtime dependency
- FR46: A developer can run integration tests against the actor processing pipeline using DAPR test containers
- FR47: A developer can run end-to-end contract tests validating the full command lifecycle across the complete Aspire topology
- FR48: A domain service developer can implement a domain aggregate by inheriting from EventStoreAggregate with typed Apply methods, as a higher-level alternative to implementing IDomainProcessor directly, with convention-based DAPR resource naming derived from the aggregate type name
- FR49: The system can detect and reject duplicate commands by tracking processed command message IDs (client-generated ULIDs) per aggregate, returning an idempotent success response for already-processed commands
- FR50: The system can route incoming query messages to query actors using a 3-tier routing model: (1) queries with EntityId route to `{QueryType}-{TenantId}-{EntityId}`, (2) queries without EntityId but with non-empty payload route to `{QueryType}-{TenantId}-{Checksum}` where Checksum is a truncated SHA256 base64url hash (11 characters) of the serialized payload, (3) queries without EntityId and with empty payload route to `{QueryType}-{TenantId}`. Note: serialization non-determinism (e.g., JSON key ordering differences) produces different checksums for semantically identical queries, resulting in separate cache actors -- this is an accepted trade-off; callers are responsible for consistent serialization
- FR51: The system can maintain one ETag actor per `{ProjectionType}:{TenantId}` that stores a self-routing ETag (format defined in FR61) representing the current projection version, regenerated on every projection change notification
- FR52: A domain service developer can notify EventStore of a projection change by calling `NotifyProjectionChanged(projectionType, tenantId, entityId?)` via NuGet helper, with the underlying transport (DAPR pub/sub by default, or direct service invocation) selected by configuration
- FR53: The query REST endpoint can perform an ETag pre-check (first gate) by decoding the self-routing ETag from the client's `If-None-Match` header to extract the projection type, then calling the corresponding ETag actor -- if the GUID portion matches the current ETag, the endpoint returns HTTP 304 without activating the query actor. If the ETag is missing, malformed, undecodable, or references a non-existent projection type, the endpoint treats it as a cache miss and routes to the query actor
- FR54: A query actor can serve as an in-memory page cache (second gate) with no state store persistence. On first activation (cold call), the query actor forwards the query to the microservice, receives `IQueryResponse<T>` containing data and projection type, caches both the data and the projection type mapping, and returns the result with a self-routing ETag header. On subsequent requests, the query actor uses its learned projection type mapping to check the ETag actor and re-queries only on mismatch. Deactivation (DAPR idle timeout) resets the mapping -- the next request is a cold call to the microservice. FR53 is the hot-path optimization; FR54 operates independently when the query actor is activated (e.g., client has no ETag or ETag is stale)
- FR55: The system can broadcast a signal-only "changed" message to connected SignalR clients when a projection's ETag is regenerated, with clients grouped by ETag actor ID (`{ProjectionType}:{TenantId}`)
- FR56: The system can host a SignalR hub inside the EventStore server, using a Redis backplane for multi-instance SignalR message distribution (a DAPR-managed Redis instance may be reused in supported deployments)
- FR57: A query contract library (NuGet) can define mandatory query metadata fields (Domain, QueryType, TenantId) and optional fields (EntityId) as typed static members, serving as the single source of truth for query routing. ProjectionType is not required on the query consumer side (browser, API caller) -- it is declared by the microservice in its `IQueryResponse<T>` implementation (FR62) and discovered at runtime by the query actor (FR63)
- FR58: The system can invalidate all cached query results for a projection+tenant pair on any projection change notification (coarse invalidation model -- all filters invalidated per projection per tenant). Rationale: coarse invalidation trades unnecessary cache refreshes for design simplicity -- fine-grained filter-aware invalidation would require the EventStore to understand projection schemas, violating the platform's opacity principle
- FR59: The SignalR client helper (NuGet) can automatically rejoin SignalR groups on connection recovery, restoring real-time push notification after Blazor Server circuit reconnection, WebSocket drops, or network interruption -- without requiring manual intervention by the developer
- FR60: The EventStore documentation and sample application can provide at least 3 reference patterns for handling the SignalR "changed" signal in Blazor UI components: (1) toast notification prompting manual refresh, (2) automatic silent data reload, (3) selective component refresh targeting only the affected projection
- FR61: The system can encode self-routing ETags in the format `{base64url(projectionType)}.{guid}` and decode them at the query endpoint to extract projection type routing information. ETags are always wrapped in quotes in HTTP response headers per RFC 7232. Undecodable, malformed, or missing ETags are treated as cache misses, ensuring safe degradation by construction
- FR62: The microservice query response contract (`IQueryResponse<T>`) can enforce at compile time that every query response includes a non-empty `ProjectionType` field, eliminating silent caching degradation when a microservice omits or leaves blank the projection mapping information. The query actor treats an empty or whitespace-only ProjectionType as an error equivalent to a missing response
- FR63: A query actor can discover its projection type mapping at runtime from the microservice's `IQueryResponse<T>` response on its first (cold) call, storing the mapping in memory for subsequent ETag actor lookups. The mapping resets on actor deactivation (DAPR idle timeout), ensuring the next cold call re-learns the mapping from the microservice
- FR64: The EventStore documentation can recommend short projection type names (e.g., `OrderList` rather than fully qualified type names) to keep self-routing ETags compact in HTTP headers, with guidance that projection type names are base64url-encoded in the ETag and longer names produce proportionally longer tokens
- FR65: The event metadata envelope can include a `metadataVersion` field (integer, starting at 1) enabling external consumers to detect envelope schema changes and adapt their deserialization without breaking. Internal consumers use the same version for forward-compatibility checks
- FR66: The system can mark an aggregate as terminated (tombstoned) via a terminal event. A terminated aggregate rejects all subsequent commands with a domain rejection event, while its event stream remains immutable and replayable
- FR67: The system can apply backpressure when aggregate command queues exceed a configurable depth threshold (default: 100 pending commands per aggregate), returning HTTP 429 with Retry-After header to prevent saga storms and head-of-line blocking cascades. Backpressure is per-aggregate, not system-wide
- FR68: The admin tool can list recently active streams (configurable count, default 1000) with stream type, last activity timestamp, and status indicator across all tenants
- FR69: The admin tool can display a unified command/event/query timeline for any aggregate stream, with before/after state snapshots per event
- FR70: The admin tool can show aggregate state at any historical event position or timestamp (point-in-time state exploration)
- FR71: The admin tool can diff aggregate state between any two event positions, highlighting changed fields
- FR72: The admin tool can trace the full causation chain for any event -- originating command, sender identity, correlation ID, and downstream projections affected
- FR73: The admin tool can list all projections with status, lag, throughput, error count, and last processed position -- with controls to pause, resume, reset from position, or replay
- FR74: The admin tool can browse all registered event types, command types, and aggregate types with their schemas, relationships, and version history
- FR75: The admin tool can display an operational health dashboard with event count, throughput, error rate, DAPR component status, and deep links to configured observability tools (Zipkin/Jaeger, Prometheus/Grafana, Aspire Dashboard)
- FR76: The admin tool can manage storage -- show growth trends, hot streams, and trigger compaction, snapshot creation, and backup operations
- FR77: The admin tool can manage tenants -- quotas, onboarding, comparison, and isolation verification. Tenant lifecycle (create, enable/disable, users, roles, configuration) is managed by Hexalith.Tenants peer service; EventStore admin UI/CLI/MCP consume its API
- FR78: The admin tool can manage dead-letter queues -- browse, search, retry, skip, archive failed events with bulk operations
- FR79: All admin read and write operations are accessible through three interfaces: Blazor Web UI, CLI (`eventstore-admin`), and MCP server -- backed by a shared Admin API
- FR80: The admin CLI supports JSON, CSV, and table output formats with pipe-friendly streaming, exit codes (0 healthy, 1 degraded, 2 critical), and shell completion scripts
- FR81: The admin MCP server exposes all read operations as structured tools returning machine-readable JSON, with approval-gated write operations (pause/reset/replay projections, trigger backups)
- FR82: Every trace, metric, and log view in the admin Web UI deep-links to the corresponding detail in the configured external observability tool rather than replicating its UI
- FR83: EventStore.Contracts exposes API-facing `SubmitCommandRequest`, `SubmitCommandResponse`, `SubmitQueryRequest`, `SubmitQueryResponse`, validation request/response DTOs, command status DTOs, replay/read DTOs, and stable ProblemDetails extension names used by HTTP gateway clients
- FR84: EventStore.Client exposes high-level `SubmitCommandAsync`, `SubmitQueryAsync`, validation, command status, replay, and stream-read client methods that handle correlation IDs, ETags, 304 responses, ProblemDetails mapping, and typed cancellation
- FR85: EventStore.Testing exposes deterministic gateway client fakes and builders for command, query, status, replay, ProblemDetails, ETag, tenant/RBAC, stale/degraded, and unavailable paths
- FR86: EventStore documents package ownership rules: API-facing wire contracts live in Contracts, HTTP convenience clients live in Client, deterministic test doubles live in Testing, and runtime server internals remain in Server/EventStore
- FR87: EventStore.Contracts exposes a stable projection adapter contract for generic query serving, including `QueryEnvelope`, `QueryResult`, projection type metadata, and malformed-response taxonomy, or explicitly documents the generic DAPR actor contract domain services must implement
- FR88: EventStore can route `Get*`, `List*`, and `Search*` domain queries through `POST /api/v1/queries` without domain services owning tenant authorization or gateway-specific DTOs
- FR89: EventStore docs define when a domain should use a generic `IProjectionActor.QueryAsync(QueryEnvelope)` adapter versus domain-specific projection actors, including actor type naming, serialization, and test expectations
- FR90: EventStore gateway validates tenant existence, lifecycle state, user membership, and role/permission before invoking a domain service or projection adapter
- FR91: EventStore integrates with Hexalith.Tenants through `ITenantValidator` and `IRbacValidator` adapters with fail-closed behavior when tenant/RBAC data is missing, stale, unavailable, or ambiguous
- FR92: EventStore exposes stable 401/403 ProblemDetails type URIs and reason codes for authentication failure, tenant not found, tenant disabled/suspended, user not a member, insufficient role, insufficient permission, and authorization service unavailable
- FR93: EventStore query contracts define paging bounds, default page size, maximum page size, cursor/offset semantics, blank search behavior, filter validation, and deterministic ordering requirements
- FR94: EventStore query responses define metadata fields for `correlationId`, `etag`, `isNotModified`, `isStale`, `isDegraded`, `projectionVersion`, `servedAt`, paging metadata, and optional warning codes
- FR95: EventStore query error taxonomy defines malformed request, unsupported filter, invalid page, projection missing, projection stale beyond policy, degraded search, malformed projection response, projection timeout, and authorization failures as stable ProblemDetails types
- FR96: EventStore-published events are durable and at-least-once; per-aggregate causal order is preserved when the configured pub/sub backend supports ordering/session keys, and backend limitations are documented
- FR97: EventStore documents and validates pub/sub ordering metadata, partition/session key selection, retry/outbox behavior, dead-letter topic policy, replay/drain behavior, and backend-specific deployment settings
- FR98: EventStore integration tests prove publish-after-persist recovery, duplicate delivery tolerance, per-aggregate causal ordering where supported, and dead-letter handling for supported pub/sub backends
- FR99: EventStore exposes stream read/replay APIs for projection rebuild with tenant/domain/aggregate scoping, sequence checkpoints, continuation tokens, and resumable progress tracking
- FR100: EventStore supports operator-safe projection rebuild flows with pause/resume/cancel, failure reason capture, idempotent checkpoint advancement, and no cross-tenant leakage
- FR101: EventStore documents how domain services rebuild projections from EventStore streams without reading state-store internals
- FR102: EventStore supports event payload and snapshot protection hooks with metadata that identifies protection state without exposing protected data
- FR103: EventStore supports crypto-shredding workflows through key deletion/invalidation semantics, restored-backup safety checks, and explicit behavior for unreadable protected payloads
- FR104: EventStore logs, admin APIs, CLI, MCP, and ProblemDetails never leak protected payload or snapshot data, including during replay, rebuild, backup validation, and failure diagnostics

Total FRs: 104

### Non-Functional Requirements

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
- NFR33: The Command API must enforce per-tenant rate limiting with a configurable threshold (default: 1,000 commands per minute per tenant), returning 429 Too Many Requests with Retry-After header when exceeded
- NFR34: The Command API must enforce per-consumer rate limiting with a configurable threshold (default: 100 commands per second per authenticated consumer), returning 429 Too Many Requests with Retry-After header when exceeded
- NFR35: ETag pre-check at the query endpoint (ETag actor call + comparison) must complete within 5ms at p99 for warm ETag actors, enabling HTTP 304 responses without activating the query actor. Cold ETag actor activation (first call after idle timeout) may exceed this target due to DAPR actor placement
- NFR36: Query actor cache hit (ETag match, return cached data) must complete within 10ms at p99
- NFR37: Query actor cache miss (ETag mismatch, re-query projection via domain service, cache result) must complete within 200ms at p99
- NFR38: SignalR "changed" signal delivery from ETag regeneration to connected client receipt must complete within 100ms at p99
- NFR39: The query pipeline must support at least 1,000 concurrent query requests per second per EventStore instance without exceeding latency targets
- NFR40: Admin API responses must complete within 500ms at p99 for read operations and 2s at p99 for write operations (projection reset, backup trigger)
- NFR41: Admin Web UI must render the operational health dashboard within 2 seconds on initial load, with subsequent SignalR-pushed updates within 200ms
- NFR42: Admin CLI must start and return results for simple queries (health check, stream info) within 3 seconds including .NET runtime startup
- NFR43: Admin MCP server must respond to tool calls within 1 second at p99 for single-resource queries
- NFR44: All admin data access must go through DAPR abstractions exclusively -- the admin tool must be state-store-backend-agnostic, inheriting DAPR's portability guarantee
- NFR45: Admin Web UI must support at least 10 concurrent users with independent views without performance degradation
- NFR46: Admin API must enforce role-based access control -- read-only for developers, operator for DBAs (projection controls, snapshot/compaction), admin for infrastructure operations (tenant management, backup/restore)

Total NFRs: 46

### Additional Requirements

- Event sourcing invariants: append-only immutability, strict per-aggregate sequence ordering, deterministic/idempotent event application, and recognition that event envelope/message type decisions are irreversible after GA.
- Data integrity constraints: optimistic concurrency through ETags, atomic 0-or-N event writes, and snapshot consistency where snapshot plus tail events equals full replay.
- Multi-tenant isolation constraints: data path isolation, storage key isolation, and pub/sub topic isolation must all hold independently.
- Operational patterns: event streams serve as the audit log, temporal queries are preserved through replay, and dead letters plus structured logs form the v1 operational contract.
- Architecture/runtime constraints: .NET 10 LTS, C# 14, DAPR 1.16.1 sidecar model, Aspire 13 orchestration, and `net10.0` target frameworks across projects.
- Required DAPR building blocks for v1: actors, state store, pub/sub, configuration, and resiliency; workflows are explicitly deferred to v2.
- Package boundaries: Contracts, Client, Server, SignalR, Aspire, and Testing packages have separate consumer and ownership responsibilities; all packages version together under SemVer.
- API constraints: Command API uses `/api/v1/commands`, status, replay, health, and readiness endpoints; Query API uses `/api/v1/queries` and `/api/v1/queries/validate`.
- Command payload shape: client sends only `messageId`, `aggregateId`, `commandType`, `payload`, and optional `correlationId`; tenant, user, causation, and domain are server-derived.
- Auth constraints: six defense layers from JWT validation through DAPR policy enforcement; permissions are tenant/domain/command/admin scoped.
- Schema constraints: event metadata uses 14 fields, stored separately from opaque JSON payload; v1 supports JSON serialization only.
- Storage/topic conventions: state store keys and pub/sub topics are tenant/domain/aggregate scoped, including per-tenant/per-domain dead-letter topics.
- Implementation constraints: DAPR sidecar latency, actor rehydration, at-least-once pub/sub, and backend transaction differences must shape design and tests.
- Testing constraints: unit tests avoid DAPR runtime, integration tests use DAPR test containers, and contract tests run across full Aspire topology.
- Scope constraints: v1 must be a full platform pipeline; Blazor dashboard, workflows, saga support, and enterprise features are deferred unless already listed as current release items.

### PRD Completeness Assessment

The PRD is highly complete for requirement extraction: it contains 104 numbered functional requirements, 46 numbered non-functional requirements, explicit domain invariants, API contracts, schema conventions, package ownership, phased scope, risks, and measurable success criteria. The main readiness concern is scope breadth: the PRD combines v1, current release query pipeline, v1.1 gateway/contracts, and v2 administration tooling in one document, so later validation must ensure epics clearly distinguish immediate implementation scope from deferred roadmap requirements.

## Epic Coverage Validation

### Coverage Matrix

Complete PRD requirement text is captured in the PRD Analysis section above. The matrix below records the implementation path claimed by the epics document.

| FR Number | Epic Coverage | Status |
| --------- | ------------- | ------ |
| FR1 | Epic 1 + Epic 3 | Covered |
| FR2 | Epic 1 | Covered |
| FR3 | Epic 2 | Covered |
| FR4 | Epic 3 | Covered |
| FR5 | Epic 3 | Covered |
| FR6 | Epic 3 | Covered |
| FR7 | Epic 3 | Covered |
| FR8 | Epic 3 | Covered |
| FR9 | Epic 2 | Covered |
| FR10 | Epic 2 | Covered |
| FR11 | Epic 1 | Covered |
| FR12 | Epic 2 | Covered |
| FR13 | Epic 7 | Covered |
| FR14 | Epic 2 | Covered |
| FR15 | Epic 2 | Covered |
| FR16 | Epic 2 | Covered |
| FR17 | Epic 4 | Covered |
| FR18 | Epic 4 | Covered |
| FR19 | Epic 4 | Covered |
| FR20 | Epic 4 | Covered |
| FR21 | Epic 1 | Covered |
| FR22 | Epic 8 | Covered |
| FR23 | Epic 2 | Covered |
| FR24 | Epic 8 | Covered |
| FR25 | Epic 8 | Covered |
| FR26 | Epic 1 | Covered |
| FR27 | Epic 5 | Covered |
| FR28 | Epic 5 | Covered |
| FR29 | Epic 5 | Covered |
| FR30 | Epic 5 | Covered |
| FR31 | Epic 5 | Covered |
| FR32 | Epic 5 | Covered |
| FR33 | Epic 5 | Covered |
| FR34 | Epic 5 | Covered |
| FR35 | Epic 6 | Covered |
| FR36 | Epic 6 | Covered |
| FR37 | Epic 6 | Covered |
| FR38 | Epic 6 | Covered |
| FR39 | Epic 6 | Covered |
| FR40 | Epic 8 | Covered |
| FR41 | Epic 8 | Covered |
| FR42 | Epic 8 | Covered |
| FR43 | Epic 8 | Covered |
| FR44 | Epic 8 | Covered |
| FR45 | Epic 8 | Covered |
| FR46 | Epic 8 | Covered |
| FR47 | Epic 8 | Covered |
| FR48 | Epic 1 | Covered |
| FR49 | Epic 2 | Covered |
| FR50 | Epic 9 | Covered |
| FR51 | Epic 9 | Covered |
| FR52 | Epic 9 | Covered |
| FR53 | Epic 9 | Covered |
| FR54 | Epic 9 | Covered |
| FR55 | Epic 10 | Covered |
| FR56 | Epic 10 | Covered |
| FR57 | Epic 9 | Covered |
| FR58 | Epic 9 | Covered |
| FR59 | Epic 10 | Covered |
| FR60 | Epic 12 | Covered |
| FR61 | Epic 9 | Covered |
| FR62 | Epic 9 | Covered |
| FR63 | Epic 9 | Covered |
| FR64 | Epic 13 | Covered |
| FR65 | Epic 1 | Covered |
| FR66 | Epic 1 | Covered |
| FR67 | Epic 4 | Covered |
| FR68 | Epic 15 | Covered |
| FR69 | Epic 15 | Covered |
| FR70 | Epic 15 + Epic 20 | Covered |
| FR71 | Epic 15 + Epic 20 | Covered |
| FR72 | Epic 20 | Covered |
| FR73 | Epic 15 | Covered |
| FR74 | Epic 15 | Covered |
| FR75 | Epic 15 + Epic 19 | Covered |
| FR76 | Epic 16 | Covered |
| FR77 | Epic 16 via Hexalith.Tenants | Covered |
| FR78 | Epic 16 | Covered |
| FR79 | Epic 14 | Covered |
| FR80 | Epic 17 | Covered |
| FR81 | Epic 18 | Covered |
| FR82 | Epic 15 | Covered |
| FR83 | Story 22.1a | Covered |
| FR84 | Story 22.1b | Covered |
| FR85 | Story 22.1c | Covered |
| FR86 | Story 22.1d | Covered |
| FR87 | Story 22.2 | Covered |
| FR88 | Story 22.2 | Covered |
| FR89 | Story 22.2 | Covered |
| FR90 | Story 22.3 | Covered |
| FR91 | Story 22.3 | Covered |
| FR92 | Story 22.3 | Covered |
| FR93 | Story 22.4 | Covered |
| FR94 | Story 22.4 | Covered |
| FR95 | Story 22.4 | Covered |
| FR96 | Story 22.5a | Covered |
| FR97 | Story 22.5b + Story 22.5c | Covered |
| FR98 | Story 22.5d | Covered |
| FR99 | Story 22.6 | Covered |
| FR100 | Story 22.6 | Covered |
| FR101 | Story 22.6 | Covered |
| FR102 | Story 22.7a | Covered |
| FR103 | Story 22.7c | Covered |
| FR104 | Story 22.7d | Covered |

### Missing Requirements

No PRD functional requirements are missing from the epics coverage map.

No FRs are referenced by the epics coverage map that are absent from the PRD.

### Coverage Statistics

- Total PRD FRs: 104
- FRs covered in epics: 104
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

Found: D:/Hexalith.EventStore/_bmad-output/planning-artifacts/ux-design-specification.md

The UX document is complete enough for readiness assessment. It covers four interaction surfaces: Developer SDK, REST API consumer experience, CLI/Aspire operator experience, and v2 Blazor Dashboard. It also includes v1 API error journeys, v1 implementation checklist items, Fluent UI v5 dashboard design guidance, accessibility strategy, and v2 admin tooling requirements UX-DR41 through UX-DR59.

### UX to PRD Alignment

- The UX personas align with the PRD personas and journeys: Marco/developer onboarding, Jerome/daily development, Priya/deployment, Sanjay/API consumer, and Alex/operator.
- v1 REST API UX is reflected in PRD requirements for command submission/status/replay, ProblemDetails, JWT authorization, correlation IDs, OpenAPI/Swagger, and retry/error behavior.
- v1 developer/operator UX is reflected in PRD requirements for pure-function domain processors, NuGet packages, Aspire startup, sample domain service, OpenTelemetry, structured logs, health/readiness, and DAPR-backed portability.
- Query/projection UX is reflected in PRD FR50-FR64 and NFR35-NFR39, covering self-routing ETags, HTTP 304, query actors, SignalR notifications, and sample refresh patterns.
- v2 admin UX is reflected in PRD FR68-FR82 and FR79-FR81, covering Admin Web UI, CLI, MCP, operational dashboard, stream/projection/dead-letter management, tenant delegation, and observability deep links.

### UX to Architecture Alignment

- Architecture ADR-P4 supports the UX requirement for three admin interfaces by using a shared Admin.Server/Admin API with thin CLI and MCP clients.
- Architecture ADR-P5 supports UX observability requirements by using domain-aware summaries plus deep links to configured external observability tools rather than embedded duplicate dashboards.
- Architecture validation explicitly states that UX-DR41-UX-DR59 are supported by ADR-P4 and ADR-P5, with detailed interactions assigned to Epics 15, 17, 18, and 20.
- Architecture and UX both align on Blazor Fluent UI v5 as the admin UI baseline. Older v4 research appears only as an input document, while the current UX spec and architecture both reference Fluent UI 5.x.
- Architecture supports v1 API error UX through ProblemDetails, stable type URIs, correlation ID rules, retry headers, OpenAPI/Swagger, and no payload leakage.
- Architecture supports accessibility and responsiveness indirectly through the Blazor Fluent UI v5 baseline and story-level acceptance criteria; the UX spec defines the more detailed axe-core, keyboard, ARIA, high-contrast, and state-matrix expectations.

### Alignment Issues

No blocking UX alignment issues were found.

### Warnings

- Detailed admin interaction requirements such as command palette, breadcrumbs, virtualized rendering, keyboard shortcuts, CLI profiles/REPL/completions, and MCP investigation session state are intentionally story-level acceptance criteria rather than architecture decisions. They must remain explicit in Epics 15, 17, 18, and 20 during implementation planning.
- Accessibility quality gates from the UX spec are stronger than the architecture summary. Implementation stories for Blazor routes should preserve axe-core, page inventory, keyboard-only navigation, ARIA tree snapshot, and high-contrast verification requirements.

## Epic Quality Review

### Review Summary

The epics document has strong traceability and mostly good story mechanics, but it is not uniformly shaped as user-value slices. The strongest implementation-readiness elements are the explicit walking skeleton gate, the complete FR coverage map, the absence of forward dependencies, and the BDD acceptance criteria in full story sections. The weakest elements are early technical/foundation epics, a non-PRD projection-builder epic, and compact completed-epic summaries that require external artifacts for full acceptance review.

### Critical Violations

#### CRIT-1: Early Epics Are Technical Foundation Slices, Not Independently Valuable User Slices

Examples:

- Epic 1: "Domain Contract Foundation"
- Epic 2: "Event Persistence & Aggregate Processing"
- Epic 7: "Snapshots, Rate Limiting & Performance"

Why this violates the standard:

These epics are mostly architecture/component layers. A user cannot experience the core product from Epic 1 alone, and Epic 2 still requires later API/orchestration work before the user-facing command flow is usable. The document acknowledges this risk with a mandatory "Walking Skeleton Gate," which is good, but the gate sits before the epic list rather than being embedded as the first implementable slice.

Impact:

Implementation can drift into building foundations before proving the end-to-end clone-to-command-flow value. This is the exact failure mode the create-epics-and-stories standards try to prevent.

Recommendation:

Preserve historical IDs, but for any new implementation pass, treat the Walking Skeleton Gate as the first required delivery slice. Add or reference a story that proves: AppHost starts, one sample command posts through `/api/v1/commands`, one event persists, status is observable, and correlation appears in logs/traces. Then deepen contracts, persistence, auth, distribution, and testing behind that working path.

### Major Issues

#### MAJ-1: Epic 11 Is Outside the PRD FR Coverage Map

Example:

- Epic 11: "Server-Managed Projection Builder"
- FRs covered: "(new -- from superpowers spec, SCP-Projection Stories 8.9-8.11)"

Why this is an issue:

Epic 11 may be valuable, but it is not traceable to a numbered PRD FR in the coverage map. It references superpowers specs and sprint change proposals instead. For implementation readiness, this creates a scope-control gap: reviewers cannot tell whether Epic 11 is part of the PRD baseline, a later approved change, or supplemental implementation detail for query/projection requirements.

Impact:

Epic 11 could be implemented or reviewed without the same requirement authority as FR-backed epics. It also complicates final readiness scoring because coverage is 100% without Epic 11, yet Epic 11 remains in the execution plan.

Recommendation:

Either map Epic 11 explicitly to existing PRD requirements such as FR50-FR54 and FR57-FR63, or create/update PRD requirements for server-managed projection building and rerun coverage validation. If it remains supplemental, mark it as change-proposal scope and keep it out of core readiness gating.

#### MAJ-2: Completed Epics 14-21 Depend on External Evidence for Story-Level Review

Examples:

- Epic 14 through Epic 21 are compact summaries with `Detail` links to implementation artifacts.
- The document states every linked implementation artifact is required evidence for acceptance review.

Why this is an issue:

The compact summaries are useful for keeping the epics document readable, but they are insufficient by themselves for full story quality validation. Acceptance criteria, error paths, test evidence, and accessibility details may live in external implementation artifacts that this step did not load individually.

Impact:

The readiness review can confirm that completed story outcomes are listed, but cannot fully verify every completed story's acceptance rigor without loading those artifacts. This creates residual risk around admin UI, CLI, MCP, and Fluent UI migration quality.

Recommendation:

Before final implementation readiness sign-off, sample or fully review the linked artifacts for Epics 14-21, especially admin UI accessibility and operational safety stories. Keep the compact epic format, but treat the artifacts as required audit inputs.

#### MAJ-3: Epic 22 Is Broad and Highly Coupled

Example:

- Epic 22 depends on Epics 3, 4, 5, 8, 9, 11, 13, 16, and 20.
- It spans Contracts, Client, Testing, projection adapters, tenant/RBAC, query policy, publishing guarantees, replay APIs, payload protection, crypto-shredding, and redaction.

Why this is an issue:

Epic 22 is decomposed into child stories well, including container-only stories 22.1 and 22.5, but the parent epic is a large cross-cutting program. It is implementation-ready only if the child-story split is treated as binding and each child keeps a narrow package or behavior boundary.

Impact:

If assigned as one implementation effort, Epic 22 is too large and too coupled. It risks partial completion, unclear review ownership, and package-boundary regressions.

Recommendation:

Do not assign Epic 22 or container stories 22.1/22.5 directly. Assign only the child stories such as 22.1a, 22.1b, 22.5a, and 22.7d-1 through 22.7d-4, with separate acceptance evidence per package or behavior.

### Minor Concerns

#### MIN-1: Epic Outcome Formatting Is Inconsistent

Examples:

- Several epics include explicit `**Outcome:**` lines.
- Others begin directly with a paragraph.

Impact:

This is not a readiness blocker, but it makes scanning and comparing epic value harder.

Recommendation:

Normalize each epic summary to include `Outcome`, `FRs covered`, `Dependencies`, and `Implementation notes` where applicable.

#### MIN-2: Historical Stories Are Mixed With Future Planning

Examples:

- Stories 8.7 and 8.8 are marked historical.
- Epics 14-21 are completed historical summaries.
- Epic 21 is a completed migration but remains in the epic list.

Impact:

This is understandable for a living planning document, but it can confuse implementation handoff because not every listed story is assignable future work.

Recommendation:

Keep status labels visible and consider separating "Completed historical evidence" from "Assignable future work" in the document index or sprint-status source of truth.

### Dependency Analysis

- No forward epic dependencies were found. Declared dependencies point backward to earlier or completed epics.
- No circular dependencies were found in the explicit dependency declarations.
- Container-only stories 22.1 and 22.5 are correctly marked as not directly assignable, with binding child-story splits.
- Within-story references to earlier stories, such as Story 3.2 referencing Story 2.4, are backward references and do not violate dependency direction.

### Story Quality Assessment

- Full story sections include user role, goal, benefit, and acceptance criteria.
- Acceptance criteria are generally BDD-style and testable.
- Several technical stories use "platform developer" as the persona, which is acceptable for an infrastructure product, but these stories should be tied to visible developer/operator outcomes during implementation review.
- Completed-story summaries in Epics 14-21 are outcome-oriented but not sufficient for full acceptance review without the linked artifact files.

### Special Implementation Checks

- Starter template: The architecture specifies a custom solution from individual templates, not a pre-built starter template. A mandatory starter-template story is therefore not required.
- Greenfield/brownfield setup: The document includes setup/orchestration/testing work in Epic 8 and a walking skeleton gate. The gate should be treated as mandatory before any new foundation implementation pass.
- Database/entity timing: The plan uses DAPR state keys and actor state rather than up-front relational schema creation. No "create all tables upfront" violation was found.

### Best Practices Compliance Checklist

| Area | Result | Notes |
| ---- | ------ | ----- |
| Epic delivers user value | Partial | Many admin/user-facing epics pass; early foundation epics are technical slices. |
| Epic can function independently | Partial | Dependencies are backward-only, but early epics do not independently deliver a usable product surface. |
| Stories appropriately sized | Mostly pass | Epic 22 parent is broad, but child splits mitigate it. |
| No forward dependencies | Pass | No future dependency violations found. |
| Database tables created when needed | Pass | DAPR state approach avoids upfront table design. |
| Clear acceptance criteria | Mostly pass | Full story sections use testable BDD criteria; completed summaries need linked artifact review. |
| Traceability to FRs maintained | Mostly pass | 104/104 PRD FRs covered; Epic 11 is supplemental/change-scope and needs explicit authority. |

## Summary and Recommendations

### Overall Readiness Status

NEEDS WORK

The planning set is strong enough to continue targeted implementation work, but it is not cleanly ready for an unqualified new implementation phase. The key reason is not missing requirements coverage: PRD-to-epic FR coverage is complete at 104/104. The issue is implementation shape and review confidence: early foundation epics are technical slices rather than independently valuable user slices, Epic 11 needs clearer requirement authority, completed admin epics require linked evidence review, and Epic 22 must be executed only through its child-story splits.

### Critical Issues Requiring Immediate Action

1. CRIT-1: Early epics are technical foundation slices. Before any new foundation implementation pass, make the Walking Skeleton Gate the first executable delivery slice and prove clone-to-command-flow end to end.
2. MAJ-1: Epic 11 is outside the numbered PRD FR coverage map. Map it to existing FRs, add PRD requirements, or mark it explicitly as approved change-proposal scope.
3. MAJ-2: Epics 14-21 require external implementation artifacts for real acceptance review. Review the linked artifacts before treating those completed epics as readiness evidence.
4. MAJ-3: Epic 22 is too broad to assign directly. Only assign child stories and preserve the package/behavior boundaries in the split map.

### Recommended Next Steps

1. Convert the Walking Skeleton Gate into a named, assignable implementation story or explicit readiness prerequisite for all future foundation work.
2. Decide Epic 11's authority: PRD-backed, change-proposal-backed, or supplemental. Update the FR coverage map accordingly.
3. Audit implementation artifacts for Epics 14-21, prioritizing accessibility, authorization, tenant isolation, protected-data redaction, and operational write safety.
4. Keep Epic 22 parent/container stories unassignable and create implementation work only from child stories such as 22.1a, 22.1b, 22.5a, and 22.7d-*.
5. Normalize epic summaries so each epic has a consistent Outcome, FR coverage, dependency, status, and implementation-evidence block.
6. Separate completed historical evidence from future assignable work in the planning index or sprint-status source of truth.

### Final Note

This assessment identified 8 issues requiring attention: 1 critical epic slicing defect, 3 major planning/readiness issues, 2 minor organization concerns, and 2 UX alignment warnings. Requirements coverage is strong, UX and architecture are aligned, and no forward dependencies were found. Address the critical and major issues before using these artifacts as the basis for a broad new implementation phase.

Assessment date: 2026-05-17

Assessor: Codex using bmad-check-implementation-readiness
