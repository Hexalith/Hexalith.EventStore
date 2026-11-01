# CI/CD Pipeline

This page documents the EventStore-specific GitHub Actions wiring. Shared
Hexalith CI/CD standards and reusable workflow guidance live in
[`references/Hexalith.Builds/.github/workflows/ci-cd-standards.md`](../references/Hexalith.Builds/.github/workflows/ci-cd-standards.md).

## Workflows

| Workflow | File | Triggers | Purpose |
|----------|------|----------|---------|
| **CI** | `.github/workflows/ci.yml` | `push` and `pull_request` to `main` and `release/parties-sdk-3.130.2` | Runs the required shared `domain-ci.yml@main` job for the solution and deterministic tests, plus the EventStore-owned governance and compatibility checks. |
| **Advisory Tests** | `.github/workflows/advisory-tests.yml` | `push`, `pull_request` to `main` and `release/parties-sdk-3.130.2`, manual dispatch | Visible non-release-blocking browser and governance suites. It installs Chromium before Playwright E2E tests, runs with `continue-on-error`, and release does not listen to this workflow. |
| **Integration Tests** | `.github/workflows/integration.yml` | `push`, `pull_request` to `main` and `release/parties-sdk-3.130.2`, manual dispatch | Dedicated DAPR lane for `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`. It is intentionally separate from the release trigger. |
| **Payload Protection Lifecycle** | `.github/workflows/payload-protection-lifecycle.yml` | `push`, `pull_request` to `main` and `release/parties-sdk-3.130.2`, manual dispatch | Isolated Dapr 1.18.7/PostgreSQL v2 capability, catalog, lease, drain, recovery, rotation, generation and restore tests. Requires at least twenty-six executed tests with zero skips, including cumulative transport limits, writer capacity, conditional restore decisions and authority races. Local qualification and its limits are recorded in the Story 8.5 spec; a CI run is separate evidence. |
| **CodeQL** | `.github/workflows/codeql.yml` | `push`, `pull_request` to `main` and `release/parties-sdk-3.130.2`, weekly schedule | Shared CodeQL workflow using `@main`, with the module configuration excluding archived `_bmad-output` source copies. |
| **Dependency Review** | `.github/workflows/dependency-review.yml` | `pull_request` to `main` and `release/parties-sdk-3.130.2` | Thin caller to the shared dependency-review gate using `@main`. |
| **Commitlint** | `.github/workflows/commitlint.yml` | `push` and `pull_request` to `main` and `release/parties-sdk-3.130.2` | Thin caller to the shared Conventional Commits gate using `@main`. |
| **Release** | `.github/workflows/release.yml` | manual dispatch from the current green `main` or exact maintenance tip | Exact-source preflight followed by an EventStore-owned job in the protected `production` environment. The job exchanges GitHub OIDC through `NuGet/login`, runs semantic-release, and uses immutable `Hexalith.Builds` actions for the approved EventStore container. |

The only maintenance release branch is `release/parties-sdk-3.130.2`. Its
protected publication is limited to repository `Hexalith/Hexalith.EventStore`,
version `3.130.2`, source proof `ci.yml`, and environment `production`; arbitrary
refs, versions, reservations, and historical receipts cannot authorize it.
`scripts/verify-release-source.sh` requires current exact-source successful push
runs of CI and commitlint, plus full `integration.yml` on maintenance. It runs
before the protected job and again immediately before the OIDC exchange. The
maintenance wrapper repeats all three proofs at both verify and publish.
The corrected canonical publisher freezes all three exact-branch workflow
receipts and revalidates them again at the container boundary before its first
write. Its focused blocking workflow lints the complete new commit range.
Main retains its existing CI and commitlint requirements.

The read-only preproduction job fetches complete history and runs
`tools/verify-release-candidate.mjs` using the lockfile-pinned native commit
analyzer and semantic-release core helpers. The real reachable `v3.130.1` must
resolve to published source `1de42b706f52af0da6513f0b7663002606d17156`;
only an actual patch candidate of `3.130.2` may enter production. Native
cosmiconfig resolution must match the committed configuration. Pure native
branch normalization uses already-fetched configured-branch tag inventories;
the candidate must satisfy its accepted type/range and have no globally
existing candidate tag, including on unrelated history. No release, minor,
major, configuration drift, wrong base or wrong source refuses before
production/OIDC. No fetch, note update or user-ref mutation runs in this gate.
The same qualification repeats before OIDC. Structured JSON retains its
source/base/type/version, effective configuration digest and pinned native
identities under `.hexalith/release-evidence/qualification`; the preproduction
job uploads its receipt and the production evidence upload retains revalidation.
These records are evidence and grant no publication authority.

