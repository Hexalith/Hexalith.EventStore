---
baseline_commit: 88aa5c2c2dcb4ca85e4e36cd5455b4e7742d0fc9
created: 2026-07-11
---

# Story 1.14: Correct Paged Rebuild And Replay Equivalence

Status: review

**Requirements covered:** FR7, FR33, FR36, NFR7, NFR8, NFR16
**Governed by:** AD-20 (controlling invariant), AD-19, AD-13, AD-8, AD-7, AD-15, AD-14, AD-12, AD-6, AD-5, AD-2
**Depends on:** Story 1.9 rebuild-checkpoint surface (`IProjectionRebuildCheckpointStore` was deliberately left untouched and reserved as *this* story's correctness surface); Story 1.10 coordinated-batch / resumable-marker primitive (`ReadModelWritePolicy`, `IReadModelBatchStore`); Story 1.11 `Rebuilding` lifecycle state (`done`)
**Feeds:** Story 1.15 parity closure — "full rebuild verification against aggregate replay" is a named 1.8/1.15 parity gate; Stories 6.3/6.4 optimize checkpoint/tail-delivery/replay cost **on top of** this correctness baseline and must not weaken it (AD-13)

> **⚠️ SEQUENCING RISK — READ BEFORE STARTING.** This story's natural upstream contracts, **Story 1.12 (async one-to-many `(Domain, ProjectionType)` dispatch)** and **Story 1.13 (MessageId-dedup / checkpoint-advance idempotency)**, are both still `backlog`. Story 1.10 is only `ready-for-dev`. Only Story 1.11 is `done`. You must implement replay-equivalence against **what exists today** (see *Current state of files that will be updated*), and you must not assume the 1.12 multi-projection dispatch pipeline or the 1.13 checkpoint-advance contract exists. Where an AC references "promotion and checkpoints follow Stories 1.10–1.13" (AC5), honor the *current* checkpoint/marker mechanics and leave a seam that 1.12/1.13 can tighten — do not fabricate their contracts here. See **Dependencies & sequencing** in Dev Notes.

## Spec authority note

No frozen `spec-1-14-...` intent-contract file exists yet. Until one is authored and frozen via `bmad-spec`, the governing intent for this story is **epics.md Story 1.14** (the canonical acceptance criteria, reproduced below) plus **architecture invariant AD-20** (the controlling rule). This story deliberately does **not** contain a `## Resolved Contract Decisions` block: the Epic-1 house pattern is spec-first, and Story 1.9's review produced a **HIGH "governing-contract conflict"** finding precisely because a narrow story got ahead of — and contradicted — its frozen spec. **Do not encode contract decisions here that neither epics.md nor AD-20 states.** Where a design choice is genuinely open (see the three flagged decision points in *Architecture and compatibility guardrails*), stop and get it frozen rather than inventing weaker semantics. [Team decision on whether to author `spec-1-14` first is raised as an open question to the workflow owner.]

## Story

As an operator,
I want paged projection rebuilds to be replay-equivalent,
so that rebuilding a long stream cannot replace correct state with a partial-page model.

## The concrete defect this closes

The paged-rebuild subsystem already exists and is largely correct on its operator/lifecycle surface (checkpoints, active-rebuild index, pause/resume/cancel/retry, bounded `toPosition`, cancellation cleanup). **The replay-equivalence core is missing and actively wrong today:**

In `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`, `DeliverProjectionForRebuildAsync` (L580) reads a **bounded page** of events —
`aggregateProxy.ReadEventsRangeAsync(perAggregateProgress, toPosition, RebuildPageSize)` (L628, `RebuildPageSize = 256` const at L954) — and sends *only that page window* to the `/project` handler. But `/project` (`IDomainProjectionHandler.Project`) is a **stateless full-replay** handler: it "rebuilds the read model from scratch… holds no state between calls and does not read or persist prior projection state." The orchestrator then does
`writeProxy.UpdateProjectionAsync(ProjectionState.FromJsonElement(...))` (L808), which the projection actor persists as a **single-key overwrite** (`EventReplayProjectionActor.ProjectionStateKey = "projection-state"`, `SetStateAsync` + `SaveStateAsync`).

**Result:** a page is presented to a full-replay handler as though it were the complete stream, and the resulting page-only model **overwrites the last complete live model**. For any stream longer than one page, the persisted projection ends up derived from only the final page — the exact `NFR8` failure ("never overwrite a complete live model with page-only state") and `AD-20` violation this story exists to fix.

## Acceptance Criteria

1. **Explicit rebuild semantics per handler.** When a projection handler participates in rebuild and its contract is inspected, it declares or is adapted to **full-replay** or **incremental** semantics: full-replay handlers receive the **complete required prefix** (a page is never presented as a complete stream); incremental handlers receive **prior staged state plus a contiguous page**. Given today's handler is stateless full-replay and `Domain`-keyed only, the rebuild path must guarantee the full-replay handler is fed a complete prefix (accumulate to the boundary) rather than a lone page — and must not silently treat a page as the whole stream. (AD-20, epics AC1)

2. **Operation-scoped staging preserves the live model while work is incomplete.** When paged rebuild begins and work is incomplete, operation-scoped staging (or equivalent non-live isolation) holds the in-flight detail/index state, and the **last complete live model is not overwritten** until every required projection for the operation completes durably. (AD-20, epics AC2, NFR8)

3. **Bounded reads are page-safe and position-equivalent.** When a stream exceeds the configured page size and rebuild reads every page, page boundaries **neither duplicate, skip, nor reorder** events, and a bounded `toPosition` produces the **same result as canonical replay through that position** (`AggregateReplayer.Replay<TState>` is the equivalence oracle). (AD-20, AD-6, epics AC3)

4. **Cancel / failure never corrupts the live model or misreports progress.** When rebuild is canceled or a handler/store fails and the operation stops then resumes, the **last complete live model remains intact**, progress resumes from a **safe boundary**, and **page-read progress is never reported as projection completion**. (AD-20, AD-8, epics AC4)

5. **Coordinated detail+index promotion; no partial-success reporting.** When detail and index projections rebuild together and the operation outcome is written, each projection remains **independently observable**, but the operation **cannot report success while any required projection is incomplete**; promotion and checkpoints follow the current coordinated-write/marker mechanics established by Stories 1.10–1.13 (see Sequencing risk — honor what exists, seam for what doesn't). (AD-20, AD-19, epics AC5)

6. **Replay-equivalence evidence through the production path.** When a fixture **larger than two pages** runs through canonical replay / full-sequence projection **and** the production paged-rebuild orchestrator, persisted **detail, index, projection versions, and checkpoints are semantically equal**; tests also cover **empty streams, exact page boundaries, bounded positions, cancellation, failure, and resume**. Projection-version equality must be sourced from persisted read-model freshness (`IReadModelFreshness`), never aliased from an ETag (AD-15). Evidence is asserted on **persisted state through the real orchestrator/`/project`/store path** — mock-only or isolated-aggregate-replayer proof does not satisfy this AC (AD-12, AD-8, Story 1.15 AC3). (AD-20, NFR16, epics AC6)

7. **Correctness precedes cost; a temporary full-sequence strategy is safety-bounded.** If a temporary full-sequence (feed-complete-prefix) strategy is used, it has an **explicit safety bound and failure mode**, and Stories 6.3/6.4 may later optimize it **without changing equivalence guarantees**. This story sets the minimum correctness baseline that AD-13 forbids 6.3/6.4 from weakening (duplicate, gap, page-safety, staging, promotion, replay-equivalence). (AD-13, epics AC7)

8. **`Rebuilding` lifecycle is truthful.** The rebuild orchestrator surfaces the `ProjectionLifecycleState.Rebuilding` (=3) state while an operation is in flight and clears it to `Current`/`Stale` **only on durable promotion** — never leaving a stale `Rebuilding`, and never inferring lifecycle from ETag/HTTP/payload/SignalR. Only `ProjectionBacked` provenance may carry the authoritative lifecycle. (AD-15, AD-8, Story 1.11 AC2 — additive tie-in; do not reopen 1.11 route-selection scope.)

## Tasks / Subtasks

- [x] **Task 1 — Establish the equivalence oracle and its harness (AC: 3, 6)**
  - [x] Add a deterministic test fixture that projects a stream via canonical `AggregateReplayer.Replay<TState>` (full-sequence) into the same detail + index read models used by the domain projection, producing the reference persisted state, projection versions, and checkpoint.
  - [x] Use the existing detail/index test models `AggregateReadModel` (detail: `Id`, `Status`) and `AggregateIndexReadModel` (index: `List<string> AggregateIds`) from `tests/Hexalith.EventStore.Client.Tests/Projections/`, or an equivalent Server-side pair; keep the reference builder side-effect-free.
  - [x] Assert the oracle rejects gaps/duplicates and requires sequence-starts-at-1 exactly as `AggregateReplayer` does (do not reimplement replay semantics).

- [x] **Task 2 — Make the rebuild delivery feed a complete prefix / stage, never overwrite with a page (AC: 1, 2, 4)**
  - [x] In `ProjectionUpdateOrchestrator.DeliverProjectionForRebuildAsync`, stop presenting a lone page to the stateless full-replay `/project` handler as the whole stream. Either (a) accumulate the complete required prefix up to the page/`toPosition` boundary before invoking the full-replay handler, or (b) hold page outputs in operation-scoped staging and only compute/promote the complete-prefix result. **Choose per the frozen spec / decision points below; do not weaken to page-only.**
  - [x] Introduce operation-scoped staging (non-live isolation) so the live `projection-state` key (and any detail/index read-model key) is **not** mutated until promotion. Do not overwrite `EventReplayProjectionActor.ProjectionStateKey` mid-operation.
  - [x] Preserve the last complete live model on cancel/failure; resume from the last durable safe boundary using `ProjectionRebuildCheckpoint.LastAppliedSequence`/`ToPosition`.

- [x] **Task 3 — Promote atomically-enough after all required projections complete (AC: 2, 5)**
  - [x] Add a promotion step that swaps staged detail+index outputs into the live slot only after every required projection for the operation is durably complete; reuse Story 1.10's coordinated-batch / resumable-marker primitive (`ReadModelWritePolicy` / `IReadModelBatchStore`) rather than inventing a new marker.
  - [x] Ensure the operation cannot report `Succeeded` while any required projection is incomplete; keep per-projection outcomes distinguishable.
  - [x] Advance rebuild checkpoints (`IProjectionRebuildCheckpointStore.SaveAsync`) only after promotion is durable; never advance a failed projection as success (AD-19). Do not advance the *delivery* checkpoint (`IProjectionCheckpointTracker`) outside the 1.13 contract — leave that seam intact.

- [x] **Task 4 — Make rebuild page size configurable and prove page-boundary safety (AC: 3)**
  - [x] Replace the hard-coded `private const int RebuildPageSize = 256` with a value on `ProjectionOptions` (e.g. `RebuildPageSize`, validated `> 0` in `ProjectionOptions.Validate()`), defaulting to 256 for compatibility. Keep the existing `events.Length < RebuildPageSize` page-complete detection correct for the new configurable size (including the exact-boundary case documented at L863–868).
  - [x] Prove no duplicate/skip/reorder across page boundaries for streams of length {0, exactly pageSize, pageSize+1, N×pageSize, N×pageSize+1}.

- [x] **Task 5 — Surface a truthful `Rebuilding` lifecycle (AC: 8)**
  - [x] Ensure an in-flight rebuild is observable as `ProjectionLifecycleState.Rebuilding` and clears to `Current`/`Stale` only on durable promotion; never leave a stale `Rebuilding` after terminal Succeeded/Failed/Canceled.
  - [x] Do not infer lifecycle from ETag/HTTP/payload/SignalR; only `ProjectionBacked` provenance carries the authoritative state (reuse `ProjectionLifecyclePolicy`; do not reopen Story 1.11/2.8 route-selection scope).

- [x] **Task 6 — Replay-equivalence + edge-case test corpus (AC: 3, 4, 6, 7)**
  - [x] Add tests in `tests/Hexalith.EventStore.Server.Tests/Projections/` (alongside `ProjectionUpdateOrchestratorTests.cs`) asserting persisted **detail, index, projection versions, and checkpoints** from the production paged-rebuild orchestrator are **semantically equal** to the Task-1 canonical oracle for a fixture **> 2 pages**.
  - [x] Cover: empty stream, exact page boundary, `toPosition`-bounded rebuild, mid-rebuild cancellation (live model intact), injected handler/store failure (live model intact, retry converges), and resume-from-progress.
  - [x] Assert **persisted end-state** (via `InMemoryReadModelStore.Snapshot<T>()` / captured store state and the projection-state key), **not** mock call counts or `202`/`200` — recorder call counts are request-shape evidence only (NFR16 / AD-12).
  - [x] Add a targeted regression test that fails on the current overwrite bug (page-only state replacing a complete model) and passes only after staging/promotion lands.

- [x] **Task 7 — Guardrails, build, and (if reachable) live-sidecar evidence (AC: 6)**
  - [x] `ConfigureAwait(false)` on every new production await (CA2007 is build-breaking). File-scoped namespaces, one type per file, no copyright headers, ULID-safe identity (`Ulid.TryParse`, never `Guid.TryParse`), central package versions only.
  - [x] If a live-DAPR/Redis lane is reachable (`tests/Hexalith.EventStore.Server.LiveSidecar.Tests/`, `DaprFactAttribute`), assert the equivalence corpus against persisted Redis state; if environment-blocked, record the exact blocker separately — deterministic tests do not substitute for persisted DAPR/Redis evidence but do not block the story on an unavailable environment.

## Dev Notes

### Current state of files that will be updated

Read these completely before touching them. The rebuild subsystem is large and mostly correct on its operator/lifecycle surface — the defect is localized to how the *page window* is applied. Preserve every existing behavior not explicitly changed by an AC.

- **`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`** (~1341 lines) — `partial class ProjectionUpdateOrchestrator : IProjectionUpdateOrchestrator, IProjectionPollerDeliveryGateway, IProjectionRebuildOrchestrator`.
  - `RebuildProjectionAsync(ProjectionRebuildCheckpointScope, CancellationToken)` — enumerates tracked identities (`checkpointTracker.EnumerateTrackedIdentitiesAsync`), matches scope, drives per-aggregate paged delivery, writes terminal Succeeded/Failed/Canceled, cleans up on cancellation. **Keep this control flow.**
  - `DeliverProjectionForRebuildAsync(...)` (L580) — **the surface to fix.** L628 reads the bounded page (`ReadEventsRangeAsync(perAggregateProgress, toPosition, RebuildPageSize)`); L808 overwrites live state (`UpdateProjectionAsync(ProjectionState.FromJsonElement(response.ProjectionType, identity.TenantId, response.State))`); L863–868 does exact-page-boundary detection (`pageComplete = events.Length < RebuildPageSize`); L954 `const int RebuildPageSize = 256`.
  - The **immediate/poller delivery path** (L53 `UpdateProjectionAsync(identity, ...)`, L239 overwrite) uses **full replay** (`aggregateProxy.GetEventsAsync(0)`) and is documented as "the safe immediate-delivery contract." Do **not** regress this path; the bug is specific to the *paged* rebuild path feeding partial windows.
  - Existing drift guard: reads delivery checkpoint and returns on `lastDeliveredSequence > highestAvailableSequence` (`Log.CheckpointDriftDetected`). Note (do not blindly copy): the poller path writes projection output and checkpoint *separately* and tolerates checkpoint failure after model persistence — an anti-pattern flagged in prior reviews; your staging/promotion must not adopt "persist model, then best-effort checkpoint."
- **`src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs`** — `IProjectionWriteActor.UpdateProjectionAsync(ProjectionState)`; `internal const string ProjectionStateKey = "projection-state"`; single-key `SetStateAsync` + `SaveStateAsync` + ETag regeneration + SignalR broadcast. **No staged/promoted slot exists** — you will add operation-scoped isolation here or in the orchestrator (decision point below).
- **`src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs`** — has `CheckpointStateStoreName` (default `"statestore"`), `DefaultRefreshIntervalMs`, `Domains`, `RebuildIndexCleanupCadenceSeconds` (default 60), and `Validate()`. **No rebuild page size** — add one (Task 4).
- **`src/Hexalith.EventStore.Server/Projections/IProjectionRebuildCheckpointStore.cs` + `ProjectionRebuildCheckpointStore.cs`** (~1135 lines, DAPR) — **this story's reserved correctness surface** (Story 1.9 explicitly left it untouched: *"This store is Story 1.14's correctness surface, not a per-aggregate delivery high-water mark. Do not add erasure here."*). Keys `projection-rebuild-checkpoints:{tenant}:{domain}:{projection}:{aggregateId|*}`, active index `projection-rebuild-active-index:{tenant}:{domain}`, pair index `projection-rebuild-active-index-pairs`. Members: `ReadAsync`, monotonic/lifecycle-guarded `SaveAsync(scope, lastAppliedSequence, status, failureReasonCode, ct, toPosition, isPerAggregateProgress)`, guard-bypassing operator `ResetAsync`, `HasActiveOperatorRebuildForDomainAsync`, `ListActiveRebuildIndexPairsAsync`, `ClearOrphanActiveRebuildIndexEntriesAsync`. Contract snapshot: `Contracts/Streams/ProjectionRebuildCheckpoint.cs` (`LastAppliedSequence`, `Status`, `ToPosition`, `OperationId`, `FailureReasonCode`).
- **`src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs`** — `Project(ProjectionRequest) → ProjectionResponse`; **stateless full-replay, Domain-keyed only**, "holds no state between calls and does not read or persist prior projection state." The `(Domain, ProjectionType)` keying and incremental handlers are Story 1.12/AD-19 territory (backlog) — do not add them here. If AC1 requires incremental semantics, either (a) keep full-replay and feed the complete prefix, or (b) get the incremental contract frozen first.
- **`src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs`** — `[Route("api/v1/admin/projections")]`, GlobalAdministrator-gated; `replay` → `RebuildProjectionAsync`; writes the `Running` row **before** invoking the orchestrator (documented precondition — preserve it).

### Architecture and compatibility guardrails

- **AD-20 is the controlling invariant.** Rebuild output, **projection versions, and checkpoints must equal canonical aggregate replay for the same event prefix, including streams longer than the configured page size.** Paging is a read optimization, never a semantic projection boundary. Staging is non-live; promote only after all required projections complete.
- **Two read-model surfaces must be reconciled under one equivalence guarantee.** Rebuild writes go through the **projection actor** (`EventReplayProjectionActor`, single `projection-state` key), while the **detail/index read-model store** (`IReadModelStore` / `ReadModelWritePolicy`, Story 1.10) is a *separate* surface the rebuild path does **not** touch today. AD-20 names **detail, index, projection versions, and checkpoints** — decide (and freeze) which surface(s) the equivalence assertion targets. **Decision point #1.**
- **Full-replay vs staging strategy. Decision point #2.** Today's `/project` is full-replay-only and Domain-keyed. Feeding the complete prefix (Task 2a) is the smallest correct change; operation-scoped staging with promotion (Task 2b/3) is the fuller AD-20 shape. Pick per the frozen spec; either way, **never present a page as a complete stream** and **never overwrite the live model with page-only state**.
- **Projection versions come from persisted freshness, not ETags (AD-15 rule 3).** `ProjectionVersion` / `IsStale` are authoritative only when provenance is `ProjectionBacked` and sourced from `IReadModelFreshness` via `ReadModelFreshnessExtensions.ToQueryResponseMetadata`. Never alias `ProjectionVersion := ETag`. The `QueryResponseMetadata.ETag` (`SelfRoutingETag.GenerateNew`) is a random change token, not a content hash or version.
- **Ordering / identity (AD-6, AD-8).** Aggregate sequence is gapless per aggregate; dedup uses `MessageId`; `SequenceNumber` is **never** treated as globally ordered. Bounded `toPosition`/`ToSequence` ties a paged read back to a canonical replay position. Serialization uses the one shared platform `JsonSerializerOptions` path (FR29) — do not introduce a private option set.
- **Checkpoint-advance seam. Decision point #3.** AC5 says "promotion and checkpoints follow Stories 1.10–1.13," but 1.12/1.13 are backlog. Advance the **rebuild** checkpoint after durable promotion; leave the **delivery** checkpoint (`IProjectionCheckpointTracker`) governed by its current contract and add only a seam that 1.13 can tighten. Do not implement 1.13's MessageId-dedup/checkpoint-advance contract here.
- **Do not reopen adjacent surfaces.** No single-key erase changes (Story 1.9 `ProjectionStateEraser`), no batch-marker semantic changes (Story 1.10), no route-selection/provenance changes (Story 2.8/1.11). Preserve released ABI on `IReadModelStore`, `ProjectionLifecycleState`, `QueryResponseMetadata`, and `ProjectionRebuildCheckpoint` — additive capability goes on new/opt-in types, never new required interface members (the 1.9/1.10 discipline).
- **Prior review lesson (directly relevant to your staging/promotion code):** do not infer atomicity from same-store placement (a DAPR "transactional" store can partially commit on ETag conflict while the SDK returns only request-level success/failure); narrow catches and return **structured outcomes**, not a bare `bool`; prove "retry converges" with persisted evidence rather than asserting it in an XML doc.

### Dependencies & sequencing

| Upstream | Status | What 1.14 may rely on today | What to seam, not assume |
| --- | --- | --- | --- |
| 1.9 rebuild-checkpoint surface | done | `IProjectionRebuildCheckpointStore` / `ProjectionRebuildCheckpoint` (reserved for this story) | — |
| 1.10 coordinated batch / marker | ready-for-dev | `ReadModelWritePolicy`, `IReadModelBatchStore`, resumable-marker pattern (once merged) | If 1.10 has not landed when you start, coordinate — do not re-implement batch semantics |
| 1.11 lifecycle | done | `ProjectionLifecycleState.Rebuilding`, `ProjectionLifecyclePolicy` | Do not reopen route-selection scope |
| 1.12 async `(Domain,ProjectionType)` dispatch | backlog | nothing | Keep Domain-keyed full-replay handler; leave dispatch seam |
| 1.13 idempotency / checkpoint-advance | backlog | nothing | Advance rebuild checkpoint only; leave delivery-checkpoint contract to 1.13 |
| 1.15 parity closure | backlog | — | 1.14 rebuild-equivalence is a named parity gate 1.15 verifies |

### Testing and validation

- **xUnit v3 + Shouldly** (`ShouldBe`, `ShouldBeTrue`, `ShouldThrow`) — never raw `Assert.*`. NSubstitute for mocks; extend the existing hand-rolled `RecordingDaprClient` for DAPR request-shape evidence.
- **Assert persisted state, not mock calls / status codes (NFR16, AD-12).** Use `InMemoryReadModelStore.Snapshot<T>()` and captured store/actor end-state. Recorder call counts are request-shape evidence only, never completion proof.
- **Server-owned types → Server test project.** New orchestrator/staging tests live in `tests/Hexalith.EventStore.Server.Tests/Projections/` (next to `ProjectionUpdateOrchestratorTests.cs`), **not** in the Client test project (Story 1.9 was dinged for inverting layering). Canonical-replay oracle tests may reuse `tests/Hexalith.EventStore.Client.Tests/Aggregates/AggregateReplayerTests.cs` patterns.
- **The mandated corpus (AC6):** fixture **> 2 pages** through canonical replay + production orchestrator → persisted detail, index, projection versions, checkpoints **semantically equal**; plus empty streams, exact page boundaries, bounded positions, cancellation, failure, resume.
- **No mock-only / isolated-replayer proof for handler-path requirements** (Story 1.15 AC3). Exercise the real orchestrator → `/project` → store path.
- **Run test projects individually** (never solution-level `dotnet test`). For xUnit v3 focused runs, build first then invoke the built assembly with `-class`/`-method`.
- **Two lanes:** deterministic in-memory (required, must be green) and live-DAPR/Redis (`LiveSidecar.Tests`, `DaprFactAttribute`) — authoritative for persisted evidence; if environment-blocked, record the exact blocker separately.
- **Strongest-story rule (retro):** finish with a targeted regression test for the exact defect (page-only overwrite), not only a broad green build.

### Previous Story Intelligence

- **Story 1.9** deliberately reserved `IProjectionRebuildCheckpointStore` for this story and warned it is "Story 1.14's correctness surface, not a per-aggregate delivery high-water mark." Its `ProjectionStateEraser` review produced five HIGH/MEDIUM findings whose lessons apply verbatim to your staging/promotion coordination (atomicity-from-same-store fallacy, key-prefix-as-authorization fallacy, unproven retry convergence, broad `catch → bool` on an AD-12 path, mock-not-persisted "proof").
- **Story 1.10** states, in §6, "Story 1.14 owns rebuild staging/promotion," and models the reusable **resumable marker / pending-envelope** pattern (previous complete value visible until a commit marker is durable) — mirror it for staging→promotion rather than a bespoke mechanism. 1.10 also pins that checkpoints are outside its durable batch unit and that later stories advance them only after proven completion.
- **Story 1.11** (`done`) added `ProjectionLifecycleState` (`Rebuilding = 3`) and `ProjectionLifecyclePolicy`; it explicitly deferred "rebuild correctness … owned by Stories 1.12–1.15." That's this story.
- **Story 1.8** named the exact defect ("paged rebuild delivery can overwrite full-replay state with a partial page") and left "full rebuild verification against aggregate replay" as a **blocked** parity item that 1.14 + 1.15 must close.
- **Epic-1 retro:** several stories were oversized and needed review rework; compatibility preservation and fail-closed behavior were repeatedly under-specified. Keep scope tight to AD-20; fail closed on unknown/partial/gap states; preserve released shapes.

### Latest Technical Information

- .NET SDK `10.0.301` (`rollForward: latestPatch`), TFM `net10.0`, `TreatWarningsAsErrors=true`, `Nullable`+`ImplicitUsings`. **CA2007 (`ConfigureAwait(false)`) is a build-breaker** on production awaits.
- DAPR .NET SDK is centrally pinned (`Directory.Packages.props`, `ManagePackageVersionsCentrally=true`) — never add versions to `.csproj`. `architecture.md#Stack` pins Dapr 1.18.4 / xUnit v3 3.2.2 / Shouldly 4.3.0; record CLI/runtime/SDK versions separately if you produce live evidence (do not infer runtime from the NuGet version).
- DAPR transactional store → one state transaction; non-transactional → an explicitly documented **resumable equivalent** with defined atomicity boundaries; "cross-store work is never described as atomic." DAPR and in-memory implementations must expose **equivalent observable semantics**.
- Identity: ULID-safe; `Guid.TryParse` on `messageId`/`correlationId`/`aggregateId`/`causationId` is forbidden (enforced by `ProtectedIdentifierGuidParserAuditTests`). Aggregate identity `ActorId => "{TenantId}:{Domain}:{AggregateId}"` — `:` cannot appear in a component.

### Project Structure Notes

- Rebuild orchestration / staging / promotion and actors live in **`src/Hexalith.EventStore.Server/`** (`Projections/`, `Actors/`, `Configuration/`). Read-model/cursor seams and the canonical replayer live in **`src/Hexalith.EventStore.Client/`**. Wire contracts (`ProjectionRebuildCheckpoint`, `ProjectionRequest/Response`, reason codes) live in **`src/Hexalith.EventStore.Contracts/`**. Test doubles live in **`src/Hexalith.EventStore.Testing/Fakes/`** (`InMemoryReadModelStore`, `FakeProjectionWriteActor`, `FakeAggregateActor`, `InMemoryStateManager`).
- Reason codes: extend `Contracts/Streams/StreamReplayReasonCodes.cs` (e.g. `ProjectionApplyRejected`, `CheckpointConflict`, `StaleCheckpoint`, `RebuildCanceled`, `OperatorPreempted`) rather than inventing ad-hoc strings.
- No new `*.AppHost` / `*.Aspire` / `*.ServiceDefaults` projects; keep everything in existing platform libraries (AD-2). `.slnx` only; no `.sln`; no Dockerfiles.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-1.14-Correct-Paged-Rebuild-And-Replay-Equivalence` (lines 853–894)] — canonical acceptance criteria.
- [Source: `_bmad-output/planning-artifacts/architecture.md#AD-20`] — Paged Rebuilds Are Replay-Equivalent (controlling invariant); also `#AD-19`, `#AD-13`, `#AD-8`, `#AD-7`, `#AD-15`, `#AD-14`, `#AD-12`, `#AD-6`, `#AD-5`, `#AD-2`.
- [Source: `_bmad-output/planning-artifacts/prd.md`] — FR7, FR33, FR36; NFR7, NFR8 ("Paged rebuild output must equal canonical aggregate replay and must never overwrite a complete live model with page-only state"), NFR16.
- [Source: `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`] — `DeliverProjectionForRebuildAsync` L580, bounded read L628, overwrite L808, page-boundary L863–868, `RebuildPageSize` L954.
- [Source: `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs`] — `ProjectionStateKey`, single-key overwrite.
- [Source: `src/Hexalith.EventStore.Server/Projections/IProjectionRebuildCheckpointStore.cs`, `ProjectionRebuildCheckpointStore.cs`; `src/Hexalith.EventStore.Contracts/Streams/ProjectionRebuildCheckpoint.cs`] — reserved correctness surface.
- [Source: `src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs`] — stateless full-replay, Domain-keyed contract.
- [Source: `src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs`; `Contracts/Replay/*`] — canonical replay equivalence oracle.
- [Source: `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`, `ReadModelWritePolicy.cs`; `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs`] — detail/index read-model store (Story 1.10).
- [Source: `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecycleState.cs`, `ProjectionLifecyclePolicy.cs`] — `Rebuilding = 3` lifecycle (Story 1.11).
- [Source: `_bmad-output/implementation-artifacts/1-10-coordinated-read-model-batch-writes.md` §6; `1-9-read-model-and-projection-checkpoint-erasure.md`; `epic-1-context.md`; `epic-1-retro-2026-07-07.md`; `1-8-projection-query-sdk-owner-parity-proof.md`] — prior-story intelligence.
- [Source: `_bmad-output/project-context.md`] — coding/testing/identity/build rules.

## Validation

```bash
# Restore/build the whole solution (build only — never solution-level `dotnet test`)
dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0

# Run the affected test projects individually (Tier 1)
dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Release
dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Release
dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Release

# Focused replay-equivalence run (xUnit v3: build first, then invoke the built assembly)
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
  -class Hexalith.EventStore.Server.Tests.Projections.ProjectionUpdateOrchestratorTests

# Optional live-DAPR/Redis lane (persisted-state evidence) if the environment is available
dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Release

# Hygiene
git diff --check
```

Expected: whole-solution Release build clean (warnings-as-errors); new replay-equivalence + edge-case tests green with **persisted** detail/index/version/checkpoint assertions; the targeted overwrite-regression test fails on the pre-change code and passes after staging/promotion; `git diff --check` clean.

## Dev Agent Record

### Agent Model Used

### Debug Log References

- 2026-07-15 Task 1 RED: Release compilation failed with CS0246/CS0103 because the equivalence oracle and fixture types did not exist.
- 2026-07-15 Task 1 GREEN: `ProjectionRebuildEquivalenceOracleTests` passed 5/5 after the fixture delegated replay validation to `AggregateReplayer`; the full Release server test assembly passed 2,595 tests with 0 failed/errors and 25 skipped in 513.205 seconds.
- 2026-07-15 Task 2 HALT: The canonical Story 1.14 spec's safety-bound gate still leaves the maximum complete-prefix limit and structured failure reason code open and explicitly requires approval before production implementation starts. Story 1.10/1.12 sequencing is complete; this is the remaining contract gap.
- 2026-07-15 Task 2 contract resolution: Approval froze server-wide limits of 10,000 events and 67,108,864 serialized bytes with failure reason `rebuild_prefix_safety_limit_exceeded`; the preservation-validated SPEC has zero open questions.
- 2026-07-15 Task 2 RED: The multi-page regression projected only the first 256 events, and a second-page read failure did not exercise the missing continuation because rebuild performed only one page read.
- 2026-07-15 Task 2 GREEN: Rebuild reconstructs the complete prefix from sequence one, invokes the stateless full-replay handler once, and keeps its response non-live until the prefix is complete; 62 orchestrator tests passed, followed by 2,597/2,597 server tests (0 failed/errors, 25 skipped) in 511.558 seconds.
- 2026-07-15 Task 3 RED: DomainService test compilation failed because named handlers had no explicit rebuild semantics/candidate-plan seam, and the server coordinator had no rebuild-specific dispatch contract.
- 2026-07-15 Task 3 GREEN: Full-replay named handlers prepare side-effect-free candidates that the domain endpoint promotes as one Story 1.10 batch; partial preparation/dispatch blocks actor promotion and rebuild checkpoint advancement. Focused server tests passed 77/77, DomainService passed 130/130, and the full server suite passed 2,600/2,600 (0 failed/errors, 25 skipped) in 486.640 seconds.
- 2026-07-15 Task 4 RED: Release compilation failed because `ProjectionOptions` did not expose the approved page-size or prefix safety settings.
- 2026-07-15 Task 4 GREEN: Configurable paging (default 256) validates positive values and rejects non-contiguous/oversized pages. The `{0, 3, 4, 6, 7}` corpus proved exact cursors and `/project` sequence ordering; focused tests passed 79/79 and the full server suite passed 2,608/2,608 (0 failed/errors, 25 skipped) in 531.254 seconds.
- 2026-07-15 Task 5 RED: Release compilation failed with 24 errors because the persisted lifecycle actor/gateway had no rebuild begin, complete, or read-phase contract and query routing could not consult authoritative lifecycle state.
- 2026-07-15 Task 5 GREEN: Rebuild now persists `Rebuilding` before reads, defers ordinary delivery/erase conflicts, promotes named and actor state before checkpoint/lifecycle completion, and clears lifecycle on success, failure, cancellation, and durable-bound resume. Focused lifecycle/query/orchestrator tests passed 144/144 and the full server suite passed 2,613/2,613 (0 failed/errors, 25 skipped) in 506.105 seconds.
- 2026-07-15 Task 6 RED: Safety-bound test compilation failed with four CS0117 errors because the approved `rebuild_prefix_safety_limit_exceeded` reason code and enforcement path did not exist.
- 2026-07-15 Task 6 GREEN: Event-count and serialized-byte ceilings now fail closed before any promotion; the real orchestrator/coordinator/domain-dispatcher/batch-store harness proved oracle-equivalent persisted actor, detail, index, freshness version, and rebuild checkpoints across seven edge scenarios. Focused tests passed 84/84, DomainService passed 131/131, and the full server suite passed 2,622/2,622 (0 failed/errors, 25 skipped) in 505.420 seconds.
- 2026-07-15 Task 7 GREEN: The 48-project Release build completed with 0 warnings/errors. Final affected suites passed: Server 2,623 total (0 failed/errors, 25 skipped), DomainService 132/132, Client 671/671, and Contracts 702/702. Guardrail review replaced the new live-test GUID aggregate identity with a sortable ULID; the warnings-as-errors build enforced production-await CA2007 compliance.
- 2026-07-15 Task 7 persisted evidence: `PagedRebuild_MoreThanTwoPages_PersistsEquivalentRedisActorDetailIndexAndCheckpoints` passed 1/1 through the real DAPR/Redis lane, proving equivalent persisted actor, detail, index, freshness version, operator checkpoint, lifecycle, and unchanged delivery checkpoints.
- 2026-07-15 Task 7 baseline note: The broader LiveSidecar assembly passed 43/44; the sole failure, existing `NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger`, reproduced at unchanged pre-story HEAD `fc1b930a84d9ba5fad34f8d059afe46d3d5b9ea3`, confirming it is not a Story 1.14 regression.
- 2026-07-15 extended integration note: The separate broad Aspire integration assembly could not complete reliably in this workspace: ephemeral endpoints became connection-refused and projection-writer protocol v2 never became ready. The interrupted runner reported 105 total, 33 failed, 1 skipped after 873.440 seconds. This environmental lane is outside the story's required Tier-1 and focused persisted-state gates; it is recorded here rather than represented as green.

### Completion Notes List

- Task 1: Added a deterministic, side-effect-free canonical replay oracle that produces semantic detail/index models, projection freshness/version, and checkpoint state from `AggregateReplayer` output.
- Task 1: Added canonical sequence validation coverage for non-one starts, gaps, and duplicates, plus repeated-invocation and input-immutability coverage.
- Task 2: Converted rebuild paging into a bounded-read implementation detail: every invocation reconstructs the complete prefix through `toPosition` or end-of-stream before projecting and promoting.
- Task 2: Kept the candidate response operation-local until all pages are read, preserving the prior live actor state on cancellation or later-page failure and making retry reconstruct the same safe prefix.
- Task 3: Added an opt-in named rebuild contract with explicit full-replay semantics and immutable candidate plans; all required named candidates are combined into one existing resumable batch under the stable rebuild operation identity.
- Task 3: Added `/project/rebuild/v1` and server coordination with bounded per-route outcomes; actor promotion and rebuild checkpoints follow named durability, while normal delivery checkpoints are intentionally untouched.
- Task 4: Replaced the rebuild constant with validated server configuration while retaining the compatible 256-event default and exact-multiple terminal read behavior.
- Task 4: Added ordered contiguous-page validation plus a configurable page-size corpus that asserts the exact event sequence presented to the production handler path.
- Task 5: Added a persisted, operation-owned rebuild lifecycle that blocks competing delivery/erase writes and is completed only after named state, actor state, and rebuild progress are durable.
- Task 5: Query routing now overlays persisted `Rebuilding` only on projection-backed evidence and fails closed to unknown lifecycle when the authoritative lifecycle read is unavailable.
- Task 6: Added explicit complete-prefix count/byte enforcement with the approved stable failure code; exhaustion preserves live actor/named state, advances no rebuild progress, and clears terminal lifecycle.
- Task 6: Added persisted production-path equivalence coverage for more than two pages, empty/exact/bounded inputs, cancellation, interrupted batch promotion with same-operation retry, and resume from persisted progress; the cumulative event count makes the suite fail on page-only overwrite behavior.
- Task 7: Completed the project guardrail audit and clean Release build, including ULID-safe live-test identity and build-enforced `ConfigureAwait(false)` compliance.
- Task 7: Proved the greater-than-two-page corpus against persisted Redis state through the real orchestrator, rebuild endpoint, coordinated batch store, actor state, checkpoint, and lifecycle path.

### File List

- `tests/Hexalith.EventStore.Server.Tests/Projections/AggregateIndexReadModel.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/AggregateReadModel.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildAggregateState.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildEquivalenceOracle.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildEquivalenceOracleTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildEquivalenceSnapshot.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionStatusChanged.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`
- `_bmad-output/specs/spec-correct-paged-rebuild-and-replay-equivalence/.memlog.md`
- `_bmad-output/specs/spec-correct-paged-rebuild-and-replay-equivalence/SPEC.md`
- `_bmad-output/specs/spec-correct-paged-rebuild-and-replay-equivalence/rebuild-semantics.md`
- `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
- `src/Hexalith.EventStore.DomainService/DomainProjectionRebuildPlan.cs`
- `src/Hexalith.EventStore.DomainService/DomainProjectionRebuildSemantics.cs`
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`
- `src/Hexalith.EventStore.DomainService/IAsyncDomainProjectionRebuildHandler.cs`
- `src/Hexalith.EventStore.Server/Projections/INamedProjectionDispatchCoordinator.cs`
- `src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs`
- `src/Hexalith.EventStore.Server/Projections/NamedProjectionRebuildResult.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionDispatcherV2Tests.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionDispatchCoordinatorTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDispatchHttpMessageHandler.cs`
- `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs`
- `tests/Hexalith.EventStore.Server.Tests/Configuration/ProjectionOptionsTests.cs`
- `src/Hexalith.EventStore.Server/Actors/IProjectionLifecycleActor.cs`
- `src/Hexalith.EventStore.Server/Actors/ProjectionLifecycleActor.cs`
- `src/Hexalith.EventStore.Server/Actors/ProjectionRebuildLifecycleRequest.cs`
- `src/Hexalith.EventStore.Server/Projections/IProjectionLifecycleGateway.cs`
- `src/Hexalith.EventStore.Server/Projections/DaprProjectionLifecycleGateway.cs`
- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/ProjectionLifecycleActorTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`
- `src/Hexalith.EventStore.Contracts/Streams/StreamReplayReasonCodes.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildPrefixSafetyLimitExceededException.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/InMemoryProjectionRebuildCheckpointStore.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildProductionHarness.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildProductionHttpMessageHandler.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildProductionPathTests.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveCounterDetailProjectionHandler.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveCounterIndexProjectionHandler.cs`
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs`

## Change Log

- 2026-07-15: Implemented replay-equivalent paged rebuilds with bounded complete-prefix reconstruction, operation-scoped candidate isolation, coordinated detail/index promotion, truthful persisted lifecycle, durable checkpoint ordering, configurable page sizing, explicit safety limits, and deterministic plus real Redis/DAPR equivalence evidence. Status moved to review.
