[<- Back to Error Reference](./index.md)

# Command Validation Failed

**HTTP Status:** 400 Bad Request
**Problem Type:** `https://hexalith.io/problems/validation-error`

## What Happened

The command you submitted failed validation. One or more fields in the request body are missing, empty, or have an invalid format. The command was not processed.

## Common Causes

- Required fields (`tenant`, `domain`, `aggregateId`, `commandType`) are missing or empty
- Field values do not match expected formats (e.g., invalid characters in `aggregateId`)
- The `payload` object is missing or malformed for the given `commandType`

## Example

### Request

```http
POST /api/v1/commands HTTP/1.1
Host: localhost:7275
Content-Type: application/json
Authorization: Bearer <your-jwt-token>

{
    "messageId": "increment-01",
    "tenant": "",
    "domain": "",
    "aggregateId": "counter-1",
    "commandType": "IncrementCounter",
    "payload": {}
}
```

### Response

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
    "type": "https://hexalith.io/problems/validation-error",
    "title": "Command Validation Failed",
    "status": 400,
    "detail": "The command has 2 validation error(s). See 'errors' for specifics.",
    "instance": "/api/v1/commands",
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "tenantId": null,
    "errors": {
        "tenant": "'Tenant' must not be empty.",
        "domain": "'Domain' must not be empty."
    }
}
```

## How to Fix

1. Check the `errors` object in the response -- each key is a field name and each value describes the validation rule that failed.
2. Fix all listed fields in your request body.
3. Resubmit the command.

## Related

- [Error Reference Index](./index.md)
- [bad-request](./bad-request.md) -- for malformed requests outside command validation
