# Post-Epic 22 R22A1: QueryRouter Actor Proxy Fix

Status: done

Context created: 2026-05-19
Story key: `post-epic-22-r22a1-query-router-actor-proxy-fix`
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-19-queryrouter-actor-proxy-nre.md`
Epic: Post-Epic-22 Follow-Up - Public Gateway and Downstream Integration Contracts
Scope: Runtime defect fix for Story 22.2 generic projection-actor query routing, plus misleading AppHost DAPR component cleanup.

## Story

As an EventStore admin and downstream query consumer,
I want `QueryRouter` to invoke generic projection actors through the DAPR weak actor proxy path correctly,
so that Admin UI tenant queries and other `POST /api/v1/queries` projection-adapter calls no longer fail with a runtime `NullReferenceException` while preserving cancellation, routing, and public gateway contracts.

## Acceptance Criteria

1. **QueryRouter no longer casts a strongly typed projection actor proxy into the weak path.**
   - Given `QueryRouter.RouteQueryAsync` routes a query to `IProjectionActor.QueryAsync`
   - When it needs a per-call `CancellationToken`
   - Then it creates a weakly typed `ActorProxy` through `IActorProxyFactory.Create(new ActorId(actorId), actorTypeName, ...)` and invokes `InvokeMethodAsync<QueryEnvelope, QueryResult>(nameof(IProjectionActor.QueryAsync), envelope, cancellationToken)`
   - And the implementation uses `IActorProxyFactory.Create(...)`, not `CreateActorProxy<TActorInterface>(...)`, for this weak/JSON invocation path
   - And it does not create `CreateActorProxy<IProjectionActor>(...)` and then cast that proxy back to `ActorProxy`
   - And the obsolete `InvokeProjectionActorAsync(IProjectionActor, ...)` helper is deleted or made unreachable.
   - And the test suite contains at least one assertion that would fail against the old typed-proxy-plus-cast implementation.

2. **The live `list-tenants` regression path is pinned by Tier 1 coverage.**
   - Given a `SubmitQuery` equivalent to the Admin UI Tenants page request (`Tenant="system"`, `Domain="tenants"`, `AggregateId="index"`, `QueryType="list-tenants"`, `EntityId="index"`, `ProjectionType="tenants"`, `ProjectionActorType="TenantsProjectionActor"`)
   - When `QueryRouter.RouteQueryAsync` runs
   - Then it creates the weak actor proxy for `ActorId="tenants:system:index"` and actor type `"TenantsProjectionActor"`
   - And it invokes method name `QueryAsync` with a `QueryEnvelope` that preserves tenant, domain, aggregate, query type, payload, correlation ID, user ID, and entity ID
   - And a returned successful `QueryResult` produces a successful `QueryRouterResult` with the JSON payload intact.

3. **Cancellation remains a request-scope contract, not a swallowed adapter failure.**
   - Given the caller supplies a pre-cancelled token
   - When `RouteQueryAsync` is called
   - Then `OperationCanceledException` is thrown before any actor proxy is created.
   - Given the weak actor invocation throws `OperationCanceledException`
   - When `RouteQueryAsync` observes it
   - Then the same cancellation category propagates and is not converted to `QueryAdapterFailureReason.ActorException`.
   - And a focused test captures the exact `CancellationToken` passed into the weak actor invocation path.

4. **Existing adapter-edge failure mapping is preserved.**
   - Given weak actor invocation returns `null`
   - Then the result is `actor-response-mismatch`.
   - Given weak actor invocation returns success without payload bytes
   - Then the result is `missing-payload`.
   - Given weak actor invocation returns invalid JSON payload bytes
   - Then the result is `serialization-failure`.
   - Given weak actor invocation throws a non-cancellation exception
   - Then the result is `actor-exception`, except existing actor-not-found message patterns still map to `NotFound=true`.
   - And no path logs query payload bytes, projection payload bytes, protected data, DAPR addresses, or stack traces into client-facing responses.

5. **Existing public routing contracts do not change.**
   - Given Story 22.2 and `docs/reference/query-api.md` define the 3-tier actor ID model
   - When this fix lands
   - Then entity-scoped, payload-checksum, and tenant-wide actor ID derivation remain unchanged
   - And `projectionType` still selects the first actor ID segment when supplied
   - And `projectionActorType` remains only a DAPR actor type selector, not an authorization bypass or tenant selector
   - And `SubmitQueryRequestValidator` constraints for `ProjectionActorType` remain intact.

6. **The orphan DAPR config store component file is removed without changing runtime health semantics.**
   - Given `src/Hexalith.EventStore.AppHost/DaprComponents/configstore.yaml` is declared but not wired by `HexalithEventStoreExtensions.AddHexalithEventStore`
   - When this story is implemented
   - Then the orphan YAML is deleted
   - And `statestore` and `pubsub` sidecar references remain unchanged
   - And `dapr-configstore` health check behavior remains `Degraded` by design, not `Unhealthy`
   - And no production `GetConfiguration` fallback behavior is changed.

7. **Architecture and evidence are updated before review.**
   - Given the implementation changes the DAPR actor invocation strategy
   - When the story is moved to review
   - Then `_bmad-output/planning-artifacts/architecture.md` includes a short clarification that QueryRouter must use the weak `ActorProxy` creation path when it needs weak/JSON invocation with per-call cancellation
   - And the Dev Agent Record lists focused unit tests, build results, any known pre-existing failures, and Aspire smoke evidence
   - And Aspire smoke evidence proves the real Tenants workflow, not only a friendly unit test: Admin UI Tenants loads successfully or an equivalent authenticated `POST /api/v1/queries` with `QueryType=list-tenants` returns a successful query envelope through `eventstore-admin` rather than bypassing the admin service with a direct EventStore-only call
   - And the evidence explicitly records that EventStore logs no longer emit `EventId 1202` / DAPR `NullReferenceException` for `ActorId=tenants:system:index`, `QueryType=list-tenants`.
   - And if the query succeeds with zero tenants, the Dev Agent Record states the observed Admin UI empty-state behavior so success is not mistaken for a still-broken screen.

## Tasks / Subtasks

- [x] **ST0 - Reconfirm baseline and exact failure shape.** (AC: 1, 2, 7)
  - [x] Read this story, the source sprint-change proposal, Story 22.2, Story 22.4, `docs/reference/query-api.md`, `QueryRouter.cs`, and `QueryRouterTests.cs` before code edits.
  - [x] Confirm the current failing implementation creates `CreateActorProxy<IProjectionActor>(...)` and calls `ActorProxy.InvokeMethodAsync<...>` after a cast.
  - [x] Confirm `DaprTenantQueryService.ListTenantsAsync` sends the live query path through EventStore with domain `tenants`, aggregate `index`, query type `list-tenants`, and projection actor type `TenantsProjectionActor`.
  - [x] Record in the Dev Agent Record whether the live NRE was reproduced locally or accepted from the approved MCP evidence in the source proposal.

- [x] **ST1 - Fix QueryRouter weak actor invocation.** (AC: 1, 3, 4, 5)
  - [x] In `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`, replace the typed proxy creation plus cast helper with the DAPR weak proxy creation path: `ActorProxy proxy = actorProxyFactory.Create(new ActorId(actorId), actorTypeName, options: null or an explicit local options value)`.
  - [x] Invoke `proxy.InvokeMethodAsync<QueryEnvelope, QueryResult>(nameof(IProjectionActor.QueryAsync), envelope, cancellationToken)` and await with `ConfigureAwait(false)`.
  - [x] Preserve all existing result handling: null result, unsuccessful result, missing payload, malformed JSON payload, actor-not-found patterns, cancellation propagation, and `actor-exception` fallback.
  - [x] Do not change `QueryActorIdHelper`, `SubmitQuery`, `SubmitQueryRequest`, `SubmitQueryResponse`, `QueryEnvelope`, `QueryResult`, or public query routing semantics unless a compile break proves it is unavoidable.
  - [x] Remove the private `InvokeProjectionActorAsync` helper after all tests are migrated away from the typed proxy path.

- [x] **ST2 - Harden QueryRouter unit tests around the weak path.** (AC: 2, 3, 4, 5)
  - [x] Update existing `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs` tests that currently substitute `CreateActorProxy<IProjectionActor>` so they exercise the weak actor proxy path instead.
  - [x] Add a focused `list-tenants` regression test that verifies `ActorId="tenants:system:index"` and actor type `"TenantsProjectionActor"`.
  - [x] Add or update a successful-result test proving the weak invocation result payload reaches `QueryRouterResult.Payload`.
  - [x] Add a guard assertion that `CreateActorProxy<IProjectionActor>` is not called by the fixed `QueryRouter`; this must fail against the old implementation.
  - [x] Capture and assert the method name, `QueryEnvelope`, and `CancellationToken` passed to the weak invocation path.
  - [x] Add or update cancellation tests for both pre-cancelled token and invocation-thrown `OperationCanceledException`.
  - [x] Add or update actor-failure tests for null result, missing payload, invalid JSON, actor-not-found patterns, generic actor exception, wrong actor type/name routing, wrong actor ID derivation, lost cancellation, JSON serializer mismatch, and swallowed actor-not-found.
  - [x] If `ActorProxy.InvokeMethodAsync` cannot be substituted directly with NSubstitute, do not weaken the tests. Add the smallest internal test seam necessary, such as an injectable `IProjectionActorInvoker` owned by `Server/Queries`, register it in `ServiceCollectionExtensions`, and keep the default implementation as a thin wrapper over `IActorProxyFactory.Create(...).InvokeMethodAsync(...)`.
  - [x] If an internal invoker seam is added, test the default invoker separately enough to prove it calls `IActorProxyFactory.Create(...)`, not `CreateActorProxy<IProjectionActor>(...)`.
  - [x] If an internal invoker seam is added, do not count tests that mock only `IProjectionActorInvoker` as weak-DAPR-path proof; the default invoker itself must carry the regression-sensitive coverage.

- [x] **ST3 - Remove the misleading AppHost component file.** (AC: 6)
  - [x] Delete `src/Hexalith.EventStore.AppHost/DaprComponents/configstore.yaml`.
  - [x] Verify `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` still wires only `statestore` and `pubsub` into the EventStore sidecar and `statestore` into Admin.Server.
  - [x] Grep for explicit references to the deleted file path and update only references that claim the file is active. Do not change the intentional `dapr-configstore` health check registration.
  - [x] Grep docs, story artifacts, and code comments for `configstore.yaml` / `dapr-configstore`; preserve historical references, but correct any wording that implies the orphan YAML is currently wired by AppHost.
  - [x] Preserve `DaprRateLimitConfigSync` and `DomainServiceResolver` graceful fallback behavior for `GetConfiguration`; those call sites are out of scope.

- [x] **ST4 - Add the architecture clarification.** (AC: 1, 5, 7)
  - [x] Update `_bmad-output/planning-artifacts/architecture.md` in the query/projection actor section.
  - [x] State that `QueryRouter` uses the weak `ActorProxy` path because `IProjectionActor.QueryAsync(QueryEnvelope)` does not accept a `CancellationToken`, while the HTTP request scope still needs cancellation to reach the DAPR invocation.
  - [x] State that strongly typed proxies from `CreateActorProxy<IProjectionActor>` remain valid for direct interface calls, but must not be cast back to `ActorProxy` for weak/JSON invocation.

- [x] **ST5 - Validate locally and record evidence.** (AC: 2, 3, 4, 6, 7)
  - [x] Run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "QueryRouter|SubmitQuery"` first.
  - [x] Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests` only if any Contracts query types change; this story should not need that. (Skipped: Contracts surface unchanged.)
  - [x] Run `dotnet build Hexalith.EventStore.slnx --configuration Release` unless blocked by a known pre-existing issue. Record exact outcome.
  - [x] Review smoke update: fresh `EnableKeycloak=false aspire run --detach --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json` rebuilt the apphost; `GET http://localhost:8090/api/v1/admin/tenants` returned `200 OK` with `[]`; fresh EventStore log search found 0 `EventId 1202` / `ActorProxy.InvokeMethodAsync` / `ActorId=tenants:system:index.*NullReferenceException` matches.
  - [x] Run Aspire smoke with the repo's current dev instructions, preferably `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`, and verify the real Admin UI Tenants workflow or equivalent authenticated query returns through `eventstore-admin` without `EventId 1202` / DAPR `NullReferenceException` for `list-tenants`; a direct EventStore-only request may be useful debug evidence but does not satisfy this gate by itself. (Completed during review; see Review smoke update and evidence file.)
  - [x] Record whether the successful Tenants path rendered tenant rows or an empty state; if empty, confirm the page is no longer stuck on spinner/error. (Observed successful empty tenant list `[]`, not spinner/error.)
  - [x] Store smoke notes or screenshots/log excerpts under `_bmad-output/test-artifacts/post-epic-22-r22a1-query-router-actor-proxy-fix/`.
  - [x] Update Dev Agent Record, File List, Verification Status, and Change Log before moving to review.

