---
title: 'EXT-PROTECTION-1 Story 5.4 owner bindings'
type: 'feature'
created: '2026-10-22'
status: 'done'
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
- [x] `src/Hexalith.EventStore.PayloadProtection/InteractionOccurrenceKeyProvider.cs` — enforce one lease and current-root checks; deny a second encryption, a missing tombstone, and a missing delivery.
- [x] `src/Hexalith.EventStore.PayloadProtection/InteractionProtectionBindingProvider.cs` — resolve the retained target, not the caller alias; require the completion lease; erasure is true only from an independent witness receipt.
- [x] `src/Hexalith.EventStore.Server/Security/InteractionOccurrenceAuthority.cs` — implement epoch, reservation, sealed, writer, and transition checks. Missing installation denies. Restored divergence denies.
- [x] `src/Hexalith.EventStore.Server/Security/GovernanceGuardAuthority.cs` — return signing and lookup evidence only for an exact installed receipt. Missing provenance returns null.
- [x] `src/Hexalith.EventStore.Server/Security/GuardedStateTransactionAuthority.cs` — authorize the current cell, lookup, commit, and receipt. Restored absence is not a fresh empty cell.
- [x] `src/Hexalith.EventStore.Server/Security/ProtectionCurrentAnchorJournal.cs` — durable admit, compare-and-advance, same-original recover, and fresh verify. A lost record stays Unknown until lookup.
- [x] `src/Hexalith.EventStore.Server/Security/PublishedTrustDeletionConsumptionAuthority.cs` — bind the denying methods and four transition methods to the guard, revocation evidence, and journal. Exact injected evidence can pass in tests; missing or mismatched evidence denies. Do not self-issue anchors.
- [x] `src/Hexalith.EventStore.Server/Configuration/InteractionProtectionBindingServiceCollectionExtensions.cs` — register the new adapters and journal. Do not call it from the existing opt-in, AppHost, or default host. Pass no key delivery, profile, or anchor.
- [x] `tests/Hexalith.EventStore.Server.Tests/Security/PublishedTrustDeletionConsumptionAuthorityTests.cs`, `tests/Hexalith.EventStore.PayloadProtection.Tests/InteractionPayloadProtectionServiceTests.cs` — cover the matrix with synthetic ports.
- [x] `docs/implementation/ext-protection-1-owner-acceptance.md` — append observed full HEAD, no accepted target, proposed date `2026-11-02` only, the existing Live command not run, and the missing references in Design Notes.

**Acceptance Criteria:**
- Given no installed binding, anchor, epoch, or journal, when a first effect runs, then it denies and writes no secret or profile value.
- Given an admitted original and a lost result, when lookup runs, then the retained vector is returned and no second destructive identity is created.
- Given a restored snapshot or the retained OpenBao revival result, when authority is read, then the destroyed root is not reissued and the packet does not say Available.

## Implementation Notes

2026-10-22: EventStore adapters and the anchor journal are fail-closed unless an injected installation, delivery, or store is present. `AddInteractionProtectionBindings` passes none of those. Lookup of a lost manifest returns the original receipt vector and does not pin, unpin, or destroy. Observed HEAD remains `6b2988d0ecae276440cb2bd29a93af4c12d7c07c`; that commit does not contain this delivery. Server build exited 0 with 0 warnings. Filtered Server tests passed 187 with 0 skipped. Payload protection service tests passed 5 with 0 skipped. `python3 tools/verify-ext-protection-1.py --mode Source` exited 0 with 41 cases, plan SHA-256 `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c`, and `liveExecuted` false. Live was not run. The Agents register and Platform submodule were not edited. Unrelated admin and UI working-tree edits were left in place.

Review patches: a lease is stored only for a 32-byte key; duplicate admitted targets are rejected; revisions below 1 are denied; activation requires distinct receipts and the batch key version, block-set revision, and dispatch receipt; signing recovery requires the detached digest and signing-request id; sealing requires a reserved writer. The same verification commands passed again: Server build 0 warnings, 187 Server tests, 5 payload tests, Source mode 41 cases, `liveExecuted` false.

## Spec Change Log

## Review Triage Log

