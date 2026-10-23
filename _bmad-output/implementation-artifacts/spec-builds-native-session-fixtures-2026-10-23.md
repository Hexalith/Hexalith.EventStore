---
title: 'Restore native module regression session fixtures'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'e1d8b17160202761ab5c9606cafbcf4d1a4b92c3'
owner_baseline_commit: 'c548eafab396071901f38ba85f4ca3c0a18b48b3'
approval: 'User authorized all CI repairs, publication, and continued latest-main tracking'
context: []
---

<frozen-after-approval reason="user-authorized CI repair">

## Intent

Builds CI run 39941259734 reports three failures in the native runner review regressions. The shared fake-session helper predates required catalog, composition, deadline, and observed-readiness fields, so descriptor creation refuses execution before the Python child starts. Reuse the existing offline provisioning fixture, await session preparation, and preserve the report-custody and committed-outcome assertions. Add child-start evidence to hostile report cases so an early descriptor refusal cannot make those negative cases pass. Keep all production guards unchanged.

</frozen-after-approval>

## Implementation Notes

- Owner: `references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/ModuleRunnerReviewRegressionTests.cs`. Convert its shared helper to `SessionAsync` using `TestEnvironmentProvisioningFixture.PrepareAsync` and `Observe`, as existing selected-runner tests already do. Update its two direct callers and startup callback.
- Place report-written/child-start markers in test Python scripts and require them after execution. Keep token-hang cancellation after awaited session preparation. Preserve foreign-file sentinel, elapsed-time, output retention, terminal outcome, and durable-evidence assertions.
- Verify the entire regression class in Debug, including all encoded-report and unsafe-file cases, because the old incomplete fixture made several negative cases return before child execution.

- Reproduced the exact CI failures locally before correction: the whole regression class ran 43 tests with three failures. The final Debug build has zero warnings/errors, and the same class now passes all 43 tests with zero skips in 8.2 seconds.
- Commands from Builds: `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj --no-restore -c Debug -m:1 -p:GeneratePackageOnBuild=false`; `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class Hexalith.Builds.ModuleTool.Tests.ModuleRunnerReviewRegressionTests`.
- Review strengthened the existing assertions: cancellation waits for the written-report marker under an independent three-second startup bound and both concurrent tasks finish before disposal; cleanup cases prove child execution and retained report counts/hash; encoded-report cases require the correct inspection diagnostics; owned unsafe raw entries must be removed while a foreign parent-link target remains unchanged.
- A separately dispatched EventStore Release run 39946310678 was already publishing green source `e1d8b17160202761ab5c9606cafbcf4d1a4b92c3` during this fixture-only repair. That publication succeeded as v3.127.0; these fixture-only maintenance changes are synchronized afterward.

## Review Triage Log

| Finding | Verdict and evidence | Disposition |
|---|---|---|
| Failed-child cleanup case could pass on an early descriptor refusal | medium: the previous failure exit matches an early refusal without proving native execution | Patched with child marker, retained hash, and native counts |
| Fixed cancellation delay can expire before child startup on a slow host | medium: descriptor setup and child scheduling consumed the old delay | Patched with marker-driven cancellation, bounded startup wait, and awaited tasks |
| Encoded-report negative cases permit unrelated failure diagnostics | medium: null output alone does not prove the intended inspection path | Patched with exact secret-policy and malformed-report diagnostics |
| Unsafe report cases omit owned raw-entry cleanup checks | medium: null retained bytes alone cannot establish custody cleanup | Patched with owned-entry absence and separate foreign-link preservation checks |

- Preserved concurrent upstream repair `07fce74c7807a01521d53f211437e11003028cf9` and rebased the independently verified assertion improvements onto it. The owning Builds commit is `b51924a4a44b58091f1dd71067d2b7aece1ad71e`; its final test source matches the verified 43-case source.
