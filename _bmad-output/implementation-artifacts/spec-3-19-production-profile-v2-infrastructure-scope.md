---
title: '3.19 Represent infrastructure scope in production profile v2'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: '54f83e3b6a6eca3c8cf0caeb734cbcad30e18e41'
route: 'dispatch'
review_loop_iteration: 0
planning_baseline: '54f83e3b6a6eca3c8cf0caeb734cbcad30e18e41'
context:
  - '{project-root}/references/Hexalith.EventStore/AGENTS.md'
  - '{project-root}/docs/implementation/hosted-profile-v1-mapping-assessment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** V1 requires every shared instance to have a Kubernetes namespace and Dapr-style configuration digests. Required node-level containerd, node OS and cluster-scoped CRDs cannot truthfully supply these.

**Approach:** Add a focused owner v2 for actual infrastructure scope and applicable Dapr details. The user selected the mapping-first recommendation on 2026-11-16: adopt v1 if clean, otherwise prepare focused v2. The containerd mapping establishes the latter branch.

## Boundaries & Constraints

**Always:** Work in `references/Hexalith.EventStore`; execution paths below are owner-relative. Represent environment/cluster/node/namespace scope using applicable identifiers. Distinguish software and Dapr API versions. Retain provider pins, loaded identities, sources, owners, evidence and explicit bundle relationships. Require independent complete expected-instance coverage. Preserve mandatory Dapr configuration, actor isolation, token, physical-target and restore checks for applicable kinds.

Preserve separate infrastructure and resolved identities: template and declared invariant provider/topology/tool pins versus complete actual bindings. Actual instance scope, node/namespace/bundle bindings and loaded observations remain resolved-only. Explicitly version v2 preimages without expanding the infrastructure domain; keep v1 bytes/digests and strict version selection unchanged. Reuse owner types/canonicalization where suitable. Structural results leave authorization/qualification unevaluated.

**Never:** Invent namespaces/configuration, make every field nullable, hide omissions outside the resolved profile, infer constituent versions from distribution labels, duplicate the Platform codec, or add automatic fallback/migration services. Select targets, change pins, publish profiles, issue authority, modify approved preparation bytes or complete parent stories. Retain missing build-proof, qualification, codec and ratification refusals.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior |
| --- | --- | --- |
| Node/cluster | Genuine scope/identity without Dapr metadata | Structurally representable |
| Dapr | Namespaced binding and separate API/software versions | Existing mandatory checks retained; omissions refuse |
| Bundle | Constituent pin/identity and parent relationship | Preserve both; dangling/cyclic/self-parent links refuse |
| Coverage | Missing, duplicate or unexpected expected instance | Refuse with safe path/code |
| Pair | Exact management/workload/Rancher tuple | Represent identities; qualification stays unevaluated |
| Version/digest | V1 vectors, v2 input, observation/pin changes | V1 unchanged; explicit v2 decoding; correct domain changes |

</frozen-after-approval>

## Code Map

- `docs/architecture/production-profile-v1.md`, `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfile*.cs` — owner grammar, codec, validator and namespace gap; preserve v1 golden vectors.
- `tools/production-profile.cs`, `tools/publication_authority.py` — genuine structural bridge and fixed authority refusal; bind new execution inputs without claiming complete proof.

## Tasks & Acceptance

