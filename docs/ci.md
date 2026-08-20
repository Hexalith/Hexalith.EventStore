# CI/CD Pipeline

This page documents the EventStore-specific GitHub Actions wiring. Shared
Hexalith CI/CD standards and reusable workflow guidance live in
[`references/Hexalith.Builds/.github/workflows/ci-cd-standards.md`](../references/Hexalith.Builds/.github/workflows/ci-cd-standards.md).

## Workflows

| Workflow | File | Triggers | Purpose |
|----------|------|----------|---------|
| **CI** | `.github/workflows/ci.yml` | `push` and `pull_request` to `main` | Runs three blocking jobs: shared `domain-ci.yml@main` for the solution and deterministic tests, the EventStore-owned semantic-release governance fixture from a clean Node 22 lockfile install, and the Tenants source-mode topology guardrails. |
| **Advisory Tests** | `.github/workflows/advisory-tests.yml` | `push`, `pull_request` to `main`, manual dispatch | Visible non-release-blocking browser/governance/evidence scaffolding suites. It installs Chromium before Playwright E2E tests, runs with `continue-on-error`, and release does not listen to this workflow. |
| **Integration Tests** | `.github/workflows/integration.yml` | `push`, `pull_request` to `main`, manual dispatch | Dedicated DAPR lane for `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`. It is intentionally separate from the release trigger. |
| **CodeQL** | `.github/workflows/codeql.yml` | `push`, `pull_request` to `main`, weekly schedule | Thin caller to the shared CodeQL reusable workflow using `@main`. |
| **Dependency Review** | `.github/workflows/dependency-review.yml` | `pull_request` to `main` | Thin caller to the shared dependency-review gate using `@main`. |
| **Commitlint** | `.github/workflows/commitlint.yml` | `push` and `pull_request` to `main` | Thin caller to the shared Conventional Commits gate using `@main`. |
| **Release** | `.github/workflows/release.yml` | manual dispatch from the current green `main` tip | Exact-source preflight followed by a protected `production` environment and an immutable `Hexalith.Builds` release workflow for semantic-release, NuGet, GitHub Release, and the approved EventStore container. |

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
- The semantic-release GitHub lifecycle fixture and its blocking Node 22 CI
  dependency lane.
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
| Semantic-release governance | `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs` | Blocking EventStore-owned CI job. It provisions Node 22, installs exactly `package-lock.json` with `npm ci`, and exercises both release-history cases against an Undici-guarded loopback-only fake GitHub boundary. |
| Live-sidecar DAPR lane | `tests/Hexalith.EventStore.Server.LiveSidecar.Tests` | Dedicated `Integration Tests` workflow after `dapr init`. This lane is visible but not part of the semantic-release gate. After the live suite, Integration Tests includes Story 4.14 OQ8 evidence capture: `dotnet build` of `Server.Tests`, pinned `-method` support oracles, and capture-aware validation with `--support-ctrf`. It does not rerun committed historical-evidence closure, and it is not a full `dotnet test` of `Server.Tests` as the live lane. |
| Advisory browser/governance/evidence scaffolds | `tests/Hexalith.EventStore.Admin.UI.E2E`, `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests`, `tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests` | Separate `Advisory Tests` workflow. It installs Playwright Chromium for the browser suite and runs with `continue-on-error`, preserving push/PR signal without making semantic-release depend on these suites. |
| Full Aspire E2E | `tests/Hexalith.EventStore.IntegrationTests` | Deferred until a reliable Aspire-in-CI topology exists. |

`integration.yml` pins `DAPR_CLI_VERSION` (`1.18.0`) and `DAPR_RUNTIME_VERSION`
(`1.18.2`) independently. The shared `references/Hexalith.Builds/Github/dapr-init`
action uses the CLI pin for `dapr/setup-dapr` and the runtime pin for
`dapr init --runtime-version`; older callers that omit `runtime-version` retain
the legacy behavior by falling back to `version`. Neither pin is the Dapr NuGet
`PackageVersion` (owned separately in `references/Hexalith.Builds/Props/Directory.Packages.props`,
currently `1.18.5`). Fresh OQ8 validation receives the same runtime pin explicitly,
while committed Story 4.14 evidence remains bound to its observed runtime `1.18.1`.

