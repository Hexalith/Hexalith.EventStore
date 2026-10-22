using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Qualified physical all-or-none original reservation backend, including hold pins, every key copy and restore non-revival.</summary>
/// <remarks>Consume atomically checks the exact immutable target set/manifest and pins, destroys all targets or none and durably
/// retains one ordered irreversible vector. A partial implementation is invalid. Lost acknowledgement uses exact reservation lookup;
/// no interface or synthetic provider establishes physical atomicity, replication or destruction qualification.</remarks>
public interface IAtomicDeletionManifestProvider
{
    /// <summary>Consumes only the exact original irreversible owner reservation; exact retries never create another batch.</summary>
    Task<DeletionManifestProviderResult> ConsumeAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, CancellationToken cancellationToken = default);
    /// <summary>Returns exact original result, authoritative NotStarted, or Unknown; never infers absence from an outage.</summary>
    Task<DeletionManifestProviderResult> LookupAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, CancellationToken cancellationToken = default);
}
