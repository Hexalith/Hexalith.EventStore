---
title: 'Restore the live catalog harness qualification for PR 376'
type: 'bugfix'
created: '2026-10-31'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned synchronization intent; repair its required qualification gate">

## Intent

**Problem:** Synchronizing EventStore through protected PR 376 is blocked by seven live-sidecar failures at `c7b927c30f3af7a48d2f0d6aad5b822308cf0113`. The newly added test-only request observer imposes a global 1 MiB buffering ceiling on pre-existing large actor and projection replay requests. A separate subprocess assertion observes the child immediately after termination is requested and races the kernel's exit transition.

**Approach:** Restore the existing qualification behavior through a small test-harness correction. Retain request buffering and rewind for immutable tenant selection, while removing the observer's independent body ceiling. Let the configured host, endpoint and Dapr transports enforce their existing limits. Observe actual owned-child termination within the original seven-second total test deadline instead of assuming an immediate zombie state.

Given nonseekable actor binary arguments or a projection replay JSON request larger than 2 MiB, when the startup-filter pipeline consumes it and acquires catalog authority, then every original byte remains available, the exact tenant is selected and the consumed position is restored. Given the old 1 MiB observer ceiling, these regressions must fail rather than silently using a seekable test stream.

Given cancellation or timeout of the owned shell and its `sleep` descendant, when cleanup returns, then bounded polling must prove the descendant is absent or a zombie within the same original seven-second elapsed budget. A surviving child must fail the assertion; normal completion must still return its original output. Handle disappearance between process-state reads without hiding other I/O failures.

Preserve all existing tests, selectors, authorization, immutable catalog scope, persistence/effect assertions, transport limits and the exact 2 MiB projection-page refusal. Do not alter production `src/`, dependency versions, Git protections, CI job selection, or timeouts. Keep the original failed hosted run and artifact as evidence. Work only in the isolated owning EventStore worktree; preserve the standalone checkout's concurrent sharding work and McpCli's unrelated changes.

The local implementation is reversible and introduces no public API. Publication and the final McpCli gitlink update remain the separately authorized synchronization steps after verification and review. No production-readiness, package-publication, or downstream acceptance claim follows from these fixture corrections.

</frozen-after-approval>

## Implementation Notes

