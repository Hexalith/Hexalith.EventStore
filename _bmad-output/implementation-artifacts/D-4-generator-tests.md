---
created: 2026-07-01
source_story_key: D-4-generator-tests
baseline_commit: 0fdadce70b76e68753bfd4db7a8041a6407e3359
---

# Story D.4: Generator Tests

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform maintainer for Hexalith.EventStore**,
I want **persistent Roslyn generator tests for the REST API source generator**,
so that **contract discovery, route handling, diagnostics, deterministic output, and generated controller compilation are protected before Sample and Tenants adopt the generator**.

## Story Context

This is **story D4 of Epic D - REST Controller Source Generator**. D1 added the contract seam (`ICommandContract`, `RestRouteAttribute`, `RestApiAttribute`). D2 added the analyzer project and manifest-only discovery spike. D3 is intended to add typed ASP.NET Core controller emission.

**Critical preflight:** D4 must only verify already-owned generator behavior. Before implementing D4, inspect `src/Hexalith.EventStore.RestApi.Generators/`. At story creation time, the source on disk was still manifest/discovery-only:

- `RestApiGenerator` registers `RestApiMessageDiscovery`, parses `[assembly: RestApi(...)]`, collects message descriptors, and emits only `HexalithEventStoreRestApiGeneratorManifest.g.cs`.
- No controller emitter, action descriptor, route-template parser, diagnostic descriptor catalog, `IEventStoreGatewayClient` emission, or `[ApiController]` generated source was present.
- `git log` contained `feat: Implement D3 controller emission for Hexalith.EventStore`, but the source inspection did not show D3 controller-emission code. Do not trust the commit title or sprint status alone.

If D3 controller emission is still absent when D4 starts, stop and correct the sprint/story state or implement D3 through the D3 story first. Do **not** turn D4 into a hidden D3 implementation story. D4 may add D2 manifest/discovery regression tests only if the team explicitly chooses a split, but it cannot claim D4 complete without the D3 controller-emission test coverage described below.

