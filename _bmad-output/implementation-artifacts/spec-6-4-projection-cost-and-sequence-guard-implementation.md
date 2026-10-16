---
title: 'Story 6.4: Projection Cost And Sequence Guard Implementation'
type: 'feature'
created: '2026-10-15'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'e17e7294f540ebba42fe25192998689f0ecd89c1'
story_key: '6-4-projection-cost-and-sequence-guard-implementation'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md'
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/evidence/story-6-4/implementation-checkpoint.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Live delivery still reads history when current and cannot safely optimize arbitrary tails.

**Approach:** Implement the complete owner-approved specification referenced above, version 1, normative SHA-256 `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`. Its sections 1–10, W0–W6, budgets and preservation matrix are mandatory acceptance requirements.

## Boundaries & Constraints

**Always:** Preserve Stories 1.18/1.19, public compatibility, scoped MessageId identity, writer-v2 fences, receipts, complete ID digests and staged promotion. Use owning Dapr seams. Charge all passes, lookahead, proof and scheduler work.

**Never:** Change approved bytes/budgets, infer tail capability, truncate proof, publish partial replay or bypass reconciliation. No snapshots/upcasting/interface cancellation redesign, dependencies, UI, topology, deployment or execution of maintenance cutover.

## I/O & Edge-Case Matrix

| State | Required behavior |
| --- | --- |
| Proven C=H | Double source inspection and bound Idle/model proof; zero envelopes, handlers, writes, notifications or cleanup |
| Valid C<H | Admit complete contiguous readable tail; match every application event before effects; finalize once |
| Unsupported handler | Bounded complete-prefix staged fallback; no page treated as whole history |
| Duplicate/gap/conflict/race/crash | Typed no-op/retry/reconciliation; preserve fences, stable work and proven state |
| Empty/unknown/erased | Distinct outcomes; EmptyNoWork retains Unknown; never manufacture receipts |

</frozen-after-approval>

## Authorized Dependency Exception

On 2026-10-16, the user replied **“I authorize”** to the prepared five-pin update. This explicitly authorizes a narrow exception to the dependency boundary above, solely in root `Directory.Packages.props`:

| Declaration | Package | Version |
| --- | --- | --- |
| Include | Microsoft.AspNetCore.SignalR.Common | 10.0.13 |
| Include | System.Security.Cryptography.Pkcs | 10.0.13 |
| Include | System.Security.Cryptography.Xml | 10.0.13 |
| Update | Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.13 |
| Update | Microsoft.Extensions.Options | 10.0.13 |

Resolve their required transitive closure normally. Do not add other central declarations or change shared reference files or audit policy. Apply the approved update and refresh affected section 10 source/package/build/test evidence, then continue the existing BMAD acceptance and review workflow. Preserve the original normative bytes, budgets, approval and all historical run bindings. The original handoff preceded application; old passing tests and ten-warning builds remain pre-update evidence. The mutable implementation notes below record the applied current-catalog declaration repair and refreshed qualification.

## Code Map

