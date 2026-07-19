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

Status: review

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

- [x] **Task 3 - Correct the Hexalith.Builds publisher with native .NET multi-RID support (AC2).**
  - [x] In the Builds-owned `Github/publish-containers` action/helper, preserve the existing
    `project.csproj|repository` mapping, SemVer checks, registry/repository validation, and
    semantic-release invocation contract.
  - [x] Replace the single-RID `--os linux --arch x64` publish with exactly two .NET SDK container
    RIDs that produce `linux/amd64` and `linux/arm64`. Evaluate the planning baseline
    `linux-x64;linux-arm64` against the configured Alpine base and any native assets; if the
    portable Alpine RIDs `linux-musl-x64;linux-musl-arm64` are required, record and test that
    decision without changing the external platform contract.
  - [x] Pass the chosen set, quoted as one shell argument, through both `RuntimeIdentifiers` and
    `ContainerRuntimeIdentifiers`; do not also set one single `RuntimeIdentifier`, `--arch`, or
    another property that collapses the operation back to one architecture. Force
    `ContainerImageFormat=OCI` for an explicit, testable outer-index contract.
  - [x] Keep `UseHexalithProjectReferences=false`, Release configuration, the new semantic version,
    `registry.hexalith.com`, repository `eventstore`, SDK container support, alpine base family,
    non-root `app`, port 8080, and existing OCI labels intact.
  - [x] Let the .NET SDK build the multi-architecture image/index. Do not introduce Dockerfiles or a
    second hand-rolled image build pipeline when the pinned SDK already supplies the capability.
  - [x] Ensure a mapping succeeds only after its immutable published object passes Tasks 4 and 5;
    successful `dotnet publish` or tag existence alone is not release success.

- [x] **Task 4 - Add a fixture-driven, exact-set OCI validator (AC2, AC3).**
  - [x] Put reusable validation in Hexalith.Builds beside the shared publisher; do not bury it in
    Story 1.20 or duplicate it in EventStore.
  - [x] Resolve the version tag with an explicit OCI-index `Accept` header, validate HTTP
    `Content-Type` against the manifest `mediaType`, capture `Docker-Content-Digest`, then GET the
    same object by immutable digest. Require identical untouched bytes and verify
    `sha256:<hex>` against those exact bytes before parsing.
  - [x] Require top-level `schemaVersion: 2` plus
    `application/vnd.oci.image.index.v1+json`.
  - [x] Require exactly two descriptors and the exact set `linux/amd64`, `linux/arm64`; reject blank
    platform fields, variants, duplicates, extras, and `unknown/unknown`. Accept only recognized
    OCI/Docker child-manifest media types whose descriptor and response content type agree; do not
    require OCI child manifests when the exact outer object is the required OCI index.
  - [x] Resolve each descriptor by digest, bind each raw child-manifest hash and byte length to its
    descriptor digest/size, resolve its config blob, bind config digest/size, and require config
    `os`/`architecture` to equal the descriptor.
  - [x] Make failures deterministic and support-safe: reason codes may identify platform/media-type
    and digest class, but output must not expose registry credentials, authorization headers, raw
    tokens, or unrelated environment detail.
  - [x] Add a positive exact-two-platform fixture and negative fixtures for single manifest,
    missing/duplicate/extra/unknown/variant platform, wrong top-level media type, tag/digest body
    disagreement, descriptor/config size mismatch, child config mismatch, unresolved child,
    malformed descriptor, and raw digest mismatch.
  - [x] Stub registry/tool boundaries in contract tests so negative coverage performs no network,
    registry, NuGet, Git, or Docker mutation.

