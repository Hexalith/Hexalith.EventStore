using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Terminal gateway transport that numbers each submission attempt and keeps its exact body.</summary>
/// <param name="respond">Produces the gateway reply for a one-based attempt number.</param>
internal sealed class GatewayCommandTransportStub(Func<int, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler {
    private readonly List<string> _bodies = [];
    private int _attempts;

    /// <summary>Gets the number of submission attempts that reached the gateway.</summary>
    internal int Attempts => Volatile.Read(ref _attempts);

    /// <summary>Gets the exact request body of each attempt.</summary>
    internal IReadOnlyList<string> Bodies {
        get {
            lock (_bodies) {
                return [.. _bodies];
            }
        }
    }

    /// <summary>Creates the reply a duplicate admission returns while the original is still pending.</summary>
    /// <param name="correlationId">The correlation identifier of the submitted command.</param>
    /// <returns>The accepted reply.</returns>
    internal static HttpResponseMessage Pending(string correlationId)
        => new(HttpStatusCode.Accepted) { Content = JsonContent.Create(new SubmitCommandResponse(correlationId)) };

    /// <summary>Creates the synchronous domain rejection of the original submission.</summary>
    /// <param name="correlationId">The correlation identifier of the submitted command.</param>
    /// <returns>The rejection reply.</returns>
    internal static HttpResponseMessage Rejected(string correlationId)
        => new(HttpStatusCode.Forbidden) {
            Content = JsonContent.Create(new { type = "about:blank", title = "Forbidden", status = 403, correlationId },
                mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("application/problem+json")),
        };

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        lock (_bodies) {
            _bodies.Add(body);
        }

        return await respond(Interlocked.Increment(ref _attempts), cancellationToken).ConfigureAwait(false);
    }
}
