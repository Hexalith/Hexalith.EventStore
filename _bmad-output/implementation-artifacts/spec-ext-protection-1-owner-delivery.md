---
title: EXT-PROTECTION-1 owner artifact and acceptance packet
type: feature
created: 2026-10-18
status: in-progress
route: dispatch
review_loop_iteration: 0
baseline_commit: 9b062d0d4f506bf45b7fdcd8e54189599a36ff9b
context:
  - AGENTS.md
  - references/Hexalith.AI.Tools/hexalith-llm-instructions.md
  - ../agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - ../agents/_bmad-output/implementation-artifacts/story-5-4-dependency-reconciliation-2026-10-18.md
---

<frozen-after-approval reason="user-authorized complete owner delivery; approval reserved for owner record before Live">

## Intent

Deliver the production EventStore-owned protection engine and pass-capable Live verifier required by EXT-PROTECTION-1, including its full v21/v23 extensions. The register is authoritative; this spec organizes implementation and cannot narrow it. Candidate codecs, occurrence registry/protector, and deletion consumption actor currently have only synthetic providers and no production hook composition.

## Boundaries & Constraints

Preserve unrelated local changes and all fail-closed behavior. No Agents register edits, consuming Agents execution, availability changes, Live verifier run, provisioning, or destructive infrastructure actions. Do not fabricate installed custody or Product authority. Reuse existing canonical capability codec and durable nonrollback actor protocols. Each C# type has its own file. Local checks use Debug and project references; build/test projects individually. The intended canonical target must contain both implementation and verifier; never propose Committed against a placeholder or refusal-only command. The user authorized implementation and a reviewable acceptance packet now, reserving acceptance of the owner record before Live. Do not insert an intermediate approval checkpoint.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Required result |
|---|---|---|
| Protect | interaction event or directory creation outbox with explicit complete target alias | Only ProtectedContent sealed under the same custodied interaction root; other fields preserved; authenticated occurrence uniqueness |
| Read/distribute | exact stored event/snapshot/cache | Trusted unseal equals input; broker/read model stays sealed; destroyed content becomes typed Erased on replay and unreadable PayloadUnprotectionOutcome |
| Custody | per-tenant KEK and DigestKey references | WrapDek/UnwrapDek/PinDek/UnpinDek/DestroyDek; no domain key bytes; no recreation after tombstone or restore |
| Manifest | exact sorted multi-target batch and signed capability | All targets or none, pin rejects, ordered durable receipts; exact retry and lost-result lookup preserve original vector |
| v21 | committed ES256 anchor, exact online guard issue/dispatch/sole-active attestation | Reject substituted, stale, revoked, unavailable or pre-dispatch restrictive facts; later holds remain ordered at dispatch |
| v23 | block/reserve and compromise/re-attestation races | One same-owner winner; pending/unknown blocks deny; reserved recovery uses original; replacement activation compares key block revision; admission blocks never reopen |
| Qualification | accepted exact clean checkout and reference-only hash-pinned installed profile | Real passing path, independent persisted-state observer, unique retained attempts and original-operation recovery journals |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.PayloadProtection/`: internal normative v2 codecs/core and `InteractionOccurrenceProtector`; `IsPackable=false`; no hook implementation. Preserve existing goldens and canonical bytes.
- `src/Hexalith.EventStore.Server/Security/`: `InteractionOccurrenceRegistryActor` and `DeletionConsumptionActor` supply recoverable independent-anchor state machines. Authorities default absent. `IAtomicDeletionManifestProvider` has no production implementation. `DaprDeletionProtectionOwner` is the nonpublic caller seam.
- `src/Hexalith.EventStore.Contracts/Security/`: `ProtectionTarget`, capability/manifest canonical identity, typed outcomes, existing occurrence-aware default hook members. Complete identity includes tenant, interaction, explicit alias.
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs`, `SnapshotManager.cs`, `Actors/AggregateActor.cs`: legacy hook callers lack occurrence context. `EventPublisher.cs` and `Projections/ProjectionEventWireBuilder.cs` currently unseal before distribution and verify a plaintext logical digest.
- `../platform/src/`: custody lifecycle candidates exist but do not assume an exposed production WrapDek/UnwrapDek/atomic manifest endpoint. Inspect exact seams; an invented remote provider contract alone cannot prove delivery.
- `tests/Hexalith.EventStore.PayloadProtection.Tests/`, `tests/Hexalith.EventStore.Server.Tests/Security/`: synthetic component evidence; extend meaningful tests and keep that evidence distinct from Live.
- `tools/`: existing Python owner verification/evidence patterns; add an EventStore-owned runner and tests without running Live.

