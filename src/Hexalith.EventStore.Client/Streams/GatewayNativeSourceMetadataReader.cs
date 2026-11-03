using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Reads content-free native source evidence through the existing authenticated workload stream transport.</summary>
/// <remarks>The gateway must use its enrolled workload transport. Resource authorization and projection provenance
/// remain separate requirements; cold actor activation can still run existing recovery.</remarks>
/// <param name="gateway">The existing EventStore gateway client.</param>
/// <param name="clock">The observation clock and monotonic interactive deadline source.</param>
/// <param name="readTimeout">A positive whole-observation deadline of at most thirty seconds.</param>
public sealed class GatewayNativeSourceMetadataReader(IEventStoreGatewayClient gateway, TimeProvider clock, TimeSpan readTimeout)
    : INativeSourceMetadataReader
{
    /// <summary>Preserves the original two-second observation deadline for existing consumers.</summary>
    public GatewayNativeSourceMetadataReader(IEventStoreGatewayClient gateway, TimeProvider clock)
        : this(gateway, clock, TimeSpan.FromSeconds(2)) { }

    /// <inheritdoc/>
    public async Task<NativeSourceObservation?> ReadAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        if (readTimeout <= TimeSpan.Zero || readTimeout > TimeSpan.FromSeconds(30)) return null;
        using var deadline = new AuthoritativeStreamReadDeadline(readTimeout, clock,
            cancellationToken, clock.GetTimestamp());
        try
        {
            var request = new StreamReadRequest(identity.TenantId, identity.Domain, identity.AggregateId,
                FromSequence: 0, ToSequence: 0, PageSize: 1) { RequireNativeProof = true };
            return await deadline.ReadWithAdmissionAsync(async token =>
            {
                StreamReadPage page = await gateway.ReadStreamAsync(request, token).ConfigureAwait(false);
                if (page is not { Events.Count: 0, Metadata: { FromSequence: 0, ToSequence: 0,
                    LastSequenceReturned: null, EventCount: 0, IsTruncated: false, NextContinuationToken: null,
                    NativeProof: { Erased: false } proof } metadata }
                || page.Tenant != identity.TenantId || page.Domain != identity.Domain || page.AggregateId != identity.AggregateId
                || !NativeSourceProofCodec.IsValid(proof, identity, metadata.LatestSequence, proof.RetainedFloor)
                || proof.HeadMessageId is { } message && (message.Length > 256 || message != message.Trim() || message.Any(char.IsControl)))
                    return null;

                return new NativeSourceObservation(proof, clock.GetUtcNow());
            }).ConfigureAwait(false);
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }
}
