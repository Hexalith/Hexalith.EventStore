---
stepsCompleted:
  - step-01-init
  - step-02-discovery
  - step-03-success
  - step-04-journeys
  - step-05-domain
  - step-06-innovation
  - step-07-project-type
  - step-08-scoping
  - step-09-functional
  - step-10-nonfunctional
  - step-11-polish
  - step-01b-continue
  - step-12-complete
inputDocuments:
  - product-brief-Hexalith.EventStore-2026-02-11.md
  - market-event-sourcing-event-store-solutions-research-2026-02-11.md
  - technical-aspnet-core-command-api-authorization-research-2026-02-11.md
  - technical-blazor-fluent-ui-v4-research-2026-02-11.md
  - technical-dapr-workflow-pubsub-actors-research-2026-02-11.md
  - technical-dotnet-10-aspire-13-research-2026-02-11.md
  - brainstorming-session-2026-02-11.md
  - brainstorming-session-2026-03-12-1.md
workflowType: 'prd'
documentCounts:
  briefs: 1
  research: 5
  brainstorming: 2
  projectDocs: 0
classification:
  projectType: 'Event Sourcing Server Platform (primary) + Developer Tooling (secondary)'
  domain: 'Event Sourcing Infrastructure'
  complexity: 'High (Technical)'
  complexityDrivers:
    - distributed actor state management
    - exactly-once event persistence semantics
    - multi-tenant isolation at infrastructure level
    - irreversible event envelope design
    - DAPR runtime dependency adding operational surface area
  projectContext: 'Greenfield codebase, pre-validated architecture'
  scopeStrategy: 'Deliberate phasing - v1 actors with workflow-ready design, v2 workflow migration'
lastEdited: '2026-03-12'
editHistory:
  - date: '2026-03-12'
    changes: 'Integrate brainstorming session (2026-03-12): added Query Pipeline & Projection Caching (FR50-FR60, NFR35-NFR39), Journey 7 (Marco Builds a Read Model), Innovation #5 (ETag Actor pattern), expanded Phase 2 roadmap with query pipeline/SignalR/contract library. Party mode review fixes: Query API endpoint spec, query success criteria, FR50 checksum pinned to 11-char + serialization risk note, FR52 default transport, FR53/FR54 two-tier clarification, FR58 coarse invalidation rationale, NFR35 warm-actor qualifier, NFR reordering, test tier updates. Post-validation fixes: FR59 (SignalR auto-rejoin on circuit reconnect), FR60 (3 sample UI refresh patterns)'
  - date: '2026-03-12'
    changes: 'Fix all validation report findings: added Journey 6 (Platform Validation), FR49 (command idempotency), NFR33-34 (rate limiting), refined FR10/FR13, added Data Schema patterns, added causation depth guardrail to Phase 2 roadmap'
---

# Product Requirements Document - Hexalith.EventStore

**Author:** Jerome
**Date:** 2026-02-11

## Executive Summary

Hexalith.EventStore is an open-source, DAPR-native event sourcing server platform for .NET. It provides the missing infrastructure layer between DAPR's distributed building blocks and DDD application development -- enabling developers to build event-sourced applications by implementing pure function domain processors (`(Command, CurrentState?) -> List<DomainEvent>`) without writing any infrastructure code.

**Core Differentiator:** Unlike existing solutions that are either purpose-built databases (EventStoreDB), PostgreSQL-coupled libraries (Marten), or JVM-centric frameworks (Axon), Hexalith.EventStore composes DAPR's actors, state store, pub/sub, and config store into an infrastructure-agnostic event sourcing platform. Infrastructure decisions become deployment decisions -- the same application code runs on Redis (dev), PostgreSQL (prod), or Cosmos DB (scale) with zero code changes.

**Target Users:**
- **Domain service developers** -- Build event-sourced DDD services using the pure function programming model via NuGet packages and REST Command API
- **DevOps engineers** -- Deploy and configure EventStore across environments using Aspire publishers and DAPR component YAML files
- **Support engineers** (v2) -- Monitor and resolve operational issues via the Blazor Fluent UI admin dashboard

**Development Strategy:** Deliberate phasing -- v1 delivers the complete command-to-event pipeline using DAPR actors with a workflow-ready state machine design, v2 adds the query/projection caching pipeline (ETag-based cache invalidation, query actor routing, SignalR real-time notification), DAPR Workflow orchestration, and the Blazor operational dashboard.

**Technology Stack:** .NET 10 LTS, DAPR 1.14+, Aspire 13, C# 14

## Success Criteria

### User Success

**Developer Experience (Jerome persona):**

- **Time to first running system**: A new developer clones the repo, runs `dotnet aspire run`, and has the full EventStore + sample domain service + Blazor dashboard running locally in under 10 minutes -- zero manual infrastructure setup
- **Time to first domain service**: A developer implements and registers a new domain service with EventStore in under 1 hour, following documentation and the sample reference implementation
- **Learning curve**: A .NET developer familiar with DDD concepts understands the programming model -- pure function domain processor, `(Command, CurrentState?) -> List<DomainEvent>`, command/event flow -- within a single working session without prior event sourcing experience
- **Onboarding friction**: A developer reads no more than 3 documentation pages before writing their first working domain service. The "getting started" path is linear, not branching
- **The "aha!" moment**: The first time a domain service registers with EventStore and a command flows through actors to events to subscribers without writing any infrastructure code. This should happen within the first hour of onboarding
- **Zero-confusion infrastructure swap**: Changing state store or message broker backend requires only DAPR component YAML changes -- zero application code modifications, zero domain service changes
- **Deployment simplicity**: Production deployment achievable via Aspire publishers (Docker Compose, Kubernetes, or Azure Container Apps) with a single `aspire publish` command

**Operational Experience (v1 -- without Blazor UI):**

- **Structured log diagnosis**: An operator can trace a stuck command through the full pipeline (received -> processing -> events stored -> events published -> done) using structured logs and OpenTelemetry traces within 5 minutes
- **Dead letter visibility**: Failed commands land on a dedicated dead-letter topic with full command + error context in structured logs. An operator can identify poison commands without database queries
- **Correlation tracing**: Any event can be traced back to its originating command and user request via correlation + causation IDs across OpenTelemetry spans
- **Replay capability**: Failed commands can be replayed manually via the Command API after root cause is fixed

**Operational Experience (v2 -- with Blazor UI, Alex persona):**

- **Mean time to diagnosis**: Support engineer identifies the failure point for a stuck command in under 2 minutes using the Blazor dashboard
- **Self-service resolution**: Common operational tasks (tenant suspension, domain version rollback, dead-letter inspection/replay) completable entirely through the UI without developer involvement

**Query Pipeline Experience (v2):**

- **Time to first cached query**: A developer wires up projection change notification and a query contract, achieving ETag-based caching with HTTP 304 support, in under 30 minutes following documentation
- **Zero-infrastructure caching**: A developer adds query caching to an existing projection without writing any caching infrastructure code -- one `NotifyProjectionChanged` call and one query contract definition
- **Cache effectiveness**: Under steady-state read-heavy workloads, 80%+ of query requests resolve as HTTP 304 at the ETag pre-check gate (single actor call, no query actor activation, no database query)
- **Real-time UI update**: A connected Blazor/browser client receives a SignalR "changed" signal within 1 second of a projection update, triggering a UI refresh without polling

### Business Success

**Project Viability (6 months):**

- At least 2 Hexalith DDD applications running on the EventStore backbone in production
- v1 operational model (logs + traces + dead-letter topics) proven sufficient for daily operations without developer escalation for routine issues
- Architecture validated: no data-loss incidents, no fundamental design rework required
- Event envelope design validated through 3+ distinct domain service implementations before declaring v1 stable

**Open-Source Traction (12 months):**

- GitHub repository with meaningful community engagement -- at minimum: 100+ stars, active issue tracker with real-world usage reports (not just feature requests), and discussions showing developers building on the platform
- NuGet package published with documented getting-started guide and sample projects reaching 500+ downloads
- At least one external contributor or adopter beyond the Hexalith team -- measured by PRs, production usage reports, or detailed issue filings that demonstrate real integration
- Signal of real traction: at least one community member files a bug discovered through production usage (not just evaluation)

