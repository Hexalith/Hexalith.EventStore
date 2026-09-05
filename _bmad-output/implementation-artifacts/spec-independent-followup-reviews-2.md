---
title: 'Independent Follow-up Reviews'
type: 'bugfix'
created: '2026-09-05'
baseline_revision: '08f90f4bc143b657c712433a179667c88875aecf'
baseline_commit: '08f90f4bc143b657c712433a179667c88875aecf'
status: 'blocked'
review_loop_iteration: 0
followup_review_recommended: false
context: []
warnings: ['multiple-goals', 'oversized']
deferred:
  - summary: >-
      Reject nuspec metadata containing more than one dependencies element instead of inspecting only the first.
    evidence: |-
      tools/release_package_contract.py:303 resolves metadata dependencies with ElementTree.find. A malformed archive can append a second dependencies element that neither validator inspects. Current dotnet pack output emits one element, so this is pre-existing fail-closed hardening outside the completed follow-up implementation.
    location: 'tools/release_package_contract.py:303'
    severity: medium
  - summary: >-
      Pin publication-preflight execution before the irreversible NuGet push in semantic-release governance tests.
    evidence: |-
      .releaserc.json:12 currently runs validate-publication-preflight.sh in publish mode before dotnet nuget push, but ReleasePackageManifestTests.cs:305-310 pins only secret-validation ordering. Moving the publish-mode preflight after the push would remain green; verifyReleaseCmd is an earlier mitigation, making this pre-existing test hardening rather than a regression in the completed follow-up implementation.
    location: 'tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:305'
    severity: medium
  - summary: >-
      Reconcile Story 4.7 completion with its still-unchecked validation and reviewed-SHA/gitlink tasks.
    evidence: |-
      Concurrent commit edfee4aa07570eb067161ae81404ece11d415046 marks spec-4-7-tenants-query-provenance-follow-up.md done while its fresh dual-mode validation and reviewed Tenants SHA/root-gitlink tasks remain unchecked, and its implementation notes explicitly say those obligations remain open. This is unrelated concurrent work and is not part of the DW-4/DW-5 implementation.
    location: '_bmad-output/implementation-artifacts/spec-4-7-tenants-query-provenance-follow-up.md:64'
    severity: medium
  - summary: >-
      Reconcile Story 4.7's done spec status with its sprint ledger review status.
    evidence: |-
      Concurrent commit edfee4aa07570eb067161ae81404ece11d415046 sets the Story 4.7 spec to done while sprint-status.yaml keeps 4-7-tenants-query-provenance-follow-up at review. This status disagreement is outside the independent Aspire/packaging review bundle.
    location: '_bmad-output/implementation-artifacts/sprint-status.yaml:257'
    severity: medium
  - summary: >-
      Normalize the concurrently appended Story 4.7 deferred-work entries to the ledger's governed record schema.
    evidence: |-
      Concurrent commit edfee4aa07570eb067161ae81404ece11d415046 appends seven bullet-style entries with absolute source_spec paths and without identifiers, location, severity, or status fields. DW-456 already records that this shape evades governance parsing. The current user explicitly forbids this run from editing the deferred-work ledger.
    location: '_bmad-output/implementation-artifacts/deferred-work.md:3522'
    severity: medium
---

<intent-contract>

## Intent

**Problem:** The completed Aspire resource-naming and manifest-driven packaging stories retained recommendations for deliberate independent follow-up review. Their review-driven hardening is already committed at the current baseline, so this dispatch must prove that work at the operator-visible and release-contract surfaces without reopening verified or already-ledgered concerns.

**Approach:** Treat the committed follow-up implementation as the execution baseline, independently verify both focused surfaces, and patch only a newly reproduced in-scope defect. Preserve any still-reproducible but non-blocking finding in this spec's `deferred` metadata while leaving the orchestrator-owned ledger untouched.

## Boundaries & Constraints

**Always:** Preserve the `security` resource default, supported name/realm/import overrides, Keycloak endpoint and authentication behavior, the 14-package manifest, both validator entry points, and the single EventStore-scoped NuGet push. Keep AppHost environment mutation serialized and exactly restored. Treat both source story specs and the prior follow-up spec as read-only evidence. The four execution paths were clean at dispatch and their implementation is already present at `baseline_revision`; rerun the specified verification and finish when it passes without requiring a fresh source diff.

