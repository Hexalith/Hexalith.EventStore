# CLAUDE.md — Hexalith.EventStore

## AI assistant instructions

Before working in this repository, read
[`hexalith-llm-instructions.md`](./references/Hexalith.AI.Tools/hexalith-llm-instructions.md)
(in the `references/Hexalith.AI.Tools` submodule) and follow it.

Before working on any module user interface or UX, also read
[`references/Hexalith.AI.Tools/hexalith-ux-instructions.md`](./references/Hexalith.AI.Tools/hexalith-ux-instructions.md)
and follow it.

## Project Overview

DAPR-native event sourcing server for .NET. Built on CQRS, DDD, and event sourcing patterns with .NET Aspire orchestration.

- **Repository:** https://github.com/Hexalith/Hexalith.EventStore
- **License:** MIT
- **Framework:** .NET 10 (SDK 10.0.300, pinned in `global.json`)

## Solution File

**Use `Hexalith.EventStore.slnx` only.** Never use `.sln` files — the project uses the modern XML solution format exclusively.

## Git Submodules

IMPORTANT! Only initialize and update submodules declared in the root repository `.gitmodules` file.

- Initialize root-declared submodules only, using the `references/...` paths declared in the root `.gitmodules` file.
- Do not initialize, update, or recurse into nested submodules inside those root-declared submodules.
- Avoid recursive submodule commands unless they are explicitly scoped so that nested submodules are not initialized.
- If nested submodules are initialized accidentally, deinitialize them before continuing.

## Aspire Runtime & Diagnostics

Use the Aspire CLI to run the local topology. Before code changes, run the AppHost with `aspire run` and inspect resource state so you are starting from a known state. AppHost changes require restarting the Aspire application.

```bash
aspire run
```

The standalone Aspire MCP server is obsolete — use the **Aspire agents** integration instead. Run `aspire agent init` once to install the Aspire skills/tools, then drive the running app through the Aspire CLI:

- `aspire describe` for resource state, endpoints, health, environment, and relationships.
- `aspire otel logs` for structured logs, console logs, and traces before changing code for runtime failures.
- `aspire start` / `aspire stop` to run the topology in the background; `aspire wait` to block until resources are healthy.
- Restart a misbehaving resource from the Aspire dashboard (or restart the app).
- Use the Playwright MCP server for functional investigations; get navigable endpoints from `aspire describe`.

When adding a resource to the app model, use `aspire add`, choose a version aligned with `Aspire.AppHost.Sdk`, and read the latest integration docs (`aspire docs search`) before editing the AppHost.

To update the AppHost, run `aspire update` (it updates the AppHost and the Aspire-specific packages in referenced projects; other packages may still need manual updates — `dotnet-outdated`, installed via `dotnet tool install --global dotnet-outdated-tool`, can help).

Avoid persistent containers early during development. Never install or use the obsolete Aspire workload. Prefer official documentation from `https://aspire.dev`, `https://learn.microsoft.com/dotnet/aspire`, and NuGet package pages.

## Cursor Cloud Run Notes

The VM environment provides .NET 10 SDK, Docker, Aspire CLI (`aspire`), Dapr CLI (`dapr`), and Dapr runtime (`daprd`, `placement`, `scheduler`) under `$HOME/.dapr/bin`.

```bash
sudo dockerd &>/tmp/dockerd.log &
sudo chmod 666 /var/run/docker.sock
$HOME/.dapr/bin/placement --port 50005 &
$HOME/.dapr/bin/scheduler --port 50006 --etcd-data-dir /tmp/dapr-scheduler-data &
EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
```

With `EnableKeycloak=false`, auth falls back to symmetric key JWT: tokens are validated against the symmetric key `DevOnlySigningKey-AtLeast32Chars!` (in `appsettings.Development.json`) and must include `iss=hexalith-dev`, `aud=hexalith-eventstore`, plus `tenants` (JSON array) and `permissions` (e.g. `["commands:*"]`) claims for authorization. The CommandAPI listens on `http://localhost:8080`; the Aspire dashboard is at `https://localhost:17017`.

Known VM gotchas:

- DAPR slim mode does not start placement/scheduler automatically; start them before `aspire run` or actors can fail with "did not find address for actor".
- DAPR access control policies in `DaprComponents/accesscontrol.yaml` enforce deny-by-default. In slim mode without mTLS, service-to-service invocations from eventstore to domain services may be rejected with 403.
- The HTTPS dev certificate cannot be fully trusted in the cloud VM. Use `http://localhost:8080` for API calls.

## Build & Test Commands

