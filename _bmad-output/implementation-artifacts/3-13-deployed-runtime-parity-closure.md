---
baseline_commit: 4bcf2484a09eb26490cb2d32ceb6df8949f90cc6
created: 2026-08-01
story_id: "3.13"
story_key: 3-13-deployed-runtime-parity-closure
epic: "Epic 3 - Release And Repository Reliability"
requirements: FR36
governing_nfrs: NFR12, NFR16
related_release_nfrs: NFR9
architecture_decisions: AD-11, AD-12, AD-22
story_type: evidence-only-deployed-runtime-closure
dependencies:
  - 1-20-owner-approved-parity-closure-and-runtime-pin: done
  - 3-12-multi-platform-eventstore-container-publishing-correction: done
source_files:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-01.md
  - _bmad-output/planning-artifacts/story-id-migration-2026-08-01.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01-post-correction.md
  - _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md
  - _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
  - _bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md
  - _bmad-output/implementation-artifacts/evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/
  - _bmad-output/project-context.md
  - tools/release-packages.json
  - .github/workflows/release.yml
  - .releaserc.json
  - scripts/validate-publication-preflight.sh
  - references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py
  - references/Hexalith.Builds/Github/publish-containers/smoke_container_platforms.py
---

# Story 3.13: Deployed Runtime Parity Closure

Status: review

<!-- Ultimate context engine analysis completed - comprehensive developer guide created. -->

> Authority gate: this is an evidence-only closure story. It grants no authority to publish or
> replace packages, tags, manifests, images, or registry objects; deploy an image; change a
> consumer; modify Stories 1.20 or 3.12; edit a submodule; approve G5; or infer human approval.
> Registry reads, artifact downloads, digest verification, bounded ephemeral smoke, and creation
> of this story's support-safe evidence are in scope. Any external mutation requires a separate,
> explicit authority record and a separately approved story.

## Story

As an **EventStore release owner**,
I want **deployed runtime identity mapped back to the approved source/package parity evidence**,
so that **operators can select a conforming image without creating a forward dependency in Epic 1**.

## Story Context

The August readiness correction deliberately separates two closure modes. Story 1.20 remains
complete for source/package parity inside Epic 1. Story 3.12 remains complete for correcting the
shared release publisher and producing a conforming two-platform release. This story independently
closes deployed mode by proving one immutable identity chain; it cannot gate, reopen, or rewrite
either predecessor or Epic 1.

One chain means one exact EventStore source SHA, one exact 14-package ID/version/hash inventory,
one release identity and workflow run, one durable release-owner authority, one OCI index digest,
and the two child-manifest/config chains and runtime results derived from that index. An ancestor,
descendant, branch, tag, consumer SHA, package version from another build, or merely compatible
image cannot substitute for the exact source identity.

### Known starting evidence — not a closure result

| Evidence packet | Source/package identity | Container/release identity | Initial disposition |
| --- | --- | --- | --- |
| Story 1.20 approved parity packet | Source/runtime `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`; package version `999.1.20-proof.fa2d1c9910f8`; package-hash manifest `4271ddc76411780591423ab024b776cd34a2abccf1cc2dac03a245e141dbe0bc` | Index `sha256:523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87` under `quarantine-proof-fa2d1c...`; publication authority explicitly said “no deployment or consumer migration” | Approved Story 1.20 evidence input, but not yet a Story 3.13 deployed-release crosswalk |
| Story 3.12 corrective release | Release `v3.77.2`; source `77a9a442c0e6d0408957888e10c3a9accd634c99`; 14 packages at `3.77.2`; Builds `9ec0a032d785dd0abdc14276e8784d6fdd826fd0`; workflow `29694935552` | Index `sha256:db3ab41e187efc0de397fd1205660a0f685e2c94ecd8f4a8f1843ac567056bf6`; exact `linux/amd64` and `linux/arm64` children/configs and smokes | Conforming Story 3.12 release input, but not the Story 1.20-approved source/package identity |

The v3.77.2 source is 103 commits behind the Story 1.20-approved source in the inspected Git graph.
An ancestor relationship is not exact provenance and must not be presented as parity. Likewise,
the Story 1.20 proof index cannot be called a semantic release merely because its digest is valid.
The implementation must investigate each candidate independently and must not splice the source or
package half of one row to the release/index half of the other.

### Current fail-closed discovery — revalidate during implementation

A read-only registry inspection on 2026-08-01 resolved the Story 1.20 proof index to these exact
children and configs:

