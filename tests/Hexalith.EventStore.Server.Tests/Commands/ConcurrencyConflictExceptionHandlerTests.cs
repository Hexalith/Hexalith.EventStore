
using System.Text.Json;

using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class ConcurrencyConflictExceptionHandlerTests {
    private readonly InMemoryCommandStatusStore _statusStore;
    private readonly ILogger<ConcurrencyConflictExceptionHandler> _logger;
    private readonly ConcurrencyConflictExceptionHandler _handler;

    public ConcurrencyConflictExceptionHandlerTests() {
        _statusStore = new InMemoryCommandStatusStore();
        _logger = Substitute.For<ILogger<ConcurrencyConflictExceptionHandler>>();
        _handler = new ConcurrencyConflictExceptionHandler(_statusStore, _logger);
    }

    private static DefaultHttpContext CreateHttpContextWithBody() {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    [Fact]
    public async Task TryHandleAsync_ConcurrencyConflictException_Returns409ProblemDetails() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-123",
            tenantId: "acme");

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(409);

        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(409);
        problemDetails.Title.ShouldBe("Conflict");
        problemDetails.Type.ShouldBe(ProblemTypeUris.ConcurrencyConflict);
        problemDetails.Instance.ShouldBe("/api/v1/commands");
    }

    [Fact]
    public async Task TryHandleAsync_ConcurrencyConflictException_IncludesCorrelationIdExtension() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "http-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-123",
            tenantId: "acme");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldContainKey("correlationId");
        problemDetails.Extensions["correlationId"]!.ToString().ShouldBe("http-correlation-id");
    }

    [Fact]
    public async Task TryHandleAsync_ConcurrencyConflictException_DoesNotIncludeAggregateIdExtension() {
        // Arrange (UX-DR10: No aggregateId in client response)
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-456",
            tenantId: "acme");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldNotContainKey("aggregateId");
    }

    [Fact]
    public async Task TryHandleAsync_ConcurrencyConflictException_DoesNotIncludeTenantIdExtension() {
        // Arrange (UX-DR10: No tenantId in 409 client response)
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-123",
            tenantId: "acme");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldNotContainKey("tenantId");
    }

    [Fact]
    public async Task TryHandleAsync_ConcurrencyConflictException_DoesNotIncludeConflictSourceExtension() {
        // Arrange (UX-DR10: No conflictSource in client response)
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-123",
            tenantId: "acme",
            conflictSource: "StateStore");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldNotContainKey("conflictSource");
    }

    [Fact]
    public async Task TryHandleAsync_ConcurrencyConflictException_DetailDoesNotContainAggregate() {
        // Arrange (UX-DR6: No event sourcing terminology in error responses)
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-123",
            tenantId: "acme");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);
        _ = problemDetails.ShouldNotBeNull();
        _ = problemDetails.Detail.ShouldNotBeNull();
        problemDetails.Detail.ShouldNotContain("aggregate", Case.Insensitive);
        problemDetails.Detail.ShouldNotContain("actor", Case.Insensitive);
        problemDetails.Detail.ShouldNotContain("DAPR", Case.Insensitive);
        problemDetails.Detail.ShouldNotContain("sidecar", Case.Insensitive);
    }

    [Fact]
    public async Task TryHandleAsync_ConcurrencyConflictException_WritesRejectedStatus() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-123",
            tenantId: "acme");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        CommandStatusRecord? status = await _statusStore.ReadStatusAsync("acme", "cmd-corr-id");
        _ = status.ShouldNotBeNull();
        status.Status.ShouldBe(CommandStatus.Rejected);
        status.FailureReason.ShouldBe("ConcurrencyConflict");
        status.AggregateId.ShouldBe("order-123");
    }

    [Fact]
    public async Task TryHandleAsync_StatusWriteFails_StillReturns409() {
        // Arrange - use a mock status store that throws
        ICommandStatusStore failingStore = Substitute.For<ICommandStatusStore>();
        _ = failingStore.WriteStatusAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommandStatusRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Store unavailable"));

        var handler = new ConcurrencyConflictExceptionHandler(failingStore, _logger);

        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-123",
            tenantId: "acme");

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert - still returns 409 despite status write failure (rule #12)
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task TryHandleAsync_StatusWriteFails_LogsWarning() {
        // Arrange - use a mock status store that throws
        ICommandStatusStore failingStore = Substitute.For<ICommandStatusStore>();
        _ = failingStore.WriteStatusAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommandStatusRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Store unavailable"));

        ILogger<ConcurrencyConflictExceptionHandler> logger = Substitute.For<ILogger<ConcurrencyConflictExceptionHandler>>();
        var handler = new ConcurrencyConflictExceptionHandler(failingStore, logger);

        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-123",
            tenantId: "acme");

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert - warning logged for status write failure
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to write Rejected status")),
            Arg.Any<InvalidOperationException>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task TryHandleAsync_OtherException_ReturnsFalse() {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var exception = new InvalidOperationException("some other error");

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_ConcurrencyConflictException_LogsWarning() {
        // Arrange
        ILogger<ConcurrencyConflictExceptionHandler> logger = Substitute.For<ILogger<ConcurrencyConflictExceptionHandler>>();
        var handler = new ConcurrencyConflictExceptionHandler(_statusStore, logger);

        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-123",
            tenantId: "acme");

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert - warning logged with structured properties
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Concurrency conflict")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task TryHandleAsync_WrappedConcurrencyConflictException_Returns409() {
        // Arrange - simulate DAPR ActorMethodInvocationException wrapping
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";

        var innerConflict = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-123",
            tenantId: "acme");
        var wrappedException = new InvalidOperationException("ActorMethodInvocationException", innerConflict);

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, wrappedException, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task TryHandleAsync_DeeplyNestedConcurrencyConflictException_Returns409() {
        // Arrange - simulate AggregateException -> ActorMethodInvocationException -> ConcurrencyConflictException
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";

        var innerConflict = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-789",
            tenantId: "acme");
        var actorException = new InvalidOperationException("ActorMethodInvocationException", innerConflict);
        var outerException = new AggregateException("Wrapper", actorException);

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, outerException, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task TryHandleAsync_409Response_IncludesRetryAfterHeader() {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-123",
            tenantId: "acme");

        // Act
        _ = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        httpContext.Response.Headers["Retry-After"].ToString().ShouldBe("1");
    }

    [Fact]
    public async Task TryHandleAsync_NullTenantId_SkipsStatusWrite() {
        // Arrange - exception with null tenantId
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";
        var exception = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-123",
            tenantId: null); // Null tenant ID

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert - 409 returned but status NOT written
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(409);

        // Verify status was NOT written (null tenantId means no state store key)
        CommandStatusRecord? status = await _statusStore.ReadStatusAsync("any-tenant", "cmd-corr-id");
        status.ShouldBeNull();
    }

    [Fact]
    public async Task TryHandleAsync_Depth10Nesting_Returns409() {
        // Arrange - nest ConcurrencyConflictException at depth 10 (max depth limit)
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";

        Exception innermost = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-depth10",
            tenantId: "acme");

        // Wrap it 9 times (total depth = 10)
        Exception current = innermost;
        for (int i = 0; i < 9; i++) {
            current = new InvalidOperationException($"Wrapper level {i + 1}", current);
        }

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, current, CancellationToken.None);

        // Assert - should still find the conflict at depth 10
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task TryHandleAsync_AggregateExceptionWithMultipleInners_FindsConflict() {
        // Arrange - ConcurrencyConflictException is NOT the first inner exception
        DefaultHttpContext httpContext = CreateHttpContextWithBody();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        httpContext.Request.Path = "/api/v1/commands";

        var unrelatedError = new InvalidOperationException("Unrelated error");
        var conflict = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-aggregate-multi",
            tenantId: "acme");
        var aggregate = new AggregateException("Multiple failures", unrelatedError, conflict);

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, aggregate, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task TryHandleAsync_Depth11Nesting_ReturnsFalse() {
        // Arrange - nest ConcurrencyConflictException at depth 11 (exceeds max depth limit)
        var httpContext = new DefaultHttpContext();

        Exception innermost = new ConcurrencyConflictException(
            correlationId: "cmd-corr-id",
            aggregateId: "order-depth11",
            tenantId: "acme");

        // Wrap it 10 times (total depth = 11, exceeds maxDepth of 10)
        Exception current = innermost;
        for (int i = 0; i < 10; i++) {
            current = new InvalidOperationException($"Wrapper level {i + 1}", current);
        }

        // Act
        bool handled = await _handler.TryHandleAsync(httpContext, current, CancellationToken.None);

        // Assert - should NOT find the conflict (depth limit exceeded)
        handled.ShouldBeFalse();
    }
}
