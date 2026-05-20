---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
includedFiles:
  prd:
    - D:\Hexalith.EventStore\_bmad-output\planning-artifacts\prd.md
  architecture:
    - D:\Hexalith.EventStore\_bmad-output\planning-artifacts\architecture.md
  epicsAndStories:
    - D:\Hexalith.EventStore\_bmad-output\planning-artifacts\epics.md
  uxDesign:
    - D:\Hexalith.EventStore\_bmad-output\planning-artifacts\ux-design-specification.md
excludedFiles:
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

**Date:** 2026-05-20
**Project:** Hexalith.EventStore

## Document Discovery

Primary assessment documents confirmed:

- PRD: `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\prd.md`
- Architecture: `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\architecture.md`
- Epics & Stories: `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\epics.md`
- UX Design: `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\ux-design-specification.md`

Excluded from the primary assessment set:

- `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\prd-documentation-validation-report.md`
- `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\prd-validation-report-2026-03-14.md`
- `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\prd-validation-report.md`
- `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\sprint-change-proposal-2026-04-16-epic-21-post-boot-fixes.md`
- `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md`
- `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md`
- `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md`
- `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md`

No critical duplicate whole-plus-sharded document formats were found.

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
- FR72: The admin tool can trace the full causation chain for any event — originating command, sender identity, correlation ID, and downstream projections affected
- FR73: The admin tool can list all projections with status, lag, throughput, error count, and last processed position — with controls to pause, resume, reset from position, or replay
- FR74: The admin tool can browse all registered event types, command types, and aggregate types with their schemas, relationships, and version history
- FR75: The admin tool can display an operational health dashboard with event count, throughput, error rate, DAPR component status, and deep links to configured observability tools (Zipkin/Jaeger, Prometheus/Grafana, Aspire Dashboard)
- FR76: The admin tool can manage storage — show growth trends, hot streams, and trigger compaction, snapshot creation, and backup operations
- FR77: The admin tool can manage tenants — quotas, onboarding, comparison, and isolation verification. Tenant lifecycle (create, enable/disable, users, roles, configuration) is managed by Hexalith.Tenants peer service; EventStore admin UI/CLI/MCP consume its API
- FR78: The admin tool can manage dead-letter queues — browse, search, retry, skip, archive failed events with bulk operations
- FR79: All admin read and write operations are accessible through three interfaces: Blazor Web UI, CLI (`eventstore-admin`), and MCP server — backed by a shared Admin API
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
- NFR44: All admin data access must go through DAPR abstractions exclusively — the admin tool must be state-store-backend-agnostic, inheriting DAPR's portability guarantee
- NFR45: Admin Web UI must support at least 10 concurrent users with independent views without performance degradation
- NFR46: Admin API must enforce role-based access control — read-only for developers, operator for DBAs (projection controls, snapshot/compaction), admin for infrastructure operations (tenant management, backup/restore)

Total NFRs: 46

### Additional Requirements

- MVP go/no-go gates require an end-to-end command-to-event pipeline, zero data loss across chaos scenarios, confirmed three-layer multi-tenant isolation, Redis/PostgreSQL infrastructure swap validation, clone-to-running under 10 minutes via Aspire, event envelope validation through 3+ domain service implementations, and at least one Hexalith DDD application built on the backbone.
- Domain-specific invariants require append-only immutability, strict per-aggregate ordering, deterministic/idempotent event application, and careful handling of the irreversible 14-field event envelope plus `{domain}-{name}-v{ver}` message type convention.
- Data integrity constraints require ETag optimistic concurrency, atomic 0-or-N event writes, and snapshot consistency where snapshot plus tail replay equals full replay.
- Multi-tenant isolation requirements require data-path isolation, storage-key isolation, and pub/sub topic isolation.
- Operational patterns require the event stream to serve as audit log, temporal queries through replay, and dead-letter topics plus structured logging as the v1 operational contract.
- Runtime architecture requires .NET 10/C# 14, DAPR sidecar model, Aspire orchestration, `net10.0`, DAPR actors, state store, pub/sub, configuration, and resiliency building blocks for v1; DAPR workflows are deferred to v2.
- Package architecture requires coordinated SemVer across Contracts, Client, Server, SignalR, Aspire, and Testing packages; event-envelope and domain-service contract changes are major version changes.
- Command API requirements define `/api/v1/commands`, command status, replay, `/health`, `/ready`, `/api/v1/queries`, and query validation endpoints with JWT scoping.
- Authentication and authorization require six defense layers: JWT validation, claims enrichment, endpoint policy authorization, MediatR authorization behavior, actor identity verification, and DAPR policy enforcement.
- Event storage schema requirements define two-document metadata/payload storage, composite state-store key patterns, and per-tenant/per-domain event and dead-letter topic naming.
- Implementation constraints include DAPR sidecar overhead, actor activation rehydration cost, at-least-once pub/sub requiring idempotent subscribers, backend-specific state-store transaction behavior, and a three-tier testing strategy.
- Phasing constraints keep the Blazor admin dashboard, DAPR Workflow migration, saga/process-manager support, external authorization engine, advanced enterprise features, templates, tutorials, gRPC API, and plugin architecture outside the v1 platform MVP unless otherwise re-scoped.
- Resource constraints explicitly assume one senior .NET developer for v1, no additional team members, Redis local validation, PostgreSQL production validation, and strict v1 feature-freeze discipline to avoid scope creep.

### PRD Completeness Assessment

The PRD is rich and traceable enough for coverage validation: it contains 104 functional requirements, 46 non-functional requirements, user journeys, go/no-go gates, technical architecture, API contracts, identity/auth rules, data schema constraints, risk mitigation, and release phasing. It is implementation-ready as a source document, but the readiness assessment should watch three issues closely:

