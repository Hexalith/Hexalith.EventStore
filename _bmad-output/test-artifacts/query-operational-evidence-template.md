# Query Operational Evidence Template

Schema version: `query-operational-evidence/v1`

Validator support: DW4 validates curated query/v1 fixtures with
`scripts/validate-evidence.ps1 --self-test` or
`bash scripts/validate-evidence.sh --self-test`. This template intentionally
contains placeholders, so copy it to a concrete evidence file before running
the validator against the evidence path.

Copy this file to
`_bmad-output/test-artifacts/<story-or-proof-key>/evidence-YYYY-MM-DD[-short-run-id].md`
for each run. Replace every angle-bracket placeholder (`<...>`) before
committing. Keep production secrets and raw sensitive diagnostics out of
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

Note: `p99` and `throughput` are claim types, not run classifications. A claim
that satisfies all required fields and rules is classified `pass` for the
specific NFR. A claim that fails any required field is downgraded per the
classification rules in
[`docs/operations/query-operational-evidence.md`](../../docs/operations/query-operational-evidence.md).

Required metadata fields (all must be filled before final verdict):

- `schema_version`
- `evidence_run_id`
- `proof_key`
- `source_commit`
- `generated_at`
- `generated_by`
- `reviewed_by`
- `repository_status`
- `safe_tenant_alias`
- `safe_domain_alias`
- `safe_projection_alias`
- `safe_query_alias`
- `final_classification`

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
reviewed_by: <required>
repository_status: <clean|dirty>
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
- Aspire command or AppHost source (omit when not Aspire):
- Aspire AppHost state (omit when not Aspire):
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
- AppHost resource names (omit when not Aspire):
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
- Stable query payload identity (SHA-256 hex of canonical JSON of the query
  payload, sorted keys, no whitespace; record the digest plus the
  canonicalization rule used):
- Authorization mode:
- Tenant/domain claims:
- Permission scope:
- Expected authorization result:
- Negative authorization control:

### Cache-State Setup

Fill every row that applies. Use `not-applicable: <reason>` only when the proof
shape deliberately excludes that phase. The matrix below covers every Gate 1
and Gate 2 outcome the controller and `CachingProjectionActor` can produce.

| Phase | Setup evidence | Observed evidence | Classification |
| --- | --- | --- | --- |
| `cold-baseline` |  |  |  |
| `warm-same-validator-304` |  |  |  |
| `gate1-wildcard-skip` (`If-None-Match: *`) |  |  |  |
| `gate1-mixed-projection-fail-open` (mixed-projection `If-None-Match`) |  |  |  |
| `gate1-too-many-values-fail-open` (>10 comma-separated values) |  |  |  |
| `gate1-fail-open` (ETag service exception) |  |  |  |
| `gate2-cache-hit` |  |  |  |
| `gate2-cache-skipped` (in-actor `DaprETagService` returned null) |  |  |  |
| `cache-miss-after-etag-mismatch` (ETag present and differs) |  |  |  |
| `cache-miss-cold-actor` (actor cache empty) |  |  |  |
| `projection-type-discovery-bypass` (actor returns uncached because ETag was fetched with wrong projection type) |  |  |  |
| `refresh-rewarm` |  |  |  |

R9-A1/R9-A2 boundary decision:

- Stale validator after projection change belongs to R9-A1: `<yes|no|not-applicable>`
- Aspire query-cache topology proof belongs to R9-A2: `<yes|no|not-applicable>`
- This run stays in R9-A8 evidence-pattern scope: `<yes|no>`

### Scenario Matrix

| Scenario id | NFR | Cache state | Query identity | Expected outcome | Observed outcome | Classification |
| --- | --- | --- | --- | --- | --- | --- |
| scenario-id | NFR35 / NFR36 / NFR37 / NFR39 | (one of the cache-state phases above) | safe-query-alias |  |  |  |

### Measurement Boundaries

Use the canonical labels from the operations document. Do not rename labels in a
way that hides which product path is being measured. Note that
`etag-actor-lookup` covers the pre-Gate-1 lookup; the post-mediator lookup that
runs only on 200 responses (after a cache miss/refresh, to decorate the ETag
response header) is recorded separately as
`etag-actor-lookup-post-mediator`.

| Boundary label | Start marker | Stop marker | Correlation fields | Clock source | Raw sample artifact | Currently observable? |
| --- | --- | --- | --- | --- | --- | --- |
| `request-received-to-gate1-decision` |  |  |  |  |  |  |
| `etag-actor-lookup` (pre-Gate-1) |  |  |  |  |  |  |
| `etag-actor-lookup-post-mediator` (200 response decoration) |  |  |  |  |  |  |
| `gate1-304-response` |  |  |  |  |  |  |
| `gate1-fail-open-response` |  |  |  |  |  |  |
| `query-actor-invocation` |  |  |  |  |  |  |
| `gate2-cache-hit-response` |  |  |  |  |  |  |
| `gate2-cache-skipped-response` |  |  |  |  |  |  |
| `projection-execution` |  |  |  |  |  |  |
| `cache-refresh` |  |  |  |  |  |  |
| `http-response-completed` |  |  |  |  |  |  |
| `client-observed-duration` |  |  |  |  |  |  |

Cross-process timing statement (required when any boundary uses cross-process
subtraction):

- Cross-process timing used: `<yes|no>`
- Per-marker clock source named explicitly: `<yes|no>`
- Clock synchronization source (e.g., NTP server, container clock skew bound):
- Correlation continuity assumption (e.g., traceparent end-to-end):
- If any field above is missing or `no`, classification downgrade:
  `diagnostic-only`

