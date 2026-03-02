[← Back to Hexalith.EventStore](../../README.md)

# DAPR Component Configuration Reference

This is the comprehensive reference for all DAPR building blocks used by Hexalith.EventStore. It covers every component — State Store, Pub/Sub, Actors, Configuration, and Resiliency — with complete, copy-pasteable YAML examples for each supported backend. Use this page to understand what each component does, how to configure it for your target infrastructure, and what persistence guarantees each backend provides. This page assumes you know .NET but not DAPR — all DAPR concepts are defined on first use.

> **Prerequisites:** [Prerequisites and Local Dev Environment](../getting-started/prerequisites.md) | [Deployment Progression Guide](deployment-progression.md)

## Overview

Hexalith.EventStore uses five DAPR building blocks to abstract infrastructure concerns away from application code. A "building block" is a DAPR API that your application calls instead of talking directly to infrastructure. DAPR translates those API calls into backend-specific operations via pluggable components.

| Building Block | Purpose in Hexalith | DAPR Component Type |
|----------------|---------------------|---------------------|
| State Store | Actor state, event snapshots, command status tracking | `state.redis`, `state.postgresql`, `state.azure.cosmosdb` |
| Pub/Sub | Event distribution with per-tenant-per-domain topics | `pubsub.redis`, `pubsub.rabbitmq`, `pubsub.kafka`, `pubsub.azure.servicebus.topics` |
| Actors | Aggregate lifecycle management (turn-based concurrency) | Enabled via `actorStateStore: true` on state store |
| Configuration | Domain service registration and dynamic config | `configuration.redis` |
| Resiliency | Retry, timeout, and circuit breaker policies | `Resiliency` resource (not a component) |

```mermaid
flowchart TB
    Client["Client Application"]
    API["CommandAPI\n(app-id: commandapi)"]
    Sidecar["DAPR Sidecar"]
    SS["State Store\n(Redis / PostgreSQL / Cosmos DB)"]
    PS["Pub/Sub\n(Redis / RabbitMQ / Kafka / Service Bus)"]
    CS["Configuration Store\n(Redis)"]
    DS["Domain Service\n(e.g., sample)"]
    Sub["External Subscriber"]

    Client -->|"HTTP/gRPC"| API
    API -->|"DAPR SDK"| Sidecar
    Sidecar -->|"Actor State\nSnapshots\nCommand Status"| SS
    Sidecar -->|"Publish Events\nCloudEvents 1.0"| PS
    Sidecar -->|"Service Registry\nDynamic Config"| CS
    Sidecar -->|"Service Invocation"| DS
    PS -->|"Subscribe"| Sub
```

<details>
<summary>Architecture diagram text description</summary>

The diagram shows the data flow through Hexalith.EventStore DAPR components. A client application sends HTTP/gRPC requests to the CommandAPI (app-id: commandapi). The CommandAPI communicates with its DAPR sidecar via the DAPR SDK. The sidecar connects to three infrastructure components: (1) the State Store (Redis, PostgreSQL, or Cosmos DB) for actor state, snapshots, and command status; (2) the Pub/Sub broker (Redis, RabbitMQ, Kafka, or Service Bus) for publishing CloudEvents 1.0 events; and (3) the Configuration Store (Redis) for service registry and dynamic configuration. The sidecar also invokes domain services directly. External subscribers receive events from the pub/sub broker.

</details>

The key architecture decisions that affect component configuration:

| Decision | Description | Configuration Impact |
|----------|-------------|---------------------|
| D1 | Composite key strategy | Event stream keys follow `{tenant}:{domain}:{aggregateId}:events:{seq}` pattern |
| D2 | Command status TTL | 24-hour TTL set at application level, not component level |
| D4 | Deny-by-default access | Only `commandapi` accesses state store and pub/sub; access control enforces this |
| D6 | Topic naming convention | Topics follow `{tenant}.{domain}.events`; dead-letter: `deadletter.{tenant}.{domain}.events` |
| D7 | Resiliency at sidecar level | No custom retry logic in application code; DAPR resiliency policies handle all retries |

## State Store Configuration

The state store is the persistence backbone of Hexalith.EventStore. It stores actor state (aggregate state reconstructed from events), event snapshots (periodic state captures for fast rehydration), and command status entries (24-hour TTL tracking). All state store backends must set `actorStateStore: true` to enable the Actors building block.

Component scoping restricts access to `commandapi` only (D4). Domain services have zero state store access.

### Redis (Local Development)

Source: `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml`

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
      # Redis server address. Default: localhost:6379 for local development.
      # The |localhost:6379 syntax provides a fallback if REDIS_HOST is not set.
      value: "{env:REDIS_HOST|localhost:6379}"
    - name: redisPassword
      # Redis password. Leave empty for local development without auth.
      # Set via environment variable for any environment requiring authentication.
      value: "{env:REDIS_PASSWORD}"
    - name: actorStateStore
      # REQUIRED: Enables this state store for DAPR Actors building block.
      # Without this, actor activation fails with "no actor state store" error.
      value: "true"
# Component scoping: only commandapi can access this state store (D4).
# Actors run in-process under the commandapi app-id.
# Domain services (sample) have zero state store access.
scopes:
  - commandapi
```

Persistence guarantees: Redis stores data in-memory by default. Data survives process restarts only if AOF (Append Only File) or RDB (Redis Database) persistence is enabled in your Redis server configuration. For local development, data loss on restart is acceptable. ETag-based optimistic concurrency is supported via Redis `SET NX` semantics. Multi-key transactions use Redis `MULTI/EXEC`.

### PostgreSQL (On-Premise Production)

Source: `deploy/dapr/statestore-postgresql.yaml`

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.postgresql
  version: v1
  metadata:
    - name: connectionString
      # PostgreSQL connection string.
      # Format: host=<host>;port=5432;username=<user>;password=<pass>;database=<db>;sslmode=require
      # NEVER hardcode secrets -- provide via environment variable (NFR14).
      value: "{env:POSTGRES_CONNECTION_STRING}"
    - name: actorStateStore
      # REQUIRED: Enables this state store for DAPR Actors building block.
      value: "true"
# Component scoping: only commandapi can access this state store (D4).
scopes:
  - commandapi
```

Persistence guarantees: PostgreSQL provides WAL-based (Write-Ahead Logging) durability — committed data survives crashes. Consistency is ACID-strong with full transaction support. ETag optimistic concurrency uses row versioning. Data lives in a relational `state` table. Back up with `pg_dump` or continuous WAL archiving.

