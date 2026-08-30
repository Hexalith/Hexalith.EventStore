# Story 3.15 Corrected Deployed Runtime Parity Closure Proof Packet

## Decision

The technical evidence passes and reproduces, but the acceptance gate is **not** complete. Current
subject
`sha256:86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274`
has **zero of three roster-bound role receipts** (0 of 3), so the retained verifier **fails closed**,
exit 1,
deployed-runtime parity is unavailable and **no identity is selected**.

The only candidate selected identity remains
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`,
and it becomes selectable only once three roster-bound receipts bind this exact subject. This packet
grants no deployment, publication, registry mutation, consumer removal, or predecessor change
authority.

## Authority boundary

An auditor must confirm four flags in `closure.json`, and all four are `false`:

| Flag | Value |
| --- | --- |
| `deployment_authorized` | `false` |
| `publication_authorized` | `false` |
| `consumer_removal_authorized` | `false` |
| `grants_mutation_authority` | `false` |
| `deployed_runtime_parity` | `available` -- **claim only**, granted at 3 of 3 |
| `selected_deployed_identity` | the index digest -- **claim only**, granted at 3 of 3 |

A positive parity verdict is evidence that the deployed runtime matches the corrective release, not
permission to act on it.

**`deployed_runtime_parity` and `selected_deployed_identity` are the claim, not the verdict.**
`closure.json` and `subject.json` carry `deployed_runtime_parity: "available"` and
`selected_deployed_identity: sha256:4b141085...` while `acceptances.receipts` is empty. Those two
fields are the proposition the three rostered roles are asked to accept; the verifier grants them
only when three receipts bind the current subject, and at 0 of 3 it exits 1 and grants nothing. An
auditor grepping the JSON must read them together with the receipt count, never alone.
`acceptances.directory` likewise names the address receipts must occupy, not a directory that
exists today -- at 0 of 3 the packet has no `acceptances/` tree at all.


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

None. The packet has no `acceptances` directory. Collecting three receipts that bind
`86c59c79...` on dedicated Story 3.15 issue
[#352](https://github.com/Hexalith/Hexalith.EventStore/issues/352) is a separately authorized owner
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
[corrected-deployed-runtime-parity-assembly] subject=sha256:86c59c79cf783d2a11ea967fdd4cca8281d01c626b80f9e6a6dc862fbb596274 receipts=0 verifier_exit=1
$ echo $?
1
```

Reassembly deterministically reproduces subject `86c59c79...` and runs the pinned verifier over its
own output. It does not copy or rewrite any superseded receipt.
