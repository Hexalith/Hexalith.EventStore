# Sprint Change Proposal - Latest-Compatible Dependencies And Root Submodules

Date: 2026-08-20
Project: eventstore
Planning mode: Batch
Scope classification: Moderate
Recommended path: Direct Adjustment
Approval: Approved by Administrator on 2026-08-20

## 1. Issue Summary

The requested course correction is to use current NuGet package versions and the latest revisions of the root-declared Git submodules.

The governing interpretation of “latest” is the existing PRD Section 8.1 and Architecture AD-11 contract: use the latest source-resolved version that is validated as compatible; prefer stable releases for stable pins; preserve intentional prerelease channels; move coupled families together; and never downgrade or guess when a configured source is incomplete. Absolute version recency is not sufficient evidence for a breaking or framework-coupled transition.

Evidence captured on 2026-08-20:

- A live audit of references/Hexalith.Builds/Props/Directory.Packages.props queried the sole configured source, https://api.nuget.org/v3/index.json, at 2026-08-20T18:29:11.3227171Z.
- The evaluated catalog contains 284 package rows.
- Forty-three stable pins have a newer listed stable candidate.
- Four intentional prerelease pins have a newer listed prerelease candidate in their selected channel.
- The 47 candidate rows form 15 audit rollback families.
- Seven package IDs remain unresolved on the configured source and therefore cannot be advanced or downgraded from this evidence.
- The remaining 230 rows have no newer applicable candidate in the live result.
- The temporary audit evidence SHA-256 is 164a17e6cc860d01b113873deb4fb2ec0c0ee9660d0257e9d0ff8891527d8405.
- Direct upstream HEAD resolution at 2026-08-20T18:39:00Z confirmed that every checked-out root submodule is already at its then-current upstream main HEAD.
- Five parent gitlinks already match those upstream revisions. The working tree already carries the two required parent advances: Hexalith.Builds from 145ab857a50dc6cf22220723604badb28d78cdbc to eadddc7b5d8e9392e5931758ffb608b57b5fdc6c, and Hexalith.Tenants from d3f74f58493761c306063304ace553c1e7e4e85b to 87dba99daf481edc462999f86b91bcb6867f5c66.

The repository also contains unrelated in-progress Story 1.21 changes, including modified sprint tracking and evidence. Those changes, plus the already-present Builds and Tenants gitlink advances, must be preserved and reconciled rather than replaced.

## 2. Impact Analysis

### Epic and story impact

- Epic 3 is the natural owner because it governs reproducible dependencies, package authority, and root submodule layout through FR19, FR21, NFR9, and AD-11.
- Completed Story 3.11 remains an immutable, dated catalog-refresh packet. It must not be reopened or have its bound audit, revisions, approvals, or validation claims rewritten.
- Add Story 3.16, “Latest-Compatible Dependency And Root Submodule Refresh,” as a named follow-up to Story 3.11.
- Stories 3.13 through 3.15 retain their existing release-evidence identities. Story 3.16 must not splice new catalog or gitlink revisions into those frozen packets.
- No new epic is required. Epics 1, 2, and 4 through 8 keep their requirements and sequencing; their representative consumers provide regression coverage only.

### Artifact impact

- PRD: no requirement change. Section 8.1, FR19, FR21, and NFR9 already prescribe the requested outcome and safety constraints.
- Architecture: AD-11 through AD-13 remain valid. The Stack preamble must recognize follow-up refresh stories, and dated version rows must be updated only after candidate families pass.
- UX: no interaction or design requirement changes. Fluent UI and FrontComposer consumers require regression validation, not a UX rewrite.
- Epics: add Story 3.16 and replace the stale “Epic 3 story set confirmed complete” comment.
- Sprint status: add Story 3.16 as backlog without overwriting the current Story 1.21 edits or changing Stories 3.13 through 3.15.
- Implementation evidence: create a new Story 3.16 specification and audit packet. Do not mutate Story 3.11 evidence.

### Technical impact

