using System.Net.Http.Headers;

using Dapr.Client;

using Hexalith.EventStore.Operations.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Operations.Replay;

/// <summary>
/// Re-delivers retained CloudEvents through the local Dapr sidecar.
/// </summary>
internal sealed class DaprDeadLetterReplayTransport(
    DaprClient daprClient,
    IHttpClientFactory httpClientFactory,
    IOptions<EventStoreOperationsOptions> options) : IDeadLetterReplayTransport
{
    private readonly DaprClient _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly EventStoreOperationsOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public async Task DeliverAsync(byte[] body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);

        using HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
            HttpMethod.Post,
            _options.ReplayAppId,
            _options.ReplayMethodName);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/cloudevents+json");
        HttpClient client = _httpClientFactory.CreateClient(nameof(DaprDeadLetterReplayTransport));
        using HttpResponseMessage response = await client
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
    }
}
