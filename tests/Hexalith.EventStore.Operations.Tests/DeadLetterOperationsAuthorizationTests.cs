using System.Diagnostics.Metrics;
using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Operations.Actors;
using Hexalith.EventStore.Operations.Configuration;
using Hexalith.EventStore.Operations.Endpoints;
using Hexalith.EventStore.Operations.Models;
using Hexalith.EventStore.Operations.Telemetry;

using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>
/// Verifies the application-level defense behind the Dapr caller policy.
/// </summary>
public sealed class DeadLetterOperationsAuthorizationTests
{
    /// <summary>Verifies a failed durable actor capture is surfaced as a retryable HTTP response.</summary>
    [Fact]
    public async Task CapturePersistenceFailureReturnsServerError()
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":\"message-1\"}"));
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        _ = actor.CaptureAsync(Arg.Any<DeadLetterCaptureRequest>())
            .Returns<Task<DeadLetterCaptureResult>>(_ => throw new InvalidOperationException("simulated persistence failure"));
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IDeadLetterDrainActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        using ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();

        IResult result = await DeadLetterOperationsEndpointExtensions.CaptureAsync(
            context.Request,
            factory,
            Options.Create(new EventStoreOperationsOptions()),
            TimeProvider.System,
            new EventStoreOperationsTelemetry(services.GetRequiredService<IMeterFactory>(), TimeProvider.System));

        result.ShouldBeAssignableTo<IStatusCodeHttpResult>().StatusCode
            .ShouldBe(StatusCodes.Status500InternalServerError);
    }

    /// <summary>Verifies both the exact Admin app id and forwarded bearer context are mandatory.</summary>
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("eventstore-admin", null, false)]
    [InlineData("other-app", "Bearer operator-token", false)]
    [InlineData("eventstore-admin", "Basic operator-token", false)]
    [InlineData("eventstore-admin", "Bearer ", false)]
    [InlineData("eventstore-admin", "Bearer     ", false)]
    [InlineData("eventstore-admin", "Bearer operator-token", true)]
    public void InternalAuthorizationRequiresExactCallerAndBearer(
        string? callerAppId,
        string? authorization,
        bool expected)
    {
        var context = new DefaultHttpContext();
        if (callerAppId is not null)
        {
            context.Request.Headers["dapr-caller-app-id"] = callerAppId;
        }

        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        DeadLetterOperationsEndpointExtensions.IsAuthorized(
            context.Request,
            new EventStoreOperationsOptions()).ShouldBe(expected);
    }

    /// <summary>Verifies action batches and actor-key inputs are bounded before actor invocation.</summary>
    [Fact]
    public void ActionValidationRejectsOversizedBatchesAndIdentityInputs()
    {
        var options = new EventStoreOperationsOptions { MaxActionItems = 2 };

        DeadLetterOperationsEndpointExtensions.IsValidAction(
            new DeadLetterActionRequest("tenant-a", ["message-a", "message-b"]),
            options).ShouldBeTrue();
        DeadLetterOperationsEndpointExtensions.IsValidAction(
            new DeadLetterActionRequest("tenant-a", ["message-a", "message-b", "message-c"]),
            options).ShouldBeFalse();
        DeadLetterOperationsEndpointExtensions.IsValidAction(
            new DeadLetterActionRequest("tenant-a", [new string('x', DeadLetterSafeIdentity.MaxValueLength + 1)]),
            options).ShouldBeFalse();
    }
}
