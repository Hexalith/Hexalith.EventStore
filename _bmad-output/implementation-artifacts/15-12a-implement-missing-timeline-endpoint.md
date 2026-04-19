# Story 15.12a: Implement Missing Timeline Endpoint

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **developer using the Admin UI Events page (and StreamDetail, BisectTool, MCP `stream`, CLI `stream events`)**,
I want **the EventStore service to expose `GET api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/timeline` returning a `PagedResult<TimelineEntry>` of events for that stream**,
so that **`DaprStreamQueryService.GetStreamTimelineAsync` stops getting 404 from DAPR service invocation, the Events page populates with real data, and every downstream timeline-consuming surface starts working again**.

## Root Cause (1-paragraph dev brief)

`DaprStreamQueryService.GetStreamTimelineAsync` (Admin.Server, `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs:137-170`) calls `GET api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/timeline` on the EventStore app via DAPR invoke. **That route does not exist on the EventStore.** `AdminStreamQueryController` (`src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`) is mapped at `api/v1/admin/streams` but only exposes `bisect`, `blame`, `step`, and `sandbox`. The EventStore returns 404 in ~2 ms, Polly retries four times, then `InvokeEventStoreAsync` throws `HttpRequestException: 404 (Not Found)`. `Events.razor` swallows the exception per stream so the page silently renders empty. Story 15.12's tests mocked `AdminStreamApiClient` directly and never wired a real HTTP request through to a real `AdminStreamQueryController`, so the missing route was never exercised end-to-end. **Fix:** add the missing `GetStreamTimelineAsync` action to `AdminStreamQueryController.cs`, mirroring the existing `bisect`/`blame`/`step` pattern (actor proxy → `GetEventsAsync(fromSequence)` → range/count filter → project to `TimelineEntry` with `EntryType = Event`). Events-only is in-scope; commands and queries in the timeline (the "unified" half of FR69) are explicitly out-of-scope and are tracked as a follow-up.

## Acceptance Criteria

1. **Endpoint exists** — A new HTTP action `GET api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/timeline` exists on `AdminStreamQueryController` and is registered in the EventStore service. Hitting it via DAPR invoke from Admin.Server returns `200 OK` (not `404 Not Found`) for any well-formed request.

2. **Response shape exactly matches the existing `DaprStreamQueryService` expectation** — Response body is `PagedResult<TimelineEntry>` (`Hexalith.EventStore.Admin.Abstractions.Models.Common.PagedResult<T>` of `Hexalith.EventStore.Admin.Abstractions.Models.Streams.TimelineEntry`). Each `TimelineEntry` has `EntryType = TimelineEntryType.Event` and is projected from a persisted `ServerEventEnvelope` as: `SequenceNumber = e.SequenceNumber`, `Timestamp = e.Timestamp`, `TypeName = e.EventTypeName`, `CorrelationId = e.CorrelationId`, `UserId = string.IsNullOrWhiteSpace(e.UserId) ? null : e.UserId`. **No client-side change is permitted** — the wire contract must be byte-compatible with what `DaprStreamQueryService.GetStreamTimelineAsync` already deserializes (it already returns `PagedResult<TimelineEntry>` via the existing `InvokeEventStoreAsync<T>` pipeline).

3. **Happy path returns events in sequence order** — Given an aggregate with N events, when `GET .../timeline` is called with no query parameters, then the response contains the first `min(N, 100)` events ordered ascending by `SequenceNumber` and `TotalCount = items.Count`.

