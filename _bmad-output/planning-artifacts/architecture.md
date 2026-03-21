---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
lastStep: 8
status: 'complete'
completedAt: '2026-02-12'
amendedAt: '2026-03-15'
amendments:
  - D11: E2E Security Testing Infrastructure (Keycloak in Aspire)
  - D12: ULID Everywhere -- Hexalith.Commons.UniqueIds (sprint-change-proposal-2026-03-15)
inputDocuments:
  - product-brief-Hexalith.EventStore-2026-02-11.md
  - prd.md
  - prd-validation-report.md
  - market-event-sourcing-event-store-solutions-research-2026-02-11.md
  - technical-aspnet-core-command-api-authorization-research-2026-02-11.md
  - technical-blazor-fluent-ui-v4-research-2026-02-11.md
  - technical-dapr-workflow-pubsub-actors-research-2026-02-11.md
  - technical-dotnet-10-aspire-13-research-2026-02-11.md
workflowType: 'architecture'
project_name: 'Hexalith.EventStore'
user_name: 'Jerome'
date: '2026-02-12'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements: 47 FRs across 8 categories**

| Category | FRs | Architectural Significance |
|----------|-----|---------------------------|
| Command Processing | FR1-FR8 | REST API gateway, command routing, dead-letter handling, idempotency |
| Event Management | FR9-FR16 | Append-only storage, sequence ordering, envelope schema, atomic writes, snapshots |
| Event Distribution | FR17-FR20 | Pub/sub with CloudEvents 1.0, at-least-once delivery, per-tenant-per-domain topics |
| Domain Service Integration | FR21-FR25 | Pure function contract, DAPR config-based registration, multi-domain/multi-tenant |
| Identity & Multi-Tenancy | FR26-FR29 | Canonical `tenant:domain:aggregate-id` tuple, triple-layer isolation |
| Security & Authorization | FR30-FR34 | Six-layer defense in depth (JWT -> Claims -> Endpoint -> MediatR -> Actor -> DAPR) |
| Observability & Operations | FR35-FR39 | OpenTelemetry traces, structured logs, health/readiness endpoints |
| Developer Experience & Deployment | FR40-FR47 | Aspire orchestration, sample domain service, NuGet packages, three-tier testing |

**Non-Functional Requirements: 32 NFRs across 5 categories**

| Category | NFRs | Key Targets |
|----------|------|-------------|
| Performance | NFR1-NFR8 | Command submission <50ms p99, end-to-end lifecycle <200ms p99, event append <10ms p99, 100 cmd/sec/instance |
| Security | NFR9-NFR15 | TLS 1.2+, JWT validation every request, no payload in logs, triple-layer tenant isolation, DAPR access control |
| Scalability | NFR16-NFR20 | Horizontal scaling via actor placement, 10K+ active aggregates, 10+ tenants, dynamic config |
| Reliability | NFR21-NFR26 | 99.9%+ availability, zero data loss, deterministic replay, checkpointed state machine recovery |
| Integration | NFR27-NFR32 | Backend-agnostic (DAPR state store/pub/sub), OTLP-compatible telemetry, Aspire publisher deployment |

### Fundamental Tension: Infrastructure Portability vs. Feature Parity

The PRD promises "zero code changes" across DAPR backends (Redis dev, PostgreSQL prod, Cosmos DB scale). However, DAPR state store capabilities vary significantly:

| Capability | Redis | PostgreSQL | Cosmos DB |
|-----------|-------|-----------|-----------|
| Multi-key transactions | No | Yes | Yes (within partition) |
| ETag optimistic concurrency | Yes | Yes | Yes |
| Range queries | Limited | Yes | Yes |
| Atomic batch writes | No | Yes | Depends on partition |

**Architecture Resolution:** "Zero code changes" means portable **application code**, not identical **backend behavior**. The architecture must:
- Define a minimum capability contract that all supported backends satisfy
- Document a Backend Compatibility Matrix with feature-level granularity per state store
- **Hard backend requirement:** ETag-based optimistic concurrency support is non-negotiable. D1 relies on ETag concurrency on the aggregate metadata key. All mainstream DAPR state stores (Redis, PostgreSQL, Cosmos DB) support ETags
- Design around the lowest common denominator for core operations (single-key ETag concurrency)
- Allow backend-specific optimizations where available (e.g., multi-key transactions on PostgreSQL)
- Actor-level ACID writes via `IActorStateManager.SaveStateAsync` resolve FR16 universally (GAP-3 eliminated by D1)

### Specification Gaps Requiring Architectural Resolution

Gaps identified through Advanced Elicitation (Pre-mortem, Failure Mode Analysis, Red Team, Self-Consistency) and Party Mode collaborative analysis, prioritized by architectural impact:

**Architecture-Blocking (must resolve before design decisions):**

| # | Gap | Source | Impact |
|---|-----|--------|--------|
| GAP-1 | **Command status storage mechanism undefined.** FR5 defines `GET /commands/{correlationId}/status` but Data Schemas has no key pattern for command status records. | SC-3, Failure Mode | Affects state store key strategy, actor state design, and API query model |
| GAP-2 | **Domain service error/rejection contract missing.** FR21 defines happy path `(Command, CurrentState?) -> List<DomainEvent>` but not how services signal command rejection (exception? empty list? rejection event?). | SRT-1, CR-1 | Affects actor processing logic, error propagation, and dead-letter content |
| GAP-3 | **Atomic event writes on non-transactional backends.** FR16 requires 0-or-N atomic writes, but Redis lacks multi-key transactions. No compensating pattern defined. | PM-3, Failure Mode | Affects fundamental storage strategy and backend compatibility promise |

**Design-Phase (resolve during component design):**

| # | Gap | Source | Impact |
|---|-----|--------|--------|
| GAP-4 | Pub/sub topic naming convention undefined. FR19 says "per-tenant-per-domain topics" but no naming pattern specified. | SC-4 | Affects event distribution topology |
| GAP-5 | Structured log minimum fields undefined beyond correlation/causation IDs. | PM-4 | Affects operational observability contract |
| GAP-6 | Extension metadata bag interaction with serialization and CloudEvents mapping undefined. | PM-1 | Affects envelope extensibility design |
| GAP-7 | Correlation ID scoping -- FR5 status endpoint doesn't explicitly state tenant-scoped queries. | RT-3 | Affects API security model |
| GAP-8 | Snapshot production responsibility unclear -- who triggers, who produces content. | BC-3 | Affects actor-domain service interaction protocol |
| GAP-9 | API rate limiting per tenant/consumer not specified despite saga storm scenario in Journey 2. | SRT-8, CR-3 | Affects API gateway design |

**Implementation-Phase (resolve during implementation):**

| # | Gap | Source | Impact |
|---|-----|--------|--------|
| GAP-10 | Clone-to-running prerequisite assumptions (Docker, .NET 10 SDK, DAPR CLI, Aspire) not documented. | PM-2 | Affects onboarding documentation |
| GAP-11 | Extension metadata validation rules (max size, allowed characters, sanitization) undefined. | RT-4 | Affects input validation |
| GAP-12 | Command schema versioning strategy not specified for in-flight commands during domain service updates. | SRT-2 | Affects deployment strategy |
| GAP-13 | Aggregate state inspection mechanism undefined for v1 (no Blazor dashboard). | SRT-4, CR-4 | Affects operational tooling |
| GAP-14 | Backup, restore, and disaster recovery for event data not specified. | SRT-7, CR-2 | Affects operational readiness |
| GAP-15 | DAPR version compatibility testing matrix not defined. | SRT-5 | Affects CI/CD pipeline |

### Security-Critical Architectural Constraints

Identified through Red Team analysis -- these are non-negotiable constraints that shape every component:

| # | Constraint | Rationale |
|---|-----------|-----------|
| SEC-1 | **EventStore owns all 11 envelope metadata fields.** Domain services return event payloads only; EventStore populates aggregateId, tenantId, domain, sequenceNumber, timestamp, correlationId, causationId, userId, domainServiceVersion, eventTypeName, serializationFormat. Prevents event stream poisoning via malicious domain services. | RT-2 |
| SEC-2 | **Tenant validation occurs BEFORE state rehydration.** During actor activation or rebalancing, command tenant must be validated against actor identity before any state is loaded. Prevents tenant escape during actor rebalancing. | RT-1 |
| SEC-3 | **Command status queries are tenant-scoped.** FR5 status endpoint must filter by authenticated tenant, not just correlation ID. Prevents cross-tenant information leakage via correlation ID collision. | RT-3 |
| SEC-4 | **Extension metadata is sanitized at the API gateway.** Max size limits, character validation, and injection prevention applied before extensions enter the processing pipeline. | RT-4 |
| SEC-5 | **Event payload data never appears in logs.** Only envelope metadata fields may be logged. Enforced at the structured logging framework level, not by convention. | NFR12 |

### Provisional Architecture Decisions

Three preliminary decisions emerging from the analysis. These are **provisional** -- they will be formally evaluated and ratified (or overturned) in subsequent architecture decision steps.

**ADR-P1: Event Storage Strategy -- Single-Key-Per-Event with Composite Keys**

- **Context:** DAPR state store is the persistence layer. Event streams need append-only semantics with optimistic concurrency.
- **Provisional Decision:** Use `{tenant}:{domain}:{aggregateId}:events:{sequence}` as the key pattern (one key per event), with ETag-based concurrency on the aggregate metadata key.
- **Rationale:** Single-key operations are universally supported across all DAPR state stores. Multi-key transactions are only available on some backends.
- **Open Question:** How to achieve atomic multi-event writes (FR16) on non-transactional backends. Options: (a) single-key batch encoding, (b) saga-style compensation, (c) document backend limitations in compatibility matrix.
- **Backend-Specific Consideration:** PostgreSQL supports multi-key transactions and could use a more efficient batch write strategy. Architecture should allow backend-specific optimizations while maintaining a universal baseline.

