# EXT-PROTECTION-1 owner checkpoint — 2026-10-29

**BlockedNotSubmitted.** This continuation corrects authenticated historical mixed-root snapshot reads and adopts the current Platform source handback. Full owner delivery remains in progress. The [register](../../../agents/_bmad-output/planning-artifacts/external-dependency-register.md), including v21/v23, remains authoritative. The [machine packet](ext-protection-1-delivery-2026-10-29.json) preserves its exact nine commitment fields and complete contract; the [October 28 report](ext-protection-1-delivery-2026-10-28.md) and [packet](ext-protection-1-delivery-2026-10-28.json) remain historical evidence.

Owner/repository remain EventStore Maintainer / Hexalith.EventStore. Complete immutable EventStore and Platform targets, accepted integration date, accepted profile and installed engine/role/trust bindings remain null. **2026-11-02** remains an unaccepted proposal. Dependency status remains Uncommitted, availability and qualification false, and evidence Levels 4 and 5 remain required. No incomplete owner record is submitted for acceptance.

## Source correction

The mixed-root reader previously authenticated each retained ciphertext and its container digest, then returned its decrypted value without reapplying the writer's ownership rules. A historical fragment could therefore contain a foreign interaction, alias or tenant, another protected envelope, or malformed reserved content and still become readable.

[InteractionSnapshotEntriesProtector](../../src/Hexalith.EventStore.PayloadProtection/InteractionSnapshotEntriesProtector.cs) now validates the bounded reconstructed state before deserialization and requires its content-free skeleton and complete entry manifest to equal the retained originals. Invalid historical ownership returns unreadable data for both external reads and trusted replay. Existing ciphertext, identities, source enrollment and completion protocols are unchanged.

The [new tests](../../tests/Hexalith.EventStore.PayloadProtection.Tests/InteractionMixedSnapshotProtectionTests.cs) use real occurrence encryption with separately retained synthetic custody/registry authorities. Six authenticated historical-value cases reproduced the incorrect readable result before correction. Ten final cases cover those denials, unchanged original ciphertext/completion on retry, ordinary domain Target/Value compatibility, owned-buffer zeroing, and cached partial typed-Erased replay: external reading stays unreadable, while trusted replay preserves the surviving interaction. These fixtures do not establish installed custody or physical erasure.

## Current handback and full-contract audit

Platform's [October 29 handback](../../../platform/docs/implementation/ext-protection-1-erc-component-delivery-2026-10-29.md) and [bound evidence](../../../platform/docs/implementation/ext-protection-1-erc-final-source-2026-10-29/README.md) supersede older missing-source descriptions. They supply ERC custody, exact original/authority/receiving/host composition, mixed-root entry support and three qualification-role programs, with mappings for all 41 cases. The entire installed 41-case run remains unexecuted. No complete installed pass path is inferred from those mappings.

| Frozen matrix row | Source/component evidence | Remaining acceptance proof |
| --- | --- | --- |
| Protect | Existing field engine and occurrence codecs; current full PayloadProtection suite. Platform supplies occurrence/source/writer composition. | Installed exact-root outbox/event protection and complete copy inventory: G1/G4/G5. |
| Read/distribute | Existing sealed distribution/trusted replay; current single/mixed-root tests, including historical rejection and partial Erased cache replay. | Installed source/target/event/snapshot/cache/restore behavior and all-copy closure: G3/G4/G5. |
| Custody | Platform ERC lifecycle, key delivery, pin and native-store source plus retained component/native evidence. | Independently qualified six-operation custody, holder/copy census and physical non-revival: G1/G2/G3. |
| Manifest | EventStore owner/target index/originals; Platform atomic manifest/provider and retained ordered-vector component evidence. | Installed all-or-none effects, pin races, terminal receipt provenance and restore barrier: G1/G3/G6. |
| v21 | Published-trust authority, guard reducer and native capability/issue/dispatch verifier; retained actor/component tests and current offline verifier. | Installed linearizable exact guard, sole current authority, retained trust and dispatch/hold ordering: G2/G5/G6. |
| v23 | Same-owner block/reserve/activation and exact originals; current offline rejected/unissued/undispatched/post-activation correlation. | Installed race/fault histories, current registrar/mirror and authority outside application restore: G2/G5/G6. |
| Qualification | Source mode and all 78 offline verifier/interoperability tests pass; Platform ships source-owned role drivers. | Independent installed roles/ACLs, all 41 cases, actual profile/engine/trust pins, clean coordinated accepted targets/date: G1–G7. |

