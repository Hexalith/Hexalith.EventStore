namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>One exact verified source prefix proposed for independent retention; caller observations and descriptors grant no authority.</summary>
/// <param name="Cut">Fresh independently complete source cut.</param><param name="ExpectedVerifiedSources">Consecutive retained source predecessor, or zero for a changed cut.</param>
/// <param name="Source">The exact next source and certified prefix head.</param><param name="ObservationId">Original independently authenticated protected-source observation.</param>
/// <param name="ObservedHead">Committed head observed by that source proof, possibly ahead of the cut prefix.</param>
/// <param name="Publications">Exact closed projection of that certified prefix, including an empty projection.</param>
public sealed record SourcePublicationReconciliationAdvance(SourcePublicationCut Cut, int ExpectedVerifiedSources, SourcePublicationHead Source,
    string ObservationId, long ObservedHead, IReadOnlyList<SourcePublicationDescriptor> Publications)
{
    /// <summary>Gets the freshly authenticated current complete cut when advancing the independently retained original finite cut. Omission requires exact current-cut identity.</summary>
    public SourcePublicationCut? CurrentCut { get; init; }
}
