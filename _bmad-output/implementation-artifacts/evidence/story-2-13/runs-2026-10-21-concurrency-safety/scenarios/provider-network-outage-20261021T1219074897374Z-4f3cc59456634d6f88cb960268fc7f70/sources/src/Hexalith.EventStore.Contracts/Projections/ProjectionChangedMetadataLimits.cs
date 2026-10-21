using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>Owns the contract ceilings and compact JSON sizing of projection-change metadata.</summary>
public static class ProjectionChangedMetadataLimits
{
    /// <summary>The maximum number of metadata entries on either notification transport.</summary>
    public const int MaximumEntries = 16;

    /// <summary>The maximum serialized UTF-8 bytes, including JSON punctuation and escaping.</summary>
    public const int MaximumSerializedUtf8Bytes = 2_048;

    /// <summary>The serialized UTF-8 size of the empty metadata object, <c>{}</c>.</summary>
    public const int MinimumSerializedUtf8Bytes = 2;

    /// <summary>Checks the compact default System.Text.Json representation against the contract and configured cap.</summary>
    /// <param name="metadata">The optional metadata map; an absent map has no detail payload.</param>
    /// <param name="maxBytes">A configured cap that may lower the contract ceiling.</param>
    /// <returns>Whether the map contains valid strings and fits the serialized byte cap.</returns>
    public static bool FitsSerializedByteLimit(
        IReadOnlyDictionary<string, string>? metadata,
        int maxBytes = MaximumSerializedUtf8Bytes)
    {
        if (metadata is null)
        {
            return true;
        }

        int limit = Math.Min(maxBytes, MaximumSerializedUtf8Bytes);
        if (limit < MinimumSerializedUtf8Bytes)
        {
            return false;
        }

        // Raw strings are a lower bound. Reject them before allocating an escaped JSON buffer.
        long rawBytes = 0;
        foreach (KeyValuePair<string, string> entry in metadata)
        {
            if (entry.Key is null || entry.Value is null)
            {
                return false;
            }

            rawBytes += Encoding.UTF8.GetByteCount(entry.Key) + (long)Encoding.UTF8.GetByteCount(entry.Value);
            if (rawBytes > limit)
            {
                return false;
            }
        }

        return JsonSerializer.SerializeToUtf8Bytes(metadata).Length <= limit;
    }

    /// <summary>Clips metadata in ordinal key order to the configured and contract ceilings.</summary>
    /// <param name="metadata">The optional input map.</param>
    /// <param name="maxEntries">The configured entry cap.</param>
    /// <param name="maxBytes">The configured serialized byte cap, including the empty object.</param>
    /// <returns>An immutable map whose compact JSON representation fits both ceilings.</returns>
    public static IReadOnlyDictionary<string, string> Bound(
        IReadOnlyDictionary<string, string>? metadata,
        int maxEntries,
        int maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, MinimumSerializedUtf8Bytes);
        var bounded = new Dictionary<string, string>(StringComparer.Ordinal);
        if (metadata is not null)
        {
            foreach (KeyValuePair<string, string> entry in metadata.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
            {
                if (entry.Key is null || entry.Value is null)
                {
                    throw new ArgumentException("Metadata keys and values must be non-null.", nameof(metadata));
                }

                if (bounded.Count >= Math.Min(maxEntries, MaximumEntries))
                {
                    break;
                }

                bounded.Add(entry.Key, entry.Value);
                if (!FitsSerializedByteLimit(bounded, maxBytes))
                {
                    _ = bounded.Remove(entry.Key);
                    break;
                }
            }
        }

        return new ReadOnlyDictionary<string, string>(bounded);
    }
}
