---
title: '3.19 Represent infrastructure scope in production profile v2'
type: 'feature'
created: '2026-11-16'
status: 'in-progress'
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
- [ ] `docs/architecture/production-profile-v2.md` — specify closed scope/kind fields, applicability, bundle links, complete coverage and both exact digest preimages. Separate declared invariant pins/topology from actual instance scope/bundle bindings and observations.
- [ ] `src/Hexalith.EventStore.Contracts/Deployment/ProductionProfileV2*.cs` — implement owner model/validator/codec, one type per file; reuse unchanged sections and canonical rules. Preserve strict v1 consumers and reject invalid applicability/coverage/bundles.
- [ ] `tests/Hexalith.EventStore.Contracts.Tests/Deployment/ProductionProfileV2Tests.cs`, `Deployment/Fixtures/` — cover every matrix row with synthetic controls and independent canonical/digest vectors; keep v1 vectors unchanged.
- [ ] `tools/production-profile.cs`, `tools/publication_authority.py`, `docs/architecture/publication-authority-v1.md` — add explicit v2 structural consumption/coverage and required source bindings while preserving every fixed refusal.
- [ ] `tools/tests/check_production_profile_bridge.py`, `tools/tests/test_publication_authority.py` — exercise genuine v2 bridge success/refusal and retained v1 behavior without authority claims.
- [ ] `.github/workflows/publication-authority.yml` — run both profile test classes in the required fixture job; its current `*ProductionProfileTests` filter excludes `ProductionProfileV2Tests`.
- [ ] This spec — record verification/revision; parent 3.19 stays in-progress. Platform adoption and actual inventory follow separately.

**Acceptance Criteria:**
- Given complete synthetic node/CRD instances, when v2 validates them, then no fabricated namespace or Dapr metadata is required.
- Given applicable Dapr requirements, when configuration or isolation is absent, then v2 refuses.
- Given structural success, when authority evaluates today's missing prerequisites, then publication still refuses.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

- Use `run-with-temp` and Debug/source-reference builds in disposable owner/root-declared sibling copies; retain logs outside scratch.
- Build Contracts tests with `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 --disable-build-servers`; run the built assembly with `-class '*ProductionProfile*Tests'`. Require passing v1/v2 cases and unchanged v1 golden bytes/digests.
- Run `python3 -B tools/tests/check_production_profile_bridge.py` and `python3 -B -m unittest discover -s tools/tests -p 'test_publication_authority.py'`; preserve truthful authority refusal. Check workflow syntax with `actionlint`, local links and `git diff --check`. Fixtures grant no inventory acceptance.
