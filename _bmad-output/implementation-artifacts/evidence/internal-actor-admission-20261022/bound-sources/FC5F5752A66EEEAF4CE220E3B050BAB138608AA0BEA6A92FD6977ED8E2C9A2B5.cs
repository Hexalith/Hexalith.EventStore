using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Hashes strictly framed immutable registry rows using the approved sorted manifest preimage.</summary>
/// <remarks>This codec does not attest dependency closure or grant registry readiness.</remarks>
internal static class EventRegistryFingerprintCodec
{
    /// <summary>Validates and hashes one domain's admitted rows without materializing a complete manifest copy.</summary>
    internal static byte[] Compute(string domain, IReadOnlyList<ReadOnlyMemory<byte>> encodedRows, long referencedManifestBytes = 0)
    {
        EventRegistryRow[] rows = DecodeRows(domain, encodedRows, referencedManifestBytes);
        try
        {
            return ComputeDecoded(rows);
        }
        finally
        {
            foreach (EventRegistryRow row in rows)
            {
                row.Dispose();
            }
        }
    }

    /// <summary>Admits and owns one immutable sorted domain inventory under the manifest accounting bound.</summary>
    internal static EventRegistryRow[] DecodeRows(string domain, IReadOnlyList<ReadOnlyMemory<byte>> encodedRows, long referencedManifestBytes = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(domain);
        ArgumentNullException.ThrowIfNull(encodedRows);
        int count = encodedRows.Count;
        if (count > 65_536 || referencedManifestBytes < 0)
        {
            throw new ArgumentException("RegistryLimit: invalid row count or referenced manifest capacity.", nameof(encodedRows));
        }

        long accounted = checked(referencedManifestBytes + "HX-EV-REGISTRY-1\0"u8.Length + 1 + 4 + count * 256L);
        if (accounted > 64L * 1024 * 1024)
        {
            throw new ArgumentException("RegistryLimit: referenced manifest and sorting workspace exceed 64 MiB.", nameof(encodedRows));
        }

        var inputs = new ReadOnlyMemory<byte>[count];
        for (int i = 0; i < count; i++)
        {
            inputs[i] = encodedRows[i];
            if (inputs[i].Length is < 1 or > 64 * 1024)
            {
                throw new ArgumentException("RegistryLimit: an encoded registry row is limited to 64 KiB.", nameof(encodedRows));
            }

            // Encoded copies, decoded keys and UTF-8 validation/sorting workspace
            // are charged conservatively before any private row is constructed.
            accounted = checked(accounted + inputs[i].Length * 4L);
            if (accounted > 64L * 1024 * 1024)
            {
                throw new ArgumentException("RegistryLimit: the manifest and sorting workspace exceed 64 MiB.", nameof(encodedRows));
            }
        }

        var rows = new List<EventRegistryRow>(count);
        try
        {
            foreach (ReadOnlyMemory<byte> input in inputs)
            {
                var row = new EventRegistryRow(input.Span);
                rows.Add(row);
                if (!string.Equals(row.Domain, domain, StringComparison.Ordinal))
                {
                    throw new ArgumentException("A registry manifest cannot contain another domain's row.", nameof(encodedRows));
                }
            }

            rows.Sort(static (left, right) => left.CompareKey(right));
            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i - 1].CompareKey(rows[i]) == 0)
                {
                    throw new ArgumentException("Duplicate registry primary key.", nameof(encodedRows));
                }
            }

            return rows.ToArray();
        }
        catch
        {
            foreach (EventRegistryRow row in rows)
            {
                row.Dispose();
            }

            throw;
        }
    }

    /// <summary>Hashes a privately owned sorted inventory without another complete manifest allocation.</summary>
    internal static byte[] ComputeDecoded(EventRegistryRow[] rows)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("HX-EV-REGISTRY-1\0"u8);
        hash.AppendData([1]);
        Span<byte> encodedCount = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(encodedCount, checked((uint)rows.Length));
        hash.AppendData(encodedCount);
        foreach (EventRegistryRow row in rows)
        {
            hash.AppendData(row.Encoded);
        }

        return hash.GetHashAndReset();
    }
}
