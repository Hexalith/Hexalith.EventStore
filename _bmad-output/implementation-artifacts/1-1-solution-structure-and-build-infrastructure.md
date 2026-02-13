# Story 1.1: Solution Structure & Build Infrastructure

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a complete solution scaffold with all projects, build infrastructure (Directory.Build.props, Directory.Packages.props, global.json, .editorconfig), and feature folder conventions,
so that I can begin development with consistent tooling and dependency management from day one.

## Prerequisites

Before starting this story, the dev agent MUST verify:

- [x] .NET 10 SDK installed (10.0.102 used; global.json pinned to 10.0.102 with latestPatch)
- [x] Docker Desktop is installed and running (Docker 29.2.0)
- [x] DAPR CLI 1.16.x is installed (CLI 1.16.5, Runtime 1.16.8)
- [x] .NET Aspire templates available (.NET 10 includes Aspire templates natively, workload not needed)

## Acceptance Criteria

1. **Solution Project Structure** - Given a fresh clone of the repository, When I open the solution in an IDE, Then the solution contains these projects organized in solution folders (src/, samples/, tests/): Hexalith.EventStore.Contracts, Hexalith.EventStore.Client, Hexalith.EventStore.Server, Hexalith.EventStore.Aspire, Hexalith.EventStore.Testing (class libraries); plus Hexalith.EventStore.CommandApi (host), Hexalith.EventStore.AppHost (Aspire orchestrator), Hexalith.EventStore.ServiceDefaults (shared defaults), and Hexalith.EventStore.Sample (reference domain service); plus test projects Hexalith.EventStore.Contracts.Tests, Hexalith.EventStore.Server.Tests, Hexalith.EventStore.IntegrationTests.

2. **Directory.Build.props Configuration** - Given Directory.Build.props exists at the solution root, When I build the solution, Then the file sets: TargetFramework=net10.0, Nullable=enable, ImplicitUsings=enable, TreatWarningsAsErrors=true, NuGet packages are marked IsPackable=true with metadata, host/test projects override IsPackable=false, and MinVer is configured for automatic versioning.

3. **Directory.Packages.props Central Package Management** - Given Directory.Packages.props exists, When I examine the file, Then all NuGet dependencies are centrally managed with zero package versions specified in individual .csproj files, and every package referenced across all .csproj files has a pinned version entry.

4. **global.json SDK Pinning** - Given global.json exists, When I run `dotnet --version`, Then the SDK version is pinned to .NET 10 (10.0.102) with rollForward=latestPatch. _(Originally specified 10.0.103; user environment has 10.0.102 installed — see Debug Log.)_

5. **EditorConfig Enforcement** - Given .editorconfig exists, When I open any code file in an IDE, Then project coding conventions are enforced (naming, formatting, etc.) with specific analyzer severities set to warning (not error) for initial scaffolding phase.

6. **Clean Build** - Given a fresh clone with .NET 10 SDK installed, When I run `dotnet build`, Then the solution builds with zero errors and zero warnings.

7. **Aspire Startup** - Given all projects are scaffolded, When I run the AppHost project, Then the Aspire dashboard launches and all services are registered (even if implementations are stubs). NOTE: This AC may not be verifiable by a headless CI agent; manual verification is acceptable.

8. **Deploy Directory** - Given the repository is cloned, When I inspect the deploy/ directory, Then it contains DAPR component YAML templates for production configurations with `TODO:` placeholder values.

9. **Tests Pass** - Given a clean build, When I run `dotnet test`, Then all placeholder tests pass with zero failures.

10. **NuGet Pack** - Given a clean build, When I run `dotnet pack`, Then it produces .nupkg files for the 5 publishable packages (Contracts, Client, Server, Aspire, Testing) and no .nupkg files for host/sample/test projects.

## Tasks / Subtasks

### Task 1: Create build infrastructure files (AC: #2, #3, #4, #5, #6)

> Build infrastructure MUST be created BEFORE projects so that `dotnet new` picks up Directory.Build.props and Directory.Packages.props automatically.

- [x] 1.1 Create `global.json` at repository root
- [x] 1.2 Create `Directory.Build.props` at repository root
- [x] 1.3 Create `Directory.Packages.props` at repository root
- [x] 1.4 Create `nuget.config` at repository root
- [x] 1.5 Create `.editorconfig` at repository root
- [x] 1.6 Create `tests/Directory.Build.props` for test-specific overrides

**Verification:** Files exist, valid XML/JSON, `dotnet --version` confirms SDK

#### 1.1 global.json

```json
{
  "sdk": {
    "version": "10.0.103",
    "rollForward": "latestPatch"
  }
}
```

#### 1.2 Directory.Build.props

```xml
<Project>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>

    <!-- NuGet Package Defaults (overridden by host/test projects) -->
    <IsPackable>true</IsPackable>
    <IsPublishable>false</IsPublishable>

    <!-- NuGet Metadata -->
    <Authors>Hexalith Contributors</Authors>
    <Company>Hexalith</Company>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/Hexalith/Hexalith.EventStore</PackageProjectUrl>
    <RepositoryUrl>https://github.com/Hexalith/Hexalith.EventStore</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <Description>DAPR-native event sourcing server for .NET</Description>
    <PackageTags>eventsourcing;dapr;cqrs;eventstore;dotnet</PackageTags>
  </PropertyGroup>

  <!-- MinVer: Git tag-based SemVer versioning -->
  <PropertyGroup>
    <MinVerTagPrefix>v</MinVerTagPrefix>
    <MinVerDefaultPreReleaseIdentifiers>preview.0</MinVerDefaultPreReleaseIdentifiers>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MinVer" Version="7.0.0" PrivateAssets="All" />
  </ItemGroup>

</Project>
```

