# EXT-PROTECTION-1 owner checkpoint — 2026-10-28

**BlockedNotSubmitted.** Exact operation-original reads are implemented and verified. The complete owner delivery remains in progress: G1–G7 still require source/protocol integration, actual installations, physical evidence and a coordinated immutable target. The [register](../../../agents/_bmad-output/planning-artifacts/external-dependency-register.md), including v21/v23, remains authoritative. This checkpoint preserves the [previous report](ext-protection-1-delivery-2026-10-25-resumed.md) and [packet](ext-protection-1-delivery-2026-10-25-resumed.json).

The [new machine packet](ext-protection-1-delivery-2026-10-28.json) retains the complete register contract and all nine commitment fields. Owner/repository remain EventStore Maintainer / Hexalith.EventStore. Complete canonical target and accepted integration date remain null. **2026-11-02** remains an unaccepted date proposal. Evidence levels 4 and 5 remain required; these checks establish offline/component behavior only. Register status remains Uncommitted, with availability and qualification false.

## Delivered source correction

The private [owner contract](../../src/Hexalith.EventStore.Contracts/Security/IDeletionProtectionOwner.cs) and [actor](../../src/Hexalith.EventStore.Server/Security/DeletionConsumptionActor.cs) now expose four operation-specific reads:

| Read | Retained result |
| --- | --- |
| `ReadRegistrationOriginalAsync` | Exact original signed registration request and its registration outcome, including original coverage receipts. |
| `ReadReservationOriginalAsync` | Exact reservation request and its reservation-commit outcome, preserving its original revision after consumption. |
| `ReadBlockOriginalAsync` | Exact admission block request and the outcome returned at its commit, including an already-reserved result. |
| `ReadActivationOriginalAsync` | Exact successful or replacement-key-blocked activation and its original outcome after later re-attestations. |

Each returns [DeletionConsumptionOriginal](../../src/Hexalith.EventStore.Contracts/Security/DeletionConsumptionOriginal.cs). Registration/reservation outcomes and complete admission requests are retained in the independently anchored ledger; activation originals were already retained. Reads require settled stored state and current exact owner authority before release. They do not reconcile pending state, reserve, invoke a provider or create authority. Returned target/receipt collections are detached and read-only. The bounded Dapr adapter preserves explicit caller cancellation.

A reservation-original read returns `ConsumptionReserved` without inventing a terminal physical receipt vector. The existing `ReadReservedManifestOriginalAsync` supplies the separate retained reserved/consumed manifest view. Existing `LookupAsync` retains its original recovery behavior. Historical records missing a new original return null; absent nullable journals are omitted from JSON to preserve historical state/hash bytes. No historical original is reconstructed from later mutable batch state.

Platform consumers must adopt the new optional ports at the coordinated accepted source/package target and install exact tenant/scope grants for `ReadDeletionRegistrationOriginal`, `ReadDeletionReservationOriginal`, `ReadDeletionBlockOriginal` and `ReadDeletionActivationOriginal`. Omitted ports deny. No default registration or installed permission changed here.

## Updated integration assessment

Current Platform source now supplies concrete `DaprCustodyOriginalLocators`, registration publication/receiving code, `CustodyInteractionProtectionBindingSource`, `CustodyInteractionOccurrenceKeyProvider`, FR-34 composition/enrollment, and authenticated qualification receiving programs. The older October 25 statements that these source implementations are absent are superseded. The direct occurrence provider transfers one owned root buffer; the older candidate-delivery adapter still refuses material copying. The evidence assessment retains exact current file paths and hashes.

| Gap | Current remaining requirement |
| --- | --- |
| G1 | Actual six-operation custody and complete atomic native manifest/holder/pin/backup receipt provenance; source clients do not establish physical all-or-none effects. |
| G2 | Independently installed authority identities, exact grants/current trust/revocation, enrolled genesis, immutable originals and placement outside application restore; complete native deployment/ACL attestation remains unselected. |
| G3 | Complete native holder/copy census, terminal deletion, restore exclusion and survivor-recovery control/proof. The retained OpenBao counterexample remains `ObservedRevival`, `g1Qualified:false`. |
| G4 | Complete multi-root snapshot/completion/selective-replay contract and source/target/cache/restore evidence, plus actual writer/anchor/scheduler fault-control histories. Existing single-target snapshot safety is retained. |
| G5 | Complete native 41-case execution/resource/control mappings and independently installed executor/observer/enrollment, engine and ACL evidence. Receiving program source alone does not qualify these. |
| G6 | Complete authenticated guarded-commit/append inventory, current sole authority and native mutation/physical provenance. The four new ports close the named operation-original access gap; they do not enumerate unlisted guard outcomes. |
| G7 | Consumer adoption and package-lane closure at a complete immutable accepted target, accepted integration date and actual installed profile/engine/role/trust hashes. |

