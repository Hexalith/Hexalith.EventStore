using System.Text;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Checks pinned registry bytes and a supplied resolved graph against exact G rows and dependency files.</summary>
/// <remarks>This is a local verifier. It cannot capture or lock the loader, establish complete catalog roots, or advertise readiness.</remarks>
internal static class EventDependencyClosureVerifier
{
    private const long MaximumManifestBytes = 64L * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Requires every supplied local root and edge to be reachable, pinned and byte-identical.</summary>
    internal static void RequirePinnedResolvedGraph(
        EventDomainRegistry registry,
        IReadOnlyList<ReadOnlyMemory<byte>> pinnedManifestRows,
        string pinnedFingerprint,
        IReadOnlyList<EventResolvedDependency> resolvedGraph,
        IReadOnlyList<EventDependencyIdentity> executedRoots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(pinnedManifestRows);
        ArgumentException.ThrowIfNullOrEmpty(pinnedFingerprint);
        ArgumentNullException.ThrowIfNull(resolvedGraph);
        ArgumentNullException.ThrowIfNull(executedRoots);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(registry.Fingerprint, pinnedFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("CapabilityMismatch: the local registry fingerprint differs from the gateway pin.");
        }

        EventRegistryRow[] pinned = EventRegistryFingerprintCodec.DecodeRows(registry.Domain, pinnedManifestRows);
        try
        {
            EventRegistryRow[] local = registry.Rows.ToArray();
            if (pinned.Length != local.Length)
            {
                throw new InvalidOperationException("CapabilityMismatch: the gateway pin omits or adds registry rows.");
            }

            for (int i = 0; i < local.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!pinned[i].Encoded.SequenceEqual(local[i].Encoded))
                {
                    throw new InvalidOperationException("CapabilityMismatch: a gateway-pinned registry row differs from the local row.");
                }
            }
        }
        finally { foreach (EventRegistryRow row in pinned) { row.Dispose(); } }

        if (executedRoots.Count is < 1 or > 65_536 || resolvedGraph.Count is < 1 or > 65_536)
        {
            throw new InvalidOperationException("CapabilityMismatch: an executing dependency closure requires bounded roots and graph nodes.");
        }

        var manifest = registry.Rows.Where(static row => row.Tag == 0x47)
            .ToDictionary(static row => new EventDependencyIdentity(row.GetTextKey(1), row.GetTextKey(2)));
        EventResolvedDependency[] resolvedSnapshot = resolvedGraph.ToArray();
        EventDependencyIdentity[] rootSnapshot = executedRoots.ToArray();
        var graph = new Dictionary<EventDependencyIdentity, (EventResolvedDependency Node, EventDependencyIdentity[] Edges)>();
        long accounted = 0;
        foreach (EventResolvedDependency node in resolvedSnapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(node);
            ValidateIdentity(node.Identity);
            ArgumentException.ThrowIfNullOrEmpty(node.ResolvedVersionOrAbiIdentity);
            ArgumentException.ThrowIfNullOrEmpty(node.LoaderContextId);
            ArgumentException.ThrowIfNullOrEmpty(node.ResolvedFile);
            ArgumentNullException.ThrowIfNull(node.Dependencies);
            accounted = checked(accounted + 256L + StrictUtf8.GetByteCount(node.Identity.LogicalLoadIdentity) * 4L
                + StrictUtf8.GetByteCount(node.ResolvedVersionOrAbiIdentity) * 4L
                + StrictUtf8.GetByteCount(node.LoaderContextId) * 4L + StrictUtf8.GetByteCount(node.ResolvedFile) * 4L);
            if (accounted > MaximumManifestBytes)
            {
                throw new InvalidOperationException("RegistryLimit: the resolved dependency graph exceeds its admitted workspace.");
            }

            if (node.Dependencies.Count > (MaximumManifestBytes - accounted) / 128L)
            {
                throw new InvalidOperationException("RegistryLimit: dependency edges exceed their admitted workspace.");
            }
            EventDependencyIdentity[] edgeSnapshot = node.Dependencies.ToArray();
            if (!graph.TryAdd(node.Identity, (node, edgeSnapshot)))
            {
                throw new InvalidOperationException("CapabilityMismatch: duplicate resolved logical dependency identity.");
            }
            if (!manifest.TryGetValue(node.Identity, out EventRegistryRow? row)
                || !string.Equals(row.GetTextField(1), node.ResolvedVersionOrAbiIdentity, StringComparison.Ordinal)
                || !string.Equals(row.GetTextField(3), node.LoaderContextId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("CapabilityMismatch: a resolved dependency disagrees with its sealed G row.");
            }

            var edges = new HashSet<EventDependencyIdentity>();
            foreach (EventDependencyIdentity dependency in edgeSnapshot)
            {
                ValidateIdentity(dependency);
                accounted = checked(accounted + 128L + StrictUtf8.GetByteCount(dependency.LogicalLoadIdentity) * 4L);
                if (accounted > MaximumManifestBytes)
                {
                    throw new InvalidOperationException("RegistryLimit: dependency edges exceed their admitted workspace.");
                }
                if (!edges.Add(dependency))
                {
                    throw new InvalidOperationException("CapabilityMismatch: duplicate resolved dependency edge.");
                }
            }
        }

        var pending = new Queue<EventDependencyIdentity>();
        foreach (EventDependencyIdentity root in rootSnapshot)
        {
            ValidateIdentity(root);
            accounted = checked(accounted + 128L + StrictUtf8.GetByteCount(root.LogicalLoadIdentity) * 4L);
            if (accounted > MaximumManifestBytes)
            {
                throw new InvalidOperationException("RegistryLimit: executing dependency roots exceed their admitted workspace.");
            }
            pending.Enqueue(root);
        }
        var reachable = new HashSet<EventDependencyIdentity>();
        while (pending.TryDequeue(out EventDependencyIdentity identity))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!graph.TryGetValue(identity, out (EventResolvedDependency Node, EventDependencyIdentity[] Edges) node))
            {
                throw new InvalidOperationException("CapabilityMismatch: an executing root or graph edge is unresolved.");
            }
            if (!reachable.Add(identity)) { continue; }
            foreach (EventDependencyIdentity dependency in node.Edges) { pending.Enqueue(dependency); }
        }
        if (reachable.Count != graph.Count)
        {
            throw new InvalidOperationException("CapabilityMismatch: the resolved graph includes an undeclared root or unreachable dependency.");
        }

        foreach (EventDependencyIdentity identity in reachable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EventResolvedDependency node = graph[identity].Node;
            EventDependencyFileVerifier.RequireExactFile(manifest[identity], node.ResolvedFile, cancellationToken);
        }
    }

    private static void ValidateIdentity(EventDependencyIdentity identity)
    {
        ArgumentException.ThrowIfNullOrEmpty(identity.LogicalLoadIdentity);
        if (identity.Kind is not ("managed" or "native"))
        {
            throw new ArgumentException("A dependency kind must be exactly managed or native.");
        }
    }
}
