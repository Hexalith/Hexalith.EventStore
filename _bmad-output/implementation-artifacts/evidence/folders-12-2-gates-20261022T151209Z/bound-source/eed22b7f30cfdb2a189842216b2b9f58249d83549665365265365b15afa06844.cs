namespace Hexalith.EventStore.Client.Projections;

/// <summary>Locates one staged mutation within its immutable stream page.</summary>
/// <param name="Key">The logical read-model key.</param>
/// <param name="PageOrdinal">The containing page position.</param>
/// <param name="MutationOrdinal">The mutation position within the page.</param>
internal sealed record SharedProjectionStreamKey(string Key, int PageOrdinal, int MutationOrdinal);
