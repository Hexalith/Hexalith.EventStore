using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Reads bounded raw event pages with trusted provider readback evidence.</summary>
public interface IAuthenticatedRawEventSource {
    /// <summary>Reads one addressed raw page after applying the supplied count and byte ceilings.</summary>
    /// <param name="tenantId">The addressed tenant identifier.</param>
    /// <param name="domain">The addressed domain.</param>
    /// <param name="aggregateType">The addressed aggregate type.</param>
    /// <param name="aggregateId">The addressed aggregate identifier.</param>
    /// <param name="startSequence">The first requested sequence number.</param>
    /// <param name="maxCount">The maximum event count, from 1 through 256.</param>
    /// <param name="maxRawBytes">The checked raw byte ceiling, at most 128 MiB.</param>
    /// <param name="maxReadableBytes">The checked readable byte ceiling, at most 64 MiB.</param>
    /// <param name="cancellationToken">The cancellation token for the provider boundary.</param>
    /// <returns>The exact addressed page and its authenticated provider readback proof.</returns>
    Task<AuthenticatedRawEventPage> ReadRawPageAsync(
        string tenantId,
        string domain,
        string aggregateType,
        string aggregateId,
        long startSequence,
        int maxCount,
        long maxRawBytes,
        long maxReadableBytes,
        CancellationToken cancellationToken);
}