Required installation fields include `IndependentDaprAuthorityBinding` (`AppId`, `CredentialReference`, `PublicKeyPem`, `EnrollmentId`, `Scope`, `CurrentTrustRevision`), `CustodyInteractionBindingResource` (`Component`, `Key`, `OwnerScope`, `Authority`, `Metadata`) and `CustodyInteractionMaterialProfile` (`Registration`, `KeyUse`, `DigestReference`). Their source definitions grant no enrollment or custody authority. The accepted protection-profile file remains absent; installed profile, engine, role and trust bindings remain null. Twelve installed hashes in the retained Platform packet are also null. This checkpoint performed no remote installation query.

## Verification and preserved evidence

[DeletionConsumptionOriginalTests](../../tests/Hexalith.EventStore.Server.Tests/Security/DeletionConsumptionOriginalTests.cs) adds 40 passing cases covering actual committed state, restart, lost pending/main save acknowledgements, later mutations, exact retry, substitution, malformed originals, historical omission, current-authority refusal, caller-owned collection mutation, cancellation and real Dapr serialization. Read-only tests allow only state reads and authority checks and assert zero provider calls.

| Final selection | Result |
| --- | --- |
| Combined Server actor/original/authority/snapshot/persistence classes | 316 passed |
| Full replacement-authority suite | 98 passed |
| PayloadProtection snapshot/service classes | 43 passed |
| Offline verifier and native C# interoperability | 66 passed |
| Debug/project-reference test-project builds | Zero warnings/errors |
| Source verifier | Exit 0; unchanged 41-case plan; `liveExecuted:false` |

These are **523** final selected tests, with zero failures, skips or unrun cases. The initial original-read run exposed incorrect caller-token preservation; its failed result remains retained alongside the correction and final passing run. The entire test assemblies and installed matrix were not certified by these focused selections.

The frozen matrix remains open at full-delivery scope:

| Matrix row | Current source/test evidence and remaining proof |
| --- | --- |
| Protect | Existing snapshot/service and actual hook/persistence tests passed; installed same-root field protection and complete creation-outbox copy coverage remain G1/G4/G5. |
| Read/distribute | Existing actor-recovery/persistence tests passed; full source/target/snapshot/cache erased replay with installed custody remains G3/G4/G5. |
| Custody | Current source clients and offline signatures/codecs are retained; installed six-operation, pin, holder and restore closure remain G1/G3. |
| Manifest | Actual actor/original/reserved-manifest tests passed, including unchanged original receipt vectors; independently observed native all-or-none effects remain G1/G3/G6. |
| v21 | Published-trust authority and full replacement-authority suites passed; installed current guard, trust, sole attestation and exact dispatch authority remain G2/G6. |
| v23 | Actor and new operation-original cases passed across replacement, block/reserve, lost-save and later-state changes; installed owner/guard races and outside-restore authority remain G2/G5/G6. |
| Qualification | All 66 offline verifier/interoperability checks and Source mode passed; actual independent roles, native case drivers, accepted clean target and physical evidence remain G1–G7. |

Evidence directory: `/home/administrator/hexalith-protection-evidence/ext-protection-1/checkpoint-20261027T235104Z-7d8b3efb/`. `verification-manifest-final.json` retains exact argv/cwd/results, 17 hashed command logs, final source/test hashes and local Debug binaries. The supplemental `parent-replacement-tests` journal retains the exact 98-case invocation/result. `integration-assessment.json` records current Platform source and exact remaining protocols/fields. Parent checks verify those hashes and inspect the complete current correction diff. The first historical-baseline diff command (`git diff --binary 9b062d0d4f506bf45b7fdcd8e54189599a36ff9b --`) exited 128 on unavailable submodule history; its output is retained. The retry with `diff.submodule=short` captured the historical diff without changing any dependency pointer. That broad historical artifact is not a review claim for unrelated changes.

The raw plan SHA-256 remains `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c`. The original restore result remains SHA-256 `6efa381bc70b3de57a24142e5e679f53781c7f89d5ab6740fb7664966f30888e`; it was not rerun. Full-delivery tasks remain unchecked and status remains in-progress. The original baseline and frozen intent are preserved. Concurrent Story 5.12 documentation is outside this change.

Candidate owner command, from the Agents root, retained without execution:

```bash
python3 ../eventstore/tools/verify-ext-protection-1.py --mode Live --profile /home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json --evidence-root /home/administrator/hexalith-protection-evidence/ext-protection-1/attempts
```

It requires the complete accepted clean target, accepted integration date, actual reference-only profile and `ProtectionProfileSha256` bound to its exact bytes. Installed execution remains NotSchedulable. No Live/Full/cohort, Agents consumer, native experiment, infrastructure action, enrollment or spending ran. No AppHost was restarted. Platform, Agents, default DI, plan, goldens, package pins/locks and dependency pointers were not changed by this work. No stage, commit or push ran, and no incomplete owner acceptance is requested.
