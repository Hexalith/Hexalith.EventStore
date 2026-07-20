---
title: 'Restore EventStore CI release-governance coherence'
type: 'bugfix'
created: '2026-07-20'
status: 'done'
review_loop_iteration: 0
baseline_commit: 'afcc167e0c539b09ecad978a58da2f756123f34e'
context:
  - '{project-root}/docs/ci.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CI run `29720431798` exposed a weakened `commitlint.config.mjs`; PR #312 restored that policy but accidentally merged an unrelated `references/Hexalith.Builds` gitlink advance. Replacement run `29720659734` now fails because the checked-out Builds revision `ed7cea8e...` disagrees with the immutable release-authority revision `ffa16628...` pinned in `.github/workflows/release.yml`.

**Approach:** Preserve the already-correct strict commitlint configuration and restore only the Builds gitlink to the exact release workflow pin. Validate the release-governance contract and the complete Contracts test project without changing the release authority or weakening its guardrail.

## Boundaries & Constraints

**Always:** Keep `commitlint.config.mjs` at its strict three-line `@commitlint/config-conventional` policy; make the Builds worktree and superproject gitlink resolve to `ffa1662829b28d1d90554980c87f23bd9d4e25e7`; preserve the current Tenants gitlink and all unrelated files; run Release validation with warnings as errors; use a Conventional Commit message and PR title if delivery is later requested.

**Ask First:** Any proposal to adopt Builds `ed7cea8e...`, change either release workflow SHA field, edit files inside a submodule, change the Tenants pointer, alter tests or commit policy, commit/push/open a PR, or mutate GitHub state.

**Never:** Weaken or delete the governance assertion, rewrite the already-merged `afcc167e...` history, recursively initialize/update submodules, treat the immutable historical run as repairable, or bundle release authorization/publication work into this fix.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Policy regression from run `29720431798` | Active config contains relaxed rules | Active config remains exact strict three-line policy | Any policy drift blocks completion |
| Builds authority mismatch | Builds checkout/gitlink is `ed7cea8e...`; release caller pins `ffa16628...` | Checkout/gitlink and both release inputs resolve to `ffa16628...` | Do not update authority pins merely to satisfy the test |
| Unrelated dependency state | Tenants is `f03b474d...` and other merged files exist | No unrelated diff is introduced | Stop if changing Builds would modify submodule contents or another pointer |
| Historical invalid squash title | Push run `29720659809` failed on `afcc167e...` | History remains unchanged; any future delivery metadata is Conventional Commit compliant | Report the immutable old-run failure separately |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Builds` -- root-declared gitlink that must match the immutable release execution revision.
- `.github/workflows/release.yml` -- unchanged source of truth pinning `domain-release.yml` and `builds-execution-sha` to the same SHA.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs` -- unchanged regression test comparing the release pin with the checked-out Builds HEAD.
- `commitlint.config.mjs` -- unchanged strict policy that resolves the originally reported failure.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Builds` -- move only the submodule checkout/gitlink from `ed7cea8e...` back to `ffa16628...` -- restore release-authority coherence lost in PR #312.
- [x] Contracts validation -- build the focused project and run the governance and commit-policy test classes from the freshly built xUnit v3 assembly -- prove both consecutive CI failures are closed without contract weakening.
- [x] Repository diff -- verify only the Builds gitlink and this workflow artifact changed, with Tenants and submodule contents untouched -- prevent another mixed-scope repair.

**Acceptance Criteria:**
- Given the strict commit policy on current `main`, when commit-policy tests run, then the exact config and hook contract pass unchanged.
- Given both release caller inputs pin `ffa16628...`, when container-publishing governance runs, then the checked-out Builds HEAD matches that SHA and the authority mapping passes.
- Given unrelated pointers and files at baseline `afcc167e...`, when the fix is inspected, then no Tenants, workflow, test, or submodule-content diff exists.

## Spec Change Log

## Design Notes

The workflow pin is security-sensitive release authority, while the Builds advance was an accidental passenger in a commitlint-only PR. Restoring the gitlink is therefore narrower and safer than authorizing new release code by changing both workflow fields. The failed run tied to the invalid squash title is immutable; prevention belongs to shared Hexalith.Builds PR-title governance and is not duplicated locally.

## Verification

**Commands:**
- `git -C references/Hexalith.Builds rev-parse HEAD` plus release-pin extraction -- expected: all values equal `ffa1662829b28d1d90554980c87f23bd9d4e25e7`.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -warnaserror -m:1` -- expected: zero errors and warnings.
- Direct xUnit v3 execution for `CommitMessagePolicyTests` and `ContainerPublishingGovernanceTests` -- expected: both classes pass from the fresh Release assembly.
- `git status --short --branch && git diff --submodule=log && git diff --check` -- expected: only the Builds gitlink and this spec differ; no whitespace errors.

**Results (2026-07-20):**
- Focused Release build passed with zero warnings and errors.
- Direct xUnit v3 execution passed `CommitMessagePolicyTests` 14/14 and `ContainerPublishingGovernanceTests` 7/7.
- The strict commitlint config remained unchanged; the Builds checkout and both release inputs resolve to `ffa1662829b28d1d90554980c87f23bd9d4e25e7`.
- The Builds submodule is clean, the Tenants checkout/gitlink remains `f03b474d836a3465e311c48e90c75ae1e755ef45`, and `git diff --check` passed.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` changed concurrently during validation and was preserved as user-owned work.

## Suggested Review Order

**Release identity coherence**

- Start with the approved pointer-only repair and its frozen safety boundaries.
  [`spec-gh-29720431798-fix-ci-cd.md:16`](spec-gh-29720431798-fix-ci-cd.md#L16)

- Confirm the immutable reusable-workflow SHA remains the release authority source.
  [`release.yml:31`](../../.github/workflows/release.yml#L31)

**Regression proof**

- Verify CI binds the checked-out Builds gitlink to the authorized workflow SHA.
  [`ContainerPublishingGovernanceTests.cs:115`](../../tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs#L115)

- Confirm the originally reported commit-policy regression remains strictly closed.
  [`commitlint.config.mjs:2`](../../commitlint.config.mjs#L2)

**Review follow-up**

- Inspect the unrelated Story 4.8 baseline typo recorded for later repair.
  [`deferred-work.md:406`](deferred-work.md#L406)
