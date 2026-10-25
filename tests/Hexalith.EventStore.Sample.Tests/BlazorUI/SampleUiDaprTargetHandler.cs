using System.Net;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.BlazorUI;

/// <summary>Observes the actual UI bearer and SDK invocation headers.</summary>
/// <param name="target">The expected Dapr application identity.</param>
/// <param name="channelToken">The generated token configured for this test.</param>
internal sealed class SampleUiDaprTargetHandler(string target, string channelToken) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.GetValues("dapr-app-id").ShouldBe([target]);
        request.Headers.GetValues("dapr-api-token").ShouldBe([channelToken]);
        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldNotBeNullOrWhiteSpace();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
