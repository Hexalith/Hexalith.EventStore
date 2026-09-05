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

### PostgreSQL image rotation

The live-sidecar workflow and `Oq8PostgresqlFixture` use the same reviewed
multi-platform PostgreSQL index:
`postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`.
The immutable index digest is the registry pin; a platform child manifest or
the Docker image/config ID captured in historical OQ8 evidence is not a valid
replacement.

Rotate this identity only as one reviewed change:

1. Inspect the upstream tag with
   `docker buildx imagetools inspect postgres:<version> --format '{{json .Manifest}}'`.
2. Confirm the returned object is the intended multi-platform index, review its
   version and complete platform set, and record the index digest rather than an
   `amd64` or `arm64` child digest.
3. Replace the literal together in `.github/workflows/integration.yml`,
   `Oq8PostgresqlFixture.PostgresImage`, and
   `tools/validate-oq8-platform-evidence.py`, and
   `PostgreSqlImageGovernanceTests.ReviewedPostgresImage`; then issue the
   required additive, content-bound Story 4.15 successor evidence without
   rewriting historical v1 bytes.
4. Build the Contracts test project, run `PostgreSqlImageGovernanceTests`, run
   `actionlint .github/workflows/integration.yml`, and run
   `python3 tools/validate-oq8-platform-evidence.py`.
5. Pull the digest-pinned index and run the complete live-sidecar project. The
   fixture must retain its fail-closed `docker image inspect` prerequisite and
   bounded readiness checks.

Tag-only, malformed, architecture-specific, mismatched, missing, or duplicated
image declarations fail governance and must not be hidden behind generated
source, MSBuild properties, `PATH`, or local Docker tags.

Story 4.15 successor timestamps use exact UTC seconds in
`YYYY-MM-DDTHH:MM:SSZ` form. Validation parses that format generically, captures
current UTC once per run, rejects timestamps later than that instant, and keeps
the strict execution → subject freeze → receipts → handoff order. Tests that
exercise future-time rejection derive their mutation from runtime UTC; do not
renew them by hard-coding another calendar date.

The v1 and v2 successor packets remain immutable historical evidence. V2 is
validated against completed closure commit
`83b32fcfad7bb608098aebccdc15002636ffb431`, not against later working-tree
bytes. The additive `story-4-15-successors/v3` packet is the active lineage for
the evolved validator, closure tests, and this guidance; current-source closure
requires valid historical v1/v2 evidence plus a complete v3 subject, reviews,
handoff, and path-sorted manifest.

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
run CI but never start Release. The dispatch exposes one boolean
`bypass-validation` input, defaulting to `false`; semantic-release still derives
the version from commit history. The ordinary path requires the successful `CI`
push run described below. The explicitly selected bypass path instead requires
the same source SHA to have a successful push run of `commitlint.yml` and can be
requested only with
`gh workflow run release.yml --ref main -f bypass-validation=true`.
The approved immutable shared release workflow and execution pin is
`22a578b576a515d2af214fe81859447fffc97981`, matching `.github/workflows/release.yml`
and `docs/ci-secrets-checklist.md`. Before requesting environment approval, the
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

The reusable-workflow reference and `builds-execution-sha` input contain the
same reviewed 40-character Builds commit, currently
`22a578b576a515d2af214fe81859447fffc97981`. The reusable workflow verifies its
resolved SHA, checks out the nested action at that exact commit, and invokes it
locally; the action then verifies its own action and helper bytes against the
same commit before semantic-release can run. This immutable release-tool pin is
independent of the development `references/Hexalith.Builds` gitlink, so routine
submodule updates do not rotate publication authority and publication upgrades
do not create pointer churn in development dependencies. Environment approval is what authorizes an ordinary publication, and the caller
declares `require-publication-authority: false` to say so.

