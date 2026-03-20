[<- Back to Error Reference](./index.md)

# Forbidden

**HTTP Status:** 403 Forbidden
**Problem Type:** `https://hexalith.io/problems/forbidden`

## What Happened

Your identity was authenticated successfully, but you do not have permission to perform the requested operation. This typically means your JWT token lacks the required tenant authorization claim.

## Common Causes

- Your token does not include the `eventstore:tenant` claim for the requested tenant
- The tenant ID in the command does not match any of your authorized tenants
- Query endpoint authorization failed (insufficient permissions for the requested query type)
- Command status query attempted without any tenant claims

## Example

### Request

```http
POST /api/v1/commands HTTP/1.1
Host: localhost:7275
Content-Type: application/json
Authorization: Bearer <your-jwt-token>

{
    "messageId": "increment-01",
    "tenant": "tenant-b",
    "domain": "counter",
    "aggregateId": "counter-1",
    "commandType": "IncrementCounter",
    "payload": {}
}
```

### Response

```http
HTTP/1.1 403 Forbidden
Content-Type: application/problem+json

{
    "type": "https://hexalith.io/problems/forbidden",
    "title": "Forbidden",
    "status": 403,
    "detail": "Not authorized for tenant 'tenant-b'. Access denied.",
    "instance": "/api/v1/commands",
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "tenantId": "tenant-b"
}
```

## How to Fix

1. Check your JWT token claims -- verify it includes `eventstore:tenant` for the target tenant.
2. If you need access to a different tenant, request updated credentials from your identity provider administrator.
3. Ensure the `tenant` field in your command matches a tenant you are authorized for.

## Related

- [Error Reference Index](./index.md)
- [authentication-required](./authentication-required.md) -- if authentication itself is the issue
