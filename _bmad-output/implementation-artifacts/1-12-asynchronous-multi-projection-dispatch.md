---
baseline_commit: 5223e9c9c2f0dd71673003c710b8739efc8484ff
---

# Story 1.12: Asynchronous Multi-Projection Dispatch

Status: in-progress

**Requirements covered:** FR7, FR36, NFR7, NFR12, NFR16  
**Governed by:** AD-2, AD-7, AD-8, AD-12, AD-19, AD-20  
**Builds on:** Implemented platform seams from Stories 1.4, 1.9, 1.10, and 1.11. Unresolved review findings in another story are not a serial completion lock; Story 1.12 may implement and complete independently unless it exposes a direct contract contradiction. Story 1.15 remains blocked until Stories 1.9-1.14 are complete and reviewed.
**Feeds:** Stories 1.13-1.15; Story 1.14 owns named-handler rebuild staging and replay equivalence

## Story

As a domain projection author,
I want asynchronous named projection handlers with one-to-many dispatch,
so that one domain can durably maintain detail and index projections through platform seams.

## Acceptance Criteria

1. **Asynchronous persistence contract**  
   **Given** a named projection handler performs persistence  
   **When** it handles a request  
   **Then** the contract is asynchronous and cancellation-aware, can await `IReadModelStore`, `ReadModelWritePolicy`, and Story 1.10 batches, and completes only after required persistence finishes  
   **And** production awaits use `ConfigureAwait(false)`.

2. **Named route identity**  
   **Given** handlers are registered  
   **When** routes are validated  
   **Then** handlers are uniquely identified by `(Domain, ProjectionType)`, multiple projection types may share one domain, and duplicate pairs fail deterministically with a support-safe diagnostic.

3. **Deterministic one-to-many dispatch**  
   **Given** one delivery applies to multiple projections  
   **When** dispatch runs  
   **Then** every applicable named handler is invoked, detail and index outcomes remain distinguishable, and observable invocation order is deterministic.

4. **Truthful partial failure**  
   **Given** one projection handler fails after another completes durably  
   **When** the endpoint returns its bounded per-projection result or equivalent versioned result  
   **Then** failed projection checkpoint state does not advance as success  
   **And** partial failure remains retryable without losing successful durable work.

5. **Backward compatibility**  
   **Given** existing synchronous single-projection consumers remain  
   **When** the new contract ships  
   **Then** they continue through a compatibility adapter or an explicitly approved breaking-version plan  
   **And** no consumer silently receives a changed JSON shape or ambiguous domain-only route.

6. **Production-path persistence proof**  
   **Given** a test domain registers detail and index handlers  
   **When** the production dispatch path executes  
   **Then** both async persistence operations are awaited and both persisted outputs are independently verified  
   **And** a one-handler failure proves truthful result and checkpoint behavior without Parties-specific EventStore logic.

## Resolved Implementation Contract

The following decisions remove ambiguities that would otherwise make this story unsafe or non-implementable. They are requirements unless implementation evidence proves one impossible; in that case stop and return the story for architecture correction rather than silently weakening it.

### 1. Compatibility strategy is additive

- No breaking-version approval exists. Keep `IDomainProjectionHandler`, `ProjectionRequest`, `ProjectionResponse`, and the legacy `POST /project` single-object JSON contract source/wire compatible.
- Add `IAsyncDomainProjectionHandler` with canonical `Domain`, canonical `ProjectionType`, and `ProjectAsync(ProjectionRequest request, string dispatchId, CancellationToken cancellationToken)` returning a structured handler result.
- Add an explicitly versioned `POST /project/v2` request/result path. Do not return an array or v2 wrapper from the unversioned legacy route.
- Freeze the v2 wire envelope as `ProjectionDispatchRequest(ProjectionRequest Request, IReadOnlyList<string> ProjectionTypes, string DispatchId, string CatalogFingerprint)`, `ProjectionDispatchResponse(int Version, IReadOnlyList<ProjectionDispatchOutcome> Outcomes)`, and `ProjectionDispatchOutcome(string ProjectionType, ProjectionDispatchStatus Status, JsonElement? State, string? ReasonCode)`, with `Version = 2`. New optional members require a new version; do not mutate v2 semantics in place.
- The v2 path may adapt one legacy synchronous handler only through an explicitly registered `(Domain, ProjectionType)` mapping that is advertised in metadata; validate the returned `ProjectionType` against that mapping. An unmapped legacy handler remains v1-only. Reject an ambiguous legacy+named registration for the same pair.
- Preserve the existing rule that an application-pre-mapped `POST /project` wins over the SDK route. Do not modify the Tenants submodule or its bespoke async single-response endpoint in this story.
- A valid v2 dispatch returns `200` with bounded per-projection outcomes even when one handler fails. Per-handler failures are application outcomes, not a transport `500` that invites opaque service-invocation retries. Malformed/unsupported requests may still use safe `4xx`; startup route ambiguity fails before serving traffic.

### 2. Canonical routes and applicability

- Validate new handler `Domain` and `ProjectionType` with the existing `NamingConventionEngine.ValidateKebabCase` rules: lowercase ASCII alphanumeric plus internal hyphens, no leading/trailing hyphen, maximum 64 characters.
- Key routes by the exact canonical pair and detect duplicates deterministically. The duplicate diagnostic contains a stable reason code such as `duplicate_projection_route` plus sorted handler type names; it contains no payload, tenant data, event data, state keys, ETags, or exception text.
- Extend the existing `/admin/operational-index-metadata` projection catalog to report the exact named async projection types, an additive dispatch-capability/version marker, and a deterministic fingerprint of the sorted `(Domain, ProjectionType)` routes bound to app id/service version. Preserve existing public metadata constructors/deconstructors; add compatible overloads or init-only members and prove old/new JSON plus ABI compatibility.
- Add a singleton `INamedProjectionRouteCatalog` runtime seam in EventStore. `AdminOperationalIndexHostedService` atomically replaces its immutable snapshot only after one complete successful metadata load; persistence to admin indexes and runtime publication use that same loaded object. The orchestrator reads this seam rather than reconstructing a second catalog. A last-known snapshot may remain during refresh failure, but app id, service version, capability, and fingerprint must match the domain service at dispatch time or invocation fails closed.
- "Applicable" means a registered named handler whose canonical domain matches the request and whose projection type is in the explicit non-empty server-admitted v2 projection set. `/project/v2` rejects missing/empty sets and unknown projection types without invoking a handler. When all routes are denied by drift/lifecycle admission, the server skips the endpoint entirely.
- Materialize, validate, sort by `ProjectionType` using `StringComparer.Ordinal`, and await handlers sequentially. Do not rely on assembly scan order, DI `IEnumerable<T>` order, or parallel completion order for observable ordering.
- Bound one domain to a positive validated maximum number of named handlers (default 32) and reject registration beyond the limit. The result has at most one outcome per admitted handler. Bound reason codes to 128 ASCII bytes and the serialized outcome envelope to 1,048,576 bytes by validated options; over-limit output is a safe failed/malformed outcome and never advances a checkpoint.

