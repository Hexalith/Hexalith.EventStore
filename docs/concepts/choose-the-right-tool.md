[← Back to Hexalith.EventStore](../../README.md)

# Choose the Right Tool

Choosing an event sourcing solution for .NET is a significant architectural decision. This page is a structured comparison of Hexalith.EventStore against Marten (Critter Stack), EventStoreDB (now KurrentDB), and custom implementations — covering infrastructure portability, multi-tenancy, CQRS completeness, deployment models, and honest trade-offs. Use the decision guide at the end to determine which tool fits your project.

## What Is Hexalith.EventStore?

Hexalith.EventStore is a DAPR-native event sourcing server for .NET. Your domain logic follows a pure function contract model — `(Command, CurrentState?) → List<DomainEvent>` — while the runtime handles command routing, event persistence, snapshots, pub/sub delivery, and multi-tenant isolation. Because Hexalith is built on DAPR, you can swap infrastructure backends without changing application code.

Hexalith uses three DAPR building blocks to abstract infrastructure:

- **[State management](https://docs.dapr.io/developing-applications/building-blocks/state-management/)** — persists events and snapshots to any supported store (Redis, PostgreSQL, Cosmos DB, DynamoDB, and others)
- **[Pub/sub](https://docs.dapr.io/developing-applications/building-blocks/pubsub/)** — delivers domain events to subscribers through any supported broker (RabbitMQ, Kafka, Azure Service Bus, and others)
- **[Actors](https://docs.dapr.io/developing-applications/building-blocks/actors/)** — provides turn-based concurrency for aggregate processing with the virtual actor pattern

This means you can swap Redis for PostgreSQL for Cosmos DB by changing a YAML configuration file, not your code.

## Comparison at a Glance

| Dimension | Hexalith | Marten (Critter Stack) | EventStoreDB (KurrentDB) | Custom / DIY |
|-----------|----------|------------------------|--------------------------|--------------|
| **Type** | DAPR-native server | .NET library | Dedicated event database | You build it |
| **License** | MIT | MIT | KLv1 (source-available) | N/A |
| **.NET support** | .NET 10 (pre-release) | .NET 8, .NET 9 | .NET 8, .NET 9, .NET Fx 4.8 | Any |
| **Infrastructure portability** | Any DAPR-supported store/broker | PostgreSQL only | EventStoreDB server only | Chosen database |
| **Multi-tenant isolation** | Built-in (4-layer model) | Manual implementation | Manual implementation | You build it |
| **CQRS framework** | Complete, infrastructure-agnostic | Complete, PostgreSQL-coupled | Storage only — bring your own framework | You build it |
| **Projection system** | Event handlers via pub/sub | Built-in with LINQ support | Built-in subscriptions and projections | You build it |
| **LINQ querying** | Not supported | Full LINQ over events and documents | Not supported | Depends on database |
| **Pub/sub** | Built-in via DAPR (any broker) | Wolverine (Critter Stack) | Built-in subscriptions | You build it |
| **Deployment model** | DAPR sidecar (Docker, K8s, ACA) | In-process library | Dedicated server cluster | Varies |
| **Database lock-in** | None | PostgreSQL | EventStoreDB | Chosen database |
| **Polyglot SDK support** | .NET only | .NET only | 6+ languages | Varies |
| **Community maturity** | Pre-release | Established (years of production use) | Established (10+ years) | N/A |
| **Operational complexity** | Medium (DAPR runtime required) | Low (library, PostgreSQL only) | Medium (dedicated server cluster) | Low to high |

> **Note:** Competitor versions verified as of February 2026: Marten 8.x ([martendb.io](https://martendb.io/)), KurrentDB 26.x ([docs.kurrent.io](https://docs.kurrent.io/)). EventStoreDB was rebranded to KurrentDB in late 2024 — both names are used interchangeably in the community.

## Detailed Comparisons

### Hexalith vs Marten (Critter Stack)

[Marten](https://martendb.io/) is a mature .NET library that uses PostgreSQL as both a document database and an event store. Combined with [Wolverine](https://wolverine.netlify.app/) as the "Critter Stack," it provides a complete CQRS/ES framework with sophisticated LINQ querying, projections, and multi-stream aggregation.

**When Marten is the better choice:**

- You already run PostgreSQL and want zero new infrastructure — Marten is a library, not a server
- You need LINQ querying over events and documents — Marten provides full LINQ support backed by PostgreSQL
- You need a battle-tested production ecosystem today — Marten has years of production deployments
- Your team prefers working with a single database technology — PostgreSQL handles everything

**When Hexalith is the better choice:**

- You need infrastructure portability — Hexalith works with Redis, PostgreSQL, Cosmos DB, DynamoDB, and any other DAPR-supported store, while Marten requires PostgreSQL
- You need built-in multi-tenant isolation — Hexalith provides a 4-layer isolation model (input validation, composite key prefixing, DAPR actor scoping, JWT tenant enforcement), while Marten requires manual tenant implementation
- You want to avoid database lock-in — Hexalith lets you change your storage backend without modifying application code
- You deploy across multiple cloud providers or on-premises — DAPR's infrastructure abstraction means the same code runs anywhere

### Hexalith vs EventStoreDB (KurrentDB)

[EventStoreDB](https://docs.kurrent.io/) (now KurrentDB) is a purpose-built event database with over a decade of production use, native clustering, polyglot SDKs in 6+ languages, and optimized event stream performance. It runs as a dedicated server and is the most mature event sourcing database available.

**When EventStoreDB is the better choice:**

- You need maximum raw event stream performance — EventStoreDB is purpose-built for event storage and retrieval without a sidecar network hop
- You need polyglot SDK support — EventStoreDB offers clients for .NET, Java, Node.js, Go, Rust, and Python
- You need enterprise features like LDAP integration or encryption at rest — EventStoreDB offers these in paid tiers
- You want the most battle-tested solution with the largest community — EventStoreDB has over a decade of production maturity

**When Hexalith is the better choice:**

- You need infrastructure portability — Hexalith stores events on any DAPR-supported backend, while EventStoreDB requires its own dedicated server
- You need a complete CQRS framework — Hexalith includes command routing, validation, domain processing, and pub/sub delivery, while EventStoreDB provides storage only and you build the rest
- You prefer MIT licensing — Hexalith is fully MIT, while EventStoreDB uses the KLv1 license (source-available but not OSI-approved open source)
- You need built-in multi-tenant isolation — Hexalith provides multi-tenancy out of the box, while EventStoreDB requires manual implementation
- You want to avoid running a dedicated database server — Hexalith uses your existing infrastructure through DAPR

> **Note:** KurrentDB raised $12M in December 2024 and released KurrentDB 26.0 with native Kafka connectors, relational sink to PostgreSQL/SQL Server, custom indices, and archiving to AWS/Azure/GCP. The ecosystem continues to grow.

### Hexalith vs Custom Implementation

Building your own event sourcing system gives you complete control over every design decision. For simple proof-of-concept projects or teams with very specific requirements, a custom approach may be the right choice.

**When custom is the better choice:**

- You have minimal event sourcing needs — a simple append-to-table approach may suffice
- You need full control over every implementation detail — no framework overhead or abstractions
- You have no external dependencies to manage — zero learning curve beyond your chosen database
- This is a proof of concept or learning exercise — building it yourself deepens understanding

**When Hexalith is the better choice:**

- You need production-grade features — Hexalith provides command routing, event persistence, snapshots, pub/sub delivery, idempotency, concurrency handling, and dead-letter management built-in, while a custom solution requires you to build and maintain each one
- You need multi-tenant isolation — Hexalith includes a 4-layer multi-tenancy model that would take significant effort to replicate
- You deploy to multiple environments — DAPR infrastructure portability lets you swap backends without changing code, while custom implementations are typically coupled to one database
- You want to reduce long-term maintenance — Hexalith handles the infrastructure plumbing so you can focus on domain logic

## When Hexalith Is Not the Right Choice

Hexalith is not the right tool for every project. Here are specific scenarios where you should consider alternatives:

**Non-.NET stack.** Hexalith is a .NET-only solution. If you work in Java, Node.js, Go, or other languages, consider [EventStoreDB/KurrentDB](https://docs.kurrent.io/) for polyglot SDK support, or [Axon Framework](https://www.axoniq.io/) for JVM ecosystems. Hexalith trades polyglot reach for deep .NET integration with DAPR actors, typed domain results, and .NET Aspire orchestration.

**Sub-millisecond latency requirements.** DAPR introduces a sidecar network hop between your application and the infrastructure backend. If you need the absolute lowest latency for event reads and writes, [EventStoreDB/KurrentDB](https://docs.kurrent.io/) communicates directly with its purpose-built storage engine. Hexalith trades raw latency for infrastructure portability — the sidecar hop is the cost of being able to swap backends.

**No container orchestration available.** Hexalith requires DAPR, which requires a container runtime (Docker, Kubernetes, or Azure Container Apps). If you cannot run containers in your environment, [Marten](https://martendb.io/) is an in-process library that only needs PostgreSQL. Hexalith trades deployment simplicity for infrastructure portability — containers enable the DAPR sidecar that makes backend-swapping possible.

**Already all-in on PostgreSQL.** If your team runs PostgreSQL everywhere and wants zero new infrastructure, [Marten](https://martendb.io/) gives you event sourcing, document storage, and LINQ querying on top of a database you already operate. Hexalith trades single-database simplicity for the ability to use any DAPR-supported store.

**Need LINQ querying over events.** If your application heavily relies on LINQ to query events and projections, [Marten](https://martendb.io/) provides full LINQ support backed by PostgreSQL. Hexalith provides event retrieval by stream but does not support LINQ querying — it trades query flexibility for infrastructure portability.

**Need polyglot SDKs.** If your architecture includes services in multiple programming languages that all need to interact with the event store, [EventStoreDB/KurrentDB](https://docs.kurrent.io/) supports .NET, Java, Node.js, Go, Rust, and Python. Hexalith is .NET-only, trading polyglot reach for a deeper, framework-level .NET integration.

**Team unwilling to adopt DAPR.** DAPR has a learning curve and introduces operational overhead (sidecar management, component configuration, version coordination). If your team does not want to adopt DAPR, both [Marten](https://martendb.io/) and [EventStoreDB/KurrentDB](https://docs.kurrent.io/) operate without it. Hexalith trades the DAPR learning curve for infrastructure portability and standardized building blocks.

**Need battle-tested production maturity today.** Hexalith is pre-release software targeting .NET 10. If you need a production-proven solution with years of community experience, both [Marten](https://martendb.io/) and [EventStoreDB/KurrentDB](https://docs.kurrent.io/) have established track records. Hexalith trades maturity for a modern architecture built on DAPR and .NET Aspire — it is designed for teams willing to adopt early and grow with the framework.

## Decision Guide

Use this numbered question sequence to narrow down which tool fits your project. Start at question 1 and follow the recommendations.

**1. Are you building with .NET?**
No → Consider [EventStoreDB/KurrentDB](https://docs.kurrent.io/) (polyglot SDKs for 6+ languages) or a language-specific solution. Hexalith is .NET-only.

**2. Do you need infrastructure portability — the ability to swap storage backends (Redis, PostgreSQL, Cosmos DB) without changing code?**
Yes → Hexalith is designed for this. Marten requires PostgreSQL, and EventStoreDB requires its own server.

**3. Do you need built-in multi-tenant isolation?**
Yes → Hexalith provides a 4-layer multi-tenancy model out of the box. Marten and EventStoreDB require manual implementation.

**4. Are you already running PostgreSQL and want zero new infrastructure?**
Yes → Consider [Marten](https://martendb.io/). It runs as an in-process library on your existing PostgreSQL database with no additional services needed.

**5. Do you need maximum raw event stream performance with no sidecar overhead?**
Yes → Consider [EventStoreDB/KurrentDB](https://docs.kurrent.io/). It is purpose-built for event storage and retrieval with direct client-server communication.

**6. Do you need polyglot SDK support (services in multiple languages)?**
Yes → Consider [EventStoreDB/KurrentDB](https://docs.kurrent.io/). It provides SDKs for .NET, Java, Node.js, Go, Rust, and Python.

**7. Do you need LINQ querying against events and projections?**
Yes → Consider [Marten](https://martendb.io/). It provides full LINQ support backed by PostgreSQL.

**8. Is this a simple proof of concept or learning exercise?**
Yes → Consider a custom/DIY implementation. Building your own deepens understanding with no framework overhead.

**9. None of the above apply?**
You want a complete CQRS/ES framework for .NET with infrastructure portability and modern deployment patterns → **Hexalith.EventStore** is a strong fit. It handles command routing, event persistence, snapshots, pub/sub delivery, and multi-tenant isolation so you can focus on domain logic. Keep in mind that Hexalith is pre-release — evaluate whether early adoption aligns with your project timeline.

## The DAPR Trade-Off

Hexalith's infrastructure portability comes from [DAPR](https://docs.dapr.io/) (Distributed Application Runtime). Understanding this trade-off is essential to evaluating Hexalith.

**What DAPR building blocks does Hexalith use?**

| Building Block | Purpose in Hexalith | DAPR Documentation |
|----------------|--------------------|--------------------|
| [State management](https://docs.dapr.io/developing-applications/building-blocks/state-management/) | Persist events, snapshots, and command status to any supported store | [State management overview](https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/) |
| [Pub/sub](https://docs.dapr.io/developing-applications/building-blocks/pubsub/) | Deliver domain events to subscribers through any supported broker | [Pub/sub overview](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/) |
| [Actors](https://docs.dapr.io/developing-applications/building-blocks/actors/) | Provide turn-based concurrency for aggregate processing (virtual actor pattern) | [Actors overview](https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/) |

**Why DAPR?** DAPR decouples your application from specific infrastructure products. Instead of writing code against a Redis client or a Kafka producer, you write against DAPR's standard APIs. DAPR's sidecar handles the translation to whichever backend you configure. This means the same Hexalith deployment can use Redis in development, PostgreSQL in staging, and Cosmos DB in production — with zero code changes.

**What trade-offs does DAPR introduce?**

- **Runtime dependency** — DAPR must be installed and running alongside your application. This adds operational overhead compared to an in-process library like Marten.
- **Sidecar network hop** — Every state operation and pub/sub message passes through the DAPR sidecar via localhost gRPC. This adds measurable latency compared to direct database access under load.
- **Learning curve** — Your team needs to understand DAPR component configuration, sidecar lifecycle, and debugging through the sidecar layer.
- **Version coupling** — Hexalith depends on specific DAPR SDK versions. DAPR upgrades may require coordinated updates.

> **Note:** A deeper analysis of DAPR trade-offs, risk assessment, and "what-if" scenarios will be available at `docs/guides/dapr-faq.md` in a future release.

## Next Steps

- **Next:** [Quickstart Guide](../getting-started/quickstart.md) — ready to try Hexalith? Get running in under 10 minutes
- **Related:** [Prerequisites](../getting-started/prerequisites.md), [README](../../README.md), [Architecture Overview](architecture-overview.md)
