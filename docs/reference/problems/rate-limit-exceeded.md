[<- Back to Error Reference](./index.md)

# Too Many Requests (Rate Limit)

**HTTP Status:** 429 Too Many Requests
**Problem Type:** `https://hexalith.io/problems/rate-limit-exceeded`

## What Happened

Your tenant or client has exceeded the allowed request rate. This is a **per-tenant or per-consumer rate limit** -- you sent too many requests within the configured time window. This is different from [backpressure-exceeded](./backpressure-exceeded.md), which is a per-resource capacity limit.

## Common Causes

- Submitting commands faster than the configured rate for your tenant
- Multiple clients sharing the same tenant credentials and collectively exceeding the limit
- Automated scripts or load tests without rate throttling

## Example

### Request

```http
POST /api/v1/commands HTTP/1.1
Host: localhost:7275
Content-Type: application/json
Authorization: Bearer <your-jwt-token>

{
    "messageId": "increment-50",
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
Retry-After: 60

{
    "type": "https://hexalith.io/problems/rate-limit-exceeded",
    "title": "Too Many Requests",
    "status": 429,
    "detail": "Rate limit exceeded. Please retry after the specified interval.",
    "instance": "/api/v1/commands",
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "tenantId": "tenant-a",
    "consumerId": "client-app-1"
}
```

## How to Fix

1. Wait for the duration specified in the `Retry-After` header before sending new requests.
2. Implement client-side rate throttling to stay within your tenant's allowed request rate.
3. If you consistently hit rate limits, contact your service administrator to discuss increasing the limit.
4. Distribute commands over time rather than sending bursts.

## Related

- [Error Reference Index](./index.md)
- [backpressure-exceeded](./backpressure-exceeded.md) -- per-resource capacity limit (different from rate limiting)
