using System.Net.Http.Headers;
using System.Text.Json;

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
    IOptions<EventStoreOperationsOptions> options,
    IHostEnvironment environment) : IDeadLetterReplayTransport
{
    private readonly DaprClient _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly EventStoreOperationsOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public async Task DeliverAsync(byte[] body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!environment.IsDevelopment() || !_options.IsolatedSubscriberRecovery)
        {
            throw new InvalidOperationException("The subscriber replay profile is unavailable.");
        }

        using JsonDocument structured = JsonDocument.Parse(body);
        string? messageId = structured.RootElement.GetProperty("id").GetString();
        if (!Models.DeadLetterSafeIdentity.IsValidValue(messageId))
        {
            throw new InvalidOperationException("The retained subscriber identity is unavailable.");
        }

        using HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
            HttpMethod.Post,
            _options.ReplayAppId,
            _options.ReplayMethodName);
        request.Headers.Add("X-EventStore-Subscriber-Replay", messageId);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/cloudevents+json");
        HttpClient client = _httpClientFactory.CreateClient(nameof(DaprDeadLetterReplayTransport));
        using HttpResponseMessage response = await client
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        if (!response.Headers.TryGetValues("X-EventStore-Subscriber-Outcome", out IEnumerable<string>? values)
            || !values.SequenceEqual(["handled"], StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The subscriber did not confirm handler completion.");
        }
    }
}
