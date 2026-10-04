using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

public sealed class EventLocalImplementationBindingTests
{
    [Fact]
    public void DependencyFileCheckAcceptsExactManagedBytesAndRefusesDriftAndCancellation()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(file, [1, 2, 3]);
            using var writer = new EventEvolutionBinaryWriter(512);
            writer.WriteByte(0x47); writer.WriteString("d"); writer.WriteString("dependency"); writer.WriteString("managed");
            writer.WriteUInt16(3);
            writer.WriteByte(1); writer.WriteString("abi-1");
            writer.WriteByte(2); writer.WriteHash(SHA256.HashData(new byte[] { 1, 2, 3 }));
            writer.WriteByte(3); writer.WriteString("locked-context");
            using var row = new EventRegistryRow(writer.CopyEncodedBytes());
            EventDependencyFileVerifier.RequireExactFile(row, file, CancellationToken.None);
            File.WriteAllBytes(file, [1, 2, 4]);
            Should.Throw<InvalidOperationException>(() => EventDependencyFileVerifier.RequireExactFile(row, file, CancellationToken.None));
            Should.Throw<OperationCanceledException>(() => EventDependencyFileVerifier.RequireExactFile(row, file, new CancellationToken(true)));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void CurrentDeserializerChecksExplicitTypeFileAndSerializerBeforeInvocationAndExpiresLease()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IReadOnlyPayload? retained = null;
        var deserializer = new RegisteredCurrentEventDeserializer(typeof(AllowlistedCurrentEventTestValue), "test-serializer", (payload, _) =>
        {
            retained = payload;
            return new AllowlistedCurrentEventTestValue();
        }, "{}\n"u8.ToArray(), []);
        using var writer = new BoundedPayloadWriter(2, CancellationToken.None);
        writer.Write([1, 2]); writer.Complete();
        using ImmutablePayload payload = writer.TakeCompletedPayload();
        deserializer.Deserialize(registry, "evt", payload, CancellationToken.None).ShouldBeOfType<AllowlistedCurrentEventTestValue>();
        Should.Throw<ObjectDisposedException>(() => retained!.CopyTo(0, new byte[2]));

