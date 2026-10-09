using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Observes private state delivery and injects exact processor boundary failures.</summary>
internal sealed class DaprLogicalCommandStateProcessor : IDomainProcessor, IAsyncDomainProcessor
{
    /// <summary>Gets invocation count.</summary>
    internal int Calls
    {
        get; private set;
    }
    /// <summary>Gets the privately decoded counter value.</summary>
    internal int Value
    {
        get; private set;
    }
    /// <summary>Gets or sets the callback hook before returning a result.</summary>
    internal Action? Hook
    {
        get; set;
    }
    /// <summary>Gets or sets an explicit result for producer controls.</summary>
    internal DomainResult Result { get; set; } = DomainResult.NoOp();
    /// <inheritdoc/>
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? state)
    {
        Calls++;
        Value = ((DaprLogicalReconstructionTestState)state!).Value;
        Hook?.Invoke();
        return Task.FromResult(Result);
    }
    /// <inheritdoc/>
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? state, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ProcessAsync(command, state);
    }
}
