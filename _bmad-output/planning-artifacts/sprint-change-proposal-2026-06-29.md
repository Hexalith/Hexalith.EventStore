# Sprint Change Proposal - Debug Project References / Release NuGet References

- **Date:** 2026-06-29
- **Author:** Administrator (via BMAD Correct Course)
- **Project:** Hexalith.EventStore
- **Mode:** Batch
- **Change classification:** **Moderate** (build and release dependency-policy correction across package boundaries)
- **Status:** Approved for implementation
- **Trigger:** "projects must use project references for Hexalith librairies for debug and nuget package references for release"

---

## 1. Issue Summary

### Problem statement

Hexalith projects currently select several cross-repo Hexalith dependencies by **source presence** rather than
by build intent. When `references/Hexalith.*` submodules are checked out, Release builds can still compile
against sibling source through `ProjectReference`s. That is useful for debugging, but it is not the desired
release contract: published packages should depend on published Hexalith packages, not on whatever source
happens to be present in a developer or CI checkout.

**Target rule:**

> Debug builds use `ProjectReference` for Hexalith libraries when source is available. Release builds use
> `PackageReference` for published Hexalith libraries, with versions pinned in central package management.

This rule applies to cross-repo Hexalith libraries such as `Hexalith.Commons.*` and `Hexalith.Tenants.*`.
Same-repository `Hexalith.EventStore.*` project references remain project references during pack, because those
packages are built and versioned together and SDK pack translates packable project references into package
dependencies. Host applications that have no NuGet package remain source-only; they must not be disguised as
library package dependencies.

### How it was discovered

Direct Administrator correction on 2026-06-29, following the Tenants AppHost reference correction recorded in
`references/Hexalith.Tenants/_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-29-apphost-project-references.md`.
That proposal solved stale Debug assets and package fallback in Tenants, but its package/source selection is
still primarily source-presence keyed. The new instruction tightens the rule to configuration intent:
Debug = source, Release = packages.

### Evidence

- `Directory.Build.props` resolves `HexalithTenantsBasePath` and `HexalithCommonsRoot` from checked-out source
  paths without a Debug/Release dependency-mode switch.
- `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj` unconditionally references
  `Hexalith.Commons.UniqueIds` by project when the submodule is available.
- `src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj` and
  `src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj` use project references
  to `Hexalith.Commons.Aspire` / `Hexalith.Commons.ServiceDefaults` when source exists.
- `src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj` and
  `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj` reference
  `Hexalith.Tenants.Contracts` by project.
- `Directory.Packages.props` has no package-version pins for the external Hexalith packages needed by the
  package-reference path.
- `.github/workflows/release.yml` checks out submodules and runs restore before tests/release, so source is
  available in CI unless the build explicitly selects package references.

## 2. Impact Analysis

### Epic impact

No product epic scope changes. This is a build/release policy correction that supports the existing release
and domain-centric-platform epics. PRD and standalone epic artifacts are not present under
`_bmad-output/planning-artifacts`; prior correct-course proposals are the active planning baseline.

### Story impact

Add one build/release story:

| Story | Scope | Effort/Risk |
|---|---|---|
| **R1 - Configuration-keyed Hexalith dependencies** | Add a central Debug/Release dependency switch, convert external Hexalith library references to paired `ProjectReference`/`PackageReference`, update release restore/test/pack commands, and document the policy. | Medium / Medium |

No UX, command, event, aggregate, or API behavior changes.

### Artifact conflicts

