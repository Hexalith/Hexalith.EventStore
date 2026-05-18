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
    - _bmad-output/planning-artifacts/prd.md
  architecture:
    - _bmad-output/planning-artifacts/architecture.md
  epics:
    - _bmad-output/planning-artifacts/epics.md
  ux:
    - _bmad-output/planning-artifacts/ux-design-specification.md
supportingContext:
  sprintChangeProposals:
    - _bmad-output/planning-artifacts/sprint-change-proposal-2026-04-16-epic-21-post-boot-fixes.md
    - _bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md
    - _bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md
    - _bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md
    - _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-05-18
**Project:** Hexalith.EventStore

## Document Inventory

### PRD

**Whole Documents:**
- `prd.md` (122,035 bytes, modified 2026-05-12 10:00:05)

**Sharded Documents:**
- None found.

**Related Reports Excluded From Source Assessment:**
- `prd-documentation-validation-report.md` (7,255 bytes, modified 2026-02-24 14:16:01)
- `prd-validation-report-2026-03-14.md` (36,464 bytes, modified 2026-03-14 11:57:41)
- `prd-validation-report.md` (8,417 bytes, modified 2026-03-13 17:12:49)

### Architecture

**Whole Documents:**
- `architecture.md` (117,068 bytes, modified 2026-05-17 12:45:37)

**Sharded Documents:**
- None found.

### Epics & Stories

**Whole Documents:**
- `epics.md` (139,292 bytes, modified 2026-05-17 13:20:06)

**Sharded Documents:**
- None found.

**Supporting Context:**
- `sprint-change-proposal-2026-04-16-epic-21-post-boot-fixes.md` (26,879 bytes, modified 2026-04-16 18:27:05)
- `sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` (34,040 bytes, modified 2026-04-26 11:17:17)
- `sprint-change-proposal-2026-04-26-epic-3-retro-cleanup.md` (14,697 bytes, modified 2026-04-26 12:14:47)
- `sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md` (14,239 bytes, modified 2026-04-26 12:46:48)
- `sprint-change-proposal-2026-05-01-epic-10-retro-cleanup.md` (14,343 bytes, modified 2026-05-01 13:17:03)

### UX Design

**Whole Documents:**
- `ux-design-specification.md` (145,127 bytes, modified 2026-05-17 12:45:37)

**Sharded Documents:**
- None found.

### Discovery Issues

- No critical duplicate whole-vs-sharded document formats found.
- No required document type appears to be missing.

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

- Event sourcing invariants: append-only immutability, strict per-aggregate ordering, deterministic/idempotent event application, and irreversible event envelope/message type conventions.
- Data integrity constraints: optimistic concurrency through ETags, no partial writes, and snapshot consistency where snapshot plus tail events equals full replay.
- Multi-tenant isolation constraints: tenant-scoped data paths, tenant-bearing storage keys, and per-tenant-per-domain pub/sub topics.
- Operational domain patterns: event stream as audit log, temporal state through replay, and dead-letter topics plus structured logs as the v1 operational contract.
- Runtime stack constraints: .NET 10 LTS, C# 14, DAPR sidecar model, Aspire orchestration/deployment, and `net10.0` target frameworks.
- Package architecture constraints: Contracts, Client, Server, SignalR, Aspire, and Testing packages have distinct ownership and consumers; event envelope and domain service contract changes are major version changes.
- API constraints: command and query endpoints are JWT-authenticated except health/readiness, use documented response codes, and keep server-derived tenant/user/domain/causation fields out of client payloads.
- Authorization model: six-layer defense in depth from JWT validation through DAPR policy enforcement.
- Storage and distribution constraints: two-document event storage, composite DAPR keys, and per-tenant-per-domain event/dead-letter topic naming.
- Testing constraints: unit tests are DAPR-free, integration tests use DAPR test containers, and contract tests use the full Aspire topology.
- Phasing constraints: v1 must provide the complete event sourcing pipeline plus query/projection caching; v2 focuses on admin control plane, workflow migration, and operational tooling.

### PRD Completeness Assessment

The PRD is broad and requirement-rich, with 104 FRs and 46 NFRs spanning command processing, event persistence, distribution, domain integration, identity, security, observability, developer experience, query/projection caching, admin tooling, public gateway contracts, downstream integration, and protected payload handling.

