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
| initialized submodule set == root-declared set | PASS (vacuous — see below) | PASS (vacuous — see below) |
| no nested submodule initialized | PASS | PASS |

**Correction (2026-07-28 code review).** The "initialized submodule set == root-declared set" row
was **vacuous as originally run**: `ac2-guard.sh` derived `INITIALIZED` with
`git submodule status | awk '{print $2}'`, and `git submodule status` lists *every declared*
submodule whether or not it is initialized — an uninitialized entry is only marked by a `-` prefix
on field 1, so field 2 is the path in both cases and the set comparison could never fail. The guard
has been corrected to `awk '$1 !~ /^-/ {print $2}'`. The row above is retained as run, marked, and
must not be read as evidence. The **intent** of that assertion is independently carried by the
following row: the nested-submodule loop is real coverage and did execute, and the surrounding
cleanliness assertions are unaffected. Re-running the corrected guard against either lane clone is
the way to close this properly.

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

Both graphs were parsed by `analyze-assets.py` from every `project.assets.json` under
`src/`, `tests/`, and `samples/` after that lane's own `--force-evaluate` restore. Seventeen
assets files were evaluated per lane; no prior assets file was reused, and each lane has its own
working copy so no cross-mode contamination is possible.

**Precision correction (2026-07-28 code review).** "Every `project.assets.json`" means every
*Tenants-owned consumer* project — 17 of the 45 assets files present in a restored lane. The
analyzer walks only `src/`, `tests/`, and `samples/` and prunes any `references` directory
(`analyze-assets.py:18-21`), so the remaining 28 belong to the EventStore submodule's own projects.
That scoping is correct for measuring what Tenants *consumes* — those 28 are not Tenants consumers —
but the original wording overstated the denominator. Independently re-verified during review:
parsing `project.restore.frameworks[].projectReferences` (the section that actually records
`ProjectReference` edges) across **all 45** package-lane assets files yields **zero** EventStore
entries for any of the 17 `Hexalith.Tenants*` projects, so the AC3/AC4 conclusion is unchanged.

**Related observation.** `Hexalith.Tenants.slnx` carries 13 EventStore projects as solution members,
so the Release/package lane's `dotnet build` did compile those 13 EventStore assemblies from
submodule source (visible in `logs/pkg-build.log`). No Tenants project *consumes* them — every
EventStore edge from a Tenants project is a package edge — but the package lane's
"0 Warning(s), 0 Error(s)" therefore also attests to compiling source that Tenants does not link
against. Read that build result as a solution-wide signal, not as a package-mode-only one.

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

**Instrument limitation (2026-07-28 code review).** The `libraries` section this claim is drawn
from structurally cannot see a `ReferenceOutputAssembly="false"` `ProjectReference` — the same fact
the 60-vs-61 note below relies on. Such EventStore references exist in this repository:
`src/Hexalith.Tenants.Api` (the generators analyzer reference) and `src/Hexalith.Tenants.AppHost`
(three EventStore host references, guarded only by `'$(Configuration)' == 'Debug' and
'$(UseHexalithProjectReferences)' == 'true'`). A regression making any of them unconditional would
leave this table unchanged. The zero-project-edge result is therefore sound for everything NuGet
records as a library, but "including transitive ones" should be read as "including transitive
*library* edges". `analyze-assets.py` has been extended to additionally parse
`project.restore.frameworks[].projectReferences`, which does record these; re-running it against
either retained lane clone closes the gap.

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

**AD-12 persisted-path evidence, named explicitly (2026-07-28 code review).** The AD-22 scoped
exception preserves AD-12 unchanged, and the original wording above discharged it by assertion
("carried by the passing suites") without naming a test or quoting a result. The persisted-path
lane is `Hexalith.Tenants.IntegrationTests`, **167 passed / 1 skipped / 0 failed in both modes**
(`logs/src-test-IntegrationTests.log`, `logs/pkg-test-IntegrationTests.log`); the single skip is the
environment-gated `SnapshotPerformanceTests.ColdStartRehydration_…`, not a persisted-path test.
Note the honest limit: this story changed **dependency identity only** and modified no production
code path, so its AD-12 obligation is discharged by showing the pre-existing persisted-path suite
still passes under both dependency graphs — not by new persisted-path evidence. Story 2.11's
Tier-3 persisted consumer evidence remains the substantive record for the provenance/lifecycle
behaviour itself.

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
`origin/main`, and it is the exact content the matrix was run against. Re-validating at each new tip
would chase a moving head.

