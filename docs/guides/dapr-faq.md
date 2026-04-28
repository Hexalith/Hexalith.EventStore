[← Back to Hexalith.EventStore](../../README.md)

# DAPR FAQ Deep Dive

Hexalith.EventStore is a DAPR-native event sourcing server for .NET — this page answers the hard questions about what that DAPR dependency means for you. If you are evaluating Hexalith and DAPR is the thing keeping you up at night, start here. This is the deepest level of DAPR coverage in the documentation — risk assessment, operational costs, performance characteristics, and what-if scenarios.

For background on what DAPR is and why Hexalith uses it, see the [Architecture Overview](../concepts/architecture-overview.md). For how Hexalith compares to alternatives that do not use DAPR, see [Choose the Right Tool](../concepts/choose-the-right-tool.md). This page assumes you have read neither — but it goes deeper than both.

> **Prerequisites:**
>
> - [Choose the Right Tool](../concepts/choose-the-right-tool.md) — Hexalith vs Marten vs EventStoreDB comparison and trade-off analysis
> - [Architecture Overview](../concepts/architecture-overview.md) — system topology, DAPR building blocks, and command flow

## What Is DAPR and Why Does Hexalith Use It?

DAPR (Distributed Application Runtime) is a CNCF-graduated runtime that provides standardized building block APIs — state management, pub/sub, actors, service invocation — backed by pluggable component implementations. Your application talks to DAPR through localhost gRPC; DAPR talks to the actual infrastructure. For a full explanation of DAPR's role in Hexalith's topology, see the [Architecture Overview](../concepts/architecture-overview.md). The short version: DAPR is the reason you can swap from Redis to PostgreSQL to Cosmos DB by changing a YAML file instead of your code.

## What if DAPR Is Deprecated?

> **TL;DR:** Low probability, medium-high impact — but Hexalith's architecture isolates the blast radius to a single NuGet package.

