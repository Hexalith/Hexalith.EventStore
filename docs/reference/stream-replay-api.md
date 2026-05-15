# Stream Replay API

EventStore exposes downstream stream replay through the public client and Contracts package. Domain services rebuilding projections must use this public boundary; they must not read DAPR actor state keys, depend on `Hexalith.EventStore.Server`, call admin stream debugging endpoints, or query state-store indexes directly.

## Public Route

`POST /api/v1/streams/read`

Request body: `StreamReadRequest`

- `tenant`: tenant identifier.
- `domain`: domain identifier.
- `aggregateId`: aggregate-specific stream identifier. Domain-wide rebuild enumeration is operator-owned and is not exposed as raw state-store scans.
- `fromSequence`: exclusive lower sequence bound. Use `0` to read from the beginning.
- `toSequence`: optional inclusive upper sequence bound.
- `checkpoint`: optional `ProjectionRebuildCheckpoint` cursor metadata.
- `continuationToken`: opaque token from a prior page. Current implementation fails closed for supplied tokens until request-bound validation is enabled.
- `pageSize`: maximum events per page, bounded by EventStore.
- `projectionName`: optional projection/rebuild scope.

Response body: `StreamReadPage`

- `events`: ordered `StreamReadEvent` records with sequence, type name, payload bytes, serialization format, metadata version, message id, correlation id, causation id, timestamp, and user id.
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

## Checkpoint Semantics

`ProjectionRebuildCheckpoint` is scoped by tenant, domain, projection name, optional aggregate id, and operation id. EventStore persists rebuild progress with optimistic concurrency and monotonic max-sequence semantics:

- Duplicate page application is idempotent.
- Lower or stale checkpoint writes cannot reduce `lastAppliedSequence`.
- Checkpoint store conflicts return `checkpoint-conflict`.
- Checkpoint store unavailability returns `checkpoint-unavailable`.
- Failure statuses keep a sanitized `failureReasonCode` and must not expose payload bytes, state-store keys, actor ids, connection strings, or continuation-token internals.
- Bounded replay operations persist `toPosition` as operator intent; it is not treated as applied progress.

## Projection Name Constraint

The current poller/rebuild conflict check assumes `projectionName == domain` for domain projection rebuilds. If an operator uses a projection name that differs from the EventStore domain, the conflict guard may not identify the same checkpoint scope. Use the domain name as the projection name for this story's rebuild endpoints until a later contract introduces an explicit domain-to-projection mapping.

## Operator Lifecycle

Operator rebuild lifecycle endpoints are under `api/v1/admin/projections/{tenantId}/{projectionName}`:

- `GET rebuild-status`
- `POST pause`
- `POST resume`
- `POST reset`
- `POST replay`
- `POST cancel`
- `POST retry`

Lifecycle states use `ProjectionRebuildStatus`: `not-started`, `running`, `pausing`, `paused`, `resuming`, `canceling`, `canceled`, `retrying`, `succeeded`, and `failed`.

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
- `missing-stream`
- `missing-event`
- `corrupt-event`
- `protected-payload-unavailable`
- `projection-apply-rejected`
- `checkpoint-conflict`
- `checkpoint-drift`
- `checkpoint-unavailable`
- `poller-rebuild-conflict`
- `rebuild-operation-not-found`
- `rebuild-canceled`
- `rebuild-paused`
- `domain-failure`
- `retryable-transient-failure`
- `service-unavailable`
- `internal-error`

## Forbidden Paths

Downstream rebuild implementations must not use:

- `api/v1/admin/streams/*` debugging endpoints.
- DAPR actor ids or raw actor state keys.
- `DaprClient.GetStateAsync` against EventStore event stream keys.
- `ProjectionCheckpointTracker` or other `Hexalith.EventStore.Server` internals.
- DAPR sidecar addresses, connection strings, or state-store index records.

Use `Hexalith.EventStore.Contracts.Streams`, `IEventStoreGatewayClient.ReadStreamAsync`, and test fakes/builders from `Hexalith.EventStore.Testing` instead.
