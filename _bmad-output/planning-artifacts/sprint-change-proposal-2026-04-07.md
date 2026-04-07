# Sprint Change Proposal — 2026-04-07

## Section 1: Issue Summary

**Trigger:** Story 20-5 (Correlation ID Trace Map, Epic 20) introduced two build errors. While investigating and testing the full Aspire topology, a **pre-existing bug** was discovered in `EventStoreAggregate<TState>.DispatchCommandAsync()` — the core command dispatch method in the Client NuGet package.

**Problem Statement:** The `HandleMethods` dictionary is keyed by short type name (e.g., `"IncrementCounter"`), but `command.CommandType` arrives from the REST API as an assembly-qualified name (e.g., `"Hexalith.EventStore.Sample.Commands.IncrementCounter, Hexalith.EventStore.Sample"`). This caused a `KeyNotFoundException` — any domain service using the fluent `EventStoreAggregate` pattern could not process commands through the full DAPR pipeline.

**Discovery:** Aspire topology launched, authenticated via Keycloak, submitted `IncrementCounter` command. Sample service returned 500: `"No Handle method found for command type '...' on aggregate 'CounterAggregate'"`.

**Evidence:**
- Command status: `Rejected` with `failureReason: "Domain service invocation failed ... 500 (Internal Server Error)"`
- Direct sample service test confirmed: base64-encoded payload accepted, but Handle method lookup failed on fully-qualified type name

---

## Section 2: Impact Analysis

### Epic Impact
- **Epic 20 (Admin Advanced Debugging):** Story 20-5 build errors fixed. Epic continues as planned.
- **Epic 1 (Domain Contract Foundation) / Epic 8 (Sample App):** Latent bug in `EventStoreAggregate` — the fluent convention-based aggregate pattern (Story 1.4) was not handling assembly-qualified command type names.
- **All other epics:** No impact.

### Story Impact
- **Story 1.4 (Pure Function Contract & EventStoreAggregate Base):** Implementation gap — `DispatchCommandAsync` assumed short type names only.
- **Story 20-5 (Correlation ID Trace Map):** Two build errors fixed (CS0157 in `CorrelationTraceMap.razor`, CS0433 in `PerConsumerRateLimitingTests.cs`).

### Artifact Conflicts
- **PRD:** None. FR21/FR48 specify convention-based dispatch; fix aligns implementation with spec.
- **Architecture:** None. Architecture documents `EventStoreAggregate` convention pattern correctly.
- **UX Design:** None.

### Technical Impact
- **Hexalith.EventStore.Client** NuGet package: `EventStoreAggregate.cs` modified (added `ExtractShortTypeName` helper).
- **Hexalith.EventStore.Admin.UI**: `CorrelationTraceMap.razor` — `return` inside `finally` replaced with conditional.
- **Hexalith.EventStore.IntegrationTests**: `PerConsumerRateLimitingTests.cs` — added `CommandApiProgram` using alias.
- **Test gap:** No existing test covered assembly-qualified type name dispatch.

---

## Section 3: Recommended Approach

**Selected:** Direct Adjustment

**Rationale:**
- All three fixes are already applied and verified
- Build: 0 errors, 0 warnings
- Tier 1 tests: 359 passing (297 Client + 62 Sample)
- Full E2E: Aspire topology launched, commands processed successfully (Increment + Decrement both Completed with eventCount=1)
- Fixes are backward-compatible — short type names continue to work
- No API surface changes, no breaking changes

**Effort:** Low (already complete)
**Risk:** Low (additive fix, no behavioral change for existing consumers)
**Timeline:** No impact on sprint timeline

---

## Section 4: Detailed Change Proposals

### Change 1: CorrelationTraceMap.razor (Admin UI)

**File:** `src/Hexalith.EventStore.Admin.UI/Components/CorrelationTraceMap.razor`
**Story:** 20-5

OLD:
```csharp
finally
{
    if (loadVersion != _loadVersion)
    {
        return;
    }
    _loading = false;
    StateHasChanged();
}
```

NEW:
```csharp
finally
{
    if (loadVersion == _loadVersion)
    {
        _loading = false;
        StateHasChanged();
    }
}
```

**Rationale:** C# forbids `return` inside `finally` blocks (CS0157). Inverted condition achieves identical semantics.

---

### Change 2: PerConsumerRateLimitingTests.cs (Integration Tests)

**File:** `tests/Hexalith.EventStore.IntegrationTests/CommandApi/PerConsumerRateLimitingTests.cs`
**Story:** N/A (test fix)

OLD:
```csharp
using WebApplicationFactory<Program> tenantFirstFactory = ...
```

NEW:
```csharp
using CommandApiProgram = commandapi::Program;
// ...
using WebApplicationFactory<CommandApiProgram> tenantFirstFactory = ...
```

**Rationale:** `Program` is ambiguous between AppHost and Sample assemblies (CS0433). All other integration test files use the `CommandApiProgram` alias convention.

---

### Change 3: EventStoreAggregate.cs (Client NuGet Package)

**File:** `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs`
**Story:** 1.4 (Pure Function Contract & EventStoreAggregate Base)

OLD:
```csharp
private async Task<DomainResult> DispatchCommandAsync(...) {
    if (!metadata.HandleMethods.TryGetValue(command.CommandType, out HandleMethodInfo? handleInfo)) {
        throw new InvalidOperationException(...);
    }
```

NEW:
```csharp
private async Task<DomainResult> DispatchCommandAsync(...) {
    string lookupKey = ExtractShortTypeName(command.CommandType);
    if (!metadata.HandleMethods.TryGetValue(lookupKey, out HandleMethodInfo? handleInfo)) {
        throw new InvalidOperationException(...);
    }
// ...
private static string ExtractShortTypeName(string commandType) {
    int commaIndex = commandType.IndexOf(',', StringComparison.Ordinal);
    string fullName = commaIndex >= 0 ? commandType[..commaIndex] : commandType;
    int dotIndex = fullName.LastIndexOf('.');
    return dotIndex >= 0 ? fullName[(dotIndex + 1)..] : fullName;
}
```

**Rationale:** `HandleMethods` dictionary is keyed by `Type.Name` (short name), but `command.CommandType` from the REST API is assembly-qualified. The `ExtractShortTypeName` helper strips namespace and assembly to produce the matching key. Handles all three formats: `"IncrementCounter"`, `"Namespace.IncrementCounter"`, `"Namespace.IncrementCounter, Assembly"`.

---

## Section 5: Implementation Handoff

**Change Scope: Minor** — Direct implementation by development team.

**Status:** All fixes already applied and verified.

**Remaining action items:**
1. Commit the three fixes
2. Consider adding a unit test for `ExtractShortTypeName` and/or an integration test that submits a command with an assembly-qualified type name through `EventStoreAggregate`

**Success Criteria:**
- Build passes (0 errors, 0 warnings)
- Tier 1 tests pass (297 Client + 62 Sample)
- Full Aspire topology starts and processes commands end-to-end