Initial completeness is strong, but traceability risk is elevated because requirements are not strictly ordered by numeric identifier in the PRD (for example FR49 appears between FR8 and FR9, FR65-FR67 appear before FR17/FR21, and FR68 resumes after FR64). Coverage validation must use requirement IDs, not document order.

## Epic Coverage Validation

### Coverage Matrix

| FR Number | Epic Coverage | Coverage Note | Status |
| --------- | ------------- | ------------- | ------ |
| FR1 | Epic 1 + 3 | Command types (Epic 1), REST endpoint (Epic 3) | Covered |
| FR2 | Epic 1 | Command validation types and MessageType value object | Covered |
| FR3 | Epic 2 | Command routing to aggregate actor | Covered |
| FR4 | Epic 3 | Correlation ID on command submission | Covered |
| FR5 | Epic 3 | Command status query endpoint | Covered |
| FR6 | Epic 3 | Failed command replay | Covered |
| FR7 | Epic 3 | Optimistic concurrency rejection | Covered |
| FR8 | Epic 3 | Dead-letter routing | Covered |
| FR9 | Epic 2 | Append-only immutable event persistence | Covered |
| FR10 | Epic 2 | Gapless sequence numbers per aggregate | Covered |
| FR11 | Epic 1 | 14-field event metadata envelope | Covered |
| FR12 | Epic 2 | State reconstruction via event replay | Covered |
| FR13 | Epic 7 | Configurable snapshots | Covered |
| FR14 | Epic 2 | Snapshot + tail event reconstruction | Covered |
| FR15 | Epic 2 | Composite key strategy with tenant isolation | Covered |
| FR16 | Epic 2 | Atomic event writes | Covered |
| FR17 | Epic 4 | Pub/sub with CloudEvents 1.0 | Covered |
| FR18 | Epic 4 | At-least-once delivery | Covered |
| FR19 | Epic 4 | Per-tenant-per-domain topics | Covered |
| FR20 | Epic 4 | Resilient persistence during pub/sub outage | Covered |
| FR21 | Epic 1 | Pure function domain processor contract | Covered |
| FR22 | Epic 8 | Domain service registration via DAPR config | Covered |
| FR23 | Epic 2 | Domain service invocation during command processing | Covered |
| FR24 | Epic 8 | Multi-domain support (2+ domains) | Covered |
| FR25 | Epic 8 | Multi-tenant domain support (2+ tenants) | Covered |
| FR26 | Epic 1 | Canonical identity tuple | Covered |
| FR27 | Epic 5 | Data path isolation | Covered |
| FR28 | Epic 5 | Storage key isolation | Covered |
| FR29 | Epic 5 | Pub/sub topic isolation | Covered |
| FR30 | Epic 5 | JWT authentication | Covered |
| FR31 | Epic 5 | JWT claims-based authorization | Covered |
| FR32 | Epic 5 | Pre-pipeline unauthorized rejection | Covered |
| FR33 | Epic 5 | Actor-level tenant validation | Covered |
| FR34 | Epic 5 | DAPR service-to-service access control | Covered |
| FR35 | Epic 6 | OpenTelemetry traces | Covered |
| FR36 | Epic 6 | Structured logs with correlation/causation IDs | Covered |
| FR37 | Epic 6 | Dead-letter-to-origin tracing | Covered |
| FR38 | Epic 6 | Health check endpoints | Covered |
| FR39 | Epic 6 | Readiness check endpoints | Covered |
| FR40 | Epic 8 | Single Aspire command startup | Covered |
| FR41 | Epic 8 | Sample domain service reference | Covered |
| FR42 | Epic 8 | NuGet packages with zero-config quickstart | Covered |
| FR43 | Epic 8 | Environment deployment via DAPR config only | Covered |
| FR44 | Epic 8 | Aspire publisher deployment manifests | Covered |
| FR45 | Epic 8 | Unit tests without DAPR | Covered |
| FR46 | Epic 8 | Integration tests with DAPR containers | Covered |
| FR47 | Epic 8 | E2E contract tests | Covered |
| FR48 | Epic 1 | EventStoreAggregate base class with conventions | Covered |
| FR49 | Epic 2 | Duplicate command detection via ULID tracking | Covered |
| FR50 | Epic 9 | 3-tier query routing model | Covered |
| FR51 | Epic 9 | ETag actor per projection+tenant | Covered |
| FR52 | Epic 9 | NotifyProjectionChanged helper | Covered |
| FR53 | Epic 9 | ETag pre-check returning HTTP 304 | Covered |
| FR54 | Epic 9 | Query actor in-memory page cache | Covered |
| FR55 | Epic 10 | SignalR "changed" broadcast | Covered |
| FR56 | Epic 10 | SignalR hub with Redis backplane | Covered |
| FR57 | Epic 9 | Query contract library with typed metadata | Covered |
| FR58 | Epic 9 | Coarse invalidation per projection+tenant | Covered |
| FR59 | Epic 10 | Automatic SignalR group rejoining | Covered |
| FR60 | Epic 12 | 3 reference Blazor refresh patterns | Covered |
| FR61 | Epic 9 | Self-routing ETag encode/decode | Covered |
| FR62 | Epic 9 | IQueryResponse<T> compile-time enforcement | Covered |
| FR63 | Epic 9 | Runtime projection type discovery | Covered |
| FR64 | Epic 13 | Short projection type name guidance | Covered |
| FR65 | Epic 1 | metadataVersion field in envelope | Covered |
| FR66 | Epic 1 | Aggregate tombstoning via terminal event | Covered |
| FR67 | Epic 4 | Per-aggregate backpressure (HTTP 429) | Covered |
| FR68 | Epic 15 | Recently active streams listing | Covered |
| FR69 | Epic 15 | Unified command/event/query timeline | Covered |
| FR70 | Epic 15 + 20 | Point-in-time state exploration | Covered |
| FR71 | Epic 15 + 20 | Aggregate state diff | Covered |
| FR72 | Epic 20 | Full causation chain tracing | Covered |
| FR73 | Epic 15 | Projection management with controls | Covered |
| FR74 | Epic 15 | Event/command/aggregate type catalog | Covered |
| FR75 | Epic 15 + 19 | Operational health + DAPR visibility | Covered |
| FR76 | Epic 16 | Storage management | Covered |
| FR77 | Epic 16 via Hexalith.Tenants | Tenant management; lifecycle, users, and configuration managed by Hexalith.Tenants peer service | Covered |
| FR78 | Epic 16 | Dead-letter queue management | Covered |
| FR79 | Epic 14 | Three-interface shared Admin API | Covered |
| FR80 | Epic 17 | CLI output formats, exit codes, completions | Covered |
| FR81 | Epic 18 | MCP structured tools with approval gates | Covered |
| FR82 | Epic 15 | Observability deep links | Covered |
| FR83 | Story 22.1a | API-facing command/query DTOs and stable ProblemDetails extension names | Covered |
| FR84 | Story 22.1b | High-level EventStore client methods for command/query/status/replay/read paths | Covered |
| FR85 | Story 22.1c | Deterministic gateway fakes and builders in EventStore.Testing | Covered |
| FR86 | Story 22.1d | Package ownership documentation for Contracts, Client, Testing, and runtime internals | Covered |
| FR87 | Story 22.2 | Projection adapter or documented generic query actor contract | Covered |
| FR88 | Story 22.2 | Get/List/Search domain query routing through POST /api/v1/queries | Covered |
| FR89 | Story 22.2 | Generic versus domain-specific projection actor guidance | Covered |
| FR90 | Story 22.3 | Gateway tenant lifecycle, membership, role, and permission validation | Covered |
| FR91 | Story 22.3 | Hexalith.Tenants tenant/RBAC validator adapters with fail-closed behavior | Covered |
| FR92 | Story 22.3 | Stable 401/403 ProblemDetails taxonomy | Covered |
| FR93 | Story 22.4 | Query paging, filtering, blank search, and deterministic ordering policy | Covered |
| FR94 | Story 22.4 | Query response metadata contract | Covered |
| FR95 | Story 22.4 | Query error taxonomy | Covered |
| FR96 | Story 22.5a | Durable at-least-once published event guarantees and ordering notes | Covered |
| FR97 | Story 22.5b + 22.5c | Pub/sub deployment matrix, metadata, retry, drain, and dead-letter policy | Covered |
| FR98 | Story 22.5d | Backend-specific publish/order/dead-letter tests | Covered |
| FR99 | Story 22.6 | Stream read/replay APIs for projection rebuild | Covered |
| FR100 | Story 22.6 | Operator-safe projection rebuild flows | Covered |
| FR101 | Story 22.6 | Projection rebuild documentation using public APIs | Covered |
| FR102 | Story 22.7a | Payload and snapshot protection hooks | Covered |
| FR103 | Story 22.7c | Crypto-shredding and restored-backup safety workflows | Covered |
| FR104 | Story 22.7d | Protected-data redaction across logs, admin APIs, UI, CLI, MCP, ProblemDetails, replay, rebuild, and tests | Covered |

