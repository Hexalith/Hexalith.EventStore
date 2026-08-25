# Story 3.15 Corrected Deployed Runtime Parity Closure

## Current verdict

**Deployed-runtime parity is available.** The technical lineage reproduces and exactly three fresh,
roster-bound role receipts bind the unchanged current subject. The verifier selects only the
approved OCI index while every operational-authority flag remains false.

The current canonical subject is
`sha256:a8cc777ed04f1f0a7f7dffb7f24f7359f786e9114afe04fc69b1aa90cb8fdf7f`.
Its `acceptances` directory contains the three current receipts and their exact retained sources.
The prior `dab64f5f...` tree remains byte-for-byte outside the packet under
`evidence/story-3-15/superseded-acceptances/`.

Running the retained verifier reproduces exactly this state:

```text
$ python3 tools/validate-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json \
    --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity] pass: subject=sha256:a8cc777ed04f1f0a7f7dffb7f24f7359f786e9114afe04fc69b1aa90cb8fdf7f selected=sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3
$ echo $?
0
```

The selected deployed identity is
`sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.

### Why the subject changed five times

Three 2026-08-25 review loops each re-minted the subject, and by the packet's own rerun trigger each
re-mint rejected every receipt collected against the prior subject.

- Loop 2 bound the transitively imported `tools/release_evidence_handlers/v3.py`, which was
  previously bound nowhere. Subject `bb58d691…` → `1dee194f…`. The three real receipts collected
  for `bb58d691…` were rejected and moved, unbound, to
  `evidence/story-3-15/superseded-acceptances/bb58d691…/`.
- Loop 3 hardened the acceptance-source, registry, and timestamp checks (below). Subject
  `1dee194f…` → `5acb8176…`.
- Loop 4 made verified source bytes the only executable module representation, bound the complete
  four-module import provenance, rejected XML entity declarations and symlinks, made every
  recomputed-content guard mutation-reachable, and bound the Test Architect self-attestation
  limitation into every receipt. Subject `5acb8176…` → `93559e61…`.
- The authorized completion pass created dedicated Story 3.15 issue `#352`, retained a
  Story-3.15-scoped MEMBER-authenticated roster source, and replaced the issue denylist with an exact
  `#352` allowlist. Binding those new handler and registry bytes changed subject `93559e61…` →
  `dab64f5f…` before any final receipt was collected.
- The trusted-verifier review then isolated dependency imports, made the predecessor dispatcher
  source-only, rejected non-UTF-8 nuspec XML and non-integer smoke facts, bounded the complete smoke
  capture lifecycle, and corrected the rerun trigger to bind receipt-source policy changes.
  Subject `dab64f5f…` → `a8cc777e…`; all `dab64f5f…` receipts became superseded.

