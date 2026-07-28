# Story 2.12 Delta Identity Receipt — Tenants `f9e51c66745557da4f267ab40f32294f2f27fae7`

Date: 2026-07-28
Scope: **focused delta**, authorized by the owner on 2026-07-28.

This receipt does **not** stand alone. It records what changed between the previously accepted
Tenants SHA `578770679b9d3bc3fdf2a8a78190f24cdad8576e` and this one, re-proves everything the
change could have invalidated, and explicitly carries the rest forward by reference from
`../578770679b9d3bc3fdf2a8a78190f24cdad8576e/receipt.md`. Read both.

## Why This Receipt Exists

The 2026-07-28 adversarial code review rated the AC4 durable guard a HIGH finding: the
`PackageGovernanceTests` host rule was XML-shape-only and hardcoded to two package IDs, so it could
not reject "any EventStore project resolved in Release/package mode". The patch that fixes it lives
in `Hexalith.Tenants` and was, at review time, an **uncommitted working-tree edit**. The story was
returned to `in-progress` for exactly that reason.

That patch is now committed and published as Tenants
`f9e51c66745557da4f267ab40f32294f2f27fae7` (`feat(tests): enhance PackageGovernanceTests with
EventStore reference validation`, +162 / −30). The accepted SHA `578770679b9d` does **not** contain
it. Acceptance therefore has to move to a SHA that does — which changes the tracked source identity
and requires AC2, AC3 and AC4 to be re-proved at the new SHA.

## Identity At This SHA

| Fact | Value |
| --- | --- |
| Accepted Tenants SHA | `f9e51c66745557da4f267ab40f32294f2f27fae7` |
| Published | yes — equals Tenants `origin/main` before, during, and after this run |
| EventStore gitlink == checkout | `150216c3831370146814fc23d6b1437e3c97a6d5` |
| Reachable from EventStore `origin/main` | yes, at `e7de0da91f7fe0e947d1b37dfb7554761eca9fa7` |
| Builds gitlink | `53d53ae42abf7c87d385a078ab260531480bbf8a` |
| Resolved catalog version | `3.83.0` (**unchanged** from the prior receipt) |
| EventStore umbrella `references/Hexalith.Tenants` | `f9e51c66…` — equals this SHA at the time of writing |

### Delta from the previously accepted SHA

| | `578770679b9d` (prior) | `f9e51c66…` (this) |
| --- | --- | --- |
| EventStore gitlink | `c8c70030` | `150216c3` |
| Builds gitlink | `1b1c0b0` | `53d53ae` |
| `HexalithEventStoreVersion` | `3.83.0` | `3.83.0` — **unchanged** |
| AC4 guard | 2 hardcoded package IDs, XML-shape only | enumerated + set-equality + effective-condition + reachability |
| Contracts.Tests | 115 | 120 |

Because the catalog version did not move, the package lane's *identity* is the same one already
proved resolvable by real download. What genuinely changed, and is re-proved below, is the tracked
source identity, the guard, and both evaluated graphs.

## AC2 — Tracked Source Identity, Re-proved

`ac2-guard.sh` (sha256 `2ca8e48774e20fba6eca3e3d88e09aeafb8e12ef80924e93ff42b557a700a073`, reused
unchanged from the prior directory) ran on **each lane's pristine checkout, before that lane's
restore**. That ordering is load-bearing: MSBuild writes ignored `obj/` artifacts into the
EventStore submodule and trips the `--ignored=matching` assertion after any restore or build.

Both lanes: **`AC2_GUARD_OK`, exit 0**, all six assertions —

1. gitlink `150216c3…` == submodule checkout `HEAD` `150216c3…`
2. that commit is reachable from EventStore `origin/main` `e7de0da9…` (canonical GitHub remote verified)
3. consumer worktree clean, submodules included
4. EventStore submodule clean — tracked, untracked, **and ignored**
5. initialized submodule set exactly equals the root-declared set; no nested submodule initialized
6. Builds gitlink present and 40-hex: `53d53ae…`

Re-checked **after** both lanes finished: gitlink and checkout unchanged in both copies, `0` tracked
modifications in the EventStore submodule (`logs/post-lane-identity.txt`).

