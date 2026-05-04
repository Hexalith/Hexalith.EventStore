# Post-Epic-9 R9-A8: Query Operational Evidence Pattern

Status: done

<!-- Source: epic-9-retro-2026-04-30.md - R9-A8 -->
<!-- Source: post-epic-10-r10a8-r9-r10-follow-through-tracking.md - R9/R10 reconciliation -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a QA-focused query-pipeline maintainer,
I want a repeatable operational evidence pattern for query latency and cache behavior,
so that NFR35, NFR36, NFR37, and NFR39 claims are backed by measurable evidence instead of story-level assertions.

## Story Context

Epic 9 delivered the query pipeline, self-routing ETags, ETag actors, projection-change invalidation, and query actor in-memory caching. Its retrospective still left R9-A8 open because the query latency NFRs are mostly asserted by focused tests and architecture records rather than a shared operational evidence pattern.

R10-A8 confirmed this gap after inspecting the NFR assessment, traceability report, SignalR evidence pattern, and query docs. SignalR now has a reusable evidence pattern, but query-specific evidence for the ETag pre-check, query actor cache hit, query actor cache miss, and query throughput remains separate work.

This story defines the evidence contract and reusable template for query pipeline operational proof. It is not a perf-lab implementation story, not another stale-ETag proof, and not another Aspire topology proof. R9-A1 owns stale HTTP validator behavior after projection changes. R9-A2 owns the cold-query through invalidation and re-warm topology proof. R9-A8 owns the durable evidence schema, latency boundaries, classification rules, sample rules, redaction guidance, and routing for missing instrumentation.

Current HEAD at story creation: `b9f52e7`.

## Acceptance Criteria

1. **Query operational evidence documentation exists.** Add `docs/operations/query-operational-evidence.md` or an equivalent operations document that defines how to collect and review query pipeline evidence for NFR35, NFR36, NFR37, and NFR39. The document must link back to the source requirements in `_bmad-output/planning-artifacts/prd.md`, the R9-A8 action in `epic-9-retro-2026-04-30.md`, the R10-A8 follow-through row, the SignalR evidence pattern/template, query API docs, `QueriesController`, `CachingProjectionActor`, `ETagActor`, `DaprETagService`, and ServiceDefaults. It must explain what each source contributes.

2. **A reusable evidence template exists.** Add `_bmad-output/test-artifacts/query-operational-evidence-template.md` with a schema version such as `query-operational-evidence/v1`. The template must require run identity, environment, topology, query identity, authorization mode, cache-state setup, latency calculation, controls, diagnostics, redaction, deferred follow-up, reviewer verdict, and final classification sections. It must name required metadata fields including `schema_version`, `evidence_run_id`, `proof_key`, `source_commit`, `generated_at`, `reviewed_by`, and safe tenant/domain aliases.

3. **Latency boundaries are named precisely.** The pattern distinguishes at least these intervals: request received to Gate 1 decision, ETag actor lookup, Gate 1 `304 NotModified` response, query actor invocation, Gate 2 cache-hit response, projection/domain-service query execution, cache refresh, HTTP response completed, and optional client-observed duration. Do not collapse all timings into one ambiguous "query latency" value. The operations document or template must include a canonical measurement-boundary table with a stable label, start marker, stop marker, required correlation fields, clock source, applicable cache state, and whether the boundary is currently observable or a deferred instrumentation gap. Cross-process timing must be rejected or classified as diagnostic-only unless the clock and correlation assumptions are stated.

4. **NFR thresholds and claim rules are explicit.** The pattern cites NFR35 warm ETag pre-check p99 <= 5ms, NFR36 cache hit p99 <= 10ms, NFR37 cache miss p99 <= 200ms, and NFR39 >= 1,000 concurrent query requests per second per EventStore instance. It must state when a run is `path-viability`, `sample-only`, `diagnostic-only`, `not-claimable`, or a valid p99/throughput claim, and must avoid wording that implies current product compliance without evidence.

5. **p99 and throughput claims are falsifiable.** A p99 claim must record sample count, warmup rule, sample window, clock source, nearest-rank percentile method, raw sample artifact location, calculation command or worksheet, threshold source, excluded samples, and whether the calculation is per instance, endpoint, tenant, projection, or aggregate. Fewer than 100 valid post-warmup samples must not be labeled p99. A throughput claim must record concurrent in-flight requests, achieved requests per second, latency budget outcome, error rate, duration, instance identity, replica count, response mix, saturation signals, and whether retries were included. Missing raw samples, missing calculation evidence, mixed clock sources, or unstated retry treatment force `not-claimable`.

6. **Cache-state setup is part of the evidence.** The template requires evidence for cold baseline, warm same-validator `304`, Gate 2 cache-hit path when Gate 1 is intentionally bypassed or not applicable, cache miss after ETag mismatch, and cache refresh/re-warm when the proof shape includes invalidation. The pattern must explain when a phase belongs to R9-A1 or R9-A2 instead of R9-A8.

7. **Controls prevent false positives.** Every evidence run must include at least one false-positive control and one correlation-integrity control from the same run or a clearly linked control run. Examples include malformed or mixed-projection `If-None-Match` values failing open, wrong-tenant authorization denial, stale evidence-run id rejection, cache-hit claims rejected without stable query identity, p99 claims rejected when the sample count or raw sample artifact is missing, mixed cold/warm populations, unrelated background load, stale ETag evidence, invalidation race evidence, missing projection evidence, DAPR actor failure, and domain-service timeout. Controls reused from another story, tenant, projection, or environment must be marked as reference material, not proof.

