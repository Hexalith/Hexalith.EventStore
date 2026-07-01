---
baseline_commit: 9cee4d3db5fe6f12524ac22a357116582444fedd
---

# Story D.2: Generator Skeleton + Spike

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **domain-module author building on Hexalith.EventStore**,
I want **the first EventStore REST API Roslyn generator project to compile, discover REST-enabled command/query contracts, and emit a harmless manifest artifact**,
so that **the Epic D controller generator toolchain is proven before D3 adds real controller emission**.

## Story Context

This is **story D2 of Epic D - REST Controller Source Generator**. D1 created the public contract seam:

- `ICommandContract`
- `RestRouteAttribute` / `RestVerb`
- `RestApiAttribute` / `RestTenantSource`

D2 must consume that seam by symbol metadata name and prove the analyzer project shape. It must **not** emit controllers, route binding code, HTTP actions, gateway calls, diagnostics for route misuse, sample adoption, Tenants adoption, or release-pipeline package registration. Those belong to later stories:

- **D3** - generated controller/action emission and HTTP mapping.
- **D4** - persistent generator test project and `CSharpGeneratorDriver`/snapshot coverage.
- **D5/D6** - in-repo Sample/Counter adoption.
- **D7** - Tenants UI host adoption in the submodule.
- **D8** - release package inventory, docs, and guardrails.

Source of truth: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md` Section 4, Epic D row for D2, plus the D1 story file and current contract/client code.

## Acceptance Criteria

1. **New generator project exists and builds as a Roslyn component.**
   - Create `src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj`.
   - Project targets `netstandard2.0`.
   - Project sets `IsRoslynComponent=true` and `EnforceExtendedAnalyzerRules=true`.
   - Project is packable as an analyzer package shape: analyzer DLL is packed under `analyzers/dotnet/cs`, build output is not packed as `lib/`, and package dependencies are suppressed/hidden as appropriate for analyzer-only packages.
   - Do **not** set `GeneratePackageOnBuild=true`; explicit pack/release registration is D8 and the release pipeline already disables implicit package generation.
   - Do **not** add copyright headers.

2. **Roslyn package pins are added centrally.**
   - Update `Directory.Packages.props` with `Microsoft.CodeAnalysis.CSharp` version `5.3.0`.
   - Update `Directory.Packages.props` with `Microsoft.CodeAnalysis.Analyzers` version `5.3.0`.
   - The generator `.csproj` uses versionless `<PackageReference />` entries with `PrivateAssets="all"` and does not put `Version=` in the project file.
   - Do not add Roslyn package references to unrelated projects.

3. **The solution includes the generator project using the modern solution format only.**
   - Update `Hexalith.EventStore.slnx` under the `/src/` folder with the new generator project.
   - Do not create, edit, or use `.sln` files.
   - Do not add the D4 test project in this story.

4. **Generator skeleton uses incremental-generator APIs and discovers D1 contract markers.**
   - Add a generator type, recommended name `RestApiGenerator`, marked with `[Generator(LanguageNames.CSharp)]` or `[Generator]` and implementing `IIncrementalGenerator`.
   - Discovery identifies class/record declarations implementing:
     - `Hexalith.EventStore.Contracts.Commands.ICommandContract`
     - `Hexalith.EventStore.Contracts.Queries.IQueryContract`
   - Discovery reads assembly-level `Hexalith.EventStore.Contracts.Rest.RestApiAttribute` when present.
   - Discovery reads optional `Hexalith.EventStore.Contracts.Rest.RestRouteAttribute` metadata when present.
   - Use metadata-name string constants and Roslyn symbols. Do **not** add a project/package reference from the generator to `Hexalith.EventStore.Contracts`, `Hexalith.EventStore.Client`, ASP.NET Core, or the Sample.
   - Do not use runtime reflection, assembly loading, or `Type.GetType`.

5. **The spike emits only a harmless, deterministic manifest artifact.**
   - Emit one deterministic generated file, recommended hint name `HexalithEventStoreRestApiGeneratorManifest.g.cs`.
   - The generated file contains an auto-generated header, `#nullable enable`, a deterministic namespace such as `Hexalith.EventStore.RestApi.Generated`, and an `internal static` manifest class.
   - The manifest records enough compile-time evidence to prove the toolchain, such as:
     - whether `[assembly: RestApi(...)]` was found,
     - discovered command count,
     - discovered query count,
     - fully qualified discovered type names,
     - route override count or names.
   - The manifest must not contain timestamps, absolute local paths, machine names, random ids, or any source text that changes between identical builds.
   - The manifest must not emit controllers, endpoints, `[ApiController]`, `[Authorize]`, `IEventStoreGatewayClient` calls, JSON serialization, route/body binding, or HTTP result mapping.

