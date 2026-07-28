---
created: 2026-07-27
baseline_commit: 73589770b14888b703d78d37325b066befa0689c
story_id: "2.12"
story_key: 2-12-tenants-runtime-identity-adoption-and-package-mode-validation
status: in-progress
accepted_tenants_sha: 578770679b9d3bc3fdf2a8a78190f24cdad8576e
validated_eventstore_sha: c8c7003052a7f811d3b821f3442379ca5f3a9c65
validated_builds_sha: 1b1c0b0360715b82de48b618fc4e94e7e01e8092
resolved_catalog_version: 3.83.0
final_receipt: evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/receipt.md
package_lane_status: closed-against-published-catalog
package_lane_prerequisite_receipt: evidence/story-2-12/prerequisites.md
rescope_decision: evidence/story-2-12/rescope-decision-2026-07-27.md
sprint_change_proposal: ../planning-artifacts/sprint-change-proposal-2026-07-27-story-2-12-runtime-identity-rescope.md
split_from: 2-7-tenants-compatibility-and-package-mode-validation
authorization_story: 1-20-owner-approved-parity-closure-and-runtime-pin
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 2.12: Tenants Runtime Identity Adoption And Package-Mode Validation

Status: in-progress

`review` means every acceptance criterion has durable evidence at the accepted Tenants SHA
`578770679b9d3bc3fdf2a8a78190f24cdad8576e` under the amended AC2/AC3 and the AD-22 scoped
exception, and the Tenants maintainer has accepted that exact SHA. The External Prerequisite
Contract that previously held this story fail-closed is retired; `done` remains gated on
independent review of both dependency modes and the maintainer authority chain.

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

- [x] Revalidate authorization and establish the Tenants-owned change boundary (AC: 1, 2, 3, 5)
  - [x] Run the complete Story 1.20 A/B/C verifier and continue directly into its source and NuGet
        consumer procedures; derive all pins from the verified packet rather than assigning them
        from prose.
        (Verifier **exit 0** from EventStore `main` `49987454`; every authority field read from the
        verified commit A/C blobs, not prose. The two consumer procedures' *frozen-SHA* and
        *approved-byte* assertions are precisely what the amended AC2/AC3 and the AD-22 scoped
        exception replace, so they are not re-run as written; the source procedure's cleanliness
        assertions are retained verbatim in the AC2 guard. Substitution recorded in the receipt.)
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

- [x] Validate the tracked source identity in Tenants (AC: 2)
  - [x] On a **pristine checkout, before the lane's restore**, verify Tenants'
        `references/Hexalith.EventStore` gitlink equals the checked-out submodule `HEAD`, that this
        commit is reachable from EventStore `origin/main`, that no EventStore submodule content is
        edited, and that only Tenants-root-declared submodules are initialized. Record the exact
        EventStore SHA validated.
        (Ordering is load-bearing: MSBuild writes ignored `obj/` artifacts into the EventStore
        submodule and trips the `--ignored=matching` cleanliness assertion after any restore/build.
        Ran on both pristine lane copies before their restores: gitlink == checkout ==
        `c8c7003052a7f811d3b821f3442379ca5f3a9c65`, reachable from EventStore `origin/main`
        `49987454`, all cleanliness assertions PASS, only root-declared submodules initialized.)
  - [x] Do not restore, re-pin, or freeze the gitlink. The automated `build(deps)` bump is the
        expected mechanism; a receipt is bound to the SHA it names, not to a permanent pin.
        (Honored: no gitlink was restored, re-pinned, or frozen in any repository.)
  - [x] Prove Debug source intent uses `UseHexalithProjectReferences=true` plus the existing source
        path and resolves EventStore edges as projects. Do not force
        `HexalithEventStoreFromSource` directly or infer source intent from Debug configuration.

- [x] Consume the published Builds catalog identity (AC: 3, 4)
  - [x] Require the Tenants-pinned Builds commit to centrally declare
        `Hexalith.EventStore.Gateway` under the single `HexalithEventStoreVersion` variable.
        (Retained in the current pin `1b1c0b0` from the approved `8f32f127`.)
  - [x] Verify that pin's `HexalithEventStoreVersion` is a **published** version resolvable from the
        configured public package source. Do not add `Version`, `VersionOverride`, fallback
        properties, or local `PackageVersion` entries in Tenants.
        (Builds `1b1c0b0` declares `3.83.0`; all 11 consumed packages are published on nuget.org —
        the sole registered source — and were downloaded into a fresh isolated `--packages`
        directory. `CentralPackageVersionOverrideEnabled=false`; no Tenants-local version authority
        of any kind exists.)
  - [x] Record the selected Builds SHA and its resolved catalog version in the evidence receipt.
        (`1b1c0b0360715b82de48b618fc4e94e7e01e8092` → `3.83.0`.)

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

