# Sprint Change Proposal — Epic 1 Retrospective Carry-Over Fixes

**Date:** 2026-04-26
**Author:** Bob (Scrum Master)
**Project Lead:** Jerome
**Trigger:** Epic 1 retrospective (`epic-1-retro-2026-04-26.md`) action items R1-A1, R1-A2, R1-A6, R1-A7
**Mode:** Batch
**Scope Classification:** **Minor** — direct dev implementation; no replan, no PRD/PM escalation.

---

## 1. Issue Summary

Epic 1 retrospective on 2026-04-26 surfaced 8 action items. Four are flagged High priority and unimplemented in the codebase. Verification ran today against `main`:

| ID | Symptom | Verified location |
|----|---------|--------------------|
| R1-A1 | `EventPersister` and `FakeEventPersister` populate `EventEnvelope.AggregateType` from `identity.Domain` instead of the real aggregate type. Per Story 1.1 dev notes, Domain (e.g., `counter-domain`) is the bounded context; AggregateType (e.g., `counter`) is the specific aggregate within it. They are intentionally different fields | `src/Hexalith.EventStore.Server/Events/EventPersister.cs:83`<br>`src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs:61` |
| R1-A2 | No test helper exists to verify that any `ITerminatable` state class also defines `Apply(AggregateTerminated)`. Domain teams will only discover the obligation when the actor reactivates and rehydration throws | absent from `src/Hexalith.EventStore.Testing/` |
| R1-A6 | Replay path throws generic `InvalidOperationException` for missing-Apply-method scenarios, mixed with all other rehydration errors. No discriminator. Operators cannot alert on the specific tombstoning fault loop described in Story 1.5 dev notes | `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs` (8+ throw sites all using `InvalidOperationException`) |
| R1-A7 | No Tier 2 test exercises the actor-lifecycle path: terminate → deactivate → reactivate → rehydrate. This is the failure mode flagged in Story 1.5 as the highest-risk untested path (CRITICAL in retrospective Section 5, D1-2) | `tests/Hexalith.EventStore.Server.Tests/` — grep for `tombston`, `Terminat.*Lifecycle` returns zero hits |

**Why now.** Both Epic 1 and Epic 2 are closed (`done`). Epic 1 carry-overs were intended to land in Epic 2 implementation but didn't. Carrying further raises latent fault-loop risk for any new domain team building on the framework, and leaves the `AggregateType` field semantically wrong on every persisted event (silent data quality issue affecting downstream observability, projections, and admin UI).

---

## 2. Impact Analysis

### Epic Impact

- **Epic 1** (`done`): retrospective already records these as carry-overs (D1-1, D1-2). No reopen.
- **Epic 2** (`done`): retrospective optional/never run. Epic 2 should have absorbed these — it didn't. No reopen; we land them as standalone post-epic fix stories.
- **Future epics**: no impact — the changes are additive and bug-fixing; no contract breaks.

### Story Impact

Four standalone fix stories proposed under a new banner **"Post-Epic-1 Retro Cleanup"**. No existing story is reopened or amended.

| New Story Key | Title | Owner | Sequence |
|---------------|-------|-------|----------|
| `post-epic-1-r1a1-aggregatetype-pipeline` | Thread real AggregateType through persistence pipeline | Dev | 1 (architectural; unblocks data quality) |
| `post-epic-1-r1a6-missing-apply-method-exception` | Custom MissingApplyMethodException for replay diagnostics | Dev | 2 (independent, prereq for R1-A2 messaging) |
| `post-epic-1-r1a2-terminatable-compliance-helper` | AssertTerminatableCompliance<TState> test helper in Testing package | Dev | 3 (uses R1-A6 exception in failure path) |
| `post-epic-1-r1a7-tier2-tombstoning-lifecycle` | Tier 2 actor-lifecycle tombstoning test | Dev | 4 (validates R1-A1, R1-A2, R1-A6 end-to-end) |

### Artifact Conflicts

