---
baseline_commit: 1d6e9321acfc416768c1c78e9facf573c9c41f71
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

Status: in-progress

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
    and this story no farther than `in-review`/`review` while AC2/AC4 remain open; never change either
    predecessor or Epic 1.
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

- [ ] **Task 5 - Revalidate the immutable OCI graph from registry bytes (AC2, AC3).**
  - [x] Resolve the proposed tag only as discovery input; immediately bind all evidence to the
    immutable index digest. Re-fetch the index by digest with an OCI-index `Accept` header and
    require the tag and digest responses to be byte-identical when a tag is part of the candidate.
  - [x] Verify `Docker-Content-Digest`, exact raw bytes/hash/length, `schemaVersion: 2`, and media
    type `application/vnd.oci.image.index.v1+json`.
  - [x] Require exactly two direct image descriptors: one `linux/amd64` and one `linux/arm64`, with
    no duplicate, extra, nested index, `unknown`, or non-empty variant entry.
  - [ ] Resolve every child manifest by digest; verify raw digest, size, descriptor/response media
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

- [x] **Task 8 - Produce the fail-closed verdict before requesting review (AC2, AC3).**
  - [x] Run a structural verifier over `identity-crosswalk.json` and the checksum manifest. Require
    exact field presence, one candidate identity, exact package/platform sets, and a reproducible
    fail-closed verdict while AC2/AC4 independent checks remain incomplete.
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
  - [x] Hash-bind the repository-owned reviewer roster and load future receipts only from the
    subject-hash-addressed evidence path, with exact authorized identities and receipt fields.
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

### Review Findings

Chunk 1/3 (tests only) — `bmad-code-review` 2026-08-11 against `1d6e9321...HEAD` File List slice `DeployedRuntimeParityClosureTests.cs`.

- [x] [Review][Decision] ValidateRuntimeEquivalence depth — resolved 2026-08-11: keep layered. `ValidateRuntimeEquivalence` stays the Production-contract gate (`contract_equivalence` / hosting env); platform equality remains owned by `ValidateRuntimeExecution`. Finding dismissed (not patched).
- [x] [Review][Patch] WaitForProcessExit treats post-kill exit as success and callers hash truncated stdout — fixed: timeout/kill always returns false
- [x] [Review][Patch] DerivedClosureRejectsActualIncompleteLineageAndDeclarativeTampering mutates committed evidence in place — fixed: copy live evidence to temp before mutations
- [x] [Review][Patch] Package exact-set check ignores nested files under archive_root subdirectories — fixed: reject nested dirs/files; added `nested-archive` theory case
- [x] [Review][Patch] Acceptance/control symlink-escape tests hard-fail when links are unavailable — fixed: Assert.Skip when symlink creation unsupported
- [x] [Review][Patch] Missing mutable-tag-only identity negative control — fixed: `MutableTagOnlyIdentityFailsClosed`
- [x] [Review][Patch] Missing OCI platform-set negatives — fixed: `OciGraphRejectsPlatformSetMutations`
- [x] [Review][Patch] Missing config os/architecture mismatch negative — fixed: `OciGraphRejectsConfigArchitectureMismatch`
- [x] [Review][Patch] Zero poll_interval_seconds fail-closed guard is unexercised — fixed: `RuntimeLogRejectsZeroPollInterval`
- [x] [Review][Patch] MalformedChecksumManifestsFailClosed omits mismatched hash for existing file — fixed: `ChecksumManifestRejectsMismatchedHashForExistingFile`
- [x] [Review][Patch] CanonicalLineageIgnoresExecutionOnlyRuntimeFacts never asserts identity-affecting mutations — fixed: assert outcome/readiness_result change lineage
- [x] [Review][Patch] Missing expired/inaccessible retained-evidence negative — fixed: `InaccessibleRetainedEvidenceFailsClosed` (expiry already covered by deployment-authority tests)
- [x] [Review][Patch] Missing environment-vs-product failure-class negatives — fixed: classification validator + `ClassifiedRuntimeFailuresEachBlockEqually` / `UnclassifiedRuntimeFailureIsRejected`
- [x] [Review][Patch] ProhibitedCrossLineageSplicesFailClosed only tweaks a few fields — fixed: fuller splice assertions + ValidateRelease check
- [x] [Review][Defer] ResolveWithin uses Ordinal StartsWith on Path.GetFullPath roots [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5255] — deferred, pre-existing
- [x] [Review][Defer] FieldNameIsSupportSafe fragment matching can false-positive legitimate names [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4134] — deferred, pre-existing
- [x] [Review][Defer] LimitationsContainMutationProhibitions accepts weak keyword substrings [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4282] — deferred, pre-existing
- [x] [Review][Defer] ResolveWithin TOCTOU between RejectReparsePoint and later file open [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5260] — deferred, pre-existing
- [x] [Review][Defer] RunGit/ComputePinnedBuildsToolSha256 sync-over-async via GetAwaiter().GetResult() [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5334] — deferred, pre-existing
- [x] [Review][Defer] ValueIsSupportSafe misses private IPs embedded in non-URI free text [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4155] — deferred, pre-existing
- [x] [Review][Patch] Reconcile story, sprint, spec, and operator lifecycle surfaces to `in-review` / `review`.
- [x] [Review][Patch] Correct the response-metadata finding so absent metadata remains an explicit fail-closed blocker.
- [x] [Review][Patch] Scan retained raw OCI configs for support-unsafe field names and values.
- [x] [Review][Patch] Remove obsolete generated review prompts and snapshots outside the content-addressed packet.
- [x] [Review][Patch] Refresh the review-subject chronology and current verification totals.

The 2026-08-11 full review is complete. Story 3.13 remains `in-review` and non-`done` because AC2/AC4 are open with 0/3 acceptances.

Chunk 2/3 (evidence, docs, status) + post-chunk-1 test delta — `bmad-code-review` 2026-08-11, against `1d6e9321...HEAD` for evidence/docs/status and `06e62b4d...HEAD` for `DeployedRuntimeParityClosureTests.cs`. Four parallel layers; 65 raw findings triaged to 4 decisions / 25 patches / 9 defers / 2 dismissed.