- [x] Prove separate source and package dependency graphs (AC: 2, 3, 4)
  - [x] Use separate clean working copies or isolated intermediate/output directories for the two
        modes. Rerun restore after every mode change; never reuse a prior `project.assets.json`.
        (Closed 2026-07-27: two **separate clean clones** (`src-lane`, `pkg-lane`), each detached at
        the accepted SHA with only root-declared submodules initialized and no shared object store
        — verified by the absence of any `objects/info/alternates`. Each lane ran its own
        `--force-evaluate` restore; no assets file was reused or shared across modes.)
  - [x] Source lane: isolated restore, Debug, explicit
        `UseHexalithProjectReferences=true`; assert every selected EventStore dependency is a
        project rooted at the validated checkout and no EventStore package substitutes for it.
  - [x] Package lane: Release, explicit `UseHexalithProjectReferences=false`, `--force-evaluate`,
        restoring the pinned Builds catalog version from the configured public package source.
        (Isolated global-packages/HTTP-cache directories and a source-mapped temporary
        `nuget.config` are no longer required: with byte equality retired there are no approved
        bytes to isolate or map, and the catalog version is publicly published.
        An isolated `--packages` directory was nevertheless used — not for byte isolation, but so
        that resolvability from nuget.org is proved by a real download rather than a warm cache.
        Restore exit 0, no `NU*` diagnostic; all 11 packages fetched at `3.83.0`.)
  - [x] Parse the evaluated dependency items and every relevant `project.assets.json`. Require every
        selected `Hexalith.EventStore*` library to have `type: package` and exactly the pinned
        Builds catalog version; require zero EventStore project references, including transitive
        ones. These evaluated assets are the recorded AC3 evidence.
        (17 assets files per lane. Package lane: **61 edges, 0 `type: project`, 61 `type: package`,
        single resolved version `3.83.0`**. Source lane: **60 edges, 60 `type: project`, 0
        packages, 0 outside the validated checkout**. Gateway and DomainService resolve identically
        in both directions, so no mixed graph is reachable.)
  - [x] Byte-compare each restored EventStore `.nupkg` against the approved 14-line manifest.
        (**Retired 2026-07-27** with the External Prerequisite Contract — the approved bytes were
        proved unrecoverable, so there is nothing to compare against. Do not reintroduce this check
        or substitute a rebuilt artifact for it.)
  - [x] Run unattended with bounded execution and `-nodeReuse:false -m:1`. Do not add
        `--interactive`, ignore a failed source, or allow a credential prompt to turn the gate into
        an indefinite wait. (`-m:1` is required: parallel MSBuild instances race on the same
        EventStore `.deps.json`.)
        (Every restore, build, and test invocation in both lanes ran with `-nodeReuse:false -m:1`,
        unattended, with no `--interactive` and no ignored source failure.)

- [x] Run the compatibility and regression matrix in both modes (AC: 4, 5)
  - [x] **Re-run at the accepted Tenants `main` commit under the amended AC2/AC3.** The subtasks
        below were proved on a proof clone pinned back to the now-historical
        `fa2d1c9910f8`. Under the amended AC2 the binding identity is whatever Tenants `main`
        carries, so the whole matrix must be re-run there and the exact validated EventStore SHA
        recorded. Every result below is retained as prior evidence, not as closure.
        (Re-run 2026-07-27 at accepted Tenants `578770679b9d3bc3fdf2a8a78190f24cdad8576e`,
        EventStore `c8c7003052a7f811d3b821f3442379ca5f3a9c65`, Builds `1b1c0b0` → `3.83.0`.
        Both lanes: restore exit 0, build `--warnaserror` **0 Warning(s), 0 Error(s)**;
        Contracts 115/115, Server 738/738, UI 1276/1276, Integration 167 passed / 1 skipped /
        0 failed — identical counts in Debug/source and Release/package. Receipt:
        `evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/receipt.md`.)
  - [x] Restore and build `Hexalith.Tenants.slnx` separately in Debug/source and Release/package mode
        with warnings as errors and zero warnings/errors.
        (Both lanes exit 0 with **0 Warning(s), 0 Error(s)**. Superseded annotation: that earlier
        run validated compatibility only, against `3.82.0`, and could not close the pre-amendment
        AC3. Re-run at the accepted `578770679b9d…` against Builds `1b1c0b0` → `3.83.0`, the
        Release lane now closes the **amended** AC3 on its own terms.)
  - [x] Run, by project rather than solution-level `dotnet test`, the Contracts, Integration, UI,
        and Server test projects in each freshly restored mode.
        (Both modes: Contracts 115/115, Server 738/738, UI 1266/1266, Integration 167 passed /
        1 skipped / 0 failed. Zero failures in either mode. Re-run at the accepted Tenants SHA:
        identical except UI 1276/1276, Tenants `main` having gained ten UI tests since.)
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

