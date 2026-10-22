namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Marks a streamed full-replay projection whose ordinary mapped delivery can append
/// one contiguous event to a verified selected owner prefix.
/// </summary>
public interface IAsyncDomainProjectionIncrementalReplayHandler :
    IAsyncDomainProjectionStreamedRebuildHandler, IAsyncDomainProjectionRebuildHandler
{
}
