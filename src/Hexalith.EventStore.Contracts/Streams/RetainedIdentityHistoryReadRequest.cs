using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Requests independently retained attribution, without requesting profile payloads.</summary>
/// <param name="Identity">The exact authenticated source identity.</param>
/// <param name="Purpose">The accepted purpose; callers cannot select event types.</param>
public sealed record RetainedIdentityHistoryReadRequest(AggregateIdentity Identity, string Purpose)
{
    /// <summary>The closed V1 human attribution purpose.</summary>
    public const string AttributionPurpose = "party-actor-history-v1";
}
