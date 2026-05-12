---
project: Hexalith.EventStore
date: 2026-05-12
stepsCompleted: [document-discovery, prd-analysis, epic-coverage-validation, ux-alignment, epic-quality-review, final-assessment]
includedDocuments:
  prd: D:\Hexalith.EventStore\_bmad-output\planning-artifacts\prd.md
  architecture: D:\Hexalith.EventStore\_bmad-output\planning-artifacts\architecture.md
  epics: D:\Hexalith.EventStore\_bmad-output\planning-artifacts\epics.md
  ux: D:\Hexalith.EventStore\_bmad-output\planning-artifacts\ux-design-specification.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-05-12
**Project:** Hexalith.EventStore

## Document Discovery

Primary documents selected for implementation readiness assessment:

- PRD: `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\prd.md`
- Architecture: `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\architecture.md`
- Epics & Stories: `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\epics.md`
- UX Design: `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\ux-design-specification.md`

No sharded document folders were found for PRD, architecture, epics/stories, or UX. No critical duplicate whole-vs-sharded document conflicts were found.

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
- FR49: The system can detect and reject duplicate commands by tracking processed command message IDs (client-generated ULIDs) per aggregate, returning an idempotent success response for already-processed commands
- FR9: The system can persist events in an append-only, immutable event store where events are never modified or deleted after persistence
- FR10: The system can assign strictly ordered, gapless sequence numbers to events within a single aggregate stream. Cross-aggregate event ordering is explicitly not guaranteed and must not be relied upon by consumers
- FR11: The system can wrap each event in a 14-field metadata envelope (event message ID, aggregate ID, aggregate type, tenant, sequence number, global position, timestamp, correlation ID, causation ID, user identity, event type, domain service version, metadata version, extension bag) stored as separate metadata JSON and payload JSON (two-document storage per D14)
- FR12: The system can reconstruct aggregate state by replaying all events in an aggregate's stream from sequence 1 to current
- FR13: The system can create snapshots of aggregate state at administrator-configured event count intervals (default: every 100 events, configurable per tenant-domain pair) to optimize state rehydration. The EventStore signals the domain service when a snapshot threshold is reached; the domain service produces the snapshot content inline as part of command processing
- FR14: The system can reconstruct aggregate state from the latest snapshot plus subsequent events, producing identical state to full replay
- FR15: The system can store events using a composite key strategy that includes tenant, domain, and aggregate identity for isolation
- FR16: The system can enforce atomic event writes -- a command produces 0 or N events as a single transaction, never a partial subset
- FR65: The event metadata envelope can include a `metadataVersion` field (integer, starting at 1) enabling external consumers to detect envelope schema changes and adapt their deserialization without breaking. Internal consumers use the same version for forward-compatibility checks
- FR66: The system can mark an aggregate as terminated (tombstoned) via a terminal event. A terminated aggregate rejects all subsequent commands with a domain rejection event, while its event stream remains immutable and replayable
- FR17: The system can publish persisted events to subscribers via a pub/sub mechanism using CloudEvents 1.0 envelope format
- FR18: The system can deliver events to subscribers with at-least-once delivery guarantee
- FR19: The system can publish events to per-tenant-per-domain topics, ensuring subscribers only receive events for their authorized scope
- FR20: The system can continue persisting events when the pub/sub system is temporarily unavailable, draining the backlog on recovery
- FR67: The system can apply backpressure when aggregate command queues exceed a configurable depth threshold (default: 100 pending commands per aggregate), returning HTTP 429 with Retry-After header to prevent saga storms and head-of-line blocking cascades. Backpressure is per-aggregate, not system-wide
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
- FR68: The admin tool can list recently active streams (configurable count, default 1000) with stream type, last activity timestamp, and status indicator across all tenants
- FR69: The admin tool can display a unified command/event/query timeline for any aggregate stream, with before/after state snapshots per event
- FR70: The admin tool can show aggregate state at any historical event position or timestamp (point-in-time state exploration)
- FR71: The admin tool can diff aggregate state between any two event positions, highlighting changed fields
- FR72: The admin tool can trace the full causation chain for any event - originating command, sender identity, correlation ID, and downstream projections affected
- FR73: The admin tool can list all projections with status, lag, throughput, error count, and last processed position - with controls to pause, resume, reset from position, or replay
- FR74: The admin tool can browse all registered event types, command types, and aggregate types with their schemas, relationships, and version history
- FR75: The admin tool can display an operational health dashboard with event count, throughput, error rate, DAPR component status, and deep links to configured observability tools (Zipkin/Jaeger, Prometheus/Grafana, Aspire Dashboard)
- FR76: The admin tool can manage storage - show growth trends, hot streams, and trigger compaction, snapshot creation, and backup operations
- FR77: The admin tool can manage tenants - quotas, onboarding, comparison, and isolation verification. Tenant lifecycle (create, enable/disable, users, roles, configuration) is managed by Hexalith.Tenants peer service; EventStore admin UI/CLI/MCP consume its API
- FR78: The admin tool can manage dead-letter queues - browse, search, retry, skip, archive failed events with bulk operations
- FR79: All admin read and write operations are accessible through three interfaces: Blazor Web UI, CLI (`eventstore-admin`), and MCP server - backed by a shared Admin API
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
- NFR44: All admin data access must go through DAPR abstractions exclusively - the admin tool must be state-store-backend-agnostic, inheriting DAPR's portability guarantee
- NFR45: Admin Web UI must support at least 10 concurrent users with independent views without performance degradation
- NFR46: Admin API must enforce role-based access control - read-only for developers, operator for DBAs (projection controls, snapshot/compaction), admin for infrastructure operations (tenant management, backup/restore)