**IMPORTANT**: MinVer is the ONE exception to the "no versions in .csproj" rule -- it MUST be versioned in Directory.Build.props because it is a build-time-only tool, not a runtime dependency managed via CPM.

#### 1.3 Directory.Packages.props

```xml
<Project>

  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup Label="Dapr">
    <PackageVersion Include="Dapr.Client" Version="1.16.0" />
    <PackageVersion Include="Dapr.AspNetCore" Version="1.16.1" />
    <PackageVersion Include="Dapr.Actors" Version="1.16.0" />
    <PackageVersion Include="Dapr.Actors.AspNetCore" Version="1.16.1" />
  </ItemGroup>

  <ItemGroup Label="Aspire">
    <PackageVersion Include="Aspire.Hosting" Version="9.2.1" />
    <PackageVersion Include="Aspire.Hosting.AppHost" Version="9.2.1" />
    <PackageVersion Include="Aspire.Hosting.Redis" Version="9.2.1" />
    <PackageVersion Include="CommunityToolkit.Aspire.Hosting.Dapr" Version="9.7.0" />
  </ItemGroup>

  <ItemGroup Label="Aspire ServiceDefaults">
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="9.5.0" />
    <PackageVersion Include="Microsoft.Extensions.ServiceDiscovery" Version="9.2.1" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.11.2" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.11.2" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.11.1" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.11.1" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="1.11.1" />
  </ItemGroup>

  <ItemGroup Label="Application">
    <PackageVersion Include="MediatR" Version="14.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
    <PackageVersion Include="FluentValidation" Version="11.11.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.11.0" />
  </ItemGroup>

  <ItemGroup Label="Testing">
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageVersion Include="Shouldly" Version="4.3.0" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
    <PackageVersion Include="Testcontainers" Version="4.4.0" />
  </ItemGroup>

</Project>
```

> **NOTE**: Aspire versions shown above use 9.2.x as representative stable releases. The dev agent SHOULD verify the latest Aspire 9.x stable versions via NuGet before creating this file. The architecture document references "Aspire 13.1.0" which may refer to a workload or meta-version -- confirm exact NuGet package versions at implementation time. The important constraint is: use the LATEST STABLE Aspire packages compatible with .NET 10.

#### 1.4 nuget.config

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

#### 1.5 .editorconfig

```ini
# Top-most EditorConfig file
root = true

# All files
[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

# C# files
[*.cs]
# Naming conventions
dotnet_naming_rule.interface_should_begin_with_i.severity = warning
dotnet_naming_rule.interface_should_begin_with_i.symbols = interface
dotnet_naming_rule.interface_should_begin_with_i.style = begins_with_i

dotnet_naming_symbols.interface.applicable_kinds = interface
dotnet_naming_symbols.interface.applicable_accessibilities = *

dotnet_naming_style.begins_with_i.required_prefix = I
dotnet_naming_style.begins_with_i.capitalization = pascal_case

dotnet_naming_rule.private_field_should_begin_with_underscore.severity = warning
dotnet_naming_rule.private_field_should_begin_with_underscore.symbols = private_field
dotnet_naming_rule.private_field_should_begin_with_underscore.style = underscore_camel_case

dotnet_naming_symbols.private_field.applicable_kinds = field
dotnet_naming_symbols.private_field.applicable_accessibilities = private

dotnet_naming_style.underscore_camel_case.required_prefix = _
dotnet_naming_style.underscore_camel_case.capitalization = camel_case

dotnet_naming_rule.async_methods_should_end_with_async.severity = warning
dotnet_naming_rule.async_methods_should_end_with_async.symbols = async_methods
dotnet_naming_rule.async_methods_should_end_with_async.style = ends_with_async

dotnet_naming_symbols.async_methods.applicable_kinds = method
dotnet_naming_symbols.async_methods.required_modifiers = async

dotnet_naming_style.ends_with_async.required_suffix = Async
dotnet_naming_style.ends_with_async.capitalization = pascal_case

# Code style
csharp_style_namespace_declarations = file_scoped:warning
csharp_using_directive_placement = outside_namespace:warning
dotnet_sort_system_directives_first = true

# Formatting
csharp_new_line_before_open_brace = all:warning
csharp_indent_case_contents = true
csharp_indent_switch_labels = true

# Analyzer severity: warning (not error) for scaffolding phase
# These can be escalated to error once codebase matures
dotnet_diagnostic.CA1062.severity = warning
dotnet_diagnostic.CA1822.severity = warning
dotnet_diagnostic.CA2007.severity = warning

# Suppress specific analyzers that conflict with scaffolding
dotnet_diagnostic.CA1014.severity = none
```

#### 1.6 tests/Directory.Build.props

```xml
<Project>

  <!-- Import root Directory.Build.props -->
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

</Project>
```

---

### Task 2: Create solution file and all projects (AC: #1, #6)

> Input: Build infrastructure files from Task 1 already in place.
> Commands: Execute `dotnet new` commands in exact order below.
> Post-generation: Clean up each .csproj to remove any TargetFramework, Nullable, ImplicitUsings that duplicate Directory.Build.props.

- [x] 2.1 Create solution file
- [x] 2.2 Create all 12 projects using `dotnet new`
- [x] 2.3 Add all projects to solution with solution folders
- [x] 2.4 Clean up generated .csproj files (remove duplicated properties)

**Verification:** `dotnet sln list` shows all 12 projects; no .csproj contains `<TargetFramework>` or `<Nullable>`