Story 3.14 built a second, stronger gate for a corrective release that has to be
individually authorized rather than merely approved: a dispatch-reserved stable
version plus an unexpired, one-use GitHub issue-comment authority. When enabled for
EventStore, the caller pins that authority to the `github:jpiquot` release-owner identity.
That gate remains implemented and tested
in `Hexalith.Builds`, and is off by default here, because it costs the operator a
hand-computed version and an out-of-band authority comment per run. Re-enabling it
means restoring the two operator-supplied `workflow_dispatch` inputs (`release-version` and
`release-authority-issue-url`), setting `require-publication-authority: true`, mapping those values
to `reserved-version` and `release-authority-issue-url`, supplying the caller-pinned
`release-authority-owner`, and updating the governance
assertions that deliberately require those inputs to be absent while the gate is off. The live preflight only
shape-checks whatever owner value it is given; the caller is what pins the identity.
Two separate governance tests hold that line, and it is worth knowing which does what:
`ReleaseAuthorityOwnerIsPinnedWheneverTheAuthorityGateIsEnabled` checks the owner value
alone -- if the gate is ever turned on, the owner must be `github:jpiquot` -- while
`ReleaseCallerPinsSharedExecutionAndOneMappingWithoutPublicationAuthorityInputs` asserts the caller
currently carries none of the three reservation inputs. So while the gate stays off, any
attempt to re-enable it fails the suite until both tests are updated together; neither
test validates the values of `reserved-version` or `release-authority-issue-url`.
The posture is declared, never inferred: with the gate off any supplied
reservation value fails closed rather than being silently ignored, a half-declared
authority is rejected instead of read as absence, and a declaration that is
neither `true` nor `false` fails closed.

When the gate is on, the dispatch names a stable EventStore authority issue, not a
comment that would have to predict the future run ID. After the run/attempt exists,
the release owner posts one immutable comment to that issue while the protected
release job awaits approval. Its canonical identity digest binds the repository,
reserved version, exact green source, workflow run/attempt, package and container
destinations, platform set, protected environment, Builds revision, and all
installed helper hashes. The `publish` preflight consumes that authority once
through an authenticated GitHub Actions receipt before the first NuGet write;
pagination, replay, expiry, excessive validity, wrong-role, changed scope, or
mismatched helper bytes fail closed. The durable consumption evidence retains the
authenticated comment-list reread rather than GitHub's distinct POST response
shape, so the container phase rechecks the same record bytes that the publish
phase froze. A reserved version must equal Semantic Release's
`nextRelease.version`, must be stable, and must be strictly newer than every stable
GitHub release/tag, every version observed for all 14 NuGet IDs, and every stable
registry tag.

Both postures are identical in every other respect. The exact candidate version
must be absent at all package and container destinations before publication, and
source proof, frozen identity, and version-floor checks run either way. No
projected version such as `3.96.0` is ever embedded as release policy.

The `publishCmd` calls the helper installed by the shared `publish-containers`
action only after the applicable preflight gates and NuGet publication:

GitHub validates reusable-workflow permissions against every nested job before
it starts the caller, including skipped jobs. The EventStore caller therefore
allows `attestations: write` and `id-token: write` so the shared workflow can be
resolved, while explicitly passing `governed-release: false`. The selected
legacy release job declares no narrower job-level permission block, so it
inherits both write scopes even though it does not use them. This is a real token
widening, tracked for removal in the shared reusable-workflow split; it does not
by itself enable signing, SBOM generation, or attestation for EventStore.

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

### Adding a later corrective-release evidence handler (`v4`)

The live Story 3.14 verifier is a trusted, versioned dispatcher: it pins
`tools/release_evidence_handlers/v3.py` (and its package initializer) by
SHA-256 before execution, and `v3` itself is a deliberate single-packet
allowlist for the frozen `v3.96.2` codec digest. Do not edit live `v3.py` or
the dispatcher pins to “fix” a frozen packet — that invalidates the
handler pin and can break Story 3.15’s transitive import binding.

When a *new* corrective release needs a successor packet, add a handler rather
than rewriting `v3`:

1. Retain the new packet’s exact codec/verifier bytes under the packet tree the
   same way Story 3.14 retained `successful/tools/`.
2. Author `tools/release_evidence_handlers/v4.py` for that codec version only,
   with its own `CODEC_VERSION`, `EXPECTED_PACKET_CODEC_SHA256`, and validation
   rules. Leave `v3.py` byte-immutable.
3. Register the new `(schema, version, packet-codec-sha256)` key in
   `tools/validate-corrective-release-evidence.py` `HANDLERS`, and pin the new
   on-disk module (and package initializer, if changed) in
   `HANDLER_FILE_SHA256` / `HANDLER_PACKAGE_FILE_SHA256`. Recompute those pins
   with `sha256sum` on the files you added or changed.
