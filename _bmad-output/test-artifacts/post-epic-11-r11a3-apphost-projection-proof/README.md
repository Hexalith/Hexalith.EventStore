# Post-Epic 11 R11A3 AppHost Projection Proof

## Scope

This bundle captures the AppHost-managed proof for the server projection path:

command submission -> event persistence -> sample `/project` invocation -> projection actor state -> query -> ETag -> SignalR-driven UI refresh.

No production code was changed for this story.

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
| 1 | `resources-before.txt` shows AppHost resources healthy; final command listed above shows `EnableKeycloak=false`; Dapr sidecars healthy. |
| 2 | `final-proof-identity.json`, `final-command-payload.json`, and `final-query-payload.json` record tenant, domain, aggregate, message id, correlation id, and JWT claims without storing the token. |
| 3 | `eventstore-logs.txt` log ids 160, 165, 168, 181, and 182 show command received, events stored, command completed, events persisted, and events published. |
| 4 | `eventstore-logs.txt` log ids 171-174 show Dapr service invocation to sample `/process` and `/project` returning HTTP 200. |
| 5 | `eventstore-logs.txt` log id 170 records sample domain service success with `EventCount=1`; AppHost trace `8ff4e7f` includes the domain invocation span. |
| 6 | Projection debug logs were not emitted at the active log level. The accepted projection state is evidenced by the successful post-command `ProjectionActor/counter:sample-tenant:counter-1/QueryAsync` trace and the query count increase from 2 to 3. |
| 7 | `final-baseline-query-response.txt` and `final-after-query-response-5s.txt` show the pre/post query count delta and the changed ETag. |
| 8 | The post-command ETag prefix is `Y291bnRlcg`, matching `counter`; `eventstore-logs.txt` log id 183 records the ETag actor update. |
| 9 | `sample-blazor-ui-logs.txt` and `trace-ids.txt` show SignalR negotiate, WebSocket connect, and group join traces. |
| 10 | Browser screenshots and snapshots show the page refreshed from count 2 to 3 after the direct API command while the page stayed open. |
| 11 | `negative-log-search.txt` records bounded searches for projection, SignalR, query, and command error terms. |
| 12 | This README is the runbook and links every captured artifact. |
| 13 | `test-results.txt` records `dotnet test tests/Hexalith.EventStore.Sample.Tests` passing 63/63 after AppHost shutdown. Server projection tests were not required because no server code changed. |

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