## Tasks & Acceptance

- [ ] Production hook engine and explicit opt-in registration, field envelope/target contract, occurrence context wiring, sealed distribution, typed Erased replay, snapshot/cache handling, engine identity reporting.
- [ ] Custody implementation/adapter using actual EXT-SECRETS-1 seams, tenant root ownership, key lifecycle, pins, atomic manifest physical provider and exact lookup; fail closed when absent.
- [ ] Complete signed capability/online authority for v21 and bind to existing v23 owner protocol, including authenticated registrar, nonrollback restore, replacement-key activation comparisons and exact recovery.
- [ ] Pass-capable Live runner with accepted clean-target verification and reference-only pinned profile/anchors/plan. Five minimum proofs plus all matrix cases, source and target replay, containment and same-alias coverage, partial-provider failure, backup/restore and cross-tenant substitution. Independent observer must inspect persisted end state. Retain unique attempt directories and fsynced original operation journals; unknown destruction recovers by exact lookup only.
- [ ] Focused source/component checks followed by narrow meaningful integration checks; record exact commands/results and blockers. Do not run Live.
- [ ] EventStore owner packet mapping every register clause to source/tests/gaps, exact Agents-root Live command, installed input references/trust/hash bindings, owner/repository/full canonical target, future proposed date, evidence levels/status. No Committed claim unless complete at canonical target.

Acceptance: Given installed qualified inputs, Live can pass the full contract. Given absent authority/provider, any effect fails closed. Given response loss or restore, exact original lookup recovers without a new destructive identity. Given no owner acceptance, no Live execution occurs.

## Current continuation — 2026-10-22

Resume the existing full authorized delivery without an intermediate approval. Preserve its original baseline and frozen intent. Current observed HEAD is `995117f9b899a2994e99257091b0ea35cfbe5090`; unrelated Folders/testing/evidence changes are concurrent and must remain intact. This continuation does not narrow the full acceptance: external gaps keep the story in-progress.

Implementation focus for this pass:

- `src/Hexalith.EventStore.Server/Security/PublishedTrustDeletionConsumptionAuthority.cs`: implement the missing `VerifyBlockedReplacementAsync` source pass path and fully bind activation to canonical captured replacement, actual ES256 signature, exact complete manifest, retained guard batch capability/JWS/signing request/actual issue, distinct issue and dispatch receipts/operations. Use a dedicated exact issue correlation for blocked successors: no dispatch requirement and no destructive grant. Activation must admit the actual `ReplacementAwaitingActivation` guard phase and must not preempt the protection owner's atomic replacement-key-block result. Require replacement receipt equals exact guard issue receipt; reject stale/substituted/missing/ambiguous evidence. Read guard snapshots freshly and detach mutable collection ownership before checking. Never infer current linearizability from two equal reads.
- `src/Hexalith.EventStore.Server/Security/IPublishedTrustRevocationEvidence.cs`: extend with default-deny exact blocked-replacement evidence/currentness queries only if required; missing installation remains deny. Do not issue any anchor, signature or revocation locally.
- `tests/Hexalith.EventStore.Server.Tests/Security/PublishedTrustDeletionConsumptionAuthorityTests.cs` and a focused new test/fixture type if useful: meaningful valid pass plus tenant, key, manifest/target, attestation, signature, signing-id, actual issue, receipt, dispatch and block revision substitution; absent/pending/throwing evidence, exact original checks and cancellation. Use genuine ephemeral ES256 signed fixtures. Actor persisted-state reconciliation/retry/lookup/blocked-reserve and second-compromise tests must preserve the original vector with zero provider effects. Keep existing pass paths valid.
- Do not implement the separate owner-state bootstrap by accepting revision zero or absent state. `ValidateStateAsync` incorrectly equates owner and guard revisions and its synthetic state proof is not the actor transition proof. The missing independent installation/genesis/current-owner protocol must be delivered before a production adapter can safely fix that seam. Record both the EventStore source adapter gap and external artifact requirement, rather than hiding it as configuration.
- Do not change owner docs or this spec's existing sections during the implementation handoff. Root agent retains these records and external evidence. Do not commit/stage/push or change any dependency pointer, register, plan, goldens, opt-in/default registration, or production infrastructure.