- `src/Hexalith.EventStore.Server/Events/`: hash the stored EventEnvelope, preserving null extensions; AggregateMetadata supports additive init evidence; EventPersister owns its only reconstruction.
- `src/Hexalith.EventStore.Server/Actors/`: AggregateActor owns witnessed saves/cache barriers; retain old actor signatures and erase semantics.
- `src/Hexalith.EventStore.Server/Projections/`: reuse idempotency, retry, lifecycle, rebuild and cutover protocols; existing completion precedes lifecycle release and needs pending finalization.
- `src/Hexalith.EventStore.DomainService/AdminOperationalIndexMetadata.cs` advertises capabilities; `src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs` verifies ingestion. Client ProjectionRouteCatalogFingerprint.cs binds descriptors; preserve descriptor-absent bytes.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.EventStore.Server/Events/` — add SourceProof/codec files; update AggregateMetadata.cs and EventPersister.cs for source-v1 golden hashes and atomic append evidence (§2).
- [x] `src/Hexalith.EventStore.Server/Actors/` — extend IAggregateActor.cs and AggregateActor with focused inspection/range/bootstrap files; preserve barriers, ambiguous-save readback, erasure/incarnation and retained floor. Update `src/Hexalith.EventStore.Testing/Fakes/FakeAggregateActor.cs` (§2/3/9).
- [x] `src/Hexalith.EventStore.Contracts/Projections/`, `src/Hexalith.EventStore.DomainService/`, `src/Hexalith.EventStore.Client/Projections/` and catalog files above — add fingerprint-bound capabilities and strictly read-only owner/progress proof. Marker interfaces, journal acceptance and mutating status/repair APIs cannot prove completion. Preserve dispatch carriers/per-event identities (§2/4/6).
- [x] `src/Hexalith.EventStore.Server/Projections/` — upgrade ProjectionDeliveryState.cs, reservation/coordinator/store contracts to schema2 and exact scope markers. Add IdleBindingPending, same-reservation partial progress and finalization recovery; fence competing rebuild/erase/reconciliation (§2/5/6).
- [x] `src/Hexalith.EventStore.Server/Projections/`: ProjectionDeliveryCutover.cs and ProjectionDeliveryReconciler.cs — implement authenticated bootstrap/qualification, model-proof hydration and persisted attestations without changing writer-marker bytes. Extend `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs` maintenance seams; no activation (§9).
- [x] `src/Hexalith.EventStore.Server/Projections/`: ProjectionUpdateOrchestrator.cs, NamedProjectionDispatchCoordinator.cs, ProjectionDeliveryRetryWorker.cs and EventStoreProjectionDeliveryHistoryReader.cs — share current/tail logic, remove guarded head-envelope/full-prefix probes, enforce count/byte pages, pre-effect admission matching and safe staged fallback. Preserve wider configured rebuilds (§2–6).
- [x] `src/Hexalith.EventStore.Server/Projections/`: ProjectionDeliveryDiagnostics.cs and retry diagnostics — measure operations/bytes/proof visits with bounded tags and truthful lifecycle/provenance. Update `docs/operations/projection-delivery-v2-cutover.md` and `docs/concepts/projection-delivery.md` (§8/9).
- [x] `tests/Hexalith.EventStore.Server.Tests/`, `tests/Hexalith.EventStore.DomainService.Tests/`, `tests/Hexalith.EventStore.Client.Tests/`, `tests/Hexalith.EventStore.Contracts.Tests/` and `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/` — extend §7's named production fixtures for every matrix case and W0–W6, including every §6 crash boundary. Retain sanitized results under `_bmad-output/implementation-artifacts/evidence/story-6-4/` (§7/10).

**Review correction pass 1:**

- [ ] `src/Hexalith.EventStore.DomainService/DomainProjectionGuardedReplay.cs`, its dispatcher callers and owning tests — close the manifest/reference interruption window using original immutable bytes, preserve accepted private recovery formats and one preparation, and retain sufficient completion evidence so cleanup cannot enable a delayed request to prepare again. Test interruption/lost response at each preparation write and delayed stage after cleanup.
- [ ] `src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryRetryWorker.cs`, `ProjectionSourceGuardDelivery.cs`, tail delivery and owning tests — settle superseded original retry work only from exact scoped/catalog-bound completed-prefix and MessageId proof, under its own durable claim/revision. Newer head delivery must not leave an older unreserved item deferred forever; sequence comparison alone cannot authorize deletion.
- [ ] `src/Hexalith.EventStore.Server/Projections/ProjectionSourceGuardDelivery.cs`, `ProjectionSourceGuardFinalizer.cs` and owning tests — start the independent finalization deadline after read-only history verification. Slow verification must converge, while release/binding and scheduler cleanup remain bounded and preserve original work on failure.
- [ ] `src/Hexalith.EventStore.Server/Projections/DaprProjectionDeliveryStateStore.cs` and owning tests — reject oversized guarded bodies before materializing their proof graph; reject duplicate guarded evidence properties according to the actual serializer's name matching, including nested/case variants. Preserve historical schema1 behavior, both marker reads on accepted paths, exact budgets and typed failures.
- [ ] `src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryStateBody.cs` and owning tests — retain only the known evidence-path ancestry instead of preallocating from configured `MaxDepth`. Preserve the configured reader limit and safe deeper unknown JSON handling. Qualify a finite supported large-depth profile through a valid owning read, both marker reads, zero writes and bounded allocation; refresh affected Server/Live and package/Contracts evidence.
- [ ] `src/Hexalith.EventStore.Client/Projections/ReadModelOwnerSelectionMetadata.cs` and owning tests — reject ambiguous owner/freshness metadata, including case variants consumed by the Web deserializer and duplicate persisted freshness fields, before authorizing selection or receipts.
- [ ] `src/Hexalith.EventStore.Server/Projections/ProjectionSourceGuardHistoryReader.cs`, `DaprProjectionSourceGuardTransport.cs`, relevant bootstrap/replay sizing and owning tests — make admitted readable-wire byte measurements match the actual qualified transport serializer. A configurable profile that changes wire bytes must be measured consistently or rejected before effects; keep source/wire/native categories separate.
- [ ] `src/Hexalith.EventStore.Server/Actors/AggregateActor.SourceProofBootstrap.cs`, `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.SourceProof.cs` and owning tests — reject a proposed anchor over 4096 bytes before mutation, including exact-boundary and long permitted identifiers; return the existing proven source incarnation through the authenticated bootstrap response so the maintenance caller can qualify that same source.
- [ ] `src/Hexalith.EventStore.Server/Actors/AggregateActor.SourceProof.cs`, source bootstrap sizing and owning tests — verify configured actor serializer profiles against the pinned SDK's default `Utf8JsonWriter` state serialization. A relaxed encoder and permitted opaque ETag must not undermeasure source metadata or admit an anchor whose actual SDK JSON exceeds 4096 bytes. This writer-profile mismatch is separate from R1-B5's unverified unknown/padding storage-body claim; preserve owning state APIs and avoid claiming raw-provider measurement.
- [ ] `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminProjectionRebuildControllerTests.cs` — exercise all three new maintenance actions with an authenticated attributable non-administrator, asserting 403 and unchanged source/delivery/admission evidence.
- [ ] `tests/Hexalith.EventStore.DomainService.Tests/` — drive valid recovery-bound stage/commit/verify/retry through the SDK's actual `UseEventStoreDomainService` mappings and its persistent owning store; assert original manifest reuse and persisted model/recovery outcomes, without replacement route mappings.
- [ ] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs` and existing named/shared/read-model/erase integration fixtures — make backend diagnostic connections reuse the fixture's supported configured Redis endpoint. Preserve every assertion, owning-boundary correctness proof and diagnostic-only limitation; leave OQ8 routing and production code unchanged. Rebuild and refresh only affected Live outputs after the demonstrated override gap.

