# Reviewer Gate - 2026-07-19b Adversarial Divergence Re-Review (Container-Release Delta, Post-Correction)

- **Artifact:** `_bmad-output/planning-artifacts/architecture.md` (corrected 2026-07-19; AD-11 ¶2 at `architecture.md:131`, AD-12 Rule at `architecture.md:137`, AD-22 deployed-mode at `architecture.md:292-298`, Runtime topology row at `architecture.md:350`, Release row at `architecture.md:351`)
- **Prior review:** `review-update-2026-07-19b-adversarial-divergence.md` (F1 critical, F2-F4 high, F5-F8 medium, F9-F11 low)
- **Lens:** same as prior — construct two units one level down that each obey every AD to the letter yet build incompatibly.
- **Grounding checked this cycle:** `references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py`, `smoke_container_platforms.py`, `publish-containers.sh`, `action.yml`; `.github/workflows/release.yml:39-40` (container-projects = exactly `src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore`).

## Verdict

**PASS.** All eleven prior findings are closed by the corrected text, each verified against the current AD wording and, where applicable, against the shared Builds tooling the spine names as authority. Two new low-severity findings surfaced (a one-word spine/validator letter mismatch on present-but-empty `variant`, and an over-broad "any image" in the unproven-platform clause); neither admits a divergence pair with realistic probability or blocks the delta, and each closes with a single scoping word. No critical, high, or medium finding is open.

## Per-Finding Closure Table

| # | Prior finding | Severity | Status | Closure evidence (current text) |
| --- | --- | --- | --- | --- |
| F1 | Smoke outcomes absent from fail-closed enumeration; emulation-failure advisory vs blocking | Critical | **Closed** | AD-11 ¶2: enumerated fail set now includes "product smoke failure"; "emulation or environment setup failure is classified separately from a product-image failure but blocks the gate equally: a platform whose smoke cannot run is unproven, not passed." Both prior constructions (advisory annotation, crash-yet-pass) now violate the letter. Smoke tooling agrees: `smoke_container_platforms.py:32` classifies `environment/emulation-setup-failure` distinctly and the gate blocks on it. |
| F2 | Dual-representation negotiation makes "validated index digest" plural | High | **Closed** | AD-11: "a Docker manifest list or single-image manifest served for the tag is nonconforming" pins the served representation; validation "binds their SHA-256 and byte length to the registry-reported index digest"; "Tag resolution never authorizes deployment; only the recorded validated index digest does." AD-22: verifier "never re-derives identity from an alternate representation of the tag, and an observed identity absent from the chain fails closed." The Accept-pinned-single-fetch sentence was not adopted verbatim, but the pair is dead: a registry serving a non-OCI representation fails the gate, and a runtime observing an off-chain identity fails closed — no compliant path approves `D_docker`. Residual is fail-closed friction, not divergent approval. |
| F3 | Smoke vs AD-24 fail-closed startup: gate deadlock or hollow smoke | High | **Closed** | AD-11: "the same bounded support-safe health smoke on both immutable child digests under the smoke contract's declared minimal configuration; a dependency absent from that declared configuration is part of the contract, not grounds to skip or substitute the check." The production-faithful-deadlock unit now violates the letter (it substitutes a non-contract configuration); the skip excuse is expressly removed; and AD-12's enumeration now grants "container release registry and smoke evidence" first-class persisted-evidence standing, so the mock-grade-rejection unit lost its letter. Grounding: the shared smoke contract starts each child standalone under a declared env configuration and polls `/alive` (`smoke_container_platforms.py:200-257`) — liveness, which AD-24 does not gate (AD-24 gates *readiness*), so no AD collision remains. |
| F4 | "Variant descriptor" undefined across Builds/EventStore authorities | High | **Closed** | AD-11: "no duplicate, extra, or `unknown` platform and no platform `variant` field", plus "The conforming registry shape has one authority - the SHA-pinned shared Hexalith.Builds publisher/validator … neither Builds nor the EventStore caller reinterprets it locally, and shape changes route through an approved proposal and a Builds change." Validator agrees: `oci_registry_validator.py:260-261` fails `variant-not-allowed`. The two-repo split is dissolved by the single-authority sentence. (One-word residual → new finding N1, low.) |
| F5 | Child media type / nested index unpinned | Medium | **Closed** | AD-11: each descriptor "directly referencing an image manifest (nested indexes are nonconforming)". Resolved in the validator-consistent direction rather than the OCI-only direction the prior review proposed: the validator accepts child media types {OCI image manifest, Docker v2 image manifest} (`oci_registry_validator.py:17-22,247-248`) and rejects index-typed children (`unsupported-child-media-type`); the spine's "image manifest" wording covers both accepted types and its nested-index ban matches the rejection. **No spine/validator contradiction remains** — a validator failing Docker-typed children would now be a forbidden local reinterpretation of the single-authority shape. |
| F6 | Failed-tag re-point/delete/resolvable divergence | Medium | **Closed** | AD-11: "release tags, conforming and failed, are never re-pointed or deleted; a nonconforming release (v3.75.0) remains resolvable as non-authorizing failed evidence … Tag resolution never authorizes deployment; only the recorded validated index digest does (AD-22)." Verbatim adoption of the proposed tightening; both prior units now have a letter to violate. |
| F7 | Two-platform rule scoped to `eventstore` only; admin-ui container ungoverned | Medium | **Closed** | AD-11: "Released container repositories are exactly the release workflow's `container-projects` mapping - currently the single `eventstore` repository" (verified against `release.yml:39-40`); "Any future externally released container image, including `eventstore-admin-ui`, adopts this same index/platform/validation contract and an AD-22 identity mapping before its first release; until then AD-21's `eventstore-admin-ui` is an AppHost/deployment topology identity, not a released registry artifact." Release row mirrors it. AD-21's "container identity" phrase is explicitly disambiguated by AD-11's "AppHost/deployment topology identity" recharacterization — no internal contradiction. (Wording residual on the trailing clause → new finding N2, low.) |
| F8 | Config digest not an authorized AD-22 chain member | Medium | **Closed** | AD-22 deployed mode: "the packet records the full identity chain - index digest, both child manifest digests, and both child config digests. A deployed-mode verifier may observe any chain member and maps it to the approved index digest only through that recorded chain … an observed identity absent from the chain fails closed." The config-digest-reporting runtime now has an authorized mapping; the invented-mapping and fail-forever units both lost their letter. |
| F9 | Index-level fields / referrer co-objects scope | Low | **Closed** | AD-11: "Index-level annotations and repository co-objects outside the validated descriptor graph confer no release evidence." Combined with the closed-form fail enumeration and single shape authority, a validator expanding the gate to co-objects would be a local reinterpretation. |
| F10 | Container evidence not in AD-12 enumeration | Low | **Closed** | AD-12 Rule (`architecture.md:137`) now enumerates "container release registry and smoke evidence" among persisted-evidence artifacts. |
| F11 | Overlay tag-pinning vs digest verification friction | Low | **Closed** | Runtime topology convention row (`architecture.md:350`): "Deployment profiles reference the released EventStore image by its validated OCI image index digest, not by a mutable tag (AD-11, AD-22)." |

