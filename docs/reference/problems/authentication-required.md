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

1. Obtain a JWT token from the identity provider. For local development with Keycloak, discover the current Aspire endpoint rather than assuming a host port:

    ```bash
    KEYCLOAK_URL=$(aspire describe --format Json --non-interactive --nologo --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj | jq -r '.resources[] | select(.displayName=="security") | .urls[] | select(.name=="http") | .url' | head -n1)
    curl -s -X POST "${KEYCLOAK_URL}/realms/hexalith/protocol/openid-connect/token" \
      -d "client_id=hexalith-eventstore" \
      -d "grant_type=password" \
      -d "username=${HEXALITH_ADMIN_USERNAME}" \
      -d "password=${HEXALITH_ADMIN_PASSWORD}"
    ```

2. Add the token to your request:

    ```text
    Authorization: Bearer <access_token>
    ```

3. Retry the request.

See the [Quickstart Guide](../../getting-started/quickstart.md) for full token acquisition steps.

## Related

- [Error Reference Index](./index.md)
- [token-expired](./token-expired.md) -- if your token was valid but has expired
