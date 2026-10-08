using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Encodes/decodes strict new logical-model claims independently of historical provider schemas.</summary>
internal static class DaprLogicalClaimCodec
{
    /// <summary>Gets the existing route/prefix claim ceiling.</summary>
    internal const int MaximumClaimBytes = 1024 * 1024;
    private const int MaximumTextBytes = 512 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Hashes the exact selected fixed source record; host/source admission remains separate.</summary>
    internal static byte[] ComputeSourceBindingHash(DaprLogicalSourceBinding source, EventBufferBudget? budget = null)
    {
        using EventBufferReservation charge = (budget ?? new EventBufferBudget()).Reserve(128 * 1024);
        byte[] bytes = EncodeSource(source);
        try { return SHA256.HashData(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <summary>Encodes the selected source binding using actual immutable identity mappings.</summary>
    internal static byte[] EncodeSource(DaprLogicalSourceBinding source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.Identity);
        if (source.ActorHead < 0 || source.TargetSequence < 0 || source.TargetSequence > source.ActorHead
            || source.RetainedFloor != 1) { throw new ArgumentException("A logical replay requires a complete fixed event-only prefix."); }
        if (!source.MetadataPresent && (source.ActorHead != 0 || source.TargetSequence != 0 || source.MetadataETag is not null
            || source.MetadataLastModified.UtcTicks != DateTimeOffset.UnixEpoch.UtcTicks || source.MetadataLastModified.Offset != TimeSpan.Zero)) { throw new ArgumentException("Absent logical source metadata requires the exact empty-stream form."); }
        foreach (string value in new[] { source.ApplicationId, source.Namespace, source.ActorType, source.AggregateType }) { ArgumentException.ThrowIfNullOrWhiteSpace(value); }
        RequireHash(source.SourceConfigurationHash);
        using var writer = new EventEvolutionBinaryWriter(64 * 1024);
        Header(writer, "HX-EV-DAPR-SOURCE-1\0"u8, 19);
        byte tag = 1;
        foreach (string value in new[] { source.ApplicationId, source.Namespace, source.ActorType, source.Identity.ActorId,
            source.Identity.TenantId, source.Identity.Domain, source.Identity.AggregateId, source.AggregateType }) { Text(writer, tag++, value); }
        Number(writer, tag++, source.ActorHead); Number(writer, tag++, source.TargetSequence); Number(writer, tag++, source.RetainedFloor);
        Text(writer, tag++, source.Identity.EventStreamKeyPrefix); Text(writer, tag++, source.Identity.MetadataKey);
        Text(writer, tag++, DaprLogicalSourceBinding.KeyMappingCodec); Hash(writer, tag++, source.SourceConfigurationHash);
        Text(writer, tag++, DaprLogicalSourceBinding.DigestCodec); writer.WriteByte(tag++); OptionalText(writer, source.MetadataETag);
        writer.WriteByte(tag++); writer.WriteTimestamp(source.MetadataLastModified);
        Int(writer, tag, source.MetadataPresent ? 1 : 0);
        return writer.CopyEncodedBytes();
    }

    /// <summary>Hashes every consumed metadata field, with exact logical optional and sorted extension presence.</summary>
    internal static byte[] ComputeConsumedMetadataHash(DaprLogicalConsumedMetadata metadata,
        ReadOnlyMemory<byte> applicationLogicalDigest, ReadOnlyMemory<byte> sourceBindingHash, EventBufferBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(metadata); RequireHash(applicationLogicalDigest); RequireHash(sourceBindingHash);
        using EventBufferReservation working = (budget ?? new EventBufferBudget()).Reserve(2 * 1024 * 1024);
        using var writer = new EventEvolutionBinaryWriter(512 * 1024);
        writer.WriteRaw("HX-EV-DAPR-CONSUMED-1\0"u8); writer.WriteByte(1);
        foreach (string text in new[] { metadata.MessageId, metadata.AggregateId, metadata.AggregateType, metadata.TenantId, metadata.Domain }) { writer.WriteString(text); }
        writer.WriteInt64(metadata.SequenceNumber); writer.WriteInt64(metadata.GlobalPosition); writer.WriteTimestamp(metadata.Timestamp);
        writer.WriteString(metadata.CorrelationId); OptionalText(writer, metadata.CausationId); OptionalText(writer, metadata.UserId);
        OptionalText(writer, metadata.DomainServiceVersion); writer.WriteString(metadata.EventTypeName); writer.WriteInt32(metadata.MetadataVersion);
        writer.WriteString(metadata.SerializationFormat); OptionalText(writer, metadata.EventContractType); OptionalInt(writer, metadata.PayloadVersion);
        OptionalText(writer, metadata.StoredApplicationDigest);
        writer.WriteByte(metadata.Extensions is null ? (byte)0 : (byte)1);
        if (metadata.Extensions is not null)
        {
            // Do not trust Count or allocate from it. Admit each pair before retaining its slot.
            var pairs = new List<KeyValuePair<string, string>>();
            long admitted = writer.Length + 4L + 4 + StrictUtf8.GetByteCount(metadata.ApplicationFormat) + 64;
            foreach (KeyValuePair<string, string> pair in metadata.Extensions)
            {
                admitted = checked(admitted + 8L + StrictUtf8.GetByteCount(pair.Key) + StrictUtf8.GetByteCount(pair.Value));
                if (pairs.Count == 65536 || admitted > 512 * 1024) { throw new InvalidOperationException("MetadataLimit: consumed extensions exceed their encoded ceiling."); }
                pairs.Add(pair);
            }
            pairs.Sort(static (left, right) => CompareUtf8(left.Key, right.Key));
            writer.WriteUInt32(checked((uint)pairs.Count));
            for (int index = 0; index < pairs.Count; index++)
            {
                if (index > 0 && string.Equals(pairs[index - 1].Key, pairs[index].Key, StringComparison.Ordinal)) { throw new ArgumentException("Duplicate exact extension key."); }
                writer.WriteString(pairs[index].Key); writer.WriteString(pairs[index].Value);
            }
        }
        writer.WriteString(metadata.ApplicationFormat); writer.WriteHash(applicationLogicalDigest.Span); writer.WriteHash(sourceBindingHash.Span);
        return writer.ComputeSha256();
    }

    /// <summary>Encodes exactly 20 logical route fields after scalar validation.</summary>
    internal static byte[] EncodeRoute(DaprLogicalRouteClaim claim)
    {
        ValidateRoute(claim);
        using var writer = new EventEvolutionBinaryWriter(MaximumClaimBytes);
        Header(writer, "HX-EV-DAPR-ROUTE-1\0"u8, 20);
        Hash(writer, 1, claim.ApplicationLogicalDigest); Text(writer, 2, claim.TenantId); Text(writer, 3, claim.Domain);
        Text(writer, 4, claim.AggregateId); Text(writer, 5, claim.AggregateType); Number(writer, 6, claim.SequenceNumber);
        Text(writer, 7, claim.MessageId); Text(writer, 8, claim.StoredEventType); Int(writer, 9, claim.StoredMetadataVersion);
        writer.WriteByte(10); OptionalText(writer, claim.StoredCanonicalType); writer.WriteByte(11); OptionalInt(writer, claim.StoredPayloadVersion);
        Text(writer, 12, claim.StoredFormat); Text(writer, 13, claim.TargetCanonicalType); Int(writer, 14, claim.TargetPayloadVersion);
        Hash(writer, 15, claim.RegistryFingerprint); Hash(writer, 16, claim.EffectivePayloadHash); Text(writer, 17, claim.EffectiveFormat);
        Hash(writer, 18, claim.ConsumedMetadataHash); Hash(writer, 19, claim.SourceBindingHash); Text(writer, 20, claim.LogicalEvidenceModelId);
        return writer.CopyEncodedBytes();
    }

    /// <summary>Rejects wrong separators, counts, tags, options, trailing bytes, model and scalar disagreements.</summary>
    internal static DaprLogicalRouteClaim DecodeRoute(ReadOnlySpan<byte> bytes)
    {
        var reader = Start(bytes, "HX-EV-DAPR-ROUTE-1\0"u8, 20);
        byte[] logical = ReadHash(ref reader, 1); string tenant = ReadText(ref reader, 2); string domain = ReadText(ref reader, 3);
        string aggregate = ReadText(ref reader, 4); string aggregateType = ReadText(ref reader, 5); long sequence = ReadNumber(ref reader, 6);
        string message = ReadText(ref reader, 7); string storedType = ReadText(ref reader, 8); int metadataVersion = ReadInt(ref reader, 9);
        Tag(ref reader, 10); string? canonical = ReadOptionalText(ref reader); Tag(ref reader, 11); int? version = ReadOptionalInt(ref reader);
        string format = ReadText(ref reader, 12); string target = ReadText(ref reader, 13); int targetVersion = ReadInt(ref reader, 14);
        byte[] registry = ReadHash(ref reader, 15); byte[] effectiveHash = ReadHash(ref reader, 16); string effectiveFormat = ReadText(ref reader, 17);
        byte[] consumed = ReadHash(ref reader, 18); byte[] sourceHash = ReadHash(ref reader, 19); string model = ReadText(ref reader, 20);
        reader.RequireEnd();
        var result = new DaprLogicalRouteClaim(logical, tenant, domain, aggregate, aggregateType, sequence, message,
            storedType, metadataVersion, canonical, version, format, target, targetVersion, registry, effectiveHash,
            effectiveFormat, consumed, sourceHash, model);
        ValidateRoute(result); return result;
    }

    /// <summary>Encodes the selected event-only prefix with three explicitly absent anchor fields.</summary>
    internal static byte[] EncodePrefix(DaprLogicalPrefixClaim claim)
    {
        ValidatePrefix(claim);
        using var writer = new EventEvolutionBinaryWriter(MaximumClaimBytes);
        Header(writer, "HX-EV-DAPR-PREFIX-1\0"u8, 17);
        Text(writer, 1, claim.TenantId); Text(writer, 2, claim.Domain); Text(writer, 3, claim.AggregateId); Text(writer, 4, claim.AggregateType);
        Number(writer, 5, claim.StartSequence); Number(writer, 6, claim.EndSequence); Number(writer, 7, claim.ActorHead);
        Number(writer, 8, claim.TargetSequence); Int(writer, 9, claim.Count); Hash(writer, 10, claim.OrderedLogicalDigestListHash);
        Hash(writer, 11, claim.Accumulator); Hash(writer, 12, claim.RegistryFingerprint);
        for (byte tag = 13; tag <= 15; tag++) { writer.WriteByte(tag); writer.WriteByte(0); }
        Hash(writer, 16, claim.SourceBindingHash); Text(writer, 17, claim.LogicalEvidenceModelId);
        return writer.CopyEncodedBytes();
    }

    /// <summary>Decodes only complete-prefix logical forms; no caller anchor can authorize a tail.</summary>
    internal static DaprLogicalPrefixClaim DecodePrefix(ReadOnlySpan<byte> bytes)
    {
        var reader = Start(bytes, "HX-EV-DAPR-PREFIX-1\0"u8, 17);
        string tenant = ReadText(ref reader, 1); string domain = ReadText(ref reader, 2); string aggregate = ReadText(ref reader, 3);
        string type = ReadText(ref reader, 4); long start = ReadNumber(ref reader, 5); long end = ReadNumber(ref reader, 6);
        long head = ReadNumber(ref reader, 7); long target = ReadNumber(ref reader, 8); int count = ReadInt(ref reader, 9);
        byte[] list = ReadHash(ref reader, 10); byte[] accumulator = ReadHash(ref reader, 11); byte[] registry = ReadHash(ref reader, 12);
        for (byte tag = 13; tag <= 15; tag++) { Tag(ref reader, tag); if (reader.ReadByte() != 0) { throw new ArgumentException("Logical anchors are unavailable."); } }
        byte[] source = ReadHash(ref reader, 16); string model = ReadText(ref reader, 17); reader.RequireEnd();
        var result = new DaprLogicalPrefixClaim(tenant, domain, aggregate, type, start, end, head, target, count,
            list, accumulator, registry, source, model); ValidatePrefix(result); return result;
    }

    /// <summary>Hashes one ordered contiguous page's admitted logical digests.</summary>
    internal static byte[] ComputeOrderedList(ReadOnlyMemory<byte> sourceHash, IReadOnlyList<DaprLogicalDigestEntry> entries)
    {
        RequireHash(sourceHash); ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count > 256) { throw new ArgumentException("Logical pages admit at most 256 digests."); }
        using var writer = new EventEvolutionBinaryWriter(32 * 1024);
        writer.WriteRaw("HX-EV-DAPR-PREFIX-LIST-1\0"u8); writer.WriteByte(1); writer.WriteHash(sourceHash.Span);
        writer.WriteString(DaprLogicalSourceBinding.ModelId); writer.WriteUInt32(checked((uint)entries.Count));
        long prior = 0;
        foreach (DaprLogicalDigestEntry entry in entries)
        {
            if (entry.SequenceNumber < 1 || (prior != 0 && (prior == long.MaxValue || entry.SequenceNumber != prior + 1))) { throw new ArgumentException("Logical digest entries must be positive and contiguous."); }
            RequireHash(entry.ApplicationLogicalDigest); writer.WriteInt64(entry.SequenceNumber); writer.WriteHash(entry.ApplicationLogicalDigest.Span);
            prior = entry.SequenceNumber;
        }
        return writer.ComputeSha256();
    }

    /// <summary>Creates the range/registry-bound genesis; it supplies no prior-ledger authority.</summary>
    internal static byte[] ComputeGenesis(ReadOnlyMemory<byte> sourceHash, ReadOnlyMemory<byte> registry)
    {
        RequireHash(sourceHash); RequireHash(registry); using var writer = new EventEvolutionBinaryWriter(256);
        writer.WriteRaw("HX-EV-DAPR-ACC-GENESIS-1\0"u8); writer.WriteByte(1); writer.WriteHash(sourceHash.Span);
        writer.WriteString(DaprLogicalSourceBinding.ModelId); writer.WriteHash(registry.Span); return writer.ComputeSha256();
    }

    /// <summary>Computes a step for an already admitted operation-ledger predecessor, never admitting a caller hash.</summary>
    internal static byte[] ComputeAccumulatorStep(ReadOnlyMemory<byte> sourceHash, ReadOnlyMemory<byte> registry,
        ReadOnlyMemory<byte> previous, DaprLogicalDigestEntry entry)
    {
        RequireHash(sourceHash); RequireHash(registry); RequireHash(previous); RequireHash(entry.ApplicationLogicalDigest);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entry.SequenceNumber);
        using var writer = new EventEvolutionBinaryWriter(256);
        writer.WriteRaw("HX-EV-DAPR-ACC-STEP-1\0"u8); writer.WriteByte(1); writer.WriteHash(sourceHash.Span);
        writer.WriteString(DaprLogicalSourceBinding.ModelId); writer.WriteHash(registry.Span); writer.WriteHash(previous.Span);
        writer.WriteInt64(entry.SequenceNumber); writer.WriteHash(entry.ApplicationLogicalDigest.Span); return writer.ComputeSha256();
    }

