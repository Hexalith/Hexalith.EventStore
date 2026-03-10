[← Back to Hexalith.EventStore](../../README.md)

# Product Roadmap

Hexalith.EventStore is in active pre-v1.0 development. This page outlines where the project is headed — what we are building now, what comes next, and what we have already shipped. The roadmap reflects current priorities; it is not a commitment to specific dates. For real-time progress, track [GitHub Issues](https://github.com/Hexalith/Hexalith.EventStore/issues) and [Milestones](https://github.com/Hexalith/Hexalith.EventStore/milestones).

> **Tip:** Want to influence what gets built next? Jump to [How to Influence the Roadmap](#how-to-influence-the-roadmap) or start a conversation in the [Ideas category on GitHub Discussions](https://github.com/Hexalith/Hexalith.EventStore/discussions/categories/ideas).

## Current Focus

These areas are actively being worked on right now:

- **Final lifecycle documentation polish** — the roadmap is in review and the remaining Epic 15 work is the DAPR FAQ deep dive, which closes out the current documentation lifecycle track
- **Actor-based authorization and query API** — introducing pluggable authorization validators at the actor level, query contracts and routing, projection actor abstractions, and validation endpoints (Epic 17)

## Planned

Upcoming work organized by priority:

### Near-Term

- **DAPR deep-dive FAQ** — comprehensive FAQ addressing common DAPR integration questions, performance considerations, and operational patterns
- **Deployment walkthroughs and platform setup guidance** — step-by-step Docker Compose, Kubernetes, and Azure Container Apps deployment guides, plus DAPR runtime setup and deployment progression documentation
- **Operational reference gaps** — DAPR component configuration reference, health and readiness endpoint documentation, security model guidance, infrastructure-difference explanations, and production resource sizing guidance

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
- **Event distribution** — CloudEvents 1.0 publishing, per-tenant per-domain topic isolation, at-least-once delivery with DAPR retry policies, persist-then-publish resilience, and dead-letter routing
- **Multi-tenant security** — DAPR access control policies, data-path isolation, pub/sub topic isolation, security audit logging, and payload protection
- **Observability and operations** — end-to-end OpenTelemetry tracing, structured logging, health and readiness endpoints, dead-letter-to-origin tracing
- **Sample application and CI/CD** — Counter domain example, DAPR component configurations, integration tests across three tiers, GitHub Actions CI/CD, NuGet publishing, and Aspire deployment manifests
- **Fluent Client SDK API** — convention engine with `[EventStoreDomain]` attribute, assembly scanner with auto-discovery, `AddEventStore()` and `UseEventStore()` extension methods, five-layer cascading configuration
- **Documentation foundation** — README with progressive disclosure, quickstart guide, concept deep dives (architecture, command lifecycle, event envelope, identity scheme), DAPR trade-offs FAQ intro, NuGet packages guide, awesome-event-sourcing ecosystem page, community infrastructure (contributing guide, issue and PR templates, GitHub Discussions)

## How to Influence the Roadmap

This roadmap is shaped by community feedback. If a planned feature matters to your project, or if you need something not listed here, we want to hear from you:

- **Share ideas** — start a discussion in the [Ideas category on GitHub Discussions](https://github.com/Hexalith/Hexalith.EventStore/discussions/categories/ideas). Describe your use case and the problem you are trying to solve.
- **Report issues** — file bugs or request specific features through the [Issue Tracker](https://github.com/Hexalith/Hexalith.EventStore/issues). Structured templates help us triage quickly.
- **Contribute directly** — read the [Contributing Guide](../../CONTRIBUTING.md) and pick up an issue. First-time contributors are welcome — look for issues labeled `good first issue`.
- **Vote on priorities** — use reactions on existing issues and discussions to signal what matters most to you. High-signal items get prioritized.

## Versioning and Release Cadence

Hexalith.EventStore uses [Semantic Versioning](https://semver.org/) via [MinVer](https://github.com/adamralph/minver). Releases are triggered by git tags (prefix `v`) and published to [NuGet.org](https://www.nuget.org/packages?q=Hexalith.EventStore).

- **Pre-v1.0:** Breaking changes may occur between minor versions. Each release documents migration steps.
- **Post-v1.0:** Breaking changes will follow SemVer major version increments with documented upgrade paths.

See the [Changelog](../../CHANGELOG.md) for release history and the [Upgrade Path](../guides/upgrade-path.md) guide for migration between versions.

## Next Steps

**Get involved:** [GitHub Discussions](https://github.com/Hexalith/Hexalith.EventStore/discussions) | [Issue Tracker](https://github.com/Hexalith/Hexalith.EventStore/issues) | [Contributing Guide](../../CONTRIBUTING.md)

**Explore the ecosystem:** [Awesome Event Sourcing](awesome-event-sourcing.md) — curated frameworks, libraries, and learning resources
