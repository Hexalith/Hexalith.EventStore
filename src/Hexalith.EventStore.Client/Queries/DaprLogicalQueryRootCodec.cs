using System.Text;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.Queries;
/// <summary>Hashes a complete bounded typed actor root under its distinct logical query framing.</summary>
internal static class DaprLogicalQueryRootCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    /// <summary>Checks the full inventory and measures its exact writer capacity before allocation.</summary>
    internal static int Measure(DaprLogicalQueryRoot root, int maximumBytes)
    {
        if (root.ModelId != DaprLogicalSourceBinding.ModelId || root.Generation < 1 || root.Rows is null || root.BackendDescriptor is null || root.Rows.Length > 256 || root.BackendDescriptor.Length is < 1 or > 4096)
        {
            throw new InvalidOperationException("ReadModelQueryConsistencyHold: invalid logical query root.");
        }

        int size = "HX-EV-DAPR-LOGICAL-QUERY-ROOT-1\0"u8.Length + 2 + 8 + 4 + 4 + root.BackendDescriptor.Length;
        foreach (string name in new[]
        {
            root.ModelId,
            root.Tenant,
            root.Domain,
            root.HandlerRoute,
            root.KeySpace,
            root.StoreName
        }

        )
        {
            size = checked(size + TextSize(name));
        }

        byte[]? previous = null;
        int payloadBytes = 0;
        foreach (DaprLogicalQueryRow row in root.Rows)
        {
            if (row is null || row.ValueTypeName is null)
            {
                throw new InvalidOperationException("ReadModelQueryConsistencyHold: missing row metadata.");
            }

            int keySize = TextSize(row.Key), originSize = TextSize(row.OriginOperationId);
            int typeSize = row.Payload is null ? 4 : TextSize(row.ValueTypeName);
            if (row.Payload is null && row.ValueTypeName.Length != 0)
            {
                throw new InvalidOperationException("ReadModelQueryConsistencyHold: absent row has a value type.");
            }

            byte[] key = StrictUtf8.GetBytes(row.Key);
            if (previous is not null && previous.AsSpan().SequenceCompareTo(key) >= 0)
            {
                throw new InvalidOperationException("ReadModelQueryConsistencyHold: unordered or duplicate logical row.");
            }

            previous = key;
            if (row.Payload is null && row.ExpiresAt is not null)
            {
                throw new InvalidOperationException("ReadModelQueryConsistencyHold: absent row has an expiry.");
            }

            payloadBytes = checked(payloadBytes + (row.Payload?.Length ?? 0));
            if (payloadBytes > 64 * 1024 * 1024)
            {
                throw new InvalidOperationException("ReadModelQueryLimit: cumulative rows exceed 64 MiB.");
            }

            size = checked(size + keySize + typeSize + originSize + 2 + (row.Payload is null ? 0 : 4 + row.Payload.Length) + (row.ExpiresAt is null ? 0 : 8));
        }

        if (size > maximumBytes || maximumBytes > 64 * 1024 * 1024)
        {
            throw new InvalidOperationException("ReadModelQueryLimit: logical root exceeds its admitted capacity.");
        }

        return size;
    }

    /// <summary>Hashes the exact admitted root while its full writer capacity remains charged.</summary>
    internal static byte[] Compute(DaprLogicalQueryRoot root, int maximumBytes, EventBufferBudget budget)
    {
        int size = Measure(root, maximumBytes);
        using EventBufferReservation charge = budget.Reserve(size);
        using var writer = new EventEvolutionBinaryWriter(size);
        writer.WriteRaw("HX-EV-DAPR-LOGICAL-QUERY-ROOT-1\0"u8);
        writer.WriteUInt16(1);
        writer.WriteString(root.ModelId);
        writer.WriteString(root.Tenant);
        writer.WriteString(root.Domain);
        writer.WriteString(root.HandlerRoute);
        writer.WriteString(root.KeySpace);
        writer.WriteString(root.StoreName);
        writer.WriteBytes(root.BackendDescriptor);
        writer.WriteInt64(root.Generation);
        writer.WriteUInt32(checked((uint)root.Rows.Length));
        foreach (DaprLogicalQueryRow row in root.Rows)
        {
            writer.WriteString(row.Key);
            writer.WriteString(row.ValueTypeName);
            writer.WriteByte(row.Payload is null ? (byte)0 : (byte)2);
            if (row.Payload is not null)
            {
                writer.WriteBytes(row.Payload);
            }

            writer.WriteByte(row.ExpiresAt is null ? (byte)0 : (byte)2);
            if (row.ExpiresAt is not null)
            {
                writer.WriteInstant(row.ExpiresAt.Value);
            }

            writer.WriteString(row.OriginOperationId);
        }

        return writer.ComputeSha256();
    }

    private static int TextSize(string value)
    {
        if (value is null)
        {
            throw new InvalidOperationException("ReadModelQueryConsistencyHold: missing root identity.");
        }

        int bytes = StrictUtf8.GetByteCount(value);
        if (bytes is < 1 or > 1024)
        {
            throw new InvalidOperationException("ReadModelQueryConsistencyHold: invalid root identity.");
        }

        return checked(4 + bytes);
    }
}
