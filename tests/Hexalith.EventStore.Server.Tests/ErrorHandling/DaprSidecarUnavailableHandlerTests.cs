
using System.Text.Json;

using Grpc.Core;

using Hexalith.EventStore.CommandApi.ErrorHandling;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.ErrorHandling;

public class DaprSidecarUnavailableHandlerTests {
    private static DaprSidecarUnavailableHandler CreateHandler() {
        return new DaprSidecarUnavailableHandler(
            NullLogger<DaprSidecarUnavailableHandler>.Instance);
    }

    private static HttpContext CreateHttpContext(string correlationId = "test-correlation-id") {
        var context = new DefaultHttpContext();
        context.Items["CorrelationId"] = correlationId;
        context.Request.Path = "/api/v1/commands";
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
    public async Task TryHandleAsync_RpcExceptionUnavailable_Returns503() {
        // Arrange
        DaprSidecarUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var exception = new RpcException(new Status(StatusCode.Unavailable, "Connection refused"));

        // Act
        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(503);
        context.Response.Headers.RetryAfter.ToString().ShouldBe("30");

        ProblemDetails? problem = await ReadProblemDetails(context);
        _ = problem.ShouldNotBeNull();
        problem.Type.ShouldBe(ProblemTypeUris.ServiceUnavailable);
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("command processing pipeline");
    }

    [Fact]
    public async Task TryHandleAsync_WrappedRpcException_Returns503() {
        // Arrange — simulate DAPR wrapping the gRPC exception
        DaprSidecarUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, ""));
        var wrappedException = new InvalidOperationException("DaprException", rpcEx);

        // Act
        bool handled = await handler.TryHandleAsync(context, wrappedException, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task TryHandleAsync_RpcExceptionNotUnavailable_ReturnsFalse() {
        // Arrange — gRPC error but NOT Unavailable status code
        DaprSidecarUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var exception = new RpcException(new Status(StatusCode.Internal, "Internal error"));

        // Act
        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_NonMatchingException_ReturnsFalse() {
        // Arrange
        DaprSidecarUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var exception = new InvalidOperationException("Not a DAPR error");

        // Act
        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_DoesNotIncludeCorrelationId() {
        // Arrange (UX-DR2: No correlationId on 503)
        DaprSidecarUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext("my-correlation");
        var exception = new RpcException(new Status(StatusCode.Unavailable, ""));

        // Act
        _ = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        ProblemDetails? problem = await ReadProblemDetails(context);
        _ = problem.ShouldNotBeNull();
        problem.Extensions.ShouldNotContainKey("correlationId");
    }

    [Fact]
    public async Task TryHandleAsync_NoForbiddenTerminology() {
        // Arrange (UX-DR6, UX-DR11)
        DaprSidecarUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var exception = new RpcException(new Status(StatusCode.Unavailable, ""));

        // Act
        _ = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        _ = context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        string body = await reader.ReadToEndAsync();

        body.ShouldNotContain("DAPR", Case.Insensitive);
        body.ShouldNotContain("sidecar", Case.Insensitive);
        body.ShouldNotContain("actor", Case.Insensitive);
        body.ShouldNotContain("aggregate", Case.Insensitive);
    }

    [Fact]
    public async Task TryHandleAsync_HttpRequestExceptionWithSocketException_Returns503() {
        // Arrange — simulate connection refused
        DaprSidecarUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var socketEx = new System.Net.Sockets.SocketException(10061); // Connection refused
        var httpEx = new HttpRequestException("Connection refused", socketEx);

        // Act
        bool handled = await handler.TryHandleAsync(context, httpEx, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(503);
    }
}