- [x] **Task 5 - Make both-platform smoke bounded, digest-pinned, and diagnostically honest (AC4).**
  - [x] Reuse the existing EventStore liveness contract: start the published child on a loopback
    ephemeral host port with `ASPNETCORE_URLS=http://+:8080` and poll `/alive` with the same bounded
    timeout/retry policy for both platforms.
  - [x] Resolve the descriptor for each platform and run `repository@<child-digest>`; do not smoke
    the mutable version tag or rely on platform selection from the parent index digest.
  - [x] Add an arm64 emulation/runtime preflight before product smoke. If shared CI needs a QEMU or
    binfmt setup action, keep it in Hexalith.Builds and SHA-pin the third-party action per FR25.
  - [x] Emit distinct outcomes for `environment/emulation-setup-failure`, `image-start-failure`,
    `liveness-timeout`, and `pass`. Only an actual `/alive` pass for each child is success.
  - [x] Preserve the earliest causal blocker when later checks also fail; a later product error must
    not mask an existing registry/authentication/emulation environment failure.
  - [x] Always remove temporary containers/files, keep logs support-safe, and preserve both smoke
    logs and hashes in the evidence bundle.

- [x] **Task 6 - Gate the first irreversible action on durable release-owner authority (AC5).**
  - [x] Reuse the Story 1.20 publication-authority semantics rather than inventing an informal
    approval note. The frozen record must bind the repository, proposed semantic version,
    workflow/source SHA, exact `registry.hexalith.com/eventstore` repository, exact platforms,
    exact maintainer-approved Builds reusable-workflow/action/helper identities, named owner,
    authorization time, durable source, rationale, and unexpired validity window.
  - [x] Before mutation, prove the called Builds workflow, nested `publish-containers` action, and
    installed helper bytes/revisions are the exact maintainer-approved identities in the authority
    record. The accepted review decision supersedes the prior mutable `@main` policy for this
    publication-capable caller: pin one exact workflow/action SHA and do not allow independently
    resolved revisions to drift. If exact pre-publication binding cannot be established, fail
    closed.
  - [x] Distinguish the workflow/source (tag-parent) SHA from the semantic-release tag commit; record
    both after release so provenance cannot repeat the v3.75.0 identity ambiguity.
  - [x] Preflight that the proposed version is absent for every one of the 14 destination NuGet
    package IDs and that the exact container version tag is absent. Remove `--skip-duplicate` from
    the publication path: any pre-existing package or tag is a version collision, not permission
    to combine or overwrite release bytes.
  - [x] Revalidate the frozen authority at an action-time timestamp immediately before the first
    publication-capable step. Missing, malformed, mismatched, expired, wrong-role, or changed bytes
    must fail before `dotnet nuget push` and before any registry write.
  - [x] Make the semantic-release publish ordering prove that a full authority preflight—not merely
    secret presence—precedes NuGet publication. Preserve secret preflight and never echo secrets.
  - [x] Record the authority bytes, durable-source metadata, hashes, and checked-at value in
    provenance. This validates authority evidence; it does not create human authority.

- [x] **Task 7 - Integrate the thin EventStore caller and strengthen guardrails/docs (AC2, AC5, AC6).**
  - [x] Preserve `.github/workflows/release.yml` as a thin `domain-release.yml` caller and keep
    exactly the existing `eventstore` mapping. Per the accepted review decision, bind the reusable
    workflow and execution input to the same exact Builds SHA. Add only inputs/secrets/outputs
    genuinely required by the corrected shared contract.
  - [x] Update `.releaserc.json`, `scripts/validate-release-secrets.sh`, or shared helper phases as
    needed so the authority gate runs before NuGet and the validated publisher runs for the same
    `${nextRelease.version}`. Do not duplicate OCI validation in JSON command strings.
  - [x] Extend EventStore release-governance tests (prefer a focused container-publishing test file
    if adding substantial logic) to prove thin-caller ownership, one mapping, authority-before-
    mutation ordering, explicit three-secret mapping with no `secrets: inherit`, exact platform
    contract, and rejection of a stale single-platform shared publisher contract. Parse mapping
    entries and ordering structurally; substring presence alone is insufficient.
  - [x] Update `docs/ci.md` with the exact two-platform contract, immutable registry inspection,
    child-config checks, `/alive` smoke, environment-vs-product classification, evidence fields,
    and publication/Story 1.20 authority boundaries. Include no credentials or private evidence.
  - [x] Reconcile the already-stale `docs/ci-secrets-checklist.md` with all three explicitly mapped
    release secrets (`NUGET_API_KEY`, `HEXALITH_ZOT_USERNAME`, `HEXALITH_ZOT_API_KEY`), including
    inventory/count, release use, onboarding, ownership, and rotation. Add authority inputs only if
    the corrected contract requires them; credential modernization remains out of scope.
  - [x] Preserve `tools/release-packages.json` at exactly 14 IDs and preserve `Directory.Build.targets`
    plus the EventStore project container repository/base/user/port/label behavior.

