# 6.1-P2 query security and projection capability: owner handoff

Status: **draft, not accepted**. Observed locally on 2026-09-24. This record does not
authorize a release, select a rollback triple, or close the Projects P2 gate.

## Candidate coordinates

| Coordinate | Local observation | Acceptance state |
| --- | --- | --- |
| EventStore source | Clean local HEAD `04682eac0464b6b18bc02a39187f244fec3ab9bd`, including the cursor and live-sidecar restart tests | Candidate revision observed locally; not owner-accepted |
| Builds source | Clean sibling HEAD `754d2b4b6615c5004606ba837dd9c925c57e8d14` | Observed locally; not an accepted P0 runner or selected P2 pin |
| Locally evaluated package version | `3.106.0` (`dotnet msbuild src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj -getProperty:Version -p:Configuration=Release`) | Not a published or accepted P2 package pin |
| Projects consumer pin | Unchanged | Owner selection pending |

## Public capability surface

- `QueryEnvelope` retains its original 9- and 10-parameter constructors and the
  prior 15-member dual-principal constructor. The additive constructor appends
  `string? delegationId = null` after `Audience`; `[DataMember] DelegationId`
  follows the prior members. Legacy deserialization yields `null`.
- `SubmitQuery` retains its original and prior 17-member entry points. Its
  additive primary constructor appends `string? DelegationId = null`.
- `DualPrincipalIdentity` retains its five-member constructor/deconstruction
  and adds `string? DelegationId = null`. The gateway derives it only from one
  valid, bounded RFC 8693 `act.sub` claim; absent or ambiguous evidence is
  unknown. Both query-router chains carry the value to `QueryEnvelope`.
- `ProjectionEventDto` retains the eight-member constructor/deconstruction and
  adds a nine-member form with `long GlobalPosition = 0`. Negative values are
  rejected; zero denotes legacy/unknown. `ProjectionEventWireBuilder` forwards
  the exact stored event position, including gaps.
- `QueryCursorScope AddProjectionWatermark(long? watermark)` appends a canonical
  `watermark:<positive invariant decimal>` segment and rejects unknown or
  non-positive values. A consumer must read the watermark from the durably
  written read model before issuing a cursor. It cannot use allocator state.
- The existing opt-in safe-denial router maps forbidden and both not-found
  forms to the same external not-found shape; non-opted routes retain their
  prior behavior.

## Local verification

| Command or lane | Result |
| --- | --- |
| `dotnet restore Hexalith.EventStore.slnx -m:1 /nr:false` | Passed; restored five projects, including the three whose cached packages blocked the first build. |
| `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:q` | Current-HEAD rerun exit 0: 0 warnings, 0 errors. The first attempt before restore failed with `NETSDK1064` for three unavailable cached packages. |
| `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll` | Exit 0; 804 passed. |
| `dotnet tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll` | Exit 0; 13 passed. |
| `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Projections.ProjectionContractTests` | Exit 0; 9 passed. |
| `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Queries.ProjectionAdapterContractTests` | Exit 0; 108 passed. |
| `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Authorization.DualPrincipalClaimsHelperTests` | Exit 0; 38 passed. |
| `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Queries.SafeDenialQueryRouterTests` | Exit 0; 27 passed. |
| `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj --configuration Release --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:q` | Exit 0; 0 warnings/errors. |
| `dotnet tests/Hexalith.EventStore.IntegrationTests/bin/Release/net10.0/Hexalith.EventStore.IntegrationTests.dll -class Hexalith.EventStore.IntegrationTests.ContractTests.ProjectionWatermarkRebuildIntegrationTests` | Exit 0; 3 passed. The new case binds a cursor to persisted watermark 41, then rejects gapped advance to 47 and a different Tenant scope. |
| `dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ProjectionWatermarkProcessRestartTests` | Exit 0; 1 passed. Separate worker processes replay the same Dapr/Redis-persisted event history and converge on read-model watermark 109. |
| `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll` | Current rerun exit 0; 3,370 total, 0 failed, 25 skipped. The earlier run had one secret-scan failure in `DaprDomainQueryInvokerTests.cs`; its fixture was corrected before this rerun. |
| `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll` | Completed exit 1; 2,075 total, 97 failed, 0 skipped. All 97 failures are in `Packaging.*` governance/evidence tests, including unavailable nested submodule files and a pinned Builds revision. The focused P2 contract classes above passed. |
| `git diff --check` | Passed. |
| `dotnet tool run hexalith-module test --profile reads --filter Story=6.1-P2` from the Projects workspace | Exit 1: `Cannot find a tool in the manifest file that has a command named 'hexalith-module'.` |

## G-4 and residual gates

No G-4 fixture, TRX, JSON summary, or authenticated persisted cross-Tenant
negative control was produced for this candidate. The live-sidecar test proves
an operating-system process restart against Dapr/Redis-persisted event and
read-model state, but seeds the event history directly and does not replace the
required G-4 composition fixture. The full Contracts gate is not green, and
no G-4 machine-readable evidence was saved. Therefore P2 is open and Story 6.1 remains
blocked on its separately owned prerequisites.

The owner must approve an exact EventStore source/package pin, Builds runner
pin, Projects consumer pin, and rollback triple only after the missing local
and G-4 evidence passes. No rollback trigger or executable rollback procedure
has been selected or approved. A future rollback record must demonstrate that
prior events and read models remain readable, watermark-bound cursors fail
closed under the restored scope, and P2/P3/P4/Story 6.1 return to blocked.
