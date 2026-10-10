namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Server compatibility alias for the approved Contracts raw-source boundary.
/// Implementations must expose the same addressed provider read contract.
/// </summary>
[Obsolete("Legacy event evolution compatibility contract.")]
public interface IAuthenticatedRawEventSource : global::Hexalith.EventStore.Contracts.Events.IAuthenticatedRawEventSource {
}
