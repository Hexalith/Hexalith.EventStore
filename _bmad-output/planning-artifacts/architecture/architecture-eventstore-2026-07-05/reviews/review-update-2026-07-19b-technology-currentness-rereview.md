# Technology Currentness Re-Review — 2026-07-19b Corrections (F1-F5 Closure Verification)

- **Reviewer lens:** every committed claim reality-checked against the repository at review time, not asserted from training data or from the prior review's text.
- **Scope:** closure verification of prior findings F1-F5 (review-update-2026-07-19b-technology-currentness.md), plus re-verification of AD-21's rewritten claim set, AD-11's release-chain/validator claims against the actual validator source, and drift spot-checks of the unchanged Stack rows.
- **Repository state checked:** superproject `main` (working tree; `architecture.md` carries the uncommitted correction edits), Builds submodule `references/Hexalith.Builds` at `ffa1662` (2026-07-19 19:54), FrontComposer submodule at `c585073c` (`v4.0.1-78-gc585073c`), version authority `references/Hexalith.Builds/Props/Directory.Packages.props` read in full.

## Verdict

**PASS.** All five prior findings are closed by the correction edits, and every corrected sentence was re-verified against the repository rather than against the correction's own claims. The seven touched Stack rows now match `references/Hexalith.Builds/Props/Directory.Packages.props` exactly; AD-21's rewritten FrontComposer claim set is accurate on all three legs (catalog variable, missing `Contracts.UI` pin, no current source reference); AD-11's release-chain and validator claims match `.github/workflows/release.yml` and the validator source line-for-line — and the validator at the workflow's pinned Builds SHA is byte-identical to the submodule copy reviewed. The unchanged Stack rows show no silent drift. Remaining observations are Info-tier only; no spine change is required.

## Per-finding closure

### F1 — FrontComposer `3.2.2` contradiction: CLOSED

Prior defect: Stack row, ADOPTED AD-21, and the Boundaries UI row hard-coded FrontComposer `3.2.2` while the catalog pinned `4.0.1`, and the spine named `Hexalith.FrontComposer.Contracts.UI`, a package with no catalog pin.

Verified now, against the catalog and tracked sources:

1. **Catalog variable and value.** `Directory.Packages.props` line 9: `HexalithFrontComposerVersion` = `4.0.1`. Lines 52-56 pin exactly five packages under that one variable: `Hexalith.FrontComposer.Contracts`, `.Mcp`, `.Shell`, `.SourceTools`, `.Testing`. The Stack row ("Catalog `HexalithFrontComposerVersion` `4.0.1`…"), AD-21 ("single `HexalithFrontComposerVersion` variable (currently `4.0.1`)"), and the Boundaries UI row ("at the Builds catalog's single `HexalithFrontComposerVersion` (currently `4.0.1`)") all now defer to the variable and quote its current value correctly. No `3.2.2` literal survives anywhere in the spine (the only remaining `3.2.2` is xUnit v3, which is genuinely `3.2.2` in the catalog — coincidence, not residue).
2. **`Contracts.UI` gap now stated, not papered over.** The catalog has **no** `Hexalith.FrontComposer.Contracts.UI` pin — confirmed both by reading the props file and independently by NuGet's restore-time CPM echo (`src/Hexalith.EventStore.Admin.UI/obj/project.assets.json` `centralPackageVersions` lists exactly the five pinned FrontComposer packages, no `Contracts.UI`). AD-21's new sentence — "a consumed package absent from the catalog (`Contracts.UI` today) is added there under that variable before adoption, never pinned locally" — matches that reality exactly and converts the prior contradiction into an explicit adoption precondition.
3. **The package boundary exists in source.** `.gitmodules` lines 13-15 declare `references/Hexalith.FrontComposer` as a root submodule, and it contains both `src/Hexalith.FrontComposer.Contracts/Hexalith.FrontComposer.Contracts.csproj` and `src/Hexalith.FrontComposer.Contracts.UI/Hexalith.FrontComposer.Contracts.UI.csproj`. So the spine's dependency diagram (Shell → Contracts.UI) names real projects.
4. **No premature consumption.** `git grep FrontComposer` over tracked `src/`, `tests/`, `samples/` project/props/targets files returns zero package or project references — the only hits are a comment in `src/Hexalith.EventStore.Aspire/HexalithEventStorePlatformExtensions.cs` and governance-test prose in `tests/Hexalith.EventStore.Admin.UI.Tests/Governance/AdminUiFluentConformanceTests.cs`. `src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj` references only FluentUI Components/Icons, SignalR.Client, and JwtBearer plus four EventStore project references. The FrontComposer `4.0.1` strings inside `obj/` restore artifacts are the central-package-version echo NuGet writes into every assets file, not dependencies (verified by parsing the assets JSON: direct deps contain no FrontComposer, no library in the resolved graph depends on FrontComposer). AD-21's "before adoption" framing is therefore literally true today, and the prior unsatisfiable "same package boundary at the same version" rule is now satisfiable because the version is the catalog variable rather than a stale literal.

