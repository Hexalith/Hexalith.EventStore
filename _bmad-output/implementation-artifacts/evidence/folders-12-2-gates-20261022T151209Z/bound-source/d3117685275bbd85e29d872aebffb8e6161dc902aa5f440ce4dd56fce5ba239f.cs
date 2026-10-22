namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Current independent installed-coverage qualification, never inferred from stored receipt strings.</summary>
/// <param name="AuthorityRevision">Exact installation plus current authority/trust revision.</param>
/// <param name="ValidUntil">Exclusive current qualification boundary.</param>
public sealed record SourcePublicationNamespaceAuthorization(string AuthorityRevision, DateTimeOffset ValidUntil);
