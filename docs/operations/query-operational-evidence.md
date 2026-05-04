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
| Run identity | `schema_version`, `evidence_run_id`, `proof_key`, `source_commit`, `generated_at`, `generated_by`, `repository_status`, and `reviewed_by`. |
| Environment | OS, .NET runtime, Aspire/DAPR/Docker state (Aspire fields may be omitted on non-Aspire deploys), auth mode, observability backend, clock source, and clock assumptions. |
| Topology | EventStore instance identity, replica count, AppHost resources (when Aspire), DAPR actor sidecars, query endpoint, and load/client target. |
| Query identity | Tenant/domain/projection/query safe aliases, aggregate/entity identity, query type, projection type, route, HTTP status, ETag outcome, and a stable query payload identity defined as the SHA-256 hex digest of the canonical JSON of the query payload (sorted keys, no whitespace). Record the digest plus the canonicalization rule used. |
| Authorization mode | JWT or dev auth mode, tenant/domain claim shape, permission scope, and any negative auth control. |
| Cache-state setup | Cold baseline, warm same-validator `304`, Gate 1 wildcard skip, Gate 1 mixed-projection fail-open, Gate 1 too-many-values fail-open, Gate 1 fail-open on ETag service exception, Gate 2 cache hit, Gate 2 cache skipped (in-actor fail-open), cache miss after ETag mismatch, cache miss with cold actor cache, projection-type-discovery bypass, and refresh/re-warm phase evidence — fill every row that applies to the proof shape. |
| Latency calculation | Named boundary values, raw samples or metrics links, p99 method when claimed, throughput method when claimed, and threshold source. |
| Controls | At least one false-positive control AND one correlation-integrity control, both observed in the same run as the claim or in a clearly linked control run that names the same `evidence_run_id`, source commit, tenant alias, projection alias, and environment. |
| Diagnostics | Logs, traces, metrics, response headers, raw sample artifacts, calculation command or worksheet, and instrumentation gaps. |
| Redaction | Redacted fields, stable aliases, omitted raw artifacts, and why remaining evidence is safe to commit. |
| Deferred follow-up | Missing instrumentation, load-harness work, schema validation, or telemetry improvements routed as follow-up work. |
| Reviewer verdict | Reviewer name, final classification, claimable/not-claimable decision, rejection reasons, and follow-up owner. |

## Cache-State Setup

Evidence must name the cache state before interpreting latency. The matrix
below covers every Gate 1 and Gate 2 outcome the controller and
`CachingProjectionActor` produce today. Fill every row that applies; use
`not-applicable: <reason>` only when the proof shape deliberately excludes a
phase.

