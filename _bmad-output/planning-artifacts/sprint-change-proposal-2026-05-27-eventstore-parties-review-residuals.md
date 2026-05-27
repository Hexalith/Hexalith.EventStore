# Sprint Change Proposal: EventStore↔Parties Review Residuals (ES-1…ES-10)

Date: 2026-05-27
Project: Hexalith.EventStore
Trigger: A re-review of the EventStore↔Parties integration surface produced 10 findings (ES-1…ES-10). They continue the `sprint-change-proposal-2026-05-12-eventstore-parties-integration-contract-gaps` thread (Epic 22) and need to be routed as EventStore-owned follow-ups.
Mode: Batch
Re-scope decision: Each item is narrowed to its **verified residual** — the part of the original finding that current `main` does not already satisfy. All 10 IDs are retained for traceability.
Prepared by: Claude (Developer)

## 1. Issue Summary

The review table was a snapshot that predates several patches already on `main`. Before routing work, every finding was verified against current code. Three were found stale or moot in whole or part, and three more are already partially mitigated. The corrected picture is below; the proposal routes only the **residual** work so review does not re-litigate code that already complies.

This is **not** an epic/PRD/MVP change. Epics 1–22 are all `done`; this is a post-Epic-22 hardening batch in the established "route findings into focused `sprint-status.yaml` rows" pattern (cf. `post-epic-22-r22a1-…`, the `dw11…dw19b` clusters).

### Verification results (current `main`)

| ID | Original finding | Verified status | Residual to route |
|----|------------------|-----------------|-------------------|
| ES-1 | Unbounded `JsonDocument.Parse` (DoS) **and** silent `JsonException` | **Logging already present** — `CommandsController.cs:134-138` already emits `LogWarning(... CorrelationId ...)`. DoS guard absent. | **DoS guard only**: `JsonDocumentOptions.MaxDepth` + max-length cap on the result payload. |
| ES-2 | `ResultPayload` (possible PII) persisted into actor state between EventsStored/EventsPublished | **Confirmed** — `AggregateActor.cs:429` and `:475` set `PipelineState.ResultPayload` and `CheckpointAsync` persists it. | Privacy-posture decision: scrub/redact from persisted `PipelineState`, **or** document acceptance with a retention bound. |
| ES-3 | Result payload silently dropped on transient status-store read failure | **Mostly valid** — read failure *is* logged (`StatusReadForTrackingFailed`, `SubmitCommandHandler.cs:91`), but the **payload-drop** at `:167-173` has no dedicated log. | Add a warning at the drop site (no payload content), so the drop is observable. |
| ES-4 | No back-compat test for omitted `ResultPayload` deserialization | **Likely valid** — positional `string? ResultPayload = null` default exists; no test covers the JSON-omitted-field wire path. | Add an explicit deserialization fixture (wire JSON without `ResultPayload` → `null`). |
| ES-5 | `Task<T:DomainResult>` discovery vs `Task<DomainResult>` dispatch → derived async results fall through | **Stale / already fixed** — `DispatchCommandAsync` switch arm `Task asyncResult when handleInfo.IsAsync => GetAsyncDomainResultAsync(...)` (`EventStoreAggregate.cs:161`) reflects `.Result` and casts to `DomainResult`; works for any derived `T`. | **Test coverage only**: sync + async derived-result (`Task<CompositeCommandResult>`) regression test to lock the behavior. |
| ES-6 | Locale-sensitive `Message.Contains(...)` for actor-not-found | **Confirmed** — `QueryRouter.cs:137-139` matches English Dapr strings. | Introduce a typed Dapr exception / status-code check; keep string match only as last-resort fallback. |
| ES-7 | `redisHost` set via `WithMetadata`, bypassing `statestore.yaml`; Parties deploy-validation asserts the yaml | **Confirmed (decision)** — `HexalithEventStoreExtensions.cs:80-84`; comment states `WithMetadata` generates the YAML. | Decide source-of-truth: make yaml authoritative, **or** document the `WithMetadata` override as authoritative and align the validator contract. |
| ES-8 | Sanitized `_` separator collision + undocumented fallback order | **Mostly mitigated** — order is already commented (`DomainServiceResolver.cs:43-92`); collision is narrow (version format validated). | Pick a non-colliding separator/escaping (or accept with a guard) **and** publish fallback ordering to docs/tests, not only code comments. |
| ES-9 | Contracts drags `Dapr.Actors`/`Dapr.Client`/`Grpc.*`/`Google.Protobuf` into client asset graph | **Partly mitigated** — `Dapr.Actors` is `PrivateAssets="all"` (csproj:9), so NuGet transitive flow is already suppressed. Real residual coupling is `IProjectionActor : IActor` (`Contracts/Queries/IProjectionActor.cs:16`). | Relocate `IProjectionActor` (and re-check `QueryEnvelope`/`QueryResult`) so `Contracts` no longer compiles against `Dapr.Actors` at all. |
| ES-10 | Spike-doc clarifications (case-sensitivity, `powershell`/`rg`, persistence note) | **Target missing** — `docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md` does not exist anywhere in the repo. | **Needs user decision**: confirm the doc was intentionally removed (drop ES-10), or supply the real path / restore it before doc fixes apply. |

