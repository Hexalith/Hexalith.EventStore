[<- Back to Error Reference](./index.md)

# Not Implemented

**HTTP Status:** 501 Not Implemented
**Problem Type:** `https://hexalith.io/problems/not-implemented`

## What Happened

The query endpoint you are trying to use is not available. The server recognized the request but does not support the functionality required to fulfill it.

## Common Causes

- The query type is not registered in the current service configuration
- The feature is planned but not yet implemented in this version
- A typo in the query type name

## Example

### Request

```http
GET /api/v1/queries/UnsupportedQueryType HTTP/1.1
Host: localhost:7275
Authorization: Bearer <your-jwt-token>
```

### Response

```http
HTTP/1.1 501 Not Implemented
Content-Type: application/problem+json

{
    "type": "https://hexalith.io/problems/not-implemented",
    "title": "Not Implemented",
    "status": 501,
    "detail": "The query type 'UnsupportedQueryType' is not implemented.",
    "instance": "/api/v1/queries/UnsupportedQueryType",
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

## How to Fix

1. Verify the query type name is spelled correctly.
2. Check the API documentation for supported query types.
3. If the query type should be available, confirm the domain service that provides it is registered and running.

## Related

- [Error Reference Index](./index.md)
- [not-found](./not-found.md) -- for resources that exist but were not found
