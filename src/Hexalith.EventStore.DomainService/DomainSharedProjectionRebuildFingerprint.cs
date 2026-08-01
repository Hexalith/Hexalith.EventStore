using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Computes frozen v1 fingerprints for shared rebuild histories and rolling inventories.</summary>
internal static class DomainSharedProjectionRebuildFingerprint {
    private static readonly string s_emptyInventory = Hash(
        Encoding.UTF8.GetBytes("hexalith-shared-projection-rebuild-inventory-v1"));

    /// <summary>Gets the canonical empty-inventory fingerprint.</summary>
    public static string EmptyInventory => s_emptyInventory;

    /// <summary>Fingerprints one authoritative aggregate inventory entry.</summary>
    public static string ComputeHistory(string aggregateId, bool isErased, IReadOnlyList<ProjectionEventDto> events) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, aggregateId);
        AppendInt64(hash, isErased ? 1 : 0);
        AppendInt64(hash, events.Count);
        foreach (ProjectionEventDto item in events) {
            AppendString(hash, item.EventTypeName);
            AppendBytes(hash, item.Payload);
            AppendString(hash, item.SerializationFormat);
            AppendInt64(hash, item.SequenceNumber);
            AppendInt64(hash, item.Timestamp.UtcTicks);
            AppendInt64(hash, (long)item.Timestamp.Offset.TotalMinutes);
            AppendString(hash, item.CorrelationId);
            AppendNullableString(hash, item.MessageId);
            AppendNullableString(hash, item.UserId);
            AppendInt64(hash, item.GlobalPosition);
        }

        return Format(hash.GetHashAndReset());
    }

    /// <summary>Appends one accepted entry to the rolling inventory fingerprint.</summary>
    public static string AppendInventory(string previous, long ordinal, string historyFingerprint) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, previous);
        AppendInt64(hash, ordinal);
        AppendString(hash, historyFingerprint);
        return Format(hash.GetHashAndReset());
    }

    private static void AppendBytes(IncrementalHash hash, ReadOnlySpan<byte> value) {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }

    private static void AppendInt64(IncrementalHash hash, long value) {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendNullableString(IncrementalHash hash, string? value) {
        AppendInt64(hash, value is null ? 0 : 1);
        if (value is not null) {
            AppendString(hash, value);
        }
    }

    private static void AppendString(IncrementalHash hash, string value) => AppendBytes(hash, Encoding.UTF8.GetBytes(value));

    private static string Hash(ReadOnlySpan<byte> value) => Format(SHA256.HashData(value));

    private static string Format(ReadOnlySpan<byte> value)
        => "v1:" + Convert.ToBase64String(value).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