6. **D2 preserves D1 decisions and does not pre-validate D3 behavior.**
   - Empty or whitespace `RestRouteAttribute.Template` and `RestApiAttribute.RoutePrefix` values remain accepted by the contract seam; D2 must not report diagnostics for them.
   - Kebab-case/static-member validation remains conceptually aligned with `CommandContractResolver` and `QueryContractResolver`, but the generator should not try to execute static property getters at compile time.
   - Generated future code should reference `Type.Domain`, `Type.CommandType`, `Type.QueryType`, and `Type.ProjectionType` at compile time rather than attempting to evaluate arbitrary property bodies in the generator.
   - Route-template validation, route/body mismatch handling, tenant resolution, result mapping, and misuse diagnostics are explicitly deferred to D3/D4.

7. **Scope stays additive and contained.**
   - No changes to `ICommandContract`, `IQueryContract`, `RestRouteAttribute`, `RestApiAttribute`, client resolvers, gateway controllers, AppHost, samples, Tenants submodule, docs, `.releaserc.json`, or GitHub workflows.
   - No new runtime dependencies are introduced to EventStore packages.
   - No generated artifacts under `obj/`, `bin/`, or smoke-test temp folders are committed.

8. **Verification proves build and generator execution.**
   - `dotnet restore Hexalith.EventStore.slnx` succeeds.
   - `dotnet build src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj --configuration Release` succeeds with zero warnings under `TreatWarningsAsErrors=true`.
   - `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` succeeds.
   - A non-committed smoke consumer build runs the analyzer and proves the manifest is emitted for a tiny project that references local Contracts and declares `[assembly: RestApi(...)]` plus one query and one command marker type. The completion notes must include the exact smoke command/path and the generated manifest path or evidence.

## Tasks / Subtasks

- [x] **Task 1: Add central Roslyn package versions** (AC: 2)
  - [x] Add `Microsoft.CodeAnalysis.CSharp` `5.3.0` to `Directory.Packages.props`.
  - [x] Add `Microsoft.CodeAnalysis.Analyzers` `5.3.0` to `Directory.Packages.props`.
  - [x] Keep package versions out of `.csproj` files.

- [x] **Task 2: Add the generator project scaffold** (AC: 1, 3, 7)
  - [x] Create `src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj`.
  - [x] Configure it as `netstandard2.0`, `IsRoslynComponent`, `EnforceExtendedAnalyzerRules`.
  - [x] Configure analyzer-only packaging using `analyzers/dotnet/cs`, modeled on the PolymorphicSerializations generator but without implicit `GeneratePackageOnBuild`.
  - [x] Add versionless Roslyn package references with `PrivateAssets="all"`.
  - [x] Add the project to `Hexalith.EventStore.slnx` under `/src/`.

- [x] **Task 3: Implement the incremental discovery skeleton** (AC: 4, 6)
  - [x] Add `RestApiGenerator` implementing `IIncrementalGenerator`.
  - [x] Use metadata-name constants for `ICommandContract`, `IQueryContract`, `RestApiAttribute`, and `RestRouteAttribute`.
  - [x] Discover class/record declarations with a base list and semantic interface matching.
  - [x] Parse assembly-level `RestApiAttribute` options from `Compilation.Assembly.GetAttributes()`.
  - [x] Parse optional `RestRouteAttribute` constructor values from discovered message symbols.
  - [x] Keep discovered model data pure and deterministic; do not let `ISymbol` objects leak into emitted model records if those records are used as incremental cache keys.

