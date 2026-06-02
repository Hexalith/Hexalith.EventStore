# Project Overview — Hexalith.EventStore

> **Brownfield documentation** generated for AI-assisted development. Scope: full-repo deep scan.
> Output folder: `docs/brownfield/`. This set is machine-generated and is **separate** from the
> hand-authored documentation site under `docs/` (see `docs/index.md`).

## Purpose

**Hexalith.EventStore** is a **DAPR-native event sourcing server for .NET 10**. It is a distributed,
CQRS- and DDD-ready event sourcing framework that handles command routing, event persistence,
snapshots, query execution, and pub/sub delivery so application teams can focus on domain logic.
Infrastructure (state store, message broker, secret store) is abstracted via DAPR sidecars, so the
backing technology (Redis, PostgreSQL, Cosmos DB, RabbitMQ, Kafka, Azure Service Bus) can be swapped
with **zero application code changes**.

The programming model is a pure function per aggregate:

```
(Command, CurrentState?) -> DomainResult   // success events, rejection events, or no-op
state.Apply(Event)                          // fold events back into state
```

- **Repository:** https://github.com/Hexalith/Hexalith.EventStore
- **License:** MIT
- **Framework:** .NET 10 (SDK `10.0.300`, pinned in `global.json`, roll-forward `latestPatch`)
- **Solution file:** `Hexalith.EventStore.slnx` (modern XML solution format — `.sln` is never used)

## Repository Classification

| Attribute | Value |
|-----------|-------|
| Repository type | **Monorepo / multi-part** (multiple deployable services + libraries + submodules) |
| Project type | **Backend** (event-sourcing server) with a **Blazor** admin/sample UI tier |
| Primary language | **C#** (`net10.0`, `LangVersion=latest`, Nullable + ImplicitUsings enabled) |
| Solution projects | **35 projects** (15 `src/`, 3 `samples/`, 1 `perf/`, 17 `tests/`) + 5 submodule projects |
| Source files | ~900 C# files across `src/` alone |
| Package management | **Centralized** via `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`) |
| Build conventions | `TreatWarningsAsErrors=true`, Allman braces, file-scoped namespaces, `_camelCase` fields |
| Submodules | `Hexalith.Tenants`, `Hexalith.Commons`, `Hexalith.AI.Tools` (root-level only) |

## Tech Stack Summary

| Category | Technology | Version | Role |
|----------|-----------|---------|------|
| Runtime | .NET | `net10.0` (SDK 10.0.300) | Target framework |
| Distributed runtime | DAPR (Client, AspNetCore, Actors, Actors.AspNetCore) | `1.17.9` | State store, pub/sub, actors, service invocation |
| Orchestration | .NET Aspire (`Aspire.Hosting`, Redis, Keycloak, Docker, K8s, Azure AppContainers) | `13.4.0` | Local topology + publish targets |
| DAPR for Aspire | `CommunityToolkit.Aspire.Hosting.Dapr` | `13.4.0-preview` | DAPR sidecar wiring in AppHost |
| Mediation | MediatR | `14.1.0` | CQRS command/query pipeline |
| Validation | FluentValidation (+ DI extensions) | `12.1.1` | Command/query/options validation |
| Auth | `Microsoft.AspNetCore.Authentication.JwtBearer` | `10.0.8` | JWT bearer (Keycloak OIDC or symmetric-key fallback) |
| API docs | `Microsoft.AspNetCore.OpenApi`, `Swashbuckle.AspNetCore.SwaggerUI` | `10.0.8` / `10.2.1` | OpenAPI 3.1 + Swagger UI |
| Real-time | SignalR Client + `SignalR.StackExchangeRedis` | `10.0.8` | Projection-changed notifications + Redis backplane |
| UI | `Microsoft.FluentUI.AspNetCore.Components` (+ Icons) | `5.0.0-rc.3` / `4.14.2` | Blazor admin + sample UI |
| CLI | `System.CommandLine` | `2.0.8` | Admin CLI tool |
| AI integration | `ModelContextProtocol` | `1.3.0` | Admin MCP server (AI-callable tools) |
| Observability | OpenTelemetry (OTLP exporter + ASP.NET/HTTP/runtime instrumentation) | `1.15.x` | Traces, metrics, structured logs |
| Resilience | `Microsoft.Extensions.Http.Resilience`, `ServiceDiscovery` | `10.6.0` | HTTP resilience + service discovery |
| Identifiers | `Hexalith.Commons.UniqueIds` | `2.18.0` | ULID generation |
| Testing | xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0`, bunit `2.7.2`, Playwright `1.60.0`, Testcontainers `4.10.0`, coverlet `10.0.1` | — | Unit → integration → E2E |
| Load testing | NBomber + NBomber.Http | `6.4.1` / `6.2.0` | Throughput/latency perf tests |
| Release | semantic-release (Conventional Commits) | npm `^24.2.3` | Automated versioning + NuGet publish |

## What's in the Box (high-level)

- **Core event store** — command gateway API, aggregate actors, event persistence, snapshots, pub/sub,
  query pipeline with ETag caching, projection notifications, SignalR real-time refresh.
- **Admin suite** — Abstractions/Server/Server.Host (REST), Blazor UI, CLI tool, and an **MCP server**
  exposing operational tools to AI agents (stream inspection, projection control, backup/restore,
  crypto-shredding, consistency checks, dead-letter management, DAPR infra inspection).
- **Multi-tenancy** — built into the contract layer (`TenantId:Domain:AggregateId`) with 4-layer isolation.
- **Aspire AppHost** — full local DAPR topology (event store, admin, tenants, sample, optional Keycloak)
  and publish targets for Docker Compose, Kubernetes, and Azure Container Apps.
- **Samples** — a Counter domain (fluent aggregate + legacy processor) and a Blazor sample UI showing
  three real-time refresh patterns.

## Published NuGet Packages (6)

`Hexalith.EventStore.Contracts`, `.Client`, `.Server`, `.SignalR`, `.Testing`, `.Aspire`
(versioned by semantic-release on merge to `main`).

## Container Images (6)

`eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `sample`, `sample-blazor-ui`, `tenants`
(built via .NET SDK container support — no Dockerfiles; opt-in via `<EnableContainer>true</EnableContainer>`).

## Documentation Map

- [Architecture](./architecture.md) — system topology, the command/query pipelines, actors, event sourcing mechanics
- [Source Tree Analysis](./source-tree-analysis.md) — annotated directory layout
- [API Contracts](./api-contracts.md) — every REST endpoint (gateway + admin)
- [Data Models](./data-models.md) — envelopes, identities, results, storage keys, DAPR components
- [Component Inventory](./component-inventory.md) — Admin UI / sample UI components, CLI commands, MCP tools
- [Development Guide](./development-guide.md) — build, test, run locally
- [Deployment Guide](./deployment-guide.md) — containers, DAPR components, publish targets
- [Integration Architecture](./integration-architecture.md) — how the parts communicate

> **Cross-reference:** The hand-authored docs site (`docs/index.md`) has complementary conceptual and
> operational guides (quickstart, command lifecycle, event versioning, security model, DAPR FAQ,
> deployment progression, RFC 9457 problem catalog). This brownfield set focuses on the *code-derived*
> structure for AI agents.