| Artifact | Current conflict | Required action |
|---|---|---|
| `Directory.Build.props` | Source roots are resolved without a build-intent switch | Add `UseHexalithProjectReferences` (or equivalent) defaulting to `true` for Debug and `false` otherwise; source flags must require both the switch and `Exists(...)` |
| `Directory.Packages.props` | No central versions for external Hexalith packages | Add package-version properties and `PackageVersion` entries for published fallback packages |
| External Hexalith refs in `.csproj` files | Release can still select `ProjectReference` if source exists | Convert to paired conditional `ProjectReference` / `PackageReference` |
| `.github/workflows/release.yml` | Restore runs before Release configuration is established | Force package mode in release restore/test/release steps or set the property at job level |
| `.releaserc.json` | Pack command does not explicitly assert package mode | Add the package-mode property to pack commands for clarity and reproducibility |
| `CLAUDE.md`, docs | No explicit Debug-source / Release-package rule | Document the rule and the host-app exception |

### Technical impact

- Release package metadata becomes reproducible and independent of local submodule checkout state.
- Debug developer experience remains source-debuggable when submodules are checked out.
- Release builds will expose missing or unpublished package dependencies immediately. That is intended:
  an unavailable external Hexalith package is a release-blocking publication/order issue, not a reason to
  silently compile sibling source.
- AppHost-launched host projects are not libraries. They can keep Debug build-forcing `ProjectReference`s
  with `ReferenceOutputAssembly="false"` / `IsAspireProjectResource="false"` where needed, but release
  library packages should not gain dependencies on host apps.

## 3. Recommended Approach

**Selected path: Direct Adjustment with release-gate validation.**

Add one repo-wide dependency-mode property and apply it consistently to cross-repo Hexalith library
references. Default behavior should be:

```xml
<UseHexalithProjectReferences Condition="'$(UseHexalithProjectReferences)' == '' and '$(Configuration)' == 'Debug'">true</UseHexalithProjectReferences>
<UseHexalithProjectReferences Condition="'$(UseHexalithProjectReferences)' == ''">false</UseHexalithProjectReferences>
```

Then derive source flags only when both source exists and project-reference mode is enabled:

```xml
<HexalithCommonsFromSource Condition="'$(UseHexalithProjectReferences)' == 'true' and Exists('$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.UniqueIds\Hexalith.Commons.UniqueIds.csproj')">true</HexalithCommonsFromSource>
<HexalithTenantsFromSource Condition="'$(UseHexalithProjectReferences)' == 'true' and Exists('$(HexalithTenantsBasePath)\Hexalith.Tenants.Contracts\Hexalith.Tenants.Contracts.csproj')">true</HexalithTenantsFromSource>
```

Provide explicit override support for edge cases:

- `-p:UseHexalithProjectReferences=true` lets a developer debug source in a Release build intentionally.
- `-p:UseHexalithProjectReferences=false` lets a developer validate package mode in Debug.

**Effort:** Medium.

**Risk:** Medium until package fallback is proven, because the package path can expose missing package versions
or publish-order gaps. Risk drops to low after package-mode restore/build/test/pack passes.

## 4. Detailed Change Proposals

### CP-1 - Central dependency-mode property

File: `Directory.Build.props`

OLD:

```xml
<HexalithCommonsRoot Condition="'$(HexalithCommonsRoot)' == '' and Exists('$(MSBuildThisFileDirectory)references/Hexalith.Commons/src/libraries/Hexalith.Commons.UniqueIds/')">$(MSBuildThisFileDirectory)references/Hexalith.Commons</HexalithCommonsRoot>
```

NEW:

```xml
<UseHexalithProjectReferences Condition="'$(UseHexalithProjectReferences)' == '' and '$(Configuration)' == 'Debug'">true</UseHexalithProjectReferences>
<UseHexalithProjectReferences Condition="'$(UseHexalithProjectReferences)' == ''">false</UseHexalithProjectReferences>

<HexalithCommonsFromSource Condition="'$(UseHexalithProjectReferences)' == 'true' and Exists('$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.UniqueIds\Hexalith.Commons.UniqueIds.csproj')">true</HexalithCommonsFromSource>
<HexalithTenantsFromSource Condition="'$(UseHexalithProjectReferences)' == 'true' and Exists('$(HexalithTenantsBasePath)\Hexalith.Tenants.Contracts\Hexalith.Tenants.Contracts.csproj')">true</HexalithTenantsFromSource>
```

