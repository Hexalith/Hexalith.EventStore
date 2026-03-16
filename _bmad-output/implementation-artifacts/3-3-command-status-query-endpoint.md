# Story 3.3: Command Status Query Endpoint

Status: ready-for-dev

## Story

As an API consumer,
I want to check the processing status of a submitted command,
So that I can track the command lifecycle asynchronously.

## Acceptance Criteria

1. **Given** a previously submitted command with a correlation ID,
   **When** `GET /api/v1/commands/status/{correlationId}` is called,
   **Then** the system returns the current lifecycle state with timestamp (FR5, UX-DR14)
   **And** the response includes the 8-state lifecycle model (Received through Completed/Rejected/PublishFailed/TimedOut).

2. **Given** a status query for a non-existent correlation ID,
   **When** the endpoint is called,
   **Then** the system returns `404 Not Found` with RFC 7807 ProblemDetails.

3. All existing Tier 1 + Tier 2 tests pass. New tests verify RFC 7807 compliance and response format.

## Implementation State: VERIFICATION + ALIGNMENT STORY

The command status query endpoint is **fully implemented and tested**. This story verifies existing behavior against AC and aligns ProblemDetails responses to the same standards established by Stories 2.5 and 3.2.

**CRITICAL: Verify from source code, not from this story.** Read each `.cs` file directly for every PASS/FAIL verdict. Do NOT mark tasks PASS based on story descriptions.

**Gap-filling is authorized:** Writing new tests or modifying code to close gaps IS part of verification scope.

**Conflict resolution policy:** If existing code conflicts with AC, the AC takes precedence.

### Scope Boundary

This story covers the `GET /api/v1/commands/status/{correlationId}` endpoint behavior:
- 200 OK response format and headers
- 404 Not Found ProblemDetails for non-existent correlation IDs
- 403 Forbidden ProblemDetails for missing tenant claims
- 400 Bad Request ProblemDetails for invalid correlationId parameter
- Tenant-scoped status queries (SEC-3)

Do NOT modify:
- Command submission endpoint (Story 3.1)
- Validation error responses (Story 3.2)
- 401/409/503 error responses (Story 3.5)
- OpenAPI/Swagger documentation (Story 3.6)
- Status write logic in `SubmitCommandHandler` or `AggregateActor`

### Definition of Done

1. All verification subtasks have PASS or FIXED annotations in Completion Notes
2. All existing + new tests pass (diff against baseline shows no regressions)
3. ProblemDetails responses use `https://hexalith.io/problems/*` type URIs (E3)
4. ProblemDetails include `correlationId` extension; `tenantId` intentionally omitted (no single tenant context at query time — see Task 5.2)
5. `Retry-After` header is conditional: present for non-terminal statuses, absent for terminal statuses
6. No event sourcing terminology in any error response (E6)
7. `application/problem+json` content type on all error responses (E1)
8. Dev Agent Record populated

## Tasks / Subtasks

### Part A: Verify endpoint behavior and response format

