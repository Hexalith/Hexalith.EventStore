using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>In-process Dapr service-invocation forwarding into the actual SDK HTTP endpoint, preserving serialized bytes and assertions.</summary>
internal sealed class CompleteCommandSourceForwardingHandler(HttpMessageHandler receiver) : DelegatingHandler(receiver)
{
    internal long RequestBytes { get; private set; }
    internal string? Assertion { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBytes = (await request.Content!.ReadAsByteArrayAsync(cancellationToken)).LongLength;
        Assertion = request.Headers.GetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName).Single();
        request.Headers.Add(DaprAppChannelToken.HeaderName, "component-channel");
        request.RequestUri = new("http://localhost/process");
        return await base.SendAsync(request, cancellationToken);
    }
}