## New Findings (against the corrected text only)

### N1 — Present-but-empty `variant` field: spine letter forbids the field, the sole shape authority tolerates it (Low)

- *Unit A (Builds validator, the named single authority):* `oci_registry_validator.py:260-261` fails only a variant "not in (None, \"\")" — a descriptor carrying `"variant": ""` **passes** the authority.
- *Unit B (any spine-conformance auditor — e.g., a Story 1.20 packet reviewer or a gate re-reviewer):* AD-11's letter says "no platform `variant` field"; a present-but-empty field is a field. Unit B marks the release nonconforming and refuses to let its evidence authorize AD-22 closure.

Both letter-compliant; but the spine's own claim — that the validator *is* the shape and neither side reinterprets — is what makes any letter gap between them a defect rather than a tolerance. Probability is near zero (.NET SDK container tooling does not emit empty variants), and the fix is one word on either side: tighten the Builds validator to reject any present `variant` key (routed as a Builds change per the spine's own rule), or annotate the spine "no platform `variant` field (present-but-empty included)". Non-blocking.

### N2 — "Deployment profiles must not require a platform of any image that no release evidence proves" reads over-broadly as banning all non-released images (Low)

- *Unit A (AD-9/topology implementer):* deploys the full topology including `eventstore-admin-ui` — which the same AD-11 sentence expressly frames as "an AppHost/deployment topology identity" — from a profile-built (non-released) image on the profile's platform.
- *Unit B (release-governance auditor):* reads "any image" universally: a non-released image has no release evidence, hence proves no platform, hence any profile scheduling admin-ui (or any locally built image, including dev) violates the clause. Unit B blocks every profile the topology section requires.