### Missing Requirements

No PRD functional requirements are missing from the epics FR coverage map.

No FR IDs appear in the epics coverage map that are absent from the PRD.

### Coverage Statistics

- Total PRD FRs: 104
- FRs covered in epics: 104
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

Found: `_bmad-output/planning-artifacts/ux-design-specification.md` (145,127 bytes, modified 2026-05-17 12:45:37).

No sharded UX folder was found.

### PRD Alignment

- The UX document matches the PRD's multi-surface product model: developer SDK, REST API consumer experience, CLI/Aspire operator experience, Blazor admin dashboard, admin CLI, and MCP server.
- UX personas align with PRD journeys for Marco, Priya, Sanjay, and Alex, and the later admin-tooling UX requirements align with PRD administration requirements FR68-FR82.
- v1 API error journeys align with the PRD's public gateway and ProblemDetails requirements, especially FR83-FR95 and NFR security/authorization expectations.
- Query and SignalR UI expectations align with PRD query/projection caching requirements FR50-FR64 and NFR35-NFR39.

### Architecture Alignment

- The architecture explicitly accounts for UX-DR41-UX-DR59 through ADR-P4 (single Admin.Server/API architecture) and ADR-P5 (observability deep-link strategy), with interaction details delegated to Epics 15, 17, 18, and 20.
- Architecture support exists for REST/API UX expectations via D5 ProblemDetails, D8 rate limiting, the command lifecycle data flow, OpenTelemetry tracing, and health/readiness endpoints.
- Blazor dashboard architecture is aligned at the platform level: Blazor Server, Fluent UI 5.x, real-time SignalR, external observability deep links, and Admin API/CLI/MCP split.

