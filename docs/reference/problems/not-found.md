[<- Back to Error Reference](./index.md)

# Not Found

**HTTP Status:** 404 Not Found
**Problem Type:** `https://hexalith.io/problems/not-found`

## What Happened

The requested resource could not be found. This error is returned by query endpoints when the resource you are looking for does not exist.

## Common Causes

- The resource identifier does not match any existing resource
- The resource was deleted or never created
- A typo in the query type or resource identifier

## Example

### Request

```http
GET /api/v1/queries/CounterProjection?aggregateId=counter-999 HTTP/1.1
Host: localhost:7275
Authorization: Bearer <your-jwt-token>
```

### Response

```http
HTTP/1.1 404 Not Found
Content-Type: application/problem+json

{
    "type": "https://hexalith.io/problems/not-found",
    "title": "Not Found",
    "status": 404,
    "detail": "The requested resource was not found.",
    "instance": "/api/v1/queries/CounterProjection",
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

## How to Fix

1. Verify the resource identifier is correct.
2. Confirm the resource has been created by submitting the appropriate command first.
3. Check that you are querying the correct query type for the resource.

## Related

- [Error Reference Index](./index.md)
- [command-status-not-found](./command-status-not-found.md) -- specifically for command status queries