Current evidence directory: `/home/administrator/hexalith-protection-evidence/ext-protection-1/delivery-20261022T153216Z-595b9f24`. Focused commands and results must be retained there or reported exactly. Use the existing test project in Debug with `UseHexalithProjectReferences=true`; coordinate build runs sequentially. Full source contract remains incomplete and must not be marked done, Committed, Available, installed or qualified. G1 physical custody and current independent authority require the Platform owner handoff. No Live/cohort attempt is authorized before prerequisites.

## Implementation Notes

2026-10-23 observation at EventStore HEAD `af2c0d56744f97d69e20803b42f3abea18c7169e`: the current continuation's source was already present, so this pass changed no protection source, registration, plan, golden, register, or dependency pointer. Debug/project-reference Server build passed with zero warnings. Replacement tests passed 98; deletion-authority and consumption-actor tests passed 100; offline verifier tests passed 56; Source mode kept the 41-case plan SHA-256 `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c` and `liveExecuted: false`. The blocked packet and exact null runtime inputs are in `/home/administrator/hexalith-protection-evidence/ext-protection-1/delivery-20261023T051052Z-observation/`. `TargetVersionOrCommit` stays null. Platform semantic custody remains `in-progress` at `81dd98f2ecadaba63bfae0ee79a3489284dfa2c6`, and `wrap`/`unwrap` remain refused. Full-delivery tasks stay open. Status stays `in-progress`. No Live, cohort, Agents consumer, or Git mutation ran.

2026-10-22 current continuation delivered the exact retained-signature/issue/dispatch successor verification and missing blocked-replacement source pass path in six source/test files. Three review layers returned nine findings: two refuted, six test corrections implemented, one pre-existing installed fixture gap retained as blocking G2/G5/G6. The follow-up verification reviewer confirmed the coherent invalid-signature cases fail if either local cryptographic check is removed. Final selected Debug/project-reference component run passed 285 with zero failures/skips/unrun; offline verifier passed 55; Source mode retained the unchanged 41-case plan and liveExecuted:false. Build warnings/errors are zero on successful runs; failed intermediate attempts are retained. Evidence and the exact executable candidate command are in `/home/administrator/hexalith-protection-evidence/ext-protection-1/delivery-20261022T153216Z-595b9f24` and the latest owner packet section.

The full contract remains incomplete: no qualified semantic Dapr custody component/physical provider, independent installation/genesis/current-owner protocol or concrete current-state adapter, installed exact guard authority, mixed-root snapshots, complete owner fixtures/verifier chains or acceptance-ready immutable target exists. No permission to infer genesis or equate owner/guard revisions was added. No Live/cohort/Agents execution, dependency status edit, Story 8.3 enablement or Git mutation occurred. A concurrent external commit advanced HEAD to `cb414530223f77cc845450a4877a4472559de2ad`; unrelated changes and historical evidence were preserved. The original frozen intent and baseline remain unchanged; all unchecked full-delivery tasks stay open and status stays in-progress.

2026-10-18 current owner-delivery checkpoint from clean EventStore `main` HEAD `993be1bcc03fea06b9cd451ad04d57783a119e83` and read-only clean Platform HEAD `5f18593639832d1245577dd93ec07687517d3acb`: verified the exact missing production lifecycle/key/binding/atomic-manifest providers and absent accepted-profile/lifecycle references, and rechecked the hash-bound historical `ObservedRevival` result without rerunning its native probe. The approved erasure-first stop remains in force; no substitute provider or source-code change is justified by this checkpoint. Debug/project-reference Server build passed with zero warnings/errors; 181 focused authority/owner/registry/guard component tests and 56 offline verifier tests passed. Source mode retains the unchanged 41-case plan and `liveExecuted:false`. Exact commands/results, baseline and source/binary bindings are retained in `/home/administrator/hexalith-protection-evidence/ext-protection-1/owner-delivery-current-20261018T140417Z-52d12c204ffc4c0ab45a5232353a1e39/` and the owner packet's current checkpoint. G1–G7 and all full-delivery acceptance tasks remain open; there is no complete canonical target or Committed proposal. No Live/cohort/Agents execution, provisioning, destructive infrastructure operation, register/status edit or Git mutation occurred. Only the existing packet/spec evidence notes were updated outside the frozen content.

