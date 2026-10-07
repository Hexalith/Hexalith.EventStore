# Stack Reality And Currentness Check — Reconcile Update 2026-10-07

Spine: `_bmad-output/planning-artifacts/architecture.md`, section `## Stack` (lines 364-383, "observed on 2026-09-23").

Lens: compare every Stack row with current repository reality and current official upstream sources. Read-only. The spine was not changed. No build, restore, test, Git mutation, or submodule update was run.

## Verdict

**Stack rows are stale against the repository.** The `references/Hexalith.Builds` gitlink moved from `2fba3497` (Builds commit dated 2026-09-20, the gitlink at spine commit `b4ed7ed7`) to `ba4ca78c` (Builds commit dated 2026-10-05). That move changed five Stack rows: Aspire, the CommunityToolkit DAPR integration, the Dapr .NET SDK, Fluent UI and OpenTelemetry. The repository catalog is current with upstream for .NET, ASP.NET Core, Aspire, the Dapr SDK, Fluent UI, OpenTelemetry and the test stack.

There is one security-relevant patch gap. The deployment examples pin `daprio/daprd:1.18.0`, which predates the dependency CVE fixes shipped in DAPR `1.18.2`. One deployment guide still uses the mutable tag `latest`. CI runs `1.18.2`, which is two patches behind the latest stable `1.18.4`. No published DAPR security advisory affects 1.18.x.

## Method And Evidence Boundary

