[<- Back to Error Reference](./index.md)

# Unauthorized (Token Expired)

**HTTP Status:** 401 Unauthorized
**Problem Type:** `https://hexalith.io/problems/token-expired`

## What Happened

Your authentication token has expired. The JWT token included in the `Authorization` header was valid when issued but has passed its expiration time.

## Common Causes

- The token's `exp` (expiration) claim is in the past
- Long-running scripts or sessions that do not refresh tokens
- Clock skew between the client and the identity provider

## Example

### Request

```http
POST /api/v1/commands HTTP/1.1
Host: localhost:7275
Content-Type: application/json
Authorization: Bearer eyJhbGci...expired

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
WWW-Authenticate: Bearer realm="hexalith-eventstore", error="invalid_token", error_description="The token has expired"

{
    "type": "https://hexalith.io/problems/token-expired",
    "title": "Unauthorized",
    "status": 401,
    "detail": "The provided authentication token has expired.",
    "instance": "/api/v1/commands"
}
```

## How to Fix

1. Request a new token from the identity provider (see [Quickstart Guide](../../getting-started/quickstart.md)).
2. Replace the expired token in your `Authorization` header.
3. Retry the request.
4. For automated clients, implement token refresh logic before the `exp` claim is reached.

## Related

- [Error Reference Index](./index.md)
- [authentication-required](./authentication-required.md) -- if the token is missing entirely
