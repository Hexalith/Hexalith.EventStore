namespace Hexalith.EventStore.Client.Processes;

/// <summary>Enumerates authoritative sources through mutation-safe keyset continuation.</summary>
/// <remarks>The implementation must return keys strictly after the requested key and may not use an offset cursor. A removed earlier key cannot skip a later source.</remarks>
public interface IProcessSourceCatalog
{
    /// <summary>Returns at most <paramref name="limit"/> sources after the given canonical key.</summary>
    Task<ProcessSourcePage> ListAsync(string? afterKey, int limit, CancellationToken cancellationToken = default);
}
