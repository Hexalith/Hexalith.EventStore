using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>Verifies producer-frozen native evidence against the original complete input before evolution.</summary>
internal static class DomainProjectionNativeSourceAdmission
{
    /// <summary>Admits only the original supplied native cut, leaving unsupported input unproven.</summary>
    internal static async Task<ProjectionRequest> VerifyAsync(IServiceProvider services, ProjectionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { return await VerifyCoreAsync(services, request, cancellationToken).ConfigureAwait(false); }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); throw; }
    }

    private static async Task<ProjectionRequest> VerifyCoreAsync(IServiceProvider services, ProjectionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.NativeSourceProof is null)
        {
            ProjectionRequest legacy = request with { };
            legacy.ClearNativeSourceAdmission();
            return legacy;
        }
        if (request.Events is null || request.Events.Any(static item => item is null || item.Payload is null
            || string.IsNullOrWhiteSpace(item.EventTypeName) || string.IsNullOrWhiteSpace(item.SerializationFormat)))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        AggregateIdentity identity;
        try { identity = new(request.TenantId, request.Domain, request.AggregateId); }
        catch (ArgumentException) { throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome); }
        NativeSourceProof proof = request.NativeSourceProof;
        if (proof.Erased || proof.RetainedFloor != 1 || proof.HeadSequence != request.Events.LongLength
            || !NativeSourceProofCodec.IsValid(proof, identity, request.Events.LongLength, 1))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        // Copy mutable payloads without allocating a complete base64 request, and never inherit
        // an earlier private admission through a record copy or an unavailable verification path.
        ProjectionEventDto[] events = new ProjectionEventDto[request.Events.Length];
        for (int index = 0; index < events.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProjectionEventDto item = request.Events[index];
            events[index] = item with { Payload = Copy(item.Payload, cancellationToken),
                EffectivePayload = item.EffectivePayload is null ? null : Copy(item.EffectivePayload, cancellationToken),
                StoredDigest = item.StoredDigest is null ? null : Copy(item.StoredDigest, cancellationToken) };
        }
        cancellationToken.ThrowIfCancellationRequested();
        ProjectionRequest original = request with { Events = events };
        original.ClearNativeSourceAdmission();
        // Hosts without the supported authenticated transport retain unproven generations. No new
        // registration or owner authority is inferred from a producer hint or a local cursor.
        IEventStoreGatewayClient? gateway = services.GetService<IEventStoreGatewayClient>();
        if (gateway is null) return original;
        var reader = new AuthoritativeEventStreamReader(gateway,
            services.GetService<TimeProvider>() ?? TimeProvider.System, requireNativeProof: true);
        AuthoritativeStreamReadResult read = await reader.ReadAsync(identity, cancellationToken).ConfigureAwait(false);
        // Native read enrollment is independently gated. Its absence must not disable ordinary
        // projections: the delivered generation remains unproven and cannot authorize metadata.
        if (!read.IsAuthoritative || read.Stream is null) return original;
        AuthoritativeEventStream source = read.Stream;
        if (source.NativeProof?.SourceIncarnation != proof.SourceIncarnation || source.Events.Count < original.Events.Length)
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        // A legitimate append does not change the original cut. Recompute that exact native cut
        // from the authenticated complete stream's raw digests instead of substituting its new head.
        NativeSourceProof cut = NativeSourceProofCodec.CreateSeed(identity, proof.SourceIncarnation);
        foreach (StreamReadEvent item in source.Events.Take(original.Events.Length))
            cut = NativeSourceProofCodec.ExtendDigest(cut, identity, item.SequenceNumber, item.MessageId, item.NativeEventDigest!);
        if (cut != proof) throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        if (source.Events.Take(original.Events.Length).Any(item => item.ProtectionMetadata is
            { State: not Hexalith.EventStore.Contracts.Security.PayloadProtectionState.Unprotected })) return original;
        for (int index = 0; index < original.Events.Length; index++)
        {
            ProjectionEventDto item = original.Events[index];
            StreamReadEvent native = source.Events[index];
            if (item.SequenceNumber != index + 1L || item.SequenceNumber != native.SequenceNumber
                || item.MessageId != native.MessageId || item.EventTypeName != native.EventTypeName
                || item.SerializationFormat != native.SerializationFormat || !item.Timestamp.EqualsExact(native.Timestamp)
                || item.CorrelationId != native.CorrelationId || item.UserId != native.UserId
                || item.GlobalPosition != native.GlobalPosition || !item.Payload.AsSpan().SequenceEqual(native.Payload)
                || item.StoredPayloadVersion != native.PayloadVersion
                || item.MetadataVersion is not (null or 1) || native.MetadataVersion != 1)
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        original.AdmitNativeSourceProof(proof);
        return original;
    }

    private static byte[] Copy(byte[] source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] result = new byte[source.Length];
        for (int offset = 0; offset < source.Length; offset += 65_536)
        {
            cancellationToken.ThrowIfCancellationRequested();
            source.AsSpan(offset, Math.Min(65_536, source.Length - offset)).CopyTo(result.AsSpan(offset));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