**Execution:**
- [x] `docs/architecture/production-profile-v2.md` — specify closed scope/kind fields, applicability, bundle links, complete coverage and both exact digest preimages. Separate declared invariant pins/topology from actual instance scope/bundle bindings and observations.
- [x] `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileV2*.cs` — implement owner model/validator/codec, one type per file; reuse unchanged sections and canonical rules. Preserve strict v1 consumers and reject invalid applicability/coverage/bundles.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Deployment/ProductionProfileV2Tests.cs`, `Deployment/Fixtures/` — cover every matrix row with synthetic controls and independent canonical/digest vectors; keep v1 vectors unchanged.
- [x] `tools/production-profile.cs`, `tools/publication_authority.py`, `docs/architecture/publication-authority-v1.md` — add explicit v2 structural consumption/coverage and required source bindings while preserving every fixed refusal.
- [x] `tools/tests/check_production_profile_bridge.py`, `tools/tests/test_publication_authority.py` — exercise genuine v2 bridge success/refusal and retained v1 behavior without authority claims.
- [x] `.github/workflows/publication-authority.yml` — run both profile test classes in the required fixture job; its current `*ProductionProfileTests` filter excludes `ProductionProfileV2Tests`.
- [x] This spec — record verification/revision; parent 3.19 stays in-progress. Platform adoption and actual inventory follow separately.

**Acceptance Criteria:**
- Given complete synthetic node/CRD instances, when v2 validates them, then no fabricated namespace or Dapr metadata is required.
- Given applicable Dapr requirements, when configuration or isolation is absent, then v2 refuses.
- Given structural success, when authority evaluates today's missing prerequisites, then publication still refuses.

## Implementation Notes

- V2 reuses all unchanged owner sections and the exact owner canonical writer;
  nonserialized validation views share the existing mandatory binding checks.
  No Platform codec or runtime migration/fallback was added.
- `components` is the complete v2 instance inventory. Closed scope/kind rules,
  separately pinned software versions, conditional Dapr details and explicit
  distribution parents preserve real identities without placeholder metadata.
- The separately bound composing-inventory schema explicitly chooses v1 or v2.
  Both owner contracts and all shared/v2 profile sources remain required inputs.
  Structural success leaves authorization and qualification unevaluated; every
  fixed authority refusal remains in place.
- Scoped-name uniqueness includes the closed instance kind so distinct resource
  kinds can truthfully share a name; Dapr component-name uniqueness is retained.
  Controls separately reject same-kind aliases and accept distinct-kind names.
- Parent Story 3.19 remains in-progress. No production inventory, selected pins,
  profile publication, qualification or approved preparation bytes changed.

## Spec Change Log

- 2026-11-16: Implemented the approved focused owner v2 in the working tree based
  on owner HEAD `70bf20dceaafd37229236e11d279a5d6ea434759`. Preserved the original
  planning/baseline revision and all frozen intent. Concurrent committed owner
  changes are retained; implementation has not been committed by this task.
- Verification-only adjustment: the pinned xUnit v4.0.3 runner rejects the
  proposed interior wildcard `*ProductionProfile*Tests` (exit 3: wildcards only
  at beginning/end). Use two supported `-class` filters instead:
  `-class '*ProductionProfileTests' -class '*ProductionProfileV2Tests'`. The
  required fixture workflow uses that exact selection for both classes.

## Review Triage Log

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | medium | patch | Topology counts include only provider-matching rows, so an additional same-cluster Kubernetes row with another provider evades the exactly-one rule. Count the cluster instances before checking their selected provider; apply the same exact-tuple rule to Rancher. |
| B2 | medium | patch | Scope checks require namespace presence but CheckShape treats namespace as arbitrary text. A space/slash-containing namespace reaches successful v2 sealing; validate actual Kubernetes namespace syntax for v2 while retaining v1 behavior. |
| B3 | maybe-false | defer (medium, unverified) | A container-runtime declaration can reference any generic provider with a matching version. The model has no trusted provider-category registry, and qualification remains unevaluated; authoritative kind/provider applicability metadata is needed to determine contradictory generic bindings without inventing a taxonomy. |
| B4 | medium | defer | phase_4_story_5_3.run writes selected logs before assert_stable inventories untracked files; an output directory inside the checkout can invalidate its own source check. This is concurrent committed authentication work, not introduced by focused v2. |
| B5 | medium | defer | KeycloakAuthFixture.SafeShutdownAsync clears the lease/app/builder handles after failed release attempts. A subsequent fixture disposal cannot retry those handles; concurrent authentication code predates this focused implementation. |
| B6 | false | reject | All current lease construction sites pass explicit private Redis endpoints. No current caller reaches the optional shared endpoint; the finding describes a possible future omitted argument rather than an observed unsafe call. |
| B7 | low | defer | The authentication controls observe one event key immediately after rejection and do not independently assert dispatch exclusion/completion. Current authorization rejects before mediator dispatch; strengthening the future-regression evidence belongs to the concurrent authentication work. |
| B8 | medium | patch | The closed applicability switch advertises operator and further Rancher/service/distribution scopes absent from executing positive/refusal cases. Add direct table controls to the existing v2 test class. |
| B9 | medium | patch (group with V2) | Only cluster-scoped distribution containment is exercised. Environment/node/nested distribution controls are absent; the node branch is the same untested root cause as V2. |
| B10 | medium | patch | The existing physical-target alias test uses a Dapr read-model row. Add a non-Dapr alias asserting actor-physical-target-reused so removing infrastructure collision checks cannot leave the suite green. |
| V1 | medium | patch | Pre-verified mutation replaces all three v2 configuration evidence subjects with loaded identity while all 181 cases still pass. Keep configuration digests non-null and remove each actual evidence subject, checking refusal and absent returned digests. |
| V2 | medium | patch (group with B9) | Pre-verified mutation makes node-parent containment always fail while all 181 cases still pass. Add same-node acceptance plus differing-node/cluster refusals. |

The edge-case layer returned no findings. Each finding above is adjudicated
individually before grouping. B9/V2 share the missing node-parent verification
root cause; the other survivors remain distinct. Patches add no public surface
and address demonstrated states or executable verification gaps. Deferred
external findings are preserved separately without changing concurrent code.

## Verification

- Use `run-with-temp` and Debug/source-reference builds in disposable owner/root-declared sibling copies; retain logs outside scratch.
- Build Contracts tests with `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 --disable-build-servers`; run the built assembly with `-class '*ProductionProfileTests' -class '*ProductionProfileV2Tests'`. Require passing v1/v2 cases and unchanged v1 golden bytes/digests.
- Run `python3 -B tools/tests/check_production_profile_bridge.py` and `python3 -B -m unittest discover -s tools/tests -p 'test_publication_authority.py'`; preserve truthful authority refusal. Check workflow syntax with `actionlint`, local links and `git diff --check`. Fixtures grant no inventory acceptance.

### Implementation verification (2026-11-16)

- Disposable owner and root-declared Builds/Commons/Tenants source copies under
  `run-with-temp`: Debug Contracts test build using the required project-reference
  flags and serialized/no-server build succeeded with zero warnings/errors.
- Built Contracts assembly with both explicit class filters: **181/181 passed** (130 v1, 51 v2),
  zero failures/skips. This includes v1 golden and v2 scope/kind/applicability,
  bundles, complete coverage, exact pair, retained Dapr/isolation/token/restore,
  safe diagnostics, canonical size and both digest-domain controls.
- `run-with-temp python3 -B -m unittest discover -s tools/tests -p
  'test_publication_authority.py'`: **25/25 passed**. Fully bound v2 synthetic
  inputs still hit `PROFILE_BUILD_INPUT_PROOF_UNAVAILABLE` before child execution.
- Independent stdlib golden verification: four canonical vectors/four digests
  passed. All three original v1 fixture files remain byte-for-byte identical to
  the frozen baseline, including their digests.
- `actionlint .github/workflows/publication-authority.yml`, changed architecture
  document local links and `git diff --check`: passed.
- `run-with-temp python3 -B tools/tests/check_production_profile_bridge.py`:
  **48/48 controls passed** for explicit v1/v2 genuine structural consumption,
  retained promotion associations/refusals, SDK pinning and fixed authority
  refusals. The final run additionally asserts exact issue codes for canonical
  v2 negative fixtures (Dapr configuration, node scope and bundle references).
- Useful logs are retained outside scratch at
  `/home/administrator/.cache/hexalith-profile-v2-verification/`:
  `contracts.log`, `bridge-final.log`, `authority.log`, `static-checks.log`.
  All disposable verification jobs and their child work have exited. Fixtures
  grant no production inventory acceptance or publication authority.

### Enclosing matrix audit

All six rows are covered by tests in the executed 105-case v2 class and retained
130-case v1 class, with zero skipped or unrun cases:

| Matrix row | Executed coverage |
| --- | --- |
| Node/cluster | `IndependentVectorsBindTheExactVersionedPreimagesAndEveryScope`, infrastructure applicability refusals |
| Dapr | `ApplicableDaprRequirementsRemainMandatory`, exact-pair/token/restore checks |
| Bundle | Independent positive vector and dangling/self/cycle/scope/kind refusal cases |
| Coverage | `ExpectedInventoryMustBeIndependentCompleteNonemptyAndUnique`, genuine v2 bridge omissions |
| Pair | Exact positive tuple and management/workload/Rancher mismatch cases; unevaluated qualification asserted |
| Version/digest | Strict decoder selection, independent v1/v2 vectors, current-binding and invariant-pin changes |

The complete baseline diff also contains concurrently committed authentication
work from `a40642d90bcf11dc866c0e1204d32eb78a7c238e`; it is preserved external
work, not part of this focused implementation. No code was staged or committed
by this run.

### Review patches and combined verification

- B1: exact topology cardinality is checked before the selected provider identity;
  contradictory extra Kubernetes/Rancher rows now refuse.
- B2: actual v2 application/component namespaces require a 1–63-character
  lowercase Kubernetes DNS label. V1 namespace acceptance remains unchanged.
- B8/B9/B10/V1/V2: added complete positive/refusal kind/scope table controls,
  environment/node/nested distribution containment, populated configuration
  evidence omissions, and non-Dapr actor-target alias controls.
- Focused patch verification passed 105/105 v2 cases. The enclosing fresh
  disposable Debug/source-reference build and both class filters passed
  **235/235** (130 v1, 105 v2), zero warnings/errors/failures/skips.
- B3/B4/B5/B7 are recorded in Platform's deferred-work ledger with their exact
  evidence and external/unverified status. B6 is rejected on current callers.
  No concurrent authentication source was changed by this task.

### Final enclosing verification

- Fresh combined profile run: **235/235 passed** (130 v1, 105 v2), no
  failures/skips and no build warnings/errors; `contracts.log`.
- Fresh Python authority run: **25/25 passed**; `authority-reviewed.log`.
- Fresh genuine bridge run: **48/48 passed**, including both explicit versions
  and retained authority refusals; `bridge-reviewed.log`.
- Independent canonical vectors/digests, workflow syntax, unchanged v1 golden
  bytes, frozen intent and approved preparation identities, local links and
  whitespace checks pass. All disposable child work has finished.
- Three review layers completed. All accepted focused defects and verification
  gaps were patched and reverified; four external/unverified follow-ups are
  retained in Platform's ledger. Parent 3.19 remains in-progress; Platform
  adoption, qualified inventory and publication authority remain outstanding.
