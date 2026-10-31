namespace Hexalith.EventStore.Testing.Integration;

using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

using StackExchange.Redis;

/// <summary>Observes exact synthetic EventStore streams through a descriptor-owned Redis endpoint.</summary>
public static class TestEnvironmentStreamReader
{
    /// <summary>Reads the exact persisted synthetic stream from the supplied run-owned state store.</summary>
    /// <param name="descriptor">The verified owner descriptor.</param>
    /// <param name="resourceId">The explicitly declared persistence resource.</param>
    /// <param name="moduleId">The declaring module.</param>
    /// <param name="identity">The synthetic aggregate identity written through EventStore.</param>
    /// <param name="cancellationToken">The bounded test token.</param>
    /// <returns>Persisted metadata and complete event envelopes.</returns>
    public static async Task<(AggregateMetadata Metadata, EventEnvelope[] Events)> ReadStoredStreamAsync(TestEnvironmentDescriptor descriptor, string resourceId, string moduleId, AggregateIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        if (identity.TenantId != descriptor.SyntheticTenantId)
        {
            throw new InvalidOperationException("Persistence observations require this run's synthetic tenant.");
        }
        Uri endpoint = descriptor.GetEndpoint(resourceId, moduleId, "tcp");
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, descriptor.RequestCancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        bound.Token.ThrowIfCancellationRequested();
        Task<ConnectionMultiplexer> connecting = ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
        {
            EndPoints = { { endpoint.Host, endpoint.Port } },
            ConnectTimeout = 5000, SyncTimeout = 5000, AsyncTimeout = 5000, AbortOnConnectFail = true, AllowAdmin = false,
        });
        ConnectionMultiplexer connection;
        try
        {
            connection = await connecting.WaitAsync(bound.Token).ConfigureAwait(false);
        }
        catch
        {
            _ = DisposeLateConnectionAsync(connecting);
            throw;
        }
        using IConnectionMultiplexer redis = connection;
        IDatabase database = redis.GetDatabase();
        // The descriptor does not invent an app-id binding. Observe the one exact actor suffix in this owned store.
        string suffix = $"||AggregateActor||{identity.ActorId}||{identity.MetadataKey}";
        var matches = new List<string>();
        await foreach (RedisKey key in redis.GetServer(endpoint.Host, endpoint.Port).KeysAsync(pattern: "*" + suffix, pageSize: 100).WithCancellation(bound.Token).ConfigureAwait(false))
        {
            string candidate = key.ToString();
            if (candidate.EndsWith(suffix, StringComparison.Ordinal)) { matches.Add(candidate); }
            if (matches.Count > 1) { throw new InvalidOperationException("The exact synthetic stream has ambiguous persisted ownership."); }
        }
        string metadataKey = matches.SingleOrDefault() ?? throw new InvalidOperationException("The exact aggregate metadata has no persisted bytes.");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        RedisValue metadataValue = await database.HashGetAsync(metadataKey, "data").WaitAsync(bound.Token).ConfigureAwait(false);
        AggregateMetadata metadata = JsonSerializer.Deserialize<AggregateMetadata>(metadataValue.ToString(), options)
            ?? throw new InvalidOperationException("Persisted aggregate metadata is unreadable.");
        if (metadata.CurrentSequence < 1 || metadata.CurrentSequence > 100 || metadata.RetainedFloor != 1)
        {
            throw new InvalidOperationException("A fresh synthetic create requires a complete bounded stream.");
        }
        string prefix = metadataKey[..^identity.MetadataKey.Length];
        var events = new List<EventEnvelope>();
        for (long sequence = 1; sequence <= metadata.CurrentSequence; sequence++)
        {
            RedisValue value = await database.HashGetAsync(prefix + identity.EventStreamKeyPrefix + sequence, "data").WaitAsync(bound.Token).ConfigureAwait(false);
            if (value.IsNull) { throw new InvalidOperationException("An exact aggregate event key has no persisted bytes."); }
            events.Add(JsonSerializer.Deserialize<EventEnvelope>(value.ToString(), options)
                ?? throw new InvalidOperationException("A persisted event envelope is unreadable."));
        }
        return (metadata, events.ToArray());
    }

    private static async Task DisposeLateConnectionAsync(Task<ConnectionMultiplexer> connecting)
    {
        try
        {
            ConnectionMultiplexer connection = await connecting.ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The caller retains its original cancellation/failure; abandoned connection cleanup owns no credentials or CTS.
        }
    }
}