### F2 — OpenTelemetry rows stale: CLOSED

Catalog: `OpenTelemetry`, `Exporter.InMemory`, `Exporter.OpenTelemetryProtocol`, `Extensions.Hosting`, `Instrumentation.AspNetCore`, `Instrumentation.GrpcNetClient`, `Instrumentation.Http`, `Instrumentation.Runtime` are all `1.17.0` (lines 263-270); `Instrumentation.StackExchangeRedis` is `1.16.0-beta.1` (line 272, with an in-catalog rationale comment citing Hexalith.Memories ADR-8.5-001(b) and an upgrade-on-GA trigger — i.e., an AD-11-conformant recorded exception). Spine rows now read `1.17.0` for exporter/hosting/ASP.NET/HTTP and `1.17.0 (StackExchangeRedis instrumentation `1.16.0-beta.1`)` for runtime instrumentation. Exact match, and the one intentional beta is called out rather than averaged away. The prior self-inconsistency (1.16.0 vs 1.15.1) is gone.

### F3 — CommunityToolkit.Aspire.Hosting.Dapr stale: CLOSED

Catalog line 135: `13.4.1-beta.686`. Spine row now `13.4.1-beta.686`. Exact match.

### F4 — NSubstitute / Commons.UniqueIds stale: CLOSED

Catalog line 256: NSubstitute `6.0.0` (stable); spine row `6.0.0`. Catalog line 6: `HexalithCommonsVersion` = `2.28.2`, and line 36 pins `Hexalith.Commons.UniqueIds` to that variable; spine row `2.28.2`. Both exact.

### F5 — ASP.NET seed wording misleading: CLOSED

Spine row now: "ASP.NET Core / SignalR packages | `10.0.10` (catalog and security baseline aligned)". Catalog: every `Microsoft.AspNetCore.*` pin including `SignalR.Client` and `SignalR.StackExchangeRedis` is `10.0.10` (lines 170-191). The two remaining `10.0.9` pins (`Microsoft.Extensions.Identity.Http` line 212, `Microsoft.Extensions.Localization` line 215) are Microsoft.Extensions packages, outside the row's stated scope, so the reworded row is accurate as written. AD-11's sentence "The repository SDK seed and verified security baseline are `10.0.302` and ASP.NET `10.0.10` respectively" matches `global.json` (`10.0.302`, `rollForward: latestPatch`) and the catalog. The Stack `.NET SDK` row (`10.0.302` seed and baseline, `latestPatch`) matches `global.json` exactly.

## AD-11 release-chain and validator re-verification

Checked against `.github/workflows/release.yml` and `references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py`. Materially, `git diff 9ec0a032d…ffa1662 -- Github/publish-containers/` is **empty**: the validator (and the whole publish-containers directory) at the workflow's pinned Builds SHA is byte-identical to the submodule copy reviewed, so the claims below hold for the exact code the release runs.

