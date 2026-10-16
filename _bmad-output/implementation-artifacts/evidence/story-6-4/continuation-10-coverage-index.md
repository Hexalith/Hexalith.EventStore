# Story 6.4 qualification coverage index

Functional verification complete; formal Story6.4 acceptance remains open for
the required zero-warning Release gate. This index identifies executable
assertions and actual passed assets; it does not replace the approved specification.
Normative SHA-256: `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`.
No environment activation, deployment, package publication or source commit occurred.

Current check identities and exact commands are recorded in
[continuation 10 progress](continuation-10-progress.md). Final full-suite/Release
results below supersede intermediate focused runs. Frozen compact private-v2
source/asset identity is recorded in continuation-10-final-gates.json (Server65; historical58/62 retained). Existing Server DW1 skips
are separately declared (13 delivery,4 poller,8 drain); no new skip is authorized.

All mapped Server methods below passed the complete frozen65 Debug assembly:
fullServer4 selected5102/passed5077/exact25 declared DW1 skips,0 failures/errors/
not-run641.671s,exit0. FullDomain4 passed904/904 on unchanged compact-v2 Domain
source; fullClient1 passed1562/1562; package-enabled fullContractsDebug1 passed
2339/2339 with all three probes and actual14-package inventory. Exact Release
restore/build and manifest pack exited0; ten dependency advisory warnings keep
zero-warning acceptance unproven. Fresh final focusedLive21 passed40/40 with no skips/failures691.252s and
runner/control exits0/0/0. All49 sanitized cost/native/current/cold/source/generic
observations are bound in continuation-10-pinned-focused-final2-vectors.json.
Whole Debug Live2 passed192 selected/191 passed/1 intentional existing worker-helper
skip,0 failures/errors/not-run948.290s,with wrapper/control exits0/0/0. Its
[49-observation packet](continuation-10-live-full-debug2-vectors.json) binds final
Live21 assets and both actual runtime profiles; OQ8 owning cleanup is verified.
All five Debug and all five Release full assemblies have passed. The final
Release Live result and actual Build3 assets are retained in
[its 49-observation packet](continuation-10-live-full-release1-vectors.json).
No build, test runner or owned control plane remains active.

## Workloads

