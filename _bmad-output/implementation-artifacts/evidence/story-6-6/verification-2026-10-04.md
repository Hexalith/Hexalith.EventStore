# Story 6.6 Verification Evidence

Date: 2026-10-04
Spec: `spec-6-6-event-versioning-and-upcasting-implementation.md`
Workflow baseline: `1329b35e52852952ecb2c94aabf100674e9691e3`
Observed final repository HEAD: `f3dc36b934336920b3c7e4bcec5cf52c6327df9d` (equal to `origin/main` at final inspection)

## Checks run

- `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py --mutations` — passed; approved digest `bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050`; 20 obligations and 47 implementation follow-ups remain open.
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — passed with 0 warnings and 0 errors. Executed test classes from `tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll`:
  - `Hexalith.EventStore.Server.Tests.Events.EventPersisterTests` — 38/38 passed.
  - `Hexalith.EventStore.Server.Tests.DomainServices.DaprDomainServiceInvokerTests` — 40/40 passed, including the invoker-to-persister version metadata route.
  - `Hexalith.EventStore.Server.Tests.Actors.AggregateActorDomainResultTests` — 32/32 passed.
- `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — passed with 0 warnings and 0 errors. `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class Hexalith.EventStore.Client.Tests.Events.BoundedPayloadPrimitiveTests` — 4/4 passed.
- `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` — passed with 0 warnings and 0 errors. `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Events.AuthenticatedRawEventPageTests` — 5/5 passed.
- Full Contracts test assembly execution before the final page-test additions — 2,187 passed, 0 failed, 2 skipped. The skips were package inventory checks because `EVENTSTORE_PACKAGE_CONTRACT_DIR` was unset. This full run does not include the final page-test additions.
- `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false` — passed with 0 warnings and 0 errors. This run preceded the final test-only route assertion; the Server.Tests Debug project was rebuilt and focused tests rerun after that assertion was added.
- `git diff --check 1329b35e52852952ecb2c94aabf100674e9691e3..HEAD` and `git diff --check` — passed on the final tracked diff. The newly added evidence file was also checked with `git diff --no-index --check /dev/null _bmad-output/implementation-artifacts/evidence/story-6-6/verification-2026-10-04.md` — passed.

## Incomplete qualification

- `dotnet test` for Contracts.Tests with Microsoft.Testing.Platform reported 0 tests and exit code 5. The built xUnit assembly was used for focused and full execution instead.
- Aspire dependents remained waiting on Keycloak during baseline inspection. No live sidecar/provider qualification, two-host run, full affected regression set, or package/API compatibility gate completed. The Builds package pin differs from its submodule `HEAD`; it was preserved.
- M1 registry/codec resolution/fingerprints/executable evolution chain, M2 production version negotiation and authenticated raw reader/writer integration, and M3–M8 runtime integration and evidence remain open. These checks therefore do not establish Story 6.6 acceptance or V2 activation readiness.
- During implementation, repository `HEAD` advanced from the saved workflow baseline to `f3dc36b934336920b3c7e4bcec5cf52c6327df9d`; the concurrent commit was preserved. `VerifiedEffectiveCommandEvent.cs` had hidden assume-unchanged state and differed from `HEAD`; the implementation agent edited it before checking its original bytes. No pre-edit copy is available, so possible overlap with hidden local edits remains unresolved and the current content was preserved.
