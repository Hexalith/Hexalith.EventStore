# Reviewer Gate - 2026-08-16 Adversarial Divergence Review

- **Artifact:** `_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/ARCHITECTURE-SPINE.md`
- **Lens:** construct independently built release/evidence/consumer units that obey AD-11 and AD-22 literally but try to select incompatible provenance, authority, acceptance, and removal outcomes; then sweep the full spine for contradictions introduced by the August 16 amendments.
- **Deterministic pre-pass:** `uv run .agents/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05` returned `ok: true`, zero findings.
- **Mutation posture:** review only; the spine was not edited.

## Verdict

**FAIL - one critical and three high-severity literal-compliance holes remain in the August release/parity contract.** The spine correctly keeps the rejected `v3.94.1` disposition non-authorizing and correctly makes immutable OCI digests, rather than mutable tags, the deployment identity. However, it asserts that package/workflow/image facts form one exact lineage without fixing the evidence graph that proves that assertion; it does not content-bind Story 3.14 release authority; it leaves positive acceptance roles and receipt identity under-specified; and it leaves consumer-removal authority, required capabilities, and applicable parity modes open. Two independently built units can therefore obey the prose yet disagree about whether a corrective release, positive Story 3.15 packet, or destructive consumer migration is valid.

## Adversarial Constructions

The two units below are deliberately plausible one-level-down implementations, not arbitrary hostile readings.

| Axis | Unit A - content-addressed release/acceptance gate | Unit B - label-and-packet gate |
| --- | --- | --- |
| Story 3.14 authority | Requires one durable authority record naming repository, exact semantic version, source SHA, registry/repository, package scope, platforms, owner, validity window, and a digest of that record | Accepts a durable Release-owner approval saying “publish the next Story 3.14 corrective release”; chooses source/version/workflow later |
| Source/package/workflow/image link | Requires the workflow run/attempt, EventStore workflow revision, Builds execution SHA, package-manifest digest, every package digest, OCI graph digests, and independently checked edges back to the authorized source SHA | Verifies exact package versions/hashes, exact OCI digests, and the child labels; trusts the packet’s statement that those separately valid objects belong to one lineage |
| Provenance values | Uses lowercase 40-hex revision, unprefixed SemVer `3.95.0`, one byte-exact repository URL, and fixed expected URL comparison rules | Uses uppercase 40-hex revision, tag-shaped version `v3.95.0`, a `.git`/trailing-slash repository spelling, and mutable project/documentation HTTPS URLs; treats each as exact after local normalization |
| Story 3.13 disposition | Hashes one canonical disposition envelope and requires three receipts naming that digest and the referenced immutable evidence-subject digest | Lets each reviewer receipt identify a rendered/logically equivalent envelope; all receipts describe one disposition but need not carry one canonical digest |
| Story 3.15 acceptance | Requires EventStore owner, Release owner, and Test Architect to accept one SHA-256 subject that recursively fixes the whole new lineage | Treats the general “EventStore-owner-reviewed parity packet” rule as the required acceptance; “unchanged-subject” means the packet was not edited during that one review |
| Consumer removal | Requires an authoritative capability catalog, every mode the consumer actually uses, and separate Consumer-owner approval naming the repository commit and removal scope | Lets the packet producer declare the required capabilities and `deployed` as the only applicable mode; treats an EventStore-owner-reviewed positive packet as sufficient to delete local infrastructure |

Unit A rejects Unit B’s release authority, annotation spellings, lineage proof, acceptance receipts, and removal permission. Unit B can point to every literal noun currently present in AD-11/AD-22: a durable Release-owner authority, exact package hashes, a validated OCI digest chain, well-formed identical labels, an owner-reviewed packet, required acceptances as it defines them, and all capabilities it classified as required. That surviving pair is the basis for the findings below.

## Critical Finding

### C1 - “One exact lineage” is asserted, but no normative identity graph proves source/package/workflow/image equality

**Evidence:** AD-11 says required child-config provenance must be “consistent with the package/workflow/release lineage” (`ARCHITECTURE-SPINE.md:153`). AD-22 requires a packet to map one approved source SHA to exact package versions/hashes and the validated OCI index/child/config chain (`:315-319`). Neither rule fixes the release identity record, the workflow identity, the package-manifest identity, the package hash algorithm/canonical bytes, or the proof edge showing that package and image bytes were produced from the authorized source. The SHA inside `org.opencontainers.image.revision` is output-controlled metadata; equality between that label and a packet field proves label agreement, not the build input that produced the filesystem or packages.

