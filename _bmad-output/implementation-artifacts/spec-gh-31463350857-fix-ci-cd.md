---
title: 'Fix shallow checkout breaking OQ8 evidence validation'
type: 'bugfix'
created: '2026-08-11'
status: 'done'
baseline_commit: 'cf4db341c9666fb6113c6c121fc60af9b4fa5de0'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/docs/ci.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** [Integration Tests run 31463350857](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/31463350857) passes the live OQ8 test, all 33 support cases, and fresh-capture validation, then fails committed evidence validation because the depth-one checkout does not contain pinned landed source commit `e5fef514e1fbbbc52c5b64dfe6e3de18410d49ec`.

**Approach:** Give the Integration Tests checkout complete Git history, and extend the existing live-sidecar workflow guardrail so future shallow-checkout regressions fail deterministically before the expensive live lane runs.

## Boundaries & Constraints

**Always:** Keep `submodules: false` and `persist-credentials: false`; preserve the validator's tree, ancestry, and historical-blob proofs; use an unbounded full-history checkout because the pinned commit recedes as `main` advances; preserve unrelated dirty artifacts.

**Ask First:** Any change to OQ8 evidence identity, validator semantics, sealed Story 4.15 artifacts, checkout credentials/submodule policy, or workflow lane/release coupling.

**Never:** Use a fixed positive fetch depth, fetch history inside the validator, weaken or skip committed evidence validation, reseal evidence for this workflow-only correction, or modify `Oq8PlatformClosureTests.cs`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Current Integration checkout | `fetch-depth: 0` with pinned source five commits behind HEAD | `rev-parse`, ancestry, and all historical path proofs can execute | Validator remains fail-closed on real identity drift |
| Shallow regression | Checkout omits full history or sets a positive depth | Packaging guardrail fails before live-sidecar execution | Test identifies the checkout-history contract |
| Future `main` growth | Pinned landed source moves farther behind HEAD | Complete history still contains the source and connected ancestry | No depth maintenance or silent expiry |

</frozen-after-approval>

## Code Map

- `.github/workflows/integration.yml:33-38,117-122` -- checkout currently defaults to depth one; the later no-argument validator invocation is the failing committed-evidence path.
- `tools/validate-oq8-platform-evidence.py:38-40,1183-1187,1239-1249` -- read-only contract: pin the landed commit/tree, prove ancestry, and read 26 historical blobs.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:620-693,1425-1467` -- existing live-sidecar workflow fact, forbidden-mutation theory, and assertion helper; add the full-history invariant here.
- `_bmad-output/implementation-artifacts/evidence/story-4-15/e5fef514e1fbbbc52c5b64dfe6e3de18410d49ec/source-artifact-identity.json:42,72-78` -- read-only proof that ancestry is mandatory and `integration.yml` is an evolved/unbound path.
- `.github/workflows/{ci,advisory-tests}.yml:73,35` -- repository precedent for `fetch-depth: 0`.
- `docs/ci.md:50-72` -- Integration/OQ8 lane contract; document why full history is required.

## Tasks & Acceptance

**Execution:**
- [x] `.github/workflows/integration.yml` -- add `fetch-depth: 0` to the existing checkout inputs without changing credential or submodule policy.
- [x] `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- require exactly one full-history setting and add a shallow-checkout mutation that proves the guardrail rejects regression.
- [x] `docs/ci.md` -- record that committed OQ8 identity validation needs full connected history, not a maintained positive depth.

**Acceptance Criteria:**
- Given the Integration checkout at any descendant of the landed OQ8 source, when committed evidence validation runs, then Git resolves the pinned tree, proves ancestry, reads pinned blobs, and validation passes.
- Given a workflow mutation to depth one, when the live-sidecar packaging guardrail runs, then it fails while the current full-history workflow passes.
- Given the final diff, when inspected, then the validator, OQ8 closure tests, evidence artifacts, submodule policy, credentials, and release coupling are unchanged.

## Spec Change Log

## Design Notes

Actions checkout fetches one commit by default. `fetch-depth: 0` is the durable ownership boundary: the workflow supplies repository history, while the offline validator continues to prove immutable evidence without network behavior or weakened checks. The workflow is explicitly classified as closure-evolved, so this edit does not invalidate the sealed evidence packet.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -method Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests.Live_sidecar_workflow_targets_live_project_outside_release_gate -method Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests.Live_sidecar_workflow_guardrail_rejects_forbidden_mutations -noColor` -- expected: current workflow and all negative mutations pass.
- `git rev-parse 'e5fef514e1fbbbc52c5b64dfe6e3de18410d49ec^{tree}' && git merge-base --is-ancestor e5fef514e1fbbbc52c5b64dfe6e3de18410d49ec HEAD && python3 tools/validate-oq8-platform-evidence.py` -- expected: tree `e4bb19e5305bbde23563245976b97eb0aaf3c931`, connected ancestry, and committed evidence success.
- `git diff --check` -- expected: no whitespace errors; unrelated dirty artifacts remain preserved.

**Manual checks:**
- Rerun Integration Tests after the change lands; expected: the OQ8 capture/validation step and artifact upload succeed.

## Suggested Review Order

**Checkout history contract**

- Supply the complete history required by the offline OQ8 identity proof.
  [`integration.yml:36`](../../.github/workflows/integration.yml#L36)

- Explain why a fixed positive depth silently expires as `main` advances.
  [`ci.md:69`](../../docs/ci.md#L69)

**Regression guardrails**

- Bind full-history configuration to the pinned checkout action's `with` mapping.
  [`ReleasePackageManifestTests.cs:1478`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs#L1478)

- Reject shallow, globally misplaced, and checkout-environment lookalike settings.
  [`ReleasePackageManifestTests.cs:631`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs#L631)