**Never:** Edit `_bmad-output/implementation-artifacts/deferred-work.md`, `references/**`, the source story specs, production topology, package inventory or identities, or publication configuration without a newly reproduced in-scope failure. Do not publish, deploy, rerun a live Aspire topology, or reopen the already-ledgered CLI-flag, shared-temp, source-mode-CI, version-example, GitHub-asset-glob, or stale-ledger-citation findings.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Naming follow-up | Tracked operator text, hostile Keycloak environment values, and default/overridden realm options | The audit rejects stale `keycloak` role identities, actual AppHost models stay deterministic, and realm/import annotations remain exact | A focused failure must identify the violated path, environment contract, or annotation before any patch |
| Packaging follow-up | Repeated TFM groups and extra NuGet push/operand mutations | Both validators reject union-based dependency false passes and governance rejects every publication expansion | A focused failure must identify the malformed archive or semantic-release command |
| Clean completed re-drive | Committed implementation and no uncommitted overlap in the four execution paths | Verification/finalization completes without recreating the implementation | Block only on a named owned-path overlap or an exact failed verification command |
| Residual review finding | Reproducible concern outside the accepted follow-up boundary | Record evidence in this spec's `deferred` metadata and keep verified work closed | Do not edit the deferred-work ledger or broaden implementation implicitly |

</intent-contract>

## Code Map

