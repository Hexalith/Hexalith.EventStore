# Data Models — Hexalith.EventStore

> This is an event-sourcing system: there is **no relational schema**. The "data models" are the
> contract records (envelopes, metadata, results, identities), the **storage-key scheme** in the
> DAPR state store, and the **DAPR component** definitions. All types live in
> `src/Hexalith.EventStore.Contracts`.

## Core Identity — `AggregateIdentity` (`Contracts/Identity/AggregateIdentity.cs`)

```csharp
record AggregateIdentity(
    string TenantId,    // lowercased; ^[a-z0-9]([a-z0-9-]*[a-z0-9])?$ ; max 64
    string Domain,      // lowercased; ^[a-z0-9]([a-z0-9-]*[a-z0-9])?$ ; max 64
    string AggregateId) // case-sensitive; ^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$ ; max 256
```

**Colons are forbidden** in every component → key spaces are structurally disjoint per tenant
(FR15/FR28). Derived keys (the actual state-store layout):

| Property | Format |
|----------|--------|
| `ActorId` | `{TenantId}:{Domain}:{AggregateId}` |
| `EventStreamKeyPrefix` | `{TenantId}:{Domain}:{AggregateId}:events:` |
| Event key (per event) | `{...}:events:{SequenceNumber}` (write-once) |
| `MetadataKey` | `{...}:metadata` (holds `CurrentSequence`, `LastModified`, `ETag`) |
| `SnapshotKey` | `{...}:snapshot` |
| `PipelineKeyPrefix` | `{...}:pipeline:` (actor pipeline checkpoints, NFR25) |
| `PubSubTopic` | `{Domain}.events` (system tenant) or `{TenantId}.{Domain}.events` |

`IdentityParser` parses canonical `"tenant:domain:aggregate"` strings and state-store keys (with `TryParse` variants).

## Command Envelope (`Contracts/Commands/CommandEnvelope.cs`)

`record CommandEnvelope(MessageId, TenantId, Domain, AggregateId, CommandType, Payload, CorrelationId, CausationId?, UserId, Extensions?)`
— `[DataContract]`/`[DataMember]`; computes `AggregateIdentity`; **redacts `Payload` in `ToString()`** (SEC-5).

- `SubmitCommandRequest` — API DTO (`Payload` as `JsonElement`, optional `CorrelationId`/`Extensions`).
- `CommandStatus` enum: `Received(0) Processing(1) EventsStored(2) EventsPublished(3) Completed(4) Rejected(5) PublishFailed(6) TimedOut(7)`.
- `CommandStatusRecord` — status + rejection type + failure reason. `ArchivedCommand` — for replay.

## Event Envelope & Metadata (`Contracts/Events/`)

```csharp
record EventEnvelope(EventMetadata Metadata, byte[] Payload, IReadOnlyDictionary<string,string>? Extensions);
```

`EventMetadata` — **15 fields** (FR11): `MessageId, AggregateId, AggregateType, TenantId, Domain,
SequenceNumber (>=1), GlobalPosition (>=0), Timestamp, CorrelationId, CausationId, UserId,
DomainServiceVersion, EventTypeName, MetadataVersion (>=1), SerializationFormat`.

- `IEventPayload` — marker for all domain events. `IRejectionEvent : IEventPayload` — rejections.
- `ISerializedEventPayload` — pre-serialized payload carrying `EventTypeName`/`PayloadBytes`/`SerializationFormat`.
- `AggregateTerminated(AggregateType, AggregateId)` — framework rejection for tombstoned aggregates (FR66).
- Events published to pub/sub as **CloudEvents 1.0** (FR17).

## Results (`Contracts/Results/`)

- `DomainResult` — `IReadOnlyList<IEventPayload> Events`; validates events are **all-regular or
  all-rejection, never mixed**. `IsSuccess` / `IsRejection` / `IsNoOp`. Factories `Success()`,
  `Rejection()`, `NoOp()`.
- `DomainServiceWireResult` / `DomainServiceWireEvent(EventTypeName, byte[] Payload, SerializationFormat="json")`
  — wire-safe transport form (`FromDomainResult`).

## Queries (`Contracts/Queries/`)

- `IQueryContract` — `static abstract QueryType / Domain / ProjectionType` (FR57).
- `IQueryResponse<out T>` — `Data` + `ProjectionType` (base64url-encoded into ETags, FR64).
- `QueryEnvelope` / `QueryResult` — `[DataContract]` with namespace **pinned** to
  `http://schemas.datacontract.org/2004/07/Hexalith.EventStore.Server.Actors` for DAPR actor remoting
  stability; `Payload`/`PayloadBytes` are byte arrays for `DataContractSerializer` stability; payload redacted in `ToString()`.
