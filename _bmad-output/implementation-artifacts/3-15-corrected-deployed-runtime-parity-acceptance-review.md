# Story 3.15 acceptance review brief — current subject

**Review completed (2026-09-26).** This document is not an acceptance receipt. The
[new-subject owner review request](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5844480896)
was posted after user approval and separate authorization. The Test Architect has issued a local
self-attested decision report, and all three current-subject role receipts now bind the packet.
The retained verifier exits 0 at 3/3 and selects the pinned OCI index for bounded parity evidence.

A [validation request for former subject `c98fdef2...` was posted on issue `#352`](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5844166955).
It records the former Test Architect decline and is historical, not a request or acceptance for
this new subject. The new-subject request led to separate EventStore-owner and Release-owner decisions.

## Exact decision input

- Subject SHA-256: `66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6` (`subject.json` hashes to this value).
- Packet: `evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/` (relative to this artifact directory). Review its `subject.json`, `closure.json`, `technical-sha256.txt`, and retained raw evidence.
- Corrective release: `v3.96.2`, source `f343bb0153e9cdcb8b12ec10153813072f5ad38d`; predecessor identity SHA-256 `4d1a0c336397e971bf10001095d5e427dd03c499ee428a3121a913926da8c4a9`.
- OCI index selected for bounded parity evidence: `registry.hexalith.com/eventstore@sha256:4b1410852b11be3bcaebf8f2e6277c1d30ce13a19f48cf0df86ed93646d709c3`. The claim passed all three receipts and the retained verifier.
- Closed technical inventory: SHA-256 `32dc22facf833c665037738c3fe7d8b1ab0fb3f73f7e28fb175ef397f5e9fb84`, 24 retained files. Owner-role registry: SHA-256 `aee4f46be8208ea13704a38d9329320b8a7641b0cdd33e61a138114c8c142f2f`.
- A fresh 2026-09-26 two-platform Production capture with the bound `curl -q` producer and corrected limitation 4 re-minted this subject. The receipt-free `c98fdef2…` packet is preserved in `superseded-packets/`; all three older `7d64f87e…` receipts remain superseded and cannot be reused. The current receipt address is `acceptances/66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6/` and contains exactly three receipts and their sources.

The subject binds these four limitations verbatim. Each role should consider them in its own decision:

1. “This packet supplies immutable deployed-runtime parity evidence only.”
2. “It authorizes no deployment, publication, registry mutation, consumer removal, or predecessor change.”
3. “The Test Architect acceptance is a self-attested BMAD record without independent external authentication.”
4. “The two owner acceptance comments are composed by repository tooling and posted with the rostered role holder's credential, rather than typed by hand; the Test Architect source is a local self-attested BMAD record.”

The two owner roles map to the same rostered GitHub identity, `github:jpiquot`; the Test Architect maps to `bmad:murat`. The packet's `deployment_authorized`, `publication_authorized`, `consumer_removal_authorized`, and `grants_mutation_authority` fields are all `false`.

## EventStore owner — review material

**Role and source contract:** `eventstore-owner`, `github:jpiquot`, authenticated GitHub issue `#352` comment. Review the exact `subject.json` and the [proof packet](3-15-corrected-deployed-runtime-parity-closure-proof-packet.md), especially predecessor/source lineage, the independently retained 14 public packages, two OCI children, and the four non-authority flags. Decide whether that evidence supports the stated *deployed-runtime parity evidence* scope for this subject. A new role-specific decision must bind the exact subject and all four limitations; the earlier issue comment `5789893766` binds `7d64f87e…` and is historical only.

Reviewed canonical comment template. The actual [EventStore-owner acceptance](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5844573563) was posted at `2026-09-26T08:22:25Z`, with matching GitHub `created_at` and `updated_at`:

```json
{"accepted_at":"<GitHub created_at UTC second>","accepted_limitations":["This packet supplies immutable deployed-runtime parity evidence only.","It authorizes no deployment, publication, registry mutation, consumer removal, or predecessor change.","The Test Architect acceptance is a self-attested BMAD record without independent external authentication.","The two owner acceptance comments are composed by repository tooling and posted with the rostered role holder's credential, rather than typed by hand; the Test Architect source is a local self-attested BMAD record."],"accepted_scope":"Story 3.15 corrected deployed-runtime parity for 66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6","decision":"accepted","reviewer_identity":"github:jpiquot","role":"eventstore-owner","schema":"hexalith.eventstore.deployed-runtime-parity-acceptance.v1","subject_sha256":"66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6"}
```

## Release owner — review material

**Role and source contract:** `release-owner`, `github:jpiquot`, a distinct authenticated issue `#352` comment. Inspect the immutable `v3.96.2` release/workflow lineage, the 14 NuGet.org archive identities versus distinct GitHub release-asset bytes, the raw OCI index/child/config chain, and the claimed index digest. Decide whether that chain is sufficient for parity evidence under the four limitations. This decision does not grant publication, deployment, or registry mutation. The earlier issue comment `5789897143` binds `7d64f87e…` and is historical only.

