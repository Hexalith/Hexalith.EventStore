# Deployment Configuration

Production DAPR component configurations for deploying Hexalith.EventStore to different environments. Swap these YAML files to change backends with **zero application code changes, zero recompilation, zero redeployment** of application containers (NFR29).

## Directory Structure

```
deploy/
  dapr/
    statestore-postgresql.yaml    # PostgreSQL state store (production)
    statestore-cosmosdb.yaml      # Azure Cosmos DB state store (production)
    pubsub-rabbitmq.yaml          # RabbitMQ pub/sub (production)
    pubsub-kafka.yaml             # Kafka pub/sub (production)
    pubsub-servicebus.yaml        # Azure Service Bus pub/sub (production)
    resiliency.yaml               # Production resiliency policies
    accesscontrol.yaml            # Production access control (deny-by-default)
    subscription-sample-counter.yaml       # Sample subscription template
    subscription-projection-changed.yaml   # Projection change subscription
```

## Backend Compatibility Matrix

### State Store Backends

| Backend | DAPR Component Type | Config File | Key Features |
|---------|-------------------|-------------|--------------|
| Redis | `state.redis` | (local only) | Fast, simple setup |
| PostgreSQL | `state.postgresql` | `statestore-postgresql.yaml` | ACID transactions, range queries |
| Azure Cosmos DB | `state.azure.cosmosdb` | `statestore-cosmosdb.yaml` | Global distribution, elastic scale |

All state store backends support: ETag optimistic concurrency, actor state store, composite key pattern `{tenant}||{domain}||{aggregateId}`.

### Pub/Sub Backends

| Backend | DAPR Component Type | Config File | Key Features |
|---------|-------------------|-------------|--------------|
| Redis Streams | `pubsub.redis` | (local only) | Simple setup, development |
| RabbitMQ | `pubsub.rabbitmq` | `pubsub-rabbitmq.yaml` | Mature, flexible routing |
| Kafka | `pubsub.kafka` | `pubsub-kafka.yaml` | High throughput, log-based |
| Azure Service Bus | `pubsub.azure.servicebus.topics` | `pubsub-servicebus.yaml` | Native Azure, enterprise features |

All pub/sub backends support: CloudEvents 1.0, at-least-once delivery, dead-letter routing, per-tenant-per-domain topic isolation.

## Per-Backend Configuration

### PostgreSQL State Store

**Config file:** `statestore-postgresql.yaml`

**Environment variables:**

| Variable | Description | Example |
| ---------- | ------------- | --------- |
| `POSTGRES_CONNECTION_STRING` | PostgreSQL connection string | `host=mydb.postgres.database.azure.com;port=5432;username=dapr;password=<secret>;database=eventstore;sslmode=require` |

### Cosmos DB State Store

**Config file:** `statestore-cosmosdb.yaml`

**Environment variables:**

| Variable | Description | Example |
| ---------- | ------------- | --------- |
| `COSMOSDB_URL` | Account endpoint URL | `https://myaccount.documents.azure.com:443/` |
| `COSMOSDB_KEY` | Primary or secondary key | (from Azure Portal or Key Vault) |
| `COSMOSDB_DATABASE` | Database name | `eventstore` |
| `COSMOSDB_COLLECTION` | Container/collection name | `actorstate` |

### RabbitMQ Pub/Sub

**Config file:** `pubsub-rabbitmq.yaml`

**Environment variables:**

| Variable | Description | Example |
|----------|-------------|---------|
| `RABBITMQ_CONNECTION_STRING` | AMQP connection string | `amqp://user:pass@rabbitmq.example.com:5672/` |

### Kafka Pub/Sub

**Config file:** `pubsub-kafka.yaml`

**Environment variables:**

| Variable | Description | Example |
|----------|-------------|---------|
| `KAFKA_BROKERS` | Comma-separated broker addresses | `broker1:9092,broker2:9092` |
| `KAFKA_AUTH_TYPE` | Authentication type | `none`, `password`, `mtls`, or `oidc` |