| Cache state | Required setup evidence | Notes |
| --- | --- | --- |
| `cold-baseline` | Actor or app start state, first query marker, and first ETag actor activation context. | Useful diagnostics, but cold ETag actor activation may exceed NFR35; per PRD this cold-actor activation is excluded from NFR35 claims. |
| `warm-same-validator-304` | Prior response ETag, matching `If-None-Match`, Gate 1 lookup, and `304 NotModified`. | This is the NFR35 path. Stale validator behavior after projection changes belongs to R9-A1 proof. |
| `gate1-wildcard-skip` | `If-None-Match: *` request, `ETagPreCheckMiss(currentETag=false, hasIfNoneMatch=true)` log emitted exactly once, no ETag actor lookup, request falls through to Gate 2. | The controller deliberately skips Gate 1 for wildcard validators because there is no projection type to decode. Latency claim falls under NFR36 / NFR37 paths, not NFR35. |
| `gate1-mixed-projection-fail-open` | `If-None-Match` with multiple projection-type prefixes, `MixedProjectionTypesSkipped` log AND `ETagPreCheckMiss(currentETag=false, hasIfNoneMatch=true)` log emitted. | Gate 1 evaluation runs but with `currentETag = null`, so the request fails open to query routing — this is NOT a "Gate 1 skipped" path; both log events fire on the same request. |
| `gate1-too-many-values-fail-open` | `If-None-Match` with more than `MaxIfNoneMatchValues = 10` comma-separated values; `TooManyIfNoneMatchValues` log emitted; comparison short-circuits. | A Gate 1 fail-open variant. Treat like `gate1-mixed-projection-fail-open` for classification: routes to Gate 2 with `currentETag` available but no Gate 1 match. |
| `gate1-fail-open` | `eTagService.GetCurrentETagAsync` throws; `ETagPreCheckFailed` log emitted; controller sets `currentETag = null` and proceeds. | Distinct from `cache-miss-after-etag-mismatch` and `gate2-cache-hit` because it indicates an upstream lookup failure. Latency claims here are diagnostic-only until dedicated metrics exist. |
| `gate2-cache-hit` | Gate 1 intentionally bypassed or not applicable, query actor has cached payload, `eTagService.GetCurrentETagAsync` (called by the actor) returns a non-null ETag, current ETag matches cached ETag, and `CacheHit` is observed. | This is the NFR36 path. Do not count Gate 1 `304` as Gate 2 cache hit. |
| `gate2-cache-skipped` | In-actor `DaprETagService.GetCurrentETagAsync` failed (logged as `ETagFetchFailed`) and returned null; `CacheSkipped` is observed; the actor proceeds to compute the result without caching. | Distinct from `gate2-cache-hit` because the cache-hit precondition (non-null current ETag) is not met. A run that observes this path must not claim NFR36; classify diagnostic-only or instrumentation-gap depending on the gap. |
| `cache-miss-after-etag-mismatch` | Non-null current ETag that does not match the actor's cached ETag, query actor invocation, projection/domain-service query execution, and `CacheMiss`. | This is the NFR37 mismatch sub-path. Separate actor routing time from projection execution when possible. |
| `cache-miss-cold-actor` | Actor cache empty (first invocation or after eviction), `CacheMiss` observed without prior ETag mismatch. | Distinct from `cache-miss-after-etag-mismatch` because the population is colder; mixing these two into one sample window is rejected ("mixed cold/warm populations"). |
| `projection-type-discovery-bypass` | `CachingProjectionActor` discovers the ETag was fetched using the wrong projection type (envelope domain vs discovered projection); the actor returns the result WITHOUT caching to avoid contaminating the cache. | Distinct from cache-hit, cache-miss, and cache-skipped: the result is correct but uncached. Do not claim NFR36 or cache-refresh from this path; classify diagnostic-only until the next request observes the correct projection type. |
| `refresh-rewarm` | Projection-change or invalidation event, ETag regeneration, first miss, cache refresh, and subsequent warm proof. | Full topology re-warm belongs to R9-A2 proof when the run proves Aspire/DAPR behavior. |

## Latency Boundaries

Do not collapse all timings into one "query latency" value. Each boundary must
state whether it is directly observable today, available only as diagnostic
trace timing, or a deferred instrumentation gap.

Cross-process subtraction is diagnostic-only unless the evidence states, per
marker, the clock source AND the correlation-continuity assumption (e.g.,
end-to-end traceparent propagation). For claimable timing, prefer same-process
monotonic measurements or metrics recorded by one process for one named
interval. Trace timestamps can support diagnosis and root-cause analysis;
they are claimable only when the per-marker clock source and correlation
assumptions are written in the evidence.

Note on `etag-actor-lookup`: the controller calls
`eTagService.GetCurrentETagAsync` twice on a 200 response — once before Gate 1
(part of the NFR35 / NFR36 / NFR37 windows) and once after the mediator
returns, only when the first lookup yielded null, to decorate the response
ETag header. Track these as two separate boundaries
(`etag-actor-lookup` and `etag-actor-lookup-post-mediator`); the second is
outside every NFR threshold window today.

