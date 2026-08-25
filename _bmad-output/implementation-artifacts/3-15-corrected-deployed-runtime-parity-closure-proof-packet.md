# Story 3.15 Corrected Deployed Runtime Parity Closure Proof Packet

## Decision

The technical evidence passes and reproduces, and the acceptance gate is complete. Current subject
`sha256:a8cc777ed04f1f0a7f7dffb7f24f7359f786e9114afe04fc69b1aa90cb8fdf7f`
has **three of three roster-bound role receipts**, so the retained verifier reports deployed-runtime
parity available.

The only candidate selected identity remains
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.
This packet grants no deployment, publication, registry mutation, consumer removal, or predecessor
change authority.

## Bound technical evidence

- Frozen predecessor identity: `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
- Source/release: `f343bb0153e9cdcb8b12ec10153813072f5ad38d`, `v3.96.2`.
- Packages: 14 NuGet.org archives independently rehashed and cross-mapped to the Story 3.14 assets.
- OCI: independent raw index, two child manifests, and two configs for `linux/amd64` and
  `linux/arm64`.
- Runtime: bounded Production `/alive`, exact HTTP 200, zero redirects, cleanup pass for both
  immutable children.
- Owner registry: SHA-256
  `aee4f46be8208ea13704a38d9329320b8a7641b0cdd33e61a138114c8c142f2f`.

The current dispatch binds these verdict-bearing live files:

| Role | File | SHA-256 |
| --- | --- | --- |
| Handler | `tools/deployed_runtime_parity_handlers/v1.py` | `886fc7141063185f21f2658afc347cf9428b5c781b97fc81a07d612d9961ca41` |
| Verifier | `tools/validate-corrected-deployed-runtime-parity.py` | `00365d223eaf7c76a9a2fecd9ad7ec49288ff3395b6fc4e075da54c549b2097f` |
| Predecessor handler | `tools/release_evidence_handlers/v3.py` | `410952dcb855e52278ef2575ce6d53bc2df36c0d7a936eca464cba0e4cdf41b4` |
| Predecessor package | `tools/release_evidence_handlers/__init__.py` | `a33b53f823fa36b822395aee2d01597091b37c26248995c2629b0a9e30c70625` |

Both dispatchers execute only the exact verified initializer and handler source bytes under
sanitized import resolution. Repository-local standard-library/dependency shadows and stale or
preloaded trusted module names cannot participate in the verdict.

## Superseded acceptances

The three receipts for subject `dab64f5f...` and their exact retained sources were moved
byte-for-byte outside the packet to
`evidence/story-3-15/superseded-acceptances/dab64f5f.../`. They authorize nothing for the current
subject. The earlier `bb58d691...` set remains in the same audit area.

For both complete historical sets, the two owner roles map to one authenticated GitHub human,
`github:jpiquot`, while the Test Architect role is the explicitly limited, self-attested
`bmad:murat` record. This is why the operator-facing claim is “three roster-bound role receipts,”
not three independently authenticated people.

The subject binds receipt-source **policy**. An individual post-subject source replacement does not
re-mint the subject (avoiding a hash cycle), but it invalidates that source's bound receipt and
therefore any complete 3/3 verdict.

## Current acceptances

Dedicated Story 3.15 issue [#352](https://github.com/Hexalith/Hexalith.EventStore/issues/352)
retains the two owner-role acceptances for the exact current subject:

- EventStore owner: comment
  [5409145568](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5409145568),
  created at `2026-08-25T10:33:29Z`.
- Release owner: comment
  [5409148235](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5409148235),
  created at `2026-08-25T10:33:45Z`.
- Test Architect: self-attested `bmad:murat` record accepted at `2026-08-25T10:34:41Z`.

Both owner roles map to the same authenticated GitHub human, `github:jpiquot`; the Test Architect
record has no independent external authentication. The packet therefore claims three roster-bound
role receipts, not three independently authenticated people. Timestamp-mismatched owner comments
`5409140199` and `5409147909` were visibly marked superseded and are not retained as sources.

## Reproduce

```text
$ python3 tools/validate-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json \
    --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity] pass: subject=sha256:a8cc777ed04f1f0a7f7dffb7f24f7359f786e9114afe04fc69b1aa90cb8fdf7f selected=sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3
$ echo $?
0
```

Reassembly deterministically reproduces subject `a8cc777e...`, reports
`receipts=3 verifier_exit=0`, and exits 0. It does not copy or rewrite any superseded receipt.