**Two literal-compliant, unsafe units:** Unit A accepts only a content-addressed graph rooted in a named workflow run/attempt and exact EventStore/Builds revisions. Unit B independently validates package bytes and registry bytes, observes that both child configs claim source SHA `S`, and records a packet mapping those objects to `S`. Unit B can combine packages from workflow `W1` and an image from `W2`, or accept an image built from a different checkout but labeled `S`, without violating an enforceable sentence: “mixed lineage” is forbidden, but the spine does not define the evidence needed to detect it. Both objects may be immutable and internally valid; the unsafe outcome is a positive packet for a lineage that was never proven to exist.

**Impact:** Story 3.15 can select a deployment digest whose runtime bytes do not correspond to the approved package/source subject. That defeats the exact-identity purpose of FR36 and can authorize consumers against a different runtime than the package evidence proves.

**Disposition: mandatory architecture fix.** Define one canonical release identity record and its comparison rules. At minimum bind: repository; unprefixed semantic version and release tag; exact source SHA; EventStore workflow file/revision plus run/attempt; exact SHA-pinned Builds execution/publisher/validator identity; release-authority-record digest; `tools/release-packages.json` or package-manifest digest; every package ID/version/content digest with a named algorithm and canonical bytes; registry/repository; OCI index, child manifest, and config digests/sizes; and smoke evidence. Require Story 3.15 to reconstruct and independently validate every edge from retained raw bytes and trusted workflow facts rather than copying labels or Story 3.14 pass flags. A missing or merely asserted edge fails closed.

## High Findings

### H1 - Story 3.14 release authority is durable but not bound to the release it authorizes

**Evidence:** AD-22 requires only “separate durable release-owner authority before publishing a corrective semantic release” (`:333-334`). It does not say what that authority must name, how it is content-bound, when it expires, or that the actual external writes must exactly equal its subject. AD-11 constrains the artifact after publication but does not authorize the write.

**Two literal-compliant, incompatible units:** Unit A uses a one-use authority for repository `R`, version `V`, source `S`, registry/repository `G`, 14-package scope, two platforms, owner, rationale, and validity window. Unit B persists a signed/recorded approval for “the next corrective Story 3.14 release,” then selects `V`, `S`, `G`, package set, and publisher revision after approval. Both possess separate durable Release-owner authority before publishing; only Unit A prevents a stale or over-broad authorization from being replayed against a materially different release.

**Impact:** release, NuGet, and registry mutations can exceed the owner’s intended subject while the resulting objects still satisfy AD-11 technically. Story 3.15 validation after the fact cannot retroactively authorize those writes.

**Disposition: mandatory architecture fix.** Make the authority record a required node of the C1 identity graph. Bind repository, semantic version/tag, source SHA, exact registry/repository, exact package inventory/count, platforms, publisher/validator revision, owner, timestamp, rationale, validity window, and one-use/replay semantics. Require equality between the authority subject and every attempted external write before the first write occurs; expired, missing, broader, or mismatched authority fails closed.

### H2 - Acceptance is asymmetric and “content-bound/unchanged-subject” has no canonical receipt contract

**Evidence:** The Story 3.13 amendment names three roles and “one content-bound negative disposition” (`:330-332`) but defines neither the canonical subject bytes/digest nor receipt fields. Story 3.15 then requires “unchanged-subject acceptances” (`:334-335`) without naming the roles, count, digest algorithm, referenced lineage, accepted outcome, or immutability check. The general AD-22 Rule requires only an “EventStore-owner-reviewed” packet (`:315`). These clauses permit different positive acceptance floors and do not prove that multiple reviewers accepted the same bytes.

**Two literal-compliant, incompatible units:** Unit A requires EventStore owner, Release owner, and Test Architect receipts that each name the same SHA-256 digest of a canonical subject containing the entire C1 lineage and outcome. Unit B treats logical/rendered equivalence as “content-bound” for Story 3.13 and treats the EventStore owner’s unchanged packet review as the “required” Story 3.15 acceptance because no positive role set is stated. Both can truthfully claim an accepted unchanged subject under the current Rule; Unit A rejects Unit B’s `done` transition.

**Impact:** a packet may change between role reviews, omit a required reviewer, or obtain acceptance for a subject that does not recursively bind the selected OCI identity. Human review then becomes a label rather than evidence, allowing premature positive FR36 closure.

