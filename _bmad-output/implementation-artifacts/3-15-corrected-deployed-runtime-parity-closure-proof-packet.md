# Story 3.15 Corrected Deployed Runtime Parity Closure Proof Packet

## Decision

**Deployed-runtime parity fails closed at 0 of 3 receipts.** Current subject
`sha256:66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6`
was re-minted on 2026-09-26 after fresh Production smokes used the bound `curl -q` producer and
limitation 4 was corrected. Former subject `c98fdef2...` had no receipts; its packet is preserved
under `evidence/story-3-15/superseded-packets/`. The three older `7d64f87e...` receipts remain
superseded. The retained verifier exits 1
and selects no deployed identity. `closure.json` still claims only
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`,
pending three fresh subject-bound acceptances.

This packet still grants no deployment, publication, registry mutation, consumer removal, or
predecessor change authority. The rostered owner accepted both owner roles for the prior subject;
the Test Architect acceptance for that subject was self-attested. None binds the current subject.
The [fresh independent Test Architect decision](3-15-test-architect-decision-66be1b4a.md) accepts
this subject's technical evidence as a local self-attested report. It is not a packet receipt and
does not change the 0/3 verifier outcome.

## Authority boundary

An auditor must confirm four flags in `closure.json`, and all four are `false`:

| Flag | Value |
| --- | --- |
| `deployment_authorized` | `false` |
| `publication_authorized` | `false` |
| `consumer_removal_authorized` | `false` |
| `grants_mutation_authority` | `false` |
| `deployed_runtime_parity` | `available` -- claim not granted at 0 of 3 |
| `selected_deployed_identity` | the index digest -- claim not selected at 0 of 3 |

**`deployed_runtime_parity` and `selected_deployed_identity` remain the claim fields.** The
retained verifier rejects them because there are no packet-bound receipts for this subject. An
auditor must read them together with the empty receipt list and four false non-authority flags.
The current receipt address is
`acceptances/66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6/`;
it contains no receipts yet.

## Bound technical evidence

- Frozen predecessor identity: `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
- Source/release: `f343bb0153e9cdcb8b12ec10153813072f5ad38d`, `v3.96.2`.
- Packages: 14 NuGet.org archives independently rehashed and cross-mapped to the Story 3.14 assets.
- OCI: independent raw index, two child manifests, and two configs for `linux/amd64` and
  `linux/arm64`.
- Runtime: bounded Production `/alive`, exact HTTP 200, zero redirects, cleanup pass for both
  immutable children. The fresh capture ran from `2026-09-26T07:29:14.107654Z` through
  `2026-09-26T07:30:34.182021Z` against the same child digests, using the `curl -q` producer.
- Owner registry: SHA-256
  `aee4f46be8208ea13704a38d9329320b8a7641b0cdd33e61a138114c8c142f2f`.
- Closed technical inventory: SHA-256
  `32dc22facf833c665037738c3fe7d8b1ab0fb3f73f7e28fb175ef397f5e9fb84` over exactly **24** retained
  files. Anything else under the packet root is rejected.

The current dispatch binds these verdict-bearing live files:

| Role | File | SHA-256 |
| --- | --- | --- |
| Handler | `tools/deployed_runtime_parity_handlers/v1.py` | `b2b550794dc27ad7c33a9c221c594d0f456f1b90879477d3432df81336a9e6c0` |
| Verifier | `tools/validate-corrected-deployed-runtime-parity.py` | `0a768dda6de7b9c76d4c3aecabbab046c5e284ff0ebe605883e3d22e90b36613` |
| Predecessor handler | `tools/release_evidence_handlers/v3.py` | `20fcb7d024f4c1cb024f8d2c87bc1d5d47c2bb242a9126098ded025603f1204a` |
| Predecessor package | `tools/release_evidence_handlers/__init__.py` | `a33b53f823fa36b822395aee2d01597091b37c26248995c2629b0a9e30c70625` |
| Smoke capture producer | `tools/capture-corrected-deployed-runtime-parity-smokes.py` | `7d134165963877d7633295bdb00504b4ea6b7424f26142cc6e3495b18ef48236` |
| Packet assembler | `tools/assemble-corrected-deployed-runtime-parity.py` | `30328249592d11ab580a401b136c2c25d83d6c023b8cfcfb85497ac241b31ec2` |

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

