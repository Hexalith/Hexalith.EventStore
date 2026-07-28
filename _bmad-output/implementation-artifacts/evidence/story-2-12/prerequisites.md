# Story 2.12 Prerequisite Receipt

- Receipt date: `2026-07-27`
- Overall status: `superseded` (was `blocked` — see "Disposition" at the end of this file)
- Package lane: `superseded` (was `blocked`)
- Source lane: `verified`

> **Read this first (status corrected 2026-07-28 by code review).** This header previously read
> `blocked` while the disposition appended at the end of the file recorded `blocked` → `superseded`,
> so a reader stopping at the header drew the wrong conclusion. The External Prerequisite Contract
> this receipt enforces was **retired** on 2026-07-27 by the approved sprint change proposal and the
> AD-22 scoped exception. The negative package-byte audit below stands as the recorded justification
> for that exception; its *blocking* conclusions no longer apply. Everything from here to the
> disposition section is preserved verbatim as the historical fail-closed record.

This receipt is intentionally fail closed. The approved Builds catalog prerequisite exists, but
no retrievable source for the original 14 Story 1.20 package files has been proved. The Builds
gitlink and package identity must not be adopted in Tenants until the missing byte source passes
the complete manifest and byte-equality checks.

## Story 1.20 Authority Revalidation

