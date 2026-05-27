# Post-Epic 22 ES-6: Projection Actor Not-Found Typed Check

Status: done

Context created: 2026-05-27
Story key: `post-epic-22-es6-projection-actor-not-found-typed-check`
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md` (finding ES-6)
Epic: Post-Epic-22 EventStore<->Parties Review Residuals
Scope: Moderate (Developer). Replace locale-sensitive projection actor not-found classification in `QueryRouter` with bounded typed DAPR exception/status signals while retaining string matching only as a last-resort compatibility fallback.

## Story

As an EventStore platform maintainer supporting downstream projection actors,
I want `QueryRouter` to detect missing projection actors from typed DAPR failure signals,
so that actor registration/address failures still produce the intended query 404 without depending on English exception text.

## Background & Verified Residual

ES-6 is confirmed in current code. `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` catches projection actor invocation failures and calls `IsProjectionActorNotFound`, but that helper only checks `exception.Message` and `exception.InnerException?.Message` for English phrases such as `actor type not registered`, `did not find address for actor`, and `actor not found`.

This is brittle for three reasons:

- DAPR runtime and SDK messages can change by version and can be localized or wrapped.
- DAPR already exposes machine-readable error categories. The local Dapr.Client `1.17.9` XML docs show `Dapr.DaprApiException.ErrorCode`, and official DAPR error-code docs list actor errors including `ERR_ACTOR_INSTANCE_MISSING`, `ERR_ACTOR_RUNTIME_NOT_FOUND`, and `ERR_ACTOR_NO_ADDRESS`.
- DAPR actor APIs also surface HTTP/gRPC status information. Official Actors API docs show actor state endpoints returning `400 Actor not found`, and actor method invocation returns DAPR/upstream status codes. Official .NET actor-client docs continue to distinguish strongly typed `CreateActorProxy<T>` from weak `IActorProxyFactory.Create(...).InvokeMethodAsync(...)`; ES-6 must not alter that R22A1 weak-path decision.

Current production boundary:

- `QueryRouter.RouteQueryAsync` derives the actor ID and actor type, builds `QueryEnvelope`, then calls `IProjectionActorInvoker.InvokeAsync`.
- `DefaultProjectionActorInvoker` uses the weak DAPR actor proxy path: `IActorProxyFactory.Create(new ActorId(actorId), actorTypeName)` followed by `ActorProxy.InvokeMethodAsync<QueryEnvelope, QueryResult>(nameof(IProjectionActor.QueryAsync), envelope, cancellationToken)`.
- Not-found classification currently maps to `new QueryRouterResult(Success: false, Payload: null, NotFound: true)`, which `SubmitQueryHandler` turns into `QueryNotFoundException`, then the API emits RFC 7807 `404`.
- Generic actor invocation failures remain `QueryAdapterFailureReason.ActorException` and should continue to become server-side query execution failure, not a false 404.

This story is only about classification. Do not change actor ID derivation, weak invocation, query contract DTOs, public ProblemDetails taxonomy, authorization, projection actor registration, or Parties code.

## Classification Contract

`QueryRouter.IsProjectionActorNotFound(Exception exception)` must answer one narrow question: did a DAPR actor invocation fail because the target projection actor type/instance/address was missing? It must not turn general DAPR availability, authorization, routing, serialization, timeout, cancellation, or sidecar failures into a query 404.

Preferred evidence order:

1. Walk the bounded exception chain first, including known wrapper exceptions such as `ActorMethodInvocationException`, direct DAPR exceptions, `InnerException`, and `AggregateException.InnerExceptions`.
2. Treat `DaprApiException.ErrorCode` as the primary signal when present.
3. Treat gRPC/HTTP status as a secondary signal only when the status is safely scoped to DAPR actor invocation and does not contradict a more specific DAPR error code.
4. Use the legacy English marker list only as the final compatibility fallback for older SDK/runtime shapes that expose no typed metadata.

Classifier precedence:

1. Cancellation wins and propagates. `OperationCanceledException` and `TaskCanceledException` must never be converted to not-found or actor failure.
2. DAPR actor error code wins. If a supported actor-missing code is found, classify as not found; if a contradictory non-not-found DAPR error code is found, do not let a generic status code override it.
3. Actor-scoped status may classify only when there is no contradictory DAPR error code and the status is observed in the actor invocation failure path.
4. Legacy marker fallback runs last and is limited to exact known phrases from current historical DAPR shapes.
5. Everything else remains `QueryAdapterFailureReason.ActorException` or existing cancellation propagation.

Decision table:

| Exception shape | Expected classification |
| --- | --- |
| `DaprApiException.ErrorCode == "ERR_ACTOR_INSTANCE_MISSING"` anywhere in the bounded actor invocation exception chain | `NotFound == true` |
| `DaprApiException.ErrorCode == "ERR_ACTOR_RUNTIME_NOT_FOUND"` anywhere in the bounded actor invocation exception chain | `NotFound == true` |
| `DaprApiException.ErrorCode == "ERR_ACTOR_NO_ADDRESS"` anywhere in the bounded actor invocation exception chain | `NotFound == true` |
| Actor-scoped `RpcException.StatusCode == StatusCode.NotFound` with no contradictory DAPR error code | `NotFound == true` |
| Actor-scoped `HttpRequestException.StatusCode == HttpStatusCode.NotFound` with no contradictory DAPR error code | `NotFound == true` only if the implementation can confidently scope it to actor invocation |
| Localized or changed message text with no typed actor-missing signal and no legacy marker | `NotFound == false` |
| Non-DAPR exception whose message says actor not found but does not match the exact explicit legacy marker list | `NotFound == false` |
| DAPR actor invocation/infrastructure error such as method failure, placement missing, sidecar unavailable, app channel missing, deadline, internal, unavailable, forbidden, or serialization failure | `NotFound == false` |
| `OperationCanceledException` or `TaskCanceledException`, including wrapped/adjacent cases | Propagated cancellation, never classified as not found |
| Existing weak `ActorProxy` invocation path from R22A1 | Preserved |

Implementation may choose a small internal helper for exception-chain walking and classification if that makes the test contract clearer. Keep it private/internal to `QueryRouter` unless a broader reuse point already exists.

## Acceptance Criteria

1. **Typed DAPR actor-missing signals map to query not found.**
   - Given `IProjectionActorInvoker.InvokeAsync` throws a direct or wrapped DAPR actor invocation exception whose bounded exception chain contains a typed actor missing signal
   - When `QueryRouter.RouteQueryAsync` handles the exception
   - Then it returns `QueryRouterResult` with `Success == false`, `Payload == null`, and `NotFound == true`
   - And it logs `ProjectionActorNotFound`
   - And this behavior does not require the exception message to contain any English not-found phrase.

2. **Typed signal precedence is explicit and message matching is last-resort only.**
   - `IsProjectionActorNotFound` first inspects typed data in the exception chain, including `Dapr.DaprApiException.ErrorCode`, `HttpRequestException.StatusCode` when safely attributable to actor invocation, and `Grpc.Core.RpcException.StatusCode`/rich error metadata where available.
   - DAPR actor error codes treated as not-found/address-missing must be limited to actor lookup/registration/address categories such as `ERR_ACTOR_INSTANCE_MISSING`, `ERR_ACTOR_RUNTIME_NOT_FOUND`, and `ERR_ACTOR_NO_ADDRESS`.
   - Do not classify infrastructure-unavailable or transient actor runtime failures such as placement unavailable, sidecar unavailable, app-channel missing, timeout/deadline, cancellation, authorization failure, serialization failure, or generic invocation errors as not found.
   - Do not classify every HTTP/gRPC `NotFound` as projection actor not found. If DAPR exposes actor-specific error metadata, that metadata wins. A plain 404 can also mean app id, method, route, or sidecar routing failure.
   - Preserve the existing string marker list only in a clearly named legacy fallback path, after typed checks, with a comment that it exists for older DAPR/SDK shapes that do not expose machine-readable metadata.
   - The fallback must use the exact known legacy phrases, not broad `Contains("not found")`, and it must run only inside the actor invocation failure classification path.

3. **Non-not-found actor failures remain adapter failures.**
   - Given actor invocation throws `ActorMethodInvocationException`, `DaprApiException`, `RpcException`, `HttpRequestException`, or another exception with a typed code/status that is not an actor missing/address signal
   - When `RouteQueryAsync` handles it
   - Then it returns `ErrorMessage == QueryAdapterFailureReason.ActorException`, `NotFound == false`, and `Payload == null`
   - And `ActorInvocationFailed` logging remains the generic failure path.

4. **Cancellation behavior remains unchanged.**
   - Given the caller token is pre-cancelled, `DefaultProjectionActorInvoker.InvokeAsync` still throws before creating the actor proxy.
   - Given invocation throws `OperationCanceledException` or `TaskCanceledException`, `QueryRouter.RouteQueryAsync` still propagates cancellation and never converts it to `NotFound` or `ActorException`, even if the cancellation message or an adjacent wrapped exception contains a not-found phrase.

5. **R22A1 weak actor proxy contract is preserved.**
   - Do not replace `IProjectionActorInvoker` or `DefaultProjectionActorInvoker` with a strongly typed `CreateActorProxy<IProjectionActor>` path.
   - Do not cast a strongly typed actor proxy back to `ActorProxy`.
   - Keep the existing regression-sensitive tests proving `IActorProxyFactory.Create(...)` is used and `CreateActorProxy<IProjectionActor>(...)` is not called.
   - Include at least one QueryRouter regression test that exercises actor-not-found handling through the existing `IProjectionActorInvoker`/weak-proxy boundary rather than only testing a standalone classification helper.
   - Do not change `QueryActorIdHelper`, `QueryEnvelope`, `QueryResult`, `IProjectionActor`, `SubmitQuery`, `SubmitQueryRequest`, `SubmitQueryResponse`, or query routing semantics.

6. **Focused Tier 1 coverage pins typed classification and fallback boundaries.**
   - Add/update tests in `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`.
   - Cover at least one typed `DaprApiException.ErrorCode` actor missing/address case with a non-English or otherwise non-matching message.
   - Cover at least one typed `RpcException` or `HttpRequestException` status case if the current DAPR SDK/runtime shape makes that practical without brittle reflection.
   - Cover a localized or changed actor-not-found-looking message with no typed status/code and assert that message text alone does not decide classification unless it matches the explicit legacy fallback markers.
   - Cover a non-DAPR exception with not-found-looking text and assert it is not classified as projection actor not found.
   - Cover a non-not-found typed DAPR actor error, such as generic actor invocation failure or actor placement/app-channel failure, and assert `ActorException`, not 404.
   - Cover typed non-404/non-NotFound status cases such as unavailable, deadline, internal, forbidden, or no status when constructible.
   - Keep a legacy string-fallback test for the current `did not find address for actor` shape, but make its name and assertion clear that it is fallback compatibility, not the primary mechanism.
   - Preserve existing cancellation, weak-proxy, list-tenants, null result, missing payload, invalid JSON, and generic exception tests.
   - If public DAPR constructors make precise fixture setup brittle, test the smallest private/internal classification helper directly with controlled exception instances, but still keep at least one `RouteQueryAsync` test that proves the production catch path uses the classifier correctly.

7. **Runtime/API behavior stays stable.**
   - Existing callers still receive 404 ProblemDetails for a genuinely missing projection actor.
   - Existing generic actor failures remain query execution failures and do not leak DAPR exception details to client-facing responses.
   - `ProjectionActorNotFound` logs include only existing envelope/control metadata: correlation id, tenant id, domain, aggregate id, and actor id. Do not log query payload bytes, projection payload bytes, stack traces, DAPR addresses, or protected data in the not-found log.

## Tasks / Subtasks

- [x] **ST0 - Reconfirm current exception and routing surface.** (AC: 1, 2, 5)
  - [x] Re-read `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`, especially the catch ordering and `IsProjectionActorNotFound`.
  - [x] Re-read `src/Hexalith.EventStore.Server/Queries/DefaultProjectionActorInvoker.cs` and confirm the weak `IActorProxyFactory.Create(...)` path remains intact.
  - [x] Re-read `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs` and `DefaultProjectionActorInvokerTests.cs` to preserve R22A1 regression coverage.
  - [x] Inspect local Dapr `1.17.9` XML docs or runtime metadata for `DaprApiException.ErrorCode` and `ActorMethodInvocationException` wrapping shape before coding.

- [x] **ST1 - Implement typed not-found classification.** (AC: 1, 2, 3, 4, 7)
  - [x] Add `using Dapr;`, `using Grpc.Core;`, and `using System.Net;` only if the final implementation needs them.
  - [x] Replace the message-only helper with a bounded exception-chain walker that can inspect known wrappers, nested exceptions, and `AggregateException.InnerExceptions` without infinite recursion.
  - [x] Avoid `Exception.ToString()` and avoid unbounded traversal. The helper should inspect only structured exception metadata and the exact legacy message markers.
  - [x] Add a small, explicit set of DAPR actor not-found/address error codes: `ERR_ACTOR_INSTANCE_MISSING`, `ERR_ACTOR_RUNTIME_NOT_FOUND`, and `ERR_ACTOR_NO_ADDRESS`.
  - [x] Treat these error codes as the preferred classification source when found in `DaprApiException.ErrorCode`.
  - [x] Add status-code checks only when they are safely scoped to DAPR actor invocation. Prefer actor rich error metadata or DAPR error code when present; avoid broad "any HTTP 400 means not found" and "any HTTP/gRPC 404 means projection actor not found" logic.
  - [x] Preserve the existing string markers in a final `ContainsLegacyActorNotFoundMarker` fallback after typed checks.
  - [x] Keep `catch (OperationCanceledException) { throw; }` before any generic not-found catch.
  - [x] Do not change the public `QueryRouterResult` shape or the `SubmitQueryHandler` mapping.

- [x] **ST2 - Add focused regression tests.** (AC: 1, 2, 3, 4, 6)
  - [x] Add a test where the invoker throws `ActorMethodInvocationException` wrapping a `DaprApiException` with `ErrorCode == "ERR_ACTOR_NO_ADDRESS"` and a message that does not contain the old English markers; assert `NotFound == true`.
  - [x] Add a test for `ERR_ACTOR_RUNTIME_NOT_FOUND` or `ERR_ACTOR_INSTANCE_MISSING` if constructible with the public `DaprApiException` constructors.
  - [x] Add a negative test where `ErrorCode == "ERR_ACTOR_INVOKE_METHOD"` or another non-not-found actor code maps to `QueryAdapterFailureReason.ActorException`.
  - [x] Add a negative test for `ERR_ACTOR_NO_PLACEMENT` or sidecar/unavailable status if constructible; assert it is not a 404 because it is infrastructure, not a missing projection.
  - [x] Add negative tests for localized message-only, non-DAPR not-found-looking message, and typed non-not-found status cases.
  - [x] Keep or rename the current generic message test to make it a legacy fallback test.
  - [x] Add at least one test that fails on the current message-only implementation by using a typed actor-missing error code with a message that does not contain any legacy marker.
  - [x] Add at least one test that fails on an overbroad replacement by using localized or changed message text without typed metadata and asserting `ActorException`.
  - [x] Ensure cancellation tests still pass and still prove not-found-looking cancellation messages are propagated, not converted.
  - [x] Assert the production-path weak proxy guard remains green.

- [x] **ST3 - Validate and record evidence.** (AC: all)
  - [x] Run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~QueryRouter"`.
  - [x] Run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~DefaultProjectionActorInvoker"` if any weak-path code or tests are touched.
  - [x] Run `dotnet test tests/Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~SubmitQueryHandler|FullyQualifiedName~QueryNotFoundExceptionHandler"` if public not-found mapping changes or if any `QueryRouterResult` semantics are touched.
  - [x] Run `dotnet build Hexalith.EventStore.slnx --configuration Release` if the focused tests pass and the current Aspire instance does not hold build output locks; otherwise stop/restart Aspire per repository instructions and document the reason.
  - [x] Optional but valuable: with Aspire already running, exercise a dev-mode authenticated `POST /api/v1/queries` request using an unregistered projection actor type and confirm the API still returns 404 ProblemDetails, not 500.
  - [x] Update the Dev Agent Record, File List, Verification Status, and Change Log before moving the story to review.