- [x] **Task 4: Emit the D2 manifest only** (AC: 5, 6)
  - [x] Emit a single deterministic `*.g.cs` manifest source when a compilation opts in with `[assembly: RestApi(...)]`.
  - [x] Include discovered counts and type names, not controllers or runtime logic.
  - [x] Ensure generated code compiles under nullable enabled.
  - [x] Ensure generated output contains no timestamps, absolute paths, or machine-specific values.

- [x] **Task 5: Verify build and smoke execution** (AC: 8)
  - [x] Run restore.
  - [x] Build the generator project in Release.
  - [x] Build the full solution in Release package mode.
  - [x] Run a throwaway smoke consumer build from `/tmp` or another untracked path using `EmitCompilerGeneratedFiles=true`; confirm the manifest file exists and records the expected command/query counts.
  - [x] Confirm `git status --short` contains only intended source/story/sprint-status changes and no generated `bin/`, `obj/`, or temp files.

### Review Findings

- [x] [Review][Patch] Partial declarations can duplicate manifest entries [src/Hexalith.EventStore.RestApi.Generators/RestApiGenerator.cs:26]

## Dev Notes

### Top Guardrails

1. **D2 is a toolchain spike, not controller generation.** If generated code contains `[ApiController]`, `IEventStoreGatewayClient`, `SubmitCommandRequest`, `SubmitQueryRequest`, route/body binding, HTTP status mapping, or tenant resolution, the story has crossed into D3.
2. **Do not reference runtime EventStore assemblies from the analyzer.** A published analyzer package should not require `Hexalith.EventStore.Contracts.dll` or `Hexalith.EventStore.Client.dll` to load in the compiler host. Use metadata-name strings and inspect the consuming compilation's references.
3. **Do not update the release package inventory yet.** D8 owns `.releaserc.json`, package-count docs, and package-governance tests. D2 may make the project packable in analyzer shape, but semantic-release must not start publishing it in this story.
4. **No submodule edits.** `references/Hexalith.*` content is read-only unless the user explicitly asks for submodule work. D7 will handle Tenants separately.
5. **No persistent generated files.** Commit source only. Generated `obj/.../generated/...` smoke output is evidence, not an artifact.

### Files To Create

| File | Purpose |
|---|---|
| `src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj` | New Roslyn component/analyzer package project |
| `src/Hexalith.EventStore.RestApi.Generators/RestApiGenerator.cs` | `IIncrementalGenerator` entry point |
| `src/Hexalith.EventStore.RestApi.Generators/...` | Small internal model/emitter files as needed |

Keep each C# file to one primary type. If you add records/enums such as `RestApiMessageDescriptor`, `RestApiRouteDescriptor`, or `RestApiMessageKind`, put each in its own file.

### Files To Update

| File | Current State | Required D2 Change | Preserve |
|---|---|---|---|
| `Directory.Packages.props` | Central package management is enabled. Roslyn packages are not currently listed in this repo, but `references/Hexalith.Builds/Props/Directory.Packages.props` uses `Microsoft.CodeAnalysis.*` `5.3.0`. | Add only the Roslyn package pins needed by the generator. | Existing package groups, external Hexalith package pins, and versionless project references. |
| `Hexalith.EventStore.slnx` | XML solution format with `/src/` and `/tests/` folders; no generator project exists. | Add the generator project under `/src/`. | Existing ordering/style and all existing project entries. Do not create `.sln`. |

### Existing Contract Seam

D1 implemented these files:

- `src/Hexalith.EventStore.Contracts/Commands/ICommandContract.cs`
- `src/Hexalith.EventStore.Contracts/Rest/RestRouteAttribute.cs`
- `src/Hexalith.EventStore.Contracts/Rest/RestApiAttribute.cs`

Important D1 decisions to preserve:

- `RestRouteAttribute` targets classes, can apply to both command and query contracts, and accepts empty/whitespace templates; only `null` is rejected.
- `RestApiAttribute` is assembly-level and accepts `routePrefix`, optional `tag`, and `tenantSource`; only `null` route prefix is rejected.
- No generator/route-computation code exists in D1.
- D1 contract source uses same-line braces in the Contracts project. The new generator project can follow normal repo style, but do not reformat D1 files.

