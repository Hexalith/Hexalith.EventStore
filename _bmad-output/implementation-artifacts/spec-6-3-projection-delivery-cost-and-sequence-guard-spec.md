---
title: 'Story 6.3: Projection Delivery Cost And Sequence Guard Spec'
type: 'feature'
created: '2026-10-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '6cd4e738ff259c6a6602650a8f49fd8373e49524'
story_key: '6-3-projection-delivery-cost-and-sequence-guard-spec'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** FR33-C3/NFR8 lack an approved projection cost/sequence contract. Existing delivery reads events even when current.

**Approach:** Write `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md`, the G-NFR8/OR16 gate. Intake approval authorizes document work only.

## Boundaries & Constraints

**Always:** Preserve Stories 1.18/1.19 and AD-7/8/12/13/20: tenant/domain/aggregate/projection scope, EventStore `MessageId`, v2 CAS/fences, stable dispatch/batch identity, proof horizons, staging and promotion/readback. Freeze numeric budgets, workloads, pass conditions and compatibility vectors. Distinguish event-read savings from history-sized identity-proof bytes/CPU. Sequence equality alone cannot establish content continuity or `Current`.

**Out of scope:**

- Runtime/test/UI edits, dependencies/topology, deployment and consumer migration.
- Snapshots, upcasting, cancellation-interface changes and sharding.
- Generic store/proof redesign, automatic handler conversion and new evidence tooling.

Never truncate proof, sort malformed input, publish staged models or infer lifecycle from transport success. No production qualification claim. Keep 6.3 backlog and 6.4 unauthorized until named final approval binds canonical bytes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Current | Scoped completion matches head/source evidence | Zero events, handler/batch calls, model writes or checkpoint advances | Invalid evidence cannot authorize success |
| Tail | Checkpoint C behind admitted H | Contiguous C+1..H; count every pass in delta and P | Gap/conflict/reversal/unreadability/cancellation/limits preserve proven progress |
| Capability | Incremental, mapped/shared, full or legacy | Explicit negotiation; durable prior state or bounded canonical fallback | A page is never a whole stream |
| Recovery | Duplicate, race, crash, changing head | Fenced stable identities converge; preserve complete live model | Typed retry/reconciliation; no invented freshness |

</frozen-after-approval>

## Code Map

Inventory sources; no runtime edits:

- `src/Hexalith.EventStore.Server/Projections/`: `ProjectionUpdateOrchestrator.cs` (legacy/rebuild), `NamedProjectionDispatchCoordinator.cs` (existing mapped suffix), `ProjectionDeliveryIdempotencyCoordinator.cs`, `ProjectionDeliveryState.cs`, `DaprProjectionDeliveryStateStore.cs` (proof/CAS/cutover).
- Same directory: `EventStoreProjectionDeliveryHistoryReader.cs` uses P=1; retry uses 256. Rebuild defaults are P=256, 10,000 events/67,108,864 bytes; count actual pages per mode.
- `src/Hexalith.EventStore.Server/Actors/AggregateStreamMetadata.cs` lacks head content binding; `src/Hexalith.EventStore.Server/Events/EventPersister.cs` owns append metadata.
- `src/Hexalith.EventStore.DomainService/`: `IDomainProjectionHandler.cs` requires full replay; `IAsyncDomainProjectionIncrementalReplayHandler.cs` opts into verified single-event append.
- `_bmad-output/specs/spec-correct-paged-rebuild-and-replay-equivalence/`: `SPEC.md`, `rebuild-semantics.md`; historical `1-13-projection-handler-delivery-idempotency.md` in implementation artifacts preserves current 1.18's contract.
- `tests/Hexalith.EventStore.Server.Tests/Projections/`: `ProjectionDeliveryIdempotencyCoordinatorTests.cs`, `NamedProjectionDispatchCoordinatorTests.cs`, `EventStoreProjectionDeliveryHistoryReaderTests.cs`, `ProjectionRebuildProductionPathTests.cs` — reuse preservation/cost seams.

## Tasks & Acceptance

**Execution:**

- [x] `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md` — inventory every head/range/checkpoint/state read/write, dispatch/retry/fallback, lifecycle, metric and persistence boundary. Specify source authority, minimal additive evidence, handler negotiation, commit/crash recovery, migration/mixed-writer/rollback rules and rejected alternatives.
- [x] Same artifact — freeze numeric metadata/range/event/page/handler/mutation/byte budgets, workload/page model and failure ceilings, including repeated passes and history-sized proofs. Map preservation/compatibility vectors to production handler/store tests; specify payload-free telemetry and truthful lifecycle/provenance.
- [x] Same artifact — record exact validation commands, persisted equality/pass conditions and reproducible normative SHA-256. Seek detached named approval of final bytes and explicit 6.4 authority; leave approval pending until received.