### Alignment Issues

1. Envelope metadata count drift:
   - PRD FR11 defines a 14-field metadata envelope.
   - UX line 136 still says "All 11 event envelope metadata fields populated by EventStore."
   - Architecture lines 123, 1127, and 1257 still refer to "11 envelope metadata fields" in SEC-1.
   - Impact: implementers may use stale envelope ownership/security rules and miss `event message ID`, `aggregate type`, `globalPosition`, `metadataVersion`, or the removal/renaming of older fields such as `serializationFormat`.
   - Recommendation: update UX and architecture SEC-1 wording to the current PRD FR11/FR65 14-field envelope and two-document storage model.

2. Fluent UI version drift:
   - UX, architecture, epics, project context, and Epic 21 all establish Fluent UI v5 as the admin UI baseline.
   - PRD line 740 still lists "Blazor Fluent UI V4" as the Admin Web UI dependency.
   - Impact: a story author could select obsolete component APIs or migration assumptions.
   - Recommendation: update the PRD Phase 2 Admin Web UI dependency to "Blazor Fluent UI v5" and reference the Epic 21 migration baseline.

3. API error terminology conflict:
   - UX rule E6 says consumer-facing ProblemDetails must not include event sourcing terminology such as "aggregate."
   - The UX 409 Conflict example says "Another command was processed for aggregate 'order-42' while yours was in flight."
   - UX cross-surface rule C1 also says "Aggregate" is shared terminology in error messages.
   - Impact: tests based on A6 would fail against the UX example, and implementation guidance is ambiguous.
   - Recommendation: decide whether API consumers may see `aggregateId` terminology. If not, rewrite the 409 example and C1; if yes, narrow E6/A6 to prohibit internal terms such as "actor", "DAPR", "sidecar", and "event stream" while allowing stable API fields like `aggregateId`.

### Warnings

- No missing UX documentation warning: UX is present and detailed.
- No blocking architecture gap found for the v2 admin UX surfaces, but several detailed UX interactions are intentionally story-level acceptance criteria rather than architecture-level decisions.

