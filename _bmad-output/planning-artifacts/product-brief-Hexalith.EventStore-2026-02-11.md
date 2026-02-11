---
stepsCompleted: [1, 2, 3, 4, 5, 6]
workflowCompleted: true
completionDate: 2026-02-11
inputDocuments:
  - brainstorming-session-2026-02-11.md
  - market-event-sourcing-event-store-solutions-research-2026-02-11.md
  - technical-aspnet-core-command-api-authorization-research-2026-02-11.md
  - technical-blazor-fluent-ui-v4-research-2026-02-11.md
  - technical-dapr-workflow-pubsub-actors-research-2026-02-11.md
  - technical-dotnet-10-aspire-13-research-2026-02-11.md
date: 2026-02-11
author: Jerome
---

# Product Brief: Hexalith.EventStore

## Executive Summary

Hexalith.EventStore is an open-source (MIT), .NET-native event sourcing server that provides a complete command processing and event persistence backbone for Domain-Driven Design applications. Built on DAPR as its runtime infrastructure layer, it delivers the full event sourcing pipeline -- from command API ingestion through actor-based processing to event distribution via pub/sub -- with a Blazor Fluent UI operational control plane for tenant management, domain routing, and event stream exploration.

The project addresses a fundamental gap in the .NET ecosystem: there is no complete, infrastructure-portable event sourcing server that stays pure .NET while remaining agnostic to specific databases and message brokers. Existing solutions force trade-offs -- Marten locks you to PostgreSQL, EventStoreDB requires a separate database server, and custom implementations demand assembling disparate tools with no unified operational story. Hexalith.EventStore eliminates these trade-offs by delegating all infrastructure concerns to DAPR's 135 pluggable components while keeping application code entirely in .NET.

Designed as a reusable backbone for multiple DDD applications within the Hexalith ecosystem, the server treats multi-tenancy not as a feature but as its core addressing scheme (`tenant:domain:aggregate-id`), enforces domain service purity (command in, domain events out), and models everything -- including snapshots -- as events with a uniform 11-field metadata envelope. The architecture has been stress-tested across 10 failure scenarios with zero data-loss vulnerabilities identified.

---

## Core Vision

### Problem Statement

.NET developers building Domain-Driven Design applications with event sourcing face a fragmented tooling landscape. No existing solution provides a complete, .NET-native event sourcing server that is infrastructure-portable, multi-tenant by design, and operationally self-sufficient. Teams are forced to either adopt solutions that lock them into specific databases, run separate non-.NET database servers, or build custom implementations by assembling disparate libraries -- incurring significant integration effort with no unified operational control plane.

### Problem Impact

- **Infrastructure lock-in**: Marten requires PostgreSQL; Equinox requires CosmosDB or EventStoreDB; custom implementations couple directly to a chosen database
- **Operational gap**: No existing .NET event sourcing solution ships with an integrated admin UI for tenant management, domain routing configuration, and event stream exploration
- **Assembly tax**: Teams building event-sourced systems must separately integrate an event store, a message broker, a command processing pipeline, snapshotting, and observability -- each with its own configuration, failure modes, and operational story
- **Multi-tenancy as afterthought**: Existing solutions treat multi-tenancy as an application-level concern, requiring developers to implement tenant isolation themselves

### Why Existing Solutions Fall Short

- **EventStoreDB/KurrentDB**: Purpose-built but requires running a separate database server outside the .NET ecosystem; projection system fragility; operational complexity of a dedicated cluster
- **Marten/Critter Stack**: Productive and .NET-native, but locked to PostgreSQL; async projection event skipping under load; no integrated operational UI
- **Custom implementations**: The largest "competitor" (~35-40% of the market) -- teams build lightweight event stores on existing databases, but these lack standardized command processing, multi-tenant isolation, event distribution, and operational tooling
- **All solutions**: None provide DAPR-level infrastructure portability -- the ability to swap state stores, message brokers, and deployment targets without code changes

### Proposed Solution

Hexalith.EventStore is a complete event sourcing server built on .NET 10 and DAPR that provides:

