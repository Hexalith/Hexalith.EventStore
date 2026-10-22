using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>An independently authenticated finite namespace cut. This shape alone does not qualify its issuer.</summary>
/// <param name="Scope">The installed exact namespace/feed.</param>
/// <param name="AuthorityRevision">The exact namespace installation/coverage authority revision.</param>
/// <param name="ObservedAt">The authenticated observation instant.</param>
/// <param name="ValidUntil">The exclusive current-authorization limit.</param>
/// <param name="Sources">Every source and committed head covered by this finite cut.</param>
/// <param name="IsComplete">Whether the independent source certifies complete namespace coverage.</param>
public sealed record SourcePublicationCut(SourcePublicationScope Scope, string AuthorityRevision, DateTimeOffset ObservedAt, DateTimeOffset ValidUntil, IReadOnlyList<SourcePublicationHead> Sources, bool IsComplete);