- Version drift: the PRD states DAPR 1.16.1 and Aspire 13.1.x, while current project context says DAPR 1.17.7 and Aspire 13.2.2.
- Requirement ordering: all FR numbers FR1-FR104 are present, but FR49 and FR65-FR67 are inserted inside earlier topic sections rather than sorted numerically.
- Scope layering: current release, v1.1, v2, v3, and v4 requirements live in the same PRD, so epic coverage must distinguish current implementation scope from future roadmap scope.

## Epic Coverage Validation

### Coverage Matrix

| FR Number | PRD Requirement | Epic Coverage | Status |
| --------- | --------------- | ------------- | ------ |
| FR1 | An API consumer can submit a command to the EventStore via a REST endpoint with message ID (ULID), aggregate ID (ULID), command type (kebab `{domain}-{command}-v{ver}`), and payload | Epic 1 + 3 - Command types (Epic 1), REST endpoint (Epic 3) | Covered |
| FR2 | The system can validate a submitted command for structural completeness (required fields: messageId, aggregateId, commandType, payload; valid ULID format for messageId and aggregateId; valid `{domain}-{command}-v{ver}` kebab format for commandType via MessageType value object; well-formed JSON structure) before routing it for processing | Epic 1 - Command validation types and MessageType value object | Covered |
| FR3 | The system can route a command to the correct aggregate actor based on the identity scheme (`tenant:domain:aggregate-id`) | Epic 2 - Command routing to aggregate actor | Covered |
| FR4 | An API consumer can receive a correlation ID upon command submission for tracking the command lifecycle. The correlation ID defaults to the client-supplied messageId (ULID) if not provided. The client's messageId also serves as the idempotency key | Epic 3 - Correlation ID on command submission | Covered |
| FR5 | An API consumer can query the processing status of a previously submitted command using its correlation ID | Epic 3 - Command status query endpoint | Covered |
| FR6 | An operator can replay a previously failed command via the Command API after root cause is fixed | Epic 3 - Failed command replay | Covered |
| FR7 | The system can reject duplicate commands targeting an aggregate that has an optimistic concurrency conflict, returning an appropriate error | Epic 3 - Optimistic concurrency rejection | Covered |
| FR8 | The system can route failed commands to a dead-letter topic with full command payload, error details, and correlation context | Epic 3 - Dead-letter routing | Covered |
| FR9 | The system can persist events in an append-only, immutable event store where events are never modified or deleted after persistence | Epic 2 - Append-only immutable event persistence | Covered |
| FR10 | The system can assign strictly ordered, gapless sequence numbers to events within a single aggregate stream. Cross-aggregate event ordering is explicitly not guaranteed and must not be relied upon by consumers | Epic 2 - Gapless sequence numbers per aggregate | Covered |
| FR11 | The system can wrap each event in a 14-field metadata envelope (event message ID, aggregate ID, aggregate type, tenant, sequence number, global position, timestamp, correlation ID, causation ID, user identity, event type, domain service version, metadata version, extension bag) stored as separate metadata JSON and payload JSON (two-document storage per D14) | Epic 1 - 14-field event metadata envelope | Covered |
| FR12 | The system can reconstruct aggregate state by replaying all events in an aggregate's stream from sequence 1 to current | Epic 2 - State reconstruction via event replay | Covered |
| FR13 | The system can create snapshots of aggregate state at administrator-configured event count intervals (default: every 100 events, configurable per tenant-domain pair) to optimize state rehydration. The EventStore signals the domain service when a snapshot threshold is reached; the domain service produces the snapshot content inline as part of command processing | Epic 7 - Configurable snapshots | Covered |
| FR14 | The system can reconstruct aggregate state from the latest snapshot plus subsequent events, producing identical state to full replay | Epic 2 - Snapshot + tail event reconstruction | Covered |
| FR15 | The system can store events using a composite key strategy that includes tenant, domain, and aggregate identity for isolation | Epic 2 - Composite key strategy with tenant isolation | Covered |
| FR16 | The system can enforce atomic event writes -- a command produces 0 or N events as a single transaction, never a partial subset | Epic 2 - Atomic event writes | Covered |
| FR17 | The system can publish persisted events to subscribers via a pub/sub mechanism using CloudEvents 1.0 envelope format | Epic 4 - Pub/sub with CloudEvents 1.0 | Covered |
| FR18 | The system can deliver events to subscribers with at-least-once delivery guarantee | Epic 4 - At-least-once delivery | Covered |
| FR19 | The system can publish events to per-tenant-per-domain topics, ensuring subscribers only receive events for their authorized scope | Epic 4 - Per-tenant-per-domain topics | Covered |
| FR20 | The system can continue persisting events when the pub/sub system is temporarily unavailable, draining the backlog on recovery | Epic 4 - Resilient persistence during pub/sub outage | Covered |
| FR21 | A domain service developer can implement a domain processor as a pure function with the contract `(Command, CurrentState?) -> DomainResult`. The domain service returns only aggregate type (short kebab), event types (.NET types, EventStore converts to kebab), and event payloads (pure business facts). EventStore handles all metadata enrichment | Epic 1 - Pure function domain processor contract | Covered |
| FR22 | A domain service developer's domain service is automatically routed by convention (DAPR AppId matches the domain name, method "process") with zero configuration. Routing can be overridden via static registrations (appsettings.json) or DAPR config store (opt-in via `ConfigStoreName`) for complex scenarios such as per-tenant routing to different services | Epic 8 - Domain service registration via DAPR config | Covered |
| FR23 | The system can invoke a registered domain service when processing a command, passing the command and current aggregate state. The domain service returns a `DomainResult` containing aggregate type and event outputs (event type + payload). EventStore enriches each event with all 14 metadata fields per FR11 | Epic 2 - Domain service invocation during command processing | Covered |
| FR24 | The system can process commands for at least 2 independent domains within the same EventStore instance | Epic 8 - Multi-domain support (2+ domains) | Covered |
| FR25 | The system can process commands for at least 2 tenants within the same domain, each with isolated event streams | Epic 8 - Multi-tenant domain support (2+ tenants) | Covered |
| FR26 | The system can derive actor IDs, event stream keys, and pub/sub topics from a canonical identity tuple (`tenant:domain:aggregate-id`) where tenant is extracted from JWT claims (D15) and domain is parsed from the message type prefix (`{domain}-{name}-v{ver}` per D13) | Epic 1 - Canonical identity tuple | Covered |
| FR27 | The system can enforce data path isolation so that commands for one tenant are never routed to another tenant's domain service | Epic 5 - Data path isolation | Covered |
| FR28 | The system can enforce storage key isolation so that event streams for different tenants are inaccessible to each other even at the state store level | Epic 5 - Storage key isolation | Covered |
| FR29 | The system can enforce pub/sub topic isolation so that event subscribers only receive events from tenants they are authorized to access | Epic 5 - Pub/sub topic isolation | Covered |
| FR30 | An API consumer can authenticate with the Command API using a JWT token | Epic 5 - JWT authentication | Covered |
| FR31 | The system can authorize command submissions based on JWT claims for tenant, domain, and command type | Epic 5 - JWT claims-based authorization | Covered |
| FR32 | The system can reject unauthorized commands at the API gateway before they enter the processing pipeline | Epic 5 - Pre-pipeline unauthorized rejection | Covered |
| FR33 | The system can validate that a command's tenant matches the authenticated user's authorized tenants at the actor level | Epic 5 - Actor-level tenant validation | Covered |
| FR34 | The system can enforce service-to-service access control between EventStore components via DAPR policies | Epic 5 - DAPR service-to-service access control | Covered |
| FR35 | The system can emit OpenTelemetry traces spanning the full command lifecycle (received, processing, events stored, events published, completed) | Epic 6 - OpenTelemetry traces | Covered |
| FR36 | The system can emit structured logs with correlation and causation IDs at each stage of the command processing pipeline | Epic 6 - Structured logs with correlation/causation IDs | Covered |
| FR37 | An operator can trace a failed command from the dead-letter topic back to its originating request via correlation ID | Epic 6 - Dead-letter-to-origin tracing | Covered |
| FR38 | The system can expose health check endpoints indicating DAPR sidecar, state store, and pub/sub connectivity status | Epic 6 - Health check endpoints | Covered |
| FR39 | The system can expose readiness check endpoints indicating all dependencies are healthy and the system is accepting commands | Epic 6 - Readiness check endpoints | Covered |
| FR40 | A developer can start the complete EventStore system (server, sample domain service, state store, message broker, OpenTelemetry) with a single Aspire command | Epic 8 - Single Aspire command startup | Covered |
| FR41 | A developer can reference a sample domain service implementation as a working example of the pure function programming model | Epic 8 - Sample domain service reference | Covered |
| FR42 | A developer can install EventStore client packages via NuGet to build and register domain services, with a zero-configuration quickstart via convention-based `AddEventStore()` registration and auto-discovery of domain types | Epic 8 - NuGet packages with zero-config quickstart | Covered |
| FR43 | A DevOps engineer can deploy EventStore to different environments by changing only DAPR component configuration files with zero application code changes | Epic 8 - Environment deployment via DAPR config only | Covered |
| FR44 | A DevOps engineer can generate deployment manifests for Docker Compose, Kubernetes, or Azure Container Apps via Aspire publishers | Epic 8 - Aspire publisher deployment manifests | Covered |
| FR45 | A developer can run unit tests against domain service pure functions without any DAPR runtime dependency | Epic 8 - Unit tests without DAPR | Covered |
| FR46 | A developer can run integration tests against the actor processing pipeline using DAPR test containers | Epic 8 - Integration tests with DAPR containers | Covered |
| FR47 | A developer can run end-to-end contract tests validating the full command lifecycle across the complete Aspire topology | Epic 8 - E2E contract tests | Covered |
| FR48 | A domain service developer can implement a domain aggregate by inheriting from EventStoreAggregate with typed Apply methods, as a higher-level alternative to implementing IDomainProcessor directly, with convention-based DAPR resource naming derived from the aggregate type name | Epic 1 - EventStoreAggregate base class with conventions | Covered |
| FR49 | The system can detect and reject duplicate commands by tracking processed command message IDs (client-generated ULIDs) per aggregate, returning an idempotent success response for already-processed commands | Epic 2 - Duplicate command detection via ULID tracking | Covered |
| FR50 | The system can route incoming query messages to query actors using a 3-tier routing model: (1) queries with EntityId route to `{QueryType}-{TenantId}-{EntityId}`, (2) queries without EntityId but with non-empty payload route to `{QueryType}-{TenantId}-{Checksum}` where Checksum is a truncated SHA256 base64url hash (11 characters) of the serialized payload, (3) queries without EntityId and with empty payload route to `{QueryType}-{TenantId}`. Note: serialization non-determinism (e.g., JSON key ordering differences) produces different checksums for semantically identical queries, resulting in separate cache actors -- this is an accepted trade-off; callers are responsible for consistent serialization | Epic 9 - 3-tier query routing model | Covered |
| FR51 | The system can maintain one ETag actor per `{ProjectionType}:{TenantId}` that stores a self-routing ETag (format defined in FR61) representing the current projection version, regenerated on every projection change notification | Epic 9 - ETag actor per projection+tenant | Covered |
| FR52 | A domain service developer can notify EventStore of a projection change by calling `NotifyProjectionChanged(projectionType, tenantId, entityId?)` via NuGet helper, with the underlying transport (DAPR pub/sub by default, or direct service invocation) selected by configuration | Epic 9 - NotifyProjectionChanged helper | Covered |
| FR53 | The query REST endpoint can perform an ETag pre-check (first gate) by decoding the self-routing ETag from the client's `If-None-Match` header to extract the projection type, then calling the corresponding ETag actor -- if the GUID portion matches the current ETag, the endpoint returns HTTP 304 without activating the query actor. If the ETag is missing, malformed, undecodable, or references a non-existent projection type, the endpoint treats it as a cache miss and routes to the query actor | Epic 9 - ETag pre-check returning HTTP 304 | Covered |
| FR54 | A query actor can serve as an in-memory page cache (second gate) with no state store persistence. On first activation (cold call), the query actor forwards the query to the microservice, receives `IQueryResponse<T>` containing data and projection type, caches both the data and the projection type mapping, and returns the result with a self-routing ETag header. On subsequent requests, the query actor uses its learned projection type mapping to check the ETag actor and re-queries only on mismatch. Deactivation (DAPR idle timeout) resets the mapping -- the next request is a cold call to the microservice. FR53 is the hot-path optimization; FR54 operates independently when the query actor is activated (e.g., client has no ETag or ETag is stale) | Epic 9 - Query actor in-memory page cache | Covered |
| FR55 | The system can broadcast a signal-only "changed" message to connected SignalR clients when a projection's ETag is regenerated, with clients grouped by ETag actor ID (`{ProjectionType}:{TenantId}`) | Epic 10 - SignalR "changed" broadcast | Covered |
| FR56 | The system can host a SignalR hub inside the EventStore server, using a Redis backplane for multi-instance SignalR message distribution (a DAPR-managed Redis instance may be reused in supported deployments) | Epic 10 - SignalR hub with Redis backplane | Covered |
| FR57 | A query contract library (NuGet) can define mandatory query metadata fields (Domain, QueryType, TenantId) and optional fields (EntityId) as typed static members, serving as the single source of truth for query routing. ProjectionType is not required on the query consumer side (browser, API caller) -- it is declared by the microservice in its `IQueryResponse<T>` implementation (FR62) and discovered at runtime by the query actor (FR63) | Epic 9 - Query contract library with typed metadata | Covered |
| FR58 | The system can invalidate all cached query results for a projection+tenant pair on any projection change notification (coarse invalidation model -- all filters invalidated per projection per tenant). Rationale: coarse invalidation trades unnecessary cache refreshes for design simplicity -- fine-grained filter-aware invalidation would require the EventStore to understand projection schemas, violating the platform's opacity principle | Epic 9 - Coarse invalidation per projection+tenant | Covered |
| FR59 | The SignalR client helper (NuGet) can automatically rejoin SignalR groups on connection recovery, restoring real-time push notification after Blazor Server circuit reconnection, WebSocket drops, or network interruption -- without requiring manual intervention by the developer | Epic 10 - Automatic SignalR group rejoining | Covered |
| FR60 | The EventStore documentation and sample application can provide at least 3 reference patterns for handling the SignalR "changed" signal in Blazor UI components: (1) toast notification prompting manual refresh, (2) automatic silent data reload, (3) selective component refresh targeting only the affected projection | Epic 12 - 3 reference Blazor refresh patterns | Covered |
| FR61 | The system can encode self-routing ETags in the format `{base64url(projectionType)}.{guid}` and decode them at the query endpoint to extract projection type routing information. ETags are always wrapped in quotes in HTTP response headers per RFC 7232. Undecodable, malformed, or missing ETags are treated as cache misses, ensuring safe degradation by construction | Epic 9 - Self-routing ETag encode/decode | Covered |
| FR62 | The microservice query response contract (`IQueryResponse<T>`) can enforce at compile time that every query response includes a non-empty `ProjectionType` field, eliminating silent caching degradation when a microservice omits or leaves blank the projection mapping information. The query actor treats an empty or whitespace-only ProjectionType as an error equivalent to a missing response | Epic 9 - IQueryResponse<T> compile-time enforcement | Covered |
| FR63 | A query actor can discover its projection type mapping at runtime from the microservice's `IQueryResponse<T>` response on its first (cold) call, storing the mapping in memory for subsequent ETag actor lookups. The mapping resets on actor deactivation (DAPR idle timeout), ensuring the next cold call re-learns the mapping from the microservice | Epic 9 - Runtime projection type discovery | Covered |
| FR64 | The EventStore documentation can recommend short projection type names (e.g., `OrderList` rather than fully qualified type names) to keep self-routing ETags compact in HTTP headers, with guidance that projection type names are base64url-encoded in the ETag and longer names produce proportionally longer tokens | Epic 13 - Short projection type name guidance | Covered |
| FR65 | The event metadata envelope can include a `metadataVersion` field (integer, starting at 1) enabling external consumers to detect envelope schema changes and adapt their deserialization without breaking. Internal consumers use the same version for forward-compatibility checks | Epic 1 - metadataVersion field in envelope | Covered |
| FR66 | The system can mark an aggregate as terminated (tombstoned) via a terminal event. A terminated aggregate rejects all subsequent commands with a domain rejection event, while its event stream remains immutable and replayable | Epic 1 - Aggregate tombstoning via terminal event | Covered |
| FR67 | The system can apply backpressure when aggregate command queues exceed a configurable depth threshold (default: 100 pending commands per aggregate), returning HTTP 429 with Retry-After header to prevent saga storms and head-of-line blocking cascades. Backpressure is per-aggregate, not system-wide | Epic 4 - Per-aggregate backpressure (HTTP 429) | Covered |
| FR68 | The admin tool can list recently active streams (configurable count, default 1000) with stream type, last activity timestamp, and status indicator across all tenants | Epic 15 - Recently active streams listing | Covered |
| FR69 | The admin tool can display a unified command/event/query timeline for any aggregate stream, with before/after state snapshots per event | Epic 15 - Unified command/event/query timeline | Covered |
| FR70 | The admin tool can show aggregate state at any historical event position or timestamp (point-in-time state exploration) | Epic 15 + 20 - Point-in-time state exploration | Covered |
| FR71 | The admin tool can diff aggregate state between any two event positions, highlighting changed fields | Epic 15 + 20 - Aggregate state diff | Covered |
| FR72 | The admin tool can trace the full causation chain for any event — originating command, sender identity, correlation ID, and downstream projections affected | Epic 20 - Full causation chain tracing | Covered |
| FR73 | The admin tool can list all projections with status, lag, throughput, error count, and last processed position — with controls to pause, resume, reset from position, or replay | Epic 15 - Projection management with controls | Covered |
| FR74 | The admin tool can browse all registered event types, command types, and aggregate types with their schemas, relationships, and version history | Epic 15 - Event/command/aggregate type catalog | Covered |
| FR75 | The admin tool can display an operational health dashboard with event count, throughput, error rate, DAPR component status, and deep links to configured observability tools (Zipkin/Jaeger, Prometheus/Grafana, Aspire Dashboard) | Epic 15 + 19 - Operational health + DAPR visibility | Covered |
| FR76 | The admin tool can manage storage — show growth trends, hot streams, and trigger compaction, snapshot creation, and backup operations | Epic 16 - Storage management | Covered |
| FR77 | The admin tool can manage tenants — quotas, onboarding, comparison, and isolation verification. Tenant lifecycle (create, enable/disable, users, roles, configuration) is managed by Hexalith.Tenants peer service; EventStore admin UI/CLI/MCP consume its API | Epic 16 (via Hexalith.Tenants) - Tenant management — lifecycle, users, configuration managed by Hexalith.Tenants peer service; EventStore admin UI/CLI/MCP consume its API | Covered |
| FR78 | The admin tool can manage dead-letter queues — browse, search, retry, skip, archive failed events with bulk operations | Epic 16 - Dead-letter queue management | Covered |
| FR79 | All admin read and write operations are accessible through three interfaces: Blazor Web UI, CLI (`eventstore-admin`), and MCP server — backed by a shared Admin API | Epic 14 - Three-interface shared Admin API | Covered |
| FR80 | The admin CLI supports JSON, CSV, and table output formats with pipe-friendly streaming, exit codes (0 healthy, 1 degraded, 2 critical), and shell completion scripts | Epic 17 - CLI output formats, exit codes, completions | Covered |
| FR81 | The admin MCP server exposes all read operations as structured tools returning machine-readable JSON, with approval-gated write operations (pause/reset/replay projections, trigger backups) | Epic 18 - MCP structured tools with approval gates | Covered |
| FR82 | Every trace, metric, and log view in the admin Web UI deep-links to the corresponding detail in the configured external observability tool rather than replicating its UI | Epic 15 - Observability deep links | Covered |
| FR83 | EventStore.Contracts exposes API-facing `SubmitCommandRequest`, `SubmitCommandResponse`, `SubmitQueryRequest`, `SubmitQueryResponse`, validation request/response DTOs, command status DTOs, replay/read DTOs, and stable ProblemDetails extension names used by HTTP gateway clients | Epic 22.1a - API-facing command/query DTOs and stable ProblemDetails extension names | Covered |
| FR84 | EventStore.Client exposes high-level `SubmitCommandAsync`, `SubmitQueryAsync`, validation, command status, replay, and stream-read client methods that handle correlation IDs, ETags, 304 responses, ProblemDetails mapping, and typed cancellation | Epic 22.1b - High-level EventStore client methods for command/query/status/replay/read paths | Covered |
| FR85 | EventStore.Testing exposes deterministic gateway client fakes and builders for command, query, status, replay, ProblemDetails, ETag, tenant/RBAC, stale/degraded, and unavailable paths | Epic 22.1c - Deterministic gateway fakes and builders in EventStore.Testing | Covered |
| FR86 | EventStore documents package ownership rules: API-facing wire contracts live in Contracts, HTTP convenience clients live in Client, deterministic test doubles live in Testing, and runtime server internals remain in Server/EventStore | Epic 22.1d - Package ownership documentation for Contracts, Client, Testing, and runtime internals | Covered |
| FR87 | EventStore.Contracts exposes a stable projection adapter contract for generic query serving, including `QueryEnvelope`, `QueryResult`, projection type metadata, and malformed-response taxonomy, or explicitly documents the generic DAPR actor contract domain services must implement | Epic 22.2 - Projection adapter or documented generic query actor contract | Covered |
| FR88 | EventStore can route `Get*`, `List*`, and `Search*` domain queries through `POST /api/v1/queries` without domain services owning tenant authorization or gateway-specific DTOs | Epic 22.2 - Get/List/Search domain query routing through POST /api/v1/queries | Covered |
| FR89 | EventStore docs define when a domain should use a generic `IProjectionActor.QueryAsync(QueryEnvelope)` adapter versus domain-specific projection actors, including actor type naming, serialization, and test expectations | Epic 22.2 - Generic versus domain-specific projection actor guidance | Covered |
| FR90 | EventStore gateway validates tenant existence, lifecycle state, user membership, and role/permission before invoking a domain service or projection adapter | Epic 22.3 - Gateway tenant lifecycle, membership, role, and permission validation | Covered |
| FR91 | EventStore integrates with Hexalith.Tenants through `ITenantValidator` and `IRbacValidator` adapters with fail-closed behavior when tenant/RBAC data is missing, stale, unavailable, or ambiguous | Epic 22.3 - Hexalith.Tenants tenant/RBAC validator adapters with fail-closed behavior | Covered |
| FR92 | EventStore exposes stable 401/403 ProblemDetails type URIs and reason codes for authentication failure, tenant not found, tenant disabled/suspended, user not a member, insufficient role, insufficient permission, and authorization service unavailable | Epic 22.3 - Stable 401/403 ProblemDetails taxonomy | Covered |
| FR93 | EventStore query contracts define paging bounds, default page size, maximum page size, cursor/offset semantics, blank search behavior, filter validation, and deterministic ordering requirements | Epic 22.4 - Query paging, filtering, blank search, and deterministic ordering policy | Covered |
| FR94 | EventStore query responses define metadata fields for `correlationId`, `etag`, `isNotModified`, `isStale`, `isDegraded`, `projectionVersion`, `servedAt`, paging metadata, and optional warning codes | Epic 22.4 - Query response metadata contract | Covered |
| FR95 | EventStore query error taxonomy defines malformed request, unsupported filter, invalid page, projection missing, projection stale beyond policy, degraded search, malformed projection response, projection timeout, and authorization failures as stable ProblemDetails types | Epic 22.4 - Query error taxonomy | Covered |
| FR96 | EventStore-published events are durable and at-least-once; per-aggregate causal order is preserved when the configured pub/sub backend supports ordering/session keys, and backend limitations are documented | Epic 22.5a - Durable at-least-once published event guarantees and ordering notes | Covered |
| FR97 | EventStore documents and validates pub/sub ordering metadata, partition/session key selection, retry/outbox behavior, dead-letter topic policy, replay/drain behavior, and backend-specific deployment settings | Epic 22.5b + 22.5c - Pub/sub deployment matrix, metadata, retry, drain, and dead-letter policy | Covered |
| FR98 | EventStore integration tests prove publish-after-persist recovery, duplicate delivery tolerance, per-aggregate causal ordering where supported, and dead-letter handling for supported pub/sub backends | Epic 22.5d - Backend-specific publish/order/dead-letter tests | Covered |
| FR99 | EventStore exposes stream read/replay APIs for projection rebuild with tenant/domain/aggregate scoping, sequence checkpoints, continuation tokens, and resumable progress tracking | Epic 22.6 - Stream read/replay APIs for projection rebuild | Covered |
| FR100 | EventStore supports operator-safe projection rebuild flows with pause/resume/cancel, failure reason capture, idempotent checkpoint advancement, and no cross-tenant leakage | Epic 22.6 - Operator-safe projection rebuild flows | Covered |
| FR101 | EventStore documents how domain services rebuild projections from EventStore streams without reading state-store internals | Epic 22.6 - Projection rebuild documentation using public APIs | Covered |
| FR102 | EventStore supports event payload and snapshot protection hooks with metadata that identifies protection state without exposing protected data | Epic 22.7a - Payload and snapshot protection hooks | Covered |
| FR103 | EventStore supports crypto-shredding workflows through key deletion/invalidation semantics, restored-backup safety checks, and explicit behavior for unreadable protected payloads | Epic 22.7c - Crypto-shredding and restored-backup safety workflows | Covered |
| FR104 | EventStore logs, admin APIs, CLI, MCP, and ProblemDetails never leak protected payload or snapshot data, including during replay, rebuild, backup validation, and failure diagnostics | Epic 22.7d - Protected-data redaction across logs, admin APIs, UI, CLI, MCP, ProblemDetails, replay, rebuild, and tests | Covered |