**Acceptance Criteria:**

- Given the artifact, when checked against Story 6.3, then all ten clauses have concrete sections, budgets, source/test references and no unresolved design decisions.
- Given Stories 1.18/1.19, when preservation is checked, then duplicate/gap/fence/horizon, paging/promotion, lifecycle/replay, cancellation and protected-event invariants remain intact.
- Given completion, when approval is checked, then final digest, named Epic 6/architecture/Test roles, date and explicit 6.4 authorization agree; otherwise gates remain closed. Runtime acceptance belongs to 6.4.

## Implementation Notes

- Created the [canonical specification](spec-projection-cost-sequence-guard.md) with ten numbered acceptance sections, W0–W6, source-v1 golden hashes, explicit history-sized proof costs and production-path preservation vectors.
- Source/completion anchors are proposed additive evidence for Story 6.4; no runtime, test, UI, dependency or topology changes were made. The listed runtime commands are future validation requirements, not executed evidence.
- All document tasks and the final named-approval criterion are complete. On 2026-10-15 Jérôme Piquot (`jpiquot`), project owner and only contributor, accepted the requested Epic 6, architecture and Test owner roles, approved normative SHA-256 `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711` and explicitly authorized Story 6.4. Story 6.3 is done for the specification gate; Story 6.4 remains backlog. Runtime NFR8 and assurance/readiness gates are unchanged.

## Spec Change Log

- 2026-10-15: Resumed the existing ready-for-dev intake. Preserved its frozen intent and baseline. Authored the canonical document without opening runtime or approval gates.
- 2026-10-15: Applied the three-layer review corrections to the canonical document: actual Idle binding, same-reservation partial-tail recovery, pre-effect admitted-event matching, framing/lookahead accounting, bounded retry observation with occupancy-dependent shard costs, explicit guard activation, semantic replay equivalence, CI package-suite execution and truthful zero-head behavior. Kept the source codec, golden vectors, existing writer-v2 protocols and all approval gates.
- 2026-10-15: Recorded the sole owner's session approval, "I Jérôme Piquot Owner and only contributor authorize and approve", in response to the final digest/three-role/6.4 authorization request. Preserved the approved normative bytes and frozen intent. Closed this build and Story 6.3 after owner acceptance, superseding the generic workflow's review handoff; reconciled the sprint and planning specification records without claiming runtime delivery or AD-12 assurance evidence.

## Review Triage Log

All findings below concern the canonical output document, not the frozen build intent. Route `patch` means correcting that unapproved document; it authorizes no runtime change.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| B01 lifecycle release invalidates current proof | high | `ProjectionLifecycleActor.CompleteDeliveryWriteAsync` increments the persisted revision after delivery-row completion, so the draft's strict generation equality cannot survive normal release. Patch the completion/idle binding and charge its actual transitions. |
| B02 partially committed multi-event tail cannot resume | high | Section 6 allows model progress beyond C while section 3 requires prior selected state at C. Patch explicit same-reservation partial-progress recovery and stable per-event identities, preserving existing receipt/fence protocols. |
| B03 application reread can differ before effects | high | A cumulative check at the end of a separate application pass is too late to prevent an altered event from reaching a handler. Patch per-event comparison to immutable admission evidence before every effect, including W6 coverage. |
| B04 byte packing hides lookahead reads | medium | Greedy packing needs to read an envelope before knowing it does not fit; the returned-envelope-only event-key ceiling omits that read. Patch separate returned-envelope and lookahead accounting. |
| B05 uniform-page equation omits array framing | medium | At P=256, 256×8192 already equals the full 2-MiB cap; both 8192-byte and 8191-byte event fixtures can overflow after framing. Patch the fixture bound/equation and exact-boundary vector. |
| B06 eight-activation parking is not existing behavior | medium | Worker/coordinator saturate Attempt while `GetDueAsync` continues selecting pending work. Patch the description and bounded measurement model or fully specify a compatible transition; preserve existing retry semantics rather than imply an existing lifetime bound. |
| B07 shared retry ledger has no 16-KiB occupancy bound | medium | Each of 64 shard rows includes many work items, so its bytes depend on other aggregates. Patch separate actual shard occupancy/byte accounting without imposing a new scheduler limit. |
| B08 guard marker shape/read budget is ambiguous | medium | `ReadAsync` reads `ProjectionDeliveryStateKeys.WriterProtocol`; a separate guard key would add an uncharged read. Patch the exact persisted marker/key and bounded scope-admission representation with matching current-path costs. |
| B09 oracle incorrectly requires cross-mode protocol IDs to match | medium | Per-event incremental batches and a full-replacement batch derive different legitimate manifests/receipts. Patch semantic cross-mode equality plus independent protocol-identity assertions within each mode. |
| B10 package-mode suites are never executed by the plan | medium | Section 10 runs Debug/source assemblies, then only restores/builds Release/package assets. Patch explicit CI Release/package suite execution, keeping local development in Debug/source mode. |
| B11 zero-head current fixture has no setup path | medium | The preserved empty rebuild performs no model write, and a new zero row has no completion proof. Patch concrete empty/unknown qualification behavior without inventing a new empty-model receipt protocol. |
| E01 uniform-page equality at 8191–8192 bytes | medium | Independently confirmed the framing overflow described in B05. Same patch group; retain this finding as a separate review result. |
| E02 greedy byte packing needs an unreturned lookahead | medium | Independently confirmed the missing read described in B04. Same patch group; retain this finding as a separate review result. |

