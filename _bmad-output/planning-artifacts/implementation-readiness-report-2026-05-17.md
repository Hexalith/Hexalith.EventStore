---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
completedAt: 2026-05-17
assessor: Codex using bmad-check-implementation-readiness
includedFiles:
  prd: D:\Hexalith.EventStore\_bmad-output\planning-artifacts\prd.md
  architecture: D:\Hexalith.EventStore\_bmad-output\planning-artifacts\architecture.md
  epics: D:\Hexalith.EventStore\_bmad-output\planning-artifacts\epics.md
  ux: D:\Hexalith.EventStore\_bmad-output\planning-artifacts\ux-design-specification.md
excludedPatternMatches:
  - D:\Hexalith.EventStore\_bmad-output\planning-artifacts\prd-documentation-validation-report.md
  - D:\Hexalith.EventStore\_bmad-output\planning-artifacts\prd-validation-report-2026-03-14.md
  - D:\Hexalith.EventStore\_bmad-output\planning-artifacts\prd-validation-report.md
  - D:\Hexalith.EventStore\_bmad-output\planning-artifacts\sprint-change-proposal-2026-04-16-epic-21-post-boot-fixes.md
  - D:\Hexalith.EventStore\_bmad-output\planning-artifacts\sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md
  - D:\Hexalith.EventStore\_bmad-output\planning-artifacts\sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md
  - D:\Hexalith.EventStore\_bmad-output\planning-artifacts\sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md
  - D:\Hexalith.EventStore\_bmad-output\planning-artifacts\sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-05-17
**Project:** Hexalith.EventStore

## Step 1: Document Discovery

### PRD Files Found

**Whole Documents:**
- `prd.md` (122,035 bytes, modified 2026-05-12 10:00:05)

**Sharded Documents:**
- None found

**Excluded Pattern Matches:**
- `prd-documentation-validation-report.md` (7,255 bytes, modified 2026-02-24 14:16:01)
- `prd-validation-report-2026-03-14.md` (36,464 bytes, modified 2026-03-14 11:57:41)
- `prd-validation-report.md` (8,417 bytes, modified 2026-03-13 17:12:49)

### Architecture Files Found

**Whole Documents:**
- `architecture.md` (116,994 bytes, modified 2026-05-12 20:50:34)

**Sharded Documents:**
- None found

### Epics & Stories Files Found

**Whole Documents:**
- `epics.md` (111,943 bytes, modified 2026-05-12 20:40:24)

**Sharded Documents:**
- None found

**Excluded Pattern Matches:**
- `sprint-change-proposal-2026-04-16-epic-21-post-boot-fixes.md` (26,879 bytes, modified 2026-04-16 18:27:05)
- `sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` (34,040 bytes, modified 2026-04-26 11:17:17)
- `sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md` (14,697 bytes, modified 2026-04-26 12:14:47)
- `sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md` (14,239 bytes, modified 2026-04-26 12:46:48)
- `sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md` (14,343 bytes, modified 2026-05-01 13:17:03)

### UX Design Files Found

**Whole Documents:**
- `ux-design-specification.md` (144,917 bytes, modified 2026-04-12 09:36:02)

**Sharded Documents:**
- None found

### Discovery Issues

- No whole-vs-sharded duplicate document formats found.
- Primary assessment inputs confirmed: `prd.md`, `architecture.md`, `epics.md`, and `ux-design-specification.md`.

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

