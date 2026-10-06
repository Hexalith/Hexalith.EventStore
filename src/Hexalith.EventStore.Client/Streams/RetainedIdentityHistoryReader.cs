using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Verifies a purpose-scoped source certificate received over an authenticated owner transport.</summary>
/// <param name="httpClient">The host-configured authenticated internal client.</param>
/// <param name="timeProvider">The consuming observation clock.</param>
public sealed class RetainedIdentityHistoryReader(HttpClient httpClient, TimeProvider? timeProvider = null) : IRetainedIdentityHistoryReader
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public async Task<RetainedIdentityHistoryReadResult> ReadAsync(AggregateIdentity identity, string purpose,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (purpose != RetainedIdentityHistoryReadRequest.AttributionPurpose)
        {
            return new(null, "history-purpose-denied");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            var request = new RetainedIdentityHistoryReadRequest(identity, purpose);
            using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/identity-history/read")
            {
                Content = JsonContent.Create(request),
            };
            using HttpResponseMessage response = await httpClient.SendAsync(message,
                HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new(null, "history-unavailable");
            }

            if (response.Content.Headers.ContentLength > RetainedIdentityHistoryLimits.MaxResponseBytes)
            {
                return new(null, "history-response-bound-exceeded");
            }

            using Stream body = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            while (true)
            {
                int count = await body.ReadAsync(chunk.AsMemory(), deadline.Token).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                if (buffer.Length + count > RetainedIdentityHistoryLimits.MaxResponseBytes)
                {
                    return new(null, "history-response-bound-exceeded");
                }

                buffer.Write(chunk, 0, count);
            }

            RetainedIdentityHistoryReadResult? result = JsonSerializer.Deserialize<RetainedIdentityHistoryReadResult>(
                buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)), _jsonOptions);
            bool complete = result is { IsAuthoritative: true }
                && RetainedIdentityHistoryValidator.IsComplete(request, result.Stream, _clock.GetUtcNow());
            deadline.Token.ThrowIfCancellationRequested();
            return complete ? result! : new(null, "history-incomplete-or-unavailable");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(null, "history-time-bound-exceeded");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or ArgumentException or IOException)
        {
            return new(null, "history-unavailable");
        }
    }
}
