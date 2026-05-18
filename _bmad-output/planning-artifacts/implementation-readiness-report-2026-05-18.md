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
  prdReportsExcluded:
    - _bmad-output/planning-artifacts/prd-documentation-validation-report.md
    - _bmad-output/planning-artifacts/prd-validation-report-2026-03-14.md
    - _bmad-output/planning-artifacts/prd-validation-report.md
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
- `prd.md` (122,120 bytes, modified 2026-05-18 08:34:50)

**Sharded Documents:**
- None found.

**Related Reports Excluded From Source Assessment:**
- `prd-documentation-validation-report.md` (7,255 bytes, modified 2026-02-24 14:16:01)
- `prd-validation-report-2026-03-14.md` (36,464 bytes, modified 2026-03-14 11:57:41)
- `prd-validation-report.md` (8,417 bytes, modified 2026-03-13 17:12:49)

### Architecture

**Whole Documents:**
- `architecture.md` (117,238 bytes, modified 2026-05-18 08:34:50)

**Sharded Documents:**
- None found.

### Epics & Stories

**Whole Documents:**
- `epics.md` (139,805 bytes, modified 2026-05-18 08:34:50)

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
- `ux-design-specification.md` (145,648 bytes, modified 2026-05-18 08:35:38)

**Sharded Documents:**
- None found.

### Discovery Issues

- No critical duplicate whole-vs-sharded document formats found.
- No required document type appears to be missing.
- PRD validation report files were discovered by the PRD search pattern and excluded from source assessment.
## PRD Analysis

### Functional Requirements


### Command Processing

- FR1: An API consumer can submit a command to the EventStore via a REST endpoint with message ID (ULID), aggregate ID (ULID), command type (kebab `{domain}-{command}-v{ver}`), and payload
- FR2: The system can validate a submitted command for structural completeness (required fields: messageId, aggregateId, commandType, payload; valid ULID format for messageId and aggregateId; valid `{domain}-{command}-v{ver}` kebab format for commandType via MessageType value object; well-formed JSON structure) before routing it for processing
- FR3: The system can route a command to the correct aggregate actor based on the identity scheme (`tenant:domain:aggregate-id`)
- FR4: An API consumer can receive a correlation ID upon command submission for tracking the command lifecycle. The correlation ID defaults to the client-supplied messageId (ULID) if not provided. The client's messageId also serves as the idempotency key
- FR5: An API consumer can query the processing status of a previously submitted command using its correlation ID
- FR6: An operator can replay a previously failed command via the Command API after root cause is fixed
- FR7: The system can reject duplicate commands targeting an aggregate that has an optimistic concurrency conflict, returning an appropriate error
- FR8: The system can route failed commands to a dead-letter topic with full command payload, error details, and correlation context
- FR49: The system can detect and reject duplicate commands by tracking processed command message IDs (client-generated ULIDs) per aggregate, returning an idempotent success response for already-processed commands

### Event Management

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

### Event Distribution

- FR17: The system can publish persisted events to subscribers via a pub/sub mechanism using CloudEvents 1.0 envelope format
- FR18: The system can deliver events to subscribers with at-least-once delivery guarantee
- FR19: The system can publish events to per-tenant-per-domain topics, ensuring subscribers only receive events for their authorized scope
- FR20: The system can continue persisting events when the pub/sub system is temporarily unavailable, draining the backlog on recovery
- FR67: The system can apply backpressure when aggregate command queues exceed a configurable depth threshold (default: 100 pending commands per aggregate), returning HTTP 429 with Retry-After header to prevent saga storms and head-of-line blocking cascades. Backpressure is per-aggregate, not system-wide

### Domain Service Integration

- FR21: A domain service developer can implement a domain processor as a pure function with the contract `(Command, CurrentState?) -> DomainResult`. The domain service returns only aggregate type (short kebab), event types (.NET types, EventStore converts to kebab), and event payloads (pure business facts). EventStore handles all metadata enrichment
- FR22: A domain service developer's domain service is automatically routed by convention (DAPR AppId matches the domain name, method "process") with zero configuration. Routing can be overridden via static registrations (appsettings.json) or DAPR config store (opt-in via `ConfigStoreName`) for complex scenarios such as per-tenant routing to different services
- FR23: The system can invoke a registered domain service when processing a command, passing the command and current aggregate state. The domain service returns a `DomainResult` containing aggregate type and event outputs (event type + payload). EventStore enriches each event with all 14 metadata fields per FR11
- FR24: The system can process commands for at least 2 independent domains within the same EventStore instance
- FR25: The system can process commands for at least 2 tenants within the same domain, each with isolated event streams

### Identity & Multi-Tenancy

