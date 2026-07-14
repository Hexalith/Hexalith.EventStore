# Projection delivery v2 persisted-state evidence

Evidence captured on 2026-07-14 from implementation baseline `2794ecba4c435de5e53603aa6080b8d32d669858` plus the uncommitted Story 1.13 implementation under review. Final validation ran after the release-only workspace head advanced to `a62075a7a818b609beedfcaf04952c893abb6757`.

## Environment

- .NET SDK `10.0.301`, target `net10.0`
- DAPR CLI `1.18.0`, runtime `1.18.1`, .NET packages `1.18.4`
- Docker `29.4.3`
- component `state.redis`, `actorStateStore=true`
- Redis `redis:6`, image digest `redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf`
- shared live-sidecar fixture health gate: DAPR HTTP/metadata healthy, Redis reachable, EventStore host running, exact v2 writer marker active

## Commands and results

```text
dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --no-restore --filter FullyQualifiedName~ReadModelBatchLiveSidecarTests
Passed: 6, Failed: 0

dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --no-restore --filter FullyQualifiedName~ProjectionDeliveryCutoverLiveSidecarTests
Passed: 1, Failed: 0

dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --no-restore --filter FullyQualifiedName~NamedProjectionDispatchLiveSidecarTests
Passed: 4, Failed: 0
```

## Persisted assertions

The live lane drives production orchestration, named dispatch, DAPR `/project/v2`, real detail/index handlers, Story 1.10 batch storage, and Redis. It inspects persisted state after quiescence rather than treating HTTP status or mock calls as proof.

- in-order delivery persisted independent detail/index delivery rows, projection checkpoints, completed batch receipts, lifecycle-compatible writes, and an empty converged retry item;
- a partial index failure left the detail route completed, the index route fenced and retryable, and the stable head `MessageId` in retry/batch identity; recovery completed only the index sibling;
- exact completed replay left handler invocation counts, detail/index JSON, delivery rows, and batch receipts unchanged;
- simultaneous same-history invocations from separate coordinator resolutions produced one detail and one index handler invocation; the active duplicate did not invoke a handler;
- a later trigger followed by an earlier canonical trigger converged to the same completed rows as one in-order history;
- gapped and sequence/identity/content-conflicting input did not invoke either handler or advance delivery state; supplying the canonical missing history then converged;
- partial-prefix, conflict/abort restoration, and post-dispatch cancellation batch scenarios preserved the old view or reconciled the same stable batch without duplicate writes;
- marker activation required backup/quiescence/no-downgrade attestations, readiness required exact v2, a downgraded row classified as schema regression, and per-scope erase preserved the store-global marker.

Evidence intentionally omits payloads, fingerprints, physical state keys, ETags, exception messages, tokens, and secrets.
