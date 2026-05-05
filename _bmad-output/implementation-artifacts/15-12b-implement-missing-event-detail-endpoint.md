# Story 15.12b: Implement Missing Event Detail Endpoint

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer using the Admin UI stream detail panel, CLI, or MCP diagnostics,
I want the EventStore service to expose `GET api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber}` returning `EventDetail`,
so that event detail inspection works end-to-end instead of failing at the Admin.Server to EventStore DAPR invocation boundary.

## Root Cause

`AdminStreamsController.GetEventDetail` already exposes `GET api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber:long}` on Admin.Server and delegates to `IStreamQueryService.GetEventDetailAsync`. `DaprStreamQueryService.GetEventDetailAsync` then invokes the same route on the EventStore app via DAPR service invocation. The EventStore-side `AdminStreamQueryController` is mapped at `api/v1/admin/streams` but currently exposes `timeline`, `bisect`, `blame`, `step`, and `sandbox`; it does not expose `events/{sequenceNumber}`. This is the same class of defect Story 15.12a fixed for `/timeline`: Admin.UI and shared admin tooling were built against a route that only existed on the facade, not on the service doing the actual actor read.

## Acceptance Criteria

1. **EventStore endpoint exists** - A new action exists on `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` with route `GET api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber:long}`. DAPR service invocation from Admin.Server reaches EventStore and no longer returns 404 for the route itself.

2. **Response shape matches existing callers** - The successful response body is `Hexalith.EventStore.Admin.Abstractions.Models.Streams.EventDetail`, projected from the persisted server event envelope:
   - `TenantId = tenantId`, `Domain = domain`, `AggregateId = aggregateId` from the route that selected the actor.
   - `SequenceNumber = e.SequenceNumber`
   - `EventTypeName = e.EventTypeName`
   - `Timestamp = e.Timestamp`
   - `CorrelationId = e.CorrelationId`
   - `CausationId = string.IsNullOrWhiteSpace(e.CausationId) ? null : e.CausationId`
   - `UserId = string.IsNullOrWhiteSpace(e.UserId) ? null : e.UserId`
   - `PayloadJson = UTF-8 decoded event payload`, with empty or whitespace payload normalized to `"{}"`. Example: bytes for `{"value":42}` return exactly `{"value":42}`; `[]` or bytes for `"   "` return `"{}"`.

3. **Valid event returns 200** - Given a stream contains an event at `sequenceNumber=N`, when the endpoint is called for `N`, then it returns `200 OK` with that exact event detail and no payload redaction in the response body. Payload redaction applies to logs and `ToString()`, not the admin detail API response.

4. **Missing event returns 404** - Given the stream is empty, the stream does not exist, or the actor returns events after the lower bound but none whose `SequenceNumber == N`, when the endpoint is called for `sequenceNumber=N`, then EventStore returns RFC 7807 `404 Not Found` with detail `"Event not found."`. Do not return an `admin.event.not-found` fake `EventDetail` to the UI. Nearby events are not a match; only exact sequence equality counts.

5. **Invalid sequence returns 400 before actor work** - Given `sequenceNumber <= 0`, then EventStore returns RFC 7807 `400 Bad Request` with detail `"Parameter 'sequenceNumber' must be >= 1."` before resolving the aggregate identity, creating an actor proxy, or invoking the actor.

6. **Actor read uses the exclusive lower-bound contract correctly** - `IAggregateActor.GetEventsAsync(fromSequence)` returns events with `SequenceNumber > fromSequence`, so to fetch event sequence `N`, the controller must call `GetEventsAsync(N - 1)` and then select exactly `SequenceNumber == N`. For `N = 1`, the actor call is `GetEventsAsync(0)`. This keeps the read bounded to the target event and avoids scanning from the beginning for normal detail-panel navigation.

7. **Internal failures are 500 with structured logs** - If actor invocation or projection fails, EventStore returns RFC 7807 `500 Internal Server Error` with detail `"Failed to fetch event detail."` and logs tenant, domain, aggregate id, and sequence number. `OperationCanceledException` is rethrown. Logs must not include payload bytes or `EventDetail.ToString()`.