- [ ] Task 1: Verify 200 OK response format (AC: #1)
  - [ ] 1.1 Read `src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs` fully
  - [ ] 1.2 Confirm `GET /api/v1/commands/status/{correlationId}` returns `200 OK` with `CommandStatusResponse` body
  - [ ] 1.3 Confirm `CommandStatusResponse` includes all 8 lifecycle states from `CommandStatus` enum: Received(0), Processing(1), EventsStored(2), EventsPublished(3), Completed(4), Rejected(5), PublishFailed(6), TimedOut(7)
  - [ ] 1.4 Confirm response includes `correlationId`, `status` (string), `statusCode` (int), `timestamp`, and terminal-state-specific fields (`aggregateId`, `eventCount`, `rejectionEventType`, `failureReason`, `timeoutDuration`)
  - [ ] 1.5 Confirm `timeoutDuration` is serialized as ISO 8601 duration string via `XmlConvert.ToString()` (e.g., `"PT30S"`)

- [ ] Task 2: Verify and fix `Retry-After` header behavior (AC: #1)
  - [ ] 2.1 Current code ALWAYS sets `Retry-After: 1` on 200 responses (line 93). For terminal statuses (Completed, Rejected, PublishFailed, TimedOut) this is misleading because the status will not change
  - [ ] 2.2 Fix: Make `Retry-After` conditional. Only include for non-terminal statuses (Received, Processing, EventsStored, EventsPublished). Terminal status responses should NOT include `Retry-After` — the consumer should stop polling
  - [ ] 2.3 Terminal statuses are those with `CommandStatus >= CommandStatus.Completed` (values 4-7). Non-terminal are values 0-3.

- [ ] Task 3: Verify tenant-scoped queries (SEC-3) (AC: #1, #2)
  - [ ] 3.1 Confirm controller extracts `eventstore:tenant` claims from JWT
  - [ ] 3.2 Confirm controller iterates all authorized tenants, querying `ICommandStatusStore.ReadStatusAsync()` for each
  - [ ] 3.3 Confirm tenant mismatch returns 404 (not 403) — per SEC-3, unauthorized tenants see "not found" not "forbidden"
  - [ ] 3.4 Confirm no tenant claims returns 403 Forbidden

### Part B: Align ProblemDetails to project standards

- [ ] Task 4: Fix ProblemDetails `type` URIs (E3)
  - [ ] 4.1 Read `CommandStatusController.CreateProblemDetails()` helper. Currently uses `"https://tools.ietf.org/html/rfc9457#section-3"` for all errors
  - [ ] 4.2 Change the 404 `type` to `"https://hexalith.io/problems/command-status-not-found"` — specific URI that uniquely identifies this error category per E3
  - [ ] 4.3 Change the 403 `type` to `"https://hexalith.io/problems/forbidden"` — standard forbidden category
  - [ ] 4.4 Change the 400 `type` to `"https://hexalith.io/problems/bad-request"` — standard bad request category
  - [ ] 4.5 **Do NOT change ConcurrencyConflictExceptionHandler type URI** — that is Story 3.5 scope

- [ ] Task 5: Confirm `tenantId` omission from ProblemDetails is correct (D5)
  - [ ] 5.1 The `CreateProblemDetails` helper currently only includes `correlationId`. D5 says both `correlationId` and `tenantId` should be present in ProblemDetails extensions.
  - [ ] 5.2 **Decision (from party-mode review):** `tenantId` is correctly OMITTED from all CommandStatusController error responses. Rationale: the status controller iterates multiple tenant claims to find a match — there is no single tenant context at query time. Specifically: 400 = pre-validation (no tenant), 403 = no tenant claims exist, 404 = status not found in any authorized tenant. No single tenant to report.
  - [ ] 5.3 **D5 Exception Documentation (REQUIRED in Completion Notes):** Record that `tenantId` is intentionally absent from all CommandStatusController ProblemDetails as a documented D5 exception. Reason: status queries iterate multiple tenant claims with no single tenant context. Unlike command submission errors (which have a known tenant from the request body), status queries cannot attribute errors to a specific tenant. This prevents future reviewers from flagging the omission as a bug.
  - [ ] 5.4 No code change needed — verify current behavior matches the decision and document the rationale.

- [ ] Task 6: Verify no event sourcing terminology (E6)
  - [ ] 6.1 Scan all error messages in `CommandStatusController` for forbidden terms: "aggregate", "event stream", "actor", "DAPR", "sidecar"
  - [ ] 6.2 Scan `CommandStatusResponse` property names and values — `aggregateId` is intentional in the response (it's the domain concept), but verify error messages never reference internal implementation
  - [ ] 6.3 Verify structured log messages use parameterized templates (no interpolation that could leak terms into responses)

### Part C: Verify and extend test coverage

- [ ] Task 7: Establish test baseline (AC: #3)
  - [ ] 7.1 Run ALL Tier 1 tests (`Contracts.Tests`, `Client.Tests`, `Sample.Tests`, `Testing.Tests`) + ALL Tier 2 tests (`Server.Tests`) BEFORE any code changes and record exact pass/fail counts per project
  - [ ] 7.2 Run `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~CommandStatus"` specifically to get focused baseline
  - [ ] 7.3 Save both full and filtered baselines for diff comparison after changes

- [ ] Task 8: Update existing Tier 2 tests for ProblemDetails format (AC: #2, #3)
  - [ ] 8.1 Read `tests/Hexalith.EventStore.Server.Tests/Commands/CommandStatusControllerTests.cs`
  - [ ] 8.2 Update `GetStatus_NonExistentCorrelationId_Returns404ProblemDetails`: add assertions for `type` URI = `"https://hexalith.io/problems/command-status-not-found"` and `title` = `"Not Found"`
  - [ ] 8.3 Update `GetStatus_NoTenantClaims_Returns403ProblemDetails`: add assertion for `type` URI = `"https://hexalith.io/problems/forbidden"`
  - [ ] 8.4 Update `GetStatus_WhitespaceCorrelationId_Returns400`: add assertion for `type` URI = `"https://hexalith.io/problems/bad-request"`
  - [ ] 8.5 Add assertion: `correlationId` extension is present in all ProblemDetails responses

- [ ] Task 9: Add Tier 2 tests for `Retry-After` conditionality (AC: #1)
  - [ ] 9.1 Add test: non-terminal status (e.g., `Received`) → 200 with `Retry-After: 1` header
  - [ ] 9.2 Add test: terminal status (e.g., `Completed`) → 200 WITHOUT `Retry-After` header
  - [ ] 9.3 Add test: terminal status `Rejected` → 200 WITHOUT `Retry-After` header
  - [ ] 9.4 Add test: terminal status `TimedOut` → 200 WITHOUT `Retry-After` header
  - [ ] 9.5 Add test: terminal status `PublishFailed` → 200 WITHOUT `Retry-After` header (covers all 4 terminal states)
  - **Header assertion pattern:** The existing `SetupHttpContext` helper wires `DefaultHttpContext` to the controller via `ControllerContext`. Response headers are accessible at `_controller.HttpContext.Response.Headers["Retry-After"]`. Assert `.ToString()` equals `"1"` for presence, and use `ContainsKey("Retry-After").ShouldBeFalse()` for absence.

- [ ] Task 10: Verify Tier 3 integration tests still pass (AC: #3)
  - [ ] 10.1 Read `tests/Hexalith.EventStore.IntegrationTests/CommandApi/CommandStatusIntegrationTests.cs`
  - [ ] 10.2 If Tier 3 tests assert on `Retry-After` header presence for terminal statuses, update them to reflect conditional behavior
  - [ ] 10.3 Verify `GetStatus_ResponseIncludesCorrelationIdInProblemDetails` test passes with new `type` URI
  - [ ] 10.4 Update Tier 3 tests to assert new `type` URIs if they currently assert on the old RFC URI

- [ ] Task 11: Run full test suite (AC: #3)
  - [ ] 11.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings
  - [ ] 11.2 Tier 1: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` + `Client.Tests` + `Sample.Tests` + `Testing.Tests`
  - [ ] 11.3 Tier 2: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~CommandStatus"`
  - [ ] 11.4 Tier 3 (if Docker available): `dotnet test tests/Hexalith.EventStore.IntegrationTests/ --filter "FullyQualifiedName~CommandStatus"`
  - [ ] 11.5 Diff results against pre-change baseline to confirm zero regressions

## Dev Notes

### This is a verification + alignment story

The command status query endpoint is fully implemented with comprehensive test coverage. The core work is:
1. Verify response format against AC
2. Fix ProblemDetails `type` URIs to use `https://hexalith.io/problems/*` pattern (E3)
3. Make `Retry-After` header conditional on terminal vs non-terminal status
4. Extend test assertions for RFC 7807 compliance

### Existing Infrastructure (DO NOT rebuild)

| Component | File | Status |
|-----------|------|--------|
| `CommandStatusController` | `src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs` | Implemented — needs `type` URI and `Retry-After` fixes |
| `ICommandStatusStore` | `src/Hexalith.EventStore.Server/Commands/ICommandStatusStore.cs` | Implemented — no changes needed |
| `DaprCommandStatusStore` | `src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs` | Implemented — no changes needed |
| `CommandStatusRecord` | `src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs` | Implemented — no changes needed |
| `CommandStatusResponse` | `src/Hexalith.EventStore.CommandApi/Models/CommandStatusResponse.cs` | Implemented — no changes needed |
| `CommandStatusConstants` | `src/Hexalith.EventStore.Server/Commands/CommandStatusConstants.cs` | Implemented — no changes needed |
| `CommandStatusOptions` | `src/Hexalith.EventStore.Server/Commands/CommandStatusOptions.cs` | Implemented — no changes needed |
| `CommandStatus` enum | `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs` | Implemented — 8 states with stable int assignments |
| `InMemoryCommandStatusStore` | `src/Hexalith.EventStore.Testing/Fakes/InMemoryCommandStatusStore.cs` | Test fake — no changes needed |

### Response Format (200 OK)

The `CommandStatusResponse` returned by the endpoint:

```json
{
    "correlationId": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
    "status": "Received",
    "statusCode": 0,
    "timestamp": "2026-03-16T12:00:00Z",
    "aggregateId": "agg-001",
    "eventCount": null,
    "rejectionEventType": null,
    "failureReason": null,
    "timeoutDuration": null
}
```

Terminal statuses include additional fields:
- `Completed`: `eventCount` populated (e.g., `5`)
- `Rejected`: `rejectionEventType` for domain rejections OR `failureReason` for infrastructure rejections
- `PublishFailed`: `failureReason` populated
- `TimedOut`: `timeoutDuration` as ISO 8601 (e.g., `"PT30S"`)

### ProblemDetails Target Format (404 Not Found)

```json
{
    "type": "https://hexalith.io/problems/command-status-not-found",
    "title": "Not Found",
    "status": 404,
    "detail": "No command status found for correlation ID '01ARZ3NDEKTSV4RRFFQ69G5FAV'.",
    "instance": "/api/v1/commands/status/01ARZ3NDEKTSV4RRFFQ69G5FAV",
    "correlationId": "01ARZ3NDEKTSV4RRFFQ69G5FAV"
}
```

### Retry-After Conditionality

Current behavior: `Retry-After: 1` on ALL 200 responses.

Target behavior: `Retry-After: 1` only for non-terminal statuses (Received=0, Processing=1, EventsStored=2, EventsPublished=3). Terminal statuses (Completed=4, Rejected=5, PublishFailed=6, TimedOut=7) should NOT include `Retry-After` because the status will not change — the consumer should stop polling.

Implementation hint:
```csharp
// Terminal statuses are values >= Completed (4)
bool isTerminal = record.Status >= CommandStatus.Completed;
if (!isTerminal)
{
    Response.Headers["Retry-After"] = "1";
}
```

### Current `CreateProblemDetails` Helper (BEFORE changes)

```csharp
private ObjectResult CreateProblemDetails(int statusCode, string title, string detail, string correlationId) {
    var problemDetails = new ProblemDetails {
        Status = statusCode,
        Title = title,
        Type = "https://tools.ietf.org/html/rfc9457#section-3",  // WRONG: needs specific URI per E3
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions = {
            ["correlationId"] = correlationId,
            // tenantId intentionally omitted — no single tenant context at query time (D5 exception, see Task 5.2)
        },
    };
    Response.ContentType = "application/problem+json";
    return new ObjectResult(problemDetails) { StatusCode = statusCode };
}
```

Refactor to accept `type` URI (no `tenantId` — see Task 5.2 decision):
```csharp
private ObjectResult CreateProblemDetails(
    int statusCode, string type, string title, string detail,
    string correlationId)
```

Each call site passes its specific `type` URI:
- 400 call site: `"https://hexalith.io/problems/bad-request"`
- 403 call site: `"https://hexalith.io/problems/forbidden"`
- 404 call site: `"https://hexalith.io/problems/command-status-not-found"`

### Architecture Compliance (MUST FOLLOW)

| Rule | Requirement | Relevance |
|------|-------------|-----------|
| D2 | Command status key: `{tenant}:{correlationId}:status` | Already implemented in `CommandStatusConstants.BuildKey` |
| D5 | RFC 7807 ProblemDetails + extensions (correlationId, tenantId) | `correlationId` present; `tenantId` intentionally omitted — no single tenant context at query time |
| Rule 7 | ProblemDetails for ALL API error responses | All paths must comply |
| Rule 12 | Status writes are advisory (failure never blocks) | N/A for read path |
| Rule 13 | No stack traces in production error responses | Verify exception handler re-throws |
| E1 | Every error is `application/problem+json` | Content type verification |
| E3 | `type` is stable URI that uniquely identifies error category | Needs `https://hexalith.io/problems/*` URIs |
| E4 | `correlationId` included when command entered pipeline | Already present |
| E6 | No event sourcing terminology | Scan error messages |
| SEC-3 | Command status queries are tenant-scoped | Already implemented — tenant mismatch returns 404 |

### Testing Approach

**Tier 2 tests** (in `tests/Hexalith.EventStore.Server.Tests/Commands/CommandStatusControllerTests.cs`) are the primary target for this story. They test controller logic directly with `InMemoryCommandStatusStore` and `DefaultHttpContext` — no Docker or DAPR required.

**Tier 3 tests** (in `tests/Hexalith.EventStore.IntegrationTests/CommandApi/CommandStatusIntegrationTests.cs`) provide HTTP-level verification through `WebApplicationFactory`. Update assertions if they depend on old ProblemDetails format or `Retry-After` behavior.

**Test commands:**
```bash
# Baseline -- run BEFORE changes
dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~CommandStatus" 2>&1 | tee baseline-tier2.txt

# Tier 1
dotnet test tests/Hexalith.EventStore.Contracts.Tests/
dotnet test tests/Hexalith.EventStore.Client.Tests/
dotnet test tests/Hexalith.EventStore.Sample.Tests/
dotnet test tests/Hexalith.EventStore.Testing.Tests/

# Tier 3 (requires full DAPR + Docker)
dotnet test tests/Hexalith.EventStore.IntegrationTests/ --filter "FullyQualifiedName~CommandStatus"
```

### Previous Story Intelligence (Story 3.1)

- Story 3.1 is in `review` status. Completed verification with PASS on all tasks.
- `CommandsController.cs:92` was fixed for empty-string CorrelationId defaulting
- Test baseline: Tier 1: 656 passed/0 failed. Tier 2: 1299 passed/1 failed (pre-existing DaprSerialization test).
- 5 new integration tests added for CorrelationId defaulting and header format verification
- No structural changes to the controller beyond the one-line CorrelationId fix
- Pre-existing test fix: test requests were missing `messageId` field

### Previous Story Intelligence (Story 3.2)

- Story 3.2 is `ready-for-dev` (not yet implemented). It focuses on 400 validation errors.
- Defines the `https://hexalith.io/problems/validation-error` type URI pattern for validation-specific errors
- Introduces `ValidationProblemDetailsFactory` shared helper concept
- No dependency on this story — they can be developed in parallel

### Previous Story Intelligence (Story 2.5)

- `ConcurrencyConflictExceptionHandler` is the reference for `IExceptionHandler` patterns
- ProblemDetails extensions use flat string values (not nested objects)
- Advisory status writes follow Rule 12 (non-blocking)
- ConcurrencyConflictExceptionHandler also still uses RFC link as `type` URI — Story 3.5 will fix this

### Git Intelligence

Recent commits:
- `fd45dd0` feat: Implement Domain Processor State Rehydrator
- `b9a4e23` Refactor command handling and improve test assertions
- Build must produce zero warnings (`TreatWarningsAsErrors = true`)
- Tier 1 baseline: ~656 tests, Tier 2 baseline: ~1299 tests

### Key Files to Read and Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs` | **MODIFY** | Fix `type` URIs, conditional `Retry-After`, add `type` param to `CreateProblemDetails` |
| `tests/Hexalith.EventStore.Server.Tests/Commands/CommandStatusControllerTests.cs` | **MODIFY** | Update assertions for new ProblemDetails format + add `Retry-After` conditionality tests |
| `tests/Hexalith.EventStore.IntegrationTests/CommandApi/CommandStatusIntegrationTests.cs` | **MODIFY** | Update assertions if they depend on old format or `Retry-After` |
| `src/Hexalith.EventStore.CommandApi/Models/CommandStatusResponse.cs` | READ ONLY | Understand response model |
| `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs` | READ ONLY | Understand 8-state enum |
| `src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs` | READ ONLY | Understand record structure |
| `src/Hexalith.EventStore.Server/Commands/ICommandStatusStore.cs` | READ ONLY | Store interface |
| `src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs` | READ ONLY | DAPR implementation |
| `src/Hexalith.EventStore.Server/Commands/CommandStatusConstants.cs` | READ ONLY | Key pattern: `{tenant}:{correlationId}:status` |
| `src/Hexalith.EventStore.Testing/Fakes/InMemoryCommandStatusStore.cs` | READ ONLY | Test fake with TTL simulation |
| `src/Hexalith.EventStore.CommandApi/ErrorHandling/ConcurrencyConflictExceptionHandler.cs` | READ ONLY | Reference for ProblemDetails structure (extensions, content type, response writing). **WARNING:** This handler still uses the WRONG `type` URI (`tools.ietf.org/...`) — do NOT copy its `Type` value. Copy its structure only. Story 3.5 will fix its URI. |
| `tests/Hexalith.EventStore.IntegrationTests/Security/CommandStatusIsolationTests.cs` | READ ONLY | Tenant isolation tests |

### Project Structure Notes

- CommandApi at `src/Hexalith.EventStore.CommandApi/` — hosts all HTTP-facing code
- Package flow: Contracts <- Server <- CommandApi
- File-scoped namespaces, Allman braces, `_camelCase` private fields
- Nullable enabled, implicit usings, TreatWarningsAsErrors
- 4 spaces indentation, CRLF, UTF-8

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 3, Story 3.3]
- [Source: _bmad-output/planning-artifacts/architecture.md#D2, D5, SEC-3, Rule 7, Rule 13]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#Act 2, UX-DR14, UX-DR15, Enforcement Rules E1-E6]
- [Source: _bmad-output/planning-artifacts/prd.md#FR5]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Models/CommandStatusResponse.cs]
- [Source: src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs]
- [Source: src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs]
- [Source: src/Hexalith.EventStore.Server/Commands/ICommandStatusStore.cs]
- [Source: src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Commands/CommandStatusControllerTests.cs]
- [Source: tests/Hexalith.EventStore.IntegrationTests/CommandApi/CommandStatusIntegrationTests.cs]
- [Source: _bmad-output/implementation-artifacts/3-1-command-submission-endpoint.md -- Story 3.1 learnings]
- [Source: _bmad-output/implementation-artifacts/3-2-command-validation-and-400-error-responses.md -- Story 3.2 context]
- [Source: _bmad-output/implementation-artifacts/2-5-duplicate-command-detection.md -- Story 2.5 learnings]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