Evidence: `logs/ac2-guard-src-lane.log`, `logs/ac2-guard-pkg-lane.log`, `logs/post-lane-identity.txt`.

### Honest note — the lanes no longer validate identical EventStore code

The prior review recorded an unclaimed strength: `git rev-list -n1 v3.83.0` →
`c8c7003052a7f811d3b821f3442379ca5f3a9c65`, i.e. the published catalog version validated by the
package lane had been tagged from *exactly* the EventStore SHA validated by the source lane.

**That coherence does not survive this delta and must not be assumed to.** At this SHA the source
lane validates `150216c3`, which is **3 commits ahead of `v3.83.0`**:

- `49987454` — re-scope decision record and sprint change proposal for Story 2.12 (docs)
- `57143dd3` — sprint status and evidence update for Story 2.12 (docs)
- `150216c3` — `feat: add IReadModelBulkStore interface and implement bulk read functionality in DaprReadModelStore` (**functional code**)

The two lanes therefore exercise EventStore trees that differ by one functional commit. This is
**permitted** by the amended AC2/AC3, which deliberately decoupled the two lanes' identities, and the
approved re-scope decision explicitly accepted "losing the exact-tested-runtime guarantee". It is
recorded here because the prior receipt's coincidental coherence was noted as a strength, and a
reviewer must not carry that strength forward to this SHA.

## AC3 — Published Catalog Package Identity, Re-proved

Builds `53d53ae` (`logs/ac3-catalog.txt`):

- Exactly **one** `<HexalithEventStoreVersion>` definition anywhere in the lane, in
  `references/Hexalith.Builds/Props/Directory.Packages.props:8`, value **`3.83.0`**.