### Azure Cosmos DB (Cloud Production)

Source: `deploy/dapr/statestore-cosmosdb.yaml`

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.azure.cosmosdb
  version: v1
  metadata:
    - name: url
      # Cosmos DB account URL.
      # Format: https://<account-name>.documents.azure.com:443/
      value: "{env:COSMOSDB_URL}"
    - name: masterKey
      # Cosmos DB primary or secondary key.
      # NEVER hardcode -- use Azure Key Vault in production (NFR14).
      value: "{env:COSMOSDB_KEY}"
    - name: database
      # Cosmos DB database name for actor state storage.
      value: "{env:COSMOSDB_DATABASE}"
    - name: collection
      # Cosmos DB container/collection name for actor state storage.
      value: "{env:COSMOSDB_COLLECTION}"
    - name: actorStateStore
      # REQUIRED: Enables this state store for DAPR Actors building block.
      value: "true"
# Component scoping: only commandapi can access this state store (D4).
scopes:
  - commandapi
```

Azure Container Apps uses a simplified component schema (no `apiVersion`, `kind`, `metadata.name` wrapper). The equivalent ACA DAPR component configuration:

```yaml
componentType: state.azure.cosmosdb
version: v1
metadata:
    - name: url
      value: "{env:COSMOSDB_URL}"
    - name: masterKey
      value: "{env:COSMOSDB_KEY}"
    - name: database
      value: "{env:COSMOSDB_DATABASE}"
    - name: collection
      value: "{env:COSMOSDB_COLLECTION}"
    - name: actorStateStore
      value: "true"
scopes:
    - commandapi
```

Persistence guarantees: Cosmos DB provides multi-region replication with configurable consistency levels — strong, bounded staleness, session, consistent prefix, and eventual. ETag optimistic concurrency uses the `_etag` property on each document. Cross-partition transactions are limited. Data lives as JSON documents. Back up with Azure Backup (continuous or periodic).

## Pub/Sub Configuration

The pub/sub component handles event distribution using CloudEvents 1.0 format. Events are published to per-tenant-per-domain topics following the `{tenant}.{domain}.events` naming pattern (D6). Failed messages route to dead-letter topics: `deadletter.{tenant}.{domain}.events`.

All pub/sub backends use the same three-layer scoping architecture:

1. **Layer 1 — Component Scoping** (`scopes` field): Controls which app-ids can use this pub/sub component at all. Only `commandapi` and authorized subscriber app-ids are listed.
2. **Layer 2 — Publishing Scoping** (`publishingScopes` metadata): Controls which app-ids can publish to which topics. `commandapi` is intentionally NOT listed, giving it unrestricted publish access for dynamic tenant provisioning (NFR20). Subscribers are denied publishing with empty values (`subscriber=`).
3. **Layer 3 — Subscription Scoping** (`subscriptionScopes` metadata): Controls which app-ids can subscribe to which topics. External subscribers are explicitly scoped to their authorized tenant topics.

DAPR does NOT support wildcards in scoping — strict string equality matching only. Apps not listed in a scoping field have unrestricted access (default-open). Apps listed with an empty value (`app=`) are denied all access.

### Redis Streams (Local Development)

Source: `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.redis
  version: v1
  metadata:
    - name: redisHost
      # Redis server address for pub/sub. Same Redis instance as state store in local dev.
      value: "{env:REDIS_HOST|localhost:6379}"
    - name: redisPassword
      # Redis password for pub/sub connection.
      # Leave empty for local development without authentication.
      value: "{env:REDIS_PASSWORD}"
    - name: enableDeadLetter
      # Routes undeliverable messages (after retry exhaustion) to a dead-letter topic.
      value: "true"
    - name: deadLetterTopic
      # Default dead-letter topic name. Per-tenant dead-letter routing
      # (deadletter.{tenant}.{domain}.events) is configured per-subscription.
      value: "deadletter"
    - name: publishingScopes
      # Layer 2: sample= (empty) denies sample from publishing.
      # commandapi is NOT listed = unrestricted publishing to all topics (NFR20).
      value: "sample="
    - name: subscriptionScopes
      # Layer 3: sample= (empty) denies sample from subscribing.
      # External subscribers are scoped to specific tenant topics.
      value: "sample=;example-subscriber=acme.orders.events,acme.inventory.events;ops-monitor=deadletter.acme.orders.events,deadletter.acme.inventory.events"
# Layer 1: Component scoping.
scopes:
  - commandapi
  - example-subscriber
  - ops-monitor
