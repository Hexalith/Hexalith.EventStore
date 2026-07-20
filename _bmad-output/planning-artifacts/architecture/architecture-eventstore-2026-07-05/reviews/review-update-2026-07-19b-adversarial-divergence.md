# Reviewer Gate - 2026-07-19b Adversarial Divergence Review (Container-Release Delta)

- **Artifact:** `_bmad-output/planning-artifacts/architecture.md` (updated 2026-07-19; delta = AD-11 container-release paragraph, AD-22 deployed-mode container identity, Consistency Conventions Release row)
- **Lens:** construct two units one level down that each obey every AD to the letter yet build incompatibly; every surviving pair is a hole to close with a new or tightened AD.
- **Scope note:** the earlier 2026-07-19 gate cycles adversarially closed AD-24. This review attacks tonight's container delta (AD-11 ¶2 at `architecture.md:131`, AD-22 at `architecture.md:292-298`, Release row at `architecture.md:351`) plus its cross-AD seams. Grounding units: the shared Hexalith.Builds publisher (separate maintainer authority), the EventStore release caller/validator, the Story 3.12 smoke runner, the Story 1.20 parity-packet author/validator, the AD-22 deployed-mode verifier, and the AD-9 deployment-overlay unit.

## Verdict

**CHANGES REQUIRED.** The delta is directionally right — raw-bytes digest binding, exact two-platform descriptor set, fail-closed shape gate, immutable failed evidence — but the fail-closed enumeration, the identity-fetch discipline, the smoke contract, and two undefined terms each admit two letter-compliant implementations that clash. One finding is critical: the gate's explicit fail conditions enumerate only manifest shape and platform set, so a release whose arm64 image never executed anywhere (or even crashed under smoke) can pass the gate with a "separately reported" annotation while a sibling implementation blocks — both compliant. Four tightening edits to AD-11 and one to AD-22 close every surviving pair.

## Critical Findings

### F1 — Smoke outcomes are absent from the gate's fail-closed enumeration; "reported separately" is release-blocking to one implementer and advisory to another

**The clashing constructions.**

- *Unit A (Story 3.12 smoke runner + gate, reading the enumeration as exhaustive):* AD-11's only explicit gate-failure sentence is "Any other **manifest shape or platform set** fails the release evidence gate, closed" (`architecture.md:131`). Unit A validates shape, digests, and configs; the arm64 smoke cannot run (QEMU/arm64 runner unavailable — a routine CI condition). It reports the emulation failure "separately", records no product pass for arm64 — exactly as the letter demands — and passes the gate, because no enumerated fail condition fired. It even survives the stronger case: the arm64 container *runs and crashes*, and Unit A still passes the gate, because a product smoke failure is also not in the enumerated fail set.
- *Unit B (a sibling gate implementation, reading "Release validation ... smoke-runs both immutable child digests" as a required validation step):* validation that cannot complete is validation that failed; fail closed. Every release without an executed arm64 smoke blocks.

Both obey every sentence. Unit A ships a two-platform index whose arm64 child has never started anywhere — on the very story (3.12) created because arm64 was missing from v3.75.0 — and the Story 1.20 packet can cite "smoke results: amd64 pass, arm64 emulation-failure (separately reported)" as complete evidence. Unit B deadlocks the corrective release. "Never recorded as a product pass" prevents mislabeling only; it does not bind the gate outcome, and nothing states that a *product* smoke failure fails the gate either. Story 3.12's AC ("emulation/setup failure ... cannot be recorded as a pass", `epics.md:1934`) inherits the identical hole. The Release convention row (`architecture.md:351`) mirrors the same shape/platform-set-only enumeration.

**Severity:** Critical — the gate's central purpose (both platforms proven executable) is passable with zero arm64 execution, letter-compliantly.

**Minimal tightening (AD-11 ¶2):** append: "The release evidence gate passes only when every validation step completes with a recorded product pass: conforming index shape, raw-bytes digest binding, per-child config verification, and a product smoke pass for each of the two child digests. A smoke that fails, or that cannot execute for any reason — emulation, setup, or runner unavailability — leaves its platform unproven and fails the gate closed; the emulation/setup classification changes only the recorded failure category, never the gate outcome."

