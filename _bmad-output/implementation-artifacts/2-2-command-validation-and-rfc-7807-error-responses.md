# Story 2.2: Command Validation & RFC 7807 Error Responses

Status: ready-for-dev

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

- [ ] Task 1: Enhance ValidationExceptionHandler with full RFC 7807 compliance (AC: #1, #2, #3)
  - [ ] 1.1 Update `type` field to use proper RFC 7807 URI (`https://tools.ietf.org/html/rfc7807#section-3.1` or a project-specific type URI)
  - [ ] 1.2 Add `instance` field with the request path (`httpContext.Request.Path`)
  - [ ] 1.3 Add `tenantId` extension extracted from the request body (if available/parseable) or from the validation context
  - [ ] 1.4 Ensure `validationErrors` array items include `field` and `message` properties (already present, verify format)

- [ ] Task 2: Enhance GlobalExceptionHandler with full RFC 7807 compliance (AC: #2, #5)
  - [ ] 2.1 Add `instance` field with request path
  - [ ] 2.2 Ensure `detail` is always human-readable, never contains stack traces (rule #13)
  - [ ] 2.3 Add `tenantId` extension when available from request context

- [ ] Task 3: Enhance SubmitCommandRequestValidator for comprehensive SEC-4 validation (AC: #1, #6)
  - [ ] 3.1 Add validation for `>`, `&`, `'`, `"` characters in extensions (broader injection prevention beyond just `<`)
  - [ ] 3.2 Add validation for script/HTML patterns in extension values (e.g., `javascript:`, `on\w+=`)
  - [ ] 3.3 Add max total extension size validation (combined key+value bytes ≤ 64KB to prevent memory abuse)
  - [ ] 3.4 Add field-level string length limits: Tenant ≤128, Domain ≤128, AggregateId ≤256, CommandType ≤256
  - [ ] 3.5 Add character validation on Tenant, Domain, AggregateId (alphanumeric, hyphens, dots — consistent with AggregateIdentity constraints from Contracts)

- [ ] Task 4: Add MediatR ValidationBehavior for SubmitCommand (AC: #4)
  - [ ] 4.1 Create `SubmitCommandValidator` in `Server/Pipeline/` (or `CommandApi/Validation/`) to validate the MediatR `SubmitCommand` record (not just the HTTP request)
  - [ ] 4.2 Ensure ValidationBehavior catches validation failures BEFORE the handler executes
  - [ ] 4.3 Verify the ValidationBehavior is registered as an open generic in MediatR pipeline (already done in `AddCommandApi()`)

- [ ] Task 5: Write/update tests (AC: #1, #2, #3, #4, #5, #6)
  - [ ] 5.1 Unit: `ValidationExceptionHandler_ValidationException_ReturnsProblemDetailsWithExtensions`
  - [ ] 5.2 Unit: `GlobalExceptionHandler_UnhandledException_ReturnsProblemDetailsNoStackTrace`
  - [ ] 5.3 Unit: `SubmitCommandRequestValidator_MissingTenant_ReturnsValidationError`
  - [ ] 5.4 Unit: `SubmitCommandRequestValidator_InjectionCharacters_ReturnsValidationError`
  - [ ] 5.5 Unit: `SubmitCommandRequestValidator_ExtensionSizeLimits_ReturnsValidationError`
  - [ ] 5.6 Unit: `SubmitCommandRequestValidator_FieldLengthLimits_ReturnsValidationError`
  - [ ] 5.7 Integration (WebApplicationFactory): `PostCommands_MissingFields_Returns400WithValidationErrors` — verify `validationErrors` array, `correlationId`, `tenantId` extensions
  - [ ] 5.8 Integration (WebApplicationFactory): `PostCommands_InjectionInExtensions_Returns400WithProblemDetails`
  - [ ] 5.9 Integration (WebApplicationFactory): `PostCommands_OversizedExtensions_Returns400WithProblemDetails`
  - [ ] 5.10 Integration: `UnhandledException_Returns500ProblemDetailsWithoutStackTrace`

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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
