using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>Implements only the pre-capture invoker signature for default-method compatibility tests.</summary>
internal sealed class LegacyOnlyDomainServiceInvoker(DomainResult response) : IDomainServiceInvoker
{
    internal int CallCount { get; private set; }

    public Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(response);
    }
}