### Review Findings

- [x] [Review][Decision] Missing required Aspire Tenants smoke evidence - resolved during review by fresh Aspire rebuild/restart and authenticated `eventstore-admin` Tenants smoke (`200 OK`, `[]`, no fresh QueryRouter `EventId 1202` / DAPR weak-proxy NRE for `ActorId=tenants:system:index`).
- [x] [Review][Patch] Regression guard never executes the production routing path [`tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs:493`] - resolved by routing through the public `QueryRouter(IActorProxyFactory, ...)` constructor and asserting the weak `Create(...)` call plus no typed `CreateActorProxy<IProjectionActor>(...)`.
- [x] [Review][Patch] `list-tenants` Tier 1 pin omits payload preservation and JSON-intact assertions [`tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs:475`] - resolved with request payload preservation assertions and empty JSON payload verification.
- [x] [Review][Patch] Cancellation can be reclassified as actor-not-found when the cancellation exception text matches a not-found marker [`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:112`] - resolved by handling `OperationCanceledException` before generic not-found classification and adding a regression test.
- [x] [Review][Patch] New configuration-store documentation links point to nonexistent targets [`docs/guides/dapr-component-reference.md:741`] - resolved by retargeting the links to existing implementation-artifact documents.

## Dev Notes

### Runtime Defect Summary

