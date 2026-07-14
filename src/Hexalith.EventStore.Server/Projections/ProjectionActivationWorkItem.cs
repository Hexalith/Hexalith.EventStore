using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Payload-free write-ahead activation for one aggregate projection stream.</summary>
/// <param name="ActivationId">The deterministic aggregate activation id.</param>
/// <param name="TenantId">The aggregate tenant.</param>
/// <param name="Domain">The aggregate domain.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="Revision">The monotonic activation revision used to fence stale workers.</param>
/// <param name="Attempt">The bounded activation attempt.</param>
/// <param name="NextDueUtc">The next eligible activation time.</param>
public sealed record ProjectionActivationWorkItem(
    string ActivationId,
    string TenantId,
    string Domain,
    string AggregateId,
    long Revision,
    int Attempt,
    DateTimeOffset NextDueUtc) {
    /// <summary>Creates a payload-free deterministic activation.</summary>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="dueUtc">The initial due time.</param>
    /// <returns>The activation work item.</returns>
    public static ProjectionActivationWorkItem Create(AggregateIdentity identity, DateTimeOffset dueUtc) {
        ArgumentNullException.ThrowIfNull(identity);
        byte[] value = Encoding.UTF8.GetBytes(identity.ActorId);
        return new ProjectionActivationWorkItem(
            Convert.ToHexStringLower(SHA256.HashData(value)),
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            1,
            0,
            dueUtc);
    }
}