- Event sourcing invariants: append-only immutability, strictly ordered gapless per-aggregate sequence numbers, deterministic event replay, and irreversible event envelope/message-type conventions.
- Data integrity constraints: optimistic concurrency through ETags, atomic 0-or-N event writes, and snapshot consistency where snapshot plus tail events equals full replay.
- Multi-tenant isolation constraints: tenant isolation must hold across data path, storage keys, and pub/sub topics.
- Operational patterns: event streams are the audit log, temporal state is derived through replay, and dead letters plus structured logging form the v1 operational contract.
- Runtime stack: .NET 10 LTS, C# 14, DAPR 1.16.1 sidecars, Aspire 13, and `net10.0` across projects.
- DAPR building block dependencies: actors, state store, pub/sub, configuration, and resiliency are v1 requirements; workflows are deferred to v2.
- Package architecture: Contracts, Client, Server, SignalR, Aspire, and Testing packages have distinct ownership and consumer boundaries.
- Versioning constraints: envelope and domain service contract changes are major versions; all packages are versioned together.
- API constraints: command payload remains ultra-thin with four required fields plus optional correlation ID; tenant, user, causation, and domain fields are server-derived.
- Authorization model: six-layer defense in depth from JWT validation through DAPR policy enforcement.
- Storage schema: two-document event storage, composite DAPR state keys, and per-tenant-per-domain event/dead-letter topics.
- Implementation constraints: DAPR call overhead, actor activation costs, at-least-once subscriber idempotency, and backend transaction differences must be accounted for.
- Testing requirements: unit, integration, and contract tiers are defined with DAPR dependency expectations and target execution speeds.
- MVP gates: command-to-event pipeline, zero data loss, tenant isolation, Redis/PostgreSQL infrastructure swap, clone-to-running under 10 minutes, envelope validation through 3+ domain services, and at least one Hexalith DDD application on the backbone.

### PRD Completeness Assessment

The PRD is highly detailed and implementation-oriented, with complete numbered FR/NFR coverage across command processing, event persistence, distribution, domain integration, identity, authorization, observability, developer experience, query caching, administration tooling, gateway contracts, pub/sub semantics, replay/rebuild, and payload protection. It also contains domain invariants, technical constraints, schema details, API contracts, package boundaries, success metrics, and validation gates.

Potential traceability risks for later steps: several requirements are phase-scoped (`current release`, v1, v1.1, v2), and the readiness assessment must verify that epics/stories preserve those phase boundaries. FR numbering is complete but appears in PRD order rather than strict numeric order, so coverage validation should key by requirement ID rather than document order.

## Epic Coverage Validation

### Coverage Matrix