**Disposition: mandatory architecture fix.** Define one canonical subject representation and SHA-256 digest for each disposition/parity outcome. Every receipt must name role, authenticated reviewer identity, exact subject digest, exact outcome (`rejected-non-authorizing` or positive parity), timestamp, and any expiry/revocation semantics. Require the EventStore owner, Release owner, and Test Architect for both Story 3.13 and Story 3.15, all against the same immutable subject; planning approval, release authority, and story completion are not substitute receipts. Any subject or referenced-identity change invalidates all prior receipts.

### H3 - Consumer-removal prerequisites do not identify who authorizes deletion, what capabilities are required, or which modes must match

**Evidence:** AD-22 says removal happens “only after” an EventStore-owner-reviewed packet marks “every required capability” available and proves source, package, and, “when applicable,” deployed identities (`:315-319`). It never names the authority for the required-capability set, defines when source/package/deployed modes are applicable or conjunctive, identifies the consumer/repository commit and exact removal scope, or requires Consumer-owner acceptance. The August text says Story 3.13’s negative result cannot permit removal (`:321,329-336`), but it does not make the later positive packet sufficient or insufficient. The final “No result … grants … consumer-migration authority” (`:335-336`) can be read as scoped to `v3.94.1` or to all three story results; either reading leaves the missing positive action authority unresolved.

**Two literal-compliant, unsafe units:** Unit A uses an architecture-owned capability catalog, requires both package and deployed parity for a consumer that compiles against packages and calls the deployed service, and obtains separate Consumer-owner approval for a named commit/removal diff. Unit B authors its own shorter “required” set, declares only deployed mode applicable, and treats the EventStore owner’s positive parity review as permission to remove the local projection/query path. Both remove only after the AD-22 packet; only Unit A proves that the exact consumer can survive the exact deletion.

**Impact:** independently migrated consumers can delete different infrastructure against different capability/mode subsets. A positive EventStore release result can be mistaken for authority over another repository, repeating precisely the destructive cross-repository drift AD-22 says it prevents.

**Disposition: mandatory architecture fix.** Bind the parity packet to an authoritative capability catalog/version, exact consumer repository and commit, exact proposed removal scope, and an explicit mode matrix derived from actual dependency/runtime edges. State that source/package/deployed requirements are conjunctive whenever the consumer uses more than one. Separate evidence from action: Story 3.15 may make a deployed identity available, but only authenticated Consumer-owner approval of the unchanged packet and unchanged removal subject authorizes deletion; EventStore/Release/Test acceptances do not grant cross-repository mutation authority.

## Medium Findings

### M1 - Required provenance values are well-formed but not byte-canonical

**Evidence:** AD-11 requires an “exact public EventStore repository URL,” absolute public HTTPS project/release and documentation URLs, an “exact 40-character release source SHA,” and an “exact semantic release version” (`:153`). It does not publish the exact expected source literal, define URL comparison/normalization, require lowercase hexadecimal, or distinguish package SemVer `3.95.0` from tag-shaped `v3.95.0`. Children must agree with each other, but an independent Story 3.15 validator still has no single comparison rule.

**Divergence:** Unit A compares byte-exact canonical values; Unit B normalizes URL spelling and SHA case and treats `v3.95.0` as the semantic release version. A release that Unit B passes is rejected by Unit A. Mutable project/documentation URLs can also be well formed yet point at changing content; they should not silently acquire identity authority.

**Impact:** fail-closed implementations disagree at handoff, or an overly permissive implementation accepts provenance another conforming validator rejects. This is primarily convergence/availability risk; C1 is the unsafe identity risk.

**Disposition: fix with C1.** Publish the canonical repository literal; specify byte-exact comparison after no normalization (or one explicitly named normalization); require lowercase 40-hex revision; require unprefixed canonical SemVer for `.version` and separately record the `v`-prefixed release tag. State that `.url` and `.documentation` are support links, not identity edges, unless revision-pinned URLs are deliberately required.

### M2 - AD-11’s own `Binds` list omits FR36 while the capability map says AD-11 governs FR36 closure

**Evidence:** AD-11 binds FR10, FR21-FR22, FR25, NFR9-NFR11, and NFR16-NFR17 (`:147`), but the capability map assigns FR36 deployed-runtime parity closure to AD-11, AD-12, and AD-22 (`:561`). The August provenance amendment is load-bearing for Stories 3.14-3.15 and FR36, yet the invariant’s primary traceability declaration does not say so.