## High Findings

### F2 — "Validated OCI image index digest" is not one value when representation negotiation is in play; packet author, packet validator, and deployed-mode verifier can bind three self-consistent identities

**The clashing constructions.**

- *Unit A (release validator / packet author):* fetches the version tag with `Accept: application/vnd.oci.image.index.v1+json`, receives OCI index bytes, verifies SHA-256 equals the registry-reported digest `D_oci`, records `D_oci` as the AD-22 approved identity. Fully compliant: "reads the raw registry bytes, binds their SHA-256 to the registry-reported index digest".
- *Unit B (deployed-mode verifier):* observes the running workload's runtime-reported image reference. A content-negotiating registry front-end or an older client path serves the Docker manifest-list representation (`application/vnd.docker.distribution.manifest.v2+json` family) of the same logical release; the served bytes differ, the registry-reported digest for *those* bytes is `D_docker`, and the bytes-to-digest binding *also verifies* — each client's raw bytes match each client's reported digest. Unit B either fails parity forever (`D_docker` not in the packet) or, if written to trust "the digest the tag resolves to for this runtime", approves an identity Unit A never validated. A third variant: a validator that re-resolves the tag at approval time and one that trusts the recorded digest disagree whenever tag resolution and validation are separated in time.

AD-11 pins bytes-to-digest but never pins *which representation is fetched* or that the approved identity is the digest of the exact validated bytes rather than "whatever the tag resolves to". AD-22 says "the validated OCI image index digest" without saying how a verifier must acquire and compare it.

**Severity:** High — the identity is the load-bearing value of AD-22 parity closure and deployment approval.

**Minimal tightening (AD-11 ¶2 + AD-22 deployed mode):** "Validation resolves the version tag exactly once with Accept restricted to `application/vnd.oci.image.index.v1+json`, and thereafter addresses every object by digest. The approved container identity is the SHA-256 of the exact validated raw index bytes. Deployed-mode and packet comparisons use only digests recorded in the packet's provenance chain; a re-resolved tag, an alternate registry representation, or a runtime-reported digest absent from that chain never substitutes and fails closed."

### F3 — The smoke contract collides with AD-24/AD-16 fail-closed startup: a production-faithful image cannot pass a bare smoke, so one compliant smoke bricks the gate and another hollows it

**The clashing constructions.**

- *Unit A (production-faithful smoke):* runs each child digest standalone (no DAPR sidecar, no OpenBao). Per AD-24, "Every host validates its declared secrets through DAPR before becoming ready. Missing stores, bootstrap inputs, or `startup-only` secrets fail deployment/startup" (`architecture.md:320`). The conforming image therefore fails startup/readiness by design — Unit A fails *every* conforming release. Compliant with AD-11 ("smoke-runs both immutable child digests on their declared platforms") and with AD-24.
- *Unit B (relaxed smoke):* injects a smoke configuration that disables secret validation and dependencies so the container reaches Healthy. Also compliant — AD-11 defines no smoke criterion, no allowed configuration surface, and no probe. But a third reviewer-unit invoking AD-12's letter ("`202`, `200`, and mock calls are smoke signals only") can classify Unit B's run as a mock-grade signal and reject the evidence — also compliantly.

AD-11 says *that* both children are smoke-run, but not what a pass requires, which endpoint constitutes the check, or what configuration the runner may inject. Story 3.12's "bounded support-safe EventStore health smoke" is story text, not spine, and names no probe or config discipline.

**Severity:** High — either a permanent gate deadlock or an unspecified, weakenable smoke, with each side citing an AD.

**Minimal tightening (AD-11 ¶2):** "The platform smoke contract is fixed: each child digest starts standalone under a recorded smoke configuration and answers the anonymous liveness probe (`/alive`, per AD-16) with a bounded success. The smoke configuration may relax external dependencies (sidecar, secret store, state stores) only through documented configuration inputs recorded in the release evidence, never through image mutation; secret-gated readiness (AD-24) is not the smoke criterion."

