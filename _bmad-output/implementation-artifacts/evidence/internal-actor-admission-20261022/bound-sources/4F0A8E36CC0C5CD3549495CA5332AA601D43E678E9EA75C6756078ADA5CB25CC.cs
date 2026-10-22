using System.Security.Cryptography;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Provides copied key-ring snapshots from already resolved in-process key material.</summary>
public sealed class StaticIdempotencyDigestKeyProvider : IIdempotencyDigestKeyProvider, IDisposable
{
    private readonly Dictionary<string, byte[]> _keys;
    private readonly IReadOnlyList<string> _readerVersions;
    private bool _disposed;

    /// <summary>Initializes a static provider, primarily for local development and deterministic tests.</summary>
    public StaticIdempotencyDigestKeyProvider(
        string activeVersion,
        IReadOnlyDictionary<string, byte[]> keys,
        IReadOnlyList<string> readerVersions)
    {
        using var validated = new IdempotencyDigestKeyRing(activeVersion, keys, readerVersions);
        ActiveVersion = activeVersion;
        _readerVersions = readerVersions.ToArray();
        _keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (string version in validated.Versions)
        {
            _keys.Add(version, validated.RentKeyMaterial(version));
        }
    }

    private string ActiveVersion { get; }

    /// <inheritdoc/>
    public ValueTask<IdempotencyDigestKeyRing> GetKeyRingAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ValueTask.FromResult(new IdempotencyDigestKeyRing(ActiveVersion, _keys, _readerVersions));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (byte[] key in _keys.Values)
        {
            CryptographicOperations.ZeroMemory(key);
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
