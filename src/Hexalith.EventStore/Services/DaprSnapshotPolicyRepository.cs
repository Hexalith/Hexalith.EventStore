using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Services;

/// <summary>
/// DAPR-backed snapshot policy writer and runtime resolver.
/// </summary>
public sealed partial class DaprSnapshotPolicyRepository(
    DaprClient daprClient,
    IOptions<CommandStatusOptions> commandStatusOptions,
    ILogger<DaprSnapshotPolicyRepository> logger) : ISnapshotPolicyResolver {
    public const int MaxSnapshotPolicyIntervalEvents = 100000;

    private const int MaxEtagRetries = 5;
    private const string PolicyIndexPrefix = "admin:storage-snapshot-policies:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly string _stateStoreName = commandStatusOptions.Value.StateStoreName;

    /// <summary>
    /// Sets or updates an exact active snapshot policy.
    /// </summary>
    public async Task<AdminOperationResult> SetPolicyAsync(SnapshotPolicySetRequest? request, CancellationToken ct = default) {
        if (!TryValidateSetRequest(request, out PolicyIdentity identity, out AdminOperationResult validationResult)) {
            return validationResult;
        }

        string operationId = CreateOperationId("snapshot-policy-set", identity);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        try {
            List<SnapshotPolicy> allBefore = await ReadPoliciesAsync(ScopeKey("all"), ct).ConfigureAwait(false);
            List<SnapshotPolicy> tenantBefore = await ReadPoliciesAsync(ScopeKey(identity.TenantId), ct).ConfigureAwait(false);
            SnapshotPolicy? existing = FindExistingPolicy(tenantBefore, identity)
                ?? FindExistingPolicy(allBefore, identity);
            DateTimeOffset createdAtUtc = existing?.CreatedAtUtc ?? now;
            var policy = new SnapshotPolicy(
                identity.TenantId,
                identity.Domain,
                identity.AggregateType,
                request!.IntervalEvents,
                createdAtUtc);

            bool allSaved = await MutateIndexAsync(
                ScopeKey("all"),
                TenantPolicies(tenantBefore, identity.TenantId),
                identity,
                policies => UpsertPolicy(policies, identity, policy),
                ct).ConfigureAwait(false);
            bool tenantSaved = await MutateIndexAsync(
                ScopeKey(identity.TenantId),
                TenantPolicies(allBefore, identity.TenantId),
                identity,
                policies => UpsertPolicy(policies, identity, policy),
                ct).ConfigureAwait(false);

            if (allSaved && tenantSaved) {
                Invalidate(identity.TenantId, identity.Domain, identity.AggregTypeKey);
                return new AdminOperationResult(true, operationId, "Snapshot policy saved.", null);
            }

            LogPolicyIndexFailure(operationId, identity);
            return UpstreamUnavailable(operationId);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogWarning(
                ex,
                "Snapshot policy set failed safely: TenantId={TenantId}, Domain={Domain}, AggregateType={AggregateType}, OperationId={OperationId}",
                identity.TenantId,
                identity.Domain,
                identity.AggregTypeKey,
                operationId);
            return UpstreamUnavailable(operationId);
        }
    }

    /// <summary>
    /// Deletes an exact active snapshot policy.
    /// </summary>
    public async Task<AdminOperationResult> DeletePolicyAsync(SnapshotPolicyDeleteRequest? request, CancellationToken ct = default) {
        if (!TryValidateDeleteRequest(request, out PolicyIdentity identity, out AdminOperationResult validationResult)) {
            return validationResult;
        }

        string operationId = CreateOperationId("snapshot-policy-delete", identity);

        try {
            List<SnapshotPolicy> allBefore = await ReadPoliciesAsync(ScopeKey("all"), ct).ConfigureAwait(false);
            List<SnapshotPolicy> tenantBefore = await ReadPoliciesAsync(ScopeKey(identity.TenantId), ct).ConfigureAwait(false);
            bool existedInAll = ContainsPolicy(allBefore, identity);
            bool existedInTenant = ContainsPolicy(tenantBefore, identity);
            if (!existedInAll && !existedInTenant) {
                return new AdminOperationResult(false, operationId, "Snapshot policy was not found.", "NotFound");
            }

            bool allSaved = await MutateIndexAsync(
                ScopeKey("all"),
                TenantPolicies(tenantBefore, identity.TenantId),
                identity,
                policies => RemovePolicy(policies, identity),
                ct).ConfigureAwait(false);
            bool tenantSaved = await MutateIndexAsync(
                ScopeKey(identity.TenantId),
                TenantPolicies(allBefore, identity.TenantId),
                identity,
                policies => RemovePolicy(policies, identity),
                ct).ConfigureAwait(false);

            if (allSaved && tenantSaved) {
                Invalidate(identity.TenantId, identity.Domain, identity.AggregTypeKey);
                return new AdminOperationResult(true, operationId, "Snapshot policy deleted.", null);
            }

            LogPolicyIndexFailure(operationId, identity);
            return UpstreamUnavailable(operationId);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogWarning(
                ex,
                "Snapshot policy delete failed safely: TenantId={TenantId}, Domain={Domain}, AggregateType={AggregateType}, OperationId={OperationId}",
                identity.TenantId,
                identity.Domain,
                identity.AggregTypeKey,
                operationId);
            return UpstreamUnavailable(operationId);
        }
    }

    /// <inheritdoc/>
    public async Task<int?> GetIntervalAsync(
        string tenantId,
        string domain,
        string aggregateType,
        CancellationToken cancellationToken = default) {
        if (!TryNormalize(tenantId, domain, aggregateType, out PolicyIdentity identity)) {
            return null;
        }

        string cacheKey = identity.CacheKey;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(cacheKey, out CacheEntry cached) && cached.ExpiresAtUtc > now) {
            return cached.IntervalEvents;
        }

        try {
            List<SnapshotPolicy>? policies = await daprClient
                .GetStateAsync<List<SnapshotPolicy>>(_stateStoreName, ScopeKey(identity.TenantId), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            int? interval = policies?
                .FirstOrDefault(policy => Matches(policy, identity))
                ?.IntervalEvents;
            _cache[cacheKey] = new CacheEntry(interval, now.Add(CacheTtl));
            return interval;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogWarning(
                ex,
                "Snapshot policy runtime lookup failed; static snapshot options will be used: TenantId={TenantId}, Domain={Domain}, AggregateType={AggregateType}",
                identity.TenantId,
                identity.Domain,
                identity.AggregTypeKey);
            return null;
        }
    }

    /// <inheritdoc/>
    public void Invalidate(string tenantId, string domain, string aggregateType) {
        if (TryNormalize(tenantId, domain, aggregateType, out PolicyIdentity identity)) {
            _ = _cache.TryRemove(identity.CacheKey, out _);
        }
    }

    /// <summary>
    /// Validates a snapshot policy set request without reading or mutating state.
    /// </summary>
    public static AdminOperationResult? Validate(SnapshotPolicySetRequest? request)
        => TryValidateSetRequest(request, out _, out AdminOperationResult validationResult)
            ? null
            : validationResult;

    /// <summary>
    /// Validates a snapshot policy delete request without reading or mutating state.
    /// </summary>
    public static AdminOperationResult? Validate(SnapshotPolicyDeleteRequest? request)
        => TryValidateDeleteRequest(request, out _, out AdminOperationResult validationResult)
            ? null
            : validationResult;

    private async Task<List<SnapshotPolicy>> ReadPoliciesAsync(string key, CancellationToken ct) {
        List<SnapshotPolicy>? policies = await daprClient
            .GetStateAsync<List<SnapshotPolicy>>(_stateStoreName, key, cancellationToken: ct)
            .ConfigureAwait(false);
        return policies ?? [];
    }

    private async Task<bool> MutateIndexAsync(
        string key,
        IEnumerable<SnapshotPolicy> repairPolicies,
        PolicyIdentity identity,
        Func<IReadOnlyList<SnapshotPolicy>, List<SnapshotPolicy>> mutate,
        CancellationToken ct) {
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            (List<SnapshotPolicy>? existing, string etag) = await daprClient
                .GetStateAndETagAsync<List<SnapshotPolicy>>(_stateStoreName, key, cancellationToken: ct)
                .ConfigureAwait(false);

            List<SnapshotPolicy> normalized = NormalizeAndMerge(existing ?? [], repairPolicies);
            List<SnapshotPolicy> updated = Sort(mutate(normalized));
            bool saved = await daprClient
                .TrySaveStateAsync(_stateStoreName, key, updated, etag, cancellationToken: ct)
                .ConfigureAwait(false);
            if (saved) {
                return true;
            }
        }

        return false;
    }

    private static List<SnapshotPolicy> UpsertPolicy(
        IReadOnlyList<SnapshotPolicy> policies,
        PolicyIdentity identity,
        SnapshotPolicy policy)
        => [.. policies.Where(p => !Matches(p, identity)), policy];

    private static List<SnapshotPolicy> RemovePolicy(
        IReadOnlyList<SnapshotPolicy> policies,
        PolicyIdentity identity)
        => [.. policies.Where(p => !Matches(p, identity))];

    private static List<SnapshotPolicy> NormalizeAndMerge(
        IReadOnlyList<SnapshotPolicy> policies,
        IEnumerable<SnapshotPolicy> repairPolicies) {
        var result = new Dictionary<string, SnapshotPolicy>(StringComparer.Ordinal);
        foreach (SnapshotPolicy policy in policies.Concat(repairPolicies)) {
            if (!TryNormalize(policy.TenantId, policy.Domain, policy.AggregateType, out PolicyIdentity identity)) {
                continue;
            }

            SnapshotPolicy normalized = new(
                identity.TenantId,
                identity.Domain,
                policy.AggregateType.Trim(),
                policy.IntervalEvents,
                policy.CreatedAtUtc);
            result[identity.CacheKey] = normalized;
        }

        return [.. result.Values];
    }

    private static List<SnapshotPolicy> Sort(IEnumerable<SnapshotPolicy> policies)
        => [.. policies
            .OrderBy(p => p.TenantId, StringComparer.Ordinal)
            .ThenBy(p => p.Domain, StringComparer.Ordinal)
            .ThenBy(p => p.AggregateType, StringComparer.OrdinalIgnoreCase)];

    private static bool Matches(SnapshotPolicy policy, PolicyIdentity identity)
        => string.Equals(policy.TenantId, identity.TenantId, StringComparison.Ordinal)
        && string.Equals(policy.Domain, identity.Domain, StringComparison.Ordinal)
        && string.Equals(AggregateTypeKey(policy.AggregateType), identity.AggregTypeKey, StringComparison.Ordinal);

    private static SnapshotPolicy? FindExistingPolicy(IEnumerable<SnapshotPolicy> policies, PolicyIdentity identity)
        => policies.FirstOrDefault(policy => Matches(policy, identity));

    private static bool ContainsPolicy(IEnumerable<SnapshotPolicy> policies, PolicyIdentity identity)
        => policies.Any(policy => Matches(policy, identity));

    private static IEnumerable<SnapshotPolicy> TenantPolicies(IEnumerable<SnapshotPolicy> policies, string tenantId)
        => policies.Where(policy => string.Equals(policy.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));

    private static bool TryValidateSetRequest(
        SnapshotPolicySetRequest? request,
        out PolicyIdentity identity,
        out AdminOperationResult validationResult) {
        if (request is null) {
            identity = default;
            validationResult = RejectedValidation("snapshot-policy-set-request-invalid", "Snapshot policy request body is required.");
            return false;
        }

        if (!TryNormalize(request, out identity, out validationResult)) {
            return false;
        }

        if (request.IntervalEvents is < SnapshotOptions.MinimumInterval or > MaxSnapshotPolicyIntervalEvents) {
            validationResult = RejectedValidation(
                CreateOperationId("snapshot-policy-set", identity),
                $"Snapshot policy interval must be between {SnapshotOptions.MinimumInterval} and {MaxSnapshotPolicyIntervalEvents} events.");
            return false;
        }

        return true;
    }

    private static bool TryValidateDeleteRequest(
        SnapshotPolicyDeleteRequest? request,
        out PolicyIdentity identity,
        out AdminOperationResult validationResult) {
        if (request is null) {
            identity = default;
            validationResult = RejectedValidation("snapshot-policy-delete-request-invalid", "Snapshot policy request body is required.");
            return false;
        }

        return TryNormalize(request, out identity, out validationResult);
    }

    private static bool TryNormalize(
        SnapshotPolicySetRequest request,
        out PolicyIdentity identity,
        out AdminOperationResult validationResult)
        => TryNormalize(request.TenantId, request.Domain, request.AggregateType, out identity, out validationResult, "snapshot-policy-set");

    private static bool TryNormalize(
        SnapshotPolicyDeleteRequest request,
        out PolicyIdentity identity,
        out AdminOperationResult validationResult)
        => TryNormalize(request.TenantId, request.Domain, request.AggregateType, out identity, out validationResult, "snapshot-policy-delete");

    private static bool TryNormalize(
        string? tenantId,
        string? domain,
        string? aggregateType,
        out PolicyIdentity identity) {
        bool valid = TryNormalize(tenantId, domain, aggregateType, out identity, out _, "snapshot-policy");
        return valid;
    }

    private static bool TryNormalize(
        string? tenantId,
        string? domain,
        string? aggregateType,
        out PolicyIdentity identity,
        out AdminOperationResult validationResult,
        string operationPrefix) {
        identity = default;
        string candidateTenant = tenantId ?? string.Empty;
        string candidateDomain = domain ?? string.Empty;
        string candidateAggregateType = aggregateType?.Trim() ?? string.Empty;
        string operationId = $"{operationPrefix}-{Hash($"{candidateTenant}|{candidateDomain}|{candidateAggregateType}")}";

        if (string.IsNullOrWhiteSpace(candidateTenant)
            || string.IsNullOrWhiteSpace(candidateDomain)
            || string.IsNullOrWhiteSpace(candidateAggregateType)) {
            validationResult = RejectedValidation(operationId, "Tenant, domain, and aggregate type are required.");
            return false;
        }

        if (!AggregateTypeRegex().IsMatch(candidateAggregateType)) {
            validationResult = RejectedValidation(operationId, "Invalid tenant, domain, or aggregate type.");
            return false;
        }

        try {
            var structuralIdentity = new AggregateIdentity(candidateTenant, candidateDomain, candidateAggregateType);
            identity = new PolicyIdentity(
                structuralIdentity.TenantId,
                structuralIdentity.Domain,
                candidateAggregateType,
                AggregateTypeKey(candidateAggregateType));
            validationResult = new AdminOperationResult(true, operationId, null, null);
            return true;
        }
        catch (ArgumentException) {
            validationResult = RejectedValidation(operationId, "Invalid tenant, domain, or aggregate type.");
            return false;
        }
    }

    private static AdminOperationResult RejectedValidation(string operationId, string message)
        => new(false, operationId, message, "RejectedValidation");

    private static AdminOperationResult UpstreamUnavailable(string operationId)
        => new(false, operationId, "Snapshot policy state could not be updated. Retry shortly.", "UpstreamUnavailable");

    private static string CreateOperationId(string prefix, PolicyIdentity identity)
        => $"{prefix}-{Hash($"{identity.TenantId}|{identity.Domain}|{identity.AggregTypeKey}")}";

    private static string ScopeKey(string scope) => $"{PolicyIndexPrefix}{scope}";

    private static string AggregateTypeKey(string aggregateType) {
        string trimmed = aggregateType.Trim();
        int index = trimmed.LastIndexOf('.');
        string simpleName = index >= 0 ? trimmed[(index + 1)..] : trimmed;
        return simpleName.ToLowerInvariant();
    }

    private static string Hash(string value) {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private void LogPolicyIndexFailure(string operationId, PolicyIdentity identity)
        => logger.LogWarning(
            "Snapshot policy index update did not converge after bounded retries: TenantId={TenantId}, Domain={Domain}, AggregateType={AggregateType}, OperationId={OperationId}",
            identity.TenantId,
            identity.Domain,
            identity.AggregTypeKey,
            operationId);

    [GeneratedRegex(@"^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$")]
    private static partial Regex AggregateTypeRegex();

    private readonly record struct PolicyIdentity(
        string TenantId,
        string Domain,
        string AggregateType,
        string AggregTypeKey) {
        public string CacheKey => $"{TenantId}|{Domain}|{AggregTypeKey}";
    }

    private readonly record struct CacheEntry(int? IntervalEvents, DateTimeOffset ExpiresAtUtc);
}
