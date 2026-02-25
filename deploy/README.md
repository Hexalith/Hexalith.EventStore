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
    subscription-sample-counter.yaml  # Sample subscription template
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
|----------|-------------|---------|
| `POSTGRES_CONNECTION_STRING` | PostgreSQL connection string | `host=mydb.postgres.database.azure.com;port=5432;username=dapr;password=<secret>;database=eventstore;sslmode=require` |

### Cosmos DB State Store

**Config file:** `statestore-cosmosdb.yaml`

**Environment variables:**

| Variable | Description | Example |
|----------|-------------|---------|
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
- `SUBSCRIBER_APP_ID` (used in `pubsub-servicebus.yaml`): Authorized external subscriber app-id.
- `OPS_MONITOR_APP_ID` (used in `pubsub-servicebus.yaml`): Operational/monitoring subscriber app-id for dead-letter topics.

Set these before applying production templates to avoid unresolved/literal placeholder values.

## Secret Management

**Never store secrets in configuration files committed to source control (NFR14).**

Recommended approaches by platform:

- **Kubernetes:** Use DAPR's secret store component with Kubernetes Secrets or Azure Key Vault
- **Azure Container Apps:** Use managed identity with Azure Key Vault references
- **Docker Compose:** Use `.env` files (excluded from source control) with environment variable substitution

## Deployment Steps

### Docker Compose

1. Choose one state store config and one pub/sub config
2. Copy chosen files plus `resiliency.yaml` and `accesscontrol.yaml` to your DAPR components directory
3. Set required environment variables in your `.env` file
4. Mount the components directory in your `docker-compose.yaml`:

   ```yaml
   services:
     commandapi-dapr:
       image: "daprio/daprd:latest"
       volumes:
         - ./dapr-components:/components
       command: ["./daprd", "-app-id", "commandapi", "-components-path", "/components"]
   ```


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