### Review Findings

- [x] [Review][Patch] Plain HTTP/gRPC 404 status is classified as projection actor missing without actor-specific evidence [`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:157`]
- [x] [Review][Patch] gRPC rich DAPR error metadata is not inspected for actor error codes [`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:147`]

## Dev Notes

### Current State Of Files To Update

`src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`

- Current behavior: routes `SubmitQuery` to a projection actor, invokes `IProjectionActorInvoker`, maps successful `QueryResult` payloads, maps adapter-edge failures, and catches actor invocation failures.
- Current defect: `IsProjectionActorNotFound` is message-only and reads only `exception.Message` and one `InnerException` message. It is vulnerable to DAPR wording/version/localization changes.
- Required change: prefer typed DAPR error codes/statuses and use recursive exception-chain inspection. Keep message matching as a final fallback for older shapes only.
- Must preserve: `OperationCanceledException` propagation, weak actor invocation, actor ID derivation, actor type override, query envelope fields, source-generated logging, and no-payload logging policy.

`tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`

- Current behavior: broad route, adapter failure, cancellation, weak-proxy regression, and list-tenants tests.
- Required change: add typed actor-not-found classification tests and negative tests for non-not-found DAPR actor failures.
- Must preserve: existing R22A1 tests, especially `QueryRouter_ConstructedFromIActorProxyFactory_DoesNotCreateTypedDispatchProxyOnRoute`.