| FR Number | Epic Coverage | Status |
| --------- | ------------- | ------ |
| FR1 | Epic 1 + Epic 3: Command types and REST endpoint | Covered |
| FR2 | Epic 1: Command validation types and MessageType value object | Covered |
| FR3 | Epic 2: Command routing to aggregate actor | Covered |
| FR4 | Epic 3: Correlation ID on command submission | Covered |
| FR5 | Epic 3: Command status query endpoint | Covered |
| FR6 | Epic 3: Failed command replay | Covered |
| FR7 | Epic 3: Optimistic concurrency rejection | Covered |
| FR8 | Epic 3: Dead-letter routing | Covered |
| FR9 | Epic 2: Append-only immutable event persistence | Covered |
| FR10 | Epic 2: Gapless sequence numbers per aggregate | Covered |
| FR11 | Epic 1: 14-field event metadata envelope | Covered |
| FR12 | Epic 2: State reconstruction via event replay | Covered |
| FR13 | Epic 7: Configurable snapshots | Covered |
| FR14 | Epic 2: Snapshot + tail event reconstruction | Covered |
| FR15 | Epic 2: Composite key strategy with tenant isolation | Covered |
| FR16 | Epic 2: Atomic event writes | Covered |
| FR17 | Epic 4: Pub/sub with CloudEvents 1.0 | Covered |
| FR18 | Epic 4: At-least-once delivery | Covered |
| FR19 | Epic 4: Per-tenant-per-domain topics | Covered |
| FR20 | Epic 4: Resilient persistence during pub/sub outage | Covered |
| FR21 | Epic 1: Pure function domain processor contract | Covered |
| FR22 | Epic 8: Domain service registration via DAPR config | Covered |
| FR23 | Epic 2: Domain service invocation during command processing | Covered |
| FR24 | Epic 8: Multi-domain support | Covered |
| FR25 | Epic 8: Multi-tenant domain support | Covered |
| FR26 | Epic 1: Canonical identity tuple | Covered |
| FR27 | Epic 5: Data path isolation | Covered |
| FR28 | Epic 5: Storage key isolation | Covered |
| FR29 | Epic 5: Pub/sub topic isolation | Covered |
| FR30 | Epic 5: JWT authentication | Covered |
| FR31 | Epic 5: JWT claims-based authorization | Covered |
| FR32 | Epic 5: Pre-pipeline unauthorized rejection | Covered |
| FR33 | Epic 5: Actor-level tenant validation | Covered |
| FR34 | Epic 5: DAPR service-to-service access control | Covered |
| FR35 | Epic 6: OpenTelemetry traces | Covered |
| FR36 | Epic 6: Structured logs with correlation/causation IDs | Covered |
| FR37 | Epic 6: Dead-letter-to-origin tracing | Covered |
| FR38 | Epic 6: Health check endpoints | Covered |
| FR39 | Epic 6: Readiness check endpoints | Covered |
| FR40 | Epic 8: Single Aspire command startup | Covered |
| FR41 | Epic 8: Sample domain service reference | Covered |
| FR42 | Epic 8: NuGet packages with zero-config quickstart | Covered |
| FR43 | Epic 8: Environment deployment via DAPR config only | Covered |
| FR44 | Epic 8: Aspire publisher deployment manifests | Covered |
| FR45 | Epic 8: Unit tests without DAPR | Covered |
| FR46 | Epic 8: Integration tests with DAPR containers | Covered |
| FR47 | Epic 8: E2E contract tests | Covered |
| FR48 | Epic 1: EventStoreAggregate base class with conventions | Covered |
| FR49 | Epic 2: Duplicate command detection via ULID tracking | Covered |
| FR50 | Epic 9: 3-tier query routing model | Covered |
| FR51 | Epic 9: ETag actor per projection+tenant | Covered |
| FR52 | Epic 9: NotifyProjectionChanged helper | Covered |
| FR53 | Epic 9: ETag pre-check returning HTTP 304 | Covered |
| FR54 | Epic 9: Query actor in-memory page cache | Covered |
| FR55 | Epic 10: SignalR changed broadcast | Covered |
| FR56 | Epic 10: SignalR hub with Redis backplane | Covered |
| FR57 | Epic 9: Query contract library with typed metadata | Covered |
| FR58 | Epic 9: Coarse invalidation per projection+tenant | Covered |
| FR59 | Epic 10: Automatic SignalR group rejoining | Covered |
| FR60 | Epic 12: 3 reference Blazor refresh patterns | Covered |
| FR61 | Epic 9: Self-routing ETag encode/decode | Covered |
| FR62 | Epic 9: IQueryResponse compile-time enforcement | Covered |
| FR63 | Epic 9: Runtime projection type discovery | Covered |
| FR64 | Epic 13: Short projection type name guidance | Covered |
| FR65 | Epic 1: metadataVersion field in envelope | Covered |
| FR66 | Epic 1: Aggregate tombstoning via terminal event | Covered |
| FR67 | Epic 4: Per-aggregate backpressure | Covered |
| FR68 | Epic 15: Recently active streams listing | Covered |
| FR69 | Epic 15: Unified command/event/query timeline | Covered |
| FR70 | Epic 15 + Epic 20: Point-in-time state exploration | Covered |
| FR71 | Epic 15 + Epic 20: Aggregate state diff | Covered |
| FR72 | Epic 20: Full causation chain tracing | Covered |
| FR73 | Epic 15: Projection management with controls | Covered |
| FR74 | Epic 15: Event/command/aggregate type catalog | Covered |
| FR75 | Epic 15 + Epic 19: Operational health and DAPR visibility | Covered |
| FR76 | Epic 16: Storage management | Covered |
| FR77 | Epic 16 via Hexalith.Tenants: Tenant management | Covered |
| FR78 | Epic 16: Dead-letter queue management | Covered |
| FR79 | Epic 14: Three-interface shared Admin API | Covered |
| FR80 | Epic 17: CLI output formats, exit codes, completions | Covered |
| FR81 | Epic 18: MCP structured tools with approval gates | Covered |
| FR82 | Epic 15: Observability deep links | Covered |
| FR83 | Story 22.1: API-facing command/query DTOs and ProblemDetails extensions | Covered |
| FR84 | Story 22.1: High-level EventStore client methods | Covered |
| FR85 | Story 22.1: Deterministic gateway fakes and builders | Covered |
| FR86 | Story 22.1: Package ownership documentation | Covered |
| FR87 | Story 22.2: Projection adapter or documented generic query actor contract | Covered |
| FR88 | Story 22.2: Get/List/Search routing through POST /api/v1/queries | Covered |
| FR89 | Story 22.2: Generic versus domain-specific projection actor guidance | Covered |
| FR90 | Story 22.3: Gateway tenant lifecycle, membership, role, and permission validation | Covered |
| FR91 | Story 22.3: Hexalith.Tenants validator adapters with fail-closed behavior | Covered |
| FR92 | Story 22.3: Stable 401/403 ProblemDetails taxonomy | Covered |
| FR93 | Story 22.4: Query paging, filtering, blank search, and deterministic ordering policy | Covered |
| FR94 | Story 22.4: Query response metadata contract | Covered |
| FR95 | Story 22.4: Query error taxonomy | Covered |
| FR96 | Story 22.5: Durable at-least-once published event guarantees and ordering notes | Covered |
| FR97 | Story 22.5: Pub/sub deployment matrix, metadata, retry, drain, and dead-letter policy | Covered |
| FR98 | Story 22.5: Backend-specific publish/order/dead-letter tests | Covered |
| FR99 | Story 22.6: Stream read/replay APIs for projection rebuild | Covered |
| FR100 | Story 22.6: Operator-safe projection rebuild flows | Covered |
| FR101 | Story 22.6: Projection rebuild documentation using public APIs | Covered |
| FR102 | Story 22.7a: Payload and snapshot protection hooks | Covered |
| FR103 | Story 22.7c: Crypto-shredding and restored-backup safety workflows | Covered |
| FR104 | Story 22.7d: Protected-data redaction across operational surfaces | Covered |

