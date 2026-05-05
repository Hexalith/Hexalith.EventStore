---
storyId: post-epic-deferred-dw1-projection-and-drain-hardening
storyKey: post-epic-deferred-dw1-projection-and-drain-hardening
storyFile: _bmad-output/implementation-artifacts/post-epic-deferred-dw1-projection-and-drain-hardening.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-post-epic-deferred-dw1-projection-and-drain-hardening.md
detectedStack: backend
testFramework: xunit-v3
inputDocuments:
  - _bmad-output/implementation-artifacts/post-epic-deferred-dw1-projection-and-drain-hardening.md
  - tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
  - .claude/skills/bmad-testarch-atdd/resources/tea-index.csv
  - _bmad/tea/config.yaml
  - knowledge:data-factories
  - knowledge:test-quality
  - knowledge:test-healing-patterns
  - knowledge:test-levels-framework
  - knowledge:test-priorities-matrix
  - knowledge:ci-burn-in
generatedTestFiles: []
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-05-handoff
lastStep: step-05-handoff
lastSaved: 2026-05-05
generationMode: ai-generation
generatedTestFiles:
  - tests/Hexalith.EventStore.Server.Tests/Projections/Dw1ProjectionDeliveryAtddTests.cs
  - tests/Hexalith.EventStore.Server.Tests/Projections/Dw1PollerCorruptionAtddTests.cs
  - tests/Hexalith.EventStore.Server.Tests/Actors/Dw1DrainHardeningAtddTests.cs
totalScaffolds: 25
buildVerified: true
runtimeVerified: true
---

# ATDD Red-Phase Checklist — DW1 Projection & Drain Hardening

## Step 01 — Preflight & Context

### Stack Detection
- Detected: `backend` (.NET 10, xUnit v3, Shouldly, NSubstitute)
- Frontend Playwright project (`Hexalith.EventStore.Admin.UI.E2E`) exists but is unrelated to DW1 scope
- Loading profile: backend-only knowledge fragments

### Prerequisites
- [x] Story has clear acceptance criteria (13 ACs, evidence-target table, decision ledger requirements)
- [x] Test framework configured: `Hexalith.EventStore.Server.Tests.csproj` references xunit.v3, Shouldly, NSubstitute
- [x] Dev environment available (.NET SDK 10.0.103 pinned)

### Target Production Files (read-only context for scaffolds)
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs`
- `src/Hexalith.EventStore.Server/Projections/KeyedSemaphore.cs`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs`

