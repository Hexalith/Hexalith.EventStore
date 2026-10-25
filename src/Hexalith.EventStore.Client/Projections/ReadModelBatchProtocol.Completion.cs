using System.Globalization;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

internal sealed partial class ReadModelBatchProtocol
{
    /// <summary>Reads a physically compacted value only, refusing every old/new envelope before marker access.</summary>
    internal static async Task<RawStateEntry> ReadCompactedAsync(IReadModelBatchStateAccessor accessor,
        string key, CancellationToken cancellationToken, TimeProvider? timeProvider = null)
    {
        RawStateEntry entry = await accessor.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (entry.Exists && ReadModelBatchEnvelope.IsEnvelope(entry.Value.Span))
            throw new InvalidOperationException("The read-model generation is not physically complete.");
        return ReadModelBatchExpiringValue.Resolve(entry, timeProvider ?? TimeProvider.System);
    }

    private void ValidateCompletionProofBudget()
    {
        if (_ownerSelections is null || !_ownerSelections.Any(static owner =>
            owner.ReceiptProjectionName == ReadModelRebuildCompletion.ReceiptProjectionName)) return;
        // The existing UTC round-trip receipt format has a fixed encoded width, including its offset.
        // Budget its complete private representation before any state access; never reserve a different
        // generation, fingerprint or terminal time. Actual publication still copies the durable receipt.
        var receipt = new ReadModelBatchMarker
        {
            ScopeHash = _scopeHash, BatchId = _batch.Scope.BatchId, Fingerprint = _fingerprint,
            Status = ReadModelBatchMarkerStatus.Completed, OwnerSelections = _ownerSelections,
            TerminalTimeUtc = DateTimeOffset.MaxValue.ToString("O", CultureInfo.InvariantCulture),
        };
        if (JsonSerializer.SerializeToUtf8Bytes(ReadModelBatchCompletionProof.From(receipt), JsonSerializerOptions.Web).Length
            > ReadModelBatchMarker.MaxControlBytes)
            throw new ArgumentException("Guarded read-model completion evidence exceeds its 16384-byte limit.");
    }

    private async Task<bool> PublishCompletionProofAsync(CancellationToken cancellationToken)
    {
        // Enrollment is immutable manifest material. Old/direct rebuilds are never certified on retry.
        if (_ownerSelections is null || !_ownerSelections.Any(static owner =>
            owner.ReceiptProjectionName == ReadModelRebuildCompletion.ReceiptProjectionName)) return true;
        (ReadModelBatchMarker? receipt, _, _) = await ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        if (_batch.DiscoverableRecovery && receipt?.Status == ReadModelBatchMarkerStatus.Aborted) return true;
        if (receipt is not { Status: ReadModelBatchMarkerStatus.Completed, Operations: null,
                OriginalManifest: null, OriginalManifestKey: null, RecoveryAnchor: null }
            || receipt.ScopeHash != _scopeHash || receipt.BatchId != _batch.Scope.BatchId
            || receipt.Fingerprint != _fingerprint || receipt.OwnerSelection is not null
            || receipt.OwnerSelections is null || !receipt.OwnerSelections.SequenceEqual(_ownerSelections)
            || !ReadModelBatchMarker.IsUtcTerminalTime(receipt.TerminalTimeUtc)) return false;
        var proof = ReadModelBatchCompletionProof.From(receipt);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(proof, JsonSerializerOptions.Web);
        if (bytes.Length > ReadModelBatchMarker.MaxControlBytes) return false;
        string key = ReadModelBatchKeys.CompletionKey(_scopeHash);
        RawStateEntry prior = await _accessor.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (prior.Exists) return prior.Value.Span.SequenceEqual(bytes);
        // Publication follows the full physical end-state check for both store profiles. In particular,
        // a transaction receipt from a mis-qualified partial transaction cannot grant this proof.
        if (!await VerifyOperationKeysAsync(cancellationToken).ConfigureAwait(false)) return false;
        if (_injector is not null) await _injector.InjectAsync(ReadModelBatchPhase.BeforeCompletionProof, -1, cancellationToken).ConfigureAwait(false);
        _ = await _accessor.TryWriteAsync(key, bytes, string.Empty, cancellationToken).ConfigureAwait(false);
        RawStateEntry stored = await _accessor.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (_injector is not null) await _injector.InjectAsync(ReadModelBatchPhase.AfterCompletionProof, -1, cancellationToken).ConfigureAwait(false);
        return stored.Exists && stored.Value.Span.SequenceEqual(bytes);
    }
}
