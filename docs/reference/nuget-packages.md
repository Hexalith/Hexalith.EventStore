[← Back to Hexalith.EventStore](../../README.md)

# NuGet Packages Guide

Guide to the 5 published Hexalith.EventStore NuGet packages — their purposes, dependency relationships, and which ones to install for your use case. This page is for .NET developers integrating Hexalith.EventStore into their projects.

> **Prerequisites:** [Architecture Overview](../concepts/architecture-overview.md) — you should understand the system topology before choosing packages.

## Package Overview

| Package                       | Description                                                                     | Primary Use Case                                                              | When to Install                                                   |
| ----------------------------- | ------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| Hexalith.EventStore.Contracts | Domain types: commands, events, results, identities                             | Define shared command/event contracts and aggregate identity primitives       | Always required — foundational types for any Hexalith integration |
| Hexalith.EventStore.Client    | Client abstractions, domain processor contract, and DI registration             | Register and activate domain processors with fluent `AddEventStore` setup     | In your domain service project to register domain processors      |
| Hexalith.EventStore.Server    | Server-side domain processors, aggregate actors, DAPR state/pub-sub integration | Host command processing, state rehydration, persistence, and event publishing | In the hosting project that runs the event store server           |
| Hexalith.EventStore.Testing   | In-memory fakes, builders, and test helpers for unit/integration testing        | Build deterministic tests for command/event flows without real infrastructure | In your test projects                                             |
| Hexalith.EventStore.Aspire    | .NET Aspire hosting extensions for DAPR topology orchestration                  | Compose the local distributed topology in an Aspire AppHost                   | In your AppHost project for local development orchestration       |

## Dependency Graph

```mermaid
graph TD
    Contracts[Hexalith.EventStore.Contracts]
    Client[Hexalith.EventStore.Client]
    Server[Hexalith.EventStore.Server]
    Testing[Hexalith.EventStore.Testing]
    Aspire[Hexalith.EventStore.Aspire]

    Client --> Contracts
    Server --> Contracts
    Testing --> Contracts
    Testing --> Server
```

<details>
<summary>Text description of the dependency graph</summary>

- **Contracts** is the root package with no dependencies on other Hexalith packages.
- **Client** depends on Contracts.
- **Server** depends on Contracts.
- **Testing** depends on both Contracts and Server (it provides fake implementations of server-side components for integration testing).
- **Aspire** is fully independent — it has no dependency on any other Hexalith.EventStore package. It only depends on Aspire hosting libraries.

</details>

> **Note:** All 5 packages always ship at the same semantic version. Install matching versions to avoid compatibility issues.

## Which Packages Do I Need?

### Building a domain service

Install **Contracts** + **Client**.

Contracts gives you the domain types (commands, events, results). Client gives you the `AddEventStore` DI registration and the domain processor contract for wiring your domain logic into the event store pipeline.

```bash
$ dotnet add package Hexalith.EventStore.Contracts
$ dotnet add package Hexalith.EventStore.Client
```

### Running the event store server

Install **Contracts** + **Server**.

Server provides the aggregate actors, command routing, and DAPR state/pub-sub integration needed to host the event store processing pipeline.

```bash
$ dotnet add package Hexalith.EventStore.Contracts
$ dotnet add package Hexalith.EventStore.Server
```

### Testing your domain service

Install **Testing** (transitively pulls Contracts + Server).

Testing provides in-memory implementations, fake state stores, and test builders so you can unit-test and integration-test your domain logic without running DAPR.

```bash
$ dotnet add package Hexalith.EventStore.Testing
```

### Local development with Aspire

Install **Aspire** in your AppHost project.

Aspire provides the `AddEventStore` hosting extension for orchestrating the full DAPR topology (event store server, sidecars, state stores, pub/sub) in your local Aspire development environment.

```bash
$ dotnet add package Hexalith.EventStore.Aspire
```

### Full stack (domain service + hosting + testing)

Install packages across your projects based on their role:

| Project                | Packages          |
| ---------------------- | ----------------- |
| Domain service library | Contracts, Client |
| Event store host       | Contracts, Server |
| Test project           | Testing           |
| Aspire AppHost         | Aspire            |

## Package Details

### Hexalith.EventStore.Contracts

Pure domain types — `CommandEnvelope`, `EventEnvelope`, `DomainResult`, identity types. This package has no external dependencies.

**Key namespaces and types:**

- `Hexalith.EventStore.Contracts.Commands` — `CommandEnvelope`, `CommandStatus`, `ArchivedCommand`
- `Hexalith.EventStore.Contracts.Events` — `EventEnvelope`, `EventMetadata`, `IEventPayload`, `IRejectionEvent`
- `Hexalith.EventStore.Contracts.Identity` — `AggregateIdentity`, `IdentityParser`
- `Hexalith.EventStore.Contracts.Results` — `DomainResult`, `DomainServiceWireResult`

