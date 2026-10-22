using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Independently validates current installed finite legacy coverage, complete inventory and all-writer registration enforcement.</summary>
/// <remarks>No available default. An opaque stored receipt, static configuration, source maximum or mock is not production completeness.</remarks>
public interface ISourcePublicationNamespaceAuthority
{
    /// <summary>Returns exact current authority and exclusive validity only after independent scope/coverage/enforcement validation.</summary>
    Task<SourcePublicationNamespaceAuthorization?> AuthorizeAsync(SourcePublicationNamespaceState installed, CancellationToken cancellationToken = default);
}
