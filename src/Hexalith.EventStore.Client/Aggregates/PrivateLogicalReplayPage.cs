using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Admits an entire privately captured logical response before any state or event callback.</summary>
/// <remarks>Logical digests never enter historical StoredDigest carriers.</remarks>
internal sealed class PrivateLogicalReplayPage : IDisposable
{
    private readonly List<PrivateLogicalReplayEvent> _events = [];
    private readonly DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim> _prefix;
    private readonly EventBufferReservation _metadata;

    private PrivateLogicalReplayPage(DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim> prefix, EventBufferReservation metadata)
    {
        _prefix = prefix;
        _metadata = metadata;
    }

    /// <summary>Gets the complete verified page range.</summary>
    internal DaprLogicalPrefixClaim Prefix => _prefix.Value;

    /// <summary>Gets privately owned effective payloads in source sequence order.</summary>
    internal IReadOnlyList<PrivateLogicalReplayEvent> Events => _events.AsReadOnly();

    /// <summary>Copies, verifies and admits all routes before returning a callable page.</summary>
    internal static PrivateLogicalReplayPage Capture(ReadOnlySpan<byte> response, DaprLogicalSourceBinding source,
        DaprLogicalClaimTrust trust, ReadOnlyMemory<byte> predecessor, EventEvolutionService evolution,
        EventBufferBudget budget, CancellationToken token, DaprLogicalAnchoredIntake? anchored = null)
    {
        token.ThrowIfCancellationRequested();
        if (response.Length > 64 * 1024 * 1024)
        {
            throw new InvalidOperationException("ReadableLimit: logical page exceeds 64 MiB.");
        }

        if (!ReferenceEquals(trust.CapabilityLoss, evolution.CapabilityLoss)
            || !trust.RegistryFingerprint.Span.SequenceEqual(Convert.FromHexString(evolution.RegistryFingerprint)))
        {
            throw new InvalidOperationException("CapabilityMismatch: replay intake and evolution differ.");
        }

        using EventBufferReservation captureCharge = budget.Reserve(checked(response.Length * 2 + 256));
        byte[] image = response.ToArray();
        PrivateLogicalReplayPage? page = null;
        DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim>? prefix = null;
        EventBufferReservation? metadata = null;
        try
        {
            prefix = PrivateLogicalReplayResponseVerifier.Verify(image, source, trust, predecessor, budget, token, anchored);
            // Strings can expand to UTF-16, and event/owner/list capacity remains charged until page disposal.
            metadata = budget.Reserve(checked(image.Length * 2 + 4096));
            page = new PrivateLogicalReplayPage(prefix, metadata);
            prefix = null;
            metadata = null;
            var reader = new EventEvolutionBinaryReader(image);
            _ = reader.ReadRaw(anchored is null ? "HX-EV-DAPR-REPLAY-PAGE-1\0"u8.Length : "HX-EV-DAPR-ANCHORED-PAGE-1\0"u8.Length);
            _ = reader.ReadByte();
            _ = reader.ReadBytes(2 * 1024 * 1024);
            int count = checked((int)reader.ReadUInt32());
            for (int index = 0; index < count; index++)
            {
                token.ThrowIfCancellationRequested();
                long sequence = reader.ReadInt64();
                string type = reader.ReadString(64);
                int version = reader.ReadInt32();
                string format = reader.ReadString(512 * 1024);
                ReadOnlySpan<byte> payload = reader.ReadBytes(64 * 1024 * 1024);
                evolution.RequireCurrentRoute(source.Identity.Domain, source.AggregateType, type, version, format, token);
                EventBufferReservation charge = budget.Reserve(checked(payload.Length + 256));
                ImmutablePayload? owner = null;
                try
                {
                    owner = new ImmutablePayload(payload.ToArray(), payload.Length, token, charge);
                    page._events.Add(new PrivateLogicalReplayEvent(sequence, type, version, format, owner));
                    owner = null;
                }
                catch
                {
                    if (owner is null)
                    {
                        charge.Dispose();
                    }
                    else
                    {
                        owner.Dispose();
                    }
                    throw;
                }
            }

            reader.RequireEnd();
            trust.RequireCurrent(token);
            anchored?.Trust.RequireCurrent(trust, token);
            evolution.RequireActive(token);
            return page;
        }
        catch
        {
            page?.Dispose();
            prefix?.Dispose();
            metadata?.Dispose();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(image);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (PrivateLogicalReplayEvent value in _events)
        {
            value.Dispose();
        }

        _events.Clear();
        _prefix.Dispose();
        _metadata.Dispose();
    }
}
