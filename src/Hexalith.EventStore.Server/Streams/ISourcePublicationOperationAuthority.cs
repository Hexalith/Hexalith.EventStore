using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Independent private current caller/resource/method admission, separate from finite-namespace coverage qualification.
/// Missing credentials deny raw actors; actor address and stored receipt strings never authenticate a caller.</summary>
public interface ISourcePublicationOperationAuthority
{
    /// <summary>Authenticates only the exact index read.</summary>
    Task<bool> ReadIndexAsync(SourcePublicationScope scope);
    /// <summary>Authenticates the exact conditional index request, including immutable publication vector and expected revision.</summary>
    Task<bool> WriteIndexAsync(SourcePublicationIndexWrite request);
    /// <summary>Authenticates only the exact installed namespace read.</summary>
    Task<bool> ReadNamespaceAsync(SourcePublicationScope scope);
    /// <summary>Authenticates exact initial inventory and independent installation receipts.</summary>
    Task<bool> InstallNamespaceAsync(SourcePublicationNamespaceState installation);
    /// <summary>Authenticates only the exact pre-create registered source/revision within this installed scope.</summary>
    Task<bool> RegisterSourceAsync(SourcePublicationScope scope, long expectedRevision, AggregateIdentity identity);
}