### Missing Requirements

No missing PRD functional requirements were found in the epics document.

No extra FR IDs were found in the epics document that are absent from the PRD.

### Coverage Statistics

- Total PRD FRs: 104
- FRs covered in epics: 104
- Missing FRs: 0
- Extra FRs in epics: 0
- Coverage percentage: 100%

### Coverage Notes

- The epics document includes an explicit FR Coverage Map covering FR1-FR104.
- The opening Requirements Inventory section lists FR1-FR64 but does not list FR68-FR104 there; however, the later FR Coverage Map and Epic 22 story section cover FR68-FR104.
- FR77 is covered through Epic 16 with tenant lifecycle delegated to the Hexalith.Tenants peer service, matching the PRD language.

## UX Alignment Assessment

### UX Document Status

Found: `ux-design-specification.md`.

The UX document covers the multi-modal product experience across Developer SDK, REST API consumer, CLI/Aspire operator experience, Blazor Admin Web UI, Admin CLI, and Admin MCP surfaces.

### UX to PRD Alignment

- Developer SDK experience aligns with PRD FR21, FR42, FR45, and the developer success criteria around first domain service and pure-function programming model.
- REST API consumer experience aligns with PRD FR1-FR8, FR30-FR32, FR83-FR95, and the API-auth/status/error handling requirements.
- CLI/Aspire onboarding aligns with PRD FR40, FR43, FR44, NFR31, NFR32, and clone-to-running success criteria.
- Query and real-time UI refresh experience aligns with PRD FR50-FR64 and NFR35-NFR39.
- Blazor Admin Web UI, Admin CLI, and Admin MCP requirements align with PRD FR68-FR82 and NFR40-NFR46.
- UX v1 implementation checklist maps cleanly to PRD concerns for ProblemDetails, OpenAPI/Swagger, command status, retry headers, SDK surface area, Aspire onboarding, shared terminology, and documentation.

### UX to Architecture Alignment

