# Post-Epic-4 R4-A2: Reconstruct Story 4.3 Execution Record

Status: review

<!-- Source: sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md - Proposal 2 (R4-A2) -->
<!-- Source: epic-4-retro-2026-04-26.md - Action item R4-A2 + §4 #3 + §6 (High-severity row) + §11 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **future agent or reviewer auditing Epic 4's pub/sub and backpressure delivery**,
I want the Story 4.3 file (`4-3-per-aggregate-backpressure.md`) to carry the same execution record that every other `done` story carries — task checklist, dev agent record, file list, change log, and final verification summary,
So that "Story 4.3 is done" can be trusted from the artifact alone without re-deriving the implementation from `git log` and code archeology.

## Story Context

The Epic 4 retrospective (`epic-4-retro-2026-04-26.md`) recorded R4-A2 as the highest-severity documentation gap of the epic (§6 row "Story 4.3 lacks final dev record — Severity: High"): Story 4.3 is marked `done` in `sprint-status.yaml`, the per-aggregate backpressure feature is implemented in `AggregateActor`, `BackpressureExceptionHandler`, `BackpressureOptions`, and exercised by `BackpressureTests`, `BackpressureOptionsTests`, `InMemoryBackpressureTrackerTests`, and `BackpressureExceptionHandlerTests` — but the story file itself stops at the acceptance criteria. There is no task checklist, no dev agent record, no file list, no change log, and no final verification summary.

The retrospective sized R4-A2 at "needs documentation reconstruction story" (§1 R4-A2 row) and not "needs reimplementation" — the code is already in main. Sprint-change-proposal §2 (Proposal 2) lists the exact missing sections: task checklist mapping AC #1–#12 to implementation/tests, dev agent record with model, completion notes, verification summary, file list, change-log entry, and explicit final verification status with any unrun-test caveats.

**Scope boundary (what this story does NOT do):**

- It does NOT change source code under `src/`. The implementation is already in main as of commits `85f55a4` (initial) and `0748651` (handler hardening / `IBackpressureTracker` introduction).
- It does NOT add or remove tests under `tests/`. The four test files listed in the Implementation Inventory below already exist and are already part of the Tier 1 / Tier 2 baselines.
- It does NOT re-design backpressure. The actor-level fail-open state read, the `pending_command_count` actor-state key, the `BackpressureCheck` activity, and the HTTP 429 + `Retry-After` shape are all in place — this story records what is, it does not refactor.
- It does NOT close R4-A5, R4-A6, or R4-A8. Those have their own backlog rows in `sprint-status.yaml`.

**Editing target.** The single editable artifact for this story is `_bmad-output/implementation-artifacts/4-3-per-aggregate-backpressure.md`. The implementation source files are read-only references for reconstruction; do not modify them.

## Acceptance Criteria

1. **Story 4.3 file gains a `## Tasks / Subtasks` section that maps each AC #1–#12 to the production code and test code that proves it.** Each task line follows the pattern `- [x] Task N — <description> (AC: #X, #Y)` followed by `  - [x] N.1 ... (file:line)` subtask references. All tasks and subtasks are checked (`[x]`), because the work is already merged. The mapping must cover every AC #1–#12 — no AC may be left without at least one task line. Source-of-truth for the mapping is the Implementation Inventory section below.

2. **Story 4.3 file gains a `## Dev Notes` section** that includes (a) the project-wide Testing Standards block reproduced verbatim from `.claude/skills/bmad-create-story/template.md` with per-rule applicability annotations, (b) a `### Project Structure Notes` block listing the production source files and test files actually touched by Story 4.3 (matching the file list from commits `85f55a4` and `0748651`), and (c) a `### References` block with at least one [Source: ...] citation per AC, pointing to file:line in the live codebase.

