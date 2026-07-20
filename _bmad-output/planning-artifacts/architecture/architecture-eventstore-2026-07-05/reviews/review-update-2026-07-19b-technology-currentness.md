# Technology Currentness Review — 2026-07-19b Delta (AD-11 Container-Release Contract, AD-22 Identity Clarification)

- **Reviewer lens:** every committed decision web-researched or reality-checked, not asserted from training data.
- **Scope:** tonight's delta (AD-11 container-release contract, AD-22 container-identity clarification) plus Stack-table drift spot-checks against repository files at review time.
- **Repository state checked:** superproject `main` (recent HEAD `00314259`), Builds submodule `references/Hexalith.Builds` at `ffa1662` (2026-07-19 19:54).

## Verdict

**CHANGES REQUIRED** — scoped. Every delta claim in AD-11 and AD-22 verified clean against the web (OCI image-spec, Microsoft Learn, Docker/BuildKit docs) and against the repository (shared publisher, validator, manifest, workflows); **no change to tonight's delta text is required.** The verdict is driven by the Stack spot-checks: six version rows are stale against the live `references/Hexalith.Builds/Props/Directory.Packages.props`, and one of them (FrontComposer `3.2.2`) is also hard-coded inside ADOPTED decision AD-21 while the sole version authority has pinned `4.0.1` since 2026-07-16 — making an AD rule contradict the catalog it defers to. Route the reconciliation through Story 3.11 (version rows) plus an AD-21 touch-up.

## Delta claim verification

### Claim 1 — OCI image index media type and multi-platform mechanism: VERIFIED

- The OCI image-spec (`opencontainers/image-spec`, `image-index.md`) confirms the exact media type `application/vnd.oci.image.index.v1+json` and that the index is the mechanism for multi-platform tags: each `manifests[]` descriptor carries a `platform` object with `os` and `architecture` (GOOS/GOARCH values, e.g. `linux`/`amd64`, `linux`/`arm64`) and optional `variant`.
- Child image configs declaring `os`/`architecture` is standard OCI image-config; the repository's own validator (`references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py`) enforces exactly what AD-11 states: index media type must be `application/vnd.oci.image.index.v1+json` (line 17), `REQUIRED_PLATFORMS = ("linux/amd64", "linux/arm64")` (line 24), platform set must be exactly those two (`platform-set-mismatch`, line 271), `unknown` os/architecture rejected (`unknown-platform`, lines 258-259), variants rejected (`variant-not-allowed`, lines 260-261), and each child config's media type and os/architecture are checked against the descriptor.
- AD-11's prose and the enforcing validator agree line-for-line. No drift.

### Claim 2 — .NET SDK container support produces the multi-arch index without a Dockerfile: VERIFIED

- Microsoft Learn ("Containerize a .NET app reference", `learn.microsoft.com/dotnet/core/containers/publish-configuration`): SDK container support is built in since SDK 8.0.200, requires no Dockerfile, and **multi-RID container publishing is supported beginning with SDK 8.0.405 / 9.0.102 / 9.0.2xx**: when multiple `RuntimeIdentifiers`/`ContainerRuntimeIdentifiers` are set with `/t:PublishContainer`, "the SDK publishes the app for each specified RID and combines the resulting images into an OCI Image Index." The .NET 10 SDK `10.0.302` (repository `global.json`) therefore includes this workflow.
- Repository reality matches exactly: `references/Hexalith.Builds/Github/publish-containers/publish-containers.sh` runs `dotnet publish /t:PublishContainer` with `ContainerRuntimeIdentifiers="linux-musl-x64;linux-musl-arm64"` and `-p:ContainerImageFormat=OCI` (lines 9, 92-102) — the documented SDK index-assembly path, no Dockerfile, no BuildKit.
- Nuance (Info, no action): the RIDs are the musl (Alpine) variants; their OCI platforms are still `os=linux` + `architecture=amd64|arm64` with no `variant`, so the spine's platform-set wording and the validator's variant prohibition remain consistent.

### Claim 3 — Forbidding `unknown/unknown` descriptors is coherent for SDK-published indexes: VERIFIED

- Docker/BuildKit attestation-storage documentation (docs.docker.com "Image attestation storage", moby/buildkit `attestation-storage.md`) confirms that BuildKit provenance/SBOM attestations are stored as extra manifests in the image index with `platform: {os: "unknown", architecture: "unknown"}` (one per platform image), linked via the `vnd.docker.reference.digest` annotation. This is the exact shape AD-11 forbids.
- Coherence: the Hexalith publish path never invokes BuildKit for the released manifest — the SDK pushes the index directly — so a conforming SDK-published release contains only the two platform descriptors and can never trip the prohibition. The rule therefore functions purely as a fail-closed guard against a pipeline regression (e.g. someone re-introducing a buildx path with default provenance enabled, which since Buildx 0.10 adds these entries by default). Coherent and enforced (`unknown-platform` in the validator).
- Advisory (Info): as written, AD-11 also permanently excludes adopting BuildKit-style *in-index* attestations later without an AD-11 revision. If supply-chain attestation is ever wanted, the OCI referrers-API style (attestations outside the index) remains compatible with the rule. Worth remembering; no change requested.

