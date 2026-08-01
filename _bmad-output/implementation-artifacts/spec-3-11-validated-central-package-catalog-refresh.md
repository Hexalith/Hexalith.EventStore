---
title: 'Validated Central Package Catalog Refresh'
type: 'feature'
created: '2026-07-31'
status: 'done'
baseline_revision: '9b9c776791c149cab26c795a476d23d3d11f7796'
baseline_commit: '9b9c776791c149cab26c795a476d23d3d11f7796'
final_revision: 'caef47fcff54ade19f50cf752c25aeb74e639afa'
review_loop_iteration: 0
followup_review_recommended: true
operator_approvals:
  - role: 'Hexalith.Builds maintainer'
    approved_by: 'Administrator'
    approved_at: '2026-08-01'
    approved_commit: '9dc0fe1ffbf33269fddf195fd12317def86728f0'
    decision: 'approved'
  - role: 'EventStore maintainer'
    approved_by: 'Administrator'
    approved_at: '2026-08-01'
    approved_commit: 'caef47fcff54ade19f50cf752c25aeb74e639afa'
    decision: 'approved'
context:
  - '_bmad-output/project-context.md'
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
warnings: [oversized]
deferred: []
---

<intent-contract>

## Intent

**Problem:** The shared Builds catalog is structurally authoritative, but it has no reproducible freshness audit that proves all 284 evaluated package rows were considered, captures unresolved or unlisted versions, and records compatibility decisions for coupled families. A recent broad bump therefore does not by itself prove that the catalog is the latest validated compatible set.

**Approach:** Add a source-aware, deterministic audit and disposition contract in Hexalith.Builds; audit the live evaluated catalog; apply only evidence-backed updates in rollback-safe families; then validate Builds, EventStore package mode, and representative consumers while recording exact revisions and retained exceptions.

## Boundaries & Constraints

**Always:** Treat `references/Hexalith.Builds/Props/Directory.Packages.props` as the sole NuGet version authority; inventory the evaluated catalog rather than a historical row count; record every package ID, current version, latest stable and applicable prerelease candidate, source/listing state, family, disposition, UTC audit time, and evidence; keep coupled families coherent; prefer the latest validated stable release; retain current pins when search is older, missing, unlisted, incompatible, or unresolved; keep NuGet audit enabled; commit Builds-owned work before the EventStore gitlink and evidence.

**Block If:** A candidate would require an unsupported framework/SDK transition and no safe retained version can be evidenced, or repository state changes outside this story make a reviewable family rollback impossible. Missing maintainer approval is an operator handoff after all agent-capable work, never a blocked outcome.

**Never:** Guess versions, downgrade a current pin from incomplete search results, move SDK/tool/fixture exceptions into CPM, change the Tenants family without release-owner evidence, move `Microsoft.OpenApi` to 3.x without ASP.NET Core 10 runtime proof, add consumer NuGet Dependabot ownership, prune catalog rows from EventStore-only usage, initialize nested submodules, or publish packages.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Complete audit | Evaluated catalog and configured NuGet sources | One evidence entry per unique evaluated row with candidates, source state, family, disposition, timestamp, and no silent omissions | Fail closed on duplicate/missing evidence |
| Missing or unlisted package | Current version is absent, unlisted, or a source cannot resolve it | Retain the current pin and record source diagnostics plus a removal/recheck trigger | Never infer a downgrade or omit the row |
| Coupled family update | Compatible candidates exist for Aspire, Dapr, .NET, OTel, Roslyn, identity, test, or Hexalith packages | Accept or retain the family as one reviewable rollback group with compatibility evidence | Reject partial/misaligned family dispositions |
| Deliberate exception | OpenAPI 2.x, SourceLink, prerelease channel, SDK/tool pin, or Tenants approval rule applies | Preserve the pin/channel and record rationale, evidence, and concrete removal trigger | Fail validation when exception evidence is incomplete |

