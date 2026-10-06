using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Exercises token forwarding to the optional protected asynchronous processor seam.</summary>
internal sealed class CancellationFixtureProcessor : DomainProcessorBase<CancellationReplayState>
{
    /// <inheritdoc/>
    protected override Task<DomainResult> HandleAsync(CommandEnvelope command, CancellationReplayState? currentState)
    {
        CancellationTestScope.Current.Handled++;
        return Task.FromResult(DomainResult.NoOp());
    }

    /// <inheritdoc/>
    protected override Task<DomainResult> HandleAsync(CommandEnvelope command, CancellationReplayState? currentState, CancellationToken cancellationToken)
    {
        CancellationTestScope.Current.ObservedHandlerToken = cancellationToken;
        return HandleAsync(command, currentState);
    }
}
