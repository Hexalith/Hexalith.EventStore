---
title: Sprint Change Proposal - Centralize And Refresh The Hexalith NuGet Catalog
status: final
created: 2026-07-18
project: eventstore
mode: batch
scope_classification: moderate
approval: approved
approved_by: Administrator
approved_on: 2026-07-18
finalized_on: 2026-07-18
trigger: stakeholder-directed package governance correction
target_story: "3.5"
proposed_refresh_story: "3.11"
---

# Sprint Change Proposal: Centralize And Refresh The Hexalith NuGet Catalog

## 1. Issue Summary

Hexalith package governance is split between the shared Builds catalog and consuming-repository overrides. EventStore imports `references/Hexalith.Builds/Props/Directory.Packages.props`, but its root `Directory.Packages.props` still declares repository-local versions for `System.CommandLine`, `ModelContextProtocol`, `NBomber`, `NBomber.Http`, `Microsoft.Playwright`, and `xunit.v3.extensibility.core`; the current working-tree correction has already removed the local `Microsoft.Extensions.TimeProvider.Testing` entry. The root also contains a dormant `HexalithCommonsVersion` fallback. This contradicts the requested invariant that all source-owned NuGet dependency versions live in the shared Builds catalog.

MSBuild evaluation confirms that the Builds catalog currently supplies 266 `PackageVersion` entries and the EventStore wrapper supplies 268 effective entries. Two EventStore packages are absent from Builds: `NBomber.Http` `6.2.1` and `xunit.v3.extensibility.core` `3.2.2`. Two local overrides are older than their shared pins: `System.CommandLine` `2.0.9` versus `2.0.10`, and `ModelContextProtocol` `1.4.0` versus `1.4.1`. The local `NBomber` and `Microsoft.Playwright` versions already equal the shared values; the removed local TimeProvider pin was `10.7.0` while Builds supplies `10.8.0`.

The stakeholder also requires the central catalog to move to current package versions. A live NuGet.org candidate audit on 2026-07-18 found 20 evaluated entries whose search result differed from the current pin, but those results are not all directly adoptable. Examples include:

| Candidate class | Current | NuGet candidate | Required treatment |
| --- | --- | --- | --- |
| `Hexalith.PolymorphicSerializations*` family | `1.16.5` | `1.18.0` | Upgrade as an aligned family after restore/build/test proof. |
| `Microsoft.Azure.Cosmos` | `3.61.0` | `3.62.0` | Validate consuming storage/integration paths. |
| `Microsoft.Extensions.Localization` | `10.0.9` | `10.0.10` | Move with the aligned .NET 10 patch baseline. |
| `Microsoft.Identity.Web` | `4.13.1` | `4.13.2` | Validate authentication builds and tests. |
| OpenTelemetry instrumentation family | `1.16.0` / `1.16.0-beta.1` | `1.17.0` / `1.17.0-beta.1` | Align with exporter/hosting `1.17.0` and validate telemetry. |
| `Radzen.Blazor` | `11.1.5` | `11.1.6` | Validate UI consumers. |
| `System.Reactive` | `7.0.0-rc.1` | `7.0.0` | Validate the FrontComposer observable contract. |
| `Verify` / `Verify.XunitV3` | `31.24.2` | `31.25.0` | Upgrade together and run snapshot tests. |
| `Microsoft.TypeScript.MSBuild` | `6.0.3` | `7.0.0` | Treat as a major tooling migration with explicit validation. |
| `Microsoft.OpenApi` | `2.9.0` | `3.9.0` | Keep the latest compatible 2.x pin until the documented ASP.NET Core 10 runtime incompatibility is removed and proven. |
| `Hexalith.Tenants*` family | common `3.2.15` | divergent `3.2.16` and `3.15.1`; UI not returned | Resolve against an owner-approved release inventory; never manufacture a mixed family from individually newest results. |

