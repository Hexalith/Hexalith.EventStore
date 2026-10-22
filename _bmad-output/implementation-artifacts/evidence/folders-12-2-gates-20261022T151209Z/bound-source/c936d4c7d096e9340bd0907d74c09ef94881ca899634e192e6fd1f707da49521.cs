using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Computes frozen v1 fingerprints for shared rebuild histories and rolling inventories.</summary>
public static class DomainSharedProjectionRebuildFingerprint {
    private static readonly string s_emptyInventory = Hash(
        Encoding.UTF8.GetBytes("hexalith-shared-projection-rebuild-inventory-v1"));

    /// <summary>Gets the canonical empty-inventory fingerprint.</summary>
    public static string EmptyInventory => s_emptyInventory;

    /// <summary>Fingerprints one authoritative aggregate inventory entry.</summary>
    /// <param name="aggregateId">The aggregate owner.</param>
    /// <param name="isErased">Whether the owner is erased.</param>
    /// <param name="events">The exact owner history.</param>
    /// <param name="expectedFingerprint">An existing receipt whose format should be retained.</param>
    public static string ComputeHistory(string aggregateId, bool isErased,
        IReadOnlyList<ProjectionEventDto> events, string? expectedFingerprint = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentNullException.ThrowIfNull(events);
        using var builder = new DomainSharedProjectionHistoryFingerprintBuilder(
            aggregateId, isErased, events.Count, expectedFingerprint);
        foreach (ProjectionEventDto item in events) {
            builder.Append(item);
        }

        return builder.Finish();
    }

    /// <summary>Appends one accepted entry to the rolling inventory fingerprint.</summary>
    public static string AppendInventory(string previous, long ordinal, string historyFingerprint) {
        ArgumentException.ThrowIfNullOrWhiteSpace(previous);
        ArgumentException.ThrowIfNullOrWhiteSpace(historyFingerprint);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, previous);
        AppendInt64(hash, ordinal);
        AppendString(hash, historyFingerprint);
        return Format(hash.GetHashAndReset());
    }

    internal static void AppendBytes(IncrementalHash hash, ReadOnlySpan<byte> value) {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }

    internal static void AppendInt64(IncrementalHash hash, long value) {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        hash.AppendData(bytes);
    }

    internal static void AppendNullableString(IncrementalHash hash, string? value) {
        AppendInt64(hash, value is null ? 0 : 1);
        if (value is not null) {
            AppendString(hash, value);
        }
    }

    internal static void AppendString(IncrementalHash hash, string value) => AppendBytes(hash, Encoding.UTF8.GetBytes(value));

    private static string Hash(ReadOnlySpan<byte> value) => Format(SHA256.HashData(value));

    internal static string Format(ReadOnlySpan<byte> value)
        => "v1:" + Convert.ToBase64String(value).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