- **Architecture (`architecture.md`):** R1-A1 corrects an implementation drift. The architecture *already* specifies AggregateType ≠ Domain — no architecture text change needed. Add a one-paragraph clarifying note in §Data Flow / §Event Persistence noting that AggregateType is supplied by the AggregateActor (registered kebab name from `KebabConverter`, Rule 17), not derived from Domain.
- **PRD:** No change. FR11 (15-field metadata) and FR66 (tombstoning) are already covered.
- **UX Design:** No change.
- **Sprint-status.yaml:** Add 4 new keys for the post-epic fix stories (status `backlog`, transitioning as work proceeds).

### Technical Impact

| Item | Code | Tests | Migration | Risk |
|------|------|-------|-----------|------|
| R1-A1 | `IEventPersister.PersistEventsAsync` adds `string aggregateType` param. `AggregateActor` (line 365) passes registered aggregate type (already known via DAPR actor registration). `EventPersister` and `FakeEventPersister` use the new param instead of `identity.Domain`. ~10 file edits | Existing Server.Tests construction sites for `EventPersister`/`FakeEventPersister` need updated calls with the new param. ~15 test edits | None — pre-release; existing event store contents from dev DAPR can be wiped via `dapr init --slim` | Low (mechanical, named args, contract is internal `IEventPersister`) |
| R1-A2 | New file: `src/Hexalith.EventStore.Testing/Compliance/TerminatableComplianceAssertions.cs` (or `Assertions/`). Uses reflection: if `typeof(TState)` implements `ITerminatable`, assert it has a public instance `Apply(AggregateTerminated)` returning `void` | New tests in `Hexalith.EventStore.Testing.Tests` covering: passing case (CounterState), missing-method case (negative test using a deliberately broken state class). Counter sample test calls the helper | None | Trivial |
| R1-A6 | New file: `src/Hexalith.EventStore.Client/Aggregates/MissingApplyMethodException.cs` — sealed exception with state type + event type + message id context. Replace 3 specific throw sites in `DomainProcessorStateRehydrator.cs` (the ones that fire when the dispatch dictionary lookup fails) with `MissingApplyMethodException`. Other 5+ `InvalidOperationException` throws stay (they are JSON / shape errors, not Apply-method errors) | Existing rehydration tests assert the existing exception type — update to assert `MissingApplyMethodException` for the missing-Apply-method scenarios. New tests verify the exception payload (StateType, EventType, ToString()) | None — exception is new public API; no callers depend on the old type for the Apply-missing case | Low (new exception type, no deletes) |
| R1-A7 | None (test-only) | New Tier 2 test class `Server.Tests/Actors/TombstoningLifecycleTests.cs`: deactivate via `ActorRuntime`/test fixture → reactivate → rehydrate → assert (a) `IsTerminated == true`, (b) further command produces `AggregateTerminated`, (c) `R1-A6` exception NOT thrown (because Counter's `Apply(AggregateTerminated)` no-op exists) | None | Low — Tier 2 already runs in CI after `dapr init`; one more test class |

---

## 3. Recommended Approach

**Direct adjustment.** Four small standalone fix stories. Implementation order matters — see Detailed Change Proposals §4.

**Rationale.** All four items are surgical. None require architectural re-think (the architecture is correct; the implementation drifted). Reopening Epic 1 or Epic 2 would add ceremony with no benefit since their retrospectives are already recorded. Standalone post-epic fix stories preserve the audit trail (each fix references R1-A_) and let the four items be merged independently.

**Sequencing.** R1-A1 first because it touches a public-internal contract (`IEventPersister`) that other fixes don't depend on but its blast radius is the largest, so it sets a stable foundation. R1-A6 next because R1-A2's failure-message ergonomics use the new exception. R1-A2 third. R1-A7 last because it validates all three end-to-end.

**Risk assessment.** Cumulative risk: **Low**. Pre-release; Tier 1 ratchet (651 tests) catches regressions; named-argument discipline already enforced; no migration.

**Effort.** Per-story: small. Total: roughly one focused implementation session — explicitly avoiding time estimates per BMad retro guidance.

---

## 4. Detailed Change Proposals

### Proposal 1 — `post-epic-1-r1a1-aggregatetype-pipeline`

**Problem.** `EventEnvelope.AggregateType` is silently wrong on every persisted event. Per Story 1.1 dev notes, Domain (e.g., `counter-domain`) and AggregateType (e.g., `counter`) are intentionally separate fields; the implementation populates both from `identity.Domain`.

**Edits:**

```
File: src/Hexalith.EventStore.Server/Events/IEventPersister.cs
Section: PersistEventsAsync signature

OLD:
Task<EventPersistResult> PersistEventsAsync(
    AggregateIdentity identity,
    CommandEnvelope command,
    DomainResult domainResult,
    string domainServiceVersion);

NEW:
Task<EventPersistResult> PersistEventsAsync(
    AggregateIdentity identity,
    string aggregateType,
    CommandEnvelope command,
    DomainResult domainResult,
    string domainServiceVersion);

Rationale: Caller (AggregateActor) knows the registered aggregate type
(kebab from KebabConverter, Rule 17). Persister cannot derive it from
identity. Adding the parameter explicit at the boundary keeps the
SEC-1 ownership contract clean.
```

```
File: src/Hexalith.EventStore.Server/Events/EventPersister.cs:83
Section: EventEnvelope construction

OLD:
AggregateType: identity.Domain,

NEW:
AggregateType: aggregateType,

Rationale: Use the parameter passed by the caller; stop conflating with Domain.
```

```
File: src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs:61
Section: EventEnvelope construction

Apply the same change. Update FakeEventPersister to accept the same
new parameter on its PersistEventsAsync method.
```

```
File: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:365
Section: PersistEventsAsync call

OLD:
.PersistEventsAsync(command.AggregateIdentity, command, domainResult, domainServiceVersion)

NEW:
.PersistEventsAsync(command.AggregateIdentity, registeredAggregateType, command, domainResult, domainServiceVersion)

Rationale: Pass the actor's registered aggregate type. Source: the same
NamingConventionEngine value used at registration time. If the actor doesn't
already have it cached, fetch it from the EventStoreActivationContext at
actor construction.
```

```
File: tests/Hexalith.EventStore.Server.Tests/** (~15 sites)
Update all direct EventPersister/FakeEventPersister construction and
PersistEventsAsync call sites to pass the new aggregateType parameter
using named arguments.

Rationale: Enforce the named-argument discipline established in Story 1.1.
```

**Acceptance criteria:**
1. `IEventPersister.PersistEventsAsync` accepts `string aggregateType`; non-empty validation at the boundary.
2. `EventEnvelope.AggregateType` on persisted events equals the AggregateActor's registered aggregate type (e.g., `counter`), not the Domain (e.g., `counter-domain`).
3. New Tier 1 test: `EventPersisterTests.PersistEventsAsync_PopulatesAggregateTypeFromParameter_NotFromDomain`.
4. New Tier 2 test: `AggregateActorIntegrationTests.PersistedEventsCarryRegisteredAggregateType` — runs Counter, asserts `envelope.AggregateType == "counter"`.
5. All 651+ Tier 1 tests green; full Release build 0/0.

---

### Proposal 2 — `post-epic-1-r1a6-missing-apply-method-exception`

**Problem.** When rehydration replays an event whose type has no matching `Apply(TEvent)` method on the state class, the rehydrator throws generic `InvalidOperationException`. Operators cannot distinguish this from JSON / shape errors. The Story 1.5 fault-loop scenario (`ITerminatable` state missing `Apply(AggregateTerminated)`) hits this path silently.

**Edits:**

```
File (new): src/Hexalith.EventStore.Client/Aggregates/MissingApplyMethodException.cs

[Serializable]
public sealed class MissingApplyMethodException : InvalidOperationException
{
    public Type StateType { get; }
    public string EventTypeName { get; }
    public string? MessageId { get; }
    public string? AggregateId { get; }

    public MissingApplyMethodException(
        Type stateType,
        string eventTypeName,
        string? messageId = null,
        string? aggregateId = null)
        : base($"State type '{stateType.Name}' has no Apply method " +
               $"for event type '{eventTypeName}'. " +
               (stateType.GetInterfaces().Any(i => i.Name == "ITerminatable")
                  ? "Note: ITerminatable states must define a no-op " +
                    "Apply(AggregateTerminated) — see Story 1.5 dev notes."
                  : "Verify the state class declares a public " +
                    "void Apply(EventType e) method for every event type " +
                    "produced by Handle methods."))
    { ... }
}

Rationale: Subclass InvalidOperationException so existing catch blocks
still work, but provide the discriminator + structured payload for
operators and the AssertTerminatableCompliance helper to consume.
```

```
File: src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs
Section: replay paths (3 specific throw sites)

For each throw site that fires when applyMethods.TryGetValue(eventTypeName, out _) returns false:

OLD:
throw new InvalidOperationException(
    string.Format(CultureInfo.InvariantCulture,
        "No Apply method found on state '{0}' for event type '{1}'.",
        typeof(TState).Name, eventTypeName));

NEW:
throw new MissingApplyMethodException(
    stateType: typeof(TState),
    eventTypeName: eventTypeName);

Rationale: Specific exception for specific failure mode.
The other ~5 InvalidOperationException throws (JSON shape errors,
deserialization nulls) stay as-is — different failure mode.
```

**Acceptance criteria:**
1. `MissingApplyMethodException` exists in `Hexalith.EventStore.Client.Aggregates`.
2. Rehydrator throws it at the 3 specific Apply-lookup-miss sites; other throw sites unchanged.
3. New Client.Tests: `MissingApplyMethodExceptionTests` — verify constructor, properties, message text, ITerminatable hint conditional.
4. Existing rehydration tests updated where they catch the missing-Apply scenario — assert specific exception type.
5. XML doc on the new exception complete (UX-DR19).

---

### Proposal 3 — `post-epic-1-r1a2-terminatable-compliance-helper`

**Problem.** Story 1.5 design relies on a runtime-only constraint: every state class implementing `ITerminatable` must define `Apply(AggregateTerminated)`. Domain teams discover this only when their actor reactivates and rehydration throws.

**Edits:**

```
File (new): src/Hexalith.EventStore.Testing/Compliance/TerminatableComplianceAssertions.cs

public static class TerminatableComplianceAssertions
{
    public static void AssertTerminatableCompliance<TState>()
        where TState : class
    {
        var stateType = typeof(TState);
        if (!typeof(ITerminatable).IsAssignableFrom(stateType))
            return;  // not terminatable — nothing to enforce

        var applyMethod = stateType.GetMethod(
            "Apply",
            BindingFlags.Public | BindingFlags.Instance,
            new[] { typeof(AggregateTerminated) });

        if (applyMethod is null || applyMethod.ReturnType != typeof(void))
            throw new MissingApplyMethodException(
                stateType,
                eventTypeName: nameof(AggregateTerminated));
    }
}

Rationale: Reuses MissingApplyMethodException from R1-A6 so the failure
message ergonomics are consistent between test-time and runtime.
```

```
File (new): tests/Hexalith.EventStore.Testing.Tests/Compliance/TerminatableComplianceAssertionsTests.cs

Tests:
- Compliant ITerminatable state (with no-op Apply(AggregateTerminated)) -> passes
- ITerminatable state missing the Apply method -> throws MissingApplyMethodException
- Non-ITerminatable state -> passes (no enforcement)
- ITerminatable state with non-void Apply(AggregateTerminated) -> throws (return type check)
```

```
File: tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs
Add one test:

[Fact]
public void CounterState_IsTerminatableCompliant()
    => TerminatableComplianceAssertions.AssertTerminatableCompliance<CounterState>();

Rationale: Pinning test that catches accidental removal of Counter's
no-op Apply(AggregateTerminated). Doubles as documentation: any new
domain team copying Counter's pattern sees this assertion and learns
the contract.
```

**Acceptance criteria:**
1. `TerminatableComplianceAssertions.AssertTerminatableCompliance<TState>()` available in `Hexalith.EventStore.Testing`.
2. 4 unit tests covering compliant / missing / non-terminatable / wrong-return-type cases.
3. CounterState pinning test added to `Hexalith.EventStore.Sample.Tests`.
4. Public API XML docs complete; class-level remarks reference Story 1.5 design.

---

### Proposal 4 — `post-epic-1-r1a7-tier2-tombstoning-lifecycle`

**Problem.** No Tier 2 test exercises actor lifecycle with a tombstoned aggregate. This is the path most likely to fault-loop in production: the actor's first close is fine; the failure happens only after deactivation + reactivation when the persisted `AggregateTerminated` rejection event replays through `Apply`.

**Edits:**

```
File (new): tests/Hexalith.EventStore.Server.Tests/Actors/TombstoningLifecycleTests.cs

Test scenarios (Tier 2 — requires `dapr init`):

1. Terminate_Then_Deactivate_Then_Reactivate_Rehydrates_AsTerminated
   - Send CloseCounter to actor -> CounterClosed persisted
   - Force actor deactivation
   - Send any subsequent command -> actor reactivates
   - Assert state.IsTerminated == true after rehydration
   - Assert subsequent command produces AggregateTerminated rejection

2. Terminate_Then_Reactivate_Then_TerminateAgain_RejectsCleanly
   - Verify repeated rejections after reactivation don't fault-loop
   - Each rejection appends new AggregateTerminated event to stream
   - State remains rehydratable (no MissingApplyMethodException)

3. Terminate_Then_Snapshot_Then_Reactivate_Rehydrates_AsTerminated
   - Force snapshot AFTER terminal event
   - Reactivate; rehydration starts from snapshot + 0 events
   - Assert IsTerminated == true (snapshot preserved the flag)

4. Terminate_Then_Snapshot_Before_Terminal_Then_Reactivate
   - Snapshot BEFORE CounterClosed (mid-life snapshot)
   - Apply CounterClosed
   - Reactivate; rehydration replays CounterClosed from event stream after snapshot
   - Assert IsTerminated == true

Rationale: Covers all four state recovery paths Story 1.5 dev notes
flagged as snapshot/replay-safety concerns.
```

**Acceptance criteria:**
1. New test class with 4 scenarios in `tests/Hexalith.EventStore.Server.Tests/Actors/`.
2. Tests pass after `dapr init` against the standard CI test harness.
3. Each test asserts no `MissingApplyMethodException` is thrown (i.e., R1-A2 compliance helper would also pass for CounterState in production replay).
4. Tier 2 test execution time stays within the existing budget; if it doesn't, mark as `[Trait("Category", "LongRunning")]` with rationale.

---

## 5. Implementation Handoff

**Scope:** Minor — direct dev implementation.
**Owner:** Dev (single team).
**Recipients:** Implementation team (whoever picks up Post-Epic-1 cleanup).
**Order:** R1-A1 → R1-A6 → R1-A2 → R1-A7 (see §3 sequencing rationale).

**Deliverables on completion:**
- 4 merged commits / PRs (one per fix story), each with conventional commit prefix matching content (`refactor(server):`, `feat(client):`, `feat(testing):`, `test(server):`).
- All Tier 1 tests green; Tier 2 tests green after `dapr init`.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` updated: 4 new keys move `backlog → in-progress → done` as work proceeds.
- Update `epic-1-retro-2026-04-26.md` action-item table: mark R1-A1, R1-A2, R1-A6, R1-A7 as ✅ completed with the merge commit reference.

**Success criteria:**
1. `EventEnvelope.AggregateType` on persisted events is correct (kebab from `KebabConverter`, not Domain).
2. Domain teams adding `ITerminatable` to a new aggregate fail their Tier 1 tests immediately if they forget `Apply(AggregateTerminated)` — no production fault loops.
3. Operators can alert on `MissingApplyMethodException` specifically; not buried under generic `InvalidOperationException`.
4. CI catches actor-lifecycle tombstoning regressions automatically.

**Out of scope (carried forward):**
- R1-A3: enable XML doc generation on remaining 5 packages — standalone follow-up.
- R1-A4: `CommandStatus.IsTerminal()` extension — folds into Story 2.4 work area (already closed; needs its own follow-up).
- R1-A5: single-event `DomainResult.Rejection(IRejectionEvent e)` overload — low priority.
- R1-A8: code-review process change — informal documentation only.

---

## 6. Approval

This proposal is ready for Jerome's approval. On approval:
1. Add the 4 story keys to `sprint-status.yaml` with status `backlog`.
2. Stories execute in the order above.
3. Post-merge, mark each retro action item as completed in `epic-1-retro-2026-04-26.md`.
