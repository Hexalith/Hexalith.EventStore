# Continuation 08 verification

**Incomplete and unqualified. No environment was admitted or cut over.**
HEAD remains e17e7294f540ebba42fe25192998689f0ecd89c1. Approved normative SHA-256
78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711 and detached approval
657f0dc25b18b826b37ab3be53273043fa98ca61a40923c9a913922816bf531f were recomputed and
match. No dependency/audit policy/Git-index/branch/commit/submodule mutation occurred.
The parent remained read-only; builds and runners executed sequentially.

## Build and targeted commands

All builds used this exact command from the repository root, redirecting output to
the corresponding /tmp/story-6-4-cont08-buildN.log:

```bash
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
```

| Build | Result |
| --- | --- |
| 1 | Failed,0 warnings,1 CS0105 duplicate using introduced in Replay partial; corrected before any runner |
| 2 | Passed,0 warnings/errors,25.57s; exact wire/shared-prefix runtime changes |
| 3 | Passed,0 warnings/errors,25.32s; initial outbox/inspection primitives |
| 4 | Passed,0 warnings/errors,24.94s; actual proof traversal instrumentation |
| 5 | Passed,0 warnings/errors,18.84s; safe retention finalization and owning fixture |
| 6 | Passed,0 warnings/errors,11.58s; shared-prefix/canonical production vectors and bounded intent sizing |
| 7 | Passed,0 warnings/errors,11.39s; final source, inspection cache barrier and ambiguity tests |

Exact assembly runner prefix:

```bash
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll
```

| Output suffix | Arguments appended to prefix | Result |
| --- | --- | --- |
| focused1 | -method '*GuardedProtectedWirePass_Exact64MiBAndOneByteOverChargeActualDecryptAndSizingBeforeEffects' -method '*GuardedCanonicalFallback*' -method '*CanonicalReplay*' | build2:30 passed,0 failures/skips/errors,7.348s |
| primitives1 | -class '*DaprProjectionActivationOutboxTests' -class '*EventReplayProjectionActorTests' | build3:25 passed,0 failures/skips/errors,0.276s |
| proof1 | -class '*NamedProjectionDispatchCoordinatorTests' -class '*ProjectionDeliveryIdempotencyCoordinatorTests' -class '*ProjectionDeliveryStateContractTests' | build4:192 passed,0 failures/skips/errors,16.052s |
| retention1 | -method '*RetentionMovesDuringRelease*' | build5:2 passed,0 failures/skips/errors,0.378s |
| primitives2 | -method '*CanonicalAdmission_SharesOnlyOne*' -method '*ThreeFullReplayRoutes*' -class '*DaprProjectionActivationOutboxTests' -class '*EventReplayProjectionActorTests' | build6:0 selected; method/class filters intersect; NOT evidence |
| replay1 | -method '*CanonicalAdmission_SharesOnlyOne*' -method '*ThreeFullReplayRoutes*' | corrected build6 selection:2 passed,0 failures/skips/errors,0.501s |
| primitives3 | -class '*DaprProjectionActivationOutboxTests' -class '*EventReplayProjectionActorTests' | build6:25 passed,0 failures/skips/errors |
| focused2 | Full exact command below | build6:584 passed,0 failures/skips/errors,16.232s |
| primitives4 | -class '*DaprProjectionActivationOutboxTests' -class '*EventReplayProjectionActorTests' | final build7:29 passed,0 failures/skips/errors,0.272s |

Every output path is /tmp/story-6-4-cont08-<suffix>.log. No runner used assets from a
failed build. Source/assembly identities are retained in continuation-08-source-assets.json.
The broad584 run preceded only the final actor-inspection cache barrier and its four
new actual fault cases, which ran with the complete actor/outbox classes on final build7.
No final full-suite/Release/package/live gate is claimed.

## Exact broad focused command

```bash
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*ProjectionDeliveryIdempotencyCoordinatorTests' -class '*NamedProjectionDispatchCoordinatorTests' -class '*EventStoreProjectionDeliveryHistoryReaderTests' -class '*ProjectionRebuildProductionPathTests' -class '*ProjectionDeliveryCutoverTests' -class '*ProjectionDeliveryReconcilerTests' -class '*ProjectionDeliveryStateContractTests' -class '*ProjectionDeliveryDiagnosticsTests' -class '*ProjectionEraseCoordinatorTests' -class '*EventPersisterTests' -class '*AggregateMetadataTests' -class '*AggregateActorTests' -class '*AggregateActorInfrastructureFailureTests' -class '*AggregateActorGetEventsTests' -class '*DaprProjectionLifecycleGatewayTests' -class '*ProjectionDeliveryDocumentationTests' -class '*ProjectionDeliveryRetryWorkerTests' -class '*DaprProjectionDeliveryRetrySchedulerTests' -class '*ProjectionActivationWorkerTests' -class '*QueryRouterSourceGuardTests' -class '*DaprProjectionDeliveryStateStoreTests' -class '*TrustedEffectAggregateErasureTests' -class '*TrustedEffectJointOffboardingTests' -class '*DaprProjectionActivationOutboxTests' -class '*EventReplayProjectionActorTests' -showLiveOutput
```

## Cache ambiguity evidence

Final actor class includes InspectRebuild_SameActorDiscardsUncommittedCacheAndReadsLostCommittedResponse
for Stage/Promote x precommit failure/lost committed response. It uses the actual
FaultInjectingActorStateManager pending/committed semantics on the same actor instance.
Post-inspection unrelated Save cannot publish an earlier discarded write. No model or
notification is inferred from a cached candidate/receipt. Read-only inspection now
performs ClearCache then only owning reads; the cache operation changes no provider data.
The bound publication methods themselves are still unimplemented/unwired and require
equivalent barrier/ownership treatment when implemented.

## Limits and final audit

- Legacy frozen intent and original candidate inspection are prerequisites only. The
  active legacy delivery algorithm is unchanged/unbounded and must still be replaced
  with a fully fenced, resumable bounded/staged path. No partial stage/promote switch
  was enabled. No operator rebuild checkpoint was repurposed.
- continuation-08-production-costs.json retains the actual payload-free owning-seam
  measurements emitted by focused2, including newly charged proof visits. They are
  not native HTTP bytes, built-in sidecar attempt counts, RSS, working-memory or live
  qualification. Raw page framing and logical complete-array budget differ as recorded
  in current-state; no real serialization work was subtracted from the counters.
- git diff --check returned exit0 after tracked LF source normalization. No process
  from this continuation's completed build/runners/AppHost/Dapr fixtures remained.
- No full pinned live fixture, broad full Server/Client/DomainService/Contracts suite,
  final package inventory or Release/package command was run here. All remaining
  mandatory requirements from continuation07 remain open.