**Acceptance Criteria:**

- Given approved prerequisites, when implementation starts, then digest/approval and preserved 1.18/1.19 evidence are verified before runtime edits.
- Given W0–W6, when production handlers/stores and live sidecars execute, then every §3 ceiling passes and persisted semantic outcomes equal canonical replay; same-work identities remain stable.
- Given completion, when §10 validation runs, then affected suites and Release/package gates pass without unexpected skips, warnings, errors or leaked processes; environment failures remain unproven gates.

## Implementation Notes

Execution tasks and refreshed local qualification are complete on frozen source73. Source proofs, guarded Current/tail delivery, fenced finalization and original-work replay recovery, compatible legacy/named/generic fallback, maintenance seams and cost diagnostics are implemented. The [current coverage addendum](evidence/story-6-4/continuation-11-coverage-addendum.md) binds W0–W6, section 6 durable boundaries and section 7 preservation to fresh tests; the continuation10 index remains the historical executable-selector map.

All five affected complete Debug and all five Release assemblies passed: each configuration selected 10,118 tests, passed 10,092 and retained exactly 26 existing skips, with zero failures/errors/not-run. Exact focused Server/Domain/Live passed 548/110/40, owning authority passed 64, and fresh R4096 Current passed 5 theory cases with 15 exact observations. All 14 manifest packages and owning-process cleanup were verified. Actual exact Release/package restore/build and pack now pass with zero warnings/errors. The [current gate packet](evidence/story-6-4/continuation-11-final-gates.json) preserves baseline `e17e7294f540ebba42fe25192998689f0ecd89c1`, actual HEAD `008b83f0443c151052275fd291b0b88ac3aa996c`, source/catalog/package identities, each run's actual DLL binding and unsuccessful intermediate attempts.

The five authorized servicing versions are applied. SignalR.Common uses Update at 10.0.13 because the actual clean shared catalog already declares it; the strict owning validator and negative fixtures follow that action. Two test fixtures satisfy the unchanged tracked-secret scanner using an operationId name and a generated logging sentinel. No shared reference, audit-policy, extra central package/version or production runtime source change was made during this refresh.