```bash
# Restore and build
dotnet restore Hexalith.EventStore.slnx
dotnet build Hexalith.EventStore.slnx --configuration Release

# Unit tests (run projects individually; do not use solution-level dotnet test)
dotnet test tests/Hexalith.EventStore.Contracts.Tests/
dotnet test tests/Hexalith.EventStore.Client.Tests/
dotnet test tests/Hexalith.EventStore.Sample.Tests/
dotnet test tests/Hexalith.EventStore.SignalR.Tests/
dotnet test tests/Hexalith.EventStore.Testing.Tests/

# Known pre-existing build failure
# tests/Hexalith.EventStore.Server.Tests does not build due to CA2007 warnings treated as errors.

# Integration tests (requires Docker and a running Aspire environment)
dotnet test tests/Hexalith.EventStore.IntegrationTests/
```

## Container Images

Container images are produced via the **.NET SDK container support** (no Dockerfiles). Opt-in is centralized in `Directory.Build.targets` and enabled per-project via `<EnableContainer>true</EnableContainer>` + `<ContainerRepository>image-name</ContainerRepository>`.

Defaults (see `Directory.Build.targets`): base `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`, registry `registry.hexalith.com`, user `app` (non-root), port 8080, OCI labels.

```bash
# Publish a single container image to a local tar archive (no registry push)
dotnet publish src/Hexalith.EventStore/Hexalith.EventStore.csproj \
  --configuration Release \
  -t:PublishContainer \
  -p:ContainerArchiveOutputPath=/tmp/eventstore.tar.gz

# Push directly to the registry (requires SDK_CONTAINER_REGISTRY_UNAME/PWORD env vars)
dotnet publish src/Hexalith.EventStore/Hexalith.EventStore.csproj \
  --configuration Release \
  -t:PublishContainer \
  -p:ContainerImageTags="staging-latest;staging-$(git rev-parse HEAD)"
```

Services currently containerized (6 images):

| Project | Image |
|---------|-------|
| `src/Hexalith.EventStore` | `registry.hexalith.com/eventstore` |
| `src/Hexalith.EventStore.Admin.Server.Host` | `registry.hexalith.com/eventstore-admin` |
| `src/Hexalith.EventStore.Admin.UI` | `registry.hexalith.com/eventstore-admin-ui` |
| `samples/Hexalith.EventStore.Sample` | `registry.hexalith.com/sample` |
| `samples/Hexalith.EventStore.Sample.BlazorUI` | `registry.hexalith.com/sample-blazor-ui` |
| `references/Hexalith.Tenants/src/Hexalith.Tenants` (submodule) | `registry.hexalith.com/tenants` |

## Project Structure

```
src/
  Hexalith.EventStore.Contracts      # Domain types: commands, events, results, identities
  Hexalith.EventStore.Client         # Client abstractions and DI registration
  Hexalith.EventStore.Server         # Server-side domain processors, DAPR integration
  Hexalith.EventStore.SignalR        # SignalR real-time notifications
  Hexalith.EventStore               # REST/gRPC API gateway, auth, validation
  Hexalith.EventStore.Aspire         # .NET Aspire hosting extensions
  Hexalith.EventStore.AppHost        # Aspire AppHost (DAPR topology orchestrator)
  Hexalith.EventStore.ServiceDefaults# Shared service config, OpenTelemetry
  Hexalith.EventStore.Testing        # Testing utilities and helpers

tests/
  Hexalith.EventStore.Contracts.Tests   # Tier 1
  Hexalith.EventStore.Client.Tests      # Tier 1
  Hexalith.EventStore.Sample.Tests      # Tier 1
  Hexalith.EventStore.Testing.Tests     # Tier 1
  Hexalith.EventStore.SignalR.Tests     # Tier 1
  Hexalith.EventStore.Server.Tests      # Tier 2
  Hexalith.EventStore.IntegrationTests  # Tier 3

samples/
  Hexalith.EventStore.Sample         # Counter domain example
```

## NuGet Packages (manifest-driven release set)

The release inventory is defined by `tools/release-packages.json` and currently contains 14 packages:

`Hexalith.EventStore.Contracts`, `Hexalith.EventStore.Client`, `Hexalith.EventStore.Server`, `Hexalith.EventStore.SignalR`, `Hexalith.EventStore.Testing`, `Hexalith.EventStore.Testing.Integration`, `Hexalith.EventStore.Aspire`, `Hexalith.EventStore.ServiceDefaults`, `Hexalith.EventStore.DomainService`, `Hexalith.EventStore.RestApi.Generators`, `Hexalith.EventStore.Gateway`, `Hexalith.EventStore.Admin.Abstractions`, `Hexalith.EventStore.Admin.Cli`, `Hexalith.EventStore.Admin.Server`.

