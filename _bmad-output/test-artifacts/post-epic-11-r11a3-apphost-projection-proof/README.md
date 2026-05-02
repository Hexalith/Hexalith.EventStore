# Post-Epic 11 R11A3 AppHost Projection Proof

## Scope

This bundle captures the AppHost-managed proof for the server projection path:

command submission -> event persistence -> sample `/project` invocation -> projection actor state -> query -> ETag -> SignalR-driven UI refresh.

No production code was changed for this story.

## Prerequisites

To repeat this proof on a fresh machine:

1. **Docker** must be running (Dapr sidecar containers and Redis state-store/pubsub depend on it).
2. **Dapr CLI** initialized: `dapr init` (or `dapr init --slim` on Windows). On the original proof environment, `placement.exe`/`scheduler.exe` were absent under `$HOME/.dapr/bin`; Aspire-managed Dapr sidecars handled the actor placement successfully, but a fresh `dapr init` is the safest baseline.
3. **Stop the AppHost before running tests.** `dotnet test tests/Hexalith.EventStore.Sample.Tests` will fail with file-lock errors if the AppHost is still running because the sample process holds DLLs in `samples/Hexalith.EventStore.Sample/bin/Debug/net10.0`. Stop the proof AppHost (Ctrl+C in the Aspire run terminal and verify no orphaned `Hexalith.*` processes) before invoking `dotnet test`.

## AppHost Run

Final proof run:

```powershell
$env:EnableKeycloak = 'false'
$env:EventStore__Counter__TenantId = 'sample-tenant'
$env:EventStore__Authentication__Tenants__0 = 'sample-tenant'
aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
```

Notes:

- Keycloak was disabled for the final proof run.
- The sample UI tenant was overridden to `sample-tenant` so browser proof, API proof, command logs, and query logs use the same tenant/domain/aggregate.
- Dapr placement/scheduler executables were not present under `$HOME/.dapr/bin` on this Windows environment, so no manual placement/scheduler process was started. Aspire-started Dapr sidecars were healthy, and actor calls completed successfully.

## Proof Identity

- Proof window start UTC: `2026-05-01T15:12:58.1131793+00:00`
- Command message id: `r11a3-430ea44d14c8478791f67322165590b4`
- Command correlation id: `r11a3-832d7bc77883465895ce5ba94a180024`
- Tenant: `sample-tenant`
- Domain: `counter`
- Aggregate id: `counter-1`
- Query type: `get-counter-status`
- Projection type: `counter`
- Dev JWT claims were recorded in `final-proof-identity.json`; the bearer token itself was not persisted.

## Direct API Evidence

Artifacts:

- `final-proof-identity.json`
- `final-query-payload.json`
- `final-command-payload.json`
- `final-baseline-query-response.txt`
- `final-command-response.txt`
- `final-after-query-response-5s.txt`

Results:

- Baseline query: HTTP 200, payload count `2`, ETag `"Y291bnRlcg.gSYnuYG9L0GwlQP4m5l4kg"`.
- Command submission: HTTP 202, status location `/api/v1/commands/status/r11a3-832d7bc77883465895ce5ba94a180024`.
- Follow-up query after 5 seconds: HTTP 200, payload count `3`, ETag `"Y291bnRlcg.ZwLDkiDUPkeVdsfywMq-Yg"`.
- The `Y291bnRlcg` ETag prefix decodes to `counter`, which matches the projection type.

## Browser Evidence

Artifacts:

- `silent-reload-env-override-before.png`
- `ui-silent-reload-env-override-before-snapshot.md`
- `silent-reload-env-override-after.png`
- `ui-silent-reload-env-override-after-snapshot.md`
- `silent-reload-final-after-direct-command.png`
- `ui-silent-reload-final-after-direct-command-snapshot.md`

Observed flow:

- Before UI command: count `1`, refresh count `0`.
- After UI Increment button: count `2`, refresh count `1`.
- After direct API command while the page stayed open: count `3`, refresh count `2`, last UI command still `IncrementCounter at 17:12:08`.

The final page update at `17:12:58` happened after the direct API command, without another UI command click.

## Log And Trace Evidence

See:

- `resources-before.txt`
- `trace-ids.txt`
- `eventstore-logs.txt`
- `sample-logs.txt`
- `sample-blazor-ui-logs.txt`
- `negative-log-search.txt`
- `test-results.txt`

Important trace ids:

- Final direct command: `8ff4e7f`
- Final direct follow-up query: `89d739a`
- UI query after final direct command: `6dc925c`
- SignalR negotiate: `eda8c24`
- SignalR WebSocket connect: `1a84da8`
- SignalR group join: `23a3036`

## Acceptance Evidence Matrix