| Spine claim (AD-11) | Repository reality | Status |
| --- | --- | --- |
| `container-projects` is exactly the single `eventstore` mapping | `release.yml` lines 39-40: one entry, `src/Hexalith.EventStore/Hexalith.EventStore.csproj\|eventstore` | VERIFIED |
| Publisher/validator is the SHA-pinned shared Builds workflow, one authority | `uses: Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@9ec0a032d785dd0abdc14276e8784d6fdd826fd0` with matching `builds-execution-sha` (lines 31, 36); commit exists in the Builds repo | VERIFIED |
| Tag must resolve to `application/vnd.oci.image.index.v1+json`; Docker manifest list or single-image manifest is nonconforming | `wrong-index-media-type` when index `mediaType` != OCI index type (lines 231-232), plus `content-type-mismatch` binding both tag and immutable response content types to the body's media type (229-230) and `wrong-schema-version` (233-234). A Docker list or bare manifest fails here | VERIFIED |
| Exactly one descriptor per platform, `linux/amd64` + `linux/arm64`, no duplicate/extra platform | `duplicate-platform` (268-269) and `platform-set-mismatch` against `REQUIRED_PLATFORMS` (270-271) | VERIFIED |
| No `unknown` platform | `unknown-platform` when os or architecture == `"unknown"` (258-259); blank values separately fail `blank-platform` (254-257) | VERIFIED |
| No platform `variant` field | `variant-not-allowed` unless `variant` is absent/empty (260-261) | VERIFIED |
| Descriptors directly reference image manifests; nested indexes nonconforming | Child descriptor `mediaType` must be OCI **or** Docker v2 image manifest (`CHILD_MANIFEST_MEDIA_TYPES`, line 22; `unsupported-child-media-type`, 247-248); an index-typed child fails, and the child body's own `mediaType` must equal the descriptor's (`child-media-type-mismatch`, 288-289) — index-shaped bodies also fail the config-descriptor requirement (291-293) | VERIFIED |
| Raw-bytes digest and size binding | Index: raw tag bytes' SHA-256 must equal the registry-reported digest (`tag-digest-body-mismatch`, 214-215), tag and immutable-digest fetches must be byte-identical (216-217), immutable response digest header re-bound (218-223). Children: body SHA-256 vs descriptor digest and response header (`child-digest-mismatch`, 277) **and** byte length vs descriptor size (`child-size-mismatch`, 278-279). Configs: `config-digest-mismatch` + `config-size-mismatch` (301-303) | VERIFIED |
| Every descriptor resolved | Unresolved tag/index/child/config each fail closed (`unresolved-*` codes wired through both the live client and the capture path) | VERIFIED |
| Child config `os`/`architecture` equals its descriptor | `config-platform-mismatch` (306-307) | VERIFIED |
| Every validation failure fails the gate closed | All checks raise `ValidationError`; `main()` returns 1 on any (523-525); no warn-and-continue path exists | VERIFIED |

No spine sentence misstates the validator. Two pedantic wording observations are recorded as Info (I3) below; neither describes an enforcement gap.

## Unchanged Stack rows — drift spot-check

All read directly from `Directory.Packages.props` at `ffa1662`:

