# Story 3.15 Corrected Deployed Runtime Parity Closure

## Current verdict

The retained technical evidence reproduces the deployed-runtime candidate for the immutable Story
3.14 release. All three rostered roles accepted the exact unchanged Step-04 subject, and the pinned
verifier now selects the sole bound OCI index.

Current subject: `58f025f354de40fd5eee973a487417b3da45636032a5d1675c9c8c886005e2c6`

Verifier result: `pass` with exactly 3 of 3 roster-bound role receipts.

The selected deployed identity is
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`,
and no other identity is selectable. Deployment, publication, registry mutation, consumer removal,
and predecessor-change authority all remain false.

The loop-6 hardening bound the live assembler and smoke-capture producer bytes and a fourth exact
limitation disclosing that the prior owner acceptance comments were tooling-composed and posted
with the authenticated owner's write credential. Those subject-bound changes superseded the three
`a8cc777e…` receipts. They and their exact source bytes remain unchanged outside the live packet at
`evidence/story-3-15/superseded-acceptances/a8cc777e…/`. With subsequent explicit authorization,
fresh EventStore-owner comment `5424336008`, Release-owner comment `5424339580`, and the
self-attested Test Architect record were retained for unchanged subject `c22d35b…`. Step-04 then
hardened early import isolation, packet/evidence path binding, retained-file and nuspec expansion
bounds, smoke-output path confinement, and gate identity drift. Those trusted-byte changes created
subject `58f025f…`; the entire c22 receipt/source tree is now retained byte-for-byte under
`superseded-acceptances/c22d35b…/`. With renewed exact-subject authorization, EventStore-owner
comment `5425294818`, Release-owner comment `5425297492`, and the self-attested Test Architect
record were retained for `58f025f…`. Initial malformed comment `5425285803` was immediately marked
visibly superseded and is not retained in the packet.

## Reproduce

```text
$ python3 tools/validate-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json \
    --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity] pass: subject=sha256:58f025f354de40fd5eee973a487417b3da45636032a5d1675c9c8c886005e2c6 selected=sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3
$ echo $?
0
```

Reassembly is a runnable, verdict-bearing operation:

```text
$ python3 tools/assemble-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity-assembly] subject=sha256:58f025f354de40fd5eee973a487417b3da45636032a5d1675c9c8c886005e2c6 receipts=3 verifier_exit=0
$ echo $?
0
```

Two consecutive assemblies reproduced the same subject and the same accepted 3/3 verdict.

## Exact lineage

| Field | Retained identity |
| --- | --- |
| Repository | `Hexalith/Hexalith.EventStore` |
| Release | `v3.96.2` / `3.96.2` |
| Source and workflow SHA | `f343bb0153e9cdcb8b12ec10153813072f5ad38d` |
| Release workflow | run `32361958618`, attempt `1` |
| Frozen Story 3.14 identity | `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9` |
| OCI index candidate | `sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3` |
| amd64 child | `sha256:4d42f969dc5f57e0f9baa927c588346d77c31fd2615793b5d8c12c239585af63` |
| arm64 child | `sha256:ede853318267146a9888574f79e16ea1e51c1f363a35910fe883b5a9d7256f44` |
| Technical inventory | `fec6deccc686e4abe83987da16c3935e3e688fa585aa7c5575dead54b4d97611` (24 files) |
| Owner-role registry | `aee4f46be8208ea13704a38d9329320b8a7641b0cdd33e61a138114c8c142f2f` |

The retained closure and canonical subject bind the predecessor, source/workflow/publication
authority, both package byte domains, independent raw OCI graph, both Production smokes, owner
registry, technical inventory, verifier identity, producer identities, candidate decision, rerun
trigger, and non-authority flags.

## Bound trust chain and producers

| Role | Live file | SHA-256 | Size |
| --- | --- | --- | ---: |
| Handler | `tools/deployed_runtime_parity_handlers/v1.py` | `bf8fec6bbd408be0b16f3b64ff219ed8b021e7f6c04aea7f59e819ad51298fef` | 46023 |
| Verifier | `tools/validate-corrected-deployed-runtime-parity.py` | `8b6ef79cdc5eb5ee6cf995880709f582c3fb09cba00a39eff2c043f5e055f32c` | 13829 |
| Predecessor handler | `tools/release_evidence_handlers/v3.py` | `a421791b4c6176afc8120e4e5c4668cb9703976e6f74659c0525119fc5aca5f4` | 46636 |
| Predecessor package | `tools/release_evidence_handlers/__init__.py` | `a33b53f823fa36b822395aee2d01597091b37c26248995c2629b0a9e30c70625` | 78 |
| Assembler | `tools/assemble-corrected-deployed-runtime-parity.py` | `dce0806368ae08ed1869b8255af9ec5d9834de305b4080ef193b1c3ef90c0e79` | 11722 |
| Smoke capture | `tools/capture-corrected-deployed-runtime-parity-smokes.py` | `83ee91db86acc3678edd7d5e900ea9dd2c835c413c7bc7a65b082dd274d37538` | 10390 |

The dispatchers compile the already-verified initializer and handler source bytes directly under
sanitized import resolution. Receipts and retained receipt-source instances sit outside
`technical-sha256.txt` to avoid a subject/receipt hash cycle. The subject instead binds the closed
receipt-source policy; each receipt then binds one exact source by digest and size.

The retained 2026-08-21 smoke bytes predate the current capture tool. The current producer digest is
bound to force future producer changes to re-mint; it does not claim that the historical smoke bytes
were recaptured by the current tool.

## Independent technical evidence

All 14 packages declared by `tools/release-packages.json` were independently retained from
NuGet.org at `3.96.2`. Each archive contains exactly one `.signature.p7s`, and its public bytes map
to the corresponding, distinct GitHub release asset retained by Story 3.14. The verifier reopens
each public archive and validates nuspec ID, version, and repository commit.

The raw immutable OCI index, both child manifests, and both configs reproduce the Story 3.14 graph.
Both immutable children were exercised under `ASPNETCORE_ENVIRONMENT=Production` with bounded
`/alive` probes:

| Platform | Attempts | HTTP | Redirects | Cleanup | Result |
| --- | ---: | ---: | ---: | --- | --- |
| `linux/amd64` | 9 | 200 | 0 | pass | pass |
| `linux/arm64` | 31 | 200 | 0 | pass | pass |

The arm64 smoke requires host emulation registered from the digest-pinned prerequisite
`tonistiigi/binfmt@sha256:400a4873b838d1b89194d982c45e5fb3cda4593fbfd7e08a02e76b03b21166f0`.
That registration is host state and is documented rather than misrepresented as a retained packet
byte.

## Acceptance and authority boundary

The registry retains these role mappings:

- `eventstore-owner` -> `github:jpiquot`
- `release-owner` -> `github:jpiquot`
- `test-architect` -> `bmad:murat`

The retained registry-authority comment on dedicated Story 3.15 issue `#352` says
`reviewer-roster.json`; this is a known copy-carried filename mismatch and is understood to refer to
the retained `registry/owner-role-registry.json`. Correcting the external comment requires a new
owner-authenticated external write and was not performed.

