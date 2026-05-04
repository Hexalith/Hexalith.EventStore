# Post-Epic-9 R9-A8: Query Operational Evidence Pattern

Status: ready-for-dev

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

2. **A reusable evidence template exists.** Add `_bmad-output/test-artifacts/query-operational-evidence-template.md` with a schema version such as `query-operational-evidence/v1`. The template must require run identity, environment, topology, query identity, authorization mode, cache-state setup, latency calculation, controls, diagnostics, redaction, deferred follow-up, and final classification sections.

3. **Latency boundaries are named precisely.** The pattern distinguishes at least these intervals: request received to Gate 1 decision, ETag actor lookup, Gate 1 `304 NotModified` response, query actor invocation, Gate 2 cache-hit response, projection/domain-service query execution, cache refresh, HTTP response completed, and optional client-observed duration. Do not collapse all timings into one ambiguous "query latency" value. The operations document or template must include a canonical measurement-boundary table with a stable label, start marker, stop marker, required correlation fields, applicable cache state, and whether the boundary is currently observable or a deferred instrumentation gap.

4. **NFR thresholds and claim rules are explicit.** The pattern cites NFR35 warm ETag pre-check p99 <= 5ms, NFR36 cache hit p99 <= 10ms, NFR37 cache miss p99 <= 200ms, and NFR39 >= 1,000 concurrent query requests per second per EventStore instance. It must state when a run is `path-viability`, `sample-only`, `diagnostic-only`, `not-claimable`, or a valid p99/throughput claim, and must avoid wording that implies current product compliance without evidence.

5. **p99 and throughput claims are falsifiable.** A p99 claim must record sample count, warmup rule, sample window, clock source, nearest-rank percentile method, raw sample artifact location, threshold source, excluded samples, and whether the calculation is per instance, endpoint, tenant, projection, or aggregate. Fewer than 100 valid post-warmup samples must not be labeled p99. A throughput claim must record concurrent in-flight requests, achieved requests per second, latency budget outcome, error rate, duration, instance identity, replica count, response mix, saturation signals, and whether retries were included.

6. **Cache-state setup is part of the evidence.** The template requires evidence for cold baseline, warm same-validator `304`, Gate 2 cache-hit path when Gate 1 is intentionally bypassed or not applicable, cache miss after ETag mismatch, and cache refresh/re-warm when the proof shape includes invalidation. The pattern must explain when a phase belongs to R9-A1 or R9-A2 instead of R9-A8.

7. **Controls prevent false positives.** Every evidence run must include at least one false-positive control and one correlation-integrity control. Examples include malformed or mixed-projection `If-None-Match` values failing open, wrong-tenant authorization denial, stale evidence-run id rejection, cache-hit claims rejected without stable query identity, p99 claims rejected when the sample count or raw sample artifact is missing, mixed cold/warm populations, unrelated background load, stale ETag evidence, invalidation race evidence, missing projection evidence, DAPR actor failure, and domain-service timeout.

8. **Diagnostics and instrumentation gaps are routed honestly.** The pattern must list currently available evidence sources such as `QueriesController` Gate 1 logs, `CachingProjectionActor` `CacheHit`/`CacheMiss`/`CacheSkipped` logs, `ETagActor` regeneration logs, HTTP status/headers, Aspire logs/traces, and OpenTelemetry service defaults. Missing dedicated query latency metrics, histogram instruments, or trace tags must be recorded as deferred instrumentation rather than silently treated as proof.

9. **Safe redaction and storage rules are defined.** Evidence runs must live under `_bmad-output/test-artifacts/<story-or-proof-key>/` with an index and dated evidence files. The pattern must forbid committed bearer tokens, connection strings, production hostnames, raw payloads containing customer data, raw HAR files with secrets, and tenant/user identifiers that are not safe aliases.

10. **Existing SignalR evidence guidance is reused without scope bleed.** The query pattern may mirror the structure of `docs/operations/signalr-operational-evidence.md` and `_bmad-output/test-artifacts/signalr-operational-evidence-template.md`, but it must not copy SignalR-specific fields such as hub group join, broadcast origin, Redis backplane, client receipt, reconnect lifecycle, fanout, subscription, or transport wording as mandatory query evidence.

11. **No product behavior changes are made by default.** This story is expected to change only `docs/operations/query-operational-evidence.md`, `_bmad-output/test-artifacts/query-operational-evidence-template.md`, and story/sprint bookkeeping. Product code, test harness, telemetry implementation, query routing, ETag matching, cache semantics, authorization, DAPR fail-open behavior, benchmarks, and CI changes are out of scope unless Jerome explicitly expands the story. Missing metrics, spans, tags, raw-sample export, or validation automation must be recorded as deferred instrumentation rather than implemented here.

12. **Validation is appropriate for docs/evidence.** Run targeted markdown validation for the new operations document and template. Validate source-reference coverage, required schema fields, required sections, SignalR-specific exclusions, claim rules, redaction rules, and deferred instrumentation inventory. If only docs and BMAD evidence templates change, product tests are not required. If code, tests, or workflow files change unexpectedly, run the affected focused test slice and record why it was necessary.

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

