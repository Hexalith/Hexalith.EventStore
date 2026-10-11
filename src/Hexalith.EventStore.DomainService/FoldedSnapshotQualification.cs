using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.DomainService;

/// <summary>Immutable deployment-owned enrollment of one reviewed folded snapshot profile.</summary>
public sealed class FoldedSnapshotQualification
{
    /// <summary>Creates a qualification bound to one domain, aggregate, state type and evidence.</summary>
    public FoldedSnapshotQualification(
        string domain,
        string aggregateType,
        Type stateType,
        string profileId,
        string parityEvidenceId,
        FoldedSnapshotCaptureLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentNullException.ThrowIfNull(stateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(parityEvidenceId);
        ArgumentNullException.ThrowIfNull(limits);
        Domain = domain;
        AggregateType = aggregateType;
        StateType = stateType;
        ProfileId = profileId;
        ParityEvidenceId = parityEvidenceId;
        Limits = limits;
    }

    /// <summary>Gets the exact domain.</summary>
    public string Domain { get; }

    /// <summary>Gets the exact aggregate type.</summary>
    public string AggregateType { get; }

    /// <summary>Gets the exact state CLR type.</summary>
    public Type StateType { get; }

    /// <summary>Gets the exact serializer/deployment profile identifier.</summary>
    public string ProfileId { get; }

    /// <summary>Gets the reviewed parity evidence identifier.</summary>
    public string ParityEvidenceId { get; }

    /// <summary>Gets the finite bound reserved before the actor parses optional state evidence.</summary>
    public int MaximumActorStateBytes => Limits.MaximumActorParsedStateBytes;

    /// <summary>Gets the maximum token count admitted before actor-side JSON graph construction.</summary>
    public int MaximumActorNodeCount => Limits.MaximumNodeCount;

    /// <summary>Gets the maximum actor-side JSON depth.</summary>
    public int MaximumActorDepth => Limits.MaximumDepth;

    /// <summary>Gets the exact deployment-owned resource limits.</summary>
    public FoldedSnapshotCaptureLimits Limits { get; }
}