### Claim 4 — Repository reality of the release chain: VERIFIED

- **Shared publisher:** `.github/workflows/release.yml` line 31 delegates to `Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@9ec0a032d785dd0abdc14276e8784d6fdd826fd0` with `publish-containers: true` and a matching `builds-execution-sha` — the publisher is shared from Hexalith.Builds and pinned by exact SHA, as AD-11 implies.
- **One container repository named `eventstore`:** `container-projects` (release.yml lines 39-40) maps exactly one project: `src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore`. The publish script validates the `project|repository` shape and the repository-name grammar.
- **14-package inventory:** `tools/release-packages.json` lists exactly 14 packages (Contracts, Client, Server, SignalR, Testing, Testing.Integration, Aspire, ServiceDefaults, DomainService, RestApi.Generators, Gateway, Admin.Abstractions, Admin.Cli, Admin.Server). Matches the Boundaries/Release row ("inventory remains 14 packages until Story 8.2").
- **Manifest-driven publish:** `.releaserc.json` `prepareCmd` runs `tools/pack-release-packages.py` + `tools/validate-release-packages.py`; the pack script reads `tools/release-packages.json` as `MANIFEST` (line 15) and fails on duplicates/absence. The manifest is genuinely load-bearing, not documentation.
- **AD-22 identity clarification:** the "validated OCI image index digest" as the canonical deployed identity is consistent with the validator, which binds raw registry bytes' SHA-256 to the registry-reported index digest and records per-child digests as subordinate evidence — matching AD-22's "child manifest digest counts only by mapping to that index," never as a substitute identity. Internally consistent; no external claim to re-verify beyond Claims 1-3.
- **arm64 smoke feasibility:** `domain-release.yml` sets up QEMU arm64 emulation (docker/setup-qemu-action, lines 211-215) before publishing, and AD-11 correctly classifies emulation/setup failure as separately reported, never a product pass — matching `smoke-container-platforms.sh` wiring.

## Stack table spot-checks (against repository files, not memory)

Checked against `global.json`, `.github/workflows/integration.yml`, and `references/Hexalith.Builds/Props/Directory.Packages.props` at Builds submodule HEAD `ffa1662` (2026-07-19).

| Row | Spine | Repository | Status |
| --- | --- | --- | --- |
| .NET SDK | `10.0.302`, `rollForward: latestPatch` | `global.json`: identical | OK |
| DAPR runtime seed | `1.18.0` | `integration.yml` `DAPR_VERSION: '1.18.0'`; web: v1.18.0 released 2026-06-10, current stable line | OK |
| Dapr .NET SDK | 1.18.4 | catalog: all Dapr.* 1.18.4 | OK |
| Aspire.Hosting / Keycloak+Kubernetes | 13.4.6 / 13.4.6-preview.1.26319.6 | catalog: identical | OK |
| MediatR / FluentValidation / CodeAnalysis / FluentUI / xUnit v3 / Shouldly | 14.2.0 / 12.1.1 / 5.6.0 / 5.0.0-rc.4-26180.1 / 3.2.2 / 4.3.0 | catalog: identical (xunit.v3 + assert + extensibility.core all 3.2.2) | OK |
| Hexalith.FrontComposer.Shell / Contracts.UI | `3.2.2` | catalog: `HexalithFrontComposerVersion` = **4.0.1** (3.2.2 → 4.0.0 at Builds `12aaed6`, 2026-07-16; now 4.0.1); **`Hexalith.FrontComposer.Contracts.UI` has no catalog pin at all** (only Contracts, Mcp, Shell, SourceTools, Testing) | STALE (F1) |
| CommunityToolkit.Aspire.Hosting.Dapr | 13.4.0-preview.1.260602-0230 | catalog: **13.4.1-beta.686** | STALE (F3) |
| OpenTelemetry exporter/hosting | 1.16.0 | catalog: **1.17.0** | STALE (F2) |
| OpenTelemetry runtime instrumentation | 1.15.1 | catalog: **1.17.0** | STALE (F2) |
| NSubstitute | 6.0.0-rc.1 | catalog: **6.0.0** (stable) | STALE (F4) |
| Hexalith.Commons.UniqueIds | 2.28.1 | catalog: **2.28.2** | STALE (F4) |
| ASP.NET Core / SignalR | "Repository seed `10.0.9`; required security baseline `10.0.10`" | catalog: every Microsoft.AspNetCore.*/SignalR pin is **10.0.10**; only two Microsoft.Extensions stragglers (Identity.Http, Localization) remain 10.0.9 | MISLEADING (F5) |

## Findings

### High

None. All delta claims verified; the delta text requires no change.

### Medium