- Architecture supports the REST/API UX through ProblemDetails, command lifecycle status, correlation ID propagation, OpenAPI, six-layer auth, rate limiting, and command status storage.
- Architecture supports SDK and developer UX through Contracts/Client/Testing package boundaries, pure function/domain service contracts, EventStoreAggregate conventions, and Aspire topology.
- Architecture supports CLI/Aspire UX through AppHost orchestration, Aspire publishers, DAPR component portability, and OTLP observability.
- Architecture supports query and SignalR UX through query actors, ETag actors, self-routing ETags, SignalR hub/backplane, and runtime projection type discovery.
- Architecture explicitly states that UX-DR41 through UX-DR59 are supported by ADR-P4 and ADR-P5, with interaction details carried by Epics 15, 17, 18, and 20.

### Alignment Issues

| Issue | Source | Impact | Recommendation |
| ----- | ------ | ------ | -------------- |
| API route version mismatch | UX error journeys and implementation checklist use `/api/commands` and `/api/commands/status/{correlationId}`; PRD uses `/api/v1/commands`, `/api/v1/commands/status/{correlationId}`, and `/api/v1/commands/replay/{correlationId}` | API documentation, Swagger examples, tests, and ProblemDetails `instance` values may diverge | Update UX examples/checklist to the PRD versioned routes or explicitly document compatibility redirects |
| Command status route ordering mismatch | Architecture includes `GET /api/v1/commands/{correlationId}/status` in some sections, while PRD uses `GET /api/v1/commands/status/{correlationId}` | Implementers may create incompatible route shapes or duplicate endpoints | Pick one canonical route in PRD, architecture, UX, OpenAPI, and tests; current PRD route should be treated as canonical unless changed deliberately |
| Admin UX interaction requirements are architecture-supported at high level but story-owned | Architecture says command palette, breadcrumbs, virtualized rendering, keyboard shortcuts, CLI profiles/REPL/completions, and MCP session state remain story-level acceptance criteria | These details could be missed if implementation only follows architecture decisions | Preserve UX-DR41-UX-DR59 as acceptance criteria in Epics 15, 17, 18, and 20 validation |

### Warnings

- UX documentation is present and substantial; no missing-UX warning is needed.
- Route/version mismatches should be resolved before API/client test generation to avoid locking in incompatible examples.
- Blazor/Admin details are phase-scoped to v2 and beyond; readiness review should continue checking phase boundaries so v1 work is not blocked by deferred UI scope.

## Epic Quality Review

### Review Summary

Reviewed `epics.md` against create-epics-and-stories standards for user-value slicing, independence, story sizing, acceptance criteria quality, and dependency hygiene.

Validation facts:

- Epics found: 22
- Explicit inline story sections found: 66
- Inline stories with Given/When/Then acceptance criteria: 66/66
- Linked implementation artifact story files referenced by completed/historical epics: 67
- Linked artifact files found on disk: 67/67
- Linked artifact files containing acceptance criteria: 67/67
- PRD FR traceability: 104/104 covered

### Critical Violations

#### CRIT-1: Multiple Epics Are Technical Milestones Rather Than User-Value Slices

The create-epics-and-stories standard explicitly says epics should be organized around user value, not technical layers. Several epics are named and structured as implementation layers or infrastructure milestones:

- Epic 1: Domain Contract Foundation
- Epic 2: Event Persistence & Aggregate Processing
- Epic 4: Event Distribution & Pub/Sub
- Epic 5: Security & Multi-Tenant Isolation
- Epic 6: Observability & Operations
- Epic 7: Snapshots, Rate Limiting & Performance
- Epic 11: Server-Managed Projection Builder
- Epic 14: Admin API Foundation & Abstractions
- Epic 19: Admin - DAPR Infrastructure Visibility
- Epic 21: Admin UI Fluent UI v5 Stability Migration

Impact: These epics may still be technically necessary, but they make it harder to validate incremental user outcomes. Epic 1 and Epic 2 especially do not obviously deliver a complete user-facing outcome by themselves; the first clearly demonstrable external user experience appears later with Epic 3 and especially Epic 8.

