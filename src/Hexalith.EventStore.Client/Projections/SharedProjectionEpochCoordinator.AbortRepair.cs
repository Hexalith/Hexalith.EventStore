using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Client.Projections;

public sealed partial class SharedProjectionEpochCoordinator
{
    /// <summary>Reads the validated immutable repair binding for process-restart recovery.</summary>
    public async Task<SharedProjectionAbortRepairReceipt?> GetAbortRepairAsync(SharedProjectionScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        SharedProjectionEpochState state = await RequireStateAsync(scope, cancellationToken).ConfigureAwait(false);
        ValidateBoundary(state, scope);
        return state.AbortRepair;
    }

    /// <summary>Restores certified original absence while retaining the abort fence and immutable evidence.</summary>
    public async Task<SharedProjectionAbortRepairReceipt> RepairAbortMetadataAsync(
        SharedProjectionScope scope, SharedProjectionAbortRepairRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        ArgumentNullException.ThrowIfNull(request);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(request);
        request = JsonSerializer.Deserialize<SharedProjectionAbortRepairRequest>(bytes)!;
        if (!IsValidAbortRepairRequest(request))
            throw new ArgumentException("The bounded certified metadata repair request is invalid.", nameof(request));
        string fingerprint = Convert.ToHexString(SHA256.HashData(bytes));
        var receipt = new SharedProjectionAbortRepairReceipt(fingerprint, request);
        SharedProjectionEpochState reservedState;
        for (int attempt = 0; ; attempt++)
        {
            if (attempt >= MaxRetries) throw RetryExhausted();
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("The abort epoch is unavailable.");
            ValidateState(state);
            ValidateBoundary(state, scope);
            ThrowIfOffboarded(state);
            RequireOperation(state, request.OperationId, SharedProjectionEpochPhase.Aborting);
            if (state.Epoch != request.Epoch || state.ActiveGeneration != request.ActiveGeneration
                || state.InventoryFingerprint != request.InventoryFingerprint || state.ActiveGeneration >= state.Epoch)
                throw new InvalidOperationException("The certified abort repair binding is unavailable.");
            if (state.AbortRepair is { } reserved)
            {
                if (reserved.Fingerprint != fingerprint)
                    throw new InvalidOperationException("Another immutable repair owns this abort epoch.");
                // Once repair completed, normal journal drain may have prepared work or changed v6 rows.
                // Only the exact durable identity authorizes this resume; pre-repair guards no longer apply.
                if (state.AbortRepairCompleted) return reserved;
                reservedState = state;
                await VerifyRepairSourceAsync(scope, request, cancellationToken).ConfigureAwait(false);
                break;
            }
            if (state.Journal.Any(item => item.Prepared is not null || item.PreparedChunks is not null))
                throw new InvalidOperationException("Prepared journal work prevents a new repair reservation.");
            await VerifyRepairSourceAsync(scope, request, cancellationToken).ConfigureAwait(false);
            foreach (SharedProjectionAbortRepairValue value in request.Values)
            {
                ReadModelEntry<JsonNode> current = await ReadGenerationJsonAsync(scope,
                    state.ActiveGeneration, value.LogicalKey, cancellationToken).ConfigureAwait(false);
                if (!MatchesRepairObservation(current, value.ExpectedETag, value.OriginalJson,
                    value.SourceGeneration, value.PointerETag, scope.CopyOnWrite))
                    throw new InvalidOperationException("The original repair evidence changed before reservation.");
            }
            reservedState = state with { AbortRepair = receipt };
            if (await SaveAsync(scope, reservedState, entry.ETag, cancellationToken).ConfigureAwait(false)) break;
        }
        string evidenceKey = scope.StateKey + ":abort-repair:" + request.Epoch;
        ReadModelEntry<SharedProjectionAbortRepairReceipt> evidence = await _store
            .GetAsync<SharedProjectionAbortRepairReceipt>(scope.StoreName, evidenceKey, cancellationToken).ConfigureAwait(false);
        if (evidence.Value is null && !await _store.TrySaveAsync(scope.StoreName, evidenceKey,
            receipt, string.Empty, cancellationToken).ConfigureAwait(false))
            evidence = await _store.GetAsync<SharedProjectionAbortRepairReceipt>(scope.StoreName, evidenceKey, cancellationToken).ConfigureAwait(false);
        if (evidence.Value is { } retained && (retained.Fingerprint != fingerprint
            || !IsValidAbortRepairReceipt(retained)))
            throw new InvalidOperationException("The retained repair certificate conflicts.");
        for (int ordinal = 0; ordinal < request.Values.Length; ordinal++)
        {
            await VerifyRepairSourceAsync(scope, request, cancellationToken).ConfigureAwait(false);
            SharedProjectionAbortRepairValue value = request.Values[ordinal];
            if (value.OriginalJson is null) continue;
            var mutation = SharedProjectionMutation.FromOperation(ReadModelBatchOperation.Delete(
                value.LogicalKey, ReadModelBatchConcurrency.IdempotentAbsent));
            ReadModelBatch logical = await BuildCopyOnWriteBatchAsync(scope, request.ActiveGeneration,
                "abort-repair:" + request.Epoch + ":" + fingerprint + ":" + ordinal, [mutation],
                request.ActiveGeneration, reservedState.CopyOnWriteAnchorGeneration, cancellationToken).ConfigureAwait(false);
            var operations = logical.Operations.Select(operation => operation.Key == scope.PhysicalKey(request.ActiveGeneration, value.LogicalKey)
                && (!scope.CopyOnWrite || value.SourceGeneration == request.ActiveGeneration)
                    ? operation.WithConcurrency(ReadModelBatchConcurrency.Match(value.ExpectedETag))
                    : operation.Key == scope.CopyOnWritePointerKey(value.LogicalKey)
                        ? operation.WithConcurrency(value.PointerETag is null ? ReadModelBatchConcurrency.CreateOnly
                            : ReadModelBatchConcurrency.Match(value.PointerETag))
                        : operation).ToList();
            if (scope.CopyOnWrite && value.SourceGeneration is { } source && source != request.ActiveGeneration)
            {
                operations.Add(ReadModelBatchOperation.WriteCanonical(scope.PhysicalKey(source, value.LogicalKey),
                    typeof(JsonNode).FullName!, Encoding.UTF8.GetBytes(value.OriginalJson), null, null)
                    .WithConcurrency(ReadModelBatchConcurrency.Match(value.ExpectedETag)));
            }
            ReadModelBatchResult result = await _batches.ExecuteAsync(new ReadModelBatch(logical.Scope, operations), cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess) throw new InvalidOperationException("Certified metadata repair refused: " + result.Status);
        }
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            ReadModelEntry<SharedProjectionEpochState> entry = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
            SharedProjectionEpochState state = entry.Value ?? throw new InvalidOperationException("The repair reservation disappeared.");
            ValidateState(state);
            RequireOperation(state, request.OperationId, SharedProjectionEpochPhase.Aborting);
            if (state.AbortRepair?.Fingerprint != fingerprint)
                throw new InvalidOperationException("The certified repair reservation changed.");
            await VerifyRepairSourceAsync(scope, request, cancellationToken).ConfigureAwait(false);
            foreach (SharedProjectionAbortRepairValue value in request.Values)
                if (!string.IsNullOrEmpty((await ReadGenerationJsonAsync(scope,
                    request.ActiveGeneration, value.LogicalKey, cancellationToken).ConfigureAwait(false)).ETag))
                    throw new InvalidOperationException("Certified absence has not been restored.");
            if (await SaveAsync(scope, state with { AbortRepairCompleted = true }, entry.ETag, cancellationToken).ConfigureAwait(false)) return receipt;
        }
        throw RetryExhausted();
    }

    private async Task VerifyRepairSourceAsync(SharedProjectionScope scope,
        SharedProjectionAbortRepairRequest request, CancellationToken cancellationToken)
    {
        foreach (SharedProjectionAbortRepairGuard guard in request.SourceGuards)
        {
            ReadModelEntry<JsonNode> current = await ReadGenerationJsonAsync(scope,
                request.ActiveGeneration, guard.LogicalKey, cancellationToken).ConfigureAwait(false);
            string? digest = string.IsNullOrEmpty(current.ETag) ? null
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(current.Value?.ToJsonString() ?? "null")));
            if ((current.ETag ?? string.Empty) != guard.ExpectedETag || digest != guard.ContentDigest
                || scope.CopyOnWrite && (current.SourceGeneration != guard.SourceGeneration || current.PointerETag != guard.PointerETag))
                throw new InvalidOperationException("The certified complete source changed during abort repair.");
        }
    }

    private static bool MatchesRepairObservation(ReadModelEntry<JsonNode> current, string etag,
        string? json, long? generation, string? pointerETag, bool copyOnWrite)
        => (current.ETag ?? string.Empty) == etag
            && (etag.Length == 0 ? json is null : json is not null
                && JsonNode.DeepEquals(current.Value, JsonNode.Parse(json)))
            && (!copyOnWrite || current.SourceGeneration == generation && current.PointerETag == pointerETag);

    private static bool IsValidAbortRepairRequest(SharedProjectionAbortRepairRequest? request)
    {
        static bool Token(string? value) => !string.IsNullOrWhiteSpace(value) && Encoding.UTF8.GetByteCount(value) <= 512;
        static bool ETag(string? value) => value is not null && Encoding.UTF8.GetByteCount(value) <= 512;
        if (request is null || request.Epoch <= 0 || request.ActiveGeneration < 0 || request.ActiveGeneration >= request.Epoch
            || !Token(request.OperationId) || !Token(request.InventoryFingerprint) || !Token(request.CertificateId)
            || request.Values is not { Length: > 0 and <= 8 } || request.SourceGuards is null || request.SourceGuards.Length > 4096
            || request.Values.Any(value => value is null || !Token(value.LogicalKey) || !ETag(value.ExpectedETag)
                || (value.ExpectedETag.Length == 0) != (value.OriginalJson is null)
                || value.SourceGeneration < 0 || value.SourceGeneration > request.ActiveGeneration
                || value.PointerETag is not null && (!ETag(value.PointerETag) || value.PointerETag.Length == 0))
            || request.SourceGuards.Any(guard => guard is null || !Token(guard.LogicalKey) || !ETag(guard.ExpectedETag)
                || (guard.ExpectedETag.Length == 0) != (guard.ContentDigest is null)
                || guard.ContentDigest is not null && (guard.ContentDigest.Length != 64 || !guard.ContentDigest.All(Uri.IsHexDigit))
                || guard.SourceGeneration < 0 || guard.SourceGeneration > request.ActiveGeneration
                || guard.PointerETag is not null && (!ETag(guard.PointerETag) || guard.PointerETag.Length == 0))
            || request.Values.Select(value => value.LogicalKey).Distinct(StringComparer.Ordinal).Count() != request.Values.Length
            || request.SourceGuards.Select(value => value.LogicalKey).Distinct(StringComparer.Ordinal).Count() != request.SourceGuards.Length
            || JsonSerializer.SerializeToUtf8Bytes(request).Length > 256 * 1024) return false;
        try
        {
            foreach (SharedProjectionAbortRepairValue value in request.Values)
                if (value.OriginalJson is not null) _ = JsonNode.Parse(value.OriginalJson);
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static bool IsValidAbortRepairReceipt(SharedProjectionAbortRepairReceipt receipt)
        => IsValidAbortRepairRequest(receipt.Request) && receipt.Fingerprint == Convert.ToHexString(
            SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(receipt.Request)));

    private static void ValidateAbortRepairState(SharedProjectionEpochState state)
    {
        if (state.AbortRepair is null)
        {
            if (state.AbortRepairCompleted) throw new InvalidOperationException("Completed repair evidence is missing.");
            return;
        }
        SharedProjectionAbortRepairReceipt receipt = state.AbortRepair;
        if (!IsValidAbortRepairReceipt(receipt) || receipt.Request.Epoch != state.Epoch
            || receipt.Request.ActiveGeneration != state.ActiveGeneration || receipt.Request.OperationId != state.OperationId
            || receipt.Request.InventoryFingerprint != state.InventoryFingerprint
            || state.CopyOnWriteAnchorGeneration is not null && (receipt.Request.Values.Any(value => value.SourceGeneration is null)
                || receipt.Request.SourceGuards.Any(guard => guard.SourceGeneration is null))
            || state.Phase != SharedProjectionEpochPhase.Aborting && !(state.AbortRepairCompleted && state.Phase == SharedProjectionEpochPhase.Open))
            throw new InvalidOperationException("The durable abort repair evidence is invalid.");
    }

    private static void ThrowIfAbortRepairReserved(SharedProjectionEpochState state)
    {
        if (state.AbortRepair is not null && !state.AbortRepairCompleted)
            throw new InvalidOperationException("Certified abort metadata repair is reserved; resume the identical repair first.");
    }
}