| Platform | Child manifest | Config | Observed config labels |
| --- | --- | --- | --- |
| `linux/amd64` | `sha256:a47978374abf10a033f6d0b63610b05c13496335324371cbf73b155dd295ff6d` | `sha256:31d1ed8f2503eae20c026c9ce452525ea77ab453b484b66e1c1f6aab16f545fd` | version `3.82.0`; no revision label |
| `linux/arm64` | `sha256:bb245eea690cfe521098441c55a1710f81f722591a245f84c2fa45e4707fdc86` | `sha256:10a00fc7c19abb37061a8b86e1b234cbbb2fe7af85728b527f4449a326ac0a20` | version `3.82.0`; no revision label |

The `v3.82.0` repository tag resolves to source
`0b12950f12e9365fd48e3fe085ab626f9d09dfc5`, not the Story 1.20-approved `fa2d1c...` source.
The Story 1.20 authority record also limits that publication to quarantined two-platform proof and
expressly grants no deployment or consumer-migration authority. These observations are discovery
inputs, not retained Story 3.13 proof: the implementation must repeat and preserve the raw-byte
checks. Unless a single candidate independently resolves every mismatch, the only correct outcome
is a reproducible `fail-closed` packet and a non-`done` story.

Story-creation fingerprints for immutable-input comparison are:

- Story 1.20 record SHA-256:
  `0feee912874154a3885fbe69ac68419c89b209b8c9c5b9291833604881f34fa5`.
- Story 1.20 proof-packet SHA-256:
  `cb1ccde9d5cc5ca6cb52cbeab30fb9cd59bd89771e14f4b489e20bd5e3d46743`.
- Story 3.12 record SHA-256:
  `2bfc9ff991c9aeeaf11fd9c1926a17bb44ca290f99bd75b05df68a6edaf3e09c`.

The implementation must recompute these values from its own baseline. A changed fingerprint is an
input change to investigate, not permission to overwrite a predecessor or copy these values as a
passing verification result.

## Acceptance Criteria

### AC1 - Freeze completed predecessor evidence without rewriting history

**Given** Stories 1.20 and 3.12 are complete
**When** deployed-runtime closure begins
**Then** their exact evidence packets, committed evidence files, decisions, and immutable identities
are referenced and hash-checked without modification
**And** no missing field is inferred from a tag, branch, consumer SHA, current `main`, compatible
version, or mutable registry reference.

### AC2 - Prove one exact source/package/release/deployed identity chain

**Given** an EventStore OCI index is proposed as deployed parity evidence
**When** the identity crosswalk is assembled
**Then** one approved EventStore source SHA and the exact 14 package IDs, one version, and per-package
SHA-256 values map through one release version, workflow run, and durable release-owner authority to
the exact OCI index digest and both required child-manifest/config identities
**And** every raw digest, byte length, media type, platform relation, provenance field, and runtime
result is independently revalidated rather than copied as a pass from either predecessor.

### AC3 - Fail closed on every missing or inconsistent identity

**Given** any source, package byte, index, child manifest, config, authority, approval, provenance,
or runtime result is missing, unavailable, mutable-only, expired, or inconsistent
**When** closure is evaluated
**Then** the crosswalk records the exact failing field and Story 3.13 remains non-`done`
**And** Story 1.20, Story 3.12, Epic 1, consumer dependencies, package publications, registry objects,
and deployments remain unchanged.

### AC4 - Require named acceptance of the exact final packet

**Given** the complete, internally consistent packet has passed independent revalidation
**When** the EventStore owner, Release owner, and Test Architect accept the same content-bound
identity crosswalk in durable sources
**Then** Story 3.13 may become `done` with the exact deployed OCI index identity recorded
**And** the decision authorizes no consumer migration, package publication, registry mutation,
deployment mutation, Story 1.20/3.12 status change, or G5 classification.

## Tasks / Subtasks

- [x] **Task 1 - Reconfirm baseline, predecessors, scope, and authority (AC1-AC4).**
  - [x] Re-read root guidance, project context, relevant architecture, `.editorconfig`,
    `.gitattributes`, current branch/worktree/remotes, and recent history before writing evidence.
  - [x] Verify sprint tracking still has Epic 3 `in-progress`, Story 1.20 `done`, Story 3.12 `done`,
    and this story no farther than `ready-for-dev`; never change either predecessor or Epic 1.
  - [x] Record exact current EventStore and root-declared Builds gitlink/check-out SHAs. Do not
    initialize nested submodules, update dependencies, or edit submodule content.
  - [x] Separate read-only registry/artifact inspection and ephemeral smoke authority from commit,
    push, publication, registry, deployment, consumer, and approval authority.