- [x] **Task 8 - Validate implementation without external mutation (AC1-AC5).**
  - [x] Run shell syntax and shared publisher contract/fixture tests with fake executables/fixture
    bytes; prove every negative case exits nonzero with its expected support-safe classification.
  - [x] Run Hexalith.Builds workflow/action contract checks and its repository-required focused
    build/tests before recording the exact Builds commit. Wire the publisher suite into the Builds
    pre-release validation path (currently `.github/workflows/build-release.yml`) so it cannot be
    omitted from a Builds release.
  - [x] Run EventStore release-governance tests by project, then restore/build
    `Hexalith.EventStore.slnx` in Release/package mode. Do not run solution-level `dotnet test`.
  - [x] Run package pack/validation/consumer checks into a fresh temporary output and prove the
    exact 14-ID inventory without publishing it.
  - [x] Run workflow/docs/mapping scans, `git diff --check`, and separate worktree/status checks in
    both repositories. Record every command/result and any environmental blocker exactly.

- [x] **Task 9 - Publish and inspect a new corrective version only after explicit authority (AC2-AC6).**
  - [x] Stop and obtain the separate durable release-owner authority described in AC5. Story
    approval or successful local tests are insufficient.
  - [x] Publish a new semantic-release-generated version from the authorized source SHA; do not
    mutate or reuse any v3.75.0 package/tag/manifest/registry object.
  - [x] If any external mutation partially succeeds and the release later fails, preserve and
    quarantine that failed identity as non-authorizing evidence. Never retry the same semantic
    version, skip existing packages, or repoint its container tag; obtain new authority for a fresh
    version after the defect/environment is corrected.
  - [x] Capture the workflow run and attempt, resolved Hexalith.Builds reusable-workflow/action
    commit identity, source SHA, release tag commit/parent, release URL, 14 package
    files/versions/sizes/hashes, container
    repository/index digest/raw bytes/hash, child descriptors/manifests/configs, exact platforms,
    and both child-digest smoke results.
  - [x] Independently download and hash all package assets. Require one new version across exactly
    the manifest IDs and verify package/container provenance maps to the same authorized release.
  - [x] Treat publication, inspection, emulation, product, or evidence failure as non-authorizing;
    do not hide partial publication or record a failed release as pass.

- [x] **Task 10 - Hand observed evidence to Story 1.20 without deciding it (AC6, AC7).**
  - [x] Add the corrective release identity and immutable evidence to the Story 1.20 packet as
    observed candidate data first; preserve every independent production-path, reviewer, owner,
    A/B/C, and migration gate.
  - [x] Do not populate `approved_*`, change `final_decision`, authorize migration, modify Parties or
    Tenants, approve G5, or mark Story 1.20/Epic 1 done under Story 3.12 authority.
  - [x] Route the packet to the EventStore and release owners for their independent verification and
    durable disposition. Record limitations and failed/blocked gates without optimistic inference.

### Review Findings

- [x] [Review][Patch] Pin the EventStore reusable release workflow to the exact approved Builds SHA
  so a mutable workflow cannot remove its own identity check before receiving publication secrets;
  the owner chose immutable pinning and explicitly superseded the prior `@main` policy
  [.github/workflows/release.yml:31]
