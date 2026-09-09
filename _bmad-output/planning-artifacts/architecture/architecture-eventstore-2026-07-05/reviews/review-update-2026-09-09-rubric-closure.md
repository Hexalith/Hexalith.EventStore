# Architecture Spine Rubric Closure — 2026-09-09

**Verdict: PASS.** All five high, four medium, and two low findings from the rubric-walker report are closed. Deterministic lint still passes with zero findings. No critical or high regression was introduced.

## Closure evidence

| Prior finding | Result | Evidence |
| --- | --- | --- |
| H1 — release-before-proof cycle | Closed | AD-26 now permits only a separately authorized, non-authorizing candidate publication and prohibits production promotion, traffic, migration, readiness claims, and approved production identity until validation passes (`ARCHITECTURE-SPINE.md:241-247`). The Deferred preamble preserves the AD-11 candidate/publication distinction (`:409-416`). |
| H2 — production profile lacked implementable identity | Closed | The Platform deployment owner now owns canonical `deploy/dapr/production-profile.yaml`; its canonical-byte digest binds the exact DAPR runtime/CLI compatibility, Kubernetes mode, providers, app IDs, policy/configuration digests, restore posture, and evidence (`:247`). The missing artifact/runtime pin remains explicitly fail-closed with owner and trigger (`:416`). |
| H3 — AD-22 approval was not enforceable | Closed | AD-22 now binds a content-bound packet, applicable-mode matrix, exact EventStore and consumer identities, removal-subject digest, authenticated immutable Consumer-owner receipt, explicit outcome, validity, and invalidation behavior; it rejects boolean/free-form/self-declared approval (`:207-211`). |
| H4 — AD-24 lacked one secret-contract authority | Closed | The Platform deployment owner is now the sole composer of the singleton component, per-app configurations, and canonical value-free contract; inventories, grants/policies, lifecycle/cache/rotation semantics, acknowledgement, and readiness failure derive from that contract (`:219-225`). |
| H5 — AD-25 expiry/migration contracts were non-testable | Closed | AD-25 restores the exact tombstone allowlist, atomic replacement and indistinguishable expired result (`:235`), plus source/target authority order, durable acknowledgement, redirect, directory/inventory flip, idempotency, retention, and mixed-version failure (`:237-239`). |
| M1 — AD-28 title overclaimed caller identity | Closed | The title now names app-channel authentication, and the rule explicitly says the token alone does not authenticate a claimed caller while catalog/ACL authorization remains required (`:255-259`). |
| M2 — AD-33 lacked authority/precedence | Closed | Contracts owns schema/codec, Platform deployment owns the signed instance, exact routes precede catalog-declared fallbacks, production runtime overrides are forbidden, retained canonical bytes are hashed, and prepare/ready/commit activation binds all consumers to one root digest (`:285-291`). |
| M3 — diagram routed queries through admission | Closed | The diagram splits command admission, projection-backed reads, and handler queries and returns AD-14 metadata through the edge (`:53-69`). |
| M4 — load-bearing source provenance pruned | Closed | The frontmatter restores the July/August architecture, release, OpenBao, OQ8, and reconciliation proposals (`:14-30`). |
| L1 — uncited DAPR currentness | Closed | The official DAPR v1.18.3 release is now a source (`:39`), supporting the Stack statement (`:320`). |
| L2 — payload package boundary ambiguous | Closed | The Structural Seed names both approved future package boundaries and labels them nonexistent/non-authorizing until the implementation and atomic release gates pass (`:330-356`); the names match the approved payload-protection specification. |

## Regression check

### M-R1 — `altitude: platform` is outside the BMad architecture taxonomy

- **Severity:** Medium
- **Location:** `ARCHITECTURE-SPINE.md:5`
- **Evidence:** The architecture skill defines only `initiative`, `feature`, and `epic`; altitude represents the planning layer augmented, not the product's technical category. This spine augments a PRD and keeps epics coherent, which maps to `feature`.
- **Impact:** Downstream BMad routing or validation can treat the metadata as unknown even though the document content is sound.
- **Fix:** Restore `altitude: feature`. This is a metadata correction and does not reopen any architecture decision.

No other regression was found. The expanded AD-8/AD-9/AD-10, tenant/system boundary, MVP-versus-post-MVP erasure split, Operations sink contract, catalog activation protocol, current topology, and production deferrals strengthen rather than weaken the reviewed spine.

## Gate decision

There is no critical or high blocker to finalization. Correct M-R1 before setting `status: final`, then retain the successful deterministic lint result.