- **Command API gateway** with REST endpoints, authentication, and multi-dimensional authorization (tenant + domain + command type)
- **DAPR Actor-based aggregate processing** with single-threaded, turn-based concurrency eliminating race conditions by design
- **DAPR Workflow orchestration** for command lifecycle management with checkpointed state and built-in saga/compensation support
- **Event persistence** via DAPR state stores (swappable: Redis, PostgreSQL, CosmosDB, SQL Server, and 26 others)
- **Event distribution** via DAPR pub/sub (swappable: Kafka, RabbitMQ, Azure Service Bus, and 12 others)
- **Domain service integration** where external domain processors are pure functions over the network: `(Command, CurrentState?) -> List<DomainEvent>`
- **Blazor Fluent UI control plane** for system health monitoring, tenant/domain routing management, and event stream exploration
- **.NET Aspire orchestration** for local development, testing, and multi-target deployment

### Key Differentiators

1. **Pure infrastructure, zero domain knowledge**: EventStore is domain-agnostic like a database -- domain logic lives entirely in external domain services that register as handlers scoped by tenant and domain
2. **DAPR as the operating system**: Actors, workflows, pub/sub, state management, mTLS security, observability, and resiliency policies all delegated to DAPR -- the EventStore is a thin application layer on a CNCF Graduated runtime
3. **One identity rules everything**: `tenant:domain:aggregate-id` addresses actors, queue sessions, event streams, and pub/sub topics -- one tuple, zero mapping complexity
4. **Everything is an event**: Uniform 11-field metadata envelope (aggregate ID, tenant, domain, sequence, timestamp, correlation + causation IDs, user identity, domain service version, event type name, serialization format) for all data including snapshots
5. **Multi-tenancy as addressing scheme**: Triple-layered tenant isolation (actor identity + DAPR policies + command metadata) baked into the architecture, not bolted on
6. **Operational control plane included**: Blazor Fluent UI admin dashboard for real-time system health, routing configuration with hot reload, and full event stream time-travel exploration
7. **MIT open-source, .NET-native**: Pure .NET 10 application code with full infrastructure portability through DAPR's 135 pluggable components

---

## Target Users

### Primary Users

**Persona: "Jerome" -- The DDD Platform Developer**

- **Role**: Senior .NET developer and architect building multiple Domain-Driven Design applications on a shared Hexalith platform
- **Environment**: Works across the full stack -- writes domain services, configures infrastructure, operates the event store, and deploys to production. Uses Visual Studio 2026, .NET 10, and DAPR locally via Aspire orchestration
- **Motivations**: Wants to focus on domain logic, not infrastructure plumbing. Values clean architectural patterns (DDD, CQRS, event sourcing) and refuses to be locked into specific databases or message brokers. Builds reusable foundations so each new application doesn't start from scratch
- **Current Pain**: Every new DDD application requires re-assembling the event sourcing stack -- choosing a store, wiring up a message broker, building command processing, implementing multi-tenant isolation, and setting up operational tooling. Existing solutions either lock you to PostgreSQL (Marten), require a separate database server (EventStoreDB), or leave you assembling disparate libraries with no unified operational story
- **Workarounds**: Custom implementations on existing databases, manually stitching together libraries, writing bespoke multi-tenant isolation code for each project
- **Success looks like**: `dotnet new` a domain service, register it with EventStore, write a pure function `(Command, CurrentState?) -> List<DomainEvent>`, and everything else -- command routing, event persistence, pub/sub distribution, tenant isolation, observability -- just works. New Hexalith applications plug into the backbone in hours, not weeks
- **"Aha!" moment**: The first time a new domain service registers with EventStore and commands flow through actors to events to subscribers without writing any infrastructure code

**Key interactions:**
- Defines domain services as pure functions over the network
- Configures tenant + domain routing via static configuration or Blazor UI
- Debugs event streams using correlation/causation IDs when something goes wrong
- Swaps infrastructure backends (Redis for dev, PostgreSQL for prod) via DAPR component config -- zero code changes
- Runs the full system locally via `dotnet aspire run`

### Secondary Users

**Persona: "Alex" -- The Support Engineer**