- [x] [Review][Patch] Run `initialize-build` from the already approved Builds checkout after the
  workflow-identity gate, not from an independent `@main` action
  [references/Hexalith.Builds/.github/workflows/domain-release.yml:90]
- [x] [Review][Patch] Authenticate the durable authority source and owner, attach `GITHUB_TOKEN`
  only to the exact trusted GitHub API origin, verify the release-owner allowlist, and reject HTTPS
  downgrade redirects [references/Hexalith.Builds/Github/publish-containers/publication_authority.py:41]
- [x] [Review][Patch] Freeze the authority bytes/hash during `verifyRelease` and require exact byte
  equality during the pre-publication revalidation instead of overwriting the first record
  [.releaserc.json:11]
- [x] [Review][Patch] Probe every recognized manifest media type, test container-tag collisions,
  and recheck tag absence immediately before SDK container publication
  [references/Hexalith.Builds/Github/publish-containers/publication_authority.py:248]
- [x] [Review][Patch] Upload the complete support-safe release-evidence directory with `always()` so
  success and partial failure remain durable
  [references/Hexalith.Builds/.github/workflows/domain-release.yml:208]
- [x] [Review][Patch] Persist verified raw child-manifest and config bytes/hashes, not only the raw
  parent index and descriptor identities
  [references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py:179]
- [x] [Review][Patch] Validate a missing registry digest before immutable dereference so it returns
  the deterministic `missing-registry-digest` result instead of an uncaught `TypeError`
  [references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py:238]
- [x] [Review][Patch] Enforce the approved OCI repository-name grammar and keep evidence paths
  beneath their configured root
  [references/Hexalith.Builds/Github/publish-containers/publish-containers.sh:41]
- [x] [Review][Patch] Make the arm64 environment preflight prove executable binfmt/runtime support
  and behaviorally test runtime emulation-failure classification
  [references/Hexalith.Builds/Github/publish-containers/smoke_container_platforms.py:88]
- [x] [Review][Patch] Pull each immutable child explicitly with a bounded timeout before `docker run`
  and preserve registry/environment failure classification
  [references/Hexalith.Builds/Github/publish-containers/smoke_container_platforms.py:111]
- [x] [Review][Patch] Reject non-finite smoke timeout/interval values so NaN or infinity cannot make
  polling unbounded
  [references/Hexalith.Builds/Github/publish-containers/smoke_container_platforms.py:209]
- [x] [Review][Patch] Require and behaviorally assert the exact loopback `/alive` request and a 2xx
  response; redirects must not pass
  [references/Hexalith.Builds/Github/publish-containers/smoke_container_platforms.py:148]
- [x] [Review][Patch] Inspect exited containers, retain bounded support-safe diagnostics, and record
  cleanup failure without masking the earliest causal outcome
  [references/Hexalith.Builds/Github/publish-containers/smoke_container_platforms.py:161]
- [x] [Review][Patch] Add behavioral fail-closed tests proving rejected authority blocks both
  mutation commands and that workflow/action SHA and byte mismatches stop semantic-release
  [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs:49]
- [x] [Review][Patch] Exercise the live HTTP adapter and post-probe expiry path so Accept headers,
  immutable references, untouched bytes, and action-time revalidation cannot regress behind pure
  fixture tests [references/Hexalith.Builds/Github/publish-containers/tests/test_oci_registry_validator.py:186]

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
| `.github/workflows/release.yml` | Thin caller with one `eventstore` mapping; formerly resolved `domain-release.yml@main`. | **UPDATE:** retain the thin caller but bind reusable workflow and execution input to one exact approved Builds SHA, plus explicit authority/evidence wiring. | Thin caller, CI-head equality, one container mapping, explicit secrets; immutable release pin supersedes the prior `@main` policy. |
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