The approved sprint-change proposal captured a live Aspire failure on 2026-05-19: every `POST /api/v1/queries` call for `QueryType=list-tenants` returned HTTP 500. The representative EventStore console log was `EventId 1202` from `Hexalith.EventStore.Server.Queries.QueryRouter`, with `ActorId=tenants:system:index`, `QueryType=list-tenants`, and a `NullReferenceException` inside `Dapr.Actors.Client.ActorProxy.InvokeMethodAsync<TRequest,TResponse>`.

The defect source is the mixed proxy style in `QueryRouter.cs`: the code creates a strongly typed `IProjectionActor` proxy through `CreateActorProxy<IProjectionActor>(...)`, then attempts to use the weak/JSON `ActorProxy.InvokeMethodAsync<TRequest,TResponse>(...)` path after a cast. The typed dispatch proxy is suitable for interface remoting, but the weak path state is not initialized for this usage.

### Correct DAPR Actor Client Pattern

Use the DAPR weak client path directly for this story. Current DAPR docs describe two styles:

- Strongly typed clients use `CreateActorProxy<TActorInterface>` and call interface methods directly.
- Weakly typed clients use `ActorProxy`, pass method names explicitly, and serialize request/response messages with `System.Text.Json`.

For dependency-injected code, use `IActorProxyFactory.Create(ActorId, string, ActorProxyOptions?)` to create the weak proxy. The local Dapr.Actors 1.17.9 XML docs also describe `IActorProxyFactory.Create(Dapr.Actors.ActorId,System.String,Dapr.Actors.Client.ActorProxyOptions)` as creating an Actor Proxy for calls without Remoting. Do not use the static `ActorProxy.Create(...)` unless dependency injection blocks the implementation; it is harder to test and the official docs prefer `IActorProxyFactory` inside ASP.NET Core projects.

