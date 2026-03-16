
using System.Text.Json;

using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.ErrorHandling;

public class AuthorizationExceptionHandlerTests {
    private readonly ILogger<AuthorizationExceptionHandler> _logger;
    private readonly AuthorizationExceptionHandler _handler;

    public AuthorizationExceptionHandlerTests() {
        _logger = Substitute.For<ILogger<AuthorizationExceptionHandler>>();
        _handler = new AuthorizationExceptionHandler(_logger);
    }

    private static DefaultHttpContext CreateHttpContextWithBody() {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    [Fact]
    public async Task AuthorizationExceptionHandler_HandlesCommandAuthorizationException_Returns403ProblemDetails() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new CommandAuthorizationException("test-tenant", "test-domain", "CreateOrder", "Not authorized for domain 'test-domain'.");

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(403);

        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(403);
        problemDetails.Title.ShouldBe("Forbidden");
        problemDetails.Type.ShouldBe(ProblemTypeUris.Forbidden);
        problemDetails.Detail.ShouldBe("Not authorized for tenant 'test-tenant'. Not authorized for domain 'test-domain'.");
        problemDetails.Instance.ShouldBe("/api/v1/commands");
        problemDetails.Extensions.ShouldContainKey("correlationId");
        problemDetails.Extensions.ShouldContainKey("tenantId");
    }

    [Fact]
    public async Task AuthorizationExceptionHandler_IgnoresOtherExceptions_ReturnsFalse() {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var exception = new InvalidOperationException("some error");

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
    }

    [Fact]
    public async Task AuthorizationExceptionHandler_SetsContentType_ApplicationProblemJson() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        var exception = new CommandAuthorizationException("test-tenant", null, null, "Access denied.");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert - WriteAsJsonAsync may append charset, so check the content type contains problem+json
        _ = httpContext.Response.ContentType.ShouldNotBeNull();
        httpContext.Response.ContentType.ShouldContain("problem+json");
        httpContext.Response.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task AuthorizationExceptionHandler_DoesNotDuplicateTenantWhenReasonAlreadyContainsTenant() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        var exception = new CommandAuthorizationException("test-tenant", null, null, "Not authorized for tenant 'test-tenant'.");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("Not authorized for tenant 'test-tenant'.");
    }

    [Fact]
    public async Task AuthorizationExceptionHandler_DoesNotDuplicateTenantWhenReasonUsesDifferentCase() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        var exception = new CommandAuthorizationException("Test-Tenant", null, null, "Not authorized for tenant 'test-tenant'.");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("Not authorized for tenant 'test-tenant'.");
    }

    [Fact]
    public async Task AuthorizationExceptionHandler_SanitizesForbiddenTermsFromActorValidatorReason() {
        // Arrange — actor validators return "Tenant access denied by actor." which contains "actor" (UX-DR6)
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        var exception = new CommandAuthorizationException("test-tenant", null, null, "Tenant access denied by actor.");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        string detail = problemDetails.Detail.ShouldNotBeNull();
        detail.ShouldNotContain("actor", Case.Insensitive);
        detail.ShouldContain("Tenant access denied.");
    }

    [Fact]
    public async Task AuthorizationExceptionHandler_SanitizesRbacActorReason() {
        // Arrange — RBAC actor validator returns "RBAC access denied by actor."
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        var exception = new CommandAuthorizationException("test-tenant", "orders", "CreateOrder", "RBAC access denied by actor.");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        string detail = problemDetails.Detail.ShouldNotBeNull();
        detail.ShouldNotContain("actor", Case.Insensitive);
        detail.ShouldNotContain("DAPR", Case.Insensitive);
        detail.ShouldNotContain("aggregate", Case.Insensitive);
    }

    [Fact]
    public async Task AuthorizationExceptionHandler_SanitizesMultipleForbiddenTerms() {
        // Arrange — hypothetical reason with multiple forbidden terms
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation-id";
        var exception = new CommandAuthorizationException("test-tenant", null, null, "DAPR actor denied access to aggregate.");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        string detail = problemDetails.Detail.ShouldNotBeNull();
        detail.ShouldNotContain("actor", Case.Insensitive);
        detail.ShouldNotContain("DAPR", Case.Insensitive);
        detail.ShouldNotContain("aggregate", Case.Insensitive);
    }

    [Fact]
    public void CommandAuthorizationException_Properties_SetCorrectly() {
        // Arrange & Act
        var ex = new CommandAuthorizationException("acme-corp", "orders", "PlaceOrder", "Not authorized for domain 'orders'.");

        // Assert
        ex.TenantId.ShouldBe("acme-corp");
        ex.Domain.ShouldBe("orders");
        ex.CommandType.ShouldBe("PlaceOrder");
        ex.Reason.ShouldBe("Not authorized for domain 'orders'.");
    }

    [Fact]
    public void CommandAuthorizationException_ToString_DoesNotLeakSensitiveData() {
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
