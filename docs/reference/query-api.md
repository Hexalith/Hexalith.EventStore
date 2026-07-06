[← Back to Hexalith.EventStore](../../README.md)

# Query & Projection API Reference

Complete REST and SignalR reference for the Hexalith.EventStore read side, covering query execution, query preflight validation, projection invalidation, and optional projection change invalidation signals.

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

## .NET Package Boundary

Downstream .NET callers should build query gateway requests with `Hexalith.EventStore.Contracts.Queries.SubmitQueryRequest` and read envelopes as `Hexalith.EventStore.Contracts.Queries.SubmitQueryResponse`. Use `Hexalith.EventStore.Client.Gateway.IEventStoreGatewayClient` when you want the package to post the request, normalize ETags, map `304 Not Modified`, deserialize typed payloads, and expose ProblemDetails as `EventStoreGatewayException`.

Pure or non-DAPR projection query adapters can implement `Hexalith.EventStore.Contracts.Queries.IProjectionActor` directly and exchange `QueryEnvelope` and `QueryResult` from Contracts. DAPR actor hosts add DAPR packages in their hosting project and expose a runtime-owned actor interface derived from `Dapr.Actors.IActor` that mirrors the same `QueryAsync(QueryEnvelope)` method. Do not reference `Hexalith.EventStore` or `Hexalith.EventStore.Server` for gateway DTOs, query adapter DTOs, or the public projection query method contract. Public gateway and projection adapter contracts live in Contracts, the HTTP convenience client lives in Client, and deterministic public adapter doubles live in Testing. See [NuGet Packages Guide](nuget-packages.md#serving-projection-queries).

## POST /api/v1/queries

Execute a query against the current projection/read model.

### Request Body

| Field               | Type   | Required | Description                                                                                                                                       |
| ------------------- | ------ | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| tenant              | string | Yes      | Tenant identifier, for example `tenant-a`.                                                                                                        |
| domain              | string | Yes      | Domain name, for example `counter` or `inventory`.                                                                                                |
| aggregateId         | string | Yes      | Aggregate identifier whose projection should be queried.                                                                                          |
| queryType           | string | Yes      | Query type name, for example `get-party`, `list-parties`, or `search-parties`.                                                                    |
| projectionType      | string | No       | Projection/read-model selector used for actor ID routing and ETag/cache metadata. If omitted, routing uses `queryType`.                           |
| payload             | object | No       | Optional JSON payload for the query. Non-empty payloads route through the payload-checksum mode when `entityId` is absent.                         |
| entityId            | string | No       | Optional entity identifier for query handlers that target a nested entity or alternate projection identity.                                        |
| projectionActorType | string | No       | Optional DAPR actor type selector. Defaults to `ProjectionActor`. This is a routing selector only, not an authorization or tenant-selection field. Must not contain colons (`:`) or dangerous characters. Maximum 64 characters. |
| paging              | object | No       | Public paging policy. `pageSize` defaults to `50`, cannot exceed `200`, and `offset` must be zero or greater. Cursor paging is reserved and currently rejected. |
| search              | string | No       | Public search policy. Blank or whitespace search is treated as omitted. Non-blank search is reserved and currently rejected with a stable reason code. |
| filters             | array  | No       | Public filter policy. Reserved for future query engines and currently rejected without echoing filter values.                                      |
| orderBy             | array  | No       | Public ordering policy. Reserved for future query engines and currently rejected.                                                                  |
| freshness           | object | No       | Public freshness policy. Requests that require freshness fail closed as `query_projection_stale` when authoritative freshness evidence is unavailable or stale. |

### Query Policy

`POST /api/v1/queries` accepts additive query policy fields so clients can adopt a stable public contract before every projection supports every behavior.

| Policy     | Current behavior |
| ---------- | ---------------- |
| Paging     | `pageSize` defaults to `50`; maximum is `200`; `offset` must be `>= 0`; `cursor` is reserved and rejected. |
| Search     | Blank search is normalized as omitted. Non-blank search is rejected as `query_unsupported_search`. |
| Filters    | Any filter expression is rejected as `query_unsupported_filter`. Filter values are not echoed in validation errors or ProblemDetails. |
| Ordering   | Any order expression is rejected as `query_unsupported_order`. Legacy projection ordering remains projection-defined. |
| Freshness  | `requireFresh = true` or `maxStaleness` requires authoritative producer freshness metadata. Unknown or stale freshness fails closed as `query_projection_stale`. |
| Unknown top-level fields | Rejected as `query_malformed_request` through `JsonExtensionData` capture. |

### Projection Evidence Metadata

Query responses carry additive projection evidence in `metadata` when a domain handler or projection actor produces it. The platform preserves freshness, projection version, paging evidence, degraded state, and warning codes from the producer through the domain/projection result, router, gateway response, and .NET client result.

The gateway still owns HTTP validator behavior. A strong response `ETag` header wins for `metadata.eTag` when present, `metadata.isNotModified` reflects the HTTP outcome, and `metadata.servedAt` is filled only when the producer did not supply it. Missing freshness remains unknown (`isStale = null`) and is never treated as current. Request paging is only policy input; the response includes `metadata.paging` only when the producer supplies authoritative paging evidence.

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
    },
    "metadata": {
        "eTag": "etag-value-from-response-header",
        "isNotModified": false,
        "isStale": null,
        "servedAt": "2026-05-14T09:20:00Z"
    }
}
```

The exact shape of `payload` depends on the query handler and projection type. `metadata` is additive. The gateway includes normalized cache metadata when available. Producer-supplied metadata may also include `isStale`, `projectionVersion`, `isDegraded`, `warningCodes`, and `paging`; the gateway does not infer those fields from the request or from ETag values.

If the query pipeline returns a `200 OK` envelope with `"success": false`, the .NET gateway client treats it as a semantic gateway failure and throws `EventStoreGatewayException` with status code `200`, title `Query semantic failure`, the envelope `correlationId`, and the envelope `errorMessage` as detail. Callers should not deserialize `payload` from a failed semantic envelope.

### Conditional Requests with ETag

`POST /api/v1/queries` supports the `If-None-Match` header. When the supplied validator already matches the current projection version, the API returns `304 Not Modified` instead of recomputing the full payload.

HTTP examples show quoted ETags because HTTP headers require quoted entity tags. The .NET gateway client exposes response ETags as normalized unquoted strong tokens. When calling `SubmitQueryAsync`, pass either the normalized token (`etag-value-from-previous-response`) or the quoted header form (`"etag-value-from-previous-response"`); the client sends the quoted HTTP header. Empty values omit the header. Weak or malformed ETags are unsupported and rejected before the request is sent.

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

If the projection changed, the API returns `200 OK` with a new response body and an updated `ETag` header. Conditional pre-checks are skipped for requests that carry explicit query policy inputs or a non-empty `payload`, so a cached validator for one query shape cannot produce a false `304` for a different filter/search/order/page/freshness or payload identity. ETag lookup failures fail open and the query still executes. If producer freshness is unknown and the request explicitly requires freshness, the response fails closed with `query_projection_stale`.

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

Stable gateway ProblemDetails extension names are `correlationId`, `tenantId`, `errors`, `reason`, `reasonCode`, and `retryAfter`. The .NET gateway client exposes these through named properties on `EventStoreGatewayException`. Any non-standard extensions returned by the gateway (for example `traceCode` or custom diagnostic fields) are preserved verbatim in `EventStoreGatewayException.Extensions` as `IReadOnlyDictionary<string, JsonElement>` and are excluded from the named properties to avoid duplication.

Non-auth query ProblemDetails use these stable `reasonCode` values:

| Reason code                           | Typical status | Meaning |
| ------------------------------------- | -------------- | ------- |
| `query_malformed_request`             | 400            | Unknown or malformed query policy input. |
| `query_invalid_page`                  | 400            | Invalid page size, offset, or cursor/offset combination. |
| `query_unsupported_filter`            | 400            | Public filter policy was supplied before filter support is enabled. |
| `query_unsupported_search`            | 400            | Non-blank public search policy was supplied before search support is enabled. |
| `query_unsupported_order`             | 400            | Public ordering policy was supplied before order support is enabled. |
| `query_projection_missing`            | 404            | The requested projection/query target was not found. |
| `query_projection_stale`              | 400 or 503     | The request required freshness the gateway cannot currently satisfy. |
| `query_degraded_search`               | 200 metadata   | Search was served through a degraded path. |
| `query_malformed_projection_response` | 500            | A projection returned a malformed response. |
| `query_projection_timeout`            | 503            | A projection did not respond before the timeout. |
| `query_not_implemented`               | 501            | The query type or projection behavior is not implemented. |
| `query_internal_error`                | 500            | The query failed with an internal server error. |

## Projection Query Actor Contract

Domain services that serve EventStore queries expose the implementation-neutral `QueryAsync(QueryEnvelope)` method from `Hexalith.EventStore.Contracts.Queries.IProjectionActor`. Pure adapters and test fakes can implement the neutral Contracts interface directly. DAPR actor hosts should mirror that method on a local actor interface:

```csharp
public interface IPartyProjectionActor : Dapr.Actors.IActor
{
    Task<QueryResult> QueryAsync(QueryEnvelope envelope);
}

