[<- Back to Error Reference](./index.md)

# Ambiguous Command Correlation

**HTTP Status:** 409 Conflict  
**Problem Type:** `https://hexalith.io/problems/command-correlation-ambiguous`

## What Happened

Status lookup cannot select one command safely. The supplied identifier can match
multiple commands through the correlation index, match MessageId records in more
than one authorized tenant, or match a direct record in one authorized tenant and
a correlation-index record in another. Multiple legacy records can also be ambiguous.
The response does not list matching command, aggregate or tenant identities.

## Example

```http
HTTP/1.1 409 Conflict
Content-Type: application/problem+json

{
    "type": "https://hexalith.io/problems/command-correlation-ambiguous",
    "title": "Ambiguous Command Correlation",
    "status": 409,
    "detail": "The identifier matches multiple commands or authorized tenant scopes. Verify the command MessageId and authorized tenant scope; contact support if a MessageId lookup remains ambiguous.",
    "instance": "/api/v1/commands/status/01ARZ3NDEKTSV4RRFFQ69G5FAV",
    "correlationId": "01ARZ3NDEKTSV4RRFFQ69G5FAW"
}
```

## How to Fix

1. Verify the MessageId returned by the original command submission rather than
   assuming a correlation identifier uniquely identifies the command.
2. Have an operator verify the intended environment and authorized tenant scope.
   The status route has no tenant-selector parameter. A MessageId alone does not
   resolve every cross-tenant collision.
3. If the MessageId lookup remains ambiguous, contact support. Repeating the same
   lookup cannot disambiguate it. Do not infer command success or submit the
   business command again merely because its status lookup returned 409. Give support
   the response `correlationId`, which identifies this status request for diagnostics;
   it can differ from the command MessageId or correlation identifier in the URL.

See [status lookup identity](../command-api.md#status-lookup-identity) for the
existing lookup contract.
