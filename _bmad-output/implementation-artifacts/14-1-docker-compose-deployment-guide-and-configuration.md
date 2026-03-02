# Story 14.1: Docker Compose Deployment Guide & Configuration

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator deploying Hexalith locally,
I want a step-by-step walkthrough for deploying the sample application to Docker Compose,
so that I can run the system on my development machine with a production-like topology.

## Acceptance Criteria

1. `docs/guides/deployment-docker-compose.md` exists with a complete walkthrough: prerequisites, DAPR runtime setup for local Docker (FR57), step-by-step deployment instructions, verification of system health via health/readiness endpoints (FR26)
2. `samples/deploy/docker-compose.yml` exists with a working Docker Compose configuration (OR the guide documents the Aspire publisher approach to generate it)
3. The guide includes an inline Mermaid deployment topology diagram with `<details>` text description (NFR7)
4. The guide explains where event data is physically stored based on the DAPR state store configuration (FR60)
5. The guide includes resource requirements (CPU, memory, storage) for local deployment (FR63)
6. The walkthrough produces a verifiably running system when followed step-by-step (NFR22)
7. The page follows the standard page template with DAPR explained at operational depth

## Tasks / Subtasks

- [x] Task 1: Create `docs/guides/deployment-docker-compose.md` (AC: #1, #3, #4, #5, #6, #7)
  - [x] 1.1 Write page header with back-link, title, intro paragraph, prerequisites blockquote
  - [x] 1.2 Create "What You'll Deploy" section with Mermaid topology diagram (commandapi + sample + DAPR sidecars + Redis/PostgreSQL + Keycloak)
  - [x] 1.3 Add `<details>` text description for the Mermaid diagram (NFR7 accessibility)
  - [x] 1.4 Write "Prerequisites" section: Docker Desktop, .NET 10 SDK, DAPR CLI, DAPR runtime init for Docker
  - [x] 1.5 Write "Generate Docker Compose Output" section documenting `PUBLISH_TARGET=docker aspire publish` command
  - [x] 1.6 Write "DAPR Runtime Setup for Docker" section (FR57) — explain `dapr init` vs `dapr init --slim`, sidecar injection in compose
  - [x] 1.7 Write "Deploy the Application" section with step-by-step: generate compose, configure environment variables, `docker compose up`
  - [x] 1.8 Write "Configure DAPR Components" section — explain state store and pub/sub backend selection, link to `deploy/dapr/` production configs
  - [x] 1.9 Write "Where Is My Data?" section (FR60) — explain physical storage locations per DAPR state store backend (Redis keys, PostgreSQL tables, Cosmos DB containers)
  - [x] 1.10 Write "Verify System Health" section (FR26) — document `/health`, `/alive`, `/ready` endpoints with expected responses
  - [x] 1.11 Write "Send a Test Command" section — verify working system with curl/PowerShell command examples
  - [x] 1.12 Write "Resource Requirements" section (FR63) — CPU, memory, storage estimates for local Docker Compose deployment
  - [x] 1.13 Write "Backend Swap" section — demonstrate switching from Redis to PostgreSQL with zero code changes (FR9)
  - [x] 1.14 Write "Troubleshooting" section — common Docker Compose deployment issues (port conflicts, sidecar timeout, container networking)
  - [x] 1.15 Write "Next Steps" section — links to Kubernetes guide (Story 14-2), deployment progression guide (Story 14-4), DAPR component reference (Story 14-5)
- [x] Task 2: Create or document Docker Compose configuration (AC: #2)
  - [x] 2.1 Document the Aspire publisher approach: `PUBLISH_TARGET=docker aspire publish -o ./publish-output/docker`
  - [x] 2.2 Show the expected generated docker-compose.yaml structure with annotations
  - [x] 2.3 Document how to customize the generated compose file for production use (external auth, production state store)
  - [x] 2.4 If Aspire publisher is insufficient for a standalone demo, create a minimal `samples/deploy/docker-compose.yml` as a reference template
- [x] Task 3: Validation (AC: #6)
  - [x] 3.1 Follow the guide on a clean machine (or fresh Docker environment) to verify it produces a running system
  - [x] 3.2 Verify health endpoints return expected responses
  - [x] 3.3 Verify a test command can be submitted and an event is produced
  - [x] 3.4 Run markdownlint on the new file to ensure CI compliance

## Dev Notes

### Architecture Patterns & Constraints

- **Aspire Publisher is the primary Docker Compose generation path.** The AppHost's `Program.cs` (lines 70-83) configures three publisher targets via `PUBLISH_TARGET` environment variable. For Docker Compose: `PUBLISH_TARGET=docker aspire publish -o ./publish-output/docker` generates `docker-compose.yaml` + `.env` files.
- **DO NOT create a hand-written Docker Compose file as the primary artifact.** The Aspire publisher is the intended path. A hand-written compose file may be provided as a reference/template only if the Aspire output needs manual customization.
- **DAPR sidecar injection:** In Docker Compose (unlike Kubernetes), DAPR sidecars must be explicitly defined as separate containers sharing the network namespace. The Aspire publisher handles this.
- **Health check endpoints** are already implemented in `ServiceDefaults/Extensions.cs`:
  - `/health` — full health (200 Healthy/Degraded, 503 Unhealthy)
  - `/alive` — liveness probe (`"live"` tagged checks only)
  - `/ready` — readiness probe (`"ready"` tagged checks only)
- **DAPR health checks** in `CommandApi/HealthChecks/`:
  - `dapr-sidecar` → Unhealthy failure status
  - `dapr-statestore` → Unhealthy failure status
  - `dapr-pubsub` → Degraded failure status
  - `dapr-configstore` → Degraded failure status
- **Keycloak** runs on port 8180 (avoids conflict with CommandApi on 8080). Realm `hexalith` with client `hexalith-eventstore`. Can be disabled with `EnableKeycloak=false` (falls back to symmetric key auth).
- **Domain service isolation (D4):** The sample domain service has zero infrastructure access — no state store, no pub/sub references. Only receives service invocations from CommandApi's DAPR sidecar.
- **Access control:** Development uses allow-by-default; production uses deny-by-default with mTLS. The `deploy/dapr/accesscontrol.yaml` documents the production pattern.

### DAPR Component Topology

**Local development components** (`src/Hexalith.EventStore.AppHost/DaprComponents/`):
- `statestore.yaml` — Redis state store with actor support
- `pubsub.yaml` — Redis Streams pub/sub with three-layer scoping
- `accesscontrol.yaml` — Allow-by-default for dev
- `configstore.yaml` — Configuration component
- `resiliency.yaml` — Resiliency policies
- `subscription-sample-counter.yaml` — Sample subscription with dead-letter routing

**Production components** (`deploy/dapr/`):
- `statestore-postgresql.yaml` — PostgreSQL state store
- `statestore-cosmosdb.yaml` — Azure Cosmos DB state store
- `pubsub-rabbitmq.yaml` — RabbitMQ pub/sub
- `pubsub-kafka.yaml` — Kafka pub/sub
- `pubsub-servicebus.yaml` — Azure Service Bus pub/sub
- `accesscontrol.yaml` — Deny-by-default with mTLS
- `resiliency.yaml` — Production retry, timeout, circuit breaker

**Key patterns:**
- Composite key: `{tenant}||{domain}||{aggregateId}`
- Topic naming: `{tenant}.{domain}.events`
- Dead-letter: `deadletter.{tenant}.{domain}.events`
- Command status TTL: 24 hours (application-level metadata)

### Configuration Patterns

**CommandApi configuration** (`src/Hexalith.EventStore.CommandApi/appsettings.json`):
- `EventStore:DomainServices:Registrations` — Domain service routing (tenant|domain|version → AppId + MethodName)
- `Authentication:JwtBearer` — JWT auth config (Authority, Audience, Issuer, SigningKey)
- `EventStore:OpenApi:Enabled` — Swagger UI toggle (default: true)
- Global request body size limit: 1 MB via Kestrel

**Environment variable override pattern:**
- `Authentication__JwtBearer__Authority` — OIDC discovery URL
- `Authentication__JwtBearer__SigningKey` — Clear to force OIDC mode
- DAPR component YAMLs use `{env:VARIABLE_NAME|default_value}` for secrets

### Page Template Convention

Follow the established documentation page structure (see `docs/concepts/architecture-overview.md` and `docs/getting-started/quickstart.md`):
1. Back link: `[<- Back to Hexalith.EventStore](../../README.md)`
2. H1 title
3. Opening paragraph explaining what the page covers and who it's for
4. `> **Prerequisites:** [link]` blockquote
5. Content sections with Mermaid diagrams where needed
6. Code blocks with both bash and PowerShell alternatives
7. Notes and tips as blockquotes
8. "Next Steps" section with links to related pages

### Performance Targets (for Resource Requirements section)

- Command submission <50ms p99 (NFR1)
- End-to-end lifecycle <200ms p99 (NFR2)
- DAPR sidecar adds ~1-2ms per building block call (NFR8)
- Default 5-second DAPR sidecar call timeout
- Snapshot every 100 events (manages rehydration to ≤102 reads)

### Port Mappings

- CommandApi: 8080 (REST API)
- Keycloak: 8180 (OIDC provider)
- Redis: 6379 (local dev state store + pub/sub)
- PostgreSQL: 5432 (production state store alternative)
- DAPR sidecar HTTP port: auto-assigned by runtime
- DAPR sidecar gRPC port: auto-assigned by runtime
- Aspire dashboard: auto-assigned (displayed in terminal output)

### Existing Content to Reference (NOT duplicate)

- `deploy/README.md` — Already documents production DAPR component configurations, backend compatibility matrix, per-backend environment variables. Link to this, don't duplicate.
- `docs/getting-started/quickstart.md` — Already covers the Aspire-based local development flow. The Docker Compose guide should be positioned as the "next step" for production-like deployment.
- `docs/concepts/architecture-overview.md` — Already covers the system topology and DAPR building blocks. Link for context, but the Docker Compose guide should show a deployment-specific topology diagram.

### Project Structure Notes

- `docs/guides/` directory exists but is empty (has `.gitkeep`). This is where `deployment-docker-compose.md` goes.
- `samples/deploy/` does NOT exist yet. If a reference compose file is needed, this directory must be created.
- `deploy/` directory already exists with production DAPR components and README.
- Documentation filename must follow NFR26: descriptive, unabbreviated, hyphen-separated (e.g., `deployment-docker-compose.md`).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 7, Story 7.1]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR22, FR26, FR57, FR60, FR63, NFR7, NFR22]
- [Source: _bmad-output/planning-artifacts/architecture.md#D9, D10, D11]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#Deployment sections]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs — Aspire publisher configuration, lines 70-83]
- [Source: src/Hexalith.EventStore.ServiceDefaults/Extensions.cs — Health check endpoints]
- [Source: src/Hexalith.EventStore.CommandApi/HealthChecks/ — DAPR health check implementations]
- [Source: deploy/README.md — Production DAPR component documentation]
- [Source: deploy/dapr/ — Production DAPR component YAML files]
- [Source: src/Hexalith.EventStore.AppHost/DaprComponents/ — Local dev DAPR component YAML files]
- [Source: src/Hexalith.EventStore.CommandApi/appsettings.json — Configuration patterns]
- [Source: docs/getting-started/quickstart.md — Page template reference]
- [Source: docs/concepts/architecture-overview.md — Page template and Mermaid diagram reference]

## Dev Agent Record

### Agent Model Used

(to be filled by dev agent)

### Debug Log References

### Completion Notes List

### File List