Project package source of truth is `Directory.Packages.props`: Dapr packages are currently `1.17.9`, Aspire packages are currently `13.3.3` except configured preview integrations, xUnit v3 is `3.2.2`, Shouldly is `4.3.0`, and NSubstitute is `5.3.0`.

External reference: DAPR official docs, "The IActorProxyFactory interface", last modified 2026-05-15, https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-actors/dotnet-actors-client/

### Files Being Modified

Production update:

- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
  - Current state: derives projection actor ID from query routing inputs, builds `QueryEnvelope`, creates a typed `IProjectionActor` proxy, invokes the actor, classifies `QueryResult`, and logs sanitized routing/failure events.
  - Story change: create a weak `ActorProxy` through `IActorProxyFactory.Create(...)` and invoke `QueryAsync` by method name so the DAPR weak/JSON path is initialized and receives the request cancellation token.
  - Preserve: actor ID derivation, actor type fallback/override, `QueryEnvelope` fields, no-payload logging policy, `OperationCanceledException` propagation, actor-not-found mapping, adapter-edge failure categories, and `QueryRouterResult` shape.

Test update:

- `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`
  - Current state: broad routing and failure coverage, but mocks `CreateActorProxy<IProjectionActor>` and calls `actor.QueryAsync(...)`, so it does not exercise the runtime weak-path cast that failed.
  - Story change: make the tests prove weak proxy creation/invocation or a minimal invoker seam if direct `ActorProxy` substitution is not practical.
  - Preserve: existing golden actor ID tests for default, entity, list, payload-checksum, projection type, actor type override, null/missing/malformed payload, actor-not-found, and cancellation behavior.

