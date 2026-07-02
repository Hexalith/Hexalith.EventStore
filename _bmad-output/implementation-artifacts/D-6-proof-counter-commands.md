---
created: 2026-07-02
source_story_key: D-6-proof-counter-commands
---

# Story D.6: Proof - Counter Commands

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **domain-module author building a UI host on Hexalith.EventStore**,
I want **the Sample Counter command REST facade generated from Counter command contracts**,
so that **the sample proves generated, gateway-backed typed POST endpoints can replace hand-built generic command envelopes before Tenants adopts the generator**.

## Story Context

This is **story D6 of Epic D - REST Controller Source Generator**. D1-D4 built the contract seam, generator, controller emission, and generator tests. D5 proved the query path in `samples/Hexalith.EventStore.Sample.BlazorUI`, but D5 is currently still in `review` and has unresolved review findings. D6 must use those findings as input, not repeat them.

Source of truth: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md` Section 4, Epic D row for D6, D1-D5 story files, and current Sample Counter command/UI source.

## Acceptance Criteria

1. **D5 carryover is handled before command adoption.**
   - Read D5 review findings before implementation.
   - Do not duplicate command or query contract source into `Sample.BlazorUI`.
   - If `Sample.BlazorUI.csproj` still uses `<Compile Include="..\Hexalith.EventStore.Sample\Counter\Queries\GetCounterStatusQuery.cs" ...>`, replace it with a supported referenced-assembly model or stop for story correction if a `ProjectReference` to the Sample domain host causes unacceptable Web SDK/publish conflicts.
   - Do not hide D5 query review patches inside D6 except where they are required to avoid duplicated contract identity.

2. **Counter commands become real REST command contracts.**
   - Update all Counter command records in `samples/Hexalith.EventStore.Sample/Counter/Commands/`:
     - `IncrementCounter`
     - `DecrementCounter`
     - `ResetCounter`
     - `CloseCounter`
   - Each command implements `ICommandContract`.
   - Each command has an aggregate id property, preferably `CounterId`, with `AggregateId => CounterId`.
   - Preserve existing tests by using a default `CounterId` value of `"counter-1"` unless implementation proves a constructor update is safer.
   - Use static metadata:
     - `Domain => "counter"`
     - `CommandType => "increment-counter"`, `"decrement-counter"`, `"reset-counter"`, `"close-counter"`
   - Add `[RestRoute(RestVerb.Post, "{counterId}/increment")]`, `"{counterId}/decrement"`, `"{counterId}/reset"`, and `"{counterId}/close"` respectively.
   - Do not put package versions in project files.

3. **Generated command dispatch works with kebab-case command contract values.**
   - The generated controller submits `<Command>.CommandType`, which is kebab-case by D1 contract.
   - Current `EventStoreAggregate<TState>` dispatch is keyed by CLR short type name, e.g. `IncrementCounter`. Patch the platform dispatch path so command types implementing `ICommandContract` are also resolvable by `ICommandContract.CommandType`.
   - Preserve legacy short-name command dispatch and existing tests.
   - Add a regression test proving `CounterAggregate` processes an envelope where `CommandType = IncrementCounter.CommandType`.
   - Do not solve this by setting `CommandType` to `nameof(IncrementCounter)`; that violates the D1 contract and `CommandContractResolver` rules.

4. **Sample Blazor UI generates command controllers from referenced contracts.**
   - `Sample.BlazorUI` references the command contract types through the supported referenced-assembly model.
   - The generator emits command actions into `Hexalith.EventStore.Sample.BlazorUI.Generated`.
   - The generated controller route remains `[Route("api/{tenant}/counter")]` from `RestApiAssemblyInfo.cs`.
   - Expected public command endpoints:
     - `POST /api/{tenant}/counter/{counterId}/increment`
     - `POST /api/{tenant}/counter/{counterId}/decrement`
     - `POST /api/{tenant}/counter/{counterId}/reset`
     - `POST /api/{tenant}/counter/{counterId}/close`
   - Generated actions must use `IEventStoreGatewayClient.SubmitCommandAsync`, `UniqueIdHelper.GenerateSortableUniqueStringId()`, route/body mismatch checks, `Retry-After: 1`, and command status `Location`.

5. **Counter command UI no longer hand-builds generic command envelopes.**
   - Update `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterCommandForm.razor`.
   - Remove direct `POST /api/v1/commands`, anonymous `SubmitCommandRequest` JSON construction, `Guid.NewGuid()`, and stringly command-type constants from the component.
   - The component should call the generated typed endpoint for Increment, Decrement, and Reset.
   - Preserve the current behavior: one in-flight command disables buttons, support-safe error display, and a last-command timestamp.
   - Do not add a Close button unless explicitly approved; `CloseCounter` is tombstoning behavior and should be proven by generated source/tests/smoke, not exposed casually in the demo UI.
   - If the component calls its own generated endpoint over HTTP, attach the existing development/Keycloak bearer token using `EventStoreApiAuthorizationHandler` or an equivalent safe app-local client. Do not bypass `[Authorize]`.

6. **Generated endpoint request semantics are proven.**
   - For `POST /api/tenant-a/counter/counter-1/increment` with body `{"counterId":"counter-1"}`, generated code submits:
     - `Tenant = "tenant-a"`
     - `Domain = IncrementCounter.Domain`
     - `AggregateId = "counter-1"`
     - `CommandType = IncrementCounter.CommandType`
     - `Payload` containing the typed command body.
   - Route/body mismatch returns 400 problem details.
   - Gateway failures remain support-safe problem details.
   - Successful accepted command returns HTTP 202 with `SubmitCommandResponse`, `Retry-After: 1`, and command status `Location`.

7. **Scope stays Counter-command proof only.**
   - Do not touch Tenants or other submodules.
   - Do not update release package inventory, `.releaserc.json`, NuGet package count docs, or D8 guardrail docs.
   - Do not redesign Sample UI visuals or notification/query patterns.
   - Do not add AppHost, DAPR component, container, pub/sub, or state-store changes unless local smoke validation proves a missing invocation permission. If that happens, document the exact missing policy and keep it scoped.
   - Leave the separate DAPR global ordering worktree changes untouched.

8. **Tests and generated evidence prove D6.**
   - Add/extend Sample tests for command contract metadata, routes, aggregate id, and `CommandContractResolver`.
   - Add/extend aggregate dispatch tests for kebab-case command type aliases.
   - Add/extend Blazor UI guard tests so `CounterCommandForm` cannot return to direct generic `/api/v1/commands` submission or `Guid.NewGuid()`.
   - Build `Sample.BlazorUI` with generated files emitted to `/tmp/hexalith-eventstore-d6-generated`.
   - Inspect and record the generated controller file path.
   - Run:
     ```bash
     dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/
     dotnet test tests/Hexalith.EventStore.Sample.Tests/
     dotnet test tests/Hexalith.EventStore.Client.Tests/
     dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false
     ```
   - Do not run solution-level `dotnet test`.
   - If feasible, run Aspire with `EnableKeycloak=false`, call the generated increment endpoint, then verify the Counter query endpoint returns the incremented projection and a repeat query with ETag returns 304. Record exact commands and blockers.

## Tasks / Subtasks

- [ ] **Task 1: Preflight D5 and current command/generator state** (AC: 1, 3, 4)
  - [ ] Read D5 review findings and current `Sample.BlazorUI.csproj`.
  - [ ] Confirm whether referenced-assembly discovery emits commands from the Sample domain assembly.
  - [ ] Confirm current aggregate dispatch behavior for kebab-case `ICommandContract.CommandType`.

- [ ] **Task 2: Annotate Counter commands** (AC: 2)
  - [ ] Add `ICommandContract`, `CounterId`, `AggregateId`, static metadata, and `[RestRoute]` to all four Counter commands.
  - [ ] Preserve existing aggregate behavior and constructor compatibility.
  - [ ] Add command contract tests with Shouldly.

- [ ] **Task 3: Make command dispatch contract-compatible** (AC: 3)
  - [ ] Patch `EventStoreAggregate<TState>` to resolve `ICommandContract.CommandType` aliases while keeping CLR short-name lookup.
  - [ ] Add regression tests for kebab-case command dispatch.
  - [ ] Preserve existing legacy command tests.

- [ ] **Task 4: Wire Sample.BlazorUI to referenced command contracts** (AC: 1, 4)
  - [ ] Remove any compile-linked Counter query/command source from `Sample.BlazorUI`.
  - [ ] Use the supported reference model and analyzer reference without package versions.
  - [ ] Verify generated command controller source contains the four command POST actions.

- [ ] **Task 5: Replace generic command submission in `CounterCommandForm`** (AC: 5, 6, 7)
  - [ ] Call generated typed endpoints for Increment, Decrement, and Reset.
  - [ ] Preserve token forwarding/authorization, in-flight state, support-safe errors, and last-command feedback.
  - [ ] Avoid adding a Close button.

- [ ] **Task 6: Add proof/guard tests** (AC: 6, 8)
  - [ ] Add source guard preventing `CounterCommandForm` from using `/api/v1/commands`, `SubmitCommandRequest`, anonymous gateway command JSON, or `Guid.NewGuid()`.
  - [ ] Add generated-source or smoke evidence for command request shape, route/body mismatch, 202, `Retry-After`, and `Location`.
  - [ ] Keep query-path tests from D5 green.

- [ ] **Task 7: Verify and record evidence** (AC: 8)
  - [ ] Build generated files to `/tmp/hexalith-eventstore-d6-generated`.
  - [ ] Run required tests/builds individually.
  - [ ] Run Aspire smoke if feasible; otherwise record exact blocker.
  - [ ] Confirm `git status --short` contains only intended D6 changes plus known pre-existing unrelated worktree changes.

## Dev Notes

### Top Guardrails

1. **Do not use PascalCase command discriminators.** D1 defines `ICommandContract.CommandType` as kebab-case; generated controllers submit that value. Fix dispatch compatibility instead of weakening the contract.
2. **Do not duplicate contract source in the UI host.** D5 already surfaced this as a review decision for queries. D6 must not repeat it for commands.
3. **Generated controllers remain gateway facades.** They call `IEventStoreGatewayClient`; no MediatR, DAPR actors, state stores, `DomainQueryDispatcher`, or direct aggregate invocation.
4. **Command body is authoritative.** Route `counterId` must match `body.CounterId`; generated code should fail closed with 400 on mismatch.
5. **CloseCounter is not a casual UI action.** Generate/prove its endpoint, but do not add a demo button without explicit product approval.

### Current Code State Read During Story Creation

| File | Current state | D6 change | Preserve |
|---|---|---|---|
| `samples/Hexalith.EventStore.Sample/Counter/Commands/*.cs` | Four empty records with XML summaries; no `ICommandContract`, no aggregate id, no `RestRoute`. | Add command contract metadata and routes. | Existing command names and aggregate semantics. |
| `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` | Dispatch dictionary is keyed by CLR short command type name only. | Add `ICommandContract.CommandType` alias lookup for Handle parameter types implementing the contract. | Existing short-name and assembly-qualified command type behavior. |
| `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterCommandForm.razor` | Uses named `EventStoreApi` HttpClient, direct `/api/v1/commands`, `Guid.NewGuid()`, anonymous JSON, PascalCase command strings. | Submit through generated typed command endpoint. | In-flight disabling, last-command feedback, support-safe errors, no projection subscription. |
| `samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj` | D5 currently compile-links `GetCounterStatusQuery.cs` into the UI host; analyzer and Client references exist. | Replace compile-link with supported referenced contract assembly model; add nothing with `Version=`. | Analyzer reference shape and existing SignalR/ServiceDefaults references. |
| `samples/Hexalith.EventStore.Sample.BlazorUI/RestApiAssemblyInfo.cs` | `[assembly: RestApi("api/{tenant}/counter", "counter", RestTenantSource.Route)]`. | Usually no change. | Route tenant source and route prefix. |
| `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs` | Generated command actions already use `SubmitCommandAsync`, ULID helper, route/body mismatch checks, `Retry-After`, and `Location`. | No generator change expected unless actual Sample command generation exposes a defect. | D3/D4 diagnostics and deterministic generation. |
| `src/Hexalith.EventStore.AppHost/DaprComponents/accesscontrol.yaml` | Allows `sample-blazor-ui` to invoke EventStore with GET/POST; comments still say "query UI". | No functional change expected. | DAPR service invocation policy; update comments only if directly touched for a real policy change. |

### Command Contract Shape

Preferred implementation pattern:

```csharp
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;

namespace Hexalith.EventStore.Sample.Counter.Commands;

/// <summary>
/// Command to increment the counter by one.
/// </summary>
[RestRoute(RestVerb.Post, "{counterId}/increment")]
public sealed record IncrementCounter(string CounterId = "counter-1") : ICommandContract
{
    public static string Domain => "counter";

    public static string CommandType => "increment-counter";

    public string AggregateId => CounterId;
}
```

Apply the same pattern to decrement, reset, and close with route/action names matching the command intent.

### Previous Story Intelligence

From D1:

- `ICommandContract.CommandType` and `Domain` are kebab-case and colon-free by contract.
- `RestRouteAttribute` is class-level and applies to both commands and queries.
- Empty route templates are allowed at the contract seam; route semantics are generator-owned.

From D2:

- Generator discovery uses metadata names and must not reference runtime EventStore assemblies.
- Manifest emission is deterministic evidence and should stay intact.

From D3:

- Generated command actions delegate through `IEventStoreGatewayClient.SubmitCommandAsync`.
- Generated command actions use `UniqueIdHelper.GenerateSortableUniqueStringId()`, not `Guid.NewGuid()`.
- Route/body mismatches are 400 problem details.
- Generated source should not call MediatR, DAPR actors, state stores, projection actors, or `DomainQueryDispatcher`.

From D4:

- Generator tests already cover command route/body mismatch, `Retry-After`, `Location`, tenant modes, duplicate route diagnostics, unsupported command route parameters, and generated source compilation.
- If D6 exposes a generator defect, add focused generator regression coverage rather than relying only on Sample smoke validation.

From D5:

- The query proof currently has unresolved review findings, especially compile-link contract duplication and generated endpoint consumption.
- D6 must avoid creating a second contract identity in the UI host and should not leave command proof weaker than D5.

### Git Intelligence

Recent commits at story creation:

- `7743b141 chore(release): 3.25.0 [skip ci]`
- `507e5821 feat: Update sprint status and project references`
- `89931299 Enhance CI/CD documentation and tooling for NuGet package management`
- `4d1a2071 chore: Update subproject commits for Hexalith references`
- `62db148f chore: Update CI/CD workflows, enhance dependency management, and improve test coverage`

The worktree already contains unrelated D5 review edits and a separate DAPR global ordering change. Do not revert or widen those changes while implementing D6.

### Latest Technical Notes

- ASP.NET Core 10 controller APIs are still controller classes derived from `ControllerBase`; generated controllers should keep the D3 shape with `[ApiController]` and route attributes. Source: https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-10.0
- Microsoft documents `[ProducesResponseType]` as the way to describe known HTTP status codes for web API help/OpenAPI surfaces; generated command actions should continue advertising 202 and problem responses through attributes where the generator supports them. Source: https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types?view=aspnetcore-10.0
- For Blazor Server, Microsoft notes that calling back into the same server over HTTP is usually unnecessary, but APIs may still be called through a service abstraction when a token or proxy transformation is required. D6 is a generator proof, so a small app-local client is acceptable if it is explicitly tokenized and guarded. Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/call-web-api?view=aspnetcore-10.0
- Microsoft's Blazor security guidance shows token handlers for outgoing API calls; reuse the existing `EventStoreApiAuthorizationHandler` pattern rather than exposing tokens in components. Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/additional-scenarios?view=aspnetcore-10.0
- `IIncrementalGenerator` instances must not store state because compiler lifetime is controlled by Roslyn. Do not introduce generator instance state if D6 needs a generator patch. Source: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.iincrementalgenerator?view=roslyn-dotnet-5.0.0
- The repo is pinned to `Microsoft.CodeAnalysis.CSharp` 5.3.0 in central package management; do not add project-local Roslyn versions. Source: https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/

### Testing and Verification Standards

- Use xUnit v3 and Shouldly for new tests.
- Run test projects individually; do not run solution-level `dotnet test`.
- Use `Hexalith.EventStore.slnx` only for restore/build.
- Always use `ConfigureAwait(false)` on awaited production-code calls.
- For any runtime/Aspire validation, verify command acceptance plus persisted/query-observable end state. A 202 alone is a smoke signal, not sufficient evidence.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md` Section 4, Epic D table] - D6 scope and sequence.
- [Source: `_bmad-output/implementation-artifacts/D-1-contract-seam.md`] - command contract and route attribute decisions.
- [Source: `_bmad-output/implementation-artifacts/D-2-generator-skeleton-spike.md`] - analyzer discovery constraints.
- [Source: `_bmad-output/implementation-artifacts/D-3-controller-emission.md`] - generated command controller behavior.
- [Source: `_bmad-output/implementation-artifacts/D-4-generator-tests.md`] - generator test expectations.
- [Source: `_bmad-output/implementation-artifacts/D-5-proof-sample-blazorui-queries.md`] - D5 review carryover.
- [Source: `samples/Hexalith.EventStore.Sample/Counter/Commands/IncrementCounter.cs`] - current empty command shape.
- [Source: `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterCommandForm.razor`] - current direct generic command submission.
- [Source: `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs`] - current short-name command dispatch.
- [Source: `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs`] - generated command action behavior.
- [Source: `samples/Hexalith.EventStore.Sample.BlazorUI/Program.cs`] - current EventStore gateway client and auth handler wiring.
- [Source: `references/Hexalith.AI.Tools/hexalith-ux-instructions.md`] - UI rules for FrontComposer/Fluent and no new raw controls/CSS.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

## Change Log

| Date | Change |
|---|---|
| 2026-07-02 | Story D6 created with Counter command contract adoption, generated command endpoint proof, D5 carryover guardrails, kebab-case dispatch compatibility requirement, UI generic-command-submission replacement, and verification requirements. Status ready-for-dev. |
