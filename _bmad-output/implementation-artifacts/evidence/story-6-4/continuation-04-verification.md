# Continuation 04 verification

Repository HEAD remained `e17e7294f540ebba42fe25192998689f0ecd89c1`.
The approved normative SHA-256 was recomputed before edits and matched
`78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`.
Its detached owner approval remains unchanged. No normative byte, package pin,
Git branch, commit, submodule or environment admission was changed.

All build/runner commands below ran individually and sequentially from the
repository root. Independent test/build commands did not overlap.

## Exact final commands

```bash
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*ProjectionDeliveryIdempotencyCoordinatorTests' -class '*NamedProjectionDispatchCoordinatorTests' -class '*EventStoreProjectionDeliveryHistoryReaderTests' -class '*ProjectionRebuildProductionPathTests' -class '*ProjectionDeliveryCutoverTests' -class '*ProjectionDeliveryReconcilerTests' -class '*ProjectionDeliveryStateContractTests' -class '*ProjectionDeliveryDiagnosticsTests' -class '*ProjectionEraseCoordinatorTests' -class '*EventPersisterTests' -class '*AggregateMetadataTests' -class '*AggregateActorTests' -class '*AggregateActorInfrastructureFailureTests' -class '*AggregateActorGetEventsTests' -class '*DaprProjectionLifecycleGatewayTests' -class '*ProjectionDeliveryDocumentationTests'
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*EventStoreDomainServiceExtensionsTests' -class '*DomainProjectionHandlerCompatibilityTests' -class '*DomainProjectionDispatcherV2Tests' -class '*MixedProjectionCatalogTests' -class '*DomainFencedSharedProjectionIntegrationTests' -class '*DomainPartitionedSharedProjectionRebuildTests'
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*DaprReadModelBatchTests' -class '*ReadModelBatchFingerprintTests' -class '*AdminOperationalIndexHostedServiceTests'
git diff --check
```

| Command | Exit | Observed result |
| --- | --- | --- |
| Server Debug/source build | 0 | 0 warnings, 0 errors |
| Server focused runner | 0 | 402 passed, 0 failed, 0 skipped; 10.144 seconds |
| DomainService Debug/source build | 0 | 0 warnings, 0 errors |
| DomainService focused runner | 0 | 200 passed, 0 failed, 0 skipped; 106.175 seconds |
| Client Debug/source build | 0 | 10 existing NU1902/NU1903 crypto warnings, 0 errors |
| Client focused runner | 0 | 59 passed, 0 failed, 0 skipped |
| Tracked whitespace | 0 | `git diff --check` passed |

Sanitized final runner summaries, source hashes and asset hashes are retained
beside this record. Full provider output, payloads and stacks are not used as
qualification evidence. These focused results do not prove complete production
costs, all W0–W6, all crash boundaries, full affected regression, Contracts,
live sidecars or Release/package gates. No live sidecar was started here.
Installed daprd 1.18.7 versus pinned 1.18.2 remains unqualified.

## Intermediate failures retained

- An intermediate Server build failed with CS0234 because the new test helper
  qualified `ProjectionDispatchOptions` with the DomainService namespace. The
  helper now uses its owning Client namespace. The final build is clean.
- The first 60-case coordinator run had two new staged-replay failures. The
  recorder's typed `TrySaveStateAsync` deliberately returned a configured stub
  result instead of persisting transport records. Added opt-in stateful typed CAS
  in that existing recorder and enabled it for the production replay fixture;
  the default behavior is preserved. The next 60-case run passed.
- The first 140-case DomainService run failed in the new negotiation test because
  its custom test domain lacked an authoritative aggregate/keyed processor, so
  metadata correctly contained no domain. The test now registers a keyed domain
  processor. The next 140-case run passed.
- A 73-case coordinator run failed only because the new mixed-route assertion
  used `unsupported_capability` instead of the existing SDK constant's actual
  `unsupported_dispatch_capability`. The assertion now uses that constant.
- An intermediate 401-case focused Server run failed in the older assertion that
  full replay was an unimplemented protocol. Full replay is now implemented;
  the unsupported assertions remain for undeclared mapped/shared protocols, and
  a separate real full-catalog qualification test proves activation preserves C,
  complete digests and zero handler effects. The final 402-case run passed.

Transient logs were overwritten by later final runs. These summaries record the
observed failures and corrections; this was not an uninterrupted green run.

## Implemented checks

- H=256/257/771/10000 complete-prefix fallback, exactly two measured source
  passes, one preparation, complete detail/index/receipt/checkpoint equality,
  followed by current inspection with zero envelope reads or new row writes.
- Cold mapped owners at H=257/10000 use the existing bounded immutable parts;
  the large fixture spans multiple <=262,144-byte parts.
- Lost stage and commit responses reuse the original reservation and immutable
  manifest. Repeated stage/commit/verify across recreated service providers does
  not prepare again. Changed input under the same identity is rejected.
- Head MessageId, owner identity, generation and conditional-operation admission
  precede candidate publication. Invalid plans preserve live detail/index/C.
- Old metadata request/recomputation/dispatch compatibility; explicit guard
  version negotiation; unknown-version rejection; descriptor-bound proof.
- Owning shared terminal fold receipt and required writer checks, zero repair
  during inspection, conflicting source digest rejection and same-event receipt
  redelivery. Generic batch proof and qualified siblings cannot substitute for
  unsupported complete shared-family selection/freshness proof.

None of these individual measurements is the complete approved cost vector.