1. Root `HEAD` `27ac3c62`. Pinned files have no worktree diff: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.github/workflows/`, `deploy/` and the AppHost project were checked with `git diff --quiet HEAD`. The Builds submodule worktree is clean at its gitlink.
2. The dependency catalog is `references/Hexalith.Builds/Props/Directory.Packages.props`. Root `Directory.Packages.props:5,12` imports it, as does `Directory.Build.props:4,8`. It defines its version variables inline, for example `HexalithFrontComposerVersion` and `HexalithAspireHostingDaprVersion`, and imports no further props.
3. Upstream sources: .NET release metadata JSON; the GitHub Releases API for `dapr/dapr`, `dapr/cli`, `dapr/dotnet-sdk`, `dapr/components-contrib`, `microsoft/aspire`, `CommunityToolkit/Aspire`, `microsoft/fluentui-blazor`, `open-telemetry/opentelemetry-dotnet` and `openbao/openbao`; the GitHub security-advisory API for `dapr/dapr`, `dapr/components-contrib` and `open-telemetry/opentelemetry-dotnet`; NuGet v3 flat-container and registration endpoints, which give publish dates and deprecation and vulnerability flags; and the current DAPR docs, which carry the v1.18 banner.

## Repository Reality (2026-10-07)

| Item | Value | Evidence |
| --- | --- | --- |
| Builds gitlink | `ba4ca78c3868a4757cb92d912a54c8a237871b54` (`v4.29.1-22-gba4ca78`, 2026-10-05 "fix(pack): remove Npgsql package version 10.0.3") | `git ls-tree HEAD references/Hexalith.Builds`; last root bump `52bcb94f` (2026-10-05) |
| Builds gitlink at the 2026-09-23 Stack commit | `2fba3497` (2026-09-20) | `git ls-tree b4ed7ed7 references/Hexalith.Builds` |
| .NET SDK | `10.0.401`, `rollForward: latestPatch`; test runner `Microsoft.Testing.Platform` | `global.json:3-4,7` |
| Target framework | `net10.0` | `Directory.Build.props:60` |
| ASP.NET Core / SignalR | `10.0.12` | Builds catalog `:196-217` (SignalR.Client `:215`) |
| Aspire.Hosting family | `13.6.0`; Keycloak and Kubernetes hosting `13.6.0-preview.1.26479.8` | Builds catalog `:131-143` (`Aspire.Hosting` `:135`; previews `:139-140`) |
| AppHost SDK | `Aspire.AppHost.Sdk/13.6.0` (commit `308bc9f4`, 2026-10-01) | `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj:1` |
| CommunityToolkit Aspire DAPR | `13.6.0-preview.1.261001-0243` through `$(HexalithAspireHostingDaprVersion)`; `Hexalith.Folders.Aspire` is overridden to stable `13.0.0` | Builds catalog `:16-18,158`; AppHost reference `.csproj:33` |
| Dapr .NET SDK | `1.18.10` (Client, AspNetCore, Actors, Actors.AspNetCore, Actors.Generators, AI, Workflow) | Builds catalog `:161-168` |
| DAPR CLI / runtime (CI) | CLI `1.18.0`, runtime `1.18.2` | `.github/workflows/integration.yml:24-25,66-70`; shared action default `references/Hexalith.Builds/Github/dapr-init/action.yml:8`, `dapr init --runtime-version` at `:98` |
| DAPR runtime (deployment examples) | `daprio/daprd:1.18.0` ×3; pin-not-`latest` guidance | `deploy/README.md:254,272,290,309` |
| DAPR runtime (Docker Compose guide) | `daprio/daprd:latest` ×3 | `docs/guides/deployment-docker-compose.md:226,252,273` |
| PostgreSQL state component | `state.postgresql` `v1`, `actorStateStore` metadata | `deploy/dapr/statestore-postgresql.yaml:20-21,28` |
| PostgreSQL test image | `postgres@sha256:a02db8ca…1636` | `integration.yml:82`; `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs:29` |
| OpenBao secret store | Specified only (`openbao` → `secretstores.hashicorp.vault` v1). No committed component: `deploy/dapr/openbao-secret-contract.yaml` does not exist | `_bmad-output/specs/spec-eventstore-phase-4-readiness-recovery/requirements-traceability.md:131-132`; `deploy/dapr/` listing |
| MediatR / FluentValidation | `14.2.0` / `12.1.1` | Builds catalog `:194` / `:173` |
| FrontComposer / Fluent UI | `4.5.0` / `5.0.0` (Components + Icons) | Builds catalog `:10` / `:250-251` |
| OpenTelemetry | core, InMemory, OTLP and Hosting `1.19.1`; Instrumentation AspNetCore, Http and Runtime `1.19.0`; GrpcNetClient `1.19.1-beta.1` | Builds catalog `:290-297` |
| Code coverage | `Microsoft.Testing.Extensions.CodeCoverage` `18.11.2` | Builds catalog `:267` |
| Test stack | xunit.v3 `4.0.1`, Shouldly `4.3.0`, NSubstitute `6.2.0` | Builds catalog `:343`, `:318`, `:283` |

Builds catalog third-party deltas since the Stack date (`2fba3497..ba4ca78c`): Aspire 13.5.4 → 13.6.0; CommunityToolkit DAPR 13.5.1-beta.757 → 13.6.0-preview.1.261001-0243; Dapr SDK 1.18.8 → 1.18.10; Fluent UI 5.0.0-rc.5-26219.1 → 5.0.0; OpenTelemetry core 1.19.0 → 1.19.1; coverlet 10.0.1 → 10.1.0; StackExchange.Redis 3.3.0 → 3.3.1; Grpc.* 2.83.0 → 2.84.0; Microsoft.Identity.Web 4.14.2 → 4.15.0; Playwright 1.62.0 → 1.63.0. Npgsql was removed from the catalog. The .NET, MediatR, FluentValidation, CodeCoverage and test-stack pins are unchanged.

## Upstream Currentness (2026-10-07)

| Technology | Latest official | Date | Source | Notes |
| --- | --- | --- | --- | --- |
| .NET 10 | Runtime and ASP.NET `10.0.12`, SDK `10.0.401` (also `10.0.112`) | 2026-09-08 | https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json | `security: true` (CVE-2026-69439, -71328, -69522, -69304, -58649, -69806); support phase `active`. The repository is current. The next monthly servicing window falls on the second Tuesday, 2026-10-13; it is not yet published. |
| DAPR runtime | Stable `1.18.4` | 2026-09-09 | https://github.com/dapr/dapr/releases/tag/v1.18.4 | Prereleases: `1.18.5-rc.2` (2026-10-06), whose notes include "Update deps for CVE", "Fix go.mod vulns" and sentry trust-bundle file permissions; and `1.19.0-rc.1` (2026-10-01). `1.18.3` (2026-08-14) fixed "all sidecars in a namespace disconnected from Placement" and "actor state store components cannot be hot reloaded". `1.18.2` (2026-07-21) fixed CVE-2025-69725 (go-chi) and CVE-2026-2303 (mongo-driver) in daprd: https://github.com/dapr/dapr/releases/tag/v1.18.2 |
| DAPR security advisories | None affecting 1.18.x | — | https://github.com/dapr/dapr/security/advisories | The latest advisory is GHSA-85gx-3qv6-4463 / CVE-2026-41491 (affects ≤1.17.4; 2026-04-16). |
| DAPR CLI | `1.18.2` | 2026-08-21 | https://github.com/dapr/cli/releases | 1.18.1 and 1.18.2 contain functional fixes only. |
| Dapr .NET SDK | `1.18.10` | NuGet 2026-09-24 (GitHub 2026-09-26) | https://github.com/dapr/dotnet-sdk/releases | `1.19.0-preview.2` is prerelease only. The repository is current. |
| components-contrib | `1.18.5` | 2026-09-25 | https://github.com/dapr/components-contrib/releases | No published advisory. |
| Aspire | `13.6.0` (Aspire.Hosting and AppHost.Sdk) | 2026-09-29 | https://github.com/microsoft/aspire/releases/tag/v13.6.0 | Has breaking changes (https://aspire.dev/whats-new/aspire-13-6/#breaking-changes). The repository is current. |
| CommunityToolkit Aspire DAPR | `13.6.0-preview.1.261001-0243` | NuGet 2026-10-01 | https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.Dapr | Toolkit release v13.6.0 is dated 2026-10-01, but the DAPR integration ships only as prerelease. The last stable package is `13.0.0` (2025-11-25). |
| PostgreSQL state store v1 | Stable, actors supported | Docs v1.18 | https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v1/ | "There are no plans to deprecate the v1 component." v2 "is not compatible with v1, and data cannot be migrated between the two components." |
| OpenBao secret store | Through `secretstores.hashicorp.vault` v1 | Page modified 2026-10-06 | https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/ | "Currently, there is no dedicated OpenBao Secrets Store." The Vault component "has been tested and confirmed to work effectively". The secret-store table lists OpenBao as Stable. |
| OpenBao server | `2.7.1` / `2.6.4` | 2026-10-01 | https://github.com/openbao/openbao/releases | Security releases: GHSA-3237-j65r-m5rp, GHSA-q74w-hv5x-6hxf, GHSA-7m59-mp95-w6ph (AppRole expired Secret ID still authenticates), GHSA-5v22-543h-wg96. |
| Fluent UI Blazor | `5.0.0` GA | NuGet 2026-09-25; GitHub 2026-09-28 | https://github.com/microsoft/fluentui-blazor/releases/tag/v5.0.0 | The RC exception is retired. |
| FrontComposer | `4.6.0` | NuGet 2026-10-05 | https://www.nuget.org/packages/Hexalith.FrontComposer.Shell | The 4.6.0 nuspec depends on Fluent UI `5.0.0`; the pinned 4.5.0 (2026-09-19) depends on `5.0.0-rc.5-26219.1`. The FrontComposer submodule is at `v4.6.0-5-gc561b321`. |
| OpenTelemetry .NET | core `1.19.1`; instrumentation `1.19.0` | 2026-09-21 | https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.19.1 | Non-security fix. Advisories from 2026-04 were patched in 1.15.3. The repository is current. |
| CodeCoverage | `18.12.0` | NuGet 2026-10-06 | https://www.nuget.org/packages/Microsoft.Testing.Extensions.CodeCoverage | No vulnerability flag. |
| MediatR / FluentValidation | `14.2.0` / `12.1.1` | — | NuGet flat container | Current. |
| xUnit v3 / Shouldly / NSubstitute | `4.0.1` / `4.3.0` (5.0.0 preview only) / `6.2.0` | — | NuGet flat container | Current. |

## Proposed Refreshed Stack Section

> This is the repository state observed on 2026-10-07 at root `HEAD` `27ac3c62`. The Builds catalog at the root-declared gitlink `ba4ca78c` remains the sole dependency authority. Upstream availability never authorizes an upgrade; every change goes through a separately tested Builds or workflow refresh.

| Name | Repository value | Current evidence / posture |
| --- | --- | --- |
| Dependency authority | `references/Hexalith.Builds` gitlink `ba4ca78c` (2026-10-05) | Sole package-version authority; no local overrides |
| .NET SDK | `10.0.401`, `rollForward: latestPatch` | Current. This is the latest SDK for the 10.0.12 security servicing release (2026-09-08) |
| Target framework | `net10.0` | Retain; .NET 10 support is active |
| ASP.NET Core / SignalR | `10.0.12` | Current security servicing release; matches the SDK runtime |
| Aspire.Hosting / AppHost SDK | `13.6.0` / `Aspire.AppHost.Sdk/13.6.0` | Current (2026-09-29); Keycloak and Kubernetes hosting remain preview `13.6.0-preview.1.26479.8` |
| CommunityToolkit Aspire DAPR | `13.6.0-preview.1.261001-0243` (`Hexalith.Folders.Aspire`: `13.0.0`) | Latest published (2026-10-01). Upstream has no stable release after 13.0.0. The preview-channel exception remains explicit |
| DAPR runtime | CI `1.18.2` (CLI `1.18.0`); deployment examples `1.18.0` | DAPR `1.18.4` is the latest stable (2026-09-09); 1.18.5 and 1.19.0 are RC only. The example `1.18.0` predates the 1.18.2 daprd CVE fixes. AD-26 requires one tested production pin |
| Dapr .NET SDK | `1.18.10` | Current (2026-09-24) |
| PostgreSQL state component | stable `state.postgresql` v1 | DAPR docs: v1 Stable with actor support and no deprecation plans. v2 is incompatible and has no data migration path, so AD-26 retains v1 |
| OpenBao secret store | `secretstores.hashicorp.vault` v1 (specified; no committed component) | DAPR docs confirm the Vault v1 component for OpenBao. Required only in the AD-26 production profile, which must bind an OpenBao server security floor (2.7.1 / 2.6.4, 2026-10-01) |
| MediatR / FluentValidation | `14.2.0` / `12.1.1` | Repository authority; latest stable |
| FrontComposer / Fluent UI | `4.5.0` / `5.0.0` | Fluent UI v5 is GA (2026-09-25), so the RC exception is retired. The pinned FrontComposer 4.5.0 was built against rc.5; FrontComposer 4.6.0 is built against 5.0.0 |
| OpenTelemetry | `1.19.1` (instrumentation `1.19.0`) | Current. Exporter and cardinality budgets remain deployment-gated |
| Code coverage | `18.11.2` | Repository catalog pin; `18.12.0` is available (2026-10-06) |
| Test stack | xUnit `4.0.1`, Shouldly `4.3.0`, NSubstitute `6.2.0` | Repository catalog pin; latest stable |

Suggested source-list additions for the spine frontmatter: `https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v1/` (it carries the v1 no-deprecation and no-migration statements), `https://github.com/dapr/dapr/releases/tag/v1.18.2` (the CVE fixes) and `https://github.com/microsoft/fluentui-blazor/releases/tag/v5.0.0`.