8. **Diagnostics and instrumentation gaps are routed honestly.** The pattern must list currently available evidence sources such as `QueriesController` Gate 1 logs, `CachingProjectionActor` `CacheHit`/`CacheMiss`/`CacheSkipped` logs, `ETagActor` regeneration logs, HTTP status/headers, Aspire logs/traces, and OpenTelemetry service defaults. Missing dedicated query latency metrics, histogram instruments, raw-sample export, evidence-schema validation, or trace tags must be recorded as deferred instrumentation rather than silently treated as proof. Manual stopwatch timing, screenshots alone, or narrative-only observations cannot satisfy an NFR timing claim.

9. **Safe redaction and storage rules are defined.** Evidence runs must live under `_bmad-output/test-artifacts/<story-or-proof-key>/` with an index and dated evidence files. The pattern must forbid committed bearer tokens, connection strings, production hostnames, raw payloads containing customer data, raw HAR files with secrets, and tenant/user identifiers that are not safe aliases.

10. **Existing SignalR evidence guidance is reused without scope bleed.** The query pattern may mirror the structure of `docs/operations/signalr-operational-evidence.md` and `_bmad-output/test-artifacts/signalr-operational-evidence-template.md`, but it must not copy SignalR-specific fields such as hub group join, broadcast origin, Redis backplane, client receipt, reconnect lifecycle, fanout, subscription, or transport wording as mandatory query evidence.

11. **No product behavior changes are made by default.** This story is expected to change only `docs/operations/query-operational-evidence.md`, `_bmad-output/test-artifacts/query-operational-evidence-template.md`, and story/sprint bookkeeping. Product code, test harness, telemetry implementation, query routing, ETag matching, cache semantics, authorization, DAPR fail-open behavior, benchmarks, and CI changes are out of scope unless Jerome explicitly expands the story. Missing metrics, spans, tags, raw-sample export, or validation automation must be recorded as deferred instrumentation rather than implemented here.

12. **Validation is appropriate for docs/evidence.** Run targeted markdown validation for the new operations document and template. Validate source-reference coverage, required schema fields, required sections, reviewer-verdict classifications, SignalR-specific exclusions, claim rules, redaction rules, and deferred instrumentation inventory. Include at least one intentionally invalid sample or checklist row showing why evidence is rejected. If only docs and BMAD evidence templates change, product tests are not required. If code, tests, or workflow files change unexpectedly, run the affected focused test slice and record why it was necessary.

13. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and `last_updated` names the query operational evidence result. At code-review signoff, both become `done` only after the documentation, template, and validation evidence are recorded.

## Scope Boundaries

- Do not implement a full load-testing framework, perf-lab CI stage, k6 suite, NBomber suite, or BenchmarkDotNet project in this story.
- Do not re-prove R9-A1 stale-validator behavior or R9-A2 Aspire query-cache topology behavior.
- Do not change `QueriesController`, `CachingProjectionActor`, `ETagActor`, `DaprETagService`, query contracts, telemetry code, actor behavior, authorization behavior, or DAPR behavior unless Jerome explicitly expands this story.
- Do not weaken existing query/ETag tests or replace functional proof with timing-only evidence.
- Do not claim p99 or 1,000 qps readiness from single-run manual evidence.
- Do not select, install, or wire a load-testing tool; the evidence pattern may name acceptable input fields, but perf-lab tooling remains follow-up work.
- Do not initialize or update nested submodules.
- Do not edit generated preflight JSON audit files.

## Implementation Inventory

| Area | File / artifact | Expected use |
|---|---|---|
| Source requirement | `_bmad-output/planning-artifacts/prd.md` | NFR35, NFR36, NFR37, and NFR39 threshold text |
| Epic source | `_bmad-output/implementation-artifacts/epic-9-retro-2026-04-30.md` | R9-A8 action text and retrospective rationale |
| Follow-through source | `_bmad-output/implementation-artifacts/post-epic-10-r10a8-r9-r10-follow-through-tracking.md` | R9-A8 disposition and evidence inspected |
| Existing pattern | `docs/operations/signalr-operational-evidence.md` | Structural model for evidence storage, classification, controls, redaction |
| Existing template | `_bmad-output/test-artifacts/signalr-operational-evidence-template.md` | Template shape to mirror, excluding SignalR-specific fields |
| Query API docs | `docs/reference/query-api.md` | Public query endpoint, ETag, `If-None-Match`, and SignalR handoff boundaries |
| Gate 1 implementation | `src/Hexalith.EventStore/Controllers/QueriesController.cs` | ETag pre-check logs and fail-open behavior |
| Gate 2 implementation | `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs` | `CacheHit`, `CacheMiss`, `CacheSkipped`, projection-type discovery logs |
| ETag state | `src/Hexalith.EventStore.Server/Actors/ETagActor.cs` | Self-routing ETag regeneration and persistence behavior |
| ETag lookup | `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs` | ETag actor lookup and fail-open behavior |
| Service telemetry | `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` | Current OpenTelemetry/logging registration |

## Tasks / Subtasks