- FR26: The system can derive actor IDs, event stream keys, and pub/sub topics from a canonical identity tuple (`tenant:domain:aggregate-id`) where tenant is extracted from JWT claims (D15) and domain is parsed from the message type prefix (`{domain}-{name}-v{ver}` per D13)
- FR27: The system can enforce data path isolation so that commands for one tenant are never routed to another tenant's domain service
- FR28: The system can enforce storage key isolation so that event streams for different tenants are inaccessible to each other even at the state store level
- FR29: The system can enforce pub/sub topic isolation so that event subscribers only receive events from tenants they are authorized to access

### Security & Authorization

- FR30: An API consumer can authenticate with the Command API using a JWT token
- FR31: The system can authorize command submissions based on JWT claims for tenant, domain, and command type
- FR32: The system can reject unauthorized commands at the API gateway before they enter the processing pipeline
- FR33: The system can validate that a command's tenant matches the authenticated user's authorized tenants at the actor level
- FR34: The system can enforce service-to-service access control between EventStore components via DAPR policies

### Observability & Operations

- FR35: The system can emit OpenTelemetry traces spanning the full command lifecycle (received, processing, events stored, events published, completed)
- FR36: The system can emit structured logs with correlation and causation IDs at each stage of the command processing pipeline
- FR37: An operator can trace a failed command from the dead-letter topic back to its originating request via correlation ID
- FR38: The system can expose health check endpoints indicating DAPR sidecar, state store, and pub/sub connectivity status
- FR39: The system can expose readiness check endpoints indicating all dependencies are healthy and the system is accepting commands

### Developer Experience & Deployment

- FR40: A developer can start the complete EventStore system (server, sample domain service, state store, message broker, OpenTelemetry) with a single Aspire command
- FR41: A developer can reference a sample domain service implementation as a working example of the pure function programming model
- FR42: A developer can install EventStore client packages via NuGet to build and register domain services, with a zero-configuration quickstart via convention-based `AddEventStore()` registration and auto-discovery of domain types
- FR43: A DevOps engineer can deploy EventStore to different environments by changing only DAPR component configuration files with zero application code changes
- FR44: A DevOps engineer can generate deployment manifests for Docker Compose, Kubernetes, or Azure Container Apps via Aspire publishers
- FR45: A developer can run unit tests against domain service pure functions without any DAPR runtime dependency
- FR46: A developer can run integration tests against the actor processing pipeline using DAPR test containers
- FR47: A developer can run end-to-end contract tests validating the full command lifecycle across the complete Aspire topology
- FR48: A domain service developer can implement a domain aggregate by inheriting from EventStoreAggregate with typed Apply methods, as a higher-level alternative to implementing IDomainProcessor directly, with convention-based DAPR resource naming derived from the aggregate type name

### Query Pipeline & Projection Caching (current release)

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

### Administration Tooling — v2 (FR68-FR82)

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

### Public Gateway and Downstream Integration Contracts - v1.1 (FR83-FR104)

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


### Performance

- NFR1: Command submission via the REST API must complete (return 202 Accepted) within 50ms at p99 under normal load
- NFR2: End-to-end command lifecycle (API receipt to event published on pub/sub) must complete within 200ms at p99
- NFR3: Event append latency (actor persist to state store confirmation) must be under 10ms at p99
- NFR4: Actor cold activation with state rehydration (snapshot + subsequent events) must complete within 50ms at p99
- NFR5: Pub/sub delivery (event persistence to subscriber delivery confirmation) must complete within 50ms at p99
- NFR6: Full aggregate state reconstruction from 1,000 events must complete within 100ms
- NFR7: The system must support at least 100 concurrent command submissions per second per EventStore instance without exceeding latency targets
- NFR8: DAPR sidecar overhead per building block call must not exceed 2ms at p99

### Security

- NFR9: All communication between API consumers and the Command API must be encrypted via TLS 1.2+
- NFR10: JWT tokens must be validated for signature, expiry, and issuer on every request before any processing occurs
- NFR11: Failed authentication or authorization attempts must be logged with request metadata (source IP, attempted tenant, attempted command type) without logging the JWT token itself
- NFR12: Event payload data must never appear in log output; only event metadata (envelope fields) may be logged
- NFR13: Multi-tenant data isolation must be enforced at all three layers (actor identity, DAPR policies, command metadata) -- failure at one layer must not compromise isolation
- NFR14: Secrets (connection strings, JWT signing keys, DAPR component credentials) must never be stored in application code or configuration files committed to source control
- NFR15: Service-to-service communication between EventStore components must be authenticated and authorized via DAPR access control policies

### Scalability

- NFR16: The system must support horizontal scaling by adding EventStore server replicas, with DAPR actor placement distributing aggregates across replicas
- NFR17: The system must support at least 10,000 active aggregates per EventStore instance without degradation beyond defined latency targets
- NFR18: The system must support at least 10 tenants per EventStore instance with full isolation and no cross-tenant performance interference
- NFR19: Event stream growth per aggregate must be bounded by the snapshot strategy -- state rehydration time must remain constant regardless of total event count (snapshot + tail events only)
- NFR20: Adding a new tenant or domain must not require system restart or downtime -- configuration changes via DAPR config store must take effect dynamically

