# Stream Replay API

EventStore exposes downstream stream replay through the public client and Contracts package. Domain services rebuilding projections must use this public boundary; they must not read DAPR actor state keys, depend on `Hexalith.EventStore.Server`, call admin stream debugging endpoints, or query state-store indexes directly.

## Public Route

`POST /api/v1/streams/read`

Request body: `StreamReadRequest`

- `tenant`: tenant identifier.
- `domain`: domain identifier.
- `aggregateId`: aggregate-specific stream identifier. Domain-wide rebuild enumeration is operator-owned and is not exposed as raw state-store scans.
- `fromSequence`: exclusive lower sequence bound. Use `0` to read from the beginning.
- `toSequence`: optional inclusive upper sequence bound. When `toSequence` is set equal to `fromSequence` the request is valid and EventStore returns an empty page (`eventCount == 0`, `lastSequenceReturned == null`, `latestSequence >= currentSequence`). Use this shape to probe stream existence without reading events. Empty streams that have actor metadata (touched aggregates with no events persisted) return 200 with `eventCount == 0` and `latestSequence == 0`; only the absence of actor metadata returns 404 `missing-stream`. `latestSequence` is `>=` rather than `==` `currentSequence` because the actor may persist new events between the metadata read and the range read; the response always reflects the highest sequence number actually returned.
- `checkpoint`: optional `ProjectionRebuildCheckpoint` cursor metadata.
- `continuationToken`: opaque token from a prior page. Current implementation fails closed for supplied tokens until request-bound validation is enabled.
- `pageSize`: maximum events per page, bounded by EventStore.
- `projectionName`: optional projection/rebuild scope.

Response body: `StreamReadPage`

- `events`: ordered `StreamReadEvent` records with sequence, type name, payload bytes, serialization format, metadata version, message id, correlation id, causation id, timestamp, user id, and optional `protectionMetadata`. The optional `protectionMetadata` field carries the provider-neutral payload protection record (state, scheme, key alias, content hint, compatibility flags) stamped by Story 22.7a. Legacy events that predate Story 22.7a return `protectionMetadata.state = "Unprotected"` with `compatibilityFlags.legacy = "missing"`. See [Payload and Snapshot Protection Hooks](../guides/payload-protection-and-crypto-shredding.md) for the metadata contract.
- `metadata`: `fromSequence`, `toSequence`, `lastSequenceReturned` (`null` for empty pages), `latestSequence`, `eventCount`, `isTruncated`, and optional opaque `nextContinuationToken`.

## Client Usage

```csharp
StreamReadPage page = await eventStore.ReadStreamAsync(
    new StreamReadRequest(
        Tenant: "tenant-a",
        Domain: "party",
        AggregateId: "party-1",
        FromSequence: checkpoint.LastAppliedSequence,
        PageSize: 100),
    cancellationToken);

foreach (StreamReadEvent item in page.Events) {
    await projection.ApplyAsync(item, cancellationToken);
}
```

Reading a page does not advance rebuild progress. Advance a checkpoint only after the projection apply path accepts the page.

### Domain `/project` Idempotency Requirement

Domain services MUST implement `/project` as a strictly idempotent operation: applying the same page event sequence one or more times must produce the same projection state. EventStore writes projection state via `UpdateProjectionAsync` BEFORE persisting the per-aggregate rebuild checkpoint; on a checkpoint-save failure (`checkpoint-conflict` or `checkpoint-unavailable`), the projection state is already advanced but the per-aggregate progress cursor is not, and the next rebuild iteration will re-apply the same events. Non-idempotent domain `/project` handlers (counters that increment unconditionally, append-only logs that do not de-duplicate by event id, side effects that fire on every call) will double-apply and corrupt projection state. Use the `messageId` field on `StreamReadEvent` as the de-duplication key when persisting projection mutations.

Domain-service hosts should expose `/project` through `Hexalith.EventStore.DomainService` by implementing `IDomainProjectionHandler`. The SDK maps the canonical POST `/project` endpoint unless the application already mapped its own POST `/project`, and it validates discovered projection handlers at endpoint setup so duplicate domains fail deterministically instead of routing to the first matching handler. A request for a domain with no handler returns 404.

