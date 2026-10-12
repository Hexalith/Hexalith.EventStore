using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;

namespace FoldedSnapshotCompatibility;

/// <summary>Old invoker implementation with only the original method.</summary>
public sealed class LegacyInvoker : IDomainServiceInvoker
{
    /// <summary>Gets the result owned by the old binary.</summary>
    public static DomainResult Result { get; } = DomainResult.NoOp();

    /// <summary>Gets the number of original-method calls.</summary>
    public static int Calls { get; private set; }

    /// <summary>Gets the exact command passed to the old binary.</summary>
    public static CommandEnvelope? LastCommand { get; private set; }

    /// <summary>Gets the exact state passed to the old binary.</summary>
    public static object? LastState { get; private set; }

    /// <summary>Gets the cancellation token passed to the old binary.</summary>
    public static CancellationToken LastCancellationToken { get; private set; }

    /// <inheritdoc/>
    public Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        LastCommand = command;
        LastState = currentState;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Result);
    }
}