`ServiceDefaults` and `DomainService` are the domain-service SDK packages (Epic A6): a domain-service host references `DomainService` for platform hosting (which pulls in `Client` + `ServiceDefaults` transitively) and writes its domain code plus a two-line host. A domain may also own a contracts-only library when those command/query contract identities must be shared by the domain service, an external generated API host, and UI metadata consumers. Both SDK packages are packable; the active publish list is governed by the release pipeline.

`RestApi.Generators` is a Roslyn source-generator/analyzer package. It is distributed under `analyzers/dotnet/cs` and is referenced as an analyzer by dedicated external API host projects that generate typed REST controllers from `ICommandContract` and `IQueryContract` messages.

`Gateway` is the reusable command/query HTTP gateway composition package for host applications. It is part of the manifest-governed release set; do not publish it or any other package outside `tools/release-packages.json`.

Versioning: semantic-release (Conventional Commits, automated on merge to main). Centralized package management via `Directory.Packages.props`.

## Architecture Patterns

- **CQRS:** Commands in, events out via MediatR pipeline
- **Event Sourcing:** State reconstructed by replaying events, no direct updates
- **DDD Aggregates:** Pure functions — `Handle(Command, State?) -> DomainResult` with `Apply(Event)` on state
- **Fluent Convention:** Reflection-based discovery of Handle/Apply methods (no manual registration)
- **DAPR:** State store, pub/sub, and config abstracted via DAPR sidecars
- **Multi-Tenancy:** Built-in at contract level (Domain + AggregateId + TenantId)
- **Aspire Orchestration:** Full local topology via `aspire run` on the AppHost

## Domain-Module Authoring (domain-centric)

Domain modules built on Hexalith.EventStore (e.g. the Counter sample, the `Hexalith.Tenants` submodule) **must be domain-centric**: they contain only aggregates, commands, events, projections, query handlers, validators, and contracts. All boilerplate required to run on the platform is supplied by the EventStore client libraries — the **domain-service SDK** (`src/Hexalith.EventStore.DomainService`). A new domain costs its domain code plus a ~2-line host.

**Rules:**

- A domain module **must not** ship its own `*.AppHost`, `*.Aspire`, or `*.ServiceDefaults` project, and **must not** re-implement a projection/query actor, DAPR wiring, telemetry sources, health checks, or event-subscription plumbing. If a capability is missing, add it to the platform (SDK / Client / Aspire / ServiceDefaults), not the domain.
- **Host shape** — `Program.cs` is two lines: `builder.AddEventStoreDomainService();` then `app.UseEventStoreDomainService();`. The SDK provides convention discovery/registration and the canonical DAPR endpoints `/process`, `/replay-state`, `/query`, `/project`, and `/admin/operational-index-metadata`.
- **Queries** — implement `IDomainQueryHandler` (one per query type); the SDK discovers, registers, and routes them. Do not subclass a projection actor.
- **Projections (full-replay, Model a)** — implement `IDomainProjectionHandler`; the SDK maps `/project` and dispatches to it (a domain that needs bespoke `/project` wire behavior may map its own route and the SDK yields).
- **Persisted read models** — use `IReadModelStore` + `ReadModelWritePolicy` (Client); **pagination cursors** — use `IQueryCursorCodec` / `QueryCursorScope` (Client). Do not hand-roll a state store or cursor codec.
- **Aspire** — the EventStore AppHost adds the module via `project.AddEventStoreDomainModule(eventStoreResources, appId, …)`; domains don't orchestrate themselves.
- **Telemetry / health** — use `AddEventStoreDomainTelemetry(domain)` and `AddEventStoreDomainStateStoreHealthCheck(domain)`; do not declare per-domain `ActivitySource`/`Meter`/health-check classes.
- **Generated REST APIs** — REST controllers are generated from `ICommandContract`/`IQueryContract` messages into dedicated external-facing API hosts. Interactive UI hosts consume EventStore Client libraries and must not host generated or hand-written per-message MVC command/query controllers. A domain-owned contracts library is allowed when it is used to share contract identities between the domain service, external API host, and UI metadata consumers; it must contain contracts only and must not reintroduce hosting, DAPR, telemetry, state-store, query/projection actor, or UI concerns.
- **Generated REST scope** — referenced contracts exposed through `RestApi.Generators` must declare the intended API scope with `RestRouteAttribute.ApiScope`, matching the consuming host's `RestApiAttribute.Tag`. Do not rely on broad referenced-assembly discovery to decide what an external API host publishes.

