# Story 3.1: Command Submission Endpoint

Status: ready-for-dev

## Story

As an API consumer,
I want to submit commands via `POST /api/v1/commands`,
So that I can trigger domain processing through a standard REST interface.

## Acceptance Criteria

1. **Given** a valid command payload (all 8 fields: messageId, tenant, domain, aggregateId, commandType, payload, and optional correlationId + extensions — see "PRD vs. Architecture" note below),
   **When** submitted to `POST /api/v1/commands` with a valid JWT,
   **Then** the system returns `202 Accepted` with `Location` header pointing to the command status endpoint for the correlationId
   **And** includes `Retry-After: 1` header (delta-seconds format)
   **And** the correlation ID defaults to the client-supplied messageId if not provided (FR4).

> **Note:** The epic AC references 4 fields but the architecture-aligned contract requires 8 fields. See "PRD vs. Architecture field discrepancy" in Dev Notes. Use the 8-field contract.

## Tasks / Subtasks

This is primarily a **verification + gap-fill story** — most infrastructure already exists. The dev agent must verify AC compliance and fix any gaps.

### Verification protocol

For each subtask:
- **PASS** → Check the box, record "PASS" in Completion Notes
- **FAIL** → Document the gap, implement the fix, add/update tests to cover the fix, then check the box with "FIXED" in Completion Notes
- **Scope escalation:** If more than 3 subtasks require non-trivial code changes (beyond config/header tweaks), pause and document the scope expansion before continuing

### Story scope boundary

This story covers the **happy path** of command submission only: valid request → 202 Accepted with correct headers. Error responses (400, 401, 403, 409, 503) are explicitly deferred to Stories 3.2 and 3.5. Do not implement or test error paths here.

### Definition of done

1. All verification subtasks have PASS or FIXED annotations in Completion Notes
2. All existing + new tests pass (diff against baseline shows no regressions)
3. At least one smoke test proves a real HTTP POST → 202 with correct headers (not just code-reading)
4. Dev Agent Record section is populated with model, files touched, and completion notes

---

### Part A: Code verification (read and confirm)