Total NFRs: 46

### Additional Requirements

- Event sourcing invariants: append-only immutability, strict per-aggregate ordering, deterministic/idempotent event replay, and irreversible event envelope/message type design.
- Data integrity constraints: optimistic concurrency via ETags, no partial writes, and snapshot consistency against full replay.
- Multi-tenant isolation requirements: data path isolation, storage key isolation, and pub/sub topic isolation.
- Operational domain patterns: event stream as audit log, temporal queries via replay, and dead letter topic plus structured logging as the v1 operational contract.
- Runtime stack constraints: .NET 10 LTS/C# 14, DAPR 1.16.1 sidecar model, Aspire 13 orchestration, and `net10.0` target frameworks.
- Required DAPR building blocks for v1: actors, state store, pub/sub, configuration, and resiliency; workflows are deferred to v2.
- Package strategy: Contracts, Client, Server, SignalR, Aspire, and Testing packages are versioned together using SemVer 2.0.
- API contract constraints: command API endpoints, query API endpoints, JWT authorization model, ProblemDetails-style response codes, ultra-thin command payload, and server-derived tenant/user/causation/domain fields.
- Data schema constraints: 14-field event envelope, separate payload document, composite DAPR state keys, and per-tenant-per-domain pub/sub topics.
- Testing constraints: unit, integration, and contract tiers with explicit DAPR/Aspire dependency boundaries.
- Deployment constraints: Aspire AppHost defines topology; publishers generate Docker Compose, Kubernetes, or Azure Container Apps manifests.

### PRD Completeness Assessment

The PRD is unusually complete for implementation planning: it contains a clear product scope, user journeys, domain invariants, API and data schema detail, 104 numbered FRs, 46 numbered NFRs, phased roadmap, testing strategy, and explicit risk mitigations. Areas to watch during traceability validation are requirement numbering order, explicit epic coverage for late-added FR83-FR104, and whether v2/v3 roadmap requirements are intentionally included in the current implementation backlog or only documented as future scope.

## Epic Coverage Validation

### Coverage Matrix

