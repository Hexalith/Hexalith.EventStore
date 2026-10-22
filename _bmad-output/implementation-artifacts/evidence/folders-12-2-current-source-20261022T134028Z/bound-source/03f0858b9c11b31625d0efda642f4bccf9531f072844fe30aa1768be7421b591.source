using Dapr.Actors;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Private actor-owned installed roster. Its control-plane/writer credentials must be qualified and restricted separately.</summary>
public interface ISourcePublicationNamespaceActor : IActor
{
    /// <summary>Reads only committed installed inventory.</summary>
    Task<SourcePublicationNamespaceState?> ReadAsync(SourcePublicationScope scope);
    /// <summary>Installs once at NoStream; exact retry is idempotent, changed coverage conflicts.</summary>
    Task<bool> InstallAsync(SourcePublicationNamespaceState installation);
    /// <summary>Registers before source event persistence at the exact roster revision. Exact duplicate reuses the original roster.</summary>
    Task<bool> RegisterAsync(SourcePublicationScope scope, long expectedRevision, AggregateIdentity identity);
}
