# Story 1.5: CommandStatus Enum & Aggregate Tombstoning

Status: done

## Story

As a domain service developer,
I want a typed command lifecycle enum and the ability to mark aggregates as terminated,
So that command status tracking uses a shared vocabulary and terminated aggregates cleanly reject further commands.

## Acceptance Criteria

1. **CommandStatus enum** contains exactly 8 states: Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut (UX-DR16). All public types have XML documentation (UX-DR19).

2. **Aggregate tombstoning** — when a terminal event is applied (FR66), the aggregate state reflects termination, subsequent commands are rejected with a domain rejection event (via `IRejectionEvent`), and the event stream remains immutable and replayable.

3. All existing and new Tier 1 tests pass.

4. **Done definition:** CommandStatus enum verified complete, `ITerminatable` interface defined in Contracts, `AggregateTerminated` rejection event defined, `EventStoreAggregate.ProcessAsync` rejects commands on terminated state, Counter sample demonstrates tombstoning with a terminal event (`CounterClosed`), all Tier 1 tests green.

## Tasks / Subtasks

- [x] Task 1: Verify CommandStatus enum completeness (AC: #1) — audit only, no code changes expected
  - [x] 1.1 Verify `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs` contains exactly 8 states: Received (0), Processing (1), EventsStored (2), EventsPublished (3), Completed (4), Rejected (5), PublishFailed (6), TimedOut (7)
  - [x] 1.2 Verify `CommandStatusRecord.cs` has XML documentation on all public members
  - [x] 1.3 Verify CommandStatus tests exist in Contracts.Tests (search for `CommandStatus` tests). If no dedicated test class exists, add `tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs` verifying: enum has exactly 8 values, explicit integer assignments match expected values, all terminal statuses identified
  - [x] 1.4 Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` to confirm all pass
- [x] Task 2: Define `ITerminatable` interface in Contracts (AC: #2)
  - [x] 2.1 Create `src/Hexalith.EventStore.Contracts/Aggregates/ITerminatable.cs` with `bool IsTerminated { get; }` property. XML docs must include a `<remarks>` warning: "States implementing this interface MUST also provide a no-op `Apply(AggregateTerminated)` method, because the framework persists `AggregateTerminated` rejection events to the event stream and rehydration replays all events." Interface in `Hexalith.EventStore.Contracts.Aggregates` namespace
  - [x] 2.2 Add unit test: `tests/Hexalith.EventStore.Contracts.Tests/Aggregates/ITerminatableTests.cs` — verify interface shape (has `IsTerminated` property, property type is `bool`)
- [x] Task 3: Define `AggregateTerminated` rejection event in Contracts (AC: #2)
  - [x] 3.1 Create `src/Hexalith.EventStore.Contracts/Events/AggregateTerminated.cs` — a sealed record implementing `IRejectionEvent`. Follows past-tense negative naming (Rule 8). Include `string AggregateType` (class name, e.g., `"CounterAggregate"` — diagnostic context, not routing) and `string AggregateId` properties. XML docs
  - [x] 3.2 Add test: verify `AggregateTerminated` implements `IRejectionEvent`, verify it follows naming convention
- [x] Task 4: Add tombstoning guard to `EventStoreAggregate.ProcessAsync` (AC: #2)
  - [x] 4.1 In `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs`, modify `ProcessAsync` to check if `state is ITerminatable { IsTerminated: true }` AFTER rehydration, BEFORE command dispatch. If terminated, return `DomainResult.Rejection(new IRejectionEvent[] { new AggregateTerminated(AggregateType: GetType().Name, AggregateId: command.AggregateId) })`. The existing rejection path (IRejectionEvent → actor → rejection event persisted) handles it from there
  - [x] 4.2 Add tests in `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs`:
    - Test: aggregate with `ITerminatable` state that `IsTerminated == true` returns `DomainResult.IsRejection == true` with `AggregateTerminated` event
    - Test: aggregate with `ITerminatable` state that `IsTerminated == false` processes command normally
    - Test: aggregate with state NOT implementing `ITerminatable` processes command normally (no termination check — backward compatible)
    - Test: aggregate with null state (first command, no prior events) and `ITerminatable` state type processes command normally (null can't be ITerminatable, guard is safe)
- [x] Task 5: Counter sample demonstrates tombstoning (AC: #2)
  - [x] 5.1 Add terminal event `CounterClosed` in `samples/Hexalith.EventStore.Sample/Counter/Events/CounterClosed.cs` — sealed record implementing `IEventPayload`. Past-tense naming (Rule 8). XML docs
  - [x] 5.2 Add command `CloseCounter` in `samples/Hexalith.EventStore.Sample/Counter/Commands/CloseCounter.cs` — sealed record. XML docs
  - [x] 5.3 Update `CounterState` to implement `ITerminatable`: add `bool IsTerminated { get; private set; }` property, add `Apply(CounterClosed e)` method that sets `IsTerminated = true`, add `Apply(AggregateTerminated e)` no-op method (required — rejection events are persisted to the event stream and replayed during rehydration; missing this Apply method causes rehydration to throw)
  - [x] 5.4 Add `Handle(CloseCounter, CounterState?)` to `CounterAggregate`: return `DomainResult.Success` with `CounterClosed` event. Do NOT add an `IsTerminated` check inside Handle — the `ProcessAsync` tombstoning guard already rejects commands before Handle is called, so a terminated check here would be dead code
  - [x] 5.5 Add tests in `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs`:
    - CloseCounter on active counter → produces CounterClosed event
    - Any command after CounterClosed → AggregateTerminated rejection (verify the tombstoning guard fires)
    - CounterClosed event stream is replayable (rehydrate state, verify IsTerminated == true)
- [x] Task 6: Build and run all Tier 1 tests (AC: #3, #4)
  - [x] 6.1 Verify `dotnet build Hexalith.EventStore.slnx --configuration Release` succeeds with zero warnings
  - [x] 6.2 Run ALL Tier 1 test projects (Contracts.Tests, Client.Tests, Sample.Tests, Testing.Tests) — all must pass

## Dev Notes

### Scope Summary

This is a two-part story: (1) audit CommandStatus enum completeness — already implemented, verify only; (2) implement aggregate tombstoning (FR66) — add `ITerminatable` interface, `AggregateTerminated` rejection event, tombstoning guard in `EventStoreAggregate`, and Counter sample demonstration.

### Existing Implementation State

| File | Status | Notes |
|------|--------|-------|
| `src/.../Commands/CommandStatus.cs` | Complete | 8 states with explicit int assignments |
| `src/.../Commands/CommandStatusRecord.cs` | Complete | Record with terminal-state fields |
| `src/.../Events/IRejectionEvent.cs` | Complete | Marker interface extending IEventPayload |
| `src/.../Results/DomainResult.cs` | Complete | Has `Rejection()` factory method |
| `src/.../Aggregates/EventStoreAggregate.cs` | **Needs update** | Add tombstoning guard in ProcessAsync |
| `samples/.../Counter/State/CounterState.cs` | **Needs update** | Implement ITerminatable |
| `samples/.../Counter/CounterAggregate.cs` | **Needs update** | Add Handle(CloseCounter) |

### Tombstoning Design (FR66)

**Pattern:** Opt-in via `ITerminatable` interface on aggregate state classes.

**Flow:**
1. Domain developer defines a terminal event (e.g., `CounterClosed`) implementing `IEventPayload`
2. State's `Apply(CounterClosed)` sets `IsTerminated = true`
3. Next command arrives → `EventStoreAggregate.ProcessAsync` rehydrates state → checks `state is ITerminatable { IsTerminated: true }` → returns `DomainResult.Rejection` with `AggregateTerminated`
4. Actor persists the rejection event, status transitions to Rejected
5. Event stream remains immutable — `CounterClosed` and `AggregateTerminated` are events like any other

**Why opt-in, not mandatory:** Not all aggregates need termination. Many aggregates (e.g., user profiles, tenant configurations) should never be terminated. `ITerminatable` is zero-cost for aggregates that don't implement it — the `is ITerminatable` pattern check is a no-op when the interface isn't implemented.

**Where the check goes:** In `EventStoreAggregate.ProcessAsync` after `RehydrateState` and before `DispatchCommandAsync`. This ensures:
- Check happens AFTER full state rehydration (including snapshot + replay)
- Check happens BEFORE command dispatch (no Handle method invoked for terminated aggregates)
- Rejection is a DomainResult, not an exception (D3: errors as events)

**Null state safety:** On first command (no prior events), `state` is null. The pattern `state is ITerminatable { IsTerminated: true }` evaluates to `false` for null — no special null handling needed. This is safe by design.

**Replayability guarantee:** Tombstoning does NOT modify or delete events. The terminal event (`CounterClosed`) and any subsequent rejection events (`AggregateTerminated`) are persisted like any other event. Full event stream replay reconstructs `IsTerminated = true` state correctly. An explicit rehydration roundtrip test in Task 5.5 verifies this.

**AggregateTerminated rejection event:** Framework-level rejection, not domain-specific. Includes `AggregateType` (class name, not kebab — diagnostic context only, not used for routing) and `AggregateId` for diagnostic context. Implements `IRejectionEvent` so the existing rejection path handles it automatically.

**CRITICAL — Apply(AggregateTerminated) obligation:** Because rejection events are persisted to the event stream (D3: "Rejection persistence: Persisted to event stream like any other event"), and rehydration replays ALL events, any state class implementing `ITerminatable` MUST also have a no-op `Apply(AggregateTerminated e) { }` method. Without it, rehydration throws `InvalidOperationException` ("No matching Apply method found") on the second command after tombstoning. This is not a design choice — it's a constraint of the event sourcing model (append-only stream + full replay = all events need Apply methods).

**Snapshot safety:** Snapshots capture state at a point in time. The actor rehydrates from snapshot + subsequent events. If the terminal event occurs after the snapshot, replay applies it and sets `IsTerminated = true`. The tombstoning guard works correctly regardless of snapshot timing. Schema evolution is also safe: a snapshot taken before `ITerminatable` was added deserializes with `IsTerminated = false` (C# default), and replay of subsequent events corrects it.

**Irreversibility:** Tombstoning is a one-way door by design. Once a terminal event is in the append-only stream (Rule 11), `IsTerminated` is permanently true. There is no "un-terminate." If a developer needs reversible deactivation (suspend/resume), they should use a separate state flag (e.g., `IsSuspended`), not `ITerminatable`.

**Multiple terminal events:** If an aggregate defines more than one terminal event type (e.g., `CounterClosed` and `CounterArchived`), only the first one applied is a state-change event. Any subsequent terminal commands are rejected by the guard because `IsTerminated` is already true after the first.

**Repeated rejections:** Each command sent to a tombstoned aggregate produces a persisted `AggregateTerminated` rejection event. This is correct per D3 (full audit trail). At high volumes this grows the event stream — a future optimization could short-circuit repeated rejections at the actor level, but that is out of scope for this story.

**Handle(CloseCounter, null) semantics:** Closing an aggregate that has never received any events is intentionally allowed. This follows idempotent command semantics — the `CloseCounter` handler does not validate prior state. The tombstoning guard in `ProcessAsync` prevents re-closing (already-terminated aggregates are rejected before Handle is reached).

### Known Risks & Follow-Up Items

**CRITICAL — Apply(AggregateTerminated) Obligation is Runtime-Only**

The requirement that every `ITerminatable` state MUST provide a no-op `Apply(AggregateTerminated)` method is enforced only by XML documentation and a runtime `InvalidOperationException` during event replay. A developer implementing `ITerminatable` for a new domain will pass all obvious tests (close works, first rejection works) but fail on actor reactivation when the replay path hits `Apply(AggregateTerminated)` with no matching method.

Failure scenario: Terminal event applied -> actor deactivates -> actor reactivates -> rehydration replays all events including persisted `AggregateTerminated` -> missing Apply method -> `InvalidOperationException` -> actor permanently stuck in fault loop.

**Recommended mitigations (future stories):**
1. Add `AssertTerminatableCompliance<TState>()` test helper to the Testing package — uses reflection to verify `Apply(AggregateTerminated)` exists on any state class implementing `ITerminatable`. New domain teams include this assertion in their test suite
2. Consider a custom `MissingApplyMethodException` (instead of generic `InvalidOperationException`) during replay for better diagnostics and alerting
3. Add a single-event `DomainResult.Rejection(IRejectionEvent e)` overload to avoid array allocation for the common single-rejection case

**Tracked for Story 2.x (Tier 2 scope):**
- Actor-lifecycle tombstoning test: deactivate -> reactivate -> rehydrate with a tombstoned aggregate. This is the highest-risk untested path but requires DAPR (Tier 2), so it is out of Story 1.5 Tier 1 scope

### Architecture Constraints

- **D3:** Domain errors as events. Tombstoning rejection is a `DomainResult.Rejection`, not an exception
- **FR66:** Terminal event marks aggregate terminated; subsequent commands rejected with IRejectionEvent
- **Rule 8:** Events named in past tense. `AggregateTerminated`, `CounterClosed` follow this
- **Rule 11:** Event store keys write-once. Tombstoning NEVER deletes or modifies existing events
- **UX-DR19:** XML documentation on all public types
- **UX-DR20:** Only domain-service-developer-facing types public. `ITerminatable` is developer-facing (public). `AggregateTerminated` is developer-visible (public)
- **SEC-1:** EventStore owns metadata. Tombstoning guard runs inside the aggregate, so no metadata concerns

### Previous Story Intelligence (Story 1.4)

Story 1.4 implemented `EventStoreAggregate<TState>` with reflection-based Handle/Apply discovery and `IDomainProcessor` interface. Key learnings:
- `EventStoreAggregate.ProcessAsync` is the entry point: rehydrate state → dispatch command
- The state type `TState` must be `class, new()` — this is compatible with adding `ITerminatable`
- Apply methods are void methods on TState — `Apply(CounterClosed)` follows this pattern
- Assertions use `Assert.Equal` / `Assert.Throws` in Client.Tests (xUnit, no Shouldly)
- Allman brace style per `.editorconfig`

### File Location Conventions

- **Contracts interfaces:** `src/Hexalith.EventStore.Contracts/Aggregates/` (create directory if needed)
- **Contracts events:** `src/Hexalith.EventStore.Contracts/Events/`
- **Client aggregates:** `src/Hexalith.EventStore.Client/Aggregates/`
- **Sample commands:** `samples/Hexalith.EventStore.Sample/Counter/Commands/`
- **Sample events:** `samples/Hexalith.EventStore.Sample/Counter/Events/`
- **Sample state:** `samples/Hexalith.EventStore.Sample/Counter/State/`
- **Contract tests:** `tests/Hexalith.EventStore.Contracts.Tests/` (create subdirectories as needed)
- **Client tests:** `tests/Hexalith.EventStore.Client.Tests/Aggregates/`
- **Sample tests:** `tests/Hexalith.EventStore.Sample.Tests/Counter/`

### Standards

- **Braces:** Allman style per `.editorconfig` (new line before opening brace). `.editorconfig` is the authority
- **Tests:** `Assert.Equal` / `Assert.Throws` (xUnit) in Client.Tests. Don't mix Shouldly
- **Run:** `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` + `dotnet test tests/Hexalith.EventStore.Client.Tests/` + `dotnet test tests/Hexalith.EventStore.Sample.Tests/` + `dotnet test tests/Hexalith.EventStore.Testing.Tests/`

### Project Structure Notes

- `ITerminatable` goes in Contracts (not Client) so it's part of the shared type system that domain developers reference
- `AggregateTerminated` goes in Contracts/Events alongside `IRejectionEvent`
- The tombstoning guard in `EventStoreAggregate` (Client project) references Contracts — no circular dependency since Client already references Contracts
- Counter sample demonstrates the full pattern for documentation/onboarding purposes

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 1, Story 1.5]
- [Source: _bmad-output/planning-artifacts/architecture.md — D2, D3, FR66, Rule 8, Rule 11, UX-DR16, UX-DR19, UX-DR20]
- [Source: _bmad-output/planning-artifacts/prd.md — FR66]
- [Source: src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs — complete 8-state enum]
- [Source: src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs — complete record]
- [Source: src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs — ProcessAsync entry point for tombstoning guard]
- [Source: samples/Hexalith.EventStore.Sample/Counter/ — CounterAggregate, CounterState patterns]
- [Source: _bmad-output/implementation-artifacts/1-4-pure-function-contract-and-eventstoreaggregate-base.md — Story 1.4 patterns and learnings]

## Dev Agent Record

## Senior Developer Review (AI)

### Review Date

2026-03-15

### Reviewer

GitHub Copilot (GPT-5.4)

### Findings Summary

- **FIXED:** Added explicit terminal-status verification to [tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs](tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs#L38), covering `Completed`, `Rejected`, `PublishFailed`, and `TimedOut` as required by Task 1.3.

### Git vs Story Discrepancies

- Current workspace diffs are unrelated to Story 1.5. No actionable discrepancy was found between the Story 1.5 file list and the reviewed implementation; the story appears to be reviewing already-committed code rather than active uncommitted changes.

### Acceptance Criteria Validation

- **AC1:** Implemented. The enum has the required 8 states, XML documentation is present in [src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs](src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs#L1) and [src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs](src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs#L1), and dedicated tests now cover terminal statuses in [tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs](tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs#L38).
- **AC2:** Implemented. Tombstoning is enforced in [src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs](src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs#L44), the contracts are present in [src/Hexalith.EventStore.Contracts/Aggregates/ITerminatable.cs](src/Hexalith.EventStore.Contracts/Aggregates/ITerminatable.cs#L1) and [src/Hexalith.EventStore.Contracts/Events/AggregateTerminated.cs](src/Hexalith.EventStore.Contracts/Events/AggregateTerminated.cs#L1), and the sample demonstrates replayability in [tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs](tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs#L88).
- **AC3:** Implemented. Tier 1 projects passed during review: Contracts 266, Client 286, Sample 32, Testing 67.
- **AC4:** Implemented. The code artifacts listed in the done definition are present, and the remaining verification gap for Task 1.3 is now closed.

### Outcome

- Review result: **Approved after fixes**
- Remaining CRITICAL issues: **0**
- Remaining MEDIUM issues: **0**

### Agent Model Used
Claude Opus 4.6 (1M context)

### Debug Log References
- Build: 0 warnings, 0 errors (Release)
- Contracts.Tests: 267 passed (added terminal-status verification in CommandStatusTests)
- Client.Tests: 286 passed (4 new tombstoning guard tests)
- Sample.Tests: 32 passed (3 new tombstoning demo tests)
- Testing.Tests: 67 passed (no changes)

### Completion Notes List
- Task 1: CommandStatus enum verified complete — 8 states with correct integer assignments, XML docs, and dedicated terminal-status coverage in `CommandStatusTests`
- Task 2: Created `ITerminatable` interface in `Contracts.Aggregates` with `bool IsTerminated` property and `<remarks>` warning about Apply(AggregateTerminated) obligation
- Task 3: Created `AggregateTerminated` sealed record implementing `IRejectionEvent` with `AggregateType` and `AggregateId` properties
- Task 4: Added tombstoning guard in `EventStoreAggregate.ProcessAsync` — checks `state is ITerminatable { IsTerminated: true }` after rehydration, before dispatch. Returns `DomainResult.Rejection` with `AggregateTerminated`
- Task 5: Counter sample demonstrates full tombstoning lifecycle — `CloseCounter` command, `CounterClosed` terminal event, `CounterState` implements `ITerminatable` with Apply(CounterClosed) and no-op Apply(AggregateTerminated)
- Task 6: Full solution builds with zero warnings, all 648 Tier 1 tests pass

### Review Findings (Advanced Elicitation Round 1 — 2026-03-15)
- **Critical:** Apply(AggregateTerminated) obligation is runtime-only — recommended Testing package helper for compile-time/test-time enforcement
- **Fixed:** Brace style guidance corrected from "Egyptian/K&R" to "Allman" per `.editorconfig`
- **Documented:** Handle(CloseCounter, null) idempotent semantics made explicit
- **Tracked:** Actor-lifecycle tombstoning test flagged for Story 2.x (Tier 2 scope)
- **Suggested:** Single-event DomainResult.Rejection overload, custom MissingApplyMethodException

### Review Findings (Advanced Elicitation Round 2 — 2026-03-15)

Analysis of the Copilot (GPT-5.4) review finding re: terminal status test coverage:

- **Finding:** The original review said `CommandStatusTests.cs` "only asserts enum count, explicit values, and order." However, `CommandStatus_TerminalStatuses_AreIdentifiedCorrectly` at line 39 already verified the terminal subset (4 statuses, exclusion of non-terminals, `>= Completed` convention). The review referenced `#L6` (class declaration) and missed the 4th test method — likely an LLM attention truncation
- **Resolved:** The review was subsequently updated to "Approved after fixes" with 0 critical issues
- **Tracked for Story 2.4:** Add `CommandStatus.IsTerminal()` extension method for production use by `CommandStatusWriter` — the current `>= Completed` convention is tested but not shipped as an API surface
- **Process improvement:** Future CRITICAL review findings should include a verification command (e.g., `grep -n "Terminal" CommandStatusTests.cs`) so the developer can confirm before accepting a status change. False-positive CRITICAL findings waste developer cycles

### Change Log
- 2026-03-15: Story 1.5 implementation complete — CommandStatus verified, aggregate tombstoning implemented (FR66)
- 2026-03-15: Senior developer review completed; added missing terminal-status verification and returned story to done
- 2026-03-15: Advanced elicitation round 1 — added Known Risks section, fixed style inconsistency, documented idempotent close semantics
- 2026-03-15: Advanced elicitation round 2 — analyzed Copilot review finding (terminal status test already existed at line 39), added process improvement recommendation, tracked IsTerminal() extension for Story 2.4

### File List
- src/Hexalith.EventStore.Contracts/Aggregates/ITerminatable.cs (new)
- src/Hexalith.EventStore.Contracts/Events/AggregateTerminated.cs (new)
- src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs (modified — tombstoning guard)
- samples/Hexalith.EventStore.Sample/Counter/Events/CounterClosed.cs (new)
- samples/Hexalith.EventStore.Sample/Counter/Commands/CloseCounter.cs (new)
- samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs (modified — ITerminatable)
- samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs (modified — Handle(CloseCounter))
- tests/Hexalith.EventStore.Contracts.Tests/Commands/CommandStatusTests.cs (modified — terminal-status verification)
- tests/Hexalith.EventStore.Contracts.Tests/Aggregates/ITerminatableTests.cs (new)
- tests/Hexalith.EventStore.Contracts.Tests/Events/AggregateTerminatedTests.cs (new)
- tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs (modified — 4 tombstoning tests)
- tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs (modified — 3 tombstoning tests)
- _bmad-output/implementation-artifacts/1-5-commandstatus-enum-and-aggregate-tombstoning.md (modified — senior developer review)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified — status sync)
