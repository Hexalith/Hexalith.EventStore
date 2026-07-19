---
baseline_commit: 9b03ee1b9890358984969e93c605caafff8b736d
created: 2026-07-19
story_id: "3.12"
story_key: 3-12-multi-platform-eventstore-container-publishing-correction
epic: "Epic 3 - Release And Repository Reliability"
requirements: FR22, FR25
governing_nfrs: NFR9, NFR11, NFR16, NFR17
architecture_decisions: AD-11, AD-12, AD-22
story_type: cross-repository-release-correction
source_files:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md
  - _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
  - _bmad-output/implementation-artifacts/3-10-generated-api-dapr-aspire-smoke-preflight.md
  - _bmad-output/implementation-artifacts/3-8-generated-api-dapr-aspire-smoke-preflight.md
  - _bmad-output/project-context.md
  - .github/workflows/release.yml
  - .releaserc.json
  - scripts/validate-release-secrets.sh
  - docs/ci.md
  - docs/ci-secrets-checklist.md
  - tools/release-packages.json
  - references/Hexalith.Builds/Github/publish-containers/publish-containers.sh
  - references/Hexalith.Builds/Github/publish-containers/action.yml
  - references/Hexalith.Builds/.github/workflows/domain-release.yml
  - references/Hexalith.Builds/.github/workflows/build-release.yml
---

# Story 3.12: Multi-Platform EventStore Container Publishing Correction

Status: ready-for-dev

<!-- Ultimate context engine analysis completed - comprehensive developer guide created. -->

> Authority gate: this story defines implementation and evidence requirements. It grants no
> authority to edit, commit, push, publish NuGet packages, mutate the registry, move submodule
> pointers, or approve Story 1.20. Obtain the separately named repository/release-owner authority
> before each such action.

## Story

As an **EventStore release owner**,
I want **the shared release path to publish an exact two-platform OCI index**,
so that **a corrective EventStore release can satisfy Story 1.20 Acceptance Boundary 8 without changing package scope or overwriting v3.75.0**.

## Story Context

Release `v3.75.0` published the exact 14 manifest-governed NuGet packages, but
`registry.hexalith.com/eventstore:3.75.0` resolves to one Docker image manifest for
`linux/amd64`. It is not the required OCI index and has no `linux/arm64` child. The release
workflow nevertheless completed successfully because the shared Hexalith.Builds publisher
hard-codes `--os linux --arch x64` and performs no exact post-publish index validation.

The correction belongs in the shared Hexalith.Builds publisher. EventStore remains a thin caller
with the one approved mapping
`src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore`. Do not duplicate shared container
publishing, OCI parsing, emulation setup, or evidence logic in the EventStore workflow.

Story 1.20 already contains useful executable design evidence for publication authority, raw
manifest binding, child-config inspection, and a bounded `/alive` container smoke. Extract and
harden that contract in the shared publisher rather than inventing an incompatible second model.
The proof-packet commands are design evidence, not proven implementation: correct their known
gaps, including exact index media-type validation, the .NET multi-RID property relationship,
smoking each child digest rather than the index digest, and explicit environment-vs-product
failure classification.

This story may proceed independently of backlog Stories 3.6 and 3.11. It must preserve Story
3.6's 14-package manifest boundary and must not absorb Story 3.11's dependency-refresh scope.
The approved 2026-07-19 proposal is intentionally additive to the PRD traceability tables; no PRD,
architecture, or UX change is required.

### Ownership and decision boundaries

- **Hexalith.Builds maintainer:** authorizes, owns, reviews, validates, and commits shared publisher
  implementation and tests; supplies the exact Builds commit used by the release.
- **EventStore developer:** integrates/verifies the thin caller, root guardrails and docs, preserves
  release inventory, and assembles observed evidence.
- **Test Architect:** reviews exact-platform, digest, child-config, negative-fixture, package-byte,
  and both-platform smoke evidence.
- **Release owner:** separately authorizes the externally visible corrective publication before
  the first NuGet or registry mutation, including the exact approved Builds execution identity,
  and later disposes the resulting artifact identity.
- **Story 1.20 owner:** independently revalidates the candidate and alone decides whether its
  approval fields and consumer-migration authorization may change.

## Acceptance Criteria

### AC1 - Preserve v3.75.0 as failed, non-authorizing evidence

**Given** v3.75.0 is inspected
**When** its release assets and container registry object are recorded
**Then** all 14 package hashes from the approved 2026-07-19 proposal are preserved as observed
non-authorizing evidence, the container digest/media type/config platform are recorded, and its
missing `linux/arm64` descriptor is an explicit failed result
**And** no v3.75.0 package, Git tag, container tag, manifest, or registry object is overwritten or
reclassified as approved.

### AC2 - Publish exactly two platforms as an OCI index

**Given** the Hexalith.Builds shared publisher is corrected under maintainer authority
**When** the EventStore container mapping is published for a new semantic version
**Then** .NET SDK container support produces immutable `linux/amd64` and `linux/arm64` children and
the version tag resolves to an OCI image index with media type
`application/vnd.oci.image.index.v1+json`
**And** the index contains exactly one descriptor for each required platform, with no duplicate,
extra, `unknown/unknown`, or variant descriptor.

### AC3 - Validate immutable registry bytes and children fail-closed

