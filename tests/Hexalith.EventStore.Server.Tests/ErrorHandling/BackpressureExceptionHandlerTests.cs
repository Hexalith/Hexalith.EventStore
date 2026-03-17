using System.Text.Json;

using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.ErrorHandling;

public class BackpressureExceptionHandlerTests {
    private readonly ILogger<BackpressureExceptionHandler> _logger;
    private readonly BackpressureExceptionHandler _handler;

    public BackpressureExceptionHandlerTests() {
        _logger = Substitute.For<ILogger<BackpressureExceptionHandler>>();
        _handler = new BackpressureExceptionHandler(_logger);
    }

    private static DefaultHttpContext CreateHttpContextWithBody() {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    [Fact]
    public async Task TryHandleAsync_BackpressureExceededException_Returns429WithProblemDetails() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new BackpressureExceededException(
            aggregateActorId: "test-tenant:test-domain:agg-001",
            tenantId: "test-tenant",
            correlationId: "cmd-corr-id",
            currentDepth: 100);

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(429);

        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(429);
        problemDetails.Title.ShouldBe("Too Many Requests");
        problemDetails.Type.ShouldBe(ProblemTypeUris.BackpressureExceeded);
        problemDetails.Instance.ShouldBe("/api/v1/commands");
        problemDetails.Detail.ShouldBe("Too many pending commands for this entity. Please retry after the specified interval.");
    }

    [Fact]
    public async Task TryHandleAsync_BackpressureExceededException_IncludesRetryAfterHeader() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        var exception = new BackpressureExceededException(
            aggregateActorId: "test-tenant:test-domain:agg-001",
            tenantId: "test-tenant",
            correlationId: "cmd-corr-id",
            currentDepth: 100);

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        httpContext.Response.Headers["Retry-After"].ToString().ShouldBe("1");
    }

    [Fact]
    public async Task TryHandleAsync_BackpressureExceededException_IncludesCorrelationId() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "http-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new BackpressureExceededException(
            aggregateActorId: "test-tenant:test-domain:agg-001",
            tenantId: "test-tenant",
            correlationId: "cmd-corr-id",
            currentDepth: 100);

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert — prefers HTTP context correlation ID over exception's
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();

        problemDetails.Extensions.ShouldContainKey("correlationId");
        problemDetails.Extensions["correlationId"]?.ToString().ShouldBe("http-correlation-id");
    }

    [Fact]
    public async Task TryHandleAsync_BackpressureExceededException_DoesNotExposeInternalDetails() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        var exception = new BackpressureExceededException(
            aggregateActorId: "test-tenant:test-domain:agg-001",
            tenantId: "test-tenant",
            correlationId: "cmd-corr-id",
            currentDepth: 100);

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert — response body must NOT contain internal details (UX-DR10)
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        string responseBody = await reader.ReadToEndAsync();

        responseBody.ShouldNotContain("agg-001");
        responseBody.ShouldNotContain("test-tenant:test-domain:agg-001"); // actorId
        responseBody.ShouldNotContain("test-tenant"); // tenantId
        responseBody.ShouldNotContain("100"); // currentDepth (as a string check — note: this could false-positive on status code, so verify separately)
        responseBody.ShouldNotContain("CurrentDepth");
        responseBody.ShouldNotContain("actorId");
        responseBody.ShouldNotContain("aggregateId");
    }

    [Fact]
    public async Task TryHandleAsync_NonBackpressureException_ReturnsFalse() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        var exception = new InvalidOperationException("Not a backpressure exception");

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
    }

    [Fact]
    public void BackpressureExceptionHandler_HasNoStatusStoreDependency() {
        // Verify: BackpressureExceptionHandler constructor has NO ICommandStatusStore dependency
        // (unlike ConcurrencyConflictExceptionHandler which writes advisory status).
        // This is a design constraint — the handler must not write any advisory status.
        System.Reflection.ConstructorInfo[] constructors = typeof(BackpressureExceptionHandler).GetConstructors();
        foreach (System.Reflection.ConstructorInfo ctor in constructors) {
            System.Reflection.ParameterInfo[] parameters = ctor.GetParameters();
            foreach (System.Reflection.ParameterInfo param in parameters) {
                param.ParameterType.Name.ShouldNotContain("CommandStatusStore");
            }
        }
    }

    [Fact]
    public async Task TryHandleAsync_FallbackCorrelationId_UsesExceptionCorrelationId() {
        // Arrange — HTTP context has no CorrelationId set
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        // Don't set httpContext.Items["CorrelationId"]
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new BackpressureExceededException(
            aggregateActorId: "test-tenant:test-domain:agg-001",
            tenantId: "test-tenant",
            correlationId: "fallback-corr-id",
            currentDepth: 50);

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert — should fall back to exception's CorrelationId
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Extensions["correlationId"]?.ToString().ShouldBe("fallback-corr-id");
    }
}