### Missing Requirements

No missing FR coverage found. Every PRD FR from FR1 through FR104 appears in the epics document FR Coverage Map.

No epic-only FRs found. The epics coverage map contains 104 FR entries, all of which correspond to PRD FRs.

### Coverage Statistics

- Total PRD FRs: 104
- FRs covered in epics: 104
- FRs not covered in epics: 0
- FRs in epics but not PRD: 0
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

Found: `D:\Hexalith.EventStore\_bmad-output\planning-artifacts\ux-design-specification.md`

The UX documentation is substantive and covers:

- v1 non-visual experience surfaces: SDK, REST API, Aspire CLI/dashboard onboarding, OpenAPI/Swagger, ProblemDetails, structured logs, and traces.
- v2 admin experience surfaces: Blazor Fluent UI Web UI, `eventstore-admin` CLI, and MCP server.
- Cross-surface vocabulary, command lifecycle states, error semantics, accessibility, responsiveness, and interaction patterns.

### UX to PRD Alignment

Aligned:

- UX target users match PRD personas: domain developers, DevOps/operators, support engineers, DBAs, API consumers, and AI agents.
- UX "zero infrastructure code" principle aligns with PRD developer-experience goals and FR21-FR23, FR40-FR48.
- UX v1 API error journeys align with PRD API contract and public gateway requirements, especially FR83-FR95.
- UX v2 admin requirements align with PRD FR68-FR82 and v2 administration tooling goals.
- UX CLI/MCP requirements align with PRD FR79-FR81.
- UX observability deep-link strategy aligns with PRD FR75 and FR82.
- UX SignalR and refresh patterns align with PRD FR55-FR60.