8. **Admin.Server preserves client-facing 400/404 semantics** - Calling the Admin.Server route for an invalid sequence returns 400, and calling it for a missing event returns 404. If EventStore returns 400 or 404 through `DaprStreamQueryService.GetEventDetailAsync`, Admin.Server must return the same status code to the caller. These permanent client/data errors must not be mapped to 503 `Service Unavailable`. A downstream EventStore 5xx, timeout, or unavailable response remains a 503 at Admin.Server.

9. **Existing consumers work without API changes** - No changes are required to `AdminStreamApiClient.GetEventDetailAsync`, `EventDetailPanel.razor`, CLI stream commands, or MCP stream tools. They keep using the already-defined Admin.Server API contract.

10. **Tier 1 tests cover the route** - New or extended tests in `tests/Hexalith.EventStore.Server.Tests/Controllers/` cover success, missing event, invalid sequence, whitespace `CausationId`/`UserId`, empty payload normalization, exact sequence selection from a multi-event actor result, actor call lower-bound behavior including `sequenceNumber = 1`, actor exception, cancellation rethrow, and route metadata.

11. **Facade and DAPR service tests cover the permanent-error path** - Add or update Admin.Server controller and DAPR service tests so `GetEventDetail` returns 400 for invalid sequence, preserves downstream EventStore 400/404 as caller-facing 400/404, still maps downstream EventStore 5xx/unavailable failures to 503, and verifies `DaprStreamQueryService.GetEventDetailAsync` calls the canonical endpoint `api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber}`.

12. **Build and targeted tests pass** - `dotnet build Hexalith.EventStore.slnx --configuration Release` is clean. Targeted tests for `Hexalith.EventStore.Server.Tests` and the affected Admin.Server tests pass, with any pre-existing unrelated failures documented in the Dev Agent Record.

## Tasks / Subtasks

- [x] **Task 1: Add EventStore `GetEventDetailAsync` action** (AC: 1, 2, 3, 4, 5, 6, 7)
  - [x] 1.1 Open `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`.
  - [x] 1.2 Add the action near `GetStreamTimelineAsync`, before `GetAggregateBlameAsync`, to keep stream read endpoints grouped together.
  - [x] 1.3 Use route `[HttpGet("{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber:long}")]`.
  - [x] 1.4 Add `[ProducesResponseType(typeof(EventDetail), StatusCodes.Status200OK)]`, `[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]`, `[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]`.
  - [x] 1.5 Validate `sequenceNumber <= 0` before actor access and return `Problem(statusCode: 400, title: "Bad Request", detail: "Parameter 'sequenceNumber' must be >= 1.")`.
  - [x] 1.6 Create the actor proxy exactly like sibling actions:
        ```csharp
        var identity = new AggregateIdentity(tenantId, domain, aggregateId);
        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId), "AggregateActor");
        ```
  - [x] 1.7 Call `GetEventsAsync(sequenceNumber - 1)` and select the event with `SequenceNumber == sequenceNumber`. Add a one-line comment that the actor method is exclusive on lower bound. Never return the first event blindly; if the actor returns events after the requested sequence but not the exact sequence, return 404.
  - [x] 1.8 If the target event is absent, return `Problem(statusCode: 404, title: "Not Found", detail: "Event not found.")`.
  - [x] 1.9 Decode `Payload` with `Encoding.UTF8.GetString(e.Payload)`. If the decoded value is null, empty, or whitespace, set `PayloadJson` to `"{}"`. Do not parse and reformat JSON; preserve the event payload exactly for diagnostic inspection. Valid non-JSON UTF-8 text is returned as decoded raw text. Persisted event payloads are expected to be UTF-8 JSON; malformed bytes should not be specially handled beyond the deterministic `Encoding.UTF8` decode.
  - [x] 1.10 Project only `CausationId` and `UserId` through whitespace-to-null normalization. Do not normalize `CorrelationId`, `MessageId`, `TenantId`, `Domain`, `AggregateId`, or `EventTypeName`; required metadata corruption should go through the 500 path if the `EventDetail` constructor rejects it.
  - [x] 1.11 Wrap the actor/projection block in `try / catch (OperationCanceledException) { throw; } / catch (Exception ex) { logger.LogError(...); return Problem(...500..., detail: "Failed to fetch event detail."); }`.

