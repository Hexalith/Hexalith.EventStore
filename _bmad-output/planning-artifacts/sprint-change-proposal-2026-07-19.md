---
title: Sprint Change Proposal - Correct Multi-Platform EventStore Publishing
status: final
created: 2026-07-19
project: eventstore
mode: batch
scope_classification: moderate
approval: approved
approved_by: Administrator
approved_on: 2026-07-19
finalized_on: 2026-07-19
trigger: v3.75.0 packages were published successfully but its EventStore container is a single linux/amd64 image instead of the exact two-platform OCI index required by Story 1.20 Acceptance Boundary 8
observed_release: v3.75.0
observed_release_run: https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29683837428
observed_release_url: https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.75.0
target_stories:
  - "1.20"
  - "3.12"
---

# Sprint Change Proposal: Correct Multi-Platform EventStore Publishing

## 1. Issue Summary

The v3.75.0 release published all 14 manifest-governed NuGet packages, but its
EventStore container does not satisfy Story 1.20 Acceptance Boundary 8 (AC8).
AC8 requires one immutable image digest whose raw manifest is an OCI index with
exactly `linux/amd64` and `linux/arm64`. The published tag instead resolves to a
single Docker image manifest for `linux/amd64`.

This was discovered by inspecting the immutable release assets and registry
objects after the successful release workflow completed on 2026-07-19. The
failure is not a package-inventory failure: all 14 expected `.nupkg` assets exist,
their GitHub-provided SHA-256 digests match independently downloaded bytes, and
no unexpected package is present. It is a container publishing and validation
failure.

### 1.1 Release Identity

| Evidence | Observed value |
| --- | --- |
| Release | `v3.75.0`, published `2026-07-19T10:47:27Z` |
| Release workflow | [Run 29683837428](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/29683837428), conclusion `success` |
| Workflow head / source commit | `d046120fb4178252a0e0f300d714bac70018e9f7` |
| Release tag commit | `67a9e00efaf397a31669f65df7008f671d20e06a` |
| Release tag parent | `d046120fb4178252a0e0f300d714bac70018e9f7` |
| Package count | Exactly 14 assets matching `tools/release-packages.json` |
| Container tag | `registry.hexalith.com/eventstore:3.75.0` |

### 1.2 v3.75.0 NuGet Package Hashes

The GitHub release API digests below were independently reproduced by
downloading all 14 release assets and running `sha256sum` over the resulting
bytes.

| Exact package ID | Version | Size (bytes) | SHA-256 |
| --- | --- | ---: | --- |
| `Hexalith.EventStore.Contracts` | `3.75.0` | 143191 | `501485bacf3d76e847d1e162d6b51a72816a89ed2f167c56df19eb2c9eb96cf3` |
| `Hexalith.EventStore.Client` | `3.75.0` | 146237 | `5e592616dd180c8cc8b51facc0eb2dc06d0a33b7c426a9f5d9012739d7d2eb23` |
| `Hexalith.EventStore.Server` | `3.75.0` | 329419 | `a6510fb61cad2ab4d933d19d948cc5ca847edfc1e86bff342407bfb6d5acf8e3` |
| `Hexalith.EventStore.SignalR` | `3.75.0` | 21155 | `85493eefbc9c0a302f8e6b956d0e0c942c97b74d23599492782530a105725a56` |
| `Hexalith.EventStore.Testing` | `3.75.0` | 52308 | `9fd627c9cfc2114f058eeb63cfbea80735bef8b1a38771912b21af3eed476a49` |
| `Hexalith.EventStore.Testing.Integration` | `3.75.0` | 63949 | `c9b7d6e19fa804a4e880ec1ac244fed41da576e0fac42552ea061bf5b50acbc4` |
| `Hexalith.EventStore.Aspire` | `3.75.0` | 25663 | `cd7f42446536b47856e72429821ce53593e4601c16067c9d8ea3b6490b7329e1` |
| `Hexalith.EventStore.ServiceDefaults` | `3.75.0` | 16207 | `823ebba8870caa13dd8b64801a759fd0a65ec6a2f19def60c94e592f247e8313` |
| `Hexalith.EventStore.DomainService` | `3.75.0` | 51821 | `d41937ce8d9a4890d2e97a8e09d09b3c56cc46b24666216e26d6055296905050` |
| `Hexalith.EventStore.RestApi.Generators` | `3.75.0` | 36992 | `e5fa1eb0bd09e2c2e2321781aca81e0dd9f846e00d349634b194f7f43eed77dc` |
| `Hexalith.EventStore.Gateway` | `3.75.0` | 230221 | `3700b8cb46a34ed2f9d3f2664a571f07a5a1f70423df70967926cea86f39968c` |
| `Hexalith.EventStore.Admin.Abstractions` | `3.75.0` | 111743 | `345fe452dda39fbc3513778936375da03b0be5c9ef2aee858f28deb1756b1be5` |
| `Hexalith.EventStore.Admin.Cli` | `3.75.0` | 652071 | `fb2ac8c69da9790a9ff46237cb1db8958feaf04a59d340375094dc81ac503aad` |
| `Hexalith.EventStore.Admin.Server` | `3.75.0` | 173425 | `e88f69d1bbd840ae9fa29bf710dcc71cc106c5cbc8101a59dc5552887c7a7a67` |

