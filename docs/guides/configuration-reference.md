[<- Back to Hexalith.EventStore](../../README.md)

# Configuration Reference

This is the complete configuration reference for all Hexalith.EventStore settings. It documents every configurable knob — application settings, authentication, the fluent client SDK, environment variables, Aspire orchestration, DAPR infrastructure, and health and observability endpoints. Use this page to understand what each setting does, what its default value is, and how to override it for your environment. Configuration is what enables Hexalith's core promise: swap infrastructure backends with zero code changes.

> **Prerequisites:** [Prerequisites and Local Dev Environment](../getting-started/prerequisites.md), [Quickstart Guide](../getting-started/quickstart.md)

## Application Settings

All application settings live in `appsettings.json` (or environment-specific variants like `appsettings.Production.json`) under the `EventStore` section. They follow the standard .NET configuration hierarchy: `appsettings.json` → `appsettings.{Environment}.json` → environment variables → command-line arguments. Later sources override earlier ones.

### Rate Limiting

Rate limiting protects the Command API from excessive requests using a per-tenant sliding window. Health endpoints (`/health`, `/alive`, `/ready`) are excluded from rate limiting.

Configuration section: `EventStore:RateLimiting`

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `PermitLimit` | int | `100` | Maximum requests per window per tenant |
| `WindowSeconds` | int | `60` | Sliding window duration in seconds |
| `SegmentsPerWindow` | int | `6` | Number of segments within the window for smoother rate tracking |
| `QueueLimit` | int | `0` | Queue depth for requests exceeding the limit. `0` means immediate `429 Too Many Requests` rejection |

```json
{
  "EventStore": {
    "RateLimiting": {
      "PermitLimit": 200,
      "WindowSeconds": 30,
      "SegmentsPerWindow": 3,
      "QueueLimit": 10
    }
  }
}
```

> **Tip:** Setting `QueueLimit` to `0` (the default) gives clients immediate feedback when rate limited, which is usually the best choice for API consumers that implement their own retry logic.

### Extension Metadata

Extension metadata allows clients to attach custom key-value pairs to commands. These limits prevent payload abuse.

