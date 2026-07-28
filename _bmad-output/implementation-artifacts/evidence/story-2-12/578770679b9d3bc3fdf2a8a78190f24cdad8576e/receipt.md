# Story 2.12 Final Identity Receipt — Tenants `578770679b9d3bc3fdf2a8a78190f24cdad8576e`

- Evidence date: `2026-07-27`
- Accepted Tenants SHA: `578770679b9d3bc3fdf2a8a78190f24cdad8576e`
- Validated EventStore SHA: `c8c7003052a7f811d3b821f3442379ca5f3a9c65`
- Validated Builds SHA: `1b1c0b0360715b82de48b618fc4e94e7e01e8092`
- Resolved catalog version: `3.83.0`
- Criteria applied: the **amended** AC2/AC3 from
  `../../../../planning-artifacts/sprint-change-proposal-2026-07-27-story-2-12-runtime-identity-rescope.md`
  and the dated **scoped exception** to AD-22. The frozen-SHA and byte-equality requirements
  are retired for this story and this consumer only.

The Tenants SHA and the EventStore SHA are distinct facts and are never compared.

## Working Copies

Two **separate, clean, mutually isolated** Tenants working copies, one per lane — the
condition the previous session could not satisfy:

| Lane | Path | Configuration |
| --- | --- | --- |
| source | `/home/administrator/tmp-story-2-12/src-lane` | Debug, `UseHexalithProjectReferences=true` |
| package | `/home/administrator/tmp-story-2-12/pkg-lane` | Release, `UseHexalithProjectReferences=false` |

Both were produced by `setup-lane.sh` (retained here): a fresh clone of
`https://github.com/Hexalith/Hexalith.Tenants.git` detached at the accepted SHA, then
**one submodule at a time**, never `--recursive`, never `--remote`. Local reference repos were
used with `--dissociate`, so neither copy shares an object store with any other repository —
verified by the absence of any `objects/info/alternates` file in either tree. Neither copy is a
nested submodule of the EventStore umbrella.

## AC1 — Activation Authority Revalidated

The complete official-main A/B/C verifier was extracted from the packet block
(`1-20-owner-approved-parity-closure-proof-packet.md`, lines 3058-5099) and executed from the
EventStore repository root at `main` `49987454bb1a363e557347cf69ae0940b5c3f334`.

```text
EVIDENCE_COMMIT_A=b695ad3215cd873c41561635e4eb4d7ff29d56a2
POINTER_COMMIT_B=ed48057e9bf9cb5e5e8667fec84f7c70e4534eea
AUTHORIZATION_COMMIT_C=1b219d39cfa8f0349175c356001ba539bfb4aa92
AUTHORIZATION_VERIFICATION_PHASE=official-main
VERIFIER_EXIT=0
```

Every authority field was read from the **verified commit blobs**, not from story prose:

| Field | Source blob | Value |
| --- | --- | --- |
| `final_decision` | authorizing commit C | `available` |
| `authorize_consumer_migration` | authorizing commit C | `true` |
| `tested_runtime_sha` | commits A and C (equal) | `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` |
| `candidate_source_sha` | commit A | `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` |
| `approved_package_version` | commits A and C | `999.1.20-proof.fa2d1c9910f8` |

Commit A correctly still carries `still blocked` / `false`; only verified C performs the
transition, exactly as the packet's ancestry contract specifies. All committed evidence manifest
hashes verified `OK`. The AWK defect fixed earlier in this story did not recur.

**Why the Story 1.20 consumer procedures are not the gate here.** Its *source consumer
procedure* asserts `GITLINK_SHA == fa2d1c9910f8…`, and its *NuGet consumer procedure* requires a
directory holding the 14 approved `.nupkg` files. Both are exactly what the amended AC2/AC3 and
the AD-22 scoped exception replace. The cleanliness half of the source procedure is **retained
verbatim** in `ac2-guard.sh` below; the frozen-SHA equality is replaced by the tracked-`main`
check. The NuGet procedure has no applicable input, since the approved bytes were proved
unrecoverable (`../prerequisites.md`).

## AC2 — Tracked Source Identity

`ac2-guard.sh` ran on each **pristine checkout, before that lane's restore**. This ordering is
load-bearing: MSBuild writes ignored `obj/` artifacts into the EventStore submodule and would
trip the `--ignored=matching` assertion after any restore or build.

