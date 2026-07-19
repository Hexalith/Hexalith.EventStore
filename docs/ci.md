# CI/CD Pipeline

This page documents the EventStore-specific GitHub Actions wiring. Shared
Hexalith CI/CD standards and reusable workflow guidance live in
[`references/Hexalith.Builds/.github/workflows/ci-cd-standards.md`](../references/Hexalith.Builds/.github/workflows/ci-cd-standards.md).

## Workflows

| Workflow | File | Triggers | Purpose |
|----------|------|----------|---------|
| **CI** | `.github/workflows/ci.yml` | `push` and `pull_request` to `main` | Thin caller to `Hexalith.Builds` `domain-ci.yml@main`. Restores/builds `Hexalith.EventStore.slnx`, runs package consumer validation, and runs deterministic test projects including unfiltered `Server.Tests`. |
| **Advisory Tests** | `.github/workflows/advisory-tests.yml` | `push`, `pull_request` to `main`, manual dispatch | Visible non-release-blocking browser/governance/evidence scaffolding suites. It installs Chromium before Playwright E2E tests, runs with `continue-on-error`, and release does not listen to this workflow. |
| **Integration Tests** | `.github/workflows/integration.yml` | `push`, `pull_request` to `main`, manual dispatch | Dedicated DAPR lane for `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`. It is intentionally separate from the release trigger. |
| **CodeQL** | `.github/workflows/codeql.yml` | `push`, `pull_request` to `main`, weekly schedule | Thin caller to the shared CodeQL reusable workflow using `@main`. |
| **Dependency Review** | `.github/workflows/dependency-review.yml` | `pull_request` to `main` | Thin caller to the shared dependency-review gate using `@main`. |
| **Commitlint** | `.github/workflows/commitlint.yml` | `push` and `pull_request` to `main` | Thin caller to the shared Conventional Commits gate using `@main`. |
| **Release** | `.github/workflows/release.yml` | successful `CI` workflow completion for a `push` to `main` | Thin caller to an exact immutable `Hexalith.Builds` `domain-release.yml` commit for semantic-release, NuGet publish, GitHub Release, and the approved EventStore container publish. |

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
- The advisory test project list and Playwright browser install needed by the
  Admin UI E2E suite.
- Manifest-backed package validation scripts under `scripts/`.
- The separate live-sidecar workflow while shared CI has no advisory filtered
  project lane.
- The approved release container mapping:
  `src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore`.

Hexalith.Builds action and reusable workflow references generally use `@main`
by Hexalith policy. The publication-capable release workflow is the explicit
exception: it pins one exact Builds commit so the caller and nested publisher
cannot resolve independently. Third-party action pinning is enforced by shared
workflows.

## Test Lanes

| Lane | Projects | Workflow behavior |
|------|----------|-------------------|
| Deterministic release gate | Contracts, Client, Testing, SignalR, Admin, AppHost, DomainService, QueryRouting, Sample, Testing.Integration, RestApi.Generators, and `tests/Hexalith.EventStore.Server.Tests` | Blocking in shared `domain-ci.yml@main` through `unit-test-projects`. `Server.Tests` runs unfiltered because live-sidecar tests moved out. |
| Live-sidecar DAPR lane | `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` | Dedicated `Integration Tests` workflow after `dapr init`. This lane is visible but not part of the semantic-release gate. |
| Advisory browser/governance/evidence scaffolds | `tests/Hexalith.EventStore.Admin.UI.E2E`, `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests`, `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests` | Separate `Advisory Tests` workflow. It installs Playwright Chromium for the browser suite and runs with `continue-on-error`, preserving push/PR signal without making semantic-release depend on these suites. |
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
an exact 40-character `Hexalith/Hexalith.Builds` commit embedded in `release.yml`.

Semantic-release still decides from commit history whether a release is
warranted. NuGet publishing remains scoped to the 14 packages listed in
[`tools/release-packages.json`](../tools/release-packages.json). Container
publishing is enabled only for the approved EventStore host mapping. Before any
NuGet package is pushed, semantic-release validates `NUGET_API_KEY`, the
container publisher helper, and the required Zot registry credentials so a
missing container secret cannot create a partial NuGet-only release. The
semantic-release `verifyRelease` phase then fetches a separate durable GitHub
issue-comment authority record, verifies its author against the checked-in
release-owner allowlist, freezes its exact bytes, and proves the new version is
absent for all 14 NuGet IDs and the container tag before Git-tag creation. The `publish` phase
requires exact equality with the frozen authority and repeats destination
absence immediately before NuGet. The shared publisher repeats the authority,
expiry, and multi-media-type container-tag absence check immediately before the
SDK registry write. Existing versions are collisions: the release path does not
use `--skip-duplicate` and never overwrites an existing package, tag, manifest,
or registry object.

