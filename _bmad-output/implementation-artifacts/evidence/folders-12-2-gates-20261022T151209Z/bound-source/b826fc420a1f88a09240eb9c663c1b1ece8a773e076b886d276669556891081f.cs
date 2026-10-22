using System.Collections;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Retains complete identity evidence as immutable SHA-256 bytes while exposing its unchanged canonical Base64 list.</summary>
internal sealed class ProjectionMessageIdDigests(byte[] bytes, IReadOnlyDictionary<int, string?>? malformed = null) : IReadOnlyList<string>
{
    private readonly byte[] _bytes = bytes;
    private readonly IReadOnlyDictionary<int, string?>? _malformed = malformed;
    /// <inheritdoc/>
    public int Count => _bytes.Length / SHA256.HashSizeInBytes;

    /// <inheritdoc/>
    public string this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            return _malformed is not null && _malformed.TryGetValue(index, out string? invalid)
                ? invalid! : Convert.ToBase64String(_bytes.AsSpan(index * SHA256.HashSizeInBytes, SHA256.HashSizeInBytes));
        }
    }

    /// <summary>Appends every supplied identity, preserving the complete existing prefix and visit accounting.</summary>
    internal static ProjectionMessageIdDigests Append(IReadOnlyList<string>? prefix, IReadOnlyList<ProjectionDeliveryEventDigest> events)
    {
        int count = prefix?.Count ?? 0;
        var result = new byte[checked((count + events.Count) * SHA256.HashSizeInBytes)];
        if (prefix is ProjectionMessageIdDigests compact && compact._malformed is null)
        {
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofVisits, count);
            compact._bytes.CopyTo(result, 0);
        }
        else if (prefix is not null)
        {
            int index = 0;
            foreach (string value in ProjectionDeliveryCostDiagnostics.VisitProof(prefix))
            {
                Span<byte> target = result.AsSpan(index++ * SHA256.HashSizeInBytes, SHA256.HashSizeInBytes);
                if (!Convert.TryFromBase64String(value, target, out int written) || written != SHA256.HashSizeInBytes
                    || Convert.ToBase64String(target) != value) throw new InvalidOperationException("delivery_identity_conflict");
            }
        }
        foreach (ProjectionDeliveryEventDigest value in ProjectionDeliveryCostDiagnostics.VisitProof(events))
            SHA256.HashData(Encoding.UTF8.GetBytes(value.MessageId), result.AsSpan(count++ * SHA256.HashSizeInBytes, SHA256.HashSizeInBytes));
        return new(result);
    }

    /// <summary>Validates every canonical complete digest and retains exact binary membership keys only.</summary>
    internal static HashSet<ProjectionMessageIdDigest>? CreateSet(IReadOnlyList<string> values)
    {
        var result = new HashSet<ProjectionMessageIdDigest>(values.Count);
        foreach (string value in ProjectionDeliveryCostDiagnostics.VisitProof(values))
            if (!ProjectionMessageIdDigest.TryParse(value, out ProjectionMessageIdDigest digest) || !result.Add(digest)) return null;
        return result;
    }

    /// <inheritdoc/>
    public IEnumerator<string> GetEnumerator()
    {
        for (int index = 0; index < Count; index++) yield return this[index];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
