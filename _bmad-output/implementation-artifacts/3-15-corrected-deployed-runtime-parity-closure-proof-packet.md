# Story 3.15 Corrected Deployed Runtime Parity Closure Proof Packet

## Decision

**The packet fails closed at 0 of 3 receipts.** Current subject
`sha256:84dee6e51844ddd0be403fefc56848f1b8f1dd916456f3b205f5bc52066db75f`
has **zero of three roster-bound role receipts**, so the retained verifier **fails closed**,
exit 1, and grants nothing. The only identity this closure may ever select, once parity is
available, remains
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.

This packet still grants no deployment, publication, registry mutation, consumer removal, or
predecessor change authority. Collecting three fresh receipts on issue `#352` remains an Ask First
owner action and was not performed.

## Authority boundary

An auditor must confirm four flags in `closure.json`, and all four are `false`:

| Flag | Value |
| --- | --- |
| `deployment_authorized` | `false` |
| `publication_authorized` | `false` |
| `consumer_removal_authorized` | `false` |
| `grants_mutation_authority` | `false` |
| `deployed_runtime_parity` | `available` -- claim, granted only at 3 of 3 |
| `selected_deployed_identity` | the index digest -- claim, granted only at 3 of 3 |

**`deployed_runtime_parity` and `selected_deployed_identity` remain the claim fields.** At zero
packet-bound receipts they are not granted; an auditor must still read them together with the
receipt count and the four non-authority flags, never alone. Receipts would live under
`acceptances/84dee6e51844ddd0be403fefc56848f1b8f1dd916456f3b205f5bc52066db75f/` once collected.

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
| Handler | `tools/deployed_runtime_parity_handlers/v1.py` | `c19b47817f826b78fabd2c7365dcec7e40cd0d0ee2ab2ffca10a2fc06b0e5178` |
| Verifier | `tools/validate-corrected-deployed-runtime-parity.py` | `dababb480cbea609fd500a248b479c367d531012d3dc61a2a713e9f28600e104` |
| Predecessor handler | `tools/release_evidence_handlers/v3.py` | `b1a1756252fb79777dd0dbb37861260f6b02c03c58c160823b2c4252d0c33dc0` |
| Predecessor package | `tools/release_evidence_handlers/__init__.py` | `a33b53f823fa36b822395aee2d01597091b37c26248995c2629b0a9e30c70625` |
| Smoke capture producer | `tools/capture-corrected-deployed-runtime-parity-smokes.py` | `74a1b0c0e181104f01db073e94dd0e79c1926dff6dc491de015d69d25fa3a644` |
| Packet assembler | `tools/assemble-corrected-deployed-runtime-parity.py` | `abb3c77c826aedc10b8f75ae2533affc90963ca3390270eff3125019269cb032` |

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

Four acceptance sets are retained byte-for-byte outside the packet under
`evidence/story-3-15/superseded-acceptances/`: `bb58d691...`, `dab64f5f...`, `a8cc777e...`, and the
three receipts collected against `86c59c79...` that the 2026-09-06 Group A remint rejected. There are
four sets against nine subjects because receipts were only ever collected for four of them; the other
five subjects happened before any receipt existed, so their absence is expected rather than a gap.
They authorize nothing for the current subject. That directory's README carries the re-rooting rule
an auditor needs, because each retained receipt still declares its source under the
`acceptances/<subject>/sources/` path it bound while it was live.

For every historical set the two owner roles map to one authenticated GitHub human,
`github:jpiquot`, while the Test Architect role is the explicitly limited, self-attested
`bmad:murat` record, and every receipt was composed by repository tooling rather than typed by hand.
This is why the operator-facing claim is "three roster-bound role receipts," not three independently
authenticated people. All three facts are subject-bound limitations every receipt must repeat.

## Current acceptances

No packet-bound receipt currently binds subject `84dee6e5...`. The 2026-09-05 round for subject
`86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274` was moved unmodified to
`evidence/story-3-15/superseded-acceptances/86c59c79.../`:

| Role | Source |
| --- | --- |
| EventStore-owner | issue `#352` comment [5550273078](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5550273078) |
| Release-owner | issue `#352` comment [5550277712](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5550277712) |
| Test Architect | self-attested `bmad:murat` record retained beside the owners |

Collecting three fresh receipts on issue `#352` for the current subject remains an Ask First owner
action and was not performed.

## Reproduce

```text
$ python3 tools/validate-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json \
    --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity] fail: exactly three packet-bound receipts are required; rerun: Rebuild the complete subject and reject all prior receipts after any predecessor, package, OCI, Production-smoke, inventory, registry, verifier, decision, or receipt-source policy change.
$ echo $?
1
```

```text
$ python3 tools/assemble-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity-assembly] subject=sha256:84dee6e51844ddd0be403fefc56848f1b8f1dd916456f3b205f5bc52066db75f receipts=0 verifier_exit=1
$ echo $?
1
```

Reassembly deterministically reproduces subject `84dee6e5...` and runs the pinned verifier over its
own output. It does not copy or rewrite any superseded receipt.
