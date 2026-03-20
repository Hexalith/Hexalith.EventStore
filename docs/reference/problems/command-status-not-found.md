[<- Back to Error Reference](./index.md)

# Command Status Not Found

**HTTP Status:** 404 Not Found
**Problem Type:** `https://hexalith.io/problems/command-status-not-found`

## What Happened

No command status record was found for the correlation ID you provided. The system has no record of a command with that identifier.

## Common Causes

- The correlation ID is incorrect or contains a typo
- The command was never submitted (the original submission may have failed before being recorded)
- The correlation ID belongs to a different environment or service instance
- Status records have been purged (if retention policies are configured)

## Example

### Request

```http
GET /api/v1/commands/status/ffffffff-ffff-ffff-ffff-ffffffffffff HTTP/1.1
Host: localhost:7275
Authorization: Bearer <your-jwt-token>
```

### Response

```http
HTTP/1.1 404 Not Found
Content-Type: application/problem+json

{
    "type": "https://hexalith.io/problems/command-status-not-found",
    "title": "Not Found",
    "status": 404,
    "detail": "No command status found for correlation ID 'ffffffff-ffff-ffff-ffff-ffffffffffff'.",
    "instance": "/api/v1/commands/status/ffffffff-ffff-ffff-ffff-ffffffffffff",
    "correlationId": "ffffffff-ffff-ffff-ffff-ffffffffffff"
}
```

## How to Fix

1. Double-check the `correlationId` value -- use the exact value from the `202 Accepted` response when you submitted the command.
2. Confirm the command was successfully submitted (received a `202` response).
3. Verify you are querying the correct service instance and environment.

## Related

- [Error Reference Index](./index.md)
- [bad-request](./bad-request.md) -- if the correlation ID parameter is missing entirely
- [not-found](./not-found.md) -- for query endpoint resource lookups
