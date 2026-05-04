# Query Operational Evidence Pattern

This pattern defines the minimum evidence needed to review query pipeline
latency and cache behavior for NFR35, NFR36, NFR37, and NFR39. It is a proof
contract for QA, DevOps, maintainers, and future automation. It does not claim
that the current product satisfies those NFRs; it defines what evidence would
make a claim falsifiable.

Use the reusable schema in
[_bmad-output/test-artifacts/query-operational-evidence-template.md](../../_bmad-output/test-artifacts/query-operational-evidence-template.md)
for every new run.

## Source Inventory

| Source | Contribution |
| --- | --- |
| [`_bmad-output/planning-artifacts/prd.md`](../../_bmad-output/planning-artifacts/prd.md) | Defines NFR35, NFR36, NFR37, and NFR39 thresholds. |
| [`_bmad-output/implementation-artifacts/epic-9-retro-2026-04-30.md`](../../_bmad-output/implementation-artifacts/epic-9-retro-2026-04-30.md) | Records R9-A8: define operational evidence for query latency NFRs. |
| [`_bmad-output/implementation-artifacts/post-epic-10-r10a8-r9-r10-follow-through-tracking.md`](../../_bmad-output/implementation-artifacts/post-epic-10-r10a8-r9-r10-follow-through-tracking.md) | Confirms SignalR evidence exists but query-specific evidence remains separate work. |
| [`docs/operations/signalr-operational-evidence.md`](signalr-operational-evidence.md) | Provides the reusable evidence discipline for storage, controls, classification, redaction, and deferred gaps. |
| [`_bmad-output/test-artifacts/signalr-operational-evidence-template.md`](../../_bmad-output/test-artifacts/signalr-operational-evidence-template.md) | Provides the template structure mirrored here without SignalR-specific mandatory fields. |
| [`docs/reference/query-api.md`](../reference/query-api.md) | Documents `POST /api/v1/queries`, `If-None-Match`, ETag responses, and projection-change semantics. |
| [`src/Hexalith.EventStore/Controllers/QueriesController.cs`](../../src/Hexalith.EventStore/Controllers/QueriesController.cs) | Implements Gate 1 ETag pre-check, `304 NotModified`, safe fail-open behavior, and Gate 1 logs. |
| [`src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`](../../src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs) | Implements Gate 2 query actor cache hit, cache miss, cache skipped, and projection-type discovery logs. |
| [`src/Hexalith.EventStore.Server/Actors/ETagActor.cs`](../../src/Hexalith.EventStore.Server/Actors/ETagActor.cs) | Owns projection/tenant ETag state, self-routing ETag regeneration, activation state load, and cold-start logs. |
| [`src/Hexalith.EventStore.Server/Queries/DaprETagService.cs`](../../src/Hexalith.EventStore.Server/Queries/DaprETagService.cs) | Wraps ETag actor lookup and records fail-open ETag fetch failures. |
| [`src/Hexalith.EventStore.ServiceDefaults/Extensions.cs`](../../src/Hexalith.EventStore.ServiceDefaults/Extensions.cs) | Registers OpenTelemetry, JSON console logging, runtime metrics, ASP.NET Core instrumentation, and tracing sources. |

## Evidence Storage

Store each query evidence run under a story- or proof-specific folder:

```text
_bmad-output/test-artifacts/<story-or-proof-key>/evidence-YYYY-MM-DD[-short-run-id].md
_bmad-output/test-artifacts/<story-or-proof-key>/index.md
```

The folder index should list the run id, date, source commit, environment,
topology, query identity, result, classification, and links to evidence files.
Do not commit bearer tokens, connection strings, production hostnames, raw
payloads containing customer data, raw HAR files with secrets, full production
diagnostic exports, or tenant/user identifiers that are not safe aliases.

Use stable aliases such as `tenant-alias-001`, `domain-alias-001`,
`projection-alias-001`, `<redacted-token>`, and
`<redacted-connection-string>`.

## Mandatory Artifacts

Every evidence file must include:

| Artifact | Required content |
| --- | --- |
| Evidence index | Link to the proof folder index and the current evidence file. |
| Run identity | `schema_version`, `evidence_run_id`, `proof_key`, `source_commit`, `generated_at`, author/agent, repository status, and `reviewed_by`. |
| Environment | OS, .NET runtime, Aspire/DAPR/Docker state, auth mode, observability backend, clock source, and clock assumptions. |
| Topology | EventStore instance identity, replica count, AppHost resources, DAPR actor sidecars, query endpoint, and load/client target. |
| Query identity | Tenant/domain safe aliases, aggregate/entity identity, query type, projection type, route, HTTP status, ETag outcome, and stable query payload identity. |
| Authorization mode | JWT or dev auth mode, tenant/domain claim shape, permission scope, and any negative auth control. |
| Cache-state setup | Cold baseline, warm same-validator `304`, Gate 2 cache hit, cache miss, and refresh/re-warm phase evidence. |
| Latency calculation | Named boundary values, raw samples or metrics links, p99 method when claimed, throughput method when claimed, and threshold source. |
| Controls | At least one false-positive control and one correlation-integrity control from the same run or a linked control run. |
| Diagnostics | Logs, traces, metrics, response headers, raw sample artifacts, calculation command or worksheet, and instrumentation gaps. |
| Redaction | Redacted fields, stable aliases, omitted raw artifacts, and why remaining evidence is safe to commit. |
| Deferred follow-up | Missing instrumentation, load-harness work, schema validation, or telemetry improvements routed as follow-up work. |
| Reviewer verdict | Reviewer name, final classification, claimable/not-claimable decision, rejection reasons, and follow-up owner. |

## Cache-State Setup

Evidence must name the cache state before interpreting latency:

| Cache state | Required setup evidence | Notes |
| --- | --- | --- |
| `cold-baseline` | Actor or app start state, first query marker, and first ETag actor activation context. | Useful diagnostics, but cold ETag actor activation may exceed NFR35. |
| `warm-same-validator-304` | Prior response ETag, matching `If-None-Match`, Gate 1 lookup, and `304 NotModified`. | This is the NFR35 path. Stale validator behavior after projection changes belongs to R9-A1 proof. |
| `gate2-cache-hit` | Gate 1 intentionally bypassed or not applicable, query actor has cached payload, current ETag matches cached ETag, and `CacheHit` is observed. | This is the NFR36 path. Do not count Gate 1 `304` as Gate 2 cache hit. |
| `cache-miss-after-etag-mismatch` | ETag mismatch or empty actor cache, query actor invocation, projection/domain-service query execution, and `CacheMiss`. | This is the NFR37 path. Separate actor routing time from projection execution when possible. |
| `refresh-rewarm` | Projection-change or invalidation event, ETag regeneration, first miss, cache refresh, and subsequent warm proof. | Full topology re-warm belongs to R9-A2 proof when the run proves Aspire/DAPR behavior. |

## Latency Boundaries

Do not collapse all timings into one "query latency" value. Each boundary must
state whether it is directly observable today, available only as diagnostic
trace timing, or a deferred instrumentation gap.

Cross-process subtraction is diagnostic-only unless the evidence states clock
synchronization, correlation continuity, and the clock source for each marker.
For claimable timing, prefer same-process monotonic measurements or metrics
recorded by one process for one named interval. Trace timestamps can support
diagnosis and root-cause analysis; they are claimable only when the clock and
correlation assumptions are written in the evidence.