Configuration section: `EventStore:ExtensionMetadata`

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MaxTotalSizeBytes` | int | `4096` | Maximum total size of all extension metadata in bytes |
| `MaxKeyLength` | int | `128` | Maximum length of a single metadata key in characters |
| `MaxValueLength` | int | `2048` | Maximum length of a single metadata value in characters |
| `MaxExtensionCount` | int | `32` | Maximum number of extension key-value pairs per command |

```json
{
  "EventStore": {
    "ExtensionMetadata": {
      "MaxTotalSizeBytes": 8192,
      "MaxKeyLength": 64,
      "MaxValueLength": 4096,
      "MaxExtensionCount": 16
    }
  }
}
```

### Event Publisher

Controls how events are published to DAPR pub/sub after persistence.

Configuration section: `EventStore:Publisher`

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `PubSubName` | string | `"pubsub"` | DAPR pub/sub component name to publish events to |
| `DeadLetterTopicPrefix` | string | `"deadletter"` | Prefix for dead-letter topics. Format: `{prefix}.{tenant}.{domain}.events` |

```json
{
  "EventStore": {
    "Publisher": {
      "PubSubName": "eventbus",
      "DeadLetterTopicPrefix": "dlq"
    }
  }
}
```

### Event Drain / Recovery

The event drain mechanism retries publishing events that failed their initial pub/sub delivery. It uses exponential backoff between the minimum and maximum drain periods.

Configuration section: `EventStore:Drain`

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `InitialDrainDelay` | TimeSpan | `00:00:30` | Delay before the first drain attempt after a publish failure |
| `DrainPeriod` | TimeSpan | `00:01:00` | Base recurring retry interval |
| `MaxDrainPeriod` | TimeSpan | `00:30:00` | Upper bound for retry intervals (prevents infinite backoff growth) |

```json
{
  "EventStore": {
    "Drain": {
      "InitialDrainDelay": "00:00:15",
      "DrainPeriod": "00:02:00",
      "MaxDrainPeriod": "01:00:00"
    }
  }
}
```

### Snapshots

Snapshots are periodic state checkpoints that speed up aggregate rehydration. Instead of replaying all events from the beginning, the system loads the latest snapshot and replays only the events after it.

Configuration section: `EventStore:Snapshots`

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `DefaultInterval` | int | `100` | Number of events between automatic snapshots. Minimum: `10` |
| `DomainIntervals:{domainName}` | int | — | Per-domain override for snapshot interval |

```json
{
  "EventStore": {
    "Snapshots": {
      "DefaultInterval": 50,
      "DomainIntervals": {
        "orders": 25,
        "inventory": 200
      }
    }
  }
}
```

> **Warning:** Setting `DefaultInterval` below `10` will cause a validation error at startup. Low intervals increase state store write volume.

### Command Status

Command status entries allow clients to poll for the outcome of submitted commands. Entries are ephemeral and expire after the configured TTL.

Configuration section: `EventStore:CommandStatus`

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `TtlSeconds` | int | `86400` | Time-to-live for status entries in seconds (default: 24 hours) |
| `StateStoreName` | string | `"eventstore"` | DAPR state store component name for status persistence |

```json
{
  "EventStore": {
    "CommandStatus": {
      "TtlSeconds": 43200,
      "StateStoreName": "commandstatus"
    }
  }
}
```

### Domain Services

Domain services are the aggregate processors that handle commands and produce events. They can be registered statically in configuration or discovered dynamically via DAPR configuration store. The version field in registrations enables running multiple versions of the same domain service simultaneously — see [Event Versioning — Domain Service Version Routing](../concepts/event-versioning.md#domain-service-version-routing) for deployment patterns and rollback strategy.

Configuration section: `EventStore:DomainServices`

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `ConfigStoreName` | string | `"configstore"` | DAPR configuration store component name |
| `InvocationTimeoutSeconds` | int | `5` | Timeout in seconds for DAPR sidecar service invocation calls |
| `MaxEventsPerResult` | int | `1000` | Maximum number of events a single domain operation can produce |
| `MaxEventSizeBytes` | int | `1048576` | Maximum size of a single event in bytes (default: 1 MB) |
| `Registrations:{key}` | object | — | Static domain service registrations keyed by `"{tenant}\|{domain}\|{version}"` |

Each registration entry has:

| Field | Type | Description |
|-------|------|-------------|
| `AppId` | string | DAPR app-id of the service hosting this domain |
| `MethodName` | string | Method name to invoke on the service |
| `TenantId` | string | Tenant identifier |
| `Domain` | string | Domain name |
| `Version` | string | Domain version |

```json
{
  "EventStore": {
    "DomainServices": {
      "ConfigStoreName": "configstore",
      "InvocationTimeoutSeconds": 10,
      "MaxEventsPerResult": 500,
      "MaxEventSizeBytes": 524288,
      "Registrations": {
        "tenant-a|orders|v1": {
          "AppId": "order-service",
          "MethodName": "process",
          "TenantId": "tenant-a",
          "Domain": "orders",
          "Version": "v1"
        }
      }
    }
  }
}
```

### OpenAPI

Controls the Swagger UI endpoint for exploring the Command API interactively.

Configuration section: `EventStore:OpenApi`

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `true` | Enable the `/swagger` endpoint. Set to `false` in production if you do not want API documentation exposed |

```json
{
  "EventStore": {
    "OpenApi": {
      "Enabled": false
    }
  }
}
```

## Authentication and JWT

Authentication settings configure how the Command API validates incoming JWT tokens. You must provide either an OIDC `Authority` (for production) or a `SigningKey` (for development and testing). Both `Issuer` and `Audience` are always required.

Configuration section: `Authentication:JwtBearer`

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Authority` | string | `""` | OIDC authority URL (e.g., `https://keycloak.example.com/realms/hexalith`). Used in production for automatic key discovery |
| `Audience` | string | `""` | Expected JWT audience claim. **Required** |
| `Issuer` | string | `""` | Expected JWT issuer claim. **Required** |
| `SigningKey` | string | `""` | Symmetric signing key for development/testing. Must be at least 32 characters for HS256 |
| `RequireHttpsMetadata` | bool | `true` | Require HTTPS when fetching OIDC metadata. Set to `false` only for local development |

```json
{
  "Authentication": {
    "JwtBearer": {
      "Authority": "https://keycloak.example.com/realms/hexalith",
      "Audience": "hexalith-eventstore",
      "Issuer": "https://keycloak.example.com/realms/hexalith",
      "RequireHttpsMetadata": true
    }
  }
}
```

> **Warning:** Never commit a `SigningKey` to source control. Use environment variables or a secret manager for production deployments. See the [Security Model](security-model.md) for the full authentication flow.

**Validation rules:**