- **Role**: Technically skilled support engineer responsible for keeping production Hexalith applications healthy. Reads logs, understands event streams, and can trace a command through the full processing pipeline
- **Environment**: Works primarily in the Blazor Fluent UI admin dashboard. Has access to OpenTelemetry traces, structured logs, and the event stream explorer. Does not write domain service code but understands the architecture
- **Motivations**: Needs to quickly diagnose and resolve production issues -- "a command went in but nothing happened," "events aren't reaching subscribers," "a tenant's domain service is returning errors." Wants actionable visibility without needing to SSH into boxes or read source code
- **Current Pain**: Without an integrated control plane, diagnosing event sourcing issues requires piecing together information from multiple tools -- container logs, message broker dashboards, database queries, and tracing backends. No single view connects a command to its events to its subscribers
- **Workarounds**: Manual log correlation, direct database queries, separate monitoring dashboards per infrastructure component
- **Success looks like**: Opens the Blazor dashboard, sees system health at a glance, drills into a specific tenant's event stream, traces a command through its full lifecycle (received -> processing -> events stored -> events published -> done), and identifies the failure point in under 2 minutes
- **"Aha!" moment**: Using the event stream explorer as a "time machine" -- seeing the full causal history of any aggregate, tracing any event back to its command and user request via correlation + causation IDs

**Key interactions:**
- Monitors system health dashboard (queue depths, actor counts, error rates, subscriber lag)
- Inspects event streams for specific aggregates using the time-travel explorer
- Traces commands end-to-end using correlation IDs across the full lifecycle
- Manages tenant configuration -- suspending tenants, updating domain routing, rolling back domain service versions via the UI
- Handles dead-letter commands -- inspects, fixes, and replays poison commands

### User Journey

**Developer journey (Jerome):**
1. **Discovery**: Already knows event sourcing; finds Hexalith.EventStore via GitHub/NuGet while looking for a .NET-native, infrastructure-portable solution
2. **Onboarding**: Runs `dotnet aspire run` on the sample project, sees the full system (EventStore + sample domain service + Blazor dashboard) running locally in minutes
3. **First domain service**: Implements a pure function domain processor, registers it with EventStore, sends a command via the REST API, and watches events flow through the dashboard
4. **Core usage**: Builds new Hexalith applications by writing domain services that register with the shared EventStore backbone. Configures tenants and domains. Swaps infrastructure backends as needs evolve
5. **Long-term**: EventStore becomes invisible infrastructure -- it just works. Focus shifts entirely to domain logic across multiple applications

**Support engineer journey (Alex):**
1. **Discovery**: Introduced to the Blazor dashboard as part of onboarding to support Hexalith applications
2. **Onboarding**: Learns the command lifecycle model (received -> processing -> stored -> published -> done) and how to navigate tenant/domain/aggregate hierarchy
3. **Core usage**: Daily health checks via dashboard, responds to alerts, traces failing commands, manages tenant configuration
4. **Success moment**: Resolves a production incident by tracing a stuck command to a failing domain service version, rolling back the version via the UI, and watching queued commands drain successfully -- all without a developer
5. **Long-term**: Becomes the frontline for all Hexalith application operational issues, empowered by full visibility into the event sourcing pipeline

---

## Success Metrics

### User Success Metrics

**Developer Experience (Jerome persona):**
- **Time to first running system**: A new developer can clone the repo, run `dotnet aspire run`, and have the full EventStore + sample domain service + Blazor dashboard running locally in under 10 minutes
- **Time to first domain service**: A developer can implement and register a new domain service with EventStore in under 1 hour, following documentation and templates
- **Learning curve**: A .NET developer familiar with DDD concepts can understand the programming model (pure function domain processor, command/event flow) within a single working session without prior event sourcing experience
- **Deployment simplicity**: Production deployment achievable via Aspire publishers (Docker Compose, Kubernetes, or Azure Container Apps) with a single `aspire publish` command -- no manual infrastructure wiring
- **Infrastructure swap friction**: Changing state store or message broker backend requires only DAPR component YAML changes -- zero application code modifications

**Support Engineer Experience (Alex persona):**
- **Mean time to diagnosis**: Support engineer can identify the failure point for a stuck command in under 2 minutes using the Blazor dashboard
- **Self-service resolution**: Common operational tasks (tenant suspension, domain version rollback, dead-letter inspection/replay) completable entirely through the UI without developer involvement