- [x] **Task 2 - Freeze the two predecessor packets as immutable inputs (AC1, AC3).**
  - [x] Record Git blob hashes and SHA-256 values for the Story 1.20 story/packet, its selected
    `fa2d1c...` committed evidence directory, and the Story 3.12 story record before analysis.
  - [x] Run `sha256sum -c` against Story 1.20's committed `critical-evidence-sha256.txt` from the
    correct evidence directory; record missing, renamed, or mismatched files as failure.
  - [x] Extract source, package, release, workflow, Builds, authority, index, child, config, smoke,
    owner, and evidence-retention identities into separate candidate rows. Preserve `v3.75.0` and
    `v3.77.1` as failed/quarantined history and never select them.
  - [x] Do not edit, normalize, regenerate, or “correct” either predecessor packet. Later planning
    supersedes old ownership wording, but historical bytes remain evidence.

- [x] **Task 3 - Create a field-complete, content-bound crosswalk (AC1-AC3).**
  - [x] Create
    `_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md`
    as the human-readable decision packet and add one support-safe evidence directory at
    `_bmad-output/implementation-artifacts/evidence/story-3-13/<approved-source-sha>/<validated-index-sha256>/`;
    do not add Story 3.13 output beneath Story 1.20's evidence tree.
  - [x] Create `identity-crosswalk.json` with an explicit schema/version and fields for source SHA,
    package manifest path/hash, all 14 package rows, release/version/tag, workflow run/attempt,
    Builds execution SHA, release authority URL/hash/scope, registry/repository, index media type/
    digest/size/raw hash, two child descriptors, two config descriptors/platforms, smoke results,
    predecessor input hashes, limitations, and final `pass` or `fail-closed` verdict.
  - [x] Give every field a source citation and independent verification result. A copied value with
    no verification method/result is `unverified`, not `pass`.
  - [x] Require all selected fields to belong to one candidate row. Reject union, fallback, or
    “latest available value” logic across Story 1.20, v3.77.2, later tags, or current `main`.

- [ ] **Task 4 - Revalidate exact source and package identities (AC2, AC3).**
  - [ ] Require the selected release provenance source to equal—not merely contain or descend
    from—the Story 1.20-approved `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`, unless a new
    owner-reviewed parity packet explicitly approves a different exact SHA outside this story.
  - [x] Parse `tools/release-packages.json` structurally and require exactly its current 14 IDs,
    no duplicate IDs/projects, one selected version, and no package outside the manifest.
  - [ ] Independently obtain and hash the exact selected package bytes. Require every ID/version/
    SHA-256 tuple to equal the approved packet and bind the sorted per-package manifest bytes to the
    approved manifest hash.
  - [x] Treat the recorded unrecoverability of the `999.1.20-proof.fa2d1c9910f8` package bytes as a
    known blocker unless the exact original bytes are recovered from a content-addressed source and
    rehashed. Rebuilding similar packages or trusting the recorded hash list is not independent
    byte verification.
  - [x] Do not apply the Story 2.12 Tenants-only AD-22 exception: it explicitly grants no deployed-
    mode or other-consumer relief.

- [x] **Task 5 - Revalidate the immutable OCI graph from registry bytes (AC2, AC3).**
  - [x] Resolve the proposed tag only as discovery input; immediately bind all evidence to the
    immutable index digest. Re-fetch the index by digest with an OCI-index `Accept` header and
    require the tag and digest responses to be byte-identical when a tag is part of the candidate.
  - [x] Verify `Docker-Content-Digest`, exact raw bytes/hash/length, `schemaVersion: 2`, and media
    type `application/vnd.oci.image.index.v1+json`.
  - [x] Require exactly two direct image descriptors: one `linux/amd64` and one `linux/arm64`, with
    no duplicate, extra, nested index, `unknown`, or non-empty variant entry.
  - [x] Resolve every child manifest by digest; verify raw digest, size, descriptor/response media
    type, config descriptor digest/size, raw config digest/size, and config `os`/`architecture`
    equality with the parent descriptor.
  - [x] Retain support-safe raw index, child-manifest, and config bytes plus a sorted checksum
    manifest. Never retain registry credentials or authorization headers.
  - [x] Reuse the SHA-pinned Hexalith.Builds validation contract for a semantic release candidate.
    The current validator accepts a SemVer tag for initial resolution; it cannot directly accept
    the non-SemVer `quarantine-proof-*` tag. Do not weaken or fork it in this story. Use an already
    approved immutable-digest verification path or record the tool/candidate incompatibility as a
    fail-closed result.