- [x] Close through the Tenants maintainer and update the EventStore pointer (AC: 5)
  - [x] Obtain a maintainer-approved Tenants commit/PR containing the exact gitlinks, conditional
        Gateway change, and tests. Bind its CI/evidence URLs in the approval; record the exact
        accepted Tenants SHA and accepted scope.
        (Maintainer `jpiquot` accepted `578770679b9d3bc3fdf2a8a78190f24cdad8576e` on 2026-07-28,
        choosing a direct in-repository record over a GitHub comment when offered both. Approver,
        date, accepted scope, bound evidence paths, and the rejected alternative are recorded in
        the receipt's `AC5 — Tenants Maintainer Acceptance` section, together with an explicit note
        that this approval — unlike the others in this story — has no external GitHub record.)
  - [x] Verify the accepted Tenants commit is published and contains the approved EventStore and
        Builds gitlinks; distinguish the Tenants SHA from the EventStore SHA.
        (Tenants `578770679b9d3bc3fdf2a8a78190f24cdad8576e` is published and reachable on Tenants
        `origin/main`; it carries EventStore `c8c7003052a7f811d3b821f3442379ca5f3a9c65` and Builds
        `1b1c0b0360715b82de48b618fc4e94e7e01e8092`. The Tenants and EventStore SHAs are kept
        distinct and are never compared.)
  - [x] From the EventStore root, persist the final receipt and copied support-safe logs under
        `_bmad-output/implementation-artifacts/evidence/story-2-12/<accepted-tenants-sha>/`. This
        outer evidence commit may name the already-fixed Tenants SHA without changing it.
        (`receipt.md`, the three lane scripts, and 17 support-safe logs, ~100 KB.)
  - [x] Update the EventStore repository's `references/Hexalith.Tenants` gitlink only to that exact
        accepted Tenants SHA, then rerun root pointer/cleanliness guards.
        (No change was needed at the time: the root gitlink then equalled `578770679b9d…`. Guards
        rerun — all seven `references/*` gitlinks reachable on their own remotes, root worktree clean
        apart from the new untracked evidence directory, and no nested submodule initialized.
        **Corrected 2026-07-28 by code review:** this parenthetical was true when written and is
        false at the commit that carries it. EventStore `57143dd3` — the commit that publishes this
        story's receipt and advances it to `review` — moved the root gitlink from the accepted
        `578770679b9d` to `f279cb13`, whose own `references/Hexalith.EventStore` gitlink is
        `49987454`, not the validated `c8c70030`. Owner decision D1 (2026-07-28): keep the pointer
        where the automated bump put it, consistent with the amended AC2 which makes tracking `main`
        the intended mechanism, and record the delta rather than re-pinning it. The full statement
        of what this costs is in the receipt's "Umbrella Pointer Correction" section; the absence of
        any drift detector is filed in `deferred-work.md`.)
  - [x] Advance this story to `review` only when every AC has durable evidence; advance to `done`
        only after independent review confirms both modes and the maintainer authority chain.
        (Advanced to `review` on 2026-07-28: AC1-AC4 evidenced at the accepted SHA and AC5 accepted
        by the maintainer. `done` remains gated on independent review.)

### Review Findings

**Outcome: 3 decisions resolved by the owner, 13 patches applied, 2 items deferred, 12 dismissed.**
Story returned to `in-progress` — not because a finding is unresolved, but because the single patch
that fixes a HIGH finding (the AC4 guard) lives in `Hexalith.Tenants` and is **an uncommitted
working-tree edit**. It needs a Tenants commit and the same maintainer acceptance AC5 required
before this story can be `done`. Everything in the EventStore repository is complete and verified:
`Hexalith.EventStore.Contracts.Tests` **778/778**.

**Drift observed live during this review, for the third recorded time.** While the review ran, the
umbrella's `references/Hexalith.Tenants` working tree moved `f279cb13` → `85e24d5` (11 commits,
now pinning Builds `53d53ae` and EventStore `150216c3` — neither the validated `c8c70030` nor the
`49987454` recorded an hour earlier). This is the same mechanism as finding D1 and is further
evidence for the deferred drift-detector item. Notably two of those commits
(`c407c9e`, `85e24d5`) add a *story gitlink declaration guard* in Tenants, which may already
address part of that deferred item — worth checking before the follow-up story is scoped.

Adversarial code review 2026-07-28 (Claude Opus 5), four independent no-context review layers:
Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor. Every finding below was
re-verified against the live repositories before rating; subagent severities were discarded.

