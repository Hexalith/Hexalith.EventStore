# Story 3.15 Corrected Deployed Runtime Parity Closure Proof Packet

## Decision

**Deployed-runtime parity is available.** Current subject
`sha256:86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274`
has **three of three roster-bound role receipts** (3 of 3), so the retained verifier **passes**,
exit 0, and selects only
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.

This packet still grants no deployment, publication, registry mutation, consumer removal, or
predecessor change authority. The positive parity verdict is evidence that the deployed runtime
matches the corrective release, not permission to act on it.

## Authority boundary

An auditor must confirm four flags in `closure.json`, and all four are `false`:

| Flag | Value |
| --- | --- |
| `deployment_authorized` | `false` |
| `publication_authorized` | `false` |
| `consumer_removal_authorized` | `false` |
| `grants_mutation_authority` | `false` |
| `deployed_runtime_parity` | `available` -- claim granted at 3 of 3 |
| `selected_deployed_identity` | the index digest -- claim granted at 3 of 3 |

**`deployed_runtime_parity` and `selected_deployed_identity` remain the claim fields.** With three
packet-bound receipts they are granted by exit 0; an auditor must still read them together with the
receipt count and the four non-authority flags, never alone. Receipts live under
`acceptances/86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274/`.

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
- Closed technical inventory: SHA-256
  `fec6deccc686e4abe83987da16c3935e3e688fa585aa7c5575dead54b4d97611` over exactly **24** retained
  files. Anything else under the packet root is rejected.

The current dispatch binds these verdict-bearing live files:

| Role | File | SHA-256 |
| --- | --- | --- |
| Handler | `tools/deployed_runtime_parity_handlers/v1.py` | `405dd1ac8c8872d9ced666c7420019462de0779386d804f227912ca5d749c3d5` |
| Verifier | `tools/validate-corrected-deployed-runtime-parity.py` | `10b41a5677a2a31b035dad515f676587531dd9cf2de90f1404ed95724a1bad08` |
| Predecessor handler | `tools/release_evidence_handlers/v3.py` | `f212c784bb0b4b006d683f25248c40a14edf19198cbfaee61f520e07b3bb03d2` |
| Predecessor package | `tools/release_evidence_handlers/__init__.py` | `a33b53f823fa36b822395aee2d01597091b37c26248995c2629b0a9e30c70625` |
| Smoke capture producer | `tools/capture-corrected-deployed-runtime-parity-smokes.py` | `cd627d7ab35604ce3a7e07876a2a62342316e167cd1f9220a5b4091dc7904600` |
| Packet assembler | `tools/assemble-corrected-deployed-runtime-parity.py` | `c0968ac9e0c26cbedcfaae479802634e97ef80a99c203fc40021628235e5b961` |

The two producers are bound even though the verifier never executes them. Until they were bound, the
capture tool could change what a passing Production smoke means -- as it did, from any 2xx to
exactly 200 -- and the assembler could change how the packet is derived, with every receipt staying
valid.

Both dispatchers execute only the exact verified initializer and handler source bytes under
sanitized import resolution. Repository-local standard-library/dependency shadows and stale or
preloaded trusted module names cannot participate in the verdict.

## Why the receipts sit outside the technical inventory

The canonical subject hashes the technical evidence, the decision, the registry and the producer and
verifier digests. If receipts were inside the technical inventory the subject would hash bytes that
themselves cite the subject, a cycle no assembler could resolve. Receipts therefore live under
`acceptances/<subject-sha256>/`, addressed *by* the subject, close-listed by the receipt check, while
the inventory sweep rejects any acceptance tree that is not the bound one. The chain reads: pinned
verifier bytes -> canonical subject -> receipts addressed by that subject.

The subject binds receipt-source **policy**. An individual post-subject source replacement does not
re-mint the subject (avoiding that hash cycle), but it invalidates that source's bound receipt and
therefore any complete 3/3 verdict.

## Superseded acceptances

Three acceptance sets are retained byte-for-byte outside the packet under
`evidence/story-3-15/superseded-acceptances/`: `bb58d691...`, `dab64f5f...`, and the three receipts
collected against `a8cc777e...` that the loop-6 batch re-mint rejected. There are three sets against
eight subjects because receipts were only ever collected for three of them; the other five subjects
happened before any receipt existed, so their absence is expected rather than a gap. They authorize nothing for
the current subject. That directory's README carries the re-rooting rule an auditor needs, because
each retained receipt still declares its source under the `acceptances/<subject>/sources/` path it
bound while it was live.

For every historical set the two owner roles map to one authenticated GitHub human,
`github:jpiquot`, while the Test Architect role is the explicitly limited, self-attested
`bmad:murat` record, and every receipt was composed by repository tooling rather than typed by hand.
This is why the operator-facing claim is "three roster-bound role receipts," not three independently
authenticated people. All three facts are subject-bound limitations every receipt must repeat.

## Current acceptances

Three roster-bound receipts bind subject `86c59c79...` under
`acceptances/86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274/`:

| Role | Source |
| --- | --- |
| EventStore-owner | issue `#352` comment [5550273078](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5550273078) |
| Release-owner | issue `#352` comment [5550277712](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5550277712) |
| Test Architect | self-attested `bmad:murat` record retained beside the owners |

## Reproduce

```text
$ python3 tools/validate-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json \
    --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity] pass: subject=sha256:86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274 selected=sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3
$ echo $?
0
```

```text
$ python3 tools/assemble-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity-assembly] subject=sha256:86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274 receipts=3 verifier_exit=0
$ echo $?
0
```

Reassembly deterministically reproduces subject `86c59c79...` and runs the pinned verifier over its
own output. It does not copy or rewrite any superseded receipt.
