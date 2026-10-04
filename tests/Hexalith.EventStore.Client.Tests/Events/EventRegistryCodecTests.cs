using System.Text.Json;

using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

public sealed class EventRegistryCodecTests
{
    [Fact]
    public void EmptyManifest_MatchesIndependentApprovedFramingAnswer()
    {
        Convert.ToHexStringLower(EventRegistryFingerprintCodec.Compute("d", []))
            .ShouldBe("c9fb290b9cbbf614974e36da7514249399c801e7956c9b30219964ed0785706f");
    }

    [Fact]
    public void V17Registry_MatchesLiteralFingerprintIndependentOfRegistrationOrder()
    {
        Dictionary<string, string> fixture = LoadFixture();
        ReadOnlyMemory<byte>[] rows = GetRows(fixture);
        Convert.ToHexStringLower(EventRegistryFingerprintCodec.Compute("d", rows)).ShouldBe(fixture["RegistryHash"]);
        Array.Reverse(rows);
        Convert.ToHexStringLower(EventRegistryFingerprintCodec.Compute("d", rows)).ShouldBe(fixture["RegistryHash"]);
    }

    [Fact]
    public void V17StateAndReadTransform_MatchIndependentApprovedLiteralAnswers()
    {
        Dictionary<string, string> fixture = LoadFixture();
        using var registry = new EventDomainRegistry("d", GetRows(fixture));
        EventRegistryRow descriptor = registry.Rows.Single(static row => row.Tag == 0x44);
        Convert.ToHexStringLower(EventSemanticHashCodec.ComputeStateSchemaApplyHash(descriptor)).ShouldBe(fixture["StateHash"]);
        Convert.ToHexStringLower(EventSemanticHashCodec.EncodeSharedReadRow(registry.Rows.Single(static row => row.Tag == 0x53)))
            .ShouldBe(fixture["SReadRow"]);
        Convert.ToHexStringLower(EventSemanticHashCodec.ComputeEventTransformHash(registry, "r",
            Convert.FromHexString(fixture["ProtectionAdapterRow"]))).ShouldBe(fixture["TransformHash"]);
    }

    [Fact]
    public void TrustOnlyChange_ChangesRegistryWhilePreservingReadTransform()
    {
        Dictionary<string, string> fixture = LoadFixture();
        ReadOnlyMemory<byte>[] rows = GetRows(fixture);
        byte[] shared = rows[3].ToArray();
        // S key U(d), O(absent), field count, then tag 01 and B32 trust digest.
        shared[10] ^= 1;
        rows[3] = shared;
        using var registry = new EventDomainRegistry("d", rows);

        registry.Fingerprint.ShouldNotBe(fixture["RegistryHash"]);
        Convert.ToHexStringLower(EventSemanticHashCodec.ComputeEventTransformHash(registry, "r",
            Convert.FromHexString(fixture["ProtectionAdapterRow"]))).ShouldBe(fixture["TransformHash"]);
    }

    [Fact]
    public void RejectsWrongDomainDuplicateKeyUnknownTagAndTrailingBytes()
    {
        ReadOnlyMemory<byte>[] rows = GetRows(LoadFixture());
        Should.Throw<ArgumentException>(() => EventRegistryFingerprintCodec.Compute("another-domain", rows));
        Should.Throw<ArgumentException>(() => EventRegistryFingerprintCodec.Compute("d", [rows[0], rows[0]]));
        byte[] changedTag = rows[0].ToArray();
        changedTag[0] = 0x40;
        Should.Throw<ArgumentException>(() => EventRegistryFingerprintCodec.Compute("d", [changedTag]));
        byte[] trailing = [.. rows[0].Span, 0];
        Should.Throw<ArgumentException>(() => EventRegistryFingerprintCodec.Compute("d", [trailing]));
    }

    [Fact]
    public void RejectsFieldCountAndOrderingCorruption()
    {
        byte[] row = GetRows(LoadFixture())[1].ToArray();
        // D key is U("d") + U("evt"): field count begins at byte 13.
        row[14] = 11;
        Should.Throw<ArgumentException>(() => EventRegistryFingerprintCodec.Compute("d", [row]));
        row[14] = 12;
        row[15] = 2;
        Should.Throw<ArgumentException>(() => EventRegistryFingerprintCodec.Compute("d", [row]));
    }

    [Fact]
    public void RejectsReferencedManifestOverflowBeforeDecodingRows()
    {
        Should.Throw<ArgumentException>(() => EventRegistryFingerprintCodec.Compute("d", [], 64L * 1024 * 1024));
        Should.Throw<ArgumentException>(() => EventRegistryFingerprintCodec.Compute("d", [new byte[64 * 1024 + 1]]));
        long exactRemaining = 64L * 1024 * 1024 - 22;
        EventRegistryFingerprintCodec.Compute("d", [], exactRemaining).Length.ShouldBe(32);
        Should.Throw<ArgumentException>(() => EventRegistryFingerprintCodec.Compute("d", [], exactRemaining + 1));
    }

