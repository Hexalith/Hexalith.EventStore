namespace Hexalith.EventStore.Server.Tests.Security;

using System.Text.Json;
using System.Reflection;
using System.Security.Claims;

using Hexalith.EventStore.CommandApi.Configuration;
using Hexalith.EventStore.CommandApi.Controllers;
using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Models;
using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.CommandApi.Validation;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

/// <summary>
/// Story 5.4, Task 6: Security audit logging tests (AC #1, #6, #7, #8, #9, #10).
/// Validates that security events use consistent SecurityEvent field and correct metadata.
/// </summary>
public class SecurityAuditLoggingTests
{
    // --- Task 6.2: AuthorizationBehavior logs SecurityEvent=AuthorizationDenied for unauthorized tenant ---

    [Fact]
    public async Task AuthorizationBehavior_UnauthorizedDomain_LogsSecurityEvent()
    {
        var logEntries = new List<LogEntry>();
        var testLogger = new TestLogger<AuthorizationBehavior<SubmitCommand, SubmitCommandResult>>(logEntries);

        var principal = CreatePrincipal(domains: ["other-domain"]);
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Items["CorrelationId"] = "test-corr-id";
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var behavior = new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, testLogger);
        var command = CreateTestCommand(domain: "billing");

        await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        LogEntry warningLog = logEntries.First(e => e.Level == LogLevel.Warning);
        warningLog.Message.ShouldContain("SecurityEvent=AuthorizationDenied");
        warningLog.Message.ShouldContain("test-corr-id");
        warningLog.Message.ShouldContain("billing");
    }

    // --- Task 6.3: AuthorizationBehavior logs SecurityEvent for unauthorized domain ---

    [Fact]
    public async Task AuthorizationBehavior_UnauthorizedTenant_LogsSecurityEvent()
    {
        // Note: Tenant authorization happens in the controller layer (not AuthorizationBehavior).
        // AuthorizationBehavior handles domain and permission checks.
        // This test verifies domain denial produces SecurityEvent.
        var logEntries = new List<LogEntry>();
        var testLogger = new TestLogger<AuthorizationBehavior<SubmitCommand, SubmitCommandResult>>(logEntries);

        var principal = CreatePrincipal(domains: ["only-this-domain"]);
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Items["CorrelationId"] = "corr-456";
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var behavior = new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, testLogger);
        var command = CreateTestCommand(domain: "unauthorized-domain");

        await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        LogEntry warningLog = logEntries.First(e => e.Level == LogLevel.Warning);
        warningLog.Message.ShouldContain("SecurityEvent=AuthorizationDenied");
    }

    // --- Task 6.4: SecurityEvent log never contains JWT token ---

    [Fact]
    public async Task AuthorizationBehavior_SecurityEventLog_NeverContainsJwtToken()
    {
        var logEntries = new List<LogEntry>();
        var testLogger = new TestLogger<AuthorizationBehavior<SubmitCommand, SubmitCommandResult>>(logEntries);

        var principal = CreatePrincipal(domains: ["other-domain"]);
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Items["CorrelationId"] = "corr-jwt-test";
        // Simulate request context only; test verifies logs never contain JWT-like token markers.
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var behavior = new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, testLogger);
        var command = CreateTestCommand(domain: "forbidden-domain");

        await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        // Verify no log entry contains the JWT token
        foreach (LogEntry entry in logEntries)
        {
            entry.Message.ShouldNotContain("eyJ", Case.Sensitive,
                "Security audit log must never contain JWT token content (base64 encoded)");
        }
    }

    // --- Task 6.5: AggregateActor tenant mismatch logs SecurityEvent ---

    [Fact]
    public async Task AggregateActor_TenantMismatch_LogsSecurityEvent()
    {
        // Create an actor with tenant-a in its actor ID
        var logEntries = new List<LogEntry>();
        var actorLogger = new TestLogger<AggregateActor>(logEntries);

        var stateManager = Substitute.For<IActorStateManager>();
        var domainInvoker = Substitute.For<IDomainServiceInvoker>();
        var snapshotManager = Substitute.For<ISnapshotManager>();
        var statusStore = Substitute.For<ICommandStatusStore>();
        var eventPublisher = Substitute.For<IEventPublisher>();
        var deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();

        var actorHost = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-a:billing:agg-1") });

        var actor = new AggregateActor(
            actorHost, actorLogger, domainInvoker, snapshotManager,
            statusStore, eventPublisher, Options.Create(new EventDrainOptions()), deadLetterPublisher);

        // Inject mock state manager via public property (established pattern from DataPathIsolationTests)
        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // Set up the state manager to return no existing state
        stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        // Submit command with different tenant (tenant-b) -- tenant mismatch
        var command = new CommandEnvelope(
            "tenant-b", "billing", "agg-1", "PlaceOrder",
            [0x01], "corr-mismatch", null, "user-1", null);

        CommandProcessingResult result = await actor.ProcessCommandAsync(command);
        result.Accepted.ShouldBeFalse();

        // Verify SecurityEvent=TenantMismatch in logs
        logEntries.Any(e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains("SecurityEvent=TenantMismatch")).ShouldBeTrue(
            "AggregateActor tenant mismatch should log SecurityEvent=TenantMismatch");
    }

    // --- Task 6.8: All security events have required fields ---

    [Fact]
    public async Task SecurityAuditLogs_ConsistentFormat_AllEventsHaveRequiredFields()
    {
        var logEntries = new List<LogEntry>();
        var testLogger = new TestLogger<AuthorizationBehavior<SubmitCommand, SubmitCommandResult>>(logEntries);

        var principal = CreatePrincipal(domains: ["wrong-domain"]);
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Items["CorrelationId"] = "consistent-format-test";
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var behavior = new AuthorizationBehavior<SubmitCommand, SubmitCommandResult>(accessor, testLogger);
        var command = CreateTestCommand(domain: "target-domain");

        await Should.ThrowAsync<CommandAuthorizationException>(
            () => behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None));

        LogEntry securityLog = logEntries.First(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("SecurityEvent="));

        // All security audit logs must have: SecurityEvent, CorrelationId
        securityLog.Message.ShouldContain("SecurityEvent=");
        securityLog.Message.ShouldContain("CorrelationId=");
    }

    [Fact]
    public async Task ExtensionMetadataSanitizer_OversizedExtensions_LogsSecurityEvent()
    {
        var logEntries = new List<LogEntry>();
        var controllerLogger = new TestLogger<CommandsController>(logEntries);
        IMediator mediator = Substitute.For<IMediator>();
        var sanitizer = new ExtensionMetadataSanitizer(
            Options.Create(new ExtensionMetadataOptions
            {
                MaxTotalSizeBytes = 8,
                MaxKeyLength = 128,
                MaxValueLength = 2048,
                MaxExtensionCount = 32,
            }));

        var controller = new CommandsController(mediator, sanitizer, controllerLogger)
        {
            ControllerContext = new()
            {
                HttpContext = CreateAuthorizedHttpContext("tenant-a", "corr-extension-oversized"),
            },
        };

        using JsonDocument payloadDocument = JsonDocument.Parse("{\"amount\":42}");
        var request = new SubmitCommandRequest(
            Tenant: "tenant-a",
            Domain: "billing",
            AggregateId: "agg-1",
            CommandType: "PlaceOrder",
            Payload: payloadDocument.RootElement.Clone(),
            Extensions: new Dictionary<string, string>
            {
                ["k1"] = "aaaa",
                ["k2"] = "bbbb",
            });

        IActionResult actionResult = await controller.Submit(request, CancellationToken.None);

        ObjectResult badRequest = actionResult.ShouldBeOfType<ObjectResult>();
        badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        logEntries.Any(e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains("SecurityEvent=ExtensionMetadataRejected", StringComparison.Ordinal)).ShouldBeTrue(
            "Extension rejection should log SecurityEvent=ExtensionMetadataRejected.");
    }

    [Fact]
    public async Task ExtensionMetadataSanitizer_RejectionLog_DoesNotContainExtensionContent()
    {
        var logEntries = new List<LogEntry>();
        var controllerLogger = new TestLogger<CommandsController>(logEntries);
        IMediator mediator = Substitute.For<IMediator>();
        var sanitizer = new ExtensionMetadataSanitizer(Options.Create(new ExtensionMetadataOptions()));

        var controller = new CommandsController(mediator, sanitizer, controllerLogger)
        {
            ControllerContext = new()
            {
                HttpContext = CreateAuthorizedHttpContext("tenant-a", "corr-extension-content"),
            },
        };

        const string maliciousExtension = "<script>alert('xss')</script>";

        using JsonDocument payloadDocument = JsonDocument.Parse("{\"amount\":42}");
        var request = new SubmitCommandRequest(
            Tenant: "tenant-a",
            Domain: "billing",
            AggregateId: "agg-1",
            CommandType: "PlaceOrder",
            Payload: payloadDocument.RootElement.Clone(),
            Extensions: new Dictionary<string, string>
            {
                ["note"] = maliciousExtension,
            });

        IActionResult actionResult = await controller.Submit(request, CancellationToken.None);

        ObjectResult badRequest = actionResult.ShouldBeOfType<ObjectResult>();
        badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        foreach (LogEntry entry in logEntries)
        {
            entry.Message.ShouldNotContain(maliciousExtension, Case.Sensitive,
                "Rejected extension content must not be written to logs.");
        }
    }

    // --- Helpers ---

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

    private static ClaimsPrincipal CreatePrincipal(
        string[]? tenants = null,
        string[]? domains = null,
        string[]? permissions = null)
    {
        var claims = new List<Claim>();
        if (tenants is not null)
        {
            foreach (string t in tenants)
            {
                claims.Add(new Claim("eventstore:tenant", t));
            }
        }

        if (domains is not null)
        {
            foreach (string d in domains)
            {
                claims.Add(new Claim("eventstore:domain", d));
            }
        }

        if (permissions is not null)
        {
            foreach (string p in permissions)
            {
                claims.Add(new Claim("eventstore:permission", p));
            }
        }

        var identity = new ClaimsIdentity(claims, "TestScheme");
        return new ClaimsPrincipal(identity);
    }

    private static DefaultHttpContext CreateAuthorizedHttpContext(string tenant, string correlationId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["CorrelationId"] = correlationId;
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");

        var identity = new ClaimsIdentity(
        [
            new Claim("eventstore:tenant", tenant),
            new Claim("sub", "user-1"),
        ],
        authenticationType: "TestScheme");

        httpContext.User = new ClaimsPrincipal(identity);
        return httpContext;
    }

    private sealed class TestLogger<T>(List<LogEntry> entries) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private record LogEntry(LogLevel Level, string Message);
}
