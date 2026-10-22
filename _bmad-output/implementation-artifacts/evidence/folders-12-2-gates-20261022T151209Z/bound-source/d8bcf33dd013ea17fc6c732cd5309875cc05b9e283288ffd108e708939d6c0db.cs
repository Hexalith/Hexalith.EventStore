namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Materialized small per-source values under one complete reconfirmed namespace cut; source payloads are released after each fold.</summary>
/// <typeparam name="T">Materialized source summary, containing no deferred source enumeration.</typeparam>
/// <param name="Cut">Complete current namespace authority and original head vector.</param><param name="Values">Every nonempty committed source summary.</param>
public sealed record SourceNamespaceFold<T>(SourcePublicationCut Cut, IReadOnlyList<T> Values) where T : class;