Historical `a8cc777e…` receipts did not authorize `c22d35b…`. The subsequently authorized c22
receipts—EventStore-owner comment
[5424336008](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5424336008)
at `2026-08-26T11:00:05Z`, Release-owner comment
[5424339580](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5424339580)
at `2026-08-26T11:00:25Z`, and the `bmad:murat` Test Architect record at
`2026-08-26T11:01:03Z`—now authorize only superseded subject `c22d35b…` and are retained in audit
storage. Both owner roles resolve to one authenticated GitHub human, the Test
Architect source is self-attested without independent external authentication, and the owner
comments were tooling-composed. The current exact-subject receipts are EventStore-owner comment
[5425294818](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5425294818)
at `2026-08-26T12:28:20Z`, Release-owner comment
[5425297492](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5425297492)
at `2026-08-26T12:28:33Z`, and the `bmad:murat` Test Architect record at
`2026-08-26T12:29:42Z`.

Auditors must check all authority flags independently of the parity candidate:

- `deployment_authorized: false`
- `publication_authorized: false`
- `grants_mutation_authority: false`
- `consumer_removal_authorized: false`

Predecessor mutation is likewise outside this packet's authority. The current 3/3 verdict selects
only the bound index and grants none of those operational authorities.

## Verification results

- Story 3.14 predecessor validation: pass at exact frozen identity `4d1a0c33…`.
- Contracts Release/package-mode build: pass, zero warnings and errors.
- Focused closure: 141 passed; smoke capture: 13 passed; predecessor/provenance: 46 passed. All
  three focused runs have zero failures and zero skips.
- Contracts excluding the unrelated OQ8 closure class: 1448 passed, zero failed or skipped. The
  complete 1763-test run retains the same OQ8-only failure cascade because that separate packet detects pre-existing
  bound-source drift in `DaprTestContainerFixture.cs`; Story 3.15 does not re-seal it.
- Checked-in assembly: stable `58f025f…`, `receipts=3`, `verifier_exit=0`, exit 0.
- Checked-in verifier: pass at three roster-bound receipts; selects only index `4b141085…`.
- `git diff --check`: pass at final local verification.

## Rerun trigger

Rebuild the complete subject and reject all prior receipts after any predecessor, package, OCI,
Production-smoke, inventory, registry, verifier, decision, or receipt-source policy change. Do not
edit an old receipt or move it to a new subject directory.
