using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>A stable model value whose real path exists only after verified toolkit rendering.</summary>
internal sealed class DevelopmentRouteCatalogComponentPath(DevelopmentRouteCatalogComponentObserver observer) : IValueProvider
{
    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<string?>(observer.ComponentPath
            ?? throw new InvalidOperationException("The actual modeled authority has not been rendered and verified."));
    }
}
