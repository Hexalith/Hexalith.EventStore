using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Replay;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Runs real canonical Apply replay over the reconstruction HTTP request.</summary>
internal sealed class CanonicalLogicalReplayHandler : HttpMessageHandler
{
    /// <summary>Gets independent deserialized requests observed at the domain boundary.</summary>
    internal List<AggregateReconstructionRequest> Requests { get; } = [];

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AggregateReconstructionRequest replay = await request.Content!
            .ReadFromJsonAsync<AggregateReconstructionRequest>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Missing replay request.");
        Requests.Add(replay);
        AggregateReconstructionResult result = AggregateReplayer.Replay<LogicalReplayState>(replay);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
    }
}
