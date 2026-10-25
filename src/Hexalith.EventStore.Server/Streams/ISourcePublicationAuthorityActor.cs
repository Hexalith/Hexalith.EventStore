using Dapr.Actors;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Private independent authority-journal actor transport; all requests require exact workload bindings.</summary>
public interface ISourcePublicationAuthorityActor : IActor
{
    /// <summary>Executes one authenticated local journal turn without source, projector, receiver or primary-owner callbacks.</summary>
    Task<byte[]?> CallAsync(SourcePublicationAuthorityCall call);
}
