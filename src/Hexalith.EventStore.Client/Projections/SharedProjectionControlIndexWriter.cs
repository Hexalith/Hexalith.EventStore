using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The single CAS writer for append-only, cross-tenant discovery indexes. A receipt and its
/// tenant membership are saved in the same state document, so a retry cannot observe one alone.
/// </summary>
internal sealed class SharedProjectionControlIndexWriter
{
    private const int MaxRetries = 64;
    internal const int MaxTenants = 64;
    internal const int MaxReceipts = 256;
    internal const int MaxStateBytes = 32 * 1024;

    private readonly IReadModelStore _store;

    internal SharedProjectionControlIndexWriter(IReadModelStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    internal static string StateKey(string indexName)
    {
        ValidateIndexName(indexName);
        return "shared-control-index:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(indexName)));
    }

    internal static string TombstoneKey(string indexName, string tenantId)
    {
        ValidateIndexName(indexName);
        ValidateCanonicalTenantId(tenantId);
        return "shared-control-index-tombstone:"
            + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(indexName)))
            + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tenantId)));
    }

    internal async Task ApplyAsync(
        SharedProjectionScope scope,
        SharedProjectionControlIndexIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentNullException.ThrowIfNull(intent);
        ValidateCanonicalTenantId(scope.TenantId);
        string key = StateKey(intent.IndexName);
        if (await ReadTombstoneAsync(scope.StoreName, intent.IndexName, scope.TenantId, cancellationToken).ConfigureAwait(false)
            is not null)
        {
            throw new InvalidOperationException("The tenant was offboarded from this control index.");
        }

        string scopeHash = scope.ComputeHash();
        string digest = ReceiptDigest(scopeHash, intent.IndexName, scope.TenantId);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionControlIndexState> entry = await _store
                .GetAsync<SharedProjectionControlIndexState>(scope.StoreName, key, cancellationToken)
                .ConfigureAwait(false);
            SharedProjectionControlIndexState current = entry.Value
                ?? new SharedProjectionControlIndexState(
                    2,
                    intent.IndexName,
                    scope.Domain,
                    scope.Family,
                    [],
                    new Dictionary<string, SharedProjectionControlIndexReceipt>(StringComparer.Ordinal));
            ValidateState(current, intent.IndexName, scope.Domain, scope.Family);
            if (current.ScopeReceipts.TryGetValue(scopeHash, out SharedProjectionControlIndexReceipt? prior)
                && (!string.Equals(prior.Digest, digest, StringComparison.Ordinal)
                    || !string.Equals(prior.TenantId, scope.TenantId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("The control-index scope receipt conflicts with its prepared intent.");
            }

            if (prior is not null && current.TenantIds.Contains(scope.TenantId, StringComparer.Ordinal))
            {
                return;
            }

            var receipts = new Dictionary<string, SharedProjectionControlIndexReceipt>(current.ScopeReceipts, StringComparer.Ordinal)
            {
                [scopeHash] = new SharedProjectionControlIndexReceipt(scope.TenantId, digest),
            };
            string[] tenants = [.. current.TenantIds.Append(scope.TenantId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
            var updated = new SharedProjectionControlIndexState(
                2,
                intent.IndexName,
                scope.Domain,
                scope.Family,
                tenants,
                receipts);
            if (updated.TenantIds.Length > MaxTenants
                || updated.ScopeReceipts.Count > MaxReceipts
                || JsonSerializer.SerializeToUtf8Bytes(updated).Length > MaxStateBytes)
            {
                throw new SharedProjectionControlIndexCapacityException(intent.IndexName);
            }

            if (await _store.TrySaveAsync(scope.StoreName, key, updated, entry.ETag ?? string.Empty, cancellationToken)
                .ConfigureAwait(false))
            {
                if (await ReadTombstoneAsync(scope.StoreName, intent.IndexName, scope.TenantId, cancellationToken)
                    .ConfigureAwait(false) is not null)
                {
                    await PruneAsync(scope.StoreName, intent.IndexName, scope.TenantId, cancellationToken)
                        .ConfigureAwait(false);
                    throw new InvalidOperationException("The tenant was offboarded from this control index.");
                }

                return;
            }
        }

        throw new InvalidOperationException("The control-index CAS retry limit was exhausted.");
    }

    internal async Task<IReadOnlyList<string>> ReadTenantsAsync(
        string storeName,
        string indexName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        string key = StateKey(indexName);
        ReadModelEntry<SharedProjectionControlIndexState> entry = await _store
            .GetAsync<SharedProjectionControlIndexState>(storeName, key, cancellationToken)
            .ConfigureAwait(false);
        if (entry.Value is null)
        {
            return [];
        }

        ValidateState(entry.Value, indexName);
        var live = new List<string>(entry.Value.TenantIds.Length);
        foreach (string tenantId in entry.Value.TenantIds)
        {
            if (await ReadTombstoneAsync(storeName, indexName, tenantId, cancellationToken).ConfigureAwait(false) is null)
            {
                live.Add(tenantId);
            }
        }

        return live;
    }

    internal async Task TombstoneAndPruneAsync(
        string storeName,
        string indexName,
        string tenantId,
        string auditId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditId);
        string key = TombstoneKey(indexName, tenantId);
        ReadModelEntry<SharedProjectionControlIndexTombstone> prior = await _store
            .GetAsync<SharedProjectionControlIndexTombstone>(storeName, key, cancellationToken)
            .ConfigureAwait(false);
        if (prior.Value is null)
        {
            var tombstone = new SharedProjectionControlIndexTombstone(indexName, tenantId, auditId);
            if (!await _store.TrySaveAsync(storeName, key, tombstone, string.Empty, cancellationToken)
                .ConfigureAwait(false))
            {
                prior = await _store.GetAsync<SharedProjectionControlIndexTombstone>(storeName, key, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                prior = new ReadModelEntry<SharedProjectionControlIndexTombstone>(tombstone, null);
            }
        }

        if (prior.Value is null
            || !string.Equals(prior.Value.IndexName, indexName, StringComparison.Ordinal)
            || !string.Equals(prior.Value.TenantId, tenantId, StringComparison.Ordinal)
            || !string.Equals(prior.Value.AuditId, auditId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The control-index offboarding audit identity conflicts with its durable tombstone.");
        }

        await PruneAsync(storeName, indexName, tenantId, cancellationToken).ConfigureAwait(false);
    }

    private async Task PruneAsync(string storeName, string indexName, string tenantId, CancellationToken cancellationToken)
    {
        string key = StateKey(indexName);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionControlIndexState> entry = await _store
                .GetAsync<SharedProjectionControlIndexState>(storeName, key, cancellationToken)
                .ConfigureAwait(false);
            if (entry.Value is null)
            {
                return;
            }

            ValidateState(entry.Value, indexName);
            string[] tenants = [.. entry.Value.TenantIds.Where(value => !string.Equals(value, tenantId, StringComparison.Ordinal))];
            var receipts = entry.Value.ScopeReceipts
                .Where(item => !string.Equals(item.Value.TenantId, tenantId, StringComparison.Ordinal))
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            if (tenants.Length == entry.Value.TenantIds.Length
                && receipts.Count == entry.Value.ScopeReceipts.Count)
            {
                return;
            }

            var updated = new SharedProjectionControlIndexState(
                2,
                indexName,
                entry.Value.OwnerDomain,
                entry.Value.OwnerFamily,
                tenants,
                receipts);
            if (await _store.TrySaveAsync(storeName, key, updated, entry.ETag ?? string.Empty, cancellationToken)
                .ConfigureAwait(false))
            {
                return;
            }
        }

        throw new InvalidOperationException("The control-index prune CAS retry limit was exhausted.");
    }

    private async Task<SharedProjectionControlIndexTombstone?> ReadTombstoneAsync(
        string storeName,
        string indexName,
        string tenantId,
        CancellationToken cancellationToken)
    {
        ReadModelEntry<SharedProjectionControlIndexTombstone> entry = await _store
            .GetAsync<SharedProjectionControlIndexTombstone>(
                storeName,
                TombstoneKey(indexName, tenantId),
                cancellationToken)
            .ConfigureAwait(false);
        if (entry.Value is not null
            && (!string.Equals(entry.Value.IndexName, indexName, StringComparison.Ordinal)
                || !string.Equals(entry.Value.TenantId, tenantId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(entry.Value.AuditId)))
        {
            throw new InvalidOperationException("The control-index offboarding tombstone is invalid.");
        }

        return entry.Value;
    }

    private static string ReceiptDigest(string scopeHash, string indexName, string tenantId)
    {
        byte[][] components =
        [
            Encoding.UTF8.GetBytes(scopeHash),
            Encoding.UTF8.GetBytes(indexName),
            Encoding.UTF8.GetBytes(tenantId),
        ];
        byte[] bytes = new byte[checked(components.Sum(static part => part.Length + sizeof(int)))];
        int offset = 0;
        foreach (byte[] component in components)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), component.Length);
            offset += sizeof(int);
            component.CopyTo(bytes.AsSpan(offset));
            offset += component.Length;
        }

        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static void ValidateIndexName(string indexName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        if (Encoding.UTF8.GetByteCount(indexName) > ReadModelBatchScope.MaxComponentByteLength)
        {
            throw new ArgumentException("The control-index name exceeds the store component limit.", nameof(indexName));
        }
    }

    private static void ValidateState(
        SharedProjectionControlIndexState state,
        string indexName,
        string? domain = null,
        string? family = null)
    {
        if (state.Version != 2
            || !string.Equals(state.IndexName, indexName, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(state.OwnerDomain)
            || string.IsNullOrWhiteSpace(state.OwnerFamily)
            || (domain is not null && !string.Equals(state.OwnerDomain, domain, StringComparison.Ordinal))
            || (family is not null && !string.Equals(state.OwnerFamily, family, StringComparison.Ordinal))
            || state.TenantIds is null
            || state.ScopeReceipts is null
            || state.TenantIds.Any(tenantId => !IsCanonicalTenantId(tenantId))
            || state.TenantIds.Distinct(StringComparer.Ordinal).Count() != state.TenantIds.Length
            || state.ScopeReceipts.Any(item => string.IsNullOrWhiteSpace(item.Key)
                || item.Value is null
                || !IsCanonicalTenantId(item.Value.TenantId)
                || string.IsNullOrWhiteSpace(item.Value.Digest)))
        {
            throw new InvalidOperationException("The control-index state is invalid.");
        }
    }

    private static void ValidateCanonicalTenantId(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (!IsCanonicalTenantId(tenantId))
        {
            throw new ArgumentException(
                "A control-index tenant id must be a canonical lowercase ASCII slug of 1..64 characters.",
                nameof(tenantId));
        }
    }

    private static bool IsCanonicalTenantId(string? tenantId)
    {
        if (tenantId is null || tenantId.Length is < 1 or > 64)
        {
            return false;
        }

        static bool Alphanumeric(char value) => value is >= 'a' and <= 'z' or >= '0' and <= '9';
        if (!Alphanumeric(tenantId[0]) || !Alphanumeric(tenantId[^1]))
        {
            return false;
        }

        return tenantId.All(value => Alphanumeric(value) || value == '-');
    }
}