OpenAI Codex (GPT-5)

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
- 2026-07-19 Task 8 shared validation: `Tools/test-publish-containers.ps1`
  passed 22/22 fixture/contract tests; `Hexalith.Builds.slnx` Release restore and
  build passed with zero warnings/errors; the three built Builds test assemblies
  passed 11/11, 52/52, and 1/1.
- 2026-07-19 Task 8 EventStore validation: package-mode restore and Release
  solution build passed with zero warnings/errors; focused container publishing
  governance passed 5/5; a fresh temporary package lane produced and validated
  exactly 14 packages plus the package-only consumer/tool installation.
- 2026-07-19 Task 8 broad-gate blocker: the full Contracts test assembly passed
  735/736. `CommitMessagePolicyTests.CommitMessageHookExecutesCommitlintAndRemainsUnixCompatible`
  failed because the unrelated, user-owned working-tree change in
  `commitlint.config.mjs` differs from the repository's exact committed policy
  fixture. The change was initially preserved pending direction.
- 2026-07-19 Task 8 blocker resolved under the user's explicit "do best"
  direction: restored the committed three-line strict `commitlint.config.mjs`
  policy, aligned the new governance assertion with the publisher's tested
  literal shell argument, rebuilt the Contracts test project with zero
  warnings/errors, and passed the complete Contracts assembly 736/736.
- 2026-07-19 Task 8 real SDK validation initially failed with `MSB1006` because
  shell argument grouping did not preserve literal quotes for MSBuild's
  semicolon-separated property parser. A red/green contract test now requires
  literal MSBuild quoting for both RID properties. The corrected command built a
  112 MB local OCI archive without registry mutation; its exact OCI index digest
  was `sha256:6ffec336936bddecd0345834ba43a6527288cc81f4dc50d66e2a49d67df35772`,
  with exactly `linux/amd64` and `linux/arm64` children and matching configs.
- 2026-07-19 Task 8 package validation first proved all 14 packages at synthetic
  version `0.0.0-story-3-12`, but the consumer correctly rejected that version
  as lower than a transitive `>=3.74.0` constraint. A fresh non-publishing
  `99.0.0-story-3-12` lane then passed exact inventory, package validation,
  13-library consumer build, and one tool installation.
- 2026-07-19 Task 8 source audit hardened three boundaries with red/green tests:
  the nested publisher action now runs from an exact approved Builds checkout
  instead of an independent `@main`; semantic-release validates authority before
  Git-tag creation and revalidates before NuGet/registry mutation; and registry
  plus authority redirects strip authorization on cross-origin redirects. OCI
  config blobs are digest/size bound without incorrectly requiring a manifest
  media type in the blob HTTP response.
- 2026-07-19 Task 8 final rerun: the shared publisher suite passed 22/22,
  EventStore and Builds shell syntax checks passed, and `git diff --check`
  passed in both owning repositories.
- 2026-07-19 release work resumed under the user's explicit "do 1 & 2"
  direction, authorizing scoped commits/pushes in Hexalith.Builds and
  EventStore plus creation of the separate durable release-owner authority
  record. Repository review/merge, authority validation, and destination
  absence remain fail-closed gates before publication.
- 2026-07-19 Hexalith.Builds candidate committed and pushed on
  `fix/story-3-12-multi-platform-containers` as
  `cc64ec1b4d5c59a8ece72eef3ae841d3284f7b82`. The final shared suite passed
  22/22, including cross-origin redirect credential-stripping coverage; no
  Builds `main` merge or release was performed at this point.
- 2026-07-19 repository-state evidence: `git diff --check` and shell syntax
  checks passed in both owning repositories. EventStore is one release commit
  behind `origin/main` (`bf8a6358`, tag `v3.77.0`) and also contains unrelated
  user-owned architecture/memlog/review changes. No fetch integration,
  commit, push, release, registry/NuGet mutation, or Story 1.20 disposition was
  performed by this workflow run.