AppHost cleanup:

- `src/Hexalith.EventStore.AppHost/DaprComponents/configstore.yaml`
  - Current state: declares a Redis DAPR configuration component named `configstore`, scoped to `eventstore`, but `HexalithEventStoreExtensions.cs` does not reference it when creating sidecars.
  - Story change: delete the file to avoid misleading operators.
  - Preserve: `dapr-configstore` health check remains registered as `Degraded`; production `GetConfiguration` fallbacks remain unchanged.

Architecture note:

- `_bmad-output/planning-artifacts/architecture.md`
  - Add one short paragraph about weak actor proxy creation for QueryRouter's per-call cancellation requirement.

### Project Structure Notes

- Keep all query routing runtime changes under `src/Hexalith.EventStore.Server/Queries/`.
- Do not move public query contract types in `src/Hexalith.EventStore.Contracts/Queries/`; Story 22.2 already established that public package boundary.
- Do not add a new public API surface unless direct `ActorProxy` unit testing is impossible. If a seam is needed, keep it internal to Server and document why in the Dev Agent Record.
- Do not touch Admin UI components for this story. The user-visible symptom is fixed by restoring the EventStore query path.
- Do not wire a real config store in AppHost. Deleting the orphan YAML is cleanup; changing config-store health semantics is out of scope.

### Previous Story Intelligence

- Story 22.2 moved `QueryEnvelope`, `QueryResult`, `IProjectionActor`, and `QueryAdapterFailureReason` into `Hexalith.EventStore.Contracts.Queries`, established the generic projection actor model, and added routing tests. This story must not reopen that package ownership decision.
- Story 22.2 review already fixed `ProjectionActorType` validation, actor ID documentation, adapter-edge handler mapping, and cancellation propagation expectations. Preserve those decisions.
- Story 22.4 owns the public query policy/error taxonomy. This story may preserve existing adapter-edge categories but must not invent a new ProblemDetails taxonomy.
- Post-epic-3 R3A7 previously documented that config-store traces can be graceful-fallback noise and that `dapr-configstore` health is intentionally `Degraded`, not `Unhealthy`. Do not "fix" the health check as part of deleting the orphan YAML.

### Testing Standards

- Unit tests use xUnit v3, Shouldly, and NSubstitute.
- Run focused test projects individually per repository guidance.
- Prefer `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "QueryRouter|SubmitQuery"` before broad build/test runs.
- At least one QueryRouter test must be regression-sensitive: it should fail against the old typed proxy cast path, not merely pass against a cooperative fake actor.
- Cancellation-token coverage must prove the same token reaches the weak invocation seam.
- Test coverage must explicitly protect the failure classes that would recreate the production symptom: wrong actor type, wrong actor ID, lost cancellation, JSON serializer mismatch, actor-not-found translation, and generic actor exception fallback.
- `Hexalith.EventStore.Server.Tests` has known pre-existing failures/warnings in some broad runs; classify anything outside the touched query slice instead of assuming this story caused it.
- Aspire smoke requires the repo's DAPR/Aspire prerequisites. In local/dev mode, follow AGENTS.md: start Docker/DAPR placement/scheduler as needed and use `EnableKeycloak=false` if Keycloak should be skipped.

