using System.Security.Cryptography;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Owns a validated, disposable snapshot of active and retained digest keys.</summary>
public sealed class IdempotencyDigestKeyRing : IDisposable
{
    private readonly Dictionary<string, byte[]> _keys;
    private bool _disposed;

    /// <summary>Initializes a key-ring snapshot.</summary>
    public IdempotencyDigestKeyRing(
        string activeVersion,
        IReadOnlyDictionary<string, byte[]> keys,
        IReadOnlyList<string> readerVersions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activeVersion);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(readerVersions);
        if (!keys.TryGetValue(activeVersion, out byte[]? activeKey) || activeKey.Length < 32)
        {
            throw new InvalidOperationException("The active idempotency digest key is unavailable or invalid.");
        }

        var orderedReaders = new List<string>(readerVersions.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal) { activeVersion };
        foreach (string version in readerVersions)
        {
            if (string.IsNullOrWhiteSpace(version)
                || !seen.Add(version)
                || !keys.TryGetValue(version, out byte[]? key)
                || key.Length < 32)
            {
                throw new InvalidOperationException("A retained idempotency digest key is unavailable or invalid.");
            }

            orderedReaders.Add(version);
        }

        ActiveVersion = activeVersion;
        ReaderVersions = orderedReaders.AsReadOnly();
        Versions = new[] { activeVersion }.Concat(orderedReaders).ToArray();
        _keys = new Dictionary<string, byte[]>(Versions.Count, StringComparer.Ordinal);
        foreach (string version in Versions)
        {
            _keys.Add(version, (byte[])keys[version].Clone());
        }
    }

    /// <summary>Gets the sole active-writer version.</summary>
    public string ActiveVersion { get; }

    /// <summary>Gets retained-reader versions in deterministic lookup order.</summary>
    public IReadOnlyList<string> ReaderVersions { get; }

    /// <summary>Gets the active version followed by retained readers.</summary>
    public IReadOnlyList<string> Versions { get; }

    /// <summary>Creates a new snapshot with an unreferenced reader version removed.</summary>
    public IdempotencyDigestKeyRing Retire(
        string version,
        IEnumerable<IdempotencyDigestKeyReference> references)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(references);
        if (string.Equals(version, ActiveVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The active idempotency digest key version cannot be retired.");
        }

        if (!_keys.ContainsKey(version))
        {
            throw new InvalidOperationException("The idempotency digest key version is unavailable.");
        }

        if (references.Any(reference =>
            string.Equals(reference.DigestKeyVersion, version, StringComparison.Ordinal)
            && reference.Count > 0))
        {
            throw new InvalidOperationException("The idempotency digest key version is still referenced and cannot be retired.");
        }

        var copied = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            foreach (string retainedVersion in Versions.Where(candidate =>
                !string.Equals(candidate, version, StringComparison.Ordinal)))
            {
                copied.Add(retainedVersion, RentKeyMaterial(retainedVersion));
            }

            return new IdempotencyDigestKeyRing(
                ActiveVersion,
                copied,
                ReaderVersions.Where(candidate =>
                    !string.Equals(candidate, version, StringComparison.Ordinal)).ToArray());
        }
        finally
        {
            foreach (byte[] key in copied.Values)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }

    /// <summary>Returns a caller-owned key-material copy that must be zeroed after use.</summary>
    internal byte[] RentKeyMaterial(string version)
    {
        ThrowIfDisposed();
        return _keys.TryGetValue(version, out byte[]? key)
            ? (byte[])key.Clone()
            : throw new InvalidOperationException("The idempotency digest key version is unavailable.");
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
