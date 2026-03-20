[<- Back to Command API Reference](../command-api.md)

# Error Reference

Hexalith.EventStore uses [RFC 7807 Problem Details](https://www.rfc-editor.org/rfc/rfc7807) for all API error responses. Every error response has `Content-Type: application/problem+json` and includes the following standard fields:

| Field      | Type   | Description                                                  |
| ---------- | ------ | ------------------------------------------------------------ |
| `type`     | string | A URI that identifies the error category (links to this reference) |
| `title`    | string | A short human-readable summary                               |
| `status`   | int    | The HTTP status code                                         |
| `detail`   | string | A human-readable explanation specific to this occurrence      |
| `instance` | string | The request path that produced the error                     |

Some responses include Hexalith-specific extension fields:

| Extension       | Type   | Description                                              |
| --------------- | ------ | -------------------------------------------------------- |
| `correlationId` | string | Unique identifier for the request (useful for log search) |
| `tenantId`      | string | The tenant context of the request                        |
| `consumerId`    | string | Consumer identity used by rate-limiting responses        |
| `errors`        | object | Property-level validation error messages                 |

## 4xx Client Errors

| Status | Error Type | Description |
| ------ | ---------- | ----------- |
| 400    | [validation-error](./validation-error.md) | Command validation failed (missing or invalid fields) |
| 400    | [bad-request](./bad-request.md) | Malformed request (e.g., missing correlation ID) |
| 401    | [authentication-required](./authentication-required.md) | Missing or invalid JWT token |
| 401    | [token-expired](./token-expired.md) | Expired JWT token |
| 403    | [forbidden](./forbidden.md) | Tenant or domain authorization denied |
| 404    | [not-found](./not-found.md) | Requested resource not found |
| 404    | [command-status-not-found](./command-status-not-found.md) | No command status for the given correlation ID |
| 409    | [concurrency-conflict](./concurrency-conflict.md) | Optimistic concurrency conflict (retry recommended) |
| 429    | [rate-limit-exceeded](./rate-limit-exceeded.md) | Per-tenant or per-consumer rate limit exceeded |
| 429    | [backpressure-exceeded](./backpressure-exceeded.md) | Per-resource capacity limit exceeded |

## 5xx Server Errors

| Status | Error Type | Description |
| ------ | ---------- | ----------- |
| 500    | [internal-server-error](./internal-server-error.md) | Unexpected server failure |
| 501    | [not-implemented](./not-implemented.md) | Query endpoint not available |
| 503    | [service-unavailable](./service-unavailable.md) | Processing pipeline temporarily down |

> **Note:** Domain services may define additional error types beyond the 13 listed here. If you receive an error `type` URI not listed on this page, consult the documentation for the specific domain service.