- `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md` -- read-only naming intent and completed outer-surface evidence.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs:17` -- serialized environment snapshot/override/restore, actual-AppHost model assertions, tracked audit pathspec, and shared stale-identity mutations.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreSecurityExtensionsTests.cs:75` -- default and override realm URL/import annotation preservation.
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityOptions.cs:11` and `src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs:24` -- production naming and wiring invariants; verification-only unless a focused test reproduces a defect.
- `_bmad-output/implementation-artifacts/spec-3-6-manifest-driven-release-packaging.md` -- read-only package-contract intent and completed release evidence.
- `tools/release_package_contract.py:303` -- shared nuspec dependency parser used by both validator entry points; repeated TFM groups already fail case-insensitively.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs:279` -- release ordering, repeated-group, and extra-push/operand mutation coverage.
- `.releaserc.json:12` -- current single EventStore-scoped NuGet publication command; verification-only.
- `_bmad-output/implementation-artifacts/spec-independent-followup-reviews.md` -- read-only provenance for the already-committed five-defect follow-up implementation and its first review pass.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs` and `tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreSecurityExtensionsTests.cs` -- retain the committed stale-wait, hostile-environment, and realm/import regression guards; patch only if focused verification reproduces a current defect.
- [x] `tools/release_package_contract.py` and `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` -- retain the committed repeated-group and sole-scoped-publication fail-closed guards; patch only if focused verification reproduces a current defect.
- [x] `_bmad-output/implementation-artifacts/spec-independent-followup-reviews-2.md` -- record the two independently reproduced residual findings without modifying the deferred-work ledger.

**Acceptance Criteria:**
- Given tracked operator surfaces and hostile persistent/port environment values, when the focused AppHost review tests run, then stale `aspire wait keycloak` forms fail mutation controls, the current tree passes its audit, and every caller environment value is restored exactly.
- Given default and overridden realm/import options, when the security resource model is inspected, then the complete realm URL and exact import annotation are preserved while the default role remains `security`.
- Given repeated same-TFM dependency groups or an added NuGet push/operand, when both release validators and semantic-release governance tests run, then each malformed case fails closed while the current package contract passes.
- Given the four code execution paths are clean at dispatch and all specified verification succeeds, when finalization runs, then the bundle completes without requiring a new implementation diff and any unrelated repository history remains untouched.
- Given a still-reproducible concern outside this bundle's accepted implementation boundary, when review triage completes, then the finding is recorded with concrete evidence in this spec and the deferred-work ledger is not edited.

## Spec Change Log

- 2026-09-05 -- Re-drove the committed naming and packaging follow-up implementation at baseline `08f90f4bc143b657c712433a179667c88875aecf`. Every specified focused build, test, dry-run, and whitespace check passed, so no production, test, packaging, publication, or shared-ledger change was required; the two independently reproduced residual findings remain recorded only in this spec's `deferred` metadata.

## Review Triage Log

### 2026-09-05 — Review pass
- verdicts: 16 findings — high 0, medium 3, low 1, false 12, maybe-false 0
- findings:
  - `[false]` `[reject]` The reviewed baseline diff includes seven deferred-ledger additions despite this spec's no-ledger boundary — `git show --stat edfee4aa07570eb067161ae81404ece11d415046` proves the ledger delta belongs to a concurrent Story 4.7 commit made after this run captured baseline `08f90f4bc143b657c712433a179667c88875aecf`; this run's staged change contains only this spec and the ledger is clean in the working tree.
  - `[false]` `[reject]` The reviewed baseline diff includes unrelated Story 4.7/5.1 specs, sprint state, and Server tests — the current index contains only this spec; Story 4.7 is isolated in concurrent commit `edfee4aa07570eb067161ae81404ece11d415046`, and the Story 5.1 files are separate unstaged concurrent work rather than changes produced by this bundle.
  - `[medium]` `[defer]` Story 4.7 is marked done while its reviewed-SHA/gitlink and fresh dual-mode validation tasks remain unchecked — the cited tasks and implementation notes remain open in the concurrently committed spec, so its completion status can misroute automation; this unrelated finding is preserved in frontmatter.
  - `[medium]` `[defer]` Story 4.7's spec is done while sprint-status records review — both current files show the disagreement introduced by concurrent commit `edfee4aa07570eb067161ae81404ece11d415046`; this unrelated status-authority finding is preserved in frontmatter.
  - `[false]` `[reject]` Story 5.1's in-review spec and in-progress sprint row are a finalized inverse mismatch — the spec and Server-test changes remain unstaged concurrent work in an active workflow, while this run's index contains only its own spec; no finalized Story 5.1 transition is present in the reviewed change.
  - `[false]` `[reject]` This spec's Review Triage Log was missing after a completed review — the reviewer read the artifact while status was in-review and before this mandatory classification step; the log is populated by the current step, not by implementation.
  - `[low]` `[reject]` The spec did not list the exact scoped-cleanliness status command — both delegated and parent verification executed the four-path status check and found it empty, and the result record states those paths were unchanged; the only proposed remedy edits this build's spec, which review findings are not permitted to require.
  - `[medium]` `[defer]` Seven concurrently appended deferred-ledger records omit governed identity/status fields and use machine-specific source paths — the current ledger and DW-456 confirm that the bullet shape evades governance parsing; the user forbids ledger edits in this run, so the unrelated finding is preserved in frontmatter.
  - `[false]` `[reject]` The new ledger item claims it already refreshed stale Story 4.7 evidence — its imperative summary records the refresh as deferred work and its evidence explicitly says the old record still needs maintenance; it does not claim that refresh was performed.
  - `[false]` `[reject]` The uncertain admission-staging concern was promoted as established — both the concurrently appended ledger text and its source triage label the question as needing a fault test or Dapr guarantee, preserving rather than erasing the uncertainty.
  - `[false]` `[reject]` Changed save-call counts are the only proof of owner-aware finalization — dedicated infrastructure tests inspect committed pending counts, publication owners, reconciliation flags, and later-turn recovery, including AggregateActorInfrastructureFailureTests.cs:797-815 and 1518-1548; the cited count assertions are narrower regression checks, not the sole proof.
  - `[false]` `[reject]` Converted dead-letter tests omit the state-safety proof enabled by the faulting manager — dedicated AggregateActorInfrastructureFailureTests cases assert committed-state cleanup and remediation ordering; the cited dead-letter and tracing tests intentionally retain responsibility only for publication payload and observability behavior.
  - `[false]` `[reject]` Edge-case review found that this run edited the deferred ledger — the ledger change is wholly contained in concurrent commit `edfee4aa07570eb067161ae81404ece11d415046`, while this run stages only its spec and leaves the ledger clean.
  - `[false]` `[reject]` Edge-case review found unrelated actor tests could ship under this bundle's verification-only approval — those tests are unstaged concurrent Story 5.1 work and are absent from this run's staged change, so this workflow neither approves nor commits them.
  - `[false]` `[reject]` Intent alignment found the unified baseline diff primarily implements unrelated Story 4.7/5.1 work — that observation describes concurrent repository activity after baseline capture, not this run's change; the staged bundle artifact alone implements the defensible verification/finalization reading.
  - `[false]` `[reject]` Intent alignment found a direct no-ledger-edit violation — Git ownership proves the ledger delta is the separate concurrent Story 4.7 commit `edfee4aa07570eb067161ae81404ece11d415046`; the current bundle has no staged or working-tree ledger edit.

## Design Notes

This is a completed-work re-drive. The independently reviewed implementation lives in the current baseline; verification is the outer proof, and absence of a fresh code diff is expected rather than a reason to reopen the source stories.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class Hexalith.EventStore.AppHost.Tests.Configuration.AspireSecurityResourceNamingTests` -- expected: all actual-model, audit-control, mutation, and hostile-environment cases pass.
- `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class Hexalith.EventStore.AppHost.Tests.Configuration.HexalithEventStoreSecurityExtensionsTests` -- expected: default and override realm/import cases pass.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release -m:1 -p:UseHexalithProjectReferences=false` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests` -- expected: current manifest/release configuration and every mutation case pass.
- `python3 tools/pack-release-packages.py /tmp/eventstore-independent-review-dry-2 999.9.2-review --dry-run` -- expected: exactly 14 Release/package-mode commands and no package output.
- `git diff --check` -- expected: no whitespace errors in this run's working-tree changes.

