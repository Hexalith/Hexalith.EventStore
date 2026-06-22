using System.Xml.Linq;

using Dapr.Client;

using Microsoft.AspNetCore.DataProtection.Repositories;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Data Protection <see cref="IXmlRepository"/> backed by a DAPR state store.
/// </summary>
/// <remarks>
/// <para>
/// The Data Protection key ring backs the opaque query pagination cursor codec
/// (<c>IQueryCursorCodec</c>). Persisting the ring to a shared DAPR state store means a cursor sealed
/// by one replica can be unprotected by any other replica, and the ring survives pod restarts and
/// rollouts. Without shared persistence the in-memory/ephemeral ring is regenerated on every start,
/// silently invalidating every outstanding cursor (intermittent HTTP 400s under multi-replica or
/// rolling-deploy conditions).
/// </para>
/// <para>
/// The backing infrastructure (Redis, a cloud key/value store, …) is selected entirely by the DAPR
/// state-store component YAML — this type depends only on <see cref="DaprClient"/>, never on a concrete
/// infrastructure SDK, keeping infrastructure choice in DAPR configuration (see <c>deploy/dapr</c>).
/// </para>
/// <para>
/// All key-ring elements are stored as a single JSON array under one state key. The element set is tiny
/// (one entry per key, keys roll roughly every 90 days), so a single-document layout is simpler than
/// per-element keys and lets writes use an ETag compare-and-swap loop to stay consistent across replicas.
/// The Data Protection runtime reads the ring rarely (start-up plus periodic refresh) and caches it, so
/// bridging the asynchronous <see cref="DaprClient"/> calls to the synchronous <see cref="IXmlRepository"/>
/// contract has no hot-path cost.
/// </para>
/// </remarks>
internal sealed class DaprXmlRepository : IXmlRepository {
    private const int MaxStoreAttempts = 5;

    private readonly DaprClient _daprClient;
    private readonly string _stateStoreName;
    private readonly string _stateKey;

    /// <summary>Initializes a new instance of the <see cref="DaprXmlRepository"/> class.</summary>
    /// <param name="daprClient">The DAPR client used to read and write the key ring.</param>
    /// <param name="stateStoreName">The DAPR state-store component name that persists the key ring.</param>
    /// <param name="stateKey">The state key under which the key-ring elements are stored.</param>
    public DaprXmlRepository(DaprClient daprClient, string stateStoreName, string stateKey) {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateStoreName);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateKey);
        _daprClient = daprClient;
        _stateStoreName = stateStoreName;
        _stateKey = stateKey;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<XElement> GetAllElements() {
        // The IXmlRepository contract is synchronous and called off the request hot path (Data Protection
        // reads the ring at start-up and on its periodic refresh, then caches it). Bridging to the async
        // DaprClient here is therefore safe and avoids leaking async into the Data Protection surface.
        List<string> elements = GetStoredElementsAsync(CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult()
            .Elements;

        return elements
            .Select(static xml => XElement.Parse(xml))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public void StoreElement(XElement element, string friendlyName) {
        ArgumentNullException.ThrowIfNull(element);

        StoreElementAsync(element, CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    private async Task StoreElementAsync(XElement element, CancellationToken cancellationToken) {
        string serialized = element.ToString(SaveOptions.DisableFormatting);

        for (int attempt = 0; attempt < MaxStoreAttempts; attempt++) {
            (List<string> elements, string etag) = await GetStoredElementsAsync(cancellationToken)
                .ConfigureAwait(false);

            elements.Add(serialized);

            // First-write-wins compare-and-swap: a concurrent writer on another replica that committed
            // since our read changes the ETag, so the save fails and we re-read and retry. This keeps the
            // ring consistent without a distributed lock.
            bool saved = await _daprClient
                .TrySaveStateAsync(
                    _stateStoreName,
                    _stateKey,
                    elements,
                    etag,
                    new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (saved) {
                return;
            }
        }

        throw new InvalidOperationException(
            $"Failed to persist a Data Protection key to DAPR state store '{_stateStoreName}' after {MaxStoreAttempts} attempts due to write contention.");
    }

    private async Task<(List<string> Elements, string ETag)> GetStoredElementsAsync(CancellationToken cancellationToken) {
        (List<string>? elements, string etag) = await _daprClient
            .GetStateAndETagAsync<List<string>>(_stateStoreName, _stateKey, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return (elements ?? [], etag);
    }
}
