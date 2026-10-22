using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Test attribution contract kept distinct from erased profile payloads.</summary>
/// <param name="Custody">The independent retention evidence.</param>
public sealed record HistoryCustodyProbeEvent(IdentityHistoryCustodyEvidence Custody) : IIdentityHistoryEvent;
