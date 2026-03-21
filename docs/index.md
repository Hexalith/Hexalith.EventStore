[← Back to Hexalith.EventStore](../README.md)

# Documentation

This page is for .NET developers integrating Hexalith.EventStore. Documentation is organized by expertise level so you can find what you need without wading through material that's too basic or too advanced.

## Getting Started

*For developers who know DDD and want to get running fast.*

- [Quickstart Guide](getting-started/quickstart.md) — up and running in under 10 minutes
- [Prerequisites](getting-started/prerequisites.md) — required tools and environment setup
- [First Domain Service](getting-started/first-domain-service.md) — building your first aggregate, commands, and events

## Concepts

*For newcomers who want to understand how the system works.*

- [Architecture Overview](concepts/architecture-overview.md) — system topology, DAPR sidecar model, and design decisions
- [Choose the Right Tool](concepts/choose-the-right-tool.md) — when Hexalith is (and isn't) the right fit
- [Command Lifecycle](concepts/command-lifecycle.md) — how a command flows from submission to event persistence
- [Event Envelope](concepts/event-envelope.md) — the 14-field metadata structure wrapping every domain event
- [Event Versioning](concepts/event-versioning.md) — safely evolving event schemas over time
- [Identity Scheme](concepts/identity-scheme.md) — the tenant:domain:aggregate-id addressing model

## Guides

*For developers performing specific operational tasks — deployment, configuration, troubleshooting.*

- [Configuration Reference](guides/configuration-reference.md) — all configurable options and their defaults
- [DAPR Component Reference](guides/dapr-component-reference.md) — state store, pub/sub, and secret store component setup
- [DAPR FAQ](guides/dapr-faq.md) — honest answers about DAPR dependency, risks, and operational costs
- [Deployment Progression](guides/deployment-progression.md) — choosing the right deployment target for your stage
- [Deployment: Azure Container Apps](guides/deployment-azure-container-apps.md) — deploying to ACA with DAPR integration
- [Deployment: Docker Compose](guides/deployment-docker-compose.md) — local and staging deployment with Docker Compose
- [Deployment: Kubernetes](guides/deployment-kubernetes.md) — production Kubernetes deployment with Helm charts
- [Disaster Recovery](guides/disaster-recovery.md) — backup, restore, and failover procedures
- [Security Model](guides/security-model.md) — authentication, authorization, and multi-tenant isolation
- [Troubleshooting](guides/troubleshooting.md) — common issues and their solutions
- [Upgrade Path](guides/upgrade-path.md) — migrating between versions

## Reference

*For developers who need exact API contracts and specifications.*

- [Command API](reference/command-api.md) — command submission, status, replay, and preflight validation
- [Query & Projection API](reference/query-api.md) — query execution, ETag caching, projection notifications, and SignalR
- [NuGet Packages](reference/nuget-packages.md) — package roles, dependencies, and installation guidance
- [Error Reference](reference/problems/index.md) — RFC 9457 problem detail types and HTTP error responses
- [Generated API Reference](reference/api/index.md) — auto-generated public type documentation

## Community

*Ecosystem resources and project direction.*

- [Product Roadmap](community/roadmap.md) — planned features and project direction
- [Awesome Event Sourcing](community/awesome-event-sourcing.md) — curated ecosystem resources

## Next Steps

- **Next:** [Quickstart Guide](getting-started/quickstart.md) — the fastest path to a running event store
- **Related:** [README](../README.md), [Contributing](../CONTRIBUTING.md)
