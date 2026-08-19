# Reviewer Gate - 2026-08-16 Adversarial Divergence Re-Review

- **Artifact:** `_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/ARCHITECTURE-SPINE.md`
- **Prior review:** `reviews/review-update-2026-08-16-adversarial-divergence.md` (C1 critical; H1-H3 high)
- **Lens:** reconstruct literal-compliant source/package/workflow/image, release-authority, reviewer-acceptance, and consumer-removal units after correction.
- **Deterministic pre-pass:** `lint_spine.py` returned `ok: true`, zero findings.
- **Mutation posture:** review only; the spine was not edited.

## Verdict

**FAIL - no critical finding remains, but two high-severity content-binding holes survive.** C1 and H1 are closed: the spine now requires a canonical `ReleaseIdentity`, independent derivation of every identity edge, and a one-use pre-publication authority whose exact subject must match every write. H2 and H3 are materially improved but not fully closed: the three review receipts are not required to bind the validated `ReleaseIdentity` and retained evidence digests, while the consumer-owner approval and capability-catalog reference are not authenticated/content-addressed. Those omissions still permit technically valid evidence to be paired with approval of a different or mutable subject.

## Closure Table

| Prior ID | Result | Re-review conclusion |
| --- | --- | --- |
| C1 - exact lineage lacks a normative graph | **Closed** | AD-11 now defines one canonical `ReleaseIdentity` with source, workflow, Builds, authority, package, OCI, and smoke identities and requires Story 3.15 to independently derive every edge from trusted workflow facts and retained raw bytes (`ARCHITECTURE-SPINE.md:161-163`). |
| H1 - Story 3.14 authority is not subject-bound | **Closed** | AD-22 now requires a durable one-use authority binding repository, version/tag, source, registry, inventory, platforms, publisher revisions, owner, rationale, timestamp, and validity window; every attempted write must match exactly and missing/expired/replayed/mismatched authority fails closed (`:347-351`). |
| H2 - reviewer receipts do not converge | **Partially closed; one high residual** | Roles, authentication, digest algorithm, outcome, timestamp, and invalidation are now fixed (`:352-356`), but the subject's mandatory contents and its link to the validated `ReleaseIdentity` are not. |
| H3 - consumer-removal scope/mode/authority is open | **Partially closed; one high residual** | Consumer repository/commit, scope, mode matrix, conjunctive modes, and separate Consumer-owner approval are now required (`:325-331`), but the catalog and Consumer-owner approval lack immutable content/identity receipts. |

## Closed Constructions

### C1 - label-and-packet lineage assembly no longer complies

The prior permissive unit validated package hashes and OCI digests separately, trusted labels, then asserted they belonged to one source. It now violates AD-11 directly: labels, hand-authored mappings, and earlier pass flags are not identity evidence, and Story 3.15 must independently derive every edge from trusted workflow facts and retained raw bytes (`:161`). The record fixes the workflow run/attempt and revision, Builds execution SHA, package manifest and package SHA-256 digests, complete OCI chain, authority digest, and smoke digest. A package/image splice cannot pass without falsifying a required independently derived edge.

No replacement critical counterexample survived. The record's serialization/location can be fixed by the implementing story without letting two accepted identities coexist: one candidate is permitted one canonical record, and the independent verifier must derive its fields rather than trust its encoding.

### H1 - broad or stale release authorization no longer complies

The prior permissive unit used “publish the next corrective release” as durable authority and selected repository/version/source afterward. That unit now violates the enumerated authority subject and exact-write equality at `:347-351`. An authority cannot be reused, outlive its validity window, omit a listed field, or authorize a different write. The authority digest is also a required `ReleaseIdentity` node (`:161`), so Story 3.15 must derive the same authority relationship independently.

## Remaining High Findings

### RH2 - The receipts bind one digest, but the review subject is not required to bind the validated release identity and evidence graph

**Evidence:** AD-22 requires three authenticated role receipts carrying one canonical SHA-256 review-subject digest, outcome, and timestamp; subject or “referenced-evidence” change invalidates them (`:352-356`). It never states that the review subject must contain or content-address the canonical `ReleaseIdentity`, selected OCI index digest, release-authority digest, validation outcome, and retained evidence digests. “Referenced-evidence” protection applies only to evidence the subject actually references.

**Surviving literal-compliant pair:**

- Unit A hashes a canonical subject that recursively includes the validated `ReleaseIdentity` digest, selected positive OCI identity, exact outcome, and every retained evidence digest. All three reviewers accept those exact facts.
- Unit B hashes a stable subject containing only the release tag and `positive-parity` outcome. Story 3.15 separately validates a conforming `ReleaseIdentity`, and all three authenticated reviewers sign the same unchanged subject digest with the required fields. The subject does not content-address that identity, so substituting a later independently valid candidate or evidence record does not change the signed subject and no “referenced-evidence” changed.

Both units satisfy every enumerated receipt field. Unit A rejects Unit B because the reviewers did not accept the identity the packet selects; Unit B declares Story 3.15 complete.

**Impact:** correct technical validation can be paired with human acceptance of a different, incomplete, or generic subject. The three-receipt gate no longer proves unchanged-subject acceptance of the selected deployment identity.

