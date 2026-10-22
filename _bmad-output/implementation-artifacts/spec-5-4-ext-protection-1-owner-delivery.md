---
title: 'EXT-PROTECTION-1 Story 5.4 owner bindings'
type: 'feature'
created: '2026-10-22'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: c66a0a3345ad97b137959179a0583faf81d59e4b
context:
  - AGENTS.md
  - references/Hexalith.AI.Tools/hexalith-llm-instructions.md
  - ../agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - ../platform/docs/implementation/ext-secrets-1-erasure-authority-decision-2026-10-20.md
  - docs/implementation/ext-protection-1-owner-acceptance.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Agents Story 5.4 cannot treat EXT-PROTECTION-1 as Committed. The engine, v23 owner, and offline verifier exist, but the occurrence-key, protection-binding, guard, occurrence, and guarded-transaction authorities do not. Published trust denies persistence, block, revocation, and activation. The Dapr component and accepted profile are not installed.

**Approach:** Add those EventStore bindings on the accepted Dapr-only `custody-lifecycle-v1` design. Reuse the engine, actors, guard, and Platform's internal providers. Fail closed without installed references. Append a packet that lists the missing runtime inputs and does not mark the record Available.

## Boundaries & Constraints

**Always:** One C# type per file. Destroy uses the admitted original manifest, not a per-key delete. Lookup and lost-result recovery start no effect and return the retained original. An earlier effective or unknown pin blocks destruction; unpin releases only that hold; a later hold does not cancel a dispatched batch. Restored actor state and the retained OpenBao snapshot are not current authority. Keep the NoOp default, unreadable public HTTP replay, plan SHA-256 `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c`, and pdenc-v2 goldens.