```bash
$ dotnet add package Hexalith.EventStore.Contracts
```

### Hexalith.EventStore.Client

DI registration, domain processor abstractions, and the fluent `AddEventStore` extension method with assembly scanning and cascading configuration.

**Key namespaces and types:**

- `Hexalith.EventStore.Client.Registration` — `EventStoreServiceCollectionExtensions`, `EventStoreHostExtensions`
- `Hexalith.EventStore.Client.Handlers` — `IDomainProcessor`, `DomainProcessorBase`
- `Hexalith.EventStore.Client.Discovery` — `AssemblyScanner`, `DiscoveredDomain`
- `Hexalith.EventStore.Client.Conventions` — `NamingConventionEngine`
- `Hexalith.EventStore.Client.Configuration` — `EventStoreOptions`, `EventStoreDomainOptions`

**External dependencies:**

| Package                                   | Version |
| ----------------------------------------- | ------- |
| Dapr.Client                               | 1.16.1  |
| Microsoft.Extensions.Configuration.Binder | 10.0.0  |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.0  |

```bash
$ dotnet add package Hexalith.EventStore.Client
```

### Hexalith.EventStore.Server

Aggregate actors, command routing, event persistence, state rehydration, and DAPR state/pub-sub integration.

**Key namespaces and types:**

- `Hexalith.EventStore.Server.Actors` — `AggregateActor`, `ActorStateMachine`, `IdempotencyChecker`
- `Hexalith.EventStore.Server.Commands` — `CommandRouter`, `DaprCommandStatusStore`, `DaprCommandArchiveStore`
- `Hexalith.EventStore.Server.Events` — `EventPersister`, `EventStreamReader`, `SnapshotManager`, `EventPublisher`
- `Hexalith.EventStore.Server.DomainServices` — `DaprDomainServiceInvoker`, `DomainServiceResolver`
- `Hexalith.EventStore.Server.Configuration` — `ServiceCollectionExtensions`, `SnapshotOptions`

**External dependencies:**

| Package                | Version |
| ---------------------- | ------- |
| Dapr.Client            | 1.16.1  |
| Dapr.Actors            | 1.16.1  |
| Dapr.Actors.AspNetCore | 1.16.1  |
| MediatR                | 14.0.0  |

```bash
$ dotnet add package Hexalith.EventStore.Server
```

### Hexalith.EventStore.Testing

Test helpers, in-memory fakes, and builders for unit and integration testing. Depends on Server (not just Contracts) because it provides fake implementations of server-side components like state stores and test builders.

**Key namespaces and types:**

- `Hexalith.EventStore.Testing.Builders` — `CommandEnvelopeBuilder`, `EventEnvelopeBuilder`, `AggregateIdentityBuilder`
- `Hexalith.EventStore.Testing.Fakes` — `InMemoryStateManager`, `FakeDomainServiceInvoker`, `FakeEventPublisher`
- `Hexalith.EventStore.Testing.Assertions` — `DomainResultAssertions`, `EventEnvelopeAssertions`, `StorageKeyIsolationAssertions`

**External dependencies:**

| Package      | Version |
| ------------ | ------- |
| Shouldly     | 4.3.0   |
| NSubstitute  | 5.3.0   |
| xunit.assert | 2.9.3   |

```bash
$ dotnet add package Hexalith.EventStore.Testing
```

### Hexalith.EventStore.Aspire

.NET Aspire hosting extensions for DAPR topology orchestration. Fully independent — no dependency on any other Hexalith.EventStore package.

**Key namespace and types:**

- `Hexalith.EventStore.Aspire` — `HexalithEventStoreExtensions`, `HexalithEventStoreResources`

**External dependencies:**

| Package                              | Version |
| ------------------------------------ | ------- |
| Aspire.Hosting                       | 13.1.2  |
| CommunityToolkit.Aspire.Hosting.Dapr | 13.0.0  |

```bash
$ dotnet add package Hexalith.EventStore.Aspire
```

## Versioning

All 5 packages use [MinVer](https://github.com/adamralph/minver) for semantic versioning. Versions are derived from git tags with a `v` prefix (e.g., tag `v1.2.0` produces version `1.2.0`).

All package versions are centralized in `Directory.Packages.props` at the repository root. Every package always ships at the same version — there is no mix-and-match between package versions.

Browse all published packages on [NuGet.org](https://www.nuget.org/packages?q=Hexalith.EventStore).

## Next Steps

**Next:** [Command API Reference](command-api.md) — look up REST endpoints with request/response examples

**Related:** [API Reference](api/index.md) — auto-generated type documentation for all public APIs | [Architecture Overview](../concepts/architecture-overview.md) | [First Domain Service](../getting-started/first-domain-service.md) | [Quickstart](../getting-started/quickstart.md)
