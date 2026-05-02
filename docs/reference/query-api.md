[← Back to Hexalith.EventStore](../../README.md)

# Query & Projection API Reference

Complete REST and SignalR reference for the Hexalith.EventStore read side, covering query execution, query preflight validation, projection invalidation, and optional real-time projection change notifications.

> **Prerequisites:** [Quickstart](../getting-started/quickstart.md) — you should have the sample running locally before using these endpoints.

## Base URL and Authentication

Find the `eventstore` service URL in the Aspire dashboard (typically `https://localhost:{port}` during local development). All HTTP paths in this document are relative to that base URL.

All HTTP endpoints on this page require a valid JWT Bearer token:

```bash
Authorization: Bearer {token}
```

Acquire a development token from Keycloak:

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

> **Note:** Query endpoints enforce the same 1 MB request-body limit as the command endpoints.

## POST /api/v1/queries

Execute a query against the current projection/read model.

### Request Body

| Field       | Type   | Required | Description                                                                                                 |
| ----------- | ------ | -------- | ----------------------------------------------------------------------------------------------------------- |
| tenant      | string | Yes      | Tenant identifier, for example `tenant-a`.                                                                  |
| domain      | string | Yes      | Domain name, for example `counter` or `inventory`.                                                          |
| aggregateId | string | Yes      | Aggregate identifier whose projection should be queried.                                                    |
| queryType   | string | Yes      | Query type name, for example `GetCounter` or another domain-specific projection query.                      |
| payload     | object | No       | Optional JSON payload for the query.                                                                        |
| entityId    | string | No       | Optional entity identifier for query handlers that target a nested entity or alternate projection identity. |

### Example

```bash
$ curl -X POST https://localhost:5001/api/v1/queries \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenant": "tenant-a",
    "domain": "counter",
    "aggregateId": "counter-1",
    "queryType": "GetCounter",
    "payload": {}
  }'
```

### Response — 200 OK

```json
{
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "payload": {
        "count": 1
    }
}
```

The exact shape of `payload` depends on the query handler and projection type.

### Conditional Requests with ETag

`POST /api/v1/queries` supports the `If-None-Match` header. When the supplied validator already matches the current projection version, the API returns `304 Not Modified` instead of recomputing the full payload.

```bash
$ curl -X POST https://localhost:5001/api/v1/queries \
  -H "Authorization: Bearer $TOKEN" \
  -H 'If-None-Match: "etag-value-from-previous-response"' \
  -H "Content-Type: application/json" \
  -d '{
    "tenant": "tenant-a",
    "domain": "counter",
    "aggregateId": "counter-1",
    "queryType": "GetCounter"
  }'
```

If the projection did not change, the response is:

```http
HTTP/1.1 304 Not Modified
ETag: "etag-value-from-previous-response"
```

If the projection changed, the API returns `200 OK` with a new response body and an updated `ETag` header.

### Error Responses

| Status                  | Condition                                           | Body                                              |
| ----------------------- | --------------------------------------------------- | ------------------------------------------------- |
| 304 Not Modified        | `If-None-Match` matches the current projection ETag | Empty body                                        |
| 400 Bad Request         | Validation failure or malformed request             | RFC 7807 ProblemDetails with `errors` dictionary  |
| 401 Unauthorized        | Missing or invalid JWT token                        | —                                                 |
| 403 Forbidden           | User lacks tenant or query permission               | RFC 7807 ProblemDetails                           |
| 404 Not Found           | Projection/query target not found                   | RFC 7807 ProblemDetails                           |
| 429 Too Many Requests   | Per-tenant rate limit exceeded                      | RFC 7807 ProblemDetails with `Retry-After` header |
| 503 Service Unavailable | Query dependencies unavailable                      | RFC 7807 ProblemDetails                           |

## POST /api/v1/queries/validate

Perform a preflight authorization check for a query without executing it. This is useful when a client wants to know whether a user can access a projection before rendering a screen or enabling a refresh action.

### Request Body

| Field       | Type   | Required | Description                                                             |
| ----------- | ------ | -------- | ----------------------------------------------------------------------- |
| tenant      | string | Yes      | Tenant identifier to authorize against.                                 |
| domain      | string | Yes      | Target domain name.                                                     |
| queryType   | string | Yes      | Query type name.                                                        |
| aggregateId | string | No       | Optional aggregate identifier for resource-scoped authorization checks. |

### Example

```bash
$ curl -X POST https://localhost:5001/api/v1/queries/validate \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenant": "tenant-a",
    "domain": "counter",
    "queryType": "GetCounter",
    "aggregateId": "counter-1"
  }'
```

### Response — 200 OK

```json
{
    "isAuthorized": true,
    "reason": null
}
```

