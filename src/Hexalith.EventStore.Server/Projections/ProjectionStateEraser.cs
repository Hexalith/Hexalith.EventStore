using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// DAPR-backed implementation of <see cref="IProjectionStateEraser"/>.
/// </summary>
/// <remarks>
/// Cross-store erasure is resumable rather than atomic: read models are processed in request order,
/// followed by the delivery checkpoint. A failed attempt may leave an already-processed prefix absent,
/// but it returns <see langword="false"/>. Reissuing the same request is side-effect safe because absent
/// keys succeed idempotently, allowing the operation to continue to the remaining targets.
/// </remarks>
/// <param name="daprClient">The DAPR client used for same-store transactions.</param>
/// <param name="readModelStore">The read-model store used by the cross-store protocol.</param>
/// <param name="checkpointTracker">The projection delivery checkpoint tracker.</param>
/// <param name="options">Projection configuration containing the checkpoint store name.</param>
public sealed class ProjectionStateEraser(
    DaprClient daprClient,
    IReadModelStore readModelStore,
    IProjectionCheckpointTracker checkpointTracker,
    IOptions<ProjectionOptions> options) : IProjectionStateEraser {
    private static readonly byte[] s_emptyTransactionValue = [];

    /// <inheritdoc/>
    public async Task<bool> TryEraseAsync(
        AggregateIdentity identity,
        IReadOnlyCollection<ReadModelEraseTarget> readModelTargets,
        string checkpointEtag,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(readModelTargets);
        ArgumentNullException.ThrowIfNull(checkpointEtag);
        cancellationToken.ThrowIfCancellationRequested();

        ReadModelEraseTarget[] targets = [.. readModelTargets];
        foreach (ReadModelEraseTarget target in targets) {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(target.TenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(target.StoreName);
            ArgumentException.ThrowIfNullOrWhiteSpace(target.Key);
            ArgumentNullException.ThrowIfNull(target.ETag);
            if (!string.Equals(target.TenantId, identity.TenantId, StringComparison.Ordinal)
                || !target.Key.StartsWith(identity.TenantId + ":", StringComparison.Ordinal)) {
                return false;
            }
        }

        string checkpointStoreName = options.Value.CheckpointStateStoreName;
        if (targets.All(target => string.Equals(target.StoreName, checkpointStoreName, StringComparison.Ordinal))) {
            var operations = new List<StateTransactionRequest>(targets.Length + 1);
            operations.AddRange(targets.Select(CreateDeleteRequest));
            operations.Add(CreateDeleteRequest(ProjectionCheckpointTracker.GetStateKey(identity), checkpointEtag));

            try {
                await daprClient
                    .ExecuteStateTransactionAsync(
                        checkpointStoreName,
                        operations,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Dapr.DaprException) {
                return false;
            }
        }

        try {
            foreach (ReadModelEraseTarget target in targets) {
                if (!await readModelStore
                    .TryEraseAsync(target.StoreName, target.Key, target.ETag, cancellationToken)
                    .ConfigureAwait(false)) {
                    return false;
                }
            }

            return await checkpointTracker
                .TryEraseAsync(identity, checkpointEtag, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception) {
            return false;
        }
    }

    private static StateTransactionRequest CreateDeleteRequest(ReadModelEraseTarget target) =>
        CreateDeleteRequest(target.Key, target.ETag);

    private static StateTransactionRequest CreateDeleteRequest(string key, string etag) =>
        new(
            key,
            s_emptyTransactionValue,
            StateOperationType.Delete,
            etag,
            metadata: null,
            options: new StateOptions { Concurrency = ConcurrencyMode.FirstWrite });
}