Watch items:

- UX v1 checklist includes some acceptance criteria that need explicit story-level enforcement, especially resolvable error `type` URI pages, Swagger sample payloads, exact `Retry-After` values, and correlation ID presence/absence by error category.
- UX includes interaction details beyond PRD-level FRs, such as command palette, deep links, breadcrumbs, virtualized rendering, keyboard shortcuts, CLI profiles/REPL/completions, and MCP session state. Architecture acknowledges these as story-level acceptance criteria, so they are not blockers, but they must remain visible during story review.

### UX to Architecture Alignment

Aligned:

- Architecture ADR-P4 supports the three-interface admin model: Admin.Server hosts REST API and Blazor Web UI, while CLI and MCP are thin HTTP clients over the Admin API.
- Architecture ADR-P5 supports UX's observability strategy: domain-aware admin summaries with deep links to Zipkin/Jaeger, Prometheus/Grafana, and Aspire Dashboard rather than embedded duplicate dashboards.
- Architecture ADR-P6 through ADR-P9 support UX's public API, query, tenant/RBAC, replay, publishing, and protected-data UX requirements as platform contracts.
- Architecture requirements coverage explicitly maps FR68-FR82 and notes that UX-DR41 through UX-DR59 are supported by ADR-P4/ADR-P5 and remain story-level acceptance criteria in Epics 15, 17, 18, and 20.
- Fluent UI v5 / Blazor direction in UX aligns with the current project context and Epic 21 baseline.