Projection event DTOs preserve `messageId` and `userId` evidence alongside sequence, timestamp, correlation, type, payload, and serialization format. Projection handlers must treat payload bytes as opaque event data until they deserialize the known event type; logs and errors must not expose raw payloads or protected metadata.

### Domain-Event Consumer Idempotency

Services that subscribe to EventStore-published domain events should use `AddEventStoreDomainEvents(...)`, `AddEventStoreDomainEventHandler<TEvent, THandler>()`, and `MapEventStoreDomainEvents()` rather than hand-written DAPR subscription endpoints. Domain authors write typed event handlers; the platform owns endpoint mapping, event-type resolution, envelope validation, context construction, and idempotency.

Consumed-event idempotency is keyed by the EventStore `messageId`. The default marker store is in-memory; a completed marker means the message was already handled or terminally skipped, so duplicate deliveries within the process are acknowledged without deserializing payloads or invoking handlers. Services that need durable completed-message markers can explicitly register the DAPR marker store when their sidecar has access to the configured state store. DAPR marker keys are scoped by topic, subscription route, and message id, and completed markers are written with first-write concurrency.

The two stores differ in their in-flight concurrency semantics, and opting into the DAPR store trades one guarantee for another rather than strictly strengthening dedup. The in-memory store mutually excludes a concurrently-processing duplicate within the same process (a second delivery is reported as retryable while the first is in flight). The DAPR store records only terminal completion — it takes no in-progress lease — so two concurrent deliveries of the same `messageId` (for example across replicas) can both pass acquisition and dispatch handlers. Registering the DAPR store therefore buys durability across process restarts and replicas, not stronger in-flight exclusion; handlers must be idempotent under at-least-once delivery in either configuration.

No durable in-progress lease is defined by this story. Handler execution remains an at-least-once delivery boundary: if handler execution throws before completion, the in-process marker is released and the exception propagates so DAPR can redeliver the message. If completion marker persistence fails after handlers already ran, the endpoint acknowledges the message rather than forcing an immediate duplicate side-effect retry. Handlers must therefore be idempotent, especially when multiple handlers are registered for the same event type; use one composite handler if several non-idempotent side effects must succeed or fail together.

Invalid or unsupported envelopes are handled intentionally. Unknown event types, missing handlers, aggregate-identity mismatches, unsupported serialization formats, and malformed payloads do not dispatch handlers and are acknowledged to avoid poison-message loops. In-progress marker conflicts remain retryable rather than being acknowledged as duplicates. Consumer logs include safe envelope metadata only, not raw payload bytes, decoded payloads, state-store internals, stack traces, cursor values, or ETag values.

## Checkpoint Semantics

`ProjectionRebuildCheckpoint` is scoped by tenant, domain, projection name, optional aggregate id, and operation id. EventStore persists rebuild progress with optimistic concurrency and monotonic max-sequence semantics:

- Duplicate page application is idempotent.
- Lower or stale checkpoint writes cannot reduce `lastAppliedSequence`.
- Checkpoint store conflicts return `checkpoint-conflict`.
- Checkpoint store unavailability returns `checkpoint-unavailable`.
- Failure statuses keep a sanitized `failureReasonCode` and must not expose payload bytes, state-store keys, actor ids, connection strings, or continuation-token internals.
- Bounded replay operations persist `toPosition` as operator intent; it is not treated as applied progress.

## Tenant and Domain Canonicalization

Tenant and domain identifiers are case-sensitive at the state-store layer. EventStore validates incoming `tenant` and `domain` as lowercase ASCII alphanumeric (`a-z`, `0-9`, and `-`), maximum 64 characters, alphanumeric first and last character. Callers MUST canonicalize identifiers to lowercase before issuing stream-read or rebuild lifecycle requests; uppercase or mixed-case identifiers are rejected with `invalid-aggregate-identity` to prevent cross-case row aliasing.

## Projection Name Constraint

