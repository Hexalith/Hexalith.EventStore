using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Testing.Integration.Benchmarking;

/// <summary>
/// Activates production aggregate actors through Dapr and deactivates them through the actor host.
/// </summary>
public sealed class DaprBenchmarkActorLifecycle : IBenchmarkActorLifecycle {
    private readonly string _aggregateActorTypeName;
    private readonly Uri _actorHostEndpoint;
    private readonly IActorProxyFactory _actorProxyFactory;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a Dapr-backed benchmark actor lifecycle.
    /// </summary>
    /// <param name="actorProxyFactory">The production Dapr actor proxy factory.</param>
    /// <param name="httpClient">A caller-owned HTTP client for the actor host endpoint.</param>
    /// <param name="actorHostEndpoint">The application endpoint hosting the aggregate actors, not the Dapr sidecar endpoint.</param>
    /// <param name="aggregateActorTypeName">The registered aggregate actor type name.</param>
    public DaprBenchmarkActorLifecycle(
        IActorProxyFactory actorProxyFactory,
        HttpClient httpClient,
        Uri actorHostEndpoint,
        string aggregateActorTypeName) {
        ArgumentNullException.ThrowIfNull(actorProxyFactory);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(actorHostEndpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateActorTypeName);
        if (!actorHostEndpoint.IsAbsoluteUri) {
            throw new ArgumentException("The actor host endpoint must be absolute.", nameof(actorHostEndpoint));
        }

        _actorProxyFactory = actorProxyFactory;
        _httpClient = httpClient;
        _actorHostEndpoint = new Uri(string.Concat(actorHostEndpoint.AbsoluteUri.TrimEnd('/'), "/"), UriKind.Absolute);
        _aggregateActorTypeName = aggregateActorTypeName;
    }

    /// <inheritdoc/>
    public async Task<AggregateStreamMetadata> ActivateAsync(
        AggregateIdentity identity,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        IAggregateActor actor = _actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            _aggregateActorTypeName);
        return await actor.GetStreamMetadataAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeactivateAsync(
        AggregateIdentity identity,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        string relative = $"actors/{Uri.EscapeDataString(_aggregateActorTypeName)}/{Uri.EscapeDataString(identity.ActorId)}";
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri(_actorHostEndpoint, relative));
        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.IsSuccessStatusCode) {
            return;
        }

        throw new InvalidOperationException(
            $"Dapr aggregate actor deactivation failed for '{identity.ActorId}' with HTTP {(int)response.StatusCode}.");
    }
}
