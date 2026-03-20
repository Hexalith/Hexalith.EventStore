# Story 13.2: Error Reference Pages at Type URIs

Status: done

## Story

As an API consumer,
I want error `type` URIs to resolve to human-readable documentation,
So that I can understand any error and find the resolution without support.

## Acceptance Criteria

1. **Given** each error `type` URI (e.g., `https://hexalith.io/problems/validation-error`), **When** opened in a browser, **Then** a documentation page explains the error, shows an example request/response, and suggests resolution steps (UX-DR32).

## Tasks / Subtasks

- [x] Task 0: Prerequisites (AC: #1)
  - [x] 0.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- confirm baseline compiles (0 errors, 0 warnings)
  - [x] 0.2 Read `src/Hexalith.EventStore.CommandApi/ErrorHandling/ProblemTypeUris.cs` to confirm all 13 error type URIs (this is the source of truth)
  - [x] 0.3 Verify no existing error reference pages exist at `docs/reference/problems/`

- [x] Task 1: Create the error reference page template and index (AC: #1)
  - [x] 1.1 Create `docs/reference/problems/` directory
  - [x] 1.2 Create `docs/reference/problems/index.md` -- an index page listing all error types **grouped by HTTP status code family** (4xx Client Errors, 5xx Server Errors), with one-line descriptions and links to individual pages. This page itself explains the RFC 7807 ProblemDetails response format used by EventStore, including the standard fields (`type`, `title`, `status`, `detail`, `instance`) and Hexalith extensions (`correlationId`, `tenantId`, `errors`). Include a note at the bottom: "Domain services may define additional error types beyond the 13 listed here. If you receive an error `type` URI not listed on this page, consult the documentation for the specific domain service."

- [x] Task 2: Create individual error reference pages (AC: #1)
  - [x] 2.1 Create `docs/reference/problems/validation-error.md` -- 400 Bad Request, command validation failures
  - [x] 2.2 Create `docs/reference/problems/authentication-required.md` -- 401 Unauthorized, missing or invalid JWT
  - [x] 2.3 Create `docs/reference/problems/token-expired.md` -- 401 Unauthorized, expired JWT
  - [x] 2.4 Create `docs/reference/problems/bad-request.md` -- 400 Bad Request, malformed request (e.g., invalid correlation ID format)
  - [x] 2.5 Create `docs/reference/problems/forbidden.md` -- 403 Forbidden, tenant/domain authorization denied
  - [x] 2.6 Create `docs/reference/problems/not-found.md` -- 404 Not Found, requested resource not found
  - [x] 2.7 Create `docs/reference/problems/not-implemented.md` -- 501 Not Implemented, query endpoint not available
  - [x] 2.8 Create `docs/reference/problems/concurrency-conflict.md` -- 409 Conflict, optimistic concurrency conflict
  - [x] 2.9 Create `docs/reference/problems/rate-limit-exceeded.md` -- 429 Too Many Requests, per-tenant or per-consumer rate limit
  - [x] 2.10 Create `docs/reference/problems/backpressure-exceeded.md` -- 429 Too Many Requests, per-aggregate backpressure
  - [x] 2.11 Create `docs/reference/problems/service-unavailable.md` -- 503 Service Unavailable, processing pipeline temporarily down
  - [x] 2.12 Create `docs/reference/problems/command-status-not-found.md` -- 404 Not Found, no command status for correlation ID
  - [x] 2.13 Create `docs/reference/problems/internal-server-error.md` -- 500 Internal Server Error, unexpected failure

- [x] Task 3: Cross-reference and link integration (AC: #1)
  - [x] 3.1 Add a link to the error reference index from `docs/reference/command-api.md` (in the error handling section)
  - [x] 3.2 Add a link to the error reference index from `docs/guides/troubleshooting.md` (at the top, as a "see also" reference)
  - [x] 3.3 Verify all 13 `ProblemTypeUris` constants have a matching page by comparing filenames against the URI slugs

- [x] Task 4: Validate content quality (AC: #1)
  - [x] 4.1 Verify each page contains: (1) error explanation, (2) common causes list, (3) realistic example request that triggers it (actual endpoint calls, e.g., `POST /api/v1/commands` with a specific bad payload), (4) example response, (5) resolution steps
  - [x] 4.2 Grep all new files for forbidden terms: "aggregate", "event stream", "actor", "DAPR", "sidecar" -- zero matches required (UX-DR6)
  - [x] 4.3 Verify example responses match the actual ProblemDetails shape produced by the handlers (cross-reference handler source files)
  - [x] 4.4 Verify Retry-After headers are documented on 409 (1s) and 503 (30s) pages (UX-DR5)
  - [x] 4.5 Verify WWW-Authenticate header is documented on 401 pages (UX-DR4)

- [x] Task 5: Build and test (AC: #1)
  - [x] 5.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
  - [x] 5.2 Run all Tier 1 tests -- 0 regressions

## Dev Notes

### CRITICAL: This Is a Documentation-Only Story -- No Source Code Changes

This story creates 14 new Markdown files (1 index + 13 error pages) in `docs/reference/problems/` and adds 2 cross-reference links to existing docs. No C# source code is modified.

### Source of Truth: ProblemTypeUris.cs

The authoritative list of all error type URIs is `src/Hexalith.EventStore.CommandApi/ErrorHandling/ProblemTypeUris.cs`. It defines exactly **13 constants**:

| Constant | URI Slug | HTTP Status | Handler Source |
|----------|----------|-------------|----------------|
| `ValidationError` | `validation-error` | 400 | `ValidationExceptionHandler.cs`, `ValidationProblemDetailsFactory.cs` |
| `AuthenticationRequired` | `authentication-required` | 401 | `ConfigureJwtBearerOptions.cs` (OnChallenge) |
| `TokenExpired` | `token-expired` | 401 | `ConfigureJwtBearerOptions.cs` (OnChallenge) |
| `BadRequest` | `bad-request` | 400 | `CommandStatusController.cs` |
| `Forbidden` | `forbidden` | 403 | `AuthorizationExceptionHandler.cs`, `QueryExecutionFailedExceptionHandler.cs` |
| `NotFound` | `not-found` | 404 | `QueryNotFoundExceptionHandler.cs` |
| `NotImplemented` | `not-implemented` | 501 | `QueryExecutionFailedExceptionHandler.cs` |
| `ConcurrencyConflict` | `concurrency-conflict` | 409 | `ConcurrencyConflictExceptionHandler.cs` |
| `RateLimitExceeded` | `rate-limit-exceeded` | 429 | `ServiceCollectionExtensions.cs` (RateLimiter.OnRejected) |
| `BackpressureExceeded` | `backpressure-exceeded` | 429 | `BackpressureExceptionHandler.cs` |
| `ServiceUnavailable` | `service-unavailable` | 503 | `DaprSidecarUnavailableHandler.cs`, `AuthorizationServiceUnavailableHandler.cs` |
| `CommandStatusNotFound` | `command-status-not-found` | 404 | `CommandStatusController.cs` |
| `InternalServerError` | `internal-server-error` | 500 | `GlobalExceptionHandler.cs` |

**IMPORTANT:** The `DomainCommandRejectedExceptionHandler.cs` uses dynamic `rejection.RejectionType` URIs from domain business rules -- these are NOT listed in `ProblemTypeUris.cs` and are NOT in scope for this story. Only the 13 constants above need documentation pages.

### Exact ProblemDetails Response Shapes Per Error Type

Use these as the source of truth for example responses on each page. Cross-reference the actual handler source files to confirm.

**400 validation-error** (ValidationExceptionHandler + ValidationProblemDetailsFactory):
- Title: `Command Validation Failed`
- Detail: `The command has {N} validation error(s). See 'errors' for specifics.`
- Extensions: `correlationId`, `tenantId` (if available), `errors` (property-to-message map, camelCase keys via `JsonNamingPolicy.CamelCase`)
- **Example `errors` must show 2+ fields** to demonstrate the map structure (e.g., `"aggregateId": "Aggregate ID is required"`, `"commandType": "Command type is required"`). This helps API consumers build parsers for multi-field validation failures.

**401 authentication-required** (ConfigureJwtBearerOptions OnChallenge):
- Title: `Unauthorized`
- Detail: `Authentication is required to access this resource.`
- Extensions: none (pre-pipeline rejection, no correlationId per UX-DR2)
- Headers: `WWW-Authenticate: Bearer realm="hexalith-eventstore"`

**401 token-expired** (ConfigureJwtBearerOptions OnChallenge):
- Title: `Unauthorized`
- Detail: `The provided authentication token has expired.`
- Extensions: none (pre-pipeline rejection)
- Headers: `WWW-Authenticate: Bearer realm="hexalith-eventstore", error="invalid_token", error_description="The token has expired"`

**400 bad-request** (CommandStatusController):
- Title: `Bad Request`
- Detail: `Correlation ID is required.`
- Extensions: `correlationId`

**403 forbidden** (AuthorizationExceptionHandler, QueryExecutionFailedExceptionHandler):
- Title: `Forbidden`
- Detail (command authorization): `Not authorized for tenant '{tenantId}'. {reason}` (sanitized -- no ES terminology)
- Detail (query authorization): varies based on query failure type
- **Two handler sources:** Use the command authorization case (tenant denial) as the primary example. Add a secondary note in "Common Causes" mentioning that query endpoint authorization failures also return this type.
- Extensions: `correlationId`, `tenantId`

**404 not-found** (QueryNotFoundExceptionHandler):
- Title: `Not Found`
- Detail: `The requested resource was not found.`
- Extensions: `correlationId`

**501 not-implemented** (QueryExecutionFailedExceptionHandler):
- Title: `Not Implemented`
- Detail: varies
- Extensions: `correlationId`

**409 concurrency-conflict** (ConcurrencyConflictExceptionHandler):
- Title: `Conflict`
- Detail: `A concurrency conflict occurred. Please retry the command.`
- Extensions: `correlationId`
- Headers: `Retry-After: 1`
- **Retry strategy for "How to Fix":** Retry the exact same command payload after the `Retry-After` interval (1 second). The conflict is transient -- another command was processed first. Your command is still valid.

**429 rate-limit-exceeded** (ServiceCollectionExtensions RateLimiter.OnRejected):
- Title: `Too Many Requests`
- Detail: `Rate limit exceeded for tenant '{tenantId}'. Try again later.` (representative -- actual detail varies based on rate limiter config; read the `OnRejected` callback in `ServiceCollectionExtensions.cs` for the exact string)
- Extensions: `correlationId`, `tenantId`
- **429 disambiguation:** The "What Happened" section must clearly state this is a **per-tenant or per-consumer rate limit** (too many requests from your tenant/client in a time window). Cross-link to [backpressure-exceeded](./backpressure-exceeded.md) which is a **per-resource capacity limit** (too many pending commands targeting the same resource). The first sentence should make the distinction immediately clear.

**429 backpressure-exceeded** (BackpressureExceptionHandler):
- Title: `Too Many Requests`
- Detail (handler source): `The target aggregate is under backpressure due to excessive pending commands. Please retry after the specified interval.`
- **UX-DR6 WARNING:** The handler detail contains "aggregate" -- the documentation page MUST rephrase this in prose. Use: `The target resource is under backpressure due to excessive pending commands. Please retry after the specified interval.` Verify the handler detail at runtime; if the handler itself has been updated, use the actual text.
- Extensions: `correlationId`, `tenantId`, `domain`, `aggregateId`
- Headers: `Retry-After: {configurable}`
- **429 disambiguation:** The "What Happened" section must clearly state this is a **per-resource capacity limit** (too many pending commands targeting the same resource). Cross-link to [rate-limit-exceeded](./rate-limit-exceeded.md) which is a **per-tenant/per-consumer rate limit** (too many requests in a time window).

**503 service-unavailable** (DaprSidecarUnavailableHandler, AuthorizationServiceUnavailableHandler):
- Title: `Service Unavailable`
- Detail: `The command processing pipeline is temporarily unavailable. Please retry after the specified interval.`
- Extensions: none (pre-pipeline rejection, no correlationId per UX-DR2)
- Headers: `Retry-After: 30`
- **"How to Fix" must include both consumer and operator guidance:** For the consumer: retry after the Retry-After interval. For the operator: check infrastructure health endpoints, verify all service components are running, check the Aspire dashboard health page.

**404 command-status-not-found** (CommandStatusController):
- Title: `Not Found`
- Detail: `No command status found for correlation ID '{correlationId}'.`
- Extensions: `correlationId`

**500 internal-server-error** (GlobalExceptionHandler):
- Title: `Internal Server Error`
- Detail: `An unexpected error occurred while processing your request.`
- Extensions: `correlationId`, `tenantId` (if extractable from context)
- **"How to Fix" must include correlationId log search tip:** Tell the consumer to save the `correlationId` from the response and provide it to the service operator. Tell operators to search structured logs by `correlationId` to find the full stack trace and error details (which are logged server-side per Enforcement Rule #13 but never exposed to clients).

### Page Structure Template

Every error reference page MUST follow this structure:

```markdown
# {Error Title}

**HTTP Status:** {status code}
**Problem Type:** `https://hexalith.io/problems/{slug}`

## What Happened

{1-3 sentences explaining what this error means in plain language. NO event sourcing terminology.}

## Common Causes

{Bulleted list of the most frequent triggers for this error. Helps the reader self-diagnose before reading the full example.}

## Example

### Request

```http
{Example HTTP request that triggers this error}
```

### Response

```http
HTTP/1.1 {status} {reason phrase}
Content-Type: application/problem+json
{any extra headers like Retry-After, WWW-Authenticate}

{Exact JSON ProblemDetails body matching the handler output}
```

## How to Fix

{Numbered resolution steps, written for the API consumer, actionable and specific.}

## Related

- [Error Reference Index](./index.md)
- {Links to related error pages if applicable}
```

### Writing Guidelines (UX-DR6 Compliance)

**NEVER use these terms** in any error reference page:
- "aggregate" -- say "resource" or "entity" instead
- "event stream" -- say "command history" or "processing record" instead
- "actor" -- say "processor" or "handler" instead
- "DAPR" -- say "infrastructure" or omit entirely
- "sidecar" -- say "service component" or omit entirely
- "event sourcing" -- say "command processing" or omit entirely

**Stripe principle:** Write error messages and documentation for the reader (the API consumer who received the error), not for the developer who wrote the code. The reader wants to know: what went wrong, why, and how to fix it.

**Example realism:** Example requests MUST be realistic endpoint calls against actual API paths. Do NOT use generic placeholder HTTP snippets. The reader should be able to reproduce the error by copying the example.

**API endpoint paths for examples** (use these exact paths in example requests):

| Endpoint | Method | Used By Error Types |
|----------|--------|---------------------|
| `/api/v1/commands` | POST | `validation-error`, `authentication-required`, `token-expired`, `forbidden`, `concurrency-conflict`, `rate-limit-exceeded`, `backpressure-exceeded`, `service-unavailable`, `internal-server-error` |
| `/api/commands/status/{correlationId}` | GET | `bad-request`, `command-status-not-found`, `forbidden` (status query variant) |
| `/api/v1/queries/{queryType}` | GET | `not-found`, `not-implemented`, `forbidden` (query variant) |

For a valid command payload reference, see `docs/getting-started/quickstart.md` (the IncrementCounter example with `messageId`, `tenant`, `domain`, `aggregateId`, `commandType`, `payload`). Use this as the baseline and break specific fields to trigger each error.

**JWT Authorization header for 401 examples:**
- `authentication-required` example: omit the `Authorization` header entirely, OR send `Authorization: Bearer` with no token
- `token-expired` example: send `Authorization: Bearer <expired-jwt-token>` (use a placeholder like `eyJhbGci...expired`)
- For "How to Fix" on 401 pages, reference the quickstart token acquisition section: `docs/getting-started/quickstart.md` (Keycloak token endpoint at port 8180, realm `hexalith`, client ID `hexalith-eventstore`)

**UX-DR6 scope clarification:** UX-DR6 (no event sourcing terminology) applies to **prose text only** -- the human-readable explanations, "What Happened", "Common Causes", and "How to Fix" sections. JSON field names in example responses are the **actual API contract** and MUST be shown verbatim (e.g., `aggregateId`, `domain` are real extension field names in the wire format). Do NOT rename JSON fields.

**Page conciseness target:** Each error page should be **40-80 lines**. Scannable, not essays. The reader is debugging at 2am -- they need answers in under 30 seconds. Use short sentences, bullet points, and code blocks. No walls of text.

### Editing Scope

**Files you MUST create:**
- `docs/reference/problems/index.md` -- error reference index page
- `docs/reference/problems/validation-error.md`
- `docs/reference/problems/authentication-required.md`
- `docs/reference/problems/token-expired.md`
- `docs/reference/problems/bad-request.md`
- `docs/reference/problems/forbidden.md`
- `docs/reference/problems/not-found.md`
- `docs/reference/problems/not-implemented.md`
- `docs/reference/problems/concurrency-conflict.md`
- `docs/reference/problems/rate-limit-exceeded.md`
- `docs/reference/problems/backpressure-exceeded.md`
- `docs/reference/problems/service-unavailable.md`
- `docs/reference/problems/command-status-not-found.md`
- `docs/reference/problems/internal-server-error.md`

**Files you MAY edit** (add cross-reference links only):
- `docs/reference/command-api.md` -- add link to error reference index
- `docs/guides/troubleshooting.md` -- add "see also" link to error reference index

**Files you MUST NOT edit:**
- Any C# source files
- `ProblemTypeUris.cs` -- read only, do not modify
- Any handler files -- read only for verification
- Any files outside `docs/`

### What NOT to Do

- Do NOT modify any source code -- this is documentation only
- Do NOT explain event sourcing concepts in error pages
- Do NOT add unit tests -- there is no testable code change
- Do NOT create a separate website or hosting -- these are Markdown files in the docs folder
- Do NOT add error types beyond the 13 defined in `ProblemTypeUris.cs`
- Do NOT document domain rejection types (dynamic `rejection.RejectionType` from `DomainCommandRejectedExceptionHandler`) -- those are domain-specific and out of scope
- Do NOT restructure the existing docs folder -- Story 13-3 handles progressive documentation structure

### Branch Base Guidance

Branch from `main`. Branch name: `docs/story-13-2-error-reference-pages-at-type-uris`

### Previous Story Intelligence

**Story 13-1 (Quick Start Guide) learnings:**
- Documentation verification story, similar approach to this story
- Source of truth principle: the codebase is always right, docs must match code
- Two gaps were found and fixed: missing `messageId` field in sample payload, missing Aspire CLI prerequisite
- Build: 0 errors, 0 warnings. 724 Tier 1 tests pass (271 Contracts + 297 Client + 62 Sample + 67 Testing + 27 SignalR)
- Branch naming convention: `docs/story-13-1-quick-start-guide`
- Commit message style: `docs: <description> (Story 13-1)`

### Git Intelligence

Recent commits show Story 13-1 completed:
- `b17d298` Merge PR #131: docs/story-13-1-quick-start-guide
- `3eca00e` docs: Update quick start guide with Aspire CLI prerequisite and messageId field (Story 13-1)

Previous epic (12) fully complete. Codebase stable on main.

### Architecture Compliance

- **File locations:** Error reference pages go in `docs/reference/problems/` -- consistent with the existing `docs/reference/` structure (which contains `command-api.md`, `query-api.md`, `nuget-packages.md`, and `api/`)
- **Error format:** RFC 7807 ProblemDetails (Architecture Decision D5)
- **Enforcement Rule #7:** ProblemDetails for all API error responses -- never custom shapes
- **Enforcement Rule #13:** No stack traces in production error responses
- **Solution file:** `Hexalith.EventStore.slnx` (modern XML format)
- **Build command:** `dotnet build Hexalith.EventStore.slnx --configuration Release`
- **Warnings as errors:** enabled -- build must produce 0 warnings

### Testing Compliance

There is an existing compliance test at `tests/Hexalith.EventStore.Server.Tests/ErrorHandling/ProblemTypeUriComplianceTests.cs` that validates:
- All URIs start with `https://hexalith.io/problems/`
- All URIs are unique per error category (UX-DR7)
- Error messages don't contain event sourcing terminology (UX-DR6)
- Rate limiter uses `ProblemTypeUris.RateLimitExceeded` constant

This test suite validates the code side. The documentation pages created in this story are the human-readable side that these URIs should resolve to.

### Project Structure Notes

- Error reference pages at `docs/reference/problems/{slug}.md` align with URI path `https://hexalith.io/problems/{slug}`
- The `docs/reference/` folder is the correct location for reference-level documentation (UX-DR33)
- The existing `docs/guides/troubleshooting.md` (1048 lines) covers operational troubleshooting (Docker, DAPR, deployment) -- the new error reference pages cover API error responses (HTTP 4xx/5xx) -- these are complementary, not overlapping
- No existing error reference pages or problems folder exists -- this is greenfield

### References

- [Source: src/Hexalith.EventStore.CommandApi/ErrorHandling/ProblemTypeUris.cs -- all 13 error type URI constants]
- [Source: _bmad-output/planning-artifacts/epics.md, Epic 13, Story 13.2 (lines 1513-1523)]
- [Source: _bmad-output/planning-artifacts/epics.md, Epic 3, Story 3.6 BDD scenario (lines 787-789)]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md, UX-DR1 through UX-DR11 (lines 266-276)]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md, UX-DR32 (line 312)]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md, E10 enforcement rule (line 2023)]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md, A11 acceptance (line 2043)]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md, D3 deliverable (line 2080)]
- [Source: _bmad-output/planning-artifacts/architecture.md, D5 error response format (line 403)]
- [Source: _bmad-output/planning-artifacts/architecture.md, Enforcement Rule #7 (line 695)]
- [Source: _bmad-output/planning-artifacts/architecture.md, Enforcement Rule #13 (line 701)]
- [Source: src/Hexalith.EventStore.CommandApi/ErrorHandling/ -- all exception handler files]
- [Source: src/Hexalith.EventStore.CommandApi/Authentication/ConfigureJwtBearerOptions.cs -- 401 responses]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs -- status query errors]
- [Source: tests/Hexalith.EventStore.Server.Tests/ErrorHandling/ProblemTypeUriComplianceTests.cs -- existing compliance tests]

### Definition of Done

- AC #1 verified: all 13 error type URIs have a matching documentation page in `docs/reference/problems/`
- Each page explains the error, shows example request/response, and suggests resolution steps
- Index page lists all error types with HTTP status codes and links
- Zero forbidden terms in any error reference page (UX-DR6)
- Retry-After documented on 409 and 503 pages (UX-DR5)
- WWW-Authenticate documented on 401 pages (UX-DR4)
- Cross-reference links added to command-api.md and troubleshooting.md
- Build: `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
- Tier 1 tests pass with 0 regressions

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Baseline build: 0 errors, 0 warnings
- ProblemTypeUris.cs confirmed 13 constants matching story spec
- All 13 handler source files cross-referenced for exact ProblemDetails shapes
- Forbidden term grep: 0 matches across all new files (UX-DR6)
- Retry-After verified on 409 (1s) and 503 (30s) pages (UX-DR5)
- WWW-Authenticate verified on both 401 pages (UX-DR4)
- Tier 1 tests: 724 passed, 0 failed (271 Contracts + 297 Client + 62 Sample + 67 Testing + 27 SignalR)

### Completion Notes List

- Created 14 new Markdown files: 1 index + 13 error reference pages in `docs/reference/problems/`
- Each page follows the prescribed template: What Happened, Common Causes, Example (Request + Response), How to Fix, Related
- Example responses match actual handler output (verified against all 13 handler source files)
- Index page groups errors by HTTP status family (4xx/5xx) with RFC 7807 ProblemDetails format explanation
- Cross-reference links added to `docs/reference/command-api.md` (Error Responses section) and `docs/guides/troubleshooting.md` (top-level "see also")
- Zero forbidden terms (aggregate, event stream, actor, DAPR, sidecar) in any documentation page
- Backpressure page rephrases handler detail to avoid "aggregate" in prose while preserving JSON field names
- 429 pages include cross-links to distinguish rate-limit vs backpressure
- 503 page includes both consumer and operator guidance
- 500 page includes correlationId log search tip for operators
- No source code changes -- documentation only

### Change Log

- 2026-03-20: Created 14 error reference documentation pages and added cross-reference links (Story 13-2)

### File List

**New files:**
- docs/reference/problems/index.md
- docs/reference/problems/validation-error.md
- docs/reference/problems/authentication-required.md
- docs/reference/problems/token-expired.md
- docs/reference/problems/bad-request.md
- docs/reference/problems/forbidden.md
- docs/reference/problems/not-found.md
- docs/reference/problems/not-implemented.md
- docs/reference/problems/concurrency-conflict.md
- docs/reference/problems/rate-limit-exceeded.md
- docs/reference/problems/backpressure-exceeded.md
- docs/reference/problems/service-unavailable.md
- docs/reference/problems/command-status-not-found.md
- docs/reference/problems/internal-server-error.md

**Modified files:**
- docs/reference/command-api.md (added error reference cross-link)
- docs/guides/troubleshooting.md (added error reference "see also" link)
