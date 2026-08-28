---
title: 'Update Packages and Aspire to Latest Compatible Releases'
type: 'chore'
created: '2026-08-28'
status: 'in-progress'
baseline_commit: '05769ed89c4e99b283f862ca956900b14d825b1a'
review_loop_iteration: 0
context:
  - '_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The shared NuGet catalog is behind currently listed releases, its checked-in audit no longer matches the catalog, and EventStore remains on Aspire 13.4.6 while the latest stable Aspire release is 13.5.3. Partial Aspire upgrades are unsafe because 13.5 is not binary-compatible with mixed 13.4 hosting integrations.

**Approach:** Re-run the source-aware catalog audit, advance every package to its latest validated compatible release by rollback-safe family, and reconcile retained exceptions. Upgrade the complete EventStore Aspire family to 13.5.3, including aligned previews and the AppHost SDK, then prove package-mode build and runtime compatibility.

## Boundaries & Constraints

**Always:** Treat `references/Hexalith.Builds/Props/Directory.Packages.props` as the sole NuGet authority; prefer the latest listed stable version, preserve intentional prerelease channels, and update coupled families atomically. Keep `Aspire.AppHost.Sdk`, stable Aspire packages, Keycloak/Kubernetes previews, CommunityToolkit Dapr, and the exception inventory coherent. Preserve NuGet auditing, warning-as-error behavior, package-mode CI semantics, and unrelated user work.

**Ask First:** Expanding into source/API migrations unrelated to compatibility, changing machine-installed Aspire tooling, modifying consumer repositories not owned by this workspace change, or accepting a package family whose focused validation fails.

**Never:** Add versions to the root wrapper or ordinary `PackageReference` items; downgrade because a feed is missing, unlisted, unresolved, or reports an older stable than an intentional prerelease; mix Aspire 13.4 and 13.5 in EventStore; initialize nested submodules; suppress audit/analyzer failures; publish, commit, push, or rewrite unrelated history.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Stable update | A listed newer stable candidate exists | Advance the complete rollback family and record current source evidence | Revert that family if representative validation fails |
| Prerelease family | Current pin intentionally uses preview/RC/beta | Select the newest compatible release on that channel or a newer stable major | Retain with rationale when compatibility is unproven |
| Incomplete feed result | Package is missing, unlisted, or unresolved | Retain the current pin without downgrade and record a recheck trigger | Fail closed on incomplete audit coverage |
| Aspire upgrade | Aspire 13.5.3 family is available | Align SDK/stable packages to 13.5.3, Keycloak/Kubernetes to `13.5.3-preview.1.26425.3`, and Dapr toolkit to `13.5.0-preview.1.260825-0345` | Roll back the entire Aspire family on compile or runtime incompatibility |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Builds/Props/Directory.Packages.props` -- authoritative 285-row catalog and all coupled family pins.
- `references/Hexalith.Builds/Tools/audit-central-package-versions.ps1` / `Tools/package-version-audit.json` -- live NuGet discovery and deterministic selection/disposition evidence; the current audit has a known catalog-hash/five-selection baseline failure.
- `references/Hexalith.Builds/Tools/package-version-exceptions.json` -- closed non-CPM inventory; all ten declared `Aspire.AppHost.Sdk` expectations must equal `Aspire.Hosting`.
- `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj:1` -- EventStore's non-CPM SDK pin, currently 13.4.6.
- `src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj` and `tests/Hexalith.EventStore.AppHost.Tests/` -- representative compile/model tests for Aspire integrations.
- `_bmad-output/planning-artifacts/architecture.md`, `_bmad-output/project-context.md`, and `docs/reference/nuget-packages.md` -- current-version snapshots to synchronize from accepted catalog evidence.
- `Directory.Packages.props`, `global.json`, and sibling consumer submodules -- read-only boundaries except for the explicitly listed EventStore SDK pin.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Builds/Tools/package-version-audit.json` -- regenerate against configured NuGet sources and disposition every catalog row without losing prior evidence.
- [x] `references/Hexalith.Builds/Props/Directory.Packages.props` -- apply latest compatible candidates atomically by family, including the complete Aspire 13.5.3 set.
- [x] `references/Hexalith.Builds/Tools/package-version-exceptions.json` and `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` -- align non-CPM Aspire SDK declarations with `Aspire.Hosting`.
- [x] Version snapshot documentation -- update only current accepted package/Aspire values and retain historical records unchanged.

**Acceptance Criteria:**
- Given the evaluated catalog and configured sources, when the audit is regenerated, then all 285 package rows have a current candidate state, selected version, disposition, rollback group, and evidence.
- Given accepted updates, when Builds governance runs, then catalog, audit, family, exception, Dapr, and consumer-authority validators all pass.
- Given Aspire 13.5.3 pins, when EventStore restores, builds, and runs focused AppHost tests in Release package mode, then no 13.4 package remains in its evaluated Aspire graph and all tests pass.
- Given the upgraded AppHost, when Aspire starts and resource state is described, then the topology reaches a healthy runnable state without type-load or missing-method failures.

## Spec Change Log

- 2026-08-28: Regenerated the 285-row NuGet audit, accepted source-resolved compatible updates, and recorded failed-family rollback evidence for NBomber 6.6.0, Roslyn 5.9.0, and xUnit 4.0.0.
- 2026-08-28: Upgraded the complete Aspire family and AppHost SDK inventory to 13.5.3, synchronized current version snapshots, and completed package-mode build, focused governance/AppHost tests, and healthy runtime verification.

## Design Notes

Aspire 13.5 explicitly warns that mixed 13.4/13.5 hosting packages can fail at runtime. Keep the existing default orchestration dependency mode; adopting the optional CLI bundle or mutating the user-scoped CLI is outside this repository-only upgrade.

Compatibility validation retained Roslyn 5.6.0 because the pinned SDK compiler cannot load 5.9 analyzers, NBomber 6.5.0 because 6.6 makes `NodeStats.AllFailCount` obsolete under warning-as-error, and the aligned xUnit 3.x family because xUnit 4 rejects the existing parallelization attribute. Their audit family decisions contain the exact failure evidence and recheck triggers.

## Verification

**Commands:**
- `pwsh -NoProfile -File ./Tools/validate-central-package-versions.ps1` plus audit, exception, Dapr, and consumer-authority validators from `references/Hexalith.Builds` -- expected: all deterministic package-governance gates pass.
- `dotnet restore Hexalith.EventStore.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false` and serialized warning-as-error Release build -- expected: clean package-mode restore/build.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release` and the equivalent AppHost test project command -- expected: package governance and Aspire tests pass.
- `aspire start`, `aspire describe`, and `aspire stop` against `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` -- expected: upgraded topology starts and reports healthy resources.
- `bash scripts/check-doc-versions.sh` -- expected: documented package versions remain catalog-consistent.

**Results:** All package-governance validators passed; Release package-mode restore/build succeeded with only the expected `ASPIRE010` CLI-bundle warning; focused package-governance tests passed 149/149; AppHost tests passed 95/95; the evaluated AppHost graph contained no Aspire 13.4 packages; and EventStore, security, state store, and pub/sub reached `Running`/`Healthy` with no error-severity telemetry. The broad Contracts project run remains externally blocked at 1,563 passed / 200 failed because every OQ8 failure reports the pre-existing `Review subject binding drift: ciWorkflow`; those unrelated frozen evidence bindings were not changed.
