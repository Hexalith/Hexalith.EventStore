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

# Tier 2 — Integration tests (requires DAPR slim init)
dapr init --slim
dotnet test tests/Hexalith.EventStore.Server.Tests/

# Tier 3 — Aspire end-to-end contract tests (requires full DAPR init + Docker)
dapr init
dotnet test tests/Hexalith.EventStore.IntegrationTests/
```

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

## Branch Naming

- `feat/<description>` — Features and enhancements
- `fix/<description>` — Bug fixes
- `docs/<description>` — Documentation changes

## CI/CD

- **CI:** GitHub Actions on push/PR to main — restore, build (Release), Tier 1+2 tests, optional Tier 3
- **Release:** Triggered on merge to main via semantic-release — determines version from Conventional Commits, tests, pack, publish 6 NuGet packages, creates GitHub Release, updates CHANGELOG.md

## Key Dependencies

- DAPR SDK 1.17.0 (Client, AspNetCore, Actors)
- .NET Aspire 13.1.x
- MediatR 14.0.0
- FluentValidation 12.1.1
- OpenTelemetry 1.15.x