### Reliability

- NFR21: The system must achieve 99.9%+ availability with HA DAPR control plane and multi-replica deployment
- NFR22: Zero events may be lost under any tested failure scenario (state store crash, actor crash, pub/sub unavailability, network partition)
- NFR23: After a state store recovery, the system must resume processing from the last checkpointed state with deterministic replay -- no manual intervention required
- NFR24: After a pub/sub recovery, all events persisted during the outage must be delivered to subscribers via DAPR retry policies -- no events silently dropped
- NFR25: Actor crash after event persistence but before pub/sub delivery must not result in duplicate event persistence -- the checkpointed state machine must resume from the correct stage
- NFR26: Optimistic concurrency conflicts must be detected and reported (409 Conflict) -- never silently overwriting events

### Integration

- NFR27: The system must function correctly with any DAPR-compatible state store that supports key-value operations and ETag-based optimistic concurrency (validated: Redis, PostgreSQL)
- NFR28: The system must function correctly with any DAPR-compatible pub/sub component that supports CloudEvents 1.0 and at-least-once delivery (validated: RabbitMQ, Azure Service Bus)
- NFR29: Switching between validated backend configurations must require only DAPR component YAML changes -- zero application code, zero recompilation, zero redeployment of application containers
- NFR30: Domain services must be invocable via DAPR service invocation over HTTP -- the EventStore must not impose language or framework constraints on domain service implementations beyond the pure function contract
- NFR31: OpenTelemetry telemetry must be exportable to any OTLP-compatible collector (validated: Aspire dashboard, Jaeger, Grafana/Tempo)
- NFR32: The system must be deployable via Aspire publishers to Docker Compose, Kubernetes, and Azure Container Apps without custom deployment scripts

### Rate Limiting

- NFR33: The Command API must enforce per-tenant rate limiting with a configurable threshold (default: 1,000 commands per minute per tenant), returning 429 Too Many Requests with Retry-After header when exceeded
- NFR34: The Command API must enforce per-consumer rate limiting with a configurable threshold (default: 100 commands per second per authenticated consumer), returning 429 Too Many Requests with Retry-After header when exceeded

### Query Pipeline Performance (current release)

- NFR35: ETag pre-check at the query endpoint (ETag actor call + comparison) must complete within 5ms at p99 for warm ETag actors, enabling HTTP 304 responses without activating the query actor. Cold ETag actor activation (first call after idle timeout) may exceed this target due to DAPR actor placement
- NFR36: Query actor cache hit (ETag match, return cached data) must complete within 10ms at p99
- NFR37: Query actor cache miss (ETag mismatch, re-query projection via domain service, cache result) must complete within 200ms at p99
- NFR38: SignalR "changed" signal delivery from ETag regeneration to connected client receipt must complete within 100ms at p99
- NFR39: The query pipeline must support at least 1,000 concurrent query requests per second per EventStore instance without exceeding latency targets

### Administration Tooling (NFR40-NFR46)

- NFR40: Admin API responses must complete within 500ms at p99 for read operations and 2s at p99 for write operations (projection reset, backup trigger)
- NFR41: Admin Web UI must render the operational health dashboard within 2 seconds on initial load, with subsequent SignalR-pushed updates within 200ms
- NFR42: Admin CLI must start and return results for simple queries (health check, stream info) within 3 seconds including .NET runtime startup
- NFR43: Admin MCP server must respond to tool calls within 1 second at p99 for single-resource queries
- NFR44: All admin data access must go through DAPR abstractions exclusively — the admin tool must be state-store-backend-agnostic, inheriting DAPR's portability guarantee
- NFR45: Admin Web UI must support at least 10 concurrent users with independent views without performance degradation
- NFR46: Admin API must enforce role-based access control — read-only for developers, operator for DBAs (projection controls, snapshot/compaction), admin for infrastructure operations (tenant management, backup/restore)

Total NFRs: 46

### Additional Requirements

- Domain-specific invariants require append-only event streams, tenant/domain/aggregate identity boundaries, event ordering per aggregate, replayable historical events, and explicit dead-letter handling for infrastructure failures.
- API and integration constraints include REST command and query endpoints, JWT-backed tenant and permission authorization, DAPR service invocation for domain services, CloudEvents 1.0 publication, DAPR-compatible state stores/pub-sub backends, OpenTelemetry export, and Aspire-based local/deployment orchestration.
- Data schema requirements define ULID identifiers, command type naming as {domain}-{command}-v{ver}, tenant/domain/aggregate composite keys, per-tenant/domain topics, a 14-field event metadata envelope, metadata/payload two-document storage, and metadataVersion compatibility handling.
- Product phasing constrains Phase 1 to the operationally minimal EventStore pipeline without a dedicated UI, while Phase 2+ adds query/projection caching, SignalR, admin web/CLI/MCP surfaces, and enterprise governance features.
- Key implementation assumptions include infrastructure-agnostic DAPR components, a pure-function domain processor model, EventStore-owned envelope enrichment, no direct domain-service state/pub-sub ownership, and dynamic tenant/domain configuration through DAPR config where applicable.

