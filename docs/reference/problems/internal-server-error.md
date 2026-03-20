[<- Back to Error Reference](./index.md)

# Internal Server Error

**HTTP Status:** 500 Internal Server Error
**Problem Type:** `https://hexalith.io/problems/internal-server-error`

## What Happened

An unexpected error occurred while processing your request. The server encountered a condition it was not designed to handle. No details about the internal failure are exposed for security reasons.

## Common Causes

- An unhandled exception in the server-side processing pipeline
- A transient infrastructure failure that was not caught by a specific handler
- A bug in a domain service handler

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
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json

{
    "type": "https://hexalith.io/problems/internal-server-error",
    "title": "Internal Server Error",
    "status": 500,
    "detail": "An unexpected error occurred while processing your request.",
    "instance": "/api/v1/commands",
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "tenantId": "tenant-a"
}
```

## How to Fix

**For API consumers:**

1. Save the `correlationId` from the response -- it is the key to diagnosing the issue.
2. Retry the request once. If the error was transient, it may succeed on retry.
3. If the error persists, report it to the service operator and provide the `correlationId`.

**For service operators:**

1. Search structured logs by the `correlationId` to find the full stack trace and error details (these are logged server-side but never exposed to clients).
2. Check the Aspire dashboard or application logs for the time window of the request.
3. Investigate and fix the root cause in the relevant handler or domain service.

## Related

- [Error Reference Index](./index.md)
- [service-unavailable](./service-unavailable.md) -- for known infrastructure unavailability rather than unexpected failures