The audit used exact package searches against NuGet.org and the NuGet V3 package base-address resource. Search output is candidate evidence, not compatibility or release authority. Unlisted packages, intentional prerelease channels, SDK-coupled packages, and versions already newer than a search result must be handled explicitly. References: [dotnet package search](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-search), [NuGet V3 API](https://learn.microsoft.com/en-us/nuget/api/overview), and [PackageBaseAddress](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource).

This is a package-governance and implementation-slicing correction. It does not change product scope, runtime architecture, or MVP capabilities.

## 2. Impact Analysis

### Epic And Story Impact

- Epic 3 remains valid and in progress. No new epic or epic resequencing is required.
- Revise Story 3.5 so it owns the single-catalog invariant and the migration of EventStore's local version declarations into Builds, in addition to its existing source/package-mode behavior.
- Add Story 3.11, **Validated Central Package Catalog Refresh**, for the catalog-wide latest-compatible upgrade and cross-repository validation. The broad refresh should not be hidden inside Story 3.5 because it has a different blast radius, rollback boundary, and evidence burden.
- Sequence Story 3.5 before Story 3.11. This establishes one version authority and a clean baseline before changing the wider dependency graph.
- Story 2.12 and Story 8.2 retain their existing behavior, but their future package-mode work must consume the shared catalog without local version declarations.

### Artifact Conflicts And Required Reconciliation

| Artifact | Impact |
| --- | --- |
| `prd.md` | Clarify FR21 and repository guardrail 8.1: the sole source-owned NuGet version authority is `references/Hexalith.Builds/Props/Directory.Packages.props`; define “latest” as latest validated compatible rather than an untested highest version. |
| `architecture.md` | Extend AD-11 and the release convention with single-catalog ownership, aligned-family rules, prerelease/major-upgrade gates, documented compatibility exceptions, and no-downgrade behavior. Refresh the stack table after versions are validated. |
| `ux.md` | No change. Package ownership and upgrade validation do not alter user journeys or UI requirements. |
| `epics.md` | Revise Story 3.5 and add Story 3.11 with separate acceptance criteria and sequence. |
| `sprint-status.yaml` | Add `3-11-validated-central-package-catalog-refresh: backlog` only after approval. Keep Story 3.5 at `backlog`. |
| Epic 3 implementation context | Recompile the story inventory, catalog invariants, and cross-story dependencies from the reconciled plan. |
| Builds central catalog | Add the two missing EventStore package IDs; then update accepted latest-compatible versions by family. Preserve intentional exceptions with rationale and a removal trigger. |
| Builds documentation and sample | Remove guidance that invites repository-specific `PackageVersion` entries. State that consumers request additions in Builds and their root files only import the shared catalog. |
| Builds automation | Add NuGet update ownership to Builds and validate uniqueness, family alignment, candidate freshness, and documented exceptions. |
| EventStore root package wrapper | Preserve the shared import and CPM properties; remove all local version properties and `PackageVersion` items. Do not overwrite the already removed TimeProvider entry. |
| EventStore tests and scripts | Generalize package-governance tests to reject local versions and require referenced IDs in Builds. Make `scripts/check-doc-versions.sh` read DAPR versions from the shared catalog; it currently fails because it searches the wrapper. |
| EventStore docs and update automation | Replace internal claims that the root wrapper owns versions; point maintainers to Builds. Preserve downstream-consumer instructions that legitimately refer to the consumer's own CPM file. Move ecosystem NuGet-update ownership from EventStore to Builds. |
| Tool manifest, SDK seed, generated fixtures, and cache evidence | No ownership change: `.config/dotnet-tools.json` is a .NET tool manifest, `global.json` is an SDK selector, the package-consumer validation script creates an ephemeral release-version fixture, and `.csproj.lscache` files are generated evidence rather than source-owned dependency declarations. |

### Technical Impact

- Removing EventStore overrides intentionally adopts the existing Builds pins for `System.CommandLine`, `ModelContextProtocol`, and TimeProvider; it is not a semantic no-op.
- The catalog refresh affects every repository that advances its Builds gitlink, so validation must be performed in Builds and representative consumers before release.
- Package families must remain internally coherent. A package-by-package “highest version” rewrite is prohibited when a release family publishes divergent trains or omits an expected artifact.
- Major versions, stable-to-prerelease transitions, prerelease-to-stable transitions, and SDK/framework-coupled pins require explicit compatibility review.
- A current pin must never be downgraded merely because search omits, unlists, or reports an older version.
- No completed application behavior or deployment configuration is rolled back.

## 3. Recommended Approach

Use **Direct Adjustment** inside Epic 3, split into two ordered stories:

1. establish Builds as the only source-owned NuGet version authority and migrate EventStore's local declarations; then
2. refresh the now-authoritative catalog to the latest validated compatible versions with family-aware, cross-repository evidence.

This isolates governance defects from dependency behavior changes. Story 3.5 can prove that every EventStore package ID resolves from Builds, while Story 3.11 can upgrade and roll back coherent version families independently.

- **Planning scope:** Moderate; one existing story changes and one backlog story is added.
- **Implementation effort:** High; the catalog has 266 evaluated entries and serves multiple repositories.
- **Technical risk:** High for the broad refresh, especially aligned Hexalith release families and major/tooling versions; moderate for the ownership migration.
- **Timeline impact:** Story 3.11 adds an explicit compatibility-validation slice. Story 3.5 should finish first.
- **MVP impact:** None. Release reliability improves without adding or removing product capabilities.

Rollback of completed product work is not justified. An MVP review is unnecessary because FR21 already requires centrally pinned package references; this proposal makes the authority and freshness policy precise.

## 4. Detailed Change Proposals

### 4.1 PRD FR21 And Repository Guardrail

**OLD**

> Cross-repo Hexalith library dependencies must use Debug source project references when explicitly enabled and Release package references by default, with package versions pinned centrally.

> Keep package versions centralized in `Directory.Packages.props`.

**NEW**

> Cross-repo Hexalith library dependencies must use Debug source project references when explicitly enabled and Release package references by default. Every source-owned NuGet dependency version used by a Hexalith repository must be declared in `references/Hexalith.Builds/Props/Directory.Packages.props`; consuming `Directory.Packages.props` files import that catalog and declare no local `PackageVersion`, version override, or fallback version property.

> Keep the shared Builds catalog on the latest validated compatible versions available from configured package sources. Stable pins prefer the latest stable release; intentional prerelease channels, aligned release families, framework/SDK coupling, and major-version compatibility are validated as units. Every retained exception records its reason, evidence, and removal trigger, and audit automation never downgrades a pin because a package is unlisted or omitted from search.

**Rationale:** “Centrally” currently permits competing interpretations. The exact authority and safe freshness semantics must be testable.

### 4.2 Architecture AD-11 And Release Convention

**OLD**

> Central .NET/ASP.NET security patch pins move and are validated as one unit; mixed patch bands are not releasable.

> Restore/build use `Hexalith.EventStore.slnx`; unit tests run per project; package versions live in central props; release output is manifest-driven.

**NEW**

> `references/Hexalith.Builds/Props/Directory.Packages.props` is the sole source-owned NuGet version catalog for Hexalith repositories. Consumer props only import it. The catalog is refreshed to latest validated compatible versions using configured package-source evidence, grouped restore/build/test validation, and representative consumer proof. Hexalith release families, .NET/ASP.NET patch bands, OpenTelemetry packages, test adapters, and other coupled sets move coherently. Major upgrades and channel changes require explicit proof. Compatibility exceptions record rationale and a removal trigger; missing, unlisted, or older search results never cause a downgrade.

> Restore/build use `Hexalith.EventStore.slnx`; unit tests run per project; all source-owned package versions resolve from the shared Builds catalog; release output is manifest-driven.

**Rationale:** Release reproducibility requires both a single authority and an upgrade policy that does not break coupled dependency graphs.

### 4.3 Story 3.5 - Revised Scope

**NEW TITLE**

> Story 3.5: Shared Package Catalog And Source/Package Reference Modes

**Preserve** all existing acceptance criteria for Debug/source and Release/package selection, one active dependency per mode, Gateway graph alignment, and restore isolation.

**ADD acceptance criteria:**

1. Given any source-owned Hexalith project or root package props is scanned, when NuGet version declarations are evaluated, then every version originates from `references/Hexalith.Builds/Props/Directory.Packages.props`, and consumer props contain no local `PackageVersion`, `VersionOverride`, or fallback dependency-version property.
2. Given EventStore's existing local entries, when the catalog migration is applied, then `NBomber.Http` and `xunit.v3.extensibility.core` exist in Builds, all EventStore local version declarations are removed, and effective evaluation resolves each prior package ID exactly once from Builds.
3. Given local overrides are removed, when package mode restores and focused validation runs, then adoption of the current Builds versions—including `System.CommandLine`, `ModelContextProtocol`, and TimeProvider—is explicit and verified rather than treated as a formatting-only change.
4. Given package-version documentation, scripts, samples, and dependency-update automation are reviewed, when Story 3.5 completes, then they identify Builds as the owner, `scripts/check-doc-versions.sh` reads the shared catalog successfully, and no official sample invites repository-local package versions.
5. Given tool-manifest, SDK, ephemeral consumer-fixture, or cache versions are encountered, when the governance scan reports them, then they are classified explicitly and are not rewritten as NuGet CPM entries.

**Rationale:** The existing Story 3.5 is the natural owner for package-reference mode but does not currently enforce where package versions live.

### 4.4 New Story 3.11 - Validated Central Package Catalog Refresh

**Requirements covered:** FR21, FR22, FR25, NFR9, NFR10, NFR16

As a Hexalith release maintainer,
I want the shared NuGet catalog refreshed to latest validated compatible package versions,
So that all consuming repositories inherit current dependencies from one reproducible and compatibility-proven authority.

**Acceptance Criteria:**

1. **Activation gate.** Given Story 3.5 is not done or the Builds catalog does not contain every migrated EventStore package ID, when Story 3.11 is evaluated, then the refresh remains backlog and no catalog-wide version change is accepted.
2. **Candidate inventory.** Given the configured NuGet sources and the evaluated Builds catalog, when the freshness audit runs, then it records every package ID, current version, latest stable and prerelease candidates as applicable, listing state, release-family membership, proposed disposition, and audit timestamp without silently dropping unresolved packages.
3. **Selection policy.** Given a stable pin, when a compatible stable update exists, then the latest validated stable release is selected. Intentional prerelease channels, major-version changes, framework/SDK-coupled packages, and stable/prerelease transitions require explicit disposition and proof.
4. **Family coherence.** Given packages share a Hexalith release property or another coupled version family, when versions are selected, then the family uses an owner-approved common release inventory or an explicitly documented split; individually newest but incompatible package versions are never combined. The `Hexalith.Tenants` divergence is resolved with Tenants release-owner evidence before its pin changes.
5. **Compatibility exceptions.** Given the newest discovered release is incompatible, unavailable, unlisted, or older than the current pin, when the current compatible version is retained, then the catalog records the reason, supporting validation or upstream constraint, and a removal trigger. `Microsoft.OpenApi` remains on the latest proven 2.x line until ASP.NET Core 10 compatibility with v3 is demonstrated; `Microsoft.SourceLink.GitHub` is not downgraded from `10.0.302` because search reports `10.0.301`.
6. **Validation.** Given candidate families are applied in reviewable groups, when validation runs, then the Builds central-catalog validator passes; Builds restores/builds/tests pass; EventStore package-mode restore, build, focused tests, pack validation, and documentation-version checks pass; and affected representative consumers run their repository-prescribed validation.
7. **Reproducible evidence.** Given the refresh is submitted for review, when maintainers inspect it, then evidence identifies the exact Builds commit, package-source audit timestamp, accepted versions, retained exceptions, validation commands/results, and rollback grouping for each family.
8. **Automation ownership.** Given the shared catalog is authoritative, when dependency-update automation is configured, then Builds owns NuGet catalog proposals and consumer repositories do not open competing local-version updates.

**Rationale:** A catalog-wide refresh has ecosystem-wide impact and must remain independently reviewable and reversible from the ownership migration.

### 4.5 Tracker Sequencing

**OLD**

> `3-5-debug-source-references-and-release-package-references: backlog`

**NEW**

> `3-5-shared-package-catalog-and-source-package-reference-modes: backlog`
>
> `3-11-validated-central-package-catalog-refresh: backlog`

**Sequencing note:** Story 3.5 establishes sole ownership and baseline validation. Story 3.11 activates only after Story 3.5 is done. Renaming the Story 3.5 tracker key must preserve identity and history rather than create a duplicate story.

## 5. Implementation Handoff

### Recipients And Responsibilities

- **Product Owner / backlog maintainer:** apply the FR21, Epic 3, Story 3.5, Story 3.11, and sprint-status changes after approval.
- **Architecture owner:** approve the AD-11 single-authority, family-alignment, compatibility-exception, and no-downgrade rules.
- **Hexalith.Builds maintainer / Developer:** add missing IDs, remove local-version guidance from Builds samples/docs, establish update automation, perform the family-grouped catalog refresh, and publish exact validation evidence.
- **EventStore maintainer / Developer:** preserve the existing working-tree TimeProvider removal, remove the remaining local definitions, update governance tests/scripts/docs, and prove package-mode restore/build/test/pack behavior.
- **Affected repository maintainers / Test Architect:** run prescribed representative-consumer validation for changed package families and review exception evidence.

### Implementation Order

1. Apply the approved planning changes and register Story 3.11 as backlog.
2. Implement Story 3.5 in Builds first: add missing IDs and update policy/docs/samples/guards.
3. Complete Story 3.5 in EventStore: remove remaining local versions, update tests/scripts/docs/automation, and validate current shared pins.
4. Audit Story 3.11 candidates against all configured sources and obtain owner evidence for divergent Hexalith families.
5. Upgrade catalog families in reviewable groups, running Builds, EventStore, and affected-consumer checks after each group.
6. Record exact accepted versions, exceptions, results, rollback groups, and the Builds commit consumed by EventStore.

### Success Criteria

- `references/Hexalith.Builds/Props/Directory.Packages.props` is the only source-owned NuGet version catalog in scope.
- EventStore's root `Directory.Packages.props` contains only CPM configuration and shared-catalog imports; it declares no dependency version.
- Every EventStore package ID resolves exactly once from Builds, including `NBomber.Http` and `xunit.v3.extensibility.core`.
- The catalog validator passes and local-version guards fail on reintroduced consumer pins.
- The latest-compatible audit accounts for all 266 evaluated central entries, including unlisted/unresolved packages, aligned families, and explicit exceptions.
- Accepted upgrades pass grouped Builds, EventStore, and affected-consumer validation; no pin is downgraded due to search behavior.
- PRD, architecture, epics, tracker, Builds docs/sample, EventStore docs/scripts/tests, and update automation agree on ownership and policy.
- No tool-manifest version, SDK selector, generated release fixture, or cache evidence is incorrectly migrated into CPM.

### Approval And Routing Record

- Approved by Administrator on 2026-07-18.
- Applied to PRD FR21 and guardrails, Architecture AD-11 and release conventions, Epic 3 Stories 3.5 and 3.11, the compiled Epic 3 context, and sprint status.
- Routed as a moderate correction to the Product Owner / backlog maintainer, architecture owner, Hexalith.Builds maintainer, EventStore maintainer, affected repository maintainers, and Test Architect.
- Story 3.5 remains backlog and owns the authority migration. Story 3.11 remains backlog and cannot activate until Story 3.5 is done.
- No package catalog, source, test, script, product documentation, automation, gitlink, or implementation-story artifact was changed by this course-correction workflow.

## Appendix A - Change Navigation Checklist

### 1. Understand The Trigger And Context

- [N/A] 1.1 No failed implementation story triggered the change; this is a direct stakeholder governance requirement targeting Story 3.5.
- [x] 1.2 Core problem defined: consumer-local package versions compete with the shared catalog, and the catalog also requires a safe freshness policy.
- [x] 1.3 Evidence recorded: evaluated item counts, local/shared version deltas, missing central IDs, failing documentation-version lookup, live NuGet candidate audit, and Tenants family divergence.

### 2. Epic Impact Assessment

- [x] 2.1 Epic 3 remains completable.
- [x] 2.2 Modify Epic 3 by revising Story 3.5 and adding Story 3.11; no new epic.
- [x] 2.3 Remaining epics reviewed; Stories 2.12 and 8.2 inherit the policy without scope changes.
- [x] 2.4 No epic becomes obsolete.
- [x] 2.5 No epic resequencing; explicit Story 3.5 then Story 3.11 order.

### 3. Artifact Conflict And Impact Analysis

- [x] 3.1 PRD FR21 and guardrail 8.1 require clarification.
- [x] 3.2 Architecture AD-11, release convention, and version table require reconciliation.
- [N/A] 3.3 UX is unaffected.
- [x] 3.4 Other artifacts identified: epics, sprint status, Builds catalog/docs/sample/automation, EventStore wrapper/tests/scripts/docs/automation, and explicit non-CPM exceptions.

### 4. Path Forward Evaluation

- [x] 4.1 Direct Adjustment is viable; moderate planning scope, high implementation effort, and high catalog-refresh risk.
- [x] 4.2 Rollback is neither necessary nor useful; no completed product work caused the governance defect.
- [x] 4.3 MVP review is unnecessary; product scope remains unchanged.
- [x] 4.4 Two-story Direct Adjustment selected for isolated ownership and upgrade rollback boundaries.

### 5. Sprint Change Proposal Components

- [x] 5.1 Issue summary completed.
- [x] 5.2 Epic, story, artifact, technical, and automation impacts documented.
- [x] 5.3 Recommended path, risk, effort, and alternatives documented.
- [x] 5.4 MVP impact and ordered implementation plan documented.
- [x] 5.5 Product, architecture, Builds, EventStore, consumer, and test handoffs defined.

### 6. Final Review And Handoff

- [x] 6.1 Internal consistency review completed for the proposed state.
- [x] 6.2 Proposal is specific, actionable, and linked to evidence.
- [x] 6.3 Administrator approved the complete proposal on 2026-07-18.
- [x] 6.4 PRD, architecture, epics, and sprint tracker reconciled to the approved state.
- [x] 6.5 Moderate-scope implementation handoff documented for Product Owner, architecture, Builds, EventStore, affected consumer, and test owners.