### Files To Read But Avoid Editing Unless Needed

`src/Hexalith.EventStore.Server/Queries/DefaultProjectionActorInvoker.cs`

- Current behavior: creates the weak `ActorProxy` with `IActorProxyFactory.Create(...)` and invokes `QueryAsync` by name with the cancellation token.
- ES-6 should not need to edit this file unless typed exception wrapping is proven to need a narrow adapter change. If edited, preserve all R22A1 weak-path tests.

`tests/Hexalith.EventStore.Server.Tests/Queries/DefaultProjectionActorInvokerTests.cs`

- Current behavior: proves the default invoker uses `Create(...)`, not `CreateActorProxy<IProjectionActor>(...)`, and handles pre-cancelled tokens.
- Only update if `DefaultProjectionActorInvoker` changes.

`src/Hexalith.EventStore.Server/Pipeline/SubmitQueryHandler.cs`

- Current behavior: converts `QueryRouterResult.NotFound` to `QueryNotFoundException`, maps selected adapter failure reasons, and preserves cancellation.
- ES-6 should not change it unless the implementation deliberately changes router result semantics, which is not expected.

`src/Hexalith.EventStore.Contracts/Queries/QueryAdapterFailureReason.cs`

- Current behavior: already defines `ActorNotFoundInfrastructure`, but `QueryRouter` currently uses `NotFound=true` for missing actor infrastructure. Do not introduce a contract change merely to use this constant unless the team deliberately changes the public mapping, which is out of scope for ES-6.