```

Redis Streams uses consumer group semantics for at-least-once delivery. Dead-letter handling is DAPR-managed (Redis does not have native dead-letter queues).

### RabbitMQ (On-Premise Production)

Source: `deploy/dapr/pubsub-rabbitmq.yaml`

```yaml
# DAPR Pub/Sub Component -- Production RabbitMQ (D6, NFR13, FR29)
# ==================================================================
# RabbitMQ-backed pub/sub for event distribution with per-tenant-per-domain topics.
# Topic pattern: {tenant}.{domain}.events (D6)
# Dead-letter topic pattern: deadletter.{tenant}.{domain}.events (Story 4.5)
#
# SCOPING ARCHITECTURE (Three Layers)
# ====================================
#
# Layer 1 -- Component Scoping (scopes field):
#   Controls which app-ids can USE this pub/sub component at all.
#   Only commandapi and authorized subscriber app-ids are listed.
#   Domain services have zero pub/sub access (D4).
#
# Layer 2 -- Publishing Scoping (publishingScopes metadata):
#   Controls which app-ids can PUBLISH to which topics.
#   Format: "app1=topic1,topic2;app2=" (semicolon-separated, empty=deny all).
#   Apps NOT listed in publishingScopes have unrestricted publish access.
#   NOTE: DAPR does NOT support wildcards (*) in scoping -- strict string match only.
#   Do NOT use "commandapi=*" -- DAPR treats * as a literal topic name, not a wildcard.
#
# Layer 3 -- Subscription Scoping (subscriptionScopes metadata):
#   Controls which app-ids can SUBSCRIBE to which topics.
#   Same format as publishingScopes.
#   Apps NOT listed in subscriptionScopes have unrestricted subscribe access.
#   NOTE: DAPR does NOT support wildcards (*) in scoping -- strict string match only.
#
# PUBLISHER-SIDE SCOPING (Story 5.1):
#   commandapi is NOT listed in publishingScopes = can publish to ANY topic (NFR20).
#   Production domain services are not in component scopes = zero pub/sub access.
#
# SUBSCRIBER-SIDE SCOPING (Story 5.3, FR29):
#   commandapi is NOT listed in subscriptionScopes = can subscribe to all topics.
#   External subscribers must be explicitly scoped to authorized tenant topics.
#   Dead-letter topic subscription is SEPARATE from regular event topic subscription.
#   Unauthorized subscription attempts are rejected at the DAPR sidecar and are
#   observable in DAPR sidecar warning/error logs.
#
# ADDING A NEW SUBSCRIBER SERVICE (Production)
# ==============================================
# When deploying a new external subscriber service:
#
# Step 1: Add the subscriber app-id to the 'scopes' list below.
#         Use the production app-id (may differ from local development).
#
# Step 2: Add the subscriber to 'subscriptionScopes' with authorized tenant topics.
#         Format: ";{env:NEW_SUBSCRIBER_APP_ID}={tenant1}.{domain1}.events,{tenant2}.{domain2}.events"
#         Append to existing value with semicolon separator.
#         Use {env:...} syntax for app-ids to match the existing DAPR env-var pattern.
#
# Step 3: Do NOT add the subscriber to 'publishingScopes'.
#         External subscribers should NEVER publish -- only commandapi publishes.
#
# Step 4: Dead-letter access is SEPARATE. Only add dead-letter topics if the
#         subscriber is an operational/monitoring tool.
#         Format: ";{env:NEW_OPS_TOOL_APP_ID}=deadletter.{tenant}.{domain}.events"
#
# Step 5: Update PubSubTopicIsolationEnforcementTests.cs to reflect the new topology.
#
# DYNAMIC TENANT PROVISIONING (NFR20)
# =====================================
# Publisher (commandapi): NOT listed in scoping = unrestricted. Can publish to any
#   new tenant topic immediately without YAML changes.
# Subscriber: MUST be explicitly granted access per tenant topic. This requires a
#   YAML config update and redeployment (acceptable operational step -- subscriber
#   access is a security-critical decision requiring human approval).
#
# RABBITMQ-SPECIFIC NOTES
# ========================
# RabbitMQ pub/sub component uses exchanges and queues internally. DAPR scoping
# metadata (publishingScopes, subscriptionScopes) is enforced at the DAPR runtime
# level BEFORE messages reach RabbitMQ. This means:
#   - Scoping is identical across all pub/sub component types (Redis, RabbitMQ, Kafka)
#   - RabbitMQ-level ACLs/permissions are a SEPARATE layer (configure independently)
#   - Dead-letter exchanges (DLX) in RabbitMQ are managed by DAPR via enableDeadLetter
#
# SCOPING FIELD REFERENCE (DAPR 1.16)
# =====================================
# publishingScopes: "app1=topic1,topic2;app2=" -- per-app publish restrictions
# subscriptionScopes: "app1=topic1,topic2;app2=" -- per-app subscribe restrictions
# allowedTopics: "topic1,topic2" -- global whitelist (all apps, publish + subscribe)
# protectedTopics: "topic1,topic2" -- require explicit grant in scoping metadata
# IMPORTANT: No wildcard support. Strict string equality match only.
# Apps NOT listed in a scoping field have UNRESTRICTED access (default-open).
# Apps listed with empty value (app=) are DENIED all access (explicit deny).
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.rabbitmq
  version: v1
  metadata:
    - name: connectionString
      # RabbitMQ connection string. Format: amqp://<user>:<pass>@<host>:5672/
      # Provide via environment variable -- NEVER hardcode secrets in this file (NFR14).
      value: "{env:RABBITMQ_CONNECTION_STRING}"
    - name: durable
      # RabbitMQ queue durability flag (true = survives broker restart).
      value: "true"
    - name: deletedWhenUnused
      # Queue lifecycle flag (false = keep queue even when no consumers).
      value: "false"
    # Dead-letter topic: undeliverable messages (retry exhaustion) route here by default.
    # Compatible with RabbitMQ dead-letter exchanges (DLX).
    # Per-tenant dead-letter routing (deadletter.{tenant}.{domain}.events) is configured
    # per-subscription in Story 4.5.
    - name: enableDeadLetter
      value: "true"
    - name: deadLetterTopic
      value: "deadletter"
    # Publishing scoping (Story 5.1):
    # commandapi is NOT listed = unrestricted publishing to all topics (NFR20).
    # NOTE: Do NOT use "commandapi=*" -- DAPR treats * as a literal topic name,
    # not a wildcard. Omitting commandapi achieves unrestricted access.
    #
    # When adding production domain services to component scopes, you MUST add
    # denial entries here: ";{env:DOMAIN_SERVICE_APP_ID}=" to deny publishing.
    # IMPORTANT: Set SUBSCRIBER_APP_ID and OPS_MONITOR_APP_ID environment variables before deployment.
    # If left unset, DAPR env var references will not resolve and scoping will not match real app-ids.
    - name: publishingScopes
      value: "{env:SUBSCRIBER_APP_ID}=;{env:OPS_MONITOR_APP_ID}="
    # Subscription scoping (Story 5.3, FR29):
    # commandapi is NOT listed = unrestricted subscription to all topics.
    # External subscribers must be explicitly added with authorized tenant topics.
    #
    # Example with production subscriber:
    #   value: ";{env:NEW_SUBSCRIBER_APP_ID}=tenant.domain.events,tenant2.domain2.events"
    # Example with ops-monitor for dead-letter topics:
    #   value: ";{env:NEW_OPS_TOOL_APP_ID}=deadletter.tenant.domain.events"
    # MUST CUSTOMIZE: Replace example topic names (acme.orders.events, acme.inventory.events)
    # with your actual tenant.domain.events topics before deployment.
    - name: subscriptionScopes
      value: "{env:SUBSCRIBER_APP_ID}=acme.orders.events,acme.inventory.events;{env:OPS_MONITOR_APP_ID}=deadletter.acme.orders.events,deadletter.acme.inventory.events"
