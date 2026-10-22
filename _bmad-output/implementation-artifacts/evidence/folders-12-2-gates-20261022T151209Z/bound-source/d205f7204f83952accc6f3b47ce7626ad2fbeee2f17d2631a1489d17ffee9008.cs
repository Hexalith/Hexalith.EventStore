using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Optional independently governed actor-free terminal destruction proof over exact existing sealed source metadata.</summary>
/// <remarks>Implement beside IIdentityHistoryCustody only when the qualified owner can bind the original ciphertext/source
/// to a complete irreversible destruction receipt under fresh nonrollback lifecycle authority. Expiry alone, a read denial,
/// provider failure or missing acknowledgement cannot certify destruction. No expired binding may be decrypted or renewed.</remarks>
public interface IExpiredIdentityHistoryCustody
{
    /// <summary>Returns fresh exact-source terminal proof, or null; null never authorizes skipping an unreadable transition.</summary>
    Task<ExpiredIdentityHistoryCertificate?> ReadExpiredAsync(AggregateIdentity identity, string eventTypeName,
        long sourceSequence, byte[] sealedPayload, string serializationFormat, CancellationToken cancellationToken = default);
}
