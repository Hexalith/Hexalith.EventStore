using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Original authenticated reservation retained for one actor entry when physical recovery times out.</summary>
internal sealed class EntryOutcomeHolder
{
    /// <summary>Gets or sets the entry's own durable reservation.</summary>
    internal DeletionConsumptionOutcome? Reservation { get; set; }
}
