namespace Hexalith.EventStore.DomainService;

/// <summary>Declares the read-model store for private bounded mapped replay transport.</summary>
public interface IAsyncDomainProjectionRebuildTransportHandler
{
    /// <summary>Gets the same state-store component used by the handler's rebuild plan.</summary>
    string RebuildTransportStoreName { get; }
}