### 3. Handler and outcome semantics

- The named async handler returns a structured result rather than relying on thrown exceptions to represent expected durable states. The dispatcher stamps the registered `ProjectionType` into the serializable outcome so a handler cannot rename its route at runtime. The outcome carries a closed status, an optional bounded reason code, and optional legacy actor state when applicable.
- Freeze the closed wire status set and stable numeric values as `Completed = 0`, `AlreadyCompleted = 1`, `Retryable = 2`, `Indeterminate = 3`, and `Failed = 4`:
  - `Completed` and `AlreadyCompleted` — durable success proven;
  - `Retryable` — known incomplete/conflict work that may converge under the same identity;
  - `Indeterminate` — durable completion could not be proved;
  - `Failed` — terminal/configuration/domain failure.
- Provide one platform mapping/helper for `ReadModelBatchResult`: only `Completed` and `AlreadyCompleted` map to success; optimistic conflict/incomplete remain retryable; identity conflict is failed; indeterminate remains indeterminate. Do not copy ad hoc mappings into each domain.
- A handler may return optional projection state for the legacy projection-actor path. A persistence-only handler may omit state; omission must not be misclassified as successful legacy actor output.
- A state-bearing handler must return the same semantically equivalent state on `AlreadyCompleted` retries so an earlier durable handler success followed by an actor-write/transport failure can finish the actor/ETag/checkpoint sequence. State is valid only on `Completed`/`AlreadyCompleted`; state on any other status is a malformed outcome.
- An unexpected non-cancellation exception becomes that handler's safe `Indeterminate` outcome, because persistence may have occurred; it does not prevent later applicable handlers from running. `Failed` is reserved for a known terminal validation/configuration/domain outcome. Never expose exception messages or stack traces in the wire result. Request cancellation propagates as `OperationCanceledException`, prevents new handler starts, and produces no fabricated outcome/checkpoint; durable retry work remains pending so a later attempt reconciles any earlier durable success with the same dispatch identity.
- Every handler must be retry-safe. The exact `dispatchId` received by `ProjectAsync` is the `ReadModelBatchScope.BatchId` for Story 1.10-backed work; do not generate, transform, or replace it inside a handler. Full duplicate/gap/out-of-order delivery policy remains Story 1.13 scope.

### 4. Pre-persistence admission is mandatory

The current server invokes `/project` before it knows `ProjectionType` because the legacy endpoint is documented as side-effect-free. It performs projection-scoped drift checks and erase-lifecycle admission only after the response. A persistence-capable handler would violate those gates if merely substituted into that flow.