**Strategic Position (18+ months):**

- Recognized as a viable option in the .NET event sourcing landscape alongside Marten and EventStoreDB -- measured by mentions in community comparison posts, conference talks, or "which event store should I use?" discussions
- Documented production usage demonstrating DAPR-based infrastructure portability across at least 2 different backend configurations (e.g., Redis + PostgreSQL, or PostgreSQL + Azure Service Bus)

### Technical Success

Performance and resilience targets are summarized here as success criteria; detailed testable specifications are in the **Non-Functional Requirements** section (NFR1-NFR8 for performance, NFR21-NFR26 for reliability).

**Performance KPIs:**

| KPI | Target | Measurement |
|-----|--------|-------------|
| Event append latency (p99) | < 10ms | End-to-end from actor event persist to state store confirmation |
| Actor activation latency (p99) | < 50ms | Cold activation with state rehydration from snapshot + subsequent events |
| Pub/sub delivery latency (p99) | < 50ms | From event persistence to subscriber delivery confirmation |
| Aggregate replay (1000 events) | < 100ms | Full state reconstruction from event stream |
| Command lifecycle (end-to-end) | < 200ms | From REST API receipt to event published on pub/sub |
| System availability | 99.9%+ | With HA DAPR control plane and multi-replica deployment |

**Resilience KPIs (validated by brainstorming chaos scenarios):**

| Scenario | Required Outcome |
|----------|-----------------|
| State store dies mid-command | Command retried, deterministic replay, zero data loss |
| Domain service crashes | Stateless pure function, actor retries via DAPR resiliency policies |
| Actor crashes after store, before publish | Actor state machine resumes from checkpoint, events eventually published |
| Pub/sub unavailable | Events safe in state store, DAPR retry policies drain backlog on recovery |
| Network partition (on-prem to cloud) | Append-only single-writer remains available, cloud replica catches up |
| Saga command storm | Latency degrades, correctness preserved, pull-based backpressure |

**Architectural Validation:**

- Event envelope (11-field metadata) validated as sufficient across 3+ domain service implementations before v1 GA -- this is the irreversible decision that must be right
- Multi-tenant isolation confirmed: commands for tenant A never reach tenant B's domain service across all three isolation layers (actor identity, DAPR policies, command metadata)
- Infrastructure swap validated: identical application code works with Redis (dev) and PostgreSQL (prod) backends with zero code changes
- Zero data-loss across all tested failure scenarios

### Measurable Outcomes

**Developer Experience Scorecard:**

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| Time to running locally | < 10 min | Timed from `git clone` to working system with Aspire |
| Time to first domain service | < 1 hour | Timed from blank project to registered, command-processing service |
| Docs pages to first service | <= 3 pages | Count of required reading before first working service |
| Zero-code infrastructure swap | 0 lines changed | Backend change via DAPR component config only |
| Documentation completeness | 100% of core workflows | Getting started, domain service creation, deployment, operational guide |

**Adoption Funnel:**

| Stage | Target (12 months) | Signal |
|-------|--------------------|----|
| Awareness | 1000+ GitHub visitors | Repository traffic |
| Evaluation | 500+ NuGet downloads | Package adoption |
| First success | 50+ clones with Aspire run | Clone + run pattern |
| Production usage | 3+ applications (internal + external) | Reported production deployments |
| Advocacy | 1+ community blog post or talk | External content creation |

## Product Scope

### MVP - Minimum Viable Product

**v1: The Event Sourcing Pipeline**

The MVP delivers the complete command-to-event pipeline -- the minimum infrastructure needed to build a Hexalith DDD application on EventStore.

**10 Core Features:**

1. **Event Envelope** -- 11-field metadata (aggregate ID, tenant, domain, sequence, timestamp, correlation ID, causation ID, user identity, domain service version, event type name, serialization format) + opaque JSON payload + extension metadata bag
2. **Identity Scheme** -- Canonical `tenant:domain:aggregate-id` format deriving actor IDs, queue sessions, event stream keys, and pub/sub topics from one tuple
3. **Command API Gateway** -- REST endpoints with JWT authentication and claims-based authorization (tenant + domain + command type)
4. **DAPR Actor-Based Aggregate Processing** -- Single-threaded, turn-based concurrency with checkpointed state machine (designed for v2 workflow migration)
5. **Domain Service Integration** -- Pure function processors registered by tenant + domain via DAPR config store
6. **Event Persistence** -- Append-only storage via DAPR state store with composite key strategy and ETag-based optimistic concurrency
7. **Event Distribution** -- DAPR pub/sub with CloudEvents 1.0 envelope, at-least-once delivery, per tenant+domain topics
8. **Snapshot Support** -- Snapshots as special event types, EventStore controls WHEN (every N events), domain controls WHAT
9. **Aspire Orchestration** -- AppHost defining full local dev topology, `dotnet aspire run` for single-command startup, OpenTelemetry out of the box
10. **Sample Domain Service + Testing Patterns** -- Reference implementation with three-tier testing strategy (unit, integration, contract)

**v1 Operational Baseline (No UI):**
- Dead letter handling via DAPR pub/sub dead-letter topic + structured logging
- Command replay via Command API
- Full OpenTelemetry distributed tracing across the pipeline

**MVP Go/No-Go Gates:**
- Full command-to-event pipeline works end-to-end
- Zero data loss in testing across all chaos scenarios
- Multi-tenant isolation confirmed across all three layers
- Infrastructure swap validated (Redis dev, PostgreSQL prod)
- Clone-to-running < 10 minutes via Aspire
- Event envelope validated through 3+ domain service implementations
- At least 1 Hexalith DDD application successfully built on the backbone

### Post-MVP Roadmap

The phased development roadmap (v2 Operational Control Plane, v3 Enterprise Readiness, v4 Ecosystem & Community) is detailed with dependency analysis and value mapping in the **Project Scoping & Phased Development** section below.

## User Journeys

### Journey 1: Marco, the Curious .NET Developer (Primary User -- Success Path)

**Persona:** Marco is a senior .NET developer at a mid-sized fintech company. He's built CRUD applications for years and recently convinced his team to try event sourcing for their new payment processing service. He evaluated EventStoreDB (too much operational overhead for their small team) and Marten (too PostgreSQL-coupled). He's searching for something that fits their existing DAPR-based microservice architecture.

**Opening Scene:** It's 9 PM on a Tuesday. Marco is reading yet another "which event store for .NET?" Reddit thread when someone mentions Hexalith.EventStore -- "native DAPR integration, works with whatever state store you already have." He clicks the GitHub link and sees a README with one command: `dotnet aspire run`. His team already uses Aspire. He thinks: "This can't be that easy."

**Rising Action:** Marco clones the repository and runs the Aspire command. In 7 minutes, he has the EventStore server, a sample "Counter" domain service, and the full OpenTelemetry dashboard running locally. He opens the sample domain service code and sees a single pure function -- `(IncrementCommand, CounterState?) -> [CounterIncremented]` -- no infrastructure code, no event store wiring, no pub/sub configuration. He follows the 3-page getting started guide, creates a new `PaymentService` project, implements a `ProcessPayment` command handler as a pure function, and registers it via DAPR config. He sends a POST to the Command API. In his Aspire dashboard, he watches the OpenTelemetry trace: command received, actor activated, domain service called, events persisted, events published. His jaw drops. Zero infrastructure code.

**Climax:** Marco sends a second command with the same aggregate ID and sees the actor rehydrate state from the first event before calling his domain service with the current state. Event sourcing is working -- he didn't write a single line of event replay logic. He switches the DAPR state store component from Redis to PostgreSQL by changing one YAML file. Everything works identically. He messages his tech lead: "I found it."