| FR Number | Epic Coverage | Status |
| --------- | ------------- | ------ |
| FR1 | Epic 1 + Epic 3 - Command types and REST endpoint | Covered |
| FR2 | Epic 1 - Command validation types and MessageType value object | Covered |
| FR3 | Epic 2 - Command routing to aggregate actor | Covered |
| FR4 | Epic 3 - Correlation ID on command submission | Covered |
| FR5 | Epic 3 - Command status query endpoint | Covered |
| FR6 | Epic 3 - Failed command replay | Covered |
| FR7 | Epic 3 - Optimistic concurrency rejection | Covered |
| FR8 | Epic 3 - Dead-letter routing | Covered |
| FR9 | Epic 2 - Append-only immutable event persistence | Covered |
| FR10 | Epic 2 - Gapless sequence numbers per aggregate | Covered |
| FR11 | Epic 1 - 14-field event metadata envelope | Covered |
| FR12 | Epic 2 - State reconstruction via event replay | Covered |
| FR13 | Epic 7 - Configurable snapshots | Covered |
| FR14 | Epic 2 - Snapshot + tail event reconstruction | Covered |
| FR15 | Epic 2 - Composite key strategy with tenant isolation | Covered |
| FR16 | Epic 2 - Atomic event writes | Covered |
| FR17 | Epic 4 - Pub/sub with CloudEvents 1.0 | Covered |
| FR18 | Epic 4 - At-least-once delivery | Covered |
| FR19 | Epic 4 - Per-tenant-per-domain topics | Covered |
| FR20 | Epic 4 - Resilient persistence during pub/sub outage | Covered |
| FR21 | Epic 1 - Pure function domain processor contract | Covered |
| FR22 | Epic 8 - Domain service registration via DAPR config | Covered |
| FR23 | Epic 2 - Domain service invocation during command processing | Covered |
| FR24 | Epic 8 - Multi-domain support | Covered |
| FR25 | Epic 8 - Multi-tenant domain support | Covered |
| FR26 | Epic 1 - Canonical identity tuple | Covered |
| FR27 | Epic 5 - Data path isolation | Covered |
| FR28 | Epic 5 - Storage key isolation | Covered |
| FR29 | Epic 5 - Pub/sub topic isolation | Covered |
| FR30 | Epic 5 - JWT authentication | Covered |
| FR31 | Epic 5 - JWT claims-based authorization | Covered |
| FR32 | Epic 5 - Pre-pipeline unauthorized rejection | Covered |
| FR33 | Epic 5 - Actor-level tenant validation | Covered |
| FR34 | Epic 5 - DAPR service-to-service access control | Covered |
| FR35 | Epic 6 - OpenTelemetry traces | Covered |
| FR36 | Epic 6 - Structured logs with correlation/causation IDs | Covered |
| FR37 | Epic 6 - Dead-letter-to-origin tracing | Covered |
| FR38 | Epic 6 - Health check endpoints | Covered |
| FR39 | Epic 6 - Readiness check endpoints | Covered |
| FR40 | Epic 8 - Single Aspire command startup | Covered |
| FR41 | Epic 8 - Sample domain service reference | Covered |
| FR42 | Epic 8 - NuGet packages with zero-config quickstart | Covered |
| FR43 | Epic 8 - Environment deployment via DAPR config only | Covered |
| FR44 | Epic 8 - Aspire publisher deployment manifests | Covered |
| FR45 | Epic 8 - Unit tests without DAPR | Covered |
| FR46 | Epic 8 - Integration tests with DAPR containers | Covered |
| FR47 | Epic 8 - E2E contract tests | Covered |
| FR48 | Epic 1 - EventStoreAggregate base class with conventions | Covered |
| FR49 | Epic 2 - Duplicate command detection via ULID tracking | Covered |
| FR50 | Epic 9 - 3-tier query routing model | Covered |
| FR51 | Epic 9 - ETag actor per projection+tenant | Covered |
| FR52 | Epic 9 - NotifyProjectionChanged helper | Covered |
| FR53 | Epic 9 - ETag pre-check returning HTTP 304 | Covered |
| FR54 | Epic 9 - Query actor in-memory page cache | Covered |
| FR55 | Epic 10 - SignalR changed broadcast | Covered |
| FR56 | Epic 10 - SignalR hub with Redis backplane | Covered |
| FR57 | Epic 9 - Query contract library with typed metadata | Covered |
| FR58 | Epic 9 - Coarse invalidation per projection+tenant | Covered |
| FR59 | Epic 10 - Automatic SignalR group rejoining | Covered |
| FR60 | Epic 12 - 3 reference Blazor refresh patterns | Covered |
| FR61 | Epic 9 - Self-routing ETag encode/decode | Covered |
| FR62 | Epic 9 - IQueryResponse<T> compile-time enforcement | Covered |
| FR63 | Epic 9 - Runtime projection type discovery | Covered |
| FR64 | Epic 13 - Short projection type name guidance | Covered |
| FR65 | Epic 1 - metadataVersion field in envelope | Covered |
| FR66 | Epic 1 - Aggregate tombstoning via terminal event | Covered |
| FR67 | Epic 4 - Per-aggregate backpressure | Covered |
| FR68 | Epic 15 - Recently active streams listing | Covered |
| FR69 | Epic 15 - Unified command/event/query timeline | Covered |
| FR70 | Epic 15 + Epic 20 - Point-in-time state exploration | Covered |
| FR71 | Epic 15 + Epic 20 - Aggregate state diff | Covered |
| FR72 | Epic 20 - Full causation chain tracing | Covered |
| FR73 | Epic 15 - Projection management with controls | Covered |
| FR74 | Epic 15 - Event/command/aggregate type catalog | Covered |
| FR75 | Epic 15 + Epic 19 - Operational health and DAPR visibility | Covered |
| FR76 | Epic 16 - Storage management | Covered |
| FR77 | Epic 16 via Hexalith.Tenants - Tenant management integration | Covered |
| FR78 | Epic 16 - Dead-letter queue management | Covered |
| FR79 | Epic 14 + Epic 17 + Epic 18 - Shared Admin API across Web UI, CLI, MCP | Covered |
| FR80 | Epic 17 - CLI output formats, exit codes, completions | Covered |
| FR81 | Epic 18 - MCP structured tools with approval gates | Covered |
| FR82 | Epic 15 - Observability deep links | Covered |
| FR83 | Epic 22 / Story 22.1 - Gateway command/query DTO closure | Covered |
| FR84 | Epic 22 / Story 22.1 - High-level EventStore client methods | Covered |
| FR85 | Epic 22 / Story 22.1 - Deterministic testing fakes/builders | Covered |
| FR86 | Epic 22 / Story 22.1 - Package ownership documentation | Covered |
| FR87 | Epic 22 / Story 22.2 - Projection adapter/generic query contract | Covered |
| FR88 | Epic 22 / Story 22.2 - Get/List/Search query routing | Covered |
| FR89 | Epic 22 / Story 22.2 - Generic vs domain-specific projection guidance | Covered |
| FR90 | Epic 22 / Story 22.3 - Gateway tenant/RBAC validation | Covered |
| FR91 | Epic 22 / Story 22.3 - Hexalith.Tenants validator adapters | Covered |
| FR92 | Epic 22 / Story 22.3 - Stable 401/403 ProblemDetails taxonomy | Covered |
| FR93 | Epic 22 / Story 22.4 - Query paging/filter/order policy | Covered |
| FR94 | Epic 22 / Story 22.4 - Query response metadata contract | Covered |
| FR95 | Epic 22 / Story 22.4 - Query error taxonomy | Covered |
| FR96 | Epic 22 / Story 22.5 - Published event durability and ordering documentation | Covered |
| FR97 | Epic 22 / Story 22.5 - Pub/sub deployment matrix and metadata | Covered |
| FR98 | Epic 22 / Story 22.5 - Backend-specific publish/order/dead-letter tests | Covered |
| FR99 | Epic 22 / Story 22.6 - Stream read/replay APIs | Covered |
| FR100 | Epic 22 / Story 22.6 - Operator-safe projection rebuild flows | Covered |
| FR101 | Epic 22 / Story 22.6 - Projection rebuild documentation | Covered |
| FR102 | Epic 22 / Story 22.7 - Payload and snapshot protection hooks | Covered |
| FR103 | Epic 22 / Story 22.7 - Crypto-shredding workflows | Covered |
| FR104 | Epic 22 / Story 22.7 - Protected-data leak prevention | Covered |