### F4 — "Variant descriptor" is undefined across the Builds/EventStore authority split; `arm64` `variant: "v8"` (and `os.version`) is conforming to one maintainer and gate-fatal to the other

**The clashing constructions.**

- *Unit A (Hexalith.Builds publisher, separate maintainer authority per Story 3.12):* reads AD-11's ban list — "no duplicate, extra, `unknown/unknown`, or variant descriptor" — as a list of *unwanted additional descriptors*: a duplicate, an extra platform, an attestation placeholder, or an additional platform-*variant* entry (e.g., a second `linux/arm/v7` child). Under that reading, annotating the single required arm64 descriptor with the OCI-conventional `"variant": "v8"` (or propagating a base image's `os.version`) is conforming: the index still contains exactly one descriptor per supported platform.
- *Unit B (EventStore release validator):* reads "variant descriptor" as *any descriptor whose platform object carries a `variant` field* and fails the index. Alternatively the mirror clash: Builds omits `variant`, and a validator built around registry/runtime conventions that canonicalize arm64 as `arm64/v8` performs exact-platform matching including variant and fails the variant-absent descriptor.

Each reading is defensible from the letter; the two implementations live in different repositories under different approvers (`epics.md:1906`), which is precisely the condition under which the ambiguity becomes a standing gate deadlock or a divergent-acceptance bug. AD-11 verifies child config `os`/`architecture` "equals its descriptor" but is silent on descriptor-platform *shape*.

**Severity:** High — two-repo authority split over the exact byte shape the gate judges.

**Minimal tightening (AD-11 ¶2):** "Each descriptor's `platform` object is exactly `{"os":"linux","architecture":"amd64"}` or `{"os":"linux","architecture":"arm64"}` — no `variant`, `os.version`, `os.features`, or additional platform field. A *variant descriptor* is any descriptor whose platform carries a `variant` field, and it is nonconforming." (Absent-variant matches current .NET SDK container output; the point is that the spine, not either implementer, picks the canonical form.)

## Medium Findings

### F5 — Index media type is pinned; child media types and nesting are not: an OCI index over Docker-v2 children (or a nested index) is conforming to the publisher and nonconforming to the validator

- *Unit A (publisher):* emits a conforming OCI index whose two descriptors reference child manifests of media type `application/vnd.docker.distribution.manifest.v2+json` (a combination .NET/registry toolchains can produce). Letter-compliant: AD-11 pins only the *index* media type; the children resolve and their configs match.
- *Unit B (validator):* enforces "one immutable OCI image index" as an all-OCI object graph and fails Docker-typed children. A second sub-case: a descriptor referencing a *nested index* carrying `platform: linux/arm64` — Unit A resolves through it to the leaf manifest and verifies the config there; Unit B fails because the direct child has no config to verify. AD-11's config-verification sentence supports B, its resolve-every-descriptor sentence supports A.

**Severity:** Medium. **Tightening (AD-11 ¶2):** "Every index descriptor directly references an image manifest with media type `application/vnd.oci.image.manifest.v1+json`; nested indexes and Docker media types anywhere in the validated graph are nonconforming."

### F6 — Failed-release tag disposition: "immutable" plus "corrected only by a later semantic version" lets one team delete or re-point the v3.75.0 container tag and another treat exactly that as evidence tampering

- *Unit A (registry hygiene implementer):* "Published artifacts are immutable" refers to content-addressed objects; tags are pointers. To prevent accidental deployment of the failed release, Unit A deletes (or re-points to a tombstone) the `v3.75.0` container tag while preserving the digest-addressed manifests and the recorded evidence. Nothing in AD-11's letter binds *tag lifecycle*; Story 3.12's no-overwrite AC covers overwriting, arguably not deletion, and covers only v3.75.0, not the class.
- *Unit B (evidence auditor / Story 1.20 revalidator):* treats "remains recorded as non-authorizing failed evidence" as requiring the registry object to stay *resolvable*, and classifies tag deletion as artifact mutation that voids the evidence chain. Unit B fails a packet Unit A considers clean.

