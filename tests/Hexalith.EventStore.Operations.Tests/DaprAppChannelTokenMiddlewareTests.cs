using Hexalith.EventStore.Operations.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>
/// Verifies direct app-port requests require the Dapr application-channel token.
/// </summary>
public sealed class DaprAppChannelTokenMiddlewareTests
{
    /// <summary>Verifies production startup fails closed when no app token is configured.</summary>
    [Fact]
    public void ProductionRequiresConfiguredToken()
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        _ = environment.EnvironmentName.Returns(Environments.Production);

        _ = Should.Throw<InvalidOperationException>(() =>
            DaprAppChannelSecurity.ValidateConfiguration(environment, null));
    }

    /// <summary>
    /// Verifies the platform health endpoints stay outside the token boundary.
    /// </summary>
    /// <remarks>
    /// The orchestrator health check and the Dapr sidecar app health probe both call these paths without the
    /// app token. Guarding them would keep the workload permanently unhealthy in every environment where the
    /// token is mandatory, which is also where the admin console waits on it.
    /// </remarks>
    [Theory]
    [InlineData("/alive", false)]
    [InlineData("/health", false)]
    [InlineData("/ready", false)]
    [InlineData("/internal/dead-letters", true)]
    [InlineData("/internal/dead-letters/count", true)]
    [InlineData("/dead-letters/work/events", true)]
    [InlineData("/", true)]
    public void HealthEndpointsBypassTokenBoundary(string path, bool expected)
        => DaprAppChannelSecurity.RequiresToken(new PathString(path)).ShouldBe(expected);

    /// <summary>Verifies only the exact sidecar token reaches the application pipeline.</summary>
    [Theory]
    [InlineData(null, 401, false)]
    [InlineData("wrong", 401, false)]
    [InlineData("sidecar-secret", 204, true)]
    public async Task MiddlewareRequiresExactToken(string? candidate, int status, bool expectedNext)
    {
        bool nextCalled = false;
        var middleware = new DaprAppChannelTokenMiddleware(
            context =>
            {
                nextCalled = true;
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            "sidecar-secret");
        var context = new DefaultHttpContext();
        if (candidate is not null)
        {
            context.Request.Headers[DaprAppChannelTokenMiddleware.HeaderName] = candidate;
        }

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.ShouldBe(status);
        nextCalled.ShouldBe(expectedNext);
    }
}