- Either `Authority` or `SigningKey` must be set (not both empty)
- `Issuer` and `Audience` are always required
- When `Authority` is set, the system uses OIDC discovery to fetch signing keys automatically
- When `SigningKey` is set (development mode), it must be at least 32 characters

## Fluent Client SDK Configuration

The fluent client SDK provides a programmatic API for registering domain services. Configuration follows a 5-layer cascade where each layer can override the previous one. This cascade is what enables zero-code-change backend swapping — convention defaults handle the common case, and you only override what differs per environment.

### 5-Layer Configuration Cascade

Settings are resolved in this order (lowest to highest priority):

| Priority | Layer | Source | Example |
|----------|-------|--------|---------|
| 1 (lowest) | Convention defaults | `NamingConventionEngine` | State store: `{domain}-eventstore`, topic: `{domain}.events` |
| 2 | Global code options | `EventStoreOptions` in `AddEventStore()` | `options.DefaultStateStoreSuffix = "store"` |
| 3 | Domain self-config | `OnConfiguring()` override in aggregate | Per-aggregate resource naming |
| 4 | External config | `appsettings.json` section | Environment-specific overrides without code changes |
| 5 (highest) | Explicit override | `ConfigureDomain()` callback | Test or special-case overrides |

This means you can define sensible defaults in code (layers 1-3) and override them per environment using external configuration (layer 4) — no rebuild required.

### EventStoreOptions (Global)

These options apply to all domains unless overridden at the domain level.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `EnableRegistrationDiagnostics` | bool | `false` | Log detailed registration information at startup for debugging discovery issues |
| `DefaultStateStoreSuffix` | string | `"eventstore"` | Suffix appended to domain name for state store names. Produces `{domain}-eventstore` |
| `DefaultTopicSuffix` | string | `"events"` | Suffix appended to domain name for topic names. Produces `{domain}.events` |

```csharp
builder.Services.AddEventStore(options =>
{
    options.EnableRegistrationDiagnostics = true;
    options.DefaultStateStoreSuffix = "store";
    options.DefaultTopicSuffix = "evt";
});
```

### EventStoreDomainOptions (Per-Domain)

Per-domain options override the convention-based resource naming for a specific domain. All properties are nullable — `null` means "use convention default."

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `StateStoreName` | string? | `null` | Override the state store component name for this domain |
| `TopicPattern` | string? | `null` | Override the topic naming pattern for this domain |
| `DeadLetterTopicPattern` | string? | `null` | Override the dead-letter topic naming pattern for this domain |

### External Configuration (Layer 4): `EventStore:Domains:{domain}`

Layer 4 lets you override per-domain settings from external configuration without recompiling. The section key is the resolved domain name from conventions/attributes.

Configuration section pattern: `EventStore:Domains:{domain}`

| Setting | Type | Default | Valid Values | Description |
|---------|------|---------|--------------|-------------|
| `StateStoreName` | string? | `null` | `null` or non-empty component name | Per-domain state store component override |
| `TopicPattern` | string? | `null` | `null` or non-empty topic pattern | Per-domain topic naming override |
| `DeadLetterTopicPattern` | string? | `null` | `null` or non-empty dead-letter topic pattern | Per-domain dead-letter topic override |

```json
{
  "EventStore": {
    "Domains": {
      "counter": {
        "StateStoreName": "counter-postgresql",
        "TopicPattern": "counter.v2.events",
        "DeadLetterTopicPattern": "deadletter.tenant-a.counter.v2.events"
      }
    }
  }
}
```

> **Note:** `EventStore:Domains:{domain}` is Layer 4 in the five-layer cascade, between `OnConfiguring()` (Layer 3) and `ConfigureDomain()` explicit overrides (Layer 5).

```csharp
builder.Services.AddEventStore(options =>
{
    options.ConfigureDomain<OrderAggregate>(domain =>
    {
        domain.StateStoreName = "orders-postgresql";
        domain.TopicPattern = "orders.v2.events";
    });
});
```

### Convention Engine Resource Names

The `NamingConventionEngine` generates resource names from domain metadata. Understanding these conventions helps you know when to override and when the defaults are sufficient.

| Resource | Convention | Example (domain: `counter`) |
|----------|-----------|------|
| State Store | `{domain}-{DefaultStateStoreSuffix}` | `counter-eventstore` |
| Topic | `{domain}.{DefaultTopicSuffix}` | `counter.events` |
| Dead-Letter Topic | `{DeadLetterTopicPrefix}.{tenant}.{domain}.{DefaultTopicSuffix}` | `deadletter.tenant-a.counter.events` |

