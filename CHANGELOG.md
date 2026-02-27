# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

#### Core SDK and Contracts

- `Hexalith.EventStore.Contracts` NuGet package with `EventEnvelope`, `AggregateIdentity`, `CommandEnvelope`, and `DomainResult` core types
- `Hexalith.EventStore.Client` NuGet package with `IDomainProcessor` pure function contract and `DomainProcessorBase<TState>` generic helper
- `Hexalith.EventStore.Testing` NuGet package with `InMemoryStateManager`, `FakeDomainServiceInvoker`, and fluent test builders
- Multi-project solution structure with centralized package management and .NET 10, DAPR 1.16.x, and Aspire 13.1.x
- Canonical multi-tenant identity derivation (`tenant:domain:aggregate-id`) via `AggregateIdentity`
- `CommandStatus` enum tracking the full command lifecycle from Received through Completed or Rejected

#### Command API Gateway

- RESTful command submission endpoint (`POST /api/v1/commands`) with 202 Accepted async processing
- RFC 7807 Problem Details error responses with field-level validation errors
- JWT Bearer authentication with `eventstore:tenant` claims transformation for multi-tenant isolation
- Per-endpoint authorization via MediatR `AuthorizationBehavior`
- Command status tracking endpoint (`GET /api/v1/commands/status/{correlationId}`) with 24-hour TTL
- Command replay endpoint (`POST /api/v1/commands/replay/{correlationId}`) for deterministic reprocessing
- ETag-based optimistic concurrency conflict detection with 409 Conflict responses
- Per-tenant sliding window rate limiting with 429 Too Many Requests enforcement
- OpenAPI 3.1 contract and interactive Swagger UI with Counter domain examples
- MediatR pipeline with logging, validation, and authorization behaviors

#### Command Processing and Event Storage

- Command routing to DAPR actors based on canonical multi-tenant identity
- Idempotent command deduplication via correlation ID caching
- Multi-tenant and multi-domain isolation at the actor level
- Domain service invocation via DAPR service-to-service calls
- Event stream reconstruction from persistent storage with snapshot optimization
- Atomic event persistence with gapless sequence numbering via `IActorStateManager`
- Composite storage key strategy preventing cross-tenant collisions
- Configurable snapshot creation for rehydration performance optimization
- Actor state machine with checkpointed stages and automatic recovery from infrastructure failures

#### Event Distribution

- CloudEvents 1.0 standard event publication via DAPR pub/sub
- Per-tenant per-domain topic isolation (`{tenant}.{domain}.events` pattern)
- At-least-once delivery with exponential backoff retry policies
- Persist-then-publish resilience with reminder-based drain for publication failures
- Dead-letter routing with full command context and failure details

#### Multi-Tenant Security

- DAPR access control policies with deny-by-default service-to-service communication
- Data path isolation verification across actor identity, DAPR policies, and command validation
- Pub/sub topic scoping restricting tenant and service access to event topics
- Security audit logging and payload protection for sensitive data

#### Observability

- End-to-end OpenTelemetry trace instrumentation across the complete command lifecycle
- Structured logging at every pipeline stage with correlation IDs
- Dead-letter-to-origin tracing for failed event investigation
- Health check endpoints (`/health`) with per-component status reporting
- Readiness check endpoints (`/alive`) for Kubernetes probes

#### Sample Application and CI/CD

- Counter domain service sample implementing the pure function programming model
- Local DAPR component configurations for Redis-backed development
- Production DAPR component templates for PostgreSQL, Kafka, and other backends
- Integration test suite with DAPR test containers
- End-to-end contract tests with Aspire topology
- GitHub Actions CI/CD pipeline with automated build, test, and NuGet publishing
- Aspire publisher deployment manifests for container orchestration
- Domain service hot-reload validation for local development

#### Documentation

- Progressive disclosure README with comparison table and architecture diagram
- Local development prerequisites page with .NET SDK, Docker, and DAPR CLI setup
- Decision aid helping developers evaluate Hexalith against alternatives
- Animated GIF demo of the quickstart workflow
- Documentation folder structure and page conventions
- CHANGELOG initialization with complete project history
