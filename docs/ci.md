# CI/CD Pipeline

This page documents the EventStore-specific GitHub Actions wiring. Shared
Hexalith CI/CD standards and reusable workflow guidance live in
[`references/Hexalith.Builds/.github/workflows/ci-cd-standards.md`](../references/Hexalith.Builds/.github/workflows/ci-cd-standards.md).

## Workflows

| Workflow | File | Triggers | Purpose |
|----------|------|----------|---------|
| **CI** | `.github/workflows/ci.yml` | `push` and `pull_request` to `main` | Thin caller to `Hexalith.Builds` `domain-ci.yml@main`. Restores/builds `Hexalith.EventStore.slnx`, runs package consumer validation, and runs deterministic test projects including unfiltered `Server.Tests`. |
| **Advisory Tests** | `.github/workflows/advisory-tests.yml` | `push`, `pull_request` to `main`, manual dispatch | Thin caller to `domain-ci.yml@main` for visible non-release-blocking browser/governance/evidence scaffolding suites. Release does not listen to this workflow. |
| **Integration Tests** | `.github/workflows/integration.yml` | `push`, `pull_request` to `main`, manual dispatch | Dedicated DAPR lane for `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`. It is intentionally separate from the release trigger. |
| **CodeQL** | `.github/workflows/codeql.yml` | `push`, `pull_request` to `main`, weekly schedule | Thin caller to the shared CodeQL reusable workflow using `@main`. |
| **Dependency Review** | `.github/workflows/dependency-review.yml` | `pull_request` to `main` | Thin caller to the shared dependency-review gate using `@main`. |
| **Commitlint** | `.github/workflows/commitlint.yml` | `push` and `pull_request` to `main` | Thin caller to the shared Conventional Commits gate using `@main`. |
| **Release** | `.github/workflows/release.yml` | successful `CI` workflow completion for a `push` to `main` | Thin caller to `Hexalith.Builds` `domain-release.yml@main` for semantic-release, NuGet publish, GitHub Release, and the approved EventStore container publish. |

## Shared CI/CD Boundary

Reusable CI/CD logic belongs in `Hexalith.Builds`:

- Reusable workflows such as `domain-ci.yml`, `domain-release.yml`, CodeQL,
  dependency review, and commitlint.
- Composite actions such as `Github/initialize-build`, `Github/initialize-dotnet`,
  `Github/dapr-init`, and container publishing.
- Action pinning policy, submodule initialization policy, artifact conventions,
  and release-gate guidance.

EventStore keeps only module-specific wiring here:

- `Hexalith.EventStore.slnx`.
- The deterministic test project list passed to `domain-ci.yml@main`.
- The advisory test project list passed to a separate `domain-ci.yml@main`
  caller that release does not consume.
- Manifest-backed package validation scripts under `scripts/`.
- The separate live-sidecar workflow while shared CI has no advisory filtered
  project lane.
- The approved release container mapping:
  `src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore`.

Hexalith.Builds action and reusable workflow references use `@main` by Hexalith
policy. Third-party action pinning is enforced by shared workflows.

## Test Lanes

| Lane | Projects | Workflow behavior |
|------|----------|-------------------|
| Deterministic release gate | Contracts, Client, Testing, SignalR, Admin, AppHost, DomainService, QueryRouting, Sample, Testing.Integration, RestApi.Generators, and `tests/Hexalith.EventStore.Server.Tests` | Blocking in shared `domain-ci.yml@main` through `unit-test-projects`. `Server.Tests` runs unfiltered because live-sidecar tests moved out. |
| Live-sidecar DAPR lane | `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` | Dedicated `Integration Tests` workflow after `dapr init`. This lane is visible but not part of the semantic-release gate. |
| Advisory browser/governance/evidence scaffolds | `tests/Hexalith.EventStore.Admin.UI.E2E`, `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests`, `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests` | Separate `Advisory Tests` workflow. It preserves push/PR signal but is not consumed by semantic-release. |
| Full Aspire E2E | `tests/Hexalith.EventStore.IntegrationTests` | Deferred until a reliable Aspire-in-CI topology exists. |