### Target Test Files (extend, not create)
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorRefreshIntervalTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionPollerServiceTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/KeyedSemaphoreTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/UnpublishedEventsRecordTests.cs`

### Stable Diagnostic Vocabulary (binding for assertions)

**Projection (`/project`) reason codes**
- `project_upstream_4xx`, `project_upstream_5xx`
- `project_unsupported_content_type`, `project_invalid_charset`, `project_malformed_json`
- `project_invalid_projection_type`, `project_invalid_state`
- `project_timeout`, `project_cancelled`
- `checkpoint_drift`
- `unknown`

**Tracker disposition codes**
- `tracker_corrupt_scope_index`, `tracker_corrupt_identity_index`
- `tracker_recovered`, `tracker_terminal_failure`

**Drain activity reason codes**
- `drain_event_count_mismatch`, `drain_missing_event`
- `drain_publish_failed`, `drain_terminal_failure`
- `unknown`

### TEA Config Flags
- `tea_use_playwright_utils`: true (loaded API-only profile per backend stack — but skipped because no `/project` HTTP-facing test scaffolding is required at the Playwright layer)
- `tea_use_pactjs_utils`: true (skipped — DW1 does not change service contracts)
- `tea_browser_automation`: auto (skipped — backend)
- `test_stack_type`: auto → backend
- `risk_threshold`: p1

### Confirmation
User confirmed inputs on 2026-05-05. Proceeding to step-02 (generation mode).

## Step 02 — Generation Mode

**Mode chosen: AI Generation**

Rationale:
- `{detected_stack}` = `backend` → backend rule mandates AI generation (no browser recording).
- Story scope is .NET projection delivery and Dapr actor drain logic — no UI interaction surface.
- Acceptance criteria already define stable reason-code vocabulary and side-effect assertions, which is enough to author failing xUnit scaffolds directly from source-code analysis.

Recording mode skipped per step-02 rule for backend stack.

## Step 03 — Test Strategy

### Acceptance Criteria → Test Mapping

| AC | Concern | Priority | Level | Target test file | Red-phase scaffold scenarios |
|---|---|---|---|---|---|
| #1 | Checkpoint drift | P0 | Unit (Tier 2) | `Projections/Dw1ProjectionDeliveryAtddTests.cs` | Persisted `LastDeliveredSequence > aggregate.MaxSequence` → orchestrator emits `checkpoint_drift` reason code, does NOT call write actor, does NOT advance checkpoint |
| #2 | `/project` failure classification | P0 | Unit | `Projections/Dw1ProjectionDeliveryAtddTests.cs` | 4xx → `project_upstream_4xx`; 5xx → `project_upstream_5xx`; non-JSON content type → `project_unsupported_content_type`; invalid charset → `project_invalid_charset`; malformed JSON → `project_malformed_json`; empty `ProjectionType` → `project_invalid_projection_type`; null/empty State → `project_invalid_state` |
| #3 | Cancellation vs timeout | P0 | Unit | `Projections/Dw1ProjectionDeliveryAtddTests.cs` | (a) Host token cancelled → OCE propagates; (b) Inner HTTP timeout while host live → classified `project_timeout`, returns without throwing |
| #4 | Per-aggregate serialization | P0 | Concurrency unit | `Projections/Dw1ProjectionDeliveryAtddTests.cs` | Two concurrent `DeliverProjectionAsync` for same `ActorId` → second waits on `KeyedSemaphore`; assert only one in-flight at a time |
| #5 | Tracker corruption bounded | P1 | Unit (poller boundary) | `Projections/Dw1PollerCorruptionAtddTests.cs` | (a) Enumerator throws scope-index corruption → emits `tracker_corrupt_scope_index`, schedules next-due via `AdvanceKnownPollingDomains`; (b) identity-index corruption → emits `tracker_corrupt_identity_index`; both bounded — no tight retry within same tick |
| #6 | Tracker scaling docs | P3 | Documentation | (none) | Recorded in deferred-work / dev notes — no test |
| #7 | Drain poison policy | P1 | Unit | `Actors/Dw1DrainHardeningAtddTests.cs` | (a) `EventCount` mismatch → activity tag `eventstore.failure_reason = drain_event_count_mismatch`, no `PublishEventsAsync`, no `RemoveStateAsync`, no `DecrementPendingCommandCountAsync`; (b) Missing event in range → `drain_missing_event` with same invariants |
| #8 | Drain stable reason codes | P1 | Unit | `Actors/Dw1DrainHardeningAtddTests.cs` | Activity tag `eventstore.failure_reason` value is bounded reason-code (one of vocabulary) and NOT raw exception text or arbitrary message |
| #9 | Reminder re-entrancy | P2 | Unit (idempotence) | `Actors/Dw1DrainHardeningAtddTests.cs` | Two sequential `ReceiveReminderAsync` invocations against the same drained record → only one publish, only one decrement, only one reminder unregister |
| #10 | EventId/reason-code uniqueness | P2 | Static review | (none) | Local file scan during dev handoff |
| #11 | Production behavior coverage | P0 | Meta | (all scaffolds) | Side-effect assertions only (no helper-internal-only checks) |
| #12 | Scope boundaries | n/a | Code review | (none) | Reviewer checklist |
| #13 | Bookkeeping | n/a | Story closure | (none) | Dev Agent Record updates |

### Test Level Selection (backend rules)

- **Unit (Tier 2 in `Hexalith.EventStore.Server.Tests`)**: All DW1 ACs except #6, #10, #12, #13.
  - Justification: every behavioral AC has a clear public seam (orchestrator method, poller `PollOnceAsync`, drain reminder handler) that can be exercised with NSubstitute mocks of `DaprClient`, `IActorProxyFactory`, `IDomainServiceResolver`, `IEventPublisher`, and `IActorStateManager`.
  - **No Tier 3 required** — this story does not change Dapr/Aspire runtime behavior.
- **No E2E** (backend stack rule).
- **No contract tests** — `/project` response contract is not changing.

### Red-Phase Strategy (CI-Compatible)

Project rule: "All existing and new tests must pass before a story is complete." A literal red-phase suite would break CI immediately.

**Strategy**: Author scaffolds with `[Fact(Skip = "ATDD red phase — DW1 AC#X. Remove Skip when implementing.")]`. Each test:
- **Compiles** against current code (uses string literals for reason codes, not yet-defined constants).
- **Fails when Skip removed** because current code does not emit the stable reason codes / does not detect drift / does not classify timeout vs cancellation.
- **Dev workflow**: removes `Skip` per AC as implementation lands, watches it go red, then green.

This preserves the TDD spec contract while keeping CI green during the staged implementation.

### Reuse & Helpers

- Reuse `TestUtilities/TestLogger<T>` (`TestLogger.cs`) — captures `LogEntry(Level, EventId, Message)` so reason-code substring assertions are simple (`entries.ShouldContain(e => e.Message.Contains("Reason=project_upstream_4xx"))`).
- Reuse `JsonResponseHandler` and `RequestCapturingHandler` patterns from `ProjectionUpdateOrchestratorTests.cs`. Add new handlers: `StatusCodeOnlyHandler(HttpStatusCode)`, `MalformedJsonHandler(string body)`, `UnsupportedContentTypeHandler(string mediaType)`, `InvalidCharsetHandler`.
- Reuse `CreateActor` / `CreateDrainRecord` helpers from `EventDrainRecoveryTests.cs` for drain scaffolds.
- Reuse poller-test patterns from `ProjectionPollerServiceTests.cs` — including `IProjectionCheckpointTracker` substitutes that yield via `Returns` on `IAsyncEnumerable<AggregateIdentity>`.

### Out of Scope for This Run

- AC #6 (scaling docs): owner is dev's deferred-work ledger, not a test.
- AC #10 (EventId uniqueness): static review during dev handoff, not a test.
- AC #12 (scope boundaries): reviewer guardrail.
- AC #13 (bookkeeping): story closure step.

## Step 04 — Generated Tests

### Files

| File | LOC | Tests | ACs |
|---|---|---|---|
| `tests/Hexalith.EventStore.Server.Tests/Projections/Dw1ProjectionDeliveryAtddTests.cs` | ~360 | 14 | #1, #2, #3, #4 |
| `tests/Hexalith.EventStore.Server.Tests/Projections/Dw1PollerCorruptionAtddTests.cs` | ~155 | 4 | #5 |
| `tests/Hexalith.EventStore.Server.Tests/Actors/Dw1DrainHardeningAtddTests.cs` | ~395 | 7 | #7, #8, #9 |
| **Total** | | **25** | |

All tests use `[Fact(Skip = "ATDD red phase — DW1 AC#X. Remove Skip when implementing.")]` so CI stays green.

### Build & Runtime Verification

- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release` — **0 warnings, 0 errors**.
- `dotnet test --filter "FullyQualifiedName~Dw1" --no-build` — **25 skipped, 0 passed, 0 failed**.
- Skip messages name the AC and the action ("Remove Skip when implementing").

