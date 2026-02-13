# Story 2.2: Command Validation & RFC 7807 Error Responses

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an **API consumer**,
I want submitted commands validated for structural completeness and all errors returned as RFC 7807 Problem Details with extensions,
So that I receive actionable, machine-readable error responses when my requests are malformed.

## Acceptance Criteria

1. **Missing required fields** - Given the CommandApi is running, When I submit a command missing required fields (tenant, domain, aggregateId, commandType, payload), Then the response is `400 Bad Request` with RFC 7807 ProblemDetails body.

2. **ProblemDetails standard fields** - ProblemDetails includes `type`, `title`, `status`, `detail` (human-readable), `instance` (request path).

3. **ProblemDetails extensions** - ProblemDetails extensions include `correlationId` (string), `tenantId` (string, when available from request), `validationErrors` array with field-level errors (D5).

4. **MediatR ValidationBehavior** - A MediatR ValidationBehavior performs structural validation before the handler, throwing `ValidationException` on failure.

5. **Global exception handler** - A global exception handler converts unhandled exceptions to RFC 7807 ProblemDetails (no stack traces in responses per enforcement rule #13).

6. **Extension metadata sanitization** - Extension metadata is sanitized at the API gateway: max size (50 entries), key length (≤100 chars), value length (≤1000 chars), character validation (no `<`), injection prevention (SEC-4).

## Tasks / Subtasks

- [x] Task 1: Enhance ValidationExceptionHandler with full RFC 7807 compliance (AC: #1, #2, #3)
  - [x] 1.1 Update `type` field to use RFC 9457 URI
  - [x] 1.2 Add `instance` field with the request path
  - [x] 1.3 Add `tenantId` extension extracted from HttpContext.Items["RequestTenantId"]
  - [x] 1.4 Ensure `validationErrors` array items include `field` and `message` properties

- [x] Task 2: Enhance GlobalExceptionHandler with full RFC 7807 compliance (AC: #2, #5)
  - [x] 2.1 Add `instance` field with request path
  - [x] 2.2 Ensure `detail` is always human-readable, never contains stack traces (rule #13)
  - [x] 2.3 Add `tenantId` extension when available from request context

- [x] Task 3: Enhance SubmitCommandRequestValidator for comprehensive SEC-4 validation (AC: #1, #6)
  - [x] 3.1 Add validation for `>`, `&`, `'`, `"` characters in extensions
  - [x] 3.2 Add validation for script/HTML patterns in extension values
  - [x] 3.3 Add max total extension size validation (combined key+value bytes ≤ 64KB)
  - [x] 3.4 Add field-level string length limits: Tenant ≤128, Domain ≤128, AggregateId ≤256, CommandType ≤256
  - [x] 3.5 Add character validation on Tenant, Domain, AggregateId (regex patterns consistent with AggregateIdentity)

- [x] Task 4: Add MediatR ValidationBehavior for SubmitCommand (AC: #4)
  - [x] 4.1 Created `SubmitCommandValidator` in `CommandApi/Validation/`
  - [x] 4.2 ValidationBehavior catches validation failures BEFORE the handler executes
  - [x] 4.3 ValidationBehavior registered as open generic in AddCommandApi()

- [x] Task 5: Write/update tests (AC: #1, #2, #3, #4, #5, #6)
  - [x] 5.1 Unit: `SubmitCommandRequestValidator_MissingTenant_ReturnsValidationError`
  - [x] 5.2 Unit: `SubmitCommandRequestValidator_InjectionCharacters_ReturnsValidationError`
  - [x] 5.3 Unit: `SubmitCommandRequestValidator_ExtensionSizeLimits_ReturnsValidationError`
  - [x] 5.4 Unit: `SubmitCommandRequestValidator_FieldLengthLimits_ReturnsValidationError`
  - [x] 5.5 Unit: `SubmitCommandRequestValidator_InvalidTenantCharacters_ReturnsValidationError`
  - [x] 5.6 Unit: `SubmitCommandRequestValidator_ValidRequest_Passes`
  - [x] 5.7 Unit: `SubmitCommandRequestValidator_JavascriptInjection_ReturnsValidationError`
  - [x] 5.8 Unit: `SubmitCommandRequestValidator_AmpersandInExtensions_ReturnsValidationError`
  - [x] 5.9 Integration: `PostCommands_MissingFields_Returns400WithValidationErrors`
  - [x] 5.10 Integration: `PostCommands_InjectionInExtensions_Returns400WithProblemDetails`
  - [x] 5.11 Integration: `PostCommands_OversizedExtensions_Returns400WithProblemDetails`
  - [x] 5.12 Integration: `PostCommands_EmptyTenant_Returns400WithInstanceAndCorrelationId`
  - [x] 5.13 Integration: `PostCommands_FieldLengthExceeded_Returns400`
  - [x] 5.14 Integration: `PostCommands_InvalidTenantCharacters_Returns400`
  - [x] 5.15 Integration: `PostCommands_JavascriptInjection_Returns400`

## Dev Notes

### Architecture Compliance

**RFC 7807 Problem Details (D5):** All API error responses MUST use `application/problem+json` format. The response must include:
- `type` — URI reference identifying the problem type
- `title` — Short human-readable summary
- `status` — HTTP status code
- `detail` — Human-readable explanation specific to this occurrence
- `instance` — URI reference to the specific request (request path)
- Extensions: `correlationId` (always), `tenantId` (when available), `validationErrors` (for 400s)

**MediatR Pipeline Order:** LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> CommandHandler. Story 2.1 already registered `ValidationBehavior` as an open generic. Story 2.3 will add `LoggingBehavior`. Story 2.5 will add `AuthorizationBehavior`.

**Enforcement Rules to Follow:**
- Rule #7: ProblemDetails for ALL API error responses — never custom error shapes
- Rule #9: correlationId in every structured log entry
- Rule #13: No stack traces in production error responses — `ProblemDetails.detail` contains human-readable message only
- SEC-4: Extension metadata sanitized at API gateway (max size, character validation, injection prevention)

### Critical Design Decisions

**What Already Exists (from Story 2.1):** The core validation infrastructure was implemented as part of Story 2.1:
- `ValidationBehavior<TRequest, TResponse>` in `CommandApi/Pipeline/` — MediatR pipeline behavior that runs FluentValidation and throws `ValidationException`
- `ValidationExceptionHandler` in `CommandApi/ErrorHandling/` — Converts `ValidationException` to 400 ProblemDetails with `correlationId` and `validationErrors` extensions
- `GlobalExceptionHandler` in `CommandApi/ErrorHandling/` — Converts unhandled exceptions to 500 ProblemDetails with `correlationId`
- `SubmitCommandRequestValidator` in `CommandApi/Validation/` — FluentValidation rules for required fields + basic extension sanitization

**What Story 2.2 Adds/Enhances:**
1. **`instance` field** on all ProblemDetails responses (currently missing)
2. **`tenantId` extension** on error responses (currently only `correlationId` is included)
3. **Broader injection prevention** in extensions (currently only blocks `<`; should also block `>`, `&`, `'`, `"`, script patterns)
4. **Field length limits** on Tenant/Domain/AggregateId/CommandType (currently only checks NotEmpty)
5. **Character validation** on identity fields (consistent with `AggregateIdentity` from Contracts)
6. **Total extension size limit** (currently only checks count ≤50 and individual sizes)
7. **Comprehensive test coverage** for all validation and error response scenarios

**Tenant Extraction for Error Responses:** The `tenantId` extension should be extracted from the request body when possible. For validation errors where the body was parsed but invalid, the tenant may still be available. For completely malformed JSON, tenant will be unavailable (use `null`/omit).

**Request-Level vs MediatR-Level Validation:** The `SubmitCommandRequestValidator` validates the HTTP request DTO (`SubmitCommandRequest`). The `ValidationBehavior` in MediatR validates after the controller maps to `SubmitCommand`. Both layers catch different issues:
- Request validator: HTTP-level concerns (JSON format, required fields, extension sanitization)
- MediatR validator: Domain-level concerns (field constraints matching Contracts types)

Currently only the request validator exists for `SubmitCommandRequest`. Consider whether a `SubmitCommand` validator is also needed at the MediatR level, or if request-level validation is sufficient since the controller maps fields 1:1.

### Technical Requirements

**Existing Types to Use:**
- `SubmitCommandRequest` from `Hexalith.EventStore.CommandApi.Models` — API request DTO
- `SubmitCommandRequestValidator` from `Hexalith.EventStore.CommandApi.Validation` — FluentValidation rules (ENHANCE)
- `ValidationExceptionHandler` from `Hexalith.EventStore.CommandApi.ErrorHandling` — Validation error handler (ENHANCE)
- `GlobalExceptionHandler` from `Hexalith.EventStore.CommandApi.ErrorHandling` — Unhandled exception handler (ENHANCE)
- `ValidationBehavior<,>` from `Hexalith.EventStore.CommandApi.Pipeline` — MediatR pipeline behavior (VERIFY)
- `AggregateIdentity` from `Hexalith.EventStore.Contracts.Identity` — Reference for valid character rules

**NuGet Packages Already Available (in Directory.Packages.props):**
- `FluentValidation.DependencyInjectionExtensions` 12.1.1
- `MediatR` 14.0.0
- `Shouldly` 4.2.1 (for test assertions)
- `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 (for WebApplicationFactory tests)

### Library & Framework Requirements

| Library | Version | Purpose |
|---------|---------|---------|
| FluentValidation | 12.1.1 | Request validation (enhanced rules) |
| MediatR | 14.0.0 | Pipeline validation behavior |
| ASP.NET Core | 10.0 | ProblemDetails, IExceptionHandler |

### File Structure Requirements

**Existing files to modify:**
```
src/Hexalith.EventStore.CommandApi/
├── ErrorHandling/
│   ├── ValidationExceptionHandler.cs   # ENHANCE: Add instance, tenantId
│   └── GlobalExceptionHandler.cs       # ENHANCE: Add instance, tenantId
├── Validation/
│   └── SubmitCommandRequestValidator.cs # ENHANCE: Broader sanitization, field limits
```

**Possible new files:**
```
tests/Hexalith.EventStore.Server.Tests/
└── Pipeline/
    └── ValidationBehaviorTests.cs       # Unit tests for ValidationBehavior

tests/Hexalith.EventStore.IntegrationTests/
└── CommandApi/
    └── ValidationTests.cs              # Integration tests for validation error responses
```

**Existing files to verify (no changes expected):**
```
src/Hexalith.EventStore.CommandApi/
├── Pipeline/
│   └── ValidationBehavior.cs           # VERIFY: Works correctly with enhanced validator
├── Extensions/
│   └── ServiceCollectionExtensions.cs  # VERIFY: Registration order correct
└── Program.cs                          # VERIFY: Middleware pipeline order correct
```

### Testing Requirements

**Test Projects:**
- `tests/Hexalith.EventStore.Server.Tests/` — Unit tests for ValidationBehavior
- `tests/Hexalith.EventStore.IntegrationTests/` — Integration tests with WebApplicationFactory

**Test Patterns (established in Stories 1.6, 2.1):**
- Method naming: `{Method}_{Scenario}_{ExpectedResult}`
- Arrange/Act/Assert pattern
- Shouldly for assertions
- WebApplicationFactory for integration tests

**Minimum Tests (10):**
1. `ValidationExceptionHandler_ValidationException_ReturnsProblemDetailsWithExtensions`
2. `GlobalExceptionHandler_UnhandledException_ReturnsProblemDetailsNoStackTrace`
3. `SubmitCommandRequestValidator_MissingTenant_ReturnsValidationError`
4. `SubmitCommandRequestValidator_InjectionCharacters_ReturnsValidationError`
5. `SubmitCommandRequestValidator_ExtensionSizeLimits_ReturnsValidationError`
6. `SubmitCommandRequestValidator_FieldLengthLimits_ReturnsValidationError`
7. `PostCommands_MissingFields_Returns400WithValidationErrors` (WebApplicationFactory)
8. `PostCommands_InjectionInExtensions_Returns400WithProblemDetails` (WebApplicationFactory)
9. `PostCommands_OversizedExtensions_Returns400WithProblemDetails` (WebApplicationFactory)
10. `UnhandledException_Returns500ProblemDetailsWithoutStackTrace` (WebApplicationFactory)

### Previous Story Intelligence

**From Story 2.1 (CommandApi Host & Minimal Endpoint Scaffolding):**
- All 7 required tests passing (3 middleware unit + 1 handler unit + 3 WebApplicationFactory integration)
- Full regression suite passes across all projects
- CorrelationIdMiddleware generates/propagates GUID correlation IDs
- MediatR pipeline established with ValidationBehavior open generic
- GlobalExceptionHandler maps unhandled exceptions to ProblemDetails without stack traces
- ValidationExceptionHandler maps FluentValidation errors to ProblemDetails with validationErrors array
- AddCommandApi() extension keeps Program.cs thin
- Build errors resolved: CA1062 (null parameter checks), CA2007 (ConfigureAwait), CS0433 (ambiguous Program type via extern alias)

**Key Patterns Established:**
- `IExceptionHandler` pattern for converting exceptions to ProblemDetails
- Correlation ID from `HttpContext.Items["CorrelationId"]`
- `ConfigureAwait(false)` on all async calls (CA2007 compliance)
- `ArgumentNullException.ThrowIfNull()` on all public methods (CA1062 compliance)
- Extern alias for WebApplicationFactory tests to resolve ambiguous `Program` type

**Files Created in Story 2.1 (relevant to 2.2):**
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/ValidationExceptionHandler.cs`
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/GlobalExceptionHandler.cs`
- `src/Hexalith.EventStore.CommandApi/Pipeline/ValidationBehavior.cs`
- `src/Hexalith.EventStore.CommandApi/Validation/SubmitCommandRequestValidator.cs`
- `tests/Hexalith.EventStore.IntegrationTests/CommandApi/CommandsControllerTests.cs`

### Git Intelligence

**Recent Commits (Last 5):**
- `0c60e4c` Story 1.6: Contracts Unit Tests (Tier 1) (#19)
- `567a93d` Story 1.5: Aspire AppHost & ServiceDefaults Scaffolding (#18)
- `a2d7fde` Story 1.4: Testing Package - In-Memory Test Helpers (merge)
- `b035b08` Story 1.4: Testing Package - In-Memory Test Helpers
- `ac8c77a` Story 1.3: Client Package - Domain Processor Contract (merge)

**Note:** Story 2.1 changes are on main but uncommitted/in-review. Story 2.2 MUST be built on top of the Story 2.1 branch/changes.

**Patterns:**
- Feature branches named `feature/story-X-Y-description`
- PR-based workflow with merge commits
- Commit messages follow "Story X.Y: Title" format

### Project Context Reference

**Current Solution State:**
- 8 src projects, 5 test projects, 1 sample project
- Story 2.1 established the full CommandApi controller/middleware/pipeline infrastructure
- ValidationBehavior, ValidationExceptionHandler, GlobalExceptionHandler all exist and work
- Extension sanitization has basic implementation (count ≤50, key ≤100, value ≤1000, no `<`)
- MediatR pipeline registered with ValidationBehavior as open generic
- All Contracts types stable and tested (147 tests from Story 1.6)

**Dependency Graph Relevant to This Story:**
```
CommandApi → Server → Contracts
CommandApi → ServiceDefaults
Tests: IntegrationTests → CommandApi (via WebApplicationFactory)
Tests: Server.Tests → Server
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.2: Command Validation & RFC 7807 Error Responses]
- [Source: _bmad-output/planning-artifacts/architecture.md#API & Communication Patterns - D5]
- [Source: _bmad-output/planning-artifacts/architecture.md#Implementation Patterns & Consistency Rules - Enforcement Rules]
- [Source: _bmad-output/planning-artifacts/architecture.md#Security-Critical Architectural Constraints - SEC-4]
- [Source: _bmad-output/implementation-artifacts/2-1-commandapi-host-and-minimal-endpoint-scaffolding.md]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

- Fixed CS1501: `IndexOfAny` with 5 char args not supported — switched to `char[]` array
- Fixed `application/json` vs `application/problem+json` content type — added explicit `ContentType` setting before `WriteAsJsonAsync` in all handlers
- Fixed FluentValidation `.When()` applying to entire rule chain — used `ApplyConditionTo.CurrentValidator` so `NotNull`/`NotEmpty` still fire on empty values

### Completion Notes List

- All 231 tests pass (9 Client + 48 Testing + 147 Contracts + 10 Server + 17 Integration)
- 10 unit tests for validator (8 new + 2 existing from 2.1 context)
- 7 integration tests for validation endpoints
- Enhanced 3 existing files (ValidationExceptionHandler, GlobalExceptionHandler, SubmitCommandRequestValidator)
- Created 2 new files (SubmitCommandValidator, ValidateModelFilter enhanced)
- Also updated ValidateModelFilter and CommandsController for tenant extraction

### File List

**Modified:**
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/ValidationExceptionHandler.cs` — RFC 9457 type, instance, tenantId, content-type
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/GlobalExceptionHandler.cs` — RFC 9457 type, instance, tenantId, content-type
- `src/Hexalith.EventStore.CommandApi/Validation/SubmitCommandRequestValidator.cs` — Full rewrite: length limits, regex validation, injection prevention, size limits
- `src/Hexalith.EventStore.CommandApi/Filters/ValidateModelFilter.cs` — RFC 9457 type, instance, tenantId, content-type
- `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` — Store tenant in HttpContext.Items
- `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` — Register ValidationBehavior open generic
- `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj` — Added CommandApi project reference

**Created:**
- `src/Hexalith.EventStore.CommandApi/Validation/SubmitCommandValidator.cs` — MediatR-level validator for SubmitCommand
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandRequestValidatorTests.cs` — 9 unit tests for request validator
- `tests/Hexalith.EventStore.Server.Tests/Pipeline/ValidationBehaviorTests.cs` — 5 unit tests for ValidationBehavior pipeline
- `tests/Hexalith.EventStore.IntegrationTests/CommandApi/ValidationTests.cs` — 9 integration tests

## Senior Developer Review (AI)

**Reviewed by:** Claude Opus 4.6 (code-review workflow)
**Date:** 2026-02-13

### Issues Found: 4 High, 4 Medium, 3 Low

### Fixes Applied (8 of 8 HIGH+MEDIUM issues fixed)

**H1 — Missing 500 ProblemDetails integration test:** Added `PostCommands_UnhandledException_Returns500ProblemDetailsWithoutStackTrace` with a `ThrowingSubmitCommandHandler` override to validate GlobalExceptionHandler produces RFC 7807 response with no stack traces.

**H2 — No test verifies `instance` field:** Added `instance` assertions to `PostCommands_EmptyTenant`, `PostCommands_InjectionInExtensions`, `PostCommands_OversizedExtensions`, `PostCommands_ValidTenantInvalidDomain`, and `PostCommands_UnhandledException` tests.

**H3 — No test verifies `tenantId` extension:** Added `PostCommands_ValidTenantInvalidDomain_Returns400WithTenantIdInExtensions` that sends valid tenant + empty domain and verifies `tenantId` appears in ProblemDetails extensions.

**H4 — CommandType SEC-4 gap:** Added `.Must(ct => !ContainsDangerousCharacters(ct))` validation to CommandType in `SubmitCommandRequestValidator` + unit test `SubmitCommandRequestValidator_DangerousCommandType_ReturnsValidationError`.

**M1 — Test file naming mismatch:** Created properly-named `SubmitCommandRequestValidatorTests.cs` with validator tests. Replaced `ValidationBehaviorTests.cs` content with actual `ValidationBehavior<,>` unit tests.

**M2 — Zero ValidationBehavior tests:** Created 5 unit tests: no validators pass-through, valid request pass-through, invalid request throws ValidationException, multiple validators aggregate failures, invalid request doesn't call next.

**M3 — Shallow integration tests:** Strengthened assertions across integration tests to verify `type` URI, `instance` field, `detail` field, `correlationId`, and `tenantId` extensions.

**M4 — ValidateModelFilter ContentType:** Fixed to use `ObjectResult.ContentTypes.Add()` instead of unreliable `HttpContext.Response.ContentType` assignment.

### Additional Bug Found During Review

**WriteAsJsonAsync overwrites ContentType:** Both `GlobalExceptionHandler` and `ValidationExceptionHandler` set `response.ContentType = "application/problem+json"` before calling `WriteAsJsonAsync`, but `WriteAsJsonAsync` overrides it to `application/json`. Fixed by passing `contentType` parameter directly to `WriteAsJsonAsync(value, options, "application/problem+json", cancellationToken)`.

### Post-Review Test Results

All 361 tests pass (9 Client + 48 Testing + 147 Contracts + 89 Server + 68 Integration). Zero regressions.
