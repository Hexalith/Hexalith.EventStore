
using System.Text.Json;

using Dapr;

using Grpc.Core;

using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Middleware;

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
        context.Items[CorrelationIdMiddleware.HttpContextKey] = correlationId;
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
        var exception = new DaprException("Sidecar unavailable", new RpcException(new Status(StatusCode.Unavailable, "Connection refused")));

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
        var wrappedException = new InvalidOperationException("Wrapper", new DaprException("Dapr sidecar unavailable", rpcEx));

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
        var exception = new DaprException("Sidecar unavailable", new RpcException(new Status(StatusCode.Unavailable, "")));

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
        var exception = new DaprException("Sidecar unavailable", new RpcException(new Status(StatusCode.Unavailable, "")));

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
        var httpEx = new DaprException("Sidecar unavailable", new HttpRequestException("Connection refused", socketEx));

        // Act
        bool handled = await handler.TryHandleAsync(context, httpEx, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task TryHandleAsync_HttpRequestExceptionWithIOExceptionWrappingSocketException_Returns503() {
        // Arrange — .NET HTTP stack: HttpRequestException -> IOException -> SocketException
        DaprSidecarUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var socketEx = new System.Net.Sockets.SocketException(10061); // Connection refused
        var ioEx = new System.IO.IOException("Unable to read data from the transport connection", socketEx);
        var httpEx = new DaprException("Dapr sidecar unavailable", new HttpRequestException("Connection refused", ioEx));

        // Act
        bool handled = await handler.TryHandleAsync(context, httpEx, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task TryHandleAsync_HttpRequestExceptionWithoutDaprContext_ReturnsFalse() {
        // Arrange - generic network failures must not be rewritten as DAPR sidecar failures
        DaprSidecarUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var socketEx = new System.Net.Sockets.SocketException(10061);
        var httpEx = new HttpRequestException("Connection refused", socketEx);

        // Act
        bool handled = await handler.TryHandleAsync(context, httpEx, CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task TryHandleAsync_HttpRequestExceptionWithNonConnectionRefusedSocketException_ReturnsFalse() {
        // Arrange - only connection-refused should map to sidecar unavailable
        DaprSidecarUnavailableHandler handler = CreateHandler();
        HttpContext context = CreateHttpContext();
        var socketEx = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound);
        var httpEx = new DaprException("Sidecar unavailable", new HttpRequestException("Host not found", socketEx));

        // Act
        bool handled = await handler.TryHandleAsync(context, httpEx, CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }
}
