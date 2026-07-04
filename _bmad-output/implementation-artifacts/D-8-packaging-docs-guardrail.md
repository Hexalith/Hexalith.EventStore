---
created: 2026-07-02
source_story_key: D-8-packaging-docs-guardrail
supersedes_scope_note: sprint-change-proposal-2026-07-02-rest-api-external-host
baseline_commit: 84712c4957155b983f98072afc641a9eeab2f6e3
---

# Story D.8: Packaging, Docs, and Guardrails for RestApi.Generators

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **Hexalith.EventStore platform maintainer**,
I want **the REST API generator released as an analyzer package and the domain-authoring docs/guardrails corrected to the external API host architecture**,
so that **domain teams can depend on the generated typed REST surface without stale UI-host guidance, missing NuGet packaging, or regressions back to hand-written controllers**.

## Story Context

This is **story D8 of Epic D - REST Controller Source Generator**. D1-D4 built the public contract seam, generator, controller emission, and generator tests. D5 corrected the Sample proof to a contracts library plus dedicated `Sample.Api` external host. D6 is the Counter command proof and is currently in progress in this workspace. D7 is the Tenants external API host/UI-client split and is ready for development, but not complete at story creation time.

The original June 21 D8 wording said "publish generator as analyzer NuGet (8 -> 9)" and "generated controllers into the domain UI host." Both are now stale:

- The July 2 correct-course decision supersedes UI-host generation. Generated controllers live in a **dedicated external-facing API host**. Interactive UI hosts consume the EventStore Client libraries and host no generated or hand-written MVC command/query controllers.
- Release packaging is now manifest-driven through `tools/release-packages.json`, not a package loop embedded in `.releaserc.json`.
- The current release manifest already contains **12** packages. D8 adds `Hexalith.EventStore.RestApi.Generators` as the next manifest entry; do not revert docs or tooling to a historical 8/9 package count.