- AuthorizeOperationAsync is only asserted false — medium. `DeletionConsumptionActor.AdmitAsync` gates on it, and both current assertions expect false, so a permanent false stays green. Patch the true path and a journal rejection.
- Authority recover, record, and verify are not called — medium. `RecoverableAnchoredState.CommitAsync` needs `RecordTransitionAsync`. Only `AdmitTransitionAsync` is asserted. Patch the three delegated results.
- Occurrence verify-false and installed forwarding are not asserted — medium. `ValidateStateAsync` can return true after `MatchesAsync` even if the journal call is removed. Patch a false journal result and forward calls on an installed authority.
- Write release without a lease is not asserted — medium. `ReleaseAsync` depends on `IsCurrentAsync(..., "Write")`, and the seal test already holds a lease. Patch the deny-before-acquire assertions.
- Private proof is never admitted — false. The frozen task says not to self-issue anchors. The six methods verify an injected journal; `Proof` scopes are not stored, so a later revision cannot collide with a retained original.
- `ValidateStateAsync` returns true at revision 0 for any 64-hex digest — medium. The journal is skipped after the guard revision matches. Patch: revisions below 1 do not return true; the journal still verifies.
- `RecordRevisionAsync` verifies and does not record — false. The interface says this legacy hook alone never enables the actor, and the task forbids self-issued anchors. `!=` binds inside `&&`, so the revision condition already means "neither expected nor next".
- A lost advance calls `AdvanceAsync` again — false. The second call is the same original, not a new destructive identity. A different original stays denied.
- Journal `Hex` rejects lowercase while the authority accepts it — false. `Proof` builds digests with `Convert.ToHexString`, which is uppercase, and that is what the journal checks.
- `WellFormed` does not chain the predecessor digest — false. `RecordTransitionAsync` requires `Exact` equality with the admitted transition, so a substituted predecessor does not advance.
- `ConfirmsAsync` does not rehash stored bytes — low. Only a store that returns a digest different from bytes it previously admitted can hit this. Rejected: everyday callers do not meet it, and the fix adds another hash branch.
- Successful journal advance is untested — medium. `loseAdvance` stays true, so the `Advanced` read-back path is never green. Patch one successful record.
- A second process can lease again — low. Production registration passes no delivery, and the occurrence registry remains the durable single reservation. Rejected: a durable lease store is new machinery for a path that is closed.
- A short key consumes the only encryption lease — medium. `AcquireEncryptionLeaseAsync` inserts the lease once `Current` is true, before the 32-byte check. Patch: do not insert unless the delivered key is 32 bytes.
- A source that throws after granting the completion lease stays granted — false. The local id is released on throw or cancellation. The source interface has no release because a granted lease stays single-use.
- `ResolveAsync` ignores `writing` — false. The completion lease is enforced by `AcquireCompletionLeaseAsync`. Resolve returns the retained target and does not grant that lease.
- Delimiter collision in joined ids — low. Everyday tenant, alias, and operation ids do not contain U+001F or raw newlines. Rejected: rejecting those characters adds a new validation rule.
- One listed target can satisfy erasure — false. The witness is the admitted vector. A one-target manifest is a complete vector when its order digest matches.
- Duplicate admitted targets are accepted — medium. Receipt uniqueness does not reject a repeated target id. Patch: a repeated admitted target returns neither erasure nor a lookup vector.
- Lookup is not on the interface — false. The matrix is covered by the concrete lookup the payload tests call. The service does not need a second interface method.
- A later hold is not distinct from an earlier pin — false. `EffectiveHoldId` is the pin that still blocks. A later hold is not supplied as that id, and a different unpin does not clear it.
- `ValidateCellAsync` accepts any non-empty value — medium. `ReadCellAsync` returns only a history enum, so the authority cannot see a cell digest. The installation is the injected classifier; `Occupied` plus a positive revision is the check this type owns. The missing authorize/lookup/receipt tests are the same gap as the untested true paths and are patched with the journal and authority tests rather than a new store.
- Guard evidence is not joined to `state.Receipts` — false. The installation is the injected receipt source. `Exact` already rejects a different tenant or digest.
- Expired `ValidUntil` is returned — low. No production clock is installed, and adding one changes the constructor. Rejected for this closed registration.
- Expired guarded-target lease is still current — low. Same closed registration. Full record equality already rejects a different target. Rejected.
- Revocation and activation do not compare key version, block-set revision, or distinct receipts — medium. `GovernanceBatchState` has `CapabilityKeyVersion`, `BlockSetRevision`, and `DispatchReceiptId`, and three `HasCommittedReceipt` calls can reuse one id. Patch those equalities and distinct ids.
- Empty signing fields skip equality — medium. A blank detached digest or signing-request id returns a recovery that was not compared. Patch: those fields must be present and equal.
- `VerifyBlockedReplacementAsync` stays default false — false. The task binds the six methods and the four transition methods. That default remains deny, which is fail-closed.
- Key delivery does not echo purpose — false. `Own` labels the buffer with the purpose of the call that requested it. A missing tombstone stays closed when `TombstoneRequired` is true.
- The code map still says the ports are absent — false. The fix would edit this spec's code map. The acceptance section already says the 2026-10-18 discovery sentence is stale for the Platform providers and that these adapters register with no delivery.
- Transition delegation skips a second guard check — false. Guard and revocation checks are the six methods and `VerifyDispatchAsync`. The four transition methods are specified to delegate the original to the journal.
- `Proof` scope omits the revision — false. Those proofs are verified, not admitted, so two revisions do not share a stored original.
- Negative or wrapped revisions can reach `Proof` — medium. A negative guard revision that matches the state skips the journal at the revision-0 branch, and an unchecked `long` add can wrap. Patch: deny a revision below 1 before building a proof, and compare successor ordinals with a checked add.
- Occurrence `VerifySealedAsync` allows a non-reserved writer — medium. An aborted record can reach the installation. Patch: require `WriterState` `Reserved`.
- Installed occurrence authority forwards any transition — low. Production passes no installation, so `Forward` returns false. Rejected: tenant-scoped transition authorization is a new policy on a closed registration.

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