- [x] Task 0: Baseline the evidence-pattern scope (AC: #1, #4, #10)
    - [x] 0.1 Re-read NFR35, NFR36, NFR37, and NFR39 in `prd.md`.
    - [x] 0.2 Re-read R9-A8 in the Epic 9 retro and R10-A8 reconciliation table.
    - [x] 0.3 Confirm R9-A1/R9-A2 remain separate and do not pull their proof sequences into this story.

- [x] Task 1: Add the operations documentation (AC: #1, #3, #4, #6, #8, #9)
    - [x] 1.1 Create `docs/operations/query-operational-evidence.md`.
    - [x] 1.2 Define evidence storage, mandatory artifacts, latency boundaries, p99/throughput rules, diagnostics, failure classification, controls, redaction, and deferred instrumentation.
    - [x] 1.3 Cite source requirement files and implementation files by path.
    - [x] 1.4 Include a source inventory section explaining what each source contributes.
    - [x] 1.5 Include examples of acceptable and unacceptable NFR claims.

- [x] Task 2: Add the reusable evidence template (AC: #2, #5, #6, #7, #9)
    - [x] 2.1 Create `_bmad-output/test-artifacts/query-operational-evidence-template.md`.
    - [x] 2.2 Include required field placeholders and allowed run classifications.
    - [x] 2.3 Include an intentionally invalid example that rejects stale or uncorrelated evidence.
    - [x] 2.4 Keep the template schema-first: metadata, environment, scenario matrix, measurements, p99 inputs, throughput inputs, diagnostics references, exclusions, redaction statement, and reviewer verdict.
    - [x] 2.5 Add a fail-closed reviewer checklist so missing required fields produce `not-claimable`, `instrumentation-gap`, or `inconclusive` instead of an implied pass.

- [x] Task 3: Define query-specific measurement semantics (AC: #3, #4, #5, #6)
    - [x] 3.1 Separate Gate 1 `304` timing from Gate 2 query actor cache-hit timing.
    - [x] 3.2 Explain how a cache miss differs from projection/domain-service execution time.
    - [x] 3.3 State that single path-viability runs may support diagnostics but not p99 or throughput claims.
    - [x] 3.4 Require raw sample storage or metrics links before p99/throughput classification can be `pass`.
    - [x] 3.5 Define start/stop markers and correlation fields for each named boundary to prevent double-counting.
    - [x] 3.6 State which boundaries can use server-side monotonic timing, which may use trace timestamps, and which must remain diagnostic-only until dedicated instrumentation exists.

- [x] Task 4: Capture diagnostics and routing gaps (AC: #7, #8, #11)
    - [x] 4.1 Inventory existing structured logs and traces usable for query evidence.
    - [x] 4.2 Add a deferred instrumentation table for missing query histograms, query-stage Activity tags, raw-sample export, or evidence-schema validation.
    - [x] 4.3 Route load-harness or perf-lab implementation to follow-up work instead of implementing it here.
    - [x] 4.4 Define rejection criteria for mixed cold/warm populations, unrelated background load, missing correlation, and missing raw samples.
    - [x] 4.5 Separate reusable reference evidence from proof evidence tied to the same run id, tenant/domain alias, projection, environment, and source commit.

- [x] Task 5: Validate and close bookkeeping (AC: #12, #13)
    - [x] 5.1 Run markdown validation for the new operations doc, template, and this story artifact.
    - [x] 5.2 If links are added, run a targeted link check or record why it was unavailable.
    - [x] 5.3 Update this story's Dev Agent Record, File List, Change Log, Verification Status, and sprint-status row at dev handoff.
    - [x] 5.4 Record a docs-only validation checklist covering source links, required sections, schema fields, claim rules, SignalR exclusions, redaction, and deferred instrumentation.

### Review Findings

Code review on commit `d7b15e1` performed via `bmad-code-review` (Blind Hunter + Edge Case Hunter + Acceptance Auditor). Acceptance Auditor verdict: APPROVED-AS-IS — all 13 ACs and 8 scope boundaries pass. Adversarial layers raised content-quality findings on the docs/template (treated as the product per Party-Mode guidance).

#### Decision-needed

- [x] `[Review][Decision]` **Classification for redaction-rule violation** — Resolved: chose `not-claimable until redacted` (option 1). Consistent with other hygiene rows; `product-failure` overload removed. Applied in both ops-doc Reviewer Checklist and template Fail-Closed Reviewer Checklist.
- [x] `[Review][Decision]` **Throughput partial-failure tolerance** — Resolved: chose zero non-2xx and zero timeouts → `not-claimable` (option 1). Strictest, falsifiable. Applied in ops-doc Throughput Claims and template Throughput Inputs / Fail-Closed Reviewer Checklist.
- [x] `[Review][Decision]` **Precedence between `not-claimable` and `instrumentation-gap`** — Resolved: `instrumentation-gap` dominates when missing capability is product-side; `not-claimable` dominates when run-author hygiene (option 1). Routes blame correctly. Applied in ops-doc Allowed run classifications + Reviewer Checklist and template Fail-Closed Reviewer Checklist; full precedence ladder added to ops-doc.

#### Patches

- [x] `[Review][Patch]` **Sprint-status `last_updated` claims `in-progress -> review` but prior was `ready-for-dev`** [`_bmad-output/implementation-artifacts/sprint-status.yaml:31`]
- [x] `[Review][Patch]` **R9-A5 dev-handoff attribution clobbered without preservation footer (AC #12 pattern)** [`_bmad-output/implementation-artifacts/sprint-status.yaml:31`]
- [x] `[Review][Patch]` **Wildcard `If-None-Match: *` not represented in cache-state setup matrix** [`docs/operations/query-operational-evidence.md` Cache-State Setup, `_bmad-output/test-artifacts/query-operational-evidence-template.md` Cache-State Setup]
- [x] `[Review][Patch]` **Mixed-projection `If-None-Match` mis-described as "Gate 1 skipped" — controller does NOT set `gate1Skipped` and emits both `MixedProjectionTypesSkipped` and `ETagPreCheckMiss(false,true)`** [`docs/operations/query-operational-evidence.md` Cache-State Setup + Controls bullets]
- [x] `[Review][Patch]` **Latency boundary `etag-actor-lookup` does not account for the post-mediator second `eTagService.GetCurrentETagAsync` call (controller line 139)** [`docs/operations/query-operational-evidence.md` Latency Boundaries, `_bmad-output/test-artifacts/query-operational-evidence-template.md` Measurement Boundaries]
- [x] `[Review][Patch]` **`gate1-fail-open` (ETagService throws → `currentETag = null`) missing from boundary table and cache-state matrix** [`docs/operations/query-operational-evidence.md` Latency Boundaries + Cache-State Setup]
- [x] `[Review][Patch]` **`TooManyIfNoneMatchValues` (>10 comma-separated values) not surfaced in diagnostics inventory or cache-state matrix** [`docs/operations/query-operational-evidence.md` Diagnostics And Instrumentation]
- [x] `[Review][Patch]` **`gate2-cache-skipped` (in-actor `DaprETagService` fail-open returns null) not classified as a distinct cache state** [`docs/operations/query-operational-evidence.md` Cache-State Setup, template Cache-State Setup]
- [x] `[Review][Patch]` **`projection-type-discovery-bypass` path (`CachingProjectionActor` returns uncached when ETag was fetched with wrong projection type) unclassified** [`docs/operations/query-operational-evidence.md` Cache-State Setup]
- [x] `[Review][Patch]` **`gate1-304-response` boundary marked "Partially observable" via `ETagPreCheckMatch` log, but that log carries no timing — should be deferred** [`docs/operations/query-operational-evidence.md` Latency Boundaries row 3]
- [x] `[Review][Patch]` **NFR35 cold-actor activation caveat from PRD (line 967) dropped from threshold table** [`docs/operations/query-operational-evidence.md` NFR Thresholds NFR35 row]
- [x] `[Review][Patch]` **Retry treatment for throughput is recordable but rule for "retries included in achieved-rps total" not classified** [`docs/operations/query-operational-evidence.md` Throughput Claims]
- [x] `[Review][Patch]` **p99 nearest-rank formula uses `valid_sample_count` but template example uses `sample_count: 1` with no `valid_sample_count` field** [`_bmad-output/test-artifacts/query-operational-evidence-template.md` p99 Inputs + Intentionally Invalid Example]
- [x] `[Review][Patch]` **`client-observed-duration` not explicitly forbidden as p99 source absent stated cross-process clock and correlation** [`docs/operations/query-operational-evidence.md` Reviewer Checklist, `_bmad-output/test-artifacts/query-operational-evidence-template.md` Fail-Closed Reviewer Checklist]
- [x] `[Review][Patch]` **Required-fields list, YAML schema, and reviewer checklist disagree on which template fields are required (`generated_by`, `safe_projection_alias`, `safe_query_alias`, `final_classification` declared `<required>` in YAML but missing from required-fields bullet and fail-closed rows)** [`_bmad-output/test-artifacts/query-operational-evidence-template.md` Field Reference + Metadata YAML + Fail-Closed Reviewer Checklist]
- [x] `[Review][Patch]` **Reviewer-checklist drift between ops-doc and template (e.g., template requires `concurrency` while ops-doc omits it; rename `retries` ↔ `retry treatment`)** [`docs/operations/query-operational-evidence.md` Reviewer Checklist, `_bmad-output/test-artifacts/query-operational-evidence-template.md` Fail-Closed Reviewer Checklist]
- [x] `[Review][Patch]` **Final-classification taxonomy conflates run-level classifications with claim types — `p99` and `throughput` listed as claim types but missing from run classifications, and `path-viability`/`sample-only` reused across both axes** [`_bmad-output/test-artifacts/query-operational-evidence-template.md` Allowed run classifications + Latency Calculation claim type]
- [x] `[Review][Patch]` **NFR39 phrasing "≥ 1,000 concurrent query requests per second" is unit-incoherent — separate concurrent in-flight from achieved rps** [`docs/operations/query-operational-evidence.md` NFR Thresholds NFR39 row]
- [x] `[Review][Patch]` **NFR35 description "without activating the query actor" risks conflating ETag actor and query actor; add note that the post-mediator ETag-set call is in NFR36/37 windows, not NFR35** [`docs/operations/query-operational-evidence.md` NFR Thresholds + warm-same-validator-304 row]
- [x] `[Review][Patch]` **`Stable query payload identity` field referenced but format undefined (hash? canonical JSON? digest algorithm?)** [`docs/operations/query-operational-evidence.md` Mandatory Artifacts, `_bmad-output/test-artifacts/query-operational-evidence-template.md` Query Identity And Authorization]
- [x] `[Review][Patch]` **Cross-process timing rule "lacks clock and correlation assumptions" is unfalsifiable — specify minimum required level (per-marker clock source named explicitly)** [`docs/operations/query-operational-evidence.md` Reviewer Checklist, template Fail-Closed Reviewer Checklist]
- [x] `[Review][Patch]` **"Reuse from another story/tenant/projection/environment" rule has no enforcement checklist row in either document** [`docs/operations/query-operational-evidence.md` Controls + Reviewer Checklist, template Controls + Fail-Closed Reviewer Checklist]
- [x] `[Review][Patch]` **`<NFR35\|NFR36\|NFR37\|NFR39>` inside backtick code spans renders the literal `\|` per CommonMark — drop backticks or restructure** [`_bmad-output/test-artifacts/query-operational-evidence-template.md` Scenario Matrix + Latency Calculation claim type]
- [x] `[Review][Patch]` **`reviewed_by` flagged `<required before final verdict>` in YAML but listed as unconditional in required-fields bullet and fail-closed checklist** [`_bmad-output/test-artifacts/query-operational-evidence-template.md` Metadata + Field Reference + Fail-Closed Reviewer Checklist]
- [x] `[Review][Patch]` **`repository_status: <clean|dirty|not-recorded>` — `not-recorded` lacks any fail-closed downgrade row** [`_bmad-output/test-artifacts/query-operational-evidence-template.md` Metadata + Fail-Closed Reviewer Checklist]
- [x] `[Review][Patch]` **Latency Calculation NFR claim enum allows `not-applicable` — schema-weakening, no fail-closed for "claim type p99 with no NFR cited"** [`_bmad-output/test-artifacts/query-operational-evidence-template.md` Latency Calculation]
- [x] `[Review][Patch]` **Intentionally invalid example uses `safe_domain_alias: counter` (real sample-domain name) — violates the doc's own safe-alias convention** [`_bmad-output/test-artifacts/query-operational-evidence-template.md` Intentionally Invalid Example]
- [x] `[Review][Patch]` **Intentionally invalid example rejection rationale does not mention that missing required metadata alone forces `not-claimable`** [`_bmad-output/test-artifacts/query-operational-evidence-template.md` Intentionally Invalid Example]
- [x] `[Review][Patch]` **`cache-miss-after-etag-mismatch` collapses two distinct paths (ETag mismatch vs cold actor cache) — risks mixed populations the doc itself warns against** [`docs/operations/query-operational-evidence.md` Cache-State Setup, template Cache-State Setup]
- [x] `[Review][Patch]` **"SignalR-specific mandatory fields excluded: `<yes|no>`" Reviewer Verdict row has no fail-closed downgrade for a `no` answer** [`_bmad-output/test-artifacts/query-operational-evidence-template.md` Reviewer Verdict + Fail-Closed Reviewer Checklist]
- [x] `[Review][Patch]` **`http-response-completed` boundary cannot disambiguate Gate 1 / Gate 2 / cache-miss with route-tagged ASP.NET duration metric — add caveat** [`docs/operations/query-operational-evidence.md` Latency Boundaries row 8]
- [x] `[Review][Patch]` **Story Change Log version bumped from 0.2 → 1.0 — verify intent (story file versions are typically incremental like 0.3); does not collide with semantic-release but worth confirming** [`_bmad-output/implementation-artifacts/post-epic-9-r9a8-query-operational-evidence-pattern.md:248`]

#### Deferred

- [x] `[Review][Defer]` **Two duplicated reviewer checklists in ops-doc and template create drift risk** [`docs/operations/query-operational-evidence.md` + `_bmad-output/test-artifacts/query-operational-evidence-template.md`] — deferred, structural concern beyond R9-A8 scope
- [x] `[Review][Defer]` **Aspire-specific fields (apphost state, console logs, structured logs) required for every run, conflating test rig with product** [`_bmad-output/test-artifacts/query-operational-evidence-template.md` Topology + Diagnostics References] — deferred, template extensibility concern
- [x] `[Review][Defer]` **Final-classification YAML schema is the only constrained surface; downstream Reviewer Verdict + Final Classification headings are free-text** [`_bmad-output/test-artifacts/query-operational-evidence-template.md`] — deferred, requires validator implementation (listed as deferred instrumentation already)
- [x] `[Review][Defer]` **Title Case headings ("Query Identity And Authorization", "Deferred Instrumentation Or Follow-Up") inconsistent with sentence-case neighbors** [`_bmad-output/test-artifacts/query-operational-evidence-template.md`] — deferred, cosmetic
- [x] `[Review][Defer]` **Evidence Index `<story-or-proof-key>` placeholders not flagged with explicit "REPLACE" marker** [`_bmad-output/test-artifacts/query-operational-evidence-template.md` Evidence Storage] — deferred, defensive
- [x] `[Review][Defer]` **`Authorization mode` is a stand-alone Mandatory Artifacts row but template merges it into Query Identity And Authorization** [`docs/operations/query-operational-evidence.md` Mandatory Artifacts vs template] — deferred, taxonomy alignment
- [x] `[Review][Defer]` **Scenario matrix uses three placeholder formats (`<scenario-id>`, `<required>`, bare colon)** [`_bmad-output/test-artifacts/query-operational-evidence-template.md`] — deferred, cosmetic
- [x] `[Review][Defer]` **SignalR-exclusion check is asymmetric — does not address combined SignalR + query refresh proofs** [`docs/operations/query-operational-evidence.md` SignalR Pattern Reuse] — deferred, cross-doc concern

## Dev Notes

### Architecture Guardrails

- The query endpoint is the public proof boundary. Prefer HTTP status, ETag header, structured logs, traces, and metrics over private actor-state inspection.
- Gate 1 lives in `QueriesController`. It decodes self-routing `If-None-Match` values, skips unsafe mixed-projection headers, fetches the current projection ETag, returns `304` only on match, and fails open to query routing when lookup is unavailable.
- Gate 2 lives in `CachingProjectionActor`. It can return cached payload bytes only when a non-null current ETag matches the actor's cached ETag and cached payload bytes exist. Null ETags skip cache claims.
- ETag actor IDs use `{projectionType}:{tenantId}`. Query actor IDs are separate and use query/projection identity plus entity or payload routing. Do not conflate these in evidence.
- Query auth uses JWT tenant/domain claims and query permissions. Wrong-tenant or missing-permission evidence belongs in controls, not in successful latency samples.
- OpenTelemetry service defaults currently add ASP.NET Core, HTTP client, runtime metrics, the application name source, and `Hexalith.EventStore`. Dedicated query-stage histograms are not proven by this story until implemented separately.

### Evidence Classification

Use these run-level classifications unless the operations document deliberately revises them:

- `pass`: required evidence is present, controls pass, correlation is continuous, and the claim rules for p99 or throughput are satisfied.
- `diagnostic-only`: evidence helps investigation but lacks the sample rules, correlation, or boundary isolation required for an NFR claim.
- `not-claimable`: evidence is present but must be rejected for the stated NFR claim because required proof fields are missing or invalid.
- `product-failure`: query response, ETag, cache behavior, authorization, or latency outcome violates the expected contract.
- `environment-blocker`: Docker, DAPR, Aspire, auth setup, observability backend, or load harness is unavailable.
- `instrumentation-gap`: the product may work, but timestamps, metrics, raw samples, correlation fields, or schema validation are insufficient for the claimed evidence.
- `sample-only`: a bounded run proves path viability but lacks the sample count, clock discipline, or metrics needed for p99/throughput.
- `inconclusive`: required artifacts exist, but a non-instrumentation precondition or control cannot be confirmed.

Reviewer verdicts must fail closed. A reviewer may downgrade a run from `pass` to `diagnostic-only`, `not-claimable`, `instrumentation-gap`, or `inconclusive` when required metadata, correlation, raw samples, controls, or clock assumptions are missing. The pattern should make that downgrade mechanical enough that another maintainer can reproduce the verdict from the artifacts.

### Party-Mode Review Guidance

- Treat the evidence pattern itself as the product: reviewers must be able to reject invalid NFR evidence without relying on unstated interpretation.
- Required correlation fields should include, where available, evidence-run id, trace/correlation id, instance id, tenant id or safe alias, route name, query/projection identity, HTTP status, ETag outcome, cache outcome, and actor invocation identifiers.
- The operations document should distinguish `claimable`, `not-claimable`, and `diagnostic-only` evidence and avoid implying that NFR35, NFR36, NFR37, or NFR39 are already satisfied.
- If a latency boundary cannot currently be observed directly, label it as a deferred instrumentation gap with proposed owner/follow-up rather than substituting an ambiguous proxy measurement.
- Reuse the SignalR evidence structure for traceability and review discipline only. Query docs must not inherit hub, reconnect, subscription, Redis backplane, client receipt, fanout, or transport-specific requirements.
- Avoid idioms and color-only status cues. Required evidence must not depend on screenshots alone; tables need clear column names and plain-language rejection reasons.

### Previous Story Intelligence

- R9-A1 is ready-for-dev and focuses on stale `If-None-Match` after projection change. Its story explicitly leaves query-cache topology and operational evidence to R9-A2/R9-A8.
- R9-A2 is ready-for-dev and focuses on the running Aspire query-cache topology sequence. It may generate evidence that uses the R9-A8 pattern later, but it should not define the whole operational schema.
- R9-A5 is ready-for-dev and is unrelated release-governance evidence work. Do not borrow its GitHub/governance scope.
- R10-A6 defined a SignalR operational evidence pattern with storage, mandatory artifacts, latency boundaries, p99 rules, controls, diagnostics, redaction, and deferred instrumentation. R9-A8 should reuse that evidence-writing discipline for query pipeline evidence.
- R10-A8 established the reusable follow-through rule: unresolved retrospective items need direct evidence, a visible owning story/status row, or an accepted non-action decision.

### Testing Standards

- Documentation and evidence-template changes should run targeted markdown validation:
    - `npx --yes markdownlint-cli2 "docs/operations/query-operational-evidence.md" "_bmad-output/test-artifacts/query-operational-evidence-template.md" "_bmad-output/implementation-artifacts/post-epic-9-r9a8-query-operational-evidence-pattern.md"`
- Product tests are not required for documentation/template-only work.
- If product code changes are made for instrumentation, run the affected query/ETag/cache unit slice and explain why docs-only scope was insufficient.
- If a new validation script is added for evidence templates, include a positive and negative sample so missing required fields fail mechanically.

### Advanced Elicitation Guidance

- Treat `evidence_run_id`, `source_commit`, `query/projection identity`, safe tenant/domain aliases, trace/correlation id, and environment identity as the minimum chain of custody for claimable evidence.
- Require raw sample artifacts or metrics-query links for p99 and throughput claims; summary tables alone are insufficient.
- Any acceptance wording that says "prove NFR compliance" must include the sample count, threshold source, percentile method, retry treatment, and reviewer verdict needed to falsify the claim.
- The docs-only implementation should not create a new validation tool by default, but the template must be structured so a future validator can check required fields without interpreting prose.

### Latest Technical Information

- Package versions are centrally pinned in `Directory.Packages.props`; do not add a new performance or observability package in this story without an explicit follow-up scope decision.
- The existing runtime stack already uses Aspire, DAPR actors, ASP.NET Core, xUnit v3, Shouldly, and OpenTelemetry service defaults. Reuse those concepts in the evidence pattern before naming a new tool.
- Load-testing tools such as k6, NBomber, or BenchmarkDotNet belong to the separate perf-lab implementation track unless Jerome explicitly expands this story.

### Project Structure Notes

- Keep the operator-facing pattern under `docs/operations/`.
- Keep reusable evidence templates under `_bmad-output/test-artifacts/`.
- Keep story bookkeeping in `_bmad-output/implementation-artifacts/post-epic-9-r9a8-query-operational-evidence-pattern.md` and `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- Keep future run evidence under `_bmad-output/test-artifacts/<story-or-proof-key>/`.
- Do not edit `.agents/skills/`, `.claude/skills/`, `_bmad/bmm/`, or the tools submodule for this story.

## References

- [Source: `_bmad-output/implementation-artifacts/epic-9-retro-2026-04-30.md#9-action-items`] - R9-A8 source action: define operational evidence for query latency NFRs.
- [Source: `_bmad-output/implementation-artifacts/epic-9-retro-2026-04-30.md#13-r9-follow-through-annotation-recorded-by-r10-a8-reconciliation`] - R9-A8 disposition and missing evidence summary.
- [Source: `_bmad-output/implementation-artifacts/post-epic-10-r10a8-r9-r10-follow-through-tracking.md`] - R9/R10 reconciliation table and evidence inspected for R9-A8.
- [Source: `_bmad-output/planning-artifacts/prd.md#query-pipeline-performance-current-release`] - NFR35, NFR36, NFR37, and NFR39 thresholds.
- [Source: `_bmad-output/test-artifacts/nfr-assessment.md`] - Performance and query-pipeline evidence classified as partial and routed to perf-lab work.
- [Source: `_bmad-output/test-artifacts/traceability-report.md`] - Query performance NFR coverage summary and missing NFR39 coverage.
- [Source: `docs/reference/query-api.md`] - Public Query API, ETag, `If-None-Match`, and projection-change behavior.
- [Source: `docs/operations/signalr-operational-evidence.md`] - Existing operational evidence pattern to mirror structurally.
- [Source: `_bmad-output/test-artifacts/signalr-operational-evidence-template.md`] - Existing evidence template shape to reuse.
- [Source: `src/Hexalith.EventStore/Controllers/QueriesController.cs`] - Gate 1 ETag pre-check behavior and structured logs.
- [Source: `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`] - Gate 2 cache behavior and structured logs.
- [Source: `src/Hexalith.EventStore.Server/Actors/ETagActor.cs`] - ETag actor persistence and regeneration behavior.
- [Source: `src/Hexalith.EventStore.Server/Queries/DaprETagService.cs`] - ETag lookup and fail-open behavior.
- [Source: `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs`] - Current OpenTelemetry and JSON logging registration.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex.

### Debug Log References

- `aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` baseline started and inspected with Aspire MCP `list_apphosts` and `list_resources`.
- `rg -n "NFR35|NFR36|NFR37|NFR39|Query Pipeline Performance" _bmad-output/planning-artifacts/prd.md`
- `rg -n "R9-A8|query latency|operational evidence" _bmad-output/implementation-artifacts/epic-9-retro-2026-04-30.md _bmad-output/implementation-artifacts/post-epic-10-r10a8-r9-r10-follow-through-tracking.md`
- `rg -n "ETag|If-None-Match|NotModified|Log|Cache|Query" src/Hexalith.EventStore/Controllers/QueriesController.cs`
- `rg -n "CacheHit|CacheMiss|CacheSkipped|ETag|Log|Query" src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`
- `rg -n "ETag|Regenerat|Log|Persist|Actor" src/Hexalith.EventStore.Server/Actors/ETagActor.cs`
- `rg -n "ETag|Get|fail|Log|Actor" src/Hexalith.EventStore.Server/Queries/DaprETagService.cs`
- `rg -n "OpenTelemetry|AddSource|Activity|Metrics|Console|Json" src/Hexalith.EventStore.ServiceDefaults/Extensions.cs`
- `npx --yes markdownlint-cli2 "docs/operations/query-operational-evidence.md" "_bmad-output/test-artifacts/query-operational-evidence-template.md" "_bmad-output/implementation-artifacts/post-epic-9-r9a8-query-operational-evidence-pattern.md"`
- `npx --yes markdown-link-check "docs/operations/query-operational-evidence.md" "_bmad-output/test-artifacts/query-operational-evidence-template.md"`

### Completion Notes List

- Added the query operational evidence pattern under `docs/operations/` with source inventory, evidence storage, mandatory artifacts, cache-state setup, canonical latency boundaries, NFR thresholds, p99 and throughput claim rules, controls, diagnostics inventory, deferred instrumentation, SignalR reuse boundaries, claim examples, and a fail-closed reviewer checklist.
- Added the reusable `query-operational-evidence/v1` template with required metadata, run/environment/topology/query identity sections, cache-state setup, scenario matrix, measurement boundaries, p99 and throughput inputs, controls, diagnostics, redaction, deferred follow-up, reviewer verdict, final classification, and an intentionally invalid rejection example.
- Kept implementation docs-only as required by AC #11; no product code, test harness, telemetry, query routing, ETag/cache behavior, CI, benchmark, or load-testing changes were made.
- Validation completed with markdownlint and targeted link checking. Product tests were not run because this story changed only documentation, BMAD evidence templates, and story/sprint bookkeeping.

### File List

- `docs/operations/query-operational-evidence.md`
- `_bmad-output/test-artifacts/query-operational-evidence-template.md`
- `_bmad-output/implementation-artifacts/post-epic-9-r9a8-query-operational-evidence-pattern.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-04 | 1.1 | Code review completed via `bmad-code-review` (Blind Hunter + Edge Case Hunter + Acceptance Auditor). Auditor verdict APPROVED-AS-IS. Adversarial layers raised 31 patches + 3 decisions on docs/template content (treated as the product). All 3 decisions resolved (redaction → `not-claimable`; throughput tolerance → zero non-2xx; classification precedence → product-side gap dominates). All 31 patches applied to `docs/operations/query-operational-evidence.md` (cache-state matrix + boundary table + classifications precedence + reviewer checklist + NFR threshold prose) and `_bmad-output/test-artifacts/query-operational-evidence-template.md` (required-fields list aligned with YAML schema, scenario matrix rendering fix, intentionally-invalid example fixes, fail-closed reviewer checklist alignment). 8 defers recorded in deferred-work.md. 13 dismissed as noise (including E18 false-positive on `Hexalith.EventStore` ActivitySource registration). Sprint-status R9-A8 row moved review → done; preceding R9-A5 attribution preserved per AC #12. | Claude Opus 4.7 (1M context) |
| 2026-05-04 | 1.0 | Implemented docs-only query operational evidence pattern, reusable template, validation, and dev-handoff bookkeeping. | GPT-5 Codex |
| 2026-05-04 | 0.2 | Applied party-mode review hardening for query evidence falsifiability and docs-only boundaries. | Codex automation |
| 2026-05-03 | 0.1 | Created ready-for-dev R9-A8 query operational evidence pattern story. | Codex automation |

## Verification Status

Docs-only validation passed (dev handoff) and re-validated after code-review patches.

- Markdown validation passed for the operations document, reusable template, and story artifact with `markdownlint-cli2` both at dev handoff and after code-review patches.
- Targeted link check passed for all links in the new operations document and reusable template with `markdown-link-check`.
- Source-reference coverage confirmed for PRD NFRs, R9-A8 retro, R10-A8 reconciliation, SignalR pattern/template, query API docs, `QueriesController`, `CachingProjectionActor`, `ETagActor`, `DaprETagService`, and ServiceDefaults.
- Required sections confirmed for schema metadata, environment, topology, query identity, authorization, cache-state setup, latency boundaries, p99 inputs, throughput inputs, diagnostics, redaction, deferred follow-up, reviewer verdict, and final classification.
- Claim rules confirmed for `path-viability`, `sample-only`, `diagnostic-only`, `not-claimable`, and valid p99/throughput claims; precedence rules added between `instrumentation-gap` and `not-claimable` after code review.
- SignalR-specific hub, broadcast, Redis backplane, client receipt, reconnect, fanout, subscription, and transport fields are not mandatory query evidence.
- Cache-state setup matrix expanded after code review to cover wildcard `If-None-Match: *`, mixed-projection fail-open, too-many-values fail-open, ETag-service-exception fail-open, in-actor cache-skipped, projection-type-discovery bypass, and cold-actor cache miss.
- Latency boundary table updated after code review: `etag-actor-lookup` split into pre-Gate-1 and post-mediator entries; `gate1-fail-open-response` and `gate2-cache-skipped-response` rows added; `http-response-completed` ASP.NET Core route/status caveat documented; `client-observed-duration` p99 use rejected unless cross-process clock and correlation are stated.
- Throughput rules tightened after code review: any non-2xx or timeout in the throughput window forces `not-claimable`; retries-included rps downgrades to `sample-only`.
- Source Inventory note clarified that `Hexalith.EventStore` ActivitySource (used by `Hexalith.EventStore.Server` actors via `EventStoreActivitySource.SourceName`) IS registered in `ServiceDefaults`; reviewer false-positive E18 dismissed.
- Product tests were not run because no product code, workflow, or test harness files changed.

## Party-Mode Review

- Date/time: 2026-05-04T09:05:42+02:00
- Selected story key: `post-epic-9-r9a8-query-operational-evidence-pattern`
- Command/skill invocation used: `/bmad-party-mode post-epic-9-r9a8-query-operational-evidence-pattern; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: tighten the docs-only boundary; make p99 and throughput claims rejectable; require canonical measurement-boundary labels, start/stop markers, and correlation fields; separate warm ETag, Gate 2 cache hit, cache miss, invalidation, and refresh evidence; prevent SignalR-specific template leakage; require operator-friendly schema and claim examples.
- Changes applied: updated acceptance criteria, scope boundaries, task checklist, evidence classifications, and dev notes with falsifiability, correlation, source inventory, validation, and SignalR-exclusion guidance.
- Findings deferred: runtime telemetry implementation, query-stage histograms, trace tag propagation, raw-sample export automation, load-test tooling, query routing changes, ETag/cache semantics changes, authorization changes, and DAPR behavior changes.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-04T13:03:35+02:00
- Selected story key: `post-epic-9-r9a8-query-operational-evidence-pattern`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-9-r9a8-query-operational-evidence-pattern`
- Batch 1 method names: Self-Consistency Validation; Red Team vs Blue Team; Security Audit Personas; Failure Mode Analysis; Comparative Analysis Matrix
- Reshuffled Batch 2 method names: Chaos Monkey Scenarios; Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Lessons Learned Extraction
- Findings summary: the story already had the right docs-only boundary, but evidence could still be falsely upgraded if metadata, clock source, raw samples, reusable controls, or reviewer verdict rules were implicit.
- Changes applied: tightened required template metadata, clock/correlation assumptions, p99 and throughput rejection rules, same-run control handling, deferred instrumentation routing, validation checklist expectations, fail-closed reviewer verdicts, and future validator readiness.
- Findings deferred: dedicated query metrics, query-stage Activity tags, raw-sample export, automated schema validation, load harness selection, product telemetry changes, and any claim that current NFR compliance is proven.
- Final recommendation: ready-for-dev
