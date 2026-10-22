using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Private durable installed roster; independent authority must qualify exact legacy coverage and all-writer registration.</summary>
/// <param name="Scope">Exact installed feed.</param>
/// <param name="Revision">Monotonic roster compare revision.</param>
/// <param name="AuthorityRevision">Installation authority identity.</param>
/// <param name="LegacyCoverageReceipt">Opaque independently authenticated finite legacy coverage receipt.</param>
/// <param name="WriterEnforcementReceipt">Opaque independent all-writer registration/enforcement receipt.</param>
/// <param name="InitialSources">Immutable installation roster retained separately for exact installation retries.</param>
/// <param name="Sources">Complete registered source roster, including provisionally registered empty sources.</param>
public sealed record SourcePublicationNamespaceState(SourcePublicationScope Scope, long Revision, string AuthorityRevision,
    string LegacyCoverageReceipt, string WriterEnforcementReceipt, IReadOnlyList<AggregateIdentity> Sources, IReadOnlyList<AggregateIdentity>? InitialSources = null);
