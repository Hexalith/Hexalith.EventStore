# Sprint Change Proposal - 2026-06-26 - Submodule References Layout

## 1. Issue Summary

The repository's root-declared Git submodules were checked out at the repository root. The requested correction is to move those submodules under `references/` and update the solution, project path resolution, documentation, and LLM instructions that referred to the old root-level layout.

Trigger: direct user instruction, `$bmad-correct-course move submodules in /references folder. Update all resferences in solutions, projects, documents and LLM instructions`.

Evidence: `.gitmodules`, `Hexalith.EventStore.slnx`, root LLM instructions, brownfield docs, tracked C# Dev Kit `.lscache` files, and Aspire project metadata all contained paths such as `Hexalith.Tenants/...`, `Hexalith.Commons/...`, or `Hexalith.AI.Tools/...`.

## 2. Impact Analysis

Epic Impact: No epic scope change identified. PRD and epic artifacts were not present under `_bmad-output/planning-artifacts`, so this was handled as a minor direct implementation correction.

Story Impact: No new story required. Existing repository layout, solution references, docs, and path resolution code needed alignment.

Artifact Conflicts: The old root-level layout conflicted with the desired `references/` convention. The affected artifacts were `.gitmodules`, `Hexalith.EventStore.slnx`, root instructions, brownfield docs, generated API reference docs, tracked `.lscache` files, and path resolution helpers.

Technical Impact: Root submodule gitlinks now live under `references/`. EventStore's MSBuild path resolution now prefers `references/Hexalith.Tenants` and `references/Hexalith.Commons`. Tenants' local path resolver was updated so Tenants projects still build when consumed from EventStore's `references/` folder. Aspire metadata for cross-repo EventStore resources now resolves through `references/Hexalith.EventStore`.

## 3. Recommended Approach

Recommended path: Direct Adjustment.

Rationale: The change is mechanical and layout-scoped. It does not require rollback, MVP review, or backlog restructuring. The main risk is stale hard-coded paths; this was mitigated by repository-wide path scanning and a Release build.

Effort: Low to Medium.

Risk: Medium before validation because solution and MSBuild path resolution can fail at restore/build time. Residual risk is low after restore/build and focused tests passed.

## 4. Detailed Change Proposals

### Submodule Layout

OLD:

```text
Hexalith.AI.Tools/
Hexalith.Builds/
Hexalith.Commons/
Hexalith.FrontComposer/
Hexalith.Memories/
Hexalith.PolymorphicSerializations/
Hexalith.Tenants/
```

NEW:

```text
references/Hexalith.AI.Tools/
references/Hexalith.Builds/
references/Hexalith.Commons/
references/Hexalith.FrontComposer/
references/Hexalith.Memories/
references/Hexalith.PolymorphicSerializations/
references/Hexalith.Tenants/
```

Justification: Keeps root source, tests, docs, and deploy assets separate from external Hexalith module checkouts.

### Build and Solution Paths

Files: `.gitmodules`, `Hexalith.EventStore.slnx`, `Directory.Build.props`, `references/Hexalith.Tenants/Directory.Build.props`

OLD:

```xml
<Project Path="Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj" />
<HexalithTenantsBasePath>$(MSBuildThisFileDirectory)Hexalith.Tenants/src</HexalithTenantsBasePath>
```

NEW:

```xml
<Project Path="references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj" />
<HexalithTenantsBasePath>$(MSBuildThisFileDirectory)references/Hexalith.Tenants/src</HexalithTenantsBasePath>
```

Justification: Solution and project evaluation must resolve the moved submodule paths without relying on stale root-level folders.

### LLM Instructions and Documentation

Files: `AGENTS.md`, `CLAUDE.md`, `_bmad-output/project-context.md`, `docs/brownfield/*`, `docs/reference/api/Hexalith.EventStore.Aspire/*`

OLD:

```markdown
./Hexalith.AI.Tools/hexalith-llm-instructions.md
Hexalith.Tenants/src/Hexalith.Tenants
```

NEW:

```markdown
./references/Hexalith.AI.Tools/hexalith-llm-instructions.md
references/Hexalith.Tenants/src/Hexalith.Tenants
```

Justification: Future agents and readers must be directed to the current submodule layout.

### Aspire Project Metadata

File: `src/Hexalith.EventStore.Aspire/EventStorePlatformProjectMetadata.cs`

OLD:

```csharp
RepositoryProjectPaths.GetProjectPath(
    "Hexalith.EventStore",
    "src",
    "Hexalith.EventStore",
    "Hexalith.EventStore.csproj");
```

NEW:

```csharp
RepositoryProjectPaths.GetProjectPath(
    "references",
    "Hexalith.EventStore",
    "src",
    "Hexalith.EventStore",
    "Hexalith.EventStore.csproj");
```

Justification: Consuming AppHosts should resolve EventStore project metadata through the shared `references/` submodule convention.

## 5. Implementation Handoff

Scope: Minor.

Route to: Developer agent for direct implementation.

Success criteria:

- All root-declared submodule paths in `.gitmodules` use `references/...`.
- No root-level `Hexalith.*` submodule directories remain.
- `dotnet restore Hexalith.EventStore.slnx` succeeds.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` succeeds.
- Focused AppHost tests pass.

Verification completed:

- `git diff --check`
- `dotnet restore Hexalith.EventStore.slnx`
- `dotnet build Hexalith.EventStore.slnx --configuration Release`
- `dotnet test tests/Hexalith.EventStore.AppHost.Tests/ --configuration Release --no-build`

Checklist summary:

- Trigger/context: Done.
- Epic/PRD impact: PRD and epic artifacts unavailable; documented as no scope change.
- Architecture/project impact: Done; solution, MSBuild, Aspire metadata, and docs updated.
- Path forward: Direct Adjustment selected.
- Handoff: Developer implementation completed and validated.