</intent-contract>

## Code Map

- `references/Hexalith.Builds/Props/Directory.Packages.props` -- authoritative 284-row evaluated catalog; shared Hexalith properties and external coupled families are the only package-pin edit surface.
- `references/Hexalith.Builds/Tools/validate-central-package-versions.ps1` and `test-central-package-version-validator.ps1` -- reuse effective MSBuild evaluation, identity, and NuGet-version guards; they do not currently prove freshness.
- `references/Hexalith.Builds/Tools/test-authoritative-package-catalog.ps1` -- preserve required IDs, property-backed family alignment, and disabled consumer overrides.
- `references/Hexalith.Builds/Tools/package-version-exceptions.json` and `validate-package-version-exceptions.ps1` -- read-only closed inventory for non-CPM SDK/tool pins unless an accepted Aspire family update requires exact synchronized edits.
- `references/Hexalith.Builds/Tools/validate-dapr-package-versions.ps1` -- existing Dapr family-alignment gate to retain and run.
- `references/Hexalith.Builds/.github/workflows/ci.yml` and `build-release.yml` -- run audit-contract validation before release; do not make network freshness a nondeterministic release gate.
- `references/Hexalith.Builds/.github/dependabot.yml` -- Builds remains the only NuGet proposal owner.
- `references/Hexalith.Builds/Tools/README.md` -- document audit generation, offline validation, dispositions, and rollback evidence.
- `Directory.Packages.props`, `Directory.Build.props`, and `nuget.config` -- read-only EventStore import/source/audit contract; no local versions.
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/` -- reuse package authority, effective evaluation, dependency-mode, and release-manifest gates.
- `scripts/check-doc-versions.sh` and `docs/reference/nuget-packages.md` -- verify catalog-derived documentation; refresh stale accepted version text only.
- `_bmad-output/planning-artifacts/architecture.md` -- update `## Stack` version rows only from accepted audit evidence.
- `_bmad-output/implementation-artifacts/3-11-central-package-audit.json` -- checked-in complete audit/disposition evidence, exact source timestamp, family rollback groups, validation results, and Builds revision.

## Tasks & Acceptance

**Execution:**
- `references/Hexalith.Builds/Tools/audit-central-package-versions.ps1` -- add live source discovery and complete catalog audit output with fail-closed resolution semantics.
- `references/Hexalith.Builds/Tools/validate-package-version-audit.ps1` and `test-package-version-audit-validator.ps1` -- add deterministic schema/catalog/family/exception validation and positive/negative fixtures.
- `references/Hexalith.Builds/Props/Directory.Packages.props` -- apply accepted candidates by coupled rollback group; retain and annotate evidence-backed exceptions.
- `references/Hexalith.Builds/Tools/README.md` and `.github/workflows/build-release.yml` -- document generation and gate the checked-in audit contract before release.
- `_bmad-output/implementation-artifacts/3-11-central-package-audit.json` -- generate the exhaustive audit, dispositions, validation evidence, and exact Builds commit.
- `_bmad-output/planning-artifacts/architecture.md` and `docs/reference/nuget-packages.md` -- synchronize accepted version snapshots without broad documentation rewrites.
- `_bmad-output/implementation-artifacts/spec-3-11-validated-central-package-catalog-refresh.md` -- record task completion, commands/results, commit IDs, review outcome, and operator-only approvals.

