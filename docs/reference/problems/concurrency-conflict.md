[<- Back to Error Reference](./index.md)

# Conflict

**HTTP Status:** 409 Conflict
**Problem Type:** `https://hexalith.io/problems/concurrency-conflict`

## What Happened

EventStore mapped an `InvalidOperationException` from the guarded pre-`EventsStored` fence-validation/commit block to its concurrency-conflict path and exhausted the configured retry budget. The handler rehydrates before each retry. Do not infer that ordinary same-aggregate concurrency necessarily reaches this response: actor-dispatched commands are serialized, and the Story 4.5 Redis two-writer spike observed a silent same-key overwrite with no exception, retry, or conflict result.

## Common Causes

- A backend or actor-state provider rejected an optimistic state transaction and surfaced it as `InvalidOperationException`
- An execution-fence validation failed through the same guarded commit block
- Provider-specific concurrency behavior differed from the Redis live-sidecar profile

## EventStore Behavior

- Retry budget: `EventStore:CommandConcurrency:MaxPersistenceConflictRetries`, default `1`; this is configured code, not proof that the active provider surfaces a retryable exception.
- Retryable source in the implemented handler: an `InvalidOperationException` from the guarded pre-`EventsStored` fence validation or actor-state commit.
- Non-retryable source: any conflict after `EventsStored`, because events are already committed and must not be persisted again.
- Terminal mapping: command status `Rejected` with `failureReason` set to `ConcurrencyConflict`, HTTP `409`, and `Retry-After: 1`.
- Idempotency: duplicate causation IDs return cached terminal results and do not append duplicate events.

The [Story 4.5 evidence report](../../../_bmad-output/implementation-artifacts/4-5-append-durability-race-evidence.md) classifies the five current catches and records the observed Redis and generic-state conflict surfaces.

## Example

### Request

```http
POST /api/v1/commands HTTP/1.1
Host: localhost:7275
Content-Type: application/json
Authorization: Bearer <your-jwt-token>

{
    "messageId": "increment-02",
    "tenant": "tenant-a",
    "domain": "counter",
    "aggregateId": "counter-1",
    "commandType": "IncrementCounter",
    "payload": {}
}
```

### Response

```http
HTTP/1.1 409 Conflict
Content-Type: application/problem+json
Retry-After: 1

{
    "type": "https://hexalith.io/problems/concurrency-conflict",
    "title": "Conflict",
    "status": 409,
    "detail": "A concurrency conflict occurred. Please retry the command.",
    "instance": "/api/v1/commands",
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

## How to Fix

1. Wait for the duration specified in the `Retry-After` header (1 second).
2. Resubmit the **exact same command** with the same payload.
3. The conflict is transient -- the other command was processed first, but yours is still valid.
4. For automated clients, implement exponential backoff starting from the `Retry-After` value.

## Related

- [Error Reference Index](./index.md)
- [backpressure-exceeded](./backpressure-exceeded.md) -- if the resource has too many pending commands