### Missing Requirements

No PRD Functional Requirements are missing from the epics document. The epics document references all 104 PRD FRs and does not introduce extra FR numbers outside the PRD.

Traceability warning: the formal `FR Coverage Map` table in `epics.md` contains explicit rows only for FR1-FR82. FR83-FR104 are covered by Epic 22 and its seven stories, but the coverage map should be expanded with individual FR83-FR104 rows to make downstream audits cleaner.

### Coverage Statistics

- Total PRD FRs: 104
- FRs covered in epics: 104
- FRs missing from epics: 0
- FRs in epics but not in PRD: 0
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

Found: `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\ux-design-specification.md`

The UX specification covers four interaction surfaces: Developer SDK, REST API consumer experience, CLI/Aspire/operator experience, and Blazor dashboard/admin tooling. It also includes explicit v1 API error journeys, a v1 implementation checklist, and v2 administration UX requirements for Web UI, CLI, and MCP.

### UX to PRD Alignment

The UX document aligns strongly with the PRD:

- v1 API UX maps to PRD command API, ProblemDetails, authentication, command status, replay, observability, and developer onboarding requirements.
- SDK and Aspire onboarding UX maps to PRD developer success criteria: single-command startup, first domain service, NuGet packages, sample implementation, and fast feedback.
- Blazor/admin UX maps to PRD FR68-FR82 for stream browsing, timeline, state inspection, projection management, type catalog, health dashboard, storage/tenant/dead-letter management, Web UI/CLI/MCP surfaces, and observability deep links.
- CLI and MCP UX maps to PRD FR79-FR81 and NFR42-NFR43.
- Query/projection refresh UX maps to PRD FR55-FR60 and NFR38.