**Given** the published index and child manifests are inspected
**When** release validation runs against immutable digests
**Then** the raw index bytes hash to the registry-reported index digest, every descriptor digest
resolves, and each child config's `os`/`architecture` equals its descriptor
**And** a single-platform manifest, wrong media type, missing/duplicate/extra platform, config
mismatch, unresolved child, or digest mismatch fails the release evidence gate.

### AC4 - Smoke both immutable child digests equivalently

**Given** the two immutable child digests are runnable
**When** platform smoke validation executes
**Then** both `linux/amd64` and `linux/arm64` variants start successfully and pass the same bounded
support-safe EventStore health smoke
**And** emulation/setup failure is reported separately from a product image failure and cannot be
recorded as a pass.

### AC5 - Require separate durable publication authority

**Given** a corrective release is ready for external publication
**When** the publish action is requested
**Then** a separate durable release-owner authority record binds the repository, new version,
source SHA, exact container repository, two-platform scope, owner, date, rationale, and validity
window before registry or NuGet mutation occurs
**And** this planning/story approval alone grants no registry, NuGet, commit, branch, submodule, or
push authority.

### AC6 - Assemble one coherent corrective-release identity

**Given** the corrective semantic release completes
**When** its evidence is assembled
**Then** exactly the 14 IDs in `tools/release-packages.json` share the new version and have
independently verified SHA-256 values, while the container evidence records repository, index
digest, raw-index hash, child digests/configs, exact platform set, source SHA, workflow run, and
smoke results
**And** the package inventory remains 14, only `eventstore` is published as a container, and
package/container version provenance is coherent.

### AC7 - Handoff without approval leakage

**Given** Story 3.12 has produced a conforming release
**When** its handoff reaches Story 1.20
**Then** Story 1.20 independently revalidates every package/container identity, remaining
production-path result, approval, and A/B/C authorization gate before selecting approved fields or
authorizing migration
**And** Story 3.12 does not modify Parties or Tenants, approve G5, authorize consumer migration, or
mark Story 1.20/Epic 1 done.

## Tasks / Subtasks

- [x] **Task 1 - Reconfirm repositories, baseline, scope, and authority (AC1-AC7).**
  - [x] Re-read root and Hexalith.Builds tracked guidance, `.editorconfig`, `.gitattributes`,
    workflow/configuration files, current branches, remotes, worktrees, and recent history before
    editing; preserve every user change made after this story baseline.
  - [x] Confirm EventStore starts at or after `9b03ee1b9890358984969e93c605caafff8b736d` and record
    the exact live EventStore SHA and `references/Hexalith.Builds` SHA used for implementation.
  - [x] Obtain Hexalith.Builds maintainer authority before editing or committing the shared
    publisher. If absent, stop at read-only analysis; do not implement a local EventStore copy.
  - [x] Treat commit, push, gitlink update, NuGet publication, registry mutation, and Story 1.20
    disposition as separate authority-bearing actions. Do not infer one from another.
  - [x] Do not initialize/update nested submodules, create a Dockerfile, change dependencies,
    broaden package/container scope, modify Parties/Tenants, or stage/commit/push unless separately
    authorized.

- [x] **Task 2 - Freeze v3.75.0 as a regression fixture and historical failure (AC1, AC3).**
  - [x] Preserve the exact release/source/tag identities, package byte hashes, container digest,
    raw-manifest hash, media type, config digest/platform, and missing-arm64 result already recorded
    in the approved proposal and Story 1.20 proof packet.
  - [x] Add a deterministic single-manifest fixture representing the observed v3.75.0 shape and
    prove the new validator rejects it as `single-platform`/`wrong-index-media-type`.
  - [x] Keep the historical version tag and object immutable. Never use `--skip-duplicate`, tag
    replacement, or registry mutation to present v3.75.0 as corrected.
  - [x] Keep every Story 1.20 `approved_*` field null, `final_decision: still blocked`, and
    `authorize_consumer_migration: false` during this story's observed-evidence handoff.

- [ ] **Task 3 - Correct the Hexalith.Builds publisher with native .NET multi-RID support (AC2).**
  - [ ] In the Builds-owned `Github/publish-containers` action/helper, preserve the existing
    `project.csproj|repository` mapping, SemVer checks, registry/repository validation, and
    semantic-release invocation contract.
  - [ ] Replace the single-RID `--os linux --arch x64` publish with exactly two .NET SDK container
    RIDs that produce `linux/amd64` and `linux/arm64`. Evaluate the planning baseline
    `linux-x64;linux-arm64` against the configured Alpine base and any native assets; if the
    portable Alpine RIDs `linux-musl-x64;linux-musl-arm64` are required, record and test that
    decision without changing the external platform contract.
  - [ ] Pass the chosen set, quoted as one shell argument, through both `RuntimeIdentifiers` and
    `ContainerRuntimeIdentifiers`; do not also set one single `RuntimeIdentifier`, `--arch`, or
    another property that collapses the operation back to one architecture. Force
    `ContainerImageFormat=OCI` for an explicit, testable outer-index contract.
  - [ ] Keep `UseHexalithProjectReferences=false`, Release configuration, the new semantic version,
    `registry.hexalith.com`, repository `eventstore`, SDK container support, alpine base family,
    non-root `app`, port 8080, and existing OCI labels intact.
  - [ ] Let the .NET SDK build the multi-architecture image/index. Do not introduce Dockerfiles or a
    second hand-rolled image build pipeline when the pinned SDK already supplies the capability.
  - [ ] Ensure a mapping succeeds only after its immutable published object passes Tasks 4 and 5;
    successful `dotnet publish` or tag existence alone is not release success.

