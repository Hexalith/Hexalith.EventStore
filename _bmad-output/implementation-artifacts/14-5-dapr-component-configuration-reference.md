# Story 14.5: DAPR Component Configuration Reference

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator configuring Hexalith for their infrastructure,
I want documented examples for configuring each DAPR component per backend,
so that I can set up State Store, Pub/Sub, Actors, Configuration, and Resiliency for my target environment.

## Acceptance Criteria

1. The deployment guides or a dedicated section document all five DAPR building blocks (State Store, Pub/Sub, Actors, Configuration, Resiliency) with configuration examples per backend
2. Each YAML example includes inline comments explaining every field
3. Examples cover at minimum: Redis (local dev), PostgreSQL (alternative), and Azure-managed services
4. The content explains what persistence guarantees each backend provides (FR60)

## Tasks / Subtasks

- [x] Task 1: Create `docs/guides/dapr-component-reference.md` (AC: #1, #2, #3, #4)
  - [x] 1.1 Write page header: back-link `[<- Back to Hexalith.EventStore](../../README.md)`, H1 title "DAPR Component Configuration Reference", intro paragraph explaining this is the comprehensive reference for all DAPR building blocks used by Hexalith.EventStore, and prerequisites blockquote linking to `../getting-started/prerequisites.md` and `deployment-progression.md`
  - [x] 1.2 Write "Overview" section: Explain the five DAPR building blocks used (State Store, Pub/Sub, Actors, Configuration, Resiliency) and how they map to Hexalith architecture decisions D1-D7. Include a Mermaid diagram showing how components interact with the CommandAPI and domain services. Add `<details>` text description for accessibility (NFR7)
  - [x] 1.3 Write "State Store Configuration" section (AC: #1, #2, #3, #4):
    - **Redis (local development):** Complete YAML from `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml` with inline comments explaining every field. Document persistence guarantees: in-memory by default, optional AOF/RDB persistence, ETag-based optimistic concurrency
    - **PostgreSQL (on-premise production):** Complete YAML from `deploy/dapr/statestore-postgresql.yaml` with inline comments. Document persistence guarantees: ACID transactions, WAL-based durability, ETag via row versioning
    - **Azure Cosmos DB (cloud production):** Complete YAML from `deploy/dapr/statestore-cosmosdb.yaml` with inline comments. Document persistence guarantees: multi-region replication, configurable consistency levels (strong/bounded staleness/session/eventual), ETag via `_etag` property
    - Include composite key pattern explanation: `{tenant}:{domain}:{aggregateId}:events:{seq}` (D1)
    - Include `actorStateStore: true` requirement for Actors building block
    - Include component scoping: `scopes: [commandapi]` only (D4)
  - [x] 1.4 Write "Pub/Sub Configuration" section (AC: #1, #2, #3):
    - **Redis Streams (local development):** Complete YAML from `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` with inline comments. Document: consumer group semantics, at-least-once delivery, no native dead-letter (DAPR-managed)
    - **RabbitMQ (on-premise production):** Complete YAML from `deploy/dapr/pubsub-rabbitmq.yaml` with inline comments. Document: durable queues, dead-letter exchanges (DLX), AMQP delivery guarantees
    - **Kafka (on-premise production):** Complete YAML from `deploy/dapr/pubsub-kafka.yaml` with inline comments. Document: consumer group isolation, topic auto-creation, Kafka ACLs as separate layer
    - **Azure Service Bus (cloud production):** Complete YAML from `deploy/dapr/pubsub-servicebus.yaml` with inline comments. Document: topics must be pre-created, RBAC/SAS policies, native dead-letter queues
    - Document the three-layer scoping architecture for ALL pub/sub backends: Layer 1 (component scopes), Layer 2 (publishingScopes), Layer 3 (subscriptionScopes)
    - Document topic naming pattern: `{tenant}.{domain}.events` (D6)
    - Document dead-letter routing: `deadletter.{tenant}.{domain}.events`
    - Document CloudEvents 1.0 compliance for all backends
  - [x] 1.5 Write "Actors Configuration" section (AC: #1):
    - Explain that Actors are NOT a separate DAPR component YAML but enabled via `actorStateStore: true` on the state store component
    - Document actor-specific configuration: idle timeout (60 min default), turn-based concurrency (single-threaded), consistent hashing placement
    - Document actor timer and reminder configuration if applicable
    - Reference the state store section for backend-specific actor state behavior
  - [x] 1.6 Write "Configuration Store" section (AC: #1, #2):
    - **Redis (local development):** Complete YAML from `src/Hexalith.EventStore.AppHost/DaprComponents/configstore.yaml` with inline comments
    - Explain purpose: domain service registration (tenant + domain + version -> service endpoint mapping)
    - Document dynamic configuration updates without restart
    - Document scoping: commandapi and domain services needing configuration
  - [x] 1.7 Write "Resiliency Policies" section (AC: #1, #2):
    - Complete YAML from `deploy/dapr/resiliency.yaml` with inline comments explaining every policy
    - Document retry policies: defaultRetry, pubsubRetryOutbound, pubsubRetryInbound (with effective retry counts per backend)
    - Document timeouts: daprSidecar (5s), pubsubTimeout, subscriberTimeout
    - Document circuit breakers: defaultBreaker, pubsubBreaker (thresholds, trip duration)
    - Document targets: apps (commandapi) and components (pubsub, statestore)
    - Explain interaction between component built-in retries and DAPR resiliency retries
  - [x] 1.8 Write "Access Control" section:
    - Complete YAML from `deploy/dapr/accesscontrol.yaml` with inline comments
    - Document deny-by-default security posture (D4)
    - Document SPIFFE trust domain for mTLS identity validation
    - Document how to add new domain services with template
    - Note: ACA does NOT support accesscontrol.yaml — uses component scoping instead
  - [x] 1.9 Write "Declarative Subscriptions" section:
    - Complete YAML from `deploy/dapr/subscription-sample-counter.yaml` with inline comments
    - Document subscription pattern: topic -> route -> dead-letter topic
    - Document scoping per subscriber app-id
  - [x] 1.10 Write "Persistence Guarantees by Backend" section (AC: #4, FR60):
    - Create a comprehensive comparison table covering ALL state store backends:
      - Columns: Backend, Durability, Consistency, ETag Support, Transaction Support, Where Data Lives, Backup Strategy
      - Redis: in-memory (AOF/RDB optional), eventual, ETag via SET NX, multi-key transactions, key-value store, RDB snapshots
      - PostgreSQL: WAL-based, ACID strong, ETag via row version, full transaction support, relational table `state`, pg_dump/WAL archiving
      - Azure Cosmos DB: multi-region, configurable (5 levels), ETag via `_etag`, cross-partition limited, JSON documents, Azure Backup
    - Create a similar table for pub/sub backends:
      - Redis Streams: in-memory, consumer groups, DAPR-managed dead-letter
      - RabbitMQ: durable queues, DLX, message persistence optional
      - Kafka: commit log, consumer group offset, topic retention
      - Azure Service Bus: guaranteed delivery, native dead-letter queue, geo-replication
  - [x] 1.11 Write "Backend Swap Procedure" section:
    - Step-by-step instructions: Stop services -> Swap component YAML -> Update env vars -> Restart services -> Verify
    - Emphasize zero code changes required (only YAML and env vars change)
    - Link to `deploy/README.md` for per-backend environment variable reference
  - [x] 1.12 Write "Environment Variable Reference" section:
    - Table of all env vars per backend from `deploy/README.md`:
      - REDIS_HOST, POSTGRES_CONNECTION_STRING, COSMOSDB_URL/KEY/DATABASE/COLLECTION
      - RABBITMQ_CONNECTION_STRING, KAFKA_BROKERS/AUTH_TYPE, SERVICEBUS_CONNECTION_STRING
      - DAPR_TRUST_DOMAIN, DAPR_NAMESPACE, SUBSCRIBER_APP_ID, OPS_MONITOR_APP_ID
    - Document `{env:VAR_NAME}` substitution syntax in DAPR component YAML
  - [x] 1.13 Write "Next Steps" section: Links to:
    - Security Model Documentation (Story 14-6)
    - Troubleshooting Guide (Story 14-7)
    - Deployment Progression Guide (deployment-progression.md)
    - Individual deployment guides for environment-specific setup
    - Configuration Reference (Story 15-1 — future)
- [x] Task 2: Validation (AC: all)
  - [x] 2.1 Verify the page structure follows the page template convention (back-link, H1, intro paragraph, prerequisites blockquote, content sections, next steps)
  - [x] 2.2 Verify all Mermaid diagrams render correctly (valid syntax, `<details>` text alternative)
  - [x] 2.3 Verify all internal links resolve to existing files
  - [x] 2.4 Run markdownlint on `docs/guides/dapr-component-reference.md` to ensure CI compliance
  - [x] 2.5 Verify all YAML examples are syntactically valid (consistent with actual files in `deploy/dapr/` and `src/Hexalith.EventStore.AppHost/DaprComponents/`)
  - [x] 2.6 Verify persistence guarantees table is accurate per FR60
  - [x] 2.7 Verify all five DAPR building blocks are documented per AC #1

### Review Follow-ups (AI)

- [x] [AI-Review][HIGH] Replace abbreviated RabbitMQ/Kafka/Service Bus snippets with complete YAML examples as required by Task 1.4 and AC #2.
- [x] [AI-Review][HIGH] Correct and explicitly document the required composite key pattern `{tenant}:{domain}:{aggregateId}:events:{seq}`.
- [x] [AI-Review][HIGH] Add actor timer/reminder configuration coverage in Actors section per Task 1.5.
- [x] [AI-Review][MEDIUM] Ensure inline comments explain every YAML field in every example (including previously missing `redisPassword` field comments).
- [x] [AI-Review][MEDIUM] Align configuration store scoping narrative with YAML while keeping source-accurate default scope.
- [x] [AI-Review][MEDIUM] Keep Dev Agent File List synchronized with actual changed files.

## Dev Notes

### Architecture Patterns & Constraints

- **This is a DOCUMENTATION-ONLY story.** No code changes. Single markdown file output at `docs/guides/dapr-component-reference.md`.
- **FR25 is the primary requirement:** "An operator can configure each DAPR component (State Store, Pub/Sub, Actors, Configuration, Resiliency) for their target infrastructure with documented examples per backend."
- **FR60 is the secondary requirement:** "An operator can understand where event data is physically stored based on their DAPR state store configuration and what persistence guarantees each backend provides."
- **FR43 (self-contained pages):** The page must be navigable without reading prerequisite pages. Define DAPR terms on first use.
- **FR48 (troubleshooting):** Link to the troubleshooting page for common component configuration mismatches.
- **DAPR explanation depth = Operational:** This is a deployment/operations guide, so show full DAPR component configuration with field-by-field YAML comments. Assume reader knows .NET but NOT DAPR.

### Existing DAPR Component YAML Files — USE THESE AS SOURCE

**DO NOT invent YAML configurations.** Copy from these actual project files and add inline comments:

**Production components (`deploy/dapr/`):**
- `statestore-postgresql.yaml` (34 lines) — PostgreSQL state store with composite key pattern, commandapi scoping, actorStateStore: true
- `statestore-cosmosdb.yaml` (42 lines) — Cosmos DB state store with managed identity metadata
- `pubsub-rabbitmq.yaml` (146 lines) — RabbitMQ with three-layer scoping architecture (component scopes, publishingScopes, subscriptionScopes)
- `pubsub-kafka.yaml` (148 lines) — Kafka with same three-layer scoping, consumer group isolation
- `pubsub-servicebus.yaml` (117 lines) — Azure Service Bus with same three-layer scoping
- `resiliency.yaml` (74 lines) — Retry, timeout, circuit breaker policies for apps and components
- `accesscontrol.yaml` (70 lines) — Deny-by-default with SPIFFE trust domain, domain service templates
- `subscription-sample-counter.yaml` (18 lines) — Declarative subscription v2alpha1 for Counter sample

**Local development components (`src/Hexalith.EventStore.AppHost/DaprComponents/`):**
- `statestore.yaml` — Redis state store with commandapi scoping
- `pubsub.yaml` — Redis pub/sub with three-layer scoping
- `configstore.yaml` — Redis configuration store

### Key DAPR Architecture Decisions to Reference

| Decision | Description | Impact on Configuration |
|----------|-------------|------------------------|
| D1 | Composite key strategy | All state store keys follow `{tenant}:{domain}:{aggregateId}:events:{seq}` pattern |
| D2 | Command status TTL | 24-hour TTL set at application level, not component level |
| D4 | Deny-by-default access | Only `commandapi` app-id accesses state store and pub/sub; access control policy enforces this |
| D6 | Topic naming | Topics follow `{tenant}.{domain}.events` pattern; dead-letter: `deadletter.{tenant}.{domain}.events` |
| D7 | Resiliency at sidecar level | No custom retry logic in application code; DAPR resiliency policies handle all retries |

### Three-Layer Pub/Sub Scoping Architecture

All pub/sub components (RabbitMQ, Kafka, Service Bus, Redis) use the same three-layer scoping:
1. **Layer 1 — Component scopes:** Which app-ids can access the component at all
2. **Layer 2 — publishingScopes:** Which app-ids can publish to which topics (commandapi unrestricted for dynamic tenant provisioning)
3. **Layer 3 — subscriptionScopes:** Which app-ids can subscribe to which topics (explicitly scoped per subscriber)

No wildcard support — strict string equality matching only. DAPR enforcement happens before the broker.

### Persistence Guarantees Summary (FR60)

| Backend | Durability | Consistency | ETag Support | Transaction Support |
|---------|-----------|-------------|-------------|-------------------|
| Redis | In-memory (AOF/RDB optional) | Eventual | Yes (SET NX) | Multi-key MULTI/EXEC |
| PostgreSQL | WAL-based | ACID Strong | Yes (row version) | Full ACID |
| Azure Cosmos DB | Multi-region replicated | Configurable (5 levels) | Yes (`_etag`) | Cross-partition limited |
| RabbitMQ (pub/sub) | Durable queues (optional) | At-least-once | N/A | N/A |
| Kafka (pub/sub) | Commit log (configurable retention) | At-least-once | N/A | N/A |
| Azure Service Bus (pub/sub) | Guaranteed delivery | At-least-once | N/A | N/A |

### Environment Variable Reference

All backends use `{env:VAR_NAME}` substitution in DAPR component YAML:

| Variable | Backend | Example Value |
|----------|---------|---------------|
| `REDIS_HOST` | Redis | `localhost:6379` |
| `POSTGRES_CONNECTION_STRING` | PostgreSQL | `Host=localhost;Database=eventstore;Username=...` |
| `COSMOSDB_URL` | Azure Cosmos DB | `https://mycosmosdb.documents.azure.com:443/` |
| `COSMOSDB_KEY` | Azure Cosmos DB | `<primary-key>` |
| `COSMOSDB_DATABASE` | Azure Cosmos DB | `eventstore` |
| `COSMOSDB_COLLECTION` | Azure Cosmos DB | `actorstate` |
| `RABBITMQ_CONNECTION_STRING` | RabbitMQ | `amqp://user:pass@rabbitmq:5672` |
| `KAFKA_BROKERS` | Kafka | `kafka:9092` |
| `KAFKA_AUTH_TYPE` | Kafka | `none` or `mtls` |
| `SERVICEBUS_CONNECTION_STRING` | Azure Service Bus | `Endpoint=sb://...` |
| `DAPR_TRUST_DOMAIN` | Access Control | `hexalith.io` |
| `DAPR_NAMESPACE` | Access Control | `hexalith` |
| `SUBSCRIBER_APP_ID` | Pub/Sub scoping | `sample` |
| `OPS_MONITOR_APP_ID` | Pub/Sub scoping | `ops-monitor` |

### Existing Content — DO NOT Duplicate in Detail

The following content already exists in deployment guides. The component reference page should provide the CONSOLIDATED, backend-focused view and link to these for environment-specific setup:

- `docs/guides/deployment-docker-compose.md` (644 lines) — Docker Compose with Redis components
- `docs/guides/deployment-kubernetes.md` (1,050 lines) — K8s with CRD setup, secret injection
- `docs/guides/deployment-azure-container-apps.md` (978 lines) — ACA with simplified schema, Bicep
- `docs/guides/deployment-progression.md` (228 lines) — Side-by-side component comparison
- `deploy/README.md` (340 lines) — Backend compatibility matrix, per-backend env vars

The component reference should be the "go-to" page for understanding WHAT each component does and HOW to configure it, while deployment guides explain WHERE and WHEN to deploy.

### ACA-Specific Caveats

Azure Container Apps uses a **simplified component schema** (no `apiVersion`, `kind`, `metadata.name` wrapper). The component reference must show BOTH formats:
- Standard DAPR component YAML (for Docker Compose and Kubernetes)
- ACA simplified schema (for Azure Container Apps)

ACA does NOT support `accesscontrol.yaml` — equivalent security is achieved via component scoping.

### Page Template Convention

Follow the exact pattern from Stories 14-1 through 14-4:
1. Back link: `[<- Back to Hexalith.EventStore](../../README.md)`
2. H1 title
3. Opening paragraph: what the page covers and who it's for
4. `> **Prerequisites:** [link]` blockquote (max 2 per NFR10)
5. Content sections with Mermaid diagrams (with `<details>` alt text per NFR7)
6. Tables for structured comparisons
7. Copy-pasteable YAML code blocks with `yaml` language hint
8. "Next Steps" section with links

### Target Length

This is a REFERENCE page with substantial YAML examples. Target 500-800 lines (each YAML block is 15-40 lines with comments). Longer than the progression guide (228 lines) but similar to deployment guides (600-1000 lines).

### Markdownlint Rules

Configuration in `.markdownlint-cli2.jsonc`:
- `MD013`: disabled (no hard wrap)
- `MD014`: disabled (allow `$` prefix)
- `MD033`: allow `<details>`, `<summary>`, `<br>`, `<img>`, `<picture>`, `<source>`
- `MD024`: `siblings_only: true` (duplicate headings OK in different sections)
- `MD041`: disabled (nav links before H1 OK)
- `MD046`: `style: fenced` (fenced code blocks only)
- `MD048`: `style: backtick` (backtick fences only)
- `MD007`: `indent: 4` (4-space list indentation)
- `MD029`: `style: ordered` (sequential ordered list numbering)
- `MD036`: enabled (no bold-as-heading)

### Project Structure Notes

- File location: `docs/guides/dapr-component-reference.md` (NFR26: descriptive, unabbreviated, hyphen-separated)
- `docs/guides/` already contains 4 deployment guides — this is the 5th guide in the directory
- No new directories needed
- No code changes or sample files needed — documentation-only story
- Reference existing YAML files in `deploy/dapr/` and `src/Hexalith.EventStore.AppHost/DaprComponents/` for configuration examples

### Previous Story Intelligence (14-4)

Key learnings from Story 14-4 (Deployment Progression Guide):
- **Page template confirmed:** back-link, H1, intro, prerequisites, content, next steps pattern is established and consistent
- **Mermaid diagrams must include `<details>` text descriptions** for accessibility (NFR7)
- **Both bash and PowerShell command variants** for platform-specific commands (env var exports); NOT required for platform-neutral commands
- **markdownlint-cli2 must pass** — run validation before completion
- **Internal links verified manually** — ensure all links point to existing files
- **Code blocks need language hints** (`yaml`, `bash`, `powershell`) for syntax highlighting
- **Comparison tables are the most valuable content** — readers consistently reference them
- **Connector pages should be concise** — but this is a REFERENCE page, so longer is expected

### Git Intelligence

Recent commits for Epic 14:
- `f04bdf7` feat(docs): Add deployment progression guide connecting all deployment environments (Story 14-4)
- `02bd290` feat(docs): Add Azure Container Apps deployment guide and Bicep templates (Story 14-3)
- `c2f5a56` feat(docs): Add Kubernetes deployment guide and sample manifests (Story 14-2)

**Commit pattern:** `feat(docs): <description> (Story 14-5)`
**Branch pattern:** `docs/story-14-5-dapr-component-configuration-reference`

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 7.5 — DAPR Component Configuration Reference]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR25 — DAPR component configuration per backend]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR60 — Persistence guarantees per backend]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR43 — Self-contained pages]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR48 — DAPR integration troubleshooting]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1-D7 — DAPR architecture decisions]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md — Page template, markdown standards, DAPR explanation depth]
- [Source: deploy/dapr/*.yaml — Production DAPR component configurations (8 files)]
- [Source: src/Hexalith.EventStore.AppHost/DaprComponents/*.yaml — Local dev components (3 files)]
- [Source: deploy/README.md — Backend compatibility matrix, env var reference]
- [Source: docs/guides/deployment-docker-compose.md — Docker Compose deployment guide]
- [Source: docs/guides/deployment-kubernetes.md — Kubernetes deployment guide]
- [Source: docs/guides/deployment-azure-container-apps.md — Azure Container Apps deployment guide]
- [Source: docs/guides/deployment-progression.md — Deployment progression guide]
- [Source: _bmad-output/implementation-artifacts/14-4-deployment-progression-guide.md — Previous story patterns]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

No debug issues encountered. Documentation-only story — no code compilation or test execution required.

### Completion Notes List

- Created `docs/guides/dapr-component-reference.md` (675 lines) — comprehensive DAPR component configuration reference
- All five DAPR building blocks documented: State Store, Pub/Sub, Actors, Configuration, Resiliency
- YAML examples copied directly from actual project files in `deploy/dapr/` and `src/Hexalith.EventStore.AppHost/DaprComponents/` with inline comments
- Three state store backends covered: Redis (local dev), PostgreSQL (on-premise), Azure Cosmos DB (cloud)
- Four pub/sub backends covered: Redis Streams (local dev), RabbitMQ, Kafka, Azure Service Bus
- Three-layer scoping architecture documented for all pub/sub backends
- ACA simplified schema shown for Cosmos DB state store and Service Bus pub/sub
- Persistence guarantees comparison tables for all state and pub/sub backends (FR60)
- Backend swap procedure with zero-code-change emphasis
- Environment variable reference tables organized by category
- Mermaid architecture diagram with `<details>` accessibility text (NFR7)
- markdownlint: 0 errors
- All internal links to existing files verified (6/6 resolve)
- Forward links to future stories (14-6, 14-7, 15-1) included as expected
- Senior Developer Review (AI) follow-ups resolved: full production pub/sub YAML blocks restored, D1 key pattern corrected, actor reminder configuration documented, and missing inline comments/scoping narrative gaps fixed

### Change Log

- 2026-03-02: Created DAPR Component Configuration Reference page (Story 14-5) — all tasks and validation complete
- 2026-03-02: Senior Developer Review (AI) found 3 High and 3 Medium issues; moved status to in-progress and added follow-up tasks
- 2026-03-02: Applied automatic fixes for all review findings and re-validated story; moved status to done

### File List

- `docs/guides/dapr-component-reference.md` (NEW) — DAPR Component Configuration Reference page
- `_bmad-output/implementation-artifacts/14-5-dapr-component-configuration-reference.md` (UPDATED) — review follow-ups and final status sync
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (UPDATED) — sprint tracking sync for story 14-5

## Senior Developer Review (AI)

### Reviewer

Jerome

### Date

2026-03-02

### Outcome

Approved

### Findings Summary

- High: 0
- Medium: 0
- Low: 0

### Key Findings

- All previously reported High and Medium findings were addressed and verified in the updated documentation and story metadata.

### Decision Rationale

All required documentation gaps identified during review are now fixed, and no High or Medium review findings remain.