### Compile-time fixes applied during generation

- `Dw1PollerCorruptionAtddTests.ThrowingTracker.EnumerateTrackedIdentitiesAsync` — added unreachable `yield break;` after the throw to satisfy `IAsyncEnumerable` async-iterator contract (CS8420). `#pragma warning disable CS1998, CS0162` covers the unused-await + unreachable-code warnings on this throw-only iterator.
- `Dw1ProjectionDeliveryAtddTests.DeliverProjection_TwoOverlappingCallsForSameActorId_AreSerializedByKeyedSemaphore` — replaced `async _ =>` lambda passed to `NSubstitute.Returns(...)` with a local `async Task<EventEnvelope[]>` method to disambiguate NSubstitute's `Returns<T>` overload resolution (CS0029).

## Step 05 — Dev Handoff

### How the dev opens the red phase

For each AC the dev is implementing, locate the corresponding `[Fact(Skip = "...")]` tests and:

1. Remove `Skip = "..."` from the `[Fact]` attribute (one or more tests at a time).
2. Run the focused slice:
   ```bash
   dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj \
     --filter "FullyQualifiedName~Dw1" --configuration Release
   ```
3. Watch the test go red. Implement the production change. Watch it go green.
4. Move to the next AC.

### AC → Test File index (for dev/reviewer convenience)

