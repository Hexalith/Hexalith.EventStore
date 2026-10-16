# Continuation 07 verification

**Incomplete and unqualified. No environment was admitted or cut over.**
HEAD remains e17e7294f540ebba42fe25192998689f0ecd89c1. Approved normative digest
78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711 is unchanged;
detached owner approval digest remains
657f0dc25b18b826b37ab3be53273043fa98ca61a40923c9a913922816bf531f.
No dependency/audit-policy/Git-index/branch/commit/submodule mutation was performed.

## Commands and results

Commands ran sequentially from the repository root; no two test runners overlapped
and no build overlapped a runner. Builds used Debug/source references:

```bash
aspire start --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
aspire describe --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json
aspire stop --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -method '*NuGetDependencyMetadata_AllowsVersionMetadataAndRanges'
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll
git diff --check
```

Aspire baseline resources were Running/Healthy and the exact AppHost was stopped
before runtime edits. No live guard lanes were run in continuation07. Do not retain
raw Aspire environment output, which includes runtime tokens.

| Check | Result | Evidence |
| --- | --- | --- |
| Server build9 | 0 warnings/errors,11.09s | /tmp/story-6-4-cont07-server-build9.log |
| Build9 focused5 | 536 passed,0 failed/skipped/errors,14.337s | /tmp/story-6-4-cont07-focused5.log |
| Build9 full Server | 4883 total:4857 passed,1 failed,25 existing DW1 skips,0 errors,648.134s | /tmp/story-6-4-cont07-server-full.log |
| Isolated unchanged security method | 10 passed,0 failed/skipped/errors,0.155s | /tmp/story-6-4-cont07-security-isolated.log |
| Server build13 / focused7 | 0 warnings/errors;210 passed,0 failed/skipped/errors,14.502s | /tmp/story-6-4-cont07-{server-build13,focused7}.log |
| Server build14 / protected1 | 0 warnings/errors;5 passed,0 failed/skipped/errors,2.438s | /tmp/story-6-4-cont07-{server-build14,protected1}.log |
| Server build16 / cleanup2 | 0 warnings/errors;6 passed,0 failed/skipped/errors,1.151s | /tmp/story-6-4-cont07-{server-build16,cleanup2}.log |
| Server build17 / focused8 | 0 warnings/errors;532 passed,0 failed/skipped/errors,15.520s | /tmp/story-6-4-cont07-{server-build17,focused8}.log |
| Client build1 / focused1 | 10 existing crypto advisories,0 errors;58 passed,0 failed/skipped/errors,0.284s | /tmp/story-6-4-cont07-client-{build1,focused1}.log |
| Client build2 / full | 10 existing crypto advisories,0 errors;1562 passed,0 failed/skipped/errors,5.808s | /tmp/story-6-4-cont07-client-{build2,full}.log |
| DomainService build1 | 0 warnings/errors,4.19s | /tmp/story-6-4-cont07-domain-build1.log |

The final checks are appended below.
Focused8 used the original continuation06 class selection plus
DaprProjectionDeliveryStateStoreTests, with -showLiveOutput; it includes source
retention, full probe costs, same-C operator rebuild, exact subset cleanup and
streamed mapped replay. It excludes TrustedEffectAggregateErasureTests; that class
is added explicitly to the final focused pass. All scoped variants use the same
in-process xUnit runner shown above.

## Retained full-run failure

The full build9 Server run failed only
SecretsProtectionTests.NuGetDependencyMetadata_AllowsVersionMetadataAndRanges
for version [8.14.0-alpha,8.14.0). Its existing RandomSecret negative replacement
unexpectedly yielded an empty violation list. The random value was not captured.
A coincidental valid NuGet version is a hypothesis supported by the generator and
scanner's accepted grammar, not a proven root cause. The exact failed output and
isolated10/10 rerun are retained in continuation-07-security-failure.txt. No scanner
or security-test code/rules were changed. That full run remains a failed intermediate
regression gate on older build9 assets, not a passing current final gate.

## Intermediate corrections and limitations