- **13** central `PackageVersion Include="Hexalith.EventStore*"` entries, all
  `Version="$(HexalithEventStoreVersion)"`, including the `Hexalith.EventStore.Gateway` entry AC4
  requires (retained from the approved Builds PR #47 lineage).
- `ManagePackageVersionsCentrally=true`, `CentralPackageVersionOverrideEnabled=false`.

Zero Tenants-local version authority:

- No `<HexalithEventStoreVersion>` definition outside Builds.
- The Tenants-root `Directory.Packages.props` contains **no** `Hexalith.EventStore` entry at all; it
  only imports the Builds catalog.
- No `VersionOverride` in any Tenants-owned project (the only textual matches are inside
  `PackageGovernanceTests.cs`, which *forbids* them).
- All 13 `PackageReference Include="Hexalith.EventStore*"` items in `src/`, `tests/`, `samples/` are
  **version-less** and gated on `'$(HexalithEventStoreFromSource)' != 'true'`.
- The only `Version=` occurrences on EventStore references are
  `AdditionalProperties="Version=$(HexalithEventStoreVersion)"` on **`ProjectReference`** items gated
  on `'$(HexalithEventStoreFromSource)' == 'true'` — source-mode assembly metadata, not package
  version authority.

Resolvability proved by **real download**, not a warm cache: the package lane restored into a fresh
isolated `--packages` directory (`pkg-packages-f9e51c6`, removed and recreated for this run). Restore
**exit 0** with **0** `NU####` diagnostic lines. All **11** consumed packages were fetched at
`3.83.0` from nuget.org — the sole registered source (`dotnet nuget list source`: 1 source).

Evidence: `logs/ac3-catalog.txt`, `logs/pkg-restore.log`, `logs/pkg-downloaded-versions.txt`.

## Evaluated Dependency Graphs — The AC3/AC4 Evidence

`analyze-assets.py` (sha256 `da7a88fc67eecebf9ed7ee228cd3cbabc1e798143603845952f1df5fae50e748`,
reused unchanged) parsed **every** `project.assets.json` under `src/`, `tests/`, `samples/` in each
lane, after that lane's own `--force-evaluate` restore. **17 assets files per lane.**

| | source lane | package lane |
| --- | --- | --- |
| EventStore edges | **60** | **61** |
| `type: project` | **60** | **0** |
| `type: package` | **0** | **61** |
| resolving outside the validated checkout | **0** | n/a |
| resolved version set | n/a | exactly `['3.83.0']` |
| raw `ProjectReference` items (incl. `ReferenceOutputAssembly="false"`) | 12 | **0** |
| verdict | `ASSETS_OK mode=source` | `ASSETS_OK mode=package` |

Both gates were armed and are recorded in the log's `invocation:` line — the package run passed the
expected version `3.83.0` explicitly, so the exact-version gate could not be silently optional.

The 60↔61 asymmetry is the same one already run to ground in the prior receipt:
`Hexalith.EventStore.RestApi.Generators` in `Hexalith.Tenants.Api` is an
`OutputItemType="Analyzer" ReferenceOutputAssembly="false"` `ProjectReference` in source mode (not
recorded as an assets library) versus a `PrivateAssets="all"` `PackageReference` in package mode
(which is). The counts are identical to the prior validated run.

`src/Hexalith.Tenants` resolves Gateway and DomainService **identically in both directions** (7
project edges in source, 7 package edges in package), so **no mixed graph is reachable** — AC4's
structural requirement holds at this SHA.

**CORRECTED 2026-07-28 by the delta code review — this claim was false.** The text here previously
read: "The package lane's **0** raw `ProjectReference` items is the check that reads
`project.restore.frameworks[].projectReferences` rather than `libraries`, so 'zero project edges
including transitive' also covers the `ReferenceOutputAssembly="false"` class that `libraries`
structurally cannot represent."

NuGet omits `ReferenceOutputAssembly="false"` project references from **both** structures, so the
second parse closes nothing. Proved directly in the retained source lane at this SHA:

- `src/Hexalith.Tenants.AppHost/Hexalith.Tenants.AppHost.csproj:32` opens an
  `ItemGroup Condition="'$(Configuration)' == 'Debug' and '$(UseHexalithProjectReferences)' == 'true'"`
  — both true in the source lane — containing three EventStore `ProjectReference`s marked
  `ReferenceOutputAssembly="false"` (`Hexalith.EventStore`, `…Admin.Server.Host`, `…Admin.UI`).
- They were genuinely live: `logs/src-build.log` shows all three assemblies compiled from
  `src-lane-f9e51c6/references/Hexalith.EventStore/src/**/bin/Debug`.
- Yet that project's `project.assets.json` records only **three** `projectReferences` —
  `Hexalith.Commons.Aspire`, `Hexalith.EventStore.Aspire`, `Hexalith.Tenants.Aspire` — none of the
  three. Correspondingly `logs/src-assets.txt` attributes only `Hexalith.EventStore.Aspire` to the
  AppHost among its 12 raw items.

So the package lane's **0** carries no information about that class, and the figure must not be
cited as if it did. `analyze-assets.py` has been corrected to say so in its own output and comments.

**Where that class is actually covered:** the Tenants XML guard
`No_EventStore_project_reference_is_reachable_in_package_mode`, which reads each item's *effective*
condition including every ancestor `ItemGroup` — so it does see the AppHost's ItemGroup-gated
references and requires them to be gated on source intent. That guard, not the assets parse, is the
durable AC3/AC4 coverage for `ReferenceOutputAssembly="false"` edges. (The delta code review also
recorded four bypasses in that guard; they are deferred to a follow-up Tenants story by owner
decision — see `deferred-work.md`.)

Evidence: `logs/src-assets.txt`, `logs/pkg-assets.txt`, `logs/src-build.log`.

## AC4 — The Strengthened Guard, Green In Both Modes

`Hexalith.Tenants.Contracts.Tests`, run by project with `--no-build --no-restore` in each freshly
restored mode:

| lane | configuration | result |
| --- | --- | --- |
| source | Debug, `UseHexalithProjectReferences=true` | **Passed! Failed: 0, Passed: 120, Skipped: 0, Total: 120** |
| package | Release, `UseHexalithProjectReferences=false` | **Passed! Failed: 0, Passed: 120, Skipped: 0, Total: 120** |

120, not the prior 115 — the guard patch and the intervening Tenants work added tests. The review's
own red/green proof at Tenants `85e24d5` was baseline 119/119, patched 120/120.

**Non-vacuity proved explicitly.** A passing suite does not prove the two guard tests ran, so they
were re-run by name in both modes (`logs/ac4-guard-tests-named.txt`):

```text
--filter EventStore_host_dependencies_follow_one_complementary_source_package_policy|
         No_EventStore_project_reference_is_reachable_in_package_mode
source  → Passed! Failed: 0, Passed: 2, Skipped: 0, Total: 2
package → Passed! Failed: 0, Passed: 2, Skipped: 0, Total: 2
```

What the strengthened guard now does, per the review's applied patch: enumerates every
`Hexalith.EventStore*` reference in the domain host instead of resolving four literal names, asserts
set-equality of the project and package id sets, detects duplicates, and reads the **effective**
condition (item plus every ancestor `ItemGroup`). `No_EventStore_project_reference_is_reachable_in_package_mode`
extends the rule to all owned projects: every EventStore `ProjectReference` must be gated on source
intent, and no EventStore `PackageReference` may carry a version as attribute **or** child element.

## Builds

Solution build with `--warnaserror` in each lane:

| lane | configuration | result |
| --- | --- | --- |
| source | Debug | exit 0 — **0 Warning(s), 0 Error(s)** |
| package | Release | exit 0 — **0 Warning(s), 0 Error(s)** |

**AD-11 caveat, restored 2026-07-28 by the delta code review.** The `578770679b9d` receipt carried an
explicit note for this and the note was dropped here, even though this receipt is the story's
`final_receipt`. The Release lane's clean build is **not** a build of packages only:
`logs/pkg-build.log` shows **13** `Hexalith.EventStore.*` assemblies compiled from
`pkg-lane-f9e51c6/references/Hexalith.EventStore/src/**/bin/Release`. The EventStore submodule is
present in the lane and MSBuild builds it as part of the solution, independently of how Tenants
*resolves* its EventStore dependencies. AC3 is about resolution, and resolution is clean — all 61
edges are `type: package` at exactly `3.83.0` with zero project edges. But AD-11's "Release/package
validation may not compile a source edge" should be read against this fact, not around it: the lane
compiles EventStore source it does not consume. Removing the submodule from the package lane
entirely would be the stronger proof and is not what was done here.

Evidence: `logs/src-build.log`, `logs/pkg-build.log`, `logs/lane-driver.log`.

## Lane Isolation

Two genuinely separate clean clones of the canonical GitHub remote, each detached at
`f9e51c66…`, root-declared submodules initialized one at a time — never `--recursive`, never
`--remote`, no nested submodule. Local reference repositories used with `--dissociate`, and
`setup-lane.sh` asserts the absence of any `objects/info/alternates` rather than claiming isolation
in prose. Both lanes reported `LANE_READY … submodules=7 alternates=none`.

Neither lane reused the other's `project.assets.json`; each ran its own `--force-evaluate` restore.
The package lane used its own fresh `--packages` directory.

Evidence: `logs/setup-src.log`, `logs/setup-pkg.log`.

## Carried Forward — NOT Re-run In This Delta

The owner authorized a focused delta rather than a full matrix re-run. The following remain bound to
`../578770679b9d3bc3fdf2a8a78190f24cdad8576e/receipt.md` and were **not** re-executed at this SHA:

| suite | prior result at `578770679b9d` | status here |
| --- | --- | --- |
| `Hexalith.Tenants.Server.Tests` | 738/738 both modes | carried forward, not re-run |
| `Hexalith.Tenants.UI.Tests` | 1276/1276 both modes | carried forward, not re-run |
| `Hexalith.Tenants.IntegrationTests` | 167 passed / 1 skipped / 0 failed both modes | carried forward, not re-run |

Also carried forward unchanged: AC1's activation authority (the complete Story 1.20 official-main
A/B/C verifier, exit 0, every authority field read from verified commit blobs), the AD-22 scoped
exception and its Parties 8.6 non-extension, the retired External Prerequisite Contract and its
negative package-byte audit in `../prerequisites.md`, owner decisions D1–D3, and the architect
ratification disposition.

