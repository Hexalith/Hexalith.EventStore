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
    private const int IdentityPageSize = 100;
    private const string StateKeyPrefix = "projection-checkpoints:";
    private const string IdentityScopeIndexKey = "projection-identities:scopes";
    private const string IdentityScopePagePrefix = "projection-identities:scopes:";
    private const string IdentityIndexPrefix = "projection-identities:index:";
    private const string IdentityPagePrefix = "projection-identities:page:";

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

        if (checkpoint is null) {
            return 0;
        }

        if (!string.Equals(checkpoint.TenantId, identity.TenantId, StringComparison.Ordinal)
            || !string.Equals(checkpoint.Domain, identity.Domain, StringComparison.Ordinal)
            || !string.Equals(checkpoint.AggregateId, identity.AggregateId, StringComparison.Ordinal)) {
            throw new InvalidOperationException("Projection checkpoint identity does not match the requested aggregate identity.");
        }

        return checkpoint.LastDeliveredSequence;
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

                // Skip the round-trip when the proposed sequence is already covered by the
                // persisted checkpoint. This avoids burning ETag retries (and emitting a
                // misleading CheckpointSaveExhausted warning) when concurrent fan-out triggers
                // collide on a key that does not actually need to advance.
                if (existing is not null && existing.LastDeliveredSequence >= deliveredSequence) {
                    return true;
                }

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

    /// <inheritdoc/>
    public async Task TrackIdentityAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);

        ProjectionIdentityScope scope = new(identity.TenantId, identity.Domain);
        await AddScopeAsync(scope, cancellationToken).ConfigureAwait(false);
        await AddIdentityAsync(scope, new ProjectionIdentity(identity.TenantId, identity.Domain, identity.AggregateId), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AggregateIdentity> EnumerateTrackedIdentitiesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ProjectionIdentityIndex scopeIndex = await ReadStateAsync<ProjectionIdentityIndex>(IdentityScopeIndexKey, cancellationToken)
            .ConfigureAwait(false)
            ?? ProjectionIdentityIndex.Empty;

        for (int scopePageNumber = 0; scopePageNumber < scopeIndex.PageCount; scopePageNumber++) {
            cancellationToken.ThrowIfCancellationRequested();
            ProjectionIdentityScopePage? scopePage = await ReadStateAsync<ProjectionIdentityScopePage>(
                    GetScopePageKey(scopePageNumber),
                    cancellationToken)
                .ConfigureAwait(false);
            if (scopePage is null) {
                continue;
            }

            foreach (ProjectionIdentityScope scope in scopePage.Scopes) {
                ProjectionIdentityIndex identityIndex = await ReadStateAsync<ProjectionIdentityIndex>(
                        GetIdentityIndexKey(scope),
                        cancellationToken)
                    .ConfigureAwait(false)
                    ?? ProjectionIdentityIndex.Empty;

                for (int identityPageNumber = 0; identityPageNumber < identityIndex.PageCount; identityPageNumber++) {
                    cancellationToken.ThrowIfCancellationRequested();
                    ProjectionIdentityPage? identityPage = await ReadStateAsync<ProjectionIdentityPage>(
                            GetIdentityPageKey(scope, identityPageNumber),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (identityPage is null) {
                        continue;
                    }

                    foreach (ProjectionIdentity trackedIdentity in identityPage.Identities) {
                        yield return new AggregateIdentity(trackedIdentity.TenantId, trackedIdentity.Domain, trackedIdentity.AggregateId);
                    }
                }
            }
        }
    }

    internal static string GetStateKey(AggregateIdentity identity) {
        ArgumentNullException.ThrowIfNull(identity);
        return StateKeyPrefix + identity.ActorId;
    }

    private static string GetIdentityIndexKey(ProjectionIdentityScope scope) =>
        IdentityIndexPrefix + scope.TenantId + ":" + scope.Domain;

    private static string GetIdentityPageKey(ProjectionIdentityScope scope, int pageNumber) =>
        IdentityPagePrefix + scope.TenantId + ":" + scope.Domain + ":" + pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string GetScopePageKey(int pageNumber) =>
        IdentityScopePagePrefix + pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private async Task AddScopeAsync(ProjectionIdentityScope scope, CancellationToken cancellationToken) {
        ProjectionIdentityIndex index = await ReadStateAsync<ProjectionIdentityIndex>(IdentityScopeIndexKey, cancellationToken)
            .ConfigureAwait(false)
            ?? ProjectionIdentityIndex.Empty;

        for (int pageNumber = 0; pageNumber < index.PageCount; pageNumber++) {
            ProjectionIdentityScopePage page = await ReadStateAsync<ProjectionIdentityScopePage>(GetScopePageKey(pageNumber), cancellationToken)
                .ConfigureAwait(false)
                ?? ProjectionIdentityScopePage.Empty;
            if (page.Scopes.Any(existing => existing == scope)) {
                return;
            }
        }

        await AppendScopeAsync(index, scope, cancellationToken).ConfigureAwait(false);
    }

    private async Task AddIdentityAsync(ProjectionIdentityScope scope, ProjectionIdentity identity, CancellationToken cancellationToken) {
        string indexKey = GetIdentityIndexKey(scope);
        ProjectionIdentityIndex index = await ReadStateAsync<ProjectionIdentityIndex>(indexKey, cancellationToken)
            .ConfigureAwait(false)
            ?? ProjectionIdentityIndex.Empty;

        for (int pageNumber = 0; pageNumber < index.PageCount; pageNumber++) {
            ProjectionIdentityPage page = await ReadStateAsync<ProjectionIdentityPage>(GetIdentityPageKey(scope, pageNumber), cancellationToken)
                .ConfigureAwait(false)
                ?? ProjectionIdentityPage.Empty;
            if (page.Identities.Any(existing => existing == identity)) {
                return;
            }
        }

        await AppendIdentityAsync(scope, index, identity, cancellationToken).ConfigureAwait(false);
    }

    private async Task AppendScopeAsync(ProjectionIdentityIndex index, ProjectionIdentityScope scope, CancellationToken cancellationToken) {
        int pageNumber = index.PageCount == 0 || index.LastPageCount >= IdentityPageSize ? index.PageCount : index.PageCount - 1;
        string pageKey = GetScopePageKey(pageNumber);
        ProjectionIdentityScopePage page = await ReadStateAsync<ProjectionIdentityScopePage>(pageKey, cancellationToken).ConfigureAwait(false)
            ?? ProjectionIdentityScopePage.Empty;
        var updatedScopes = page.Scopes.Append(scope).ToArray();
        await SaveStateAsync(pageKey, new ProjectionIdentityScopePage(updatedScopes), cancellationToken).ConfigureAwait(false);
        ProjectionIdentityIndex updatedIndex = index.PageCount == 0 || index.LastPageCount >= IdentityPageSize
            ? new ProjectionIdentityIndex(index.PageCount + 1, 1)
            : index with { LastPageCount = updatedScopes.Length };
        await SaveStateAsync(IdentityScopeIndexKey, updatedIndex, cancellationToken).ConfigureAwait(false);
    }

    private async Task AppendIdentityAsync(
        ProjectionIdentityScope scope,
        ProjectionIdentityIndex index,
        ProjectionIdentity identity,
        CancellationToken cancellationToken) {
        int pageNumber = index.PageCount == 0 || index.LastPageCount >= IdentityPageSize ? index.PageCount : index.PageCount - 1;
        string pageKey = GetIdentityPageKey(scope, pageNumber);
        ProjectionIdentityPage page = await ReadStateAsync<ProjectionIdentityPage>(pageKey, cancellationToken).ConfigureAwait(false)
            ?? ProjectionIdentityPage.Empty;
        var updatedIdentities = page.Identities.Append(identity).ToArray();
        await SaveStateAsync(pageKey, new ProjectionIdentityPage(updatedIdentities), cancellationToken).ConfigureAwait(false);
        ProjectionIdentityIndex updatedIndex = index.PageCount == 0 || index.LastPageCount >= IdentityPageSize
            ? new ProjectionIdentityIndex(index.PageCount + 1, 1)
            : index with { LastPageCount = updatedIdentities.Length };
        await SaveStateAsync(GetIdentityIndexKey(scope), updatedIndex, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T?> ReadStateAsync<T>(string key, CancellationToken cancellationToken) =>
        await daprClient
            .GetStateAsync<T>(
                options.Value.CheckpointStateStoreName,
                key,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    private async Task SaveStateAsync<T>(string key, T value, CancellationToken cancellationToken) =>
        await daprClient
            .SaveStateAsync(
                options.Value.CheckpointStateStoreName,
                key,
                value,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    private sealed record ProjectionIdentityIndex(int PageCount, int LastPageCount) {
        public static ProjectionIdentityIndex Empty { get; } = new(0, 0);
    }

    private sealed record ProjectionIdentityScope(string TenantId, string Domain);

    private sealed record ProjectionIdentityScopePage(ProjectionIdentityScope[] Scopes) {
        public static ProjectionIdentityScopePage Empty { get; } = new([]);
    }

    private sealed record ProjectionIdentity(string TenantId, string Domain, string AggregateId);

    private sealed record ProjectionIdentityPage(ProjectionIdentity[] Identities) {
        public static ProjectionIdentityPage Empty { get; } = new([]);
    }
}