- Package versions remain owned only by references/Hexalith.Builds/Props/Directory.Packages.props.
- The checked-in Builds audit, family decisions, package-version exception inventory, and generated documentation snapshots must agree with the accepted catalog.
- The Aspire family transition also requires the non-CPM Aspire.AppHost.Sdk exception inventory and every in-scope actual AppHost SDK pin to remain exactly aligned.
- Major or framework-coupled candidates require code and runtime proof. The highest-risk transitions are Microsoft.OpenApi 2.12.0 to 3.10.0, xUnit v3 packages to 4.0.0, and the Roslyn family from 5.6.0 to 5.9.0.
- Builds, EventStore, FrontComposer, Memories, and Tenants are representative consumers for the affected source/package, AppHost, generator, test, and UI families available in this workspace.
- Only the seven paths declared by the root .gitmodules are in submodule scope. Nested submodules remain excluded.
- Advancing the Builds catalog creates a new Builds-owned revision. That revision must be validated in Builds first and must be reachable from its upstream before a later parent commit may record the final gitlink.

## 3. Recommended Approach

Use Direct Adjustment by adding and executing Story 3.16.

This route preserves MVP scope and all completed evidence while providing one reviewable maintenance unit. Rollback remains available per package family and per parent gitlink, but rolling back the product or earlier stories is unnecessary. An MVP scope review is not warranted because no feature requirement changes.

Effort: Medium to high. Most candidates are mechanical only after validation, but OpenAPI, xUnit, Roslyn, Aspire, and Fluent UI may require source or test adaptations.

Risk: Medium. Family-level branches, package-mode validation, and independent gitlink groups bound the blast radius. Risk becomes high if major candidates are applied as isolated row edits or if shared-catalog consumers are not validated.

Timeline impact: one focused Epic 3 maintenance story. It may proceed independently of the evidence-only Story 3.15 work only when working-tree ownership is isolated and neither story rewrites the other’s artifacts.

## 4. Detailed Change Proposals

### 4.1 Epic 3 story addition

Artifact: _bmad-output/planning-artifacts/epics.md

OLD:

    <!-- Epic 3 story set confirmed complete. -->

