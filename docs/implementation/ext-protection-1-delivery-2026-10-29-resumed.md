# EXT-PROTECTION-1 resumed owner checkpoint — 2026-10-29

**BlockedNotSubmitted.** Mixed-root snapshot cancellation and Server integration coverage are corrected. Full delivery remains in progress: concurrent Platform review withdrew both prior runtime candidates and found additional source defects. Independent installation, physical qualification and the entire installed 41-case run also remain outstanding. The [machine packet](ext-protection-1-delivery-2026-10-29-resumed.json) preserves the complete authoritative register contract and its exact nine commitment fields. The [earlier October 29 packet](ext-protection-1-delivery-2026-10-29.json) remains historical evidence.

## Delivered correction and integration evidence

The mixed-root reader could return readable state when its registered JSON converter cancelled the caller during materialization. [InteractionSnapshotEntriesProtector](../../src/Hexalith.EventStore.PayloadProtection/InteractionSnapshotEntriesProtector.cs) now checks the exact caller token before returning reconstructed state. Two actual-engine read/replay regressions failed before correction and pass afterward; they also inspect unchanged original ciphertext, active occurrence records and zeroed owned buffers.

Two older Server tests assumed all mixed-root content must refuse before binding lookup. The delivered format permits independently enrolled same-tenant entries. The corrected negatives require the exact complete manifest, paths, target identities and skeleton digest at that read-only lookup, while still requiring no registry/key/completion/persisted effects when enrollment is absent. Foreign tenants continue to refuse before any binding call.

Two new [Server integration cases](../../tests/Hexalith.EventStore.Server.Tests/Security/InteractionProtectionPersistenceTests.cs) use the real engine and SnapshotManager with independently fixed synthetic authorities. They inspect committed per-entry ciphertext and composite completion, exact original retry without a new root/lease, ordinary loads and serialized-cache loads. Partial erasure preserves the surviving interaction through the actual SnapshotManager replay caller while external reads remain unreadable. These are persisted-state component checks, not installed custody or physical-erasure qualification.

## Withdrawn candidates and remaining source work

The earlier EventStore candidate `5945ba8b3575f76597075e460bb9a973453272ea` lacks the historical mixed-root ownership-read correction already included in observed HEAD `8348bc491ace70dbd0d84c2320a6e4372479bbb9`. The current cancellation correction also requires inclusion in a replacement reviewed target. Neither observed HEAD nor a documentation commit is accepted as the complete target.

The concurrent [Platform review and iteration-1 handoff](../../../platform/_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md#review-triage-log) withdraw Platform candidate `965b00249b473d602a663f1f48be39424f996337` as well. Its verified findings require actual erased-replay pass paths, reachable pending-manifest and exact role-result recovery, consistent backup/restore identities, both hold/dispatch race outcomes, replay correlation to each native copy, quiet-host holder completion, bounded immutable history, native framing/deadlines, safe bootstrap transport, malformed-skeleton rejection and zero-effects missing-input preflight. Configured composition and actual role-service recovery also need their identified regression coverage. These corrections are being developed separately in isolated worktrees; this continuation neither imports unfinished work nor closes those findings. The new concrete SnapshotManager tests cover the overlapping VG-1 caller gap; disposition of that independent review remains with its owner.

Current source supplies the previously missing custody, authority, composition and role APIs. Their existence does not establish complete source correctness or a passing installed matrix. The original full frozen acceptance remains unchanged, all full-delivery task boxes stay unchecked and the workflow remains at implementation step 3.

## Installed and physical acceptance

G1–G7 remain open. The packet maps all seven frozen matrix rows to source/component evidence and outstanding proof. Required inputs include the exact ERC native endpoint/store/enrollment/genesis/tenant keys, independently accepted copy policy and inventory, five independent current authority bindings, source/writer/digest enrollment, capability/compromise trust, and three installed Executor/Observer/Enrollment programs with distinct identities, P-256 anchors, scoped credentials, complete file inventories, native arguments and observation locations. The retained binding-path inventory names the actual current contracts without supplying invented values.

The accepted profile remains absent at `/home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json`. Complete accepted EventStore/Platform targets, integration date and installed profile/engine/role/trust hashes remain null. Owner/repository remain EventStore Maintainer / Hexalith.EventStore; `2026-11-02` remains an unaccepted proposal. The register stays `Uncommitted`; availability and qualification stay false.

Full-copy destruction and nonrollback restore remain unproved for historical Raft/WAL/storage, replicas, memory/swap/core, exports, backups, restored clones and operator copies. The copy-policy authority must continue to deny until independently qualified. The retained earlier `ObservedRevival` result, SHA-256 `6efa381bc70b3de57a24142e5e679f53781c7f89d5ab6740fb7664966f30888e`, still has `g1Qualified:false` and was not rerun. No remote installation query occurred.

## Verification and boundaries

| Final current-run check | Result |
| --- | --- |
| Full standalone PayloadProtection suite | 892 passed; zero failures/errors/skips/unrun. Includes the twelve mixed-snapshot cases. |
| Selected Server SnapshotManager/persistence/actor recovery | 119 passed; zero failures/errors/skips/unrun. Includes the two new integration cases. |
| Offline verifier and actual C# interoperability | 78 passed. |
| Debug/project-reference builds | Zero warnings/errors on successful builds. Final Server build uses `-p:MinVerVersionOverride=1.0.0`. |
| Source verifier | 41 cases; unchanged plan SHA-256 `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c`; `liveExecuted:false`. |

These are 1,089 final nonoverlapping tests; focused subsets are included. The two genuine cancellation failures, two stale integration-expectation failures and two transient CS0006 shared-output build failures remain retained. No gate or dependency pin was weakened. The initial exact-equality preservation check also detected a concurrent owner-withdrawal paragraph; the final insertion-only check confirms that all original owner-packet lines and that concurrent addition survive.

Implementation evidence is `/home/administrator/hexalith-protection-evidence/ext-protection-1/delivery-20261029T172523Z-7a55224c/`; parent audit/evidence is `/home/administrator/hexalith-protection-evidence/ext-protection-1/resume-20261029T172443Z-b32cba74/`. Their manifests bind exact commands, results, failed attempts, source and binary hashes. Parent inspected all five current source/test files. The full original-baseline diff is retained losslessly; no full historical review or formal workflow-completion claim is made.

Candidate command, from the Agents root, retained without execution:

```bash
python3 ../eventstore/tools/verify-ext-protection-1.py --mode Live --profile /home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json --evidence-root /home/administrator/hexalith-protection-evidence/ext-protection-1/attempts
```

It remains NotSchedulable. This continuation ran no Live/cohort/Agents consumer, native/infrastructure/enrollment/spending action or AppHost restart, and made no Git, dependency-pointer/package-pin/lock, register, plan/golden or default-DI mutation. Concurrent HTTP-security and external owner-review work were preserved. No acceptance is requested for an incomplete owner record.