| Required vector | Concrete executable assertions | Last relevant passed evidence / remaining gate |
| --- | --- | --- |
| W0 prescribed heads/pages/R | `NamedProjectionDispatchCoordinatorTests.GuardedCurrent_ProductionActorsAndStoresMeetExactLogicalReadAndZeroMutationBudget`; live `SourceGuard_CurrentMatrixUsesExactlyElevenMetadataReadsAndPreservesEveryOwningSurface` | Final Server65 Debug5102/5077+25 and Release5102/5077+25 pass. [Final Live21 Debug](continuation-10-live-full-debug2-vectors.json) has15 R256 Current observations; [R4096 Live18](continuation-10-pinned-current4096-vectors.json) has15 observations with exact older asset binding. |
| W0 absent/touched/erased/drift | Actor `SourceBootstrap_EmptyNewOrTouchedSourceRetainsOneSeedWithoutInventingEnvelopes`, erasure/bootstrap/inspection vectors; `SourceGuardQualification_ExplicitNewEmptyScopeInitializesZeroWithoutModelOrCompletionReceipt`; production empty/erased rebuild methods; live `SourceGuard_QualifiedTouchedEmptySourceStaysUnknownAndCreatesNoReceiptOrDeliveryEffects` | Mapped owning methods pass complete Server65 Debug and Release. Final Live21 Debug includes the touched-empty zero-effects/Unknown vector; [earlier empty packet](continuation-10-pinned-empty1-vectors.json) retains its detailed numeric observation. |
| W1 long small tail / three routes | `GuardedTail_ProductionActorStoreAndHandlerCostsStayInsideEveryIndependentCeiling` atH10000/C9999/P1,P256; `GuardedThreeRouteTail_ProductionOwnersChargeAllSevenRealPassesWithoutPriorHistory`; `GuardedTail_ProductionDispatchAndStoreMatchCanonicalReplayWithinSourcePassCeilings` | Complete Server65 Debug and Release pass; [final Live21 Debug](continuation-10-live-full-debug2-vectors.json) includes single-route delta1. Three-route seven real copies remain explicitly charged by the owning Server assertions. |
| W2 delta771 / P1,P256 | Same production cost method atH9771/C9000; `GuardedRetryWorker_UsesItsActual256EventRangeProfile` | Complete Server65 Debug and Release pass; final Live21 Debug P256 cost vector passes,with canonical model/count and original-work cleanup assertions. |
| W3 count and byte framing | Production tail costs atdelta0,256,257,512,513; `AggregateActorGetEventsTests.BoundedRange_ChargesArrayFramingAndAtMostOneLookahead` at8190/8191/8192; empty/missing/erased actor cases | Complete Server65 Debug and Release pass. Final Live21 Debug typed oversized-vector native/send/callback observations pass; source envelope/key reads/array framing/lookahead remain separate charged categories. |
| W4 mixed source/protected and64MiB±1 | `GuardedMixedLargePayloadTail_ChargesEveryActualLookaheadDecodeAndSizingByte`; `GuardedProtectedMixedTail_ChargesActualProviderReadabilityAndKeepsCurrentReadOnly`; `GuardedSourcePass_Exact64MiBAndOneByteOverPreservePreEffectAdmission`; `GuardedProtectedWirePass_Exact64MiBAndOneByteOverChargeActualDecryptAndSizingBeforeEffects` | Complete Server65 Debug and Release pass. Source/wire limits independently reject before effects and retain proof. |
| W4 working bytes | `ProjectionProofWorkingMemoryTests.GuardCompletionRetainedProofAndActualSdkSendRootsFitApprovedEnvelope`; all `ProjectionEventWorkingMemoryTests` methods | Complete Server65 Debug and Release execute all mapped memory assertions successfully. [Managed phases](continuation-10-working-memory-phases.json) and [additional readback/source/native phases](continuation-10-additional-working-phases.json) retain detailed prior measured identities; four post-CAS proof roots and actual native Brotli peak1,602,232 are counted. |
| W4 shared scheduler occupancy/cold/CAS races | `DaprProjectionDeliveryRetrySchedulerTests` Costs/CasRaces partials:64x64 plus one128-item shard,0/4 refused CAS, every pre/post body, cold migration; actual guarded N9 worker | Complete Server65 Debug and Release include every scheduler class and pass. [Detailed scheduler vectors](continuation-10-scheduler-vectors.json) retain actual cold/combined/copy/mutation measurements; exact focused filter alone is not used as this closure. |
| W5 complete-prefix legacy/named/generic | `ProjectionRebuildProductionPathTests` LegacyReplay/LegacyRecovery H0/256/257/771/10000; guarded canonical/mapped SourceGuard methods; `ProjectionReplayRecoveryTests`, worker bounded loader; live `ReplayRecovery_GenericHandlerReusesOneOriginalManifestAcrossThreeLostDurableResponses` | Complete Server65 Debug and Release pass. [Final Live21 Debug](continuation-10-live-full-debug2-vectors.json) includes12 actual generic recovery attempts at four prescribed heads,with compact-v2 context/one original preparation. H10001 and expanded-byte rejection remain asserted. |
| W5 wider compatibility | `SourceStreamedReplayUploadsMoreThan64MiBWithoutRetainingPartBodies`; `ProjectionUpdateOrchestratorTests.RebuildProjectionAsync_MappedOnlySourceStreamsBeyondLegacy64MiBLimit`; established bounded target/empty/readback rebuild tests | Complete Server65 Debug and Release include owning orchestrator and coordinator methods and pass. Wider operator behavior remains separate from the bounded qualification profile. |
| W6 A8/A32 andN8/N9 | `GuardedOwningTransitionsExhaustExactlyTheConfiguredCapThenPreserveOriginalWorkAndUseFencedRecovery`; `ActualGuardAndOwningSchedulerChargeNineFailedActivationsThenResumeTheOriginalPendingReservation` | Complete Server65 Debug and Release pass all transition/recovery methods. Original IDs/full proof/work remain pending on ninth activation and converge later through fenced recovery. |
| W6 exactR/R+1 identities | `Compaction_RespectsConfiguredBoundaryAndKeepsCumulativeHead`; `OwningStorePreservesExactRetainedDuplicatesAndCompleteIdsAcrossRAndRPlusOne` at256/4096; older-ID suffix andv2 position/provenance conflict tests | Complete Server65 Debug and Release pass both256/4096 horizons; unchanged canonical rows,complete identity digests and exact retained receipts are asserted. |
| W6 replicas/source/ownership changes | Live original receipt recovery/two replicas; raw source codec mutation suite; SourceGuard partial event/same reservation/replaced generation/source growth/floor tests; gateway pending-writer fences | Complete Server65 Debug and Release include gateway/retry/worker assertions and pass. Final Live21 Debug includes original-receipt recovery and both real replicas. |

## Section 6 durable boundaries

