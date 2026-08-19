# Reviewer Gate - 2026-08-16 Final Adversarial Divergence Re-Review

- **Artifact:** `_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/ARCHITECTURE-SPINE.md`
- **Prior re-review:** `reviews/review-update-2026-08-16-adversarial-divergence-rereview.md` (RH2-RH3 high)
- **Lens:** reconstruct literal-compliant substitutions at the Story 3.15 receipt and consumer-removal authorization boundaries after final correction.
- **Deterministic pre-pass:** `lint_spine.py` returned `ok: true`, zero findings.
- **Mutation posture:** review only; the spine was not edited.

## Verdict

**PASS - RH2 and RH3 are closed; no critical or high finding remains.** Story 3.15 receipts now transitively content-address the exact validated release identity and all retained evidence, and consumer removal now requires an authenticated Consumer-owner receipt over immutable packet, catalog, mode, consumer-commit, and removal subjects. The original permissive constructions cannot satisfy the corrected letter.

## RH2 Closure - Story 3.15 Review Subject And Receipts

### Corrected rule

AD-22 now requires the Story 3.15 canonical review subject to contain:

- the exact canonical `ReleaseIdentity` SHA-256 digest;
- the selected OCI index digest;
- the one-use release-authority digest;
- the explicit parity outcome; and
- the SHA-256 digest of every retained evidence object.

The subject digest is computed over exact canonical bytes. Every authenticated EventStore-owner, Release-owner, and Test-Architect receipt must equal the packet's recomputed subject digest and record identity, role, outcome, and timestamp. Missing references or any transitive evidence change invalidates every receipt (`ARCHITECTURE-SPINE.md:352-360`).

### Counterexample attempted and rejected

**Attempt:** validate a conforming `ReleaseIdentity` independently, then have all three reviewers sign an unchanged generic subject containing only `release_tag` and `positive-parity`. Later pair those receipts with a different conforming identity or evidence packet.

**Why it fails literally:** the generic subject omits the required `ReleaseIdentity`, OCI index, authority, and evidence digests. Adding or changing any of them changes the exact canonical subject bytes and recomputed digest, invalidating all existing receipts. A receipt for release `A` cannot be reused for release `B`, and a technically valid identity without matching triad receipts cannot close Story 3.15.

### Result

**RH2 closed.** Technical validation and human acceptance now select the same immutable lineage and outcome. No subject-floating or receipt-reuse construction survives.

## RH3 Closure - Consumer Catalog And Removal Authorization

### Corrected rule

The unchanged AD-22 parity packet now binds:

- the capability catalog's canonical owner, path, schema, version, and SHA-256 content digest;
- the exact consumer repository and commit;
- the exact removal-subject digest and scope; and
- the source/package/deployed applicable-mode matrix and its SHA-256 digest (`:325`).

Every applicable mode used by the consumer must pass against that same packet. Deletion additionally requires an authenticated Consumer-owner receipt containing the consumer repository/commit, packet-subject digest, catalog digest, mode-matrix digest, removal-subject digest, explicit `consumer-removal-authorized` outcome, timestamp, and validity. Any bound change invalidates it; Booleans, free-form approval, version labels, Story 3.15 completion, and EventStore-side receipts confer no cross-repository authority (`:331`).

### Counterexamples attempted and rejected

**Catalog mutation attempt:** approve catalog version `capabilities-v1`, then shorten its required-capability set without changing the version. This fails because the catalog's canonical identity includes its SHA-256 content digest; the packet and Consumer-owner receipt no longer match.

**Removal substitution attempt:** approve a named removal scope, then change the files/diff inside that scope. This fails because the exact removal-subject digest changes and invalidates the receipt.

**Mode omission attempt:** mark package or deployed mode inapplicable even though the consumer uses it. This violates the explicit rule that every mode the consumer uses is applicable and must pass against the same packet.

**Approval substitution attempt:** use a packet Boolean, free-form email, Story 3.15 completion, or EventStore-side triad receipts as removal authority. Each substitute is expressly non-authorizing; only the authenticated, unexpired Consumer-owner receipt with the enumerated bound fields permits deletion.

### Result

**RH3 closed.** The capability baseline, consumer identity, applicable evidence modes, and destructive removal action are one content-addressed authorization subject. No consumer can retain literal compliance while changing one of those facts after approval.

## Regression And Contradiction Sweep

- C1 remains closed: the canonical `ReleaseIdentity` fixes the source/package/workflow/OCI graph, and Story 3.15 independently derives every edge from trusted workflow facts and retained raw bytes (`:161-163`).
- H1 remains closed: Story 3.14's durable one-use authority enumerates its subject, every external write must match, and missing/expired/replayed/mismatched authority fails closed (`:347-351`).
- Story 3.13 remains a content-bound rejected/non-authorizing disposition and cannot substitute for Story 3.15 (`:337-362`).
- Story 3.14 publication and Story 3.15 independent validation remain separate; neither confers consumer-removal authority.
- Mutable tags remain non-authorizing; the validated OCI index digest remains the deployment identity (`:163,329`).
- The Story 2.12 exception remains limited to its named consumer/story and does not weaken deployed-mode or later-consumer rules (`:335`).
- No new contradiction among AD-11, AD-12, AD-22, the runtime-topology convention, or the capability map was created by the final RH2/RH3 amendments.

## Non-Blocking Tail

AD-11's local `Binds` list still omits FR36 while the capability map names AD-11 for FR36 (`:153,582,585`). This is a medium traceability cleanup, not a surviving literal-compliance path through the corrected release/acceptance/removal rules. The exact canonical serialization schemas and authentication providers should be pinned by the implementing stories, but the spine now fixes the identity fields, digest relationships, outcomes, and fail-closed invalidation semantics needed to prevent incompatible choices.

## Disposition Summary

| Finding | Prior severity | Final status |
| --- | --- | --- |
| C1 - source/package/workflow/image identity graph | Critical | Closed |
| H1 - Story 3.14 one-use release authority | High | Closed |
| RH2 - Story 3.15 subject/receipt transitive binding | High | Closed |
| RH3 - consumer catalog/removal receipt binding | High | Closed |

Gate result: **PASS.** No critical or high adversarial-divergence finding remains.