| AC | Evidence |
| --- | --- |
| 1 | `resources-before.txt` shows `eventstore`, `sample`, `sample-blazor-ui`, `statestore`, and `pubsub` Running/Healthy; AppHost run section above shows `EnableKeycloak=false`. |
| 2 | Resource snapshot in `resources-before.txt` plus the proof identity carried by three files: `final-proof-identity.json` (tenant, domain, aggregate, messageId, correlationId, non-secret JWT claims, proof-window start), `final-command-payload.json` (tenant/domain/aggregate/messageId on the wire), `final-query-payload.json` (tenant/domain/aggregate/queryType/projectionType on the wire). The bearer token was not persisted. |
| 3 | `final-command-response.txt` records HTTP 202, `Location: http://localhost:8080/api/v1/commands/status/r11a3-832d7bc77883465895ce5ba94a180024`, `Retry-After: 1`, and the response body's `correlationId` matching the proof identity. The browser submission path was paired with this direct API call to capture the headers. |
| 4 | `eventstore-logs.txt` log ids 160, 165, 168, 181, 182 show command received, events stored, command completed, events persisted (`EventCount=1; NewSequence=3; Stage=EventsPersisted`), and events published — all under trace `8ff4e7f`. |
| 5 | `eventstore-logs.txt` log ids 170-174 show Dapr service invocation to sample `/process` and `/project` returning HTTP 200 with `AppId=sample, ResultType=Success`; AppHost trace `8ff4e7f` contains the domain invocation span; `samples/Hexalith.EventStore.Sample/Program.cs` maps `/project` to `CounterProjectionHandler.Project` (not the malformed-fault path). |
| 6 | Projection write-side log lines (`EventReplayProjectionActor.UpdateProjectionAsync`, `ProjectionUpdateOrchestrator.ProjectionStateUpdated`) are emitted at `LogLevel.Debug` and were not captured at the AppHost's active `Information` level. The substitute proof: (a) `/project` HTTP 200 with valid `ProjectionResponse` payload, (b) `eventstore-logs.txt` `ProjectionActor/counter:sample-tenant:counter-1/QueryAsync` trace `89d739a` after the command, (c) query count delta 2 → 3, and (d) ETag regeneration on the `counter` projection. Caveat documented; residual hardening tracked under R11-A1 (`post-epic-11-r11a1-checkpoint-tracked-projection-delivery`). |
| 7 | `eventstore-logs.txt` log id 183 records the ETag actor update with `ActorId=counter:sample-tenant; ETagPrefix=Y291bnRl`. The base64 prefix `Y291bnRlcg` decodes to `counter`, proving the regenerated ETag belongs to the `counter` projection. ETag values in `final-baseline-query-response.txt` (`...gSYnuYG9L0GwlQP4m5l4kg`) and `final-after-query-response-5s.txt` (`...ZwLDkiDUPkeVdsfywMq-Yg`) differ — the API responses are the canonical ETag-change evidence (the browser UI snapshots truncate the ETag display to a fixed prefix and cannot prove the change). |
| 8 | `final-baseline-query-response.txt` and `final-after-query-response-5s.txt` show payload `count: 2` → `count: 3`, both responses HTTP 200, both with `Y291bnRlcg.*` (`counter`) ETag. The query payload (`final-query-payload.json`) confirms tenant `sample-tenant`, domain `counter`, aggregate `counter-1`, queryType `get-counter-status`, projectionType `counter`. Note: the query response body's `correlationId` is a fresh GUID minted per request (not the proof prefix `r11a3-…`); the proof-window link is via wall-clock and the trace IDs in `trace-ids.txt`. |
| 9 | `sample-blazor-ui-logs.txt` and `trace-ids.txt` show SignalR negotiate (`eda8c24`), WebSocket connect (`1a84da8`), and group join (`23a3036`). Direct moment-of-delivery logs (`SignalRProjectionChangedBroadcaster.BroadcastSent`, client `OnProjectionChanged`) emit at `Debug` and were not captured. The AC-permitted refresh-after-signal evidence is the silent-reload page (no polling timer in `SilentReloadPattern.razor`) advancing from `Refresh count: 1` to `Refresh count: 2` after the direct API command without a UI command click. Caveat documented. |
| 10 | Browser screenshots and snapshots show the silent-reload page refreshed from count `2` to `3` after the direct API command while the page stayed open. The UI configuration was confirmed effective via `EventStore__Counter__TenantId=sample-tenant` (see AppHost Run section); the snapshots evidence the configured tenant, projection, and query type were used. |
| 11 | `negative-log-search.txt` records bounded searches across EventStore, sample, and sample-blazor-ui logs returned by Aspire MCP for the proof window 2026-05-01T15:12:58Z–15:13:03Z. Term list expanded post-review to cover `InvalidProjectionResponse`, `NoDomainServiceRegistered`, `CheckpointSaveExhausted`/`Failed`, `ProjectionChangeNotificationFailed`, plus generic `error`/`Error`/`exception`/`Exception`/`fail`/`warn`. |
| 12 | This README is the repeatable runbook (Prerequisites + AppHost Run + payload shapes + AC matrix + linked artifacts). |
| 13 | No `.cs`/`.razor`/`.csproj` files in the commit. `test-results.txt` records `dotnet test tests/Hexalith.EventStore.Sample.Tests` 63/63 PASS after AppHost shutdown; server projection tests not required (no server code changed). |

## Superseded Warm-Up Artifacts

The first no-Keycloak proof used direct API calls successfully but the browser UI was still configured for the default `tenant-a`. Those artifacts are retained for transparency:

- `proof-identity.json`
- `query-payload.json`
- `command-payload.json`
- `baseline-query-response.txt`
- `command-response.txt`
- `after-query-response-5s.txt`
- `silent-reload-current.png`
- `ui-silent-reload-current-snapshot.md`

The final proof artifacts listed above are authoritative for this story.
