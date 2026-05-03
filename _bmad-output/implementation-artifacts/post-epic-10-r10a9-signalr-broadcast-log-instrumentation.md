# Post-Epic-10 R10-A9: SignalR Broadcast Log Instrumentation

Status: ready-for-dev

<!-- Source: post-epic-10-r10a8-r9-r10-follow-through-tracking.md R10-A1 residual -->
<!-- Source: epic-10-retro-2026-05-01.md R10-A1 -->
<!-- Source: docs/operations/signalr-operational-evidence.md instrumentation gaps -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **QA / observability engineer responsible for real-time notification evidence**,
I want direct SignalR broadcast and client receipt instrumentation for projection-change signals,
so that future AppHost and runtime proofs can show the SignalR leg without relying only on behavioral UI refresh evidence or broad Debug logging.

## Story Context

Epic 10 delivered the SignalR notification path and R11-A3 later accepted a live AppHost proof for R10-A1, but that proof was intentionally caveated: the sample UI refreshed after the signal, yet direct broadcaster and client receipt logs were not captured at the active log level. R10-A8 closed the retro follow-through by making this residual visible as `post-epic-10-r10a9-signalr-broadcast-log-instrumentation`.

This story closes that residual instrumentation gap only. It is not another Redis backplane proof, tenant-authorization story, reconnect guidance story, operational evidence-pattern rewrite, or AppHost projection proof. The work is to make a single projection-change signal observable at the server broadcast boundary and client receipt boundary with stable structured fields, safe redaction, and tests that prevent evidence drift.

Current HEAD at story creation: `de8859e`.

## Acceptance Criteria

1. **Instrumentation decision is recorded before source changes.** Add a short decision record in this story's Dev Agent Record before implementation. It must choose the exact evidence emitters, log levels, ActivitySource names, EventId ranges, and any configuration gate. The decision must explicitly preserve the public hub payload `ProjectionChanged(projectionType, tenantId)`, the hub route `/hubs/projection-changes`, the group format `{projectionType}:{tenantId}`, and fail-open broadcaster behavior.

2. **Server broadcast start and completion are directly observable.** Instrument `SignalRProjectionChangedBroadcaster.BroadcastChangedAsync()` so a successful broadcast emits structured evidence for broadcast start and completion without requiring global `Microsoft.AspNetCore.SignalR` Debug logging. The evidence must include projection type, tenant id or safe alias, group name, UTC start/completion timestamps or equivalent Activity timing, elapsed milliseconds, success/failure classification, current trace/span identifiers when present, and the logging category. Do not include projection state, ETags, command payloads, access tokens, connection strings, or read-model data.

3. **Fail-open failure evidence remains explicit.** Broadcast failure must still be caught and logged without throwing. The failure evidence must include the same projection/tenant/group fields as success plus exception type and elapsed milliseconds, while preserving command processing, ETag regeneration, and query behavior. Do not promote fail-open warnings into command failures.

4. **Client receipt is directly observable without changing callbacks.** Instrument `EventStoreSignalRClient` so receiving a `ProjectionChanged(projectionType, tenantId)` signal emits structured client-side evidence before consumer callbacks run. The evidence must include projection type, tenant id or safe alias, group name, UTC receipt timestamp or equivalent Activity timing, connection state, callback count, current trace/span identifiers when present, and logging category. Do not change the callback signature, public hub payload, reconnect semantics, or subscription API.

5. **SignalR ActivitySources are enabled where this repo owns telemetry defaults.** Update `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` only if needed so default OpenTelemetry tracing can collect repository-owned SignalR proof spans. Include `Microsoft.AspNetCore.SignalR.Server` and `Microsoft.AspNetCore.SignalR.Client` when useful, and keep the existing `Hexalith.EventStore` source. Do not add a new tracing backend, dashboard, exporter, or broad observability platform migration.

