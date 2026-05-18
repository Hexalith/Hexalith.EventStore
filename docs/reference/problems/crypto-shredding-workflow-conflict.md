[<- Back to Error Reference](./index.md)

# Crypto-Shredding Workflow Conflict

**HTTP Status:** 202 Accepted / 409 Conflict / 422 Unprocessable Entity / 425 Too Early
(depends on `workflowState`)
**Problem Type:** `https://hexalith.io/problems/crypto-shredding-workflow-conflict`

## What Happened

A crypto-shredding workflow request conflicts with the current workflow state. The body carries
the canonical state machine name and stable kebab-case reason code; the human-readable text is
localizable and is NEVER the contract.

## Workflow State Mapping

| `workflowState` | `reasonCode` | Status | Operator next action |
| --- | --- | --- | --- |
| `Requested` | `requested` | 202 | `SubmitOperatorDecision` |
| `Approved` | `approved` | 202 | `None` |
| `Rejected` | `rejected` | 409 | `None` |
| `PendingProvider` | `pending-provider` | 425 | `RetryWithBackoff` |
| `Invalidated` | `invalidated` | 409 | `None` |
| `Deleted` | `deleted` | 409 | `None` |
| `VerificationFailed` | `verification-failed` | 422 | `SubmitOperatorDecision` |
| `RestoreConflict` | `restore-conflict` | 409 | `SubmitOperatorDecision` |
| `Quarantined` | `quarantined` | 409 | `SubmitOperatorDecision` |
| `OperatorDecisionRequired` | `operator-decision-required` | 409 | `SubmitOperatorDecision` |
| `Completed` | `completed` | 409 | `None` |
| `CancelledBeforeDecision` | `cancelled-before-decision` | 409 | `None` |

## Common Causes

- Caller attempted to undo an irreversible decision (`Invalidated` / `Deleted`).
- Caller submitted a duplicate request while the provider call was in flight (`PendingProvider`).
- A restored backup conflicts with a recorded workflow (`RestoreConflict`).
- The provider reported a verification failure that requires explicit operator resolution.

## Response Shape

```json
{
  "type": "https://hexalith.io/problems/crypto-shredding-workflow-conflict",
  "title": "Crypto-shredding workflow conflict",
  "status": 409,
  "detail": "Key invalidation has been recorded. Affected data is permanently unreadable.",
  "instance": "/api/v1/admin/...",
  "workflowId": "01HK...",
  "workflowState": "Invalidated",
  "reasonCode": "invalidated",
  "nextAction": "None",
  "tenantId": "tenant-1",
  "domain": "orders",
  "aggregateId": "agg-1",
  "irreversibleDecisionRecorded": true,
  "correlationId": "01HK..."
}
```

### Forbidden in the response

Raw key material, key alias text, payload bytes, snapshot state, IVs/nonces, authentication tags,
provider-private metadata, stack traces, state-store keys, connection strings, and provider
exception messages **never** appear.

## See Also

- [Payload protection and crypto-shredding guide](../../guides/payload-protection-and-crypto-shredding.md)
- [Restored-backup admission conflict](./restored-backup-admission-conflict.md)
- [Unreadable protected data](./unreadable-protected-data.md)
