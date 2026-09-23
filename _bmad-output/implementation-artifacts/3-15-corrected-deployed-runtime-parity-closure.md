# Story 3.15 Corrected Deployed Runtime Parity Closure

## Current verdict

**Deployed-runtime parity is available at 3 of 3 receipts.** Current subject
`sha256:7d64f87e3e6d85163651e7748c751222ca1f0fb4f0c47f21408a2bde4eba5274`. The DW-508 trust-path
correction re-minted the subject after the Story 5.3 producer hardening. The Group A remint had rejected the three
`86c59c79...` receipts collected on 2026-09-05. Those receipts remain byte-for-byte outside the
packet under `evidence/story-3-15/superseded-acceptances/86c59c79.../`, alongside the earlier
`bb58d691...`, `dab64f5f...` and `a8cc777e...` trees. The rostered `github:jpiquot` owner
accepted the exact current subject for both owner roles on issue `#352`, and `bmad:murat` accepted
it as a self-attested Test Architect. The selected identity is only
`registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`.
This remains evidence only; deployment, publication, registry mutation, consumer removal, and
predecessor change require separate authority.

Running the retained verifier reproduces exactly this state:

```text
$ python3 tools/validate-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json \
    --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity] pass: subject=sha256:7d64f87e3e6d85163651e7748c751222ca1f0fb4f0c47f21408a2bde4eba5274 selected=sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3
$ echo $?
0
```

Re-running the assembler is idempotent and reports the same accepted verdict:

```text
$ python3 tools/assemble-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
[corrected-deployed-runtime-parity-assembly] subject=sha256:7d64f87e3e6d85163651e7748c751222ca1f0fb4f0c47f21408a2bde4eba5274 receipts=3 verifier_exit=0
$ echo $?
0
```

**`deployed_runtime_parity` and `selected_deployed_identity` remain claim fields whose grant is
the 3-of-3 receipt gate.** The retained verifier now grants them after validating all three receipts;
an auditor must still read them with the receipt bindings and four false non-authority flags.

### Completed owner action (now superseded)

1. Posted EventStore-owner and Release-owner acceptances on issue `#352`, each binding subject
   `86c59c79...` with the exact scope, decision, identity, role and the four required limitations,
   and each with `created_at == updated_at == accepted_at` to the second.
2. Retained the `bmad:murat` Test Architect self-attested record for the same subject.
3. Re-ran the assembler, which re-derived the receipt bindings and re-ran the pinned verifier over
   its own output.

Those earlier three receipts no longer bind the current subject. They are retained unbound for audit.

### Current owner action

