using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Http;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Middleware;

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
        CorrelationIdMiddleware.IsValidIdentifier(capturedCorrelationId).ShouldBeTrue();
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
    public async Task InvokeAsync_WithMultipleValues_UsesOneGeneratedCorrelationForResponseAndDenialTelemetry()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = new[] { "first-id", "second-id" };
        string? denialCorrelation = null;
        var middleware = new CorrelationIdMiddleware(context =>
        {
            denialCorrelation = WorkloadAuthenticationTelemetry.GetCorrelationId(context);
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        string assigned = context.Items[CorrelationIdMiddleware.HttpContextKey].ShouldBeOfType<string>();
        CorrelationIdMiddleware.IsValidIdentifier(assigned).ShouldBeTrue();
        assigned.ShouldNotBe("first-id");
        assigned.ShouldNotBe("second-id");
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ShouldHaveSingleItem().ShouldBe(assigned);
        denialCorrelation.ShouldBe(assigned);
    }

    [Theory]
    [InlineData("not_a_valid_id")]
    [InlineData("legacy\n")]
    public async Task InvokeAsync_WithInvalidHeader_GeneratesNewId(string invalidId) {
        // Arrange
        string? capturedCorrelationId = null;
        var middleware = new CorrelationIdMiddleware(next: context => {
            capturedCorrelationId = context.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString();
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = invalidId;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedCorrelationId.ShouldNotBe(invalidId);
        CorrelationIdMiddleware.IsValidIdentifier(capturedCorrelationId).ShouldBeTrue();
    }
}
