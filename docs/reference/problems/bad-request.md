[<- Back to Error Reference](./index.md)

# Bad Request

**HTTP Status:** 400 Bad Request
**Problem Type:** `https://hexalith.io/problems/bad-request`

## What Happened

The request is malformed or missing a required parameter. This differs from [validation-error](./validation-error.md) -- it applies to request-level issues rather than command payload validation.

## Common Causes

- The `messageId` path segment is invalid when querying command status: it is not 1-128 ASCII letters, digits or hyphens with an alphanumeric first and last character
- A required query parameter is absent

## Example

### Request

```http
GET /api/v1/commands/status/-not-a-message-id HTTP/1.1
Host: localhost:7275
Authorization: Bearer <your-jwt-token>
```

### Response

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
    "type": "https://hexalith.io/problems/bad-request",
    "title": "Bad Request",
    "status": 400,
    "detail": "Message or correlation identifier must be 1-128 characters of alphanumerics and hyphens (with alphanumeric anchors).",
    "instance": "/api/v1/commands/status/-not-a-message-id",
    "correlationId": "01HKQXYZ0000000000000000E5"
}
```

## How to Fix

1. Put a valid `messageId` in the request path:

    ```text
    GET /api/v1/commands/status/01HKQXYZ0000000000000000C3
    ```

2. Use the `messageId` from the `202 Accepted` body returned when you submitted the command, or follow that response's `Location` header.

3. On the retried request, send a bearer token and exactly one scope header. For either scope, the gateway returns `403 Forbidden` when a request with a valid `messageId` includes a `tenant` or `tenantId` query parameter.

    Use `X-Hexalith-Tenant` for the command's tenant with a token authorized for that tenant. For a platform command, send `X-Hexalith-Scope: platform:tenants` with the `tenants:read` permission or `X-Hexalith-Scope: platform:global-administrators` with the `global-administrators:read` permission. After gateway claim normalization, exactly one `eventstore:permission` claim must match the required value. The JWT can supply that value through `permissions`.

## Related

- [Error Reference Index](./index.md)
- [validation-error](./validation-error.md) -- for command payload validation failures
- [command-status-not-found](./command-status-not-found.md) -- if the message ID is valid but no status exists