### Alignment Issues

No blocking UX alignment gaps found.

Non-blocking issues to track:

- Version drift: UX and PRD reference earlier Aspire/DAPR version baselines in places, while project context currently says Aspire 13.2.2 and DAPR 1.17.7.
- Architecture delegates several UX interaction details to stories. This is acceptable, but the readiness review must confirm those details appear in story acceptance criteria before implementation begins.
- UX v1 API error handling is more precise than the PRD in a few areas, especially exact ProblemDetails prose constraints and header behavior. Stories should treat the UX spec as authoritative for consumer-facing error behavior.

### Warnings

No missing UX-documentation warning. UI is implied and explicitly documented.

## Epic Quality Review

### Review Summary

- Explicit story headings reviewed: 79
- Explicit stories with `Acceptance Criteria`: 79
- Linked implementation artifact references checked: 68
- Missing linked implementation artifacts: 0
- Forward epic dependencies found: 0
- PRD FR traceability maintained: yes, 104/104 FRs mapped

### Critical Violations

No critical violations found that would block implementation readiness outright.

The epic/story set has complete FR traceability, no forward epic dependency chain, and no missing acceptance criteria for explicit story headings.

### Major Issues

#### Major 1: Several Epic Titles Remain Technology-Centric

Affected epics:

- Epic 1: Domain Contract Foundation
- Epic 2: Event Persistence & Aggregate Processing
- Epic 4: Event Distribution & Pub/Sub
- Epic 5: Security & Multi-Tenant Isolation
- Epic 7: Snapshots, Rate Limiting & Performance
- Epic 9: Query Pipeline & ETag Caching
- Epic 11: Server-Managed Projection Builder
- Epic 14: Admin API Foundation & Abstractions
- Epic 19: Admin - DAPR Infrastructure Visibility
- Epic 21: Admin UI Fluent UI v5 Stability Migration