public sealed class PartyProjectionActor : Actor, IPartyProjectionActor
{
    public Task<QueryResult> QueryAsync(QueryEnvelope envelope)
    {
        // Read envelope.QueryType, envelope.EntityId, and envelope.Payload.
        // Return QueryResult.FromPayload(json, projectionType: "party").
    }
}
```

`PartyProjectionActor` is a DAPR actor because the hosting project chooses to inherit from `Actor` and expose a runtime-owned interface derived from `Dapr.Actors.IActor`. The Contracts interface itself does not inherit `Dapr.Actors.IActor` and does not require a DAPR package reference. Non-DAPR adapters and test fakes can implement the neutral `IProjectionActor` interface directly.

`QueryEnvelope` is the projection query wire envelope. Its fields are `TenantId`, `Domain`, `AggregateId`, `QueryType`, UTF-8 JSON `Payload` bytes, `CorrelationId`, `UserId`, and optional `EntityId`. `ToString()` redacts payload bytes and should be used instead of logging raw query payloads.

`QueryResult` is the actor response. Successful results carry UTF-8 JSON `PayloadBytes`, optional `ProjectionType`, and optional `QueryResponseMetadata`; failures set `Success = false` and a coarse adapter-edge `ErrorMessage`. Use the public `QueryAdapterFailureReason` constants for stable adapter-edge categories:

| Category                         | Meaning                                                                  |
| -------------------------------- | ------------------------------------------------------------------------ |
| `missing-payload`                | Actor returned success without payload bytes.                            |
| `invalid-envelope`               | Query envelope was malformed or incompatible.                            |
| `actor-response-mismatch`        | Actor returned null or an incompatible response shape.                   |
| `unsupported-query-type`         | Actor does not support the query type.                                   |
| `serialization-failure`          | Payload bytes could not be serialized/deserialized as the public shape.  |
| `actor-exception`                | Actor invocation failed before a valid adapter response was returned.    |
| `unknown-query-type`             | Query type is unknown to the projection adapter.                         |
| `actor-not-found-infrastructure` | DAPR actor runtime reported missing actor registration or actor address. |

Story 22.4 owns non-auth query taxonomy, paging/filter/freshness policy, and detailed query behavior. Story 22.3 owns tenant/RBAC enforcement before query actor invocation, ETag comparison, cache lookup, and any not-found/missing-projection response.

### Actor Type and ID Routing

EventStore uses three deterministic actor ID modes. Routing segments must not contain `:`. The 11-character checksum is a deterministic routing key, not a uniqueness, authorization, or security proof.

| Query shape       | Request fields                                             | Actor ID format                    | Example                                  |
| ----------------- | ---------------------------------------------------------- | ---------------------------------- | ---------------------------------------- |
| Entity `GetParty` | `projectionType = "party"`, `entityId = "party-42"`        | `{projectionType}:{tenant}:{id}`   | `party:tenant-a:party-42`                |
| List parties      | `projectionType = "party-list"`, empty payload, no entity  | `{projectionType}:{tenant}`        | `party-list:tenant-a`                    |
| Search parties    | `projectionType = "party-search"`, non-empty JSON payload  | `{projectionType}:{tenant}:{hash}` | `party-search:tenant-a:A5BYxvLAy0k`      |

If `projectionType` is omitted, EventStore uses `queryType` as the first actor ID segment for compatibility. If `projectionActorType` is omitted, EventStore uses DAPR actor type `ProjectionActor`; otherwise it uses the supplied actor type selector. `projectionActorType` does not bypass authentication, authorization, tenant validation, or future Story 22.3 policy. The field must not contain colons (`:`, reserved as the actor ID segment separator) or dangerous characters; it is limited to 64 characters. These constraints are enforced at the gateway validator before the value reaches the actor proxy factory.

The `/project` projection update/rebuild contract is separate from `POST /api/v1/queries` query serving. Projection update endpoints change read-model state and ETags; query actors serve current read-model data.

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
    "reason": null,
    "reasonCode": null
}
```