### Implementation Guardrails

- Do not use broad `message.Contains("not found")` as the primary classifier.
- Do not use exception type alone as the classifier. The type must expose actor-missing error code/status evidence, or fall through to the explicit legacy marker fallback.
- Do not use `Exception.ToString()` for classification.
- Do not classify arbitrary `HttpRequestException.StatusCode == BadRequest` as projection actor not found. Bad requests can also mean malformed invocation or serialization problems.
- Do not classify arbitrary HTTP/gRPC 404 as projection actor not found unless the implementation can confidently attribute the status to DAPR actor invocation and no more specific DAPR error metadata contradicts it.
- Do not classify placement/scheduler/sidecar availability failures as 404. They are infrastructure failures and should stay in the generic actor failure path or existing sidecar-unavailable handling.
- Do not log exception messages in `ProjectionActorNotFound`. Existing not-found logging is intentionally sanitized.
- Do not update Parties code. The source proposal explicitly says Parties migrates its two copies after EventStore is fixed first.

### Previous Story Intelligence

- R22A1 fixed the QueryRouter runtime NRE by introducing the internal `IProjectionActorInvoker` seam and default weak `ActorProxy` implementation. ES-6 must preserve this and only harden failure classification.
- ES-1 through ES-5 are result-payload hardening and verification stories. They changed controller parsing, actor pipeline checkpoint payload posture, payload-drop logging, wire compatibility tests, and derived-result test coverage. ES-6 should not touch result-payload paths.
- ES-5 validation ran the full client unit project green (399/399) and kept runtime code unchanged. This reinforces the pattern for ES rows: keep the implementation narrow and evidence-focused.