| Assertion | src-lane | pkg-lane |
| --- | --- | --- |
| gitlink == submodule checkout `HEAD` | PASS | PASS |
| checkout reachable from EventStore `origin/main` | PASS | PASS |
| EventStore submodule origin is the canonical GitHub remote | PASS | PASS |
| consumer worktree clean (`--untracked-files=all --ignore-submodules=none`) | PASS | PASS |
| EventStore submodule clean (tracked + untracked) | PASS | PASS |
| EventStore submodule clean **including ignored** | PASS | PASS |
| initialized submodule set == root-declared set | PASS | PASS |
| no nested submodule initialized | PASS | PASS |

```text
AC2_GUARD_OK
TENANTS_SHA=578770679b9d3bc3fdf2a8a78190f24cdad8576e
EVENTSTORE_GITLINK_SHA=c8c7003052a7f811d3b821f3442379ca5f3a9c65
EVENTSTORE_CHECKOUT_SHA=c8c7003052a7f811d3b821f3442379ca5f3a9c65
EVENTSTORE_ORIGIN_MAIN=49987454bb1a363e557347cf69ae0940b5c3f334
BUILDS_GITLINK_SHA=1b1c0b0360715b82de48b618fc4e94e7e01e8092
```

**The exact EventStore SHA the validation matrix was run against is
`c8c7003052a7f811d3b821f3442379ca5f3a9c65`.** No gitlink was restored, re-pinned, or frozen; the
automated `build(deps)` bump remains the mechanism, and this receipt is bound to the SHA it
names. After both lanes completed, gitlink and checkout still equalled `c8c70030` with zero
tracked modifications in the submodule — no EventStore content was edited.

## AC3 — Published Catalog Package Identity

The Tenants-pinned Builds commit `1b1c0b0` declares one version variable and puts every consumed
package under it:

```text
<HexalithEventStoreVersion Condition="'$(HexalithEventStoreVersion)' == ''">3.83.0</HexalithEventStoreVersion>
```

Thirteen central `PackageVersion` entries reference `$(HexalithEventStoreVersion)` and cover all
eleven `Hexalith.EventStore*` assets Tenants actually resolves. The same file sets
`CentralPackageVersionOverrideEnabled=false`, so `VersionOverride` is structurally unavailable.

**No consumer-local version authority exists.** Tenants declares no `Directory.Packages.props`
entry, no `Version` attribute, no `VersionOverride`, and no fallback property for any
`Hexalith.EventStore*` package. The only `Version=` occurrences in Tenants are
`AdditionalProperties="Version=$(HexalithEventStoreVersion)"` on **`ProjectReference`** items —
source-mode assembly-version metadata, not package-version authority.

**Resolvable from the configured public source.** The only registered source is
`nuget.org` (`https://api.nuget.org/v3/index.json`). `3.83.0` is present in the flat-container
index of every consumed package, and the package lane restored into a **fresh isolated
`--packages` directory**, so every `.nupkg` was genuinely downloaded from nuget.org rather than
served from a warm global cache:

```text
hexalith.eventstore.admin.abstractions   3.83.0    hexalith.eventstore.restapi.generators  3.83.0
hexalith.eventstore.aspire               3.83.0    hexalith.eventstore.server              3.83.0
hexalith.eventstore.client               3.83.0    hexalith.eventstore.servicedefaults     3.83.0
hexalith.eventstore.contracts            3.83.0    hexalith.eventstore.testing             3.83.0
hexalith.eventstore.domainservice        3.83.0    hexalith.eventstore.testing.integration 3.83.0
hexalith.eventstore.gateway              3.83.0
```

Restore exited 0 with no `NU*` diagnostic.

## Evaluated Dependency Graphs — The AC3/AC4 Evidence

Both graphs were parsed by `analyze-assets.py` from **every** `project.assets.json` under
`src/`, `tests/`, and `samples/` after that lane's own `--force-evaluate` restore. Seventeen
assets files were evaluated per lane; no prior assets file was reused, and each lane has its own
working copy so no cross-mode contamination is possible.

| Lane | assets files | EventStore edges | `type: project` | `type: package` | outside validated checkout |
| --- | --- | --- | --- | --- | --- |
| source (Debug) | 17 | 60 | **60** | **0** | **0** |
| package (Release) | 17 | 61 | **0** | **61** | — |