- 2026-07-19 Task 9 HALT: no separate durable EventStore release-owner authority
  authorizes Git-tag/release execution, NuGet publication, or registry mutation.
  The maintainer authority in this session explicitly permits only uncommitted
  implementation and local validation, so no publication or Story 1.20 handoff
  mutation was attempted.
- 2026-07-19 Task 9 HALT resolved for repository mutation: the user explicitly
  authorized the scoped Builds/EventStore commits and pushes and creation of
  the durable release-owner record. Publication remains prohibited until that
  record binds the exact reviewed/merged source and Builds SHAs and passes the
  live destination-absence preflight.
- 2026-07-19 concurrent repository-state change: an external maintainer process
  checked both repositories out to `fix/story-3-12-multi-platform-containers`
  and committed/pushed the shared Builds implementation as
  `cc64ec1b4d5c59a8ece72eef3ae841d3284f7b82` (`fix(release): enforce exact
  multi-platform container publication`, author Jérôme Piquot). Codex did not
  run the checkout, commit, amend, or push. EventStore remains uncommitted at
  `bf8a6358f32cbefeba037f664d8fe3120e644dc9`; its gitlink appears modified only
  because the shared checkout moved externally. This observed commit does not
  create release authority, and Builds `origin/main` still resolves to
  `786955bccaa3ea676c3025c797bec3c30189425c`.