- [ ] **Task 6 - Re-run equivalent, digest-pinned runtime evidence (AC2, AC3).**
  - [ ] Run the same bounded support-safe `/alive` smoke against each immutable child digest, with
    the same minimal configuration, timeout, polling, 2xx-without-redirect expectation, cleanup,
    and log-redaction contract for both platforms.
  - [x] Run arm64 emulation/runtime readiness before arm64 product smoke. Classify environment/
    emulation setup failure separately from image pull/start failure and liveness failure; every
    non-pass blocks closure.
  - [ ] Record child digest, observed runtime platform, command contract, start/end time, exit code,
    bounded log hash, readiness result, and cleanup result. Do not treat workflow success, image
    pull, process start, or the parent index alone as runtime proof.
  - [x] If an actual deployed instance is inspected, observe its image identity and map an observed
    index, child, or config digest only through the frozen selected chain. An identity absent from
    the chain fails closed; never derive it from a mutable deployment tag.

- [ ] **Task 7 - Verify release provenance and durable authority (AC2, AC3).**
  - [ ] Bind one release version/tag, workflow run and attempt, source/tag commit relationship,
    Builds execution SHA, publisher/validator identity, package inventory, and container index to
    the same release event.
  - [x] Hash and validate the durable release-owner authority record. Require repository, exact
    source SHA, version/tag, container repository, platform scope, owner, date, rationale, and
    validity at the original action time; an expired record may prove historical authorization but
    cannot authorize a new mutation.
  - [x] Require the selected candidate's retained evidence to be durable and content-bound.
    Expired GitHub Actions artifacts, inaccessible package bytes, mutable issue text without a
    frozen hash, or registry tags without digest read-back are missing evidence.
  - [x] Keep signing, SBOM, attestations, trusted publishing, and credential modernization out of
    scope. Their absence does not weaken this story's required provenance fields and this story
    does not claim to implement them.

- [ ] **Task 8 - Produce the fail-closed verdict before requesting review (AC2, AC3).**
  - [ ] Run a structural verifier over `identity-crosswalk.json` and the checksum manifest. Require
    exact field presence, one candidate identity, exact package/platform sets, and all independent
    checks passing.
  - [x] Add focused regression coverage in
    `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs`
    for the crosswalk schema, exact 14-package/two-platform sets, selected hashes/verdict, and
    approval-subject binding. Use existing test dependencies; do not change a project file.
  - [x] Explicitly test the known prohibited splice: Story 1.20 source/packages plus v3.77.2
    release/index must fail. Also reject v3.77.2 source/packages plus the Story 1.20 proof index.
  - [x] If no single candidate passes, record `fail-closed`, the exact blockers and evidence
    attempted, and the separately owned corrective action needed. Do not invent a release, request
    publication under this story, or change any completed status.
  - [x] This story may enter `review` only after the focused crosswalk exists and its verdict is
    reproducible. It may become `done` only for a complete `pass` packet under AC4.

- [ ] **Task 9 - Obtain content-bound owner and Test Architect acceptance (AC4).**
  - [x] Freeze a review subject containing the final crosswalk hash, evidence-manifest hash,
    source/package/release/index identities, limitations, and proposed decision.
  - [ ] Obtain distinct durable acceptance from the EventStore owner and Release owner plus Murat's
    Test Architect review. Verify reviewer roles, exact subject hash, accepted scope, limitations,
    decision, date, and durable source; do not infer approval from story creation or prior approval.
  - [ ] Rehash the packet after approvals. Any evidence/content change invalidates the approvals
    until all three reviewers accept the replacement subject.
  - [ ] For a passing packet, record only the deployed identity decision. Preserve the explicit
    prohibition on consumer migration, deployment, publication, registry mutation, and G5.

- [x] **Task 10 - Validate and hand off without scope leakage (AC1-AC4).**
  - [x] Validate JSON structure, all checksum manifests, predecessor immutability, support-safe
    output, exact package/platform sets, Git diff hygiene, and the story/sprint status transition.
  - [x] Confirm no runtime source, workflow, release configuration, manifest, submodule, Story 1.20,
    Story 3.12, consumer, deployment, or registry object changed under Story 3.13.
  - [x] Record exact commands/results and environmental blockers in the Dev Agent Record. Do not
    report a blocked or unavailable check as passed.
  - [x] Update only the stale Story 3.12-to-Story 1.20 ownership paragraph in `docs/ci.md` to name
    Story 3.13 as the deployed-runtime closure owner. Preserve the documented release mechanics,
    all historical records, and Story 1.20's source/package-only authority.
  - [x] If AC4 passes, move this story through review to `done`; otherwise leave it non-`done` with
    the focused blocker record. Never change Story 1.20, Story 3.12, or Epic 1.

## Dev Notes

### Governing authority and traceability

- Amelia (Developer) assembles the identity crosswalk. The EventStore owner and Release owner
  approve the exact deployed identity, and Murat (Test Architect) reviews the immutable
  index/child/config and runtime evidence. Story implementation must not infer any of these
  acceptances from role assignment or predecessor approval.
- The PRD owns FR/NFR meaning; the epics file owns Story 3.13 slicing and ACs; architecture AD-11,
  AD-12, and AD-22 own the immutable release, evidence, and exact-SHA rules.
