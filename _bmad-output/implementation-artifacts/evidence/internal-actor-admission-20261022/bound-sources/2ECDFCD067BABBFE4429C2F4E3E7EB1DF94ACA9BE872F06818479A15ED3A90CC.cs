using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Reads bounded complete prefixes through the authenticated gateway, never projections.</summary>
public interface IAuthoritativeEventStreamReader
{
    /// <summary>Reads the exact authorized aggregate and verifies a stable current checkpoint.</summary>
    /// <param name="identity">The immutable tenant/domain/aggregate scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A complete prefix or a classified unavailable result.</returns>
    Task<AuthoritativeStreamReadResult> ReadAsync(AggregateIdentity identity, CancellationToken cancellationToken = default);
}
