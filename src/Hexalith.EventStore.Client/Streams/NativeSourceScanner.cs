using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Streams native source through a fresh bounded selector, verifying every contiguous digest and the final current head.</summary>
/// <param name="gateway">The independent scan-only authenticated transport.</param>
/// <param name="timeProvider">The monotonic budget and observation clock.</param>
/// <param name="readTimeout">A positive whole-operation bound of at most thirty seconds.</param>
public sealed class NativeSourceScanner(IEventStoreGatewayClient gateway, TimeProvider timeProvider, TimeSpan? readTimeout = null)
    : INativeSourceScanner
{
    private const int PageSize = 255;
    private const int MaximumAttempts = 3;

    /// <inheritdoc/>
    public async Task<NativeSourceScanResult<T>> ScanAsync<T>(AggregateIdentity identity,
        Func<INativeSourceScanFold<T>> createFold, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(createFold);
            long started = timeProvider.GetTimestamp();
            TimeSpan bound = readTimeout ?? TimeSpan.FromSeconds(30);
            if (bound <= TimeSpan.Zero || bound > TimeSpan.FromSeconds(30)) return Refuse<T>("source-invalid-time-bound");
            using var deadline = new AuthoritativeStreamReadDeadline(bound, timeProvider, cancellationToken, started);
            try
            {
                NativeSourceScanResult<T> result = await ScanCoreAsync(identity, createFold, deadline).ConfigureAwait(false);
                deadline.ThrowIfCancellationRequested();
                return result;
            }
            catch (OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Refuse<T>(deadline.IsExpired ? "source-time-bound-exceeded" : "source-unavailable");
            }
            catch (Exception exception) when (exception is EventStoreGatewayException or HttpRequestException
                or InvalidOperationException or ArgumentException or System.Text.Json.JsonException
                or Hexalith.EventStore.Client.Events.EventPayloadEvolutionException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Refuse<T>("source-unavailable");
            }
        }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private async Task<NativeSourceScanResult<T>> ScanCoreAsync<T>(AggregateIdentity identity,
        Func<INativeSourceScanFold<T>> createFold, AuthoritativeStreamReadDeadline deadline)
    {
        for (int attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            StreamReadPage first = await ReadPageAsync(identity, 0, 0, 1, deadline).ConfigureAwait(false);
            NativeSourceProof? initial = first.Metadata.NativeProof;
            long head = first.Metadata.LatestSequence;
            if (initial is null || initial.Erased || initial.RetainedFloor != 1
                || !NativeSourceProofCodec.IsValid(initial, identity, head, 1)) return Refuse<T>("source-native-proof-unavailable");
            var fold = await deadline.ReadWithAdmissionAsync(_ => Task.FromResult(createFold()
                ?? throw new InvalidOperationException("Missing native source selector."))).ConfigureAwait(false);
            NativeSourceProof chain = NativeSourceProofCodec.CreateSeed(identity, initial.SourceIncarnation);
            long cursor = 0;
            bool changed = false;
            while (cursor < head)
            {
                StreamReadPage page = await ReadPageAsync(identity, cursor, head, PageSize, deadline).ConfigureAwait(false);
                if (page.Metadata.NativeProof != initial || page.Metadata.LatestSequence != head)
                {
                    changed = true;
                    break;
                }
                if (page.Events.Count == 0) return Refuse<T>("source-gap");
                bool applied = await deadline.ReadWithAdmissionAsync(_ =>
                {
                    foreach (StreamReadEvent item in page.Events)
                    {
                        deadline.ThrowIfCancellationRequested();
                        if (item.SequenceNumber != checked(cursor + 1)) return Task.FromResult(false);
                        chain = NativeSourceProofCodec.ExtendDigest(chain, identity, item.SequenceNumber,
                            item.MessageId, item.NativeEventDigest!);
                        cursor = item.SequenceNumber;
                        if (!fold.TryApply(item)) return Task.FromResult(false);
                    }
                    return Task.FromResult(true);
                }).ConfigureAwait(false);
                if (!applied) return Refuse<T>("source-gap-or-unsupported");
                if (!page.Metadata.IsTruncated && cursor != head) return Refuse<T>("source-incomplete");
            }
            StreamReadPage final = await ReadPageAsync(identity, 0, 0, 1, deadline).ConfigureAwait(false);
            if (changed || final.Metadata.LatestSequence != head || final.Metadata.NativeProof != initial) continue;
            if (chain != initial) return Refuse<T>("source-prefix-mismatch");
            T value = await deadline.ReadWithAdmissionAsync(_ => Task.FromResult(fold.Complete())).ConfigureAwait(false);
            deadline.ThrowIfCancellationRequested();
            return new(value, new(initial, timeProvider.GetUtcNow()), null);
        }
        return Refuse<T>("source-head-changed");
    }

    private async Task<StreamReadPage> ReadPageAsync(AggregateIdentity identity, long from, long to, int size,
        AuthoritativeStreamReadDeadline deadline)
    {
        var request = new StreamReadRequest(identity.TenantId, identity.Domain, identity.AggregateId,
            from, to, PageSize: size) { RequireNativeProof = true };
        StreamReadPage supplied = await deadline.ReadWithAdmissionAsync(token => gateway.ReadNativeSourceScanPageAsync(request, token)).ConfigureAwait(false);
        return await deadline.ReadWithAdmissionAsync(_ =>
        {
            StreamReadPage page = NativeSourceScanPageCapture.Capture(supplied, size, deadline);
            StreamReadPageValidator.ValidateAndGetNextSequence(request, page);
            if (to == 0 && (page.Events.Count != 0 || page.Metadata.IsTruncated))
                throw new InvalidOperationException("Invalid current native observation.");
            return Task.FromResult(page);
        }).ConfigureAwait(false);
    }

    private static NativeSourceScanResult<T> Refuse<T>(string reason) => new(default, null, reason);
}
