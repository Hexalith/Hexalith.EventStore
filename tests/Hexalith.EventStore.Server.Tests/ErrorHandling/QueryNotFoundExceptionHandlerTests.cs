using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Queries;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.ErrorHandling;

public class QueryNotFoundExceptionHandlerTests {
    [Fact]
    public async Task TryHandleAsync_QueryNotFoundException_Returns404ProblemDetails() {
        // Arrange
        var handler = new QueryNotFoundExceptionHandler(NullLogger<QueryNotFoundExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Items["CorrelationId"] = "corr-1";
        httpContext.Request.Path = "/api/v1/queries";

        var exception = new QueryNotFoundException("test-tenant", "orders", "order-1", "GetOrderStatus");

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task TryHandleAsync_NonQueryNotFoundException_ReturnsFalse() {
        // Arrange
        var handler = new QueryNotFoundExceptionHandler(NullLogger<QueryNotFoundExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException(), CancellationToken.None);

        // Assert
        handled.ShouldBeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_ResponseBody_DoesNotContainInternalDetails() {
        // Arrange
        var handler = new QueryNotFoundExceptionHandler(NullLogger<QueryNotFoundExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Items["CorrelationId"] = "corr-1";
        httpContext.Request.Path = "/api/v1/queries";

        var exception = new QueryNotFoundException("secret-tenant", "internal-domain", "order-123", "GetOrderStatus");

        // Act
        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert — read response body
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        string body = await reader.ReadToEndAsync();

        // SECURITY: internal details must NOT appear in response
        body.ShouldNotContain("secret-tenant");
        body.ShouldNotContain("internal-domain");
        body.ShouldNotContain("order-123");
        body.ShouldContain("The requested resource was not found.");
        body.ShouldNotContain("aggregate");
        body.ShouldNotContain("projection");
    }
}
