# Query Operational Evidence Template

Schema version: `query-operational-evidence/v1`

Copy this file to
`_bmad-output/test-artifacts/<story-or-proof-key>/evidence-YYYY-MM-DD[-short-run-id].md`
for each run. Keep production secrets and raw sensitive diagnostics out of
committed evidence.

Authoritative field definitions, latency boundaries, classification rules,
redaction rules, and deferred gaps live in
[`docs/operations/query-operational-evidence.md`](../../docs/operations/query-operational-evidence.md).
Do not skip a required section without recording the reason in that section.

## Field Reference

Allowed run classifications:

- `pass`
- `path-viability`
- `sample-only`
- `diagnostic-only`
- `not-claimable`
- `product-failure`
- `environment-blocker`
- `instrumentation-gap`
- `inconclusive`

Required metadata fields:

- `schema_version`
- `evidence_run_id`
- `proof_key`
- `source_commit`
- `generated_at`
- `reviewed_by`
- `safe_tenant_alias`
- `safe_domain_alias`

Required fields must be filled. A required field that is genuinely irrelevant
for the proof shape may carry the per-field marker
`not-applicable: <one-line reason>`. This marker is not a legal value for the
run-level classification.

## Evidence

### Metadata

```yaml
schema_version: query-operational-evidence/v1
evidence_run_id: <required>
proof_key: <required>
source_commit: <required>
generated_at: <required UTC timestamp>
generated_by: <required>
reviewed_by: <required before final verdict>
repository_status: <clean|dirty|not-recorded>
safe_tenant_alias: <required>
safe_domain_alias: <required>
safe_projection_alias: <required>
safe_query_alias: <required>
final_classification: <pass|path-viability|sample-only|diagnostic-only|not-claimable|product-failure|environment-blocker|instrumentation-gap|inconclusive>
```

### Evidence Index

- Proof folder: `_bmad-output/test-artifacts/<story-or-proof-key>/`
- Index file: `_bmad-output/test-artifacts/<story-or-proof-key>/index.md`
- This evidence file:
  `_bmad-output/test-artifacts/<story-or-proof-key>/evidence-YYYY-MM-DD[-short-run-id].md`
- Related artifacts:
    - Logs:
    - Traces:
    - Metrics:
    - Raw samples:
    - Calculation command or worksheet:
    - HTTP request/response summaries:
    - Screenshots or browser snapshots:

### Run Identity

- Evidence run id:
- Story/proof key:
- UTC run window start:
- UTC run window end:
- Source commit:
- Build version:
- Evidence author/agent:
- Reviewer:
- Review timestamp:
- Repository status:

### Environment

- OS/runtime host:
- .NET SDK/runtime:
- Aspire command or AppHost source:
- Aspire AppHost state:
- Docker state:
- DAPR placement/scheduler state:
- DAPR sidecar state:
- Auth mode:
- Observability backend:
- Clock source:
- Clock synchronization notes:
- Port isolation notes:
- Environment blockers:

### Topology

- EventStore instance identity:
- EventStore replica count:
- Query endpoint:
- AppHost resource names:
- DAPR actor sidecars:
- Client/load target:
- Instance routing rule:
- Dependency resources:
- Relevant configuration:

### Query Identity And Authorization

- Route: `POST /api/v1/queries`
- HTTP method:
- Query type:
- Projection type:
- Tenant alias:
- Domain alias:
- Aggregate alias:
- Entity alias:
- Stable query payload identity:
- Authorization mode:
- Tenant/domain claims:
- Permission scope:
- Expected authorization result:
- Negative authorization control:

### Cache-State Setup

Fill every row. Use `not-applicable: <reason>` only when the proof shape
deliberately excludes that phase.

| Phase | Setup evidence | Observed evidence | Classification |
| --- | --- | --- | --- |
| `cold-baseline` |  |  |  |
| `warm-same-validator-304` |  |  |  |
| `gate2-cache-hit` |  |  |  |
| `cache-miss-after-etag-mismatch` |  |  |  |
| `refresh-rewarm` |  |  |  |