Source of truth: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02-rest-api-external-host.md` CP-1 and CP-7; original D8 row in `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md`; D1-D7 story files; current release tooling in `.releaserc.json` and `tools/release-packages.json`; current docs and guardrail tests.

## Acceptance Criteria

1. **Preflight verifies final Epic D shape before closeout claims.**
   - Read D5, D6, and D7 current story records before editing docs or guardrails.
   - If D6 or D7 is still not implemented, do not write docs that claim the proof is complete. Packaging and forward-looking governance may proceed, but the Dev Agent Record must state the unresolved dependency.
   - Confirm the accepted architecture is the July 2 model: generated controllers in external API hosts; interactive UI hosts use EventStore Client libraries and host no generated or hand-written MVC command/query controllers.
   - Confirm current package inventory from `tools/release-packages.json`; at story creation it has 12 entries and omits `Hexalith.EventStore.RestApi.Generators`.
   - Do not use the obsolete "8 -> 9" math except as historical context.

2. **`Hexalith.EventStore.RestApi.Generators` is published through the manifest-driven release flow.**
   - Add this entry to `tools/release-packages.json`:
     - `id`: `Hexalith.EventStore.RestApi.Generators`
     - `project`: `src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj`
   - Preserve the manifest shape consumed by `tools/pack-release-packages.py` and `tools/validate-release-packages.py`.
   - Preserve release-script safety flags: `-p:GeneratePackageOnBuild=false` and `-p:UseHexalithProjectReferences=false`.
   - Do not reintroduce a hard-coded package loop into `.releaserc.json`; it should keep delegating to the Python scripts unless a failing validation proves a narrow script fix is needed.
   - Verify the generated `.nupkg` contains the analyzer under `analyzers/dotnet/cs/Hexalith.EventStore.RestApi.Generators.dll` and does not expose a runtime `lib/` asset.
   - Keep package versions centralized; do not add `Version=` attributes to project files.

3. **Release/package governance tests protect the new package and current manifest model.**
   - Add or extend tests in an existing blocking test project; prefer `tests/Hexalith.EventStore.Contracts.Tests/Packaging/` for release-manifest structure and `tests/Hexalith.EventStore.DomainService.Tests/` for domain-authoring guardrails.
   - Tests must assert:
     - `tools/release-packages.json` includes `Hexalith.EventStore.RestApi.Generators`.
     - every manifest project exists.
     - package IDs and project paths are unique.
     - the generator project packs analyzer assets under `analyzers/dotnet/cs`.
     - obsolete release-package counts in key docs are absent after the docs update.
   - Prefer structural checks over brittle string snapshots. If a count is needed, derive it from the manifest so future package additions update one source of truth.

4. **Domain-authoring guardrails are corrected and extended.**
   - Update `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs`.
   - Fix the Tenants path bug: do not probe a non-existent root-level `Hexalith.Tenants`; use `references/Hexalith.Tenants` when the submodule is initialized.
   - Do not scan the whole Tenants repository as if every sibling project were a domain module. The domain-service root is `references/Hexalith.Tenants/src/Hexalith.Tenants`; sibling projects such as `Hexalith.Tenants.AppHost`, `.Aspire`, `.UI`, and the future `.Api` have different ownership.
   - Keep the existing domain-centric checks: no domain-service-owned `*.Aspire` or `*.ServiceDefaults`; no projection/query actor reimplementation.
   - Add an interactive UI host guard. Known UI roots:
     - `samples/Hexalith.EventStore.Sample.BlazorUI`
     - `src/Hexalith.EventStore.Admin.UI`
     - `references/Hexalith.Tenants/src/Hexalith.Tenants.UI` when initialized
   - The UI guard must fail if an interactive UI host contains generated-controller opt-in or MVC controller hosting for command/query REST, including `[assembly: RestApi(...)]`, `AddControllers`, `MapControllers`, `ControllerBase`, `[ApiController]`, or a generator analyzer reference to `Hexalith.EventStore.RestApi.Generators`.
   - Dedicated external API hosts are allowed and should not be flagged, for example `samples/Hexalith.EventStore.Sample.Api` and the future Tenants API host.
   - If D7 has not yet deleted the old Tenants service controller, do not hide the issue by broad allowlisting. Record the dependency and keep the guard scoped so it becomes meaningful once D7 lands.

5. **Repository instructions and package docs describe the current package set and analyzer package.**
   - Update both `CLAUDE.md` and `AGENTS.md` because this repository carries both and they currently duplicate stale package/release text.
   - Replace `## NuGet Packages (8 published)` with manifest-derived current wording. At story creation, D8 makes the release manifest **13 packages**.
   - Add `Hexalith.EventStore.RestApi.Generators` to the package list and describe it as a Roslyn source-generator/analyzer package distributed under `analyzers/dotnet/cs`.
   - Update the release paragraph that still says "publish 6 NuGet packages"; it should reference the manifest-driven package set instead.
   - Update `docs/reference/nuget-packages.md`:
     - title/intro package count,
     - overview table,
     - dependency graph or explanatory text,
     - "Which package do I need?" guidance for an external API host,
     - package details for `Hexalith.EventStore.RestApi.Generators`,
     - versioning section.
   - Update stale package-count docs discovered at story creation:
     - `docs/brownfield/project-overview.md`
     - `docs/brownfield/index.md`
     - `docs/guides/upgrade-path.md`
     - `docs/ci-secrets-checklist.md`
   - Do not update generated API reference files under `docs/reference/api/**` unless an explicit API-doc generation task is run.

6. **Architecture docs reflect external API hosts, not UI-host controllers.**
   - Update `docs/brownfield/architecture.md`.
   - Add `Hexalith.EventStore.RestApi.Generators` to "The Parts" as a NuGet analyzer/source-generator package.
   - Add or update the domain-service authoring section:
     - domain services stay headless/domain-centric,
     - typed public per-domain REST is generated from contracts,
     - generated controllers belong in dedicated external API hosts,
     - interactive UI hosts call EventStore Client libraries,
     - hand-written per-message controllers and BFF command/query wrappers are anti-patterns.
   - Update "Key Design Decisions / Rules" with the same external API host principle.
   - Update `docs/brownfield/integration-architecture.md` so the topology shows external API hosts as gateway-backed facades and does not imply direct HTTP between services.
   - Preserve DAPR/service-invocation architecture: generated controllers call `IEventStoreGatewayClient`; they do not call MediatR, domain services, DAPR actors, state stores, or projection actors directly.

