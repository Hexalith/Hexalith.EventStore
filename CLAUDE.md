# CLAUDE.md — Hexalith.EventStore

## Project Overview

DAPR-native event sourcing server for .NET. Built on CQRS, DDD, and event sourcing patterns with .NET Aspire orchestration.

- **Repository:** https://github.com/Hexalith/Hexalith.EventStore
- **License:** MIT
- **Framework:** .NET 10 (SDK 10.0.103, pinned in `global.json`)

## Solution File

**Use `Hexalith.EventStore.slnx` only.** Never use `.sln` files — the project uses the modern XML solution format exclusively.

## Build & Test Commands

```bash
# Restore and build
dotnet restore Hexalith.EventStore.slnx
dotnet build Hexalith.EventStore.slnx --configuration Release

# Tier 1 — Unit tests (no external dependencies)
dotnet test tests/Hexalith.EventStore.Contracts.Tests/
dotnet test tests/Hexalith.EventStore.Client.Tests/
dotnet test tests/Hexalith.EventStore.Sample.Tests/
dotnet test tests/Hexalith.EventStore.Testing.Tests/
dotnet test tests/Hexalith.EventStore.SignalR.Tests/

# Tier 2 — Integration tests (requires full DAPR init + Docker)
dapr init
dotnet test tests/Hexalith.EventStore.Server.Tests/

# Tier 3 — Aspire end-to-end contract tests (requires full DAPR init + Docker)
dapr init
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
| `Hexalith.Tenants/src/Hexalith.Tenants` (submodule) | `registry.hexalith.com/tenants` |

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

## NuGet Packages (6 published)

Hexalith.EventStore.Contracts, Client, Server, SignalR, Testing, Aspire

Versioning: semantic-release (Conventional Commits, automated on merge to main). Centralized package management via `Directory.Packages.props`.

## Architecture Patterns

- **CQRS:** Commands in, events out via MediatR pipeline
- **Event Sourcing:** State reconstructed by replaying events, no direct updates
- **DDD Aggregates:** Pure functions — `Handle(Command, State?) -> DomainResult` with `Apply(Event)` on state
- **Fluent Convention:** Reflection-based discovery of Handle/Apply methods (no manual registration)
- **DAPR:** State store, pub/sub, and config abstracted via DAPR sidecars
- **Multi-Tenancy:** Built-in at contract level (Domain + AggregateId + TenantId)
- **Aspire Orchestration:** Full local topology via `dotnet run` on AppHost

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

- **Framework:** xUnit 2.9.3
- **Assertions:** Shouldly 4.3.0 (fluent)
- **Mocking:** NSubstitute 5.3.0
- **Coverage:** coverlet.collector 6.0.4
- All existing and new tests must pass before a story is complete
- Tier 1 tests run in CI on every PR; Tier 2 after DAPR slim init; Tier 3 optional

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

- **CI:** GitHub Actions on push/PR to main — restore, build (Release), Tier 1+2 tests, optional Tier 3
- **Release:** Triggered on merge to main via semantic-release — determines version from Conventional Commits, tests, pack, publish 6 NuGet packages, creates GitHub Release, updates CHANGELOG.md

## Key Dependencies

- DAPR SDK 1.16.1 (Client, AspNetCore, Actors)
- .NET Aspire 13.1.x
- MediatR 14.0.0
- FluentValidation 12.1.1
- OpenTelemetry 1.15.x