- [ ] **Task 4 - Add a fixture-driven, exact-set OCI validator (AC2, AC3).**
  - [ ] Put reusable validation in Hexalith.Builds beside the shared publisher; do not bury it in
    Story 1.20 or duplicate it in EventStore.
  - [ ] Resolve the version tag with an explicit OCI-index `Accept` header, validate HTTP
    `Content-Type` against the manifest `mediaType`, capture `Docker-Content-Digest`, then GET the
    same object by immutable digest. Require identical untouched bytes and verify
    `sha256:<hex>` against those exact bytes before parsing.
  - [ ] Require top-level `schemaVersion: 2` plus
    `application/vnd.oci.image.index.v1+json`.
  - [ ] Require exactly two descriptors and the exact set `linux/amd64`, `linux/arm64`; reject blank
    platform fields, variants, duplicates, extras, and `unknown/unknown`. Accept only recognized
    OCI/Docker child-manifest media types whose descriptor and response content type agree; do not
    require OCI child manifests when the exact outer object is the required OCI index.
  - [ ] Resolve each descriptor by digest, bind each raw child-manifest hash and byte length to its
    descriptor digest/size, resolve its config blob, bind config digest/size, and require config
    `os`/`architecture` to equal the descriptor.
  - [ ] Make failures deterministic and support-safe: reason codes may identify platform/media-type
    and digest class, but output must not expose registry credentials, authorization headers, raw
    tokens, or unrelated environment detail.
  - [ ] Add a positive exact-two-platform fixture and negative fixtures for single manifest,
    missing/duplicate/extra/unknown/variant platform, wrong top-level media type, tag/digest body
    disagreement, descriptor/config size mismatch, child config mismatch, unresolved child,
    malformed descriptor, and raw digest mismatch.
  - [ ] Stub registry/tool boundaries in contract tests so negative coverage performs no network,
    registry, NuGet, Git, or Docker mutation.

- [ ] **Task 5 - Make both-platform smoke bounded, digest-pinned, and diagnostically honest (AC4).**
  - [ ] Reuse the existing EventStore liveness contract: start the published child on a loopback
    ephemeral host port with `ASPNETCORE_URLS=http://+:8080` and poll `/alive` with the same bounded
    timeout/retry policy for both platforms.
  - [ ] Resolve the descriptor for each platform and run `repository@<child-digest>`; do not smoke
    the mutable version tag or rely on platform selection from the parent index digest.
  - [ ] Add an arm64 emulation/runtime preflight before product smoke. If shared CI needs a QEMU or
    binfmt setup action, keep it in Hexalith.Builds and SHA-pin the third-party action per FR25.
  - [ ] Emit distinct outcomes for `environment/emulation-setup-failure`, `image-start-failure`,
    `liveness-timeout`, and `pass`. Only an actual `/alive` pass for each child is success.
  - [ ] Preserve the earliest causal blocker when later checks also fail; a later product error must
    not mask an existing registry/authentication/emulation environment failure.
  - [ ] Always remove temporary containers/files, keep logs support-safe, and preserve both smoke
    logs and hashes in the evidence bundle.

- [ ] **Task 6 - Gate the first irreversible action on durable release-owner authority (AC5).**
  - [ ] Reuse the Story 1.20 publication-authority semantics rather than inventing an informal
    approval note. The frozen record must bind the repository, proposed semantic version,
    workflow/source SHA, exact `registry.hexalith.com/eventstore` repository, exact platforms,
    exact maintainer-approved Builds reusable-workflow/action/helper identities, named owner,
    authorization time, durable source, rationale, and unexpired validity window.
  - [ ] Before mutation, prove the called Builds workflow, nested `publish-containers` action, and
    installed helper bytes/revisions are the exact maintainer-approved identities in the authority
    record. Because repository policy uses mutable `@main`, do not infer this from the EventStore
    gitlink or current remote branch tip and do not allow independently resolved workflow/action
    revisions to drift. If exact pre-publication binding cannot be established, fail closed.
  - [ ] Distinguish the workflow/source (tag-parent) SHA from the semantic-release tag commit; record
    both after release so provenance cannot repeat the v3.75.0 identity ambiguity.
  - [ ] Preflight that the proposed version is absent for every one of the 14 destination NuGet
    package IDs and that the exact container version tag is absent. Remove `--skip-duplicate` from
    the publication path: any pre-existing package or tag is a version collision, not permission
    to combine or overwrite release bytes.
  - [ ] Revalidate the frozen authority at an action-time timestamp immediately before the first
    publication-capable step. Missing, malformed, mismatched, expired, wrong-role, or changed bytes
    must fail before `dotnet nuget push` and before any registry write.
  - [ ] Make the semantic-release publish ordering prove that a full authority preflight—not merely
    secret presence—precedes NuGet publication. Preserve secret preflight and never echo secrets.
  - [ ] Record the authority bytes, durable-source metadata, hashes, and checked-at value in
    provenance. This validates authority evidence; it does not create human authority.

