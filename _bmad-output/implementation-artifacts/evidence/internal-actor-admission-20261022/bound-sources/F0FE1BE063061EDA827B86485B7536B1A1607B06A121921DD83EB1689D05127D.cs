namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Reads exact addressed source bytes and provider evidence before typed event materialization.</summary>
[Obsolete("Legacy event evolution compatibility contract.")]
public interface IAuthenticatedRawEventSource
{
    /// <summary>Reads one bounded addressed raw page from the authoritative provider.</summary>
    /// <param name="tenantId">The addressed tenant.</param>
    /// <param name="domain">The addressed domain.</param>
    /// <param name="aggregateType">The addressed aggregate type.</param>
    /// <param name="aggregateId">The addressed aggregate identifier.</param>
    /// <param name="startSequence">The first requested stream sequence.</param>
    /// <param name="maxCount">The maximum page count, from 1 through 256.</param>
    /// <param name="maxRawBytes">The checked raw source byte ceiling, at most 128 MiB.</param>
    /// <param name="maxReadableBytes">The checked readable payload ceiling, at most 64 MiB.</param>
    /// <param name="cancellationToken">The originating operation cancellation token.</param>
    /// <returns>A detached page whose raw bytes and proof still require authentication.</returns>
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
