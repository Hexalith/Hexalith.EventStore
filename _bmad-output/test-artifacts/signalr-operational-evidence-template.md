# SignalR Operational Evidence Template

Schema version: `signalr-operational-evidence/v1`

Copy this file to `_bmad-output/test-artifacts/<story-or-proof-key>/evidence-YYYY-MM-DD[-short-run-id].md` for each run. Keep production secrets and raw diagnostics out of committed evidence.

Authoritative field definitions, latency intervals, classification table, and redaction rules live in [`docs/operations/signalr-operational-evidence.md`](../../docs/operations/signalr-operational-evidence.md). The Mandatory Artifacts list in that document is the canonical checklist; the section headings below mirror it. Do not skip a section without recording the reason in the section's body.

## Field Reference

Allowed classification values (single-value enum for the run-level `Classification` field):

- `pass`
- `product-failure`
- `environment-blocker`
- `instrumentation-gap`
- `sample-only`
- `inconclusive`

Required fields must be filled. A required field that is genuinely irrelevant for the proof shape may instead carry the **per-field marker** `not-applicable: <one-line reason>` — this marker is for individual field bodies only and is not a legal value of the run-level `Classification`. Optional fields may be omitted when they do not apply to the proof shape.

## Evidence

### Run Identity

- Evidence run id: `<required>`
- Schema version: `signalr-operational-evidence/v1`
- Story/proof key: `<required>`
- UTC run window start: `<required>`
- UTC run window end: `<required>`
- Commit SHA/build version: `<required>`
- Evidence author/agent: `<required>`
- Repository status: `<clean|dirty|not-recorded>`
- Classification: `<pass|product-failure|environment-blocker|instrumentation-gap|sample-only|inconclusive>`

### Evidence Index

- Proof folder: `_bmad-output/test-artifacts/<story-or-proof-key>/`
- Index file: `_bmad-output/test-artifacts/<story-or-proof-key>/index.md`
- This evidence file: `_bmad-output/test-artifacts/<story-or-proof-key>/evidence-YYYY-MM-DD[-short-run-id].md`
- Related artifacts:
    - Logs:
    - Traces:
    - Metrics:
    - Screenshots/browser snapshots:
    - Query payloads/responses:

### Environment

- OS/runtime host:
- .NET SDK/runtime:
- Aspire command or apphost source:
- Aspire AppHost state:
- Docker state:
- Redis state:
- DAPR placement/scheduler state:
- Browser automation state:
- Auth/token setup:
- Observability backend:
- Clock source:
- Clock synchronization notes:
- Port isolation notes:
- Environment blockers:

### Topology

- EventStore instance count:
- EventStore resources/processes/containers:
- Client target:
- Broadcast origin:
- SignalR hub URL:
- Redis/backplane endpoint or explicit none:
- DAPR resources:
- Keycloak/auth mode:
- Connection target:
- Broadcast origin identity:

### SignalR Configuration

- `EventStore:SignalR:Enabled`:
- `EventStore:SignalR:BackplaneRedisConnectionString` or `EVENTSTORE_SIGNALR_REDIS`:
- Runtime proof/test-only gate:
- Hub path:
- Group format:
- Group name:
- Public hub payload confirmed as `ProjectionChanged(projectionType, tenantId)` only: `<yes|no>`

### Correlation And Identity

- Evidence run id:
- Operation id:
- Trace id:
- Span id(s):
- Correlation id:
- Causation id:
- Command/message id:
- Event id or projection-change identifier:
- Stream or aggregate id:
- Projection type:
- Tenant id or safe tenant alias:
- Group name:
- Connection id or client-session alias:
- Connection target:
- Broadcast origin:
- Query response status:
- ETag evidence:

### Authenticated Join

- Token source or safe alias:
- Tenant authorization result:
- Join request timestamp UTC:
- Join completed timestamp UTC:
- Joined group:
- Connection id or client-session alias:
- Join logs/traces:

### Trigger And Broadcast

