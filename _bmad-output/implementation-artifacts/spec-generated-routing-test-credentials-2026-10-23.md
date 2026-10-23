---
title: 'Generate credentials in Dapr routing tests'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '93be37d26c15ba1bb951f23d13d7e1dd9df1ddde'
approval: 'User authorized CI fixes, publication, and continued latest-main tracking'
context: []
---

<frozen-after-approval reason="user-authorized CI repair">

## Intent

Current main's CI fails the tracked-content secret guard at two newly added Dapr routing tests. Replace their committed credential values with fresh generated test values. Preserve target switching, rotation, removal, and one-factory-read-per-send assertions. Keep production code, the secret scanner, historical evidence, and all security rules unchanged.

</frozen-after-approval>

## Implementation Notes

- Change only the input values in `DaprServiceInvocationHandlerTests.SendAsyncWithRotatedOrRemovedCredentialResolvesEachSend` and `DaprServiceInvocationRegistrationTests.FactoriesUseDependencyInjectionOncePerSend`.
- The existing scanner reproduced exactly the two CI findings through its unchanged compiled `FindViolations` entry point; its source has not changed since the local Server test assembly was built. The temporary probe runs in the conventional Debug output directory so repository-root discovery remains correct.
- Verify the changed files and this record with that same scanner, and build/run the two affected Client classes in Debug with source references. Existing tests cover the behavioral contract; no new diagnostic-mirroring tests are needed.

- Failing source: `93be37d26c15ba1bb951f23d13d7e1dd9df1ddde`; [CI run 39935474875](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/39935474875), job `120354271410`. The only failed test was `SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets`, reporting the two Client test paths. The full Client suite passed in that same CI run.
- Scanner command: `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/SecretScanProbe.dll tests/Hexalith.EventStore.Client.Tests/Handlers/DaprServiceInvocationHandlerTests.cs tests/Hexalith.EventStore.Client.Tests/Registration/DaprServiceInvocationRegistrationTests.cs _bmad-output/implementation-artifacts/spec-generated-routing-test-credentials-2026-10-23.md`. Before correction, each test file produced exactly one finding; after correction both files and this record produced zero findings. The probe invokes the production guard's unchanged scanner implementation; it is temporary validation tooling outside tracked source.
- Debug/source build passed with zero warnings and errors: `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --no-restore -c Debug -m:1 -p:UseHexalithProjectReferences=true`.
- Both affected classes passed: `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class Hexalith.EventStore.Client.Tests.Handlers.DaprServiceInvocationHandlerTests -class Hexalith.EventStore.Client.Tests.Registration.DaprServiceInvocationRegistrationTests`; 14 tests, zero errors, failures, or skips.

## Review Triage Log

| Finding | Verdict and evidence | Disposition |
|---|---|---|
| Successful scanner result not recorded | low: results were available from the unchanged scanner but absent from this record | Patched with exact command and before/after counts |
| Behavioral build/test results not recorded | low: completed build and test logs showed success | Patched with commands, zero-warning build, and 14 passing tests |
| CI diagnosis lacks run/source citation | low: the baseline SHA existed but the failing run was not cited | Patched with exact run URL, source SHA, job ID, and failed test |
