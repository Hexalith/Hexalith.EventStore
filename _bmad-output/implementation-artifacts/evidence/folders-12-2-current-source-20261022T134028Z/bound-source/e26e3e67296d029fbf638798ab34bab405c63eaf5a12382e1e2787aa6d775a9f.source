using Hexalith.EventStore.Client.Processes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>An authoritative catalog with no synthetic process sources.</summary>
internal sealed class EmptyCatalog : IProcessSourceCatalog
{
    /// <inheritdoc/>
    public Task<ProcessSourcePage> ListAsync(string? afterKey, int limit, CancellationToken cancellationToken = default)
        => Task.FromResult(new ProcessSourcePage([], null));
}