If authorization fails, the endpoint still returns `200 OK`, but `isAuthorized` is `false`; `reason` is safe human-readable detail and `reasonCode` is the stable machine-readable denial category. Runtime query submission returns RFC 7807 401/403/503 responses for authentication, authorization, and validator-infrastructure failures.

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

SignalR notifications are invalidation signals only. They do not contain projection data, ETags, command status, or a replay of missed signals. The Query API remains the authoritative source for current projection state.

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

Use `EventStoreSignalRClientOptions.AccessTokenProvider` when the hub requires bearer authentication. Use `RetryPolicy` to supply the SignalR reconnect policy, and `ConfigureHttpConnection` to customize the underlying HTTP connection options. These settings affect the hub connection; they do not change the Query API responsibility for current projection data.

`ConfigureHttpConnection` is invoked after `AccessTokenProvider` is wired, so a delegate that sets `connectionOptions.AccessTokenProvider` will override the dedicated option. Pick one place to supply the bearer token.

### Connect and Reconnect Responsibilities

A common pattern is:

1. Call `POST /api/v1/queries` at the lifecycle moment your component or service exposes first (typically component initialization or service startup) to establish baseline state, then cache the `ETag`.
2. Join the SignalR group for the relevant projection type and tenant.
3. When `ProjectionChanged` fires, re-run the query with `If-None-Match`.
4. Refresh only when the API returns `200 OK`; keep the cached UI when it returns `304 Not Modified`.

