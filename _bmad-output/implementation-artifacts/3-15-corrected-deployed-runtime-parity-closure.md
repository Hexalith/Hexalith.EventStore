# Story 3.15 Corrected Deployed Runtime Parity Closure

## Current verdict

**Fail closed pending three real acceptances.** The complete technical lineage passes, but the
EventStore owner, Release owner, and Test Architect have not yet accepted the unchanged canonical
subject. The production verifier therefore selects no deployment-grade identity today and reports
`exactly three packet-bound receipts are required`.

The acceptance-ready subject is
`sha256:bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709`.
If all three valid receipts are retained beneath
`acceptances/bb58d691ee404cc958433e996204e3382721de3931ac64cf8f7a61de97c30709/`
without changing any subject input, the verified positive identity is exactly
`sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.

This record supplies evidence only. It never authorizes deployment, publication, registry mutation,
consumer removal, or predecessor changes.

## Exact lineage

| Field | Retained identity |
| --- | --- |
| Repository | `Hexalith/Hexalith.EventStore` |
| Release | `v3.96.2` / `3.96.2` |
| Source and workflow SHA | `f343bb0153e9cdcb8b12ec10153813072f5ad38d` |
| Release workflow | run `32361958618`, attempt `1` |
| Frozen Story 3.14 identity | `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9` |
| OCI index | `sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3` |
| amd64 child | `sha256:4d42f969dc5f57e0f9baa927c588346d77c31fd2615793b5d8c12c239585af63` |
| arm64 child | `sha256:ede853318267146a9888574f79e16ea1e51c1f363a35910fe883b5a9d7256f44` |
| Technical inventory | `2d066fb5a5c48c7dd9184f382c098bac36b9e531cd6d86dfc5aa231747ceea86` |
| Owner-role registry | `534268c2b5fbf39709e558f9806670d4ed8dd70574574f63babf903dc23e54fc` |

The retained [closure](evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json)
and [canonical subject](evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/subject.json)
bind the predecessor, source/workflow/publication authority, both package byte domains, independent
raw OCI graph, both Production smokes, owner registry, technical inventory, verifier identity,
positive outcome, selected index, rerun trigger, and all non-authority flags.

## Independent technical evidence

All 14 packages declared by `tools/release-packages.json` were downloaded from NuGet.org at
`3.96.2`. Each public archive contains the repository signature and retains a different SHA-256 from
the unsigned GitHub release asset in Story 3.14. The verifier reopens every public archive, checks
the signature entry and nuspec ID/version/repository commit, and maps it to the matching predecessor
asset without conflating the byte domains.

The public registry was reread by immutable digest. The independent index, both child manifests,
and both configs are byte-equal to the Story 3.14 graph and reproduce every descriptor, platform,
config, and provenance edge.

Both immutable children were then executed independently with `ASPNETCORE_ENVIRONMENT=Production`
and a fixed non-secret smoke-only JWT configuration. The bounded `/alive` results are:

| Platform | Attempts | HTTP | Redirects | Cleanup | Result |
| --- | ---: | ---: | ---: | --- | --- |
| `linux/amd64` | 9 | 200 | 0 | pass | pass |
| `linux/arm64` | 31 | 200 | 0 | pass | pass |

The arm64 run used the already-cached, digest-pinned `tonistiigi/binfmt` image
`sha256:400a4873b838d1b89194d982c45e5fb3cda4593fbfd7e08a02e76b03b21166f0`
to register arm64 emulation before executing the immutable child.

## Acceptance gate

The role registry retains the owner-ratified mappings:

- `eventstore-owner` → `github:jpiquot`
- `release-owner` → `github:jpiquot`
- `test-architect` → `bmad:murat`

No planning approval, release authority, prior receipt, label, tag, self-declared role, or synthetic
test fixture is treated as a Story 3.15 acceptance. The two owner receipts require retained GitHub
issue-comment sources whose exact JSON bodies accept this subject. The Test Architect receipt
requires a retained `bmad:murat` source record. All three receipts must repeat the exact scope,
limitations, decision, identity, role, subject digest, and acceptance time. Any subject change
invalidates all of them.

## Commands and results

- Story 3.14 predecessor validation: pass, exact digest `4d1a0c33…`.
- Contracts Release/package-mode build: pass, zero warnings and errors.
- Focused Story 3.15 suite: 48 passed, zero failed, zero skipped. Its positive case uses explicit
  synthetic test fixtures only to mutation-prove the receipt contract; those fixtures are never
  copied into the retained packet.
- Checked-in Story 3.15 verifier: expected fail-closed result, three receipts missing.
- `git diff --check`: recorded at final handoff.

## Rerun trigger

Rebuild the complete subject and reject all prior receipts after any predecessor, package, OCI,
Production-smoke, inventory, registry, verifier, decision, or receipt-source change. Do not edit an
old receipt or move it to a new subject directory.