# Component-level scoping: commandapi and explicitly authorized subscriber app-ids
# can access this pub/sub component. Domain services have zero pub/sub access (D4).
#
# When deploying external subscribers, add their production app-id here
# AND scope their topic access via subscriptionScopes above:
#   scopes:
#     - commandapi
#     - "{env:SUBSCRIBER_APP_ID}"
#     - "{env:OPS_MONITOR_APP_ID}"
scopes:
  - commandapi
  - "{env:SUBSCRIBER_APP_ID}"
  - "{env:OPS_MONITOR_APP_ID}"
```

RabbitMQ uses durable queues and dead-letter exchanges (DLX). Message persistence is enabled via the `durable` flag. AMQP delivery guarantees provide at-least-once delivery. RabbitMQ-level ACLs/permissions are a separate layer — configure independently from DAPR scoping.

### Kafka (On-Premise Production)

Source: `deploy/dapr/pubsub-kafka.yaml`

```yaml
# DAPR Pub/Sub Component -- Production Kafka (D6, NFR13, FR29)
# ===============================================================
# Kafka-backed pub/sub for event distribution with per-tenant-per-domain topics.
# Topic pattern: {tenant}.{domain}.events (D6)
# Dead-letter topic pattern: deadletter.{tenant}.{domain}.events (Story 4.5)
#
# SCOPING ARCHITECTURE (Three Layers)
# ====================================
#
# Layer 1 -- Component Scoping (scopes field):
#   Controls which app-ids can USE this pub/sub component at all.
#   Only commandapi and authorized subscriber app-ids are listed.
#   Domain services have zero pub/sub access (D4).
#
# Layer 2 -- Publishing Scoping (publishingScopes metadata):
#   Controls which app-ids can PUBLISH to which topics.
#   Format: "app1=topic1,topic2;app2=" (semicolon-separated, empty=deny all).
#   Apps NOT listed in publishingScopes have unrestricted publish access.
#   NOTE: DAPR does NOT support wildcards (*) in scoping -- strict string match only.
#   Do NOT use "commandapi=*" -- DAPR treats * as a literal topic name, not a wildcard.
#
# Layer 3 -- Subscription Scoping (subscriptionScopes metadata):
#   Controls which app-ids can SUBSCRIBE to which topics.
#   Same format as publishingScopes.
#   Apps NOT listed in subscriptionScopes have unrestricted subscribe access.
#   NOTE: DAPR does NOT support wildcards (*) in scoping -- strict string match only.
#
# PUBLISHER-SIDE SCOPING (Story 5.1):
#   commandapi is NOT listed in publishingScopes = can publish to ANY topic (NFR20).
#   Production domain services are not in component scopes = zero pub/sub access.
#
# SUBSCRIBER-SIDE SCOPING (Story 5.3, FR29):
#   commandapi is NOT listed in subscriptionScopes = can subscribe to all topics.
#   External subscribers must be explicitly scoped to authorized tenant topics.
#   Dead-letter topic subscription is SEPARATE from regular event topic subscription.
#   Unauthorized subscription attempts are rejected at the DAPR sidecar and are
#   observable in DAPR sidecar warning/error logs.
#
# ADDING A NEW SUBSCRIBER SERVICE (Production)
# ==============================================
# When deploying a new external subscriber service:
#
# Step 1: Add the subscriber app-id to the 'scopes' list below.
#         Use the production app-id (may differ from local development).
#
# Step 2: Add the subscriber to 'subscriptionScopes' with authorized tenant topics.
#         Format: ";{env:NEW_SUBSCRIBER_APP_ID}={tenant1}.{domain1}.events,{tenant2}.{domain2}.events"
#         Append to existing value with semicolon separator.
#         Use {env:...} syntax for app-ids to match the existing DAPR env-var pattern.
#
# Step 3: Do NOT add the subscriber to 'publishingScopes'.
#         External subscribers should NEVER publish -- only commandapi publishes.
#
# Step 4: Dead-letter access is SEPARATE. Only add dead-letter topics if the
#         subscriber is an operational/monitoring tool.
#         Format: ";{env:NEW_OPS_TOOL_APP_ID}=deadletter.{tenant}.{domain}.events"
#
# Step 5: Update PubSubTopicIsolationEnforcementTests.cs to reflect the new topology.
#
# DYNAMIC TENANT PROVISIONING (NFR20)
# =====================================
# Publisher (commandapi): NOT listed in scoping = unrestricted. Can publish to any
#   new tenant topic immediately without YAML changes.
# Subscriber: MUST be explicitly granted access per tenant topic. This requires a
#   YAML config update and redeployment (acceptable operational step -- subscriber
#   access is a security-critical decision requiring human approval).
#
# KAFKA-SPECIFIC NOTES
# =====================
# Kafka pub/sub component uses Kafka topics and consumer groups internally. DAPR
# scoping metadata (publishingScopes, subscriptionScopes) is enforced at the DAPR
# runtime level BEFORE messages reach Kafka. This means:
#   - Scoping is identical across all pub/sub component types (Redis, RabbitMQ, Kafka)
#   - Kafka-level ACLs (broker authorization) are a SEPARATE layer (configure independently)
#   - Consumer group isolation is managed by Kafka -- each subscriber app-id uses
#     its own consumer group by default
#   - Kafka topic auto-creation supports NFR20 dynamic tenant provisioning
#
# SCOPING FIELD REFERENCE (DAPR 1.16)
# =====================================
# publishingScopes: "app1=topic1,topic2;app2=" -- per-app publish restrictions
# subscriptionScopes: "app1=topic1,topic2;app2=" -- per-app subscribe restrictions
# allowedTopics: "topic1,topic2" -- global whitelist (all apps, publish + subscribe)
# protectedTopics: "topic1,topic2" -- require explicit grant in scoping metadata
# IMPORTANT: No wildcard support. Strict string equality match only.
# Apps NOT listed in a scoping field have UNRESTRICTED access (default-open).
# Apps listed with empty value (app=) are DENIED all access (explicit deny).
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.kafka
  version: v1
  metadata:
    - name: brokers
      # Comma-separated Kafka broker addresses. Format: broker1:9092,broker2:9092
      # Provide via environment variable -- NEVER hardcode in this file (NFR14).
      value: "{env:KAFKA_BROKERS}"
    - name: authType
      # Kafka authentication type: none, password, mtls, or oidc.
      # Use 'password' for SASL/PLAIN or SASL/SCRAM, 'mtls' for certificate auth.
      value: "{env:KAFKA_AUTH_TYPE}"
    # Dead-letter topic: undeliverable messages (retry exhaustion) route here by default.
    # Kafka dead-letter: messages routed to a separate Kafka topic after consumer failure.
    # Per-tenant dead-letter routing (deadletter.{tenant}.{domain}.events) is configured
    # per-subscription in Story 4.5.
    - name: enableDeadLetter
      value: "true"
    - name: deadLetterTopic
      value: "deadletter"
    # Publishing scoping (Story 5.1):
    # commandapi is NOT listed = unrestricted publishing to all topics (NFR20).
    # NOTE: Do NOT use "commandapi=*" -- DAPR treats * as a literal topic name,
    # not a wildcard. Omitting commandapi achieves unrestricted access.
    #
    # When adding production domain services to component scopes, you MUST add
    # denial entries here: ";{env:DOMAIN_SERVICE_APP_ID}=" to deny publishing.
    # IMPORTANT: Set SUBSCRIBER_APP_ID and OPS_MONITOR_APP_ID environment variables before deployment.
    # If left unset, DAPR env var references will not resolve and scoping will not match real app-ids.
    - name: publishingScopes
      value: "{env:SUBSCRIBER_APP_ID}=;{env:OPS_MONITOR_APP_ID}="
    # Subscription scoping (Story 5.3, FR29):
    # commandapi is NOT listed = unrestricted subscription to all topics.
    # External subscribers must be explicitly added with authorized tenant topics.
    #
    # Example with production subscriber:
    #   value: ";{env:NEW_SUBSCRIBER_APP_ID}=tenant.domain.events,tenant2.domain2.events"
    # Example with ops-monitor for dead-letter topics:
    #   value: ";{env:NEW_OPS_TOOL_APP_ID}=deadletter.tenant.domain.events"
    # MUST CUSTOMIZE: Replace example topic names (acme.orders.events, acme.inventory.events)
    # with your actual tenant.domain.events topics before deployment.
    - name: subscriptionScopes
      value: "{env:SUBSCRIBER_APP_ID}=acme.orders.events,acme.inventory.events;{env:OPS_MONITOR_APP_ID}=deadletter.acme.orders.events,deadletter.acme.inventory.events"