- [ ] Task 1: Verify CommandsController endpoint behavior (AC: #1)
  - [ ] 1.1 Confirm `POST /api/v1/commands` returns `202 Accepted` on valid submission
  - [ ] 1.2 Confirm `Location` response header points to `/api/v1/commands/status/{correlationId}` — verify whether ASP.NET Core generates a relative or absolute URI (AC says relative; `CreatedAtAction` defaults to absolute — check and align)
  - [ ] 1.3 Confirm `Retry-After` response header uses delta-seconds format (integer `1`), NOT HTTP-date format
  - [ ] 1.4 Confirm `X-Correlation-ID` response header is present
  - [ ] 1.5 Fix any gaps in response headers or status codes

- [ ] Task 2: Verify CorrelationId defaulting logic (AC: #1)
  - [ ] 2.1 **CRITICAL CHECK:** Read `SubmitCommandRequestValidator.cs` — does it require CorrelationId to be non-empty? If yes, the validator will reject null/empty CorrelationId BEFORE the defaulting logic executes, making the default dead code. If the validator blocks null/empty, relax the CorrelationId validation rule to allow null/empty (it's optional per FR4)
  - [ ] 2.2 Confirm when `CorrelationId` is **null** (property absent from JSON) in request, it defaults to `MessageId`
  - [ ] 2.3 Confirm when `CorrelationId` is **empty string** (`""`) in request, it defaults to `MessageId`
  - [ ] 2.4 Confirm when `CorrelationId` is explicitly provided, it is used as-is
  - [ ] 2.5 Verify this defaulting occurs at the correct layer (controller or handler)

- [ ] Task 3: Verify MediatR pipeline and pre-pipeline execution order (AC: #1)
  - [ ] 3.1 Confirm `ValidateModelFilter` is wired in `Program.cs` and executes before the controller action
  - [ ] 3.2 Confirm pipeline behaviors execute: Logging → Validation → Authorization → Handler
  - [ ] 3.3 Confirm SubmitCommandHandler writes "Received" status before actor invocation
  - [ ] 3.4 Confirm CommandRouter routes to correct AggregateActor

- [ ] Task 4: Verify CorrelationIdMiddleware and two-correlation-ID relationship (AC: #1)
  - [ ] 4.1 Confirm middleware extracts `X-Correlation-ID` from request header when valid
  - [ ] 4.2 Confirm middleware generates new correlation ID when header absent
  - [ ] 4.3 Confirm correlation ID propagated to response header
  - [ ] 4.4 Clarify the relationship between **middleware correlation ID** (HTTP request tracing, `X-Correlation-ID` header) and **request body `CorrelationId`** (command lifecycle tracker). Document: are they the same value? Does one override the other? What happens when header says `abc` but body says `xyz`?

### Part B: Testing and regression verification

- [ ] Task 5: Establish test baseline (AC: #1)
  - [ ] 5.1 Run ALL Tier 1 + Tier 2 tests **before any code changes** and record exact failure list
  - [ ] 5.2 Categorize each failure as pre-existing (from Stories 2.1-2.3) or new
  - [ ] 5.3 Save baseline results for diff comparison after changes

- [ ] Task 6: Write/verify integration tests in `tests/Hexalith.EventStore.IntegrationTests/CommandApi/` (AC: #1)
  - [ ] 6.1 Test: valid command → 202 with correct Location header format + Retry-After: 1 (delta-seconds)
  - [ ] 6.2 Test: Location header format — assert exact format (relative path vs. absolute URI) and document the decision
  - [ ] 6.3 Test: CorrelationId defaults to MessageId when property is null (absent from JSON)
  - [ ] 6.4 Test: CorrelationId defaults to MessageId when property is empty string
  - [ ] 6.5 Test: explicit CorrelationId is preserved in response
  - [ ] 6.6 Test: response body contains CorrelationId
  - [ ] 6.7 Run all Tier 1 + Tier 2 tests, diff against baseline to confirm no regressions

- [ ] Task 7: Smoke test — real HTTP request (AC: #1)
  - [ ] 7.1 If Tier 3 environment available (DAPR + Docker): send a real `POST /api/v1/commands` via test client and verify 202 + all response headers
  - [ ] 7.2 If Tier 3 unavailable: document this as a verification gap — HTTP-level header behavior cannot be fully confirmed without the full pipeline. Verify as much as possible via Tier 2 controller unit tests with mocked dependencies

## Dev Notes

### CRITICAL: This is a verification story

Most of the command submission endpoint infrastructure already exists. The existing codebase includes:

- **`CommandsController.cs`** — `POST /api/v1/commands` endpoint with `[Authorize]`, 1MB request size limit
- **`SubmitCommandRequest.cs`** — HTTP DTO with MessageId, Tenant, Domain, AggregateId, CommandType, Payload, CorrelationId, Extensions
- **`SubmitCommandResponse.cs`** — Response DTO with CorrelationId
- **`CorrelationIdMiddleware.cs`** — Extracts/generates X-Correlation-ID header
- **`SubmitCommandRequestValidator.cs`** — FluentValidation for HTTP DTO (MessageId, Tenant, Domain, AggregateId, CommandType, Payload, Extensions)
- **`SubmitCommandValidator.cs`** — FluentValidation for MediatR message (defense-in-depth)
- **`ExtensionMetadataSanitizer.cs`** — Security validation (XSS, SQL injection, LDAP, path traversal)
- **MediatR pipeline behaviors** — LoggingBehavior, ValidationBehavior, AuthorizationBehavior
- **`SubmitCommandHandler`** — Writes "Received" status, archives command, routes via CommandRouter
- **`CommandRouter.cs`** — Derives AggregateIdentity, creates DAPR actor proxy, invokes ProcessCommandAsync
- **Exception handlers** — Validation→400, Authorization→403, AuthServiceUnavailable→503, ConcurrencyConflict→409, DomainRejected→404/409/422, Global→500

Your job: **read the existing code, verify each AC, record PASS/FAIL, fix any gaps.**

### PRD vs. Architecture field discrepancy

The PRD (D16) specifies an "Ultra-Thin Client Command" with **4+1 fields**: messageId, aggregateId, commandType, payload + optional correlationId. Server derives tenantId (from JWT) and domain (from commandType prefix via MessageType).

The current `SubmitCommandRequest` has **8 fields** including explicit Tenant, Domain, and Extensions. The architecture document shows this expanded contract.

**Resolution:** Accept the current architecture-aligned 8-field contract. The architecture is the authoritative design document and was written after the PRD. Do NOT refactor to 4+1 fields — that would break existing tests and the established contract.

### Sprint dependency warning

Story 2-3 (State Rehydration & Domain Service Invocation) is still in `review` status. If that review surfaces issues in `CommandRouter` or `SubmitCommandHandler`, those fixes could conflict with 3-1 verification. Recommend completing 2-3 review before starting this story if possible.

### Two correlation IDs — middleware vs. request body

The system has **two correlation ID sources** that serve different purposes:

1. **`X-Correlation-ID` HTTP header** (via `CorrelationIdMiddleware`) — HTTP request tracing ID. Generated by middleware if absent. Used for structured logging and OpenTelemetry spans.
2. **`CorrelationId` field in `SubmitCommandRequest`** — Command lifecycle tracker. Optional, defaults to `MessageId` per FR4. Used for command status queries via `GET /api/v1/commands/status/{correlationId}`.

**Dev agent must verify:** How does the controller reconcile these? If the header provides one value and the body provides a different value, which one wins for the `Location` header? Document the actual behavior and ensure it's consistent.

### Location header format decision

The AC references a relative path (`/api/v1/commands/status/{correlationId}`), but ASP.NET Core `CreatedAtAction` generates absolute URIs by default (e.g., `https://host/api/v1/commands/status/{id}`). Either format is valid per HTTP spec (RFC 7231 Section 7.1.2 allows both).

**Decision for dev agent:** Accept whichever format the controller currently produces. Document the actual format in Completion Notes. If it uses `CreatedAtAction` → absolute URI is fine. The key requirement is that the URI correctly resolves to the status endpoint.

### Response header verification checklist

Per UX spec Act 2 and architecture:
- `202 Accepted` status code
- `Location` header pointing to command status endpoint for the correlationId (absolute or relative — see above)
- `Retry-After: 1` header — must use delta-seconds format (integer `1`), not HTTP-date format
- `X-Correlation-ID: {correlationId}` header
- Response body: `{ "correlationId": "..." }`

### Controller flow (verify each step)

```
1. Extract JWT 'sub' claim → UserId (reject if missing → 401)
2. Sanitize Extensions via ExtensionMetadataSanitizer (reject → 400)
3. Store tenant in HttpContext.Items["RequestTenantId"]
4. Map SubmitCommandRequest → SubmitCommand (MediatR message)
5. Send to MediatR pipeline
6. Return 202 Accepted with Location + Retry-After headers
```

### Architecture compliance

- **D5:** All errors must use RFC 7807 ProblemDetails (`application/problem+json`)
- **Rule 4:** MediatR pipeline order: Logging → Validation → Authorization → Handler
- **Rule 7:** ProblemDetails for ALL API error responses — never custom shapes
- **Rule 12:** Command status writes are advisory — failure must never block pipeline
- **Rule 13:** No stack traces in production error responses
- **E6:** No event sourcing terminology in any error response ("aggregate", "event stream", "actor", "DAPR" never appear)

### Testing approach

**Test baseline first:** Before making ANY code changes, run all tests and record exact failure list. This prevents confusing pre-existing failures with new regressions.

**Tier 3 dependency:** HTTP-level tests (response headers, Location URI, Retry-After format) require the full ASP.NET Core pipeline via `WebApplicationFactory`, which lives in `tests/Hexalith.EventStore.IntegrationTests/`. This requires full DAPR init + Docker. If Docker is unavailable, document this as a verification gap and verify as much as possible via Tier 2 controller unit tests with mocked dependencies.

**CorrelationId validator gotcha:** Check whether `SubmitCommandRequestValidator` or `SubmitCommandValidator` requires CorrelationId to be non-empty. If so, the FR4 defaulting logic (null/empty → MessageId) is dead code because validation rejects null/empty before the controller/handler can apply the default. Fix: make CorrelationId optional in the validator (it IS optional per FR4 and the epic AC).

**Tier 2 tests** (DAPR slim init required) — unit-level verification:
- `SubmitCommandRequestValidatorTests.cs` — validates structural constraints
- `CommandRouterTests.cs` — verifies routing logic
- `SubmitCommandExtensionsTests.cs` — verifies DTO mapping

**Tier 3 tests** (full DAPR + Docker) — HTTP integration, **target project for new tests:**
- `tests/Hexalith.EventStore.IntegrationTests/CommandApi/` — all new HTTP-level tests go here (response headers, Location URI format, Retry-After format require the full HTTP pipeline via `WebApplicationFactory`)
- `CommandRoutingIntegrationTests.cs` — end-to-end submission
- `CommandsControllerTests.cs` — HTTP-level integration

**Test commands:**
```bash
# Baseline — run BEFORE changes, save output
dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~CommandRouter|FullyQualifiedName~SubmitCommand|FullyQualifiedName~Validator" 2>&1 | tee baseline-tier2.txt

# Tier 1 — all contract tests
dotnet test tests/Hexalith.EventStore.Contracts.Tests/

# Tier 3 — integration (requires full DAPR init + Docker)
dotnet test tests/Hexalith.EventStore.IntegrationTests/ --filter "FullyQualifiedName~CommandRouting|FullyQualifiedName~CommandsController"
```

**Known pre-existing test failures** (from Story 2.1-2.3):
- 4 SubmitCommandHandler tests (NullRef — investigate)
- 1 validator test
- 10 auth integration tests
- Diff post-change results against baseline to isolate new failures

### Previous story intelligence

**From Stories 2.1-2.3:**
- CommandRouter derives actor ID using canonical identity: `{tenant}:{domain}:{aggregateId}`
- Advisory status writes pattern: status write failures logged but never block pipeline
- SubmitCommandHandler flow: write "Received" status → archive command → route to actor → check result
- Fakes available: `FakeEventPersister`, `FakeDomainServiceInvoker` with SetupResponse/Invocations tracking
- Test patterns: use `--filter "FullyQualifiedName~..."` for scoped execution, Shouldly for assertions
- DAPR SDK version: check `Directory.Packages.props` for actual version (was 1.16.1 not 1.17.0)

### Key files to read

| File | Purpose |
|------|---------|
| `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` | POST endpoint implementation |
| `src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs` | Correlation ID extraction/generation |
| `src/Hexalith.EventStore.CommandApi/Models/SubmitCommandRequest.cs` | HTTP request DTO |
| `src/Hexalith.EventStore.CommandApi/Models/SubmitCommandResponse.cs` | HTTP response DTO |
| `src/Hexalith.EventStore.CommandApi/Validation/SubmitCommandRequestValidator.cs` | HTTP DTO validation |
| `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` | DI registration |
| `src/Hexalith.EventStore.CommandApi/Filters/ValidateModelFilter.cs` | Pre-controller FluentValidation action filter |
| `src/Hexalith.EventStore.CommandApi/Program.cs` | App startup + middleware order |
| `src/Hexalith.EventStore.Server/Commands/CommandRouter.cs` | Command routing to actor |
| `src/Hexalith.EventStore.Server/Commands/SubmitCommandHandler.cs` | MediatR handler |
| `tests/Hexalith.EventStore.Server.Tests/Commands/CommandRouterTests.cs` | Router tests |
| `tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandRequestValidatorTests.cs` | Validator tests |
| `tests/Hexalith.EventStore.IntegrationTests/CommandApi/CommandRoutingIntegrationTests.cs` | E2E tests |

### Project Structure Notes

- CommandApi is the host project — references Server + Contracts
- Package dependencies flow inward: Contracts ← Server ← CommandApi
- Feature-folder organization within each project
- All new files follow file-scoped namespaces, Allman braces, `_camelCase` fields

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 3, Story 3.1]
- [Source: _bmad-output/planning-artifacts/architecture.md#API Boundary, Data Flow, Error Handling]
- [Source: _bmad-output/planning-artifacts/prd.md#FR1-FR4, D15-D16, NFR1-NFR2]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#Act 2, Error Journeys 6-10, Enforcement Rules E1-E10]
- [Source: _bmad-output/implementation-artifacts/2-1-aggregate-actor-and-command-routing.md]
- [Source: _bmad-output/implementation-artifacts/2-2-event-persistence-and-sequence-numbers.md]
- [Source: _bmad-output/implementation-artifacts/2-3-state-rehydration-and-domain-service-invocation.md]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