## Epic Quality Review

### Review Scope

Validated `_bmad-output/planning-artifacts/epics.md` against implementation-readiness standards:

- Epics should deliver recognizable user value rather than only technical milestones.
- Epics should be independently valuable in sequence, with no forward dependencies.
- Stories should be independently completable, testable, and appropriately sized.
- Acceptance criteria should be specific, verifiable, and preferably Given/When/Then.

### Critical Violations

None found that block traceability outright. The FR coverage map is complete and no explicit forward dependency was found where an earlier epic requires a later epic to function.

### Major Issues

1. Several epics are still framed as technical layers rather than user-value slices.
   - Examples: Epic 1 "Domain Contract Foundation", Epic 2 "Event Persistence & Aggregate Processing", Epic 4 "Event Distribution & Pub/Sub", Epic 7 "Snapshots, Rate Limiting & Performance".
   - Impact: these titles and groupings can encourage implementation by subsystem rather than by end-to-end usable outcome.
   - Recommendation: preserve the existing FR mapping, but add explicit user-value completion statements for each technical epic, such as "developer can process a command end-to-end with persisted events" or "operator can keep processing stable under growth/abuse pressure."

2. Epic 7 combines three different concerns: snapshots, per-tenant rate limiting, and per-consumer rate limiting.
   - Impact: it is cohesive only under a broad performance/resilience umbrella; story sequencing and testing can drift because snapshots affect rehydration while rate limiting affects gateway behavior.
   - Recommendation: split validation evidence by concern, or add a rationale explaining why these remain one epic and how each story is independently shippable.

3. Epic 8 is very broad across Aspire orchestration, sample app, client package, hot reload, testing, deployment manifests, and CI/CD.
   - Impact: this epic crosses developer onboarding, package publishing, test strategy, deployment, and release engineering. It is harder to assess as one independently complete user outcome.
   - Recommendation: keep Epic 8 as an onboarding/release umbrella only if each story retains independent acceptance evidence; otherwise split future work into onboarding, testing, deployment, and release automation tracks.

4. Completed admin epics (14-21) use summarized story bullets instead of full story specs in the epics document.
   - Examples: Epics 14-20 list "Completed stories" with outcome, key acceptance, and artifact links, but not full Given/When/Then acceptance criteria inline.
   - Impact: readiness depends on the linked implementation artifacts. The main planning document alone is not enough to validate acceptance completeness or edge-case coverage.
   - Recommendation: for any remaining or future implementation, either inline the acceptance criteria or require the linked implementation artifacts as mandatory review inputs before starting work.

5. Some completed historical stories are too broad by current standards.
   - Story 3.5 covers authentication failure, authorization failure, concurrency conflict, infrastructure unavailability, and correlation ID serialization in one story.
   - Story 8.5 covers the full three-tier test pyramid.
   - Story 8.6 covers deployment manifests and environment portability.
   - Impact: broad stories make independent completion and review harder, even though the document includes split maps for some future follow-ups.
   - Recommendation: treat the included split maps as binding for future remediation or regression work; do not assign similarly broad stories going forward.

### Minor Concerns

1. Epic 12 depends conceptually on SignalR notifications from Epic 10, but the overview does not list an explicit dependency.
   - Recommendation: add `Dependencies: Epic 10` to Epic 12.

2. Epic 11 is supplemental implementation scope for query/projection behavior but is not tied to explicit PRD FRs.
   - Recommendation: keep the coverage note, and ensure future server-managed projection-builder work is represented in PRD requirements before new implementation stories are opened.

3. Story numbering around Epic 15 includes `15.12`, `15.12a`, and `15.12b`.
   - Impact: acceptable for historical remediation, but it complicates ordering and tooling.
   - Recommendation: future addenda should use normal sequential story numbers or clearly marked patch-story conventions.

4. Container-only stories in Epic 22 are clearly marked, but the overview still counts them in the story list.
   - Recommendation: keep the "do not assign" language and ensure sprint planning tools cannot pick parent container stories directly.

### Best Practices Compliance Checklist