    private static void ValidateRoute(DaprLogicalRouteClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim); ValidateScope(claim.TenantId, claim.Domain, claim.AggregateId, claim.AggregateType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(claim.SequenceNumber);
        foreach (string value in new[] { claim.MessageId, claim.StoredEventType, claim.StoredFormat, claim.EffectiveFormat }) { ArgumentException.ThrowIfNullOrWhiteSpace(value); }
        if (claim.StoredMetadataVersion == 1)
        {
            if (claim.StoredCanonicalType is not null || claim.StoredPayloadVersion is not null) { throw new ArgumentException("V1 metadata cannot carry a versioned identity tuple."); }
        }
        else if (claim.StoredMetadataVersion == 2 && claim.StoredCanonicalType is not null && claim.StoredPayloadVersion.HasValue)
        {
            DaprLogicalClaimRules.RequireCanonicalIdentity(claim.StoredCanonicalType);
            DaprLogicalClaimRules.RequirePayloadVersion(claim.StoredPayloadVersion.Value);
            if (claim.StoredEventType != claim.StoredCanonicalType) { throw new ArgumentException("V2 stored event type must exactly match its canonical identity."); }
        }
        else { throw new ArgumentException("Invalid logical stored metadata identity tuple."); }
        DaprLogicalClaimRules.RequireCanonicalIdentity(claim.TargetCanonicalType);
        DaprLogicalClaimRules.RequirePayloadVersion(claim.TargetPayloadVersion);
        RequireModel(claim.LogicalEvidenceModelId);
        foreach (ReadOnlyMemory<byte> hash in new[] { claim.ApplicationLogicalDigest, claim.RegistryFingerprint,
            claim.EffectivePayloadHash, claim.ConsumedMetadataHash, claim.SourceBindingHash }) { RequireHash(hash); }
    }