### Party-Mode Review Hardening

Party-mode review on 2026-05-19 reached consensus that the story is ready for implementation, with four non-negotiable hardening points:

- Winston: keep this as a narrow runtime wiring defect; preserve actor type constants, cancellation, adapter-edge ownership, and AppHost topology.
- Amelia: approve only after weak proxy invocation, cancellation, orphan YAML deletion, architecture note, and Aspire Tenants evidence are proven.
- Murat: unit tests must fail on the old implementation and prove weak invocation plus cancellation; unit-only proof is insufficient for this runtime bug.
- John: smoke evidence must prove the user-facing Tenants workflow, and the Dev Agent Record must distinguish a valid empty tenant list from a still-broken screen.

### Advanced Elicitation Hardening

Advanced elicitation on 2026-05-19 added five extra guardrails:

- Pre-mortem: a test seam alone is insufficient; if `IProjectionActorInvoker` is introduced, its default DAPR implementation must be tested directly.
- Red team: deleting `configstore.yaml` must not leave active-topology docs or comments claiming the orphan YAML is wired.
- Failure mode analysis: tests must cover wrong actor type, wrong actor ID, lost cancellation, JSON serializer mismatch, actor-not-found translation, and generic exception fallback.
- First principles: the job is not "unit tests pass"; the job is the Tenants workflow works again through `eventstore-admin`.
- Self-consistency: every implementation and story reference must say `IActorProxyFactory.Create(...)` for weak invocation, not `CreateActorProxy<T>`.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-19-queryrouter-actor-proxy-nre.md`] - approved issue summary, evidence, and implementation handoff.
- [Source: `_bmad-output/planning-artifacts/epics.md#Story-22.2`] - projection adapter contract and generic query actor model.
- [Source: `_bmad-output/implementation-artifacts/22-2-projection-adapter-contract-and-generic-query-actor-model.md`] - previous implementation decisions and review findings.
- [Source: `_bmad-output/implementation-artifacts/22-4-query-behavior-policy-and-error-taxonomy.md`] - query taxonomy ownership and adapter handoff guardrails.
- [Source: `docs/reference/query-api.md#Projection-Query-Actor-Contract`] - public query actor contract and actor ID routing rules.
- [Source: `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`] - primary production file to update.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`] - primary test file to update.
- [Source: `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`] - live Admin UI Tenants query caller.
- [Source: `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`] - AppHost DAPR component wiring.
- [Source: `src/Hexalith.EventStore/HealthChecks/HealthCheckBuilderExtensions.cs`] - `dapr-configstore` degraded-by-design health registration.
- [Source: `Directory.Packages.props`] - package version source of truth.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.7 (1M context).

### Debug Log References

- Baseline NRE accepted from approved MCP evidence captured in
  `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-19-queryrouter-actor-proxy-nre.md`
  (EventId 1202 + Dapr.Actors.Client.ActorProxy NRE on `ActorId=tenants:system:index`,
  `QueryType=list-tenants`). The live NRE was not re-reproduced locally in this session because the
  Aspire/DAPR smoke prerequisites (Docker desktop, `dapr init`, Aspire CLI) are operator-only here.
- During the test rewrite, NSubstitute initially refused to proxy the internal `IProjectionActorInvoker`
  interface because the Server assembly was not visible to Castle DynamicProxy. Adding
  `InternalsVisibleTo("DynamicProxyGenAssembly2")` to `src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj`
  resolved the `Castle.DynamicProxy.DefaultProxyBuilder.AssertValidTypeForTarget` failure.
- NSubstitute also special-cases `OperationCanceledException` returns for `Task<T>`-returning methods
  by converting the configured exception into `Task.FromCanceled<T>(token)`, which throws
  `TaskCanceledException("A task was canceled.")` on await instead of the configured
  `OperationCanceledException("abandoned request")`. The cancellation-propagation behavioral test was
  kept (router re-throws `OperationCanceledException`) but the cosmetic message-content assertion was
  dropped because it asserts NSubstitute internals, not router behavior.

### Completion Notes List

- ST0: confirmed bug shape by reading `QueryRouter.cs` (typed `CreateActorProxy<IProjectionActor>` +
  `if (proxy is ActorProxy actorProxy) ...` weak-call cast) and the live caller
  `DaprTenantQueryService.ListTenantsAsync` (domain `tenants`, aggregate `index`, query type
  `list-tenants`, projection actor type `TenantsProjectionActor`).
- ST1: replaced the typed-proxy-plus-cast with the DAPR weak path via a new internal
  `IProjectionActorInvoker` seam whose default implementation
  (`DefaultProjectionActorInvoker`) calls
  `IActorProxyFactory.Create(new ActorId(actorId), actorTypeName).InvokeMethodAsync<QueryEnvelope, QueryResult>(nameof(IProjectionActor.QueryAsync), envelope, cancellationToken)`.
  The obsolete private `InvokeProjectionActorAsync` helper was deleted. The public DI surface is
  preserved: `services.TryAddScoped<IQueryRouter, QueryRouter>()` still binds to the public
  `QueryRouter(IActorProxyFactory, ILogger<QueryRouter>)` constructor, which internally wraps the
  factory in `DefaultProjectionActorInvoker`. No additional DI registration was needed because the
  seam is intentionally internal to Server and constructed by the public ctor — this satisfies the
  "smallest internal test seam owned by Server/Queries" requirement without forcing
  `IProjectionActorInvoker` into the public API.
- ST2: rewrote `QueryRouterTests.cs` to substitute `IProjectionActorInvoker`; added a regression-
  sensitive guard (`QueryRouter_ConstructedFromIActorProxyFactory_DoesNotCreateTypedDispatchProxy`)
  that fails against the old implementation; added the `list-tenants` regression pin
  (`RouteQueryAsync_ListTenants_UsesTenantsProjectionActorAtSystemIndex`); added a
  cancellation-token capture test (`RouteQueryAsync_PassesCallerCancellationTokenIntoInvoker`);
  added an explicit NRE-from-invocation classification test
  (`RouteQueryAsync_NullReferenceFromActorInvocation_ReturnsActorExceptionCategory`) so the original
  NRE class is now fail-closed even if it ever recurs from another DAPR path. Added a separate
  `DefaultProjectionActorInvokerTests` test class proving the default invoker calls
  `IActorProxyFactory.Create(...)` and never `CreateActorProxy<IProjectionActor>(...)` — these tests
  satisfy the "do not count tests that mock only IProjectionActorInvoker as weak-DAPR-path proof"
  pre-mortem hardening point.
- ST3: deleted `src/Hexalith.EventStore.AppHost/DaprComponents/configstore.yaml`; verified
  `HexalithEventStoreExtensions.AddHexalithEventStore` continues to wire only `statestore` and
  `pubsub`; preserved the intentional `dapr-configstore` `Degraded`-by-design health check; reworded
  `docs/guides/dapr-component-reference.md` so the Configuration Store section is presented as an
  optional example for non-Aspire deployments rather than a "source" file shipped in AppHost;
  removed the obsolete `cp configstore.yaml` line from `docs/guides/deployment-docker-compose.md`
  and adjusted the redis-host rewrite note to mention `configstore.yaml` only when an operator has
  chosen to add one. Historical sprint-change proposals, sprint-status comments, and prior
  implementation records that mention the orphan file path were intentionally preserved per ST3.
- ST4: added a "QueryRouter weak ActorProxy invocation (post-Epic-22 R22A1)" paragraph in
  `_bmad-output/planning-artifacts/architecture.md` under "Projection Query Adapter Contract"
  explaining the weak invocation requirement and the typed-proxy-cast pitfall.
- ST5: focused Server.Tests slice `QueryRouter|SubmitQuery|DefaultProjectionActorInvoker` now reports
  **91/91 passed, 0 failed, 0 skipped** (Release) after the review regression test additions.
  Full Release solution build reports **0 warnings / 0 errors**. Fresh `EnableKeycloak=false`
  Aspire smoke through `eventstore-admin` succeeded for `GET /api/v1/admin/tenants` with
  **200 OK** and `[]`; the fresh EventStore log contains the `list-tenants` routing entry and
  **0** fresh QueryRouter `EventId 1202` / DAPR weak-proxy NRE entries for
  `ActorId=tenants:system:index`.
- Review patch pass: applied all four code-review patch findings: production-path regression guard,
  `list-tenants` payload/JSON assertions, cancellation-before-not-found classification, and fixed
  configuration-store documentation links.

### File List

Production:

- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` (modified)
- `src/Hexalith.EventStore.Server/Queries/IProjectionActorInvoker.cs` (added)
- `src/Hexalith.EventStore.Server/Queries/DefaultProjectionActorInvoker.cs` (added)
- `src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj` (modified — added
  `InternalsVisibleTo DynamicProxyGenAssembly2`)