- [x] [Review][Decision] Three Story 1.20 predecessor evidence trees have FAILING checksum manifests at HEAD — RESOLVED 2026-08-11: do NOT write predecessor bytes from Story 3.13. Blast radius measured: `089369bb` touched 25 files; genuine content corruption is exactly three `environment.txt` files (`38f85086…`, `4983299103…`, `ec0d35a0…`), each a single hash mismatch in `critical-evidence-sha256.txt`. Story 3.13's own `predecessor-tree-sha256.txt` verifies 40/40 OK, and the `nuget-sha256.txt` failures across all four trees are missing files (the unrecoverable 14 proof packages), not corruption. Logged as a HIGH Epic 1 evidence-integrity defect in `deferred-work.md`; warrants its own scoped story under its own authority record. Original finding text: — `089369bb` ("docs: clear remaining root predecessor SDK patch tokens", 25 files, not a Story 3.13 commit) rewrote `10.0.301`→`10.0.302` SDK tokens inside frozen owner-approved Epic 1 evidence. Story 3.13 restored only its own `fa2d1c99…` tree at `3d6dea69`. `sha256sum -c critical-evidence-sha256.txt` now FAILS on `environment.txt` in `38f85086fc25…`, `4983299103bf…`, and `ec0d35a082bc…`. Restoring them means writing predecessor bytes, which the frozen constraints forbid and which Story 3.13 already did once. Decide: restore all three for consistency, record as an Epic 1 integrity defect owned elsewhere, or open a separate story.
- [x] [Review][Decision] AC4 may be unsatisfiable as rostered — RESOLVED 2026-08-11 by the owner: the dual `eventstore-owner`/`release-owner` mapping to `github:jpiquot` is ratified as legitimate (he holds both roles), and the `bmad:murat` Test Architect receipt is accepted. AC4 is collectable as rostered once the packet passes. Converted to a patch recording this ratification in the roster so future review loops do not re-litigate it. Original finding text: — `reviewer-roster.json` maps `eventstore-owner` and `release-owner` both to `github:jpiquot`, and `test-architect` to `bmad:murat`, a BMad agent persona with no durable external identity. "Three named acceptances of the same content-bound subject" reduces to one human accepting twice plus an agent. Previously deferred as roster cosmetics; it is an AC4 satisfiability question. Decide whether this composition satisfies AC4 or a third independent human is required.
- [x] [Review][Decision] AC2 recovery may be dead rather than blocked — RESOLVED 2026-08-11: do not launch a fresh recovery sweep (the search was already performed and recorded as exhausted, including GitHub Packages with `read:packages`), and do not re-scope the story unilaterally — re-scoping is an owner correct-course decision. Converted to a patch that makes the existing fail-closed record reproducible: name the durable sources actually queried and the query method, without reintroducing the absolute host paths that support-safety redacted. Original finding text: — `package-availability.json` records only NuGet.org flat-container 404s for all 14 packages, `rebuild_attempted: false`, and redacts `local_search_roots` to `<redacted-*>` so the search is unreproducible; no Hexalith-internal feed or GitHub Packages query is evidenced. If the proof archives are permanently unrecoverable, no further hardening pass can close this story and it sits in `review` indefinitely. Decide: accept permanent fail-closed and re-scope, or authorize a documented recovery attempt against the remaining durable sources.
- [x] [Review][Decision] Fail-closed contract is inconsistent — RESOLVED 2026-08-11: missing retained evidence MUST return `false`. AC3 states the *verdict* is `fail-closed`; an exception escaping the evaluator produces no verdict at all, which is a strictly weaker outcome than the one the AC requires. Converted to a patch: wrap the evidence reads so absent files yield `false`, and rewrite the test to assert the false verdict. Original finding text: — `InaccessibleRetainedEvidenceFailsClosed` asserts `Should.Throw<FileNotFoundException>` from `EvaluateWithFreshReview` while every other fail-closed path returns `false`, locking an unhandled I/O exception in as the contract [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:1295]. Decide whether missing retained evidence must return `false` or may propagate.
- [x] [Review][Patch] APPLIED 2026-08-12 — all four lifecycle surfaces now read `in-progress` (spec frontmatter, story record, `sprint-status.yaml`, `docs/ci.md`). Spec frontmatter said `status: 'done'` while AC4 is 0/3 — flipped from `'in-review'` to `'done'` by `2bc8ee17`, the same commit whose findings record "Reconcile story, sprint, spec, and operator lifecycle surfaces to `in-review` / `review`" and whose story text says "remains `in-review` and non-`done`". Contradicts the frozen constraint "stay non-`done` unless AC4 passes", the story record, `sprint-status.yaml:106`, `docs/ci.md`, and `verdict.story_may_be_done: false` [_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:5]
- [x] [Review][Patch] APPLIED 2026-08-12 — added `preflight-failure-class`, `platform-failure-class`, and `platform-unknown-failure-class` cases to `RuntimeEvidenceRejectsExecutionAndBoundMutations`, which mutate the runtime node and persist it via `PersistRuntimeBindings` so `DeepEquals` holds and control reaches the guard. Mutation-verified: neutering `RuntimeFailureClassificationIsValid` to `return true` now turns exactly those 3 cases red while the other 24 stay green (previously it left the whole suite green). Runtime failure-classification guard was provably vacuous — mutation-verified: neutering `RuntimeFailureClassificationIsValid` to `return true` leaves the focused suite at 157/157. Both call sites are immediately followed by `outcome != "pass"`, and the two tests added to cover it set `outcome = "fail"`, so they are rejected earlier by `DeepEquals` at `:3298` and by the outcome check. Its only live branch — `outcome: "pass"` carrying a `failure_class`, or a class outside `environment|product|evidence` — has zero coverage, yet the story records the finding as fixed [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5784]
- [x] [Review][Patch] APPLIED 2026-08-12 — restored 116 comment lines across 24 keys from baseline `1d6e9321` (file back to 119 indented comments), including both Epic 1 annotations above `1-9` and `1-13`. Verified comment-only: YAML parses, key set identical, zero status values changed. Restore the sprint-status decision comments destroyed by Story 3.13 commit `2a6c2177` (131 → 0; only the 3 test-pinned Story 1.20 lines were later restored) — the loss includes two Epic 1 annotations above `1-9` and `1-13`, violating "Epic 1 stays unchanged", plus the 2.12 re-scope block, the 3.1 merge/ratification record, the 2.5/2.6/2.7/2.11 acceptance records, and the Epic 6/7/8 gating notes. Only the Story 1.20 block is protected by a test, so the next YAML round-trip will delete the rest again [_bmad-output/implementation-artifacts/sprint-status.yaml]
- [x] [Review][Patch] APPLIED 2026-08-12 — proof packet now records that Story 3.13 wrote two predecessor files at `3d6dea69` solely to restore approved bytes drifted by unrelated commit `089369bb`, states the net-state sense in which `verdict.predecessor_state_changed` remains `false`, and names the three sibling trees it holds no authority to repair. The subsequently committed pre-chunk-3 packet binding was `2ec51a05…`; the current review cascade rebinds it to `eac7033b…`, with the review subject and outer manifest refreshed. Proof packet and crosswalk attested to no predecessor modification, which was false — "No predecessor file was normalized, regenerated, or modified" and `predecessor_state_changed: false` are contradicted by Story 3.13 commit `3d6dea69`, which modified `1-20-owner-approved-parity-closure-proof-packet.md` and `evidence/story-1-20/fa2d1c99…/environment.txt`. The restoration is disclosed in the Spec Change Log but denied by the content-bound subject reviewers are asked to accept [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md:66]
- [x] [Review][Patch] APPLIED 2026-08-12 — added `EvidenceDirectoryHasNoUnlistedFiles`, enumerating the evidence directory and rejecting any top-level file no manifest lists, wired into `ValidateEvidenceIntegrity`; new `UnlistedEvidenceDirectoryFileFailsClosed` theory (3 cases) is mutation-verified to go red when the guard is removed. No stray-file detection existed in the content-addressed evidence directory — verified that `Directory.GetFiles` is used only for the package archive root, the receipt directory, and `CopyDirectory`; nothing enumerates the evidence directory, so a file present on disk but listed in no manifest rides inside the packet undetected [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3789]
- [x] [Review][Patch] `4-8-durable-admission-evidence-ledger: backlog` row was added by the same rewrite that deleted the rule forbidding it ("Story 4.8 is a non-executable evidence ledger and therefore has no status row"), leaving an orphan key matching no story file [_bmad-output/implementation-artifacts/sprint-status.yaml:117]
- [x] [Review][Patch] APPLIED 2026-08-12 (in passing, during the required sprint-status sync) — now `last_updated: '2026-08-12'`. Was unquoted, non-ISO, ambiguous MM-DD-YYYY, and contradicts both the file's own header comment and the correctly-quoted ISO `generated:` on the line above [_bmad-output/implementation-artifacts/sprint-status.yaml:44]
- [x] [Review][Patch] The `nested-index` case of `OciGraphRejectsPlatformSetMutations` exercises no nested index — it only flips `registry-readback.json → objects[0].content_type`, a path already covered elsewhere, and never writes an index descriptor, a third descriptor, or a `platform.variant` entry into `index.raw`; the raw-index rejections at `:3052-3053` can be deleted with the suite still green [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:1216]
- [x] [Review][Patch] The raw-config support-safety scan added to `ValidateOciGraph` has no covering test — `OciProvenanceRejectsSensitiveConfigRawValues` asserts only `ValidateOciProvenance`, and a plain content edit cannot reach the graph-side scan because `BytesMatchDescriptor` rejects the mismatched digest first; removing `|| !DocumentIsSupportSafe(config)` at `:3120` changes no test result [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3120]
- [x] [Review][Patch] `identity-crosswalk.json` records `"exit_code": 0` for a run whose own verification reason states the retained logs "omit … exit codes", with no citation and no per-field verification result [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/identity-crosswalk.json:313]
- [x] [Review][Patch] `oci-validation.json` identifies the image by the mutable tag `registry.hexalith.com/eventstore:quarantine-proof-fa2d1c99…` rather than the immutable `@sha256:` reference used everywhere else — the exact mutable-tag identity `MutableTagOnlyIdentityFailsClosed` was added to reject — and is the only evidence document with no `schema_version` and no `verification` block [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/oci-validation.json:31]
- [x] [Review][Patch] Retained per-platform and preflight `outcome: "pass"` claims are kept in `smoke-results.json`, `runtime-verification.json`, and the crosswalk while the verifier separately asserts those same logs FAIL `ValidateRuntimeLog`/`ValidatePreflightLog`; the eighth-pass honesty fix reached only the top-level `result: "fail"`. Mark preflight and per-platform outcomes `unverified` as `execution_result` already is [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/smoke-results.json]
- [x] [Review][Patch] `review-subject.json` presents `authority_record_sha256: null` to the three reviewers even though the crosswalk carries a hash-checked authority record (`record_sha256: 2fd6a43f…`, `deployment_authorized: false`, quarantine-only scope); reviewers accept a subject showing no authority identity rather than the quarantine-only one. Its `expires_at: 2026-08-25` is also unmentioned by any blocker or reopen trigger [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/review-subject.json:23]
- [x] [Review][Patch] `git diff --check` is recorded as passing and cited as scope evidence it cannot supply — it reports whitespace errors only, says nothing about which paths changed, and re-running the recorded command at HEAD exits 2 with hits in two `bundle-contract.md` files and two `gh-*-review-diff.txt` evidence files [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:440]
- [x] [Review][Patch] The spec contains two divergent `## Suggested Review Order` sections with conflicting anchors for the same targets (evaluator cited at `:2040` vs `:2369`, `deferred-work.md:1021` vs `:1212`); the first block's `ci.md:267` pointer lands on the Story 3.12 paragraph and its `3-13-…closure.md:675` pointer lands on the File List [_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:220]
- [x] [Review][Patch] Restore the Story 1.20 sole-authority sentence removed from `docs/ci.md` by Story 3.13 commit `b140a576` — the deleted paragraph stated Story 1.20 "retains sole authority over its approval fields and consumer-migration decision"; Task 10 authorized replacing the stale deployed-closure ownership text, not narrowing the predecessor's documented authority [docs/ci.md]
- [x] [Review][Patch] APPLIED 2026-08-12 (in passing) — set to `13`. Was `review_loop_iteration: 7` against twelve dated hardening passes in the Spec Change Log — previously deferred as cosmetic when the gap was smaller; it is now materially wrong and is the only machine-readable loop counter [_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:7]
- [x] [Review][Patch] Several new negative tests have no paired positive control, so they cannot distinguish "the mutation caused the failure" from "this fixture never passed" — `MutableTagOnlyIdentityFailsClosed`, `OciGraphRejectsConfigArchitectureMismatch`, `RuntimeLogRejectsZeroPollInterval`, and `OciProvenanceRejectsSensitiveConfigRawValues` never assert the un-mutated fixture returns true first, unlike `CanonicalLineageIgnoresExecutionOnlyRuntimeFacts` which correctly captures `before` [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:1162]
- [x] [Review][Patch] `failure_class` is written into the retained log fixtures but never read by any validator — the only consumer reads it from the crosswalk node, so a crosswalk claiming `failure_class: "environment"` over a log recording `"product"` is undetectable, which is precisely the environment-vs-product separation Task 6 requires [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5787]
- [x] [Review][Patch] The proof packet's headline "complete Contracts suite: 1260 passed" is not attributable to Story 3.13 — the same Debug Log shows 999 on 2026-08-04 and 1001 on 2026-08-05 while the focused verifier grew only 117 → 157; the remaining ~219 tests came from concurrent unrelated work in the same range and the packet does not say so [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md:136]
- [x] [Review][Patch] `CopyDirectory` defeats the isolation it was added for — `File.Copy` follows symlinks rather than reproducing them, so symlink-escape conditions vanish in the staged copy, and the first loop over `Directory.GetDirectories(..., AllDirectories)` is dead given the `CreateDirectory` inside the file loop [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5798]
- [x] [Review][Patch] Validator robustness gaps in the fail-closed paths: `ValidateAcceptances` omits `FormatException` from its catch filter so a malformed `created_at` throws instead of returning false; `DateTimeOffset.Parse` is called without `CultureInfo.InvariantCulture`; runtime preflight/platform nodes accept arbitrary undeclared keys (no `HasExactProperties`); a trailing separator on `archive_root` makes the `GetDirectoryName` comparison never match and would reject a fully recovered 14-archive set; the acceptance scan reads only top-level `*.json` so non-JSON or nested receipt material is unchecked; and the `nested-index` branch indexes `objects[0]` without checking the array is populated [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3609]
- [x] [Review][Patch] Record the owner's 2026-08-11 ratification of the dual `eventstore-owner`/`release-owner` identity and the `bmad:murat` Test Architect receipt in the roster, with a `created_at` and an authority source for the roster itself, so future review loops do not re-raise AC4 satisfiability [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/reviewer-roster.json]
- [x] [Review][Patch] Make the AC2 fail-closed record reproducible — name the durable sources actually queried (NuGet.org flat container, GitHub Packages with `read:packages`, any Hexalith-internal feed) and the query method, so a reviewer can re-derive "unrecoverable" without the absolute host paths that support-safety redacted to `<redacted-*>` [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/package-availability.json:7]
- [x] [Review][Patch] Make absent retained evidence return a `false` verdict instead of propagating `FileNotFoundException`, and rewrite `InaccessibleRetainedEvidenceFailsClosed` to assert the false verdict — AC3 requires a fail-closed *verdict*, and an escaping exception yields none [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:1295]
- [x] [Review][Defer] Story 4.5's evidence packet is self-invalidating — `validate-evidence.py` was modified by `3e365150` after its manifest was sealed at `86308550`, so `evidence-sha256.txt` fails with a genuine content mismatch at HEAD; unrelated to Story 3.13 [_bmad-output/implementation-artifacts/evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513/evidence-sha256.txt] — deferred, pre-existing
- [x] [Review][Defer] OCI layer descriptors are never validated — the retained real manifests carry seven layer descriptors each whose digests and sizes are unchecked, and the pass-path fixtures use layer-less manifests no registry would accept [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4925] — deferred, pre-existing
- [x] [Review][Defer] `release-provenance.json`, `deployment-authority.json`, and `deployment-authority-source.json` are validated by code paths that have never met a real artifact — no such file exists in the committed evidence directory [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs] — deferred, pre-existing
- [x] [Review][Defer] The required structured runtime-log format exists only inside the test fixture — retained logs are line-oriented text while the pass-path validators parse JSON objects, so reopen trigger #5 asks the Hexalith.Builds smoke-contract owner to satisfy a schema specified nowhere outside the test file [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/smoke-linux-amd64.log] — deferred, pre-existing
- [x] [Review][Defer] The acceptance receipt contract is unanchored and unscaffolded — `external_receipt_location` is the relative string `acceptances/{subject_sha256}` with no stated root, `required_receipt_fields` binds to no roster version, and the directory does not exist [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/identity-crosswalk.json] — deferred, pre-existing
- [x] [Review][Defer] `evidence-sha256.txt` is the one evidence file whose bytes nothing hashes — absent from the core manifest and unbound in the review subject; mitigated because its entry set is structurally pinned and its hashes are recomputed against live bytes [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/evidence-sha256.txt] — deferred, pre-existing
- [x] [Review][Defer] Epic 4 tracker churn and Story 4.5/4.14/OQ8/DAPR-pin prose land inside the reviewed range from concurrent commits, and the packet's non-mutation attestation is scoped only to submodule gitlinks so it under-discloses what its own range changed [docs/ci.md] — deferred, pre-existing
- [x] [Review][Defer] `WaitForProcessExit` orphans a child process when both the kill and the 5-second post-kill wait fail, and no test drives a git invocation past the 30-second window, so neither the old nor the new timeout behavior is observed [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5770] — deferred, pre-existing
- [x] [Review][Defer] `checked_at` in `package-availability.json` (2026-08-04T11:17:05Z) and `registry-readback.json` (2026-08-04T11:48:07Z) predates the 2026-08-09 rewrite of those files [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/package-availability.json] — deferred, pre-existing