2026-10-18 guard-membership continuation from canonical `main` HEAD `55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b`, preserving all nine pre-existing modified files: reproduced and corrected malformed/missing hold membership and ordering evidence, out-of-observation registration/dispatch revisions, reader-owned collection changes after guard-byte comparison, and registration receipts whose reference previously came from the caller rather than the committed HoldId. Actual guard-owner persisted-state tests cover registration, exact retry and independent release-authority references; only their shape is checked here, not installed authentication. Final Debug/project-reference checks pass 36 authority cases, 555 affected Server cases, 637 PayloadProtection cases and 56 offline verifier cases. Source mode retains the unchanged 41-case plan and `liveExecuted:false`. Exact commands, failed regressions, redacted local Aspire baseline and final source/binary hashes are retained in `/home/administrator/hexalith-protection-evidence/ext-protection-1/owner-guard-facts-20261018T134430Z-4c27480a1a114c0cb66de622cb618552/` and mapped in the owner packet's guard-membership section. These are source/component corrections only. G1–G7 remain open, including the erasure-first `ObservedRevival` stop; no complete canonical target, Committed proposal, Live/cohort/Agents execution, provisioning, destructive infrastructure operation, register edit or Git mutation is claimed.

2026-10-18 owner-delivery continuation from clean canonical `main` at `f43b121c6c7539487b1f38b3c28508e3ab6264cc`: corrected completion-task ownership through the underlying cancellation-ignoring registry mutation, actor reentry and both runtime cleanup hooks; corrected dispatch admission to require one current credential per exact batch while permitting distinct accepted/containment batches. Regression runs first reproduced both defects; final Debug/project-reference checks passed 530 affected Server cases, 637 PayloadProtection cases and 56 offline verifier cases, with zero build warnings/errors. Source mode reports the unchanged 41-case plan and `liveExecuted:false`. Exact commands, failed intermediate attempts, final logs and hashes are recorded in the owner packet's dated recovery/dispatch section and `/home/administrator/hexalith-protection-evidence/ext-protection-1/owner-recovery-20261018T121959Z-7e95f62fe78a4a9ba65b90ca7eb4407b/`. The logical deadline still bounds recovery response; cancellation-ignoring original I/O may occupy the actor through cleanup beyond that deadline. No transport cancellation or physical exactly-once claim follows. The approved option A feasibility's `ObservedRevival` result stops production construction until a real all-copy erasure hierarchy and independently surviving authority are established. G1–G7 remain explicit; full delivery stays in-progress, with no proposed Committed target, Live/cohort/Agents execution, provisioning, Git mutation or register edit.

2026-10-18 continuation from `c0c08d450dff89faaeea2bb88dd570a589ce203d`: authoritative Platform native discovery and installed HMAC mappings establish the provider technology but explicitly no lifecycle/all-copy qualification. G1 needs the concrete provider decision now recorded in Platform's `docs/implementation/ext-secrets-1-dek-provider-decision-2026-10-18.md`; do not substitute HMAC or fabricate a remote DEK service. Independent verifier capability/native-receipt work is tracked in `spec-ext-protection-1-capability-evidence.md`. Independent G4 work adds pre-commit durable reminder enrollment, activation rearming and bounded idle original-completion recovery; tests must prove persisted originals, outage retention, scheduler refusal, completion-save uncertainty and no re-encryption. These are source corrections, not physical qualification. Original baseline/frozen intent remain preserved.

Initial tree clean on main. No Agents files may change. Source changes alone do not establish Live evidence or dependency availability. If an independently owned prerequisite is actually missing, document its exact missing API and remaining implementation gap rather than inventing authority or concealing it behind a remote executor.

## Spec Change Log

## Review Triage Log

### 2026-10-22 component review

