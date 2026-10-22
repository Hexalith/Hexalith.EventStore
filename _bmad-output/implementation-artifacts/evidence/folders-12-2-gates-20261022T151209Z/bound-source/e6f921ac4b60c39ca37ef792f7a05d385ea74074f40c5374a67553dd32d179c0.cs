using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Persists private replay context beside unchanged historical part fields in the owning component.</summary>
internal sealed record DomainProjectionStoredReplayPart(ProjectionDispatchRequest Header, int Ordinal, byte[] Data)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectionReplayTransportContext? ReplayContext { get; init; }

    internal ProjectionRebuildTransportPartRequest Restore()
    {
        if (Header?.Request is null) throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        try { return new(ReplayContext?.Apply(Header) ?? Header, Ordinal, Data); }
        catch (JsonException) { throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict); }
    }
}