Finding:

Most of these epics do include a user-value `Outcome`, but the titles are still implementation-mechanism names rather than user capability names. This violates the create-epics-and-stories standard in title form, even when the body mitigates it.

Impact:

The risk is sprint planning drift: implementation teams may optimize for technical completion instead of user-visible capability or operational outcome.

Recommendation:

Keep the existing IDs, but add user-capability aliases or rename titles in future planning passes. Examples:

- Epic 2: "Turn Submitted Commands into Durable Replayable Event Streams"
- Epic 4: "Deliver Persisted Events Reliably to Subscribers"
- Epic 7: "Keep Command Processing Responsive Under Growth and Abuse"
- Epic 19: "Diagnose DAPR Runtime Health from EventStore Context"

#### Major 2: Parent/Container Stories Must Not Be Assigned Directly

Affected stories:

- Story 22.1: Gateway Command/Query Contract Closure and Package Docs (Container Only)
- Story 22.5: Event Publishing Guarantees and Backend Deployment Matrix (Container Only)

Finding:

Both stories are explicitly marked as container-only and include binding child-story splits. This is good mitigation, but the parent stories still have acceptance criteria and story headings, so a sprint tool or implementer could mistakenly assign them directly.

Impact:

If assigned as implementation units, these stories are too broad and would violate independent story completion. They span multiple package boundaries, docs, tests, backend policies, and evidence artifacts.