#### 2.1 Solution and Project Creation Commands

Run these commands from the repository root in this exact order:

```bash
# Solution file
dotnet new sln --name Hexalith.EventStore

# --- Class Libraries (NuGet packages) ---
dotnet new classlib --name Hexalith.EventStore.Contracts --output src/Hexalith.EventStore.Contracts
dotnet new classlib --name Hexalith.EventStore.Client --output src/Hexalith.EventStore.Client
dotnet new classlib --name Hexalith.EventStore.Server --output src/Hexalith.EventStore.Server
dotnet new classlib --name Hexalith.EventStore.Aspire --output src/Hexalith.EventStore.Aspire
dotnet new classlib --name Hexalith.EventStore.Testing --output src/Hexalith.EventStore.Testing

# --- Host Projects ---
dotnet new web --name Hexalith.EventStore.CommandApi --output src/Hexalith.EventStore.CommandApi
dotnet new aspire-apphost --name Hexalith.EventStore.AppHost --output src/Hexalith.EventStore.AppHost
dotnet new aspire-servicedefaults --name Hexalith.EventStore.ServiceDefaults --output src/Hexalith.EventStore.ServiceDefaults

# --- Sample ---
dotnet new web --name Hexalith.EventStore.Sample --output samples/Hexalith.EventStore.Sample

# --- Test Projects ---
dotnet new xunit --name Hexalith.EventStore.Contracts.Tests --output tests/Hexalith.EventStore.Contracts.Tests
dotnet new xunit --name Hexalith.EventStore.Server.Tests --output tests/Hexalith.EventStore.Server.Tests
dotnet new xunit --name Hexalith.EventStore.IntegrationTests --output tests/Hexalith.EventStore.IntegrationTests
```

#### 2.2 Add to Solution with Folders

```bash
# src folder
dotnet sln add src/Hexalith.EventStore.Contracts --solution-folder src
dotnet sln add src/Hexalith.EventStore.Client --solution-folder src
dotnet sln add src/Hexalith.EventStore.Server --solution-folder src
dotnet sln add src/Hexalith.EventStore.Aspire --solution-folder src
dotnet sln add src/Hexalith.EventStore.Testing --solution-folder src
dotnet sln add src/Hexalith.EventStore.CommandApi --solution-folder src
dotnet sln add src/Hexalith.EventStore.AppHost --solution-folder src
dotnet sln add src/Hexalith.EventStore.ServiceDefaults --solution-folder src

# samples folder
dotnet sln add samples/Hexalith.EventStore.Sample --solution-folder samples

# tests folder
dotnet sln add tests/Hexalith.EventStore.Contracts.Tests --solution-folder tests
dotnet sln add tests/Hexalith.EventStore.Server.Tests --solution-folder tests
dotnet sln add tests/Hexalith.EventStore.IntegrationTests --solution-folder tests
```

#### 2.3 Post-Generation .csproj Cleanup

After running `dotnet new`, each generated .csproj will contain properties that duplicate Directory.Build.props. For EVERY generated .csproj file:

1. **REMOVE** `<TargetFramework>net10.0</TargetFramework>` (inherited from Directory.Build.props)
2. **REMOVE** `<Nullable>enable</Nullable>` (inherited)
3. **REMOVE** `<ImplicitUsings>enable</ImplicitUsings>` (inherited)
4. **KEEP** only project-specific properties (OutputType, project references, package references without versions)

For host projects (CommandApi, AppHost, Sample), ADD:

```xml
<PropertyGroup>
  <IsPackable>false</IsPackable>
  <IsPublishable>true</IsPublishable>
</PropertyGroup>
```

For ServiceDefaults, ADD:

```xml
<PropertyGroup>
  <IsPackable>false</IsPackable>
</PropertyGroup>
```

Test projects inherit `IsPackable=false` from `tests/Directory.Build.props`.

---

### Task 3: Configure project references and NuGet packages (AC: #1, #3, #10)

> Input: All 12 projects created and added to solution.
> Action: Add ProjectReference and PackageReference entries to each .csproj per the dependency table below.
> CRITICAL: PackageReference entries MUST NOT contain Version attributes -- versions come from Directory.Packages.props only.

- [x] 3.1 Set up Contracts project (zero internal dependencies)
- [x] 3.2 Set up Client project (depends on Contracts)
- [x] 3.3 Set up Server project (depends on Contracts)
- [x] 3.4 Set up Testing project (depends on Contracts, Server)
- [x] 3.5 Set up Aspire integration project
- [x] 3.6 Set up CommandApi project (depends on Server, Contracts, ServiceDefaults)
- [x] 3.7 Set up ServiceDefaults project
- [x] 3.8 Set up AppHost project (references all service projects + Aspire)
- [x] 3.9 Set up Sample project (depends on Client, ServiceDefaults)
- [x] 3.10 Set up test projects with appropriate references

**Verification:** `dotnet build` succeeds; `dotnet pack` produces exactly 5 .nupkg files

#### Project Dependency and Package Assignment Table

