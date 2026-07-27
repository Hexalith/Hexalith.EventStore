---
created: 2026-07-27
baseline_commit: 73589770b14888b703d78d37325b066befa0689c
story_id: "2.12"
story_key: 2-12-tenants-runtime-identity-adoption-and-package-mode-validation
status: ready-for-dev
package_lane_status: re-scoped
package_lane_prerequisite_receipt: evidence/story-2-12/prerequisites.md
rescope_decision: evidence/story-2-12/rescope-decision-2026-07-27.md
sprint_change_proposal: ../planning-artifacts/sprint-change-proposal-2026-07-27-story-2-12-runtime-identity-rescope.md
split_from: 2-7-tenants-compatibility-and-package-mode-validation
authorization_story: 1-20-owner-approved-parity-closure-and-runtime-pin
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.12: Tenants Runtime Identity Adoption And Package-Mode Validation

Status: in-progress

`in-progress` means authorized source/code work and prerequisite coordination are under way. The
package lane and story review remain fail-closed until the external deliverables named below exist.

## Story

As a Tenants release maintainer,
I want Tenants to adopt only the owner-approved EventStore runtime identity in source and package modes,
so that consumer migration is reproducible, maintainer-approved, and tied to the exact Story 1.20 evidence.

## Acceptance Criteria

1. **Activation fails closed.** Given Story 1.20 has not durably recorded
   `final_decision: available`, `authorize_consumer_migration: true`, a 40-hex
   `tested_runtime_sha`, named EventStore and release-owner approvals, and the approved package
   version plus SHA-256 inventory, when Story 2.12 activation is evaluated, then it remains
   `backlog`, no implementation story file is created, and no Tenants, EventStore, or Builds
   dependency identity changes.
2. **Source identity is tracked and internally consistent.** Given Tenants tracks EventStore
   `main` through its automated `build(deps)` submodule bump rather than a frozen owner-approved
   pin, when Debug/source mode is validated, then Tenants' `references/Hexalith.EventStore`
   gitlink equals the checked-out submodule `HEAD`, that commit is reachable from EventStore
   `origin/main`, no EventStore submodule content is edited, only Tenants-root-declared submodules
   are initialized, and the recorded evidence names the exact EventStore SHA the validation matrix
   was run against.
3. **Package identity is the published catalog version.** Given the Tenants-pinned Builds commit
   declares a single published `HexalithEventStoreVersion` and centrally declares every consumed
   `Hexalith.EventStore*` package under it, when Release/package mode restores, then every
   resolved `Hexalith.EventStore*` asset is `type: package` at exactly that catalog version, that
   version is resolvable from the configured public package source, zero EventStore project edges
   remain including transitive ones, no `Version`, `VersionOverride`, fallback property, or
   Tenants-local `PackageVersion` entry supplies that version, and the evaluated
   `project.assets.json` files are the recorded evidence.
4. **Gateway cannot create a mixed graph.** Given `Hexalith.EventStore.Gateway` is in the
   EventStore release manifest, when the dependency graph is aligned, then Gateway follows the
   same conditional source/package policy as DomainService, and Release assets contain neither a
   mixed Gateway-project/DomainService-package graph nor any EventStore `ProjectReference`.
5. **Compatibility and approval are recorded.** Given source and package modes are aligned, when
   validation runs, then Tenants preserves its domain-service, AppHost, and UI registration and
   passes the focused source/package restore, build, projection/query/provenance/freshness, and
   package-compatibility evidence; completion records the Tenants maintainer-approved commit and
   exact accepted Tenants SHA.

## Activation Decision And Historical Pins

AC1 is satisfied for story creation on 2026-07-27 and remains the fail-closed activation gate.
Re-run the Story 1.20 A/B/C verifier before claiming activation authority; this summary is not a
replacement authority.

**The pins below are a historical record of the Story 1.20 authorization, not binding identity
targets.** The sprint change proposal
`../planning-artifacts/sprint-change-proposal-2026-07-27-story-2-12-runtime-identity-rescope.md`
re-scoped AC2 and AC3 on 2026-07-27, and the AD-22 scoped exception records why. Do not derive
AC2 or AC3 pass/fail from this table.

| Historical record | Value | Status under the amended criteria |
| --- | --- | --- |
| Story 1.20 decision | `available` | **Binding** — AC1 activation authority |
| Consumer migration | `true` | **Binding** — AC1 activation authority |
| Approved/tested EventStore source SHA | `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` | **Historical** — AC2 now tracks EventStore `main` |
| Approved package version | `999.1.20-proof.fa2d1c9910f8` | **Retired** — bytes proved unrecoverable |
| Package hash-manifest SHA-256 | `4271ddc76411780591423ab024b776cd34a2abccf1cc2dac03a245e141dbe0bc` | **Retired** — no byte-equality check in AC3 |
| EventStore owner approval | `jpiquot`, issue comment `5083143163` | **Binding** — AC1 activation authority |
| Release-owner disposition | `jpiquot`, issue comment `5083164122` | **Binding** — AC1 activation authority |
| Authorizing commit C | `1b219d39cfa8f0349175c356001ba539bfb4aa92` | **Binding** — AC1 activation authority |

The 14-package SHA-256 inventory in `nuget-sha256.txt` remains a checked-in Story 1.20 artifact and
must not be reconstructed, shortened, or replaced. It is no longer a Story 2.12 acceptance target.

### Completion Gate At Creation — Superseded

The three coordination conditions recorded at creation (approved-SHA gitlink, a Builds commit
exposing `999.1.20-proof.fa2d1c9910f8`, and a retrievable source for the 14 `.nupkg` files) are
**superseded** by the amended AC2 and AC3. The surviving useful part of the Builds prerequisite is
the central `Hexalith.EventStore.Gateway` catalog entry from PR #47 — it is retained in the current
Builds pins and is still required by AC4.

At the accepted Tenants `main` these conditions resolve as follows: the EventStore gitlink is
whatever `main` carries and must satisfy the amended AC2 reachability/equality check; the Builds
pin must expose a **published** `HexalithEventStoreVersion` plus the Gateway entry. It remains
forbidden to add a version in a Tenants project or edit Builds without its owner/release change
control.

### External Prerequisite Contract — Retired

**This contract is retired as of 2026-07-27.** It required (1) a Builds commit setting
`HexalithEventStoreVersion` to `999.1.20-proof.fa2d1c9910f8`, and (2) a retrievable source for the
original 14 Story 1.20 `.nupkg` files proved byte-equal against the approved manifest.

