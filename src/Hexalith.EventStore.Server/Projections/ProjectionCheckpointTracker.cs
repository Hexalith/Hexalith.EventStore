using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// DAPR state-store implementation of <see cref="IProjectionCheckpointTracker"/>.
/// </summary>
public sealed class ProjectionCheckpointTracker(
    DaprClient daprClient,
    IOptions<ProjectionOptions> options,
    ILogger<ProjectionCheckpointTracker> logger) : IProjectionCheckpointTracker {
    private const int MaxEtagRetries = 3;
    private const string StateKeyPrefix = "projection-checkpoints:";

    /// <inheritdoc/>
    public async Task<long> ReadLastDeliveredSequenceAsync(
        AggregateIdentity identity,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);

        ProjectionCheckpoint? checkpoint = await daprClient
            .GetStateAsync<ProjectionCheckpoint>(
                options.Value.CheckpointStateStoreName,
                GetStateKey(identity),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return checkpoint?.LastDeliveredSequence ?? 0;
    }

    /// <inheritdoc/>
    public async Task<bool> SaveDeliveredSequenceAsync(
        AggregateIdentity identity,
        long deliveredSequence,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfNegative(deliveredSequence);

        string key = GetStateKey(identity);
        string stateStoreName = options.Value.CheckpointStateStoreName;
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            try {
                (ProjectionCheckpoint? existing, string etag) = await daprClient
                    .GetStateAndETagAsync<ProjectionCheckpoint>(
                        stateStoreName,
                        key,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                long maxSequence = Math.Max(existing?.LastDeliveredSequence ?? 0, deliveredSequence);
                var checkpoint = new ProjectionCheckpoint(
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    maxSequence,
                    DateTimeOffset.UtcNow);

                bool saved = await daprClient
                    .TrySaveStateAsync(
                        stateStoreName,
                        key,
                        checkpoint,
                        etag,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (saved) {
                    return true;
                }

                logger.LogDebug(
                    "ETag mismatch while updating projection checkpoint '{CheckpointKey}', retry {Attempt}.",
                    key,
                    attempt + 1);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (attempt < MaxEtagRetries - 1) {
                logger.LogDebug(
                    ex,
                    "Retry {Attempt} while updating projection checkpoint '{CheckpointKey}'.",
                    attempt + 1,
                    key);
            }
        }

        return false;
    }

    internal static string GetStateKey(AggregateIdentity identity) {
        ArgumentNullException.ThrowIfNull(identity);
        return StateKeyPrefix + identity.ActorId;
    }
}