### 1.3 Container Failure Evidence

| Evidence | Observed value | AC8 expectation | Result |
| --- | --- | --- | --- |
| Immutable digest | `sha256:1cb7b6ed3db986e9896cc36f360d14017f3bf3521eed8e33267c3dffd05ca253` | Immutable digest of the raw OCI index | Digest is valid, but identifies the wrong object type |
| Raw-manifest SHA-256 | `1cb7b6ed3db986e9896cc36f360d14017f3bf3521eed8e33267c3dffd05ca253` | Equal to the immutable digest | Pass |
| Manifest media type | `application/vnd.docker.distribution.manifest.v2+json` | `application/vnd.oci.image.index.v1+json` | Fail |
| `manifests` array | Absent | Exactly two descriptors | Fail |
| Config digest | `sha256:9623a1d61f39b21ab28948e903669a54b38ab500c190dc7e7c9889744209a6e9` | One matching child config per required platform | Only one config exists |
| Config platform | `linux/amd64` | Exactly `linux/amd64` and `linux/arm64` | Fail: `linux/arm64` absent |
| Image version label | `3.75.0` | Matches release version | Pass |

The shared publisher installed from `Hexalith.Builds@main` invokes .NET SDK
container publishing with `--os linux --arch x64`. The EventStore caller supplies
only the project/repository mapping and cannot add an arm64 variant through the
current input contract. The release workflow therefore reported success after
publishing a single architecture and did not validate the registry object against
AC8.

The v3.75.0 package hashes are useful immutable evidence, but the package set
cannot be combined with this nonconforming container to authorize Story 1.20.
The release remains historical failed evidence, not an approved artifact identity.

## 2. Impact Analysis

### Epic Impact

- Epic 1 remains valid and `in-progress`.
- Story 1.20 remains `in-progress`/blocked with `final_decision: still blocked`
  and `authorize_consumer_migration: false`.
- Epic 3 remains the correct release-reliability home for the fix.
- Add focused Story 3.12 rather than broadening Story 1.20 or reopening runtime
  projection/query implementation.
- No other epic is invalidated, reordered, or expanded.

### Story Impact

#### Story 1.20

- Record the v3.75.0 package hashes as observed, non-authorizing evidence.
- Record the single-platform container object and exact reason it fails AC8.
- Keep all `approved_*` package/container fields null until one later release
  satisfies every closure gate.
- Do not select v3.75.0 as the tested/approved release identity and do not
  authorize Parties Story 8.6.