## 2. Checklist Findings

| Item | Status | Finding |
|------|--------|---------|
| 1.1 Triggering issue | Done | EventStore↔Parties integration re-review (ES-1…ES-10), continuing the 2026-05-12 contract-gaps thread. |
| 1.2 Core problem | Done | Mixed: residual security/robustness hardening (ES-1, ES-6), observability gaps (ES-3), test debt (ES-4, ES-5), two architecture/policy decisions (ES-2, ES-7), one decoupling refactor (ES-9), and doc hygiene (ES-8, ES-10). |
| 1.3 Evidence | Done | All 10 verified against current `main` with file:line evidence (see §1 table). 3 stale/moot, 3 partly mitigated. |
| 2.1 Current epic impact | Done | No open epic. Epics 1–22 are `done`; this is post-Epic-22 follow-up scope. |
| 2.2 Epic-level changes | N/A | No new epic; no reopening. Findings route to focused `sprint-status.yaml` rows under a new post-Epic-22 cluster. |
| 2.3 Remaining planned epics | Done | None pending; nothing invalidated. |
| 2.4 New epic needed | N/A | No. |
| 2.5 Priority/order | Done | Security/robustness first (ES-1, ES-6), then observability/tests (ES-3, ES-4, ES-5), then decisions/refactor (ES-2, ES-7, ES-9), then docs (ES-8, ES-10). |
| 3.1 PRD conflict | N/A | No FR added/changed. ES-2/ES-7 are policy decisions within existing FR scope (FR11/NFR12 envelope/log-redaction; FR43/NFR29 component-config portability). |
| 3.2 Architecture conflict | Action-needed | ES-2 (persisted-state redaction posture), ES-7 (component-config source of truth), ES-9 (Contracts dependency boundary) each record a small architecture/ADR note. No topology change. |
| 3.3 UX conflict | N/A | No UI/UX change. ES-1 keeps the existing RFC 7807 surface unchanged. |
| 3.4 Other artifacts | Action-needed | Touches: `sprint-status.yaml` (new rows), deploy validator contract (ES-7, Parties-side), NuGet package guide / fitness-test pin (ES-9), domain-service routing docs (ES-8), spike doc (ES-10). |
| 4.1 Direct adjustment | Viable | Best path. Effort low–medium; risk low except ES-9 (public-surface relocation) which is medium. |
| 4.2 Rollback | Not viable | Nothing to revert; the issue is residual hardening, not a bad path. |
| 4.3 MVP review | N/A | MVP unaffected. |
| 4.4 Recommended path | Done | Direct adjustment: 10 focused follow-up rows; ES-5/ES-1-logging close as verification-only; ES-10 gated on user decision. |
| 5.1–5.5 Proposal components | Done | §3–§5 below. |
| 6.1–6.5 Approval/handoff | Action-needed | Awaits Jerome approval before `sprint-status.yaml` edit and story-file creation. |

## 3. Impact Analysis

**Epic impact:** None reopened. All work is post-Epic-22 follow-up, consistent with the existing `post-epic-22-*` and `post-epic-deferred-*` rows.

**PRD impact:** None. No FR is added or modified. ES-2 and ES-7 are posture decisions inside existing requirements (NFR12 "event payload data never in logs"; FR43/NFR29 "deploy by changing only DAPR component config").

**Architecture impact (notes/ADRs only, no topology change):**
- ES-2 — record the persisted-`PipelineState` redaction posture (the existing `ProtectedDataDiagnosticRedactor` already governs activity/exception redaction; this extends the decision to checkpointed pipeline state).
- ES-7 — record whether `statestore.yaml` or the `WithMetadata` override is the authoritative runtime source of truth, and align the Parties deploy validator accordingly.
- ES-9 — record the `Contracts` → no-`Dapr.Actors` boundary once `IProjectionActor` is relocated; relax the Parties `ClientArchitecturalFitnessTests` leaked-set pin afterward.

