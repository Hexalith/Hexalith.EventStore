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
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            long startedAt = _clock.GetTimestamp();
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
            if (purpose != RetainedIdentityHistoryReadRequest.AttributionPurpose)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new(null, "history-purpose-denied");
            }

            using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), _clock, cancellationToken, startedAt);
            try
            {
                RetainedIdentityHistoryReadResult result;
                try
                {
                    result = await ReadCoreAsync(new(identity, purpose), deadline).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or ArgumentException or IOException)
                {
                    result = new(null, "history-unavailable");
                }

                deadline.ThrowIfCancellationRequested();
                return result;
            }
            catch (OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new(null, deadline.IsExpired ? "history-time-bound-exceeded" : "history-unavailable");
            }
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private async Task<RetainedIdentityHistoryReadResult> ReadCoreAsync(RetainedIdentityHistoryReadRequest request,
        AuthoritativeStreamReadDeadline deadline)
    {
        HttpRequestMessage? message = null;
        HttpResponseMessage? response = null;
        Stream? body = null;
        Task? pendingOperation = null;
        try
        {
            deadline.ThrowIfCancellationRequested();
            message = new HttpRequestMessage(HttpMethod.Post, "api/v1/identity-history/read")
            {
                Content = JsonContent.Create(request),
            };
            response = await deadline.ReadAsync(token => httpClient.SendAsync(message,
                HttpCompletionOption.ResponseHeadersRead, token), operation => pendingOperation = operation).ConfigureAwait(false);
            pendingOperation = null;
            deadline.ThrowIfCancellationRequested();
            if (!response.IsSuccessStatusCode)
            {
                return new(null, "history-unavailable");
            }

            if (response.Content.Headers.ContentLength > RetainedIdentityHistoryLimits.MaxResponseBytes)
            {
                return new(null, "history-response-bound-exceeded");
            }

            body = await deadline.ReadAsync(token => response.Content.ReadAsStreamAsync(token),
                operation => pendingOperation = operation).ConfigureAwait(false);
            pendingOperation = null;
            deadline.ThrowIfCancellationRequested();
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            while (true)
            {
                deadline.ThrowIfCancellationRequested();
                int count = await deadline.ReadAsync(token => body.ReadAsync(chunk.AsMemory(), token).AsTask(),
                    operation => pendingOperation = operation).ConfigureAwait(false);
                pendingOperation = null;
                deadline.ThrowIfCancellationRequested();
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
            deadline.ThrowIfCancellationRequested();
            return complete ? result! : new(null, "history-incomplete-or-unavailable");
        }
        finally
        {
            // Provider Dispose can block even when the tracked operation already completed.
            // Keep both direct and late owned cleanup off the query continuation and observe faults.
            Task cleanup = Task.Run(() => DisposeAfterCompletionAsync(pendingOperation, message, response, body));
            _ = cleanup.ContinueWith(static task => { _ = task.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private static async Task DisposeAfterCompletionAsync(Task? operation, HttpRequestMessage? message,
        HttpResponseMessage? response, Stream? body)
    {
        try
        {
            if (operation is not null)
            {
                await operation.ConfigureAwait(false);
            }
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
            try
            {
                body?.Dispose();
            }
            finally
            {
                try
                {
                    response?.Dispose();
                }
                finally
                {
                    message?.Dispose();
                }
            }
        }
    }
}