### PRD Completeness Assessment

The PRD is complete enough for traceability analysis: it contains numbered FRs and NFRs, user journeys, domain constraints, API/integration contracts, data schemas, and phased scope. Requirement numbering is non-linear because later requirement groups were added after the initial set, so coverage validation must match by requirement ID rather than sequence position.
## Epic Coverage Validation

### Epic FR Coverage Extracted

- FR1: Covered in Epic 1 + 3 - Command types (Epic 1), REST endpoint (Epic 3)
- FR2: Covered in Epic 1 - Command validation types and MessageType value object
- FR3: Covered in Epic 2 - Command routing to aggregate actor
- FR4: Covered in Epic 3 - Correlation ID on command submission
- FR5: Covered in Epic 3 - Command status query endpoint
- FR6: Covered in Epic 3 - Failed command replay
- FR7: Covered in Epic 3 - Optimistic concurrency rejection
- FR8: Covered in Epic 3 - Dead-letter routing
- FR9: Covered in Epic 2 - Append-only immutable event persistence
- FR10: Covered in Epic 2 - Gapless sequence numbers per aggregate
- FR11: Covered in Epic 1 - 14-field event metadata envelope
- FR12: Covered in Epic 2 - State reconstruction via event replay
- FR13: Covered in Epic 7 - Configurable snapshots
- FR14: Covered in Epic 2 - Snapshot + tail event reconstruction
- FR15: Covered in Epic 2 - Composite key strategy with tenant isolation
- FR16: Covered in Epic 2 - Atomic event writes
- FR17: Covered in Epic 4 - Pub/sub with CloudEvents 1.0
- FR18: Covered in Epic 4 - At-least-once delivery
- FR19: Covered in Epic 4 - Per-tenant-per-domain topics
- FR20: Covered in Epic 4 - Resilient persistence during pub/sub outage
- FR21: Covered in Epic 1 - Pure function domain processor contract
- FR22: Covered in Epic 8 - Domain service registration via DAPR config
- FR23: Covered in Epic 2 - Domain service invocation during command processing
- FR24: Covered in Epic 8 - Multi-domain support (2+ domains)
- FR25: Covered in Epic 8 - Multi-tenant domain support (2+ tenants)
- FR26: Covered in Epic 1 - Canonical identity tuple
- FR27: Covered in Epic 5 - Data path isolation
- FR28: Covered in Epic 5 - Storage key isolation
- FR29: Covered in Epic 5 - Pub/sub topic isolation
- FR30: Covered in Epic 5 - JWT authentication
- FR31: Covered in Epic 5 - JWT claims-based authorization
- FR32: Covered in Epic 5 - Pre-pipeline unauthorized rejection
- FR33: Covered in Epic 5 - Actor-level tenant validation
- FR34: Covered in Epic 5 - DAPR service-to-service access control
- FR35: Covered in Epic 6 - OpenTelemetry traces
- FR36: Covered in Epic 6 - Structured logs with correlation/causation IDs
- FR37: Covered in Epic 6 - Dead-letter-to-origin tracing
- FR38: Covered in Epic 6 - Health check endpoints
- FR39: Covered in Epic 6 - Readiness check endpoints
- FR40: Covered in Epic 8 - Single Aspire command startup
- FR41: Covered in Epic 8 - Sample domain service reference
- FR42: Covered in Epic 8 - NuGet packages with zero-config quickstart
- FR43: Covered in Epic 8 - Environment deployment via DAPR config only
- FR44: Covered in Epic 8 - Aspire publisher deployment manifests
- FR45: Covered in Epic 8 - Unit tests without DAPR
- FR46: Covered in Epic 8 - Integration tests with DAPR containers
- FR47: Covered in Epic 8 - E2E contract tests
- FR48: Covered in Epic 1 - EventStoreAggregate base class with conventions
- FR49: Covered in Epic 2 - Duplicate command detection via ULID tracking
- FR50: Covered in Epic 9 - 3-tier query routing model
- FR51: Covered in Epic 9 - ETag actor per projection+tenant
- FR52: Covered in Epic 9 - NotifyProjectionChanged helper
- FR53: Covered in Epic 9 - ETag pre-check returning HTTP 304
- FR54: Covered in Epic 9 - Query actor in-memory page cache
- FR55: Covered in Epic 10 - SignalR "changed" broadcast
- FR56: Covered in Epic 10 - SignalR hub with Redis backplane
- FR57: Covered in Epic 9 - Query contract library with typed metadata
- FR58: Covered in Epic 9 - Coarse invalidation per projection+tenant
- FR59: Covered in Epic 10 - Automatic SignalR group rejoining
- FR60: Covered in Epic 12 - 3 reference Blazor refresh patterns
- FR61: Covered in Epic 9 - Self-routing ETag encode/decode
- FR62: Covered in Epic 9 - IQueryResponse<T> compile-time enforcement
- FR63: Covered in Epic 9 - Runtime projection type discovery
- FR64: Covered in Epic 13 - Short projection type name guidance
- FR65: Covered in Epic 1 - metadataVersion field in envelope
- FR66: Covered in Epic 1 - Aggregate tombstoning via terminal event
- FR67: Covered in Epic 4 - Per-aggregate backpressure (HTTP 429)
- FR68: Covered in Epic 15 - Recently active streams listing
- FR69: Covered in Epic 15 - Unified command/event/query timeline
- FR70: Covered in Epic 15 + 20 - Point-in-time state exploration
- FR71: Covered in Epic 15 + 20 - Aggregate state diff
- FR72: Covered in Epic 20 - Full causation chain tracing
- FR73: Covered in Epic 15 - Projection management with controls
- FR74: Covered in Epic 15 - Event/command/aggregate type catalog
- FR75: Covered in Epic 15 + 19 - Operational health + DAPR visibility
- FR76: Covered in Epic 16 - Storage management
- FR77: Covered in Epic 16 (via Hexalith.Tenants) - Tenant management — lifecycle, users, configuration managed by Hexalith.Tenants peer service; EventStore admin UI/CLI/MCP consume its API
- FR78: Covered in Epic 16 - Dead-letter queue management
- FR79: Covered in Epic 14 - Three-interface shared Admin API
- FR80: Covered in Epic 17 - CLI output formats, exit codes, completions
- FR81: Covered in Epic 18 - MCP structured tools with approval gates
- FR82: Covered in Epic 15 - Observability deep links
- FR83: Covered in Epic 22.1a - API-facing command/query DTOs and stable ProblemDetails extension names
- FR84: Covered in Epic 22.1b - High-level EventStore client methods for command/query/status/replay/read paths
- FR85: Covered in Epic 22.1c - Deterministic gateway fakes and builders in EventStore.Testing
- FR86: Covered in Epic 22.1d - Package ownership documentation for Contracts, Client, Testing, and runtime internals
- FR87: Covered in Epic 22.2 - Projection adapter or documented generic query actor contract
- FR88: Covered in Epic 22.2 - Get/List/Search domain query routing through POST /api/v1/queries
- FR89: Covered in Epic 22.2 - Generic versus domain-specific projection actor guidance
- FR90: Covered in Epic 22.3 - Gateway tenant lifecycle, membership, role, and permission validation
- FR91: Covered in Epic 22.3 - Hexalith.Tenants tenant/RBAC validator adapters with fail-closed behavior
- FR92: Covered in Epic 22.3 - Stable 401/403 ProblemDetails taxonomy
- FR93: Covered in Epic 22.4 - Query paging, filtering, blank search, and deterministic ordering policy
- FR94: Covered in Epic 22.4 - Query response metadata contract
- FR95: Covered in Epic 22.4 - Query error taxonomy
- FR96: Covered in Epic 22.5a - Durable at-least-once published event guarantees and ordering notes
- FR97: Covered in Epic 22.5b + 22.5c - Pub/sub deployment matrix, metadata, retry, drain, and dead-letter policy
- FR98: Covered in Epic 22.5d - Backend-specific publish/order/dead-letter tests
- FR99: Covered in Epic 22.6 - Stream read/replay APIs for projection rebuild
- FR100: Covered in Epic 22.6 - Operator-safe projection rebuild flows
- FR101: Covered in Epic 22.6 - Projection rebuild documentation using public APIs
- FR102: Covered in Epic 22.7a - Payload and snapshot protection hooks
- FR103: Covered in Epic 22.7c - Crypto-shredding and restored-backup safety workflows
- FR104: Covered in Epic 22.7d - Protected-data redaction across logs, admin APIs, UI, CLI, MCP, ProblemDetails, replay, rebuild, and tests

