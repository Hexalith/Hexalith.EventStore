using Dapr.Actors.Runtime;

namespace Hexalith.EventStore.Server.Tests.GlobalPositions;

/// <summary>
/// A serialized-call fixture for admission and reservation arithmetic.
/// It does not provide actor ownership, distributed concurrency, or provider qualification.
/// </summary>
/// <param name="stateManager">The isolated test state manager.</param>
internal sealed class QualificationAllocator(IActorStateManager stateManager)
{
    /// <summary>Admits one generation without resetting an existing reservation ceiling.</summary>
    /// <param name="shard">The exact shard to admit.</param>
    /// <param name="generation">The positive generation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task completing after the admission is saved.</returns>
    internal async Task AdmitAsync(QualificationShard shard, long generation, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generation);
        string key = shard.StateKey();
        ConditionalValue<QualificationAllocationState> existing = await stateManager
            .TryGetStateAsync<QualificationAllocationState>(key, cancellationToken);

        if (existing.HasValue)
        {
            ValidateState(shard, existing.Value);
            if (existing.Value.Generation != generation)
            {
                throw new InvalidOperationException("An admitted generation cannot be replaced.");
            }

            return;
        }

        await stateManager.SetStateAsync(key, new QualificationAllocationState(shard, generation, 0), cancellationToken);
        await stateManager.SaveStateAsync(cancellationToken);
    }

    /// <summary>Reserves a range and saves its ceiling before returning its first identity.</summary>
    /// <param name="shard">The previously admitted exact shard.</param>
    /// <param name="count">The positive reservation count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first position; subsequent identities use consecutive counters.</returns>
    internal async Task<QualificationPosition> ReserveAsync(QualificationShard shard, int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        string key = shard.StateKey();
        ConditionalValue<QualificationAllocationState> existing = await stateManager
            .TryGetStateAsync<QualificationAllocationState>(key, cancellationToken);
        if (!existing.HasValue)
        {
            throw new InvalidOperationException("The shard has not been admitted.");
        }

        QualificationAllocationState current = existing.Value;
        ValidateState(shard, current);
        long first = checked(current.LastReservedCounter + 1);
        long last = checked(current.LastReservedCounter + count);
        await stateManager.SetStateAsync(key, current with { LastReservedCounter = last }, cancellationToken);
        await stateManager.SaveStateAsync(cancellationToken);
        return new QualificationPosition(2, 0, shard, current.Generation, first);
    }

    private static void ValidateState(QualificationShard shard, QualificationAllocationState current)
    {
        if (current is null || current.Shard != shard || current.Generation <= 0 || current.LastReservedCounter < 0)
        {
            throw new InvalidOperationException("The admitted allocation state is invalid.");
        }
    }
}
