# Post-Epic-22 R22A1 — QueryRouter weak ActorProxy fix evidence

Date: 2026-05-19
Story: `post-epic-22-r22a1-query-router-actor-proxy-fix`

## 1. Baseline failure (ST0)

The approved sprint-change proposal `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-19-queryrouter-actor-proxy-nre.md` captured the live Aspire failure:

- Every `POST /api/v1/queries` for `QueryType=list-tenants` returned HTTP 500.
- EventStore console log `EventId 1202` (`Hexalith.EventStore.Server.Queries.QueryRouter`).
- Inner exception: `NullReferenceException` raised inside `Dapr.Actors.Client.ActorProxy.InvokeMethodAsync<TRequest,TResponse>`.
- Routing context: `ActorId=tenants:system:index`, `QueryType=list-tenants`.

Code inspection confirmed the bug shape exactly as the proposal described:

- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` (pre-fix) created the actor proxy with
  `actorProxyFactory.CreateActorProxy<IProjectionActor>(new ActorId(actorId), actorTypeName)` and then
  invoked through the weak path via a cast inside the private `InvokeProjectionActorAsync` helper:
  `if (proxy is ActorProxy actorProxy) { return actorProxy.InvokeMethodAsync<...>(nameof(IProjectionActor.QueryAsync), envelope, cancellationToken); }`.
- DAPR's strongly typed dispatch proxy inherits from `ActorProxy` at runtime but does not initialize the
  weak/JSON invocation state, so `InvokeMethodAsync<TRequest,TResponse>` dereferences a null internal
  field — producing the `NullReferenceException` observed in EventStore logs.
- `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` (`ListTenantsAsync`)
  sends the live query through the EventStore service-invocation path with
  `domain=tenants, aggregateId=index, queryType=list-tenants, projectionActorType=TenantsProjectionActor`.

The live NRE was not reproduced locally in this session. The story accepts the MCP-captured production
evidence from the approved sprint-change proposal per ST0 sub-task wording. Aspire smoke is also recorded
as operator-only below (ST5 sub-evidence).

## 2. Test results (ST5)

### Focused regression suite

```
dotnet test tests/Hexalith.EventStore.Server.Tests --configuration Release \
  --filter "FullyQualifiedName~QueryRouter|FullyQualifiedName~SubmitQuery|FullyQualifiedName~DefaultProjectionActorInvoker"