        bool called = false;
        var wrongBinding = new RegisteredCurrentEventDeserializer(typeof(string), "test-serializer", (_, _) =>
        {
            called = true;
            return "wrong";
        }, "{}\n"u8.ToArray(), []);
        Should.Throw<InvalidOperationException>(() => wrongBinding.Deserialize(registry, "evt", payload, CancellationToken.None));
        called.ShouldBeFalse();
        var wrongOutput = new RegisteredCurrentEventDeserializer(typeof(AllowlistedCurrentEventTestValue), "test-serializer", (_, _) => "wrong", "{}\n"u8.ToArray(), []);
        Should.Throw<InvalidOperationException>(() => wrongOutput.Deserialize(registry, "evt", payload, CancellationToken.None));
        var wrongSerializer = new RegisteredCurrentEventDeserializer(typeof(AllowlistedCurrentEventTestValue), "unknown", (_, _) => new AllowlistedCurrentEventTestValue(), "{}\n"u8.ToArray(), []);
        Should.Throw<InvalidOperationException>(() => wrongSerializer.Deserialize(registry, "evt", payload, CancellationToken.None));
    }

    [Fact]
    public void BoundVersionValidationChecksBothDescriptorsBeforeCallbacksAndExpiresBothLeases()
    {
        using EventDomainRegistry registry = CreateRegistry();
        using var cancellation = new CancellationTokenSource();
        IReadOnlyPayload? schemaPayload = null;
        IReadOnlyPayload? identityPayload = null;
        var calls = new List<string>();
        EventVersionValidator schema = (_, _, _, _, payload, token) =>
        {
            token.ShouldBe(cancellation.Token);
            schemaPayload = payload;
            calls.Add("schema");
        };
        EventVersionValidator identity = (_, _, _, _, payload, token) =>
        {
            token.ShouldBe(cancellation.Token);
            identityPayload = payload;
            Should.Throw<ObjectDisposedException>(() => schemaPayload!.CopyTo(0, new byte[1]));
            calls.Add("identity");
        };
        var validation = new RegisteredEventVersionValidation(registry,
            "test-schema", schema, "{}\n"u8.ToArray(), [], "test-identity", identity, "{}\n"u8.ToArray(), []);
        using var writer = new BoundedPayloadWriter(1, cancellation.Token);
        writer.Write([1]); writer.Complete();
        using ImmutablePayload payload = writer.TakeCompletedPayload();
        validation.Validate("d", "evt", 1, "json", payload, cancellation.Token);
        calls.ShouldBe(["schema", "identity"]);
        Should.Throw<ObjectDisposedException>(() => identityPayload!.CopyTo(0, new byte[1]));
        Should.Throw<ObjectDisposedException>(() => identityPayload!.Length);
        calls.Clear();
        var mismatch = new RegisteredEventVersionValidation(registry,
            "test-schema", schema, "{}\n"u8.ToArray(), [], "wrong-identity", identity, "{}\n"u8.ToArray(), []);
        Should.Throw<InvalidOperationException>(() => mismatch.Validate("d", "evt", 1, "json", payload, cancellation.Token));
        calls.ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => validation.Validate("wrong-domain", "evt", 1, "json", payload, cancellation.Token));
        calls.ShouldBeEmpty();
    }

    private static EventDomainRegistry CreateRegistry()
    {
        Dictionary<string, string> fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Events", "Fixtures", "EventRegistryV17.json")))!;
        ReadOnlyMemory<byte>[] rows = [Convert.FromHexString(fixture["AliasRow"]),
            ReplaceRow(fixture["DescriptorRow"], "UIUHUHIBUHHH", 0x44),
            ReplaceRow(fixture["VersionRow"], "UHHUHHUUHH", 0x56), Convert.FromHexString(fixture["SharedRow"])];
        return new EventDomainRegistry("d", rows);
    }

    private static byte[] ReplaceRow(string hex, string fields, byte tag)
    {
        var reader = new EventEvolutionBinaryReader(Convert.FromHexString(hex));
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(reader.ReadByte()); writer.WriteString(reader.ReadString(64)); writer.WriteString(reader.ReadString(64));
        if (tag == 0x56) { writer.WriteInt32(reader.ReadInt32()); }
        writer.WriteUInt16(reader.ReadUInt16());
        for (int field = 1; field <= fields.Length; field++)
        {
            writer.WriteByte(reader.ReadByte());
            if (fields[field - 1] == 'U')
            {
                string value = reader.ReadString(4096);
                writer.WriteString(tag == 0x44 && field == 3 ? typeof(AllowlistedCurrentEventTestValue).AssemblyQualifiedName!
                    : tag == 0x56 && field == 4 ? "test-serializer"
                    : tag == 0x56 && field == 1 ? "test-schema"
                    : tag == 0x56 && field == 8 ? "test-identity" : value);
            }
            else if (fields[field - 1] == 'H')
            {
                byte[] value = reader.ReadHash().ToArray();
                if (tag == 0x44 && field == 4 || tag == 0x56 && field is 2 or 5 or 9)
                {
                    using FileStream stream = File.OpenRead(typeof(AllowlistedCurrentEventTestValue).Assembly.Location);
                    value = SHA256.HashData(stream);
                }
                if (tag == 0x56 && field is 3 or 6 or 10) { value = EventOptionsManifestCodec.ComputeHash("{}\n"u8.ToArray(), []); }
                writer.WriteHash(value);
            }
            else if (fields[field - 1] == 'I') { writer.WriteInt32(reader.ReadInt32()); }
            else { writer.WriteBytes(reader.ReadBytes(4096)); }
        }
        reader.RequireEnd();
        return writer.CopyEncodedBytes();
    }

}