- The 2026-08-01 correction and migration crosswalk supersede older Story 3.12 wording that handed
  deployed evidence back to Story 1.20. Preserve the old bytes as history; follow the new ownership.
- Canonical Story 3.13 names FR36, NFR12, and NFR16. The PRD high-risk map also includes 3.13 under
  NFR9. Treat NFR9's reproducibility/package-safe constraint as applicable without silently
  rewriting the canonical story header.
- UX artifacts are non-impacting: this story changes no UI, route, component, localization,
  accessibility behavior, or FrontComposer/Fluent UI dependency.

### Current state, change, and preservation boundaries

| File / area | Current state | Story 3.13 action | Must preserve |
| --- | --- | --- | --- |
| `1-20-owner-approved-parity-closure-and-runtime-pin.md` and proof packet | Story 1.20 is `done`/`available` for the exact `fa2d1c...` packet and still contains historical container evidence | **READ/VERIFY ONLY** | Status, decisions, approved identities, A/B/C history, and all historical failures |
| `evidence/story-1-20/fa2d1c.../` | Committed critical evidence includes source/package hash lists, an OCI index, child descriptors, provenance, runtime log hashes, publication authority, owner decisions, and WORM-bundle pins; it does not commit the original package bytes or raw child/config objects | **READ/HASH/REFERENCE ONLY** | Directory bytes and checksum manifest; never add Story 3.13 evidence here |
| `3-12-multi-platform-eventstore-container-publishing-correction.md` | Completed v3.77.2 publisher correction and release record, including exact 14 packages, public release, OCI graph, and smoke evidence | **READ/VERIFY ONLY** | `done` status, v3.75.0 failed record, v3.77.1 quarantine, v3.77.2 historical evidence, review caveats |
| `tools/release-packages.json` | Current exact 14-package source of truth | **READ/STRUCTURALLY VERIFY** | No package addition/removal/reordering or release-scope change |
| `.github/workflows/release.yml`, `.releaserc.json`, publication preflight | Current release path is manually dispatched, requires exact green `main`, pins shared Builds execution, and gates publication | **READ/VERIFY ONLY** | No release dispatch, workflow edit, semantic-release, or secret use under this story |
| Shared Builds OCI validator/smoke | Reusable exact two-platform validation and bounded child smoke; live caller and current submodule may differ from Story 3.12's historical execution SHA | **REUSE/VERIFY IDENTITY; DO NOT EDIT** | Shared ownership, exact platforms, support-safe errors, no duplicate local validator |
| `3-13-deployed-runtime-parity-closure.md` | This ready-for-dev implementation guide and future execution record | **UPDATE during execution** | AC wording, authority boundary, exact-source/no-splice rule, honest status |
| `3-13-deployed-runtime-parity-closure-proof-packet.md` | Does not exist | **NEW decision packet** | Exact evidence references, fail-closed/pass verdict, limitations, content-bound approvals |
| `evidence/story-3-13/<approved-source-sha>/<validated-index-sha256>/` | Does not exist | **NEW only when independently verified evidence is captured** | Support-safe, content-addressed, self-describing evidence; no credentials/raw tokens |
| `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs` | Does not exist | **NEW focused evidence-contract tests** | Existing project/dependencies, deterministic local assertions, no live secrets or registry mutation |
| `docs/ci.md` | Release mechanics are authoritative, but one ownership paragraph still routes deployed evidence from Story 3.12 to Story 1.20 | **UPDATE that paragraph only** | Release mechanics and historical evidence; Story 1.20 remains source/package-only |
| `sprint-status.yaml` | Story 3.13 becomes `ready-for-dev` when this file is created | **UPDATE only for truthful lifecycle transitions** | Comments/order/status definitions, Story 1.20/3.12/Epic 1 statuses, non-regressing date |

### Architecture compliance and anti-pattern guards

- **AD-11:** release inventory is `tools/release-packages.json`; the only released container mapping
  is `eventstore`; the accepted outer object is an immutable OCI index with exactly two direct
  platform children. Tag resolution never authorizes deployment; only the validated index digest
  can identify the deployed artifact.
- **AD-12:** status codes, workflow success, mocks, tag existence, descriptor labels, or prior
  prose are not sufficient. Persist raw registry bytes, package bytes/hashes, runtime results, and
  content-bound review evidence.
- **AD-22:** deployed mode maps the running image through release provenance to the exact approved
  EventStore SHA. An observed child/config digest maps to an index only through the recorded graph.
  The consumer repository SHA is never compared with the EventStore SHA.
- Do not create a new release pipeline, Dockerfile, image repository, package, container mapping,
  package version, deployment profile, or source implementation. If evidence reveals a missing
  artifact, report the gap; fixing it belongs to separately authorized release work.