**Technical impact:** Confined to `Hexalith.EventStore` (gateway controller, server actor/pipeline/query-router/resolver, contracts, client aggregate) plus tests. ES-9 changes a public assembly boundary (`IProjectionActor` moves out of `Contracts`) — a SemVer-relevant change for the `Contracts` package; sequence and version-bump deliberately. CLAUDE.md R2-A7 (ULIDs, never `Guid.TryParse`) is not violated by any item; ES-1 touches no ID field.

## 4. Recommended Path Forward

**Direct adjustment** — add 10 focused rows to `sprint-status.yaml` under a new `Post-Epic-22 EventStore↔Parties Review Residuals` cluster, each tracing to its ES-ID. Two rows (ES-1 logging half, ES-5 implementation) are verification-only closes. ES-10 is gated on a user decision (missing target).

Rationale: lowest-risk path, matches the repository's established follow-up mechanism, avoids re-opening completed epics, and avoids re-reviewing already-compliant code.

## 5. Detailed Change Proposals

Per-item residual scope, suggested `sprint-status.yaml` row, scope class, and Parties-side follow-up. Story files are created via `bmad-create-story` only when a row is selected for execution.

### ES-1 — Result-payload parse DoS guard
- **Residual:** Add `JsonDocumentOptions { MaxDepth = <bound> }` to the `JsonDocument.Parse` call and a max-length guard on `result.ResultPayload` before parsing, in `CommandsController.ParseOptionalResultPayload`. **Logging already exists** — do not re-add.
- **Row:** `post-epic-22-es1-result-payload-parse-dos-guard`
- **Scope:** Minor (Developer).
- **Parties:** None.

### ES-2 — PipelineState result-payload privacy posture
- **Residual:** Decide and implement one of: (a) scrub/redact `ResultPayload` from the persisted `PipelineState` checkpoints (`AggregateActor.cs:429`, `:475`) while keeping it for the terminal in-memory return; or (b) document acceptance with an explicit retention bound (the status key already has a 24h TTL — extend the rationale to the pipeline key).
- **Row:** `post-epic-22-es2-pipeline-state-result-payload-privacy-posture`
- **Scope:** Moderate (Developer + Architect note).
- **Parties:** Revisit Parties PII-marking assumptions once posture is set.

### ES-3 — Result-payload drop observability
- **Residual:** Emit a warning at the drop site (`SubmitCommandHandler.cs:167-173`) when a non-null `processingResult.ResultPayload` is dropped because `finalStatus?.Status != Completed` (or status read failed). Log envelope metadata only (correlationId, tenant) — no payload content.
- **Row:** `post-epic-22-es3-result-payload-drop-observability`
- **Scope:** Minor (Developer).
- **Parties:** None.

### ES-4 — DomainServiceWireResult omitted-payload back-compat test
- **Residual:** Add a Tier 1 fixture deserializing wire JSON that omits `ResultPayload`, asserting it binds to `null` (locks the STJ positional-record default against future serializer-config changes). Target `Hexalith.EventStore.Contracts.Tests` (or `.Server.Tests` if that is where wire round-trips live).
- **Row:** `post-epic-22-es4-domain-service-wire-result-backcompat-test`
- **Scope:** Minor (Developer).
- **Parties:** None.

### ES-5 — Async derived-result test coverage (verification-only)
- **Residual:** Implementation is already correct (`EventStoreAggregate.cs:161` + `GetAsyncDomainResultAsync`). Add sync + async derived-result coverage (e.g. a handler returning `Task<CompositeCommandResult>`) to prevent regression. Close as verification.
- **Row:** `post-epic-22-es5-async-derived-domain-result-test-coverage`
- **Scope:** Minor (Developer).
- **Parties:** Composite handlers may use derived async results (already supported).

### ES-6 — Typed actor-not-found detection
- **Residual:** Replace the locale-sensitive `Message.Contains(...)` in `QueryRouter.IsProjectionActorNotFound` (`:129-139`) with a typed Dapr exception / status-code check; retain the string match only as a documented last-resort fallback. EventStore is fixed **first**; Parties migrates its two copies after.
- **Row:** `post-epic-22-es6-projection-actor-not-found-typed-check`
- **Scope:** Moderate (Developer).
- **Parties:** Switch `PartyDetailProjectionQueryActor` + `PartyIndexProjectionQueryActor` to the typed check.

### ES-7 — State-store config source-of-truth decision
- **Residual:** Decide whether `statestore.yaml` or the `WithMetadata` override (`HexalithEventStoreExtensions.cs:80-84`) is authoritative; implement to make that single source true and document it.
- **Row:** `post-epic-22-es7-statestore-yaml-source-of-truth-decision`
- **Scope:** Moderate (Developer + Architect note).
- **Parties:** Reconcile `deploy/validate-deployment.ps1` Redis-host assertions after the decision.