- 2026-07-19 review remediation: Hexalith.Builds head
  `077e6fcd892425fa4de0053a39c82913a5362bb0` passes the complete 36-test
  publisher suite, Python compilation, shell syntax, diff checks, and workflow
  lint (with actionlint 1.7.12's unsupported-property diagnostic ignored only
  for GitHub's documented `job.workflow_repository` and `job.workflow_sha`).
  SonarCloud passes that exact head. All code-level review patches are resolved;
  the mutable-`@main` trust decision remains open because neither repository has
  required-check branch protection.
- 2026-07-19 Task 9 release authority expanded: `jpiquot`, acting as maintainer,
  authorized commits, releases, NuGet/registry publication, and Story 1.20
  disposition. External mutation remains fail-closed until the exact PR heads,
  repository trust decision, version, source SHA, Builds SHA, durable authority
  record, and destination-absence gates are resolved.
- 2026-07-19 merged implementation identities: Hexalith.Builds PRs #18 and #19
  landed the authority-bound publisher at `819c1f6d...` then `f8981e8e...`;
  EventStore PRs #290 and #292 landed sources `c1bdfe31...` and `a832374a...`.
  After the first live smoke exposed missing JWT startup configuration and an
  insufficient arm64 emulation bound, Builds PRs #20 and #21 landed the final
  reviewed shared execution commit
  `9ec0a032d785dd0abdc14276e8784d6fdd826fd0`. EventStore PRs #294 and #295
  then landed the final caller at
  `77a9a442c0e6d0408957888e10c3a9accd634c99`; CI run `29694589389` passed.
- 2026-07-19 final authority-bound helper SHA-256 values at Builds
  `9ec0a032...`: publisher
  `0787e2160ca5b02ce263cd314eba8c89207b09ab47ee3aec29f1199558460a59`,
  OCI validator
  `e1547e31fbdb8a678c99a245510e718c1cb35f6b9ec51264aa7bc1cdae419509`,
  authority validator
  `92692f7c1cea6d7a81c6ef9b1c5964c8b54b65fa461e91ac02c441b7101d8f8d`,
  smoke shell entry point
  `f11865658a24ecea906da087d8446c7f631dc5a9328e294676534d590ef0e89e`,
  and smoke implementation
  `f9120d16c42e828a91393cd8eceb3bdee1ae4c28311ee4893d03cd51c044c812`.
- 2026-07-19 Task 9 partial-publication quarantine: release v3.77.1 published all
  14 NuGet packages, tag, and exact two-platform OCI index, then failed both
  product smokes. That identity remains permanently quarantined; it was never
  retried, repointed, overwritten, or represented as authorizing evidence.
- 2026-07-19 Task 9 pre-publication retry audit: run `29694306927` attempt 1 was
  externally cancelled during build and made no release mutation; attempt 2
  failed closed because its repository variable still referenced the v3.77.1
  authority; attempt 3 consumed the corrected authority but became a safe no-op
  when final release PR #295 advanced `main`. Each attempt was followed by
  explicit tag, release, 14-package, and registry absence checks before the next
  authorized source was considered.
- 2026-07-19 Task 9 completed: durable authority comment `5016454096` bound
  version `3.77.2`, source/tag commit
  `77a9a442c0e6d0408957888e10c3a9accd634c99`, Builds workflow/action commit
  `9ec0a032d785dd0abdc14276e8784d6fdd826fd0`, exact helper hashes, repository,
  platforms, owner, rationale, and validity window. Release run `29694935552`
  attempt 1 completed successfully and published GitHub release v3.77.2.
- 2026-07-19 Task 9 evidence: Actions artifact `8444768158`
  (`release-evidence-29694935552-1`) has API digest
  `sha256:6e033439acaba9b44fd101984c7b567cb6914aee30a5bc0d12c8001116d5a9b2`.
  The live OCI index digest and exact raw-index SHA-256 are
  `sha256:db3ab41e187efc0de397fd1205660a0f685e2c94ecd8f4a8f1843ac567056bf6`;
  it contains only child digests `sha256:3b5d2a23...` for amd64 and
  `sha256:254a5714...` for arm64, whose configs match their descriptors. Both
  immutable child smokes and cleanup passed.
- 2026-07-19 Task 9 independent inspection: all 14 GitHub release assets and all
  14 NuGet.org copies were downloaded and hashed. IDs and version `3.77.2` are
  exact; after excluding NuGet.org's repository `.signature.p7s`, every package
  payload entry is byte-identical, and all 14 NuGet.org repository signatures
  pass `dotnet nuget verify --all`. Independent registry
  tag/digest/child/config downloads exactly match the retained evidence bytes.
- 2026-07-19 Task 10 evidence handoff: the Story 1.20 packet now records v3.77.2
  as observed candidate data while keeping every `approved_*` field null,
  `final_decision: still blocked`, and `authorize_consumer_migration: false`.
  It explicitly retains the unrun exact-SHA production-path gate, Story 1.16
  review, named owner approvals, WORM evidence, limitations, and A/B/C chain as
  blockers.

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
- Task 3 replaced the collapsing `--os linux --arch x64` invocation with the
  Alpine-compatible exact RID set `linux-musl-x64;linux-musl-arm64`, supplied as
  one argument through both multi-RID properties. It forces OCI format and
  package-reference Release mode, retains the mapping/version/repository
  contract, and requires validator plus child-smoke success before a mapping can
  return success. Fake-tool contract tests pass and prove no single-RID option
  remains.
- Task 4 added exact raw-byte registry validation with explicit OCI Accept
  semantics, immutable tag/digest equality, schema/media-type enforcement, the
  exact two-platform set, child/config digest and size binding, and config
  platform matching. The fixture matrix passes positive coverage and every
  required fail-closed negative classification without network or mutation.
- Task 5 added SHA-pinned arm64 emulation setup plus an environment preflight,
  then runs the same bounded loopback `/alive` policy against each immutable
  child digest. Fake Docker/curl tests prove cleanup, both passes, and distinct
  `environment/emulation-setup-failure`, `image-start-failure`, and
  `liveness-timeout` outcomes; support-safe logs, summaries, and hashes are
  retained.
- Task 6 added a shared durable authority validator that binds the exact release
  identity, one resolved Builds workflow/action SHA, installed helper hashes,
  named release-owner role, rationale, and validity window. It records frozen
  authority/source/check-time evidence and proves all 14 package versions plus
  the container tag are absent. The shared workflow and action fail on identity
  drift, EventStore runs this gate before NuGet, and duplicate skipping was
  removed. Source SHA remains distinct from the later tag commit, which can only
  be recorded after an authorized release.
- Task 7 kept EventStore as a thin shared-workflow caller with one `eventstore`
  mapping and three explicit release secrets. Focused structural tests prove the
  authority gate precedes NuGet mutation, the shared publisher receives the
  same version, the package inventory remains exactly 14, and container defaults
  remain unchanged. EventStore and Builds CI documentation now records the exact
  two-platform, immutable-validation, smoke, evidence, credential, and authority
  boundaries. The shared 22-test publisher suite and all 5 EventStore governance
  tests pass.
- Task 8 completed every non-mutating release/package check. The shared
  publisher suite passes 22/22, the focused governance suite passes 5/5, the
  complete Contracts assembly passes 736/736, Builds and EventStore Release
  builds pass with zero warnings/errors, and the fresh package-only lane proves
  the exact 14-ID inventory plus consumer/tool installation. Shell, diff, docs,
  mapping, and separate repository-state checks also pass.
- Final source audit also proved the native .NET SDK creates the exact local OCI
  index, removed a real MSBuild semicolon-quoting failure, made the nested action
  immutable at the approved Builds SHA, moved authority validation ahead of Git
  tagging while retaining action-time revalidation, aligned config-blob handling
  with OCI Distribution semantics, and prevents cross-origin credential
  forwarding.
- Task 9 published fresh corrective release v3.77.2 only after exact durable
  authority and destination-absence checks. It retains one coherent authorized
  source/Builds/version identity across the 14 packages and exact two-platform
  OCI index; both digest-pinned platform smokes passed. Failed v3.77.1 remains
  quarantined as non-authorizing evidence.
- Task 10 added the complete v3.77.2 observed identity to Story 1.20 without
  selecting or approving it. Story 1.20 remains blocked and migration remains
  unauthorized pending its independent runtime, reviewer, owner, durable
  evidence, limitations, and A/B/C gates.

### File List

- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md`
- `_bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py`
- `references/Hexalith.Builds/Github/publish-containers/publication_authority.py`
- `references/Hexalith.Builds/Github/publish-containers/publish-containers.sh`
- `references/Hexalith.Builds/Github/publish-containers/action.yml`
- `references/Hexalith.Builds/Github/publish-containers/smoke-container-platforms.sh`
- `references/Hexalith.Builds/Github/publish-containers/smoke_container_platforms.py`
- `references/Hexalith.Builds/Github/publish-containers/tests/test_oci_registry_validator.py`
- `references/Hexalith.Builds/Github/publish-containers/tests/test_publish_script_contract.py`
- `references/Hexalith.Builds/Github/publish-containers/tests/test_publication_authority.py`
- `references/Hexalith.Builds/Github/publish-containers/tests/test_smoke_container_platforms.py`
- `references/Hexalith.Builds/.github/workflows/domain-release.yml`
- `references/Hexalith.Builds/.github/workflows/build-release.yml`
- `references/Hexalith.Builds/.github/workflows/domain-release.md`
- `references/Hexalith.Builds/Github/publish-containers/README.md`
- `.github/workflows/release.yml`
- `.releaserc.json`
- `docs/ci.md`
- `docs/ci-secrets-checklist.md`
- `scripts/validate-release-authority.sh`
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs`
- `references/Hexalith.Builds/Tools/test-publish-containers.ps1`
- `references/Hexalith.Builds/test/fixtures/publish-containers/v3.75.0-evidence.json`
- `references/Hexalith.Builds/test/fixtures/publish-containers/v3.75.0-single-manifest.json`
- `references/Hexalith.Builds/test/fixtures/publish-containers/validation-cases.json`

### Change Log

- 2026-07-19: Published and independently inspected corrective release v3.77.2;
  handed its observed package/container identity to Story 1.20 while preserving
  every independent approval and migration blocker.
