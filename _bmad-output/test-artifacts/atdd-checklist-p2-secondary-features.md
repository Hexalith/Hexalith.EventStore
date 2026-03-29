---
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-05-validate-and-complete
lastStep: step-05-validate-and-complete
lastSaved: '2026-03-29'
storyId: p2-secondary-features
detectedStack: backend
generationMode: ai-generation
executionMode: sequential
---

# ATDD Checklist — P2 Secondary Feature Coverage

## Summary

| Metric | Value |
|--------|-------|
| Total new tests | 39 |
| Test files created | 2 |
| All tests passing | YES |
| Build errors | 0 |

## Targets

### HttpContextAdminAuthContext (Zero coverage -> 13 tests)

**File:** `tests/Hexalith.EventStore.Admin.Server.Tests/Services/HttpContextAdminAuthContextTests.cs`

| Test | Category |
|------|----------|
| GetToken_ReturnsBearerToken_WhenAuthorizationHeaderPresent | Happy path |
| GetToken_ReturnsNull_WhenNoAuthorizationHeader | Missing header |
| GetToken_ReturnsNull_WhenAuthorizationHeaderIsEmpty | Empty header |
| GetToken_ReturnsNull_WhenNotBearerScheme | Wrong scheme (Basic) |
| GetToken_ReturnsNull_WhenBearerTokenIsBlank | "Bearer   " edge case |
| GetToken_IsCaseInsensitive_ForBearerPrefix | "bearer" lowercase |
| GetToken_TrimsWhitespace_FromToken | Whitespace trimming |
| GetToken_ReturnsNull_WhenHttpContextIsNull | Null context safety |
| GetUserId_ReturnsSubClaim_WhenPresent | JWT sub claim |
| GetUserId_FallsBackToNameIdentifier_WhenNoSubClaim | ClaimTypes fallback |
| GetUserId_PrefersSubClaim_OverNameIdentifier | Priority verification |
| GetUserId_ReturnsNull_WhenNoClaims | No claims safety |
| GetUserId_ReturnsNull_WhenHttpContextIsNull | Null context safety |

### CompletionScripts Direct Unit Tests (Indirect-only -> 26 tests)

**File:** `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Config/CompletionScriptsTests.cs`

| Test | Coverage |
|------|----------|
| AllShells_ContainGeneratedDateHeader (x4) | Date header format verification |
| Shells_ContainProfileSubcommands (x3) | add/list/show/remove in bash/ps/fish |
| AllShells_ContainFormatValues (x4) | json/csv/table in all shells |
| CompletionShellChoices_Present (x3) | bash/zsh/powershell/fish choices |
| AllShells_ContainConfigSubcommands (x4) | profile/use/current/completion |
| PowerShell_UsesUserProfileEnvVar | $env:USERPROFILE for Windows |
| UnixShells_UseHomeEnvVar (x3) | HOME for Unix shells |
| AllShells_ContainBackupSubcommands (x4) | trigger/restore/validate/export-stream/import-stream |

### OpenAPI Transformers — Deferred

OpenAPI transformers (`AdminOperationTransformer`, `AdminRoleDescriptionTransformer`) require constructing `OpenApiOperationTransformerContext` with `ApiDescription` which is tightly coupled to ASP.NET Core's endpoint routing. These are already covered indirectly by the existing `AdminOpenApiDocumentTests` integration tests which verify the full OpenAPI document output including response codes and role descriptions. Direct unit testing would require complex test infrastructure for marginal additional value.

### AdminAuthorizationPolicies — No Tests Needed

`AdminAuthorizationPolicies` is a pure constants class with 3 `const string` fields. These are compile-time constants verified transitively by controller tests that use the policy names and by the OpenAPI integration tests.
