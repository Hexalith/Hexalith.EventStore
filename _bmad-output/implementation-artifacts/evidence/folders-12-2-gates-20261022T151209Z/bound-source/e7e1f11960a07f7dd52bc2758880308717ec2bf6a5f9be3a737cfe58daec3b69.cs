namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Declares that ordinary named delivery may call the handler once for each source event,
/// in owner sequence order, and safely repeat an already delivered event after a retry.
/// </summary>
public interface IAsyncDomainProjectionSingleEventHandler : IAsyncDomainProjectionHandler;