All three independent layers completed against the six-file current component diff; eight blind findings, no edge findings and one verification gap. Full-delivery status stays `in-progress`; this scoped review does not establish installed qualification. Each finding is retained separately in the external `review-v1-triage.json`.

| Finding | Verdict / route | Evidence |
| --- | --- | --- |
| blind-1 | false / reject | The same-owner ActivateAsyncCoreAsync compares ExpectedKeyBlockSetRevision with the actual current protection ledger before any Unconsumed transition, and records a present replacement-key block instead. Historical guard batch.BlockSetRevision is not the current tenant block-set. Existing actor tests cover stale comparison and corrected revision; restoring that local equality would reject legitimate unrelated-key revocation progress. |
| blind-2 | false / reject | IGovernanceScopeGuard.ReadAsync is the independently qualified exact-state boundary. GovernanceScopeGuardOwner uses DaprGuardedStateTransaction.ReadGuardAsync with strong reads, current enrollment, and ValidateCellAsync before returning. The journal proof is not the sole guard authentication. A noncompliant installed reader could lie, but no such installation is claimed or enabled; missing installed exact-cell authority remains G2 and is named in the owner handoff. |
| blind-3 | medium / patch | The fixture currentness callback derives expected bytes from the mutable guard State, so it does not demonstrate stale/different observation rejection against a separately retained head. Add independent revision/digest fixtures. |
| blind-4 | medium / patch | Exact MatchesBlockedReplacement and currentness callbacks can reject changed requests before local checks. Add permissive-evidence local correlation negatives with valid positive preconditions. |
| blind-5 | medium / patch | The new fixture always approves the final journal and lacks refusal, fault, and during-await cancellation tests at these entry points. Add targeted boundaries. |
| blind-6 | medium / patch | Retained verification now considers revoked entries. Add absence and duplicate/conflicting revoked-plus-active trust negatives to protect unambiguous retained-key selection. |
| blind-7 | medium / defer | The installed end-to-end guard mirror, next reattestation and provider fixture is a pre-existing G2/G5/G6 full-delivery gap. The component tests deliberately use a hand-built authenticated observation and synthetic owner bootstrap. Actual installed composition depends on the missing custody/current-authority artifact; retain as blocking qualification work, never infer it from these tests. |
| blind-8 | medium / patch | New entry points capture caller arrays but the tests mutate only guard arrays. Add target/revocation array mutation across callbacks/awaits and inspect captured originals/proofs. |
| verification-1 | medium / patch | The signature negative can reject via mismatched guard metadata, and blocked request equality can reject earlier. Install consistently correlated but cryptographically invalid detached JWS and assert both real successor entry points reject it; removing either signature call must turn a case red. |

## Verification

Build/run focused PayloadProtection and Server security tests with .NET 10, Debug, `UseHexalithProjectReferences=true`; invoke xUnit v3 assemblies directly for filters. Run Python verifier tests offline, syntax/source checks, and relevant server integration components. Capture logs outside the worktree. No invocation of the Live owner command.

Implementation checkpoint: 635 codec, 245 pre-correction affected Server and 145 guard/owner/registry checks passed. Post-review consumer tests are recorded in the owner acceptance packet and retained command manifest. The current result is a partial owner artifact, not a complete Committed proposal; production custody/authority/physical-provider/qualification gaps remain explicitly recorded in docs/implementation/ext-protection-1-owner-acceptance.md. No Live run or Git mutation was performed by this work. Concurrent unrelated secret-scan work advanced main to 37bdc90a72b9f0ea547852e903334cc8cc541641 and was preserved.

### Review 1 triage (all three reviewers collected before correction)

