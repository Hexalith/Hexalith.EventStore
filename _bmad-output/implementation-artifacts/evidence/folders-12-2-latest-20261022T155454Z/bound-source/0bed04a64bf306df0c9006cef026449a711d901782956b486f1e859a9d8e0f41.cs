using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>A content-free publication identity projected from an existing source-atomic event.</summary>
/// <param name="PublicationId">The immutable logical publication identity.</param>
/// <param name="Identity">The exact source identity.</param>
/// <param name="SourceRevision">The original source event position.</param>
/// <param name="SourceMessageId">The original source envelope message identity.</param>
/// <param name="StableFieldsDigest">Digest of the closed safe publication metadata tuple, never general content.</param>
public sealed record SourcePublicationDescriptor(string PublicationId, AggregateIdentity Identity, long SourceRevision, string SourceMessageId, string StableFieldsDigest);
