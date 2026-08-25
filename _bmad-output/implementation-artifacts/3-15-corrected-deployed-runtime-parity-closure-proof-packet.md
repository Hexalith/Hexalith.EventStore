# Story 3.15 Corrected Deployed Runtime Parity Closure Proof Packet

## Decision

The technical evidence passes and reproduces. **No acceptance binds the current subject**, so the
packet **fails closed at zero of three receipts** and selects no identity. Deployed-runtime parity
is **not** available.

Canonical subject:
`sha256:93559e6134c16d15e295b7c3fbf83d959e86da75d2dfe4201ffdde4d42ac39a0`.

Identity the closure would select once parity becomes available (not selected today):
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.

## Bound evidence

- Frozen predecessor identity: `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
- Source/release: `f343bb0153e9cdcb8b12ec10153813072f5ad38d`, `v3.96.2`.
- Packages: 14 NuGet.org archives with one `.signature.p7s` entry, independently rehashed and cross-mapped to the
  14 Story 3.14 GitHub release assets.
- OCI: independent raw index, two child manifests, and two configs; exact `linux/amd64` and
  `linux/arm64` graph.
- Runtime: bounded Production `/alive`, HTTP 200, zero redirects, cleanup pass for both immutable
  children.
- Technical inventory: 24 exact retained files, SHA-256
  `2d066fb5a5c48c7dd9184f382c098bac36b9e531cd6d86dfc5aa231747ceea86`.
- Owner registry: SHA-256
  `534268c2b5fbf39709e558f9806670d4ed8dd70574574f63babf903dc23e54fc`.

The `dispatch` block binds the four files that decide the verdict. Recompute each with `sha256sum`:

| Role | File | SHA-256 |
| --- | --- | --- |
| Handler | `tools/deployed_runtime_parity_handlers/v1.py` | `e8b6538b11d626bb248fa9a0a61b22a2b2d4c39b5e6561f4e334a50a731f6b3d` |
| Verifier | `tools/validate-corrected-deployed-runtime-parity.py` | `96e5d7d92f88110be23677580f265fde1abfa77c88c30967f6e663deadf80217` |
| Predecessor handler | `tools/release_evidence_handlers/v3.py` | `5cb007497466090956eb35a7d5340cfe4c9e07920a1b55dd1801d422509e0f1b` |
| Predecessor package | `tools/release_evidence_handlers/__init__.py` | `a33b53f823fa36b822395aee2d01597091b37c26248995c2629b0a9e30c70625` |

The v1 package initializer is pinned in the verifier's `IMPORT_PATH_FILE_SHA256`, whose own bytes
are bound by the verifier row above, so the trust chain closes transitively over all five files.

The subject and receipt directories sit outside the technical inventory to avoid a checksum cycle.
The subject binds the inventory and every decision input; `closure.json` binds the subject and,
when present, the three subject-addressed receipt files.

## Acceptances

`acceptances/93559e6134c16d15e295b7c3fbf83d959e86da75d2dfe4201ffdde4d42ac39a0/` is **empty**. Three
receipts — one each for `eventstore-owner`, `release-owner`, and `test-architect` — must be
collected against the exact subject bytes above, on a **dedicated Story 3.15 issue**. Issues `#324`
(Story 1.20) and `#346` (Story 3.14) are rejected by number as cross-lineage splices.

Three earlier receipts exist but bind the superseded subject
`bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709` and were anchored on `#346`.
They are retained, unbound and non-authorizing, under
`evidence/story-3-15/superseded-acceptances/bb58d691…/`. They must not be moved back into the
packet.

Even a complete receipt set would be one authenticated human account plus a self-authored BMAD
record: the roster maps both owner roles to `github:jpiquot`, and the Test Architect receipt is a
`bmad-test-architect-record` without independent external authentication. Every receipt must repeat
that subject-bound limitation.

## Reproduce

```text
$ python3 tools/validate-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json \
    --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity] fail: exactly three packet-bound receipts are required; rerun: Rebuild the complete subject and reject all prior receipts after any predecessor, package, OCI, Production-smoke, inventory, registry, verifier, decision, or receipt-source change.
$ echo $?
1
```

To rebuild the packet after any bound input changes, run
`tools/assemble-corrected-deployed-runtime-parity.py <packet-root>`; it re-mints the subject, runs
the verifier over its own output, and exits non-zero unless three receipts bind the result.

## Authority boundary

Even a passing receipt set proves immutable evidence only. `deployment_authorized`,
`consumer_removal_authorized`, `publication_authorized`, and `grants_mutation_authority` remain
false. Deployment and consumer removal require separate owner decisions outside this packet.