The Integration Tests checkout uses explicit `fetch-depth: 1` because fresh OQ8
capture needs only the checked-out commit. Committed OQ8 historical-evidence
closure is owned by the blocking Tier-1 `Oq8PlatformClosureTests`; Integration
Tests does not duplicate that current-checkout validation.

Do not reintroduce a `Category!=LiveSidecar` filter to make `Server.Tests`
deterministic. Live-sidecar coverage belongs in the live-sidecar project and
workflow so the deterministic release gate can remain unfiltered. Do not run
`dotnet test tests/Hexalith.EventStore.Server.Tests/` as the live lane.

Story 4.5's append-durability race and generic ETag control remain in this
dedicated LiveSidecar lane. Their hash-bound capture is an architecture evidence
artifact, not a reason to add the project to `unit-test-projects` or the release
workflow. See the [Story 4.5 evidence report](../_bmad-output/implementation-artifacts/4-5-append-durability-race-evidence.md).

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
  current package dependency floors. Before the first pack command, the packer
  also proves that every manifest entry resolves beneath the root `src/`
  directory, evaluates as packable, and produces its declared `PackageId` in
  Release/package mode. Those checks run MSBuild, so `--dry-run` also needs a
  working .NET SDK and a restorable checkout.
- `scripts/validate-nuget-packages.py` uses the shared contract in
  `tools/release_package_contract.py` to inspect each archive's embedded
  `.nuspec`. Filenames, embedded IDs, versions, dependencies, and the exact
  manifest inventory must agree; renamed, foreign, duplicate, mixed-version,
  source-path-bearing, and incomplete internal dependency metadata fail closed.
- `scripts/validate-consumer-package-references.py` creates one temporary
  package-only consumer per library package. Each consumer has exactly one
  direct manifest package reference, restores only from the local package
  directory plus NuGet.org, builds independently, and rejects project-backed
  assets. Dotnet tool packages are installed separately in isolated tool
  manifests.

The release prepare step in [`.releaserc.json`](../.releaserc.json) still uses
the `tools/` scripts so semantic-release remains manifest-driven:

```bash
python3 tools/pack-release-packages.py ./nupkgs <version>
python3 tools/validate-release-packages.py ./nupkgs <version>
```

The release validator consumes the same archive parser as CI and additionally
requires every embedded version to equal semantic-release's requested version.
Every root-owned manifest `ProjectReference` must become a canonical,
same-release NuGet dependency in each applicable target-framework group, and
each direct external `Hexalith.*` `PackageReference` must appear as a versioned
dependency. Both dependency contracts are waived only for the manifest tool
packages listed in `TOOL_PACKAGE_IDS`, which ship a self-contained closure and
emit no dependency metadata. The waiver is keyed on that manifest-owned list,
never on the archive's own `<packageTypes>`, so an archive cannot switch off the
proof it is subject to; a package that declares `DotnetTool` without being a
manifest tool package — or a manifest tool package that omits it — fails closed.
The Gateway project graph carries an additional explicit four-edge guard for
`Hexalith.EventStore.Admin.Abstractions`,
`Hexalith.EventStore.Contracts`, `Hexalith.EventStore.Server`, and
`Hexalith.EventStore.ServiceDefaults`, so the derived expectation the Gateway
archive is measured against cannot silently shrink. Project paths, build-output
paths (`bin/`, `obj/`, `artifacts/`) and checkout-local source metadata are
never accepted as package dependency evidence, anywhere in the nuspec.

## Release Flow

Release is an intentional operator action. Ordinary pushes and pull requests
run CI but never start Release. Before requesting environment approval, the
manual workflow fails closed unless all of these are true:

- the dispatch ref is exactly `refs/heads/main`;
- the dispatch SHA still equals the live `main` ref returned by GitHub;
- the exact SHA has a completed, successful `CI` workflow run whose event was a
  push to `main`.

The release concurrency group is `release-production` with cancellation
disabled, so a later request cannot silently replace an approved publication.
Only after source verification does the reusable release job enter the
`production` environment. That environment requires reviewer `jpiquot`, permits
deployments from `main` only, disables administrator bypass, and gates use of
the three explicitly mapped repository publication secrets. No duplicate
environment-secret copy is needed.

Semantic-release decides from commit history whether a release is warranted.
NuGet publishing remains scoped to the 14 packages listed in
[`tools/release-packages.json`](../tools/release-packages.json). Container
publishing is enabled only for the approved EventStore host mapping. Before any
NuGet package is pushed, semantic-release validates `NUGET_API_KEY`, the
container publisher helper, and the required Zot registry credentials so a
missing container secret cannot create a partial NuGet-only release. The
semantic-release `verifyRelease` phase re-proves that the source is still the
live `main` tip with exact successful push CI, then freezes exact repository,
version, source proof, environment, workflow run, approved Builds, helper
hashes, normalized package IDs, canonical manifest hash, container, and
platform identity. It also proves the new version is absent for all 14 NuGet
IDs and the container tag before Git-tag creation. The `publish` phase requires
exact frozen-identity equality and repeats both live source proof and every
destination check immediately before NuGet. The shared publisher requires both
earlier phases and repeats live source proof plus multi-media-type container-tag
absence immediately before the SDK registry write. NuGet and OCI probes use
exact read-only `HEAD` requests; redirects and ambiguous statuses fail closed.
Existing versions are collisions: the release path does not use
`--skip-duplicate` and never overwrites an existing package, tag, manifest, or
registry object.

The `main` branch accepts changes only through pull requests. Release automation
therefore does not use `@semantic-release/changelog` or `@semantic-release/git`:
it tags the already CI-approved source commit and publishes generated notes and
package assets through the GitHub release without creating or pushing a release
commit to `main`. Any tracked `CHANGELOG.md` update must arrive through its own
reviewed pull request; GitHub Releases are the current machine-generated release
record.

GitHub Release and package-asset publication remain enabled, but release
completion intentionally does not comment on or label referenced issues and
pull requests. The GitHub plugin uses `successCommentCondition: false` so
branch-name fragments embedded in merge commit messages, such as
`fix/gh-<run-id>`, cannot be mistaken for issue-closing references and turn an
otherwise successful publication red. A dedicated blocking CI lane proves this
behavior for issue-like and ordinary histories using the installed lockfile
version. Shared deterministic CI, this governance lane, and the Tenants
source-mode lane are all blocking. Required build, validation, package,
container, smoke, and GitHub publication failures remain blocking.

The reusable-workflow reference and `builds-execution-sha` input contain the
same reviewed 40-character Builds commit, currently
`c475153c0f533e4a65c956bd84127edf92181e5e`. The reusable workflow verifies its
resolved SHA, checks out the nested action at that exact commit, and invokes it
locally; the action then verifies its own action and helper bytes against the
same commit before semantic-release can run. This immutable release-tool pin is
independent of the development `references/Hexalith.Builds` gitlink, so routine
submodule updates do not rotate publication authority and publication upgrades
do not create pointer churn in development dependencies. Environment approval
is necessary but not sufficient for a corrective publication. Story 3.14 also
requires a dispatch-reserved stable version and an unexpired, one-use GitHub
issue-comment authority from the pinned `github:jpiquot` release-owner identity.
The dispatch names a stable EventStore authority issue, not a comment that would
have to predict the future run ID. After the run/attempt exists, the release owner
posts one immutable comment to that issue while the protected release job awaits
approval. Its canonical identity digest binds the repository, reserved version,
exact green source, workflow run/attempt, package and container destinations,
platform set, protected environment, Builds revision, and all installed helper
hashes. The `publish` preflight consumes that authority once through an
authenticated GitHub Actions receipt before the first NuGet write; pagination,
replay, expiry, excessive validity, wrong-role, changed scope, or mismatched
helper bytes fail closed.

