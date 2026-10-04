using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Computes the approved state/Apply and read-transform identities separately from writer and trust inventory.</summary>
/// <remarks>Selected dependency rows require independently attested reachability before production use.</remarks>
internal static class EventSemanticHashCodec
{
    /// <summary>Computes exact state/Apply identity from the D descriptor's independently framed fields.</summary>
    internal static byte[] ComputeStateSchemaApplyHash(EventRegistryRow descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.Tag != 0x44)
        {
            throw new ArgumentException("State identity requires a D descriptor.", nameof(descriptor));
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("HX-EV-STATE-APPLY-1\0"u8);
        hash.AppendData([1]);
        foreach (int field in new[] { 1, 7, 8, 9, 10, 11, 5, 6 })
        {
            hash.AppendData(descriptor.GetEncodedField(field));
        }

        return hash.GetHashAndReset();
    }

    /// <summary>Renders the exact fixed S-read subset, excluding all trust/write-only members.</summary>
    internal static byte[] EncodeSharedReadRow(EventRegistryRow shared)
    {
        ArgumentNullException.ThrowIfNull(shared);
        if (shared.Tag != 0x53)
        {
            throw new ArgumentException("Shared read identity requires an S descriptor.", nameof(shared));
        }

        int length = 4;
        foreach (int field in Enumerable.Range(2, 15).Concat(Enumerable.Range(29, 3)))
        {
            length = checked(length + 1 + shared.GetEncodedField(field).Length);
        }

        using var writer = new EventEvolutionBinaryWriter(length);
        writer.WriteByte(0x73);
        writer.WriteByte(1);
        writer.WriteUInt16(18);
        byte tag = 1;
        foreach (int field in Enumerable.Range(2, 15).Concat(Enumerable.Range(29, 3)))
        {
            writer.WriteByte(tag++);
            writer.WriteRaw(shared.GetEncodedField(field));
        }

        return writer.CopyEncodedBytes();
    }

    /// <summary>Hashes all immutable admitted read rows on a route and its exact selected dependency subset.</summary>
    internal static byte[] ComputeEventTransformHash(EventDomainRegistry registry, string aggregateRouteId,
        ReadOnlySpan<byte> protectionAdapterRow, IReadOnlyList<EventRegistryRow>? selectedDependencies = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrEmpty(aggregateRouteId);
        ValidateProtectionAdapter(protectionAdapterRow);
        EventRegistryRow[] descriptors = registry.Rows.Where(row => row.Tag == 0x44
            && string.Equals(row.GetTextField(1), aggregateRouteId, StringComparison.Ordinal)).ToArray();
        if (descriptors.Length == 0)
        {
            throw new ArgumentException("The aggregate route is not registered.", nameof(aggregateRouteId));
        }

        var types = descriptors.Select(static row => row.GetTextKey(1)).ToHashSet(StringComparer.Ordinal);
        EventRegistryRow[] readRows = registry.Rows.Where(row => (row.Tag is 0x44 or 0x56 or 0x45) && types.Contains(row.GetTextKey(1))
            || row.Tag == 0x41 && types.Contains(row.GetTextField(1))).ToArray();
        Array.Sort(readRows, static (left, right) => left.CompareKey(right));
        EventRegistryRow[] dependencies = selectedDependencies?.ToArray() ?? [];
        Array.Sort(dependencies, static (left, right) => left.CompareKey(right));
        for (int i = 0; i < dependencies.Length; i++)
        {
            EventRegistryRow dependency = dependencies[i];
            if (dependency.Tag != 0x47 || (i > 0 && dependencies[i - 1].CompareKey(dependency) == 0)
                || !registry.Rows.Any(row => row.Tag == 0x47 && row.Encoded.SequenceEqual(dependency.Encoded)))
            {
                throw new ArgumentException("Transform dependencies must be a unique exact subset of the admitted G rows.", nameof(selectedDependencies));
            }
        }

        byte[] stateHash = ComputeStateSchemaApplyHash(descriptors[0]);
        if (!stateHash.AsSpan().SequenceEqual(descriptors[0].GetEncodedField(12)))
        {
            throw new ArgumentException("The D descriptor contains an invalid state/Apply hash.", nameof(registry));
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("HX-EV-TRANSFORM-1\0"u8);
        hash.AppendData([1]);
        using (var scope = new EventEvolutionBinaryWriter(checked(8 + System.Text.Encoding.UTF8.GetByteCount(registry.Domain)
            + System.Text.Encoding.UTF8.GetByteCount(aggregateRouteId))))
        {
            scope.WriteString(registry.Domain);
            scope.WriteString(aggregateRouteId);
            hash.AppendData(scope.CopyEncodedBytes());
        }

        Span<byte> count = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(count, checked((uint)(readRows.Length + 2 + dependencies.Length)));
        hash.AppendData(count);
        foreach (EventRegistryRow row in readRows)
        {
            hash.AppendData(row.Encoded);
        }

        hash.AppendData(stateHash);
        hash.AppendData(EncodeSharedReadRow(registry.Rows.Single(static row => row.Tag == 0x53)));
        hash.AppendData(protectionAdapterRow);
        foreach (EventRegistryRow dependency in dependencies)
        {
            hash.AppendData(dependency.Encoded);
        }

        return hash.GetHashAndReset();
    }

    private static void ValidateProtectionAdapter(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 64 * 1024)
        {
            throw new ArgumentException("RegistryLimit: a protection adapter row is limited to 64 KiB.", nameof(bytes));
        }

        var reader = new EventEvolutionBinaryReader(bytes);
        if (reader.ReadByte() != 0x70 || reader.ReadByte() != 1 || reader.ReadUInt16() != 3 || reader.ReadByte() != 1
            || string.IsNullOrEmpty(reader.ReadString(64 * 1024)) || reader.ReadByte() != 2)
        {
            throw new ArgumentException("Malformed protection adapter row.", nameof(bytes));
        }

        _ = reader.ReadHash();
        if (reader.ReadByte() != 3)
        {
            throw new ArgumentException("Malformed protection adapter row tag.", nameof(bytes));
        }

        _ = reader.ReadHash();
        reader.RequireEnd();
    }
}
