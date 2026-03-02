# Story 14.2: Kubernetes Deployment Guide & Configuration

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator deploying Hexalith to an on-premise cluster,
I want a step-by-step walkthrough for deploying the sample application to Kubernetes,
so that I can run the system in a production environment.

## Acceptance Criteria

1. `docs/guides/deployment-kubernetes.md` exists with a complete walkthrough: DAPR runtime setup for Kubernetes (FR57), step-by-step deployment instructions, Kubernetes YAML manifests, DAPR component configuration for Kubernetes, and health/readiness verification (FR26)
2. `samples/deploy/kubernetes/` contains all necessary Kubernetes manifests and DAPR component configs (OR the guide documents the Aspire publisher approach to generate them)
3. The guide explicitly references what the reader already knows from the Docker quickstart and what's new (FR59)
4. The guide explains infrastructure differences between local Docker and Kubernetes (FR58)
5. The guide includes resource requirements and pod sizing guidance (FR63)
6. Event data storage location is documented per backend (FR60)
7. The walkthrough produces a verifiably running system when followed step-by-step (NFR22)
8. The page follows the standard page template with DAPR explained at operational depth

## Tasks / Subtasks

- [ ] Task 1: Create `docs/guides/deployment-kubernetes.md` (AC: #1, #3, #4, #5, #6, #7, #8)
  - [ ] 1.1 Write page header: back-link `[<- Back to Hexalith.EventStore](../../README.md)`, H1 title, intro paragraph, prerequisites blockquote
  - [ ] 1.2 Write "What You Already Know" bridge section referencing Docker Compose guide concepts (FR59) — topology, DAPR building blocks, health endpoints. State what's NEW in K8s: DAPR operator, sidecar injection via annotations, CRD-based components, Helm chart output, K8s Secrets, external OIDC
  - [ ] 1.3 Create "What You'll Deploy" section with Mermaid deployment topology diagram showing: K8s cluster with namespace, commandapi Pod + DAPR sidecar, sample Pod + DAPR sidecar, DAPR operator, DAPR placement service, external state store (PostgreSQL), external pub/sub (RabbitMQ/Kafka), external OIDC provider
  - [ ] 1.4 Add `<details>` text description for the Mermaid diagram (NFR7 accessibility)
  - [ ] 1.5 Write "Prerequisites" section: kubectl, Helm 3, Kubernetes cluster (minikube/kind/AKS/EKS/GKE), .NET 10 SDK, Aspire CLI (`dotnet tool install -g Aspire.Cli`), DAPR CLI, container registry access
  - [ ] 1.6 Write "Install DAPR on Kubernetes" section (FR57): `dapr init -k` or Helm chart install, verify DAPR system pods (`dapr-operator`, `dapr-sidecar-injector`, `dapr-placement`, `dapr-sentry`), version compatibility note (DAPR SDK 1.17.0)
  - [ ] 1.7 Write "Generate Kubernetes Manifests" section: Aspire publisher command `PUBLISH_TARGET=k8s EnableKeycloak=false aspire publish --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -o ./publish-output/k8s` with both bash and PowerShell variants. Explain `EnableKeycloak=false` is REQUIRED (K8s publisher does not support bind mounts for realm import). Document expected Helm chart output: `Chart.yaml`, `values.yaml`, templates with Deployments/Services/ConfigMaps for commandapi and sample
  - [ ] 1.8 Write "Add DAPR Annotations" section: The K8s publisher does NOT auto-generate DAPR annotations. Show exact annotation block to add to each Deployment pod template:
    - commandapi: `dapr.io/enabled: "true"`, `dapr.io/app-id: "commandapi"`, `dapr.io/app-port: "8080"`, `dapr.io/config: "accesscontrol"`
    - sample: `dapr.io/enabled: "true"`, `dapr.io/app-id: "sample"`, `dapr.io/config: "accesscontrol"`
  - [ ] 1.9 Write "Apply DAPR Components" section: Apply production DAPR components from `deploy/dapr/` as K8s CRDs. Show `kubectl apply -f` for chosen state store + pub/sub + resiliency + accesscontrol + subscription. Explain component scoping and namespace targeting
  - [ ] 1.10 Write "Configure Secrets" section: Create K8s Secrets for connection strings (e.g., `POSTGRES_CONNECTION_STRING`, `RABBITMQ_CONNECTION_STRING`). Show `kubectl create secret generic` commands. Explain env-var-to-secret mapping in Deployment manifests. Reference deploy/README.md for per-backend env var details
  - [ ] 1.11 Write "Configure External OIDC Authentication" section: Set env vars on commandapi Deployment — `Authentication__JwtBearer__Authority`, `Authentication__JwtBearer__Issuer`, `Authentication__JwtBearer__Audience`, `Authentication__JwtBearer__RequireHttpsMetadata=true`. Reference deploy/README.md "External OIDC Configuration" section. Show example for Entra ID (Azure AD)
  - [ ] 1.12 Write "Deploy the Application" section: `helm install` or `kubectl apply` commands, wait for pod readiness, verify DAPR sidecar injection (`kubectl get pods` showing 2/2 containers)
  - [ ] 1.13 Write "Verify System Health" section (FR26): port-forward to commandapi, check `/health`, `/alive`, `/ready` endpoints, verify DAPR sidecar health at `localhost:3500/v1.0/healthz`, check sidecar logs for `component loaded` messages
  - [ ] 1.14 Write "Send a Test Command" section: port-forward, obtain token from external OIDC, submit IncrementCounter command via curl/PowerShell, verify event in state store
  - [ ] 1.15 Write "Where Is My Data?" section (FR60): explain physical storage per DAPR state store backend — PostgreSQL tables, Cosmos DB containers. Composite key pattern `{tenant}||{domain}||{aggregateId}`. Link to deploy/README.md for full backend compatibility matrix
  - [ ] 1.16 Write "Resource Requirements" section (FR63): pod resource requests/limits for commandapi and sample, DAPR sidecar overhead (~128Mi memory, 0.1 CPU per sidecar), state store and pub/sub sizing guidance, node sizing for a minimal cluster
  - [ ] 1.17 Write "Infrastructure Differences: Docker vs Kubernetes" section (FR58): comparison table — sidecar injection (manual containers vs annotation-based), component config (file mount vs CRDs), secret management (.env vs K8s Secrets), networking (Docker network vs K8s Service DNS), scaling (manual vs HPA), health checks (Docker healthcheck vs K8s probes)
  - [ ] 1.18 Write "Backend Swap" section: demonstrate switching state store or pub/sub by applying different CRDs — same zero-code-change principle as Docker Compose
  - [ ] 1.19 Write "Troubleshooting" section: common K8s deployment issues — sidecar not injecting (missing annotations, DAPR operator not running), component load failures (wrong namespace, missing secrets), pod crash loops (missing env vars), DAPR placement service issues
  - [ ] 1.20 Write "Next Steps" section: links to Azure Container Apps guide (Story 14-3), deployment progression guide (Story 14-4), DAPR component configuration reference (Story 14-5), security model docs (Story 14-6)
- [ ] Task 2: Create or document Kubernetes manifests (AC: #2)
  - [ ] 2.1 Create `samples/deploy/kubernetes/` directory
  - [ ] 2.2 Document the Aspire publisher approach as the PRIMARY path: `PUBLISH_TARGET=k8s aspire publish`
  - [ ] 2.3 Show the expected generated Helm chart structure with annotations explaining each file
  - [ ] 2.4 Create a minimal reference `samples/deploy/kubernetes/namespace.yaml` defining the target namespace
  - [ ] 2.5 Create `samples/deploy/kubernetes/dapr-annotations-patch.yaml` as a kustomize-style overlay showing DAPR annotation additions (since publisher doesn't generate them)
  - [ ] 2.6 Create `samples/deploy/kubernetes/secrets-template.yaml` showing the Secret structure (values replaced with placeholders) for PostgreSQL + RabbitMQ connection strings
  - [ ] 2.7 Create `samples/deploy/kubernetes/README.md` documenting the files in the directory and how they complement the Aspire publisher output
- [ ] Task 3: Validation (AC: #7)
  - [ ] 3.1 Verify the guide structure follows the page template convention (back-link, H1, intro, prerequisites, content, next steps)
  - [ ] 3.2 Verify all Mermaid diagrams render correctly
  - [ ] 3.3 Verify all code blocks include both bash and PowerShell alternatives where applicable
  - [ ] 3.4 Verify all internal links resolve (to deploy/README.md, quickstart, architecture-overview, Docker Compose guide)
  - [ ] 3.5 Run markdownlint on the new files to ensure CI compliance

## Dev Notes

### Architecture Patterns & Constraints

- **Aspire Kubernetes Publisher is the primary manifest generation path.** The AppHost's `Program.cs` (line 80-81) configures the K8s publisher: `builder.AddKubernetesEnvironment("k8s")` activated by `PUBLISH_TARGET=k8s`. Generated output is a Helm chart with `Chart.yaml`, `values.yaml`, and templates.
- **DO NOT create hand-written K8s Deployments/Services as the primary artifact.** The Aspire publisher is the intended path. Reference manifests in `samples/deploy/kubernetes/` should be supplementary patches/templates only (namespace, DAPR annotations, secrets).
- **`EnableKeycloak=false` is REQUIRED** for the K8s publisher. The publisher does not support bind mounts used by Keycloak's realm import. Production K8s deployments must use an external OIDC provider.
- **DAPR annotations are NOT auto-generated** by the Aspire K8s publisher. The guide MUST document adding these annotations manually to each Deployment's pod template spec.
- **DAPR components are Kubernetes CRDs** in K8s (unlike Docker Compose where they're file-mounted). Apply them via `kubectl apply -f`. The production components already exist in `deploy/dapr/`.
- **DAPR operator is required** in the K8s cluster for sidecar injection and component management. Install via `dapr init -k` or the official Helm chart.

### DAPR on Kubernetes Specifics

- **Sidecar injection**: DAPR operator watches for `dapr.io/enabled: "true"` annotation and injects sidecar container automatically. Pods show `2/2` containers when sidecar is running.
- **Component scoping**: DAPR K8s CRDs support namespace scoping. Components in a namespace are available to all DAPR-enabled pods in that namespace.
- **Access control with mTLS**: Production uses deny-by-default with SPIFFE-based mTLS identity. Requires `DAPR_TRUST_DOMAIN` and `DAPR_NAMESPACE` environment variables in `deploy/dapr/accesscontrol.yaml`.
- **Secret management**: Use K8s Secrets referenced by env vars in Deployment manifests. DAPR also supports its own secret store component with K8s Secrets backend.
- **DAPR system pods**: After `dapr init -k`, expect: `dapr-operator`, `dapr-sidecar-injector`, `dapr-placement`, `dapr-sentry` (mTLS CA) in `dapr-system` namespace.
- **DAPR SDK compatibility**: The project uses DAPR SDK 1.17.0. Ensure the DAPR runtime version in K8s is compatible (DAPR 1.14+ recommended).

### Health Check Endpoints

Already implemented in `ServiceDefaults/Extensions.cs`:
- `/health` — full health (200 Healthy/Degraded, 503 Unhealthy)
- `/alive` — liveness probe (`"live"` tagged checks only)
- `/ready` — readiness probe (`"ready"` tagged checks only)

Map these to K8s probe definitions:
```yaml
livenessProbe:
  httpGet:
    path: /alive
    port: 8080
  initialDelaySeconds: 10
readinessProbe:
  httpGet:
    path: /ready
    port: 8080
  initialDelaySeconds: 15
```

DAPR health checks in `CommandApi/HealthChecks/`:
- `dapr-sidecar` → Unhealthy failure status
- `dapr-statestore` → Unhealthy failure status
- `dapr-pubsub` → Degraded failure status
- `dapr-configstore` → Degraded failure status

### DAPR Component Topology (Production)

Production components in `deploy/dapr/`:
- `statestore-postgresql.yaml` — PostgreSQL state store (env: `POSTGRES_CONNECTION_STRING`)
- `statestore-cosmosdb.yaml` — Azure Cosmos DB state store (envs: `COSMOSDB_URL`, `COSMOSDB_KEY`, `COSMOSDB_DATABASE`, `COSMOSDB_COLLECTION`)
- `pubsub-rabbitmq.yaml` — RabbitMQ pub/sub (env: `RABBITMQ_CONNECTION_STRING`)
- `pubsub-kafka.yaml` — Kafka pub/sub (envs: `KAFKA_BROKERS`, `KAFKA_AUTH_TYPE`)
- `pubsub-servicebus.yaml` — Azure Service Bus pub/sub (env: `SERVICEBUS_CONNECTION_STRING`)
- `resiliency.yaml` — Production retry, timeout, circuit breaker policies
- `accesscontrol.yaml` — Deny-by-default with mTLS (envs: `DAPR_TRUST_DOMAIN`, `DAPR_NAMESPACE`)
- `subscription-sample-counter.yaml` — Sample subscription with dead-letter routing

### Configuration Patterns

CommandApi configuration (`src/Hexalith.EventStore.CommandApi/appsettings.json`):
- `EventStore:DomainServices:Registrations` — Domain service routing
- `Authentication:JwtBearer` — JWT auth config
- External OIDC env vars: `Authentication__JwtBearer__Authority`, `Authentication__JwtBearer__Issuer`, `Authentication__JwtBearer__Audience`, `Authentication__JwtBearer__RequireHttpsMetadata`

### Key Differences from Story 14-1 (Docker Compose)

| Aspect | Docker Compose (14-1) | Kubernetes (14-2) |
|--------|----------------------|-------------------|
| DAPR sidecar | Manual container definitions | Annotation-based auto-injection |
| Components | File-mounted YAML | Kubernetes CRDs |
| Secrets | `.env` file | K8s Secrets |
| Networking | Docker network | K8s Service DNS |
| Auth | Keycloak (local) | External OIDC (required) |
| Scaling | Manual replicas | HPA / KEDA |
| Health probes | Docker HEALTHCHECK | K8s liveness/readiness probes |
| Manifest source | `aspire publish` → docker-compose.yaml | `aspire publish` → Helm chart |

### Page Template Convention

Follow the established documentation page structure:
1. Back link: `[<- Back to Hexalith.EventStore](../../README.md)`
2. H1 title
3. Opening paragraph explaining what the page covers and who it's for
4. `> **Prerequisites:** [link]` blockquote
5. Content sections with Mermaid diagrams where needed
6. Code blocks with both bash and PowerShell alternatives
7. Notes and tips as blockquotes
8. "Next Steps" section with links to related pages

### Existing Content to Reference (NOT duplicate)

- `deploy/README.md` — Already documents production DAPR components, backend compatibility matrix, per-backend env vars, K8s deployment steps, Aspire publisher commands. Link to this, don't duplicate.
- `docs/getting-started/quickstart.md` — Already covers the Aspire-based local development flow. The K8s guide builds upon this.
- `docs/concepts/architecture-overview.md` — Already covers system topology and DAPR building blocks. Link for context.
- Story 14-1 `docs/guides/deployment-docker-compose.md` — The Docker Compose guide. K8s guide should reference it and build upon shared concepts (FR59).

### Performance Targets (for Resource Requirements section)

- Command submission <50ms p99 (NFR1)
- End-to-end lifecycle <200ms p99 (NFR2)
- DAPR sidecar adds ~1-2ms per building block call (NFR8)
- Default 5-second DAPR sidecar call timeout
- Snapshot every 100 events (manages rehydration to ≤102 reads)

### Port Mappings

- CommandApi: 8080 (REST API, used as DAPR app-port)
- DAPR sidecar HTTP: 3500 (default in K8s)
- DAPR sidecar gRPC: 50001 (default in K8s)
- DAPR metrics: 9090 (for Prometheus scraping)

### Project Structure Notes

- `docs/guides/` directory exists with `.gitkeep` only. This is where `deployment-kubernetes.md` goes.
- `samples/deploy/` does NOT exist yet (only has `.gitkeep` equivalent from docs). Directory must be created along with `kubernetes/` subdirectory.
- `deploy/` directory already exists with production DAPR components and README.
- Documentation filename must follow NFR26: descriptive, unabbreviated, hyphen-separated (`deployment-kubernetes.md`).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 7, Story 7.2]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR23, FR26, FR57, FR58, FR59, FR60, FR63, NFR7, NFR22, NFR26]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#Deployment sections, K8s publisher]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs — Aspire K8s publisher, lines 74, 80-81]
- [Source: src/Hexalith.EventStore.ServiceDefaults/Extensions.cs — Health check endpoints]
- [Source: src/Hexalith.EventStore.CommandApi/HealthChecks/ — DAPR health check implementations]
- [Source: deploy/README.md — Production DAPR components, K8s deployment steps, Aspire publisher docs]
- [Source: deploy/dapr/ — Production DAPR component YAML files]
- [Source: _bmad-output/implementation-artifacts/14-1-docker-compose-deployment-guide-and-configuration.md — Previous story patterns]

## Dev Agent Record

### Agent Model Used

(to be filled by dev agent)

### Debug Log References

### Completion Notes List

### File List
