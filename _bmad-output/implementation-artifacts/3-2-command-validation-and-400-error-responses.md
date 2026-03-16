# Story 3.2: Command Validation & 400 Error Responses

Status: ready-for-dev

## Story

As an API consumer,
I want clear, field-level validation errors when my command is malformed,
So that I know exactly what to fix without guessing.

## Acceptance Criteria

1. **Given** a command missing required fields or with invalid format,
   **When** submitted to the command endpoint,
   **Then** the system returns `400 Bad Request` with RFC 7807 `application/problem+json` (UX-DR1)
   **And** `type` is `https://hexalith.io/problems/validation-error`
   **And** `errors` object uses JSON path keys (e.g., `payload.amount`) with human-readable messages (UX-DR3)
   **And** `correlationId` is present in ProblemDetails extensions (UX-DR2)
   **And** no event sourcing terminology appears in the response (UX-DR6).

2. **Given** extension metadata that fails security sanitization (XSS, SQL injection, LDAP, path traversal),
   **When** submitted,
   **Then** the system returns `400 Bad Request` with the same RFC 7807 format as AC #1
   **And** sanitization rejection reason is included in `detail` field.

3. All three 400-producing validation paths (ValidateModelFilter, ValidationExceptionHandler, Controller extension sanitization) produce an **identical RFC 7807 response shape**.

4. All existing Tier 1 + Tier 2 tests pass. New tests cover the standardized response format.

## Implementation State: ALIGNMENT STORY

The validation infrastructure is fully implemented and functional. This story **aligns the existing 400 response output** to the RFC 7807 shape specified by the UX design specification (Journey 6) and Epic 3 AC. The core work is standardizing ProblemDetails fields across three existing validation paths.

**This is NOT a verification story** -- you must modify code to align the response format.

**BREAKING CHANGE:** This story intentionally changes the 400 error response shape. The `validationErrors` array and `errorsDictionary` extensions are removed and replaced by a single `errors` dictionary with camelCase keys. All existing tests that assert on the old shape MUST be updated to match the new format. This is a deliberate contract change to comply with UX spec Journey 6 and enforcement rules E1/E8.

### Scope Boundary

This story covers **400 Bad Request validation responses ONLY**. Do NOT modify:
- 401/403 responses (Story 3.5)
- 409 Conflict responses (Story 2.5 -- done)
- 503 Service Unavailable responses (Story 3.5)
- 500 Internal Server Error responses (existing)
- Swagger/OpenAPI documentation (Story 3.6)

### Definition of Done

1. All three validation paths return identical RFC 7807 shape per AC #1
2. `type` URI is `https://hexalith.io/problems/validation-error` (not the RFC link)
3. `title` is `"Command Validation Failed"`
4. `errors` object is a flat dictionary with JSON path keys and human-readable messages
5. `correlationId` and `tenantId` extensions are present in all 400 responses (per D5)
6. No event sourcing terminology in any validation error message
7. All existing + new tests pass (zero regressions)
8. Dev Agent Record populated

## Tasks / Subtasks

### Prerequisites

- **DAPR slim init required** for Tier 2 tests: run `dapr init --slim` before starting any task that runs Server.Tests
- **If Story 3.1 has modified target files:** Read the CURRENT source code before applying changes. Do not assume the code state described in Dev Notes is still accurate

### Part A: Create shared factory FIRST