| Project | ProjectReferences | PackageReferences | IsPackable | DAPR app-id |
|---------|------------------|-------------------|------------|-------------|
| **Contracts** | _(none)_ | _(none)_ | true | - |
| **Client** | Contracts | Dapr.Client | true | - |
| **Server** | Contracts | Dapr.Client, Dapr.Actors, Dapr.Actors.AspNetCore, MediatR | true | - |
| **Aspire** | _(none)_ | Aspire.Hosting, CommunityToolkit.Aspire.Hosting.Dapr | true | - |
| **Testing** | Contracts, Server | Shouldly, NSubstitute | true | - |
| **CommandApi** | Server, Contracts, ServiceDefaults | Dapr.AspNetCore, MediatR, FluentValidation.DependencyInjectionExtensions, Microsoft.AspNetCore.Authentication.JwtBearer | false | `commandapi` |
| **ServiceDefaults** | _(none)_ | Microsoft.Extensions.Http.Resilience, Microsoft.Extensions.ServiceDiscovery, OpenTelemetry.Exporter.OpenTelemetryProtocol, OpenTelemetry.Extensions.Hosting, OpenTelemetry.Instrumentation.AspNetCore, OpenTelemetry.Instrumentation.Http, OpenTelemetry.Instrumentation.Runtime | false | - |
| **AppHost** | CommandApi, Sample, Aspire | ~~Aspire.Hosting.AppHost~~ _(replaced by Aspire.AppHost.Sdk)_, Aspire.Hosting.Redis, CommunityToolkit.Aspire.Hosting.Dapr | false | - |
| **Sample** | Client, ServiceDefaults | Dapr.AspNetCore | false | `sample` |
| **Contracts.Tests** | Contracts, Testing | coverlet.collector, Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio | false | - |
| **Server.Tests** | Server, Testing | coverlet.collector, Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio, Testcontainers | false | - |
| **IntegrationTests** | CommandApi, Sample, Testing | coverlet.collector, Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio | false | - |

---

### Task 4: Create stub implementations for build verification (AC: #6, #9)

> Input: All projects configured with references.
> Action: Create minimal compilable code in every project.
> RULE: Every library project gets a single `BuildVerification.cs` class. No empty projects.

- [x] 4.1 Add `BuildVerification.cs` to each class library project
- [x] 4.2 Configure CommandApi Program.cs with minimal host
- [x] 4.3 Configure Sample Program.cs with minimal host
- [x] 4.4 Add a passing placeholder test in each test project

**Verification:** `dotnet build` zero errors/warnings; `dotnet test` all pass

#### 4.1 BuildVerification.cs (for each class library)

Create `BuildVerification.cs` in the root of each class library (Contracts, Client, Server, Aspire, Testing):

```csharp
// File: src/Hexalith.EventStore.{ProjectShortName}/BuildVerification.cs
namespace Hexalith.EventStore.{ProjectShortName};

/// <summary>
/// Placeholder to verify the project compiles. Remove when first real type is added.
/// </summary>
internal static class BuildVerification
{
    internal static bool IsConfigured => true;
}
```

Replace `{ProjectShortName}` with: `Contracts`, `Client`, `Server`, `Aspire`, `Testing`.

#### 4.2 CommandApi Program.cs

```csharp
// File: src/Hexalith.EventStore.CommandApi/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => "Hexalith EventStore CommandApi");

app.Run();
```

#### 4.3 Sample Program.cs

```csharp
// File: samples/Hexalith.EventStore.Sample/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => "Hexalith EventStore Sample Domain Service");

app.Run();
```

#### 4.4 Placeholder Tests

For each test project, create a single test file:

```csharp
// File: tests/Hexalith.EventStore.{TestProject}/BuildVerificationTests.cs
namespace Hexalith.EventStore.{TestProject};

public class BuildVerificationTests
{
    [Fact]
    public void Project_IsConfigured()
    {
        Assert.True(true);
    }
}
```

Replace `{TestProject}` with: `Contracts.Tests`, `Server.Tests`, `IntegrationTests`.

**Note:** Delete any auto-generated `Class1.cs`, `UnitTest1.cs`, or template files from `dotnet new`.

---

### Task 5: Aspire orchestration and DAPR scaffolding (AC: #7, #8)

> Input: All projects compile and tests pass.
> Action: Configure AppHost topology, ServiceDefaults, Aspire extension, and DAPR component YAMLs.

- [x] 5.1 Configure AppHost Program.cs with topology
- [x] 5.2 Verify ServiceDefaults Extensions.cs (should be generated by aspire-servicedefaults template)
- [x] 5.3 Create stub Aspire integration extension (AddHexalithEventStore)
- [x] 5.4 Create AppHost DaprComponents/ directory with 4 local YAML configs

**Verification:** `dotnet build` succeeds; AppHost project compiles with DAPR component references

#### 5.1 AppHost Program.cs

```csharp
// File: src/Hexalith.EventStore.AppHost/Program.cs
using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var redis = builder.AddRedis("redis");

// DAPR Components (local dev)
var stateStore = builder.AddDaprStateStore("statestore");
var pubSub = builder.AddDaprPubSub("pubsub");

// Services
var commandApi = builder.AddProject<Projects.Hexalith_EventStore_CommandApi>("commandapi")
    .WithDaprSidecar(new DaprSidecarOptions { AppId = "commandapi" })
    .WithReference(stateStore)
    .WithReference(pubSub)
    .WithReference(redis);

var sample = builder.AddProject<Projects.Hexalith_EventStore_Sample>("sample")
    .WithDaprSidecar(new DaprSidecarOptions { AppId = "sample" })
    .WithReference(stateStore)
    .WithReference(pubSub)
    .WithReference(redis);

builder.Build().Run();
```

> **NOTE**: The exact CommunityToolkit.Aspire.Hosting.Dapr API may differ. The dev agent MUST verify the actual API surface at implementation time by checking the package's public API or documentation. The key patterns are: `AddDaprStateStore()`, `AddDaprPubSub()`, `WithDaprSidecar()`. If the API differs, adapt accordingly while maintaining the same topology intent.

