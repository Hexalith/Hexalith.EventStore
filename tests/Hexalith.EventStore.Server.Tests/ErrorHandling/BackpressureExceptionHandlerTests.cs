using System.Text.Json;

using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.ErrorHandling;

/// <summary>
/// Story 4.3 Task 6.3: BackpressureExceptionHandler unit tests (AC: #2).
/// </summary>
public class BackpressureExceptionHandlerTests {
    private readonly BackpressureExceptionHandler _handler;

    public BackpressureExceptionHandlerTests() {
        ILogger<BackpressureExceptionHandler> logger = Substitute.For<ILogger<BackpressureExceptionHandler>>();
        _handler = new BackpressureExceptionHandler(
            Options.Create(new BackpressureOptions { RetryAfterSeconds = 10 }),
            logger);
    }

    private static DefaultHttpContext CreateHttpContextWithBody() {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    [Fact]
    public async Task TryHandleAsync_BackpressureExceededException_Returns429Async() {
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new BackpressureExceededException(
            correlationId: "corr-1", tenantId: "acme", domain: "orders",
            aggregateId: "order-123", pendingCount: 150, threshold: 100);

        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(429);
    }

    [Fact]
    public async Task TryHandleAsync_BackpressureExceededException_IncludesRetryAfterHeaderAsync() {
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        var exception = new BackpressureExceededException(
            correlationId: "corr-1", tenantId: "acme", domain: "orders",
            aggregateId: "order-123", pendingCount: 150, threshold: 100);

        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        httpContext.Response.Headers["Retry-After"].ToString().ShouldBe("10");
    }

    [Fact]
    public async Task TryHandleAsync_BackpressureExceededException_IncludesProblemDetailsAsync() {
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new BackpressureExceededException(
            correlationId: "corr-1", tenantId: "acme", domain: "orders",
            aggregateId: "order-123", pendingCount: 150, threshold: 100);

        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(429);
        problemDetails.Title.ShouldBe("Too Many Requests");
        problemDetails.Type.ShouldBe(ProblemTypeUris.BackpressureExceeded);
    }

    [Fact]
    public async Task TryHandleAsync_BackpressureExceededException_IncludesCorrelationIdAndTenantIdAsync() {
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "http-corr-id";
        var exception = new BackpressureExceededException(
            correlationId: "actor-corr-id", tenantId: "acme", domain: "orders",
            aggregateId: "order-123", pendingCount: 150, threshold: 100);

        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldContainKey("correlationId");
        problemDetails.Extensions.ShouldContainKey("tenantId");
    }

    [Fact]
    public async Task TryHandleAsync_BackpressureExceededException_IncludesAggregateIdentityAsync() {
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "http-corr-id";
        var exception = new BackpressureExceededException(
            correlationId: "actor-corr-id", tenantId: "acme", domain: "orders",
            aggregateId: "order-123", pendingCount: 150, threshold: 100);

        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldContainKey("domain");
        problemDetails.Extensions["domain"]!.ToString().ShouldBe("orders");
        problemDetails.Extensions.ShouldContainKey("aggregateId");
        problemDetails.Extensions["aggregateId"]!.ToString().ShouldBe("order-123");
    }

    [Fact]
    public async Task TryHandleAsync_NonBackpressureException_ReturnsFalseAsync() {
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        var exception = new InvalidOperationException("Something else");

        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.ShouldBeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_WrappedBackpressureException_HandlesAsync() {
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        var inner = new BackpressureExceededException(
            correlationId: "corr-1", tenantId: "acme", domain: "orders",
            aggregateId: "order-123", pendingCount: 150, threshold: 100);
        var wrapper = new InvalidOperationException("Wrapped", inner);

        bool handled = await _handler.TryHandleAsync(httpContext, wrapper, CancellationToken.None);

        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(429);
    }
}