- EventStore-owner: [issue #352 comment 5789893766](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5789893766), authored by authenticated `github:jpiquot`.
- Release-owner: [issue #352 comment 5789897143](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5789897143), authored by the same rostered account.
- Test Architect: subject-bound, self-attested `bmad:murat` record in the current packet.

Both owner comments have `created_at == updated_at == accepted_at`; all three accept the four
subject-bound limitations. Six earlier timestamp-mismatched posting attempts were visibly marked
superseded on issue `#352` and are not retained in the packet.

The owner's separate authorization of the 2026-09-23 receipt-collection run is recorded in
[issue #352 comment 5803577826](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5803577826),
authored by authenticated `github:jpiquot` (account id `6775094`). It quotes the owner's exact
written response, “I Jérôme Piquot, owner; authorize,” alongside the request's scope and the two
owner acceptance comment IDs. This authorization records the collection run; the two acceptance
comments remain the subject-bound receipt evidence. It grants no deployment, publication,
registry, consumer-removal, or predecessor-change authority.
This is an as-observed external audit citation: the comment is mutable, is not retained or
hash-closed in the packet, and is not checked by the verifier that returns the 3/3 parity verdict.

### Why the subject changed

Five 2026-08-25 review loops, two authorized completion/hardening passes, the 2026-08-30
producer/verifier hardening, and the 2026-09-06 Group A tools patch each re-minted the
subject, and by the packet's own rerun trigger each re-mint rejected every receipt collected against
the prior subject. Receipts existed for only four subjects; the others happened before any receipt
had been collected for them.

- Loop 2 bound the transitively imported `tools/release_evidence_handlers/v3.py`, which was
  previously bound nowhere. Subject `bb58d691...` -> `1dee194f...`. The three real receipts collected
  for `bb58d691...` were rejected and moved, unbound, to
  `evidence/story-3-15/superseded-acceptances/bb58d691.../`.
- Loop 3 hardened the acceptance-source, registry, and timestamp checks (below). Subject
  `1dee194f...` -> `5acb8176...`.
- Loop 4 made verified source bytes the only executable module representation, bound the complete
  four-module import provenance, rejected XML entity declarations and symlinks, made every
  recomputed-content guard mutation-reachable, and bound the Test Architect self-attestation
  limitation into every receipt. Subject `5acb8176...` -> `93559e61...`.
- The authorized completion pass created dedicated Story 3.15 issue `#352`, retained a
  Story-3.15-scoped MEMBER-authenticated roster source, and replaced the issue denylist with an exact
  `#352` allowlist. Binding those new handler and registry bytes changed subject `93559e61...` ->
  `dab64f5f...` before any final receipt was collected.
- The trusted-verifier review then isolated dependency imports, made the predecessor dispatcher
  source-only, rejected non-UTF-8 nuspec XML and non-integer smoke facts, bounded the complete smoke
  capture lifecycle, and corrected the rerun trigger to bind receipt-source policy changes.
  Subject `dab64f5f...` -> `a8cc777e...`; all `dab64f5f...` receipts became superseded. Three fresh
  receipts were then collected against `a8cc777e...`.
- Loop 6 landed as one authorized batch: both packet producers became bound decision inputs, the
  retained GitHub comment envelopes became closed-schema, a fourth limitation disclosing
  tooling-composed receipts was bound into every receipt, cleanup became bounded rather than skipped
  when a platform budget is exhausted, the capture began refusing to overwrite retained evidence,
  the nuspec DTD scan was narrowed to the XML prolog, and a post-import path assertion that was true
  by construction was removed. Subject `a8cc777e...` -> `e27f9f39...`; all three `a8cc777e...`
  receipts became superseded.
- Loop 7 landed at zero receipts (nothing to burn), where a re-mint costs nothing. It closed a **fail-open regression
  loop 6 introduced**: the narrowed nuspec prolog scan returned silently when the prolog did not
  begin with `<`, and since `utf-8-sig` strips exactly one byte-order mark, a doubled BOM left a
  residual U+FEFF that skipped the scan -- a nuspec carrying a DTD was then accepted with a smuggled
  entity resolving into the package id. It also closed a bytes-path guard bypass loop 6 introduced
  in both dispatchers, made the roster-configuration guard able to fail, widened the per-platform
  smoke bound so the capture tool cannot emit records the verifier rejects, and bound the assembler
  to its own executing bytes. Subject -> `663747b1...`; no receipts existed to reject.
- The 2026-08-30 producer/verifier hardening established a hermetic interpreter boundary, closed
  proxy and producer-path escapes, bounded support-safe producer failures, tightened packet and
  smoke semantics, and expanded executable regression coverage. Subject `663747b1...` ->
  `86c59c79...`; no receipts existed to reject.
- The 2026-09-06 Group A tools patch landed as one re-mint: a `TimeoutExpired` during `docker run`
  now inspects/rms the uuid-named container; the assembler restores the previous `closure.json` when
  the pinned verifier does not complete; encrypted or unsupported-compression nuspec reads fail
  closed as `EvidenceError`; and the closed inventory exempts only the validated evidence path.
  Subject `86c59c79...` -> `84dee6e5...`; all three `86c59c79...` receipts became superseded.

The first superseded receipt set is additionally rejected on lineage: both owner comments were anchored on
issue [#346](https://github.com/Hexalith/Hexalith.EventStore/issues/346), which is Story 3.14's
acceptance thread. Reusing it is the cross-lineage splice this story family exists to prevent. The
verifier now accepts owner receipts only from dedicated issue `#352`, rejecting every sibling issue.

### Superseded acceptance history

Four complete acceptance rounds were collected and are now superseded. All four are retained
byte-for-byte in the superseded audit area and authorize nothing for `7d64f87e...`.

Against subject `dab64f5f...`:

1. Dedicated Story 3.15 issue [#352](https://github.com/Hexalith/Hexalith.EventStore/issues/352)
   carries the scoped roster and both owner acceptances.
2. Comment [5408186984](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5408186984)
   supplied the EventStore-owner acceptance, and comment
   [5408189299](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5408189299)
   supplied the Release-owner acceptance.
3. The Test Architect `bmad:murat` acceptance carried the exact self-attestation limitation.

Against subject `a8cc777e...`:

1. EventStore-owner comment
   [5409145568](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5409145568),
   created at `2026-08-25T10:33:29Z`.
2. Release-owner comment
   [5409148235](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5409148235),
   created at `2026-08-25T10:33:45Z`.
3. Test Architect `bmad:murat` self-attested record, accepted at `2026-08-25T10:34:41Z`.

Against subject `86c59c79...`:

1. EventStore-owner comment
   [5550273078](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5550273078).
2. Release-owner comment
   [5550277712](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5550277712).
3. Test Architect `bmad:murat` self-attested record.

The first timestamp attempt for each owner role in that round crossed GitHub's second boundary.
Comments
[5409140199](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5409140199) and
[5409147909](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5409147909) were
immediately marked `SUPERSEDED -- INVALID TIMESTAMP-MISMATCH ATTEMPT` and were not retained as packet
sources.

**Identity caveats:** the owner-role registry maps both `eventstore-owner` and `release-owner` to
`github:jpiquot`; the `test-architect` receipt is a self-attested `bmad-test-architect-record`
without independent external authentication; and every receipt is composed by repository tooling and
posted with the rostered role holder's credential rather than typed by hand -- the exact-second
agreement between `accepted_at` and GitHub's server-assigned `created_at` cannot be produced by
hand. The Test Architect and tooling caveats are required limitations repeated in the receipts;
the shared-owner-account mapping is subject-bound through the role registry, not repeated as a
receipt limitation. A 3-of-3 result is therefore two roster-bound owner roles held by one
authenticated human plus a self-authored BMAD record, not independent three-party review.

**Known wording mismatch:** the retained roster comment `5407975180` names the ratified artifact
`reviewer-roster.json`, wording copy-carried from Story 3.13, while the packet retains
`registry/owner-role-registry.json`. The reference is understood to mean that file. The body is
exact-match-required, so correcting it would need a new owner comment on `#352` plus another
re-mint; the mismatch is recorded here instead.

This record supplies evidence only. It never authorizes deployment, publication, registry mutation,
consumer removal, or predecessor changes.

### DW-508 sign-off accounting

Under review decision D2, the owner chose to count these records toward the DW-508 sign-off
requirement. Their scopes are narrower than dedicated trust-path attestations:

| Role | Record counted under D2 | Scope of that record |
| --- | --- | --- |
| Architecture | The Story 3.15 spec's 2026-09-23 Change Log statement that architecture sign-off passed. | A narrative result; no dedicated architecture review of the corrected trust path is retained. |
| Test | The current subject-bound, self-attested `bmad:murat` Test Architect receipt under `acceptances/7d64f87e.../test-architect.json`. | Acceptance of the parity subject; no separate trust-path-test attestation is retained. |
| Security | The four-layer 2026-09-23 code review of receipt-collection commit `a2f5cba2`, recorded in the spec's Review Findings. | Code review findings and triage; no dedicated Story 3.15 Security Reviewer or trust-path security attestation is retained. |

The Story 4.15 v4 security and test review files bind Story 4.15's subject and do not serve as
Story 3.15 attestations.

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
| Technical inventory | `fec6deccc686e4abe83987da16c3935e3e688fa585aa7c5575dead54b4d97611` (24 files) |
| Owner-role registry | `aee4f46be8208ea13704a38d9329320b8a7641b0cdd33e61a138114c8c142f2f` |
| Bound producers | capture and assembler digests in `closure.json` `dispatch` |

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
to register arm64 emulation before executing the immutable child. That registration is host state,
not an input byte the packet can hash, so it is recorded as a documented environmental prerequisite
in `tools/capture-corrected-deployed-runtime-parity-smokes.py` rather than bound into the subject.

The retained smoke bytes are timestamped `2026-08-21T19:24-19:26`, so they were produced by the
capture tool as it stood before the loop-6 hardening. Both producers are now bound in the closure
`dispatch` block, so any further producer edit re-mints the subject; the retained evidence was
deliberately bound rather than re-captured, because re-capturing would replace evidence rather than
bind it.

## Acceptance gate

The role registry retains the owner-ratified mappings:

- `eventstore-owner` -> `github:jpiquot`
- `release-owner` -> `github:jpiquot`
- `test-architect` -> `bmad:murat`

Three current acceptances bind subject `7d64f87e...`; the retained verifier validates all three.
The four superseded rounds are listed above and authorize nothing here.

No planning approval, release authority, prior receipt, label, tag, self-declared role, or synthetic
test fixture is treated as a Story 3.15 acceptance. The two owner receipts require retained GitHub
issue-comment sources whose exact JSON bodies accept this subject, whose envelopes are closed-schema,
and all of whose `id`, `url`, `html_url` anchor and `issue_url` fields resolve to one comment on
issue `#352`. The Test Architect receipt requires a retained `bmad:murat` source record. All three
receipts must repeat the exact scope, limitations, decision, identity, role, subject digest, and
acceptance time. Any subject change invalidates all of them.

## Authority boundary

An auditor checking this packet must confirm four flags in `closure.json` are all `false`, and they
are:

| Flag | Value |
| --- | --- |
| `deployment_authorized` | `false` |
| `publication_authorized` | `false` |
| `consumer_removal_authorized` | `false` |
| `grants_mutation_authority` | `false` |
| `deployed_runtime_parity` | `available` -- **claim only**, granted at 3 of 3 |
| `selected_deployed_identity` | the index digest -- **claim only**, granted at 3 of 3 |

A positive parity verdict is evidence that the deployed runtime matches the corrective release. It
is not permission to deploy it, to publish or recover packages, to mutate the registry, to remove a
consumer, or to change the frozen predecessor packet. Each of those needs its own separately
authorized owner action.

## Why the receipts sit outside the technical inventory

The canonical subject is a hash of the technical evidence plus the decision and the registry. If
receipts were part of the technical inventory the subject would hash bytes that themselves cite the
subject -- a cycle no assembler could resolve. So receipts live under
`acceptances/<subject-sha256>/`, addressed *by* the subject, and are close-listed separately by the
receipt check while the inventory sweep rejects any acceptance tree that is not the bound one. The
trust chain therefore reads: pinned verifier bytes -> canonical subject over technical evidence,
decision, registry and producer digests -> receipts addressed by that subject.

## Commands and results

- Story 3.14 predecessor validation: pass, exact digest `4d1a0c33…`.
- Contracts Release/package-mode build: pass, zero warnings and errors.
- 2026-09-23 focused closure and predecessor/provenance suites: 268 passed before receipt collection,
  zero failed or skipped. The post-collection closure class passed 213/213 before the additional
  in-clone assembler path regression; the spec verification section records those historical runs.
  The positive closure case uses explicit
  synthetic test fixtures only to mutation-prove the receipt contract; those fixtures are never
  copied into the retained packet.
- Complete Contracts suite after the `review` tracker transition and in-clone path regression:
  **2106 passed, zero failed, skipped, or unrun**. The Contracts test project Release build passed
  with zero warnings and errors.
- `sprint-status.yaml` and `docs/ci.md` name the current subject and are drift-guarded by the
  focused suite. The guide's final positive verdict is bound by separately reviewed Story 4.15 v4
  subject `8a59c89c276e0958f2066dfe8173d15ace2df6df2efb0120f6589c0ce20809b5`;
  its default validator passes.
- Checked-in Story 3.15 assembler and verifier: **pass at three of three receipts**, exit 0;
  subject `7d64f87e...`; selected OCI index only; all non-authority flags false.
- `git diff --check`: no whitespace errors reported.

## Rerun trigger

Rebuild the complete subject and reject all prior receipts after any predecessor, package, OCI,
Production-smoke, inventory, registry, verifier, decision, or receipt-source policy change. An
individual source replacement invalidates its bound receipt and any complete verdict without
re-minting the subject. Do not edit an old receipt or move it to a new subject directory.