The order of steps 1 and 2 is not significant before `StartAsync()` is called: `EventStoreSignalRClient` queues `JoinGroup` calls until the connection starts, and no `ProjectionChanged` callback can fire before that point. The numbered order above is a recommended reading order, not a required execution order.

Automatic reconnect and group rejoin restore future notification delivery only. They do not replay notifications missed while the connection was down. After a known reconnect, browser resume, page restore, or other known downtime, clients that display projection data should re-query the Query API for the projections they show.

The current `EventStoreSignalRClient` rejoins tracked groups internally after reconnect, but it does not expose a public reconnected event or callback for application refresh logic. Applications that need reconnect-aware refresh should use lifecycle signals they can observe, such as browser online/resume events, page restore events, explicit user refresh, or host-specific connection monitoring, then re-query the HTTP Query API.

Do:

- Query on initial load before relying on future `ProjectionChanged` callbacks.
- Re-query after known reconnect, browser resume, page restore, or known downtime.
- Treat SignalR notifications as refresh hints and use `If-None-Match` to avoid replacing unchanged UI state.

Do not:

- Treat `ProjectionChanged` as projection data.
- Treat SignalR as a command-status, ETag, or missed-notification replay channel.
- Infer exact reconnect timing from the EventStore contract; reconnect timing is governed by the configured SignalR retry policy.

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