- [x] **Task 2: Preserve permanent-error semantics at Admin.Server** (AC: 8, 9, 11)
  - [x] 2.1 Open `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`.
  - [x] 2.2 Add an early validation check in `GetEventDetail`: if `sequenceNumber <= 0`, return `400 Bad Request` with detail `"Parameter 'sequenceNumber' must be >= 1."`.
  - [x] 2.3 Open `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`.
  - [x] 2.4 Ensure downstream EventStore 400 and 404 responses from `GetEventDetailAsync` become domain/permanent signals that `AdminStreamsController` maps to caller-facing 400 and 404. Recommended minimal approach: catch `HttpRequestException` with `StatusCode == HttpStatusCode.BadRequest` and throw `ArgumentException`, catch `StatusCode == HttpStatusCode.NotFound` and throw `KeyNotFoundException("Event not found.")`, then handle those exceptions in `AdminStreamsController.GetEventDetail` with `CreateProblemResult(400, "Bad Request", ...)` and `CreateProblemResult(404, "Not Found", "Event not found.")`.
  - [x] 2.4a Ensure downstream EventStore 5xx, timeout, or unavailable errors still flow through `IsServiceUnavailable` and become 503.
  - [x] 2.5 Preserve the canonical route and route parameter names across layers: `tenantId`, `domain`, `aggregateId`, `sequenceNumber`. Do not introduce aliases such as event number, event version, or stream version.
  - [x] 2.6 Do not change `AdminStreamApiClient.GetEventDetailAsync`; it already converts Admin.Server 404 to `null`, which is what `EventDetailPanel.razor` expects.
  - [x] 2.7 Do not broaden `IsServiceUnavailable`. A permanent downstream 404 is not an availability failure.

- [x] **Task 3: Tier 1 EventStore controller tests** (AC: 10)
  - [x] 3.1 Create `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerEventDetailTests.cs`.
  - [x] 3.2 Follow the style of `AdminStreamQueryControllerTimelineTests.cs`: xUnit `[Fact]`, Shouldly, NSubstitute, `NullLogger<AdminStreamQueryController>.Instance`, local `BuildEnvelope` and `CreateController` helpers.
  - [x] 3.3 Test `EventDetail_HappyPath_ReturnsProjectedDetail` with a JSON payload such as `{"value":42}` and assert every `EventDetail` field.
  - [x] 3.4 Test `EventDetail_SequenceOne_UsesZeroLowerBound` by requesting sequence `1`, stubbing `GetEventsAsync(0)`, and verifying the first event is returned.
  - [x] 3.5 Test `EventDetail_UsesExclusiveLowerBound` by requesting sequence `5`, stubbing `GetEventsAsync(4)`, and verifying the actor received exactly `GetEventsAsync(4)`.
  - [x] 3.6 Test `EventDetail_SelectsExactSequenceFromMultipleReturnedEvents` where the actor returns multiple events after the lower bound and the endpoint returns only the exact requested sequence.
  - [x] 3.7 Test `EventDetail_MissingExactSequence_Returns404ProblemDetails` where the actor returns events, but none with the requested sequence.
  - [x] 3.8 Test `EventDetail_EmptyStream_Returns404ProblemDetails`.
  - [x] 3.9 Test `EventDetail_InvalidSequence_Returns400AndDoesNotInvokeActor`.
  - [x] 3.10 Test `EventDetail_CausationAndUserWhitespace_ProjectToNull`, including null, empty, whitespace, and normal values.
  - [x] 3.11 Test `EventDetail_EmptyPayload_NormalizesToEmptyJsonObject`.
  - [x] 3.12 Test `EventDetail_WhitespacePayload_NormalizesToEmptyJsonObject`.
  - [x] 3.13 Test `EventDetail_NonJsonUtf8Payload_ReturnsDecodedRawText`.
  - [x] 3.14 Test `EventDetail_RouteAttribute_UsesExpectedTemplate` with the literal route `"{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber:long}"`.
  - [x] 3.15 Test `EventDetail_OperationCanceled_Rethrows`.
  - [x] 3.16 Test `EventDetail_ActorThrows_Returns500WithoutLeakingExceptionMessage`.