3. **Story 4.3 file gains a populated `## Dev Agent Record` section** with: an `### Agent Model Used` line that reproduces the original commit author/co-author attribution from `git log` (commit `85f55a4` records `Co-Authored-By: Claude Opus 4.6 (1M context)`); a `### Completion Notes List` summarizing the verification outcome (see AC #5); and a `### File List` enumerating every production file and every test file affected by Story 4.3, grouped by repository area.

4. **Story 4.3 file gains a `## Change Log` section** with at least one row recording the original implementation merge (commit subject `feat: Implement Story 4.3 — Per-Aggregate Backpressure`, SHA `85f55a4`, PR #107, merge SHA `c870241`, date 2026-03-17), one row recording the follow-up command-API hardening (commit subject `feat: Implement backpressure handling in command API`, SHA `0748651`, date 2026-03-17), and one row recording the R4-A2 documentation reconstruction itself (this story). Date column uses ISO YYYY-MM-DD. Change Log columns: `Date | Version | Description | Author`. Source SHAs and dates are pulled from the actual `git log` and must not be invented.

5. **Story 4.3 file gains a `## Verification Status` section.** AC #5 has four mandatory sub-conditions (#5a, #5b, #5c, #5d) — **all four must hold for AC #5 to be considered met.** The section is plain prose plus a small table; format matches the `## Verification Status` precedent in `_bmad-output/implementation-artifacts/post-epic-3-r3a7-live-command-surface-verification.md` if that file is available, otherwise use a plain `Tier | Suite | Pass | Fail | Skipped | Notes` table. The dev MUST check off each of #5a–#5d separately during execution; marking AC #5 done while any sub-AC is unaddressed is a defect.

5a. **Test counts for the four backpressure-specific test files are recorded** at the time R4-A2 closes. Counts come from the test runner's actual output (per Task 6.2 capture), not from a method-name grep — see Task 0.2's hedge note for why the runner can exceed the grep when `[Theory]` rows expand.

5b. **Local Tier 1 and Tier 2 baselines are recorded** at the time R4-A2 closes. These are the Task 0.3 baseline numbers and the Task 6.2 post-story numbers; both must match (AC #11). If they diverge, the story is not closeable until the divergence is investigated.

5c. **Tier 3 unrun-test caveat is recorded explicitly.** The live Aspire/DAPR Tier 3 backpressure path was NOT exercised by Story 4.3 (see the `### Tier 3 (live Aspire) coverage status` subsection below in this Implementation Inventory). That fact MUST be recorded as an unrun-test caveat in the Verification Status block — not omitted, not implied, not dropped to a footnote. The caveat names `post-epic-4-r4a5-tier3-pubsub-delivery` as the routing target.

5d. **Production-DI gap discovered during reconstruction is recorded as a caveat.** Specifically, the `IBackpressureTracker` non-registration finding documented in Implementation Inventory item #5 is recorded as a caveat in the Verification Status block. This story does NOT invent a fix; the wire-or-delete decision is carved out to `post-epic-4-r4a2b-backpressure-tracker-di-decision` per AC #12.

6. **Story 4.3 status field stays `done`.** The `Status:` line at the top of `4-3-per-aggregate-backpressure.md` must remain `done` at the end of this story. This story RECONSTRUCTS the execution record of an already-`done` story; it does not transition or revisit completion. No regression of the existing `Status: done` line is allowed.

7. **Story 4.3 existing content is not edited.** The existing Story header (Status line), Story statement, AC #1–#12 list, and Definition of Done block in `4-3-per-aggregate-backpressure.md` must remain byte-identical post-reconstruction. The reconstruction adds new sections; it does not rewrite or paraphrase any of the existing content. **Mechanical check (content-anchored, not line-numbered):** running `git diff` on `4-3-per-aggregate-backpressure.md` after this story must show only additions starting **after** the existing `### Definition of Done` block (line 39 in the file as of HEAD `8028cf2`; verify with `wc -l` and a tail-read at execution time, since trailing whitespace can shift the boundary by one line). Pre-existing content above the new `## Tasks / Subtasks` insertion point is read-only.

8. **No source or test code is modified by this story.** The diff for this story consists of: this story file (`post-epic-4-r4a2-story-4-3-execution-record.md`), edits to `4-3-per-aggregate-backpressure.md`, and the `sprint-status.yaml` row flip per AC #10 + sibling carve-out row per AC #12. No `.cs`, no `.csproj`, no test file, no source-file XML comment edits. This AC mirrors `post-epic-3-r3a8` AC #11's null-by-design rule. **R4-A8** (story numbering source-comment cleanup) has its own backlog row and is explicitly NOT in scope here.

9. **Cross-references to sibling R4-* stories are correct.** The new Dev Notes section names the routing for all 8 R4-* action items (R4-A1 applied directly to Story 4.2; R4-A3 covered by `post-epic-3-r3a1-replay-ulid-validation`; R4-A4 covered by `post-epic-3-r3a7-live-command-surface-verification`; R4-A5/A6/A8 remain `backlog`; new sibling `post-epic-4-r4a2b-backpressure-tracker-di-decision` is `backlog` per AC #12; R4-A7 preserves existing Epic-1/Epic-2 routing). Format: **prose paragraph ≤6 lines OR a small table** (3 columns: Action | Status | Routing) — the dev's choice, but a table is recommended once the row count exceeds 4 because 8 sibling references in 6 prose lines is tight. Cite the relevant `sprint-status.yaml` rows under the `# Post-Epic-4 Retro Cleanup` section header (anchor by section, not line number) and `epic-4-retro-2026-04-26.md` §8 as evidence. The intent is to make Story 4.3's reconstructed record self-locating in the post-Epic-4 cleanup family without duplicating the retrospective.

10. **Sprint-status bookkeeping is closed.** `_bmad-output/implementation-artifacts/sprint-status.yaml` shows `post-epic-4-r4a2-story-4-3-execution-record` flipped from `ready-for-dev` to `review` (or to `done` if `code-review` already ran and signed off — see Dev Notes for the transition ownership rule). Both the leading-comment `last_updated:` line AND the YAML `last_updated:` key are updated with today's UTC date and a one-line note naming this story. The Status: line at the top of THIS story file is also updated to `review` to match. Same rule as `post-epic-3-r3a6` AC #10 / `post-epic-3-r3a7` AC #11 — bookkeeping is non-negotiable.

11. **Test impact is null-by-design AND mandatorily verified.** No code is changed (AC #8), so Tier 1 / Tier 2 / Tier 3 pass counts at story close MUST equal the local baseline at story start. **Mathematical certainty without measurement is not evidence** — Tier 1 + Tier 2 baseline capture per Task 0.3 and post-story re-run per Task 6.2 are **both mandatory** for AC #11 closure. The Dev Agent Record records the captured numbers and the equality check; if any test that was green pre-story goes red post-story it is a defect requiring investigation before story close (root-cause is almost certainly NOT this story's diff, but the equality check is the gate that proves it). Tier 3 is not in scope for re-run — the AC #5c caveat already records its non-execution status.

12. **Sibling carve-out `post-epic-4-r4a2b-backpressure-tracker-di-decision` exists with explicit promotion trigger.** The wire-or-delete decision for `IBackpressureTracker` (the AC #5d caveat) is **deliberately carved out** to a sibling story `post-epic-4-r4a2b-backpressure-tracker-di-decision`. The carve-out is added to `_bmad-output/implementation-artifacts/sprint-status.yaml` under the `# Post-Epic-4 Retro Cleanup` block with status `backlog`, in the same diff as this story's creation (added at story-creation time as part of party-mode review patches, 2026-04-30; the dev's job at Task 5 is verification, not creation). R4-A2 is documentation-only by design (AC #8 / #11); R4-A2b will make the binary decision: (i) wire `IBackpressureTracker` into production DI as a second line of defense beyond the actor-level Step 2b check, OR (ii) delete the `IBackpressureTracker` interface, `InMemoryBackpressureTracker` implementation, and the corresponding `SubmitCommandHandler` constructor overloads as dead code.

    **Promotion trigger (R4-A2b: `backlog` → `ready-for-dev`).** Without an explicit trigger, R4-A2b can sit in `backlog` indefinitely and the AC #5d caveat stays dormant — exactly the failure mode advanced-elicitation patterns warn against. R4-A2b MUST be promoted from `backlog` to `ready-for-dev` once **either** of the following first becomes true:
    - **Trigger A — observed need:** The next backpressure-touching code change is being authored (any change that imports `IBackpressureTracker`, modifies `SubmitCommandHandler` constructor signatures, or touches `BackpressureOptions` semantics). The wire-or-delete decision lands as part of work that already touches the constructor-selection surface, minimizing marginal cost.
    - **Trigger B — calendar SLA:** Within 4 weeks of R4-A2 close (target: 2026-05-28). Trigger B is the hard ceiling that prevents R4-A2b from sliding into perpetual backlog if Trigger A's signal never materializes.

    The promotion is performed at sprint-status update time (Bob, the project lead, or whichever skill triggers the flip) and is recorded in the same `last_updated` line that flips R4-A2b. The carve-out is added by the same diff that creates R4-A2 (added at story-creation time as part of party-mode review patches, 2026-04-30); it is not a future-tense "should be filed" — verify its row exists.

    **Caveat-update obligation when R4-A2b resolves.** When R4-A2b lands and makes the binary wire-or-delete decision, the R4-A2b dev MUST update the reconstructed Story 4.3's AC #5d caveat note (in the Verification Status block) to reflect the resolution. **Concrete obligations on R4-A2b:** (i) if `IBackpressureTracker` is wired into production DI, the caveat note becomes "**Resolved at <SHA> by R4-A2b** — `IBackpressureTracker` is now registered as `<Lifetime>` in `<file:line>`; both actor-level Step 2b and pipeline-level tracker now enforce backpressure. Wiring path: <one-sentence summary>." (ii) if the interface + implementation are deleted as dead code, the caveat note becomes "**Resolved at <SHA> by R4-A2b** — `IBackpressureTracker` and `InMemoryBackpressureTracker` removed as dead code; actor-level Step 2b is the sole enforcement path. The `SubmitCommandHandler` constructor overloads accepting `IBackpressureTracker` are also removed." This obligation is a hard contract on R4-A2b — without it, the caveat note in the reconstructed Story 4.3 silently lies after R4-A2b ships. R4-A2b's own AC list MUST include "update Story 4.3's AC #5d caveat note per R4-A2 AC #12 caveat-update obligation" as a non-skippable AC.

## Implementation Inventory

**This block is the source of truth for the AC #1 task-mapping and AC #3 file list reconstructions.** It enumerates the production and test code that Story 4.3 actually shipped, grouped by AC. Use this to populate the new sections in `4-3-per-aggregate-backpressure.md`. **Do not edit the source files** — they are listed read-only.

**File:line citations are pinned to HEAD `8028cf2`.** Verify every cited line at execution time per Task 0.1 (the file may have drifted since this spec was authored). When you reconstruct the Story 4.3 file list, **record the actual HEAD SHA you observed** in the reconstructed inventory's header so future agents reading the reconstructed file can re-anchor citations against a known-good commit.

**The "Story 4.3 ACs covered" column is the *primary* file responsibility, not an exhaustive ownership map.** Some Story 4.3 ACs depend on multi-file interaction — notably AC #4 (counter atomicity), where the increment (Inventory item #6 line 230), decrement (item #6 lines 547-551, 703-708, 1018), state-machine checkpointing, and `SaveStateAsync` ordering all participate. Treat the column as a starting point, not a contract; some ACs reference more files than the column lists.

### Production source files (already merged)

| # | File | Role | Story 4.3 ACs covered |
|---|------|------|---|
| 1 | `src/Hexalith.EventStore.Server/Configuration/BackpressureOptions.cs` | `BackpressureOptions` record + `ValidateBackpressureOptions` | AC #8 (configurable threshold + startup validation) |
| 2 | `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:54-57` | DI registration of `BackpressureOptions` (`AddOptions<BackpressureOptions>().Bind(...).ValidateOnStart()`) and `IValidateOptions<BackpressureOptions>` | AC #8 |
| 3 | `src/Hexalith.EventStore.Server/Actors/BackpressureExceededException.cs` | Actor-thrown exception variant carrying `CorrelationId`, `TenantId`, `Domain`, `AggregateId`, `PendingCount`, `Threshold` | AC #2 (HTTP 429 plumbing source), AC #11 (logging context) |
| 4 | `src/Hexalith.EventStore.Server/Commands/BackpressureExceededException.cs` | Pipeline-thrown exception variant (server-side logging fields only — never serialized to client per file XML doc, lines 5–8) | AC #2 (HTTP 429 plumbing source) |
| 5 | `src/Hexalith.EventStore.Server/Commands/IBackpressureTracker.cs` + `InMemoryBackpressureTracker.cs` | Pipeline-layer per-aggregate counter (`ConcurrentDictionary` + CAS); shipped but **not** registered in production DI as of HEAD `8028cf2` (verified by Task 0.5 grep). The `(ICommandStatusStore, ICommandArchiveStore, ICommandRouter, ILogger)` 4-arg `SubmitCommandHandler` constructor at `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:30` is **expected to be** selected per default `Microsoft.Extensions.DependencyInjection` constructor-selection semantics (longest-resolvable-constructor; the 5-/6-arg overloads have an unresolvable `IBackpressureTracker` parameter and are skipped). **This is the expected runtime behavior, not verified at runtime by this story.** Task 0.5 confirms the registration absence; runtime constructor-selection observation is left to R4-A2b (per AC #12) when the wire-or-delete decision is made. **This finding is recorded as a Verification Status caveat (AC #5d); reconstruction does NOT add a DI registration in this story.** | AC #1 (defense in depth — actor-level check is the active enforcement, regardless of which `SubmitCommandHandler` constructor wins) |
| 6 | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | Step 2b backpressure check (lines 184–234): `BackpressureCheck` activity, `ReadPendingCommandCountAsync` (line 806) with **fail-open** on state-read failure (lines 197–205), threshold compare against `BackpressureOptions.MaxPendingCommandsPerAggregate`, `Log.BackpressureRejected` (line 209) emitted as Warning, returns `CommandProcessingResult(Accepted: false, BackpressureExceeded: true, BackpressurePendingCount, BackpressureThreshold)`. Increment via `StagePendingCommandCountAsync(pendingCount + 1)` (line 230); decrement on terminal status at lines 547–551, 703–708, 1018; helpers at 806–826. | AC #1, #3, #4, #5, #7, #10, #11 |
| 7 | `src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs:23-25` | Adds `BackpressureExceeded`, `BackpressurePendingCount`, `BackpressureThreshold` `[DataMember]` fields on the actor-result record | AC #1 (DAPR-serialized signal) |
| 8 | `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:32-47, 157-164` | Two new constructor overloads accepting `IBackpressureTracker` (delegate to primary), and the `BackpressureExceeded` flag check that throws `Hexalith.EventStore.Server.Actors.BackpressureExceededException` to be caught by the API-layer handler | AC #2 |
| 9 | `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs:42` | Adds `BackpressureCheck = "EventStore.Actor.BackpressureCheck"` activity name constant | AC #1 (tracing) |
| 10 | `src/Hexalith.EventStore/ErrorHandling/BackpressureExceptionHandler.cs` | `IExceptionHandler` returning HTTP 429 + RFC 7807 ProblemDetails. Sets `Retry-After` from `BackpressureOptions.RetryAfterSeconds` (line 44, 62), content type `application/problem+json` (line 20, 63). Walks inner-exception chain up to depth 10 (lines 69–95) so wrapped exceptions are still mapped. | AC #2 |
| 11 | `src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs:17` | Adds `BackpressureExceeded = "https://hexalith.io/problems/backpressure-exceeded"` URI constant | AC #2 |
| 12 | `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` | Registers `BackpressureExceptionHandler` in the global `IExceptionHandler` chain | AC #2 |
| 13 | `src/Hexalith.EventStore/OpenApi/ErrorReferenceEndpoints.cs:85-87` | Adds the `/api/v1/errors/backpressure-exceeded` reference page metadata | AC #2 (consumer-visible contract) |
| 14 | `docs/reference/problems/backpressure-exceeded.md` | Published documentation page at the ProblemDetails type URI | AC #2 (consumer-visible contract) |

### Test files (already merged)

| # | File | Test count | Story 4.3 ACs covered |
|---|------|---|---|
| T1 | `tests/Hexalith.EventStore.Server.Tests/Actors/BackpressureTests.cs` | 13 `[Fact]`s (see method names below) | AC #1, #3, #4, #5, #6, #7, #9, #10 |
| T2 | `tests/Hexalith.EventStore.Server.Tests/Configuration/BackpressureOptionsTests.cs` | 7 tests (defaults, configuration binding, four validation rejections, one validation acceptance) | AC #8 |
| T3 | `tests/Hexalith.EventStore.Server.Tests/Commands/InMemoryBackpressureTrackerTests.cs` | 11 tests (acquire under/at/over threshold, release/decrement, per-aggregate independence, floor-at-zero, default-100, custom threshold, async concurrency at threshold, dictionary-entry removal at zero, threshold-zero disable) | Defense-in-depth tracker (covers AC #1 / #3 backstop) |
| T4 | `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/BackpressureExceptionHandlerTests.cs` | 7 async tests (429 status, `Retry-After` header, ProblemDetails body, correlation+tenant extensions, aggregate identity extension, non-backpressure exception ignored, wrapped exception walked) | AC #2, #11 |

`BackpressureTests.cs` `[Fact]` method names — copy these into the reconstructed task list as `(AC: #N)` evidence rows:

```
ProcessCommand_PendingCountAtThreshold_Rejected                            (line 128)  AC #1
ProcessCommand_PendingCountAboveThreshold_Rejected                         (line 144)  AC #1
ProcessCommand_PendingCountBelowThreshold_Accepted                         (line 158)  AC #1
ProcessCommand_DuplicateCommand_BypassesBackpressure                       (line 178)  AC #9
ProcessCommand_DifferentAggregates_IndependentBackpressure                 (line 195)  AC #3
ProcessCommand_Accepted_IncrementsPendingCount                             (line 229)  AC #4
ProcessCommand_Completed_DecrementsPendingCount                            (line 244)  AC #4
PendingCount_DefaultsToZero_WhenMissing                                    (line 267)  AC #7
PendingCount_SurvivesActorReactivation                                     (line 285)  AC #5
ProcessCommand_BackpressureRejected_NoStateRehydrationOrDomainInvocation   (line 302)  AC #1 (fast-fail proof)
ProcessCommand_BackpressureCheckStateReadFails_FailsOpen                   (line 319)  AC #1 (resilience)
DrainSuccess_DecrementsPendingCount                                        (line 351)  AC #6
ProcessCommand_TenantMismatch_DoesNotTouchPendingCount                     (line 394)  AC #1 (ordering: tenant before backpressure)
```

### Implementation history

- **2026-03-17** — commit `85f55a4` `feat: Implement Story 4.3 — Per-Aggregate Backpressure`. Initial implementation of all 12 ACs. Author: Quentin Dassi Vignon. Co-Author: `Claude Opus 4.6 (1M context)`. Touched 26 files (+2591 / -3448 lines, includes a large `review-diff.txt` snapshot reduction).
- **2026-03-17** — commit `0748651` `feat: Implement backpressure handling in command API`. Pipeline-side hardening: introduced `Hexalith.EventStore.Server.Commands.BackpressureExceededException` (separate from the actor-side variant), `IBackpressureTracker` + `InMemoryBackpressureTracker`, refactored `SubmitCommandHandler` to throw the pipeline-side exception when the actor result carries `BackpressureExceeded: true`, added `BackpressureTests.cs` (Tier 2). Author: Jérôme Piquot. Includes resolved merge conflicts noted in the commit body.
- **2026-03-17** — merge `c870241` (PR #107 `feat/story-4.3-per-aggregate-backpressure`) lands both commits on `main`.
- **Subsequent maintenance** — `91e3c77 refactor(test): modernize and align test code style` (Tier 2 test file format normalization, no semantic changes to backpressure logic).

### Tier 3 (live Aspire) coverage status

**Not exercised by Story 4.3.** No Tier 3 (`tests/Hexalith.EventStore.IntegrationTests/`) test asserts the end-to-end backpressure path against a running Aspire topology. The `ConcurrencyConflictIntegrationTests.cs:285` reference to `InMemoryBackpressureTracker` is a positive-path fixture wiring (so the simulating handler still satisfies the constructor signature when the API host is built), not a backpressure assertion.

This is a known and intentional carry-over to **`post-epic-4-r4a5-tier3-pubsub-delivery`** — that story is the right place for live-topology backpressure verification. R4-A2 records the Tier 3 gap as an explicit caveat per AC #5c; it does NOT close it.

## Tasks / Subtasks

- [x] **Task 0 — Pre-flight verification of the Implementation Inventory** (AC: #1, #3, #4, #5, #11)
  - [x] 0.1 Open this story's Implementation Inventory section. For each production file in the table (rows 1–14), confirm the file exists at the cited path and that the cited line numbers still resolve to the cited code. If any line has drifted, locate the symbol by literal substring (e.g., `pendingCount >= bpOptions.MaxPendingCommandsPerAggregate`) and note the new line in the reconstructed Story 4.3 references.
  - [x] 0.2 For each test file in the inventory (rows T1–T4), run the file alone (e.g., `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~BackpressureTests"`) and capture the **test-runner output count** (not a method-name grep). Method-name grep results as of HEAD `8028cf2`: T1 = 13 `[Fact]`-decorated methods, T2 = 7 methods, T3 = 11 methods, T4 = 7 async methods — **but the runner can exceed the method count if any method becomes a `[Theory]` after publication, since `[Theory]` rows expand into multiple data rows.** Capture the runner output, not the grep — the AC #5a Verification Status row uses runner numbers. **If the runner count differs from the grep, do not "fix" anything — record the actual runner count in the reconstructed Verification Status block per AC #5a and flag the divergence in the Dev Agent Record.**
  - [x] 0.3 Capture the local Tier 1 and Tier 2 baseline pass counts (per CLAUDE.md command list). These are the AC #5b baseline numbers. **AC #11 has been tightened to make this baseline mandatory** — the post-story re-run at Task 6.2 must match these baseline numbers exactly. Mathematical certainty without measurement is not evidence.
  - [x] 0.4 Capture the current `sprint-status.yaml` state of `post-epic-4-r4a2-story-4-3-execution-record` (expected: `ready-for-dev`) into the Dev Agent Record's starting-state line — evidence for AC #10 at Task 5.
  - [x] 0.5 Confirm the `IBackpressureTracker` non-registration finding (Implementation Inventory item #5). Use this comprehensive grep (covers Singleton/Scoped/Transient + factory delegates + generic-arg registrations): `grep -rE '\b(Try)?Add(Singleton|Scoped|Transient)\b.*\bIBackpressureTracker\b' src/` AND `grep -rE 'services\.Add<.*IBackpressureTracker.*>' src/`. Expected: zero matches in `src/` for both. If a registration HAS been added since this story was authored, demote the AC #5d caveat to a one-line resolved note (cite the registration's file:line) AND demote AC #12's R4-A2b carve-out to "superseded by upstream registration in commit <SHA>" — do not invent a duplicate fix.

- [x] **Task 1 — Reconstruct the `## Tasks / Subtasks` section in Story 4.3** (AC: #1, #7)
  - [x] 1.1 Open `_bmad-output/implementation-artifacts/4-3-per-aggregate-backpressure.md`. **Do not modify lines 1–41 (Status header through Definition of Done block).**
  - [x] 1.2 Append a new `## Tasks / Subtasks` section after the Definition of Done block. Use the AC #1 mapping pattern: one `Task N` per logical work area (BackpressureOptions, AggregateActor Step 2b, CommandProcessingResult, SubmitCommandHandler integration, BackpressureExceptionHandler, error reference page, test files). Each task line is `- [x]` (closed) and references one or more ACs from the existing AC #1–#12 list. Each subtask line cites a `file:line` from the Implementation Inventory.
  - [x] 1.3 Coverage check: every **Story 4.3 AC #1–#12** (the 12 ACs in the existing `4-3-per-aggregate-backpressure.md` body, NOT this post-epic story's AC #1–#12) must appear in at least one `(AC: #N)` annotation in the reconstructed Tasks block. Run `grep -oE "AC: #[0-9]+|AC: #[0-9]+, " <newly written tasks block>` and confirm 1..12 all present. Story 4.3's AC #12 ("All existing tests pass") maps to Task 0.2 / 0.3's baseline-equality capture.

- [x] **Task 2 — Reconstruct the `## Dev Notes` section in Story 4.3** (AC: #2, #9)
  - [x] 2.1 Append `## Dev Notes` after the Tasks / Subtasks block.
  - [x] 2.2 Reproduce the `### Testing Standards (project-wide rules — apply to every story)` block verbatim from `.claude/skills/bmad-create-story/template.md` lines 30–34 (Tier 1 + Tier 2 / Tier 3 + ID-validation rules). **Read-only access. Do NOT modify `.claude/skills/bmad-create-story/template.md`** — it is a BMAD method file, off-limits per project rule (memory entry "Don't edit BMAD method files"; precedent: R3-A8 supersedure note in `sprint-status.yaml`). Pin the source SHA at execution time so the reproduction is traceable to a specific template revision. After each rule, add a parenthesized applicability annotation: Tier 1 → "**Applicable** — `BackpressureOptionsTests.cs`, `InMemoryBackpressureTrackerTests.cs`, `BackpressureExceptionHandlerTests.cs`"; Tier 2 / Tier 3 → "**Applicable** — `BackpressureTests.cs` (Tier 2 mocked actor); Tier 3 backpressure path **not** exercised by Story 4.3 — see Verification Status caveat and `post-epic-4-r4a5-tier3-pubsub-delivery`"; ID validation → "**Not applicable** — Story 4.3 does not introduce a new controller or validator surface; HTTP 429 path is exception-handler-driven and reads `correlationId` from `CorrelationIdMiddleware.HttpContextKey`".
  - [x] 2.3 Add a `### Project Structure Notes` sub-block listing the 14 production files + 4 test files from the Implementation Inventory. Group by repo area: `Server/Configuration/`, `Server/Actors/`, `Server/Commands/`, `Server/Pipeline/`, `Server/Telemetry/`, `EventStore/ErrorHandling/`, `EventStore/Extensions/`, `EventStore/OpenApi/`, `docs/reference/problems/`, `tests/Hexalith.EventStore.Server.Tests/`. Each file gets one line.
  - [x] 2.4 Add a `### References` sub-block. At minimum, every AC #1–#12 needs a [Source: ...] line to file:line in `src/` or `tests/`. Pull file:line citations from the Implementation Inventory table — do not invent line numbers, copy them from the inventory.
  - [x] 2.5 Add the AC #9 ≤4-line cross-reference paragraph naming R4-A1 (resolved in 4.2), R4-A3 (covered by `post-epic-3-r3a1-replay-ulid-validation`), R4-A4 (covered by `post-epic-3-r3a7-live-command-surface-verification`), R4-A5/A6/A8 (`backlog`), R4-A2b (new sibling `backlog`, see AC #12), R4-A7 (existing Epic-1/Epic-2 routing). Cite the relevant `sprint-status.yaml` rows under the `# Post-Epic-4 Retro Cleanup` block and `epic-4-retro-2026-04-26.md` §8 — anchor by content header, not line number, since rows shift as the file grows.

- [x] **Task 3 — Reconstruct the `## Dev Agent Record` section in Story 4.3** (AC: #3, #11)
  - [x] 3.1 Append `## Dev Agent Record` after the Dev Notes block.
  - [x] 3.2 `### Agent Model Used` — record `Claude Opus 4.6 (1M context)` (the original-implementation model, per commit `85f55a4` `Co-Authored-By:` trailer) AND the model running THIS reconstruction session, in the form: `original implementation: Claude Opus 4.6 (1M context); documentation reconstruction: <name + version of model executing this task>`. **Resolve the angle-bracket placeholder at execution time — do NOT commit literal `<...>` to the artifact.** Do NOT leave `TBD`.
  - [x] 3.3 `### Completion Notes List` — bullet list summarizing: (a) all 12 Story 4.3 ACs implemented at PR #107 / SHA c870241; (b) 38 dedicated backpressure tests across 4 files (method-name grep: 13 + 7 + 11 + 7; **runner counts captured per Task 0.2 may differ if `[Theory]` rows expanded**); (c) no source/test diff in this reconstruction story per AC #8, mandatory baseline-equality re-run per AC #11 / Task 6.2; (d) the AC #5c Tier 3 caveat (live backpressure path deferred to `post-epic-4-r4a5-tier3-pubsub-delivery`); (e) the AC #5d `IBackpressureTracker` DI-gap caveat (defense-in-depth pipeline tracker is shipped + tested but unregistered in production DI per Task 0.5; wire-or-delete decision deferred to sibling `post-epic-4-r4a2b-backpressure-tracker-di-decision` per AC #12). Do NOT speculate about cause or claim a fix is in scope.
  - [x] 3.4 `### File List` — flat list of every file path from the Implementation Inventory (14 production + 4 test = 18 entries). Group with subheadings per Task 2.3. Each path is on its own line, no truncation, no glob shorthand.

- [x] **Task 4 — Reconstruct the `## Change Log` and `## Verification Status` sections in Story 4.3** (AC: #4, #5, #11)
  - [x] 4.1 Append `## Change Log` after the Dev Agent Record.
  - [x] 4.2 Add three rows in the table `| Date | Version | Description | Author |`. **For Row 3 only: both the Date AND Author fields contain `<...>` placeholders that MUST be resolved at execution time — substitute today's UTC `YYYY-MM-DD` for the Date and your dev name + model for the Author. Do NOT commit literal `<...>` to the artifact.** Rows 1 and 2 are pre-filled and require no execution-time edits. Match the git author/co-author format: `Author (Co-Authored-By: ...)` where the Co-Authored-By line is the verbatim git commit trailer.
    - Row 1: `2026-03-17 | 1.0 | Initial Story 4.3 implementation merged via PR #107 (commit 85f55a4, merge c870241). All 12 Story 4.3 ACs implemented; 38 backpressure-specific tests added (method-name count) across BackpressureTests, BackpressureOptionsTests, InMemoryBackpressureTrackerTests, BackpressureExceptionHandlerTests. | Quentin Dassi Vignon (Co-Authored-By: Claude Opus 4.6 (1M context))`
    - Row 2: `2026-03-17 | 1.1 | Pipeline-side hardening: pipeline-layer BackpressureExceededException variant, IBackpressureTracker + InMemoryBackpressureTracker, SubmitCommandHandler refactor (commit 0748651). | Jérôme Piquot`
    - Row 3 (placeholders to resolve): `<YYYY-MM-DD> | 1.2 | R4-A2 documentation reconstruction per party-mode-reviewed + advanced-elicitation-patched spec: added Tasks/Subtasks, Dev Notes, Dev Agent Record, File List, Change Log, Verification Status sections. No source/test diff. AC #5d DI-gap caveat recorded with caveat-update obligation on R4-A2b per AC #12; wire-or-delete decision carved out to sibling post-epic-4-r4a2b. | <dev name + model>`
  - [x] 4.3 Append `## Verification Status` after the Change Log.
  - [x] 4.4 Format: short prose paragraph + a `Tier | Suite | Pass | Fail | Skipped | Notes` table. Populate the table with Task 0.2 (runner counts) / 0.3 (Tier 1 + Tier 2 baselines) / 6.2 (post-story re-run for AC #11 equality check). The Notes column for Tier 3 explicitly says "backpressure path NOT exercised by Story 4.3 — deferred to post-epic-4-r4a5-tier3-pubsub-delivery" (AC #5c). Include the AC #5d `IBackpressureTracker` DI-gap caveat **as a separate `**Caveat (AC #5d):**` paragraph immediately below the table, before any other content** — naming the sibling carve-out `post-epic-4-r4a2b-backpressure-tracker-di-decision` per AC #12 and reproducing the dual-`BackpressureExceededException` shape note from this story's Dev Notes. Record the gap as found, do not file a fix in this story.

- [x] **Task 5 — Sprint-status bookkeeping, self-status, sibling carve-out verification** (AC: #6, #10, #12)
  - [x] 5.1 Verify the `Status:` line at the top of `4-3-per-aggregate-backpressure.md` is still `done` after Tasks 1–4. The reconstruction adds new sections; it must not touch the Status line. AC #6 mechanical check.
  - [x] 5.2 Run `git diff _bmad-output/implementation-artifacts/4-3-per-aggregate-backpressure.md` and confirm only additions appear **starting after** the existing `### Definition of Done` block (the Status header, Story statement, AC #1–#12 list, and DoD block above the new Tasks insertion point are byte-identical to HEAD). AC #7 mechanical check, content-anchored, not line-numbered.
  - [x] 5.3 Update `_bmad-output/implementation-artifacts/sprint-status.yaml`: flip `post-epic-4-r4a2-story-4-3-execution-record` from `ready-for-dev` to `review`. Update the leading-comment `last_updated:` line AND the YAML `last_updated:` key with today's UTC date and a one-line note naming this story.
  - [x] 5.4 Update the `Status:` line at the top of THIS story file (`post-epic-4-r4a2-story-4-3-execution-record.md`) from `ready-for-dev` to `review` to match.
  - [x] 5.5 **AC #12 verification:** Confirm `post-epic-4-r4a2b-backpressure-tracker-di-decision: backlog` exists in `sprint-status.yaml` under the `# Post-Epic-4 Retro Cleanup` section. The carve-out was added by the same diff that creates this R4-A2 story (added at story creation time as part of party-mode review patches, 2026-04-30); the dev's job here is **verification**, not creation. If the row is missing, add it with status `backlog` plus a one-line comment `# AC #12 carve-out: wire-or-delete decision for IBackpressureTracker. Promotion trigger: next backpressure-touching code change OR by 2026-05-28.` and flag the omission in the Dev Agent Record.
  - [x] 5.6 Run `git diff _bmad-output/implementation-artifacts/sprint-status.yaml` and confirm: one row flipped (R4-A2 to `review`), one row pre-added (R4-A2b at `backlog` if it wasn't already there at story-creation time), two `last_updated` lines bumped, all other rows unchanged. No other story keys touched.

- [x] **Task 6 — Test-impact null-check (mandatory baseline-equality verification per AC #11)** (AC: #8, #11)
  - [x] 6.1 Run `git diff --name-only` on the working branch. Confirm the diff contains exactly: `_bmad-output/implementation-artifacts/post-epic-4-r4a2-story-4-3-execution-record.md`, `_bmad-output/implementation-artifacts/4-3-per-aggregate-backpressure.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml`. No `.cs`, no `.csproj`, no test file, no source XML comment edit. **R4-A8 source-comment cleanup is a separate backlog story — do not absorb it.**
  - [x] 6.2 **Mandatory per AC #11.** Re-run Tier 1 + Tier 2 (per CLAUDE.md command list) and capture the post-story pass counts. Compare to the Task 0.3 baseline. The two MUST be equal — that equality is the gate that satisfies AC #11. Append the comparison (baseline → post-story, equality confirmed) to the reconstructed Story 4.3 Verification Status block per AC #5b; do NOT silently overwrite Task 0.3 numbers if they were captured earlier. If the post-story count differs from baseline, STOP — root-cause is almost certainly NOT this story's diff (which contains zero source/test changes per AC #8) but the divergence still blocks story close until investigated.

## Dev Notes

### Why this story is documentation-only, not code

Per Epic 4 retro §6 (Severity: High row "Story 4.3 lacks final dev record"), the implementation is in main and works as designed; what's missing is the artifact that lets future agents *trust* "Story 4.3 is done" without re-running `git log` and `dotnet test` themselves. Sprint-change-proposal §2 (Proposal 2) explicitly scopes R4-A2 as "documentation reconstruction story" and lists the missing sections — task checklist, dev agent record, file list, change log, and verification summary. That is exactly the surface this story restores.

The "test" of this story is whether a future agent reading `4-3-per-aggregate-backpressure.md` can answer "what was implemented, where, and how was it verified?" without leaving the file. The validation is observational — caught by the next agent reading the reconstructed file, not by `dotnet test`.

### Why the AC #5d DI-gap caveat is recorded but NOT fixed (and why R4-A2b is paired)

`IBackpressureTracker` is a defense-in-depth tracker that ships with full unit tests (T3 in the inventory) but is not registered in production DI as of HEAD `8028cf2` (verified by Task 0.5 grep). The active enforcement is the actor-level Step 2b check inside `AggregateActor` (Inventory item #6), which is wired and operational regardless of which `SubmitCommandHandler` constructor the container picks. R4-A2 is documentation-only by AC #8 — wiring or deleting `IBackpressureTracker` is a code change with non-zero behavior surface (constructor selection switches; CAS contention enters the per-command hot path) that belongs in a separate story scoped for code, not documentation. AC #5d records the gap as a caveat; the binary wire-or-delete decision is made elsewhere.

**Why a paired sibling (R4-A2b) instead of a free-floating caveat.** Free-floating caveats are the exact failure mode the Epic 3 retrospective surfaced — R2-A8 sat in `backlog` while Epic 3 closed because no per-story gate enforced follow-through. The pattern precedent is `post-epic-2-r2a5` (symptom fix) → `post-epic-2-r2a5b` (structural cure), both filed in the same diff so the structural follow-up cannot be forgotten. R4-A2 → R4-A2b mirrors that shape: AC #12 makes R4-A2b's row creation mandatory in the same diff as R4-A2, with explicit Trigger A (next backpressure-touching code change) / Trigger B (4-week SLA) promotion rules and a hard caveat-update obligation when R4-A2b ships.

### Why there are two `BackpressureExceededException` classes (and neither is dead code)

Inventory items #3 and #4 list two separately-namespaced exceptions: `Hexalith.EventStore.Server.Actors.BackpressureExceededException` and `Hexalith.EventStore.Server.Commands.BackpressureExceededException`. **Both are live; neither is dead code.** A future agent doing a "dedupe similar types" cleanup pass MUST NOT delete either without understanding the role split:

- **Actor-side variant (`Server/Actors/BackpressureExceededException.cs`).** Carries `CorrelationId`, `TenantId`, `Domain`, `AggregateId`, `PendingCount`, `Threshold`. Thrown by — wait, it's actually NOT thrown by the actor; the actor returns `CommandProcessingResult(BackpressureExceeded: true, ...)` instead. The actor-side class exists as the **target shape** that `SubmitCommandHandler` constructs from the actor result and throws into the MediatR pipeline (see `SubmitCommandHandler.cs:157-164`). Naming it under `Server.Actors` aligns with the `BackpressureCheck` activity name (`EventStore.Actor.BackpressureCheck`) and the actor-result fields.
- **Pipeline-side variant (`Server/Commands/BackpressureExceededException.cs`).** Pipeline-thrown variant carrying `AggregateActorId`, `TenantId?`, `CorrelationId`, `CurrentDepth`. Per its own XML doc (lines 5–8): "Properties are for server-side structured logging only — they must NEVER appear in the client-facing 429 response (UX-DR10, Rule E6). This exception does NOT need serialization attributes since it is never serialized across DAPR boundaries (thrown from `SubmitCommandHandler` inside the MediatR pipeline, not from the actor)."
- **Why both exist.** The two classes evolved across the two implementation commits (`85f55a4` introduced the actor-side, `0748651` introduced the pipeline-side). They occupy different namespaces, carry different field sets, and serve different concerns (actor-result-derived vs pipeline-tracker-derived). A future redesign could collapse them, but that's a deliberate design decision, not a deduplication mechanic.

**Reconstruction guidance for the Story 4.3 file:** the reconstructed Dev Notes should reproduce this duality explanation (or a tightened version of it) so the next agent doing source archeology doesn't repeat the near-miss surfaced during the R4-A2 advanced-elicitation review.

### AC #12 trigger enforcement is prose-only — known limitation

AC #12's promotion triggers (Trigger A: "next backpressure-touching code change"; Trigger B: "by 2026-05-28") are **prose, not mechanical**. There is no CI check, scheduled job, or automated reminder that fires when either condition becomes true. This is the same failure-mode class the Epic 3 retro identified ("Prior retrospectives need a gate, not a memory") — and the same limitation that blocked R3-A8 (the BMAD-method-file gate) from being installed in the first place. Mechanical enforcement is unavailable here for the same reason: it would require edits to `.claude/skills/` or new automation outside `_bmad-output/`, both off-limits or out-of-scope for this story.

**Mitigation in lieu of mechanical enforcement:** (a) Trigger A's recognition burden falls on the dev/SM authoring backpressure-touching changes — a rename like `IBackpressureTracker → IPendingCommandLimiter` could miss the linkage; explicit grep for `BackpressureTracker|backpressure_tracker_di_decision|r4a2b` should be part of any backpressure-area review. (b) Trigger B's calendar SLA depends on Bob/project lead noticing the date — surface 2026-05-28 in the next sprint planning cycle and again 1 week before. (c) When R4-A2b is eventually picked up, log in its own Dev Agent Record which trigger fired (A vs B vs neither — i.e., picked up opportunistically) so the project learns whether prose triggers actually work or whether the next paired-sibling pattern needs a different approach.

**This is not a defect in R4-A2 — it is acknowledgment of the structural limit on the kind of follow-up enforcement available without BMAD-method or CI changes.** Future sibling-carve-out patterns should either accept this limit or carry the structural-cure surface higher (e.g., into CI tooling outside the BMAD method).

### Sprint-status transition ownership (AC #10)

Same rule as `post-epic-3-r3a6` (closed 2026-04-29) and `post-epic-3-r3a7` (closed 2026-04-30):

- `ready-for-dev → review` is the **dev's** responsibility, executed at Task 5.3.
- `review → done` is **`code-review`'s** responsibility (the skill flips it when it signs off). If `code-review` is skipped or does not sign off, the story stops at `review` and is closed by the project lead — **not** by the dev.
- Do **not** flip the story directly to `done` from this story's task list. AC #10 is satisfied at `review`.

### Cross-references inside the post-Epic-4 cleanup family

| Action | Status | Routing |
|---|---|---|
| R4-A1 | Resolved | Applied directly to `4-2-resilient-publication-and-backlog-draining.md` (status reconciled `review` → `done`); no new story. |
| R4-A2 | This story | Reconstructs Story 4.3 execution record. |
| R4-A2b | Backlog (party-mode-review carve-out, 2026-04-30) | New sibling: `post-epic-4-r4a2b-backpressure-tracker-di-decision`. Makes the binary wire-or-delete decision for `IBackpressureTracker` (the AC #5d caveat). Promotion trigger per AC #12: next backpressure-touching code change OR 2026-05-28 (whichever first). |
| R4-A3 | Done | Covered by `post-epic-3-r3a1-replay-ulid-validation` (`done` 2026-04-28). |
| R4-A4 | Done | Covered by `post-epic-3-r3a7-live-command-surface-verification` (`done` 2026-04-30). |
| R4-A5 | Backlog | `post-epic-4-r4a5-tier3-pubsub-delivery`. Owns the live Tier 3 backpressure verification this story records as a caveat. |
| R4-A6 | Backlog | `post-epic-4-r4a6-drain-integrity-guard`. |
| R4-A7 | Routed | Covered by existing `post-epic-1-r1a1-aggregatetype-pipeline` (`done`) + `post-epic-2-r2a2-commandstatus-isterminal-extension` (`done`). |
| R4-A8 | Backlog | `post-epic-4-r4a8-story-numbering-comments`. **Explicitly NOT in scope for R4-A2** — do not absorb source-comment edits into this story's diff (AC #8). |

Source: `sprint-status.yaml` rows under the `# Post-Epic-4 Retro Cleanup` block (anchor by section header, not line number); `epic-4-retro-2026-04-26.md` §8.

### Previous-story intelligence — what makes this story different

- **`post-epic-3-r3a6-tier3-error-contract-update` (`done` 2026-04-29):** template for the sprint-status bookkeeping AC and for the "no `dev-story` flip to done" rule. Pattern reference only — R3-A6 was a code change story; R4-A2 is documentation-only.
- **`post-epic-3-r3a7-live-command-surface-verification` (`done` 2026-04-30):** template for the Verification Status block format and the "explicit unrun-test caveat" discipline. R4-A2 inherits the Verification Status table shape from R3-A7's reconstruction.
- **`post-epic-3-r3a8-retro-follow-through-gate` (`superseded` 2026-04-30):** explicit anti-pattern. R3-A8 was blocked because it required edits to BMAD method files under `.claude/skills/bmad-create-story/` (off-limits per the project rule). **R4-A2 stays inside `_bmad-output/`** — the only artifact edits are this story file, the existing Story 4.3 file, and `sprint-status.yaml`. No `.claude/` or `_bmad/` edits are required or allowed.

### Testing Standards (project-wide rules — apply to every story)

- **Tier 1 (Unit):** xUnit 2.9.3 + Shouldly + NSubstitute. No DAPR runtime, no Docker. **Not applicable to this story** (no code change). Sanity-check Tier 1 baseline equality is captured per Task 0.3 / 6.2 if the dev opts to re-run.
- **Tier 2 / Tier 3 (Integration) — REQUIRED end-state inspection:** If the story creates or modifies Tier 2 (`Server.Tests`) or Tier 3 (`IntegrationTests`) tests, each test MUST inspect state-store end-state. **Not applicable** — this story creates no tests. *Reference for the rule itself:* Epic 2 retro R2-A6.
- **ID validation:** Any controller / validator handling `messageId`, `correlationId`, `aggregateId`, or `causationId` MUST use `Ulid.TryParse` (or accept any non-whitespace string per `AggregateIdentity` rules). `Guid.TryParse` on these fields is forbidden. **Not applicable** — this story touches no controller/validator.

(The Testing Standards block is reproduced verbatim from the live template so a fresh LLM context running `dev-story` does not have to cross-reference. The "Not applicable" annotations are this story's evidence that the dev considered each rule explicitly. The same annotations are then propagated into the reconstructed Story 4.3 Dev Notes per Task 2.2 — but with the actual Tier 1 / Tier 2 applicability flipped to "Applicable" for the Story 4.3 audience, since Story 4.3 ships 38 tests.)

### Library / framework versions

Not applicable — no code change. Markdown edits only. References to existing code use file:line citations against HEAD of the live working tree.

### Project Structure Notes

- All edits land in:
  - `_bmad-output/implementation-artifacts/post-epic-4-r4a2-story-4-3-execution-record.md` (this file — created at story creation time + party-mode-reviewed, status-flipped at Task 5.4)
  - `_bmad-output/implementation-artifacts/4-3-per-aggregate-backpressure.md` (the reconstruction target — additions only after the existing `### Definition of Done` block per AC #7)
  - `_bmad-output/implementation-artifacts/sprint-status.yaml` (R4-A2 row flip per AC #10; R4-A2b sibling carve-out row at `backlog` per AC #12 — added at story creation, verified by dev at Task 5.5; two `last_updated` lines bumped)
- Out of scope, explicitly:
  - Any `src/Hexalith.EventStore*/...` file (AC #8). The Implementation Inventory enumerates production files but only as **read-only references** for reconstruction.
  - Any `tests/Hexalith.EventStore*/...` file (AC #8).
  - The R4-A8 source-comment normalization (separate backlog story).
  - Any DI fix for `IBackpressureTracker` (AC #5d — caveat is recorded, fix is carved out to `post-epic-4-r4a2b-backpressure-tracker-di-decision` per AC #12).
  - `.claude/` and `_bmad/` directories — BMAD method files, off-limits per project rule (see R3-A8 supersedure note in `sprint-status.yaml`). **Read-only access is allowed** — Task 2.2 reads `.claude/skills/bmad-create-story/template.md` to copy the Testing Standards block into `_bmad-output/`, but does NOT modify it.

### References

- [Source: `_bmad-output/implementation-artifacts/epic-4-retro-2026-04-26.md#8-Action-Items`] — R4-A2 row, owner Bob/Dev, priority High, "Done When" criteria
- [Source: `_bmad-output/implementation-artifacts/epic-4-retro-2026-04-26.md#6-Technical-Debt-and-Risks`] — High-severity row "Story 4.3 lacks final dev record"
- [Source: `_bmad-output/implementation-artifacts/epic-4-retro-2026-04-26.md#11-Commitments`] — R4-A2 categorized under "Documentation cleanup needed for future agent reliability"
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md#Proposal-2`] — verbatim list of missing sections (task checklist / dev agent record / file list / change log / verification summary)
- [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml#L297-L306`] — Post-Epic-4 Retro Cleanup block, including the routing comments for R4-A1/A3/A4/A7
- [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml#L80-L85`] — Epic 4 done; Story 4-3 done; epic retrospective done
- [Source: `_bmad-output/implementation-artifacts/4-3-per-aggregate-backpressure.md`] — the reconstruction target. Lines 1–41 (Status, Story, AC #1–#12, DoD) are read-only per AC #7.
- [Source: `_bmad-output/implementation-artifacts/post-epic-3-r3a6-tier3-error-contract-update.md`] — pattern reference for sprint-status bookkeeping AC and `ready-for-dev → review` ownership
- [Source: `_bmad-output/implementation-artifacts/post-epic-3-r3a7-live-command-surface-verification.md`] — pattern reference for the Verification Status block format and explicit unrun-test caveat discipline
- [Source: `_bmad-output/implementation-artifacts/post-epic-3-r3a8-retro-follow-through-gate.md`] — anti-pattern reference: do NOT touch `.claude/skills/bmad-*` or `_bmad/bmm/` files; this story stays inside `_bmad-output/`
- [Source: `_bmad-output/implementation-artifacts/post-epic-2-r2a5b-version-prose-source-of-truth-refactor.md`] — pattern reference for the symptom-fix-then-structural-cure two-stage shipping pattern that R4-A2 → R4-A2b mirrors (sibling story carved out at story-creation time, not future-tense "should be filed")
- [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml`] — `post-epic-4-r4a2b-backpressure-tracker-di-decision: backlog` row added at story creation (party-mode review patches, 2026-04-30); see AC #12 + the "Why a sibling carve-out" Dev Note
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L184-L234`] — Step 2b backpressure check (active enforcement)
- [Source: `src/Hexalith.EventStore.Server/Configuration/BackpressureOptions.cs`] — options record + validator (AC #8 source)
- [Source: `src/Hexalith.EventStore/ErrorHandling/BackpressureExceptionHandler.cs`] — HTTP 429 + RFC 7807 handler (AC #2 source)
- [Source: `tests/Hexalith.EventStore.Server.Tests/Actors/BackpressureTests.cs`] — 13 actor-level tests (AC #1, #3–#7, #9, #10, #11)
- [Source: `tests/Hexalith.EventStore.Server.Tests/Configuration/BackpressureOptionsTests.cs`] — 7 options tests (AC #8)
- [Source: `tests/Hexalith.EventStore.Server.Tests/Commands/InMemoryBackpressureTrackerTests.cs`] — 11 tracker tests (defense-in-depth)
- [Source: `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/BackpressureExceptionHandlerTests.cs`] — 7 handler tests (AC #2, #11)
- [Source: `git log 85f55a4`] — original Story 4.3 commit; co-author trailer for Dev Agent Record
- [Source: `git log 0748651`] — pipeline-side hardening commit; second Change Log row
- [Source: `git log c870241`] — PR #107 merge SHA
- [Source: `CLAUDE.md#Code-Review-Process`] — mandatory code-review pipeline stage; explains why `review → done` is owned by `code-review`, not by `dev-story`
- [Source: `.claude/skills/bmad-create-story/template.md#L30-L34`] — the verbatim Testing Standards block to reproduce in the reconstructed Story 4.3 Dev Notes

## Dev Agent Record

### Agent Model Used

Claude Opus 4.7 (1M context)

### Debug Log References

- HEAD SHA pinned at execution: `8028cf2` (matches the spec's Implementation Inventory pin).
- Build: 0 errors, 0 warnings (Release configuration, `-p:NuGetAudit=false` per repo convention).
- Tier 1 baseline (Task 0.3) = post-story (Task 6.2): Contracts 281 + Client 334 + Sample 63 + Testing 78 + SignalR 32 = **788/788** (equality confirmed).
- Tier 2 baseline (Task 0.3) = post-story (Task 6.2): 1620 pass / 25 fail / 1645 total. The 25 failures are infrastructure-class only (`DaprTestContainerFixture` pre-flight check fails because Redis/DAPR placement/scheduler are unreachable when Docker is not running on the dev host) — same Docker-absence shape as the `post-epic-3-r3a6` close note. Zero regression.
- Backpressure-specific runner count (Task 0.2) = post-story: **38/38** (BackpressureTests 13 + BackpressureOptionsTests 7 + InMemoryBackpressureTrackerTests 11 + BackpressureExceptionHandlerTests 7) — runner equals method-name grep, no `[Theory]` row expansion.
- IBackpressureTracker registration grep (Task 0.5): `grep -rE '\b(Try)?Add(Singleton|Scoped|Transient)\b.*\bIBackpressureTracker\b' src/` → 0 matches; `grep -rE 'services\.Add<.*IBackpressureTracker.*>' src/` → 0 matches. AC #5d caveat stands; R4-A2b carve-out is the right routing.
- Sprint-status starting state for `post-epic-4-r4a2-story-4-3-execution-record` (Task 0.4): `ready-for-dev` (verified at line 303 of `sprint-status.yaml` pre-edit).
- AC #6 mechanical check: Story 4.3 `Status:` line stays `done` (verified post-edit at line 3 of `4-3-per-aggregate-backpressure.md`).
- AC #7 mechanical check: `git diff` shows `@@ -39,3 +39,247 @@` — only additions starting after the existing `### Definition of Done` block. Status header / Story statement / AC #1–#12 list / DoD block are byte-identical to HEAD.
- AC #8 mechanical check: `git status --short` shows exactly 3 markdown/yaml files (`4-3-per-aggregate-backpressure.md`, `sprint-status.yaml`, `post-epic-4-r4a2-story-4-3-execution-record.md`); zero `.cs` / `.csproj` / test / source XML comment edits.
- AC #12 verification (Task 5.5): `post-epic-4-r4a2b-backpressure-tracker-di-decision: backlog` is present in `sprint-status.yaml` under the `# Post-Epic-4 Retro Cleanup` section — the carve-out was added at story-creation time (party-mode review patches, 2026-04-30); the dev's job here was verification, not creation. Verification passed.
- Out-of-scope observation (recorded in Story 4.3 Completion Notes, NOT fixed in this story): `BackpressureExceptionHandler` is registered twice in `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` (lines 38 and 43). Pre-existing condition; first registration wins per ASP.NET Core handler-chain semantics; second is dead code. Not in scope for AC #8 (no source code changes); a future code-cleanup pass should consolidate.
- Citation-drift observations (recorded in Story 4.3 Completion Notes): `ErrorReferenceEndpoints.cs` backpressure entry actually spans lines 85–88 (the spec inventory cited 85–87 — Implementation Inventory header explicitly authorized recording the actual span when drift is observed).

### Completion Notes List

- ✅ Task 0 — Pre-flight verification of the Implementation Inventory: HEAD SHA pinned `8028cf2`; spot-checked production-file citations all resolve to the cited code (BackpressureOptions, ServiceCollectionExtensions, AggregateActor Step 2b at 184-234, ReadPendingCommandCountAsync at 806, finally decrement at 544-561, drain decrement at 702-708, terminal decrement at 1018, CommandProcessingResult fields at 23-25, SubmitCommandHandler constructors at 32-47 and throw site at 157-164, EventStoreActivitySource.BackpressureCheck at line 42, BackpressureExceptionHandler retry-after at 44+62 and inner-walk at 69-95, ProblemTypeUris.BackpressureExceeded at line 17, ErrorReferenceEndpoints actual span 85-88 vs cited 85-87 — recorded). Test method-name grep matches inventory: T1=13, T2=7, T3=11, T4=7. AC #5a runner=grep (no `[Theory]` divergence). Tier 1 baseline = 788/788. Tier 2 baseline = 1620/25/1645 (Docker absent). IBackpressureTracker DI grep = 0 matches in `src/`.
- ✅ Task 1 — Reconstructed `## Tasks / Subtasks` in Story 4.3: 9 numbered tasks, 36 subtasks, every Story 4.3 AC #1–#12 cited at least once. All boxes `[x]` because PR #107 already shipped the implementation.
- ✅ Task 2 — Reconstructed `## Dev Notes` in Story 4.3: Testing Standards block reproduced verbatim from `.claude/skills/bmad-create-story/template.md` lines 30–34 (read-only access; pinned to HEAD `8028cf2`); per-rule applicability annotations added; "Why two `BackpressureExceededException` classes" Dev Note included to prevent future dead-code deletion; sibling cross-references table covers all 8 R4-A* + R4-A2b carve-out; Project Structure Notes lists 14 production + 1 doc + 4 test files; References block cites every AC #1–#12.
- ✅ Task 3 — Reconstructed `## Dev Agent Record` in Story 4.3: Agent Model Used line records both the original-implementation model (Claude Opus 4.6 (1M context) per the `85f55a4` Co-Authored-By trailer) and the reconstruction model (Claude Opus 4.7 (1M context)); Completion Notes List captures the verification outcome with explicit AC #5c and AC #5d caveats; File List enumerates all 14 production + 1 doc + 4 test = 19 paths grouped by repo area.
- ✅ Task 4 — Reconstructed `## Change Log` and `## Verification Status` in Story 4.3: Change Log has 3 rows (PR #107 initial, pipeline hardening 0748651, this 1.2 R4-A2 reconstruction with resolved date 2026-04-30 and Author "Quentin Dassi Vignon (dev) + Claude Opus 4.7 (1M context)"); Verification Status table records Tier 1 (281+334+63+78+32=788), Tier 2 full (1620/25/1645), Tier 2 backpressure-filter (38/38), Tier 3 N/A; both AC #5c (Tier 3 deferral to `post-epic-4-r4a5-tier3-pubsub-delivery`) and AC #5d (`IBackpressureTracker` DI gap, deferral to `post-epic-4-r4a2b-backpressure-tracker-di-decision`) recorded as separate `**Caveat (...)**` paragraphs immediately below the table per the spec's Task 4.4 formatting requirement.
- ✅ Task 5 — Sprint-status bookkeeping, self-status flip, sibling carve-out verification: AC #6 mechanical check passed (Story 4.3 `Status: done` unchanged); AC #7 mechanical check passed (`git diff @@ -39,3 +39,247 @@` confirms additions only after DoD block); `sprint-status.yaml` flipped `post-epic-4-r4a2-story-4-3-execution-record: ready-for-dev → review`; both `last_updated` lines (leading comment + YAML key) bumped with concise execution note; THIS story's `Status:` line flipped to `review`; AC #12 verification confirmed `post-epic-4-r4a2b-backpressure-tracker-di-decision: backlog` row present (added at story-creation time per party-mode review patches, 2026-04-30); no other story keys touched.
- ✅ Task 6 — Test-impact null-check (mandatory baseline-equality verification per AC #11): `git status --short` shows exactly 3 modified/added markdown+yaml files (no `.cs`, no `.csproj`, no test, no source XML comment); R4-A8 source-comment cleanup not absorbed. Post-story re-run = baseline: Tier 1 788/788; Tier 2 1620/25/1645; backpressure-filter 38/38. Equality confirmed empirically — AC #11 closed.
- **Caveat (AC #5c)** recorded in Story 4.3 Verification Status: live Aspire / DAPR Tier 3 backpressure path NOT exercised by Story 4.3; deferred to `post-epic-4-r4a5-tier3-pubsub-delivery`.
- **Caveat (AC #5d)** recorded in Story 4.3 Verification Status: defense-in-depth pipeline-layer `IBackpressureTracker` is shipped + tested but not registered in production DI as of HEAD `8028cf2`; wire-or-delete decision carved out to sibling `post-epic-4-r4a2b-backpressure-tracker-di-decision` (status `backlog`, promotion triggers per AC #12). The actor-level Step 2b check is the active enforcement and is wired and operational regardless of which `SubmitCommandHandler` constructor the container picks.
- **Out-of-scope observation** (recorded only — AC #8 forbids fixing it here): `BackpressureExceptionHandler` is registered twice in `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs:38, 43`; first registration wins per ASP.NET Core semantics; second is dead code. Worth a follow-up code-cleanup story.

### File List

- `_bmad-output/implementation-artifacts/4-3-per-aggregate-backpressure.md` (additions only; lines 1–41 byte-identical to HEAD per AC #7)
- `_bmad-output/implementation-artifacts/post-epic-4-r4a2-story-4-3-execution-record.md` (this file — Status flipped `ready-for-dev → review`; Tasks/Subtasks all checked; Dev Agent Record populated)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (R4-A2 row flipped `ready-for-dev → review`; both `last_updated` lines bumped; R4-A2b carve-out row at `backlog` verified to be present from story-creation time)
