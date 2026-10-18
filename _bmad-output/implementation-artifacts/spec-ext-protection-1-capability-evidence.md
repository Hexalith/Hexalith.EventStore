---
title: EXT-PROTECTION-1 capability and original receipt evidence
created: 2026-10-18
status: in-progress
route: dispatch
baseline_commit: c0c08d450dff89faaeea2bb88dd570a589ce203d
context:
  - AGENTS.md
  - references/Hexalith.AI.Tools/hexalith-llm-instructions.md
  - docs/implementation/ext-protection-1-owner-acceptance.md
  - docs/implementation/ext-protection-1-verifier-protocol.md
  - ../agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - ../agents/_bmad-output/implementation-artifacts/story-5-4-dependency-reconciliation-2026-10-18.md
---

<frozen-after-approval reason="continuation explicitly authorizes independent work while a genuinely missing provider decision is prepared; no intermediate approval">

## Intent

Continue EXT-PROTECTION-1 from the stated baseline. Investigation establishes that G1 has no selected production DEK lifecycle/atomic provider: the installed OpenBao binding is HMAC only and explicitly lacks all-copy qualification. Complete independent G6 capability and original receipt validation while the parent prepares the provider decision in the owning Platform repository. This subordinate implementation scope does not close G1, G2, G3, G5 or the entire G6 contract.

## Boundaries & Constraints

Preserve unrelated work. No Live, provisioning, Agents execution/register edits, dependency updates, Git mutations or availability claims. No new custody interfaces pretending to implement a provider. Python tests may generate ephemeral offline P-256 keys only. Installed profile anchors are public references and hashes; no private values. Keep unchanged plan cases and byte hash. Source/observer signatures do not establish online authority or irreversible destruction. Do not change the parent acceptance packet or original spec; the parent owns those.

## I/O & Edge-Case Matrix

| Input | Expected behavior |
| --- | --- |
| Complete original request, canonical closed payload, valid detached ES256 against separate pinned tenant/version trust | Cryptographic validation succeeds; raw original evidence is then checked |
| Header, payload, issuer, audience, tenant, key, anchor or signature substitution | Reject before qualification can pass |
| Bool/fraction/overflow ordinal, duplicate member, padded/noncanonical base64url, unknown payload/header field | Reject |
| Absent or changed issue/dispatch receipt, wrong batch/target/revision/signing identity | Reject correlation |
| Provider and owner consumed result with different original target receipt vectors | Reject; labels such as irreversible:true cannot replace native receipt identity |
| Changed/missing accepted public anchor | Block, including drift before a later role call |

</frozen-after-approval>

## Code Map

- `tools/ext_protection_1.py`: reference-only profile/v1 currently pins transport anchors only. `evaluate` trusts naked capabilities, guard booleans and fictional irreversible receipt fields. Add separate capability trust and use native original records. `execute` already enforces actual unseal equality; preserve it and recovery journals.
- `tools/tests/test_ext_protection_1.py`: offline only. Preserve all existing cases; add real cryptographic and receipt contradiction tests.
- `src/Hexalith.EventStore.Contracts/Security/DeletionBatchCapabilityIdentity.cs`: authoritative fourteen-field RFC8785 bytes, SigningRequestId uppercase SHA256, target digest uppercase SHA256 of default System.Text.Json serialized string arrays. Establish cross-language exact-byte vectors, including Unicode/HTML escaping, rather than assuming Python JSON parity.
- `DeletionBatchConsumptionRequest.cs`: Capability, DetachedJws, CommittedIssuedGuardRevision, DispatchReceiptId, DispatchGuardRevision, Targets.
- `GovernanceBatchState.cs`, `GovernanceProtocolReceipt.cs`: original capability/JWS/signing request, issue/dispatch receipt ids and actual successful revisions. Inspect reducer for actual issue versus dispatch field presence; do not invent fields absent from real records.
- `DeletionManifestProviderResult.cs`, `DeletionConsumptionResult.cs`, `DeletionTargetReceipt.cs`: native tenant/batch/reservation/status and complete ordered Target/OriginalBatchId/ReceiptId. Bind exact results to this vector.
- `../platform/src/Hexalith.Platform.Custody/DeletionBatchCapabilityCodec.cs` and `DetachedEs256JwsCore.cs`: exact candidate header: alg ES256, anchorVersion, keyVersion, kid, typ DeletionBatchCapabilityV1; signing input header.base64url(canonical payload), 64-byte P1363 signature; detached middle segment empty. Read as ordinary source only.

## Tasks & Acceptance

- [ ] `tools/ext_protection_1.py` and a focused helper if needed: require profile/v2 capabilityTrust records binding issuer/audience/tenant/family/key version, public anchor id/version/file/hash and trust revision. Retain old versions without capability expiry. Validate exact references and P-256; prevent transport-key substitution. Recheck all trust pins with each role call. Update binding digest through the accepted profile hash; no installation invented.
- [ ] Validate actual consumptionRequests with strict payload types, canonical bytes, exact sorted full targets and native manifest digest; verify detached ES256 against exactly one separately accepted trust entry.
- [ ] Reconstruct persisted batch/issue/dispatch receipt correlation from real contract fields, including signature digest and successful issue versus signed intended revision. Reject missing, ambiguous, substituted or inconsistent evidence. Document that currentness and linearizable reads still require installed authorities.
- [ ] Replace fictional irreversible flags with native ordered receipts correlated across consumed owner/provider and exact retry/lookup terminal originals. Keep authenticated physical provenance and nonrollback as blockers.
- [ ] Extend offline tests across the matrix. Include a nontrivial passing signed request and native records, plus rejection mutations. Run narrow Python checks and meaningful cross-language canonical/ES256 integration; do not run Live.
- [ ] Update only the verifier protocol document with exact schema and remaining limitations. Report commands/results and file map to parent.

Acceptance: Given a valid separately trusted signature and consistent original records, offline validation passes. Given any listed substitution, it fails. No success establishes physical qualification or permits a cohort run.

## Implementation Notes

Provider investigation evidence: installed runtime reference SHA256 31c28a9d1dd21eed8ca4d0c421088cb946b262c624ba461d0795cab92cd1feb3; mapping SHA256 f4e650995f0b35882377e01fb4f30b3944b018aec136e16496b6d349dd595ffe. Both explicitly lack lifecycle/nonrollback enrollment. The parent is preparing the concrete provider decision and acceptance mapping; these records are not code authorities for signature trust.

## Spec Change Log

## Review Triage Log

## Verification

Run `python3 -m unittest discover -s tools/tests -p test_ext_protection_1.py -v`, additional helper tests if separate, `python3 tools/verify-ext-protection-1.py --mode Source`, and `git diff --check`. Retain cross-language golden evidence. Builds, if needed, use Debug and existing project references; no package changes.