Recommendation:

Preserve the child stories as the only assignable units. Mark parent rows as non-implementation containers in sprint-status or any planning board. Keep the container ACs as summary acceptance only.

#### Major 3: Completed Admin Epics Depend on External Detail Artifacts for Full Review

Affected epics:

- Epic 14 through Epic 21

Finding:

The epics document intentionally keeps completed Epics 14-21 compact and links detailed implementation artifacts instead of embedding full story ACs inline. All 68 referenced implementation artifacts exist, so this is not a missing-file problem.

Impact:

Readiness for future work in these areas depends on loading the linked detail artifact before accepting, changing, or testing a story. Without that handoff discipline, story quality cannot be validated from `epics.md` alone.

Recommendation:

Keep the existing handoff rule as binding. Any new implementation, regression fix, or follow-up touching Epics 14-21 should explicitly cite the loaded detail artifact in its story or review notes.

#### Major 4: Starter/Bootstrap Story Is Mitigated but Not First-Class

Finding:

Architecture selects a custom solution assembled from individual templates, but Epic 1 Story 1 is `Core Identity & Event Envelope`, not an initial project setup story. The document adds `Story WS-1: Clone-to-Command Flow Walking Skeleton` before the epic list, which preserves early observable value and largely mitigates this for the current repository.

Impact:

If these planning docs are reused for a greenfield restart, setup/bootstrap work could be hidden inside technical foundation stories rather than managed as a clear first implementation slice.

Recommendation:

For future greenfield or re-bootstrap work, make WS-1 or an explicit "Solution/AppHost Bootstrap" story the first assignable implementation slice. For the current repository, WS-1 is sufficient as a readiness gate.

### Minor Concerns

