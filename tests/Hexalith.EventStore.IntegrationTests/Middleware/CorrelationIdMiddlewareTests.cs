extern alias commandapi;

namespace Hexalith.EventStore.IntegrationTests.Middleware;

using commandapi::Hexalith.EventStore.CommandApi.Middleware;

using Microsoft.AspNetCore.Http;

using Shouldly;

public class CorrelationIdMiddlewareTests {
    [Fact]
    public async Task InvokeAsync_NoHeader_GeneratesCorrelationId() {
        // Arrange
        string? capturedCorrelationId = null;
        var middleware = new CorrelationIdMiddleware(next: context => {
            capturedCorrelationId = context.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString();
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedCorrelationId.ShouldNotBeNullOrEmpty();
        Guid.TryParse(capturedCorrelationId, out _).ShouldBeTrue();
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().ShouldBe(capturedCorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_WithValidGuidHeader_PropagatesExistingId() {
        // Arrange
        string existingId = Guid.NewGuid().ToString();
        string? capturedCorrelationId = null;
        var middleware = new CorrelationIdMiddleware(next: context => {
            capturedCorrelationId = context.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString();
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = existingId;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedCorrelationId.ShouldBe(existingId);
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().ShouldBe(existingId);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidHeader_GeneratesNewId() {
        // Arrange
        string? capturedCorrelationId = null;
        var middleware = new CorrelationIdMiddleware(next: context => {
            capturedCorrelationId = context.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString();
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "not-a-guid";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedCorrelationId.ShouldNotBe("not-a-guid");
        Guid.TryParse(capturedCorrelationId, out _).ShouldBeTrue();
    }
}
