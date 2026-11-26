using System.Net.Http.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Subscriptions;

namespace Hexalith.EventStore.DomainService;

/// <summary>Explicitly configured isolated Development transport to the Operations poison owner.</summary>
internal sealed class DevelopmentSubscriberPoisonSink(DaprClient daprClient, IHttpClientFactory httpClients, string operationsAppId)
    : IEventStoreSubscriberPoisonSink
{
    internal const string ReplayHeader = "X-EventStore-Subscriber-Replay";
    internal const string OutcomeHeader = "X-EventStore-Subscriber-Outcome";
    internal string OperationsAppId { get; } = operationsAppId;

    /// <inheritdoc/>
    public async Task<EventStoreSubscriberPoisonOutcome> ObserveAsync(EventStoreSubscriberDelivery delivery,
        EventStoreSubscriberFailure? failure, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = daprClient.CreateInvokeMethodRequest(HttpMethod.Post, OperationsAppId,
            "internal/dead-letters/subscriber-delivery");
        request.Content = JsonContent.Create(new { delivery, failure });
        using HttpResponseMessage response = await httpClients.CreateClient(nameof(DevelopmentSubscriberPoisonSink))
            .SendAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EventStoreSubscriberPoisonOutcome>(cancellationToken).ConfigureAwait(false);
    }
}
