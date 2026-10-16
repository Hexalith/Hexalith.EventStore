# Continuation 01 verification

Source revision: `e17e7294f540ebba42fe25192998689f0ecd89c1`, dirty working tree.
Approved normative SHA-256:
`78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`.
SDK: 10.0.401. Mode: local Debug/source; no live or Release qualification.
`continuation-01-identities.json` records retained logs, changed source files
and exact test-directory assembly identities. DomainService tests ran against
their own retained build outputs before later Server-only deadline changes;
no claim is made that every run used the identical whole-tree source snapshot.

All commands below ran from the repository root and returned **exit 0**.

```bash
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*ProjectionDeliveryIdempotencyCoordinatorTests' -class '*NamedProjectionDispatchCoordinatorTests' -class '*EventStoreProjectionDeliveryHistoryReaderTests' -class '*ProjectionRebuildProductionPathTests' -class '*ProjectionDeliveryCutoverTests' -class '*ProjectionDeliveryReconcilerTests' -class '*ProjectionDeliveryStateContractTests' -class '*ProjectionDeliveryDiagnosticsTests' -class '*ProjectionEraseCoordinatorTests' -class '*EventPersisterTests' -class '*AggregateMetadataTests' -class '*AggregateActorTests' -class '*AggregateActorInfrastructureFailureTests' -class '*AggregateActorGetEventsTests' -class '*DaprProjectionLifecycleGatewayTests'
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainProjectionHandlerCompatibilityTests' -class '*DomainProjectionDispatcherV2Tests' -class '*MixedProjectionCatalogTests' -class '*DomainFencedSharedProjectionIntegrationTests' -class '*DomainPartitionedSharedProjectionRebuildTests'
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*DaprReadModelBatchTests' -class '*ProjectionRouteCatalogFingerprintTests'
git diff --check
```

The Client class wildcard for `ProjectionRouteCatalogFingerprintTests` matched
no class; the 21 executed tests are the `DaprReadModelBatchTests` subset. No
catalog-test execution is inferred from that unmatched selection.

| Check | Retained log | Result |
| --- | --- | --- |
| Server build | `continuation-01-server-build.log` | 0 warnings, 0 errors |
| Server focused | `continuation-01-server-focused.log` | 344 passed, 0 failed, 0 skipped; 7.842s |
| DomainService build | `continuation-01-domain-build.log` | 0 warnings, 0 errors |
| DomainService focused | `continuation-01-domain-focused.log` | 105 passed, 0 failed, 0 skipped; 92.795s |
| Client build | `continuation-01-client-build.log` | 10 existing NU1902/NU1903 warnings, 0 errors |
| Client batch/receipt subset | `continuation-01-client-focused.log` | 21 passed, 0 failed, 0 skipped; 0.215s |

The existing Client warnings concern pinned
`System.Security.Cryptography.Pkcs/Xml` 10.0.12. Dependencies and audit settings
were not changed. This continuation does not prove zero-warning Release or
package gates. Full affected suites, Contracts, real pinned Dapr/Redis and
Release/package gates were not executed in this continuation. No sidecars
were started; HTTP/test fixtures dispose their owned clients/providers.
