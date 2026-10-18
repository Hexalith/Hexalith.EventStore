---
title: 'EXT-PROTECTION-1 owner qualification delivery'
type: 'feature'
created: '2026-10-18'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 1aaa173715305a08aff30c4b9cd6354a7073e541
context:
  - AGENTS.md
  - references/Hexalith.AI.Tools/hexalith-llm-instructions.md
  - ../agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - ../agents/_bmad-output/implementation-artifacts/story-5-4-dependency-reconciliation-2026-10-18.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** OQ-5.4-1 phase 0 needs EXT-PROTECTION-1 Committed. At `1aaa173715305a08aff30c4b9cd6354a7073e541` the engine, v21/v23 owner, and offline verifier exist, but custody, online authority, and installed inputs do not. Local tests must not make it Available.

**Approach:** Add a fail-closed online consumption authority and actor opt-in, tighten native verifier checks, and refresh the acceptance packet as a proposal only.

## Boundaries & Constraints

**Always:** Follow the Agents register, including v21 and v23. Preserve fail-closed defaults, pdenc-v2 goldens, plan hash `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c`, proposed date `2026-11-02`, and the local status edit on `spec-ext-protection-1-capability-evidence.md`. One C# type per file. Fixture-key tests are component evidence.

**Never:** Select Platform custody option A or B, or bind the OpenBao HMAC mapping as a DEK or manifest provider. Self-issue anchors, use an always-true authority, or invent a mixed-root snapshot policy. Edit the Agents register, run Live or OQ-5.4-1, enable an Agents consumer, or change Git history. A local test is not Available or Committed.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Dispatch | Valid detached ES256, one published trust entry, fresh guard read with one active attestation and committed dispatch | `VerifyDispatchAsync` returns true | N/A |
| Absent trust or guard | Missing anchor, stale or revoked attestation, mismatched batch, or unreadable guard | Returns false and destroys nothing | Fail closed |
| Opt-in without custody | Engine opt-in without key, binding, manifest, or authority services | Actors resolve; first custody or consumption effect denies | Those services are not created |
| Later hold | Hold ordered after a successful dispatch receipt | That batch is not cancelled; other batches stay restricted | N/A |
| Verifier | Substituted native hold, pin, coverage, activation, or restore original | Offline evaluation rejects | Missing roles stay Blocked |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Security/IDeletionConsumptionAuthority.cs` — reuse the online contract unchanged.
- `src/Hexalith.EventStore.Server/Security/DeletionConsumptionActor.cs` — v23 owner; null authority or provider denies.
- `src/Hexalith.EventStore.Server/Security/GovernanceScopeGuardOwner.cs` — fresh guard read. Do not cache it.
- `src/Hexalith.EventStore.Contracts/Security/DeletionBatchCapabilityIdentity.cs` — keep canonical bytes.
- `src/Hexalith.EventStore.Server/Configuration/InteractionPayloadProtectionServiceCollectionExtensions.cs` — opt-in replaces the no-op and must not create custody.
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` — default host stays on `NoOpEventPayloadProtectionService`.
- `IInteractionOccurrenceKeyProvider`, `IInteractionProtectionBindingProvider`, `IAtomicDeletionManifestProvider` — seams only. Platform options A and B in `../platform/docs/implementation/ext-secrets-1-dek-provider-decision-2026-10-18.md` are unselected.
- `tools/ext_protection_1.py`, `tools/ext-protection-1/plan.json` — keep the 41-case plan bytes.
- `docs/implementation/ext-protection-1-owner-acceptance.md` — refresh; status remains Uncommitted.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Server/Security/PublishedTrustDeletionConsumptionAuthority.cs` — implement the authority with injected published trust and `IGovernanceScopeGuard`. Verify the detached ES256, then reread the guard for the same batch, one unrevoked attestation, and the committed dispatch. Deny on absence, substitution, revocation, or an unavailable read.
- [x] `src/Hexalith.EventStore.Server/Configuration/InteractionPayloadProtectionServiceCollectionExtensions.cs` — register the occurrence-registry and deletion-consumption actors only in this opt-in.
- [x] `tests/Hexalith.EventStore.Server.Tests/Security/PublishedTrustDeletionConsumptionAuthorityTests.cs` — cover the matrix with fixture trust and a guard double.
- [x] `tools/ext_protection_1.py` and `tools/tests/test_ext_protection_1.py` — reject substituted native originals for the existing hold, pin, coverage, activation, and restore checks. Do not add plan cases.
- [x] `docs/implementation/ext-protection-1-owner-acceptance.md` — record this baseline, the source map, this run's commands, and the unchanged missing inputs. Keep the candidate Live command and proposed date. State that no Committed target exists.

**Acceptance Criteria:**
- Given no published trust, custody provider, or installed profile, when a content or destruction path runs, then it fails closed and the dependency stays Uncommitted.
- Given the register, when the packet is read, then it names EventStore Maintainer, Hexalith.EventStore, proposed date `2026-11-02`, evidence levels 4 and 5, the candidate Live command, and every absent installed input.

## Implementation Notes

2026-10-18: published-trust dispatch authority and actor opt-in are component source. Journal, admission, revocation, and activation methods deny, so actor persistence stays closed without an installed nonrollback journal. Plan hash `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c` is unchanged. Verification from the EventStore root: server test build exit 0 with 0 warnings; authority class 4 passed, 0 skipped; Python `test_ext_protection_1*.py` 55 passed; Source mode exit 0 with `liveExecuted` false. Live was not run. Status remains Uncommitted. No Committed target exists.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence |
| --- | --- | --- |
| Blind: sole-active count ignores a different attestation digest | high | `Admitted` counts only the same digest and key version, so a second unconsumed dispatched batch still leaves `active == 1`. |
| Blind: empty authorization list still admits | maybe-false | A committed dispatch receipt is checked. Whether that receipt can exist without a separate authorization entry was not established. |
| Blind: issued revision need not be intended plus one | high | `Admitted` matches the request's issued revision and does not compare it with `IntendedIssuedGuardRevision + 1`. The fixture uses 7 and 8, so the happy path hides it. |
| Blind: shared issue and dispatch operation id | medium | `Committed` never reads `OperationId`. |
| Blind: ambiguous later-hold receipt denies the batch | false | A later hold with one committed receipt above the dispatch high water does not restrict. Any other receipt count is not a proven later hold, and denial is fail-closed. |
| Blind: unrelated receipt can suppress a hold | false | The matching receipt is selected by `ReferenceId == HoldId`. The native receipt has no operation-type field to require. |
| Blind: dispatch skips Capture and can share one guard instance | medium | The actor captures before `VerifyDispatchAsync`, but the public method does not. Serializing after the second read treats one mutable instance as two snapshots. |
| Blind: SPKI trailing bytes are ignored | low | `ImportSubjectPublicKeyInfo` discards `bytesRead`. |
| Blind: Unicode lowercase tenant passes Python capture | medium | Pre-existing verifier identity helper, not this authority. `AggregateIdentity` rejects non-ASCII before lowercasing. |
| Blind: coverage `Original` skips signature validation | medium | Pre-existing `retained_owner_batches` path. |
| Blind: exact-results are not tied to the activation original | medium | Pre-existing `exact-results` equality check. |
| Blind: coverage, pin, and activation binders leave substituted fields unchecked | medium | New binders omit coverage manifest equality, pin operation uniqueness, replacement `BatchId`, and integer type checks. |
| Blind: role pin reads use a fresh budget | medium | Pre-existing `Role.request` path. |
| Blind: completion writer proof is outside the I/O tracker | high | Pre-existing recovery loop. `CompletePayloadProtectionAsync` is still a bare `WaitAsync`. |
| Edge: second live attestation still admits | high | Same sole-active predicate as the blind finding. |
| Edge: issued revision can disagree with the signed intent | high | Same missing `IntendedIssuedGuardRevision + 1` comparison. |
| Edge: reused guard operation id admits | medium | Same unread `OperationId`. |
| Edge: unsorted or oversized targets admit on the public method | medium | Same missing capture on `VerifyDispatchAsync`. The actor path captures first. |
| Edge: one shared guard instance compares equal | medium | Same post-read comparison. |
| Edge: boolean or float coverage ordinal passes | medium | `==` treats `True` as `1` when the group ordinal is not typed. |
| Edge: substituted coverage manifest passes | medium | `bind_coverage_originals` does not compare `ManifestDigest` with the covered manifest. |
| Edge: another inventory target satisfies pin | medium | Pin outcomes in the current fixtures have status only, so a different in-inventory target still passes. |
| Edge: substituted replacement batch id passes | medium | `bind_replacement_original` does not compare `BatchId`. |
| Edge: activation batch is optional | medium | Batch and manifest are checked only when `consumptionRequests` is non-empty. |
| Edge: activation ordinal and replacement receipt are unbound | medium | Those original fields are not compared with the signed request or guard receipts. |
| Edge: boolean block and revocation revisions match | medium | Summary revisions are not required to be `int`. |
| Edge: unreferenced guard receipt may reuse an operation id | medium | Pre-existing referenced-receipt index. |
| Edge: stalled anchor read ignores the budget | medium | Pre-existing pin read. |
| Edge: canonicalization can overrun the budget | medium | Pre-existing size check runs after canonicalization. |
| Edge claim: reread does not prove one attestation | high | Same sole-active predicate. |
| Edge claim: substituted originals still pass | high | Same new binder gaps. |
| Verification: command re-entry misses the barrier check | high | Filed evidence: the reminder test throws inside `PayloadProtectionIoAsync` before `EnsureStateCacheBarrierAsync`. Pre-existing I/O tracker. |
| Verification: writer proof is not quiesced | high | Filed evidence: `CompletePayloadProtectionAsync` is not stored in `_unfinishedPayloadProtectionIo`. Pre-existing recovery loop. |
| Verification: admission, revocation, and activation denies are untested | medium | The three methods return false and no test calls them. Changing one to true would leave the class green. |

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — expected: exit 0.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Security.PublishedTrustDeletionConsumptionAuthorityTests -noColor` — expected: all passed.
- `python3 -m unittest discover -s tools/tests -p 'test_ext_protection_1*.py' -v` — expected: all passed.
- `python3 tools/verify-ext-protection-1.py --mode Source` — expected: exit 0 and `liveExecuted` false. Do not run `--mode Live`.