- [ ] Task 1: Create shared ProblemDetails factory (AC: #3)
  - [ ] 1.1 Create `src/Hexalith.EventStore.CommandApi/ErrorHandling/ValidationProblemDetailsFactory.cs` as a static class with two overloads:
    - `Create(string detail, IEnumerable<ValidationFailure> failures, string? correlationId, string? tenantId)` -- for paths 1 and 2 (FluentValidation errors). Internally applies camelCase conversion via `JsonNamingPolicy.CamelCase.ConvertName()` and `"; "` joining for multiple messages per property
    - `Create(string detail, Dictionary<string, string> errors, string? correlationId, string? tenantId)` -- for path 3 (pre-built errors dict, e.g., extension sanitization)
    - Both overloads return `ProblemDetails` with identical structure. Both always include `tenantId` (even if null)
  - [ ] 1.2 The factory owns: `type` = `"https://hexalith.io/problems/validation-error"`, `title` = `"Command Validation Failed"`, `status` = 400, camelCase key transformation -- single source of truth prevents future drift
  - [ ] 1.3 Include XML documentation on the class and both overloads

### Part B: Wire each validation path to the shared factory

- [ ] Task 2: Wire `ValidateModelFilter.cs` to factory (AC: #1, #3)
  - [ ] 2.1 Read `src/Hexalith.EventStore.CommandApi/Filters/ValidateModelFilter.cs` fully
  - [ ] 2.2 Replace inline ProblemDetails construction with `ValidationProblemDetailsFactory.Create(detail, failures, correlationId, tenantId)`. Remove both `validationErrors` array AND `errorsDictionary` extensions -- the factory produces the single `errors` dict
  - [ ] 2.3 Verify `detail` field provides a human-readable summary (e.g., "The command has N validation error(s). See 'errors' for specifics." -- note: say "command", not "command payload", since errors can be on any field, not just payload)
  - [ ] 2.4 Verify response content type is `application/problem+json` (not `application/json`). The filter currently sets this via `ContentTypes.Add(...)` -- confirm this is preserved after wiring to the factory
  - [ ] 2.5 Verify no event sourcing terminology in error messages (aggregate, event stream, actor, DAPR)

- [ ] Task 3: Wire `ValidationExceptionHandler.cs` to factory (AC: #1, #3)
  - [ ] 3.1 Read `src/Hexalith.EventStore.CommandApi/ErrorHandling/ValidationExceptionHandler.cs` fully
  - [ ] 3.2 Replace inline ProblemDetails construction with `ValidationProblemDetailsFactory.Create(detail, exception.Errors, correlationId, tenantId)`. Remove the `validationErrors` array extension
  - [ ] 3.3 Verify `detail` field follows same pattern as Task 2.3
  - [ ] 3.4 Verify response content type is `application/problem+json`. `WriteAsJsonAsync` defaults to `application/json` -- you must explicitly set `httpContext.Response.ContentType = "application/problem+json"` before writing
  - [ ] 3.5 Verify no event sourcing terminology

- [ ] Task 4: Wire Controller extension sanitization to factory (AC: #2, #3)
  - [ ] 4.1 Read `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` lines around extension sanitization (search `sanitizeResult`)
  - [ ] 4.2 Replace the inline `Problem()` call with `ValidationProblemDetailsFactory.Create(detail, errors, correlationId, tenantId)`. Pass `detail` = sanitization rejection reason, `errors` = `{ "extensions": sanitizeResult.RejectionReason }`, plus `correlationId` and `tenantId` from request context
  - [ ] 4.3 Verify the response content type is `application/problem+json` (not `application/json`)

### Part C: Update/add tests

### Part C: Update/add tests

- [ ] Task 5: Update existing validation tests (AC: #4)
  - [ ] 5.1 Read `tests/Hexalith.EventStore.IntegrationTests/CommandApi/ValidationTests.cs`. **Before modifying:** identify and list all assertions that reference `validationErrors`, `errorsDictionary`, or the old `type` URI. These are EXPECTED breakages from the BREAKING CHANGE, not regressions. Record the count in Completion Notes
  - [ ] 5.2 Update assertions for `type` URI: assert `"https://hexalith.io/problems/validation-error"` (not RFC link)
  - [ ] 5.3 Update assertions for `title`: assert `"Command Validation Failed"`
  - [ ] 5.4 Add assertions for `errors` dictionary structure: keys are property names, values are human-readable strings
  - [ ] 5.5 Add assertions for `correlationId` and `tenantId` presence in extensions
  - [ ] 5.6 **Tier 3 fallback:** If Docker is unavailable and these integration tests cannot run, update assertions based on code reading and verify the format is correct via Tier 2 `ValidationExceptionHandlerTests` (Task 7) instead. Document Tier 3 as a verification gap in Completion Notes

- [ ] Task 6: Add new tests for format consistency (AC: #1, #3)
  - [ ] 6.1 Test: missing `messageId` field → 400 with `errors` containing camelCase key `"messageId"`
  - [ ] 6.2 Test: invalid `commandType` format → 400 with `errors` containing camelCase key `"commandType"`
  - [ ] 6.3 Test: extension sanitization failure (XSS attempt) → 400 with same `type` and `title` as structural validation
  - [ ] 6.4 Test: multiple validation errors → 400 with multiple keys in `errors` dictionary
  - [ ] 6.5 Verify the response does NOT contain `validationErrors` array OR `errorsDictionary` extension (only flat `errors` dict)

- [ ] Task 7: Add Tier 2 unit tests for ValidationExceptionHandler (AC: #3, #4)
  - [ ] 7.1 Create `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/ValidationExceptionHandlerTests.cs` following the `ConcurrencyConflictExceptionHandlerTests.cs` pattern (NSubstitute + DefaultHttpContext + MemoryStream)
  - [ ] 7.2 Test: FluentValidation.ValidationException with single error → 400 with correct `type`, `title`, camelCase `errors` key
  - [ ] 7.3 Test: FluentValidation.ValidationException with multiple errors on same property → messages joined with `"; "`
  - [ ] 7.4 Test: non-ValidationException → handler returns false (does not handle)
  - [ ] 7.5 Test: `correlationId` and `tenantId` present in extensions
  - [ ] 7.6 Test: response does NOT contain `validationErrors` or `errorsDictionary` extensions
  - [ ] 7.7 Test: response content type is `application/problem+json` (not `application/json`)
  - [ ] 7.8 These Tier 2 tests run without Docker, providing format verification even when Tier 3 is unavailable

- [ ] Task 8: Run full test suite (AC: #4)
  - [ ] 8.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- zero warnings
  - [ ] 8.2 Tier 1: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/` + `Client.Tests` + `Sample.Tests` + `Testing.Tests`
  - [ ] 8.3 Tier 2: `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~Validator|FullyQualifiedName~Validation"`
  - [ ] 8.4 Tier 3 (if Docker available): `dotnet test tests/Hexalith.EventStore.IntegrationTests/ --filter "FullyQualifiedName~Validation"`
  - [ ] 8.5 Diff results against pre-change baseline to confirm zero regressions

## Dev Notes

### Target Response Shape (UX Journey 6)

Every 400 response from all validation paths MUST match this exact shape:

```json
{
    "type": "https://hexalith.io/problems/validation-error",
    "title": "Command Validation Failed",
    "status": 400,
    "detail": "The command has 2 validation error(s). See 'errors' for specifics.",
    "instance": "/api/v1/commands",
    "correlationId": "01JQXYZ1234567890ABCDEF",
    "tenantId": "tenant-acme",
    "errors": {
        "payload.destinationAccount": "Required field is missing.",
        "payload.amount": "Required field is missing."
    }
}
```

Source: `_bmad-output/planning-artifacts/ux-design-specification.md` Journey 6.

### Three Validation Paths That Produce 400 Responses

| # | Component | File | Trigger | Current Issues |
|---|-----------|------|---------|----------------|
| 1 | `ValidateModelFilter` | `src/Hexalith.EventStore.CommandApi/Filters/ValidateModelFilter.cs` | FluentValidation on HTTP DTO (pre-controller) | Wrong `type` URI (RFC link), wrong `title`, has both `errors` dict AND `validationErrors` array |
| 2 | `ValidationExceptionHandler` | `src/Hexalith.EventStore.CommandApi/ErrorHandling/ValidationExceptionHandler.cs` | FluentValidation from MediatR pipeline (post-controller) | Wrong `type` URI, wrong `title`, uses `validationErrors` array instead of `errors` dict |
| 3 | Controller inline | `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` | ExtensionMetadataSanitizer rejection | Uses generic `Problem()` helper, different format |

**Path 1 (ValidateModelFilter)** fires first for structural issues (missing fields, invalid format). Most consumer-facing validation errors come from here.

**Path 2 (ValidationExceptionHandler)** is defense-in-depth for commands that bypass the action filter (e.g., direct MediatR send). Same validators, different error path.

**Path 3 (Controller inline)** handles security sanitization of extension metadata, which runs after structural validation passes.

### Target Extensions Shape (all three paths)

All paths must produce these ProblemDetails extensions (and NO others):
- `"correlationId"`: string (from HttpContext.Items)
- `"tenantId"`: string or null (always present, even if null)
- `"errors"`: flat `Dictionary<string, string>` with camelCase keys and `"; "`-joined messages

Legacy extensions to **REMOVE**: `validationErrors` (array), `errorsDictionary` (Dict<string, string[]>). The factory handles this -- callers just pass validation failures or a pre-built dict.

### JSON Property Naming for `errors` Keys -- camelCase REQUIRED

FluentValidation uses PascalCase property names (e.g., `MessageId`, `CommandType`, `Payload`). The UX spec Journey 6 and AC #1 use camelCase JSON paths (e.g., `payload.amount`, `messageId`). Enforcement rule E8 mandates JSON path notation.

**Decision: Convert error keys to camelCase.** The consumer sees camelCase in their request JSON (ASP.NET Core default serializer). Error keys must match what they sent. Returning PascalCase `MessageId` when the consumer sent `messageId` is disorienting.

**Implementation:** Apply `JsonNamingPolicy.CamelCase.ConvertName()` to each FluentValidation `PropertyName` before inserting into the `errors` dictionary. This is a one-liner per path. Example:

```csharp
var errors = failures
    .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
    .ToDictionary(
        g => g.Key,
        g => string.Join("; ", g.Select(e => e.ErrorMessage)));
```

For the Controller extension sanitization path, use `"extensions"` as the key (already camelCase).

**`tenantId` null handling:** If `tenantId` cannot be extracted (e.g., request deserialization failed before the tenant field was parsed), include `"tenantId": null` in extensions rather than omitting the key. This maintains a consistent extension shape across all 400 responses. The shared factory should accept `string? tenantId` and always include it.

**Future-proofing note:** Current validators only produce single-segment property names (e.g., `MessageId`). If child validators with dot-notation paths are added later (e.g., `Extensions[0].Key`), `JsonNamingPolicy.CamelCase.ConvertName()` only converts the first character of the entire string. A future story may need to split on `.`, convert each segment, and rejoin. Do NOT implement this now -- it's out of scope.

### Extension Sanitization Error Format

The Controller currently returns:
```csharp
return Problem(statusCode: 400, detail: sanitizeResult.RejectionReason);
```

This needs to become a full ProblemDetails with:
- `type`: `"https://hexalith.io/problems/validation-error"`
- `title`: `"Command Validation Failed"`
- `detail`: The sanitization rejection reason (human-readable)
- `errors`: `{ "extensions": sanitizeResult.RejectionReason }`
- `correlationId`: from HttpContext.Items
- `tenantId`: from request (per D5)

### Architecture Compliance (MUST FOLLOW)

| Rule | Requirement | Relevance |
|------|-------------|-----------|
| D5 | RFC 7807 ProblemDetails + extensions (correlationId, tenantId) | Core of this story |
| Rule 7 | ProblemDetails for ALL API error responses -- never custom shapes | All 3 paths must comply |
| Rule 13 | No stack traces in production error responses | Verify no stack traces leak |
| E1 | Every error is `application/problem+json` | Content type verification |
| E2 | `detail` written for the API consumer, not the developer | Human-readable messages |
| E3 | `type` is stable URI that uniquely identifies error category | `https://hexalith.io/problems/validation-error` |
| E4 | `correlationId` included when command entered pipeline (400 = yes) | Must be in extensions |
| E6 | No event sourcing terminology in error responses | Scan all error messages |
| E8 | Validation errors use JSON path notation in `errors` object | Property name keys |
| E10 | Error `type` URIs resolve to documentation | Out of scope (Story 3.6) |

### Testing Approach

**Tier 3 tests preferred** for HTTP-level response verification (content type headers, JSON structure). Existing `ValidationTests.cs` in `tests/Hexalith.EventStore.IntegrationTests/CommandApi/` already has 10+ validation tests -- update these to assert the new format.

**Tier 2 tests** for unit-level handler verification. `ConcurrencyConflictExceptionHandlerTests.cs` shows the established pattern for testing IExceptionHandler implementations -- follow the same NSubstitute + HttpContext mocking approach if adding unit tests for ValidationExceptionHandler.

**Test commands:**
```bash
# Baseline -- run BEFORE changes
dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~Validator|FullyQualifiedName~Validation" 2>&1 | tee baseline-tier2.txt

# Tier 1
dotnet test tests/Hexalith.EventStore.Contracts.Tests/
dotnet test tests/Hexalith.EventStore.Client.Tests/
dotnet test tests/Hexalith.EventStore.Sample.Tests/
dotnet test tests/Hexalith.EventStore.Testing.Tests/

# Tier 3 (requires full DAPR + Docker)
dotnet test tests/Hexalith.EventStore.IntegrationTests/ --filter "FullyQualifiedName~Validation"
```

**Known pre-existing test failures:** 4 SubmitCommandHandler NullRef, 1 validator, 10 auth integration. These are out-of-scope -- diff against baseline to isolate new failures.

### Cross-Story Notes

- **Story 3.1** (review): Focuses on happy path (202 Accepted). No implementation dependency -- can develop in parallel. If 3.1 modified target files, read current source before changes.
- **Story 3.6** (backlog): Must include the `errors` dictionary schema in the OpenAPI spec when implementing Swagger UI. The standardized error shape from this story becomes the API contract.

### Previous Story Intelligence (Story 2.5)

- `ConcurrencyConflictExceptionHandler` is the reference implementation for `IExceptionHandler` patterns in this codebase
- Exception handler tests use NSubstitute with `HttpContext` mocking, `DefaultHttpContext`, and `MemoryStream` for response body
- Advisory status writes follow Rule 12 (non-blocking)
- ProblemDetails extensions use flat string values, not nested objects
- Test file location mirrors source structure: `ErrorHandling/` → `Commands/` in Server.Tests

### Git Intelligence

Recent commits (Epic 1 complete, Epic 2 nearly done):
- `fd45dd0` feat: Implement Domain Processor State Rehydrator
- `b9a4e23` Refactor command handling and improve test assertions
- Build must produce zero warnings (`TreatWarningsAsErrors = true`)
- Tier 1 baseline: ~656 tests
- DAPR SDK pinned at 1.16.1 (not 1.17.0 per CLAUDE.md)

### Key Files to Read and Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/Hexalith.EventStore.CommandApi/ErrorHandling/ValidationProblemDetailsFactory.cs` | **CREATE** | Shared factory for consistent 400 ProblemDetails |
| `src/Hexalith.EventStore.CommandApi/Filters/ValidateModelFilter.cs` | **MODIFY** | Wire to shared factory |
| `src/Hexalith.EventStore.CommandApi/ErrorHandling/ValidationExceptionHandler.cs` | **MODIFY** | Standardize ProblemDetails format |
| `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` | **MODIFY** | Align extension sanitization 400 response |
| `tests/Hexalith.EventStore.IntegrationTests/CommandApi/ValidationTests.cs` | **MODIFY** | Update assertions for new format |
| `src/Hexalith.EventStore.CommandApi/Validation/SubmitCommandRequestValidator.cs` | READ ONLY | Understand existing validation rules |
| `src/Hexalith.EventStore.CommandApi/Validation/ExtensionMetadataSanitizer.cs` | READ ONLY | Understand sanitization flow |
| `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` | READ ONLY | Handler registration order |
| `src/Hexalith.EventStore.CommandApi/Pipeline/ValidationBehavior.cs` | READ ONLY | MediatR pipeline validation flow |
| `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/ValidationExceptionHandlerTests.cs` | **CREATE** | Tier 2 unit tests for ValidationExceptionHandler |
| `tests/Hexalith.EventStore.Server.Tests/Commands/ConcurrencyConflictExceptionHandlerTests.cs` | READ ONLY | Reference test pattern for IExceptionHandler |

### Project Structure Notes

- CommandApi at `src/Hexalith.EventStore.CommandApi/` -- hosts all HTTP-facing code
- Package flow: Contracts <- Server <- CommandApi
- Feature-folder organization: ErrorHandling/, Filters/, Validation/, Controllers/, Pipeline/
- File-scoped namespaces, Allman braces, `_camelCase` private fields
- Nullable enabled, implicit usings, TreatWarningsAsErrors
- XML docs on all public types
- 4 spaces indentation, CRLF, UTF-8

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 3, Story 3.2]
- [Source: _bmad-output/planning-artifacts/architecture.md#D5, Rule 7, Rule 13]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#Journey 6, Enforcement Rules E1-E10]
- [Source: _bmad-output/planning-artifacts/prd.md#FR2, D16]
- [Source: src/Hexalith.EventStore.CommandApi/Filters/ValidateModelFilter.cs]
- [Source: src/Hexalith.EventStore.CommandApi/ErrorHandling/ValidationExceptionHandler.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Validation/SubmitCommandRequestValidator.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Validation/ExtensionMetadataSanitizer.cs]
- [Source: tests/Hexalith.EventStore.IntegrationTests/CommandApi/ValidationTests.cs]
- [Source: tests/Hexalith.EventStore.Server.Tests/Commands/ConcurrencyConflictExceptionHandlerTests.cs -- reference test pattern]
- [Source: _bmad-output/implementation-artifacts/2-5-duplicate-command-detection.md -- Story 2.5 learnings]
- [Source: _bmad-output/implementation-artifacts/3-1-command-submission-endpoint.md -- Story 3.1 context]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