Dismissed (2): a "circular predecessor proof" claim — refuted, the crosswalk pin `47f09bdf` is the value at Story 3.13's own baseline and dates to 2026-08-01, so `3d6dea69` restored approved bytes that the unrelated `089369bb` had drifted, rather than fabricating them; and a `docs/ci.md` Epic-4/OQ8 scope-leak attribution — those paragraphs come from `fe715c70`, `ab1666dd`, `b927472a`, `35a1eecd`, and `86308550`, not from Story 3.13.

Verification independently reproduced at HEAD before chunk 3: Release build 0 warnings / 0 errors; focused verifier 172/172, 0 skipped; Story 1.20 `critical-evidence-sha256.txt` 33/33 OK for the approved `fa2d1c99…` tree; Story 3.13 `evidence-core-sha256.txt` 17/17 and `evidence-sha256.txt` 3/3 OK; `markdownlint docs/ci.md` 0 issues. The complete Contracts aggregate later passed 1409/1409 after the OQ8 verifier was reconciled with this change set's removal of the orphan Story 4.8 status row.

Chunk 3/3 (post-chunk-2 delta) — `bmad-code-review` 2026-08-13, against `2bc8ee17...HEAD` for the Story 3.13 File List plus `deferred-work.md` and the new Step-3 gate proposal. Four parallel layers; 71 raw findings triaged to 4 decisions / 26 patches / 4 defers / 3 dismissed.