The PRD does not carry `UX-DR` identifiers directly, but the epics document imports UX-DR1 through UX-DR59 and maps them to epics/stories. That provides the practical traceability bridge.

### UX to Architecture Alignment

Architecture support exists for the major UX surfaces:

- ADR-P4 defines the three-interface admin architecture: Admin.Server hosts REST API and Blazor Web UI; CLI and MCP are thin HTTP clients.
- ADR-P5 supports observability deep links rather than embedded observability dashboards.
- Package architecture includes `Hexalith.EventStore.Admin.Abstractions`, `Hexalith.EventStore.Admin.Server`, `Hexalith.EventStore.Admin.Cli`, and `Hexalith.EventStore.Admin.Mcp`.
- Architecture stack includes Blazor Fluent UI 5.x for v2.
- Architecture API/error decisions support RFC 7807 ProblemDetails, stable extension names, and public gateway contracts.
- Architecture security decisions support admin authentication, role-based access, tenant-scoped access, and MCP/CLI access boundaries.

### Alignment Issues

- Architecture summary sections are stale. The architecture document still opens with "47 FRs across 8 categories" and "32 NFRs across 5 categories" and later claims "Functional Requirements: 47/47 covered" and "Non-Functional Requirements: 32/32 covered." The current PRD has 104 FRs and 46 NFRs, so the architecture validation summary no longer represents the full product scope.
- Admin UX detail is present in UX and epics, and partially supported by ADR-P4/P5, but the architecture validation section does not explicitly validate UX-DR41 through UX-DR59. Command palette, deep linking, breadcrumbs, virtualized rendering, keyboard accessibility, CLI profiles/REPL/completions, and MCP investigation session state should be reflected in architectural validation or accepted as epic-level design detail.
- UX-DR41 through UX-DR59 are referenced in epics as grouped coverage, but many are not individually represented in the top-level FR Coverage Map. This is not a blocker because the epics list covers them, but it weakens auditability.

### Warnings

- Update architecture coverage/validation sections before treating the planning set as fully synchronized. The design decisions are mostly present, but the architecture document's summary math and readiness claims predate the expanded PRD/admin scope.
- If admin UX implementation is next, confirm whether detailed UI interaction requirements such as command palette, deep links, breadcrumbs, keyboard shortcuts, virtualized rendering, and ARIA/accessibility tests are architectural constraints or story-level acceptance criteria only.

## Epic Quality Review

### Overall Quality Finding

The epics document has strong requirement coverage and many well-written stories with testable Given/When/Then acceptance criteria. However, it is not structurally clean enough to be treated as uniformly implementation-ready. The document has accumulated sprint-change additions and now mixes an approved epic list, partial admin story fragments, completed/historical migration work, and fully detailed story sections.

### Critical Violations

