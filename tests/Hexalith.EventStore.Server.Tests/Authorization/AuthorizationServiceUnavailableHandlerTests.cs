
using System.Text.Json;

using Hexalith.EventStore.ErrorHandling;

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
    public async Task TryHandleAsync_MatchingException_Returns503WithRetryAfter30() {
        // Arrange (UX-DR5: fixed 30s for 503)
        AuthorizationServiceUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var exception = new AuthorizationServiceUnavailableException(
            "TenantActor", "tenant-1", "Unreachable", new HttpRequestException());

        // Act
        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(503);
        context.Response.Headers.RetryAfter.ToString().ShouldBe("30");
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
            "SecretActorType", "secret-tenant-id", "Internal error details", new Exception());

        // Act
        _ = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert — SECURITY: no internal details in response body
        ProblemDetails? problem = await ReadProblemDetails(context);
        _ = problem.ShouldNotBeNull();
        problem.Title.ShouldBe("Service Unavailable");
        problem.Type.ShouldBe(ProblemTypeUris.ServiceUnavailable);
        problem.Detail.ShouldBe("The command processing pipeline is temporarily unavailable. Please retry after the specified interval.");

        // Verify no actor details leaked
        string body = await ReadResponseBody(context);
        body.ShouldNotContain("SecretActorType");
        body.ShouldNotContain("secret-tenant-id");
        body.ShouldNotContain("Internal error details");
        body.ShouldNotContain("Authorization service");
    }

    [Fact]
    public async Task TryHandleAsync_ContentTypeIsProblemJson() {
        // Arrange
        AuthorizationServiceUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var exception = new AuthorizationServiceUnavailableException(
            "Actor", "id", "reason", new Exception());

        // Act
        _ = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        _ = context.Response.ContentType.ShouldNotBeNull();
        context.Response.ContentType.ShouldContain("application/problem+json");
    }

    [Fact]
    public async Task TryHandleAsync_DoesNotIncludeCorrelationId() {
        // Arrange (UX-DR2: No correlationId on 503 — pre-pipeline rejection)
        AuthorizationServiceUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext("my-correlation-123");
        var exception = new AuthorizationServiceUnavailableException(
            "Actor", "id", "reason", new Exception());

        // Act
        _ = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(context);
        _ = problem.ShouldNotBeNull();
        problem.Extensions.ShouldNotContainKey("correlationId");
    }

    [Fact]
    public async Task TryHandleAsync_DetailContainsCommandProcessingPipeline() {
        // Arrange (UX-DR11: "command processing pipeline" — never name internal components)
        AuthorizationServiceUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var exception = new AuthorizationServiceUnavailableException(
            "Actor", "id", "reason", new Exception());

        // Act
        _ = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(context);
        _ = problem.ShouldNotBeNull();
        _ = problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("command processing pipeline");
        problem.Detail.ShouldNotContain("Authorization service");
        problem.Detail.ShouldNotContain("actor", Case.Insensitive);
        problem.Detail.ShouldNotContain("DAPR", Case.Insensitive);
    }

    private static async Task<string> ReadResponseBody(HttpContext context) {
        _ = context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
}