**Independently confirmed as sound** (recorded so they are not re-litigated): AC2's five guard
assertions at `c8c70030` reachable from EventStore `origin/main`; AC3's published-catalog identity
(Builds `1b1c0b0` → `3.83.0`, 13 central entries, `CentralPackageVersionOverrideEnabled=false`, zero
Tenants-local version authority, all 11 packages really downloaded); edge counts 60 source-project /
61 package at exactly `3.83.0` and the 60↔61 analyzer-reference asymmetry; **zero EventStore project
edges from all 17 Tenants projects in the package lane**; lane isolation (no `objects/info/alternates`
in either clone); test counts 115 / 738 / 1276 / 167+1-skipped identical in both modes with
`0 Warning(s) / 0 Error(s)`; and the AD-22 exception's own scoping — dated, one story, one consumer,
explicit non-extension to Parties 8.6, with AD-11 and AD-12 byte-unchanged.

**Unclaimed strength worth recording:** `git rev-list -n1 v3.83.0` → `c8c7003052a7f811d3b821f3442379ca5f3a9c65`.
The published catalog version validated by the package lane was tagged from *exactly* the EventStore
SHA validated by the source lane. The amendment nominally decoupled the two lanes' identities and the
re-scope decision accepts "losing the exact-tested-runtime guarantee" — in fact both lanes validated
the same EventStore code. No artifact notices this; it materially strengthens AC2/AC3 coherence.