Source of truth: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md` Section 4, Epic D row for D4, plus D1-D3 story files and the current generator/client contracts.

## Acceptance Criteria

1. **Persistent generator test project exists and is wired into the solution.**
   - Create `tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj`.
   - Add the test project under the `/tests/` folder in `Hexalith.EventStore.slnx`.
   - The project targets the repo default `net10.0`, is non-packable through `tests/Directory.Build.props`, and uses xUnit v3 plus Shouldly.
   - Reference `src/Hexalith.EventStore.RestApi.Generators`, `src/Hexalith.EventStore.Contracts`, and `src/Hexalith.EventStore.Client`.
   - Add versionless `PackageReference` entries only as needed, especially `Microsoft.CodeAnalysis.CSharp`, `Microsoft.NET.Test.Sdk`, `xunit.v3`, `xunit.runner.visualstudio`, `Shouldly`, and `coverlet.collector`.
   - Add `FrameworkReference Include="Microsoft.AspNetCore.App"` when controller-generation compile tests need ASP.NET Core MVC/Authorization metadata.
   - Do not add Verify/snapshot packages unless the implementation deliberately chooses snapshot approval tests and updates central package versions with a clear reason. Direct source assertions are sufficient for D4.

2. **A focused `CSharpGeneratorDriver` test harness exists.**
   - Create a small helper, recommended `RestApiGeneratorTestHarness`, that builds `CSharpCompilation` inputs from inline source strings.
   - The helper must reference runtime assemblies needed by the generated source: `System.Runtime`, `netstandard`, `System.Collections`, `System.Linq`, `System.Text.Json`, EventStore Contracts, EventStore Client, ASP.NET Core MVC/Authorization abstractions when controller tests run, and `Hexalith.Commons.UniqueIds` if generated command code references `UniqueIdHelper`.
   - Use `new RestApiGenerator()` and `CSharpGeneratorDriver.Create(...)`. If the Roslyn overload requires source-generator adaptation, use `generator.AsSourceGenerator()` as in the FrontComposer precedent.
   - Use `TestContext.Current.CancellationToken` and pass cancellation through compilation/driver operations.
   - Expose helpers to return `GeneratorDriverRunResult`, generated source by hint-name suffix, diagnostics, and a compilation that includes generated trees for compile validation.
   - Keep the helper test-only; do not add test helpers to the production generator project.

3. **D2 manifest/discovery behavior is regression-tested.**
   - When a compilation does not have `[assembly: RestApi(...)]`, no manifest or controller source is emitted.
   - When a compilation opts in with `[assembly: RestApi("api/counter", "counter", RestTenantSource.System)]`, one command implementing `ICommandContract`, and one query implementing `IQueryContract`, the manifest is emitted and records:
     - `RestApiAttributeFound = true`
     - route prefix, tag, and tenant source
     - command count and query count
     - fully-qualified command/query type names
     - route override count and route override values.
   - Non-marker classes/records with `[RestRoute]` are ignored.
   - Command-only, query-only, and mixed command/query compilations produce correct deterministic counts.
   - Empty and whitespace route templates remain accepted by the discovery layer; D1 intentionally allows them.
   - Repeated runs over identical input produce byte-identical generated output with no timestamps, absolute paths, machine names, random IDs, or temporary directory paths.

4. **Incremental-generator behavior is covered where it matters.**
   - Use `GeneratorDriverOptions(trackIncrementalGeneratorSteps: true)` or the current Roslyn equivalent.
   - Assert the tracked steps include the local names `RestApiMessageDiscovery` and `RestApiOptions`.
   - An unrelated syntax-tree edit should leave existing message discovery outputs cached or unchanged.
   - Adding a new command/query marker should produce a new discovery output and preserve existing cached/unchanged outputs.
   - These tests are guardrails only; do not overfit to every Roslyn internal reason value if a stable assertion on cached/unchanged/new outputs is enough.

5. **D3 generated controller source compiles in memory.**
   - Once D3 exists, add tests that run the generator against an opted-in test domain containing at least:
     - one command with `[RestRoute(RestVerb.Post, "{counterId}/increment")]` and `AggregateId => CounterId`
     - one query with `[RestRoute(RestVerb.Get, "{counterId}")]`
     - one query using convention fallback
     - one command using an absolute route template beginning with `~/`.
   - Add generated trees back into the compilation and assert no compile diagnostics with severity `Error`.
   - Assert generated source contains `[ApiController]`, `[Authorize]`, `[Route("api/counter")]`, `[Tags("counter")]`, `IEventStoreGatewayClient`, `SubmitCommandAsync`, `SubmitQueryAsync`, `ConfigureAwait(false)`, and `UniqueIdHelper.GenerateSortableUniqueStringId()`.
   - Assert generated source does **not** contain `Type.GetType`, `Assembly.Load`, `Activator.CreateInstance`, `DomainQueryDispatcher`, DAPR clients, projection actors, state stores, or direct MediatR dispatch.
   - Generated controllers must call the gateway client, not bypass the gateway.

6. **Route, binding, tenant, and HTTP mapping behavior is asserted at the generator boundary.**
   - Relative `[RestRoute]` templates are emitted relative to the controller route prefix.
   - Absolute `~/` templates are emitted as absolute ASP.NET Core route templates.
   - Empty string templates route at the controller prefix root.
   - Commands without `[RestRoute]` default to `POST` at the prefix root.
   - Queries without `[RestRoute]` default to `GET` at the prefix root only when the payload can be built without an unsupported body.
   - Command actions include route parameters, `[FromBody]` body, `CancellationToken`, null-body 400 handling, route/body mismatch 400 handling, `SubmitCommandRequest` construction, `Retry-After: 1`, and `Location` pointing to `/api/v1/commands/status/{correlationId}`.
   - Query actions include route parameters, supported `[FromQuery]` values, `[FromHeader(Name = "If-None-Match")]`, `SubmitQueryRequest` construction, 304 handling, strong ETag copy, and raw payload return for 200.
   - `RestTenantSource.System`, `Route`, and `Claims` are each covered. Route mode without `tenant` or `tenantId` must be a generator diagnostic; claims mode must not parse bearer tokens or raw JWT payloads in generated code.

7. **Misuse diagnostics are tested with stable IDs.**
   - Assert diagnostics for unsupported ambiguous query route shapes, especially multiple non-tenant route parameters without explicit `aggregateId`/`entityId` mapping.
   - Assert diagnostics for `RestTenantSource.Route` when no `tenant` or `tenantId` route parameter exists.
   - Assert diagnostics for any query shape D3 cannot bind deterministically instead of allowing generated code that guesses.
   - If D3 has not introduced stable diagnostic IDs/descriptors, add or require them before writing diagnostic assertions. Do not assert only on free-form diagnostic text.
   - Diagnostic tests must also assert that unsupported shapes do not produce a broken controller action.

8. **Scope remains test-focused and contained.**
   - Do not annotate Sample/Counter contracts, remove `CounterQueryService`, edit Tenants, delete `TenantsQueryController`, update release package inventory, or change docs in D4.
   - Do not modify submodule files.
   - Do not add AppHost, Aspire, DAPR component, container, UI/UX, or runtime gateway behavior changes.
   - Production generator fixes are allowed only when D4 tests expose a defect in D2/D3-owned generator behavior. Do not implement D3 from scratch under D4.
   - No generated `bin/`, `obj/`, compiler-generated files, or temporary smoke projects are committed.

9. **Verification proves the test project works.**
   - `dotnet build src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj --configuration Release` succeeds.
   - `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` succeeds.
   - `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` succeeds unless a pre-existing unrelated blocker is documented with exact output.
   - Do not run solution-level `dotnet test`.

## Tasks / Subtasks

- [x] **Task 1: Preflight D3 and generator state** (AC: 5, 6, 7, 8)
  - [x] Inspect `src/Hexalith.EventStore.RestApi.Generators/` for controller emitter, action descriptors, route parser, and diagnostic descriptors.
  - [x] If only manifest emission exists, stop and correct/finish D3 before claiming D4 can complete.
  - [x] Record the inspected files and conclusion in the Dev Agent Record.

- [x] **Task 2: Add the persistent generator test project** (AC: 1)
  - [x] Create `tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj`.
  - [x] Add required project/package references with central package versions only.
  - [x] Add `Microsoft.AspNetCore.App` framework reference for generated controller compile tests if needed.
  - [x] Add the project to `Hexalith.EventStore.slnx` under `/tests/`.

- [x] **Task 3: Build the Roslyn test harness** (AC: 2)
  - [x] Add a compilation helper with metadata references for Contracts, Client, ASP.NET Core, System.Text.Json, and UniqueIds as needed.
  - [x] Add helpers to run `RestApiGenerator`, fetch generated sources, fetch diagnostics, and compile generated output.
  - [x] Keep helper APIs minimal and deterministic.

- [x] **Task 4: Add D2 manifest/discovery regression tests** (AC: 3, 4)
  - [x] Cover no opt-in, command-only, query-only, mixed command/query, route overrides, non-marker ignored, empty/whitespace template acceptance, deterministic output, and incremental tracking.

- [x] **Task 5: Add D3 controller compile and source-shape tests** (AC: 5, 6)
  - [x] Cover command and query generated controllers with relative, absolute, empty, and convention fallback routes.
  - [x] Add generated syntax trees back to the compilation and assert no compiler errors.
  - [x] Assert gateway delegation and forbidden-bypass strings.

- [x] **Task 6: Add misuse diagnostic tests** (AC: 7)
  - [x] Cover ambiguous query route shapes.
  - [x] Cover route-tenant mode without tenant route parameter.
  - [x] Cover any unsupported body/query shape D3 reports.
  - [x] Assert stable diagnostic IDs and no broken action emission for invalid shapes.

- [x] **Task 7: Verify and clean the worktree** (AC: 8, 9)
  - [x] Build the generator project in Release.
  - [x] Run the new generator test project individually.
  - [x] Build `Hexalith.EventStore.slnx` in Release package mode.
  - [x] Confirm `git status --short` contains only intended source/story/sprint-status changes and no generated outputs.

### Review Findings

- [x] [Review][Patch] Compile-check convention command and claims tenant controller branches [tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs:70]
- [x] [Review][Patch] Assert duplicate-route diagnostics suppress the invalid duplicate action [tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiDiagnosticTests.cs:95]
- [x] [Review][Patch] Reject unexpected diagnostics in positive manifest and no-opt-in tests [tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiManifestGenerationTests.cs:8]
- [x] [Review][Patch] Reject unexpected extra errors in diagnostic tests [tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiDiagnosticTests.cs:138]
- [x] [Review][Patch] Assert the required `{counterId}` query route/action from the happy-path fixture [tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs:246]
- [x] [Review][Patch] Prove command 400 responses and command-status Location include the required status/correlation behavior [tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs:42]
- [x] [Review][Patch] Extend deterministic-output guard to cover absolute paths and actual random ID values [tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiManifestGenerationTests.cs:93]

## Dev Notes

### Top Guardrails

1. **D4 tests must not hide missing D3 implementation.** If there is no controller-emission source, do not implement it inside D4. Return to D3 or split D4 into a D2-only test slice explicitly.
2. **Test the generator through Roslyn.** Prefer `CSharpGeneratorDriver` and in-memory compilation over direct calls to internal parser/emitter methods. The dev agent should exercise the same public generator entry point that consumers use.
3. **No snapshot framework by default.** The repo already has central pins for Roslyn and test packages. Direct source assertions avoid adding Verify package management just for D4.
4. **Generated source must compile.** String assertions alone are not enough for controller emission; add generated trees to a compilation with the required references and fail on compiler errors.
5. **Gateway remains the boundary.** Tests should make it hard for future generator changes to route directly to MediatR, DAPR, actors, state stores, or domain query dispatchers.

### Files to Create

| File | Purpose |
|---|---|
| `tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj` | Persistent xUnit v3 test project for the generator |
| `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratorTestHarness.cs` | Roslyn compilation/driver helper |
| `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiManifestGenerationTests.cs` | D2 manifest/discovery regression tests |
| `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiIncrementalGenerationTests.cs` | Incremental tracking/determinism tests |
| `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs` | D3 generated controller compile/source-shape tests |
| `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiDiagnosticTests.cs` | Misuse diagnostic tests |

Use one primary C# type per file. Test fixture helper records/classes should be private or internal to the test project.

### Files to Update

| File | Current state | D4 change | Preserve |
|---|---|---|---|
| `Hexalith.EventStore.slnx` | XML solution format with existing `/tests/` folder entries; no generator test project entry. | Add `tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj` under `/tests/`. | Existing ordering/style; never create or use `.sln`. |
| `Directory.Packages.props` | Already contains `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Analyzers` `5.3.0`, plus xUnit v3 and Shouldly pins. | Usually no change. Only update if a deliberately chosen test package is missing. | No package versions in `.csproj`; do not add Verify by accident. |
| `src/Hexalith.EventStore.RestApi.Generators/*` | At story creation, manifest/discovery-only source was present. D3 code may exist by implementation time. | Read for test expectations. Patch only defects exposed by tests in already-owned D2/D3 behavior. | Analyzer-only project; no runtime references; no generator instance state; metadata-name discovery. |

### Current Generator State Read During Story Creation

- `RestApiGenerator.cs`: `IIncrementalGenerator`; collects message descriptors and options; emits only `RestApiManifestEmitter.HintName`.
- `RestApiMessageParser.cs`: candidate filter for class/record with base list; symbol checks against `ICommandContract` and `IQueryContract`; parses optional `RestRouteAttribute`.
- `RestApiManifestEmitter.cs`: emits deterministic internal manifest class with route prefix, tag, tenant source, command/query counts, and route overrides.
- `RestApiAttributeParser.cs`: reads assembly-level `RestApiAttribute`.
- `RestApiMessageDescriptor.cs`, `RestApiRouteDescriptor.cs`, `RestApiOptions.cs`: pure equatable value structs.
- `Hexalith.EventStore.RestApi.Generators.csproj`: `netstandard2.0`, `IsRoslynComponent=true`, `EnforceExtendedAnalyzerRules=true`, analyzer package path `analyzers/dotnet/cs`, Roslyn packages private.

### Test Harness Shape

Recommended helper behavior:

```csharp
internal static class RestApiGeneratorTestHarness
{
    internal static GeneratorDriverRunResult Run(params string[] sources)
    {
        CSharpCompilation compilation = CreateCompilation(sources);
        var generator = new RestApiGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        return driver.GetRunResult();
    }
}
```

Adjust the exact overloads to the installed Roslyn API. Keep the helper small; avoid global mutable state.

### Inline Source Fixtures

Use small inline sources instead of real Sample/Tenants contracts. This avoids submodule edits and keeps failures local. A happy-path source should include:

```csharp
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Rest;

[assembly: RestApi("api/counter", "counter", RestTenantSource.System)]

namespace Smoke;

[RestRoute(RestVerb.Post, "{counterId}/increment")]
public sealed record IncrementCounter(string CounterId, int Amount) : ICommandContract
{
    public static string Domain => "counter";
    public static string CommandType => "increment-counter";
    public string AggregateId => CounterId;
}

[RestRoute(RestVerb.Get, "{counterId}")]
public sealed record GetCounterStatus(string CounterId) : IQueryContract
{
    public static string QueryType => "get-counter-status";
    public static string Domain => "counter";
    public static string ProjectionType => "counter-status";
}
```

Add separate fixtures for convention fallback, absolute routes, `RestTenantSource.Claims`, `RestTenantSource.Route`, non-marker classes, and invalid diagnostic cases.

### Previous Story Intelligence

From D1:

- Contracts intentionally allow empty/whitespace route templates and route prefixes; generator tests must not reintroduce null/whitespace rejection at the contract seam.
- `ICommandContract` and REST attributes are public contract APIs; tests should use them through source snippets, not copy their definitions.

From D2:

- The analyzer must not reference runtime EventStore assemblies except through the consuming compilation. The test project may reference Contracts/Client, but production generator code must stay metadata-name based.
- Manifest output is deterministic evidence and should remain covered even after D3 adds controllers.
- Current generator tracking names are `RestApiMessageDiscovery` and `RestApiOptions`; D4 can lock these in if tests use incremental-step assertions.

From D3:

- D3 is supposed to generate gateway-backed typed controllers. D4 must verify the generated source compiles and calls `IEventStoreGatewayClient`.
- D3 explicitly excludes Sample/Tenants adoption and packaging/docs changes; D4 should preserve that boundary.
- D3 expected diagnostics for unsupported route/query/tenant shapes. D4 should convert those expectations into stable tests.

### Git Intelligence

Recent commits observed while creating D4:

- `294aab40 feat: Implement D3 controller emission for Hexalith.EventStore`
- `c30e6d23 chore(deps): update HexalithCommonsUniqueIdsVersion to 2.24.2 and add Microsoft.OpenApi package`
- `9cee4d3d chore: update sprint change proposals and project context with approval statuses`
- `f4f9bd69 chore(deps): update Hexalith dependency references`
- `84ac5b41 chore(workflows): consolidate ci and release automation`

Actionable implications:

- Validate Release package mode with `-p:UseHexalithProjectReferences=false`.
- Do not broaden release packaging in D4; D8 owns analyzer package publication.
- Because the D3 commit title conflicts with the source inspection, trust code and tests over status labels.

### Latest Technical Notes

- NuGet lists `Microsoft.CodeAnalysis.CSharp` `5.3.0`; the package page shows compatibility with `netstandard2.0` and modern .NET targets, matching the generator's `netstandard2.0` target and test usage.
- NuGet lists `Microsoft.CodeAnalysis.Analyzers` `5.3.0`; its description targets authors of analyzers/code fixes/tools built on `Microsoft.CodeAnalysis`, matching this generator project. Keep it `PrivateAssets="all"`.
- Microsoft Learn's `CSharpGeneratorDriver.Create` docs include overloads for `IIncrementalGenerator[]`, `ISourceGenerator`, additional texts, parse options, analyzer config options, and driver options. Use the overload matching the installed Roslyn package.
- Microsoft Learn's `IIncrementalGenerator` docs state that generator lifetime is compiler-controlled and generator instances must not store state.
- Roslyn's incremental-generator docs describe an immutable pipeline defined in `Initialize`; the cookbook provides common generator patterns. D4 tests should exercise the pipeline rather than internal mutable state.

### Testing and Verification Standards

- Use xUnit v3 and Shouldly. Do not use raw `Assert.*`.
- Run test projects individually:
  - `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/`
- Use `.slnx` only for restore/build:
  - `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false`
- Do not run solution-level `dotnet test`.
- `Hexalith.EventStore.Server.Tests` historical CA2007 warning is not part of D4 validation.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md` Section 4, Epic D table] - D4 scope and sequence.
- [Source: `_bmad-output/implementation-artifacts/D-1-contract-seam.md`] - D1 contract seam decisions and empty-template validation rule.
- [Source: `_bmad-output/implementation-artifacts/D-2-generator-skeleton-spike.md`] - D2 analyzer/discovery/manifest baseline.
- [Source: `_bmad-output/implementation-artifacts/D-3-controller-emission.md`] - D3 generated-controller and diagnostics expectations.
- [Source: `src/Hexalith.EventStore.RestApi.Generators/RestApiGenerator.cs`] - current generator entry point and tracking names.
- [Source: `src/Hexalith.EventStore.RestApi.Generators/RestApiMessageParser.cs`] - command/query discovery and route parsing.
- [Source: `src/Hexalith.EventStore.RestApi.Generators/RestApiManifestEmitter.cs`] - manifest output contract.
- [Source: `src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs`] - generated controller gateway dependency.
- [Source: `src/Hexalith.EventStore.Contracts/Commands/SubmitCommandRequest.cs`] - command request shape.
- [Source: `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs`] - query request shape.
- [Source: `references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.SourceTools.Tests/CompilationHelper.cs`] - local Roslyn compilation-helper precedent.
- [Source: `references/Hexalith.FrontComposer/tests/Hexalith.FrontComposer.SourceTools.Tests/Caching/IncrementalCachingTests.cs`] - local `CSharpGeneratorDriver` incremental-test precedent.
- [NuGet: Microsoft.CodeAnalysis.CSharp](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/) - current Roslyn C# package and target-framework compatibility.
- [NuGet: Microsoft.CodeAnalysis.Analyzers](https://www.nuget.org/packages/Microsoft.CodeAnalysis.Analyzers/) - analyzer API rules package.
- [Microsoft Learn: CSharpGeneratorDriver.Create](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.csharpgeneratordriver.create?view=roslyn-dotnet-5.0.0) - generator-driver test API.
- [Microsoft Learn: IIncrementalGenerator](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.iincrementalgenerator?view=roslyn-dotnet-5.0.0) - generator lifetime and no-instance-state rule.
- [Roslyn incremental generators design](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md) - immutable incremental pipeline model.
- [Roslyn incremental generator cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md) - common incremental-generator patterns.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-07-02: Task 1 preflight inspected `RestApiGenerator.cs`, `RestApiControllerEmitter.cs`, `RestApiRouteTemplateParser.cs`, `RestApiDiagnosticDescriptors.cs`, `RestApiMessageParser.cs`, and `RestApiManifestEmitter.cs`. D3 controller emission is present, gateway-backed, and includes route parsing plus stable diagnostics `HESREST001`-`HESREST005`; D4 can proceed as a test story.
- 2026-07-02: Added `Hexalith.EventStore.RestApi.Generators.Tests` with Roslyn generator harness and 21 tests covering manifest discovery, incremental tracking, controller compile/source shape, tenant modes, and misuse diagnostics. `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` passed.
- 2026-07-02: Aspire preflight ran with `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`; `aspire describe` reported EventStore, Admin, Admin UI, Sample, Sample Blazor UI, and sidecars running healthy; AppHost was stopped before code edits continued.
- 2026-07-02: Required D4 validation passed: `dotnet build src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj --configuration Release`, `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/`, and `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false`.
- 2026-07-02: Regression validation passed for documented Tier 1 tests plus additional unit projects: Contracts, Client, Sample, SignalR, Testing, Admin.Abstractions, Admin.Cli, Admin.Mcp, Admin.Server, Admin.Server.Host, Admin.UI, AppHost, DomainService, QueryRouting, and Testing.Integration. Non-baseline ATDD/governance projects `DeferredWorkGovernance.Tests` and `OperationalEvidence.Validator.Tests` failed due missing unrelated DW4/DW6 artifacts/entrypoints; D4 files do not touch those areas.
- 2026-07-02: `git status --short --untracked-files=all` showed D4-owned files plus pre-existing unrelated changes in `.github/workflows/*`, `D-3-controller-emission.md`, and `ContractsPackageDependencyTests.cs`; no generated `bin`, `obj`, or `TestResults` outputs are tracked.

### Implementation Plan

- Add a persistent xUnit v3 generator test project with versionless central package references and a `/tests/` solution entry.
- Exercise `RestApiGenerator` through `CSharpGeneratorDriver` using inline source fixtures and generated-tree compile validation.
- Cover D2 manifest/discovery and D3 controller/diagnostic behavior without editing Sample, Tenants, AppHost, runtime gateway behavior, or submodules.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added persistent xUnit v3 generator test project and `.slnx` entry under `/tests/`.
- Added Roslyn `CSharpGeneratorDriver` harness with metadata references for Contracts, Client, ASP.NET Core MVC/HTTP, System.Text.Json, and UniqueIds.
- Added D2 manifest/discovery and incremental guardrail tests.
- Added D3 generated controller compile/source-shape tests covering gateway delegation, route handling, tenant modes, ETag/304 handling, and forbidden bypass strings.
- Added misuse diagnostic tests for stable IDs `HESREST001`, `HESREST002`, and `HESREST005`, including no broken invalid action emission.
- Verified D4 acceptance criteria with Release generator build, focused generator tests, and Release package-mode solution build.

### File List

- `_bmad-output/implementation-artifacts/D-4-generator-tests.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `Hexalith.EventStore.slnx`
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj`
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs`
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiDiagnosticTests.cs`
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratorTestHarness.cs`
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiIncrementalGenerationTests.cs`
- `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiManifestGenerationTests.cs`

## Change Log

| Date | Change |
|---|---|
| 2026-07-01 | Story D4 created with generator test project scope, Roslyn harness guidance, D2 regression coverage, D3 controller compile/diagnostic guardrails, and current-source preflight warning. Status ready-for-dev. |
| 2026-07-02 | Started D4 implementation, recorded baseline commit, marked sprint status in-progress, and completed D3 preflight. |
| 2026-07-02 | Added persistent REST API generator test project, Roslyn harness, manifest/incremental/controller/diagnostic coverage, and verification evidence. Status review. |
