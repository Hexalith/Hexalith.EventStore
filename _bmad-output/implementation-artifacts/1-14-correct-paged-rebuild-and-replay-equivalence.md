---
baseline_commit: 88aa5c2c2dcb4ca85e4e36cd5455b4e7742d0fc9
created: 2026-07-11
---

# Story 1.14: Correct Paged Rebuild And Replay Equivalence

Status: ready-for-dev

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

- [ ] **Task 1 — Establish the equivalence oracle and its harness (AC: 3, 6)**
  - [ ] Add a deterministic test fixture that projects a stream via canonical `AggregateReplayer.Replay<TState>` (full-sequence) into the same detail + index read models used by the domain projection, producing the reference persisted state, projection versions, and checkpoint.
  - [ ] Use the existing detail/index test models `AggregateReadModel` (detail: `Id`, `Status`) and `AggregateIndexReadModel` (index: `List<string> AggregateIds`) from `tests/Hexalith.EventStore.Client.Tests/Projections/`, or an equivalent Server-side pair; keep the reference builder side-effect-free.
  - [ ] Assert the oracle rejects gaps/duplicates and requires sequence-starts-at-1 exactly as `AggregateReplayer` does (do not reimplement replay semantics).

- [ ] **Task 2 — Make the rebuild delivery feed a complete prefix / stage, never overwrite with a page (AC: 1, 2, 4)**
  - [ ] In `ProjectionUpdateOrchestrator.DeliverProjectionForRebuildAsync`, stop presenting a lone page to the stateless full-replay `/project` handler as the whole stream. Either (a) accumulate the complete required prefix up to the page/`toPosition` boundary before invoking the full-replay handler, or (b) hold page outputs in operation-scoped staging and only compute/promote the complete-prefix result. **Choose per the frozen spec / decision points below; do not weaken to page-only.**
  - [ ] Introduce operation-scoped staging (non-live isolation) so the live `projection-state` key (and any detail/index read-model key) is **not** mutated until promotion. Do not overwrite `EventReplayProjectionActor.ProjectionStateKey` mid-operation.
  - [ ] Preserve the last complete live model on cancel/failure; resume from the last durable safe boundary using `ProjectionRebuildCheckpoint.LastAppliedSequence`/`ToPosition`.

- [ ] **Task 3 — Promote atomically-enough after all required projections complete (AC: 2, 5)**
  - [ ] Add a promotion step that swaps staged detail+index outputs into the live slot only after every required projection for the operation is durably complete; reuse Story 1.10's coordinated-batch / resumable-marker primitive (`ReadModelWritePolicy` / `IReadModelBatchStore`) rather than inventing a new marker.
  - [ ] Ensure the operation cannot report `Succeeded` while any required projection is incomplete; keep per-projection outcomes distinguishable.
  - [ ] Advance rebuild checkpoints (`IProjectionRebuildCheckpointStore.SaveAsync`) only after promotion is durable; never advance a failed projection as success (AD-19). Do not advance the *delivery* checkpoint (`IProjectionCheckpointTracker`) outside the 1.13 contract — leave that seam intact.

- [ ] **Task 4 — Make rebuild page size configurable and prove page-boundary safety (AC: 3)**
  - [ ] Replace the hard-coded `private const int RebuildPageSize = 256` with a value on `ProjectionOptions` (e.g. `RebuildPageSize`, validated `> 0` in `ProjectionOptions.Validate()`), defaulting to 256 for compatibility. Keep the existing `events.Length < RebuildPageSize` page-complete detection correct for the new configurable size (including the exact-boundary case documented at L863–868).
  - [ ] Prove no duplicate/skip/reorder across page boundaries for streams of length {0, exactly pageSize, pageSize+1, N×pageSize, N×pageSize+1}.

- [ ] **Task 5 — Surface a truthful `Rebuilding` lifecycle (AC: 8)**
  - [ ] Ensure an in-flight rebuild is observable as `ProjectionLifecycleState.Rebuilding` and clears to `Current`/`Stale` only on durable promotion; never leave a stale `Rebuilding` after terminal Succeeded/Failed/Canceled.
  - [ ] Do not infer lifecycle from ETag/HTTP/payload/SignalR; only `ProjectionBacked` provenance carries the authoritative state (reuse `ProjectionLifecyclePolicy`; do not reopen Story 1.11/2.8 route-selection scope).

- [ ] **Task 6 — Replay-equivalence + edge-case test corpus (AC: 3, 4, 6, 7)**
  - [ ] Add tests in `tests/Hexalith.EventStore.Server.Tests/Projections/` (alongside `ProjectionUpdateOrchestratorTests.cs`) asserting persisted **detail, index, projection versions, and checkpoints** from the production paged-rebuild orchestrator are **semantically equal** to the Task-1 canonical oracle for a fixture **> 2 pages**.
  - [ ] Cover: empty stream, exact page boundary, `toPosition`-bounded rebuild, mid-rebuild cancellation (live model intact), injected handler/store failure (live model intact, retry converges), and resume-from-progress.
  - [ ] Assert **persisted end-state** (via `InMemoryReadModelStore.Snapshot<T>()` / captured store state and the projection-state key), **not** mock call counts or `202`/`200` — recorder call counts are request-shape evidence only (NFR16 / AD-12).
  - [ ] Add a targeted regression test that fails on the current overwrite bug (page-only state replacing a complete model) and passes only after staging/promotion lands.

- [ ] **Task 7 — Guardrails, build, and (if reachable) live-sidecar evidence (AC: 6)**
  - [ ] `ConfigureAwait(false)` on every new production await (CA2007 is build-breaking). File-scoped namespaces, one type per file, no copyright headers, ULID-safe identity (`Ulid.TryParse`, never `Guid.TryParse`), central package versions only.
  - [ ] If a live-DAPR/Redis lane is reachable (`tests/Hexalith.EventStore.Server.LiveSidecar.Tests/`, `DaprFactAttribute`), assert the equivalence corpus against persisted Redis state; if environment-blocked, record the exact blocker separately — deterministic tests do not substitute for persisted DAPR/Redis evidence but do not block the story on an unavailable environment.

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

### Completion Notes List

### File List