Total FRs in epics: 104

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
| FR49 | The system can detect and reject duplicate commands by tracking processed command message IDs (client-generated ULIDs) per aggregate, returning an idempotent success response for already-processed commands | Epic 2 - Duplicate command detection via ULID tracking | Covered |
| FR9 | The system can persist events in an append-only, immutable event store where events are never modified or deleted after persistence | Epic 2 - Append-only immutable event persistence | Covered |
| FR10 | The system can assign strictly ordered, gapless sequence numbers to events within a single aggregate stream. Cross-aggregate event ordering is explicitly not guaranteed and must not be relied upon by consumers | Epic 2 - Gapless sequence numbers per aggregate | Covered |
| FR11 | The system can wrap each event in a 14-field metadata envelope (event message ID, aggregate ID, aggregate type, tenant, sequence number, global position, timestamp, correlation ID, causation ID, user identity, event type, domain service version, metadata version, extension bag) stored as separate metadata JSON and payload JSON (two-document storage per D14) | Epic 1 - 14-field event metadata envelope | Covered |
| FR12 | The system can reconstruct aggregate state by replaying all events in an aggregate's stream from sequence 1 to current | Epic 2 - State reconstruction via event replay | Covered |
| FR13 | The system can create snapshots of aggregate state at administrator-configured event count intervals (default: every 100 events, configurable per tenant-domain pair) to optimize state rehydration. The EventStore signals the domain service when a snapshot threshold is reached; the domain service produces the snapshot content inline as part of command processing | Epic 7 - Configurable snapshots | Covered |
| FR14 | The system can reconstruct aggregate state from the latest snapshot plus subsequent events, producing identical state to full replay | Epic 2 - Snapshot + tail event reconstruction | Covered |
| FR15 | The system can store events using a composite key strategy that includes tenant, domain, and aggregate identity for isolation | Epic 2 - Composite key strategy with tenant isolation | Covered |
| FR16 | The system can enforce atomic event writes -- a command produces 0 or N events as a single transaction, never a partial subset | Epic 2 - Atomic event writes | Covered |
| FR65 | The event metadata envelope can include a `metadataVersion` field (integer, starting at 1) enabling external consumers to detect envelope schema changes and adapt their deserialization without breaking. Internal consumers use the same version for forward-compatibility checks | Epic 1 - metadataVersion field in envelope | Covered |
| FR66 | The system can mark an aggregate as terminated (tombstoned) via a terminal event. A terminated aggregate rejects all subsequent commands with a domain rejection event, while its event stream remains immutable and replayable | Epic 1 - Aggregate tombstoning via terminal event | Covered |
| FR17 | The system can publish persisted events to subscribers via a pub/sub mechanism using CloudEvents 1.0 envelope format | Epic 4 - Pub/sub with CloudEvents 1.0 | Covered |
| FR18 | The system can deliver events to subscribers with at-least-once delivery guarantee | Epic 4 - At-least-once delivery | Covered |
| FR19 | The system can publish events to per-tenant-per-domain topics, ensuring subscribers only receive events for their authorized scope | Epic 4 - Per-tenant-per-domain topics | Covered |
| FR20 | The system can continue persisting events when the pub/sub system is temporarily unavailable, draining the backlog on recovery | Epic 4 - Resilient persistence during pub/sub outage | Covered |
| FR67 | The system can apply backpressure when aggregate command queues exceed a configurable depth threshold (default: 100 pending commands per aggregate), returning HTTP 429 with Retry-After header to prevent saga storms and head-of-line blocking cascades. Backpressure is per-aggregate, not system-wide | Epic 4 - Per-aggregate backpressure (HTTP 429) | Covered |
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