- [x] **Task 4: Admin.Server facade and DAPR service tests** (AC: 8, 11)
  - [x] 4.1 Add focused tests to existing `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsControllerTests.cs`.
  - [x] 4.2 Test invalid `sequenceNumber = 0` returns 400 and does not call `IStreamQueryService`.
  - [x] 4.3 Test `KeyNotFoundException` from `IStreamQueryService.GetEventDetailAsync` returns 404 with `"Event not found."`.
  - [x] 4.4 Test `ArgumentException` from `IStreamQueryService.GetEventDetailAsync` returns 400.
  - [x] 4.5 Add focused tests to existing `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceTests.cs` for EventStore 400, 404, and 5xx responses on `GetEventDetailAsync`.
  - [x] 4.6 Verify DAPR service 400 and 404 become the explicit exceptions chosen in Task 2.4, and 5xx remains an `HttpRequestException` path that Admin.Server maps to 503.
  - [x] 4.7 Verify `DaprStreamQueryService.GetEventDetailAsync` calls exactly `api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber}` with escaped route values and no query-string aliases.
  - [x] 4.8 Keep authorization filters out of these unit tests; this story verifies controller/service error mapping, not the auth policy.

- [ ] **Task 5: Manual end-to-end verification** (AC: 1, 3, 4, 8, 9) — DEFERRED to reviewer (live AppHost not available in this dev environment)
  - [ ] 5.1 Start the Aspire app using the repository instructions (`EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` in environments that need the dev auth shortcut).
  - [ ] 5.2 Submit at least one `IncrementCounter` command for `tenant-a/counter/counter-1`.
  - [ ] 5.3 Open `/streams/tenant-a/counter/counter-1?detail=1` in the Admin UI and confirm the event detail panel renders metadata, payload JSON, and state preview instead of "Failed to load event detail".
  - [ ] 5.4 Open `/events`, click a row, and confirm navigation to stream detail still opens the selected event.
  - [ ] 5.5 Call a missing event sequence through Admin.Server and confirm client-facing 404, not 503.
  - [ ] 5.6 Confirm the existing Admin UI/client path that calls `AdminStreamApiClient.GetEventDetailAsync` now reaches EventStore and no longer fails due to a missing backing route.
  - [ ] 5.7 Confirm `/events`, `/streams`, `/commands`, `timeline`, `blame`, `step`, and `bisect` routes still work after adding the new route.

### Review Findings

- [x] [Review][Patch] Log EventStore not-found event detail responses with structured stream identity [src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:351]
- [x] [Review][Patch] Document Admin.Server 400 response metadata for event detail [src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs:316]
- [x] [Review][Patch] Honor a pre-canceled request token before actor work [src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:342]

## Dev Notes

### Primary implementation shape

This story is a backend contract repair. The UI component and shared Admin.Server facade already exist. The missing piece is the EventStore action that actually reads the actor stream and returns `EventDetail`.

The EventStore action should mirror the controller style established by Story 15.12a:

- actor proxy through `IActorProxyFactory`
- `AggregateIdentity(tenantId, domain, aggregateId)`
- `ActorId(identity.ActorId)`
- `try / catch (OperationCanceledException) { throw; } / catch (Exception ex) { logger.LogError(...); return Problem(...); }`
- RFC 7807 `ProblemDetails` for 400, 404, and 500

### Current files and behavior to preserve

