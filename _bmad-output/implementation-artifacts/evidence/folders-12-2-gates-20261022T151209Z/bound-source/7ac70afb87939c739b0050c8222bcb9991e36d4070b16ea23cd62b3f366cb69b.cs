using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Support-safe bounded recovery diagnostics, excluding payloads and provider exception details.</summary>
internal static partial class AggregateActorRecoveryLog
{
    /// <summary>Records why one recovery owner retained its evidence.</summary>
    [LoggerMessage(EventId = 8030, Level = LogLevel.Warning,
        Message = "Recovery evidence retained: ActorId={ActorId}, Owner={Owner}, Reason={Reason}")]
    internal static partial void Retained(ILogger logger, string actorId, string owner, string reason);
}
