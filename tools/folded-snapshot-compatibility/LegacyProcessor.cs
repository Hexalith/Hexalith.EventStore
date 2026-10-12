using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace FoldedSnapshotCompatibility;

/// <summary>Old async processor implementation with no capture capability.</summary>
public sealed class LegacyProcessor : IAsyncDomainProcessor
{
    /// <summary>Gets the number of original-method calls.</summary>
    public static int Calls { get; private set; }

    /// <summary>Gets the exact command passed to the old binary.</summary>
    public static CommandEnvelope? LastCommand { get; private set; }

    /// <summary>Gets the exact state passed to the old binary.</summary>
    public static object? LastState { get; private set; }

    /// <summary>Gets the cancellation token passed to the old binary.</summary>
    public static CancellationToken LastCancellationToken { get; private set; }

    /// <inheritdoc/>
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastCommand = command;
        LastState = currentState;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(DomainResult.NoOp());
    }
}