4. Add focused mutation coverage that proves an unsupported version still fails
   closed and that the new handler accepts only its intended packet digest.
5. Do not rotate the Story 3.14 frozen evidence packet, do not claim Story 3.15
   / FR36 closure from the handler addition alone, and do not treat handler
   authorship as publication authority.

This procedure is documentation only until a later authorized corrective release
needs it; this repository’s current live codec/handler/dispatcher pins stay
unchanged.

Contracts CI excludes `Category=HeavyweightContainerPublish` via
`--filter-not-trait`. That trait remains only on the two real
`PublishContainer` cases —
`RealMultiRidArchiveContainsExactProvenanceInBothChildConfigs` and
`ContainerPublicationRejectsMissingProvenanceInputs`. The msbuild-only
`ContainerPublicationRejectsMalformedProvenanceInputs` theory stays in the
default Contracts gate so fail-closed `ValidateContainerProvenanceInputs`
negatives are still observed. Local Microsoft.Testing.Platform runs that
also want the fast lane must put `Category!=HeavyweightContainerPublish`
inside a single `--filter` expression; do not combine `--filter` with
`--filter-not-trait`.

Story 3.12 supplies historical corrective-release evidence to Story 3.13. After the
Story 1.20 proof archives were declared nonexistent, Story 3.13's selected exact
identity is source `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`, release `v3.94.1`,
and the 14 manifest packages at version `3.94.1`. Those 14 `v3.94.1` archives under
the selected packet `packages/` are tracked evidence, not restore output, because
`ValidatePackageBytes` rehashes them when `byte_verification.result` is `pass`. The
`fa2d1c99` packet remains historical fail-closed evidence and is not the selected
candidate. Story 1.20 remains complete and authoritative for source/package parity
only; Story 3.13 cannot rewrite either predecessor, infer identity across lineages,
or authorize a consumer migration, deployment, publication, or registry mutation.
Story 1.20 retains sole authority over its approval fields and consumer-migration
decision. The checked-in GitHub approval-role allowlist remains historical
proof-packet evidence; the release workflow does not consume it.

Story 3.13 owns the `v3.94.1` rejection only. Its content-bound disposition envelope
lives under `_bmad-output/implementation-artifacts/evidence/story-3-13/disposition/`
and is addressed by the immutable review-subject digest recorded in the selected packet
`6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97`, which an operator
reproduces with `sha256sum review-subject.json`. The envelope records
`candidate_disposition: rejected-non-authorizing`, `deployed_runtime_parity:
unavailable-for-v3.94.1`, `selected_deployed_identity: null`, and
`deployment_authorized: false`. It lives outside both content-addressed evidence trees
because the frozen crosswalk pins `receipt_count` to `0`. The malformed `https` values
for `org.opencontainers.image.source`, `.url`, and `.documentation`, the absent
`.revision` label, and the withheld deployment authority are retained verbatim and are
never reinterpreted as passing. Story 3.13 reached `done` on 2026-08-24, when the EventStore owner, Release owner and
Test Architect each accepted the unchanged envelope
`a7ecd45524ca3ebd6f2c9a23143e2786f31d705f6a4a741be8f35cfc1c1851ec`; the approved
2026-08-16 correct-course decision was planning authority, not a receipt, and was not
counted. Both owner receipts are GitHub-minted issue comments on #351 whose bodies are
the acceptance JSON; the Test Architect receipt is a bmad record. Because the roster maps
both owner roles to one account, that acceptance is a self-attestation rather than
independent three-party review.

Story 3.14 owns the corrective release; Story 3.15 owns positive deployed-runtime parity
for it. A complete Story 3.13 disposition therefore still selects no image and authorizes
no release, registry, deployment, consumer, or predecessor mutation, and it creates no
dependency on Story 3.14.

The Story 3.13 tracker key stays `3-13-v3-94-1-deployed-runtime-evidence-disposition`
and only its display title changed. Proposal §4.9 allows the rename only when the story
filename, spec key, and every repository reference move atomically; both Story 3.13
proof-packet filenames are pinned by SHA-256 inside the frozen review subjects and the
focused verifier, so they cannot be renamed and that condition cannot be met.

### Story 3.15 corrected deployed-runtime parity