The `main` branch accepts changes only through pull requests. Release automation
therefore does not use `@semantic-release/changelog` or `@semantic-release/git`:
it tags the already CI-approved source commit and publishes generated notes and
package assets through the GitHub release without creating or pushing a release
commit to `main`. Any tracked `CHANGELOG.md` update must arrive through its own
reviewed pull request; GitHub Releases are the current machine-generated release
record.

The authority record binds the EventStore repository, proposed version,
workflow source SHA, `registry.hexalith.com/eventstore`, exact platform set,
named owner, authorization and expiry times, rationale, durable source, and one
maintainer-approved Hexalith.Builds execution SHA. The reusable workflow checks
its resolved workflow SHA, checks out the nested action at that exact commit,
and invokes it locally. The action then verifies its own action and helper bytes
against the same commit before semantic-release can run. The repository
embedded Builds SHA and repository variable `HEXALITH_RELEASE_AUTHORITY_URL`
provide those non-secret inputs. The reusable-workflow reference and
`builds-execution-sha` input must contain the same literal SHA. This gate
validates authority evidence; it does not create human publication authority.
The `GITHUB_TOKEN` is attached only to the exact GitHub API origin; cross-origin
redirects drop authorization and HTTPS downgrade redirects fail closed.

The `publishCmd` calls the helper installed by the shared `publish-containers`
action only after the authority gate and NuGet publication:

```text
src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore
```

Do not add sample, admin, or UI container mappings without an explicit release
owner decision.

### Exact container contract and evidence

The shared publisher uses .NET SDK container support in Release/package mode
with `linux-musl-x64;linux-musl-arm64` supplied through both
`RuntimeIdentifiers` and `ContainerRuntimeIdentifiers`. The external contract
is exactly `linux/amd64` plus `linux/arm64`. The version tag must resolve to an
OCI index with media type `application/vnd.oci.image.index.v1+json`; duplicate,
missing, extra, variant, blank, or `unknown/unknown` descriptors fail closed.

Post-publish validation reads the tag with an explicit OCI `Accept` header,
captures `Docker-Content-Digest`, rereads the object by immutable digest, and
requires byte-for-byte equality and a matching SHA-256. Each child manifest and
config is then resolved by digest. Manifest descriptor and response media types,
all descriptor byte sizes and raw hashes, config descriptor media types, and
config `os`/`architecture` must all agree. Exact raw child-manifest and config
bytes are retained beside the raw parent index with independent hashes.

Both immutable child references (`repository@sha256:...`) are explicitly pulled
with bounded timeouts and run the same bounded
smoke: loopback ephemeral host port, `ASPNETCORE_URLS=http://+:8080`, a fixed
non-secret JWT issuer/audience/key used only by the ephemeral smoke container,
and `/alive`. The Production-mode symmetric-key override is explicit, and the
common 180-second bound accommodates emulated arm64 startup without becoming
unbounded.
Arm64 emulation is prepared by a SHA-pinned shared action and checked before the
product smoke. Outcomes remain diagnostically distinct:

- `environment/emulation-setup-failure` — the runner cannot execute arm64;
- `registry-pull-failure` — an immutable child cannot be pulled;
- `image-start-failure` — the child image does not start;
- `liveness-timeout` — the process starts but `/alive` never passes in time;
- `cleanup-failure` — a passing child cannot be safely removed;
- `pass` — the child returns a successful `/alive` response.

Only an exact 2xx `/alive` response passes; redirects are not followed. Exited
containers are inspected before removal and bounded support-safe diagnostic
hashes/excerpts preserve the earliest failure. Only two `pass` results complete
container publication. Evidence records the
source SHA separately from the later semantic-release tag commit, workflow run
and approved Builds identity, repository/version, index digest and raw hash,
child manifest/config identities, exact platforms, authority bytes/source/hash
and checked-at time, and both smoke logs/hashes. Registry, authentication,
emulation, product, or evidence failure leaves the release non-authorizing.
The reusable workflow uploads the complete hidden evidence directory with
`always()` so partial publication remains visible.

Story 3.12 may hand a corrective release to Story 1.20 only as observed
candidate evidence. Story 1.20 independently revalidates the full package and
container identity and retains sole authority over its approval fields and
consumer-migration decision.

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
