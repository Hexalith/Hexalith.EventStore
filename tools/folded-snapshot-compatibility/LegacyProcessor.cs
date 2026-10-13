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

    /// <summary>Gets the original result returned by the old binary.</summary>
    public static DomainResult? LastResult { get; private set; }

    /// <inheritdoc/>
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastCommand = command;
        LastState = currentState;
        LastCancellationToken = cancellationToken;
        LastResult = command.CommandType switch
        {
            "LegacySuccess" => new LegacyEventfulResult([new LegacySuccessEvent()], "legacy-success-result"),
            "LegacyRejection" => new LegacyEventfulResult([new LegacyRejectionEvent()], "legacy-rejection-result"),
            _ => DomainResult.NoOp(),
        };
        return Task.FromResult(LastResult);
    }
}