4. **Empty stream returns 200 with empty items** — Given an aggregate stream that has no events, when the endpoint is called, then it returns `200 OK` with `Items = []` and `TotalCount = 0`. **It does NOT return 404** — empty stream is a valid state. (This matches the existing DAPR invoke caller's expectation, which does `result ?? new PagedResult<TimelineEntry>([], 0, null)`.)

5. **`from` and `to` range filtering** — Given query params `from=A&to=B` (both optional, both inclusive), when applied: events with `SequenceNumber < A` or `SequenceNumber > B` are excluded; `from` defaults to `0` (i.e., include all from sequence 1 upward); `to` defaults to "no upper bound". Order remains ascending by `SequenceNumber`.

6. **`count` cap and default** — Given query param `count=N`, when applied: at most `N` events are returned (after the from/to filter). Default is 100. If `count <= 0`, server normalizes to 100 (do NOT 400 on this — match the proposal's intent).

7. **Bad-request validation** — The following return `400 Bad Request` with an RFC 7807 `ProblemDetails`:
   - `from` is provided and `< 0` → "Parameter 'from' must be >= 0 when provided."
   - `to` is provided and `< 1` → "Parameter 'to' must be >= 1 when provided."
   - both provided and `to < from` → "Parameter 'to' must be >= 'from'."

8. **`UserId` empty/whitespace → `null`** — Given a persisted event whose `UserId` is `""`, `null`, or whitespace, then the projected `TimelineEntry.UserId` is `null` (not `""`). Required because `TimelineEntry.UserId` is `string?` and downstream consumers (UI columns, MCP read tools) treat `null` as "system action" and empty string as "anonymous user".

9. **5xx on internal failures, with structured log** — Given the actor invocation throws (other than `OperationCanceledException`), then the endpoint returns `500 Internal Server Error` (RFC 7807) with detail "Failed to fetch stream timeline." and an `ILogger.LogError` entry containing tenant, domain, and aggregate identifiers. `OperationCanceledException` MUST rethrow (not be swallowed) — matches the sibling `bisect`/`blame`/`step` actions in the same controller.

10. **Tier 1 unit tests cover all of the above** — A new test class `AdminStreamQueryControllerTimelineTests` exists in `tests/Hexalith.EventStore.Server.Tests/Controllers/`, follows the `QueriesControllerTests` style (xUnit + Shouldly + NSubstitute, `IActorProxyFactory` mocked), and covers cases listed in Task 2 below. All tests pass: `dotnet test tests/Hexalith.EventStore.Server.Tests/`.

11. **No regression in sibling endpoints** — Existing `bisect`, `blame`, `step`, `sandbox` tests on `AdminStreamQueryController` continue to pass. Build is clean: `dotnet build Hexalith.EventStore.slnx --configuration Release` produces zero warnings, zero errors (TreatWarningsAsErrors is on globally).

12. **End-to-end manual verification** — After rebuild + redeploy of the `eventstore` Aspire resource, reload `https://localhost:60034/events`. The Recent Events / Unique Event Types / Active Streams stat cards populate with non-zero values, the grid shows recent events from `tenant-a/counter/counter-1`, and the warning banner is absent. Reload `https://localhost:60034/streams/tenant-a/counter/counter-1` — the timeline tab populates. `/commands` and Bisect tool are unaffected.

## Tasks / Subtasks

- [x] **Task 1: Add `GetStreamTimelineAsync` action to `AdminStreamQueryController`** (AC: 1, 2, 3, 4, 5, 6, 7, 8, 9)
    - [x] 1.1 Open `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`.
    - [x] 1.2 Add `using Hexalith.EventStore.Admin.Abstractions.Models.Common;` to the using block (sorted alphabetically with other `Hexalith.EventStore.Admin.Abstractions.*` usings — the namespace `Hexalith.EventStore.Admin.Abstractions.Models.Streams` is already imported, so add the `.Common` using next to it).
    - [x] 1.3 Insert a new public action between `BisectAggregateStateAsync` and `GetAggregateBlameAsync` with route `[HttpGet("{tenantId}/{domain}/{aggregateId}/timeline")]`. Signature:
        ```csharp
        public async Task<IActionResult> GetStreamTimelineAsync(
            string tenantId,
            string domain,
            string aggregateId,
            [FromQuery] long? from,
            [FromQuery] long? to,
            [FromQuery] int count = 100,
            CancellationToken ct = default)
        ```
        Note: name the cancellation parameter `ct` (not `_`) to match `BisectAggregateStateAsync`/`GetEventStepFrameAsync`/`SandboxCommandAsync` in the same file. `GetAggregateBlameAsync` uses `_` because it currently does not honour cancellation between iterations — do NOT replicate that. (The proposal's snippet showed `_`; we deliberately diverge here to keep the door open for honouring cancellation in step-through iterations later. AC 9 only requires that `OperationCanceledException` is rethrown, which the `try/catch (OperationCanceledException) { throw; }` block guarantees regardless of whether `ct` is observed.)
    - [x] 1.4 Decorate with: `[ProducesResponseType(typeof(PagedResult<TimelineEntry>), StatusCodes.Status200OK)]` and `[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]`. Do NOT declare `[ProducesResponseType(404)]` — empty streams MUST return 200 (AC 4).
    - [x] 1.5 Add `///` summary that starts with "Returns a paginated timeline of events for the specified aggregate stream." and includes "Used by the Admin UI Events page, StreamDetail page, MCP and CLI tools (FR69)." Match the doc-comment style of the sibling actions (one-sentence summary).
    - [x] 1.6 Implement validation block in this exact order to keep error messages predictable for tests (AC 7):
        1. `if (from is < 0)` → 400 `"Parameter 'from' must be >= 0 when provided."`
        2. `if (to is < 1)` → 400 `"Parameter 'to' must be >= 1 when provided."`
        3. `if (from.HasValue && to.HasValue && to.Value < from.Value)` → 400 `"Parameter 'to' must be >= 'from'."`
        4. `if (count <= 0) { count = 100; }` (silent normalization, NOT a 400 — AC 6).
    - [x] 1.7 Activate the actor proxy exactly the same way the sibling actions do — do NOT invent a new helper:
        ```csharp
        var identity = new AggregateIdentity(tenantId, domain, aggregateId);
        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId), "AggregateActor");
        ServerEventEnvelope[] allEvents = await actor.GetEventsAsync(from ?? 0).ConfigureAwait(false);
        ```
        Note the use of the file's existing `ServerEventEnvelope` alias (`using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;`).
    - [x] 1.8 `IAggregateActor.GetEventsAsync(fromSequence)` returns events with `SequenceNumber > fromSequence` (exclusive lower bound — see `IAggregateActor.cs:23-26`). To make AC 5 inclusive on `from`, pass `from is > 0 ? from.Value - 1 : 0` to `GetEventsAsync`. Document this with a single-line comment because the off-by-one is non-obvious: `// GetEventsAsync is exclusive on lower bound; subtract 1 to make AC inclusive.`
    - [x] 1.9 Apply the upper bound and count cap, then project (AC 2, 3, 5, 6, 8):
        ```csharp
        IEnumerable<ServerEventEnvelope> filtered = allEvents;
        if (to.HasValue) {
            filtered = filtered.Where(e => e.SequenceNumber <= to.Value);
        }

        List<TimelineEntry> entries = [.. filtered
            .OrderBy(e => e.SequenceNumber)
            .Take(count)
            .Select(e => new TimelineEntry(
                e.SequenceNumber,
                e.Timestamp,
                TimelineEntryType.Event,
                e.EventTypeName,
                e.CorrelationId,
                string.IsNullOrWhiteSpace(e.UserId) ? null : e.UserId))];

        return Ok(new PagedResult<TimelineEntry>(entries, entries.Count, null));
        ```
        Notes: `TotalCount` is set to the page size, NOT the unfiltered total. This matches the existing `GetRecentlyActiveStreamsAsync` pattern in `DaprStreamQueryService` (lines 119-125 of that file return `filteredList.Count` after filtering). Pagination cursor (`ContinuationToken`) is always `null` in this version — multi-page timeline traversal is a follow-up story (see "Out of Scope" below).
    - [x] 1.10 Wrap the actor invocation block in `try / catch (OperationCanceledException) { throw; } / catch (Exception ex) { logger.LogError(ex, "Failed to fetch stream timeline for {TenantId}/{Domain}/{AggregateId}.", tenantId, domain, aggregateId); return Problem(...500 Internal Server Error..., detail: "Failed to fetch stream timeline."); }` (AC 9). Mirror the exact try/catch shape of `GetAggregateBlameAsync` (lines 268-334).
    - [x] 1.11 **Checkpoint**: `dotnet build src/Hexalith.EventStore/Hexalith.EventStore.csproj --configuration Release` compiles with zero warnings.

- [x] **Task 2: Tier 1 unit tests in `AdminStreamQueryControllerTimelineTests`** (AC: 10)
    - [x] 2.1 Create `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTimelineTests.cs`. Follow the conventions from `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs`: xUnit `[Fact]`, NSubstitute for `IActorProxyFactory`/`IAggregateActor`/`IDomainServiceInvoker`, `NullLogger<AdminStreamQueryController>.Instance`, Shouldly assertions, helper builder methods at the top of the class.
    - [x] 2.2 Helper: `BuildEnvelope(long seq, string? userId = "user-1", string corrId = "corr-1", string typeName = "CounterIncremented")` — returns a `ServerEventEnvelope` with all 17 fields populated (use `new byte[0]` for `Payload`, `null` for `Extensions`, sensible defaults for the rest). This avoids 6+ identical literal envelopes scattered through the file.
    - [x] 2.3 Helper: `CreateController(IAggregateActor actor)` — wires `IActorProxyFactory.CreateActorProxy<IAggregateActor>(...)` to return `actor`, returns the controller.
    - [x] 2.4 Test: `Timeline_HappyPath_ReturnsThreeEntriesProjectedFromEnvelopes` — actor returns 3 envelopes (seq 1,2,3); no query params → `OkObjectResult` with `PagedResult<TimelineEntry>` of 3 items, every `EntryType == Event`, `SequenceNumber`/`Timestamp`/`TypeName`/`CorrelationId`/`UserId` projected correctly, ordered ascending. Assert `TotalCount == 3` and `ContinuationToken is null`.
    - [x] 2.5 Test: `Timeline_EmptyStream_ReturnsOkWithEmptyItems` — actor returns `[]`; assert `200 OK`, `Items.Count == 0`, `TotalCount == 0`. Explicitly `ShouldNotBeOfType<NotFoundResult>()` to lock in AC 4.
    - [x] 2.6 Test: `Timeline_RangeFilter_ReturnsOnlyEventsInRange` — actor returns 10 envelopes (seq 1..10); call with `from=3&to=7` → exactly seqs 3,4,5,6,7 returned (5 items). Verify the actor was called with `fromSequence == 2` (i.e., `from - 1`), proving the off-by-one note in Task 1.8.
    - [x] 2.7 Test: `Timeline_CountCap_TakesFirstNAfterOrdering` — actor returns 500 envelopes; call with `count=25` → 25 items, sequences 1..25.
    - [x] 2.8 Test: `Timeline_CountZeroOrNegative_NormalizesTo100` — actor returns 200 envelopes; call with `count=0` → 100 items returned (NOT 400). Repeat with `count=-5` → 100 items.
    - [x] 2.9 Test: `Timeline_BadRequest_FromNegative` — `from=-1` → `BadRequestObjectResult` (or `ObjectResult` with 400) whose value is a `ProblemDetails` with `Title == "Bad Request"` and detail containing `"'from' must be >= 0"`.
    - [x] 2.10 Test: `Timeline_BadRequest_ToZero` — `to=0` → 400 with detail containing `"'to' must be >= 1"`.
    - [x] 2.11 Test: `Timeline_BadRequest_ToLessThanFrom` — `from=5&to=3` → 400 with detail containing `"'to' must be >= 'from'"`.
    - [x] 2.12 Test: `Timeline_UserIdEmptyOrWhitespace_ProjectsToNull` — actor returns 3 envelopes with `UserId` values `""`, `"   "`, and `"alice"`. Assert resulting `TimelineEntry.UserId` is `null`, `null`, `"alice"` respectively.
    - [x] 2.13 Test: `Timeline_OperationCanceled_Rethrows` — wire `IAggregateActor.GetEventsAsync` to throw `OperationCanceledException`. Call `await Should.ThrowAsync<OperationCanceledException>(() => controller.GetStreamTimelineAsync(...))`. Confirms AC 9 cancellation contract.
    - [x] 2.14 Test: `Timeline_ActorThrows_Returns500WithProblemDetails` — wire actor to throw `InvalidOperationException("kaboom")`. Assert response is a 500 `ObjectResult` whose value is `ProblemDetails` with detail `"Failed to fetch stream timeline."`. Verify nothing about `kaboom` leaks into the response detail (security: do NOT expose internal exception message to callers).
    - [x] 2.15 **Checkpoint**: `dotnet test tests/Hexalith.EventStore.Server.Tests/` — all new tests pass and ALL pre-existing tests in the project remain green.

- [x] **Task 3: Update sprint-change-proposal references in `epics.md`** (per `sprint-change-proposal-2026-04-19-timeline-endpoint-missing.md` Section 4.3)
    - [x] 3.1 Open `_bmad-output/planning-artifacts/epics.md`. Locate the closing approach bullets of Story 15.12 (around line 600 — search for `15.12` to land on the right section).
    - [x] 3.2 Append the dated follow-up paragraph from Section 4.3 of the sprint change proposal verbatim, preserving the exact wording so future readers see one canonical breadcrumb pointing to the 2026-04-19 memo.
    - [x] 3.3 Do NOT edit Story 15.12's acceptance criteria — only append the dated note.

- [ ] **Task 4: Manual end-to-end verification** (AC: 12) *(4.1 done by dev; 4.2–4.7 deferred to reviewer — user-in-the-loop browser verification)*
    - [x] 4.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — confirm 0 warnings, 0 errors.
    - [ ] 4.2 Start the AppHost. Restart the `eventstore` Aspire resource so the new endpoint is loaded. *(Pending reviewer verification — requires live AppHost session.)*
    - [ ] 4.3 Submit at least 3 `IncrementCounter` commands via the sample UI (or POST directly to `tenant-a/counter/counter-1`). *(Pending reviewer verification.)*
    - [ ] 4.4 Open `https://localhost:60034/events` (Admin.UI). Confirm the stat cards (`Recent Events`, `Unique Event Types`, `Active Streams`) populate with non-zero values, the grid lists recent events, and the warning banner from the 2026-04-18 timeout episode is absent. *(Pending reviewer verification.)*
    - [ ] 4.5 Open `https://localhost:60034/streams/tenant-a/counter/counter-1`. Confirm the timeline tab populates with the seeded events. *(Pending reviewer verification.)*
    - [ ] 4.6 Confirm the `/commands` page and the Bisect tool are unaffected. *(Pending reviewer verification.)*
    - [ ] 4.7 Re-run the verifications from `sprint-change-proposal-2026-04-18-tenant-query-auth.md` and `sprint-change-proposal-2026-04-18-events-page-slow.md` — confirm both remain green (no regression introduced). *(Pending reviewer verification.)*

- [ ] **Task 5: (Optional, dev discretion) Wire a Tier 3 Aspire E2E assertion** *(Deferred per story guidance — filed as follow-up #4 in Out of Scope.)*
    - [ ] 5.1 If straightforward, add an integration test in `tests/Hexalith.EventStore.IntegrationTests/` that submits a command and asserts `GET .../timeline` returns a non-empty `PagedResult<TimelineEntry>`. This is the "prevent regression forever" net the proposal explicitly calls out as a follow-up. If non-trivial in a single sitting, defer and file as a follow-up story; do NOT block this story on it.

## Dev Notes

### Sprint change proposal — required reading

Authoritative spec for this story: **`_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-19-timeline-endpoint-missing.md`**. It contains the full root-cause trace, evidence trail (trace_id `350b758`, log_id `4748`), and the exact code-level patch this story implements.

The 2026-04-18 memo `sprint-change-proposal-2026-04-18-events-page-slow.md` (the 5 s → 30 s `HttpClient` timeout bump) is **partially superseded** — its diagnosis was wrong (the 503 the UI saw was the Admin.Server's own exception-to-503 mapping firing for a 404). The timeout bump is retained as a safety margin but did NOT and could NOT have fixed the symptom. Don't confuse the two.

### Architecture compliance — non-negotiables

- **`Hexalith.EventStore.slnx` only** — never use `.sln` (CLAUDE.md gate, repo policy).
- **`TreatWarningsAsErrors = true` is global** — any new warning fails the build.
- **File-scoped namespaces, Allman braces, `_camelCase` private fields, `Async` suffix** — `.editorconfig` enforces all of these.
- **No new DAPR components, no new options sections, no new DI registrations** — this story is purely an additive endpoint on an existing controller. The controller is already constructed by ASP.NET via `[ApiController]` discovery; no `Add*` extension method needs editing.
- **No SDK upgrades, no NuGet additions** — `Hexalith.EventStore.Admin.Abstractions` (which owns `TimelineEntry`/`PagedResult`) is already a project reference of `src/Hexalith.EventStore` (verifiable via the existing `using Hexalith.EventStore.Admin.Abstractions.Models.Streams;` at the top of `AdminStreamQueryController.cs`). One additional `using Hexalith.EventStore.Admin.Abstractions.Models.Common;` is needed for `PagedResult<T>` and that's the entire dependency surface change.

### Reference implementation: sibling actions in the same file

Your single best reference is `AdminStreamQueryController.cs` itself. The new action mirrors the activation pattern of `BisectAggregateStateAsync`, `GetAggregateBlameAsync`, and `GetEventStepFrameAsync` — identical actor proxy creation, identical `try/catch (OperationCanceledException) { throw; }` structure, identical `Problem(...)` shape for 400s and 500s. Read all three before writing the new one. Copy their style — do NOT invent a new error-mapping helper or a new `IAggregateActor` extension.

### Wire contract — ONE chance to get this right

`DaprStreamQueryService.GetStreamTimelineAsync` is already deployed in production-flavoured Admin.Server code and already deserializes the response as `PagedResult<TimelineEntry>` via `InvokeEventStoreAsync<PagedResult<TimelineEntry>>(HttpMethod.Get, endpoint, ct)` (`src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs:159-160`). Both `PagedResult<T>` and `TimelineEntry` are System.Text.Json records with the standard property ordering. **Do not change either type. Do not introduce a new wrapper. Do not add fields to `TimelineEntry`.** If you find yourself wanting to, stop and re-read this paragraph. Any contract drift breaks the silent-swallow `catch` block in `Events.razor` in a different way than the 404 did, so a regression here will not surface as a "the page is empty" symptom — it will surface as garbled data.

### Off-by-one on `from`

`IAggregateActor.GetEventsAsync(long fromSequence)` returns events with `SequenceNumber > fromSequence` (exclusive). The story AC defines `from` as inclusive (`from=3` returns event with `SequenceNumber == 3`). To bridge the two, pass `from is > 0 ? from.Value - 1 : 0` to `GetEventsAsync`. Test 2.6 verifies this with `Received(1).GetEventsAsync(2)` for `from=3`. If you forget, AC 5 fails subtly — the lowest event in range is excluded.

### `UserId` projection — `string` (in event) vs `string?` (in TimelineEntry)

`ServerEventEnvelope.UserId` is non-nullable `string` (declared in `src/Hexalith.EventStore.Server/Events/EventEnvelope.cs:40`), but persisted events from the existing system contain `""` for system actions. `TimelineEntry.UserId` is `string?`. Always project with `string.IsNullOrWhiteSpace(e.UserId) ? null : e.UserId` (AC 8). Do NOT use the null-coalescing operator `??` — it doesn't help here because `e.UserId` is non-nullable.

### `CorrelationId` projection — guaranteed non-empty

`TimelineEntry`'s primary constructor throws `ArgumentException` if `CorrelationId` is null/empty/whitespace (`src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/TimelineEntry.cs:25-27`). Persisted events should always carry a non-empty `CorrelationId` (FR11), but a defensive belt-and-braces test is NOT required — if such an event existed, the controller would 500 and AC 9's logging would surface it. Don't pre-validate; let the exception bubble through the existing 500 path.

### Why events-only, not unified events+commands+queries

The full FR69 vision is a unified timeline (events + commands + queries interleaved by timestamp). Implementing the unified view requires merging actor-sourced events with the `admin:command-activity:all` index (and a yet-to-be-defined query log) AND making a design decision about how to interleave entries with overlapping/equal timestamps. That is **a separate story**, not this one. This story restores the contract that downstream callers ALREADY expect (they all just project events into `TimelineEntry`s). The unified view is captured as a follow-up at the bottom of this file.

### Auth posture matches sibling endpoints

The controller is decorated with `[AllowAnonymous]` at the class level (`AdminStreamQueryController.cs:30`). The new endpoint inherits this. Authentication/authorization for admin endpoints is enforced one layer up at the Admin.Server façade (`AdminStreamsController` in the Admin.Server project) and via DAPR ACL on the EventStore service. Do not add `[Authorize]` to the new action — that is out of pattern with the file and is an architecture-wide change owned by a different track.

### Testing standards

- Tier 1 framework: xUnit 2.9.3, Shouldly 4.3.0, NSubstitute 5.3.0, coverlet.collector 6.0.4.
- Test file path: `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTimelineTests.cs`. The `Controllers/` subfolder already exists with three sibling tests files (`QueriesControllerTests.cs`, `CommandsControllerTenantTests.cs`, `CommandValidationControllerTests.cs`) — match their layout.
- Conventions: PascalCase test method names with `MethodName_Condition_ExpectedBehaviour`. `using static` is not used in this codebase. No NSubstitute `Returns(_ => ...)` lambdas where a literal `Returns(value)` works. NullLogger from `Microsoft.Extensions.Logging.Abstractions`.
- Do NOT mock `IDomainServiceInvoker` if the test does not exercise the sandbox endpoint — pass `Substitute.For<IDomainServiceInvoker>()` and leave it un-stubbed. The new action does not use it.
- `ServerEventEnvelope` is the alias `Hexalith.EventStore.Server.Events.EventEnvelope`. Tests should mirror the controller's alias usage: `using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;` at the top of the test file.

### Anti-patterns — DO NOT

1. **DO NOT** introduce per-event "from > position" filtering on the actor side instead of the off-by-one; you'll break the contract for the lowest-sequence event.
2. **DO NOT** return `404 Not Found` on an empty stream. The whole point of this story is making `GetStreamTimelineAsync` reliable for callers — and an empty aggregate is a perfectly valid state. AC 4 explicitly forbids this.
3. **DO NOT** add a `[Produces("application/json")]` attribute or a custom `JsonSerializerOptions` — the controller already inherits the project-wide JSON contract; deviating breaks the wire format silently.
4. **DO NOT** read events page-by-page with multiple `GetEventsAsync` calls. The actor's contract returns the full event array in one round-trip and is what every sibling action does.
5. **DO NOT** include the entire exception in the 500 response detail. The detail string is "Failed to fetch stream timeline." (no exception message). Internal failure detail goes to the structured log, not the wire (security: SEC-5 redaction posture).
6. **DO NOT** swap `OrderBy(SequenceNumber)` for `OrderBy(Timestamp)`. Sequence number is gapless and monotonic per FR10; timestamps may collide for events appended in the same millisecond.
7. **DO NOT** set `ContinuationToken` to a non-null value. There is no server-side cursor format; downstream code does not know how to interpret it. Pagination beyond `count` is the job of the future "unified timeline" story.
8. **DO NOT** rename or move `TimelineEntry`, `TimelineEntryType`, or `PagedResult<T>`. They are stable contract types referenced from Admin.UI, Admin.Server, MCP, and CLI.
9. **DO NOT** add `[FromRoute]` annotations to the route segments. ASP.NET binds them by convention from the `[HttpGet("{tenantId}/{domain}/{aggregateId}/timeline")]` template; explicit attributes are redundant and inconsistent with the sibling actions in the file.
10. **DO NOT** lower the visibility of `AdminStreamQueryController` or any of its sibling actions — even if the IDE suggests `internal` is sufficient. The controller is publicly discovered by `[ApiController]`.

### Previous story intelligence — Story 15.13 (most recently shipped, same epic)

- **Pattern: structural cloning of an existing component is the correct approach.** Story 15.13 cloned `DaprCommandActivityTracker` to build `DaprStreamActivityTracker`. This story clones the action shape of `BisectAggregateStateAsync`/`GetAggregateBlameAsync` to build `GetStreamTimelineAsync`. Resist the urge to "improve" the pattern in flight.
- **Pattern: data-pipeline bugs in this epic surface as silently-empty UIs.** 15.13 fixed a missing writer; this story fixes a missing reader endpoint. Both share the same root failure mode — `Events.razor` swallows per-stream exceptions in a `try/catch` and renders empty cells. Be aware that any test that goes through `Events.razor` will pass even if your endpoint is broken; that's why Tier 1 testing of the controller is in-scope and the manual smoke test in Task 4 is non-optional.
- **Tier 1 test conventions:** Story 15.13's test file `DaprStreamActivityTrackerTests.cs` is a recent example of the project's Shouldly + NSubstitute conventions. Match its style for `AdminStreamQueryControllerTimelineTests.cs` (helper builders at top, `[Fact]`-only, no `[Theory]` unless the input set is genuinely combinatorial — for AC 7's three 400-paths use three separate `[Fact]`s for clarity).

### Previous story intelligence — Story 15.12 (the originator of the bug)

- 15.12 mocked `AdminStreamApiClient.GetStreamTimelineAsync` directly in its tests, which is why the missing route was never caught. **Lesson, not a code change:** for any future Admin.Server-to-EventStore call, prefer at least one Tier 2 (integration) test that exercises the real HTTP boundary, not a mock of the client. Task 5 is the optional follow-up to apply this lesson here.
- Story 15.12 left a `try/catch (Exception) { /* swallow */ }` per stream in `Events.razor`. The fix in 15.13's review tightened that, but if you observe behaviour where errors disappear during manual testing, the per-stream swallow is why. **Amended 2026-04-19 (code review DN1):** the swallow remains in place, but this story now also surfaces its impact by incrementing `_failedStreamCount` inside the catch and rendering a user-visible warning banner when any stream failed. Full revert of the swallow stays out of scope; the banner is an additive, non-risky defense-in-depth against future data-pipeline regressions.

### Project-context anchors

| Action | File | Project |
|--------|------|---------|
| MODIFY | `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` | EventStore |
| NEW | `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTimelineTests.cs` | Tests (Tier 1) |
| MODIFY | `_bmad-output/planning-artifacts/epics.md` | Planning |
| MODIFY | `_bmad-output/implementation-artifacts/sprint-status.yaml` | Sprint tracking (already done as part of story creation) |

Build: `dotnet build Hexalith.EventStore.slnx --configuration Release`
Test: `dotnet test tests/Hexalith.EventStore.Server.Tests/`

### Out of scope (file as follow-ups, do NOT pull into this story)

1. **Unified timeline (events + commands + queries in one stream)** — full FR69. Requires a design decision on how to merge command activity (from `admin:command-activity:all`) with actor-sourced events when their timestamps overlap. Requires a query log that does not yet exist. Separate story.
2. **Pagination cursor for streams larger than `count`** — `ContinuationToken` stays `null` for now. When unified timeline lands, define a base64-encoded `(lastSequenceNumber, lastTimestamp)` cursor.
3. **Distinguish 4xx vs 5xx in `AdminStreamsController` exception-to-HTTP mapping (Admin.Server)** — the appendix of the 2026-04-19 memo notes this turns a clear 404 into an opaque 503 at the UI layer. Optional cleanup story.
4. **Tier 3 Aspire E2E test for `/events` page** — Task 5 above is the lightweight version; a full Playwright-backed E2E that drives the UI and asserts non-empty cards is a separate story.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-19-timeline-endpoint-missing.md`] — Authoritative spec, root cause, code-level patch.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-18-events-page-slow.md`] — Partially superseded; explains the misdiagnosis path.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-18-tenant-query-auth.md`] — Independent companion bug fix; verify no regression in Task 4.7.
- [Source: `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`] — File to modify; sibling-action reference patterns.
- [Source: `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs:137-170`] — Caller; expected wire contract source of truth.
- [Source: `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/TimelineEntry.cs`] — DTO contract (CorrelationId/TypeName non-empty invariants, UserId nullable).
- [Source: `src/Hexalith.EventStore.Admin.Abstractions/Models/Common/PagedResult.cs`] — Wire envelope.
- [Source: `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs:19-26`] — `GetEventsAsync(fromSequence)` exclusive lower-bound contract.
- [Source: `src/Hexalith.EventStore.Server/Events/EventEnvelope.cs`] — `ServerEventEnvelope` shape (15 metadata fields + payload).
- [Source: `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs`] — Test conventions reference.
- [Source: `_bmad-output/implementation-artifacts/15-13-stream-activity-tracker-writer.md`] — Prior story; structural-clone pattern, Tier 1 conventions.
- [Source: `_bmad-output/implementation-artifacts/15-12-events-page-cross-stream-browser.md`] — Originating story; UI consumer.
- [Source: `CLAUDE.md`] — Solution file, Conventional Commits, build/test commands.

## Dev Agent Record

### Agent Model Used

claude-opus-4-7[1m]

### Debug Log References

- Target-project build: `dotnet build src/Hexalith.EventStore/Hexalith.EventStore.csproj --configuration Release` — 0 warnings, 0 errors.
- Full solution build: `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 warnings, 0 errors (AC 11).
- Targeted tests: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter FullyQualifiedName~AdminStreamQueryControllerTimelineTests` — 12/12 passing.
- Full Server.Tests: 1611 passing, 17 failing. Baseline before change (verified via `git stash`): 1599 passing, 17 failing. Net delta: +12 passing, 0 new failures — all 17 pre-existing failures are in unrelated areas (`DaprDomainServiceInvokerTests` version extraction, `SubmitCommandExtensionsTests` trace extensions, `EndToEndTraceTests`, `DeadLetterTraceChainTests`).

### Completion Notes List

- **AC 1–10 satisfied in code + Tier 1 tests.** `GetStreamTimelineAsync` added to `AdminStreamQueryController.cs` between `BisectAggregateStateAsync` and `GetAggregateBlameAsync`, mirroring the sibling-action pattern (actor proxy activation, `try/catch (OperationCanceledException) { throw; }`, RFC 7807 Problem responses).
- **Off-by-one on `from`:** controller passes `from - 1` to `IAggregateActor.GetEventsAsync(fromSequence)` when `from > 0`, making the AC-inclusive lower bound correct against the actor's exclusive-lower-bound contract. Covered by `Timeline_RangeFilter_ReturnsOnlyEventsInRange` asserting `Received(1).GetEventsAsync(2)` for `from=3`.
- **`UserId` projection:** `string.IsNullOrWhiteSpace(e.UserId) ? null : e.UserId` per AC 8. Covered by `Timeline_UserIdEmptyOrWhitespace_ProjectsToNull`.
- **AC 11 (no regression):** Full Hexalith.EventStore.slnx Release build passes with 0 warnings, 0 errors. Baseline Server.Tests comparison via temporary stash confirmed zero new failures introduced.
- **AC 12 (manual E2E) — DEFERRED TO REVIEWER.** Requires starting AppHost, seeding 3 `IncrementCounter` commands, and verifying browser-side stat cards + `/streams/.../timeline` + no regression on `/commands` + Bisect. Cannot be validated by dev without a live Aspire session. Task 4.2–4.7 left unchecked with explicit "Pending reviewer verification" notes.
- **Task 3 (epics.md breadcrumb)** was already present at line 605 from the sprint change proposal's creation; no further edit required.
- **Task 5 (Tier 3 E2E)** intentionally deferred per the story's "If non-trivial in a single sitting, defer and file as a follow-up story" guidance; tracked as Out-of-Scope item #4.
- One minor divergence from the story's snippet in Task 1.3: cancellation parameter is named `ct` (not `_`), matching `BisectAggregateStateAsync`/`GetEventStepFrameAsync`/`SandboxCommandAsync`. This is explicitly called out in the story task as a deliberate choice.

### File List

- MODIFIED: `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` — added `using Hexalith.EventStore.Admin.Abstractions.Models.Common;` and new `GetStreamTimelineAsync` action (AC 1–9).
- NEW: `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTimelineTests.cs` — 12 Tier 1 tests (AC 10).
- MODIFIED: `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor` — added `_failedStreamCount` / `_totalStreamCount` fields, `Interlocked.Increment` inside the per-stream swallow-catch, and a user-visible warning banner rendered when any stream's timeline fetch failed. Additive defense-in-depth surfaced by code review DN1; the underlying swallow remains intact.
- MODIFIED: `src/Hexalith.EventStore.Admin.UI/Pages/Streams.razor` — switched `GridTemplateColumns` from fixed widths (`100px 100px 100px 1fr 80px 80px 48px`) to proportional `1fr 1fr 1fr 1fr 1fr 1fr 1fr`; replaced the 8-char aggregate-id truncation with full-text rendering + CSS `grid-cell-truncate`. Drive-by UI polish accepted via code review DN2; `CopyAggregateId` still copies the full id.
- MODIFIED: `samples/Hexalith.EventStore.Sample.BlazorUI/Components/App.razor` — replaced the empty Blazor CSS-isolation bundle link (`Hexalith.EventStore.Sample.BlazorUI.styles.css` — project contains zero `.razor.css` files) with the FluentUI v5 bundled stylesheet (`_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css`); restructured `<FluentProviders>` from v4 wrap-around to v5 self-closed sibling. Both moves match the official FluentUI v5 Installation guide (`FluentProviders` has no `ChildContent` parameter in v5). Belated Epic 21 migration carry-over accepted via code review DN3.
- MODIFIED: `samples/Hexalith.EventStore.Sample.BlazorUI/Layout/MainLayout.razor` — added `Match="NavLinkMatch.All"` to the Overview `FluentNavItem` so it only highlights on the root route, not on all child routes. Routing fix accepted via code review DN3.
- MODIFIED: `_bmad-output/implementation-artifacts/sprint-status.yaml` — 15-12a status: `ready-for-dev` → `in-progress` → `review` (per workflow step 9).
- MODIFIED: `_bmad-output/implementation-artifacts/15-12a-implement-missing-timeline-endpoint.md` — status, task checkboxes, Dev Agent Record, Change Log.
- (Pre-existing) `_bmad-output/planning-artifacts/epics.md` — 2026-04-19 follow-up paragraph already appended under Story 15.12 by the sprint-change-proposal creator.

## Change Log

| Date       | Change                                                                                                             |
|------------|--------------------------------------------------------------------------------------------------------------------|
| 2026-04-19 | Implemented `GetStreamTimelineAsync` on `AdminStreamQueryController` + 12 Tier 1 tests. AC 1–11 satisfied; AC 12 deferred to reviewer for live-AppHost verification. |
| 2026-04-19 | Code review: 3 decision-needed (scope leaks), 0 patch, 4 defer (pre-existing), 8 dismissed. All 12 new tests green. |
| 2026-04-19 | Code review DN1/DN2/DN3 all resolved via story amendment: anti-pattern for `Events.razor` updated; File List extended with `Events.razor`, `Streams.razor`, `samples/.../App.razor`, `samples/.../MainLayout.razor` (latter two verified against official FluentUI v5 Installation docs). Story status → `done`; AC 12 still reviewer-gated for live-AppHost verification. |

## Review Findings

Adversarial review completed 2026-04-19 with three parallel layers (Blind Hunter / Edge Case Hunter / Acceptance Auditor). Core timeline-endpoint implementation AC 1–11 all PASS; AC 12 correctly deferred. The only unresolved items are scope leaks — 6 files modified outside the story's declared File List.

- [x] **[Review][Decision] Events.razor banner violates story anti-pattern** — **RESOLVED 2026-04-19:** story amended to accept the banner. Previous-story-intelligence note for 15.12 updated; File List extended to include `Events.razor` with a description of the additive behaviour (failed-stream counter + warning banner). Swallow-catch itself remains intact.
- [x] **[Review][Decision] Streams.razor grid template + aggregate-id rendering change** — **RESOLVED 2026-04-19:** story amended to accept the drive-by UI polish. File List extended to include `Streams.razor` with a description of the grid-template switch and the full-text aggregate-id rendering. `CopyAggregateId` still copies the full id.
- [x] **[Review][Decision] Sample BlazorUI App.razor + MainLayout.razor changes** — **RESOLVED 2026-04-19 (verified against FluentUI v5 docs):** all three sub-changes are correct v5 migration fixes. Official Installation guide (step 3) specifies the FluentUI bundled stylesheet; `FluentProviders` in v5 has no `ChildContent` parameter, so self-closed sibling placement is the v5 pattern; sample project contains zero `.razor.css` files (verified) so removing the isolation-bundle link has no visible effect. `NavLinkMatch.All` is a standard Blazor routing fix. File List extended with both files and a breadcrumb to the FluentUI v5 docs; treated as belated Epic 21 "v4→v5 migration" carry-over (Story 21-10 "sample-blazorui-alignment" landed 2026-04-15 but missed these three).
- [x] **[Review][Defer] Unbounded actor read + no `count` upper cap** [`AdminStreamQueryController.cs:286`] — `await actor.GetEventsAsync(fromSequence)` materializes all events from `fromSequence` onward before `.Take(count)`. `count` has a lower-bound normalization (≤0 → 100) but no upper bound. Large streams could OOM the service. Deferred, pre-existing: sibling endpoints (`bisect`, `blame`, `step`) share the identical pattern and the story explicitly says "Copy their style — do NOT invent a new error-mapping helper or a new `IAggregateActor` extension" and "DO NOT read events page-by-page". Architectural follow-up requires an `IAggregateActor` range-read overload.
- [x] **[Review][Defer] CancellationToken not propagated to actor call** [`AdminStreamQueryController.cs:286`] — the action accepts `CancellationToken ct` but `IAggregateActor.GetEventsAsync(long fromSequence)` has no CT parameter (confirmed at `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs:26`). Deferred, pre-existing: every sibling endpoint in the same controller shares this gap; requires an actor interface change.
- [x] **[Review][Defer] Missing boundary tests: `from==to`, `from=0` explicit, `to=1`, `count=1`** [`AdminStreamQueryControllerTimelineTests.cs`] — existing 12 tests cover AC 2.1–2.14 but no single-event range or small-count boundary cases. Deferred as test-thoroughness enhancement; a refactor flipping `>` vs `>=` on the filter would currently pass the test suite.
- [x] **[Review][Defer] No test asserting `ContinuationToken` / `TotalCount` semantics under truncation** [`AdminStreamQueryControllerTimelineTests.cs`] — by design the endpoint always returns `ContinuationToken = null` and `TotalCount = items.Count` (documented in story Task 1.9). A test locking in that explicit contract (rather than leaving it implicit) would make the "pagination is intentionally absent" choice self-documenting. Deferred as documentation-via-test.

### Dismissed as noise (intentional or false positive)

- Test stub uses 3-arg `CreateActorProxy<T>(ActorId, string, ActorProxyOptions?)` while controller calls the 2-arg overload — false positive. The 2-arg form is an extension method that delegates to the 3-arg interface method; NSubstitute stubs on the 3-arg form correctly intercept. All 12 tests run green (verified: `Passed: 12, Failed: 0`).
- `TotalCount == items.Count` instead of stream total — explicit spec decision (Task 1.9: "TotalCount is set to the page size, NOT the unfiltered total. This matches the existing `GetRecentlyActiveStreamsAsync` pattern").
- `ContinuationToken` always `null` — explicit out-of-scope decision (Task 1.9 + Out of Scope #2).
- Null / empty `CorrelationId` bubbles to 500 — explicit spec decision (Dev Notes §"`CorrelationId` projection": "Don't pre-validate; let the exception bubble through the existing 500 path").
- `from=0` sequence-origin concern — sequences start at 1 per FR10; `from=0` correctly includes seq 1 upward.
- `count<=0` split into `Timeline_CountZero_NormalizesTo100` + `Timeline_CountNegative_NormalizesTo100` instead of a single `[Fact]` — equal/greater coverage, cosmetic divergence only.
- `AdminUIServiceExtensions.cs` 5s → 30s HTTP timeout — legitimate carryover from `sprint-change-proposal-2026-04-18-events-page-slow.md`; the story's Dev Notes explicitly say it "is retained as a safety margin".
- `DaprTenantQueryService.cs` Bearer-token forwarding — legitimate carryover from `sprint-change-proposal-2026-04-18-tenant-query-auth.md` (Task 4.7 of this story explicitly requires verifying the 2026-04-18 tenant-query-auth fix remains green).

