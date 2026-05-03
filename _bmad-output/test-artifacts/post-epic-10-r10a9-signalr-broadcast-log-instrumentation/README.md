# R10-A9 SignalR Broadcast Log Instrumentation Evidence

Schema version: `signalr-operational-evidence/v1`

## Run Identity

- Evidence run id: `r10a9-focused-tests-2026-05-03`
- Story/proof key: `post-epic-10-r10a9-signalr-broadcast-log-instrumentation`
- UTC run window start: `2026-05-03T11:24:31Z`
- UTC run window end: `2026-05-03T11:45:00Z`
- Commit SHA/build version: `b9f52e7` plus working tree changes for R10-A9
- Evidence author/agent: `GPT-5 Codex`
- Repository status: `dirty`
- Classification: `environment-blocker`

## Environment

- OS/runtime host: Windows PowerShell workspace
- .NET SDK/runtime: repository restore/build used by `dotnet test`
- Aspire AppHost state: existing AppHost detected at `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`
- Aspire resource blocker: `eventstore` and `sample` were stopped/finished with exit code `-1` during baseline resource inspection; Aspire MCP returned no console logs for either resource
- Docker/DAPR/browser/auth: not exercised for this bounded evidence artifact
- Observability backend: not required for focused unit evidence
- Environment blockers: live runtime proof was not practical from the current AppHost state without first restoring/rebuilding stopped runtime resources

## SignalR Configuration

- Hub path: `/hubs/projection-changes`
- Group format: `{projectionType}:{tenantId}`
- Public hub payload confirmed as `ProjectionChanged(projectionType, tenantId)` only: `yes`
- Runtime proof/test-only gate: not used

## Trigger And Broadcast

- Trigger shape: focused unit harness invoking `SignalRProjectionChangedBroadcaster.BroadcastChangedAsync`
- Projection type: `order-list`
- Tenant id or safe tenant alias: `acme`
- Broadcast group: `order-list:acme`
- Broadcast start EventId/category: `1090` / `Hexalith.EventStore.SignalRHub.SignalRProjectionChangedBroadcaster`
- Broadcast completed EventId/category: `1091` / `Hexalith.EventStore.SignalRHub.SignalRProjectionChangedBroadcaster`
- Broadcast fail-open EventId/category: `1092` / `Hexalith.EventStore.SignalRHub.SignalRProjectionChangedBroadcaster`
- Broadcast elapsed milliseconds: asserted present in focused tests
- Broadcast exception type: `InvalidOperationException` asserted for fail-open test
- Broadcast ActivitySource: `Hexalith.EventStore`, activity `EventStore.SignalR.BroadcastProjectionChanged`

## Client Receipt

- Trigger shape: focused unit harness invoking private `EventStoreSignalRClient.OnProjectionChanged` through existing reflection test seam
- Projection type: `counter`
- Tenant id or safe tenant alias: `acme`
- Group: `counter:acme`
- Receipt EventId/category: `2090` / `Hexalith.EventStore.SignalR.EventStoreSignalRClient`
- Receipt connection state: `Disconnected` in the focused non-network unit harness
- Receipt callback count: asserted as `1` and `2`
- Callback boundary: receipt log assertion runs before callback counter increments
- Public API/payload: unchanged

## Reliability Controls

### Control 1

- Control name: mismatched tenant receipt rejection
- Product failure guarded against: stale or unrelated receipt accepted as current trigger evidence
- Setup: subscribed to `counter:acme`, then invoked `ProjectionChanged(counter, other-tenant)`
- Expected result: no callback and no receipt log
- Observed result: focused client test passes
- Pass/fail: pass

### Correlation-Integrity Control

- Mismatched or missing field: group/tenant mismatch
- Expected validation result: `fail`
- Observed validation result: no callback/log evidence accepted for mismatched group
- Evidence path: `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs`

## Focused Test Evidence

- `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~SignalRProjectionChangedBroadcasterTests"`: passed, 8/8
- `dotnet test tests\Hexalith.EventStore.SignalR.Tests\Hexalith.EventStore.SignalR.Tests.csproj`: passed, 35/35
- `dotnet test tests\Hexalith.EventStore.Server.Tests\Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~OpenTelemetryRegistrationTests.ServiceDefaults_RegistersBothActivitySources"`: passed, 1/1

## Result

- Final classification: `environment-blocker`
- Pass/fail summary: focused instrumentation tests pass for server broadcast success/failure, fail-open behavior, client receipt before callbacks, callback count, mismatch rejection, and telemetry source registration
- Product failures: none observed in focused tests
- Environment blockers: current AppHost state did not support a bounded live runtime proof
- Instrumentation gaps: proof harnesses still own evidence run id propagation outside the public SignalR payload