### Aspire Baseline

Before creating this story, `aspire describe --format Json --non-interactive` found an existing running Aspire application on 2026-05-27:

- Dashboard: `https://localhost:17017/login?t=3e84886ef4a5815b01147d189f20072a`
- Core resources were running and healthy, including `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `sample`, `sample-blazor-ui`, `statestore`, `tenants`, and DAPR sidecars.
- No AppHost code changes are part of this story, so no AppHost restart is required for story creation. Implementation should restart or rebuild resources only if code changes need live validation.

### Latest Technical Information

- DAPR official .NET actor-client docs distinguish strongly typed actor clients (`CreateActorProxy<T>`) from weakly typed clients (`IActorProxyFactory.Create(...)` returning `ActorProxy`) and show weak invocation by method name. ES-6 must preserve the weak path because R22A1 needs per-call cancellation and weak JSON invocation. Source: <https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-actors/dotnet-actors-client/>
- DAPR official error-code docs list actor runtime error codes and state that DAPR returns error codes in HTTP response bodies or gRPC `ErrorInfo` when present. Actor-relevant codes include `ERR_ACTOR_INSTANCE_MISSING`, `ERR_ACTOR_RUNTIME_NOT_FOUND`, and `ERR_ACTOR_NO_ADDRESS`. Source: <https://docs.dapr.io/developing-applications/error-codes/error-codes-reference/>
- DAPR official Actors API docs show actor endpoint status codes and actor state operations returning `400 Actor not found`; method invocation can return DAPR/upstream statuses. Treat status code as useful but less precise than DAPR error code when both are available. Source: <https://docs.dapr.io/reference/api/actors_api/>
- Local package source of truth is `Directory.Packages.props`: DAPR packages are `1.17.9`, .NET target is `net10.0`, xUnit v3 is `3.2.2`, Shouldly is `4.3.0`, and NSubstitute is `5.3.0`.
- Local Dapr.Client `1.17.9` XML docs expose `Dapr.DaprApiException.ErrorCode` and `IsTransient`. Local Dapr.Actors `1.17.9` XML docs show `ActorMethodInvocationException` has message/inner/transient constructors but no dedicated status property, so the implementation likely needs to inspect inner exceptions and DAPR client exceptions.

### Project Context Reference

Apply `_bmad-output/project-context.md`:

- Target .NET SDK `10.0.300` / `net10.0`.
- Treat warnings as build-breaking.
- Use xUnit v3, Shouldly, and NSubstitute in tests.
- Run targeted test projects individually.
- Do not add packages or abstractions unless the existing internal seam truly cannot carry the implementation.
- Never log query payload bytes, projection payload bytes, secrets, stack traces, DAPR addresses, or protected data in client-facing responses or sanitized not-found logs.

## References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-27-eventstore-parties-review-residuals.md#ES-6`] - residual scope: replace locale-sensitive actor-not-found message matching with typed DAPR exception/status checks.
- [Source: `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`] - primary production file and current `IsProjectionActorNotFound` helper.
- [Source: `src/Hexalith.EventStore.Server/Queries/DefaultProjectionActorInvoker.cs`] - R22A1 weak `ActorProxy` invocation path to preserve.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`] - primary query router test suite to extend.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Queries/DefaultProjectionActorInvokerTests.cs`] - weak-path regression guard to preserve.
- [Source: `_bmad-output/implementation-artifacts/post-epic-22-r22a1-query-router-actor-proxy-fix.md`] - previous QueryRouter weak-proxy fix and live Tenants regression context.
- [Source: `_bmad-output/project-context.md`] - repository rules for DAPR, testing, logging, and workflow.
- [External: DAPR .NET actor client docs](https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-actors/dotnet-actors-client/)
- [External: DAPR error codes reference](https://docs.dapr.io/developing-applications/error-codes/error-codes-reference/)
- [External: DAPR Actors API reference](https://docs.dapr.io/reference/api/actors_api/)

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: `aspire ps --non-interactive` and `aspire describe --format Json --non-interactive` confirmed a running AppHost baseline before code edits; core resources were running/healthy.
- 2026-05-27: Initial red run of `dotnet test .\tests\Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~QueryRouter"` was blocked by Debug output DLL locks from the running Aspire app.
- 2026-05-27: Stopped Aspire with `aspire stop --non-interactive` to release build output locks, per story validation note.
- 2026-05-27: Red QueryRouter run failed expected typed/status/cancellation cases before implementation.
- 2026-05-27: Green QueryRouter run passed after typed classifier implementation.
- 2026-05-27: Full `Server.Tests` attempted after focused validation; failed on unrelated baseline/environment items: health/access-control expectation drift and DAPR scheduler not reachable on `localhost:6060`.
- 2026-05-27: Restarted Aspire detached with `EnableKeycloak=false` after validation; resources returned to running/healthy.
- 2026-05-27: Dev-auth live smoke against `POST http://localhost:8080/api/v1/queries` with an unregistered projection actor returned `404 application/problem+json` and `reasonCode=query_projection_missing`.

### Completion Notes List

- Replaced the message-only projection actor not-found helper with a bounded exception-chain classifier.
- Added typed DAPR actor-missing error-code detection for `ERR_ACTOR_INSTANCE_MISSING`, `ERR_ACTOR_RUNTIME_NOT_FOUND`, and `ERR_ACTOR_NO_ADDRESS`.
- Added actor-invocation-scoped gRPC/HTTP `NotFound` status classification while preventing contradictory DAPR error codes from being overridden.
- Preserved the weak `IActorProxyFactory.Create(...)` actor invocation path and the public `QueryRouterResult`/`SubmitQueryHandler` mapping.
- Kept legacy English message markers as an explicitly named final compatibility fallback.
- Added cancellation-chain detection so wrapped cancellation is propagated before any not-found or actor-failure classification.
- Added focused QueryRouter coverage for typed positive cases, non-not-found DAPR errors, placement/infrastructure failures, localized message-only negatives, status positives/negatives, contradictory metadata, legacy fallback, and wrapped cancellation.
- Review patch: plain HTTP/gRPC 404 statuses no longer classify as projection actor missing without actor-specific DAPR metadata.
- Review patch: gRPC rich `ErrorInfo` metadata is inspected for DAPR actor error codes before classification.

### File List

- `_bmad-output/implementation-artifacts/post-epic-22-es6-projection-actor-not-found-typed-check.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
- `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`

## Verification Status

- PASS: `dotnet test .\tests\Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~QueryRouter"` (47/47).
- PASS: review patch rerun `dotnet test .\tests\Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~QueryRouter"` (48/48).
- PASS: `dotnet test .\tests\Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~DefaultProjectionActorInvoker"` (6/6).
- PASS: `dotnet test .\tests\Hexalith.EventStore.Server.Tests --filter "FullyQualifiedName~SubmitQueryHandler|FullyQualifiedName~QueryNotFoundExceptionHandler"` (20/20).
- PASS: `dotnet build .\Hexalith.EventStore.slnx --configuration Release` (0 warnings, 0 errors).
- PASS: review patch rerun `dotnet build .\Hexalith.EventStore.slnx --configuration Release` (0 warnings, 0 errors).
- PASS: `dotnet test .\tests\Hexalith.EventStore.Client.Tests` (399/399).
- PASS: `dotnet test .\tests\Hexalith.EventStore.Contracts.Tests` (513/513).
- PASS: `dotnet test .\tests\Hexalith.EventStore.Sample.Tests` (74/74).
- PASS: `dotnet test .\tests\Hexalith.EventStore.Testing.Tests` (144/144).
- PASS: Dev-auth live smoke `POST http://localhost:8080/api/v1/queries` for unregistered projection actor returned 404 ProblemDetails with `query_projection_missing`.
- ATTEMPTED BROAD SUITE: `dotnet test .\tests\Hexalith.EventStore.Server.Tests` failed outside this story's files: 6 health/access-control baseline expectation failures and 27 DAPR live-fixture failures because the scheduler was not reachable on `localhost:6060`.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-27 | 0.1 | Created ready-for-dev post-Epic-22 ES-6 story: typed DAPR actor-not-found classification for `QueryRouter`, preserving R22A1 weak actor proxy behavior and legacy string fallback only as compatibility. | Codex |
| 2026-05-27 | 0.2 | Applied party-mode review fixes: added formal classification contract, decision table, actor-scoped status guardrails, negative test matrix, cancellation edge coverage, and weak-proxy regression requirement. | Codex |
| 2026-05-27 | 0.3 | Applied advanced elicitation refinements: added classifier precedence, exact fallback constraints, no-`ToString()` rule, red-phase test expectations, and fixture brittleness guidance. | Codex |
| 2026-05-27 | 1.0 | Implemented typed DAPR projection actor not-found classification, preserved weak actor proxy routing, added focused regression tests, and recorded validation evidence. | Codex |
| 2026-05-27 | 1.1 | Applied code review fixes: scoped away plain 404 status-only classification, added gRPC rich DAPR error metadata inspection, and verified focused QueryRouter tests plus Release build. | Codex |

## Story Completion Status

Done. Typed projection actor not-found classification implemented and review fixes applied; focused QueryRouter evidence and Release build are green. Broad Server.Tests residuals remain recorded as unrelated baseline/environment failures.