- Published verifier correction: EventStore commit
  `737b3e5a7113de6105e233459203e988af0f78d4`,
  [PR #332](https://github.com/Hexalith/Hexalith.EventStore/pull/332)
- Evidence commit A: `b695ad3215cd873c41561635e4eb4d7ff29d56a2`
- Pointer-only commit B: `ed48057e9bf9cb5e5e8667fec84f7c70e4534eea`
- Authorizing commit C: `1b219d39cfa8f0349175c356001ba539bfb4aa92`
- Approved EventStore runtime:
  `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`
- Approved package version: `999.1.20-proof.fa2d1c9910f8`
- Approved 14-line manifest SHA-256:
  `4271ddc76411780591423ab024b776cd34a2abccf1cc2dac03a245e141dbe0bc`
- EventStore owner approval: `jpiquot`, 2026-07-26,
  [issue comment 5083143163](https://github.com/Hexalith/Hexalith.EventStore/issues/324#issuecomment-5083143163)
- Release-owner approval: `jpiquot`, 2026-07-26,
  [issue comment 5083164122](https://github.com/Hexalith/Hexalith.EventStore/issues/324#issuecomment-5083164122)

The complete official-main A/B/C verifier passed from EventStore commit
`737b3e5a7113de6105e233459203e988af0f78d4`. In the same shell, the source-consumer procedure
passed against clean Tenants commit `902065efa37d25fd558fc4268a31dfccc515fa41`; that commit's
EventStore gitlink and checked-out submodule both equal the approved runtime SHA.

## Tenants Scope Approval

- Approver: `jpiquot`
- Approval date: `2026-07-27`
- Durable record: [Tenants issue #32](https://github.com/Hexalith/Hexalith.Tenants/issues/32)

The record binds the accepted source pin, approved Builds pin, conditional Gateway graph,
focused tests, and dual-mode validation. It rejects local version overrides, rebuilt package
substitution, mixed graphs, recursive/remote submodule updates, unrelated upgrades, and UX work.
Final Tenants commit acceptance remains a separate approval after green CI.

## Approved Builds Catalog Prerequisite

- Status: `passed`
- Repository: `Hexalith/Hexalith.Builds`
- Published `main` commit: `8f32f127c73026e46f7eb4fcb1b702d2b518d3e9`
- Reviewed head commit: `960979d428f93964ac7b0a6c4366429242ba2401`
- Author and approver: `jpiquot`
- Approval date: `2026-07-27`
- Pull request: [Hexalith.Builds PR #47](https://github.com/Hexalith/Hexalith.Builds/pull/47)
- Exact-SHA approval:
  [issue comment 5088870151](https://github.com/Hexalith/Hexalith.Builds/pull/47#issuecomment-5088870151)
- Accepted catalog changes:
  1. `HexalithEventStoreVersion` is
     `999.1.20-proof.fa2d1c9910f8`.
  2. `Hexalith.EventStore.Gateway` is centrally declared with
     `Version="$(HexalithEventStoreVersion)"`.
- Validation: central catalog validation, authoritative catalog tests, validator scenario tests,
  consumer-authority scenario tests, repository build/test, CodeQL, SonarCloud, Codacy,
  dependency review, Python contracts, and commitlint all passed.

This approval does not prove package-byte availability and grants no authority to substitute or
rebuild the approved packages.

## Original Package-Byte Prerequisite

- Status: `blocked`
- Required inventory: the exact 14 `.nupkg` files named by the checked-in
  `nuget-sha256.txt`, at version `999.1.20-proof.fa2d1c9910f8`
- Required proof: literal filename set, exact count, complete manifest validation, and SHA-256
  equality for every package before any consumer restore

Recovery checks performed on 2026-07-27:

1. The original build log identifies transient directory `/tmp/tmp.FdPTcyt3L7/packages`, but the
   directory no longer exists and no exact package file remains under `/tmp`, `/var/tmp`, the
   administrator home, the project workspace, or the NuGet global-packages directory.
   **Corrected and extended 2026-07-27 (second audit):** proof-version package bytes *do*
   survive in the administrator home, but never for the approved runtime. See check 7.
2. Azure storage account `hexalithevidence`, container `story-1-20`, contains only
   `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594/story-1-20-raw-evidence.tar.gz` for this runtime.
   Object version `2026-07-26T10:36:02.8785061Z` is covered by a locked immutability policy through
   `2033-08-01T00:00:00Z` and has metadata SHA-256
   `76d9d02e9d75017f5d2b952d36c76e243968f037739a56c3ed18e34be3bf68ec`.
   The archive contains package build/validation logs and manifests, but no `.nupkg` file. Stored
   blob versions contain no package or proof-version object.
3. GitHub Actions run `30196420994` still exposes the `blocking-test-results` artifact, but its
   extracted contents contain no `.nupkg`, proof-version path, or package archive. The other
   runtime-matched artifacts are test-result bundles only.
4. The configured nuget.org source has no `Hexalith.EventStore.Gateway` version
   `999.1.20-proof.fa2d1c9910f8`.
5. GitHub Packages enumeration could not be proved because the configured GitHub token lacks the
   `read:packages` scope. No GitHub Packages source is configured locally.
6. No deleted-but-open local package handle was found.

7. **Exhaustive filesystem scan (2026-07-27, second audit).** A whole-filesystem search for
   `*999.1.20-proof*` (excluding `/proc`, including the mounted Windows volumes) returned 65
   matching paths. Five surviving Story 1.20 transient package directories were found under
   `/home/administrator/tmp-story-1-20/`, each holding a complete 19-file proof set:

   | Transient directory | Proof version present |
   | --- | --- |
   | `tmp.3JcRFJBxed/packages` | one of the five suffixes below |
   | `tmp.8QeVFDgstJ/packages` | one of the five suffixes below |
   | `tmp.Tg9CMiwIaN/packages` | one of the five suffixes below |
   | `tmp.XIiKfvEBwv/packages` | one of the five suffixes below |
   | `tmp.kx5WDx9NDm/packages` | one of the five suffixes below |

   Suffixes present: `38f85086fc25`, `bae137d9e931`, `eb59649b29a0`, `ed5af0f650a1`,
   `f692f903d31b`. The NuGet global-packages folder additionally retains five
   `999.1.20-proof.440ff4cb36a9` artifacts.

   **None of these is the approved runtime.** The only `.nupkg` anywhere on the machine at
   version `999.1.20-proof.fa2d1c9910f8` is `Hexalith.Commons.UniqueIds` — a collateral
   artifact from a `Hexalith.Commons` build that inherited the version override, not a member
   of the approved 14-package EventStore inventory. Distinct `Hexalith.EventStore*` `.nupkg`
   files at the approved version found on this machine: **0 of 14**.

8. **The GitHub Packages gap in check 5 is now closed — negatively.** The release owner granted
   `read:packages` on 2026-07-27 (`gh auth refresh -h github.com -s read:packages`; scopes are
   now `gist`, `read:org`, `read:packages`, `repo`, `workflow`). With the scope in place:

   - `/orgs/Hexalith/packages?package_type=nuget` enumerates **185** packages across two pages.
     Filtering for `^Hexalith\.EventStore` returns **none**. The only near matches are
     `Hexalith.Infrastructure.DaprEventStore` and the typo package
     `Hehalith.Infrastructure.DaprEventStore`, neither of which is in the approved inventory.
   - `/users/jpiquot/packages?package_type=nuget` and `/user/packages?package_type=nuget` return
     **0** packages.

   No `Hexalith.EventStore*` package exists in GitHub Packages at any version, so the approved
   proof version cannot be there either.

The raw evidence archive, version string, build log, and hash manifest do not satisfy package
availability. Required next state is a release-owner-provided retrievable source containing the
original 14 files, followed by the packet's NuGet consumer procedure in the same verified shell.
A rebuild, feed version match without byte equality, or locally assigned package version remains
rejected.

### Audit Disposition After Check 8

Every retrieval avenue named by the External Prerequisite Contract has now been executed and
returned negative. No avenue remains that could be tested without new external state:

| Avenue | Result |
| --- | --- |
| Original transient build directory | Deleted; five other candidate runtimes survive, approved one does not |
| Whole-filesystem scan | 0 of 14 approved `Hexalith.EventStore*` `.nupkg` |
| nuget.org | Approved proof version absent (143 versions, nearest `3.82.0`) |
| Azure WORM raw-evidence archive | Logs and manifests only, no `.nupkg`; no other stored blob version |
| GitHub Actions retained artifacts | Test-result bundles only |
| GitHub Packages (org and user) | No `Hexalith.EventStore*` package at any version |

The original approved bytes are therefore **not recoverable**. Closing AC3, the package half of
AC4, and AC5 as literally specified is impossible, because the story pins byte equality against a
manifest whose artifacts no longer exist anywhere.

Only two dispositions remain, and both are release-owner decisions outside this story's authority:

1. The release owner produces the original 14 files from a location not visible to this
   environment, after which the packet's NuGet consumer procedure runs unchanged.
2. The packaging is re-run from the approved source SHA `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`
   under release-owner change control, producing a **new** hash manifest and a **new** durable
   approval. The story explicitly refuses to let a rebuilt artifact inherit Story 1.20 authority,
   so this path requires amending the pinned manifest and approval record rather than reusing
   `4271ddc76411780591423ab024b776cd34a2abccf1cc2dac03a245e141dbe0bc`.

Until one of these lands, the package lane stays `blocked` and the story stays below `review`.

## Published-Main Regression Recorded 2026-07-27

Two conditions on published Tenants `main` (`230a533d`) were proved during the second audit.
Both are recorded here because they change the prerequisite disposition; neither was created by
this story's current session, and neither was worked around.

1. **The approved source pin was mechanically discarded.** The `/pushall` merge `230a533d`
   resolved `references/Hexalith.EventStore` to `737b3e5a`, discarding the approved
   `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594` adopted by `902065e`/`db09a84`. AC2 is therefore
   violated on `main`, and the source consumer guard fails closed there.

2. **Package identity was adopted before this receipt passed.** Tenants `main` already points
   `references/Hexalith.Builds` at `0e464b5410b487cee50b9523da3eedd0eec74589`, a descendant of
   the approved `8f32f127` whose catalog sets `HexalithEventStoreVersion` to
   `999.1.20-proof.fa2d1c9910f8`. Because those bytes were never published, Tenants `main`
   cannot restore its own solution in **either** mode: `dotnet restore Hexalith.Tenants.slnx`
   fails `NU1102` for `Hexalith.EventStore.Client`, `Hexalith.Tenants.IntegrationTests` cannot
   restore, and one `TenantsUiCompositionTests` package-mode case fails closed.

   This contradicts the External Prerequisite Contract, which permits source pinning,
   conditional Gateway code, and tests while the byte receipt is blocked, but forbids adopting
   any package identity. The Builds gitlink was **not** changed by this session in either
   direction; resolving it is a release-owner decision.

Detailed commands, exit codes, resolved graph, and test counts are in
`source-lane-2026-07-27.md`.

## Update 2026-07-27 (Second Session)

Re-verified against Tenants `main` `4ca5f86fa777db62efaafb2f305f96a3a548b993`. Full commands,
exit codes, graphs, and counts are in `dual-mode-2026-07-27.md`.

**Condition 2 above is CLOSED.** Tenants `main` now pins Builds at
`bb02cdc85735062de3a606f069ec714c9a398032`
(`fix(deps): restore published EventStore package pin`), returning `HexalithEventStoreVersion`
to the published `3.82.0` while retaining the approved central `Hexalith.EventStore.Gateway`
entry from `8f32f127`. The premature proof-version package identity is therefore no longer
adopted in Tenants. Solution-level restore exits 0 in both modes, `IntegrationTests` restores
and runs, and the `TenantsUiCompositionTests` package-mode case passes.

**Condition 1 above is UNCHANGED and has worsened.** The approved source pin is not simply
stale — it is overwritten repeatedly by an automated `build(deps)` submodule bump on Tenants
`main`: `4ca5f86` moved it to `b2d34025`, and `f1053a31` moved it to `c8c70030` while this
session was running. The approved SHA is now 46 commits behind EventStore `main`. Restoring the
pin is proven green (unpublished proof commit `b8698e9d`, verifier + source consumer exit 0,
full dual-mode matrix passing) but will regress again unless the automation excludes
`references/Hexalith.EventStore` or a Tenants CI check enforces the approved SHA.

**The original package-byte prerequisite is UNCHANGED: still `blocked`.** No new avenue was
found or attempted; the audit disposition after check 8 stands in full. The Release lane run in
this session used the published `3.82.0` catalog and is explicitly a *compatibility* lane, not
package-identity evidence: no isolated `--packages` directory, no source-mapped `nuget.config`,
no manifest verification, and no byte comparison were performed, because no approved bytes
exist to map, verify, or compare.

Overall status remains `blocked`. Package lane remains `blocked`.

## Closing Disposition 2026-07-27 — SUPERSEDED

- Overall status: `blocked` → **`superseded`**
- Superseded by:
  `../../../planning-artifacts/sprint-change-proposal-2026-07-27-story-2-12-runtime-identity-rescope.md`
- Owner decision record: `rescope-decision-2026-07-27.md`

The External Prerequisite Contract this receipt serves was **retired** by an approved sprint change
proposal on 2026-07-27. This receipt is not rewritten and nothing above is withdrawn: the negative
package-byte audit is the recorded justification for the AD-22 scoped exception, and it must remain
readable exactly as proved.

| Prerequisite | Final disposition |
| --- | --- |
| Approved Builds catalog commit | `passed`, then **partly retired**. `HexalithEventStoreVersion = 999.1.20-proof.fa2d1c9910f8` is retired with the proof version. The central `Hexalith.EventStore.Gateway` entry **survives**, is retained in the Tenants Builds pin, and is still required by AC4. |
| Original package bytes (14 `.nupkg`) | **Retired, not satisfied.** Proved unrecoverable from every avenue the contract named (0 of 14). Neither of the two remaining dispositions was exercised: the owner re-scoped AC3 instead. A rebuild inheriting Story 1.20 authority remains forbidden and is no longer required by any acceptance criterion. |
| Source lane | `verified` against the historical pin; **re-validation required** at the accepted Tenants `main` under the amended AC2 (gitlink == checkout `HEAD`, reachable from EventStore `origin/main`, validated SHA recorded). |

Under the amended AC3, the package lane validates against the published Builds-catalog version
already pinned by Tenants (`1b1c0b0` → `3.83.0`, published on nuget.org) rather than the retired
proof version. The package lane is therefore **no longer blocked on external deliverables**.

The Story 1.20 authority chain, its A/B/C verifier result, and the Tenants scope approval recorded
above are unaffected and remain binding for AC1 and AC5.