Story 3.15 independently revalidates the canonical Story 3.14 identity digest
`4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`, downloads and rehashes all
14 NuGet.org packages containing one `.signature.p7s` entry, and maps those public bytes to the distinct GitHub
release-asset bytes retained by Story 3.14. It also rereads the raw immutable OCI index, both child
manifests, and both configs, then runs bounded digest-pinned `/alive` smokes under the Production
hosting environment for `linux/amd64` and `linux/arm64`.

`tools/validate-corrected-deployed-runtime-parity.py` dispatches only to the exact allowlisted v1
handler. Every file on the import path — the v1 handler, its package initializer, the v3 predecessor
handler, and *its* package initializer — is pinned before execution. The source-only loader compiles
those verified bytes directly, so stale timestamp-valid bytecode cannot stand in for reviewed
source and importlib never resolves these modules at all -- provenance is established before
execution rather than re-checked afterwards. The handler
never executes packet-supplied code: it parses and rehashes retained bytes, rejects symlinks and
nuspec DTD/entity declarations, checks the closed technical inventory, recomputes the canonical
subject, and validates exactly three subject-addressed receipts.

The closure's `dispatch` block binds the v1 handler, the v3 predecessor handler, and the v3
package initializer directly; the v1 package initializer is pinned in the dispatcher's
`IMPORT_PATH_FILE_SHA256`, whose own bytes are bound by `dispatch.verifier`, so the trust chain
closes transitively over all four. This closes a gap found in the
2026-08-25 review: previously only `v1.py` and its dispatcher were bound, so a tampered `v3.py` —
which performs predecessor validation, nuspec identity parsing, and the release-manifest check —
produced the identical subject and selected identity with all three receipts still valid, contrary
to the closure's own rerun trigger. Binding those bytes changed `v1.py` and therefore the canonical
subject, which by that same rerun trigger **rejected every receipt collected for the superseded
subject**. Those three receipts are retained, unbound, under
`_bmad-output/implementation-artifacts/evidence/story-3-15/superseded-acceptances/`.

A second 2026-08-25 review loop hardened the verifier further, and by the same rerun trigger
re-minted the subject again. That loop made the acceptance-source checks bind what they claimed to:
the retained comment's `id`, `url`, `html_url` anchor, and `issue_url` must now all resolve to one
comment on one issue (previously each was prefix-matched independently, so a receipt could splice a
comment id from one thread onto an anchor from another), and each rostered role is bound to exactly
one source kind. It also closed a disclaimer bypass — `authorizes nothing beyond deployment role identity`
satisfied the previous substring markers while asserting the opposite — normalized CRLF comment
bodies, made a date-only timestamp fail closed instead of crashing the verifier, and made the
assembler run the pinned verifier over its own output rather than always exiting zero. The
superseded `bb58d691` receipts were themselves anchored on issue `#346`, so they are now rejected
on lineage as well as on subject.

A third 2026-08-25 review loop bound the Test Architect source's lack of independent external
authentication as an exact limitation every receipt must repeat, made the owner GitHub account one
shared named identity, replaced the disclaimer regex with the retained exact non-authority sentence,
made every recomputed semantic check mutation-reachable, and added an explicit manifest override and
rerun trigger to failure output. Those verifier changes re-minted the subject once more.

The authorized completion pass then created dedicated Story 3.15 issue `#352`, retained its
Story-3.15-scoped MEMBER-authenticated roster comment, replaced the two-issue denylist with a single
positive issue allowlist, and required both owner receipts to resolve to that exact thread. Binding
the new handler and registry bytes re-minted the subject again.

A subsequent trusted-verifier review made both dispatchers execute only verified source bytes
under sanitized import resolution, rejected non-UTF-8 nuspec XML and non-integer smoke facts,
bounded all smoke-capture work by one per-platform monotonic deadline, and corrected the rerun
trigger to bind receipt-source **policy** changes. Those trusted-byte changes re-minted the subject
again and superseded every `dab64f5f...` receipt.

