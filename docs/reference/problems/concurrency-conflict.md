[<- Back to Error Reference](./index.md)

# Conflict

**HTTP Status:** 409 Conflict
**Problem Type:** `https://hexalith.io/problems/concurrency-conflict`

## What Happened

Another command targeting the same resource was processed just before yours. EventStore automatically retries state-store optimistic concurrency conflicts that occur before the `EventsStored` checkpoint is committed, rehydrating the latest aggregate state before each retry. This response means the configured retry limit was exhausted and the caller must decide whether to resubmit.

## Common Causes

- Two clients submitted commands for the same resource at nearly the same time
- A retry arrived while the original command was still being processed
- High-frequency updates to a single resource from multiple consumers

## EventStore Behavior

- Retry limit: `EventStore:CommandConcurrency:MaxPersistenceConflictRetries`, default `1`.
- Retryable source: actor state-store optimistic concurrency conflicts before a successful `EventsStored` checkpoint.
- Non-retryable source: any conflict after `EventsStored`, because events are already committed and must not be persisted again.
- Terminal mapping: command status `Rejected` with `failureReason` set to `ConcurrencyConflict`, HTTP `409`, and `Retry-After: 1`.
- Idempotency: duplicate causation IDs return cached terminal results and do not append duplicate events.

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