**Acceptance Criteria:**
- Given the evaluated Builds catalog and effective configured sources, when the audit runs, then every unique catalog row has a timestamped stable/prerelease/listing result or explicit unresolved evidence and disposition.
- Given an older, absent, or unlisted search result, when dispositions are generated, then the current version is retained without downgrade and the reason and recheck trigger are recorded.
- Given a coupled family or prerelease/major transition, when a candidate is accepted, then all coupled rows, SDK exceptions where applicable, compatibility proof, representative consumers, and one rollback group agree.
- Given retained OpenAPI, SourceLink, Tenants, prerelease, or non-CPM exceptions, when offline validation runs, then each has rationale, evidence, and a concrete removal trigger and no exception is silently normalized into CPM.
- Given the accepted catalog groups, when Builds and EventStore validation runs from fresh package-mode restores, then all structural validators, Builds projects/tests, focused EventStore tests, release-pack checks, and documentation-version checks pass with NuGet audit enabled.
- Given completed agent-capable work, when the story is handed off, then the evidence names exact Builds/EventStore revisions, sources and UTC audit time, accepted and retained groups, commands/results, representative consumers, rollback boundaries, and any required maintainer approvals under `operator_actions`.

## Spec Change Log

- 2026-07-31 -- Created the executable catalog-refresh contract from the approved Story 3.11 scope.
- 2026-07-31 -- Implemented the complete live audit, offline validation, accepted rollback groups, retained exceptions, consumer evidence, and four-layer review hardening.
- 2026-07-31 -- Finalized the agent-complete implementation at `caef47fcff54ade19f50cf752c25aeb74e639afa` for the two required maintainer approvals.
- 2026-08-01 -- Administrator approved the exact Hexalith.Builds and EventStore implementation revisions; Story 3.11 moved to `done`.

## Review Triage Log

### 2026-07-31 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 8: (high 3, medium 4, low 1)
- defer: 0
- reject: 4: (high 0, medium 2, low 2)
- addressed_findings:
  - `[high]` `[patch]` Audit acceptance could disagree with per-source resolution or per-package source results. The offline validator now reconciles source aggregates and results and permits `accepted` only when every configured source returned listed candidate evidence.
  - `[high]` `[patch]` Identity and bUnit compatibility boundaries were too narrow. `Microsoft.Identity.Web` now shares one rollback group with all seven IdentityModel/JWT rows, and AngleSharp shares one rollback group with bUnit; every member must receive one coherent disposition.
  - `[high]` `[patch]` The live generator had no deterministic regression suite. Added 14 fixture-driven scenarios and wired them into CI and release validation, including paging, huge prerelease identifiers, missing/unlisted packages, unresolved secondary sources, output collisions, and offline-validation compatibility.
  - `[medium]` `[patch]` Audit metadata admitted ambiguous or stale-looking evidence. Validation now requires a non-empty evaluated catalog, a full revision, zero-offset UTC time, the evaluated relative catalog path, reconciled counts, and no orphan families.
  - `[medium]` `[patch]` Generator source and version handling was brittle. Source discovery is repository-anchored; response URIs are validated; prerelease numeric identifiers use arbitrary precision; output/catalog collisions fail closed; and family mapping is package-ID based.
  - `[medium]` `[patch]` Negative fixtures did not pin per-source reconciliation, metadata invariants, rollback-group dispositions, or exact workflow commands. Expanded the validator suite to 18 scenarios and asserted the checked-in workflow wiring.
  - `[medium]` `[patch]` Azure Monitor 1.6.0 had no checked-out representative consumer, while the StackExchange.Redis 3.1.0 two-instance proof failed before readiness. Both candidates were withdrawn and their existing pins retained with concrete recheck triggers instead of overstating compatibility.
  - `[low]` `[patch]` Root evidence summarized commands and documentation retained stale accepted-version snapshots. Recorded exact commands/results and synchronized the Architecture and NuGet package reference rows to the accepted catalog.

## Design Notes

The live audit may use network sources, but CI validates a checked-in evidence contract deterministically against the evaluated catalog. This separates time-varying discovery from the release gate while making omissions, stale evidence, source ambiguity, and family splits reviewable.

## Verification