Reviewed canonical comment template. The actual [Release-owner acceptance](https://github.com/Hexalith/Hexalith.EventStore/issues/352#issuecomment-5844574016) was posted at `2026-09-26T08:22:30Z`, with matching GitHub `created_at` and `updated_at`:

```json
{"accepted_at":"<GitHub created_at UTC second>","accepted_limitations":["This packet supplies immutable deployed-runtime parity evidence only.","It authorizes no deployment, publication, registry mutation, consumer removal, or predecessor change.","The Test Architect acceptance is a self-attested BMAD record without independent external authentication.","The two owner acceptance comments are composed by repository tooling and posted with the rostered role holder's credential, rather than typed by hand; the Test Architect source is a local self-attested BMAD record."],"accepted_scope":"Story 3.15 corrected deployed-runtime parity for 66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6","decision":"accepted","reviewer_identity":"github:jpiquot","role":"release-owner","schema":"hexalith.eventstore.deployed-runtime-parity-acceptance.v1","subject_sha256":"66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6"}
```

## Test Architect — review material

**Role and source contract:** `test-architect`, `bmad:murat`, a new subject-bound self-attested BMAD record. Inspect both `linux/amd64` and `linux/arm64` Production `/alive` smoke results and logs, bounded execution and cleanup, the curl configuration isolation change, and the focused fail-closed mutation suite. Decide whether the coverage supports the exact subject while acknowledging the lack of independent external authentication of this role. The prior self-attested record binds `7d64f87e…` and is historical only.

**Independent decision for this subject: ACCEPT technical evidence.** The
[fresh Test Architect decision](3-15-test-architect-decision-66be1b4a.md) accepts the exact
`66be1b4a...` subject with its four limitations after checking the predecessor, 24-file inventory,
current tool pins, two fresh Production smokes, and focused tests. It is a local self-attested
decision report without independent external authentication; that report alone was not a canonical
packet source or receipt. Its acceptance was later transcribed into the local self-attested source
and receipt, and the retained verifier now passes at 3/3. The previous Test Architect decision declined
`c98fdef2...` because its August smoke observations predated `curl -q` and limitation 4 called
every receipt credential-posted. The new smoke observations ran on 2026-09-26 from
`07:29:14.107654Z` through `07:30:34.182021Z` against the same immutable child digests. Both
platforms returned HTTP 200 with zero redirects and passed cleanup. The bound producer places
`-q` first, and limitation 4 now distinguishes the owner comments from the local Test Architect
source. The fresh reviewer found that evidence sufficient for the bounded technical scope.

Focused pre-collection checks on 2026-09-26: the Story 3.14 predecessor verifier passed at identity
`4d1a0c33…`; all 24 Story 3.15 technical-inventory checksums matched; the Story 3.15 verifier
refused closure at 0/3. The Release build had zero warnings or errors, and the built Contracts
test executable passed 235/235 across `*CorrectedDeployedRuntimeParityClosureTests` and
`*CorrectedDeployedRuntimeParitySmokeCaptureTests`, with no skipped cases. These tests include
synthetic receipt cases; they do not supply a real acceptance for the current subject.
After receipt collection, the Release Contracts test project built without warnings or errors;
the two focused Story 3.15 classes passed 235/235 and the guarded lifecycle test passed 1/1.

The independent decision above supplies the review judgment. The following reviewed template
was used for the canonical `test-architect` source and receipt, issued at
`2026-09-26T08:23:47Z`. This is the source-creation second, later than the decision report's
07:39 UTC minute; it does not claim a new externally authenticated decision. Any further bound
evidence or limitation change would create another subject and require another decision:

```json
{"accepted_at":"<actual decision time UTC second>","accepted_limitations":["This packet supplies immutable deployed-runtime parity evidence only.","It authorizes no deployment, publication, registry mutation, consumer removal, or predecessor change.","The Test Architect acceptance is a self-attested BMAD record without independent external authentication.","The two owner acceptance comments are composed by repository tooling and posted with the rostered role holder's credential, rather than typed by hand; the Test Architect source is a local self-attested BMAD record."],"accepted_scope":"Story 3.15 corrected deployed-runtime parity for 66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6","decision":"accepted","reviewer_identity":"bmad:murat","role":"test-architect","schema":"hexalith.eventstore.deployed-runtime-parity-acceptance.v1","subject_sha256":"66be1b4a23d377db6af3cdae3972bc94fbd2fa8e44d180be1a1ff86a222ea9b6"}
```

These reviewed templates preceded the final receipts. Each owner made a separate decision and has a distinct GitHub comment from the rostered account. Tooling composed the compact JSON and retained the closed-schema envelopes with `created_at == updated_at == accepted_at`; no timestamp-mismatched attempt occurred for this subject. The Test Architect source separately contains the accepted self-attested `bmad:murat` record. The assembler bound all three durable sources and the verifier passed.

## Known evidence limits for all reviewers

Retained GitHub comment envelopes are checked for internal consistency but are neither fetched live nor independently signed by the verifier. The NuGet.org archive bytes are content-checked against the retained package mapping, while their derived download URLs have no retained service-response attestation. The proof packet and the Story 3.15 deferred-work entries record these accepted limits. Each role may decline acceptance if they require stronger evidence.

## Verification and handoff boundary

From the repository root, the read-only verifier now exits 0 and selects the pinned index:

```sh
python3 tools/validate-corrected-deployed-runtime-parity.py \
    _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d/closure.json \
    --packet-root _bmad-output/implementation-artifacts/evidence/story-3-15/f343bb0153e9cdcb8b12ec10153813072f5ad38d
```

The owner sources and self-attested Test Architect source are retained under the current acceptance address. The assembler and pinned verifier pass at 3/3. Any changed predecessor, package, OCI, Production smoke, inventory, registry, verifier, decision, or receipt-source policy requires a new subject and fresh decisions. This evidence grants no operational authority. Story 4.15 OQ8 seal reconciliation is a separate open gate recorded in `deferred-work.md`.