**Resolution:** Within a week, Marco has three domain services running on EventStore. His team's payment processing system is event-sourced with full audit trails, and they didn't write any infrastructure code. The DAPR portability means they can run Redis locally and PostgreSQL in production with zero code changes. Marco writes his first GitHub issue -- a real production edge case -- and becomes the project's first external contributor.

**Capabilities Revealed:** Aspire single-command startup, sample domain service reference, 3-page getting started guide, Command API, domain service registration via DAPR config, OpenTelemetry tracing, infrastructure-agnostic state store, pure function programming model.

---

### Journey 2: Marco's Bad Day (Primary User -- Edge Case / Error Recovery)

**Persona:** Same Marco, three months later. His payment processing service is in production on EventStore v1 (no Blazor dashboard yet).

**Opening Scene:** Monday morning. The on-call Slack channel lights up: "Payment commands are timing out for tenant acme-corp." Marco pulls up the Grafana dashboard fed by OpenTelemetry metrics -- command latency for `acme-corp` has spiked to 30 seconds while all other tenants are fine. Something is wrong with one tenant's aggregate processing.

**Rising Action:** Marco searches structured logs filtered by `tenant:acme-corp` and finds a repeating pattern: a `ProcessRefund` command is failing with a domain validation error, but the caller keeps retrying it every 2 seconds. The command targets aggregate `acme-corp:payments:order-8847` -- and because DAPR actors are single-threaded per identity, every retry blocks the entire aggregate's command queue. He checks the dead-letter topic and finds the original failure: the domain service's `ProcessRefund` handler threw because the order was already refunded. The dead letter event contains the full command payload, error message, stack trace, and correlation ID.

**Climax:** Marco traces the correlation ID across OpenTelemetry spans and discovers the retry loop: an upstream saga service isn't checking the dead-letter response and keeps resubmitting. He identifies the root cause in 12 minutes -- well within the 5-minute diagnosis target for simple cases, slightly over for this saga-caused cascade. He stops the upstream saga, and the blocked aggregate immediately drains its pending commands.

**Resolution:** Marco adds idempotency checking to the saga service (the domain service was already idempotent -- it correctly rejected the duplicate refund). He replays the legitimate pending commands that were stuck behind the poison command using the Command API's replay endpoint. All commands process successfully. He files an issue requesting a max-retry or circuit-breaker pattern for saga command storms -- one of the brainstormed chaos scenarios becoming a real improvement.