**ADR-P2: Persist-Then-Publish Event Flow**

- **Context:** Events must be persisted to the state store before being published to pub/sub (FR20 requires continued persistence during pub/sub outages).
- **Provisional Decision:** Actor persists events to state store first, then publishes to pub/sub. Checkpointed state machine tracks which stage completed for crash recovery (NFR25).
- **Rationale:** Prevents event loss if pub/sub is unavailable. State store is the source of truth; pub/sub is a distribution mechanism.
- **Open Question:** How to handle the case where persistence succeeds but publish fails repeatedly. DAPR retry policies handle transient failures, but what about permanent pub/sub failure?

**ADR-P3: Domain Service Contract -- Result Object Over Exceptions**

- **Context:** FR21 defines `(Command, CurrentState?) -> List<DomainEvent>` but the error/rejection path is undefined (GAP-2).
- **Provisional Decision:** Domain services return a result object that contains either a list of events (success) or an error descriptor (rejection). Empty event lists are valid (command acknowledged, no state change). Exceptions indicate infrastructure failures, not domain rejections.
- **Rationale:** Distinguishes between domain rejections (business rule violations -- expected, logged, returned to caller) and infrastructure failures (network errors, timeouts -- retried via DAPR resiliency). This distinction affects dead-letter routing and retry behavior.

### Technical Constraints & Dependencies

**Runtime Dependencies:**

| Dependency | Version | Constraint Type |
|-----------|---------|----------------|
| .NET | 10 LTS (November 2025 GA) | Target framework `net10.0` |
| C# | 14 | Language features |
| DAPR Runtime | 1.14+ | Sidecar model, actors, state store, pub/sub, config store, resiliency |
| Aspire | 13 | Orchestration, service defaults, publishers |
| DAPR SDK for .NET | Compatible with DAPR 1.14+ | Actor client, state management, pub/sub client |

**DAPR Building Block Dependencies (v1):**

| Building Block | Purpose | Constraint |
|---------------|---------|------------|
| Actors | 1:1 aggregate processing | Single-threaded turn-based concurrency; 60-min idle timeout; consistent hashing placement |
| State Store | Event persistence + actor state + snapshots | Must support key-value + ETag concurrency; transaction support varies by backend |
| Pub/Sub | Event distribution | CloudEvents 1.0; at-least-once delivery; topic-per-tenant-per-domain |
| Configuration | Domain service registration | Tenant + domain + version -> service endpoint mapping |
| Resiliency | Retry, circuit breaker, timeout | Applied at sidecar level; no custom retry logic in application code |

**Infrastructure Constraints:**

- DAPR sidecar adds ~1-2ms per building block call (NFR8 ceiling: 2ms p99)
- Actor activation requires state rehydration -- snapshot strategy critical for aggregates with 100+ events
- Pub/sub delivery is at-least-once; all subscribers must be idempotent
- State store transaction support varies by backend -- architecture must handle both transactional and non-transactional stores

**NuGet Package Architecture (6 packages):**

> **Note:** This section was corrected to reflect 6 shipped packages (SignalR added post-initial architecture).

| Package | Purpose | Consumers |
|---------|---------|-----------|
| `Hexalith.EventStore.Contracts` | Event envelope, command/event types, identity scheme | Domain service developers |
| `Hexalith.EventStore.Client` | Domain service SDK with convention-based fluent API (`AddEventStore`/`UseEventStore`), auto-discovery, and explicit `IDomainProcessor` registration | Domain service developers |
| `Hexalith.EventStore.Server` | Core EventStore server with actor processing pipeline | Platform operators |
| `Hexalith.EventStore.SignalR` | SignalR client helper for real-time projection change notifications | UI/integration clients |
| `Hexalith.EventStore.Aspire` | Aspire AppHost integration and service defaults | Both |
| `Hexalith.EventStore.Testing` | Test helpers, in-memory DAPR mocks, assertion utilities | Domain service developers |

### Cross-Cutting Concerns

| # | Concern | Affected Components | Architectural Impact |
|---|---------|-------------------|---------------------|
| 1 | **Multi-Tenant Isolation** | Every component -- API gateway, actor identity, state store keys, pub/sub topics, DAPR policies | Triple-layer enforcement required at every boundary; `tenant:domain:aggregate-id` identity permeates all addressing |
| 2 | **Observability** | All pipeline stages | OpenTelemetry traces must span full command lifecycle; structured logs with correlation/causation IDs at every stage; metrics for latency, throughput, error rates |
| 3 | **Security (Six-Layer Auth)** | API gateway, MediatR pipeline, actor processing, DAPR policies | Authorization decisions at 6 distinct points; JWT claims flow through entire pipeline; DAPR access control policies restrict service-to-service communication |
| 4 | **Error Propagation** | Domain services -> actors -> API -> dead-letter | Domain rejections vs. infrastructure failures must be distinguished; dead-letter topic must carry full context; correlation IDs must be traceable across all failure paths |
| 5 | **Infrastructure Portability** | State store, pub/sub, config store | All persistence and messaging through DAPR abstractions; no direct backend access; backend-specific behavior documented in compatibility matrix |
| 6 | **Event Envelope Consistency** | Persistence, distribution, domain service contract, client SDK | 11-field metadata schema is irreversible; EventStore owns all metadata population; extension bag for unforeseen needs |
| 7 | **Crash Recovery** | Actor processing, state machine, pub/sub delivery | Checkpointed state machine must resume from correct stage; no duplicate event persistence; events eventually published after pub/sub recovery |
| 8 | **Configuration-Driven Behavior** | Domain service registration, snapshot intervals, tenant routing | DAPR config store for runtime configuration; dynamic updates without system restart (NFR20) |
| 9 | **Versioning** | Event envelope (MAJOR), domain service contract (MAJOR), API (v1 path prefix), NuGet packages (SemVer, monorepo single version) | Envelope changes are irreversible; all packages versioned together; API versioned in URL path |
| 10 | **Testability** | All components across three tiers | Unit tests: pure functions, no DAPR dependency; Integration tests: DAPR test containers; Contract tests: full Aspire topology. Architecture must support clean test boundaries at each tier. |

### Scale & Complexity Assessment

- **Primary domain:** Event Sourcing Infrastructure (distributed systems)
- **Complexity level:** High (Technical) -- distributed actor state management, exactly-once semantics, multi-tenant isolation, irreversible schema decisions, DAPR runtime dependency
- **Project type:** Event Sourcing Server Platform (primary) + Developer Tooling (secondary)
- **Estimated architectural components:** ~12-15 (API gateway, command router, actor host, domain service invoker, event persister, event publisher, snapshot manager, identity resolver, auth pipeline, config manager, health/readiness probes, telemetry pipeline, dead-letter handler, NuGet client SDK, Aspire orchestrator)
- **v1/v2 phasing:** v1 actors with workflow-ready state machine; v2 DAPR Workflows + Blazor dashboard
- **Resource model:** Solo developer (Jerome) -- v1 scope deliberately sized for single experienced .NET developer

## Starter Template Evaluation

### Primary Technology Domain

**Server Platform + NuGet Library (backend infrastructure)** -- a distributed systems server with a client SDK. The starter establishes the multi-project solution structure for 5 NuGet packages + Aspire orchestration + DAPR actor hosting.

### Verified Current Versions (February 2026)

