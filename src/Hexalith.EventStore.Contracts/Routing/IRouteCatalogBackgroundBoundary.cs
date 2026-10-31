namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Acquires a separately owned execution lifetime before an admitted action schedules detached work.</summary>
public interface IRouteCatalogBackgroundBoundary
{
    /// <summary>Acquires and binds work to the same verified run before acceptance or scheduling.</summary>
    Task<IRouteCatalogBackgroundWork> AcquireAsync(CancellationToken cancellationToken);
}
