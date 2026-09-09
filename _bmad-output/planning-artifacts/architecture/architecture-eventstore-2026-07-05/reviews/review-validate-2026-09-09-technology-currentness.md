# Technology Currentness Review — VALIDATE Gate 2026-09-09

Spine: `_bmad-output/planning-artifacts/architecture.md` (status `final`, updated 2026-08-29).

Lens: verify every Stack entry and every named external/runtime claim against current repository/package evidence and authoritative primary web sources as of 2026-09-09. This is critique-only; the spine was not changed.

## Verdict

**CHANGES REQUIRED.** No critical technology-currentness defect was found, but one high-severity security-baseline claim is stale: the spine normatively calls .NET/ASP.NET `10.0.11` and SDK `10.0.400` current after Microsoft published the security-bearing `10.0.12` release and SDK `10.0.401`. Two medium repository-reality mismatches remain in the Stack rendering (FrontComposer and DAPR runtime pins), plus four low currentness/provenance issues.

Severity count: **0 critical / 1 high / 2 medium / 4 low**.

## Method And Evidence Boundary

No result below relies only on model training data.

1. Read the complete spine, its append-only memlog, and the repository's required `AGENTS.md` and Hexalith baseline.
2. Compared Stack and normative literals with current repository reality at root `HEAD` `1b6f08d4`: `global.json`, the committed `references/Hexalith.Builds` gitlink `a32cb422`, the committed `references/Hexalith.FrontComposer` gitlink `053b2008`, `.github/workflows/integration.yml`, `deploy/README.md`, `deploy/dapr/statestore-postgresql.yaml`, project files, and resolved local NuGet package metadata.
3. Queried the live official NuGet v3 flat-container indexes for every Stack package family. Exact index links appear in the verification table below.
4. Checked vendor/runtime claims against primary sources: [.NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json), [.NET 10.0.12 release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.12/10.0.12.md), [Aspire releases](https://github.com/microsoft/aspire/releases), [DAPR runtime releases](https://github.com/dapr/dapr/releases), current DAPR 1.18 documentation, [OpenBao releases](https://github.com/openbao/openbao/releases), Microsoft Azure Container Apps documentation, OCI Image Specification material, Microsoft .NET container documentation, and the RFC Editor.
5. Classified a package as current for this architecture when it matches the live Builds catalog and exists upstream. A newer upstream version is a finding only when the spine itself calls an older value current/security-required, the repository's own claimed runtime pins disagree, or the stale row can make independently built units select different inputs. The Stack's explicit statement that the catalog wins keeps catalog-only refresh opportunities low.

## Critical Findings

None.

## High Findings

### H1 — The normative .NET/ASP.NET security baseline is superseded and the paired current SDK is now known

- **Spine evidence:** `_bmad-output/planning-artifacts/architecture.md:157` says both that the “current required SDK” is `10.0.400` and that the ASP.NET/runtime security baseline is `10.0.11`; `:492` repeats `10.0.400` as current required; `:502` calls `10.0.11` the aligned security baseline.
- **Repository evidence:** `global.json:3-4` still seeds `10.0.400` with `rollForward: latestPatch`; `references/Hexalith.Builds/Props/Directory.Packages.props:172-193,202-221,308` still pins the applicable ASP.NET/Microsoft.Extensions/System.Text.Json patch family to `10.0.11`.
- **Current primary evidence:** Microsoft's live [.NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) now reports `latest-release: 10.0.12`, `latest-sdk: 10.0.401`, release date 2026-09-08, and `security: true`. The official [.NET 10.0.12 notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.12/10.0.12.md) identify SDK `10.0.401` (and `10.0.112`), state that the release carries security fixes, and list the ASP.NET Core, SignalR, DataProtection, Microsoft.Extensions, and System.Text.Json packages at `10.0.12`. The live [SignalR Client NuGet index](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.signalr.client/index.json) includes stable `10.0.12`; this is no longer a release-day propagation ambiguity.
- **Why it matters:** AD-11 is a normative adopted rule, not only a dated Stack snapshot. A builder following it today can retain a security-superseded runtime/package patch and can describe SDK `10.0.400` as current even though `10.0.401` is the same feature-band servicing SDK. `rollForward: latestPatch` allows a suitable installed `10.0.401`, but that does not make the hard-coded “current required” value truthful. AD-11's own coordinated-patch rule requires evaluation of the new band.
- **Disposition:** **Discuss, then update as one validated unit.** Move `global.json`, the central .NET/ASP.NET/Microsoft.Extensions package family, and the spine's dated rendering to the vendor's `10.0.12` / SDK `10.0.401` band only after the repository's restore/build/test/consumer proof passes. If an intentional temporary hold is necessary, record `10.0.11` as a dated catalog pin with a named revisit condition; do not call it the current security baseline.

## Medium Findings

### M1 — FrontComposer `4.1.1` contradicts both live version authorities, and “matching source” is too strong

- **Spine evidence:** `_bmad-output/planning-artifacts/architecture.md:506` says the catalog's `HexalithFrontComposerVersion` is `4.1.1` and that the root-declared Debug source matches the Release packages. The UI convention at `:482` correctly delegates to the catalog variable without repeating a literal.
- **Repository evidence:** `references/Hexalith.Builds/Props/Directory.Packages.props:9,53-57` now sets the single family variable to `4.4.0`. The root-declared FrontComposer source is committed at `053b2008`, described by its own repository as `v4.4.0-5-g053b2008`, while its package line is `4.4.0`; source is therefore five commits past the package tag rather than exactly matching the released bytes.
- **Current primary evidence:** the live [Hexalith.FrontComposer.Shell NuGet index](https://api.nuget.org/v3-flatcontainer/hexalith.frontcomposer.shell/index.json) lists stable `4.4.0`. All cataloged FrontComposer packages share the one `HexalithFrontComposerVersion`, which is the correct convergence mechanism.
- **Why it matters:** A story author copying the Stack literal selects a version three minor releases behind the actual catalog. Separately, Debug source and Release package modes do not have byte/source identity merely because they share a `4.4.0` tag band; current source includes five later commits.
- **Disposition:** **Autofix the rendering in an Update; discuss exact parity policy.** Prefer removing the literal entirely (“live `HexalithFrontComposerVersion` from the Builds catalog”). If the spine continues to claim Debug/Release matching, define whether that means API compatibility, tag-band equality, or exact source/package provenance and enforce that interpretation.

### M2 — “CI/deployment seed `1.18.0`” conflates three distinct DAPR runtime facts

- **Spine evidence:** `_bmad-output/planning-artifacts/architecture.md:384` and `:497` say the repository CI/deployment seed is `1.18.0`; AD-25 at `:451-456` correctly uses the band-level `1.18.x` for the `oq8-postgresql-v1` evidence profile.
- **Repository evidence:** `.github/workflows/integration.yml:24-25,64-68,148` pins DAPR CLI `1.18.0` but the live-sidecar runtime to `1.18.2` and verifies OQ8 evidence against that runtime. `deploy/README.md:243,261,279,298` separately uses `daprio/daprd:1.18.0` as the deployment-template example. These are not one shared CI/deployment runtime pin.
- **Current primary evidence:** the official [DAPR releases page](https://github.com/dapr/dapr/releases) marks runtime `v1.18.3` (2026-08-14) as the latest stable release; `v1.18.4-rc.4` is prerelease. The current DAPR documentation still treats 1.18 as the latest stable documentation line.
- **Why it matters:** A CI slice can incorrectly downgrade its runtime to the CLI version, while a deployment slice can mistake the older template seed for the repository's tested runtime. The spine's `1.18.x` compatibility band remains valid, but it does not cure the false shared-pin sentence.
- **Disposition:** **Autofix the wording; defer runtime movement to tested evidence.** State the three facts separately: CLI `1.18.0`, CI/live-sidecar runtime `1.18.2`, deployment-template runtime `1.18.0`; record upstream stable `1.18.3` as a candidate pending compatibility/evidence refresh. Do not infer that a newer patch is production-approved merely from release ordering.

## Low Findings

### L1 — Microsoft.Testing.Extensions.CodeCoverage is one catalog refresh behind

- **Evidence:** `_bmad-output/planning-artifacts/architecture.md:504` and `references/Hexalith.Builds/Props/Directory.Packages.props:243` agree on `18.10.0`; the live [official NuGet index](https://api.nuget.org/v3-flatcontainer/microsoft.testing.extensions.codecoverage/index.json) lists stable `18.11.0`.
- **Impact:** No cross-unit divergence exists because the catalog is authoritative, but the Stack is not upstream-current.
- **Disposition:** **Defer** to the next tested catalog refresh; no architecture rule change is required.

### L2 — The CommunityToolkit DAPR integration has newer prerelease builds on a different prerelease label

- **Evidence:** `_bmad-output/planning-artifacts/architecture.md:496` and the catalog at `:136` agree on `13.5.0-preview.1.260825-0345`. Its installed `.nuspec` targets `net10.0` and depends on Aspire.Hosting `13.5.0`, so it is compatible with the repository's `13.5.3` patch. The live [NuGet index](https://api.nuget.org/v3-flatcontainer/communitytoolkit.aspire.hosting.dapr/index.json) also contains later `13.5.1-beta.*` builds, currently through `13.5.1-beta.748`.
- **Impact:** This is not a correctness conflict: AD-11 explicitly requires proof for a channel change, and the pinned package exists and fits. It is nevertheless a currentness item for the catalog owner.
- **Disposition:** **Defer/ignore in the spine.** Evaluate only through the catalog's normal prerelease-channel compatibility gate.

### L3 — AD-17 cites an obsolete RFC and implies the RFC requires an absolute Location

- **Spine evidence:** `_bmad-output/planning-artifacts/architecture.md:261` says an absolute `Location` is emitted “(RFC 7231)”.
- **Current primary evidence:** the [RFC Editor status page for RFC 7231](https://www.rfc-editor.org/info/rfc7231/) says it is obsoleted by RFC 9110. Both [RFC 7231 §7.1.2](https://www.rfc-editor.org/rfc/rfc7231.html#section-7.1.2) and current [RFC 9110 §10.2.2](https://www.rfc-editor.org/rfc/rfc9110.html#section-10.2.2) define `Location = URI-reference` and explicitly permit relative references resolved against the request/target URI.
- **Impact:** The platform's stricter absolute-URI contract is reasonable and remains enforceable, but it is a Hexalith interoperability decision, not a requirement supplied by RFC 7231.
- **Disposition:** **Autofix** the parenthetical in an Update: cite RFC 9110 for the field semantics and say explicitly that Hexalith intentionally requires the stricter absolute form.

### L4 — The memlog does not contain a currentness entry for the 2026-08-29 version rewrite or this servicing release

- **Evidence:** `.memlog.md:97` is the last typed `(version)` entry and still says the repository seed is SDK `10.0.302`; `_bmad-output/planning-artifacts/architecture.md` changed to `10.0.400` in commit `b34f252c` on 2026-08-29. `.memlog.md:105` records the 2026-09-08 validation verdict as an event, including the `10.0.12` problem, but does not append the checked version facts and sources.
- **Impact:** The rendered spine can be independently reality-checked, as this review does, but the run's declared working memory no longer proves where its current version literals came from. That weakens the requirement that committed technology decisions be demonstrably researched rather than asserted.
- **Disposition:** **Discuss/update memlog only during an authorized Update run.** Append a `(version)` entry recording the 2026-09-09 .NET metadata, current repository/catalog pins, DAPR split, and FrontComposer evidence. The memlog is append-only; do not rewrite line 97.

## Full Stack Verification

| Spine claim | Repository evidence | Live authoritative evidence (2026-09-09) | Result |
| --- | --- | --- | --- |
| .NET SDK `10.0.400`, `rollForward: latestPatch` (`architecture.md:157,492`) | `global.json:3-4` exactly matches the seed | [.NET metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) now says SDK `10.0.401` / runtime `10.0.12` | **Seed confirmed; “current required” stale — H1** |
| Target framework `net10.0` (`:493`) | `Directory.Build.props` and current projects target `net10.0` | [.NET 10 metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) reports active LTS support to 2028-11-14 | **Pass** |
| Aspire.Hosting `13.5.3` (`:494`) | catalog `:113`; AppHost SDK is `Aspire.AppHost.Sdk/13.5.3` | [Aspire releases](https://github.com/microsoft/aspire/releases) marks 13.5.3 latest; [NuGet index](https://api.nuget.org/v3-flatcontainer/aspire.hosting/index.json) contains 13.5.3 | **Pass/current** |
| Aspire Keycloak/Kubernetes `13.5.3-preview.1.26425.3` (`:495`) | catalog `:117-118` | [Keycloak index](https://api.nuget.org/v3-flatcontainer/aspire.hosting.keycloak/index.json) and [Kubernetes index](https://api.nuget.org/v3-flatcontainer/aspire.hosting.kubernetes/index.json) list that as their latest builds | **Pass/current prerelease** |
| CommunityToolkit Aspire DAPR `13.5.0-preview.1.260825-0345` (`:496`) | catalog `:136`; installed nuspec targets net10 and depends on Aspire.Hosting 13.5.0 | [NuGet index](https://api.nuget.org/v3-flatcontainer/communitytoolkit.aspire.hosting.dapr/index.json) confirms the exact build and later beta-channel builds | **Pass/compatible; L2 refresh note** |
| DAPR runtime shared seed `1.18.0`, production `1.18.x` (`:384,452,497`) | CI runtime 1.18.2; deployment template 1.18.0 | [DAPR releases](https://github.com/dapr/dapr/releases) latest stable 1.18.3 | **Band pass; shared-pin sentence stale — M2** |
| Dapr .NET SDK `1.18.5` (`:498`) | catalog `:139-146` | [Dapr.Client index](https://api.nuget.org/v3-flatcontainer/dapr.client/index.json) latest stable is 1.18.5 | **Pass/current stable** |
| OpenBao Stable v1 since DAPR 1.16 using `secretstores.hashicorp.vault` (`:384,499`) | planned Story 7.6 boundary; no conflicting provider SDK in app code | DAPR [secret-store catalog](https://docs.dapr.io/reference/components-reference/supported-secret-stores/) lists OpenBao Stable/v1/since 1.16; [OpenBao component page](https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/) confirms no dedicated type and use of `secretstores.hashicorp.vault` v1 | **Pass/current** |
| MediatR `14.2.0` (`:500`) | catalog `:170` and project references | [NuGet index](https://api.nuget.org/v3-flatcontainer/mediatr/index.json) latest stable 14.2.0 | **Pass/current** |
| FluentValidation `12.1.1` (`:501`) | catalog `:151,153` | [NuGet index](https://api.nuget.org/v3-flatcontainer/fluentvalidation/index.json) latest stable 12.1.1 | **Pass/current** |
| ASP.NET Core/SignalR `10.0.11` aligned security baseline (`:502`) | catalog `:172-193` matches 10.0.11 | [.NET 10.0.12 notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.12/10.0.12.md) and [SignalR index](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.signalr.client/index.json) show security-serviced 10.0.12 | **Stale — H1** |
| Microsoft.CodeAnalysis `5.9.0` (`:503`) | catalog `:196-201` | [NuGet index](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.csharp/index.json) latest stable 5.9.0 | **Pass/current** |
| Microsoft.Testing.Extensions.CodeCoverage `18.10.0` (`:504`) | catalog `:243` | [NuGet index](https://api.nuget.org/v3-flatcontainer/microsoft.testing.extensions.codecoverage/index.json) latest stable 18.11.0 | **Catalog-consistent; upstream stale — L1** |
| Fluent UI Blazor V5 `5.0.0-rc.5-26219.1` (`:505`) | catalog `:226-227`; installed nuspec has a net10 dependency group; UI source consumes it | [NuGet index](https://api.nuget.org/v3-flatcontainer/microsoft.fluentui.aspnetcore.components/index.json) lists this as the latest V5 RC (V4 GA is a separate major line) | **Pass/current V5 prerelease** |
| FrontComposer catalog `4.1.1` with matching source/package (`:506`) | catalog 4.4.0; source `v4.4.0-5-g053b2008` | [Shell NuGet index](https://api.nuget.org/v3-flatcontainer/hexalith.frontcomposer.shell/index.json) latest stable 4.4.0 | **Stale/mismatched — M1** |
| OpenTelemetry core/instrumentation `1.18.0`, Redis `1.18.0-beta.1` (`:507-508`) | catalog `:266-275` | official NuGet indexes for [hosting](https://api.nuget.org/v3-flatcontainer/opentelemetry.extensions.hosting/index.json), [runtime](https://api.nuget.org/v3-flatcontainer/opentelemetry.instrumentation.runtime/index.json), and [StackExchangeRedis](https://api.nuget.org/v3-flatcontainer/opentelemetry.instrumentation.stackexchangeredis/index.json) match | **Pass/current** |
| Hexalith.Commons.UniqueIds `2.30.0` (`:509`) | catalog `:6,36` | [NuGet index](https://api.nuget.org/v3-flatcontainer/hexalith.commons.uniqueids/index.json) latest stable 2.30.0 | **Pass/current** |
| xUnit v3 `4.0.0` (`:510`) | catalog `:318-321`; test projects reference xunit.v3 | [NuGet index](https://api.nuget.org/v3-flatcontainer/xunit.v3/index.json) latest stable 4.0.0 | **Pass/current** |
| Shouldly `4.3.0` (`:511`) | catalog `:294` | [NuGet index](https://api.nuget.org/v3-flatcontainer/shouldly/index.json) latest stable 4.3.0; later 5.x is preview | **Pass/current stable** |
| NSubstitute `6.2.0` (`:512`) | catalog `:259` | [NuGet index](https://api.nuget.org/v3-flatcontainer/nsubstitute/index.json) latest stable 6.2.0 | **Pass/current** |
| NBomber `6.6.0` / NBomber.Http `6.2.1` (`:513`) | catalog `:253-254` | [NBomber index](https://api.nuget.org/v3-flatcontainer/nbomber/index.json) and [NBomber.Http index](https://api.nuget.org/v3-flatcontainer/nbomber.http/index.json) confirm those latest stable versions | **Pass/current stable** |

## Named Technology And Runtime Claims That Passed

- **DAPR actors/state/pub-sub/service invocation remain a fit for the paradigm.** Current DAPR 1.18 [actor documentation](https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/) retains turn-based actor access, transactional actor state, and `actorStateStore: true`; [pub/sub documentation](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/) retains at-least-once delivery and CloudEvents 1.0 wrapping. The spine's duplicate/out-of-order defenses are appropriately conservative rather than relying on broker ordering.
- **The AD-25 PostgreSQL profile exists and matches repository proof.** `deploy/dapr/statestore-postgresql.yaml:15-29` pins `state.postgresql` version `v1` and `actorStateStore: true`; the OQ8 fixture consumes that tracked file and records the `oq8-postgresql-v1` profile. Current DAPR [PostgreSQL documentation](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v2/) says v1 remains available and is not deprecated, while encouraging incompatible v2 for new applications. Keeping v1 for frozen evidence is therefore current and deliberate, not accidental drift.
- **OpenBao metadata and rotation assumptions remain supported.** The DAPR [Vault/OpenBao-compatible component reference](https://docs.dapr.io/reference/components-reference/supported-secret-stores/hashicorp-vault/) documents `vaultAddr`, TLS controls, `vaultTokenMountPath`, `vaultKVPrefix`, `vaultKVUsePrefix`, `enginePath`, `vaultValueType`, `metadata.version_id`, and confirms that component `secretKeyRef` values remain initialized until sidecar restart. OpenBao itself remains active; [v2.6.2](https://github.com/openbao/openbao/releases) is the current stable release. The spine intentionally defers the exact server image/HA/storage choice, so no unpinned-current-version defect exists here.
- **Azure Container Apps non-conformance remains accurate.** Microsoft's current [managed DAPR overview](https://learn.microsoft.com/en-us/azure/container-apps/dapr-overview) lists Azure Key Vault as its only supported secret-store component, limits components to its enumerated Tier 1/Tier 2 set, and lists capabilities requiring the DAPR Configuration spec as unsupported. OpenBao is absent. AD-24's exclusion is still correct.
- **OCI release/provenance claims remain technically available.** The current OCI [predefined annotations](https://github.com/opencontainers/image-spec/blob/main/annotations.md) retain `source`, `url`, `documentation`, `revision`, and `version` with the meanings encoded by AD-11. Microsoft continues to support Dockerfile-free [.NET SDK container publishing](https://learn.microsoft.com/en-us/dotnet/core/containers/sdk-publish). The spine's exact two-platform/index shape is a stricter project release contract, not an incorrect claim that OCI itself requires exactly two platforms.
- **SignalR group-scoped notifications and DataProtection-backed opaque cursors remain supported choices.** Current ASP.NET Core 10 [SignalR group documentation](https://learn.microsoft.com/en-us/aspnet/core/signalr/groups?view=aspnetcore-10.0) supports named groups while warning that groups are not authorization; AD-10 supplies the application authorization boundary. ASP.NET Core 10 [Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction?view=aspnetcore-10.0) remains the supported cryptographic framework behind purpose/scope-isolated protected payloads.
- **No named technology has disappeared.** Aspire 13.5.3, DAPR 1.18, OpenBao, PostgreSQL state v1, MediatR, FluentValidation, SignalR, Fluent UI Blazor V5 RC, OpenTelemetry, xUnit v3, Shouldly, NSubstitute, NBomber, OCI image indexes, and .NET SDK container support all remain published and usable in the selected target bands. The findings are about stale assertions and evidence alignment, not abandoned technology choices.

## Gate Recommendation

Do not describe this spine as technology-current until H1 is resolved or explicitly recorded as a temporary, owner-approved security-patch hold. M1 and M2 should be corrected in the same Update because they are direct repository-reality errors, even though the live catalog/evidence paths partly protect implementers. L1 and L2 can ride the normal catalog refresh; L3 is a safe standards-reference correction; L4 should be closed by appending current evidence to the memlog during that Update.
