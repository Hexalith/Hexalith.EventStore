namespace Hexalith.EventStore.DomainService;

/// <summary>One bounded linked discovery page; page numbers remain sequential after a crash.</summary>
public sealed record ProcessIndexPage<T>(int Version, int Page, IReadOnlyList<T> Entries, int? NextPage);