- **F1 — FrontComposer `3.2.2` contradicts the version authority the spine itself designates.** AD-11 declares `references/Hexalith.Builds/Props/Directory.Packages.props` the sole source-owned NuGet version catalog, yet the Stack row (line 372), ADOPTED AD-21 (line 286), and the Boundaries UI row (line 349) all pin FrontComposer Shell/Contracts.UI at `3.2.2` while the catalog has pinned `4.0.1` since 2026-07-16 (a major-version jump, which under AD-11 itself "requires explicit proof"). AD-21's claim that "Debug source and Release package modes resolve the same package boundary" is unsatisfiable as written: a Release package restore resolves 4.0.1. Additionally, `Hexalith.FrontComposer.Contracts.UI` does not exist in the catalog at all, so the spine names a package with no pinned version. No tracked EventStore source currently references Shell/Contracts.UI (Story 7.14 is future work), so nothing is broken today — but the earlier 2026-07-19 gate passed over drift that already existed. Fix: Story 3.11 refreshes the Stack row from accepted evidence (or records the hold-at-3.2.2 as an explicit AD-11 compatibility exception with rationale and removal trigger), and AD-21 should reference the catalog-governed FrontComposer version rather than a hard-coded literal, or re-ratify the literal against the catalog.
- **F2 — OpenTelemetry rows stale.** Spine: exporter/hosting 1.16.0, runtime instrumentation 1.15.1; catalog: 1.17.0 for both families. AD-11 requires OpenTelemetry packages to "move coherently" — the catalog has moved coherently; the spine's two rows lag it and additionally disagree with each other about how far they lag. Story 3.11 refresh.

### Low

- **F3 — CommunityToolkit.Aspire.Hosting.Dapr row stale:** spine 13.4.0-preview.1.260602-0230 vs catalog 13.4.1-beta.686 (also a preview→beta channel-label change worth an explicit note under AD-11's channel rules). Story 3.11.
- **F4 — Two small stale pins:** NSubstitute spine 6.0.0-rc.1 vs catalog 6.0.0 stable (the rc has been released); Hexalith.Commons.UniqueIds spine 2.28.1 vs catalog 2.28.2. Story 3.11.
- **F5 — ASP.NET row wording misleading:** "Repository seed `10.0.9`" no longer describes any Microsoft.AspNetCore.*/SignalR pin — the whole family is 10.0.10 in the catalog (memory-independent check: grep of the props file). Only two Microsoft.Extensions packages remain at 10.0.9, and they are not ASP.NET Core/SignalR packages. Reword or refresh at 3.11.

### Info

- **I1 — RID/platform nuance:** the publish script uses `linux-musl-x64;linux-musl-arm64`; OCI platforms remain `linux/amd64` + `linux/arm64` with no `variant`, so the spine's platform-set language and the validator's variant prohibition are mutually consistent. No action.
- **I2 — Attestation forward-compatibility:** forbidding `unknown/unknown` descriptors permanently excludes BuildKit-style in-index attestations from the release index; OCI referrers-API attestations remain compatible if supply-chain evidence is ever wanted. Note for any future supply-chain story; no change requested.
- **I3 — Nonconforming-release example is real:** AD-11's "(v3.75.0)" nonconforming-release citation corresponds to actual catalog history (`HexalithEventStoreVersion` passed through 3.75.0 on 2026-07-19 and is now 3.77.2), consistent with the immutable-failed-evidence rule.

## Sources

- OCI image index spec: https://github.com/opencontainers/image-spec/blob/main/image-index.md
- Microsoft Learn — Containerize a .NET app reference (multi-arch `ContainerRuntimeIdentifiers` → OCI Image Index; SDK 8.0.405/9.0.102/9.0.2xx floor): https://learn.microsoft.com/dotnet/core/containers/publish-configuration
- Microsoft Learn — .NET application publishing overview (Dockerfile-less SDK container publish): https://learn.microsoft.com/dotnet/core/deploying/
- Microsoft Learn — Containerize a .NET app with dotnet publish: https://learn.microsoft.com/dotnet/core/containers/sdk-publish
- Docker — Image attestation storage (`unknown/unknown` attestation manifests): https://docs.docker.com/build/metadata/attestations/attestation-storage/
- BuildKit attestation storage spec: https://github.com/moby/buildkit/blob/master/docs/attestations/attestation-storage.md
- Dapr v1.18.0 release: https://github.com/dapr/dapr/releases/tag/v1.18.0 and https://blog.dapr.io/posts/2026/06/10/dapr-v1.18-is-now-available/
- Repository files: `.github/workflows/release.yml`, `.github/workflows/integration.yml`, `global.json`, `tools/release-packages.json`, `tools/pack-release-packages.py`, `.releaserc.json`, `references/Hexalith.Builds/.github/workflows/domain-release.yml`, `references/Hexalith.Builds/Github/publish-containers/{publish-containers.sh,oci_registry_validator.py}`, `references/Hexalith.Builds/Props/Directory.Packages.props` (@ `ffa1662`).
