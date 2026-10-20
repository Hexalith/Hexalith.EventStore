namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// A read-model value paired with the ETag under which it was read.
/// </summary>
/// <typeparam name="TValue">The read-model type.</typeparam>
/// <param name="Value">The persisted value, or <see langword="null"/> when the key is absent.</param>
/// <param name="ETag">The ETag of the read, or <see langword="null"/> when the key is absent.</param>
public sealed record ReadModelEntry<TValue>(TValue? Value, string? ETag)
    where TValue : class
{
    /// <summary>SDK-owned source expiry evidence for shared inheritance; absent on legacy implementations.</summary>
    internal DateTimeOffset? ExpiresAtUtc { get; init; }
}