### Existing Metadata Resolvers

Current code already includes runtime metadata resolvers:

- `src/Hexalith.EventStore.Client/Commands/CommandContractResolver.cs`
- `src/Hexalith.EventStore.Client/Queries/QueryContractResolver.cs`
- `src/Hexalith.EventStore.Contracts/Commands/CommandContractMetadata.cs`
- `src/Hexalith.EventStore.Contracts/Queries/QueryContractMetadata.cs`

Use these as semantic guardrails, not as analyzer dependencies. The generator cannot safely execute arbitrary static property getters at compile time. D2 should discover types and emit a manifest; D3 can generate code that references the static members in the consumer compilation.

### Recommended Generator Shape

Use an incremental pipeline that keeps the early syntax filter cheap:

- `SyntaxProvider.CreateSyntaxProvider` with a predicate for `ClassDeclarationSyntax` or `RecordDeclarationSyntax` with a base list.
- Transform by getting the declared `INamedTypeSymbol` and checking `AllInterfaces` against `compilation.GetTypeByMetadataName(...)`.
- Combine discovered message descriptors with assembly-level `RestApiAttribute` options.
- Emit only if the assembly has `RestApiAttribute`, so merely referencing the analyzer does not create noise in non-opted-in projects.

For attribute-based route overrides, it is fine to inspect `symbol.GetAttributes()` after the marker-interface match. D2 should only record route metadata; semantic validation belongs in D3/D4.

### Analyzer Packaging Pattern

Use the local PolymorphicSerializations generator as the packaging reference:

- `references/Hexalith.PolymorphicSerializations/src/libraries/Hexalith.PolymorphicSerializations.CodeGenerators/Hexalith.PolymorphicSerializations.CodeGenerators.csproj`

Adapt, do not copy blindly:

- Keep `netstandard2.0`, `IsRoslynComponent`, `EnforceExtendedAnalyzerRules`, analyzer `PackagePath="analyzers/dotnet/cs"`, and `PrivateAssets="all"`.
- Do **not** copy the submodule's copyright header.
- Do **not** set `GeneratePackageOnBuild=true`; EventStore's release remediation explicitly avoids implicit package generation.
- Keep analyzer dependencies private and avoid runtime dependency leakage.

### Smoke Consumer Guidance

D4 will add real generator tests. D2 still needs proof that the analyzer runs. Use an untracked temporary project for verification. A typical smoke shape:

```bash
tmpdir="$(mktemp -d)"
dotnet new classlib --framework net10.0 --output "$tmpdir/Smoke"
# Patch the temp csproj to reference:
# - src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj
# - src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj
#   with OutputItemType="Analyzer" ReferenceOutputAssembly="false"
# Add a temp .cs file with [assembly: RestApi("api/counter", "counter")] and one query + one command marker.
dotnet build "$tmpdir/Smoke/Smoke.csproj" \
  -p:EmitCompilerGeneratedFiles=true \
  -p:CompilerGeneratedFilesOutputPath="$tmpdir/generated" \
  --configuration Release
find "$tmpdir/generated" -name '*Manifest*.g.cs' -print
```

Do not commit the temporary project or generated files. Record the actual path/evidence in the Dev Agent Record.

### Testing And Verification Standards

- Use `Hexalith.EventStore.slnx` only for restore/build.
- Do not run solution-level `dotnet test`.
- D2 does not add the generator test project; D4 owns generator tests.
- Build commands for story completion:
  - `dotnet restore Hexalith.EventStore.slnx`
  - `dotnet build src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj --configuration Release`
  - `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false`
  - smoke consumer build as described above.

### Latest Technical Notes

- NuGet lists `Microsoft.CodeAnalysis.CSharp` `5.3.0` as the current version, compatible with `netstandard2.0` and `net10.0`. Use this pin to align with the repo's `.NET 10` SDK and sibling Hexalith Roslyn projects.
- NuGet lists `Microsoft.CodeAnalysis.Analyzers` `5.3.0` as the current analyzer API rules package. It is aimed at authors of analyzers/generators and should be private to the generator project.
- Microsoft Learn's Roslyn SDK docs describe source generators as compile-time metaprogramming that can read the compilation and add generated source. The `IIncrementalGenerator` API docs explicitly warn not to store state on generator instances because compiler-controlled lifetimes are not stable.
- The Roslyn source-generator cookbook states generators are additive: they add source to the compilation and do not modify existing user code. D2's manifest-only emission honors that boundary.