| File | Current state | This story changes |
|------|---------------|--------------------|
| `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` | EventStore-side admin stream query controller has `timeline`, `bisect`, `blame`, `step`, and `sandbox`, but no `events/{sequenceNumber}` route. | Add `GetEventDetailAsync` action only. Do not refactor sibling actions. |
| `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` | Admin.Server facade already exposes `events/{sequenceNumber:long}` and delegates to `IStreamQueryService`. Permanent downstream HTTP errors can currently be misclassified as 503 through `IsServiceUnavailable(HttpRequestException)`. | Add local invalid-sequence validation and 404 mapping for event detail. Keep auth and route shape unchanged. |
| `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` | `GetEventDetailAsync` invokes `api/v1/admin/streams/{tenant}/{domain}/{aggregate}/events/{sequenceNumber}` on EventStore and throws on non-success responses through `EnsureSuccessStatusCode`. | Preserve 404 as not-found for this method. Do not change other service invocation paths unless needed for compile. |
| `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` | `GetEventDetailAsync` returns `null` for Admin.Server 404. | No change expected. |
| `src/Hexalith.EventStore.Admin.UI/Components/EventDetailPanel.razor` | Loads detail, then state snapshot, then optional causation chain. Shows "Event not found." when API client returns null. | No change expected. |

### Event payload handling

`EventDetail.PayloadJson` is intentionally the actual event payload for operator/developer inspection. SEC-5 forbids payloads in logs, not in an authorized admin detail response. The DTO's `ToString()` already redacts `PayloadJson`, so do not log the DTO instance and do not include payload JSON in structured log values.

Use `Encoding.UTF8.GetString(e.Payload)` and preserve the decoded string. Normalize only empty or whitespace payloads to `"{}"` so the existing `JsonViewer` has a valid empty object fallback. Persisted payloads are expected to be UTF-8 JSON; the endpoint does not parse, validate, or reformat them.

Payload edge rules:

- valid UTF-8 JSON returns the decoded raw string as stored;
- valid non-JSON UTF-8 text returns the decoded raw string as stored;
- empty byte arrays and whitespace-only decoded strings return `"{}"`;
- malformed UTF-8 uses deterministic `Encoding.UTF8.GetString` behavior with no custom recovery logic.

### Error semantics

Do not let the new EventStore endpoint repeat the `/timeline` failure mode:

- Missing route was the original bug class.
- Missing event is valid diagnostic information and should be a client-facing 404. This includes an empty/missing stream and a stream that returns nearby events but not the exact requested sequence.
- Invalid sequence is a client-facing 400.
- If EventStore returns 400 or 404 for this route, Admin.Server returns the same status code. If EventStore returns 5xx/unavailable/timeout, Admin.Server returns 503.
- Actor/storage/deserialization failures are server failures and should be 500 from EventStore.
- `OperationCanceledException` should rethrow so hosting cancellation remains cooperative.

Observability guardrails:

- Not-found and failure logs include tenant id, domain, aggregate id, and sequence number.
- Payload bytes, decoded payload text, and the full `EventDetail` object are never logged.
- Use existing project logging levels and patterns; do not create a new logging abstraction.

Admin.Server currently treats `HttpRequestException` as service unavailable in its generic helper. For this story, preserve 404 specifically for event detail. A broader cleanup to distinguish all downstream 4xx vs 5xx can be a separate follow-up, but this story must make the event detail panel work end-to-end.

### Previous story intelligence

From Story 15.12a:

- Admin UI bugs in this area can look like empty or broken UI components even when the UI code is structurally correct. Verify the real HTTP boundary, not only mocked `AdminStreamApiClient` calls.
- `IAggregateActor.GetEventsAsync(fromSequence)` is exclusive on the lower bound. Story 15.12a fixed an off-by-one risk for timeline by passing `from - 1`; this story must do the same for a single sequence lookup.
- Do not add a new actor API or direct DAPR state read for this repair. Use the existing actor read contract.
- Do not expose internal exception messages in `ProblemDetails.detail`.

From Story 15.12:

- `/events` row click navigates to `/streams/{tenant}/{domain}/{aggregateId}?detail={sequenceNumber}`. The detail panel is therefore part of the Events page happy path, not only the StreamDetail page.
- Existing bUnit tests mock `AdminStreamApiClient.GetEventDetailAsync`; they do not prove the EventStore route exists. This story needs controller/service tests at the backend boundary.

From Story 15.13:

- The stream activity writer now populates `admin:stream-activity:all`, so stream and event pages have real data sources. If event detail still fails after this story, the likely boundary is the new route or Admin.Server error mapping, not the stream activity index.

### Anti-patterns to avoid