#### Minor 1: WS-1 Acceptance Criteria Are Not BDD-Formatted

Finding:

`Story WS-1` uses a bullet checklist instead of Given/When/Then acceptance criteria.

Impact:

The checklist is testable, but it deviates from the story AC format used elsewhere.

Recommendation:

Convert WS-1 criteria to Given/When/Then if it is ever assigned as implementation work again.

#### Minor 2: Supplemental Scope Is Documented but Outside Numbered PRD FR Coverage

Finding:

Epic 11 is marked as supplemental implementation scope for the query/projection pipeline and explicitly says it is not counted as additional numbered PRD coverage unless a future PRD update adds explicit server-managed projection-builder FRs.

Impact:

This is well disclosed, but it can confuse readiness statistics because it supports existing query FRs while adding implementation shape not directly represented as a PRD FR.

Recommendation:

Keep Epic 11 tied to its cited approved projection-change scope and do not count it as new PRD coverage unless the PRD is updated.

### Dependency Analysis

- Epic dependency chain is valid: all declared dependencies point to earlier epics.
- No forward epic dependencies found.
- Within-epic dependencies are mostly sequential and backward-looking.
- Stories 3.5 and 8.5 include future split maps for completed broad historical work. This is acceptable only if future work uses the split maps rather than reopening the broad parent story.

### Database/Entity Creation Timing

Relational database-table timing is not directly applicable because the product uses DAPR state-store keys rather than a dedicated relational schema in the planning docs.

State and storage artifacts are introduced where first used:

- Event keys and aggregate metadata: Story 2.2
- Command status keys: Story 2.4
- Pub/sub topics: Story 4.1
- Projection state and checkpoint behavior: Epic 11 stories

No "create every table/model upfront" violation found.

### Best Practices Compliance Checklist

| Area | Status | Notes |
| ---- | ------ | ----- |
| Epic delivers user value | Partial | Outcomes are user-value oriented; several titles remain technical |
| Epic independence | Pass | Declared dependencies point backward only |
| Stories appropriately sized | Partial | Most pass; container stories 22.1 and 22.5 must not be assigned directly |
| No forward dependencies | Pass | No forward epic dependency found |
| Database/entities created when needed | Pass / N/A | DAPR state-store keys are introduced at first use |
| Clear acceptance criteria | Pass | 79/79 explicit stories have AC; WS-1 format is checklist-style |
| Traceability to FRs maintained | Pass | 104/104 PRD FRs mapped |

### Epic Quality Recommendation

Proceed with caution. The epics are traceable and mostly implementation-ready, but planning hygiene depends on preserving the handoff rules:

- Do not assign container-only parent stories.
- Load linked detail artifacts for completed admin/UI epics before any follow-up.
- Treat technical epic titles as labels only; implementation planning should use the user-value outcomes.
- Keep WS-1 as the early value gate for any renewed foundation work.

## Summary and Recommendations

### Overall Readiness Status

READY WITH CONDITIONS

The implementation planning set is strong enough to proceed if the handoff controls are enforced. The PRD, architecture, UX specification, and epics are present, aligned, and traceable. PRD functional requirements have 100% coverage in the epic map, UX is explicitly documented, and no forward epic dependencies were found.

This is not a clean "no concerns" readiness state. The artifacts contain structural risks that can cause implementation drift if ignored.

### Critical Issues Requiring Immediate Action

No critical issues require immediate action before proceeding.

### Material Issues Requiring Attention

1. Technology-centric epic titles remain in several places. The outcomes are user-value oriented, but sprint planning should use those outcomes rather than treating the titles as pure technical milestones.
2. Parent/container stories 22.1 and 22.5 must not be assigned directly. Only their child stories should become implementation units.
3. Completed Admin/UI Epics 14-21 require their linked implementation artifacts for any follow-up review or change. All linked artifacts exist, but readiness depends on loading them when relevant.
4. Bootstrap/setup is mitigated by WS-1, but not first-class as Epic 1 Story 1. This is acceptable for the current repo, but risky if the plan is reused as a greenfield start.
5. PRD and UX version references drift from current project context, especially Aspire and DAPR version baselines.
6. Current-release, v1.1, v2, v3, and v4 requirements live together in the PRD. Sprint planning must distinguish current implementation scope from roadmap scope.
7. UX interaction details are intentionally delegated to story-level acceptance criteria. This is acceptable only if story review keeps UX-DR41 through UX-DR59 visible.
8. WS-1 acceptance criteria are checklist-style rather than Given/When/Then.
9. Epic 11 is supplemental scope outside direct numbered PRD FR coverage and should remain tied to its approved change proposal unless the PRD is updated.

### Recommended Next Steps

1. Add a short "Planning Guardrails" note to sprint-status or implementation handoff: parent/container stories are non-assignable, technical epic titles are labels only, and user-value outcomes drive execution.
2. For any new work touching Epics 14-21, require the linked implementation artifact path in the story/review evidence before approval.
3. Normalize current technology baselines across PRD, UX, architecture, and project context: Aspire 13.2.2 and DAPR 1.17.7 appear to be the current repository baselines.
4. Tag requirements by scope horizon before implementation selection: current release, v1.1, v2, v3, v4.
5. Convert WS-1 to Given/When/Then if it is reused as an assignable story.
6. Add user-capability aliases for technical epic titles without renumbering existing epics.
7. Keep Epic 11 tied to its approved projection-builder change proposal, or add explicit PRD FRs if it becomes permanent product scope.

### Final Note

This assessment identified 9 issues across document hygiene, UX handoff, epic structure, story assignability, scope governance, and technology-baseline consistency. None are critical blockers, but the major issues should be controlled before new implementation begins.

Assessment completed on 2026-05-20 by Codex using the `bmad-check-implementation-readiness` workflow.