- [ ] **Task 7 - Integrate the thin EventStore caller and strengthen guardrails/docs (AC2, AC5, AC6).**
  - [ ] Preserve `.github/workflows/release.yml` as a thin `domain-release.yml@main` caller and keep
    exactly the existing `eventstore` mapping. Add only inputs/secrets/outputs genuinely required by
    the corrected shared contract.
  - [ ] Update `.releaserc.json`, `scripts/validate-release-secrets.sh`, or shared helper phases as
    needed so the authority gate runs before NuGet and the validated publisher runs for the same
    `${nextRelease.version}`. Do not duplicate OCI validation in JSON command strings.
  - [ ] Extend EventStore release-governance tests (prefer a focused container-publishing test file
    if adding substantial logic) to prove thin-caller ownership, one mapping, authority-before-
    mutation ordering, explicit three-secret mapping with no `secrets: inherit`, exact platform
    contract, and rejection of a stale single-platform shared publisher contract. Parse mapping
    entries and ordering structurally; substring presence alone is insufficient.
  - [ ] Update `docs/ci.md` with the exact two-platform contract, immutable registry inspection,
    child-config checks, `/alive` smoke, environment-vs-product classification, evidence fields,
    and publication/Story 1.20 authority boundaries. Include no credentials or private evidence.
  - [ ] Reconcile the already-stale `docs/ci-secrets-checklist.md` with all three explicitly mapped
    release secrets (`NUGET_API_KEY`, `HEXALITH_ZOT_USERNAME`, `HEXALITH_ZOT_API_KEY`), including
    inventory/count, release use, onboarding, ownership, and rotation. Add authority inputs only if
    the corrected contract requires them; credential modernization remains out of scope.
  - [ ] Preserve `tools/release-packages.json` at exactly 14 IDs and preserve `Directory.Build.targets`
    plus the EventStore project container repository/base/user/port/label behavior.

- [ ] **Task 8 - Validate implementation without external mutation (AC1-AC5).**
  - [ ] Run shell syntax and shared publisher contract/fixture tests with fake executables/fixture
    bytes; prove every negative case exits nonzero with its expected support-safe classification.
  - [ ] Run Hexalith.Builds workflow/action contract checks and its repository-required focused
    build/tests before recording the exact Builds commit. Wire the publisher suite into the Builds
    pre-release validation path (currently `.github/workflows/build-release.yml`) so it cannot be
    omitted from a Builds release.
  - [ ] Run EventStore release-governance tests by project, then restore/build
    `Hexalith.EventStore.slnx` in Release/package mode. Do not run solution-level `dotnet test`.
  - [ ] Run package pack/validation/consumer checks into a fresh temporary output and prove the
    exact 14-ID inventory without publishing it.
  - [ ] Run workflow/docs/mapping scans, `git diff --check`, and separate worktree/status checks in
    both repositories. Record every command/result and any environmental blocker exactly.

- [ ] **Task 9 - Publish and inspect a new corrective version only after explicit authority (AC2-AC6).**
  - [ ] Stop and obtain the separate durable release-owner authority described in AC5. Story
    approval or successful local tests are insufficient.
  - [ ] Publish a new semantic-release-generated version from the authorized source SHA; do not
    mutate or reuse any v3.75.0 package/tag/manifest/registry object.
  - [ ] If any external mutation partially succeeds and the release later fails, preserve and
    quarantine that failed identity as non-authorizing evidence. Never retry the same semantic
    version, skip existing packages, or repoint its container tag; obtain new authority for a fresh
    version after the defect/environment is corrected.
  - [ ] Capture the workflow run and attempt, resolved Hexalith.Builds reusable-workflow/action
    commit identity, source SHA, release tag commit/parent, release URL, 14 package
    files/versions/sizes/hashes, container
    repository/index digest/raw bytes/hash, child descriptors/manifests/configs, exact platforms,
    and both child-digest smoke results.
  - [ ] Independently download and hash all package assets. Require one new version across exactly
    the manifest IDs and verify package/container provenance maps to the same authorized release.
  - [ ] Treat publication, inspection, emulation, product, or evidence failure as non-authorizing;
    do not hide partial publication or record a failed release as pass.

- [ ] **Task 10 - Hand observed evidence to Story 1.20 without deciding it (AC6, AC7).**
  - [ ] Add the corrective release identity and immutable evidence to the Story 1.20 packet as
    observed candidate data first; preserve every independent production-path, reviewer, owner,
    A/B/C, and migration gate.
  - [ ] Do not populate `approved_*`, change `final_decision`, authorize migration, modify Parties or
    Tenants, approve G5, or mark Story 1.20/Epic 1 done under Story 3.12 authority.
  - [ ] Route the packet to the EventStore and release owners for their independent verification and
    durable disposition. Record limitations and failed/blocked gates without optimistic inference.

## Dev Notes

### Current implementation state and required preservation

