
using System.Security.Claims;

using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline;

public class AuthorizationBehaviorTests {
    private readonly List<LogEntry> _logEntries = [];
    private readonly TestLogger<AuthorizationBehavior<SubmitCommand, SubmitCommandResult>> _logger;

    public AuthorizationBehaviorTests() => _logger = new TestLogger<AuthorizationBehavior<SubmitCommand, SubmitCommandResult>>(_logEntries);

    private static SubmitCommand CreateTestCommand(
        string tenant = "test-tenant",
        string domain = "test-domain",
        string commandType = "CreateOrder") =>
        new(
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
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);
        return new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, _logger);
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
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        _ = accessor.HttpContext.Returns(httpContext);
        var otherLogger = new TestLogger<AuthorizationBehavior<PingRequest, PingResponse>>([]);
        var behavior = new AuthorizationBehavior<PingRequest, PingResponse>(accessor, otherLogger);
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
        LogEntry warningLog = _logEntries.First(e => e.Level == LogLevel.Warning);
        warningLog.Message.ShouldContain("test-correlation-id");
        warningLog.Message.ShouldContain("test-tenant");
        warningLog.Message.ShouldContain("test-domain");

        // JWT token content must never appear in logs (NFR11)
        foreach (LogEntry entry in _logEntries) {
            entry.Message.ShouldNotContain("Bearer");
            entry.Message.ShouldNotContain("eyJ"); // JWT prefix
        }
    }

    // Helper types for non-SubmitCommand test
    public record PingRequest : IRequest<PingResponse>;

    public record PingResponse;

    private sealed class TestLogger<T>(List<LogEntry> entries) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}