- Point the scoped corrective-item requirement to Story 3.12.

#### New Story 3.12

- Correct the shared publishing path so a new release emits an OCI index with
  exactly `linux/amd64` and `linux/arm64`.
- Add positive and negative validation that prevents another single-platform
  image from being reported as a successful multi-platform release.
- Publish a new semantic-release-generated corrective version; do not overwrite
  v3.75.0 packages, tag, container tag, or registry object.
- Return the new release's 14 package hashes, index digest, child digests,
  platform set, source mapping, and smoke evidence to Story 1.20. Story 3.12 does
  not itself approve the parity packet or consumer migration.

### Artifact Conflicts And Required Adjustments

| Artifact | Adjustment |
| --- | --- |
| Story 1.20 proof packet | Add a v3.75.0 observed-release section containing all 14 package hashes and the failed container identity; preserve every fail-closed approval field. |
| `epics.md` | Add Story 3.12 under Epic 3 with the scoped publishing and evidence contract below. |
| `sprint-status.yaml` | After proposal approval, add Story 3.12 as `backlog`; retain Story 1.20 and both affected epics in their current non-done states. |
| Hexalith.Builds shared publisher | Under separate Builds-maintainer authority, implement two-platform publishing and exact post-publish validation. |
| Hexalith.Builds publisher tests | Add exact-set positive coverage and single, missing, duplicate, extra, unknown, mismatched-config, wrong-media-type, and digest-mismatch negative fixtures. |
| EventStore release caller/tests | Prove the existing single `eventstore` mapping consumes the corrected shared contract and rejects nonconforming registry evidence. |
| `docs/ci.md` | During implementation, document the exact two-platform release contract and evidence boundary. |
| PRD | No requirement change. FR22, NFR9, NFR11, and NFR16 already require reproducible, validated release behavior. |
| Architecture | No decision change. AD-11, AD-12, and AD-22 already govern artifact identity and evidence. |
| UX | No impact. |

### Technical Impact

The correction is intentionally narrow:

1. Build and publish one EventStore image child for `linux/amd64` and one for
   `linux/arm64` using .NET SDK container support; no Dockerfile is introduced.
2. Assemble those immutable child digests into one OCI image index tagged with
   the new release version.
3. Read the published raw registry bytes, bind their SHA-256 to the reported
   digest, and reject any media type or descriptor set other than the exact
   two-platform contract.
4. Verify each child config declares the descriptor's OS/architecture and smoke
   each immutable child digest on its declared platform.
5. Record the new release's exact 14-package byte identities and container
   provenance without changing `tools/release-packages.json`.

The correction does not add a package, container repository, runtime feature,
deployment topology, or consumer dependency change.

## 3. Recommended Approach

Use a **Direct Adjustment** within Epic 3.

1. Preserve v3.75.0 as immutable failed release evidence.
2. Add Story 3.12 at `backlog` after this proposal is approved.
3. Implement and review the shared publisher correction in Hexalith.Builds under
   that repository's maintainer authority.
4. Validate the EventStore caller against the corrected shared publisher.
5. Obtain separate, durable release-owner authority before performing the
   externally visible corrective publication.
6. Publish a new semantic version, capture the complete package/container
   evidence, and hand it back to Story 1.20 for its independent approval gates.

- **Planning scope:** Moderate; one cross-repository release story and one active
  proof packet need coordinated changes.
- **Implementation effort:** Medium.
- **Technical risk:** Medium; multi-architecture builds, registry index assembly,
  emulated smoke execution, and immutable digest validation must agree.
- **Release risk:** High if validation remains post-hoc, so exact-set validation
  is a blocking story criterion.
- **Timeline impact:** Story 1.20 remains blocked until Story 3.12 produces a
  conforming later release and all other Story 1.20 gates pass.
- **MVP impact:** None; this corrects existing release evidence rather than
  changing product scope.