```

Result: **90/90 passed, 0 failed, 0 skipped** (Release).

This slice includes:

- 28 `QueryRouterTests` covering routing tiers, envelope construction, all adapter-edge failure
  categories (null result, missing payload, malformed JSON, actor-not-found, generic actor exception),
  cancellation propagation (pre-cancel, mid-invocation, token capture), the `list-tenants` regression
  pin (`ActorId=tenants:system:index`, actor type `TenantsProjectionActor`), and a guard test that
  asserts `IActorProxyFactory.CreateActorProxy<IProjectionActor>` is never called by the production
  constructor — this guard fails against the old typed-proxy-plus-cast implementation.
- 6 `DefaultProjectionActorInvokerTests` covering the default DAPR invoker directly:
  `IActorProxyFactory.Create(...)` is called with the expected `ActorId` and actor type,
  `CreateActorProxy<IProjectionActor>(...)` is *not* called, pre-cancelled tokens short-circuit
  before any factory call, and argument null/whitespace guards throw the right exceptions.
- The remaining tests are `SubmitQuery*` coverage that exercises the gateway-side query pipeline
  on top of the refactored `QueryRouter`.

### Full Release solution build

```
dotnet build Hexalith.EventStore.slnx --configuration Release
```

Result: **0 warnings / 0 errors** across the full solution including AppHost, Admin UI, and integration test assemblies.

## 3. Aspire smoke (ST5 sub-evidence)

Initial implementation evidence recorded this smoke as deferred until a full Aspire restart could be
run. That deferral is superseded by the review smoke update below, which rebuilt the topology and
verified the Tenants workflow through `eventstore-admin`.

Reference operator validation flow used by the completed review smoke:

1. `dapr init` (one-time per workstation).
2. `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`.
3. Visit the Admin UI Tenants page (or `POST /api/v1/queries` with `QueryType=list-tenants`,
   `ProjectionActorType=TenantsProjectionActor`, `Tenant=system`, `Domain=tenants`, `AggregateId=index`).
4. Assert no `EventId 1202` / DAPR `NullReferenceException` in the EventStore log stream for
   `ActorId=tenants:system:index`.
5. Record whether the response renders tenant rows or the empty-state — both are valid green outcomes
   for this story. The bug being fixed is the NRE; an empty list with HTTP 200 is success.

### Review smoke update (2026-05-19)

Review smoke completed after discovering the previously running Aspire instance was stale (`dotnet run --no-build`) and still exercising the pre-fix `QueryRouter.cs:line 62` binary. The stale run reproduced the original failure and was discarded as invalid post-fix evidence.

Fresh run command:

```
EnableKeycloak=false aspire run --detach --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json
```

Fresh run build evidence:

- Aspire stopped the previous instance and rebuilt the topology.
- Debug build completed with **0 warnings / 0 errors**.
- EventStore and Admin.Server health endpoints returned HTTP 200.

Smoke request:

```
GET http://localhost:8090/api/v1/admin/tenants
Authorization: Bearer <dev symmetric JWT, redacted>
```

Smoke result:

```
STATUS=200 OK
BODY=[]
```

EventStore log evidence from the fresh run:

- `Query received: CorrelationId=01KRZZT62WKY4HCVSGC3B3QAAJ, QueryType=list-tenants, TenantId=system, Domain=tenants, AggregateId=index`
- `MediatR pipeline exit: CorrelationId=01KRZZT62WKY4HCVSGC3B3QAAJ ... DurationMs=124.137`
- Search count for `"EventId":1202`, `Dapr.Actors.Client.ActorProxy.InvokeMethodAsync`, and `ActorId=tenants:system:index.*NullReferenceException` in the fresh EventStore log: **0**.

Observed Admin/API state:

- The Tenants workflow returned an empty tenant list (`[]`) successfully. This is a valid empty-state outcome for this story; the previous failure mode was HTTP 500 with QueryRouter `EventId 1202`.
- A separate `DaprETagService` warning was observed after the successful response: `ETag actor fetch failed: ActorId=tenant-index:system, ExceptionType=NullReferenceException. Proceeding without ETag (fail-open).` This is not the QueryRouter weak ActorProxy NRE and did not block the Tenants response.

## 4. Files changed

Production:

- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` — refactored to consume `IProjectionActorInvoker`;
  obsolete private `InvokeProjectionActorAsync` helper deleted; routing/cancellation/failure-mapping
  semantics preserved.
- `src/Hexalith.EventStore.Server/Queries/IProjectionActorInvoker.cs` (new, internal seam).
- `src/Hexalith.EventStore.Server/Queries/DefaultProjectionActorInvoker.cs` (new, default DAPR
  weak-proxy invoker).
- `src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj` — added
  `InternalsVisibleTo DynamicProxyGenAssembly2` so NSubstitute can proxy the internal seam.

Test:

- `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs` — rewritten to substitute
  `IProjectionActorInvoker`, with new `list-tenants` regression test, weak-path guard, and
  cancellation-token capture.
- `tests/Hexalith.EventStore.Server.Tests/Queries/DefaultProjectionActorInvokerTests.cs` (new) —
  proves `Create(...)` is called and `CreateActorProxy<IProjectionActor>(...)` is not.

AppHost cleanup:

- `src/Hexalith.EventStore.AppHost/DaprComponents/configstore.yaml` — **deleted** (orphan).

Documentation:

- `_bmad-output/planning-artifacts/architecture.md` — added the weak ActorProxy clarification
  under "Projection Query Adapter Contract".
- `docs/guides/dapr-component-reference.md` — Configuration Store section reworded: explicit that
  Aspire AppHost no longer ships an orphan `configstore.yaml`; `dapr-configstore` health remains
  `Degraded`-by-design; example YAML kept for non-Aspire deployments.
- `docs/guides/deployment-docker-compose.md` — removed the `cp ... configstore.yaml ...` line and
  updated the post-copy Redis-host rewrite note.
