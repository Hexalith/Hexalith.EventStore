# Continuation 03 verification

Repository HEAD remained `e17e7294f540ebba42fe25192998689f0ecd89c1`.
The approved normative SHA-256 and detached statement SHA-256 were recomputed
before edits and remain respectively
`78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711` and
`657f0dc25b18b826b37ab3be53273043fa98ca61a40923c9a913922816bf531f`.
All commands ran from the repository root, individually and sequentially.

## Exact final commands

```bash
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*ProjectionDeliveryIdempotencyCoordinatorTests' -class '*NamedProjectionDispatchCoordinatorTests' -class '*EventStoreProjectionDeliveryHistoryReaderTests' -class '*ProjectionRebuildProductionPathTests' -class '*ProjectionDeliveryCutoverTests' -class '*ProjectionDeliveryReconcilerTests' -class '*ProjectionDeliveryStateContractTests' -class '*ProjectionDeliveryDiagnosticsTests' -class '*ProjectionEraseCoordinatorTests' -class '*EventPersisterTests' -class '*AggregateMetadataTests' -class '*AggregateActorTests' -class '*AggregateActorInfrastructureFailureTests' -class '*AggregateActorGetEventsTests' -class '*DaprProjectionLifecycleGatewayTests'
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*DaprReadModelBatchTests' -class '*ReadModelBatchFingerprintTests' -class '*AdminOperationalIndexHostedServiceTests'
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainProjectionHandlerCompatibilityTests' -class '*DomainProjectionDispatcherV2Tests' -class '*MixedProjectionCatalogTests' -class '*DomainFencedSharedProjectionIntegrationTests' -class '*DomainPartitionedSharedProjectionRebuildTests'
git diff --check
```

Final Server build/test was repeated after the final Client proof-reader change
so the Server output carries the final Client assembly. The command listing is
grouped by project; builds and runner invocations did not overlap.

| Command | Exit | Observed result |
| --- | --- | --- |
| Server Debug/source build | 0 | 0 warnings, 0 errors |
| Server focused runner | 0 | 385 passed, 0 failed, 0 skipped; 9.156 seconds |
| Client Debug/source build | 0 | 10 existing NU1902/NU1903 warnings, 0 errors |
| Client focused runner | 0 | 59 passed, 0 failed, 0 skipped; 0.358 seconds |
| DomainService Debug/source build | 0 | 0 warnings, 0 errors |
| DomainService focused runner | 0 | 105 passed, 0 failed, 0 skipped; 97.560 seconds |
| Tracked whitespace | 0 | `git diff --check` passed |

The Client advisories concern centrally pinned
`System.Security.Cryptography.Pkcs/Xml` 10.0.12. Neither dependencies nor audit
policy was changed. They prevent a zero-warning Release claim.

## Earlier test failures retained

Two intermediate executions failed in the new test
`SourceGuardQualification_NonemptySourceCannotInventFreshZeroOwnerProgress`:

- The five-class Server run returned exit 1: 197 total, 1 failure, 0 skips,
  8.250 seconds. Its record equality assertion compared arrays with
  deserialized lists. The persisted scalar values were unchanged. The assertion
  now checks the preserved schema/checkpoint/evidence and absent mutation.
- The first broader focused run returned exit 1: 385 total, 1 failure, 0 skips,
  9.734 seconds. The recorder represents no state transaction as `null`, while
  the assertion expected an empty collection. The assertion now expects `null`.

These are summaries of observed runner output. Transient logs were overwritten
by subsequent reruns; the final retained logs and this explicit failure record
must not be represented as an uninterrupted all-green execution.

## Evidence limits

Retained runner logs contain outcomes only. Source/test identities are recorded
in `continuation-03-source-sha256.txt`; built final test assets are recorded in
`continuation-03-assets-sha256.txt`. No payloads, provider credentials or stacks
are qualification evidence. No live sidecar was started by this continuation.
No full affected regression, Contracts, live or Release/package gate was run.
Historical broad results do not qualify these newer changes.