1. Epics 14-21 are not expanded into full detailed epic sections.
   - Evidence: detailed `## Epic N` sections exist for Epics 1-13 and Epic 22 only. Epics 14-21 appear in the Epic List, but do not have full detailed story breakdowns.
   - Impact: FR68-FR82 and UX-DR34-DR59 are listed as covered, but much of the admin implementation path is not independently executable from this document.
   - Recommendation: add full detailed sections for Epics 14-20 or move their detailed story files into this artifact with links/indexing. For Epic 21, either add the 14 detailed migration stories or mark it as completed historical work outside the active implementation plan.

2. Epic 21 is a technical migration gate, not a user-value epic.
   - Evidence: "Fluent UI Blazor v4 -> v5 Migration" is framed as package/component migration and includes "MIGRATION GATE: Complete Epic 21 before starting any new UI stories."
   - Impact: it violates the user-value-first epic standard and creates a blocking process gate. If it is already completed, leaving it in the active epic list creates confusion; if incomplete, it blocks all admin UI work without a user-facing outcome.
   - Recommendation: archive it as completed technical debt work, or reframe it as user-visible "Admin UI remains stable and accessible on Fluent UI v5" with concrete smoke-test/user outcomes.

3. Epic 22 depends on admin epics whose detailed implementation plans are missing from the main detailed sections.
   - Evidence: Epic 22 dependencies include Epics 16 and 20. Epics 16 and 20 are listed but lack detailed `## Epic 16` and `## Epic 20` sections.
   - Impact: dependency ordering may be logically valid, but the dependency artifacts are not complete enough for a developer agent to know what has been delivered or must be delivered first.
   - Recommendation: either confirm Epics 16 and 20 are already completed with links to completed story artifacts, or restore their detailed stories to the epics document.

### Major Issues

1. Admin story fragments are embedded in the Epic List section.
   - Evidence: Stories 15.9, 15.11, and 15.12 appear immediately after the Epic 15 list entry, before the detailed `## Epic 1` section begins.
   - Impact: this breaks the expected document structure: Requirements Inventory -> FR Coverage Map -> Epic List -> detailed Epic sections.
   - Recommendation: move these stories under a proper `## Epic 15` detailed section, or split active sprint-change stories into a separate indexed implementation artifact.

2. Story numbering gaps signal incomplete or externally scattered scope.
   - Evidence: Epic 15 includes Story 15.9, Story 15.11, and Story 15.12, but Stories 15.1-15.8 and 15.10 are absent from the detailed body.
   - Impact: implementers cannot determine whether missing stories are completed, intentionally omitted, or lost.
   - Recommendation: add a compact story index for Epic 15 showing status and file/source for every story number.

3. FR83-FR104 coverage is range-based rather than row-level in the FR Coverage Map.
   - Evidence: Epic 22 says "FRs covered: FR83-FR104", and stories 22.1-22.7 map naturally to those FRs, but the formal FR Coverage Map ends at FR82.
   - Impact: implementation coverage is present, but auditability is weaker.
   - Recommendation: expand the FR Coverage Map with rows for FR83-FR104.

4. Several epics are technically titled and should be reframed around user outcomes.
   - Examples: Epic 1 "Domain Contract Foundation", Epic 2 "Event Persistence & Aggregate Processing", Epic 7 "Snapshots, Rate Limiting & Performance", Epic 11 "Server-Managed Projection Builder", Epic 14 "Admin API Foundation & Abstractions".
   - Impact: some are valid platform slices, but their titles make them look like technical milestones rather than user-value increments.
   - Recommendation: keep the technical implementation notes, but rename/reframe the epic goal around what a developer, operator, DBA, or API consumer can do after the epic.

5. Story 22.7 is likely too broad for a single implementation story.
   - Evidence: it covers payload protection, snapshot encryption, key deletion/invalidation, restored-backup safety, unreadable payload behavior, logs, ProblemDetails, admin APIs, UI, CLI, MCP, replay, rebuild, backup validation, and test artifacts.
   - Impact: blast radius is too high for one developer agent and risks partial implementation.
   - Recommendation: split into payload/snapshot protection hooks, unreadable data behavior, crypto-shredding workflow, and redaction/audit-surface verification stories.

### Minor Concerns