No PRD functional requirements are missing from the epics FR coverage map.

### Extra Requirements

No FR IDs appear in the epics coverage map that are absent from the PRD.

### Coverage Statistics

- Total PRD FRs: 104
- FRs covered in epics: 104
- Coverage percentage: 100%
## UX Alignment Assessment

### UX Document Status

Found: _bmad-output/planning-artifacts/ux-design-specification.md (145,648 bytes, modified 2026-05-18 08:35:38).

No sharded UX folder was found.

### PRD Alignment

- The UX document matches the PRD's multi-surface product model: developer SDK, REST API consumer experience, CLI/Aspire operator experience, Blazor admin dashboard, admin CLI, and MCP server.
- UX personas align with PRD journeys for Marco, Priya, Alex, Sanjay, Maria, and Claude, including the later admin-tooling requirements FR68-FR82.
- API error journeys align with the PRD's public gateway and ProblemDetails requirements, especially FR83-FR95 and NFR security/authorization expectations.
- Query and SignalR UI expectations align with PRD query/projection caching requirements FR50-FR64 and NFR35-NFR39.
- Fluent UI versioning is now aligned: PRD, UX, architecture, and epics all point future admin UI work at Fluent UI v5 / the Epic 21 migration baseline.
- Consumer-facing API error terminology is now aligned: UX Rule E6 permits stable API field names such as `aggregateId`ggregateId only as field paths or extension keys, while ProblemDetails prose avoids internal implementation terms.

### Architecture Alignment