# Component-level scoping: commandapi and explicitly authorized subscriber app-ids
# can access this pub/sub component. Domain services have zero pub/sub access (D4).
#
# When deploying external subscribers, add their production app-id here
# AND scope their topic access via subscriptionScopes above:
#   scopes:
#     - commandapi
#     - "{env:SUBSCRIBER_APP_ID}"
#     - "{env:OPS_MONITOR_APP_ID}"
scopes:
  - commandapi
  - "{env:SUBSCRIBER_APP_ID}"
  - "{env:OPS_MONITOR_APP_ID}"
```

Kafka uses a commit log with configurable retention. Each subscriber app-id uses its own consumer group by default, providing consumer group isolation. Kafka topic auto-creation supports dynamic tenant provisioning (NFR20). Kafka-level ACLs are a separate layer from DAPR scoping.

### Azure Service Bus (Cloud Production)

Source: `deploy/dapr/pubsub-servicebus.yaml`

```yaml
# DAPR Pub/Sub Component -- Production Azure Service Bus (D6, NFR13, FR29)
# ===========================================================================
# Azure Service Bus-backed pub/sub for event distribution with per-tenant-per-domain topics.
# Topic pattern: {tenant}.{domain}.events (D6)
# Dead-letter topic pattern: deadletter.{tenant}.{domain}.events (Story 4.5)
#
# SCOPING ARCHITECTURE (Three Layers)
# ====================================
#
# Layer 1 -- Component Scoping (scopes field):
#   Controls which app-ids can USE this pub/sub component at all.
#   Only commandapi and authorized subscriber app-ids are listed.
#   Domain services have zero pub/sub access (D4).
#
# Layer 2 -- Publishing Scoping (publishingScopes metadata):
#   Controls which app-ids can PUBLISH to which topics.
#   Format: "app1=topic1,topic2;app2=" (semicolon-separated, empty=deny all).
#   Apps NOT listed in publishingScopes have unrestricted publish access.
#   NOTE: DAPR does NOT support wildcards (*) in scoping -- strict string match only.
#   Do NOT use "commandapi=*" -- DAPR treats * as a literal topic name, not a wildcard.
#
# Layer 3 -- Subscription Scoping (subscriptionScopes metadata):
#   Controls which app-ids can SUBSCRIBE to which topics.
#   Same format as publishingScopes.
#   Apps NOT listed in subscriptionScopes have unrestricted subscribe access.
#   NOTE: DAPR does NOT support wildcards (*) in scoping -- strict string match only.
#
# PUBLISHER-SIDE SCOPING (Story 5.1):
#   commandapi is NOT listed in publishingScopes = can publish to ANY topic (NFR20).
#   Production domain services are not in component scopes = zero pub/sub access.
#
# SUBSCRIBER-SIDE SCOPING (Story 5.3, FR29):
#   commandapi is NOT listed in subscriptionScopes = can subscribe to all topics.
#   External subscribers must be explicitly scoped to authorized tenant topics.
#   Dead-letter topic subscription is SEPARATE from regular event topic subscription.
#   Unauthorized subscription attempts are rejected at the DAPR sidecar and are
#   observable in DAPR sidecar warning/error logs.
#
# ADDING A NEW SUBSCRIBER SERVICE (Production)
# ==============================================
# When deploying a new external subscriber service:
#
# Step 1: Add the subscriber app-id to the 'scopes' list below.
#         Use the production app-id (may differ from local development).
#
# Step 2: Add the subscriber to 'subscriptionScopes' with authorized tenant topics.
#         Format: ";{env:NEW_SUBSCRIBER_APP_ID}={tenant1}.{domain1}.events,{tenant2}.{domain2}.events"
#         Append to existing value with semicolon separator.
#         Use {env:...} syntax for app-ids to match the existing DAPR env-var pattern.
#
# Step 3: Do NOT add the subscriber to 'publishingScopes'.
#         External subscribers should NEVER publish -- only commandapi publishes.
#
# Step 4: Dead-letter access is SEPARATE. Only add dead-letter topics if the
#         subscriber is an operational/monitoring tool.
#         Format: ";{env:NEW_OPS_TOOL_APP_ID}=deadletter.{tenant}.{domain}.events"
#
# Step 5: Update PubSubTopicIsolationEnforcementTests.cs to reflect the new topology.
#
# DYNAMIC TENANT PROVISIONING (NFR20)
# =====================================
# Publisher (commandapi): NOT listed in scoping = unrestricted. Can publish to any
#   new tenant topic immediately without YAML changes.
# Subscriber: MUST be explicitly granted access per tenant topic. This requires a
#   YAML config update and redeployment (acceptable operational step -- subscriber
#   access is a security-critical decision requiring human approval).
#
# AZURE SERVICE BUS-SPECIFIC NOTES
# ==================================
# Azure Service Bus pub/sub component uses topics and subscriptions. DAPR scoping
# metadata (publishingScopes, subscriptionScopes) is enforced at the DAPR runtime
# level BEFORE messages reach Azure Service Bus. This means:
#   - Scoping is identical across all pub/sub component types (Redis, RabbitMQ, Kafka, Service Bus)
#   - Azure Service Bus-level RBAC/SAS policies are a SEPARATE layer (configure independently)
#   - Dead-letter queues (DLQ) in Service Bus are native -- DAPR leverages built-in DLQ support
#   - Topics must be PRE-CREATED in Azure Service Bus (no auto-creation like Redis/RabbitMQ)
#   - Use Shared Access Signature (SAS) or Azure AD authentication for production
#
# SCOPING FIELD REFERENCE (DAPR 1.16)
# =====================================
# publishingScopes: "app1=topic1,topic2;app2=" -- per-app publish restrictions
# subscriptionScopes: "app1=topic1,topic2;app2=" -- per-app subscribe restrictions
# allowedTopics: "topic1,topic2" -- global whitelist (all apps, publish + subscribe)
# protectedTopics: "topic1,topic2" -- require explicit grant in scoping metadata
# IMPORTANT: No wildcard support. Strict string equality match only.
# Apps NOT listed in a scoping field have UNRESTRICTED access (default-open).
# Apps listed with empty value (app=) are DENIED all access (explicit deny).
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.azure.servicebus.topics
  version: v1
  metadata:
    - name: connectionString
      # Azure Service Bus connection string with topic management permissions.
      # Format: Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<key-name>;SharedAccessKey=<key>
      # Alternatively, use Azure AD authentication with namespaceName metadata.
      value: "{env:SERVICEBUS_CONNECTION_STRING}"
    - name: enableDeadLetter
      # Enables routing to dead-letter topic when retries are exhausted.
      value: "true"
    - name: deadLetterTopic
      # Default dead-letter topic name used by this component.
      value: "deadletter"
    # IMPORTANT: Set SUBSCRIBER_APP_ID and OPS_MONITOR_APP_ID before deployment.
    # If left unset, these values may remain literal and scoping will not match real app-ids.
    - name: publishingScopes
      value: "{env:SUBSCRIBER_APP_ID}=;{env:OPS_MONITOR_APP_ID}="
    # MUST CUSTOMIZE: Replace example topic names (acme.orders.events, acme.inventory.events)
    # with your actual tenant.domain.events topics before deployment.
    - name: subscriptionScopes
      value: "{env:SUBSCRIBER_APP_ID}=acme.orders.events,acme.inventory.events;{env:OPS_MONITOR_APP_ID}=deadletter.acme.orders.events,deadletter.acme.inventory.events"