**Recorded Results:** Both focused Release builds passed with zero warnings and errors. `AspireSecurityResourceNamingTests` passed 5/5, `HexalithEventStoreSecurityExtensionsTests` passed 10/10, and `ReleasePackageManifestTests` passed 114/114 with no skips or not-run cases. The package dry run emitted exactly 14 Release/package-mode commands and left `/tmp/eventstore-independent-review-dry-2` absent. `git diff --check` passed. No newly reproduced in-scope defect required a source patch, and the four execution code paths remained unchanged.

## Auto Run Result

Status: blocked

Blocking condition: finalization left repository dirty.

Finalization evidence: after commit `a511db66068b7359c775a158d79a7eaaa1441f57`, `git status --short --branch` still reported unrelated unstaged Story 5.1 changes under `_bmad-output/implementation-artifacts/spec-5-1-infrastructure-failure-cache-clear.md`, `src/Hexalith.EventStore.Server/Actors/`, and `tests/Hexalith.EventStore.Server.Tests/`. Those concurrent changes include modified and deleted files and cannot be staged, committed, reverted, or cleaned by this bundle. The bundle-owned spec was committed, its four execution code paths remained clean, and this run did not edit the deferred-work ledger.

Summary: Independently re-verified the committed Aspire naming and manifest-driven packaging follow-up implementation. No current in-scope defect reproduced, so verified source, topology, package inventory, validator, and publication behavior remain unchanged. Five concrete residual findings are recorded in this spec's `deferred` metadata; the orchestrator-owned deferred-work ledger was not edited by this run.

Files changed:
- `_bmad-output/implementation-artifacts/spec-independent-followup-reviews-2.md` -- records the current-baseline review plan, fresh verification evidence, complete four-layer triage, and residual findings.

Review findings breakdown:
- Patches applied: 0 (high 0, medium 0, low 0).
- Items deferred this pass: 3 medium findings, all from concurrent Story 4.7/ledger work that entered the unified baseline diff after capture; they concern an unsupported done status, spec/sprint status disagreement, and non-governed deferred-record shape.
- Rejected: the ledger-edit and unrelated-diff claims were disproved by commit/index ownership; the Story 5.1 status claim describes active unstaged work; the empty triage-log claim observed the artifact before this step populated it; the scoped-status observation is low and was independently checked; the stale-record item is explicitly a deferred action; the staging-exception record preserves uncertainty; dedicated infrastructure tests disprove both alleged Server-test proof gaps; and both edge-case plus both intent-alignment divergence claims attribute concurrent work to this run despite the index containing only this spec.

Follow-up review recommendation: false. This pass patched no high, medium, or low entry; the focused implementation has converged.

Verification performed:
- AppHost Release build: succeeded with 0 warnings and 0 errors.
- `AspireSecurityResourceNamingTests`: 5/5 passed, 0 skipped/not run.
- `HexalithEventStoreSecurityExtensionsTests`: 10/10 passed, 0 skipped/not run.
- Contracts Release build: succeeded with 0 warnings and 0 errors.
- `ReleasePackageManifestTests`: 114/114 passed, 0 skipped/not run.
- Package dry run: exactly 14 Release/package-mode commands; output directory remained absent.
- Four execution paths: clean in both dispatch-start and final scoped status checks.
- Deferred-work ledger: no staged or working-tree change from this run.
- YAML parse and staged/unstaged whitespace checks: passed.

Residual risks: malformed nuspecs with multiple `<dependencies>` elements remain only partially inspected; publication-preflight ordering before NuGet push remains unpinned; and the three concurrent Story 4.7/ledger consistency findings remain owned outside this bundle.