| Boundary label | Start marker | Stop marker | Required correlation fields | Clock source | Cache state | Current observability |
| --- | --- | --- | --- | --- | --- | --- |
| `request-received-to-gate1-decision` | ASP.NET Core request received for `POST /api/v1/queries` | Gate 1 decides match, miss, skip, or fail-open | `evidence_run_id`, trace/correlation id, tenant alias, route, query identity, `If-None-Match` outcome | Server monotonic or server trace | `warm-same-validator-304`, miss, or skipped | Deferred dedicated metric; logs identify outcome but not elapsed boundary. |
| `etag-actor-lookup` (pre-Gate-1) | `DaprETagService.GetCurrentETagAsync` call starts (before Gate 1 decision) | ETag actor returns value or fail-open warning is logged | Actor id `{projectionType}:{tenantId}`, trace/correlation id, tenant alias, projection type | Server monotonic preferred | Gate 1 and Gate 2 | Deferred dedicated metric; fail-open warning is logged. |
| `etag-actor-lookup-post-mediator` (200 response decoration) | `DaprETagService.GetCurrentETagAsync` call starts (after mediator returns, only when prior lookup was null) | ETag actor returns value or fail-open warning is logged | Same as `etag-actor-lookup` | Server monotonic preferred | Cache miss / fail-open paths only | Deferred dedicated metric. Outside NFR35/36/37 threshold windows; do not include in those p99 calculations. |
| `gate1-304-response` | Gate 1 ETag match decision | HTTP `304 NotModified` response completed with ETag header | Trace/correlation id, tenant alias, projection type, ETag prefix, HTTP status | Server monotonic preferred | `warm-same-validator-304` | Deferred dedicated metric; `ETagPreCheckMatch` log identifies outcome but carries no elapsed timing. |
| `gate1-fail-open-response` | Gate 1 fail-open trigger (mixed-projection / too-many-values / ETag service exception) | HTTP response completed (200/5xx depending on downstream outcome) | Trace/correlation id, fail-open reason, projection type when available | Server monotonic preferred | `gate1-mixed-projection-fail-open`, `gate1-too-many-values-fail-open`, `gate1-fail-open` | Deferred dedicated metric; logs identify the fail-open reason. |
| `query-actor-invocation` | Mediator routes `SubmitQuery` after Gate 1 miss/skip/fail-open | Query actor returns `QueryResult` | Query identity, actor id, trace/correlation id, tenant/domain alias | Server monotonic preferred | Gate 2 hit / miss / skipped | Deferred dedicated metric; actor logs identify hit/miss/skipped outcome. |
| `gate2-cache-hit-response` | Query actor receives envelope with matching current ETag and cached payload | Cached payload returned and HTTP response completed | Actor id, correlation id, cached ETag prefix, HTTP status, query identity | Server monotonic preferred | `gate2-cache-hit` | `CacheHit` log exists; elapsed boundary needs instrumentation or raw samples. |
| `gate2-cache-skipped-response` | Query actor in-actor `DaprETagService.GetCurrentETagAsync` returned null (fail-open) | Result returned without caching | Actor id, correlation id, fail-open reason | Server monotonic preferred | `gate2-cache-skipped` | `CacheSkipped` log identifies path; elapsed boundary is deferred. |
| `projection-execution` | `ExecuteQueryAsync` begins after cache miss | Projection/domain-service query returns payload or error | Query type, projection type, domain alias, aggregate/entity alias, correlation id | Server monotonic preferred | `cache-miss-after-etag-mismatch` and `cache-miss-cold-actor` | Deferred unless query handler/harness records it. |
| `cache-refresh` | Successful projection result and current ETag available | Query actor stores payload bytes and cached ETag | Actor id, current ETag prefix, projection type, correlation id | Server monotonic preferred | Cache miss and refresh | `CacheMiss` log identifies refresh result; elapsed boundary is deferred. |
| `http-response-completed` | ASP.NET Core request received | HTTP response body and headers completed | Route, status, trace id, correlation id, tenant alias, response mix | ASP.NET Core server metric or server monotonic | Any query path | ASP.NET Core `http.server.request.duration` is tagged by route and status only; it CANNOT disambiguate Gate 1 304 from Gate 2 cache hit from cache miss. Use it as a sanity envelope, not as per-NFR proof. |
| `client-observed-duration` | Client sends request | Client receives response | Client target, route, status, safe tenant alias, traceparent/correlation id when available | Client clock | Any query path | Diagnostic-only unless per-marker clock source AND correlation continuity assumptions are stated. p99 computed from this boundary alone is downgraded to `diagnostic-only`. |

## NFR Thresholds And Claim Rules

Source thresholds from
[`_bmad-output/planning-artifacts/prd.md`](../../_bmad-output/planning-artifacts/prd.md):

| NFR | Claim boundary | Threshold |
| --- | --- | --- |
| NFR35 | Warm ETag pre-check at the query endpoint, including ETag actor call (the pre-Gate-1 lookup only — the post-mediator ETag-set call on 200 responses is outside the NFR35 window) and ETag comparison, enabling HTTP `304` without invoking the query actor (`CachingProjectionActor`). Cold ETag actor activations are excluded from NFR35 per PRD. | p99 <= 5 ms |
| NFR36 | Query actor cache hit with ETag match and cached data returned. | p99 <= 10 ms |
| NFR37 | Query actor cache miss with ETag mismatch or cold actor cache, projection/domain-service query, and cache refresh. | p99 <= 200 ms |
| NFR39 | Sustained query throughput per EventStore instance without exceeding NFR35/36/37 latency targets. Recorded as two related quantities: concurrent in-flight requests and achieved requests per second. | concurrent in-flight >= 1,000 AND achieved rps >= 1,000 |

Allowed run classifications:

| Classification | Use when |
| --- | --- |
| `pass` | Required evidence is present, controls pass, correlation is continuous, and p99 or throughput rules are satisfied for the exact claim. |
| `path-viability` | A bounded run proves the path can work but does not attempt statistical or throughput proof. |
| `sample-only` | Samples exist but sample count, window, warmup, clock discipline, or raw artifact quality is insufficient for p99/throughput. |
| `diagnostic-only` | Evidence helps investigation but cannot support an NFR claim because the boundary is indirect, cross-process assumptions are missing, or fields are incomplete. |
| `not-claimable` | Evidence is present but must be rejected for the stated claim because required proof fields, raw samples, controls, or calculation evidence are missing or invalid (run-author hygiene). |
| `product-failure` | Query response, ETag behavior, cache behavior, authorization, or latency result violates the expected product contract. |
| `environment-blocker` | Docker, Aspire, DAPR, auth setup, observability, or load harness availability prevents the run. |
| `instrumentation-gap` | Product behavior may be correct, but timestamps, metrics, raw samples, trace tags, or schema validation are insufficient because the product does not yet emit them. |
| `inconclusive` | Required artifacts exist, but a non-instrumentation precondition or control cannot be confirmed. |

Precedence when more than one classification could apply:

1. `product-failure` always dominates: if the run shows a contract violation, classify `product-failure` regardless of evidence quality.
2. `environment-blocker` dominates over evidence-quality classifications: if the run could not be executed, do not assess evidence quality.
3. `instrumentation-gap` dominates `not-claimable` when the missing capability is product-side (e.g., raw-sample export does not exist in the product). This routes the gap to the right backlog.
4. `not-claimable` dominates `instrumentation-gap` when the run-author had the capability and did not record it (e.g., raw samples were collected but not attached to the evidence).
5. `diagnostic-only` is the floor for cross-process timing and `client-observed-duration`-derived p99 when clock and correlation assumptions are not stated per marker.
6. `sample-only` is reserved for under-powered statistical claims (fewer than 100 valid post-warmup samples for p99, or retries-included rps for throughput).
7. `inconclusive` applies only when none of the above dominates and a non-instrumentation precondition is missing.

A run must not imply current NFR compliance unless it is classified `pass` for a
specific NFR claim and includes all required raw data and calculations.

## p99 Claims

A p99 claim must record:

- Sample count after warmup exclusions (record as `valid_post_warmup_sample_count`).
- Warmup rule chosen before measurement.
- Sample window start and end.
- Per-marker clock source and clock assumptions.
- Nearest-rank percentile method:
  `rank = ceil(0.99 * valid_post_warmup_sample_count)`, one-based.
- Raw sample artifact location or immutable metrics query link.
- Calculation command, script, or worksheet.
- Threshold source.
- Excluded samples and exclusion reasons.
- Scope: per instance, endpoint, tenant, projection, aggregate, query type, or
  a declared combination.

Fewer than 100 valid post-warmup samples must not be labeled p99. Missing raw
samples, missing calculation evidence, mixed clock sources, mixed cold/warm
populations, or unstated retry treatment force `not-claimable` for p99. A p99
computed from `client-observed-duration` without per-marker clock source AND
correlation-continuity assumptions stated is downgraded to `diagnostic-only`.

## Throughput Claims

An NFR39 throughput claim must record:

- Concurrent in-flight requests and achieved requests per second.
- Duration and sample window.
- EventStore instance identity and replica count.
- Endpoint, route, tenant/domain alias, projection/query mix, and response mix.
- Latency budget outcome for NFR35, NFR36, and NFR37 paths included in the run.
- Error rate, timeout rate, retry treatment, and whether retries are included
  in the achieved-rps total.
- Saturation signals: CPU, memory, thread pool, DAPR sidecar health, actor
  placement, dependency latency, queue depth, and HTTP connection pressure when
  available.
- Raw load output, metrics query, and calculation command.

Tolerance and downgrade rules:

- Any non-2xx HTTP response in the throughput window forces `not-claimable`.
- Any client or server timeout in the throughput window forces `not-claimable`.
- Retries included in the achieved-rps total downgrade the claim to `sample-only`.
- Missing raw load output, missing per-instance identity, unstated response
  mix, unstated retry treatment, or latency budgets not evaluated with the
  throughput run force `not-claimable`.

## Controls

Every run must include at least one false-positive control AND one
correlation-integrity control, both observed in the same run as the claim or
in a clearly linked control run that names the same `evidence_run_id`, source
commit, tenant alias, projection alias, and environment. Controls reused from
another story, tenant, projection, environment, or source commit are reference
material, not proof for the current run.

Useful false-positive controls:

- Malformed, mixed-projection, wildcard, or too-many-values `If-None-Match`
  requests fail open to query routing and are not counted as Gate 1 `304`.
- Wrong-tenant or missing-permission request is denied and is not counted as a
  successful latency sample.
- Stale `evidence_run_id` or stale source commit is rejected.
- Cache-hit claim is rejected without stable query identity and matching ETag.
- p99 claim is rejected when sample count or raw sample artifact is missing.
- Mixed cold/warm populations (e.g., `cache-miss-cold-actor` mixed with
  `cache-miss-after-etag-mismatch`) are rejected.
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
| `QueriesController` Gate 1 logs | `ETagPreCheckMatch`, `ETagPreCheckMiss`, `ETagPreCheckFailed`, `ETagDecodeSkipped`, `MixedProjectionTypesSkipped`, and `TooManyIfNoneMatchValues` logs. | Outcome evidence exists; dedicated elapsed timing is deferred. |
| HTTP status and headers | `304 NotModified`, `200 OK`, ETag headers, and request route/status are observable. | HTTP duration alone does not isolate ETag lookup, Gate 2, or projection execution; ASP.NET Core duration is tagged by route and status only. |
| `CachingProjectionActor` logs | `CacheHit`, `CacheMiss`, `CacheSkipped`, projection-type discovery, and mismatch logs. | Actor elapsed timing and raw sample export are deferred; in-actor `ETagFetchFailed` produces a `CacheSkipped` path that is distinct from cache-hit and cache-miss. |
| `ETagActor` logs | ETag regeneration, state load, cold start, old-format migration, and load/migration failures. | Regeneration and lookup elapsed timing require extra instrumentation. |
| `DaprETagService` logs | ETag actor fetch failure is logged with fail-open classification. | Successful lookup elapsed timing is deferred. |
| Aspire logs and traces | Resource state, console logs, structured logs, and traces can link run behavior. | Missing query-stage tags may prevent claimable boundary isolation; non-Aspire deploys do not produce these. |
| ServiceDefaults OpenTelemetry | Registers ASP.NET Core, HTTP client, runtime metrics, JSON console logs, the application name source, `Hexalith.EventStore` (the `ActivitySource` used by `Hexalith.EventStore.Server` actors and services via `EventStoreActivitySource`), `Microsoft.AspNetCore.SignalR.Server`, and `Microsoft.AspNetCore.SignalR.Client`. | Dedicated query histograms and raw sample export are not present in this story; query-stage Activity tags and per-stage Activity sources are not registered. |

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
boundaries, samples, cache state, and reviewer verdict. When a single proof
claims both SignalR delivery and query refresh, both schemas apply additively;
controls and reviewer verdicts must be recorded for each schema.

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
| Any required metadata field missing (`schema_version`, `evidence_run_id`, `proof_key`, `source_commit`, `generated_at`, `generated_by`, `reviewed_by`, `repository_status`, `safe_tenant_alias`, `safe_domain_alias`, `safe_projection_alias`, `safe_query_alias`, `final_classification`). | `not-claimable` |
| Cache state is not declared. | `not-claimable` |
| Boundary labels collapse Gate 1, Gate 2, and cache miss timing. | `diagnostic-only` |
| p99 uses fewer than 100 valid post-warmup samples. | `sample-only` |
| Raw samples or calculation command are missing for p99. | `not-claimable` |
| p99 computed from `client-observed-duration` without per-marker clock source and correlation-continuity assumptions stated. | `diagnostic-only` |
| Throughput omits concurrency, response mix, error rate, timeout rate, replica count, or retry treatment. | `not-claimable` |
| Throughput run records any non-2xx response or any timeout. | `not-claimable` |
| Throughput claim includes retries in the achieved-rps total. | `sample-only` |
| False-positive control is absent or not from the same run / linked control run. | `not-claimable` |
| Correlation-integrity control is absent or not from the same run / linked control run. | `not-claimable` |
| Cross-process timing lacks per-marker clock source or correlation-continuity statement. | `diagnostic-only` |
| Dedicated instrumentation missing for the claimed boundary. | `instrumentation-gap` |
| Both `not-claimable` and `instrumentation-gap` rules apply. | `instrumentation-gap` when the missing capability is product-side; `not-claimable` when the gap is run-author hygiene. |
| `claim type` is `p99` or `throughput` and NFR field is missing. | `not-claimable` |
| Reviewer marks SignalR-specific mandatory fields excluded as `no`. | `not-claimable` |
| Secrets or unsafe identifiers are present. | `not-claimable` until redacted |
