# Story 3.15 Corrected Deployed Runtime Parity Closure Proof Packet

## Decision

The technical evidence passes and reproduces. **No acceptance binds the current subject**, so the
packet **fails closed at zero of three receipts** and selects no identity. Deployed-runtime parity
is **not** available.

Canonical subject:
`sha256:5acb81765201a22d6493d815a56f4b8d9c1ba141280779716013962eca3fa5f5`.

Identity the closure would select once parity becomes available (not selected today):
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.

## Bound evidence

- Frozen predecessor identity: `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
- Source/release: `f343bb0153e9cdcb8b12ec10153813072f5ad38d`, `v3.96.2`.
- Packages: 14 NuGet.org repository-signed archives independently rehashed and cross-mapped to the
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
| Handler | `tools/deployed_runtime_parity_handlers/v1.py` | `4f1b62ef5b3f350b4a92da5cd3e7992d52474f7caa705d7993e4cebfedabc12d` |
| Verifier | `tools/validate-corrected-deployed-runtime-parity.py` | `e7370f29fc5dd80378471d795492c64578ee32ad26cbe2a1417cb03991c3d161` |
| Predecessor handler | `tools/release_evidence_handlers/v3.py` | `3f366eee1509f5350806b9277eb514d20987790fff5b248f81155dbb5857d490` |
| Predecessor package | `tools/release_evidence_handlers/__init__.py` | `a33b53f823fa36b822395aee2d01597091b37c26248995c2629b0a9e30c70625` |

The v1 package initializer is pinned in the verifier's `IMPORT_PATH_FILE_SHA256`, whose own bytes
are bound by the verifier row above, so the trust chain closes transitively over all five files.

The subject and receipt directories sit outside the technical inventory to avoid a checksum cycle.
The subject binds the inventory and every decision input; `closure.json` binds the subject and,
when present, the three subject-addressed receipt files.

## Acceptances

`acceptances/5acb81765201a22d6493d815a56f4b8d9c1ba141280779716013962eca3fa5f5/` is **empty**. Three
receipts — one each for `eventstore-owner`, `release-owner`, and `test-architect` — must be
collected against the exact subject bytes above, on a **dedicated Story 3.15 issue**. Issues `#324`
(Story 1.20) and `#346` (Story 3.14) are rejected by number as cross-lineage splices.

Three earlier receipts exist but bind the superseded subject
`bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709` and were anchored on `#346`.
They are retained, unbound and non-authorizing, under
`evidence/story-3-15/superseded-acceptances/bb58d691…/`. They must not be moved back into the
packet.

Even a complete receipt set would be one human account plus tooling: the roster maps both owner
roles to `github:jpiquot` and the Test Architect receipt is a `bmad-test-architect-record`.

## Reproduce

```text
$ python3 tools/validate-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json \
    --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity] fail: exactly three packet-bound receipts are required
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
