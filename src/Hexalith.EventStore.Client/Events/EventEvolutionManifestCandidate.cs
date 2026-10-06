namespace Hexalith.EventStore.Client.Events;

/// <summary>Owns a validated, caller-pinned domain manifest before any readiness decision.</summary>
/// <remarks>
/// A caller-supplied graph check cannot establish the complete reviewed catalog, immutable execution
/// binding, loader observations or deployment authority.
/// </remarks>
internal sealed class EventEvolutionManifestCandidate : IDisposable
{
    internal EventEvolutionManifestCandidate(
        string domain,
        IReadOnlyList<ReadOnlyMemory<byte>> encodedRows,
        string pinnedFingerprint,
        long referencedManifestBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(encodedRows);
        ArgumentException.ThrowIfNullOrEmpty(pinnedFingerprint);
        if (pinnedFingerprint.Length != 64 || pinnedFingerprint.Any(static value => value is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new ArgumentException("A pinned registry fingerprint must be 64 lower-case hexadecimal characters.", nameof(pinnedFingerprint));
        }

        Registry = new EventDomainRegistry(domain, encodedRows, referencedManifestBytes);
        if (!string.Equals(Registry.Fingerprint, pinnedFingerprint, StringComparison.Ordinal))
        {
            Registry.Dispose();
            throw new InvalidOperationException("CapabilityMismatch: the local registry fingerprint differs from the gateway pin.");
        }

        PinnedFingerprint = pinnedFingerprint;
    }

    internal EventDomainRegistry Registry { get; }

    /// <summary>Gets the caller-supplied registry pin. It is not a provider attestation.</summary>
    internal string PinnedFingerprint { get; }

    /// <summary>Checks a supplied local dependency graph without advertising registry readiness.</summary>
    internal void RequireSuppliedLocalClosure(
        IReadOnlyList<EventResolvedDependency> resolvedGraph,
        IReadOnlyList<EventDependencyIdentity> executedRoots,
        CancellationToken cancellationToken)
        => EventDependencyClosureVerifier.RequireResolvedGraph(Registry, resolvedGraph, executedRoots, cancellationToken);

    /// <inheritdoc/>
    public void Dispose() => Registry.Dispose();
}