- Architecture supports UX-DR41 through UX-DR59 through ADR-P4 for the three-interface admin architecture and ADR-P5 for observability deep links, with detailed interactions delegated to Epics 15, 17, 18, and 20.
- Architecture support exists for REST/API UX expectations through D5 ProblemDetails, D8 rate limiting, the command lifecycle data flow, OpenTelemetry tracing, and health/readiness endpoints.
- Blazor dashboard architecture is aligned at the platform level: Blazor Server, Fluent UI 5.x, real-time SignalR, external observability deep links, and Admin API/CLI/MCP split.
- Admin performance and access requirements map to NFR40-NFR46 and architecture's Administration Tooling NFR coverage.

### Alignment Issues

1. Residual envelope metadata count drift remains in secondary UX and architecture wording.
   - PRD FR11 defines a 14-field metadata envelope and two-document storage.
   - Architecture SEC-1 now correctly says EventStore owns all 14 fields, but architecture line references still say "11-field metadata schema" in the cross-cutting concern table and EventEnvelope.cs tree comment.
   - UX still lists "Event envelope with 11 metadata fields" in the innovation pattern table.
   - Impact: implementers reading secondary tables may use stale envelope assumptions around metadata ownership and compatibility.
   - Recommendation: update the residual UX and architecture references to the current 14-field envelope, including metadataVersion and two-document storage language.

### Warnings

- No missing UX documentation warning: UX is present and detailed.
- No Fluent UI version warning remains: the PRD has been updated to Fluent UI v5.
- No blocking architecture gap found for v2 admin UX surfaces, but detailed admin interactions remain story-level acceptance criteria and must stay visible during story review.
## Epic Quality Review

### Review Scope

Validated _bmad-output/planning-artifacts/epics.md against implementation-readiness standards:

- Epics should deliver recognizable user value rather than only technical milestones.
- Epics should be independently valuable in sequence, with no forward dependencies.
- Stories should be independently completable, testable, and appropriately sized.
- Acceptance criteria should be specific, verifiable, and preferably Given/When/Then.

### Critical Violations

None found that block traceability outright. The FR coverage map is complete and no explicit forward dependency was found where an earlier epic requires a later epic to function.

### Major Issues

1. Several epics still use technical-layer titles, even where outcome text now improves the framing.
   - Examples: Epic 1 Domain Contract Foundation, Epic 2 Event Persistence & Aggregate Processing, Epic 4 Event Distribution & Pub/Sub, Epic 7 Snapshots, Rate Limiting & Performance, Epic 8 Aspire Orchestration, Sample App & Testing.
   - Impact: the improved outcome statements help, but the title/grouping can still bias implementation toward subsystem delivery instead of user-visible slices.
   - Recommendation: preserve the FR mapping, but keep or add explicit user-value completion statements in every epic list and detailed epic section.

2. Epic 7 combines snapshots, per-tenant rate limiting, and per-consumer rate limiting.
   - Impact: snapshots affect state rehydration and correctness, while rate limiting is gateway abuse/backpressure behavior. The epic is cohesive only under a broad performance/resilience umbrella.
   - Recommendation: keep the stories independent and require separate validation evidence for snapshot correctness, tenant throttling, and consumer throttling.

3. Epic 8 is broad across Aspire orchestration, sample domain service, client package registration, hot reload, testing, deployment manifests, and CI/CD.
   - Impact: it crosses onboarding, runtime topology, package developer experience, test strategy, deployment, and release engineering. This is hard to treat as one independently shippable user outcome.
   - Recommendation: use the existing story boundaries as the implementation units; do not assign Epic 8 as a single body of work.

4. Completed admin epics 14-21 rely on linked implementation artifacts for full acceptance evidence.
   - Examples: Epics 14-21 list completed stories with outcome, key acceptance, and Detail: links rather than full Given/When/Then acceptance criteria inline.
   - Impact: the epics document alone is not sufficient to revalidate these areas before follow-up work.
   - Recommendation: treat the linked implementation artifacts as mandatory review inputs for future admin, CLI, MCP, and Fluent UI v5 work.

5. Some historical stories remain too broad by current standards, though split maps now mitigate the risk.
   - Examples: Story 3.5 covers auth failure, authorization failure, concurrency conflict, infrastructure unavailability, and correlation ID serialization; Story 8.5 covers the full three-tier test pyramid; Story 8.6 covers deployment manifests and environment portability.
   - Impact: broad historical stories are harder to test and review independently.
   - Recommendation: keep the included split maps binding for future remediation or regression work.

### Minor Concerns

1. Some detailed epic sections express the outcome as a prose paragraph rather than the consistent **Outcome:** marker used elsewhere.
   - Examples: Epics 3, 8, 9, 10, 12, 13, and 22.
   - Recommendation: normalize these to explicit **Outcome:** lines for easier automated review.

2. Epic 22 contains correctly marked parent container stories, but the parent containers still appear in the story flow.
   - Recommendation: keep the Container rule: do not assign language and ensure sprint planning tools cannot select Story 22.1 or Story 22.5 directly.