scopes:
  - commandapi
  - "{env:SUBSCRIBER_APP_ID}"
  - "{env:OPS_MONITOR_APP_ID}"
```

Azure Container Apps equivalent (simplified schema):

```yaml
componentType: pubsub.azure.servicebus.topics
version: v1
metadata:
    - name: connectionString
      value: "{env:SERVICEBUS_CONNECTION_STRING}"
    - name: enableDeadLetter
      value: "true"
    - name: deadLetterTopic
      value: "deadletter"
    - name: publishingScopes
      value: "{env:SUBSCRIBER_APP_ID}=;{env:OPS_MONITOR_APP_ID}="
    - name: subscriptionScopes
      value: "{env:SUBSCRIBER_APP_ID}=acme.orders.events,acme.inventory.events;{env:OPS_MONITOR_APP_ID}=deadletter.acme.orders.events,deadletter.acme.inventory.events"
scopes:
    - commandapi
    - "{env:SUBSCRIBER_APP_ID}"
    - "{env:OPS_MONITOR_APP_ID}"
```

Azure Service Bus provides guaranteed delivery with native dead-letter queue support. Topics must be pre-created in Azure Service Bus — there is no auto-creation like Redis or RabbitMQ. Use Shared Access Signature (SAS) or Azure AD (RBAC) authentication for production. Geo-replication is available for disaster recovery.

### CloudEvents 1.0 Compliance

All pub/sub backends emit events in CloudEvents 1.0 format. The DAPR runtime handles CloudEvents envelope wrapping automatically. This means your subscriber receives a standard CloudEvents envelope regardless of whether the broker is Redis, RabbitMQ, Kafka, or Azure Service Bus.

## Actors Configuration

Actors in Hexalith.EventStore are NOT a separate DAPR component YAML. The Actors building block is enabled by setting `actorStateStore: true` on the state store component (shown in every state store example above). DAPR uses the state store marked as the actor state store to persist actor state automatically.

Actor-specific runtime configuration:

| Setting | Default | Description |
|---------|---------|-------------|
| Idle timeout | 60 minutes | Actor is deactivated after this period of inactivity, freeing memory |
| Turn-based concurrency | Single-threaded | Only one method executes at a time per actor instance — no concurrent access |
| Placement | Consistent hashing | DAPR distributes actors across instances using consistent hashing |
| Reentrancy | Disabled | Reentrant calls within the same actor are blocked by default |

Actor reminders (used in publish recovery flows):

- Reminder name format: `drain-unpublished-{correlationId}`
- Trigger path: `ReceiveReminderAsync` processes only reminders with `drain-unpublished-` prefix
- Schedule source: `EventStore:Drain` options
  - `InitialDrainDelay` controls due time (negative values are clamped to `0`)
  - `DrainPeriod` controls repeat interval (defaults to `00:01:00` when invalid)
  - `MaxDrainPeriod` upper-bounds repeat interval (defaults to `00:30:00` when invalid)
- Registration behavior: reminder is registered only after successful state commit in publish-failure handling

Hexalith currently uses reminders for unpublished event draining and does not define custom actor timers in component YAML.

These settings are configured in your application code (via `ActorRuntimeOptions`), not in DAPR component YAML. The state store backend affects actor performance characteristics:

- **Redis:** Fastest reads/writes, but data is in-memory (enable AOF/RDB for durability)
- **PostgreSQL:** ACID guarantees, slightly higher latency than Redis
- **Cosmos DB:** Globally distributed, configurable consistency, highest latency for single-region reads

## Configuration Store

The configuration store provides dynamic key-value configuration for domain service registration. It maps tenant + domain + version combinations to service endpoint information, enabling the CommandAPI to discover and invoke domain services at runtime.

Source: `src/Hexalith.EventStore.AppHost/DaprComponents/configstore.yaml`

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: configstore
spec:
  type: configuration.redis
  version: v1
  metadata:
    - name: redisHost
      # Redis server address for configuration store.
      # Can share the same Redis instance as state store and pub/sub in local dev.
      value: "{env:REDIS_HOST|localhost:6379}"
    - name: redisPassword
      # Redis password for configuration-store connection.
      # Leave empty for local development without authentication.
      value: "{env:REDIS_PASSWORD}"
# Default scope includes `commandapi`; add domain-service app-ids when they consume configuration directly.
# Example:
# scopes:
#   - commandapi
#   - sample
scopes:
  - commandapi
```

