using System.Net;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Carries a real persisted SDK proof through the unchanged production gateway's HTTP response guard.</summary>
internal sealed class FixedOwnerProofLiveHttpHandler(ProjectionOwnerProofResult result) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(result, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
        });
}
