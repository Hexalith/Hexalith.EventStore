# Story 8.6: Deployment Manifests & Environment Portability

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a DevOps engineer,
I want to deploy EventStore to different environments by changing only DAPR component configuration,
so that the same application runs across Redis, PostgreSQL, and cloud backends without code changes.

## Acceptance Criteria

1. **Given** Aspire publishers,
   **When** generating deployment manifests,
   **Then** Docker Compose, Kubernetes, and Azure Container Apps targets are supported (FR44).

2. **Given** a different target environment,
   **When** DAPR component YAML files are changed,
   **Then** the system functions correctly with zero application code changes, zero recompilation, zero redeployment (FR43, NFR29).

## Definition of Done

Story is complete when:
- **Required:** Tasks 0, 4, 5, 6 pass (build, portability validation, documentation, Dockerfiles)
- **Best-effort:** Tasks 1-3 (publisher validation) — if publishers fail after 15-min timebox, document failure and proceed
- **Conditional:** Task 7 — run Tier 1 tests only if any `src/` or `samples/` files were modified during Tasks 1-8
- **Captures fixes:** Task 8 applies any gap fixes found during validation

## Context: What Already Exists

This is a **validation and gap-analysis story**, not a greenfield story. The deployment infrastructure is already substantially built. The work is to audit, validate, and fill any gaps.

### Current Deployment Infrastructure

| Component | Status | Location |
|-----------|--------|----------|
| Production DAPR components (9 YAMLs: 5 backend configs + 2 infrastructure + 2 subscription templates) | Built | `deploy/dapr/` |
| Deployment README guide (340+ lines) | Built | `deploy/README.md` |
| AppHost with PUBLISH_TARGET support | Built | `src/Hexalith.EventStore.AppHost/Program.cs` |
| Aspire publisher NuGet packages | Installed | AppHost.csproj (Docker, Kubernetes, Azure.AppContainers) |
| Local dev DAPR components (6 YAMLs) | Built | `src/Hexalith.EventStore.AppHost/DaprComponents/` |
| Dockerfiles (CommandApi + Sample) | Built | `src/Hexalith.EventStore.CommandApi/Dockerfile`, `samples/.../Dockerfile` |
| CI/CD workflows (5 files) | Built | `.github/workflows/` |
| Sample Azure Bicep templates | Built | `samples/deploy/azure/` |
| deploy-staging.yml workflow | Built | `.github/workflows/deploy-staging.yml` |

### Production DAPR Component Configs (`deploy/dapr/`)

| File | Backend | Purpose |
|------|---------|---------|
| `statestore-postgresql.yaml` | PostgreSQL | Production state store with ETag, actor support |
| `statestore-cosmosdb.yaml` | Azure Cosmos DB | Scale state store |
| `pubsub-rabbitmq.yaml` | RabbitMQ | Production pub/sub |
| `pubsub-kafka.yaml` | Apache Kafka | High-throughput pub/sub |
| `pubsub-servicebus.yaml` | Azure Service Bus | Enterprise pub/sub |
| `accesscontrol.yaml` | N/A | Deny-by-default mTLS |
| `resiliency.yaml` | N/A | Exponential backoff, circuit breakers |
| `subscription-sample-counter.yaml` | N/A | Declarative subscription template |
| `subscription-projection-changed.yaml` | N/A | Projection change subscription |

### Local Dev DAPR Components (`AppHost/DaprComponents/`)

| File | Backend | Purpose |
|------|---------|---------|
| `statestore.yaml` | Redis (in-memory) | Local dev state store |
| `pubsub.yaml` | Redis Streams | Local dev pub/sub |
| `accesscontrol.yaml` | N/A | Allow-by-default (no mTLS) |
| `resiliency.yaml` | N/A | Same as production |
| `configstore.yaml` | N/A | Configuration/secrets |
| `subscription-sample-counter.yaml` | N/A | Sample subscription |

### Aspire Publisher Configuration (`AppHost/Program.cs`)

The AppHost already supports three publisher targets via `PUBLISH_TARGET` environment variable (lines 96-113):
- `PUBLISH_TARGET=docker` -> Docker Compose output
- `PUBLISH_TARGET=k8s` -> Kubernetes Helm charts
- `PUBLISH_TARGET=aca` -> Azure Container Apps Bicep