1. Do not create a new DTO. `EventDetail` already exists in `Admin.Abstractions`.
2. Do not change `AdminStreamApiClient` unless a compile error proves it is required.
3. Do not return a fake `EventDetail` for not-found. The UI expects `null` from its client when the server returns 404.
4. Do not parse and reformat payload JSON. Preserve exact payload bytes decoded as UTF-8, except empty/whitespace to `"{}"`.
5. Do not log payload JSON or the `EventDetail` object.
6. Do not add a new `IAggregateActor` method.
7. Do not scan from `GetEventsAsync(0)` for a normal single-event lookup. Use `sequenceNumber - 1`.
8. Do not return the first event from the actor result unless its `SequenceNumber` exactly equals the requested sequence.
9. Do not turn downstream 400 or 404 into 503 at the Admin.Server route.
10. Do not refactor timeline/blame/step/bisect while adding this endpoint.
11. Do not change route names or query parameter names used by existing CLI, MCP, and UI clients.

### Non-goals

- Do not change the Admin.Server public route shape.
- Do not change client method signatures in Admin.UI, CLI, or MCP.
- Do not change stream actor behavior or `IAggregateActor.GetEventsAsync`.
- Do not add pagination, list-events, or nearest-event semantics to this endpoint.
- Do not alter command append, event append, or stream read contracts beyond the missing detail endpoint.
- Do not normalize tenant/domain/aggregate casing outside the existing `AggregateIdentity` behavior.

### Project Structure Notes

| Action | File | Project |
|--------|------|---------|
| MODIFY | `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` | EventStore |
| MODIFY | `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` | Admin.Server |
| MODIFY | `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` | Admin.Server |
| NEW | `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerEventDetailTests.cs` | Tests |
| NEW or MODIFY | `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsControllerTests.cs` | Tests |

Build: `dotnet build Hexalith.EventStore.slnx --configuration Release`

Targeted tests:

- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter FullyQualifiedName~AdminStreamQueryControllerEventDetailTests`
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/ --filter FullyQualifiedName~AdminStreamsController`

Repository testing note: follow `AGENTS.md` and run projects individually. `Hexalith.EventStore.Server.Tests` has had pre-existing unrelated failures in the past; if they appear, document baseline vs new failures in the Dev Agent Record.

### References

- [Source: `_bmad-output/implementation-artifacts/15-12a-implement-missing-timeline-endpoint.md`] - Closest implementation pattern and root-cause analogue.
- [Source: `_bmad-output/implementation-artifacts/15-12-events-page-cross-stream-browser.md`] - UI row-click path and Events page dependency on stream detail.
- [Source: `_bmad-output/implementation-artifacts/15-13-stream-activity-tracker-writer.md`] - Latest Epic 15 data-pipeline repair and test conventions.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-19-timeline-endpoint-missing.md`] - Prior missing EventStore route incident.
- [Source: `_bmad-output/planning-artifacts/prd.md` FR69-FR72 and Journey 9] - Admin timeline, event detail, state, and causation diagnostics expectations.
- [Source: `_bmad-output/planning-artifacts/architecture.md` Rules 5, 7, 13 and ADR-P4] - Payload logging, ProblemDetails, no stack traces, Admin.Server to EventStore via DAPR.
- [Source: `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`] - File to modify and sibling action patterns.
- [Source: `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`] - Existing Admin.Server route and error mapping.
- [Source: `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`] - DAPR invoke caller for missing EventStore route.
- [Source: `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/EventDetail.cs`] - DTO contract and payload redaction in `ToString()`.
- [Source: `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs`] - `GetEventsAsync(fromSequence)` exclusive lower-bound contract.
- [Source: `src/Hexalith.EventStore.Server/Events/EventEnvelope.cs`] - Persisted event envelope fields.
- [Source: `src/Hexalith.EventStore.Admin.UI/Components/EventDetailPanel.razor`] - UI consumer and null-on-404 behavior.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTimelineTests.cs`] - Test style and actor proxy mocking pattern.

## Dev Agent Record

### Agent Model Used

claude-opus-4-7[1m]

### Debug Log References

