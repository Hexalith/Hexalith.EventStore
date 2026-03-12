
using System.Security.Claims;

using Hexalith.EventStore.CommandApi.Authorization;
using Hexalith.EventStore.CommandApi.Controllers;
using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Contracts.Validation;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public class QueryValidationControllerTests {
    private static (QueryValidationController Controller, ITenantValidator TenantValidator, IRbacValidator RbacValidator, List<LogEntry> Logs) CreateController(ClaimsPrincipal? principal = null) {
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        var logs = new List<LogEntry>();
        var logger = new TestLogger<QueryValidationController>(logs);

        // Default: both validators allow
        tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);
        rbacValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(RbacValidationResult.Allowed);

        var controller = new QueryValidationController(tenantValidator, rbacValidator, logger);

        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        httpContext.User = principal ?? new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "test-user")], "Bearer"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, tenantValidator, rbacValidator, logs);
    }

    private static ValidateQueryRequest CreateRequest(
        string tenant = "acme",
        string domain = "orders",
        string queryType = "GetOrderDetails",
        string? aggregateId = null) =>
        new(tenant, domain, queryType, aggregateId);

    [Fact]
    public async Task Validate_AuthorizedUser_Returns200WithAuthorized() {
        // Arrange
        (QueryValidationController controller, _, _, _) = CreateController();
        ValidateQueryRequest request = CreateRequest();

        // Act
        IActionResult result = await controller.Validate(request, CancellationToken.None);

        // Assert
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        PreflightValidationResult validationResult = okResult.Value.ShouldBeOfType<PreflightValidationResult>();
        validationResult.IsAuthorized.ShouldBeTrue();
        validationResult.Reason.ShouldBeNull();
    }

    [Fact]
    public async Task Validate_UnauthorizedTenant_Returns200WithDenied() {
        // Arrange
        (QueryValidationController controller, ITenantValidator tenantValidator, _, _) = CreateController();
        tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), "acme", Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Denied("Not authorized for tenant 'acme'."));
        ValidateQueryRequest request = CreateRequest();

        // Act
        IActionResult result = await controller.Validate(request, CancellationToken.None);

        // Assert
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        PreflightValidationResult validationResult = okResult.Value.ShouldBeOfType<PreflightValidationResult>();
        validationResult.IsAuthorized.ShouldBeFalse();
        validationResult.Reason.ShouldBe("Not authorized for tenant 'acme'.");
    }

    [Fact]
    public async Task Validate_UnauthorizedRbac_Returns200WithDenied() {
        // Arrange
        (QueryValidationController controller, _, IRbacValidator rbacValidator, _) = CreateController();
        rbacValidator.ValidateAsync(
            Arg.Any<ClaimsPrincipal>(), "acme", "orders", "GetOrderDetails", "query",
            Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(RbacValidationResult.Denied("Not authorized for query type 'GetOrderDetails' in domain 'orders'."));
        ValidateQueryRequest request = CreateRequest();

        // Act
        IActionResult result = await controller.Validate(request, CancellationToken.None);

        // Assert
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        PreflightValidationResult validationResult = okResult.Value.ShouldBeOfType<PreflightValidationResult>();
        validationResult.IsAuthorized.ShouldBeFalse();
        validationResult.Reason.ShouldBe("Not authorized for query type 'GetOrderDetails' in domain 'orders'.");
    }

    [Fact]
    public async Task Validate_RbacCalledWithQueryCategory() {
        // Arrange
        (QueryValidationController controller, _, IRbacValidator rbacValidator, _) = CreateController();
        ValidateQueryRequest request = CreateRequest();

        // Act
        _ = await controller.Validate(request, CancellationToken.None);

        // Assert — verify messageCategory is "query"
        await rbacValidator.Received(1).ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            "acme",
            "orders",
            "GetOrderDetails",
            "query",
            Arg.Any<CancellationToken>(),
            null);
    }

    [Fact]
    public async Task Validate_NullAggregateId_Succeeds() {
        // Arrange
        (QueryValidationController controller, ITenantValidator tenantValidator, IRbacValidator rbacValidator, _) = CreateController();
        ValidateQueryRequest request = CreateRequest(aggregateId: null);

        // Act
        IActionResult result = await controller.Validate(request, CancellationToken.None);

        // Assert
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        PreflightValidationResult validationResult = okResult.Value.ShouldBeOfType<PreflightValidationResult>();
        validationResult.IsAuthorized.ShouldBeTrue();

        await tenantValidator.Received(1).ValidateAsync(
            Arg.Any<ClaimsPrincipal>(), "acme", Arg.Any<CancellationToken>(), null);
        await rbacValidator.Received(1).ValidateAsync(
            Arg.Any<ClaimsPrincipal>(), "acme", "orders", "GetOrderDetails", "query", Arg.Any<CancellationToken>(), null);
    }

    [Fact]
    public async Task Validate_WithAggregateId_ForwardsAggregateIdToValidators() {
        // Arrange
        (QueryValidationController controller, ITenantValidator tenantValidator, IRbacValidator rbacValidator, _) = CreateController();
        ValidateQueryRequest request = CreateRequest(aggregateId: "order-123");

        // Act
        IActionResult result = await controller.Validate(request, CancellationToken.None);

        // Assert
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        PreflightValidationResult validationResult = okResult.Value.ShouldBeOfType<PreflightValidationResult>();
        validationResult.IsAuthorized.ShouldBeTrue();

        await tenantValidator.Received(1).ValidateAsync(
            Arg.Any<ClaimsPrincipal>(), "acme", Arg.Any<CancellationToken>(), "order-123");
        await rbacValidator.Received(1).ValidateAsync(
            Arg.Any<ClaimsPrincipal>(), "acme", "orders", "GetOrderDetails", "query", Arg.Any<CancellationToken>(), "order-123");
    }

    [Fact]
    public async Task Validate_CorrelationIdExtractedFromHttpContext() {
        // Arrange
        (QueryValidationController controller, _, _, List<LogEntry> logs) = CreateController();
        controller.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "custom-correlation-id";
        ValidateQueryRequest request = CreateRequest();

        // Act
        IActionResult result = await controller.Validate(request, CancellationToken.None);

        // Assert — verify the controller processed successfully using the correlationId from HttpContext
        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        PreflightValidationResult validationResult = okResult.Value.ShouldBeOfType<PreflightValidationResult>();
        validationResult.IsAuthorized.ShouldBeTrue();
        logs.ShouldContain(log => log.Message.Contains("CorrelationId=custom-correlation-id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Validate_TenantStoredInHttpContextForRateLimiter() {
        // Arrange
        (QueryValidationController controller, _, _, _) = CreateController();
        ValidateQueryRequest request = CreateRequest(tenant: "my-tenant");

        // Act
        _ = await controller.Validate(request, CancellationToken.None);

        // Assert
        controller.HttpContext.Items["RequestTenantId"].ShouldBe("my-tenant");
    }

    [Fact]
    public async Task Validate_AuthorizationServiceUnavailable_TenantValidator_Propagates503() {
        // Arrange
        (QueryValidationController controller, ITenantValidator tenantValidator, _, _) = CreateController();
        tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .ThrowsAsync(new AuthorizationServiceUnavailableException(
                "TenantValidatorActor", "acme", "Actor unreachable", 30,
                new HttpRequestException("Connection refused")));
        ValidateQueryRequest request = CreateRequest();

        // Act & Assert — exception propagates (caught by AuthorizationServiceUnavailableHandler in real pipeline)
        _ = await Should.ThrowAsync<AuthorizationServiceUnavailableException>(
            () => controller.Validate(request, CancellationToken.None));
    }

    [Fact]
    public async Task Validate_AuthorizationServiceUnavailable_RbacValidator_Propagates503() {
        // Arrange
        (QueryValidationController controller, _, IRbacValidator rbacValidator, _) = CreateController();
        rbacValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .ThrowsAsync(new AuthorizationServiceUnavailableException(
                "RbacValidatorActor", "acme", "Actor unreachable", 30,
                new HttpRequestException("Connection refused")));
        ValidateQueryRequest request = CreateRequest();

        // Act & Assert
        _ = await Should.ThrowAsync<AuthorizationServiceUnavailableException>(
            () => controller.Validate(request, CancellationToken.None));
    }

    [Fact]
    public async Task Validate_NullRequest_ThrowsArgumentNullException() {
        // Arrange
        (QueryValidationController controller, _, _, _) = CreateController();

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentNullException>(
            () => controller.Validate(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Validate_MissingSubClaim_ReturnsUnauthorizedAndLogsWarning() {
        // Arrange — JWT with no 'sub' claim must not produce a false positive authorization result
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "Bearer"));
        (QueryValidationController controller, _, _, List<LogEntry> logs) = CreateController(principal);
        ValidateQueryRequest request = CreateRequest();

        // Act
        IActionResult result = await controller.Validate(request, CancellationToken.None);

        // Assert
        _ = result.ShouldBeOfType<UnauthorizedResult>();
        logs.ShouldContain(log =>
            log.Level == LogLevel.Warning
            && log.Message.Contains("JWT 'sub' claim missing for pre-flight query validation", StringComparison.Ordinal));
        logs.ShouldContain(log =>
            log.Level == LogLevel.Warning
            && log.Message.Contains("DeniedBy=jwt", StringComparison.Ordinal)
            && log.Message.Contains("Reason=User is not authenticated.", StringComparison.Ordinal));
    }

    private sealed class TestLogger<T>(List<LogEntry> entries) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
