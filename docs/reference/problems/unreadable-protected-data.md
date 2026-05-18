[<- Back to Error Reference](./index.md)

# Unreadable Protected Data

**HTTP Status:** 410 Gone / 422 Unprocessable Entity / 503 Service Unavailable (depends on `reasonCategory`)
**Problem Type:** `https://hexalith.io/problems/unreadable-protected-data`

## What Happened

A protected event payload or snapshot state could not be returned safely. EventStore classifies
the condition deterministically using the provider-neutral
`UnreadableProtectedDataReason` taxonomy (Story 22.7b) and fails closed: the public surface
never returns protected bytes, opaque records, plaintext markers, key material, provider-private
metadata, or provider exception text.

## Reason Taxonomy

| `reasonCategory` | `reasonCode` | Status | Retryable | Permanent |
| --- | --- | --- | --- | --- |
| `MissingKey` | `missing-key` | 422 | maybe (operator) | no |
| `KeyInvalidatedOrDeleted` | `key-invalidated` | 410 | no | yes |
| `ProviderUnavailable` | `provider-unavailable` | 503 | yes | no |
| `ProviderDenied` | `provider-denied` | 422 | maybe | maybe |
| `ConsistencyMismatch` | `consistency-mismatch` | 422 | no | yes |
| `MalformedMetadata` | `malformed-metadata` | 422 | no | yes |
| `UnknownMetadataVersion` | `unknown-metadata-version` | 422 | maybe (upgrade) | maybe |
| `ProviderOpaqueUnsupportedOperation` | `provider-opaque-unsupported` | 422 | no | yes |
| `BytesMetadataMismatch` | `bytes-metadata-mismatch` | 422 | no | yes |

## Common Causes

- The required protection key is unavailable to the configured provider (e.g., not yet
  registered, rotated out, or scoped to a different environment).
- The provider reports the key has been invalidated or deleted (permanent).
- The protection provider is transiently unavailable (network, sidecar, dependency outage).
- Stored protection metadata is malformed, declares an unsupported version, or contains
  forbidden secret-shaped fields.
- The stored record is `ProviderOpaque` — EventStore cannot interpret it without invoking the
  matching protection provider.

## Example

### Request

```http
POST /api/v1/streams/read HTTP/1.1
Host: localhost:7275
Content-Type: application/json
Authorization: Bearer <your-jwt-token>

{
    "tenant": "tenant-a",
    "domain": "billing",
    "aggregateId": "order-001",
    "fromSequence": 1,
    "pageSize": 50
}
```

### Response

```http
HTTP/1.1 422 Unprocessable Entity
Content-Type: application/problem+json

{
    "type": "https://hexalith.io/problems/unreadable-protected-data",
    "title": "Protected data is unreadable",
    "status": 422,
    "detail": "Protection metadata is provider-opaque. The requested operation requires interpretable data.",
    "instance": "/api/v1/streams/read",
    "correlationId": "01HW8M9Q2V1K3Z4Y5X6W7R8T9P",
    "reasonCode": "provider-opaque-unsupported",
    "reasonCategory": "ProviderOpaqueUnsupportedOperation",
    "stage": "replay",
    "sequenceNumber": 42,
    "tenantId": "tenant-a",
    "domain": "billing",
    "aggregateId": "order-001",
    "metadataVersion": 1,
    "retryable": false,
    "permanent": true
}
```

When runtime diagnostics must describe an unsafe provider exception, EventStore uses deterministic
fallback text instead of provider messages:

```text
Protected data diagnostic details were redacted. ReasonCode=<reason-code>; Stage=<stage>.
```

## What is NEVER Returned

- Payload bytes (protected or plaintext).
- Snapshot state content.
- Key material, IVs/nonces, authentication tags.
- Key aliases or provider identifiers.
- Provider-private metadata blobs.
- State-store keys or connection strings.
- Stack traces or provider exception messages.

## How to Fix

**For API consumers:**

1. Read `reasonCode`. Stable wire codes never change.
2. If `retryable=true` (e.g., `provider-unavailable`), retry with backoff. Honor `Retry-After`
   when present.
3. If `permanent=true` (e.g., `key-invalidated`), the protected data is irrecoverable through
   this stream. Contact the service operator for governance-driven remediation.
4. If `unknown-metadata-version`, the storage was written by a newer EventStore release.
   Upgrade.

**For service operators:**

1. Cross-reference the `reasonCode` and `sequenceNumber` against operational logs (look for
   `Stage=PublishUnreadable`, `Stage=ReplayUnreadable`, or `Stage=SnapshotUnreadable`).
2. For `missing-key`: verify the protection provider has the required key registered for the
   tenant/scheme.
3. For `provider-unavailable`: check the protection provider's health endpoint and dependencies.
4. For `consistency-mismatch` / `bytes-metadata-mismatch`: investigate stream provenance.
   Restored-backup governance is owned by Story 22.7c.
5. For `provider-opaque-unsupported`: ensure the correct protection provider is registered for
   the workload.

## Related

- [Payload Protection and Crypto-Shredding Guide](../../guides/payload-protection-and-crypto-shredding.md)
- [Error Reference Index](./index.md)
- [service-unavailable](./service-unavailable.md) -- for non-protection transient failures