- [x] [Review][Decision] Root `references/Hexalith.Tenants` gitlink no longer equals the accepted SHA, and the commit that says otherwise is the commit that moved it — All four layers converged on this. `git ls-tree 57143dd3 references/Hexalith.Tenants` → `f279cb13`, while the preceding `49987454` carried the accepted `578770679b9d`. `f279cb13`'s own `references/Hexalith.EventStore` gitlink is `49987454`, **not** the validated `c8c70030`, so the umbrella now composes a Tenants commit that no AC2 guard and no dual-mode matrix ever covered. The final subtask records "No change was needed: the root gitlink already equals `578770679b9d…`" and `receipt.md:251` repeats it — both false at HEAD. `f279cb13` is a superset of `46b96bc`, the tip the receipt explicitly rejected for pulling another story's unreviewed work into acceptance. Options: (a) move the root gitlink back to `578770679b9d` and accept that automation will overwrite it again, (b) re-run the matrix at the current tip and obtain fresh maintainer acceptance, or (c) keep the pointer and amend the claim to state that the umbrella tracks `main` by design under the amended AC2, recording the delta explicitly. This is exactly the treadmill the re-scope was meant to escape, so it is an owner call. **Resolved 2026-07-28 — owner chose option (c):** keep the pointer, and amend the story and receipt to state that the umbrella tracks `main` by design under the amended AC2, recording the `578770679b9d` → `f279cb13` delta and the resulting EventStore identity difference explicitly. Converted to a patch below.
- [x] [Review][Decision] The only externally durable maintainer approval covers the pre-amendment scope — Tenants issue #32 (`jpiquot`, **0 comments**, still open, unchanged since 2026-07-27T08:01:12Z) approves scope item 1 "pin the EventStore gitlink to `fa2d1c9910f8`" and item 2 a Builds catalog at `999.1.20-proof.fa2d1c9910f8`, and lists among its **rejected alternatives** "retaining the non-authorizing EventStore `3.82.0` catalog pin". The delivered work does neither approved item and does the rejected-class thing (a published catalog pin, `3.83.0`). `receipt.md` nonetheless states the #32 approval "stands … and it supersedes nothing in that boundary." Combined with AC5's SHA acceptance having no external record (`gh search issues "578770679b9d" --owner Hexalith` → `[]`), the entire *post-amendment* maintainer authority chain is repository-internal, in a repo where every commit carries the same git identity. Options: obtain a fresh external approval bound to the amended scope + accepted SHA, or record an explicit owner decision that the in-repo record suffices. **Resolved 2026-07-28 — owner chose to accept the in-repository record as sufficient**, and to correct the receipt's inaccurate "supersedes nothing in that boundary" claim about issue #32. Converted to a patch below.
- [x] [Review][Decision] The architect ratification the approved change proposal assigned was never recorded — SCP §5 assigns "Winston (Architect) — Ratify the AD-22 scoped exception text and confirm the Parties 8.6 non-extension sentence is sufficient". `grep -rn "Winston"` across the story file and every `evidence/story-2-12/*.md` returns nothing. The exception is already committed to `architecture.md:308` and the story advanced to `review` without it. The exception text itself reads as correctly scoped on inspection, so this is a process gate, not a content defect: either obtain the ratification before `done`, or record that the owner's SCP approval subsumes it. **Resolved 2026-07-28 — owner decided the SCP approval subsumes the architect ratification**; the assigned-but-unperformed step is closed by written note rather than by a separate Winston pass. Converted to a patch below.
- [x] [Review][Patch] Record the three owner decisions above in the story and receipt: (D1) the umbrella tracks `main` by design under the amended AC2 — replace the false "root gitlink already equals `578770679b9d…`" claim with the recorded `578770679b9d` → `f279cb13` delta and its EventStore identity difference (`c8c70030` → `49987454`); (D2) the in-repository AC5 record is accepted as sufficient and issue #32's "supersedes nothing" claim is corrected to state the amendment replaced most of that boundary; (D3) the owner's SCP approval subsumes the assigned architect ratification [_bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/receipt.md:251]
- [x] [Review][Patch] All 17 "support-safe lane logs" bound as AC3/AC5 evidence are gitignored and were never committed [_bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/logs/]
- [x] [Review][Patch] AC4's durable guard is XML-shape-only and hardcoded to two package IDs, so it cannot reject "any EventStore project resolved in Release/package mode" [references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/PackageGovernanceTests.cs:126] — **Applied 2026-07-28 with explicit owner approval to edit the submodule. NOT YET COMMITTED OR PUBLISHED in Hexalith.Tenants.** `EventStore_host_dependencies_follow_one_complementary_source_package_policy` now enumerates every `Hexalith.EventStore*` reference in the domain host instead of resolving four literal names, asserts set-equality of the project and package id sets, detects duplicates, and reads the **effective** condition (item plus every ancestor `ItemGroup`). A new `No_EventStore_project_reference_is_reachable_in_package_mode` extends the rule to all owned projects: every EventStore `ProjectReference` must be gated on source intent (`HexalithEventStoreFromSource` or `UseHexalithProjectReferences`), and no EventStore `PackageReference` may carry a version as attribute **or** child element. Both guard against vacuous passes. Red/green proof at Tenants `85e24d5` in an isolated lane: baseline 119/119; patched 120/120; with an unconditional `Hexalith.EventStore.Admin.Server` `ProjectReference` injected into the host the **old** suite still passed 119/119 while the patched suite failed both tests with exact messages — confirming the original guard could not see the mixed graph AC4 forbids.
- [x] [Review][Patch] `ac2-guard.sh` assertion 5 ("only root-declared submodules are initialized") is a tautology that can never fail, yet the receipt reports it as PASS [_bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/ac2-guard.sh:47]
- [x] [Review][Patch] `analyze-assets.py` has three silent-pass paths: a lane resolving zero EventStore edges prints `ASSETS_OK`, the exact-version gate is optional, and a mis-cased mode disables both mode assertions [_bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/analyze-assets.py:14,76-79,101]
- [x] [Review][Patch] The "zero project edges, including transitive" proof reads `libraries`, which structurally cannot see `ReferenceOutputAssembly="false"` ProjectReferences — and three such EventStore references exist in the AppHost [_bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/analyze-assets.py:43-52]
- [x] [Review][Patch] The Release lane compiled 13 EventStore projects from source, and "every `project.assets.json`" is 17 of 45 [_bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/receipt.md:140]
- [x] [Review][Patch] No deferred-work entry exists for the follow-up the change proposal named, and the receipt states "Open Items: None for this story" [_bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/receipt.md:296]
- [x] [Review][Patch] AD-12's persisted-path requirement — explicitly preserved by the AD-22 exception — is discharged by assertion, naming no persisted-path test and quoting no result [_bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/receipt.md]
- [x] [Review][Patch] The three new proof-packet regressions assert packet *text*, not verifier *behaviour*, though the file already has a working `bash` execution harness [tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs:362-408]
- [x] [Review][Patch] The repaired deferred-work AWK gate counts sections without tracking which, so a duplicated heading plus a missing one still totals 3 and exits 0 [_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md:4322]
- [x] [Review][Patch] `setup-lane.sh` hardening: unguarded `rm -rf -- "$DEST"`, `$2` accepted without full-SHA verification or a post-checkout assertion, submodule path derived from name rather than the declared path, and no `objects/info/alternates` assertion despite the receipt's isolation claim resting on it; all three scripts are committed `100644` though documented as directly executed [_bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/setup-lane.sh:24,27,32]
- [x] [Review][Patch] Evidence-document corrections: `prerequisites.md:4` still opens "Overall status: `blocked`" while `:233` says `superseded`; the receipt's rejected-alternative rationale for declining `46b96bc` applies verbatim to the accepted `5787706` (itself a Story 1.6 docs commit, 26 commits and ~2443 insertions past the baseline); `architecture.md:308` attributes five pin overwrites to "a `/pushall` merge and recurring `build(deps)` bumps" when only 2 of the 5 are `build(deps)` and the named merge is not among them; no CI URL is bound in the AC5 approval although the subtask requires it and `release / release` is **failing** on the accepted SHA (pre-existing — it fails on `f279cb13` too); and the File List claims Builds and gitlink files as story-owned while every scope statement says no gitlink was changed
- [x] [Review][Defer] No blocking CI job restores, builds, or tests the source lane, so the source half of the new Gateway conditional is never evaluated [references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj:20-22] — deferred, pre-existing; Tenants-repo CI work outside this story's scope
- [x] [Review][Defer] Nothing durably detects EventStore gitlink drift or a wrong-but-resolvable catalog version; the amended AC2/AC3 gate lives only in hand-run scripts [_bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/ac2-guard.sh] — deferred, pre-existing; the approved SCP §5 explicitly placed the Tenants CI reachability check out of scope as a candidate follow-up

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