The first superseded receipt set is additionally rejected on lineage: both owner comments were anchored on
issue [#346](https://github.com/Hexalith/Hexalith.EventStore/issues/346), which is Story 3.14's
acceptance thread. Reusing it is the cross-lineage splice this story family exists to prevent. The
verifier now accepts owner receipts only from dedicated issue `#352`, rejecting every sibling issue.

### Acceptance action

With fresh exact-subject authorization, the three current acceptances were collected against
`a8cc777e...` and retained without changing the subject:

1. EventStore-owner comment
   [5409145568](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5409145568),
   created at `2026-08-25T10:33:29Z`.
2. Release-owner comment
   [5409148235](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5409148235),
   created at `2026-08-25T10:33:45Z`.
3. Test Architect `bmad:murat` self-attested record, accepted at `2026-08-25T10:34:41Z`.

The first timestamp attempt for each owner role crossed GitHub's second boundary. Comments
[5409140199](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5409140199) and
[5409147909](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5409147909) were
immediately marked `SUPERSEDED — INVALID TIMESTAMP-MISMATCH ATTEMPT` and were not retained as packet
sources.

### Superseded acceptance history

The owner authorized and completed evidence acceptance against exact historical subject
`dab64f5f…`:

1. Dedicated Story 3.15 issue [#352](https://github.com/Hexalith/Hexalith.EventStore/issues/352)
   carries the scoped roster and both owner acceptances.
2. Comment [5408186984](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5408186984)
   supplies the EventStore-owner acceptance, and comment
   [5408189299](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5408189299)
   supplies the Release-owner acceptance.
3. The Test Architect `bmad:murat` acceptance carried the exact self-attestation limitation.

Those three receipts and sources are preserved byte-for-byte in the superseded audit area and
authorize nothing for `a8cc777e…`.

**Self-attestation caveat:** the owner-role registry maps both `eventstore-owner` and
`release-owner` to `github:jpiquot`, and the `test-architect` receipt is a self-attested
`bmad-test-architect-record` without independent external authentication. Every receipt must repeat
that subject-bound limitation. A 3-of-3 result would therefore be two roster-bound owner roles held
by one authenticated human plus a self-authored BMAD record, not independent three-party review.

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
| Technical inventory | `fec6deccc686e4abe83987da16c3935e3e688fa585aa7c5575dead54b4d97611` |
| Owner-role registry | `aee4f46be8208ea13704a38d9329320b8a7641b0cdd33e61a138114c8c142f2f` |

The retained [closure](evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json)
and [canonical subject](evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/subject.json)
bind the predecessor, source/workflow/publication authority, both package byte domains, independent
raw OCI graph, both Production smokes, owner registry, technical inventory, verifier identity,
positive outcome, selected index, rerun trigger, and all non-authority flags.

## Independent technical evidence

All 14 packages declared by `tools/release-packages.json` were downloaded from NuGet.org at
`3.96.2`. Each public archive contains one `.signature.p7s` entry and retains a different SHA-256 from
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

The current EventStore-owner acceptance was created at `2026-08-25T10:33:29Z` in issue comment
[5409145568](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5409145568).
The current Release-owner acceptance was created at `2026-08-25T10:33:45Z` in issue comment
[5409148235](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5409148235).
The current Test Architect acceptance was created at `2026-08-25T10:34:41Z` in the retained
`bmad:murat` source record. All three bind exact subject `a8cc777e…`; the earlier `dab64f5f…`
receipts remain superseded and unbound.

No planning approval, release authority, prior receipt, label, tag, self-declared role, or synthetic
test fixture is treated as a Story 3.15 acceptance. The two owner receipts require retained GitHub
issue-comment sources whose exact JSON bodies accept this subject. The Test Architect receipt
requires a retained `bmad:murat` source record. All three receipts must repeat the exact scope,
limitations, decision, identity, role, subject digest, and acceptance time. Any subject change
invalidates all of them.

## Commands and results

- Story 3.14 predecessor validation: pass, exact digest `4d1a0c33…`.
- Contracts Release/package-mode build: pass, zero warnings and errors.
- Focused Story 3.15 closure and smoke-capture suites: 114 passed, zero failed, zero skipped. The
  positive closure case uses explicit
  synthetic test fixtures only to mutation-prove the receipt contract; those fixtures are never
  copied into the retained packet.
- Focused predecessor/provenance suite: 34 passed, zero failed, zero skipped.
- Complete Contracts suite: 1702 passed, zero failed, zero skipped.
- Checked-in Story 3.15 assembler and verifier: pass at three receipts without subject drift;
  subject `a8cc777e…`; selected OCI index `4b141085…`; all non-authority flags false.
- `git diff --check`: recorded at final handoff.

## Rerun trigger

Rebuild the complete subject and reject all prior receipts after any predecessor, package, OCI,
Production-smoke, inventory, registry, verifier, decision, or receipt-source policy change. An
individual source replacement invalidates its bound receipt and any complete verdict without
re-minting the subject. Do not edit an old receipt or move it to a new subject directory.