Package lane resolved package version set: **`['3.83.0']`** — a single value, exactly the pinned
catalog version, with no second version anywhere in the graph.

`src/Hexalith.Tenants` — the domain host that owns the Gateway/DomainService pair — resolves
seven EventStore edges in both lanes: `Admin.Abstractions`, `Client`, `Contracts`,
`DomainService`, `Gateway`, `Server`, `ServiceDefaults`. **Gateway and DomainService resolve
identically in both directions**, so no mixed Gateway-project / DomainService-package graph is
reachable, and Release assets contain **zero** EventStore `ProjectReference` — including
transitive ones, since the check covers every library entry in every assets file, not just
direct references.

**The 60 vs 61 asymmetry is expected and benign.** It is entirely
`Hexalith.EventStore.RestApi.Generators` in `Hexalith.Tenants.Api`: in source mode it is an
`OutputItemType="Analyzer" ReferenceOutputAssembly="false"` `ProjectReference`, which NuGet does
not record as an assets library; in package mode it is a `PrivateAssets="all"`
`PackageReference`, which it does. Same generator, same conditional policy, different assets
representation.

## AC4/AC5 — Compatibility And Regression Matrix, Both Modes

The solution was used for restore and build only; tests ran **by project**, never solution-level.

| Lane | Restore | Build `--warnaserror` |
| --- | --- | --- |
| Debug / source | exit 0 | **0 Warning(s), 0 Error(s)** |
| Release / package | exit 0 | **0 Warning(s), 0 Error(s)** |

| Test project | Debug / source | Release / package |
| --- | --- | --- |
| `Hexalith.Tenants.Contracts.Tests` | **115 / 115** | **115 / 115** |
| `Hexalith.Tenants.Server.Tests` | **738 / 738** | **738 / 738** |
| `Hexalith.Tenants.UI.Tests` | **1276 / 1276** | **1276 / 1276** |
| `Hexalith.Tenants.IntegrationTests` | **167 passed, 1 skipped, 0 failed** | **167 passed, 1 skipped, 0 failed** |

Identical counts in both modes; zero failures. UI is 1276 here versus 1266 in the prior
proof-clone evidence because Tenants `main` gained ten UI tests between those commits.

The single skip is `SnapshotPerformanceTests.ColdStartRehydration_CompletesWithin30Seconds_With500KEvents`,
a pre-existing environment-gated performance test — not a regression and not disabled here.

Guards carried by these passing suites, confirmed present in the code that ran rather than
assumed:

- **AC4 host graph rule** — `PackageGovernanceTests.cs` asserts the
  `Hexalith.EventStore.Gateway` project/package pair against DomainService. In `Contracts.Tests`,
  green in both modes.
- **AD-18** — `TenantsApiStructuralTests.cs` and `TenantsApiGatewayHandlerTests.cs` carry the
  platform `AddEventStoreDaprServiceInvocation` handler-order guard and the prohibition on a
  Tenants-local DAPR routing-header handler. In `IntegrationTests`, green in both modes.
- **Stories 2.4-2.7** dedicated external API host, generated-controller gateway boundary,
  domain-service/AppHost registration, and typed-client-only UI boundaries — carried by
  `IntegrationTests` and `UI.Tests`.
- **Story 2.11** fail-closed provenance/lifecycle matrices — carried by the passing
  `Server.Tests`, `UI.Tests`, and `IntegrationTests`.

No production policy, guard, or fixture was weakened, and no test was adjusted to make a lane
pass. No source file was modified in this session in any repository.

## Commands

```text
# both lanes, pristine, BEFORE restore
ac2-guard.sh <lane>

# source lane
dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Debug \
  -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false \
  -p:HexalithCommonsFromSource=false -nodeReuse:false -m:1
dotnet build   Hexalith.Tenants.slnx --configuration Debug --no-restore --warnaserror \
  -p:UseHexalithProjectReferences=true -p:HexalithMemoriesFromSource=false \
  -p:HexalithCommonsFromSource=false -nodeReuse:false -m:1

# package lane (separate working copy, isolated packages directory)
dotnet restore Hexalith.Tenants.slnx --force-evaluate -p:Configuration=Release \
  -p:UseHexalithProjectReferences=false \
  --packages /home/administrator/tmp-story-2-12/pkg-packages -nodeReuse:false -m:1
dotnet build   Hexalith.Tenants.slnx --configuration Release --no-restore --warnaserror \
  -p:UseHexalithProjectReferences=false -nodeReuse:false -m:1

# tests, by project, in each freshly restored mode
dotnet test tests/<project>/<project>.csproj --configuration <mode> \
  --no-build --no-restore <matching -p: properties> -nodeReuse:false -m:1
```