- 2026-07-27 (Claude Opus 5, third session — first run under the amended criteria) — The complete
  official-main A/B/C verifier was re-run from EventStore `main` `49987454`: **exit 0**. Authority
  fields were read from the verified commit blobs: commit A carries `still blocked` / `false` and
  authorizing commit C carries `available` / `true`, with `tested_runtime_sha` ==
  `candidate_source_sha` == `fa2d1c9910f8…` in both. All committed evidence manifest hashes `OK`.
  The Story 1.20 *source* and *NuGet* consumer procedures were deliberately **not** re-run as
  written: their frozen-SHA equality and approved-byte inputs are exactly what the amended AC2/AC3
  and the AD-22 scoped exception retire. The source procedure's cleanliness assertions were
  preserved verbatim in a purpose-built AC2 guard.
- 2026-07-27 — **Two genuinely separate, mutually isolated Tenants working copies** were created —
  the condition the previous session recorded as unmet. Each is a fresh clone of the canonical
  GitHub remote detached at the accepted SHA, with root-declared submodules initialized one at a
  time (never `--recursive`, never `--remote`, no nested submodule). Local reference repositories
  were used with `--dissociate`, and the absence of any `objects/info/alternates` in either tree
  confirms neither copy shares an object store with the other or with the umbrella.
- 2026-07-27 — **Amended AC2 proved on pristine checkouts, before either restore.** Both lanes:
  gitlink == submodule checkout `HEAD` == `c8c7003052a7f811d3b821f3442379ca5f3a9c65`, reachable
  from EventStore `origin/main` `49987454`; consumer worktree clean; EventStore submodule clean
  tracked, untracked, **and ignored**; initialized submodule set exactly equal to the root-declared
  set. Re-checked after both lanes finished: gitlink and checkout unchanged, zero tracked
  modifications in the submodule.
- 2026-07-27 — **Amended AC3 proved.** Builds `1b1c0b0` declares a single
  `HexalithEventStoreVersion` `3.83.0` with thirteen central `PackageVersion` entries covering all
  eleven packages Tenants resolves, and sets `CentralPackageVersionOverrideEnabled=false`. Tenants
  contributes no version authority: no local `Directory.Packages.props` entry, no `Version`
  attribute, no `VersionOverride`, no fallback property. The only `Version=` occurrences are
  `AdditionalProperties="Version=$(HexalithEventStoreVersion)"` on **`ProjectReference`** items,
  which is source-mode assembly metadata rather than package-version authority. `3.83.0` is in the
  nuget.org flat-container index for every consumed package, and the package lane restored into a
  fresh isolated `--packages` directory so resolvability is proved by real download, not a warm
  cache.
- 2026-07-27 — **Both evaluated graphs parsed from every `project.assets.json`** (17 per lane, under
  `src/`, `tests/`, and `samples/`), after that lane's own `--force-evaluate` restore. Source: 60
  EventStore edges, all `type: project`, 0 packages, 0 resolving outside the validated checkout.
  Package: 61 edges, **0 `type: project`**, 61 `type: package`, resolved version set exactly
  `['3.83.0']`. Because the check covers every library entry rather than direct references only,
  the zero-project-edge result includes transitive edges. `src/Hexalith.Tenants` resolves Gateway
  and DomainService identically in both directions, so no mixed graph is reachable.
- 2026-07-27 — An analyzer defect was caught and fixed before any conclusion was drawn from it: the
  first assets parse resolved `msbuildProject` relative to the `obj/` directory instead of the
  project directory, reporting all 60 source-lane project edges as "outside the validated
  checkout". The raw value (`../../references/Hexalith.EventStore/...`) confirmed the off-by-one;
  after the fix the count is 0. The 60-vs-61 edge asymmetry between lanes was likewise run to
  ground rather than waved through: it is entirely `Hexalith.EventStore.RestApi.Generators` in
  `Hexalith.Tenants.Api`, an `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`
  `ProjectReference` in source mode (not recorded as an assets library) versus a
  `PrivateAssets="all"` `PackageReference` in package mode (which is).
