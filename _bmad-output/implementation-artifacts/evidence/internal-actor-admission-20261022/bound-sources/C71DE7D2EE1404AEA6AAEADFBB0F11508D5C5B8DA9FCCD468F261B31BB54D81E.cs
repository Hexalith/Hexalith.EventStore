using Dapr.Actors.Runtime;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// DAPR actor that owns cross-aggregate monotonic event position allocation.
/// </summary>
public partial class GlobalPositionActor(ActorHost host, ILogger<GlobalPositionActor> logger)
    : Actor(host), IGlobalPositionActor {
    /// <summary>
    /// The actor type name used for DAPR actor registration.
    /// </summary>
    public const string ActorTypeName = "GlobalPositionActor";

    private const string CurrentPositionStateKey = "current-global-position";

    /// <inheritdoc/>
    public async Task<long> AllocateAsync(int count) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        ConditionalValue<long> result = await StateManager
            .TryGetStateAsync<long>(CurrentPositionStateKey)
            .ConfigureAwait(false);

        long current = result.HasValue ? result.Value : 0;
        long first = checked(current + 1);
        long last = checked(current + count);

        await StateManager.SetStateAsync(CurrentPositionStateKey, last).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);

        Log.RangeAllocated(logger, Host.Id.GetId(), first, last, count);

        return first;
    }

    /// <inheritdoc/>
    public async Task<long> GetCurrentAsync() {
        ConditionalValue<long> result = await StateManager
            .TryGetStateAsync<long>(CurrentPositionStateKey)
            .ConfigureAwait(false);

        return result.HasValue ? result.Value : 0;
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 5050,
            Level = LogLevel.Debug,
            Message = "Global event position range allocated. ActorId={ActorId}, First={First}, Last={Last}, Count={Count}")]
        public static partial void RangeAllocated(
            ILogger logger,
            string actorId,
            long first,
            long last,
            int count);
    }
}