This finite regular release ref is intentionally ordered before `main`, with
`channel: false` on both. It has a one-release lifecycle: after `3.130.2`, remove
its publication/deployment authorization and retire the branch through the
normal reviewed process. It conveys no authority for another release branch
or version. Do not merge future feature histories into it or use it as a
reusable maintenance template. Main keeps its existing release behavior.

Protect the exact maintenance branch with the normal required checks and PR
review policy. Add only this exact branch to the production deployment policy;
retain the designated reviewer and disabled administrator bypass. The existing
`release.yml`/production trusted publishing identity and fourteen-package
inventory remain authoritative. After publication, independently download all
fourteen official archives and verify embedded ID, version and source commit.
The normal main workflow jobs and branch protection remain required.

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
- The semantic-release GitHub lifecycle fixture and its blocking Node 24 CI
  dependency lane.
- The approved release container mapping:
  `src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore`.

Hexalith.Builds action and reusable workflow references generally use `@main`
by Hexalith policy. The EventStore-owned publication job is the explicit
exception: it checks out one exact Builds commit and invokes its local actions.
Its third-party actions are SHA-pinned in `release.yml`.

CodeQL keeps C# analysis without builds and the default security queries. Its
[configuration](../.github/codeql/codeql-config.yml) excludes only `_bmad-output/**`,
which contains historical project and source copies rather than active build
inputs. Application, test, sample, tool, and documentation source remains in the
scan.

## Evidence Policy

Proof is the repository's tests plus CI, as decided in the
[architecture document](../_bmad-output/planning-artifacts/architecture.md)
(AD-12, evidence storage). In CI terms, `scripts/check-committed-files.sh` is
the single enforcement point: the Husky `pre-commit` and `pre-merge-commit`
hooks run it with `--staged`, and the required `live-sidecar` job in
`integration.yml` runs it over the full tree right after checkout, together with
`tools/tests/test_check_committed_files.py` and the Dapr broker qualification
input tests. It lives there because the required `ci / build-and-test` job is
the shared Hexalith.Builds reusable workflow, which accepts no caller-defined
steps. Workflow artifact uploads (test results, release evidence) remain the
place for run output.

## Test Lanes

| Lane | Projects | Workflow behavior |
|------|----------|-------------------|
| Deterministic release gate | Contracts, Client, ServiceDefaults, Testing, SignalR, Admin, AppHost, DomainService, QueryRouting, Sample, Testing.Integration, RestApi.Generators, and `tests/Hexalith.EventStore.Server.Tests` | Blocking in the required shared `domain-ci.yml@main` job through `unit-test-projects`. The serial Server.Tests assembly remains unfiltered; the job has a 180-minute limit to complete the full suite. |
| Semantic-release governance | `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs`, `tools/tests/test_release_publication_authority.py` | Blocking EventStore-owned CI job. It provisions Node 24, installs exactly `package-lock.json` with `npm ci`, and exercises both release-history cases against an Undici-guarded loopback-only fake GitHub boundary. It also runs the publication-authority regressions against the exact approved Builds publication engine. |
| Live-sidecar DAPR lane | `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`, plus `scripts/check-committed-files.sh`, `tools/tests/test_check_committed_files.py` and `tools/tests/test_dapr_broker_qualification.py` | Dedicated `Integration Tests` workflow after `dapr init`; its required `live-sidecar` job first runs the committed-file guard and the two Python suites. This lane is visible but not part of the semantic-release gate. It is not a `dotnet test` of `Server.Tests`. |
| Payload-protection lifecycle | `tests/Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests` | Dedicated `Payload Protection Lifecycle` workflow; isolated processes and digest-pinned PostgreSQL v2 state, persisted assertions and zero skips. The existing `Payload Protection` core workflow runs independently. |
| Advisory browser/governance suites | `tests/Hexalith.EventStore.Admin.UI.E2E`, `tests/Hexalith.EventStore.DeferredWorkGovernance.Tests` | Separate `Advisory Tests` workflow. It installs Playwright Chromium for the browser suite and runs with `continue-on-error`, preserving push/PR signal without making semantic-release depend on these suites. |
| Full Aspire E2E | `tests/Hexalith.EventStore.IntegrationTests` | Deferred until a reliable Aspire-in-CI topology exists. |
| Platform package integration | `tests/Hexalith.EventStore.Platform.IntegrationTests` | Deferred until a qualified Platform release tag and matching Builds catalog enable the receipt-capable `domain-ci-v2` package profile. The existing blocking AppHost integration remains active. |
| Isolated synthetic sharding | `tests/Hexalith.EventStore.Sharding.Tests` | Deferred CI automation; the operator opt-in Debug lane and its owned `Sharding.TestHost` fixture are documented in `tests/Hexalith.EventStore.Sharding.Tests/README.md`. Synthetic observations do not qualify a production provider profile or release gate. |

