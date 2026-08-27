---
title: 'Fix CI run 33105831846'
type: 'bugfix'
created: '2026-08-27'
status: 'done'
review_loop_iteration: 0
baseline_commit: '2ae587024ec7dd7dfaca174bf22aa8d74b7a8dc1'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/docs/ci.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** [CI run 33105831846](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/33105831846) has two independent blocking failures inherited by current `main`: 269 Contracts failures because Story 4.15 detects one changed OQ8-bound fixture, and one Server test failure because its AppHost source marker predates nullable Tenants topology.

**Approach:** Integrity-preservingly re-seal the Story 4.15 source-only closure onto the final fixture commit, with fresh exact-subject reviews, and update the stale Server source-shape assertion to the current guarded Tenants block.

## Boundaries & Constraints

**Always:** Preserve immutable Story 4.14 capture bytes, OQ8 design/profile/behavior, all 24 capability paths, ancestry/current-byte checks, external-authority exclusions, and the hardened fixture. Use `5e8f175b2ced4715f7c6f765386812cc1001dbb4` (tree `96fdfbba56df41b58889bf7f3b532a64d15314bd`) as the new landed source; no bound path changed after it. Bind fresh architecture, security, and test receipts to one final subject before sealing.

**Ask First:** Any OQ8 behavior/design/profile/public-contract change; release, package, registry, deployment, pin, consumer, submodule, or external-repository mutation; inability to obtain all three fresh reviews; committing or pushing.

**Never:** Relax or bypass the validator, exclude the fixture from the 24-path contract, replace only a hash, reuse/fabricate stale receipts, revert Story 4.5 fixture hardening, mutate Story 4.14 evidence, or claim external authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Committed OQ8 closure | HEAD descends from `5e8f175b…`; 24 paths match; three receipts bind the subject | Validator and all OQ8 closure tests pass | Missing, changed, or unreviewed evidence fails closed |
| Later bound-path drift | Any of the 24 capability paths changes after the landed source | Committed validation rejects the exact path | Do not weaken or auto-reseal |
| Tenants topology guard | Nullable Tenants resources are wired inside the guarded block | Test reaches and preserves shared-component assertions | Missing/reshaped block fails with a bounded marker diagnostic |

</frozen-after-approval>

## Code Map