#### 5.2 ServiceDefaults

The `aspire-servicedefaults` template generates `Extensions.cs` with OpenTelemetry, health checks, and resilience configuration. Verify the generated file exists and compiles. Do NOT modify it unless it fails to build.

#### 5.3 Aspire Integration Extension Stub

```csharp
// File: src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs
namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Provides extension methods for adding Hexalith EventStore to an Aspire distributed application.
/// This is the public API for the Hexalith.EventStore.Aspire NuGet package.
/// </summary>
public static class HexalithEventStoreExtensions
{
    // TODO: Implement in Story 1.5
    // public static IResourceBuilder<ProjectResource> AddHexalithEventStore(
    //     this IDistributedApplicationBuilder builder, string name) { ... }
}
```

#### 5.4 DAPR Local Component YAMLs

Create directory: `src/Hexalith.EventStore.AppHost/DaprComponents/`

**statestore.yaml**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.redis
  version: v1
  metadata:
    - name: redisHost
      value: localhost:6379
    - name: actorStateStore
      value: "true"
```

**pubsub.yaml**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.redis
  version: v1
  metadata:
    - name: redisHost
      value: localhost:6379
```

**resiliency.yaml**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Resiliency
metadata:
  name: resiliency
spec:
  policies:
    retries:
      defaultRetry:
        policy: constant
        duration: 1s
        maxRetries: 3
    timeouts:
      daprSidecar:
        general: 5s
    circuitBreakers:
      defaultBreaker:
        maxRequests: 1
        interval: 10s
        timeout: 30s
        trip: consecutiveFailures > 3
  targets:
    apps:
      commandapi:
        retry: defaultRetry
        timeout: daprSidecar
        circuitBreaker: defaultBreaker
```

**accesscontrol.yaml**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Configuration
metadata:
  name: accesscontrol
spec:
  accessControl:
    defaultAction: deny
    policies:
      - appId: commandapi
        defaultAction: allow
        operations:
          - name: /v1.0/state/*
            httpVerb: ["GET", "POST", "DELETE"]
            action: allow
          - name: /v1.0/publish/*
            httpVerb: ["POST"]
            action: allow
      - appId: sample
        defaultAction: allow
        operations:
          - name: /v1.0/state/*
            httpVerb: ["GET"]
            action: allow
```

---

### Task 6: Create deploy directory structure (AC: #8)

- [x] 6.1 Create `deploy/dapr/` directory with 6 production component YAML templates
- [x] 6.2 Create `deploy/README.md` stub

**Verification:** All 6 YAML files exist in deploy/dapr/ with `TODO:` placeholders

#### 6.1 Production DAPR Component Templates

All production templates use `TODO:` placeholders for values that must be configured per environment.

**deploy/dapr/statestore-postgresql.yaml**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.postgresql
  version: v1
  metadata:
    - name: connectionString
      value: "TODO: PostgreSQL connection string"
    - name: actorStateStore
      value: "true"
```

**deploy/dapr/statestore-cosmosdb.yaml**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.azure.cosmosdb
  version: v1
  metadata:
    - name: url
      value: "TODO: Cosmos DB account URL"
    - name: masterKey
      value: "TODO: Cosmos DB master key"
    - name: database
      value: "TODO: Database name"
    - name: collection
      value: "TODO: Collection name"
    - name: actorStateStore
      value: "true"
```

**deploy/dapr/pubsub-rabbitmq.yaml**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.rabbitmq
  version: v1
  metadata:
    - name: connectionString
      value: "TODO: RabbitMQ connection string"
    - name: durable
      value: "true"
    - name: deletedWhenUnused
      value: "false"
```

**deploy/dapr/pubsub-kafka.yaml**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.kafka
  version: v1
  metadata:
    - name: brokers
      value: "TODO: Kafka broker addresses"
    - name: authType
      value: "TODO: none|password|mtls|oidc"
```

**deploy/dapr/resiliency.yaml**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Resiliency
metadata:
  name: resiliency
spec:
  policies:
    retries:
      defaultRetry:
        policy: exponential
        maxInterval: 15s
        maxRetries: 10
    timeouts:
      daprSidecar:
        general: 5s
    circuitBreakers:
      defaultBreaker:
        maxRequests: 1
        interval: 60s
        timeout: 60s
        trip: consecutiveFailures > 5
  targets:
    apps:
      commandapi:
        retry: defaultRetry
        timeout: daprSidecar
        circuitBreaker: defaultBreaker
```

**deploy/dapr/accesscontrol.yaml**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Configuration
metadata:
  name: accesscontrol
spec:
  accessControl:
    defaultAction: deny
    trustDomain: "TODO: production trust domain"
    policies:
      - appId: commandapi
        defaultAction: deny
        namespace: "TODO: production namespace"
        operations:
          - name: /v1.0/state/*
            httpVerb: ["GET", "POST", "DELETE"]
            action: allow
          - name: /v1.0/publish/*
            httpVerb: ["POST"]
            action: allow
```

#### 6.2 deploy/README.md

```markdown
# Deployment Configuration

Production DAPR component configurations are in `dapr/`.

See the architecture document for deployment guidance.
```

---

### Task 7: Verify the full build, tests, and pack (AC: #6, #9, #10)

- [x] 7.1 Run `dotnet build` and verify zero errors and zero warnings
- [x] 7.2 Run `dotnet test` and verify all placeholder tests pass
- [x] 7.3 Run `dotnet pack` and verify exactly 5 .nupkg files are produced
- [x] 7.4 Verify no .csproj file contains a `Version=` attribute on any `<PackageReference>`

