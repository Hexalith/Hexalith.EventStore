namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>A detached complete set of exact source prefixes under one reconfirmed namespace cut.</summary>
/// <param name="Cut">Current independently authenticated namespace coverage and committed heads.</param>
/// <param name="Sources">Every nonempty exact source in the cut; authenticated zero heads certify uncreated registrations.</param>
/// <remarks>Deployment coverage and all-writer enforcement still require independent qualification.</remarks>
public sealed record SourceNamespaceSnapshot(SourcePublicationCut Cut, IReadOnlyList<AuthoritativeEventStream> Sources);
