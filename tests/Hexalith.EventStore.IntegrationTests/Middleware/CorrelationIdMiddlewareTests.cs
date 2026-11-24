using System.Net;

using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
    [InlineData(null)]
    [InlineData("caller-correlation-001")]
    public async Task ExceptionHandlerClearsResponseHeadersAndResponseStartRestoresCorrelation(string? suppliedId)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using WebApplication app = builder.Build();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler(error => error.Run(async context =>
        {
            context.Response.HasStarted.ShouldBeFalse();
            context.Response.Headers.ContainsKey(CorrelationIdMiddleware.HeaderName).ShouldBeFalse();
            context.Response.Headers.ContainsKey("X-Test-Cleared").ShouldBeFalse();

            string correlation = context.Items[CorrelationIdMiddleware.HttpContextKey].ShouldBeOfType<string>();
            CorrelationIdMiddleware.IsValidIdentifier(correlation).ShouldBeTrue();
            if (suppliedId is not null) correlation.ShouldBe(suppliedId);
            await context.Response.StartAsync(context.RequestAborted);
            context.Response.HasStarted.ShouldBeTrue();
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ShouldHaveSingleItem().ShouldBe(correlation);
            await context.Response.WriteAsync("handled", context.RequestAborted);
        }));
        app.MapGet("/throw", (HttpContext context) =>
        {
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ShouldHaveSingleItem().ShouldNotBeNullOrEmpty();
            context.Response.Headers["X-Test-Cleared"] = "before-exception";
            throw new InvalidOperationException("Synthetic endpoint failure.");
        });

        await app.StartAsync(cancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()) };
        using HttpRequestMessage request = new(HttpMethod.Get, "/throw");
        if (suppliedId is not null) request.Headers.Add(CorrelationIdMiddleware.HeaderName, suppliedId);
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Headers.Contains("X-Test-Cleared").ShouldBeFalse();
        string restoredId = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).ShouldHaveSingleItem();
        CorrelationIdMiddleware.IsValidIdentifier(restoredId).ShouldBeTrue();
        if (suppliedId is not null) restoredId.ShouldBe(suppliedId);
        (await response.Content.ReadAsStringAsync(cancellationToken)).ShouldBe("handled");
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
