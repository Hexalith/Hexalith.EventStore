using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService.Queries;
/// <summary>Guards existing single/bulk read-model APIs with the dispatcher-installed private session.</summary>
internal sealed class PrivateLogicalQueryStore(PrivateLogicalQueryHolder holder) : IReadModelStore, IReadModelBulkStore
{
    /// <inheritdoc/>
    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default)
        where TValue : class
    {
        return holder.Session.GetAsync<TValue>(storeName, key, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReadModelBulkEntry<TValue>>> GetManyAsync<TValue>(string storeName, IReadOnlyList<string> keys, int parallelism, CancellationToken cancellationToken = default)
        where TValue : class
    {
        holder.Session.RequirePins(cancellationToken);
        int count;
        try
        {
            count = keys.Count;
        }
        finally
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        await holder.Session.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (count is < 0 or > 256 || parallelism < 1)
        {
            throw new InvalidOperationException("ReadModelQueryLimit: invalid bulk query input.");
        }

        var admittedKeys = new string[count];
        for (int i = 0; i < count; i++)
        {
            try
            {
                admittedKeys[i] = keys[i];
            }
            finally
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            await holder.Session.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);
            holder.Session.RequireRead<TValue>(storeName, admittedKeys[i], cancellationToken);
        }

        if (admittedKeys.Distinct(StringComparer.Ordinal).Count() != count)
        {
            throw new InvalidOperationException("ReadModelQueryLimit: duplicate bulk key.");
        }

        var result = new List<ReadModelBulkEntry<TValue>>(count);
        foreach (string key in admittedKeys)
        {
            ReadModelEntry<TValue> row = await holder.Session.GetAsync<TValue>(storeName, key, cancellationToken).ConfigureAwait(false);
            result.Add(new ReadModelBulkEntry<TValue>(key, row.Value, row.ETag));
        }

        return result;
    }

    /// <inheritdoc/>
    public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default)
        where TValue : class
    {
        holder.Session.RequirePins(cancellationToken);
        throw new InvalidOperationException("ReadModelRouteContextRequired: logical query store is read-only.");
    }

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default)
        where TValue : class
    {
        holder.Session.RequirePins(cancellationToken);
        throw new InvalidOperationException("ReadModelRouteContextRequired: logical query store is read-only.");
    }
}