- 2026-07-27 — **Full dual-mode matrix green at the accepted SHA.** Restore exit 0 and build
  `--warnaserror` **0 Warning(s) / 0 Error(s)** in each lane; Contracts 115/115, Server 738/738,
  UI 1276/1276, Integration 167 passed / 1 skipped / 0 failed — identical counts in both modes. UI
  is 1276 rather than the prior 1266 because Tenants `main` gained ten UI tests in between. The
  single skip is the pre-existing environment-gated
  `SnapshotPerformanceTests.ColdStartRehydration_CompletesWithin30Seconds_With500KEvents`. The
  guard tests the ACs depend on were confirmed present in the code that ran — the
  `PackageGovernanceTests` Gateway/DomainService host rule, and the AD-18 handler-order and
  no-local-handler guards in `TenantsApiStructuralTests`/`TenantsApiGatewayHandlerTests`.
- 2026-07-27 — **The pin drifted again mid-session, as predicted, and it did not invalidate the
  run.** Tenants `main` advanced from `5787706` to `46b96bc` (EventStore gitlink `c8c70030` →
  `49987454`) while the matrix was running. That delta is not a bare submodule bump: it carries
  three commits of unrelated in-flight work, including edits to `PackageGovernanceTests.cs`.
  `5787706` was therefore kept as the acceptance target — it is published and reachable on Tenants
  `origin/main`, it is the exact content the matrix ran against, and it is already the commit the
  EventStore umbrella points to. Chasing each new tip would both be unverifiable and pull another
  story's unreviewed work into this story's acceptance. Root pointer guards were rerun: all seven
  `references/*` gitlinks are reachable on their own remotes and no nested submodule is
  initialized.

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

- **2026-07-27 third-session disposition — four of five acceptance criteria now have durable
  evidence at a single accepted Tenants SHA.** Working from
  `578770679b9d3bc3fdf2a8a78190f24cdad8576e` (EventStore `c8c70030`, Builds `1b1c0b0` → `3.83.0`):
  **AC1** re-verified by the complete official-main A/B/C verifier at exit 0 with every authority
  field read from verified commit blobs; **AC2** proved on pristine checkouts before any restore,
  with the exact validated EventStore SHA recorded; **AC3** proved against the published catalog
  with all eleven consumed packages resolving `type: package` at exactly `3.83.0`, zero project
  edges including transitive, and no consumer-local version authority anywhere; **AC4** proved
  structurally in both directions — Gateway and DomainService resolve identically, so no mixed
  graph is reachable — and behaviourally by the green `PackageGovernanceTests` host rule.
- **The two conditions that blocked the previous session are both closed by the amendment, not by
  a workaround.** The frozen-pin treadmill is resolved because AC2 now tracks `main`; the
  unrecoverable proof bytes no longer matter because AC3 now validates a published catalog
  version. Nothing was rebuilt, substituted, or reconstructed to stand in for the retired
  artifacts, and the retired 14-package manifest was not touched.
- **The previously unmet lane-isolation requirement is now genuinely met.** Two separate clean
  clones served the two modes, with no shared object store and no shared assets file — not one
  working copy restored twice.
- **AC5 is the sole remaining gate, and it is an owner action.** Every technical requirement it
  names is satisfied and recorded: registrations preserved, both focused matrices green, evidence
  persisted under the accepted SHA, and the EventStore root gitlink already equal to that SHA with
  its pointer guards rerun. What is missing is the Tenants maintainer's explicit approval bound to
  this exact SHA and its evidence. The Tenants issue #32 scope approval stands, but acceptance of
  a specific final SHA is a separate approval that this session cannot self-certify.
- No source file, test, production policy, dependency identity, or published repository state was
  changed in this session, in any repository. Nothing was pushed. No fixture was adjusted and no
  guard was weakened to make a lane pass.
- **2026-07-28 — AC5 accepted; story advanced to `review`.** Maintainer `jpiquot` accepted
  `578770679b9d3bc3fdf2a8a78190f24cdad8576e` with the scope, evidence bindings, and rejected
  alternative recorded in the receipt. One caveat is recorded rather than glossed: unlike the
  EventStore owner, release-owner, and Builds PR approvals earlier in this story, this acceptance
  has **no external GitHub record** — it is durable in the repository only, because the maintainer
  chose that channel when offered both. A reviewer verifying the authority chain from outside the
  repository should expect to find nothing third-party for this one link.
- All 44 subtasks are checked and every acceptance criterion has durable evidence. `done` remains
  gated on independent review, which should confirm both dependency modes and the maintainer
  authority chain — ideally with a different model than the one that produced this evidence.