3. Story numbering includes patch-style suffixes such as 15.12a, 15.12b, 21.9.5, and 21.9.5.7.
   - Impact: acceptable for historical remediation, but it complicates ordering and tooling.
   - Recommendation: future addenda should use sequential story numbers or a clearly documented patch-story convention.

4. Epic 11 remains supplemental scope for query/projection behavior without explicit PRD FR ownership.
   - Recommendation: keep the current coverage note; add PRD requirements before opening new server-managed projection-builder implementation stories.

### Best Practices Compliance Checklist

| Area | Result | Notes |
| ---- | ------ | ----- |
| FR traceability | Pass | 104/104 PRD FRs covered in the epics coverage map. |
| No explicit forward dependencies | Pass | Dependencies are backward or historical; no earlier epic explicitly requires a later epic. |
| User-value framing | Partial | Outcome text exists for many epics, but several titles and umbrella groupings remain technical. |
| Story sizing | Partial | Many stories are appropriately sized; broad historical stories rely on split maps for future work. |
| Acceptance criteria quality | Partial | Epics 1-13 and 22 mostly include Given/When/Then; Epics 14-21 require linked artifacts for full evidence. |
| Dependency clarity | Pass | Admin and gateway dependencies are explicit and backward-pointing. |
| Starter/project setup coverage | Pass | Epic 8 covers Aspire AppHost, sample, topology, testing, deployment, and CI/CD. |
| Greenfield/brownfield fit | Pass | Planning recognizes greenfield platform work plus integration with existing Hexalith, DAPR, and Aspire ecosystem. |

### Readiness Implication

Epic-level FR coverage is ready, but story-level implementation readiness remains uneven. The safest implementation path is to assign work from fully specified child stories, honor container-story split rules, and require linked implementation artifacts as evidence before admin or Fluent UI follow-up work begins.
## Summary and Recommendations

### Overall Readiness Status

NEEDS WORK

The planning set is close to implementation-ready: all required source documents exist, the PRD exposes 104 functional requirements and 46 non-functional requirements, and the epics coverage map accounts for 104/104 PRD FRs. It should not be treated as fully ready yet because one source-of-truth drift remains around the event envelope, and story-level readiness still depends on disciplined use of split stories and linked acceptance evidence.

### Critical Issues Requiring Immediate Action

No critical traceability blocker was found.

The highest-priority issues to resolve before assigning new implementation work are:

1. Residual envelope metadata count drift.
   - PRD FR11 and architecture SEC-1 define a 14-field metadata envelope with two-document storage.
   - UX and architecture still contain secondary references to an 11-field envelope.
   - This is the most important cleanup because envelope ownership is a security, compatibility, and persistence boundary.

2. Evidence dependency for completed admin and Fluent UI work.
   - Epics 14-21 summarize completed stories and link implementation artifacts.
   - Future work in those areas must load the linked artifacts before implementation or review.

3. Broad umbrella epics and historical stories must not be assigned as single implementation units.
   - Epic 7, Epic 8, Story 3.5, Story 8.5, and Story 8.6 are broad.
   - Epic 22 parent stories 22.1 and 22.5 are correctly marked as containers and must remain unassignable.

### Recommended Next Steps

1. Update the remaining 11-field envelope references in `ux-design-specification.md` and `architecture.md` to the current 14-field envelope language.

2. Normalize detailed epic sections that use prose outcome paragraphs into explicit `Outcome:` lines for Epics 3, 8, 9, 10, 12, 13, and 22.

3. Keep the Epic 7 and Epic 8 story boundaries as mandatory assignment boundaries; require separate validation evidence for snapshots, throttling, topology, testing, deployment, and release automation.

4. Configure sprint planning/review practice so container stories 22.1 and 22.5 cannot be selected directly.

5. For any work touching Epics 14-21, load the linked implementation artifact listed in `Detail:` before estimating, implementing, or reviewing.

6. Preserve the complete FR coverage map as the traceability baseline; validate future PRD changes by ID, not by numeric order.

### Issue Count

- Document discovery issues: 0 critical, 0 missing required documents, 1 excluded-report selection note
- PRD extraction issues: 0 blockers, 1 numbering/traceability caution
- Epic coverage issues: 0 missing FRs, 0 extra FRs
- UX alignment issues: 1
- Epic quality issues: 5 major, 4 minor

Total issues requiring attention: 11

### Final Note

This assessment found 11 issues across document hygiene, PRD traceability practice, UX/architecture alignment, and epic/story quality. The strongest part of the planning set is requirement coverage: 104/104 PRD FRs are mapped. The weakest part is implementation discipline around older umbrella work and linked acceptance evidence. Fix the residual envelope wording and enforce child-story assignment rules before treating the plan as fully implementation-ready.

Assessment date: 2026-05-18

Assessor: Codex using bmad-check-implementation-readiness