**Capabilities Revealed:** Multi-tenant isolation (failure in one tenant doesn't affect others at the actor identity level), structured logging with full context, dead-letter topic with command payload + error details, correlation ID tracing across OpenTelemetry spans, Command API replay, actor single-writer concurrency model (intentional design -- prevents data corruption but creates head-of-line blocking).

---

### Journey 3: Alex and the Monday Morning Incident (Operations / Support -- v2)

**Persona:** Alex is a support engineer at a company running three Hexalith DDD applications on EventStore v2. Alex doesn't write code but is responsible for production health and first-response incident management. Alex has access to the Blazor Fluent UI admin dashboard.

**Opening Scene:** Alex arrives Monday morning to find 47 Slack alerts from overnight. The event sourcing system shows elevated error rates for the `inventory` domain across two tenants. Before EventStore v2, this would mean paging a developer to dig through logs. Now, Alex opens the Blazor dashboard.

**Rising Action:** The system health overview shows two tenants -- `warehouse-east` and `warehouse-central` -- with red status indicators on the `inventory` domain. Alex clicks into the dead-letter explorer and sees 200+ failed `AdjustStock` commands, all with the same error: "Domain service version 2.3.1 returned schema validation error." Alex recognizes the pattern -- someone deployed a new domain service version that has a breaking change in its command contract. Alex checks the domain service version management panel and sees that version 2.3.1 was deployed at 2 AM by an automated CI/CD pipeline.

**Climax:** Alex clicks "Rollback to Previous Version" on the domain service version management panel, selecting version 2.2.0 -- the last known good version. The rollback takes effect immediately via DAPR config store update. Alex then selects the 200+ dead-letter commands, clicks "Replay Selected," and watches the event stream time-travel explorer as each command successfully processes through the rolled-back domain service.

**Resolution:** Within 8 minutes of sitting down, Alex has resolved the incident without developer involvement. Alex suspends the CI/CD tenant's automated deployment privilege through the tenant management panel and creates an incident report linking to the specific dead-letter commands, the version rollback, and the replay results. The developers will review the breaking schema change when they arrive, but production is healthy now. Alex's mean time to diagnosis: under 2 minutes. Mean time to resolution: under 8 minutes.

**Capabilities Revealed:** Blazor dashboard system health overview, dead-letter command management UI (inspect/replay), domain service version management with instant rollback, tenant management (suspension), event stream time-travel explorer, self-service operational resolution without developer escalation.

---

### Journey 4: Priya, the DevOps Engineer (Infrastructure / Deployment)

**Persona:** Priya is a DevOps engineer responsible for deploying and maintaining the EventStore infrastructure. She manages environments from local development through staging to production, and she's the person who configures DAPR components, sets up monitoring, and handles infrastructure scaling.

**Opening Scene:** Priya's team has decided to adopt Hexalith.EventStore. Her first task: get it running in their Azure Kubernetes Service cluster. She's used to multi-day infrastructure setup for distributed systems -- Kafka clusters, dedicated EventStoreDB nodes, custom Helm charts. She opens the Hexalith.EventStore repository expecting the same.

**Rising Action:** Priya finds the Aspire AppHost project that defines the entire system topology. For production deployment, she runs `aspire publish --publisher azure-container-apps` and gets a complete deployment manifest. She customizes the DAPR component YAML files: Azure Cosmos DB for the state store (event persistence), Azure Service Bus for pub/sub (event distribution), and Azure App Configuration for the config store (domain service registration). No application code changes -- just DAPR component configuration. She adds her standard OpenTelemetry collector configuration and sees that EventStore already exports traces, metrics, and structured logs in the expected formats.

**Climax:** Priya runs the same Aspire AppHost locally with `dotnet aspire run`, watches everything come up with Redis and in-memory components, then deploys to staging with Azure-backed DAPR components. Both environments work identically. The moment she's been dreading -- the "works on my machine but not in production" debugging session -- never comes. The DAPR abstraction means the application genuinely doesn't know or care which backend it's talking to.

**Resolution:** Priya's production deployment is running within a day -- not the week she budgeted. She sets up a second environment with a different backend configuration (PostgreSQL + RabbitMQ) for their on-premise customers, using the exact same application containers with different DAPR component YAMLs. She documents the infrastructure matrix: 3 environments, 2 backend configurations, zero application code differences. Her infrastructure-as-code repository has DAPR component YAMLs and Aspire publisher configs -- no custom operators, no bespoke Helm charts, no infrastructure-specific application builds.

**Capabilities Revealed:** Aspire publishers, DAPR component configuration for state store / pub/sub / config store, OpenTelemetry export, infrastructure portability, zero-code environment switching.

---

### Journey 5: The Payment Gateway Integration (API Consumer)

**Persona:** An external payment gateway system needs to send commands to a Hexalith DDD application running on EventStore. The integration is built by Sanjay, a backend developer at the payment gateway company, who has no knowledge of EventStore internals -- he just has an API specification.

**Opening Scene:** Sanjay receives API documentation for the Hexalith payment service's Command API. He needs to send `AuthorizePayment` commands and receive confirmation that they were processed. He's built REST integrations before, but event sourcing systems make him nervous -- he's heard they're complex.

**Rising Action:** The Command API is a standard REST endpoint. Sanjay sends a POST with a JSON body containing the command type, tenant ID, aggregate ID, and command payload. He includes a JWT token issued by his company's identity provider -- the token contains claims for the allowed tenant and command types. The API returns a `202 Accepted` with a correlation ID. Sanjay uses the correlation ID to query the command status endpoint and sees the command progress through states: `Received -> Processing -> Completed`. For failed commands, the status includes the error details. No event sourcing concepts leak through the API -- Sanjay doesn't know or care about actors, event streams, or pub/sub topics.

**Climax:** Sanjay's integration test sends 1,000 concurrent `AuthorizePayment` commands across 50 different aggregates. Every command returns `202 Accepted` within the p99 latency target. He checks the status of each -- all `Completed`. He intentionally sends a malformed command and gets a clear `400 Bad Request` with validation details. He sends a command for a tenant his JWT doesn't authorize and gets `403 Forbidden`. The API behaves exactly like any well-designed REST API.

**Resolution:** Sanjay's integration is live within two days. He never needed to understand event sourcing, DAPR, actors, or any EventStore internals. The Command API abstracted all of that behind a clean REST contract with JWT authentication. When he later needs real-time event notifications (rather than polling the status endpoint), he subscribes to the CloudEvents pub/sub topic -- but that's a future enhancement. For now, the REST API does everything he needs.

**Capabilities Revealed:** Command API Gateway REST endpoints, JWT authentication with claims-based authorization (tenant + command type), command status tracking via correlation ID, clean API abstraction hiding event sourcing internals, standard HTTP status codes and error responses, CloudEvents pub/sub for real-time integration (future path).

---

### Journey 6: Jerome's Platform Validation Sprint (Platform Quality Gate)

**Persona:** Jerome is the primary developer and architect of Hexalith.EventStore. Before declaring v1 GA, he must validate the platform's performance, resilience, and event envelope design across multiple domain service implementations.

**Opening Scene:** Jerome has three domain services running on EventStore -- the Counter sample, a payment processing service, and an inventory management service. The core pipeline works. Now he needs to prove the platform meets its success criteria before public release. He opens the Aspire AppHost and prepares his validation suite.

**Rising Action:** Jerome starts with event envelope validation. He reviews the 11-field metadata across all three domain services: Counter uses basic fields, Payments uses correlation + causation chains for saga flows, Inventory uses the extension metadata bag for warehouse-specific context. All three work without envelope changes -- the extension bag absorbed domain-specific needs without schema modification. He runs the automated snapshot verification: snapshot at sequence N plus events N+1..M produces identical state to full replay from sequence 1..M across all three domains.

**Climax:** Jerome executes the chaos scenario suite. He kills the state store mid-command -- the actor's checkpointed state machine resumes from the correct stage, deterministic replay produces the same events, zero data loss. He crashes the pub/sub while commands are processing -- events persist safely in the state store, DAPR retry policies drain the backlog on recovery. He simulates actor rebalancing during load -- consistent hashing and the checkpoint design ensure no commands are lost. He runs 100 concurrent command submissions per second for 10 minutes, monitoring p99 latencies: command submission < 50ms, end-to-end lifecycle < 200ms, event append < 10ms. All targets met. He switches the entire test suite from Redis to PostgreSQL -- all tests pass with zero code changes.

**Resolution:** Jerome's validation spreadsheet is complete: event envelope validated across 3 domain implementations, all 6 chaos scenarios pass with zero data loss, performance KPIs met under sustained load, infrastructure swap confirmed between Redis and PostgreSQL. He tags the repository `v1.0.0-rc1` and publishes the NuGet packages. The platform is proven.

**Capabilities Revealed:** Event envelope validation across multiple domains, snapshot consistency verification, chaos scenario testing (state store crash, pub/sub outage, actor rebalancing), performance benchmarking (p99 latency targets), infrastructure portability validation (Redis to PostgreSQL swap), health and readiness endpoint verification, optimistic concurrency under concurrent load, atomic event write verification, composite key isolation testing, identity scheme derivation validation.

---

### Journey 7: Marco Builds a Read Model (Query / Projection Caching -- v2)

**Persona:** Same Marco, six months in. His payment processing service is running on EventStore v2. His team needs a paginated order history UI with 1M+ orders across multiple tenants. The Blazor Server frontend needs real-time updates when new orders are processed.

**Opening Scene:** Marco's current implementation queries the SQL projection directly on every page request. Under load, the database is hammered by redundant queries for data that hasn't changed. His team lead asks: "Can EventStore cache this?"

**Rising Action:** Marco reads the query pipeline docs and discovers the 3-tier query actor routing model. For the paginated order list, he defines a query contract in the NuGet package with `ProjectionType = "OrderList"`, `QueryType = "GetOrders"`, and serializable filter/paging parameters. He adds one line to his domain service's event handler: `eventStore.NotifyProjectionChanged("OrderList", tenantId)` -- called after the SQL projection is updated. He sends a query through the REST endpoint and watches the trace: query received, ETag actor checked, query actor activated, projection queried, result cached, ETag returned in response header.

**Climax:** Marco sends the same query again with the ETag in `If-None-Match`. The trace shows: query received, ETag actor checked -- match -- HTTP 304 returned. The query actor was never activated. The database was never touched. Under steady state, 90%+ of his requests are 304s resolved by a single sub-millisecond ETag actor call. He wires up the Blazor SignalR client helper: when a new order is processed, the ETag actor regenerates its GUID and broadcasts "changed" to the SignalR group. His Blazor component receives the signal and triggers a refresh -- the user sees the new order appear within seconds, no polling.

**Resolution:** Marco's order history page handles 1M+ orders across tenants with sub-10ms cache hits, zero database load on unchanged data, and real-time UI updates via SignalR. He wrote zero caching infrastructure code -- one `NotifyProjectionChanged` call and one query contract definition. The ETag actor handles invalidation, the query actor handles caching, SignalR handles browser notification. When he accidentally deploys a domain service version that changes JSON serialization order, the checksum-based query actor IDs create duplicate cache entries -- but DAPR's idle timeout garbage-collects the orphans within an hour, and the ETag invalidation ensures fresh data is always served. The footgun stings but doesn't bite.

**Capabilities Revealed:** 3-tier query actor routing, ETag actor cache invalidation (GUID-based), HTTP 304 pre-check at endpoint, query actor as in-memory page cache, SignalR real-time notification, `NotifyProjectionChanged` one-line integration, query contract library, coarse invalidation model, DAPR idle timeout as garbage collection.

---

### Journey Requirements Summary

| Journey | Primary Capabilities Required |
|---------|-------------------------------|
| Marco's First Day (Success Path) | Aspire orchestration, sample domain service, getting started docs, Command API, DAPR config-based service registration, OpenTelemetry, infrastructure-agnostic state store |
| Marco's Bad Day (Edge Case) | Multi-tenant actor isolation, structured logging, dead-letter topics, correlation ID tracing, OpenTelemetry spans, Command API replay |
| Alex's Monday Morning (Operations v2) | Blazor dashboard, dead-letter management UI, domain service version rollback, tenant management, event stream explorer |
| Priya's Deployment (Infrastructure) | Aspire publishers, DAPR component configuration, OpenTelemetry export, infrastructure portability, zero-code environment switching |
| Sanjay's Integration (API Consumer) | Command API Gateway, JWT + claims authorization, command status tracking, correlation IDs, clean REST abstraction, HTTP error responses |
| Jerome's Platform Validation (Quality Gate) | Event envelope validation, snapshot consistency, chaos scenario testing, performance benchmarking, infrastructure swap, health/readiness endpoints, concurrency testing, composite key isolation |
| Marco's Read Model (Query/Projection v2) | 3-tier query actor routing, ETag actor cache invalidation, HTTP 304 pre-check, query actor page cache, SignalR real-time notification, NotifyProjectionChanged API, query contract library |

**Cross-Journey Insights:**

- **The Command API is the universal entry point** -- every external interaction flows through it, making it the most critical surface area for v1
- **Multi-tenant isolation appears in every journey** -- it's not just a technical feature but a user-facing concern across developers, operators, and API consumers
- **DAPR abstraction is the core value proposition** -- Marco, Priya, and Sanjay all benefit from never touching infrastructure code
- **v1 operational model (logs + traces + dead letters) is sufficient** -- Marco's edge case journey proves the v1 approach works, while Alex's journey shows why v2 dashboard is a natural evolution, not a v1 blocker
- **The pure function programming model is the "aha!" moment** -- Marco's climax and resolution both center on the surprise that event sourcing can be this simple
- **The query pipeline extends the zero-infrastructure philosophy to reads** -- Marco's read model journey mirrors his first day: one integration point, zero caching infrastructure code, EventStore owns the full pipeline

## Domain-Specific Requirements

### Event Sourcing Invariants

- **Append-only immutability** -- Events, once persisted, are never modified or deleted. This is the foundational invariant of event sourcing and must be enforced at the storage layer, not just by convention
- **Strict ordering guarantee** -- Events within a single aggregate stream must be strictly ordered by sequence number with no gaps. Cross-aggregate ordering is not guaranteed and must not be relied upon
- **Idempotent event application** -- Replaying the same events must always produce the same state. Domain service pure functions must be deterministic
- **Event envelope irreversibility** -- The 11-field metadata schema is the hardest decision to change post-GA. Any field omitted now requires a breaking migration later; any unnecessary field is permanent cruft

### Data Integrity Constraints

- **Optimistic concurrency via ETags** -- Two concurrent commands against the same aggregate must not both succeed. The second must fail and retry with updated state
- **No partial writes** -- A command produces 0 or N events atomically. Never a subset. DAPR state store transactions enforce this
- **Snapshot consistency** -- A snapshot at sequence N plus events N+1..M must produce the same state as replaying events 1..M from scratch. Snapshots are an optimization, never a source of truth

### Multi-Tenant Isolation Requirements

- **Data path isolation** -- Commands for tenant A must never be routed to tenant B's domain service, even during actor rebalancing or infrastructure failures
- **Storage key isolation** -- Event stream keys must include tenant ID in the composite key, preventing cross-tenant data access even with direct state store queries
- **Pub/sub topic isolation** -- Event distribution uses per-tenant+domain topics, ensuring subscribers only receive events for their authorized tenants

### Operational Domain Patterns

- **Event stream as audit log** -- The event stream IS the audit trail. No separate audit system needed, but this means event metadata must be rich enough for compliance queries (who, when, what, why)
- **Temporal queries via event replay** -- "What was the state at time T?" is answered by replaying events up to T, not by querying a mutable database. This capability must be preserved even after snapshots
- **Dead letter as operational signal** -- Failed commands are not silent failures. The dead-letter topic + structured logging is the v1 operational contract. Every failure must be observable

### Domain-Specific Risks

| Risk | Mitigation |
|------|-----------|
| Event envelope too narrow | Validate through 3+ domain service implementations before v1 GA; include extension metadata bag for unforeseen needs |
| Snapshot divergence from replay | Automated verification in test suite: snapshot + tail events must equal full replay |
| Cross-tenant data leak | Triple-layer isolation (actor identity, DAPR policies, command metadata) with integration tests verifying isolation |
| Unbounded event stream growth | Snapshot strategy limits replay window; hot/cold tiering deferred to v3 |
| DAPR sidecar as single point of failure | DAPR resiliency policies + HA control plane; application never bypasses sidecar |

## Innovation & Novel Patterns

### Detected Innovation Areas

**1. DAPR-Native Event Sourcing (Primary Innovation)**

No existing event store in the .NET ecosystem -- or broadly -- is built natively on DAPR's building block abstraction. EventStoreDB is a purpose-built database. Marten piggybacks on PostgreSQL's JSONB. Axon Server is a custom JVM infrastructure. Hexalith.EventStore takes a fundamentally different approach: it composes DAPR's existing primitives (actors for concurrency, state store for persistence, pub/sub for distribution, config store for registration) into an event sourcing platform. The innovation is architectural -- building an event store from commodity distributed systems primitives rather than purpose-built storage engines.

**2. Infrastructure-Agnostic Event Store (Derived Innovation)**

Because DAPR abstracts the state store, message broker, and config store behind standardized APIs, Hexalith.EventStore works with any DAPR-compatible backend combination. Redis + RabbitMQ for development, PostgreSQL + Azure Service Bus for production, Cosmos DB + Kafka for scale -- with zero application code changes. No other event store offers this level of backend portability. The innovation is that infrastructure decisions become deployment decisions, not architectural decisions.

**3. Platform Model vs. Library/Database Model (Paradigm Innovation)**

Existing event sourcing solutions require applications to own the event store client code. Developers import a library (Marten), connect to a database (EventStoreDB), or embed a framework (Axon). Hexalith inverts this: domain services register with the EventStore platform and implement a pure function contract `(Command, CurrentState?) -> List<DomainEvent>`. The EventStore owns the entire command lifecycle -- routing, concurrency, persistence, distribution. Domain services are stateless, infrastructure-free, and focused purely on business logic. This is closer to a serverless function model than a traditional event sourcing library.

**4. Actor-as-Aggregate with Deliberate Migration Path (Design Innovation)**

Using DAPR virtual actors as 1:1 aggregate proxies is not new in concept, but the deliberate v1-to-v2 migration design is novel: building actors with a checkpointed state machine that maps directly to future DAPR Workflow activities. This "workflow-ready actor" pattern means v1 gets the simplicity of actors while v2 gets workflow orchestration without rewriting the core processing logic.

**5. ETag Actor as Dual-Purpose Cache Invalidation Gateway (Design Innovation)**

Projection cache invalidation in distributed systems typically requires complex pub/sub fanout, distributed cache coherence protocols, or application-level polling. Hexalith.EventStore introduces the ETag actor pattern: a single DAPR virtual actor per `{ProjectionType}-{TenantId}` that serves as both the staleness detection point (GUID-based ETag regenerated on every projection change) and the SignalR broadcast point (notifying connected clients of changes). The innovation is dual: (1) failure is safe by construction -- any actor state loss generates a new GUID, causing refresh rather than serving stale data; (2) the REST endpoint performs an ETag pre-check before activating the query actor, enabling HTTP 304 responses that skip the entire query pipeline for warm clients. This transforms the hot path from two actor calls to one lightweight ETag comparison, bringing standard HTTP caching semantics into an actor-based system.

### Market Context & Competitive Landscape

The .NET event sourcing market is split between:
- **Dedicated databases** (EventStoreDB/KurrentDB) -- powerful but heavy operational footprint
- **Library approaches** (Marten, Eventuous, Equinox) -- lightweight but PostgreSQL-coupled or framework-dependent
- **JVM-centric platforms** (Axon) -- mature but wrong ecosystem

No solution occupies the "DAPR-native, infrastructure-agnostic platform" position. This is a genuine whitespace opportunity, not a crowded market segment. The closest analog is what Dapr itself did for microservice patterns -- standardizing building blocks rather than building custom infrastructure.

### Validation Approach

| Innovation | Validation Method | Success Signal |
|-----------|-------------------|----------------|
| DAPR-native architecture | Build 3+ domain services on the platform | No DAPR limitation forces architectural compromise |
| Infrastructure portability | Run identical tests against Redis, PostgreSQL, and Cosmos DB backends | All tests pass, zero code changes |
| Platform model | External developer builds a domain service without Hexalith team help | Under 1 hour from docs to working service |
| Workflow-ready actors | v2 migration to DAPR Workflows | Actor state machine maps cleanly to workflow activities without data migration |

### Risk Mitigation

| Innovation Risk | Fallback Strategy |
|----------------|-------------------|
| DAPR abstraction leaks (state store limitations, pub/sub semantics differ across backends) | Document supported backend matrix with known limitations; don't promise universal compatibility, promise tested compatibility |
| Platform model too opaque for advanced users | Provide escape hatches: direct event stream access for read models, custom middleware hooks in the command pipeline (v4) |
| DAPR runtime dependency adds operational complexity | Aspire orchestration minimizes local friction; document production DAPR deployment patterns; provide Aspire publisher manifests |
| Actor model doesn't scale for high-throughput aggregates | Benchmark and document throughput ceilings; provide guidance on aggregate design for high-volume scenarios |

## Event Sourcing Server Platform Specific Requirements

### Project-Type Overview

Hexalith.EventStore is a hybrid project type: an **infrastructure server platform** that domain service developers interact with as a **developer tool** through NuGet packages and a REST API, while operating as a **multi-tenant SaaS-like system** for the organizations deploying it. Requirements must address all three facets.

### Technical Architecture Considerations

**Runtime Stack:**
- .NET 10 LTS (November 2025 GA) with C# 14
- DAPR 1.14+ runtime (sidecar model)
- Aspire 13 for orchestration and deployment
- Target frameworks: `net10.0` for all projects

**DAPR Building Block Dependencies:**

| Building Block | Purpose | v1 Required |
|---------------|---------|-------------|
| Actors | Aggregate processing (1:1 actor-to-aggregate) | Yes |
| State Store | Event persistence + actor state + snapshots | Yes |
| Pub/Sub | Event distribution (CloudEvents 1.0) | Yes |
| Configuration | Domain service registration by tenant + domain | Yes |
| Resiliency | Retry policies, circuit breakers, timeouts | Yes |
| Workflows | Aggregate lifecycle orchestration | No (v2) |

### NuGet Package Architecture

**Package Distribution Strategy:**

| Package | Purpose | Consumers |
|---------|---------|-----------|
| `Hexalith.EventStore.Contracts` | Event envelope, command/event types, identity scheme | Domain service developers |
| `Hexalith.EventStore.Client` | Domain service SDK with convention-based fluent API (`AddEventStore`/`UseEventStore`), auto-discovery, and explicit `IDomainProcessor` registration | Domain service developers |
| `Hexalith.EventStore.Server` | Core EventStore server with actor processing pipeline | Platform operators |
| `Hexalith.EventStore.Aspire` | Aspire AppHost integration and service defaults | Both |
| `Hexalith.EventStore.Testing` | Test helpers, in-memory DAPR mocks, assertion utilities | Domain service developers |

**Versioning Strategy:**
- SemVer 2.0 for all packages
- Event envelope changes are MAJOR version bumps (breaking, irreversible)
- Domain service contract changes are MAJOR version bumps
- New features (snapshot policies, new metadata fields in extension bag) are MINOR
- All packages versioned together (monorepo single version)

### Command API Specification

**Endpoints:**

| Method | Path | Purpose | Auth |
|--------|------|---------|------|
| POST | `/api/v1/commands` | Submit a command for processing | JWT (tenant + domain + command type) |
| GET | `/api/v1/commands/{correlationId}/status` | Query command processing status | JWT (tenant) |
| POST | `/api/v1/commands/{correlationId}/replay` | Replay a failed command | JWT (tenant + domain + admin) |
| GET | `/api/v1/health` | Health check (DAPR sidecar + state store + pub/sub) | None |
| GET | `/api/v1/ready` | Readiness check (all dependencies healthy) | None |

**Query API Endpoints (v2):**

| Method | Path | Purpose | Auth |
|--------|------|---------|------|
| POST | `/api/v2/queries` | Submit a query for processing (payload contains query type, filters, paging) | JWT (tenant + domain) |
| GET | `/api/v2/queries/{queryType}/{tenantId}` | Singleton query (no parameters) | JWT (tenant + domain) |
| GET | `/api/v2/queries/{queryType}/{tenantId}/{entityId}` | Entity-specific query | JWT (tenant + domain) |

Query responses include `ETag` header. Clients pass `If-None-Match` with cached ETag to receive HTTP 304 when data is unchanged. Authorization uses the same JWT claims model as the Command API, scoped to tenant + domain (no command-type dimension for queries).

**Command Payload Schema:**

```json
{
  "tenantId": "string (required)",
  "domain": "string (required)",
  "aggregateId": "string (required)",
  "commandType": "string (required, fully qualified type name)",
  "payload": "object (required, opaque JSON)",
  "correlationId": "string (optional, generated if omitted)",
  "causationId": "string (optional, for saga chains)",
  "userId": "string (extracted from JWT claims)"
}
```

**Response Codes:**

| Code | Meaning |
|------|---------|
| 202 Accepted | Command queued for processing |
| 400 Bad Request | Validation failure (missing fields, malformed payload) |
| 401 Unauthorized | Missing or invalid JWT |
| 403 Forbidden | JWT lacks required tenant/domain/command claims |
| 409 Conflict | Optimistic concurrency violation (retry with current state) |
| 503 Service Unavailable | DAPR sidecar or dependent service unhealthy |

### Authentication & Authorization Model

**Six-Layer Defense in Depth:**

1. **JWT Validation** -- Token signature, expiry, issuer verification via ASP.NET Core authentication middleware
2. **Claims Extraction** -- `IClaimsTransformation` enriches principal with tenant + domain + command permissions
3. **Endpoint Authorization** -- `[Authorize]` attribute with policy-based ABAC on Command API controllers
4. **MediatR Pipeline Behavior** -- `AuthorizationBehavior<TCommand>` validates tenant x domain x command type before handler execution
5. **Actor Identity Verification** -- Actor validates command tenant matches actor's tenant partition
6. **DAPR Policy Enforcement** -- DAPR access control policies restrict service-to-service communication by app ID

**Permission Dimensions:**

| Dimension | Source | Enforcement Point |
|-----------|--------|-------------------|
| Tenant | JWT claim `tenant_id` | Layers 2-6 |
| Domain | JWT claim `domains[]` | Layers 3-5 |
| Command Type | JWT claim `commands[]` | Layers 3-4 |
| Admin Operations | JWT claim `role=admin` | Layers 3-4 |

### Data Schemas

**Event Envelope (11-field metadata):**

| Field | Type | Purpose |
|-------|------|---------|
| `aggregateId` | string | Target aggregate identity |
| `tenantId` | string | Tenant isolation key |
| `domain` | string | Domain service namespace |
| `sequenceNumber` | long | Strictly ordered per aggregate stream |
| `timestamp` | DateTimeOffset | Event creation time (server clock) |
| `correlationId` | string | Request-level tracing |
| `causationId` | string | Parent event/command tracing |
| `userId` | string | Authenticated user identity |
| `domainServiceVersion` | string | Version of domain service that produced the event |
| `eventTypeName` | string | Fully qualified event type for deserialization |
| `serializationFormat` | string | Payload encoding (default: JSON) |
| `payload` | byte[] | Opaque serialized event data |
| `extensions` | Dictionary<string, string> | Open metadata bag for domain-specific needs |

**Composite Key Strategy (DAPR State Store):**

| Key Pattern | Purpose |
|-------------|---------|
| `{tenant}:{domain}:{aggregateId}:events:{sequence}` | Individual event storage |
| `{tenant}:{domain}:{aggregateId}:snapshot` | Latest snapshot |
| `{tenant}:{domain}:{aggregateId}:metadata` | Aggregate metadata (version, last sequence) |
| `{tenant}:{domain}:{correlationId}:status` | Command processing status tracking |

**Pub/Sub Topic Naming Convention:**

| Topic Pattern | Purpose |
|---------------|---------|
| `{tenant}:{domain}:events` | Per-tenant-per-domain event distribution topic |
| `{tenant}:{domain}:deadletter` | Per-tenant-per-domain dead-letter topic |

### Implementation Considerations

**Performance Constraints:**
- DAPR sidecar adds ~1-2ms per building block call
- Actor activation requires state rehydration -- snapshot strategy critical for aggregates with 100+ events
- Pub/sub delivery is at-least-once; subscribers must be idempotent
- State store transaction support varies by backend -- PostgreSQL supports multi-key transactions, Redis does not

**Testing Strategy (Three-Tier):**

| Tier | Scope | DAPR Dependency | Speed |
|------|-------|-----------------|-------|
| Unit | Domain service pure functions, event envelope validation | None (in-process) | < 1s |
| Integration | Actor processing pipeline, state store operations, ETag actor logic, query actor caching | DAPR test container | < 30s |
| Contract | End-to-end command lifecycle, multi-tenant isolation, query pipeline with ETag 304 flow, SignalR notification delivery | Full Aspire topology | < 2min |

**Aspire Integration Points:**
- `AppHost` project defines complete local topology (EventStore server, sample domain service, DAPR sidecars, state store, message broker)
- Service defaults project configures OpenTelemetry, health checks, resilience
- Aspire publishers generate deployment manifests for Docker Compose, Kubernetes, Azure Container Apps

## Project Scoping & Phased Development

### MVP Strategy & Philosophy

**MVP Approach: Platform MVP**

Hexalith.EventStore's MVP is a **platform MVP** -- the minimum infrastructure that enables building a DDD application on EventStore. Unlike a feature MVP (smallest useful product) or experience MVP (simplest delightful experience), a platform MVP must deliver a complete pipeline or it delivers nothing. A half-built event sourcing pipeline has zero value -- commands must flow to events, events must persist, events must distribute. There is no useful subset of this pipeline.

**Strategic Rationale:**
- The product brief's vision is "the missing Dapr-native, append-only event store for .NET" -- the MVP must prove this vision works end-to-end
- Jerome is the primary developer -- the MVP must be achievable by a single experienced .NET developer in a focused development period
- The open-source traction goal (100+ stars, 500+ NuGet downloads in 12 months) requires a working, demonstrable system on day one of public release
- The irreversible event envelope decision must be validated before GA -- the MVP scope includes 3+ domain service implementations as a validation gate

**Resource Requirements:**
- Primary developer: 1 senior .NET developer (Jerome) with deep DAPR and DDD expertise
- Infrastructure: DAPR runtime, .NET 10 SDK, Aspire 13 tooling
- Testing environments: Redis (local dev), PostgreSQL (production validation)
- No additional team members required for v1; v2 Blazor dashboard may benefit from frontend contribution

### MVP Feature Set (Phase 1)

**Core User Journeys Supported by v1:**

| Journey | v1 Support | Key Features Required |
|---------|-----------|----------------------|
| Marco's First Day (Developer Success) | Full | Aspire orchestration, sample service, Command API, domain registration |
| Marco's Bad Day (Error Recovery) | Full | Structured logging, dead-letter topics, correlation tracing, Command API replay |
| Priya's Deployment (Infrastructure) | Full | Aspire publishers, DAPR component config, OpenTelemetry |
| Sanjay's Integration (API Consumer) | Full | Command API Gateway, JWT auth, correlation IDs |
| Alex's Monday Morning (Operations) | Deferred to v2 | Requires Blazor dashboard |
| Marco's Read Model (Query/Projection) | Deferred to v2 | Requires query pipeline, ETag actors, SignalR |

**Must-Have Analysis:**

| Capability | Without This... | Manual Alternative? | MVP Verdict |
|-----------|-----------------|---------------------|-------------|
| Event Envelope (11 fields) | Events lack traceability, multi-tenancy, versioning | No | MUST HAVE |
| Identity Scheme (`tenant:domain:id`) | No consistent addressing across actors, stores, topics | No | MUST HAVE |
| Command API Gateway | No entry point for commands | No | MUST HAVE |
| Actor-Based Processing | No concurrency control, no aggregate isolation | No | MUST HAVE |
| Domain Service Integration | EventStore can't process domain logic | No | MUST HAVE |
| Event Persistence | Events are lost | No | MUST HAVE |
| Event Distribution (Pub/Sub) | Subscribers can't react to events | Polling state store (fragile) | MUST HAVE |
| Snapshot Support | Aggregates with many events become slow to load | Tolerable for small event counts | MUST HAVE (performance gate) |
| Aspire Orchestration | Manual multi-service startup | Docker Compose (painful) | MUST HAVE (DX gate) |
| Sample Domain Service | Developers have no reference | Documentation only (insufficient) | MUST HAVE (onboarding gate) |
| Dead Letter Handling | Failed commands disappear silently | Manual log searching | MUST HAVE (operational gate) |
| OpenTelemetry Tracing | No visibility into command lifecycle | printf debugging | MUST HAVE (operational gate) |
| JWT Authentication | No access control on Command API | None (security risk) | MUST HAVE |
| Blazor Dashboard | No UI for operations | Logs + traces (v1 operational model) | DEFER TO v2 |
| DAPR Workflows | No workflow orchestration | Actors with state machine (v1 design) | DEFER TO v2 |
| Saga Support | No multi-aggregate coordination | Manual compensation via domain services | DEFER TO v2 |
| Query Pipeline / Projection Caching | No server-side query caching or cache invalidation | Direct database queries from domain services | DEFER TO v2 |
| SignalR Real-Time Notification | No push notification to UI clients | Client polling or manual refresh | DEFER TO v2 |

### Post-MVP Features

**Phase 2: Operational Control Plane (v2)**

| Feature | Dependency | Value |
|---------|-----------|-------|
| Blazor Fluent UI Dashboard | v1 stable, Blazor Fluent UI V4 | Enables Alex's journey -- self-service operations |
| DAPR Workflow Migration | v1 workflow-ready actor design | Replaces direct actor lifecycle with orchestrated workflows |
| Saga/Process Manager | DAPR Workflows | Multi-aggregate operations with compensation |
| Dead-Letter Management UI | Blazor Dashboard | Visual inspect/fix/replay replacing topic + log approach |
| Domain Service Version Management | Blazor Dashboard + DAPR config | Instant rollback via UI |
| External Authorization Engine | v1 JWT model stable | OpenFGA/OPA extending claims-based model |
| Max Causation Depth Guardrail | Saga/Process Manager | Saga loop prevention via configurable causation chain depth limit |
| Query Pipeline & Projection Caching | v1 stable event distribution | ETag actor-based cache invalidation, 3-tier query actor routing, page cache actors, HTTP 304 pre-check |
| SignalR Real-Time Notification | Query Pipeline | Push-to-browser projection change signals via SignalR hub inside EventStore, DAPR pub/sub as backplane |
| Query Contract Library (NuGet) | Query Pipeline | Typed query metadata (Domain, QueryType, TenantId, ProjectionType, optional EntityId) as single source of routing truth |

**Phase 3: Enterprise Readiness (v3)**

| Feature | Dependency | Value |
|---------|-----------|-------|
| GDPR Crypto-Shredding | Per-tenant encryption key management | Regulatory compliance for EU markets |
| Event Versioning/Migration | Stable event envelope | Schema evolution tooling (EF Migrations-like) |
| Active-Passive Failover | Cloud replica infrastructure | HA for production-critical deployments |
| Hot/Cold Storage Tiering | Snapshot-based archival | Cost optimization for high-volume event streams |
| Advanced Authorization | External auth engine (v2) | Per-command ACLs, state-dependent rules |

**Phase 4: Ecosystem & Community (v4)**

| Feature | Dependency | Value |
|---------|-----------|-------|
| `dotnet new` Templates | Stable domain service contract | One-command domain service scaffolding |
| Domain Service SDK | v1+ stable packages | Testing helpers, assertion utilities |
| Interactive Onboarding Tutorial | Stable platform + docs | Guided first experience |
| gRPC Command API | REST API stable (v1-v3) | High-performance command submission |
| Plugin Architecture | Stable command pipeline | Custom middleware extensibility |

### Risk Mitigation Strategy

**Technical Risks:**

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Event envelope insufficient for real-world domains | Medium | Critical (irreversible) | Validate through 3+ domain implementations before GA; extension metadata bag as escape valve |
| DAPR state store limitations block event sourcing patterns | Medium | High | Test against Redis + PostgreSQL early; document backend compatibility matrix; design around lowest common denominator with backend-specific optimizations |
| Actor rebalancing causes command loss during scaling | Low | Critical | DAPR actor placement uses consistent hashing; checkpointed state machine enables recovery; integration tests simulate rebalancing |
| Performance targets missed (p99 latency) | Medium | Medium | Benchmark early and continuously; snapshot strategy tunable per aggregate; identify DAPR sidecar overhead baseline |

**Market Risks:**

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| .NET event sourcing market too niche for traction | Low | Medium | Target DAPR community (broader than event sourcing community); position as "DAPR-native" not "yet another event store" |
| DAPR dependency scares potential adopters | Medium | Medium | Emphasize Aspire single-command startup; demonstrate zero-config local development; show DAPR as value-add not lock-in |
| Marten or EventStoreDB adds DAPR integration | Low | High | Move fast to establish first-mover position; focus on platform model differentiator (not just DAPR integration) |

**Resource Risks:**

| Risk | Mitigation |
|------|-----------|
| Solo developer bottleneck | v1 scope deliberately sized for single developer; no UI work in v1; leverage DAPR building blocks to avoid building infrastructure |
| Burnout from scope creep | Strict v1 feature freeze after 10 core features defined; defer all "nice-to-have" to v2+ |
| External contributor dependency for v2 | v2 Blazor dashboard is independent from core pipeline; can be contributed separately without blocking v1 operations |

## Functional Requirements

### Command Processing

- FR1: An API consumer can submit a command to the EventStore via a REST endpoint with tenant, domain, aggregate ID, command type, and payload
- FR2: The system can validate a submitted command for structural completeness (required fields: tenantId, domain, aggregateId, commandType, payload; well-formed JSON structure) before routing it for processing
- FR3: The system can route a command to the correct aggregate actor based on the identity scheme (`tenant:domain:aggregate-id`)
- FR4: An API consumer can receive a correlation ID upon command submission for tracking the command lifecycle
- FR5: An API consumer can query the processing status of a previously submitted command using its correlation ID
- FR6: An operator can replay a previously failed command via the Command API after root cause is fixed
- FR7: The system can reject duplicate commands targeting an aggregate that has an optimistic concurrency conflict, returning an appropriate error
- FR8: The system can route failed commands to a dead-letter topic with full command payload, error details, and correlation context
- FR49: The system can detect and reject duplicate commands by tracking processed command IDs per aggregate, returning an idempotent success response for already-processed commands

### Event Management

- FR9: The system can persist events in an append-only, immutable event store where events are never modified or deleted after persistence
- FR10: The system can assign strictly ordered, gapless sequence numbers to events within a single aggregate stream. Cross-aggregate event ordering is explicitly not guaranteed and must not be relied upon by consumers
- FR11: The system can wrap each event in an 11-field metadata envelope (aggregate ID, tenant, domain, sequence, timestamp, correlation ID, causation ID, user identity, domain service version, event type, serialization format) plus opaque payload and extension metadata bag
- FR12: The system can reconstruct aggregate state by replaying all events in an aggregate's stream from sequence 1 to current
- FR13: The system can create snapshots of aggregate state at administrator-configured event count intervals (default: every 100 events, configurable per tenant-domain pair) to optimize state rehydration. The EventStore signals the domain service when a snapshot threshold is reached; the domain service produces the snapshot content inline as part of command processing
- FR14: The system can reconstruct aggregate state from the latest snapshot plus subsequent events, producing identical state to full replay
- FR15: The system can store events using a composite key strategy that includes tenant, domain, and aggregate identity for isolation
- FR16: The system can enforce atomic event writes -- a command produces 0 or N events as a single transaction, never a partial subset

### Event Distribution

- FR17: The system can publish persisted events to subscribers via a pub/sub mechanism using CloudEvents 1.0 envelope format
- FR18: The system can deliver events to subscribers with at-least-once delivery guarantee
- FR19: The system can publish events to per-tenant-per-domain topics, ensuring subscribers only receive events for their authorized scope
- FR20: The system can continue persisting events when the pub/sub system is temporarily unavailable, draining the backlog on recovery

### Domain Service Integration

- FR21: A domain service developer can implement a domain processor as a pure function with the contract `(Command, CurrentState?) -> List<DomainEvent>`
- FR22: A domain service developer can register their domain service with EventStore by tenant and domain via explicit DAPR configuration entry (specifying tenant ID, domain name, and service invocation endpoint) or automatically via convention-based assembly scanning
- FR23: The system can invoke a registered domain service when processing a command, passing the command and current aggregate state
- FR24: The system can process commands for at least 2 independent domains within the same EventStore instance
- FR25: The system can process commands for at least 2 tenants within the same domain, each with isolated event streams

### Identity & Multi-Tenancy

- FR26: The system can derive actor IDs, event stream keys, pub/sub topics, and queue sessions from a single canonical identity tuple (`tenant:domain:aggregate-id`)
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

### Query Pipeline & Projection Caching (v2)

- FR50: The system can route incoming query messages to query actors using a 3-tier routing model: (1) queries with EntityId route to `{QueryType}-{TenantId}-{EntityId}`, (2) queries without EntityId but with non-empty payload route to `{QueryType}-{TenantId}-{Checksum}` where Checksum is a truncated SHA256 base64url hash (11 characters) of the serialized payload, (3) queries without EntityId and with empty payload route to `{QueryType}-{TenantId}`. Note: serialization non-determinism (e.g., JSON key ordering differences) produces different checksums for semantically identical queries, resulting in separate cache actors -- this is an accepted trade-off; callers are responsible for consistent serialization
- FR51: The system can maintain one ETag actor per `{ProjectionType}-{TenantId}` that stores a GUID (base64-22 encoded) representing the current projection version, regenerated on every projection change notification
- FR52: A domain service developer can notify EventStore of a projection change by calling `NotifyProjectionChanged(projectionType, tenantId, entityId?)` via NuGet helper, with the underlying transport (DAPR pub/sub by default, or direct service invocation) selected by configuration
- FR53: The query REST endpoint can perform an ETag pre-check (first gate) by calling the ETag actor before routing to the query actor -- if the client's `If-None-Match` header matches the current ETag, the endpoint returns HTTP 304 without activating the query actor
- FR54: A query actor can serve as an in-memory page cache (second gate) with no state store persistence, comparing its cached ETag against the current ETag actor value on each request and re-querying the projection on mismatch. FR53 is the hot-path optimization; FR54 operates independently when the query actor is activated (e.g., client has no ETag or ETag is stale)
- FR55: The system can broadcast a signal-only "changed" message to connected SignalR clients when a projection's ETag is regenerated, with clients grouped by ETag actor ID (`{ProjectionType}-{TenantId}`)
- FR56: The system can host a SignalR hub inside the EventStore server, using DAPR pub/sub as the backplane for multi-instance SignalR message distribution
- FR57: A query contract library (NuGet) can define mandatory query metadata fields (Domain, QueryType, TenantId, ProjectionType) and optional fields (EntityId) as typed static members, serving as the single source of truth for query routing
- FR58: The system can invalidate all cached query results for a projection+tenant pair on any projection change notification (coarse invalidation model -- all filters invalidated per projection per tenant). Rationale: coarse invalidation trades unnecessary cache refreshes for design simplicity -- fine-grained filter-aware invalidation would require the EventStore to understand projection schemas, violating the platform's opacity principle
- FR59: The SignalR client helper (NuGet) can automatically rejoin SignalR groups on connection recovery, restoring real-time push notification after Blazor Server circuit reconnection, WebSocket drops, or network interruption -- without requiring manual intervention by the developer
- FR60: The EventStore documentation and sample application can provide at least 3 reference patterns for handling the SignalR "changed" signal in Blazor UI components: (1) toast notification prompting manual refresh, (2) automatic silent data reload, (3) selective component refresh targeting only the affected projection

## Non-Functional Requirements

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

### Query Pipeline Performance (v2)

- NFR35: ETag pre-check at the query endpoint (ETag actor call + comparison) must complete within 5ms at p99 for warm ETag actors, enabling HTTP 304 responses without activating the query actor. Cold ETag actor activation (first call after idle timeout) may exceed this target due to DAPR actor placement
- NFR36: Query actor cache hit (ETag match, return cached data) must complete within 10ms at p99
- NFR37: Query actor cache miss (ETag mismatch, re-query projection via domain service, cache result) must complete within 200ms at p99
- NFR38: SignalR "changed" signal delivery from ETag regeneration to connected client receipt must complete within 100ms at p99
- NFR39: The query pipeline must support at least 1,000 concurrent query requests per second per EventStore instance without exceeding latency targets