Recommendation: Reframe technical epics around user outcomes, for example "Domain developers can define and register commands/events safely", "API consumers can submit and track commands", "Operators can diagnose and recover failed commands", and "Developers can run and verify the system locally". Keep technical implementation notes under those value slices.

### Major Issues

#### MAJ-1: First-Run / Onboarding Value Arrives Too Late In The Epic Sequence

PRD and UX both make clone-to-running, Aspire startup, sample domain service, and first command trace core adoption gates. In the epic sequence, Aspire orchestration, sample app, NuGet client package, and test pyramid are grouped in Epic 8 after seven technical foundation epics.

Impact: If implemented strictly in epic order, the project delays the most important user proof: "I can run this and see a command flow." This weakens incremental validation and increases the risk that earlier technical work goes unproven in a full topology.

Recommendation: Pull a thin walking skeleton into Epic 1 or Epic 2: minimal AppHost, sample domain service, one command, one persisted event, and one trace. Later epics can deepen persistence, auth, distribution, and operational behavior.

#### MAJ-2: Completed Epics 14-21 Are Not Self-Contained In `epics.md`

Epics 14 through 21 list completed story artifact links rather than inline story sections with user story text and acceptance criteria. All 67 linked artifact files exist and contain acceptance criteria, so the information is not missing from the repository, but the central epics document is not self-contained for those epics.

Impact: A developer or reviewer using only `epics.md` cannot validate story quality, dependencies, or acceptance criteria for Epics 14-21 without following many external files.

Recommendation: Add a compact inline summary for each completed story under Epics 14-21 with persona, outcome, and key acceptance criteria, while preserving the artifact links for detail.

#### MAJ-3: Several Stories Are Compound And Likely Oversized

Examples:

- Story 3.5 combines 401, 403, 409, 503, retry headers, terminology redaction, correlation ID rules, and identifier serialization behavior.
- Story 8.5 combines unit, integration, and E2E contract test architecture.
- Story 22.1 combines Contracts DTOs, Client APIs, Testing fakes/builders, compatibility wrappers, and generated documentation.
- Story 22.5 combines publishing guarantees, backend deployment matrix, retry/outbox/drain behavior, dead-letter policy, and backend-specific tests.
- Story 22.7d spans logs, ProblemDetails, admin UI, CLI, MCP, replay, rebuild, backup validation, and tests.

Impact: These stories are testable, but several are broad enough to become mini-epics. That increases implementation risk and makes review/acceptance less crisp.

Recommendation: Split compound stories by independently releasable surface or behavior. For example, split Story 22.1 into Contracts DTOs, Client methods, Testing fakes, and package documentation.

#### MAJ-4: Route Shape Inconsistencies Leak Into Story Quality

The route mismatch found during UX alignment also affects story quality. Story 3.3 and related API stories should lock one command status route shape, but PRD, architecture, and UX currently contain variants.

Impact: Acceptance criteria may pass while implementing an endpoint shape that disagrees with another planning artifact.

Recommendation: Correct the canonical API route before implementation or test generation, then update affected acceptance criteria and OpenAPI examples.

### Minor Concerns

#### MIN-1: Phase Labels Are Inconsistent Around Query Pipeline Scope

The PRD calls query/projection caching part of the current release, while the epics requirements inventory labels "Query Pipeline & Projection Caching — v2". The epic list itself treats it as Epic 9/10/12/13 work rather than purely deferred v2 work.

Impact: Teams may misclassify query pipeline stories as deferred when the PRD says they are current-release scope.

Recommendation: Normalize phase labels across PRD, epics, and sprint status.

#### MIN-2: Historical/Migration Epics Need Clearer Outcome Framing

Epic 21 is a historical migration and is marked completed, but as an epic title it is implementation-mechanical. Its outcome is stability, accessibility, and testability on Fluent UI v5.

Impact: The title undersells the user value and can look like dependency churn rather than risk reduction.

Recommendation: Rename or annotate historical migration epics with the operational/user outcome they protect.