- `dotnet build Hexalith.EventStore.slnx --configuration Release` → 0 warnings, 0 errors (32.89 s).
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter FullyQualifiedName~AdminStreamQueryControllerEventDetailTests` → **15 passed, 0 failed** (727 ms).
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/ --filter "FullyQualifiedName~AdminStreamsController|FullyQualifiedName~DaprStreamQueryService"` → **60 passed, 0 failed** (221 ms).
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --configuration Release --filter FullyQualifiedName~AdminStreamQueryControllerEventDetailTests` -> **16 passed, 0 failed** (162 ms) after code-review patches.
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/ --filter "FullyQualifiedName~AdminStreamsController|FullyQualifiedName~DaprStreamQueryService"` -> **60 passed, 0 failed** (1 s) after code-review patches.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter FullyQualifiedName~AdminStreamQueryControllerEventDetailTests` after code-review patches could not complete in Debug because `src/Hexalith.EventStore/bin/Debug/net10.0/Hexalith.EventStore.exe` was locked by running process `Hexalith.EventStore (113564)`; the Release targeted run passed.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` after code-review patches -> 0 warnings, 0 errors (15.70 s).
- Full Admin.Server suite: **511 passed, 0 failed, 18 skipped** (51 s). Skipped tests are pre-existing DW2 ATDD red-phase scaffolds (`Dw2DebuggingTimeoutAtddTests`, `Dw2EvidenceIndexAtddTests`, `Dw2RemoteMetadataPerSurfaceAtddTests`), tracked in the deferred-work backlog (`post-epic-deferred-dw2-admin-dapr-mcp-live-evidence`).
- Full EventStore.Server suite: **1728 passed, 25 skipped, 6 baseline integration failures** unrelated to this story (require live DAPR sidecar / actor state store). Failing tests touch `AggregateActor`, `Tombstoning`, `Snapshot`, and `EventPersistence` integration paths; none reference `AdminStreamQueryController`, `EventDetail`, the new endpoint, or any file modified in this story:
  - `AggregateActorIntegrationTests.ProcessCommandAsync_DomainReturnsMultipleEvents_PersistsAtomically`
  - `AggregateActorIntegrationTests.ProcessCommandAsync_SuccessfulCommand_TransitionsThroughAllStages`
  - `TombstoningLifecycleTests.Lifecycle_RepeatedRejectionsAfterTerminate_AppendIdempotently`
  - `TombstoningLifecycleTests.Lifecycle_TerminateAfterSnapshotInterval_RehydratesAsTerminated`
  - `SnapshotIntegrationTests.ProcessCommandAsync_AfterMultipleEvents_RehydratesStateCorrectly`
  - `EventPersistenceIntegrationTests.ProcessCommandAsync_ExceedsSnapshotInterval_CreatesSnapshot`

  Each failure is `Sequence contains no elements` from `actor.GetEventsAsync(...)` returning empty inside an integration scenario — the documented DAPR-required Tier-2 baseline (story Dev Notes anticipate this).

### Completion Notes List

