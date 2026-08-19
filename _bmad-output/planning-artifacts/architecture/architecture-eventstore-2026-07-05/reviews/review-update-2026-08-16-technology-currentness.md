---
title: Technology currentness review - architecture update 2026-08-16
reviewed_artifact: _bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/ARCHITECTURE-SPINE.md
review_type: configured-technology-currentness
date: 2026-08-16
verdict: changes-required
---

# Technology Currentness Review - 2026-08-16 Update

## Verdict

**Changes required.** The August 16 OCI provenance and release-disposition decisions are technically sound, match the official OCI/.NET contracts, and accurately classify the retained `v3.94.1` evidence. The spine is not safe to hand off as a current build substrate, however, because AD-11 and six Stack rows contradict the repository's present authoritative Builds catalog, including the August 11 .NET 10 security baseline.

## Findings

### High - H1: AD-11 and the Stack no longer match the authoritative catalog or current .NET security release

**Disposition: autofix the spine before closure.**

The spine calls its Stack table the "current planning baseline" and says the Builds catalog is the sole version authority, but the root-declared `references/Hexalith.Builds` checkout is clean at parent gitlink `6b7807533cea31aa7592450742a5c94dd1bc1d9f`. Its `Props/Directory.Packages.props` now selects:

| Spine | Repository authority | Required correction |
| --- | --- | --- |
| ASP.NET Core / SignalR `10.0.10` | `10.0.11` | Update AD-11 and Stack to `10.0.11`. |
| `CommunityToolkit.Aspire.Hosting.Dapr` `13.4.1-beta.687` | `13.4.1-beta.706` | Refresh Stack. |
| `Hexalith.Commons.UniqueIds` `2.28.2` | shared `HexalithCommonsVersion` `2.30.0` | Refresh Stack. |
| FrontComposer `4.0.1` | shared `HexalithFrontComposerVersion` `4.1.1` | Refresh Stack and AD-21's current-value implication. |
| Redis OpenTelemetry instrumentation `1.16.0-beta.1` | `1.17.0-beta.1` | Refresh Stack. |
| NSubstitute `6.0.0` | `6.2.0` | Refresh Stack. |

This is more than low-value package drift. Microsoft's live .NET 10 release metadata identifies `10.0.11` (2026-08-11) as the latest security release and lists SDK `10.0.303` for the repository's 10.0.3xx feature band. The retained `v3.94.1` configs themselves were built with SDK `10.0.303` and contain ASP.NET/runtime `10.0.11`. `global.json` may remain a repository seed at `10.0.302` with `rollForward: latestPatch`, but the spine must not call `10.0.302` / ASP.NET `10.0.10` the verified security baseline. Record the current same-band security floor as SDK `10.0.303` and ASP.NET/runtime `10.0.11`, while distinguishing it from the still-tracked seed if the seed is intentionally not changed in this planning-only update.

Primary source: [official .NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) (`latest-release: 10.0.11`, `security: true`, and SDK `10.0.303`). Repository evidence: `references/Hexalith.Builds/Props/Directory.Packages.props:6-10,113-120,136,171-192,257,264-273,292,316-318`; `global.json:2-5`; retained `child-linux-amd64.config.raw` and `child-linux-arm64.config.raw`.

### Medium - M1: The provenance contract fits, but current publishing code cannot yet enforce it

**Disposition: keep as an explicit Story 3.14 implementation prerequisite; do not weaken AD-11.**

AD-11's five-field child-config contract is a valid, useful strengthening of the OCI standard:

- `source` is officially the URL from which image source can be obtained.
- `url` is the URL for more information about the image.
- `documentation` is the image documentation URL.
- `revision` is the packaged software's source-control revision.
- `version` is the packaged-software version and may be SemVer-compatible.

Requiring public HTTPS URLs, an exact 40-character Git SHA, exact SemVer, cross-child equality, and package/workflow-lineage equality is project policy layered on those meanings; it does not conflict with OCI. .NET SDK container tooling officially supports arbitrary `ContainerLabel` key/value items, treats labels as generated-image-independent metadata, and creates an OCI image index for multi-RID publication, so the chosen implementation seam is appropriate.

Current repository reality does not yet meet the target, as expected by Story 3.14:

- `Directory.Build.targets:23-25` declares only `source`, `licenses`, and `vendor`; it does not explicitly supply `url`, `documentation`, `revision`, or the release-bound `version` contract.
- The shared validator at `references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py` verifies media types, raw digests, sizes, platforms, and child configs, but never reads or validates provenance labels. The exact `v3.94.1` release-pinned validator at Builds `f75daebd4c522c081a6f62e274cf25e07971de69` has the same gap.
- A local MSBuild evaluation sees the complete repository URL in the configured `source` item, yet the immutable multi-RID `v3.94.1` child configs contain only `"https"`. Story 3.14 therefore correctly requires an emitted-image regression test and owner diagnosis across EventStore configuration, shared publisher argument handling, and .NET SDK container metadata; property evaluation alone is not proof.

No architecture change is required beyond preserving that prerequisite. The release gate must be implemented in the shared publisher/validator and demonstrated against raw emitted configs before any corrective publication.