**The carry-forward is CLOSED — the three suites were re-run at this SHA on 2026-07-28.** See
"Carried-Forward Gap Closed" below. The table above is retained as the historical record of what the
focused delta originally carried; it is no longer the evidence for those three suites.

## Carried-Forward Gap Closed (2026-07-28 delta code review)

The delta code review found that the paragraph previously printed here understated the gap
materially, and the owner directed a re-run rather than a restatement.

**What the understatement was.** The original text attributed the whole gap to EventStore — "this
SHA's source lane is `150216c3`, three commits ahead" — and called the prior evidence "a
near-identical tree". The **Tenants** tree also moved, and by far more:

```text
git log  578770679b9d..f9e51c66   ->  16 commits
git diff --shortstat              ->  53 files changed, 4006 insertions(+), 367 deletions(-)
git diff --stat -- src/           ->  19 files changed,  451 insertions(+), 197 deletions(-)
```

Those 19 production files are on the surfaces this story's own subtask requires preserving:
`Services/Configuration/TenantConfigurationReadPolicyProvider.cs` (+161), new
`TenantConfigurationValidatedPolicy.cs`, new `Services/Gateways/TenantsBffComposition.cs`,
`Services/Gateways/TenantQueryGateway.cs` (+27), deleted `LegacyConfigurationDisplaySanitizer.cs`,
and six Razor components. `tests/Hexalith.Tenants.UI.Tests` went from 792 to 829 `[Fact]`/`[Theory]`
attributes. Neither this receipt nor the story disclosed that the Tenants tree had moved at all.