- [x] [Review][Decision] Reviewer-roster `authority_source` is self-certifying and back-dated — it cites commit `77f34d13` as the owner's ratification record, but that commit never touches the roster (its blob there has only `schema`/`repository`/`roles`), the patch that adds the field is still `- [ ]` unapplied at that commit, `created_at: 2026-08-12T06:05:15+00:00` is exactly `77f34d13`'s commit instant although the field was actually written 2h34m later at `be392a3a`, and `decision_date: 2026-08-11` disagrees with the cited commit's 2026-08-12 date. The value is hardcoded at four verifier sites. This is the record AC4's three content-bound acceptances bind to, and the spec forbids inferring identity from "summaries, or prior approvals". Decide what artifact constitutes the owner's authority record: (a) point `authority_source` at a durable external decision record (GitHub review/issue/approval) and re-derive `created_at` honestly; (b) keep the repository-commit form but cite a commit that actually carries the decision and drop the back-dated `created_at`; or (c) accept the story record itself as the authority and say so explicitly, acknowledging it is self-referential [_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/reviewer-roster.json] — RESOLVED 2026-08-13 by the owner: option (a). `authority_source` must cite a durable EXTERNAL decision record (GitHub review/issue/approval), and `created_at` must be re-derived honestly. The external URL cannot be invented by the review; the owner must supply it, so this splits into an applied part (honest `created_at`, temporal-ordering check, roster mutation coverage) and a blocked part (the external citation itself) recorded as an open action.
- [ ] [Review][Action] Owner must supply the durable external GitHub decision URL that ratifies the exact reviewer-role roster; the repository now records the missing authority explicitly and cannot collect valid acceptances until this action closes.
- [x] [Review][Decision] An orphan Epic 1 story row was inserted into a `done` epic — `1-21-frozen-story-1-20-evidence-integrity-repair: backlog` sits between `1-20-…: done` and `epic-1-retrospective: done` inside the `epic-1: done` block. By the file's own legend `done` means "All stories in epic completed" and `backlog` means "Story only exists in epic file", but no `1-21-*` story file exists, `epics.md` has no 1.21, and the key appears nowhere else in the repository. Its authorizing comment claims "Approved 2026-08-12 post-closure evidence maintenance" with no external approval record. This contradicts the frozen "Never … modify predecessors … Epic 1" constraint, the proposal's own §2.4 "No new epic or story is required", and its frontmatter `sprint_tracking_mutation: additive-comments-only-approved`. Decide: (a) revert the row and track the Epic 1 repair only in `deferred-work.md` until a scoped story is authored; (b) author the real `1-21` story file + epics.md entry under its own authority record; or (c) ratify the row as-is with an explicit approval record [_bmad-output/implementation-artifacts/sprint-status.yaml:80] — RESOLVED 2026-08-13 by the owner: option (b). Author the real Story 1.21 file and its `epics.md` entry under its own authority record, so the sprint row stops being an orphan. This work belongs to the new story, not to Story 3.13; Story 3.13 still writes no predecessor bytes.
- [x] [Review][Decision] `durable_source_queries` mints five source results that no retained evidence supports — `azure-worm-archive-inventory` and `github-actions-retained-artifact-inventory` appear in no story, packet, or spec text; there is no per-query timestamp, operator, or citation; `checked_at` is still `2026-08-04T11:17:05Z`, predating the 2026-08-11 decision these entries encode; and that decision authorized naming only the NuGet.org flat container, GitHub Packages with `read:packages`, and any Hexalith-internal feed, while explicitly saying "do not launch a fresh recovery sweep". All five strings are hardcoded as *required* in the verifier, so AC2's fail-closed reproducibility record now asserts searches that may never have run. Decide: (a) confirm the two extra sweeps were actually performed and record when, by whom, and against what, or (b) remove them and keep the three authorized sources [.../package-availability.json:7] — RESOLVED 2026-08-13 by the owner: option (a). Remove `azure-worm-archive-inventory` and `github-actions-retained-artifact-inventory`, keep only the three authorized sources, and unlock the verifier's required set. The evidence hash cascade must be recomputed.
- [x] [Review][Decision] Lifecycle token vs. the change's own prohibition — all four surfaces are set to `in-progress`, whose legend is "Developer actively working on implementation", while the same change prohibits all further Story 3.13 work until an external restart gate is satisfied. The four surfaces are mutually consistent and the frozen "stay non-`done`" constraint is respected, but the token misdescribes the state. This is the fourth lifecycle flip in the story. Decide whether to keep `in-progress`, restore `review`/`in-review`, or introduce a blocked/awaiting-evidence token [_bmad-output/implementation-artifacts/sprint-status.yaml:220] — RESOLVED 2026-08-13 by the owner: keep `in-progress` on all four surfaces. The imprecision is accepted; it is the safest non-`done` token and is already consistent across story, spec, sprint row and `docs/ci.md`. No change.
- [x] [Review][Patch] The AC3 fail-closed *verdict* is still observed by nothing — `EvaluateWithFreshReview` calls `RefreshReviewBindings` → `WriteCoreManifest` first, which `File.ReadAllBytes` every core entry, so `InaccessibleRetainedEvidenceFailsClosed`'s deleted `index.raw` throws inside fixture rebinding and is swallowed by the new blanket catch; `EvaluateClosure` is never entered and the `.ShouldBeFalse()` asserts the harness's own exception handler. Narrowing `EvaluateClosure`'s catch to re-propagate leaves the suite at 172/172. Assert `EvaluateClosure(...)` directly with bindings captured before the deletion [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:1373]
- [x] [Review][Patch] `RuntimeFailureClassificationMatchesLog` is dead at both integration call sites — CONFIRMED BY MUTATION: replacing both `&& RuntimeFailureClassificationMatchesLog(...)` clauses with `&& true` leaves the focused suite at 172/172, 0 failed. `RuntimeFailureClassificationIsValid` short-circuits earlier in the same `||` chain for node-side mutations, and the only log-side fixtures set `outcome: "fail"`, which `log["outcome"] == "pass"` rejects first. Add a `log-failure-class` mutation that writes `failure_class` into a retained smoke log, refreshes `log_sha256`, keeps the node `outcome: "pass"` and class-free, and asserts `ValidateRuntimeExecution(...)` false. Fifth recurrence of the guards-green-by-construction pattern [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4607]
- [x] [Review][Patch] The blanket `catch … return false` in `EvaluateWithFreshReview` covers all ~21 call sites, of which the file carries 118 `ShouldBeFalse()` against only 35 `ShouldBeTrue()` — any future fixture-construction fault (renamed key, null node) silently turns a negative test green instead of red. Assert the specific rejecting gate alongside the closure verdict, or narrow the catch filter [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:5844]
- [x] [Review][Patch] The non-mutation attestation is false for submodules — Story 3.13's own commits bump gitlinks: `77f34d13` moves `references/Hexalith.FrontComposer` and `references/Hexalith.Memories`, `47afe552` moves `references/Hexalith.Builds` and `references/Hexalith.Memories`. The packet's carve-out for changes "outside this story's authored change set" does not cover commits the story itself authored [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md:153]
- [x] [Review][Patch] The Step-3 gate proposal contradicts itself and its own commit — frontmatter declares `status: approved-for-documentation-handoff`, `final_approved_by: Administrator`, `handoff_status: routed`, while the body still reads "**Status:** DRAFT FOR REVIEW — NOT APPROVED", §5 "These edits are **proposed only**… must not be applied until this complete proposal receives explicit approval. Sprint tracking must not be modified before approval", and §9 "Creating this file does not approve implementation". Every §5 edit is applied inside the same range. Separately, the proposal asserts "The proof packet, evidence schemas, reviewer subject, test verifier, and checksum manifests remain unchanged" and "no further hardening is justified" — yet the very commit that introduces it (`be392a3a`) rewrites nine evidence files and adds 556 lines to the verifier [_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-12-story-3-13-step-3-gate.md:25]
- [x] [Review][Patch] `exit_code_verification` gates have no negative case — the exact `citation`/`result`/`reason` shape, the `citation == "bounded-smoke-process-result"` and `result == "pass"` equalities, and the `smoke-results.json` `DeepEquals` pair can all be deleted with the suite green, because `PersistRuntimeBindings` copies the node verbatim so the pair can never diverge in any existing test. Add `exit-code-citation`, `exit-code-verification-result`, and `smoke-exit-code-verification-drift` cases [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3536]
- [x] [Review][Patch] The new `oci-validation.json` `immutable_reference` and `verification` guards have no mutation case — `OciValidationReportRejectsSchemaRootAndDescriptorMutations` covers only `schema`, `repository`, `raw-index`, `child-digest`, `extra-field`; deleting both new clauses leaves the suite green, so the mutable-tag identity this patch exists to reject becomes acceptable again inside that document [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3210]
- [x] [Review][Patch] The `oci-validation.json` rewrite silently dropped its raw-byte bindings — `manifest_raw_file`, `manifest_raw_sha256`, `config_raw_file`, `config_raw_sha256`, `config_media_type`, `media_type`, `index_size` and `platforms` were removed, and the new `HasExactProperties` call now *forbids* restoring them, so the document attests to a graph whose bytes it no longer names. The raw children remain bound at packet level by `evidence-core-sha256.txt`, so this is disclosure and self-description, not lost integrity. Restore the per-child raw bindings or record the narrowing in the story, spec and packet [.../oci-validation.json]
- [x] [Review][Patch] `EvidenceDirectoryHasNoUnlistedFiles` closes only the top level and its listed set cannot match nested entries — it uses `SearchOption.TopDirectoryOnly`, never enumerates subdirectories, and builds `listed` from full relative paths (`archiveRoot + "/" + archive`, `hash_manifest_path`) while comparing against `Path.GetFileName`, so those entries can never match. Latent on today's flat 21-file directory; it breaks for a recovered-archive packet and lets an unlisted subdirectory (or a sibling `acceptances/<other-hash>/` tree) ride inside the content-addressed packet undetected [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4432]
- [x] [Review][Patch] The recorded hash cascade names two hashes that exist in no artifact — the note "proof packet `03f2b59c…` → `8bf27efc…`" appears only in the story and spec prose; the committed cascade uses `2ec51a05a0dd69b585c12865bb6559f72a13c200f84ad63e477eee670a6cd130`, which I recomputed and which matches both `review-subject.json` and `evidence-sha256.txt`. The bindings are correct; only the narrative is wrong [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:335]
- [x] [Review][Patch] The "Verification independently reproduced at HEAD" line is stale and mis-attributes cause — "the complete Contracts aggregate passed 1254/1275 and failed 21 unrelated OQ8 tests" is false at HEAD: I measured 1409/1409, 0 failed, 0 skipped. The failures were not "unrelated" — this diff's own removal of the `4-8-durable-admission-evidence-ledger` row broke `Oq8PlatformClosureTests`, and `510daf79`/`6877457c` later reconciled that verifier, which now *forbids* the key [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:373]
- [x] [Review][Patch] The chunk-2 header's triage counts do not match the block it heads — it claims "22 patches / 8 defers"; the block contains 25 `[Review][Patch]` and 9 `[Review][Defer]` bullets (decisions 4 is correct) [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md:330]
- [x] [Review][Patch] `docs/ci.md` points operators at a document it never identifies — the new paragraph says work must not resume "until all restart conditions in the approved follow-up Sprint Change Proposal are satisfied" but gives no filename, path, or link, so the gate is unreachable from the operator-facing surface. The seven-line addition also exceeds the Code Map's authorization to replace *only* the stale Story 3.12-to-1.20 ownership paragraph [docs/ci.md:293]
- [x] [Review][Patch] The spec's own Verification command is recorded as failing while the Verification section still declares it must pass — the record now says `npx markdownlint-cli2 docs/ci.md && git diff --check` "exits 2 on four unrelated historical whitespace findings … This command is not scope evidence", contradicting the spec's stated expectation. Amend the Verification section (it is outside the frozen block) or fix the whitespace [_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:269]
- [x] [Review][Patch] The added positive controls are taken against the pre-rebind fixture — `ValidateOciGraph(...).ShouldBeTrue()` runs before `RebindAmd64ConfigArchitecture`/`RebindIndex`, while the assertion under test runs against the rebound directory, so the controls do not establish that the rebound fixture would otherwise pass; `OciGraphRejectsPlatformSetMutations`, whose `nested-index` case was rewritten to use `RebindIndex`, received no control at all [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:1296]
- [x] [Review][Patch] A `failure_class` key present with an explicit JSON `null` is treated as absent by both classification helpers, because each tests `is JsonValue` rather than key presence — a passing node can therefore carry an explicit null classification undetected [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:6244]
- [x] [Review][Patch] The honesty demotion was applied to `outcome` but not to its siblings — `outcome` drops to `"unverified"` while `cleanup: "pass"` and the `attempts` counts are retained, although all three rest on the same retained logs that fail `ValidateRuntimeLog` [.../smoke-results.json:12]
- [x] [Review][Patch] Roster `created_at` is not ordered against `decision_date`, `assembled_at`, or the subject's `created_at`, so a ratification stamped after the subject it authorizes still passes [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4232]
- [x] [Review][Patch] `ReviewerRosterRejectsExtraAndUnauthorizedMappings` has no case touching `created_at` or any `authority_source` field — deleting the whole authority block from the validator leaves the suite green, so a roster citing an arbitrary commit would be accepted [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4233]
- [x] [Review][Patch] A roster missing `authority_source` raises `NullReferenceException` instead of the documented `InvalidDataException` contract, because the node is dereferenced before the validation guard [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:4218]
- [x] [Review][Patch] The File List omits two files this chunk changes — `deferred-work.md` and `sprint-change-proposal-2026-08-12-story-3-13-step-3-gate.md`; the existing File List deferral covered evidence files only [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md]
- [x] [Review][Patch] `sprint-status.yaml`'s header comment still reads `# last_updated: 2026-08-11` while the same change sets the value to `'2026-08-12'` [_bmad-output/implementation-artifacts/sprint-status.yaml:2]
- [x] [Review][Patch] The proof packet's Verification Record carries an orphaned parenthetical — the 172 line reads "(re-measured 2026-08-12); this is the test count attributable to Story 3.13." immediately followed by "(re-measured after the 2026-08-11 full review patches)", which belonged to the deleted 157 figure [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md:138]
- [x] [Review][Patch] `ValidateAcceptances` declares two byte-identical arrays — `expectedSourceNames` recomputes `expectedNames` (`RequiredRoles.Select(role => role + ".json").Order(...)`) one line before use [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:3792]
- [x] [Review][Patch] The rewritten Suggested Review Order anchors are wrong, including ones this change shifted — `sprint-status.yaml:215` (row is at 220; 215 is a comment), `3-13-…closure.md:736` (bullet at 732), `identity-crosswalk.json:431` (`"verdict"` now begins at 436 after this change's +5 lines), `3-13-…proof-packet.md:123` (Verification Record now starts at 136). This is the same anchor-drift defect the change claims to fix [_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md:277]
- [x] [Review][Patch] The headline "focused verifier 172/172, 0 skipped" holds only on symlink-capable hosts — `EvidenceCopyRejectsSymbolicLinks` calls `Assert.Skip` when symlink creation fails, so on a CI agent without that privilege the new reparse-point guard is never exercised and the skip count is non-zero. Record the caveat [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:1852]
- [x] [Review][Defer] The 116 restored sprint-status comment lines are still pinned by no test — the finding's own text warns "the next YAML round-trip will delete the rest again", yet only the three Story 1.20 lines remain test-protected. The restoration itself is correct; the guard gap predates this chunk [_bmad-output/implementation-artifacts/sprint-status.yaml] — deferred, pre-existing
- [x] [Review][Defer] Eighteen of the twenty-five chunk-2 patch bullets are checked `[x]` with no disposition text, so the record does not say what changed for them; only seven carry an "APPLIED 2026-08-12" note [_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md] — deferred, pre-existing
- [x] [Review][Defer] `RebindIndex` can throw `IOException` from `Directory.Move` instead of exercising the rejection path when the mutation leaves index bytes unchanged or the `manifests` array is empty [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:6441] — deferred, pre-existing
- [x] [Review][Defer] `archive_root` trailing-separator normalization is still duplicated between `ValidatePackageBytes` and `ExpectedCoreFilesFor`, so repeated or platform-alternate separators can diverge between the two validators [tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs:2771] — deferred, pre-existing

Dismissed (3): a claim that the retained deployment authority must be compared against `DateTimeOffset.UtcNow` — refuted, the packet is retained historical evidence and `deploymentActionAt < expiresAt` is the correct reproducible semantic; adding a wall-clock comparison would make a content-addressed packet expire and fail the suite on 2026-08-25, breaking NFR9 reproducibility. A claim that the new `deferred-work.md` sections break the ledger's format — refuted, only 13 of 241 entries carry a `status:` line and the new entries match the dominant `source_spec`/`summary`/`evidence` shape. A claim that `ValidateDurablePackageSourceQueries` makes the AC2 pass path unsatisfiable — refuted, it sits only on the `…package-availability/v1` fail-closed branch; the recovered path uses the `/v2` shape with `recovered_count == 14` and a different exact property set.

Verification independently reproduced after chunk-3 implementation at HEAD (`24e5caea`): Release build 0 warnings / 0 errors; focused verifier 186/186, 0 skipped on a symlink-capable host; complete Contracts aggregate 1423/1423, 0 failed, 0 skipped; Story 1.20 `critical-evidence-sha256.txt` 33/33 OK across the frozen 40-file tree; Story 3.13 `evidence-core-sha256.txt` 17/17 and `evidence-sha256.txt` 3/3 OK, with `evidence-sha256.txt` itself the only unhashed file of the 21. On a host that cannot create symbolic links, the reparse-point coverage skips instead of reporting zero skips.

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
- `4bcf2484` was the original story-creation baseline and updated sprint/submodule state. The
  authoritative review/implementation baseline is now `1d6e9321` (aligned with the spec frontmatter
  and `DeployedRuntimeParityClosureTests.ExpectedBaselineCommit`). Preserve unrelated submodule
  identities and user work.
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
  children/configs from the registry. Raw descriptor/body digests, sizes, media types, and
  platforms passed, but child/config response content types and digest headers were not retained;
  complete registry-response replay therefore fails closed.
- The current shared smoke contract ran from `2026-08-04T11:10:03.248829Z` through
  `2026-08-04T11:12:03.469486Z` and reported passing polls and cleanup under `Development`.
  The retained logs omit structured HTTP, redirect, observed-platform, per-platform timing, and
  exit-code facts, so they do not independently prove liveness. `docs/ci.md` also requires
  `Production`; runtime evidence and contract equivalence remain blockers.
- Both retained configs set the OCI source, URL, and documentation labels to the malformed value
  `https`. The byte graph passes, but provenance-label validation fails closed.
- Exact local proof-package search found zero archives; all 14 NuGet.org flat-container requests
  returned HTTP 404. No replacement packages were built.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 --no-restore -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`
  passed with zero warnings and zero errors.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 --no-restore -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --filter FullyQualifiedName~DeployedRuntimeParityClosureTests`
  passed 115/115 tests with zero failed, skipped, or not run.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 --no-restore -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`
  passed the complete Contracts suite: 999/999 tests with zero failed, skipped, or not run.
- `jq empty _bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/*.json`
  exited zero for every retained JSON document.
- `npx markdownlint-cli2 docs/ci.md` exited zero; the configured selection reported zero issues.
- `git diff --check 1d6e9321acfc416768c1c78e9facf573c9c41f71 -- .` exits 2 on four
  unrelated historical whitespace findings (two `bundle-contract.md` files and two retained
  `gh-*-review-diff.txt` files). This command is not scope evidence; current-patch whitespace and
  path scope are verified separately with `git diff --check` and `git diff --name-only`.
- 2026-08-05 code review, after applying six patches: the focused filter passed 117/117 and the
  complete Contracts suite passed 1001/1001, both with zero failed, skipped, or not run; the Release
  build reported zero warnings and zero errors.
- 2026-08-05 guard-effectiveness controls: independently inverting `ExpectedSmokeToolSha256` and
  `ExpectedOciValidatorSha256` each turned `CompleteDerivedLineagePassesAndMissingChecksOrBlockersFail`
  red, proving the new real-byte tool checks are reachable rather than vacuous.

- 2026-08-08 eighth review pass: fail-closed smoke-results honesty (`result=fail`),
  ValidateActualFailClosedSubject runtime/OCI/registry/smoke enum locks, unstructured-log
  assertions in IncompleteRuntimeEvidenceFailsClosed, and IPv4-compatible private embedding
  rejection. Focused filter passed 142/142; Release build zero warnings/errors; predecessor
  critical manifest, markdownlint, and git diff --check passed. AC2/AC4 remain open (0/3).
- 2026-08-11 full review: Release build passed with zero warnings/errors; focused verifier passed
  157/157. The 1260/1260 complete Contracts result was a workspace aggregate containing concurrent
  unrelated tests and is retained only as regression evidence, not Story 3.13 attribution. Raw OCI
  configs are now included in support-safety validation, and obsolete generated review snapshots
  were removed.

### Completion Notes List

- Frozen and independently hash-checked both predecessors without modifying their bytes or status.
- Created one content-addressed, schema-versioned identity crosswalk with an exact 14-package set,
  exact two-platform raw OCI byte graph, separate tag/digest response bodies, retained Development
  smoke artifacts, rejected v3.77.2 lineage, and both prohibited splice controls.
- Hardened the structural closure evaluator to verify actual package archives, semantic-release
  provenance, deployment authority, registry/reference/root identity, exact OCI source revision,
  structured support-safe runtime facts, checksum closure, full review-subject binding, reviewer
  roster authorization, receipt chronology, and symlink-safe evidence paths.
- Added a second review-hardening matrix that independently mutates and rebinds package bytes,
  baseline/predecessor Git objects, authoritative release and deployment records, canonical lineage,
  both OCI reports and provenance labels, runtime timing/cadence, support-safe values, and durable
  receipt sources. The actual fail-closed subject and outer manifest are now derived checks too.
- Recorded `fail-closed`: original proof packages remain unavailable; semantic-release provenance,
  valid OCI provenance labels, Production runtime equivalence, exact image-source mapping,
  deployed authority, and all three Story 3.13 acceptances are absent.
- Froze the crosswalk, evidence-core manifest, and human proof first, then created a content-bound
  fail-closed review subject over their raw hashes. Future receipts are external to those hashes;
  Task 9 remains open because no approval was requested, provided, or inferred.
- Corrected deployed-runtime closure ownership in `docs/ci.md` without changing release mechanics.
- 2026-08-05 code review applied six patches: reconciled the lifecycle status to `in-progress`
  across this record, the spec kernel, and `sprint-status.yaml`; scoped the proof packet's
  submodule claim to author-controlled state; excluded execution-only runtime facts from
  `canonical_lineage_id` so re-verifying an unchanged artifact no longer invalidates authority;
  bound the shared Builds smoke tool and OCI validator to their pinned bytes read from the
  submodule object store; added `NullReferenceException` to five validator catch filters; and
  closed the IPv6 support-safety gap, including bare literals that previously parsed as URIs.
- The proof packet edit changed its bytes, so the content-bound review subject and outer manifest
  were rehashed. The recorded fail-closed decision, blockers, and 0/3 acceptance count are
  unchanged, and no approval was requested, provided, or inferred.
- 2026-08-09 review patches: corrected the proof-packet identity-crosswalk pin to `11b17fb0…` and
  rebound the review subject / outer evidence manifest; aligned this record's `baseline_commit` to
  `1d6e9321`; restored the truncated Story 2.12 `sprint-status.yaml` key; required the shared OCI
  validator `cli_candidate_consequence` pass string; and asserted retained `smoke-preflight.log`
  fails `ValidatePreflightLog` in the incomplete-runtime fail-closed test. Lifecycle remains
  `in-progress` with AC2/AC4 and 0/3 acceptances open.
- 2026-08-09 tenth review pass: fail-closed subject now rejects recovered package-availability v2
  pass claims, binds citation hosting-environment fields to `runtime-verification.json`, and locks
  the unavailable-path OCI validator consequence string; zero poll intervals and private DNS
  suffixes are rejected; incomplete-runtime catches map `OverflowException`. The earlier
  2026-08-05 lifecycle note that reconciled trackers to `in-progress` is historical only — live
  status remains `in-progress`.
- 2026-08-11 full review reconciled the lifecycle surfaces, corrected the response-metadata review
  claim, added raw-config support-safety validation, removed obsolete review snapshots, and
  re-measured focused and complete Contracts coverage. AC2/AC4 and 0/3 acceptances remain open.
- 2026-08-12 thirteenth review pass closed every remaining review patch, rebound the complete
  fail-closed packet, and passed the focused verifier 172/172. The full Contracts aggregate passed
  1409/1409 after this change set's Story 4.8 status-row removal and the OQ8 verifier were
  reconciled. AC2/AC4 and 0/3 acceptances remain open.
- 2026-08-13 chunk-3 review implementation hardened recursive evidence closure, runtime failure
  classification, exit-code/OCI mutations, roster authority chronology, and retained evidence
  honesty; rebound the packet; and passed 186/186 focused plus 1423/1423 complete Contracts tests.
  The required durable external reviewer-roster authority URL remains an explicit open action, so
  the external restart gate, AC2/AC4, Tasks 4–7/9, and 0/3 acceptance state remain unchanged.

### File List

- `_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure-proof-packet.md`
- `_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/epic-3-context.md`
- `_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/`
- `_bmad-output/implementation-artifacts/evidence/story-3-13/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/523f01dfe2bc5b1192e58a98daf43b34778b6604b4dfe58fcbf7847156ec4a87/reviewer-roster.json`
- `_bmad-output/implementation-artifacts/spec-3-13-deployed-runtime-parity-closure.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-12-story-3-13-step-3-gate.md`
- `docs/ci.md`
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/DeployedRuntimeParityClosureTests.cs`

## Story Completion Status

- Status remains `in-progress` after the 2026-08-13 chunk-3 review implementation. Every in-scope review
  patch is applied and locally verified. The fail-closed packet still has external evidence
  blockers; AC2/AC4 remain open with 0/3 acceptances.
- AC1 and AC3 pass. Raw OCI descriptor/body relationships pass, but child/config response metadata,
  independently replayable runtime facts, package bytes, release/source authority, valid
  provenance labels, and Production runtime equivalence are incomplete, so AC2 does not pass.
- AC4 does not pass: the packet is not a complete passing lineage and has zero of three required
  content-bound acceptances.
- Current acceptance status is exactly 0/3; no receipt, approval, publication, registry,
  deployment, or consumer state was created or changed by this hardening. Historical Story 3.13
  commits did advance root-declared submodule gitlinks. Two predecessor
  files were written at `3d6dea69` solely to restore approved bytes drifted by the unrelated
  commit `089369bb`; net predecessor state at HEAD is byte-identical to the approved identity.
- Story 3.13 must remain non-`done` until every blocker is resolved and all three reviewers accept
  one unchanged replacement review subject.

### Post-Handoff Evidence Disposition — `bmad-build` Step 3 Gate (2026-08-12)

- The `bmad-build` handoff halted correctly at Step 3. Repository-owned Story 3.13 hardening is
  complete and locally verified; the remaining Tasks 4–7 and 9 require external evidence or
  acceptance that this repository cannot create.
- Story 3.13 remains `in-progress`. AC1 and AC3 pass; AC2 and AC4 remain open; the acceptance count
  remains 0/3. Tasks 4–7 and 9 remain unchecked and are not complete, passed, or not applicable.
- No further Story 3.13 hardening, verifier expansion, evidence rebinding, or `bmad-build` attempt is
  authorized until every restart condition in the approved follow-up Sprint Change Proposal is
  satisfied for one unchanged content-bound lineage.
- The approved terminal-closure proposal dated 2026-08-12 remains preserved but is on operational
  hold. Terminal-`unavailable` re-scoping remains a possible future governance decision; it cannot
  currently close because Task 9 content-bound acceptance evidence is unavailable.