| Technology | Version | Notes |
|-----------|---------|-------|
| .NET SDK | 10.0.103 | LTS, supported until November 2028 |
| C# | 14 | Ships with .NET 10 |
| DAPR Runtime | 1.16.6 | Latest stable (updated from PRD's 1.14+ minimum) |
| DAPR .NET SDK | Dapr.Client 1.16.1, Dapr.AspNetCore 1.16.1 | Requires .NET 8+ |
| Aspire | 13.1.2 | Polyglot platform, requires .NET 10 SDK |
| CommunityToolkit.Aspire.Hosting.Dapr | 13.0.0 | Aspire + DAPR integration (replaces deprecated `Aspire.Hosting.Dapr`) |
| Blazor Fluent UI | 4.13.2 | v5 in development; v4 supported until Nov 2026 (v2 reference) |

### Starter Options Considered

| Option | Approach | Fit | Verdict |
|--------|---------|-----|---------|
| `dotnet new aspire-starter` | AppHost + ServiceDefaults + ApiService + Blazor Web | Poor -- includes Blazor (v2), 2-service topology doesn't match 6-package architecture | Reject |
| Custom solution from individual templates | Precise match to 6-package NuGet architecture + AppHost + ServiceDefaults + sample | Excellent -- each project uses correct template, no dead code | **Selected** |
| DAPR quickstart/sample as base | Single actor service with basic wiring | Poor -- educational, no Aspire integration, no NuGet structure | Reject |

### Selected Starter: Custom Solution from Individual Templates

**Rationale:** Hexalith.EventStore's 6-package NuGet architecture, DAPR actor interface/implementation separation, and Aspire orchestration don't match any existing starter. Building from individual `dotnet new` templates provides precise control while leveraging .NET 10 / Aspire 13.1 scaffolding.

**Initialization Command:**

```bash
# Create solution
dotnet new sln -n Hexalith.EventStore -o Hexalith.EventStore

# NuGet Package Projects (class libraries)
dotnet new classlib -n Hexalith.EventStore.Contracts -o src/Hexalith.EventStore.Contracts -f net10.0
dotnet new classlib -n Hexalith.EventStore.Client -o src/Hexalith.EventStore.Client -f net10.0
dotnet new classlib -n Hexalith.EventStore.Server -o src/Hexalith.EventStore.Server -f net10.0
dotnet new classlib -n Hexalith.EventStore.Testing -o src/Hexalith.EventStore.Testing -f net10.0

# Aspire Projects
dotnet new aspire-servicedefaults -n Hexalith.EventStore.ServiceDefaults -o src/Hexalith.EventStore.ServiceDefaults
dotnet new aspire-apphost -n Hexalith.EventStore.AppHost -o src/Hexalith.EventStore.AppHost

# Aspire Integration Package (class library)
dotnet new classlib -n Hexalith.EventStore.Aspire -o src/Hexalith.EventStore.Aspire -f net10.0

# Command API Host (ASP.NET Core -- hosts actors + REST endpoints)
dotnet new webapi -n Hexalith.EventStore.CommandApi -o src/Hexalith.EventStore.CommandApi -f net10.0

# Sample Domain Service (reference implementation)
dotnet new webapi -n Hexalith.EventStore.Sample -o samples/Hexalith.EventStore.Sample -f net10.0

# Test Projects
dotnet new xunit -n Hexalith.EventStore.Contracts.Tests -o tests/Hexalith.EventStore.Contracts.Tests -f net10.0
dotnet new xunit -n Hexalith.EventStore.Server.Tests -o tests/Hexalith.EventStore.Server.Tests -f net10.0
dotnet new xunit -n Hexalith.EventStore.IntegrationTests -o tests/Hexalith.EventStore.IntegrationTests -f net10.0
```

**Architectural Decisions Provided by Starter:**

**Project Structure:**

```
Hexalith.EventStore/
├── src/
│   ├── Hexalith.EventStore.Contracts/       # Event envelope, command/event types, identity scheme
│   ├── Hexalith.EventStore.Client/          # Domain service SDK for registration and integration
│   ├── Hexalith.EventStore.Server/          # Core actor processing pipeline
│   ├── Hexalith.EventStore.Aspire/          # Aspire AppHost integration and service defaults
│   ├── Hexalith.EventStore.Testing/         # Test helpers, in-memory DAPR mocks, assertions
│   ├── Hexalith.EventStore.CommandApi/      # ASP.NET Core host (DAPR actors + REST API)
│   ├── Hexalith.EventStore.ServiceDefaults/ # Shared resilience, telemetry, health checks
│   └── Hexalith.EventStore.AppHost/         # Aspire orchestration (full local topology)
├── samples/
│   └── Hexalith.EventStore.Sample/          # Reference domain service implementation
├── tests/
│   ├── Hexalith.EventStore.Contracts.Tests/ # Unit tests (pure functions, no DAPR)
│   ├── Hexalith.EventStore.Server.Tests/    # Integration tests (DAPR test containers)
│   └── Hexalith.EventStore.IntegrationTests/# Contract tests (full Aspire topology)
└── Hexalith.EventStore.slnx
```

**Key NuGet Dependencies:**

| Package | Project(s) | Version |
|---------|-----------|---------|
| `Dapr.Actors.AspNetCore` | CommandApi, Server | 1.16.x |
| `Dapr.Client` | Server, Client | 1.16.1 |
| `Dapr.AspNetCore` | CommandApi | 1.16.1 |
| `CommunityToolkit.Aspire.Hosting.Dapr` | AppHost | 13.0.0 |
| `MediatR` | Server, CommandApi | latest |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | CommandApi | 10.0.x |

**Build & Versioning:**
- `dotnet build` / `dotnet pack` for NuGet packages
- MinVer or Nerdbank.GitVersioning for SemVer from Git tags
- Monorepo single-version strategy (all packages share version)

**Testing:** xUnit with three-tier strategy mapped to test projects

**Development Experience:** `dotnet aspire run` via AppHost for full local topology; OpenTelemetry via ServiceDefaults

**Note:** Project initialization using these commands should be the first implementation story.

## Core Architectural Decisions

### Decision Summary

**Decisions Made (D1-D10):**

| # | Decision | Choice | Resolves |
|---|---------|--------|----------|
| D1 | Event Storage Strategy | Single-key-per-event with actor-level ACID writes | ADR-P1, GAP-3 |
| D2 | Command Status Storage | Dedicated state store key `{tenant}:{correlationId}:status` | GAP-1 |
| D3 | Domain Service Error Contract | Domain errors as events; only infrastructure errors are exceptions | ADR-P3, GAP-2 |
| D4 | DAPR Access Control Granularity | Per-app-id allow list | -- |
| D5 | API Error Response Format | RFC 7807 Problem Details + extensions (correlationId, tenantId) | -- |
| D6 | Pub/Sub Topic Naming | `{tenant}.{domain}.events` | GAP-4 |
| D7 | Domain Service Invocation | DAPR service invocation (`DaprClient.InvokeMethodAsync`) | -- |
| D8 | Rate Limiting Strategy | ASP.NET Core built-in `RateLimiting` middleware, per-tenant sliding window | GAP-9 |
| D9 | NuGet Package Versioning | MinVer (version from Git tags, zero config) | -- |
| D10 | CI/CD Pipeline | GitHub Actions | -- |
| D11 | E2E Security Testing Infrastructure | Keycloak in Aspire with realm-as-code | Story 5.1 Task 7, AC #5/#6, runtime security verification for all Epic 5 stories |
| D12 | ULID Everywhere | `Hexalith.Commons.UniqueIds` for all ID generation | Story 1.7, D13 message type prefix extraction |

**Decisions Already Made (PRD + Starter Template):**

| Decision | Choice | Source |
|---------|--------|--------|
| Runtime | .NET 10 LTS, C# 14, DAPR 1.16+, Aspire 13.1 | PRD + version verification |
| API Style | REST with `/api/v1/` path prefix | PRD |
| Auth Model | JWT with six-layer defense in depth | PRD |
| Package Structure | 5 NuGet packages + CommandApi host + Aspire orchestration | PRD + Starter |
| Testing | xUnit, three-tier (unit/integration/contract) | PRD + Starter |
| Observability | OpenTelemetry (traces, metrics, structured logs) | PRD |
| Hosting | Aspire publishers (Docker Compose, Kubernetes, Azure Container Apps) | PRD |
| Frontend | None in v1; Blazor Fluent UI 4.x in v2 | PRD |

**Decisions Deferred (Post-v1):**

| Decision | Deferred To | Rationale |
|---------|------------|-----------|
| Blazor dashboard architecture | v2 | No UI in v1 |
| DAPR Workflow migration strategy | v2 | v1 actors with workflow-ready state machine |
| External authorization engine (OpenFGA/OPA) | v2 | JWT six-layer sufficient for v1 |
| Event versioning/migration tooling | v3 | Requires stable envelope first |
| GDPR crypto-shredding | v3 | Per-tenant encryption key management |
| gRPC Command API | v4 | REST-only in v1 |

### Data Architecture

**D1: Event Storage Strategy -- Single-Key-Per-Event with Actor-Level ACID**

- **Pattern:** Each event stored as `{tenant}:{domain}:{aggId}:events:{seq}`, aggregate metadata at `{tenant}:{domain}:{aggId}:metadata`
- **Atomicity:** DAPR actor `IActorStateManager` batches all state changes within a single actor turn. `SetStateAsync` for each event key + metadata update, then `SaveStateAsync` commits atomically
- **Concurrency:** ETag-based optimistic concurrency on aggregate metadata key
- **Backend universality:** Actor state manager handles transactional batching regardless of underlying state store capabilities. Eliminates the non-transactional backend concern (GAP-3 resolved)
- **Rationale:** Simplest approach that provides universal backend support. Actor-level ACID is a DAPR runtime guarantee, not a state store feature

**D2: Command Status Storage -- Dedicated State Store Key**

- **Pattern:** `{tenant}:{correlationId}:status` stored in DAPR state store
- **Lifecycle updates:** Checkpointed state machine: `Received → Processing → EventsStored → EventsPublished → Completed | Rejected | PublishFailed | TimedOut`
- **Received status location:** Written at API layer (`SubmitCommandHandler`) before actor invocation, ensuring status is queryable even if actor activation fails. All subsequent status transitions occur inside the actor
- **Query model:** `GET /api/v1/commands/{correlationId}/status` reads directly from state store -- no actor activation required
- **Tenant scoping:** Key includes tenant; API enforces JWT tenant matches query tenant (SEC-3 enforced)
- **Status content:** Includes current stage, timestamp, aggregate ID, and for terminal states: event count (Completed), rejection event type name (Rejected), failure reason (PublishFailed), or timeout duration (TimedOut)
- **TTL:** Default 24-hour TTL on status entries via DAPR state store `ttlInSeconds` metadata, configurable per-tenant via DAPR config store. Prevents unbounded state store growth from ephemeral status records
- **Terminal statuses:** `Completed` (all events stored and published), `Rejected` (domain rejection event persisted), `PublishFailed` (events stored but pub/sub permanently unavailable after DAPR retry exhaustion), `TimedOut` (command exceeded configured processing timeout)
- **Rationale:** Decouples status queries from actor lifecycle. Status remains queryable even after actor deactivation

**D3: Domain Service Error Contract -- Errors as Events**

- **Contract:** `(Command, CurrentState?) -> List<DomainEvent>` -- always returns events, never throws for domain logic
- **Domain rejection:** Expressed as rejection event types via naming convention (e.g., `OrderRejected`, `PaymentDeclined`, `InsufficientFundsDetected`) AND `IRejectionEvent` marker interface for programmatic identification
- **Rejection persistence:** Persisted to event stream like any other event -- rejection events increment the aggregate sequence number
- **Backward-compatible deserialization (CRITICAL):** Domain services MUST maintain backward-compatible deserialization for all event types they have ever produced. UnknownEvent during state rehydration is an error condition, not a skip-and-continue path. Skipping unknown events produces incorrect aggregate state. Recovery: redeploy previous domain service version, then add backward-compatible deserializer for removed event type
- **Infrastructure failure:** Exceptions only (network, timeout, domain service unreachable) -- handled by DAPR resiliency policies (retry, circuit breaker, timeout)
- **Dead-letter routing:** Infrastructure failures only, after DAPR retry exhaustion. Domain rejections are normal events, not error paths
- **Rationale:** Consistent with "everything is an event" philosophy. Full audit trail of rejections. Simpler contract -- domain services always return events

### Authentication & Security

**D4: DAPR Access Control -- Per-App-ID Allow List**

- **Policy:** CommandApi app can invoke actor services and domain services. Domain services can invoke nothing directly (they are called, never call out)
- **Enforcement:** DAPR access control policy YAML with `allowedOperations` by `appId`
- **Rationale:** Simplest model that enforces the intended call graph. Fine-grained per-operation policies add YAML maintenance burden without security benefit for v1's straightforward topology

### API & Communication Patterns

**D5: Error Response Format -- RFC 7807 Problem Details + Extensions**

- **Standard:** `application/problem+json` per RFC 7807, using ASP.NET Core's built-in `ProblemDetails`
- **Extensions:** `correlationId` (string), `tenantId` (string), `validationErrors` (array of field-level errors for 400 responses)
- **Rationale:** Industry standard, built into ASP.NET Core, extensible for project-specific context

**D6: Pub/Sub Topic Naming -- Dot-Separated**

- **Pattern:** `{tenant}.{domain}.events` (e.g., `acme-corp.payments.events`)
- **Rationale:** Dot separation is the most common convention across message brokers (RabbitMQ, Kafka, Azure Service Bus). Compatible with DAPR pub/sub topic naming across all backends

**D7: Domain Service Invocation -- DAPR Service Invocation**

- **Mechanism:** Actor calls domain service via `DaprClient.InvokeMethodAsync<TRequest, TResponse>`
- **Service discovery:** Domain service endpoint resolved from DAPR config store registration (`tenant:domain:version -> appId + method`)
- **Security:** mTLS between sidecars (automatic with DAPR)
- **Resiliency:** DAPR resiliency policies (retry with backoff, circuit breaker, timeout) applied at sidecar level
- **Rationale:** Leverages DAPR's built-in service discovery, mTLS, and resiliency. No custom HTTP client management. Domain services are any HTTP endpoint with a DAPR sidecar

**D8: Rate Limiting -- ASP.NET Core Built-In Middleware**

- **Mechanism:** `Microsoft.AspNetCore.RateLimiting` middleware with `SlidingWindowRateLimiter`
- **Scope:** Per-tenant rate limits extracted from JWT `tenant_id` claim
- **Configuration:** Limits configurable per tenant via DAPR config store (dynamic, no restart)
- **Rationale:** Built into ASP.NET Core, no external dependency. Per-tenant scoping prevents one tenant's saga storm from affecting others

### Infrastructure & Deployment

**D9: Package Versioning -- MinVer**

- **Tool:** MinVer (derives SemVer from Git tags)
- **Workflow:** Tag `v1.0.0` on release commit, all packages get `1.0.0`. Pre-release versions auto-calculated from tag + commit height
- **Monorepo strategy:** Single `Directory.Build.props` with MinVer; all packages share version
- **Rationale:** Zero configuration, Git-native workflow, widely adopted in .NET ecosystem

**D10: CI/CD Pipeline -- GitHub Actions**

- **Platform:** GitHub Actions (free for open-source repositories)
- **Pipelines:** Build + test on PR, pack + publish NuGet on release tag, DAPR integration tests with containerized DAPR sidecar
- **Rationale:** Native GitHub integration, excellent .NET and DAPR ecosystem support, free for open-source

**D11: E2E Security Testing Infrastructure -- Keycloak in Aspire**

- **Context:** Story 5.1 has 14 passing static YAML validation tests but lacks runtime verification (Task 7.1/7.2). Current integration tests use symmetric key JWT (HS256) via `TestJwtTokenGenerator`, which bypasses OIDC discovery, JWKS validation, and real IdP token issuance. Multiple security stories (5.1, 5.3, 5.4) require runtime proof of the six-layer auth pipeline with real tokens.
- **Decision:** Add `Aspire.Hosting.Keycloak` to the AppHost with a checked-in realm export (`hexalith-realm.json`) containing pre-configured client, protocol mappers, and test users. E2E tests acquire real OIDC tokens via Resource Owner Password Grant and validate through the full auth pipeline.
- **Key Components:**
  - **Package:** `Aspire.Hosting.Keycloak` in AppHost (hosting only -- no `Aspire.Keycloak.Authentication` client package needed; existing `ConfigureJwtBearerOptions` OIDC discovery path is sufficient)
  - **Port:** `8180` (avoids conflict with `commandapi` on `8080`)
  - **Realm:** `hexalith` with client `hexalith-eventstore`, OIDC protocol mappers for `tenants`, `domains`, `permissions` (JSON array claims matching `EventStoreClaimsTransformation` expectations)
  - **CommandApi wiring:** Environment variable overrides set `Authority` to Keycloak realm URL, triggering existing OIDC discovery at `ConfigureJwtBearerOptions:50-54`. Zero auth code changes
- **Test Users (realm-as-code):**

| User | Tenants | Domains | Permissions | E2E Scenario |
|------|---------|---------|-------------|-------------|
| `admin-user` | tenant-a, tenant-b | orders, inventory | command:submit, command:replay, command:query | Multi-tenant admin |
| `tenant-a-user` | tenant-a | orders | command:submit, command:query | Cross-tenant isolation proof |
| `tenant-b-user` | tenant-b | inventory | command:submit | Lateral isolation proof |
| `readonly-user` | tenant-a | orders | command:query | Permission enforcement |
| `no-tenant-user` | *(none)* | orders | command:submit | Tenant validation rejection |

- **Test placement:** Inside existing `Hexalith.EventStore.IntegrationTests` with `[Trait("Category", "E2E")]` to separate from fast symmetric-key tests
- **Scope:** Shared foundation for all security-requiring stories across Epic 5 and beyond. The realm export is a living artifact -- new users/roles added as stories progress
- **What this proves that symmetric keys cannot:** Real OIDC discovery flow, asymmetric key validation via JWKS, IdP-issued claims structure, issuer URL validation, token expiry management by Keycloak
- **Rationale:** Minimal investment (one package, one realm JSON, environment variable overrides) that unlocks runtime verification for the entire security story backlog. No auth code changes. Existing fast tests remain untouched

**D12: ULID Everywhere -- Hexalith.Commons.UniqueIds**

- **Context:** The system needs lexicographically sortable, distributed-safe unique identifiers for messageId, aggregateId, and correlationId. ULIDs (26-character Crockford Base32) provide timestamp-embedded, monotonically ordered IDs ideal for event sourcing streams.
- **Decision:** Use `Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId()` (NuGet: `Hexalith.Commons.UniqueIds` v2.13.0+) as the single ULID generation mechanism. The Contracts package takes a dependency on `Hexalith.Commons.UniqueIds` -- it does NOT depend directly on a raw ULID library. No custom `UlidId` value object is created; ULID fields are `string`-typed throughout Contracts, consistent with the existing `AggregateIdentity` pattern.
- **Available API surface:**
  - `UniqueIdHelper.GenerateSortableUniqueStringId()` -- 26-char Crockford Base32 ULID (monotonic increment within same millisecond)
  - `UniqueIdHelper.ExtractTimestamp(string)` -- extract creation UTC timestamp from ULID string
  - `UniqueIdHelper.ToGuid(string)` -- convert ULID string to System.Guid (identity-preserving, not sort-preserving)
  - `UniqueIdHelper.ToSortableUniqueId(Guid)` -- convert Guid to ULID string
- **Fields using ULID format:** messageId (all envelopes), aggregateId, correlationId, causationId
- **Rationale:** `Hexalith.Commons.UniqueIds` is an organizational shared library from the Hexalith ecosystem, already published on NuGet with monotonic ULID generation, validation, and Guid conversion. Using it avoids building a redundant custom type and aligns with organizational package reuse.

### Decision Impact Analysis

**Implementation Sequence:**

1. **Contracts package first** -- Event envelope (11 fields + IRejectionEvent), identity scheme, command/event types, rejection event naming convention
2. **Testing package early** -- In-memory DAPR mocks (InMemoryStateManager, FakeDomainServiceInvoker), assertion utilities. Built early so all subsequent packages can be test-driven
3. **Server package** -- Actor processing pipeline (thin orchestrator with 5-step delegation), D1 storage, D3 error contract, D7 invocation, checkpointed state machine
4. **CommandApi host** -- REST endpoints with D5 error format, D8 rate limiting, D2 status storage with TTL, health checks (DAPR sidecar + config store)
5. **Client package** -- Domain service SDK implementing the pure function contract
6. **Sample domain service** -- Reference implementation demonstrating D3 (rejection events), serves as de-facto template for domain service developers
7. **Aspire + AppHost** -- Orchestration with DAPR sidecars, D6 topic naming, local DAPR component configs
8. **Deploy configs** -- Production DAPR component YAMLs (PostgreSQL, RabbitMQ, Kafka, Cosmos DB)
9. **CI/CD** -- GitHub Actions with MinVer tagging (D9, D10), three-tier test execution

**Cross-Component Dependencies:**

- D1 + D2 share the DAPR state store but use independent key namespaces
- D3 flows through D1 (rejection events persisted), D2 (status reflects rejection), D6 (rejection events published to topics)
- D7 + D4 must be consistent -- DAPR access control allows the invocation paths D7 requires
- D8 operates at the API gateway layer, before D1/D2/D3 processing begins
- D11 validates the full auth chain (D4 DAPR access control + six-layer auth) at runtime with real OIDC tokens. Depends on existing `ConfigureJwtBearerOptions` OIDC discovery path -- zero auth code changes

## Implementation Patterns & Consistency Rules

### Naming Patterns

**C# Code Naming (standard .NET conventions):**

| Element | Convention | Example |
|---------|-----------|---------|
| Namespaces | PascalCase, match folder path | `Hexalith.EventStore.Server.Actors` |
| Classes/Records | PascalCase | `AggregateActor`, `EventEnvelope` |
| Interfaces | `I` prefix + PascalCase | `IAggregateActor`, `IDomainServiceInvoker` |
| Methods | PascalCase | `ProcessCommandAsync`, `PersistEventsAsync` |
| Properties | PascalCase | `AggregateId`, `SequenceNumber` |
| Local variables | camelCase | `correlationId`, `eventCount` |
| Private fields | `_camelCase` | `_stateManager`, `_daprClient` |
| Constants | PascalCase | `MaxRetryCount`, `DefaultSnapshotInterval` |
| Async methods | `*Async` suffix | `InvokeDomainServiceAsync` |

**Event Type Naming (D3 rejection convention):**

| Pattern | Convention | Example |
|---------|-----------|---------|
| State-change events | Past tense verb + noun | `OrderPlaced`, `PaymentProcessed`, `InventoryAdjusted` |
| Rejection events | Noun + past tense negative | `OrderRejected`, `PaymentDeclined`, `InsufficientFundsDetected` |
| Snapshot events | `{Aggregate}Snapshot` | `OrderSnapshot`, `PaymentSnapshot` |

**DAPR State Store Keys (D1, D2):**

| Key Pattern | Convention | Example |
|------------|-----------|---------|
| Event | `{tenant}:{domain}:{aggId}:events:{seq}` | `acme:payments:order-123:events:5` |
| Metadata | `{tenant}:{domain}:{aggId}:metadata` | `acme:payments:order-123:metadata` |
| Snapshot | `{tenant}:{domain}:{aggId}:snapshot` | `acme:payments:order-123:snapshot` |
| Command status | `{tenant}:{correlationId}:status` | `acme:abc-def-123:status` |

**JSON Serialization:**

| Context | Convention | Rationale |
|---------|-----------|-----------|
| REST API request/response | camelCase | ASP.NET Core default, standard REST convention |
| Event envelope metadata | camelCase | Consistent with API, CloudEvents convention |
| Event payload (opaque) | Determined by domain service | EventStore is schema-ignorant |
| ProblemDetails extensions | camelCase | `correlationId`, `tenantId`, `validationErrors` |

**Convention Engine Naming (Fluent API):**

| Input | Convention | Output |
|-------|-----------|--------|
| Type name to domain | Strip "Aggregate"/"Projection" suffix, kebab-case | `OrderAggregate` → `order` |
| Domain to state store | `{domain}-eventstore` | `order-eventstore` |
| Domain to pub/sub topic | `{tenant}.{domain}.events` (per D6) | `acme.order.events` |
| Domain to command endpoint | `{domain}-commands` | `order-commands` |
| Attribute override | `[EventStoreDomain("custom")]` replaces derived name | `[EventStoreDomain("billing")]` → `billing` |
| Multi-tenant pattern | `{domain}-{tenantId}-eventstore` (when enabled) | `order-acme-eventstore` |

**Convention Override Priority (5-layer cascade):**

| Layer | Source | Example |
|-------|--------|---------|
| 1. Convention defaults | Domain type name derivation | `OrderAggregate` → `order` |
| 2. Global code options | `AddEventStore(options => ...)` | Custom serializer for all domains |
| 3. Domain self-config | `OnConfiguring()` override | Per-domain state store component |
| 4. External config | `appsettings.json` / environment variables | Deployment-time overrides |
| 5. Explicit override | `Configure<EventStoreDomainOptions>(...)` | Full manual control |

Lower layers override higher layers. Layer 1 always applies unless overridden. Layers 4-5 enable deployment-time customization without recompilation.

### Structure Patterns

**Project Organization:**

| Pattern | Convention | Example |
|---------|-----------|---------|
| Feature folders over type folders | Group by domain concept | `Actors/`, `Commands/`, `Events/`, not `Models/`, `Services/`, `Interfaces/` |
| One public type per file | File name = type name | `EventEnvelope.cs`, `AggregateActor.cs` |
| Interface + implementation | Same folder, separate files | `IDomainServiceInvoker.cs`, `DaprDomainServiceInvoker.cs` |
| Extension methods | `*Extensions.cs` | `ServiceCollectionExtensions.cs` |
| Configuration | `*Options.cs` record types | `EventStoreOptions.cs`, `SnapshotOptions.cs` (DefaultInterval = 100) |
| DI registration | `Add*` extension methods | `AddEventStoreServer()`, `AddEventStoreClient()` |

**Test Organization:**

| Pattern | Convention | Example |
|---------|-----------|---------|
| Test class naming | `{ClassUnderTest}Tests` | `AggregateActorTests`, `EventEnvelopeTests` |
| Test method naming | `{Method}_{Scenario}_{ExpectedResult}` | `ProcessCommand_ValidCommand_ReturnsEvents` |
| Test data | `TestData/` folder or inline builders | `TestData/SampleCommands.cs` |
| Arrange/Act/Assert | Explicit AAA sections | Standard pattern |

### Communication Patterns

**Actor Processing Pipeline (core flow):**

```
Command received at API
  -> Status: Received (written at API layer, SubmitCommandHandler)
  -> MediatR pipeline (logging -> validation -> auth -> routing)
    -> Actor activated
      -> Thin Orchestrator (5 explicit steps, strictly ordered):
        1. Idempotency check (cheapest, prevents all subsequent work)
        2. Tenant validation (SEC-2, before any state access)
        3. State rehydration (snapshot + events via EventStreamReader)
        4. Domain service invocation (DAPR service invocation, D7)
        5. State machine execution:
           -> Events persisted via IActorStateManager (D1)
           -> Status: EventsStored
           -> Events published to pub/sub topic (D6)
           -> Status: EventsPublished -> Completed | Rejected | PublishFailed | TimedOut
```

**AggregateActor as Thin Orchestrator:**

The AggregateActor delegates all work to specialized components. The actor method body is ~5 lines of delegation:

```csharp
// Pseudocode -- illustrative, not implementation
public async Task<CommandResult> ProcessCommandAsync(CommandEnvelope command)
{
    if (await _idempotencyChecker.IsDuplicateAsync(command)) return CommandResult.AlreadyProcessed;
    _tenantValidator.Validate(command.TenantId, ActorId);
    var state = await _stateReader.RehydrateAsync();
    var events = await _domainInvoker.InvokeAsync(command, state);
    return await _stateMachine.ExecuteAsync(events);
}
```

**MediatR Pipeline Behaviors (ordered):**

1. `LoggingBehavior` -- structured log with correlation ID
2. `ValidationBehavior` -- FluentValidation on command structure
3. `AuthorizationBehavior` -- tenant x domain x command type check
4. `CommandHandler` -- routes to actor

**Dependency Injection Registration Order:**

```csharp
// In CommandApi Program.cs
builder.AddServiceDefaults();           // Aspire: resilience, telemetry, health
builder.Services.AddAuthentication();   // JWT
builder.Services.AddAuthorization();    // Policies
builder.Services.AddRateLimiter();      // Per-tenant (D8)
builder.Services.AddMediatR();          // Pipeline
builder.Services.AddEventStoreServer(); // Core services
builder.Services.AddActors();           // DAPR actors
```

### Process Patterns

**Error Handling:**

| Layer | Pattern | Example |
|-------|---------|---------|
| API controller | Return `ProblemDetails` (D5) | `return Problem(statusCode: 400, detail: "...")` |
| MediatR pipeline | Throw typed exceptions for auth/validation | `throw new UnauthorizedTenantException(tenantId)` |
| Actor processing | Domain rejections are events (D3); infrastructure exceptions propagate | `if (result.Any(e => e is RejectionEvent))` |
| DAPR interactions | Let DAPR resiliency handle transient failures | No custom retry in application code |
| Global handler | `IExceptionHandler` maps exceptions to ProblemDetails | Maps `UnauthorizedTenantException` -> 403 |

**Structured Logging Pattern (resolves GAP-5):**

| Stage | Required Fields | Level |
|-------|----------------|-------|
| Command received | correlationId, tenantId, domain, aggregateId, commandType | Information |
| Actor activated | correlationId, tenantId, aggregateId, currentSequence | Debug |
| Domain service invoked | correlationId, tenantId, domain, domainServiceVersion | Information |
| Events persisted | correlationId, tenantId, aggregateId, eventCount, newSequence | Information |
| Events published | correlationId, tenantId, topic, eventCount | Information |
| Command completed | correlationId, tenantId, aggregateId, status, durationMs | Information |
| Domain rejection | correlationId, tenantId, aggregateId, rejectionEventType | Warning |
| Infrastructure failure | correlationId, tenantId, aggregateId, exceptionType, message | Error |

**Never log:** Event payload data (NFR12), JWT tokens (NFR11), connection strings.

**OpenTelemetry Activity Naming:**

| Activity | Name Pattern | Example |
|---------|-------------|---------|
| Command API | `EventStore.CommandApi.{verb}` | `EventStore.CommandApi.Submit` |
| Actor processing | `EventStore.Actor.{operation}` | `EventStore.Actor.ProcessCommand` |
| Domain invocation | `EventStore.DomainService.Invoke` | `EventStore.DomainService.Invoke` |
| Event persistence | `EventStore.Events.Persist` | `EventStore.Events.Persist` |
| Event publishing | `EventStore.Events.Publish` | `EventStore.Events.Publish` |

### Enforcement Guidelines

**All AI Agents MUST:**

1. Follow C# naming conventions exactly as documented above -- no variations
2. Use feature folders, not type-based folders, within each project
3. Follow the MediatR pipeline behavior order (logging -> validation -> auth -> handler)
4. Never add custom retry logic -- all retries are DAPR resiliency policies
5. Never log event payload data -- only envelope metadata fields
6. Use `IActorStateManager` for all actor state operations -- never bypass with direct `DaprClient` state calls
7. Return `ProblemDetails` for all API error responses -- never custom error shapes
8. Name events in past tense (state-change) or past tense negative (rejection) -- never imperative
9. Include correlationId in every structured log entry and OpenTelemetry activity
10. Register services via `Add*` extension methods -- never inline in `Program.cs`
11. Event store keys are write-once -- once `{tenant}:{domain}:{aggId}:events:{seq}` is written, it is never updated or deleted. Violation indicates a bug, not a valid operation
12. Command status writes are advisory -- failure to write/update status must never block or fail the command processing pipeline. Status is ephemeral metadata, not a source of truth
13. No stack traces in production error responses -- `ProblemDetails.detail` contains human-readable message only; stack traces logged server-side at Error level, never exposed to clients
14. DAPR sidecar call timeout is 5 seconds -- all `DaprClient` and `IActorStateManager` calls must complete within 5s, enforced via DAPR resiliency timeout policy. Prevents hung sidecars from blocking actor turns indefinitely
15. Snapshot configuration is mandatory -- every domain registration must specify a snapshot interval (recommended default: 100 events). No "never snapshot" behavior allowed. Keeps actor activation reads ≤102 state store calls
16. E2E security tests use real Keycloak OIDC tokens -- never synthetic JWTs for runtime security verification. `TestJwtTokenGenerator` (HS256) is for fast unit/integration tests only. Runtime proof of the six-layer auth pipeline requires real IdP-issued tokens via Keycloak (D11)

## Project Structure & Boundaries

### Complete Project Directory Structure

```
Hexalith.EventStore/
├── .github/
│   └── workflows/
│       ├── ci.yml                              # Build + test on PR
│       ├── release.yml                         # Pack + publish NuGet on tag
│       └── integration.yml                     # DAPR integration tests
├── .editorconfig                               # Code style enforcement
├── .gitignore
├── Directory.Build.props                       # Shared build properties, MinVer, net10.0
├── Directory.Packages.props                    # Central package management
├── nuget.config
├── global.json                                 # .NET SDK version pinning (10.0.103)
├── Hexalith.EventStore.slnx
│
├── src/
│   ├── Hexalith.EventStore.Contracts/          # NuGet: Event envelope, types, identity
│   │   ├── Hexalith.EventStore.Contracts.csproj  # Depends on Hexalith.Commons.UniqueIds (D12)
│   │   ├── Events/
│   │   │   ├── EventEnvelope.cs                # 11-field metadata + payload + extensions
│   │   │   ├── EventMetadata.cs                # Metadata record type
│   │   │   ├── IEventPayload.cs                # Marker interface for event payloads
│   │   │   └── IRejectionEvent.cs              # Marker interface for rejection events (D3)
│   │   ├── Commands/
│   │   │   ├── CommandEnvelope.cs              # Command submission payload
│   │   │   └── CommandStatus.cs                # Status enum + record (D2)
│   │   ├── Identity/
│   │   │   ├── AggregateIdentity.cs            # tenant:domain:aggregate-id tuple
│   │   │   └── IdentityParser.cs               # Parse/format identity strings
│   │   ├── Results/
│   │   │   └── DomainResult.cs                 # List<DomainEvent> (D3: always events)
│   │   └── Serialization/
│   │       └── EventSerializer.cs              # JSON serialization contracts
│   │
│   ├── Hexalith.EventStore.Client/             # NuGet: Domain service SDK
│   │   ├── Hexalith.EventStore.Client.csproj
│   │   ├── Aggregates/
│   │   │   ├── EventStoreAggregate.cs          # High-level base class (wraps IDomainProcessor)
│   │   │   └── EventStoreProjection.cs         # Projection base class
│   │   ├── Attributes/
│   │   │   └── EventStoreDomainAttribute.cs    # [EventStoreDomain("name")] override
│   │   ├── Conventions/
│   │   │   └── NamingConventionEngine.cs       # Type name -> kebab-case resource names
│   │   ├── Discovery/
│   │   │   └── AssemblyScanner.cs              # Auto-discovery of aggregate/projection types
│   │   ├── Registration/
│   │   │   ├── DomainServiceRegistration.cs    # Registration metadata
│   │   │   ├── ServiceCollectionExtensions.cs  # AddEventStoreClient() + AddEventStore()
│   │   │   └── HostExtensions.cs               # UseEventStore() activation
│   │   ├── Handlers/
│   │   │   ├── IDomainProcessor.cs             # (Command, State?) -> List<Event> (low-level)
│   │   │   └── DomainProcessorBase.cs          # Base class with common patterns
│   │   └── Configuration/
│   │       ├── EventStoreClientOptions.cs      # Client configuration
│   │       ├── EventStoreOptions.cs            # Global cross-cutting options
│   │       └── EventStoreDomainOptions.cs      # Per-domain options
│   │
│   ├── Hexalith.EventStore.Server/             # NuGet: Core processing pipeline
│   │   ├── Hexalith.EventStore.Server.csproj
│   │   ├── Actors/
│   │   │   ├── IAggregateActor.cs              # Actor interface (FR3, FR4)
│   │   │   ├── AggregateActor.cs               # Thin orchestrator (5-step delegation)
│   │   │   ├── EventStoreStateManager.cs       # Event persistence via IActorStateManager (D1)
│   │   │   ├── ActorStateMachine.cs            # Checkpointed stages (NFR25)
│   │   │   └── IdempotencyChecker.cs           # Command deduplication check
│   │   ├── Commands/
│   │   │   ├── CommandRouter.cs                # Routes to correct actor (FR3)
│   │   │   ├── CommandStatusWriter.cs          # Writes status to state store (D2)
│   │   │   └── CommandValidator.cs             # Structural validation (FR2)
│   │   ├── Events/
│   │   │   ├── EventPersister.cs               # Append-only storage (FR9, D1)
│   │   │   ├── EventPublisher.cs               # Pub/sub distribution (FR17, D6)
│   │   │   ├── EventStreamReader.cs            # Replay from snapshot + events (FR12)
│   │   │   └── SnapshotManager.cs              # Snapshot creation/read (FR13, FR14)
│   │   ├── DomainServices/
│   │   │   ├── IDomainServiceInvoker.cs        # Invocation contract
│   │   │   ├── DaprDomainServiceInvoker.cs     # DAPR service invocation (D7)
│   │   │   └── DomainServiceResolver.cs        # Config store lookup (FR22)
│   │   ├── Pipeline/
│   │   │   ├── LoggingBehavior.cs              # MediatR behavior #1
│   │   │   ├── ValidationBehavior.cs           # MediatR behavior #2
│   │   │   ├── AuthorizationBehavior.cs        # MediatR behavior #3 (FR31)
│   │   │   └── SubmitCommandHandler.cs         # MediatR handler -> actor
│   │   ├── Security/
│   │   │   ├── TenantClaimsTransformation.cs   # IClaimsTransformation (FR31)
│   │   │   └── TenantAuthorizationPolicy.cs    # ABAC policies (FR32)
│   │   └── Configuration/
│   │       ├── EventStoreOptions.cs            # Server configuration
│   │       ├── SnapshotOptions.cs              # Snapshot intervals
│   │       └── ServiceCollectionExtensions.cs  # AddEventStoreServer()
│   │
│   ├── Hexalith.EventStore.CommandApi/         # Host: ASP.NET Core + DAPR actors
│   │   ├── Hexalith.EventStore.CommandApi.csproj
│   │   ├── Program.cs                          # DI registration order per patterns
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── Controllers/
│   │   │   ├── CommandsController.cs           # POST /api/v1/commands (FR1)
│   │   │   ├── CommandStatusController.cs      # GET /api/v1/commands/{id}/status (FR5)
│   │   │   └── ReplayController.cs             # POST /api/v1/commands/{id}/replay (FR6)
│   │   ├── Middleware/
│   │   │   ├── CorrelationIdMiddleware.cs      # Generates/propagates correlation ID (FR4)
│   │   │   └── TenantRateLimitingMiddleware.cs # Per-tenant rate limiting (D8)
│   │   ├── ErrorHandling/
│   │   │   └── GlobalExceptionHandler.cs       # IExceptionHandler -> ProblemDetails (D5)
│   │   ├── HealthChecks/
│   │   │   ├── DaprSidecarHealthCheck.cs       # FR38
│   │   │   ├── DaprConfigStoreHealthCheck.cs   # Config store readiness
│   │   │   └── ReadinessCheck.cs               # FR39
│   │   └── appsettings.json
│   │
│   ├── Hexalith.EventStore.Aspire/             # NuGet: Aspire integration
│   │   ├── Hexalith.EventStore.Aspire.csproj
│   │   └── EventStoreResourceExtensions.cs     # AddEventStore() for AppHost
│   │
│   ├── Hexalith.EventStore.ServiceDefaults/    # Shared: Resilience, telemetry, health
│   │   ├── Hexalith.EventStore.ServiceDefaults.csproj
│   │   └── Extensions.cs                       # AddServiceDefaults()
│   │
│   ├── Hexalith.EventStore.AppHost/            # Aspire orchestration
│   │   ├── Hexalith.EventStore.AppHost.csproj
│   │   ├── Program.cs                          # Full local topology definition
│   │   ├── KeycloakRealms/
│   │   │   └── hexalith-realm.json             # Realm import: users, client, claim mappers (D11)
│   │   └── DaprComponents/
│   │       ├── statestore.yaml                 # Redis (local) state store config
│   │       ├── pubsub.yaml                     # Redis (local) pub/sub config
│   │       ├── resiliency.yaml                 # DAPR resiliency policies
│   │       └── accesscontrol.yaml              # Per-app-id allow list (D4)
│   │
│   ├── Hexalith.EventStore.Testing/            # NuGet: Test helpers
│       ├── Hexalith.EventStore.Testing.csproj
│       ├── Builders/
│       │   ├── CommandEnvelopeBuilder.cs        # Test data builder
│       │   └── EventEnvelopeBuilder.cs          # Test data builder
│       ├── Fakes/
│       │   ├── InMemoryStateManager.cs          # Fake IActorStateManager
│       │   └── FakeDomainServiceInvoker.cs      # Fake domain service
│       └── Assertions/
│           └── EventAssertions.cs               # Event stream assertion helpers
│
├── deploy/                                        # Production DAPR component configurations
│   ├── dapr/
│   │   ├── statestore-postgresql.yaml             # PostgreSQL state store (production)
│   │   ├── statestore-cosmosdb.yaml               # Cosmos DB state store (scale)
│   │   ├── pubsub-rabbitmq.yaml                   # RabbitMQ pub/sub (production)
│   │   ├── pubsub-kafka.yaml                      # Kafka pub/sub (scale)
│   │   ├── resiliency.yaml                        # Production resiliency policies
│   │   └── accesscontrol.yaml                     # Production access control (D4)
│   └── README.md                                  # Deployment configuration guide
│
├── samples/
│   └── Hexalith.EventStore.Sample/             # Reference domain service
│       ├── Hexalith.EventStore.Sample.csproj
│       ├── Program.cs                          # Minimal API host with DAPR
│       ├── Counter/
│       │   ├── Commands/
│       │   │   └── IncrementCounter.cs         # Sample command
│       │   ├── Events/
│       │   │   ├── CounterIncremented.cs       # State-change event
│       │   │   └── CounterLimitExceeded.cs     # Rejection event (D3 demo)
│       │   ├── State/
│       │   │   └── CounterState.cs             # Aggregate state
│       │   └── CounterProcessor.cs             # Pure function handler
│       └── appsettings.json
│
└── tests/
    ├── Hexalith.EventStore.Contracts.Tests/    # Tier 1: Unit tests (no DAPR)
    │   ├── Hexalith.EventStore.Contracts.Tests.csproj
    │   ├── Events/
    │   │   └── EventEnvelopeTests.cs           # Envelope validation
    │   ├── Identity/
    │   │   └── AggregateIdentityTests.cs       # Identity parsing
    │   └── Commands/
    │       └── CommandEnvelopeTests.cs          # Command validation
    │
    ├── Hexalith.EventStore.Server.Tests/       # Tier 2: Integration (DAPR test container)
    │   ├── Hexalith.EventStore.Server.Tests.csproj
    │   ├── Actors/
    │   │   ├── AggregateActorTests.cs          # Actor processing pipeline
    │   │   ├── ActorStateMachineTests.cs       # Checkpointed recovery
    │   │   └── AtomicBatchWriteTests.cs        # IActorStateManager ACID verification
    │   ├── Events/
    │   │   ├── EventPersisterTests.cs          # State store operations
    │   │   └── EventPublisherTests.cs          # Pub/sub operations
    │   ├── Chaos/
    │   │   ├── PubSubFailureTests.cs           # Permanent pub/sub unavailability
    │   │   ├── SidecarTimeoutTests.cs          # 5s sidecar call timeout behavior
    │   │   └── ActorRebalancingTests.cs        # Tenant validation during rebalancing
    │   └── Fixtures/
    │       └── DaprTestFixture.cs              # DAPR container setup
    │
    └── Hexalith.EventStore.IntegrationTests/   # Tier 3: Contract (full Aspire)
        ├── Hexalith.EventStore.IntegrationTests.csproj
        ├── CommandLifecycleTests.cs             # End-to-end command -> event
        ├── MultiTenantIsolationTests.cs         # Tenant A != Tenant B (FR27-29)
        ├── InfrastructureSwapTests.cs           # Redis vs PostgreSQL (NFR29)
        └── Fixtures/
            └── AspireTestFixture.cs             # Full topology via Aspire testing
```

### Architectural Boundaries

**API Boundary (CommandApi):**

The CommandApi is the sole external entry point. All external interactions flow through REST endpoints. No component behind this boundary is directly accessible from outside.

```
External Consumers
    │
    ▼
[CommandApi] ── REST /api/v1/* ──► [MediatR Pipeline] ──► [Actor Host]
    │                                                          │
    │ reads                                              DAPR service
    ▼                                                    invocation
[State Store]                                                  │
(command status)                                               ▼
                                                     [Domain Services]
```

**Package Dependency Boundaries:**

```
Contracts ◄── Client        (domain service developers reference both)
    ▲
    │
Server ────► Contracts      (server depends on contracts, never reverse)
    ▲
    │
CommandApi ─► Server        (host depends on server, never reverse)
    │
    ▼
Aspire ────► CommandApi     (AppHost references deployable projects)

Testing ───► Contracts      (test helpers depend on contracts only)
             Server         (fakes implement server interfaces)
```

**Rule:** Dependencies flow inward. `Contracts` has zero dependencies on other Hexalith packages. `Server` depends only on `Contracts`. `CommandApi` depends on `Server` + `Contracts`. No circular dependencies.

### Requirements to Structure Mapping

| FR Category | Primary Project | Key Directories |
|------------|----------------|-----------------|
| Command Processing (FR1-FR8) | CommandApi + Server | `CommandApi/Controllers/`, `Server/Commands/`, `Server/Pipeline/` |
| Event Management (FR9-FR16) | Server + Contracts | `Server/Events/`, `Server/Actors/`, `Contracts/Events/` |
| Event Distribution (FR17-FR20) | Server | `Server/Events/EventPublisher.cs` |
| Domain Service Integration (FR21-FR25) | Server + Client | `Server/DomainServices/`, `Client/Handlers/` |
| Identity & Multi-Tenancy (FR26-FR29) | Contracts + Server | `Contracts/Identity/`, `Server/Security/` |
| Security & Authorization (FR30-FR34) | CommandApi + Server | `CommandApi/Middleware/`, `Server/Pipeline/`, `Server/Security/` |
| Observability & Operations (FR35-FR39) | ServiceDefaults + CommandApi | `ServiceDefaults/Extensions.cs`, `CommandApi/HealthChecks/` |
| Developer Experience (FR40-FR47) | AppHost + Sample + Testing | `AppHost/Program.cs`, `samples/`, `tests/` |

**Cross-Cutting Concern Locations:**

| Concern | Files |
|---------|-------|
| Multi-Tenant Isolation | `Contracts/Identity/`, `Server/Security/`, `Server/Actors/AggregateActor.cs`, `AppHost/DaprComponents/` |
| Observability | `ServiceDefaults/Extensions.cs`, `Server/Pipeline/LoggingBehavior.cs`, `CommandApi/Middleware/CorrelationIdMiddleware.cs` |
| Six-Layer Auth | `CommandApi/Program.cs` (layers 1-3), `Server/Pipeline/AuthorizationBehavior.cs` (layer 4), `Server/Actors/AggregateActor.cs` (layer 5), `AppHost/DaprComponents/` (layer 6) |
| Error Handling | `CommandApi/ErrorHandling/GlobalExceptionHandler.cs`, `Server/Actors/AggregateActor.cs` (D3), `Server/Pipeline/` |

### Data Flow

```
POST /api/v1/commands
    │
    ▼
CorrelationIdMiddleware (generate/propagate)
    │
    ▼
RateLimiting (per-tenant sliding window, D8)
    │
    ▼
JWT Authentication (layer 1) + Claims Transformation (layer 2)
    │
    ▼
[Authorize] policy (layer 3)
    │
    ▼
MediatR: Logging -> Validation -> Authorization (layer 4) -> SubmitCommandHandler
    │
    ▼
CommandStatusWriter: status = Received (at API layer, before actor invocation)
    │
    ▼
CommandRouter -> Actor activation
    │
    ▼
Actor Step 1: IdempotencyChecker (duplicate? -> return AlreadyProcessed)
    │
    ▼
Actor Step 2: TenantValidator (SEC-2, layer 5 -- before any state access)
    │
    ▼
CommandStatusWriter: status = Processing
    │
    ▼
Actor Step 3: EventStreamReader.RehydrateAsync() (snapshot + events)
    │
    ▼
Actor Step 4: DaprDomainServiceInvoker.InvokeAsync() (D7, DAPR policies layer 6)
    │
    ▼
Domain service returns List<DomainEvent> (D3: state-change or rejection)
    │
    ▼
EventStore populates all 11 envelope metadata fields (SEC-1)
    │
    ▼
Actor Step 5: StateMachine.ExecuteAsync()
    ├── EventPersister: batch save via IActorStateManager (D1: actor ACID, write-once keys)
    ├── CommandStatusWriter: status = EventsStored
    ├── EventPublisher: publish to {tenant}.{domain}.events (D6)
    ├── CommandStatusWriter: status = EventsPublished
    └── CommandStatusWriter: final status (Completed | Rejected | PublishFailed | TimedOut)
```

## Architecture Validation

### Validation Process

The architecture was validated through **3 rounds of Advanced Elicitation** (15 methods) and **1 Party Mode** collaborative review session. Each round stress-tested the architecture from different angles, progressively uncovering and resolving gaps.

| Round | Methods Applied | Findings | Key Outcomes |
|-------|----------------|----------|--------------|
| Round 1 | Chaos Monkey, Failure Mode Analysis, First Principles, Self-Consistency, Security Audit | 10 gaps found | 3 enforcement rules (#11-#13), PublishFailed terminal status, command idempotency, extension metadata limits |
| Party Mode | Winston (PM), Amelia (UX), Murat (QA), John (Arch) | 6 recommendations | Thin actor orchestrator, EventStoreStateManager rename, test tier allocation, Chaos/ test directory, sample-as-template, ACID verification tests |
| Round 2 | Comparative Analysis Matrix, Pre-mortem, User Persona Focus Group, Occam's Razor, Graph of Thoughts | 13 findings, 1 CRITICAL | **CRITICAL revision: UnknownEvent skip removed** (correctness violation), TimedOut status, 2 enforcement rules (#14-#15), deploy/ directory, sidecar timeout, mandatory snapshot config |
| Round 3 | Critique and Refine, What If Scenarios, 5 Whys Deep Dive, Active Recall Testing, Hindsight Reflection | 7 findings | Command status TTL, IRejectionEvent interface, ETag hard requirement, snapshot defaults (100 events), actor pipeline ordering, Received status write location |

### Coherence Validation

All 10 architectural decisions (D1-D10) verified for mutual compatibility:

| Decision Pair | Interaction | Compatible? |
|--------------|-------------|-------------|
| D1 + D2 | Both use DAPR state store with independent key namespaces | Yes |
| D1 + D3 | Rejection events stored identically to state-change events (same write path) | Yes |
| D3 + D6 | Rejection events published to same topics as state-change events | Yes |
| D4 + D7 | Access control allows exactly the invocation paths D7 requires | Yes |
| D5 + D8 | Rate limit 429 returned as ProblemDetails with tenant context | Yes |
| D7 + D4 | Service invocation paths match access control allow list | Yes |
| D8 + D2 | Rate-limited commands never reach actor; no phantom status entries | Yes |
| D9 + D10 | MinVer Git tags integrated into GitHub Actions release pipeline | Yes |

### Requirements Coverage

**Functional Requirements: 47/47 covered**

| Category | FRs | Coverage |
|----------|-----|----------|
| Command Processing | FR1-FR8 | CommandApi controllers, MediatR pipeline, actor routing, idempotency check, dead-letter topic |
| Event Management | FR9-FR16 | EventPersister (D1 write-once keys), EventStreamReader, SnapshotManager (100-event default), actor ACID |
| Event Distribution | FR17-FR20 | EventPublisher (D6 topic naming), CloudEvents 1.0, at-least-once delivery, persist-then-publish |
| Domain Service Integration | FR21-FR25 | DaprDomainServiceInvoker (D7), DomainServiceResolver (config store), pure function contract (D3) |
| Identity & Multi-Tenancy | FR26-FR29 | AggregateIdentity (canonical tuple), triple-layer isolation, tenant-scoped state store keys |
| Security & Authorization | FR30-FR34 | Six-layer auth (JWT → Claims → Endpoint → MediatR → Actor → DAPR), TenantValidator (SEC-2) |
| Observability & Operations | FR35-FR39 | OpenTelemetry activities, structured logging pattern, health checks (sidecar + config store + readiness) |
| Developer Experience | FR40-FR47 | AppHost orchestration, sample domain service (de-facto template), NuGet packages, three-tier testing |

**Non-Functional Requirements: 32/32 covered**

| Category | NFRs | Key Coverage |
|----------|------|-------------|
| Performance | NFR1-NFR8 | Thin actor orchestrator, snapshot-based rehydration (≤102 reads), 5s sidecar timeout |
| Security | NFR9-NFR15 | TLS via DAPR mTLS, JWT every request, no payload in logs (rule #5), triple-layer isolation, access control (D4) |
| Scalability | NFR16-NFR20 | Actor placement (consistent hashing), per-tenant rate limiting, dynamic config via DAPR config store |
| Reliability | NFR21-NFR26 | Checkpointed state machine (8 states, 4 terminal), write-once event keys (rule #11), advisory status writes (rule #12) |
| Integration | NFR27-NFR32 | Backend-agnostic via DAPR abstractions (ETag hard requirement), OTLP telemetry, Aspire publishers, deploy/ configs |

### Implementation Readiness Checklist

| Criterion | Status | Notes |
|-----------|--------|-------|
| All blocking gaps resolved | Pass | GAP-1 through GAP-3 resolved by D1, D2, D3 |
| All design-phase gaps resolved | Pass | GAP-4 through GAP-9 resolved by D4-D8 + patterns |
| No circular dependencies | Pass | Contracts → (no deps), Server → Contracts, CommandApi → Server |
| All enforcement rules testable | Pass | 15 rules, each verifiable via unit or integration tests |
| All security constraints enforceable | Pass | 5 SEC constraints, each with explicit enforcement point |
| All terminal statuses defined | Pass | Completed, Rejected, PublishFailed, TimedOut |
| Recovery procedures documented | Pass | Deserialization failure, pub/sub failure, sidecar timeout |
| Test strategy covers all tiers | Pass | Unit (Contracts.Tests), Integration (Server.Tests + Chaos/), Contract (IntegrationTests) |
| Operational guidance present | Pass | deploy/ directory, production DAPR YAMLs, command status TTL |

### Critical Architectural Revisions

**CRITICAL-1: UnknownEvent Handling (Round 2, FG-GAP-1)**

- **Original design:** Unknown event types during state rehydration would be deserialized as `UnknownEvent` with state projection skipping them
- **Problem:** Skipping events produces incorrect aggregate state. If events 5, 6, 7 exist and event 6 is "unknown," the aggregate state after rehydration reflects only events 5 and 7 -- which is wrong
- **Revised design:** Domain services MUST maintain backward-compatible deserialization for ALL event types they have ever produced. UnknownEvent during rehydration is an error condition, not a normal path
- **Recovery procedure:** (1) Redeploy previous domain service version, (2) Add backward-compatible deserializer for the removed event type
- **Impact:** This revision affects D3 contract, domain service versioning strategy, and enforcement rules

**CRITICAL-2: Command Status TTL (Round 3, CR3-1 + HR-1)**

- **Problem:** Command status entries accumulate indefinitely in the state store. A high-throughput tenant (1000 commands/day) generates 365K status entries/year
- **Resolution:** Default 24-hour TTL on status entries via DAPR `ttlInSeconds` metadata, configurable per-tenant
- **Impact:** Affects D2 specification, operational guidance

### Enforcement Rules Summary (Final: 17 Rules)

| # | Rule | Category |
|---|------|----------|
| 1 | C# naming conventions exactly as documented | Consistency |
| 2 | Feature folders, not type-based folders | Structure |
| 3 | MediatR pipeline order: logging → validation → auth → handler | Pipeline |
| 4 | Never add custom retry logic -- DAPR resiliency only | Resilience |
| 5 | Never log event payload data -- envelope metadata only | Security |
| 6 | IActorStateManager for all actor state operations -- never DaprClient bypass | Actor isolation |
| 7 | ProblemDetails for all API error responses -- never custom shapes | API consistency |
| 8 | Events named in past tense (state-change) or past tense negative (rejection) | Naming |
| 9 | correlationId in every structured log and OpenTelemetry activity | Observability |
| 10 | Services registered via Add* extension methods -- never inline | DI |
| 11 | Event store keys are write-once -- never updated or deleted | Data integrity |
| 12 | Command status writes are advisory -- never block pipeline | Resilience |
| 13 | No stack traces in production error responses | Security |
| 14 | DAPR sidecar call timeout is 5 seconds | Resilience |
| 15 | Snapshot configuration is mandatory (default 100 events) | Performance |
| 16 | E2E security tests use real Keycloak OIDC tokens -- never synthetic JWTs for runtime security verification | Testing |
| 17 | Convention-derived resource names use kebab-case; type suffix stripping is automatic (Aggregate, Projection); attribute overrides are validated at startup for non-empty, kebab-case compliance | Naming |

### Security Constraints Summary (Final: 5 Constraints)

| # | Constraint | Enforcement Point |
|---|-----------|------------------|
| SEC-1 | EventStore owns all 11 envelope metadata fields | EventPersister (after domain service returns) |
| SEC-2 | Tenant validation BEFORE state rehydration | Actor Step 2 (TenantValidator) |
| SEC-3 | Command status queries are tenant-scoped | CommandStatusController (JWT tenant match) |
| SEC-4 | Extension metadata sanitized at API gateway | CorrelationIdMiddleware / validation pipeline |
| SEC-5 | Event payload data never in logs | LoggingBehavior + structured logging framework |

### Deferred Items

| Item | Deferred To | Rationale |
|------|------------|-----------|
| Blazor dashboard | v2 | No UI in v1 |
| DAPR Workflow migration | v2 | v1 actors with workflow-ready state machine |
| External authorization (OpenFGA/OPA) | v2 | JWT six-layer sufficient for v1 |
| Event stream compaction/archival | v2/v3 | Events accumulate; snapshots handle read performance |
| Event versioning/migration tooling | v3 | Requires stable envelope first |
| GDPR crypto-shredding | v3 | Per-tenant encryption key management |
| gRPC Command API | v4 | REST-only in v1 |
| Serialization format migration (JSON → Protobuf) | Future | serializationFormat field in envelope enables this |