Rollback is not useful because v3.75.0 is already published and must remain
immutable. An MVP review is unnecessary because the requirement and architecture
remain valid.

## 4. Detailed Change Proposals

### 4.1 Story 1.20 Proof Packet - Observed Release Evidence

**Current text**

> `tools/release-packages.json` currently names exactly 14 package IDs. No
> closure package build was produced, so every version and SHA-256 hash is
> unresolved.

> approved immutable digest: not selected
>
> approved platform set: not selected

**Proposed text**

Add a separate `Observed v3.75.0 Non-Authorizing Release Evidence` section that
contains the complete tables from sections 1.1-1.3 of this proposal and states:

> v3.75.0 published all 14 expected package assets, and each recorded SHA-256
> matches both the GitHub release digest and independently downloaded bytes.
> Its container tag resolves to immutable digest
> `sha256:1cb7b6ed3db986e9896cc36f360d14017f3bf3521eed8e33267c3dffd05ca253`,
> but that object is a single Docker image manifest whose config declares only
> `linux/amd64`. It is not an OCI index and has no `linux/arm64` descriptor.
> Therefore v3.75.0 fails Acceptance Boundary 8 and cannot authorize consumer
> migration. The `approved_*` fields remain null, the final decision remains
> `still blocked`, and the scoped corrective release is Story 3.12.

**Rationale:** The packet must preserve useful immutable package evidence without
misclassifying a partially conforming release as an approved Story 1.20 identity.

### 4.2 New Epic 3 Story

**OLD**

No Story 3.12 exists. Story 1.20 requires a scoped corrective item when closure
remains blocked.

**NEW**

#### Story 3.12: Multi-Platform EventStore Container Publishing Correction

**Requirements covered:** FR22, FR25, NFR9, NFR11, NFR16, NFR17; governed by
AD-11, AD-12, AD-22, and Story 1.20 Acceptance Boundary 8.

**Owner / review boundary:** Amelia (Developer) coordinates EventStore and the
shared publishing integration; the Hexalith.Builds maintainer approves the shared
publisher implementation and exact Builds commit; Murat (Test Architect) reviews
platform-set, digest, child-config, and smoke evidence; the release owner alone
authorizes external publication and disposes the resulting artifact identity.

**Focused validation:** shared publisher contract tests; negative manifest/index
fixtures; EventStore release-caller validation; immutable registry inspection;
both-platform child-config validation; `linux/amd64` and `linux/arm64` smoke; exact
14-package inventory and independent SHA-256 verification.

As an EventStore release owner,
I want the shared release path to publish an exact two-platform OCI index,
So that a corrective EventStore release can satisfy Story 1.20 AC8 without
changing package scope or overwriting v3.75.0.

**Acceptance Criteria:**

**Given** v3.75.0 is inspected
**When** its release assets and container registry object are recorded
**Then** all 14 package hashes from this proposal are preserved as observed
non-authorizing evidence, the container digest/media type/config platform are
recorded, and its missing `linux/arm64` descriptor is an explicit failed result
**And** no v3.75.0 package, Git tag, container tag, manifest, or registry object is
overwritten or reclassified as approved.

**Given** the Hexalith.Builds shared publisher is corrected under maintainer
authority
**When** the EventStore container mapping is published for a new semantic version
**Then** .NET SDK container support produces immutable `linux/amd64` and
`linux/arm64` children and the version tag resolves to an OCI image index with
media type `application/vnd.oci.image.index.v1+json`
**And** the index contains exactly one descriptor for each required platform,
with no duplicate, extra, `unknown/unknown`, or variant descriptor.

**Given** the published index and child manifests are inspected
**When** release validation runs against immutable digests
**Then** the raw index bytes hash to the registry-reported index digest, every
descriptor digest resolves, and each child config's `os`/`architecture` equals
its descriptor
**And** a single-platform manifest, wrong media type, missing/duplicate/extra
platform, config mismatch, unresolved child, or digest mismatch fails the release
evidence gate.