| Boundary label | Start marker | Stop marker | Required correlation fields | Clock source | Cache state | Current observability |
| --- | --- | --- | --- | --- | --- | --- |
| `request-received-to-gate1-decision` | ASP.NET Core request received for `POST /api/v1/queries` | Gate 1 decides match, miss, skip, or fail-open | `evidence_run_id`, trace/correlation id, tenant alias, route, query identity, `If-None-Match` outcome | Server monotonic or server trace | `warm-same-validator-304`, miss, or skipped | Deferred dedicated metric; logs identify outcome but not elapsed boundary. |
| `etag-actor-lookup` | `DaprETagService.GetCurrentETagAsync` call starts | ETag actor returns value or fail-open warning is logged | Actor id `{projectionType}:{tenantId}`, trace/correlation id, tenant alias, projection type | Server monotonic preferred | Gate 1 and Gate 2 | Deferred dedicated metric; fail-open warning is logged. |
| `gate1-304-response` | Gate 1 ETag match decision | HTTP `304 NotModified` response completed with ETag header | Trace/correlation id, tenant alias, projection type, ETag prefix, HTTP status | Server monotonic or ASP.NET Core request metric with route/status tags | `warm-same-validator-304` | Partially observable through `ETagPreCheckMatch` and HTTP response. |
| `query-actor-invocation` | Mediator routes `SubmitQuery` after Gate 1 miss/skip | Query actor returns `QueryResult` | Query identity, actor id, trace/correlation id, tenant/domain alias | Server monotonic preferred | Gate 2 hit or miss | Deferred dedicated metric; actor logs identify hit/miss outcome. |
| `gate2-cache-hit-response` | Query actor receives envelope with matching current ETag and cached payload | Cached payload returned and HTTP response completed | Actor id, correlation id, cached ETag prefix, HTTP status, query identity | Server monotonic preferred | `gate2-cache-hit` | `CacheHit` log exists; elapsed boundary needs instrumentation or raw samples. |
| `projection-execution` | `ExecuteQueryAsync` begins after cache miss | Projection/domain-service query returns payload or error | Query type, projection type, domain alias, aggregate/entity alias, correlation id | Server monotonic preferred | `cache-miss-after-etag-mismatch` | Deferred unless query handler/harness records it. |
| `cache-refresh` | Successful projection result and current ETag available | Query actor stores payload bytes and cached ETag | Actor id, current ETag prefix, projection type, correlation id | Server monotonic preferred | Cache miss and refresh | `CacheMiss` log identifies refresh result; elapsed boundary is deferred. |
| `http-response-completed` | ASP.NET Core request received | HTTP response body and headers completed | Route, status, trace id, correlation id, tenant alias, response mix | ASP.NET Core server metric or server monotonic | Any query path | Existing ASP.NET Core instrumentation may support request duration, but it is route-level unless query-stage tags are added. |
| `client-observed-duration` | Client sends request | Client receives response | Client target, route, status, safe tenant alias, traceparent/correlation id when available | Client clock | Any query path | Diagnostic-only unless client clock, network scope, and correlation assumptions are stated. |

## NFR Thresholds And Claim Rules

Source thresholds from
[`_bmad-output/planning-artifacts/prd.md`](../../_bmad-output/planning-artifacts/prd.md):

| NFR | Claim boundary | Threshold |
| --- | --- | --- |
| NFR35 | Warm ETag pre-check at the query endpoint, including ETag actor call and comparison, enabling HTTP `304` without activating the query actor. | p99 <= 5 ms |
| NFR36 | Query actor cache hit with ETag match and cached data returned. | p99 <= 10 ms |
| NFR37 | Query actor cache miss with ETag mismatch, projection/domain-service query, and cache refresh. | p99 <= 200 ms |
| NFR39 | Query pipeline concurrent request throughput per EventStore instance without exceeding latency targets. | >= 1,000 concurrent query requests per second |

Allowed run classifications:

| Classification | Use when |
| --- | --- |
| `pass` | Required evidence is present, controls pass, correlation is continuous, and p99 or throughput rules are satisfied for the exact claim. |
| `path-viability` | A bounded run proves the path can work but does not attempt statistical or throughput proof. |
| `sample-only` | Samples exist but sample count, window, warmup, clock discipline, or raw artifact quality is insufficient for p99/throughput. |
| `diagnostic-only` | Evidence helps investigation but cannot support an NFR claim because the boundary is indirect, cross-process assumptions are missing, or fields are incomplete. |
| `not-claimable` | Evidence is present but must be rejected for the stated claim because required proof fields, raw samples, controls, or calculation evidence are missing or invalid. |
| `product-failure` | Query response, ETag behavior, cache behavior, authorization, or latency result violates the expected product contract. |
| `environment-blocker` | Docker, Aspire, DAPR, auth setup, observability, or load harness availability prevents the run. |
| `instrumentation-gap` | Product behavior may be correct, but timestamps, metrics, raw samples, trace tags, or schema validation are insufficient. |
| `inconclusive` | Required artifacts exist, but a non-instrumentation precondition or control cannot be confirmed. |