## Notable Gaps

1. **Spine Stack rows are stale against the catalog (medium).** Aspire `13.5.4`→`13.6.0` (`:135`), CommunityToolkit DAPR `13.5.1-beta.757`→`13.6.0-preview.1.261001-0243` (`:17,158`), Dapr SDK `1.18.8`→`1.18.10` (`:161-168`), Fluent UI `rc.5`→`5.0.0` (`:250-251`, so the "explicit RC exception" posture is now false) and OpenTelemetry `1.19.0`→`1.19.1` (`:290-293`). All are at Builds `ba4ca78c`; spine lines 373-381 still show the old values.
2. **Security-relevant DAPR patch gap in deployment examples (high for examples, medium overall).** `deploy/README.md:254,272,290` pin `daprio/daprd:1.18.0`, which lacks the daprd fixes for CVE-2025-69725 (go-chi) and CVE-2026-2303 (mongo-driver) shipped in 1.18.2 (https://github.com/dapr/dapr/releases/tag/v1.18.2, 2026-07-21). `docs/guides/deployment-docker-compose.md:226,252,273` use mutable `latest`, which contradicts `deploy/README.md:309`. CI `integration.yml:25` runtime `1.18.2` is behind the latest stable `1.18.4` (2026-09-09); 1.18.3 includes a fix for a Placement mass-disconnect that halts actors. No GHSA affects 1.18.x. `1.18.5-rc.2` (2026-10-06) carries further dependency CVE updates and should be watched.
3. **DAPR CLI pin lags (low).** `integration.yml:24` and `references/Hexalith.Builds/Github/dapr-init/action.yml:8` use CLI `1.18.0`; the latest is `1.18.2` (2026-08-21, functional fixes only).
4. **The FrontComposer and Fluent UI pair is not co-built (medium).** Catalog `:10` pins FrontComposer `4.5.0`, whose nuspec requires Fluent UI `5.0.0-rc.5-26219.1`, while `:250-251` force `5.0.0` GA through central transitive pinning. FrontComposer `4.6.0` (NuGet 2026-10-05, built against 5.0.0) exists, and the submodule is already at `v4.6.0-5-gc561b321`. The change belongs in Builds, not EventStore.
5. **OpenBao has no committed component or server floor (medium, production-profile only).** No `deploy/dapr` OpenBao component or `openbao-secret-contract.yaml` exists. The DAPR docs (modified 2026-10-06) confirm `secretstores.hashicorp.vault` v1 as the OpenBao path. OpenBao `2.7.1` / `2.6.4` (2026-10-01, https://github.com/openbao/openbao/releases) fix four GHSAs, including expired AppRole Secret IDs still authenticating, so the AD-26 profile should bind that floor.
6. **Secondary docs carry stale version claims (low).** `docs/ci.md:65` says the Dapr NuGet version is "currently `1.18.5`" (actual `1.18.10`). `docs/brownfield/project-overview.md:46,57` say Dapr `1.18.5` and OpenTelemetry `1.18.0`. There is no .NET gap: `10.0.12` / SDK `10.0.401` is the latest (2026-09-08), and the next servicing window, expected 2026-10-13, is not yet published. The CodeCoverage `18.12.0` refresh opportunity is catalog-owned.