| AC | Where to remove Skip first |
|---|---|
| #1 (checkpoint drift) | `Projections/Dw1ProjectionDeliveryAtddTests.cs` — tests starting `DeliverProjection_PersistedCheckpointAheadOfAggregateStream_*` |
| #2 (`/project` failures) | `Projections/Dw1ProjectionDeliveryAtddTests.cs` — tests starting `DeliverProjection_*Reason*` (8 tests) |
| #3 (cancellation vs timeout) | `Projections/Dw1ProjectionDeliveryAtddTests.cs` — `*HostTokenCancelled*`, `*HttpTimeoutWhileHostStillRunning*` |
| #4 (per-aggregate serialization) | `Projections/Dw1ProjectionDeliveryAtddTests.cs` — `*TwoOverlappingCallsForSameActorId_AreSerializedByKeyedSemaphore` |
| #5 (tracker corruption) | `Projections/Dw1PollerCorruptionAtddTests.cs` — all 4 tests |
| #7 (drain poison) | `Actors/Dw1DrainHardeningAtddTests.cs` — `DrainEventCountMismatch_DoesNotDecrementPendingCommandCount` |
| #8 (drain stable codes) | `Actors/Dw1DrainHardeningAtddTests.cs` — tests starting `Drain*ActivityFailureReasonTagIs*` and `*ActivityFailureReasonTagDoesNotContainRawExceptionMessage` |
| #9 (reminder re-entrancy) | `Actors/Dw1DrainHardeningAtddTests.cs` — tests starting `DrainReminder_FiredTwiceForSameCorrelationId_*` |

### Diagnostic-vocabulary reminders for dev

- Reason codes are emitted as **string literals** in tests. The dev may either:
  - Emit the literal directly in `LoggerMessage` template (`Reason={Reason}` with a constant); or
  - Introduce a static class of constants (e.g. `ProjectionReasonCodes.UpstreamFourXx = "project_upstream_4xx"`) and update the tests to reference the constants — either approach satisfies the assertions.
- Activity tag values must be **bounded** (test asserts `< 64 chars` and that raw exception text is not present). Keep raw exception text in structured logs only.

### Stop-sign reminders (from story Advanced Elicitation)

These remain in scope for the dev to refuse — and to record as deferred-work entries instead of patching:

- Public supportability or admin-facing contract for `/project`, tracker, or drain diagnostics.
- Automatic tracker rebuild or drain terminal disposition without a recorded product/architecture choice.
- Cross-process projection serialization, global queues, broad caches, or Dapr component changes.
- New integration or Aspire runtime dependency solely to prove behavior that can be covered by focused Tier 2 tests.

### Out-of-Scope items that still need closure (story bookkeeping)

- AC #6 (tracker scaling): Dev must record current scaling model (`pageSize=100`, `MaxIdentitiesPerTick=100`, ETag retries=3) and any deferred limit (with concrete trigger thresholds: identity count, scope/page count, polling-interval) in `deferred-work.md` — no test scaffold needed.
- AC #10 (EventId uniqueness): Dev must grep touched files for EventId collisions during handoff (current allocations: 1110-1136 in projection code, plus drain log codes in `AggregateActor`). New EventIds must not reuse any existing value.
- AC #12 (scope boundaries) / AC #13 (bookkeeping): Reviewer + story-closure checklists.

### Sign-off

- Master Test Architect run completed 2026-05-05.
- Build clean. Runtime: 25 skipped scaffolds visible to dev and reviewer.
- Story moves forward to dev-story execution (Amelia / `bmad-dev-story`) with this checklist as the test-side input.
