#if SELECTED
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

namespace PublishedConsumer;

/// <summary>Resolves the registered manifest fixture's aggregate route through the supported production seam.</summary>
public sealed class FixtureAggregateTypeResolver : ICommandAggregateTypeResolver
{
    /// <inheritdoc/>
    public Task<string?> ResolveAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(command.Domain == "d" ? "r" : null);
}
#endif