- [ ] Task 0: Baseline the evidence-pattern scope (AC: #1, #4, #10)
    - [ ] 0.1 Re-read NFR35, NFR36, NFR37, and NFR39 in `prd.md`.
    - [ ] 0.2 Re-read R9-A8 in the Epic 9 retro and R10-A8 reconciliation table.
    - [ ] 0.3 Confirm R9-A1/R9-A2 remain separate and do not pull their proof sequences into this story.

- [ ] Task 1: Add the operations documentation (AC: #1, #3, #4, #6, #8, #9)
    - [ ] 1.1 Create `docs/operations/query-operational-evidence.md`.
    - [ ] 1.2 Define evidence storage, mandatory artifacts, latency boundaries, p99/throughput rules, diagnostics, failure classification, controls, redaction, and deferred instrumentation.
    - [ ] 1.3 Cite source requirement files and implementation files by path.
    - [ ] 1.4 Include a source inventory section explaining what each source contributes.
    - [ ] 1.5 Include examples of acceptable and unacceptable NFR claims.

- [ ] Task 2: Add the reusable evidence template (AC: #2, #5, #6, #7, #9)
    - [ ] 2.1 Create `_bmad-output/test-artifacts/query-operational-evidence-template.md`.
    - [ ] 2.2 Include required field placeholders and allowed run classifications.
    - [ ] 2.3 Include an intentionally invalid example that rejects stale or uncorrelated evidence.
    - [ ] 2.4 Keep the template schema-first: metadata, environment, scenario matrix, measurements, p99 inputs, throughput inputs, diagnostics references, exclusions, redaction statement, and reviewer verdict.

- [ ] Task 3: Define query-specific measurement semantics (AC: #3, #4, #5, #6)
    - [ ] 3.1 Separate Gate 1 `304` timing from Gate 2 query actor cache-hit timing.
    - [ ] 3.2 Explain how a cache miss differs from projection/domain-service execution time.
    - [ ] 3.3 State that single path-viability runs may support diagnostics but not p99 or throughput claims.
    - [ ] 3.4 Require raw sample storage or metrics links before p99/throughput classification can be `pass`.
    - [ ] 3.5 Define start/stop markers and correlation fields for each named boundary to prevent double-counting.

- [ ] Task 4: Capture diagnostics and routing gaps (AC: #7, #8, #11)
    - [ ] 4.1 Inventory existing structured logs and traces usable for query evidence.
    - [ ] 4.2 Add a deferred instrumentation table for missing query histograms, query-stage Activity tags, raw-sample export, or evidence-schema validation.
    - [ ] 4.3 Route load-harness or perf-lab implementation to follow-up work instead of implementing it here.
    - [ ] 4.4 Define rejection criteria for mixed cold/warm populations, unrelated background load, missing correlation, and missing raw samples.

- [ ] Task 5: Validate and close bookkeeping (AC: #12, #13)
    - [ ] 5.1 Run markdown validation for the new operations doc, template, and this story artifact.
    - [ ] 5.2 If links are added, run a targeted link check or record why it was unavailable.
    - [ ] 5.3 Update this story's Dev Agent Record, File List, Change Log, Verification Status, and sprint-status row at dev handoff.
    - [ ] 5.4 Record a docs-only validation checklist covering source links, required sections, schema fields, claim rules, SignalR exclusions, redaction, and deferred instrumentation.

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

TBD by dev-story agent.

### Debug Log References

TBD by dev-story agent.

### Completion Notes List

TBD by dev-story agent.

### File List

TBD by dev-story agent.

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-04 | 0.2 | Applied party-mode review hardening for query evidence falsifiability and docs-only boundaries. | Codex automation |
| 2026-05-03 | 0.1 | Created ready-for-dev R9-A8 query operational evidence pattern story. | Codex automation |

## Verification Status

Story created for pre-development hardening. Implementation and validation are pending `bmad-dev-story`.

## Party-Mode Review

- Date/time: 2026-05-04T09:05:42+02:00
- Selected story key: `post-epic-9-r9a8-query-operational-evidence-pattern`
- Command/skill invocation used: `/bmad-party-mode post-epic-9-r9a8-query-operational-evidence-pattern; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: tighten the docs-only boundary; make p99 and throughput claims rejectable; require canonical measurement-boundary labels, start/stop markers, and correlation fields; separate warm ETag, Gate 2 cache hit, cache miss, invalidation, and refresh evidence; prevent SignalR-specific template leakage; require operator-friendly schema and claim examples.
- Changes applied: updated acceptance criteria, scope boundaries, task checklist, evidence classifications, and dev notes with falsifiability, correlation, source inventory, validation, and SignalR-exclusion guidance.
- Findings deferred: runtime telemetry implementation, query-stage histograms, trace tag propagation, raw-sample export automation, load-test tooling, query routing changes, ETag/cache semantics changes, authorization changes, and DAPR behavior changes.
- Final recommendation: ready-for-dev