Independently, AD-22's scoped exception preserves **AD-12 unchanged**, and AD-12/NFR16 states that
compilation alone does not close high-risk compatibility — so discharging the persisted-path lane by
a `--warnaserror` build was not available as an option regardless of the carry-forward question.

**Measured results at Tenants `f9e51c66…`, both modes, `--no-build --no-restore` in the two retained
isolated lanes** (each verified to be at the accepted SHA before any suite ran; driver
`rerun-carried-suites.sh`, log `logs/rerun-driver.log`):

| suite | Debug/source | Release/package | vs. carried-forward |
| --- | --- | --- | --- |
| `Hexalith.Tenants.Server.Tests` | 738 / 738 | 738 / 738 | unchanged |
| `Hexalith.Tenants.UI.Tests` | **1325 / 1325** | **1325 / 1325** | **was 1276 — the carried-forward count was wrong** |
| `Hexalith.Tenants.IntegrationTests` | 167 passed / 1 skipped / 0 failed | 167 passed / 1 skipped / 0 failed | unchanged |

Zero failures in either mode; every lane exited 0 (`RERUN_OK`, `RERUN_ANY_FAILURE=0`). The single
skip is the pre-existing environment-gated
`SnapshotPerformanceTests.ColdStartRehydration_CompletesWithin30Seconds_With500KEvents`.

**AD-12 persisted-path evidence, named explicitly:** `Hexalith.Tenants.IntegrationTests`, 167 passed
/ 1 skipped in **both** dependency modes at the accepted SHA. That is the lane that exercises
persisted projection/query/provenance/freshness behaviour rather than asserting it; it is no longer
carried forward and no longer discharged by compilation.

The UI delta is the substantive correction: 1325 − 1276 = **49 tests that the carried-forward figure
never covered**, on exactly the configuration-policy and gateway surfaces the 16 intervening commits
changed.

## Commands

```text
# both lanes, pristine, BEFORE restore
setup-lane.sh <lane> f9e51c66745557da4f267ab40f32294f2f27fae7
ac2-guard.sh  <lane>

# source lane
dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Debug \
  -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false \
  -p:HexalithCommonsFromSource=false -nodeReuse:false -m:1
dotnet build   Hexalith.Tenants.slnx --configuration Debug --no-restore --warnaserror \
  <same -p: properties> -nodeReuse:false -m:1

# package lane (separate working copy, fresh isolated packages directory)
dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Release \
  -p:UseHexalithProjectReferences=false \
  --packages /home/administrator/tmp-story-2-12/pkg-packages-f9e51c6 -nodeReuse:false -m:1
dotnet build   Hexalith.Tenants.slnx --configuration Release --no-restore --warnaserror \
  -p:UseHexalithProjectReferences=false -nodeReuse:false -m:1

# guard suite, by project, in each freshly restored mode
dotnet test tests/Hexalith.Tenants.Contracts.Tests/Hexalith.Tenants.Contracts.Tests.csproj \
  --configuration <mode> --no-build --no-restore <matching -p:> -nodeReuse:false -m:1

# evaluated graphs
analyze-assets.py <src-lane> source
analyze-assets.py <pkg-lane> package 3.83.0
```