### Azure Service Bus Pub/Sub

**Config file:** `pubsub-servicebus.yaml`

**Environment variables:**

| Variable | Description | Example |
|----------|-------------|---------|
| `SERVICEBUS_CONNECTION_STRING` | Service Bus connection string | `Endpoint=sb://mynamespace.servicebus.windows.net/;SharedAccessKeyName=dapr;SharedAccessKey=<key>` |

**Note:** Azure Service Bus does not auto-create topics. Pre-create topics matching the `{tenant}.{domain}.events` pattern before deployment.

### Access Control and Subscriber Scoping Template Variables

Some production templates use environment variable placeholders for environment-specific identity values:

- `DAPR_TRUST_DOMAIN` (used in `accesscontrol.yaml`): SPIFFE trust domain for mTLS identity validation.
- `DAPR_NAMESPACE` (used in `accesscontrol.yaml`): Kubernetes namespace where `commandapi` runs.
- `SUBSCRIBER_APP_ID` (used in all pub/sub configs): Authorized external subscriber app-id.
- `OPS_MONITOR_APP_ID` (used in all pub/sub configs): Operational/monitoring subscriber app-id for dead-letter topics.

Set these before applying production templates to avoid unresolved/literal placeholder values.

## Security Posture: Local vs. Production

| Aspect | Local Dev (`AppHost/DaprComponents/`) | Production (`deploy/dapr/`) |
|--------|---------------------------------------|---------------------------|
| Access Control | `defaultAction: allow` (no mTLS) | `defaultAction: deny` + mTLS (SPIFFE trust domain) |
| Component Scoping | Explicit scopes (e.g., `commandapi`) | Explicit `scopes: ["commandapi"]` on every component |
| Secrets | `appsettings.json` / env vars | K8s Secrets / Azure Key Vault |
| TLS | None (local loopback) | mTLS via DAPR sidecar (NFR9) |
| Resiliency | Constant retry (3 retries, 1s), shorter intervals | Exponential retry (10 retries, 15s max), longer intervals |

## Secret Management

**Never store secrets in configuration files committed to source control (NFR14).**

Recommended approaches by platform:

- **Kubernetes:** Use DAPR's secret store component with Kubernetes Secrets or Azure Key Vault
- **Azure Container Apps:** Use managed identity with Azure Key Vault references
- **Docker Compose:** Use `.env` files (excluded from source control) with environment variable substitution

**Production deployment recommendation:** Use GitOps (Argo CD, Flux) or Sealed Secrets to manage DAPR component YAMLs in production. Manual `kubectl apply` of component files is acceptable for staging, but production environments should track component configuration as versioned, reviewed infrastructure-as-code. DAPR component YAMLs contain infrastructure routing — unauthorized modification redirects all events.

## Deployment Steps

### Docker Compose

