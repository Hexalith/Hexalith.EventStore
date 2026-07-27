---
created: 2026-07-27
baseline_commit: 73589770b14888b703d78d37325b066befa0689c
story_id: "2.12"
story_key: 2-12-tenants-runtime-identity-adoption-and-package-mode-validation
status: ready-for-dev
package_lane_status: blocked
package_lane_prerequisite_receipt: evidence/story-2-12/prerequisites.md
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
2. **Source identity is exact.** Given Story 1.20 authorizes migration and names the approved
   EventStore source SHA, when Debug/source mode is adopted, then Tenants'
   `references/Hexalith.EventStore` gitlink and checkout both equal that SHA, no EventStore
   submodule content is edited, and only Tenants-root-declared submodules are initialized.
3. **Package identity is exact.** Given the approved package version and hashes, when
   Release/package mode restores from an isolated cache, then every resolved
   `Hexalith.EventStore*` asset is a package at the exact approved version, the fetched bytes
   match the approved hashes, and the selected Builds commit already exposes that version.
4. **Gateway cannot create a mixed graph.** Given `Hexalith.EventStore.Gateway` is in the
   EventStore release manifest, when the dependency graph is aligned, then Gateway follows the
   same conditional source/package policy as DomainService, and Release assets contain neither a
   mixed Gateway-project/DomainService-package graph nor any EventStore `ProjectReference`.
5. **Compatibility and approval are recorded.** Given source and package modes are aligned, when
   validation runs, then Tenants preserves its domain-service, AppHost, and UI registration and
   passes the focused source/package restore, build, projection/query/provenance/freshness, and
   package-compatibility evidence; completion records the Tenants maintainer-approved commit and
   exact accepted Tenants SHA.

## Activation Decision And Immutable Pins

AC1 is satisfied for story creation on 2026-07-27. Re-run the Story 1.20 A/B/C verifier and its
consumer handoff in the same shell before changing an identity; this summary is not a replacement
authority.

| Pin | Authorized value |
| --- | --- |
| Story 1.20 decision | `available` |
| Consumer migration | `true` |
| Approved/tested EventStore source SHA | `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` |
| Approved package version | `999.1.20-proof.fa2d1c9910f8` |
| Package hash-manifest SHA-256 | `4271ddc76411780591423ab024b776cd34a2abccf1cc2dac03a245e141dbe0bc` |
| EventStore owner approval | `jpiquot`, issue comment `5083143163` |
| Release-owner disposition | `jpiquot`, issue comment `5083164122` |
| Authorizing commit C | `1b219d39cfa8f0349175c356001ba539bfb4aa92` |

The approved hash inventory contains exactly 14 packages. The two dependencies whose conditional
alignment is central to this story are:

- `Hexalith.EventStore.DomainService`:
  `e4419446724a8ab0fadc4650a0b7c8c1c64a5564585d02a44c0229c0a735dd87`
- `Hexalith.EventStore.Gateway`:
  `32fdab1f307e184498d1921242053f929c335048cd537fe4a6f88f5e6fd1d57d`

Use the complete checked-in `nuget-sha256.txt`; never reconstruct, shorten, or replace the
inventory from this story.

### Known Completion Gate At Creation

The activation authority exists, but the current Tenants dependency graph is not yet eligible for
completion:

- EventStore root pins Tenants `f8aff935cdfbc9d9d394c4b4c0e2861d191f6107`; that Tenants commit
  pins nested EventStore `56acc0788e00388038eb1889f3d77c7730a65c94`, not the approved SHA.
- Tenants pins Builds `4e5c2a3ea6510f38121f718fa122e7b92489821c`. Its catalog exposes
  `HexalithEventStoreVersion` `3.82.0`, not the approved proof version, and has no
  `PackageVersion` entry for `Hexalith.EventStore.Gateway`.
- No currently available Builds commit was found that exposes
  `999.1.20-proof.fa2d1c9910f8` and the Gateway entry.
- The Story 1.20 raw evidence bundle retains logs and identity manifests, not the 14 `.nupkg` files;
  its URL is not an approved package source by itself.

This is a fail-closed coordination gate, not permission to use `3.82.0`, add a version in a Tenants
project, or edit Builds without its owner/release change control. An already-approved Builds commit
and approved package bytes/source must exist before package-mode implementation can close AC3-AC5.

### External Prerequisite Contract

EventStore release owner `jpiquot` owns both prerequisite deliverables. Record them in EventStore's
`_bmad-output/implementation-artifacts/evidence/story-2-12/prerequisites.md` before the package lane:

1. A published Hexalith.Builds commit reachable from `origin/main` that sets
   `HexalithEventStoreVersion` to `999.1.20-proof.fa2d1c9910f8`, adds
   `Hexalith.EventStore.Gateway` under that variable, and has a durable approval URL binding the
   exact Builds SHA, two catalog changes, author, approver, date, and accepted scope.
2. A retrievable directory, immutable archive, or source-mapped feed containing the original 14
   Story 1.20 `.nupkg` files. Its receipt names the retrieval URL/path, object/feed version, retention
   or availability boundary, archive hash when applicable, and proves every extracted/fetched file
   against the approved 14-line manifest. A feed/version match without byte equality is insufficient.

