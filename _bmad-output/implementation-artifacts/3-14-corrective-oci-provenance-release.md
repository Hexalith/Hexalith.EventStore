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
has SHA-256 `926ccfdf9bf3f095211fb37fcdbb8c4f608ad5359cb3636774239992a7751af4`.
It binds the repository, `v3.96.2`, source, workflow run/attempt, exact Builds execution and helper
bytes, one-use authority, all package archives, raw OCI graph/config bytes and labels, and both
smoke results. Validation re-derived every bound claim from retained bytes.

This record and packet select no deployed identity and grant no mutation authority. Story 3.15
must independently decide whether this release can satisfy corrected deployed-runtime parity.

## Canonical Identity

| Field | Retained identity |
| --- | --- |
| Repository | `Hexalith/Hexalith.EventStore` |
| Version / tag | `3.96.2` / `v3.96.2` |
| Source | `f343bb0153e9cdcb8b12ec10153813072f5ad38d` |
| Exact green CI | run `32361196834`, attempt 1, `success` |
| Release workflow | run `32361958618`, attempt 1, number 942, `success` |
| Builds execution | `eadddc7b5d8e9392e5931758ffb608b57b5fdc6c` |
| Authority | `github:jpiquot`, comment `5355025457`, identity SHA-256 `fa2751173bdc88c783525379111beaef8ede858d0662868f9c8313eb17ffda16` |
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
smoke logs/results, frozen publication identity, authority record, consumption record, and all
three preflight observations. Supplemental live observations and the partial-attempt ledger are in
[`observations.json`](evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/observations.json).
The generated [packet checksum manifest](evidence/story-3-14/f343bb0153e9cdcb8b12ec10153813072f5ad38d/packet-sha256.txt)
binds all 41 preceding packet files and itself hashes to
`a84323d3af624d2055c1825d223f8b38005799e949d50c72b765dffb3208276c`.

## Corrected Provenance

Both raw configs retain the exact required values:

- `org.opencontainers.image.source=https://github.com/Hexalith/Hexalith.EventStore`
- `org.opencontainers.image.url=https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.96.2`
- `org.opencontainers.image.documentation=https://github.com/Hexalith/Hexalith.EventStore/blob/f343bb0153e9cdcb8b12ec10153813072f5ad38d/README.md`
- `org.opencontainers.image.revision=f343bb0153e9cdcb8b12ec10153813072f5ad38d`
- `org.opencontainers.image.version=3.96.2`

The evidence codec binds the complete raw config label maps while independently requiring those
five values. This permits normal SDK/base-image labels without weakening or replacing the required
provenance fields. A required-label mutation fails the focused evidence suite.

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
quarantined version nor any earlier authority record.

## Verification

- `python3 tools/validate-corrective-release-evidence.py <release-identity> --manifest tools/release-packages.json --packet-root <packet>` — pass; canonical SHA-256
  `926ccfdf9bf3f095211fb37fcdbb8c4f608ad5359cb3636774239992a7751af4`.
- Contracts Release/package-mode build — pass, zero warnings and zero errors.
- `CorrectiveOciProvenanceReleaseTests` — 7 passed, zero failed, zero skipped; covers every matrix
  row and required-label, raw-byte, OCI-media-type, smoke-cleanup, authority, selection, and partial
  publication mutations.
- Shared Builds publisher suite — 121 passed, zero skipped after the final one-use comment
  representation correction.
- `ContainerPublishingGovernanceTests` — 19 passed, zero skipped.
- Complete Contracts regression suite — 1434 passed, zero failed, zero skipped.
- Manifest pack/validation — exactly 14 valid packages.
- Shell syntax, action lint, and `git diff --check` — pass in both owning repositories.

The live release check additionally resolved `v3.96.2` to the exact source, observed all 14 NuGet
IDs, confirmed the GitHub Release is non-draft/non-prerelease with 14 assets, and revalidated the
public OCI index and both retained smoke outcomes.