NEW:

    ### Story 3.16: Latest-Compatible Dependency And Root Submodule Refresh

    As a platform maintainer,
    I want the shared NuGet catalog and root-declared submodule revisions refreshed from authoritative upstream evidence,
    So that current development uses the latest compatible dependency set without weakening reproducibility or overwriting in-flight work.

    Requirements coverage: Primary maintenance ownership of FR19 and FR21; supporting NFR9, NFR11, and NFR12.

    Architecture constraints: AD-11 through AD-13. Builds remains the sole NuGet version authority; stable, prerelease, framework-coupled, and major families move only with compatible evidence; root gitlinks use exact reachable commits; nested submodules are excluded.

    Dependencies: Completed Story 3.11 supplies the audit and validation contract but remains immutable. Existing Story 3.13 through 3.15 evidence remains bound to its original identities. Current unrelated Story 1.21 work must be preserved.

    Current reconciliation: The 2026-08-20 live audit evaluated 284 rows, identified 43 stable-pin and four prerelease-channel candidates across 15 audit families, and left seven source-unresolved IDs retained. All seven checked-out root submodules matched then-current upstream main; only the parent Builds and Tenants gitlinks differed, and those advances were already present in the working tree.

    Acceptance Criteria:

    Given Story 3.16 implementation begins
    When repository and source preflight runs
    Then it records the current EventStore branch, status, remotes, recent history, exact parent gitlinks, each root submodule status/revision/upstream main HEAD, configured NuGet sources, catalog revision, and unrelated modified paths
    And it preserves every pre-existing change, performs no nested initialization or update, and stops before overwriting or absorbing another story’s work.

    Given the Builds catalog is audited
    When live NuGet V3 registration and flat-container evidence is collected
    Then every evaluated package row records current version, latest listed stable and prerelease candidates, listing state, source result, family, disposition, rollback group, rationale, evidence, and removal trigger
    And missing, unlisted, or unresolved results never cause a guessed version, downgrade, omitted row, or false latest claim.

    Given a stable pin has a newer stable candidate
    When selection is proposed
    Then the latest listed stable candidate is tested as the default
    And any retained older version is accepted only as the latest validated compatible version with exact incompatibility evidence, an accountable owner, and a concrete recheck trigger.

    Given an intentional prerelease pin has a newer prerelease candidate
    When selection is proposed
    Then it advances within the intentional channel as one compatible family
    And it neither falls back to an older stable version nor crosses to another channel without explicit architecture and consumer evidence.

    Given a family is coupled by SDK, runtime, compiler, adapter, UI, or test-host behavior
    When any member changes
    Then all required family rows and non-CPM exceptions align in one rollback-safe unit and representative consumers pass
    And partial family upgrades, mixed AppHost SDK/package versions, or isolated major bumps are rejected.

    Given Microsoft.OpenApi 3.x, xUnit 4.x, Roslyn 5.9, Aspire 13.5, or another major/framework-coupled candidate is considered
    When compatibility validation runs
    Then compile-time, runtime, generated-output, discovery/execution, package-mode, and public-surface effects applicable to that family are proved and required source adaptations are included
    And version recency alone cannot override the existing ASP.NET Core OpenAPI, compiler-host, test-adapter, or AppHost contracts.

    Given the accepted catalog is written
    When Builds governance runs
    Then central-version, authoritative-catalog, exception, Dapr, live-audit schema, offline-audit, family, and consumer-authority validators pass and the checked-in audit binds the exact Builds revision and validation results
    And no PackageReference version is added to EventStore or another consumer project.

    Given EventStore and in-scope root consumers evaluate the accepted catalog
    When Debug/source and Release/package modes restore, build, test, generate, pack, and inspect dependency graphs
    Then affected AppHost, Server, Contracts, generator, Admin UI, FrontComposer, Tenants, Memories, and release-package boundaries pass with warnings as errors and NuGet audit enabled
    And a source-only success, stale assets file, skipped test lane, or one representative project cannot establish compatibility.

    Given root submodule revisions are refreshed
    When authoritative upstream main HEADs are resolved again immediately before application
    Then each of the seven root gitlinks is either already equal or advances to the exact validated reachable revision, with Builds pointing to the accepted catalog commit
    And no nested submodule, unrelated gitlink, detached unpushed commit, recursive update, remote-tracking guess, or local content change is silently included.

    Given package or gitlink validation fails
    When rollback is required
    Then only the affected package family or gitlink group is reverted to its recorded before identity and validation is rerun
    And frozen release/evidence packets, unrelated working-tree changes, and other accepted groups remain untouched.

    Given Story 3.16 completion is requested
    When final evidence is assembled
    Then it binds exact before/after catalog rows, retained exceptions, configured-source results and UTC time, Builds/EventStore/submodule SHAs, commands/results, package and gitlink rollback groups, documentation snapshots, limitations, and named Builds/EventStore maintainer approvals
    And it performs or implies no NuGet publication, deployment, nested-submodule action, commit, push, merge, or rewrite of Story 3.11 or Story 3.13 through 3.15 evidence without separate authority.

    <!-- Epic 3 story set includes the approved Story 3.16 maintenance follow-up. -->

Rationale: Story 3.11 explicitly requires a named follow-up for catalog drift after its frozen audit. The new story keeps current dependency work evidence-bound without falsifying the completed packet.

### 4.2 Sprint status addition

Artifact: _bmad-output/implementation-artifacts/sprint-status.yaml

OLD:

    3-15-corrected-deployed-runtime-parity-closure: backlog
    epic-3-retrospective: optional

NEW:

    3-15-corrected-deployed-runtime-parity-closure: backlog
    # Approved follow-up to the frozen Story 3.11 audit; current catalog and root gitlinks
    # are re-evaluated from authoritative upstream evidence without rewriting prior packets.
    3-16-latest-compatible-dependency-and-root-submodule-refresh: backlog
    epic-3-retrospective: optional