- `src/Hexalith.EventStore.AppHost/DaprComponents/configstore.yaml` (deleted)

Tests:

- `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs` (rewritten)
- `tests/Hexalith.EventStore.Server.Tests/Queries/DefaultProjectionActorInvokerTests.cs` (added)

Documentation:

- `_bmad-output/planning-artifacts/architecture.md` (modified — added weak ActorProxy clarification)
- `docs/guides/dapr-component-reference.md` (modified — Configuration Store section)
- `docs/guides/deployment-docker-compose.md` (modified — removed orphan copy step, updated
  redis-host rewrite note)

Evidence:

- `_bmad-output/test-artifacts/post-epic-22-r22a1-query-router-actor-proxy-fix/evidence.md` (added)

## Verification Status

- Focused Server.Tests slice green: 91/91 passed for
  `FullyQualifiedName~QueryRouter|FullyQualifiedName~SubmitQuery|FullyQualifiedName~DefaultProjectionActorInvoker`
  (Release).
- Full Release solution build green: 0 warnings / 0 errors across all projects in
  `Hexalith.EventStore.slnx`.
- Aspire smoke green through `eventstore-admin`: authenticated `GET /api/v1/admin/tenants`
  returned 200 OK with `[]`; fresh EventStore logs show the `list-tenants` route and 0 fresh
  QueryRouter `EventId 1202` / DAPR weak-proxy NRE entries for `ActorId=tenants:system:index`.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-19 | 1.1 | Code-review patch pass complete. Applied all four review patches, completed fresh Aspire Tenants smoke through `eventstore-admin` (200 OK, `[]`, no fresh QueryRouter weak-proxy NRE), focused Server.Tests 91/91, full Release build 0/0. Status -> done. | Codex |
