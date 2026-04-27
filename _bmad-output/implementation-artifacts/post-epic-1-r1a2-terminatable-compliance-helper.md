# Story Post-Epic-1 R1-A2: AssertTerminatableCompliance Test Helper in Testing Package

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a domain framework consumer adding `ITerminatable` to a new aggregate state class,
I want a Tier 1 test helper that proves my state declares the required `Apply(AggregateTerminated)` method,
so that the runtime-only tombstoning replay obligation fails fast at test time instead of fault-looping when an actor reactivates.

## Acceptance Criteria

1. `Hexalith.EventStore.Testing.Compliance.TerminatableComplianceAssertions` static class exposes a public generic method `AssertTerminatableCompliance<TState>()` constrained to `where TState : class`.

2. When `typeof(TState)` does not implement `ITerminatable`, the helper returns silently (no exception, no enforcement) so non-terminatable aggregates can call it without ceremony.

3. When `typeof(TState)` implements `ITerminatable` and declares a public instance `void Apply(AggregateTerminated)` method, the helper returns silently.

4. When `typeof(TState)` implements `ITerminatable` and is missing a matching `Apply(AggregateTerminated)` method (no public instance method, wrong return type, or wrong parameter type), the helper throws `MissingApplyMethodException` (the existing R1-A6 type from `Hexalith.EventStore.Client.Aggregates`).

5. The thrown `MissingApplyMethodException` carries `StateType == typeof(TState)` and `EventTypeName == nameof(AggregateTerminated)`, and its message surfaces the `ITerminatable` tombstoning hint already produced by `MissingApplyMethodException.BuildMessage` for terminatable states.

6. Method discovery rules match `DomainProcessorStateRehydrator.DiscoverApplyMethods`: public + instance only, exact name `Apply`, exactly one parameter of type `AggregateTerminated`, return type exactly `void`. Static, non-public, and `ref`/`out` parameter variants do not satisfy the contract. Apply methods declared on a base class **do** satisfy the contract — the helper inherits the rehydrator's behaviour of walking inherited public instance methods. (Generic `Apply<T>(AggregateTerminated)` declarations are not enforced — they are syntactically legal but uncallable in practice and not a realistic failure mode worth testing against.)

7. Tier 1 unit tests in `Hexalith.EventStore.Testing.Tests` cover five cases at minimum: compliant `ITerminatable` state passes; non-`ITerminatable` state passes; `ITerminatable` state with **no** Apply methods at all throws; `ITerminatable` state with **other** Apply methods present but no `Apply(AggregateTerminated)` throws (the realistic forgot-one-method failure mode that ships); `ITerminatable` state with non-`void` return-type `Apply(AggregateTerminated)` throws. Each negative test asserts the thrown exception is `MissingApplyMethodException` and verifies its `StateType` and `EventTypeName` payload.