The reserved version must equal Semantic Release's `nextRelease.version`, must
be stable, and must be strictly newer than every stable GitHub release/tag, every
version observed for all 14 NuGet IDs, and every stable registry tag. The exact
candidate must also be absent at all package and container destinations before
publication. A changed projection therefore requires a new run and authority;
`3.96.0` is never embedded as release policy.

The `publishCmd` calls the helper installed by the shared `publish-containers`
action only after the authority gate and NuGet publication:

GitHub validates reusable-workflow permissions against every nested job before
it starts the caller, including skipped jobs. The EventStore caller therefore
allows `attestations: write` and `id-token: write` so the shared workflow can be
resolved, while explicitly passing `governed-release: false`. The selected
legacy release job narrows its callee-level permissions and neither receives nor
uses those capabilities. This compatibility allowance does not enable signing,
SBOM generation, or attestation for EventStore.

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
Both configs must also contain identical exact
`org.opencontainers.image.source`, `.url`, `.documentation`, `.revision`, and
`.version` labels. EventStore rebinds those five labels after the .NET SDK
multi-RID inner-build parser so URL colons cannot be truncated to `https` and
passes source revision and release version as explicit publisher inputs.

Both immutable child references (`repository@sha256:...`) are explicitly pulled
with bounded timeouts and run the same bounded
smoke: loopback ephemeral host port, `ASPNETCORE_URLS=http://+:8080`, a fixed
non-secret JWT issuer/audience/key used only by the ephemeral smoke container,
and `/alive`. The declared Development hosting environment and symmetric-key
override are explicit, and the
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
child manifest/config identities, exact platforms, frozen publication identity
and destination checks, and both smoke logs/hashes. Registry, authentication,
emulation, product, or evidence failure leaves the release non-authorizing.
The reusable workflow uploads the complete hidden evidence directory with
`always()` so partial publication remains visible.

Any successful write followed by a later failure permanently quarantines that
version as immutable non-authorizing evidence. A retry resolves a new version
newer than every live NuGet, registry, tag, and release destination and requires
a new authority. `tools/validate-corrective-release-evidence.py` verifies the
canonical Story 3.14 handoff directly from retained bytes: exactly 14 manifest
packages and their nuspec source commits, one two-platform `eventstore` index,
raw index/child/config hashes and descriptors, exact child labels, both
digest-pinned bounded Development liveness smokes, and one source/run/Builds/authority
lineage. The packet records the selected codec and verifier content hashes and
must itself use that codec's canonical UTF-8 form. Its output explicitly selects
no deployed identity and grants no mutation authority; Story 3.15 owns that
decision.

Story 3.12 supplies historical corrective-release evidence to Story 3.13. After the
Story 1.20 proof archives were declared nonexistent, Story 3.13's selected exact
identity is source `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`, release `v3.94.1`,
and the 14 manifest packages at version `3.94.1`. Those 14 `v3.94.1` archives under
the selected packet `packages/` are tracked evidence, not restore output, because
`ValidatePackageBytes` rehashes them when `byte_verification.result` is `pass`. The
`fa2d1c99` packet remains historical fail-closed evidence and is not the selected
candidate. Story 1.20 remains
complete and authoritative for source/package parity only; Story 3.13 cannot
rewrite either predecessor, infer identity across lineages, or authorize a
consumer migration, deployment, publication, or registry mutation. Story 1.20
retains sole authority over its approval fields and consumer-migration decision.
The checked-in GitHub approval-role allowlist remains historical proof-packet
evidence; the release workflow does not consume it.

Story 3.13 remains `in-progress` until the `v3.94.1` packet independently satisfies
AC1–AC4, including three content-bound acceptances of the new review subject. It
authorizes no release, registry, deployment, consumer, or predecessor mutation.

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
