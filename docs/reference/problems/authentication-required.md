[<- Back to Error Reference](./index.md)

# Unauthorized

**HTTP Status:** 401 Unauthorized
**Problem Type:** `https://hexalith.io/problems/authentication-required`

## What Happened

Your request is missing a valid authentication token. The API requires a JWT Bearer token in the `Authorization` header for all requests.

## Common Causes

- The `Authorization` header is missing from the request
- The `Authorization` header is present but contains no token (e.g., `Bearer` with nothing after it)
- The token is malformed or was issued by an untrusted identity provider

## Example

### Request

```http
POST /api/v1/commands HTTP/1.1
Host: localhost:7275
Content-Type: application/json

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
HTTP/1.1 401 Unauthorized
Content-Type: application/problem+json
WWW-Authenticate: Bearer realm="hexalith-eventstore"

{
    "type": "https://hexalith.io/problems/authentication-required",
    "title": "Unauthorized",
    "status": 401,
    "detail": "Authentication is required to access this resource.",
    "instance": "/api/v1/commands"
}
```

## How to Fix

1. Obtain a short-lived JWT from the identity provider configured for this EventStore deployment. The direct API flow requires a caller-controlled external or development/test identity; the clean-clone AppHost does not expose its generated realm-user credentials through `HEXALITH_ADMIN_USERNAME` or `HEXALITH_ADMIN_PASSWORD`. For the executable local flow, use the [quickstart sample UI](../../getting-started/quickstart.md), which authenticates internally.

2. Add the token to your request:

    ```text
    Authorization: Bearer <access_token>
    ```

3. Retry the request.

See the [Quickstart Guide](../../getting-started/quickstart.md) for the authenticated local UI flow.

## Related

- [Error Reference Index](./index.md)
- [token-expired](./token-expired.md) -- if your token was valid but has expired