DAPR [graduated from the CNCF](https://www.cncf.io/projects/dapr/) in February 2024. Graduation is the highest CNCF maturity level — it requires demonstrated production adoption, a healthy contributor base, a defined governance model, and a security audit. CNCF graduation does not guarantee immortality, but it provides structural protection: governance is independent of any single company (Microsoft initiated DAPR but does not control it), and the project must meet ongoing community health criteria.

For context, CNCF graduated projects include Kubernetes, Prometheus, Envoy, and Helm. No CNCF graduated project has been deprecated to date. CNCF does have an [archiving process](https://github.com/cncf/toc/blob/main/process/archiving.md) for projects that lose momentum — but archived projects were universally sandbox or incubating stage, not graduated. This does not mean graduation is a lifetime guarantee, but the historical precedent is strong.

**What would deprecation actually mean for Hexalith?**

The honest answer is a little messier than "only one package uses DAPR." Today, the sharp isolation boundary is around **domain code**, not every supporting NuGet package:

- **DAPR-free domain model:** your command types, event types, state types, and `Handle`/`Apply` domain logic do not need DAPR imports
- **DAPR-integrated runtime and tooling:** `Hexalith.EventStore.Server` carries the core runtime dependency, while `Client`, `Testing`, and `Aspire` also include some DAPR-facing integration or hosting references

If DAPR disappeared tomorrow, your domain code would still compile and behave the same. The bulk of the migration work would be replacing the Server package's actor lifecycle, event persistence, snapshot management, pub/sub delivery, and idempotency logic with an alternative runtime, then updating the smaller DAPR-facing seams in hosting and integration helpers.

That replacement is significant — the Server package is not trivial — but it is still _scoped_. You would not need to redesign your domain services, command contracts, event types, or aggregate state model.

**The honest competitive context:** Marten and EventStoreDB do not carry this risk. Marten is a .NET library with no external runtime dependency. EventStoreDB is a dedicated database with its own server. Neither depends on a third-party CNCF project for core functionality. If DAPR dependency risk is a dealbreaker for your project, those are viable alternatives — see [Choose the Right Tool](../concepts/choose-the-right-tool.md) for the full comparison. The trade-off you accept with Hexalith is DAPR runtime coupling in exchange for infrastructure portability across every DAPR-supported backend.

> **See also:** [What If a Better Abstraction Emerges?](#what-if-a-better-abstraction-emerges) for what replacing the Server package would involve.

## How Does DAPR Versioning Affect Hexalith?

> **TL;DR:** DAPR follows SemVer with 5+ years of v1.x backward compatibility. Hexalith pins to a specific SDK version in [`Directory.Packages.props`](../../Directory.Packages.props) and CI verifies on every commit.

DAPR has been on v1.x since February 2021. In over five years of releases, DAPR has maintained backward compatibility within the major version — minor releases add features, patch releases fix bugs, and neither breaks existing API contracts. DAPR's [versioning policy](https://docs.dapr.io/operations/support/support-versioning/) follows semantic versioning: breaking changes are reserved for major version bumps.

Hexalith pins the DAPR SDK version in [`Directory.Packages.props`](../../Directory.Packages.props) — that file is the single source of truth. The CI pipeline tests against the pinned version on every commit. To upgrade DAPR:

1. Bump the version in `Directory.Packages.props` on a feature branch
2. Run the full test suite (Tier 1 unit tests, Tier 2 integration tests with DAPR slim, Tier 3 end-to-end with Aspire)
3. If tests pass, merge. If they fail, investigate the DAPR release notes for breaking behavior

DAPR continues to add new building blocks — Workflow (v1.14), Cryptography (v1.13), Jobs (v1.15) — but Hexalith currently uses only **State Store**, **Pub/Sub**, **Actors**, **Service Invocation**, and **Configuration Store**. New DAPR building blocks do not affect Hexalith unless explicitly adopted. Your sidecar runs the new DAPR version with the same component configuration, and Hexalith uses the same API surface it always has.

**Major version upgrades:** When DAPR eventually ships v2.0 with breaking API changes, Hexalith will ship a corresponding major version with migration guidance. Because DAPR coupling is isolated to the Server package, the blast radius of a DAPR major version upgrade is contained — domain code is unaffected.

> **See also:** [What if DAPR Is Deprecated?](#what-if-dapr-is-deprecated) for the broader risk profile of DAPR dependency.

## What Is the Performance Overhead of DAPR Sidecars?

> **TL;DR:** The sidecar adds a localhost gRPC hop per operation — typically microseconds to low single-digit milliseconds. No network hop. Negligible for most business applications.

Every state store read/write, pub/sub publish, and actor invocation passes through a localhost gRPC call to the DAPR sidecar. This is a same-host, loopback-only call — there is no network hop between your application and the sidecar. The sidecar then communicates with the actual backend (Redis, PostgreSQL, Cosmos DB) over the network.

**What the overhead looks like in practice:**

- **Per-operation latency:** The localhost gRPC hop adds microseconds to low single-digit milliseconds per call. The exact number depends on payload size, serialization format, and host resources. DAPR's own [performance documentation](https://docs.dapr.io/operations/performance-and-scalability/) provides testing methodology and results for various configurations
- **Batch amortization:** Hexalith batches event persistence operations (multiple events in a single state store transaction). The sidecar overhead is incurred once per batch, not once per event
- **gRPC vs HTTP:** Hexalith uses gRPC for sidecar communication, which is the faster of DAPR's two supported protocols. HTTP/1.1 adds more overhead due to text-based headers; gRPC uses HTTP/2 with binary framing
- **No double-hop:** The sidecar runs on the same host as your application. The latency path is: app → localhost gRPC → sidecar → network → backend. Compare with direct database access: app → network → backend. The difference is the localhost gRPC step, not an additional network hop

**When this overhead matters:**

If your application requires sub-millisecond event stream reads/writes and every microsecond counts, direct database access (EventStoreDB with its purpose-built TCP/gRPC protocol) is faster. DAPR's sidecar hop is the price of infrastructure portability — you trade raw latency for the ability to swap backends without code changes.

For most business applications processing commands at human-interaction speeds (API requests, form submissions, workflow transitions), the sidecar overhead is negligible compared to network latency to the actual backend, serialization costs, and business logic execution time.

**Sidecar resource consumption:**

Each DAPR sidecar consumes CPU and memory on the host. DAPR's [production deployment guidance](https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-production/) provides recommended resource limits for Kubernetes deployments. In practice, sidecars are lightweight — but you should account for per-instance overhead in your pod sizing calculations, especially at scale. See [What Are the Operational Costs of Running DAPR?](#what-are-the-operational-costs-of-running-dapr) for detailed resource planning.

> **See also:** [What Are the Operational Costs of Running DAPR?](#what-are-the-operational-costs-of-running-dapr) for sidecar resource sizing and pod planning.

## Can I Use Hexalith Without DAPR?

> **TL;DR:** No. DAPR is a hard runtime dependency. Without it, Hexalith does not function.

DAPR is not optional, not a plugin, and not a nice-to-have. It is the runtime that Hexalith delegates all infrastructure interactions to — state persistence, event publishing, actor lifecycle, service discovery, and configuration resolution. Without a running DAPR sidecar, the Command API Gateway cannot persist events, cannot publish to subscribers, cannot activate actors, and cannot resolve domain services.

**Why not make DAPR optional?**

To make DAPR optional, Hexalith would need to build and maintain its own:

- State store abstraction layer (with pluggable backends for Redis, PostgreSQL, Cosmos DB, etc.)
- Pub/sub integration layer (with pluggable brokers for RabbitMQ, Kafka, Azure Service Bus, etc.)
- Actor framework (with placement, activation, deactivation, and turn-based concurrency)
- Service discovery and invocation layer
- Configuration store abstraction

This is essentially what DAPR _is_. Rebuilding it within Hexalith would mean maintaining a parallel infrastructure runtime — the engineering cost would dwarf the current DAPR dependency, and the result would be a less mature, less tested version of what DAPR already provides as a CNCF-governed project with hundreds of contributors.

**The isolation guarantee:**

While DAPR is a hard _runtime_ dependency, your _domain code_ is DAPR-free. The `Handle`/`Apply` pure functions, command types, event types, and state types do not require DAPR imports. This means:

- Your business logic is portable regardless of what happens to DAPR
- Your tests (Tier 1 unit tests) run without DAPR installed
- Your domain model compiles and works against any future runtime that implements the same processing contract

**If DAPR is a dealbreaker:**

If adopting DAPR is not acceptable for your project — whether due to operational constraints, team expertise, organizational policy, or risk tolerance — there are strong alternatives:

- **[Marten](https://martendb.io/):** In-process .NET library, PostgreSQL only, no external runtime dependency. Zero sidecar overhead
- **[EventStoreDB/KurrentDB](https://docs.kurrent.io/):** Dedicated event database server, no sidecar. Direct client-server communication with purpose-built performance

See [Choose the Right Tool](../concepts/choose-the-right-tool.md) for the full comparison.

> **See also:** [What if DAPR Is Deprecated?](#what-if-dapr-is-deprecated) for what replacing DAPR would involve if you adopt Hexalith today.

## What Are the Operational Costs of Running DAPR?

> **TL;DR:** DAPR adds per-instance sidecar overhead, a placement service for actors, component YAML management, and version coordination. It is not free — but it replaces the operational cost of building your own infrastructure abstraction.

Running DAPR in production means running a sidecar process alongside every application instance plus a small set of system services. Here is what that costs:

**Per-instance sidecar overhead:**

Every Hexalith service instance (Command API Gateway, domain services) gets a DAPR sidecar. Each sidecar consumes CPU and memory. DAPR's [Kubernetes production configuration](https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-production/) provides recommended resource requests and limits. Account for sidecar resources when sizing your pods — each pod needs headroom for both your application and its sidecar.

**DAPR system services:**

- **Placement service (required):** Manages actor assignment for the virtual actor pattern. Hexalith uses actors for aggregate processing, so the placement service must be running. It is a single lightweight process
- **Sentry (optional):** Provides mTLS certificate management for sidecar-to-sidecar communication. Recommended for production, not required for development
- **Operator (Kubernetes only):** Manages DAPR component and configuration resources as Kubernetes CRDs

**Component YAML management:**

Each DAPR building block (state store, pub/sub, configuration store) requires a YAML component definition specifying the backend and connection details. Hexalith pre-configures all component YAML for the quickstart, and the [DAPR Component Configuration Reference](dapr-component-reference.md) documents every configuration option. In production, you manage these YAML files alongside your deployment manifests. Changes to component configuration require sidecar restart (not application restart).

**Version coordination:**

DAPR sidecar versions should be coordinated across all services in a deployment. Running mixed sidecar versions is unsupported and can cause subtle behavior differences. In Kubernetes, the DAPR operator handles sidecar injection with a consistent version. In Docker Compose, you pin the sidecar image version in your compose file.

**Monitoring and debugging:**

DAPR provides a [dashboard](https://docs.dapr.io/reference/cli/dapr-dashboard/) for component-level visibility and integrates with [OpenTelemetry](https://docs.dapr.io/operations/observability/tracing/) for distributed tracing. Hexalith extends this with its own OpenTelemetry trace correlation — see [How Does Hexalith Handle DAPR Sidecar Failures?](#how-does-hexalith-handle-dapr-sidecar-failures) for the debugging story.

**Comparison with alternatives:**

| Operational Cost         | Hexalith (DAPR)                                                                             | Marten                    | EventStoreDB                |
| ------------------------ | ------------------------------------------------------------------------------------------- | ------------------------- | --------------------------- |
| Per-instance overhead    | DAPR sidecar (CPU + memory)                                                                 | Zero (in-process library) | Zero (separate server)      |
| Infrastructure services  | Placement + optional Sentry                                                                 | None                      | Cluster nodes               |
| Configuration management | Component YAML files                                                                        | Connection string         | Server config files         |
| Container requirement    | Usually yes for documented topologies (Docker/K8s/ACA); Dapr slim mode exists for local dev | No                        | No (but recommended)        |
| Version coordination     | Sidecar version across services                                                             | NuGet package version     | Server + client SDK version |

Marten has the lowest operational cost — it is a library that runs inside your process with no additional infrastructure. EventStoreDB requires its own server cluster but no sidecar. DAPR's operational cost is the price of infrastructure portability: you manage sidecars and component configuration in exchange for the ability to swap backends without code changes.

> **See also:** [What Is the Performance Overhead of DAPR Sidecars?](#what-is-the-performance-overhead-of-dapr-sidecars) for runtime performance impact.

## What Are the Backend-Specific Consistency Differences?

> **TL;DR:** Infrastructure portability means portable _code_, not portable _behavior_. Different DAPR state store backends provide different consistency guarantees.

DAPR abstracts the API — you call the same state store methods regardless of backend — but the underlying consistency semantics vary by implementation. Swapping from Redis to PostgreSQL to Cosmos DB changes your consistency profile even though your code stays the same.

For background on the DAPR trade-off model, see the [Choose the Right Tool](../concepts/choose-the-right-tool.md) isolation guarantee section. Here, we go deeper into what consistency differences mean in practice.

**The three backends most commonly used with Hexalith:**

| Backend    | Consistency Model                             | ETag/Optimistic Concurrency | Transaction Support                         |
| ---------- | --------------------------------------------- | --------------------------- | ------------------------------------------- |
| Redis      | Strong (single-instance) / Eventual (cluster) | Yes (WATCH-based)           | Yes (MULTI)                                 |
| PostgreSQL | Strong (ACID)                                 | Yes (version column)        | Yes (SQL transactions)                      |
| Cosmos DB  | Configurable (strong to eventual)             | Yes (ETag-based)            | Yes (transactional batch, single partition) |

**What this means for Hexalith:**

Hexalith uses DAPR's state store for event persistence, snapshots, and actor state. The actor model provides single-threaded command processing per aggregate — so within a single aggregate, you get serial consistency regardless of backend. The consistency differences surface in cross-aggregate scenarios, read-your-writes latency, and multi-key transaction behavior.

- **Redis single-instance:** Strong consistency for development and simple deployments. Redis Cluster introduces eventual consistency for keys on different shards — fine for Hexalith because aggregate state keys are scoped to a single shard via consistent hashing, but snapshot reads across aggregates may see stale data briefly
- **PostgreSQL:** Full ACID semantics. The strongest consistency option. Multi-key transactions (event batch + snapshot in one transaction) are fully atomic
- **Cosmos DB:** Consistency is configurable per-account (strong, bounded staleness, session, consistent prefix, eventual). For Hexalith, **session consistency** is the recommended minimum — it guarantees read-your-writes within a session, which maps well to the actor model where each actor maintains its own session with the sidecar

**Do not assume consistency equivalence across backends.** If your application relies on strong read-your-writes consistency across aggregates, validate that your chosen backend and its DAPR component configuration provide the guarantees you need. DAPR's component specification pages document the consistency behavior for each backend:

- [Redis state store component](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-redis/)
- [PostgreSQL state store component](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v2/)
- [Cosmos DB state store component](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-azure-cosmosdb/)

DAPR supports [many other state store backends](https://docs.dapr.io/reference/components-reference/supported-state-stores/) beyond these three. If you use a different backend, consult the DAPR component specification for its consistency guarantees — do not assume it matches Redis, PostgreSQL, or Cosmos DB behavior.

> **See also:** [DAPR Component Configuration Reference](dapr-component-reference.md) for Hexalith-specific component YAML configurations.

## What If a Better Abstraction Emerges?

> **TL;DR:** Hexalith isolates all DAPR integration in the Server package. Replacing it is significant but scoped — your domain code survives any runtime change.

For background on Hexalith's isolation guarantee, see [Choose the Right Tool](../concepts/choose-the-right-tool.md). Here, we go deeper into what "replacing the Server package" actually means.

The Contracts package and your aggregate/domain code define the programming model — commands, events, domain results, and the `Handle`/`Apply` contract. The higher-level registration and hosting helpers aim to preserve that programming model, but some supporting packages currently carry DAPR-related references.

The Server package contains most of the runtime-specific work:

- **Actor lifecycle:** `AggregateActor` implementation, activation/deactivation, timer management
- **Event persistence:** Event stream writes, sequence number management, batch operations
- **Snapshot management:** Snapshot creation at configurable intervals, state reconstruction from snapshot + tail events
- **Pub/sub delivery:** CloudEvents 1.0 envelope construction, topic routing, dead-letter handling
- **Idempotency:** Causation ID tracking and duplicate command detection
- **Domain service invocation:** DAPR service invocation to resolve and call domain processors

If a superior runtime appeared — whether a next-generation DAPR, a CNCF successor, or an entirely different approach to infrastructure abstraction — the migration path is:

1. **Domain services:** Zero or near-zero changes. Your `Handle`/`Apply` pure functions continue to express the same business logic
2. **Contracts:** Zero changes. Commands, events, and result types are DAPR-free
3. **Server:** Full replacement. Re-implement actor lifecycle, event persistence, snapshot management, pub/sub delivery, idempotency, and domain service invocation against the new runtime
4. **Client:** Likely a compatibility update so the existing registration surface (`AddEventStore()` / `UseEventStore()`) continues to work without DAPR-backed plumbing underneath
5. **Testing:** Tier 1 domain/unit tests keep working; DAPR-aware fakes or integration helpers would need targeted updates
6. **Aspire:** Update orchestration to match the new runtime's hosting model

The Server package replacement is real engineering work — it is not a weekend project. But it is _scoped_ work. The architectural boundary between domain code and infrastructure code means you are replacing the plumbing, not rebuilding the house.

> **See also:** [What if DAPR Is Deprecated?](#what-if-dapr-is-deprecated) for the probability assessment of needing this migration.

## How Does Hexalith Handle DAPR Sidecar Failures?

> **TL;DR:** Hexalith relies on DAPR's built-in retry and health check mechanisms plus Hexalith's own idempotency guarantees for safe recovery. Events are never lost once persisted.

Sidecar failures are a reality in any distributed system. Here is how the failure modes work and what protections exist at each layer.

### Sidecar Health and Restart

- **Development (Aspire):** .NET Aspire monitors sidecar processes and restarts them automatically. If a sidecar crashes, Aspire detects the health check failure and restarts the sidecar, reconnecting it to the application
- **Production (Kubernetes):** Liveness and readiness probes detect sidecar failures. The kubelet restarts the sidecar container, and the DAPR operator re-injects configuration. The application pod remains running while the sidecar restarts
- **DAPR built-in:** The sidecar exposes health endpoints (`/v1.0/healthz`) that DAPR-aware orchestrators use to monitor sidecar health

### The Persist-Then-Publish Atomicity Gap

The most important failure scenario: what happens when the sidecar fails _after_ persisting events to the state store but _before_ publishing them to pub/sub?

**Answer:** Events are safe. They are already persisted in the state store. The aggregate's event stream is intact. What fails is the pub/sub delivery to downstream subscribers.

On sidecar recovery:

1. The DAPR sidecar restarts and reconnects to the state store and pub/sub broker
2. Hexalith's actor reactivates and rehydrates state from the persisted event stream
3. If the actor detects unpublished events (events persisted but not confirmed as published), it re-publishes them
4. Downstream consumers may receive duplicate events during recovery. Hexalith's consumer-side idempotency — causation ID check on every event delivery — handles this. If a consumer has already processed an event (matching causation ID), the duplicate is discarded

This is an at-least-once delivery guarantee, not exactly-once. The combination of persist-first + re-publish-on-recovery + consumer idempotency provides effective exactly-once _processing_ semantics while maintaining the simplicity of at-least-once _delivery_.

### Retry Policies and Circuit Breakers

DAPR provides configurable [retry policies](https://docs.dapr.io/operations/resiliency/resiliency-overview/) and [circuit breakers](https://docs.dapr.io/operations/resiliency/resiliency-overview/) at the component level:

- **Retry policies:** Configure maximum retries, backoff intervals, and timeout thresholds for state store operations, pub/sub publishes, and service invocations
- **Circuit breakers:** Prevent cascading failures by temporarily stopping calls to a failing backend after a threshold of consecutive failures

Hexalith works with DAPR's resiliency configuration — you define policies in your resiliency YAML, and the sidecar enforces them transparently.

### Dead-Letter Routing

When event delivery to a subscriber fails after all retries are exhausted, DAPR routes the event to a dead-letter topic with full context (original event, failure reason, retry count, timestamps). See the [Troubleshooting Guide](troubleshooting.md) for how to investigate and replay dead-lettered events.

### Debugging the Sidecar Chain

When something fails through DAPR, the error propagation path is: **app → sidecar → backend → sidecar → app**. This chain makes stack traces harder to read than direct database calls. Here is where to look:

- **Application logs:** Hexalith logs command processing stages with structured fields (aggregate ID, tenant, causation ID). Check here for the command that triggered the failure
- **Sidecar logs:** DAPR sidecars log component-level operations. In Kubernetes, `kubectl logs <pod> -c daprd` shows the sidecar logs. In Aspire, sidecar logs appear in the dashboard alongside application logs
- **DAPR dashboard:** Provides component-level visibility — which components are loaded, their configuration, and runtime status. Useful for verifying component configuration and connectivity
- **OpenTelemetry traces:** Hexalith provides trace correlation across the entire command pipeline — from HTTP request through actor processing to state store write and pub/sub publish. DAPR's sidecar participates in the trace context, so spans are connected end-to-end. Use your tracing backend (Jaeger, Zipkin, Azure Monitor) to follow a single command through the full chain

For specific error resolutions (sidecar injection failure, state store connection timeout, pub/sub message loss, actor activation conflict), see the [Troubleshooting Guide](troubleshooting.md).

> **See also:** [What Are the Operational Costs of Running DAPR?](#what-are-the-operational-costs-of-running-dapr) for monitoring and operational tooling.

## Risk Assessment Summary

| Risk                              | Likelihood                                 | Impact | Mitigation                                             | Residual Risk                      |
| --------------------------------- | ------------------------------------------ | ------ | ------------------------------------------------------ | ---------------------------------- |
| DAPR deprecated                   | Low (CNCF graduated, 5+ years v1.x)        | High   | Architectural isolation; Server-only dependency        | Medium (Server replacement effort) |
| DAPR breaking change              | Low (SemVer, rare major releases)          | Medium | Pinned version, CI verification, migration guidance    | Low                                |
| Sidecar performance bottleneck    | Low (localhost gRPC, microsecond overhead) | Medium | gRPC protocol, batch operations, direct DB alternative | Low                                |
| Backend consistency mismatch      | Medium (implicit assumption)               | Medium | Documentation, per-backend testing, explicit config    | Low (with testing)                 |
| Sidecar failure during processing | Medium (distributed systems reality)       | Low    | Persist-first, at-least-once delivery, idempotency     | Low                                |
| Operational overhead at scale     | Medium (per-instance sidecar)              | Medium | Resource limits, Kubernetes operator, monitoring       | Low (with planning)                |

## Next Steps

- [Choose the Right Tool](../concepts/choose-the-right-tool.md) — full comparison of Hexalith, Marten, and EventStoreDB
- [Architecture Overview](../concepts/architecture-overview.md) — system topology and DAPR building blocks
- [DAPR Component Configuration Reference](dapr-component-reference.md) — component YAML for every supported backend
- [Troubleshooting Guide](troubleshooting.md) — specific error resolutions and debugging procedures