The configuration store supports dynamic updates without application restart — when a configuration key changes in Redis, the DAPR sidecar notifies subscribed applications via the Configuration API.

## Resiliency Policies

Resiliency is a DAPR resource (not a component) that defines retry, timeout, and circuit breaker policies applied to DAPR building block operations. Hexalith.EventStore uses sidecar-level resiliency exclusively — no custom retry logic exists in application code (D7).

Source: `deploy/dapr/resiliency.yaml`

```yaml
apiVersion: dapr.io/v1alpha1
kind: Resiliency
metadata:
  name: resiliency
spec:
  policies:
    retries:
      defaultRetry:
        # Exponential backoff with jitter for general operations.
        policy: exponential
        maxInterval: 15s
        maxRetries: 10
      pubsubRetryOutbound:
        # Publisher -> sidecar -> broker retries.
        # Conservative: Story 4.4 recovery drain handles prolonged outages.
        # Effective retry count = component built-in retries x resiliency retries:
        #   Redis Streams: 0 built-in -> effective = 5
        #   RabbitMQ: ~3 built-in (publisher confirms) -> effective ~15
        #   Kafka: ~infinite built-in (default retries=2147483647) -> resiliency rarely fires
        #   Azure Service Bus: 3 built-in (SDK default) -> effective ~15
        policy: exponential
        maxInterval: 10s
        maxRetries: 5
      pubsubRetryInbound:
        # Broker -> sidecar -> subscriber app retries.
        # More retries with longer intervals for subscriber processing failures.
        policy: exponential
        maxInterval: 60s
        maxRetries: 20
    timeouts:
      daprSidecar:
        # General sidecar operation timeout.
        general: 5s
      pubsubTimeout:
        # Pub/sub outbound: prevents hung sidecar->broker calls
        # from blocking actor turns.
        10s
      subscriberTimeout:
        # Pub/sub inbound: allows more time for subscriber processing.
        30s
    circuitBreakers:
      defaultBreaker:
        # General circuit breaker for app-to-sidecar operations.
        maxRequests: 1      # 1 probe request in half-open state
        interval: 60s       # Sampling window for failure counting
        timeout: 60s        # Time in open state before half-open
        trip: consecutiveFailures > 5
      pubsubBreaker:
        # Pub/sub circuit breaker for prolonged broker outages.
        # When open, AggregateActor receives immediate failure (fast-fail)
        # and transitions to PublishFailed state.
        maxRequests: 1
        interval: 60s
        timeout: 60s
        trip: consecutiveFailures > 10
  targets:
    apps:
      commandapi:
        # Apply default policies to commandapi operations.
        retry: defaultRetry
        timeout: daprSidecar
        circuitBreaker: defaultBreaker
    components:
      pubsub:
        outbound:
          # Publisher-side resiliency.
          retry: pubsubRetryOutbound
          timeout: pubsubTimeout
          circuitBreaker: pubsubBreaker
        inbound:
          # Subscriber-side resiliency.
          retry: pubsubRetryInbound
          timeout: subscriberTimeout
      statestore:
        # State store resiliency for event persistence,
        # snapshot read/write, and command status operations.
        retry: defaultRetry
        timeout: daprSidecar
        circuitBreaker: defaultBreaker
```

The resiliency policies augment built-in component retries. The effective retry count is `component built-in retries x resiliency retries`. For example, RabbitMQ has ~3 built-in publisher confirm retries, so with `maxRetries: 5` in `pubsubRetryOutbound`, the effective outbound retry count is approximately 15.

## Access Control

Access control defines service-to-service invocation policies. Each DAPR sidecar loads this configuration to evaluate incoming service invocation requests. Hexalith.EventStore uses a deny-by-default security posture (D4).

Source: `deploy/dapr/accesscontrol.yaml`

```yaml
apiVersion: dapr.io/v1alpha1
kind: Configuration
metadata:
  name: accesscontrol
spec:
  accessControl:
    # Deny-by-default: all service invocations are blocked unless explicitly allowed (D4).
    defaultAction: deny
    # SPIFFE trust domain for mTLS identity validation.
    # All sidecars must present certificates from this trust domain.
    # Mismatched trust domains are rejected at TLS handshake.
    trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
    policies:
      # commandapi: Trusted caller -- REST API + actor host + event publisher.
      # When commandapi invokes a domain service via DaprClient.InvokeMethodAsync,
      # the domain service's sidecar evaluates this policy.
      - appId: commandapi
        defaultAction: deny
        trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
        namespace: "{env:DAPR_NAMESPACE|hexalith}"
        operations:
          # Wildcard allows any method path since domain service method names
          # are dynamically resolved from the domain service registry.
          # POST-only: DaprClient.InvokeMethodAsync uses POST by default.
          - name: /**
            httpVerb: ['POST']
            action: allow
      # Production domain services template:
      # Add a new policy entry for each domain service with defaultAction: deny
      # and NO allowed operations (domain services cannot invoke other services).
      #
      # - appId: <domain-service-app-id>
      #   defaultAction: deny
      #   trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
      #   namespace: "<production-namespace>"
```

