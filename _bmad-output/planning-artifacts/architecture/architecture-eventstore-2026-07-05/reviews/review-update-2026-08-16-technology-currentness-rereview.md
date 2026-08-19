---
title: Technology currentness re-review - architecture update 2026-08-16
reviewed_artifact: _bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05/ARCHITECTURE-SPINE.md
review_type: configured-technology-currentness-rereview
date: 2026-08-16
verdict: pass
---

# Technology Currentness Re-review - 2026-08-16 Update

## Verdict

**Pass.** Every prior technology-currentness finding is closed in the revised spine, the release-provenance contract still fits the official OCI and .NET implementation seams, and the retained and live `v3.94.1` evidence still supports the rejected, non-authorizing disposition. No new critical or high currentness issue was found.

## Prior-Finding Closure

### High H1 - SDK/security baseline and six stale Stack rows: closed

AD-11 now distinguishes the tracked SDK seed from the security floor at `ARCHITECTURE-SPINE.md:155`: repository seed `10.0.302` with `rollForward: latestPatch`, required same-band SDK `10.0.303`, and ASP.NET/runtime `10.0.11`. The Stack repeats that distinction at line 484 and aligns ASP.NET/SignalR at line 494.

That is accurate as of this review. Microsoft's live .NET 10 release metadata identifies `10.0.11`, released 2026-08-11, as the current security release and includes SDK `10.0.303` with runtime `10.0.11` in the repository's 10.0.3xx feature band. The local `global.json` still contains `10.0.302` plus `latestPatch`, so the spine no longer confuses a tracked seed with the current security patch floor.

Primary authority: [official .NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json). Local authority: `global.json:2-5`.

The six previously stale rows now exactly match the root-declared Builds catalog:

| Stack location | Revised value | Catalog authority |
| --- | --- | --- |
| line 488 | `CommunityToolkit.Aspire.Hosting.Dapr` `13.4.1-beta.706` | `references/Hexalith.Builds/Props/Directory.Packages.props:136` |
| line 494 | ASP.NET Core / SignalR `10.0.11` | catalog lines 171-192 |
| line 497 | `HexalithFrontComposerVersion` `4.1.1` | catalog line 9 |
| line 499 | StackExchangeRedis instrumentation `1.17.0-beta.1` | catalog line 273 |
| line 500 | `Hexalith.Commons.UniqueIds` `2.30.0` | `HexalithCommonsVersion` at catalog line 6 and package row at line 36 |
| line 503 | NSubstitute `6.2.0` | catalog line 257 |

The catalog is present repository reality, not an unbound checkout: the superproject's `HEAD` gitlink for `references/Hexalith.Builds` is `6b7807533cea31aa7592450742a5c94dd1bc1d9f`, the submodule checkout resolves to that exact SHA, and its worktree is clean. AD-11 identifies this catalog as the sole source-owned NuGet authority at line 155; the Stack correctly describes itself as a dated rendering over which the live catalog wins at line 480. AD-21 contains no independent FrontComposer literal and correctly defers to the shared catalog variable and Stack rendering at line 319.

### Low L1 - absolute HTTPS source label: closed

AD-11 now says at line 159 that `org.opencontainers.image.source` is the exact **absolute public HTTPS** EventStore repository URL. It separately applies the same absolute-public-HTTPS requirement to `url` and `documentation`. This removes the prior wording gap without changing the approved contract.

The meanings remain consistent with the OCI specification: `source` identifies the source-code URL, `url` provides more information about the image, `documentation` identifies documentation, `revision` is the source-control revision, and `version` is the packaged-software version. The spine's exact HTTPS URL, exact 40-character Git SHA, exact SemVer, cross-child equality, and release-identity checks are valid project-level constraints layered on those standard meanings.

