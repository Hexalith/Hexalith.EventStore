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
    /// <summary>Verifies target coordinates, content type, and original bytes are preserved.</summary>
    [Fact]
    public async Task DeliverUsesConfiguredTargetAndExactStructuredCloudEvent()
    {
        byte[] body = [1, 3, 5, 7];
        (DaprDeadLetterReplayTransport transport, DaprClient daprClient, CapturingHandler handler) = CreateTransport(HttpStatusCode.OK);

        await transport.DeliverAsync(body);

        _ = daprClient.Received(1).CreateInvokeMethodRequest(HttpMethod.Post, "works", "work/events");
        handler.Body.ShouldBe(body);
        handler.ContentType.ShouldBe("application/cloudevents+json");
    }

    /// <summary>Verifies a non-success target acknowledgement propagates to the actor state machine.</summary>
    [Fact]
    public async Task DeliverPropagatesNonSuccessResponse()
    {
        (DaprDeadLetterReplayTransport transport, _, _) = CreateTransport(HttpStatusCode.ServiceUnavailable);

        _ = await Should.ThrowAsync<HttpRequestException>(() => transport.DeliverAsync([1]));
    }

    private static (DaprDeadLetterReplayTransport Transport, DaprClient DaprClient, CapturingHandler Handler)
        CreateTransport(HttpStatusCode statusCode)
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.CreateInvokeMethodRequest(HttpMethod.Post, "works", "work/events")
            .Returns(new HttpRequestMessage(HttpMethod.Post, "http://localhost/invoke/works/work/events"));
        var handler = new CapturingHandler(statusCode);
        var client = new HttpClient(handler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient(Arg.Any<string>()).Returns(client);
        var transport = new DaprDeadLetterReplayTransport(
            daprClient,
            factory,
            Options.Create(new EventStoreOperationsOptions()));
        return (transport, daprClient, handler);
    }

    private sealed class CapturingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        internal byte[]? Body { get; private set; }

        internal string? ContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            return new HttpResponseMessage(statusCode);
        }
    }
}