The current poller/rebuild conflict check assumes `projectionName == domain` for domain projection rebuilds. If an operator uses a projection name that differs from the EventStore domain, the conflict guard may not identify the same checkpoint scope. Use the domain name as the projection name for this story's rebuild endpoints until a later contract introduces an explicit domain-to-projection mapping.

## Operator Lifecycle

### Domain-wide Rebuild Progress Reporting

For domain-wide rebuilds (no `aggregateId`), the operator-scope checkpoint row's `lastAppliedSequence` is intentionally reported as `0` after `succeeded`. Per-aggregate checkpoint rows carry truthful per-aggregate progress; admin/CLI/MCP callers should treat the operator-scope `lastAppliedSequence` for domain-wide rebuilds as a "rebuild completed" marker rather than a progress indicator. The cross-aggregate maximum would otherwise inflate to an artifact of the largest aggregate's sequence space (e.g., a domain covering aggregates A `{0..100}` and B `{0..1000}` would report `1000` which is not meaningful as domain-wide progress). Aggregate-scoped rebuilds (with an explicit `aggregateId`) report the actual applied sequence.

Operator rebuild lifecycle endpoints are under `api/v1/admin/projections/{tenantId}/{projectionName}`:

- `GET rebuild-status`
- `POST pause`
- `POST resume`
- `POST reset`
- `POST replay`
- `POST cancel`
- `POST retry`

Lifecycle states use `ProjectionRebuildStatus`: `not-started`, `running`, `pausing`, `paused`, `resuming`, `canceling`, `canceled`, `retrying`, `succeeded`, and `failed`.

### Lifecycle State-Machine Transitions

- `not-started` → `running` (operator start via `replay`)
- `not-started` → `canceled` (operator cancel against an unstarted scope; cancel-cleanup path)
- `running` → `pausing` → `paused` (operator pause)
- `paused` → `resuming` → `running` (operator resume)
- `running` → `canceling` → `canceled` (operator cancel)
- `running` → `succeeded` (page-complete with terminal advancement)
- `running` → `failed` (transient store/projection failure with sanitized `failureReasonCode`)
- `failed` → `retrying` → `running` (operator retry; orchestrator-driven)
- terminal states (`succeeded`, `failed`, `canceled`): a new operator action against the same `(tenant, domain, projectionName, aggregateId?)` scope requires routing through `ResetAsync` (which is the documented trust boundary for terminal-record OperationId overwrite).

Normal polling and operator rebuilds use a reject policy: when an active rebuild checkpoint exists for the domain projection, normal projection delivery skips that aggregate and logs `poller-rebuild-conflict` instead of racing the rebuild checkpoint.

## Failure Reason Codes

Stable stream replay and rebuild reason codes include:

- `invalid-range`
- `missing-required-field`
- `invalid-aggregate-identity`
- `invalid-continuation`
- `token-request-mismatch`
- `unauthorized-tenant`
- `forbidden-replay-scope`
- `forbidden-role`
- `missing-stream`
- `missing-event`
- `corrupt-event`
- `protected-payload-unavailable`
- `projection-apply-rejected`
- `checkpoint-conflict`
- `stale-checkpoint`
- `operation-in-flight`
- `checkpoint-drift`
- `checkpoint-unavailable`
- `poller-rebuild-conflict`
- `rebuild-operation-not-found`
- `rebuild-canceled`
- `rebuild-paused`
- `operator-preempted`
- `domain-failure`
- `retryable-transient-failure`
- `service-unavailable`
- `no-domain-service`
- `internal-error`

## Forbidden Paths

Downstream rebuild implementations must not use:

- `api/v1/admin/streams/*` debugging endpoints.
- DAPR actor ids or raw actor state keys.
- `DaprClient.GetStateAsync` against EventStore event stream keys.
- `ProjectionCheckpointTracker` or other `Hexalith.EventStore.Server` internals.
- DAPR sidecar addresses, connection strings, or state-store index records.

Use `Hexalith.EventStore.Contracts.Streams`, `IEventStoreGatewayClient.ReadStreamAsync`, and test fakes/builders from `Hexalith.EventStore.Testing` instead.
