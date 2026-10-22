using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Marks finite purpose-protected opaque attribution; it cannot use the profile-key lifecycle.</summary>
public interface IIdentityHistoryEvent : IEventPayload
{
    /// <summary>Gets the accepted independent history protection/expiry evidence.</summary>
    IdentityHistoryCustodyEvidence Custody { get; }
}
