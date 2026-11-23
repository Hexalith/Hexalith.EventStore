[<- Back to Error Reference](./index.md)

# Command Status Not Found

**HTTP Status:** 404 Not Found
**Problem Type:** `https://hexalith.io/problems/command-status-not-found`

## What Happened

The gateway could not find a status record for the requested command in the selected scope. Use the command’s `messageId` to look up its status. The `correlationId` in the problem response identifies this HTTP request for troubleshooting.

## Common Causes

- The message ID is incorrect or contains a typo
- The command was never submitted (the original submission may have failed before being recorded)
- The command belongs to a different tenant, platform scope, or environment
- Status records have been purged (if retention policies are configured)

## Example

### Request

```http
GET /api/v1/commands/status/01ARZ3NDEKTSV4RRFFQ69G5FAV HTTP/1.1
Host: localhost:7275
Authorization: Bearer <your-jwt-token>
X-Hexalith-Tenant: tenant-a
X-Correlation-ID: status-check-42
```

### Response

```http
HTTP/1.1 404 Not Found
Content-Type: application/problem+json
X-Correlation-ID: status-check-42

{
    "type": "https://hexalith.io/problems/command-status-not-found",
    "title": "Not Found",
    "status": 404,
    "detail": "No command status found for message or correlation identifier '01ARZ3NDEKTSV4RRFFQ69G5FAV'.",
    "instance": "/api/v1/commands/status/01ARZ3NDEKTSV4RRFFQ69G5FAV",
    "correlationId": "status-check-42"
}
```

## How to Fix

1. Confirm the submission returned `202 Accepted`. Follow the `Location` header when present, or use the accepted command’s exact `messageId` in the status URL.
2. Send the lookup to the original environment with the authorized tenant or platform scope used for submission.
3. If the command was accepted and the ID and scope match, check the configured status retention period.

## Related

- [Error Reference Index](./index.md)
- [bad-request](./bad-request.md) -- for a malformed status identifier
- [not-found](./not-found.md) -- for query endpoint resource lookups