Every invocation ran unattended with `-nodeReuse:false -m:1`, with no `--interactive`, no ignored
source failure, and no reachable credential prompt. `-m:1` is required because parallel MSBuild
instances race on the same EventStore `.deps.json`. The full driver is retained as `run-lanes.sh`.

The three shared lane scripts were **reused unchanged** from the prior evidence directory rather than
duplicated here. These are the **as-run** hashes — the exact bytes that produced the evidence in this
directory:

```text
f4d2339892420444771fec2004b092adb348fa77505380e56507ce6817dd8b8e  setup-lane.sh      (as run)
2ca8e48774e20fba6eca3e3d88e09aeafb8e12ef80924e93ff42b557a700a073  ac2-guard.sh       (as run)
da7a88fc67eecebf9ed7ee228cd3cbabc1e798143603845952f1df5fae50e748  analyze-assets.py  (as run)
```

**The scripts were hardened after this evidence was produced** (2026-07-28 delta code review), so the
files on disk no longer match the hashes above. That is intentional and the distinction is preserved
rather than papered over: the hashes above identify the instruments that produced these logs; the
hashes below identify the instruments a future lane will use.

```text
61ee8d563f09f07492b76c4fcb29dceb5df7d1a663ab6b155ab320d856dbe30a  setup-lane.sh      (hardened)
e1ca51e23f44b5692800aca1b859d076e230e5ef05322d334269ddef7ef72a9d  ac2-guard.sh       (hardened)
d8cf1a314a12098edf6d0c9ebb667e6c566ad1f1b2c52bf66336376f18853c00  analyze-assets.py  (hardened)
```

What the hardening changed, and why none of it invalidates the recorded results: `setup-lane.sh` now
canonicalizes its destination before the scratch-prefix check (the old `case` glob accepted a `..`
traversal out of the scratch tree, and the "is it a git working copy" check did not catch it because
a real repository *does* have `.git`) and distinguishes a failed alternates search from a clean one;
`ac2-guard.sh` now asserts the Builds gitlink is mode `160000` type `commit` rather than merely
40-hex; `analyze-assets.py` now exits 2 rather than 1 on a usage error, keys raw project references
by project id instead of path substring, de-duplicates them across target frameworks, accepts
`--expect-assets` / `--expect-edges` so a partial restore cannot pass, and no longer claims to cover
the `ReferenceOutputAssembly="false"` class. Re-running the hardened `analyze-assets.py` against both
retained lanes reproduces this receipt's graph results exactly, now with `--expect-assets 17`
asserted rather than eyeballed. A red/green proof for the two `ac2-guard.sh` repairs — absent when
they were made — is in `logs/ac2-guard-redgreen.txt`.

## Drift During This Run — None

For the first time in this story, the acceptance target did not move while it was being validated.
Tenants `origin/main` was `f9e51c66…` before the lanes were created and still `f9e51c66…` after they
finished. The EventStore umbrella's `references/Hexalith.Tenants` gitlink equals it.

That is a point-in-time fact, not a guarantee. The `build(deps)` automation that overwrote the
consumer pin five times earlier in this story is unchanged, and nothing durably detects the next
move — see the deferred item below.

### Partial mitigation that appeared since the prior receipt

Tenants commits `c407c9e` and `85e24d5` added `scripts/validate-story-gitlinks.py` plus
Tenants-local `_bmad/custom/bmad-dev-story.toml`, `_bmad/custom/bmad-code-review.toml`, and a
`bmad-dev-story` checklist entry. The review flagged these as possibly addressing part of the
deferred drift item; they were checked, and the accurate reading is:

- **What it does:** fails a story whose commits move a `references/` gitlink between the story's
  `baseline_commit` and HEAD without declaring it in the File List. Fail-closed on a missing
  baseline.
- **What it does not do:** it is a *story-authoring process* gate, not a CI job, and it does not
  detect EventStore gitlink drift on `main` or a wrong-but-resolvable catalog version.
