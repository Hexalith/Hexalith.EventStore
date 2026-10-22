using Hexalith.EventStore.Client.Processes;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>A distinct catalog adapter for conflicting-registration checks.</summary>
internal sealed class AlternateProcessCatalog : IProcessSourceCatalog
{
    /// <inheritdoc/>
    public Task<ProcessSourcePage> ListAsync(string? afterKey, int limit, CancellationToken cancellationToken = default)
        => Task.FromResult(new ProcessSourcePage([], null));
}