Five acceptance sets are retained byte-for-byte outside the packet under
`evidence/story-3-15/superseded-acceptances/`: `bb58d691...`, `dab64f5f...`, `a8cc777e...`,
`86c59c79...`, and `7d64f87e...`. The latest set was superseded by the 2026-09-26 curl isolation
re-mint. Other subjects had no collected receipts, so they have no receipt set to retain.
They authorize nothing for the current subject. That directory's README carries the re-rooting rule
an auditor needs, because each retained receipt still declares its source under the
`acceptances/<subject>/sources/` path it bound while it was live.

For every historical set the two owner roles map to one authenticated GitHub human,
`github:jpiquot`, while the Test Architect role is the explicitly limited, self-attested
`bmad:murat` record. The two owner comments were composed by repository tooling and posted with
the rostered credential rather than typed by hand.
This is why the operator-facing claim is "three roster-bound role receipts," not three independently
authenticated people. The Test Architect and tooling caveats are required limitations repeated in
the receipts; the shared-owner-account mapping is subject-bound in the role registry, not repeated
as a receipt limitation.

## Current acceptances and prior sources

No receipt binds current subject `66be1b4a...`. The three prior receipts bind `7d64f87e...`
and remain byte-for-byte under `superseded-acceptances/7d64f87e.../`. Their historical sources are:

| Role | Source |
| --- | --- |
| EventStore-owner | issue `#352` comment [5789893766](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5789893766) |
| Release-owner | issue `#352` comment [5789897143](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5789897143) |
| Test Architect | self-attested `bmad:murat` record in the superseded acceptance directory |

Both owner comments are authenticated to the rostered `github:jpiquot` account, and each GitHub
`created_at` and `updated_at` equals the receipt's `accepted_at`. Six timestamp-mismatched attempts
were visibly marked superseded on issue `#352` and are not retained in the packet.

The after-the-fact ratification of the 2026-09-23 collection run is
[issue #352 comment 5803577826](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5803577826),
created at `2026-09-23T21:53:37Z` by authenticated `github:jpiquot` (account id `6775094`),
about 16 hours after the kept receipts at `06:00:46Z` and `06:01:05Z`, and after commit
`a2f5cba2`. It is an agent-composed request that quotes the owner's written line
“I Jérôme Piquot, owner; authorize” verbatim. Under review decision D1, that quoted line counts
as the owner's authorization; the request's scope and two acceptance comment IDs are agent-composed
text. This ratification is distinct from the packet-bound acceptance receipts and grants no
operational authority.
This is an as-observed external audit citation: the comment is mutable, is not retained or
hash-closed in the packet, and is not checked by the current 0/3 verifier verdict.

## DW-508 sign-offs

Under review decision D2, the owner chose to count the spec's 2026-09-23 Change Log architecture
sign-off statement, the prior subject-bound and self-attested `bmad:murat` Test Architect receipt,
and the four-layer 2026-09-23 review of receipt-collection commit `a2f5cba2` as the architecture,
test, and security records, respectively. The first is a narrative result, the second accepts the
parity subject, and the third records code review findings and triage. No dedicated Story 3.15
architecture review, Security Reviewer record, or trust-path-test attestation is retained. The
Story 4.15 v4 security and test review files bind a different subject.

## Reproduce

```text
$ python3 tools/validate-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json \
    --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity] fail: exactly three packet-bound receipts are required
$ echo $?
1
```

```text
$ python3 tools/assemble-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity-assembly] subject=sha256:66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6 receipts=0 verifier_exit=1
$ echo $?
1
```

Reassembly deterministically reproduces subject `66be1b4a...` and runs the pinned verifier over its
own output. It does not copy or rewrite any superseded receipt.

The historical complete Contracts suite after the 2026-09-24 in-clone and elided-subject
regressions passed **2108/2108**, with zero failures, skips, or unrun tests. The current Release
build had zero warnings and errors, and both focused Story 3.15 classes passed **235/235**, with
zero failures, skips, or unrun tests. The Story 3.14 predecessor packet remains unchanged.
