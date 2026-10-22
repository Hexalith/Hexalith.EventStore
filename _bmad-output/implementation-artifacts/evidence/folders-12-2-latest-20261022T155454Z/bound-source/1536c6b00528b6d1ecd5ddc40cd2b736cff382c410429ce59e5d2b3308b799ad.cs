using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Holds a valid command result and an optional private post-admission fold callback.</summary>
public sealed class FoldedSnapshotProcessingResult
{
    /// <summary>Creates a result whose optional capture sees only once-admitted wire events.</summary>
    public FoldedSnapshotProcessingResult(
        DomainResult result,
        Func<IReadOnlyList<DomainServiceWireEvent>, CancellationToken, string?>? capture)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        Capture = capture;
    }

    /// <summary>Gets the unchanged command result.</summary>
    public DomainResult Result { get; }

    /// <summary>Gets the optional capture callback; failures omit snapshot evidence.</summary>
    public Func<IReadOnlyList<DomainServiceWireEvent>, CancellationToken, string?>? Capture { get; }
}