A sixth review loop landed as one authorized batch and re-minted the subject. It made
both packet producers -- the bounded smoke capture tool and the packet assembler -- bound decision
inputs in the closure `dispatch` block, so a producer edit can no longer change what a passing
Production smoke means while every receipt stays valid; it closed the schema of the retained GitHub
comment envelopes, so a stray unreviewed field can no longer persist inside the packet's only
external authentication artifacts with the subject unchanged; and it bound a fourth limitation
disclosing that every acceptance receipt is composed by repository tooling and posted with the
rostered role holder's credential rather than typed by hand. The same batch made cleanup bounded
rather than skipped when a platform budget is exhausted, made the capture refuse to overwrite a
populated `smokes/` directory without `--force`, restricted the nuspec DTD scan to the XML prolog,
and removed a post-import path assertion that was true by construction.

A seventh review loop landed at zero receipts, where a re-mint costs nothing, and re-minted the
subject once more. It closed a **fail-open regression the sixth loop introduced**: narrowing the
nuspec DTD scan to the XML prolog also made that scan return silently when the prolog did not begin
with `<`, and because `utf-8-sig` strips exactly one byte-order mark, a doubled BOM left a residual
U+FEFF that skipped the scan entirely -- a nuspec carrying `<!DOCTYPE ... <!ENTITY smuggle ...>` was
then accepted with the smuggled entity resolving into the package id. Every prolog exit is now
either "reached the document element" or a fail-closed reason, and a residual BOM is rejected.
The same loop closed a second regression in both dispatchers: adding `TypeError` to the
path-resolution catch had silenced a crash by making a bytes repository path answer "not
repository-local", so such a module escaped both displacement and the post-execution shadow check.
Paths are now decoded with `os.fsdecode` first. It also made the roster-configuration guard able to
fail (it had compared the identity table against strings interpolated from that same table), widened
the per-platform smoke window bound to the platform budget plus the cleanup allowance so the capture
tool can no longer emit records this verifier rejects, and bound the assembler to the bytes actually
executing rather than the pristine repository file.

The 2026-08-30 verifier and producer hardening re-minted the subject once more at zero receipts,
where no acceptance was burned. The packet's current subject is
`86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274`, and the packet **fails closed at
zero of three receipts**: each re-mint rejected the receipts collected against the prior subject by
the same rerun trigger, and collecting replacements on issue `#352` is an owner action outside this
repository. Until that happens deployed-runtime parity is **unavailable** and **no identity is
selected**. Reassembly reports `receipts=0 verifier_exit=1`. The `bb58d691...`, `dab64f5f...` and
`a8cc777e...` receipts and sources all remain byte-for-byte in the superseded audit area, whose
README carries the re-rooting rule an auditor needs to re-pair a superseded receipt with its source.
Five of the eight subjects never had receipts collected at all, so three retained sets against seven
re-mints is the expected shape, not a gap.

`closure.json` and `subject.json` carry `deployed_runtime_parity: "available"` and
`selected_deployed_identity`. Those two fields are the **claim** the three rostered roles are asked
to accept, not a granted verdict: the verifier grants them only at three of three, and at zero
receipts it exits 1 and grants nothing. `acceptances.directory` likewise names the address receipts
must occupy, not a directory that exists today.

The roster maps both owner roles to one authenticated human, `github:jpiquot`, while the Test
Architect record is explicitly self-attested without independent external authentication. Owner
comments `5409140199` and `5409147909` had timestamp mismatches, were immediately marked visibly
superseded, and are not retained in the packet.

Two facts are recorded rather than corrected. The retained roster comment names the ratified
artifact `reviewer-roster.json` -- wording copy-carried from Story 3.13 -- while the packet retains
`registry/owner-role-registry.json`; the reference is understood to mean that file, and correcting
it would need a new owner comment plus another re-mint. And the `linux/arm64` Production smoke
depends on QEMU user-mode emulation registered from
`tonistiigi/binfmt@sha256:400a4873b838d1b89194d982c45e5fb3cda4593fbfd7e08a02e76b03b21166f0`; that
registration is host state, not an input byte the packet can hash, so it is documented as an
environmental prerequisite in the capture script rather than bound into the subject.

The subject cannot bind each post-subject receipt-source instance without a hash cycle. It binds
the source policy instead: replacing one retained source invalidates that source's receipt and any
complete 3/3 verdict, while a source-policy change re-mints the subject and rejects all receipts.

The only identity the closure may ever select is
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`,
and it is selected only once parity is available. That verdict is evidence, not operational
authority: deployment, package publication, registry mutation, consumer removal, and predecessor
mutation remain separately prohibited.

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