If authorization fails, the endpoint still returns `200 OK`, but `isAuthorized` is `false` and `reason` explains why.

## POST /projections/changed

Notify the Command API that a projection changed so it can regenerate the current ETag and optionally broadcast a SignalR refresh signal.

This endpoint is designed for internal projection/update flows rather than browser clients. The current implementation consumes a `ProjectionChangedNotification` payload:

| Field          | Type   | Required | Description                                                                   |
| -------------- | ------ | -------- | ----------------------------------------------------------------------------- |
| projectionType | string | Yes      | Kebab-case projection name, for example `counter` or `order-list`.            |
| tenantId       | string | Yes      | Kebab-case tenant identifier.                                                 |
| entityId       | string | No       | Optional entity identifier reserved for finer-grained invalidation scenarios. |

### Example Payload

```json
{
    "projectionType": "counter",
    "tenantId": "tenant-a",
    "entityId": "counter-1"
}
```

The API uses this signal to refresh the projection ETag and, when SignalR is enabled, broadcast a `ProjectionChanged` message to subscribed clients in the corresponding `{projectionType}:{tenantId}` group.

## SignalR Hub: /hubs/projection-changes

The Command API can expose an optional SignalR hub at:

```text
/hubs/projection-changes
```

The hub does not send projection data. It sends a **signal only** — clients receive the projection type and tenant ID, then decide whether to requery the HTTP API.

### Hub Methods

| Direction       | Method                                        | Description                                         |
| --------------- | --------------------------------------------- | --------------------------------------------------- |
| Client → Server | `JoinGroup(projectionType, tenantId)`         | Subscribe the connection to a projection group.     |
| Client → Server | `LeaveGroup(projectionType, tenantId)`        | Unsubscribe the connection from a projection group. |
| Server → Client | `ProjectionChanged(projectionType, tenantId)` | Notify the client that the projection changed.      |

Group names use the format:

```text
{projectionType}:{tenantId}
```

Both values must be non-empty and must not contain `:`.

### Using Hexalith.EventStore.SignalR

The `Hexalith.EventStore.SignalR` package provides `EventStoreSignalRClient`, which wraps `Microsoft.AspNetCore.SignalR.Client` with automatic reconnect and automatic group rejoin.

```csharp
await using var client = new EventStoreSignalRClient(
    new EventStoreSignalRClientOptions
    {
        HubUrl = "https://localhost:5001/hubs/projection-changes",
        AccessTokenProvider = () => Task.FromResult<string?>(token),
    });

await client.SubscribeAsync("counter", "tenant-a", () =>
{
    // Re-run POST /api/v1/queries here.
});

await client.StartAsync();
```

A common pattern is:

1. Call `POST /api/v1/queries` and cache the `ETag`.
2. Join the SignalR group for the relevant projection type and tenant.
3. When `ProjectionChanged` fires, re-run the query with `If-None-Match`.
4. Refresh only when the API returns `200 OK`; keep the cached UI when it returns `304 Not Modified`.

The sample Blazor UI demonstrates three client reactions to this signal-only model: persistent notification, silent reload, and selective component refresh. See [Sample Blazor UI](../guides/sample-blazor-ui.md) for the UI behavior, command feedback boundary, and smoke-test evidence format.

## Projection Type Naming

Projection type names are base64url-encoded inside self-routing ETags (format: `{base64url(projectionType)}.{base64url-guid}`, where `base64url-guid` is the 16-byte GUID encoded with the URL-safe alphabet and trailing `=` padding stripped). Longer names produce proportionally longer ETag tokens in HTTP headers. Use short, descriptive names to keep ETags compact.

| Recommended | Avoid | Reason |
|-------------|-------|--------|
| `order-list` | `mycompany-sales-projections-order-list-projection` | Short name = compact ETag |
| `product-catalog` | `ecommerce-inventory-readmodels-product-catalog-readmodel` | No namespace needed |
| `user-profile` | `application-layer-identity-projections-user-profile-v2` | No version suffix needed |

**Guidelines:**

- Use short kebab-case names (e.g., `order-list`, `product-catalog`, `user-profile`) — the API validator enforces the pattern `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`
- Omit namespace prefixes — projection type names are already scoped by tenant and domain
- Omit suffixes like `-projection`, `-readmodel`, or version numbers — these add bytes without adding clarity
- The `projectionType` field in `POST /projections/changed` and SignalR group names uses the same short name

> **Reference:** FR64 — projection type names should be short for compact ETags.

## Next Steps

**Next:** [NuGet Packages Guide](nuget-packages.md) — choose the packages needed for your host, domain service, tests, and real-time clients

**Related:** [Command API Reference](command-api.md) | [Architecture Overview](../concepts/architecture-overview.md) | [Quickstart](../getting-started/quickstart.md)