Rationale: source path discovery remains layout-aware, but selecting source becomes a Debug/default developer
choice rather than an accidental Release behavior.

### CP-2 - Central package-version pins

File: `Directory.Packages.props`

Add external Hexalith package versions and fallback package entries. Initial version values must be verified
against the feed before implementation; do not pin an unreleased commit.

```xml
<HexalithCommonsUniqueIdsVersion>2.18.0</HexalithCommonsUniqueIdsVersion>
<HexalithTenantsVersion>3.15.1</HexalithTenantsVersion>

<PackageVersion Include="Hexalith.Commons.UniqueIds" Version="$(HexalithCommonsUniqueIdsVersion)" />
<PackageVersion Include="Hexalith.Tenants.Contracts" Version="$(HexalithTenantsVersion)" />
```

Rationale: package mode must be centrally pinned and reproducible. `Hexalith.Commons.Aspire` and
`Hexalith.Commons.ServiceDefaults` are not published package IDs on the configured feed, so the small hosting
helper surface is kept inside EventStore instead of creating broken Release package fallbacks. A Debug-only
source reference to `Hexalith.Commons.ServiceDefaults` is still allowed to support source-debugging external
host projects that consume its public types.

### CP-3 - Convert external Hexalith library references

Files:

- `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj`
- `src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj`
- `src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj`
- `src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj`
- `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj`

OLD:

```xml
<ProjectReference Include="$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.UniqueIds\Hexalith.Commons.UniqueIds.csproj" />
```

NEW:

```xml
<ProjectReference Include="$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.UniqueIds\Hexalith.Commons.UniqueIds.csproj"
                  Condition="'$(HexalithCommonsFromSource)' == 'true'" />
<PackageReference Include="Hexalith.Commons.UniqueIds"
                  Condition="'$(HexalithCommonsFromSource)' != 'true'" />
```

Equivalent paired references should be used for `Hexalith.Tenants.Contracts`. The former
`Hexalith.Commons.Aspire` source reference is removed because those helpers are now implemented inside
EventStore and have no published package fallback. `Hexalith.Commons.ServiceDefaults` remains a Debug-only
source edge with no Release package fallback, so it cannot leak into published EventStore package metadata.

Rationale: every external Hexalith library dependency has exactly one source of truth per build mode.

### CP-4 - AppHost handling

File: `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`

Do **not** replace launched host applications with package references. Host projects such as
`Hexalith.Tenants` are not library packages. Include external host resources only when source mode is enabled,
and do not list external submodule projects as standalone `Hexalith.EventStore.slnx` build targets. If the
AppHost needs to guarantee fresh child assets during `aspire run`, add Debug-only build edges:

```xml
<ItemGroup Condition="'$(Configuration)' == 'Debug'">
  <ProjectReference Include="$(HexalithTenantsBasePath)\Hexalith.Tenants\Hexalith.Tenants.csproj"
                    Condition="Exists('$(HexalithTenantsBasePath)\Hexalith.Tenants\Hexalith.Tenants.csproj')"
                    ReferenceOutputAssembly="false"
                    Private="false"
                    IsAspireProjectResource="false" />
</ItemGroup>
```

Rationale: this mirrors the Tenants AppHost correction. Debug launch gets fresh binaries; Release package
graphs are not polluted with host-app project references or direct submodule solution projects.

### CP-5 - Release workflow and semantic-release

Files:

- `.github/workflows/release.yml`
- `.github/workflows/integration.yml`
- `.releaserc.json`

OLD:

```yaml
- name: Restore
  run: dotnet restore
```

NEW:

```yaml
env:
  UseHexalithProjectReferences: 'false'

- name: Restore
  run: dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false
```

Also pass `-p:UseHexalithProjectReferences=false` in the semantic-release pack loop. Keep integration/debug
jobs free to use source mode only when that is the deliberate test target.

