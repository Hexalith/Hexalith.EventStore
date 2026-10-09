using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Exposes the virtual result getter for exact callback and bounded no-op controls.</summary>
/// <param name="Inputs">The exact caller-supplied result list.</param>
internal sealed record DaprLogicalCommandStateResult(IReadOnlyList<IEventPayload> Inputs) : DomainResult(Inputs)
{
    /// <summary>Gets or sets the application payload getter.</summary>
    internal Func<string?>? Getter
    {
        get; set;
    }
    /// <inheritdoc/>
    public override string? ResultPayload => Getter?.Invoke();
}
