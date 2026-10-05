using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Replay;

/// <summary>Immutable handoff of one source-verified replay page to an async aggregate route.</summary>
/// <remarks>Construction does not authenticate the proof, token or session; the trusted route must do so before Apply.</remarks>
public sealed record PagedContext {
    private const int MaximumProofBytes = 2 * 1024 * 1024;
    private readonly byte[] _eventEvolutionProof;
    private readonly byte[] _scratchHandleToken;
    private readonly byte[]? _continuationToken;
    private readonly byte[] _storedAccumulator;
    private readonly byte[] _effectiveAccumulator;

    /// <summary>Snapshots page views and all public proof and handle bytes.</summary>
    public PagedContext(
        IReadOnlyList<VerifiedEffectiveEventView> verifiedPageViews,
        byte[] eventEvolutionProof,
        string tenantId,
        string domain,
        string aggregateType,
        string aggregateId,
        long expectedHead,
        long targetSequence,
        long pageStart,
        long pageEnd,
        int pageCount,
        string operationId,
        byte[] scratchHandleToken,
        byte[]? continuationToken,
        byte[] storedAccumulator,
        byte[] effectiveAccumulator,
        bool isComplete,
        IPagedReplayStateSession stateSession) {
        ArgumentNullException.ThrowIfNull(verifiedPageViews);
        ArgumentNullException.ThrowIfNull(eventEvolutionProof);
        ArgumentNullException.ThrowIfNull(scratchHandleToken);
        ArgumentNullException.ThrowIfNull(storedAccumulator);
        ArgumentNullException.ThrowIfNull(effectiveAccumulator);
        ArgumentNullException.ThrowIfNull(stateSession);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (pageCount is < 0 or > 256 || verifiedPageViews.Count != pageCount) {
            throw new ArgumentOutOfRangeException(nameof(pageCount));
        }

        if (eventEvolutionProof.Length is 0 or > MaximumProofBytes
            || scratchHandleToken.Length is 0 or > MaximumProofBytes
            || continuationToken?.Length > MaximumProofBytes) {
            throw new ArgumentOutOfRangeException(nameof(eventEvolutionProof), "A replay proof or opaque token exceeds its transport bound.");
        }

        if (storedAccumulator.Length != 32 || effectiveAccumulator.Length != 32) {
            throw new ArgumentException("Replay accumulators must contain exactly 32 bytes.");
        }

        var views = new VerifiedEffectiveEventView[pageCount];
        for (int index = 0; index < pageCount; index++) {
            views[index] = verifiedPageViews[index] ?? throw new ArgumentException("A replay page cannot contain a null view.", nameof(verifiedPageViews));
        }

        VerifiedPageViews = Array.AsReadOnly(views);
        _eventEvolutionProof = eventEvolutionProof.ToArray();
        TenantId = tenantId;
        Domain = domain;
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        ExpectedHead = expectedHead;
        TargetSequence = targetSequence;
        PageStart = pageStart;
        PageEnd = pageEnd;
        PageCount = pageCount;
        OperationId = operationId;
        _scratchHandleToken = scratchHandleToken.ToArray();
        _continuationToken = continuationToken?.ToArray();
        _storedAccumulator = storedAccumulator.ToArray();
        _effectiveAccumulator = effectiveAccumulator.ToArray();
        IsComplete = isComplete;
        StateSession = stateSession;
    }

    /// <summary>Gets the ordered current-payload views for this page.</summary>
    public IReadOnlyList<VerifiedEffectiveEventView> VerifiedPageViews { get; }

    /// <summary>Gets a copy of the exact signed page/prefix proof.</summary>
    public byte[] EventEvolutionProof => _eventEvolutionProof.ToArray();

    /// <summary>Gets the addressed tenant.</summary>
    public string TenantId { get; }

    /// <summary>Gets the addressed domain.</summary>
    public string Domain { get; }

    /// <summary>Gets the addressed aggregate type.</summary>
    public string AggregateType { get; }

    /// <summary>Gets the addressed aggregate ID.</summary>
    public string AggregateId { get; }

    /// <summary>Gets the fixed source head.</summary>
    public long ExpectedHead { get; }

    /// <summary>Gets the inclusive target sequence.</summary>
    public long TargetSequence { get; }

    /// <summary>Gets the first sequence in this page.</summary>
    public long PageStart { get; }

    /// <summary>Gets the last sequence in this page.</summary>
    public long PageEnd { get; }

    /// <summary>Gets the number of events in this page.</summary>
    public int PageCount { get; }

    /// <summary>Gets the stable replay operation ID.</summary>
    public string OperationId { get; }

    /// <summary>Gets a copy of the opaque prior-state handle.</summary>
    public byte[] ScratchHandleToken => _scratchHandleToken.ToArray();

    /// <summary>Gets a copy of the exact continuation token, when present.</summary>
    public byte[]? ContinuationToken => _continuationToken?.ToArray();

    /// <summary>Gets a copy of the stored-source accumulator.</summary>
    public byte[] StoredAccumulator => _storedAccumulator.ToArray();

    /// <summary>Gets a copy of the effective-payload accumulator.</summary>
    public byte[] EffectiveAccumulator => _effectiveAccumulator.ToArray();

    /// <summary>Gets whether the admitted page reaches the fixed target.</summary>
    public bool IsComplete { get; }

    /// <summary>Gets the borrowed private state session for this in-process call.</summary>
    [JsonIgnore]
    public IPagedReplayStateSession StateSession { get; }
}