    [Fact]
    public void DomainRegistry_ResolvesExactAliasAndRefusesMissingVersionOrChain()
    {
        Dictionary<string, string> fixture = LoadFixture();
        ReadOnlyMemory<byte>[] rows = GetRows(fixture);
        using var registry = new EventDomainRegistry("d", rows);
        registry.Fingerprint.ShouldBe(fixture["RegistryHash"]);
        registry.ResolveAlias("Legacy.Event").ShouldBe(("evt", 1, "json"));
        registry.GetCurrentVersion("evt").ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => registry.ResolveAlias("legacy.event"));
        Should.Throw<InvalidOperationException>(() => registry.GetVersion("evt", 2));
        Should.Throw<InvalidOperationException>(() => new EventDomainRegistry("d", [rows[0], rows[1], rows[3]]));
    }

    [Fact]
    public void DomainRegistry_RejectsRetainedVersionWithMissingAdjacentEdge()
    {
        ReadOnlyMemory<byte>[] rows = GetRows(LoadFixture());
        byte[] descriptor = rows[1].ToArray();
        using (var decoded = new EventRegistryRow(descriptor))
        {
            // Replace the exact four-byte current version through its field framing.
            byte[] currentVersion = decoded.GetEncodedField(2).ToArray();
            int offset = GetFieldOffset(descriptor, 2, "UU", "UIUHUHIBUHHH");
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(descriptor.AsSpan(offset, currentVersion.Length), 2);
        }

        byte[] versionTwo = rows[2].ToArray();
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(versionTwo.AsSpan(13, 4), 2);
        Should.Throw<InvalidOperationException>(() => new EventDomainRegistry("d", [rows[0], descriptor, rows[2], versionTwo, rows[3]]));
    }

    [Fact]
    public void Primitives_PreserveMultibyteUtf8SignedIntegersAndOriginalOffset()
    {
        DateTimeOffset timestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));
        using var writer = new EventEvolutionBinaryWriter(128);
        writer.WriteString("é😀");
        writer.WriteInt32(-42);
        writer.WriteInt64(long.MaxValue);
        writer.WriteTimestamp(timestamp);
        writer.WriteInstant(timestamp);
        byte[] encoded = writer.CopyEncodedBytes();
        var reader = new EventEvolutionBinaryReader(encoded);
        reader.ReadString(6).ShouldBe("é😀");
        reader.ReadInt32().ShouldBe(-42);
        reader.ReadInt64().ShouldBe(long.MaxValue);
        DateTimeOffset restored = reader.ReadTimestamp();
        restored.EqualsExact(timestamp).ShouldBeTrue();
        reader.ReadInstant().Offset.ShouldBe(TimeSpan.Zero);
        reader.RequireEnd();
    }

    [Fact]
    public void PrimitiveWriter_RejectsInvalidUnicodeBeforeAdvancingAndOverflowBeforeCopying()
    {
        using var writer = new EventEvolutionBinaryWriter(8);
        Should.Throw<System.Text.EncoderFallbackException>(() => writer.WriteString("\ud800"));
        writer.Length.ShouldBe(0);
        Should.Throw<InvalidOperationException>(() => writer.WriteString("12345"));
        writer.Length.ShouldBe(0);
        writer.WriteString("1234");
        writer.Length.ShouldBe(8);
    }

    private static ReadOnlyMemory<byte>[] GetRows(Dictionary<string, string> fixture)
        => [Convert.FromHexString(fixture["AliasRow"]), Convert.FromHexString(fixture["DescriptorRow"]),
            Convert.FromHexString(fixture["VersionRow"]), Convert.FromHexString(fixture["SharedRow"])];

    private static Dictionary<string, string> LoadFixture()
        => JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Events", "Fixtures", "EventRegistryV17.json")))!;

    private static int GetFieldOffset(byte[] encoded, int fieldTag, string keyTypes, string fieldTypes)
    {
        var reader = new EventEvolutionBinaryReader(encoded);
        _ = reader.ReadByte();
        foreach (char type in keyTypes)
        {
            if (type == 'U')
            {
                _ = reader.ReadString(64 * 1024);
            }
            else
            {
                _ = reader.ReadInt32();
            }
        }

        _ = reader.ReadUInt16();
        for (int tag = 1; tag <= fieldTypes.Length; tag++)
        {
            _ = reader.ReadByte();
            if (tag == fieldTag)
            {
                return reader.Position;
            }

            if (fieldTypes[tag - 1] == 'U' || fieldTypes[tag - 1] == 'B')
            {
                _ = reader.ReadBytes(64 * 1024);
            }
            else if (fieldTypes[tag - 1] == 'H')
            {
                _ = reader.ReadHash();
            }
            else
            {
                _ = reader.ReadInt32();
            }
        }

        throw new ArgumentOutOfRangeException(nameof(fieldTag));
    }
}