**Never:** Edit the Agents register or Platform submodule. Run Live, OQ-5.4-1, an Agents consumer, provisioning, or a destructive probe. Reference a Platform project from Server or add a second custody engine. Bind `ext-secrets-1-hmac` as a DEK provider. Invent profile, anchor, credential, tenant, or journal values. Claim Committed or Available from a local test.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Protect | Complete target and current write lease | Only `ProtectedContent` is sealed under that root | Absent key, binding, or epoch denies before ciphertext |
| Hold | Effective or unknown pin, then destroy | Destroy is blocked | A different hold's unpin does not release it |
| Manifest | Admitted batch and exact capability | All targets or none, with the original ordered receipts | Changed digest conflicts; partial transport is Unknown |
| Lost result | Admitted work then timeout | Lookup returns the retained vector | Lookup does not pin, unpin, or destroy |
| v21 / v23 | Exact anchor, sole attestation, and one owner race | Dispatch can proceed; block or reserve has one winner | Stale, revoked, substituted, pending, or cross-tenant evidence denies; admission blocks never reopen |
| Restore / absent inputs | Old snapshot, revival result, or null reference | No reissue and no first effect | Write no synthetic profile or secret value |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Configuration/InteractionPayloadProtectionServiceCollectionExtensions.cs` — opt-in registers the service and two actors only. Leave it unchanged. Default host stays `NoOpEventPayloadProtectionService`.
- `src/Hexalith.EventStore.PayloadProtection/IInteractionOccurrenceKeyProvider.cs`, `IInteractionProtectionBindingProvider.cs` — no production class. Key delivery is not open: Platform refuses `wrap` and `unwrap`. The new key adapter returns an owned key only when an injected delivery does; production passes none.
- `src/Hexalith.EventStore.Server/Security/IInteractionOccurrenceAuthority.cs` — no implementation. `InteractionOccurrenceRegistryActor` returns `Unavailable` without an epoch and authorization.
- `src/Hexalith.EventStore.Server/Security/PublishedTrustDeletionConsumptionAuthority.cs` — keep `VerifyDispatchAsync`. The six methods at lines 39–99 and the inherited transition defaults deny, so `DeletionConsumptionActor` cannot commit.
- `src/Hexalith.EventStore.Server/Security/DaprGuardedStateTransaction.cs`, `GovernanceScopeGuardOwner.cs` — durable cells already fail closed. Add the two missing authorities; do not add a store.
- `src/Hexalith.EventStore.Contracts/Security/IAnchoredStateTransitionAuthority.cs` — admit retains the original without advancing; record compares and advances; recover and verify read that original.
- `references/Hexalith.Platform/src/Hexalith.Platform.Secrets.Runtime/CustodyKeyLifecycleBindingProvider.cs`, `references/Hexalith.Platform/src/Hexalith.Platform.Protection/AtomicDeletionManifestBindingProvider.cs` — reuse. Internal, unregistered, and Unknown or Unavailable without a witness. `Platform.Protection` references Server, so Server must not reference it.
- `tools/ext-protection-1/plan.json`, `tools/verify-ext-protection-1.py` — do not change the plan or run Live.
- `docs/implementation/ext-protection-1-owner-acceptance.md` — append one section. The 2026-10-18 sentence is stale for the two Platform providers and still true for the EventStore ports.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.EventStore.PayloadProtection/InteractionOccurrenceKeyProvider.cs` — enforce one lease and current-root checks; deny a second encryption, a missing tombstone, and a missing delivery.
- [ ] `src/Hexalith.EventStore.PayloadProtection/InteractionProtectionBindingProvider.cs` — resolve the retained target, not the caller alias; require the completion lease; erasure is true only from an independent witness receipt.
- [ ] `src/Hexalith.EventStore.Server/Security/InteractionOccurrenceAuthority.cs` — implement epoch, reservation, sealed, writer, and transition checks. Missing installation denies. Restored divergence denies.
- [ ] `src/Hexalith.EventStore.Server/Security/GovernanceGuardAuthority.cs` — return signing and lookup evidence only for an exact installed receipt. Missing provenance returns null.
- [ ] `src/Hexalith.EventStore.Server/Security/GuardedStateTransactionAuthority.cs` — authorize the current cell, lookup, commit, and receipt. Restored absence is not a fresh empty cell.
- [ ] `src/Hexalith.EventStore.Server/Security/ProtectionCurrentAnchorJournal.cs` — durable admit, compare-and-advance, same-original recover, and fresh verify. A lost record stays Unknown until lookup.
- [ ] `src/Hexalith.EventStore.Server/Security/PublishedTrustDeletionConsumptionAuthority.cs` — bind the denying methods and four transition methods to the guard, revocation evidence, and journal. Exact injected evidence can pass in tests; missing or mismatched evidence denies. Do not self-issue anchors.
- [ ] `src/Hexalith.EventStore.Server/Configuration/InteractionProtectionBindingServiceCollectionExtensions.cs` — register the new adapters and journal. Do not call it from the existing opt-in, AppHost, or default host. Pass no key delivery, profile, or anchor.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Security/PublishedTrustDeletionConsumptionAuthorityTests.cs`, `tests/Hexalith.EventStore.PayloadProtection.Tests/InteractionPayloadProtectionServiceTests.cs` — cover the matrix with synthetic ports.
- [ ] `docs/implementation/ext-protection-1-owner-acceptance.md` — append observed full HEAD, no accepted target, proposed date `2026-11-02` only, the existing Live command not run, and the missing references in Design Notes.

**Acceptance Criteria:**
- Given no installed binding, anchor, epoch, or journal, when a first effect runs, then it denies and writes no secret or profile value.
- Given an admitted original and a lost result, when lookup runs, then the retained vector is returned and no second destructive identity is created.
- Given a restored snapshot or the retained OpenBao revival result, when authority is read, then the destroyed root is not reissued and the packet does not say Available.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Server cannot construct Platform's internal providers without a project cycle. The explicit registration exposes EventStore adapters only. Production key delivery stays absent because `wrap` and `unwrap` are refused. These remain exact missing inputs: `/home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json`; `executor`, `observer`, and `enrollment` executables, anchors, credential references, and manifests; `capabilityTrust`; `runtime.allCopyLifecycleAuthorityReference`; `runtime.allCopyInventoryReference`; `runtime.nonrollbackJournalReference`; `runtime.restoreTarget`; `runtime.protectionOwnerReference`; `runtime.currentRevocationReference`; `runtime.publicTrustReference`; `runtime.publicAnchorRetentionReference`. HMAC reference `31c28a9d1dd21eed8ca4d0c421088cb946b262c624ba461d0795cab92cd1feb3` is not a substitute.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — expected: exit 0, zero warnings.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true --filter "FullyQualifiedName~PublishedTrustDeletionConsumptionAuthorityTests|FullyQualifiedName~DeletionConsumptionActorTests|FullyQualifiedName~GovernanceScopeGuardTests|FullyQualifiedName~InteractionOccurrenceRegistryActorTests"` — expected: pass, zero skip.
- `dotnet test tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true --filter "FullyQualifiedName~InteractionPayloadProtectionServiceTests"` — expected: pass, zero skip.
- `python3 tools/verify-ext-protection-1.py --mode Source` — expected: exit 0, 41 cases, unchanged plan hash, `liveExecuted` false.

**Manual checks (if no CLI):**
- The new packet section names the observed HEAD and the missing references, and does not say Committed or Available.