| File / area | Current state | Story change | Must preserve |
| --- | --- | --- | --- |
| `references/Hexalith.Builds/Github/publish-containers/publish-containers.sh` | Validates version/mapping/credentials, logs into Zot, then publishes one `linux/x64` image via `--os linux --arch x64`; no index/smoke validation. | **UPDATE in Builds:** native exact two-RID publish, authority-aware phases, immutable validator and smoke orchestration. | Mapping grammar, semantic version, registry/repository/tag inputs, support-safe output, SDK container support. |
| `references/Hexalith.Builds/Github/publish-containers/action.yml` | Validates nonblank mappings and installs the version-matched helper into the caller workspace. | **UPDATE/VERIFY in Builds:** expose only contract inputs/outputs needed for authority, validation, emulation, and evidence. | Helper remains shipped with the resolved shared action; caller submodule contents are not the executed helper. |
| `references/Hexalith.Builds/Github/publish-containers/README.md` | Documents one SDK publish per mapping but no platform/index guarantee. | **UPDATE in Builds:** exact platforms, failure contract, authority, inspection, smoke, and evidence. | Shared ownership and mapping documentation. |
| New Builds publisher validator/tests/fixtures | No exact-set OCI validator or publisher fixture suite exists. | **ADD in Builds:** reusable immutable-byte validator, fake-tool workflow tests, positive/negative registry fixtures, and bounded child-digest smoke coverage. | Keep network/mutation outside fixture lanes; follow existing `Tools/test-*.ps1` and `test/fixtures` conventions or a maintainer-approved equivalent. |
| `references/Hexalith.Builds/.github/workflows/domain-release.yml` and `.md` | Installs the helper before `npx semantic-release`; declares registry credentials; no arm64 emulation/evidence phase. | **UPDATE in Builds:** required setup, fail-closed inputs/outputs, authority and evidence contract. | Full-history checkout, root-only submodules, package-mode Release build, pinned third-party actions, semantic-release ownership. |
| `references/Hexalith.Builds/.github/workflows/build-release.yml` | Current Builds pre-release validation has no exact multi-platform publisher suite. | **UPDATE in Builds:** run the fixture/fake-tool publisher contract suite before a Builds release. | Existing validation/build/release ordering and required checks. |
| `.github/workflows/release.yml` | Thin `domain-release.yml@main` caller with one `eventstore` mapping. | **VERIFY; UPDATE only if shared contract requires explicit authority/evidence wiring.** | Thin caller, `@main` policy, CI-head equality, one container mapping, explicit secrets. |
| `.releaserc.json`, `scripts/validate-release-secrets.sh`, and focused authority/destination validators as needed | Secret/helper checks precede NuGet; NuGet currently uses `--skip-duplicate`, then precedes the single-platform helper. | **UPDATE/ADD/VERIFY:** complete authority, approved-Builds-identity, and destination-absence preflight must precede the first NuGet/registry mutation; remove duplicate skipping; corrected publisher uses the same semantic version. Keep durable-record parsing out of `.releaserc.json` command strings. | Manifest pack/validate prepare phase, secret safety, and scoped NuGet package glob. |
| `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` | Verifies 14 packages, thin shared caller, approved mapping, `NUGET_API_KEY`, and publish ordering; it does not assert both Zot secrets or prove multi-platform registry behavior. | **UPDATE or complement with NEW focused test:** assert the corrected shared contract and ordering without duplicating shared validator internals. | Existing manifest, lane, shared-caller, and package-governance coverage. |
| `docs/ci.md` | Describes 14 packages and one EventStore mapping but does not promise/verify an exact OCI index. | **UPDATE:** document the exact contract and evidence/authority boundary. | Shared-workflow ownership, lane separation, package manifest, supply-chain backlog exclusions. |
| `docs/ci-secrets-checklist.md` | Stale: lists only `NUGET_API_KEY` for releases although the caller also maps two Zot credentials. | **UPDATE:** reconcile all three release secrets throughout inventory/count, release use, onboarding, ownership, and rotation; include any new authority input if applicable. | Explicit least-privilege secret mapping; no credential-modernization scope. |
| `tools/release-packages.json` | Exact 14-package source of truth. | **PRESERVE.** | No package addition/removal/reordering as part of this correction. |
| `Directory.Build.targets` and `src/Hexalith.EventStore/Hexalith.EventStore.csproj` | SDK container support, `aspnet:10.0-alpine`, `app`, port 8080, OCI labels, repository `eventstore`. | **VERIFY/PRESERVE.** | No Dockerfile, runtime feature, new repository, or container mapping. |
| Story 1.20 proof packet | Holds v3.75.0 non-authorizing evidence and reusable draft publication/smoke contracts; approval fields are null. | **UPDATE only with observed corrective evidence after authorized publication.** | Independent review, `approved_*`, final decision, and A/B/C authority gates. |

New Builds test/fixture paths should follow the owning repository's existing `Tools/test-*.ps1`
and `test/fixtures` conventions, or another maintainer-approved equivalent. Add new files to
`Hexalith.Builds.slnx` when that solution explicitly tracks their class of artifact.

### Historical v3.75.0 evidence baseline

- Workflow run: `29683837428`; release `v3.75.0`, published `2026-07-19T10:47:27Z`.
- Workflow head/tag parent: `d046120fb4178252a0e0f300d714bac70018e9f7`.
- Release tag commit: `67a9e00efaf397a31669f65df7008f671d20e06a`.
- Container: `registry.hexalith.com/eventstore:3.75.0`.
- Digest/raw SHA-256: `sha256:1cb7b6ed3db986e9896cc36f360d14017f3bf3521eed8e33267c3dffd05ca253`.
- Media type: `application/vnd.docker.distribution.manifest.v2+json` (failure).
- Config digest: `sha256:9623a1d61f39b21ab28948e903669a54b38ab500c190dc7e7c9889744209a6e9`.
- Config platform: `linux/amd64`; required `linux/arm64` is absent.
The historical package-byte fixture is exact; any mismatch is a regression in evidence handling:

| Package ID | Size | SHA-256 |
| --- | ---: | --- |
| `Hexalith.EventStore.Contracts` | 143191 | `501485bacf3d76e847d1e162d6b51a72816a89ed2f167c56df19eb2c9eb96cf3` |
| `Hexalith.EventStore.Client` | 146237 | `5e592616dd180c8cc8b51facc0eb2dc06d0a33b7c426a9f5d9012739d7d2eb23` |
| `Hexalith.EventStore.Server` | 329419 | `a6510fb61cad2ab4d933d19d948cc5ca847edfc1e86bff342407bfb6d5acf8e3` |
| `Hexalith.EventStore.SignalR` | 21155 | `85493eefbc9c0a302f8e6b956d0e0c942c97b74d23599492782530a105725a56` |
| `Hexalith.EventStore.Testing` | 52308 | `9fd627c9cfc2114f058eeb63cfbea80735bef8b1a38771912b21af3eed476a49` |
| `Hexalith.EventStore.Testing.Integration` | 63949 | `c9b7d6e19fa804a4e880ec1ac244fed41da576e0fac42552ea061bf5b50acbc4` |
| `Hexalith.EventStore.Aspire` | 25663 | `cd7f42446536b47856e72429821ce53593e4601c16067c9d8ea3b6490b7329e1` |
| `Hexalith.EventStore.ServiceDefaults` | 16207 | `823ebba8870caa13dd8b64801a759fd0a65ec6a2f19def60c94e592f247e8313` |
| `Hexalith.EventStore.DomainService` | 51821 | `d41937ce8d9a4890d2e97a8e09d09b3c56cc46b24666216e26d6055296905050` |
| `Hexalith.EventStore.RestApi.Generators` | 36992 | `e5fa1eb0bd09e2c2e2321781aca81e0dd9f846e00d349634b194f7f43eed77dc` |
| `Hexalith.EventStore.Gateway` | 230221 | `3700b8cb46a34ed2f9d3f2664a571f07a5a1f70423df70967926cea86f39968c` |
| `Hexalith.EventStore.Admin.Abstractions` | 111743 | `345fe452dda39fbc3513778936375da03b0be5c9ef2aee858f28deb1756b1be5` |
| `Hexalith.EventStore.Admin.Cli` | 652071 | `fb2ac8c69da9790a9ff46237cb1db8958feaf04a59d340375094dc81ac503aad` |
| `Hexalith.EventStore.Admin.Server` | 173425 | `e88f69d1bbd840ae9fa29bf710dcc71cc106c5cbc8101a59dc5552887c7a7a67` |

All rows are version `3.75.0`; the table is copied from the approved proposal so the developer can
build a deterministic fixture without inferring or re-fetching mutable evidence.

### Technical requirements and anti-pattern guards

- Pinned repository SDK: .NET `10.0.302`, `rollForward: latestPatch`; target `net10.0`.
- .NET multi-architecture publishing uses a quoted, semicolon-delimited RID set passed through both
  `RuntimeIdentifiers` and `ContainerRuntimeIdentifiers`. The planning baseline is
  `linux-x64;linux-arm64`, but this project uses an Alpine base; deliberately test whether portable
  `linux-musl-x64;linux-musl-arm64` is required by native assets. Either internal RID set must yield
  the external exact platforms `linux/amd64` and `linux/arm64`. Multiple RIDs with no single RID
  produce an image index; set `ContainerImageFormat=OCI` to make its outer media-type contract
  explicit.
- Do not retain `--arch x64`, add one single `RuntimeIdentifier`, or mix single/multi-RID inputs.
- The registry's immutable raw bytes are authoritative. An SDK-generated index capture is useful
  diagnostic evidence but cannot replace post-publish registry read-back and digest verification.
- Validate child descriptors/configs and smoke child digests. A tag, index digest selected by
  `--platform`, workflow success, or `dotnet publish` exit code is insufficient child proof.
- The exact allowed descriptor set is a product/release contract stricter than the general OCI
  specification: precisely `linux/amd64` and `linux/arm64`, no variants or auxiliary descriptors.
- Require the OCI media type for the outer index, but accept recognized OCI or Docker v2 child
  manifest media types when descriptor, response, digest, size, and config bindings all agree.
- Never log credentials, auth headers, tokens, raw approval content beyond the frozen support-safe
  record, private endpoints, or unbounded container logs.
- Treat version uniqueness as a release invariant. Pre-existing destination packages or a container
  tag fail preflight; never use duplicate skipping, tag replacement, or a same-version retry to
  manufacture one identity from different publication attempts.
- Keep source/package release mode package-safe (`UseHexalithProjectReferences=false`) and rerun
  restore before any `--no-restore` Release publish after changing mode or RIDs.
- Preserve supply-chain backlog boundaries: no trusted publishing, signing, SBOM, attestation,
  general provenance platform, or credential-modernization work.

### Testing requirements

- **Shared unit/contract lane:** pure fixture validation of index media type, raw digest, exact
  descriptor set, child resolution/config matching, reason codes, mapping parsing, and preflight
  authority. No network or mutation.
- **Shared workflow lane:** fake `dotnet`, Docker/registry-inspection, and smoke tools prove command
  construction, preflight ordering, cleanup, evidence capture, and fail-closed propagation.
- **EventStore guardrails:** project-scoped xUnit v3 + Shouldly tests keep the root release caller
  thin, preserve one mapping/14 packages, and prove authority-before-NuGet ordering.