Do not reintroduce a `Category!=LiveSidecar` filter to make `Server.Tests`
deterministic. Live-sidecar coverage belongs in the live-sidecar project and
workflow so the deterministic release gate can remain unfiltered.

## Package Validation

Shared `domain-ci.yml@main` calls these EventStore entry points when
`run-consumer-validation: true`:

```bash
python3 scripts/pack-release-packages.py ./nupkgs 0.0.0-ci-test
python3 scripts/validate-nuget-packages.py ./nupkgs
python3 scripts/validate-consumer-package-references.py ./nupkgs
```

`tools/release-packages.json` remains the authoritative package inventory. The
`scripts/` entry points are compatibility wrappers/checks for the shared workflow:

- `scripts/pack-release-packages.py` delegates to the existing manifest packer
  under `tools/`. When shared CI passes `0.0.0-ci-test`, this wrapper packs
  `999.0.0-ci-test` instead so synthetic validation packages still satisfy
  current package dependency floors.
- `scripts/validate-nuget-packages.py` validates that the `.nupkg` directory
  contains exactly the manifest-listed EventStore packages at one version.
- `scripts/validate-consumer-package-references.py` creates a temporary
  package-only consumer, restores from the local package directory, builds it,
  and rejects project-reference resolution.

The release prepare step in [`.releaserc.json`](../.releaserc.json) still uses
the `tools/` scripts so semantic-release remains manifest-driven:

```bash
python3 tools/pack-release-packages.py ./nupkgs <version>
python3 tools/validate-release-packages.py ./nupkgs <version>
```

## Release Flow

The release workflow starts only after the `CI` workflow completes successfully
for a push to `main`. The job also requires `github.sha` to equal the completed
CI workflow's `head_sha`, so a queued release does not publish a newer `main`
tip than the commit whose CI passed. It delegates to
`Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@main`.

Semantic-release still decides from commit history whether a release is
warranted. NuGet publishing remains scoped to the 14 packages listed in
[`tools/release-packages.json`](../tools/release-packages.json). Container
publishing is enabled only for the approved EventStore host mapping, and the
semantic-release `publishCmd` calls the helper installed by the shared
`publish-containers` action:

```text
src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore
```

Do not add sample, admin, or UI container mappings without an explicit release
owner decision.

## Submodules

Shared workflows initialize root-declared submodules through Hexalith.Builds
setup. EventStore workflow code must not use recursive submodule checkout or
recursive submodule update.

Release and consumer validation run in package-reference mode. `Debug` source
references are a local-development convenience and must not leak into package
publication.

## Supply-Chain Backlog

Current shared workflow migration keeps the immediate policy surface consistent
with other Hexalith modules. Remaining hardening work stays explicit:

- NuGet publishing still uses `NUGET_API_KEY`; Trusted Publishing is a follow-up.
- SBOM, artifact attestations, package signing, and provenance evidence remain
  shared Hexalith.Builds backlog items unless a story assigns them to EventStore.
- Shared workflows own third-party action pinning and npm signature checks; this
  repository should not duplicate that policy in local workflow steps.
- Do not enable `run-coverage-gate` in EventStore CI until the expected
  `scripts/validate-coverage.py` contract exists here.

## Local CI Mirror

Use the solution only for restore/build:

```bash
dotnet restore Hexalith.EventStore.slnx
dotnet build Hexalith.EventStore.slnx --configuration Release
```

Run test projects individually, matching the workflow lists. Do not use
solution-level `dotnet test`.

For package validation, run the same shared-CI entry points locally:

```bash
python3 scripts/pack-release-packages.py /tmp/hexalith-eventstore-ci-packages 0.0.0-ci-test
python3 scripts/validate-nuget-packages.py /tmp/hexalith-eventstore-ci-packages
python3 scripts/validate-consumer-package-references.py /tmp/hexalith-eventstore-ci-packages
```

## Related

- [Hexalith.Builds CI/CD standards](../references/Hexalith.Builds/.github/workflows/ci-cd-standards.md)
- [`ci-secrets-checklist.md`](ci-secrets-checklist.md)
- [`.releaserc.json`](../.releaserc.json)
- [`commitlint.config.mjs`](../commitlint.config.mjs)
