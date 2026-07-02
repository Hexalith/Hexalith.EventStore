# CI/CD Pipeline

This page documents the EventStore-specific GitHub Actions wiring. Shared
Hexalith CI/CD standards and reusable workflow guidance live in
[`references/Hexalith.Builds/.github/workflows/ci-cd-standards.md`](../references/Hexalith.Builds/.github/workflows/ci-cd-standards.md).

## Workflows

| Workflow | File | Triggers | Purpose |
|----------|------|----------|---------|
| **CI** | `.github/workflows/ci.yml` | `push` and `pull_request` to `main` | Restore/build `Hexalith.EventStore.slnx`, run deterministic fast tests, run `Server.Tests` excluding `LiveSidecar`, upload TRX/coverage evidence. |
| **Integration Tests** | `.github/workflows/integration.yml` | `push`, `pull_request` to `main`, manual dispatch | Run the `Category=LiveSidecar` DAPR-backed server integration tests in a dedicated lane. |
| **Release** | `.github/workflows/release.yml` | successful `CI` workflow completion for a `push` to `main` | Run `semantic-release` for versioning, changelog, NuGet publish, and GitHub Release creation. |

## Shared CI/CD Boundary

Reusable CI/CD logic belongs in `Hexalith.Builds`:

- Composite actions such as `Github/initialize-dotnet` and `Github/dapr-init`.
- Reusable workflow templates and shared standards.
- Action pinning policy, submodule initialization policy, artifact conventions, and release-gate guidance.

EventStore keeps only module-specific wiring here:

- `Hexalith.EventStore.slnx`.
- EventStore test project manifests and tier exclusions.
- EventStore release/package behavior in `.releaserc.json`.
- EventStore-specific integration constraints, such as the deferred full Aspire topology lane.

## Test Tiers

| Tier | Projects | Workflow behavior |
|------|----------|-------------------|
| Fast deterministic tests | Contracts, Client, Testing, SignalR, Admin, AppHost, DomainService, QueryRouting, Sample, Testing.Integration, and RestApi.Generators tests | Blocking in `ci / build-and-test`. |
| Server in-process tests | `tests/Hexalith.EventStore.Server.Tests` with `Category!=LiveSidecar` | Blocking in `server-inprocess-tests`. |
| Live-sidecar tests | `tests/Hexalith.EventStore.Server.Tests` with `Category=LiveSidecar` | Dedicated `Integration Tests` workflow. Not part of release. |
| Browser E2E | `tests/Hexalith.EventStore.Admin.UI.E2E` | Not currently automated in the three-workflow baseline; the CI manifest marks it as known non-blocking so it cannot be forgotten silently. |
| ATDD validator scaffolds | `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests`, `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests` | Known non-blocking until their expected entrypoint files and generated artifacts exist. |
| Full Aspire E2E | `tests/Hexalith.EventStore.IntegrationTests` | Deferred until a reliable Aspire-in-CI topology exists. |

`ci.yml` contains a manifest check that compares discovered test projects under
`tests/` against the blocking and known non-blocking lists. Adding a new test
project requires assigning it to a tier.

## Release Flow

The release workflow no longer repeats the CI gate. It starts only after the
`CI` workflow completes successfully for a push to `main`, then checks out the
exact CI head SHA, attaches it to a local `main` branch, and runs
`npx semantic-release`.

Semantic-release still owns versioned artifact production through
[`.releaserc.json`](../.releaserc.json): package packing and NuGet publishing
run only when the commit history warrants a release.

## Submodules

The workflows initialize only `references/Hexalith.Builds`, because Release
builds use published Hexalith package references via
`UseHexalithProjectReferences=false`. They do not use recursive submodule
checkout or recursive submodule update.

## Caching And Artifacts

NuGet caching hashes dependency-defining inputs:

- `global.json`
- `nuget.config`
- `Directory.Packages.props`
- `references/Hexalith.Builds/Props/Directory.Packages.props`
- project files

Blocking test jobs upload TRX and Cobertura coverage artifacts with
`if: always()` and short retention.

## Local CI Mirror

Use the solution only for restore/build:

```bash
dotnet restore Hexalith.EventStore.slnx
dotnet build Hexalith.EventStore.slnx --configuration Release
```

Run test projects individually, matching the workflow lists. Do not use
solution-level `dotnet test`.

## Related

- [Hexalith.Builds CI/CD standards](../references/Hexalith.Builds/.github/workflows/ci-cd-standards.md)
- [`ci-secrets-checklist.md`](ci-secrets-checklist.md)
- [`.releaserc.json`](../.releaserc.json)
- [`commitlint.config.mjs`](../commitlint.config.mjs)