**Disposition: mandatory architecture fix.** At `:352-356`, require the canonical review subject to content-address the exact `ReleaseIdentity` record, selected index digest, explicit disposition/parity outcome, release-authority digest, and every retained evidence object used by the decision. Define its SHA-256 over exact retained canonical bytes. A receipt is valid only when its subject digest equals the packet's recomputed subject digest; missing references or any transitive identity/evidence change invalidates all receipts.

### RH3 - Consumer removal names an owner and catalog version, but neither approval nor catalog content is immutable evidence

**Evidence:** AD-22 binds the packet to an “authoritative capability-catalog version,” consumer repository/commit, exact removal subject/scope, and a mode matrix (`:325`), and requires the Consumer owner to approve the unchanged packet and exact removal subject (`:331`). It does not name the catalog authority/path or require a catalog content digest; a version label can retain its spelling while its required-capability set changes. It also does not require an authenticated Consumer-owner receipt binding the packet digest, removal-subject digest, catalog digest, and consumer commit. This is weaker than the explicit authenticated receipt contract granted to EventStore/Release/Test reviewers at `:352-356`.

**Surviving literal-compliant pair:**

- Unit A binds an architecture-owned catalog path/version/SHA-256, hashes the exact removal diff, and records an authenticated Consumer-owner receipt over packet, catalog, consumer commit, mode matrix, and removal-subject digests.
- Unit B records `capabilities-v1` as its authoritative catalog version and receives free-form Consumer-owner approval of the packet and named removal scope. The catalog contents are later shortened without changing `capabilities-v1`, or the removal subject changes within the same named scope. The packet fields and approval wording remain unchanged; all modes still marked applicable pass against the now-shorter catalog.

Both units carry one authoritative catalog version, an unchanged packet, an exact named scope, and Consumer-owner approval. Unit A rejects Unit B's deletion because neither the required set nor the approved deletion bytes are content-bound.

**Impact:** a consumer can remove infrastructure after approval against a capability set or removal diff the owner did not review, despite exact runtime/package parity.

**Disposition: mandatory architecture fix.** At `:325-331`, name the capability-catalog owner and canonical path/schema and bind its SHA-256 content digest, not only its version. Require an authenticated Consumer-owner receipt carrying consumer repository/commit, packet review-subject digest, catalog digest, applicable-mode matrix digest, exact removal-subject digest, outcome `consumer-removal-authorized`, and timestamp/validity. Any packet, catalog, mode, commit, or removal change invalidates the receipt; a Boolean, free-form approval, version label, or EventStore-side receipt is insufficient.

## Medium Tail And Contradiction Sweep

### M1 - AD-11 still omits FR36 from its own `Binds` field

AD-11's `Binds` list remains FR10, FR21-FR22, FR25, NFR9-NFR11, and NFR16-NFR17 (`:153`), while the capability map makes AD-11 governing for FR36 source/package and deployed-runtime closure (`:580,583`). A planner driven by AD-local `Binds` can still omit the new `ReleaseIdentity` rule from an FR36 slice. Add FR36 to AD-11's `Binds` list.

### M2 - “Trusted workflow facts” is enforceable only if the implementing verifier fixes its trust source

AD-11 correctly rejects labels and hand-authored mappings as identity evidence (`:161`), but does not name the exact API/attestation/archive from which workflow run/attempt and revisions become trusted. This is not critical/high because the rule requires independent derivation and a unit that simply trusts packet fields violates it. The Story 3.14/3.15 contract should nevertheless pin the provider and retained fields so independent verifiers do not disagree operationally.

### Contradiction results

- No stale positive `v3.94.1` statement survives; its exact failed subject and facts are now explicit (`:337-345`).
- Negative Story 3.13 completion remains distinct from positive Story 3.15 parity (`:347-358`).
- Story 3.14 publication and Story 3.15 validation remain separate and non-circular.
- Tags remain non-authorizing; the recorded validated OCI index digest remains the deployment identity (`:163,329`).
- Multiple consumer modes are now explicitly conjunctive (`:331`); no source/package/deployed-mode bypass survived.
- The Story 2.12 scoped exception remains isolated and does not weaken the new corrective-release path (`:335`).
- The only open traceability contradiction is M1. RH2 and RH3 are under-specification gaps, not contradictory commands.

## Disposition Summary

| ID | Finding | Severity | Status |
| --- | --- | --- | --- |
| C1 | Canonical release identity and independent derivation | Critical | Closed |
| H1 | One-use content-bound Story 3.14 authority | High | Closed |
| RH2 | Review subject does not content-address validated identity/evidence | High | Open - mandatory fix |
| RH3 | Consumer catalog and approval are not authenticated/content-addressed | High | Open - mandatory fix |
| M1 | AD-11 `Binds` omits FR36 | Medium | Open - autofix |
| M2 | Trusted workflow-fact provider is not pinned | Medium | Open - implementation-contract hardening |

Gate result: **FAIL.** The release itself is now exact and independently provable, but the human approval subjects at the Story 3.15 and consumer-removal boundaries can still float away from the exact evidence they are meant to authorize.
