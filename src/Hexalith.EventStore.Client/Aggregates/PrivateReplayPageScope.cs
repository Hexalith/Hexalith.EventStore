namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Names one already admitted operation, owner generation and private replay page.</summary>
/// <remarks>This local scope neither authenticates a source nor proves a durable page transition.</remarks>
internal sealed record PrivateReplayPageScope
{
    /// <summary>Validates the bounded range before any private state allocation.</summary>
    internal PrivateReplayPageScope(string tenantId, string domain, string aggregateType, string aggregateId,
        string operationId, long ownerGeneration, long expectedHead, long targetSequence,
        long pageStart, int pageCount, bool includeTimeline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (ownerGeneration < 1 || expectedHead < 0 || targetSequence < 0 || targetSequence > expectedHead
            || pageStart < 1 || pageCount is < 0 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(pageCount), "ReplayRestartRequired: invalid private replay scope.");
        }

        bool validRange = pageCount == 0
            ? (targetSequence == 0 && pageStart == 1)
                || (targetSequence > 0 && targetSequence < long.MaxValue && pageStart == targetSequence + 1
                    && expectedHead == targetSequence && !includeTimeline)
                || (pageStart == long.MaxValue && targetSequence == long.MaxValue && expectedHead == long.MaxValue
                    && !includeTimeline)
            : pageStart <= targetSequence && pageCount - 1L <= targetSequence - pageStart;
        if (!validRange)
        {
            throw new ArgumentException("ReplayRestartRequired: the private page range exceeds the admitted target.");
        }

        if (includeTimeline && targetSequence > 1000)
        {
            throw new InvalidOperationException("TimelineLimit: a timeline cannot exceed 1,000 entries.");
        }

        TenantId = tenantId;
        Domain = domain;
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        OperationId = operationId;
        OwnerGeneration = ownerGeneration;
        ExpectedHead = expectedHead;
        TargetSequence = targetSequence;
        PageStart = pageStart;
        PageCount = pageCount;
        IncludeTimeline = includeTimeline;
    }

    /// <summary>Gets the addressed tenant.</summary>
    internal string TenantId { get; }
    /// <summary>Gets the exact domain.</summary>
    internal string Domain { get; }
    /// <summary>Gets the aggregate route.</summary>
    internal string AggregateType { get; }
    /// <summary>Gets the addressed aggregate.</summary>
    internal string AggregateId { get; }
    /// <summary>Gets the stable operation identity.</summary>
    internal string OperationId { get; }
    /// <summary>Gets the admitted scratch-owner generation.</summary>
    internal long OwnerGeneration { get; }
    /// <summary>Gets the fixed source head.</summary>
    internal long ExpectedHead { get; }
    /// <summary>Gets the inclusive replay target.</summary>
    internal long TargetSequence { get; }
    /// <summary>Gets the first admitted page sequence or zero-event sentinel.</summary>
    internal long PageStart { get; }
    /// <summary>Gets the number of admitted events.</summary>
    internal int PageCount { get; }
    /// <summary>Gets whether every post-Apply state must be retained privately.</summary>
    internal bool IncludeTimeline { get; }
}