Verification-gap layer: no findings. All three layers returned before triage. No finding is deferred or treated as owner approval.

Resolution: all 13 layer findings above were corrected in the canonical document (the two edge findings share fixes with B04/B05). Parent audit also corrected the old-marker description to preserve existing serialized derived members rather than impose a four-property wire shape. No runtime defect was claimed fixed and nothing was deferred. Final document identity is normative SHA-256 `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`.

## Verification

- `git diff --check` — no whitespace errors; check untracked bytes separately.
- Cross-check Story 6.3/FR33-C3/NFR8/G-NFR8/OR16 and recompute final normative digest.
- Bind Server coordinator/history/rebuild, DomainService compatibility and `NamedProjectionDispatchLiveSidecarTests` commands in the artifact. No runtime test execution for this document-only story.
- PASS (2026-10-15): `git diff --check` and explicit Python audit of both untracked Markdown files: UTF-8, LF, no BOM/trailing whitespace, final newline, ten numbered clauses, W0–W6, referenced C# filenames/test methods and unique normative markers.
- PASS: parent independently reproduced all five source-v1 golden hashes; checked framing/lookahead boundaries, 11 current-path metadata-key reads and byte ceiling, 3A+5/3A+1 proof bounds, shared-shard occupancy and working-byte arithmetic; recomputed the recorded normative digest above.
- PASS (pre-approval audit): frozen intent was byte-identical to the input captured in the review diff; no tracked file or index mutations; sprint Stories 6.3/6.4 were backlog at that stage. The owner-approved bookkeeping update below supersedes the pending status only.
- Matrix audit: Current maps to section 2.4/W0; Tail to section 3/W1–W4; Capability to section 4/7; Recovery to sections 5–6/W6. These are design and future runtime-validation mappings. Runtime tests were not executed and runtime acceptance is not claimed, as the frozen document-only scope requires.
- Final acceptance received (2026-10-15): Jérôme Piquot (`jpiquot`), sole owner/contributor holding the requested Epic 6, architecture and Test owner roles, approved the unchanged normative digest and authorized Story 6.4 in this conversation. The detached record preserves the exact owner statement and its SHA-256. Story 6.3 is done and OR16's specification obligation is resolved; Story 6.4 stays backlog and no runtime, independent-review or CI-sealed assurance result is claimed.
- PASS (approval reconciliation): exact normative bytes remain unchanged; canonical/build/sprint/planning records bind the same approved digest, owner and date; the three requested approval roles and explicit Story 6.4 authorization are recorded. The historical readiness assessment keeps its overall FAIL and gains only a dated specification reconciliation.
- PASS (commit preflight): pinned `@commitlint/cli` and `@commitlint/config-conventional` are both 21.1.0. Exact command `npx --no -- commitlint --edit '/tmp/hexalith-story-6-3-d2gmc5my-commit-message.txt' --verbose` returned exit 0, `found 0 problems, 0 warnings`, for `docs(sprint): approve story 6.3 projection cost spec`. Successful command/output evidence is retained at `/tmp/hexalith-story-6-3-d2gmc5my-commitlint-evidence.txt`; the same message file is used for the local commit.
