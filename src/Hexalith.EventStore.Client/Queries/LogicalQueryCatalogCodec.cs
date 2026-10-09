using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.Queries;
/// <summary>Encodes the compatible query catalog digest without creating a signature or result proof.</summary>
internal static class LogicalQueryCatalogCodec
{
    /// <summary>Checks the exact ordered root templates in a strictly decoded fifteen-field query row.</summary>
    internal static void RequireBindings(EventRegistryRow row)
    {
        if (row.Tag != 0x5b)
        {
            throw new ArgumentException("A query descriptor requires the 5b row.");
        }

        var field = new EventEvolutionBinaryReader(row.GetEncodedField(15));
        ReadOnlySpan<byte> encoded = field.ReadBytes(64 * 1024);
        field.RequireEnd();
        var reader = new EventEvolutionBinaryReader(encoded);
        uint count = reader.ReadUInt32();
        if (count is < 1 or > 256)
        {
            throw new ArgumentException("ReadModelQueryLimit: invalid root binding count.");
        }

        byte[][]? previous = null;
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> binding = reader.ReadBytes(64 * 1024);
            var item = new EventEvolutionBinaryReader(binding);
            string route = item.ReadString(1024), keySpace = item.ReadString(1024), ownership = item.ReadString(32);
            string store = item.ReadString(1024);
            ReadOnlySpan<byte> backend = item.ReadBytes(4096);
            string aggregate = item.ReadString(1024);
            item.RequireEnd();
            if (route.Length == 0 || keySpace.Length == 0 || store.Length == 0 || backend.IsEmpty || ownership is not ("aggregate" or "shared") || (ownership == "shared") != (aggregate.Length == 0))
            {
                throw new ArgumentException("ReadModelRouteContextRequired: invalid root template.");
            }

            byte[][] components = [Encoding.UTF8.GetBytes(route), Encoding.UTF8.GetBytes(keySpace), Encoding.UTF8.GetBytes(ownership), Encoding.UTF8.GetBytes(store), backend.ToArray(), Encoding.UTF8.GetBytes(aggregate)];
            if (previous is not null && CompareComponents(previous, components) >= 0)
            {
                throw new ArgumentException("ReadModelRouteContextRequired: root templates must be unique and canonically ordered.");
            }

            previous = components;
        }

        reader.RequireEnd();
    }

    /// <summary>Hashes exact 5b, selected input and selected dependency rows under their compatible framing.</summary>
    internal static byte[] Compute(ReadOnlySpan<byte> queryRow, IReadOnlyList<ReadOnlyMemory<byte>> inputRows, IReadOnlyList<ReadOnlyMemory<byte>> dependencyRows, EventBufferBudget budget)
    {
        using var row = new EventRegistryRow(queryRow, allowCatalog: true);
        RequireBindings(row);
        int size = checked("HX-EV-QUERY-ROUTE-1\0"u8.Length + 1 + 4 + queryRow.Length + 4 + 4 + inputRows.Sum(static x => checked(4 + x.Length)) + dependencyRows.Sum(static x => checked(4 + x.Length)));
        if (size > 8 * 1024 * 1024)
        {
            throw new InvalidOperationException("ReadModelQueryLimit: catalog evidence exceeds 8 MiB.");
        }

        using EventBufferReservation charge = budget.Reserve(size);
        using var writer = new EventEvolutionBinaryWriter(size);
        writer.WriteRaw("HX-EV-QUERY-ROUTE-1\0"u8);
        writer.WriteByte(1);
        writer.WriteBytes(queryRow);
        writer.WriteUInt32(checked((uint)inputRows.Count));
        EventRegistryRow? previous = null;
        try
        {
            foreach (ReadOnlyMemory<byte> input in inputRows)
            {
                var decoded = new EventRegistryRow(input.Span, allowCatalog: true);
                try
                {
                    if (decoded.Tag is not (0x52 or 0x59) || decoded.Domain != row.Domain || previous is not null && previous.CompareKey(decoded) >= 0)
                    {
                        throw new ArgumentException("Query input rows must be same-domain, unique and canonically ordered.");
                    }

                    writer.WriteBytes(input.Span);
                }
                catch
                {
                    decoded.Dispose();
                    throw;
                }

                previous?.Dispose();
                previous = decoded;
            }
        }
        finally
        {
            previous?.Dispose();
        }

        writer.WriteUInt32(checked((uint)dependencyRows.Count));
        previous = null;
        try
        {
            foreach (ReadOnlyMemory<byte> dependency in dependencyRows)
            {
                var decoded = new EventRegistryRow(dependency.Span);
                try
                {
                    if (decoded.Tag != 0x47 || decoded.Domain != row.Domain || previous is not null && previous.CompareKey(decoded) >= 0)
                    {
                        throw new ArgumentException("Query dependency rows must be same-domain, unique and canonically ordered.");
                    }

                    writer.WriteBytes(dependency.Span);
                }
                catch
                {
                    decoded.Dispose();
                    throw;
                }

                previous?.Dispose();
                previous = decoded;
            }
        }
        finally
        {
            previous?.Dispose();
        }

        return writer.ComputeSha256();
    }

    private static int CompareComponents(byte[][] left, byte[][] right)
    {
        for (int i = 0; i < left.Length; i++)
        {
            int order = left[i].AsSpan().SequenceCompareTo(right[i]);
            if (order != 0)
            {
                return order;
            }
        }

        return 0;
    }
}