7. **No stale UI-host generator guidance remains in active docs.**
   - Search active repository instructions and docs, excluding historical BMAD artifacts and generated API reference, for stale phrases such as:
     - `generated controllers into the domain UI host`
     - `generate controllers into Hexalith.Tenants.UI`
     - `UI host owns generated controllers`
     - `Sample.BlazorUI hosts generated API controllers`
   - Replace stale guidance with the July 2 external API host/client-library split.
   - Historical story files under `_bmad-output/implementation-artifacts/` may retain old text if clearly marked as historical/correct-course context; do not rewrite past story records just to make search output clean.

8. **Scope stays D8 closeout only.**
   - Do not implement D6 command proof or D7 Tenants API/UI split inside D8.
   - Do not modify Sample command contracts, Tenants command/query contracts, UI components, AppHost resources, DAPR access-control policies, or generated-controller code unless a docs/guardrail test cannot be written without a narrowly scoped correction.
   - Do not modify submodule files without explicit user approval. Reading `references/Hexalith.Tenants` is required; editing it is not part of D8.
   - Do not initialize nested submodules or run recursive submodule commands.
   - Do not add package versions to `.csproj` files.
   - Keep generated `.nupkg`, `bin`, `obj`, `TestResults`, and temporary pack output out of the repository.

9. **Verification proves packaging, docs, and guardrails.**
   - Run focused tests:
     ```bash
     dotnet test tests/Hexalith.EventStore.Contracts.Tests/
     dotnet test tests/Hexalith.EventStore.DomainService.Tests/
     ```
   - Run generator tests to ensure analyzer package code still passes:
     ```bash
     dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/
     ```
   - Build the solution in Release package mode:
     ```bash
     dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false
     ```
   - Validate the release manifest and package output:
     ```bash
     rm -rf /tmp/hexalith-eventstore-d8-nupkgs
     python3 tools/pack-release-packages.py /tmp/hexalith-eventstore-d8-nupkgs 0.0.0-d8
     python3 tools/validate-release-packages.py /tmp/hexalith-eventstore-d8-nupkgs 0.0.0-d8
     unzip -l /tmp/hexalith-eventstore-d8-nupkgs/Hexalith.EventStore.RestApi.Generators.0.0.0-d8.nupkg
     ```
   - The unzip evidence must show `analyzers/dotnet/cs/Hexalith.EventStore.RestApi.Generators.dll`.
   - Do not run solution-level `dotnet test`.
   - If the full package loop is blocked by an unrelated package, run and record the failing command, then at minimum run a direct pack for the generator project and the manifest validation dry-run.

## Tasks / Subtasks

- [x] **Task 1: Preflight current Epic D and release state** (AC: 1, 8)
  - [x] Read D5, D6, and D7 story records.
  - [x] Confirm whether D6 and D7 are complete, in progress, or not started.
  - [x] Count `tools/release-packages.json` entries and confirm generator omission.
  - [x] Inspect `src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj` package shape.

- [x] **Task 2: Register the analyzer package for release** (AC: 2, 3)
  - [x] Add `Hexalith.EventStore.RestApi.Generators` to `tools/release-packages.json`.
  - [x] Add manifest/package governance tests.
  - [x] Preserve `.releaserc.json` script delegation.
  - [x] Verify analyzer package contents with local package output.

- [x] **Task 3: Update repository instructions and package docs** (AC: 5, 7)
  - [x] Update `CLAUDE.md` and `AGENTS.md`.
  - [x] Update `docs/reference/nuget-packages.md`.
  - [x] Update stale count docs: brownfield overview/index, upgrade path, and CI secrets checklist.
  - [x] Search active docs/instructions for obsolete package counts and UI-host generator wording.

