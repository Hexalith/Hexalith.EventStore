# Technology Currentness Review - VALIDATE gate 2026-09-08

Spine: `_bmad-output/planning-artifacts/architecture.md` (status final, updated 2026-08-29, 610 lines).
Lens: every committed version/technology claim must be confirmed against repository reality (committed Builds catalog gitlink `35c3d1e5`, worktree `a32cb422`, `global.json`, `.github/workflows/integration.yml`, `deploy/README.md`) and, for external factual claims, against the live web as of 2026-09-08.

## Verdict

PASS-WITH-FINDINGS - no critical findings; one high (AD-11's normative `10.0.11` security baseline was superseded by the .NET 10.0.12 security servicing release published today), two medium repository-drift findings (FrontComposer row two minor versions behind the catalog and the committed submodule; DAPR runtime seed text conflates CI `1.18.2` with deployment `1.18.0`), three low.

## Method

1. Read Stack table (`architecture.md:486-513`), AD-11 Rule (`architecture.md:153-165`), AD-24 (`architecture.md:380-398`), AD-25 evidence profile (`architecture.md:451-460`), frontmatter sources (`architecture.md:1-50`).
2. Repository reality: `global.json`; `git -C references/Hexalith.Builds show 35c3d1e5...:Props/Directory.Packages.props` (committed gitlink) and `git -C references/Hexalith.Builds diff 35c3d1e5 a32cb422 -- Props/Directory.Packages.props` (worktree move); `git ls-tree HEAD references/Hexalith.FrontComposer` + `git describe --tags`; `.github/workflows/integration.yml:24-25`; `deploy/README.md:243,261,279,298`; `git log` on `architecture.md`, `global.json`, `integration.yml`; memlog tail and date grep.
3. Web reality (WebFetch/WebSearch, all reachable; no site blocked):
   - nuget.org v3 flat-container indexes for xunit.v3, Shouldly, NSubstitute, MediatR, FluentValidation, OpenTelemetry.Exporter.OpenTelemetryProtocol, OpenTelemetry.Instrumentation.StackExchangeRedis, Microsoft.CodeAnalysis.CSharp, NBomber, NBomber.Http, Microsoft.Testing.Extensions.CodeCoverage, Microsoft.FluentUI.AspNetCore.Components, Aspire.Hosting, Aspire.Hosting.Keycloak, CommunityToolkit.Aspire.Hosting.Dapr, Microsoft.AspNetCore.SignalR.Client, Dapr.Client, Hexalith.FrontComposer.Shell, Hexalith.Commons.UniqueIds; nuget.org gallery pages for Dapr.Client and Microsoft.AspNetCore.SignalR.Client.
   - https://github.com/dapr/dapr/releases, https://github.com/dapr/dapr/releases/tag/v1.18.3, https://github.com/dapr/cli/releases, https://docs.dapr.io/operations/support/support-release-policy/
   - https://docs.dapr.io/reference/components-reference/supported-secret-stores/ , .../openbao/ , .../hashicorp-vault/ , https://docs.dapr.io/developing-applications/building-blocks/secrets/secrets-scopes/
   - https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json , https://dotnet.microsoft.com/en-us/download/dotnet/10.0 , https://github.com/dotnet/core/blob/main/release-notes/10.0/README.md , https://support.microsoft.com/en-us/servicing/dotnet/net-10/2026/net-10-0-update-september-8-2026
   - https://github.com/dotnet/aspire/releases
   - https://learn.microsoft.com/en-us/azure/container-apps/dapr-overview , https://learn.microsoft.com/en-us/azure/container-apps/dapr-components
   - https://github.com/opencontainers/image-spec/blob/main/annotations.md
   - https://github.com/openbao/openbao/releases
4. Classification per claim: CONFIRMED-CURRENT / STALE / UNVERIFIABLE (table in Passes). The Stack header (`architecture.md:488`) makes the Builds catalog the live authority, so a nuget-newer version is only a finding where spine text itself binds a number a builder would follow (AD-11 SDK/security baseline, AD-24 runtime seed, AD-25 profile) or where a Stack row would mislead a story author.
5. Memlog staleness: last `(version)` currentness entries are `.memlog.md:91` and `:97` (both 2026-08-16); no entry dated after 2026-08-16 exists (grep for `2026-08-2x`, `2026-08-3x`, `2026-09-0x` returns nothing), while `architecture.md` was changed by `b34f252c` (2026-08-29, SDK 10.0.400) and `f44b5800` (2026-08-29).

## Findings

### H1 - high - AD-11 binds ASP.NET/runtime security baseline `10.0.11`, superseded today by the .NET 10.0.12 security servicing release

- Evidence: `architecture.md:157` ("the ASP.NET/runtime security baseline is `10.0.11`"); `architecture.md:502` ("`10.0.11` (catalog and security baseline aligned)"). Microsoft Support page https://support.microsoft.com/en-us/servicing/dotnet/net-10/2026/net-10-0-update-september-8-2026 exists and states "This update contains security fixes" and "when the .NET 10.0.12 is installed .NET 10.0.11 version will be removed". As of fetch time the propagation lag is visible: `releases.json` still reports latest-release 10.0.11 / latest-sdk 10.0.400, the dotnet/core 10.0 README's top row is still 10.0.11 (2026/08/11), and nuget.org still lists `Microsoft.AspNetCore.SignalR.Client` 10.0.11 as latest (no 10.0.12 package yet; the paired SDK number could not be read - UNVERIFIABLE today, expected 10.0.401 band).
- Why it matters: AD-11 is normative text, not a dated Stack rendering. A story author or catalog refresh (Story 3.16) reading AD-11 today would treat `10.0.11` as the required floor while a security patch is public; AD-11's own rule ("central .NET/ASP.NET security patch pins move together under the latest-compatible evidence rules") demands the 10.0.12 band be evaluated. This is the same class of finding the 2026-08-16 currentness review closed (`.memlog.md:97`) and it has reopened by the passage of time because the sentence hard-codes a patch number.
- Repository reality: `global.json` = SDK `10.0.400`, `rollForward: latestPatch` (CONFIRMED against releases.json latest-sdk 10.0.400 as of 10.0.11); catalog Microsoft.AspNetCore.*/Microsoft.Extensions.* = 10.0.11 (CONFIRMED equal to spine). Nothing in the repository is wrong yet; only the spine's fixed number is now behind the vendor.
- Disposition: autofix (wording), plus defer the actual catalog move to Story 3.16 / AD-11's refresh contract once 10.0.12 packages and the paired SDK are on nuget.org and validated.
- Suggested wording for `architecture.md:157`: "The repository SDK seed and current required SDK are `10.0.400` with `rollForward: latestPatch`; the ASP.NET/runtime security baseline is the catalog's current same-band patch (`10.0.11` at 2026-08-29; Microsoft published the `10.0.12` security servicing release on 2026-09-08, and the catalog, `global.json`, and this row move to that band as one validated unit)." And for `architecture.md:502`: "`10.0.11` (catalog pin; `10.0.12` security release published 2026-09-08 pending catalog validation)".

### M1 - medium - FrontComposer Stack row says catalog `4.1.1`; catalog is `4.3.0` at the committed gitlink, `4.4.0` in the worktree, and the committed FrontComposer source is already past the v4.4.0 tag

- Evidence: `architecture.md:506` ("Catalog `HexalithFrontComposerVersion` `4.1.1`; matching root-declared source in Debug and centrally pinned NuGet packages in Release"). Committed catalog `references/Hexalith.Builds@35c3d1e5:Props/Directory.Packages.props:9` = `4.3.0`; worktree `a32cb422` = `4.4.0` (uncommitted gitlink move). nuget.org `Hexalith.FrontComposer.Shell` latest = `4.4.0`. `git ls-tree HEAD references/Hexalith.FrontComposer` = `a0acb78f` = `v4.4.0-2-ga0acb78f`; worktree = `053b2008` = `v4.4.0-5-g053b2008`.
- Why it matters: this is the only place the spine writes a FrontComposer number (AD-21 and the UI convention row at `architecture.md:482` correctly reference the catalog variable without a literal). A story author copying the row would pin `4.1.1`, two minor versions behind both the catalog and the published package. Additionally, at the committed state the "matching root-declared source in Debug and centrally pinned NuGet packages in Release" claim is not true: Debug source is v4.4.0+2 commits while Release resolves `4.3.0`; the uncommitted worktree move to `4.4.0` is what restores the match. This row has now drifted for the third time (memlog `:77` 3.2.2 vs 4.0.1; `:97` 4.1.1; now 4.3.0/4.4.0), which is itself evidence that a literal here does not survive between validations.
- Guard: the Stack header (`architecture.md:488`) says the catalog always wins, so a builder that reads the header is protected; that keeps this at medium rather than high.
- Disposition: autofix. Suggested wording for `architecture.md:506`: "Catalog `HexalithFrontComposerVersion` (`4.3.0` at the committed Builds gitlink `35c3d1e5`; `4.4.0` pending in the in-flight Builds bump, matching the committed FrontComposer source at v4.4.0+); root-declared source in Debug only when its tag band equals the catalog pin, centrally pinned NuGet packages in Release". Alternatively drop the literal entirely and defer the number to the catalog, which is the only form that cannot go stale.

### M2 - medium - DAPR runtime "Repository CI/deployment seed `1.18.0`" conflates two different repository pins; CI runtime is `1.18.2`, latest stable runtime is `1.18.3`

- Evidence: `architecture.md:384` (AD-24: "the repository CI/deployment seed is `1.18.0`"), `architecture.md:497` (Stack: "Repository CI/deployment seed `1.18.0`; production profiles pin a compatible 1.18.x release"). Repository: `.github/workflows/integration.yml:24-25` = `DAPR_CLI_VERSION: '1.18.0'`, `DAPR_RUNTIME_VERSION: '1.18.2'` (and `:148` asserts the runtime version fail-closed); `deploy/README.md:243,261,279` pin `daprio/daprd:1.18.0`, `:298` recommends pinning "e.g. `1.18.0`". Web: https://github.com/dapr/dapr/releases latest stable `v1.18.3` (2026-08-14; also `v1.17.13`, `v1.16.19` patch lines, `v1.18.4-rc.3` 2026-09-02); https://github.com/dapr/cli/releases latest `v1.18.2` (2026-08-21). The docs support-policy page (https://docs.dapr.io/operations/support/support-release-policy/) still lists 1.18.0 as the latest patch - that page is itself stale and should not be cited as the patch authority.
- Why it matters: "CI/deployment seed 1.18.0" is only true of `deploy/README.md`; the live-sidecar CI suite (the runtime the repository actually proves against, and the runtime AD-25's `oq8-postgresql-v1` evidence runs on) is `1.18.2`. A deployment story author copying `1.18.0` would pin a runtime two patches behind what CI validates and three behind current stable; a CI author reading the spine would think the CI runtime is `1.18.0`. AD-25's "DAPR runtime 1.18.x" (`architecture.md:452`) is band-level and remains correct. The `Dapr.*` SDK `1.18.5` row is CONFIRMED (nuget latest stable 1.18.5, 2026-07-25) and the spine already states SDK versions are not runtime evidence.
- Disposition: autofix. Suggested wording for `architecture.md:384` and `:497`: "the repository CI live-sidecar runtime pin is `1.18.2` (CLI `1.18.0`, `.github/workflows/integration.yml`), the deployment template seed is `daprd:1.18.0` (`deploy/README.md`), and the current upstream stable is `1.18.3` (2026-08-14); production profiles pin one explicit 1.18.x patch at or above the CI-proven runtime".

### L1 - low - Microsoft.Testing.Extensions.CodeCoverage `18.10.0` row lags nuget `18.11.0`

- Evidence: `architecture.md:504`; catalog `:243` = 18.10.0 (spine equals catalog); nuget flat-container latest = `18.11.0`.
- Why: catalog-governed, spine matches the catalog, so no divergence between units; recorded for the Story 3.16 refresh. Disposition: ignore (spine) / defer to catalog refresh.

### L2 - low - CommunityToolkit.Aspire.Hosting.Dapr `13.5.0-preview.1.260825-0345` has newer prerelease builds (`13.5.1-beta.748`)

- Evidence: `architecture.md:496`; catalog `:136` matches; nuget lists `13.5.1-beta.736..748` after the pinned preview. The pinned version is the only `preview`-channel tag in the 13.5 line; the newer builds are `beta` channel.
- Why: catalog-governed and channel choice is a Builds decision (AD-11: "channel changes require explicit proof"). Disposition: ignore.

### L3 - low - Memlog currentness record is 23 days old and now contradicts the spine's own SDK seed

- Evidence: `.memlog.md:97` (2026-08-16): "global.json seed stays SDK 10.0.302 ... verified same-band security floor is SDK 10.0.303 and ASP.NET/runtime 10.0.11". `architecture.md:157,492` now say `10.0.400` (changed by `b34f252c`, 2026-08-29, "fix: update .NET SDK version to 10.0.400"), and `global.json` is `10.0.400`. No memlog line is dated after 2026-08-16 (file is 104 lines; last line "spine finalized"). The 2026-08-29 spine update therefore recorded no currentness verification, and the memlog's last recorded SDK numbers are wrong for the current spine.
- Why: the memlog is the run memory reviewers use to know what was last reality-checked; a reader would conclude the seed is 10.0.302 and re-open a closed item, or trust a 23-day-old verification across a .NET Patch Tuesday and two DAPR patch releases. Disposition: discuss (memlog is append-only and out of scope for this reviewer); the parent should append a `(version)` entry for today's verification results with this review as the source.

### Info (no action)

- OpenBao latest stable is `v2.6.2` (2026-08-18, https://github.com/openbao/openbao/releases). AD-24 says "pinned official OpenBao container" without a number; no `openbao` component, `deploy/dapr/openbao-secret-contract.yaml`, or AppHost OpenBao resource exists yet in the repository (grep of `deploy/`, `samples/`, `src/Hexalith.EventStore.AppHost`), consistent with AD-24 assigning that to Story 7.6 and the deployment overlay.
- Azure Container Apps docs additionally state "the Dapr server extensions, actor, and workflow SDK packages aren't compatible with Azure Container Apps" and that supported GA APIs are the Dapr v1.12 runtime APIs. This strengthens, not weakens, AD-24's non-conformance conclusion (`architecture.md:396`) and could be cited if AD-24 is ever reopened.
- `Dapr.Client 1.19.0-preview.2` exists on nuget (dated 2026-07-11, before 1.18.5). Not a stable line; no action.
- The DAPR docs OpenBao page states "there is no dedicated OpenBao Secrets Store" and points at `secretstores.hashicorp.vault` with "the same metadata fields are compatible" - exactly what AD-24 encodes.

## Passes

Every claim checked, with classification:

| Claim (anchor) | Repository reality | Web reality (2026-09-08) | Class |
| --- | --- | --- | --- |
| .NET SDK `10.0.400`, `rollForward: latestPatch` (`:157`, `:492`) | `global.json` = 10.0.400 / latestPatch | releases.json latest-sdk 10.0.400 (as of 10.0.11); 10.0.12 servicing published today, paired SDK not yet visible | CONFIRMED-CURRENT (seed); see H1 for the baseline sentence |
| ASP.NET/runtime security baseline `10.0.11` (`:157`, `:502`) | catalog Microsoft.AspNetCore.* / Microsoft.Extensions.* = 10.0.11 | superseded by 10.0.12 (support.microsoft.com, 2026-09-08) | STALE -> H1 |
| Target framework net10.0 (`:493`) | src projects net10.0 (brief) | .NET 10 LTS to 2028-11-14 | CONFIRMED |
| Aspire.Hosting 13.5.3 (`:494`) | catalog :113 = 13.5.3 | nuget latest 13.5.3; github latest v13.5.3 (2026-08-25); no 13.6/14 | CONFIRMED-CURRENT |
| Aspire Keycloak/Kubernetes 13.5.3-preview.1.26425.3 (`:495`) | catalog :117-118 | nuget latest for Aspire.Hosting.Keycloak = same | CONFIRMED-CURRENT |
| CommunityToolkit.Aspire.Hosting.Dapr 13.5.0-preview.1.260825-0345 (`:496`) | catalog :136 | newer 13.5.1-beta.748 (beta channel) | CONFIRMED (catalog) / L2 |
| DAPR runtime seed 1.18.0; production 1.18.x (`:384`, `:497`, `:452`) | CI runtime 1.18.2, CLI 1.18.0, deploy README daprd 1.18.0 | stable 1.18.3 (2026-08-14); CLI 1.18.2; 1.18.4-rc.3 | STALE -> M2 (1.18.x band CONFIRMED) |
| Dapr .NET SDK 1.18.5 (`:498`) | catalog :139-146 = 1.18.5 | nuget latest stable 1.18.5 (2026-07-25) | CONFIRMED-CURRENT |
| OpenBao secret store: Stable v1 since runtime 1.16, `secretstores.hashicorp.vault` (`:384`, `:499`) | no component in repo yet (future Story 7.6) | DAPR catalog: OpenBao Stable, v1, since 1.16; OpenBao page: type `secretstores.hashicorp.vault` version v1, no dedicated store | CONFIRMED-CURRENT |
| `vaultValueType: map`, `vaultKVUsePrefix`, `vaultKVPrefix`, `vaultAddr`, `enginePath`, `vaultTokenMountPath`, `vaultToken`, `metadata.version_id` (`:386`, `:392`) | n/a | all documented on the hashicorp-vault reference page (map is the default of vaultValueType; version_id documented for GetSecret); OpenBao page says same metadata applies | CONFIRMED-CURRENT |
| DAPR Configuration secret scopes `defaultAccess: deny` + `allowedSecrets`, kubernetes store deny (`:388`) | n/a | secrets-scopes page documents storeName/defaultAccess/allowedSecrets/deniedSecrets and the kubernetes-store deny example | CONFIRMED-CURRENT |
| Azure Container Apps managed DAPR excludes OpenBao and lacks Configuration secret scopes (`:396`) | n/a | dapr-overview: Tier 1 secrets = `secretstores.azure.keyvault` only, Tier 2 has no secret store; Limitations: "Dapr Configuration spec: any capabilities that require use of the Dapr configuration spec"; dapr-components page names no Vault/OpenBao | CONFIRMED-CURRENT |
| OCI annotations source/url/documentation/revision/version (`:161`) | Directory.Build.targets emits source only (memlog :91) | annotations.md definitions match the spine's reading | CONFIRMED-CURRENT |
| MediatR 14.2.0 (`:500`) | catalog :170 | nuget latest 14.2.0 | CONFIRMED-CURRENT |
| FluentValidation 12.1.1 (`:501`) | catalog :151,:153 | nuget latest 12.1.1 | CONFIRMED-CURRENT |
| Microsoft.CodeAnalysis 5.9.0 (`:503`) | catalog :196-201 | nuget latest 5.9.0 | CONFIRMED-CURRENT |
| Microsoft.Testing.Extensions.CodeCoverage 18.10.0 (`:504`) | catalog :243 | nuget 18.11.0 | CONFIRMED (catalog) / L1 |
| Microsoft.FluentUI.AspNetCore.Components 5.0.0-rc.5-26219.1 (`:505`) | catalog :226-227 | nuget latest 5.0.0-rc.5-26219.1; no GA; 4.14.4 latest v4 stable | CONFIRMED-CURRENT |
| Hexalith.FrontComposer 4.1.1 (`:506`) | catalog 4.3.0 committed / 4.4.0 worktree; submodule v4.4.0+2 | nuget latest 4.4.0 | STALE -> M1 |
| OpenTelemetry 1.18.0 family, StackExchangeRedis 1.18.0-beta.1 (`:507-508`) | catalog :266-275 | nuget latest 1.18.0 / 1.18.0-beta.1 | CONFIRMED-CURRENT |
| Hexalith.Commons.UniqueIds 2.30.0 (`:509`) | catalog :6 HexalithCommonsVersion 2.30.0 | nuget latest 2.30.0 | CONFIRMED-CURRENT |
| xUnit v3 4.0.0 (`:510`) | catalog :318-321 | nuget latest 4.0.0 | CONFIRMED-CURRENT |
| Shouldly 4.3.0 (`:511`) | catalog :294 | nuget latest stable 4.3.0 (5.0.0-preview.2 prerelease) | CONFIRMED-CURRENT |
| NSubstitute 6.2.0 (`:512`) | catalog :259 | nuget latest 6.2.0 | CONFIRMED-CURRENT |
| NBomber 6.6.0 / NBomber.Http 6.2.1 (`:513`) | catalog :253-254 | nuget latest stable 6.6.0 (6.7.0-beta.0 prerelease) / 6.2.1 | CONFIRMED-CURRENT |
| Hexalith baseline constraints (.NET 10+, C# 14, DAPR 1.18+, Aspire 13.x, Fluent UI Blazor V5, xUnit v3/Shouldly/NSubstitute, .NET SDK container support, no Dockerfiles) | brief-confirmed | all stack rows sit inside these bands | CONFIRMED |

Nothing was UNVERIFIABLE except the SDK number paired with the 10.0.12 servicing release (GitHub release-notes file returned 404 at fetch time; NuGet not yet updated), noted inside H1. All web sources responded; no search tool was unavailable.