- Focused1:171/172, one test's fake clock older than its original actual UpdatedAt;
  fixed test clock to original.UpdatedAt+1minute.
- Build2 failed on new test RecordedOutcomes property name; corrected PerTargetOutcomes.
- Focused2:180/181, stale fake ReturnedSequences assertion after switching to actual
  actor ownership; replaced with genuine source cost plus persisted semantic checks.
- Focused3 passed183. Build8 failed on missing fixture namespace import; corrected.
  Focused4 inadvertently used older build7 assets after that failed build (534 passes);
  it is not evidence for the failed/newer sources.
- Focused6:205/207, two failures after switching all P to real Probe: the byte-over
  test expected a thrown exception instead of the production typed rejected result;
  wider ordinary rebuild fixture needed its original unconditional stable manifest
  profile. Both corrected; focused7 passed210. Budgets were not relaxed.
- Build15 failed because two ShouldAllBe assertions passed method groups rather
  than expression lambdas. Corrected in build16. Cleanup1 inadvertently used older
  build14 assets after failed build15 and is not new-test evidence. Cleanup2 passed
  on build16, including the actual subset/private-material test.
- Full DomainService build1 run:899 total,896 passed,3 failed,0 skips/errors,105.148s.
  All failures were frozen versioned-intake rejection before handler/store resolution.
  Guarded staged replay had resolved enrollment too early. Restored RequireLegacy
  before guarded replay resolution and before its enrollment enumeration; tests and
  rejection codes were preserved. Exact failures:continuation-07-domain-failure.txt.
- Source/assembly identities and final process audit are retained separately.
  Source/SDK debug proof is not Release/package or pinned full live qualification.
  Prior continuation06 live tests cover source primitives only. No new full Server
  rerun was requested before the remaining runtime implementation; it remains open.
- continuation-07-production-costs.json retains48 actual owning-seam vectors from
  focused9. Counters represent logical/serialization work. They are not native HTTP
  bytes, built-in sidecar attempts, complete working-memory evidence or live budgets.
- The protected fixture performs actual AES-GCM/Brotli reads at the existing provider
  seam. It is an explicit test provider, not a deployed provider qualification.
  Exact source-array and readable-wire-array64MiB/+1 are separate actual cases.
- The mapped transport uses bounded measure/upload streams, avoiding the prior full
  duplicate serialized array. All-mode retained working-byte gate remains required.

## Final sequential checks

- DomainService build2: exit0,0 warnings/errors,3.82s. Corrected intake class:9
  passed,0 failed/skipped/errors,0.261s (/tmp/story-6-4-cont07-domain-intake.log).
- Corrected full DomainService:899 passed,0 failed/skipped/errors,104.857s,exit0
  (/tmp/story-6-4-cont07-domain-full2.log). This rerun follows the early validation
  restoration; the preceding failed full run remains retained and disclosed.
- Final Server build18: exit0,0 warnings/errors,21.11s
  (/tmp/story-6-4-cont07-server-build18.log).
- Final Server focused9 on build18:533 passed,0 failed/skipped/errors,15.295s,
  exit0 (/tmp/story-6-4-cont07-focused9.log). Exact selection is the focused8 command
  plus -class '*TrustedEffectAggregateErasureTests' and -showLiveOutput. Mapped replay
  assertions verify bounded uploaded parts and two actual serialization passes.
- Source-erasure restart tests reside in TrustedEffectJointOffboardingTests; this
  entire class ran separately after focused9, on the same build18 assets, with
  -class '*TrustedEffectJointOffboardingTests' -showLiveOutput. See
  /tmp/story-6-4-cont07-erasure-final.log for the exact result appended below.
- git diff --check:exit0. No owned runner/build/AppHost/Dapr process remains at
  final process capture. See continuation-07-process-audit.json.

Final source-erasure class runner summary:

```text
  Finished:    Hexalith.EventStore.Server.Tests (ID = 'a22c81c1644009a3e6b0a7fd61c21c28090d74900df170ecbf330b134b2940c6')
=== TEST EXECUTION SUMMARY ===
   Hexalith.EventStore.Server.Tests  Total: 20, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 0.457s
```
