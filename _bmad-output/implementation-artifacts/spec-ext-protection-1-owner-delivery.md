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

## Implementation Notes

2026-10-18 continuation from `c0c08d450dff89faaeea2bb88dd570a589ce203d`: authoritative Platform native discovery and installed HMAC mappings establish the provider technology but explicitly no lifecycle/all-copy qualification. G1 needs the concrete provider decision now recorded in Platform's `docs/implementation/ext-secrets-1-dek-provider-decision-2026-10-18.md`; do not substitute HMAC or fabricate a remote DEK service. Independent verifier capability/native-receipt work is tracked in `spec-ext-protection-1-capability-evidence.md`. Independent G4 work adds pre-commit durable reminder enrollment, activation rearming and bounded idle original-completion recovery; tests must prove persisted originals, outage retention, scheduler refusal, completion-save uncertainty and no re-encryption. These are source corrections, not physical qualification. Original baseline/frozen intent remain preserved.

Initial tree clean on main. No Agents files may change. Source changes alone do not establish Live evidence or dependency availability. If an independently owned prerequisite is actually missing, document its exact missing API and remaining implementation gap rather than inventing authority or concealing it behind a remote executor.

## Spec Change Log

## Review Triage Log

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