R9-A1/R9-A2 boundary decision:

- Stale validator after projection change belongs to R9-A1: `<yes|no|not-applicable>`
- Aspire query-cache topology proof belongs to R9-A2: `<yes|no|not-applicable>`
- This run stays in R9-A8 evidence-pattern scope: `<yes|no>`

### Scenario Matrix

| Scenario id | NFR | Cache state | Query identity | Expected outcome | Observed outcome | Classification |
| --- | --- | --- | --- | --- | --- | --- |
| `<scenario-id>` | `<NFR35\|NFR36\|NFR37\|NFR39>` | `<cache-state>` | `<safe-query-alias>` |  |  |  |

### Measurement Boundaries

Use the canonical labels from the operations document. Do not rename labels in a
way that hides which product path is being measured.

| Boundary label | Start marker | Stop marker | Correlation fields | Clock source | Raw sample artifact | Currently observable? |
| --- | --- | --- | --- | --- | --- | --- |
| `request-received-to-gate1-decision` |  |  |  |  |  |  |
| `etag-actor-lookup` |  |  |  |  |  |  |
| `gate1-304-response` |  |  |  |  |  |  |
| `query-actor-invocation` |  |  |  |  |  |  |
| `gate2-cache-hit-response` |  |  |  |  |  |  |
| `projection-execution` |  |  |  |  |  |  |
| `cache-refresh` |  |  |  |  |  |  |
| `http-response-completed` |  |  |  |  |  |  |
| `client-observed-duration` |  |  |  |  |  |  |

Cross-process timing statement:

- Cross-process timing used: `<yes|no>`
- Clock synchronization source:
- Correlation assumption:
- If assumptions are missing, classification downgrade:
  `<diagnostic-only|not-applicable>`

### Latency Calculation

- Claim type: `<path-viability|sample-only|p99|throughput>`
- NFR claimed: `<NFR35|NFR36|NFR37|NFR39|not-applicable>`
- Threshold source:
- Boundary label:
- Sample count:
- Valid post-warmup sample count:
- Sample window:
- Warmup exclusion rule:
- Excluded samples and reasons:
- Clock source:
- Calculation method:
- Raw sample artifact:
- Calculation command or worksheet:
- Scope: `<instance|endpoint|tenant|projection|aggregate|query-type|combined>`
- Result:
- Classification:

### p99 Inputs

Required when claiming p99. Missing values force `not-claimable`.

- At least 100 valid post-warmup samples: `<yes|no>`
- Nearest-rank method used:
  `rank = ceil(0.99 * valid_sample_count)`: `<yes|no>`
- Sorted raw sample artifact:
- p99 rank:
- p99 value:
- Threshold:
- Pass/fail:
- Retry treatment:
- Mixed cold/warm population rejected: `<yes|no>`
- Reviewer downgrade reason:

### Throughput Inputs

Required when claiming NFR39. Missing values force `not-claimable`.

- Concurrent in-flight requests:
- Achieved requests per second:
- Duration:
- EventStore instance identity:
- Replica count:
- Response mix:
- Query/projection mix:
- Error rate:
- Timeout rate:
- Retry treatment:
- Latency budget outcome:
- Saturation signals:
- Raw load output:
- Calculation command:
- Reviewer downgrade reason:

### Controls

At least one false-positive control is required. Add another `#### Control N`
block for every additional control.

#### Control 1

- Control name:
- Product failure guarded against:
- Setup:
- Expected result:
- Observed result:
- Evidence path:
- Pass/fail:

#### Correlation-Integrity Control

- Mismatched or missing field:
- Expected validation result: `fail`
- Observed validation result:
- Evidence path:
- Reviewer verdict:

### Diagnostics References

- `QueriesController` Gate 1 logs:
- HTTP status/headers:
- `CachingProjectionActor` `CacheHit`/`CacheMiss`/`CacheSkipped` logs:
- `ETagActor` regeneration/state/cold-start logs:
- `DaprETagService` fail-open lookup logs:
- Aspire resource state:
- Aspire console logs:
- Aspire structured logs:
- Aspire traces:
- OpenTelemetry traces:
- OpenTelemetry metrics:
- Product-specific metrics:
- Why diagnostics do or do not prove the NFR claim:

### Exclusions

- Warmup samples excluded:
- Cold-start samples excluded:
- Background load excluded:
- Failed requests excluded:
- Retries excluded or included:
- Mixed tenant/projection/query populations excluded:
- Manual stopwatch, screenshots, or narrative-only evidence excluded:
- Reason each exclusion is valid:

### Redaction

- Redacted bearer tokens:
- Redacted connection strings:
- Redacted production hostnames:
- Redacted tenant/user identifiers:
- Redacted payload fields:
- Raw logs/HAR/network traces omitted:
- Stable aliases used:
- Safe-to-commit statement:

### Deferred Instrumentation Or Follow-Up

| Gap | Owner | Proposed location | Why needed | Blocking this proof? |
| --- | --- | --- | --- | --- |
| `<gap>` | `<owner>` | `<proposed-location>` | `<why-needed>` | `<yes\|no>` |

### Reviewer Verdict

- Reviewer:
- Review timestamp:
- Claimed NFR:
- Required metadata complete: `<yes|no>`
- Required cache-state evidence complete: `<yes|no>`
- Required controls complete: `<yes|no>`
- Raw samples or metrics links complete: `<yes|no|not-applicable>`
- Calculation evidence complete: `<yes|no|not-applicable>`
- Redaction complete: `<yes|no>`
- SignalR-specific mandatory fields excluded: `<yes|no>`
- Final classification:
- Rejection or downgrade reasons:
- Follow-up owner:

### Final Classification

- Final classification:
- Pass/fail summary:
- Claimable boundaries:
- Not-claimable boundaries:
- Product failures:
- Environment blockers:
- Instrumentation gaps:
- Deferred follow-ups:

## Fail-Closed Reviewer Checklist

| Missing or invalid item | Required downgrade |
| --- | --- |
| `schema_version`, `evidence_run_id`, `proof_key`, `source_commit`, `generated_at`, or `reviewed_by` missing | `not-claimable` |
| Safe tenant/domain aliases missing | `not-claimable` |
| Cache state not declared | `not-claimable` |
| Gate 1, Gate 2, and cache miss timings collapsed into one value | `diagnostic-only` |
| p99 uses fewer than 100 valid post-warmup samples | `sample-only` |
| Raw samples or calculation command missing for p99 | `not-claimable` |
| Throughput omits concurrency, response mix, error rate, replica count, or retry treatment | `not-claimable` |
| False-positive control missing | `not-claimable` |
| Correlation-integrity control missing | `not-claimable` |
| Cross-process timing lacks clock and correlation assumptions | `diagnostic-only` |
| Dedicated instrumentation missing for the claimed boundary | `instrumentation-gap` |
| Secret, production hostname, or unsafe user/tenant identifier included | `product-failure` until redacted |

## Intentionally Invalid Example

This example demonstrates a schema rejection. It must not be copied as
successful evidence.

```yaml
schema_version: query-operational-evidence/v1
evidence_run_id: r9a8-20260504-001
proof_key: post-epic-9-r9a8-query-operational-evidence-pattern
source_commit: b9f52e7
generated_at: 2026-05-04T14:00:00Z
reviewed_by: qa-reviewer
safe_tenant_alias: tenant-alias-001
safe_domain_alias: counter
claim:
  nfr: NFR35
  classification: pass
  boundary_label: query-latency
  sample_count: 1
  raw_sample_artifact: null
  calculation_command: null
  clock_source: manual stopwatch
  control_result: not-recorded
observed:
  http_status: 304
  duration_ms: 2
```

Reject this evidence as `not-claimable`. It uses one manual sample, an
ambiguous boundary label, no raw sample artifact, no calculation command, no
control, and no valid p99 sample window. It may be useful as path diagnostics,
but it cannot prove NFR35.