Azure Container Apps does NOT support `accesscontrol.yaml`. In ACA, equivalent security is achieved through component scoping (the `scopes` field on each component) and ACA's built-in networking isolation.

## Declarative Subscriptions

Declarative subscriptions define which topics a subscriber listens to, including dead-letter routing. Each subscription is a separate YAML file that DAPR loads alongside component configurations.

Source: `deploy/dapr/subscription-sample-counter.yaml`

```yaml
apiVersion: dapr.io/v2alpha1
kind: Subscription
metadata:
  name: sample-counter-events
spec:
  pubsubname: pubsub
  # Topic follows D6 naming: {tenant}.{domain}.events
  topic: sample.counter.events
  routes:
    # HTTP endpoint on the subscriber that receives events.
    default: /events/idempotency-demo
  # Dead-letter topic follows convention: deadletter.{tenant}.{domain}.events
  deadLetterTopic: deadletter.sample.counter.events
# Scoped to the subscriber app-id only.
scopes:
  - sample
```

The subscription pattern: a topic routes events to an HTTP endpoint on the subscriber application. If the subscriber fails to process the event after retry exhaustion, the message moves to the dead-letter topic. Dead-letter topic subscription is separate — only operational/monitoring tools should subscribe to dead-letter topics.

## Persistence Guarantees by Backend

### State Store Backends

| Backend | Durability | Consistency | ETag Support | Transaction Support | Where Data Lives | Backup Strategy |
|---------|-----------|-------------|-------------|-------------------|-----------------|----------------|
| Redis | In-memory (AOF/RDB optional) | Eventual | Yes (SET NX) | Multi-key MULTI/EXEC | Key-value store | RDB snapshots |
| PostgreSQL | WAL-based | ACID Strong | Yes (row version) | Full ACID | Relational `state` table | pg_dump / WAL archiving |
| Azure Cosmos DB | Multi-region replicated | Configurable (5 levels) | Yes (`_etag`) | Cross-partition limited | JSON documents | Azure Backup |

### Pub/Sub Backends

| Backend | Durability | Delivery Guarantee | Dead-Letter Support | Consumer Isolation | Auto-Create Topics |
|---------|-----------|-------------------|--------------------|--------------------|-------------------|
| Redis Streams | In-memory (AOF/RDB optional) | At-least-once | DAPR-managed | Consumer groups | Yes |
| RabbitMQ | Durable queues (configurable) | At-least-once | Native DLX | Queue-per-consumer | Yes |
| Kafka | Commit log (configurable retention) | At-least-once | Separate topic | Consumer group offset | Yes |
| Azure Service Bus | Guaranteed delivery | At-least-once | Native DLQ | Subscription-per-consumer | No (pre-create required) |

## Backend Swap Procedure

Switching backends requires zero code changes — only YAML configuration and environment variables change. Follow these steps:

1. **Stop services:** Shut down the CommandAPI and all subscriber services to prevent in-flight operations during the swap.
2. **Swap component YAML:** Replace the state store and/or pub/sub component YAML files with the target backend configuration. For example, replace `statestore.yaml` (Redis) with `statestore-postgresql.yaml` (PostgreSQL). The component `metadata.name` must remain `statestore` and `pubsub` respectively.
3. **Update environment variables:** Set the environment variables required by the new backend (see [Environment Variable Reference](#environment-variable-reference) below). Remove or unset variables from the previous backend.
4. **Restart services:** Start the DAPR sidecars and application services. DAPR loads the new component YAML on startup.
5. **Verify:** Confirm DAPR sidecar logs show `component loaded. name: statestore` and `component loaded. name: pubsub`. Send a test command to verify actor activation and event publishing.

See `deploy/README.md` in the repository for per-backend environment variable details.

## Environment Variable Reference

All backends use `{env:VAR_NAME}` substitution in DAPR component YAML. Set these environment variables before deploying.

### State Store Variables

| Variable | Backend | Example Value |
|----------|---------|---------------|
| `REDIS_HOST` | Redis | `localhost:6379` |
| `REDIS_PASSWORD` | Redis | (empty for local dev) |
| `POSTGRES_CONNECTION_STRING` | PostgreSQL | `host=mydb;port=5432;username=dapr;password=<secret>;database=eventstore;sslmode=require` |
| `COSMOSDB_URL` | Azure Cosmos DB | `https://myaccount.documents.azure.com:443/` |
| `COSMOSDB_KEY` | Azure Cosmos DB | (from Azure Portal or Key Vault) |
| `COSMOSDB_DATABASE` | Azure Cosmos DB | `eventstore` |
| `COSMOSDB_COLLECTION` | Azure Cosmos DB | `actorstate` |

### Pub/Sub Variables

| Variable | Backend | Example Value |
|----------|---------|---------------|
| `RABBITMQ_CONNECTION_STRING` | RabbitMQ | `amqp://user:pass@rabbitmq:5672/` |
| `KAFKA_BROKERS` | Kafka | `broker1:9092,broker2:9092` |
| `KAFKA_AUTH_TYPE` | Kafka | `none`, `password`, `mtls`, or `oidc` |
| `SERVICEBUS_CONNECTION_STRING` | Azure Service Bus | `Endpoint=sb://mynamespace.servicebus.windows.net/;SharedAccessKeyName=dapr;SharedAccessKey=<key>` |

### Scoping and Security Variables

| Variable | Used In | Example Value |
|----------|---------|---------------|
| `SUBSCRIBER_APP_ID` | Pub/Sub scoping | `my-subscriber` |
| `OPS_MONITOR_APP_ID` | Pub/Sub scoping | `ops-monitor` |
| `DAPR_TRUST_DOMAIN` | Access Control | `hexalith.io` |
| `DAPR_NAMESPACE` | Access Control | `hexalith` |

## Next Steps

- [Security Model Documentation](security-model.md) — authentication, authorization, and security architecture
- [Troubleshooting Guide](troubleshooting.md) — common DAPR component configuration issues and solutions
- [Deployment Progression Guide](deployment-progression.md) — how to move between deployment environments
- [Docker Compose Deployment](deployment-docker-compose.md) — local development with Docker Compose
- [Kubernetes Deployment](deployment-kubernetes.md) — on-premise production with Kubernetes
- [Azure Container Apps Deployment](deployment-azure-container-apps.md) — cloud PaaS with Azure Container Apps
- Configuration Reference (Story 15-1) — application-level configuration settings (future)