1. Choose one state store config and one pub/sub config
2. Copy chosen files plus `resiliency.yaml` to a `dapr-components/` directory, and `accesscontrol.yaml` to a separate `dapr-config/` directory (see [Docker Compose Publisher](#docker-compose-publisher) for why)
3. Set required environment variables in your `.env` file
4. Add DAPR sidecar containers — see the [reference override file pattern](#docker-compose-publisher) for a complete example


### Kubernetes

1. Apply chosen component configs as DAPR component resources:

   ```bash
   kubectl apply -f statestore-postgresql.yaml
   kubectl apply -f pubsub-rabbitmq.yaml
   kubectl apply -f resiliency.yaml
   kubectl apply -f accesscontrol.yaml
   ```

2. Create Kubernetes Secrets for connection strings referenced by environment variables
3. Configure DAPR annotations on your application pods:

   ```yaml
   annotations:
     dapr.io/enabled: "true"
     dapr.io/app-id: "commandapi"
     dapr.io/config: "accesscontrol"
   ```

### Azure Container Apps (Aspire Publisher)

1. Publish the Aspire app for Azure Container Apps and generate deployment artifacts.
2. Create/update Container Apps secrets for all required component values (for example: `POSTGRES_CONNECTION_STRING`, `SERVICEBUS_CONNECTION_STRING`, `DAPR_TRUST_DOMAIN`, `DAPR_NAMESPACE`, `SUBSCRIBER_APP_ID`, `OPS_MONITOR_APP_ID`).
3. Apply DAPR production components (`statestore-*`, `pubsub-*`, `resiliency.yaml`, `accesscontrol.yaml`) to the target environment, ensuring placeholders resolve from environment/secrets.
4. Configure the `commandapi` container app with matching environment variables and DAPR settings (app-id `commandapi`, config name `accesscontrol`).
5. Pre-create Azure Service Bus topics/subscriptions used by your tenant/domain topology before traffic cutover.
6. Verify component load and sidecar health through Container Apps logs and DAPR health endpoint checks before promoting traffic.

## Aspire Publisher Integration

The AppHost supports three Aspire publisher targets for generating deployment manifests. Publisher environments are configured in `src/Hexalith.EventStore.AppHost/Program.cs` and selected at publish time via the `PUBLISH_TARGET` environment variable.

**Prerequisites:** Install the Aspire CLI as a global tool: `dotnet tool install -g Aspire.Cli`

**Generated manifests are version-tied:** Publisher output format is specific to the Aspire SDK version (currently 13.1.2). When upgrading Aspire, regenerate manifests via `aspire publish` -- do not manually edit previously generated files.

### Docker Compose Publisher

**Command:**

```bash
PUBLISH_TARGET=docker aspire publish --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -o ./publish-output/docker
```

**PowerShell (Windows):**

```powershell
$env:PUBLISH_TARGET="docker"
aspire publish --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -o .\publish-output\docker
```

**Generated output:** `docker-compose.yaml` + `.env` file containing parameterized placeholders for container images, ports, and secrets.

**Services generated:** `commandapi`, `sample`, `keycloak` (when `EnableKeycloak` is not `false`), `docker-dashboard`.

**DAPR sidecar handling:** `CommunityToolkit.Aspire.Hosting.Dapr` is a local dev orchestration tool. The Docker Compose publisher does **NOT** generate DAPR sidecar containers. To add DAPR support:

1. Copy production DAPR components from `deploy/dapr/` into a `dapr-components/` directory mounted as a volume. Place the `accesscontrol.yaml` (a DAPR Configuration resource, `kind: Configuration`) in a separate `dapr-config/` directory to avoid DAPR loading it as both a config and a component.
2. Resolve environment variable placeholders in the `.env` file with production values.
3. Add sidecar containers via a `docker-compose.override.yml` file (keeps Aspire-generated `docker-compose.yaml` unchanged and reviewable):

**Reference override file pattern (`docker-compose.override.yml`):**

```yaml
services:
  commandapi-dapr:
    image: "daprio/daprd:1.16.1"
    network_mode: "service:commandapi"
    volumes:
      - ./dapr-components:/components
      - ./dapr-config:/config
    command: ["./daprd", "-app-id", "commandapi", "-app-port", "8080", "-components-path", "/components", "-config", "/config/accesscontrol.yaml"]
  sample-dapr:
    image: "daprio/daprd:1.16.1"
    network_mode: "service:sample"
    volumes:
      - ./dapr-components:/components
      - ./dapr-config:/config
    command: ["./daprd", "-app-id", "sample", "-app-port", "8080", "-components-path", "/components", "-config", "/config/accesscontrol.yaml"]
```

Pin the DAPR sidecar image to a specific version (e.g., `1.16.1`) — avoid mutable tags like `latest` (see [CI/CD Image Tagging](#cicd-image-tagging)).

### Kubernetes Publisher

**Command:**

```bash
PUBLISH_TARGET=k8s EnableKeycloak=false aspire publish --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -o ./publish-output/k8s
```

**PowerShell (Windows):**

```powershell
$env:PUBLISH_TARGET="k8s"
$env:EnableKeycloak="false"
aspire publish --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -o .\publish-output\k8s
```

**Note:** `EnableKeycloak=false` is required because the Kubernetes publisher does not support bind mounts (used by Keycloak's realm import). For production Kubernetes deployments, use an external OIDC provider instead of Keycloak (see [External OIDC Configuration](#external-oidc-configuration-for-production)).

**Generated output:** Helm chart with `Chart.yaml`, `values.yaml`, and templates containing Deployments, Services, and ConfigMaps for `commandapi` and `sample`.

**DAPR annotation handling:** The Kubernetes publisher does **NOT** auto-generate DAPR annotations on pod templates. To add DAPR support:

1. Add DAPR annotations to each Deployment's pod template:

   ```yaml
   spec:
     template:
       metadata:
         annotations:
           dapr.io/enabled: "true"
           dapr.io/app-id: "commandapi"
           dapr.io/app-port: "8080"
           dapr.io/config: "accesscontrol"
   ```

2. Install the DAPR operator in your Kubernetes cluster.
3. Apply production DAPR components as Kubernetes CRDs:

   ```bash
   kubectl apply -f deploy/dapr/statestore-postgresql.yaml
   kubectl apply -f deploy/dapr/pubsub-rabbitmq.yaml
   kubectl apply -f deploy/dapr/resiliency.yaml
   kubectl apply -f deploy/dapr/accesscontrol.yaml
   ```

4. Create Kubernetes Secrets for connection strings referenced by environment variables.

### Azure Container Apps Publisher

**Command:**

```bash
PUBLISH_TARGET=aca EnableKeycloak=false aspire publish --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -o ./publish-output/azure
```

**PowerShell (Windows):**

```powershell
$env:PUBLISH_TARGET="aca"
$env:EnableKeycloak="false"
aspire publish --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -o .\publish-output\azure
```

**Note:** `EnableKeycloak=false` is recommended for production ACA deployments. Use an external OIDC provider.

**Generated output:** Bicep modules including: `main.bicep` (subscription-scoped orchestrator), ACR module (Azure Container Registry), Container Apps Environment module, and per-service modules (`commandapi.bicep`, `sample.bicep`) with managed identity.

**DAPR configuration handling:** The Bicep output does **NOT** include DAPR configuration. Azure Container Apps has native DAPR support that must be configured separately:

1. Enable DAPR in the Container Apps Environment via Bicep or Azure Portal.
2. Configure each container app with DAPR settings (app-id, app-port).
3. Create DAPR components in the Container Apps Environment for state store and pub/sub, referencing the production configs from `deploy/dapr/`.
4. Use managed identity for secret references where possible.

### External OIDC Configuration for Production

Publisher manifests exclude Keycloak when `EnableKeycloak=false`. For production auth, configure an external OIDC provider via these environment variables on the `commandapi` container:

| Variable | Description | Example |
| ---------- | ------------- | --------- |
| `Authentication__JwtBearer__Authority` | OIDC discovery URL (issuer) | `https://login.microsoftonline.com/{tenant}/v2.0` |
| `Authentication__JwtBearer__Issuer` | Expected token issuer | `https://login.microsoftonline.com/{tenant}/v2.0` |
| `Authentication__JwtBearer__Audience` | Expected token audience | `api://hexalith-eventstore` |
| `Authentication__JwtBearer__RequireHttpsMetadata` | Require HTTPS for metadata | `true` (recommended for production) |

When `Authentication__JwtBearer__Authority` is set, the application uses OIDC discovery to validate tokens. When it is not set, it falls back to symmetric key validation via `Authentication__JwtBearer__SigningKey`.

## CI/CD Image Tagging

The `deploy-staging.yml` workflow uses `staging-latest` mutable tags for staging deployments. For production, use immutable tags — either a SemVer tag (e.g., `v1.2.3`) or a SHA digest (e.g., `@sha256:abc123...`). Mutable tags like `latest` or `staging-latest` mean rollbacks may silently pick up a different image than the one originally deployed.

## Validating Configuration

After deployment, verify the configuration:

1. **DAPR sidecar health:** `GET http://localhost:3500/v1.0/healthz` returns 200
2. **Component loaded:** Check DAPR sidecar logs for `component loaded. name: statestore` and `component loaded. name: pubsub`
3. **Actor state store:** Verify actor activation succeeds by sending a test command
4. **Pub/sub delivery:** Verify events publish and subscribers receive them
5. **Dead-letter routing:** Simulate a subscriber failure to confirm dead-letter topic receives the message
6. **Access control:** Verify unauthorized app-ids receive 403 Forbidden from DAPR sidecar

## Adding New Domain Services

When adding a new domain service to the production deployment:

1. **Do NOT** add it to state store scopes -- domain services have zero state store access (D4)
2. **Do NOT** add it to pub/sub component scopes -- domain services have zero pub/sub access (D4)
3. Add its app-id to `accesscontrol.yaml` following the template in the file comments
4. If the domain service needs to receive invocations from commandapi, ensure its `defaultAction: deny` policy allows the specific operations needed
5. Update subscription files if the domain service processes events from specific topics

## Adding New Subscriber Services

When adding a new external event subscriber:

1. Add the subscriber app-id to pub/sub `scopes` list
2. Add topic authorization to `subscriptionScopes` metadata
3. Deny publishing via `publishingScopes` metadata (subscribers should never publish)
4. Create a declarative subscription YAML (see `subscription-sample-counter.yaml` as template)
5. Update access control if the subscriber needs to invoke other services

See inline documentation in each pub/sub config file for detailed step-by-step instructions.

## Adding a New Backend

The NFR29 portability guarantee means any DAPR-compatible backend works — not just the pre-built configs in `deploy/dapr/`. To add a new backend (e.g., Azure Table Storage, DynamoDB, NATS):

### State Store

1. Find the DAPR component spec for your backend at [DAPR State Store Components](https://docs.dapr.io/reference/components-reference/supported-state-stores/)
2. Create a new YAML file in `deploy/dapr/` following the pattern in `statestore-postgresql.yaml`
3. Required metadata fields:
   - `actorStateStore: "true"` (required for DAPR actors)
   - Connection credentials via `{env:VAR_NAME}` references (never hardcode)
4. The component `metadata.name` **must** be `statestore` (matches what the application code references)
5. Add `scopes: ["commandapi"]` (D4 requirement — only commandapi accesses the state store)
6. Verify the backend supports ETag-based optimistic concurrency (D1 hard requirement)

### Pub/Sub

1. Find the DAPR component spec at [DAPR Pub/Sub Components](https://docs.dapr.io/reference/components-reference/supported-pubsub/)
2. Create a new YAML file following the pattern in `pubsub-rabbitmq.yaml`
3. Required metadata fields:
   - Configure dead-letter handling using backend-supported metadata (for example: `enableDeadLetter` + `deadLetterTopic` where supported)
   - `publishingScopes` and `subscriptionScopes` (copy from existing pub/sub configs)
   - Connection credentials via `{env:VAR_NAME}` references
4. The component `metadata.name` **must** be `pubsub` (matches what the application code references)
5. Add `scopes` list: `["commandapi", "{env:SUBSCRIBER_APP_ID}", "{env:OPS_MONITOR_APP_ID}"]` — must include `commandapi` (publisher) plus any authorized subscriber app-ids
6. Verify the backend supports CloudEvents 1.0 and at-least-once delivery
