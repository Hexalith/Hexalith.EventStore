# 6.1-P2 query security and projection capability: owner handoff

Status: **draft, not accepted**. Observed locally on 2026-09-24. This record does not
authorize a release, select a rollback triple, or close the Projects P2 gate.

## Candidate coordinates

| Coordinate | Local observation | Acceptance state |
| --- | --- | --- |
| EventStore source | `ffb6901a5b840af7be030ff246435b4bad00b79c` plus the uncommitted cursor integration test in `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ProjectionWatermarkRebuildIntegrationTests.cs` | No immutable candidate revision |
| Builds source | `2fba3497043fe5ffcfe4dc44c51a09eae9b950ab` in the dirty root-declared sibling checkout | Not an immutable or selected P2 pin |
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
| `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -m:1 /nr:false -p:UseSharedCompilation=false` | Passed: 0 warnings, 0 errors. The first attempt before restore failed with `NETSDK1064` for three unavailable cached packages. |
| `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll` | Exit 0; 804 passed. |
| `dotnet tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll` | Exit 0; 13 passed. |
| `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Projections.ProjectionContractTests` | Exit 0; 9 passed. |
| `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll -class Hexalith.EventStore.Contracts.Tests.Queries.ProjectionAdapterContractTests` | Exit 0; 108 passed. |
| `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Authorization.DualPrincipalClaimsHelperTests` | Exit 0; 38 passed. |
| `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Queries.SafeDenialQueryRouterTests` | Exit 0; 27 passed. |
| `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj --configuration Release --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:q` | Exit 0; 0 warnings/errors. |
| `dotnet tests/Hexalith.EventStore.IntegrationTests/bin/Release/net10.0/Hexalith.EventStore.IntegrationTests.dll -class Hexalith.EventStore.IntegrationTests.ContractTests.ProjectionWatermarkRebuildIntegrationTests` | Exit 0; 3 passed. The new case binds a cursor to persisted watermark 41, then rejects gapped advance to 47 and a different Tenant scope. |
| `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll` | Exit 1; 3,357 total, 1 failed, 25 skipped. The sole failure is the pre-existing `SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets`, which reports four lines in `DaprDomainQueryInvokerTests.cs`. |
| `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll` | Interrupted (exit 58); 1,776 discovered and 31 failures observed. Failures include unavailable nested submodule files and the unavailable pinned Builds commit `22a578b576a515d2af214fe81859447fffc97981`. This is not a completed green gate. |
| `git diff --check` | Passed. |
| `dotnet tool run hexalith-module test --profile reads --filter Story=6.1-P2` from the Projects workspace | Exit 1: `Cannot find a tool in the manifest file that has a command named 'hexalith-module'.` |

## G-4 and residual gates

No G-4 fixture, TRX, JSON summary, persisted cross-Tenant negative control,
or operating-system process-restart proof was produced for this candidate. The
in-memory integration test recreates a DI provider and replays through the
supported projection seam; it does **not** prove an OS-process restart or a
persisted production state store. No test logs or TRX files were saved from
these local runs. The full Contracts and Server gates are also
not green as described above. Therefore P2 is open and Story 6.1 remains
blocked on its separately owned prerequisites.

The owner must approve an exact EventStore source/package pin, Builds runner
pin, Projects consumer pin, and rollback triple only after the missing local
and G-4 evidence passes. No rollback trigger or executable rollback procedure
has been selected or approved. A future rollback record must demonstrate that
prior events and read models remain readable, watermark-bound cursors fail
closed under the restored scope, and P2/P3/P4/Story 6.1 return to blocked.
