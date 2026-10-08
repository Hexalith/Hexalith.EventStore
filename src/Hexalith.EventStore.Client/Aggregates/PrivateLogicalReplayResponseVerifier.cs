using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Verifies retained response payloads and every logical route/prefix under current trust before retry/progress use.</summary>
internal static class PrivateLogicalReplayResponseVerifier
{
    /// <summary>Checks exact outer framing, scalar/image equality, source range and the operation-ledger predecessor.</summary>
    internal static DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim> Verify(ReadOnlySpan<byte> response, DaprLogicalSourceBinding binding,
        DaprLogicalClaimTrust trust, ReadOnlyMemory<byte> previousAccumulator, EventBufferBudget budget,
        CancellationToken cancellationToken)
    {
        if (response.Length > 64 * 1024 * 1024)
        {
            throw new InvalidOperationException("ReadableLimit: logical response exceeds its ceiling.");
        }

        trust.RequireCurrent(cancellationToken);
        var reader = new EventEvolutionBinaryReader(response);
        if (!reader.ReadRaw("HX-EV-DAPR-REPLAY-PAGE-1\0"u8.Length).SequenceEqual("HX-EV-DAPR-REPLAY-PAGE-1\0"u8)
            || reader.ReadByte() != 1)
        {
            throw new ArgumentException("Logical response model mismatch.");
        }

        using UnverifiedEventEvolutionProofFrame proof = EventEvolutionProofFraming.Capture(reader.ReadBytes(2 * 1024 * 1024),
            budget, true, false, cancellationToken);
        DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim> prefixOwner = trust.VerifyPrefix(proof.PrefixClaim, Encoding.UTF8.GetString(proof.PrefixKey),
            proof.PrefixSignature, budget, cancellationToken);
        try
        {
            DaprLogicalPrefixClaim prefix = prefixOwner.Value;
            byte[] sourceHash = DaprLogicalClaimCodec.ComputeSourceBindingHash(binding, budget);
            if (prefix.TenantId != binding.Identity.TenantId || prefix.Domain != binding.Identity.Domain
                || prefix.AggregateId != binding.Identity.AggregateId || prefix.AggregateType != binding.AggregateType
                || prefix.ActorHead != binding.ActorHead || prefix.TargetSequence != binding.TargetSequence
                || !prefix.SourceBindingHash.Span.SequenceEqual(sourceHash) || reader.ReadUInt32() != prefix.Count || proof.RouteCount != prefix.Count)
            {
                throw new InvalidOperationException("AddressMismatch: pinned prefix disagrees with its fixed source or event count.");
            }

            var entries = new List<DaprLogicalDigestEntry>();
            byte[] accumulator = previousAccumulator.ToArray();
            for (int index = 0; index < proof.RouteCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using DaprLogicalVerifiedClaim<DaprLogicalRouteClaim> routeOwner = trust.VerifyRoute(proof.RouteClaim(index),
                    Encoding.UTF8.GetString(proof.RouteKey(index)), proof.RouteSignature(index), budget, cancellationToken);
                DaprLogicalRouteClaim route = routeOwner.Value;
                long sequence = reader.ReadInt64();
                string type = reader.ReadString(64);
                int version = reader.ReadInt32();
                string format = reader.ReadString(512 * 1024);
                ReadOnlySpan<byte> payload = reader.ReadBytes(64 * 1024 * 1024);
                if (route.SequenceNumber != sequence || sequence != prefix.StartSequence + index
                    || route.TenantId != prefix.TenantId || route.Domain != prefix.Domain || route.AggregateId != prefix.AggregateId
                    || route.AggregateType != prefix.AggregateType || route.TargetCanonicalType != type || route.TargetPayloadVersion != version
                    || route.EffectiveFormat != format || !route.SourceBindingHash.Span.SequenceEqual(sourceHash)
                    || !SHA256.HashData(payload).AsSpan().SequenceEqual(route.EffectivePayloadHash.Span))
                {
                    throw new InvalidOperationException("AddressMismatch: pinned route/image/source disagreement.");
                }

                var entry = new DaprLogicalDigestEntry(sequence, route.ApplicationLogicalDigest);
                entries.Add(entry);
                accumulator = DaprLogicalClaimCodec.ComputeAccumulatorStep(sourceHash, trust.RegistryFingerprint, accumulator, entry);
            }

            reader.RequireEnd();
            if (!DaprLogicalClaimCodec.ComputeOrderedList(sourceHash, entries).AsSpan().SequenceEqual(prefix.OrderedLogicalDigestListHash.Span)
                || !accumulator.AsSpan().SequenceEqual(prefix.Accumulator.Span))
            {
                throw new InvalidOperationException("AddressMismatch: pinned logical ordered list or complete accumulator disagreement.");
            }

            trust.RequireCurrent(cancellationToken);
            return prefixOwner;
        }
        catch
        {
            prefixOwner.Dispose();
            throw;
        }
    }
}
