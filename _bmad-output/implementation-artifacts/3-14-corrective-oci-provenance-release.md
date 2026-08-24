# Story 3.14 Corrective OCI Provenance Release

## Outcome

Story 3.14 produced corrective release `v3.96.2` from exact source
`f343bb0153e9cdcb8b12ec10153813072f5ad38d`. Release run
[`32361958618` attempt 1](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/32361958618)
completed successfully after exact-source CI run
[`32361196834`](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/32361196834)
passed. The stable GitHub Release contains exactly the 14 packages declared by
`tools/release-packages.json`; all 14 are visible on NuGet.org. The public
`registry.hexalith.com/eventstore:3.96.2` tag resolves to an OCI image index containing exactly
`linux/amd64` and `linux/arm64`, and both immutable child digests passed the same bounded `/alive`
smoke under the helper's declared `Development` environment.

The canonical [release identity](evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/release-identity.json)
has SHA-256 `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
It binds the repository, `v3.96.2`, source, workflow run/attempt, exact Builds execution and helper
bytes, one-use authority and receipt, all package archives, raw OCI graph/config bytes and labels,
both raw smoke logs and results, and the cycle-free packet inventory. Validation re-derived every
bound claim from retained bytes.

The identity document has been regenerated twice since publication; the published release itself —
tag, packages, OCI bytes and smokes — is unchanged by either regeneration, and neither affects the
one-use authority, which binds the separate publication identity `fa275117…`. The digest history is:

| Canonical digest | Commit | Change |
| --- | --- | --- |
| `926ccfdf9bf3f095211fb37fcdbb8c4f608ad5359cb3636774239992a7751af4` | `a55b5bef` | Codec version 1, as first retained. |
| `92b7479bfac6f61c755a0cb3023ea2db08f4115eb8119ca08ba84765630fdb7b` | `1e5abd26` | Codec version 2: helper file bindings, `packet_manifest`, authority record/snapshot/role bindings, smoke logs. |
| `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9` | code review 2026-08-21 | Codec version 3: the codec and verifier are now retained inside the packet under `successful/tools/` and validation binds those retained bytes instead of the live `tools/` files, so a later fix to the codec can no longer invalidate this packet. The same pass removed the two uncalled helpers `select_absent_version` and `publication_disposition` from the codec, and amended the two matrix rows they were the only implementation of. |

This record and packet select no deployed identity and grant no mutation authority. Story 3.15
must independently decide whether this release can satisfy corrected deployed-runtime parity.

Post-release review hardening initially rotated the EventStore release caller to Builds commit
`63409393541f1437e23006b7a4e05174f8b50da7`, but that revision was not yet published to the
Hexalith.Builds remote when the pin was set, so a Release dispatch could not then have resolved the
reusable workflow. The revision later reached remote `main`; the caller now pins
`a07078ad74d3727bc5a6b6d85d47d56a6e5c9fec`, which superseded `63409393…`, is reachable on
Hexalith.Builds `main`, and is asserted by `ApprovedBuildsReleaseSha` in
`ContainerPublishingGovernanceTests.cs`. This does not rewrite the historical release execution:
the packet correctly remains bound to executed Builds SHA `eadddc7b5d8e9392e5931758ffb608b57b5fdc6c`,
while the ordinary development gitlink is pinned independently of the release caller. The gitlink has
moved several times since this record was first written (`145ab857` at `4038cf33`, `eadddc7b` at
`a55b5bef`, back to `145ab857` at `1e5abd26`, and `eadddc7b` again at `c8902353`); read it from
`git ls-tree HEAD references/Hexalith.Builds` rather than from this paragraph.

## Canonical Identity

| Field | Retained identity |
| --- | --- |
| Repository | `Hexalith/Hexalith.EventStore` |
| Version / tag | `3.96.2` / `v3.96.2` |
| Source | `f343bb0153e9cdcb8b12ec10153813072f5ad38d` |
| Exact green CI | run `32361196834`, attempt 1, `success` |
| Release workflow | run `32361958618`, attempt 1, number 942, `success` |
| Builds execution | `eadddc7b5d8e9392e5931758ffb608b57b5fdc6c` |
| Builds helpers | Five exact files retained under `successful/builds/eadddc7…`; all hashes recomputed |
| Authority | `github:jpiquot`, comment `5355025457`, raw record SHA-256 `d97629bb…`, identity SHA-256 `fa275117…` |
| Authority proof | Full issue 346 comment snapshot plus repository permission `admin` |
| One-use receipt | comment `5355070052`, consumed once |
| GitHub Release | stable release `373676516`, 14 package assets, published `2026-08-20T11:14:41Z` |
| NuGet | all 14 manifest IDs visible at `3.96.2` |
| OCI index | `sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3` |
| OCI children | `linux/amd64` `sha256:4d42f969…`; `linux/arm64` `sha256:ede85331…` |
| Smokes | both pass; digest-pinned; `Development`; `/alive`; 180 seconds; cleanup pass |
| Handoff | `selects_deployed_identity: false`; `grants_mutation_authority: false` |

The packet retains the 14 original `.nupkg` release assets under
[`successful/packages/`](evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/successful/packages/),
and the complete generated workflow artifact under
[`successful/run-artifact/`](evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/successful/run-artifact/).
The artifact includes the raw index, both child manifests, both raw configs, validation summary,
smoke logs/results, frozen publication identity, authority summary, and exact consumption record.
The packet additionally retains the exact executed Builds helper bytes, the raw GitHub authority
comment, full relevant issue-comment snapshot, and repository role proof. Supplemental live
observations and the partial-attempt ledger are in
[`observations.json`](evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/observations.json).
The generated [packet checksum manifest](evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/packet-sha256.txt)
binds all 64 retained files other than the canonical identity and itself. The identity binds the
manifest's exact bytes, size, and SHA-256
`0736d3ac05c21b560d9e9c204603e39c5750fa8a28271f1d0411e3e9a051b730`, avoiding a checksum cycle.

The retained raw authority comment is the exact Actions-token `CONTRIBUTOR` representation whose
publisher-canonical bytes reproduce the original `d97629bb…` record digest. The full issue snapshot
shows the same immutable comment body/IDs/timestamps with the public `MEMBER` association, while
the retained collaborator endpoint independently proves repository permission `admin`.

## Corrected Provenance

Both raw configs retain the exact required values:

- `org.opencontainers.image.source=https://github.com/Hexalith/Hexalith.EventStore`
- `org.opencontainers.image.url=https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.96.2`
- `org.opencontainers.image.documentation=https://github.com/Hexalith/Hexalith.EventStore/blob/f343bb0153e9cdcb8b12ec10153813072f5ad38d/README.md`
- `org.opencontainers.image.revision=f343bb0153e9cdcb8b12ec10153813072f5ad38d`
- `org.opencontainers.image.version=3.96.2`

