[<- Back to Error Reference](./index.md)

# Too Many Requests (Backpressure)

**HTTP Status:** 429 Too Many Requests
**Problem Type:** `https://hexalith.io/problems/backpressure-exceeded`

## What Happened

The target resource has too many pending commands waiting to be processed. This is a **per-resource capacity limit** -- the specific resource you are targeting is overwhelmed. This is different from [rate-limit-exceeded](./rate-limit-exceeded.md), which is a per-tenant or per-consumer rate limit based on your request frequency.

## Common Causes

- Many commands targeting the same resource in rapid succession
- The resource's command processor is slower than the inbound command rate
- A burst of commands from multiple clients all targeting the same resource

## Example

### Request

```http
POST /api/v1/commands HTTP/1.1
Host: localhost:7275
Content-Type: application/json
Authorization: Bearer <your-jwt-token>

{
    "messageId": "increment-100",
    "tenant": "tenant-a",
    "domain": "counter",
    "aggregateId": "counter-1",
    "commandType": "IncrementCounter",
    "payload": {}
}
```

### Response

```http
HTTP/1.1 429 Too Many Requests
Content-Type: application/problem+json
Retry-After: 10

{
    "type": "https://hexalith.io/problems/backpressure-exceeded",
    "title": "Too Many Requests",
    "status": 429,
    "detail": "The target resource is under backpressure due to excessive pending commands. Please retry after the specified interval.",
    "instance": "/api/v1/commands",
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "tenantId": "tenant-a",
    "domain": "counter",
    "aggregateId": "counter-1"
}
```

## How to Fix

1. Wait for the duration specified in the `Retry-After` header.
2. Retry the command after the interval -- the backpressure is temporary and will clear as pending commands are processed.
3. If you are sending many commands to the same resource, space them out or batch them.
4. Consider distributing work across different resources when possible.

## Related

- [Error Reference Index](./index.md)
- [rate-limit-exceeded](./rate-limit-exceeded.md) -- per-tenant/per-consumer rate limit (different from backpressure)
