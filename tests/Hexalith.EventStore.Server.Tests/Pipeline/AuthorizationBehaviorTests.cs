
using System.Security.Claims;

using Hexalith.EventStore.CommandApi.Authorization;
using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Pipeline.Queries;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline;

public class AuthorizationBehaviorTests {
    private readonly List<AuthorizationLogEntry> _logEntries = [];
    private readonly TestLogger<AuthorizationBehavior<SubmitCommand, SubmitCommandResult>> _logger;

    public AuthorizationBehaviorTests() => _logger = new TestLogger<AuthorizationBehavior<SubmitCommand, SubmitCommandResult>>(_logEntries);

    private static SubmitCommand CreateTestCommand(
        string tenant = "test-tenant",
        string domain = "test-domain",
        string commandType = "CreateOrder") =>
        new(
            MessageId: Guid.NewGuid().ToString(),
            Tenant: tenant,
            Domain: domain,
            AggregateId: "agg-001",
            CommandType: commandType,
            Payload: [0x01],
            CorrelationId: "test-correlation-id",
            UserId: "test-user");

    private static RequestHandlerDelegate<SubmitCommandResult> CreateSuccessDelegate() =>
        new((_) => Task.FromResult(new SubmitCommandResult("test-correlation-id")));

    private AuthorizationBehavior<SubmitCommand, SubmitCommandResult> CreateBehavior(ClaimsPrincipal principal) {
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);
        var tenantValidator = new ClaimsTenantValidator();
        var rbacValidator = new ClaimsRbacValidator();
        return new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, tenantValidator, rbacValidator, _logger);
    }

    private static ClaimsPrincipal CreatePrincipal(
        string[]? tenants = null,
        string[]? domains = null,
        string[]? permissions = null) {
        var claims = new List<Claim>();
        if (tenants is not null) {
            foreach (string t in tenants) {
                claims.Add(new Claim("eventstore:tenant", t));
            }
        }

        if (domains is not null) {
            foreach (string d in domains) {
                claims.Add(new Claim("eventstore:domain", d));
            }
        }

        if (permissions is not null) {
            foreach (string p in permissions) {
                claims.Add(new Claim("eventstore:permission", p));
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task AuthorizationBehavior_UserWithMatchingDomain_Succeeds() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"], domains: ["test-domain"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand();

        // Act
        SubmitCommandResult result = await behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
        result.CorrelationId.ShouldBe("test-correlation-id");
    }

    [Fact]
    public async Task AuthorizationBehavior_UnauthenticatedUser_ThrowsCommandAuthorizationException() {
        // Arrange - User has Identity but IsAuthenticated = false
        var identity = new ClaimsIdentity(); // No authenticationType -> IsAuthenticated is false
        var principal = new ClaimsPrincipal(identity);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand();

        // Act & Assert
        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        ex.Reason.ShouldBe("User is not authenticated.");
    }

    [Fact]
    public async Task AuthorizationBehavior_NoIdentity_ThrowsCommandAuthorizationException() {
        // Arrange - User has no Identity
        var principal = new ClaimsPrincipal();
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand();

        // Act & Assert
        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        ex.Reason.ShouldBe("User is not authenticated.");
    }

    [Fact]
    public async Task ValidateAsync_NoDomainOrPermissionClaims_OpenByDefault_Allowed() {
        // Arrange - Open-by-default design intent test
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand(domain: "any-random-domain", commandType: "AnyRandomCommand");

        // Act
        SubmitCommandResult result = await behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
        result.CorrelationId.ShouldBe("test-correlation-id");
    }

    [Fact]
    public async Task AuthorizationBehavior_SuccessLogsDebugMessage() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand(tenant: "test-tenant", domain: "test-domain", commandType: "CreateOrder");

        // Act
        _ = await behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert
        var logs = _logEntries.Where(e => e.Level == LogLevel.Debug && e.Message.Contains("Authorization succeeded")).ToList();
        logs.ShouldNotBeEmpty();
        logs[0].EventId.Id.ShouldBe(1020);
        logs[0].Message.ShouldContain("Authorization succeeded");
        logs[0].Message.ShouldContain("CorrelationId=test-correlation-id");
        logs[0].Message.ShouldContain("CausationId=test-correlation-id");
        logs[0].Message.ShouldContain("test-tenant");
        logs[0].Message.ShouldContain("test-domain");
        logs[0].Message.ShouldContain("CreateOrder");
    }

    [Fact]
    public async Task AuthorizationBehavior_RbacValidatorReturnsNull_ThrowsInvalidOperationException() {
        // Arrange
        var httpContext = new DefaultHttpContext {
            User = CreatePrincipal(tenants: ["test-tenant"]),
        };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);

        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);

        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        _ = rbacValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns((RbacValidationResult)null!);

        var behavior = new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, tenantValidator, rbacValidator, _logger);
        SubmitCommand command = CreateTestCommand(tenant: "test-tenant");

        // Act & Assert
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        ex.Message.ShouldContain("server bug");
        ex.Message.ShouldContain("IRbacValidator.ValidateAsync returned null");
    }

    [Fact]
    public async Task AuthorizationBehavior_EnsureReasonNamesTenant_DoesNotDuplicate() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"], domains: ["wrong-domain"]);

        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        _ = rbacValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(RbacValidationResult.Denied("Not authorized for tenant 'test-tenant'."));

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);

        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);

        var behavior = new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, tenantValidator, rbacValidator, _logger);
        SubmitCommand command = CreateTestCommand(tenant: "test-tenant");

        // Act & Assert
        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        ex.Reason.ShouldBe("Not authorized for tenant 'test-tenant'."); // Not duplicated
    }

    [Fact]
    public async Task AuthorizationBehavior_EnsureReasonNamesTenant_EmptyTenant_ReturnsReasonUnchanged() {
        // Arrange — empty tenant should return reason unchanged
        var httpContext = new DefaultHttpContext {
            User = CreatePrincipal(tenants: ["test-tenant"]),
        };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);

        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);

        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        _ = rbacValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(RbacValidationResult.Denied("Domain mismatch."));

        var behavior = new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, tenantValidator, rbacValidator, _logger);

        // Use empty tenant to trigger null/whitespace path in EnsureReasonNamesTenant
        SubmitCommand command = CreateTestCommand(tenant: "");

        // Act & Assert
        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        // When tenant is empty, EnsureReasonNamesTenant returns reason unchanged
        ex.Reason.ShouldBe("Domain mismatch.");
    }

    [Fact]
    public async Task AuthorizationBehavior_EnsureReasonNamesTenant_NullReason_UsesFallbackAndPrefixesTenant() {
        // Arrange — null/empty reason should use the fallback reason and include tenant context
        var httpContext = new DefaultHttpContext {
            User = CreatePrincipal(tenants: ["test-tenant"]),
        };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);

        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);

        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        _ = rbacValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(new RbacValidationResult(false, null));

        var behavior = new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, tenantValidator, rbacValidator, _logger);
        SubmitCommand command = CreateTestCommand(tenant: "test-tenant");

        // Act & Assert
        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        // When reason is null, the behavior uses "RBAC check failed." fallback, then EnsureReasonNamesTenant prepends tenant
        ex.Reason.ShouldContain("tenant 'test-tenant'");
        ex.Reason.ShouldContain("RBAC check failed.");
    }

    [Fact]
    public async Task AuthorizationBehavior_UserWithNoDomainClaims_Succeeds() {
        // Arrange - no domain claims means all domains authorized (AC #5)
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand();

        // Act
        SubmitCommandResult result = await behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
    }

    [Fact]
    public async Task AuthorizationBehavior_UserWithWrongDomain_ThrowsAuthorizationException() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"], domains: ["other-domain"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand(domain: "test-domain");

        // Act & Assert
        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        ex.Domain.ShouldBe("test-domain");
        ex.Reason.ShouldContain("tenant 'test-tenant'");
        ex.Reason.ShouldContain("domain");
    }

    [Fact]
    public async Task AuthorizationBehavior_UserWithMatchingPermission_Succeeds() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"], permissions: ["CreateOrder"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand(commandType: "CreateOrder");

        // Act
        SubmitCommandResult result = await behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
    }

    [Fact]
    public async Task AuthorizationBehavior_UserWithNoPermissionClaims_Succeeds() {
        // Arrange - no permission claims means all command types authorized (AC #5)
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand();

        // Act
        SubmitCommandResult result = await behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
    }

    [Fact]
    public async Task AuthorizationBehavior_UserWithWrongPermission_ThrowsAuthorizationException() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"], permissions: ["OtherCommand"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand(commandType: "CreateOrder");

        // Act & Assert
        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        ex.CommandType.ShouldBe("CreateOrder");
        ex.Reason.ShouldContain("tenant 'test-tenant'");
        ex.Reason.ShouldContain("command type");
    }

    [Fact]
    public async Task AuthorizationBehavior_UserWithWildcardPermission_Succeeds() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"], permissions: ["commands:*"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand(commandType: "AnyCommandType");

        // Act
        SubmitCommandResult result = await behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
    }

    [Fact]
    public async Task AuthorizationBehavior_NonSubmitCommandRequest_PassesThrough() {
        // Arrange - use a non-SubmitCommand MediatR request
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);
        var otherLogger = new TestLogger<AuthorizationBehavior<PingRequest, PingResponse>>([]);
        var tenantValidator = new ClaimsTenantValidator();
        var rbacValidator = new ClaimsRbacValidator();
        var behavior = new AuthorizationBehavior<PingRequest, PingResponse>(accessor, tenantValidator, rbacValidator, otherLogger);
        var next = new RequestHandlerDelegate<PingResponse>((_) => Task.FromResult(new PingResponse()));

        // Act
        PingResponse result = await behavior.Handle(new PingRequest(), next, CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
    }

    [Fact]
    public async Task AuthorizationBehavior_CaseInsensitiveDomainMatch_Succeeds() {
        // Arrange - domain claim in different case
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"], domains: ["TEST-DOMAIN"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand(domain: "test-domain");

        // Act
        SubmitCommandResult result = await behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
    }

    [Fact]
    public async Task AuthorizationBehavior_FailedAuth_LogsWarningWithoutJwtToken() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"], domains: ["other-domain"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand(domain: "test-domain");

        // Act
        _ = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        // Assert
        AuthorizationLogEntry warningLog = _logEntries.First(e => e.Level == LogLevel.Warning);
        warningLog.Message.ShouldContain("test-correlation-id");
        warningLog.Message.ShouldContain("test-tenant");
        warningLog.Message.ShouldContain("test-domain");

        // JWT token content must never appear in logs (NFR11)
        foreach (AuthorizationLogEntry entry in _logEntries) {
            entry.Message.ShouldNotContain("Bearer");
            entry.Message.ShouldNotContain("eyJ"); // JWT prefix
        }
    }

    [Fact]
    public async Task AuthorizationBehavior_UserWithNoTenantClaims_ThrowsAuthorizationException() {
        // Arrange — authenticated user with no tenant claims (now caught by behavior via ITenantValidator)
        ClaimsPrincipal principal = CreatePrincipal(tenants: null);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand();

        // Act & Assert
        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        ex.Reason.ShouldContain("tenant 'test-tenant'");
        ex.Reason.ShouldContain("No tenant authorization claims");
    }

    [Fact]
    public async Task AuthorizationBehavior_UserWithWrongTenant_ThrowsAuthorizationException() {
        // Arrange — tenant mismatch (case-SENSITIVE comparison)
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["other-tenant"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand(tenant: "test-tenant");

        // Act & Assert
        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        ex.Reason.ShouldContain("Not authorized for tenant");
    }

    [Fact]
    public async Task AuthorizationBehavior_UserWithMatchingTenant_Succeeds() {
        // Arrange — matching tenant claim
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand(tenant: "test-tenant");

        // Act
        SubmitCommandResult result = await behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
        result.CorrelationId.ShouldBe("test-correlation-id");
    }

    [Fact]
    public async Task AuthorizationBehavior_TenantPasses_RbacFails_ThrowsAuthorizationException() {
        // Arrange — tenant matches, but domain does not (sequential tenant-then-RBAC flow)
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"], domains: ["other-domain"]);
        AuthorizationBehavior<SubmitCommand, SubmitCommandResult> behavior = CreateBehavior(principal);
        SubmitCommand command = CreateTestCommand(tenant: "test-tenant", domain: "test-domain");

        // Act & Assert
        CommandAuthorizationException ex = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        // Should fail on RBAC (domain), not tenant
        ex.Reason.ShouldContain("tenant 'test-tenant'");
        ex.Reason.ShouldContain("domain");
    }

    [Fact]
    public async Task AuthorizationBehavior_CallsTenantValidatorWithCorrectParameters() {
        // Arrange — use NSubstitute mocks to verify delegation parameters
        var httpContext = new DefaultHttpContext {
            User = CreatePrincipal(tenants: ["test-tenant"]),
        };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);

        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);

        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        _ = rbacValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(RbacValidationResult.Allowed);

        var behavior = new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, tenantValidator, rbacValidator, _logger);
        SubmitCommand command = CreateTestCommand(tenant: "test-tenant", domain: "test-domain", commandType: "CreateOrder");

        // Act
        _ = await behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert — verify tenant validator called with correct parameters
        _ = await tenantValidator.Received(1).ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            "test-tenant",
            Arg.Any<CancellationToken>(),
            "agg-001");
    }

    [Fact]
    public async Task AuthorizationBehavior_CallsRbacValidatorWithCorrectParameters() {
        // Arrange — use NSubstitute mocks to verify delegation parameters
        var httpContext = new DefaultHttpContext {
            User = CreatePrincipal(tenants: ["test-tenant"]),
        };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);

        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);

        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        _ = rbacValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(RbacValidationResult.Allowed);

        var behavior = new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, tenantValidator, rbacValidator, _logger);
        SubmitCommand command = CreateTestCommand(tenant: "test-tenant", domain: "test-domain", commandType: "CreateOrder");

        // Act
        _ = await behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert — verify RBAC validator called with correct parameters (including "command" category)
        _ = await rbacValidator.Received(1).ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            "test-tenant",
            "test-domain",
            "CreateOrder",
            "command",
            Arg.Any<CancellationToken>(),
            "agg-001");
    }

    // ── Query authorization tests (Story 17-5) ──

    private static SubmitQuery CreateTestQuery(
        string tenant = "test-tenant",
        string domain = "test-domain",
        string queryType = "GetOrderStatus") =>
        new(
            Tenant: tenant,
            Domain: domain,
            AggregateId: "agg-001",
            QueryType: queryType,
            Payload: [],
            CorrelationId: "test-correlation-id",
            UserId: "test-user",
            EntityId: null);

    private AuthorizationBehavior<SubmitQuery, SubmitQueryResult> CreateQueryBehavior(ClaimsPrincipal principal) {
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);
        var tenantValidator = new ClaimsTenantValidator();
        var rbacValidator = new ClaimsRbacValidator();
        var queryLogger = new TestLogger<AuthorizationBehavior<SubmitQuery, SubmitQueryResult>>(_logEntries);
        return new AuthorizationBehavior<SubmitQuery, SubmitQueryResult>(accessor, tenantValidator, rbacValidator, queryLogger);
    }

    private static RequestHandlerDelegate<SubmitQueryResult> CreateQuerySuccessDelegate() {
        System.Text.Json.JsonElement payload = System.Text.Json.JsonDocument.Parse("{}").RootElement;
        return new((_) => Task.FromResult(new SubmitQueryResult("test-correlation-id", payload)));
    }

    [Fact]
    public async Task AuthorizationBehavior_SubmitQuery_ValidUser_PassesThrough() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"]);
        AuthorizationBehavior<SubmitQuery, SubmitQueryResult> behavior = CreateQueryBehavior(principal);

        // Act
        SubmitQueryResult result = await behavior.Handle(CreateTestQuery(), CreateQuerySuccessDelegate(), CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
        result.CorrelationId.ShouldBe("test-correlation-id");
    }

    [Fact]
    public async Task AuthorizationBehavior_SubmitQuery_UnauthorizedTenant_ThrowsAuthorizationException() {
        // Arrange
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["other-tenant"]);
        AuthorizationBehavior<SubmitQuery, SubmitQueryResult> behavior = CreateQueryBehavior(principal);

        // Act & Assert
        _ = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(CreateTestQuery(), CreateQuerySuccessDelegate(), CancellationToken.None));
    }

    [Fact]
    public async Task AuthorizationBehavior_SubmitQuery_UnauthorizedRbac_ThrowsAuthorizationException() {
        // Arrange — tenant matches, domain does not
        ClaimsPrincipal principal = CreatePrincipal(tenants: ["test-tenant"], domains: ["other-domain"]);
        AuthorizationBehavior<SubmitQuery, SubmitQueryResult> behavior = CreateQueryBehavior(principal);

        // Act & Assert
        _ = await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(CreateTestQuery(), CreateQuerySuccessDelegate(), CancellationToken.None));
    }

    [Fact]
    public async Task AuthorizationBehavior_SubmitQuery_RbacValidatorCalledWithQueryCategory() {
        // Arrange — use NSubstitute mocks to verify messageCategory is "query"
        var httpContext = new DefaultHttpContext {
            User = CreatePrincipal(tenants: ["test-tenant"]),
        };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);

        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);

        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        _ = rbacValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(RbacValidationResult.Allowed);

        var queryLogger = new TestLogger<AuthorizationBehavior<SubmitQuery, SubmitQueryResult>>(_logEntries);
        var behavior = new AuthorizationBehavior<SubmitQuery, SubmitQueryResult>(accessor, tenantValidator, rbacValidator, queryLogger);

        // Act
        _ = await behavior.Handle(CreateTestQuery(), CreateQuerySuccessDelegate(), CancellationToken.None);

        // Assert — verify RBAC validator called with "query" category (NOT "command")
        _ = await rbacValidator.Received(1).ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            "test-tenant",
            "test-domain",
            "GetOrderStatus",
            "query",
            Arg.Any<CancellationToken>(),
            "agg-001");

        _ = await tenantValidator.Received(1).ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            "test-tenant",
            Arg.Any<CancellationToken>(),
            "agg-001");
    }

    // Helper types for non-SubmitCommand test
    public record PingRequest : IRequest<PingResponse>;

    public record PingResponse;

    private sealed class TestLogger<T>(List<AuthorizationLogEntry> entries) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => entries.Add(new AuthorizationLogEntry(logLevel, eventId, formatter(state, exception)));
    }

    private sealed record AuthorizationLogEntry(LogLevel Level, EventId EventId, string Message);
}