Installed publisher packages in `AppHost.csproj`:
- `Aspire.Hosting.Docker`
- `Aspire.Hosting.Kubernetes`
- `Aspire.Hosting.Azure.AppContainers`

### GitHub Actions CI/CD

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `ci.yml` | Push/PR to main | Build + Tier 1/2/3 tests |
| `release.yml` | Git tags `v*` | Pack + validate + publish 6 NuGet packages |
| `deploy-staging.yml` | CI success | Docker build/push + kubectl restart |

## Tasks / Subtasks

- [ ] Task 0: Build solution to verify no build errors (AC: #1, #2)
  - [ ] 0.1 Run full solution build:
    ```bash
    dotnet build Hexalith.EventStore.slnx --configuration Release
    ```
  - [ ] 0.2 If build fails, fix build errors before proceeding. Publisher validation and Dockerfile builds depend on a clean build.

- [ ] Task 1: Validate Aspire publisher output for Docker Compose (AC: #1)
  **Timebox: 15 minutes.** If publisher generation fails after troubleshooting, document the failure and skip to Task 4. Publisher validation is not a blocker for portability (Task 4) or documentation (Task 5) validation.
  - [ ] 1.1 Run `aspire publish` with Docker target:
    ```bash
    cd D:/Hexalith.EventStore
    PUBLISH_TARGET=docker EnableKeycloak=false dotnet run --project src/Hexalith.EventStore.AppHost/ -- publish -o ./publish-output/docker
    ```
    **CLI syntax fallback:** If the above command fails, Aspire 13.1.2 may use a different invocation. Try these alternatives in order:
    1. `dotnet aspire publish --project src/Hexalith.EventStore.AppHost/ -o ./publish-output/docker`
    2. `dotnet publish src/Hexalith.EventStore.AppHost/ -p:PublishingTarget=docker -o ./publish-output/docker`
    3. Check `dotnet aspire --help` or Context7 for Aspire 13.1.2 publisher docs.
  - [ ] 1.2 List the output directory (`ls -R ./publish-output/docker`) to see what was actually generated. Aspire versions may produce different structures — adapt validation to actual output.
  - [ ] 1.3 Verify output is **non-empty** and contains a compose file (`docker-compose.yaml`, `docker-compose.yml`, or `compose.yaml`) with service definitions for `commandapi` and `sample`.
  - [ ] 1.4 Verify environment variables, port mappings, and network configuration are present.
  - [ ] 1.5 Document any gaps in Docker Compose output (e.g., missing DAPR sidecar containers — Aspire publishers may not auto-generate DAPR sidecars). This is a known limitation to document, not fix.
  - [ ] 1.6 Clean up `publish-output/` directory after validation.

- [ ] Task 2: Validate Aspire publisher output for Kubernetes (AC: #1)
  **Timebox: 15 minutes.** Same fallback as Task 1 — skip to Task 4 if publisher fails.
  - [ ] 2.1 Run `aspire publish` with Kubernetes target (use CLI fallback from Task 1 if needed):
    ```bash
    PUBLISH_TARGET=k8s EnableKeycloak=false dotnet run --project src/Hexalith.EventStore.AppHost/ -- publish -o ./publish-output/k8s
    ```
  - [ ] 2.2 List the output directory (`ls -R ./publish-output/k8s`) to see actual structure.
  - [ ] 2.3 Verify output is **non-empty** and contains Helm chart structure (`Chart.yaml`, `values.yaml`, Deployments, Services).
  - [ ] 2.4 Verify container image references and resource configurations are present.
  - [ ] 2.5 If `values.yaml` exists, verify key fields are parameterized (not hardcoded in templates): `replicaCount`, `image.repository`, `image.tag`, `resources.limits`. Hardcoded values make the Helm chart unusable for real deployments.
  - [ ] 2.6 Document any gaps (e.g., missing DAPR pod annotations `dapr.io/enabled`, `dapr.io/app-id`). DAPR annotations must be manually added in production — this is expected and should be documented. Note: `EnableKeycloak=true` is expected to fail for K8s (bind mounts not supported).
  - [ ] 2.7 Clean up `publish-output/` directory after validation.

- [ ] Task 3: Validate Aspire publisher output for Azure Container Apps (AC: #1)
  **Timebox: 15 minutes.** Same fallback as Task 1 — skip to Task 4 if publisher fails.
  - [ ] 3.1 Run `aspire publish` with ACA target (use CLI fallback from Task 1 if needed):
    ```bash
    PUBLISH_TARGET=aca EnableKeycloak=false dotnet run --project src/Hexalith.EventStore.AppHost/ -- publish -o ./publish-output/azure
    ```
  - [ ] 3.2 List the output directory (`ls -R ./publish-output/azure`) to see actual structure.
  - [ ] 3.3 Verify output is **non-empty** and contains Bicep modules (`main.bicep`, per-service modules).
  - [ ] 3.4 Verify managed identity, ACR, and Container Apps Environment are configured.
  - [ ] 3.5 Verify Bicep accepts parameters for resource group, location, and container SKU — hardcoded values force forking. Check if Container Apps Environment module includes `daprEnabled: true` — the one line everyone forgets.
  - [ ] 3.6 Document any gaps (e.g., DAPR enablement in Container Apps Environment must be done manually). Note: `EnableKeycloak=true` is expected to fail for ACA (bind mounts not supported).
  - [ ] 3.7 Clean up `publish-output/` directory after validation.

- [ ] Task 4: Validate DAPR component portability — backend swap (AC: #2)
  - [ ] 4.1 Verify that `deploy/dapr/` contains production-grade state store configs for at least 2 backends (PostgreSQL and Cosmos DB). Each MUST include mandatory metadata:
    - `actorStateStore: "true"` (required for DAPR actors)
    - ETag concurrency support (implicit in PostgreSQL/Cosmos — verify component type supports it)
    - Connection string uses environment variable reference (`$POSTGRES_CONNECTION_STRING`), never hardcoded
  - [ ] 4.2 Verify that `deploy/dapr/` contains production-grade pub/sub configs for at least 2 backends (RabbitMQ and Kafka — Service Bus is a bonus). Each MUST include:
    - Dead-letter topic configuration (`deadLetterTopic` or backend equivalent)
    - CloudEvents content type enabled
    - At-least-once delivery guarantee (backend-specific config)
  - [ ] 4.3 Verify that production access control (`deploy/dapr/accesscontrol.yaml`) uses `defaultAction: deny` with mTLS (SPIFFE trust domain). Note: mTLS trust domain must match the DAPR deployment namespace in production.
  - [ ] 4.4 Verify that production resiliency (`deploy/dapr/resiliency.yaml`) has exponential backoff, circuit breakers, and timeouts. **Note:** resiliency.yaml is REQUIRED, not optional — its absence means no circuit breakers, no timeout, no backoff. First transient failure cascades.
  - [ ] 4.5 Verify scopes on EVERY production DAPR component file **individually** — enumerate each file and confirm `scopes: ["commandapi"]` (D4 requirement). Do not batch-assert; check each file explicitly to catch a single missing scope.
  - [ ] 4.6 Verify the DAPR component **name** (the `metadata.name` field inside YAML, not the filename) matches what the application code expects. The application references state store and pub/sub by component name. A renamed component = broken deployment. Check: what name does `AppHost/DaprComponents/statestore.yaml` use? Production YAMLs must use the same name.
  - [ ] 4.7 Verify that switching from local Redis to production PostgreSQL requires ONLY changing the DAPR component YAML — no code changes, no recompilation.
  - [ ] 4.8 Grep the application code for hard-coded backend references. Search for:
    - Backend names: `Redis`, `PostgreSQL`, `Cosmos`, `RabbitMQ`, `Kafka`, `ServiceBus`
    - Port numbers: `6379`, `5432`, `27017`, `5672`, `9092`
    - Connection string patterns: `ConnectionString`, `server=`, `host=`
    - Backend-specific NuGet package imports: `StackExchange.Redis`, `Npgsql`, `Microsoft.Azure.Cosmos`
    The only allowed references are in DAPR component YAML files, `deploy/`, and test fixtures. Any backend reference in `src/` code is a portability violation.
  - [ ] 4.8 Specifically check `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` for backend-specific references. This is a **published NuGet package** — any Redis-specific code baked in there is a portability violation for consumers, not just local dev.
  - [ ] 4.9 Verify `deploy/dapr/statestore-cosmosdb.yaml` has correct partition key configuration aligned with the actor ID pattern `{tenant}:{domain}:{aggId}`. Misconfigured partition keys cause cross-partition queries that blow RU budgets. ETag-based optimistic concurrency (D1 hard requirement) must work with the chosen partition strategy.

- [ ] Task 5: Validate deploy/README.md completeness (AC: #1, #2)
  - [ ] 5.1 Verify `deploy/README.md` documents all three publisher targets (Docker Compose, Kubernetes, Azure Container Apps).
  - [ ] 5.2 Verify it documents the Backend Compatibility Matrix (state stores and pub/sub backends with their features).
  - [ ] 5.3 Verify it documents environment variables for production (connection strings, DAPR config).
  - [ ] 5.4 Verify it documents the security posture difference (local allow-by-default vs. production deny-by-default).
  - [ ] 5.5 Verify it documents secret management guidance (never hardcode, use K8s Secrets / Azure Key Vault).
  - [ ] 5.6 Verify it documents the **manual DAPR sidecar injection steps** for each publisher target. This is the #1 thing a DevOps engineer will hit — Aspire publishers don't generate DAPR sidecars. For each target, README must explain: (a) Docker Compose: how to add sidecar containers alongside each service, (b) Kubernetes: how to add DAPR pod annotations (`dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/app-port`), (c) Azure Container Apps: how to enable DAPR in the Container Apps Environment and configure components.
  - [ ] 5.7 Verify README includes guidance for **adding a new backend** (e.g., Azure Table Storage, DynamoDB). The NFR29 portability promise means "any DAPR-compatible backend" — not just the 5 pre-built configs. At minimum, point to DAPR component docs and explain what metadata fields are required.
  - [ ] 5.8 Verify README recommends **GitOps or sealed-secrets** for production DAPR component deployment — not manual `kubectl apply`. Component YAMLs contain infrastructure routing; unauthorized modification redirects all events.
  - [ ] 5.9 Verify README includes (or links to) a **reference `docker-compose.override.yml`** showing DAPR sidecar containers alongside application services. This is the #1 DX win for Docker Compose users who need a working local-to-staging path.
  - [ ] 5.10 Verify the main project `README.md` links to `deploy/README.md`. If the deployment guide exists but nobody can find it, it's invisible.
  - [ ] 5.11 Verify `deploy-staging.yml` workflow references correct Dockerfile paths, build context (must match repo root for COPY commands), and image tags. Flag any `latest`-style tags as a security concern — production should use immutable tags (SHA digest or SemVer).
  - [ ] 5.12 If any section is missing or inaccurate, update `deploy/README.md`.

- [ ] Task 6: Validate Dockerfiles (AC: #1)
  - [ ] 6.1 Verify `src/Hexalith.EventStore.CommandApi/Dockerfile` exists and builds correctly:
    ```bash
    docker build -f src/Hexalith.EventStore.CommandApi/Dockerfile -t hexalith-commandapi:test .
    ```
    **If Docker is unavailable** (common on Windows without Docker Desktop running), structural validation is sufficient — do NOT block the story on Docker availability. Verify: multi-stage build pattern, correct .NET 10 base images, correct COPY paths referencing the solution structure, correct EXPOSE ports, and correct ENTRYPOINT.
  - [ ] 6.2 Verify `samples/Hexalith.EventStore.Sample/Dockerfile` exists and is structurally correct.
  - [ ] 6.3 Both Dockerfiles should use .NET 10 base images (`mcr.microsoft.com/dotnet/aspnet:10.0` and `/sdk:10.0`).
  - [ ] 6.4 Verify Dockerfile `COPY` paths are correct when built from repo root (the standard CI build context). If `docker build -f src/.../Dockerfile .` is run from repo root, the COPY commands must reference paths relative to repo root, not relative to the Dockerfile location.
  - [ ] 6.5 Note: Dockerfile `EXPOSE` is informational only — DAPR sidecar connects via `--app-port`, not Docker EXPOSE. Check that EXPOSE is reasonable (e.g., 8080) but don't block on port mismatch with AppHost.

- [ ] Task 7: Validate all Tier 1 tests pass — zero regressions (AC: #1, #2)
  **Conditional:** Run only if any `src/` or `samples/` files were modified during Tasks 1-8. If this story makes zero source code changes, skip and document "No src changes — Tier 1 skipped" in Completion Notes.
  - [ ] 7.1 Run ALL Tier 1 test suites:
    ```bash
    dotnet test tests/Hexalith.EventStore.Contracts.Tests/ --configuration Release
    dotnet test tests/Hexalith.EventStore.Client.Tests/ --configuration Release
    dotnet test tests/Hexalith.EventStore.Sample.Tests/ --configuration Release
    dotnet test tests/Hexalith.EventStore.Testing.Tests/ --configuration Release
    dotnet test tests/Hexalith.EventStore.SignalR.Tests/ --configuration Release
    ```
  - [ ] 7.2 Document total test count and any failures in Completion Notes.

- [ ] Task 8: Fill critical gaps (if any found) (AC: #1, #2)
  **Scope:** Fixes target `deploy/`, `deploy/README.md`, Dockerfiles, and AppHost publisher config ONLY. Never modify local dev components in `AppHost/DaprComponents/`.
  - [ ] 8.1 If any publisher target fails to generate output, investigate and fix the AppHost publisher configuration.
  - [ ] 8.2 If any production DAPR component is missing or structurally invalid, create/fix it in `deploy/dapr/`.
  - [ ] 8.3 If a backend-specific reference is found in application code, refactor to use DAPR abstractions.
  - [ ] 8.4 If Dockerfiles are missing or broken, fix them.
  - [ ] 8.5 If NO critical gaps are found, document "Deployment infrastructure complete" in Completion Notes.

## Dev Notes

### THIS IS A VALIDATION/AUDIT STORY

The deployment infrastructure **already exists** with production DAPR components, Aspire publisher support, Dockerfiles, and CI/CD workflows. This story validates that:
1. All three Aspire publisher targets generate correct output (FR44)
2. Backend swap via DAPR component YAML requires zero code changes (FR43, NFR29)
3. Documentation accurately reflects the deployment architecture

### Architecture: Environment Portability Design

The portability guarantee (NFR29) is achieved through DAPR abstraction:

```
Application Code (CommandApi, Server)
    │
    ▼
DAPR Abstractions (IActorStateManager, DaprClient)
    │
    ▼
DAPR Sidecar (runtime)
    │
    ▼
DAPR Component YAML (configuration)
    │
    ▼
Backend (Redis / PostgreSQL / Cosmos DB / RabbitMQ / Kafka / Service Bus)
```

**Switching backends** = changing DAPR component YAML only. Application code talks to DAPR abstractions, never to backends directly.

### Critical Deployment Notes

- **`deploy/dapr/` contains 9 YAMLs:** 5 are backend configs (2 state stores + 3 pub/subs), 2 are infrastructure (access control + resiliency), and 2 are subscription templates (reference examples, not backend configs). Don't conflate the count.
- **resiliency.yaml is REQUIRED, not optional.** Its absence means DAPR defaults apply — no circuit breaker, no configured timeout, no exponential backoff. First transient failure cascades system-wide.
- **DAPR component names matter.** The application references state store and pub/sub by the `metadata.name` field inside the YAML. Renaming a component without updating all references = broken deployment.
- **Production YAMLs should fail-closed.** If an environment variable like `$POSTGRES_CONNECTION_STRING` is unset, the DAPR component should fail to initialize — not silently connect to localhost. This is a DAPR platform behavior, not something this story can test locally, but it should be noted in `deploy/README.md` as an operations concern.
- **Docker Compose publisher is for staging/evaluation.** Kubernetes and Azure Container Apps are the production deployment targets. Docker Compose output requires the most manual work (adding sidecars, networks, volumes) and doesn't provide HA or orchestration.

### Key Architecture Decisions

- **D1:** Event storage uses `IActorStateManager` — backend-agnostic by design (Rule 6)
- **D4:** Access control scopes infrastructure to `commandapi` only — domain services have zero backend access
- **D6:** Pub/sub topics use `{tenant}.{domain}.events` pattern — works on all DAPR pub/sub backends
- **D7:** Domain service invocation via `DaprClient.InvokeMethodAsync` — no direct HTTP
- **NFR27:** Function correctly with any DAPR-compatible state store supporting key-value + ETag (validated: Redis, PostgreSQL)
- **NFR28:** Function correctly with any DAPR-compatible pub/sub supporting CloudEvents 1.0 + at-least-once (validated: RabbitMQ, Azure Service Bus)
- **NFR29:** Switching backends requires only DAPR component YAML changes — zero code, zero recompilation
- **NFR32:** Deployable via Aspire publishers to Docker Compose, Kubernetes, Azure Container Apps without custom scripts

### Aspire Publisher Commands

```bash
# Docker Compose
PUBLISH_TARGET=docker EnableKeycloak=false dotnet run --project src/Hexalith.EventStore.AppHost/ -- publish -o ./publish-output/docker

# Kubernetes
PUBLISH_TARGET=k8s EnableKeycloak=false dotnet run --project src/Hexalith.EventStore.AppHost/ -- publish -o ./publish-output/k8s

# Azure Container Apps
PUBLISH_TARGET=aca EnableKeycloak=false dotnet run --project src/Hexalith.EventStore.AppHost/ -- publish -o ./publish-output/azure
```

**Note:** `EnableKeycloak=false` is required for K8s and ACA targets because Keycloak realm import uses bind mounts (not supported in production orchestrators). For production, configure Keycloak separately.

### Known Aspire Publisher Limitations

Aspire publishers generate application infrastructure but have known gaps with DAPR:

1. **Docker Compose**: DAPR sidecar containers are NOT auto-generated. Must be manually added alongside each service with matching `--app-id`, `--app-port`, and component mount paths.
2. **Kubernetes**: DAPR pod annotations (`dapr.io/enabled: "true"`, `dapr.io/app-id: "commandapi"`) are NOT auto-generated. Must be manually added to Deployment specs. DAPR components must be applied separately via `kubectl apply -f deploy/dapr/`.
3. **Azure Container Apps**: DAPR enablement in the Container Apps Environment must be configured manually. DAPR components are created as Azure resources, not file-based.

These are **expected** limitations — DAPR integration is handled by DAPR tooling, not Aspire publishers. The `deploy/README.md` should document these manual steps clearly.

### Security Posture Difference

| Aspect | Local Dev (AppHost/DaprComponents/) | Production (deploy/dapr/) |
|--------|-------------------------------------|---------------------------|
| Access Control | `defaultAction: allow` | `defaultAction: deny` + mTLS |
| Trust Domain | N/A | SPIFFE trust domain |
| Component Scoping | Implicit (single process) | Explicit `scopes: ["commandapi"]` |
| Secrets | appsettings.json / env vars | K8s Secrets / Azure Key Vault |
| TLS | None (local) | mTLS via DAPR (NFR9) |

### Backend Compatibility Matrix

| Backend | State Store | Pub/Sub | ETag Support | Actor Support |
|---------|-------------|---------|-------------|---------------|
| Redis | Yes (local dev) | Yes (local dev) | Yes | Yes |
| PostgreSQL | Yes (production) | N/A | Yes | Yes |
| Cosmos DB | Yes (scale) | N/A | Yes | Yes |
| RabbitMQ | N/A | Yes (production) | N/A | N/A |
| Kafka | N/A | Yes (scale) | N/A | N/A |
| Azure Service Bus | N/A | Yes (enterprise) | N/A | N/A |

### WARNING: Pre-Existing Test Failures

There are known pre-existing failures in Tier 2 and Tier 3 tests. These are NOT regressions from this story. Do NOT attempt to fix them.

### Coding Conventions (from .editorconfig)

- File-scoped namespaces: `namespace X.Y.Z;`
- Allman braces (new line before `{`)
- Private fields: `_camelCase`
- 4-space indentation, CRLF, UTF-8
- Nullable enabled, implicit usings enabled
- Warnings as errors (`TreatWarningsAsErrors = true`)

Do NOT:
- Restructure the deploy/ directory layout
- Change DAPR component names (state store names, pub/sub names)
- Modify local dev components in `AppHost/DaprComponents/`
- Modify the Aspire topology in ways that break the default local launch
- Add new production backends beyond what's already in `deploy/dapr/`
- Fix pre-existing test failures unrelated to this story

### Key Package Versions (from Directory.Packages.props)

| Package | Version |
|---------|---------|
| Aspire.AppHost.Sdk | 13.1.2 |
| Aspire.Hosting.Docker | (match Aspire version) |
| Aspire.Hosting.Kubernetes | (match Aspire version) |
| Aspire.Hosting.Azure.AppContainers | (match Aspire version) |
| CommunityToolkit.Aspire.Hosting.Dapr | 9.7.0 |
| Dapr.Client | 1.16.1 |
| .NET SDK | 10.0.103 |

### Project Structure Notes

```
deploy/
  dapr/                                    # Production DAPR components (9 YAMLs)
    accesscontrol.yaml                     # Deny-by-default + mTLS
    resiliency.yaml                        # Exponential backoff, circuit breakers
    statestore-postgresql.yaml             # Production state store
    statestore-cosmosdb.yaml               # Scale state store
    pubsub-rabbitmq.yaml                   # Production pub/sub
    pubsub-kafka.yaml                      # Scale pub/sub
    pubsub-servicebus.yaml                 # Enterprise pub/sub
    subscription-sample-counter.yaml       # Subscription template
    subscription-projection-changed.yaml   # Projection subscription
  README.md                                # Deployment guide (340+ lines)
src/Hexalith.EventStore.AppHost/
  Program.cs                               # Aspire topology with PUBLISH_TARGET
  DaprComponents/                          # Local dev DAPR components (6 YAMLs)
  KeycloakRealms/                          # Realm-as-code (D11)
  Hexalith.EventStore.AppHost.csproj       # Publisher packages installed
src/Hexalith.EventStore.Aspire/
  HexalithEventStoreExtensions.cs          # AddHexalithEventStore() extensions
src/Hexalith.EventStore.CommandApi/
  Dockerfile                               # Multi-stage Docker build
samples/Hexalith.EventStore.Sample/
  Dockerfile                               # Multi-stage Docker build
samples/deploy/azure/                      # Reference Bicep templates
.github/workflows/
  ci.yml                                   # Build + test on PR
  release.yml                              # Pack + publish NuGet on tag
  deploy-staging.yml                       # Docker build/push + kubectl restart
```

### Previous Story Intelligence (Story 8.5)

- Story 8.5 is a test pyramid validation/audit story — same pattern as this story
- Epic 8 stories are validation/audit stories, not greenfield
- Key learning: check what already exists before creating new code — most infrastructure is already built
- Pre-existing test failures exist and should not be fixed
- CI pipeline in `.github/workflows/ci.yml` runs all 3 test tiers

### Previous Story Intelligence (Story 8.4)

- Added Greeting domain service registration — multi-domain support confirmed
- Pattern: minimal changes to working code
- All Tier 1 tests: 670/670 passed
- `dotnet watch` not supported by Aspire 13.1.2

### Git Intelligence

Recent commits (2026-03-19):
- `bfe66e5` feat: Implement Story 8.4 — Greeting domain service registration, multi-domain hot reload
- `53903b7` feat: Complete Story 8.3 — NuGet client package, XML documentation
- `0f9b28f` feat: Implement multi-domain support with Greeting aggregate

All Epic 8 work has been validation/completion pattern — minimal changes to working code.

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 8, Story 8.6]
- [Source: _bmad-output/planning-artifacts/architecture.md — D1 storage, D4 access control, D7 invocation, NFR27-32, deploy/ directory]
- [Source: _bmad-output/planning-artifacts/prd.md — FR43 environment portability, FR44 Aspire publishers]
- [Source: _bmad-output/implementation-artifacts/8-5-three-tier-test-pyramid.md — Previous story, validation pattern]
- [Source: _bmad-output/implementation-artifacts/8-4-domain-service-hot-reload.md — Previous story, pre-existing failures]
- [Source: deploy/README.md — Existing deployment documentation]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs — PUBLISH_TARGET publisher support]
- [Source: .github/workflows/ — CI/CD pipeline configuration]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