Parent step-03 accepted the source73 execution and matrix evidence. Independent step-04 review then exposed the correction tasks above; source73 remains historical qualification and cannot qualify changed runtime outputs. Review correction pass 1 and refreshed acceptance/review are open. Local qualification is not authenticated exact-head sealed CI, the AD-12/NFR8 transition or production acceptance. This refresh performed no maintenance activation, deployment, package publication or source commit.

KEEP for re-derivation: preserve the approved normative bytes/budgets and original authorization, additive public compatibility, source-v1 golden bytes/atomic append and erasure/cache barriers, exact scope and unchanged writer-v2 marker bytes, full ID digests/receipts, staged original-work replay and owner/lifecycle fences, all existing W0–W6/preservation assertions, the exact authorized five versions and strict authority adapter, and truthful per-command source/package/runtime evidence. Do not weaken a test, skip, warning, audit or cost ceiling. Preserve all user/external committed work and the validated fixture/configuration repairs; no uncommitted runtime edits exist at the cited production locations to roll back. Re-derive the corrective implementation in place from this clarified non-frozen plan. The raw actor-storage-byte concern in R1-B5 remains unverified; do not bypass `IActorStateManager` or invent a raw-body guarantee.

## Spec Change Log

- 2026-10-16: R1-P2's bounded normal regression confirms that pinned `DaprClientBuilder` accepts `MaxDepth=1,000,000`; an accepted 1,738-byte guarded row reads both markers and performs no writes, but its header validator allocates 8,016,800 bytes from that option alone. Replaced that preallocation with fixed storage for known path levels, preserving configured JSON depth and covering a deeper unknown value. Keep red/green identities distinct and refresh affected Server/Live plus fresh package/Contracts qualification. No approved bytes/budgets, public surface, dependency or provider boundary changes.

- 2026-10-16: A shared Redis `FLUSHALL` at 12:12:42 UTC destroyed active fixture source/marker data; owning actor/marker diagnostics bracket the sequence reset and recreated-marker conflict. The failed full lane was interrupted and remains unqualified. Existing `EVENTSTORE_TEST_REDIS_PORT` isolation uses a task-owned instance of the exact observed Redis 6.2.21 image/command/settings. Its first run exposed six hardcoded backend diagnostic connections still targeting 6379; the test-only endpoint adapter is now part of the mutable correction plan. Preserve all assertions, OQ8's separate profile and the hardcoded shared persistence diagnostic disclosure. No frozen bytes, runtime source, dependencies, topology or cost ceilings change for this verification repair.

- 2026-10-16: Review loop 1 (`bad_spec`) clarified the implementation plan for interrupted guarded preparation, cleanup re-preparation, superseded retry settlement, finalization deadline placement, bounded/unambiguous proof parsing, consistent wire sizing, usable bounded bootstrap, and real endpoint regression coverage. The known-bad state is durable work stranded or re-prepared, ambiguous/oversized proof accepted, or untested routing/authority bypass. KEEP instructions above preserve the implemented source/owner/fence/compatibility/cost protocols and existing user work. Frozen intent and normative bytes are unchanged; runtime correction and fresh gates are required before acceptance.

- 2026-10-16: Refreshed local source73 Debug/Release, W0–W6/live, R4096, package and exact zero-warning gates. Preserved per-command runtime bindings, historical failures and original approval/normative bytes. Reconciled mutable concept/runbook qualification wording with a separate documentation hash delta; formal acceptance/review, sealed CI and production qualification remain open.

- 2026-10-16: Reconciled the actual clean shared catalog at `728174f506d56204657e0c52e98a4453aef79f6a`, which already declares SignalR.Common 10.0.13. The original root Include now causes NU1506. Changed only that declaration to Update at the same authorized version and adjusted its owning validator/test expectation. This is a technical configuration repair within the existing five-package/version authorization, not a new human approval. The original authorization table and frozen normative bytes remain historical and unchanged; fresh restore/build and effective-closure comparison are required.

- 2026-10-16: User replied “I authorize” to the exact five-pin root dependency update. Recorded the narrow exception above; no normative bytes, budgets, shared references or audit-policy changes are authorized. Revalidation is pending.

