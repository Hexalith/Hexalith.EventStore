# EXT-PROTECTION-1 resumed owner delivery — 2026-10-25

**BlockedNotSubmitted. G1–G7 remain open.** This continuation corrects snapshot target ownership and incorporates the latest Platform source handback. Complete production delivery still requires missing owner implementations, actual installations and independent all-copy destruction/restore evidence. The full [register contract](../../../agents/_bmad-output/planning-artifacts/external-dependency-register.md), including v21/v23, remains authoritative. The [October 25 reconciliation](../../../agents/_bmad-output/implementation-artifacts/story-5-4-dependency-reconciliation-2026-10-25.md) and its [protection report](../../../agents/_bmad-output/implementation-artifacts/tests/story-5-4-reconciliation-2026-10-25/protection-owner-packets.md) were read in full.

The [new machine packet](ext-protection-1-delivery-2026-10-25-resumed.json) preserves all nine exact register fields and the complete protection section. It repairs the missing closing backtick in the prior packet's `ConsumingStories` transcription. The [original October 25 JSON](ext-protection-1-delivery-2026-10-25.json), [report](ext-protection-1-delivery-2026-10-25.md), failed attempts and original restore evidence remain unchanged. The full-delivery spec stays `in-progress`; none of its full-delivery tasks is marked complete.

## Commitment and execution boundary

| Field | Current delivery |
| --- | --- |
| Owner / repository | EventStore Maintainer / Hexalith.EventStore, with Platform's independently owned custody and authority prerequisites. |
| Required artifact | Entire registered production field engine, six-operation custody, all-or-none manifest protocol, v21/v23 current authority and exact-original recovery, complete copy/holder closure, snapshots/cache and installed qualification. |
| Complete canonical target | **Unresolved; null.** EventStore source was observed at `7012502bc094484eb2d4687badb98cb3b5e52b73`; this continuation adds uncommitted source. Platform sibling observation was `90aef9ac3e813d9cae5b16d68ca5e326ce0fb599`; root reference was `7c56ad6352a74edec62450668e106ec8d6011e40`. Fresh full Git observations are retained in the machine packet. |
| Explicit integration date | Existing proposal **2026-11-02**, unaccepted. Accepted date is null; installed qualification remains `NotSchedulable` while prerequisites are unresolved. |
| Compatibility command | Exact candidate below; installation and acceptance prerequisites remain unmet. |
| Evidence level / status | Levels 4 and 5 required; current verification is offline component evidence. Register stays `Uncommitted`; dependency availability and qualification remain false. |
| Consumers | 5.6, 5.8, 6.1–6.4, 6.8, 7.1–7.4, 7.7, 8.1–8.3, 8.8; `RQ-1`. |

Candidate executable owner command, from the Agents repository root, retained without execution:

```bash
python3 ../eventstore/tools/verify-ext-protection-1.py --mode Live --profile /home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json --evidence-root /home/administrator/hexalith-protection-evidence/ext-protection-1/attempts
```

This entrypoint has an input-dependent success path. It requires the accepted complete immutable clean checkout, explicit accepted integration date, actual installed closed profile and `ProtectionProfileSha256=<actual raw profile-file SHA-256>` in the compatibility record. The named profile remains absent. Invoking Live would create an attempt directory before refusal, so it was not used as a diagnostic. Original-operation recovery remains distinct from a passing qualification run.

## Snapshot correction and retained source corrections

[InteractionPayloadProtectionService](../../src/Hexalith.EventStore.PayloadProtection/InteractionPayloadProtectionService.cs) now validates serialized snapshot targets before consulting bindings, registry or keys. The bounded [target reader](../../src/Hexalith.EventStore.PayloadProtection/SnapshotProtectionTargetReader.cs) recognizes the existing closed `ProtectedContent` shape under nested members such as `Request` and `Output`, arrays and complete target references. Supported whole snapshots must constrain all explicit content to the same tenant/interaction/alias. That explicit target constrains the independently retained binding; it grants no enrollment authority.