Primary sources: [OCI predefined annotation meanings](https://github.com/opencontainers/image-spec/blob/main/annotations.md), [.NET SDK container labels and multi-RID publishing](https://learn.microsoft.com/en-us/dotnet/core/containers/publish-configuration).

### Low - L1: The source field should retain the approved HTTPS precision

**Disposition: autofix during the same polish pass.**

The approved proposal requires `source`, `url`, and `documentation` to be absolute public HTTPS URIs. AD-11 currently says `source` is the exact public repository URL while applying the explicit absolute-public-HTTPS wording only to `url` and `documentation`. The actual repository URL is HTTPS, so this is not a present divergence, but exact wording would prevent a later non-HTTPS repository spelling from passing a literal implementation. State that all three are absolute public HTTPS URLs, with `source` additionally equal to the exact EventStore repository URL.

## Verification Of The August 16 Decisions

### OCI graph and media types - pass

The official OCI Image Specification defines `application/vnd.oci.image.index.v1+json`, `application/vnd.oci.image.manifest.v1+json`, and `application/vnd.oci.image.config.v1+json` exactly as used by AD-11. An image index is the higher-level object pointing to platform-specific manifests. OCI permits more shapes, nested indexes, and optional variants; EventStore's exactly-two-platform, direct-child, no-variant rule is a valid stricter release profile rather than an inaccurate statement about OCI generally. The rule correctly keeps index annotations and unrelated repository co-objects outside the evidence identity because the approved identity is the validated content-addressed descriptor graph.

Primary sources: [OCI media types](https://github.com/opencontainers/image-spec/blob/main/media-types.md), [OCI image-index specification](https://github.com/opencontainers/image-spec/blob/main/image-index.md), [OCI manifest specification](https://github.com/opencontainers/image-spec/blob/main/manifest.md), [OCI distribution digest/byte semantics](https://github.com/opencontainers/distribution-spec/blob/main/spec.md).

### `v3.94.1` immutable evidence facts - pass

The retained packet is internally and externally consistent:

- `sha256sum -c evidence-core-sha256.txt` passed for every listed object, including both configs/manifests, index/tag bytes, registry readback, release provenance, 14 package archives, runtime logs, and authority record.
- Local Git resolves annotated tag `v3.94.1` to exact commit `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`. The public GitHub release identifies the same commit and was published on 2026-08-14; workflow run `31781920404` attempt 1 is public, successful, and bound to that commit.
- Live registry readback on 2026-08-16 still returns OCI-index media type, length `493`, and digest `sha256:ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd`; hashing the returned raw bytes produces the same digest.
- Live immutable config reads match the retained bytes: `linux/amd64` config `sha256:d8222e67...` and `linux/arm64` config `sha256:c5c4a51f...` both carry version `3.94.1`, malformed `source`/`url`/`documentation` values `"https"`, and no `revision`.
- The retained authority record explicitly has `deployment_authorized: false`; the evidence subject has zero of three required acceptances. Thus `rejected-non-authorizing`, no selected deployed identity, and no deployment/consumer authority are the only evidence-supported disposition.

Primary/public sources: [GitHub release v3.94.1](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.94.1), [exact source commit](https://github.com/Hexalith/Hexalith.EventStore/commit/80d12ef5eee71a9fe3ea7be51171da4a71b69a28), [release workflow run](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/31781920404/attempts/1), [live registry tag](https://registry.hexalith.com/v2/eventstore/manifests/3.94.1). Retained evidence root: `_bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd/`.

### Release-provenance and Story 3.13/3.14/3.15 split - pass

The contract fits the failure mode and preserves authority correctly. Content-addressed index/child/config digests establish immutable byte identity; the five validated config labels bind those bytes to source and version; package hashes and workflow provenance complete the release lineage; Production smoke proves runnability rather than identity; unchanged-subject human acceptances authorize only the parity conclusion. AD-22 correctly refuses to infer positive parity, deployment, or consumer removal from a successful workflow, a mutable tag, a smoke pass, or completion of a negative-evidence story.

The live `v3.95.0` tag already exists and still exposes the same malformed three URL labels with no revision. This does not weaken the spine: its generic AD-11 gate rejects that release too, and AD-22 selects no positive identity until Story 3.14 produces a separately authorized conforming release and Story 3.15 independently validates it. Story 3.14 must therefore choose a genuinely later semantic version; `v3.95.0` must not be inferred to be the corrective release merely because it is later than `v3.94.1`.

## Currentness Sweep Scope

All other named Stack rows were compared with the current Builds catalog. Aspire, Dapr SDK, MediatR, FluentValidation, Roslyn, Fluent UI, core OpenTelemetry, xUnit, and Shouldly still match. No unrelated low-value web-only package drift was opened. DAPR runtime/OpenBao and platform choices were not re-litigated because this update does not change them and the repository still carries their previously verified compatibility constraints.

## Gate Result

- **Critical:** 0
- **High:** 1
- **Medium:** 1
- **Low:** 1
- **Required before handoff:** refresh the catalog/security version claims in AD-11, AD-21, and Stack; retain Story 3.14 as the mandatory publisher/validator implementation gate.
