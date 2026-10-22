using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Independently authenticates complete namespace installation and committed source-head coverage.</summary>
/// <remarks>Implementations must certify actual registration/backfill completeness and current authority. An observed maximum or caller-authored manifest is insufficient. No production default is installed.</remarks>
public interface ISourcePublicationNamespaceSource
{
    /// <summary>Reads an authenticated finite cut or returns null when installation/authority/completeness is unavailable.</summary>
    Task<SourcePublicationCut?> ReadAsync(SourcePublicationScope scope, CancellationToken cancellationToken = default);
}
