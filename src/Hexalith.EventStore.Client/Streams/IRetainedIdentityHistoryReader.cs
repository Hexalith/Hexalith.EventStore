using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Reads purpose-retained history independently of an erased profile's decryption key.</summary>
public interface IRetainedIdentityHistoryReader
{
    /// <summary>Reads and validates the exact authenticated retained-history partition.</summary>
    Task<RetainedIdentityHistoryReadResult> ReadAsync(AggregateIdentity identity, string purpose,
        CancellationToken cancellationToken = default);
}
