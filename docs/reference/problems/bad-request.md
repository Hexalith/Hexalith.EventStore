[<- Back to Error Reference](./index.md)

# Bad Request

**HTTP Status:** 400 Bad Request
**Problem Type:** `https://hexalith.io/problems/bad-request`

## What Happened

The request is malformed or missing a required parameter. This differs from [validation-error](./validation-error.md) -- it applies to request-level issues rather than command payload validation.

## Common Causes

- The `correlationId` path parameter is missing or empty when querying command status
- A required query parameter is absent

## Example

### Request

```http
GET /api/v1/commands/status/ HTTP/1.1
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
    "detail": "Correlation ID is required.",
    "instance": "/api/v1/commands/status/",
    "correlationId": ""
}
```

## How to Fix

1. Include the required `correlationId` in the request path:

    ```
    GET /api/v1/commands/status/a1b2c3d4-e5f6-7890-abcd-ef1234567890
    ```

2. Use the `correlationId` returned in the `202 Accepted` response when you originally submitted the command.

## Related

- [Error Reference Index](./index.md)
- [validation-error](./validation-error.md) -- for command payload validation failures
- [command-status-not-found](./command-status-not-found.md) -- if the correlation ID is valid but no status exists