## DAPR Infrastructure

Hexalith.EventStore uses five DAPR building blocks to abstract infrastructure concerns. DAPR component configuration is done through YAML files — not application settings — which is what enables zero-code-change backend swapping.

| Building Block | Purpose in Hexalith | Component Types |
|----------------|---------------------|-----------------|
| State Store | Actor state, event snapshots, command status tracking | `state.redis`, `state.postgresql`, `state.azure.cosmosdb` |
| Pub/Sub | Event distribution with per-tenant-per-domain topics | `pubsub.redis`, `pubsub.rabbitmq`, `pubsub.kafka`, `pubsub.azure.servicebus.topics` |
| Actors | Aggregate lifecycle management (turn-based concurrency) | Enabled via `actorStateStore: "true"` on the state store |
| Configuration | Domain service registration and dynamic config | `configuration.redis` |
| Resiliency | Retry, timeout, and circuit breaker policies | `Resiliency` resource |

> **Note:** DAPR component YAML is documented in full in the [DAPR Component Configuration Reference](dapr-component-reference.md). This section provides an overview — refer to that page for complete, copy-pasteable YAML examples per backend.

### Backend Compatibility by Environment

| Environment | State Store | Pub/Sub | Configuration |
|-------------|-------------|---------|---------------|
| Local development | Redis | Redis | Redis |
| On-premise production | PostgreSQL | RabbitMQ or Kafka | Redis |
| Azure cloud | Cosmos DB | Azure Service Bus | Redis |

To swap backends, you change the DAPR component YAML files and (optionally) set environment variables for connection strings. No application code changes are needed. See the [Deployment Progression Guide](deployment-progression.md) for a detailed walkthrough.

## Environment Variables

Environment variables configure infrastructure connections and operational behavior. They are the primary mechanism for injecting secrets and environment-specific values in containerized deployments.

### OpenTelemetry

| Variable | Default | Description |
|----------|---------|-------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | (empty) | OpenTelemetry OTLP exporter endpoint (e.g., `http://localhost:4317`). When set, metrics and traces are exported to the configured collector |

### DAPR

| Variable | Default | Description |
|----------|---------|-------------|
| `DAPR_HTTP_PORT` | (auto) | Override the DAPR sidecar HTTP port. Normally auto-assigned by DAPR |
| `DAPR_TRUST_DOMAIN` | `"hexalith.io"` | SPIFFE trust domain for mTLS between services |
| `DAPR_NAMESPACE` | `"hexalith"` | Kubernetes namespace used in DAPR access control policies |

### Infrastructure

| Variable | Default | Description |
|----------|---------|-------------|
| `REDIS_HOST` | `"localhost:6379"` | Redis host and port for DAPR components (local development) |
| `REDIS_PASSWORD` | (empty) | Redis password. Leave empty for local development without auth |
| `POSTGRES_CONNECTION_STRING` | (N/A) | PostgreSQL connection string for production state store |
| `RABBITMQ_CONNECTION_STRING` | (N/A) | RabbitMQ connection string for production pub/sub |
| `SUBSCRIBER_APP_ID` | (N/A) | DAPR app-id of the event subscriber service (production pub/sub routing) |
| `OPS_MONITOR_APP_ID` | (N/A) | DAPR app-id of the operations monitor service (dead-letter routing) |

```bash
# Local development (Docker Compose)
export REDIS_HOST="redis:6379"
export REDIS_PASSWORD=""

# Production (Kubernetes)
export POSTGRES_CONNECTION_STRING="Host=db.internal;Database=eventstore;Username=app;Password=secret"
export RABBITMQ_CONNECTION_STRING="amqp://user:pass@rabbitmq.internal:5672"
export DAPR_TRUST_DOMAIN="mycompany.io"
export DAPR_NAMESPACE="production"
```

> **Warning:** Never hardcode secrets in environment variable files committed to source control. Use Kubernetes secrets, Azure Key Vault references, or your platform's secret management for production.

## Aspire Orchestration

The .NET Aspire AppHost orchestrates the full local development topology — the Command API, DAPR sidecars, Redis, Keycloak, and the sample application. Configuration is done through environment variables and Aspire's resource builder API.

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableKeycloak` | `"true"` | Set to `"false"` to disable the Keycloak identity provider in the local Aspire topology. Useful when testing without authentication |
| `PUBLISH_TARGET` | (empty) | Aspire publisher target for deployment manifest generation: `"docker"`, `"k8s"`, or `"aca"` |

```bash
# Run without Keycloak
EnableKeycloak=false dotnet run --project src/Hexalith.EventStore.AppHost