The evidence codec binds the complete raw config label maps while independently requiring those
five values. This permits normal SDK/base-image labels without weakening or replacing the required
provenance fields.

**Known defect in this release.** The five values above are exact, but v3.96.2 shipped with
`org.opencontainers.image.created` and `org.opencontainers.artifact.created` truncated to
`2026-08-20T11` by the same SDK `String.Split(':')[1]` reconstruction — those two labels were not
among the five rebound by `Directory.Build.targets`, and neither the codec nor the archive test
inspected any label outside the five. The published image is otherwise correct and its identity,
source, revision and version labels are unaffected; the malformed values are timestamps only.
Post-release hardening now rebinds both created labels and makes a direct multi-RID archive test
pass one explicit publisher-shaped instant while asserting the complete label surface rather than a
five-key allowlist. The current Hexalith.Builds development source generates one publisher-owned RFC
3339 instant and forwards it to every container publish. EventStore's immutable `a07078ad…` release
pin predates that publisher change and must be rotated only after the Builds change receives its own
reviewed commit. Reissuing v3.96.2 was deliberately not attempted: the labels are
not contract-bound by the Story 3.14 matrix, and a re-release would consume a further version and a
further one-use authority.

## Quarantined Attempts

Every failed attempt remains immutable, non-authorizing history. No tag, package, authority
comment, receipt, or artifact was deleted, rewritten, or reused.

| Run | Version | Writes before failure | Failure and disposition |
| --- | --- | --- | --- |
| `32347773728/1` (#938) | none | none | Reusable-workflow permission ceiling caused startup failure before any job or authority. |
| `32350537607/1` (#939) | `3.96.0` | none | Authority role check saw the private member as `CONTRIBUTOR`; the collaborator-permission proof was added before retry. |
| `32354815109/1` (#940) | `3.96.0` | tag `v3.96.0` | Publish preflight rejected the exact source tag created earlier in the same run. The tag and [artifact](evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/quarantine/run-32354815109-attempt-1/) are quarantined; the version is never reused. |
| `32358676358/1` (#941) | `3.96.1` | tag, all 14 NuGet packages, receipt | Container phase detected a POST-vs-list comment representation difference as replay. The tag, packages, receipt, and [artifact](evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/quarantine/run-32358676358-attempt-1/) are quarantined; no OCI image or GitHub Release was produced. |

The partial attempts forced fresh versions and fresh one-use authority. The final run used neither
quarantined version nor any earlier authority record. The packet retains the exact public NuGet
3.96.1 bytes for all 14 quarantined packages under that attempt's `packages/` directory.

## Verification

- `python3 tools/validate-corrective-release-evidence.py <release-identity> --manifest tools/release-packages.json --packet-root <packet>` — pass; canonical SHA-256
  `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
- Contracts Release/package-mode build — pass, zero warnings and zero errors.
- Focused corrective-release and container-governance suite — 70 passed, zero failed, zero skipped
  (30 corrective-release cases and 40 container-governance cases);
  includes the checked-in packet's exact final digest and manifest, package-origin, workflow,
  checksum, raw-smoke, authority-window/edit, receipt-schema, reservation, and provenance mutations.
- Shared Builds publication preflight and publisher-contract counts recorded for the release execution
  SHA `eadddc7b…` were 54 passed and 123 passed for the full harness, both with zero skipped. The
  independently pinned `a07078ad…` fixture runs two named authority cases directly from that exact
  archived commit and requires both to pass without skips.
- Complete Contracts regression suite — 1439 passed, zero failed, zero skipped.
- Manifest pack/validation — exactly 14 valid packages.
- Shell syntax, action lint, and `git diff --check` — pass in both owning repositories.

The live release check additionally resolved `v3.96.2` to the exact source, observed all 14 NuGet
IDs, confirmed the GitHub Release is non-draft/non-prerelease with 14 assets, and revalidated the
public OCI index and both retained smoke outcomes.