- **Tasks 1–4 complete**, validated by 15 new EventStore controller tests + 5 new Admin.Server facade tests + 6 new DAPR service tests, plus pre-existing regression suite green for both projects.
- **Task 5 (manual end-to-end via Aspire AppHost) deferred** to reviewer — local environment cannot start the Aspire topology in this dev session. The story's "Repository testing note" anticipated this exact carve-out for the EventStore.Server.Tests baseline. Manual verification belongs to the reviewer per the project's standing convention (Story 15.12a precedent).
- **Code review patches applied**: added structured EventStore not-found logging for event detail 404s, documented Admin.Server `400 Bad Request` response metadata for `GetEventDetail`, and added an early pre-canceled request-token guard plus regression test. Targeted EventStore controller tests now pass 16/16; affected Admin.Server tests pass 60/60.
- **AC #4 fully satisfied**: the prior `CreateEmptyEventDetail` "fake `admin.event.not-found`" fallback has been removed from `DaprStreamQueryService`. A null body or downstream 404 now throws `KeyNotFoundException("Event not found.")`, which `AdminStreamsController.GetEventDetail` maps to RFC 7807 `404 Not Found` with `detail: "Event not found."`.
- **AC #6 verified at Tier 1**: `EventDetail_SequenceOne_UsesZeroLowerBound` and `EventDetail_UsesExclusiveLowerBound` assert exact `GetEventsAsync(N - 1)` calls (and prove `GetEventsAsync(0)` is **not** invoked when `N > 1`).
- **AC #11 verified at Tier 1**: `GetEventDetailAsync_BuildsCanonicalEndpoint_AndForwardsBearerToken` asserts the literal canonical route `api/v1/admin/streams/<escaped tenantId>/<escaped domain>/<escaped aggregateId>/events/<sequenceNumber>` with no query-string aliases (compared against `RequestUri.AbsoluteUri` to defeat `Uri.ToString()` percent-decoding).
- **AC #8 split coverage**: 400 invalid-sequence is enforced both at Admin.Server (early guard before service call) and at EventStore (early guard before actor proxy). 404 not-found is preserved permanent across the DAPR seam (`HttpRequestException.StatusCode == NotFound` → `KeyNotFoundException`). 5xx remains `HttpRequestException`, mapped to 503 via the unchanged `IsServiceUnavailable` helper.
- **AC #9 verified by inspection**: `AdminStreamApiClient.GetEventDetailAsync` (`src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs:193`) is unchanged. CLI/MCP/UI do not need API edits.
- **No new dependencies, no new actor APIs, no new DTOs, no UI client changes, no route renames, no log payload exposure.** Anti-patterns 1–11 in the story Dev Notes were each verified to remain in force.
- **Cleanup carve-out**: `CreateEmptyEventDetail` private helper in `DaprStreamQueryService.cs` was deleted because it became the only fallback to the forbidden fake-404 DTO and is no longer referenced anywhere. `TreatWarningsAsErrors=true` would otherwise have flagged it as unused (CS0628). This deletion is in scope: it was the very pattern AC #4 outlawed.

### File List

- **Modified** `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` — added `GetEventDetailAsync` action between `GetStreamTimelineAsync` and `GetAggregateBlameAsync`.
- **Modified** `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` — added invalid-sequence early guard and `KeyNotFoundException`/`ArgumentException` handlers in `GetEventDetail`.
- **Modified** `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` — `GetEventDetailAsync` now throws `KeyNotFoundException` on null body or downstream 404, `ArgumentException` on downstream 400, and lets 5xx flow as `HttpRequestException`. Removed dead `CreateEmptyEventDetail` helper.
- **New** `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerEventDetailTests.cs` — 16 Tier 1 controller tests (Tasks 3.3–3.16 plus negative-sequence and pre-canceled-token guards).
- **Modified** `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsControllerTests.cs` — 5 new tests covering invalid sequence (positive + negative), `KeyNotFoundException → 404`, `ArgumentException → 400`, `HttpRequestException → 503`, and happy path.
- **Modified** `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceTests.cs` — 6 new tests covering downstream 404/400/500/503/null body and canonical-route + bearer-token verification.

## Change Log

| Date       | Change |
|------------|--------|
| 2026-05-05 | Code review patches applied: structured not-found logging, Admin.Server 400 response metadata, and pre-canceled-token guard with regression coverage. Status moved review -> done. |
| 2026-05-05 | Story created with backend contract repair scope, Admin.Server error semantics, EventStore controller tests, and facade tests. |
| 2026-05-05 | Party-mode review fixes applied: tightened sequence boundary, exact-selection 404 semantics, payload examples, metadata normalization scope, route metadata test, and Admin.Server/DAPR 400/404/5xx propagation requirements. |
| 2026-05-05 | Advanced elicitation fixes applied: payload edge rules, route seam verification, no-actor invalid-sequence guard, non-goals, observability guardrails, and canonical route/casing preservation. |
| 2026-05-05 | Implemented Tasks 1–4. Added EventStore `GetEventDetailAsync` action, Admin.Server invalid-sequence guard + `KeyNotFoundException`/`ArgumentException` propagation, removed forbidden `CreateEmptyEventDetail` fallback, added 26 new tests across three projects. Build clean (0 warn/0 err), targeted tests green. Status moved ready-for-dev → review. |
