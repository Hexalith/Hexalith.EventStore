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