Different interaction, alias or tenant targets and malformed/ambiguous reserved shapes refuse before effects. Historical authenticated ciphertext is checked after decryption and before returning deserialized readable state. Ordinary unrelated `Target`/`Value` domain fields retain compatibility. Actual ciphertext, exact retry/completion identity, cancellation, zeroed owned buffers and SnapshotManager committed-state/no-staging behavior are covered offline. **A multi-root directory snapshot format, completion representation and selective replay remain unsupported; those snapshots now refuse. G4 stays open.**

The app-channel token reuse correction at `d80cbf4d1f7948f4ef39d06b3846fd221bc0ef8a` and fixture correction at `03ef9f156c5590a81c5409fde601952ebfb58c21` are included in the starting source. Their 13 affected files are hash-checked unchanged. No default DI, plan, golden, tracked lock, package pin, dependency pointer, Agents register or Platform source was changed by this continuation.

## G1–G7 delivery assessment with Platform

The newer [reviewed Platform handback](../../../platform/docs/implementation/custody-lifecycle-foundation-2026-10-25/REVIEWED-DELIVERY.md) supplies `CustodyLifecycleClient`, public Dapr lifecycle/authority adapters and `DaprAtomicDeletionManifestProvider`. Older descriptions that those exact source clients are absent are superseded. Its independent issuers, receiving host, original locators, installation and physical qualification remain incomplete.

| Gap | Remaining work |
| --- | --- |
| G1 — Open | Install and qualify actual wrap/unwrap/pin/unpin/per-key/manifest custody; connect occurrence key delivery and complete original registrations. Require effective hold pins and complete atomic native effects with authenticated original vectors. |
| G2 — Open | Supply independent private/recovery and lifecycle authorizer services, durable `RegisteredDispatchBlocked` hold/dispatch reservations, current guard/trust/revocation, private registrar and independently surviving genesis/enrollment. The existing source bridge keeps owner, guard and custody revisions separate. |
| G3 — Open | Produce actual complete copy/holder/producer inventories, independent placement/restore cut, native retirement/deletion, sanitation/termination, survivor recovery and non-revival evidence. Schema members and local buffer disposal establish none of those installed facts. |
| G4 — Open | Deliver multi-root snapshot/completion representation and selective replay; compose actual retained occurrence/completion/erasure bindings, writer/anchor/scheduler and source-plus-target snapshot/cache/restore qualification. The single-target source safety correction above is complete. |
| G5 — Open | Install distinct executor, read-only persisted-state observer and enrollment programs, ACLs and public anchors against the same enrolled store/deployment. Supply actual engine/role/manifest pins, fixture drivers and one-attempt authority. |
| G6 — Open | Qualify current online sole-attestation/dispatch authority, complete authenticated append inventory, original physical receipt vectors and restore provenance. Existing native ES256/signing/no-issue/intent/receipt consistency and durable original recovery checks remain offline component evidence. |
| G7 — Open | Return the complete coordinated canonical source/package target, actual installed profile/engine/role/trust hashes and accepted integration date. Earlier package-lane NU1102 remains preserved historical evidence; package availability was not rechecked here. |

Two concrete source seams remain missing:

- `ICustodyKeyRegistrationSource.FindAsync(CustodyKeyObjectIdentity, token)` must retain the entire tenant/object/purpose/alias/key/lifecycle/store/contract identity and original wrap operation, actor expected revision, original KEK version, opaque wrapped-key reference and wrap receipt. Each use must authenticate the independent original.
- `ICustodyReservedManifestSource.FindAsync(reservationReceiptId, token)` must return the unchanged complete signed `DeletionBatchConsumptionRequest`, including capability/JWS, committed issue revision, dispatch receipt/revision and sorted targets. Existing owner `LookupAsync` returns an outcome and can resume reserved recovery; it supplies neither a read-only observer nor that complete receipt-to-original mapping. A reconstructed singleton, batch hint or replacement signature is insufficient.