- Story 8.7 is labeled historical and references Story 8.8, but Story 8.8 is not present in the detailed story list. This should be resolved as an archive note or restored story.
- Epic 11 is included as "new -- from superpowers spec" without normal FR rows in the top epic list. It has good detailed stories, but the traceability wording should be normalized.
- Architecture says a custom solution from individual templates is used rather than a pre-built starter. Because this is now an existing/brownfield repo, lack of a starter-template setup story is not a blocker.
- Database table timing is not directly applicable; this product uses DAPR state stores and actor state. State keys appear to be introduced near the stories that need them rather than all upfront.

### Compliance Summary

| Area | Status | Notes |
| ---- | ------ | ----- |
| FR traceability | Pass with warning | 104/104 PRD FRs represented; FR83-FR104 need explicit map rows |
| UX traceability | Pass with warning | UX-DR1-DR59 represented in epics; detailed admin story structure is uneven |
| Epic user value | Mixed | Many epics are implementation slices; several need user-value reframing |
| Epic independence | Mixed | Core Epics 1-13 are sequential and usable; admin Epics 14-22 need completed dependency artifacts |
| Story independence | Mostly pass for detailed stories | Most stories are sequentially completable; Story 22.7 is too broad |
| Acceptance criteria | Mostly pass | Detailed stories generally use testable Given/When/Then |
| Document structure | Fail | Missing detailed sections for Epics 14-21 and misplaced Epic 15 fragments |

### Remediation Priority

1. Restore or link detailed story sections for Epics 14-20.
2. Decide whether Epic 21 is active work or completed historical migration, then move/reframe it accordingly.
3. Move Stories 15.9, 15.11, and 15.12 under a proper detailed Epic 15 section and add a story index for missing numbers.
4. Expand the FR Coverage Map for FR83-FR104.
5. Split Story 22.7 before implementation.

## Summary and Recommendations

### Overall Readiness Status

NEEDS WORK

This planning set is close on requirements completeness but not clean enough for broad implementation handoff. The PRD is strong, the UX document exists, and all 104 PRD Functional Requirements are represented in the epics document. The blocker is artifact integrity: architecture validation is stale, admin epics are only partially expanded, and the epics document mixes current implementation planning with historical sprint-change material.

### Critical Issues Requiring Immediate Action

1. Restore detailed implementation plans for Epics 14-20 or link them explicitly from this report/artifact set.
2. Resolve Epic 21's status. If completed, archive it as historical migration work; if active, reframe it as user-value work with complete stories and acceptance criteria.
3. Fix the admin epic dependency chain before starting Epic 22 work that depends on Epics 16 and 20.
4. Move embedded Epic 15 story fragments into the correct detailed section and add a complete story index for missing story numbers.
5. Update `architecture.md` so its requirements overview and validation sections reflect the current PRD: 104 FRs and 46 NFRs, not the older 47/32 baseline.

### Recommended Next Steps

1. Run an artifact cleanup pass on `epics.md`: normalize document structure, add missing detailed sections, move sprint-change fragments, and expand the FR83-FR104 map rows.
2. Run an architecture amendment pass focused only on alignment: update requirement counts, coverage validation, admin UX support, and PRD FR83-FR104 decisions.
3. Split Story 22.7 into smaller implementable stories before assigning it to a developer agent.
4. Add explicit traceability for UX-DR41-UX-DR59 to either architecture constraints or story acceptance criteria.
5. Re-run implementation readiness after those cleanup changes to confirm the status can move from NEEDS WORK to READY.

### Issue Summary

This assessment identified 13 issues across 4 categories:

- Requirements traceability: 1 warning
- Architecture alignment: 3 issues
- Epic/story structure: 6 issues
- Story sizing and implementation readiness: 3 issues

### Final Note

The core product thinking is solid. This is not a requirements failure; it is a planning-artifact hygiene and handoff-readiness failure. Address the critical structural issues before broad implementation. If implementation must proceed immediately, use a narrow slice with complete detailed stories, preferably one of Epics 1-13 or a cleaned-up Epic 22 story that does not depend on undocumented admin work.

Assessor: Codex using `bmad-check-implementation-readiness`
Date: 2026-05-12
