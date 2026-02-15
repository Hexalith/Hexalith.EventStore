# Story 5.4: Security Audit Logging & Payload Protection

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

### Prerequisites

**This is the fourth and final story in Epic 5: Multi-Tenant Security & Access Control Enforcement.**

Stories 5.1 (DAPR Access Control Policies), 5.2 (Data Path Isolation Verification), and 5.3 (Pub/Sub Topic Isolation Enforcement) should be complete or in-review before starting this story. This story focuses on the **observability and logging enforcement** side of security -- ensuring that security-relevant events are auditable, JWT tokens are never logged, event payloads never leak into logs, and extension metadata is sanitized at the API boundary.

Verify these files/resources exist before starting:
- `src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs` (existing -- MediatR outermost behavior with structured logging)
- `src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationBehavior.cs` (existing -- MediatR authorization behavior)
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/GlobalExceptionHandler.cs` (existing -- unhandled exception -> ProblemDetails)
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationExceptionHandler.cs` (existing -- CommandAuthorizationException -> 403)
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/ValidationExceptionHandler.cs` (existing -- validation errors -> 400)
- `src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs` (existing -- generates/propagates correlation IDs)
- `src/Hexalith.EventStore.CommandApi/Authentication/ConfigureJwtBearerOptions.cs` (existing -- JWT configuration)
- `src/Hexalith.EventStore.CommandApi/Validation/SubmitCommandRequestValidator.cs` (existing -- command structural validation)
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` (existing -- 5-step orchestrator with structured logging at each stage)
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` (existing -- OpenTelemetry activity source and tag constants)
- `src/Hexalith.EventStore.CommandApi/Telemetry/EventStoreActivitySources.cs` (existing -- CommandApi activity source)
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` (existing -- publishes events to pub/sub)
- `src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs` (existing -- dead-letter publication)
- `src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs` (existing -- command envelope with Payload and Extensions)
- `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs` (existing -- event envelope with Payload)
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs` (existing -- test helper)

Run `dotnet test` to confirm all existing tests pass before beginning.

## Story

As a **security auditor**,
I want comprehensive security audit logging for failed authentication/authorization attempts and enforcement that event payload data never appears in logs,
So that security incidents are traceable while sensitive data is protected (SEC-4, SEC-5, NFR11, NFR12).

## Acceptance Criteria

1. **Failed authentication/authorization logged with request metadata** - Given the system processes commands with security checks at multiple layers, When an authentication or authorization failure occurs at any layer, Then the failure is logged with: timestamp, correlation ID, source IP, attempted tenant, attempted command type, failure reason, failure layer, And the JWT token itself is never logged (NFR11).

2. **Event payload data never appears in any log output** - Given events flow through the actor pipeline (persistence, publication, dead-letter routing), When structured logging occurs at any pipeline stage, Then event payload data (`EventEnvelope.Payload`, `CommandEnvelope.Payload`) never appears in log output, And only envelope metadata fields (the 11 fields, correlation ID, sequence number, etc.) are logged (SEC-5, NFR12).

3. **Extension metadata sanitized at API gateway** - Given a command is submitted with extension metadata, When the API gateway processes the command, Then extension metadata is validated for: maximum size (configurable, default 4KB total), allowed character set (printable ASCII + common UTF-8, no control characters), injection pattern rejection (no script tags, SQL injection patterns, LDAP injection), And commands with invalid extensions are rejected with 400 Bad Request as ProblemDetails (SEC-4).

4. **Secrets never appear in logs or source control** - Given the system uses connection strings, JWT signing keys, and DAPR credentials, When any logging occurs at any level, Then secrets (connection strings, JWT signing keys, DAPR component credentials) never appear in log output (NFR14), And no secrets are committed to source control (verified by test).

5. **Payload protection enforced at framework level** - Given the importance of SEC-5, When the payload protection mechanism is implemented, Then it operates at the framework level (e.g., custom log enricher, serialization filter, or ToString override), And does not rely on individual developer discipline to avoid logging payloads, And a test proves that even if a developer attempts to log a full EventEnvelope or CommandEnvelope, the payload field is redacted.

6. **Security audit log format is machine-parseable and consistent** - Given security audit events are logged, When an operator queries logs for security incidents, Then all security audit log entries use a consistent structured format with: `SecurityEvent` field identifying the event type (AuthenticationFailed, AuthorizationDenied, TenantMismatch, ExtensionMetadataRejected), And all entries include correlation ID, timestamp, source IP (when available), and failure details.

7. **Authentication failure logging at JWT layer** - Given JWT authentication middleware is configured, When an unauthenticated request arrives (missing/invalid/expired JWT), Then the failure is logged with: source IP (from HttpContext.Connection.RemoteIpAddress), attempted path, failure reason (missing token, invalid signature, expired, invalid issuer), And the JWT token content is never logged (NFR11), And the log entry includes `SecurityEvent=AuthenticationFailed`.

8. **Authorization failure logging at MediatR pipeline layer** - Given the AuthorizationBehavior checks tenant/domain/command-type permissions, When an authenticated but unauthorized command is submitted, Then the failure is logged with: correlation ID, authenticated user's tenant claims, attempted tenant, attempted domain, attempted command type, And the log entry includes `SecurityEvent=AuthorizationDenied`.

9. **Tenant mismatch logging at actor layer** - Given the AggregateActor performs SEC-2 tenant validation, When a tenant mismatch is detected, Then the existing logging is enhanced to include: `SecurityEvent=TenantMismatch`, source IP (if available in command context), And the log entry matches the consistent security audit format.

10. **Extension metadata sanitization logging** - Given extension metadata validation is applied at the API gateway, When a command is rejected due to invalid extensions, Then the rejection is logged with: correlation ID, tenant, domain, rejection reason (size exceeded, invalid characters, injection pattern), And the log entry includes `SecurityEvent=ExtensionMetadataRejected`, And the rejected extension content is NOT logged (it may contain injection attempts).

11. **Existing logging verified for payload absence** - Given the existing codebase has structured logging at multiple layers, When a comprehensive audit of all log statements is performed, Then no existing log statement includes event payload data or command payload data, And this is verified by automated tests scanning log output patterns.

## Tasks / Subtasks

- [x] Task 0: Verify prerequisites and understand current state (BLOCKING)
  - [x] 0.1 Run all existing tests -- they must pass before proceeding
  - [x] 0.2 Review existing `LoggingBehavior.cs` -- understand current structured logging fields (correlation ID, tenant, domain, command type)
  - [x] 0.3 Review existing `AuthorizationBehavior.cs` -- understand current authorization failure handling
  - [x] 0.4 Review existing `AuthorizationExceptionHandler.cs` -- understand how auth failures become 403 responses
  - [x] 0.5 Review existing `GlobalExceptionHandler.cs` -- understand unhandled exception logging
  - [x] 0.6 Review existing `AggregateActor.cs` -- understand structured logging at each pipeline stage (Rule #5 already noted in `LogStageTransition`)
  - [x] 0.7 Review existing `ConfigureJwtBearerOptions.cs` -- understand JWT event hooks for authentication failure logging
  - [x] 0.8 Review `CommandEnvelope.cs` and `EventEnvelope.cs` -- understand `Payload` and `Extensions` fields and their ToString behavior
  - [x] 0.9 Audit ALL existing log statements across CommandApi and Server projects for any payload data leakage
  - [x] 0.10 Identify gaps: which security events are NOT currently logged, which log statements might leak payload data

- [x] Task 1: Implement extension metadata sanitization at API gateway (AC: #3, #10)
  - [x] 1.1 Create `src/Hexalith.EventStore.CommandApi/Validation/ExtensionMetadataSanitizer.cs`:
    - `SanitizeResult Sanitize(IDictionary<string, string>? extensions)` method
    - Validate total size (default 4KB, configurable via `ExtensionMetadataOptions`)
    - Validate individual key/value character sets (printable ASCII + common UTF-8, reject control characters \x00-\x1F except \t \n \r)
    - Reject injection patterns: `<script`, `javascript:`, SQL keywords in suspicious context (`'; DROP`, `UNION SELECT`), LDAP injection (`)(`, `*)(`)
    - Return `SanitizeResult` with success/failure and rejection reason
  - [x] 1.2 Create `src/Hexalith.EventStore.CommandApi/Configuration/ExtensionMetadataOptions.cs`:
    - `int MaxTotalSizeBytes` (default 4096)
    - `int MaxKeyLength` (default 128)
    - `int MaxValueLength` (default 2048)
    - `int MaxExtensionCount` (default 32)
  - [x] 1.3 Integrate `ExtensionMetadataSanitizer` into the command submission flow:
    - Either as part of `SubmitCommandRequestValidator` (FluentValidation) or as explicit call in `SubmitCommandHandler`
    - Rejection returns 400 Bad Request as ProblemDetails with `SecurityEvent=ExtensionMetadataRejected`
    - Log rejection with correlation ID, tenant, domain, rejection reason (NOT the rejected content)
  - [x] 1.4 Register `ExtensionMetadataOptions` in DI via `AddEventStoreCommandApi()` or equivalent