# Generate Kubernetes deployment manifests
PUBLISH_TARGET=k8s dotnet run --project src/Hexalith.EventStore.AppHost -- publish
```

The Aspire AppHost also configures:

- **DAPR sidecar injection** for all services (auto-provisioned)
- **Redis** as the default state store, pub/sub, and configuration store for local development
- **Keycloak** as the OIDC provider (when enabled)
- **OpenTelemetry** collection through the Aspire dashboard

> **Tip:** Run `dotnet run --project src/Hexalith.EventStore.AppHost` to start the complete local topology. The Aspire dashboard at `https://localhost:17225` shows all resources, logs, traces, and metrics.

## Health and Observability

### Health Check Endpoints

The Command API exposes three health check endpoints. All are excluded from authentication and rate limiting.

| Endpoint | Purpose | Checks | Status Codes |
|----------|---------|--------|--------------|
| `/health` | Full health check | All registered health checks | `200` (Healthy/Degraded), `503` (Unhealthy) |
| `/alive` | Liveness probe | Only checks tagged `"live"` | `200` (Healthy/Degraded), `503` (Unhealthy) |
| `/ready` | Readiness probe | Only checks tagged `"ready"` | `200` (Healthy/Degraded), `503` (Unhealthy) |

Use `/alive` for Kubernetes liveness probes (restart on failure) and `/ready` for readiness probes (stop routing traffic until ready).

### OpenTelemetry

Hexalith.EventStore exports metrics and traces via the OpenTelemetry Protocol (OTLP) when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

**Metrics instrumentation:**

| Source | What It Measures |
|--------|-----------------|
| ASP.NET Core | Request duration, request count, response size |
| HttpClient | Outbound HTTP call duration and status |
| .NET Runtime | GC collections, thread pool, memory usage |

**Trace instrumentation:**

| Source | What It Traces |
|--------|----------------|
| Custom activity sources | Command processing, event persistence, event publishing, actor activation |
| ASP.NET Core | Inbound HTTP request spans |
| HttpClient | Outbound HTTP call spans |

**Structured logging:**

All log output uses JSON format with UTC timestamps via the .NET console logger. Log levels are configured in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## Quick-Reference Summary Table

This table lists every configurable setting for quick scanning, including explicit valid values.