If supplying either deliverable would rebuild, republish, or otherwise replace the approved bytes,
route that work through release-owner change control; a newly built artifact cannot inherit Story
1.20 authority. Until both receipts pass, work may proceed on source pinning, conditional Gateway
code, and tests, but no package identity is adopted and the story cannot enter `review`.

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

- [ ] Satisfy the release-owner prerequisite contract (AC: 3, 4)
  - [x] Obtain and verify the approved Builds commit and durable approval record defined above.
  - [ ] Obtain and verify a retrievable source for the original approved package bytes; do not treat
        the Story 1.20 raw-log bundle, version string, or hash manifest alone as package availability.
  - [x] Store the prerequisite receipt in EventStore `_bmad-output`, outside the Tenants commit.

- [ ] Adopt the exact source identity in Tenants (AC: 2)
  - [ ] Change only Tenants' `references/Hexalith.EventStore` gitlink to
        `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`; do not edit EventStore content.
        (Regressed 2026-07-27: the `/pushall` merge `230a533d` discarded the approved pin on
        published `main`. Restored and proved only in an unpublished proof clone.)
  - [ ] Verify the Tenants gitlink and checked-out EventStore `HEAD` both equal the approved SHA and
        both repositories are clean, including ignored generated/configuration inputs covered by
        the Story 1.20 consumer guard.
        (Green in the proof clone; fails closed on published Tenants `main`.)
  - [x] Prove Debug source intent uses `UseHexalithProjectReferences=true` plus the existing source
        path and resolves EventStore edges as projects. Do not force
        `HexalithEventStoreFromSource` directly or infer source intent from Debug configuration.

- [ ] Consume an owner-approved Builds catalog identity (AC: 3, 4)
  - [x] Require an approved Builds commit that already sets the central
        `HexalithEventStoreVersion` to `999.1.20-proof.fa2d1c9910f8` and centrally declares
        `Hexalith.EventStore.Gateway` under that variable.
  - [ ] Update only the Tenants Builds gitlink to that accepted commit after Builds/release-owner
        approval; do not add `Version`, `VersionOverride`, fallback properties, or local
        `PackageVersion` entries in Tenants.
  - [x] Record the selected Builds SHA and its approval/evidence in the Story 2.12 evidence receipt.

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
  - [x] Source lane: isolated restore, Debug, explicit
        `UseHexalithProjectReferences=true`; assert every selected EventStore dependency is a
        project rooted at the exact approved checkout and no EventStore package substitutes for it.
  - [ ] Package lane: isolated global-packages and HTTP-cache directories, Release, explicit
        `UseHexalithProjectReferences=false`, `--force-evaluate`, and a temporary `nuget.config`
        whose source mapping sends `Hexalith.EventStore*` only to the approved package directory.
  - [ ] Parse the evaluated dependency items and every relevant `project.assets.json`. Require every
        selected `Hexalith.EventStore*` library to have `type: package` and the exact approved
        version; require zero EventStore project references, including transitive ones.
  - [ ] Verify the approved package directory against the complete 14-line manifest, then byte-compare
        each restored EventStore `.nupkg` used by Tenants to its already hash-verified approved file.
        A matching filename/version without matching bytes does not pass.
  - [ ] Run unattended with bounded execution and `-nodeReuse:false`. Do not add `--interactive`,
        ignore a failed source, or allow a credential prompt to turn the gate into an indefinite wait.

- [ ] Run the compatibility and regression matrix in both modes (AC: 4, 5)
  - [ ] Restore and build `Hexalith.Tenants.slnx` separately in Debug/source and Release/package mode
        with warnings as errors and zero warnings/errors.
  - [ ] Run, by project rather than solution-level `dotnet test`, the Contracts, Integration, UI,
        and Server test projects in each freshly restored mode.
  - [ ] Preserve the dedicated external API host, generated-controller gateway boundary, domain
        service and AppHost registrations, typed-client-only UI, and package/source conditional
        topology established by Stories 2.4-2.7.
  - [ ] Preserve Story 2.10's platform-owned
        `AddEventStoreDaprServiceInvocation("eventstore", daprApiToken)` handler order and the guards
        forbidding a Tenants-local DAPR routing-header handler.
  - [ ] Preserve Story 2.11's fail-closed provenance/lifecycle behavior. Exercise the existing
        projection/query/provenance/freshness tests; do not weaken production policy to accommodate
        fixtures and do not accept mock-only proof for a persisted-path assertion.
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
- **AD-22:** Source mode compares the EventStore gitlink and checkout to the approved EventStore SHA;
  package mode compares versions and bytes to the approved manifest. Never compare the consuming
  Tenants SHA with the EventStore SHA.
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
- The package lane remains blocked and is now proved unsatisfiable from local state: 0 of the 14
  approved `Hexalith.EventStore*` `.nupkg` files exist anywhere on this machine, on nuget.org, in
  the WORM archive, or in the retained Actions artifacts. Package bytes must come from the release
  owner, or the packaging must be re-run from the approved source SHA under release-owner change
  control with a new manifest and approval — the story forbids a rebuild inheriting Story 1.20
  authority.
- Story status remains `in-progress`. No dependency identity was changed in any published
  repository, nothing was pushed, and no production policy or test was weakened.

### File List

- _bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md
- _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
- _bmad-output/implementation-artifacts/evidence/story-2-12/prerequisites.md
- _bmad-output/implementation-artifacts/evidence/story-2-12/source-lane-2026-07-27.md
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