**Given** the two immutable child digests are runnable
**When** platform smoke validation executes
**Then** both `linux/amd64` and `linux/arm64` variants start successfully and pass
the same bounded support-safe EventStore health smoke
**And** emulation/setup failure is reported separately from a product image
failure and cannot be recorded as a pass.

**Given** a corrective release is ready for external publication
**When** the publish action is requested
**Then** a separate durable release-owner authority record binds the repository,
new version, source SHA, exact container repository, two-platform scope, owner,
date, rationale, and validity window before registry or NuGet mutation occurs
**And** this planning/story approval alone grants no registry, NuGet, commit,
branch, submodule, or push authority.

**Given** the corrective semantic release completes
**When** its evidence is assembled
**Then** exactly the 14 IDs in `tools/release-packages.json` share the new version
and have independently verified SHA-256 values, while the container evidence
records repository, index digest, raw-index hash, child digests/configs, exact
platform set, source SHA, workflow run, and smoke results
**And** the package inventory remains 14, only `eventstore` is published as a
container, and package/container version provenance is coherent.

**Given** Story 3.12 has produced a conforming release
**When** its handoff reaches Story 1.20
**Then** Story 1.20 independently revalidates every package/container identity,
remaining production-path result, approval, and A/B/C authorization gate before
selecting approved fields or authorizing migration
**And** Story 3.12 does not modify Parties or Tenants, approve G5, authorize
consumer migration, or mark Story 1.20/Epic 1 done.

**Explicit exclusions:** no Dockerfile; no EventStore runtime behavior change; no
new package or container mapping; no v3.75.0 mutation; no trusted-publishing,
signing, SBOM, attestation, or credential-modernization expansion; no consumer
dependency update; and no Story 1.20 proof-result approval.

**Rationale:** A focused release story fixes the shared publishing defect while
preserving the independent evidence and human-approval boundaries of Story 1.20.

### 4.3 Sprint Tracker

After explicit approval of this proposal, add:

```yaml
3-12-multi-platform-eventstore-container-publishing-correction: backlog
```

Keep these existing states unchanged:

- `1-20-owner-approved-parity-closure-and-runtime-pin: in-progress`
- `epic-1: in-progress`
- `epic-3: in-progress`

Add a support-safe comment stating that v3.75.0 has valid observed package hashes
but a single `linux/amd64` image, so Story 3.12 must produce a later exact
two-platform OCI index before Story 1.20 can consider release identity complete.

### 4.4 Release Guidance And Evidence

During Story 3.12 implementation:

- Update `docs/ci.md` to say the EventStore version tag resolves to an OCI image
  index containing exactly `linux/amd64` and `linux/arm64`.
- Document the registry inspection, child-config validation, digest binding, and
  both-platform smoke commands without embedding credentials.
- Add the new release evidence to the Story 1.20 packet as observed data first;
  only Story 1.20's later owner-approved authorization sequence may promote it.
- Preserve existing supply-chain backlog boundaries.

## 5. Implementation Handoff

### Scope Classification

**Moderate** - the product correction is narrow, but implementation crosses the
EventStore release caller and the shared Hexalith.Builds publisher, culminates in
an external release, and feeds a separately governed authorization packet.

### Recipients And Responsibilities

- **Product Owner / backlog maintainer:** approve Story 3.12's scope and add it to
  Epic 3 and the sprint tracker.
- **Hexalith.Builds maintainer:** own, review, validate, and commit the shared
  multi-platform publisher and its contract tests in the Builds repository.
- **EventStore Developer:** integrate the corrected shared contract, add caller
  validation/docs, preserve the 14-package inventory, and assemble evidence.
- **Test Architect:** review exact-platform, negative-fixture, digest, child config,
  smoke, and package-byte evidence.
- **Release owner:** provide separate durable authority before publication and a
  distinct disposition after inspecting the resulting immutable artifacts.