**Severity:** Medium — process clash, both letter-compliant; also leaves open whether a *conforming* release's tags may ever move (AD-11's present-tense "the release version tag resolves to" is the only guard).

**Tightening (AD-11 ¶2):** "Release tags — conforming and failed — are never re-pointed or deleted; the failed version's tag and registry objects remain resolvable as recorded evidence. Tag resolution never authorizes deployment; only the recorded validated index digest does (AD-22), so a resolvable failed tag is not a deployment hazard."

### F7 — The two-platform contract binds "The EventStore container" (singular) while AD-21 fixes an `eventstore-admin-ui` container identity and the topology deploys it: the release unit and the deployment unit build incompatibly on arm64

- *Unit A (release implementer):* AD-11 ¶2 and Story 3.12 scope the container contract to the single `eventstore` image ("only `eventstore` is published as a container", `epics.md:1944`). If/when `eventstore-admin-ui` (whose *container identity* AD-21 explicitly fixes, `architecture.md:286`) or a sample host is containerized for deployment, Unit A publishes it single-platform amd64 with no index validation — no AD governs its registry shape, platform set, or approved identity.
- *Unit B (deployment overlay / AD-9 topology unit):* deploys the full topology (`architecture.md:406-445` includes `eventstore-admin-ui` as an orchestrated resource) onto mixed or arm64 node pools, reasonably assuming platform parity because the platform's flagship image guarantees both platforms. Admin UI fails to schedule or silently degrades; AD-9's topology tests pass because they assert app IDs/scopes, not image platforms. Both units letter-compliant; AD-22's "deployed EventStore container identity" is likewise singular and gives the verifier no mandate over the co-deployed image.

**Severity:** Medium (latent until admin-ui ships as a container, but the delta just created the asymmetry and two teams can already disagree about whether it silently binds admin-ui).

**Tightening (AD-11 ¶2):** "The `eventstore` repository is currently the only released container image. Any future externally released container image — including `eventstore-admin-ui` — adopts this same index/platform/validation contract and an AD-22 identity mapping before its first release; deployment profiles must not require a platform of any image that no release evidence proves."

### F8 — AD-22's identity chain names index and child-manifest digests but not config digests; a runtime that reports only the config digest splits verifiers into fail-closed-forever vs invented-mapping