| Row | Spine | Catalog | Status |
| --- | --- | --- | --- |
| Aspire.Hosting | 13.4.6 | 13.4.6 (line 112; whole Aspire family aligned) | OK |
| Aspire.Hosting.Keycloak / Kubernetes | 13.4.6-preview.1.26319.6 | identical (116-117) | OK |
| Dapr .NET SDK packages | 1.18.4 | all eight `Dapr.*` pins 1.18.4 (138-145) | OK |
| MediatR | 14.2.0 | 14.2.0 (168) | OK |
| FluentValidation | 12.1.1 | 12.1.1 (150; DI extensions 12.1.1) | OK |
| Microsoft.FluentUI.AspNetCore.Components | 5.0.0-rc.4-26180.1 | identical, Components + Icons (224-225) | OK |
| Microsoft.CodeAnalysis packages | 5.6.0 | all five `Microsoft.CodeAnalysis.*` pins 5.6.0 (194-199) | OK |
| xUnit v3 | 3.2.2 | `xunit.v3` + `assert` + `extensibility.core` all 3.2.2 (315-317) | OK |
| Shouldly | 4.3.0 | 4.3.0 (291) | OK |

Residue sweep: grep of the spine for `3.2.2`, `10.0.9`, `1.15.1`, `6.0.0-rc`, `2.28.1`, `13.4.0-preview`, `1.16.0` finds only the two legitimate survivors (xUnit `3.2.2`; StackExchangeRedis `1.16.0-beta.1`). No silent drift; no stale literal left behind by the correction.

## New findings

### High / Medium

None.

### Low

None.

### Info

- **I1 — FrontComposer submodule is 78 commits past the released tag.** `references/Hexalith.FrontComposer` HEAD `c585073c` describes as `v4.0.1-78-gc585073c`. Nothing consumes it yet, and AD-21's rule now correctly binds adoption to the catalog variable, so there is no live divergence — but when Story 7.14 turns on consumption, "Debug source and Release package modes resolve the same package boundary at the same version" will require the AD-11 source/package equivalence discipline against a submodule that naturally runs ahead of the released package. Expected mechanics already exist in AD-11; noted so the adoption story checks it explicitly. No spine change.
- **I2 — Two Microsoft.Extensions `10.0.9` stragglers persist in the catalog** (`Microsoft.Extensions.Identity.Http`, `Microsoft.Extensions.Localization`) without the rationale-plus-removal-trigger comment AD-11 asks of compatibility exceptions (the Localization comment states purpose, not exception rationale). This is a Builds-catalog observation, not a spine defect — the spine's ASP.NET/SignalR row is scoped so that it remains accurate. Candidate for a Builds-side tidy-up alongside the next catalog move.
- **I3 — Two pedantic validator-wording parses, neither an enforcement gap.** (a) The strictest reading of "binds their SHA-256 and byte length to the registry-reported index digest and to every descriptor" would demand an index byte-length binding, for which no registry authority exists; the validator instead binds the index by SHA-256 **plus** tag/immutable byte-equality, which is strictly stronger, and binds digest **and** size for every child/config descriptor — the natural distributive reading of the sentence is accurate. (b) "no platform `variant` field": the validator tolerates a present-but-**empty** `variant` (`None` or `""`), consistent with OCI semantics where empty means absent. Neither warrants a text change.
- **I4 — Independent restore-time confirmation of the catalog surface.** The CPM echo in `Admin.UI`'s assets file (five FrontComposer packages at `4.0.1`, no `Contracts.UI`) independently corroborates the props-file reading from NuGet's own restore machinery. Recorded because it upgrades the `Contracts.UI`-has-no-pin claim from single-source to dual-source evidence.

## Sources

- Repository files at review time: `references/Hexalith.Builds/Props/Directory.Packages.props` (@ `ffa1662`), `_bmad-output/planning-artifacts/architecture.md` (working tree), `.github/workflows/release.yml`, `global.json`, `.gitmodules`, `references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py` (identical at pinned `9ec0a032d…` and `ffa1662`), `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts{,.UI}/*.csproj`, `src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj`, tracked-file `git grep FrontComposer`, `src/Hexalith.EventStore.Admin.UI/obj/project.assets.json` (CPM echo, parsed).
- Prior review: `_bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/reviews/review-update-2026-07-19b-technology-currentness.md` (whose web-sourced Claims 1-3 were verified there and are unchanged by tonight's correction; this re-review re-verified every repository-sourced claim independently).
