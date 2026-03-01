[← Back to Hexalith.EventStore](../../README.md)

# Awesome Event Sourcing

A curated list of event sourcing frameworks, CQRS and DDD libraries, DAPR ecosystem projects, message brokers, learning resources, and complementary tools for .NET developers. Whether you are building your first event-sourced system or evaluating alternatives, this page helps you discover the broader ecosystem and understand where Hexalith.EventStore fits alongside other projects.

> **Tip:** New to event sourcing? Start with the [Quickstart](../getting-started/quickstart.md) to see the system running in minutes.

## Where Hexalith.EventStore Fits

[Hexalith.EventStore](https://github.com/Hexalith/Hexalith.EventStore) is a DAPR-native event sourcing server for .NET built on CQRS, DDD, and event sourcing patterns with .NET Aspire orchestration. It handles command routing, event persistence, snapshots, and pub/sub delivery so you can focus on domain logic. Sweet spot: DAPR-native teams wanting infrastructure-abstracted event sourcing with zero vendor lock-in.

Key differentiators:

- **DAPR sidecar architecture** — swap state stores and message brokers without changing application code
- **Multi-tenant at contract level** — data, topic, and access isolation built in from the ground up
- **Pure-function aggregate pattern** — `Handle(Command, State?) → DomainResult` keeps domain logic testable and side-effect free
- **[.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) local dev topology** — full distributed system running locally with a single `dotnet run`

For a detailed comparison with alternatives, see [Choose the Right Tool](../concepts/choose-the-right-tool.md).

## Event Sourcing Frameworks (.NET and Other)

- **[Marten](https://martendb.io/)** ([GitHub](https://github.com/JasperFx/marten)) — .NET transactional document DB and event store on PostgreSQL (sweet spot: PostgreSQL-native teams)
- **[KurrentDB](https://www.kurrent.io/)** ([GitHub](https://github.com/kurrent-io/KurrentDB)) — purpose-built event-native database, formerly EventStoreDB (sweet spot: dedicated event store infrastructure)
- **[Eventuous](https://eventuous.dev/)** ([GitHub](https://github.com/Eventuous/eventuous)) — lightweight event sourcing library for .NET targeting KurrentDB (sweet spot: minimal-ceremony ES with KurrentDB)
- **[NEventStore](https://github.com/NEventStore/NEventStore)** — persistence-agnostic event store for .NET (sweet spot: pluggable storage backends)
- **[EventFlow](https://geteventflow.net/)** ([GitHub](https://github.com/eventflow/EventFlow)) — async/await CQRS+ES and DDD framework for .NET (sweet spot: highly configurable DDD framework)

### Other Ecosystems

- **[Axon Framework](https://github.com/AxonFramework/AxonFramework)** (JVM) — dominant DDD/CQRS/ES framework on the JVM
- **[Apache Pekko](https://pekko.apache.org/)** — open-source fork of Akka with event sourcing support; JVM/Scala ecosystem

## CQRS & DDD Libraries

- **[MediatR](https://github.com/jbogard/MediatR)** — in-process mediator for commands, queries, and notifications. Note: v13+ uses dual licensing (RPL-1.5/commercial under Lucky Penny Software); evaluate free-tier eligibility for your project.
- **[Wolverine](https://wolverinefx.net/)** ([GitHub](https://github.com/JasperFx/wolverine)) — .NET command bus and message broker from JasperFx; integrates natively with Marten for aggregate-handler CQRS/ES
- **[FluentValidation](https://docs.fluentvalidation.net/)** ([GitHub](https://github.com/FluentValidation/FluentValidation)) — validation library for .NET with fluent API; commonly paired with CQRS command pipelines

## DAPR Ecosystem

- **[Dapr](https://dapr.io/)** ([GitHub](https://github.com/dapr/dapr)) — CNCF-graduated distributed application runtime providing state, pub/sub, actors, and service invocation via sidecar
- **[Dapr .NET SDK](https://github.com/dapr/dotnet-sdk)** — official .NET SDK for Dapr
- **[CommunityToolkit.Aspire.Hosting.Dapr](https://github.com/CommunityToolkit/Aspire)** — .NET Aspire Community Toolkit integration for DAPR sidecar support

## Learning Resources

### Books

- **"Domain-Driven Design"** by Eric Evans (2003) — the "Blue Book"; foundational text for aggregates, bounded contexts, and ubiquitous language
- **"Implementing Domain-Driven Design"** by Vaughn Vernon (2013) — the "Red Book"; practical code-level guidance on DDD with CQRS and event sourcing
- **"Domain-Driven Design Distilled"** by Vaughn Vernon (2016) — accessible intro for teams new to DDD
- **"Versioning in an Event Sourced System"** by Greg Young ([free online](https://leanpub.com/esversioning/read)) — definitive guide to event schema evolution
- **"Introducing EventStorming"** by Alberto Brandolini (Leanpub) — the original EventStorming workshop technique book

### Articles

- Martin Fowler — [Event Sourcing](https://martinfowler.com/eaaDev/EventSourcing.html) — foundational pattern definition
- Martin Fowler — [CQRS](https://martinfowler.com/bliki/CQRS.html) — authoritative CQRS definition
- Martin Fowler — [What do you mean by "Event-Driven"?](https://martinfowler.com/articles/201701-event-driven.html) — disambiguates event notification, event-carried state transfer, event sourcing, and CQRS
- Greg Young — [CQRS Documents](https://cqrs.files.wordpress.com/2010/11/cqrs_documents.pdf) — the original comprehensive CQRS+ES document

### Blogs & Newsletters

- **[event-driven.io](https://event-driven.io/en/)** by Oskar Dudycz — pragmatic, deeply technical articles on event sourcing in .NET
- **[Architecture Weekly](https://www.architecture-weekly.com/)** by Oskar Dudycz — weekly curated software architecture resources

### Reference Repositories

- **[EventSourcing.NetCore](https://github.com/oskardudycz/EventSourcing.NetCore)** by Oskar Dudycz — comprehensive examples and self-paced workshops covering event sourcing in .NET

## Complementary Tools

### Message Brokers & Streaming

Event sourcing systems typically publish events through a message broker. These are commonly paired with the frameworks above.

- **[Apache Kafka](https://kafka.apache.org/)** — distributed event streaming platform; industry standard for high-throughput event pipelines
- **[RabbitMQ](https://www.rabbitmq.com/)** — open-source message broker; commonly used with DAPR pub/sub component
- **[Azure Event Hubs](https://learn.microsoft.com/en-us/azure/event-hubs/)** — managed event streaming service; Kafka-compatible, zero-infrastructure for Azure-native stacks

### Event Modeling & Design

- **[EventStorming](https://www.eventstorming.com/)** — collaborative workshop technique for discovering domain events and processes
- **[Event Modeling](https://eventmodeling.org/)** — blueprint-style method for designing event-sourced information systems

### Testing

- **[Testcontainers for .NET](https://dotnet.testcontainers.org/)** — throwaway Docker containers for integration tests (PostgreSQL, KurrentDB, Redis, etc.)
- **[Verify](https://github.com/VerifyTests/Verify)** — snapshot testing for .NET; useful for verifying event stream shapes and projection outputs
- **[Bogus](https://github.com/bchavez/Bogus)** — realistic fake data generator for .NET; useful for populating test aggregates
- **[Respawn](https://github.com/jbogard/Respawn)** — intelligent database cleanup for integration tests

### Community Channels

- **[Dapr Discord](https://aka.ms/dapr-discord)** — official Dapr community discussions
- **[DDD-CQRS-ES Slack](https://ddd-cqrs-es.slack.com/)** — community for DDD, CQRS, and event sourcing practitioners

## Contributing to This Page

Know a resource we're missing? Contributions are welcome — [open a pull request](https://github.com/Hexalith/Hexalith.EventStore/pulls) to suggest additions.

This page is reviewed quarterly to keep links current and add new projects.

## Next Steps

**Next:** [Architecture Overview](../concepts/architecture-overview.md) — understand the Hexalith.EventStore system topology

**Related:** [Choose the Right Tool](../concepts/choose-the-right-tool.md), [Quickstart](../getting-started/quickstart.md), [NuGet Packages Guide](../reference/nuget-packages.md)