The qualified lifecycle profile keeps Dapr's default 4 MiB gRPC request limit.
Each transaction admits at most 512 operations and 2 MiB of charged data: value,
UTF-8 key and ETag bytes, plus 1024 bytes per operation and 1024 bytes per
transaction for conservative framing/metadata allowance. Each enclosing engine
phase admits at most 4096 strong reads and 16 MiB of cumulative returned values;
it reserves a full 64 KiB response before each call. Whole-index enumeration
also checks that every remaining page and the confirming head fit at the maximum
64 KiB per response, in addition to bytes already charged to the enclosing phase.
This conservative preflight can reject an index whose actual sparse pages would
fit; it does not dynamically sum only the later observed page sizes. Registration and traffic
admission also calculate the member-catalog and future clearing-proof working
set, reserving 1 MiB and 512 reads for authority/index work. Immutable clearing
acknowledgements are read once per resume; permanent Cleared members require no
acknowledgement read. The existing structural schema bounds remain unchanged.
Real Dapr qualification demonstrates the exact charged transaction boundary and
257 completed writer reservations with both indexes spanning two pages, plus
257 retained registered process incarnations. This is the measured supported
case; it does not qualify every structural maximum or a production throughput.
Index membership currently rewrites every page in the affected scope/KEK
inventory. Therefore one heavily shared KEK can reach the transaction ceiling
before the structural entry maximum; rotation's complete inventory verification
can independently reach the read ceiling. These ceilings depend on actual record
sizes, page count and the enclosing authority/work transaction, rather than a
fixed supported key count. Oversized plans fail before submission with the typed
`ProviderUnavailable` admission result; capacity exhaustion is not a transient
read failure and does not enter the read retry loop. State-conflict recovery
never repeats backend mutations, and ambiguous backend outcomes are reconciled
by stable operation ID. Confirmed transient backend failures retain the bounded
retry policy. An unreturned Create with a durable Reserved record remains quarantinable.
A separate capacity redesign is required before Story 8.7 host wiring.
Transactions above the operational limit are rejected with output/completion
withheld. Read-budget exhaustion retains discoverable indexed work for a later
phase. Structurally valid workloads above this profile's limits remain
unsupported; the engine never blindly retries a mutation.

`integration.yml` pins `DAPR_CLI_VERSION` (`1.18.0`) and `DAPR_RUNTIME_VERSION`
(`1.18.2`) independently. The shared `references/Hexalith.Builds/Github/dapr-init`
action uses the CLI pin for `dapr/setup-dapr` and the runtime pin for
`dapr init --runtime-version`; older callers that omit `runtime-version` retain
the legacy behavior by falling back to `version`. Neither pin is the Dapr NuGet
`PackageVersion` (owned separately in `references/Hexalith.Builds/Props/Directory.Packages.props`,
currently `1.19.0`).

The Integration Tests checkout uses explicit `fetch-depth: 1` because its test
steps need only the checked-out commit. A step that reads Git history must also
change that checkout to `fetch-depth: 0`.

Do not reintroduce a `Category!=LiveSidecar` filter to make `Server.Tests`
deterministic. Live-sidecar coverage belongs in the live-sidecar project and
workflow so the deterministic release gate can remain unfiltered. Do not run
`dotnet test tests/Hexalith.EventStore.Server.Tests/` as the live lane.

