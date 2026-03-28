[← Back to Hexalith.EventStore](../../README.md)

# Command API Reference

Complete REST API reference for the Hexalith EventStore command surface, covering command submission, status tracking, replay, and command preflight validation. Use this page to look up HTTP methods, request/response schemas, and copy-pasteable `curl` commands for building clients or testing integrations.

> **Prerequisites:** [Quickstart](../getting-started/quickstart.md) — you should have the sample running locally before using these endpoints.
> **Looking for the read side?** Query execution, query preflight validation, projection invalidation, and SignalR notifications are documented in the [Query & Projection API Reference](query-api.md).

## Base URL and Authentication

### Base URL

Find the `eventstore` service URL in the Aspire dashboard (typically `https://localhost:{port}` during local development). All endpoint paths in this document are relative to that base URL.

### Authentication

All endpoints require a valid JWT Bearer token. Include it in every request:

```bash
Authorization: Bearer {token}
```

### Token Acquisition

Acquire a token from the Keycloak development instance:

```bash
$ curl -s -X POST http://localhost:8180/realms/hexalith/protocol/openid-connect/token \
  -d "grant_type=password" \
  -d "client_id=hexalith-eventstore" \
  -d "username=admin-user" \
  -d "password=admin-pass"
```

> **Tip:** On Windows PowerShell 5.x, use:

```powershell
$ Invoke-RestMethod -Method Post -Uri "http://localhost:8180/realms/hexalith/protocol/openid-connect/token" -Body @{grant_type="password"; client_id="hexalith-eventstore"; username="admin-user"; password="admin-pass"} | Select-Object -ExpandProperty access_token
```

> **Note:** Keycloak development tokens expire after 5 minutes. If you receive `401 Unauthorized`, re-acquire a token using the command above.

### Content-Type

All POST requests require the `Content-Type: application/json` header.

### Swagger UI and OpenAPI Spec

Swagger UI is available at `/swagger`. The machine-readable OpenAPI spec is available at `/openapi/v1.json` — download this spec and use it to auto-generate typed HTTP clients (e.g., via NSwag, Kiota, or OpenAPI Generator).

### Correlation ID

The `X-Correlation-ID` header is optional on requests (the system generates one if missing) and always present on responses. A correlation ID is a unique identifier that links your request to all downstream processing stages — use it to trace your command through logs and the Aspire dashboard.

### Request Body Size Limit

All POST endpoints enforce a **1 MB (1,048,576 bytes)** maximum request body size. Requests exceeding this limit are rejected with `413 Payload Too Large` before any validation runs.

### JSON Property Casing

All request and response JSON properties use **camelCase** (e.g., `aggregateId`, `commandType`, `correlationId`). This is the ASP.NET Core `System.Text.Json` default. Do NOT use PascalCase (`AggregateId`) — it will fail silently as unbound properties, triggering a `400` validation error for "missing" fields.

### Idempotency

The API does not provide idempotency guarantees for command submission. Submitting the same command twice produces two independent processing results. Use the `X-Correlation-ID` header to track specific requests.

### API Versioning

The API is versioned via URL path (`/api/v1/`). Version lifecycle and deprecation policies are not yet defined.

### Payload Security

Command payloads are redacted in all server logs (the framework returns `[REDACTED]` for payload content). Place sensitive business data in the `payload` field, not in `extensions` metadata.

## POST /api/v1/commands

Submit a command for asynchronous processing.

### Request Body

| Field       | Type   | Required | Constraints                                                                                                                                                                                                                                     |
| ----------- | ------ | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| tenant      | string | Yes      | 1-128 chars, lowercase alphanumeric + hyphens, regex `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`. Identifies the tenant context.                                                                                                                          |
| domain      | string | Yes      | 1-128 chars, same regex as tenant. Identifies which aggregate type handles the command (e.g., `counter`, `inventory`). See [Identity Scheme](../concepts/identity-scheme.md).                                                                   |
| aggregateId | string | Yes      | 1-256 chars, alphanumeric + dots/hyphens/underscores, regex `^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$`. Identifies the specific entity instance (e.g., `counter-1`, `order-42`). See [Identity Scheme](../concepts/identity-scheme.md).       |
| commandType | string | Yes      | 1-256 chars, no `<`, `>`, `&`, `'`, `"` characters.                                                                                                                                                                                             |
| payload     | object | Yes      | JSON object matching the command's constructor parameters.                                                                                                                                                                                      |
| extensions  | object | No       | Max 50 entries, keys max 100 chars, values max 1000 chars, total max 64 KB. No `<`, `>`, `&`, `'`, `"` characters. Blocked injection regex: `(?i)(javascript\s*:\|on\w+\s*=\|<\s*script)` (alternation separators escaped for table rendering). |