- Do not broaden into signature, attestation, SBOM, trusted-publishing, secret-rotation, catalog,
  Story 3.11, consumer adoption, Parties/Tenants, or payload-protection work.

### Tooling and framework requirements

- Repository SDK remains .NET `10.0.302`, target `net10.0`; no dependency or SDK update is required.
- Use the existing shared Builds OCI validator and smoke contract where the selected candidate fits
  their input contract. Pin and record the actual validator/smoke bytes or Builds SHA used.
- Use OCI Distribution manifest/blob reads for independent digest verification. Preserve response
  body bytes exactly; JSON reserialization changes bytes and cannot prove the registry digest.
- Registry credentials, if already configured for read access, are task-relevant credentials but
  must never be printed, persisted, forwarded across origins, or included in evidence.
- A mutable tag is discovery metadata only. Deployment/closure identity is
  `registry.hexalith.com/eventstore@sha256:<index>`.
- Do not add package references. If a small verifier is needed, prefer a repository-owned script
  beside Story 3.13 evidence or a focused test using existing dependencies; do not copy the shared
  publisher implementation.

### File structure requirements

- Keep all new closure evidence under one content-addressed Story 3.13 directory. Use deterministic,
  descriptive names such as `identity-crosswalk.json`, `index.json`,
  `child-linux-amd64.json`, `config-linux-amd64.json`, `child-linux-arm64.json`,
  `config-linux-arm64.json`, `package-sha256.txt`, `smoke-linux-amd64.log`,
  `smoke-linux-arm64.log`, `review-subject.json`, and `evidence-sha256.txt`.
- Store raw registry JSON bytes without pretty-printing when those bytes are digest evidence. Put
  parsed summaries in separate files rather than rewriting raw evidence.
- Keep the focused evidence-contract tests in the existing Contracts test project; do not add or
  update package references. If a verifier is added, keep it scoped to Story 3.13 evidence. A
  reusable release validator change belongs to Hexalith.Builds and requires separate authority.
- Follow `.gitattributes`: Markdown and shell files use LF. Preserve JSON formatting used by
  adjacent evidence. Never commit transient `bin`, `obj`, package caches, credentials, or containers.

### Testing and evidence requirements

- **Predecessor integrity:** Git blob hashes plus SHA-256/checksum-manifest verification.
- **Crosswalk structure:** required-field schema, exact one-candidate rule, exact 14 package IDs,
  exact two platforms, unique digest/config rows, and explicit pass/fail for every field.
- **Negative controls:** both prohibited cross-lineage combinations, mutable-tag-only input,
  missing package bytes, missing child/config, digest/size/platform mismatch, expired/inaccessible
  evidence, and missing/mismatched approval subject.
- **Live registry proof:** raw index, both child manifests, both configs, immutable digests/sizes,
  and exact platform relationships read back from the registry.
- **Runtime proof:** equal bounded `/alive` smoke on both immutable children; environment failure is
  distinct but equally blocking.
- **Approval proof:** three named roles accepting the same final content hash.
- Run tests per project if code/tests are added; never run solution-level `dotnet test`. Use xUnit
  v3, Shouldly, PascalCase test names, and `ConfigureAwait(false)` on awaited C# calls.
- Because this is evidence-only, a solution rebuild is not a substitute for live identity evidence.
  If no code/config changes occur, the narrow final checks are evidence verification, structural
  validation, `git diff --check`, and scope/status inspection.

### Previous Story Intelligence

- Story 3.12 proved the exact release shape and introduced reusable fail-closed validation. Reuse
  its raw-byte, exact-set, child/config, equivalent-smoke, environment-vs-product, cleanup, and
  support-safe evidence rules.
- Story 3.12's v3.77.2 result is historical proof that the corrected publisher can work, not proof
  that v3.77.2 equals Story 1.20's later approved runtime.
- Story 3.12 review records that its implementation SHA was superseded on `main`; pin the historical
  validator used for historical claims and the actual current validator used for new read-only
  verification. Do not claim they are the same.
- The Story 3.12 Actions artifact was recorded with an expiry of 2026-08-18. Retrieve and hash any
  still-required bytes before expiry or record them unavailable; never infer expired bytes from
  summaries.
- Preserve earliest causal failure and exact cleanup results. A retry or later pass does not erase
  an earlier failed/quarantined artifact identity.

### Git intelligence

- `8f004ecf` introduced the August correction, Story 3.13, crosswalk, architecture changes, and
  post-correction readiness report. It is the key planning-history change for this story.
- `77d6f477` and `dbf81916` refreshed and authorized Story 1.20 evidence. Read current packet bytes;
  do not use earlier review-state assumptions.
