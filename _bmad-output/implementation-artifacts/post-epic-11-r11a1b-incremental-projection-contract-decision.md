# Post-Epic-11 R11-A1b: Incremental Projection Contract Decision

Status: done

<!-- Source: post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md — Re-Review (2026-05-01) Decision-Needed CRITICAL finding gating R11-A1 closure -->
<!-- Source: epic-11-retro-2026-04-30.md — Action item R11-A1 (full replay vs incremental delivery) -->
<!-- Source: docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md — §2 Projection Checkpoint Tracker, §3 Immediate Trigger, §5 Domain Service /project Endpoint -->
<!-- Source: sprint-status.yaml — `post-epic-11-r11a1b-incremental-projection-contract-decision: backlog` row -->
<!-- Pattern precedent: post-epic-2-r2a5 → post-epic-2-r2a5b paired sibling shipping; post-epic-4-r4a2 → post-epic-4-r4a2b structural carve-out -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform architect responsible for the Hexalith.EventStore server-managed projection builder**,
I want the binary architectural decision recorded and executed for how `ProjectionUpdateOrchestrator` reads aggregate events when delivering projections to domain `/project` endpoints,
so that the conservative full-replay safety patch applied during the R11-A1 re-review (`ProjectionUpdateOrchestrator.cs:88` calls `GetEventsAsync(0)` regardless of checkpoint) is either **made permanent product policy with the checkpoint reduced to polling-coordination scope**, or **replaced with incremental delivery enabled by a contract change** (extend `ProjectionRequest` with prior projection state OR require domain handlers to own prior state) — closing the parent R11-A1 closure gate that currently blocks `in-progress → review`.

This story is the **structural cure** complement to the **safety patch** shipped inside `post-epic-11-r11a1-checkpoint-tracked-projection-delivery` (currently `in-progress`, reopened from `done` on 2026-05-01 by the independent code-review pass that found the CRITICAL incremental-delivery regression in rebuild-from-scratch projection handlers). R11-A1 reverted immediate delivery to full replay while retaining checkpoint state writes; R11-A1b makes the binary policy decision for the future of incremental delivery.

## Story Context

### What R11-A1 left in code as of HEAD on 2026-05-01

- `ProjectionUpdateOrchestrator.DeliverProjectionAsync` at `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:87-89` reads events via `aggregateProxy.GetEventsAsync(0)` — full replay, regardless of any persisted checkpoint.
- `ProjectionCheckpointTracker` (`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`) is fully wired: `IProjectionCheckpointTracker` interface (`IProjectionCheckpointTracker.cs:8-40`) exposes `ReadLastDeliveredSequenceAsync`, `SaveDeliveredSequenceAsync`, `TrackIdentityAsync`, `EnumerateTrackedIdentitiesAsync`. The tracker is registered as `Singleton` in `ServiceCollectionExtensions.cs:48`.
- `ProjectionUpdateOrchestrator.DeliverProjectionAsync` at `:147-158` still calls `checkpointTracker.SaveDeliveredSequenceAsync(identity, highestDeliveredSequence, …)` after a successful `IProjectionWriteActor.UpdateProjectionAsync`. The save is correct (max-sequence merge, ETag retry, no-op short-circuit) but is **never read** by immediate delivery — only by the polling path through `ProjectionPollerService` (Story 11-2 / R11-A2).
- Per-aggregate serialization is in place: `s_projectionLocks` `ConcurrentDictionary<string, SemaphoreSlim>` at `:39` plus the `WaitAsync`/`Release` pattern at `:65-66`/`:165-166` prevents concurrent fire-and-forget triggers from interleaving on the same `AggregateIdentity.ActorId`.
- The design doc `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md` already records the deferred decision verbatim: §2 line 84 says "delta-only projection requests require a future contract decision because the current `ProjectionRequest` does not carry prior projection state"; §3 line 91 and §4 line 103 both say full replay is used "pending the incremental projection contract decision"; §"Error Handling" line 164 says "Future incremental delivery must fail open without skipping events".

### Why incremental delivery is currently unsafe

The CRITICAL finding from the R11-A1 re-review is reproducible from the diff alone:

1. `ProjectionRequest` (`src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs:12-16`) carries only `TenantId`, `Domain`, `AggregateId`, and `Events[]` — no prior state.
2. `EventReplayProjectionActor.UpdateProjectionAsync` (`src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs:37-44`) overwrites the persisted `ProjectionState` whole — `StateManager.SetStateAsync(ProjectionStateKey, state)` does no merge.
3. The canonical sample handler `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs:21-36` rebuilds state from scratch on every call: `CounterState state = new(); foreach (evt in request.Events) ApplyEvent(state, evt.EventTypeName); return new ProjectionResponse("counter", JsonSerializer.SerializeToElement(new { count = state.Count }))`.
4. The downstream consumer `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs:20-62` also rebuilds the per-aggregate `TenantReadModel state = new()` from scratch each call (the index half reads-and-merges from DAPR state, but the per-aggregate half does not).

If `GetEventsAsync(checkpoint)` is restored without a contract change, the next trigger after the first save would deliver only the delta to the handler. The handler computes a fresh state from `new()` plus delta-only events, and `EventReplayProjectionActor` overwrites the prior accumulated state with the delta-only state — silently corrupting any aggregate with more than one event.

### Three candidate paths

The decision picks **exactly one** of three:

- **Path A — Full-replay-permanent.** Formalize the R11-A1 safety patch as the production contract. `GetEventsAsync(0)` is the immediate-delivery read shape forever. The persisted checkpoint becomes either (a) polling-coordination-only (its only consumer is `ProjectionPollerService`'s identity enumeration via `EnumerateTrackedIdentitiesAsync`), or (b) deleted along with `ReadLastDeliveredSequenceAsync` / `SaveDeliveredSequenceAsync` if the polling path is also adjusted to track identities without sequence numbers. Domain handlers stay rebuild-from-scratch and are documented as the supported shape.
- **Path B — Extend `ProjectionRequest` with prior state.** `ProjectionRequest` gains an optional `PriorState` (or `BaseState`) field. `ProjectionUpdateOrchestrator` reads the persisted `ProjectionState` from `EventReplayProjectionActor` (or its DAPR state-store key) before invoking `/project` and includes it in the request. Handlers receive prior state plus delta events and apply the delta onto prior state. The orchestrator switches to `GetEventsAsync(lastDeliveredSequence)`. Both `CounterProjectionHandler` and `TenantProjectionHandler` must be updated to consume the new field. **This is a SemVer-breaking contract change for `Hexalith.EventStore.Contracts`.**
- **Path C — Require incremental-aware handlers (handler-owned prior state).** Keep `ProjectionRequest` shape. Document a new contract obligation: handlers MUST persist their own prior state (e.g., in DAPR state, or in the projection actor via a side path) and MUST treat `events[]` as a delta to that state, not as a complete replay. The orchestrator switches to `GetEventsAsync(lastDeliveredSequence)`. `CounterProjectionHandler` is rewritten to read prior count from a counter-side store before applying events. `TenantProjectionHandler` already does this for the index but not for the per-aggregate state — must be updated. **This is a SemVer-breaking behavioral contract change** (no signature change, but existing rebuild-from-scratch handlers silently break) and the breaking-change marker rests in the `Hexalith.EventStore.Server` package because the orchestrator's read shape changes.

### Scope boundary (what this story does NOT do)

- It does NOT redesign `EventReplayProjectionActor`. The actor stays as the projection-state read/write boundary regardless of which path is picked. (Path B reads its persisted state opportunistically from the DAPR state store via `DaprClient.GetStateAsync<ProjectionState>(stateStoreName, key, ...)`, OR via a new actor-proxy read method — see Implementation Inventory item PB-3 for the exact decision the dev makes during execution.)
- It does NOT close R11-A2 polling-mode product behavior. The `ProjectionPollerService` (Story 11-2) is in `review` and continues to use the same `DeliverProjectionAsync` path the orchestrator exposes; whichever path R11-A1b picks, polling delivery follows the same read shape.
- It does NOT close R11-A3 (AppHost projection proof) or R11-A4 (valid round-trip Tier 3 proof). Those stories provide the runtime evidence that the chosen path actually works end-to-end. R11-A1b's job is the code/contract change; the proof stories pin behavior.
- It does NOT change `EventEnvelope`, `IAggregateActor.GetEventsAsync` signature, or the projection state-store key layout in `EventReplayProjectionActor`.
- It does NOT touch `ProjectionEventDto` field shape under Paths A/C. Path B may add a sibling `PriorState` field on `ProjectionRequest` only — `ProjectionEventDto` remains the per-event wire format.
- It does NOT close the deferred sub-findings recorded against R11-A1's review (sequence overflow at `AggregateActor.cs:596`, Polly-less `HttpClient`, content-type validation, etc.). Those remain in `deferred-work.md` and are not in scope here.

### Promotion-trigger context

R11-A1b was carved on 2026-05-01 the same day R11-A1 reopened. Per the project's carry-over protocol (R3-A8 / R4-A2 / R4-A2b precedent), this story records the **firing trigger** when execution starts. Trigger candidates:
- **Trigger A — observed need:** R11-A1 is `in-progress` and cannot transition to `review` until r11a1b lands. The parent gate is the de-facto Trigger A and almost certainly fires on first dev pickup.
- **Trigger B — calendar SLA reached:** target 2026-05-29 (28-day SLA from carve date 2026-05-01), so Trigger B is a fallback only if the parent gate does not fire pickup first.
- **Neither — opportunistic flush:** record only if the dev picks the story up explicitly to unblock R11-A1 closure on the same day a different change ships.

Record the firing trigger in the Decision Record per AC C1 below.

### Editing target by path

| Path | Production source touchpoints | Test touchpoints | Doc touchpoints | Sample / downstream |
|---|---|---|---|---|
| **A** (full-replay-permanent) | `ProjectionUpdateOrchestrator.cs:87-89` (no functional change — pin), checkpoint tracker scope decision per AC A2 | New: tier-2 test pinning `GetEventsAsync(0)` always; tier-2 test pinning that no rebuild-from-scratch handler is corrupted by repeat triggers | `2026-03-15-server-managed-projection-builder-design.md` §2/§3/§4 — change "pending the incremental projection contract decision" to "full replay is the production contract" | None (Counter/Tenant handlers stay correct under full replay) |
| **B** (extend ProjectionRequest with PriorState) | `ProjectionRequest.cs` (add `PriorState`); `ProjectionUpdateOrchestrator.cs:87-89` (incremental read) and `:109-118` (read prior state, attach to request) | New: tier-2 tests for prior-state-attached request shape, prior-state-missing first-delivery, downstream contract roundtrip | Design doc, `Contracts/Projections` XML docs, `CHANGELOG.md` BREAKING note | `CounterProjectionHandler.cs:21-36` (apply onto `request.PriorState ?? new()`); `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs:20-62` (apply onto `request.PriorState ?? new TenantReadModel()`) |
| **C** (handler-owned prior state) | `ProjectionUpdateOrchestrator.cs:87-89` (incremental read only); no contract change | New: tier-2 tests for incremental read shape; sample tests for handler-side state persistence | Design doc rewritten to document the new handler obligation; `/project` endpoint contract docs in `docs/` | `CounterProjectionHandler.cs:21-36` (read counter-side persisted state, apply delta, write back); `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs:20-62` (use existing index-side store pattern for per-aggregate state too) |

## Acceptance Criteria

ACs are split into **common** ACs (apply regardless of path), **path-A** ACs (full-replay-permanent), **path-B** ACs (extend `ProjectionRequest`), and **path-C** ACs (handler-owned prior state). The dev MUST satisfy all ACs in the **common** group plus all ACs in **exactly one** of A / B / C.

### Common ACs (apply to all paths)

**C1. The decision is recorded with explicit rationale.** A new `### R11-A1b Decision Record` block is appended to this story's Dev Notes at execution time. The block has THREE mandatory sub-headings, each on its own line, in this order:
- `**Chosen path:**` — `full-replay-permanent` OR `extend-projection-request` OR `handler-owned-prior-state`. (No abbreviations; the close gate parses these strings literally.)
- `**Firing trigger:**` — `Trigger A — observed need: R11-A1 closure gate` OR `Trigger A — observed need: <one-sentence summary of a different incoming change>` OR `Trigger B — calendar SLA reached (date >= 2026-05-29)` OR `Neither — opportunistic flush on <YYYY-MM-DD>`.
- `**Rationale:**` — 4–10 sentences citing at least two pieces of concrete evidence beyond "we shipped it / we didn't ship it." Required signals: (a) sample handler shape evidence (does Counter/Tenant rebuild-from-scratch or read-prior?); (b) downstream consumer impact (Hexalith.Tenants is the only known external consumer of `ProjectionRequest` per the grep below — if Path B is picked, name the SemVer obligation); (c) operational signal — does any current trace show full replay actually causing measurable cost on the sample event stream, or is the cost speculative? Cite `git log` / `dotnet test` output / runtime trace IDs from r11a3 evidence captures where applicable.

From this point on, only the chosen path's ACs apply: A = A1–A5, B = B1–B7, C = C-path-1 through C-path-6 (named with explicit prefix to avoid clashing with common-AC numbering — see "Path-C ACs" section). The other paths' ACs are not acted on; record them in the Dev Agent Record's `Completion Notes List` as a single-line annotation `**N/A — not chosen path:** A1–A5 / B1–B7` etc., to keep the post-merge review surface tight.

**C2. R11-A1's closure gate is explicitly closed in the parent story.** The `Verification (2026-05-01, dev-story re-entry)` block in `_bmad-output/implementation-artifacts/post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md` is updated in-place to record the resolution. Append a new sub-block titled `### R11-A1b Closure (<YYYY-MM-DD>)` immediately before the closing `## File List` section, naming the chosen path AND the merge SHA of THIS story's PR. The exact text varies by path but MUST include all three of: chosen path label, merge SHA placeholder resolved at merge time (pre-merge: `<pending merge SHA>`), and a one-sentence summary of what the orchestrator now does (`uses GetEventsAsync(0) by design` for path A, `uses GetEventsAsync(checkpoint) and attaches prior state to ProjectionRequest` for path B, `uses GetEventsAsync(checkpoint); domain handlers own prior state` for path C). Also flip the front-matter `Status:` of `post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md` from `in-progress` to `review` in the **same commit** that lands this story's structural change, so the parent story is unblocked atomically.

**C3. Sprint-status bookkeeping is closed.** `_bmad-output/implementation-artifacts/sprint-status.yaml` shows `post-epic-11-r11a1b-incremental-projection-contract-decision` at `review` at dev-story handoff (or `done` if `code-review` already ran and signed off — see precedent at `post-epic-4-r4a2b`). Both the leading-comment `last_updated:` line AND the YAML `last_updated:` key are updated with today's UTC date and a one-line note naming this story, the chosen path, and the parent r11a1 closure flip. The `Status:` line at the top of THIS story file follows the same rule: `review` at dev-story handoff, `done` after code-review signoff.

**C4. Tier 1 + Tier 2 baseline-equality re-run captured AND gated on path-specific delta.** Capture pre-story Tier 1 + Tier 2 baselines AND post-story counts per CLAUDE.md command list. Expected delta is hard (no ranges) and differs by path:

| Chosen path | Tier 1 Δ | Tier 2 Δ | Sources |
|---|---|---|---|
| `full-replay-permanent` | 0 | +2 | A1 + A4 (path-A option (b) additionally deletes existing `ProjectionCheckpointTrackerTests` for removed methods — adjust Δ down by the deleted test count and record the deletion separately) |
| `extend-projection-request` | +3 | +4 | Tier 1 = Contracts +1 (B6) + Sample +2 (B5); Tier 2 = +4 (B4 four tests) |
| `handler-owned-prior-state` | +2 | +3 | Tier 1 = Sample +2 (C-path-5 sample test); Tier 2 = +3 (C-path-5 three orchestrator tests) |

**The post-story counts MUST equal `baseline + expected_delta` exactly — the equality check is the gate.** (R4-A2 AC #11 phrasing: "mathematical certainty without measurement is not evidence.") Record both numbers AND the equality check in the Dev Agent Record AND in a new `### Verification Status` block appended to this story file. If the post-story count does not match, STOP and root-cause before closing. Sample-tier baseline at HEAD on 2026-05-01 per recent precedent: `Sample 63` per `post-epic-11-r11a3-apphost-projection-proof` Sample.Tests 63/63 PASS record; verify by re-running before computing Δ.

**C5. The design doc reflects the chosen path.** `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md` is updated in-place. The three current "pending the incremental projection contract decision" hedge sentences (lines 84, 91, 103 at HEAD) and the "Future incremental delivery must fail open without skipping events" sentence at line 164 are rewritten per chosen path:
- **Path A**: "Immediate and polling delivery use full replay (`GetEventsAsync(0)`) as the production contract. Domain handlers may rebuild projection state from the supplied event sequence." Also rewrite §"Error Handling" to remove the future-incremental hedge and §"Projection Checkpoint Tracker" to describe its remaining scope (polling identity enumeration only OR removed).
- **Path B**: "Immediate and polling delivery use `GetEventsAsync(lastDeliveredSequence)` and attach the persisted `ProjectionState` as `ProjectionRequest.PriorState`. Domain handlers apply delta events onto prior state." Also document the `PriorState` field semantics (nullable, present only when a prior state exists, opaque `JsonElement`).
- **Path C**: "Immediate and polling delivery use `GetEventsAsync(lastDeliveredSequence)`. Domain handlers MUST persist their own prior projection state and treat `ProjectionRequest.Events` as a delta. Rebuild-from-scratch handlers are no longer supported." Also document migration guidance for handler authors and link to the updated `CounterProjectionHandler`.

**C6. Sibling routing remains correct.** R11-A1 (parent), R11-A2 (polling), R11-A3 (AppHost proof), R11-A4 (valid round-trip) routing is preserved. R11-A5 / R11-A6 / R11-A7 / R11-A8 / R11-A9 routing is unchanged. The `Sibling Sequencing` Dev Note below records the post-r11a1b state of the routing table.

**C7. Conventional-commit prefix on the merge.** Per `CLAUDE.md` § Commit Messages and the project's semantic-release configuration (verify the config matches the AC bump per R4-A2b's R4 / 2-minute pre-merge check pattern):
- **Path A**: `refactor(server):` OR `docs:` — patch bump if any code-only pin or doc-only updates ship; if a checkpoint API method is removed (per AC A2 option (b)), this becomes `feat(server)!:` major. Path A's default expectation: patch bump (no API surface change to `Hexalith.EventStore.Server` or `Hexalith.EventStore.Contracts` if the checkpoint stays as polling-coordination scope).
- **Path B**: `feat(contracts)!:` — major bump. `ProjectionRequest` is a public type in `Hexalith.EventStore.Contracts` (one of the 6 published NuGet packages per CLAUDE.md). Adding a new property to a `record` is a SemVer-breaking change for downstream callers because it changes the constructor signature and the deserialization shape. Pair with `feat(server):` minor bump for the orchestrator change (single PR / single squash commit can carry one prefix; if so, use `feat(contracts)!:` and call out the server change in the body).
- **Path C**: `feat(server)!:` — major bump. The orchestrator's read shape changes from `GetEventsAsync(0)` to `GetEventsAsync(checkpoint)`, and existing rebuild-from-scratch domain handlers silently break — that is a behavioral breaking change for downstream domain-service authors who already deployed handlers against the Epic 11 contract. The `Hexalith.EventStore.Contracts` shape stays unchanged so that package gets no bump.

The body MUST state explicitly: chosen path (`full-replay-permanent` / `extend-projection-request` / `handler-owned-prior-state`), expected semantic-release bump (patch / major / major), AND whether the parent r11a1 status flip is in the same commit (per AC C2).

**Version-bump-fired emergency revert (mirrors R2-A5 AC #10 / R4-A2b AC C7):** if the merge fires a semantic-release bump that does NOT match the AC C7-declared expected bump, revert IMMEDIATELY within the GitHub Releases retention window and file a `release-config-drift` story.

**Pre-merge config + downstream-consumer checks:**
- **(P1) Downstream-consumer check for paths B and C only.** Run `Bash` `grep -rn "ProjectionRequest" Hexalith.Tenants/ --include="*.cs"` (per `Hexalith.Tenants/CLAUDE.md`'s ripgrep workaround). Expected at HEAD on 2026-05-01: matches in `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs` and `Hexalith.Tenants/src/Hexalith.Tenants/Program.cs`. The submodule consumes the public type; under path B the handler signature is preserved (constructor signature changes — fix the call site) and under path C the handler logic must be updated to read prior state from store. Record the grep output and the Tenant-side patch plan in the Dev Agent Record. **Skip on path A.**
- **(R4) semantic-release config verification (2-minute pre-merge check, all paths).** Read whichever of `.releaserc`, `.releaserc.json`, `.releaserc.yaml`, `release.config.js`, or `package.json` `"release"` key configures semantic-release in this repo. Confirm `feat!` / `BREAKING CHANGE:` is mapped to `release: "major"` and `feat:` to `release: "minor"`. Record the verification command and observed config snippet in the Dev Agent Record.

**C8. Existing projection tests stay green.** `ProjectionUpdateOrchestratorTests`, `ProjectionUpdateOrchestratorRefreshIntervalTests`, `ProjectionCheckpointTrackerTests`, `EventReplayProjectionActorTests`, `AggregateActorGetEventsTests`, `ProjectionContractTests`, and `CounterProjectionHandlerTests` remain green. Tests that pinned the R11-A1 safety patch's `GetEventsAsync(0)` argument may need to flip to `GetEventsAsync(checkpoint)` under paths B/C — that is allowed and required, but the change must be a tightening (the test still asserts the intended behavior, not a relaxation to "any sequence number"). Tests that pinned full-replay correctness under paths B/C must be replaced by incremental-correctness tests in the same file with the same naming convention.

**C9. r11a1's deferred CRITICAL is removed.** The `Re-Review (2026-05-01) → Decision-Needed` row in `post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md` (currently `[x]` with text "RESOLVED in current pass by reverting immediate delivery to full replay …") is updated in-place to add a new line `**Closed by R11-A1b at <merge SHA>** with chosen path: <path>` underneath the existing RESOLVED text, preserving the historical resolution-by-safety-patch context. Same SHA-resolution rule as AC C2.

### Path-A ACs (apply only if AC C1 picks `full-replay-permanent`)

**A1. The `GetEventsAsync(0)` call is preserved AND pinned by a new test.** `ProjectionUpdateOrchestrator.cs:87-89` is unchanged. Add a new test `UpdateProjectionAsync_ImmediateDelivery_AlwaysReadsFullHistory` to `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs` that pre-saves a checkpoint at sequence `N > 0` via the real tracker, fires `UpdateProjectionAsync`, and asserts `aggregateProxy.Received(1).GetEventsAsync(0)` (literal zero, not `checkpoint`). The test is the structural pin against future regressions where a developer reads "checkpoint exists, must mean we read incrementally" and re-introduces the corruption path.

**A2. The checkpoint scope is decided.** Pick ONE of:
- **(a) Polling-coordination-only** — `IProjectionCheckpointTracker.SaveDeliveredSequenceAsync` and `ReadLastDeliveredSequenceAsync` continue to exist and continue to be called from `DeliverProjectionAsync`. The `LastDeliveredSequence` value remains advisory and is only consumed by the polling path (R11-A2's `ProjectionPollerService`) for "this aggregate has had at least one successful delivery" semantics. Rationale: zero code churn beyond doc clarification; risks future re-corruption if a dev re-wires `ReadLastDeliveredSequenceAsync` into the orchestrator's `GetEventsAsync` call.
- **(b) Sequence storage removed; identity tracking retained** — Remove `LastDeliveredSequence` from `ProjectionCheckpoint` (`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpoint.cs:11-16`) and remove `ReadLastDeliveredSequenceAsync` / `SaveDeliveredSequenceAsync` from `IProjectionCheckpointTracker.cs:8-40`. Keep `TrackIdentityAsync` and `EnumerateTrackedIdentitiesAsync` for polling-mode identity enumeration. Update `ProjectionUpdateOrchestrator.cs:147-158` to drop the post-write checkpoint save. Update `ProjectionPollerService` to call only `EnumerateTrackedIdentitiesAsync`. **This is a SemVer-major change for `Hexalith.EventStore.Server`** because `IProjectionCheckpointTracker` is a public interface and removing methods breaks downstream implementations. AC C7 prefix becomes `feat(server)!:` major if option (b) is chosen.

Record the option chosen in the Decision Record. Default expectation: (a) — minimum churn, smallest blast radius.

**A3. Domain-handler shape documentation.** Update both sample / downstream handlers' XML docs to assert the supported shape under path A:
- `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs:11-14`: rewrite the summary to "Replays events from a `ProjectionRequest` onto a fresh `CounterState` and returns the current count. Rebuild-from-scratch is the supported pattern under the full-replay projection contract; the handler does not need to read or persist prior projection state."
- `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs:20-62`: add an XML doc remark stating that the per-aggregate `TenantReadModel state = new()` rebuild is correct under the full-replay contract; the index-side state-store read-and-merge is independent and orthogonal.

**A4. Tier-2 corruption non-regression test.** Add a new test `UpdateProjectionAsync_RepeatTriggersOnSameAggregate_ProducesIdenticalProjectionState` to `ProjectionUpdateOrchestratorTests.cs`. The test simulates two sequential triggers on the same aggregate with non-empty event history, captures the `ProjectionState` written to the actor each time (via NSubstitute argument capture on `IProjectionWriteActor.UpdateProjectionAsync`), and asserts the two states are byte-identical (or JSON-structurally-equal). Under path A, this is the runtime proof that full replay does not produce drift between triggers. Tier 2 expected delta: **+2** (A1 + A4).

**A5. r11a1's deferred concurrent-state-regression note is reclassified.** The `Review Findings (Current Pass 2026-05-01) → Decision` row in `post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md` that currently reads "Concurrent projection triggers can regress projection state while the checkpoint remains advanced — RESOLVED by per-aggregate serialization in `ProjectionUpdateOrchestrator`" is preserved unchanged (path A does not weaken serialization). Append `; full-replay-permanent contract under R11-A1b makes the regression scenario impossible by construction (no delta-only delivery).` to the line.

### Path-B ACs (apply only if AC C1 picks `extend-projection-request`)

**B1. `ProjectionRequest` gains a `PriorState` field.** Update `src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs` to:
```csharp
public record ProjectionRequest(
    string TenantId,
    string Domain,
    string AggregateId,
    ProjectionEventDto[] Events,
    JsonElement? PriorState = null);
```
The new field is **nullable and defaulted to `null`** so first-delivery (no prior state exists) and any handler that does not need prior state stays binary-identical at the wire. JSON serialization must omit `PriorState` when `null` (use `JsonIgnoreCondition.WhenWritingNull` if needed; current `record` defaults already write `null` as `"priorState": null`, which is acceptable if downstream handlers tolerate it — verify with the existing `ProjectionRequest_RoundTrips_Json` test in `tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionContractTests.cs:49`). Update XML docs to describe `PriorState` as "the projection state persisted from the previous successful delivery, or `null` on first delivery" and to remove the §2 line 84 hedge.

**B2. `ProjectionUpdateOrchestrator` reads prior state and switches to incremental reads.** Update `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`:
- After the polling-mode guard (`:46-56`) and before the aggregate proxy event read, read the persisted prior state. **Prefer the existing DAPR state-store key path** (`DaprClient.GetStateAsync<ProjectionState>(stateStoreName, projectionStateKey, ...)`), where `projectionStateKey` is derived from `EventReplayProjectionActor.ProjectionStateKey = "projection-state"` and the actor ID. This avoids adding a new actor proxy method. If the actor's DAPR state-store layout cannot be cleanly accessed without leaking the actor's internal key shape, the alternative is to add a read-only `GetProjectionStateAsync()` method to a new internal `IProjectionReadActor` interface implemented by `EventReplayProjectionActor` — the dev decides during execution per Implementation Inventory item PB-3 below and records the choice in the Decision Record.
- Before reading prior state, read the checkpoint via `checkpointTracker.ReadLastDeliveredSequenceAsync(identity, cancellationToken)`. Fail-open: on read failure, fall back to `lastDeliveredSequence = 0` AND `priorState = null` (replay from zero with no prior state — equivalent to first delivery). Log via `Log.CheckpointReadFailed`.
- Replace `GetEventsAsync(0)` at `:87-89` with `GetEventsAsync(lastDeliveredSequence)`.
- Attach `priorStateJson` (the `JsonElement?` extracted from the persisted `ProjectionState.GetState()` or `null` if no prior state) to the new `ProjectionRequest` constructor: `var request = new ProjectionRequest(identity.TenantId, identity.Domain, identity.AggregateId, projectionEvents, priorStateJson);`.

**B3. Sample and downstream handlers consume `PriorState`.**
- `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs:21-36`: change the body to read prior count from `request.PriorState`, seed `CounterState state = new() { Count = priorCount }`, then apply the delta events. Concretely: `int priorCount = request.PriorState?.TryGetProperty("count", out JsonElement c) == true && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0;`. Update XML docs.
- `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs:20-62`: change the per-aggregate path to deserialize `request.PriorState` into `TenantReadModel` (or `new()` if null) before the apply loop. The index-side state-store read-and-merge stays as-is. Match the existing pattern where the per-aggregate write key is `TenantProjectionKeyPrefix + request.AggregateId`.

**B4. New tier-2 incremental-delivery tests.** Add to `ProjectionUpdateOrchestratorTests.cs`:
- `UpdateProjectionAsync_FirstDelivery_AttachesNullPriorState_ReadsFromZero`: no checkpoint, no prior state — `GetEventsAsync(0)`, `request.PriorState` is `null`.
- `UpdateProjectionAsync_SubsequentDelivery_AttachesPersistedPriorState_ReadsFromCheckpoint`: pre-save checkpoint `N > 0`, pre-write a `ProjectionState` to the state-store mock — `GetEventsAsync(N)`, `request.PriorState` matches the persisted `JsonElement`.
- `UpdateProjectionAsync_PriorStateReadFailure_FailsOpenWithFullReplay`: state-store read throws — fall back to `GetEventsAsync(0)` AND `request.PriorState = null` AND log `Log.CheckpointReadFailed` (or new `Log.PriorStateReadFailed` per dev's structured-log naming choice).
- `UpdateProjectionAsync_RepeatTriggersOnSameAggregate_ProducesAdvancingProjectionState`: simulate two sequential triggers — second trigger sees the first's persisted state via `PriorState` and applies only the delta — final state matches the full-replay-equivalent count.

Tier 2 expected delta: **+4**.

**B5. Sample handler tests cover PriorState semantics.** Add to `tests/Hexalith.EventStore.Sample.Tests/Counter/Projections/CounterProjectionHandlerTests.cs`:
- `Project_WithPriorState_AppliesDeltaOntoPrior`: prior count = 5, delta = `[CounterIncremented]` — final count = 6.
- `Project_WithoutPriorState_RebuildsFromScratch`: `PriorState = null`, full replay events — same as current `Project_NoEvents_ReturnsZero` / `Project_SingleIncrement_ReturnsOne` shape.

Tier 1 (Sample) expected delta: **+2**.

**B6. Contract roundtrip test extended.** Update `tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionContractTests.cs` `ProjectionRequest_RoundTrips_Json` (line 49) to round-trip both `PriorState = null` AND a non-null `JsonElement` (e.g., `JsonDocument.Parse("""{"count":7}""").RootElement`). Add a new test `ProjectionRequest_PriorState_OmitsOrNullsCleanly` asserting the JSON shape is downstream-handler-tolerant (verify `null` is either omitted or written as `"priorState":null` consistently).

Tier 1 (Contracts) expected delta: **+1**.

**B7. r11a1's safety-patch revert pin is updated.** The `Review Findings (Current Pass 2026-05-01) → Decision` rows in `post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md` that say "RESOLVED in current pass by reverting immediate delivery to full replay" are updated to append `; replaced under R11-A1b at <merge SHA> with prior-state-attached incremental delivery (extend-projection-request path)`. This is the documentation that links the safety patch's lifetime to the structural cure.

### Path-C ACs (apply only if AC C1 picks `handler-owned-prior-state`)

**C-path-1. `ProjectionUpdateOrchestrator` switches to incremental reads only.** Update `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:87-89` to `GetEventsAsync(lastDeliveredSequence)`, where `lastDeliveredSequence` is read via `checkpointTracker.ReadLastDeliveredSequenceAsync(identity, cancellationToken)` after the polling-mode guard. Fail-open on read failure (`lastDeliveredSequence = 0`, log `Log.CheckpointReadFailed`). **No change to `ProjectionRequest` shape or `ProjectionEventDto`.**

**C-path-2. Domain-handler contract is documented and enforced by tests.** The contract is: handlers MUST persist their own per-aggregate prior state (e.g., in DAPR state, in the projection actor's state-store via a side channel, or in any handler-side store) and MUST treat `request.Events` as a delta to that state. Update the design doc (per common AC C5) AND add the contract paragraph to `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md` §5 "Domain Service `/project` Endpoint" describing the obligation.

**C-path-3. Counter sample is rewritten to own its prior state.** Update `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs:21-36` to:
- Inject `DaprClient` (or pass it as a parameter from the endpoint registration in `samples/Hexalith.EventStore.Sample/Program.cs:39-43`).
- Read the prior `CounterState` from a DAPR state key like `counter-projection:{request.TenantId}:{request.AggregateId}` (or equivalent — pick a key shape that matches the existing Tenant precedent at `TenantProjectionHandler.cs:14`).
- Apply delta events onto the read state.
- Write the updated state back to the same key BEFORE returning the `ProjectionResponse`.

This makes the handler symmetric with `TenantProjectionHandler`'s per-aggregate path under path C (see C-path-4).

**C-path-4. Tenant downstream handler is updated for per-aggregate state.** Update `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs:20-62`:
- The per-aggregate `TenantReadModel state = new()` rebuild becomes a read from `TenantProjectionKeyPrefix + request.AggregateId` (pattern already used by the index-side at `:40-42`).
- Apply delta events onto the read state.
- Write the updated state back (the existing `daprClient.SaveStateAsync` at `:34-37` already does this; no change needed beyond the read seed).

**C-path-5. New tier-2 incremental-delivery tests.** Add to `ProjectionUpdateOrchestratorTests.cs`:
- `UpdateProjectionAsync_FirstDelivery_NoCheckpoint_ReadsFromZero`: tracker returns 0 — `GetEventsAsync(0)`.
- `UpdateProjectionAsync_SubsequentDelivery_ReadsFromCheckpoint`: tracker returns `N > 0` — `GetEventsAsync(N)`.
- `UpdateProjectionAsync_CheckpointReadFailure_FailsOpenFromZero`: tracker throws — fall back to `GetEventsAsync(0)` AND log `Log.CheckpointReadFailed`.

Plus a new sample test `Project_WithPersistedPriorState_AppliesDeltaCorrectly` in `CounterProjectionHandlerTests.cs` proving the read-apply-write loop produces the right count when called twice.

Tier 2 expected delta: **+3**. Tier 1 (Sample) expected delta: **+2**.

**C-path-6. r11a1's safety-patch revert pin is updated.** Same shape as B7: the relevant `RESOLVED` rows in `post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md` get appended `; replaced under R11-A1b at <merge SHA> with handler-owned-prior-state incremental delivery (handler-owned-prior-state path)`.

## Implementation Inventory

**This block is the source of truth for the per-path edit list.** Every cited line is anchored to HEAD on 2026-05-01. Verify and re-anchor by literal substring at execution time per Task 0.1.

### Production source files

| # | File | Role | Path-A action | Path-B action | Path-C action |
|---|------|------|---|---|---|
| P1 | `src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs:12-16` | Public DTO record (4 fields) | No edit | Add `JsonElement? PriorState = null` field; update XML docs | No edit |
| P2 | `src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs:14-16` | Public DTO record | No edit | No edit | No edit |
| P3 | `src/Hexalith.EventStore.Contracts/Projections/ProjectionEventDto.cs:16-22` | Public DTO record | No edit | No edit | No edit |
| P4 | `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:87-89` | Event read site (`GetEventsAsync(0)`) | Pin via test (no code change); update XML doc summary on `:21-30` to say full-replay is contract | Replace with `GetEventsAsync(lastDeliveredSequence)`; insert prior-state read before the call; pass prior state to `new ProjectionRequest(...)` at `:110` | Replace with `GetEventsAsync(lastDeliveredSequence)`; no contract change at `:110` |
| P5 | `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:147-158` | Post-write checkpoint save | Path-A option (a): no edit. Path-A option (b): remove the save block | Preserve unchanged (the save is needed for next-delivery's checkpoint read) | Preserve unchanged |
| P6 | `src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs:8-40` | Public interface | Path-A option (a): no edit. Path-A option (b): remove `ReadLastDeliveredSequenceAsync` and `SaveDeliveredSequenceAsync` (SemVer-major) | No edit | No edit |
| P7 | `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpoint.cs:11-16` | Persisted record | Path-A option (b): remove `LastDeliveredSequence` field (SemVer-major) | No edit | No edit |
| P8 | `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs:34-44` | Projection state read/write | No edit | Read `ProjectionState` via DAPR state-store from orchestrator (preferred per PB-3) — no actor change. Alternative: add `GetProjectionStateAsync()` to a new internal `IProjectionReadActor` interface | No edit |
| P9 | `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs:21-36` | Sample handler | Doc-only update on the summary | Apply delta onto `request.PriorState ?? new CounterState()` | Read prior state from DAPR state, apply delta, write back |
| P10 | `samples/Hexalith.EventStore.Sample/Program.cs:41-42` | `/project` endpoint registration | No edit | No edit (handler is static; signature unchanged) | Update endpoint registration to inject `DaprClient` if `CounterProjectionHandler` becomes non-static |
| P11 | `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs:20-62` | Downstream submodule consumer | Doc-only remark on the summary | Apply delta onto `JsonSerializer.Deserialize<TenantReadModel>(request.PriorState ?? default) ?? new TenantReadModel()` for the per-aggregate path | Read prior `TenantReadModel` from `TenantProjectionKeyPrefix + request.AggregateId` (mirror the index-side pattern at `:40-42`) before the apply loop |
| P12 | `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md:84,91,103,164` | Design doc hedges | Rewrite all four to "full replay is the production contract" | Rewrite all four to describe prior-state-attached incremental delivery | Rewrite all four to describe handler-owned-prior-state incremental delivery |

### Test files

| # | File | Path-A action | Path-B action | Path-C action |
|---|------|---|---|---|
| T1 | `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs` | Add A1 + A4 tests | Add B4's four tests; update existing tests that pin `GetEventsAsync(0)` to pin `GetEventsAsync(checkpoint)` where applicable | Add C-path-5's three tests; same update obligation |
| T2 | `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs` | Path-A option (a): no edit. Path-A option (b): delete tests for removed methods | No edit | No edit |
| T3 | `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs` | No edit | No edit (polling deferral guard precedes the read) | No edit |
| T4 | `tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionContractTests.cs:49` | No edit | Update `ProjectionRequest_RoundTrips_Json` for the new field; add `ProjectionRequest_PriorState_OmitsOrNullsCleanly` | No edit |
| T5 | `tests/Hexalith.EventStore.Sample.Tests/Counter/Projections/CounterProjectionHandlerTests.cs` | No edit | Add B5's two tests; update existing tests that constructed `ProjectionRequest` (lines 19, 28, 38, 50, 61, 73, 83, 95, 104, 116, 137, 149, 163, 177, 191) to use the new positional or named arg shape — most can use the default `null` for `PriorState` | Add `Project_WithPersistedPriorState_AppliesDeltaCorrectly`; depending on handler refactor, mock `DaprClient` for the read/write |

### Decision-evidence as of HEAD on 2026-05-01

- **`GetEventsAsync(0)` call sites in production:** `Bash` `grep -rn "GetEventsAsync(0)" src/Hexalith.EventStore.Server/` returns exactly **1 match** at `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:88`. No other production call site exists. The poller (`ProjectionPollerService.cs`) routes through `IProjectionUpdateOrchestrator.UpdateProjectionAsync` and inherits whichever read shape the orchestrator picks.
- **`ProjectionRequest` constructor call sites:** 1 production (`ProjectionUpdateOrchestrator.cs:110`), 0 sample (handlers receive the request and never construct it), 0 server tests, ~16 contracts + sample tests (per `Bash` `grep -n "new ProjectionRequest\|ProjectionRequest request = " tests/` at HEAD on 2026-05-01). Path B's optional-parameter addition is source-compatible — call sites that do not pass `PriorState` keep working without edits because the new parameter has a default value of `null`. Wire-format compatibility is a separate concern: existing JSON without a `priorState` property deserializes cleanly into the new record because the missing-property default is `null`.
- **Downstream consumers of `ProjectionRequest`:** the `Hexalith.Tenants` submodule (per `Bash` `grep -rn "ProjectionRequest" Hexalith.Tenants/ --include="*.cs"`) — `Program.cs` and `Projections/TenantProjectionHandler.cs`. No other downstream consumer is known. **Path B's SemVer-major declaration relies on this fact at HEAD; re-grep at execution time and update the count if any new consumer has appeared.**
- **Sample handler shape:** `CounterProjectionHandler.Project` rebuilds from scratch on every call (`CounterState state = new(); foreach (evt in request.Events) ApplyEvent(state, evt.EventTypeName)`). Under paths B/C this is the regression vector that must be fixed. Under path A it is the supported pattern.
- **Tenant handler shape:** `TenantProjectionHandler.ProjectAsync` rebuilds the per-aggregate `TenantReadModel state = new()` from scratch but reads-and-merges the index-side `TenantIndexReadModel` from DAPR state. The per-aggregate half is the regression vector under paths B/C; the index half is already incremental-correct.

### PB-3 — Path B prior-state read mechanism

Under path B, the orchestrator must read the persisted projection state before invoking `/project`. Two mechanisms are available; the dev picks one during execution and records the choice:

- **(i) Direct DAPR state-store read.** `daprClient.GetStateAsync<ProjectionState>(actorStateStoreName, projectionStateKey, ...)`. The key is derived from the `EventReplayProjectionActor`'s state-key constant `ProjectionStateKey = "projection-state"` (per `EventReplayProjectionActor.cs:34`) and the actor ID (per `QueryActorIdHelper.DeriveActorId(...)` at `ProjectionUpdateOrchestrator.cs:133-137`). DAPR actor state is stored under the configured actor state store with a composite key. **Risk:** leaks the actor's internal state-key shape into the orchestrator. **Mitigation:** add an internal helper on `EventReplayProjectionActor` that exposes only the key derivation, not the read mechanism — or, keep the key derivation centralized in `QueryActorIdHelper`.
- **(ii) New actor-proxy method.** Add `Task<ProjectionState?> GetProjectionStateAsync()` to a new internal `IProjectionReadActor` interface implemented by `EventReplayProjectionActor`. The orchestrator calls it via `actorProxyFactory.CreateActorProxy<IProjectionReadActor>(...)`. **Risk:** adds an actor round-trip for every projection update (currently the actor is invoked only for the write at `:143-145`). **Mitigation:** the read is cheap and serializable, and the actor is single-threaded so the read is naturally consistent with the upcoming write.

Pick option (i) by default unless option (ii)'s clean-boundary argument outweighs the actor round-trip cost. Record the chosen option in the Decision Record's Rationale block.

## Tasks / Subtasks

- [x] **Task 0 — Pre-flight verification of the Implementation Inventory** (AC: C1, C4)
  - [x] 0.1 For every cited line in the Implementation Inventory tables (P1–P12, T1–T5), confirm the file exists at the cited path and the cited line numbers still resolve to the cited code. Re-anchor by literal substring (e.g., `GetEventsAsync(0)`, `ProjectionStateKey = "projection-state"`, `TenantProjectionKeyPrefix + request.AggregateId`) for any drifted citation. Record the actual HEAD SHA in the Dev Agent Record `Debug Log References`.
  - [x] 0.2 Run the decision-evidence greps from the section above. Confirm `GetEventsAsync(0)` call-site count = 1, `ProjectionRequest` downstream-consumer count = 2 (both in `Hexalith.Tenants/`), and the sample/tenant handler shape evidence still matches the inventory. If any of these regress (e.g., a new consumer has appeared, a sample handler has been updated since 2026-05-01), pause and update the inventory before continuing.
  - [x] 0.3 Capture local Tier 1 + Tier 2 baseline pass counts per the CLAUDE.md command list. Record in the Dev Agent Record. (Per R2-A6: do not run only `dotnet test --filter` and call it a baseline; capture the full Tier 1 + Tier 2 baseline shape so the AC C4 equality can be checked end-to-end.)
  - [x] 0.4 Capture sprint-status.yaml current state of `post-epic-11-r11a1b-incremental-projection-contract-decision` (expected: `ready-for-dev` after this story-creation execution; flipped to `in-progress` by dev-story start; `review` at dev-story handoff per AC C3) AND the parent row `post-epic-11-r11a1-checkpoint-tracked-projection-delivery: in-progress`. Record both starting-state lines.

- [x] **Task 1 — Make and record the path decision** (AC: C1)
  - [x] 1.1 Review the decision-framework Dev Note below. Weigh evidence for paths A / B / C against the project's current operational reality: (a) does any captured trace from `post-epic-11-r11a3` show full replay producing observable cost? (b) what is the SemVer cost of path B's contract change vs the API-stability cost of path A's checkpoint reduction vs the migration cost of path C's handler-rewrite? (c) what is the smallest change that closes the parent r11a1 gate cleanly? **Default recommendation: path A (full-replay-permanent).** Rationale: (i) the safety patch already ships and is correct; (ii) the sample event stream is small and there is no recorded operational signal that full replay is too expensive; (iii) keeping the `Hexalith.EventStore.Contracts` surface stable avoids forcing the `Hexalith.Tenants` submodule into a synchronized-release dance. Override only with an explicit captured-evidence Trigger A or with a project-lead instruction.
  - [x] 1.2 Record the firing trigger per common AC C1 second sub-bullet.
  - [x] 1.3 Append the `### R11-A1b Decision Record` block to this story's Dev Notes with the three required sub-headings (`**Chosen path:**`, `**Firing trigger:**`, `**Rationale:**`). Per R4-A2b's R5 unverified-format caveat: verify the parsing format the `code-review` skill expects by reading `.claude/skills/bmad-code-review/` (or whichever skill runs the close gate) and adjust the block shape WITHOUT changing the three required pieces of information. Record format-verification outcome in the Dev Agent Record.

- [x] **Task 2 — Execute path A (only if AC C1 picked `full-replay-permanent`)** (AC: A1, A2, A3, A4, A5)
  - [x] 2.1 Pick AC A2 sub-option (a) or (b). Default: (a). Record in Decision Record.
  - [x] 2.2 Add A1 test `UpdateProjectionAsync_ImmediateDelivery_AlwaysReadsFullHistory` to `ProjectionUpdateOrchestratorTests.cs`.
  - [x] 2.3 Add A4 test `UpdateProjectionAsync_RepeatTriggersOnSameAggregate_ProducesIdenticalProjectionState`.
  - [x] 2.4 If A2(b) chosen: remove `LastDeliveredSequence` from `ProjectionCheckpoint.cs`, remove the read/save methods from `IProjectionCheckpointTracker.cs` and `ProjectionCheckpointTracker.cs`, remove the `:147-158` save block from the orchestrator, update `ProjectionPollerService` to call only `EnumerateTrackedIdentitiesAsync`, delete the deleted-method tests in `ProjectionCheckpointTrackerTests.cs`. Verify Tier 2 delta accounts for the deletions. N/A: A2(a) chosen; no checkpoint API removal.
  - [x] 2.5 Update XML docs per A3 on `CounterProjectionHandler` and `TenantProjectionHandler`.
  - [x] 2.6 Update the design doc per common AC C5 path-A wording.
  - [x] 2.7 Append per A5 to the relevant `RESOLVED` row in the parent r11a1 story.
  - [x] 2.8 Verify `_ = grep -rn "pending the incremental projection contract decision" docs/` returns 0 matches (the hedge is fully removed under path A).

- [x] **Task 3 — Execute path B (only if AC C1 picked `extend-projection-request`)** (AC: B1, B2, B3, B4, B5, B6, B7) — N/A: path A chosen.
  - [x] 3.1 Update `ProjectionRequest.cs` per B1.
  - [x] 3.2 Pick PB-3 mechanism (i) or (ii). Default: (i). Record in Decision Record.
  - [x] 3.3 Update `ProjectionUpdateOrchestrator.cs` per B2 (read checkpoint, read prior state, switch to `GetEventsAsync(lastDeliveredSequence)`, attach prior state to the new `ProjectionRequest`).
  - [x] 3.4 Update `CounterProjectionHandler.cs` per B3 to apply delta onto `request.PriorState`.
  - [x] 3.5 Update `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs` per B3. Re-run the P1 grep at the end of edits to verify the only `ProjectionRequest` call sites are the orchestrator and the test files.
  - [x] 3.6 Add B4's four tier-2 tests to `ProjectionUpdateOrchestratorTests.cs`.
  - [x] 3.7 Add B5's two sample tests to `CounterProjectionHandlerTests.cs`.
  - [x] 3.8 Update B6's contract test in `ProjectionContractTests.cs`.
  - [x] 3.9 Update existing `CounterProjectionHandlerTests.cs` constructor call sites to pass the new field (default `null` is fine for tests that don't exercise prior state).
  - [x] 3.10 Update the design doc per common AC C5 path-B wording.
  - [x] 3.11 Append per B7 to the relevant `RESOLVED` rows in the parent r11a1 story.
  - [x] 3.12 Re-run the AC C7 P1 downstream-consumer grep on `Hexalith.Tenants/` and confirm the submodule patch is the only Tenant-side change. Record in Dev Agent Record.

- [x] **Task 4 — Execute path C (only if AC C1 picked `handler-owned-prior-state`)** (AC: C-path-1, C-path-2, C-path-3, C-path-4, C-path-5, C-path-6) — N/A: path A chosen.
  - [x] 4.1 Update `ProjectionUpdateOrchestrator.cs` per C-path-1 (read checkpoint, switch to `GetEventsAsync(lastDeliveredSequence)`).
  - [x] 4.2 Update `CounterProjectionHandler.cs` per C-path-3: read prior state from DAPR state, apply delta, write back. Inject `DaprClient` per `Program.cs:41-42` registration.
  - [x] 4.3 Update `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs` per C-path-4: seed per-aggregate state from `TenantProjectionKeyPrefix + request.AggregateId` instead of `new()`.
  - [x] 4.4 Add C-path-5's three orchestrator tests to `ProjectionUpdateOrchestratorTests.cs` and the sample test to `CounterProjectionHandlerTests.cs`.
  - [x] 4.5 Update the design doc per common AC C5 path-C wording.
  - [x] 4.6 Append per C-path-6 to the relevant `RESOLVED` rows in the parent r11a1 story.
  - [x] 4.7 Document the new handler obligation in `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md` §5 with explicit migration guidance.

- [x] **Task 5 — Common closure obligations regardless of path** (AC: C2, C3, C4, C8, C9)
  - [x] 5.1 Update the parent r11a1 story per AC C2: append the `### R11-A1b Closure (<YYYY-MM-DD>)` block AND flip front-matter `Status: in-progress → review` in the SAME commit as the structural change. Pre-merge SHA: leave `<pending merge SHA>`; post-merge: resolve.
  - [x] 5.2 Update sprint-status.yaml per AC C3: flip `post-epic-11-r11a1b-incremental-projection-contract-decision: ready-for-dev → review`. Add a one-line `last_updated` note that names the chosen path AND the parent r11a1 status flip. Preserve all other rows and comments byte-identical.
  - [x] 5.3 Re-run Tier 1 + Tier 2 per AC C4 and capture post-story counts. Compute equality `post = baseline + expected_delta` and record both numbers in a new `### Verification Status` block at the end of this story file.
  - [x] 5.4 Verify per AC C8 that all listed existing tests pass green. If any tier-2 test had to be tightened to flip from `GetEventsAsync(0)` to `GetEventsAsync(checkpoint)` under paths B/C, record the test name and the diff shape (one-line: `<TestName>: pinned arg flipped from 0 to lastDeliveredSequence`) in the Dev Agent Record.
  - [x] 5.5 Update the parent r11a1 deferred CRITICAL row per AC C9.
  - [x] 5.6 Conventional-commit prefix per AC C7 chosen-path mapping. Verify semantic-release config per (R4) before pushing the merge.

- [x] **Task 6 — Path-coherence gate** (AC: C1)
  - [x] 6.1 Run a final `Bash` `grep -rn "pending the incremental projection contract decision" docs/` AND `grep -rn "GetEventsAsync(0)" src/Hexalith.EventStore.Server/`. Under path A: design-doc grep returns 0; orchestrator grep returns 1 (preserved). Under path B: design-doc grep returns 0; orchestrator grep returns 0 (replaced). Under path C: same as path B.
  - [x] 6.2 Verify no half-wire/half-rewrite state by confirming the chosen path's full edit set landed: P1 grep on `PriorState` (path B only — must appear in contracts AND sample AND tenant), P9 grep on `CounterState state = new() { Count = priorCount }` (path B) or `daprClient.GetStateAsync<CounterState>` (path C), P11 grep on the corresponding tenant edit. If any check fails, the path is not coherent and the story must not close.

## Dev Notes

### Sibling Sequencing

Post-r11a1b sibling-routing table (preserved from r11a1's review notes):

| Story | Status before r11a1b lands | Status after r11a1b lands | Notes |
|---|---|---|---|
| `post-epic-11-r11a1-checkpoint-tracked-projection-delivery` | in-progress (gated on r11a1b) | review (per AC C2 atomic flip) | Parent — closure gate cleared by r11a1b |
| `post-epic-11-r11a1b-incremental-projection-contract-decision` | backlog → ready-for-dev (this story create) | review (per AC C3) | Carve-out structural cure |
| `post-epic-11-r11a2-polling-mode-product-behavior` | review | unchanged | Polling routes through whatever read shape r11a1b picks; no new edits |
| `post-epic-11-r11a3-apphost-projection-proof` | review | unchanged | AppHost evidence captured against the safety-patch behavior; if path B/C lands, the proof's correctness assertion still holds (projection state advances with each command), but the wire-shape evidence (PriorState present/absent) will reflect the new contract |
| `post-epic-11-r11a4-valid-projection-round-trip` | in-progress | unchanged | Tier 3 round-trip proof is contract-agnostic at the API layer |
| `post-epic-11-r11a5..r11a8` | various | unchanged | Out of scope here |

### Decision Framework

The default recommendation is **path A (full-replay-permanent)**. Reasoning:

1. **The safety patch is already correct.** R11-A1's revert to `GetEventsAsync(0)` ships in the same diff as the checkpoint tracker. The system works end-to-end today against the sample. Path A preserves that working state and adds documentation + tests.
2. **No operational signal supports paying the contract-change cost.** R11-A3's AppHost proof (in `review` as of 2026-05-01) captured a working command→event→`/project`→projection-actor→query→ETag→SignalR→UI path. The trace did not record full replay producing measurable cost on the sample event stream. Path B and path C both pay SemVer-breaking change costs against unmeasured concern.
3. **Path B's contract surface impact is the largest.** `ProjectionRequest` is a public type in `Hexalith.EventStore.Contracts` (one of the 6 published NuGet packages per CLAUDE.md). The `Hexalith.Tenants` submodule consumes it. Path B forces a synchronized release across both repos OR a careful deprecate-then-remove dance.
4. **Path C's behavioral-contract impact is subtle but large.** Existing rebuild-from-scratch handlers (the documented Epic 11 pattern) silently break under path C. The migration cost is real even though the wire shape is unchanged.
5. **The future is not foreclosed.** Path A leaves the checkpoint tracker available (option (a)). If a future operational signal emerges (production traces show full replay is expensive at scale), a follow-up story can switch to path B or path C without new architectural debt.

Override the default ONLY if:
- An explicit Trigger A names a backpressure-touching change that needs incremental delivery NOW, OR
- The project lead instructs a path other than A, OR
- A captured runtime trace from r11a3 or r11a4 shows full replay producing measurable cost on a representative non-sample workload.

Record the override evidence in the Decision Record's Rationale block per AC C1.

### Why the safety patch is not enough on its own

The R11-A1 re-review's Decision-Needed CRITICAL is "RESOLVED" by the safety patch in the parent story's history. That resolution is correct AS A PATCH but is not load-bearing as a contract: a future developer reading the diff might see the persisted checkpoint and reason "the checkpoint is read somewhere — let me re-introduce `GetEventsAsync(checkpoint)` in the orchestrator" and re-introduce the corruption. Path A's AC A1 (the `UpdateProjectionAsync_ImmediateDelivery_AlwaysReadsFullHistory` test) is the structural pin that makes this regression impossible without explicit test deletion. Paths B and C close the corruption path by changing the contract so incremental delivery is always safe.

### Why this carve-out is paired with R11-A1 instead of folded in

R11-A1 is `in-progress` — not yet `done`. Folding the structural-cure decision back into r11a1 would either (a) bloat r11a1's diff with a SemVer-breaking contract change beyond its original scope (checkpoint-tracked delivery), or (b) keep r11a1 stuck in `in-progress` while the architectural decision is debated. The carve-out shape (precedent: r2a5/r2a5b, r4a2/r4a2b) lets r11a1 close cleanly on the safety patch and lets the contract decision be debated and shipped under its own merge with its own conventional-commit prefix.

### Project Structure Notes

- New code stays under `src/Hexalith.EventStore.Server/Projections/`. No new folders.
- New tests stay under `tests/Hexalith.EventStore.Server.Tests/Projections/`. No new test classes beyond the already-listed files.
- Sample edits stay under `samples/Hexalith.EventStore.Sample/Counter/Projections/`. Sample tests stay under `tests/Hexalith.EventStore.Sample.Tests/Counter/Projections/`.
- Submodule edits stay under `Hexalith.Tenants/src/Hexalith.Tenants/Projections/` (path B and C). Per `Hexalith.Tenants/CLAUDE.md`: Glob/Grep tools may silently fail on the submodule because ripgrep is not installed on the dev host — use `Bash` `grep -rn` and `find` for searches there.
- Use existing logging source-generator pattern (per the `Log` partial class in `ProjectionUpdateOrchestrator.cs:170-242`) for any new structured-log message under paths B/C (e.g., `Log.PriorStateReadFailed`).
- Conventional commits per CLAUDE.md § Commit Messages. Per-path prefix mapping is in AC C7.

### Architecture and Version Notes

- Package versions stay centrally pinned in `Directory.Packages.props`. Do not introduce new packages.
- DAPR actor execution is single-threaded per actor method, but `EventPublisher` can start concurrent background projection tasks. The existing `s_projectionLocks` per-aggregate `SemaphoreSlim` (`ProjectionUpdateOrchestrator.cs:39, 65, 165-166`) handles in-process concurrency. Multi-instance concurrency under at-least-once delivery is handled by the checkpoint's ETag-merged max-sequence write.
- Path B's prior-state read via DAPR state-store (PB-3 option (i)) reuses `DaprClient.GetStateAsync<T>` — no new actor proxy method needed. Path B option (ii) requires a new actor interface and a new actor-method round-trip per delivery.
- The `Hexalith.EventStore.Contracts` package is consumed via NuGet by external domain services AND by the in-tree `Hexalith.Tenants` submodule. Path B's BREAKING CHANGE applies to both.
- Per CLAUDE.md § "ID validation rule" (R2-A7): `MessageId`, `CorrelationId`, `AggregateId`, `CausationId` are ULIDs. This story does not touch any of those fields; the `ProjectionRequest.AggregateId` is a string carried opaquely.
- Per CLAUDE.md § "Integration test rule" (R2-A6): tier-2 and tier-3 tests must inspect state-store end-state. The B4/B5/C-path-5 tests must assert the persisted `ProjectionState` (or domain handler state under path C), not just call counts on mocks.

### Previous Story Intelligence

- R11-A1 retro and re-review: full replay was intentionally accepted as a sample-only safety net; this story closes the production decision. The R11-A1 Re-Review (2026-05-01) `Decision-Needed` row is the authoritative source for the regression scenario.
- R11-A2 polling-mode: the poller routes through `IProjectionUpdateOrchestrator.UpdateProjectionAsync`; whatever read shape this story picks, polling inherits it without a separate edit.
- R11-A3 AppHost proof: captured the running path against the safety patch. If path B or C lands, the AppHost proof's wire-shape evidence (request bodies, response bodies) will record the new shape — record this fact in the Decision Record if any path other than A is picked.
- R11-A4 valid round-trip: tier-3 contract test in `review`. Contract-agnostic at the API layer; not affected by this story.
- Story 11-3 ADR-3 in `_bmad-output/planning-artifacts/epics.md` (Epic 11 spec) explicitly chose full-replay for correctness in the Epic 11 timeframe. Path A formalizes that choice.
- Sample handler `CounterProjectionHandlerTests.cs:36-46`: the only multi-event test passes the full history in one batch, so the existing tests do not catch the incremental regression. Paths B/C add the missing coverage.
- Two non-virtual `DaprClient` members limit pure unit coverage of `DaprClient.GetStateAsync` interactions (per R11-A1 Dev Notes). Use the existing `HttpClient` fake-handler pattern where applicable; for path B's prior-state read, prefer the `IProjectionCheckpointTracker`-style boundary that wraps DAPR access behind an injectable interface if a clean unit boundary is needed.

### R11-A1b Decision Record

**Chosen path:** full-replay-permanent
**Firing trigger:** Trigger A - observed need: R11-A1 closure gate
**Rationale:** Path A is the smallest change that closes the parent R11-A1 gate while preserving the already-green safety patch. The sample `CounterProjectionHandler` still rebuilds from `CounterState state = new()` and the tenant handler still rebuilds `TenantReadModel state = new()` for the per-aggregate projection, so delta-only event delivery would corrupt state unless a new prior-state contract or handler-owned state obligation shipped. The downstream consumer grep shows `ProjectionRequest` is consumed by the in-tree `Hexalith.Tenants` submodule at `TenantProjectionHandler.cs` and `Program.cs`, so path B would create a synchronized public-contract release cost with no current operational signal demanding it. R11-A3 evidence recorded a working sample projection path with count 2 -> 3, regenerated ETag, SignalR silent reload, and Sample.Tests 63/63 PASS; it did not record full replay as a measurable cost on the sample stream. The post-change regressions pin `UpdateProjectionAsync_ImmediateDelivery_AlwaysReadsFullHistory` and `UpdateProjectionAsync_RepeatTriggersOnSameAggregate_ProducesIdenticalProjectionState`, keeping `GetEventsAsync(0)` explicit and making the rebuild-from-scratch contract visible to future maintainers. AC A2 option (a) is chosen: checkpoint sequence storage remains for polling coordination and observability; no checkpoint API is removed.

## References

- `_bmad-output/implementation-artifacts/post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md` — parent story; Re-Review (2026-05-01) section is the source of the gating CRITICAL.
- `_bmad-output/implementation-artifacts/epic-11-retro-2026-04-30.md` — R11-A1 action item.
- `_bmad-output/implementation-artifacts/post-epic-11-r11a2-polling-mode-product-behavior.md` — sibling polling story; routes through the same orchestrator.
- `_bmad-output/implementation-artifacts/post-epic-11-r11a3-apphost-projection-proof.md` — sibling AppHost evidence; provides the runtime trace baseline.
- `_bmad-output/implementation-artifacts/post-epic-11-r11a4-valid-projection-round-trip.md` — sibling tier-3 round-trip proof.
- `_bmad-output/implementation-artifacts/post-epic-2-r2a5b-version-prose-source-of-truth-refactor.md` — pattern precedent for paired structural-cure carve-out.
- `_bmad-output/implementation-artifacts/post-epic-4-r4a2b-backpressure-tracker-di-decision.md` — pattern precedent for binary architectural-decision carve-out with common + path-specific ACs.
- `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md` — design doc with the deferred contract-decision hedge sentences (lines 84, 91, 103, 164).
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:87-89` — the `GetEventsAsync(0)` call site this story decides about.
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:147-158` — the post-write checkpoint save block.
- `src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs:8-40` — checkpoint tracker interface (path-A option (b) modifies this).
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpoint.cs:11-16` — persisted checkpoint record (path-A option (b) modifies this).
- `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs:34-44` — projection actor write boundary; path B's PB-3 option (ii) extends this.
- `src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs:12-16` — public DTO; path B extends this.
- `src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs:14-16` — public DTO; unchanged.
- `src/Hexalith.EventStore.Contracts/Projections/ProjectionEventDto.cs:16-22` — public DTO; unchanged.
- `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs:21-36` — sample handler; paths B and C rewrite this.
- `samples/Hexalith.EventStore.Sample/Program.cs:39-43` — sample `/project` endpoint registration.
- `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs:20-62` — downstream consumer; paths B and C update this.
- `Hexalith.Tenants/CLAUDE.md` — submodule conventions including ripgrep workaround.
- `CLAUDE.md` — project conventions, commit messages, integration-test rule R2-A6, ID validation rule R2-A7.

## Dev Agent Record

### Agent Model Used

GPT-5.5 Codex

### Debug Log References

- AppHost pre-flight: `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`; Aspire resources healthy for `eventstore`, `sample`, `tenants`, `statestore`, `pubsub`, sidecars, admin, and sample UI.
- Pre-flight HEAD: `34884ac2935f8303b7a0f2d4a601a405cf8fd311`.
- Decision-evidence greps: `GetEventsAsync(0)` call-site count = 1 (`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:88`); `ProjectionRequest` downstream consumer count = 2 in `Hexalith.Tenants` (`TenantProjectionHandler.cs:28`, `Program.cs:94`).
- Handler-shape anchors confirmed: `CounterState state = new();`, `TenantReadModel state = new();`, `ProjectionStateKey = "projection-state";`, and `TenantProjectionKeyPrefix + request.AggregateId`.
- Baseline Tier 1: Contracts 281/281, Client 334/334, Sample 63/63, Testing 78/78, SignalR 32/32; total 788/788. Baseline Tier 2 Server.Tests: 1668/1668.
- Path-coherence greps: design-doc hedge text `pending the incremental projection contract decision` = 0 matches; orchestrator `GetEventsAsync(0)` = 1 match.
- Code-review format verification: `.claude/skills/bmad-code-review/` expects story/spec context and does not require a stricter Decision Record shape beyond readable story parsing; the required three-field block is retained.
- Semantic-release verification: `.releaserc.json` uses default `@semantic-release/commit-analyzer` / release-notes plugins with no custom `releaseRules` or preset. For path A with no API removal, recommended conventional-commit prefix is `refactor(server):` or `docs:` patch, not `feat!`.

### Completion Notes List

- Chose path A `full-replay-permanent` and AC A2 option (a), keeping checkpoint sequence storage for polling coordination and observability.
- Added Tier 2 regressions that prove immediate delivery ignores the persisted checkpoint for event reads and that repeated triggers over the same aggregate produce identical full-history projection state.
- Updated sample and tenant projection handler XML documentation to state the supported rebuild-from-scratch contract under full replay.
- Rewrote the server-managed projection builder design doc to remove the deferred contract hedge and record the full-replay production contract.
- Updated the parent R11-A1 story status to `review`, appended the R11-A1b closure block, and closed the deferred CRITICAL row with chosen path `full-replay-permanent`.
- Paths B and C were not executed because the chosen path was A; no `ProjectionRequest` wire-contract change, prior-state field, or handler-owned prior-state migration was introduced.

### File List

- `_bmad-output/implementation-artifacts/post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md`
- `_bmad-output/implementation-artifacts/post-epic-11-r11a1b-incremental-projection-contract-decision.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md`
- `samples/Hexalith.EventStore.Sample/Counter/Projections/CounterProjectionHandler.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`
- `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs`

### Verification Status

- Pre-change baseline: Tier 1 = 788/788; Tier 2 Server.Tests = 1668/1668.
- Post-change validation: Tier 1 = 788/788; Tier 2 Server.Tests = 1670/1670.
- Expected path-A delta: Tier 1 +0, Tier 2 +2. Equality holds: Tier 1 `788 = 788 + 0`; Tier 2 `1670 = 1668 + 2`.
- Targeted red/green path: new orchestrator regression tests failed first on implementation mistakes, then passed after fixing the test harness; final targeted run was 2/2 PASS.

### Review Findings (2026-05-02 /bmad-code-review)

Layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor (independent — priorities and severities reconciled by triage step).

#### Decision-Needed

- [x] [Review][Decision] **Design doc removed actor-blocking mitigation note for permanent full-replay** [`docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md:131`] — RESOLVED (option 3 compromise): added one-line caveat `Stream length is unbounded: under the full-replay contract GetEventsAsync(0) is O(N) in aggregate stream length. Operators should monitor stream length on hot aggregates; snapshotting is out-of-scope for the Epic 11 contract.` Records the trade-off honestly without committing to a follow-up story that current operational signal does not justify.

#### Patch

- [x] [Review][Patch] **Dead mock setup `ReadLastDeliveredSequenceAsync(...).Returns(7)` paired with `DidNotReceiveWithAnyArgs`** [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs:509`] — RESOLVED: dead `Returns(7)` removed. Test pins `DidNotReceiveWithAnyArgs.ReadLastDeliveredSequenceAsync` cleanly. 33/33 ProjectionUpdateOrchestrator suite PASS.
- [x] [Review][Patch] **`<pending merge SHA>` placeholder unresolved post-merge** [`_bmad-output/implementation-artifacts/post-epic-11-r11a1-checkpoint-tracked-projection-delivery.md`] — RESOLVED: both occurrences replaced with `7c7ce7e` (R11-A1b Closure block at `:216`, Decision-Needed `Closed by R11-A1b` row at `:280`). A5 row addendum did not include a merge SHA placeholder per AC A5 wording.
- [x] [Review][Patch] **`JsonSerializerOptions` allocated per fake-HTTP call; `PropertyNameCaseInsensitive = true` masks orchestrator casing** [`tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs:735-752`] — RESOLVED: `CountingProjectionResponseHandler` now uses the framework-cached `JsonSerializerOptions.Web` for both deserialization (request body) and serialization (response body). Matches DAPR's outgoing wire shape for `ProjectionRequest`; eliminates per-call allocation and casing-tolerance mask.

#### Defer

- [x] [Review][Defer] Commit `7c7ce7e` subject `Enhance server-managed projection builder design and tests` lacks any Conventional Commits prefix per CLAUDE.md and AC C7 — immutable on `main`; logged here as a process lesson (path A target was `refactor(server):` or `docs:`).
- [x] [Review][Defer] Commit `7c7ce7e` mixes the structural change with story-status flips, sprint-status YAML rewrite, full path-B / path-C task-box bulk-checks, and a substantive 51-line rewrite of unrelated story `post-epic-10-r10a2-redis-backplane-runtime-proof.md` (added/expanded ACs #1-#12, scope-boundary lines, and Tasks 0-6 sub-items). The R10-A2 spec rewrite was not gated through its own create-story flow — flag for retro and decide whether to revert/re-author under R10-A2's own track.
- [x] [Review][Defer] Path-B and Path-C subitems in r11a1b's Tasks/Subtasks block are checked `[x]` while annotated "N/A: path A chosen" — future `grep "[x] Update \`ProjectionRequest.cs\`"` returns false positive. Convert to `[ ]` with `(N/A)` annotation in a future docs sweep.
- [x] [Review][Defer] Test `UpdateProjectionAsync_RepeatTriggersOnSameAggregate_ProducesIdenticalProjectionState` uses sequential `await` despite the "Repeat triggers" name — does not exercise the per-aggregate semaphore. Rename or use `Task.WhenAll`.
- [x] [Review][Defer] `events.Max(e => e.SequenceNumber)` at `ProjectionUpdateOrchestrator.cs:148` is safe today via the early-return at `:92` but would throw `InvalidOperationException` if a future filter empties the array between the guard and the `Max` call — silently dropping the post-write checkpoint save. Speculative; track for the next orchestrator refactor.
- [x] [Review][Defer] No test covers the HTTP 4xx/5xx branch returned by the domain-service `/project` endpoint at `ProjectionUpdateOrchestrator.cs:117-118`.
- [x] [Review][Defer] `JsonValueKind.Null or Undefined` guard at `ProjectionUpdateOrchestrator.cs:126` misses `JsonValueKind.String` empty payloads — `"state": ""` passes the guard, gets serialized into `ProjectionState.StateBytes`, and `GetState()` later throws on read.
- [x] [Review][Defer] `httpClientFactory.CreateClient()` (no name) at `ProjectionUpdateOrchestrator.cs:116` has no test for the named-client wiring branch.
- [x] [Review][Defer] `ProjectionUpdateOrchestrator.ProjectionLocks` is a `static` field; xUnit parallel tests targeting `TestIdentity.ActorId == "test-tenant:test-domain:agg-001"` share the semaphore. Consider unique aggregate ids per test or a reset hook.
- [x] [Review][Defer] `capturedStates[1].StateBytes.ShouldBe(capturedStates[0].StateBytes)` is sequence-equality on `byte[]` today but silently flips to reference equality if `StateBytes` becomes `ReadOnlyMemory<byte>` or `ImmutableArray<byte>`.
- [x] [Review][Defer] Commit `7c7ce7e` body claims `Modified the CounterProjectionHandler to support rebuilding projection state from scratch` but the diff is doc-comment-only (3 added XML lines) — misleading commit body; immutable history.

#### Dismiss

- Decision Record uses plain hyphen instead of em-dash in `Trigger A - observed need: R11-A1 closure gate` — Debug Log References record code-review parser tolerance verification; literal-string match risk acknowledged but not fired.
- A4 test "tautology" claim — the differential `aggregateActor.GetEventsAsync(1).Returns([CreateTestEnvelope(2)])` stub means a regression to `GetEventsAsync(checkpoint)` would yield `count=1` vs `count=2`, so `capturedStates[1].StateBytes.ShouldBe(capturedStates[0].StateBytes)` would fail. The test pins regression at the orchestrator-mock layer per AC A4 explicit shape.
- Dead `GetEventsAsync(1).Returns(...)` stub — same reason as above; the stub provides differential output that produces a state divergence under regression.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-01 | 1.0 | Implemented path A full-replay-permanent decision, pinned regressions, updated docs and parent closure state. | Codex |
| 2026-05-02 | 1.1 | /bmad-code-review: 1 decision-needed, 3 patches, 11 deferred, 3 dismissed. Findings appended; deferred work mirrored to deferred-work.md. | Claude |
