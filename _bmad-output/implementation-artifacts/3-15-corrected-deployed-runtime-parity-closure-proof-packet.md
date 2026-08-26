# Story 3.15 Corrected Deployed Runtime Parity Closure Proof Packet

## Decision

Current subject: `58f025f354de40fd5eee973a487417b3da45636032a5d1675c9c8c886005e2c6`

Verifier result: `pass` with exactly 3 of 3 roster-bound role receipts.

The verifier selects the sole candidate
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`,
while granting no deployment, publication, registry mutation, consumer removal, or
predecessor-change authority.

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
| Handler | `tools/deployed_runtime_parity_handlers/v1.py` | `bf8fec6bbd408be0b16f3b64ff219ed8b021e7f6c04aea7f59e819ad51298fef` | 46023 |
| Verifier | `tools/validate-corrected-deployed-runtime-parity.py` | `8b6ef79cdc5eb5ee6cf995880709f582c3fb09cba00a39eff2c043f5e055f32c` | 13829 |
| Predecessor handler | `tools/release_evidence_handlers/v3.py` | `a421791b4c6176afc8120e4e5c4668cb9703976e6f74659c0525119fc5aca5f4` | 46636 |
| Predecessor package | `tools/release_evidence_handlers/__init__.py` | `a33b53f823fa36b822395aee2d01597091b37c26248995c2629b0a9e30c70625` | 78 |
| Assembler | `tools/assemble-corrected-deployed-runtime-parity.py` | `dce0806368ae08ed1869b8255af9ec5d9834de305b4080ef193b1c3ef90c0e79` | 11722 |
| Smoke capture | `tools/capture-corrected-deployed-runtime-parity-smokes.py` | `83ee91db86acc3678edd7d5e900ea9dd2c835c413c7bc7a65b082dd274d37538` | 10390 |

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

Subsequent explicit authorization produced EventStore-owner comment `5424336008`, Release-owner
comment `5424339580`, and the `bmad:murat` Test Architect record for subject `c22d35b…`. Step-04
bound-code hardening then re-minted the current subject. The complete c22 receipt/source tree is
retained byte-for-byte under `superseded-acceptances/c22d35b…/`; it authorizes nothing for
`58f025f…`. Renewed authorization produced EventStore-owner comment `5425294818` at
`2026-08-26T12:28:20Z`, Release-owner comment `5425297492` at `2026-08-26T12:28:33Z`, and the
`bmad:murat` Test Architect record at `2026-08-26T12:29:42Z`, all retained beneath the unchanged
current subject. Malformed attempt `5425285803` is visibly superseded and is not retained.

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

## Verification results

- Contracts Release build: pass, zero warnings and errors.
- Closure 141/141, smoke capture 13/13, predecessor/provenance 46/46; zero skips.
- Contracts excluding the unrelated OQ8 evidence class: 1448/1448. The full 1763-test invocation
  retains the known OQ8-only failure cascade on its pre-existing bound-source drift in
  `DaprTestContainerFixture.cs`; this story does not re-seal another story's evidence.
- Direct Story 3.14 validation passes; direct Story 3.15 validation passes at exact 3/3.

## Reproduce

```text
$ python3 tools/assemble-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity-assembly] subject=sha256:58f025f354de40fd5eee973a487417b3da45636032a5d1675c9c8c886005e2c6 receipts=3 verifier_exit=0
$ echo $?
0
```

Two consecutive assemblies reproduce the same subject. Direct verification passes with all three
roster-bound receipts and selects only the bound index.