### Business Objectives

**Project Viability (6 months):**
- At least 2 Hexalith DDD applications running on the EventStore backbone in production
- Support engineer operating independently via the Blazor dashboard without escalating routine issues to developers
- Architecture validated: no data-loss incidents, no fundamental design rework required

**Open-Source Traction (12 months):**
- GitHub repository with meaningful community engagement (stars, issues, discussions)
- NuGet package published with documented getting-started guide and sample projects
- At least one external contributor or adopter beyond the Hexalith team

**Strategic Position (18+ months):**
- Recognized as a viable option in the .NET event sourcing landscape alongside Marten and EventStoreDB
- Documented production usage demonstrating DAPR-based infrastructure portability across multiple backends

### Key Performance Indicators

**Technical Performance:**

| KPI | Target | Measurement |
|-----|--------|-------------|
| Event append latency (p99) | < 10ms | End-to-end from actor event persist to state store confirmation |
| Actor activation latency (p99) | < 50ms | Cold activation with state rehydration from snapshot + subsequent events |
| Pub/sub delivery latency (p99) | < 50ms | From event persistence to subscriber delivery |
| Aggregate replay (1000 events) | < 100ms | Full state reconstruction from event stream |
| Command lifecycle (end-to-end) | < 200ms | From REST API receipt to event published on pub/sub |
| System availability | 99.9%+ | With HA DAPR control plane and multi-replica deployment |

**Developer Experience:**

| KPI | Target | Measurement |
|-----|--------|-------------|
| Time to running locally | < 10 min | Clone to working system with Aspire |
| Time to first domain service | < 1 hour | New domain service registered and processing commands |
| Zero-code infrastructure swap | 0 lines changed | Backend change via DAPR component config only |
| Documentation completeness | 100% of core workflows | Getting started, domain service creation, deployment, admin UI |

**Community Adoption:**

| KPI | Target (12 months) | Measurement |
|-----|---------------------|-------------|
| GitHub stars | 100+ | Repository engagement signal |
| NuGet downloads | 500+ | Package adoption |
| External contributors | 1+ | PRs from outside core team |
| GitHub issues/discussions | Active | Community engagement beyond core team |

---

## MVP Scope

### Core Features

**v1: The Event Sourcing Pipeline**

The MVP delivers the complete command-to-event pipeline -- the minimum infrastructure needed to build a Hexalith DDD application on top of EventStore.

**1. Event Envelope (11-field metadata)**
- Define the canonical event metadata structure: aggregate ID, tenant, domain, sequence, timestamp, correlation ID, causation ID, user identity, domain service version, event type name, serialization format
- This is the single most critical decision -- every event ever stored conforms to this shape
- JSON serialization for v1 (serialization_format field enables future binary migration)

**2. Identity Scheme (`tenant:domain:aggregate-id`)**
- Canonical identity format and parsing logic
- Actor ID, queue session ID, event stream key, and pub/sub topic naming all derived from the same tuple
- Multi-tenant addressing baked into every primitive

**3. Command API Gateway**
- REST endpoints for command submission
- JWT authentication with multi-dimensional authorization (tenant + domain + command type)
- Command validation and routing to the correct DAPR actor based on tenant + domain + aggregate ID
- Command response = "accepted" (async), never "completed"

**4. DAPR Actor-Based Aggregate Processing**
- Each aggregate instance = one DAPR actor (tenant:domain:aggregate-id)
- Single-threaded, turn-based concurrency -- no race conditions by design
- Actor pulls command, calls external domain service, persists events, publishes events, marks done
- Actor state IS the processing checkpoint -- crash recovery via state resumption

**5. Domain Service Integration**
- Domain services register as handlers scoped by tenant + domain
- Registration is deployment-time configuration (DAPR config store): tenant + domain + version -> DAPR service endpoint
- Domain processor contract: `(Command, CurrentState?) -> List<DomainEvent>`
- Domain service reads event stream from EventStore to build current state
- EventStore is schema-ignorant -- stores opaque payloads with metadata

