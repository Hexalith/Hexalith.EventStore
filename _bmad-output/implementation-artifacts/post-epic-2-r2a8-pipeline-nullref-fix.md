# Story Post-Epic-2 R2-A8: Root-Cause and Fix `SubmitCommandHandler` NullReferenceException in 4 Tier 2 Tests

Status: done (closed-as-superseded with structural pin + code-review-driven test hardening — see Dev Notes → Reproduction Discovery + Resolution + Review Findings)

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform maintainer,
I want the 4 stable `SubmitCommandHandler.Handle` `NullReferenceException` failures in `Hexalith.EventStore.Server.Tests` root-caused and fixed,
so that Epic 3 Story 3.1 (Command Submission Endpoint) and every subsequent Epic 3 story can extend the same pipeline without inheriting a 4-test red-baseline that has been carried unchanged for the full Epic 2 run (every Epic 2 story logged the same 15-failure pre-existing count, of which exactly these 4 are Pipeline-scope).

## Acceptance Criteria

1. **Pre-flight reproduction is captured before code changes.** Before editing any production source, run `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~PayloadProtectionTests|FullyQualifiedName~CausationIdLoggingTests|FullyQualifiedName~LogLevelConventionTests|FullyQualifiedName~StructuredLoggingCompletenessTests" -p:NuGetAudit=false` (after `dapr init`). Capture the exact `NullReferenceException` stack trace from at least one of: `PayloadProtectionTests.SubmitCommandHandler_NeverLogsPayloadData`, `CausationIdLoggingTests.SubmitCommandHandler_IncludesCausationId`, `LogLevelConventionTests.CommandReceived_UsesInformationLevel`, `StructuredLoggingCompletenessTests.CommandReceived_LogContainsAllRequiredFieldsAsync`. Stack trace + line number + identified null object recorded in story dev notes (Debug Log References section). **The work cannot proceed until this evidence exists** — it is the input that all later acceptance criteria depend on. If the tests pass on first run despite the retro reporting they fail, the story must HALT and re-open with the discovery (the failures may have been incidentally cleared by Epic 1 retro carry-over merges; the proposal's reproduction premise would need to be re-validated).

2. **Root cause documented in story dev notes.** One paragraph in `## Dev Notes → Root Cause Analysis` that names the specific code path inside `SubmitCommandHandler.Handle` (or a directly-invoked dependency) that dereferenced a null reference, the test-fixture wiring decision that exposed it, and why the production runtime path doesn't hit it. The paragraph must reference:
   - The exact file + line (against current `4619f75` HEAD or later) of the NRE.
   - Which constructor overload of `SubmitCommandHandler` the failing tests use (5-arg `(statusStore, archiveStore, commandRouter, backpressureTracker, logger)` per `tests/Hexalith.EventStore.Server.Tests/Logging/*Tests.cs`).
   - Why the parallel test `SubmitCommandHandlerTests.Handle_ValidCommand_ReturnsCorrelationId` (`tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs:30`) — which uses the *same* 5-arg constructor — passes today (it injects real `InMemoryCommandStatusStore` + `InMemoryCommandArchiveStore` instead of `NSubstitute.For<>` mocks). The differential between the two test shapes is the most likely root-cause signal.

3. **Production fix lands at the root cause, not at the symptom.** The fix MUST be one of:
   - **(a) Null guard at the surfaced site in `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`** — only if the null source is *actually* nullable on the production code path (i.e., the field is declared as `?` or the API contract permits null). If the field is non-nullable in production wiring, a defensive null guard hides the test-fixture bug rather than fixing it; reject this option in that case.
   - **(b) Test-fixture wiring fix** in the 4 affected test classes (e.g., configure the NSubstitute mock to return the right shape, replace `Substitute.For<>` with the same in-memory fakes the passing test uses, or supply a missing fixture parameter). Acceptable only if the production code is correct as written.
   - **(c) DI registration fix** in `src/Hexalith.EventStore.Server/DependencyInjection/*` if the missing dependency is genuinely supposed to be registered as non-null at runtime but the constructor overload accepts a nullable.

   The chosen path is justified in dev notes against the rejected alternatives. **Whichever is chosen, the rejected paths must be named and rejected explicitly** — silently picking one of three with no rationale is exactly the kind of bookkeeping drift the Epic 2 retro flagged (§3 Struggle 8).

4. **The 4 named tests pass under the standard invocation.** After the fix, `dotnet test tests/Hexalith.EventStore.Server.Tests/ -p:NuGetAudit=false` (post-`dapr init`) shows all 4 of these previously-failing tests as **Passed**:
   - `Hexalith.EventStore.Server.Tests.Logging.PayloadProtectionTests.SubmitCommandHandler_NeverLogsPayloadData`
   - `Hexalith.EventStore.Server.Tests.Logging.CausationIdLoggingTests.SubmitCommandHandler_IncludesCausationId`
   - `Hexalith.EventStore.Server.Tests.Logging.LogLevelConventionTests.CommandReceived_UsesInformationLevel`
   - `Hexalith.EventStore.Server.Tests.Logging.StructuredLoggingCompletenessTests.CommandReceived_LogContainsAllRequiredFieldsAsync`

5. **Pre-existing-failure count drops by exactly 4.** Tier 2 (`Server.Tests`) total failure count observed before the fix MUST decrease by exactly 4 after the fix. Per Epic 2 retro §3 Struggle 4, the documented pre-existing failures are: 4 `SubmitCommandHandler.Handle` NullRefs (this story's scope), 1 validator extension-size test (out-of-scope), 10 auth integration tests (Epic 5 scope, out-of-scope). If the failure count drops by *more* than 4, investigate — a side-effect change leaked outside scope. If it drops by fewer than 4, at least one of the four named tests still has a residual failure mode (re-open ACs 4 + 5 until all four are green simultaneously).

6. **New regression test pins the exact null path identified in AC #2.** Add **one** Tier 1-shaped test method in `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs` (the existing class, not a new file) named after the null-path signal (e.g., `Handle_ValidCommand_DoesNotThrowNullReference_WhenStatusStoreIsMocked` or `Handle_ValidCommand_DoesNotThrowNullReference_WhenActivityTrackersAreNull` — pick the wording that matches the actual root cause). The test:
   - Reproduces the exact constructor + mock-arrangement shape that the 4 failing tests use (5-arg ctor + NSubstitute for `ICommandStatusStore` / `ICommandArchiveStore` / `ICommandRouter` / `IBackpressureTracker`).
   - Asserts that `await handler.Handle(...)` does NOT throw `NullReferenceException` and returns a `SubmitCommandResult` with the expected `CorrelationId`.
   - Carries an XML-doc summary that names R2-A8, links to this story file, and explains why the existing `Handle_ValidCommand_ReturnsCorrelationId` test (which uses real fakes) does not catch this regression class.

   **Why a dedicated regression test on top of re-enabling the 4 originals:** the 4 originals are *logging* tests that happened to crash. Their primary contract (log-payload-redaction, log-causation-id-presence, log-level-convention, log-completeness) is orthogonal to the null-path being fixed. If any one of them later gets disabled or rewritten for an unrelated reason, the null-path regression must still be guarded — the dedicated regression test is the load-bearing pin. Per Epic 2 retro Lesson 1 ("'It compiles and tests pass' ≠ 'it works'"): the existing tests have been failing silently in CI's "out-of-scope" bucket for a full epic; relying on them as the regression guard for next time is the same shape that produced this story.

7. **Tier 1 ratchet holds at 656 (or higher).** All Tier 1 windows green, no count regression:
   - `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` → 271+/271 pass
   - `dotnet test tests/Hexalith.EventStore.Client.Tests/ -p:NuGetAudit=false` → 334+/334 pass
   - `dotnet test tests/Hexalith.EventStore.Sample.Tests/ -p:NuGetAudit=false` → 63+/63 pass
   - `dotnet test tests/Hexalith.EventStore.Testing.Tests/ -p:NuGetAudit=false` → 78+/78 pass (post-R1-A2 + R1-A7 baseline; verify against `4619f75` HEAD before merge)
   - `dotnet test tests/Hexalith.EventStore.SignalR.Tests/` → baseline preserved (verify count against `4619f75` HEAD)

   The new regression test in `Pipeline/SubmitCommandHandlerTests.cs` lives in `Server.Tests` (Tier 2 project) and counts toward Tier 2, not Tier 1 — but it is a Tier 1-*shaped* test (no `[Collection("DaprTestContainer")]`, NSubstitute mocks only) so it does not pay the live-fixture startup cost.

8. **Full Release build remains 0 warnings, 0 errors.** `dotnet build Hexalith.EventStore.slnx --configuration Release -p:NuGetAudit=false` → 0/0 with `TreatWarningsAsErrors=true`. Pre-existing `NU1902` OpenTelemetry transitive CVE warnings continue to be bypassed via `-p:NuGetAudit=false` (same workaround as R1-A1, R1-A2, R1-A6, R1-A7).

9. **Epic 2 retro action item R2-A8 marked complete.** Update the action items table in `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` §6 row R2-A8 with the merge commit reference (or PR number) using the exact format precedent set by the post-epic-1 cluster: e.g., `✅ Done <commit-sha> — 4 Pipeline NullRef tests now green; pre-existing Server.Tests failure count dropped 15 → 11`. The §10 Commitments section's "Critical path before Epic 3 starts" line referencing R2-A8 is updated to confirm closure (do NOT delete the line — preserve the audit trail; append "✅" or move the row to a "Closed" sub-bullet, mirroring the Epic 1 retro convention).

10. **Conventional-commit hygiene on the merge.** The merge commit (or squashed PR title) uses the `fix(server):` prefix per `CLAUDE.md` Conventional Commits guidance, since this is a defect fix in `Hexalith.EventStore.Server`. Patch-level semver bump is correct (no contract changes; no breaking changes).

## Tasks / Subtasks

- [x] Task 1: Reproduce the failures end-to-end and capture the NRE evidence (AC: #1, #2)
  - [x] 1.1 Run `dapr init` (full DAPR + Docker) per `CLAUDE.md` Tier 2 prerequisites — already initialized (CLI 1.15.2 / Runtime 1.17.5; `dapr_redis`, `dapr_placement`, `dapr_scheduler`, `dapr_zipkin` healthy)
  - [x] 1.2 Run the failure-set filter: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~PayloadProtectionTests|FullyQualifiedName~CausationIdLoggingTests|FullyQualifiedName~LogLevelConventionTests|FullyQualifiedName~StructuredLoggingCompletenessTests" -p:NuGetAudit=false --logger "console;verbosity=detailed"`
  - [x] 1.3 **HALT triggered** — all 4 named NRE tests reported as **Passed**, not **Failed**. The story's premise has been invalidated. Recorded the discovery in Dev Notes → Reproduction Discovery and Dev Agent Record → Debug Log References. Project Lead consultation needed before proceeding (rest of the story does not apply unless premise is re-validated)
  - [~] 1.4 Not applicable — no NRE stack trace exists; no NRE produced on current HEAD `f803e9a`
  - [x] 1.5 Full Tier 2 sweep recorded: `1642 / 1642 passed, 0 failed`. The retro's expected baseline of 15 pre-existing failures (4 Pipeline + 1 validator + 10 auth-integration) is no longer present at current HEAD. AC #5's delta-of-4 measurement is moot when the starting baseline is 0 failures

- [~] Task 2: Identify the root cause through differential analysis (AC: #2, #3) — **N/A per HALT (Project Lead chose Option 1: close-as-superseded)**. No reproducing failure exists at HEAD `f803e9a`; differential analysis would be speculative (Epic 2 retro Theme 4). The structural-pin test (Task 4) makes the all-mocks-no-trackers contract load-bearing for Epic 3 Story 3.1 without requiring a root-cause attribution for the historical clearing.
  - [ ] 2.1 Compare the failing test fixture vs the passing fixture: `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs:30-52` (`Handle_ValidCommand_ReturnsCorrelationId` — uses *real* `InMemoryCommandStatusStore` + `InMemoryCommandArchiveStore`) vs the 4 failing tests (use `NSubstitute.For<ICommandStatusStore>()` etc.). Both use the same 5-arg constructor. The difference is the only variable
  - [ ] 2.2 Walk the `Handle` method (`src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:49-185`) call by call and ask: "for each `await foo.BarAsync(...)`, what does an unconfigured NSubstitute return?" The contract is: `Task` (non-generic) → `Task.CompletedTask`; `Task<T>` → `Task.FromResult(default(T))`. **For nullable reference types, `default(T?)` is `null`** — a returned `Task<CommandStatusRecord?>` returns `Task.FromResult((CommandStatusRecord?)null)`, which awaits to `null`. Verify whether any code path then dereferences that null (note: the `finalStatus?.…` chains use null-conditional, so they are safe — but the search must cover *every* `await` site, including async chains the source generator emits)
  - [ ] 2.3 Inspect any LoggerMessage source-generator-emitted partial methods (`Log.CommandReceived` etc., declared at `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:187-260`). The generator-emitted code lives under `obj/` after a build; if the NRE stack trace points into a `.g.cs` file, the issue is at the generator's expanded site (e.g., a logger field accessed before construction completes — unlikely with primary-constructor params but worth eliminating)
  - [ ] 2.4 Inspect `SubmitCommand.ToArchivedCommand()` (`src/Hexalith.EventStore.Server/Commands/ArchivedCommandExtensions.cs:14`). The extension reads `command.Tenant`, `command.Domain`, `command.AggregateId`, `command.CommandType`, `command.Payload`, `command.Extensions`, all of which are populated in every failing test's `SubmitCommand` literal. Eliminate this site
  - [ ] 2.5 Inspect any Activity/ActivitySource-related state. The 4 failing test classes each register an `ActivityListener` in their constructor (e.g., `LogLevelConventionTests.cs:41-46`). Cross-check against `SubmitCommandHandler` and any of its dependencies for an `Activity.Current`-style dereference; this is the most plausible "subtle null in the test fixture only" candidate
  - [ ] 2.6 Confirm the root-cause hypothesis by running ONLY the suspect fixture-arrangement against a debugger / `--logger:trxlogger` and observing whether the NRE moves with the change. **Do not "fix" before this confirmation** — the retro flagged speculative-fix as a recurring failure mode (Epic 2 retro Theme 4)
  - [ ] 2.7 Write up Dev Notes → Root Cause Analysis paragraph per AC #2

- [~] Task 3: Apply the chosen fix (AC: #3, #4, #8) — **N/A per HALT (Option 1)**. Production code (`src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`) was NOT modified — `git log` since retro HEAD `4619f75` shows zero commits on this file; the file is structurally identical to its retro-time state, yet the failures no longer reproduce. Test-fixture wiring was NOT modified — the 4 Logging tests' all-mocks-no-trackers shape works as-is on current HEAD. DI registration was NOT modified. AC #8's "Release build 0/0" gate was still validated post-pin-test — see Dev Agent Record → Validation Results.
  - [ ] 3.1 Pick the option (a / b / c per AC #3) that fits the root cause. **The DI-registration option (c)** is preferred when the missing dependency is documented to be registered non-null at runtime — it makes test fixtures self-correct without weakening production. **Test-fixture option (b)** is preferred when the production code is correct and the test's NSubstitute setup was incomplete. **Production null-guard option (a)** is preferred only when the field is genuinely nullable and a missing-but-not-required dependency triggers the dereference
  - [ ] 3.2 Apply the edit. Match the file's existing style: file-scoped namespaces, `_camelCase` private fields, Allman braces, `Async` suffix on async methods, file-level XML docs (per `.editorconfig` and `CLAUDE.md` § Code Style)
  - [ ] 3.3 If the fix lives in `SubmitCommandHandler.cs` (option a), preserve the existing partial-class shape and the `LoggerMessage`-based source-generator pattern. Do NOT switch to plain `_logger.LogInformation(...)` — that breaks the structured-logging contract the 4 tests assert (`StructuredLoggingCompletenessTests` specifically checks `Stage=CommandReceived` token presence)
  - [ ] 3.4 If the fix lives in test-fixture wiring (option b), edit ALL 4 test classes consistently. The 4 classes share an identical handler-construction snippet — extract to a helper if the same edit is repeated 4x to avoid further drift
  - [ ] 3.5 If the fix lives in DI registration (option c), update the corresponding `AddEventStoreServer(...)` extension or equivalent registration in `src/Hexalith.EventStore.Server/`. Do NOT register a fake or mock implementation of the missing dependency at runtime — register the production type. If the production type doesn't exist, that's the real defect, and the fix scope expands (re-evaluate before proceeding)
  - [ ] 3.6 Build clean: `dotnet build Hexalith.EventStore.slnx --configuration Release -p:NuGetAudit=false` → 0 warnings, 0 errors

- [x] Task 4: Add the dedicated regression test (AC: #6) — **reframed per Option 1 as a forward-looking structural pin for Epic 3 Story 3.1, NOT as a regression test for a non-reproducing bug** (Epic 2 retro Theme 4 forbids the latter framing without a reproducing failure)
  - [x] 4.1 Added ONE new `[Fact]` method `Handle_ValidCommand_DoesNotThrowNullReferenceWhenStoresAreNSubstituteMocks` to the existing `SubmitCommandHandlerTests` class in `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs`
  - [x] 4.2 Method name encodes the null-path signal (`...DoesNotThrowNullReferenceWhenStoresAreNSubstituteMocks`)
  - [x] 4.3 Reproduces the exact mock arrangement: `Substitute.For<ICommandStatusStore>()` + `Substitute.For<ICommandArchiveStore>()` + configured `ICommandRouter` returning `CommandProcessingResult(Accepted: true, CorrelationId: "corr-pin-1")` + `IBackpressureTracker` substitute, 5-arg constructor
  - [x] 4.4 Uses Shouldly. Direct `await handler.Handle(...)` (a NullReferenceException would bubble up and fail the test naturally — the cleaner pattern; `Should.NotThrowAsync` discards the `Task<T>` result and would force a separate call to retrieve the result, so it's avoided in favor of the natural-bubble pattern, with an inline comment explaining the choice). Asserts `result.CorrelationId.ShouldBe("corr-pin-1")`
  - [x] 4.5 XML doc `<summary>` block in place — references R2-A8 closure-as-superseded, the retro file path, the 4 historically-failing tests by FQN, names the orthogonal-contracts argument (the 4 logging tests cannot stand in as the load-bearing guard for the null-path class), and frames the test as Epic 3 Story 3.1 prep rather than a regression for a non-reproducing bug
  - [x] 4.6 Run in isolation: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~SubmitCommandHandlerTests"` → 2/2 Réussi (existing `Handle_ValidCommand_ReturnsCorrelationId` + new pin test)

- [x] Task 5: Validate the change set end-to-end (AC: #4, #5, #7, #8) — see Dev Agent Record → Validation Results for full evidence
  - [ ] 5.1 Run the targeted re-enable filter: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~PayloadProtectionTests|FullyQualifiedName~CausationIdLoggingTests|FullyQualifiedName~LogLevelConventionTests|FullyQualifiedName~StructuredLoggingCompletenessTests" -p:NuGetAudit=false`. Confirm the 4 named tests pass
  - [ ] 5.2 Run the FULL Tier 2 sweep: `dotnet test tests/Hexalith.EventStore.Server.Tests/ -p:NuGetAudit=false`. Confirm the failure count dropped by exactly 4 vs Task 1.5's baseline. If it dropped by more, investigate side-effect leakage. If by fewer, return to Task 2 (root cause is not yet fully characterized)
  - [ ] 5.3 Run the Tier 1 windows in parallel (per `CLAUDE.md` build commands):
        - `dotnet test tests/Hexalith.EventStore.Contracts.Tests/`
        - `dotnet test tests/Hexalith.EventStore.Client.Tests/ -p:NuGetAudit=false`
        - `dotnet test tests/Hexalith.EventStore.Sample.Tests/ -p:NuGetAudit=false`
        - `dotnet test tests/Hexalith.EventStore.Testing.Tests/ -p:NuGetAudit=false`
        - `dotnet test tests/Hexalith.EventStore.SignalR.Tests/`
        Confirm each baseline is preserved or grows (regression test added in Task 4 lives in Server.Tests, so Tier 1 counts are unaffected)
  - [ ] 5.4 Full Release build clean: `dotnet build Hexalith.EventStore.slnx --configuration Release -p:NuGetAudit=false` → 0/0
  - [ ] 5.5 Tier 3 (`tests/Hexalith.EventStore.IntegrationTests/`) is NOT in scope. The fix is at the in-process handler boundary and does not touch the Aspire end-to-end contract. Per `CLAUDE.md`, Tier 3 runs are optional in CI; do not gate this story on Tier 3

- [x] Task 6: Close the loop on Epic 2 retro action items (AC: #9) — closure-as-superseded annotation, not a "✅ Done <sha>" closure (because no fix was applied; the failures cleared transitively before the dev session)
  - [x] 6.1 Edited `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` §6 R2-A8 row: appended `✅ Closed-as-superseded 2026-04-27` annotation with the baseline-15-→-0 evidence, the most-likely transitive culprit (`2070c68 refactor(server)!: thread aggregate type through persistence pipeline`), the HALT-then-Option-1 decision trace, and the pin-test FQN. Original action-item description preserved as-written
  - [x] 6.2 Edited §10 Commitments: appended `[R2-A8 ✅ Closed-as-superseded 2026-04-27 — see §6 row]` after the R2-A8 token in the "Critical path before Epic 3 starts" line. R2-A1 and R2-A7 stay open
  - [x] 6.3 No edit needed to `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` — preserved as-written

- [x] Task 7: Sprint-status sync (AC: triggered by close, not part of dev work — handled by the bmad workflow)
  - [x] 7.1 `_bmad-output/implementation-artifacts/sprint-status.yaml` development_status entry `post-epic-2-r2a8-pipeline-nullref-fix`: `ready-for-dev` → `in-progress` (work started) → `in-progress` (HALT; awaiting decision) → `review` (Option 1 chosen; pin test added; retro updated). `last_updated` annotation reflects the closure summary

## Dev Notes

### Scope Summary

This is a defect-fix story scoped to a single hotspot: the `Handle` method of `Hexalith.EventStore.Server.Pipeline.SubmitCommandHandler` (or one of its directly-invoked dependencies) is throwing `NullReferenceException` in 4 specific Tier 2 tests, and the failure has been carried unchanged across the entire Epic 2 run. The fix is local; no contract change; no public-API surface change. The only public-API surface touched (if at all) is the addition of one regression test method in `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs`.

This story does NOT:
- Change command-dispatch semantics, persistence, replay, or pub/sub behavior
- Touch the actor pipeline (`AggregateActor`, `CommandRouter`)
- Change the 5-arg or 6-arg constructor signatures of `SubmitCommandHandler` (those are public-ish surface; refactoring them is breaking)
- Resolve any of the *other* 11 pre-existing Server.Tests failures (1 validator extension-size + 10 auth integration). Those are explicitly Epic 5 scope per Epic 2 retro §3 Struggle 4 and stay deferred

### Why This Story Exists

Story 2.1's senior review documented 15 pre-existing Server.Tests failures, including 4 specifically attributed to `SubmitCommandHandler.Handle` `NullReferenceException` "at line 81" (the file has since grown; the line has shifted). Stories 2.2, 2.3, 2.4, 2.5 each logged the same 15-failure count. The 4 Pipeline NREs were never investigated and never fixed inside Epic 2 — they sat in CI's "out-of-scope" bucket for the full epic.

Epic 2 retro action item R2-A8 (`epic-2-retro-2026-04-26.md` §6) elevated this to High priority because:
- **Story 3.1 (Command Submission Endpoint) lives in this exact handler.** Epic 3 will exercise the same code path heavily — adding a 202 response, a `Location` header, and a `Retry-After: 1` header on top of `Handle`'s output. Carrying 4 unrelated NRE failures into Epic 3 means every Epic 3 story will inherit a confused failure baseline.
- **Failure stability is itself a signal.** 4 tests, never moving, across 5 stories' rerun cycles, is "stable" in the same sense that a load-bearing crack is stable — it isn't getting worse, but it isn't getting better either, and the next workload that touches the area is the one that breaks it.
- **The cost is lowest *now*.** Epic 2's Server.Tests fixture knowledge is freshest; reviewers and dev have just walked the SubmitCommand flow during 2.1/2.2/2.4/2.5 work; investigating the NRE root cause without that recency penalty would be more expensive in a month.

The sprint-change-proposal (`_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` §4 Proposal 2) makes this story **critical-before-Epic-3-starts**.

### Reproduction Discovery (2026-04-27 — by Dev)

**The story's premise that 4 `SubmitCommandHandler.Handle` `NullReferenceException` failures reproduce on current HEAD has been invalidated.** Per AC #1 and Task 1.3, this triggers a mandatory HALT and Project Lead consultation. The dev did NOT proceed with applying any fix or adding a regression test, because:

- AC #1 explicitly states: *"If the tests pass on first run despite the retro reporting they fail, the story must HALT and re-open with the discovery (the failures may have been incidentally cleared by Epic 1 retro carry-over merges; the proposal's reproduction premise would need to be re-validated)."*
- Task 1.3 explicitly states: *"If any of the 4 named NRE tests are now reported as **Passed** instead of **Failed**, HALT and re-open the story under 'verify proposal premise' framing — record the discovery in dev notes and consult the Project Lead before proceeding."*
- Epic 2 retro Theme 4 (referenced in this story spec) flags speculative fix as a recurring failure mode: applying option (a/b/c) per AC #3, or adding the AC #6 dedicated regression test, with no reproducing failure to anchor against, is exactly that antipattern.

**Reproduction attempt evidence (current HEAD `f803e9a`, 2026-04-27):**

1. `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~PayloadProtectionTests|FullyQualifiedName~CausationIdLoggingTests|FullyQualifiedName~LogLevelConventionTests|FullyQualifiedName~StructuredLoggingCompletenessTests" -p:NuGetAudit=false --logger "console;verbosity=detailed"` → **4 / 4 Réussi (Passed)** in ~96 ms each. The 4 named tests report Pass, not Fail, contrary to the retro's documented behavior.
2. Full Tier 2 sweep: `dotnet test tests/Hexalith.EventStore.Server.Tests/ -p:NuGetAudit=false` → **1642 / 1642 Réussi (Passed); 0 échec (Failed)**. The retro's documented baseline of 15 pre-existing failures (4 Pipeline NRE + 1 validator extension-size + 10 auth integration) is **no longer present** at current HEAD. Delta-of-4 measurement (AC #5) is moot when the starting baseline is 0 failures across the entire Tier 2 project.

**Why the failures may have cleared (hypothesis only — not validated; recorded for Project Lead context):**

- The story spec was authored against retro `epic-2-retro-2026-04-26.md` (2026-04-26) and references HEAD `4619f75`. Current HEAD is `f803e9a` (2026-04-27), which post-dates the retro by ~24 h and includes three Epic 1 / Epic 2 carry-over merges:
  - `2070c68` `refactor(server)!: thread aggregate type through persistence pipeline` — touched the 4 Logging test files (1-line edit each, only on the `EventPersister`-related tests inside the same files; the `SubmitCommandHandler_*` tests in those files were NOT touched). However, the BREAKING-CHANGE refactor reshaped the persistence pipeline, which may have indirectly cleared mock-state-graph behavior the failures depended on.
  - `bdff4c4` `feat(testing): add TerminatableComplianceAssertions helper (R1-A2)` and `b086ee5` `test(server): add Tier 2 tombstoning lifecycle test (R1-A7)` — added new Testing helpers + a new Tier 2 test. Neither directly modifies `SubmitCommandHandler` or the 4 failing test fixtures.
- A direct `git log -- src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` since the retro returned **zero commits** on that file. So the production code path the retro identified at "line 81" is structurally unchanged. The failure-clearing change must therefore be transitive (test-runner toolchain, NSubstitute behavior, restored package versions, or test-project compilation order against the post-`2070c68` interface signature).
- The 10 auth-integration tests and the 1 validator extension-size test that the retro counted toward the 15 are also not failing on current HEAD. This suggests the entire 15-failure baseline may have been cleared en bloc by something between `4619f75` and `f803e9a`.

**Validated facts:**
- Current HEAD: `f803e9a` (post `bdff4c4` + `b086ee5` merge of `feat/epic-1-retro-r1a2-r1a7`).
- `SubmitCommandHandler.cs` content unchanged since `e84550f code style` (pre-Epic-2 retro by ~6 weeks).
- 4 named tests pass; full Tier 2 = 1642 / 1642 green.
- DAPR sidecars (`dapr_redis`, `dapr_placement`, `dapr_scheduler`, `dapr_zipkin`) all healthy.

**Decision required from Project Lead (Jerome):**

Pick one — and update the story accordingly:

1. **Close as superseded.** Mark `R2-A8` action item closed-without-fix in `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` §6 with annotation `✅ Closed-without-fix — failures cleared between 4619f75 and f803e9a; baseline 15 → 0 on Server.Tests at HEAD f803e9a (post `feat/epic-1-retro-r1a2-r1a7` merge). Root-cause attribution not feasible without a reproducing failure.` Move sprint-status entry `post-epic-2-r2a8-pipeline-nullref-fix` to `done` with the same closure note. **AC #6's dedicated regression test is the open question** — adding it with no reproducing failure would be speculative, but skipping it leaves the null-path class unguarded for future regressions. Recommendation: add a *Tier 1-shaped guard test* in `SubmitCommandHandlerTests.cs` that pins the all-mocks-no-trackers shape (matches the 4 failing tests' construction) and asserts `Should.NotThrow` + `CorrelationId` round-trip. Justification: Epic 3 Story 3.1 will exercise this exact shape; pinning it now is cheaper than re-discovering it during 3.1's review. This is *not* "adding a regression test for a non-reproducing bug" — it is "adding a guard for a code path Epic 3 will exercise heavily, framed as a structural pin rather than a regression."

2. **Re-validate the proposal premise.** Have a senior reviewer (or run `git bisect`) confirm whether the Epic 2 retro's "15 pre-existing failures" was accurate at the time it was written. If yes, identify the commit between `4619f75` and `f803e9a` that incidentally cleared them, and document the inadvertent fix. If no (i.e., the retro's count was wrong), mark `R2-A8` closed-as-misattributed with retro erratum.

3. **Re-open with revised scope.** If the Project Lead has prior knowledge (or new evidence) that the failures still reproduce in a configuration the dev didn't run (e.g., CI's specific runner image, a different .NET SDK patch level, or a specific test ordering), document the configuration and re-issue the story with reproduction steps that anchor on that configuration. Tier 2 currently runs on Windows 11 with .NET SDK 10.0.103, dapr CLI 1.15.2, runtime 1.17.5, Docker Desktop; if CI uses a different combination, that combination is the missing reproduction step.

**The dev has NOT moved the story to `review`. Status remains `halted-pending-review`. Sprint-status entry remains `in-progress` with halt annotation.** Tasks 2 through 7 (root-cause analysis, fix, regression test, validation, retro closure, sprint-status sync) are pending Project Lead decision and have NOT been attempted; performing them on a non-reproducing failure would be speculative work that the story spec and Epic 2 retro Theme 4 explicitly forbid.

### Resolution (2026-04-27 — Project Lead chose Option 1 + structural pin)

**Decision:** close R2-A8 as superseded; add a forward-looking structural pin in `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs` framed as Epic 3 Story 3.1 prep, not as a regression test for a non-reproducing bug.

**What was delivered:**
1. **Pin test added** — `Handle_ValidCommand_DoesNotThrowNullReferenceWhenStoresAreNSubstituteMocks` in `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs`. Pins the all-mocks-no-trackers shape (5-arg constructor + `Substitute.For<>()` for status store / archive store / router / backpressure tracker, with the router's `RouteCommandAsync` configured to return `CommandProcessingResult(Accepted: true, CorrelationId: "corr-pin-1")`). Asserts the await completes without `NullReferenceException` and the result round-trips the correlation id. XML doc summary names R2-A8, the 4 historically-failing tests, the orthogonal-contracts argument (the 4 Logging tests assert payload-redaction / causation-id-presence / log-level-convention / structured-field-completeness — none can stand in as the load-bearing guard for the null-path class), and the Epic 3 Story 3.1 prep framing.
2. **Retro updated** — `epic-2-retro-2026-04-26.md` §6 R2-A8 row + §10 Commitments line annotated with closure-as-superseded.
3. **Production code untouched** — `SubmitCommandHandler.cs` was not modified (no NRE to fix; `git log` since `4619f75` shows zero commits on the file).
4. **Sprint-status updated** — entry moved through full lifecycle to `review` with closure annotation.

**What was NOT done (deliberately):**
- No git-bisect archaeology between `4619f75..f803e9a` to identify the inadvertent fixer. The Project Lead chose Option 1 over Option 2 explicitly; the historical attribution was assessed low-value vs cost.
- No "✅ Done <merge-commit-sha>" closure annotation on the retro — that style asserts a fix was applied, which would be untrue here. `✅ Closed-as-superseded` is the truthful framing.

**Commit-prefix alignment under Option 1 (resolves AC #10 mismatch):**
AC #10 prescribed `fix(server):` because the spec was authored expecting a real defect fix in `Hexalith.EventStore.Server`. Under Option 1, the diff is test-only + docs-only — no production source touched, no defect repaired. The truthful Conventional Commits prefix is `test(server):` (semantic-release: no-op bump). Using `fix(server):` would inflate the patch version under semantic-release for a non-fixing change. **Merge prefix MUST be `test(server):`** (or `chore(server):` if reviewer prefers, since the diff is also documentation-heavy); AC #10 is treated as superseded by the closure-as-superseded resolution. This deviation is intentional and tracked in the post-review code-review findings.

### Root Cause Analysis (TBD by Dev — populate during Task 2.7)

> **Dev:** Replace this paragraph with the actual root-cause writeup before submitting for code review. The write-up MUST address: (a) the file + line of the NRE in current HEAD, (b) which constructor overload the failing tests use, (c) why the parallel `Handle_ValidCommand_ReturnsCorrelationId` test passes despite using the same constructor, (d) which of options a / b / c (per AC #3) was chosen, (e) why the rejected options were rejected.
>
> Format expectation:
>
> ```
> The NRE fires at SubmitCommandHandler.cs:<LINE> when await <site> returns null
> because <reason — typically: NSubstitute auto-stub returns Task.FromResult(default)
> for Task<T?> methods, and the dereference at line <LINE> assumes non-null>.
> The 4 failing tests use the 5-arg constructor (statusStore, archiveStore, router,
> tracker, logger) which delegates to the primary 6-arg ctor with activityTracker:null
> and streamActivityTracker:null. The parallel Handle_ValidCommand_ReturnsCorrelationId
> passes because it injects InMemoryCommandStatusStore + InMemoryCommandArchiveStore —
> both real fakes whose <method> returns <non-null shape>, so the dereference at line
> <LINE> never observes the null.
>
> Fix chosen: option <a|b|c>. Rationale: <one sentence>. Rejected (a): <one phrase>.
> Rejected (b): <one phrase>. Rejected (c): <one phrase>.
> ```

### Hypothesis Space (input to Task 2 — DO NOT prematurely commit to one)

These are the four candidate root causes the proposal flagged. Task 2 is investigation-driven; the dev confirms or eliminates each one before applying a fix.

1. **NSubstitute auto-stub null in a `Task<T?>`-returning store method.** `ICommandStatusStore.ReadStatusAsync` returns `Task<CommandStatusRecord?>`; an unconfigured NSubstitute returns `Task.FromResult((CommandStatusRecord?)null)` which awaits to `null`. The `finalStatus?.…` chains in `Handle` (lines 121–144) use null-conditional and are *safe by inspection* — but verify against the actual stack trace. The `ReadStatusAsync` block at lines 167–169 (in the `!processingResult.Accepted` branch) is also a candidate — but it's not reached in the tests because the mocked router returns `Accepted: true`.

2. **A logger or activity-tracker dependency dereferenced via Activity/ActivitySource.** Each failing test class registers an `ActivityListener` in its constructor (e.g., `LogLevelConventionTests.cs:41-46`). If `SubmitCommandHandler` (or a transitive) reads `Activity.Current` and dereferences it, the listener registration would change observable behavior. Cross-check the `LoggerMessage`-emitted source for `Activity` references.

3. **`SubmitCommand.ToArchivedCommand()` reading a null field.** The extension at `src/Hexalith.EventStore.Server/Commands/ArchivedCommandExtensions.cs:14` reads 6 fields off the `SubmitCommand` record. All 6 are populated in every failing test's `SubmitCommand` literal (verified). This site is most likely **eliminated** — but record the elimination in dev notes for completeness.

4. **A LoggerMessage source-generator partial-method emission with a non-null assumption.** The `Log.CommandReceived` method at line 188-200 takes 8 string parameters; the test inputs populate all 8. This site is also most likely **eliminated** — but generators occasionally surprise; verify against the `obj/` `.g.cs` output.

The retro framing ("the failures appeared at line 81") suggests path #1 or #3 historically. Don't trust the historical framing — confirm against the *current* stack trace.

### Architecture Decisions

This story does NOT introduce new architecture. The fix is constrained to one of:
- A defensive null guard at a specific line (option a)
- A test-fixture wiring correction (option b)
- A DI registration alignment (option c)

No new types, no new contracts, no new packages. If the dev's investigation reveals that the fix requires a contract change (e.g., a new dependency that must be registered at runtime), the story scope has expanded and the dev MUST pause and consult the Project Lead before proceeding.

### Testing Standards (project-wide rules — apply to every story)

- **Tier 1 (Unit):** xUnit 2.9.3 + Shouldly + NSubstitute. No DAPR runtime, no Docker.
- **Tier 2 / Tier 3 (Integration) — REQUIRED end-state inspection:** If the story creates or modifies Tier 2 (`Server.Tests`) or Tier 3 (`IntegrationTests`) tests, each test MUST inspect state-store end-state (e.g., Redis key contents, persisted `EventEnvelope`, CloudEvent body, advisory status record). Asserting only API return codes, mock call counts, or pub/sub call invocations is forbidden — that is an API smoke test, not an integration test. *Reference:* Epic 2 retro R2-A6; precedent fixes in Story 2.1 (`CommandRoutingIntegrationTests` missing `messageId`) and Story 2.2 (persistence integration test rewrote to inspect Redis directly).
- **ID validation:** Any controller / validator handling `messageId`, `correlationId`, `aggregateId`, or `causationId` MUST use `Ulid.TryParse` (or accept any non-whitespace string per `AggregateIdentity` rules). `Guid.TryParse` on these fields is forbidden. *Reference:* Epic 2 retro R2-A7; precedent fix in Story 2.4 `CommandStatusController`.

### R2-A6 Compliance for This Story Specifically

The 4 tests being re-enabled and the 1 regression test being added are **NOT classical Tier 2 integration tests**:
- They live in the `Hexalith.EventStore.Server.Tests` project (which CLAUDE.md classifies as Tier 2)
- BUT they do NOT carry `[Collection("DaprTestContainer")]`
- They do NOT boot `daprd` or Docker
- They use NSubstitute mocks for every dependency

They are effectively Tier 1-shaped tests living in a Tier 2 project (because they assert against types internal to `Server` — `LoggerMessage`-emitted partial classes, `SubmitCommandHandler` itself — that aren't visible to the `Client.Tests` or `Contracts.Tests` projects).

**This story does NOT need to rewrite them as Redis-inspecting integration tests.** R2-A6's rule applies to Tier 2 tests that boot the live runtime — its purpose is to prevent integration tests that are smoke tests in disguise. The 4 tests at hand are unit-shape tests of *handler logging behavior*; their contract is "the handler emits a log entry with these fields at this level," which is intrinsically a unit-shape contract.

The dev should NOT redesign the 4 tests to inspect Redis state during this story. That would be scope creep AND would change what the tests assert (logging behavior, not state-store behavior). If a future story adds *integration* coverage of the SubmitCommand pipeline, it lives in a different file with `[Collection("DaprTestContainer")]` and Redis inspection — and that story would carry the R2-A6 rule.

The regression test added in Task 4 follows the same Tier 1-shape pattern.

### Project Structure Notes

- **`SubmitCommandHandler.cs` location:** `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`. Sibling `Pipeline/Commands/SubmitCommand.cs`, `Pipeline/Commands/DomainCommandRejectedException.cs`. The `Pipeline/Commands/` subfolder for the request/response records is a project convention; do not move records into `SubmitCommandHandler.cs`.
- **`ArchivedCommand` and the `ToArchivedCommand` extension:** `src/Hexalith.EventStore.Server/Commands/ArchivedCommandExtensions.cs`. The extension validates fields *only* on the inverse direction (`ToSubmitCommand` for replay); the forward direction trusts `SubmitCommand`'s record-shape invariants.
- **`InMemoryCommandStatusStore` / `InMemoryCommandArchiveStore`:** test fakes in `tests/Hexalith.EventStore.Server.Tests/Commands/` (or similar). The passing parallel test imports them; the 4 failing tests don't.
- **`SubmitCommandHandler` constructor overloads** (verbatim, per `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:18-47`):
  1. **Primary (6-arg, primary-constructor syntax):** `(ICommandStatusStore, ICommandArchiveStore, ICommandRouter, ICommandActivityTracker?, IStreamActivityTracker?, ILogger<SubmitCommandHandler>)`
  2. **4-arg legacy:** `(ICommandStatusStore, ICommandArchiveStore, ICommandRouter, ILogger<SubmitCommandHandler>)` — delegates to primary with both trackers null
  3. **5-arg with backpressure tracker:** `(ICommandStatusStore, ICommandArchiveStore, ICommandRouter, IBackpressureTracker, ILogger<SubmitCommandHandler>)` — delegates to primary with both trackers null AND validates `backpressureTracker` is non-null. **The 4 failing tests use this overload.**
  4. **6-arg with command-activity tracker AND backpressure tracker:** `(..., ICommandActivityTracker?, IBackpressureTracker, ILogger)` — delegates to primary with `streamActivityTracker:null`.

  **Note:** the 5-arg and 6-arg backpressure-tracker overloads accept `IBackpressureTracker` but the handler itself NEVER calls `tracker.TryAcquire(...)` or any other method on it. The constructor parameter is validated for non-null and discarded. This is unusual — backpressure logic lives in the actor pipeline (`CommandProcessingResult.BackpressureExceeded`), not in `Handle`. If the dev's investigation suggests refactoring the constructor signatures to remove the unused `IBackpressureTracker` parameter, that is **out of scope** for this story — it would be a public-API change requiring its own scope discussion.

### Constraints That MUST NOT Change

- The 4 `SubmitCommandHandler` constructor overload signatures (public surface; downstream callers depend on them)
- The `Handle` method's logging contract: emits `Log.CommandReceived` then `Log.CommandRouted` (or one of the failure-path log calls), with the structured fields the 4 tests assert (`MessageId`, `CorrelationId`, `CausationId`, `CommandType`, `TenantId`, `Domain`, `AggregateId`, `Stage=…`). The `LoggerMessage` source-generator pattern (`partial class Log` at lines 187–260) stays
- The `ICommandStatusStore` / `ICommandArchiveStore` / `ICommandRouter` / `IBackpressureTracker` interface surfaces — modifying them would force changes across `Server.Tests` baselines and is out of scope
- The advisory-write rule (Rule #12 per Story 2.4): every `await statusStore.…` / `await archiveStore.…` / activity-tracking call in `Handle` is wrapped in `try / catch (OperationCanceledException) throw / catch (Exception)` and never blocks the pipeline. If the fix involves moving code, preserve the try/catch shape verbatim
- The `Log.StatusReadForTrackingFailed` / `Log.ActivityTrackingFailed` / `Log.StreamActivityTrackingFailed` branches at lines 107–153 — the activity-tracking section is conditional on a non-null tracker AND `processingResult.Accepted` AND `EventCount > 0`; if the fix is in this region, preserve the boolean shape

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` § Proposal 2 (lines 167–209)] — originating spec
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 6 R2-A8 (line 104)] — action item
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 3 Struggle 4 (line 60)] — pre-existing 15-failure baseline (4 Pipeline + 1 validator + 10 auth)
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 7 Risks for Epic 3 (line 125)] — "The 4 Pipeline-scope `SubmitCommandHandler` NullRefs (D2-1) sit directly in Story 3.1's path. They will block Epic 3 work if not addressed first (R2-A8)"
- [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` § 4 Theme 4] — "It compiles and tests pass" ≠ "it works"
- [Source: `CLAUDE.md` § Build & Test Commands (Tier 2)] — `dapr init` prerequisite; `dotnet test tests/Hexalith.EventStore.Server.Tests/`
- [Source: `CLAUDE.md` § Code Review Process → Integration test rule] — R2-A6 rationale (this story's narrowing of R2-A6 is justified in `## Dev Notes → R2-A6 Compliance for This Story Specifically` above)
- [Source: `CLAUDE.md` § Commit Messages] — `fix(server):` Conventional Commits prefix
- [Source: `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:18-47`] — 4 constructor overloads
- [Source: `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:49-185`] — `Handle` method body
- [Source: `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:187-260`] — `Log` LoggerMessage partial class
- [Source: `src/Hexalith.EventStore.Server/Commands/ArchivedCommandExtensions.cs:14`] — `ToArchivedCommand` extension
- [Source: `src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs`] — `CommandProcessingResult` record (used by the mocked router in failing tests)
- [Source: `src/Hexalith.EventStore.Server/Pipeline/Commands/SubmitCommand.cs`] — `SubmitCommand` record (8 fields + `IsGlobalAdmin`)
- [Source: `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs:30-52`] — passing parallel test (`Handle_ValidCommand_ReturnsCorrelationId`)
- [Source: `tests/Hexalith.EventStore.Server.Tests/Logging/PayloadProtectionTests.cs:51-83`] — failing test 1 (`SubmitCommandHandler_NeverLogsPayloadData`)
- [Source: `tests/Hexalith.EventStore.Server.Tests/Logging/CausationIdLoggingTests.cs:52-72`] — failing test 2 (`SubmitCommandHandler_IncludesCausationId`)
- [Source: `tests/Hexalith.EventStore.Server.Tests/Logging/LogLevelConventionTests.cs:114-134`] — failing test 3 (`CommandReceived_UsesInformationLevel`)
- [Source: `tests/Hexalith.EventStore.Server.Tests/Logging/StructuredLoggingCompletenessTests.cs:53-80`] — failing test 4 (`CommandReceived_LogContainsAllRequiredFieldsAsync`)
- [Source: `_bmad-output/implementation-artifacts/post-epic-1-r1a7-tier2-tombstoning-lifecycle.md`] — sibling post-epic story precedent (story shape, Dev Notes structure, Tier 2 validation pattern)

## Dev Agent Record

### Agent Model Used

claude-opus-4-7 (Opus 4.7, 1M context) via Claude Code CLI

### Debug Log References

**Reproduction attempt 1 — targeted filter (Task 1.2; current HEAD `f803e9a`, 2026-04-27):**

```
$ dotnet test tests/Hexalith.EventStore.Server.Tests/ \
    --filter "FullyQualifiedName~PayloadProtectionTests.SubmitCommandHandler_NeverLogsPayloadData|FullyQualifiedName~CausationIdLoggingTests.SubmitCommandHandler_IncludesCausationId|FullyQualifiedName~LogLevelConventionTests.CommandReceived_UsesInformationLevel|FullyQualifiedName~StructuredLoggingCompletenessTests.CommandReceived_LogContainsAllRequiredFieldsAsync" \
    -p:NuGetAudit=false --logger "console;verbosity=detailed"

Réussi Hexalith.EventStore.Server.Tests.Logging.PayloadProtectionTests.SubmitCommandHandler_NeverLogsPayloadData [96 ms]
Réussi Hexalith.EventStore.Server.Tests.Logging.CausationIdLoggingTests.SubmitCommandHandler_IncludesCausationId [96 ms]
Réussi Hexalith.EventStore.Server.Tests.Logging.StructuredLoggingCompletenessTests.CommandReceived_LogContainsAllRequiredFieldsAsync [96 ms]
Réussi Hexalith.EventStore.Server.Tests.Logging.LogLevelConventionTests.CommandReceived_UsesInformationLevel [96 ms]

Série de tests réussie.
Nombre total de tests : 4
     Réussi(s) : 4
 Durée totale : 2,6842 Secondes
```

No `NullReferenceException` was emitted. The 4 named tests reported `Réussi` (Passed). Story Task 1.4 (paste stack trace verbatim) is therefore **N/A** — there is no stack trace to paste.

**Reproduction attempt 2 — full Tier 2 sweep (Task 1.5):**

```
$ dotnet test tests/Hexalith.EventStore.Server.Tests/ -p:NuGetAudit=false

Réussi!  - échec :     0, réussite :  1642, ignorée(s) :     0, total :  1642, durée : 5 s - Hexalith.EventStore.Server.Tests.dll (net10.0)
```

Pre-existing failure baseline at HEAD `f803e9a`: **0**. Retro's expected baseline at HEAD `4619f75`: 15 (4 Pipeline NRE + 1 validator extension-size + 10 auth integration). Delta = 15 → 0 across the entire Tier 2 suite. Without a reproducing failure, AC #5's "drops by exactly 4" measurement is structurally untestable.

**Environment validated:**
- DAPR CLI 1.15.2, runtime 1.17.5
- Docker containers healthy: `dapr_redis`, `dapr_placement`, `dapr_scheduler`, `dapr_zipkin` (all `Up 6 hours`)
- Working tree at `f803e9a` with non-functional uncommitted changes (sprint-status.yaml + this story file edits + `Hexalith.Tenants` submodule pointer + `.claude/mcp.json` + `_tmp_diff.patch` + this story's parent retro `.md`)
- .NET SDK 10.0.103 per `global.json`
- Build prior to test run: clean (Tier 2 project + transitive deps all built green)

### Completion Notes List

- **Task 1.1** ✅ — DAPR CLI + sidecars verified healthy; no `dapr init` required (already initialized; uptime 6 h).
- **Task 1.2** ✅ — Targeted filter executed; **all 4 named tests Pass**. The proposal's reproduction premise is invalidated.
- **Task 1.3** ⚠️ HALT TRIGGERED — per the story spec's own AC #1 + Task 1.3 instruction. Recorded discovery in Dev Notes → Reproduction Discovery (above). Project Lead consultation pending. Did **not** advance to Task 1.4 / 1.5 because Task 1.3 explicitly halts the workflow.
- **Task 1.5 (advanced anyway, for diagnostic value to the Project Lead)** ✅ — Full Tier 2 = **1642 / 1642 green, 0 failures**. The 15-failure baseline from the retro (4 Pipeline + 1 validator + 10 auth) is **not present** at current HEAD.
- **Tasks 2 through 7** — NOT attempted. The dev did not perform root-cause analysis, did not apply a fix, did not add a regression test, did not validate, did not close the retro action item, and did not update sprint-status to `review`. Per the story spec's own AC #1, AC #3 (rejected-paths discipline), AC #6 (regression-test rationale), and Epic 2 retro Theme 4 (speculative-fix-as-failure-mode), each of those tasks requires a reproducing failure to anchor against. Without one, the work is by the story spec's own framing speculative.
- **Sprint-status update** — moved `post-epic-2-r2a8-pipeline-nullref-fix` from `ready-for-dev` to `in-progress` with halt annotation. NOT moved to `review` (the work specified by the story is incomplete by design — premise invalidation halts it). Final status disposition (close-as-superseded / re-validate / re-open with revised scope) is the Project Lead's decision; see Dev Notes → Reproduction Discovery for the three options.

### Validation Results (2026-04-27, post-pin-test, HEAD `f803e9a` + uncommitted pin test)

**AC #4 (4 named tests pass) + AC #6 (pin test green) — combined filter:**
```
$ dotnet test tests/Hexalith.EventStore.Server.Tests/ \
    --filter "FullyQualifiedName~PayloadProtectionTests.SubmitCommandHandler_NeverLogsPayloadData|...|FullyQualifiedName~SubmitCommandHandlerTests" \
    -p:NuGetAudit=false
Réussi!  - échec :     0, réussite :     6, ignorée(s) :     0, total :     6
```
6 = 4 historical NRE tests + 2 in `SubmitCommandHandlerTests` class (the existing `Handle_ValidCommand_ReturnsCorrelationId` + the new `Handle_ValidCommand_DoesNotThrowNullReferenceWhenStoresAreNSubstituteMocks`).

**AC #5 (pre-existing failure delta) — full Tier 2 sweep:**
```
$ dotnet test tests/Hexalith.EventStore.Server.Tests/ -p:NuGetAudit=false --no-build
Réussi!  - échec :     0, réussite :  1643, ignorée(s) :     0, total :  1643, durée : 5 s
```
Pre-fix baseline (Task 1.5): 1642/1642. Post-pin: 1643/1643 (+1 from new pin test). Failures both before and after: 0. AC #5's "drops by exactly 4" target is structurally inapplicable when the starting baseline is 0; the spirit of the AC (no side-effect leakage outside scope) is satisfied — Tier 2 grew by exactly 1, which is the deliberate +1 from the pin test.

**AC #7 (Tier 1 ratchet) — windows green; counts at or above retro baseline:**

| Project | Result | Baseline (story spec) |
|---------|--------|------------------------|
| `Hexalith.EventStore.Contracts.Tests` | **271 / 271** Réussi | 271+ |
| `Hexalith.EventStore.Client.Tests` | **334 / 334** Réussi | 334+ |
| `Hexalith.EventStore.Sample.Tests` | **63 / 63** Réussi | 63+ |
| `Hexalith.EventStore.Testing.Tests` | **78 / 78** Réussi | 78+ |
| `Hexalith.EventStore.SignalR.Tests` | **32 / 32** Réussi | baseline preserved |

Total Tier 1 windows: **778 / 778** green (271 + 334 + 63 + 78 + 32). Pin test lives in Server.Tests (Tier 2 project) per AC #7's note.

**AC #8 (Release build clean):**
```
$ dotnet build Hexalith.EventStore.slnx --configuration Release -p:NuGetAudit=false
La génération a réussi.
    0 Avertissement(s)
    0 Erreur(s)
Temps écoulé 00:00:29.53
```
0 warnings, 0 errors with `TreatWarningsAsErrors=true`.

**Tier 3 (`tests/Hexalith.EventStore.IntegrationTests/`):** Per Task 5.5 of the story spec, Tier 3 is explicitly NOT in scope — confirmed correct given Option 1 (no production change to the in-process handler boundary).

**Post-code-review re-validation (2026-04-27, pin hardened):**

```
$ dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~SubmitCommandHandlerTests" -p:NuGetAudit=false
Réussi!  - échec :     0, réussite :     2, ignorée(s) :     0, total :     2, durée : 141 ms

$ dotnet test tests/Hexalith.EventStore.Server.Tests/ -p:NuGetAudit=false --no-build
Réussi!  - échec :     0, réussite :  1643, ignorée(s) :     0, total :  1643, durée : 6 s

$ dotnet build Hexalith.EventStore.slnx --configuration Release -p:NuGetAudit=false
La génération a réussi.
    0 Avertissement(s)
    0 Erreur(s)
Temps écoulé 00:00:13.45
```

Hardened pin (distinct correlation-id sentinels + `Received(1)` shape assertions + `DidNotReceive()` no-trackers lock + `ShouldNotBeNull()` defensive + missing `using Hexalith.EventStore.Contracts.Commands;` directive added) is green. Tier 2 holds at 1643/1643 (pre-review baseline preserved). Release build remains 0/0.

### File List

**Modified:**
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs` — added `[Fact] Handle_ValidCommand_DoesNotThrowNullReferenceWhenStoresAreNSubstituteMocks` (structural pin for Epic 3 Story 3.1 prep) with full XML-doc summary referencing R2-A8 closure.
- `_bmad-output/implementation-artifacts/epic-2-retro-2026-04-26.md` — §6 R2-A8 row annotated with `✅ Closed-as-superseded 2026-04-27` + closure trace; §10 Commitments line appended with R2-A8 closure pointer.
- `_bmad-output/implementation-artifacts/post-epic-2-r2a8-pipeline-nullref-fix.md` (this file) — Status header → `review`; Task checkboxes (1.1/1.2/1.3/1.5 ✅, 1.4 N/A, 2/3 N/A per HALT, 4.1–4.6 ✅, 5/6/7 ✅); Dev Notes → Reproduction Discovery (HALT evidence) + Resolution (Option 1 trace); Dev Agent Record fully populated.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `post-epic-2-r2a8-pipeline-nullref-fix`: `ready-for-dev` → `in-progress` → `review` with closure-as-superseded annotation; `last_updated` reflects the closure.

**NOT modified (deliberately, per Option 1):**
- `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` — no production change applied; no NRE to fix on current HEAD.
- `tests/Hexalith.EventStore.Server.Tests/Logging/{PayloadProtection,CausationIdLogging,LogLevelConvention,StructuredLoggingCompleteness}Tests.cs` — no test-fixture wiring change; the all-mocks shape works as-is.
- `src/Hexalith.EventStore.Server/DependencyInjection/*` — no DI registration change.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-2-retro-cleanup.md` — preserved as-written (originating spec; closure traces through the retro action-item table + sprint-status, per Task 6.3).

### Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-04-27 | HALT triggered per AC #1 / Task 1.3 — story premise invalidated; 4 named tests pass on first run at HEAD `f803e9a`; full Tier 2 = 1642/1642 green; pre-existing failure baseline 15 → 0 between retro HEAD `4619f75` and current. Story Status: `halted-pending-review`. Three Decision options recorded in Dev Notes → Reproduction Discovery. | Dev (Opus 4.7 via Claude Code) |
| 2026-04-27 | Project Lead (Jerome) chose Option 1: close R2-A8 as superseded + add forward-looking structural-pin test framed as Epic 3 Story 3.1 prep. Pin test added (`Handle_ValidCommand_DoesNotThrowNullReferenceWhenStoresAreNSubstituteMocks`). Epic 2 retro §6 R2-A8 row + §10 Commitments line annotated with closure-as-superseded. Story Status → `review`; sprint-status entry → `review`. | Dev (Opus 4.7 via Claude Code) |
| 2026-04-27 | Code review complete (Blind Hunter + Edge Case Hunter + Acceptance Auditor); 4 decision-needed all resolved as patches by Project Lead, 6 patches applied, 8 deferred (out of Option 1 narrow-pin scope; logged in `deferred-work.md`), 8 dismissed. Pin hardened with: distinct command/router correlation-id sentinels (pins handler:56 round-trip semantics), `Received(1)` shape assertions on success-path store calls, `DidNotReceive()` on `ReadStatusAsync` (locks no-trackers shape), `result.ShouldNotBeNull()` defensive check, and removal of dead `tracker.TryAcquire` stub. AC #10 superseded: merge prefix is `test(server):` (test-only diff under Option 1; `fix(server):` would inflate patch version). Retro §6 R2-A8 closure row updated to point readers to story §Dev Agent Record → Debug Log References + §Validation Results for verbatim run-output evidence. Story Status → `done`. | Dev (Opus 4.7 via Claude Code) |

### Review Findings

_Code review run 2026-04-27 (Blind Hunter + Edge Case Hunter + Acceptance Auditor). 3 layers, 0 failed. 4 decision-needed (all resolved as patches by Project Lead), 6 patches applied, 8 deferred, 8 dismissed._

**Decision-needed** (resolved by Project Lead 2026-04-27 — all 4 folded into patches below):

- [x] [Review][Decision → Patch] Add `Received()` / `DidNotReceive()` assertion on store mocks to anchor the pin — RESOLVED as patch. Pin now asserts `statusStore.Received(1).WriteStatusAsync`, `archiveStore.Received(1).WriteCommandAsync`, `router.Received(1).RouteCommandAsync`, and `statusStore.DidNotReceive().ReadStatusAsync` (the last one locks the no-trackers shape — read-back at handler:97-110 must NOT fire under the 5-arg ctor). Sources: blind+edge+auditor.
- [x] [Review][Decision → Patch] Add `result.ShouldNotBeNull()` before dereferencing `.CorrelationId` — RESOLVED as patch. One-line defensive check added. Source: blind.
- [x] [Review][Decision → Patch] Commit-prefix at merge: `fix(server):` (per AC #10) vs `test(server):` (truthful) — RESOLVED as `test(server):`. AC #10's prescription was written assuming a real production defect fix; under Option 1 closure-as-superseded the diff is test-only + docs-only. Using `fix(server):` would inflate the patch version under semantic-release for a non-fixing change. The Resolution section above now documents the AC #10 supersession explicitly. Source: auditor.
- [x] [Review][Decision → Patch] Strengthen audit-artifact pointer in retro R2-A8 closure row — RESOLVED as patch. Retro row now points readers to the story's `Dev Agent Record → Debug Log References` and `Validation Results` sections (which embed verbatim console output). House convention preserved (inline run-output rather than external CI link), pointer made discoverable. Source: blind.

**Patch** (all 6 applied 2026-04-27):

- [x] [Review][Patch] Use distinct correlation-id sentinels in pin test [`tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs`] — Applied. `CommandCorrelationId = "corr-from-cmd-1"` and `RouterCorrelationId = "corr-from-router-1"` are now distinct const sentinels; the assertion `result.CorrelationId.ShouldBe(CommandCorrelationId)` pins the round-trip semantics from `SubmitCommandHandler.cs:56` (`new SubmitCommandResult(request.CorrelationId)` — handler returns the *command's* id, not the router's). Source: blind.
- [x] [Review][Patch] Remove dead `tracker.TryAcquire(...).Returns(true)` arrange [`tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs`] — Applied. Tracker stub is now structural-only (non-null requirement of the 5-arg ctor at `SubmitCommandHandler.cs:32-38`); the dead `Returns(true)` setup is gone. Inline comment documents that the handler never invokes any method on the tracker. Sources: blind+edge+auditor.
- [x] [Review][Patch] Add `Received(1)` shape assertions on success-path store calls — Applied. Pin now verifies `statusStore.WriteStatusAsync`, `archiveStore.WriteCommandAsync`, `router.RouteCommandAsync` were each called exactly once with the expected tenant/correlationId. Source: D1 resolution.
- [x] [Review][Patch] Add `DidNotReceive()` assertion on `ReadStatusAsync` to lock the no-trackers shape — Applied. The tracker-gated read-back at handler:97-110 must not fire under the 5-arg ctor; pin now catches a future change that adds a non-null default tracker. Source: D1 resolution.
- [x] [Review][Patch] Add `result.ShouldNotBeNull()` before correlation-id dereference — Applied. Guards against future nullable-return refactor of `Handle`. Source: D2 resolution.
- [x] [Review][Patch] AC #10 supersession + retro pointer strengthening — Applied (story Resolution section + retro §6 R2-A8 row). Source: D3 + D4 resolutions.

**Deferred** (real but pre-existing or out of spec scope per Option 1 narrow-pin framing):

- [x] [Review][Defer] Pin does not cover `Accepted: false / BackpressureExceeded: true` rejection branch — out of spec scope; pin is intentionally narrow per Option 1 framing. `SubmitCommandHandler.cs:156-165` (source: edge HIGH)
- [x] [Review][Defer] Pin does not cover `Accepted: false / BackpressureExceeded: false` (DomainCommandRejected) rejection branch — out of spec scope per Option 1. The second `statusStore.ReadStatusAsync` site is the most plausible historical NRE location per the hypothesis-space writeup, but pinning it requires a configured rejection result — narrow pin chose not to. `SubmitCommandHandler.cs:167-177` (source: edge HIGH)
- [x] [Review][Defer] Pin does not cover the `InvalidOperationException` fallback path in `SubmitCommandHandler.Handle` — out of spec scope. `SubmitCommandHandler.cs:179` (source: edge MEDIUM)
- [x] [Review][Defer] Pin does not cover throwing-store advisory-write edges (the try/catch-OCE-rethrow / catch-Exception-swallow contract from Story 2.4 Rule #12) — out of spec scope per Option 1. `SubmitCommandHandler.cs:72-77, 86-91, 104-109` (source: edge MEDIUM)
- [x] [Review][Defer] Pin does not cover the 6-arg ctor with non-null `ICommandActivityTracker` / `IStreamActivityTracker` mocks; activity-tracker branches are dead under the 5-arg ctor selected. A future change that calls `activityTracker.TrackAsync(...)` and dereferences its returned Task without null-check would not regress this pin. `SubmitCommandHandler.cs:98, 113, 136` (source: edge MEDIUM)
- [x] [Review][Defer] Pin does not cover cancellation-during-await; uses `CancellationToken.None`. OCE rethrow paths (handler:72, 86, 104, 127, 148) are unverified. (source: edge LOW)
- [x] [Review][Defer] `Payload: [0x01]` and `Extensions: null` shape pinned; size-sensitive and Extensions-populated paths unprobed. Handler does not branch on payload length so the gap is small; documentary mismatch with the XML doc's "fixture shape downstream of `POST /api/v1/commands`" claim. (source: edge NIT + blind NIT)
- [x] [Review][Defer] Pre-existing test `Handle_ValidCommand_ReturnsCorrelationId` carries the same identical-correlation-id-sentinel ambiguity as the new pin — pre-existing, not introduced by this diff. `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs:30-52` (source: blind)

**Dismissed** (noise / handled / by-design):

- The "Shouldly's `Should.NotThrowAsync` does not surface a `Task<T>` result" justification comment is over-explained — minor; rationale isn't wrong, just verbose. (source: blind NIT)
- Sprint-status YAML inline comment exceeds typical line lengths — pre-existing convention across many sprint-status entries; not introduced by this diff. (source: blind LOW)
- Speculative root-cause attribution to commit `2070c68` — already caveated as "Most likely transitive culprit … though `SubmitCommandHandler.cs` itself was not modified." Hedging is correct under Option 1. (source: blind LOW)
- Blind Hunter unverifiable-symbols flag — by-design constraint; symbols verify on file inspection. (source: blind NIT)
- Doc-comment "fixture shape" minor over-claim — covered as a deferral above. (source: blind NIT)
- `request == null` guard at `SubmitCommandHandler.cs:50` uncovered — Edge Case Hunter explicitly labeled "intentional, by-design narrow pin scope." (source: edge NIT)
- Activity-tracker branches advertised as covered — Auditor: "Acceptable under Option 1's structural-pin framing." Captured in deferral DF-6 above. (sources: blind+auditor)
- AC #1–#5 vacuous-satisfaction concerns — Auditor confirmed all are appropriately documented as inapplicable / vacuous under Option 1; Resolution + Reproduction Discovery sections cover the audit trail. (source: auditor)