- *Unit A (packet author):* records index digest + two child manifest digests, and treats config digests as validation internals (AD-11 verifies configs; AD-22's mapping sentence names only "a per-platform child manifest digest"). Compliant.
- *Unit B (deployed-mode verifier):* on its platform the observable identity of the running workload is the image *config* digest (the common `Image` field), not a manifest digest. AD-22's letter authorizes mapping only child-manifest digests through provenance, "never as a substitute identity" for anything else — so Unit B must either fail closed permanently (config digest is not an authorized chain member) or invent a config→child→index mapping the packet never recorded. A sibling verifier makes the opposite choice. Both compliant, one blocks parity forever, one approves through an unrecorded mapping.

**Severity:** Medium. **Tightening (AD-22 deployed mode):** "The packet records the full identity chain: index digest, both child manifest digests, and both child config digests. Deployed mode may observe any chain member and maps it to the approved index digest through the recorded chain; any observed identity absent from the chain fails closed."

## Low Findings

### F9 — Index-level fields and repository co-objects are out of the shape contract: `subject`/`artifactType`/annotations and OCI-1.1 referrer objects (SBOM/attestation manifests) are "extra" to one validator and out-of-scope to another

The descriptor rules say nothing about index-level `subject`, `artifactType`, or `annotations`, nor about additional untagged manifests a publisher pushes into the `eventstore` repository via the referrers convention. One validator scopes "no extra" to descriptors only (letter-accurate); another fails the release because unexpected registry objects appeared alongside it. Digest identity is unaffected (raw-bytes rule), so this is gate-friction, not identity corruption. **Tightening:** one sentence declaring index-level annotation fields permitted-but-non-authorizing (or forbidden) and repository co-objects outside the validated graph explicitly out of gate scope (they confer no evidence).

### F10 — Container release evidence durability is not in AD-12's enumeration

AD-12 enumerates persisted Redis/state-store/read-model/CloudEvent bodies, topology YAML/sidecar arguments, package outputs, and security denials (`architecture.md:137`). Registry-inspection and smoke evidence is not listed; one implementer persists it as a durable artifact, another leaves it in workflow logs with bounded retention. Story 1.20's independent revalidation then finds evidence expired — accept-citation vs fail-closed divergence. **Tightening:** add "container release registry/smoke evidence" to AD-12's enumeration.

### F11 — AD-9 overlays may pin deployment images by tag, letter-compliantly, guaranteeing recurring AD-22 deployed-mode failures

AD-9 binds AppHost + DAPR YAML but says nothing about image references; an overlay pinning `:vX.Y.Z` (mutable pointer) is compliant while AD-22 requires the running image to map by digest. Fail-closed catches drift, so this is friction, not corruption. **Tightening (Runtime topology convention row or AD-9):** "deployment profiles reference the EventStore image by its validated index digest, not by tag."

## Attacks Constructed And Discarded (require violating an AD's letter)

- *Same child digest listed under both platforms:* the per-child config `os`/`architecture` equality check makes one of the two entries nonconforming; no compliant construction.
- *Canonicalize-then-hash validator (annotation stripping):* violates "reads the raw registry bytes, binds their SHA-256" directly.
- *Correction semver granularity (v3.75.1 vs v3.76.0):* "a conforming later semantic version" admits both; the choice is not observable by any other unit and produces no incompatible build.
- *Re-pointing the version tag of a conforming release post-validation:* under the delta's present-tense "the release version tag resolves to" plus artifact immutability, at least one sentence is violated by the re-pointer; the residual ambiguity (deletion, failed tags) is retained as F6 rather than as a compliant re-point.
- *Descriptor ordering / whitespace divergence:* identity is digest-of-raw-bytes; order is frozen by the published bytes and no unit judges order independently.
- *Dockerfile-built children indistinguishable from SDK-built:* an implementer skipping SDK container support violates the letter outright; provenance verifiability is an evidence concern already covered by Story 3.12's workflow-run recording, not a compliant divergence.

## Disposition Summary

| # | Finding | Severity | Close by |
| --- | --- | --- | --- |
| F1 | Smoke outcomes absent from fail-closed enumeration; emulation-failure advisory vs blocking | Critical | AD-11 ¶2 gate-pass enumeration incl. per-platform product smoke pass; unexecutable smoke fails closed |
| F2 | Dual-representation / negotiation makes "validated index digest" plural | High | AD-11+AD-22: single Accept-pinned fetch, digest-of-validated-bytes identity, chain-only comparison |
| F3 | Smoke vs AD-24 fail-closed startup: gate deadlock or hollow smoke | High | AD-11 smoke contract: recorded config, `/alive` liveness criterion, no image mutation |
| F4 | "Variant descriptor" undefined across Builds/EventStore authorities (arm64 v8) | High | AD-11 exact platform-object shape; define variant descriptor |
| F5 | Child media type / nested index unpinned | Medium | AD-11 pin child media type, forbid nesting |
| F6 | Failed-tag re-point/delete/resolvable divergence | Medium | AD-11 tag lifecycle: never re-point/delete; digest-only authorization |
| F7 | Two-platform rule scoped to `eventstore` only; admin-ui container ungoverned | Medium | AD-11 sole-released-image statement + forward rule for future containers |
| F8 | Config digest not an authorized AD-22 chain member | Medium | AD-22 full identity chain incl. config digests |
| F9 | Index-level fields / referrer co-objects scope | Low | one scope sentence in AD-11 |
| F10 | Container evidence not in AD-12 enumeration | Low | extend AD-12 list |
| F11 | Overlay tag-pinning vs digest verification friction | Low | Runtime topology convention row |

Gate result: **CHANGES REQUIRED** — F1 must land before the delta can pass this lens; F2-F4 should land in the same edit; F5-F8 are one sentence each in the same two ADs; F9-F11 are discretionary hardening.