Rationale: Epic 3 is already in progress. Adding the story is sufficient; existing statuses and the unrelated in-progress Story 1.21 edits must remain intact.

### 4.3 Architecture Stack authority and dated values

Artifact: _bmad-output/planning-artifacts/architecture.md

OLD:

    The table is a dated rendering of the current planning baseline; the Builds catalog remains live version authority and always wins. Story 3.11 updates version rows only from accepted shared-catalog and compatibility evidence.

NEW:

    The table is a dated rendering of the current planning baseline; the Builds catalog remains live version authority and always wins. Story 3.11 established the validated refresh contract; approved follow-ups such as Story 3.16 update version rows only from accepted shared-catalog and compatibility evidence.

After Story 3.16 validation, update only rows whose families are accepted. The 2026-08-20 candidates for existing Stack rows are:

| Stack row | Current | Candidate |
| --- | --- | --- |
| Aspire.Hosting | 13.4.6 | 13.5.0 |
| Aspire.Hosting.Keycloak / Kubernetes | 13.4.6-preview.1.26319.6 | 13.5.0-preview.1.26417.10 |
| Microsoft.CodeAnalysis packages | 5.6.0 | 5.9.0 |
| Microsoft.FluentUI.AspNetCore.Components | 5.0.0-rc.4-26180.1 | 5.0.0-rc.5-26219.1 |
| xUnit | 3.2.2, with runner 3.1.5 | 4.0.0 family |

Rationale: The architecture snapshot follows accepted evidence; it must not predeclare candidates as compatible.

### 4.4 PRD and UX disposition

PRD change: none. FR19, FR21, NFR9, Section 8.1, and the existing constraints already require root-declared submodule consistency and the latest validated compatible central package set.

UX change: none. The Fluent UI prerelease candidate and FrontComposer consumers require functional, accessibility, localization, and browser regression checks, but no interaction contract changes unless implementation evidence exposes an actual behavior change.

### 4.5 NuGet candidate baseline

The following table is discovery input, not pre-accepted compatibility evidence:

| Audit family | Rows | Current | Latest candidate | Required gate |
| --- | ---: | --- | --- | --- |
| aspire | 11 stable rows | 13.4.6 | 13.5.0 | AppHost SDK exception alignment, model/startup, source/package consumers |
| aspire | 2 prerelease rows | 13.4.6-preview.1.26319.6 | 13.5.0-preview.1.26417.10 | Preserve preview channel and align with Aspire 13.5 family |
| fluent-ui | 2 prerelease rows | 5.0.0-rc.4-26180.1 | 5.0.0-rc.5-26219.1 | FrontComposer/Admin UI compile, browser, accessibility, localization |
| hexalith-eventstore | 13 rows | 3.95.0 | 3.96.2 | Exact published package set, package-only consumers, self-version coherence |
| package:fscheck.xunit.v3 | 1 | 3.3.4 | 3.4.0 | Property-test discovery and execution |
| package:google.apis | 1 | 1.75.0 | 1.76.0 | Compile/runtime consumer |
| package:google.apis.auth.aspnetcore3 | 1 | 1.75.0 | 1.76.0 | Authentication host/runtime consumer |
| package:microsoft.openapi | 1 | 2.12.0 | 3.10.0 | ASP.NET Core 10 runtime document generation and API migration |
| package:microsoft.semantickernel | 1 | 1.79.0 | 1.80.0 | Compile/runtime consumer |
| package:microsoft.typescript.msbuild | 1 | 7.0.0 | 7.0.1 | Front-end build output |
| package:nbomber | 1 | 6.5.0 | 6.6.0 | Benchmark/performance build and execution |
| package:radzen.blazor | 1 | 11.2.5 | 11.2.6 | UI build and focused rendering |
| package:roslynator.analyzers | 1 | 4.16.0 | 4.16.1 | Warning-as-error build |
| package:roslynator.formatting.analyzers | 1 | 4.16.0 | 4.16.1 | Warning-as-error build |
| roslyn | 5 | 5.6.0 | 5.9.0 | Compiler host, generators, public API and generated-output regression |
| xunit | 4 | runner 3.1.5; libraries 3.2.2 | 4.0.0 | Test discovery, adapters, traits, all required lanes |

