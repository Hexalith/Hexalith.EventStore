---
title: 'Provision the Tenants services with plain Aspire run'
type: 'bugfix'
created: '2026-08-26'
status: 'done'
review_loop_iteration: 0
baseline_commit: '70a0358f943684853f0979b7bd8147cc1b0a135c'
context:
  - '{project-root}/docs/brownfield/development-guide.md'
  - '{project-root}/docs/reference/nuget-packages.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A plain `aspire run` evaluates the AppHost in package dependency mode, which currently compiles the `tenants` and `tenants-api` resources out of the topology. The Admin UI still invokes DAPR app-id `tenants`, so `/tenants` and `/events` wait for the HTTP resilience timeout before rendering.

**Approach:** Make local run-mode topology discovery independent from the repository-wide NuGet-versus-project-reference switch. When Aspire runs locally, register the checked-out Tenants host projects by path with the same DAPR, dependency, environment, and security wiring as explicit source mode; keep package, Release, and publish behavior unchanged.

## Boundaries & Constraints

**Always:** Plain local `aspire run` must contain healthy `tenants` and `tenants-api` resources when the root Tenants checkout is available. Reuse the repository-layout resolver and existing wiring. If either host is absent in run mode, fail before starting resources and name the root submodule initialization command. Preserve explicit source mode and all DAPR/security relationships.

**Ask First:** Any proposal to change the published topology, CI release graph, or the public API of `Hexalith.EventStore.Aspire`; any need to edit the Tenants submodule.

**Never:** Change global dependency defaults; initialize submodules automatically; add a container fallback; silently omit Tenants in local runs; publish path-discovered Tenants resources.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Default local run | Run mode, package dependency defaults, both Tenants host projects present | Register `tenants` and `tenants-api` by resolved project paths with existing wiring | N/A |
| Explicit source run | `HexalithTenantsFromSource=true` | Register both generated `Projects.*` resources exactly once | N/A |
| Missing checkout | Run mode and either Tenants host project absent | Do not start a partial topology | Throw an actionable error naming `git submodule update --init references/Hexalith.Tenants` |
| Publish/package build | Publish mode or Release/package evaluation | Preserve the current package graph and exclude path-discovered resources | N/A |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.AppHost/Program.cs` -- the compile symbol gates both Tenants resources and security edges; add the run-only fallback and share downstream wiring here.
- `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` -- preserve the conditional references and compile symbol for explicit source mode.
- `src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs` -- reuse `GetProjectPath` to keep both hosts pinned to the root-declared Tenants submodule.
- `Directory.Build.props` -- read-only invariant: package mode remains the default and `HexalithTenantsFromSource` remains an explicit source-dependency signal.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs` -- cover both hosts, wiring parity, fallback, and the missing-project diagnostic.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs` -- require Tenants security relationships in the default run model.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/TenantBootstrapHealthTests.cs` -- replace the missing-resource skip with the default topology contract.
- `docs/getting-started/quickstart.md`, `docs/brownfield/development-guide.md`, `docs/reference/nuget-packages.md` -- document prerequisites and separate runtime discovery from dependency mode.
- Baseline revision: `70a0358f943684853f0979b7bd8147cc1b0a135c`.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.AppHost/Program.cs` -- add run-only Tenants project discovery, fail-fast validation, and shared registration/wiring while preserving the compile-time source branch.
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs` and `AspireSecurityResourceNamingTests.cs` -- cover default registration, missing checkout, uniqueness, and wiring parity.
- [x] `tests/Hexalith.EventStore.IntegrationTests/ContractTests/TenantBootstrapHealthTests.cs` -- require the Tenants resource in the default contract topology.
- [x] `docs/getting-started/quickstart.md`, `docs/brownfield/development-guide.md`, and `docs/reference/nuget-packages.md` -- document submodule prerequisites and the run-versus-package distinction.

**Acceptance Criteria:**
- Given an initialized root Tenants submodule, when plain `aspire run` starts, then both Tenants resources become healthy and appear exactly once.
- Given the default local topology is healthy, when `/tenants` or `/events` is requested, then rendering no longer incurs the missing-`tenants` DAPR timeout.
- Given either Tenants host project is missing, when a local run begins, then startup stops with an actionable root-submodule message before other resources run.
- Given Release/package evaluation or default Aspire publish, when the graph is inspected, then it remains package-safe with no path-discovered Tenants resource.

## Spec Change Log

## Design Notes

Aspire.Hosting 13.4.6 provides `ExecutionContext.IsRunMode` and `AddProject(name, projectPath)`. Use them only for the fallback; keep generated `Projects.Hexalith_Tenants*` authoritative in explicit source mode. Resolve both paths before adding either resource.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj --configuration Debug -m:1` -- expected: topology guardrails pass.
- `dotnet msbuild src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -nologo -getProperty:UseHexalithProjectReferences,UseNuGetDeps,HexalithTenantsFromSource -getItem:ProjectReference` -- expected: package properties and static references are unchanged.
- `dotnet build src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false` -- expected: no cross-repo source edges.
- `aspire start --non-interactive && aspire wait tenants --non-interactive && aspire wait tenants-api --non-interactive && aspire describe --include-hidden --format Json` -- expected: both occur once and are healthy; stop Aspire before builds.
- `dotnet test tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj --configuration Debug -m:1 --filter FullyQualifiedName~TenantBootstrapHealthTests` -- expected: bootstrap proof runs without skipping when prerequisites exist.
- Timed requests to `https://localhost:8093/tenants` and `/events` -- expected: neither waits for the prior approximately 30-second missing-app timeout.

## Suggested Review Order

**Runtime topology**

- Separate local resource discovery from package dependency selection.
  [`Program.cs:74`](../../src/Hexalith.EventStore.AppHost/Program.cs#L74)

- Pin both runtime hosts to the root-declared Tenants checkout and fail fast.
  [`TenantsProjectPaths.cs:20`](../../src/Hexalith.EventStore.AppHost/TenantsProjectPaths.cs#L20)

- Apply identical DAPR and dependency wiring to both registration paths.
  [`Program.cs:90`](../../src/Hexalith.EventStore.AppHost/Program.cs#L90)

- Preserve Tenants authentication relationships when security is enabled.
  [`Program.cs:167`](../../src/Hexalith.EventStore.AppHost/Program.cs#L167)

**Regression coverage**

- Prove default run-mode registration, uniqueness, paths, and DAPR relationships.
  [`TenantsApiLaunchSettingsTests.cs:119`](../../tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs#L119)

- Exclude path-discovered resources from publish while preserving explicit source mode.
  [`TenantsApiLaunchSettingsTests.cs:178`](../../tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs#L178)

- Lock root checkout resolution and actionable missing-project diagnostics.
  [`TenantsApiLaunchSettingsTests.cs:218`](../../tests/Hexalith.EventStore.AppHost.Tests/Configuration/TenantsApiLaunchSettingsTests.cs#L218)

- Require both Tenants hosts to become healthy in Tier-3 bootstrap proof.
  [`TenantBootstrapHealthTests.cs:43`](../../tests/Hexalith.EventStore.IntegrationTests/ContractTests/TenantBootstrapHealthTests.cs#L43)

**Operator guidance**

- Explain local run discovery without weakening Release/package invariants.
  [`development-guide.md:76`](../../docs/brownfield/development-guide.md#L76)

- Make fresh-clone root submodule prerequisites executable.
  [`quickstart.md:19`](../../docs/getting-started/quickstart.md#L19)

- Clarify runtime discovery versus NuGet/project-reference selection.
  [`nuget-packages.md:94`](../../docs/reference/nuget-packages.md#L94)
