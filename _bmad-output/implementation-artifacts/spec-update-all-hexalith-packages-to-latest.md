---
title: 'Update all Hexalith packages and source checkouts to latest releases'
type: 'chore'
created: '2026-09-06'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/project-context.md'
  - '_bmad-output/planning-artifacts/architecture.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The shared Builds catalog contains 72 `Hexalith.*` package entries, of which the live NuGet audit identifies 14 reachable entries in the Memories, Parties, and Tenants families as behind their latest stable releases. Root-declared source submodules for package-backed families also do not consistently point at the exact tags corresponding to their package versions.

**Approach:** Refresh evidence for every Hexalith catalog family, update stale family pins atomically to the latest published stable versions, and align each root-declared package-backed source submodule to the exact matching release tag. Preserve unresolved package identities and concurrent user work unless the decisions below explicitly authorize broader handling.

## Boundaries & Constraints

**Always:** Treat `references/Hexalith.Builds/Props/Directory.Packages.props` as the sole package-version authority; use the configured NuGet source and latest listed stable releases; update package families atomically; never downgrade when a package is missing, unlisted, or reported older than the current pin; preserve the generator's source evidence. Align the root source checkouts to `Hexalith.Commons` `v2.30.0`, `Hexalith.FrontComposer` `v4.3.0`, `Hexalith.Memories` `v2.26.1`, `Hexalith.PolymorphicSerializations` `v1.19.2`, and `Hexalith.Tenants` `v5.7.0`. Capture the implementation-start Git state and preserve/exclude all pre-existing work.

**Never:** Upgrade non-Hexalith dependencies, the .NET SDK, or Aspire; change application behavior or feature code; initialize/update nested submodules; reset the EventStore repository to a package tag; rewrite history; stage or commit unrelated work; publish, push, deploy, or bypass package-audit or commit-message validation.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Newer stable release | Listed stable version exceeds the family pin | Advance the shared family property and every existing family row coherently; refresh audit evidence | Stop the family update if compatibility validation fails |
| Already current | Latest stable equals the pin | Retain the version and refresh evidence | Report no-op, do not churn the catalog |
| Missing, unlisted, or older result | NuGet cannot supply a qualifying newer stable version | Retain the current pin and record the observed source state | Never infer a downgrade or fabricate a release |
| Source/package parity | A root package-backed submodule has the exact release tag | Check out the tag's commit and update only the root gitlink | Stop if the tag is absent or does not resolve uniquely |
| Concurrent dirty work | Root or submodule contains pre-existing changes | Exclude those paths from this task and leave their state intact | Stop before any overlapping write or unsafe checkout |

</frozen-after-approval>

## Open Questions

- Missing/renamed catalog identities — options: retain all 72 existing entries and only advance their shared family versions, leaving the five unpublished IDs explicitly unresolved in the audit (keeps the existing catalog contract; recommended) / replace or expand catalog membership to mirror current release manifests (a broader shared-catalog migration affecting consumers).
- Audit provenance commits — options: authorize the minimum local commits, without pushing, required to commit the Builds catalog before generating and validating its revision-bound audit, then record the parent gitlink changes (produces valid reproducible evidence; recommended) / prohibit commits (the catalog may be edited, but its authoritative audit cannot be validly regenerated, so acceptance cannot be completed).

## Code Map

- `references/Hexalith.Builds/Props/Directory.Packages.props` -- authoritative package catalog and Hexalith family pins.
- `references/Hexalith.Builds/Tools/package-version-audit.json` -- revision-bound NuGet evidence consumed by validators.
- `references/Hexalith.Builds/Tools/audit-central-package-versions.ps1` -- source-aware audit generator.
- `Directory.Packages.props` -- import-only root entry point; must remain free of local versions.
- `.gitmodules` -- authoritative boundary for root-declared submodules.
- `references/Hexalith.{Commons,FrontComposer,Memories,PolymorphicSerializations,Tenants}` -- source-mode checkouts requiring exact package-tag parity.

## Tasks & Acceptance

**Execution:**
- [ ] Repository state -- capture root and affected-submodule status before writes; protect every pre-existing change throughout implementation.
- [ ] `references/Hexalith.Builds/Props/Directory.Packages.props` -- update Memories `2.25.0` to `2.26.1`, Parties `1.0.0` to `1.1.1`, and Tenants `5.6.0` to `5.7.0`; retain every other Hexalith family unless refreshed evidence proves a newer qualifying stable version.
- [ ] `references/Hexalith.Builds/Tools/package-version-audit.json` -- after the catalog revision exists, refresh all 15 Hexalith family identifiers and validate revision provenance without degrading non-Hexalith evidence.
- [ ] Root package-backed submodule gitlinks -- resolve and check out the exact approved tags, then update only the corresponding root gitlinks.
- [ ] Package- and source-mode consumers -- restore/build the affected solution paths and run focused package-governance and compatibility tests.

**Acceptance Criteria:**
- Given every existing Hexalith catalog row, when the complete source-aware audit is evaluated, then each row is either at the latest listed stable release or retained with explicit missing/unlisted/older evidence and no downgrade.
- Given a root-declared package-backed Hexalith source submodule, when its family has a catalog version, then its checkout resolves to the exact matching stable tag listed above.
- Given package mode and the affected source-mode paths, when restore, build, and focused tests run, then they pass with warnings treated as errors.
- Given the captured dirty baseline, when the final diff and index are inspected, then no pre-existing or unrelated work was modified, staged, or committed by this task.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

The audit binds `generatedFromRevision` to committed catalog content, so catalog and evidence are intentionally sequenced: validate and locally commit the catalog, regenerate evidence against that exact revision, validate the audit, then update the parent Builds gitlink. This is provenance, not release publication; pushing and publishing remain out of scope.

## Verification

**Commands:**
- `pwsh -NoProfile -File ./Tools/validate-central-package-versions.ps1` in `references/Hexalith.Builds` -- catalog invariants pass.
- `pwsh -NoProfile -File ./Tools/validate-package-version-audit.ps1` in `references/Hexalith.Builds` -- refreshed evidence and catalog revision agree.
- `pwsh -NoProfile -File ./Tools/test-authoritative-package-catalog.ps1` plus the central-version, audit-generator, audit-validator, consumer-authority test scripts -- all governance regression suites pass.
- `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false` -- package-mode restore succeeds.
- `dotnet build Hexalith.EventStore.slnx --no-restore --configuration Release -warnaserror -m:1 -p:UseHexalithProjectReferences=false` -- package-mode solution build succeeds.
- Focused affected source-mode builds and Admin/Contracts tests -- package/source compatibility passes without initializing nested submodules.
- `git diff --check` and root/submodule status inspection -- clean patch formatting and protected baseline preserved.