### PostgreSQL image rotation

The live-sidecar workflow and `Oq8PostgresqlFixture` use the same reviewed
multi-platform PostgreSQL index:
`postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`.
The immutable index digest is the registry pin; a platform child manifest or
a local Docker image/config ID is not a valid replacement.

Rotate this identity only as one reviewed change:

1. Inspect the upstream tag with
   `docker buildx imagetools inspect postgres:<version> --format '{{json .Manifest}}'`.
2. Confirm the returned object is the intended multi-platform index, review its
   version and complete platform set, and record the index digest rather than an
   `amd64` or `arm64` child digest.
3. Replace the literal together in `.github/workflows/integration.yml`,
   `Oq8PostgresqlFixture.PostgresImage`, and
   `PostgreSqlImageGovernanceTests.ReviewedPostgresImage`.
4. Build the Contracts test project, run `PostgreSqlImageGovernanceTests`, and run
   `actionlint .github/workflows/ci.yml .github/workflows/integration.yml`.
5. Pull the digest-pinned index and run the complete live-sidecar project. The
   fixture must retain its fail-closed `docker image inspect` prerequisite and
   bounded readiness checks.

Tag-only, malformed, architecture-specific, mismatched, missing, or duplicated
image declarations fail governance and must not be hidden behind generated
source, MSBuild properties, `PATH`, or local Docker tags.

Story 4.5's append-durability race and generic ETag control remain in this
dedicated LiveSidecar lane; they are not a reason to add the project to
`unit-test-projects` or the release workflow. See the
[Story 4.5 report](../_bmad-output/implementation-artifacts/4-5-append-durability-race-evidence.md).

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
run CI but never start Release. The dispatch has no inputs. Semantic Release selects the version
from the approved branch's commit history. Main uses
`gh workflow run release.yml --ref main`; the bounded maintenance operation uses
`gh workflow run release.yml --ref release/parties-sdk-3.130.2`.
The approved immutable Builds actions execution pin is
`30ea43a56330a9828ed0bb2e837289231d8630d1`, matching `.github/workflows/release.yml`
and `docs/ci-secrets-checklist.md`. Before requesting environment approval, the
manual workflow fails closed unless all of these are true:

- the dispatch ref is exactly `refs/heads/main` or
  `refs/heads/release/parties-sdk-3.130.2`;
- the dispatch SHA still equals the live selected branch ref returned by GitHub;
- the exact SHA has a completed, successful push run of `ci.yml` and
  `commitlint.yml`, plus `integration.yml` for maintenance;
- maintenance's read-only native analyzer qualification proves actual patch
  `3.130.2` from the verified reachable published `v3.130.1` base.

The release concurrency group is `release-production` with cancellation
disabled, so a later request cannot silently replace an approved publication.
Only after source verification does the EventStore-owned publish job enter the
`production` environment. That environment requires reviewer `jpiquot`, permits
deployments from exactly `main` and `release/parties-sdk-3.130.2`, and disables
administrator bypass. The job has
`id-token: write` so `NuGet/login` can exchange its protected EventStore
`release.yml` identity for a temporary NuGet key. The nuget.org package owner
must activate a policy for repository `Hexalith/Hexalith.EventStore`, workflow
`release.yml`, environment `production`, and all 14 manifest package IDs, then
set repository variable `NUGET_TRUSTED_PUBLISHING_USER` to the policy creator's
nuget.org username. The job maps the two Zot organization secrets and no stored
NuGet API key. Do not dispatch production before policy activation and review.

Semantic-release decides from commit history whether a release is warranted.
NuGet publishing remains scoped to the 14 packages listed in
[`tools/release-packages.json`](../tools/release-packages.json). Container
publishing is enabled only for the approved EventStore host mapping. Before any
NuGet package is pushed, semantic-release validates the temporary
`NUGET_TRUSTED_PUBLISHING_KEY`, the
container publisher helper, and the required Zot registry credentials so a
missing container secret cannot create a partial NuGet-only release. Immediately before Semantic Release, the publish job re-proves those two
push runs. The semantic-release `verifyRelease` phase then freezes exact repository,
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

