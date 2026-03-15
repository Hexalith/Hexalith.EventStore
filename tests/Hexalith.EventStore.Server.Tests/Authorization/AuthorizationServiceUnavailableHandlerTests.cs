
using System.Text.Json;

using Hexalith.EventStore.CommandApi.ErrorHandling;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authorization;

public class AuthorizationServiceUnavailableHandlerTests {
    private static AuthorizationServiceUnavailableHandler CreateHandler() {
        ILogger<AuthorizationServiceUnavailableHandler> logger = NullLoggerFactory.Instance.CreateLogger<AuthorizationServiceUnavailableHandler>();
        return new AuthorizationServiceUnavailableHandler(logger);
    }

    private static HttpContext CreateHttpContext(string correlationId = "test-correlation-id") {
        var context = new DefaultHttpContext();
        context.Items["CorrelationId"] = correlationId;
        context.Request.Path = "/api/commands";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ProblemDetails?> ReadProblemDetails(HttpContext context) {
        _ = context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }).ConfigureAwait(false);
    }

    [Fact]
    public async Task TryHandleAsync_MatchingException_Returns503WithRetryAfter() {
        // Arrange
        AuthorizationServiceUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var exception = new AuthorizationServiceUnavailableException(
            "TenantActor", "tenant-1", "Unreachable", 15, new HttpRequestException());

        // Act
        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(503);
        context.Response.Headers.RetryAfter.ToString().ShouldBe("15");
    }

    [Fact]
    public async Task TryHandleAsync_NonMatchingException_ReturnsFalse() {
        // Arrange
        AuthorizationServiceUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var exception = new InvalidOperationException("Not our exception");

        // Act
        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_ProblemDetailsBodyIsGeneric_NoInternalDetails() {
        // Arrange
        AuthorizationServiceUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var exception = new AuthorizationServiceUnavailableException(
            "SecretActorType", "secret-tenant-id", "Internal error details", 5, new Exception());

        // Act
        _ = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert — SECURITY: no internal details in response body
        ProblemDetails? problem = await ReadProblemDetails(context);
        _ = problem.ShouldNotBeNull();
        problem.Title.ShouldBe("Service Unavailable");
        problem.Detail.ShouldBe("Authorization service is temporarily unavailable. Please retry.");

        // Verify no actor details leaked
        string body = await ReadResponseBody(context);
        body.ShouldNotContain("SecretActorType");
        body.ShouldNotContain("secret-tenant-id");
        body.ShouldNotContain("Internal error details");
    }

    [Fact]
    public async Task TryHandleAsync_ContentTypeIsProblemJson() {
        // Arrange
        AuthorizationServiceUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var exception = new AuthorizationServiceUnavailableException(
            "Actor", "id", "reason", 5, new Exception());

        // Act
        _ = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        _ = context.Response.ContentType.ShouldNotBeNull();
        context.Response.ContentType.ShouldContain("application/problem+json");
    }

    [Fact]
    public async Task TryHandleAsync_IncludesCorrelationId() {
        // Arrange
        AuthorizationServiceUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext("my-correlation-123");
        var exception = new AuthorizationServiceUnavailableException(
            "Actor", "id", "reason", 5, new Exception());

        // Act
        _ = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        string body = await ReadResponseBody(context);
        body.ShouldContain("my-correlation-123");
    }

    [Fact]
    public async Task TryHandleAsync_RetryAfterHeaderMatchesExceptionValue() {
        // Arrange
        AuthorizationServiceUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var exception = new AuthorizationServiceUnavailableException(
            "Actor", "id", "reason", 42, new Exception());

        // Act
        _ = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Headers.RetryAfter.ToString().ShouldBe("42");
    }

    private static async Task<string> ReadResponseBody(HttpContext context) {
        _ = context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
}
