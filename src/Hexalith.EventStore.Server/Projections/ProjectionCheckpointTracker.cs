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

                if (existing is not null
                    && (!string.Equals(existing.TenantId, identity.TenantId, StringComparison.Ordinal)
                        || !string.Equals(existing.Domain, identity.Domain, StringComparison.Ordinal)
                        || !string.Equals(existing.AggregateId, identity.AggregateId, StringComparison.Ordinal))) {
                    throw new InvalidOperationException("Projection checkpoint identity does not match the requested aggregate identity.");
                }

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
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.AggregateId);

        // R2P6 — reject characters that participate in the projection-identity key scheme so a malformed
        // identity cannot collide across logical scopes or inject a key prefix. The check covers all three
        // identity components (tenant, domain, aggregate id) because AggregateId flows into _activeIdentities
        // via AggregateIdentity.ActorId. ':' is the state-key separator; '\0' is rejected because some state
        // stores treat it as a record terminator; '|' and newline characters are reserved by sibling
        // discovery/key parsers. AggregateIdentity's regex already lowercases tenant+domain and rejects most
        // of these, so this guard is defense-in-depth against bypass paths (reflection, deserialization
        // without validation, with-clones).
        AssertNoReservedChars(identity.TenantId, nameof(identity.TenantId));
        AssertNoReservedChars(identity.Domain, nameof(identity.Domain));
        AssertNoReservedChars(identity.AggregateId, nameof(identity.AggregateId));

        ProjectionIdentityScope scope = new(identity.TenantId, identity.Domain);
        if (!await TryAddScopeAsync(scope, cancellationToken).ConfigureAwait(false)) {
            logger.LogWarning(
                "Projection identity scope registration exhausted ETag retries for tenant {TenantId}, domain {Domain}.",
                identity.TenantId,
                identity.Domain);
            return;
        }

        if (!await TryAddIdentityAsync(scope, new ProjectionIdentity(identity.TenantId, identity.Domain, identity.AggregateId), cancellationToken).ConfigureAwait(false)) {
            logger.LogWarning(
                "Projection identity registration exhausted ETag retries for tenant {TenantId}, domain {Domain}, aggregate {AggregateId}.",
                identity.TenantId,
                identity.Domain,
                identity.AggregateId);
        }
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

            // Defensive null coalesce: a single corrupted/legacy persisted record with a null
            // array (e.g. from a partial restore or schema drift) must not raise NRE and tear
            // down the entire poll tick.
            foreach (ProjectionIdentityScope scope in scopePage.Scopes ?? []) {
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

                    foreach (ProjectionIdentity trackedIdentity in identityPage.Identities ?? []) {
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

    private static void AssertNoReservedChars(string value, string parameterName) {
        // Reserved characters across the projection-identity key scheme: ':' separator, '\0' terminator,
        // '|' (used by ProjectionDiscoveryHostedService.ExtractDomain), and CR/LF which break log lines.
        if (value.AsSpan().IndexOfAny(s_reservedChars) >= 0) {
            throw new ArgumentException(
                $"{parameterName} must not contain ':', '\\0', '|', '\\r', or '\\n' — reserved by the projection-identity key scheme.",
                parameterName);
        }
    }

    private static readonly System.Buffers.SearchValues<char> s_reservedChars = System.Buffers.SearchValues.Create(":\0|\r\n");

    private static string GetIdentityIndexKey(ProjectionIdentityScope scope) =>
        IdentityIndexPrefix + scope.TenantId + ":" + scope.Domain;

    private static string GetIdentityPageKey(ProjectionIdentityScope scope, int pageNumber) =>
        IdentityPagePrefix + scope.TenantId + ":" + scope.Domain + ":" + pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string GetScopePageKey(int pageNumber) =>
        IdentityScopePagePrefix + pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // Page+index updates use a single bounded ETag retry loop on the index "anchor" plus
    // ETag-guarded page writes. Concurrent same-process or cross-replica writers cannot lose
    // data: the loser sees ETag mismatch, re-reads, and re-applies the append. After the
    // bounded retry budget is exhausted, the caller logs and returns rather than performing a
    // blind non-ETag save that could regress the index.
    private async Task<bool> TryAddScopeAsync(ProjectionIdentityScope scope, CancellationToken cancellationToken) {
        string stateStoreName = options.Value.CheckpointStateStoreName;
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            try {
                (ProjectionIdentityIndex? indexOrNull, string indexEtag) = await daprClient
                    .GetStateAndETagAsync<ProjectionIdentityIndex>(stateStoreName, IdentityScopeIndexKey, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                ProjectionIdentityIndex index = indexOrNull ?? ProjectionIdentityIndex.Empty;

                bool alreadyTracked = false;
                for (int pageNumber = 0; pageNumber < index.PageCount; pageNumber++) {
                    ProjectionIdentityScopePage existingPage = await ReadStateAsync<ProjectionIdentityScopePage>(GetScopePageKey(pageNumber), cancellationToken)
                        .ConfigureAwait(false)
                        ?? ProjectionIdentityScopePage.Empty;
                    if ((existingPage.Scopes ?? []).Any(existing => existing == scope)) {
                        alreadyTracked = true;
                        break;
                    }
                }

                if (alreadyTracked) {
                    return true;
                }

                int targetPageNumber = index.PageCount == 0 || index.LastPageCount >= IdentityPageSize
                    ? index.PageCount
                    : index.PageCount - 1;
                string pageKey = GetScopePageKey(targetPageNumber);
                (ProjectionIdentityScopePage? pageOrNull, string pageEtag) = await daprClient
                    .GetStateAndETagAsync<ProjectionIdentityScopePage>(stateStoreName, pageKey, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                ProjectionIdentityScopePage page = pageOrNull ?? ProjectionIdentityScopePage.Empty;
                ProjectionIdentityScope[] updatedScopes = (page.Scopes ?? []).Append(scope).ToArray();

                bool pageSaved = await daprClient
                    .TrySaveStateAsync(stateStoreName, pageKey, new ProjectionIdentityScopePage(updatedScopes), pageEtag, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (!pageSaved) {
                    continue;
                }

                ProjectionIdentityIndex updatedIndex = index.PageCount == 0 || index.LastPageCount >= IdentityPageSize
                    ? new ProjectionIdentityIndex(index.PageCount + 1, 1)
                    : index with { LastPageCount = updatedScopes.Length };

                bool indexSaved = await daprClient
                    .TrySaveStateAsync(stateStoreName, IdentityScopeIndexKey, updatedIndex, indexEtag, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (indexSaved) {
                    return true;
                }
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (attempt < MaxEtagRetries - 1) {
                logger.LogDebug(
                    ex,
                    "Retry {Attempt} while registering projection scope '{TenantId}:{Domain}'.",
                    attempt + 1,
                    scope.TenantId,
                    scope.Domain);
            }
        }

        return false;
    }

    private async Task<bool> TryAddIdentityAsync(ProjectionIdentityScope scope, ProjectionIdentity identity, CancellationToken cancellationToken) {
        string stateStoreName = options.Value.CheckpointStateStoreName;
        string indexKey = GetIdentityIndexKey(scope);
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            try {
                (ProjectionIdentityIndex? indexOrNull, string indexEtag) = await daprClient
                    .GetStateAndETagAsync<ProjectionIdentityIndex>(stateStoreName, indexKey, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                ProjectionIdentityIndex index = indexOrNull ?? ProjectionIdentityIndex.Empty;

                bool alreadyTracked = false;
                for (int pageNumber = 0; pageNumber < index.PageCount; pageNumber++) {
                    ProjectionIdentityPage existingPage = await ReadStateAsync<ProjectionIdentityPage>(GetIdentityPageKey(scope, pageNumber), cancellationToken)
                        .ConfigureAwait(false)
                        ?? ProjectionIdentityPage.Empty;
                    if ((existingPage.Identities ?? []).Any(existing => existing == identity)) {
                        alreadyTracked = true;
                        break;
                    }
                }

                if (alreadyTracked) {
                    return true;
                }

                int targetPageNumber = index.PageCount == 0 || index.LastPageCount >= IdentityPageSize
                    ? index.PageCount
                    : index.PageCount - 1;
                string pageKey = GetIdentityPageKey(scope, targetPageNumber);
                (ProjectionIdentityPage? pageOrNull, string pageEtag) = await daprClient
                    .GetStateAndETagAsync<ProjectionIdentityPage>(stateStoreName, pageKey, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                ProjectionIdentityPage page = pageOrNull ?? ProjectionIdentityPage.Empty;
                ProjectionIdentity[] updatedIdentities = (page.Identities ?? []).Append(identity).ToArray();

                bool pageSaved = await daprClient
                    .TrySaveStateAsync(stateStoreName, pageKey, new ProjectionIdentityPage(updatedIdentities), pageEtag, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (!pageSaved) {
                    continue;
                }

                ProjectionIdentityIndex updatedIndex = index.PageCount == 0 || index.LastPageCount >= IdentityPageSize
                    ? new ProjectionIdentityIndex(index.PageCount + 1, 1)
                    : index with { LastPageCount = updatedIdentities.Length };

                bool indexSaved = await daprClient
                    .TrySaveStateAsync(stateStoreName, indexKey, updatedIndex, indexEtag, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (indexSaved) {
                    return true;
                }
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (attempt < MaxEtagRetries - 1) {
                logger.LogDebug(
                    ex,
                    "Retry {Attempt} while registering projection identity '{TenantId}:{Domain}:{AggregateId}'.",
                    attempt + 1,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId);
            }
        }

        return false;
    }

    private async Task<T?> ReadStateAsync<T>(string key, CancellationToken cancellationToken) =>
        await daprClient
            .GetStateAsync<T>(
                options.Value.CheckpointStateStoreName,
                key,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    // Persisted-schema records — internal (not private) so test assemblies with
    // InternalsVisibleTo can construct/inspect them when probing storage interactions.
    // Not part of the public API surface; serialization shape is owned here.
    internal sealed record ProjectionIdentityIndex(int PageCount, int LastPageCount) {
        public static ProjectionIdentityIndex Empty { get; } = new(0, 0);
    }

    internal sealed record ProjectionIdentityScope(string TenantId, string Domain);

    internal sealed record ProjectionIdentityScopePage(ProjectionIdentityScope[] Scopes) {
        public static ProjectionIdentityScopePage Empty { get; } = new([]);
    }

    internal sealed record ProjectionIdentity(string TenantId, string Domain, string AggregateId);

    internal sealed record ProjectionIdentityPage(ProjectionIdentity[] Identities) {
        public static ProjectionIdentityPage Empty { get; } = new([]);
    }
}