### Previous Story Intelligence

D1's story record includes these learnings:

- Contracts builds enforce XML docs because `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj` sets `GenerateDocumentationFile=true`.
- D1 deliberately used classic constructors for attributes so `ArgumentNullException.ThrowIfNull(...)` lives in the established attribute idiom.
- D1 explicitly deferred route-template validation and convention fallback implementation to D3.
- D1 did not touch package versions, `.slnx`, samples, or submodules; D2 is the first story that should modify `Directory.Packages.props` and `Hexalith.EventStore.slnx`.

Current implementation has also added command/query metadata resolver code after the D1 story file. Treat it as live code:

- Command and query type/domain/projection names must remain kebab-case, max 64 chars, no leading/trailing hyphen, and command/query discriminators must not contain colons.
- The generator should not duplicate runtime resolver behavior by executing code. It should discover and emit scaffolding only.

### Git Intelligence

Recent commits are mostly release/dependency-policy and workflow maintenance:

- `chore: update sprint change proposals and project context with approval statuses`
- `chore(deps): update Hexalith dependency references`
- `chore(workflows): consolidate ci and release automation`
- `chore(workflows): update actions and improve checkout steps in integration and release workflows`
- `chore(references): update subproject commits for Hexalith dependencies`

Actionable implications:

- Release builds should be validated with `-p:UseHexalithProjectReferences=false`.
- Do not broaden release packaging in D2; the current `.releaserc.json` intentionally packs a scoped list of EventStore projects.
- Do not introduce submodule project entries into the EventStore solution.

### Project Structure Notes

