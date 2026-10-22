using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.DomainService;

/// <summary>Retains output capacity until the final owner fence accepts dispatch or clears every refused output array.</summary>
internal sealed class PrivateProducedDomainResult : IDisposable
{
    private DomainServiceWireResult? _wire;
    private EventBufferReservation? _charge;

    /// <summary>Takes already admitted output bytes and their still-live reservation.</summary>
    internal PrivateProducedDomainResult(DomainServiceWireResult wire, EventBufferReservation? charge)
    {
        _wire = wire;
        _charge = charge;
    }

    /// <summary>Gets the private charged result during final-fence verification.</summary>
    internal DomainServiceWireResult Wire => _wire ?? throw new ObjectDisposedException(nameof(PrivateProducedDomainResult));

    /// <summary>Transfers the accepted DTO at the dispatch boundary and then releases its private pipeline capacity.</summary>
    internal DomainServiceWireResult Release()
    {
        DomainServiceWireResult wire = Interlocked.Exchange(ref _wire, null) ?? throw new ObjectDisposedException(nameof(PrivateProducedDomainResult));
        Interlocked.Exchange(ref _charge, null)?.Dispose();
        return wire;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        DomainServiceWireResult? wire = Interlocked.Exchange(ref _wire, null);
        if (wire is not null)
        {
            foreach (DomainServiceWireEvent item in wire.Events)
            {
                CryptographicOperations.ZeroMemory(item.Payload);
            }
        }
        Interlocked.Exchange(ref _charge, null)?.Dispose();
    }
}