Each boundary is mapped to owning production seams. A direct finalizer unit is
identified as such; it cannot substitute for actor/store durability where a live
row is required. Existing live batch vectors qualify their actual SDK boundary,
not an invented Cartesian expansion of every fault across every mode.

| Durable boundary | Guarded tail / canonical fallback or preserved SDK assertion |
| --- | --- |
| Before source/during admission/decryption | History reader foreign/incomplete/provider/cancellation tests; guard pre-canceled call; byte-limit and unreadable late provider assertions; bounded recovery loader cancellation between ranges. |
| After activation/lease,before reservation | `GuardedOwningCancellationAfterClaimOrReservationPreservesOriginalWorkBeforeAnyHandler` claimed cases use actual scheduler/actors/stores for single-event and full-replay semantics; A8/A32 reserve exhaustion retains original work and authorized same-C reconciliation. |
| After reservation,before handler | Same cancellation method reserved cases assert persisted reservation,zero handler/model effects and original-work convergence for both modes; existing rightful/expired fence tests remain. |
| During event/detail/index/candidate/part | Guard partial committed event/lost response/corrupt original manifest/replaced owner tests; legacy interrupted original-work promotion cases; mapped uploaded part resume; Client/live batch partial-prefix tests. |
| Durable effects,response lost/caller canceled | Guard event response loss; canonical stage/commit/verify lost durable responses; actual generic live three-response recovery; existing live SDK post-dispatch cancellation. |
| Missing receipt/generation/freshness/readback | Guard conflicting/corrupt receipt/manifest and replaced owner tests; direct finalizer missing selected receipt test; existing owning read-model proof tests and shared-owner barriers. |
| Verification source pass failure | Guard application reread tuple mutation; legacy changed prefix after stage/during promotion; verification fail leaves original reservation/effects and old C. |
| Pending CAS/release/lost Idle response | `GuardedOwningLostReleaseResponseAfterPendingCasSettlesOriginalCompletionWithoutRedispatch` both semantics use actual lifecycle actor/persisted pending row; direct finalizer rightful/expired pending recovery tests retain original fence and no later handlers. |
| Completion CAS response/lifecycle/source before reread | Direct finalizer lost completion response/changed lifecycle tests; owning `GuardedTail_AppendAfterCompletionCasBeforeReadbackCompletesFrozenHeadWithoutClaimingCurrent`; diagnostics preserve actual C and latest H after a post-CAS source fault. |
| Complete row/failed cleanup | Guard canonical cleanup and exact subset retry ownership; live actual activation worker closes append outbox; worker elapsed-budget carryover and newer activation preservation; legacy completed row/failed outbox cleanup. Pure Current remains zero-cleanup. |
| Fallback promotion/cancel/resume/release | Legacy interrupted original-work boundaries; production rebuild interrupted promotion/canceled first page/resumable progress/fence release; real live batch partial prefix/cancellation/conflict and one manifest generic recovery. |
| Restart/fleet/restore/retention | Actor unsafe-cache/reload/ambiguous-save barriers; source incarnation/floor qualification; schema downgrade/marker survival and pending competing writers; fresh domain replicas/catalog/owner proof and two live replicas. |

New owning interruption six-case run: build51 focused32 passed9/9 (six interruption
cases plus three event working cases). Complete frozen65 Server Debug and Release runs now provide the closing
evidence for all mapped methods and unchanged Story1.18/1.19 preservation tests.

## Compatibility and final gates

The full §7 table remains mandatory: codec/append ambiguity; duplicates/fences;
raw content/position/provenance and old IDs; complete horizons; bounded pages;
mapped real repeated passes; full-prefix/empty equivalence; staging/promotion;
handler barrier/cancellation/public ABI; shared owner/tenant; fleet/migration/
downgrade/erasure; payload-free telemetry. The affected Server,Domain,Client,
Contracts and indicated live classes above cover the owning source locations.

Additional explicit compatibility: `ProjectionReplayTransportContextTests`
freezes public request properties/body bytes and private persisted-part/context
validation. Compact v2 binds exact head and complete length-framed ordinal owner
tuples; the owning4096-byte generation case preserves its raw original manifest
through a lost stage response,using236 header bytes and one preparation.
`CompactReplayContextResumesAnExistingVersionOneReferenceWithoutPreparingAgain`
retains accepted private-v1 recovery. Raw and v2 changed source/owner vectors
reject before another preparation/publication.
[Private context packet](continuation-10-private-context-vectors.json) records
19/19 focused checks; exact focused Server535/535 passed on build58.
Descriptor-absent catalog/v1/v2 fingerprint golden tests stay intact;
`GuardOffCatalogPreservesOlderActorCompatibilityWhileSchema2StillFailsClosed`
covers advertised/absent descriptor and inactive/removed-marker rows;
`QualifiedCurrentPreservesSourceRowBothMarkersLifecycleOwnerSourceReadOrder`
freezes the required read ordering without a12th metadata read. Full Domain
trust-boundary tests include exact private recovery route policy/anonymous denial.