| Finding | Verdict | Verified evidence / route |
| --- | --- | --- |
| Blind 1 nested extra envelope | high | ReadTarget validates only the supported top envelope; additional nonselected nested content can remain plaintext. Patch strict rejection and regression. |
| Blind 2 post-save completion loss | high | Contexts are call-local and EventsStored resume bypasses preparation. Correct durable original recovery before publishing/resuming. |
| Blind 3 snapshot lost-save result | high | Manual readback catch returns Created without executing completion. Patch original completion in confirmed branch. |
| Blind 4 pre-save reservation abandonment | medium | Preparation exceptions lose the returned contexts; independent registry retains pending original and retries can reuse it, but no cleanup worker exists. Retain as G4 until exact never-written recovery is composed; never infer absence. |
| Blind 5 production erased replay | high | DaprLogicalEventReader intentionally uses external unreadable route and production rehydration consumes it. Correct distinct trusted replay path or retain G4 explicitly. |
| Blind 6 inherited role credentials | high | subprocess.run inherits os.environ containing every credential validated by profile. Patch explicit per-role environment. |
| Blind 7 dependency drift | high | Only executable and anchor are rechecked per call. Patch full installation inventory and engine binary checks. |
| Blind 8 store mismatch | high | InspectInstallation merely checks nonempty storeIdentity. Patch same store/deployment binding across roles before enrollment. |
| Blind 9 incomplete copies | high | Required kinds are global, not per target; snapshot/cache kinds absent. Patch exact per-target kind inventory. |
| Blind 10 fake sealed bytes | high | Empty bytes and a Sealed string satisfy plaintext absence. Patch actual envelope shape/magic/target checks and nonempty buffers. |
| Blind 11 broad negative status | high | DependencyNotAvailable can satisfy every Reject step. Patch specific terminal outcomes and fixture-success preconditions. |
| Blind 12 empty hold order | high | all(empty) passes. Patch nonempty operation-bound ordering proofs. |
| Blind 13 capability/receipt-chain completeness | high | Transport signature authenticates observer, not deletion JWS or original chains. Remains G2/G6; no Committed claim permitted. |
| Blind 14 empty recovery | high | Zero original files returns success. Patch nonempty/authenticated original manifest and distinguish recovery from qualification. |
| Edge 1 nested extra envelope | high | Same demonstrated source defect as Blind 1; same correction. |
| Edge 2 postcommit original recovery | high | Same demonstrated durable-context defect as Blind 2; same correction. |
| Edge 3 missing second-target replay | high | evaluate checks kind set only. Same correction as Blind 9. |
| Edge 4 missing snapshot/cache/backup | high | Plan names do not require actual copy kinds. Same correction as Blind 9. |
| Edge 5 empty hold order | high | Same vacuous assertion as Blind 12. |
| Edge 6 production logical erased replay | high | Traced production reader external API call. Same source gap as Blind 5. |
| Verification 1 actor completion coverage | high | Persistence test invokes completion manually; actor tests use no-op. Add actual-engine actor persisted-state tests. |
| Verification 2 actor erased replay coverage | high | Service helper tested but not actor domain invocation. Add actual rehydration test. |
| Verification 3 logical engine digest coverage | high | Existing logical tests use legacy substitutes. Add actual-engine read and missing/changed-digest regressions. |
| Verification 4 offline tests absent CI | medium | New file not selected by workflows/ci-local. Add explicit offline unittest step to ordinary payload protection workflow. |

The full production implementation gaps G1–G6 remain blockers; tests passing after local corrections cannot close them. No broad rollback of working safeguards or weakening of expected outcomes is authorized by these findings.

### Correction disposition

Nested reserved fields now refuse before key/registry work, and the default provider scans reserved JSON shapes regardless of the serialization label. Actor originals are committed as a content-free completion journal in the same atomic batch as events/snapshots. Recovery validates original persisted identities and metadata/digest, uses one independent bounded budget, requests exact independent writer proof, and runs before resume/publication. A separate internal production reader route admits tombstone-proven Erased replay; external readers remain unreadable. Component tests cover those actual engine/actor/reader paths, including interrupted completion and source-digest substitutions. Remaining mixed-root snapshot and real authority/operational qualification stay G4.

The offline harness corrections isolate role credentials, recheck all installed bytes before every role call, bind all roles to the same store/deployment, require per-target copy/replay inventories, inspect envelope structure, restrict negative outcomes, reject empty hold ordering evidence, and separate hold/dispatch ordering from post-dispatch owner races. Originals are independently registered before mutations; recovery reconciles their complete retained inventory and reports RECOVERED rather than a qualification PASS. 22 offline checks pass. Complete deletion JWS/raw receipt-chain verification and independently installed scenario drivers remain G2/G5/G6; transport signatures and a plan do not satisfy those requirements.

The spec remains in-progress because its frozen full-delivery acceptance is unmet. No incomplete component is marked Committed, Available, or done; there is no owner approval request for an invented canonical target.