Every lane ran unattended with `-nodeReuse:false -m:1` and no `--interactive`; no credential
prompt was reachable and no source failure was ignored. `-m:1` is required because parallel
MSBuild instances race on the same EventStore `.deps.json`.

## Observed Drift During This Session — And Why It Does Not Invalidate This Receipt

Tenants `main` advanced again while this matrix ran:

| Tenants `main` | `references/Hexalith.EventStore` |
| --- | --- |
| `5787706` (validated, accepted here) | `c8c70030` |
| `46b96bc` (main after this session's matrix) | `49987454` |

The delta is **not** a submodule bump alone: it carries three commits of unrelated in-flight
work (Story 1.6 read-only tenant configuration, partial-release recovery scripts), including
changes to `PackageGovernanceTests.cs`.

`5787706` is therefore the correct acceptance target. It is published and reachable on Tenants
`origin/main`; it is the exact content the matrix was run against; and it is already the commit
the EventStore umbrella's `references/Hexalith.Tenants` gitlink points to, so no pointer change
is required. Re-validating at each new tip would chase a moving head and would additionally pull
another story's unreviewed work into this story's acceptance.

Under the amended AC2 this drift is expected behaviour, not a violation: Tenants tracks
EventStore `main`, and the receipt binds the SHA it names.

## Scope Statement

Nothing was pushed to any remote. No gitlink was changed in any repository. No package identity
was adopted or altered. No EventStore submodule content was edited. No `Version`,
`VersionOverride`, fallback property, or local `PackageVersion` entry was added to Tenants. No
nested submodule was initialized; no recursive or remote submodule update was used. No rebuild
of the retired Story 1.20 proof packages was attempted.

## AC5 — Tenants Maintainer Acceptance

- Approver: `jpiquot` (Tenants maintainer / release owner)
- Approval date: `2026-07-28`
- Approval channel: direct maintainer decision recorded in this receipt and in the story's Dev
  Agent Record. The maintainer explicitly chose this channel over a GitHub issue comment when
  offered both.
- Accepted Tenants SHA: `578770679b9d3bc3fdf2a8a78190f24cdad8576e`
- Accepted scope: the tracked EventStore source identity `c8c7003052a7f811d3b821f3442379ca5f3a9c65`
  under the amended AC2; the published Builds catalog identity `1b1c0b0` → `3.83.0` under the
  amended AC3; the already-published conditional Gateway/DomainService alignment and its
  `PackageGovernanceTests` host rule under AC4; and the dual-mode compatibility matrix above under
  AC5.
- Bound evidence: this receipt and the 17 support-safe lane logs in `logs/`, plus the three
  retained lane scripts (`setup-lane.sh`, `ac2-guard.sh`, `analyze-assets.py`), all in this
  directory.
- Rejected alternative: re-validating at the newer Tenants tip `46b96bc`, declined because it
  would pull Story 1.6's in-flight work into this story's acceptance and would restart against a
  head that keeps drifting.
- Prior scope approval: [Tenants issue #32](https://github.com/Hexalith/Hexalith.Tenants/issues/32)
  approved the Story 2.12 boundary. This SHA-specific acceptance is the separate second approval
  that AC5 requires, and it supersedes nothing in that boundary.

**Note on channel durability.** Every other approval in this story (EventStore issue comment
`5083143163`, release-owner comment `5083164122`, Builds PR #47 comment `5088870151`) has an
external GitHub record. This one deliberately does not. It is durable within the repository — this
receipt is committed alongside the evidence it binds — but a reviewer verifying the authority
chain from outside the repository will find no third-party record of it. Recorded here explicitly
so the difference is visible rather than assumed.

## Open Items Carried Forward

None for this story. The structural finding that the automated `build(deps)` bump overwrites any
frozen consumer pin is resolved *for Story 2.12* by the amended AC2, which tracks `main` by design.
It remains a live consideration for any future story that needs a frozen consumer pin, and is not
re-opened here.