Separate cold catalog control evidence: live
`SourceGuard_CatalogColdControlWorkIsMeasuredSeparatelyFromWarmCurrent` passed1/1
against Live19,4.237s,runner/control exits0/0/0.
[Cold/warm packet](continuation-10-pinned-cold1-vectors.json) records actual metadata
request/response bytes,five owning index writes and separate warm eleven-read Current.
Removed-route provider-local cancellation maps to typed unavailable while caller
cancellation propagates; query/owning-probe pair passes in focused34/build53.

Full Server Debug/source build53 passed5080 selected,5055 passed,25 declared
DW1 skips,0 failures/errors/not-run643.408s,exit0. Exact skip and frozen asset
identities are in [final gate packet](continuation-10-final-gates.json).

Domain4 passed904/904 with no skips/errors/failures105.836s on compact-v2 assets.
Frozen58 fullServer3 passed5086 selected/5061 passed/25 declared skips,649.802s.
Server60 narrow111/111 verifies final ordinary/removed local-cancellation and
normal append ambiguity/duplicate-extension-key assertions. Server62 owning
older-source9/9 verifies only preactivation structured-missing-method fallback,
exact returned scope/both markers/activation revision before and under lease,
no transient/owned/staged downgrade and retained original unsupported publication.
Final §10 focusedServer5 passed548/548,18.805s,exit0 on freeze65.
Actor/orchestrator/rebuild affected176/176 passed4.935s,including real AddActors
factory activation and actual non-null catalog/named scheduler/outbox closure.
Frozen Live20 focused40 had38pass/2known actor constructor failures,fully
retained as intermediate. Fresh Live21 exact focused40/40 and whole Debug
Live192/191 plus the one declared worker-helper skip pass after the correction.
The separate initial whole Debug Live1 artifact-ordering failure is retained;
exact coherent-host rebuild and owning OQ81/1 plus whole retry2 pass.

All five complete Debug and all five complete Release assemblies passed,
with the real14-package Contracts inventory enabled in both configurations. Exact Release/package
restore/build and pack were executed successfully; the ten advisory warning
occurrences for six distinct pinned-package advisories keep the required
zero-warning gate unproven. No dependency/audit-policy exception is applied.
A failed or unavailable gate remains explicitly unproven; listing it here is
not evidence.

## Final gate disposition

| Complete assembly | Debug selected / passed / declared skips | Release selected / passed / declared skips |
| --- | --- | --- |
| Server | 5102 / 5077 / 25 | 5102 / 5077 / 25 |
| DomainService | 904 / 904 / 0 | 904 / 904 / 0 |
| Client | 1562 / 1562 / 0 | 1562 / 1562 / 0 |
| Contracts,actual14-package ENV | 2339 / 2339 / 0 | 2339 / 2339 / 0 |
| LiveSidecar | 192 / 191 / 1 | 192 / 191 / 1 |

Each configuration selected10099/passed10073 with26 declared skips and no
failures/errors/not-run. Server skips are the unchanged25 DW1 declarations;
the live skip is the unchanged parent-launched worker helper,whose owning
restart parent passes. Exact focused Server548,Domain110 and Live40 also pass.
Both live full runs release owned controls and verify the existing OQ8 owning
cleanup receipt and absent Postgres container.

Exact Release restore/build and actual14-package pack succeeded. The latest
exact build reports0errors/10warning occurrences for six distinct pinned-package
advisories. Therefore the frozen zero-warning requirement is NOT SATISFIED and
Story6.4 acceptance stays open. No dependency/audit-policy exception was applied.
The transitive Gateway retains its existing generatedDebug configuration in
both exact Release build2/build3 logs; actual per-build configurations are
disclosed without implying every dependency is Release. No source/dependency
change was made to hide this observed build behavior.

The [final gate packet](continuation-10-final-gates.json) retains exact commands,
per-gate source/asset identities,all intermediate failures and corrections,
explicit skip inventory and runtime profiles. The closing audit separately
checks source/package bytes,whitespace/privacy and owned process/control cleanup.
No production activation,publication,commit or overall readiness is claimed.