- [x] **Task 4: Update architecture/integration docs** (AC: 6, 7)
  - [x] Add the generator package to `docs/brownfield/architecture.md`.
  - [x] Document external API hosts and UI client-library consumption in architecture rules.
  - [x] Update `docs/brownfield/integration-architecture.md` topology/integration tables.
  - [x] Preserve gateway-backed generated-controller semantics.

- [x] **Task 5: Extend domain-authoring guardrails** (AC: 4)
  - [x] Fix domain module root discovery so Tenants uses `references/Hexalith.Tenants/src/Hexalith.Tenants` when initialized.
  - [x] Add interactive UI host controller/generator opt-in guard.
  - [x] Keep external API hosts out of the UI guard.
  - [x] Add clear failure messages pointing to the external API host rule.

- [x] **Task 6: Verify and record evidence** (AC: 9)
  - [x] Run focused tests and Release build.
  - [x] Run package manifest pack/validate commands.
  - [x] Record `.nupkg` analyzer-path evidence.
  - [x] Confirm `git status --short` contains only intended D8 changes plus pre-existing unrelated workspace changes.

### Review Findings

- [ ] [Review][Patch] Remove submodule pointer updates from D8 or record explicit approval [references/Hexalith.Builds:1]
- [ ] [Review][Patch] Remove root build/package behavior changes from D8 scope [Directory.Build.props:39]
- [ ] [Review][Patch] Correct package/distribution docs that still understate packaged components [docs/brownfield/architecture.md:69]
- [ ] [Review][Patch] Fix UI package guidance so command/query UI clients include the EventStore Client package [docs/reference/nuget-packages.md:229]
- [ ] [Review][Patch] Extend the UI-host guard to catch MVC controller-hosting variants beyond the current exact markers [tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs:37]

## Dev Notes

### Top Guardrails

1. **Do not implement the superseded UI-host design.** Any active doc text saying generated controllers belong in an interactive UI host must be corrected.
2. **Do not regress release tooling to hard-coded package loops.** The release package manifest is the source of truth.
3. **Use the current manifest count, not historical counts.** At story creation: 12 current packages, D8 adds the generator as 13.
4. **Guardrails must scan the right roots.** Tenants lives under `references/Hexalith.Tenants`, and the domain-service root is not the whole Tenants repository.
5. **Generated controllers stay gateway-backed.** Docs and tests must preserve the rule that generated controllers delegate to `IEventStoreGatewayClient`.
6. **D8 is closeout, not proof implementation.** Do not fold unfinished D6/D7 implementation into this story.

### Current Code State Read During Story Creation

| File | Current state | D8 change | Preserve |
|---|---|---|---|
| `tools/release-packages.json` | Manifest with 12 packages; generator is missing. | Add `Hexalith.EventStore.RestApi.Generators`. | Reviewable manifest shape. |
| `tools/pack-release-packages.py` | Loads manifest, validates uniqueness/existence, runs `dotnet pack` with Release, version, output, `GeneratePackageOnBuild=false`, `UseHexalithProjectReferences=false`. | Usually no change. Add tests around its assumptions rather than replacing it. | Source/package mode safety flags. |
| `tools/validate-release-packages.py` | Compares `.nupkg` files against manifest IDs and requested version. | Usually no change. | Manifest-driven validation. |
| `.releaserc.json` | Uses `@semantic-release/exec` prepare command to run pack + validate scripts; publish glob is `./nupkgs/Hexalith.EventStore.*.nupkg`. | Usually no change. | Semantic-release script delegation. |
| `src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj` | `netstandard2.0`, `IsRoslynComponent`, analyzer DLL packed under `analyzers/dotnet/cs`, `IncludeBuildOutput=false`, `SuppressDependenciesWhenPacking=true`. | Verify package content and document it. | Analyzer-only package shape, private Roslyn dependencies. |
| `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` | Checks Sample root and a wrong root-level `Hexalith.Tenants` path; does not guard interactive UI hosts. | Fix path/root semantics and add UI-host controller guard. | Existing domain-centric SDK guardrails. |
| `CLAUDE.md` / `AGENTS.md` | Stale package count and release text; no corrected external API host REST rule. | Update both consistently. | Submodule, build/test, and domain-service rules. |
| `docs/reference/nuget-packages.md` | Guide to 8 packages; no generator package. | Update package guide for current manifest and generator analyzer. | Existing package purpose/dependency guidance. |
| `docs/brownfield/architecture.md` | Parts table omits generator; domain-centric section does not include corrected generated REST principle. | Add generator and external API host rule. | DAPR/gateway/auth architecture. |
| `docs/brownfield/integration-architecture.md` | Sample UI is shown calling EventStore; no external API host lane. | Add external API host lane without implying direct service-to-service HTTP. | DAPR service invocation boundary. |