- `4bcf2484` is the current story-creation baseline and updated sprint/submodule state. Preserve
  unrelated submodule identities and user work.
- Recent runtime/test changes are unrelated to this evidence-only slice. Do not pull them into the
  file list merely because they are newer than the approved runtime.
- The inspected v3.77.2 source is an ancestor of the approved Story 1.20 source by 103 commits.
  That fact is diagnostic only; AD-22 requires exact equality, not ancestry.

### Suggested non-mutating validation commands

Adapt paths to the selected candidate and capture only support-safe output:

```bash
git status --short --branch
git diff --check

evidence='_bmad-output/implementation-artifacts/evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594'
(
  cd "$evidence"
  sha256sum -c critical-evidence-sha256.txt
)

jq -er '
  .packages as $packages |
  ($packages | length) == 14 and
  ($packages | map(.id) | unique | length) == 14 and
  ($packages | map(.project) | unique | length) == 14
' tools/release-packages.json

git merge-base --is-ancestor \
  77a9a442c0e6d0408957888e10c3a9accd634c99 \
  fa2d1c9910f8976553adb33dcdb1c9ff2ea75594
test "$(git rev-list --count \
  77a9a442c0e6d0408957888e10c3a9accd634c99..fa2d1c9910f8976553adb33dcdb1c9ff2ea75594)" = 103
```

The ancestry commands prove the known mismatch; they do not satisfy exact-source equality. Use the
existing shared validator/smoke entry points for a SemVer release candidate only after confirming
their exact revision and read-only/live-smoke authority. Do not place registry credentials on a
command line or in the story record.

### Latest technical information

- OCI Image Specification `1.1.1` defines the index, descriptor, and image-config contracts used
  by this story. The OCI Image Index uses media type
  `application/vnd.oci.image.index.v1+json`; its descriptors carry digest, size, media type, and
  platform fields. This story's exact two-platform/no-variant rule is stricter than the general OCI
  specification and remains authoritative.
- OCI Distribution Specification `1.1.1` supports manifest retrieval by tag or digest and returns
  `Docker-Content-Digest`. A client that uses a digest must verify returned content against it;
  exact response bytes, not reserialized JSON, are the digest input.
- OCI image manifests point to config descriptors, and config JSON carries the runtime
  architecture/OS that must agree with the parent descriptor.
- Docker documents `docker buildx imagetools inspect --raw` as returning the original unformatted
  manifest JSON. It is useful independent corroboration, but the story still binds registry headers,
  byte lengths, child manifests, and config blobs rather than trusting one CLI summary.
- Kubernetes supports `image@sha256:<digest>` and uses the digest when a tag and digest are both
  present. A runtime `imageID` may identify a selected child/config rather than the top-level index;
  map it only through the frozen index graph and strip only a known runtime prefix such as
  `docker-pullable://` or `containerd://`.
- `dotnet nuget verify --all` verifies package signatures, not the package-file SHA-256 required by
  this crosswalk. NuGet.org repository signing can add `.signature.p7s` and change the archive hash;
  preserve unsigned release-asset and signed NuGet.org identities as different byte domains.
- Signatures, referrers, SBOMs, and attestations may be inventoried if already present, but their
  presence cannot substitute for the source/package/index/child/config chain and their absence is
  not a failure under this story. Do not add Cosign, ORAS, SLSA, or GitHub attestation requirements.
- Existing repository constraints—not the existence of newer tooling—govern versions. Do not
  upgrade .NET, Docker/buildx, or introduce Cosign/ORAS solely for this evidence story.

### Project Structure Notes

- The implementation is limited to Story 3.13 evidence, its proof packet/story record, the focused
  Contracts test, the narrow `docs/ci.md` ownership correction, and truthful sprint tracking. Runtime
  source, release workflows, package manifests, container build configuration, deployments,
  consumers, and submodules are outside the authorized change set.
- The story file is the human-readable execution and review record. The content-addressed evidence
  directory is the machine-verifiable record; neither replaces the immutable predecessor packets.
- No UI/UX, DAPR/Aspire topology, API contract, database/state schema, package API, or runtime
  behavior changes are expected.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-313-Deployed-Runtime-Parity-Closure]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-3-Release-And-Repository-Reliability]