| Area | Result | Notes |
| ---- | ------ | ----- |
| FR traceability | Pass | 104/104 PRD FRs covered in the epics coverage map. |
| No explicit forward dependencies | Pass | Listed dependencies are backward or historical; no earlier epic explicitly requires a later one. |
| User-value framing | Partial | Outcomes exist, but several epic titles/groupings are technical. |
| Story sizing | Partial | Many stories are good; several completed historical stories are broad and rely on split maps for future work. |
| Acceptance criteria quality | Partial | Epics 1-13 and 22 mostly include Given/When/Then; Epics 14-21 rely on artifact links. |
| Dependency clarity | Partial | Epic 12 should list Epic 10; Epic 11's supplemental status should stay explicit. |
| Starter/project setup coverage | Pass | Epic 8 covers Aspire AppHost, sample, topology, testing, deployment, and CI/CD. |
| Greenfield/brownfield fit | Pass | Planning recognizes greenfield platform work plus integration with existing Hexalith/DAPR/Aspire ecosystem. |

### Readiness Implication

Epic-level FR coverage is ready, but story-level implementation readiness is uneven. The safest implementation path is to assign work from the full story specs where present, and for completed/historical admin areas, require the linked implementation artifact as the source of acceptance criteria before any follow-up work begins.

## Summary and Recommendations

### Overall Readiness Status

NEEDS WORK

The planning set is close: all required planning documents exist, PRD requirements are extractable, and the epics coverage map accounts for all 104 PRD functional requirements. However, the artifacts are not clean enough to declare full implementation readiness because several source-of-truth conflicts and story-quality risks remain.

### Critical Issues Requiring Immediate Action

No critical traceability blocker was found.

The highest-priority issues to resolve before implementation are:

1. Metadata envelope source-of-truth conflict:
   - PRD FR11/FR65 says the event envelope has 14 metadata fields.
   - UX and architecture still refer to 11 metadata fields in key SEC-1 guidance.
   - This must be corrected because envelope ownership is a security and compatibility boundary.

2. Fluent UI version conflict:
   - UX, architecture, epics, project context, and Epic 21 use Fluent UI v5.
   - PRD Phase 2 Admin Web UI still says Fluent UI V4.
   - This should be corrected before any admin UI follow-up stories are assigned.

3. API error terminology conflict:
   - UX rule E6 says ProblemDetails must not include terms like "aggregate."
   - The UX 409 example and cross-surface terminology rule allow/use aggregate terminology.
   - This must be resolved before writing or reviewing API error-response tests.

4. Story readiness evidence gap for completed admin epics:
   - Epics 14-21 summarize completed stories and point to implementation artifacts.
   - The epics document alone does not contain enough acceptance detail to validate those stories.
   - Future work in those areas must load the linked implementation artifacts before implementation or review.

### Recommended Next Steps

1. Update UX and architecture SEC-1 references from "11 envelope metadata fields" to the current 14-field PRD envelope, including `metadataVersion` and two-document storage language.

2. Update the PRD Admin Web UI dependency from "Blazor Fluent UI V4" to "Blazor Fluent UI v5" and reference Epic 21 as the established migration baseline.

3. Decide the API error terminology policy, then update UX E6/A6, C1, and the 409 conflict example so they agree.

4. Add `Dependencies: Epic 10` to Epic 12, since Blazor refresh patterns rely on SignalR projection-change notification behavior.

5. For any new follow-up work in Epics 14-21, require the linked implementation artifact as the acceptance source; do not rely only on the summarized story bullets.

6. Treat broad historical stories such as 3.5, 8.5, and 8.6 as closed history. Use their split maps for future work rather than reopening them as single large stories.

7. Improve epic framing where practical by adding user-value completion statements to technical epics, especially Epics 1, 2, 4, 7, and 8.

### Issue Count

- Document discovery issues: 0 critical, 0 missing required documents
- PRD extraction issues: 0 extraction blockers, 1 ordering/traceability caution
- Epic coverage issues: 0 missing FRs, 0 extra FRs
- UX alignment issues: 3
- Epic quality issues: 5 major, 4 minor

Total issues requiring attention: 13

### Final Note

This assessment found 13 issues across PRD clarity, UX/architecture alignment, and epic/story quality. The artifacts are usable, but they need targeted cleanup before they should be treated as fully implementation-ready. The strongest part of the planning set is FR traceability; the weakest part is source-of-truth consistency across older and newer planning layers.

Assessment date: 2026-05-18

Assessor: Codex using `bmad-check-implementation-readiness`