**Commands:**
- `pwsh -NoProfile -File Tools/test-package-version-audit-validator.ps1` (Builds) -- expected: positive and fail-closed fixture scenarios pass.
- `pwsh -NoProfile -File Tools/validate-central-package-versions.ps1` plus authoritative, consumer, exception, Dapr, and audit validators/tests (Builds) -- expected: all package-governance gates pass.
- `dotnet restore Hexalith.Builds.slnx && dotnet build Hexalith.Builds.slnx --configuration Release` (Builds), then each Builds test project individually -- expected: clean restore/build/tests.
- `dotnet restore Hexalith.EventStore.slnx -p:UseHexalithProjectReferences=false && dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -p:UseHexalithProjectReferences=false` -- expected: fresh package-mode build passes.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Release` and release-manifest validation -- expected: focused package-governance and pack boundary pass.
- `bash scripts/check-doc-versions.sh` -- expected: catalog-backed documentation values and multiplicities pass.

## Auto Run Result

Status: done

Summary: Story 3.11 now provides a source-aware live generator and deterministic offline contract for every one of the 284 evaluated catalog rows. The validated catalog accepts 13 rows in five rollback groups, retains 271 rows with explicit evidence, records eight feed-missing IDs without downgrade, and proves the selected state in Builds plus EventStore package mode. Review hardening also withdrew two candidates whose representative compatibility evidence was insufficient.

Files changed:
- `references/Hexalith.Builds` -- advances the gitlink to the committed catalog, audit generator, offline validator, deterministic fixture suites, workflow gates, and operating documentation.
- `_bmad-output/implementation-artifacts/3-11-central-package-audit.json` -- records the content-addressed audit, exact Builds revision, accepted groups, retained exceptions, consumer evidence, rollback instructions, and command results.
- `_bmad-output/planning-artifacts/architecture.md` and `docs/reference/nuget-packages.md` -- synchronize accepted CommunityToolkit Dapr and Aspire/Dapr snapshots.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- hands the completed agent-capable work to the required maintainers.

Review findings: 8 patches applied (high 3, medium 4, low 1); 0 deferred; 4 rejected as credential-provider expansion, semantic judging of free-form rationale, live-network revision freshness, or a permanent EventStore-side duplicate validator outside this story's deterministic handoff contract.

Follow-up review recommendation: `true`; three high-severity patches were applied. Patched counts were high 3, medium 4, low 1, for a weighted score of 13.

Verification performed:
- Hexalith.Builds package governance -- central catalog 284 entries; authoritative catalog 49 identities and 3 shared versions; audit 284 packages, 139 families, 1 source; generator fixtures 14/14; audit-validator fixtures 18/18; consumer-authority fixtures 16/16; exception fixtures 7/7; Dapr fixtures 29/29.
- `dotnet restore Hexalith.Builds.slnx` and warning-as-error Release build -- passed with 0 warnings and 0 errors; Builds tests passed 106/106, 24/24, and 1/1.
- Fresh EventStore package-mode restore with NuGet audit enabled and warning-as-error Release build -- passed with 0 warnings and 0 errors.
- EventStore focused suites -- Contracts 878/878; SignalR 44/44; Admin UI 841/841; AppHost 63/63; REST generators 124/124.
- Full EventStore Server suite -- 2,870 passed, 25 skipped, 0 failed (2,895 total).
- `bash scripts/check-doc-versions.sh` -- 4 Dapr rows verified at 1.18.5.
- Root evidence reconciliation -- JSON valid; audit SHA-256 `507496549651a66f17dac221b2632b5ff9c5f4eb40055fbfeafcfd3c93e9bffa`; 284 packages; 139 families; exact Builds revision `9dc0fe1ffbf33269fddf195fd12317def86728f0`.

Residual risks: eight packages remain absent from the configured feed and are retained without downgrade; source metadata remains time-varying outside deterministic CI; Azure Monitor and StackExchange.Redis candidates remain intentionally unaccepted until their stated consumer/runtime triggers pass. Administrator approved the exact Builds and EventStore implementation revisions on 2026-08-01.