6. **EventId collisions cannot confuse evidence filters.** Resolve or explicitly route the current 1084/1085 collision between `SignalRProjectionChangedBroadcaster` and `ProjectionChangedHub`. A source-code fix should prefer non-overlapping EventId ranges for hub and broadcaster logs. If a source fix is not chosen, the story must record why and must update evidence guidance to require filtering by both category and EventId. Do not leave numeric EventId-only filters ambiguous.

7. **Correlation continuity is falsifiable.** Broadcast and receipt evidence must support correlation with an evidence run id or ambient trace id when a proof run supplies one. If the SignalR protocol cannot propagate a run id without changing the public payload, keep the run id in proof metadata, log scopes, Activity tags, test harness state, or client-side logger scope. Add a negative/correlation-integrity test or evidence note proving stale or mismatched receipt evidence is not accepted as the current trigger.

8. **Operational evidence docs are updated narrowly.** Update `docs/operations/signalr-operational-evidence.md` and the reusable template only for fields made available or clarified by this instrumentation. Keep R10-A6 as the evidence-pattern owner; do not rewrite the whole pattern or change its classification enum. The docs must explain which fields are now directly emitted, which still require a proof harness, and how to filter categories/EventIds safely.

9. **Runtime proof artifact demonstrates the new signal leg when practical.** Create `_bmad-output/test-artifacts/post-epic-10-r10a9-signalr-broadcast-log-instrumentation/README.md` or an evidence file showing one bounded run or focused harness output with server broadcast evidence and client receipt evidence for the same projection/tenant/group. If Docker, DAPR, Aspire, browser automation, or auth setup blocks the run, classify it as `environment-blocker` using the R10-A6 evidence schema and still provide focused unit-test evidence for the instrumentation.

10. **Tests cover success, failure, and callback boundaries.** Add or update focused tests for the broadcaster success log/activity, fail-open failure log/activity, client receipt log before callback execution, callback count, and no public API/payload change. Prefer existing test projects and patterns: `tests/Hexalith.EventStore.Server.Tests/SignalR/SignalRProjectionChangedBroadcasterTests.cs` and `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs`. If service-default tracing registration changes, add the narrowest feasible assertion or document why it is verified by inspection.

11. **Existing story boundaries are preserved.** Do not re-prove R10-A2 Redis cross-instance delivery, R10-A3 tenant authorization, R10-A5 reconnect responsibilities, R10-A6 evidence schema, R10-A7 Redis isolation policy, R11-A3 AppHost projection proof, or R11-A4 valid projection round trip. Reference those artifacts for context only.

12. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and `last_updated` names R10-A9 and the instrumentation decision. At code-review signoff, both become `done` only after tests and evidence artifacts are recorded.

## Scope Boundaries

- Do not add projection state, ETags, aggregate ids, command status, trace ids, run ids, or serialized read-model data to the public SignalR payload.
- Do not make SignalR durable, replay missed signals, or change reconnect/rejoin behavior.
- Do not change tenant authorization rules in `ProjectionChangedHub`; that remains R10-A3 scope.
- Do not change Redis backplane topology or channel-prefix policy; that remains R10-A7 scope.
- Do not add a new external observability backend, production dashboard, alert rule, or metrics pipeline.
- Do not require persistent containers for normal local development.
- Do not commit raw production logs, tokens, connection strings, full network traces, production hostnames, or unsafe tenant/user identifiers.
- Do not initialize or update nested submodules.

## Implementation Inventory

