using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace SnapshotAliasProbe;

/// <summary>Exercises the public built-in processor and records whether Handle ran.</summary>
public sealed class ProbeProcessor : DomainProcessorBase<MutationState>
{
    /// <summary>Gets whether reconstruction reached command handling.</summary>
    public bool Handled { get; private set; }

    /// <inheritdoc/>
    protected override Task<DomainResult> HandleAsync(CommandEnvelope command, MutationState? currentState)
    {
        Handled = true;
        return Task.FromResult(DomainResult.NoOp());
    }
}