`samples/Hexalith.EventStore.Sample` is the reference domain module. See `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-02.md` for the rationale.

## Code Style & Conventions

Defined in `.editorconfig`:

- **Namespaces:** File-scoped (`namespace X.Y.Z;`)
- **Braces:** Allman style (new line before opening brace)
- **Private fields:** `_camelCase` prefix
- **Interfaces:** `I` prefix
- **Async methods:** `Async` suffix
- **Indentation:** 4 spaces, CRLF line endings, UTF-8
- **Nullable:** Enabled globally
- **Implicit usings:** Enabled globally
- **Warnings as errors:** Enabled (`TreatWarningsAsErrors = true`)

## Test Conventions

- **Framework:** xUnit v3
- **Assertions:** Shouldly 4.3.0 (fluent)
- **Mocking:** NSubstitute 5.3.0
- **Coverage:** coverlet.collector 10.0.1
- All existing and new tests must pass before a story is complete
- Run test projects individually; use `Hexalith.EventStore.slnx` for restore/build only
- Unit tests run in CI on every PR; integration tests require Docker and a running Aspire environment
- `Hexalith.EventStore.Server.Tests` is currently excluded from the baseline because of the pre-existing CA2007 build failure

## Code Review Process

Senior code review (typically GitHub Copilot GPT-5.4 or equivalent) is a mandatory pipeline stage for every story, not a rubber-stamp formality. The review-driven-patch rate across Epic 2 was 5/5 stories: every story had at least one HIGH or MEDIUM reviewer finding that produced a real patch.

**Implications:**

- Story specs should budget for review-found rework as the norm.
- "Verification" stories (audit existing code) typically uncover one design or test gap per story — plan accordingly.
- Reviewer-found patches are applied and validated before the story closes. If the reviewer surfaces a CRITICAL finding, verify before accepting (false-positive CRITICALs are expensive — see Epic 1 retro R1-A8 for the verification-command rule).

**Integration test rule** (Epic 2 retro R2-A6):

Tier 2 and Tier 3 integration tests MUST inspect state-store end-state (e.g., Redis key contents, persisted CloudEvent body), not only API return codes or mock call counts. "Asserts the call returned 202" is an API smoke test, not an integration test.

**ID validation rule** (Epic 2 retro R2-A7):

Controllers and validators that handle `messageId`, `correlationId`, `aggregateId`, or `causationId` MUST use `Ulid.TryParse` (or accept any non-whitespace string per `AggregateIdentity` rules). `Guid.TryParse` on these fields is **forbidden** — the system's identifiers are ULIDs. ULID and GUID share a 36-char shape only by coincidence.

## Commit Messages

All commit messages **must** follow the [Conventional Commits](https://www.conventionalcommits.org/) specification. This is required for semantic-release to determine version bumps and generate changelogs.

Format: `<type>(<optional scope>): <description>`

- `feat:` — New feature (triggers **minor** version bump)
- `fix:` — Bug fix (triggers **patch** version bump)
- `docs:` — Documentation only
- `refactor:` — Code change that neither fixes a bug nor adds a feature
- `test:` — Adding or updating tests
- `chore:` — Build process, CI, or tooling changes
- `perf:` — Performance improvement

For breaking changes, add `BREAKING CHANGE:` in the commit body or append `!` after the type (e.g., `feat!:`). This triggers a **major** version bump.

Examples:
```
feat(contracts): add SnapshotInterval to EventStoreOptions
fix(server): prevent duplicate event sequence numbers on concurrent writes
docs: update quickstart with DAPR init prerequisites
refactor(client): extract retry policy into shared helper
feat!: rename EventEnvelope.StreamId to AggregateId
```

## Branch Naming

- `feat/<description>` — Features and enhancements
- `fix/<description>` — Bug fixes
- `docs/<description>` — Documentation changes

## CI/CD

- **CI:** GitHub Actions on push/PR to main — restore, build (Release), configured unit/integration test suites, optional Aspire end-to-end tests
- **Release:** Triggered on merge to main via semantic-release — determines version from Conventional Commits, tests, packs and publishes the manifest-driven NuGet package set from `tools/release-packages.json`, creates GitHub Release, updates CHANGELOG.md

## Key Dependencies

- DAPR SDK (Client, AspNetCore, Actors) — pinned in [`Directory.Packages.props`](Directory.Packages.props)
- .NET Aspire 13.3.x
- MediatR 14.1.0
- FluentValidation 12.1.1
- OpenTelemetry 1.15.x