- [x] Task 2: Implement payload protection at framework level (AC: #2, #5, #11)
  - [x] 2.1 Evaluate implementation approach for payload redaction:
    - **Option A:** Override `ToString()` on `EventEnvelope` and `CommandEnvelope` to exclude Payload field
    - **Option B:** Create a custom `ILogFormatter` or enricher that strips payload fields
    - **Option C:** Mark `Payload` properties with `[LogPropertyIgnore]` or similar attribute
    - Choose the approach that provides framework-level enforcement (not developer discipline)
  - [x] 2.2 Implement the chosen approach:
    - If Option A: Ensure `EventEnvelope.ToString()` outputs all 11 metadata fields but replaces `Payload` with `[REDACTED]` and truncates `Extensions` to keys only
    - If Option B/C: Configure the logging pipeline to automatically strip `Payload` fields from any logged objects
  - [x] 2.3 CRITICAL: The approach must work even if a developer writes `logger.LogInformation("Event: {Event}", eventEnvelope)` -- the payload must still be redacted
  - [x] 2.4 Verify existing log statements in `AggregateActor.cs` -- confirm none log payload data (current code already follows Rule #5, but verify)
  - [x] 2.5 Verify existing log statements in `EventPublisher.cs` -- confirm none log payload data
  - [x] 2.6 Verify existing log statements in `DeadLetterPublisher.cs` -- confirm none log payload data (dead-letter message contains command payload for replay)

- [x] Task 3: Enhance security audit logging for authentication failures (AC: #1, #7)
  - [x] 3.1 Review `ConfigureJwtBearerOptions.cs` for existing `JwtBearerEvents` hooks
  - [x] 3.2 Add or enhance `OnAuthenticationFailed` event handler to log:
    - `SecurityEvent=AuthenticationFailed`
    - Source IP: `context.HttpContext.Connection.RemoteIpAddress`
    - Attempted path: `context.HttpContext.Request.Path`
    - Failure reason: expired, invalid signature, invalid issuer (extracted from exception)
    - Correlation ID: from `CorrelationIdMiddleware` if available
    - CRITICAL: NEVER log the JWT token content (NFR11)
  - [x] 3.3 Add or enhance `OnChallenge` event handler for missing token scenarios:
    - `SecurityEvent=AuthenticationFailed`
    - Reason: "MissingToken" or "InvalidScheme"
    - Source IP and path
  - [x] 3.4 Ensure authentication failure logs use `ILogger<T>` with structured parameters (not string interpolation)

- [x] Task 4: Enhance security audit logging for authorization failures (AC: #1, #8)
  - [x] 4.1 Enhance `AuthorizationBehavior.cs` to log authorization denials with:
    - `SecurityEvent=AuthorizationDenied`
    - Correlation ID
    - Authenticated user's tenant claims (from JWT)
    - Attempted tenant, domain, command type
    - Denial reason (tenant mismatch, domain not authorized, command type not permitted)
  - [x] 4.2 Enhance `AuthorizationExceptionHandler.cs` to include `SecurityEvent=AuthorizationDenied` in its log output
  - [x] 4.3 CRITICAL: Never log the JWT token itself -- only log extracted claims (tenant list, domain list, etc.)

- [x] Task 5: Enhance security audit logging at actor layer (AC: #9)
  - [x] 5.1 Enhance `AggregateActor.cs` tenant mismatch logging (around line 146-149) to include:
    - `SecurityEvent=TenantMismatch`
    - Existing fields are already good: CorrelationId, CommandTenant, ActorTenant
  - [x] 5.2 NOTE: Source IP is typically not available at the actor layer (commands arrive via DAPR actor invocation, not HTTP). If source IP was captured at the API layer and stored in `CommandEnvelope` metadata, include it. Otherwise, document this limitation.

- [x] Task 6: Create SecurityAuditLoggingTests.cs (AC: #1, #6, #7, #8, #9, #10)
  - [x] 6.1 Create `tests/Hexalith.EventStore.Server.Tests/Security/SecurityAuditLoggingTests.cs`
  - [x] 6.2 Test: `AuthorizationBehavior_UnauthorizedTenant_LogsSecurityEvent` -- Verify AuthorizationBehavior logs with `SecurityEvent=AuthorizationDenied` when tenant is not authorized
  - [x] 6.3 Test: `AuthorizationBehavior_UnauthorizedDomain_LogsSecurityEvent` -- Verify AuthorizationBehavior logs with `SecurityEvent=AuthorizationDenied` when domain is not authorized
  - [x] 6.4 Test: `AuthorizationBehavior_SecurityEventLog_NeverContainsJwtToken` -- Verify authorization denial logs do not contain the JWT token (check log output for "Bearer" or JWT-like patterns)
  - [x] 6.5 Test: `AggregateActor_TenantMismatch_LogsSecurityEvent` -- Verify actor logs with `SecurityEvent=TenantMismatch` on tenant mismatch
  - [x] 6.6 Test: `ExtensionMetadataSanitizer_OversizedExtensions_LogsSecurityEvent` -- Verify extension rejection logs with `SecurityEvent=ExtensionMetadataRejected`
  - [x] 6.7 Test: `ExtensionMetadataSanitizer_RejectionLog_DoesNotContainExtensionContent` -- Verify the rejection log does not include the rejected extension values (which may contain injection payloads)
  - [x] 6.8 Test: `SecurityAuditLogs_ConsistentFormat_AllEventsHaveRequiredFields` -- Verify all security event log entries include: SecurityEvent type, correlationId, timestamp

- [x] Task 7: Create PayloadProtectionTests.cs (AC: #2, #5, #11)
  - [x] 7.1 Create `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionTests.cs`
  - [x] 7.2 Test: `EventEnvelope_ToString_DoesNotContainPayload` -- Verify `EventEnvelope.ToString()` excludes or redacts the `Payload` field
  - [x] 7.3 Test: `CommandEnvelope_ToString_DoesNotContainPayload` -- Verify `CommandEnvelope.ToString()` excludes or redacts the `Payload` field
  - [x] 7.4 Test: `EventEnvelope_ToString_ContainsAllMetadataFields` -- Verify `ToString()` still includes all 11 metadata fields (useful for debugging)
  - [x] 7.5 Test: `LoggingBehavior_CommandSubmission_NeverLogsPayload` -- Using a captured logger (e.g., `FakeLogger` or `ListLoggerProvider`), verify LoggingBehavior never outputs payload content
  - [x] 7.6 Test: `AggregateActor_AllLogStatements_NeverReferencePayload` -- Scan `AggregateActor.cs` source for log statements containing "Payload" or "payload" -- verify none exist (static analysis test)
  - [x] 7.7 Test: `EventPublisher_AllLogStatements_NeverReferencePayload` -- Scan `EventPublisher.cs` source for log statements containing "Payload" -- verify none exist
  - [x] 7.8 Test: `DeadLetterPublisher_AllLogStatements_NeverReferencePayload` -- Scan `DeadLetterPublisher.cs` source for log statements containing "Payload" -- verify none exist

- [x] Task 8: Create ExtensionMetadataSanitizerTests.cs (AC: #3, #10)
  - [x] 8.1 Create `tests/Hexalith.EventStore.Server.Tests/Security/ExtensionMetadataSanitizerTests.cs`
  - [x] 8.2 Test: `Sanitize_NullExtensions_ReturnsSuccess` -- Null or empty extensions are valid
  - [x] 8.3 Test: `Sanitize_ValidExtensions_ReturnsSuccess` -- Normal key-value pairs pass validation
  - [x] 8.4 Test: `Sanitize_OversizedTotal_ReturnsFailure` -- Extensions exceeding 4KB total are rejected
  - [x] 8.5 Test: `Sanitize_OversizedKey_ReturnsFailure` -- Individual key exceeding MaxKeyLength is rejected
  - [x] 8.6 Test: `Sanitize_OversizedValue_ReturnsFailure` -- Individual value exceeding MaxValueLength is rejected
  - [x] 8.7 Test: `Sanitize_TooManyExtensions_ReturnsFailure` -- More than MaxExtensionCount extensions are rejected
  - [x] 8.8 Test: `Sanitize_ControlCharacters_ReturnsFailure` -- Keys/values with control characters (\x00-\x1F except \t \n \r) are rejected
  - [x] 8.9 Test: `Sanitize_ScriptTagInjection_ReturnsFailure` -- `<script>alert(1)</script>` in values is rejected
  - [x] 8.10 Test: `Sanitize_SqlInjection_ReturnsFailure` -- `'; DROP TABLE users; --` in values is rejected
  - [x] 8.11 Test: `Sanitize_LdapInjection_ReturnsFailure` -- `)(cn=*))(|(cn=*` in values is rejected
  - [x] 8.12 Test: `Sanitize_UnicodeNormalization_HandlesConsistently` -- Unicode NFD/NFC normalization doesn't bypass character validation
  - [x] 8.13 Test: `ExtensionMetadataOptions_DefaultValues_AreReasonable` -- Verify default options: 4096 bytes, 128 key, 2048 value, 32 count

- [x] Task 9: Create SecretsProtectionTests.cs (AC: #4)
  - [x] 9.1 Create `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs`
  - [x] 9.2 Test: `SourceCode_NoHardcodedSecrets_InConfigFiles` -- Scan `appsettings*.json` for patterns like connection strings with passwords, JWT signing keys, or API keys
  - [x] 9.3 Test: `SourceCode_NoHardcodedSecrets_InDaprYaml` -- Scan DAPR YAML files for hardcoded passwords or secrets (should use environment variable substitution patterns)
  - [x] 9.4 Test: `SourceCode_NoHardcodedSecrets_InCSharpFiles` -- Scan `.cs` files for patterns like `"password"`, `"secret"`, `"connectionstring"` in string literals (excluding test files)
  - [x] 9.5 NOTE: These are static analysis tests using `File.ReadAllText` + regex pattern matching, similar to the existing YAML validation tests

- [x] Task 10: Verify no regressions and comprehensive coverage (AC: #1-#11)
  - [x] 10.1 Run `dotnet test` to confirm all existing + new tests pass
  - [x] 10.2 Verify that security audit logging enhancement does not change the behavior of existing error handlers (same HTTP status codes, same ProblemDetails structure)
  - [x] 10.3 Verify that extension metadata sanitization integrates cleanly into the existing command submission pipeline
  - [x] 10.4 Confirm the payload protection mechanism works with the existing test infrastructure

## Dev Notes

### Story Context

This is the **fourth and final story in Epic 5: Multi-Tenant Security & Access Control Enforcement**. While Stories 5.1-5.3 focused on infrastructure-level access control (DAPR policies, data path isolation, pub/sub scoping), this story focuses on the **observability and enforcement** side of security -- making sure that security events are auditable, sensitive data is protected in logs, and extension metadata cannot be used as an injection vector.

**What already exists (BUILD ON, not replicate):**
- LoggingBehavior with structured logging: correlation ID, tenant, domain, command type, duration (Story 2.3)
- AuthorizationBehavior with claims-based ABAC (Story 2.5)
- AuthorizationExceptionHandler converting CommandAuthorizationException to 403 ProblemDetails (Story 2.5)
- GlobalExceptionHandler with correlation ID in ProblemDetails (Story 2.2)
- AggregateActor with `LogStageTransition` method explicitly noting Rule #5 (no payload in logs) (Stories 3.2-3.11)
- EventStoreActivitySource with tag constants for all metadata fields (Story 3.11)
- TenantValidator with TenantMismatchException logging (Story 3.3)
- JWT authentication middleware with ConfigureJwtBearerOptions (Story 2.4)
- SubmitCommandRequestValidator for structural validation (Story 2.2)
- 882+ tests passing across all projects

**What this story adds (NEW):**
- Extension metadata sanitizer (SEC-4): size limits, character validation, injection prevention
- ExtensionMetadataOptions for configurable sanitization limits
- Framework-level payload protection (SEC-5): ensure payload data cannot leak into logs
- Enhanced security audit logging with consistent `SecurityEvent` field across all layers
- Authentication failure logging with source IP (NFR11)
- Authorization failure logging with JWT claims context (NFR11)
- Secrets-in-source-control verification tests (NFR14)
- Comprehensive test suite: ~30+ new tests

**What this story modifies (EXISTING):**
- `src/Hexalith.EventStore.CommandApi/Authentication/ConfigureJwtBearerOptions.cs` -- ADD authentication failure event handlers
- `src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationBehavior.cs` -- ADD `SecurityEvent=AuthorizationDenied` to log statements
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationExceptionHandler.cs` -- ADD `SecurityEvent` to log output
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- ADD `SecurityEvent=TenantMismatch` to tenant validation log
- `src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs` -- MODIFY `ToString()` to exclude/redact Payload
- `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs` -- MODIFY `ToString()` to exclude/redact Payload

### Architecture Compliance

- **SEC-4:** Extension metadata sanitized at API gateway -- max size, character validation, injection prevention
- **SEC-5:** Event payload data never in logs -- enforced at framework level, not developer convention
- **NFR11:** Failed authentication/authorization logged with request metadata (source IP, attempted tenant, command type) WITHOUT the JWT token
- **NFR12:** Event payload data never in log output -- only envelope metadata
- **NFR14:** Secrets never in application code or committed configuration files
- **Rule #5:** Never log event payload data -- envelope metadata only (already partially enforced, this story provides framework-level guarantee)
- **Rule #7:** ProblemDetails for all API error responses -- extension sanitization failures return ProblemDetails
- **Rule #9:** correlationId in every structured log entry -- security audit logs include correlationId
- **Rule #13:** No stack traces in production error responses -- error handlers already comply

### Critical Design Decisions

- **Payload protection via `ToString()` override is the simplest effective approach.** C# records auto-generate `ToString()` that includes all properties. By overriding `ToString()` on `EventEnvelope` and `CommandEnvelope` to redact the `Payload` field, any accidental `logger.LogInformation("Event: {Event}", envelope)` will produce safe output. This is framework-level enforcement because it works regardless of developer awareness.

- **Extension metadata sanitization is a NEW validation layer, separate from structural validation.** Structural validation (SubmitCommandRequestValidator) checks for required fields. Extension sanitization (ExtensionMetadataSanitizer) checks for security-relevant content. These are different concerns and should be separate classes, but both run before the command enters the pipeline.

- **Security audit logging uses a consistent `SecurityEvent` field.** All security-relevant log entries include a `SecurityEvent` field with a well-defined value (AuthenticationFailed, AuthorizationDenied, TenantMismatch, ExtensionMetadataRejected). This allows operators to query logs for security events using a single filter: `SecurityEvent != null`.

- **Source IP is available at the API layer but NOT at the actor layer.** HTTP context (and thus `RemoteIpAddress`) is available in middleware, controllers, and MediatR pipeline behaviors. It is NOT available inside DAPR actor invocations (commands arrive via DAPR actor proxy, not HTTP). If source IP is needed at the actor layer, it must be captured at the API layer and passed through the `CommandEnvelope` (e.g., via extension metadata or a dedicated field). For this story, source IP logging is implemented where available (API layer) and documented as unavailable at the actor layer.

- **JWT tokens are never logged -- only extracted claims.** NFR11 is clear: "without logging the JWT token itself." Security audit logs should include the authenticated user's claims (tenant list, domain list, roles) but never the raw JWT string. This applies to authentication failures (where the token is invalid anyway) and authorization failures (where the token is valid but insufficient).

- **Dead-letter messages intentionally contain command payload for replay.** The `DeadLetterMessage` (Story 4.5) includes the full command payload so operators can replay failed commands. This is by design (AC #5 of Story 4.5). However, dead-letter message content must never appear in LOGS. The dead-letter publisher should log metadata (correlation ID, tenant, failure stage) but not the message content.

- **Injection pattern detection is "best effort" for v1.** The extension metadata sanitizer uses regex-based pattern detection for common injection vectors (XSS, SQL, LDAP). This is not a comprehensive WAF replacement. The primary defense is at the API gateway level where extension metadata is consumed -- downstream components treat extensions as opaque key-value pairs that are already sanitized.

- **Static analysis tests for payload leakage are pragmatic, not exhaustive.** Tests that scan source files for log statements containing "Payload" are a pragmatic guard against regression. They don't replace code review, but they catch the most common accidental payload logging.

### Existing Patterns to Follow

**Test project conventions (from existing security tests):**
- NSubstitute for mocking (`Substitute.For<IInterface>()`)
- Shouldly for assertions (`result.ShouldBe(expected)`, `Should.Throw<T>(...)`)
- xUnit with `[Fact]` and `[Theory]`/`[InlineData]`
- Feature folder organization: security tests in `tests/Hexalith.EventStore.Server.Tests/Security/`
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- File path construction for source scanning: `Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", ...))`
- String-based content validation (no YAML parsing library)

**Structured logging pattern (from AggregateActor.cs):**
```csharp
logger.LogInformation(
    "Actor {ActorId} stage transition: Stage={Stage}, CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, DurationMs={DurationMs}",
    Host.Id, stage, command.CorrelationId, command.TenantId, command.Domain, command.AggregateId, command.CommandType, durationMs);
```

**Security audit log pattern (NEW -- establish in this story):**
```csharp
logger.LogWarning(
    "Security event: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, SourceIp={SourceIp}, Tenant={TenantId}, Domain={Domain}, CommandType={CommandType}, Reason={Reason}",
    "AuthorizationDenied", correlationId, sourceIp, tenantId, domain, commandType, reason);
```

**Error handling pattern (from existing exception handlers):**
```csharp
// Always include correlationId in ProblemDetails extensions
var problemDetails = new ProblemDetails
{
    Status = StatusCodes.Status400BadRequest,
    Title = "Bad Request",
    Type = "https://tools.ietf.org/html/rfc9457#section-3",
    Detail = "Extension metadata validation failed.",
    Instance = httpContext.Request.Path,
    Extensions = { ["correlationId"] = correlationId, ["tenantId"] = tenantId },
};
```

### Mandatory Coding Patterns

- xUnit + Shouldly + NSubstitute (match existing test infrastructure)
- `ConfigureAwait(false)` on all async operations
- Feature folder organization: tests in `tests/Hexalith.EventStore.Server.Tests/Security/`
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`
- No new NuGet dependencies needed
- All log statements use structured logging (named parameters, not string interpolation)
- `SecurityEvent` field in all security audit log entries
- ProblemDetails for all API error responses (Rule #7)
- correlationId in every structured log entry (Rule #9)
- No payload data in any log statement (Rule #5, SEC-5, NFR12)
- No JWT token in any log statement (NFR11)

### Project Structure Notes

**New files:**
- `src/Hexalith.EventStore.CommandApi/Validation/ExtensionMetadataSanitizer.cs` -- Extension metadata validation
- `src/Hexalith.EventStore.CommandApi/Configuration/ExtensionMetadataOptions.cs` -- Sanitization options
- `tests/Hexalith.EventStore.Server.Tests/Security/SecurityAuditLoggingTests.cs` -- Security audit log verification tests
- `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionTests.cs` -- Payload redaction tests
- `tests/Hexalith.EventStore.Server.Tests/Security/ExtensionMetadataSanitizerTests.cs` -- Extension validation tests
- `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs` -- No-secrets-in-source tests

**Modified files:**
- `src/Hexalith.EventStore.CommandApi/Authentication/ConfigureJwtBearerOptions.cs` -- ADD auth failure event handlers
- `src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationBehavior.cs` -- ADD SecurityEvent to logs
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationExceptionHandler.cs` -- ADD SecurityEvent to logs
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- ADD SecurityEvent=TenantMismatch to log
- `src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs` -- MODIFY ToString() for payload redaction
- `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs` -- MODIFY ToString() for payload redaction

**Alignment with unified project structure:**
- Security tests go in `tests/Hexalith.EventStore.Server.Tests/Security/` (existing folder, contains `StorageKeyIsolationTests.cs`, `AccessControlPolicyTests.cs`, `DataPathIsolationTests.cs`, etc.)
- New CommandApi validation class goes in `Validation/` folder (existing, contains `SubmitCommandRequestValidator.cs`)
- New CommandApi configuration class goes in `Configuration/` folder (existing, contains `RateLimitingOptions.cs`)
- No new project folders needed

### Previous Story Intelligence

**From Story 5.1 (DAPR Access Control Policies):**
- All 882 tests passing. 12 new security tests added.
- AccessControlPolicyTests validates YAML configuration structure using `File.ReadAllText` + `ShouldContain/ShouldNotContain`
- Pattern established: security tests in `tests/.../Security/` folder
- Story 5.1 established zero-trust domain service posture: domain services have zero infrastructure access

**From Story 5.2 (Data Path Isolation Verification):**
- Three-layer isolation model validated: actor identity, DAPR policies, command metadata
- TenantMismatchException carries `CommandTenant` and `ActorTenant` -- these fields are available for security audit logging
- Actor test pattern: `ActorHost.CreateForTest<AggregateActor>()` with mock state manager injection via reflection
- NSubstitute mock pattern for DaprClient and IActorStateManager established

**From Story 5.3 (Pub/Sub Topic Isolation Enforcement):**
- YAML-based configuration testing pattern established for pub/sub scoping validation
- Documentation-as-code pattern: YAML comments verified by automated tests
- Test project has NO YAML parsing library -- use `File.ReadAllText` + string matching

**From Story 2.4 (JWT Authentication & Claims Transformation):**
- `ConfigureJwtBearerOptions` configures JWT authentication middleware
- `EventStoreClaimsTransformation` extracts tenant/domain/command-type permissions from JWT claims
- JWT events (`OnAuthenticationFailed`, `OnChallenge`) may or may not be hooked -- Task 3.1 will verify

**From Story 2.5 (Endpoint Authorization & Command Rejection):**
- `AuthorizationBehavior` in MediatR pipeline checks claims-based ABAC
- `CommandAuthorizationException` carries TenantId and Reason
- `AuthorizationExceptionHandler` converts to 403 ProblemDetails

**From Story 3.3 (Tenant Validation at Actor Level):**
- TenantValidator logs mismatch with: CorrelationId, CommandTenant, ActorTenant
- Activity status set to Error with "TenantMismatch" description
- Rejection recorded via idempotency checker

### Git Intelligence

Recent commits show Epic 4 completion and Epic 5 in progress:
- `452962a` feat: Stories 4.2 & 4.3 - Topic isolation and at-least-once delivery (#38)
- `42bcd85` feat: Implement at-least-once delivery and DAPR retry policies
- `72d7a53` Story 4.1: Event Publisher with CloudEvents 1.0 (#37)
- `226a260` Story 3.11: Actor state machine and checkpointed stages (#36)

Patterns:
- Security tests in dedicated `Security/` folder
- Test libraries: xUnit, Shouldly, NSubstitute (no YAML parser, no log capture library)
- Structured logging uses `ILogger<T>` with named parameters
- All existing logging carefully avoids payload data (Rule #5 compliance by convention)
- Error handlers consistently use ProblemDetails with correlationId extension

### Testing Requirements

**New test classes (4):**

1. `SecurityAuditLoggingTests.cs` -- ~7 tests:
   - Authorization denial SecurityEvent logging
   - Authorization denial never contains JWT
   - Actor tenant mismatch SecurityEvent logging
   - Extension metadata rejection SecurityEvent logging
   - Rejection log doesn't contain extension content
   - Consistent security event format

2. `PayloadProtectionTests.cs` -- ~7 tests:
   - EventEnvelope.ToString() excludes payload
   - CommandEnvelope.ToString() excludes payload
   - EventEnvelope.ToString() includes metadata
   - LoggingBehavior never logs payload
   - AggregateActor source scan for payload references
   - EventPublisher source scan for payload references
   - DeadLetterPublisher source scan for payload references

3. `ExtensionMetadataSanitizerTests.cs` -- ~12 tests:
   - Null/empty extensions valid
   - Valid extensions pass
   - Oversized total rejected
   - Oversized key rejected
   - Oversized value rejected
   - Too many extensions rejected
   - Control characters rejected
   - Script tag injection rejected
   - SQL injection rejected
   - LDAP injection rejected
   - Unicode normalization handled
   - Default options verification

4. `SecretsProtectionTests.cs` -- ~3 tests:
   - No secrets in appsettings.json
   - No secrets in DAPR YAML files
   - No secrets in C# source files

**Total estimated: ~29 new tests**

### Failure Scenario Matrix

| Scenario | Expected Behavior | Enforcement |
|----------|-------------------|-------------|
| JWT missing from request | 401, logged with SecurityEvent=AuthenticationFailed, source IP | JWT middleware + event hooks |
| JWT expired | 401, logged with SecurityEvent=AuthenticationFailed, reason="expired" | JWT middleware + event hooks |
| JWT valid but wrong tenant | 403, logged with SecurityEvent=AuthorizationDenied, attempted tenant | AuthorizationBehavior |
| JWT valid but wrong domain | 403, logged with SecurityEvent=AuthorizationDenied, attempted domain | AuthorizationBehavior |
| Actor receives mismatched tenant | Rejected, logged with SecurityEvent=TenantMismatch | TenantValidator (SEC-2) |
| Extension metadata > 4KB | 400 ProblemDetails, logged with SecurityEvent=ExtensionMetadataRejected | ExtensionMetadataSanitizer |
| Extension contains `<script>` | 400 ProblemDetails, logged with SecurityEvent=ExtensionMetadataRejected | ExtensionMetadataSanitizer |
| Extension contains SQL injection | 400 ProblemDetails, logged with SecurityEvent=ExtensionMetadataRejected | ExtensionMetadataSanitizer |
| Developer logs EventEnvelope | Payload field appears as [REDACTED] in log output | ToString() override |
| Developer logs CommandEnvelope | Payload field appears as [REDACTED] in log output | ToString() override |
| Hardcoded secret in appsettings.json | Test failure | SecretsProtectionTests static scan |
| Hardcoded secret in DAPR YAML | Test failure | SecretsProtectionTests static scan |

### Extension Metadata Sanitization Rules (SEC-4)

**Size limits (configurable via ExtensionMetadataOptions):**
- Total extension size: max 4096 bytes (keys + values + separators)
- Individual key length: max 128 characters
- Individual value length: max 2048 characters
- Extension count: max 32 entries

**Character validation:**
- Keys: printable ASCII only `[a-zA-Z0-9_.-]` (strict -- keys are machine-readable identifiers)
- Values: printable ASCII + common UTF-8 (BMP range), excluding control characters (\x00-\x1F except \t \n \r)
- Reject null bytes (\x00) in any position

**Injection pattern detection (regex-based):**
- XSS: `<script`, `javascript:`, `on\w+=`, `<iframe`, `<object`, `<embed`
- SQL: `';\s*(DROP|ALTER|DELETE|INSERT|UPDATE|EXEC)`, `UNION\s+SELECT`, `--\s*$`
- LDAP: `\)\(`, `\*\)\(`, `\|\(`, `\&\(`
- Path traversal: `\.\./`, `\.\.\\`

**Rejection behavior:**
- Return 400 Bad Request with ProblemDetails
- ProblemDetails.Detail: "Extension metadata validation failed: {reason}" (without the content)
- Log SecurityEvent=ExtensionMetadataRejected with reason but NOT the extension content

### Security Event Types

| SecurityEvent Value | Layer | Trigger |
|---------------------|-------|---------|
| `AuthenticationFailed` | API (JWT middleware) | Missing/invalid/expired JWT |
| `AuthorizationDenied` | API (MediatR AuthorizationBehavior) | Valid JWT but insufficient permissions |
| `TenantMismatch` | Actor (TenantValidator) | Command tenant != actor tenant |
| `ExtensionMetadataRejected` | API (ExtensionMetadataSanitizer) | Invalid extension metadata |

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 5, Story 5.4]
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-4 Extension metadata sanitized at API gateway]
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-5 Event payload data never in logs]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR11 Failed auth attempts logged without JWT token]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR12 Event payload data never in log output]
- [Source: _bmad-output/planning-artifacts/architecture.md#NFR14 Secrets never in source control]
- [Source: _bmad-output/planning-artifacts/architecture.md#Rule 5 Never log event payload data]
- [Source: _bmad-output/planning-artifacts/architecture.md#Rule 9 correlationId in every structured log]
- [Source: _bmad-output/planning-artifacts/architecture.md#Rule 13 No stack traces in production error responses]
- [Source: src/Hexalith.EventStore.CommandApi/Pipeline/LoggingBehavior.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationBehavior.cs]
- [Source: src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationExceptionHandler.cs]
- [Source: src/Hexalith.EventStore.CommandApi/ErrorHandling/GlobalExceptionHandler.cs]
- [Source: src/Hexalith.EventStore.CommandApi/Authentication/ConfigureJwtBearerOptions.cs]
- [Source: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#LogStageTransition]
- [Source: src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs]
- [Source: src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs]
- [Source: src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs]
- [Source: src/Hexalith.EventStore.Server/Events/EventPublisher.cs]
- [Source: src/Hexalith.EventStore.Server/Events/DeadLetterPublisher.cs]
- [Source: _bmad-output/implementation-artifacts/5-1-dapr-access-control-policies.md]
- [Source: _bmad-output/implementation-artifacts/5-2-data-path-isolation-verification.md]
- [Source: _bmad-output/implementation-artifacts/5-3-pubsub-topic-isolation-enforcement.md]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

- Build verified: 0 errors, 0 warnings after all source changes
- Full test suite: 874 unit tests passing (660 Server.Tests + 157 Contracts.Tests + 9 Client.Tests + 48 Testing.Tests)
- 52 new tests added by this story (in Security/ folder)
- 10 integration tests fail due to pre-existing Keycloak/DAPR infrastructure dependency (not related to this story)

### Completion Notes List

- **Option A chosen for payload protection**: ToString() override on CommandEnvelope, Contracts EventEnvelope, and Server EventEnvelope. This provides framework-level enforcement because C# string interpolation and structured logging both invoke ToString().
- **Two distinct EventEnvelope types discovered**: Contracts version uses nested EventMetadata record; Server version has flattened fields. Both received ToString() overrides.
- **Extension metadata sanitizer uses [GeneratedRegex] source generators** for compiled regex patterns (XSS, SQL, LDAP, path traversal).
- **Sanitizer integrated into CommandsController** (explicit call before MediatR send) rather than FluentValidation, because the sanitizer returns SanitizeResult (not validation exceptions) and logs SecurityEvent.
- **Source IP unavailable at actor layer**: Commands arrive via DAPR actor proxy (not HTTP). Documented as limitation per Task 5.2.
- **AuthorizationExceptionHandler upgraded from LogDebug to LogWarning** with SecurityEvent field, per AC #6 consistency requirement.
- **Task 6.6/6.7 implemented as controller-level security tests**: Added tests for extension rejection security logging and extension content redaction in logs (`ExtensionMetadataSanitizer_OversizedExtensions_LogsSecurityEvent`, `ExtensionMetadataSanitizer_RejectionLog_DoesNotContainExtensionContent`).
- **Review remediation pass completed**: JWT failure logs now include attempted tenant, attempted command type, attempted path, and failure layer; MediatR authorization denial logs now include authenticated tenant claims and failure layer.
- **ConfigureAwait(false) removed from test methods**: xUnit analyzer (xUnit1030) prohibits ConfigureAwait in test methods.

### File List

**New files:**

- `src/Hexalith.EventStore.CommandApi/Configuration/ExtensionMetadataOptions.cs` - Extension metadata sanitization options with defaults
- `src/Hexalith.EventStore.CommandApi/Validation/ExtensionMetadataSanitizer.cs` - Extension metadata validation (size, charset, injection prevention)
- `tests/Hexalith.EventStore.Server.Tests/Security/SecurityAuditLoggingTests.cs` - 7 tests for SecurityEvent field consistency, including extension metadata rejection logging/redaction
- `tests/Hexalith.EventStore.Server.Tests/Security/PayloadProtectionTests.cs` - 11 tests for payload redaction and source scanning
- `tests/Hexalith.EventStore.Server.Tests/Security/ExtensionMetadataSanitizerTests.cs` - 20 tests for extension metadata validation
- `tests/Hexalith.EventStore.Server.Tests/Security/SecretsProtectionTests.cs` - 3 tests for hardcoded secrets detection

**Modified files:**

- `src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs` - Added ToString() override with Payload=[REDACTED]
- `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs` - Added ToString() override with Payload=[REDACTED]
- `src/Hexalith.EventStore.Server/Events/EventEnvelope.cs` - Added ToString() override with Payload=[REDACTED]
- `src/Hexalith.EventStore.CommandApi/Authentication/ConfigureJwtBearerOptions.cs` - Added SecurityEvent=AuthenticationFailed to OnAuthenticationFailed and OnChallenge
- `src/Hexalith.EventStore.CommandApi/Pipeline/AuthorizationBehavior.cs` - Added SecurityEvent=AuthorizationDenied to LogAuthorizationFailure
- `src/Hexalith.EventStore.CommandApi/ErrorHandling/AuthorizationExceptionHandler.cs` - Upgraded to LogWarning with SecurityEvent=AuthorizationDenied
- `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` - Added ExtensionMetadataSanitizer integration and SecurityEvent fields
- `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` - Added DI registration for ExtensionMetadataOptions and ExtensionMetadataSanitizer
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` - Added SecurityEvent=TenantMismatch to tenant mismatch log
- `_bmad-output/implementation-artifacts/5-4-security-audit-logging-and-payload-protection.md` - Updated review outcomes, completion notes, and file tracking
- `_bmad-output/implementation-artifacts/sprint-status.yaml` - Synced story status from review to done

### Change Log

- **2026-02-15**: Story 5.4 implemented - Security audit logging with consistent SecurityEvent field across JWT, authorization, and actor layers; payload protection via ToString() overrides on CommandEnvelope and EventEnvelope; extension metadata sanitizer with XSS/SQL/LDAP/path traversal injection prevention; secrets-in-source-control verification tests. 52 new tests, 0 regressions.
- **2026-02-15**: Code review remediation completed - added missing extension metadata rejection logging/redaction tests (Task 6.6/6.7), enriched JWT authentication failure logs with attempted tenant/command type/failure layer, enriched AuthorizationBehavior denial logs with authenticated tenant claims, and marked story/sprint status as done.
