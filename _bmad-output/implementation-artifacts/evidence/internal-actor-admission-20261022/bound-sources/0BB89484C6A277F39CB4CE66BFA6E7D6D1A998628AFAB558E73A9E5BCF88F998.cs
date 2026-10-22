namespace Hexalith.EventStore.DomainService;

/// <summary>Indexes immutable trusted capture declarations registered by the serving deployment.</summary>
public sealed class FoldedSnapshotQualificationRegistry
{
    private readonly IReadOnlyList<FoldedSnapshotQualification> _entries;

    /// <summary>Builds a registry and refuses duplicate domain/aggregate or profile identities.</summary>
    public FoldedSnapshotQualificationRegistry(IEnumerable<FoldedSnapshotQualification> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        FoldedSnapshotQualification[] copy = entries.ToArray();
        if (copy.GroupBy(static item => (item.Domain, item.AggregateType)).Any(static group => group.Count() != 1)
            || copy.GroupBy(static item => (item.Domain, item.ProfileId)).Any(static group => group.Count() != 1))
        {
            throw new ArgumentException("SnapshotCaptureUnsupported: duplicate qualification.", nameof(entries));
        }
        _entries = copy;
    }

    /// <summary>Finds the exact domain/aggregate declaration.</summary>
    public FoldedSnapshotQualification? Find(string domain, string aggregateType)
        => _entries.FirstOrDefault(item => string.Equals(item.Domain, domain, StringComparison.Ordinal)
            && string.Equals(item.AggregateType, aggregateType, StringComparison.Ordinal));

    /// <summary>Finds the exact domain/profile declaration.</summary>
    public FoldedSnapshotQualification? FindByProfile(string domain, string profileId)
        => _entries.FirstOrDefault(item => string.Equals(item.Domain, domain, StringComparison.Ordinal)
            && string.Equals(item.ProfileId, profileId, StringComparison.Ordinal));
}