| 2026-05-19 | 1.0 | Implementation complete. Refactored QueryRouter to use a new internal `IProjectionActorInvoker` seam whose default invoker creates a weak `ActorProxy` via `IActorProxyFactory.Create(...)`; removed obsolete typed-proxy-plus-cast helper; rewrote `QueryRouterTests.cs` and added `DefaultProjectionActorInvokerTests.cs` with regression-sensitive guards, `list-tenants` pin, cancellation-token capture, and NRE fail-closed classification; deleted orphan `AppHost/DaprComponents/configstore.yaml`; updated `architecture.md`, `dapr-component-reference.md`, and `deployment-docker-compose.md`; focused Server.Tests 90/90, full Release build 0/0. Aspire smoke operator-deferred. Status -> review. | Claude Opus 4.7 |
| 2026-05-19 | 0.3 | Applied advanced elicitation hardening for default invoker proof, eventstore-admin smoke, configstore reference cleanup, failure-class coverage, and explicit `IActorProxyFactory.Create(...)` wording. | Codex |
| 2026-05-19 | 0.2 | Applied party-mode review hardening: old-implementation-sensitive weak-proxy tests, cancellation-token capture, real Tenants smoke proof, and empty-state evidence. | Codex |
| 2026-05-19 | 0.1 | Created ready-for-dev post-Epic-22 story for QueryRouter weak actor proxy NRE fix and orphan configstore cleanup. | Codex |