### Latency Calculation

- Claim type (one of): `path-viability` / `sample-only` / `p99` / `throughput`
- NFR claimed (one of): `NFR35` / `NFR36` / `NFR37` / `NFR39` (when `claim type` is `p99` or `throughput`, NFR is required and must not be omitted)
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
- Scope (one of): `instance` / `endpoint` / `tenant` / `projection` / `aggregate` / `query-type` / `combined`
- Result:
- Classification:

### p99 Inputs

Required when claiming p99. Missing values force `not-claimable`.

- At least 100 valid post-warmup samples: `<yes|no>`
- Valid post-warmup sample count:
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
- Error rate (any non-2xx response counts here; non-zero forces `not-claimable`):
- Timeout rate (any client/server timeout counts here; non-zero forces `not-claimable`):
- Retry treatment (when retries are included in achieved-rps total, claim is downgraded to `sample-only`):
- Latency budget outcome:
- Saturation signals:
- Raw load output:
- Calculation command:
- Reviewer downgrade reason:

### Controls

At least one false-positive control AND one correlation-integrity control are
required, both observed in the same run as the claim (or in a clearly linked
control run that names the same `evidence_run_id`, source commit, tenant alias,
projection alias, and environment). Add another `#### Control N` block for
every additional control.

#### Control 1

- Control name:
- Product failure guarded against:
- Setup:
- Expected result:
- Observed result:
- Evidence path:
- Same-run as claim (or linked control run id): `<same-run|linked: <run-id>>`
- Pass/fail:

#### Correlation-Integrity Control

- Mismatched or missing field:
- Expected validation result: `fail`
- Observed validation result:
- Evidence path:
- Same-run as claim (or linked control run id): `<same-run|linked: <run-id>>`
- Reviewer verdict:

### Diagnostics References

- `QueriesController` Gate 1 logs (`ETagPreCheckMatch` / `Miss` / `Failed` / `ETagDecodeSkipped` / `MixedProjectionTypesSkipped` / `TooManyIfNoneMatchValues`):
- HTTP status/headers:
- `CachingProjectionActor` `CacheHit`/`CacheMiss`/`CacheSkipped` logs:
- `ETagActor` regeneration/state/cold-start logs:
- `DaprETagService` fail-open lookup logs:
- Aspire resource state (omit when not Aspire):
- Aspire console logs (omit when not Aspire):
- Aspire structured logs (omit when not Aspire):
- Aspire traces (omit when not Aspire):
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
| gap | owner | proposed-location | why-needed | yes / no |

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
- SignalR-specific mandatory fields excluded: `<yes|no>` (if `no`, downgrade to `not-claimable` because the run inherited fields the pattern explicitly forbids)
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
| Any required metadata field missing (`schema_version`, `evidence_run_id`, `proof_key`, `source_commit`, `generated_at`, `generated_by`, `reviewed_by`, `repository_status`, `safe_tenant_alias`, `safe_domain_alias`, `safe_projection_alias`, `safe_query_alias`, `final_classification`) | `not-claimable` |
| Cache state not declared | `not-claimable` |
| Gate 1, Gate 2, and cache miss timings collapsed into one value | `diagnostic-only` |
| p99 uses fewer than 100 valid post-warmup samples | `sample-only` |
| Raw samples or calculation command missing for p99 | `not-claimable` |
| p99 computed from `client-observed-duration` without stated cross-process clock and per-marker correlation continuity | `diagnostic-only` |
| Throughput omits concurrency, response mix, error rate, timeout rate, replica count, or retry treatment | `not-claimable` |
| Throughput run records any non-2xx response or any timeout | `not-claimable` |
| Throughput claim includes retries in achieved-rps total | `sample-only` |
| False-positive control missing or not from same run / linked control run | `not-claimable` |
| Correlation-integrity control missing or not from same run / linked control run | `not-claimable` |
| Cross-process timing lacks per-marker clock source or correlation-continuity statement | `diagnostic-only` |
| Dedicated instrumentation missing for the claimed boundary | `instrumentation-gap` |
| Both `not-claimable` and `instrumentation-gap` rules apply | `instrumentation-gap` when the missing capability is product-side (e.g., no raw-sample export exists in product); `not-claimable` when the gap is run-author hygiene (e.g., raw samples were collected but not attached) |
| Latency Calculation `claim type` is `p99` or `throughput` and NFR field is missing or `not-applicable` | `not-claimable` |
| `repository_status` is missing or unknown | `not-claimable` |
| Reviewer Verdict marks SignalR-specific mandatory fields excluded as `no` | `not-claimable` |
| Secret, production hostname, or unsafe user/tenant identifier included | `not-claimable` until redacted |

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
safe_domain_alias: domain-alias-001
claim:
  nfr: NFR35
  classification: pass
  boundary_label: query-latency
  sample_count: 1
  valid_post_warmup_sample_count: 0
  raw_sample_artifact: null
  calculation_command: null
  clock_source: manual stopwatch
  control_result: not-recorded
observed:
  http_status: 304
  duration_ms: 2
```

Reject this evidence as `not-claimable`. It uses one manual sample with zero
valid post-warmup samples, an ambiguous boundary label (`query-latency` is not
a canonical label in the operations document), no raw sample artifact, no
calculation command, no control, and no valid p99 sample window. It is also
missing required metadata fields (`generated_by`, `repository_status`,
`safe_projection_alias`, `safe_query_alias`, `final_classification`) — any one
of those omissions independently forces `not-claimable` per the fail-closed
checklist before timing is even considered. It may be useful as path
diagnostics, but it cannot prove NFR35.