### Positive Findings

- FR traceability is excellent: the explicit coverage map covers every PRD FR from FR1 through FR104.
- Inline stories consistently use Given/When/Then acceptance criteria, and the criteria are generally specific and independently testable.
- No explicit forward dependency language was found that requires a later story or later epic to make an earlier story work.
- Linked implementation artifacts for completed epics all exist and contain acceptance criteria.
- Database/table-upfront anti-pattern was not observed. The system mostly uses DAPR state-store keys and actor state rather than large upfront relational schema creation.

### Best Practices Compliance Checklist

| Area | Result | Notes |
| ---- | ------ | ----- |
| Epics deliver user value | Fail | Several epics are technical layers or migrations rather than user-value slices |
| Epic independence | Partial | No hard forward references found, but early epics do not independently demonstrate end-user value |
| Stories appropriately sized | Partial | Most are testable; several compound stories should be split |
| No forward dependencies | Pass | No explicit future-story dependency violations found |
| Database/entities created when needed | Pass | No big upfront table/model creation pattern found |
| Clear acceptance criteria | Pass | 66/66 inline stories and 67/67 linked artifacts contain acceptance criteria |
| Traceability to FRs maintained | Pass | 104/104 PRD FRs covered |

## Summary and Recommendations

### Overall Readiness Status

NEEDS WORK

The implementation artifacts are not blocked by missing PRD coverage: required documents exist, the PRD is complete, 104/104 PRD functional requirements are mapped to epics, and story acceptance criteria coverage is strong. However, the planning set should not be treated as fully ready until the route inconsistencies and epic-structure defects are addressed or explicitly accepted.

### Critical Issues Requiring Immediate Action

1. Technical epic structure violates create-epics-and-stories standards. Several epics are implementation-layer milestones rather than user-value slices, especially early foundation epics.
2. API route inconsistencies exist across UX, PRD, and architecture. UX uses `/api/commands`; PRD uses `/api/v1/commands`; architecture contains another status route variant.
3. The first-run/onboarding value path is sequenced too late. Aspire, sample app, and first running topology appear in Epic 8 even though they are core PRD/UX success gates.
4. Several stories are compound and should be split before new implementation work uses them as execution units.
5. Epics 14-21 are not self-contained in `epics.md`; they rely on linked implementation artifact files for story detail.

### Recommended Next Steps

1. Canonicalize API route shapes across PRD, UX, architecture, epics, OpenAPI expectations, and tests. Treat `/api/v1/commands`, `/api/v1/commands/status/{correlationId}`, and `/api/v1/commands/replay/{correlationId}` as canonical unless the product owner deliberately changes them.
2. Reframe or annotate technical epics with explicit user outcomes. At minimum, add outcome statements that explain who can do what after each epic completes.
3. Pull a thin walking skeleton into the earliest epic sequence: AppHost, sample domain service, one command, one persisted event, and one observable trace.
4. Split oversized stories before assigning them for implementation, especially Story 3.5, Story 8.5, Story 22.1, Story 22.5, and Story 22.7d.
5. Add compact story summaries for Epics 14-21 directly in `epics.md`, while keeping links to the detailed implementation artifact files.
6. Normalize phase labels for query/projection caching so PRD, epics, and sprint status all agree whether the work is current release, v1.1, or v2.
7. Preserve UX-DR41 through UX-DR59 as acceptance criteria in Admin Web UI, CLI, MCP, and advanced debugging stories; architecture supports them only at a high level.

### Final Note

This assessment identified 8 issues across 4 categories:

- 1 critical epic-structure violation
- 4 major readiness issues
- 2 minor consistency concerns
- 1 UX/API route alignment warning that also affects story quality

The artifacts are strong on requirements coverage and acceptance criteria mechanics. The main risk is not missing requirements; it is that implementers could follow technically organized epics and inconsistent route examples into avoidable rework. Address the critical and major issues before using the planning set as the primary implementation guide.

Assessment completed on 2026-05-17 by Codex using `bmad-check-implementation-readiness`.