### Examples

**IncrementCounter:**

```bash
$ curl -X POST https://localhost:5001/api/v1/commands \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenant": "tenant-a",
    "domain": "counter",
    "aggregateId": "counter-1",
    "commandType": "IncrementCounter",
    "payload": {}
  }'
```

**DecrementCounter:**

```bash
$ curl -X POST https://localhost:5001/api/v1/commands \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenant": "tenant-a",
    "domain": "counter",
    "aggregateId": "counter-1",
    "commandType": "DecrementCounter",
    "payload": {}
  }'
```

**ResetCounter:**

```bash
$ curl -X POST https://localhost:5001/api/v1/commands \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenant": "tenant-a",
    "domain": "counter",
    "aggregateId": "counter-1",
    "commandType": "ResetCounter",
    "payload": {}
  }'
```

### Response — 202 Accepted

`202 Accepted` means the command was received and queued for asynchronous processing. It does NOT mean the command succeeded — poll the status endpoint to check the result.

Example response (using `curl -i`):

```http
HTTP/1.1 202 Accepted
Location: https://localhost:5001/api/v1/commands/status/a1b2c3d4-e5f6-7890-abcd-ef1234567890
Retry-After: 1
X-Correlation-ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
Content-Type: application/json

{"correlationId":"a1b2c3d4-e5f6-7890-abcd-ef1234567890"}
```

### Error Responses

> **See also:** [Error Reference](./problems/index.md) for detailed documentation on each error type, including example requests/responses and resolution steps.

| Status                     | Condition                                                               | Body                                              |
| -------------------------- | ----------------------------------------------------------------------- | ------------------------------------------------- |
| 400 Bad Request            | Validation failure (missing fields, regex mismatch, injection patterns) | RFC 7807 ProblemDetails with `errors` dictionary  |
| 401 Unauthorized           | Missing or invalid JWT token                                            | —                                                 |
| 403 Forbidden              | User lacks `eventstore:tenant` claim for requested tenant               | RFC 7807 ProblemDetails                           |
| 409 Conflict               | Optimistic concurrency violation (concurrent writes to same aggregate)  | RFC 7807 ProblemDetails                           |
| 413 Payload Too Large      | Request body exceeds 1 MB limit                                         | —                                                 |
| 415 Unsupported Media Type | Missing or incorrect `Content-Type` header (must be `application/json`) | —                                                 |
| 429 Too Many Requests      | Per-tenant rate limit exceeded                                          | RFC 7807 ProblemDetails with `Retry-After` header |
| 500 Internal Server Error  | Unhandled server exception (rare)                                       | RFC 7807 ProblemDetails                           |

**Example 400 — validation error:**

```json
{
    "type": "https://tools.ietf.org/html/rfc9457#section-3",
    "title": "Validation Failed",
    "status": 400,
    "detail": "One or more validation errors occurred.",
    "instance": "/api/v1/commands",
    "errors": {
        "Tenant": ["'Tenant' must not be empty."]
    },
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

### Extensions

The `extensions` field carries optional metadata with the command. Extension values are sanitized for injection patterns at the API gateway layer. Values matching the regex `(?i)(javascript\s*:|on\w+\s*=|<\s*script)` are rejected with a `400` error — if you receive an unexpected validation error on extensions, check for these patterns in your values.

## POST /api/v1/commands/validate

Perform a preflight authorization check for a command without enqueuing it for execution. This endpoint is useful for UI affordances such as disabling a command button before the user submits the full request.

### Request Body

| Field       | Type   | Required | Description                                                             |
| ----------- | ------ | -------- | ----------------------------------------------------------------------- |
| tenant      | string | Yes      | Tenant identifier to authorize against.                                 |
| domain      | string | Yes      | Target domain name, for example `counter` or `inventory`.               |
| commandType | string | Yes      | Command type name, for example `IncrementCounter`.                      |
| aggregateId | string | No       | Optional aggregate identifier for resource-scoped authorization checks. |

### Example

```bash
$ curl -X POST https://localhost:5001/api/v1/commands/validate \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenant": "tenant-a",
    "domain": "counter",
    "commandType": "IncrementCounter",
    "aggregateId": "counter-1"
  }'
