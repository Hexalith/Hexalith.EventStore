[<- Back to Error Reference](./index.md)

# Service Unavailable

**HTTP Status:** 503 Service Unavailable
**Problem Type:** `https://hexalith.io/problems/service-unavailable`

## What Happened

The command processing pipeline is temporarily unavailable. The API server received your request but cannot forward it for processing because one or more infrastructure components are down or unreachable.

## Common Causes

- The command processing infrastructure is starting up and not yet ready
- A required service component is temporarily offline
- Network connectivity issues between internal service components
- The authorization service is unreachable

## Example

### Request

```http
POST /api/v1/commands HTTP/1.1
Host: localhost:7275
Content-Type: application/json
Authorization: Bearer <your-jwt-token>

{
    "messageId": "increment-01",
    "tenant": "tenant-a",
    "domain": "counter",
    "aggregateId": "counter-1",
    "commandType": "IncrementCounter",
    "payload": {}
}
```

### Response

```http
HTTP/1.1 503 Service Unavailable
Content-Type: application/problem+json
Retry-After: 30

{
    "type": "https://hexalith.io/problems/service-unavailable",
    "title": "Service Unavailable",
    "status": 503,
    "detail": "The command processing pipeline is temporarily unavailable. Please retry after the specified interval.",
    "instance": "/api/v1/commands"
}
```

## How to Fix

**For API consumers:**

1. Wait for the duration specified in the `Retry-After` header (30 seconds).
2. Retry the request. The condition is typically temporary.
3. If the error persists after several retries, contact the service operator.

**For service operators:**

1. Check infrastructure health endpoints to identify which component is down.
2. Verify all service components are running (use the Aspire dashboard if available).
3. Check network connectivity between service components.
4. Review structured logs for the specific infrastructure failure.

## Related

- [Error Reference Index](./index.md)
- [internal-server-error](./internal-server-error.md) -- for unexpected failures rather than infrastructure unavailability
