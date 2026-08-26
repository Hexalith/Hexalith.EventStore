# Story 3.15 Corrected Deployed Runtime Parity Closure Proof Packet

## Decision

Current subject: `c22d35b617fdecf06168071faf442621501c016b629a3674800f50489e2bf22f`

Verifier result: `fail closed` with exactly 0 of 3 roster-bound role receipts; no identity is selected.

The technical evidence reproduces the sole candidate
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`,
but the post-hardening acceptance gate is incomplete and selects nothing. This evidence grants no
deployment, publication, registry mutation, consumer removal, or predecessor-change authority.

## Bound technical evidence

- Frozen predecessor identity: `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
- Source/release: `f343bb0153e9cdcb8b12ec10153813072f5ad38d`, `v3.96.2`.
- Packages: 14 NuGet.org archives independently rehashed and mapped to distinct Story 3.14 assets.
- OCI: immutable index, two child manifests, and two configs for `linux/amd64` and `linux/arm64`.
- Runtime: bounded Production `/alive`, HTTP 200, zero redirects, cleanup pass for both children.
- Technical inventory: SHA-256
  `fec6deccc686e4abe83987da16c3935e3e688fa585aa7c5575dead54b4d97611`, 24 files.
- Owner registry: SHA-256
  `aee4f46be8208ea13704a38d9329320b8a7641b0cdd33e61a138114c8c142f2f`.

The current dispatch binds these live files:

| Role | File | SHA-256 | Size |
| --- | --- | --- | ---: |
| Handler | `tools/deployed_runtime_parity_handlers/v1.py` | `f3366d3974ea10d8eb9dc83a3b0bb713229d2457df00f574fd980474bc5aa3e0` | 45160 |
| Verifier | `tools/validate-corrected-deployed-runtime-parity.py` | `58bae1d84a5f22a3b706ef1c1fc6fc958d2c617f4b33dc09c7f32dca55e29b7e` | 12457 |
| Predecessor handler | `tools/release_evidence_handlers/v3.py` | `c186f0506f5b7a4153b8afabff8a597c40147f25b209f56455b46f761d2a8638` | 46106 |
| Predecessor package | `tools/release_evidence_handlers/__init__.py` | `a33b53f823fa36b822395aee2d01597091b37c26248995c2629b0a9e30c70625` | 78 |
| Assembler | `tools/assemble-corrected-deployed-runtime-parity.py` | `dce0806368ae08ed1869b8255af9ec5d9834de305b4080ef193b1c3ef90c0e79` | 11722 |
| Smoke capture | `tools/capture-corrected-deployed-runtime-parity-smokes.py` | `c223905278381ad3391893a95578194eb1a63747fc0e757b842d522ced5ac1f3` | 9647 |

Both dispatchers execute exact verified source bytes under sanitized import resolution. Receipt
files and source instances remain outside the closed 24-file technical inventory to avoid a hash
cycle: the subject binds receipt-source policy, while each receipt binds one exact source by digest
and size.

The arm64 smoke requires the documented host prerequisite
`tonistiigi/binfmt@sha256:400a4873b838d1b89194d982c45e5fb3cda4593fbfd7e08a02e76b03b21166f0`.
The retained smokes were captured on 2026-08-21 by the producer pre-image; binding the current
producer bytes records future re-mint sensitivity and does not claim a recapture.

## Acceptance and authority boundary

The loop-6 re-mint superseded the three `a8cc777e…` receipts. Those immutable receipts and their
sources remain under `evidence/story-3-15/superseded-acceptances/a8cc777e…/`; the README specifies
the mechanical re-rooting rule for each receipt's historical `durable_source.file` value. They
authorize nothing for `c22d35b…`.

Fresh EventStore-owner, Release-owner, and Test Architect receipts require a separately authorized
Ask First collection. No such external write was authorized or performed during this re-mint.

The subject binds four exact limitations: evidence-only scope, no operational authority, the Test
Architect's lack of independent external authentication, and the tooling-composed owner comments.
Both owner roles map to the same authenticated GitHub human. The registry-authority comment's
copy-carried `reviewer-roster.json` name is a known reference to
`registry/owner-role-registry.json`; correcting the external comment was not authorized.

Auditors must verify these false flags directly:

- `deployment_authorized`
- `publication_authorized`
- `grants_mutation_authority`
- `consumer_removal_authorized`

The packet grants no predecessor-change authority either.

## Reproduce

```text
$ python3 tools/assemble-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity-assembly] subject=sha256:c22d35b617fdecf06168071faf442621501c016b629a3674800f50489e2bf22f receipts=0 verifier_exit=1
$ echo $?
1
```

Two consecutive assemblies reproduce the same subject. Direct verification reports the exact
three-receipt requirement and exits 1. This is the expected fail-closed verdict, not an assembly
failure or a PASS-shaped gate.