**Divergence:** A downstream planner selecting governing decisions from each AD’s `Binds` field omits AD-11 from an FR36 slice; another selecting from the capability map includes it. Both follow a spine-owned traceability surface and produce different acceptance contracts.

**Impact:** the provenance requirements can disappear from generated FR36/story work even though prose elsewhere expects them.

**Disposition: autofix.** Add FR36 to AD-11’s `Binds` list (and keep the capability map unchanged).

## Attacks That Did Not Survive The Literal Rules

### Negative disposition versus positive parity - closed

AD-22 is explicit that Story 3.13 owns a rejected, non-authorizing disposition, may finish only on that negative outcome, and cannot mark deployed parity available, select an image, authorize deployment, permit removal, or substitute for Story 3.15 (`:321,325-336`). A unit that marks `v3.94.1` as positive or deployable violates the letter. The finding is receipt binding (H2), not negative-versus-positive semantics.

### Mutable tags versus immutable digests - closed for deployment identity

AD-11 forbids re-pointing or deleting conforming and failed release tags and says tag resolution never authorizes deployment (`:153`). AD-22 records the validated OCI index/child/config digest chain and rejects an observed identity outside it (`:319`); the Runtime topology convention requires deployment profiles to use the validated index digest rather than a mutable tag (`:453`). A tag-only deployment unit is not compliant. C1 concerns the unproven relationship between separately valid artifacts, not tag mutability.

### Story 3.14 publication versus Story 3.15 validation - ownership split is conceptually sound

The spine assigns publication to separately authorized Story 3.14 and independent positive validation to Story 3.15 (`:321,333-335`). A unit that lets Story 3.14 self-approve deployed parity or lets Story 3.15 publish the corrective release violates the letter. The open holes are the subject of the authority (H1), the identity graph independently validated (C1), and the acceptance floor (H2), not the two-story split itself.

### Existing Story 2.12 exception - no August contradiction found

The dated Tenants exception remains explicitly limited to one story/consumer and denies authority for deployed mode or future consumers (`:323`). The August amendment neither generalizes nor silently reuses it. Its unusual source/package relief therefore does not close or worsen C1-H3 for Stories 3.14-3.15.

## Full-Spine Contradiction Sweep

- **One traceability contradiction is open:** M2’s AD-11 `Binds` list versus the FR36 capability map.
- **One action-authority ambiguity is open:** H3’s general removal gate versus the August “No result … grants consumer-migration authority” language. The safe reading is that a positive packet is evidence only, but the spine does not name the additional Consumer-owner authority that completes the action.
- **No stale positive `v3.94.1` statement survives.** AD-11, AD-22, the Release convention, Runtime topology convention, and FR36 capability row all treat it as failed/non-authorizing or route positive closure to the later release.
- **No tag/digest contradiction survives.** The validated index digest is consistently the deployment identity.
- **No Story 3.14/3.15 ownership reversal survives.** Publication and independent parity validation remain separate.
- **No conflict with AD-12:** AD-12 demands persisted release evidence, but it cannot repair C1-H3 until AD-11/AD-22 define the exact evidence and authority subjects.

## Recommended Tightening Order

1. Fix C1 with one canonical content-addressed release identity graph and independent edge validation.
2. Bind Story 3.14’s authority record to that exact graph before any external write (H1).
3. Bind all three Story 3.13/3.15 receipts to one canonical subject digest and explicit outcomes (H2).
4. Add the consumer-specific capability/mode/removal subject and separate Consumer-owner authority (H3).
5. Canonicalize provenance values and repair AD-11’s FR36 `Binds` traceability (M1-M2).

## Disposition Summary

| ID | Finding | Severity | Gate status |
| --- | --- | --- | --- |
| C1 | No normative proof graph binds source, packages, workflow, and OCI bytes | Critical | Open - mandatory architecture fix |
| H1 | Story 3.14 release authority is not content-bound to the actual writes | High | Open - mandatory architecture fix |
| H2 | Reviewer roles/receipts/subject identity are asymmetric and under-specified | High | Open - mandatory architecture fix |
| H3 | Consumer-removal capability, mode, scope, and action authority are undefined | High | Open - mandatory architecture fix |
| M1 | Provenance value normalization is not canonical | Medium | Open - fix with C1 |
| M2 | AD-11 omits FR36 from `Binds` while the map assigns it | Medium | Open - autofix |

Gate result: **FAIL.** The negative-disposition and digest-only deployment protections are strong, but the positive corrective-release path is not yet a single mechanically enforceable authority/evidence/acceptance/removal chain.