Always rerun restore after changing `Configuration` or `UseHexalithProjectReferences`; `--no-restore` can reuse
stale project-reference assets from the previous mode.

Rationale: CI checks out submodules, so package mode must be explicit in the release lane.

### CP-6 - Documentation

Files:

- `CLAUDE.md`
- `_bmad-output/project-context.md`
- `docs/brownfield/development-guide.md`
- `docs/reference/nuget-packages.md`

Add the build policy:

```markdown
Cross-repo Hexalith library dependencies are Debug-source / Release-package:
Debug builds use ProjectReference when the root-declared submodule source is present; Release builds use
PackageReference pinned in Directory.Packages.props. Use -p:UseHexalithProjectReferences=true only for an
intentional Release source-debug session, never for package publication.
```

Rationale: future agents should not reintroduce source-presence keyed Release behavior.

## 5. Implementation Handoff

- **Scope:** Moderate.
- **Route to:** Developer agent for implementation, with release-owner review of package-version pins and feed
  availability.
- **Implementation sequence:**
  1. Add the central dependency-mode property and source flags.
  2. Add package-version pins.
  3. Convert external Hexalith library references to paired conditional references.
  4. Update release workflow and semantic-release pack command to assert package mode.
  5. Update docs and project context.
  6. Validate Debug source mode and Release package mode separately.

### Success criteria

1. `dotnet restore Hexalith.EventStore.slnx -p:UseHexalithProjectReferences=true` succeeds with submodules present.
2. `dotnet build Hexalith.EventStore.slnx --configuration Debug -p:UseHexalithProjectReferences=true` uses external Hexalith `ProjectReference`s.
3. `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false` restores external Hexalith packages.
4. `dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false` succeeds.
5. The release gate tests pass with package mode selected.
6. `npx semantic-release --dry-run` or the equivalent pack loop produces only `Hexalith.EventStore.*.nupkg` packages and no `Hexalith.Commons.*` or `Hexalith.Tenants.*` packages.
7. Generated NuGet package metadata shows package dependencies for external Hexalith libraries rather than source project paths.

## 6. Correct-Course Checklist Summary

- [x] 1.1 Trigger identified: direct Administrator build/release policy correction.
- [x] 1.2 Core problem defined: Release source dependency selection is keyed by source presence instead of build intent.
- [x] 1.3 Evidence gathered from `Directory.Build.props`, project files, release workflow, and Tenants 2026-06-29 proposal.
- [x] 2.1 Current epic assessed: no product epic change.
- [x] 2.2 Epic-level change: one build/release story proposed.
- [x] 2.3 Remaining epics reviewed: Epic D and release packaging are supported, not re-scoped.
- [x] 2.4 No new product epic required.
- [x] 2.5 No epic priority/order change required.
- [x] 3.1 PRD conflict: PRD unavailable; no MVP/product scope impact identified.
- [x] 3.2 Architecture/build conflict documented.
- [N/A] 3.3 UI/UX conflict: no UI behavior impact.
- [x] 3.4 CI/CD and documentation impacts documented.
- [x] 4.1 Direct Adjustment viable: selected.
- [x] 4.2 Rollback not viable: no completed product work needs reverting.
- [x] 4.3 MVP review not applicable.
- [x] 4.4 Recommended path selected.
- [x] 5.1 Issue summary created.
- [x] 5.2 Epic/artifact impacts documented.
- [x] 5.3 Recommended path and alternatives documented.
- [x] 5.4 MVP impact and action plan defined.
- [x] 5.5 Handoff plan established.
- [x] 6.3 Explicit approval received from Administrator.
- [N/A] 6.4 `sprint-status.yaml` update: no sprint-status file or epic/story status artifact found.

## 7. Approval

- [x] Approved for implementation - 2026-06-29
- [ ] Approved with changes (noted below)
- [ ] Rejected / revise

Notes: Approved by Administrator. Implementation may proceed as a Moderate build/release correction.