- `tools/validate-oq8-platform-evidence.py:38-56,1199-1294` -- closure/landed pins, capture-to-landed overrides, and exact HEAD byte proof.
- `_bmad-output/implementation-artifacts/evidence/story-4-15/4b0a7b1d*/` -- current sealed identity, subject, reviews, handoff, and manifests; move/rebuild under the new landed SHA without touching `evidence/story-4-14/**`.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs:15,1365` -- closure literals and adversarial integrity contract.
- `_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml:36-55` and four OQ8 public docs -- outer binding and reviewed landed-source wording.
- `tests/Hexalith.EventStore.Server.Tests/DaprComponents/DaprComponentValidationTests.cs:94-106` -- stale Tenants block marker; production `Program.cs:77-113` is read-only evidence.
- `_bmad-output/implementation-artifacts/deferred-work.md:3821-3828` -- close DW-460 only after the Contracts gate passes.

## Tasks & Acceptance

**Execution:**
- [x] Story 4.15 validator, closure directory, Contracts tests, packet, and four public docs -- rebind landed commit/tree and add the fixture capture-to-landed override (`0da109…` → `28a898…`); generate the pre-review subject, obtain three fresh content-bound approvals, then seal manifests and outer digests.
- [x] `DaprComponentValidationTests.cs` -- extract the Tenants wiring from `if (tenants is not null && tenantsApi is not null) {` through the sample boundary while retaining the existing isolation assertions.
- [x] `deferred-work.md` -- record DW-460 resolved with focused and full Contracts evidence.

**Acceptance Criteria:**
- Given the unchanged 24-path set at and after `5e8f175b…`, when committed OQ8 validation and the full Contracts project run, then all 1,763 baseline tests pass and Story 4.14 remains byte-identical.
- Given current nullable Tenants topology, when the focused Server guard and full Server project run, then the guard reaches its semantic assertions and all configured tests pass.
- Given any bound-path, subject, receipt, manifest, or authority mutation, when adversarial closure tests run, then validation still fails closed.

## Spec Change Log

## Design Notes

This follows the proven reseal pattern in `spec-gh-31483075631-fix-ci-cd.md`. A new live-sidecar capture is unnecessary solely for source drift: the immutable capture remains historical evidence, while a declared capture-to-landed override explains the fixture evolution. The reseal commit must itself leave all 24 bound paths untouched.

## Verification

**Commands:**
- `PYTHONDONTWRITEBYTECODE=1 .oq8-python/bin/python tools/validate-oq8-platform-evidence.py --pre-review` -- expected: refreshed identity passes before receipts; final mode still rejects absent approvals.
- `PYTHONDONTWRITEBYTECODE=1 .oq8-python/bin/python tools/validate-oq8-platform-evidence.py` -- expected: final sealed closure passes after three reviews.
- Release/package-mode builds plus direct xUnit v3 `-class Oq8PlatformClosureTests` and `-method Hexalith.EventStore.Server.Tests.DaprComponents.DaprComponentValidationTests.DomainServiceSidecars_DoNotReferenceStateStoreOrPubSubComponents`, followed by full Contracts and Server project lanes -- expected: zero failures, warnings, or skips beyond configured baselines.
- `git merge-base --is-ancestor 5e8f175b2ced4715f7c6f765386812cc1001dbb4 HEAD` and a 24-path diff from that anchor, Story 4.14 tree diff, manifest checksum checks, and `git diff --check` -- expected: ancestry true; no protected drift; all seals and whitespace valid.

## Suggested Review Order

**OQ8 closure reseal**

- Start with the fail-closed landed/current source identity contract.
  [`validate-oq8-platform-evidence.py:1176`](../../tools/validate-oq8-platform-evidence.py#L1176)

- Inspect the exact landed commit, tree, paths, and fixture override.
  [`source-artifact-identity.json:4`](evidence/story-4-15/5e8f175b2ced4715f7c6f765386812cc1001dbb4/source-artifact-identity.json#L4)

- Confirm the reviewed subject binds every closure input and authority boundary.
  [`review-subject.json:60`](evidence/story-4-15/5e8f175b2ced4715f7c6f765386812cc1001dbb4/review-subject.json#L60)

- Verify fresh architecture approval targets the exact new subject.
  [`architecture.json:2`](evidence/story-4-15/5e8f175b2ced4715f7c6f765386812cc1001dbb4/reviews/architecture.json#L2)

- Check the sealed source-only consumer instructions and denied external authorities.
  [`source-only-handoff.json:12`](evidence/story-4-15/5e8f175b2ced4715f7c6f765386812cc1001dbb4/source-only-handoff.json#L12)

- Confirm the outer packet exposes the rebuilt closure digests.
  [`4-8-eventstore-oq8-platform-evidence.yaml:36`](4-8-eventstore-oq8-platform-evidence.yaml#L36)

**Server topology guard**

- Review structural extraction of the nullable Tenants wiring block.
  [`DaprComponentValidationTests.cs:94`](../../tests/Hexalith.EventStore.Server.Tests/DaprComponents/DaprComponentValidationTests.cs#L94)

**Regression and public contract**

- Check adversarial closure tests pin the resealed source and evidence directory.
  [`Oq8PlatformClosureTests.cs:13`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/Oq8PlatformClosureTests.cs#L13)

- Verify public wording records completion without granting operational authority.
  [`architecture-overview.md:278`](../../docs/concepts/architecture-overview.md#L278)

**Review follow-ups and concurrent context**

- Keep closure-assembly commit binding open until separately solved.
  [`deferred-work.md:2502`](deferred-work.md#L2502)

- Review incidental Tenants and sweep findings captured without submodule mutation.
  [`deferred-work.md:4151`](deferred-work.md#L4151)

- Treat the concurrent Tenants gitlink advance as outside this approved fix.
  [`Hexalith.Tenants:1`](../../references/Hexalith.Tenants#L1)