Under the amended AC2 this drift is expected behaviour, not a violation: Tenants tracks
EventStore `main`, and the receipt binds the SHA it names.

### Umbrella Pointer Correction (2026-07-28 code review, owner decision D1)

This section originally continued: "*and it is already the commit the EventStore umbrella's
`references/Hexalith.Tenants` gitlink points to, so no pointer change is required*". **That claim
was true when written and is false at the EventStore commit that carries this receipt.** The
corresponding story subtask carried the same false parenthetical and has been corrected too.

| EventStore commit | `references/Hexalith.Tenants` | that Tenants commit's `references/Hexalith.EventStore` |
| --- | --- | --- |
| `49987454` | `578770679b9d` (accepted, validated) | `c8c70030` (validated) |
| `57143dd3` (publishes this receipt) | **`f279cb13`** | **`49987454`** — not the validated SHA |

`f279cb13` is four commits past the accepted SHA and is a superset of `46b96bc`, the very tip this
receipt declines below. The umbrella therefore composes a Tenants commit that no `ac2-guard.sh` run
and no dual-mode matrix ever covered.

**Owner decision (2026-07-28):** keep the pointer where automation put it and record the delta
explicitly, rather than re-pinning it (which automation would overwrite again — it already did five
times) or re-running the matrix at a head that keeps moving. This is consistent with the amended
AC2, which makes tracking `main` the intended mechanism: the umbrella gitlink is *not* a frozen
acceptance artifact, and this receipt binds the SHA it names rather than whatever the pointer
currently holds.

**What this costs, stated plainly.** The validated identity (`578770679b9d` / `c8c70030`) and the
composed identity (`f279cb13` / `49987454`) are different commits, and only the former has evidence.
Anyone building the umbrella at or after `57143dd3` is not building what this receipt validates.
Nothing detects further drift — that gap is filed in `deferred-work.md` under
"code review of 2-12-… (2026-07-28)" and is a Tenants/Builds CI item, explicitly out of scope here
per the approved change proposal.

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
- Rejected alternative: re-validating at the newer Tenants tip `46b96bc`, declined because it would
  restart against a head that keeps drifting.
  **Rationale correction (2026-07-28 code review):** this alternative was originally declined partly
  because `46b96bc` "would pull Story 1.6's in-flight work into this story's acceptance". That
  distinction does not hold — the accepted `578770679b9d` is *itself* a Story 1.6 commit
  (`docs: update deferred work with review findings from 1-6-read-only-tenant-configuration`), 26
  commits and ~2443 non-`_bmad` insertions past the story baseline, including substantial unrelated
  Story 1.9 UI work. The surviving and sufficient reason is the first one: `578770679b9d` is the
  exact content the dual-mode matrix was actually run against. Acceptance is bound to *validated
  content*, not to a commit free of other stories' work — no such commit exists on this branch.