Semantic Release creates its Git tag before invoking the publish hook. The
verify preflight therefore requires the candidate tag to be absent, while the
publish preflight permits exactly one `v<candidate-version>` self-tag only when
it targets the approved source SHA. The proof is retained in version-floor
evidence; a missing, duplicate, unprefixed, or wrong-source tag fails closed.

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

The EventStore job checks out Builds at the reviewed 40-character commit
`30ea43a56330a9828ed0bb2e837289231d8630d1`, verifies that checkout's HEAD,
and invokes its actions locally. The container action verifies its own action
and helper bytes against the same commit before semantic-release can run. This
immutable release-tool pin is
independent of the development `references/Hexalith.Builds` gitlink, so routine
submodule updates do not rotate publication authority and publication upgrades
do not create pointer churn in development dependencies. Environment approval
authorizes publication. EventStore sets `HEXALITH_RELEASE_REQUIRE_AUTHORITY: 'false'`
and supplies no reserved version, authority issue, or authority owner. Semantic
Release selects the version. A typed reservation cannot override that version.

The shared Builds publisher still implements a separate corrective gate: a
dispatch-reserved stable version plus a one-use GitHub issue-comment authority.
EventStore does not use that gate. The exact Semantic Release version must be
absent at all package and container destinations before publication, and source
proof, frozen identity, and version-floor checks still run. Main retains semantic version selection; the maintenance source additionally
requires exactly `3.130.2`, and every other non-main branch/version tuple refuses.

The `publishCmd` calls the helper installed by the pinned shared
`publish-containers` action only after the applicable preflight gates and NuGet
publication. The EventStore job grants `id-token: write` for the OIDC exchange;
it does not request `attestations: write`.

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
`org.opencontainers.image.source`, `.url`, `.documentation`, `.revision`, `.version`,
`.created`, and `org.opencontainers.artifact.created` labels. EventStore rebinds those labels after the .NET SDK
multi-RID inner-build parser so URL colons cannot be truncated to `https` and
passes source revision, release version, and one publisher-owned RFC 3339 creation instant as
explicit publisher inputs.

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
The EventStore release job uploads the complete hidden evidence directory with
`always()` so partial publication remains visible.

Any successful write followed by a later failure permanently quarantines that
version as immutable non-authorizing evidence. A retry resolves a new version
newer than every live NuGet, registry, tag, and release destination and requires
a new authority.

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

- NuGet trusted publishing requires the package owner's activated 14-ID policy
  and repository variable `NUGET_TRUSTED_PUBLISHING_USER` before the first
  protected production dispatch.
- SBOM, artifact attestations, package signing, and provenance evidence remain
  shared Hexalith.Builds backlog items unless a story assigns them to EventStore.
- The EventStore release workflow pins its own third-party actions and checks
  npm signatures; the shared workflows retain their own action policy.
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

For local Contracts packaging and governance checks, restore and build in Debug
source-reference mode, then run the individual Contracts project from EventStore:

```bash
dotnet restore tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -p:UseNuGetDeps=false -m:1 -v:q
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Debug -p:UseNuGetDeps=false -m:1 --no-restore -v:q
dotnet test --project tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Debug -p:UseNuGetDeps=false --no-build --no-restore
```

The governance sources are `Hexalith.Builds`, `Hexalith.AI.Tools`, and
`Hexalith.Tenants`. Standalone checkouts use their root `.gitmodules` declarations.
An umbrella workspace may supply its own root-declared dependencies when its
`.gitmodules` also declares the exact EventStore checkout; a containing Tenants
repository supplies its own source. Uninitialized nested submodules are left
untouched. Each selected source must own its Git worktree and identify the intended
Hexalith repository through its origin remote. Unrelated sibling clones and paths
that cause Git to discover an ancestor repository are rejected.

`HEXALITH_BUILDS_SOURCE` can explicitly select a verified Builds checkout. The
release-caller guard verifies publication authority separately from the EventStore
Builds development gitlink. The bounded publisher correction must descend from
the previous approved publisher and preserve all other executed container helper
bytes. The Builds object store must contain the exact
historical commits used by the release and tool-hash assertions, including the
reviewed release pin; checkout layout does not relax commit, ancestry, or tool-byte
checks.

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