- **Where it lives:** `Hexalith.Tenants` **only**. EventStore has no such script and no
  `bmad-dev-story` customization, so it did not bind this run.

Deferred item 2 in `../../deferred-work.md` therefore stays open, narrowed rather than closed.

Its substance was nonetheless applied to this story by hand
(`logs/eventstore-gitlink-delta.txt`). Between the story's `baseline_commit`
`73589770b14888b703d78d37325b066befa0689c` and EventStore HEAD
`e7de0da91f7fe0e947d1b37dfb7554761eca9fa7`, three `references/` gitlinks moved:

| submodule | baseline | HEAD | moved by |
| --- | --- | --- | --- |
| `references/Hexalith.Builds` | `4e5c2a3e` | `53d53ae4` | Builds change control upstream; **the final hop `1b1c0b03`→`53d53ae4` was committed by Story 2.12 commit `e7de0da9`** |
| `references/Hexalith.Memories` | `6e6d3fb9` | `1868c8f9` | automated `build(deps)` upstream; **the final hop `327d1a9d`→`1868c8f9` was committed by Story 2.12 commit `5a1d277e`** |
| `references/Hexalith.Tenants` | `f8aff935` | `f9e51c66` | **moved to the accepted SHA by Story 2.12 commit `e7de0da9`, as the story's own closure subtask requires** |

**CORRECTED 2026-07-28 by the delta code review.** This table previously read "automated
`build(deps)` … not this story" for all three rows and concluded "**No gitlink was moved by a Story
2.12 commit.**" That conclusion was false at the commits carrying it, and it is owner decision D1
recurring inside the commits that document D1:

```text
git diff e7de0da9^ e7de0da9 -- references/   ->  Hexalith.Builds  1b1c0b03 -> 53d53ae4
                                                 Hexalith.Tenants f279cb13 -> f9e51c66
git diff 5a1d277e^ 5a1d277e -- references/   ->  Hexalith.Memories 327d1a9d -> 1868c8f9
```