- **Authorized integration evidence:** registry read-back by index and child digest plus two bounded
  `/alive` smokes. A missing arm64 execution environment is `blocked`, never `pass`; it must be
  distinguished from a product image failure.
- Run test projects individually. Use `Hexalith.EventStore.slnx` and `Hexalith.Builds.slnx` only for
  restore/build; never solution-level `dotnet test` and never create `.sln` files.
- New C# test methods use PascalCase, Shouldly assertions, file-scoped namespaces, Allman braces,
  and one C# type per file. For shell/Markdown line endings, follow each owning repository's tracked
  configuration and the convention of adjacent files.

### Previous Story Intelligence

Story 3.10 is the highest existing Epic 3 story file below 3.12. It is a completed historical
reissue wrapper; the substantive predecessor record is the superseded Story 3.8 preflight. Reuse
its evidence rules: classify environment, topology, and sidecar/emulation blockers separately from
product failures; preserve the earliest causal blocker; keep diagnostics support-safe; and require
the actual verdict path and end-state evidence rather than status or command construction alone.
When parsing JSON, do not use fallback expressions that turn an explicit boolean `false` into a
missing/default value. Validate option values, exact sets, cleanup traps, and the completeness of
every evidence file list. Neither Story 3.10 nor 3.8 provides container-publishing implementation,
and neither must be reopened.

Stories 3.7 and 3.8 already established thin shared callers, one approved EventStore container
mapping, exact package inventory, and pre-mutation secret/identity safety. Extend those patterns in
Hexalith.Builds instead of adding local workflow steps. Story 3.9 keeps broader supply-chain work
out of this correction.

### Git intelligence

- `9b03ee1b` approved and added Story 3.12 planning, preserved v3.75.0 evidence, and advanced root
  submodule pointers. It did not create this implementation story or fix the publisher.
- `3111a4bc` added the Story 1.20 multi-platform proof blueprint. Reuse its evidence semantics, but
  do not depend on its internal SDK target/hook names as a stable product API.
- `ea5a1207` added the current release-secret preflight; extend its fail-before-mutation pattern to
  full durable publication authority.
- `12baa75c` established the shared workflow migration and single approved EventStore mapping;
  preserve that ownership boundary.
- `496887e7` and `d046120f` reinforced strict commitlint/CI evidence; preserve shared validation and
  do not bypass hooks.
- `67a9e00e` is the v3.75.0 semantic-release tag commit; its parent/source is `d046120f`, so release
  evidence must distinguish tag commit from source SHA.
- Builds commit `5b726e0c` extracted the current publisher whose helper hard-codes one architecture;
  correct that shared implementation rather than reintroducing local publishing.
- Current inspected Builds baseline is `4bbe7c04eb901050ee84075f0c8ad225fcc5fefe`; re-read the live
  submodule before implementation because the reusable workflow/action is resolved from `@main`.
  Record the actual remote commit resolved by the release run and its attempt; the caller's local
  gitlink alone is not executed-action identity.
- Work and validate inside the repository owning each change. If commits are authorized, commit
  Builds first and update the EventStore gitlink separately; never bundle unrelated submodule dirt.

### Latest technical information

- Microsoft documents native multi-architecture SDK publishing via
  `ContainerRuntimeIdentifiers`; the values must be a subset of `RuntimeIdentifiers`, and a
  multi-RID `/t:PublishContainer` operation creates an OCI image index. Multi-architecture output
  is OCI. Source: https://learn.microsoft.com/en-us/dotnet/core/containers/publish-configuration
- The pinned SDK implementation can be inspected at
  https://github.com/dotnet/sdk/blob/v10.0.302/src/Containers/packaging/build/Microsoft.NET.Build.Containers.targets;
  treat public documented properties as the contract and do not bind the publisher to internal
  target names or generated-file hooks.
- The portable RID catalog distinguishes `linux-*` from `linux-musl-*`, and Microsoft's Alpine
  support matrix is the check for Alpine-native compatibility. Sources:
  https://learn.microsoft.com/en-us/dotnet/core/rid-catalog and
  https://learn.microsoft.com/en-us/dotnet/core/install/linux-alpine
- The OCI Image Index specification defines media type
  `application/vnd.oci.image.index.v1+json`, a required `manifests` array, and per-descriptor
  platform fields. Story 3.12 deliberately applies an exact-set rule stricter than the general
  spec. Source: https://github.com/opencontainers/image-spec/blob/main/image-index.md
- OCI descriptor and image-config contracts supply the digest, size, media type, and platform
  bindings that Task 4 must verify. Sources:
  https://github.com/opencontainers/image-spec/blob/main/descriptor.md and
  https://github.com/opencontainers/image-spec/blob/main/config.md
- The OCI Distribution Specification requires manifest retrieval by tag or digest, preserves the
  uploaded manifest's exact bytes, and exposes `Docker-Content-Digest`; clients using the digest
  must verify it against returned content. Source:
  https://github.com/opencontainers/distribution-spec/blob/main/spec.md
- These sources explain the mechanism; repository pins and this story's exact platform/evidence
  contract remain authoritative. Do not upgrade the SDK or introduce another tool solely because a
  newer document exists.

### Suggested non-mutating validation commands

Adapt exact paths to the implementation chosen in Hexalith.Builds and record every result:

```bash
bash -n references/Hexalith.Builds/Github/publish-containers/publish-containers.sh
dotnet restore Hexalith.EventStore.slnx -p:UseHexalithProjectReferences=false
dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -warnaserror
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release --no-restore
python3 scripts/pack-release-packages.py /tmp/hexalith-eventstore-story-3-12-packages 0.0.0-ci-test
python3 scripts/validate-nuget-packages.py /tmp/hexalith-eventstore-story-3-12-packages
python3 scripts/validate-consumer-package-references.py /tmp/hexalith-eventstore-story-3-12-packages
git diff --check
```

Use the repository's xUnit v3/Microsoft.Testing.Platform convention for a focused class or method;
do not assume `dotnet test --filter` behaves like VSTest. Use a fresh temporary package directory
and do not reuse `/tmp/hexalith-eventstore-story-3-12-packages` if it contains prior output.

### Project Structure Notes

- Shared release behavior belongs in `references/Hexalith.Builds` under its own maintainer and Git
  history. EventStore owns thin caller inputs, local governance tests/docs, manifest identity, and
  observed release evidence.
- No UI/UX file is affected. No DAPR/Aspire runtime topology, application endpoint, package API,
  EventStore runtime code, consumer module, or deployed configuration changes.
- Historical planning artifacts remain historical. Update the active proof packet only as required
  for observed evidence; do not rewrite the approved proposal or v3.75.0 release record.
- The Story 1.20 packet's inline scripts are a source of contract knowledge, not the final shared
  implementation location.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-312-Multi-Platform-EventStore-Container-Publishing-Correction]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md#1-Issue-Summary]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md#42-New-Epic-3-Story]
- [Source: _bmad-output/planning-artifacts/prd.md#63-Release-And-Repository-Reliability]
- [Source: _bmad-output/planning-artifacts/prd.md#7-Cross-Cutting-Non-Functional-Requirements]
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-11---Release-Is-Manifest-Governed-ADOPTED]
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-12---High-Risk-Verification-Requires-Persisted-Evidence-ADOPTED]
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-22---Consumer-Infrastructure-Removal-Requires-Owner-Approved-Exact-SHA-Parity-ADOPTED]
- [Source: _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md#Observed-v3750-non-authorizing-container-failure]
- [Source: _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md#Cross-Cutting-Compatibility-And-Release-Gate]
- [Source: _bmad-output/implementation-artifacts/3-10-generated-api-dapr-aspire-smoke-preflight.md#Strengthened-Evidence-Rule]
- [Source: _bmad-output/implementation-artifacts/3-8-generated-api-dapr-aspire-smoke-preflight.md]
- [Source: docs/ci.md#Release-Flow]
- [Source: docs/ci-secrets-checklist.md]
- [Source: references/Hexalith.Builds/Github/publish-containers/publish-containers.sh]
- [Source: references/Hexalith.Builds/.github/workflows/domain-release.yml]
- [Source: references/Hexalith.Builds/.github/workflows/build-release.yml]
- [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules]

## Dev Agent Record

### Agent Model Used

_To be completed by the implementing dev agent._

### Debug Log References

- 2026-07-19 Task 1 read-only gate: EventStore `main`/`origin/main`/live remote
  `main` resolve to `80fa4476460958b625cc9ad4f9be5cec252a83af`; baseline
  `9b03ee1b9890358984969e93c605caafff8b736d` is an ancestor.
- 2026-07-19 Task 1 read-only gate: the EventStore gitlink, clean local
  Hexalith.Builds checkout, local `origin/main`, and live Hexalith.Builds remote
  `main` all resolve to `4bbe7c04eb901050ee84075f0c8ad225fcc5fefe`.
- 2026-07-19 HALT: no separate Hexalith.Builds maintainer authority was provided
  or found. The approved planning/story record explicitly grants no shared
  publisher edit or commit authority, so implementation stopped before editing
  Hexalith.Builds or EventStore release files.
- 2026-07-19 authority resumed: `jpiquot`, acting as a Hexalith.Builds
  maintainer, authorized uncommitted shared-publisher implementation edits and
  local validation at Builds SHA
  `4bbe7c04eb901050ee84075f0c8ad225fcc5fefe`; commits, pushes, publication,
  release execution, registry/NuGet mutation, and Story 1.20 disposition remain
  explicitly unauthorized.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Task 1 was initially blocked at its mandatory authority gate; before authority
  was supplied, no code, workflow, documentation, dependency, submodule,
  package, registry, branch, commit, push, or Story 1.20 disposition action was
  performed.
- Task 1 completed after named Hexalith.Builds maintainer edit/test authority was
  supplied. Repository identity, clean shared worktree, scope exclusions, and
  separate irreversible-action gates were verified.
- Task 2 froze the exact v3.75.0 identities and 14 package hashes as
  non-authorizing fixture evidence. Focused tests prove its deterministic Docker
  single-manifest shape fails the new OCI validator as
  `wrong-index-media-type`; the historical release and Story 1.20 packet were
  not mutated.

### File List

- `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py`
- `references/Hexalith.Builds/Github/publish-containers/tests/test_oci_registry_validator.py`
- `references/Hexalith.Builds/Tools/test-publish-containers.ps1`
- `references/Hexalith.Builds/test/fixtures/publish-containers/v3.75.0-evidence.json`
- `references/Hexalith.Builds/test/fixtures/publish-containers/v3.75.0-single-manifest.json`
- `references/Hexalith.Builds/test/fixtures/publish-containers/validation-cases.json`