- CI at the accepted SHA (2026-07-28 code review; the AC5 subtask requires CI/evidence URLs to be
  bound and none were): of 8 checks on `578770679b9d`, `ci / build-and-test`, `ci / aspire-tests`,
  `codeql / analyze`, `commitlint / commitlint`, and `Verify exact green main source` **succeed**;
  `ci / performance-tests` and `Verify exact source was published` are **skipped**; and
  `release / release` **fails**
  ([run 30307676844](https://github.com/Hexalith/Hexalith.Tenants/actions/runs/30307676844/job/90115782191)).
  The release failure is **pre-existing and independent of Story 2.12** — it fails identically on
  `f279cb13` and is release-pipeline breakage, not a consumer-identity regression. Recorded rather
  than omitted, because `../prerequisites.md` set "after green CI" as a precondition for this
  acceptance and CI was not, strictly, green.
- Prior scope approval: [Tenants issue #32](https://github.com/Hexalith/Hexalith.Tenants/issues/32)
  approved the Story 2.12 boundary. This SHA-specific acceptance is the separate second approval
  that AC5 requires.
  **Correction (2026-07-28 code review):** this line originally added "and it supersedes nothing in
  that boundary", which is inaccurate. Issue #32 approves the **pre-amendment** scope — its item 1
  is "pin the EventStore gitlink to `fa2d1c9910f8`", its item 2 requires a Builds catalog at
  `999.1.20-proof.fa2d1c9910f8`, and its **rejected alternatives** include "retaining the
  non-authorizing EventStore `3.82.0` catalog pin". The delivered work performs neither approved
  item and adopts a published catalog pin of exactly the rejected class. The approved sprint change
  proposal replaced most of that boundary; #32 survives only as the record that a Story 2.12 scope
  was authorized at all, not as approval of the scope actually delivered.
  **Owner decision (2026-07-28, D2):** the in-repository record is accepted as sufficient authority
  for the amended scope; no fresh external approval will be sought. A reviewer should therefore treat
  the entire *post-amendment* maintainer authority chain as repository-internal — see the channel
  note below, which applies to the amended scope as much as to the SHA.

**Note on channel durability.** Every other approval in this story (EventStore issue comment
`5083143163`, release-owner comment `5083164122`, Builds PR #47 comment `5088870151`) has an
external GitHub record. This one deliberately does not. It is durable within the repository — this
receipt is committed alongside the evidence it binds — but a reviewer verifying the authority
chain from outside the repository will find no third-party record of it. Recorded here explicitly
so the difference is visible rather than assumed.

## Open Items Carried Forward

The structural finding that the automated `build(deps)` bump overwrites any frozen consumer pin is
resolved *for Story 2.12* by the amended AC2, which tracks `main` by design. It remains a live
consideration for any future story that needs a frozen consumer pin, and is not re-opened here.

**Correction (2026-07-28 code review).** This section originally read "None for this story", which
was wrong on two counts: the approved change proposal (§5) had already named a Tenants CI gitlink
reachability check as a candidate follow-up, and no ledger entry had been written for it. Two
entries now exist in `../../deferred-work.md` under
`## Deferred from: code review of 2-12-tenants-runtime-identity-adoption-and-package-mode-validation (2026-07-28)`:

1. **No blocking CI evaluates the Debug/source lane**, so the source half of this story's Gateway
   conditional is never exercised by an automated gate. Owned by the Hexalith.Tenants maintainer.
2. **Nothing durably detects EventStore gitlink drift or a wrong-but-resolvable catalog version.**
   The amended AC2/AC3 gate exists only as the hand-run scripts in this directory, which no workflow
   and no test invokes. Owned by the Hexalith.Tenants and Hexalith.Builds maintainers.

Item 2 is not hypothetical — see "Umbrella Pointer Correction" above, where the umbrella gitlink left
the accepted SHA inside this story's own final commit, within a day.

## Architect Ratification (2026-07-28, owner decision D3)

The approved sprint change proposal (§5) assigned Winston (Architect) to ratify the AD-22 scoped
exception text and confirm the Parties 8.6 non-extension sentence is sufficient. That ratification
was never performed or recorded, yet the exception was committed to `architecture.md` and the story
advanced to `review`. **The owner decided on 2026-07-28 that the sprint-change-proposal approval
subsumes the assigned architect ratification**; no separate Winston pass will be sought.

Recorded for the reviewer: the exception text was independently inspected during code review and is
correctly scoped — dated, naming one story and one consumer, explicitly conferring no authority on
Parties Story 8.6, on any other consumer, on deployed-mode closure, or on any future frozen-identity
relief, with AD-11 and AD-12 byte-unchanged (`architecture.md` gained exactly two lines).