```

### Response — 200 OK

The endpoint always returns `200 OK` for an authenticated request, with the authorization result encoded in the response body:

```json
{
    "isAuthorized": true,
    "reason": null
}
```

If authorization fails, `isAuthorized` is `false` and `reason` explains why:

```json
{
    "isAuthorized": false,
    "reason": "RBAC check failed."
}
```

### Error Responses

| Status                  | Condition                               | Body                                              |
| ----------------------- | --------------------------------------- | ------------------------------------------------- |
| 400 Bad Request         | Validation failure for the request body | RFC 7807 ProblemDetails with `errors` dictionary  |
| 401 Unauthorized        | Missing or invalid JWT token            | —                                                 |
| 429 Too Many Requests   | Per-tenant rate limit exceeded          | RFC 7807 ProblemDetails with `Retry-After` header |
| 503 Service Unavailable | Authorization dependencies unavailable  | RFC 7807 ProblemDetails                           |

## GET /api/v1/commands/status/{correlationId}

Query the processing status of a previously submitted command.

### Path Parameter

`correlationId` — GUID string returned in the submit response.

### Example

```bash
$ curl https://localhost:5001/api/v1/commands/status/a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  -H "Authorization: Bearer $TOKEN"
```

### Response — 200 OK

| Field              | Type     | Description                                                                                                |
| ------------------ | -------- | ---------------------------------------------------------------------------------------------------------- |
| correlationId      | string   | The command's correlation ID.                                                                              |
| status             | string   | One of: Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut. |
| statusCode         | integer  | Numeric enum value (0-7).                                                                                  |
| timestamp          | string   | ISO 8601 timestamp of last status update.                                                                  |
| aggregateId        | string?  | Populated when processing begins.                                                                          |
| eventCount         | integer? | Number of events produced (Completed status only).                                                         |
| rejectionEventType | string?  | Rejection event type name (Rejected status only).                                                          |
| failureReason      | string?  | Error description (PublishFailed status only).                                                             |
| timeoutDuration    | string?  | .NET TimeSpan format, e.g., `"00:00:30"` (TimedOut status only).                                           |

**Example — completed command:**

```json
{
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "status": "Completed",
    "statusCode": 4,
    "timestamp": "2026-03-01T10:30:00.000Z",
    "aggregateId": "counter-1",
    "eventCount": 1,
    "rejectionEventType": null,
    "failureReason": null,
    "timeoutDuration": null
}
```

**Example — rejected command:**

```json
{
    "correlationId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
    "status": "Rejected",
    "statusCode": 5,
    "timestamp": "2026-03-01T10:31:00.000Z",
    "aggregateId": "counter-1",
    "eventCount": null,
    "rejectionEventType": "CounterAlreadyAtZero",
    "failureReason": null,
    "timeoutDuration": null
}
```

### Status Lifecycle

Poll `/api/v1/commands/status/{correlationId}` at the interval indicated by the `Retry-After` response header (typically 1 second) until a terminal status is returned.

1. **Received** (0) — Command accepted by API, queued for processing
2. **Processing** (1) — Actor activated, domain service invocation started
3. **EventsStored** (2) — Events persisted to state store
4. **EventsPublished** (3) — Events published to pub/sub topic
5. **Completed** (4) — Terminal success — events stored and published
6. **Rejected** (5) — Terminal — domain rejected the command (business rule violation)
7. **PublishFailed** (6) — Terminal — events stored but pub/sub delivery failed
8. **TimedOut** (7) — Terminal — processing exceeded configured timeout

Terminal states: Completed, Rejected, PublishFailed, TimedOut. See [Command Lifecycle](../concepts/command-lifecycle.md) for the full processing pipeline explanation.

### Error Responses

| Status                | Condition                                  | Body                                              |
| --------------------- | ------------------------------------------ | ------------------------------------------------- |
| 400 Bad Request       | Invalid GUID format for correlationId      | RFC 7807 ProblemDetails                           |
| 401 Unauthorized      | Missing or invalid JWT token               | —                                                 |
| 403 Forbidden         | No `eventstore:tenant` claims found in JWT | RFC 7807 ProblemDetails                           |
| 404 Not Found         | Command not found in authorized tenants    | RFC 7807 ProblemDetails                           |
| 429 Too Many Requests | Per-tenant rate limit exceeded             | RFC 7807 ProblemDetails with `Retry-After` header |

> **Note:** A `404` response means the command was not found among your authorized tenants. This is intentional — the API does not distinguish between "command does not exist" and "you are not authorized for that tenant" to prevent tenant enumeration.

## POST /api/v1/commands/replay/{correlationId}

Replay a previously failed command. Only commands in terminal failure states (Rejected, PublishFailed, TimedOut) can be replayed.

### Path Parameter

`correlationId` — GUID string of a previously failed command.

### Example

```bash
$ curl -X POST https://localhost:5001/api/v1/commands/replay/a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json"
```

### Response — 202 Accepted

`202 Accepted` means the replayed command was received and queued for asynchronous processing. It does NOT mean the replay succeeded — poll the status endpoint using the new correlation ID to check the result.

The response includes a `Location` header pointing to the status endpoint for the new correlation ID, and a `Retry-After: 1` header.

| Field          | Type    | Description                                                            |
| -------------- | ------- | ---------------------------------------------------------------------- |
| correlationId  | string  | New correlation ID for the replayed command.                           |
| isReplay       | boolean | Always `true`.                                                         |
| previousStatus | string? | Status before replay (Rejected, PublishFailed, or TimedOut). Nullable. |

**Example response:**

```json
{
    "correlationId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
    "isReplay": true,
    "previousStatus": "Rejected"
}
```

### Error Responses

| Status                     | Condition                                                                         | Body                                              |
| -------------------------- | --------------------------------------------------------------------------------- | ------------------------------------------------- |
| 400 Bad Request            | Invalid GUID format for correlationId                                             | RFC 7807 ProblemDetails                           |
| 401 Unauthorized           | Missing or invalid JWT token                                                      | —                                                 |
| 403 Forbidden              | No `eventstore:tenant` claims or tenant mismatch                                  | RFC 7807 ProblemDetails                           |
| 404 Not Found              | Command not found or archive expired                                              | RFC 7807 ProblemDetails                           |
| 409 Conflict               | Command not in a replayable state (Completed, still in-flight, or status expired) | RFC 7807 ProblemDetails                           |
| 413 Payload Too Large      | Request body exceeds 1 MB limit                                                   | —                                                 |
| 415 Unsupported Media Type | Missing or incorrect `Content-Type` header (must be `application/json`)           | —                                                 |
| 429 Too Many Requests      | Per-tenant rate limit exceeded                                                    | RFC 7807 ProblemDetails with `Retry-After` header |
| 500 Internal Server Error  | Unhandled server exception (rare)                                                 | RFC 7807 ProblemDetails                           |

> **Note:** A `404` response uses the same intentional ambiguity as the status endpoint — the API does not distinguish between "command does not exist" and "you are not authorized for that tenant" to prevent tenant enumeration.

## Command Status Lifecycle

All commands progress through 8 possible states:

| Status          | Code | Description                                           | Terminal                  |
| --------------- | ---- | ----------------------------------------------------- | ------------------------- |
| Received        | 0    | Command accepted by API, queued for processing        | No                        |
| Processing      | 1    | Actor activated, domain service invocation started    | No                        |
| EventsStored    | 2    | Events persisted to state store                       | No                        |
| EventsPublished | 3    | Events published to pub/sub topic                     | No                        |
| Completed       | 4    | Events stored and published successfully              | Yes (success)             |
| Rejected        | 5    | Domain rejected the command (business rule violation) | Yes (failure, replayable) |
| PublishFailed   | 6    | Events stored but pub/sub delivery failed             | Yes (failure, replayable) |
| TimedOut        | 7    | Processing exceeded configured timeout                | Yes (failure, replayable) |

**Terminal states:** Completed, Rejected, PublishFailed, TimedOut.

**Replayable states:** Rejected, PublishFailed, TimedOut (all terminal failures).

Poll the status endpoint at the interval indicated by the `Retry-After` response header (typically 1 second) until a terminal status is returned: Completed, Rejected, PublishFailed, or TimedOut.

For the full processing pipeline explanation, see [Command Lifecycle](../concepts/command-lifecycle.md).

## Rate Limiting

All endpoints are subject to per-tenant sliding window rate limiting.

**Default configuration:** 100 requests per 60-second window per tenant.

When the rate limit is exceeded, the API returns `429 Too Many Requests` with an RFC 7807 ProblemDetails body and a `Retry-After` header indicating how long to wait before retrying.

Health and readiness endpoints (`/health`, `/alive`, `/ready`) are excluded from rate limiting. These endpoints are not part of the Command API surface — they exist for infrastructure health checks only.

## Error Response Format

All error responses use the [RFC 7807 ProblemDetails](https://tools.ietf.org/html/rfc9457) format:

```json
{
    "type": "https://tools.ietf.org/html/rfc9457#section-3",
    "title": "...",
    "status": 400,
    "detail": "...",
    "instance": "/api/v1/commands",
    "correlationId": "...",
    "tenantId": "..."
}
```

**Validation errors** (400) include an `errors` field — a dictionary mapping field names to arrays of error messages:

```json
{
    "type": "https://tools.ietf.org/html/rfc9457#section-3",
    "title": "Validation Failed",
    "status": 400,
    "detail": "One or more validation errors occurred.",
    "instance": "/api/v1/commands",
    "errors": {
        "Tenant": ["'Tenant' must not be empty."],
        "Domain": ["'Domain' does not match the required pattern."]
    },
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

> **Note:** `413 Payload Too Large` and `415 Unsupported Media Type` are returned by the ASP.NET Core framework before the application runs — these are raw HTTP responses, not RFC 7807 ProblemDetails.

## Complete Flow Example

A quick-reference recipe showing end-to-end command submission and status polling. See the [Quickstart](../getting-started/quickstart.md) for the full guided walkthrough.

**Step 1 — Acquire token:**

```bash
$ TOKEN=$(curl -s -X POST http://localhost:8180/realms/hexalith/protocol/openid-connect/token \
  -d "grant_type=password" \
  -d "client_id=hexalith-eventstore" \
  -d "username=admin-user" \
  -d "password=admin-pass" | jq -r '.access_token')
```

**Step 2 — Submit command:**

```bash
$ curl -X POST https://localhost:5001/api/v1/commands \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"tenant":"tenant-a","domain":"counter","aggregateId":"counter-1","commandType":"IncrementCounter","payload":{}}'
```

```json
{ "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890" }
```

**Step 3 — Poll status:**

```bash
$ curl https://localhost:5001/api/v1/commands/status/a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  -H "Authorization: Bearer $TOKEN"
```

**Step 4 — Observe terminal status:**

```json
{
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "status": "Completed",
    "statusCode": 4,
    "timestamp": "2026-03-01T10:30:00.000Z",
    "aggregateId": "counter-1",
    "eventCount": 1
}
```

## Next Steps

**Next:** [Query & Projection API Reference](query-api.md) — query execution, ETag validation, and real-time projection refresh

**Related:** [NuGet Packages Guide](nuget-packages.md), [Command Lifecycle](../concepts/command-lifecycle.md), [Event Envelope](../concepts/event-envelope.md), [Architecture Overview](../concepts/architecture-overview.md)
