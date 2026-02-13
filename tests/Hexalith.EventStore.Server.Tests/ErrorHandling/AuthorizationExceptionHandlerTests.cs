namespace Hexalith.EventStore.Server.Tests.ErrorHandling;

using System.Text.Json;

using Hexalith.EventStore.CommandApi.ErrorHandling;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

public class AuthorizationExceptionHandlerTests
{
    private readonly ILogger<AuthorizationExceptionHandler> _logger;
    private readonly AuthorizationExceptionHandler _handler;

    public AuthorizationExceptionHandlerTests()
    {
        _logger = Substitute.For<ILogger<AuthorizationExceptionHandler>>();
        _handler = new AuthorizationExceptionHandler(_logger);
    }

    private static DefaultHttpContext CreateHttpContextWithBody()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    [Fact]
    public async Task AuthorizationExceptionHandler_HandlesCommandAuthorizationException_Returns403ProblemDetails()
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new CommandAuthorizationException("test-tenant", "test-domain", "CreateOrder", "Not authorized for domain 'test-domain'.");

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(403);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(403);
        problemDetails.Title.ShouldBe("Forbidden");
        problemDetails.Type.ShouldBe("https://tools.ietf.org/html/rfc9457#section-3");
        problemDetails.Detail.ShouldBe("Not authorized for domain 'test-domain'.");
        problemDetails.Instance.ShouldBe("/api/v1/commands");
        problemDetails.Extensions.ShouldContainKey("correlationId");
        problemDetails.Extensions.ShouldContainKey("tenantId");
    }

    [Fact]
    public async Task AuthorizationExceptionHandler_IgnoresOtherExceptions_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var exception = new InvalidOperationException("some error");

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
    }

    [Fact]
    public async Task AuthorizationExceptionHandler_SetsContentType_ApplicationProblemJson()
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        var exception = new CommandAuthorizationException("test-tenant", null, null, "Access denied.");

        // Act
        await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert - WriteAsJsonAsync may append charset, so check the content type contains problem+json
        httpContext.Response.ContentType.ShouldNotBeNull();
        httpContext.Response.ContentType.ShouldContain("json");
        httpContext.Response.StatusCode.ShouldBe(403);
    }

    [Fact]
    public void CommandAuthorizationException_Properties_SetCorrectly()
    {
        // Arrange & Act
        var ex = new CommandAuthorizationException("acme-corp", "orders", "PlaceOrder", "Not authorized for domain 'orders'.");

        // Assert
        ex.TenantId.ShouldBe("acme-corp");
        ex.Domain.ShouldBe("orders");
        ex.CommandType.ShouldBe("PlaceOrder");
        ex.Reason.ShouldBe("Not authorized for domain 'orders'.");
    }

    [Fact]
    public void CommandAuthorizationException_ToString_DoesNotLeakSensitiveData()
    {
        // Arrange
        var ex = new CommandAuthorizationException("acme-corp", "orders", "PlaceOrder", "Not authorized.");

        // Act
        string message = ex.ToString();

        // Assert - should contain tenant, reason but NOT JWT token content or event payloads
        message.ShouldContain("acme-corp");
        message.ShouldNotContain("Bearer");
        message.ShouldNotContain("eyJ"); // JWT prefix
        message.ShouldNotContain("Payload");
    }
}