- Trigger shape: `<command|projection-change-notification|runtime-proof-endpoint|other>`
- Trigger accepted UTC:
- ETag regeneration completed UTC or equivalent projection-change publish point:
- Broadcast start UTC:
- Broadcast completed UTC:
- Broadcast origin:
- Broadcast group:
- Broadcast result:
- Broadcast start EventId/category:
- Broadcast completed or fail-open EventId/category:
- Broadcast elapsed milliseconds:
- Broadcast exception type:
- Broadcast logs/traces:

### Client Receipt

- Client receipt UTC:
- Received method:
- Received `projectionType`:
- Received `tenantId`:
- Payload contains only `projectionType` and `tenantId`: `<yes|no>`
- Receipt connection id or client-session alias:
- Receipt EventId/category:
- Receipt connection state:
- Receipt callback count:
- Receipt logs/traces:

### Query Refresh

- Query started UTC:
- Query completed UTC:
- Query endpoint:
- Query response status:
- ETag before:
- ETag after:
- `304 Not Modified` observed:
- Response payload redaction notes:
- Query logs/traces:

### UI Render Evidence

- Required for this proof: `<yes|no>`
- UI refresh/render UTC:
- Browser target:
- Screenshot/snapshot path:
- Visible state before:
- Visible state after:
- Browser automation logs:

### Latency Calculation

- Claim type: `<path-viability|sample-only|p99>`
- Threshold source:
- SignalR delivery threshold:
- Sample count:
- Sample window:
- Warmup exclusion rule:
- Clock source:
- Calculation method: `nearest-rank, rank = ceil(0.99 * sample_count)`
- Trigger-to-broadcast:
- Broadcast-to-client-receipt:
- Client-receipt-to-query-refresh:
- Query-refresh-to-render:
- End-to-end trigger-to-refresh:
- Cold-start outliers:
- Warm-run outliers:
- Raw sample artifact:

### Reliability Controls

At least one false-positive control is required. Add another `#### Control N` block for every additional control; do not stop at one if the proof shape exercises more guards.

#### Control 1

- Control name:
- Product failure guarded against:
- Setup:
- Expected result:
- Observed result:
- Pass/fail:

<!-- Duplicate the block above as `#### Control 2`, `#### Control 3`, ... for each additional false-positive control. -->

#### Correlation-integrity control (required)

- Mismatched or missing field:
- Expected validation result: `fail`
- Observed validation result:
- Evidence path:

### Diagnostics

- Product logs:
- Product traces:
- Product metrics:
- SignalR server ActivitySource (`Microsoft.AspNetCore.SignalR.Server`) evidence:
- SignalR client ActivitySource (`Microsoft.AspNetCore.SignalR.Client`) evidence:
- `Microsoft.AspNetCore.Http.Connections` metrics:
- Connection-health interpretation:
- Why diagnostics do or do not prove specific delivery:

### Redaction

- Redacted tokens:
- Redacted connection strings:
- Redacted production hostnames:
- Redacted tenant/user identifiers:
- Raw logs/HAR/network traces omitted:
- Stable aliases used:

### Deferred Instrumentation Or Follow-Up

| Gap | Owner | Proposed location | Why needed | Blocking this proof? |
| --- | --- | --- | --- | --- |
| `<gap>` | `<owner>` | `<proposed-location>` | `<why-needed>` | `<yes\|no>` |

### Result

- Final classification:
- Pass/fail summary:
- Product failures:
- Environment blockers:
- Instrumentation gaps:
- Evidence reviewer notes:

## Intentionally Invalid Example

This example demonstrates a schema rejection. It must not be copied as successful evidence.

```yaml
schema_version: signalr-operational-evidence/v1
classification: product-failure
validation_result: fail
reason: client receipt cannot be correlated to the current trigger
trigger:
  evidence_run_id: r10a6-20260502-001
  correlation_id: r10a6-20260502-001
  broadcast_completed_utc: 2026-05-02T13:30:17.376Z
client_receipt:
  evidence_run_id: r10a6-20260502-previous-run
  correlation_id: r10a6-20260502-previous-run
  receipt_utc: 2026-05-02T13:30:17.407Z
  projection_type: counter
  tenant_id: tenant-alias-001
```

Reject this evidence even though a matching-looking `ProjectionChanged(counter, tenant-alias-001)` was observed. The run/correlation identifiers prove it may be stale or unrelated to the current trigger.
