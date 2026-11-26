using Microsoft.Extensions.Hosting;

using System.Net;

using Dapr.Client;

using Hexalith.EventStore.Operations.Configuration;
using Hexalith.EventStore.Operations.Replay;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>
/// Verifies replay uses an exact structured-CloudEvent service invocation.
/// </summary>
public sealed class DaprDeadLetterReplayTransportTests
{
    private static readonly byte[] s_body = """{"id":"message-a","data":{"sequenceNumber":1}}"""u8.ToArray();
    /// <summary>Verifies target coordinates, content type, and original bytes are preserved.</summary>
    [Fact]
    public async Task DeliverUsesConfiguredTargetAndExactStructuredCloudEvent()
    {
        byte[] body = s_body;
        (DaprDeadLetterReplayTransport transport, DaprClient daprClient, CapturingHandler handler) = CreateTransport(HttpStatusCode.OK);

        await transport.DeliverAsync(body);

        _ = daprClient.Received(1).CreateInvokeMethodRequest(HttpMethod.Post, "works", "work/events");
        handler.Body.ShouldBe(body);
        handler.ContentType.ShouldBe("application/cloudevents+json");
        handler.ReplayHeader.ShouldBe(["message-a"]);
    }

    /// <summary>Verifies a success status alone is not replay completion evidence.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("skipped")]
    [InlineData("Handled")]
    public async Task DeliverRejectsSuccessWithoutHandledOutcome(string? outcome)
    {
        (DaprDeadLetterReplayTransport transport, _, _) = CreateTransport(HttpStatusCode.OK, outcome);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => transport.DeliverAsync(s_body));
    }

    /// <summary>Verifies a non-success target acknowledgement propagates to the actor state machine.</summary>
    [Fact]
    public async Task DeliverPropagatesNonSuccessResponse()
    {
        (DaprDeadLetterReplayTransport transport, _, _) = CreateTransport(HttpStatusCode.ServiceUnavailable);

        _ = await Should.ThrowAsync<HttpRequestException>(() => transport.DeliverAsync(s_body));
    }

    private static (DaprDeadLetterReplayTransport Transport, DaprClient DaprClient, CapturingHandler Handler)
        CreateTransport(HttpStatusCode statusCode, string? outcome = "handled")
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.CreateInvokeMethodRequest(HttpMethod.Post, "works", "work/events")
            .Returns(new HttpRequestMessage(HttpMethod.Post, "http://localhost/invoke/works/work/events"));
        var handler = new CapturingHandler(statusCode, outcome);
        var client = new HttpClient(handler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient(Arg.Any<string>()).Returns(client);
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        var transport = new DaprDeadLetterReplayTransport(
            daprClient,
            factory,
            Options.Create(new EventStoreOperationsOptions { IsolatedSubscriberRecovery = true }),
            environment);
        return (transport, daprClient, handler);
    }

    private sealed class CapturingHandler(HttpStatusCode statusCode, string? outcome) : HttpMessageHandler
    {
        internal byte[]? Body { get; private set; }

        internal string? ContentType { get; private set; }

        internal string[] ReplayHeader { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            ReplayHeader = request.Headers.TryGetValues("X-EventStore-Subscriber-Replay", out IEnumerable<string>? values) ? [.. values] : [];
            var response = new HttpResponseMessage(statusCode);
            if (outcome is not null)
            {
                response.Headers.Add("X-EventStore-Subscriber-Outcome", outcome);
            }

            return response;
        }
    }
}