- 2026-10-16: Completed execution checklist and reconciled actual verification evidence. Approved intent, normative bytes and dependency policy remain unchanged; zero-warning acceptance and subsequent review remain open.

## Review Triage Log

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| R1-B1 — guarded manifest/reference crash window | high | `SaveAsync` saves -2 then -3 separately; `ReadAsync` rejects an existing -2 without -3 before dispatch can resume. A crash or lost second write strands the original plan. **bad_spec**, group G1. |
| R1-B2 — guarded cleanup permits another preparation | medium | `ReclaimAsync` deletes both -2 and -3; the dispatcher calls `PrepareRebuildBatchAsync` when `ReadAsync` returns null. A delayed Stage can prepare again under the same identity before terminal fingerprint rejection. **bad_spec**, group G2. |
| R1-B3 — bootstrap does not return its incarnation | medium | The actor generates and returns `SourceProof.SourceIncarnation`; the authenticated controller omits it, while qualification requires `ExpectedIncarnation`. The advertised maintenance HTTP workflow cannot carry that proof forward from its own response. **bad_spec**, group G3. |
| R1-B4 — guarded proof deserialization precedes byte cap | high | `ReadAsync` materializes `ProjectionDeliveryState` and its digest array before `ValidateSize`. An oversized guarded state therefore allocates an unbounded graph before rejection. **bad_spec**, group G4. |
| R1-B5 — actor metadata raw storage-body accounting | maybe-false | The actor measures typed state using its configured `ActorRuntimeOptions.JsonSerializerOptions`. The cited inflation needs unknown/whitespace bytes not exposed by `IActorStateManager`; the current diff does not establish whether a permitted actor serializer/provider can retain those bytes or how they relate to the approved logical body. SDK serialization-seam evidence and an owning-boundary reproducer would settle it; no direct-provider bypass is permitted. Potential high cost-evidence impact is unverified. **defer**, pending the cascading loopback; no ledger action taken yet. |
| R1-B6 — case-ambiguous owner/freshness metadata | high | Exact-name duplicate checks precede a Web deserializer whose property matching is case-insensitive. `version`/`Version` or owner-generation variants survive validation, and root freshness duplicates are not rejected. **patch**, group G5; included in re-derivation because higher bad-spec entries cascade. |
| R1-B7 — duplicate guarded delivery-row properties | high | The guarded row is deserialized without a duplicate-property check; schema/nested proof fields can be selected by order. The guard-marker parser's separate check does not validate this row. **bad_spec**, group G6. |
| R1-B8 — wire sizing and actual serializer differ | medium | History admission uses Web options while the guarded HTTP transport uses `dapr.JsonSerializerOptions`; configurable options can enlarge readable events after the admitted wire cap was checked. **bad_spec**, group G7. |
| R1-B9 — pending recovery starts finalization too early | medium | `RecoverPendingAsync` starts the 30-second token before `RecoverAsync` admits the whole history with the caller token, then passes the expiring token to binding and scheduler update. Slow read-only verification can prevent convergence on every attempt. **patch**, group G8; included in re-derivation because higher bad-spec entries cascade. |
| R1-B10 — bootstrap can save an oversized anchor | medium | The proposed anchor is measured but is saved without comparing against 4096; permitted ETag/head identity lengths can produce Available followed immediately by Invalid inspection. **patch**, group G9; included in re-derivation because higher bad-spec entries cascade. |
| R1-E1 — orphan guarded manifest | high | Independently reported same SaveAsync/ReadAsync interruption as R1-B1, verified at both write/read branches. **bad_spec**, group G1; retained as a separate finding. |
| R1-E2 — newer head leaves original retry item forever | medium | Tail selection uses the observed head if no reservation exists and schedules that head's work. Worker cleanup requires exact original head equality; subsequent Current observations of the newer head always defer the old item. **bad_spec**, group G10. |
| R1-V1 — maintenance administrator regression gap | high | Pre-verified gap: existing controller tests cover other actions, and actor tests sign directly without a principal. None exercise non-administrator access to qualify/activate/bootstrap, so removing the administrator checks would pass. **patch**, group G11; included in re-derivation because higher bad-spec entries cascade. |
| R1-V2 — recovery HTTP routing regression gap | medium | Pre-verified gap: recovery fixtures parse headers and route directly, while the actual SDK mapping is tested only for authorization of `{}`. Removing its recovery-header branch would evade the current recovery assertions. **patch**, group G12; included in re-derivation because higher bad-spec entries cascade. |
| R1-P1 — SDK metadata writer profile differs from stream sizing | medium | Pinned SDK `DaprStateProvider` creates a default `Utf8JsonWriter` then serializes the typed value with configured options. Writer Encoder/Indented settings take precedence; our direct-stream sizing uses the configured encoder instead. A relaxed encoder with permitted opaque metadata text can undercount the SDK-produced JSON. **patch**, group G13; verify a focused reproducer and align the measurement/cap without a state-provider bypass. This does not resolve or refute R1-B5's distinct unknown/padding claim. |
| R1-Q1 — live backend diagnostics ignore the configured endpoint | medium | The existing fixture uses `EVENTSTORE_TEST_REDIS_PORT` for Dapr state/pubsub, while six diagnostic connections in four integration fixtures hardcode 6379. An isolated full run therefore reads the wrong backend and fails unchanged assertions. **patch**, group G14; expose/reuse the fixture endpoint, retain interrupted evidence and refresh affected Live outputs. Owning Dapr proofs, OQ8 routing, production code and dependency policy remain unchanged. |
| R1-P2 — configured parser depth preallocates unvisited ancestry | medium | A permitted pinned-SDK profile with finite `MaxDepth=1,000,000` accepts a 1,738-byte guarded row through the owning store with both marker reads and zero writes, but header validation allocates 8,016,800 bytes before inspecting actual depth. **patch**, group G15; retain bounded known-path storage and safe deeper unknown handling without changing configured reader depth. The normal red regression selected 1/failed 1; preserve its exact snapshot/log/vector and refresh affected runtime/package evidence after green. |
| R1-Q2 — pre-existing process renewal fixture continuation race | medium | Fresh same-DLL complete Domain run selected 977/passed 976/failed 1 at `SweepRenewalFaultPreservesCompletedSourceObservation` (`Fired` false), while the narrow unchanged test passed 1/1. Its asynchronous source-release continuation sets `Fired` after renewal cancellation can complete the awaited pass; external fixture state assertions were not reached. **defer**, pre-existing external fixture race, pending final step-04 cascade processing; preserve failed/narrow and fresh complete outcomes, make no unrelated source change and do not append the deferred ledger yet. A repeated full failure or persisted-state assertion failure requires renewed investigation. |