**6. Event Persistence**
- Append-only event storage via DAPR state store (Redis for dev, PostgreSQL/CosmosDB for prod)
- Composite key strategy: `{tenant}:{domain}:{aggregateId}-{sequenceNumber}`
- ETag-based optimistic concurrency for append operations
- Event stream read API: latest snapshot + subsequent events

**7. Event Distribution**
- DAPR pub/sub publishing after event persistence (per tenant+domain topic)
- CloudEvents 1.0 envelope format
- At-least-once delivery guarantee
- Subscribers receive events without EventStore needing to know who they are

**8. Snapshot Support**
- Snapshot as a special event type (uniform data model)
- EventStore controls WHEN (every N events, configurable), domain controls WHAT (snapshot content)
- Event stream read returns latest snapshot + subsequent events for efficient replay

**9. Aspire Orchestration**
- AppHost defining the full local development topology (EventStore + DAPR sidecar + sample domain service + state store + message broker)
- `dotnet aspire run` for single-command local startup
- OpenTelemetry observability out of the box (distributed tracing, metrics, structured logging via DAPR)

**10. Sample Domain Service**
- Reference implementation of a domain processor demonstrating the pure function contract
- Serves as both documentation and integration test fixture

### Out of Scope for MVP

**Deferred to v2 -- Blazor Admin UI:**
- System health dashboard
- Tenant/domain routing management UI
- Event stream time-travel explorer
- Dead-letter command inspection and replay UI
- Domain service version rollback UI
- Rationale: Developers can operate v1 via logs, OpenTelemetry traces, and direct API calls. The admin UI adds significant development effort without blocking the core pipeline

**Deferred to v2 -- DAPR Workflow Orchestration:**
- Command lifecycle workflow with checkpointed steps
- Saga/process manager support
- Compensation patterns for distributed rollback
- Rationale: v1 actors handle the full command lifecycle directly. Workflows add orchestration sophistication needed for complex multi-aggregate operations, but simple single-aggregate command processing works without them

**Deferred to v2+ -- Advanced Features:**
- GDPR crypto-shredding (per-tenant encryption key management)
- Event versioning/migration tooling
- Cloud replica and active-passive failover
- Snapshot-based event archival (hot/cold storage tiering)
- Max causation depth guardrail (saga loop prevention)
- gRPC command API (REST-only in v1)

**Explicitly NOT in scope:**
- Any domain-specific logic -- EventStore is pure infrastructure
- Custom projection/read-model framework -- subscribers build their own
- User management / identity provider -- external IdP via JWT

### MVP Success Criteria

**Go/No-Go Gates:**
- A domain service can register, receive commands, and produce events through the full pipeline end-to-end
- Events are persisted durably and published to subscribers reliably (zero data loss in testing)
- Multi-tenant isolation confirmed: commands for tenant A never reach tenant B's domain service
- Infrastructure swap validated: same application code works with Redis (dev) and PostgreSQL (prod) backends
- Local development experience: clone-to-running in under 10 minutes via Aspire
- At least 1 Hexalith DDD application successfully built on the MVP backbone

**Decision point for v2:** MVP is validated when the first Hexalith application is running in production on the EventStore backbone and the architecture has survived real-world usage without fundamental design rework.

### Future Vision

**v2 -- Operational Control Plane:**
- Blazor Fluent UI admin dashboard (system health, tenant management, event explorer)
- DAPR Workflow orchestration replacing direct actor lifecycle management
- Saga/process manager support for multi-aggregate operations
- Dead-letter command management with inspect/fix/replay
- Domain service version management with instant rollback

**v3 -- Enterprise Readiness:**
- GDPR crypto-shredding with per-tenant encryption keys
- Event versioning/migration tooling (comparable to EF Migrations)
- Active-passive failover with cloud replica
- Hot/cold storage tiering for event archival
- Advanced authorization (per-command ACLs, aggregate-state-dependent rules)

**v4 -- Ecosystem & Community:**
- `dotnet new` templates for domain service creation
- Domain service SDK with testing helpers
- Interactive onboarding tutorial
- Published benchmarks and production case studies
- Plugin architecture for custom middleware in the command pipeline
