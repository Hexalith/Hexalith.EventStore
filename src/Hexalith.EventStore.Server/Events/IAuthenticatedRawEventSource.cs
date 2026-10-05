namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Server compatibility alias for the approved Contracts raw-source boundary.
/// Implementations must expose the same addressed provider read contract.
/// </summary>
public interface IAuthenticatedRawEventSource : global::Hexalith.EventStore.Contracts.Events.IAuthenticatedRawEventSource {
}