G4's older assertion that no mixed-root representation exists is superseded by current entry envelopes and composite completion journals. G2/G5/G6's older absent-implementation assertions are likewise superseded by the current receiving/role/composition source. Their installed, physical and complete-matrix proof obligations remain open. G7 still requires coordinated source/package acceptance; no dependency version or pointer changes here.

The exact installed-input requirements include `ErasureRootHostInstallation`'s Native/Endpoint/CredentialReference/RegistryEpochs, five independent authority bindings, CapabilityTrust, InventoryConfigurationFile/Sha256 and CopyPolicyFile/Sha256. Native enrollment requires StoreId, EnrollmentId, TransitMount, HeadMount, NativeKeys and an accepted DurabilityPolicy. Each authority requires its exact AppId, CredentialReference, PublicKeyPem, EnrollmentId, Scope and CurrentTrustRevision. The verifier additionally requires three distinct installed role executables/inventories/anchors/credential references, the accepted profile, engine binary hash, `ProtectionProfileSha256` and `ProtectionPlatformTargetCommit`. No missing value is supplied by a source hash.

The accepted-profile file remains absent at the retained path. This continuation made no remote installation query. The existing operational HMAC service and the separate disposable native ERC tests are distinct evidence. The latter use synthetic private eligibility; trim and leaked-old-ciphertext refusal do not prove sanitization of historical Raft/WAL/storage, replicas, memory/swap/core, backup/export, restored clones or operator copies. Full-copy policy authority must remain absent or denying until independently qualified. The earlier OpenBao `ObservedRevival`, `g1Qualified:false`, SHA-256 `6efa381bc70b3de57a24142e5e679f53781c7f89d5ab6740fb7664966f30888e`, is preserved and was not rerun.

## Verification and execution boundary

| Current-run check | Result |
| --- | --- |
| Debug/project-reference PayloadProtection test-project build | Zero warnings/errors. |
| Full standalone PayloadProtection suite | 890 passed; zero failures/errors/skips/unrun. Includes all ten new cases. |
| Offline verifier discovery, including actual C# interoperability | 78 passed; interoperability build zero warnings/errors. |
| Source verifier | Exit 0; 41 cases; unchanged plan SHA-256 `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c`; `liveExecuted:false`. |

These are 968 final tests; the focused ten are included in 890. The initial fixture failure, six genuine pre-correction failures, intermediate passes and final results remain retained. No Server, Platform or installed-role suite was rerun by this continuation. Prior handback results remain attributed to their own snapshots, with no additive or installed-qualification claim. Parent inspection covered the complete three-file source/test correction and current documentation; formal build-workflow review/completion remains pending at implementation step 3 because full acceptance is unmet.

Evidence directory: `/home/administrator/hexalith-protection-evidence/ext-protection-1/delivery-20261029T170427Z-f2b93ebf/`. The machine packet and adjacent verification manifest bind exact argv/cwd/results, logs and current source hashes. Initial handback auditing matched 180 of 181 source hashes; the remaining Platform custody-test file had concurrent changes. All 56 retained handback log hashes matched. These observations do not qualify current or installed binaries.

Candidate command, from the Agents root, retained without execution:

```bash
python3 ../eventstore/tools/verify-ext-protection-1.py --mode Live --profile /home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json --evidence-root /home/administrator/hexalith-protection-evidence/ext-protection-1/attempts
```

It remains NotSchedulable until the complete accepted clean targets, installed reference-only inputs, independently qualified copy policy and explicit owner acceptance exist. No Live/cohort/Agents consumer, native/infrastructure/enrollment/spending action or AppHost restart ran. No register, default DI, plan, golden, package pin/lock, dependency pointer or Git mutation was performed by this continuation. Concurrent Story 5.14/Folders/sprint work and externally staged owner documentation were preserved. The original spec baseline/frozen intent remain unchanged, all full-delivery tasks stay unchecked and status stays in-progress.
