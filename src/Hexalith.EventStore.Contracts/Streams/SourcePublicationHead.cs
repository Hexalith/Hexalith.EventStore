using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>The authenticated committed prefix to reconcile for one exact source.</summary>
/// <param name="Identity">The exact tenant/domain/source.</param>
/// <param name="Head">The committed source prefix, never a reserved global position.</param>
public sealed record SourcePublicationHead(AggregateIdentity Identity, long Head);
