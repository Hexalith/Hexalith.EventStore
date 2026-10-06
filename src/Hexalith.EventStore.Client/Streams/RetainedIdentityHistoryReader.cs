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

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), _clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        HttpRequestMessage? message = null;
        HttpResponseMessage? response = null;
        Stream? body = null;
        Task? pendingOperation = null;
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            var request = new RetainedIdentityHistoryReadRequest(identity, purpose);
            message = new HttpRequestMessage(HttpMethod.Post, "api/v1/identity-history/read")
            {
                Content = JsonContent.Create(request),
            };
            Task<HttpResponseMessage> sending = httpClient.SendAsync(message,
                HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            pendingOperation = sending;
            response = await sending.WaitAsync(deadline.Token).ConfigureAwait(false);
            pendingOperation = null;
            deadline.Token.ThrowIfCancellationRequested();
            if (!response.IsSuccessStatusCode)
            {
                return new(null, "history-unavailable");
            }

            if (response.Content.Headers.ContentLength > RetainedIdentityHistoryLimits.MaxResponseBytes)
            {
                return new(null, "history-response-bound-exceeded");
            }

            Task<Stream> acquiring = response.Content.ReadAsStreamAsync(deadline.Token);
            pendingOperation = acquiring;
            body = await acquiring.WaitAsync(deadline.Token).ConfigureAwait(false);
            pendingOperation = null;
            deadline.Token.ThrowIfCancellationRequested();
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                Task<int> reading = body.ReadAsync(chunk.AsMemory(), deadline.Token).AsTask();
                pendingOperation = reading;
                int count = await reading.WaitAsync(deadline.Token).ConfigureAwait(false);
                pendingOperation = null;
                deadline.Token.ThrowIfCancellationRequested();
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
        finally
        {
            if (pendingOperation is not null)
            {
                // A noncooperative operation still owns its request/response/buffer. Observe its
                // eventual completion only to dispose transport material; never resume this read.
                _ = DisposeAfterCompletionAsync(pendingOperation, message, response, body);
            }
            else
            {
                body?.Dispose();
                response?.Dispose();
                message?.Dispose();
            }
        }
    }

    private static async Task DisposeAfterCompletionAsync(Task operation, HttpRequestMessage? message,
        HttpResponseMessage? response, Stream? body)
    {
        try
        {
            await operation.ConfigureAwait(false);
            if (operation is Task<HttpResponseMessage> sending)
            {
                response = sending.Result;
            }
            else if (operation is Task<Stream> acquiring)
            {
                body = acquiring.Result;
            }
        }
        catch (Exception)
        {
            // Observe late transport faults without releasing history or exposing their content.
        }
        finally
        {
            body?.Dispose();
            response?.Dispose();
            message?.Dispose();
        }
    }
}