    private static void ValidatePrefix(DaprLogicalPrefixClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim); ValidateScope(claim.TenantId, claim.Domain, claim.AggregateId, claim.AggregateType);
        if (claim.ActorHead < 0 || claim.TargetSequence < 0 || claim.TargetSequence > claim.ActorHead || claim.Count is < 0 or > 256
            || (claim.Count == 0 && (claim.StartSequence != 1 || claim.EndSequence != 0 || claim.TargetSequence != 0))
            || (claim.Count > 0 && (claim.StartSequence < 1 || claim.EndSequence < claim.StartSequence
                || claim.EndSequence > claim.TargetSequence || claim.EndSequence - claim.StartSequence != claim.Count - 1))) { throw new ArgumentException("Invalid complete event-only logical prefix range."); }
        RequireModel(claim.LogicalEvidenceModelId);
        foreach (ReadOnlyMemory<byte> hash in new[] { claim.OrderedLogicalDigestListHash, claim.Accumulator,
            claim.RegistryFingerprint, claim.SourceBindingHash }) { RequireHash(hash); }
    }

    private static void ValidateScope(string tenant, string domain, string aggregate, string type)
    {
        var identity = new AggregateIdentity(tenant, domain, aggregate);
        if (identity.TenantId != tenant || identity.Domain != domain) { throw new ArgumentException("Claim identity must already be canonical."); }
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
    }
    private static void RequireModel(string model) { if (model != DaprLogicalSourceBinding.ModelId) { throw new ArgumentException("Logical evidence model mismatch."); } }
    private static void RequireHash(ReadOnlyMemory<byte> hash) { if (hash.Length != 32) { throw new ArgumentException("A logical hash must contain exactly 32 bytes."); } }
    private static int CompareUtf8(string left, string right)
    {
        // Strict UTF-8 was admitted already. Scalar order equals unsigned UTF-8 byte order.
        StringRuneEnumerator a = left.EnumerateRunes(); StringRuneEnumerator b = right.EnumerateRunes();
        while (a.MoveNext())
        {
            if (!b.MoveNext()) { return 1; }
            int compared = a.Current.Value.CompareTo(b.Current.Value);
            if (compared != 0) { return compared; }
        }
        return b.MoveNext() ? -1 : 0;
    }
    private static void Header(EventEvolutionBinaryWriter writer, ReadOnlySpan<byte> separator, ushort count) { writer.WriteRaw(separator); writer.WriteByte(1); writer.WriteUInt16(count); }
    private static EventEvolutionBinaryReader Start(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> separator, ushort count)
    {
        if (bytes.Length > MaximumClaimBytes) { throw new InvalidOperationException("ProofLimit: logical claim exceeds 1 MiB."); }
        var reader = new EventEvolutionBinaryReader(bytes);
        if (!reader.ReadRaw(separator.Length).SequenceEqual(separator) || reader.ReadByte() != 1 || reader.ReadUInt16() != count) { throw new ArgumentException("Logical claim separator, codec or field count mismatch."); }
        return reader;
    }
    private static void Text(EventEvolutionBinaryWriter writer, byte tag, string value)
    {
        if (StrictUtf8.GetByteCount(value) > MaximumTextBytes) { throw new InvalidOperationException("MetadataLimit: logical scalar exceeds 512 KiB."); }
        writer.WriteByte(tag); writer.WriteString(value);
    }
    private static void Number(EventEvolutionBinaryWriter writer, byte tag, long value) { writer.WriteByte(tag); writer.WriteInt64(value); }
    private static void Int(EventEvolutionBinaryWriter writer, byte tag, int value) { writer.WriteByte(tag); writer.WriteInt32(value); }
    private static void Hash(EventEvolutionBinaryWriter writer, byte tag, ReadOnlyMemory<byte> value) { writer.WriteByte(tag); writer.WriteHash(value.Span); }
    private static void OptionalText(EventEvolutionBinaryWriter writer, string? value)
    {
        if (value is not null && StrictUtf8.GetByteCount(value) > MaximumTextBytes) { throw new InvalidOperationException("MetadataLimit: logical scalar exceeds 512 KiB."); }
        writer.WriteByte(value is null ? (byte)0 : (byte)1); if (value is not null) { writer.WriteString(value); }
    }
    private static void OptionalInt(EventEvolutionBinaryWriter writer, int? value) { writer.WriteByte(value is null ? (byte)0 : (byte)1); if (value.HasValue) { writer.WriteInt32(value.Value); } }
    private static void Tag(ref EventEvolutionBinaryReader reader, byte tag) { if (reader.ReadByte() != tag) { throw new ArgumentException("Missing, reordered or duplicate logical claim field."); } }
    private static string ReadText(ref EventEvolutionBinaryReader reader, byte tag) { Tag(ref reader, tag); return reader.ReadString(512 * 1024); }
    private static long ReadNumber(ref EventEvolutionBinaryReader reader, byte tag) { Tag(ref reader, tag); return reader.ReadInt64(); }
    private static int ReadInt(ref EventEvolutionBinaryReader reader, byte tag) { Tag(ref reader, tag); return reader.ReadInt32(); }
    private static byte[] ReadHash(ref EventEvolutionBinaryReader reader, byte tag) { Tag(ref reader, tag); return reader.ReadHash().ToArray(); }
    private static bool Present(ref EventEvolutionBinaryReader reader) => reader.ReadByte() switch { 0 => false, 1 => true, _ => throw new ArgumentException("Invalid optional logical discriminator.") };
    private static string? ReadOptionalText(ref EventEvolutionBinaryReader reader) => Present(ref reader) ? reader.ReadString(512 * 1024) : null;
    private static int? ReadOptionalInt(ref EventEvolutionBinaryReader reader) => Present(ref reader) ? reader.ReadInt32() : null;
}
