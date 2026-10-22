using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Drives the production legacy dispatcher and sample handler through its public JSON carrier.</summary>
internal sealed class LegacyReplayProductionHttpHandler(IServiceProvider services) : HttpMessageHandler
{
    internal List<long[]> Preparations { get; } = [];
    private Func<Task>? _beforePreparation;
    internal Func<Task>? BeforePreparation { set => _beforePreparation = value; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ProjectionRequest input = (await request.Content!.ReadFromJsonAsync<ProjectionRequest>(cancellationToken).ConfigureAwait(false))!;
        if (Interlocked.Exchange(ref _beforePreparation, null) is { } before) await before().ConfigureAwait(false);
        Preparations.Add([.. input.Events.Select(static item => item.SequenceNumber)]);
        ProjectionResponse? projected = DomainProjectionDispatcher.Project(services, input, cancellationToken);
        return projected is null ? new(HttpStatusCode.NotFound) : new(HttpStatusCode.OK)
        { Content = JsonContent.Create(projected, options: JsonSerializerOptions.Web) };
    }
}