## Design Notes

API shapes are additive implementation choices; no intent gaps remain. Migration/activation are operator work.

## Verification

Every exact approved section 10 command was executed on the recorded corrected candidate: individual Debug/source focused and complete assemblies, real Dapr/Redis fixtures, exact Release/package restore/build, actual 14-package inventory and all five complete Release suites. Persisted source/model/detail/index/receipt/checkpoint/lifecycle/retry assertions pass through owning seams. See the [current coverage addendum](evidence/story-6-4/continuation-11-coverage-addendum.md), [gate packet](evidence/story-6-4/continuation-11-final-gates.json) and [closing audit](evidence/story-6-4/continuation-11-final-audit.json).

Verified with SDK 10.0.401/runtime 10.0.12. Regular fixtures explicitly pin Dapr 1.18.2 with owned controls; unchanged OQ8 uses installed 1.18.7/shared controls, recorded separately. The fixture profile and HotReload are unchanged. Redis/scheduler forwarding and inotify failed attempts remain unsuccessful historical evidence; owned resources and the guarded temporary inotify adjustment are resolved in the closing record.

The exact command `dotnet build Hexalith.EventStore.slnx -c Release --no-restore -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -m:1` exits zero with zero warnings/errors. Package/Contracts fixtures republish host outputs, so actual per-command bindings are retained: four complete Release suites use build4; whole Release Live and R4096 use coherent build5. Server/Gateway DLL hashes differ between these builds despite unchanged source/catalog/configuration/deps; the precise PE difference is unresolved and no binary/CIL equivalence is claimed. The existing transitive Gateway generatedDebug behavior remains disclosed. Final concept/runbook edits are separately hashed documentation changes and do not rewrite source73 runtime snapshots.

Formal acceptance/review and authenticated exact-head sealed CI/AD-12/NFR8 transition remain required. Do not infer production readiness or perform maintenance cutover from local passing gates.
