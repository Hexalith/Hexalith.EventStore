namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Counts pdenc-v2 key-resolver calls so tests can prove local decisions never reach key resolution.
/// </summary>
/// <param name="resolve">Produces the resolver outcome; the default returns a fresh copy of the G-001 DEK.</param>
internal sealed class CountingKeyResolver(Func<CancellationToken, byte[]?>? resolve = null)
{
    private int _calls;

    /// <summary>Gets the number of resolver calls.</summary>
    internal int Calls => Volatile.Read(ref _calls);

    /// <summary>Resolves a key reference and version through the configured outcome.</summary>
    internal ValueTask<byte[]?> ResolveAsync(string keyReference, uint dekVersion, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _calls);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(resolve is null ? TestFixture.Dek() : resolve(cancellationToken));
    }
}
