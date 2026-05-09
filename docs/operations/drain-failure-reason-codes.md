# Drain Failure Reason Codes

Aggregate drain failures are reported through the `eventstore.failure_reason` activity tag and matching structured-log context. These values are a stable observability contract: add new values only when a deterministic operation boundary exists, never by parsing localized exception text, and never rename or repurpose an existing value without a compatibility plan.

| Reason code | Emitted when | Retry expectation | Operator action | Compatibility rule |
| --- | --- | --- | --- | --- |
| `drain_event_count_mismatch` | The drain record event count does not match its persisted sequence range. | Automatic retry is not expected to repair the record. | Inspect the drain record and event stream for corruption before manual recovery. | Stable DW1 value; do not rename. |
| `drain_missing_event` | A persisted sequence in the drain range is missing. | Automatic retry is not expected to recreate the missing event. | Inspect state-store contents for the aggregate stream and repair through an explicit recovery path. | Stable DW1 value; do not rename. |
| `drain_publish_failed` | The drain publish operation returns an unsuccessful result or throws at the publish boundary. | Retryable while pub/sub or subscriber infrastructure recovers. | Check pub/sub component health and publisher logs; raw publisher details stay in structured logs, not the activity tag. | Stable DW1 value; do not rename. |
| `drain_state_store_failure` | The drain operation fails at a controlled actor state-store boundary while loading or saving drain state/events. | Usually retryable if the state store or DAPR sidecar recovers. | Check state-store availability, DAPR sidecar logs, ETag/conflict symptoms, and storage health. | Additive DW8 value; future state-store subcategories require tests and a doc update. |
| `drain_dapr_unavailable` | A non-state-store DAPR actor/runtime unavailable signal is classified without a narrower operation-boundary code. | Retryable while DAPR placement, sidecar, or runtime recovers. | Check DAPR sidecar/runtime health and actor placement availability. | Additive DW8 value; keep broad unless a deterministic narrower boundary is added. |
| `unknown` | The failure is uncategorized or lacks a deterministic operation boundary. | Depends on the underlying exception. | Inspect structured logs for the exception type/message and open a classifier follow-up if the same failure repeats. | Stable residual value; do not replace with `drain_failure_unknown`. |

The observable surfaces are activity tags, structured logs, focused server tests, and any dashboard or trace view that reads the activity tag. Raw exception text can appear in logs for diagnostics, but activity tags must stay bounded to the reason codes above.
