# Story 7.7: Aspire Publisher Deployment Manifests

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **DevOps engineer deploying Hexalith.EventStore**,
I want to generate deployment manifests for Docker Compose, Kubernetes, and Azure Container Apps via `aspire publish`,
so that I can deploy the complete EventStore topology to any target environment without writing custom deployment scripts (FR44, NFR32).

## Acceptance Criteria

1. **Docker Compose publisher** - The AppHost references `Aspire.Hosting.Docker` and calls `AddDockerComposeEnvironment("docker")`. Running `aspire publish --publisher docker` generates a valid output directory containing services for commandapi, sample, and Redis. The generated `.env` file contains parameterized placeholders for secrets and configuration. If Docker is available locally, `docker compose config` validates the output without errors. Document any DAPR sidecar gaps in the output (see AC #4).
2. **Kubernetes publisher** - The AppHost references `Aspire.Hosting.Kubernetes` and calls `AddKubernetesEnvironment("k8s")`. Running `aspire publish --publisher kubernetes` generates Helm charts or Kubernetes YAML manifests containing Deployments/Services for commandapi and sample. If DAPR annotations (`dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/app-port`) are NOT auto-generated, document how to add them manually. If `helm` or `kubectl` is available locally, validate the output.
3. **Azure Container Apps publisher** - The AppHost references `Aspire.Hosting.Azure.AppContainers` and calls `AddAzureContainerAppEnvironment("aca")`. Running `aspire publish --publisher azure` generates Bicep modules for: Container Apps Environment, container apps for commandapi and sample, and managed identity. Document DAPR configuration requirements for ACA.
4. **DAPR component supplementation** - Enhance `deploy/README.md` with an "Aspire Publisher Integration" section that documents, for each publisher target, how to supplement the generated manifests with production DAPR components from `deploy/dapr/` (state store, pub/sub, resiliency, access control). `CommunityToolkit.Aspire.Hosting.Dapr` is a local dev tool and may NOT produce DAPR sidecar/component config in publisher output -- document the actual observed behavior and the manual steps required.
5. **No local development regression** - Existing `dotnet run` and `dotnet aspire run` behavior is unchanged. Publisher environments only activate during `aspire publish`. All Tier 1 unit tests pass. Tier 2 integration tests pass (if DAPR available). Tier 3 Aspire tests (`DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>()`) are not broken by the new environment resources.
6. **Conditional Keycloak** - Verify that publisher manifests exclude Keycloak when `EnableKeycloak=false` (the existing conditional in `Program.cs` should prevent Keycloak from entering the resource model). Document how to configure an external OIDC provider (Authority, Issuer, Audience) via environment variables for production auth.
7. **Generated output is gitignored** - `publish-output/` directory is added to `.gitignore` so validation output is never committed.

## Tasks / Subtasks

- [ ] Task 1: Add publisher NuGet packages to AppHost (AC: #1, #2, #3)
  - [ ] 1.1 Verify actual available versions on nuget.org for `Aspire.Hosting.Docker`, `Aspire.Hosting.Kubernetes`, `Aspire.Hosting.Azure.AppContainers` matching Aspire SDK 13.x. Use the latest stable 13.x version (may not be exactly `13.1.1` -- check nuget.org).
  - [ ] 1.2 Add verified versions to `Directory.Packages.props` under the `Aspire` ItemGroup
  - [ ] 1.3 Add package references to `Hexalith.EventStore.AppHost.csproj` (no version -- central management)
  - [ ] 1.4 Run `dotnet restore` and `dotnet build` to verify packages resolve correctly
- [ ] Task 2: Configure publisher environments in AppHost Program.cs (AC: #1, #2, #3, #5)
  - [ ] 2.1 Add publisher environment lines BEFORE `builder.Build().Run()`:
    ```csharp
    builder.AddDockerComposeEnvironment("docker");
    builder.AddKubernetesEnvironment("k8s");
    builder.AddAzureContainerAppEnvironment("aca");
    ```
  - [ ] 2.2 Verify `dotnet run --project src/Hexalith.EventStore.AppHost/` still works (environments must be inert at runtime)
  - [ ] 2.3 Verify `dotnet build` succeeds for the entire solution
- [ ] Task 3: Verify multi-environment coexistence (AC: #5)
  - [ ] 3.1 Run all Tier 1 unit tests: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ tests/Hexalith.EventStore.Client.Tests/ tests/Hexalith.EventStore.Testing.Tests/ --configuration Release` -- zero regressions
  - [ ] 3.2 Run Tier 2 integration tests (if DAPR available): `dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release` -- zero regressions
  - [ ] 3.3 Verify Tier 3 test fixture still works: `dotnet build tests/Hexalith.EventStore.IntegrationTests/` must compile. If DAPR runtime available, run a smoke test to confirm `DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>()` still starts the topology.
- [ ] Task 4: Validate Docker Compose publisher output (AC: #1, #4)
  - [ ] 4.1 Install Aspire CLI if not present: `dotnet workload install aspire`
  - [ ] 4.2 Run `aspire publish --publisher docker -o ./publish-output/docker`
  - [ ] 4.3 Inspect generated `docker-compose.yaml`: verify commandapi and sample services present
  - [ ] 4.4 Check whether DAPR sidecar containers are included. Document actual behavior: if sidecars are missing, this is expected (CommunityToolkit.Aspire.Hosting.Dapr is primarily a local dev tool)
  - [ ] 4.5 Verify `.env` file contains parameterized placeholders
  - [ ] 4.6 If Docker is available: run `docker compose -f ./publish-output/docker/docker-compose.yaml config` to validate syntax
- [ ] Task 5: Validate Kubernetes publisher output (AC: #2, #4)
  - [ ] 5.1 Run `aspire publish --publisher kubernetes -o ./publish-output/k8s`
  - [ ] 5.2 Inspect generated manifests: verify Deployments/Services for commandapi and sample
  - [ ] 5.3 Check whether DAPR annotations are auto-generated on pod templates. Document actual behavior.
  - [ ] 5.4 If `helm` available: run `helm lint ./publish-output/k8s/`. If `kubectl` available: `kubectl apply --dry-run=client -f ./publish-output/k8s/`
- [ ] Task 6: Validate Azure Container Apps publisher output (AC: #3, #4)
  - [ ] 6.1 Run `aspire publish --publisher azure -o ./publish-output/azure`
  - [ ] 6.2 Inspect generated Bicep modules: verify Container Apps Environment, container app definitions, managed identity
  - [ ] 6.3 Check whether DAPR configuration is present in Bicep resources. Document actual behavior.
- [ ] Task 7: Enhance deploy/README.md with Aspire publisher integration (AC: #4)
  - [ ] 7.1 Add "Aspire Publisher Integration" section to `deploy/README.md` with subsections for Docker Compose, Kubernetes, and Azure Container Apps
  - [ ] 7.2 For each target, document: (a) the `aspire publish` command, (b) what the publisher generates, (c) what DAPR components are missing and how to supplement them from `deploy/dapr/`, (d) environment variable resolution for secrets
  - [ ] 7.3 Document that generated manifests are tied to the Aspire SDK version -- regenerate (don't manually edit) when upgrading Aspire
  - [ ] 7.4 Document external OIDC provider configuration for production auth (replacing Keycloak)
- [ ] Task 8: Handle Keycloak conditional exclusion (AC: #6)
  - [ ] 8.1 Run `aspire publish --publisher docker -o ./publish-output/docker-no-keycloak` with `EnableKeycloak=false` environment variable
  - [ ] 8.2 Verify Keycloak is NOT present in the generated output
  - [ ] 8.3 Document the `EnableKeycloak=false` flag and external OIDC configuration in deploy/README.md
- [ ] Task 9: Add publish-output to .gitignore (AC: #7)
  - [ ] 9.1 Add `publish-output/` entry to root `.gitignore`

## Dev Notes

### Prerequisites

- **Aspire CLI**: Required for `aspire publish`. Install via: `dotnet workload install aspire`
- **Validation tools (optional)**: Docker (for `docker compose config`), Helm/kubectl (for K8s validation), Azure CLI (for Bicep validation). Mark validation subtasks as "skip if tool unavailable" -- the core deliverable is the AppHost configuration, not the validation tooling.

### Architecture Constraints

- **FR44**: DevOps engineer can generate deployment manifests for Docker Compose, Kubernetes, or Azure Container Apps via Aspire publishers
- **NFR32**: System must be deployable via Aspire publishers to Docker Compose, Kubernetes, and Azure Container Apps without custom deployment scripts
- **Rule #4**: NEVER add custom retry logic -- DAPR resiliency only
- **NFR29**: Backend switch via YAML only, zero code changes -- publisher manifests must not hardcode backend choices
- **D10**: GitHub Actions as CI/CD platform (Story 7.6 already implements this)

### Aspire 13.x Publisher Model (CRITICAL)

In Aspire 13.0+, publishers use the **environment resource model**, NOT the legacy publisher APIs:

```csharp
// CORRECT (Aspire 13.x) -- environment resources
builder.AddDockerComposeEnvironment("docker");
builder.AddKubernetesEnvironment("k8s");
builder.AddAzureContainerAppEnvironment("aca");

// WRONG (removed in Aspire 9.3) -- do NOT use
// builder.AddDockerComposePublisher();
// builder.AddKubernetesPublisher();
```

**Key behaviors:**
- Environment resources only activate during `aspire publish` -- zero impact on `dotnet run` / `aspire run`
- Multiple environments can coexist in the same AppHost
- `aspire publish --publisher <name>` selects which environment to generate for
- Generated artifacts contain **parameterized placeholders** (`${VAR}`) -- not resolved secrets

### NuGet Packages Required

| Package | Purpose | Add To |
|---------|---------|--------|
| `Aspire.Hosting.Docker` | Docker Compose publisher | `Directory.Packages.props` + AppHost csproj |
| `Aspire.Hosting.Kubernetes` | Kubernetes publisher | `Directory.Packages.props` + AppHost csproj |
| `Aspire.Hosting.Azure.AppContainers` | Azure Container Apps publisher | `Directory.Packages.props` + AppHost csproj |

**Version pinning:** Use the latest stable 13.x version available on nuget.org. These packages may not be at exactly `13.1.1` -- verify actual available versions before adding. Existing Aspire packages in `Directory.Packages.props` are at `13.1.1` (Aspire.Hosting, Aspire.Hosting.Testing) or `13.0.0` (CommunityToolkit).

### DAPR + Publisher Integration (CRITICAL -- Realistic Expectations)

`CommunityToolkit.Aspire.Hosting.Dapr` is primarily a **local development orchestration tool**. Aspire publishers may NOT fully translate `.WithDaprSidecar()` configuration into publisher output. Expect that:

1. **Publishers generate base infrastructure** (container definitions, networking, environment variables)
2. **DAPR sidecar/component config is a manual post-generation step** per the existing `deploy/README.md` instructions

| Publisher | Likely DAPR Sidecar Handling | Component Config (Manual) |
|-----------|------------------------------|---------------------------|
| Docker Compose | May or may not include sidecar containers -- **verify and document actual behavior** | Mount `deploy/dapr/` as volume (`/components`) |
| Kubernetes | May or may not include DAPR annotations -- **verify and document actual behavior** | `kubectl apply -f deploy/dapr/*.yaml` as CRDs + install DAPR operator |
| Azure Container Apps | ACA has native DAPR support but Bicep config may need manual additions | Configure DAPR in ACA environment via Bicep/Portal |

**Do NOT assume DAPR integration works in publishers.** Verify actual output and document the gap. This is expected behavior, not a bug.

**Production DAPR components** are already in `deploy/dapr/`:
- `statestore-postgresql.yaml`, `statestore-cosmosdb.yaml`
- `pubsub-rabbitmq.yaml`, `pubsub-kafka.yaml`, `pubsub-servicebus.yaml`
- `resiliency.yaml`, `accesscontrol.yaml`, `subscription-sample-counter.yaml`

### Technical Stack

| Technology | Version | Notes |
|-----------|---------|-------|
| .NET SDK | 10.0.102 | Pinned in `global.json` |
| Aspire SDK | 13.1.1 | `Aspire.AppHost.Sdk/13.1.1` |
| Aspire.Hosting | 13.1.1 | Core hosting abstractions |
| Aspire.Hosting.Docker | latest stable 13.x | Docker Compose publisher -- verify version on nuget.org |
| Aspire.Hosting.Kubernetes | latest stable 13.x | Kubernetes publisher -- verify version on nuget.org |
| Aspire.Hosting.Azure.AppContainers | latest stable 13.x | Azure Container Apps publisher -- verify version on nuget.org |
| CommunityToolkit.Aspire.Hosting.Dapr | 13.0.0 | DAPR sidecar integration (existing) |
| DAPR Runtime | 1.16.x | Sidecar model |

### AppHost Modification Pattern

Current `Program.cs` ends with `builder.Build().Run();`. Add publisher environments **before** this line:

```csharp
// --- Publisher environments (only activate during `aspire publish`) ---
builder.AddDockerComposeEnvironment("docker");
builder.AddKubernetesEnvironment("k8s");
builder.AddAzureContainerAppEnvironment("aca");

builder.Build().Run();
```

**DO NOT** use `.WithComputeEnvironment()` on individual resources unless disambiguation is needed. When only one environment of each type exists, Aspire auto-assigns all compute resources.

### Existing AppHost Topology (DO NOT break)

```
commandapi  -- ProjectResource + DAPR sidecar (app-id: commandapi)
  |- statestore (state.in-memory, actorStateStore=true)
  |- pubsub (pubsub.redis)
sample      -- ProjectResource + DAPR sidecar (app-id: sample)
  |- NO state store or pub/sub references (zero-infrastructure domain service)
keycloak    -- Conditional (EnableKeycloak != "false")
  |- Realm import from KeycloakRealms/hexalith-realm.json
```

### Aspire Testing Compatibility (CRITICAL)

Story 7.5 Tier 3 tests use `DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>()`. This loads the **full AppHost** including any publisher environments. If adding publisher packages injects middleware, changes the resource graph, or causes ambiguity errors during testing, it will break Tier 3 tests.

**Verification required:** After adding publisher environments, confirm that building and starting the IntegrationTests project still works. If publisher environments cause issues in test context, investigate whether `DistributedApplicationTestingBuilder` ignores publisher environments or if conditional logic is needed.

### Generated Manifests Are Version-Tied

Publisher output format is specific to the Aspire SDK version. When upgrading Aspire in the future:
- **Regenerate** manifests via `aspire publish` -- do not manually edit previously generated files
- Review changelogs for publisher output format changes
- Test that production deployments still work after regeneration

### File Structure

```
src/Hexalith.EventStore.AppHost/
  Program.cs                         <- MODIFY: Add 3 publisher environment lines
  Hexalith.EventStore.AppHost.csproj <- MODIFY: Add 3 publisher package references

Directory.Packages.props             <- MODIFY: Add 3 publisher package versions
.gitignore                           <- MODIFY: Add publish-output/ entry

deploy/
  README.md                          <- MODIFY: Add "Aspire Publisher Integration" section
  dapr/                              <- Existing production components (no changes)
```

### Project Structure Notes

- Changes are minimal: 3 lines in `Program.cs`, 3 package references in csproj, 3 version entries in `Directory.Packages.props`, 1 section added to `deploy/README.md`, 1 line in `.gitignore`
- No new C# classes or interfaces needed
- No changes to any existing project except AppHost
- No new directories or standalone documentation files

### References

- [Source: _bmad-output/planning-artifacts/prd.md#FR44 -- Aspire publisher deployment manifests]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR32 -- Deployable via Aspire publishers]
- [Source: _bmad-output/planning-artifacts/architecture.md#Hosting -- Aspire publishers (Docker Compose, Kubernetes, Azure Container Apps)]
- [Source: _bmad-output/planning-artifacts/architecture.md#D10 CI/CD Pipeline -- GitHub Actions]
- [Source: deploy/README.md -- Existing deployment instructions and backend compatibility matrix]
- [Source: deploy/dapr/ -- Production DAPR component configurations]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs -- Current topology]
- [Source: src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs -- Topology wiring]
- [Source: Directory.Packages.props -- Central package management]
- [Source: https://aspire.dev/deployment/overview/ -- Aspire publishing and deployment overview]
- [Source: https://aspire.dev/integrations/compute/docker/ -- Docker integration]
- [Source: https://aspire.dev/integrations/compute/kubernetes/ -- Kubernetes integration]
- [Source: https://aspire.dev/integrations/cloud/azure/configure-container-apps/ -- Azure Container Apps]
- [Source: https://learn.microsoft.com/dotnet/aspire/compatibility/9.3/remove-publisher-apis -- Breaking change: publisher API removal]

### Previous Story Intelligence

**From Story 7.6 (CI/CD Pipeline and NuGet Publishing):**
- GitHub Actions CI/CD is set up with `ci.yml` and `release.yml`
- MinVer versioning from Git tags, `fetch-depth: 0` required
- All 5 NuGet packages share same version (monorepo single-version strategy)
- Security hardening: pin actions by SHA, explicit permissions, `--verbosity quiet`
- Tier 3 Aspire tests as optional CI job (full DAPR runtime needed)

**From Story 7.5 (E2E Contract Tests with Aspire Topology - Tier 3):**
- Aspire topology tests use `DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>()`
- Adding publisher environments must NOT break this test pattern
- AppPort intentionally omitted for Aspire Testing compatibility (randomized ports)
- Build succeeds with 785+ passing tests
- Review found auth token mismatch issues (15 failures) -- unrelated to this story but be aware of pre-existing test state

**From Story 7.3 (Production DAPR Component Configurations):**
- All production DAPR components already exist in `deploy/dapr/`
- Component names (`statestore`, `pubsub`) identical across environments
- Environment variable pattern for secrets (e.g., `POSTGRES_CONNECTION_STRING`)

### Git Intelligence

Recent commits show:
- Stories 7.1-7.5 established the complete Aspire topology, sample domain, and testing
- Story 7.6 added CI/CD -- publisher manifests complement this for deployment
- Repository uses .NET 10, Aspire 13.1, DAPR 1.16.x
- Central package management in `Directory.Packages.props`
- `Hexalith.EventStore.slnx` modern solution format

### Latest Tech Information

**Aspire 13.x Publishers (2026):**
- `AddDockerComposeEnvironment()`, `AddKubernetesEnvironment()`, `AddAzureContainerAppEnvironment()` -- current API
- Legacy `AddDockerComposePublisher()` / `AddKubernetesPublisher()` removed in Aspire 9.3
- `aspire publish` generates parameterized artifacts; `aspire deploy` resolves and applies
- Multiple environments can coexist in one AppHost without runtime impact
- Docker publisher generates `docker-compose.yaml` + `.env`
- Kubernetes publisher generates Helm charts + K8s YAML manifests
- Azure publisher generates Bicep modules with managed identity, ACR, Log Analytics

**Dapr + Aspire Publishers (expect gaps):**
- `CommunityToolkit.Aspire.Hosting.Dapr` handles local dev orchestration only
- Publisher output may NOT include DAPR sidecar configuration -- this is expected, not a bug
- Docker Compose: May need manual sidecar container definitions + volume-mounted components
- Kubernetes: May need manual DAPR annotations + DAPR operator + component CRDs
- Azure Container Apps: May need manual DAPR environment configuration in Bicep

### Out of Scope

- Domain service hot-reload validation (Story 7.8)
- Actual deployment to any target environment (this story generates manifests only)
- Container image building and registry pushing (CI/CD handles this)
- Production secret provisioning (manual DevOps step)
- DAPR component selection for specific environments (already handled by `deploy/dapr/`)
- Branch protection rules or GitHub environment configuration
- Automated deployment pipelines (can be added after manifests are validated)
- Installing Docker, Helm, kubectl, or Azure CLI on the dev machine (optional validation tools)

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