The distinction that matters: the *content* of each move originated upstream (Builds release change
control, `build(deps)` automation, and the Tenants maintainer's own commits), but the *act of
recording* the Builds and Memories hops, and the whole of the Tenants hop, was performed by Story
2.12 commits. The Tenants move is not a defect — it is exactly what the closure subtask "update the
EventStore repository's `references/Hexalith.Tenants` gitlink only to that exact accepted Tenants
SHA" requires, and it lands on the accepted SHA. The Builds and Memories hops were absorbed
incidentally by evidence commits. All three targets are reachable on their own remotes; verified
2026-07-28. Neither `Hexalith.Memories` nor the Builds hop changes what EventStore builds — Builds
`53d53ae4` still declares the same catalog version `3.83.0` this story validated.

## Scope Statement

**Scoped to the delta session as authored, then corrected 2026-07-28 by the delta code review.** The
original text claimed "No gitlink was changed in any repository" and enumerated "the complete set of
writes" as this evidence directory, the story file, and one `sprint-status.yaml` line. Both
statements were false of the commits that published this receipt, so they are replaced:

- **Gitlinks did change in the EventStore umbrella.** `e7de0da9` moved `references/Hexalith.Builds`
  and `references/Hexalith.Tenants`; `5a1d277e` moved `references/Hexalith.Memories`. See the
  corrected declared-gitlink table above for what each move means.
- **The write set was larger than enumerated.** `e7de0da9` also rewrote `.gitignore`,
  `deferred-work.md`, `_bmad-output/planning-artifacts/architecture.md`, `../prerequisites.md`, the
  Story 1.20 proof packet, `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ProofPacketValidatorIntegrityTests.cs`,
  and the three shared lane scripts.

What remains true, and was re-verified on 2026-07-28: **no package identity was adopted or altered**
(Builds `53d53ae4` still resolves the same catalog version `3.83.0`); **no EventStore submodule
content was edited**; **no `Version`, `VersionOverride`, fallback property, or local
`PackageVersion` entry was added to Tenants**; **no nested submodule was initialized** and no
recursive or remote submodule update was used; and **no production policy, guard, or fixture was
weakened, and no test was adjusted to make a lane pass**. No Tenants or EventStore *product* source
file was modified by this story in any repository.

## AC5 — Tenants Maintainer Acceptance

- Approver: `jpiquot` (Tenants maintainer / release owner)
- Approval date: `2026-07-28`
- Approval channel: direct maintainer decision recorded in this receipt and in the story's Dev Agent
  Record. **Same channel caveat as the prior acceptance** — see "Channel durability" below.
- Accepted Tenants SHA: `f9e51c66745557da4f267ab40f32294f2f27fae7`
- Accepted scope:
  - the tracked EventStore source identity `150216c3831370146814fc23d6b1437e3c97a6d5` under the
    amended AC2, reachable from EventStore `origin/main` `e7de0da9…`;
  - the published Builds catalog identity `53d53ae4…` → `3.83.0` under the amended AC3, with zero
    consumer-local version authority and all 11 consumed packages resolved by real download;
  - the **strengthened** `PackageGovernanceTests` host rule and its new
    `No_EventStore_project_reference_is_reachable_in_package_mode` reachability rule under AC4,
    green and provably non-vacuous in both dependency modes;
  - the **focused-delta** compatibility scope under AC5 — explicitly including the carried-forward
    gap documented in "Carried Forward — NOT Re-run In This Delta" above.
- Bound evidence: this receipt, the 18 support-safe lane logs in `logs/`, and the lane driver
  `run-lanes.sh` in this directory; plus the three shared lane scripts in
  `../578770679b9d3bc3fdf2a8a78190f24cdad8576e/`, bound by the sha256 values recorded above.
- Rejected alternatives, both offered and both declined by the owner on 2026-07-28:
  1. **Full dual-mode matrix re-run at `f9e51c6`** — declined in favour of the focused delta, on the
     grounds that the catalog identity did not move (`3.83.0` in both receipts), so the package
     lane's identity was already proved by real download, and what genuinely changed — tracked
     source identity, the guard, and both evaluated graphs — is exactly what this delta re-proves.
  2. **Keeping `578770679b9d` as the accepted SHA** and filing the guard strengthening as a
     follow-up — declined because it would leave AC4's durable guard living at a SHA that no
     acceptance covers, which is the defect the code review raised in the first place.
- Prior approvals retained, not superseded: the Story 1.20 EventStore-owner and release-owner
  approvals (AC1 activation authority), Builds PR #47 comment `5088870151` (the surviving central
  Gateway catalog entry AC4 requires), and the `578770679b9d` acceptance, which remains the binding
  record for the three carried-forward suites.
- **CI at the accepted SHA — bound 2026-07-28 by the delta code review.** This entry previously read
  "not separately re-queried for this delta … expected to be unchanged in kind", which left AC5's
  "bind its CI/evidence URLs" subtask unmet on an assumption. The check was one API call and it is
  **green**. `gh api repos/Hexalith/Hexalith.Tenants/commits/f9e51c66745557da4f267ab40f32294f2f27fae7/check-runs`:

  ```text
  ci / build-and-test        success
  ci / aspire-tests          success
  codeql / analyze           success
  commitlint / commitlint    success
  ci / performance-tests     skipped
  ```

  Note this supersedes the pessimistic expectation carried from the prior receipt: no check run on
  the accepted SHA is failing. `release / release` does not appear as a check run on this commit —
  it is a `main`-push workflow, not a per-commit check — so the prior receipt's `release / release`
  observation neither applies to nor blocks this SHA.

**Channel durability.** Every approval in this story except the two SHA acceptances has an external
GitHub record (EventStore issue comment `5083143163`, release-owner comment `5083164122`, Builds
PR #47 comment `5088870151`). This acceptance, like the `578770679b9d` one before it, deliberately
does not: it is durable within the repository, committed alongside the evidence it binds, but a
reviewer verifying the authority chain from outside the repository will find no third-party record
of it. Stated explicitly so the difference is visible rather than assumed.

## Regression Outside The Lanes

`Hexalith.EventStore.Contracts.Tests` — the EventStore-owned suite this story touched via
`Packaging/ProofPacketValidatorIntegrityTests.cs` — re-run at EventStore
`e7de0da91f7fe0e947d1b37dfb7554761eca9fa7`:
**Passed! Failed: 0, Passed: 778, Skipped: 0, Total: 778.** No regression.