- The new project belongs under `src/`, not `tools/`, `tests/`, or a submodule.
- No UI/UX files are involved.
- No Aspire runtime changes are involved.
- No DAPR sidecar or container changes are involved.
- No package inventory/docs change is required until D8.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md` Section 4, Epic D table] - D2 scope, sequence, and D3-D8 boundaries.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md` Section 4.2] - D3 controller exemplar, explicitly deferred here.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-22-ci-release-retier.md` Section 4, CP-6/CP-7] - release pack is scoped and implicit package generation is suppressed.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-29.md` Sections 3-5] - Debug source / Release package dependency policy.
- [Source: `_bmad-output/implementation-artifacts/D-1-contract-seam.md`] - D1 contract seam decisions and validation notes.
- [Source: `src/Hexalith.EventStore.Contracts/Commands/ICommandContract.cs`] - command marker metadata.
- [Source: `src/Hexalith.EventStore.Contracts/Queries/IQueryContract.cs`] - query marker metadata.
- [Source: `src/Hexalith.EventStore.Contracts/Rest/RestRouteAttribute.cs`] - optional route override attribute.
- [Source: `src/Hexalith.EventStore.Contracts/Rest/RestApiAttribute.cs`] - assembly opt-in attribute.
- [Source: `src/Hexalith.EventStore.Client/Commands/CommandContractResolver.cs`] - runtime command metadata validation semantics.
- [Source: `src/Hexalith.EventStore.Client/Queries/QueryContractResolver.cs`] - runtime query metadata validation semantics.
- [Source: `references/Hexalith.PolymorphicSerializations/src/libraries/Hexalith.PolymorphicSerializations.CodeGenerators/Hexalith.PolymorphicSerializations.CodeGenerators.csproj`] - analyzer packaging precedent.
- [Source: `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.SourceTools/FrontComposerGenerator.cs`] - local incremental-generator precedent.
- [NuGet: Microsoft.CodeAnalysis.CSharp 5.3.0](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/) - current Roslyn C# package and target-framework compatibility.
- [NuGet: Microsoft.CodeAnalysis.Analyzers 5.3.0](https://www.nuget.org/packages/Microsoft.CodeAnalysis.Analyzers) - analyzer API rules package.
- [Microsoft Learn: .NET Compiler Platform SDK](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) - source generator concept and compiler API context.
- [Microsoft Learn: IIncrementalGenerator](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.iincrementalgenerator?view=roslyn-dotnet-5.0.0) - generator lifetime and initialization contract.
- [Roslyn incremental generator cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md) - additive source-generation model and incremental design guidance.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-07-01: Resolved `bmad-dev-story` workflow; loaded project context files and story D.2.
- 2026-07-01: Attempted Aspire baseline with `EnableKeycloak=false aspire run --detach --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json`; AppHost Debug build succeeded with 0 warnings and resources briefly reported `Running`, but the detached process exited before `aspire describe` could attach.
- 2026-07-01: Task 1 red check `rg 'Microsoft\.CodeAnalysis\.(CSharp|Analyzers)' Directory.Packages.props || true` returned no existing pins.
- 2026-07-01: Task 1 validation `rg 'Microsoft\.CodeAnalysis\.(CSharp|Analyzers)|Version="5\.3\.0"' Directory.Packages.props` confirmed both central package pins.
- 2026-07-01: Task 2 red checks confirmed no existing generator project or solution entry.
- 2026-07-01: Task 2 validation `dotnet build src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj --configuration Release` succeeded with 0 warnings and 0 errors.
- 2026-07-01: Initial Task 3/4 generator build failed on `RestApiManifestEmitter.cs` because `SymbolDisplay` needed `Microsoft.CodeAnalysis.CSharp`; fixed by adding the C# namespace import.
- 2026-07-01: Task 3/4 validation `dotnet build src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj --configuration Release` succeeded with 0 warnings and 0 errors.
- 2026-07-01: Scope guard `rg 'ApiController|Authorize|IEventStoreGatewayClient|SubmitCommandRequest|SubmitQueryRequest|FromBody|Http(Get|Post|Put|Patch|Delete)|ControllerBase|Microsoft\.AspNetCore|Hexalith\.EventStore\.Client' src/Hexalith.EventStore.RestApi.Generators || true` returned no matches.
- 2026-07-01: Task 5 plain restore `dotnet restore Hexalith.EventStore.slnx` failed on `references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj` because NU1903 is treated as an error for transitive package `Microsoft.OpenApi` 2.0.0 (`GHSA-v5pm-xwqc-g5wc`).
- 2026-07-01: Package-mode restore `dotnet restore Hexalith.EventStore.slnx -p:UseHexalithProjectReferences=false` succeeded.
- 2026-07-01: Release/package-mode restore `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false` succeeded.
- 2026-07-01: Task 5 generator build `dotnet build src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj --configuration Release` succeeded with 0 warnings and 0 errors.
- 2026-07-01: Task 5 solution build `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` succeeded with 0 warnings and 0 errors.
- 2026-07-01: Smoke project created at `/tmp/hexalith-eventstore-restapi-smoke.y8ReGT/Smoke` and built with `dotnet build /tmp/hexalith-eventstore-restapi-smoke.y8ReGT/Smoke/Smoke.csproj --configuration Release -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=/tmp/hexalith-eventstore-restapi-smoke.y8ReGT/generated`; build succeeded with 0 warnings and 0 errors.
- 2026-07-01: Smoke manifest emitted at `/tmp/hexalith-eventstore-restapi-smoke.y8ReGT/generated/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.RestApiGenerator/HexalithEventStoreRestApiGeneratorManifest.g.cs`; evidence showed `CommandCount = 1`, `QueryCount = 1`, `RouteOverrideCount = 2`, `Smoke.CreateCounterCommand`, and `Smoke.CounterStatusQuery`.
- 2026-07-01: `git status --short` showed no generated `bin/`, `obj/`, or temp files, but also showed unrelated pre-existing changes outside this story (`D-1-contract-seam.md`, planning artifact deletion, submodule pointers, RestApi/RestRoute attributes, ContractsPackageDependencyTests, and deferred/spec artifacts), so Task 5 remains incomplete.
- 2026-07-01: User requested fixing the transitive restore error; added `Microsoft.OpenApi` `2.9.0` to `references/Hexalith.Tenants/Directory.Packages.props` and reran `dotnet restore Hexalith.EventStore.slnx`, which succeeded.
- 2026-07-01: Rechecked `git status --short --untracked-files=all` before story completion; output was empty, confirming no generated `bin/`, `obj/`, or temp files were present in the worktree.
- 2026-07-01: Reran D2 validation: `dotnet restore Hexalith.EventStore.slnx`, generator Release build, package-mode solution Release build, and smoke build at `/tmp/hexalith-eventstore-restapi-smoke.0Tznfy/Smoke/Smoke.csproj`; all succeeded with 0 warnings and 0 errors.
- 2026-07-01: Smoke manifest emitted at `/tmp/hexalith-eventstore-restapi-smoke.0Tznfy/generated/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.RestApiGenerator/HexalithEventStoreRestApiGeneratorManifest.g.cs`; evidence showed `RestApiAttributeFound = true`, `CommandCount = 1`, `QueryCount = 1`, `RouteOverrideCount = 2`, `Smoke.CreateCounterCommand`, and `Smoke.CounterStatusQuery`.
- 2026-07-01: Regression tests passed for Contracts, Client, Sample, SignalR, Testing, QueryRouting, DomainService, Admin.Abstractions, Admin.Cli, Admin.Mcp, Admin.Server.Host, Admin.Server, Admin.UI, AppHost, Server, and sample package tests. DeferredWorkGovernance and OperationalEvidence.Validator ATDD suites are pre-existing red-phase scaffolds outside D2 scope; failures were due missing DW4/DW6 entrypoint/story artifacts with a clean worktree before story completion edits.

### Implementation Plan

- Follow the D2 task order exactly: central package pins, analyzer project scaffold, incremental discovery, deterministic manifest emission, and smoke verification.
- Keep the analyzer isolated from EventStore runtime assemblies by using Roslyn metadata names only.
- Emit only the manifest artifact and defer controller generation, diagnostics, route binding, and package-release registration to later stories.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Task 1 completed: Roslyn package versions were added centrally and no `.csproj` package versions were introduced.
- Task 2 completed: Analyzer project scaffold was added under `src/`, configured as a `netstandard2.0` Roslyn component, and included in `Hexalith.EventStore.slnx`.
- Tasks 3 and 4 completed: Incremental generator discovery uses metadata-name constants and Roslyn symbols only, parses assembly/route attributes into pure descriptors, and emits only the deterministic manifest source.
- Task 5 completed: restore, generator Release build, package-mode solution Release build, smoke manifest execution, scope guard, and final git-status hygiene all passed.
- D2 is ready for review; no controller emission, runtime gateway dependency, release-pipeline package registration, sample adoption, or submodule adoption was added in this story.

### File List

- `Directory.Packages.props`
- `Hexalith.EventStore.slnx`
- `src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiAttributeParser.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiGenerator.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiManifestEmitter.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiMessageDescriptor.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiMessageParser.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiMetadataNames.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiOptions.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RestApiRouteDescriptor.cs`
- `src/Hexalith.EventStore.RestApi.Generators/RoslynAttributeValueReader.cs`
- `references/Hexalith.Tenants/Directory.Packages.props`
- `_bmad-output/implementation-artifacts/D-2-generator-skeleton-spike.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

| Date | Change |
|---|---|
| 2026-07-01 | Story D2 created with generator scaffold, discovery, analyzer packaging, and smoke-verification guidance. Status ready-for-dev. |
| 2026-07-01 | Started D2 implementation and added central Roslyn package pins. |
| 2026-07-01 | Added generator project scaffold and solution entry. |
| 2026-07-01 | Implemented incremental discovery and manifest-only emission. |
| 2026-07-01 | Ran verification; left Task 5 incomplete due external restore blocker and unrelated pre-existing git status entries. |
| 2026-07-01 | Fixed Tenants transitive `Microsoft.OpenApi` restore blocker and reran restore successfully. |
| 2026-07-01 | Completed final D2 verification, confirmed clean worktree hygiene, and moved story to review. |