- Supporting: `QueryFilter`, `QuerySort`, `QueryPagingOptions`, `QueryFreshnessPolicy`, `QueryPolicyLimits`,
  `QueryProblemReasonCodes`, `QueryWarningCodes`, `QueryAdapterFailureReason`.

## Security / Compliance Models (`Contracts/Security/`)

- `EventStorePayloadProtectionMetadata(State, MetadataVersion>=1, Scheme?, KeyAlias?, ContentHint?, CompatibilityFlags?)`
  — provider-neutral, **non-secret** protection metadata (bounds enforced). Factories `Unprotected()`, `ProviderOpaque()`.
- `PayloadProtectionState` enum; `IEventPayloadProtectionService` (default no-op).
- **Crypto-shredding** workflow: `CryptoShreddingWorkflowRequest/State/Decision/Scope/Identity/Transitions`,
  `CryptoShreddingNextAction`, `CryptoShreddingAuditEvent`.
- **Restored-backup admission**: `RestoredBackupAdmissionRequest/Result/State/Transitions`.

## Message Type Value Object (`Contracts/Messages/MessageType.cs`)

Canonical string `{domain}-{name}-v{version}` (domain lowercase no-hyphen; name kebab-case; version >=1;
max 192). `[JsonConverter(typeof(MessageTypeJsonConverter))]` serializes as a plain string.
`Parse`/`TryParse`/`Assemble(domain, type, version)`.

## DAPR State Store Layout

| Concern | Mechanism | Key/Notes |
|---------|-----------|-----------|
| Aggregate events | `IActorStateManager` (actor-scoped) | `{...}:events:{n}` (write-once) |
| Aggregate metadata | actor state | `{...}:metadata` (sequence, ETag) |
| Snapshots | actor state | `{...}:snapshot` |
| ETags | `ETagActor` state | per `{ProjectionType}:{TenantId}` |
| Projection state | `EventReplayProjectionActor` state | DAPR type name `"ProjectionActor"` |
| Command status | `DaprCommandStatusStore` | `SaveStateAsync` + TTL (default 24h) |
| Command archive | `DaprCommandArchiveStore` | for replay; TTL |
| Composite key (prod) | — | `{tenant}\|\|{domain}\|\|{aggregateId}` (deploy/README.md, D1) |

> **Never** use `DaprClient.QueryStateAsync()` / bulk queries without explicit tenant filtering;
> always use actor-scoped state managers (FR28 enforcement).

## DAPR Components

**Local (dev)** — `src/Hexalith.EventStore.AppHost/DaprComponents/`:

| File | Type | Purpose |
|------|------|---------|
| `statestore.yaml` | `state.redis` | Actor state/snapshots/command status; scoped to `eventstore`, `eventstore-admin`, `tenants`; `keyPrefix=none` so admin reads same keys |
| `pubsub.yaml` | `pubsub.redis` | 3-layer scoping (component/publish/subscribe); dead-letter enabled |
| `accesscontrol.yaml` (+ `.eventstore-admin`/`.sample`/`.tenants`) | Configuration | Per-service ACLs (allow-by-default in self-hosted; deny+mTLS in prod) |
| `resiliency.yaml` | Resiliency | Retry/timeout/circuit-breaker (pubsub CB opens >5 failures → drain recovery) |
| `subscription-sample-counter.yaml` | Subscription | Declarative subscription + per-subscription dead-letter reference |

**Production** — `deploy/dapr/` (backend-swappable, zero code change, NFR29):

| Backend | File | Component type |
|---------|------|----------------|
| PostgreSQL | `statestore-postgresql.yaml` | `state.postgresql` |
| Cosmos DB | `statestore-cosmosdb.yaml` | `state.azure.cosmosdb` |
| RabbitMQ | `pubsub-rabbitmq.yaml` | `pubsub.rabbitmq` |
| Kafka | `pubsub-kafka.yaml` | `pubsub.kafka` |
| Azure Service Bus | `pubsub-servicebus.yaml` | `pubsub.azure.servicebus.topics` |
| + | `resiliency.yaml`, `accesscontrol*.yaml`, `subscription-*.yaml` | resiliency / ACL / subscriptions |