Deliverable 1 was satisfied (Builds `8f32f127`, PR #47, approval comment `5088870151`) and then
correctly rolled back to a published catalog version; its Gateway entry survives and is retained.
Deliverable 2 was proved **unsatisfiable** — every avenue the contract named returned negative, so
0 of the 14 approved `Hexalith.EventStore*` `.nupkg` files exist anywhere. The complete negative
audit is preserved in `evidence/story-2-12/prerequisites.md` and is the recorded justification for
the AD-22 scoped exception.

Under the amended AC3 the package lane is no longer blocked on external deliverables: it validates
against the published Builds-catalog version already pinned by Tenants. A rebuild of the retired
proof packages is no longer required and must not be undertaken to satisfy this story.

## Tasks / Subtasks

- [ ] Revalidate authorization and establish the Tenants-owned change boundary (AC: 1, 2, 3, 5)
  - [ ] Run the complete Story 1.20 A/B/C verifier and continue directly into its source and NuGet
        consumer procedures; derive all pins from the verified packet rather than assigning them
        from prose.
  - [x] Confirm the EventStore and release-owner approvals still bind the exact runtime, package
        version, 14-package hash inventory, accepted scope, and migration authorization.
  - [x] Work from a clean Tenants repository checkout where `references/Hexalith.EventStore` and
        `references/Hexalith.Builds` are Tenants-root-declared submodules. Do not initialize a nested
        Tenants dependency from the EventStore umbrella and do not use recursive/remote updates.
  - [x] Obtain the Tenants maintainer's explicit scope approval before changing Tenants gitlinks;
        retain approver, date, accepted scope, rejected alternatives, and open decisions.
  - [x] Stop the affected lane without its identity change if its authority or artifact cannot be
        proved. Do not use success in the source lane to claim package evidence or story review.

- [x] Satisfy the release-owner prerequisite contract (AC: 3, 4) — **retired 2026-07-27**
  - [x] Obtain and verify the approved Builds commit and durable approval record defined above.
        (Builds `8f32f127`, PR #47, approval comment `5088870151`. Its proof-version pin was
        correctly rolled back; its central `Hexalith.EventStore.Gateway` entry survives and is
        still required by AC4.)
  - [x] Prove whether a retrievable source for the original approved package bytes exists.
        (Proved **negative and closed**: 0 of 14 across whole-filesystem scan, nuget.org, the
        locked Azure WORM archive, retained Actions artifacts, and GitHub Packages with
        `read:packages` granted. This negative audit is the recorded justification for the AD-22
        scoped exception; the contract is retired, not satisfied.)
  - [x] Store the prerequisite receipt in EventStore `_bmad-output`, outside the Tenants commit.

- [ ] Validate the tracked source identity in Tenants (AC: 2)
  - [ ] On a **pristine checkout, before the lane's restore**, verify Tenants'
        `references/Hexalith.EventStore` gitlink equals the checked-out submodule `HEAD`, that this
        commit is reachable from EventStore `origin/main`, that no EventStore submodule content is
        edited, and that only Tenants-root-declared submodules are initialized. Record the exact
        EventStore SHA validated.
        (Ordering is load-bearing: MSBuild writes ignored `obj/` artifacts into the EventStore
        submodule and trips the `--ignored=matching` cleanliness assertion after any restore/build.)
  - [ ] Do not restore, re-pin, or freeze the gitlink. The automated `build(deps)` bump is the
        expected mechanism; a receipt is bound to the SHA it names, not to a permanent pin.
  - [x] Prove Debug source intent uses `UseHexalithProjectReferences=true` plus the existing source
        path and resolves EventStore edges as projects. Do not force
        `HexalithEventStoreFromSource` directly or infer source intent from Debug configuration.

- [ ] Consume the published Builds catalog identity (AC: 3, 4)
  - [x] Require the Tenants-pinned Builds commit to centrally declare
        `Hexalith.EventStore.Gateway` under the single `HexalithEventStoreVersion` variable.
        (Retained in the current pin `1b1c0b0` from the approved `8f32f127`.)
  - [ ] Verify that pin's `HexalithEventStoreVersion` is a **published** version resolvable from the
        configured public package source. Do not add `Version`, `VersionOverride`, fallback
        properties, or local `PackageVersion` entries in Tenants.
  - [ ] Record the selected Builds SHA and its resolved catalog version in the evidence receipt.

- [x] Align Gateway with the existing EventStore dependency-mode policy (AC: 3, 4)
  - [x] In `src/Hexalith.Tenants/Hexalith.Tenants.csproj`, give the Gateway project reference the
        same `HexalithEventStoreFromSource == true` condition and version metadata as DomainService.
  - [x] Add the complementary condition-only `PackageReference` for
        `Hexalith.EventStore.Gateway`; the central Builds catalog supplies its version.
  - [x] Preserve the current host composition and comment intent: the Tenants host composes the
        reusable Gateway library while the standalone EventStore web/container host stays separate.
  - [x] Extend `PackageGovernanceTests` or add a focused Contracts test to reject any configuration
        that resolves Gateway from source while DomainService resolves from a package, or resolves
        any EventStore project in Release/package mode. Do not hide this host-graph rule in an API- or
        UI-only test.

- [ ] Prove separate source and package dependency graphs (AC: 2, 3, 4)
  - [ ] Use separate clean working copies or isolated intermediate/output directories for the two
        modes. Rerun restore after every mode change; never reuse a prior `project.assets.json`.
        (Partial 2026-07-27: a full `--force-evaluate` restore was rerun after every mode change and
        no prior assets file was reused, but one working copy served both modes rather than separate
        copies/isolated output directories. Left unchecked until the package lane runs for real.)
  - [x] Source lane: isolated restore, Debug, explicit
        `UseHexalithProjectReferences=true`; assert every selected EventStore dependency is a
        project rooted at the validated checkout and no EventStore package substitutes for it.
  - [ ] Package lane: Release, explicit `UseHexalithProjectReferences=false`, `--force-evaluate`,
        restoring the pinned Builds catalog version from the configured public package source.
        (Isolated global-packages/HTTP-cache directories and a source-mapped temporary
        `nuget.config` are no longer required: with byte equality retired there are no approved
        bytes to isolate or map, and the catalog version is publicly published.)
  - [ ] Parse the evaluated dependency items and every relevant `project.assets.json`. Require every
        selected `Hexalith.EventStore*` library to have `type: package` and exactly the pinned
        Builds catalog version; require zero EventStore project references, including transitive
        ones. These evaluated assets are the recorded AC3 evidence.
  - [x] Byte-compare each restored EventStore `.nupkg` against the approved 14-line manifest.
        (**Retired 2026-07-27** with the External Prerequisite Contract — the approved bytes were
        proved unrecoverable, so there is nothing to compare against. Do not reintroduce this check
        or substitute a rebuilt artifact for it.)
  - [ ] Run unattended with bounded execution and `-nodeReuse:false -m:1`. Do not add
        `--interactive`, ignore a failed source, or allow a credential prompt to turn the gate into
        an indefinite wait. (`-m:1` is required: parallel MSBuild instances race on the same
        EventStore `.deps.json`.)

- [ ] Run the compatibility and regression matrix in both modes (AC: 4, 5)
  - [ ] **Re-run at the accepted Tenants `main` commit under the amended AC2/AC3.** The subtasks
        below were proved on a proof clone pinned back to the now-historical
        `fa2d1c9910f8`. Under the amended AC2 the binding identity is whatever Tenants `main`
        carries, so the whole matrix must be re-run there and the exact validated EventStore SHA
        recorded. Every result below is retained as prior evidence, not as closure.
  - [x] Restore and build `Hexalith.Tenants.slnx` separately in Debug/source and Release/package mode
        with warnings as errors and zero warnings/errors.
        (Both lanes exit 0 with **0 Warning(s), 0 Error(s)**. The Release lane validates
        *compatibility* against the published `3.82.0` catalog; it does **not** close AC3, whose
        exact approved-version/byte requirement stays blocked.)
  - [x] Run, by project rather than solution-level `dotnet test`, the Contracts, Integration, UI,
        and Server test projects in each freshly restored mode.
        (Both modes: Contracts 115/115, Server 738/738, UI 1266/1266, Integration 167 passed /
        1 skipped / 0 failed. Zero failures in either mode.)
  - [x] Preserve the dedicated external API host, generated-controller gateway boundary, domain
        service and AppHost registrations, typed-client-only UI, and package/source conditional
        topology established by Stories 2.4-2.7.
  - [x] Preserve Story 2.10's platform-owned
        `AddEventStoreDaprServiceInvocation("eventstore", daprApiToken)` handler order and the guards
        forbidding a Tenants-local DAPR routing-header handler.
  - [x] Preserve Story 2.11's fail-closed provenance/lifecycle behavior. Exercise the existing
        projection/query/provenance/freshness tests; do not weaken production policy to accommodate
        fixtures and do not accept mock-only proof for a persisted-path assertion.
        (Carried by the passing `IntegrationTests`, `Server.Tests`, and `UI.Tests` suites in both
        modes. No production policy, guard, or fixture was weakened.)
  - [x] Record commands, SDK/package inputs, mode, results, resolved dependency inventory, exact
        hashes, and persisted-path evidence in temporary/CI artifacts until an exact Tenants commit
        exists. Do not place SHA-named evidence inside the commit whose SHA it claims to identify.

- [ ] Close through the Tenants maintainer and update the EventStore pointer (AC: 5)
  - [ ] Obtain a maintainer-approved Tenants commit/PR containing the exact gitlinks, conditional
        Gateway change, and tests. Bind its CI/evidence URLs in the approval; record the exact
        accepted Tenants SHA and accepted scope.
  - [ ] Verify the accepted Tenants commit is published and contains the approved EventStore and
        Builds gitlinks; distinguish the Tenants SHA from the EventStore SHA.
  - [ ] From the EventStore root, persist the final receipt and copied support-safe logs under
        `_bmad-output/implementation-artifacts/evidence/story-2-12/<accepted-tenants-sha>/`. This
        outer evidence commit may name the already-fixed Tenants SHA without changing it.
  - [ ] Update the EventStore repository's `references/Hexalith.Tenants` gitlink only to that exact
        accepted Tenants SHA, then rerun root pointer/cleanliness guards.
  - [ ] Advance this story to `review` only when every AC has durable evidence; advance to `done`
        only after independent review confirms both modes and the maintainer authority chain.

## Dev Notes

### Architecture And Implementation Guardrails

- **AD-11 / FR21-FR22:** Package mode is the default in every configuration. Only explicit
  `UseHexalithProjectReferences=true` expresses source intent; unset/false remains package intent.
  The Builds catalog is the only source-owned NuGet version authority. Release/package validation
  may not compile a source edge.
- **AD-12 / NFR16:** HTTP success, compilation alone, or mock calls do not close high-risk
  compatibility. Persist package bytes, evaluated assets, exact identities, and relevant persisted
  projection/query evidence.
- **AD-22 (as amended 2026-07-27 by its scoped Story 2.12 exception):** Source mode proves the
  EventStore gitlink equals the submodule checkout `HEAD` and is reachable from EventStore
  `origin/main`, recording the validated SHA; package mode proves every resolved
  `Hexalith.EventStore*` asset is `type: package` at exactly the pinned Builds catalog's published
  `HexalithEventStoreVersion`, with zero project edges and no consumer-local version authority.
  Byte equality against the retired 14-package manifest is waived; AD-11's central-catalog rule and
  AD-12's persisted-path requirement are **not**. Never compare the consuming Tenants SHA with the
  EventStore SHA. This relief is scoped to this story and this consumer and extends to no other.
- **AD-2/AD-3/AD-4:** Do not recreate hosting, gateway, controller, or transport infrastructure in
  Tenants. Generated REST remains in `Hexalith.Tenants.Api`; UI remains a typed client consumer.
- **AD-14/AD-15:** Preserve query metadata and explicit route provenance. Only valid
  `ProjectionBacked` evidence may render projection-confirmed lifecycle state; missing/invalid,
  `HandlerComputed`, and `Unknown` remain fail closed.
- **AD-18:** The platform client owns `dapr-app-id` and `dapr-api-token`. Current Tenants already uses
  the platform handler and has structural/behavioral guards; reuse them rather than adding a handler.
- **AD-9/AD-10:** Dependency adoption must not change AppHost/DAPR identities or weaken application-
  layer authentication, tenant authorization, or support-safe failure behavior.
- **No UX redesign:** This story changes dependency identity and validation only. Preserve the
  existing Fluent/FrontComposer UI and lifecycle presentation; do not address unrelated legacy CSS
  token debt.
- **No scope expansion:** Do not fix the broader sibling mutation gates recorded in
  `deferred-work.md`, change the pre-Story-4.7 producer alias, publish a new EventStore release,
  change the 14-package manifest, upgrade unrelated dependencies, or alter container identity.
- Keep nullable and warnings-as-errors behavior. Use the repository-pinned .NET SDK and existing
  xUnit v3, Shouldly, and test-project conventions; do not introduce a test framework or package.

### Current Code Intelligence

- `Directory.Build.props` already implements the required default: explicit project-reference
  opt-in plus source existence selects `HexalithEventStoreFromSource`; otherwise package mode wins.
  Preserve this logic rather than adding configuration-name heuristics.
- EventStore dependencies in Contracts, Client, Server, Aspire, AppHost, API, UI, and integration
  test projects already use complementary source/package conditions. The outlier is the Gateway
  reference in `src/Hexalith.Tenants/Hexalith.Tenants.csproj`, which is unconditional while
  DomainService immediately below it is conditional.
- `TenantsApiStructuralTests` already evaluates both dependency modes and protects the generated API
  and AD-18 boundaries, but it does not inspect the domain host's Gateway/DomainService graph. Reuse
  its evaluation pattern; place the new host rule in package-governance/focused dependency tests.
- `TenantsUiCompositionTests` already forces a fresh restore for each dependency mode, reads
  `project.assets.json`, rejects empty/vacuous results, uses `-nodeReuse:false`, and protects the
  typed-client-only UI boundary. Reuse its helpers or factor a narrowly shared test helper only when
  that reduces duplication without coupling unrelated test assemblies.
- `scripts/validate-consumer-package-references.py` is a Tenants-package consumer smoke and explicitly
  tolerates `NU1603`; it is not exact Story 1.20 EventStore identity evidence. Keep it as smoke only;
  the Story 1.20 NuGet consumer procedure, assets inspection, hashes, and byte comparisons are the
  authority for this migration.
- Current `Program.cs` calls the platform `AddEventStoreDaprServiceInvocation`; current structural
  tests reject `DaprAppIdHandler` and direct routing-header setters. Story 2.10's handoff is already
  implemented at the creation baseline.

### Previous Story And Git Intelligence

- Story 2.7 is the completed pre-authorization registration/provenance correction. It deliberately
  changed no dependency identity and handed all authorized adoption to this story.
- Story 2.11 is done and its current Tenants work is published at
  `f8aff935cdfbc9d9d394c4b4c0e2861d191f6107`. Its older prose naming `d2e5a121` or an uncommitted
  `7e445f3` checkout is stale after root commit `73589770`; derive identity from Git and Story 1.20,
  never copy that narrative.
- Story 2.11 established the testing pattern: fail-closed matrices first, then the full UI suite,
  and production-gateway persisted-state evidence where required. A null live query payload must
  degrade to unknown/non-actionable rather than being inferred as current.
- The broader member/configuration/metadata/lifecycle mutation-gate defect remains explicitly
  deferred. It is not an identity-adoption regression and is outside Story 2.12.
- At creation, EventStore, Tenants, and Builds worktrees are clean on `main`, aligned with their
  published branches. Recheck immediately before implementation; do not assume this snapshot remains
  current.

### Expected File Scope And Repository Ownership

The implementer must report the actual list and commit from the repository that owns each change.

**Tenants repository paths** (shown relative to the Tenants root):

- `references/Hexalith.EventStore` — gitlink at the approved EventStore SHA; never edit its content.
- `references/Hexalith.Builds` — gitlink at the already-approved catalog commit; never edit the
  Builds catalog as part of a Tenants commit.
- `src/Hexalith.Tenants/Hexalith.Tenants.csproj` — conditional Gateway project/package pair.
- `tests/Hexalith.Tenants.Contracts.Tests/PackageGovernanceTests.cs` or a focused Contracts test —
  host Gateway/DomainService and evaluated-graph assertions.
- `tests/Hexalith.Tenants.IntegrationTests/TenantsApiStructuralTests.cs` only if the existing API/
  AD-18 boundary needs a regression adjustment; it does not own the domain host dependency graph.
- `tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs` only if its assets-file coverage must
  be extended; do not churn unrelated UI tests.

**EventStore root paths** (only after an accepted Tenants SHA exists, except prerequisites):

- `_bmad-output/implementation-artifacts/evidence/story-2-12/prerequisites.md` — external Builds and
  package-byte availability receipts.
- `_bmad-output/implementation-artifacts/evidence/story-2-12/<accepted-tenants-sha>/` — final commands,
  results, asset inventory, hashes, maintainer approval, and exact identity receipt.
- `references/Hexalith.Tenants` — root gitlink to the exact accepted Tenants SHA.
- This story and `sprint-status.yaml` — status/evidence bookkeeping.

### Validation Matrix

Run from the Tenants repository that owns the code change. Use `Hexalith.Tenants.slnx` for restore
and build only; run tests by project. The exact approved NuGet consumer procedure in Story 1.20 is
normative and must wrap the package lane. Representative command shapes are:

```text
# Debug/source — fresh isolated restore after exact gitlink/checkout verification
dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Debug \
  -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false \
  -p:HexalithCommonsFromSource=false -nodeReuse:false
dotnet build Hexalith.Tenants.slnx --configuration Debug --no-restore --warnaserror \
  -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false \
  -p:HexalithCommonsFromSource=false -nodeReuse:false

# Release/package — use Story 1.20's source-mapped config and isolated --packages directory
dotnet restore Hexalith.Tenants.slnx --configfile <approved-source-mapped-nuget.config> \
  --packages <isolated-packages> --force-evaluate -p:Configuration=Release \
  -p:UseHexalithProjectReferences=false -nodeReuse:false
dotnet build Hexalith.Tenants.slnx --configuration Release --no-restore --warnaserror \
  -p:UseHexalithProjectReferences=false -nodeReuse:false
```

For each freshly built mode, run these projects separately with matching properties and
`--no-build --no-restore`:

- `tests/Hexalith.Tenants.Contracts.Tests/Hexalith.Tenants.Contracts.Tests.csproj`
- `tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj`
- `tests/Hexalith.Tenants.UI.Tests/Hexalith.Tenants.UI.Tests.csproj`
- `tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj`

Do not use package-mode output to satisfy source-mode tests or vice versa. If the environment cannot
run an approved feed/source or a required persisted-path lane, record the exact command and blocker;
do not mark the evidence passed.

### Latest Technical Notes

- NuGet restore writes the resolved graph to `obj/project.assets.json`; inspect that file after the
  lane's own restore rather than inferring the graph from project XML.
- An isolated `--packages` directory prevents the user's global package cache from supplying stale
  bytes. A source-mapped temporary `nuget.config` prevents the first-responding source from
  substituting an identically versioned EventStore package.
- `--force-evaluate` forces reevaluation; it does not replace cache isolation, source mapping, hash
  verification, or byte comparison.
- Git's recorded gitlink and the submodule working-tree `HEAD` are separate facts. Verify both; use a
  path-scoped `submodule update --init -- references/Hexalith.EventStore` only from the Tenants root
  and only when authorized. Never add `--recursive`.
- Architecture and the immutable Story 1.20 packet pin the versions for this story. Do not perform an
  opportunistic SDK, framework, or NuGet upgrade based on current public versions.

Official references checked 2026-07-27:

- [NuGet package restore](https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore)
- [Managing NuGet global packages and cache folders](https://learn.microsoft.com/en-us/nuget/consume-packages/managing-the-global-packages-and-cache-folders)
- [`dotnet restore`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-restore)
- [NuGet package source mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping)
- [Git submodule](https://git-scm.com/docs/git-submodule)

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:1512`] — Story, owner boundary, focused
  validation, and acceptance criteria.
- [Source: `_bmad-output/planning-artifacts/epics.md:124`] — mode-specific parity activation and
  exact-identity rules.
- [Source: `_bmad-output/planning-artifacts/prd.md:275`] — reproducible package-mode release default.
- [Source: `_bmad-output/planning-artifacts/prd.md:289`] — repository/build guardrails and explicit
  source opt-in.
- [Source: `_bmad-output/planning-artifacts/architecture.md:135`] — AD-11 manifest/catalog and
  package-safe dependency policy.
- [Source: `_bmad-output/planning-artifacts/architecture.md:143`] — AD-12 persisted evidence.
- [Source: `_bmad-output/planning-artifacts/architecture.md:203`] — AD-15 route-bound provenance.
- [Source: `_bmad-output/planning-artifacts/architecture.md:245`] — AD-18 platform-owned DAPR
  routing headers.
- [Source: `_bmad-output/planning-artifacts/architecture.md:298`] — AD-22 exact source/package
  consumer identity.
- [Source: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:1`]
  — durable authority fields and exact artifact pins.
- [Source: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:98`]
  — approved 14-package byte inventory.
- [Source: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:5113`]
  — normative source/NuGet consumer handoff procedures.
- [Source: `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:5350`]
  — downstream ownership routing to Story 2.12.
- [Source: `_bmad-output/implementation-artifacts/evidence/story-1-20/fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/nuget-sha256.txt`]
  — authoritative package hash file.
- [Source: `_bmad-output/implementation-artifacts/2-7-tenants-compatibility-and-package-mode-validation.md`]
  — pre-authorization correction and Story 2.12 handoff.
- [Source: `_bmad-output/implementation-artifacts/2-11-query-provenance-consumption-in-generated-rest-and-tenants.md`]
  — previous-story provenance, validation, and maintainer learnings.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:622`] — local credential/node-reuse
  hazard and proven source-mode mitigation.
- [Source: `references/Hexalith.Tenants/Directory.Build.props:53`] — current dependency-mode
  selection logic.
- [Source: `references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj:20`] — current
  unconditional Gateway outlier and conditional DomainService pair.
- [Source: `references/Hexalith.Builds/Props/Directory.Packages.props:8`] — current non-authorizing
  `3.82.0` catalog pin.
- [Source: `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiStructuralTests.cs:35`]
  — generated API/dependency and AD-18 structural guards.
- [Source: `references/Hexalith.Tenants/tests/Hexalith.Tenants.UI.Tests/TenantsUiCompositionTests.cs:358`]
  — per-mode resolved UI dependency guard.
- [Source: `_bmad-output/planning-artifacts/ux.md:9`] — canonical UX handoff; no redesign in scope.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex (sessions through 2026-07-27), Claude Opus 5 (2026-07-27 verification session)

### Debug Log References

- 2026-07-27 — Story 1.20 official-main authorization verifier attempted three consecutive times.
  The first attempt used an incorrectly expanded pointer-B SHA and failed at commit resolution.
  After resolving the exact A/B/C chain as `b695ad3215cd873c41561635e4eb4d7ff29d56a2` →
  `ed48057e9bf9cb5e5e8667fec84f7c70e4534eea` →
  `1b219d39cfa8f0349175c356001ba539bfb4aa92`, both retries passed all committed evidence
  manifest hashes but exited 1 at `cmp --silent "$A_RAW_EVIDENCE_PROOF"
  "$A_PROVIDER_PROOF_CURRENT"` (verifier line 383). A standalone provider `describe` produced
  the committed 650-byte proof with SHA-256
  `ba460df9e6d85b294e3c39843d1583fd6ebb7c131c20b6974bd7d9f5a28d4dee`, so the full verifier's
  live-proof comparison remains unstable and unproved. Per the three-consecutive-failure and
  fail-closed identity gates, implementation halted without changing any dependency identity.
- 2026-07-27 — The required Story 2.12 prerequisite receipt
  `_bmad-output/implementation-artifacts/evidence/story-2-12/prerequisites.md` is absent; no
  approved Builds commit or retrievable original 14-package byte source is therefore recorded.
- 2026-07-27 — Administrator authorized resolution of the verifier and external prerequisite
  blockers. Work resumed on branch `fix/story-1-20-verifier-adapter-identity`. Red–green coverage
  identified and corrected the random adapter-filename identity defect and narrowed the historical
  deferred-work guard to the three named closure prerequisites; 12/12
  `ProofPacketValidatorIntegrityTests` passed. The third resumed complete-verifier attempt then
  exited 1 because the multiline parenthesized AWK assignment is not accepted by this AWK
  implementation (`awk: cmd. line:12: unexpected newline or end of string`). The workflow's
  three-consecutive-failure gate halted further correction. No dependency identity changed.
- 2026-07-27 — The AWK verifier was repaired with three focused regressions: preserve the canonical
  adapter filename, scope deferred-work validation to the three named closure prerequisites, and
  stop Owner Review extraction at the next `##` or `###` heading. All 13
  `ProofPacketValidatorIntegrityTests`, all 768 Contracts tests, and the repository build passed.
  EventStore [PR #332](https://github.com/Hexalith/Hexalith.EventStore/pull/332) merged the fix as
  `737b3e5a7113de6105e233459203e988af0f78d4`; the complete official-main verifier then passed.
- 2026-07-27 — Tenants maintainer `jpiquot` approved the exact Story 2.12 boundary in
  [Tenants issue #32](https://github.com/Hexalith/Hexalith.Tenants/issues/32). Tenants commit
  `902065efa37d25fd558fc4268a31dfccc515fa41` changes only the EventStore gitlink to
  `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`. The complete A/B/C verifier and source-consumer
  procedure passed in one shell against a separate clean clone of that commit.
- 2026-07-27 — Builds [PR #47](https://github.com/Hexalith/Hexalith.Builds/pull/47) passed all
  repository checks and merged as `8f32f127c73026e46f7eb4fcb1b702d2b518d3e9`. Release-owner
  [approval comment 5088870151](https://github.com/Hexalith/Hexalith.Builds/pull/47#issuecomment-5088870151)
  binds the exact proof version and Gateway catalog entry.
- 2026-07-27 — Package-byte recovery remained fail closed. The locked Azure raw archive contains
  logs and manifests but no `.nupkg`; runtime-matched GitHub artifacts, stored blob versions,
  nuget.org, local caches, transient directories, and deleted-open handles exposed no approved
  package files. GitHub Packages enumeration additionally lacks `read:packages` authorization.
  The exact audit and required external state are recorded in
  `evidence/story-2-12/prerequisites.md`; the Tenants Builds gitlink was not changed.
- 2026-07-27 — Gateway alignment followed red–green TDD. The new Contracts governance test first
  failed because the Gateway package edge did not exist, then passed after Gateway adopted the
  DomainService source/package conditions. Serial MSBuild was required to avoid two project
  instances racing on the same EventStore `.deps.json`. Source evaluation resolved Gateway and
  DomainService as projects under the approved nested checkout and resolved zero EventStore
  packages. These conditional code/test edits remain uncommitted while the package prerequisite is
  blocked because the existing complete package-governance suite correctly requires the approved
  Builds gitlink before accepting the new package reference.
- 2026-07-27 (Claude Opus 5 session) — The complete official-main A/B/C verifier was re-run from
  EventStore `main` `347e0df0` and passed (exit 0); the earlier AWK defect did not recur. The
  source consumer procedure was then run in the same shell.
- 2026-07-27 — **AC2 regressed on published Tenants `main`.** The conditional Gateway work
  (`a7ca142`) reached `main`, but the mechanical merge `230a533d`
  (`build: merge feat/story-2-12-runtime-identity-adoption into main via /pushall`) resolved
  `references/Hexalith.EventStore` to the main-side `737b3e5a`, discarding the approved
  `fa2d1c9910f8` adopted by `902065e`/`db09a84`. The source consumer guard fails closed on `main`
  at `test "$GITLINK_SHA" = "$APPROVED_EVENTSTORE_SHA"`; cleanliness assertions pass, so the
  identity mismatch is the sole failure. Restoring the pin in a separate clean proof clone
  (unpublished local commit `3c2aeaa2`) made the verifier plus source consumer procedure pass in
  one shell.
- 2026-07-27 — Source-lane graph proved. `src/Hexalith.Tenants` resolves 7 EventStore edges, all
  `type: project` rooted at the approved checkout, and **0** EventStore package edges, so Gateway
  and DomainService are aligned with no reachable mixed graph. Debug build with `--warnaserror`
  produced 0 warnings / 0 errors and compiled all seven EventStore assemblies from source.
  Focused Debug/source suites: Contracts 115/115 and Server 738/738 passed; UI 1260/1261.
- 2026-07-27 — **Package identity was already adopted on `main` ahead of its receipt.** Tenants
  `main` points Builds at `0e464b5410b487cee50b9523da3eedd0eec74589` (a descendant of the approved
  `8f32f127`) whose catalog sets `HexalithEventStoreVersion` to `999.1.20-proof.fa2d1c9910f8`.
  Because those bytes were never published, `dotnet restore Hexalith.Tenants.slnx` fails `NU1102`
  for `Hexalith.EventStore.Client` via `Hexalith.Memories.Server`,
  `Hexalith.Tenants.IntegrationTests` cannot restore, and the single UI failure is
  `TenantsUiCompositionTests` failing closed on its own package-mode restore. All three reproduce
  at unmodified `main` `230a533d`, so they are pre-existing and independent of the pin restore.
  The Builds gitlink was not changed in either direction.
- 2026-07-27 — Second package-byte audit extended and corrected the first. A whole-filesystem
  scan found five surviving Story 1.20 transient package directories under
  `/home/administrator/tmp-story-1-20/` holding complete proof sets for runtimes `38f85086fc25`,
  `bae137d9e931`, `eb59649b29a0`, `ed5af0f650a1`, and `f692f903d31b`, plus
  `999.1.20-proof.440ff4cb36a9` artifacts in the NuGet global cache. **None is the approved
  runtime.** The only `.nupkg` at `999.1.20-proof.fa2d1c9910f8` anywhere on the machine is a
  collateral `Hexalith.Commons.UniqueIds`; `Hexalith.EventStore*` coverage is 0 of 14. nuget.org
  has no such version, and GitHub Packages remains unprovable because the token scopes are
  `gist`, `read:org`, `repo`, `workflow` (403, needs `read:packages`).
- 2026-07-27 — **The last open recovery avenue is now closed, negatively.** The release owner
  granted `read:packages`. With the scope in place, `/orgs/Hexalith/packages?package_type=nuget`
  enumerates 185 packages across two pages and contains **no** `Hexalith.EventStore*` package at
  any version (nearest names are `Hexalith.Infrastructure.DaprEventStore` and the typo package
  `Hehalith.Infrastructure.DaprEventStore`); `/users/jpiquot/packages` and `/user/packages` return
  0. Every avenue named by the External Prerequisite Contract has now been executed and returned
  negative, so the original approved bytes are **not recoverable** and AC3, the package half of
  AC4, and AC5 cannot be closed as literally specified. The two remaining dispositions — owner
  supplies the files from outside this environment, or the packaging is re-run from
  `fa2d1c9910f8` under change control with a new manifest and approval — are release-owner
  decisions recorded in `prerequisites.md`.

- 2026-07-27 (Claude Opus 5, second session) — Re-ran the complete official-main A/B/C verifier
  from EventStore `main` `c8c70030`: **exit 0**, `A_TESTED_RUNTIME_SHA` derived from the verified
  packet rather than prose. In the same shell the source consumer procedure was run against
  published Tenants `main` `4ca5f86` and **failed closed on identity only** (gitlink and checkout
  both `b2d34025`); all three cleanliness assertions passed.
- 2026-07-27 — **The AC2 pin is being overwritten on a recurring automated cadence, not by one
  merge.** Beyond the `230a533d` `/pushall` clobber, `build(deps)` submodule bumps advanced the
  gitlink to `b2d34025` (`4ca5f86`) and then, **observed live mid-session**, to `c8c70030`
  (`f1053a31`). The approved SHA is now 46 commits behind EventStore `main`. Any restore of the
  pin will regress again unless `references/Hexalith.EventStore` is excluded from the automated
  bump or a Tenants CI check fails when the gitlink leaves the approved SHA.
- 2026-07-27 — **Prior blockers B1 and B2 are resolved, and not by this session.** Tenants `main`
  now pins Builds at `bb02cdc8` (`fix(deps): restore published EventStore package pin`), which
  returns `HexalithEventStoreVersion` to the published `3.82.0` while retaining the approved
  central `Hexalith.EventStore.Gateway` entry. Solution-level Debug/source restore at unmodified
  `main` now exits 0, `IntegrationTests` restores and runs, and `UI.Tests` is 1266/1266 (was
  1260/1261). The premature package-identity adoption that contradicted the External Prerequisite
  Contract has therefore been rolled back in Tenants.
- 2026-07-27 — Approved pin restored in a fresh clean proof clone as unpublished commit
  `b8698e9d` (content identical to Tenants `main` `4ca5f86`). Complete A/B/C verifier plus source
  consumer procedure in one shell: **`VERIFIER_OK` / `SOURCE_CONSUMER_OK` / exit 0**. Ordering
  defect found and recorded: the guard's `--ignored=matching` assertion fails after any
  restore/build because MSBuild writes ignored `obj/` artifacts into the EventStore submodule, so
  the guard must run on a pristine checkout **before** the lane's restore.
- 2026-07-27 — **Dual-mode graphs proved.** Source lane: `src/Hexalith.Tenants` resolves 7
  EventStore edges, all `type: project` under the approved checkout, **0** package edges (Api 3/3,
  UI 2/2, Client 2/2, Contracts 1/1, all projects). Package lane: **0** project edges and **7**
  package edges, so Release assets contain zero EventStore `ProjectReference`. Gateway and
  DomainService resolve identically in both directions, so no mixed graph is reachable — AC4's
  structural requirement is satisfied in both modes. The package lane resolves `3.82.0`; the
  approved `999.1.20-proof.fa2d1c9910f8` is absent, so AC3 is not claimed.
- 2026-07-27 — **Full compatibility matrix green in both modes**, solution build `--warnaserror`
  0 Warning(s) / 0 Error(s) each: Contracts 115/115, Server 738/738, UI 1266/1266, Integration
  167 passed / 1 skipped / 0 failed — identical counts in Debug/source and Release/package. Every
  lane restored fresh with `--force-evaluate`, run unattended with `-nodeReuse:false -m:1` and no
  `--interactive`. `-m:1` is required because parallel MSBuild instances race on the same
  EventStore `.deps.json`. Details in `evidence/story-2-12/dual-mode-2026-07-27.md`.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- The AWK verifier correction is merged on official EventStore `main`; its focused, assembly-wide,
  build, and full official-main verification gates are green.
- Story activation is `in-progress`. The exact approved EventStore source identity is committed in
  Tenants and passed the same-shell source-consumer guard from a separate clean checkout.
- The approved Builds catalog prerequisite is published and durably approved. The EventStore
  prerequisite receipt binds its exact SHA, scope, author, approver, date, and validation.
- The package lane remains blocked because no retrievable source for the original 14 approved
  `.nupkg` files exists in the audited locations. No Builds gitlink/package identity changed, no
  NuGet consumer procedure ran, and the story remains below `review`.
- Gateway conditional alignment and its focused governance test are implemented locally and green
  in source mode. They are intentionally not published as a commit whose default package-governance
  checks would fail before the authorized Builds gitlink can be adopted.
- 2026-07-27 update: the Gateway conditional alignment and its governance test **are now published**
  on Tenants `main` (`a7ca142`, merged by `230a533d`), and the Contracts suite covering them passes
  115/115 in Debug/source mode.
- The authority chain is green: the complete official-main A/B/C verifier passes, and with the
  approved pin restored the source consumer procedure passes in the same shell.
- **Two blocking conditions now exist on published Tenants `main`, and both are owner decisions.**
  First, AC2 is violated because a mechanical `/pushall` merge discarded the approved EventStore
  pin. Second, the Builds catalog pinning `999.1.20-proof.fa2d1c9910f8` was adopted before the
  byte-availability receipt passed, so Tenants `main` cannot restore its solution in either
  dependency mode. Neither was introduced by this session, and neither was worked around.
- The package lane remains blocked and is now proved unsatisfiable from **every** avenue the
  prerequisite contract names: 0 of the 14 approved `Hexalith.EventStore*` `.nupkg` files exist on
  this machine, on nuget.org, in the WORM archive, in retained Actions artifacts, or in GitHub
  Packages (checked with `read:packages` granted — the org has 185 NuGet packages and none is an
  EventStore package). The original approved bytes are not recoverable. Package bytes must come
  from the release owner from outside this environment, or the packaging must be re-run from the
  approved source SHA under release-owner change control with a new manifest and approval — the
  story forbids a rebuild inheriting Story 1.20 authority, so that path amends the pinned manifest
  rather than reusing it.
- Story status remains `in-progress`. No dependency identity was changed in any published
  repository, nothing was pushed, and no production policy or test was weakened.
- **2026-07-27 second-session disposition.** One of the two blocking conditions recorded above is
  **closed**: the premature proof-version package identity was rolled back in Tenants (Builds
  `bb02cdc8` → `3.82.0`), so `main` restores, builds, and tests cleanly in both modes again. The
  full compatibility matrix now passes in Debug/source **and** Release/package with zero failures
  and zero warnings, and AC4's structural no-mixed-graph requirement is proved in both directions.
- **AC2 is proven achievable at current Tenants content but is not adopted, and restoring it is now
  a policy decision rather than a one-line fix.** The pin is not merely stale; it is actively
  overwritten by a recurring automated `build(deps)` submodule bump, observed advancing again
  during this session. Publishing a restore without also excluding
  `references/Hexalith.EventStore` from that automation — or adding a Tenants CI check that fails
  when the gitlink leaves the approved SHA — would regress within hours, for the third time.
- **AC3 remains blocked and unsatisfiable as literally specified**, unchanged from the exhaustive
  audit: 0 of the 14 approved `Hexalith.EventStore*` `.nupkg` files exist in any avenue the
  External Prerequisite Contract names. The package lane was therefore run only as a
  *compatibility* lane against the published `3.82.0` catalog — with no isolated `--packages`
  directory, no source-mapped `nuget.config`, no manifest verification and no byte comparison,
  because there are no approved bytes to map, verify, or compare. None of those steps is claimed
  as passed, and AC3, the identity half of AC4, and AC5 stay open.
- The story therefore **cannot enter `review`** this session by its own External Prerequisite
  Contract. Both remaining gates are release-owner decisions, not implementation work.
- **2026-07-27 — the owner decided both gates by re-scoping, and the story is HALTED pending
  `bmad-correct-course`.** Decision 1: AC2 is re-scoped so Tenants tracks EventStore `main`
  through its normal automated submodule bump instead of pinning the frozen approved SHA.
  Decision 2: AC3 is re-scoped so package mode validates against a published catalog version
  instead of the unpublished proof version, with no byte-equality check. Both decisions rewrite
  acceptance criteria and contradict **AD-22**; Decision 2 also retires the External Prerequisite
  Contract and the 14-package manifest. `bmad-dev-story` may not modify acceptance criteria,
  `epics.md`, or `architecture.md`, so no AC, epic, or architecture text was touched. The verbatim
  decisions, their consequences, and the required re-validation are recorded in
  `evidence/story-2-12/rescope-decision-2026-07-27.md`. Next action is
  `bmad-correct-course`, then a re-run of the dual-mode matrix at the accepted Tenants `main`
  commit under the amended criteria.

### File List

- _bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md
- _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
- _bmad-output/implementation-artifacts/evidence/story-2-12/prerequisites.md
- _bmad-output/implementation-artifacts/evidence/story-2-12/source-lane-2026-07-27.md
- _bmad-output/implementation-artifacts/evidence/story-2-12/dual-mode-2026-07-27.md
- _bmad-output/implementation-artifacts/evidence/story-2-12/rescope-decision-2026-07-27.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- references/Hexalith.Tenants/references/Hexalith.EventStore
- references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj
- references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/PackageGovernanceTests.cs
- references/Hexalith.Builds/Props/Directory.Packages.props
- references/Hexalith.Builds/Tools/test-authoritative-package-catalog.ps1
- tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs

## Change Log

- 2026-07-27 — Repaired and published the Story 1.20 AWK verifier, established durable Tenants and
  Builds approvals, proved the exact source pin, and recorded the unresolved original-package-byte
  prerequisite without advancing the package lane or story status.
- 2026-07-27 — Re-ran the complete official-main A/B/C verifier (pass), proved the Debug/source
  dependency graph and focused test matrix, and recorded two blocking conditions found on
  published Tenants `main`: the approved EventStore pin was discarded by a mechanical `/pushall`
  merge, and the proof-version Builds catalog was adopted before its byte-availability receipt,
  breaking restore in both dependency modes. Extended the package-byte audit to an exhaustive
  filesystem scan proving 0 of 14 approved packages exist locally. Unchecked the source-identity
  task to match the published state. No identity changed, nothing pushed, story stays
  `in-progress`.
- 2026-07-27 — Second session: re-ran the official-main A/B/C verifier (pass) and the source
  consumer guard (fails on identity only). Recorded that blockers B1/B2 are **resolved** by the
  Builds rollback to `3.82.0`, and that the AC2 pin is being overwritten by a **recurring**
  automated submodule bump — observed advancing again mid-session. Completed the full dual-mode
  compatibility matrix with zero failures and zero warnings (Contracts 115, Server 738, UI 1266,
  Integration 167+1 skipped, in each mode) and proved AC4's no-mixed-graph requirement in both
  directions. Checked off the compatibility/regression task; AC2, AC3, and AC5 stay open pending
  release-owner decisions. No identity changed, nothing pushed, story stays `in-progress`.
- 2026-07-27 — Owner re-scoped both blocked gates: AC2 to track EventStore `main` instead of the
  frozen approved SHA, and AC3 to a published catalog version instead of the unpublished proof
  version. Recorded verbatim in `evidence/story-2-12/rescope-decision-2026-07-27.md`. Story
  HALTED for `bmad-correct-course`, which owns the AC, `epics.md`, and AD-22 amendments that
  `bmad-dev-story` may not make. No acceptance criterion or planning artifact was modified here.
- 2026-07-27 — **`bmad-correct-course` applied the re-scope.** Approved sprint change proposal:
  `../planning-artifacts/sprint-change-proposal-2026-07-27-story-2-12-runtime-identity-rescope.md`
  (Direct Adjustment; scope Moderate). AC2 replaced with tracked-`main` identity (gitlink ==
  checkout `HEAD`, reachable from EventStore `origin/main`, validated SHA recorded); AC3 replaced
  with published-Builds-catalog package identity (`type: package` at the exact catalog version,
  zero project edges, no consumer-local version authority, evaluated `project.assets.json` as
  evidence). `epics.md` Story 2.12 AC2/AC3 and focused validation amended; a dated **scoped
  exception** added to **AD-22** in `architecture.md` with matching non-extension statements at
  `epics.md:135` (Parties gate) and `epics.md:290` (Guardrails). The Activation Decision table is
  now a historical record, the External Prerequisite Contract is retired, and the byte-comparison
  subtask is retired. AC1, AC4, AC5, AD-11, and AD-12 are unchanged. No code, test, dependency
  identity, or published repository state changed; PRD and `sprint-status.yaml` needed no edit.
  Story stays `in-progress` pending the dual-mode matrix re-run at the accepted Tenants `main`.
