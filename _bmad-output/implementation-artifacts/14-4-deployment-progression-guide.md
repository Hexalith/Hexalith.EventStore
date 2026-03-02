# Story 14.4: Deployment Progression Guide

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer who started with the local Docker quickstart,
I want a guide showing the progression from local to Kubernetes to Azure,
so that I understand how the same application code runs across all environments with only infrastructure changes.

## Acceptance Criteria

1. Given all three deployment guides exist (Stories 14-1, 14-2, 14-3), when a developer navigates to `docs/guides/deployment-progression.md`, then the page shows a clear progression path: local Docker Compose -> on-premise Kubernetes -> Azure Container Apps using the same Counter application code
2. The page highlights what changes between environments (DAPR components, infrastructure config) and what stays the same (application code)
3. The page includes a comparison table of environment differences (FR58)
4. The page links to each deployment guide as the detailed walkthrough
5. The page follows the standard page template (back-link, H1, intro, prerequisites blockquote, content, next steps)

## Tasks / Subtasks

- [x] Task 1: Create `docs/guides/deployment-progression.md` (AC: #1, #2, #3, #4, #5)
  - [x] 1.1 Write page header: back-link `[<- Back to Hexalith.EventStore](../../README.md)`, H1 title "Deployment Progression Guide", intro paragraph explaining this is the "big picture" page connecting the three deployment guides, and prerequisites blockquote linking to `../getting-started/prerequisites.md`
  - [x] 1.2 Write "The Zero-Code-Change Promise" section: Explain that the SAME Counter sample application code (`samples/Hexalith.EventStore.Sample/`) runs identically across all three environments. Only infrastructure configuration (DAPR components, secrets, networking) changes. Highlight this is enabled by DAPR's building block abstraction — the application talks to DAPR APIs, DAPR talks to the infrastructure
  - [x] 1.3 Create "Progression Overview" section with a Mermaid diagram showing the three-tier progression: Docker Compose (local dev) -> Kubernetes (on-premise production) -> Azure Container Apps (cloud PaaS). Include `<details>` text description for accessibility (NFR7)
  - [x] 1.4 Write "What Changes, What Stays the Same" section with TWO tables:
    - **Table 1 — What STAYS the same** across all environments: application code, domain service logic, command/event contracts, DAPR building block usage (state store, pub/sub, actors), health check endpoints (/health, /alive, /ready), REST API endpoints
    - **Table 2 — What CHANGES** per environment: DAPR management (manual containers / operator-injected / Azure-managed), component config format (file-mounted YAML / Kubernetes CRDs / ACA environment-level resources), secret management (.env file / K8s Secrets + secretKeyRef / managed identity + Key Vault), networking (Docker network / K8s Service DNS / ACA Environment DNS), authentication (Keycloak local / external OIDC / Entra ID native), scaling (manual / HPA + KEDA / built-in scaling rules + KEDA), health probes (Docker HEALTHCHECK / K8s liveness+readiness+startup probes / ACA probes), image delivery (local build / registry push / ACR integration), mTLS (not available / DAPR Sentry / Azure-managed automatic), IaC tool (docker-compose.yaml / Helm chart + kubectl / Bicep modules), manifest generation (`aspire publish --docker` / `aspire publish --k8s` / `aspire publish --aca`)
  - [x] 1.5 Write "Environment Comparison Table" section (FR58): Create a comprehensive three-column comparison table covering ALL environment differences. Categories: DAPR Runtime Setup, Component Configuration Format, Secret Management Approach, Networking Model, Authentication Provider, Scaling Strategy, Health Check Implementation, Container Image Delivery, Service-to-Service Security (mTLS), Infrastructure-as-Code Tool, Aspire Publisher Target, DAPR Install Command, Approximate Resource Requirements, Estimated Cost (dev environment)
  - [x] 1.6 Write "DAPR Component Configuration Across Environments" section: Show side-by-side examples of the SAME state store component configured for each environment:
    - Docker Compose: File-mounted YAML with `state.redis`, `{env:REDIS_HOST}` placeholders
    - Kubernetes: CRD with `state.postgresql`, `secretKeyRef` for credentials
    - Azure: ACA simplified schema with `state.azure.cosmosdb`, `azureClientId` for managed identity
    - Explain that the application code NEVER changes — only the DAPR component YAML differs
  - [x] 1.7 Write "Choosing Your Deployment Target" section: Decision guide helping readers pick the right environment:
    - **Docker Compose**: Best for local development, demos, single-machine testing. Simplest setup. Use when: getting started, CI pipeline testing, quick demos
    - **Kubernetes**: Best for on-premise production, multi-node clusters, existing K8s infrastructure. Use when: running in your own data center, compliance requires on-premise, team already uses K8s
    - **Azure Container Apps**: Best for cloud-native, managed operations, minimal infrastructure management. Use when: Azure subscription available, want managed DAPR (zero operator maintenance), prefer PaaS over IaaS
  - [x] 1.8 Write "The Progression Path" section: Step-by-step narrative guiding a developer through the natural progression:
    - Step 1: Start with Docker Compose (link to `deployment-docker-compose.md`) — Learn the topology, DAPR building blocks, health endpoints, backend swap
    - Step 2: Move to Kubernetes (link to `deployment-kubernetes.md`) — Apply production patterns: CRDs, secret management, external OIDC, K8s probes, ingress exposure
    - Step 3: Graduate to Azure (link to `deployment-azure-container-apps.md`) — Leverage managed DAPR, Bicep IaC, managed identity, Entra ID, auto-scaling
    - Each step should state "What you already know" and "What's new" (FR59)
  - [x] 1.9 Write "Common Patterns Across All Environments" section: Highlight the patterns that remain identical:
    - Composite key strategy: `{tenant}:{domain}:{aggregateId}:events:{seq}`
    - Command status key: `{tenant}:{correlationId}:status` with 24h TTL
    - Topic naming: `{tenant}.{domain}.events`
    - Dead-letter routing: `deadletter.{tenant}.{domain}.events`
    - Component scoping: Only `commandapi` app-id accesses state store and pub/sub (D4)
    - Deny-by-default access control (different implementations per environment)
  - [x] 1.10 Write "Backend Compatibility Matrix" section: Reference the matrix from `deploy/README.md` — show which state store and pub/sub backends work in which environments:
    - State stores: Redis (local), PostgreSQL (K8s/Azure), Azure Cosmos DB (Azure)
    - Pub/sub: Redis Streams (local), RabbitMQ (K8s), Kafka (K8s), Azure Service Bus (Azure)
    - Link to `../../deploy/README.md` for full configuration details per backend
  - [x] 1.11 Write "Next Steps" section: Links to:
    - DAPR Component Configuration Reference (Story 14-5)
    - Security Model Documentation (Story 14-6)
    - Troubleshooting Guide (Story 14-7)
    - Each individual deployment guide for detailed walkthroughs
- [x] Task 2: Validation (AC: #5)
  - [x] 2.1 Verify the guide structure follows the page template convention (back-link, H1, intro paragraph, prerequisites blockquote, content sections, next steps)
  - [x] 2.2 Verify all Mermaid diagrams render correctly (valid syntax, `<details>` text alternative)
  - [x] 2.3 Verify all internal links resolve to existing files (three deployment guides, deploy/README.md, quickstart, prerequisites, architecture-overview)
  - [x] 2.4 Run markdownlint on `docs/guides/deployment-progression.md` to ensure CI compliance
  - [x] 2.5 Verify FR58 comparison table is comprehensive and accurate (cross-check against tables in 14-1, 14-2, 14-3 guides)

## Dev Notes

### Architecture Patterns & Constraints

- **This is a DOCUMENTATION-ONLY story.** No code changes. Single markdown file output at `docs/guides/deployment-progression.md`.
- **DO NOT duplicate content from the three deployment guides.** This page is a high-level overview and connector. Link to the guides for detailed walkthroughs. Keep it concise — the individual guides are already 600-1000 lines each.
- **FR56 is the primary requirement:** "A developer can follow a documented progression from the local Docker sample to on-premise Kubernetes to Azure cloud deployment using the same application code with only infrastructure configuration changes."
- **FR58 is the secondary requirement:** "A developer can understand what infrastructure differences exist between local Docker, on-premise Kubernetes, and Azure cloud deployments and why each configuration differs."
- **FR59 informs the tone:** Each progression step should explicitly state what the developer already knows from previous environments and what's new.

### Existing Content — DO NOT Duplicate

The three deployment guides already exist and are comprehensive:
- `docs/guides/deployment-docker-compose.md` (644 lines) — Docker Compose with DAPR sidecars, Redis, Keycloak
- `docs/guides/deployment-kubernetes.md` (1,050 lines) — K8s with DAPR operator, CRDs, external OIDC
- `docs/guides/deployment-azure-container-apps.md` (978 lines) — ACA with managed DAPR, Bicep, Entra ID

Each guide already contains its own "Infrastructure Differences" comparison table (FR58). This progression guide should provide the CONSOLIDATED comparison and add the high-level narrative connecting them.

Additional existing content to link (not duplicate):
- `deploy/README.md` (340 lines) — Backend compatibility matrix, per-backend env vars, Aspire publisher commands
- `docs/getting-started/quickstart.md` — Aspire-based local development flow
- `docs/concepts/architecture-overview.md` — System topology and DAPR building blocks

### Comparison Tables Already in Deployment Guides

All three guides already have a "Docker vs K8s vs ACA" comparison table. The canonical comparison from Story 14-3:

| Aspect | Docker Compose | Kubernetes | Azure Container Apps |
|--------|---------------|-----------|---------------------|
| DAPR sidecar | Manual container definitions | Annotation-based auto-injection | Azure-managed (enable per app) |
| Components | File-mounted YAML | Kubernetes CRDs | Environment-level ACA resources |
| Secrets | `.env` file | K8s Secrets + secretKeyRef | Managed identity + Key Vault |
| Networking | Docker network | K8s Service DNS | Container Apps Environment DNS |
| Auth | Keycloak (local) | External OIDC (required) | Entra ID (recommended) |
| Scaling | Manual replicas | HPA / KEDA | Built-in scaling rules + KEDA |
| Health probes | Docker HEALTHCHECK | K8s liveness/readiness probes | ACA probes (same schema as K8s) |
| Manifest source | `aspire publish` -> docker-compose.yaml | `aspire publish` -> Helm chart | `aspire publish` -> Bicep modules |
| mTLS | Not available | DAPR Sentry (manual config) | Azure-managed (automatic) |
| IaC | docker-compose.yaml | Helm + kubectl | Bicep / ARM templates |
| DAPR install | `dapr init` (Docker runtime) | `dapr init -k` (operator) | None (Azure-managed) |

Use this as the foundation for the consolidated FR58 comparison table, but EXPAND it with additional rows: Aspire publisher command, approximate resource requirements, estimated cost, DAPR component format example, and secret management pattern.

### DAPR Component Side-by-Side Example

For Section 1.6, show the same logical component (state store) in three formats:

**Docker Compose (file-mounted):**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.redis
  version: v1
  metadata:
    - name: redisHost
      value: "redis:6379"
  scopes:
    - commandapi
```

**Kubernetes (CRD):**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
  namespace: hexalith
spec:
  type: state.postgresql
  version: v1
  metadata:
    - name: connectionString
      secretKeyRef:
        name: postgres-credentials
        key: connection-string
  scopes:
    - commandapi
```

**Azure Container Apps (simplified schema):**
```yaml
componentType: state.azure.cosmosdb
version: v1
metadata:
  - name: url
    value: "https://mycosmosdb.documents.azure.com:443/"
  - name: database
    value: "eventstore"
  - name: collection
    value: "actorstate"
  - name: azureClientId
    value: "<managed-identity-client-id>"
scopes:
  - commandapi
```

Note how:
- Schema differs (full CRD vs simplified ACA format)
- Backend changes (Redis -> PostgreSQL -> Cosmos DB)
- Secret approach changes (env var -> K8s Secret -> managed identity)
- Scoping is identical (`commandapi` only — D4 principle)
- Application code is UNCHANGED

### Aspire Publisher Commands

For reference in the progression narrative:
- **Docker**: `PUBLISH_TARGET=docker aspire publish --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -o ./publish-output/docker`
- **Kubernetes**: `PUBLISH_TARGET=k8s EnableKeycloak=false aspire publish --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -o ./publish-output/k8s`
- **Azure**: `PUBLISH_TARGET=aca EnableKeycloak=false aspire publish --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -o ./publish-output/azure`

Note: `EnableKeycloak=false` is REQUIRED for K8s and ACA (Keycloak bind mounts are not supported outside Docker).

### Page Template Convention

Follow the exact same pattern as the three deployment guides:
1. Back link: `[<- Back to Hexalith.EventStore](../../README.md)`
2. H1 title
3. Opening paragraph explaining what the page covers and who it's for
4. `> **Prerequisites:** [link]` blockquote
5. Content sections with Mermaid diagrams where needed (with `<details>` alt text per NFR7)
6. Tables for comparison data
7. Links to detailed guides for walkthroughs
8. "Next Steps" section with links to related pages

### Target Length

This is a CONNECTOR page, not a detailed walkthrough. Target 200-350 lines (compare: each deployment guide is 600-1000 lines). Avoid duplicating detailed instructions — link instead.

### Project Structure Notes

- File location: `docs/guides/deployment-progression.md` (NFR26: descriptive, unabbreviated, hyphen-separated)
- `docs/guides/` already contains: `deployment-docker-compose.md`, `deployment-kubernetes.md`, `deployment-azure-container-apps.md`
- No new directories needed
- No sample files or code artifacts needed for this story

### Previous Story Intelligence (14-3)

Key learnings from Story 14-3 (Azure Container Apps guide):
- **Page template works well** — back-link, H1, intro, prerequisites, content, next steps is established and consistent
- **Mermaid diagrams must include `<details>` text descriptions** for accessibility (NFR7)
- **Both bash and PowerShell command variants** should be shown for platform-specific commands (env var exports); NOT required for platform-neutral commands (az, dotnet, kubectl)
- **markdownlint-cli2 must pass** — run validation before completion
- **Internal links were verified manually** — ensure all links point to existing files
- **Code blocks need language hints** (```yaml, ```bash, ```powershell) for syntax highlighting
- **Comparison tables are the most valuable content** — readers consistently reference them

### Git Intelligence

Recent commits show the deployment guide pattern:
- `02bd290` feat(docs): Add Azure Container Apps deployment guide and Bicep templates (Story 14-3)
- `c2f5a56` feat(docs): Add Kubernetes deployment guide and sample manifests (Story 14-2)
- `2700786` feat(docs): Update Docker Compose deployment guide and configuration status to done

Commit message pattern: `feat(docs): <description> (Story <id>)`
Branch pattern: `docs/story-14-4-deployment-progression-guide`

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 7, Story 7.4]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR56, FR58, FR59, NFR7, NFR22, NFR26]
- [Source: docs/guides/deployment-docker-compose.md — Docker Compose guide, 644 lines]
- [Source: docs/guides/deployment-kubernetes.md — Kubernetes guide, 1,050 lines]
- [Source: docs/guides/deployment-azure-container-apps.md — Azure Container Apps guide, 978 lines]
- [Source: deploy/README.md — Backend compatibility matrix, Aspire publisher commands]
- [Source: _bmad-output/implementation-artifacts/14-3-azure-container-apps-deployment-guide-and-configuration.md — Previous story patterns]
- [Source: _bmad-output/implementation-artifacts/14-2-kubernetes-deployment-guide-and-configuration.md — K8s story patterns]
- [Source: _bmad-output/implementation-artifacts/14-1-docker-compose-deployment-guide-and-configuration.md — Docker Compose story patterns]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

No debug issues encountered. Clean implementation.

### Completion Notes List

- Created `docs/guides/deployment-progression.md` (228 lines) as a connector page linking the three deployment guides
- Document includes: page header with back-link and prerequisites (1.1), Zero-Code-Change Promise section (1.2), Mermaid progression diagram with details alt text (1.3), two comparison tables for what stays/changes (1.4), comprehensive FR58 environment comparison table with 14 categories (1.5), side-by-side DAPR component configuration examples for all 3 environments (1.6), deployment target decision guide (1.7), three-step progression path with "What you already know"/"What's new" per FR59 (1.8), common patterns section (1.9), backend compatibility matrix linking to deploy/README.md (1.10), and next steps section (1.11)
- Validation: markdownlint-cli2 passes with 0 errors, all 6 internal links verified, Mermaid diagram with details tag present, page template convention followed
- AI code review follow-up applied: corrected over-strong zero-redeploy wording, added explicit Step 1 "What you already know" + "What's new" structure for FR59 consistency, added `EnableKeycloak=false` publisher note for K8s/ACA alignment, and converted Next Steps future items to valid planning links
- Story File List reconciled with git-tracked changes from review workflow updates
- No code changes — documentation-only story
- All 465 Tier 1 tests pass (157 Contracts + 231 Client + 29 Sample + 48 Testing), 0 regressions

### Senior Developer Review (AI)

- Review outcome: issues addressed automatically (critical/high/medium findings resolved in documentation and story artifacts)
- Confirmed: ACs remain satisfied and page template/lint compliance preserved after fixes
- Story moved from `review` to `done` and sprint tracking synchronized

### Change Log

- 2026-03-02: Created deployment progression guide (docs/guides/deployment-progression.md) connecting Docker Compose, Kubernetes, and Azure Container Apps deployment guides with consolidated comparison tables and progression narrative
- 2026-03-02: Applied AI review fixes for FR59 step consistency, operational publish note (`EnableKeycloak=false`), Next Steps link validity, and story metadata synchronization

### File List

- docs/guides/deployment-progression.md (new, then updated by AI review fixes)
- _bmad-output/implementation-artifacts/14-4-deployment-progression-guide.md (updated with review record and status)
- _bmad-output/implementation-artifacts/sprint-status.yaml (status sync)
