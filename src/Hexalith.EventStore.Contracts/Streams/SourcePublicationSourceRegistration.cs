using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>An exact pre-create roster operation.</summary>
/// <param name="Scope">The installed namespace.</param><param name="ExpectedRevision">The roster compare revision.</param><param name="Identity">The exact new source.</param>
public sealed record SourcePublicationSourceRegistration(SourcePublicationScope Scope, long ExpectedRevision, AggregateIdentity Identity);