- **Story 1.20 EventStore owner:** independently decide whether the later release
  may populate approved fields and participate in consumer-migration approval.

### Success Criteria

- v3.75.0's 14 exact package hashes and single-platform failure are durably
  recorded as non-authorizing evidence.
- Story 3.12 exists in Epic 3 and the sprint tracker at `backlog` after approval.
- A later semantic release publishes exactly 14 packages and one `eventstore`
  OCI index containing exactly `linux/amd64` and `linux/arm64`.
- Raw-index digest, child descriptors/configs, and both platform smokes pass;
  single, missing, duplicate, extra, unknown, or mismatched platforms fail.
- v3.75.0 remains immutable.
- Story 1.20 and Epic 1 remain non-done until their independent evidence,
  approval, and authorization gates all pass.
- No Parties, Tenants, payload-protection G5, extra package/container, submodule,
  commit, or push change is smuggled into the corrective story.

## Appendix A - Change Navigation Checklist

### 1. Understand The Trigger And Context

- [x] 1.1 Triggering story: Story 1.20, specifically Acceptance Boundary 8 and
  its scoped-corrective-item rule.
- [x] 1.2 Core problem: a failed release approach. The shared publisher emits
  only `linux/amd64`, while AC8 requires an exact two-platform OCI index.
- [x] 1.3 Evidence: v3.75.0 release assets and hashes, immutable registry manifest
  and config, successful release run, and the shared publisher's hard-coded
  `--arch x64` command.

### 2. Epic Impact Assessment

- [x] 2.1 Epic 1 remains completable but Story 1.20 stays blocked.
- [x] 2.2 Add focused Story 3.12 to existing Epic 3; do not create a new epic.
- [x] 2.3 Remaining epics were reviewed and require no change.
- [x] 2.4 No epic is obsolete and no additional epic is required.
- [x] 2.5 Priority is explicit: Story 3.12 precedes any release-identity closure
  attempt in Story 1.20; other epic order is unchanged.

### 3. Artifact Conflict And Impact Analysis

- [x] 3.1 PRD goals and MVP remain valid; no PRD text change is required.
- [x] 3.2 AD-11, AD-12, and AD-22 already govern the correction; no architecture
  decision change is required.
- [N/A] 3.3 UI/UX is unaffected.
- [x] 3.4 Story 1.20 evidence, Epic 3, sprint status, the shared publisher, release
  validation/tests, and CI guidance require coordinated changes.

### 4. Path Forward Evaluation

- [x] 4.1 Direct Adjustment is viable: medium effort, medium technical risk, and
  a high consequence if validation remains absent.
- [x] 4.2 Rollback is not viable or appropriate because v3.75.0 is already
  published and immutable.
- [x] 4.3 MVP review is unnecessary; requirements and product scope remain valid.
- [x] 4.4 Direct Adjustment selected.

### 5. Sprint Change Proposal Components

- [x] 5.1 Issue summary and exact evidence completed.
- [x] 5.2 Epic, story, artifact, and technical impacts documented.
- [x] 5.3 Recommended path and rejected alternatives documented.
- [x] 5.4 MVP impact and ordered action plan documented.
- [x] 5.5 Product, Builds, developer, test, release-owner, and Story 1.20 handoffs
  defined.

### 6. Final Review And Handoff

- [x] 6.1 Applicable checklist sections addressed; approval-dependent actions are
  explicitly identified.
- [x] 6.2 Proposal checked against the observed release bytes, registry object,
  Story 1.20 AC8, AD-11, AD-12, and AD-22.
- [x] 6.3 Administrator explicitly approved the complete proposal on 2026-07-19.
- [x] 6.4 Story 3.12 was added to Epic 3 and the sprint tracker at `backlog`.
- [x] 6.5 The moderate implementation handoff is routed; external publication
  still requires separate durable release-owner authority.
