[<- Back to Error Reference](./index.md)

# Forbidden

**HTTP Status:** 403 Forbidden
**Problem Type:** `https://hexalith.io/problems/forbidden`

## What Happened

Your identity was authenticated successfully, but gateway tenant/RBAC validation denied the operation before EventStore invoked a domain service, projection adapter, ETag lookup, cache lookup, replay/read path, or resource-existence check.

## Common Causes

- Your token does not include the `eventstore:tenant` claim for the requested tenant
- The tenant ID in the command does not match any of your authorized tenants
- Query endpoint authorization failed (insufficient permissions for the requested query type)
- Command status query attempted without any tenant claims
- Hexalith.Tenants reports the tenant as missing, disabled, suspended, stale, ambiguous, or unavailable

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
    "tenantId": "tenant-b",
    "reason": "Not authorized for tenant 'tenant-b'. Access denied.",
    "reasonCode": "principal_not_member"
}
```

## Stable Authorization Reason Codes

| `reasonCode` | Retryable | Caller action | Safe for end-user display |
| --- | --- | --- | --- |
| `authentication_required` | No | Provide a valid token. | Yes |
| `subject_missing` | No | Fix token subject claim. | No |
| `tenant_missing` | No | Provide tenant ID. | Yes |
| `tenant_mismatch` | No | Align route/body/client tenant values. | Yes |
| `tenant_not_found` | No | Verify tenant exists and caller may see it. | Yes |
| `tenant_disabled` | No | Reactivate or choose another tenant. | Yes |
| `tenant_suspended` | No | Resolve tenant suspension. | Yes |
| `tenant_stale` | Yes | Retry after tenant authority refresh. | Yes |
| `tenant_unavailable` | Yes | Retry or contact platform operations. | Yes |
| `tenant_ambiguous` | No | Contact platform operations to repair authority data. | No |
| `principal_not_member` | No | Grant tenant membership. | Yes |
| `insufficient_role` | No | Grant required tenant role. | Yes |
| `insufficient_permission` | No | Grant required command/query permission. | Yes |
| `authorization_service_unavailable` | Yes | Retry after `Retry-After`. | Yes |

The extension name is `reasonCode`. The legacy `reason` extension and `detail` are safe explanatory text and must not be parsed for control flow.

## How to Fix

1. Check your JWT token claims -- verify it includes `eventstore:tenant` for the target tenant.
2. If you need access to a different tenant, request updated credentials from your identity provider administrator.
3. Ensure the `tenant` field in your command matches a tenant you are authorized for.

## Related

- [Error Reference Index](./index.md)
- [authentication-required](./authentication-required.md) -- if authentication itself is the issue