The seven source-unresolved pins to retain unless a configured source later resolves them are:

- Hexalith.Chatbot.Contracts 1.80.0
- Hexalith.Parties.Server 1.0.0
- Hexalith.Parties.ServiceDefaults 1.0.0
- Hexalith.Parties.UI 1.0.0
- Hexalith.Tenants.UI 5.4.1
- Microsoft.Extensions.Identity.Http 10.0.9
- Serilog.Sinks.Browser 8.0.0

### 4.6 Root submodule baseline

| Root submodule | Parent pin before | Upstream main observed | Proposed disposition |
| --- | --- | --- | --- |
| Hexalith.AI.Tools | de38f78ef7672df2a0997ddc60bf35ba0d02fa25 | de38f78ef7672df2a0997ddc60bf35ba0d02fa25 | Retain |
| Hexalith.Builds | 145ab857a50dc6cf22220723604badb28d78cdbc | eadddc7b5d8e9392e5931758ffb608b57b5fdc6c | Preserve current local advance; later point to the validated Story 3.16 Builds commit |
| Hexalith.Commons | 6fbac0c5dff2b8a58e90732c51b31911421a8a65 | 6fbac0c5dff2b8a58e90732c51b31911421a8a65 | Retain |
| Hexalith.FrontComposer | 7a337a21d4ba261bf27aeb3feedde47789f0160a | 7a337a21d4ba261bf27aeb3feedde47789f0160a | Retain unless upstream advances before execution |
| Hexalith.Memories | 003fd21488d60307cd932a3139f69319a25cea66 | 003fd21488d60307cd932a3139f69319a25cea66 | Retain |
| Hexalith.PolymorphicSerializations | cd7d8d06cffe3942d358b54664445926c4c98fa4 | cd7d8d06cffe3942d358b54664445926c4c98fa4 | Retain |
| Hexalith.Tenants | d3f74f58493761c306063304ace553c1e7e4e85b | 87dba99daf481edc462999f86b91bcb6867f5c66 | Preserve current local advance |

All upstream identities must be resolved again immediately before implementation because remote HEADs are time-varying.

## 5. Change Analysis Checklist

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering change | [x] | Administrator requested current NuGet packages and latest root submodules. |
| 1.2 Core problem | [x] | Dependency and upstream revision drift after the frozen Story 3.11 audit. |
| 1.3 Evidence | [x] | Live 284-row NuGet V3 audit plus direct upstream HEAD resolution for all seven root submodules. |
| 2.1 Current epic impact | [x] | Add one Epic 3 maintenance story; no new epic. |
| 2.2 Story impact | [x] | Story 3.11 remains immutable; add Story 3.16. |
| 2.3 Future epic impact | [x] | No requirement changes; affected consumers supply regression evidence. |
| 2.4 Epic validity | [x] | Epic 3 remains valid and already in progress. |
| 2.5 Sequence | [x] | Story 3.16 is independent of 3.15 only with artifact/worktree isolation. |
| 3.1 PRD conflict | [x] | No conflict or edit; existing latest-compatible rules govern. |
| 3.2 Architecture conflict | [x] | No decision change; dated Stack authority wording and accepted rows need refresh. |
| 3.3 UX conflict | [N/A] | No UX contract change; UI family regression remains required. |
| 3.4 Other artifacts | [x] | Epics, sprint status, Story 3.16 spec/audit, Builds catalog/evidence, docs, and root gitlinks. |
| 4.1 Direct adjustment | [x] | Recommended; bounded by family and gitlink rollback groups. |
| 4.2 Rollback | [x] | Contingency only, per failed family or gitlink group. |
| 4.3 MVP review | [N/A] | Product scope is unchanged. |
| 4.4 Recommended path | [x] | Direct Adjustment. |
| 5.1 Issue summary | [x] | Trigger, policy interpretation, audit, gitlinks, and dirty-worktree constraint recorded. |
| 5.2 Impact summary | [x] | Epic, artifact, technical, consumer, and evidence impacts recorded. |
| 5.3 Approach | [x] | Effort, risk, sequence, and rollback posture recorded. |
| 5.4 Detailed proposals | [x] | Exact story/status/architecture edits and candidate baselines supplied. |
| 5.5 Handoff | [x] | Owners, sequence, and success criteria supplied below. |
| 6.1 Proposal coherence | [x] | Preserves completed evidence and central authority. |
| 6.2 Proposal accuracy | [x] | Bound to 2026-08-20 source and upstream observations; requires execution-time refresh. |
| 6.3 Approval | [x] | Administrator continued review and explicitly approved implementation on 2026-08-20. |
| 6.4 Sprint status | [!] | Proposed only; no sprint-status mutation performed. |
| 6.5 Handoff | [x] | Moderate change routed to Product Owner, Builds maintainer, EventStore Developer, and root maintainer. |

