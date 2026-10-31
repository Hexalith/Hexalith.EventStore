---
title: Await actual owned subprocess termination within its existing bound
status: verified-awaiting-protected-pr-checks
---

CI run41218513032, live-sidecar job124587997056 on main61e6fa853, failed the cancellation case because the owned sleep child was briefly runnable after its parent exited. Process.Kill(entireProcessTree:true) sends termination signals without waiting for every child to finish; the fixture asserted the Linux child state immediately after waiting only for the parent.

The test now observes that same owned PID until it is absent or a zombie within the original seven-second total deadline. It preserves the final termination assertion and includes the observation wait inside the existing deadline. The owned subprocess implementation, cancellation/timeout policy, real native infrastructure and all production behavior remain unchanged.

Validation: `dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Release --disable-build-servers -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:Version=3.130.0 -p:Hexalith1BuildPackageProps=/home/administrator/projects/hexalith/parties/references/Hexalith.Builds/Props/Directory.Packages.props -warnaserror` passes with zero warnings/errors. `dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class '*RouteCatalogFixtureToolTests'` passes all6 cases, zero failures/skips,2.677s. Protected PR checks must complete before merge/publication.

## File Scope

Allowed files for this story:

- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/RouteCatalogFixtureToolTests.cs`
- `_bmad-output/implementation-artifacts/spec-wait-for-owned-subprocess-exit.md`