Primary authority: [OCI predefined annotation keys](https://github.com/opencontainers/image-spec/blob/main/annotations.md).

### Medium M1 - Story 3.14 publisher/validator enforcement ownership: closed

The final sentence at line 159 is now explicit: Story 3.14 owns adding label emission and raw-config label validation to the EventStore release configuration and the SHA-pinned shared Builds publisher/validator; no corrective release conforms until that work is proved. This is the required ownership boundary.

Repository reality confirms that the sentence describes future implementation rather than falsely claiming present enforcement. `Directory.Build.targets:23-25` explicitly configures only `source`, `licenses`, and `vendor`, while `references/Hexalith.Builds/Github/publish-containers/oci_registry_validator.py` currently verifies OCI media types, digests, byte sizes, platform descriptors, child manifests, config descriptors, and config platform fields but does not inspect provenance labels. The gap is therefore safely fail-closed and assigned to Story 3.14 rather than hidden or treated as already implemented.

.NET's supported seam fits that ownership: `ContainerLabel` accepts arbitrary key/value metadata, and multiple `ContainerRuntimeIdentifiers` produce a multi-architecture image. Primary authority: [.NET SDK container publishing configuration](https://learn.microsoft.com/en-us/dotnet/core/containers/publish-configuration).

## OCI Semantics Re-check

AD-11 still uses the official media types correctly:

- release tag: `application/vnd.oci.image.index.v1+json`;
- direct platform child: `application/vnd.oci.image.manifest.v1+json`;
- child config: `application/vnd.oci.image.config.v1+json`.

The OCI Image Index specification defines an index as the higher-level manifest pointing to platform-specific image manifests and permits nested indexes. EventStore's exactly-two direct manifests, `linux/amd64` and `linux/arm64`, no extra/unknown platforms, and no non-empty variant rule is a valid stricter release profile. OCI Distribution requires successful manifest responses to bind digest and byte length, matching AD-11's raw-byte validation contract.

Primary authorities: [OCI media types](https://github.com/opencontainers/image-spec/blob/main/media-types.md), [OCI image-index specification](https://github.com/opencontainers/image-spec/blob/main/image-index.md), [OCI manifest specification](https://github.com/opencontainers/image-spec/blob/main/manifest.md), and [OCI Distribution Specification](https://github.com/opencontainers/distribution-spec/blob/main/spec.md).

## `v3.94.1` Evidence Re-check

The evidence remains internally and externally consistent:

- Running `sha256sum -c evidence-core-sha256.txt` in the retained evidence root passed every listed object, including both child configs/manifests, registry responses, provenance, authority, package archives, and smoke artifacts.
- The retained index is 493 bytes with digest `sha256:ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd`, media type `application/vnd.oci.image.index.v1+json`, and exactly the two expected direct platform manifests.
- A fresh public registry GET on 2026-08-16 returned HTTP 200, the same OCI-index content type, `Content-Length: 493`, the same `Docker-Content-Digest`, and byte-for-byte hashes matching the retained tag/index data.
- Fresh reads of config digests `sha256:d8222e67...` (`amd64`, 3851 bytes) and `sha256:c5c4a51f...` (`arm64`, 3852 bytes) hash to their retained identities. Both still contain version `3.94.1`, malformed `source`/`url`/`documentation` values `"https"`, and no `revision`.
- `deployment-authority.json` still records `deployment_authorized: false` and no authorized index digest. The retained crosswalk still reports failed deployment authority and missing content-bound acceptances.
- The public GitHub release still binds `v3.94.1` to source commit `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`, and release run `31781920404` attempt 1 is public and successful for that commit. A successful publication run does not override the failed provenance gate.

Primary/public authorities: [GitHub release v3.94.1](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.94.1), [exact source commit](https://github.com/Hexalith/Hexalith.EventStore/commit/80d12ef5eee71a9fe3ea7be51171da4a71b69a28), [release workflow run](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/31781920404/attempts/1), and [live registry tag](https://registry.hexalith.com/v2/eventstore/manifests/3.94.1). Retained local evidence root: `_bmad-output/implementation-artifacts/evidence/story-3-13/80d12ef5eee71a9fe3ea7be51171da4a71b69a28/ab8784c8c9c67229ee178e9d6dd809df9554b3cdafb43ffb7bfd38c792e2afcd/`.

The revised release-provenance contract therefore remains a good fit: content-addressed OCI bytes establish artifact identity; validated child-config labels bind source and version; workflow, Builds, package, authority, and smoke digests complete the lineage; Story 3.14 produces the enforcing release path; and Story 3.15 independently decides positive parity. The immutable `v3.94.1` failure remains evidence, never deployment authority.

## New Critical/High Sweep

No new critical or high issue was introduced by the corrections. The other named Stack values remain consistent with the current Builds catalog or with their existing explicit compatibility/channel constraints; no unrelated low-value drift was reopened.

## Gate Result

- **Critical:** 0
- **High:** 0
- **Verdict:** pass; the prior currentness gate is closed.
