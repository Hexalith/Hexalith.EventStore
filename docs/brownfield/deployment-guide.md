# Deployment Guide — Hexalith.EventStore

> Code- and config-derived. See the hand-authored `docs/guides/deployment-*.md`,
> `docs/guides/deployment-progression.md`, `docs/guides/disaster-recovery.md`, and `deploy/README.md`.

## Container Images (6)

Built via **.NET SDK container support** — **no Dockerfiles**. Defaults centralized in
`Directory.Build.targets`; opt-in per project with `<EnableContainer>true</EnableContainer>` +
`<ContainerRepository>image-name</ContainerRepository>`.

Defaults: base `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`, registry `registry.hexalith.com`,
non-root user `app`, port `8080`, OCI labels (source/licenses/vendor), tag `staging-latest`.

| Project | Image |
|---------|-------|
| `src/Hexalith.EventStore` | `registry.hexalith.com/eventstore` |
| `src/Hexalith.EventStore.Admin.Server.Host` | `registry.hexalith.com/eventstore-admin` |
| `src/Hexalith.EventStore.Admin.UI` | `registry.hexalith.com/eventstore-admin-ui` |
| `samples/Hexalith.EventStore.Sample` | `registry.hexalith.com/sample` |
| `samples/Hexalith.EventStore.Sample.BlazorUI` | `registry.hexalith.com/sample-blazor-ui` |
| `Hexalith.Tenants/src/Hexalith.Tenants` (submodule) | `registry.hexalith.com/tenants` |

```bash
# Publish one image to a local tar (no registry push)
dotnet publish src/Hexalith.EventStore/Hexalith.EventStore.csproj \
  --configuration Release -t:PublishContainer \
  -p:ContainerArchiveOutputPath=/tmp/eventstore.tar.gz

# Push to registry (needs SDK_CONTAINER_REGISTRY_UNAME / _PWORD)
dotnet publish src/Hexalith.EventStore/Hexalith.EventStore.csproj \
  --configuration Release -t:PublishContainer \
  -p:ContainerImageTags="staging-latest;staging-$(git rev-parse HEAD)"
```

The Admin.Cli is a **NuGet tool** (`PackAsTool`, `ToolCommandName=eventstore-admin`); the Admin.Mcp is
a standalone **exe** (no container).

## Aspire Publish Targets

The AppHost supports publish targets via `PUBLISH_TARGET`:

| Target | Output |
|--------|--------|
| `docker` | Docker Compose (`docker-compose.yaml` + `.env`) |
| `k8s` | Kubernetes (Helm charts / manifests) |
| `aca` | Azure Container Apps (Bicep) |

Aspire packages referenced: `Aspire.Hosting.Docker`, `Aspire.Hosting.Kubernetes`,
`Aspire.Hosting.Azure.AppContainers`, `Aspire.Hosting.Redis`, `Aspire.Hosting.Keycloak` (all `13.4.0`).

## DAPR Component Configuration (the deployment contract)

DAPR is the infrastructure abstraction: **swap the component YAML, change zero application code**
(NFR29). Production component files live in `deploy/dapr/`.

### State store (choose one)

| Backend | File | Env |
|---------|------|-----|
| PostgreSQL | `deploy/dapr/statestore-postgresql.yaml` | `POSTGRES_CONNECTION_STRING` |
| Cosmos DB | `deploy/dapr/statestore-cosmosdb.yaml` | `COSMOSDB_URL`, `COSMOSDB_KEY`, `COSMOSDB_DATABASE`, `COSMOSDB_COLLECTION` |
| Redis (dev) | AppHost `DaprComponents/statestore.yaml` | — |

Must be an **actor state store** (`actorStateStore=true`), `keyPrefix=none` so `eventstore-admin`
reads the same keys. Composite key pattern: `{tenant}||{domain}||{aggregateId}`.

### Pub/Sub (choose one)

| Backend | File | Notes |
|---------|------|-------|
| RabbitMQ | `deploy/dapr/pubsub-rabbitmq.yaml` | durable queues + DLX; `RABBITMQ_CONNECTION_STRING` |
| Kafka | `deploy/dapr/pubsub-kafka.yaml` | per-partition ordering (partition key not yet emitted) |
| Azure Service Bus | `deploy/dapr/pubsub-servicebus.yaml` | session ordering (SessionId not yet emitted) |
| Redis (dev) | AppHost `DaprComponents/pubsub.yaml` | — |

**3-layer scoping** in every pub/sub component: component `scopes`, `publishingScopes`,
`subscriptionScopes`. DAPR treats `*` literally — omit scopes for unrestricted access.

### Access control & resiliency

- `deploy/dapr/accesscontrol.yaml` (+ `.eventstore-admin`, `.sample`): per-service Configuration CRDs.
  **Production must be deny-by-default with mTLS**; self-hosted/dev mode is allow-by-default.
- `deploy/dapr/resiliency.yaml`: retry/timeout/circuit-breaker. Pub/sub circuit breaker opens on >5
  consecutive failures → triggers `AggregateActor` `PublishFailed` → drain recovery.
- `deploy/dapr/subscription-*.yaml`: declarative subscriptions with per-subscription dead-letter routing.

### Domain-service isolation (D4)

Domain services (`sample`, `tenants`) reference **no** state-store/pub-sub components (empty DAPR
resources path) — stricter than scoping alone. They interact only via DAPR service invocation from the
event store.

## Secrets

See `docs/ci-secrets-checklist.md`. Container push needs `SDK_CONTAINER_REGISTRY_UNAME` /
`SDK_CONTAINER_REGISTRY_PWORD`. State-store/pub-sub credentials come from env vars referenced in the
DAPR component YAML (production should use a DAPR secret store, not inline secrets).

## Auth in production

JWT bearer via OIDC discovery (`Authentication:JwtBearer:Authority` → Keycloak realm `hexalith-realm`).
Symmetric-key (`SigningKey`) is for dev/test only. Service-to-service calls use the composite "Hexalith"
scheme with trusted `dapr-caller-app-id` callers (`Authentication:DaprInternal:AllowedCallers`).
See `docs/guides/security-model.md`.

## Caveats from the scan

- **No `.github/workflows/` in this checkout** — deployment automation may live elsewhere; verify
  before assuming an image build/publish pipeline exists here.
- Kafka partition keys and Service Bus SessionIds are **not yet emitted** by the event publisher, so
  strict per-key ordering on those brokers is not yet guaranteed (per `deploy/README.md`).