- Before invoking a named persistence handler, the server must resolve the exact named projection set from the versioned operational metadata catalog.
- For every projection, in stable ordinal order, read its projection-scoped delivery checkpoint and perform drift validation. A checkpoint ahead of the available stream excludes that projection and emits the existing bounded drift diagnostic.
- Perform `IProjectionLifecycleGateway.TryAdmitDeliveryWriteAsync` for each projection before remote persistence. A denied projection is deferred and is not included in the v2 request.
- Preserve same-aggregate serialization with `ProjectionLocks`. Do not regress tenant/domain/aggregate/projection scoping.
- The v2 request carries only the server-admitted projection names plus a stable dispatch identity and the exact catalog fingerprint used for admission. Derive the immediate-delivery identity from stable persisted event metadata (the highest-sequence event's `MessageId` for the current full-history delivery), not a random per-retry value. The endpoint recomputes/reads its current fingerprint and rejects a mismatch before handler invocation. Story 1.13 may later strengthen the general delivery identity/checkpoint contract without changing this compatibility guarantee.
- Bind the persisted route catalog to the resolved domain-service app id and service version. If versioned route metadata is missing, belongs to another app/version, has a mismatched fingerprint, is malformed, or lacks a recognized capability marker, do not invoke named handlers. An explicitly registered legacy/bespoke v1 route may continue through the existing v1 behavior; never reinterpret legacy `ProjectionNames` as authorization for v2 persistence.

### 5. Server result and checkpoint truth

- Process every returned outcome independently and reject duplicate, missing, unrequested, over-limit, or malformed projection outcomes without checkpoint advancement.
- Advance `IProjectionDeliveryCheckpointStore` for a projection only after its handler outcome proves `Completed`/`AlreadyCompleted` and any required legacy actor write has completed. `Retryable`, `Indeterminate`, `Failed`, absent, or malformed outcomes never advance that projection.
- Preserve successful sibling durable work and its own checkpoint when another projection fails. The aggregate-wide call is partial, not a fabricated all-success result.
- On retry with the same stream head and dispatch identity, a Story 1.10-backed successful handler returns `AlreadyCompleted`; the failed sibling can retry without reapplying successful durable work.
- Apply one explicit reconciliation matrix: `Completed`/`AlreadyCompleted` may advance only their own checkpoint after any actor write; `Retryable` and `Indeterminate` remain pending and are automatically retried; known-terminal `Failed` produces an operator-visible safe diagnostic but is not automatically retried; missing/malformed/unrequested/duplicate outcomes and transport interruption advance nothing and remain pending for bounded retry. Cancellation never invents an outcome and leaves pre-registered work pending.
- Add a durable `IProjectionDeliveryRetryScheduler` plus hosted worker for immediate-mode partial delivery; `ProjectionPollerService` does not cover domains whose `RefreshIntervalMs == 0`. Before remote dispatch, persist a work item keyed by aggregate identity and observed stream head with the admitted routes, dispatch id, catalog fingerprint, attempt, and next-due time, but not event payloads. A retry reloads aggregate history only through the recorded head sequence, verifies its `MessageId`, reconstructs the same request, and reuses the same dispatch id. Use validated bounded attempts/backoff per activation, retain exhausted work for a later due/operator-visible attempt rather than acknowledging it as success, and delete the work item only when every route is completed/already-completed. Mark known-terminal routes terminal and retain bounded operator-visible evidence until an explicit safe cleanup policy removes it. This keeps retry state restart-safe without turning Story 1.13 into this story.
- Preserve the legacy ordering for a state-bearing legacy outcome: lifecycle admission -> projection actor write -> ETag regeneration -> projection-scoped checkpoint. Existing fail-open ETag behavior is not expanded or redefined here.
- Emit bounded source-generated logs/telemetry per projection route, status, duration, and aggregate-safe identity context. Do not log handler result state, payloads, read-model keys, ETags, batch fingerprints, raw reason detail, tokens, or exception text.

### 6. Rebuild is explicitly gated to Story 1.14

- Story 1.12 changes normal/immediate production delivery only. Keep the existing legacy side-effect-free full-replay path available for existing rebuild consumers.
- Do not invoke persistence-capable named handlers from `DeliverProjectionForRebuildAsync`: the current rebuild sends a 256-event page, and live persistence there could overwrite complete state with page-only output.
- If a domain has only named persistence handlers, rebuild must fail/defer support-safely without handler invocation or checkpoint advancement until Story 1.14 adds explicit full/incremental semantics, staging, resume, and promotion.
- Do not implement rebuild staging/promotion, duplicate/gap history, or delivery cost optimization in this story.

## Tasks / Subtasks

- [x] Task 1 - Freeze additive contracts and limits (AC: 1-5)
  - [x] Add one-type-per-file v2 contracts under `src/Hexalith.EventStore.Contracts/Projections/`: `ProjectionDispatchRequest`, `ProjectionDispatchResponse`, `ProjectionDispatchOutcome`, `ProjectionDispatchStatus`, and dispatch capability/version constants.
  - [x] Preserve the released constructors and serialized members of `ProjectionRequest` and `ProjectionResponse`; add contract tests that serialize the legacy shape and deserialize with old expectations.
  - [x] Add validated `ProjectionDispatchOptions` (default maximum 32 handlers/outcomes plus bounded retry/backoff settings) without adding an inline package version.
  - [x] Add bounded stable reason codes for duplicate route, unsupported route/capability, malformed outcome, handler failure, cancellation, and partial retry.

- [x] Task 2 - Add the named async handler seam and compatibility adapter (AC: 1, 2, 5)
  - [x] Add `IAsyncDomainProjectionHandler` and `DomainProjectionHandlerResult` in DomainService with the exact async signature and status/state/reason behavior above.
  - [x] Leave `IDomainProjectionHandler.Project` unchanged; update its documentation only to name the additive async seam.
  - [x] Add only an explicitly named `(Domain, ProjectionType)` legacy adapter for v2, validate the returned route, and reject ambiguous legacy+named registration for the same pair; unmapped legacy handlers remain v1-only.
  - [x] Register persistence-capable handlers with a DI lifetime that permits scoped dependencies (`IReadModelStore`, logging, options); do not preserve the old singleton assumption for the new interface.

- [x] Task 3 - Build deterministic route validation and metadata (AC: 2, 3)
  - [x] Update route validation to permit same-domain/different-projection async handlers, reject duplicate canonical pairs and over-limit domains, and preserve legacy duplicate-domain validation.
  - [x] Sort named routes explicitly by projection type using ordinal comparison.
  - [x] Extend `AdminOperationalIndexMetadata` additively with exact named routes, dispatch version/capability, app/service binding, and deterministic sorted-route fingerprint while preserving released constructors/deconstructors.
  - [x] Add `INamedProjectionRouteCatalog`; atomically publish the exact successful metadata-load snapshot to both runtime consumers and persisted admin indexes instead of deriving a second catalog.
  - [x] Prove missing/legacy/stale/mismatched metadata fails closed and cannot authorize side-effectful dispatch.

- [x] Task 4 - Implement v2 domain-service dispatch (AC: 1-5)
  - [x] Add `DomainProjectionDispatcher.DispatchAsync` to validate the admitted set, await sequential handlers with `ConfigureAwait(false)`, and continue after non-cancellation handler failure.
  - [x] Map expected batch outcomes through one platform helper; map unexpected failure to support-safe `Indeterminate`, reserving `Failed` for known terminal outcomes.
  - [x] Map `POST /project/v2` without changing the v1 endpoint or bespoke-v1-route yielding behavior.
  - [x] Return `200` plus bounded outcomes for a valid partial dispatch; reject empty admission, catalog-fingerprint mismatch, and malformed/unsupported requests before invoking handlers.

- [x] Task 5 - Integrate safe normal delivery (AC: 3, 4, 6)
  - [x] Refactor the 1,419-line `ProjectionUpdateOrchestrator` through a focused named-dispatch collaborator instead of duplicating another large flow inline.
  - [x] Resolve exact versioned projection routes, run projection-scoped drift and lifecycle admission before v2 invocation, and send only admitted routes with the stable dispatch identity.
  - [x] Reconcile v2 outcomes independently; persist optional legacy actor state and advance only proven successful projection checkpoints.
  - [x] Add durable `IProjectionDeliveryRetryScheduler` work plus a hosted worker for immediate-mode partial outcomes; reconstruct the original full-history request through the recorded stream head and reuse its dispatch id/fingerprint across restart-safe bounded retries.
  - [x] Preserve successful sibling checkpoints, retain retryable/indeterminate/missing/malformed work, surface known-terminal failure without auto-retry, and never report all-success for partial failure.
  - [x] Keep named persistence dispatch out of `DeliverProjectionForRebuildAsync` and prove the safe defer behavior.

- [x] Task 6 - Add a generic detail/index proof domain (AC: 1, 3, 4, 6)
  - [x] Add two named handlers in test fixtures for one generic domain; do not encode Parties/Tenants business rules.
  - [x] Have both handlers use the platform persistence seams; at least one proof must execute an `IReadModelBatchStore` batch with the stable dispatch identity.
  - [x] Add an awaited-completion barrier/fault hook proving the endpoint does not return and the server does not checkpoint before durable completion.
  - [x] Inject one-handler failure/indeterminate result after its sibling commits and prove retry converges to `AlreadyCompleted` + completed without duplicate successful work.

- [x] Task 7 - Add deterministic compatibility and failure tests (AC: 1-6)
  - [x] DomainService tests: discovery/lifetime, same domain with distinct projection types, duplicate pair/case variant, over-limit routes, reverse registration order, deterministic invocation order, cancellation, continue-after-failure, bounded outcome, v1 shape, legacy adapter, and bespoke `/project` yielding.
  - [x] Contracts tests: v1 JSON shape, v2 version/status stability, unsafe enum/input behavior, duplicate/unrequested/malformed outcomes, and bounded reason codes.
  - [x] Server tests: pre-persistence drift/erase denial, empty-admission skip, catalog-fingerprint mismatch, per-projection outcome reconciliation, sibling checkpoint independence, exact dispatch-id-to-batch-id propagation, no checkpoint on retryable/indeterminate/failed/malformed outcome or canceled transport, and no named-handler invocation during rebuild.
  - [x] Retry tests: immediate mode does not rely on `ProjectionPollerService`; pending work survives scheduler/worker recreation, reloads only through the recorded stream head, preserves dispatch identity, applies bounded backoff, clears on convergence, and remains operator-visible on known-terminal or exhausted work.
  - [x] Keep existing Sample synchronous handler tests green; use Shouldly for new assertions and xUnit v3 project lanes.

- [x] Task 8 - Add persisted production-path evidence (AC: 4, 6; NFR16)
  - [x] Before claiming Story 1.10 batch wiring proven, run its `ReadModelBatchLiveSidecarTests` in a working Tier-3 environment; the prior implementation session authored the tests but recorded exit 144 during fixture startup, not executed evidence.
  - [x] Add a live DAPR/Redis test that drives EventStore orchestration -> DAPR `/project/v2` -> both named handlers -> `IReadModelBatchStore`/`IReadModelStore` -> response reconciliation.
  - [x] Inspect persisted detail, index, batch marker/receipt, successful and failed projection checkpoints, and retry end state directly. HTTP `200`, handler call counts, or recorder requests are insufficient.
  - [x] Record Dapr.Client package, DAPR CLI, runtime, and Redis component versions used by the evidence; do not infer runtime semantics from the NuGet version.

- [x] Task 9 - Update authoring guidance and guardrails (AC: 1, 5)
  - [x] Update projection/read-model authoring documentation to prefer the async named seam for persistence and retain the legacy seam for synchronous full-replay compatibility.
  - [x] Update project context/state instructions in the owning repository source when appropriate; do not edit `references/Hexalith.AI.Tools` from EventStore without explicit approval.
  - [x] Keep domain-module guardrails green: domains consume `IReadModelStore`/`IReadModelBatchStore`; they do not add raw DAPR state clients, custom batch markers, query/projection actors, or platform plumbing.
  - [x] Do not modify root-declared submodules, AppHost/DAPR YAML, release inventory, generated REST/UI surfaces, or add a package/project.

## Dev Notes

### Current State Of Files Expected To Be Updated

- `src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs` — released synchronous, domain-only, stateless full-replay seam. Preserve `Domain` and `Project(ProjectionRequest)` exactly. Add only documentation/adapter integration; the new async contract belongs in its own file.
- `src/Hexalith.EventStore.DomainService/DomainProjectionHandlerRouteValidator.cs` — materializes handlers and currently rejects any same-domain pair case-insensitively. Split legacy domain validation from named async `(Domain, ProjectionType)` validation; retain deterministic sorted diagnostics.
- `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs` — currently selects the first matching domain handler and returns one nullable response synchronously. Preserve this as v1 behavior; add a separate async fan-out path rather than changing its return shape.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` — discovers handlers, registers legacy handlers as singletons, validates routes at endpoint mapping, maps v1 `/project`, and yields when a bespoke v1 route exists. Add scoped named-handler registration and v2 mapping without disturbing canonical v1 endpoints, query routing, telemetry discovery, or bespoke route precedence.
- `src/Hexalith.EventStore.DomainService/AdminOperationalIndexMetadata.cs` — already owns the commands/events/projections/query catalog. Its current handler discovery collapses projection names to the domain. Extend it additively with exact named async routes/capability; do not repurpose legacy `ProjectionNames` in a way old consumers misread.
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` — normal delivery reads full history, calls v1 `/project`, then performs drift check, lifecycle admission, actor write, ETag regeneration, and one projection checkpoint. Its rebuild path calls the same endpoint with 256-event pages. Refactor only normal v2 delivery through a focused `NamedProjectionDispatchCoordinator`; preserve legacy/rebuild behavior and same-aggregate locking.
- `src/Hexalith.EventStore.Server/Projections/ProjectionReasonCodes.cs` — stable bounded internal diagnostics. Add symbols instead of scattering string literals.
- `src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs` — already loads domain operational metadata and persists projection/query indexes, but its successful load is currently transient and unavailable to the orchestrator. Preserve all-or-nothing refresh behavior and atomically publish that same immutable load through `INamedProjectionRouteCatalog`; do not re-query or derive a second catalog.
- `src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs` — intentionally skips domains with `RefreshIntervalMs == 0`; do not claim it retries immediate-mode partial outcomes. Add a separate durable retry scheduler/worker and keep polling cadence semantics unchanged.
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` and `Fixtures/WidgetDomain.cs` — current proof covers legacy singleton discovery, domain-only duplicate rejection, one response, 404, and endpoint mapping. Keep these cases and add named async fixtures/tests.
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs` — broad current delivery/rebuild regression corpus. Preserve actor write, ETag, drift, lifecycle, cancellation, and rebuild cases; add focused v2 behavior, preferably around a new collaborator to limit fixture explosion.
- `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjection.cs` and `CounterProjectionHandler.cs` — legacy synchronous proof. No migration is required; their unchanged behavior is the compatibility evidence.
- `references/Hexalith.Tenants/.../Program.cs` and projection dispatcher/handlers — real bespoke async v1 compatibility consumer. Read-only evidence only; do not modify the submodule.

### Architecture And Compatibility Guardrails

- Domain modules remain domain-centric (AD-2). New routing, outcome contracts, metadata, checkpoint reconciliation, and compatibility adapters live in existing EventStore packages.
- Checkpoints are per `(tenant, domain, aggregate, projection)` through `IProjectionDeliveryCheckpointStore`. Do not add required members to the released aggregate-wide checkpoint interface.
- `IReadModelBatchStore` is immediate and returns structured truth. Only `Completed`/`AlreadyCompleted` are success; never turn `Incomplete`/`Indeterminate` into success or advance checkpoints from them.
- Unexpected handler/transport uncertainty is `Indeterminate`, not terminal `Failed`. The durable immediate-mode retry record is platform state, contains route/head/identity metadata rather than event payloads, and is independent of the polling registry.
- Story 1.10's Redis profile remains `Resumable`; DAPR metadata alone does not qualify transactions. Its terminal receipts remain indefinite until Story 1.13 defines a bounded dedup horizon.
- `ProjectionRequest.Events` may contain protected-data-safe DTOs only. Do not add server-internal actor/checkpoint objects or secrets to the domain wire.
- EventStore identifiers are ULID-safe; do not parse message/dispatch/batch IDs as GUIDs.
- One C# type per file, file-scoped namespaces, XML documentation on public/protected/internal members, central package versions, no copyright headers, warnings-as-errors, and `ConfigureAwait(false)` on production awaits.
- AOT/trimming remains out of scope while reflection discovery is load-bearing.

### Scope Boundaries

**In scope:** generic async named handler API; additive v2 wire; deterministic fan-out; exact route catalog; normal-delivery pre-admission; truthful bounded outcomes; per-projection checkpoint reconciliation; legacy adapter; generic detail/index persistence proof.

**Out of scope:** Parties/Tenants migration or submodule edits; general MessageId duplicate/gap/out-of-order state (1.13); paged rebuild staging/promotion/equivalence (1.14); lifecycle/provenance changes (1.11/2.8); batch protocol changes (1.10); erasure changes (1.9); projection cost optimization (6.3/6.4); generated REST/UI; AppHost/topology; release inventory.

### Previous Story Intelligence

- Story 1.11 completed the additive seven-state lifecycle/provenance contract and explicitly excluded async dispatch. Preserve its fail-safe rule: persistence/HTTP/ETag/SignalR success does not fabricate lifecycle authority.
- Story 1.10 implemented `IReadModelBatchStore` plus shared resumable/transaction-qualified protocol, structured outcomes, stable fingerprints, deterministic fake parity, and checkpoint exclusion. Reuse it; do not build another batch mechanism.
- Story 1.10's deterministic lanes passed, but its live-sidecar suite did not execute in the prior environment (test host exit 144). Production dispatch wiring needs executed Tier-3 evidence, not the existence of test code.
- Commit `acc45f14` hardened batch compaction/compensation with ETag-guarded writes/deletes and `Indeterminate` outcomes. Handler-level tests must keep that truth rather than optimistically treating an ambiguous call as complete.
- Story 1.9 supplied projection-scoped checkpoint and erase-lifecycle seams. Preserve independent checkpoint advancement and perform lifecycle admission before named handler persistence.
- Recent persisted-evidence precedent is commit `f70761c5`: real Redis end-state proof, not mock/call-count evidence. Recent released-shape/DI precedent is `dbcca284`.

### Git Intelligence Summary

- Current baseline inspected: `512292e2`; worktree was clean and synchronized with `origin/main` during story creation.
- Relevant implementation lineage:
  - `3c158ba5` — current synchronous DomainService projection seam and duplicate-domain validation.
  - `428ef1a1` + `acc45f14` — coordinated batch API/protocol and race hardening.
  - `6aa29af6` — lifecycle/provenance compatibility patterns.
  - `940e8acb` + `c8bbd384` — projection-scoped checkpoints and lifecycle write gate.
- Scope from the story/code map, not commit subject alone; recent commits include unrelated submodule pointer changes.

### Latest Technical Information

- The repository pins .NET SDK `10.0.301`/`net10.0`, Dapr.Client and Dapr.AspNetCore `1.18.4`, Aspire Hosting `13.4.6`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`. Keep versions centralized; this story is not a dependency upgrade.
- ASP.NET Core 10 Minimal APIs bind a `CancellationToken` route parameter to `HttpContext.RequestAborted`; pass it through every dispatcher/handler/store await. [Microsoft Minimal API parameter binding](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0)
- Microsoft DI preserves registration order when resolving `IEnumerable<T>`, but deterministic behavior in this contract must not depend on that incidental registration order; sort validated routes explicitly. [Microsoft ASP.NET Core dependency injection](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection)
- DAPR HTTP service invocation may retry transient failures, and non-streaming requests with known content length remain retryable. A valid partial handler failure must therefore be a `200` versioned application outcome, and transport retries must reuse stable dispatch/batch identity. [DAPR service invocation overview](https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/)
- DAPR retry guidance treats `5xx`/timeouts as retry candidates and supports policy/status-code filters. Do not make per-projection business/durable outcomes indistinguishable from transport failure. [DAPR retry policies](https://docs.dapr.io/operations/resiliency/policies/retries/retries-overview/)
- The local Story 1.10 record observed DAPR CLI `1.18.0` and runtime `1.18.1`, distinct from the `1.18.4` .NET SDK package. Re-record actual runtime evidence for this story.

### Testing And Validation

Run restore/build through the `.slnx`; run tests per project. For xUnit v3 focused filters, build first and invoke the built assembly with `-class`/`-method` rather than relying on project-level `--filter`.

```bash
dotnet restore Hexalith.EventStore.slnx
dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release
dotnet test tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Release
dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Release
dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --configuration Release
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Projections.ProjectionUpdateOrchestratorTests
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ReadModelBatchLiveSidecarTests
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.AsynchronousMultiProjectionDispatchLiveSidecarTests
git diff --check
```

The live DAPR/Redis lane is a completion gate. If environment-blocked, record the exact blocker separately; deterministic tests do not substitute for persisted detail/index/receipt/checkpoint evidence.

### Project Structure Notes

Expected new production types stay within existing packages and one type per file:

- `src/Hexalith.EventStore.Contracts/Projections/` — v2 request/response/outcome/status/capability contracts.
- `src/Hexalith.EventStore.DomainService/` — named async handler, legacy adapter, v2 dispatcher/options/validator helpers.
- `src/Hexalith.EventStore.Server/Projections/` — small named-route catalog, dispatch/reconciliation collaborator, and durable immediate-mode retry scheduler/worker rather than more inline orchestrator complexity.
- `tests/.../Projections/` and DomainService fixture folders — focused contract/dispatch/server/live evidence.

Do not add a project, package, Dockerfile, AppHost resource, DAPR component, or release-manifest entry.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md`, Parties Projection/Query Parity Gate and Story 1.12]
- [Source: `_bmad-output/planning-artifacts/prd.md`, FR7, FR36, NFR7, NFR12, NFR16]
- [Source: `_bmad-output/planning-artifacts/architecture.md`, AD-2, AD-7, AD-8, AD-12, AD-19, AD-20]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11.md`, sections 2.2, 2.4, 4.2, and 4.7]
- [Source: `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md`, Additional Blocking SDK Constraints]
- [Source: `_bmad-output/implementation-artifacts/1-10-coordinated-read-model-batch-writes.md`, Resolved Contract Decisions and Dev Agent Record]
- [Source: `_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md`, Boundaries & Constraints and Auto Run Result]
- [Source: `_bmad-output/project-context.md`]
- [Source: `references/Hexalith.AI.Tools/hexalith-state-instructions.md`]
- [Source: `src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs`]
- [Source: `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`]
- [Source: `src/Hexalith.EventStore.DomainService/DomainProjectionHandlerRouteValidator.cs`]
- [Source: `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`]
- [Source: `src/Hexalith.EventStore.DomainService/AdminOperationalIndexMetadata.cs`]
- [Source: `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`]
- [Source: `src/Hexalith.EventStore.Server/Projections/IProjectionDeliveryCheckpointStore.cs`]
- [Source: `src/Hexalith.EventStore.Client/Projections/IReadModelBatchStore.cs`]
- [Source: `src/Hexalith.EventStore.Client/Projections/ReadModelBatchResult.cs`]
- [Source: `references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs` and `Projections/ProjectionDispatcher.cs` — read-only compatibility evidence]

### Traceability Note

The Story 1.12 heading in `epics.md` names NFR7/NFR12, while the PRD high-risk matrix also maps NFR16 to Story 1.12. This story carries NFR16 explicitly because the required proof is persisted detail/index/receipt/checkpoint evidence. No UI artifact is changed; UX relevance is limited to preserving the rule that dispatch or transport success alone is not projection-confirmed user success.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Baseline/preflight: `5223e9c9c2f0dd71673003c710b8739efc8484ff`; approved correction recorded in `sprint-change-proposal-2026-07-13.md` before implementation continued.
- Release restore/build: `Hexalith.EventStore.slnx` — 48 projects restored/built, 0 warnings, 0 errors.
- Deterministic tests: Contracts 701/701; Client 663/663; DomainService 113/113; Testing 150/150; Sample 117/117; Server 2442 passed, 25 pre-existing quarantined ATDD specifications skipped.
- Tier-3 live-sidecar tests: 34/34 passed, including Story 1.10 `ReadModelBatchLiveSidecarTests` and Story 1.12 `NamedProjectionDispatchLiveSidecarTests`.
- Live evidence versions: Dapr.Client 1.18.4; DAPR CLI 1.18.0; DAPR runtime 1.18.1; Redis `docker.io/redis:6`; Docker Server 29.4.3; StackExchange.Redis 3.0.11.
- Final hygiene: `git diff --check` passed.

### Completion Notes List

- Added additive v2 contracts, exact capability-bound route catalogs, scoped async handler discovery, explicit legacy mapping, deterministic sequential dispatch, and truthful closed per-route outcomes without changing v1 wire/API behavior.
- Added pre-persistence drift/lifecycle admission, independent checkpoint reconciliation, state-bearing write/ETag/checkpoint ordering, and a durable payload-free retry scheduler/worker with stable dispatch identity, bounded backoff, terminal retention, and recorded-head reconstruction.
- Persisted the exact named route set into admin operational indexes and published the runtime catalog only after a complete successful metadata load/index write.
- Proved detail/index partial failure and convergence through real AggregateActor -> EventStore orchestrator -> DAPR `/project/v2` -> batch store -> Redis, including independent checkpoints, durable receipts, scheduler/worker recreation, empty converged ledger, and duplicate `AlreadyCompleted` receipts without mutation.
- Preserved legacy synchronous Sample projections and the v1 rebuild path; the verified rebuild lane never invokes named persistence handlers.
- Added named projection/read-model authoring guidance and updated the repository-owned AI context. No AppHost/DAPR YAML, release inventory, generated REST/UI surface, package, or project was added or changed for Story 1.12.

### File List

- `_bmad-output/implementation-artifacts/1-12-asynchronous-multi-projection-dispatch.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/prd.md`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-13.md`
- `_bmad-output/project-context.md`
- `docs/brownfield/integration-architecture.md`
- `docs/guides/named-projection-authoring.md`
- `docs/index.md`
- `src/Hexalith.EventStore.Client/Projections/ProjectionDispatchOptions.cs`
- `src/Hexalith.EventStore.Client/Projections/ProjectionDispatchRoute.cs`
- `src/Hexalith.EventStore.Client/Projections/ProjectionRouteCatalogFingerprint.cs`
- `src/Hexalith.EventStore.Contracts/Projections/ProjectionDispatchOutcome.cs`
- `src/Hexalith.EventStore.Contracts/Projections/ProjectionDispatchProtocol.cs`
- `src/Hexalith.EventStore.Contracts/Projections/ProjectionDispatchReasonCodes.cs`
- `src/Hexalith.EventStore.Contracts/Projections/ProjectionDispatchRequest.cs`
- `src/Hexalith.EventStore.Contracts/Projections/ProjectionDispatchResponse.cs`
- `src/Hexalith.EventStore.Contracts/Projections/ProjectionDispatchStatus.cs`
- `src/Hexalith.EventStore.DomainService/AdminOperationalIndexMetadata.cs`
- `src/Hexalith.EventStore.DomainService/DomainProjectionCatalogRegistry.cs`
- `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
- `src/Hexalith.EventStore.DomainService/DomainProjectionHandlerResult.cs`
- `src/Hexalith.EventStore.DomainService/DomainProjectionHandlerRouteValidator.cs`
- `src/Hexalith.EventStore.DomainService/DomainProjectionServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`
- `src/Hexalith.EventStore.DomainService/IAsyncDomainProjectionHandler.cs`
- `src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs`
- `src/Hexalith.EventStore.DomainService/LegacyDomainProjectionHandlerAdapter.cs`
- `src/Hexalith.EventStore.DomainService/ProjectionDispatchValidationException.cs`
- `src/Hexalith.EventStore.DomainService/ReadModelBatchProjectionResultMapper.cs`
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Server/Projections/DaprProjectionDeliveryRetryScheduler.cs`
- `src/Hexalith.EventStore.Server/Projections/INamedProjectionDispatchCoordinator.cs`
- `src/Hexalith.EventStore.Server/Projections/INamedProjectionRouteCatalog.cs`
- `src/Hexalith.EventStore.Server/Projections/IProjectionDeliveryRetryScheduler.cs`
- `src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs`
- `src/Hexalith.EventStore.Server/Projections/NamedProjectionRouteCatalog.cs`
- `src/Hexalith.EventStore.Server/Projections/NamedProjectionRouteCatalogEntry.cs`
- `src/Hexalith.EventStore.Server/Projections/NamedProjectionRouteCatalogSnapshot.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryRetryLedger.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryRetryWorkItem.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryRetryWorker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionEventReadabilityResult.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionEventWireBuilder.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs`
- `tests/Hexalith.EventStore.Client.Tests/Indexes/AdminOperationalIndexHostedServiceTests.cs`
- `tests/Hexalith.EventStore.Client.Tests/Projections/ProjectionDispatchOptionsTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionDispatchContractTests.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionDispatcherV2Tests.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionHandlerCompatibilityTests.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/Fixtures/WidgetAsyncProjectionHandler.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveCounterDetailProjectionHandler.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveCounterIndexProjectionHandler.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveNamedProjectionFaultControl.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionDispatchCoordinatorTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionRouteCatalogTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryRetryWorkerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDispatchHttpMessageHandler.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-13 | Implemented additive asynchronous named multi-projection dispatch, exact route metadata/catalog admission, independent checkpoint reconciliation, durable immediate retry, persisted DAPR/Redis evidence, compatibility tests, and authoring guidance; moved the story to review. |
| 2026-07-13 | Applied the approved sprint correction: Story 1.12 may proceed independently of unresolved sibling-story review findings while Story 1.15 retains the complete-and-reviewed convergence gate. |
| 2026-07-12 | Created comprehensive ready-for-dev Story 1.12 context with additive v2 compatibility, pre-persistence admission, deterministic fan-out, partial-failure checkpoint truth, rebuild safety, and persisted evidence gates. |

### Review Findings

Code review 2026-07-13 (baseline `5223e9c9` → HEAD, 55 code files / +3898 −118). Four adversarial layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor); no layer failed. Spec conformance is strong — ACs 1–6 substantially MET, `ConfigureAwait(false)` clean, no `Guid.TryParse`, per-projection checkpoint truth correct.

**Fix status (2026-07-13):** 7 patches applied and verified green — deterministic (Contracts 701/701, Client 663/663, DomainService 113/113, Server 2467 with 25 pre-existing skips) **and Tier-3 live-sidecar on real DAPR 1.18.1 + Redis 6**: `NamedProjectionDispatchLiveSidecarTests` 1/1 and `ReadModelBatchLiveSidecarTests` 3/3 green, with the partial-failure→converge path (`counter-detail=Completed`, `counter-index=Retryable`→retry→`Completed`) exercising the patched coordinator reconciliation; no orphaned `daprd`. 2 patches remain as action items (P7 CI tests, P8 ledger re-architecture — need a design decision). 1 patch reverted (P10 — the fail-closed guard was the wrong fix; see the corrected D3 below). 5 deferred, 4 dismissed. Story stays **in-progress** pending P7/P8 and the corrected D3.

**Decision-needed (resolved 2026-07-13)**

- [x] [Review][Decision] Single global retry-ledger key architecture (HIGH) → **Re-architect now** (Patch P8, action item — not yet applied). Partition the ledger off the single `projection-delivery-retry:ledger:v1` key (per-aggregate/tenant keys or index+shard) so it no longer contends/grows/wedges at scale. [blind-hunter+edge-case-hunter] [src/Hexalith.EventStore.Server/Projections/DaprProjectionDeliveryRetryScheduler.cs:14]
- [x] [Review][Decision] No terminal/dead-letter/cleanup taxonomy for non-converging retry work (MEDIUM) → **Patch safe ones (Patch P9 — applied), defer rest**. Applied: drift-ahead (`deliveredSequence > head`) is now terminal for the work item. Deferred to Story 1.13 + a cleanup-policy story: poison retry ceiling / dead-letter, fingerprint/version re-bind, permanent-4xx handling, terminal-only cleanup. [blind-hunter+edge-case-hunter] [src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:112]
- [ ] [Review][Decision] Legacy projection starved when a domain also has named routes (MEDIUM, AC5 / Resolved Contract #1) — **RE-OPENED. Original resolution (fail-closed startup guard, P10) was REVERTED**: applying it broke 4 `UseEventStoreDomainService_*` tests because the frozen spec §1 ("an unmapped legacy handler remains v1-only") and the story's own fixtures (`WidgetAsyncProjectionHandler` native named handler + legacy `WidgetProjection`, both domain `widget`) intentionally support legacy/named coexistence per domain; the DomainService correctly serves both `/project` and `/project/v2`. The real defect is that the EventStore **orchestrator** (`ProjectionUpdateOrchestrator.DeliverProjectionAsync:171-181`) fully skips the v1 `/project` call once the v2 catalog is present, so an unadapted legacy projection stops updating. Correct fix (D3 option b) is orchestrator-side: still deliver v1 legacy routes alongside v2 named routes for the same domain — a larger change needing its own tests + Tier-3. Decide: implement orchestrator coexistence now, or defer as a follow-up. [blind-hunter+acceptance-auditor] [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:171]

**Patch**

- [x] [Review][Patch] Retry worker has no top-level exception guard → host crash (HIGH) — **applied**: `ExecuteAsync` now wraps each `RunOnceAsync` tick in try/catch (rethrow shutdown OCE, else log-and-continue), and the `ProcessAsync` in-catch `DeferAsync` is guarded, so a CAS-exhaustion/`DaprException` no longer faults the `BackgroundService` (default `StopHost`). [blind-hunter+edge-case-hunter] [src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryRetryWorker.cs:33]
- [x] [Review][Patch] Coordinator ledger faults propagate into live delivery and drop it silently (MEDIUM) — **applied**: `ScheduleAsync`/reconcile/`DeferAsync` ledger calls are guarded (log + treat as v2-owned defer, never fall through to v1), and a null/blank `head.MessageId` is caught before building a work item. [edge-case-hunter+blind-hunter] [src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:86]
- [x] [Review][Patch] Catalog fingerprint basis diverges from emitted metadata (MEDIUM, PLAUSIBLE) — **applied**: `CreateCore` now computes the fingerprint over exactly the emitted (aggregate-backed) route set, and presents as a non-v2 service when no route is emitted, so an orphan named handler can no longer reject the whole catalog. [blind-hunter] [src/Hexalith.EventStore.DomainService/AdminOperationalIndexMetadata.cs:118]
- [x] [Review][Patch] Worker/live double-dispatch & work-item resurrection (MEDIUM) — **applied**: initial `NextDueUtc = now + RetryWorkerInterval`; `UpdateAsync` is update-only (never resurrects a converged/deleted `WorkId`). [blind-hunter+edge-case-hunter] [src/Hexalith.EventStore.Server/Projections/DaprProjectionDeliveryRetryScheduler.cs:38]
- [x] [Review][Patch] `MaxRetryAttempts` overloaded as the worker's per-tick scan page size (LOW-MEDIUM) — **applied**: added `RetryScanBatchSize` (default 64) + validation; the worker uses it for `GetDueAsync`, leaving `MaxRetryAttempts` for backoff only. [blind-hunter+edge-case-hunter] [src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryRetryWorker.cs:45]
- [x] [Review][Patch] `/project/v2` validates only `Request.Domain` (LOW-MEDIUM) — **applied**: `ValidateRequest` now rejects null/blank `Request.TenantId`/`Request.AggregateId`. [edge-case-hunter] [src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs:117]
- [x] [Review][Patch] (from D2) Drift-ahead route becomes terminal instead of deferring forever (MEDIUM) — **applied**: drift-ahead routes are collected into a settled set and pruned in a unified `ReconcileRetryLedgerAsync`, so the work item can converge/delete instead of deferring that route forever. [blind-hunter+edge-case-hunter] [src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:112]
- [ ] [Review][Patch] CI verification gaps on the durable-retry backbone and production wiring (MEDIUM, ACTION ITEM) — exercised only by the CI-excluded Tier-3 suite or not at all: the whole `DaprProjectionDeliveryRetryScheduler`, `AdminOperationalIndexHostedService.StartAsync` (Replace + fail-closed skip), `RegisterNamedProjectionCatalog` + v2 endpoint exception→`BadRequest` mapping, the orchestrator v2 short-circuit positive branch, the worker head-match/rebuild/unreadable/exception guards, dispatcher request-validation/envelope-overflow branches, the `ReadModelBatchProjectionResultMapper` default arm, and named-only-domain rebuild safe-defer. Add fake-`DaprClient`/substitute unit tests so regressions fail the CI gate. Depends on P8's final scheduler shape. [verification-gap] [tests/Hexalith.EventStore.Server.Tests/Projections/]
- [ ] [Review][Patch] (from D1) Re-architect the retry ledger off the single global key (HIGH, ACTION ITEM) — replace the single `projection-delivery-retry:ledger:v1` state key with a partitioned layout (per-aggregate or per-tenant keys, or an index + sharded records). Needs a state-store index-capability decision and Tier-3 validation. Preserve idempotent get-or-create by `WorkId`, `GetDueAsync` ordering, and restart-safety. [blind-hunter+edge-case-hunter] [src/Hexalith.EventStore.Server/Projections/DaprProjectionDeliveryRetryScheduler.cs:14]

**Deferred**

- [x] [Review][Defer] `HasFailures` blast radius on named-metadata rejection [src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs:37] — deferred: atomic all-or-nothing publish is spec-mandated (§2); cross-app coupling + startup-only load with no refresh is a broader platform concern. [verification-gap]
- [x] [Review][Defer] `DomainProjectionHandlerResult.AlreadyCompleted()` has no state overload [src/Hexalith.EventStore.DomainService/DomainProjectionHandlerResult.cs:25] — deferred: Contract #3/#5 gap for a hand-written state-bearing handler returning `AlreadyCompleted()` on retry (checkpoint advances without the deferred actor write); adapter + batch handlers unaffected, so narrow. Recommend adding `AlreadyCompleted(JsonElement? state)`. [acceptance-auditor]
- [x] [Review][Defer] `WorkId` omits app id/version/fingerprint [src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryRetryWorkItem.cs:44] — deferred: two `(appId, serviceVersion)` bindings for the same domain+head collide on one work item, stalling the second binding's retries. Affects blue/green or multi-version rollout of the same domain. [blind-hunter]
- [x] [Review][Defer] `DomainProjectionCatalogRegistry` empty after a domain-service restart [src/Hexalith.EventStore.DomainService/DomainProjectionCatalogRegistry.cs:8] — deferred: until the gateway re-queries metadata (startup-only), `Contains(fingerprint)` is false → 400 → coordinator defers. Overlaps the refresh-cadence concern above. [edge-case-hunter]
- [x] [Review][Defer] (from D2) Retry taxonomy remainder — poison ceiling / dead-letter, catalog fingerprint/version re-bind, permanent-4xx handling, terminal-only cleanup [src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:227] — deferred to Story 1.13 (poison/dedup horizon) + a dedicated retry-cleanup-policy story; drift-ahead is handled now (Patch from D2). A `4xx` from `/project/v2` can be a transient metadata-refresh race, so terminal-4xx handling needs the same story's design. [blind-hunter+edge-case-hunter]