A run must not imply current NFR compliance unless it is classified `pass` for a
specific NFR claim and includes all required raw data and calculations.

## p99 Claims

A p99 claim must record:

- Sample count after warmup exclusions.
- Warmup rule chosen before measurement.
- Sample window start and end.
- Clock source and clock assumptions.
- Nearest-rank percentile method:
  `rank = ceil(0.99 * valid_sample_count)`, one-based.
- Raw sample artifact location or immutable metrics query link.
- Calculation command, script, or worksheet.
- Threshold source.
- Excluded samples and exclusion reasons.
- Scope: per instance, endpoint, tenant, projection, aggregate, query type, or
  a declared combination.

Fewer than 100 valid post-warmup samples must not be labeled p99. Missing raw
samples, missing calculation evidence, mixed clock sources, mixed cold/warm
populations, or unstated retry treatment force `not-claimable` for p99.

## Throughput Claims

An NFR39 throughput claim must record:

- Concurrent in-flight requests and achieved requests per second.
- Duration and sample window.
- EventStore instance identity and replica count.
- Endpoint, route, tenant/domain alias, projection/query mix, and response mix.
- Latency budget outcome for NFR35, NFR36, and NFR37 paths included in the run.
- Error rate, timeout rate, retry treatment, and whether retries are included.
- Saturation signals: CPU, memory, thread pool, DAPR sidecar health, actor
  placement, dependency latency, queue depth, and HTTP connection pressure when
  available.
- Raw load output, metrics query, and calculation command.

Missing raw load output, missing per-instance identity, unstated response mix,
unstated retry treatment, or latency budgets not evaluated with the throughput
run force `not-claimable`.

## Controls

Every run must include at least one false-positive control and one
correlation-integrity control from the same run or a clearly linked control run.
Controls reused from another story, tenant, projection, environment, or source
commit are reference material, not proof for the current run.

Useful false-positive controls:

- Malformed or mixed-projection `If-None-Match` values fail open to query
  routing and are not counted as Gate 1 `304`.
- Wrong-tenant or missing-permission request is denied and is not counted as a
  successful latency sample.
- Stale `evidence_run_id` or stale source commit is rejected.
- Cache-hit claim is rejected without stable query identity and matching ETag.
- p99 claim is rejected when sample count or raw sample artifact is missing.
- Mixed cold/warm populations are rejected.
- Unrelated background load is recorded and either isolated or rejected.
- Stale ETag evidence and invalidation race evidence are routed to R9-A1 or
  R9-A2 when they are not part of this run.
- DAPR actor failure and domain-service timeout are classified as
  `product-failure`, `environment-blocker`, or `diagnostic-only` as appropriate.

Correlation-integrity controls must prove validation fails when a required
field is missing or mismatched, such as run id, trace id, correlation id, tenant
alias, projection type, query identity, actor id, ETag prefix, or source commit.

## Diagnostics And Instrumentation

Current repository evidence sources:

| Evidence source | Current support | Limits |
| --- | --- | --- |
| `QueriesController` Gate 1 logs | `ETagPreCheckMatch`, `ETagPreCheckMiss`, `ETagPreCheckFailed`, `ETagDecodeSkipped`, and mixed-projection skip logs. | Outcome evidence exists; dedicated elapsed timing is deferred. |
| HTTP status and headers | `304 NotModified`, `200 OK`, ETag headers, and request route/status are observable. | HTTP duration alone does not isolate ETag lookup, Gate 2, or projection execution. |
| `CachingProjectionActor` logs | `CacheHit`, `CacheMiss`, `CacheSkipped`, projection-type discovery, and mismatch logs. | Actor elapsed timing and raw sample export are deferred. |
| `ETagActor` logs | ETag regeneration, state load, cold start, old-format migration, and load/migration failures. | Regeneration and lookup elapsed timing require extra instrumentation. |
| `DaprETagService` logs | ETag actor fetch failure is logged with fail-open classification. | Successful lookup elapsed timing is deferred. |
| Aspire logs and traces | Resource state, console logs, structured logs, and traces can link run behavior. | Missing query-stage tags may prevent claimable boundary isolation. |
| ServiceDefaults OpenTelemetry | ASP.NET Core, HTTP client, runtime metrics, JSON console logs, app source, `Hexalith.EventStore`, and SignalR sources are registered. | Dedicated query histograms and raw sample export are not present in this story. |

Deferred instrumentation:

| Gap | Owner | Proposed location | Why needed | Blocking docs-only proof? |
| --- | --- | --- | --- | --- |
| Dedicated query-stage histograms | EventStore telemetry | Query endpoint and query actor instrumentation | Required for automated p99 claims on Gate 1, Gate 2, and cache miss paths. | No |
| Query-stage Activity tags | EventStore telemetry | `QueriesController`, `DaprETagService`, and `CachingProjectionActor` | Required to distinguish ETag lookup, cache hit, cache miss, projection execution, and refresh. | No |
| Raw-sample export | QA/test tooling | Future perf-lab or operational metrics export | Required for falsifiable p99 and throughput calculations. | No |
| Evidence-schema validation | Tooling/CI | Template schema to JSON schema or lint script | Required to make fail-closed review mechanical. | No |
| Load harness selection | QA/perf-lab | Future load-testing story | Required for NFR39 throughput proof. | No |

Manual stopwatch timing, screenshots alone, narrative-only observations, and
summary tables without raw artifacts cannot satisfy an NFR timing claim.

## SignalR Pattern Reuse

This query pattern mirrors the SignalR evidence discipline for storage,
classification, controls, redaction, and deferred gaps. Query evidence must not
inherit SignalR-specific mandatory fields for hub group join, broadcast origin,
Redis backplane, client receipt, reconnect lifecycle, fanout, subscription, or
transport behavior.

SignalR evidence may be linked as context when a user-visible refresh flow
includes notifications, but the query NFR claim still needs query-specific
boundaries, samples, cache state, and reviewer verdict.

## Claim Examples

Acceptable:

```text
NFR36 sample-only: 25 warm Gate 2 cache-hit samples were collected from one
EventStore instance for tenant-alias-001 and projection-alias-001. The run
proves path viability only because fewer than 100 post-warmup samples exist.
```

Acceptable:

```text
NFR35 pass candidate: 500 post-warmup samples from one EventStore instance
used server-side monotonic timing for request-received-to-gate1-decision and
gate1-304-response. Raw samples and nearest-rank p99 calculation are linked.
```

Unacceptable:

```text
The Query API is p99 compliant because a manual curl returned 304 quickly.
```

Reject the last claim as `not-claimable`: it lacks sample count, raw samples,
clock source, calculation evidence, controls, and a reviewer verdict.

## Reviewer Checklist

A reviewer must fail closed when any required item is missing:

| Check | Failure classification |
| --- | --- |
| Required metadata fields are missing. | `not-claimable` |
| Source commit or safe tenant/domain aliases are missing. | `not-claimable` |
| Cache state is not declared. | `not-claimable` |
| Boundary labels collapse Gate 1, Gate 2, and cache miss timing. | `diagnostic-only` |
| p99 uses fewer than 100 valid post-warmup samples. | `sample-only` |
| Raw samples or calculation command are missing for p99. | `not-claimable` |
| Throughput omits retries, response mix, replica count, or error rate. | `not-claimable` |
| False-positive or correlation-integrity control is absent. | `not-claimable` |
| Cross-process timing lacks clock assumptions. | `diagnostic-only` |
| Missing metrics, spans, or raw export are treated as proof. | `instrumentation-gap` |
| Secrets or unsafe identifiers are present. | `product-failure` until redacted |