| Setting | Type | Default | Valid Values | Category |
|---------|------|---------|--------------|----------|
| `EventStore:RateLimiting:PermitLimit` | int | `100` | Integer `> 0` | Application |
| `EventStore:RateLimiting:WindowSeconds` | int | `60` | Integer `> 0` | Application |
| `EventStore:RateLimiting:SegmentsPerWindow` | int | `6` | Integer `>= 1` | Application |
| `EventStore:RateLimiting:QueueLimit` | int | `0` | Integer `>= 0` | Application |
| `EventStore:ExtensionMetadata:MaxTotalSizeBytes` | int | `4096` | Integer `> 0` | Application |
| `EventStore:ExtensionMetadata:MaxKeyLength` | int | `128` | Integer `> 0` | Application |
| `EventStore:ExtensionMetadata:MaxValueLength` | int | `2048` | Integer `> 0` | Application |
| `EventStore:ExtensionMetadata:MaxExtensionCount` | int | `32` | Integer `> 0` | Application |
| `EventStore:Publisher:PubSubName` | string | `"pubsub"` | Non-empty string | Application |
| `EventStore:Publisher:DeadLetterTopicPrefix` | string | `"deadletter"` | Non-empty string | Application |
| `EventStore:Drain:InitialDrainDelay` | TimeSpan | `00:00:30` | TimeSpan `>= 00:00:00` | Application |
| `EventStore:Drain:DrainPeriod` | TimeSpan | `00:01:00` | TimeSpan `> 00:00:00` | Application |
| `EventStore:Drain:MaxDrainPeriod` | TimeSpan | `00:30:00` | TimeSpan `>= DrainPeriod` | Application |
| `EventStore:Snapshots:DefaultInterval` | int | `100` | Integer `>= 10` | Application |
| `EventStore:Snapshots:DomainIntervals:{name}` | int | — | Integer `>= 10` | Application |
| `EventStore:CommandStatus:TtlSeconds` | int | `86400` | Integer `> 0` | Application |
| `EventStore:CommandStatus:StateStoreName` | string | `"eventstore"` | Non-empty string | Application |
| `EventStore:DomainServices:ConfigStoreName` | string | `"configstore"` | Non-empty string | Application |
| `EventStore:DomainServices:InvocationTimeoutSeconds` | int | `5` | Integer `> 0` | Application |
| `EventStore:DomainServices:MaxEventsPerResult` | int | `1000` | Integer `> 0` | Application |
| `EventStore:DomainServices:MaxEventSizeBytes` | int | `1048576` | Integer `> 0` | Application |
| `EventStore:DomainServices:Registrations:{key}` | object | — | Object keyed by `tenant\|domain\|version` or `tenant:domain:version` | Application |
| `EventStore:OpenApi:Enabled` | bool | `true` | `true` or `false` | Application |
| `Authentication:JwtBearer:Authority` | string | `""` | Empty string or absolute OIDC URL | Authentication |
| `Authentication:JwtBearer:Audience` | string | `""` | Non-empty string | Authentication |
| `Authentication:JwtBearer:Issuer` | string | `""` | Non-empty string | Authentication |
| `Authentication:JwtBearer:SigningKey` | string | `""` | Empty string or length `>= 32` | Authentication |
| `Authentication:JwtBearer:RequireHttpsMetadata` | bool | `true` | `true` or `false` | Authentication |
| `EventStoreOptions.EnableRegistrationDiagnostics` | bool | `false` | `true` or `false` | Fluent SDK |
| `EventStoreOptions.DefaultStateStoreSuffix` | string | `"eventstore"` | `null` or non-empty string | Fluent SDK |
| `EventStoreOptions.DefaultTopicSuffix` | string | `"events"` | `null` or non-empty string | Fluent SDK |
| `EventStore:Domains:{domain}:StateStoreName` | string? | `null` | `null` or non-empty string | Fluent SDK |
| `EventStore:Domains:{domain}:TopicPattern` | string? | `null` | `null` or non-empty string | Fluent SDK |
| `EventStore:Domains:{domain}:DeadLetterTopicPattern` | string? | `null` | `null` or non-empty string | Fluent SDK |
| `EventStoreDomainOptions.StateStoreName` | string? | `null` | `null` or non-empty string | Fluent SDK |
| `EventStoreDomainOptions.TopicPattern` | string? | `null` | `null` or non-empty string | Fluent SDK |
| `EventStoreDomainOptions.DeadLetterTopicPattern` | string? | `null` | `null` or non-empty string | Fluent SDK |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | string | (empty) | Empty string or valid OTLP endpoint URL | Environment |
| `DAPR_HTTP_PORT` | int | (auto) | Integer `1-65535` | Environment |
| `DAPR_TRUST_DOMAIN` | string | `"hexalith.io"` | DNS-like trust domain string | Environment |
| `DAPR_NAMESPACE` | string | `"hexalith"` | Valid Kubernetes namespace string | Environment |
| `REDIS_HOST` | string | `"localhost:6379"` | `<host>:<port>` | Environment |
| `REDIS_PASSWORD` | string | (empty) | Empty string or secret value | Environment |
| `POSTGRES_CONNECTION_STRING` | string | (N/A) | Valid PostgreSQL connection string | Environment |
| `RABBITMQ_CONNECTION_STRING` | string | (N/A) | Valid RabbitMQ connection string/URI | Environment |
| `SUBSCRIBER_APP_ID` | string | (N/A) | Non-empty DAPR app-id string | Environment |
| `OPS_MONITOR_APP_ID` | string | (N/A) | Non-empty DAPR app-id string | Environment |
| `EnableKeycloak` | string | `"true"` | `"true"` or `"false"` (case-insensitive) | Aspire |
| `PUBLISH_TARGET` | string | (empty) | Empty, `docker`, `k8s`, or `aca` | Aspire |
| `/health` | endpoint | — | Fixed path `/health` | Health |
| `/alive` | endpoint | — | Fixed path `/alive` | Health |
| `/ready` | endpoint | — | Fixed path `/ready` | Health |

## Next Steps

- **Next:** [Deployment Progression](deployment-progression.md) — Choose your deployment target and see how configuration changes per environment
- **Related:** [DAPR Component Reference](dapr-component-reference.md) — Complete YAML examples for every supported backend
- **Related:** [Security Model](security-model.md) — Authentication flow, authorization, and secrets management
- **Related:** [Troubleshooting](troubleshooting.md) — Common errors and resolution steps