The [current Platform ledger](../../../platform/docs/implementation/custody-lifecycle-foundation-2026-10-25/installation-input-ledger-after-review.json) retains **68 distinct missing inputs**, each with an owner and next action. Its [expanded required profile members](../../../platform/docs/implementation/custody-lifecycle-foundation-2026-10-25/reference-profile-inputs.json) and [missing source dispositions](../../../platform/docs/implementation/custody-lifecycle-foundation-2026-10-25/source-dispositions.json) are copied into the new packet. Required member names remain distinct from an actual copy inventory. No backend adoption, approval, credential or physical proof was inferred from those files.

## Actual hashes and retained evidence

| Input | Actual current binding |
| --- | --- |
| Accepted protection profile | File absent; SHA-256 null. |
| Installed engine | No accepted installed binary/reference; hash null. Local Debug binary hashes are recorded separately as component artifacts. |
| Installed executor / observer / enrollment | Executables, public anchors, credential references, identities and installation-manifest hashes remain null. |
| Installed capability trust | No accepted issuer/audience/tenant/key/anchor/revision inventory; hash null. |
| Raw 41-case plan | `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c`; unchanged and unqualified. |
| Public HMAC runtime reference | `31c28a9d1dd21eed8ca4d0c421088cb946b262c624ba461d0795cab92cd1feb3`; 51 null fields remain. This is the HMAC installation reference. |
| Public key mappings | `f4e650995f0b35882377e01fb4f30b3944b018aec136e16496b6d349dd595ffe`; ordinary snapshot semantics, lifecycle/all-copy unqualified. |
| Reviewed Platform missing-input ledger | `36f0b4862233365128fdc93539fac4bb00f2d10cf9fa07aa3c47752a5c353192`; evidence-file hash. |
| Retained OpenBao restore counterexample | `6efa381bc70b3de57a24142e5e679f53781c7f89d5ab6740fb7664966f30888e`; `ObservedRevival`, `g1Qualified:false`; unchanged, never rerun. |

Evidence is retained in `/home/administrator/hexalith-protection-evidence/ext-protection-1/resume-20261025T111935Z-7229ca34/`. The exact argv/cwd/results and log hashes include the initial `NETSDK1004` missing-assets failure and all successful replacements. Source-graph restores route generated locks outside the worktree. `verification-manifest.json` and `source-inventory-final.json` bind current results and component artifacts separately from absent installed hashes. The original packet, 234 indexed historical evidence files, frozen intent, baseline and 13 correction files are checked for preservation.

| Current check | Result |
| --- | --- |
| Complete PayloadProtection test assembly | 876 passed; zero failures, errors, skips or unrun tests. Includes the current fixture corrections. |
| Four affected Server classes: actual hook, persistence, actor recovery and no-op hook | 80 passed on the final rebuilt source; zero failures, errors, skips or unrun tests. |
| App-channel token builder class | 7 passed; no topology/resources started by these tests. |
| Offline verifier and actual C# native interoperability modules | 66 passed; disposable offline signing keys only. |
| Final three Debug/project-reference test-project builds and native probe build | Zero warnings/errors. |
| Source entrypoint | Exit 0; 41 planned cases, unchanged plan hash, `liveExecuted:false`. |

These are **1,029 passing offline/component tests**, counting each final selection once. Parent diff inspection caught an overbroad `Target`/`Value` interpretation; the implementation was narrowed and genuine ordinary-domain compatibility cases passed. Full owner acceptance, its installed matrix and the full build workflow remain incomplete.

The local development AppHost reused existing binaries, started, reported application resources waiting, and stopped successfully before source work. It establishes no protection installation. No owner Live/Full, cohort, consumer Agents path, new physical experiment, native provisioning/destruction, enrollment or spending ran. No incomplete acceptance is requested; the OQ-5.4-1 prerequisites, phase order and retained single-attempt/original-journal rules remain effective.