Unit B's reading proves too much — it contradicts the first half of its own sentence and the AppHost dev-mode text in AD-24 — which is why this is low rather than medium: the divergence requires reading the clause against its immediate context. Still, the letter admits it. Fix is one scoping phrase: "must not require of any **released registry image** a platform its release evidence does not prove; non-released topology identities are proven by the deploying profile's own build/validation." Honesty note: the adopted clause is verbatim from prior F7's proposed tightening — this residual is the prior review's own wording, refined here rather than manufactured against the correction.

## Validator/Spine Conflict Check (explicitly requested)

**No conflict.** The spine requires each index descriptor to "directly reference an image manifest" with "nested indexes … nonconforming"; the shared validator accepts exactly {`application/vnd.oci.image.manifest.v1+json`, `application/vnd.docker.distribution.manifest.v2+json`} as child media types and fails anything else — including index/manifest-list-typed children — with `unsupported-child-media-type`. A Docker v2 schema-2 manifest *is* an image manifest, so the spine's wording covers both accepted types; the spine's "Docker manifest list … nonconforming" is scoped to what is "served for the tag" (the top-level object) and does not contradict Docker-typed children. The only letter gap found anywhere between spine and validator is N1's empty-`variant` tolerance.

## New Attacks Constructed And Discarded

- *Floating/`latest` tag re-pointing vs "release tags are never re-pointed":* the publisher (`publish-containers.sh`) validates strict SemVer and pushes no floating tags; the ambiguity ("is `latest` a release tag?") has no live construction, and introducing a floating tag would itself be a Builds change routed through proposal. Latent-only; no compliant pair today.
- *Cross-release chain-member collision (identical child config digest recorded in two packets):* the config digest covers rootfs diff_ids, so a colliding member means byte-identical running content; AD-22 verification is scoped to a single packet's recorded chain, so no ambiguous mapping arises and no behavioral divergence exists.
- *Smoke-contract authority split (caller-declared vs Builds-declared minimal configuration):* the smoke helper ships inside the same SHA-pinned Builds action the release workflow consumes (`action.yml:44-63`), its configuration is declared in the script and its evidence persisted under AD-12; a packet validator verifies against the *declared* configuration recorded in evidence, leaving no second self-consistent contract to diverge to.
- *Index byte-length source ambiguity:* no second unit judges the index's own length independently; identity is digest-of-raw-bytes and the descriptors carry child sizes, which the validator checks (`child-size-mismatch`).
- *AD-21 "container identity" vs AD-11 "not a released registry artifact":* AD-11 explicitly recharacterizes the AD-21 identity as an AppHost/deployment topology identity until first release; precedence is stated, not inferred — no clash.
- *Packet author re-deriving config digests from gate evidence:* recorded objects are digest-addressed; fetching them by digest is deterministic and is not "an alternate representation of the tag" — no divergence.
- *Deeper-probe revalidator (demanding `/ready` over `/alive`):* the letter gives a revalidator no criterion to demand beyond the smoke contract's declared check; the rejection is invented, not letter-grounded.

## Disposition Summary

| # | Finding | Severity | Status |
| --- | --- | --- | --- |
| F1 | Smoke/emulation fail-closed disposition | Critical | Closed |
| F2 | Identity representation negotiation | High | Closed |
| F3 | Smoke vs AD-24 startup collision | High | Closed |
| F4 | Variant authority split | High | Closed |
| F5 | Child media types / nesting | Medium | Closed |
| F6 | Failed-tag disposition | Medium | Closed |
| F7 | Admin-ui container asymmetry | Medium | Closed |
| F8 | Config-digest chain gap | Medium | Closed |
| F9 | Index annotations / co-objects | Low | Closed |
| F10 | AD-12 evidence enumeration | Low | Closed |
| F11 | Tag-pinned overlays | Low | Closed |
| N1 | Empty-`variant` spine/validator letter mismatch | Low | New — non-blocking; one-word alignment (Builds validator tightening preferred) |
| N2 | Over-broad "any image" in unproven-platform clause | Low | New — non-blocking; one scoping phrase in AD-11 ¶2 |

Gate result: **PASS.** The corrected delta survives the adversarial-divergence lens; N1 and N2 are discretionary hardening for the next editorial pass, not gate conditions.
