[← Back to Hexalith.EventStore](../../README.md)

# Product Roadmap

Hexalith.EventStore is in active development and continues to evolve across major releases. This page outlines where the project is headed — what we are building now, what comes next, and what we have already shipped. The roadmap reflects current priorities; it is not a commitment to specific dates. For real-time progress, track [GitHub Issues](https://github.com/Hexalith/Hexalith.EventStore/issues) and [Milestones](https://github.com/Hexalith/Hexalith.EventStore/milestones).

> **Tip:** Want to influence what gets built next? Jump to [How to Influence the Roadmap](#how-to-influence-the-roadmap) or start a conversation in the [Ideas category on GitHub Discussions](https://github.com/Hexalith/Hexalith.EventStore/discussions/categories/ideas).

## Current Focus

These areas are actively being worked on right now:

- **Documentation and developer onboarding** — completing Epic 13 with repository documentation alignment, ensuring all docs, planning artifacts, and code tell the same story
- **Read-model ergonomics** — polishing the developer experience around query endpoints, ETag caching, and real-time projection refresh patterns

## Planned

Upcoming work organized by priority:

### Near-Term

- **Production resource sizing guidance** — capacity planning, infrastructure sizing recommendations, and performance tuning guidance for production deployments

### Future Considerations

These are areas we are exploring but have not yet committed to. Community interest helps us prioritize:

- Event replay and projection rebuild tooling
- Multi-region and geo-distributed deployment patterns
- Event store administration dashboard
- gRPC transport alongside REST
- Event schema registry integration
- Performance benchmarks and tuning guides

## Completed

Key capabilities that have already shipped:

- **Core event sourcing server** — CQRS command processing with DDD aggregate pattern using pure-function `Handle(Command, State?) → DomainResult`, event persistence with atomic writes, snapshots, and state rehydration
- **Command API gateway** — REST endpoints with JWT authentication, FluentValidation, MediatR pipeline, rate limiting, OpenAPI/Swagger, RFC 7807 error responses, and optimistic concurrency
- **Query and projection refresh API** — query execution via `POST /api/v1/queries`, preflight authorization endpoints, ETag-based cache validation, projection invalidation hooks, and projection-changed notifications
- **Real-time projection updates** — optional SignalR hub at `/hubs/projection-changes` plus the `Hexalith.EventStore.SignalR` client helper for automatic reconnect and group rejoin
- **Event distribution** — CloudEvents 1.0 publishing, per-tenant per-domain topic isolation, at-least-once delivery with DAPR retry policies, persist-then-publish resilience, and dead-letter routing
- **Multi-tenant security** — DAPR access control policies, data-path isolation, pub/sub topic isolation, security audit logging, and payload protection
- **Observability and operations** — end-to-end OpenTelemetry tracing, structured logging, health and readiness endpoints, dead-letter-to-origin tracing
- **Sample application and CI/CD** — Counter domain example, DAPR component configurations, integration tests across three tiers, GitHub Actions CI/CD, NuGet publishing, and Aspire deployment manifests
- **Fluent Client SDK API** — convention engine with `[EventStoreDomain]` attribute, assembly scanner with auto-discovery, `AddEventStore()` and `UseEventStore()` extension methods, five-layer cascading configuration
- **Documentation foundation** — README with progressive disclosure, quickstart guide, concept deep dives (architecture, command lifecycle, event envelope, identity scheme), NuGet packages guide, awesome-event-sourcing ecosystem page, community infrastructure (contributing guide, issue and PR templates, GitHub Discussions)
- **DAPR deep-dive FAQ** — comprehensive FAQ addressing DAPR dependency risks, deprecation scenarios, performance characteristics, operational costs, and migration strategies
- **Deployment and operations guides** — Docker Compose, Kubernetes, and Azure Container Apps deployment walkthroughs, deployment progression documentation, DAPR component configuration reference, security model guidance, disaster recovery procedures, troubleshooting guide, and configuration reference

## How to Influence the Roadmap

This roadmap is shaped by community feedback. If a planned feature matters to your project, or if you need something not listed here, we want to hear from you:

- **Share ideas** — start a discussion in the [Ideas category on GitHub Discussions](https://github.com/Hexalith/Hexalith.EventStore/discussions/categories/ideas). Describe your use case and the problem you are trying to solve.
- **Report issues** — file bugs or request specific features through the [Issue Tracker](https://github.com/Hexalith/Hexalith.EventStore/issues). Structured templates help us triage quickly.
- **Contribute directly** — read the [Contributing Guide](../../CONTRIBUTING.md) and pick up an issue. First-time contributors are welcome — look for issues labeled `good first issue`.
- **Vote on priorities** — use reactions on existing issues and discussions to signal what matters most to you. High-signal items get prioritized.

## Versioning and Release Cadence

Hexalith.EventStore uses [Semantic Versioning](https://semver.org/) via [MinVer](https://github.com/adamralph/minver). Releases are triggered by git tags (prefix `v`) and published to [NuGet.org](https://www.nuget.org/packages?q=Hexalith.EventStore).

- **Current releases:** Breaking changes follow documented release notes and upgrade guidance in the [Changelog](../../CHANGELOG.md) and [Upgrade Path](../guides/upgrade-path.md).
- **Forward compatibility goal:** Breaking changes should continue to follow SemVer major version increments with explicit migration guidance.

See the [Changelog](../../CHANGELOG.md) for release history and the [Upgrade Path](../guides/upgrade-path.md) guide for migration between versions.

## Next Steps

**Get involved:** [GitHub Discussions](https://github.com/Hexalith/Hexalith.EventStore/discussions) | [Issue Tracker](https://github.com/Hexalith/Hexalith.EventStore/issues) | [Contributing Guide](../../CONTRIBUTING.md)

**Explore the ecosystem:** [Awesome Event Sourcing](awesome-event-sourcing.md) — curated frameworks, libraries, and learning resources