**Verification Commands:**

```bash
dotnet build --verbosity minimal
dotnet test --verbosity minimal
dotnet pack --output ./artifacts
# Verify 5 .nupkg files: Contracts, Client, Server, Aspire, Testing
ls ./artifacts/*.nupkg
# Clean up artifacts
rm -rf ./artifacts
```

If `dotnet build` produces warnings:
1. Check if they come from .editorconfig rules -- if so, suppress with `<NoWarn>` ONLY for the specific generated files
2. Check if they come from empty projects -- ensure BuildVerification.cs exists in every library
3. Do NOT lower TreatWarningsAsErrors; fix the warnings instead

---

## Dev Notes

### Technical Stack (Verified February 2026)

| Technology | Version | NuGet Package | Source |
|-----------|---------|---------------|--------|
| .NET SDK | 10.0.102 | _(workload)_ | [Download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) |
| Target Framework | net10.0 | - | Architecture doc |
| C# Language | 14 | - | Ships with .NET 10 |
| DAPR Runtime | 1.16.6 | _(CLI)_ | [Releases](https://github.com/dapr/dapr/releases) |
| Dapr.Client | 1.16.1 | Dapr.Client | [NuGet](https://www.nuget.org/packages/Dapr.Client/) |
| Dapr.AspNetCore | 1.16.1 | Dapr.AspNetCore | [NuGet](https://www.nuget.org/packages/Dapr.AspNetCore/) |
| Dapr.Actors | 1.16.1 | Dapr.Actors | [NuGet](https://www.nuget.org/packages/Dapr.Actors/) |
| Dapr.Actors.AspNetCore | 1.16.1 | Dapr.Actors.AspNetCore | [NuGet](https://www.nuget.org/packages/Dapr.Actors.AspNetCore/) |
| Aspire Hosting | 13.1.1 | Aspire.Hosting, Aspire.AppHost.Sdk, Aspire.Hosting.Redis | [NuGet](https://www.nuget.org/packages/Aspire.Hosting/) |
| CommunityToolkit Dapr | 13.0.0 | CommunityToolkit.Aspire.Hosting.Dapr | [NuGet](https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.Dapr/) |
| MinVer | 7.0.0 | MinVer | [NuGet](https://www.nuget.org/packages/MinVer/) |
| MediatR | 14.0.0 | MediatR | [NuGet](https://www.nuget.org/packages/MediatR/) |
| xUnit | 2.9.3 | xunit | [NuGet](https://www.nuget.org/packages/xunit/) |
| Shouldly | 4.3.0 | Shouldly | [NuGet](https://www.nuget.org/packages/Shouldly/) |
| FluentValidation | 11.11.0 | FluentValidation | [NuGet](https://www.nuget.org/packages/FluentValidation/) |
| NSubstitute | 5.3.0 | NSubstitute | [NuGet](https://www.nuget.org/packages/NSubstitute/) |

> **NOTE on Aspire versions**: The architecture document references "Aspire 13.1.0" but individual Aspire NuGet packages use their own versioning (e.g., 9.2.x). The dev agent should verify and use the LATEST STABLE package versions compatible with .NET 10 at implementation time. The critical constraint is using `CommunityToolkit.Aspire.Hosting.Dapr` (NOT the deprecated `Aspire.Hosting.Dapr`).

### NuGet Package Versioning Strategy

- **Tool:** MinVer 7.0.0 (derives SemVer from Git tags, zero configuration needed)
- **Strategy:** Monorepo single-version -- all 5 NuGet packages published with same version
- **Tag prefix:** `v` (configured via `MinVerTagPrefix` in Directory.Build.props)
- **Initial state:** Before any tag exists, MinVer produces `0.0.0-alpha.0.{height}` -- this is expected
- **First release workflow:** Tag `v1.0.0` on release commit; all packages auto-get `1.0.0`
- **Pre-release:** Auto-calculated from tag + commit height (e.g., `1.0.1-alpha.0.3`)
- **Location:** Single `Directory.Build.props` with MinVer configuration

### Project Dependency Graph

| Project | Depends On | Depended On By |
|---------|-----------|----------------|
| **Contracts** | _(none -- leaf node)_ | Client, Server, Testing, CommandApi, Contracts.Tests |
| **Client** | Contracts | Sample, Testing |
| **Server** | Contracts | CommandApi, Testing, Server.Tests |
| **Aspire** | _(none -- standalone)_ | AppHost |
| **Testing** | Contracts, Server | Contracts.Tests, Server.Tests, IntegrationTests |
| **CommandApi** | Server, Contracts, ServiceDefaults | AppHost, IntegrationTests |
| **ServiceDefaults** | _(none)_ | CommandApi, Sample |
| **AppHost** | CommandApi, Sample, Aspire | _(orchestrator -- not referenced)_ |
| **Sample** | Client, ServiceDefaults | AppHost, IntegrationTests |
| **Contracts.Tests** | Contracts, Testing | _(test runner)_ |
| **Server.Tests** | Server, Testing | _(test runner)_ |
| **IntegrationTests** | CommandApi, Sample, Testing | _(test runner)_ |

### DAPR Configuration Conventions

- **Component names**: lowercase, no prefix (e.g., `statestore`, `pubsub`)
- **App IDs**: lowercase, no dots (e.g., `commandapi`, `sample`)
- **Sidecar timeout**: 5 seconds for all calls (architecture rule #14)
- **Local backend**: Redis (via Aspire-managed container)
- **Production backends**: PostgreSQL or Cosmos DB (state), RabbitMQ or Kafka (pub/sub)

### Architecture Enforcement Rules (MUST FOLLOW)

1. **Feature folders only** - Group by domain concept (Events/, Commands/, Actors/), NOT by type (Models/, Services/)
2. **One public type per file** - File name matches type name exactly
3. **No payload in logs** - Only envelope metadata fields in log output
4. **DI via Add* extensions** - Never inline DI registration in Program.cs; always use ServiceCollectionExtensions
5. **IActorStateManager only** - All state operations via actor state manager interface
6. **Write-once event keys** - Event keys are immutable after write (rule #11)
7. **Advisory status writes** - Command status is "fire and forget" (rule #12)
8. **5-second DAPR sidecar timeout** - All sidecar calls timeout at 5s (rule #14)
9. **Snapshot configuration mandatory** - Default 100 events between snapshots (rule #15)
10. **MediatR pipeline order** - LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> CommandHandler

> Rules #3-#10 are NOT exercised in this scaffolding story. They are listed here for context so the dev agent understands the architectural constraints that will govern future stories. For this story, rules #1 and #2 are the only enforceable rules.

### Key Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Namespaces | PascalCase, match folder | `Hexalith.EventStore.Server.Actors` |
| Classes/Records | PascalCase | `AggregateActor`, `EventEnvelope` |
| Interfaces | I + PascalCase | `IAggregateActor`, `IDomainProcessor` |
| Methods | PascalCase + Async suffix | `ProcessCommandAsync` |
| Private fields | _camelCase | `_stateManager` |
| Constants | PascalCase | `MaxRetryCount` |
| Config records | *Options | `EventStoreOptions`, `SnapshotOptions` |
| DI extensions | Add* | `AddEventStoreServer()`, `AddEventStoreClient()` |
| DAPR state keys | colon-separated | `{tenant}:{domain}:{aggId}:events:{seq}` |
| Pub/sub topics | dot-separated | `{tenant}.{domain}.events` |

### Three-Tier Testing Architecture

| Tier | Project | Scope | Dependencies |
|------|---------|-------|-------------|
| 1 - Unit | Contracts.Tests | Pure functions, envelope validation | No DAPR, fast (<1s) |
| 2 - Integration | Server.Tests | Actor pipeline, state store, pub/sub | DAPR test container (<30s) |
| 3 - Contract | IntegrationTests | End-to-end lifecycle, multi-tenant isolation | Full Aspire topology (<2min) |

### What Already Exists in Repository

- **Planning artifacts**: Complete PRD, Architecture, Epics, UX spec in `_bmad-output/planning-artifacts/`
- **Design prototype**: Blazor FluentUI proof-of-concept in `_bmad-output/planning-artifacts/design-directions-prototype/`
- **No production code**: Clean slate -- no .sln, .csproj, src/, tests/, or build files exist
- **Git history**: All planning/research commits on main branch
- **License**: MIT (already present)
- **README.md**: Minimal placeholder (already present)
- **.gitignore**: Standard VisualStudio template (already present)

### Divergence from Other Hexalith Repositories

This repository does NOT use the `Hexalith.Builds` Git submodule pattern found in other Hexalith repos (e.g., Hexalith.EasyAuthentication). Instead:
- Build infrastructure is self-contained in this repository
- `Directory.Build.props` and `Directory.Packages.props` are local, not shared
- This is intentional for this project's monorepo single-version strategy

### CRITICAL GUARDRAILS FOR DEV AGENT

1. **DO NOT hardcode any specific state store or pub/sub backend** - All persistence via DAPR abstractions
2. **DO NOT add Aspire.Hosting.Dapr** - It is DEPRECATED; use `CommunityToolkit.Aspire.Hosting.Dapr` instead
3. **DO NOT put package versions in .csproj files** - All versions MUST be in Directory.Packages.props only (exception: MinVer in Directory.Build.props)
4. **DO NOT create type-based folders** (Models/, Services/) - Use feature folders (Events/, Commands/, Actors/)
5. **DO NOT skip MinVer setup** - Versioning from Git tags is the chosen strategy; configure MinVerTagPrefix=v
6. **DO NOT use .NET SDK version 10.0.102** - Use 10.0.103 (latest as of Feb 10, 2026)
7. **DO NOT add Workflows DAPR building block** - Deferred to v2
8. **Contracts package MUST have zero internal dependencies** - It is the leaf of the dependency graph
9. **All async methods MUST use Async suffix** - Per naming conventions
10. **All error responses MUST use RFC 7807 ProblemDetails** - Per D5 architecture decision
11. **DO NOT create Dockerfiles** - Container images are built via `dotnet publish` and Aspire publisher manifests (Story 7.7)
12. **DO NOT leave template boilerplate** - Remove all auto-generated Class1.cs, UnitTest1.cs, WeatherForecast files from `dotnet new`
13. **DO NOT add CI/CD workflows** - CI pipeline is Story 7.6; this story is local build only

### References

- [Source: _bmad-output/planning-artifacts/architecture.md - Solution Structure, Project Dependencies, Build Infrastructure]
- [Source: _bmad-output/planning-artifacts/epics.md - Epic 1 Story 1.1 Acceptance Criteria and BDD Scenarios]
- [Source: _bmad-output/planning-artifacts/prd.md - FR11, FR21, FR26, FR40, FR42, FR45, NFR27-NFR32]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md - Blazor FluentUI v4 design reference]
- [Source: https://dotnet.microsoft.com/en-us/download/dotnet/10.0 - .NET 10 SDK 10.0.103]
- [Source: https://github.com/dapr/dapr/releases - DAPR 1.16.6]
- [Source: https://www.nuget.org/packages/MinVer/ - MinVer 7.0.0]
- [Source: https://www.nuget.org/packages/MediatR/ - MediatR 14.0.0]
- [Source: https://www.nuget.org/packages/xunit/ - xUnit 2.9.3]
- [Source: https://www.nuget.org/packages/Dapr.Actors.AspNetCore/ - Dapr.Actors.AspNetCore 1.16.1]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- CPM conflict: MinVer Version attribute in Directory.Build.props not compatible with Central Package Management; moved to Directory.Packages.props
- Dapr.Actors version downgrade: Bumped from 1.16.0 to 1.16.1 to match Dapr.Actors.AspNetCore transitive dependency
- CommunityToolkit.Aspire.Hosting.Dapr v13 API change: WithReference for IDaprComponentResource on project builder is obsolete; removed explicit Dapr component references (auto-discovered at runtime)
- ServiceDefaults CA1062: Template-generated Extensions.cs required ArgumentNullException.ThrowIfNull fix for TreatWarningsAsErrors compliance
- AppHost template generated AppHost.cs conflicting with Program.cs top-level statements; deleted AppHost.cs
- Aspire package versions updated from 9.2.1 to 13.1.1 per user instruction to use Aspire 13
- .NET SDK pinned to 10.0.102 (user has 10.0.102 installed; story spec 10.0.103 not available)
- Aspire workload not needed for .NET 10 (templates available natively)
- Code review: Added PackageReadmeFile and README.md pack item to Directory.Build.props to resolve NuGet pack missing readme warnings
- Code review: OpenTelemetry Instrumentation packages at 1.13.0 vs core at 1.13.1 is normal (independent versioning between core and contrib sub-projects); no change needed

### Completion Notes List

- All 7 tasks (30 subtasks) completed successfully
- Build: zero errors, zero warnings
- Tests: 3/3 pass (BuildVerification placeholder tests)
- Pack: 5 .nupkg files produced (Contracts, Client, Server, Aspire, Testing)
- 12 projects in solution organized in src/, samples/, tests/ solution folders
- Deploy directory with 6 production DAPR component YAML templates created
- All .csproj files cleaned: zero Version= attributes on PackageReference entries
- MinVer versioning working: produces 0.0.0-preview.0.{height} before first tag

### Implementation Plan

Followed story tasks sequentially: build infrastructure first (global.json, Directory.Build.props, Directory.Packages.props, nuget.config, .editorconfig, tests/Directory.Build.props), then project creation via dotnet new, solution organization, .csproj cleanup/reference configuration, stub implementations, Aspire/DAPR scaffolding, deploy templates, and final verification.

### Change Log

- 2026-02-12: Story 1.1 implemented - complete solution scaffold with 12 projects, build infrastructure, Aspire orchestration, DAPR components, and deploy templates
- 2026-02-12: Code review fixes applied — added PackageReadmeFile to Directory.Build.props; fixed story File List (5 missing files); corrected AC #4 SDK version text; updated dependency table for AppHost (Aspire.AppHost.Sdk), coverlet.collector in test projects; aligned Dev Notes version table with actual package versions

### File List

**New files:**
- global.json
- Directory.Build.props
- Directory.Packages.props
- nuget.config
- .editorconfig
- Hexalith.EventStore.slnx
- tests/Directory.Build.props
- src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj
- src/Hexalith.EventStore.Contracts/BuildVerification.cs
- src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj
- src/Hexalith.EventStore.Client/BuildVerification.cs
- src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj
- src/Hexalith.EventStore.Server/BuildVerification.cs
- src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj
- src/Hexalith.EventStore.Aspire/BuildVerification.cs
- src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs
- src/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.csproj
- src/Hexalith.EventStore.Testing/BuildVerification.cs
- src/Hexalith.EventStore.CommandApi/Hexalith.EventStore.CommandApi.csproj
- src/Hexalith.EventStore.CommandApi/Program.cs
- src/Hexalith.EventStore.CommandApi/Properties/launchSettings.json
- src/Hexalith.EventStore.CommandApi/appsettings.json
- src/Hexalith.EventStore.CommandApi/appsettings.Development.json
- src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
- src/Hexalith.EventStore.AppHost/Program.cs
- src/Hexalith.EventStore.AppHost/appsettings.json
- src/Hexalith.EventStore.AppHost/appsettings.Development.json
- src/Hexalith.EventStore.AppHost/Properties/launchSettings.json
- src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml
- src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml
- src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml
- src/Hexalith.EventStore.AppHost/DaprComponents/accesscontrol.yaml
- src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj
- src/Hexalith.EventStore.ServiceDefaults/Extensions.cs
- samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj
- samples/Hexalith.EventStore.Sample/Program.cs
- samples/Hexalith.EventStore.Sample/Properties/launchSettings.json
- samples/Hexalith.EventStore.Sample/appsettings.json
- samples/Hexalith.EventStore.Sample/appsettings.Development.json
- tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj
- tests/Hexalith.EventStore.Contracts.Tests/BuildVerificationTests.cs
- tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
- tests/Hexalith.EventStore.Server.Tests/BuildVerificationTests.cs
- tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj
- tests/Hexalith.EventStore.IntegrationTests/BuildVerificationTests.cs
- deploy/README.md
- deploy/dapr/statestore-postgresql.yaml
- deploy/dapr/statestore-cosmosdb.yaml
- deploy/dapr/pubsub-rabbitmq.yaml
- deploy/dapr/pubsub-kafka.yaml
- deploy/dapr/resiliency.yaml
- deploy/dapr/accesscontrol.yaml
