[<- Back to Error Reference](./index.md)

# Restored-Backup Admission Conflict

**HTTP Status:** 202 Accepted / 409 Conflict / 503 Service Unavailable (depends on `admissionState`)
**Problem Type:** `https://hexalith.io/problems/restored-backup-admission-conflict`

## What Happened

A restored-backup admission request was either blocked, quarantined, requires explicit operator
decision, or cannot yet be proven safe. EventStore never serves protected content until the
admission is `Accepted`.

## Admission State Mapping

| `admissionState` | `reasonCode` | Status | Operator next action |
| --- | --- | --- | --- |
| `Pending` | `pending` | 202 | `SubmitOperatorDecision` |
| `Accepted` | `accepted` | 200 | `None` |
| `Blocked` | `blocked` | 409 | `None` |
| `Quarantined` | `quarantined` | 409 | `SubmitOperatorDecision` |
| `OperatorDecisionRequired` | `operator-decision-required` | 409 | `SubmitOperatorDecision` |
| `DeferredValidation` | `deferred-validation` | 503 | `ProvideRestoreEvidence` |

## Current Behavior

Until the backup engine lands (deferred), every admission request returns `DeferredValidation`
with the safe watermark conflict code `backup-engine-deferred`. Callers must NOT serve protected
content based on this admission. The contract exists so consumers can integrate now and gain the
audit/idempotency guarantees once the engine ships.

## Response Shape

```json
{
  "type": "https://hexalith.io/problems/restored-backup-admission-conflict",
  "title": "Restored backup admission conflict",
  "status": 503,
  "detail": "Admission cannot be proved with current evidence. Provide additional restore evidence and retry.",
  "instance": "/api/v1/admin/backups/admissions",
  "admissionId": "01HK...",
  "admissionState": "DeferredValidation",
  "reasonCode": "deferred-validation",
  "nextAction": "ProvideRestoreEvidence",
  "tenantId": "tenant-1",
  "domain": "orders",
  "backupManifestId": "manifest-1",
  "metadataVersion": 1,
  "watermarkConflict": "backup-engine-deferred",
  "correlationId": "01HK..."
}
```

### Forbidden in the response

Raw key material, key alias text, payload bytes, snapshot state, IVs/nonces, authentication tags,
provider-private metadata, stack traces, state-store keys, connection strings, and provider
exception messages **never** appear.

## See Also

- [Payload protection and crypto-shredding guide](../../guides/payload-protection-and-crypto-shredding.md)
- [Crypto-shredding workflow conflict](./crypto-shredding-workflow-conflict.md)
- [Unreadable protected data](./unreadable-protected-data.md)