- [Source: _bmad-output/planning-artifacts/prd.md#68-Consumer-ProjectionQuery-Parity-Closure]
- [Source: _bmad-output/planning-artifacts/prd.md#7-Cross-Cutting-Non-Functional-Requirements]
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-11---Release-Is-Manifest-Governed-ADOPTED]
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-12---High-Risk-Verification-Requires-Persisted-Evidence-ADOPTED]
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-22---Consumer-Infrastructure-Removal-Requires-Owner-Approved-Exact-SHA-Parity-ADOPTED]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-01.md#41-Remove-The-Epic-1-Forward-Dependency]
- [Source: _bmad-output/planning-artifacts/story-id-migration-2026-08-01.md#Dependency-And-Scope-Corrections]
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01-post-correction.md#Dependency-Analysis]
- [Source: _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md#Artifact-Identity-Pin]
- [Source: _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md#Owner-Review]
- [Source: _bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md#Acceptance-Criteria]
- [Source: _bmad-output/implementation-artifacts/3-12-multi-platform-eventstore-container-publishing-correction.md#Debug-Log-References]
- [Source: tools/release-packages.json]
- [Source: .github/workflows/release.yml]
- [Source: references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py]
- [Source: references/Hexalith.Builds/Github/publish-containers/smoke_container_platforms.py]
- [OCI Image Index Specification](https://github.com/opencontainers/image-spec/blob/main/image-index.md)
- [OCI Descriptor Specification](https://github.com/opencontainers/image-spec/blob/main/descriptor.md)
- [OCI Image Configuration Specification](https://github.com/opencontainers/image-spec/blob/main/config.md)
- [OCI Image Specification Releases](https://github.com/opencontainers/image-spec/releases)
- [OCI Distribution Specification](https://github.com/opencontainers/distribution-spec/blob/main/spec.md)
- [Docker `imagetools inspect`](https://docs.docker.com/reference/cli/docker/buildx/imagetools/inspect/)
- [Kubernetes Images](https://kubernetes.io/docs/concepts/containers/images/)
- [.NET NuGet Signature Verification](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-verify)

## Dev Agent Record

### Agent Model Used

OpenAI Codex (GPT-5)

### Debug Log References

- `sha256sum -c critical-evidence-sha256.txt` from the Story 1.20 evidence directory:
  33/33 entries passed.
- Exact predecessor SHA-256 values matched the story-creation fingerprints; the full selected
  Story 1.20 evidence tree remains Git tree `fcd0c25c9cf6bb0554e208d529f1ef09c223725a`
  with 40 files.
- The current shared OCI validation functions read the proof tag and immutable index plus both
  children/configs from the registry; all media types, digests, sizes, and platforms passed.
- The current shared smoke contract ran from `2026-08-04T11:10:03.248829Z` through
  `2026-08-04T11:12:03.469486Z`: amd64 passed after 18 polls, arm64 passed after 40 polls,
  and both cleanup paths passed. The run used `Development`, while `docs/ci.md` requires
  `Production`; runtime contract equivalence therefore remains a blocker.
- Both retained configs set the OCI source, URL, and documentation labels to the malformed value
  `https`. The byte graph passes, but provenance-label validation fails closed.
- Exact local proof-package search found zero archives; all 14 NuGet.org flat-container requests
  returned HTTP 404. No replacement packages were built.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`
  passed with zero warnings and zero errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.DeployedRuntimeParityClosureTests`
  passed 26/26 tests with zero failed, skipped, or not run.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll`
  passed the complete Contracts suite: 910/910 tests with zero failed, skipped, or not run.

### Completion Notes List

- Frozen and independently hash-checked both predecessors without modifying their bytes or status.
- Created one content-addressed, schema-versioned identity crosswalk with an exact 14-package set,
  exact two-platform OCI graph, separate tag/digest response bodies, fresh Development liveness
  evidence, rejected v3.77.2 lineage, and both prohibited splice controls.
- Recorded `fail-closed`: original proof packages remain unavailable; semantic-release provenance,
  valid OCI provenance labels, Production runtime equivalence, exact image-source mapping,
  deployed authority, and all three Story 3.13 acceptances are absent.
- Froze the crosswalk, evidence-core manifest, and human proof first, then created a content-bound
  fail-closed review subject over their raw hashes. Future receipts are external to those hashes;
  Task 9 remains open because no approval was requested, provided, or inferred.
- Corrected deployed-runtime closure ownership in `docs/ci.md` without changing release mechanics.

### File List

- `_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md`
- `_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md`
- `_bmad-output/implementation-artifacts/epic-3-context.md`
- `_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/`
- `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/ci.md`
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs`

## Story Completion Status

- Status set to `review` for a reproducible `fail-closed` packet.
- AC1 and AC3 pass. The OCI graph and Development liveness portions of AC2 pass, but package bytes,
  release/source authority, valid provenance labels, and Production runtime equivalence are
  incomplete, so AC2 does not pass as a whole.
- AC4 does not pass: the packet is not a complete passing lineage and has zero of three required
  content-bound acceptances.
- Story 3.13 must remain non-`done` until every blocker is resolved and all three reviewers accept
  one unchanged replacement review subject.