8. A pinning test `CounterState_IsTerminatableCompliant` is added to `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs` that calls `TerminatableComplianceAssertions.AssertTerminatableCompliance<CounterState>()`. It must currently pass (Counter's no-op `Apply(AggregateTerminated)` is intact) and must fail compile or runtime if a future change deletes that method.

9. Public API XML documentation is complete on the new static class and method (UX-DR19). Class-level remarks reference the runtime-only constraint declared on `ITerminatable` itself (via `<see cref="ITerminatable"/>`) — not internal story IDs, which are opaque to downstream NuGet consumers. Build remains 0 warnings / 0 errors with `TreatWarningsAsErrors=true`.

10. All validation steps in Tasks 4.1-4.5 pass cleanly (build green, four Tier 1 suites green). Specific commands and expected counts live in those tasks — kept there to avoid duplicating the source of truth.

## Tasks / Subtasks

- [x] Task 1: Add the compliance helper to the Testing package (AC: #1, #2, #3, #4, #5, #6, #9)
  - [x] 1.1 Create folder `src/Hexalith.EventStore.Testing/Compliance/` (new — sibling to `Assertions/`, `Builders/`, `Fakes/`)
  - [x] 1.2 Add `src/Hexalith.EventStore.Testing/Compliance/TerminatableComplianceAssertions.cs` with namespace `Hexalith.EventStore.Testing.Compliance`
  - [x] 1.3 Implement `public static void AssertTerminatableCompliance<TState>() where TState : class`
  - [x] 1.4 Short-circuit when `typeof(ITerminatable).IsAssignableFrom(typeof(TState))` is `false`
  - [x] 1.5 Resolve the Apply method via `typeof(TState).GetMethod("Apply", BindingFlags.Public | BindingFlags.Instance, types: new[] { typeof(AggregateTerminated) })` (binder defaulting to null)
  - [x] 1.6 Treat `null` from `GetMethod` and `applyMethod.ReturnType != typeof(void)` as failures and throw `new MissingApplyMethodException(stateType: typeof(TState), eventTypeName: nameof(AggregateTerminated))`
  - [x] 1.7 Add XML docs on the class and method. Reference `<see cref="ITerminatable"/>` and `<see cref="AggregateTerminated"/>` (so downstream NuGet consumers can navigate via IDE — internal story IDs like "Story 1.5" do not survive the published-package boundary), explain the runtime-only constraint that motivates the helper, and link to `<see cref="MissingApplyMethodException"/>` for the failure ergonomics. Include the defensive-call recommendation: *"Recommended: call this from every aggregate state's primary test class, even when the state does not currently implement `ITerminatable`. The helper is a no-op for non-terminatable states, so the call activates automatically the moment the interface is later added."* Match the comment style and indentation used in existing Testing assertion files (`StorageKeyIsolationAssertions.cs`, `DomainResultAssertions.cs`)
  - [x] 1.8 Confirm no new package or project reference is required: Testing → Server → Client transitively brings `MissingApplyMethodException` into scope. Do not add a direct Client project reference

- [x] Task 2: Cover the helper with focused Tier 1 tests (AC: #4, #5, #6, #7)
  - [x] 2.1 Add `tests/Hexalith.EventStore.Testing.Tests/Compliance/TerminatableComplianceAssertionsTests.cs` with namespace `Hexalith.EventStore.Testing.Tests.Compliance`
  - [x] 2.2 Define test-only state types as `private sealed class` nested in the test class to keep them out of the public surface. Note: every fixture below uses `IsTerminated => false` arbitrarily — the helper ignores the property; only the Apply-method shape matters. Also declare a placeholder event record `private sealed record SomeOtherEvent : IEventPayload;` nested in the test class for use by the partially-broken fixture:
        - `CompliantTerminatableState : ITerminatable` with `IsTerminated => false` and a no-op `public void Apply(AggregateTerminated e) { }`
        - `EmptyBrokenTerminatableState : ITerminatable` with `IsTerminated => false` and **no** Apply methods at all
        - `PartiallyBrokenTerminatableState : ITerminatable` with `IsTerminated => false` and a no-op `public void Apply(SomeOtherEvent e) { }` (using the nested placeholder record above) but **no** `Apply(AggregateTerminated)` — this models the realistic "domain author wrote several Apply methods and forgot the one for AggregateTerminated" failure mode
        - `WrongReturnTypeTerminatableState : ITerminatable` with `IsTerminated => false` and `public bool Apply(AggregateTerminated _) => true;` — note the `_` parameter name to sidestep CA1801 / CS9113 / unused-parameter warnings under `TreatWarningsAsErrors`. If still flagged, add a localised `[SuppressMessage]` with rationale "test fixture intentionally violates the Apply contract"
        - `NonTerminatableState` not implementing `ITerminatable`
  - [x] 2.3 Tests:
        - `AssertTerminatableCompliance_CompliantState_DoesNotThrow`
        - `AssertTerminatableCompliance_NonTerminatableState_DoesNotThrow`
        - `AssertTerminatableCompliance_StateWithNoApplyMethods_ThrowsMissingApplyMethodException` (assert `StateType` and `EventTypeName == nameof(AggregateTerminated)`)
        - `AssertTerminatableCompliance_StateMissingOnlyTerminatedApply_StillThrows` (the realistic forgot-one-method failure mode — assert the throw still fires when other Apply methods exist on the state)
        - `AssertTerminatableCompliance_WrongReturnType_ThrowsMissingApplyMethodException` (covers AC #6 return-type rule)
  - [x] 2.4 Use `Shouldly` for assertions to match the existing `StorageKeyIsolationAssertions.cs` pattern. Do not introduce `Assert.Throws` if it conflicts with the surrounding style of the file you add — check sibling files in `tests/Hexalith.EventStore.Testing.Tests/Assertions/` first and pick the dominant style for new tests
  - [x] 2.5 Verify the message-text branch implicitly: at least one negative test should `ShouldContain` the substring `"ITerminatable"` or `"Apply(AggregateTerminated)"` produced by `MissingApplyMethodException.BuildMessage` (already covered by R1-A6 — this test pins the helper-to-exception integration, not the exception itself)

- [x] Task 3: Pin the Counter sample's compliance (AC: #8, #10)
  - [x] 3.1 Update `tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj` to add `<ProjectReference Include="..\..\src\Hexalith.EventStore.Testing\Hexalith.EventStore.Testing.csproj" />` (currently absent — Sample.Tests references only the Sample project today)
  - [x] 3.2 Add `using Hexalith.EventStore.Testing.Compliance;` to `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs`
  - [x] 3.3 Append a new `[Fact] public void CounterState_IsTerminatableCompliant()` that calls `TerminatableComplianceAssertions.AssertTerminatableCompliance<CounterState>()`. Lead the test method with a sentinel comment to deter casual deletion: `// R1-A2 sentinel — do not delete; protects ITerminatable replay safety. See _bmad-output/implementation-artifacts/post-epic-1-r1a2-terminatable-compliance-helper.md`
  - [x] 3.4 Confirm the test passes against `samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs` as it stands today (which already declares `public void Apply(AggregateTerminated e) { }`)
  - [x] 3.5 Do not modify any other Sample test or assertion in this story

- [x] Task 4: Validate the change set (AC: #9, #10)
  - [x] 4.1 Run `dotnet build src/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.csproj --configuration Release` — clean (0 warnings, 0 errors). Use `-p:NuGetAudit=false` only if pre-existing OpenTelemetry transitive CVE warnings reproduce on `main` HEAD (sibling stories R1-A1 and R1-A6 documented this workaround in their Debug Log References)
  - [x] 4.2 Run `dotnet test tests/Hexalith.EventStore.Testing.Tests/` — must add 5+ green tests on top of the existing 67
  - [x] 4.3 Run `dotnet test tests/Hexalith.EventStore.Sample.Tests/` — Counter pinning test must pass; existing 62 stay green
  - [x] 4.4 Run `dotnet test tests/Hexalith.EventStore.Client.Tests/` — must remain 334/334 (regression check on `MissingApplyMethodException` consumers)
  - [x] 4.5 Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` — must remain 271/271 (sanity check)
  - [x] 4.6 Tier 2 / Tier 3 are out of scope for this story; do not attempt `dapr init`. R1-A7 owns actor-lifecycle integration coverage

## Dev Notes

### Scope Summary

This is a focused diagnostics and developer-experience story in the `Hexalith.EventStore.Testing` package. It adds one static helper that converts the runtime-only `ITerminatable` ⇒ `Apply(AggregateTerminated)` obligation (Story 1.5, retro D1-2) into a deterministic Tier 1 assertion any domain team can call from their own tests.

This story must not change command dispatch, replay semantics, persistence, projection behavior, or runtime error handling. The helper exists exclusively for test-time verification of a domain's state class shape.

### Why This Story Exists

Story 1.5 introduced `ITerminatable` and documented in the contract's `<remarks>` that any implementer MUST also declare a no-op `Apply(AggregateTerminated)` because rejection events are persisted to the event stream and replayed during rehydration. Epic 1 retrospective elevated this latent risk to **R1-A2** (HIGH priority, D1-2 in the design-debt table) because:

- A new domain team's `ITerminatable` state will pass first-close and first-rejection tests.
- The fault appears only after actor deactivation + reactivation, when the persisted `AggregateTerminated` rejection event replays through `Apply` and finds no method.
- Today, the only signal is a runtime `MissingApplyMethodException` (R1-A6) thrown by `DomainProcessorStateRehydrator` after a stuck actor begins fault-looping.

R1-A2 closes the gap on the consuming side: a one-line Tier 1 assertion the domain team copies from the Counter sample.

### Architecture Decisions

#### ADR R1A2-01: Test-time reflection helper, not interface change

**Status:** Accepted (sprint-change-proposal-2026-04-26.md §4 Proposal 3).

**Context:** `ITerminatable` carries a runtime-only obligation — implementers must declare a no-op `Apply(AggregateTerminated)`. Domain teams discover this only on actor reactivation. Epic 1 retro flagged HIGH (D1-2).

**Decision:** Ship a Tier 1 reflection helper in `Hexalith.EventStore.Testing.Compliance`. Failures throw the existing R1-A6 `MissingApplyMethodException` (identical to runtime ergonomics).

**Consequences:**
- (+) Catches the obligation at test time, before actor reactivation can fault-loop.
- (+) Failure messages match runtime exactly — no parallel ergonomic surface to maintain.
- (+) Zero impact on `Contracts` package surface, zero impact on production runtime.
- (−) Opt-in: teams that don't call the helper get no protection. Counter sample sets the example; the defensive-call recommendation in the helper's XML doc covers the future-`ITerminatable`-adopter path.
- (−) Reflection-based: depends on `MissingApplyMethodException` API stability (see Architecture Constraints).

**Alternatives considered:**

1. **Add `void Apply(AggregateTerminated e);` to `ITerminatable` directly.** Rejected — `ITerminatable` is a state-shape contract (one read-only property: `bool IsTerminated { get; }`), not a behaviour contract. Adding an Apply method mixes "describe my state" with "react to an event," and downstream domains that already discover Apply methods via `DomainProcessorStateRehydrator.DiscoverApplyMethods` would have one method routed via interface dispatch and the rest via reflection. Two routing models for one logical concern is a maintenance trap. Also creates structural namespace coupling: `AggregateTerminated` lives in `Hexalith.EventStore.Contracts.Events`, decoupled from `ITerminatable` in `Hexalith.EventStore.Contracts.Aggregates` — putting the method on the interface forces every implementer to `using` the events namespace.
2. **Roslyn analyzer.** Rejected — scope creep; analyzer infrastructure is heavyweight; out of sprint-change-proposal scope.
3. **Source generator that synthesizes the no-op `Apply(AggregateTerminated)`.** Rejected — hides the obligation; bypassed silently if a domain team removes the generator usage.
4. **Status quo (`<remarks>` warning only).** Rejected — this is the gap R1-A2 exists to fix.

**Reversibility:** Low cost. If alternative #1 is pursued in a future major version, the helper becomes a no-op and can be deprecated.

#### ADR R1A2-02: Helper short-circuits silently for non-`ITerminatable` states

**Status:** Accepted.

**Context:** AC #2 mandates the helper return silently when `TState` does not implement `ITerminatable`.

**Decision:** Return silently rather than throw, log, or emit a "skipped" indicator.

**Rationale:** Enables the *defensive-call pattern* — a domain team can call the helper from every aggregate state's primary test class today, and the safety net activates the moment `ITerminatable` is later added. If the helper threw or logged for non-terminatable states, domain teams would only call it after adopting `ITerminatable`, defeating the prevent-future-regression goal.

#### ADR R1A2-03: Parameter-type-keyed lookup, mirroring `DiscoverApplyMethods`

**Status:** Accepted.

**Context:** AC #6 mandates `GetMethod(types: new[] { typeof(AggregateTerminated) })` semantics, not name-based or attribute-based discovery.

**Decision:** Resolve the Apply method by exact parameter type, not by scanning all `Apply` methods and inspecting their first parameter.

**Rationale:** Matches the rehydrator's contract exactly (`DiscoverApplyMethods` keys on `parameters[0].ParameterType`). Also rejects accidental matches like `Apply(string eventTypeName)` or `Apply(JsonElement element)` that a name-based scan would incorrectly accept.

### Why R1-A6 Lands First

Per `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26.md` §3, implementation order is **R1-A1 → R1-A6 → R1-A2 → R1-A7**. R1-A6 (`MissingApplyMethodException`) is already `done`. This story consumes that exception type directly: the helper throws the same `MissingApplyMethodException` that runtime replay throws, so failure ergonomics are identical at test time and at runtime — domain teams see the same hint text and the same structured payload (`StateType`, `EventTypeName`).

R1-A1 has no direct dependency on R1-A2. R1-A7 (Tier 2 actor-lifecycle tombstoning test) lands after this story and validates R1-A1, R1-A2, and R1-A6 end-to-end.

### Existing Code Reality (verified 2026-04-27 against `main` HEAD `4619f75`)

- `src/Hexalith.EventStore.Contracts/Aggregates/ITerminatable.cs`: interface with `bool IsTerminated { get; }` and the `<remarks>` warning about `Apply(AggregateTerminated)`. Do not modify.
- `src/Hexalith.EventStore.Contracts/Events/AggregateTerminated.cs`: `public sealed record AggregateTerminated(string AggregateType, string AggregateId) : IRejectionEvent`. Do not modify.
- `src/Hexalith.EventStore.Client/Aggregates/MissingApplyMethodException.cs`: already exists post-R1-A6. Public sealed, derives from `InvalidOperationException`, carries `StateType`, `EventTypeName`, optional `MessageId`, and optional `AggregateId`. The `BuildMessage` static method emits the `ITerminatable` hint when the state type implements `ITerminatable` **OR** the event type name equals `nameof(AggregateTerminated)`. The two-arg constructor `new MissingApplyMethodException(stateType, eventTypeName)` is the right shape for this story (no message id / aggregate id available at test time).
- `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`: hosts `DiscoverApplyMethods(Type stateType)` — the canonical Apply-discovery rule we mirror. Discovers public + instance methods named `Apply` with exactly one parameter and exactly `void` return type, keyed by the parameter's CLR type short name. Snapshot-aware replay (`DomainServiceCurrentState.Events` envelope path, exercised by R1-A6's regression test) routes through the **same** `DiscoverApplyMethods` mechanism, so this helper's coverage applies to both fresh-stream replay and post-snapshot replay by definition.
- `samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs`: already declares `public void Apply(AggregateTerminated e) { /* no-op */ }`. The Counter pinning test must pass on day one.
- `tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj`: today references only the Sample project. Adding the Testing project reference is a new line in the existing `<ItemGroup>`.
- `src/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.csproj`: already references `Hexalith.EventStore.Server`, which references `Hexalith.EventStore.Client`. `MissingApplyMethodException` is reachable by transitive reference. Do not add a direct Client reference.

### Suggested Helper Shape

```csharp
namespace Hexalith.EventStore.Testing.Compliance;

using System.Reflection;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Aggregates;
using Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Test-time helpers that convert the runtime-only Apply-method obligations
/// of <see cref="ITerminatable"/> aggregate states into Tier 1 assertions.
/// </summary>
/// <remarks>
/// <see cref="ITerminatable"/> carries a runtime-only constraint documented in
/// its own remarks: any state implementing the interface must also declare a
/// no-op <c>Apply(AggregateTerminated)</c> method, because
/// <see cref="AggregateTerminated"/> rejection events are persisted to the
/// event stream and replayed during actor rehydration. Domain teams that omit
/// the method pass first-close tests and only fail on actor reactivation. This
/// helper makes the obligation deterministic at test time, sharing failure
/// ergonomics with the runtime <see cref="MissingApplyMethodException"/>
/// thrown by aggregate rehydration.
/// </remarks>
public static class TerminatableComplianceAssertions
{
    /// <summary>
    /// Asserts that <typeparamref name="TState"/> satisfies the
    /// <see cref="ITerminatable"/> Apply-method contract. No-op for state
    /// types that do not implement <see cref="ITerminatable"/>.
    /// </summary>
    /// <typeparam name="TState">The aggregate state class to verify.</typeparam>
    /// <exception cref="MissingApplyMethodException">
    /// Thrown when <typeparamref name="TState"/> implements
    /// <see cref="ITerminatable"/> but has no public instance
    /// <c>void Apply(AggregateTerminated)</c> method.
    /// </exception>
    public static void AssertTerminatableCompliance<TState>()
        where TState : class
    {
        Type stateType = typeof(TState);
        if (!typeof(ITerminatable).IsAssignableFrom(stateType))
        {
            return;
        }

        MethodInfo? applyMethod = stateType.GetMethod(
            name: "Apply",
            bindingAttr: BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(AggregateTerminated) },
            modifiers: null);

        if (applyMethod is null || applyMethod.ReturnType != typeof(void))
        {
            throw new MissingApplyMethodException(
                stateType: stateType,
                eventTypeName: nameof(AggregateTerminated));
        }
    }
}
```

This shape is a recommendation, not a contract. The acceptance criteria define behavior; the implementation may differ on style as long as ACs hold.

### Apply-Discovery Rules to Mirror (from R1-A6 Dev Notes)

`DomainProcessorStateRehydrator.DiscoverApplyMethods(Type stateType)` enforces:

- public instance methods only
- method name exactly `Apply`
- exactly one parameter
- return type exactly `void`

Mirror these rules. Resolve the method by parameter type rather than walking all public methods so the helper rejects accidental matches like `Apply(string eventTypeName)`. The story does not need to revisit name-collision behavior for short event-type names — that is `DiscoverApplyMethods`'s concern, deferred from R1-A6 review.

### Failure Message Reuse

`MissingApplyMethodException.BuildMessage` already produces the `Hint: states implementing ITerminatable must declare a no-op Apply(AggregateTerminated)…` line whenever the state implements `ITerminatable` or the event type name is `AggregateTerminated`. Re-using the exception (rather than throwing a different type) means:

- Test failure output points domain teams at the same hint runtime would have shown them post-deactivation.
- Operators reading test logs can grep for `MissingApplyMethodException` and find both runtime and test-time occurrences.
- Future improvements to the message (Story 1.5 dev-notes references, links to architecture rule numbers) flow into both paths.

### Sample-Tests Project Reference (New)

`tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj` does not currently reference the Testing project. Adding it brings `Hexalith.EventStore.Testing.Compliance` plus all the existing Testing fakes/builders into Sample tests. This is intentional — Sample.Tests is the consumer-pattern fixture; future sample tests may reuse `EventEnvelopeBuilder`, `AggregateIdentityBuilder`, `DomainResultAssertions`, etc.

Verify the new reference does not pull in the Server's DAPR dependencies in a way that breaks the Tier 1 test boundary. Today, the Testing project already references Server (via its csproj line 13), so the transitive surface is consistent with how Server.Tests and Testing.Tests already see it. This story does not add net-new transitive surface to Sample.Tests beyond the Testing reference itself.

### Architecture Constraints

- **D3** (rejection events persisted): rejection events live in the event stream forever. Replay must process them. This is the design rule that makes `Apply(AggregateTerminated)` mandatory.
- **FR48**: `EventStoreAggregate<TState>` uses typed Apply methods; the helper's reflection contract aligns with the production rehydration contract.
- **FR66**: tombstoning irreversibility — once `IsTerminated == true`, future commands are rejected. The helper does not test FR66 behavior; it tests the prerequisite that the state can replay the rejection event without throwing.
- **Rule 8**: event names remain past-tense. Do not rename `AggregateTerminated` in test fixtures.
- **Rule 11**: write-once event stream. Test fixtures do not need to honor this rule (they don't persist), but the conceptual reason behind R1-A2 is Rule 11 — past `AggregateTerminated` events are immutable history that future replays will see.
- **UX-DR19**: public XML docs required on the new `TerminatableComplianceAssertions` static class and method. The Testing csproj already enforces this implicitly through the broader `TreatWarningsAsErrors` policy.
- **UX-DR20**: minimal public surface — one static class, one static method. Resist scope creep. A general `AssertAggregateCompliance` covering all event types is out of scope for this story.
- **R1-A6 dependency invariant (load-bearing).** This helper relies on the public surface of `MissingApplyMethodException`: namespace `Hexalith.EventStore.Client.Aggregates`, the 2-arg constructor `(Type stateType, string eventTypeName)`, and the `BuildMessage` ITerminatable-hint trigger condition. Any future refactor that moves the namespace, changes the ctor signature, or removes the hint MUST update `TerminatableComplianceAssertions` in lockstep. R1-A2's tests will surface the breakage but the linkage is structural — call it out in any PR that touches `MissingApplyMethodException`.

### Testing Standards (project-wide rules — apply to every story)

- **Tier 1 (Unit):** xUnit + Shouldly + NSubstitute. No DAPR runtime, no Docker. This entire story is Tier 1.
- **Tier 2 / Tier 3 (Integration) — REQUIRED end-state inspection:** Not applicable. This story does not create or modify Tier 2 (`Server.Tests`) or Tier 3 (`IntegrationTests`) tests. Sibling story R1-A7 owns the Tier 2 actor-lifecycle coverage that exercises the same R1-A2 obligation through the live actor pipeline.
- **ID validation:** Not applicable. The helper does not handle `messageId`, `correlationId`, `aggregateId`, or `causationId`. *Reference:* Epic 2 retro R2-A7.
- **Named-argument discipline (R1-A1 retro guidance):** the helper's only call site that passes multiple parameters is `new MissingApplyMethodException(stateType: typeof(TState), eventTypeName: nameof(AggregateTerminated))`. Use named arguments here as in the suggested shape — both R1-A1 and R1-A6 review patches enforced this same rule at the matching exception-construction sites.

### Previous Story Intelligence

From `_bmad-output/implementation-artifacts/post-epic-1-r1a6-missing-apply-method-exception.md`:

- `MissingApplyMethodException` is `public sealed`, derives from `InvalidOperationException`, lives in `Hexalith.EventStore.Client.Aggregates`. Constructor signature is `(Type stateType, string eventTypeName, string? messageId = null, string? aggregateId = null)`.
- Constructor validates `stateType != null` and `eventTypeName` is non-whitespace. Passing `nameof(AggregateTerminated)` satisfies the validation.
- The exception's `BuildMessage` emits the tombstoning hint when either the state implements `ITerminatable` or the event name is `AggregateTerminated`. Either trigger condition produces the helper-friendly message — no special handling needed.
- R1-A6 review patch enforced named-argument discipline at the three throw sites in `DomainProcessorStateRehydrator`. Apply the same convention at the new throw site in this helper.
- Story-creation lessons (`_bmad-output/process-notes/story-creation-lessons.md`) are sparse for this codebase; rely primarily on the sibling stories' Dev Notes for tone and convention.

From `_bmad-output/implementation-artifacts/post-epic-1-r1a1-aggregatetype-pipeline.md`:

- Keep follow-up stories tightly scoped. Do not bundle R1-A2 with R1-A7's Tier 2 work — they are separate stories by design.
- Prefer explicit named arguments at exception/method boundaries with multiple `string` or `Type` parameters.
- A NuGet `NU1902` audit-bypass workaround (`-p:NuGetAudit=false`) is already established for pre-existing OpenTelemetry transitive CVE warnings on `main` HEAD; reuse the workaround if encountered, do not address upstream package upgrades in this story.

From `_bmad-output/implementation-artifacts/1-5-commandstatus-enum-and-aggregate-tombstoning.md` (lines 96, 110-117):

- The `Apply(AggregateTerminated)` obligation is documented in `<remarks>`, but a domain dev implementing `ITerminatable` for a new aggregate will pass close-works/first-rejection tests and only fail on actor reactivation. Story 1.5's own Dev Notes explicitly call out R1-A2 as the prescribed mitigation: *"Add `AssertTerminatableCompliance<TState>()` test helper to the Testing package — uses reflection to verify `Apply(AggregateTerminated)` exists on any state class implementing `ITerminatable`. New domain teams include this assertion in their test suite."*
- This story is the planned execution of that mitigation. Do not propose alternative mitigations (Roslyn analyzer, source-generator, etc.) — they are explicitly out of scope.

### File Structure Notes

- New helper: `src/Hexalith.EventStore.Testing/Compliance/TerminatableComplianceAssertions.cs` (new sibling folder to `Assertions/`, `Builders/`, `Fakes/`, `Http/`)
- New tests: `tests/Hexalith.EventStore.Testing.Tests/Compliance/TerminatableComplianceAssertionsTests.cs` (new sibling folder to `Assertions/`, `Builders/`, `Fakes/`)
- Sample test pin: `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs` (one new `[Fact]` and one new `using` directive)
- Csproj edit: `tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj` (one new `<ProjectReference>` in the existing `<ItemGroup>`)
- Do not edit any Server, Client, or Contracts source for this story
- Do not edit any actor lifecycle test or DAPR fixture
- Do not introduce a new namespace style; match `Hexalith.EventStore.Testing.Assertions` precedent for the new `Hexalith.EventStore.Testing.Compliance` namespace

### Project Structure Notes

- Folder placement under `src/Hexalith.EventStore.Testing/Compliance/` matches the sprint change proposal §4 Proposal 3 explicitly. Do not collapse it into `Assertions/` even though the suffix `Assertions` is shared — the semantic distinction (test-time runtime-contract verification vs. data-shape assertions) is intentional and matches how operators search for "compliance" issues in test failure output.
- Sample.Tests does not currently reference Testing. This is the first story to add that reference. Confirm the addition does not break the existing `Hexalith.EventStore.Sample.Tests` test pyramid placement (still Tier 1 — Testing's transitive Server reference does not pull DAPR runtime into Tier 1 test execution).
- **Compile-graph expansion (acknowledged):** the new `Sample.Tests → Testing` reference transitively brings `Hexalith.EventStore.Server` (and its `Dapr.Client`, `Dapr.Actors`, `Dapr.Actors.AspNetCore`, `MediatR` package references) onto Sample.Tests's compile graph. This does not change Tier 1 runtime behaviour (no DAPR sidecar starts; no actor activation), but it widens the previously narrow Sample.Tests compile surface. Accepted as the right trade because the helper must ship in the Testing NuGet for downstream domain teams; an alternative (one-off helper inside Sample.Tests, or a dedicated test-shared project) was considered and rejected for not solving the actual consumer problem.

### Future Extensibility (out of scope for R1-A2)

R1-A2's helper is specialized for the (`ITerminatable`, `AggregateTerminated`) pair because that is today's only gating-interface / framework-rejection-event pair. The general form of the obligation is *"for any event type `TEvent` that the framework persists unconditionally during rejection, every state implementing the gating interface must declare `Apply(TEvent)`."*

If the framework ever introduces a second pair (e.g., `ISuspendable` × `AggregateSuspended`, or `ILocked` × `AggregateLocked`), generalize to a single primitive rather than copy-pasting a second specialized helper:

```csharp
public static void AssertReplayCompliance<TState, TInterface, TEvent>()
    where TState : class
    where TEvent : IEventPayload;
```

`AssertTerminatableCompliance<TState>()` then becomes a one-line specialization. **Do not introduce this generalization in R1-A2** — YAGNI. The note exists so the next maintainer recognizes the pattern when the second pair lands.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26.md` §4 Proposal 3]
- [Source: `_bmad-output/implementation-artifacts/epic-1-retro-2026-04-26.md` §R1-A2, D1-2]
- [Source: `_bmad-output/implementation-artifacts/1-5-commandstatus-enum-and-aggregate-tombstoning.md` lines 96, 110-117]
- [Source: `_bmad-output/implementation-artifacts/post-epic-1-r1a1-aggregatetype-pipeline.md`]
- [Source: `_bmad-output/implementation-artifacts/post-epic-1-r1a6-missing-apply-method-exception.md`]
- [Source: `_bmad-output/planning-artifacts/architecture.md`]
- [Source: `_bmad-output/planning-artifacts/prd.md` FR48, FR66]
- [Source: `src/Hexalith.EventStore.Contracts/Aggregates/ITerminatable.cs`]
- [Source: `src/Hexalith.EventStore.Contracts/Events/AggregateTerminated.cs`]
- [Source: `src/Hexalith.EventStore.Client/Aggregates/MissingApplyMethodException.cs`]
- [Source: `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs` (DiscoverApplyMethods)]
- [Source: `src/Hexalith.EventStore.Testing/Assertions/StorageKeyIsolationAssertions.cs` (Shouldly style precedent)]
- [Source: `src/Hexalith.EventStore.Testing/Assertions/DomainResultAssertions.cs` (xUnit style precedent)]
- [Source: `samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs`]
- [Source: `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs`]
- [Source: `tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj`]
- [Source: `tests/Hexalith.EventStore.Testing.Tests/Assertions/DomainResultAssertionsTests.cs`]

## Dev Agent Record

### Agent Model Used

claude-opus-4-7 (1M context)

### Debug Log References

- Build (Testing project, Release): 0 warnings / 0 errors.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/` → **72/72 green** (was 67; +5 new tests in `Compliance/TerminatableComplianceAssertionsTests.cs`).
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/ -p:NuGetAudit=false` → **63/63 green** (was 62; +1 `CounterState_IsTerminatableCompliant` pin). NuGetAudit workaround applied per Task 4.1: pre-existing OpenTelemetry 1.15.1 CVE warnings (GHSA-g94r-2vxg-569j, GHSA-mr8r-92fq-pj8p, GHSA-q834-8qmm-v933) reproduce on `main` HEAD via `ServiceDefaults` transitive reference and are upstream-package work, not in story scope.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/ -p:NuGetAudit=false` → **334/334 green** (regression check on `MissingApplyMethodException` consumers; baseline preserved).
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` → **271/271 green** (sanity check).
- One in-flight build error during Task 2 development: CA1822 (Performance) flagged the test fixtures' `Apply` methods as static-able. Resolved by adding localized `[SuppressMessage("Performance", "CA1822", Justification = "...")]` attributes — the helper requires `BindingFlags.Instance` discovery, so the methods must remain instance members. Anticipated by Subtask 2.2 of the story spec.

### Completion Notes List

- **Helper shape:** Implemented per the suggested shape in Dev Notes — `GetMethod` with explicit `types` array (parameter-type-keyed lookup, ADR R1A2-03) plus `BindingFlags.Public | BindingFlags.Instance` and named arguments at every multi-arg call site (R1-A1/R1-A6 retro discipline). Failure path throws the existing R1-A6 `MissingApplyMethodException` with `nameof(AggregateTerminated)` for `eventTypeName`, so the `BuildMessage` ITerminatable hint fires automatically.
- **No new project references on the Testing side:** confirmed `MissingApplyMethodException` reaches the helper transitively via Testing → Server → Client (Task 1.8). No direct Client reference added.
- **Sample.Tests now references Testing:** Task 3.1 adds the new `<ProjectReference>` line. The compile-graph expansion (DAPR-related Server transitive packages onto Sample.Tests) was acknowledged in the story Dev Notes and accepted; no DAPR runtime starts in Tier 1.
- **Test-style choice:** Sibling files in `tests/Hexalith.EventStore.Testing.Tests/Assertions/` (`DomainResultAssertionsTests.cs`, `EventEnvelopeAssertionsTests.cs`) use xUnit `Assert.Throws<>` / `Assert.Equal` — the new test file matches that dominant style, per Subtask 2.4's "pick the dominant style for new tests" guidance. (The story listed Shouldly as one option but flagged conflicting style as a deal-breaker. Testing.Tests csproj has no Shouldly PackageReference; xUnit `Assert.*` is the established surface.)
- **Counter pinning sentinel comment** added per Subtask 3.3 to deter casual deletion of the no-op `Apply(AggregateTerminated)` in `CounterState.cs`.
- **Out of scope (correctly skipped):** Tier 2 / Tier 3 / `dapr init` (Task 4.6); generalization to `AssertReplayCompliance<TState, TInterface, TEvent>()` (story Future-Extensibility note — YAGNI).

### File List

**New:**
- `src/Hexalith.EventStore.Testing/Compliance/TerminatableComplianceAssertions.cs`
- `tests/Hexalith.EventStore.Testing.Tests/Compliance/TerminatableComplianceAssertionsTests.cs`

**Modified:**
- `tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj` (added `ProjectReference` to `Hexalith.EventStore.Testing`)
- `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs` (added `using Hexalith.EventStore.Testing.Compliance;` and `CounterState_IsTerminatableCompliant` pin test with R1-A2 sentinel comment)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (status `ready-for-dev` → `in-progress` → `review` for `post-epic-1-r1a2-terminatable-compliance-helper`; `last_updated` line updated accordingly)

**Deleted:** none.

### Review Findings

- [x] [Review][Decision→Patch] No test covers Apply method inherited from a base class — Resolved by adding `BaseStateWithApply` + `InheritedApplyTerminatableState` fixtures and `AssertTerminatableCompliance_StateInheritsApplyFromBase_DoesNotThrow` test. Pins AC #6's "inherited public instance methods satisfy the contract" promise. Sources: blind+edge.

- [x] [Review][Patch] Remove unused `NonTerminatableState.Value` property (dead test fixture code) [tests/Hexalith.EventStore.Testing.Tests/Compliance/TerminatableComplianceAssertionsTests.cs] — Removed; class is now an empty body. Sources: blind+auditor.

- [x] [Review][Defer] Abstract class TState with abstract `Apply(AggregateTerminated)` — unusual edge case; `IsAbstract` not checked, `MethodInfo` for an abstract method would still satisfy the helper. Pre-existing scope: state classes are concrete in practice; not exercised by current Counter sample or AC #7. [src/Hexalith.EventStore.Testing/Compliance/TerminatableComplianceAssertions.cs:52-63]
- [x] [Review][Defer] TState passed as interface or open-generic type definition — `where TState : class` allows interfaces; calling helper with an interface or open-generic as TState is unusual usage. Reflection on interfaces returns interface methods, behaviour technically defined but undocumented. Not a real failure mode. [src/Hexalith.EventStore.Testing/Compliance/TerminatableComplianceAssertions.cs:45-50]
- [x] [Review][Defer] `GetMethod` could theoretically throw `AmbiguousMatchException` not wrapped by helper — With `binder: null` and exact `types: new[] { typeof(AggregateTerminated) }`, ambiguity is rare but theoretically possible. Helper would surface raw reflection exception instead of `MissingApplyMethodException`, slightly violating helper contract. [src/Hexalith.EventStore.Testing/Compliance/TerminatableComplianceAssertions.cs:52-57]

## Change Log

- **2026-04-27 — Code review (Status: review → done).** 3 reviewers (Blind Hunter, Edge Case Hunter, Acceptance Auditor) produced 30 distinct findings. Triage: 1 decision-needed → resolved as patch, 1 patch, 3 deferred (logged in `deferred-work.md`), 25 dismissed. Patches applied: (1) added base-class inheritance fixture + test `AssertTerminatableCompliance_StateInheritsApplyFromBase_DoesNotThrow` to pin AC #6's "inherited public instance Apply satisfies the contract" promise; (2) removed dead `NonTerminatableState.Value` property. Testing.Tests now 73/73 green (was 72; +1 inheritance test). Build clean (0/0). Acceptance Auditor reported 0 violations on AC #1-10 and on subtask compliance.
- **2026-04-27 — Implementation (Status: ready-for-dev → in-progress → review).** Added Tier 1 reflection helper `TerminatableComplianceAssertions.AssertTerminatableCompliance<TState>()` in the new `Hexalith.EventStore.Testing.Compliance` namespace; helper short-circuits for non-`ITerminatable` states and otherwise resolves `Apply(AggregateTerminated)` via `GetMethod` parameter-type-keyed lookup, throwing the existing R1-A6 `MissingApplyMethodException` for missing or non-`void` matches. Covered by 5 new Tier 1 tests in `Hexalith.EventStore.Testing.Tests/Compliance/`. Pinned `CounterState`'s compliance via a new `[Fact]` in `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateTests.cs` (also added the Testing project reference to Sample.Tests). All four Tier 1 suites green: Testing 72/72 (+5), Sample 63/63 (+1), Client 334/334, Contracts 271/271. Build clean (0/0). Pre-existing OpenTelemetry CVE warnings worked around per documented `-p:NuGetAudit=false` policy (Task 4.1).