### ES-8 — DomainServiceResolver separator + fallback-order docs
- **Residual:** Choose a non-colliding separator/escaping for the sanitized wildcard key (or add an explicit collision guard), and publish the documented fallback ordering to repo docs/tests (currently only in code comments at `DomainServiceResolver.cs:43-92`).
- **Row:** `post-epic-22-es8-domain-service-resolver-separator-and-fallback-docs`
- **Scope:** Minor (Developer).
- **Parties:** Re-verify Parties wildcard `*|party|v1` registration after change.

### ES-9 — Contracts ↔ Dapr decoupling
- **Residual:** Relocate `IProjectionActor : IActor` (and re-check `QueryEnvelope`/`QueryResult`) out of `Hexalith.EventStore.Contracts` so the package no longer compiles against `Dapr.Actors`; then drop the `Dapr.Actors` reference. NuGet transitive flow is already blocked by `PrivateAssets="all"`, so this is the architectural-cleanliness completion, not a new leak.
- **Row:** `post-epic-22-es9-contracts-dapr-decoupling`
- **Scope:** Moderate (Developer + Architect note). SemVer-relevant for the `Contracts` package.
- **Parties:** Relax `ClientArchitecturalFitnessTests` leaked-set pin once the dependency is gone.

### ES-10 — Parties actor-invocation spike doc (decision required)
- **Residual:** Target `docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md` does not exist. Confirm one of: (a) the doc was intentionally removed → **drop ES-10**; (b) supply the correct path → re-scope the doc fixes; (c) restore the doc → then apply the case-sensitivity, `powershell`/`rg`, and persistence-note fixes.
- **Row:** `post-epic-22-es10-parties-actor-invocation-spike-doc` (created only if option (b)/(c))
- **Scope:** Minor (doc-only) or dropped.
- **Parties:** None.

### Proposed `sprint-status.yaml` addition

Appended after `post-epic-deferred-dw19b-keycloak-fast-start-tier3-validation-and-port-hardening` (all rows `backlog`; story files created when a row is selected):

```yaml
  # Post-Epic-22 EventStore↔Parties Review Residuals (sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md)
  # 10 review findings (ES-1…ES-10) re-scoped to verified residual after checking current main.
  # ES-1 logging half and ES-5 implementation are verification-only (code already complies).
  # ES-10 is gated on a user decision — its target spike doc does not exist in the repo.
  post-epic-22-es1-result-payload-parse-dos-guard: backlog
  post-epic-22-es2-pipeline-state-result-payload-privacy-posture: backlog
  post-epic-22-es3-result-payload-drop-observability: backlog
  post-epic-22-es4-domain-service-wire-result-backcompat-test: backlog
  post-epic-22-es5-async-derived-domain-result-test-coverage: backlog
  post-epic-22-es6-projection-actor-not-found-typed-check: backlog
  post-epic-22-es7-statestore-yaml-source-of-truth-decision: backlog
  post-epic-22-es8-domain-service-resolver-separator-and-fallback-docs: backlog
  post-epic-22-es9-contracts-dapr-decoupling: backlog
  # post-epic-22-es10-parties-actor-invocation-spike-doc: backlog  # add only if doc is restored/repath'd
```

## 6. Implementation Handoff

- **Scope classification:** Moderate — backlog reorganization (new `sprint-status.yaml` rows), no epic/PRD/MVP change.
- **Route to:** Developer agent for all coding rows; Architect note for ES-2, ES-7, ES-9 decisions.
- **Sequence:** ES-1, ES-6 → ES-3, ES-4, ES-5 → ES-2, ES-7, ES-9 → ES-8 → ES-10 (pending decision).
- **Cross-repo:** ES-6, ES-7, ES-8, ES-9 each have a Parties-side follow-up that lands **after** the EventStore change.
- **Success criteria:** each row's residual implemented with targeted Tier 1/Tier 2 evidence and senior code review (per CLAUDE.md mandatory review stage); ES-5 and ES-1-logging close as verification; ES-9 takes a deliberate `Contracts` version bump.
- **Constraints honored:** CLAUDE.md R2-A7 (ULIDs, no `Guid.TryParse`) — no item touches an ID field; NFR12/Rule 5 (no payload in logs) — ES-3 logs envelope metadata only.

## 7. Decision Required

ES-10's target doc is missing. Choose: (a) drop ES-10, (b) supply the correct path, or (c) restore the doc. The `sprint-status.yaml` ES-10 row stays commented out until then.