- Baseline hosted failure: workflow `41216293727`, live job `124581736741`, seven failed, 282 passed, four pre-existing worker skips. Retained artifact SHA256 `b3fb363e43f921d2756aefdd0cda53dda9032dd737334d2dc6992c35827d9194`; full logs and extracted failed-case descriptions are in `/tmp/hexalith-sync-20261031-BzPhAb/`.
- Read-only investigations independently confirmed the global observation buffer ceiling and the immediate child-state race. The owning standalone checkout's sharding changes are isolated from this branch. Only the new owning worktree's two directly declared AI.Tools/Builds dependencies were initialized; no nested submodule was initialized.
- Added two nonseekable startup-pipeline regressions to `Fixtures/LiveAdmissionRouteCatalogTests.cs`, reusing its exact immutable authority factory and `LiveByteCountingStream`. Both fail against the original filter with `IOException: Buffer limit exceeded` (`eventstore-buffer-regression-red.{log,xml}`); the isolated Release build passed with zero warnings/errors.
- Removed only the observer's `bufferLimit` argument, retaining its 30 KiB spill threshold and underlying host/endpoint/sidecar ceilings. The regressions verify binary actor and JSON replay bodies above 2 MiB, full-byte hashes, tenant selection, rewind and consumed-position restoration, and dispose the spill stream explicitly.
- `Integration/RouteCatalogFixtureToolTests.cs` now polls for absence/zombie state using the original stopwatch and seven-second deadline. The elapsed assertion follows child-state verification; only missing-file/directory races are accepted. The owned process helper and its five-second cleanup bound are unchanged.
- Green focused verification: the Release build (`dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --configuration Release --no-restore -m:1 -p:UseHexalithProjectReferences=false -warnaserror`) had zero warnings/errors. Direct DLL execution with `-class '*LiveAdmissionRouteCatalogTests' -class '*RouteCatalogFixtureToolTests'` passed 22 cases without failures/skips; log/XML: `/tmp/hexalith-sync-20261031-BzPhAb/eventstore-harness-focused.{log,xml}`.
- The descendant-termination mutation changed only the isolated helper's `entireProcessTree` argument to false, rebuilt, and ran `-method '*OwnedInfrastructureSubprocessHasBoundedCancellationAndNormalCompletion'`. Both cancellation/timeout cases failed on surviving owned children; normal completion passed. The two exact test-owned `sleep 60` survivors were terminated, the helper was restored byte-for-byte, and the restored build passed. Artifact: `/tmp/hexalith-sync-20261031-BzPhAb/eventstore-child-termination-negative-summary.json` with matching log/XML.
- Independent blind review returned three findings. A real open/read disappearance race was reproduced with an owned process and already-open `/proc` descriptor: Linux returned errno 3 after exit/reaping (`eventstore-proc-esrch-reproduction.json`). Specific handling and a partial-body cursor regression are pending while the current live probes retain immutable producer inputs.
- Local original-case verification is currently pending: direct DLL execution selects all theory rows of the four original projection methods under a 900-second outer deadline (17 cases, including the six original projection failures). Artifacts: `/tmp/hexalith-sync-20261031-BzPhAb/eventstore-original-live-cases.{log,xml}`. Full exact-head hosted qualification and protected publication remain pending; the earlier hosted failure is retained.
- The original projection verification completed successfully: 17 passed, zero failures/skips, 806.589 seconds, with local `daprd` 1.18.7. It includes every original failed projection case and the unchanged parameter controls. The command used repeated `-method` selectors for `ReplayRecovery_GenericHandlerReusesOneOriginalManifestAcrossThreeLostDurableResponses`, `SourceGuard_RealActorOversizedEnvelopeReturnsTypedPageFailureBeforeAnyEffects`, `SourceGuard_CurrentMatrixUsesExactlyElevenMetadataReadsAndPreservesEveryOwningSurface`, and `SourceGuard_RealActorHandlerAndStores_ProveTailThenReadOnlyCurrent` on the built Release DLL, with `-result-xml /tmp/hexalith-sync-20261031-BzPhAb/eventstore-original-live-cases.xml`.
- Review refinements retain the same scope: four startup-pipeline rows now cover both EOF and a 257-byte partial position for actor binary/replay JSON bodies. The child-state reader recognizes only missing-file, missing-directory, or Linux ESRCH (errno/HResult 3), with focused disappearance/error-preservation controls; its polling loop retains the same original total deadline. Full exact-head hosted qualification remains pending on the final repair candidate.
- Final local review-refinement build passed with zero warnings/errors; `-class '*LiveAdmissionRouteCatalogTests' -class '*RouteCatalogFixtureToolTests'` passed 28 cases, zero failures/skips, 3.285 seconds. Artifacts: `/tmp/hexalith-sync-20261031-BzPhAb/eventstore-harness-final.{log,xml}`. All three review findings are patched and none deferred. This `done` status records the reviewed local repair artifact; protected hosted acceptance and repository synchronization remain separate pending gates.
- During the probes, the owning PR branch advanced to `6bcd40e3158d5b99e8d94cd300575768a6e479ab` with six concurrent owner commits, including native actor/stamped-call buffering bypasses and the bounded child-exit wait. Preserve those implementations during the separately authorized Git reconciliation; the local results above bind the original repair based on `c7b927c30f3af7a48d2f0d6aad5b822308cf0113`, and do not claim qualification of those concurrent production changes.

## Review Triage Log

- **medium / patch:** Linux can return ESRCH when an already-open `/proc/<pid>/stat` loses its process. The owned-process reproduction returned errno 3; the [kernel path](https://github.com/torvalds/linux/blob/master/fs/proc/base.c) returns that error and [.NET maps its raw errno into IOException.HResult](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/Common/src/Interop/Unix/Interop.IOErrors.cs). Added narrow handling and controls proving unrelated I/O errors propagate.
- **low / patch:** EOF-only controls could miss a future regression restoring every cursor to EOF. Added partial actor/JSON consumption controls proving exact non-EOF restoration, the remaining suffix and complete body hashes.
- **low / patch:** Verification notes lacked the completed green/negative results while live checks were pending. Recorded the 22-case green run, the expected two-failure cleanup mutation, restored-helper evidence, and the completed 17-case live result. Exact-head hosted results remain explicitly pending.