| Area | File | Expected use |
|---|---|---|
| Server broadcaster | `src/Hexalith.EventStore/SignalRHub/SignalRProjectionChangedBroadcaster.cs` | Add server broadcast start/completion/failure evidence while preserving fail-open behavior |
| SignalR hub | `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs` | Resolve EventId collision or document category+EventId filtering |
| Client helper | `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` | Add client receipt evidence before callbacks without changing public API |
| Telemetry defaults | `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` | Add SignalR ActivitySource registration if needed |
| Activity source constants | `src/Hexalith.EventStore/Telemetry/EventStoreActivitySources.cs` and `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` | Reuse existing source naming instead of inventing incompatible names |
| Server tests | `tests/Hexalith.EventStore.Server.Tests/SignalR/SignalRProjectionChangedBroadcasterTests.cs` | Cover success/failure logs or activities |
| Client tests | `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs` | Cover receipt logging and callback ordering |
| Evidence docs | `docs/operations/signalr-operational-evidence.md` and `_bmad-output/test-artifacts/signalr-operational-evidence-template.md` | Narrowly update available fields and filter guidance |
| Runtime evidence | `_bmad-output/test-artifacts/post-epic-10-r10a9-signalr-broadcast-log-instrumentation/` | Store bounded proof or environment-blocker evidence |

## Tasks / Subtasks