## 6. Implementation Handoff

Primary routing:

- Product Owner: accept Story 3.16 and its sprint placement.
- Hexalith.Builds maintainer: own catalog, audit, exception inventory, governance validation, and Builds revision.
- EventStore Developer: adapt and validate affected EventStore consumers in source and package modes.
- Root repository maintainer: reconcile the existing Builds/Tenants pointer changes, re-resolve all seven upstream HEADs, and own final root gitlink evidence.
- Architect: accept any new retained major-version exception or architecture change exposed by compatibility evidence.

Execution order:

1. Reconcile ownership of the current dirty EventStore tree and preserve Story 1.21 changes.
2. Re-run the live NuGet audit and root-submodule upstream resolution.
3. Apply and validate candidates one rollback family at a time in Hexalith.Builds.
4. Align non-CPM Aspire SDK exceptions and validate available root consumers.
5. Record accepted and retained dispositions in a new Story 3.16 audit packet.
6. Validate EventStore source/package modes, affected focused suites, release-package boundaries, and documentation.
7. Advance only root-declared gitlinks to validated reachable revisions; do not touch nested submodules.
8. Update Story 3.16 planning/status and dated architecture/package documentation from accepted evidence.

Success criteria:

- Every one of the 284 evaluated rows has a current, source-backed disposition.
- Every newer source-resolved candidate is either accepted and validated or retained as the latest validated compatible version with exact evidence and a removal trigger.
- All 47 current candidates are handled in coherent rollback groups; the seven unresolved IDs are not guessed or downgraded.
- Builds governance and representative source/package consumers pass with warnings as errors and NuGet audit enabled.
- All seven parent gitlinks equal their execution-time validated targets, with no nested or unrelated submodule mutation.
- Existing Story 1.21 work and frozen Story 3.11/3.13-3.15 evidence remain byte-for-byte outside the explicitly approved edits.
- No package publication, deployment, commit, push, merge, or nested-submodule update occurs without its separate authority.

## 7. Workflow Execution Log

- 2026-08-20: Administrator requested a course correction for current NuGet packages and latest root-declared submodules.
- 2026-08-20: Batch review mode selected.
- 2026-08-20: PRD, epics, architecture, UX, project context, repository guidance, live NuGet evidence, and root-submodule upstream identities reviewed.
- 2026-08-20: Moderate Direct Adjustment proposal generated and presented.
- 2026-08-20: Administrator selected Continue and explicitly approved the complete proposal for implementation.
- 2026-08-20: Approved proposal routed to Product Owner and Developer, with Hexalith.Builds and root-repository maintainer responsibilities identified.
- Implementation status: not started by this workflow; proposed artifact, catalog, source, audit, and gitlink changes remain handoff deliverables.