### File List

- _bmad-output/implementation-artifacts/2-12-tenants-runtime-identity-adoption-and-package-mode-validation.md
- _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
- _bmad-output/implementation-artifacts/evidence/story-2-12/prerequisites.md
- _bmad-output/implementation-artifacts/evidence/story-2-12/source-lane-2026-07-27.md
- _bmad-output/implementation-artifacts/evidence/story-2-12/dual-mode-2026-07-27.md
- _bmad-output/implementation-artifacts/evidence/story-2-12/rescope-decision-2026-07-27.md
- _bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/receipt.md
- _bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/ac2-guard.sh
- _bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/analyze-assets.py
- _bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/setup-lane.sh
- _bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/logs/ (17 support-safe lane logs)
- _bmad-output/implementation-artifacts/sprint-status.yaml
- tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs

**Files this story consumed but did not change** (corrected 2026-07-28 by code review — these were
previously listed above as if they were story-owned edits, contradicting every Scope Statement in the
receipt, `dual-mode-2026-07-27.md`, and `source-lane-2026-07-27.md`, all of which state "No gitlink
was changed in any repository" and "No Builds gitlink was changed"):

- `references/Hexalith.Tenants/references/Hexalith.EventStore` — read as evidence; moved by Tenants'
  own automated `build(deps)` bumps, never by this story.
- `references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj` — the conditional
  Gateway pair, published on Tenants `main` as `a7ca142` before this story's validation sessions.
- `references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/PackageGovernanceTests.cs` —
  the AC4 host rule, published in the same `a7ca142`.
- `references/Hexalith.Builds/Props/Directory.Packages.props` and
  `references/Hexalith.Builds/Tools/test-authoritative-package-catalog.ps1` — owned by Hexalith.Builds
  and changed under its own release change control (PR #47), not by a Tenants or EventStore commit
  in this story.

**Added by the 2026-07-28 code review:**

- `.gitignore` — evidence-log negation so `evidence/**/logs/` is no longer silently excluded.
- `_bmad-output/implementation-artifacts/deferred-work.md` — two deferred entries.
- `_bmad-output/implementation-artifacts/evidence/story-2-12/prerequisites.md` — header status correction.
- `_bmad-output/planning-artifacts/architecture.md` — AD-22 pin-overwrite attribution correction.
- `_bmad-output/implementation-artifacts/evidence/story-2-12/578770679b9d3bc3fdf2a8a78190f24cdad8576e/`
  — receipt corrections, the three hardened lane scripts (now `100755`), and the 17 previously
  untracked lane logs.

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
- 2026-07-27 — **Dual-mode matrix re-run under the amended criteria and AC1-AC4 closed.** Accepted
  Tenants SHA `578770679b9d3bc3fdf2a8a78190f24cdad8576e`; validated EventStore SHA
  `c8c7003052a7f811d3b821f3442379ca5f3a9c65`; Builds `1b1c0b0` → published catalog `3.83.0`.
  Re-ran the official-main A/B/C verifier (exit 0, pins from verified blobs); proved tracked source
  identity on two pristine checkouts before any restore; proved the published catalog is resolvable
  by real download into an isolated packages directory; parsed all 17 `project.assets.json` per
  lane from two **separate clean clones**, closing the previously partial isolation subtask
  (source 60/60 project, 0 package; package 61 package at exactly `3.83.0`, 0 project); and ran the
  full compatibility matrix green in both modes (115 / 738 / 1276 / 167+1 skipped, build
  `--warnaserror` 0 W / 0 E). Persisted the receipt, scripts, and logs under
  `evidence/story-2-12/<accepted-tenants-sha>/`, and reran the EventStore root pointer guards — the
  root `references/Hexalith.Tenants` gitlink already equals the accepted SHA, so no pointer change
  was made. Recorded that Tenants `main` drifted again mid-run (`5787706` → `46b96bc`) and why the
  validated SHA remains the correct acceptance target. AC5's maintainer approval bound to this
  exact SHA is the sole open gate. No code, test, dependency identity, or published repository
  state changed; nothing was pushed.
- 2026-07-28 — **AC5 accepted and story advanced to `review`.** Maintainer `jpiquot` accepted the
  exact Tenants SHA `578770679b9d3bc3fdf2a8a78190f24cdad8576e`, its scope, and its bound evidence,
  choosing a direct in-repository record over a GitHub comment; the absence of an external record
  for this one approval is stated explicitly in both the receipt and the completion notes. All 44
  subtasks are now checked, every acceptance criterion has durable evidence, and story status moved
  `in-progress` → `review` in the story file and `sprint-status.yaml`. No code, test, dependency
  identity, or published repository state changed; nothing was pushed.