- [ ] Task 0: Baseline and decision record (AC: #1, #6, #11)
    - [ ] 0.1 Record baseline HEAD and confirm this story is still `ready-for-dev`.
    - [ ] 0.2 Inspect broadcaster, hub, client helper, service defaults, R10-A6 operations doc, and R11-A3 evidence caveats.
    - [ ] 0.3 Record the instrumentation decision: emitters, log levels, ActivitySource names, EventId ranges, fields, redaction, and proof-gating choice.
    - [ ] 0.4 Confirm no public payload, hub route, group format, reconnect semantics, Redis topology, or tenant authorization change is required.

- [ ] Task 1: Add server broadcast evidence (AC: #2, #3, #6, #7)
    - [ ] 1.1 Emit server-side broadcast start/completion evidence with projection, tenant, group, timestamp/activity timing, elapsed milliseconds, category, and trace/span identifiers when present.
    - [ ] 1.2 Emit fail-open failure evidence with exception type and elapsed milliseconds while preserving non-throwing behavior.
    - [ ] 1.3 Resolve the broadcaster/hub EventId collision or update evidence guidance with a category+EventId filter requirement.
    - [ ] 1.4 Ensure instrumentation does not log projection state, ETags, command payloads, secrets, connection strings, or read-model data.

- [ ] Task 2: Add client receipt evidence (AC: #4, #7)
    - [ ] 2.1 Emit client receipt evidence before callback invocation.
    - [ ] 2.2 Include projection, tenant, group, timestamp/activity timing, connection state, callback count, category, and trace/span identifiers when present.
    - [ ] 2.3 Preserve callback signatures, subscription storage, reconnect/rejoin behavior, and disposal behavior.
    - [ ] 2.4 Add a correlation-integrity guard in tests or evidence so stale/mismatched receipts cannot be accepted silently.

- [ ] Task 3: Wire telemetry defaults only as needed (AC: #5)
    - [ ] 3.1 Inspect whether repository-owned spans use existing `Hexalith.EventStore` source or need explicit SignalR ActivitySource registration.
    - [ ] 3.2 If adding SignalR ActivitySources, update `Extensions.cs` narrowly and keep existing OpenTelemetry exporters/configuration unchanged.
    - [ ] 3.3 Record verification by focused assertion or inspection if no clean test seam exists.

- [ ] Task 4: Update docs and evidence template narrowly (AC: #8, #9)
    - [ ] 4.1 Update `docs/operations/signalr-operational-evidence.md` only for newly emitted fields and safe filtering guidance.
    - [ ] 4.2 Update the evidence template only if field names or proof expectations changed.
    - [ ] 4.3 Add R10-A9 evidence under `_bmad-output/test-artifacts/post-epic-10-r10a9-signalr-broadcast-log-instrumentation/`.
    - [ ] 4.4 If runtime proof is blocked, classify the blocker with the existing evidence schema and preserve focused unit-test evidence.

- [ ] Task 5: Tests and bookkeeping (AC: #10, #12)
    - [ ] 5.1 Run `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~SignalRProjectionChangedBroadcasterTests"` or an equivalent focused server test command.
    - [ ] 5.2 Run `dotnet test tests/Hexalith.EventStore.SignalR.Tests/Hexalith.EventStore.SignalR.Tests.csproj`.
    - [ ] 5.3 Run markdown/link validation for touched docs and evidence artifacts when available.
    - [ ] 5.4 Update this story's Dev Agent Record, File List, Change Log, Verification Status, and sprint-status row.

## Dev Notes

### Architecture Guardrails

- SignalR is an invalidation channel. Query endpoints remain authoritative for projection state, ETags, tenant authorization, and UI state.
- The public hub payload remains exactly `ProjectionChanged(projectionType, tenantId)`. Proof metadata belongs in logs, scopes, Activity tags, or evidence files.
- Broadcast failures are fail-open. They must be observable but must not block ETag regeneration, command processing, projection writes, or query refresh.
- Prefer EventStore-owned structured logs and Activities over broad framework Debug categories for proof evidence. Built-in SignalR diagnostics are supporting context, not proof that a named signal reached a named client.
- Treat tenant id as sensitive unless the evidence run uses a safe test alias. Current code already logs tenant id in some SignalR paths; do not broaden the data surface beyond projection/tenant/group identifiers.
- Any new EventId values should avoid the current 1084/1085 overlap and should be stable enough for evidence filters.
- Use current logging patterns with `LoggerMessage` partial methods and xUnit/Shouldly/NSubstitute tests where possible.

### Current-Code Intelligence

- `SignalRProjectionChangedBroadcaster.BroadcastChangedAsync()` currently sends to group `{projectionType}:{tenantId}`, logs success at Debug EventId 1084, logs fail-open failure at Warning EventId 1085, and catches all broadcast exceptions.
- `ProjectionChangedHub` currently also uses EventIds 1084 and 1085 for tenant authorization denial and validator failure, so numeric EventId-only filters can mix hub and broadcaster evidence.
- `EventStoreSignalRClient.OnProjectionChanged()` currently invokes callbacks without a receipt log, timestamp, Activity, or callback-count evidence.
- `EventStoreSignalRClient` already accepts an optional `ILogger<EventStoreSignalRClient>` and validates projection/tenant parts for subscribe/unsubscribe. Use that seam before adding new public API.
- `AddServiceDefaults()` currently registers OpenTelemetry logging, ASP.NET Core/runtime/http metrics, and tracing sources for the application name and `Hexalith.EventStore`. It does not explicitly add `Microsoft.AspNetCore.SignalR.Server` or `Microsoft.AspNetCore.SignalR.Client`.
- `EventStoreActivitySources.EventStore` exists in the `Hexalith.EventStore` assembly. Prefer reusing existing source names or constants instead of introducing one-off string literals.

### Previous Story Intelligence

- R11-A3 accepted behavioral refresh-after-signal evidence but recorded that direct broadcaster and client receipt Debug logs were not captured at the active log level.
- R10-A6 defined the SignalR operational evidence pattern and listed missing broadcast start timestamp, missing default SignalR ActivitySource registration, missing client receipt timestamp, and missing correlation continuity as instrumentation gaps.
- R10-A6 also warned that EventIds 1084/1085 collide between broadcaster and hub categories; evidence filters must distinguish categories until source IDs are separated.
- R10-A5 documented that reconnect does not replay missed signals. Do not make receipt instrumentation imply durable delivery.
- R10-A7 selected Redis isolation policy and should not be reopened here.

### Testing Standards

- Run affected test projects individually. Do not run solution-level `dotnet test`.
- Focus server tests on broadcaster success/failure instrumentation and fail-open behavior.
- Focus client tests on receipt logging, callback count, callback ordering, and unchanged subscription/reconnect semantics.
- If source code changes only logging or Activity emission, product integration tests are not required unless implementation touches AppHost/runtime proof code.
- If docs or evidence artifacts change, run markdown and link checks when available. Record unavailable tooling explicitly.

### Latest Technical Information

- Microsoft Learn for ASP.NET Core SignalR diagnostics says SignalR server/client tracing uses `DiagnosticSource` and `Activity`; the .NET SignalR server ActivitySource is `Microsoft.AspNetCore.SignalR.Server`, and the client ActivitySource is `Microsoft.AspNetCore.SignalR.Client` in .NET 9+ / .NET 10 environments.
- Microsoft Learn for ASP.NET Core built-in metrics lists `Microsoft.AspNetCore.Http.Connections` SignalR metrics including `signalr.server.connection.duration` and `signalr.server.active_connections`. These are connection-health metrics, not direct proof that a specific projection-change signal reached a client.
- The repository currently uses `Microsoft.AspNetCore.SignalR.Client` and `Microsoft.AspNetCore.SignalR.StackExchangeRedis` version `10.0.5`, and OpenTelemetry packages `1.15.x` in `Directory.Packages.props`.

### Project Structure Notes

- Keep source changes close to `src/Hexalith.EventStore/SignalRHub/`, `src/Hexalith.EventStore.SignalR/`, and `src/Hexalith.EventStore.ServiceDefaults/`.
- Keep evidence under `_bmad-output/test-artifacts/post-epic-10-r10a9-signalr-broadcast-log-instrumentation/`.
- Keep docs updates narrow and additive. R10-A6 remains the canonical evidence-pattern story.
- Do not edit `.agents/skills/`, `.claude/skills/`, `_bmad/bmm/`, or the tools submodule for this implementation story.

### References

- [Source: `_bmad-output/implementation-artifacts/post-epic-10-r10a8-r9-r10-follow-through-tracking.md`] - R10-A1 residual routed to R10-A9.
- [Source: `_bmad-output/implementation-artifacts/epic-10-retro-2026-05-01.md#13-r10-follow-through-annotation-recorded-by-r10-a8-reconciliation`] - R10-A1 caveat and revisit trigger.
- [Source: `_bmad-output/implementation-artifacts/post-epic-11-r11a3-apphost-projection-proof.md`] - AppHost proof caveats for projection actor and SignalR delivery evidence.
- [Source: `_bmad-output/implementation-artifacts/post-epic-10-r10a6-signalr-operational-evidence-pattern.md`] - Evidence schema and instrumentation-gap guidance.
- [Source: `docs/operations/signalr-operational-evidence.md`] - Current operational evidence pattern, latency boundaries, classification values, and deferred instrumentation gaps.
- [Source: `_bmad-output/test-artifacts/signalr-operational-evidence-template.md`] - Reusable evidence template.
- [Source: `src/Hexalith.EventStore/SignalRHub/SignalRProjectionChangedBroadcaster.cs`] - Server broadcast implementation and current success/failure logs.
- [Source: `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs`] - Hub authorization, group management, and current EventId collision.
- [Source: `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs`] - Client receipt callback and reconnect behavior.
- [Source: `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs`] - OpenTelemetry defaults.
- [Source: `_bmad-output/planning-artifacts/prd.md#NFR38`] - SignalR changed signal delivery target.
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-10-SignalR-Real-Time-Notifications`] - Epic 10 SignalR scope.
- [Source: Microsoft Learn, `https://learn.microsoft.com/aspnet/core/signalr/diagnostics?view=aspnetcore-10.0`] - SignalR logging, tracing, and ActivitySource guidance.
- [Source: Microsoft Learn, `https://learn.microsoft.com/aspnet/core/log-mon/metrics/built-in?view=aspnetcore-10.0`] - ASP.NET Core built-in SignalR metrics.

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Decision Record

TBD by dev-story agent before source changes.

### Debug Log References

TBD by dev-story agent.

### Completion Notes List

TBD by dev-story agent.

### File List

TBD by dev-story agent.

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-03 | 0.1 | Created ready-for-dev R10-A9 SignalR broadcast/client-receipt instrumentation story. | Codex automation |

## Verification Status

Story created for pre-development hardening. Implementation and validation are pending `bmad-dev-story`.
