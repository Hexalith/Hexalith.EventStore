---
title: 'Update all Hexalith packages and source checkouts to latest releases'
type: 'chore'
created: '2026-09-06'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'b869bc26fb4726b76eabdea1e226b4ebf3fcacef'
context:
  - '_bmad-output/project-context.md'
  - '_bmad-output/planning-artifacts/architecture.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A live NuGet audit identifies 14 of the shared catalog's 72 `Hexalith.*` entries, across Memories, Parties, and Tenants, as behind their latest stable releases. Several package-backed source submodules also differ from their package-version tags.

**Approach:** Refresh every Hexalith family's evidence, atomically update stale family pins, and align each root package-backed source submodule to its exact release tag while preserving unresolved identities and concurrent work.

## Boundaries & Constraints

**Always:** Keep `references/Hexalith.Builds/Props/Directory.Packages.props` as the sole version authority; use the configured NuGet source and latest listed stable releases; update families atomically; never downgrade missing, unlisted, or older results; preserve source evidence. Align root checkouts to Commons `v2.30.0`, FrontComposer `v4.3.0`, Memories `v2.26.1`, PolymorphicSerializations `v1.19.2`, and Tenants `v5.7.0`. Capture and preserve/exclude all pre-existing Git state.

**Never:** Upgrade non-Hexalith dependencies, .NET, or Aspire; change feature code; initialize nested submodules; reset EventStore to a package tag; rewrite history; include unrelated work; publish, push, deploy, or bypass validation.

## Decisions

- Preserve all 72 catalog entries. Advance qualifying family versions while retaining the five unpublished identities with unresolved audit evidence.
- Create only the validated local commits required for revision-bound Builds catalog and audit provenance and the parent gitlink updates. Do not push those commits.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Newer stable release | Listed stable exceeds the family pin | Advance the family property and rows coherently; refresh evidence | Stop if compatibility validation fails |
| Already current | Latest stable equals the pin | Retain the version and refresh evidence | Report no-op, do not churn the catalog |
| Missing, unlisted, or older result | No qualifying newer stable version | Retain the pin and record source state | Never infer a downgrade or release |
| Source/package parity | A root package-backed submodule has the exact release tag | Check out the tag's commit and update only the root gitlink | Stop if the tag is absent or does not resolve uniquely |
| Concurrent dirty work | Pre-existing root/submodule changes | Exclude them and leave their state intact | Stop before an overlapping write or unsafe checkout |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Builds/Props/Directory.Packages.props` -- authoritative package catalog and Hexalith family pins.
- `references/Hexalith.Builds/Tools/package-version-audit.json` -- revision-bound NuGet evidence consumed by validators.
- `Directory.Packages.props` -- import-only root entry point; must remain free of local versions.
- `.gitmodules` -- authoritative boundary for root-declared submodules.
- `references/Hexalith.{Commons,FrontComposer,Memories,PolymorphicSerializations,Tenants}` -- source-mode checkouts requiring exact package-tag parity.

## Tasks & Acceptance

**Execution:**
- [ ] Repository state -- capture root and affected-submodule status; protect all pre-existing changes.
- [ ] `references/Hexalith.Builds/Props/Directory.Packages.props` -- update Memories `2.25.0` to `2.26.1`, Parties `1.0.0` to `1.1.1`, and Tenants `5.6.0` to `5.7.0`; retain every other Hexalith family unless refreshed evidence proves a newer qualifying stable version.
- [ ] `references/Hexalith.Builds/Tools/package-version-audit.json` -- after committing the catalog, refresh all 15 Hexalith families and validate provenance without degrading other evidence.
- [ ] Root package-backed submodule gitlinks -- resolve and check out the exact approved tags, then update only the corresponding root gitlinks.
- [ ] Package/source consumers -- restore/build affected paths and run focused governance and compatibility tests.

**Acceptance Criteria:**
- Given every Hexalith catalog row, when the audit is evaluated, then each is latest stable or retained with explicit missing/unlisted/older evidence and no downgrade.
- Given a root-declared package-backed Hexalith source submodule, when its family has a catalog version, then its checkout resolves to the exact matching stable tag listed above.
- Given package and affected source modes, restore, build, and focused tests pass with warnings as errors.
- Given the captured dirty baseline, when the final diff and index are inspected, then no pre-existing or unrelated work was modified, staged, or committed by this task.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

The audit binds `generatedFromRevision` to committed catalog content. Validate and locally commit the catalog, regenerate and validate evidence against that revision, then update the parent Builds gitlink. This is provenance, not publication.

## Verification

**Commands:**
- `pwsh -NoProfile -File ./Tools/validate-central-package-versions.ps1` in `references/Hexalith.Builds` -- catalog invariants pass.
- `pwsh -NoProfile -File ./Tools/validate-package-version-audit.ps1` in `references/Hexalith.Builds` -- refreshed evidence and catalog revision agree.
- Governance test scripts for the catalog, audit generator/validator, and consumer authority -- all pass.
- `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false` -- package-mode restore succeeds.
- `dotnet build Hexalith.EventStore.slnx --no-restore --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- package-mode solution build succeeds.
- Focused source-mode builds and Admin/Contracts tests -- compatibility passes without nested-submodule initialization.
- `git diff --check` and root/submodule status inspection -- clean patch formatting and protected baseline preserved.