### Expected Package Inventory After D8

`tools/release-packages.json` should contain the existing 12 entries plus:

```json
{
  "id": "Hexalith.EventStore.RestApi.Generators",
  "project": "src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj"
}
```

The exact order may follow the source-tree order near Contracts/Client or place generator after DomainService; keep the ordering intentional and easy to review.

### Documentation Language To Use

Use this principle consistently:

```text
REST controllers are generated from ICommandContract/IQueryContract messages into dedicated external-facing API hosts. Interactive UI hosts consume EventStore Client libraries and must not host generated or hand-written per-message MVC command/query controllers.
```

Avoid the superseded phrasing:

```text
generated controllers into the domain UI host
generated controllers into Hexalith.Tenants.UI
Sample.BlazorUI hosts generated API controllers
```

### Previous Story Intelligence

From D1:

- `ICommandContract` and `IQueryContract` metadata values are the author-facing seam.
- `[RestRoute]` is class-level and `[RestApi]` is assembly-level.
- Route-shape validation belongs to generator stories, not contracts.

From D2:

- Analyzer packaging follows NuGet analyzer conventions using `analyzers/dotnet/cs`.
- The generator must stay analyzer-only and should not leak runtime dependencies.
- Release package registration was explicitly deferred to D8.

From D3:

- Generated controllers are ASP.NET Core controllers that inject `IEventStoreGatewayClient`.
- They must not call MediatR, DAPR actors, state stores, projection actors, or domain query dispatchers directly.
- Generated source is deterministic, analyzer-safe, and covered by stable diagnostics.

From D4:

- Persistent generator tests exist and should be part of D8 validation.
- Generated source must compile; string assertions alone are not enough for generator behavior.

From D5:

- UI-host generation caused contract-identity duplication and an unused endpoint.
- Correct-course resolved this by moving contracts into `Sample.Contracts`, generated controllers into `Sample.Api`, and keeping `Sample.BlazorUI` on Client libraries.
- Release docs/guardrails were left for D8.

From D6:

- Counter command work must not update release package inventory, package count docs, or D8 guardrails.
- D8 should verify D6 is complete before claiming the Counter command proof is part of closed Epic D evidence.

From D7:

- Tenants follows the same external API host/UI-client split.
- D8 owns the docs/governance correction and must not implement Tenants submodule changes.

### Git Intelligence

Recent root commits at story creation:

- `5db0bfd9 feat: Update D5 proof status to review, enhance launch settings, and add tests for Sample.Api`
- `6db716e4 feat: Introduce external REST API host and contracts library`
- `90709756 refactor: remove unused RestApiAssemblyInfo and GetCounterStatusQuery files`
- `dc55d61c chore(release): 3.27.0 [skip ci]`
- `15ecad6f chore(references): update Hexalith.FrontComposer submodule commit`

Current worktree at story creation includes unrelated D6/sample changes:

- `_bmad-output/implementation-artifacts/D-6-proof-counter-commands.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- Sample command files being moved/deleted into `samples/Hexalith.EventStore.Sample.Contracts/Counter/Commands/`
- `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterCommandForm.razor`
- `samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj`
- `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs`

Do not revert or fold those D6 changes into D8.

### Latest Technical Notes

- Microsoft NuGet analyzer packaging conventions place analyzer DLLs under an `analyzers` package folder, with language-specific folders such as `analyzers/dotnet/cs` for C# analyzers. Source: https://learn.microsoft.com/en-us/nuget/guides/analyzers-conventions
- Microsoft Roslyn SDK documentation describes source generators as compile-time metaprogramming that adds source to the compilation. D8 packaging must preserve source-generator/analyzer distribution rather than turning the generator into a runtime library. Source: https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/
- `@semantic-release/exec` supports `prepareCmd` and `publishCmd`; non-zero exit codes stop the release. This matches the current `.releaserc.json` design where pack and validation scripts run during prepare. Source: https://github.com/semantic-release/exec
- The current EventStore release flow uses the manifest-driven Python scripts documented in `docs/ci.md`; D8 should update the manifest and tests, not bypass that release boundary.

### Testing and Verification Standards

- Use xUnit v3 and Shouldly for new C# tests.
- Run test projects individually; do not run solution-level `dotnet test`.
- Use `Hexalith.EventStore.slnx` only for restore/build.
- Run Release/package mode with `-p:UseHexalithProjectReferences=false` for release-impacting changes.
- Keep package output under `/tmp/hexalith-eventstore-d8-nupkgs` or another untracked path.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02-rest-api-external-host.md`] - corrected external API host architecture and D8 governance scope.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-21.md`] - original Epic D and D8 package/docs/guardrail intent.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02.md`] - manifest-driven release package tooling context.
- [Source: `_bmad-output/implementation-artifacts/D-1-contract-seam.md`] - public contract seam.
- [Source: `_bmad-output/implementation-artifacts/D-2-generator-skeleton-spike.md`] - analyzer package shape and D8 release deferral.
- [Source: `_bmad-output/implementation-artifacts/D-3-controller-emission.md`] - generated controller gateway behavior.
- [Source: `_bmad-output/implementation-artifacts/D-4-generator-tests.md`] - generator test coverage.
- [Source: `_bmad-output/implementation-artifacts/D-5-proof-sample-blazorui-queries.md`] - Sample external API host correction.
- [Source: `_bmad-output/implementation-artifacts/D-6-proof-counter-commands.md`] - D6 scope boundary.
- [Source: `_bmad-output/implementation-artifacts/D-7-proof-tenants-ui-host-submodule.md`] - D7 external API host/UI-client split.
- [Source: `tools/release-packages.json`] - active release package manifest.
- [Source: `tools/pack-release-packages.py`] - manifest-driven pack script.
- [Source: `tools/validate-release-packages.py`] - package-output validator.
- [Source: `.releaserc.json`] - semantic-release exec wiring.
- [Source: `src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj`] - analyzer package project.
- [Source: `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs`] - domain-authoring guardrail to extend.
- [Source: `CLAUDE.md`] - root repository instructions to update.
- [Source: `AGENTS.md`] - root agent instructions to keep in sync.
- [Source: `docs/reference/nuget-packages.md`] - user-facing package guide to update.
- [Source: `docs/brownfield/architecture.md`] - architecture map to update.
- [Source: `docs/brownfield/integration-architecture.md`] - integration topology to update.
- [Microsoft Learn: Analyzer NuGet formats](https://learn.microsoft.com/en-us/nuget/guides/analyzers-conventions) - analyzer package path conventions.
- [Microsoft Learn: Roslyn SDK](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) - source generator compile-time model.
- [semantic-release exec plugin](https://github.com/semantic-release/exec) - `prepareCmd`/`publishCmd` behavior.

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Activation: loaded `bmad-dev-story`, resolved workflow customization, loaded `_bmad/bmm/config.yaml`, and loaded all `project-context.md` persistent fact files.
- Baseline: captured commit `84712c4957155b983f98072afc641a9eeab2f6e3`, moved `D-8-packaging-docs-guardrail` to `in-progress` in sprint status, and started from a clean root worktree.
- Preflight: read complete D5, D6, and D7 story records. D5, D6, and D7 are all implemented and currently in `review`; they are not yet `done`.
- Preflight: confirmed the accepted July 2 architecture is generated controllers in dedicated external API hosts while interactive UI hosts consume EventStore Client libraries.
- Preflight: `tools/release-packages.json` contained 12 packages and did not include `Hexalith.EventStore.RestApi.Generators`.
- Preflight: inspected `src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj`; it is analyzer-only (`IncludeBuildOutput=false`, `SuppressDependenciesWhenPacking=true`) and packs the generator DLL under `analyzers/dotnet/cs`.
- Red: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` failed because `tools/release-packages.json` did not include `Hexalith.EventStore.RestApi.Generators`.
- Green: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` passed: 4/4 after adding the generator manifest entry and packaging guard tests.
- Package evidence: direct `dotnet pack src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj --configuration Release --output /tmp/hexalith-eventstore-d8-generator-pack -p:Version=0.0.0-d8 -p:GeneratePackageOnBuild=false -p:UseHexalithProjectReferences=false` succeeded.
- Package evidence: `unzip` is not installed in the VM (`/bin/bash: unzip: command not found`), so `python3 -m zipfile -l /tmp/hexalith-eventstore-d8-generator-pack/Hexalith.EventStore.RestApi.Generators.0.0.0-d8.nupkg` was used for local listing evidence. It showed `analyzers/dotnet/cs/Hexalith.EventStore.RestApi.Generators.dll` and no `lib/` asset.
- Red: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` failed after adding docs guard tests because active docs still contained stale 6/8-package release counts.
- Docs scan: `rg` over `AGENTS.md`, `CLAUDE.md`, and `docs` excluding `docs/reference/api/**` found no prohibited stale package-count wording or superseded UI-host generator phrases after the docs update.
- Green: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` passed: 6/6 after repository instructions and package docs were updated.
- Architecture update: added `RestApi.Generators`, external API host topology, UI client-library consumption, and generated-controller gateway semantics to `docs/brownfield/architecture.md` and `docs/brownfield/integration-architecture.md`.
- Active-doc scan: `rg` found no stale package-count wording, obsolete unpublished DomainService wording, or superseded UI-host generator phrases after architecture and component inventory cleanup.
- Green: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --filter FullyQualifiedName~ReleasePackageManifestTests` passed: 6/6 after architecture docs update.
- Red: `dotnet test tests/Hexalith.EventStore.DomainService.Tests/ --filter FullyQualifiedName~DomainModuleAuthoringGuardrailTests` failed on the new Tenants-root assertion, proving the old guard skipped the initialized `references/Hexalith.Tenants/src/Hexalith.Tenants` domain root. The same run also exposed a stale Sample-only-DomainService reference assumption after the D5/D6 contracts-library split.
- Green: `dotnet test tests/Hexalith.EventStore.DomainService.Tests/ --filter FullyQualifiedName~DomainModuleAuthoringGuardrailTests` passed: 5/5 after scoping Tenants to the domain-service root, allowing the Sample domain-owned contracts library, and adding the interactive UI host controller/generator guard.
- Green: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` passed: 556/556.
- Green: `dotnet test tests/Hexalith.EventStore.DomainService.Tests/` passed: 39/39.
- Green: `dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/` passed: 45/45.
- Green: `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` passed with 0 warnings/errors.
- Green: `python3 tools/pack-release-packages.py /tmp/hexalith-eventstore-d8-nupkgs 0.0.0-d8` packed 13 manifest packages.
- Green: `python3 tools/validate-release-packages.py /tmp/hexalith-eventstore-d8-nupkgs 0.0.0-d8` validated 13 release packages.
- Package evidence: `python3 -m zipfile -l /tmp/hexalith-eventstore-d8-nupkgs/Hexalith.EventStore.RestApi.Generators.0.0.0-d8.nupkg` showed `analyzers/dotnet/cs/Hexalith.EventStore.RestApi.Generators.dll` and no `lib/` asset. `unzip` remains unavailable in this VM, so zipfile listing was used as equivalent archive evidence.
- Baseline unit regression: `dotnet test tests/Hexalith.EventStore.Client.Tests/` passed 483/483; `dotnet test tests/Hexalith.EventStore.Sample.Tests/` passed 91/91; `dotnet test tests/Hexalith.EventStore.SignalR.Tests/` passed 35/35; `dotnet test tests/Hexalith.EventStore.Testing.Tests/` passed 144/144. The parallel run emitted a package-assets-cache I/O warning on one project, but all builds/tests completed successfully.
- Green: `git diff --check` passed with no whitespace errors.
- Final status check before completion: `git status --short` showed D8 story/sprint tracking, docs, release manifest, packaging tests, DomainService guardrail tests, and two package-governance files (`Directory.Packages.props`, `ContractsPackageDependencyTests.cs`) present in the workspace diff and included in this file list.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Completed D8 preflight against D5/D6/D7, current release tooling, and generator package shape. D5/D6/D7 are in review; D8 may document them as implemented proofs but not as done/released closeout.
- Registered `Hexalith.EventStore.RestApi.Generators` in the manifest-driven release package inventory, added blocking manifest/package governance tests, preserved semantic-release delegation to `tools/pack-release-packages.py` and `tools/validate-release-packages.py`, and verified the generator package is analyzer-only in local package output.
- Updated root instructions and package docs to the manifest-driven 13-package release set, documented `RestApi.Generators` as an analyzer package, added external API host package guidance, and replaced stale package-count wording in active docs.
- Updated architecture and integration docs so generated controllers are described as dedicated external API host facades backed by `IEventStoreGatewayClient`; interactive UI hosts consume EventStore Client libraries and do not own per-message MVC controllers.
- Extended domain-authoring guardrails so Tenants scans the initialized submodule domain-service root only, interactive UI hosts fail on generated-controller opt-in or MVC command/query controller hosting, and external API hosts are not broad-allowlisted into the UI guard.
- Completed D8 verification: focused D8 tests, baseline unit projects, Release package-mode solution build, full manifest pack/validate, generator analyzer package-content evidence, and whitespace/status checks all passed or were recorded with exact local-tooling context.

### File List

- `_bmad-output/implementation-artifacts/D-8-packaging-docs-guardrail.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `tools/release-packages.json`
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` (new)
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs`
- `AGENTS.md`
- `CLAUDE.md`
- `Directory.Packages.props`
- `docs/reference/nuget-packages.md`
- `docs/brownfield/project-overview.md`
- `docs/brownfield/index.md`
- `docs/guides/upgrade-path.md`
- `docs/ci-secrets-checklist.md`
- `docs/brownfield/architecture.md`
- `docs/brownfield/integration-architecture.md`
- `docs/brownfield/component-inventory.md`
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs`

## Change Log

| Date | Change |
|---|---|
| 2026-07-02 | Story D8 created with manifest-driven analyzer package release scope, corrected external API host documentation requirements, current package-count guardrails, DomainModuleAuthoringGuardrail path/UI-host checks, stale-doc cleanup, and verification plan. Status ready-for-dev. |
| 2026-07-03 | Completed preflight and registered `Hexalith.EventStore.RestApi.Generators` in the manifest-driven release package inventory with focused packaging guard tests and analyzer package output evidence. |
| 2026-07-03 | Updated repository instructions and package docs to the manifest-driven package set, added generator analyzer package guidance, and added stale package-count/UI-host wording guard tests. |
| 2026-07-03 | Updated brownfield architecture docs for external API hosts, UI client-library consumption, and gateway-backed generated-controller semantics. |
| 2026-07-03 | Extended domain-authoring guardrails for the Tenants submodule root and interactive UI host controller/generator prohibition. |
| 2026-07-03 | Completed D8 verification with required tests, baseline unit tests, Release package-mode build, manifest pack/validate, and generator analyzer package-content evidence. |
